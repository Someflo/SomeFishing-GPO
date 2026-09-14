using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;

namespace SomeFishingGPO
{
    internal static class AsyncSettingsStoreTests
    {
        internal static void Run(Action<bool, string> check)
        {
            var entered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            var received = new List<Settings>();
            int active = 0, maximum = 0;
            var store = new AsyncSettingsStore(delegate(Settings snapshot)
            {
                int running = Interlocked.Increment(ref active);
                maximum = Math.Max(maximum, running);
                try
                {
                    if (snapshot.ManualCommonBait == 11) { entered.Set(); release.Wait(); }
                    received.Add(snapshot);
                }
                finally { Interlocked.Decrement(ref active); }
            });
            try
            {
                var first = new Settings { ManualCommonBait = 11, InterfaceLanguage = "en", Area = new Rectangle(1, 2, 80, 300) };
                long firstRevision = store.Queue(first);
                check(entered.Wait(3000), "Settings write starts on a background worker");
                first.ManualCommonBait = 999; first.InterfaceLanguage = "es"; first.Area = Rectangle.Empty;
                check(store.Pending && !store.Flush(0), "An unfinished settings write reports pending without waiting");
                var elapsed = Stopwatch.StartNew();
                check(!store.Flush(30), "A settings flush times out while the writer is blocked");
                check(elapsed.ElapsedMilliseconds < 1000, "A blocked settings flush has a bounded wait");
                long lastRevision = 0;
                var latest = new Settings { ManualCommonBait = 33, ManualRareBait = 108, OcrLanguage = "es-MX" };
                Exception queueError = null;
                var producer = new Thread(delegate()
                {
                    try
                    {
                        for (int i = 0; i < 100; i++) store.Queue(new Settings { ManualCommonBait = 22 });
                        lastRevision = store.Queue(latest);
                        latest.ManualCommonBait = 777; latest.OcrLanguage = "changed after queue";
                    }
                    catch (Exception error) { queueError = error; }
                });
                producer.IsBackground = true; producer.Start();
                check(producer.Join(1000) && queueError == null, "Queueing many settings snapshots does not wait for a blocked disk write");
                check(lastRevision == firstRevision + 101, "Every queued settings snapshot receives a monotonic revision");
                release.Set();
                check(store.Flush(3000) && !store.Pending && store.LastError == null, "Flushing completes after the latest pending snapshot is saved");
                check(received.Count == 2 && maximum == 1, "Repeated pending updates coalesce and never run concurrent settings writers");
                check(received[0].ManualCommonBait == 11 && received[0].InterfaceLanguage == "en" && received[0].Area == new Rectangle(1, 2, 80, 300),
                    "Queued settings retain their original scalar, language and rectangle values");
                check(received[1].ManualCommonBait == 33 && received[1].ManualRareBait == 108 && received[1].OcrLanguage == "es-MX",
                    "The final coalesced settings snapshot is isolated from later caller mutations");
            }
            finally
            {
                release.Set(); store.Flush(3000); store.Dispose(); entered.Dispose(); release.Dispose();
            }

            int attempts = 0;
            using (var errors = new AsyncSettingsStore(delegate(Settings snapshot)
            {
                if (Interlocked.Increment(ref attempts) == 1) throw new IOException("simulated locked settings file");
            }))
            {
                errors.Queue(new Settings());
                check(!errors.Flush(3000) && !errors.Pending && errors.LastError == "simulated locked settings file",
                    "Settings write errors are observable and do not leave work pending");
                check(!errors.Flush(0) && attempts == 1, "A failed settings write does not busy-loop or retry during Flush");
                errors.Queue(new Settings { ManualCommonBait = 299 });
                check(errors.Flush(3000) && errors.LastError == null && attempts == 2,
                    "A later settings snapshot retries after failure and clears a resolved error");
            }

            string folder = Path.Combine(Path.GetTempPath(), "SomeFishing-settings-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "settings.xml");
            Directory.CreateDirectory(folder);
            try
            {
                using (var disk = new AsyncSettingsStore(path))
                {
                    disk.Queue(new Settings { ManualCommonBait = 17, ManualRareBait = 108, InterfaceLanguage = "en" });
                    check(disk.Flush(3000) && Settings.Load(path).ManualCommonBait == 17, "Background settings writing creates a valid settings XML file");
                    disk.Queue(new Settings { ManualCommonBait = 16, ManualRareBait = 108, InterfaceLanguage = "en" });
                    check(disk.Flush(3000) && Settings.Load(path).ManualCommonBait == 16 && !File.Exists(path + ".tmp"),
                        "The background writer preserves atomic replacement on later inventory snapshots");
                }
            }
            finally
            {
                // Only remove the exact files and unique directory this test created.
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
                Directory.Delete(folder);
            }

            var disposeEntered = new ManualResetEventSlim(false);
            var disposeRelease = new ManualResetEventSlim(false);
            var disposing = new AsyncSettingsStore(delegate(Settings snapshot) { disposeEntered.Set(); disposeRelease.Wait(); });
            try
            {
                disposing.Queue(new Settings());
                check(disposeEntered.Wait(3000), "Dispose timeout test has an active settings write");
                var elapsed = Stopwatch.StartNew(); disposing.Dispose();
                check(elapsed.ElapsedMilliseconds < 2200 && disposing.Pending,
                    "Disposing settings storage returns after a bounded wait without aborting an active write");
                bool rejected = false;
                try { disposing.Queue(new Settings()); } catch (ObjectDisposedException) { rejected = true; }
                check(rejected, "Disposed settings storage rejects new work");
                disposeRelease.Set();
                check(disposing.Flush(3000) && !disposing.Pending, "An in-progress settings write can finish cleanly after Dispose timed out");
            }
            finally
            {
                disposeRelease.Set(); disposing.Flush(3000); disposing.Dispose(); disposeEntered.Dispose(); disposeRelease.Dispose();
            }
        }
    }
}
