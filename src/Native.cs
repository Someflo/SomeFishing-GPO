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
            var input = new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = down ? 0x0002u : 0x0004u } } };
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
            Rectangle desktop=SystemInformation.VirtualScreen;
            if(!Settings.ContainsSafely(desktop,new Rectangle(point,new Size(1,1))))throw new InvalidOperationException("El botón está fuera del escritorio.");
            var input=new Input {Type=0,Data=new InputUnion {Mouse=new MouseInput {
                X=(int)(((long)point.X-desktop.Left)*65536/desktop.Width+32768/desktop.Width),
                Y=(int)(((long)point.Y-desktop.Top)*65536/desktop.Height+32768/desktop.Height),Flags=0xC001u}}};
            if(SendInput(1,new[]{input},Marshal.SizeOf(typeof(Input)))!=1)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows no aceptó mover el puntero al botón.");
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

    internal sealed class GameRuntime : IGameRuntime, IShopRuntime, IDisposable
    {
        private readonly Settings settings;
        private readonly IntPtr target;
        private readonly bool sendClicks;
        private readonly bool requireFishingArea;
        private readonly MouseLease mouse;
        private readonly MouseLease jump;
        private readonly MouseLease shopKey, controlKey;
        private volatile int currentShopKey;
        private readonly WindowsShopReader shopReader = new WindowsShopReader();
        private readonly WindowsBaitReader baitReader = new WindowsBaitReader();
        private readonly System.Threading.Timer watchdog;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private volatile bool closing;
        private volatile string safetyReason = "Parada de protección: no se pudo comprobar la ventana de Roblox.";
        public Bitmap LastFrame { get; private set; }
        internal bool PendingRelease { get { return mouse.PendingRelease || jump.PendingRelease || shopKey.PendingRelease || controlKey.PendingRelease; } }
        internal string FaultReason { get { return mouse.Fault ?? jump.Fault ?? shopKey.Fault ?? controlKey.Fault; } }
        internal GameRuntime(Settings settings, IntPtr target, bool sendClicks, bool requireFishingArea=true)
        {
            this.settings = settings; this.target = target; this.sendClicks = sendClicks;
            this.requireFishingArea=requireFishingArea;
            mouse = new MouseLease(delegate(bool down) { if (sendClicks) Native.MouseButton(down); },
                delegate { return ForegroundAllowed; }, delegate { return clock.Elapsed.TotalMilliseconds; }, delegate { return safetyReason; });
            jump = new MouseLease(delegate(bool down) { if (sendClicks) Native.JumpKey(down); },
                delegate { return ForegroundAllowed; }, delegate { return clock.Elapsed.TotalMilliseconds; }, delegate { return safetyReason; }, "Espacio");
            shopKey = new MouseLease(delegate(bool down){if(sendClicks)Native.PurchaseKey(currentShopKey,down);},
                delegate{return ForegroundAllowed;},delegate{return clock.Elapsed.TotalMilliseconds;},delegate{return safetyReason;},"la tecla de compra");
            controlKey = new MouseLease(delegate(bool down){if(sendClicks)Native.PurchaseKey(0x11,down);},
                delegate{return ForegroundAllowed;},delegate{return clock.Elapsed.TotalMilliseconds;},delegate{return safetyReason;},"Ctrl");
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
                if (Native.GetForegroundWindow() != target)
                { safetyReason = "Detenida: Roblox dejó de estar en primer plano."; return false; }
                if (Native.InStopCorner())
                { safetyReason = "Detenida: el ratón llegó a la esquina superior izquierda."; return false; }
                Rectangle client = Native.ClientBounds(target);
                bool inside = ((!requireFishingArea&&settings.Area.IsEmpty)||Settings.ContainsSafely(client, settings.Area)) && (!settings.UsesBaitCounter || Settings.ContainsSafely(client, settings.BaitArea)) && (!settings.AutoBuyBait || Settings.ContainsSafely(client,settings.ShopArea)) && (!settings.AutoCast ||
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
            if (sendClicks && !Native.SetCursorPos(settings.CastPoint.X, settings.CastPoint.Y))
                throw new Win32Exception("No se pudo mover el ratón al punto de lanzamiento.");
        }
        public void SetHeld(bool value)
        {
            if (!value) { mouse.Release(); return; }
            Guard();
            jump.Release();shopKey.Release();controlKey.Release();
            if (jump.PendingRelease||shopKey.PendingRelease||controlKey.PendingRelease) throw new InvalidOperationException("Una tecla sigue pendiente de liberación.");
            mouse.SetHeld(true);
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
            Guard();if(!settings.AutoBuyBait||!Settings.ContainsSafely(settings.ShopArea,new Rectangle(point,new Size(1,1))))throw new InvalidOperationException("Clic de compra fuera de la zona autorizada.");
            Release();if(PendingRelease)throw new InvalidOperationException("Hay una entrada pendiente de liberación.");
            if(sendClicks)Native.MovePointer(point);
        }
        public void ShopClick(Point point)
        {
            Guard();if(!settings.AutoBuyBait||!Settings.ContainsSafely(settings.ShopArea,new Rectangle(point,new Size(1,1))))throw new InvalidOperationException("Clic de compra fuera de la zona autorizada.");
            Point actual=Cursor.Position;
            if(sendClicks&&(Math.Abs(actual.X-point.X)>3||Math.Abs(actual.Y-point.Y)>3))throw new InvalidOperationException("El puntero no llegó al botón o se movió. Clic cancelado; suelta el ratón durante la prueba.");
            Release();if(PendingRelease)throw new InvalidOperationException("Hay una entrada pendiente de liberación.");
            mouse.Pulse(180);
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
            if (baitReader.Due(now)) baitReader.Submit(Native.Capture(settings.BaitArea), now);
            return baitReader.Latest;
        }
        public Observation Observe()
        {
            Guard();
            if(!requireFishingArea&&settings.Area.IsEmpty)return new Observation{Detail="Prueba sin zona de pesca configurada"};
            Bitmap frame = Native.Capture(settings.Area);
            if (LastFrame != null) LastFrame.Dispose();
            LastFrame = frame;
            return Detector.Analyze(frame, settings);
        }
        public void Dispose()
        {
            closing = true; mouse.Stop(); jump.Stop(); shopKey.Stop(); controlKey.Stop(); baitReader.Dispose(); shopReader.Dispose();
            if (!PendingRelease) watchdog.Dispose();
            // If Windows rejected button-up, the timer retains this object and retries
            // until it is accepted. The UI blocks a new run while release is pending.
            if (LastFrame != null) LastFrame.Dispose(); LastFrame = null;
        }
    }
}
