using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SomeFishingGPO
{
    public static class DirectPurchaseTests
    {
        private static Settings Options(int quantity=298)
        {
            return new Settings{AutoBuyBait=true,BuyMaximum=true,BuyQuantity=quantity,
                ShopOpenMilliseconds=1000,ShopSettleMilliseconds=700,ShopButtonsSet=true,
                ShopLeftPoint=new Point(1102,928),ShopMiddlePoint=new Point(1251,931),ShopRightPoint=new Point(1410,931)};
        }
        private static bool Terminal(IPurchaseFlow flow)
        {return flow.State==PurchasePhase.Complete||flow.State==PurchasePhase.Failed;}
        private static void At(IPurchaseFlow flow,Fake game,double time)
        {game.Advance(time);flow.Tick(time,null,0);}
        private static void Finish(IPurchaseFlow flow,Fake game)
        {for(double time=game.Now+50;time<=60000&&!Terminal(flow);time+=50)At(flow,game,time);}
        private static void Until(IPurchaseFlow flow,Fake game,Func<bool> condition)
        {for(double time=game.Now+50;time<=60000&&!Terminal(flow)&&!condition();time+=50)At(flow,game,time);}
        public static void Run(Action<bool,string> check,string output)
        {
            Settings options=Options();DirectPurchaseTargets targets=DirectPurchaseTargets.ForSettings(options);
            check(targets.Yes==options.ShopLeftPoint&&targets.Buy==targets.Yes&&targets.Quantity==options.ShopMiddlePoint&&targets.Close==targets.Quantity&&targets.Right==options.ShopRightPoint,"Direct purchase uses the three marked points; Yes/Buy share left and Quantity/Close share middle");
            var negative=Options();negative.ShopLeftPoint=new Point(-1800,750);negative.ShopMiddlePoint=new Point(-1600,754);negative.ShopRightPoint=new Point(-1400,754);
            check(DirectPurchaseTargets.ForSettings(negative).Quantity==negative.ShopMiddlePoint,"Marked buttons support valid negative monitor coordinates without recalculating percentages");

            var game=new Fake();IPurchaseFlow flow=new DirectPurchaseController(options,game,game);flow.Start(0);
            options.BuyQuantity=5;options.ShopMiddlePoint=new Point(1300,950);Finish(flow,game);
            check(flow.State==PurchasePhase.Complete&&flow.Submitted&&flow.Quantity==298,"Direct flow completes one order of 298, preserving its start-time quantity");
            check(flow.Status=="Secuencia enviada; resultado sin verificar"&&flow.Diagnostic.Contains("resultado sin verificar"),"Direct completion explicitly leaves the purchase result unverified");
            check(game.Reads==0&&game.FishingActions==0,"Direct flow never reads shop/bait/fishing images and never casts or jumps");
            check(game.Clicks.Count==5&&game.Clicks.Select(c=>c.Kind).SequenceEqual(new[]{ShopClickKind.Button,ShopClickKind.Quantity,ShopClickKind.Quantity,ShopClickKind.Button,ShopClickKind.Button}),"Direct flow clicks Yes once, the number twice, Buy once and closing once");
            check(game.Clicks.Select(c=>c.Point).SequenceEqual(new[]{targets.Yes,targets.Quantity,targets.Quantity,targets.Buy,targets.Close}),"All direct click destinations use the marked positions captured at start");
            check(game.Aims.SequenceEqual(new[]{targets.Yes,targets.Quantity,targets.Buy,targets.Close})&&!game.Clicks.Any(c=>c.Point==targets.Right),"Direct flow aims at each required button and never clicks No/Cancelar");
            check(game.Keys.Where(k=>k.Down).Select(k=>k.Key).SequenceEqual(new[]{0x45,0x11,0x41,0x08,0x32,0x39,0x38}),"Direct typing sends E, Ctrl+A, Backspace and exactly the digits 298");
            check(game.Clicks[2].At-game.Clicks[1].At==250&&game.Clicks[1].At+180<=game.Clicks[2].At,"Direct number double-click allows its 180 ms pulse to release before the next press");
            check(game.KeysDown.Count==0&&!game.MouseHeld&&game.MouseUps==5,"Direct completion releases every key and each mouse pulse");
            int count=game.InputCount;At(flow,game,300000);flow.Start(300000);
            check(game.InputCount==count&&flow.State==PurchasePhase.Complete,"Completed direct orders cannot repeat, recast, restart or emit future inputs");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"direct-sequence.txt"),game.Events.ToArray());}

            foreach(int quantity in new[]{1,50,9999}){
                game=new Fake();flow=new DirectPurchaseController(Options(quantity),game,game);flow.Start(0);Finish(flow,game);
                string written=string.Concat(game.Keys.Where(k=>k.Down&&k.Key>=0x30&&k.Key<=0x39).Select(k=>((char)k.Key).ToString()));
                check(flow.State==PurchasePhase.Complete&&flow.Quantity==quantity&&written==quantity.ToString(CultureInfo.InvariantCulture)&&game.Clicks.Count==5,"Direct explicit quantity "+quantity+" is typed once without an invented MAX or inventory reading");
            }

            game=new Fake();flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);
            At(flow,game,999);check(game.KeysDown.Contains(0x45)&&game.Aims.Count==0,"Direct flow sustains E for its full configured duration");
            At(flow,game,1000);At(flow,game,2499);check(!game.KeysDown.Contains(0x45)&&game.Aims.Count==0,"Direct flow waits at least 1500 ms after E before targeting Yes");
            At(flow,game,2500);At(flow,game,3199);check(game.Aims.Count==1&&game.Clicks.Count==0,"Direct Yes waits for the configured pointer settling time");
            At(flow,game,3200);At(flow,game,4699);check(game.Clicks.Count==1&&game.Aims.Count==1,"Direct flow waits for the quantity dialog after its single Yes click");
            At(flow,game,4700);At(flow,game,5400);At(flow,game,5649);check(game.Clicks.Count==2&&!game.MouseHeld,"Direct second number click does not happen before its 250 ms deadline");
            At(flow,game,5650);At(flow,game,6349);check(game.Clicks.Count==3&&!game.KeysDown.Contains(0x11),"Direct typing waits after the second number click");
            At(flow,game,6350);check(game.KeysDown.Contains(0x11),"Direct selection starts Ctrl only after the field settling pause");flow.Fail("test");

            options=Options();options.ShopSettleMilliseconds=2500;game=new Fake();flow=new DirectPurchaseController(options,game,game);flow.Start(0);
            At(flow,game,1000);At(flow,game,3499);check(game.Aims.Count==0,"Direct menu wait honors a configured pause longer than 1500 ms");At(flow,game,3500);check(game.Aims.Count==1,"Direct long menu wait ends at its own deadline");flow.Fail("test");
            game=new Fake();flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);At(flow,game,4000);At(flow,game,4000);
            check(game.Keys.Count==2&&game.Aims.Count==0&&game.Clicks.Count==0,"A delayed direct tick releases E without catching up through future actions");flow.Fail("test");

            game=new Fake();flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return game.Clicks.Count==2;});
            flow.Fail("F10");count=game.InputCount;At(flow,game,100000);flow.Start(100000);
            check(flow.State==PurchasePhase.Failed&&game.Clicks.Count==2&&game.InputCount==count&&!game.MouseHeld&&game.KeysDown.Count==0,"F10 between number clicks releases the first pulse and cancels the second click and all typing");
            game=new Fake();flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);game.Active=false;At(flow,game,100);
            check(flow.State==PurchasePhase.Failed&&game.KeysDown.Count==0&&game.Clicks.Count==0,"Focus loss while E is held stops a direct order and releases E");
            game=new Fake();flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return game.KeysDown.Contains(0x11);});
            game.Active=false;At(flow,game,game.Now+1);count=game.InputCount;At(flow,game,90000);
            check(flow.State==PurchasePhase.Failed&&game.KeysDown.Count==0&&!game.Keys.Any(k=>k.Down&&k.Key==0x41)&&game.InputCount==count,"Focus loss during direct Ctrl selection releases it and prevents A, typing and Buy");
            game=new Fake();flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Until(flow,game,delegate{return flow.Submitted;});flow.Fail("F10");count=game.InputCount;At(flow,game,100000);
            check(flow.Submitted&&flow.State==PurchasePhase.Failed&&game.Clicks.Count==4&&game.InputCount==count,"Stopping after direct Buy preserves the submitted flag without another Buy or closing click");
            game=new Fake{ThrowNextClick=true};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);Finish(flow,game);count=game.InputCount;At(flow,game,100000);
            check(flow.State==PurchasePhase.Failed&&!game.MouseHeld&&game.KeysDown.Count==0&&game.InputCount==count,"A direct input exception releases an uncertain mouse press and cannot retry it");
            game=new Fake{Active=false};flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);
            check(flow.State==PurchasePhase.Failed&&game.InputCount==0,"A direct order cannot start when the game is inactive");
            game=new Fake();flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);At(flow,game,90001);
            check(flow.State==PurchasePhase.Failed&&game.KeysDown.Count==0&&game.Clicks.Count==0,"The 90 second direct timeout releases E without sending overdue clicks");
            game=new Fake();flow=new DirectPurchaseController(Options(),game,game);flow.Start(0);At(flow,game,100);At(flow,game,50);
            check(flow.State==PurchasePhase.Failed&&game.KeysDown.Count==0,"A backwards direct clock terminates safely");

            var invalid=new List<Settings>();
            options=Options();options.ShopButtonsSet=false;invalid.Add(options);
            options=Options();options.ShopMiddlePoint=options.ShopLeftPoint;invalid.Add(options);
            options=Options();options.ShopRightPoint=new Point(1000,931);invalid.Add(options);
            options=Options();options.ShopRightPoint=new Point(int.MaxValue,931);invalid.Add(options);
            options=Options();options.ShopMiddlePoint=new Point(1251,int.MaxValue);invalid.Add(options);
            options=Options(0);invalid.Add(options);options=Options(10000);invalid.Add(options);
            options=Options();options.ShopOpenMilliseconds=0;invalid.Add(options);
            options=Options();options.ShopOpenMilliseconds=3001;invalid.Add(options);
            options=Options();options.ShopSettleMilliseconds=199;invalid.Add(options);
            options=Options();options.ShopSettleMilliseconds=3001;invalid.Add(options);
            options=Options();options.AutoBuyBait=false;invalid.Add(options);
            for(int i=0;i<invalid.Count;i++){
                game=new Fake();flow=new DirectPurchaseController(invalid[i],game,game);flow.Start(0);At(flow,game,100000);
                check(flow.State==PurchasePhase.Failed&&game.InputCount==0&&game.Reads==0,"Invalid direct configuration "+i+" cannot emit input or read OCR");
            }
            game=new Fake();flow=new DirectPurchaseController(Options(),game,game);At(flow,game,500);check(game.InputCount==0,"Tick before direct Start sends no input");flow.Fail("F10");flow.Start(1000);check(game.InputCount==0&&flow.State==PurchasePhase.Failed,"Cancellation before direct Start prevents a later start on the same order");
        }

        private sealed class ClickEvent { internal Point Point;internal ShopClickKind Kind;internal double At; }
        private sealed class KeyEvent { internal int Key;internal bool Down; }
        private sealed class Fake : IGameRuntime,IShopRuntime
        {
            internal bool Active=true,MouseHeld,ThrowNextClick;
            internal double Now,mouseReleaseAt;
            internal int Reads,FishingActions,MouseUps;
            internal Point Aimed;
            internal readonly HashSet<int> KeysDown=new HashSet<int>();
            internal readonly List<ClickEvent> Clicks=new List<ClickEvent>();
            internal readonly List<KeyEvent> Keys=new List<KeyEvent>();
            internal readonly List<Point> Aims=new List<Point>();
            internal readonly List<string> Events=new List<string>();
            internal int InputCount { get {return Aims.Count+Clicks.Count+Keys.Count;} }
            public bool IsActive { get {return Active;} }
            private void Log(string text){Events.Add(Now.ToString(CultureInfo.InvariantCulture)+" ms: "+text);}
            internal void Advance(double now){Now=now;if(MouseHeld&&now>=mouseReleaseAt){MouseHeld=false;MouseUps++;Log("Mouse up");}}
            public void Release(){if(MouseHeld){MouseHeld=false;MouseUps++;Log("Mouse released");}foreach(int key in KeysDown.ToArray())ShopKey(key,false);}
            public void ShopAim(Point point){if(!Active)throw new InvalidOperationException("Inactive aim");Release();Aimed=point;Aims.Add(point);Log("Aim "+point);}
            public void ShopClick(Point point,ShopClickKind kind){
                if(!Active||Aimed!=point||MouseHeld||KeysDown.Count!=0)throw new InvalidOperationException("Unsafe or overlapping fake click");
                MouseHeld=true;mouseReleaseAt=Now+180;Clicks.Add(new ClickEvent{Point=point,Kind=kind,At=Now});Log("Click "+kind+" "+point);
                if(ThrowNextClick){ThrowNextClick=false;throw new InvalidOperationException("Simulated uncertain mouse down");}
            }
            public void ShopKey(int key,bool held){
                if(held&&(!Active||MouseHeld))throw new InvalidOperationException("Unsafe fake key");
                if(held)KeysDown.Add(key);else KeysDown.Remove(key);
                Keys.Add(new KeyEvent{Key=key,Down=held});Log("Key "+key+(held?" down":" up"));
            }
            public ShopReading ReadShop(double now){Reads++;throw new InvalidOperationException("Direct flow must not read shop OCR");}
            public BaitReading ReadBait(double now){Reads++;throw new InvalidOperationException("Direct flow must not read bait OCR");}
            public Observation Observe(){Reads++;throw new InvalidOperationException("Direct flow must not capture the fishing area");}
            public void MoveToCastPoint(){FishingActions++;throw new InvalidOperationException("Direct flow must not recast");}
            public void SetHeld(bool held){FishingActions++;throw new InvalidOperationException("Direct flow must not fish");}
            public void SetJumpHeld(bool held){FishingActions++;throw new InvalidOperationException("Direct flow must not jump");}
        }
    }
}
