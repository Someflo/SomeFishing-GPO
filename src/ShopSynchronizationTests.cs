using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SomeFishingGPO
{
    public static class ShopSynchronizationTests
    {
        private static Settings Options()
        {return new Settings{AutoBuyBait=true,UseDirectShopFlow=true,BuyQuantity=2,ShopOpenMilliseconds=1000,ShopSettleMilliseconds=700,ShopButtonsSet=true,ShopLeftPoint=new Point(1104,925),ShopMiddlePoint=new Point(1254,926),ShopRightPoint=new Point(1410,926)};}
        private static bool Terminal(IPurchaseFlow flow){return flow.State==PurchasePhase.Complete||flow.State==PurchasePhase.Failed;}
        private static void At(IPurchaseFlow flow,Fake game,double now){game.Advance(now);flow.Tick(now,null,0);}
        private static void Until(IPurchaseFlow flow,Fake game,Func<bool> done,double budget=30000)
        {double end=game.Now+budget;for(double now=game.Now+50;now<=end&&!Terminal(flow)&&!done();now+=50)At(flow,game,now);}
        public static void Run(Action<bool,string> check,string output)
        {
            var game=new Fake{AimDelay=450};IPurchaseFlow flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);
            Until(flow,game,delegate{return game.Aims.Count==1;});double aimed=game.Now;
            At(flow,game,aimed+449);check(game.Clicks.Count==0&&game.Arrivals.Count==0,"Shop synchronization never clicks while pointer movement is pending");
            At(flow,game,aimed+450);At(flow,game,aimed+1149);
            check(game.Arrivals.Count==1&&game.Clicks.Count==0,"The full 700 ms settling delay starts after pointer arrival, not after requesting movement");
            At(flow,game,aimed+1150);Until(flow,game,delegate{return game.Clicks.Count==1;},500);check(game.Clicks.Count==1&&game.Clicks[0].At-game.Arrivals[0]>=700,"The initial Yes click occurs only after the arrived pointer has settled and fresh confirmation is visible");
            Until(flow,game,delegate{return Terminal(flow);});
            check(flow.State==PurchasePhase.Complete&&flow.Submitted&&game.Digits=="2"&&game.Clicks.Count==5,"Fresh quantity-menu confirmations allow the complete synchronized purchase sequence");
            check(game.Reads>=6&&game.TextReads==0&&game.FishingReads==0,"Synchronized direct buying verifies visual presence without any text OCR or fishing reads");
            check(game.Clicks[2].At-game.Clicks[1].At==250&&game.Aims.Count==4,"Synchronization preserves the number's 250 ms double click and four explicit aiming stages");
            check(game.Arrivals.Count==4&&game.Clicks[3].At-game.Arrivals[2]>=700&&game.Clicks[4].At-game.Arrivals[3]>=700,"Buy and closing clicks also wait the full settling delay after arrival");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"shop-synchronization.txt"),game.Events.ToArray());}

            game=new Fake{AimDelay=1000};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return game.Aims.Count==1;});game.Active=false;At(flow,game,game.Now+50);int events=game.InputCount,polls=game.PointerPolls;
            At(flow,game,game.Now+20000);check(flow.State==PurchasePhase.Failed&&game.PointerCancelled&&game.Clicks.Count==0&&game.InputCount==events&&game.PointerPolls==polls,"Focus loss during movement cancels the pending aim and prevents later polling, clicks and keys");
            game=new Fake{AimDelay=1000};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return game.Aims.Count==1;});flow.Fail("F10");events=game.InputCount;At(flow,game,game.Now+20000);
            check(flow.State==PurchasePhase.Failed&&game.PointerCancelled&&game.InputCount==events&&game.Clicks.Count==0,"F10 during pointer movement cannot leave a delayed click behind");
            game=new Fake{AimDelay=100,ThrowAim=true};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return Terminal(flow);});
            check(flow.State==PurchasePhase.Failed&&game.Clicks.Count==0&&game.KeysDown.Count==0,"A failed pointer driver stops before Yes and releases all held keys");

            game=new Fake{Mode="missing"};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return Terminal(flow);});
            check(flow.State==PurchasePhase.Failed&&game.Clicks.Count==1&&game.Aims.Count==1&&!game.Keys.Any(k=>k!=0x45)&&!flow.Submitted,"If Yes does not open the quantity menu, synchronization blocks the number, typing and Buy");
            check(game.FirstReadAt>=0&&game.Now-game.FirstReadAt>=9800&&game.Now-game.FirstReadAt<=10200,"An unknown quantity menu fails at the configured ten second phase timeout without retrying Yes");
            events=game.InputCount;At(flow,game,game.Now+100000);check(game.InputCount==events,"Failed visual confirmation cannot emit future inputs");

            game=new Fake{Mode="lost_while_aiming_number"};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return Terminal(flow);});
            check(flow.State==PurchasePhase.Failed&&game.Aims.Count==2&&game.Clicks.Count==1&&game.Keys.All(k=>k==0x45)&&!flow.Submitted,"A quantity menu lost during pointer movement to the field prevents both number clicks, typing and Buy");

            game=new Fake{Mode="lost_before_selection"};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return Terminal(flow);});
            check(flow.State==PurchasePhase.Failed&&game.Clicks.Count==3&&!game.Keys.Contains(0x11)&&!game.Keys.Contains(0x41)&&!flow.Submitted,"Losing the quantity menu after the double click prevents Ctrl+A and any typing");
            game=new Fake{Mode="lost_before_buy"};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return Terminal(flow);});
            check(flow.State==PurchasePhase.Failed&&game.Digits=="2"&&game.Clicks.Count==3&&!flow.Submitted&&game.KeysDown.Count==0,"Losing the quantity menu after typing prevents Buy and releases the keyboard");

            foreach(string mode in new[]{"stale","future","same_frame","before_gate","single_then_missing","alternating"}){
                game=new Fake{Mode=mode};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return Terminal(flow);});
                check(flow.State==PurchasePhase.Failed&&game.Clicks.Count==1&&game.Aims.Count==1&&game.Keys.All(k=>k==0x45)&&!flow.Submitted,"Visual mode "+mode+" cannot authorize the number or keyboard from invalid/contradictory evidence");
            }
            game=new Fake{Mode="throws"};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return Terminal(flow);});
            check(flow.State==PurchasePhase.Failed&&game.Clicks.Count==1&&game.KeysDown.Count==0,"A visual reader exception fails safely without proceeding to the field");

            game=new Fake{Mode="manual"};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return game.FirstReadAt>=0;});double gate=game.Now;
            game.Manual=new ShopVisualReading{QuantityMenu=true,Sequence=10,SampledAt=gate+1,Detail="first fresh"};At(flow,game,gate+1);
            check(game.Aims.Count==1,"One fresh quantity-menu frame does not authorize moving to the number");At(flow,game,gate+50);
            check(game.Aims.Count==1,"Reading the same cached visual frame again does not satisfy two-frame confirmation");
            game.Manual=new ShopVisualReading{QuantityMenu=true,Sequence=11,SampledAt=gate+99,Detail="too close"};At(flow,game,gate+99);
            check(game.Aims.Count==1,"Two visual samples separated by less than 100 ms cannot authorize the next stage");
            game.Manual=new ShopVisualReading{QuantityMenu=true,Sequence=12,SampledAt=gate+251,Detail="second spaced fresh"};At(flow,game,gate+251);Until(flow,game,delegate{return game.Aims.Count==2;},300);
            check(game.Aims.Count==2&&flow.State!=PurchasePhase.Failed,"Two distinct fresh samples spaced by at least 100 ms advance to the number");flow.Fail("test");
        }

        private sealed class ClickEvent {internal Point Point;internal double At;internal ShopClickKind Kind;}
        private sealed class Fake : IGameRuntime,IShopRuntime,IShopPointerRuntime,IShopVisualRuntime
        {
            internal bool Active=true,PointerCancelled,ThrowAim,PointerPending,MouseHeld;
            internal double Now,AimDelay=100,AimReadyAt,mouseUpAt,FirstReadAt=-1;
            internal int PointerPolls,Reads,TextReads,FishingReads;
            internal string Mode="good";
            internal ShopVisualReading Manual=new ShopVisualReading();
            internal readonly List<Point> Aims=new List<Point>();
            internal readonly List<double> Arrivals=new List<double>();
            internal readonly List<ClickEvent> Clicks=new List<ClickEvent>();
            internal readonly List<int> Keys=new List<int>();
            internal readonly HashSet<int> KeysDown=new HashSet<int>();
            internal readonly List<string> Events=new List<string>();
            internal Point Target,Position;
            internal string Digits {get{return string.Concat(Keys.Where(k=>k>=0x30&&k<=0x39).Select(k=>((char)k).ToString()));}}
            internal int InputCount {get{return Aims.Count+Clicks.Count+Keys.Count;}}
            private void Log(string text){Events.Add(Now.ToString(CultureInfo.InvariantCulture)+" ms: "+text);}
            internal void Advance(double now){Now=now;if(MouseHeld&&now>=mouseUpAt)MouseHeld=false;}
            public bool IsActive {get{return Active;}}
            public void Release(){MouseHeld=false;KeysDown.Clear();if(PointerPending)PointerCancelled=true;PointerPending=false;}
            public void ShopAim(Point point){if(!Active)throw new InvalidOperationException("Inactive pointer");Release();PointerCancelled=false;PointerPending=true;Target=point;AimReadyAt=Now+AimDelay;Aims.Add(point);Log("Aim start "+point);}
            public bool TickShopAim(double now){PointerPolls++;if(!Active||PointerCancelled)throw new InvalidOperationException("Cancelled pointer poll");if(ThrowAim)throw new InvalidOperationException("Pointer could not arrive");if(!PointerPending)return true;if(now<AimReadyAt)return false;PointerPending=false;Position=Target;Arrivals.Add(now);Log("Aim arrived "+Target);return true;}
            public void ShopClick(Point point,ShopClickKind kind){if(!Active||PointerPending||Position!=point||MouseHeld||KeysDown.Count!=0)throw new InvalidOperationException("Unsettled fake click");MouseHeld=true;mouseUpAt=Now+180;Clicks.Add(new ClickEvent{Point=point,Kind=kind,At=Now});Log("Click "+point);}
            public void ShopKey(int key,bool held){if(held){if(!Active||PointerPending||MouseHeld)throw new InvalidOperationException("Unsafe key");Keys.Add(key);KeysDown.Add(key);Log("Key "+key);}else KeysDown.Remove(key);}
            public ShopVisualReading ReadShopVisual(double now){
                Reads++;Log("Visual "+Mode+" #"+Reads);
                if(Clicks.Count==0)return new ShopVisualReading{Menu=ShopMenuKind.Confirm,Sequence=Reads,SampledAt=now};
                if(Clicks.Count==4)return new ShopVisualReading{Menu=ShopMenuKind.Done,Sequence=Reads,SampledAt=now};
                if(Clicks.Count>=5)return new ShopVisualReading{Menu=ShopMenuKind.Absent,Sequence=Reads,SampledAt=now};
                if(FirstReadAt<0)FirstReadAt=now;
                if(Mode=="throws")throw new InvalidOperationException("Visual capture failed");if(Mode=="manual")return Manual;
                bool present=Clicks.Count>=1&&Clicks.Count<4;
                if(Mode=="missing")present=false;
                if(Mode=="lost_while_aiming_number"&&Aims.Count>=2)present=false;
                if(Mode=="lost_before_selection"&&Clicks.Count>=3)present=false;
                if(Mode=="lost_before_buy"&&Digits.Length>0)present=false;
                if(Mode=="single_then_missing")present=now==FirstReadAt;
                if(Mode=="alternating")present=Reads%2==1;
                double sampled=Mode=="stale"?now-501:Mode=="future"?now+1:Mode=="same_frame"?FirstReadAt:Mode=="before_gate"?FirstReadAt-200:now;
                return new ShopVisualReading{QuantityMenu=present,Sequence=Mode=="same_frame"?1:Reads,SampledAt=sampled,Detail="Synthetic visual frame"};
            }
            public ShopReading ReadShop(double now){TextReads++;throw new InvalidOperationException("No menu OCR allowed");}
            public BaitReading ReadBait(double now){TextReads++;throw new InvalidOperationException("No counter OCR allowed");}
            public Observation Observe(){FishingReads++;throw new InvalidOperationException("No fishing capture allowed");}
            public void MoveToCastPoint(){throw new InvalidOperationException("No recast allowed");}
            public void SetHeld(bool held){throw new InvalidOperationException("No fishing input allowed");}
            public void SetJumpHeld(bool held){throw new InvalidOperationException("No jump allowed");}
        }
    }
}
