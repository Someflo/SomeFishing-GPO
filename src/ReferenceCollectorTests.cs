using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SomeFishingGPO
{
    internal static class ReferenceCollectorTests
    {
        private static readonly List<string> results=new List<string>();
        private static void Check(bool condition,string text)
        {if(!condition)throw new Exception("FAIL: "+text);results.Add("OK: "+text);}
        internal static int Run(string output)
        {
            Directory.CreateDirectory(output);results.Clear();
            try
            {
                Settings source=Options();source.AutoBuyBait=true;source.MonitorBait=true;source.PurchaseByTimer=true;
                string before=LocalizationTests.Snapshot(source);
                Settings engine=ReferenceCollector.FishingOptions(source,30),native=ReferenceCollector.RuntimeOptions(source,30);
                Check(!engine.AutoBuyBait&&!engine.MonitorBait&&!engine.PurchaseByTimer&&!engine.IdleJumpEnabled&&engine.KeepOneBait,"Fishing cannot buy, jump or depend on OCR during collection");
                Check(engine.ManualCommonBait==30&&engine.ManualRareBait==0&&engine.ManualLegendaryBait==0&&engine.ActiveBaitKind==BaitKind.Common,"Collection uses only common bait and reserves one");
                Check(native.AutoBuyBait&&!object.ReferenceEquals(engine,native)&&before==LocalizationTests.Snapshot(source),"Native E permission is isolated from the engine and original settings are untouched");
                bool rejected=false;try{ReferenceCollector.FishingOptions(source,31);}catch(ArgumentOutOfRangeException){rejected=true;}
                Check(rejected,"Initial quantity is bounded at thirty");

                var fake=new Fake();var captures=new List<string>();var pairs=new List<int>();
                var collector=Create(fake,30,captures,pairs);collector.Start(0);RunUntil(fake,collector,delegate{return !collector.Running;},600000);
                Check(collector.State==ReferenceCollector.Stage.Complete&&collector.Rounds==29&&collector.Samples==30&&collector.Remaining==1,"Thirty declared bait produces exactly twenty-nine rounds and thirty completed capture series");
                Check(fake.Casts==29&&fake.Yes==30&&fake.Cancel==30&&fake.Buys==0&&fake.NumericKeys==0,"Thirty-series run opens and cancels every menu; never types or buys and never casts the reserved bait");
                Check(captures.Count==120&&captures[0]=="0:30:cebos-a"&&captures[2]=="0:30:max-a"&&captures.Last()=="29:1:max-b","Two original snapshots per stage include the initial thirty-bait pair before any fishing");
                Check(pairs.SequenceEqual(Enumerable.Range(0,30)),"Completed round IDs are collected exactly once");
                Check(fake.CastsAtFirstCapture==0&&fake.OcrReads==0,"The initial sample precedes all casts and no OCR call is made");
                int events=fake.Events;for(int i=0;i<20;i++){fake.Now+=50;collector.Tick(fake.Now);}
                Check(fake.Events==events,"No input survives completion");

                fake=new Fake{ShowDone=true};captures=new List<string>();pairs=new List<int>();collector=Create(fake,1,captures,pairs);collector.Start(0);
                RunUntil(fake,collector,delegate{return !collector.Running;},60000);
                Check(collector.Samples==1&&fake.Casts==0&&fake.Closes==1&&fake.Buys==0,"One-bait follow-up captures without fishing and handles an optional three-dot closing dialog");

                fake=new Fake{Menu=ShopMenuKind.Quantity};collector=Create(fake,30,new List<string>(),new List<int>());collector.Start(0);
                RunUntil(fake,collector,delegate{return !collector.Running;},30000);
                Check(collector.State==ReferenceCollector.Stage.Stopped&&fake.Yes==0&&fake.EPresses==0,"An initially open quantity dialog cannot receive a misplaced Yes/Buy click");

                fake=new Fake{SuppressE=true};collector=Create(fake,2,new List<string>(),new List<int>());collector.Start(0);
                RunUntil(fake,collector,delegate{return !collector.Running;},50000);
                Check(fake.Yes==0&&fake.Casts==0&&!fake.HeldE&&collector.State==ReferenceCollector.Stage.Stopped,"If E does not open the dialog, no left click or cast follows and E is released");

                fake=new Fake{Stale=true};collector=Create(fake,2,new List<string>(),new List<int>());collector.Start(0);
                RunUntil(fake,collector,delegate{return !collector.Running;},30000);
                Check(fake.EPresses==0&&fake.Yes==0,"Repeated cached menu frames cannot authorize opening or clicking");

                fake=new Fake();collector=Create(fake,2,new List<string>(),new List<int>());collector.Start(0);
                RunUntil(fake,collector,delegate{return collector.State==ReferenceCollector.Stage.ClickYes;},30000);
                fake.Menu=ShopMenuKind.Quantity;RunUntil(fake,collector,delegate{return !collector.Running;},30000);
                Check(fake.Yes==0&&fake.Buys==0,"A menu change during pointer movement invalidates Yes before it can hit Buy");

                fake=new Fake();collector=new ReferenceCollector(Options(),2,fake,delegate{throw new IOException("synthetic disk full");},delegate{});collector.Start(0);
                RunUntil(fake,collector,delegate{return !collector.Running;},30000);
                Check(collector.Status=="synthetic disk full"&&fake.EPresses==0,"A capture write failure stops before entering the shop");

                fake=new Fake{SuppressFish=true};collector=Create(fake,2,new List<string>(),new List<int>());collector.Start(0);
                RunUntil(fake,collector,delegate{return !collector.Running;},250000);
                Check(collector.Rounds==0&&collector.Samples==1&&fake.Yes==1,"Failed casts do not decrement bait or produce another sample; the session is bounded");

                var stages=new[]{ReferenceCollector.Stage.SelectCommon,ReferenceCollector.Stage.Preflight,ReferenceCollector.Stage.BaitA,
                    ReferenceCollector.Stage.BaitB,ReferenceCollector.Stage.OpenE,ReferenceCollector.Stage.ReleaseE,ReferenceCollector.Stage.Confirm,
                    ReferenceCollector.Stage.AimYes,ReferenceCollector.Stage.ClickYes,ReferenceCollector.Stage.Quantity,ReferenceCollector.Stage.MaxA,
                    ReferenceCollector.Stage.MaxB,ReferenceCollector.Stage.AimCancel,ReferenceCollector.Stage.ClickCancel,ReferenceCollector.Stage.Closed,
                    ReferenceCollector.Stage.AimClose,ReferenceCollector.Stage.ClickClose,ReferenceCollector.Stage.Fishing};
                foreach(var stage in stages)
                {
                    fake=new Fake{ShowDone=true};collector=Create(fake,2,new List<string>(),new List<int>());collector.Start(0);
                    RunUntil(fake,collector,delegate{return collector.State==stage;},60000);
                    collector.Stop("test stop");events=fake.Events;fake.Now+=100000;collector.Tick(fake.Now);
                    Check(!collector.Running&&!fake.HeldE&&!fake.HeldMouse&&events==fake.Events,"Stop releases inputs and cancels delayed work at "+stage);
                    fake=new Fake{ShowDone=true};collector=Create(fake,2,new List<string>(),new List<int>());collector.Start(0);
                    RunUntil(fake,collector,delegate{return collector.State==stage;},60000);
                    fake.Active=false;fake.Now+=50;collector.Tick(fake.Now);
                    Check(!collector.Running&&!fake.HeldE&&!fake.HeldMouse&&fake.Buys==0,"Focus loss stops without a purchase at "+stage);
                }

                TestStorage(output);
                using(var form=new ReferenceCollectorForm(true))form.Render(Path.Combine(output,"recopilador.png"));
                results.Add("PASS: "+results.Count+" collector checks. No real input or screen capture.");
                File.WriteAllLines(Path.Combine(output,"referencias-resultados.txt"),results);return 0;
            }
            catch(Exception error){results.Add(error.ToString());File.WriteAllLines(Path.Combine(output,"referencias-resultados.txt"),results);return 1;}
        }
        private static Settings Options()
        {return new Settings{Area=new Rectangle(0,0,100,340),CastPointSet=true,CastPoint=new Point(25,25),
            ShopButtonsSet=true,UseDirectShopFlow=true,ShopLeftPoint=new Point(80,260),ShopMiddlePoint=new Point(180,260),ShopRightPoint=new Point(280,260),
            ShopOpenMilliseconds=1000,ShopSettleMilliseconds=679,UseBaitPoints=true,BaitPointsSet=7,BaitCommonPoint=new Point(40,380),BaitRarePoint=new Point(40,360),BaitLegendaryPoint=new Point(40,340)};}
        private static ReferenceCollector Create(Fake fake,int initial,List<string> captures,List<int> pairs)
        {return new ReferenceCollector(Options(),initial,fake,delegate(int round,int estimate,string stage){if(fake.CastsAtFirstCapture<0)fake.CastsAtFirstCapture=fake.Casts;captures.Add(round+":"+estimate+":"+stage);},delegate(int round,int estimate){pairs.Add(round);});}
        private static void RunUntil(Fake fake,ReferenceCollector collector,Func<bool> ready,double limit)
        {
            double end=fake.Now+limit;
            while(!ready()&&collector.Running&&fake.Now<end){fake.Now+=50;collector.Tick(fake.Now);}
            if(!ready())throw new Exception("Expected stage not reached: "+collector.State+" / "+collector.Status);
        }
        private static void TestStorage(string output)
        {
            Settings settings=Options();Rectangle client=new Rectangle(-300,40,400,440);
            settings.BaitArea=new Rectangle(-280,60,10,15);settings.BaitMenuArea=new Rectangle(-290,50,100,70);
            bool active=true;Rectangle received=Rectangle.Empty;
            Func<Rectangle,Bitmap> pixels=delegate(Rectangle area){received=area;var b=new Bitmap(area.Width,area.Height);using(var g=Graphics.FromImage(b))g.Clear(Color.Blue);return b;};
            var store=new ReferenceCaptureStore(Path.Combine(output,"capturas-sinteticas"),settings,client,delegate{return active;},pixels,30);
            foreach(string stage in new[]{"cebos-a","cebos-b","max-a","max-b"})store.Capture(0,30,stage);
            store.CompletePair(0,30);
            using(var image=new Bitmap(Path.Combine(store.DirectoryPath,"muestra-000-cebos-a.png")))Check(image.Size==client.Size&&received==client,"Capture keeps original window dimensions including negative desktop origins");
            using(var image=new Bitmap(Path.Combine(store.DirectoryPath,"muestra-000-cebos-a-contador.png")))Check(image.Size==settings.BaitArea.Size,"Counter crop retains native pixels");
            string csv=File.ReadAllText(Path.Combine(store.DirectoryPath,"revisar.csv"));
            Check(csv.Contains("000,0,30,,,si")&&!csv.Contains("270"),"Manifest leaves actual bait and MAX unlabelled; estimate is explicitly separate");
            bool failed=false;try{store.Capture(0,30,"max-a");}catch(IOException){failed=true;}
            Check(failed,"An existing reference image cannot be overwritten");
            active=false;failed=false;try{store.Capture(1,29,"cebos-a");}catch(InvalidOperationException){failed=true;}
            Check(failed&&!File.Exists(Path.Combine(store.DirectoryPath,"muestra-001-cebos-a.png")),"Losing focus prevents saving another application's pixels");
            store.Finish("complete");store.Finish("later close");
            Check(File.ReadAllText(Path.Combine(store.DirectoryPath,"resultado.txt")).Contains("complete"),"Closing the form later preserves the original session result");
        }
        private sealed class Fake : IGameRuntime,IShopRuntime,IShopPointerRuntime,IShopVisualRuntime,IBaitSelectionRuntime,ICastPointerRuntime
        {
            internal bool Active=true,HeldE,HeldMouse,ShowDone,SuppressE,SuppressFish,Stale;
            internal int Casts,Yes,Cancel,Closes,Buys,NumericKeys,EPresses,Events,OcrReads,CastsAtFirstCapture=-1;
            internal double Now;
            internal ShopMenuKind Menu=ShopMenuKind.Absent;
            private Point aim;
            private double aimedAt,fishStart=-10000,fishEnd=-10000;
            private bool castArmed;
            private long sequence;
            public bool IsActive {get{return Active;}}
            public void Release(){HeldE=HeldMouse=false;}
            public void MoveToCastPoint(){Casts++;Events++;castArmed=true;}
            public bool TickCastAim(double now){return true;}
            public void SetHeld(bool held)
            {HeldMouse=held;if(held&&castArmed){castArmed=false;fishStart=Now+350;fishEnd=Now+1500;}Events++;}
            public Observation Observe(){return !SuppressFish&&Now>=fishStart&&Now<fishEnd?new Observation{Found=true,MenuVisible=true,FishY=100,GapY=125,GapTop=110,GapBottom=140,BarBounds=new Rectangle(20,10,25,300)}:new Observation();}
            public void SetJumpHeld(bool held){throw new Exception("Collector jumped");}
            public BaitReading ReadBait(double now){OcrReads++;throw new Exception("Collector read OCR");}
            public ShopReading ReadShop(double now){OcrReads++;throw new Exception("Collector read shop OCR");}
            public void ShopKey(int key,bool held)
            {if(key!=0x45){NumericKeys++;throw new Exception("Non-E key");}HeldE=held;Events++;if(held)EPresses++;else if(!SuppressE)Menu=ShopMenuKind.Confirm;}
            public void ShopAim(Point point){aim=point;aimedAt=Now;Events++;}
            public bool TickShopAim(double now){return Now-aimedAt>=300;}
            public void ShopClick(Point point,ShopClickKind kind)
            {
                if(!Active||point!=aim||Now-aimedAt<1000||HeldE||HeldMouse)throw new Exception("Unsettled or inactive click");
                if(kind!=ShopClickKind.Button)throw new Exception("Quantity field click");
                Events++;
                if(point.X==80){if(Menu!=ShopMenuKind.Confirm){Buys++;throw new Exception("Buy must not be clicked");}Yes++;Menu=ShopMenuKind.Quantity;}
                else if(point.X==280){if(Menu!=ShopMenuKind.Quantity)throw new Exception("Wrong cancel state");Cancel++;Menu=ShowDone?ShopMenuKind.Done:ShopMenuKind.Absent;}
                else {if(Menu!=ShopMenuKind.Done)throw new Exception("Wrong close state");Closes++;Menu=ShopMenuKind.Absent;}
            }
            public ShopVisualReading ReadShopVisual(double now){return new ShopVisualReading{Menu=Menu,Sequence=Stale?1:++sequence,SampledAt=now};}
            public void BeginBaitSelection(BaitKind kind,double now){if(kind!=BaitKind.Common)throw new Exception("Wrong bait");}
            public BaitSelectionResult TickBaitSelection(double now){return new BaitSelectionResult{Completed=true,Succeeded=true};}
        }
    }
}

