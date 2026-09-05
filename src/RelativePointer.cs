using System;
using System.Drawing;

namespace SomeFishingGPO
{
    // Feedback-controlled relative movement. This class has no native input or
    // cursor-setting API: the caller supplies position, movement and focus guards.
    public sealed class RelativePointer
    {
        private readonly Func<Point> read;
        private readonly Action<Point> moveRelative;
        private readonly Func<bool> allowed;
        private readonly Func<Point, bool> inside;
        private Point target, waypoint, before, sent;
        private double startedAt, lastTick, nextMove, stableSince;
        private double gainX = 1, gainY = 1;
        private bool nudging, feedbackPending;
        public bool Ready { get; private set; }
        public bool Running { get; private set; }
        public string Status { get; private set; }

        public RelativePointer(Func<Point> read, Action<Point> moveRelative, Func<bool> allowed, Func<Point, bool> inside)
        {
            if (read == null) throw new ArgumentNullException("read");
            if (moveRelative == null) throw new ArgumentNullException("moveRelative");
            if (allowed == null) throw new ArgumentNullException("allowed");
            if (inside == null) throw new ArgumentNullException("inside");
            this.read = read; this.moveRelative = moveRelative; this.allowed = allowed; this.inside = inside;
            Status = "Puntero preparado";
        }
        public void Start(Point destination, double now)
        {
            Cancel();
            try
            {
                if (!ValidTime(now) || now < 0) throw new InvalidOperationException("Tiempo del puntero no válido.");
                if (!allowed()) throw new InvalidOperationException("El juego perdió el foco.");
                if (!inside(destination)) throw new InvalidOperationException("El destino del puntero está fuera del juego.");
                Point current = read();
                if (!inside(current)) throw new InvalidOperationException("El puntero está fuera del juego.");
                target = destination; waypoint = destination;
                startedAt = lastTick = nextMove = now; stableSince = -1;
                gainX = gainY = 1; feedbackPending = false;
                nudging = Near(current, destination);
                if (nudging) waypoint = NudgePoint(current);
                Running = true; Ready = false;
                Status = nudging ? "Actualizando la posición del puntero" : "Moviendo el puntero al botón";
            }
            catch (Exception error) { throw Failed(error); }
        }
        public void Tick(double now)
        {
            if (!Running) return;
            try
            {
                if (!ValidTime(now) || now < lastTick) throw new InvalidOperationException("El reloj del puntero cambió.");
                lastTick = now;
                if (!allowed()) throw new InvalidOperationException("El juego perdió el foco.");
                if (now - startedAt >= 12000) throw new InvalidOperationException("No se pudo colocar el puntero en 12 segundos.");
                Point current = read();
                if (!inside(current)) throw new InvalidOperationException("El puntero salió del juego.");
                if (feedbackPending)
                {
                    if (now < nextMove) return;
                    gainX = Estimate(gainX, (long)current.X - before.X, sent.X);
                    gainY = Estimate(gainY, (long)current.Y - before.Y, sent.Y);
                    feedbackPending = false;
                }
                if (nudging && Near(current, waypoint))
                {
                    nudging = false; waypoint = target;
                    Status = "Volviendo al botón";
                }
                if (!nudging && Near(current, target))
                {
                    if (stableSince < 0) stableSince = now;
                    if (now - stableSince >= 100)
                    {
                        Ready = true; Running = false; Status = "Puntero estable sobre el botón";
                    }
                    return;
                }
                stableSince = -1;
                if (now < nextMove) return;
                int dx = Delta((long)waypoint.X - current.X, gainX);
                int dy = Delta((long)waypoint.Y - current.Y, gainY);
                while ((dx != 0 || dy != 0) && !PredictedInside(current, dx, dy))
                {
                    dx /= 2; dy /= 2;
                }
                if (dx == 0 && dy == 0) throw new InvalidOperationException("No hay un movimiento relativo válido dentro del juego.");
                before = current; sent = new Point(dx, dy);
                // Bookkeeping is set first: even if delivery fails, no retry or
                // second event is possible in this tick.
                nextMove = now + 50; feedbackPending = true;
                moveRelative(sent);
            }
            catch (Exception error) { throw Failed(error); }
        }
        public void Cancel()
        {
            Running = false; Ready = false; feedbackPending = false; nudging = false;
            Status = "Movimiento cancelado";
        }
        private InvalidOperationException Failed(Exception error)
        {
            Running = false; Ready = false; feedbackPending = false; nudging = false;
            Status = error.Message;
            return error as InvalidOperationException ?? new InvalidOperationException("Falló el movimiento del puntero: " + error.Message, error);
        }
        private Point NudgePoint(Point current)
        {
            foreach (Point offset in new[] { new Point(6, 0), new Point(-6, 0), new Point(0, 6), new Point(0, -6) })
            {
                long x = (long)current.X + offset.X, y = (long)current.Y + offset.Y;
                if (!Representable(x, y)) continue;
                var candidate = new Point((int)x, (int)y);
                if (inside(candidate) && !Near(candidate, target)) return candidate;
            }
            throw new InvalidOperationException("No hay espacio para actualizar el puntero junto al botón.");
        }
        private bool PredictedInside(Point current, int dx, int dy)
        {
            long x = (long)current.X + (long)Math.Round(dx * gainX);
            long y = (long)current.Y + (long)Math.Round(dy * gainY);
            return Representable(x, y) && inside(new Point((int)x, (int)y));
        }
        private static bool Representable(long x, long y)
        { return x >= int.MinValue && x <= int.MaxValue && y >= int.MinValue && y <= int.MaxValue; }
        private static bool ValidTime(double now) { return !double.IsNaN(now) && !double.IsInfinity(now); }
        private static bool Near(Point point, Point destination)
        { return Math.Abs((long)point.X - destination.X) <= 2 && Math.Abs((long)point.Y - destination.Y) <= 2; }
        private static int Delta(long error, double gain)
        {
            if (Math.Abs(error) <= 2) return 0;
            int amount = (int)Math.Min(64, Math.Max(1, Math.Floor(Math.Abs(error) / gain / 2)));
            return error < 0 ? -amount : amount;
        }
        private static double Estimate(double previous, long observed, int input)
        {
            if (input == 0 || observed == 0 || Math.Sign(observed) != Math.Sign(input)) return previous;
            return Math.Max(.1, Math.Min(20, Math.Abs((double)observed) / Math.Abs(input)));
        }
    }
}
