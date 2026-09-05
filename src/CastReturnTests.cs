using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SomeFishingGPO
{
    public static class CastReturnTests
    {
        private static Settings Options(bool buy=false,bool timer=false)
        {
            return new Settings{AutoCast=true,CastPointSet=true,CastPoint=new Point(800,500),CastMilliseconds=220,
                Area=new Rectangle(100,100,60,300),UseDirectShopFlow=true,AutoBuyBait=buy,MonitorBait=buy,
                BaitArea=new Rectangle(1050,600,50,30),PurchaseByTimer=timer,PurchaseIntervalMinutes=1,
                BuyQuantity=50,BaitCapacity=300,BuyBaitAt=2,ShopButtonsSet=true,
                ShopLeftPoint=new Point(1104,925),ShopMiddlePoint=new Point(1254,926),ShopRightPoint=new Point(1410,926)};
        }
        private static void At(FishingEngine engine,Fake game,double now,int? count=null)
        {game.Advance(now);game.Bait=new BaitReading{Sequence=++game.Sequence,SampledAt=now,Count=count};engine.Tick(now);}
        private static void RunOrder(FishingEngine engine,Fake game)
        {double end=game.Now+60000;for(double now=game.Now+50;now<=end&&engine.Running&&engine.State==Phase.Purchasing;now+=50)At(engine,game,now);}
        private static Observation Fish(){return new Observation{Found=true,MenuVisible=true,GapY=180,FishY=80,GapTop=150,GapBottom=210};}
        public static void Run(Action<bool,string> check,string output)
        {
            var game=new Fake();var engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,1000);
            check(engine.State==Phase.AimingCast&&game.CastMoves==1&&!game.FishingHeld&&game.CastPresses==0,"The first cast begins by moving to water without pressing the mouse");
            At(engine,game,1499);check(engine.State==Phase.AimingCast&&game.CastPending&&game.CastPresses==0,"Initial casting waits until the asynchronous pointer actually arrives");
            At(engine,game,1500);At(engine,game,1699);check(game.CastReady&&!game.FishingHeld&&game.CastPresses==0,"Initial casting waits an extra 200 ms after pointer arrival");
            At(engine,game,1700);check(engine.State==Phase.Casting&&game.FishingHeld&&game.CastPresses==1&&game.PressAt==1700,"The actual first cast starts only after arrival plus the full 200 ms settling interval");
            At(engine,game,1919);check(game.FishingHeld&&engine.State==Phase.Casting,"The cast retains its full configured hold after movement and settling");
            At(engine,game,1920);check(!game.FishingHeld&&engine.State==Phase.Waiting&&game.ReleaseAt-game.PressAt==220,"Cast hold duration is measured from button-down, not from the earlier pointer request");engine.Stop("test");

            game=new Fake();engine=new FishingEngine(Options(true,true),game);engine.Start(0);At(engine,game,60000);At(engine,game,60050);At(engine,game,61650);
            check(engine.State==Phase.Purchasing&&game.CastMoves==0,"A due purchase starts before any water movement in the return-after-buy fixture");RunOrder(engine,game);double bought=game.Now;
            check(engine.State==Phase.Preparing&&game.ShopClicks==5&&game.Digits=="50"&&!game.FishingHeld,"The purchase completes its five shop clicks before preparing a return to the water");
            At(engine,game,bought+999);check(game.CastMoves==0,"The post-purchase preparation pause does not move or launch early");At(engine,game,bought+1000);
            check(engine.State==Phase.AimingCast&&game.CastMoves==1&&game.CastTarget==new Point(800,500)&&game.CastPresses==0,"After buying, the engine requests the same marked water destination through asynchronous aiming");
            At(engine,game,bought+1499);At(engine,game,bought+1500);At(engine,game,bought+1699);
            check(game.CastPresses==0&&!game.FishingHeld,"Returning from the shop waits for both pointer travel and 200 ms of settling");At(engine,game,bought+1700);
            check(engine.State==Phase.Casting&&game.CastPresses==1&&game.PressAt==bought+1700,"The first post-purchase launch happens only after the pointer is ready at the water");At(engine,game,bought+1920);
            check(engine.State==Phase.Waiting&&!game.FishingHeld&&game.ReleaseAt-game.PressAt==220&&game.ShopReads==0&&game.BaitReads==0,"The returned cast holds exactly 220 ms and the timer path still needs no OCR");engine.Stop("test");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"cast-return.txt"),game.Events.ToArray());}

            game=new Fake();engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,1000);game.Active=false;At(engine,game,1100);int inputs=game.InputCount,polls=game.CastPolls;At(engine,game,100000);
            check(!engine.Running&&game.CastCancelled&&!game.CastPending&&game.CastPresses==0&&game.InputCount==inputs&&game.CastPolls==polls,"Focus loss during return movement cancels the aim and prevents any future launch or movement tick");
            game=new Fake();engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,1000);engine.Stop("F10");inputs=game.InputCount;At(engine,game,100000);
            check(!engine.Running&&game.CastCancelled&&game.CastPresses==0&&game.InputCount==inputs,"F10 while moving to water leaves no delayed cast behind");
            game=new Fake();engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,1000);At(engine,game,1500);engine.Stop("F10");At(engine,game,1700);
            check(!engine.Running&&game.CastCancelled&&!game.CastReady&&game.CastPresses==0,"F10 during the extra 200 ms settling interval also cancels the pending cast");
            game=new Fake();engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,1000);At(engine,game,1500);game.Active=false;At(engine,game,1700);
            check(!engine.Running&&game.CastPresses==0&&!game.FishingHeld,"Focus is rechecked at the final casting deadline after the pointer has arrived");

            game=new Fake();engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,1000);game.Current=Fish();At(engine,game,1100);
            check(engine.State==Phase.Waiting&&game.CastCancelled&&game.CastPresses==0&&!game.FishingHeld,"A fishing minigame appearing during water movement cancels the aim and waits without a launch");engine.Stop("test");
            game=new Fake();engine=new FishingEngine(Options(),game);engine.Start(0);At(engine,game,1000);At(engine,game,1500);game.Current=new Observation{MenuVisible=true};At(engine,game,1700);
            check(engine.State==Phase.Waiting&&game.CastCancelled&&game.CastPresses==0&&!game.FishingHeld,"A menu appearing during the final settling interval prevents a cast even without complete fish detection");engine.Stop("test");

            var options=Options();options.AutoCast=false;game=new Fake();engine=new FishingEngine(options,game);engine.Start(0);At(engine,game,1000);At(engine,game,10000);
            check(engine.State==Phase.Waiting&&game.CastMoves==0&&game.CastPolls==0&&game.CastPresses==0,"Manual casting mode never starts the asynchronous water pointer or presses to launch");engine.Stop("test");
            options=Options(true,true);options.TestBuyQuantity=1;game=new Fake{ForbidObserve=true};engine=new FishingEngine(options,game,RunKind.PurchaseTest);engine.Start(0);At(engine,game,1000);RunOrder(engine,game);
            check(!engine.Running&&engine.PurchaseSubmitted&&game.ShopClicks==5&&game.CastMoves==0&&game.CastPolls==0&&game.CastPresses==0,"A successful purchase diagnostic ends without returning to water or auto-casting");
            options=Options();game=new Fake();engine=new FishingEngine(options,game,RunKind.EmptyBaitTest);engine.Start(0);At(engine,game,0);At(engine,game,50);At(engine,game,100);At(engine,game,1000);
            check(!engine.Running&&game.CastMoves==0&&game.CastPolls==0&&game.CastPresses==0,"The no-bait diagnostic cannot start water movement or a fishing click");

            options=Options(true);game=new Fake{AimDelay=1000};engine=new FishingEngine(options,game);engine.Start(0);At(engine,game,0,4);At(engine,game,50,4);At(engine,game,1000,4);
            check(engine.State==Phase.AimingCast&&game.CastPending,"Low-bait interruption fixture is moving to water before any launch");At(engine,game,1050,2);At(engine,game,1100,2);
            check(engine.State==Phase.PausingPurchase&&game.CastCancelled&&game.CastPresses==0&&!game.CastPending,"A newly confirmed low counter interrupts and cancels water aiming before it can cast");At(engine,game,1150,2);At(engine,game,2749,2);
            check(engine.PurchaseAttempts==0,"A low-bait interruption still requires the 1600 ms clear-menu pause");At(engine,game,2750,2);
            check(engine.State==Phase.Purchasing&&engine.PurchaseAttempts==1&&game.KeysDown.Contains(0x45)&&game.CastPresses==0,"An interrupted aim is not treated as an in-flight cast requiring a bite window before buying");engine.Stop("test");
            options=Options(true,true);game=new Fake{AimDelay=1000};engine=new FishingEngine(options,game);engine.Start(0);At(engine,game,59900);At(engine,game,60000);At(engine,game,60050);At(engine,game,61650);
            check(engine.State==Phase.Purchasing&&game.CastCancelled&&game.CastPresses==0&&engine.PurchaseAttempts==1,"Timer expiration during water aiming also cancels the unsent cast and buys after only the clear-menu pause");engine.Stop("test");
        }
        private sealed class Fake : IGameRuntime,IShopRuntime,ICastPointerRuntime
        {
            internal bool Active=true,FishingHeld,ShopHeld,CastPending,CastReady,CastCancelled,ForbidObserve;
            internal double Now,AimDelay=500,CastArrivesAt,mouseUpAt,PressAt,ReleaseAt;
            internal long Sequence;
            internal int CastMoves,CastPolls,CastPresses,TrackPresses,ShopClicks,ShopReads,BaitReads;
            internal Point CastTarget,ShopTarget;
            internal Observation Current=new Observation();
            internal BaitReading Bait=new BaitReading();
            internal readonly List<int> Keys=new List<int>();
            internal readonly HashSet<int> KeysDown=new HashSet<int>();
            internal readonly List<string> Events=new List<string>();
            internal string Digits {get{return string.Concat(Keys.Where(k=>k>=0x30&&k<=0x39).Select(k=>((char)k).ToString()));}}
            internal int InputCount {get{return CastMoves+CastPresses+TrackPresses+ShopClicks+Keys.Count;}}
            private void Log(string text){Events.Add(Now.ToString(CultureInfo.InvariantCulture)+" ms: "+text);}
            internal void Advance(double now){Now=now;if(ShopHeld&&now>=mouseUpAt)ShopHeld=false;}
            public bool IsActive {get{return Active;}}
            public void MoveToCastPoint(){if(!Active)throw new InvalidOperationException("Inactive cast aim");Release();CastMoves++;CastTarget=new Point(800,500);CastPending=true;CastReady=false;CastCancelled=false;CastArrivesAt=Now+AimDelay;Log("Water aim started "+CastTarget);}
            public bool TickCastAim(double now){CastPolls++;if(!Active||CastCancelled)throw new InvalidOperationException("Cancelled cast aim");if(CastReady)return true;if(!CastPending)throw new InvalidOperationException("Cast aim was not requested");if(now<CastArrivesAt)return false;CastPending=false;CastReady=true;Log("Water pointer arrived");return true;}
            public void SetHeld(bool held){if(held){if(!Active||CastPending||ShopHeld)throw new InvalidOperationException("Fishing click before pointer arrival");if(!FishingHeld){if(CastReady){CastPresses++;CastReady=false;PressAt=Now;Log("Cast down");}else TrackPresses++;}}else if(FishingHeld){ReleaseAt=Now;Log("Fishing up");}FishingHeld=held;}
            public void Release(){if(CastPending||CastReady)CastCancelled=true;CastPending=CastReady=false;FishingHeld=ShopHeld=false;KeysDown.Clear();}
            public Observation Observe(){if(ForbidObserve)throw new InvalidOperationException("Diagnostic must not inspect fish");return Current;}
            public BaitReading ReadBait(double now){BaitReads++;return Bait;}
            public void SetJumpHeld(bool held){if(held)throw new InvalidOperationException("Unexpected jump in cast-return test");}
            public ShopReading ReadShop(double now){ShopReads++;throw new InvalidOperationException("No shop OCR in marked-button mode");}
            public void ShopAim(Point point){if(!Active)throw new InvalidOperationException("Inactive shop aim");Release();ShopTarget=point;}
            public void ShopClick(Point point,ShopClickKind kind){if(!Active||CastPending||FishingHeld||ShopHeld||point!=ShopTarget)throw new InvalidOperationException("Unsafe shop click");ShopClicks++;ShopHeld=true;mouseUpAt=Now+180;}
            public void ShopKey(int key,bool held){if(held){if(!Active||CastPending||FishingHeld||ShopHeld)throw new InvalidOperationException("Unsafe shop key");Keys.Add(key);KeysDown.Add(key);}else KeysDown.Remove(key);}
        }
    }
}
