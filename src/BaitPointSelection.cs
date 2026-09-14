using System;
using System.Drawing;

namespace SomeFishingGPO
{
    internal static class BaitPointTargets
    {
        internal static bool TryGet(Settings settings, BaitKind kind, out Point point)
        {
            point = Point.Empty;
            if (!settings.UseManualBait || !settings.UseBaitPoints) return false;
            int bit;
            switch (kind)
            {
                case BaitKind.Common: bit = 1; point = settings.BaitCommonPoint; break;
                case BaitKind.Rare: bit = 2; point = settings.BaitRarePoint; break;
                case BaitKind.Legendary: bit = 4; point = settings.BaitLegendaryPoint; break;
                default: return false;
            }
            return (settings.BaitPointsSet & bit) != 0;
        }
        internal static bool Authorized(Settings settings, BaitKind kind, Point point, Rectangle client)
        {
            Point marked;
            return TryGet(settings, kind, out marked) && marked == point &&
                Settings.ContainsSafely(client, new Rectangle(point, new Size(1, 1)));
        }
        internal static bool ConfiguredInside(Settings settings, Rectangle client)
        {
            if (!settings.UseManualBait || !settings.UseBaitPoints || (settings.BaitPointsSet & ~7) != 0) return false;
            int reserve = settings.KeepOneBait ? 1 : 0;
            foreach (BaitKind kind in new[] { BaitKind.Common, BaitKind.Rare, BaitKind.Legendary })
            {
                int count = kind == BaitKind.Common ? settings.ManualCommonBait : kind == BaitKind.Rare ? settings.ManualRareBait : settings.ManualLegendaryBait;
                if (count <= reserve && !(kind == BaitKind.Common && settings.AutoBuyBait)) continue;
                Point point;
                if (!TryGet(settings, kind, out point) || !Authorized(settings, kind, point, client)) return false;
            }
            return true;
        }
    }

    // This mode follows explicit marked points. It never reads pixels or claims
    // that a game selection was observed. Success means one input was released.
    internal sealed class BaitPointSelectionController
    {
        private readonly Func<bool> allowed, pendingRelease;
        private readonly Func<BaitKind, Point?> markedPoint;
        private readonly Func<BaitKind, Point, bool> authorized;
        private readonly Action<Point> aim, click;
        private readonly Func<double, bool> tickAim;
        private readonly Action release;
        private readonly double timeout;
        private BaitSelectionResult result = new BaitSelectionResult { Completed = true };
        private BaitKind kind;
        private Point target;
        private double startedAt, lastNow, arrivedAt, clickedAt;
        private int phase;
        internal BaitPointSelectionController(Func<bool> allowed, Func<BaitKind, Point?> markedPoint,
            Func<BaitKind, Point, bool> authorized, Action<Point> aim, Func<double, bool> tickAim,
            Action<Point> click, Action release, Func<bool> pendingRelease, int timeoutSeconds)
        {
            if (allowed == null || markedPoint == null || authorized == null || aim == null ||
                tickAim == null || click == null || release == null || pendingRelease == null)
                throw new ArgumentNullException("runtime");
            this.allowed = allowed; this.markedPoint = markedPoint; this.authorized = authorized;
            this.aim = aim; this.tickAim = tickAim; this.click = click; this.release = release;
            this.pendingRelease = pendingRelease;
            timeout = Math.Max(3, Math.Min(60, timeoutSeconds)) * 1000;
        }
        internal void Begin(BaitKind value, double now)
        {
            Cancel();
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0) throw new ArgumentOutOfRangeException("now");
            kind = value; startedAt = lastNow = now; phase = 0;
            result = new BaitSelectionResult { Status = "Preparando clic en el cebo " + BaitSelectionController.Name(kind) };
            if (!allowed()) { Finish(false, "Roblox perdió el foco antes de seleccionar el cebo."); return; }
            Point? point = markedPoint(kind);
            if (!point.HasValue || !authorized(kind, point.Value))
            { Finish(false, "Marca el punto del cebo " + BaitSelectionController.Name(kind) + " dentro de Roblox."); return; }
            target = point.Value;
        }
        internal BaitSelectionResult Tick(double now)
        {
            if (result.Completed) return result;
            try
            {
                if (double.IsNaN(now) || double.IsInfinity(now) || now < lastNow)
                    return Finish(false, "El reloj cambió durante la selección del cebo.");
                lastNow = now;
                if (!allowed()) return Finish(false, "Roblox perdió el foco durante la selección del cebo.");
                if (!authorized(kind, target)) return Finish(false, "El punto de cebo dejó de estar autorizado dentro de Roblox.");
                if (now - startedAt >= timeout) return Finish(false, "No se pudo terminar el clic de cebo a tiempo.");
                if (phase == 0)
                {
                    if (pendingRelease()) { result.Status = "Esperando soltar entradas antes de seleccionar cebo"; return result; }
                    aim(target); phase = 1;
                    result.Status = "Moviendo el puntero al cebo " + BaitSelectionController.Name(kind); return result;
                }
                if (phase == 1)
                {
                    if (tickAim(now)) { phase = 2; arrivedAt = now; result.Status = "Puntero en el cebo · esperando antes del clic"; }
                    return result;
                }
                if (phase == 2)
                {
                    if (now - arrivedAt < 350) return result;
                    if (pendingRelease()) return Finish(false, "Hay una entrada pendiente de liberación antes del clic de cebo.");
                    // The guarded runtime rechecks exact position, window and
                    // foreground immediately before the single held click.
                    click(target); phase = 3; clickedAt = now;
                    result.Status = "Clic enviado al cebo · esperando soltar"; return result;
                }
                if (phase == 3)
                {
                    if (now - clickedAt < 250) return result;
                    release(); phase = 4;
                }
                if (pendingRelease()) { result.Status = "Esperando liberar el clic de cebo"; return result; }
                return Finish(true, "Clic enviado al cebo " + BaitSelectionController.Name(kind) + " · selección sin verificar");
            }
            catch (Exception error) { return Finish(false, "Falló la selección del cebo: " + error.Message); }
        }
        internal void Cancel()
        {
            // Mark terminal before releasing: a later tick must never send input.
            if (!result.Completed) result = new BaitSelectionResult { Completed = true, Status = "Selección de cebo cancelada" };
            phase = 0; release();
        }
        private BaitSelectionResult Finish(bool success, string status)
        {
            result = new BaitSelectionResult { Completed = true, Succeeded = success, Status = status };
            release(); return result;
        }
    }
}
