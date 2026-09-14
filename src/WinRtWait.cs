using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace SomeFishingGPO
{
    // One native OCR pipeline at a time, including a timed-out operation whose
    // cancellation Windows has not acknowledged. Never abandon it and start
    // another native job: that would accumulate jobs/resources during long runs.
    internal sealed class WinRtWait
    {
        internal const int RecognitionMilliseconds = 3000;
        internal const int ReadMilliseconds = 8000;
        internal const string TimeoutDetail = "Windows OCR tardó demasiado. Cantidad desconocida; se conserva el inventario manual.";
        internal const string BusyDetail = "Windows OCR sigue ocupado con una lectura anterior. Se conserva el inventario manual.";
        internal static readonly WinRtWait Shared = new WinRtWait();
        private readonly object sync = new object();
        private Task active;
        [ThreadStatic] private static ReadBudget currentBudget;

        internal sealed class ReadBudget : IDisposable
        {
            private readonly ReadBudget previous;
            private readonly long deadline;
            internal readonly CancellationToken Token;
            internal ReadBudget(int milliseconds, CancellationToken token)
            {
                previous = currentBudget; Token = token;
                deadline = Stopwatch.GetTimestamp() + (long)Math.Ceiling(milliseconds * (double)Stopwatch.Frequency / 1000);
                currentBudget = this;
            }
            internal int Remaining
            {
                get
                {
                    Token.ThrowIfCancellationRequested();
                    long remaining = deadline - Stopwatch.GetTimestamp();
                    if (remaining <= 0) throw new TimeoutException(TimeoutDetail);
                    int own = Math.Max(1, (int)Math.Ceiling(remaining * 1000.0 / Stopwatch.Frequency));
                    return previous == null ? own : Math.Min(own, previous.Remaining);
                }
            }
            public void Dispose() { currentBudget = previous; }
        }

        internal static ReadBudget BeginRead(CancellationToken token, int milliseconds = ReadMilliseconds)
        { return new ReadBudget(milliseconds, token); }

        internal static T Recognize<T>(Func<CancellationToken, Task<T>> operation, IDisposable lifetime)
        {
            ReadBudget budget = currentBudget;
            int timeout;
            try { timeout = budget == null ? RecognitionMilliseconds : Math.Min(RecognitionMilliseconds, budget.Remaining); }
            catch { lifetime.Dispose(); throw; }
            return Shared.Run(operation, timeout, budget == null ? CancellationToken.None : budget.Token, lifetime);
        }

        internal bool Busy { get { lock (sync) return active != null && !active.IsCompleted; } }

        internal T Run<T>(Func<CancellationToken, Task<T>> operation, int milliseconds, CancellationToken stop, IDisposable lifetime = null)
        {
            bool launched = false;
            try
            {
                stop.ThrowIfCancellationRequested();
                if (milliseconds < 1) throw new TimeoutException(TimeoutDetail);
                CancellationTokenSource cancellation;
                Task<T> task;
                var elapsed = Stopwatch.StartNew();
                lock (sync)
                {
                    if (active != null && !active.IsCompleted) throw new InvalidOperationException(BusyDetail);
                    cancellation = CancellationTokenSource.CreateLinkedTokenSource(stop);
                    // Cancellation runs on the timer thread. A misbehaving native
                    // Cancel call cannot extend this worker's bounded wait.
                    cancellation.CancelAfter(milliseconds);
                    CancellationToken token = cancellation.Token;
                    try
                    {
                        task = Task.Run<T>(async delegate
                        {
                            using (lifetime)
                            {
                                token.ThrowIfCancellationRequested();
                                return await operation(token).ConfigureAwait(false);
                            }
                        });
                        active = task; launched = true;
                    }
                    catch { cancellation.Dispose(); throw; }
                }
                // The continuation observes even late faults, disposes cancellation
                // only after the real operation exits and retains the slot meanwhile.
                task.ContinueWith(delegate(Task<T> completed)
                {
                    if (completed.IsFaulted) { var ignored = completed.Exception; }
                    cancellation.Dispose();
                    lock (sync) { if (Object.ReferenceEquals(active, completed)) active = null; }
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                int remaining = Math.Max(0, milliseconds - (int)Math.Min(Int32.MaxValue, elapsed.ElapsedMilliseconds));
                try
                {
                    if (!task.Wait(remaining, stop)) throw new TimeoutException(TimeoutDetail);
                }
                catch (AggregateException)
                {
                    // GetAwaiter unwraps the original exception; a cancelled deadline
                    // means unknown, never a fabricated zero or a closed shop menu.
                    if (task.IsCanceled && !stop.IsCancellationRequested) throw new TimeoutException(TimeoutDetail);
                    return task.GetAwaiter().GetResult();
                }
                return task.GetAwaiter().GetResult();
            }
            finally { if (!launched && lifetime != null) lifetime.Dispose(); }
        }

        internal static async Task<T> Native<T>(IAsyncOperation<T> operation, CancellationToken token)
        {
            // Do not pass cancellation to AsTask: keep its task tied to the
            // operation's actual completion, even when Cancel is ignored.
            using (token.Register(delegate { try { operation.Cancel(); } catch { } }))
                return await operation.AsTask().ConfigureAwait(false);
        }
    }
}
