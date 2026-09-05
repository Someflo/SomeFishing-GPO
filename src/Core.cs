using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace SomeFishingGPO
{
    public class Settings
    {
        public Rectangle Area;
        public Point CastPoint = new Point(-1, -1);
        public bool CastPointSet;
        public bool AutoCast = true;
        public int CastMilliseconds = 220;
        public int BiteSeconds = 15;
        public int RestMilliseconds = 1800;
        public int BlueArgb = Color.FromArgb(85, 170, 255).ToArgb();
        public int MarkerArgb = Color.White.ToArgb();
        public int Tolerance = 38;
        public bool HoldMovesUp = true;
        public int AnticipationMilliseconds = 80;
        public bool MonitorBait;
        public Rectangle BaitArea;
        public bool IdleJumpEnabled;
        public int IdleJumpSeconds = 60;
        public bool AutoBuyBait;
        public Rectangle ShopArea;
        public bool BuyMaximum = true;
        public int BuyQuantity = 5;
        public int PurchaseLimit = 10;

        public string Validate(Rectangle desktop, bool sending)
        {
            if (Area.Width < 8 || Area.Height < 50 || Area.Width > 1200 || Area.Height > 1400 || (long)Area.Width * Area.Height > 600000)
                return "Selecciona una zona de al menos 8 × 50 px, hasta 1200 × 1400 px y 600 000 píxeles en total.";
            if (!ContainsSafely(desktop, Area)) return "La zona queda fuera de la pantalla. Selecciónala de nuevo.";
            if (Tolerance < 5 || Tolerance > 90) return "La tolerancia debe estar entre 5 y 90.";
            if (AnticipationMilliseconds < 0 || AnticipationMilliseconds > 300) return "Anticipación fuera de rango.";
            if (CastMilliseconds < 50 || CastMilliseconds > 3000 || BiteSeconds < 5 || BiteSeconds > 120 || RestMilliseconds < 500 || RestMilliseconds > 10000)
                return "Revisa los tiempos de lanzamiento y espera.";
            if (sending && AutoCast && (!CastPointSet || !ContainsSafely(desktop, new Rectangle(CastPoint, new Size(1, 1))))) return "Selecciona un punto sobre el agua para lanzar.";
            if (IdleJumpSeconds < 15 || IdleJumpSeconds > 300) return "El intervalo de saltos debe estar entre 15 y 300 segundos.";
            if (MonitorBait) { string problem = ValidateBaitArea(BaitArea, desktop); if (problem != null) return problem; }
            if (AutoBuyBait)
            {
                if (!MonitorBait) return "Activa y configura la lectura del cebo antes de habilitar las compras.";
                string problem = ValidateShopArea(ShopArea, desktop); if (problem != null) return problem;
                if (BuyQuantity<1||BuyQuantity>9999||PurchaseLimit<1||PurchaseLimit>100) return "Revisa la cantidad de compra y el límite de compras por sesión.";
            }
            return null;
        }
        public static string ValidateShopArea(Rectangle area, Rectangle desktop)
        {
            if(area.Width<150||area.Height<80||area.Width>1000||area.Height>600) return "Selecciona el diálogo completo y sus botones, entre 150 × 80 y 1000 × 600 px.";
            return ContainsSafely(desktop,area)?null:"La zona de compra queda fuera de la pantalla.";
        }
        public static string ValidateBaitArea(Rectangle area, Rectangle desktop)
        {
            if (area.Width < 10 || area.Height < 6 || area.Width > 400 || area.Height > 120 || (long)area.Width * area.Height > 30000)
                return "Rodea solo la cantidad de cebo: entre 10 × 6 y 400 × 120 px, hasta 30 000 píxeles.";
            return ContainsSafely(desktop, area) ? null : "El contador queda fuera de la pantalla.";
        }
        public static bool ContainsSafely(Rectangle outer, Rectangle inner)
        {
            return outer.Width > 0 && outer.Height > 0 && inner.Width > 0 && inner.Height > 0
                && inner.X >= outer.X && inner.Y >= outer.Y
                && (long)inner.X + inner.Width <= (long)outer.X + outer.Width
                && (long)inner.Y + inner.Height <= (long)outer.Y + outer.Height;
        }

        public static Settings Load(string path)
        {
            if (!File.Exists(path)) return new Settings();
            if (new FileInfo(path).Length > 32768) throw new InvalidDataException("Archivo de ajustes demasiado grande.");
            var options = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32768 };
            using (var reader = System.Xml.XmlReader.Create(path, options))
            {
                var loaded = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(reader);
                if (loaded == null) throw new InvalidDataException("Los ajustes están vacíos. Se usarán los valores iniciales.");
                return loaded;
            }
        }
        public void Save(string path)
        {
            string temporary = path + ".tmp";
            using (var writer = new StreamWriter(temporary, false, System.Text.Encoding.UTF8))
                new XmlSerializer(typeof(Settings)).Serialize(writer, this);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }

    public sealed class Observation
    {
        public bool Found;
        public bool MenuVisible;
        public double FishY;
        public double GapY;
        public int GapTop, GapBottom;
        public Rectangle BarBounds;
        public string Detail = "Sin barra detectable";
    }

    // Works only on a bitmap. Does not inspect game memory or execute game code.
    public static class Detector
    {
        public static Observation Analyze(Bitmap image, Settings settings)
        {
            int width = image.Width, height = image.Height;
            if (width < 8 || height < 50) return new Observation();
            // Locate the blue track anywhere across the selected area. Only then
            // measure its gray gap and white fish line in an isolated narrow strip.
            // The green progress bar never contributes to blue/white measurements.
            int[] dark = new int[width], blue = new int[width];
            Color target = Color.FromArgb(settings.BlueArgb);
            BitmapData data = image.LockBits(new Rectangle(0,0,width,height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[Math.Abs(data.Stride)];
                for (int y=0; y<height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);
                    for (int x=0; x<width; x++)
                    {
                        int i=x*4; byte r=row[i+2],g=row[i+1],b=row[i];
                        if (IsDark(r,g,b)) dark[x]++;
                        if (!IsGreen(r,g,b) && Matches(r,g,b,target,settings.Tolerance)) blue[x]++;
                    }
                }
            }
            finally { image.UnlockBits(data); }
            var bands = new System.Collections.Generic.List<Point>();
            int start=-1;
            for(int x=0;x<=width;x++)
            {
                bool edge=x<width && dark[x]>=height*.60;
                if(edge && start<0)start=x;
                if(!edge && start>=0)
                {
                    // A blue sky above/below the track can contribute to every
                    // column. Compare locally, so vertical padding is tolerated
                    // without merging a long gray gap into the track's borders.
                    int minimum=height;
                    for(int column=start;column<x;column++)minimum=Math.Min(minimum,blue[column]);
                    int run=-1;
                    for(int column=start;column<=x;column++)
                    {
                        bool border=column<x && blue[column]<minimum+height*.10;
                        if(border && run<0)run=column;
                        if(!border && run>=0){bands.Add(new Point(run,column-1));run=-1;}
                    }
                    start=-1;
                }
            }
            Observation best=null, partial=null;
            int maxInterior=Math.Min(120,Math.Max(12,height/3));
            for(int i=0;i+1<bands.Count;i++)
            {
                int left=bands[i].Y, right=bands[i+1].X;
                if(right-left<4 || right-left>maxInterior)continue;
                bool enclosedBlue=false;
                for(int x=left+1;x<right;x++) if(blue[x]>=height*.20)enclosedBlue=true;
                if(!enclosedBlue)continue;
                int x0=Math.Max(bands[i].X,left-2), x1=Math.Min(bands[i+1].Y+1,right+3);
                var box=new Rectangle(x0,0,x1-x0,height);
                Observation found;
                using(Bitmap strip=image.Clone(box,PixelFormat.Format32bppArgb)) found=AnalyzeTrack(strip,settings);
                found.BarBounds=box;
                if(found.MenuVisible && partial==null)partial=found;
                if(!found.Found)continue;
                if(best!=null)
                    return new Observation { MenuVisible=true,Detail="Veo más de una barra azul. Reduce la zona para incluir un solo minijuego." };
                best=found;
            }
            return best ?? partial ?? new Observation { Detail="Buscando la barra azul y sus dos bordes. Incluye toda su altura y margen a los lados." };
        }
        private static bool IsGreen(byte r,byte g,byte b)
        { return g>70 && g>r+18 && g>b+18; }
        private static bool IsDark(byte r,byte g,byte b)
        {
            int high=Math.Max(r,Math.Max(g,b)),low=Math.Min(r,Math.Min(g,b));
            return low>=12 && high<=48 && high-low<=14;
        }
        private static Observation AnalyzeTrack(Bitmap image, Settings settings)
        {
            int width = image.Width, height = image.Height;
            var result = new Observation();
            if (width < 8 || height < 50) return result;
            var blue = new int[height];
            var white = new int[height];
            var dark = new int[height];
            var blueColumns = new int[width];
            var darkColumns = new int[width];
            Color background = Color.FromArgb(settings.BlueArgb);
            Color marker = Color.FromArgb(settings.MarkerArgb);
            Rectangle bounds = new Rectangle(0, 0, width, height);
            BitmapData data = image.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] row = new byte[stride];
                // Ignore the side borders; a user-selected region can include a little padding.
                int x0 = width / 5, x1 = width - width / 5;
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, stride);
                    for (int x = 0; x < width; x++)
                    {
                        int i = x * 4;
                        bool green = IsGreen(row[i + 2], row[i + 1], row[i]);
                        bool isBlue = !green && Matches(row[i + 2], row[i + 1], row[i], background, settings.Tolerance);
                        int high = Math.Max(row[i], Math.Max(row[i + 1], row[i + 2]));
                        int low = Math.Min(row[i], Math.Min(row[i + 1], row[i + 2]));
                        bool isDark = low >= 12 && high <= 48 && high - low <= 14;
                        if (isBlue) blueColumns[x]++;
                        if (isDark) darkColumns[x]++;
                        if (x >= x0 && x < x1)
                        {
                            if (isBlue) blue[y]++;
                            if (isDark) dark[y]++;
                            if (!green && Matches(row[i + 2], row[i + 1], row[i], marker, settings.Tolerance)) white[y]++;
                        }
                    }
                }
            }
            finally { image.UnlockBits(data); }

            int innerWidth = width - 2 * (width / 5);
            int requiredBlue = Math.Max(2, (int)Math.Ceiling(innerWidth * .18));
            int requiredWhite = Math.Max(2, (int)Math.Ceiling(innerWidth * .20));
            int first = -1, last = -1, blueRows = 0;
            for (int y = 0; y < height; y++)
                if (blue[y] >= requiredBlue) { if (first < 0) first = y; last = y; blueRows++; }
            if (first < 0 || last - first < 30 || blueRows < Math.Max(20, height / 6))
            { result.Detail = "No veo suficiente azul. Ajusta la zona o el color."; return result; }
            // Color alone is not evidence of a menu: the ocean and sky can match.
            // Require a cyan column enclosed by two long, neutral dark track edges.
            int leftEdge = -1, rightEdge = -1;
            for (int x = 0; x < width; x++)
                if (darkColumns[x] >= height * .60) { if (leftEdge < 0) leftEdge = x; rightEdge = x; }
            bool enclosedBlue = false;
            for (int x = Math.Max(0, leftEdge + 1); x < rightEdge; x++)
                if (blueColumns[x] >= height * .20) enclosedBlue = true;
            if (!enclosedBlue)
            { result.Detail = "No veo los dos bordes oscuros. Incluye la barra completa, sin paisaje."; return result; }
            result.MenuVisible = true;

            // The controllable gray gap can reach either end of the track. Extend
            // past the visible blue using the dark track, including small anti-aliased
            // holes, rather than requiring blue on both sides of the gap.
            int requiredTrack = Math.Max(2, (int)Math.Ceiling(innerWidth * .50));
            int trackFirst = first, trackLast = last, holes = 0;
            for (int y = first - 1; y >= 0; y--)
            {
                if (dark[y] + blue[y] >= requiredTrack || white[y] >= requiredWhite)
                { trackFirst = y; holes = 0; }
                else if (++holes > 3) break;
            }
            holes = 0;
            for (int y = last + 1; y < height; y++)
            {
                if (dark[y] + blue[y] >= requiredTrack || white[y] >= requiredWhite)
                { trackLast = y; holes = 0; }
                else if (++holes > 3) break;
            }

            // The gray gap is controlled by the mouse. The white horizontal line is
            // the fish to follow. Its thin interruption is too short to be the gap.
            int bestStart = -1, bestEnd = -1, current = -1;
            for (int y = trackFirst; y <= trackLast + 1; y++)
            {
                bool gap = y <= trackLast && blue[y] < requiredBlue;
                if (gap && current < 0) current = y;
                if (!gap && current >= 0)
                {
                    int length = y - current;
                    if (length > bestEnd - bestStart + 1) { bestStart = current; bestEnd = y - 1; }
                    current = -1;
                }
            }
            int gapSize = bestEnd - bestStart + 1;
            if (bestStart < 0 || gapSize < Math.Max(6, height / 40) || gapSize > (trackLast - trackFirst) * .65)
            { result.Detail = "No localizo el hueco entre los tramos azules."; return result; }
            int darkGapRows = 0;
            for (int y = bestStart; y <= bestEnd; y++)
                if (dark[y] >= Math.Max(2, innerWidth * .35)) darkGapRows++;
            if (darkGapRows < gapSize * .65)
            { result.Detail = "El hueco no tiene el fondo gris esperado."; return result; }

            int markerStart = -1, markerEnd = -1, score = 0, run = -1, runScore = 0;
            // Allow a small border margin for a marker right at a bar endpoint.
            for (int y = Math.Max(0, trackFirst - 5); y <= Math.Min(height - 1, trackLast + 5) + 1; y++)
            {
                bool match = y < height && y <= trackLast + 5 && white[y] >= requiredWhite;
                if (match) { if (run < 0) run = y; runScore += white[y]; }
                else if (run >= 0)
                {
                    int thickness = y - run;
                    if (thickness <= Math.Max(14, height / 12) && runScore > score)
                    { markerStart = run; markerEnd = y - 1; score = runScore; }
                    run = -1; runScore = 0;
                }
            }
            if (markerStart < 0)
            { result.Detail = "Veo el hueco, pero no la marca blanca."; return result; }
            result.Found = true;
            result.FishY = (markerStart + markerEnd) / 2.0;
            result.GapTop = bestStart; result.GapBottom = bestEnd;
            result.GapY = (bestStart + bestEnd) / 2.0;
            result.Detail = "Pez (línea blanca) y hueco gris detectados";
            return result;
        }

        private static bool Matches(byte r, byte g, byte b, Color target, int tolerance)
        { return Math.Abs(r - target.R) <= tolerance && Math.Abs(g - target.G) <= tolerance && Math.Abs(b - target.B) <= tolerance; }
    }

    public sealed class Controller
    {
        private double previousGap, previousFish, previousTime, gapVelocity, fishVelocity;
        private bool initialized, holding;
        public void Reset() { initialized = false; holding = false; gapVelocity = fishVelocity = 0; }
        public bool Update(Observation observation, double milliseconds, Settings settings)
        {
            if (!observation.Found) { Reset(); return false; }
            if (initialized)
            {
                double seconds = Math.Max(.02, Math.Min(.25, (milliseconds - previousTime) / 1000));
                double measuredGap = (observation.GapY - previousGap) / seconds;
                double measuredFish = (observation.FishY - previousFish) / seconds;
                // A shorter filter delay lets us brake before passing the target.
                gapVelocity = .28 * gapVelocity + .72 * Math.Max(-1500, Math.Min(1500, measuredGap));
                fishVelocity = .35 * fishVelocity + .65 * Math.Max(-1500, Math.Min(1500, measuredFish));
            }
            previousGap = observation.GapY; previousFish = observation.FishY; previousTime = milliseconds; initialized = true;
            double horizon = settings.AnticipationMilliseconds / 1000.0;
            // Near a stationary fish, counter the gap's momentum earlier. Fade
            // this extra damping out as the fish moves, avoiding a mode switch.
            double quietWeight = Math.Max(0, 1 - Math.Abs(fishVelocity) / 40.0);
            horizon += .12 * quietWeight;
            double predictedGap = observation.GapY + gapVelocity * horizon;
            double predictedFish = observation.FishY + fishVelocity * horizon;
            double error = predictedFish - predictedGap;
            double command = (settings.HoldMovesUp ? -1 : 1) * error;
            double deadband = Math.Max(1, (observation.GapBottom - observation.GapTop) * .01);
            if (command > deadband) holding = true;
            else if (command < -deadband) holding = false;
            return holding;
        }
    }

    // A short renewable lease bounds a held click even if the UI timer stops.
    // All operations are serialized, including the background watchdog. Failed
    // button-up attempts remain pending and can be retried without another down.
    internal sealed class MouseLease
    {
        private readonly object gate = new object();
        private readonly Action<bool> send;
        private readonly Func<bool> allowed;
        private readonly Func<double> clock;
        private readonly Func<string> safetyReason;
        private readonly string inputName;
        private double lastBeat;
        private double releaseAt = double.PositiveInfinity;
        private bool held, stopped;
        private string fault;
        internal MouseLease(Action<bool> send, Func<bool> allowed, Func<double> clock, Func<string> safetyReason = null, string inputName = "el clic")
        { this.send = send; this.allowed = allowed; this.clock = clock; this.safetyReason = safetyReason; this.inputName = inputName; lastBeat = clock(); }
        internal bool PendingRelease { get { lock (gate) return held; } }
        internal string Fault { get { lock (gate) return fault; } }
        private bool Allowed()
        { try { return allowed(); } catch { return false; } }
        private void ReleaseLocked()
        {
            if (!held) return;
            try { send(false); held = false; releaseAt = double.PositiveInfinity; }
            catch (Exception error) { stopped = true; fault = "Windows no aceptó soltar " + inputName + "; reintentando. " + error.Message; }
        }
        private void Trip(string reason)
        { stopped = true; if (fault == null) fault = reason; ReleaseLocked(); }
        private bool CheckLocked()
        {
            if (held && clock() >= releaseAt) ReleaseLocked();
            if (stopped) { ReleaseLocked(); return false; }
            double elapsed = clock() - lastBeat;
            if (elapsed > 500 || elapsed < 0)
            { Trip("Parada de protección: la interfaz dejó de responder durante más de medio segundo."); return false; }
            if (!Allowed())
            { Trip(safetyReason == null ? "Parada de protección: F10, esquina de parada o cambio de ventana." : safetyReason()); return false; }
            return true;
        }
        internal bool Beat()
        {
            lock (gate)
            {
                if (!CheckLocked()) return false;
                lastBeat = clock(); return true;
            }
        }
        internal void SetHeld(bool value)
        {
            lock (gate)
            {
                if (!value) { ReleaseLocked(); return; }
                if (!CheckLocked()) throw new InvalidOperationException(fault ?? "Macro detenida.");
                if (held) return;
                // Treat an input failure as uncertain until button-up succeeds.
                held = true;
                try { send(true); }
                catch (Exception error) { Trip(error.Message); throw; }
            }
        }
        internal void Release() { lock (gate) ReleaseLocked(); }
        internal void Pulse(int milliseconds)
        {
            if (milliseconds < 1 || milliseconds > 500) throw new ArgumentOutOfRangeException("milliseconds");
            lock (gate) { SetHeld(true); releaseAt = clock() + milliseconds; }
        }
        internal void Stop() { lock (gate) { stopped = true; ReleaseLocked(); } }
        internal void Watchdog() { lock (gate) CheckLocked(); }
    }

    public interface IGameRuntime
    {
        bool IsActive { get; }
        void MoveToCastPoint();
        void SetHeld(bool held);
        void Release();
        Observation Observe();
        BaitReading ReadBait(double now);
        void SetJumpHeld(bool held);
    }

    public enum Phase { Stopped, Preparing, Casting, Waiting, Tracking, Resting, IdleWaiting, Jumping, Purchasing }

    // Nonblocking state machine. A GUI timer drives this; no delayed background click
    // can survive Stop(). Every tick checks game focus before any action.
    public sealed class FishingEngine
    {
        private readonly Settings settings;
        private readonly IGameRuntime runtime;
        private readonly Controller controller = new Controller();
        private double deadline, missingSince = -1, invalidSince = -1, trackingStarted;
        private int stableFrames, failures;
        private readonly BaitMonitor bait = new BaitMonitor();
        private double nextJump, jumpUntil, idleMenuMissingSince;
        private bool waitingForBait;
        private string idleReason;
        private PurchaseController purchase;
        public int PurchaseAttempts { get; private set; }
        public bool PurchaseSubmitted { get { return purchase != null && purchase.Submitted; } }
        public int? BaitCount { get { return bait.Count; } }
        public string BaitStatus { get { return settings.MonitorBait ? bait.Detail : "Lectura de cebo desactivada"; } }
        public int JumpRequests { get; private set; }
        public Phase State { get; private set; }
        public string Status { get; private set; }
        public int Cycles { get; private set; }
        public bool SuggestedHold { get; private set; }
        public Observation LastObservation { get; private set; }
        public bool Running { get { return State != Phase.Stopped; } }

        public FishingEngine(Settings settings, IGameRuntime runtime)
        { this.settings = settings; this.runtime = runtime; State = Phase.Stopped; Status = "Detenida"; }
        public void Start(double now)
        {
            if (!runtime.IsActive) throw new InvalidOperationException("Roblox debe estar en primer plano.");
            failures = 0; Cycles = 0; stableFrames = 0; missingSince = invalidSince = -1; controller.Reset();
            bait.Reset();
            JumpRequests = 0;
            PurchaseAttempts = 0; purchase = null;
            State = Phase.Preparing; deadline = now + 1000; Status = "Preparando la pesca…";
        }
        public void Stop(string reason)
        {
            State = Phase.Stopped; Status = reason; SuggestedHold = false;
            controller.Reset(); runtime.Release();
        }
        private void BeginCast(double now)
        {
            controller.Reset(); stableFrames = 0; missingSince = invalidSince = -1;
            if (HandleNoBait(now)) return;
            if (!settings.AutoCast)
            { State = Phase.Waiting; deadline = double.PositiveInfinity; Status = "Esperando a que lances manualmente…"; return; }
            runtime.MoveToCastPoint(); runtime.SetHeld(true);
            State = Phase.Casting; deadline = now + settings.CastMilliseconds; Status = "Lanzando la caña";
        }
        private bool HandleNoBait(double now)
        {
            if(!settings.MonitorBait||(!bait.Empty&&!bait.Disappeared))return false;
            string reason=bait.Empty?"Sin cebo confirmado":"Contador desaparecido durante 8 s";
            if(settings.AutoBuyBait && PurchaseAttempts<settings.PurchaseLimit)
            {
                var shop=runtime as IShopRuntime;
                if(shop==null){Stop("El lector de compra no está disponible");return true;}
                purchase=new PurchaseController(settings,runtime,shop);PurchaseAttempts++;
                State=Phase.Purchasing;SuggestedHold=false;purchase.Start(now);Status=purchase.Status;return true;
            }
            EnterIdle(now,settings.AutoBuyBait?reason+" · límite de compras alcanzado":reason,true);return true;
        }
        private void EnterIdle(double now, string reason, bool noBait)
        {
            runtime.Release(); controller.Reset(); SuggestedHold = false;
            if (!settings.IdleJumpEnabled) { Stop(reason + " · saltos de espera desactivados"); return; }
            State = Phase.IdleWaiting; idleReason = reason; waitingForBait = noBait;
            stableFrames = 0; idleMenuMissingSince = now; nextJump = now + 2000;
            Status = reason + " · espera con saltos";
        }
        private void TickIdle(double now)
        {
            LastObservation = runtime.Observe();
            if (LastObservation.Found || LastObservation.MenuVisible)
            {
                runtime.SetJumpHeld(false); State = Phase.IdleWaiting;
                idleMenuMissingSince = -1; nextJump = now + settings.IdleJumpSeconds * 1000;
                if (LastObservation.Found && ++stableFrames >= 2)
                { State = Phase.Tracking; trackingStarted = now; controller.Reset(); failures = 0; }
                else if (!LastObservation.Found) stableFrames = 0;
                Status = "Menú visible · saltos suspendidos"; return;
            }
            stableFrames = 0;
            if (idleMenuMissingSince < 0) idleMenuMissingSince = now;
            if (waitingForBait && bait.Count.HasValue && bait.Count.Value > 0)
            {
                runtime.Release(); failures = 0; State = Phase.Preparing; deadline = now + 1000;
                Status = "Cebo disponible · reanudando pesca"; return;
            }
            if (State == Phase.Jumping)
            {
                if (now >= jumpUntil)
                { runtime.SetJumpHeld(false); State = Phase.IdleWaiting; nextJump = now + settings.IdleJumpSeconds * 1000; }
                return;
            }
            Status = idleReason + " · próximo salto en " + Math.Max(0, Math.Ceiling((nextJump - now) / 1000)) + " s";
            if (now >= nextJump && now - idleMenuMissingSince >= 1600)
            { runtime.SetJumpHeld(true); JumpRequests++; State = Phase.Jumping; jumpUntil = now + 100; Status = idleReason + " · salto en espera"; }
        }
        public void Tick(double now)
        {
            if (!Running) return;
            try
            {
                if (!runtime.IsActive) { Stop("Detenida: cambiaste de ventana o activaste la parada con el ratón."); return; }
                if (settings.MonitorBait) bait.Update(runtime.ReadBait(now), now);
                if (State == Phase.Purchasing)
                {
                    LastObservation=runtime.Observe();
                    if(LastObservation.Found||LastObservation.MenuVisible){Stop("Compra detenida: apareció el minijuego. Reinicia cuando termine el diálogo.");return;}
                    purchase.Tick(now,bait.Count,bait.ConfirmedAt);Status=purchase.Status;
                    if(purchase.State==PurchasePhase.Failed){Stop(purchase.Status);return;}
                    if(purchase.State==PurchasePhase.Complete){failures=0;State=Phase.Preparing;deadline=now+1000;}
                    return;
                }
                if (State == Phase.IdleWaiting || State == Phase.Jumping) { TickIdle(now); return; }
                if (State == Phase.Tracking && now - trackingStarted > 120000)
                { Stop("Detenida: la ronda superó 2 minutos. Revisa la detección."); return; }
                if (State == Phase.Preparing)
                {
                    LastObservation = runtime.Observe();
                    if (LastObservation.Found || LastObservation.MenuVisible)
                    { State = Phase.Waiting; deadline = now + settings.BiteSeconds * 1000; }
                    else if (now >= deadline) BeginCast(now);
                }
                if (State == Phase.Casting)
                {
                    if (now >= deadline)
                    { runtime.SetHeld(false); State = Phase.Waiting; deadline = now + settings.BiteSeconds * 1000; Status = "Esperando la picada…"; }
                    return;
                }
                if (State == Phase.Waiting || State == Phase.Tracking)
                {
                    LastObservation = runtime.Observe();
                    if (LastObservation.Found)
                    {
                        missingSince = invalidSince = -1;
                        if (++stableFrames >= 2)
                        {
                            if (State != Phase.Tracking) { trackingStarted = now; controller.Reset(); }
                            State = Phase.Tracking;
                            SuggestedHold = controller.Update(LastObservation, now, settings);
                            runtime.SetHeld(SuggestedHold);
                            Status = SuggestedHold ? "Clic mantenido · subiendo el hueco" : "Clic suelto · bajando el hueco";
                            if (!settings.HoldMovesUp) Status = SuggestedHold ? "Clic mantenido · bajando el hueco" : "Clic suelto · subiendo el hueco";
                            if (now - trackingStarted > 120000) Stop("Detenida: la ronda superó 2 minutos. Revisa la detección.");
                        }
                    }
                    else
                    {
                        stableFrames = 0; SuggestedHold = false; runtime.SetHeld(false); controller.Reset();
                        if (LastObservation.MenuVisible)
                        {
                            missingSince = -1;
                            if (invalidSince < 0) invalidSince = now;
                            Status = "Menú visible, detección incompleta · clic liberado";
                            if (now - invalidSince >= 3000)
                            { Stop("Detenida: el menú sigue abierto, pero perdí la detección. Revisa la zona."); return; }
                        }
                        else if (State == Phase.Tracking)
                        {
                            invalidSince = -1;
                            if (missingSince < 0) missingSince = now;
                            Status = "Comprobando cierre del menú · clic liberado";
                            if (now - missingSince >= 1600)
                            { Cycles++; failures = 0; State = Phase.Resting; deadline = now + settings.RestMilliseconds; Status = "Ronda terminada · pausa"; }
                        }
                    }
                    if (State == Phase.Waiting && now >= deadline)
                    {
                        runtime.SetHeld(false);
                        if (LastObservation.MenuVisible || LastObservation.Found)
                        { Stop("Detenida: el minijuego está visible, pero no pude seguirlo. Revisa la zona."); return; }
                        if (HandleNoBait(now)) return;
                        if (++failures >= 3)
                        { EnterIdle(now, bait.Empty ? "Sin cebo confirmado" : "3 intentos sin minijuego", bait.Empty); return; }
                        State = Phase.Resting; deadline = now + settings.RestMilliseconds; Status = "Sin picada · preparando otro intento";
                    }
                }
                if (State == Phase.Resting && now >= deadline)
                {
                    LastObservation = runtime.Observe();
                    if (LastObservation.MenuVisible || LastObservation.Found)
                    { State = Phase.Waiting; deadline = now + settings.BiteSeconds * 1000; stableFrames = 0; }
                    else BeginCast(now);
                }
            }
            catch (Exception error) { Stop("Detenida: " + error.Message); }
        }
    }
}
