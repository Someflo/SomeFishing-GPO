using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SomeFishingGPO
{
    public static class SimplePurchaseEngineTests
    {
        private static Settings Options(bool timer=false)
        {
            return new Settings{UseDirectShopFlow=true,AutoBuyBait=true,MonitorBait=true,AutoCast=false,
                Area=new Rectangle(100,100,60,300),BaitArea=new Rectangle(1050,600,50,30),
                CastPointSet=true,CastPoint=new Point(800,500),ShopButtonsSet=true,
                ShopLeftPoint=new Point(1102,928),ShopMiddlePoint=new Point(1251,931),ShopRightPoint=new Point(1410,931),
                BaitCapacity=300,BuyBaitAt=2,BuyMaximum=true,BuyQuantity=50,PurchaseByTimer=timer,
                PurchaseIntervalMinutes=timer?1:40,ShopOpenMilliseconds=1000,ShopSettleMilliseconds=700};
        }
        private static Observation Fish()
        {return new Observation{Found=true,MenuVisible=true,GapY=180,FishY=80,GapTop=150,GapBottom=210};}
        private static void At(FishingEngine engine,Fake game,double now,int? count=null,bool absent=false)
        {
            game.Advance(now);game.Bait=new BaitReading{Count=count,VisuallyAbsent=absent,Sequence=++game.Sequence,SampledAt=now};engine.Tick(now);
        }
        private static void RunOrder(FishingEngine engine,Fake game,int? count=null)
        {double end=game.Now+60000;for(double now=game.Now+50;now<=end&&engine.Running&&engine.State==Phase.Purchasing;now+=50)At(engine,game,now,count);}
        private static FishingEngine Low(Fake game,int count)
        {
            var engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,0,count);At(engine,game,50,count);
            if(count==0)At(engine,game,100,count);return engine;
        }
        public static void Run(Action<bool,string> check,string output)
        {
            var options=Options(true);options.AutoCast=true;var game=new Fake{Current=Fish()};var engine=new FishingEngine(options,game);engine.Start(0);
            At(engine,game,0);At(engine,game,50);At(engine,game,59999);
            check(engine.State==Phase.Tracking&&game.FishingHeld&&engine.PurchaseAttempts==0,"Simple timer keeps fishing until its deadline");
            At(engine,game,60000);check(engine.State==Phase.PausingPurchase&&!game.FishingHeld&&!engine.SuggestedHold&&game.KeysDown.Count==0,"Simple timer expiration pauses tracking and releases the fishing click before opening the shop");
            At(engine,game,61000);check(engine.PurchaseAttempts==0&&game.Clicks.Count==0&&game.Keys.Count==0,"A visible fish menu prevents E and all purchase clicks while paused");
            game.Current=new Observation();At(engine,game,61100);At(engine,game,62699);
            check(engine.State==Phase.PausingPurchase&&engine.PurchaseAttempts==0,"The simple timer requires 1600 ms of a clear fishing area");
            At(engine,game,62700);check(engine.State==Phase.Purchasing&&engine.PurchaseAttempts==1&&game.KeysDown.Contains(0x45),"A clear area starts one timed purchase after the pause");
            RunOrder(engine,game);double completed=game.Now;
            check(engine.Running&&engine.State==Phase.Preparing&&game.Digits=="50"&&engine.PurchaseSubmitted&&game.Clicks.Count==5,"Simple timed purchasing sends 50 and returns to preparing after the one-shot sequence");
            check(game.ShopReads==0&&game.BaitReads==0&&engine.TimerStatus(completed).Contains("01:00"),"Simple timer reads neither menu OCR nor bait OCR and restarts its full interval after buying");
            At(engine,game,completed+999);check(game.CastMoves==0,"Resuming after a simple purchase respects the one second preparation pause");
            At(engine,game,completed+1000);check(game.CastMoves==1&&engine.State==Phase.Casting&&game.FishingHeld,"Simple timer resumes automatic casting after completing its sequence");engine.Stop("test");

            game=new Fake{Current=Fish()};engine=new FishingEngine(Options(true),game);engine.Start(0);At(engine,game,0);At(engine,game,50);At(engine,game,60000);game.Active=false;At(engine,game,60050);
            int inputs=game.InputCount;At(engine,game,200000);check(!engine.Running&&!game.FishingHeld&&game.KeysDown.Count==0&&game.InputCount==inputs&&game.Clicks.Count==0,"Focus loss while pausing cancels E, clicks and future simple purchases");
            game=new Fake{Current=Fish()};engine=new FishingEngine(Options(true),game);engine.Start(0);At(engine,game,0);At(engine,game,50);At(engine,game,60000);engine.Stop("F10");inputs=game.InputCount;game.Current=new Observation();At(engine,game,100000);
            check(!engine.Running&&game.InputCount==inputs&&engine.PurchaseAttempts==0,"F10 during the fishing pause prevents a later shop sequence");

            options=Options(true);options.AutoCast=true;game=new Fake();engine=new FishingEngine(options,game);engine.Start(0);At(engine,game,59900);
            check(engine.State==Phase.Casting&&game.FishingHeld,"Cast-in-flight fixture begins its cast before the timer is due");
            At(engine,game,60000);At(engine,game,60050);At(engine,game,74999);
            check(engine.State==Phase.PausingPurchase&&engine.PurchaseAttempts==0&&!game.FishingHeld,"A timer expiring during a cast preserves the full pending bite window before buying");
            At(engine,game,75000);check(engine.State==Phase.Purchasing&&engine.PurchaseAttempts==1,"A cast without a fish can buy only after its pending bite window expires");engine.Stop("test");
            options=Options(true);options.AutoCast=true;game=new Fake();engine=new FishingEngine(options,game);engine.Start(0);At(engine,game,59000);At(engine,game,59220);At(engine,game,60000);At(engine,game,60050);At(engine,game,74219);
            check(engine.PurchaseAttempts==0&&engine.State==Phase.PausingPurchase,"A timer expiring during Waiting retains that cast's existing bite deadline");At(engine,game,74220);check(engine.PurchaseAttempts==1,"Waiting can enter the shop at the preserved bite deadline");engine.Stop("test");

            game=new Fake{Current=Fish()};engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,0,4);At(engine,game,50,4);At(engine,game,100,3);At(engine,game,150,3);
            check(engine.State==Phase.Tracking&&engine.PurchaseAttempts==0&&game.Keys.Count==0,"Confirmed 4 and 3 bait do not trigger a threshold-2 purchase");At(engine,game,200,2);
            check(engine.State==Phase.Tracking&&engine.PurchaseAttempts==0,"One low-counter frame cannot pause fishing or authorize spending");At(engine,game,250,2);
            check(engine.State==Phase.PausingPurchase&&!game.FishingHeld&&engine.PurchaseAttempts==0,"Two confirmed frames at 2 bait pause fishing before replenishment");game.Current=new Observation();At(engine,game,300,2);At(engine,game,1900,2);
            check(engine.State==Phase.Purchasing&&engine.PurchaseDetail.Contains("cantidad solicitada: 298"),"The normal simple mode computes 300 minus 2 as an order of 298");int reads=game.BaitReads;RunOrder(engine,game,2);
            check(engine.State==Phase.Preparing&&game.Digits=="298"&&game.ShopReads==0&&game.BaitReads==reads,"Normal simple purchasing uses no shop OCR and pauses counter reads while typing 298");
            double after=game.Now;for(double now=after+50;now<=after+12000&&engine.Running;now+=50)At(engine,game,now,2);
            check(engine.PurchaseAttempts==1&&game.Clicks.Count==5,"An unchanged low count after the order cannot cause repeated buying");
            At(engine,game,game.Now+50,3);At(engine,game,game.Now+50,3);At(engine,game,game.Now+50,2);At(engine,game,game.Now+50,2);
            check(engine.State==Phase.PausingPurchase&&engine.PurchaseAttempts==1,"Observing bait above the threshold rearms a later low-counter purchase");engine.Stop("test");

            game=new Fake();engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,0,0);At(engine,game,50,0);
            check(engine.State!=Phase.PausingPurchase&&engine.PurchaseAttempts==0&&!engine.BaitCount.HasValue,"Two zero readings are insufficient to authorize a 300-bait order");At(engine,game,100,0);At(engine,game,150,0);At(engine,game,1750,0);
            check(engine.State==Phase.Purchasing&&engine.PurchaseDetail.Contains("cantidad solicitada: 300"),"Three confirmed zero readings compute a 300-bait replenishment");RunOrder(engine,game,0);check(game.Digits=="300"&&engine.PurchaseAttempts==1,"Zero replenishment types 300 once");engine.Stop("test");

            game=new Fake();engine=Low(game,2);At(engine,game,100,1);At(engine,game,150,1);At(engine,game,1700,1);
            check(engine.State==Phase.Purchasing&&engine.PurchaseDetail.Contains("cantidad solicitada: 299"),"A count changing from 2 to 1 before E is recalculated as 299 at the actual purchase start");RunOrder(engine,game,1);check(game.Digits=="299","The recalculated normal order actually types 299");engine.Stop("test");
            game=new Fake{Current=Fish()};engine=Low(game,2);At(engine,game,100,null);game.Current=new Observation();At(engine,game,200,null);At(engine,game,1800,null);
            check(engine.State==Phase.PausingPurchase&&engine.PurchaseAttempts==0&&game.Keys.Count==0,"An unknown counter during the pause cannot invent the missing replenishment amount");At(engine,game,1850,1);At(engine,game,1900,1);
            check(engine.State==Phase.Purchasing&&engine.PurchaseDetail.Contains("cantidad solicitada: 299"),"Fresh confirmed counter data can finish the waiting pause without a guessed MAX");engine.Stop("test");

            game=new Fake{Current=Fish()};engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,0,5);At(engine,game,50,5);
            At(engine,game,100,null,true);At(engine,game,3000,null,true);At(engine,game,6000,null,true);At(engine,game,8100,null,true);game.Current=new Observation();At(engine,game,8200,null,true);At(engine,game,10000,null,true);At(engine,game,12000,null,true);
            check(engine.PurchaseAttempts==0&&game.Keys.Count==0&&game.ShopReads==0&&!engine.BaitCount.HasValue,"A disappeared counter cannot authorize a simple purchase or invent MAX/capacity as the current count");engine.Stop("test");
            game=new Fake();engine=new FishingEngine(Options(),game);engine.Start(0);for(double now=0;now<=10000&&engine.Running;now+=50)At(engine,game,now,null);
            check(engine.PurchaseAttempts==0&&game.Clicks.Count==0,"A never-readable counter does not spend on normal automatic replenishment");engine.Stop("test");

            foreach(int quantity in new[]{1,17}){
                options=Options();options.Area=Rectangle.Empty;options.BaitArea=Rectangle.Empty;options.ShopArea=Rectangle.Empty;options.TestBuyQuantity=quantity;
                game=new Fake{Current=Fish(),ForbidObserve=true,ForbidBait=true};engine=new FishingEngine(options,game,RunKind.PurchaseTest);engine.Start(0);At(engine,game,0);At(engine,game,1000);RunOrder(engine,game);
                check(!engine.Running&&engine.PurchaseSubmitted&&game.Digits==quantity.ToString(CultureInfo.InvariantCulture)&&game.Clicks.Count==5,"Direct PurchaseTest sends its explicit quantity "+quantity+" once and terminates");
                check(game.Observations==0&&game.BaitReads==0&&game.ShopReads==0&&game.CastMoves==0,"Direct PurchaseTest does not inspect fish/menu/counter areas or start fishing");
            }

            Rectangle desktop=new Rectangle(0,0,1920,1080);options=Options();check(options.Validate(desktop,true)==null,"A marked-button normal setup with capacity 300 validates without a shop OCR rectangle");
            Settings copy=options.ForPurchase(298);check(copy.BuyQuantity==298&&!copy.BuyMaximum&&options.BuyQuantity==50&&options.BuyMaximum,"Creating a 298-bait order preserves the saved 50-bait timer settings");
            options.BaitCapacity=2;check(options.Validate(desktop,true)!=null,"Capacity at or below the threshold is rejected");options.BaitCapacity=10000;check(options.Validate(desktop,true)!=null,"Capacity above 9999 is rejected");options.BaitCapacity=300;options.ShopButtonsSet=false;check(options.Validate(desktop,true)!=null,"Unmarked purchase buttons prevent a normal session");
            options=Options();options.ShopRightPoint=new Point(2000,931);check(options.ValidateShopButtons(desktop)!=null,"All three marked points, including No/Cancelar, must fit within the target bounds");
            options=Options();options.ShopMiddlePoint=options.ShopLeftPoint;check(options.ValidateShopButtons(desktop)!=null,"Overlapping or unordered marked buttons fail validation");
            if(!string.IsNullOrEmpty(output)){
                Directory.CreateDirectory(output);string path=Path.Combine(output,"simple-purchase-settings.xml");options=Options(true);options.PurchaseIntervalMinutes=40;options.Save(path);Settings loaded=Settings.Load(path);
                check(loaded.UseDirectShopFlow&&loaded.ShopButtonsSet&&loaded.ShopLeftPoint==options.ShopLeftPoint&&loaded.ShopMiddlePoint==options.ShopMiddlePoint&&loaded.ShopRightPoint==options.ShopRightPoint&&loaded.BaitCapacity==300,"Capacity and all marked buttons survive settings serialization");
                check(loaded.PurchaseByTimer&&loaded.BuyQuantity==50&&loaded.PurchaseIntervalMinutes==40&&loaded.BuyBaitAt==2,"The two purchase modes preserve 50 bait, 40 minutes and threshold 2 in settings");
            }
        }
        private sealed class Fake : IGameRuntime,IShopRuntime
        {
            internal bool Active=true,FishingHeld,ShopHeld,ForbidObserve,ForbidBait;
            internal double Now,mouseReleaseAt;
            internal long Sequence;
            internal int ShopReads,BaitReads,Observations,CastMoves,Presses,Releases,Jumps;
            internal Observation Current=new Observation();
            internal BaitReading Bait=new BaitReading();
            internal readonly List<int> Keys=new List<int>();
            internal readonly HashSet<int> KeysDown=new HashSet<int>();
            internal readonly List<Point> Clicks=new List<Point>();
            internal Point Aimed;
            internal string Digits {get{return string.Concat(Keys.Where(k=>k>=0x30&&k<=0x39).Select(k=>((char)k).ToString()));}}
            internal int InputCount {get{return Keys.Count+Clicks.Count+CastMoves+Presses+Jumps;}}
            internal void Advance(double now){Now=now;if(ShopHeld&&now>=mouseReleaseAt)ShopHeld=false;}
            public bool IsActive {get{return Active;}}
            public void MoveToCastPoint(){if(!Active)throw new InvalidOperationException("Inactive cast");CastMoves++;}
            public void SetHeld(bool held){if(held&&!Active)throw new InvalidOperationException("Inactive fishing input");if(held&&!FishingHeld)Presses++;FishingHeld=held;}
            public void Release(){FishingHeld=ShopHeld=false;KeysDown.Clear();Releases++;}
            public Observation Observe(){Observations++;if(ForbidObserve)throw new InvalidOperationException("Diagnostic must not inspect a fishing image");return Current;}
            public BaitReading ReadBait(double now){BaitReads++;if(ForbidBait)throw new InvalidOperationException("Diagnostic must not inspect bait OCR");return Bait;}
            public void SetJumpHeld(bool held){if(held){if(!Active)throw new InvalidOperationException("Inactive jump");Jumps++;}}
            public ShopReading ReadShop(double now){ShopReads++;throw new InvalidOperationException("Marked buttons must never read shop OCR");}
            public void ShopAim(Point point){if(!Active)throw new InvalidOperationException("Inactive aim");Release();Aimed=point;}
            public void ShopClick(Point point,ShopClickKind kind){if(!Active||FishingHeld||ShopHeld||Aimed!=point)throw new InvalidOperationException("Unsafe fake shop click");Clicks.Add(point);ShopHeld=true;mouseReleaseAt=Now+180;}
            public void ShopKey(int key,bool held){if(held){if(!Active||FishingHeld||ShopHeld)throw new InvalidOperationException("Unsafe fake shop key");Keys.Add(key);KeysDown.Add(key);}else KeysDown.Remove(key);}
        }
    }
}
