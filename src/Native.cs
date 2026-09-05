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

    internal sealed class GameRuntime : IGameRuntime, IDisposable
    {
        private readonly Settings settings;
        private readonly IntPtr target;
        private readonly bool sendClicks;
        private readonly MouseLease mouse;
        private readonly MouseLease jump;
        private readonly WindowsBaitReader baitReader = new WindowsBaitReader();
        private readonly System.Threading.Timer watchdog;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private volatile bool closing;
        private volatile string safetyReason = "Parada de protección: no se pudo comprobar la ventana de Roblox.";
        public Bitmap LastFrame { get; private set; }
        internal bool PendingRelease { get { return mouse.PendingRelease || jump.PendingRelease; } }
        internal string FaultReason { get { return mouse.Fault ?? jump.Fault; } }
        internal GameRuntime(Settings settings, IntPtr target, bool sendClicks)
        {
            this.settings = settings; this.target = target; this.sendClicks = sendClicks;
            mouse = new MouseLease(delegate(bool down) { if (sendClicks) Native.MouseButton(down); },
                delegate { return ForegroundAllowed; }, delegate { return clock.Elapsed.TotalMilliseconds; }, delegate { return safetyReason; });
            jump = new MouseLease(delegate(bool down) { if (sendClicks) Native.JumpKey(down); },
                delegate { return ForegroundAllowed; }, delegate { return clock.Elapsed.TotalMilliseconds; }, delegate { return safetyReason; }, "Espacio");
            watchdog = new System.Threading.Timer(delegate
            {
                mouse.Watchdog(); jump.Watchdog();
                if (closing && !PendingRelease && watchdog != null) watchdog.Dispose();
            }, null, 50, 50);
        }

        public bool IsActive
        {
            get
            {
                bool active = !closing && mouse.Beat() && jump.Beat();
                if (!active) { mouse.Release(); jump.Release(); }
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
                bool inside = Settings.ContainsSafely(client, settings.Area) && (!settings.MonitorBait || Settings.ContainsSafely(client, settings.BaitArea)) && (!settings.AutoCast ||
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
            jump.Release();
            if (jump.PendingRelease) throw new InvalidOperationException("Espacio sigue pendiente de liberación.");
            mouse.SetHeld(true);
        }
        public void SetJumpHeld(bool value)
        {
            if (!value) { jump.Release(); return; }
            Guard();
            if (!settings.IdleJumpEnabled) throw new InvalidOperationException("Los saltos de espera están desactivados.");
            mouse.Release();
            if (mouse.PendingRelease) throw new InvalidOperationException("El clic sigue pendiente de liberación.");
            jump.Pulse(100);
        }
        public void Release()
        {
            mouse.Release(); jump.Release();
        }
        public BaitReading ReadBait(double now)
        {
            Guard();
            if (!settings.MonitorBait) return new BaitReading();
            if (baitReader.Due(now)) baitReader.Submit(Native.Capture(settings.BaitArea), now);
            return baitReader.Latest;
        }
        public Observation Observe()
        {
            Guard();
            Bitmap frame = Native.Capture(settings.Area);
            if (LastFrame != null) LastFrame.Dispose();
            LastFrame = frame;
            return Detector.Analyze(frame, settings);
        }
        public void Dispose()
        {
            closing = true; mouse.Stop(); jump.Stop(); baitReader.Dispose();
            if (!PendingRelease) watchdog.Dispose();
            // If Windows rejected button-up, the timer retains this object and retries
            // until it is accepted. The UI blocks a new run while release is pending.
            if (LastFrame != null) LastFrame.Dispose(); LastFrame = null;
        }
    }
}
