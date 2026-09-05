using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SomeFishingGPO
{
    internal static class SelfTests
    {
        private static readonly List<string> results = new List<string>();
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            results.Add("OK: " + description);
        }
        internal static Bitmap CreateSample()
        {
            var bitmap = new Bitmap(72, 340, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(25, 25, 25));
                using (var blue = new SolidBrush(Color.FromArgb(85, 170, 255)))
                {
                    graphics.FillRectangle(blue, 23, 12, 26, 102);
                    graphics.FillRectangle(blue, 23, 177, 26, 152);
                }
                graphics.FillRectangle(Brushes.White, 9, 241, 54, 3);
            }
            return bitmap;
        }
        internal static int Run(string[] args)
        {
            string output = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pruebas");
            Directory.CreateDirectory(output);
            try
            {
                Check(Marshal.SizeOf(typeof(Native.Input)) == (IntPtr.Size == 8 ? 40 : 28), "Windows INPUT structure matches the platform ABI");
                var settings = new Settings();
                using (Bitmap sample = CreateSample())
                {
                    Observation result = Detector.Analyze(sample, settings);
                    Check(result.Found, "Detect cyan bar, interior gap and thin white marker");
                    Check(Math.Abs(result.GapY - 145) < 2, "Target center agrees with synthetic image");
                    Check(Math.Abs(result.FishY - 242) < 2, "Marker center agrees with synthetic image");
                }
                using (var empty = new Bitmap(72, 340, PixelFormat.Format32bppArgb))
                    Check(!Detector.Analyze(empty, settings).Found, "Empty images do not start tracking");
                using (Bitmap sample = CreateSample())
                {
                    using (Graphics graphics = Graphics.FromImage(sample)) graphics.FillRectangle(Brushes.Black, 0, 238, 72, 12);
                    Check(!Detector.Analyze(sample, settings).Found, "Missing white marker is rejected");
                }
                if (args.Length > 2)
                {
                    using (var source = new Bitmap(args[2]))
                    using (Bitmap crop = source.Clone(new Rectangle(111, 101, 20, 229), PixelFormat.Format32bppArgb))
                    {
                        Observation found = Detector.Analyze(crop, settings);
                        Check(found.Found, "Detect the bar in the screenshot supplied by the user");
                        Check(found.FishY + 101 >= 222 && found.FishY + 101 <= 228, "Screenshot: marker near y=225");
                        Check(found.GapY + 101 >= 155 && found.GapY + 101 <= 176, "Screenshot: target in the dark gap above the marker");
                        results.Add(string.Format("IMAGE: marker y={0:F1}; target y={1:F1}; target bounds {2}..{3}",
                            found.FishY + 101, found.GapY + 101, found.GapTop + 101, found.GapBottom + 101));
                    }
                }
                if (args.Length > 3)
                {
                    using (var source = new Bitmap(args[3]))
                    using (Bitmap crop = source.Clone(new Rectangle(358, 202, 27, 346), PixelFormat.Format32bppArgb))
                    {
                        Observation found = Detector.Analyze(crop, settings);
                        Check(found.Found, "New screenshot 1: detect the gray gap at the bottom endpoint");
                        Check(found.FishY + 202 >= 361 && found.FishY + 202 <= 368, "New screenshot 1: white fish line near y=365");
                        Check(found.GapY + 202 >= 486 && found.GapY + 202 <= 512, "New screenshot 1: controlled gap near the bottom");
                        Check(new Controller().Update(found, 0, settings), "New screenshot 1: hold to raise the gap toward the fish");
                        results.Add(string.Format("IMAGE 2: fish y={0:F1}; controlled gap y={1:F1}", found.FishY + 202, found.GapY + 202));
                    }
                }
                if (args.Length > 4)
                {
                    using (var source = new Bitmap(args[4]))
                    using (Bitmap crop = source.Clone(new Rectangle(373, 151, 28, 308), PixelFormat.Format32bppArgb))
                    {
                        Observation found = Detector.Analyze(crop, settings);
                        Check(found.Found, "New screenshot 2: detect the interior gap and fish");
                        Check(found.FishY + 151 >= 337 && found.FishY + 151 <= 346, "New screenshot 2: white fish line near y=342");
                        Check(found.GapY + 151 >= 278 && found.GapY + 151 <= 307, "New screenshot 2: gap above the fish");
                        Check(!new Controller().Update(found, 0, settings), "New screenshot 2: release to lower the gap toward the fish");
                        results.Add(string.Format("IMAGE 3: fish y={0:F1}; controlled gap y={1:F1}", found.FishY + 151, found.GapY + 151));
                    }
                }

                var controller = new Controller();
                Check(controller.Update(new Observation { Found = true, FishY = 160, GapY = 230 }, 0, settings), "Hold raises the gray gap toward a fish above it");
                controller.Reset();
                Check(!controller.Update(new Observation { Found = true, FishY = 160, GapY = 120 }, 0, settings), "Release lowers the gray gap toward a fish below it");
                Check(!controller.Update(new Observation(), 50, settings), "Losing detection releases the controller");
                settings.HoldMovesUp = false; controller.Reset();
                Check(controller.Update(new Observation { Found = true, FishY = 160, GapY = 100 }, 0, settings), "Reverse direction is supported");
                settings.HoldMovesUp = true;
                TestMotion(settings, 650, 140, false);
                TestMotion(settings, 1600, 250, true);
                TestStability();

                var game = new FakeGame();
                var engine = new FishingEngine(settings, game);
                engine.Start(0); engine.Tick(1000);
                Check(engine.State == Phase.Casting && game.Held && game.Moves == 1, "Casting holds only after the preparation delay");
                game.Active = false; engine.Tick(1050);
                Check(!engine.Running && !game.Held, "Changing focus during a cast stops and releases");
                int actions = game.Presses; engine.Tick(10000);
                Check(game.Presses == actions, "A stopped engine cannot send a delayed press");

                game = new FakeGame { Current = new Observation { Found = true, MenuVisible = true, FishY = 120, GapY = 200 } };
                engine = new FishingEngine(settings, game);
                engine.Start(0); engine.Tick(100); engine.Tick(150);
                Check(engine.State == Phase.Tracking && game.Held && game.Moves == 0, "An already visible minigame skips casting");
                game.Current = new Observation(); engine.Tick(200);
                Check(!game.Held && engine.State == Phase.Tracking, "A single missing frame releases without counting a round");
                engine.Tick(1900);
                Check(engine.State == Phase.Resting && engine.Cycles == 1, "Sustained disappearance completes one round");
                engine.Tick(3800);
                Check(engine.State == Phase.Casting && game.Moves == 1, "A closed menu triggers the next cast after the rest");
                engine.Stop("test"); Check(!game.Held, "Explicit stop releases the mouse");

                game = new FakeGame { Current = new Observation { Found = true, MenuVisible = true, FishY = 120, GapY = 200 } };
                engine = new FishingEngine(settings, game); engine.Start(0); engine.Tick(100); engine.Tick(150);
                game.Current = new Observation { MenuVisible = true }; engine.Tick(200); engine.Tick(1900);
                Check(engine.State == Phase.Tracking && engine.Cycles == 0 && !game.Held, "Missing fish with a visible menu does not count as a round");
                engine.Tick(3300);
                Check(!engine.Running && game.Moves == 0, "Persistent tracking loss stops without casting into an open menu");

                game = new FakeGame { Current = new Observation { MenuVisible = true } };
                engine = new FishingEngine(settings, game); engine.Start(0); engine.Tick(100); engine.Tick(3200);
                Check(!engine.Running && game.Moves == 0, "A visible but unrecognized minigame is never treated as a casting screen");

                game = new FakeGame(); engine = new FishingEngine(settings, game); engine.Start(0);
                for (double now = 0; now < 120000 && engine.Running; now += 50) engine.Tick(now);
                Check(!engine.Running && game.Moves == 3 && !game.Held, "Three failed attempts stop further casting");

                settings.AutoCast = false; game = new FakeGame(); engine = new FishingEngine(settings, game);
                engine.Start(0); engine.Tick(1000); engine.Tick(200000);
                Check(engine.Running && game.Moves == 0 && game.Presses == 0, "Manual launching never moves or clicks while waiting");
                engine.Stop("test");

                settings.AutoCast = true; settings.Area = new Rectangle(20, 20, 25, 300); settings.CastPoint = new Point(450, 450); settings.CastPointSet = true;
                Check(settings.Validate(new Rectangle(0, 0, 1920, 1080), true) == null, "Valid screen coordinates accepted");
                settings.Area = new Rectangle(1800, 20, 300, 300);
                Check(settings.Validate(new Rectangle(0, 0, 1920, 1080), true) != null, "Off-screen capture is rejected");
                settings.Area = new Rectangle(20, 20, 25, 300);
                settings.Save(Path.Combine(output, "test-settings.xml"));
                Settings loaded = Settings.Load(Path.Combine(output, "test-settings.xml"));
                Check(loaded.Area == settings.Area && loaded.CastPoint == settings.CastPoint, "Settings round trip preserves coordinates");
                AuditRegressions(output, args);
                TestWideArea(output, args);
                TestSelection(output);
                TestBaitAndIdle(args);
                TestPurchases(args);

                using (var form = new MainForm(true)) form.RenderExample(Path.Combine(output, "interfaz.png"));
                results.Add("UI: rendered off-screen non-activating form; no hotkeys registered and no clicks sent.");
                results.Add("PASS: " + results.FindAll(delegate(string line) { return line.StartsWith("OK:"); }).Count + " checks.");
                File.WriteAllLines(Path.Combine(output, "resultados.txt"), results.ToArray());
                return 0;
            }
            catch (Exception error)
            { results.Add(error.ToString()); File.WriteAllLines(Path.Combine(output, "resultados.txt"), results.ToArray()); return 1; }
        }
        private static void TestSelection(string output)
        {
            var desktop = new Rectangle(-1080, 0, 1080, 720);
            var selected = new Rectangle(-690, 185, 320, 340);
            using (var background = new Bitmap(1080,720,PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(background))
                using (Bitmap bar = CreateWideSample(60))
                {
                    graphics.Clear(Color.FromArgb(22,78,117));
                    graphics.DrawImageUnscaled(bar,390,185);
                }
                using (var picker = new SelectionOverlay(false,selected,background,desktop))
                {
                    Check(picker.DialogResult == System.Windows.Forms.DialogResult.None && picker.Selection.IsEmpty,
                        "Opening the selector does not commit the previous zone automatically");
                    picker.RenderPreview(Path.Combine(output,"selector.png"));
                    Check(picker.TryConfirm() && picker.Selection == selected,
                        "Confirming the visible rectangle preserves negative monitor coordinates");
                }
                using (var picker = new SelectionOverlay(false,selected,background,desktop))
                {
                    picker.CancelSelection();
                    Check(picker.DialogResult == System.Windows.Forms.DialogResult.Cancel && picker.Selection.IsEmpty,
                        "Canceling selection returns no replacement for the saved zone");
                }
                using (var picker = new SelectionOverlay(false,new Rectangle(-610,185,4,20),background,desktop))
                    Check(!picker.TryConfirm() && picker.DialogResult == System.Windows.Forms.DialogResult.None,
                        "An undersized rectangle stays open for correction instead of being saved");
            }
        }
        internal static Bitmap CreateWideSample(int offset)
        {
            var image=new Bitmap(320,340,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(image))
            using(Bitmap sample=CreateSample())
            {
                g.Clear(Color.FromArgb(44,113,180));g.DrawImageUnscaled(sample,offset,0);
                using(var dark=new SolidBrush(Color.FromArgb(25,25,25)))g.FillRectangle(dark,offset+90,25,24,280);
                using(var green=new SolidBrush(Color.FromArgb(156,255,65)))g.FillRectangle(green,offset+97,160,10,140);
                g.FillRectangle(Brushes.White,offset+125,80,20,3);
            }
            return image;
        }
        private static void TestWideArea(string output,string[] args)
        {
            var settings=new Settings();var game=new FakeGame();var engine=new FishingEngine(settings,game);
            using(Bitmap first=CreateWideSample(20))game.Current=Detector.Analyze(first,settings);
            engine.Start(0);
            for(int frame=1;frame<=30;frame++)
            {
                int offset=70+(int)(55*Math.Sin(frame*.4));
                using(Bitmap scene=CreateWideSample(offset))game.Current=Detector.Analyze(scene,settings);
                Check(game.Current.Found && Math.Abs(game.Current.FishY-242)<2 && Math.Abs(game.Current.GapY-145)<2
                    && game.Current.BarBounds.Contains(offset+36,200),"Follow lateral sway in a wide area, frame "+frame);
                engine.Tick(frame*50);
            }
            Check(engine.State==Phase.Tracking && engine.Cycles==0 && game.Moves==0,
                "Lateral sway with a green progress bar does not trigger a recast or end the round");
            engine.Stop("test");
            using(Bitmap image=CreateWideSample(60))image.Save(Path.Combine(output,"zona-amplia.png"));
            using(Bitmap green=CreateSample())
            {
                using(Graphics g=Graphics.FromImage(green))
                using(var brush=new SolidBrush(Color.FromArgb(156,255,65)))
                {g.FillRectangle(brush,23,12,26,102);g.FillRectangle(brush,23,177,26,152);}
                Check(!Detector.Analyze(green,new Settings{Tolerance=90,BlueArgb=Color.FromArgb(156,255,65).ToArgb()}).MenuVisible,
                    "Green is excluded even with maximum tolerance and an incorrectly calibrated blue color");
            }
            using(var ambiguous=new Bitmap(220,340,PixelFormat.Format32bppArgb))
            {
                using(Graphics g=Graphics.FromImage(ambiguous))using(Bitmap sample=CreateSample())
                {g.Clear(Color.FromArgb(44,113,180));g.DrawImageUnscaled(sample,5,0);g.DrawImageUnscaled(sample,135,0);}
                Observation seen=Detector.Analyze(ambiguous,settings);
                Check(!seen.Found && seen.MenuVisible,"Two plausible blue bars are treated as ambiguous, with no tracking click");
            }
            Rectangle[] panels={new Rectangle(80,101,120,229),new Rectangle(342,202,116,346),new Rectangle(360,151,123,308)};
            double[] fishY={123.5,162.5,191.5}, gapY={64,298.5,140};
            for(int i=0;i<panels.Length && i+2<args.Length;i++)
            using(var source=new Bitmap(args[i+2]))
            using(Bitmap panel=source.Clone(panels[i],PixelFormat.Format32bppArgb))
            {
                using(var padded=new Bitmap(320,panel.Height+100,PixelFormat.Format32bppArgb))
                {
                    using(Graphics g=Graphics.FromImage(padded)){g.Clear(Color.FromArgb(85,170,255));g.DrawImageUnscaled(panel,70,50);}
                    Observation seen=Detector.Analyze(padded,settings);
                    Check(seen.Found && Math.Abs(seen.FishY-fishY[i]-50)<3 && Math.Abs(seen.GapY-gapY[i]-50)<4,
                        "Real screenshot "+i+" tolerates 50 pixels of blue-sky padding above and below");
                }
                foreach(int offset in new[]{0,23,90,170})
                using(var scene=new Bitmap(320,panel.Height,PixelFormat.Format32bppArgb))
                {
                    using(Graphics g=Graphics.FromImage(scene)){g.Clear(Color.FromArgb(85,170,255));g.DrawImageUnscaled(panel,offset,0);}
                    Observation seen=Detector.Analyze(scene,settings);
                    Check(seen.Found && Math.Abs(seen.FishY-fishY[i])<3 && Math.Abs(seen.GapY-gapY[i])<4,
                        "Real screenshot "+i+" including green progress tracks correctly at horizontal offset "+offset);
                }
            }
            if(args.Length>3)
            {
                int count=0;
                using(var source=new Bitmap(args[3]))
                {
                    using(Bitmap green=source.Clone(new Rectangle(410,202,44,346),PixelFormat.Format32bppArgb))
                        Check(!Detector.Analyze(green,settings).MenuVisible,"Real green progress bar alone does not count as a fishing track");
                    for(int y=10;y+300<=source.Height;y+=90)
                        for(int x=0;x+180<=source.Width;x+=50)
                        {
                            var region=new Rectangle(x,y,180,300);
                            if(region.IntersectsWith(new Rectangle(350,185,100,370)))continue;
                            using(Bitmap crop=source.Clone(region,PixelFormat.Format32bppArgb))
                            {
                                Observation seen=Detector.Analyze(crop,settings);
                                Check(!seen.MenuVisible,"Wide landscape region rejected: "+region);count++;
                            }
                        }
                }
                results.Add("WIDE NEGATIVE: "+count+" wide landscape crops rejected.");
            }
        }
        private static void AuditRegressions(string output, string[] args)
        {
            var settings = new Settings { Area = new Rectangle(int.MaxValue - 10, 20, 30, 300),
                CastPoint = new Point(400,400), CastPointSet = true };
            Check(settings.Validate(new Rectangle(0,0,1920,1080),true) != null, "Overflowing coordinates are rejected");
            Check(Settings.ContainsSafely(new Rectangle(-1920,0,3840,1080),new Rectangle(-1800,20,30,300)), "Valid negative monitor coordinates are supported");
            string nil = Path.Combine(output,"nil-settings.xml");
            File.WriteAllText(nil,"<Settings xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />");
            bool rejected = false;
            try { Settings.Load(nil); } catch { rejected = true; }
            Check(rejected,"Null XML configuration is rejected without returning a null Settings object");
            File.WriteAllText(nil,"<!DOCTYPE Settings [<!ENTITY probe 'text'>]><Settings>&probe;</Settings>");
            rejected = false; try { Settings.Load(nil); } catch { rejected = true; }
            Check(rejected,"DTD and entity declarations in settings are rejected");

            using (var blue = new Bitmap(30,300,PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(blue)) graphics.Clear(Color.FromArgb(85,170,255));
                Check(!Detector.Analyze(blue,new Settings()).MenuVisible,"A solid blue background is not treated as the fishing menu");
            }
            if (args.Length > 3)
            {
                int regions=0, falseMenus=0, falseTracking=0;
                using (var source = new Bitmap(args[3]))
                    for (int y=20;y+300<source.Height;y+=65)
                    for (int x=10;x+28<source.Width;x+=29)
                    {
                        var box=new Rectangle(x,y,28,300);
                        if (box.IntersectsWith(new Rectangle(346,180,100,375))) continue;
                        using (Bitmap crop=source.Clone(box,PixelFormat.Format32bppArgb))
                        {
                            Observation seen=Detector.Analyze(crop,new Settings()); regions++;
                            if(seen.MenuVisible)falseMenus++; if(seen.Found)falseTracking++;
                        }
                    }
                Check(regions==90 && falseMenus==0 && falseTracking==0,"Reject all 90 tested landscape crops away from the minigame");
                results.Add("NEGATIVE IMAGE: "+regions+" landscape crops; false menus="+falseMenus+"; false tracking="+falseTracking);
            }
            var game = new FakeGame { Current = new Observation { Found=true,MenuVisible=true,FishY=100,GapY=200 } };
            var engine = new FishingEngine(new Settings(),game); engine.Start(0);engine.Tick(50);engine.Tick(100);
            for(int i=0;i<10;i++) {game.Current=new Observation{Found=i%2==1,MenuVisible=true,FishY=100,GapY=200};engine.Tick(121000+i*50);}
            Check(!engine.Running && !game.Held,"Tracking timeout still stops when valid and invalid frames alternate");

            double now=0; bool permitted=true, held=false; int downs=0, ups=0, failUps=0;
            Action<bool> sender=delegate(bool down)
            {
                if(!down && failUps>0) {failUps--;throw new IOException("simulated OS input rejection");}
                held=down; if(down)downs++;else ups++;
            };
            var lease=new MouseLease(sender,delegate{return permitted;},delegate{return now;});
            lease.SetHeld(true); now=600;lease.Watchdog();
            Check(!held && !lease.PendingRelease,"Independent watchdog releases a click when UI heartbeats stop");
            int previousDowns=downs;rejected=false;
            try {lease.SetHeld(true);}catch{rejected=true;}
            Check(rejected && downs==previousDowns,"Expired leases cannot send a new mouse-down");
            now=0;lease=new MouseLease(sender,delegate{return permitted;},delegate{return now;});
            lease.SetHeld(true);
            for(int i=1;i<=40;i++){now=i*50;CheckBeat(lease);lease.Watchdog();}
            Check(held,"Regular UI heartbeats renew the lease while casting or tracking");
            permitted=false;lease.Watchdog();
            Check(!held,"A focus or F10 safety signal releases a click without a UI tick");
            permitted=true;now=0;lease=new MouseLease(sender,delegate{return permitted;},delegate{return now;});
            lease.SetHeld(true);failUps=1;lease.Stop();
            Check(held && lease.PendingRelease,"Rejected button-up remains pending instead of discarding its state");
            lease.Watchdog();Check(!held && !lease.PendingRelease,"A pending release is retried successfully without another mouse-down");
            now=0;lease=new MouseLease(sender,delegate{return permitted;},delegate{return now;});
            lease.SetHeld(true);lease.Release();lease.SetHeld(true);lease.Release();
            Check(!held,"Normal release does not prevent the next tracking press");
            lease=new MouseLease(sender,delegate{return false;},delegate{return now;},delegate{return "Specific focus reason";});
            Check(!lease.Beat() && lease.Fault == "Specific focus reason", "The stop report preserves the specific safety reason");
        }
        private static void CheckBeat(MouseLease lease)
        { if(!lease.Beat())throw new Exception("A valid mouse lease unexpectedly expired"); }
        private static void TestMotion(Settings settings, double acceleration, double maxSpeed, bool abrupt)
        {
            var controller = new Controller();
            double gap = 170, velocity = 0;
            int total = 0, inside = 0;
            for (int frame = 0; frame < 800; frame++)
            {
                double time = frame * .05;
                double fish = 170 + 75 * Math.Sin(time * (abrupt ? 1.2 : .6));
                if (abrupt) fish += (Math.Floor(time / 4) % 2 == 0 ? 22 : -22);
                var seen = new Observation { Found = true, MenuVisible = true, FishY = fish,
                    GapY = gap, GapTop = (int)gap - 32, GapBottom = (int)gap + 32 };
                bool held = controller.Update(seen, time * 1000, settings);
                velocity = Math.Max(-maxSpeed, Math.Min(maxSpeed, velocity + (held ? -acceleration : acceleration) * .05));
                gap = Math.Max(34, Math.Min(306, gap + velocity * .05));
                if (frame > 40) { total++; if (Math.Abs(fish - gap) <= 32) inside++; }
            }
            double fraction = (double)inside / total;
            results.Add(string.Format("SIMULATION: {0}, inside the gap {1:P1} (synthetic physics, not a live-game guarantee)", abrupt ? "fast with direction jumps" : "slow", fraction));
            Check(fraction >= .75, "Controller follows " + (abrupt ? "fast and abrupt" : "slow") + " simulated fish movement");
        }
        // The previous released controller is retained ONLY as a comparison in
        // tests. These simulations never create a GameRuntime or send real input.
        private sealed class PreviousController
        {
            private double gap, fish, gv, fv; private bool started, held;
            internal bool Update(Observation o)
            {
                if (started)
                {
                    gv=.45*gv+.55*Math.Max(-1500,Math.Min(1500,(o.GapY-gap)/.05));
                    fv=.35*fv+.65*Math.Max(-1500,Math.Min(1500,(o.FishY-fish)/.05));
                }
                gap=o.GapY;fish=o.FishY;started=true;
                double command=-((fish-gap)+(fv-gv)*.08);
                double band=Math.Max(1.5,(o.GapBottom-o.GapTop)*.025);
                if(command>band)held=true;else if(command < -band)held=false;
                return held;
            }
        }
        private static double[] StabilityMotion(bool revised, double up, double down, double speed, int target, int delay)
        {
            var current = new Controller(); var previous = new PreviousController(); var settings = new Settings();
            var pending = new Queue<bool>(); for(int i=0;i<delay;i++)pending.Enqueue(false);
            double gap=240,velocity=0,error=0;int count=0,inside=0;
            for(int frame=0;frame<1200;frame++)
            {
                double t=frame*.05;
                double fish=target==0?170:170+75*Math.Sin(t*(target==1?.6:1.2));
                if(target==2)fish+=Math.Floor(t/4)%2==0?22:-22;
                double observedGap=Math.Round(gap)+Math.Sin(frame*1.7);
                var seen=new Observation {Found=true,MenuVisible=true,FishY=Math.Round(fish)+Math.Cos(frame*2.1),
                    GapY=observedGap,GapTop=(int)observedGap-20,GapBottom=(int)observedGap+20};
                pending.Enqueue(revised?current.Update(seen,t*1000,settings):previous.Update(seen));
                bool action=pending.Dequeue();
                velocity=Math.Max(-speed,Math.Min(speed,velocity+(action?-up:down)*.05));
                gap=Math.Max(34,Math.Min(306,gap+velocity*.05));
                if(frame>100){count++;error+=Math.Abs(gap-fish);if(Math.Abs(gap-fish)<=20)inside++;}
            }
            return new[]{error/count,(double)inside/count};
        }
        private static void TestStability()
        {
            Check(new Settings().CastMilliseconds==220 && new Settings().BiteSeconds==15,
                "New installations start with the user's 220 ms cast and 15 s bite wait");
            double[,] physics={{650,650,140},{1600,1600,250},{900,450,230},{450,900,230},{2200,1800,350}};
            double oldQuiet=0,newQuiet=0;
            for(int i=0;i<physics.GetLength(0);i++)
                for(int target=0;target<3;target++)
                    for(int delay=0;delay<=1;delay++)
                    {
                        double[] old=StabilityMotion(false,physics[i,0],physics[i,1],physics[i,2],target,delay);
                        double[] updated=StabilityMotion(true,physics[i,0],physics[i,1],physics[i,2],target,delay);
                        if(target==0){oldQuiet+=old[0];newQuiet+=updated[0];}
                        results.Add(string.Format("STABILITY: physics={0}, target={1}, delay={2} ms; mean error {3:F2} -> {4:F2} px; inside {5:P1} -> {6:P1}",
                            i,target,delay*50,old[0],updated[0],old[1],updated[1]));
                        Check(updated[0]<=old[0]*1.05 && updated[1]>=old[1]-.02,
                            "Tracking remains at least comparable with noise: physics "+i+", target "+target+", delay "+delay);
                    }
            Check(newQuiet < oldQuiet*.75,"Stationary-fish mean error falls by at least 25 percent across ten synthetic cases");
            results.Add(string.Format("STATIONARY: aggregate mean error reduced by {0:P1}; synthetic physics only.",1-newQuiet/oldQuiet));
        }
        private static BaitReading Reading(int? count, long sequence, double now)
        { return new BaitReading { Count=count, Sequence=sequence, SampledAt=now }; }
        private static FishingEngine EmptyBaitEngine(FakeGame game)
        {
            var engine=new FishingEngine(new Settings { MonitorBait=true,IdleJumpEnabled=true },game);
            engine.Start(0);
            game.Bait=Reading(0,1,0);engine.Tick(0);
            game.Bait=Reading(0,2,100);engine.Tick(100);
            game.Bait=Reading(0,3,200);engine.Tick(200);engine.Tick(1000);
            return engine;
        }
        private static void TestBaitAndIdle(string[] args)
        {
            foreach(string text in new[]{"300","x300","*300","×300"," X300 "})
                Check(BaitText.Parse(text)==300,"Read a single bait quantity: "+text);
            Check(BaitText.Parse("x0")==0,"A literal zero can be read");
            foreach(string text in new[]{"",null,"O","xO","x3OO","x.300","300 42","x300\nx42","-1","123456","Cebo 300"})
                Check(!BaitText.Parse(text).HasValue,"Ambiguous or missing OCR is not interpreted as bait: "+(text??"null"));
            var monitor=new BaitMonitor();
            monitor.Update(Reading(0,1,0),0);monitor.Update(Reading(0,1,0),50);monitor.Update(Reading(0,1,0),100);
            Check(!monitor.Empty,"Repeated reads of the same cached frame cannot confirm zero");
            monitor.Update(Reading(0,2,150),150);Check(!monitor.Empty,"Two zero frames are insufficient");
            monitor.Update(Reading(0,3,200),200);Check(monitor.Empty,"Three fresh consecutive zero frames confirm exhaustion");
            monitor.Update(Reading(null,4,250),250);Check(!monitor.Empty&&!monitor.Count.HasValue,"A missing counter is unknown, never zero");
            monitor.Update(Reading(42,5,300),300);monitor.Update(Reading(42,6,350),350);
            Check(monitor.Count==42,"Two consistent positive readings confirm the selected bait quantity");
            monitor.Update(Reading(42,6,350),6000);Check(!monitor.Count.HasValue,"Stale OCR readings expire");
            monitor.Update(Reading(0,7,7000),6500);Check(!monitor.Empty,"Future-dated OCR is rejected");
            monitor.Reset();monitor.Update(Reading(0,1,0),0);Check(!monitor.Empty,"Restarting the reader clears old confirmations");

            var game=new FakeGame();var engine=EmptyBaitEngine(game);
            Check(engine.State==Phase.IdleWaiting && game.Moves==0 && !game.Held,"Confirmed zero skips casting and enters idle wait");
            engine.Tick(2950);Check(game.Jumps==0,"Idle wait does not jump before the initial grace period");
            engine.Tick(3000);Check(game.JumpHeld && game.Jumps==1 && !game.Held,"Idle wait presses Space without holding the mouse");
            engine.Tick(3100);Check(!game.JumpHeld && engine.State==Phase.IdleWaiting,"Space is released after its short pulse");
            engine.Tick(63000);Check(game.Jumps==1,"No extra jump before the configured interval");
            engine.Tick(63100);Check(game.Jumps==2,"A later jump follows the configured interval");
            engine.Stop("F10");int jumps=game.Jumps;engine.Tick(100000);
            Check(!game.JumpHeld&&!game.Held&&game.Jumps==jumps,"Explicit stop cancels jumping and every later action");

            game=new FakeGame();engine=EmptyBaitEngine(game);engine.Tick(3000);game.Active=false;engine.Tick(3050);
            Check(!engine.Running&&!game.JumpHeld,"Losing Roblox focus during a jump releases Space and stops");
            game=new FakeGame();engine=EmptyBaitEngine(game);engine.Tick(3000);
            game.Current=new Observation {Found=true,MenuVisible=true,FishY=100,GapY=200};engine.Tick(3050);engine.Tick(3100);
            Check(!game.JumpHeld&&engine.State==Phase.Tracking,"A visible minigame interrupts jumping and resumes tracking");
            engine.Stop("test");

            game=new FakeGame();engine=EmptyBaitEngine(game);
            game.Current=new Observation {MenuVisible=true};engine.Tick(3000);engine.Tick(100000);
            Check(game.Jumps==0&&game.Moves==0,"A visible but unreadable minigame suppresses idle jumps");engine.Stop("test");
            game=new FakeGame();engine=EmptyBaitEngine(game);
            game.Bait=Reading(12,4,2000);engine.Tick(2000);
            game.Bait=Reading(12,5,3500);engine.Tick(3500);
            Check(engine.State==Phase.Preparing&&!game.JumpHeld,"Confirmed replenishment resumes fishing after an empty-bait wait");engine.Stop("test");

            game=new FakeGame();engine=new FishingEngine(new Settings {IdleJumpEnabled=true},game);engine.Start(0);
            for(int now=0;now<=55000;now+=50)engine.Tick(now);
            Check(engine.Running&&game.Moves==3&&game.Jumps>0,"Three casts without a minigame enter jumping wait without more casts");
            engine.Stop("test");
            game=new FakeGame();engine=new FishingEngine(new Settings {IdleJumpEnabled=false},game);engine.Start(0);
            for(int now=0;now<=60000;now+=50)engine.Tick(now);
            Check(!engine.Running&&game.Jumps==0,"Disabled idle jumps preserve the previous stop behavior");

            game=new FakeGame {Current=new Observation{Found=true,MenuVisible=true,FishY=100,GapY=200}};
            engine=new FishingEngine(new Settings {MonitorBait=true,IdleJumpEnabled=true},game);engine.Start(0);
            game.Bait=Reading(0,1,0);engine.Tick(0);game.Bait=Reading(0,2,50);engine.Tick(50);
            game.Bait=Reading(0,3,100);engine.Tick(100);
            Check(engine.State==Phase.Tracking&&game.Jumps==0,"The last bait reaching zero does not interrupt its active fish");
            game.Current=new Observation();engine.Tick(150);engine.Tick(1800);engine.Tick(3600);
            Check(engine.State==Phase.IdleWaiting&&game.Moves==0,"After the last fish closes, zero bait enters idle wait");engine.Stop("test");

            game=new FakeGame {Current=new Observation{Found=true,MenuVisible=true,FishY=100,GapY=200}};
            engine=new FishingEngine(new Settings {MonitorBait=true,IdleJumpEnabled=true},game);engine.Start(0);
            game.Bait=Reading(30,1,0);engine.Tick(0);game.Bait=Reading(30,2,50);engine.Tick(50);
            game.Current=new Observation();engine.Tick(150);engine.Tick(1800);engine.Tick(3600);
            Check(engine.State==Phase.Casting&&game.Moves==1&&game.Jumps==0,"Normal menu closure with bait casts again instead of jumping");engine.Stop("test");

            game=new FakeGame();engine=new FishingEngine(new Settings {MonitorBait=true,IdleJumpEnabled=true},game);
            engine.Start(0);engine.Tick(1000);engine.Tick(1250);
            game.Bait=Reading(0,1,1300);engine.Tick(1300);game.Bait=Reading(0,2,1350);engine.Tick(1350);
            game.Bait=Reading(0,3,1400);engine.Tick(1400);
            Check(engine.State==Phase.Waiting&&game.Jumps==0,"Zero after a cast still waits for the last bite");
            game.Current=new Observation{Found=true,MenuVisible=true,FishY=100,GapY=200};engine.Tick(1500);engine.Tick(1550);
            Check(engine.State==Phase.Tracking&&game.Jumps==0,"The last bite is tracked even though the counter is zero");engine.Stop("test");

            double time=0;bool keyDown=false;int keyPresses=0,failUps=0;
            var key=new MouseLease(delegate(bool down){if(!down&&failUps-->0)throw new IOException("rejected key-up");keyDown=down;if(down)keyPresses++;},delegate{return true;},delegate{return time;},null,"Espacio");
            key.Pulse(100);time=99;key.Watchdog();Check(keyDown,"Space pulse is held for its intended duration");
            time=100;key.Watchdog();Check(!keyDown&&!key.PendingRelease&&key.Fault==null,"Watchdog releases Space at 100 ms without a UI tick");
            key.Beat();key.Pulse(100);failUps=1;time=200;key.Watchdog();
            Check(!keyDown&&!key.PendingRelease&&key.Fault!=null,"A rejected Space-up is retried without another key-down");
            int before=keyPresses;bool rejected=false;try{key.Pulse(100);}catch{rejected=true;}
            Check(rejected&&keyPresses==before,"A failed Space lease cannot send later key-down events");

            var desktop=new Rectangle(0,0,1920,1080);
            Check(Settings.ValidateBaitArea(new Rectangle(200,300,30,17),desktop)==null,"A small bait counter rectangle is accepted");
            Check(Settings.ValidateBaitArea(new Rectangle(1910,300,30,17),desktop)!=null,"A counter outside the screen is rejected");
            Check(Settings.ValidateBaitArea(new Rectangle(20,20,500,100),desktop)!=null,"An excessively wide inventory selection is rejected");
            using(var background=new Bitmap(600,400))
            using(var selector=new SelectionOverlay(false,new Rectangle(50,50,30,17),background,new Rectangle(0,0,600,400),true))
                Check(selector.TryConfirm()&&selector.Selection.Size==new Size(30,17),"The counter selector allows a 17-pixel-high numeric region");
            if(args.Length>5)
            using(var source=new Bitmap(args[5]))
            {
                using(var cropped=source.Clone(new Rectangle(208,119,30,17),PixelFormat.Format32bppArgb))
                    Check(WindowsBaitReader.ReadImage(cropped).Count==300,"Windows OCR reads 300 common bait in the user's new screenshot");
                using(var cropped=source.Clone(new Rectangle(207,81,33,20),PixelFormat.Format32bppArgb))
                    Check(!WindowsBaitReader.ReadImage(cropped).Count.HasValue,"The difficult rare-bait lettering is unknown instead of guessed as a number or zero");
                using(var cropped=source.Clone(new Rectangle(205,80,35,58),PixelFormat.Format32bppArgb))
                    Check(!WindowsBaitReader.ReadImage(cropped).Count.HasValue,"Selecting both bait rows is rejected instead of combining the quantities");
                results.Add("OCR: two image scales agree on 300; the rare bait row is outside the selected region.");
                results.Add("OCR LIMIT: the 42 rare-bait crop was not recognized reliably; the reader reports unknown. Both rows together are rejected.");
            }
        }
        private static BaitReading Absent(long sequence,double time)
        { return new BaitReading{Sequence=sequence,SampledAt=time,VisuallyAbsent=true}; }
        private static ShopReading ShopFrame(ShopMenu menu,long seq,double time,int? maximum,int? quantity)
        {return new ShopReading{Menu=menu,Sequence=seq,SampledAt=time,Maximum=maximum,Quantity=quantity,Left=new Point(80,180),Middle=new Point(250,180)};}
        private static void FeedShop(PurchaseController purchase,FakeGame game,ShopMenu menu,long seq,double now,int? max,int? quantity,int? bait,double confirmedAt)
        {game.Shop=ShopFrame(menu,seq,now,max,quantity);purchase.Tick(now,bait,confirmedAt);}
        private static PurchaseController ReadyToVerify(FakeGame game,Settings settings)
        {
            var p=new PurchaseController(settings,game,game);p.Start(0);p.Tick(150,null,0);
            FeedShop(p,game,ShopMenu.Confirm,1,400,null,null,null,0);
            FeedShop(p,game,ShopMenu.Confirm,2,1400,null,null,null,0);
            FeedShop(p,game,ShopMenu.Quantity,3,1700,5,1,null,0);
            FeedShop(p,game,ShopMenu.Quantity,4,2700,5,1,null,0);
            for(int t=3000;t<=3500;t+=100)p.Tick(t,null,0);
            return p;
        }
        private static void TestPurchases(string[] args)
        {
            var m=new BaitMonitor();m.Reset();
            for(int t=0;t<=12000;t+=1500)m.Update(Absent(t/1500,t),t);
            Check(!m.Disappeared,"A never-readable counter cannot trigger disappearance replenishment");
            m.Update(Reading(3,20,13000),13000);m.Update(Reading(3,21,14500),14500);
            m.Update(Absent(22,16000),16000);m.Update(Absent(23,17500),17500);m.Update(Absent(24,19000),19000);
            Check(!m.Disappeared,"Short counter disappearance is ignored");
            m.Update(Absent(25,20500),20500);m.Update(Absent(26,22000),22000);m.Update(Absent(27,23500),23500);
            Check(!m.Disappeared,"Counter disappearance waits the full eight seconds");
            m.Update(Absent(28,25000),25000);Check(m.Disappeared&&!m.Count.HasValue&&!m.Empty,"Confirmed visual disappearance remains distinct from a numeric zero");
            m.Update(Absent(28,25000),31000);Check(!m.Disappeared,"A stalled OCR worker cannot maintain disappearance evidence");
            m.Update(Reading(2,30,32000),32000);m.Update(Reading(2,31,33500),33500);
            for(int t=35000;t<=47000;t+=1500)m.Update(Reading(null,32+(t-35000)/1500,t),t);
            Check(!m.Disappeared,"Unreadable but still visible yellow lettering does not imply disappearance");
            m.Reset();Check(!m.Disappeared&&!m.Count.HasValue,"Restart clears disappearance history");
            using(var blank=new Bitmap(30,20))Check(WindowsBaitReader.ReadImage(blank).VisuallyAbsent,"A counter crop without yellow text reports visual absence");
            Check(ShopText.Maximum("How many do you want? MAX: 5")==5,"Parse the displayed purchase maximum");
            Check(!ShopText.Maximum("MAX: 5 MAX: 8").HasValue&&!ShopText.Maximum("MAX: O").HasValue,"Ambiguous and letter-based purchase limits are rejected");
            Check(ShopText.Confirmation("Would you like to purchase 1 Common Fish Baits for 45 Peli?"),"Recognize the bait offer in game currency");
            Check(!ShopText.Confirmation("Purchase fish baits for Robux, not Peli"),"A Robux prompt cannot authorize a purchase");
            Check(!ShopText.Confirmation("Would you like to purchase a sword for 45 Peli?"),"Other item offers are rejected");
            Check(ShopLabels.RepeatedNumber("111")==1&&ShopLabels.RepeatedNumber("25 25 25")==25,"Short numeric fields require matching copies of the same glyph image");
            Check(!ShopLabels.RepeatedNumber("5 6 5").HasValue&&!ShopLabels.RepeatedNumber("O O O").HasValue,"Repeated-glyph disagreements and letters are rejected");
            using(var dots=new Bitmap(505,207)){
                using(var g=Graphics.FromImage(dots)){g.Clear(Color.FromArgb(40,40,40));for(int i=0;i<3;i++)g.FillEllipse(Brushes.White,242+i*8,199,4,4);}
                var box=new Rectangle(168,151,168,56);
                Check(WindowsShopReader.ThreeDots(dots,box),"Three aligned white dots identify the final central button");
                using(var g=Graphics.FromImage(dots))g.FillRectangle(Brushes.White,290,175,5,18);
                Check(!WindowsShopReader.ThreeDots(dots,box),"Additional lettering prevents a false ellipsis button");
            }
            var game=new FakeGame();var p=ReadyToVerify(game,new Settings{AutoBuyBait=true,BuyMaximum=true});
            Check(p.State==PurchasePhase.Verifying&&p.Quantity==5&&game.ShopClicks==2,"Purchase opens once, confirms Yes and types the displayed maximum");
            Check(game.KeyLog.Contains("+69")&&game.KeyLog.Contains("+17")&&game.KeyLog.Contains("+65")&&game.KeyLog.Contains("+53")&&game.KeysDown.Count==0,"Quantity entry sends E and Ctrl+A then 5, releasing every key");
            FeedShop(p,game,ShopMenu.Quantity,5,3800,5,1,0,3700);
            FeedShop(p,game,ShopMenu.Quantity,6,4800,5,1,0,4700);
            Check(!p.Submitted&&game.ShopClicks==2,"Wrong displayed quantity cannot click Buy");
            FeedShop(p,game,ShopMenu.Quantity,7,5200,5,5,0,5100);
            FeedShop(p,game,ShopMenu.Quantity,8,6200,5,5,0,6100);
            Check(p.Submitted&&game.ShopClicks==3,"Verified quantity submits Buy exactly once");
            FeedShop(p,game,ShopMenu.Done,9,6600,null,null,5,6100);
            FeedShop(p,game,ShopMenu.Done,10,7600,null,null,5,6100);
            Check(game.ShopClicks==4&&p.State==PurchasePhase.Closing,"The final ellipsis can close while the dialogue obscures the bait counter");
            FeedShop(p,game,ShopMenu.Unknown,11,7900,null,null,5,6100);
            FeedShop(p,game,ShopMenu.Unknown,12,8900,null,null,5,6100);
            Check(p.State==PurchasePhase.Closing,"Old positive bait readings cannot confirm a new purchase");
            FeedShop(p,game,ShopMenu.Unknown,13,9000,null,null,5,8950);
            FeedShop(p,game,ShopMenu.Unknown,14,10000,null,null,5,9950);
            Check(p.State==PurchasePhase.Complete&&game.KeysDown.Count==0,"A closed dialogue with fresh bait completes replenishment");
            p.Tick(11000,5,10900);Check(game.ShopClicks==4,"Completed purchase cannot repeat any click");
            game=new FakeGame();p=ReadyToVerify(game,new Settings{BuyMaximum=false,BuyQuantity=80});
            Check(p.Quantity==5,"A fixed quantity is capped by the visible maximum");
            game.Active=false;p.Tick(3600,null,0);Check(p.State==PurchasePhase.Failed&&game.KeysDown.Count==0,"Losing game focus stops a purchase and releases keys");
            game=new FakeGame();p=new PurchaseController(new Settings(),game,game);p.Start(0);p.Tick(150,null,0);
            FeedShop(p,game,ShopMenu.Confirm,1,500,null,null,null,0);p.Tick(600,null,0);p.Tick(700,null,0);
            Check(game.ShopClicks==0,"Repeated cached shop frames cannot authorize Yes");
            p.Tick(21000,null,0);Check(p.State==PurchasePhase.Failed&&game.ShopClicks==0&&game.KeysDown.Count==0,"Missing menus time out without any purchase clicks");
            game=new FakeGame();p=ReadyToVerify(game,new Settings{BuyMaximum=true});
            FeedShop(p,game,ShopMenu.Quantity,5,3800,3,5,null,0);FeedShop(p,game,ShopMenu.Quantity,6,4800,3,5,null,0);
            Check(!p.Submitted,"A maximum that drops below the typed amount blocks Buy");p.Fail("test");
            var settings=new Settings{MonitorBait=true,AutoBuyBait=true,IdleJumpEnabled=true};game=new FakeGame();var engine=new FishingEngine(settings,game);engine.Start(0);
            game.Bait=Reading(0,1,0);engine.Tick(0);game.Bait=Reading(0,2,100);engine.Tick(100);game.Bait=Reading(0,3,200);engine.Tick(200);engine.Tick(1000);
            Check(engine.State==Phase.Purchasing&&game.Moves==0&&game.Jumps==0&&engine.PurchaseAttempts==1,"Auto replenishment takes priority over idle jumps on confirmed zero");
            engine.Stop("F10");engine.Tick(4000);Check(game.KeysDown.Count==0&&game.ShopClicks==0,"F10 cancels a purchase while E is held");
            game=new FakeGame();engine=new FishingEngine(settings,game);engine.Start(0);for(int t=0;t<=55000;t+=50)engine.Tick(t);
            Check(game.ShopClicks==0&&engine.PurchaseAttempts==0&&game.Jumps>0,"Three failed casts alone cannot authorize spending on bait");engine.Stop("test");
            var desktop=new Rectangle(0,0,1920,1080);
            Check(Settings.ValidateShopArea(new Rectangle(100,300,505,207),desktop)==null,"The purchase dialogue area is accepted");
            Check(Settings.ValidateShopArea(new Rectangle(1900,0,505,207),desktop)!=null,"Purchase areas outside the desktop are rejected");
            Check(!new Settings().AutoBuyBait,"Automatic spending is disabled in new configurations");
            game=new FakeGame();engine=new FishingEngine(new Settings{MonitorBait=true,AutoBuyBait=true,IdleJumpEnabled=true,PurchaseLimit=1},game);engine.Start(0);
            bool resumed=false;
            for(int t=0;t<=26000;t+=50){
                ShopMenu menu=game.ShopClicks==0?ShopMenu.Confirm:game.ShopClicks<3?ShopMenu.Quantity:game.ShopClicks==3?ShopMenu.Done:ShopMenu.Unknown;
                game.Shop=ShopFrame(menu,t,t,5,game.ShopClicks<2?1:5);
                game.Bait=Reading(game.ShopClicks>=4&&t<6000?5:0,t,t);engine.Tick(t);
                if(game.ShopClicks==4&&engine.State==Phase.Preparing)resumed=true;
            }
            Check(resumed&&game.ShopClicks==4&&game.Moves==1,"The fishing engine completes the whole replenishment cycle and casts again");
            Check(engine.PurchaseAttempts==1&&game.Jumps>0,"The session purchase limit prevents another order and permits idle jumps");engine.Stop("test");
            game=new FakeGame{Current=new Observation{Found=true,MenuVisible=true,FishY=100,GapY=200}};
            engine=new FishingEngine(new Settings{MonitorBait=true,AutoBuyBait=true,IdleJumpEnabled=true},game);engine.Start(0);
            game.Bait=Reading(1,1,0);engine.Tick(0);game.Bait=Reading(1,2,50);engine.Tick(50);
            for(int t=100;t<=10600;t+=1500){game.Bait=Absent(3+(t-100)/1500,t);engine.Tick(t);}
            Check(engine.State==Phase.Tracking&&game.ShopClicks==0&&engine.PurchaseAttempts==0,"Counter disappearance cannot interrupt the active last fish");
            game.Current=new Observation();engine.Tick(10700);engine.Tick(12400);game.Bait=Absent(20,14200);engine.Tick(14200);
            Check(engine.State==Phase.Purchasing&&game.Moves==0,"A persistent disappearance begins replenishment only after the last fish closes");engine.Stop("test");
            if(args.Length>8){
                using(var source=new Bitmap(args[6]))using(var crop=source.Clone(new Rectangle(80,358,505,204),PixelFormat.Format32bppArgb)){
                    var reading=WindowsShopReader.ReadImage(crop);Check(reading.Menu==ShopMenu.Confirm,"The supplied purchase offer and Sí/No buttons are recognized");
                    Check(!ShopLabels.Match(crop,new Rectangle(336,148,169,56),ShopLabels.Yes),"The actual No button does not match Sí");
                }
                using(var source=new Bitmap(args[7]))using(var crop=source.Clone(new Rectangle(62,24,505,207),PixelFormat.Format32bppArgb)){
                    var reading=WindowsShopReader.ReadImage(crop);Check(reading.Menu==ShopMenu.Quantity&&reading.Maximum==5&&reading.Quantity==1,"The supplied amount dialogue reads MAX 5 and quantity 1");
                }
                using(var source=new Bitmap(args[8]))using(var crop=source.Clone(new Rectangle(62,24,505,207),PixelFormat.Format32bppArgb))
                    Check(WindowsShopReader.ReadImage(crop).Menu==ShopMenu.Unknown,"The cropped final screenshot without visible dots cannot authorize a close click");
                results.Add("SHOP LIMIT: the supplied final screenshot does not show the ellipsis button; that detector is validated with synthetic dots only. No live purchases were sent.");
            }
            if(args.Length>9)using(var source=new Bitmap(args[9]))using(var crop=source.Clone(new Rectangle(403,388,30,17),PixelFormat.Format32bppArgb))
                Check(WindowsBaitReader.ReadImage(crop).Count==295,"The supplied 295-bait counter is readable when the yellow button border is excluded");
        }
        private sealed class FakeGame : IGameRuntime, IShopRuntime
        {
            public bool Active = true, Held, JumpHeld;
            public int Moves, Presses, Jumps;
            public BaitReading Bait = new BaitReading();
            public ShopReading Shop=new ShopReading();
            public int ShopClicks;
            public readonly System.Collections.Generic.HashSet<int> KeysDown=new System.Collections.Generic.HashSet<int>();
            public readonly System.Collections.Generic.List<string> KeyLog=new System.Collections.Generic.List<string>();
            public Observation Current = new Observation();
            public bool IsActive { get { return Active; } }
            public void MoveToCastPoint() { if (!Active) throw new Exception("Inactive input"); Moves++; }
            public void SetHeld(bool held) { if (held && !Active) throw new Exception("Inactive input"); if (held && !Held) Presses++; Held = held; }
            public void Release() { Held = false; JumpHeld = false; KeysDown.Clear(); }
            public ShopReading ReadShop(double now){return Shop;}
            public void ShopClick(Point point){if(!Active)throw new Exception("Inactive shop click");ShopClicks++;}
            public void ShopKey(int key,bool held){if(held&&!Active)throw new Exception("Inactive shop key");KeyLog.Add((held?"+":"-")+key);if(held)KeysDown.Add(key);else KeysDown.Remove(key);}
            public Observation Observe() { return Current; }
            public BaitReading ReadBait(double now) { return Bait; }
            public void SetJumpHeld(bool held)
            {
                if(held && !Active)throw new Exception("Inactive jump");
                if(held && !JumpHeld)Jumps++;
                JumpHeld=held;
            }
        }
    }
}
