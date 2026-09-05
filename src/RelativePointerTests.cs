using System;
using System.Collections.Generic;
using System.Drawing;

namespace SomeFishingGPO
{
    internal static class RelativePointerTests
    {
        private sealed class Simulation
        {
            internal Point Position;
            internal double GainX = 1, GainY = 1, Now;
            internal bool Active = true, Stalled;
            internal readonly List<Point> Moves = new List<Point>();
            internal readonly List<double> Times = new List<double>();
            internal Func<Point, bool> Inside = delegate(Point p) { return p.X >= -4000 && p.X <= 4000 && p.Y >= -4000 && p.Y <= 4000; };
            internal RelativePointer Create()
            {
                return new RelativePointer(delegate { return Position; }, delegate(Point delta)
                {
                    Moves.Add(delta); Times.Add(Now);
                    if (!Stalled) Position = new Point(Position.X + (int)Math.Round(delta.X * GainX, MidpointRounding.AwayFromZero), Position.Y + (int)Math.Round(delta.Y * GainY, MidpointRounding.AwayFromZero));
                }, delegate { return Active; }, delegate(Point p) { return Inside(p); });
            }
        }
        internal static void Run(Action<bool, string> check, string output)
        {
            foreach (double gain in new[] { .5, 1.0, 2.0, 3.0 })
            {
                var simulation = new Simulation { Position = new Point(-200, 80), GainX = gain, GainY = gain };
                RelativePointer pointer = simulation.Create(); Point target = new Point(170, -120);
                pointer.Start(target, 0);
                check(simulation.Moves.Count == 0 && pointer.Running && !pointer.Ready, "Relative aiming starts without sending input at gain " + gain);
                RunUntilDone(pointer, simulation);
                check(pointer.Ready && Near(simulation.Position, target), "Relative feedback reaches the marked button at gain " + gain);
                check(Spaced(simulation) && Bounded(simulation), "Relative events are spaced and capped at gain " + gain);
            }
            var anisotropic = new Simulation { Position = new Point(300, -200), GainX = .5, GainY = 3 };
            var mixed = anisotropic.Create(); mixed.Start(new Point(-230, 180), 0); RunUntilDone(mixed, anisotropic);
            check(mixed.Ready && Near(anisotropic.Position, new Point(-230, 180)), "Feedback learns horizontal and vertical gains independently");

            var same = new Simulation { Position = new Point(100, 100) };
            var nudge = same.Create(); nudge.Start(same.Position, 0);
            RunUntilDone(nudge, same);
            bool positive = false, negative = false;
            foreach (Point delta in same.Moves) { if (delta.X > 0) positive = true; if (delta.X < 0) negative = true; }
            check(nudge.Ready && positive && negative && Near(same.Position, new Point(100, 100)), "An already aligned pointer moves away and back before it is ready");
            check(same.Times.Count > 0 && same.Now - same.Times[same.Times.Count - 1] >= 100, "Readiness waits for at least 100 ms after the final movement");

            var extreme = new Simulation { Position = new Point(int.MaxValue - 40, int.MinValue + 40), Inside = delegate { return true; } };
            var nearLimit = extreme.Create(); nearLimit.Start(new Point(int.MaxValue, int.MinValue), 0); RunUntilDone(nearLimit, extreme);
            check(nearLimit.Ready && Near(extreme.Position, new Point(int.MaxValue, int.MinValue)), "Targets at integer limits use long differences without overflow");
            var far = new Simulation { Position = new Point(int.MinValue, 0), Inside = delegate { return true; } };
            var farPointer = far.Create(); farPointer.Start(new Point(int.MaxValue, 0), 0); farPointer.Tick(0);
            check(far.Moves.Count == 1 && far.Moves[0].X == 64, "A cross-integer-range target produces a bounded movement in the correct direction");
            farPointer.Cancel();

            var timing = new Simulation(); var timed = timing.Create(); timed.Start(new Point(500, 0), 0);
            timed.Tick(0); int firstCount = timing.Moves.Count;
            timing.Now = 49; timed.Tick(49); timed.Tick(49);
            check(timing.Moves.Count == firstCount, "Repeated early ticks cannot send another relative event");
            timing.Now = 500; timed.Tick(500);
            check(timing.Moves.Count == firstCount + 1, "A delayed tick sends one event instead of catching up with a burst");
            int beforeCancel = timing.Moves.Count; timed.Cancel(); timed.Cancel(); timing.Now = 1000; timed.Tick(1000);
            check(!timed.Ready && !timed.Running && timing.Moves.Count == beforeCancel, "Cancel is idempotent and prevents future movement");

            var stable = new Simulation { Position = new Point(0, 0) }; var hold = stable.Create(); hold.Start(new Point(10, 0), 0);
            while (stable.Now < 300 && !Near(stable.Position, new Point(10, 0))) { hold.Tick(stable.Now); stable.Now += 50; }
            hold.Tick(stable.Now); stable.Now += 50; hold.Tick(stable.Now);
            check(!hold.Ready, "A newly reached button is not ready before 100 ms of stability");
            stable.Position = new Point(4, 0); stable.Now += 25; hold.Tick(stable.Now);
            check(!hold.Ready && hold.Running, "Position drift resets the stable-button interval");
            hold.Cancel();

            var focus = new Simulation(); var guarded = focus.Create(); guarded.Start(new Point(100, 0), 0);
            focus.Active = false;
            check(Throws(delegate { guarded.Tick(0); }) && !guarded.Running && !guarded.Ready && focus.Moves.Count == 0, "Losing focus stops relative aiming before input");
            guarded.Tick(100);
            check(focus.Moves.Count == 0, "A failed pointer cannot resume sending events");
            var stalled = new Simulation { Stalled = true }; var timeout = stalled.Create(); timeout.Start(new Point(100, 0), 0);
            stalled.Now = 0; timeout.Tick(0); int beforeTimeout = stalled.Moves.Count; stalled.Now = 12000;
            check(Throws(delegate { timeout.Tick(12000); }) && !timeout.Running && stalled.Moves.Count == beforeTimeout, "A stalled pointer times out after 12 seconds without one last movement");
            var outside = new Simulation(); var bounds = outside.Create();
            check(Throws(delegate { bounds.Start(new Point(5000, 0), 0); }) && !bounds.Running && outside.Moves.Count == 0, "A target outside the client is rejected before movement");
            bounds.Start(new Point(100, 0), 0); outside.Position = new Point(5000, 0);
            check(Throws(delegate { bounds.Tick(0); }) && !bounds.Running && outside.Moves.Count == 0, "Unexpected movement outside the client stops aiming");
            var clock = new Simulation(); var clockPointer = clock.Create(); clockPointer.Start(new Point(100, 0), 10);
            check(Throws(delegate { clockPointer.Tick(9); }) && !clockPointer.Running, "A backwards clock cancels relative aiming");
        }
        private static void RunUntilDone(RelativePointer pointer, Simulation simulation)
        {
            for (simulation.Now = 0; simulation.Now < 12000 && pointer.Running; simulation.Now += 25) pointer.Tick(simulation.Now);
        }
        private static bool Near(Point a, Point b) { return Math.Abs((long)a.X - b.X) <= 2 && Math.Abs((long)a.Y - b.Y) <= 2; }
        private static bool Bounded(Simulation simulation)
        {
            foreach (Point delta in simulation.Moves) if (Math.Abs(delta.X) > 64 || Math.Abs(delta.Y) > 64 || delta == Point.Empty) return false;
            return true;
        }
        private static bool Spaced(Simulation simulation)
        {
            for (int i = 1; i < simulation.Times.Count; i++) if (simulation.Times[i] - simulation.Times[i - 1] < 50) return false;
            return true;
        }
        private static bool Throws(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
    }
}
