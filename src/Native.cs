using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SomeFishingGPO
{
    internal static class Native
    {
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window,uint flags);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr window, out Rect rectangle);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct NativePoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseInput
        { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput
        { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
        { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
        [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public InputUnion Data; }

        internal static void MouseButton(bool down)
        {
            var input = MouseButtonInput(down);
            if (SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input))) != 1)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no aceptó el clic. Ejecuta ambos programas como usuario normal.");
        }
        internal static void JumpKey(bool down)
        {
            // Physical scan code for Space; no direction, inventory or chat key.
            var input = new Input { Type = 1, Data = new InputUnion {
                Keyboard = new KeyboardInput { Scan = 0x39, Flags = down ? 0x0008u : 0x000Au } } };
            if (SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input))) != 1)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no aceptó la tecla Espacio.");
        }
        internal static void PurchaseKey(int key, bool down)
        {
            var input=PurchaseInput(key,down);
            if(SendInput(1,new[]{input},Marshal.SizeOf(typeof(Input)))!=1)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows no aceptó la tecla de compra "+key+". Ejecuta ambos programas como usuario normal.");
        }
        internal static Input MouseButtonInput(bool down)
        { return new Input { Type=0,Data=new InputUnion { Mouse=new MouseInput { Flags=down?0x0002u:0x0004u } } }; }
        // Pure construction is testable without sending input to any application.
        internal static Input PurchaseInput(int key,bool down)
        {
            ushort scan;
            if(key==0x45)scan=0x12;else if(key==0x11)scan=0x1d;else if(key==0x41)scan=0x1e;
            else if(key==0x08)scan=0x0e;else if(key==0x30)scan=0x0b;
            else if(key>=0x31&&key<=0x39)scan=(ushort)(key-0x31+2);else throw new ArgumentOutOfRangeException("key");
            // KEYEVENTF_SCANCODE: same physical input path as the working Space key.
            return new Input {Type=1,Data=new InputUnion {Keyboard=new KeyboardInput {Scan=scan,Flags=down?8u:10u}}};
        }
        internal static void MovePointer(Point point)
        {
            SendPointerInput(PointerInput(point,SystemInformation.VirtualScreen,false));
        }
        internal static void ShopMouseDown(Point point,ShopClickKind kind)
        {
            SendPointerInput(ShopClickInput(point,SystemInformation.VirtualScreen,kind));
        }
        internal static Input ShopClickInput(Point point,Rectangle desktop,ShopClickKind kind)
        {
            if(!Settings.ContainsSafely(desktop,new Rectangle(point,new Size(1,1))))throw new InvalidOperationException("El botón está fuera del escritorio.");
            // Move and settle before every click. Neither half of a numeric
            // double click carries another position update in its button event.
            if(kind==ShopClickKind.Button||kind==ShopClickKind.Quantity)return MouseButtonInput(true);
            throw new ArgumentOutOfRangeException("kind");
        }
        internal static Input RelativeInput(Point delta)
        {
            if((delta.X==0&&delta.Y==0)||Math.Abs((long)delta.X)>64||Math.Abs((long)delta.Y)>64)throw new ArgumentOutOfRangeException("delta");
            return new Input{Type=0,Data=new InputUnion{Mouse=new MouseInput{X=delta.X,Y=delta.Y,Flags=0x2001}}};
        }
        internal static void MoveRelative(Point delta) { SendPointerInput(RelativeInput(delta)); }
        private static void SendPointerInput(Input input)
        {
            if(SendInput(1,new[]{input},Marshal.SizeOf(typeof(Input)))!=1)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows no aceptó la posición y entrada del ratón de compra.");
        }
        internal static Input PointerInput(Point point,Rectangle desktop,bool down)
        {
            if(!Settings.ContainsSafely(desktop,new Rectangle(point,new Size(1,1))))throw new InvalidOperationException("El botón está fuera del escritorio.");
            return new Input {Type=0,Data=new InputUnion {Mouse=new MouseInput {
                X=(int)(((long)point.X-desktop.Left)*65536/desktop.Width+32768/desktop.Width),
                Y=(int)(((long)point.Y-desktop.Top)*65536/desktop.Height+32768/desktop.Height),Flags=down?0xE003u:0xC001u}}};
        }
        internal static Rectangle ClientBounds(IntPtr window)
        {
            Rect rect; var point = new NativePoint();
            if (!GetClientRect(window, out rect) || !ClientToScreen(window, ref point)) return Rectangle.Empty;
            return new Rectangle(point.X, point.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        internal static bool IsRoblox(IntPtr window)
        {
            uint id; GetWindowThreadProcessId(window, out id);
            try
            {
                using (Process process = Process.GetProcessById((int)id))
                    return string.Equals(process.ProcessName, "RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(process.ProcessName, "RobloxPlayer", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        internal static IntPtr RootWindow(IntPtr window)
        { IntPtr root=GetAncestor(window,2);return root==IntPtr.Zero?window:root; }
        internal static IntPtr WindowAt(Point point)
        { return RootWindow(WindowFromPoint(new NativePoint{X=point.X,Y=point.Y})); }
        internal static string WindowDescription(IntPtr window)
        {
            uint id;GetWindowThreadProcessId(window,out id);
            string processName="no disponible";
            try{using(var process=Process.GetProcessById((int)id))processName=process.ProcessName;}catch{}
            return processName+" · ventana 0x"+window.ToInt64().ToString("X");
        }
        internal static bool InStopCorner()
        {
            Point position = Cursor.Position;
            Rectangle screen = Screen.FromPoint(position).Bounds;
            return position.X < screen.Left + 8 && position.Y < screen.Top + 8;
        }
        internal static Bitmap Capture(Rectangle rectangle)
        {
            var image = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(image))
                    graphics.CopyFromScreen(rectangle.Location, Point.Empty, rectangle.Size, CopyPixelOperation.SourceCopy);
                return image;
            }
            catch { image.Dispose(); throw; }
        }
    }

    internal sealed class GameRuntime : IGameRuntime, IShopRuntime, IShopPointerRuntime, IShopVisualRuntime, ICastPointerRuntime, IBaitSelectionRuntime, IDisposable
    {
        private readonly Settings settings;
        private readonly IntPtr target;
        private readonly bool sendClicks;
        private readonly bool requireFishingArea;
        private readonly MouseLease mouse;
        private readonly RelativePointer shopPointer;
        private readonly RelativePointer castPointer;
        private readonly BaitSelectionController baitSelection;
        private readonly BaitPointSelectionController baitPointSelection;
        private Point? permittedBaitPoint;
        private BaitKind? activeBaitKind;
        private BaitMenuReading lastBaitMenu;
        private Bitmap lastBaitMenuFrame;
        private double nextBaitMenu;
        private long baitMenuSequence, publishedBaitSequence, rawBaitSequence = -1, absentBaitSequence = -1;
        private BaitReading publishedBaitReading = new BaitReading();
        private bool baitRowAbsent;
        private bool castPressPending;
        private Point? settledShopPoint;
        private Point shopAimTarget;
        private ShopVisualReading lastShopVisual;
        private double nextShopVisual;
        private long shopVisualSequence;
        private Point? shopMouseDownPoint;
        private ShopClickKind shopMouseDownKind;
        private bool tracingShopPress;
        private double shopPressStarted;
        private volatile string inputStatus="Sin pulsaciones de compra";
        private readonly MouseLease jump;
        private readonly MouseLease shopKey, controlKey;
        private volatile int currentShopKey;
        private readonly WindowsShopReader shopReader;
        private WindowsBaitReader baitReader;
        private readonly System.Threading.Timer watchdog;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private volatile bool closing;
        private volatile string safetyReason = "Parada de protección: no se pudo comprobar la ventana de Roblox.";
        private readonly StatefulTrackingDetector fishingDetector=new StatefulTrackingDetector();
        public Bitmap LastFrame { get; private set; }
        internal bool PendingRelease { get { return mouse.PendingRelease || jump.PendingRelease || shopKey.PendingRelease || controlKey.PendingRelease; } }
        internal string FaultReason { get { return mouse.Fault ?? jump.Fault ?? shopKey.Fault ?? controlKey.Fault; } }
        internal string InputStatus { get { return inputStatus; } }
        internal GameRuntime(Settings settings, IntPtr target, bool sendClicks, bool requireFishingArea=true)
        {
            this.settings = settings; this.target = target; this.sendClicks = sendClicks;
            shopReader=new WindowsShopReader(settings.OcrLanguage);baitReader=new WindowsBaitReader(settings.OcrLanguage);
            this.requireFishingArea=requireFishingArea;
            mouse = new MouseLease(delegate(bool down) {
                if(sendClicks){
                    if(down&&shopMouseDownPoint.HasValue)Native.ShopMouseDown(shopMouseDownPoint.Value,shopMouseDownKind);else Native.MouseButton(down);
                }
                if(down&&shopMouseDownPoint.HasValue){tracingShopPress=true;shopPressStarted=clock.Elapsed.TotalMilliseconds;inputStatus=(sendClicks?"Windows aceptó presionar":"Presión simulada")+" · cursor "+Cursor.Position.X+", "+Cursor.Position.Y+" · esperando soltar";}
                else if(!down&&tracingShopPress){tracingShopPress=false;inputStatus=(sendClicks?"Windows aceptó soltar":"Liberación simulada")+" · duración "+Math.Round(clock.Elapsed.TotalMilliseconds-shopPressStarted)+" ms · esto no confirma la respuesta del juego";}
            },
                delegate { return ForegroundAllowed; }, delegate { return clock.Elapsed.TotalMilliseconds; }, delegate { return safetyReason; });
            jump = new MouseLease(delegate(bool down) { if (sendClicks) Native.JumpKey(down); },
                delegate { return ForegroundAllowed; }, delegate { return clock.Elapsed.TotalMilliseconds; }, delegate { return safetyReason; }, "Espacio");
            shopKey = new MouseLease(delegate(bool down){if(sendClicks)Native.PurchaseKey(currentShopKey,down);TraceKey(currentShopKey,down);},
                delegate{return ForegroundAllowed;},delegate{return clock.Elapsed.TotalMilliseconds;},delegate{return safetyReason;},"la tecla de compra");
            controlKey = new MouseLease(delegate(bool down){if(sendClicks)Native.PurchaseKey(0x11,down);TraceKey(0x11,down);},
                delegate{return ForegroundAllowed;},delegate{return clock.Elapsed.TotalMilliseconds;},delegate{return safetyReason;},"Ctrl");
            shopPointer=new RelativePointer(delegate{return Cursor.Position;},delegate(Point delta){if(sendClicks)Native.MoveRelative(delta);},
                delegate{return ForegroundAllowed;},delegate(Point point){return Settings.ContainsSafely(Native.ClientBounds(target),new Rectangle(point,new Size(1,1)));});
            castPointer=new RelativePointer(delegate{return Cursor.Position;},delegate(Point delta){if(sendClicks)Native.MoveRelative(delta);},
                delegate{return ForegroundAllowed;},delegate(Point point){return Settings.ContainsSafely(Native.ClientBounds(target),new Rectangle(point,new Size(1,1)));});
            baitSelection = new BaitSelectionController(delegate { return IsActive; }, ReadBaitMenu, AimBaitRow,
                TickShopAim, ClickBaitRow, ReleaseBaitInputs, settings.ShopPhaseTimeoutSeconds);
            baitPointSelection = new BaitPointSelectionController(delegate { return IsActive; }, delegate(BaitKind kind) {
                Point point; return BaitPointTargets.TryGet(settings, kind, out point) ? (Point?)point : null;
            }, delegate(BaitKind kind, Point point) { return BaitPointTargets.Authorized(settings, kind, point, Native.ClientBounds(target)); },
                AimBaitRow, TickShopAim, ClickBaitRow, ReleaseBaitInputs, delegate { return PendingRelease; }, settings.ShopPhaseTimeoutSeconds);
            watchdog = new System.Threading.Timer(delegate
            {
                mouse.Watchdog(); jump.Watchdog(); shopKey.Watchdog(); controlKey.Watchdog();
                if (closing && !PendingRelease && watchdog != null) watchdog.Dispose();
            }, null, 50, 50);
        }

        public bool IsActive
        {
            get
            {
                bool active = !closing && mouse.Beat() && jump.Beat() && shopKey.Beat() && controlKey.Beat();
                if (!active) Release();
                return active;
            }
        }
        private bool ForegroundAllowed
        {
            get
            {
                // Only F10 is sampled. No keyboard logging or hooks are used.
                if (closing) { safetyReason = "La aplicación se está cerrando."; return false; }
                if ((Native.GetAsyncKeyState((int)Keys.F10) & 0x8000) != 0)
                { safetyReason = "Detenida con F10."; return false; }
                IntPtr foreground=Native.GetForegroundWindow();
                if (foreground != target)
                { safetyReason = "Detenida: Roblox dejó de estar en primer plano. Activa: "+Native.WindowDescription(foreground)+"; esperada: "+Native.WindowDescription(target); return false; }
                if (Native.InStopCorner())
                { safetyReason = "Detenida: el ratón llegó a la esquina superior izquierda."; return false; }
                Rectangle client = Native.ClientBounds(target);
                bool baitInside = settings.UseManualBait ? (settings.UseBaitPoints ? BaitPointTargets.ConfiguredInside(settings, client) : Settings.ContainsSafely(client, settings.BaitMenuArea)) : (!settings.UsesBaitCounter || Settings.ContainsSafely(client, settings.BaitArea));
                bool inside = ((!requireFishingArea&&settings.Area.IsEmpty)||Settings.ContainsSafely(client, settings.Area)) && baitInside && (!settings.AutoBuyBait || (settings.UseDirectShopFlow?settings.ValidateShopButtons(client)==null:Settings.ContainsSafely(client,settings.ShopArea))) && (!settings.AutoCast ||
                    (settings.CastPointSet && Settings.ContainsSafely(client, new Rectangle(settings.CastPoint, new Size(1, 1)))));
                if (!inside) safetyReason = "Detenida: la zona o el punto de lanzamiento quedó fuera de la ventana de Roblox.";
                return inside;
            }
        }
        private void Guard()
        { if (!IsActive) throw new InvalidOperationException("Roblox perdió el foco o la zona quedó fuera de su ventana."); }
        public void MoveToCastPoint()
        {
            Guard();
            Release();if(PendingRelease)throw new InvalidOperationException("Hay una entrada pendiente de liberación antes de volver al agua.");
            if(sendClicks)castPointer.Start(settings.CastPoint,clock.Elapsed.TotalMilliseconds);castPressPending=true;
            inputStatus="Moviendo con desplazamientos relativos al punto del agua";
        }
        public bool TickCastAim(double now)
        {
            Guard();
            if(!castPressPending)throw new InvalidOperationException("El movimiento al agua fue cancelado.");
            if(!sendClicks)return true;
            castPointer.Tick(clock.Elapsed.TotalMilliseconds);
            inputStatus=castPointer.Status;
            return castPointer.Ready;
        }
        public void SetHeld(bool value)
        {
            if (!value) { mouse.Release(); return; }
            Guard();
            if(castPressPending&&sendClicks)
            {
                Point actual=Cursor.Position;
                if(!castPointer.Ready||Math.Abs((long)actual.X-settings.CastPoint.X)>3||Math.Abs((long)actual.Y-settings.CastPoint.Y)>3)
                    throw new InvalidOperationException("El puntero no quedó sobre el punto del agua. Lanzamiento cancelado.");
                if(Native.WindowAt(actual)!=Native.RootWindow(target))throw new InvalidOperationException("Otra ventana cubre el punto del agua. Lanzamiento cancelado.");
            }
            jump.Release();shopKey.Release();controlKey.Release();
            if (jump.PendingRelease||shopKey.PendingRelease||controlKey.PendingRelease) throw new InvalidOperationException("Una tecla sigue pendiente de liberación.");
            mouse.SetHeld(true);
            castPressPending=false;
        }
        public void SetJumpHeld(bool value)
        {
            if (!value) { jump.Release(); return; }
            Guard();
            if (!settings.IdleJumpEnabled) throw new InvalidOperationException("Los saltos de espera están desactivados.");
            mouse.Release();shopKey.Release();controlKey.Release();
            if (mouse.PendingRelease||shopKey.PendingRelease||controlKey.PendingRelease) throw new InvalidOperationException("Una entrada sigue pendiente de liberación.");
            jump.Pulse(100);
        }
        public void Release()
        {
            if (baitSelection != null) baitSelection.Cancel();
            if (baitPointSelection != null) baitPointSelection.Cancel();
            if(shopPointer!=null)shopPointer.Cancel();settledShopPoint=null;
            if(castPointer!=null)castPointer.Cancel();castPressPending=false;
            ReleaseInputs();
        }
        private void TraceKey(int key,bool down)
        {
            string name=key==0x11?"Ctrl":key==0x08?"Retroceso":((char)key).ToString();
            inputStatus=(sendClicks?"Windows aceptó tecla ":"Tecla simulada ")+name+(down?" abajo":" arriba")+" · respuesta del juego sin verificar";
        }
        private void ReleaseInputs()
        {
            mouse.Release(); jump.Release(); shopKey.Release(); controlKey.Release();
        }
        public ShopReading ReadShop(double now)
        {
            Guard();
            if(!settings.AutoBuyBait)return new ShopReading();
            if(shopReader.Due(now))shopReader.Submit(Native.Capture(settings.ShopArea),settings.ShopArea.Location,now);
            return shopReader.Latest;
        }
        public void ShopAim(Point point)
        {
            Guard();if(!ShopPointAllowed(point))throw new InvalidOperationException("Clic de compra fuera de los botones autorizados.");
            Release();if(PendingRelease)throw new InvalidOperationException("Hay una entrada pendiente de liberación.");
            if(settings.UseDirectShopFlow)
            {shopAimTarget=point;shopPointer.Start(point,clock.Elapsed.TotalMilliseconds);inputStatus="Moviendo con desplazamientos relativos hacia "+point.X+", "+point.Y;}
            else if(sendClicks)Native.MovePointer(point);
        }
        public bool TickShopAim(double now)
        {
            Guard();
            if(!sendClicks){settledShopPoint=shopAimTarget;return true;}
            shopPointer.Tick(clock.Elapsed.TotalMilliseconds);
            inputStatus=shopPointer.Status;
            if(shopPointer.Ready)settledShopPoint=shopAimTarget;
            return shopPointer.Ready;
        }
        public void ShopClick(Point point,ShopClickKind kind)
        {
            Guard();if(!ShopPointAllowed(point))throw new InvalidOperationException("Clic de compra fuera de los botones autorizados.");
            PressMarkedPoint(point,kind,settings.UseDirectShopFlow);
        }
        private void PressMarkedPoint(Point point, ShopClickKind kind, bool requireSettled)
        {
            if(requireSettled&&settledShopPoint!=point)throw new InvalidOperationException("El movimiento al botón no terminó; clic cancelado.");
            Point actual=Cursor.Position;
            if(sendClicks&&(Math.Abs(actual.X-point.X)>3||Math.Abs(actual.Y-point.Y)>3))throw new InvalidOperationException("El puntero no llegó al botón o se movió. Clic cancelado; suelta el ratón durante la prueba.");
            IntPtr below=Native.WindowAt(actual);
            if(sendClicks&&below!=Native.RootWindow(target))throw new InvalidOperationException("El clic quedó sobre otra ventana: "+Native.WindowDescription(below)+". Despeja el diálogo de Roblox y repite la prueba.");
            ReleaseInputs();if(PendingRelease)throw new InvalidOperationException("Hay una entrada pendiente de liberación.");
            shopMouseDownPoint=point;shopMouseDownKind=kind;
            try{mouse.Pulse(180);}finally{shopMouseDownPoint=null;}
        }
        private bool ShopPointAllowed(Point point)
        {
            return MarkedShopPointAllowed(settings, point);
        }
        internal static bool MarkedShopPointAllowed(Settings options, Point point)
        {
            if (options.UseDirectShopFlow)
                return options.ShopButtonsSet && ((options.AutoBuyBait && point == options.ShopLeftPoint)
                    || point == options.ShopMiddlePoint || point == options.ShopRightPoint);
            return options.AutoBuyBait && Settings.ContainsSafely(options.ShopArea, new Rectangle(point, new Size(1, 1)));
        }
        public ShopVisualReading ReadShopVisual(double now)
        {
            Guard();if(lastShopVisual!=null&&now<nextShopVisual)return lastShopVisual;
            Rectangle client=Native.ClientBounds(target);
            var points=new[]{settings.ShopLeftPoint,settings.ShopMiddlePoint,settings.ShopRightPoint};
            var regions=new Rectangle[3];
            for(int i=0;i<3;i++)
            {
                regions[i]=ShopVisual.Region(points[i]);
                if(!Settings.ContainsSafely(client,regions[i]))throw new InvalidOperationException("Marca el centro de los botones, con margen dentro de Roblox.");
            }
            using(Bitmap left=Native.Capture(regions[0]))
            using(Bitmap middle=Native.Capture(regions[1]))
            using(Bitmap right=Native.Capture(regions[2]))
            {lastShopVisual=ShopVisual.Analyze(left,middle,right);}
            lastShopVisual.Sequence=++shopVisualSequence;lastShopVisual.SampledAt=now;nextShopVisual=now+150;
            return lastShopVisual;
        }
        public void ShopKey(int key,bool held)
        {
            if(!held){if(key==0x11)controlKey.Release();else shopKey.Release();return;}
            Guard();if(!settings.AutoBuyBait)throw new InvalidOperationException("La compra automática está desactivada.");
            if(key!=0x45&&key!=0x11&&key!=0x41&&key!=0x08&&(key<0x30||key>0x39))throw new ArgumentOutOfRangeException("key");
            mouse.Release();jump.Release();if(mouse.PendingRelease||jump.PendingRelease)throw new InvalidOperationException("Hay una entrada pendiente de liberación.");
            if(key==0x11){controlKey.SetHeld(true);return;}
            shopKey.Release();if(shopKey.PendingRelease)throw new InvalidOperationException("No se pudo liberar la tecla anterior.");
            currentShopKey=key;
            // E can require a sustained interaction. The state machine releases it
            // at the configured deadline; the 500 ms heartbeat guard still applies.
            if(key==0x45)shopKey.SetHeld(true);else shopKey.Pulse(100);
        }
        public BaitReading ReadBait(double now)
        {
            Guard();
            if (!settings.UsesBaitCounter) return new BaitReading();
            if (settings.UseManualBait) return ReadActiveBaitCounter(now);
            if (baitReader.Due(now)) baitReader.Submit(Native.Capture(settings.BaitArea), now);
            return baitReader.Latest;
        }
        public void BeginBaitSelection(BaitKind kind, double now)
        {
            Guard();
            if (!settings.UseManualBait) throw new InvalidOperationException("La selección por tipo de cebo está desactivada.");
            if (!activeBaitKind.HasValue || activeBaitKind.Value != kind)
            {
                activeBaitKind = kind; ResetBaitReader();
                publishedBaitReading = new BaitReading { Sequence = ++publishedBaitSequence, SampledAt = now, Detail = "Esperando el contador del nuevo tipo de cebo" };
            }
            // A previous menu snapshot must not authorize a new selection.
            nextBaitMenu = 0;
            if (settings.UseBaitPoints) { baitSelection.Cancel(); baitPointSelection.Begin(kind, now); }
            else { baitPointSelection.Cancel(); baitSelection.Begin(kind, now); }
        }
        public BaitSelectionResult TickBaitSelection(double now) { return settings.UseBaitPoints ? baitPointSelection.Tick(now) : baitSelection.Tick(now); }
        private void ReleaseBaitInputs()
        {
            permittedBaitPoint = null;
            if (shopPointer != null) shopPointer.Cancel(); settledShopPoint = null;
            ReleaseInputs();
        }
        private void AimBaitRow(Point point)
        {
            Guard();
            if (!BaitRowPointAllowed(point))
                throw new InvalidOperationException("La fila de cebo está fuera del menú marcado.");
            ReleaseBaitInputs();
            if (PendingRelease) throw new InvalidOperationException("Hay una entrada pendiente de liberación.");
            permittedBaitPoint = shopAimTarget = point;
            shopPointer.Start(point, clock.Elapsed.TotalMilliseconds);
            inputStatus = "Moviendo el puntero a la fila de cebo";
        }
        private void ClickBaitRow(Point point)
        {
            Guard();
            if (permittedBaitPoint != point || !BaitRowPointAllowed(point))
                throw new InvalidOperationException("Clic de cebo fuera de la fila autorizada.");
            PressMarkedPoint(point, ShopClickKind.Button, true);
        }
        private bool BaitRowPointAllowed(Point point)
        {
            if (!settings.UseManualBait) return false;
            if (settings.UseBaitPoints)
                return activeBaitKind.HasValue && BaitPointTargets.Authorized(settings, activeBaitKind.Value, point, Native.ClientBounds(target));
            return Settings.ContainsSafely(settings.BaitMenuArea, new Rectangle(point, new Size(1, 1)));
        }
        private BaitMenuReading ReadBaitMenu(double now)
        {
            Guard();
            if (lastBaitMenu != null && now < nextBaitMenu) return lastBaitMenu;
            Rectangle area = settings.BaitMenuArea;
            if (!settings.UseManualBait || area.Width < 60 || area.Height < 16 || !Settings.ContainsSafely(Native.ClientBounds(target), area))
                throw new InvalidOperationException("Selecciona el menú completo de cebos dentro de Roblox.");
            Bitmap frame = Native.Capture(area);
            if (lastBaitMenuFrame != null) lastBaitMenuFrame.Dispose();
            lastBaitMenuFrame = frame;
            lastBaitMenu = BaitMenuVisual.Analyze(frame, area.Location);
            lastBaitMenu.Sequence = ++baitMenuSequence; lastBaitMenu.SampledAt = now; nextBaitMenu = now + 200;
            return lastBaitMenu;
        }
        private void ResetBaitReader()
        {
            // The old worker may finish later, but its disposed reader can no
            // longer publish a quantity belonging to a different bait type.
            baitReader.Dispose(); baitReader = new WindowsBaitReader(settings.OcrLanguage);
            rawBaitSequence = -1; absentBaitSequence = -1; baitRowAbsent = false;
        }
        private BaitReading ReadActiveBaitCounter(double now)
        {
            if (!activeBaitKind.HasValue) return publishedBaitReading;
            // OCR is optional supporting evidence in point mode. An unset or
            // outdated OCR crop cannot block explicitly marked bait selection.
            if (settings.UseBaitPoints && (settings.BaitMenuArea.Width < 60 || settings.BaitMenuArea.Height < 16 ||
                !Settings.ContainsSafely(Native.ClientBounds(target), settings.BaitMenuArea)))
                return new BaitReading { Sequence = ++publishedBaitSequence, SampledAt = now,
                    Detail = "OCR de apoyo sin zona de menú · usando inventario manual" };
            BaitMenuReading menu = ReadBaitMenu(now); BaitMenuRow row;
            if (!menu.TryGetRow(activeBaitKind.Value, out row))
            {
                if (!baitRowAbsent) { ResetBaitReader(); baitRowAbsent = true; }
                if (absentBaitSequence != menu.Sequence)
                {
                    absentBaitSequence = menu.Sequence;
                    publishedBaitReading = new BaitReading { Sequence = ++publishedBaitSequence, SampledAt = menu.SampledAt,
                        // Ambiguous/hidden menus are unknown, never a numeric zero.
                        VisuallyAbsent = menu.Rows.Count > 0 && menu.Rows.TrueForAll(delegate(BaitMenuRow candidate) { return candidate.Kind != activeBaitKind.Value; }),
                        Detail = "No se distingue el contador de cebo " + BaitSelectionController.Name(activeBaitKind.Value) };
                }
                return publishedBaitReading;
            }
            baitRowAbsent = false;
            if (baitReader.Due(now))
            {
                Rectangle local = row.CounterBounds; local.Offset(-settings.BaitMenuArea.X, -settings.BaitMenuArea.Y);
                if (!Settings.ContainsSafely(new Rectangle(Point.Empty, lastBaitMenuFrame.Size), local))
                    throw new InvalidOperationException("El contador quedó fuera del menú de cebos.");
                baitReader.Submit(lastBaitMenuFrame.Clone(local, PixelFormat.Format32bppArgb), menu.SampledAt);
            }
            BaitReading raw = baitReader.Latest;
            if (raw.Sequence > 0 && raw.Sequence != rawBaitSequence)
            {
                rawBaitSequence = raw.Sequence;
                publishedBaitReading = new BaitReading { Sequence = ++publishedBaitSequence, SampledAt = raw.SampledAt,
                    Count = raw.Count, VisuallyAbsent = raw.VisuallyAbsent, Detail = raw.Detail };
            }
            return publishedBaitReading;
        }
        public Observation Observe()
        {
            Guard();
            if(!requireFishingArea&&settings.Area.IsEmpty)return new Observation{Detail="Prueba sin zona de pesca configurada"};
            Bitmap frame = Native.Capture(settings.Area);
            if (LastFrame != null) LastFrame.Dispose();
            LastFrame = frame;
            return fishingDetector.Analyze(frame, settings);
        }
        public void Dispose()
        {
            baitSelection.Cancel();
            baitPointSelection.Cancel();
            shopPointer.Cancel();settledShopPoint=null;
            castPointer.Cancel();castPressPending=false;
            closing = true; mouse.Stop(); jump.Stop(); shopKey.Stop(); controlKey.Stop(); baitReader.Dispose(); shopReader.Dispose();
            if (!PendingRelease) watchdog.Dispose();
            // If Windows rejected button-up, the timer retains this object and retries
            // until it is accepted. The UI blocks a new run while release is pending.
            if (LastFrame != null) LastFrame.Dispose(); LastFrame = null;
            if (lastBaitMenuFrame != null) lastBaitMenuFrame.Dispose(); lastBaitMenuFrame = null;
        }
    }
}
