using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace SomeFishingGPO
{
    // One writer and one pending snapshot keep disk latency off the control loop.
    // A newer queued snapshot replaces only pending work, never a write in progress.
    // Neither the worker nor the snapshot retains controls, images or game inputs.
    internal sealed class AsyncSettingsStore : IDisposable
    {
        private static readonly FieldInfo[] fields = typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance);
        private readonly object gate = new object();
        private readonly Action<Settings> writer;
        private Settings pending;
        private bool workerRunning, disposed;
        private long revision;
        private string lastError;

        internal AsyncSettingsStore(string path) : this(FileWriter(path)) { }
        internal AsyncSettingsStore(Action<Settings> writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            this.writer = writer;
        }
        private static Action<Settings> FileWriter(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A settings path is required.", "path");
            // Settings.Save already replaces the destination atomically.
            return delegate(Settings snapshot) { snapshot.Save(path); };
        }
        private static Settings Snapshot(Settings source)
        {
            if (source == null) throw new ArgumentNullException("source");
            var copy = new Settings();
            foreach (FieldInfo field in fields)
            {
                // All current settings are scalars, strings, enums, Points or
                // Rectangles. Refuse future mutable reference fields rather than
                // accidentally sharing their state with the writing thread.
                if (!field.FieldType.IsValueType && field.FieldType != typeof(string))
                    throw new InvalidOperationException("Settings snapshot requires a copy rule for " + field.Name + ".");
                field.SetValue(copy, field.GetValue(source));
            }
            return copy;
        }
        internal long Queue(Settings settings)
        {
            Settings snapshot = Snapshot(settings);
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException("AsyncSettingsStore");
                pending = snapshot;
                long result = ++revision;
                if (!workerRunning)
                {
                    workerRunning = true;
                    try
                    {
                        if (!ThreadPool.QueueUserWorkItem(delegate { WritePending(); }))
                            throw new InvalidOperationException("The settings writer could not start.");
                    }
                    catch (Exception error)
                    {
                        workerRunning = false; pending = null; lastError = error.Message;
                        Monitor.PulseAll(gate); throw;
                    }
                }
                return result;
            }
        }
        private void WritePending()
        {
            while (true)
            {
                Settings snapshot;
                lock (gate)
                {
                    snapshot = pending; pending = null;
                    if (snapshot == null)
                    {
                        workerRunning = false;
                        Monitor.PulseAll(gate); return;
                    }
                }
                string errorMessage = null;
                try { writer(snapshot); }
                catch (Exception error) { errorMessage = error.Message; }
                lock (gate)
                {
                    lastError = errorMessage;
                    Monitor.PulseAll(gate);
                }
                // A failed snapshot is not requeued. Retry only when the caller
                // supplies a later snapshot; a locked disk cannot cause a spin.
            }
        }
        internal bool Pending { get { lock (gate) return workerRunning || pending != null; } }
        internal string LastError { get { lock (gate) return lastError; } }
        internal bool Flush(int timeoutMilliseconds)
        {
            if (timeoutMilliseconds < 0) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            var elapsed = Stopwatch.StartNew();
            lock (gate)
            {
                while (workerRunning || pending != null)
                {
                    long remaining = timeoutMilliseconds - elapsed.ElapsedMilliseconds;
                    if (remaining <= 0) return false;
                    Monitor.Wait(gate, (int)Math.Min(int.MaxValue, remaining));
                }
                return lastError == null;
            }
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
            }
            // No abort: an atomic write already in progress may finish later.
            // Thread-pool workers are background threads and never hold app exit.
            Flush(1000);
        }
    }
}
