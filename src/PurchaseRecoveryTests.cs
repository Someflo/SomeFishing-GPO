using System;
using System.Collections.Generic;
using System.Drawing;

namespace SomeFishingGPO
{
    public static class PurchaseRecoveryTests
    {
        private static Settings Options()
        {return new Settings{AutoBuyBait=true,BuyQuantity=50,ShopButtonsSet=true,ShopLeftPoint=new Point(100,300),ShopMiddlePoint=new Point(250,300),ShopRightPoint=new Point(400,300),ShopOpenMilliseconds=1000,ShopSettleMilliseconds=700,ShopRetryLimit=2,ShopPhaseTimeoutSeconds=3};}
        private static bool Terminal(IPurchaseFlow flow){return flow.State==PurchasePhase.Complete||flow.State==PurchasePhase.Failed;}
        private static void Finish(IPurchaseFlow flow,Fake runtime)
        {for(double now=runtime.Now+50;now<=90050&&!Terminal(flow);now+=50){runtime.Now=now;flow.Tick(now,null,0);}}
        private static DirectPurchaseController Purchase(Fake runtime,Settings options=null)
        {var flow=new DirectPurchaseController(options??Options(),runtime,runtime);flow.Start(0);Finish(flow,runtime);return flow;}
        private static ShopRecoveryController Recover(Fake runtime,Settings options=null)
        {var flow=new ShopRecoveryController(options??Options(),runtime,runtime);flow.Start(0);Finish(flow,runtime);return flow;}
        public static void Run(Action<bool,string> check)
        {
            VisualCases(check);
            var fake=new Fake();var direct=Purchase(fake);
            check(direct.State==PurchasePhase.Complete&&direct.CompletionObserved&&direct.Submitted&&fake.Buys==1&&fake.Digits=="50","Purchase recognizes Confirm, Quantity, Done and stable absence before reporting observed completion");
            check(fake.InvalidInputs==0&&fake.RightClicks==0,"Normal purchase directs each action to its recognized menu without cancelling");
            fake=new Fake{DropOpen=1};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Complete&&fake.Openings==2&&fake.Buys==1,"A missed E retries only after fresh stable absence, then sends one purchase");
            fake=new Fake{DropYes=1};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Complete&&fake.YesClicks==2&&fake.Buys==1,"A missed Yes retries only while the confirmation menu is still visible");
            fake=new Fake{DropClose=1};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Complete&&fake.CloseClicks==2&&fake.Buys==1&&direct.CompletionObserved,"A missed closing click retries the three dots without repeating Buy");
            fake=new Fake{DropYes=99};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Failed&&fake.YesClicks==3&&fake.Buys==0&&fake.Digits=="","Two configured retries limit a stuck confirmation to three Yes clicks and never type");
            var noRetry=Options();noRetry.ShopRetryLimit=0;fake=new Fake{DropYes=1};direct=Purchase(fake,noRetry);
            check(direct.State==PurchasePhase.Failed&&fake.YesClicks==1&&!direct.Submitted,"Zero retries leaves one initial attempt before reporting a stuck phase");
            fake=new Fake{HoldQuantityAfterBuy=true};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Failed&&direct.Submitted&&!direct.CompletionObserved&&fake.Buys==1&&fake.CloseClicks==0,"A submitted purchase that remains in Quantity is never submitted twice or treated as the closing dialog");
            fake=new Fake{ThrowBuy=true};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Failed&&direct.Submitted&&fake.Buys==1&&!direct.CompletionObserved,"An input exception during Buy preserves an uncertain submitted order and blocks automatic resubmission");
            fake=new Fake{UnknownAfterBuy=true};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Failed&&fake.Buys==1&&fake.CloseClicks==0&&!direct.CompletionObserved,"Unknown post-purchase imagery cannot authorize the center closing click or inventory credit");
            fake=new Fake{VisualMode="stale"};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Failed&&fake.YesClicks==0&&fake.Buys==0&&fake.Openings==1,"Stale confirmation imagery cannot authorize Yes or an opening retry");
            fake=new Fake{VisualMode="same"};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Failed&&fake.YesClicks==0,"Repeated cached confirmation is not counted as two fresh observations");
            fake=new Fake{VisualMode="future"};direct=Purchase(fake);
            check(direct.State==PurchasePhase.Failed&&fake.YesClicks==0,"Future-dated menu imagery cannot authorize a click");
            fake=new Fake{OpenDelay=1600};var delayed=Options();delayed.ShopPhaseTimeoutSeconds=4;direct=Purchase(fake,delayed);
            check(direct.State==PurchasePhase.Complete&&fake.Openings==1&&fake.YesClicks==1&&fake.InvalidInputs==0,"A late opening dialog waits for recognition without sending premature Yes or extra E");
            fake=new Fake();direct=new DirectPurchaseController(Options(),fake,fake);direct.Start(0);
            while(fake.Reads<6&&!Terminal(direct)&&fake.Now<10000){fake.Now+=50;direct.Tick(fake.Now,null,0);}
            fake.Menu=ShopMenuKind.Unknown;fake.Now+=1000;direct.Tick(fake.Now,null,0);Finish(direct,fake);
            check(direct.State==PurchasePhase.Failed&&fake.YesClicks==0&&fake.InvalidInputs==0,"A long scheduling pause invalidates the old confirmation before a queued Yes click");

            foreach(ShopMenuKind menu in new[]{ShopMenuKind.Confirm,ShopMenuKind.Quantity,ShopMenuKind.Done}){
                fake=new Fake{Menu=menu};var recovery=Recover(fake);
                check(recovery.State==PurchasePhase.Complete&&fake.Buys==0&&fake.Openings==0&&fake.Digits==""&&fake.InvalidInputs==0,
                    "Recovery of "+menu+" closes only the matched menu without typing, opening or buying");
                check(menu==ShopMenuKind.Done?fake.CloseClicks==1&&fake.RightClicks==0:fake.RightClicks==1&&fake.CloseClicks==0,
                    "Recovery of "+menu+" chooses the correct Cancel or three-dot target");
            }
            fake=new Fake{Menu=ShopMenuKind.Absent};var clean=Recover(fake);
            check(clean.State==PurchasePhase.Complete&&fake.Aims==0&&fake.Clicks==0&&fake.Reads>=3,"Already absent menus complete recovery after stable frames without any input");
            fake=new Fake{Menu=ShopMenuKind.Unknown};clean=Recover(fake);
            check(clean.State==PurchasePhase.Failed&&fake.Clicks==0&&fake.Aims==0,"Recovery refuses to guess a cancellation target from an unknown menu");
            fake=new Fake{Menu=ShopMenuKind.Quantity,IgnoreCancel=true};clean=Recover(fake);
            check(clean.State==PurchasePhase.Failed&&fake.RightClicks==3&&fake.Buys==0,"Recovery retries a stuck cancellation only up to the configured attempt limit");
            fake=new Fake{Menu=ShopMenuKind.Confirm,ChangeDuringAim=true};clean=Recover(fake);
            check(clean.State==PurchasePhase.Complete&&fake.RightClicks==0&&fake.CloseClicks==1&&fake.Aims==2&&fake.InvalidInputs==0,"A menu change during gradual aiming invalidates the old target and re-identifies the closing menu");
            fake=new Fake{Menu=ShopMenuKind.Quantity,VisualMode="same"};clean=Recover(fake);
            check(clean.State==PurchasePhase.Failed&&fake.Clicks==0,"Cached recovery frames never authorize Cancel");
            fake=new Fake{Menu=ShopMenuKind.Quantity};clean=new ShopRecoveryController(Options(),fake,fake);clean.Start(0);
            for(int i=1;i<=4;i++){fake.Now=i*50;clean.Tick(fake.Now,null,0);}fake.Active=false;fake.Now+=50;clean.Tick(fake.Now,null,0);int clicks=fake.Clicks;fake.Now+=10000;clean.Tick(fake.Now,null,0);
            check(clean.State==PurchasePhase.Failed&&fake.Clicks==clicks&&!fake.Pending&&fake.Held.Count==0,"Focus loss during recovery cancels pending movement and any delayed click");
        }
        private static Bitmap Patch()
        {var image=new Bitmap(49,33);using(var g=Graphics.FromImage(image))g.Clear(Color.FromArgb(28,24,20));return image;}
        private static void Letter(Bitmap image,Color color)
        {using(var g=Graphics.FromImage(image))using(var brush=new SolidBrush(color)){g.FillRectangle(brush,15,10,3,12);g.FillRectangle(brush,21,10,3,12);}}
        private static void VisualCases(Action<bool,string> check)
        {
            using(var left=Patch())using(var middle=Patch())using(var right=Patch()){
                check(ShopVisual.Analyze(left,middle,right).Kind==ShopMenuKind.Absent,"Blank marked button patches classify as absent rather than a usable menu");
                Letter(left,Color.White);Letter(right,Color.White);
                check(ShopVisual.Analyze(left,middle,right).Kind==ShopMenuKind.Confirm,"White side labels with an empty center identify the initial Yes / No dialog");
                Letter(middle,Color.White);
                check(ShopVisual.Analyze(left,middle,right).Kind==ShopMenuKind.Unknown,"Conflicting center and side white text remains unknown instead of authorizing Yes");
            }
            using(var left=Patch())using(var middle=Patch())using(var right=Patch()){
                using(var g=Graphics.FromImage(middle))for(int x=14;x<=30;x+=8)g.FillRectangle(Brushes.White,x,18,3,3);
                check(ShopVisual.Analyze(left,middle,right).Kind==ShopMenuKind.Done,"Three aligned separate small white dots identify the closing dialog");
                Letter(left,Color.White);
                check(ShopVisual.Analyze(left,middle,right).Kind==ShopMenuKind.Unknown,"Side text prevents treating three central specks as a closing dialog");
            }
            using(var left=Patch())using(var middle=Patch())using(var right=Patch()){
                Letter(middle,Color.White);
                check(ShopVisual.Analyze(left,middle,right).Kind==ShopMenuKind.Unknown,"An isolated central quantity digit is not accepted as a three-dot close button");
            }
        }
        private sealed class Fake : IGameRuntime,IShopRuntime,IShopPointerRuntime,IShopVisualRuntime
        {
            internal bool Active=true,Pending,IgnoreCancel,HoldQuantityAfterBuy,ThrowBuy,UnknownAfterBuy,ChangeDuringAim;
            internal int DropOpen,DropYes,DropClose,Openings,YesClicks,Buys,CloseClicks,RightClicks,Aims,Reads,InvalidInputs;
            internal double Now,OpenDelay,openVisibleAt=-1,aimAt;
            internal ShopMenuKind Menu=ShopMenuKind.Absent;
            internal string VisualMode="fresh",Digits="";
            internal readonly HashSet<int> Held=new HashSet<int>();
            private Point target,position;
            internal int Clicks {get{return YesClicks+Buys+CloseClicks+RightClicks;}}
            public bool IsActive {get{return Active;}}
            public void Release(){Held.Clear();Pending=false;}
            public void ShopAim(Point point){Release();target=point;Pending=true;aimAt=Now+100;Aims++;}
            public bool TickShopAim(double now){if(now<aimAt)return false;Pending=false;position=target;if(ChangeDuringAim){ChangeDuringAim=false;Menu=ShopMenuKind.Done;}return true;}
            public void ShopKey(int key,bool down){
                if(down){Held.Add(key);if(key>=0x30&&key<=0x39){if(Menu!=ShopMenuKind.Quantity)InvalidInputs++;Digits+=(char)key;}}
                else{Held.Remove(key);if(key==0x45){Openings++;if(Openings>DropOpen){if(OpenDelay==0)Menu=ShopMenuKind.Confirm;else openVisibleAt=Now+OpenDelay;}}}
            }
            public void ShopClick(Point point,ShopClickKind kind){
                if(Pending||position!=point||!Active||Held.Count>0){InvalidInputs++;throw new InvalidOperationException("Unsettled test input");}
                if(point.X==400&&(Menu==ShopMenuKind.Confirm||Menu==ShopMenuKind.Quantity)){RightClicks++;if(!IgnoreCancel)Menu=ShopMenuKind.Absent;}
                else if(point.X==100&&Menu==ShopMenuKind.Confirm){YesClicks++;if(YesClicks>DropYes)Menu=ShopMenuKind.Quantity;}
                else if(point.X==100&&Menu==ShopMenuKind.Quantity){Buys++;if(ThrowBuy)throw new InvalidOperationException("Uncertain Buy");if(!HoldQuantityAfterBuy)Menu=UnknownAfterBuy?ShopMenuKind.Unknown:ShopMenuKind.Done;}
                else if(point.X==250&&Menu==ShopMenuKind.Done){CloseClicks++;if(CloseClicks>DropClose)Menu=ShopMenuKind.Absent;}
                else if(point.X!=250||Menu!=ShopMenuKind.Quantity){InvalidInputs++;throw new InvalidOperationException("Wrong menu for click");}
            }
            public ShopVisualReading ReadShopVisual(double now){
                Reads++;if(openVisibleAt>=0&&now>=openVisibleAt){openVisibleAt=-1;Menu=ShopMenuKind.Confirm;}
                return new ShopVisualReading{Menu=Menu,Sequence=VisualMode=="same"?1:Reads,SampledAt=VisualMode=="stale"?now-501:VisualMode=="future"?now+1:now,Detail="Synthetic menu"};
            }
            public ShopReading ReadShop(double now){throw new InvalidOperationException("No OCR");}
            public BaitReading ReadBait(double now){throw new InvalidOperationException("No OCR");}
            public Observation Observe(){throw new InvalidOperationException("No fishing");}
            public void MoveToCastPoint(){throw new InvalidOperationException("No cast");}
            public void SetHeld(bool held){throw new InvalidOperationException("No fishing");}
            public void SetJumpHeld(bool held){throw new InvalidOperationException("No jump");}
        }
    }
}
