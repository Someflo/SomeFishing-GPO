using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SomeFishingGPO
{
    internal static class OcrTimeoutTests
    {
        private sealed class Resource : IDisposable
        {
            internal int Disposals;
            public void Dispose() { Interlocked.Increment(ref Disposals); }
        }
        private static bool Until(Func<bool> condition)
        { return SpinWait.SpinUntil(condition, 2500); }
        private static bool TimesOut(Action operation)
        { try { operation(); return false; } catch (TimeoutException) { return true; } }
        private static Task Pending(object reader)
        { return (Task)reader.GetType().GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reader); }

        internal static void Run(Action<bool,string> check)
        {
            var gate = new WinRtWait();
            var resource = new Resource();
            int result = gate.Run(delegate(CancellationToken token) { return Task.FromResult(42); }, 2500, CancellationToken.None, resource);
            check(result == 42 && resource.Disposals == 1, "Bounded OCR preserves a successful value and disposes its owned resources once");
            check(!gate.Busy, "A completed OCR operation releases its native slot");
            bool originalFault = false;
            resource = new Resource();
            try { gate.Run<int>(delegate(CancellationToken token) { throw new FormatException("fake OCR fault"); }, 2500, CancellationToken.None, resource); }
            catch (FormatException error) { originalFault = error.Message == "fake OCR fault"; }
            check(originalFault && !gate.Busy && resource.Disposals == 1, "OCR failure propagates its original error, frees the slot and disposes resources");

            using (var stopped = new CancellationTokenSource())
            {
                stopped.Cancel(); int starts = 0; bool cancelled = false; resource = new Resource();
                try { gate.Run(delegate(CancellationToken token) { starts++; return Task.FromResult(1); }, 2500, stopped.Token, resource); }
                catch (OperationCanceledException) { cancelled = true; }
                check(cancelled && starts == 0 && resource.Disposals == 1, "A stopped OCR request never starts native work or retains its image");
            }

            var hung = new TaskCompletionSource<int>();
            var cancelSeen = new ManualResetEventSlim();
            int launches = 0; resource = new Resource();
            Resource retained = resource;
            var elapsed = Stopwatch.StartNew();
            bool timedOut = TimesOut(delegate
            {
                gate.Run(delegate(CancellationToken token)
                {
                    Interlocked.Increment(ref launches);
                    token.Register(delegate { cancelSeen.Set(); });
                    return hung.Task;
                }, 150, CancellationToken.None, retained);
            });
            check(timedOut && elapsed.ElapsedMilliseconds < 2500, "A never-completing OCR task returns an unknown result within a bounded wait");
            check(cancelSeen.Wait(2500), "The recognition deadline requests cancellation from the native operation");
            check(gate.Busy && retained.Disposals == 0, "Ignored cancellation retains the single OCR slot and resources until native completion");
            int blocked = 0, disposedRejected = 0;
            for (int i = 0; i < 40; i++)
            {
                var rejected = new Resource();
                try { gate.Run(delegate(CancellationToken token) { Interlocked.Increment(ref launches); return Task.FromResult(99); }, 100, CancellationToken.None, rejected); }
                catch (InvalidOperationException error) { if (error.Message == WinRtWait.BusyDetail) blocked++; }
                disposedRejected += rejected.Disposals;
            }
            check(blocked == 40 && launches == 1, "Forty retries cannot accumulate additional native workers behind one hung OCR request");
            check(disposedRejected == 40 && retained.Disposals == 0, "Rejected captures are released while the pending native capture remains valid");
            hung.SetResult(999);
            check(Until(delegate { return !gate.Busy; }) && retained.Disposals == 1, "A late native completion releases its resources without turning the timeout into a count");
            check(gate.Run(delegate(CancellationToken token) { return Task.FromResult(3); }, 2500, CancellationToken.None) == 3, "OCR resumes with a fresh request once the previous native operation really exits");
            cancelSeen.Dispose();

            var cancelledTask = new TaskCompletionSource<int>();
            check(TimesOut(delegate
            {
                gate.Run(delegate(CancellationToken token)
                {
                    token.Register(delegate { cancelledTask.TrySetCanceled(); });
                    return cancelledTask.Task;
                }, 150, CancellationToken.None);
            }), "Acknowledged deadline cancellation is reported as a timeout instead of a zero");
            check(Until(delegate { return !gate.Busy; }), "Acknowledged cancellation frees the native slot for subsequent readings");

            var lateFault = new TaskCompletionSource<int>();
            check(TimesOut(delegate { gate.Run(delegate(CancellationToken token) { return lateFault.Task; }, 100, CancellationToken.None); }), "A task that may fault later still obeys the initial deadline");
            lateFault.SetException(new InvalidOperationException("late fake native fault"));
            check(Until(delegate { return !gate.Busy; }), "A late native fault is observed and releases the OCR slot");

            using (var releaseFactory = new ManualResetEventSlim())
            {
                resource = new Resource(); retained = resource;
                check(TimesOut(delegate
                {
                    gate.Run(delegate(CancellationToken token) { releaseFactory.Wait(); return Task.FromResult(1); }, 100, CancellationToken.None, retained);
                }) && gate.Busy && retained.Disposals == 0, "Even a stuck native operation factory cannot block the caller past its deadline");
                releaseFactory.Set();
                check(Until(delegate { return !gate.Busy; }) && retained.Disposals == 1, "Releasing a stuck factory cleans up its only retained native resource");
            }

            using (var stopped = new CancellationTokenSource())
            {
                var late = new TaskCompletionSource<int>(); bool interrupted = false;
                stopped.CancelAfter(100);
                try { gate.Run(delegate(CancellationToken token) { return late.Task; }, 2500, stopped.Token); }
                catch (OperationCanceledException) { interrupted = true; }
                check(interrupted && gate.Busy, "Stopping a read interrupts its wait while an unacknowledged native operation keeps the slot");
                late.SetResult(8);
                check(Until(delegate { return !gate.Busy; }), "A stopped native operation can finish without leaving the global OCR slot busy");
            }

            using (var outer = WinRtWait.BeginRead(CancellationToken.None, 1000))
            {
                using (var inner = WinRtWait.BeginRead(CancellationToken.None, 2000))
                    check(inner.Remaining <= 1000, "Nested OCR work shares the outer total read budget instead of extending it per crop");
                check(outer.Remaining <= 1000, "Leaving a nested OCR crop restores the existing total read budget");
            }
            using (var expired = WinRtWait.BeginRead(CancellationToken.None, 0))
                check(TimesOut(delegate { int ignored = expired.Remaining; }), "An exhausted total read budget prevents another OCR recognition from starting");
            CheckReaderLifecycle(check);
        }

        private static void CheckReaderLifecycle(Action<bool,string> check)
        {
            using (var release = new ManualResetEventSlim())
            using (var started = new ManualResetEventSlim())
            {
                int starts = 0;
                var reader = new WindowsBaitReader("", delegate(Bitmap image, string language, CancellationToken stop)
                { Interlocked.Increment(ref starts); started.Set(); release.Wait(); return new BaitReading { Count = 999 }; });
                reader.Submit(new Bitmap(4, 4), 0);
                check(started.Wait(2500) && !reader.Due(9000), "A bait reader remains busy while its existing worker is pending");
                Task pending = Pending(reader);
                reader.Submit(new Bitmap(4, 4), 9000);
                check(starts == 1, "A second bait submission cannot replace a pending read");
                reader.Dispose(); release.Set();
                check(pending.Wait(2500) && !reader.Latest.Count.HasValue && !reader.Due(10000), "Disposal discards late bait quantities and never restarts the stopped reader");
                reader.Dispose();
            }
            using (var release = new ManualResetEventSlim())
            using (var started = new ManualResetEventSlim())
            {
                int starts = 0;
                var reader = new WindowsShopReader("", delegate(Bitmap image, string language, CancellationToken stop)
                { Interlocked.Increment(ref starts); started.Set(); release.Wait(); return new ShopReading { Menu = ShopMenu.Quantity, Maximum = 300, Quantity = 1 }; });
                reader.Submit(new Bitmap(4, 4), new Point(100, 200), 0);
                check(started.Wait(2500) && !reader.Due(9000), "A shop reader remains busy while its existing worker is pending");
                Task pending = Pending(reader);
                reader.Submit(new Bitmap(4, 4), Point.Empty, 9000);
                check(starts == 1, "A second shop submission cannot create concurrent menu OCR work");
                reader.Dispose(); release.Set();
                check(pending.Wait(2500) && reader.Latest.Menu == ShopMenu.Unknown && !reader.Latest.Maximum.HasValue && !reader.Due(10000), "A disposed reader cannot publish a late purchase menu or its MAX quantity");
                reader.Dispose();
            }
        }
    }
}
