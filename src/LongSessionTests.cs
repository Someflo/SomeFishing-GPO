using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace SomeFishingGPO
{
    // Accelerated clock tests exercise the actual engine and purchase controllers.
    // No screenshot, native input, thread sleep or real game window is used.
    internal static class LongSessionTests
    {
        internal static void Run(Action<bool, string> check)
        {
            Soak(check, false, 24);
            Soak(check, true, 12);
        }

        private static Settings Options(bool timer)
        {
            return new Settings {
                UseManualBait = true, ManualInventoryConfirmed = true,
                ManualCommonBait = 200, ManualRareBait = 30, ManualLegendaryBait = 20,
                ActiveBaitKind = BaitKind.Common, MonitorBait = true,
                AutoCast = true, AutoBuyBait = true, CastMilliseconds = 220,
                BiteSeconds = 5, RestMilliseconds = 500, CastRetryLimit = 2,
                Area = new Rectangle(100, 100, 70, 320), CastPointSet = true,
                CastPoint = new Point(800, 500), BaitMenuArea = new Rectangle(600, 500, 350, 200),
                UseDirectShopFlow = true, ShopButtonsSet = true,
                ShopLeftPoint = new Point(1100, 930), ShopMiddlePoint = new Point(1250, 930),
                ShopRightPoint = new Point(1400, 930),
                BuyMaximum = !timer, BaitCapacity = 300, BuyBaitAt = 2, BuyQuantity = 50,
                PurchaseByTimer = timer, PurchaseIntervalMinutes = 40, PurchaseLimit = 100,
                ShopOpenMilliseconds = 100, ShopSettleMilliseconds = 200,
                ShopRetryLimit = 2, ShopPhaseTimeoutSeconds = 3,
                LongSessionMode = true, RecoveryPauseSeconds = 20, RecoveryLimit = 3,
                KeepOneBait = true, UseBaitPoints = true, BaitPointsSet = 7,
                BaitLegendaryPoint = new Point(710, 530), BaitRarePoint = new Point(710, 570),
                BaitCommonPoint = new Point(710, 610)
            };
        }

        private static void Soak(Action<bool, string> check, bool timer, int hours)
        {
            string name = (timer ? "Timer" : "Threshold") + " " + hours + "h accelerated session";
            var settings = Options(timer);
            var game = new SimulatedGame(settings, timer ? 42813 : 72149);
            var engine = new FishingEngine(settings, game);
            engine.Start(0);
            double until = hours * 3600000.0;
            long baselineMemory = 0, maximumMemory = 0;
            int ticks = 0, maximumPurchases = 0;
            bool inventoryMatched = true, countsMatched = true;
            string mismatch = "";
            var recent = new Queue<string>();
            Phase previousPhase = engine.State;
            int previousRound = 0;
            while (engine.Running && game.Now < until)
            {
                // Jittered callbacks and occasional scheduler stalls must never
                // turn delayed clicks into a burst based on stale menu evidence.
                double step = ++ticks % 997 == 0 ? 1200 : 100 + (ticks % 4) * 25;
                game.Advance(Math.Min(until, game.Now + step));
                engine.Tick(game.Now);
                if (engine.State != previousPhase || game.RealRounds != previousRound)
                {
                    recent.Enqueue(game.Now + "ms " + engine.State + " round " + engine.RoundsStarted + "/" + game.RealRounds + " common " + engine.Inventory.Count(BaitKind.Common) + "/" + game.Count(BaitKind.Common));
                    while (recent.Count > 12) recent.Dequeue();
                    previousPhase = engine.State; previousRound = game.RealRounds;
                }
                maximumPurchases = Math.Max(maximumPurchases, engine.PurchaseAttempts);
                if (engine.State == Phase.Resting && engine.Cycles > 0)
                {
                    if (countsMatched && (engine.RoundsStarted != game.RealRounds || engine.Cycles != game.RealFinished))
                    { countsMatched = false; mismatch = " first mismatch at " + game.Now + "ms: engine rounds " + engine.RoundsStarted + "/" + engine.Cycles + ", game " + game.RealRounds + "/" + game.RealFinished + ". Recent transitions: " + string.Join("; ", recent); }
                    foreach (BaitKind kind in new[] { BaitKind.Legendary, BaitKind.Rare, BaitKind.Common })
                        if (inventoryMatched && engine.Inventory.Count(kind) != game.Count(kind))
                        { inventoryMatched = false; if (mismatch == "") mismatch = " first mismatch at " + game.Now + "ms: " + kind + " engine " + engine.Inventory.Count(kind) + ", game " + game.Count(kind) + ". Recent transitions: " + string.Join("; ", recent); }
                }
                if (baselineMemory == 0 && game.Now >= 3600000)
                    baselineMemory = GC.GetTotalMemory(true);
                if (game.Now >= 3600000 && ticks % 30000 == 0)
                    maximumMemory = Math.Max(maximumMemory, GC.GetTotalMemory(true));
            }
            check(engine.Running && game.Now >= until, name + " stays running; ended at " + game.Now.ToString("F0", CultureInfo.InvariantCulture) + "ms in " + engine.State + ": " + engine.Status);
            check(countsMatched, name + " counts each observed round exactly once despite dropouts" + (countsMatched ? "" : mismatch));
            check(inventoryMatched && !engine.Inventory.Uncertain, name + " keeps all three manual estimates equal to independent game quantities" + (inventoryMatched ? "" : mismatch));
            check(game.RealFinished > (timer ? 800 : 3000), name + " completes thousands/hundreds of full simulated rounds (" + game.RealFinished + ")");
            check(game.CompletedPurchases > 10 && maximumPurchases > 10 && maximumPurchases < settings.PurchaseLimit,
                name + " replenishes more than ten times within the explicit session limit (" + game.CompletedPurchases + "/" + maximumPurchases + ")");
            check(game.Selections.Count >= 3 && game.Selections.Take(3).SequenceEqual(new[] { BaitKind.Legendary, BaitKind.Rare, BaitKind.Common }) && game.Selections.Skip(3).All(k => k == BaitKind.Common),
                name + " selects legendary, rare, then common and only reselects common thereafter");
            check(game.Count(BaitKind.Legendary) == 1 && game.Count(BaitKind.Rare) == 1 && game.MinimumCommon >= 1,
                name + " keeps one legendary, one rare and at least one common bait so configured rows remain visible");
            check(game.MissedCasts >= 7 && engine.CastAttempts > game.RealRounds && game.RealFinished > 120,
                name + " recovers from repeated missed casts without consuming imaginary bait (" + game.MissedCasts + " misses)");
            check(game.PartialRounds > 5 && game.BriefLostFrames > 50,
                name + " includes repeated five-second partial detections and brief complete image losses");
            check(game.CancelledOrders > 0 && game.IgnoredYesClicks > 0 && game.IgnoredCloseClicks > 0 && game.DelayedMenus > 0,
                name + " exercises failed pre-submit orders, Yes retries, closing retries and delayed menus");
            check(game.DuplicateSubmissions == 0 && game.BuyClicks == game.CompletedPurchases + (game.PendingSubmittedOrder ? 1 : 0) && engine.OrdersSubmitted == game.BuyClicks && engine.PurchaseBudgetUsed == game.BuyClicks,
                name + " never resubmits Buy and charges the purchase limit only for submitted orders");
            check(game.UnsafeInputs == 0 && game.PurchaseDuringRound == 0 && game.CastDuringDialog == 0,
                name + " keeps casting, selection, typing and shop actions in their required states");
            check(game.OcrReads > 100 && game.IncorrectOcrReads > 100,
                name + " tolerates missing and conflicting supporting OCR without replacing the manual ledger");
            if (timer)
                check(game.Quantities.Count > 10 && game.Quantities.All(q => q >= 1 && q <= 50) && game.TimerLengthIntervals > 1,
                    name + " uses adjustable fifty-bait requests and repeated forty-minute replenishment intervals");
            else
                check(game.Quantities.Count > 10 && game.Quantities.All(q => q >= 298 && q <= 300),
                    name + " fills capacity from two or fewer remaining common baits");
            long finalMemory = GC.GetTotalMemory(true);
            maximumMemory = Math.Max(maximumMemory, finalMemory);
            check(baselineMemory > 0 && maximumMemory - baselineMemory < 8L * 1024 * 1024,
                name + " retains less than 8 MiB additional managed memory after hour one (" + Math.Max(0, maximumMemory - baselineMemory) + " bytes); native resources are outside this simulation");
            engine.Stop("Simulated session complete");
            int inputs = game.InputActions;
            for (int i = 0; i < 30; i++) { game.Advance(game.Now + 10000); engine.Tick(game.Now); }
            check(!engine.Running && !game.Held && game.KeysDown.Count == 0 && game.InputActions == inputs,
                name + " releases all held input and cannot send delayed actions after Stop");
        }

        private sealed class SimulatedGame : IGameRuntime, IShopRuntime, IShopVisualRuntime,
            IBaitSelectionRuntime, ICastPointerRuntime, IShopPointerRuntime
        {
            private readonly Settings settings;
            private readonly Random random;
            private readonly int[] counts;
            private double castAimAt, shopAimAt, selectionAt;
            private double fishAt = -1, fishUntil = -1, pendingAt = -1;
            private double previousPurchaseCompletedAt = -1;
            private bool castArmed, castPress, fishStarted, partialCounted, submitted;
            private bool selected, quantityFocused;
            private int openOrder, castReleases, yesThisOrder, closeThisOrder, quantityClicks;
            private string digits = "";
            private Point aim;
            private BaitKind selectedKind;
            private ShopMenuKind pendingMenu;
            private long visualSequence;
            internal double Now;
            internal bool Held;
            internal ShopMenuKind Menu = ShopMenuKind.Absent;
            internal int RealRounds, RealFinished, CompletedPurchases, BuyClicks, DuplicateSubmissions;
            internal int MissedCasts, PartialRounds, BriefLostFrames, CancelledOrders;
            internal int IgnoredYesClicks, IgnoredCloseClicks, DelayedMenus, TimerLengthIntervals;
            internal int UnsafeInputs, PurchaseDuringRound, CastDuringDialog, InputActions;
            internal int OcrReads, IncorrectOcrReads;
            internal int MinimumCommon;
            internal readonly HashSet<int> KeysDown = new HashSet<int>();
            internal readonly List<BaitKind> Selections = new List<BaitKind>();
            internal readonly List<int> Quantities = new List<int>();
            internal bool PendingSubmittedOrder { get { return submitted && (Menu != ShopMenuKind.Absent || pendingAt >= 0); } }
            public bool IsActive { get { return true; } }
            private bool FishActive { get { return fishStarted && Now < fishUntil; } }

            internal SimulatedGame(Settings options, int seed)
            {
                settings = options; random = new Random(seed);
                counts = new[] { options.ManualCommonBait, options.ManualRareBait, options.ManualLegendaryBait };
                MinimumCommon = options.ManualCommonBait;
            }
            internal int Count(BaitKind kind) { return counts[(int)kind]; }
            internal void Advance(double now)
            {
                Now = now;
                if (pendingAt >= 0 && Now >= pendingAt) { Menu = pendingMenu; pendingAt = -1; }
                if (fishAt >= 0 && Now >= fishAt && !fishStarted)
                {
                    Need(counts[(int)selectedKind] > (settings.KeepOneBait ? 1 : 0), "A fishing round cannot consume the last reserved bait");
                    counts[(int)selectedKind]--; RealRounds++; fishStarted = true; partialCounted = false;
                    MinimumCommon = Math.Min(MinimumCommon, counts[(int)BaitKind.Common]);
                }
                if (fishStarted && Now >= fishUntil)
                { RealFinished++; fishAt = fishUntil = -1; fishStarted = false; }
            }
            private void Need(bool condition, string message)
            { if (!condition) { UnsafeInputs++; throw new InvalidOperationException(message); } }
            private void Schedule(ShopMenuKind kind, double delay)
            { pendingMenu = kind; pendingAt = Now + delay; if (delay > 1500) DelayedMenus++; }
            public void MoveToCastPoint()
            {
                if (Menu != ShopMenuKind.Absent || pendingAt >= 0) CastDuringDialog++;
                Need(Menu == ShopMenuKind.Absent && pendingAt < 0 && !FishActive, "Cast aimed during an active menu");
                Need(selected && Count(selectedKind) > (settings.KeepOneBait ? 1 : 0), "Cast requires a selected bait row above its reserve");
                InputActions++; castArmed = true; castPress = false; castAimAt = Now;
            }
            public bool TickCastAim(double now) { return now - castAimAt >= 175; }
            public void SetHeld(bool held)
            {
                if (held && castArmed) castPress = true;
                if (!held && castArmed && castPress)
                {
                    castArmed = castPress = false; castReleases++;
                    // Seven consecutive failures exceed the ordinary retry
                    // window and require a genuine delayed recovery cycle.
                    int burst = castReleases % 389;
                    if (burst >= 120 && burst <= 126) { MissedCasts++; }
                    else
                    {
                        fishAt = Now + 500 + random.Next(0, 5) * 100;
                        fishUntil = fishAt + (settings.PurchaseByTimer ? random.Next(30000, 40001) : random.Next(10000, 18001));
                    }
                }
                if (Held != held) InputActions++;
                Held = held;
            }
            public void Release() { Held = false; KeysDown.Clear(); }
            public void SetJumpHeld(bool held) { if (held) InputActions++; }
            public Observation Observe()
            {
                if (!FishActive) return new Observation();
                double elapsed = Now - fishAt;
                if (RealRounds % 31 == 11 && elapsed >= 3000 && elapsed < 8000)
                {
                    if (!partialCounted) { PartialRounds++; partialCounted = true; }
                    return new Observation { MenuVisible = true };
                }
                if (elapsed >= 2000 && ((int)elapsed % 1900) < 120)
                { BriefLostFrames++; return new Observation(); }
                double fish = 155 + Math.Sin(elapsed / 640.0) * 90;
                return new Observation { Found = true, MenuVisible = true, FishY = fish,
                    GapY = 155, GapTop = 125, GapBottom = 185 };
            }
            public BaitReading ReadBait(double now)
            {
                OcrReads++;
                int? count = Count(selectedKind);
                if (OcrReads % 13 < 3) { IncorrectOcrReads++; count = OcrReads % 2 == 0 ? (int?)null : 999; }
                return new BaitReading { Count = count, Sequence = OcrReads, SampledAt = now,
                    VisuallyAbsent = !count.HasValue, Detail = "Simulated supporting OCR" };
            }
            public ShopReading ReadShop(double now)
            { throw new InvalidOperationException("Direct purchase must not request shop OCR"); }
            public void BeginBaitSelection(BaitKind kind, double now)
            {
                Need(!Held && !FishActive && Menu == ShopMenuKind.Absent && Count(kind) > (settings.KeepOneBait ? 1 : 0), "Cannot select a depleted or reserved bait row or select during a menu");
                selectedKind = kind; selected = false; selectionAt = now; Selections.Add(kind); InputActions++;
            }
            public BaitSelectionResult TickBaitSelection(double now)
            {
                bool ready = now - selectionAt >= 200;
                if (ready) selected = true;
                return new BaitSelectionResult { Completed = ready, Succeeded = ready, Status = "Simulated selected bait border" };
            }
            public ShopVisualReading ReadShopVisual(double now)
            { return new ShopVisualReading { Menu = Menu, Sequence = ++visualSequence, SampledAt = now, Detail = Menu.ToString() }; }
            public void ShopAim(Point point)
            {
                Need(!Held && !FishActive, "Shop aiming must release fishing input");
                aim = point; shopAimAt = Now; InputActions++;
            }
            public bool TickShopAim(double now) { return now - shopAimAt >= 250; }
            public void ShopClick(Point point, ShopClickKind kind)
            {
                Need(!Held && !FishActive && point == aim && Now - shopAimAt >= 250, "Shop click used an unsettled pointer or active fishing round");
                InputActions++;
                if (point == settings.ShopRightPoint && (Menu == ShopMenuKind.Confirm || Menu == ShopMenuKind.Quantity))
                { CancelledOrders++; Menu = ShopMenuKind.Absent; pendingAt = -1; quantityFocused = false; return; }
                if (point == settings.ShopLeftPoint && Menu == ShopMenuKind.Confirm)
                {
                    yesThisOrder++;
                    if (openOrder % 7 == 3 || (openOrder % 5 == 2 && yesThisOrder == 1)) { IgnoredYesClicks++; return; }
                    Schedule(ShopMenuKind.Quantity, openOrder % 3 == 0 ? 2400 : 350); return;
                }
                if (point == settings.ShopMiddlePoint && Menu == ShopMenuKind.Quantity && kind == ShopClickKind.Quantity)
                { quantityClicks++; quantityFocused = quantityClicks >= 2; return; }
                if (point == settings.ShopLeftPoint && Menu == ShopMenuKind.Quantity)
                {
                    if (submitted) DuplicateSubmissions++;
                    Need(!submitted && quantityFocused, "Buy duplicated or quantity field never received two clicks");
                    int quantity;
                    Need(int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out quantity) && quantity > 0 && Count(BaitKind.Common) + quantity <= settings.BaitCapacity,
                        "Purchase quantity exceeds real capacity or is not typed correctly");
                    counts[(int)BaitKind.Common] += quantity; Quantities.Add(quantity);
                    BuyClicks++; submitted = true; Schedule(ShopMenuKind.Done, 500); return;
                }
                if (point == settings.ShopMiddlePoint && Menu == ShopMenuKind.Done)
                {
                    closeThisOrder++;
                    if (openOrder % 4 == 1 && closeThisOrder == 1) { IgnoredCloseClicks++; return; }
                    Need(submitted, "Closed Done without submitted purchase");
                    CompletedPurchases++; Menu = ShopMenuKind.Absent;
                    previousPurchaseCompletedAt = Now; return;
                }
                Need(false, "Shop click did not match the visible menu");
            }
            public void ShopKey(int key, bool held)
            {
                if (!held)
                {
                    bool wasDown = KeysDown.Remove(key);
                    if (key == 0x45 && wasDown)
                    {
                        Need(Menu == ShopMenuKind.Absent && pendingAt < 0, "E used while dialog was already opening");
                        openOrder++; submitted = false; quantityFocused = false; digits = "";
                        yesThisOrder = closeThisOrder = quantityClicks = 0;
                        if (previousPurchaseCompletedAt >= 0 && Now - previousPurchaseCompletedAt >= 40 * 60000) TimerLengthIntervals++;
                        Schedule(ShopMenuKind.Confirm, openOrder % 3 == 0 ? 2200 : 350);
                    }
                    return;
                }
                if (FishActive) PurchaseDuringRound++;
                Need(!Held && !FishActive, "Shop key pressed during fishing control");
                if (key != 0x45) Need(Menu == ShopMenuKind.Quantity && quantityFocused, "Quantity typing targeted a different menu or unfocused field");
                KeysDown.Add(key); InputActions++;
                if (key == 0x08) digits = "";
                if (key >= 0x30 && key <= 0x39) digits += (char)key;
            }
        }
    }
}
