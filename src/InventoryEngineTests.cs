using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace SomeFishingGPO
{
    internal static class InventoryEngineTests
    {
        private static Settings Options(int common = 10, int rare = 0, int legendary = 0)
        {
            return new Settings { UseManualBait = true, ManualInventoryConfirmed = true,
                ManualCommonBait = common, ManualRareBait = rare, ManualLegendaryBait = legendary,
                ActiveBaitKind = BaitKind.Common, AutoCast = true, AutoBuyBait = false, MonitorBait = false,
                Area = new Rectangle(100, 100, 60, 300), BaitMenuArea = new Rectangle(600, 500, 350, 200),
                CastPointSet = true, CastPoint = new Point(800, 500), CastMilliseconds = 220,
                BiteSeconds = 5, RestMilliseconds = 500, CastRetryLimit = 0,
                UseDirectShopFlow = true, ShopButtonsSet = true,
                ShopLeftPoint = new Point(1100, 930), ShopMiddlePoint = new Point(1250, 930), ShopRightPoint = new Point(1400, 930),
                BaitCapacity = 300, BuyBaitAt = 2, BuyQuantity = 50, BuyMaximum = true,
                PurchaseIntervalMinutes = 1, ShopOpenMilliseconds = 100, ShopSettleMilliseconds = 200,
                ShopRetryLimit = 0, ShopPhaseTimeoutSeconds = 3 };
        }
        private static Observation Fish()
        { return new Observation { Found = true, MenuVisible = true, FishY = 80, GapY = 180, GapTop = 150, GapBottom = 210 }; }
        private static void At(FishingEngine engine, Fake game, double now)
        { game.Now = now; engine.Tick(now); }
        private static void Until(FishingEngine engine, Fake game, Func<bool> reached, double maximumMilliseconds = 30000)
        {
            double end = game.Now + maximumMilliseconds;
            while (engine.Running && !reached() && game.Now < end) At(engine, game, game.Now + 50);
        }
        private static void StartFish(FishingEngine engine, Fake game)
        {
            Until(engine, game, delegate { return engine.State == Phase.Waiting; });
            game.Current = Fish(); At(engine, game, game.Now + 50); At(engine, game, game.Now + 50);
        }
        private static void EndFish(FishingEngine engine, Fake game)
        {
            game.Current = new Observation();
            Until(engine, game, delegate { return engine.State == Phase.Resting; }, 2000);
        }
        private static FishingEngine Start(Settings options, Fake game)
        { var engine = new FishingEngine(options, game); engine.Start(0); return engine; }

        internal static void Run(Action<bool, string> check)
        {
            var options = Options(3, 1, 1); var game = new Fake(); var engine = Start(options, game);
            Until(engine, game, delegate { return engine.State == Phase.SelectingBait; });
            check(game.Selections.SequenceEqual(new[] { BaitKind.Legendary }) && engine.CastAttempts == 0 && game.CastMoves == 0,
                "Manual engine locates and selects legendary bait before its first casting input");
            Until(engine, game, delegate { return engine.State == Phase.Casting; });
            check(engine.CastAttempts == 1 && engine.RoundsStarted == 0 && engine.Inventory.ActiveCount == 1 && game.SelectionCompleted,
                "Selecting and launching alone cannot consume estimated bait or invent a fishing round");
            StartFish(engine, game);
            check(engine.State == Phase.Tracking && engine.RoundsStarted == 1 && engine.Inventory.ActiveCount == 0 && engine.Cycles == 0,
                "Two confirmed fish frames begin one round and decrement the selected legendary estimate");
            for (int i = 0; i < 20; i++) At(engine, game, game.Now + 50);
            check(engine.RoundsStarted == 1 && engine.Inventory.ActiveCount == 0 && engine.Inventory.RoundsStarted == 1,
                "Repeated tracking frames cannot consume the final bait repeatedly");
            game.Current = new Observation(); At(engine, game, game.Now + 50); At(engine, game, game.Now + 1000);
            game.Current = Fish(); At(engine, game, game.Now + 50); At(engine, game, game.Now + 50);
            check(engine.RoundsStarted == 1 && engine.Cycles == 0 && engine.Inventory.RoundsFinished == 0,
                "A brief detection dropout within one minigame creates neither a new round nor a finished round");
            EndFish(engine, game);
            check(engine.Cycles == 1 && engine.Inventory.RoundsFinished == 1,
                "A sustained menu disappearance counts one finished round without claiming a catch");
            Until(engine, game, delegate { return engine.State == Phase.Casting; }); StartFish(engine, game); EndFish(engine, game);
            check(game.Selections.SequenceEqual(new[] { BaitKind.Legendary, BaitKind.Rare }) && engine.Inventory.Count(BaitKind.Rare) == 0 && engine.Cycles == 2,
                "After the legendary row is depleted, the next round requires a fresh rare selection and charges rare only");
            Until(engine, game, delegate { return engine.State == Phase.Casting; }); StartFish(engine, game); EndFish(engine, game);
            check(game.Selections.SequenceEqual(new[] { BaitKind.Legendary, BaitKind.Rare, BaitKind.Common }) && engine.Inventory.ActiveKind == BaitKind.Common && engine.Inventory.ActiveCount == 2 && engine.RoundsStarted == 3,
                "After rare depletion the engine selects common and preserves the three separate type quantities");
            Until(engine, game, delegate { return engine.State == Phase.Casting; });
            check(game.Selections.Count == 3 && engine.CastAttempts == 4,
                "Further common casts retain the confirmed common selection without selecting a vanished row again");
            engine.Stop("test");

            game = new Fake(); engine = Start(Options(9), game);
            Until(engine, game, delegate { return engine.State == Phase.RecoveringShop; }, 15000);
            check(engine.State == Phase.RecoveringShop && engine.CastAttempts == 1 && engine.RoundsStarted == 0 && engine.Inventory.ActiveCount == 9,
                "A cast whose minigame never appears respects zero retries and leaves manual inventory untouched");
            game.Menu = ShopMenuKind.Quantity;
            Until(engine, game, delegate { return engine.State == Phase.Preparing; });
            check(engine.Running && game.CancelClicks == 1 && game.BuyClicks == 0 && engine.Inventory.ActiveCount == 9,
                "Missing-minigame recovery identifies Quantity and cancels it without buying or charging bait");
            Until(engine, game, delegate { return engine.State == Phase.Casting; });
            check(engine.CastAttempts == 2 && game.Selections.Count == 2,
                "Recovery confirms bait selection again before returning to the water for another cast");
            Until(engine, game, delegate { return !engine.Running; }, 15000);
            check(!engine.Running && engine.CastAttempts == 2 && game.CancelClicks == 1 && engine.RoundsStarted == 0,
                "Repeated failure after the one recovery does not cause an unbounded cast or cleanup loop");

            game = new Fake { SelectionSucceeds = false }; engine = Start(Options(), game);
            Until(engine, game, delegate { return !engine.Running; });
            check(!engine.Running && engine.CastAttempts == 0 && game.CastMoves == 0 && engine.RoundsStarted == 0,
                "An unconfirmed bait-row selection stops before casting or consuming inventory");

            foreach (int remaining in new[] { 2, 0 })
            {
                options = Options(remaining); options.AutoBuyBait = true; game = new Fake(); engine = Start(options, game);
                Until(engine, game, delegate { return engine.State == Phase.Purchasing; });
                check(engine.PurchaseAttempts == 1 && engine.CastAttempts == 0 && engine.PurchaseDetail.Contains("cantidad solicitada: " + (300 - remaining)),
                    "Manual remaining " + remaining + " computes a common replenishment of capacity minus remaining before casting");
                Until(engine, game, delegate { return engine.State == Phase.Preparing; });
                check(engine.Running && game.Digits == (300 - remaining).ToString(CultureInfo.InvariantCulture) && game.BuyClicks == 1 && engine.Inventory.ActiveCount == 300 && engine.Inventory.LastPurchaseEstimated,
                    "Observed purchase completion credits the requested common amount once as an estimate from " + remaining);
                check(engine.RoundsStarted == 0 && engine.Cycles == 0 && game.BaitReads == 0 && game.ShopReads == 0,
                    "A manual purchase needs neither counter OCR nor invented fishing rounds from " + remaining);
                Until(engine, game, delegate { return engine.State == Phase.Casting; });
                check(engine.PurchaseAttempts == 1 && game.BuyClicks == 1 && engine.Inventory.ActiveCount == 300 && game.SelectionCompleted,
                    "Resuming after replenishment reselects common without charging the estimated inventory or buying again");
                engine.Stop("test");
            }

            options = Options(270); options.AutoBuyBait = true; options.PurchaseByTimer = true; options.AutoCast = false;
            game = new Fake(); engine = Start(options, game);
            Until(engine, game, delegate { return engine.State == Phase.Waiting; }); At(engine, game, 60000);
            Until(engine, game, delegate { return engine.State == Phase.Purchasing; });
            check(engine.PurchaseAttempts == 1 && engine.PurchaseDetail.Contains("cantidad solicitada: 30"),
                "A timed request for fifty common bait is capped to thirty when the manual estimate is 270 of 300");
            Until(engine, game, delegate { return engine.State == Phase.Preparing; });
            check(engine.Inventory.ActiveCount == 300 && game.Digits == "30" && engine.TimerStatus(game.Now).Contains("01:00") && engine.CastAttempts == 0,
                "Timed replenishment updates the common estimate and restarts its interval while preserving manual-cast mode");
            engine.Stop("test");

            options = Options(0); options.AutoBuyBait = true; options.PurchaseByTimer = true;
            game = new Fake(); engine = Start(options, game);
            Until(engine, game, delegate { return engine.State == Phase.Purchasing; }, 5000);
            check(engine.PurchaseAttempts == 1 && game.Now < 60000 && engine.PurchaseDetail.Contains("cantidad solicitada: 50") && engine.CastAttempts == 0,
                "An empty manual common inventory can replenish fifty immediately instead of casting without bait until the timer");
            engine.Stop("test");

            options = Options(20, 2, 1); options.AutoBuyBait = true; options.PurchaseByTimer = true; options.AutoCast = false;
            game = new Fake(); engine = Start(options, game); Until(engine, game, delegate { return engine.State == Phase.Waiting; }); At(engine, game, 60000);
            check(engine.PurchaseAttempts == 0 && engine.Inventory.ActiveKind == BaitKind.Legendary && game.BuyClicks == 0,
                "A common-bait timer cannot interrupt the higher-priority legendary bait before common becomes active");
            engine.Stop("test");

            options = Options(3); options.AutoBuyBait = true; game = new Fake(); engine = Start(options, game);
            Until(engine, game, delegate { return engine.State == Phase.Casting; }); StartFish(engine, game);
            for (int i = 0; i < 20; i++) At(engine, game, game.Now + 50);
            check(engine.Inventory.ActiveCount == 2 && engine.State == Phase.Tracking && engine.PurchaseAttempts == 0 && engine.Cycles == 0,
                "Reaching threshold two at round start lets that confirmed manual round finish before opening the shop");
            EndFish(engine, game); Until(engine, game, delegate { return engine.State == Phase.Purchasing; });
            check(engine.Cycles == 1 && engine.Inventory.RoundsFinished == 1 && engine.PurchaseDetail.Contains("cantidad solicitada: 298"),
                "The round-driven replenishment quantity uses the once-decremented common count after the menu closes");
            engine.Stop("test");

            options = Options(2); options.AutoBuyBait = true;
            game = new Fake { IgnoreYesClicks = 100 }; engine = Start(options, game);
            Until(engine, game, delegate { return engine.State == Phase.RecoveringShop; });
            check(engine.State == Phase.RecoveringShop && !engine.PurchaseSubmitted && engine.Inventory.ActiveCount == 2 && !engine.Inventory.Uncertain,
                "A failed initial Yes phase begins recovery without marking an unsent purchase as spent or ambiguous");
            Until(engine, game, delegate { return engine.State == Phase.Preparing; });
            Until(engine, game, delegate { return engine.State == Phase.Casting; });
            check(engine.Running && game.CancelClicks == 1 && game.BuyClicks == 0 && engine.PurchaseAttempts == 1 && engine.CastAttempts == 1 && engine.Inventory.ActiveCount == 2,
                "Recovery cancels Confirm and returns to fishing with a purchase retry cooldown instead of immediate repeated spending");
            engine.Stop("test");

            options = Options(2); options.AutoBuyBait = true;
            game = new Fake { SuppressDoneAfterBuy = true }; engine = Start(options, game);
            Until(engine, game, delegate { return engine.State == Phase.RecoveringShop; });
            check(engine.State == Phase.RecoveringShop && engine.PurchaseSubmitted && engine.Inventory.Uncertain && engine.Inventory.ActiveCount == 2,
                "A submitted purchase without a completed dialog leaves manual inventory uncertain with no fictional credit");
            Until(engine, game, delegate { return !engine.Running; });
            int inputs = game.InputCount; At(engine, game, game.Now + 120000);
            check(!engine.Running && game.CancelClicks == 1 && game.BuyClicks == 1 && engine.CastAttempts == 0 && engine.PurchaseAttempts == 1 && game.InputCount == inputs,
                "An ambiguous submitted purchase is cleaned up once then requires correction without buying or fishing on a guessed inventory");

            options = Options(2); options.AutoBuyBait = true; options.ShopRetryLimit = 1;
            game = new Fake { IgnoreCloseClicks = 1 }; engine = Start(options, game);
            Until(engine, game, delegate { return engine.State == Phase.Purchasing; });
            Until(engine, game, delegate { return engine.State == Phase.Preparing; });
            check(engine.Running && game.CloseClicks == 2 && game.BuyClicks == 1 && engine.PurchaseAttempts == 1 && engine.Inventory.ActiveCount == 300 && !engine.Inventory.Uncertain,
                "Retrying a stuck final close button cannot resubmit the purchase or credit its estimated quantity twice");
            engine.Stop("test");

            options = Options(2, 108, 11); options.AutoBuyBait = true; options.ManualInventoryUncertain = true; options.TestBuyQuantity = 17;
            game = new Fake { ForbidObservations = true, ForbidBait = true }; engine = new FishingEngine(options, game, RunKind.PurchaseTest); engine.Start(0);
            Until(engine, game, delegate { return !engine.Running; });
            check(!engine.Running && engine.Inventory == null && game.Digits == "17" && game.BuyClicks == 1 && game.Selections.Count == 0 && game.CastMoves == 0,
                "Purchase diagnostics bypass manual thresholds, uncertainty and bait selection to test the explicit quantity once");
            check(engine.RoundsStarted == 0 && engine.Cycles == 0 && game.Observations == 0 && game.BaitReads == 0,
                "Purchase diagnostics cannot charge manual rounds or require fish/counter OCR");
            options = Options(100, 108, 11); options.ManualInventoryUncertain = true;
            game = new Fake { ForbidObservations = true, ForbidBait = true }; engine = new FishingEngine(options, game, RunKind.EmptyBaitTest); engine.Start(0);
            Until(engine, game, delegate { return !engine.Running; });
            check(!engine.Running && engine.Inventory == null && game.Selections.Count == 0 && game.CastMoves == 0 && game.BuyClicks == 0,
                "Empty-bait diagnostics simulate zero independently from manual starting quantities without fishing inputs");
        }

        private sealed class Fake : IGameRuntime, IShopRuntime, IShopVisualRuntime, IBaitSelectionRuntime
        {
            internal bool Active = true, FishingHeld, SelectionCompleted, SelectionSucceeds = true;
            internal bool SuppressDoneAfterBuy, ForbidObservations, ForbidBait;
            internal double Now, selectionAt;
            internal int CastMoves, BaitReads, ShopReads, Observations, YesClicks, BuyClicks, CloseClicks, CancelClicks;
            internal int IgnoreYesClicks, IgnoreCloseClicks;
            internal long visualSequence;
            internal Observation Current = new Observation();
            internal ShopMenuKind Menu = ShopMenuKind.Absent;
            internal Point Aimed;
            internal readonly List<BaitKind> Selections = new List<BaitKind>();
            internal readonly List<int> Keys = new List<int>();
            internal readonly HashSet<int> KeysDown = new HashSet<int>();
            internal string Digits { get { return string.Concat(Keys.Where(k => k >= 0x30 && k <= 0x39).Select(k => ((char)k).ToString())); } }
            internal int InputCount { get { return CastMoves + Keys.Count + YesClicks + BuyClicks + CloseClicks + CancelClicks + Selections.Count; } }
            public bool IsActive { get { return Active; } }
            public void MoveToCastPoint()
            {
                if (!Active || !SelectionCompleted) throw new InvalidOperationException("Cast requires an active confirmed bait selection");
                if (Menu != ShopMenuKind.Absent) throw new InvalidOperationException("Cast cannot target an open purchase dialog");
                CastMoves++;
            }
            public void SetHeld(bool held) { if (held && !Active) throw new InvalidOperationException("Inactive fishing input"); FishingHeld = held; }
            public void Release() { FishingHeld = false; KeysDown.Clear(); }
            public Observation Observe() { Observations++; if (ForbidObservations) throw new InvalidOperationException("Diagnostic inspected fish area"); return Current; }
            public BaitReading ReadBait(double now) { BaitReads++; if (ForbidBait) throw new InvalidOperationException("Diagnostic read bait OCR"); return new BaitReading { Sequence = BaitReads, SampledAt = now }; }
            public void SetJumpHeld(bool held) { if (held && !Active) throw new InvalidOperationException("Inactive jump"); }
            public ShopReading ReadShop(double now) { ShopReads++; throw new InvalidOperationException("Manual direct flow must not read shop OCR"); }
            public void BeginBaitSelection(BaitKind kind, double now)
            { if (!Active || FishingHeld) throw new InvalidOperationException("Unsafe bait selection"); Selections.Add(kind); SelectionCompleted = false; selectionAt = now; }
            public BaitSelectionResult TickBaitSelection(double now)
            {
                bool finished = now - selectionAt >= 100;
                if (finished && SelectionSucceeds) SelectionCompleted = true;
                return new BaitSelectionResult { Completed = finished, Succeeded = finished && SelectionSucceeds, Status = SelectionSucceeds ? "Cebo seleccionado" : "Selección no confirmada" };
            }
            public ShopVisualReading ReadShopVisual(double now)
            { return new ShopVisualReading { Menu = Menu, Sequence = ++visualSequence, SampledAt = now, Detail = Menu.ToString() }; }
            public void ShopAim(Point point) { if (!Active) throw new InvalidOperationException("Inactive shop aim"); Release(); Aimed = point; }
            public void ShopClick(Point point, ShopClickKind kind)
            {
                if (!Active || FishingHeld || point != Aimed) throw new InvalidOperationException("Unsafe shop click");
                if (point.X == 1400 && (Menu == ShopMenuKind.Confirm || Menu == ShopMenuKind.Quantity)) { CancelClicks++; Menu = ShopMenuKind.Absent; }
                else if (point.X == 1100 && Menu == ShopMenuKind.Confirm) { YesClicks++; if (IgnoreYesClicks-- <= 0) Menu = ShopMenuKind.Quantity; }
                else if (point.X == 1100 && Menu == ShopMenuKind.Quantity) { BuyClicks++; if (!SuppressDoneAfterBuy) Menu = ShopMenuKind.Done; }
                else if (point.X == 1250 && Menu == ShopMenuKind.Done) { CloseClicks++; if (IgnoreCloseClicks-- <= 0) Menu = ShopMenuKind.Absent; }
                else if (!(point.X == 1250 && Menu == ShopMenuKind.Quantity && kind == ShopClickKind.Quantity)) throw new InvalidOperationException("Click does not match the observed menu");
            }
            public void ShopKey(int key, bool held)
            {
                if (!held) { KeysDown.Remove(key); if (key == 0x45 && Menu == ShopMenuKind.Absent) Menu = ShopMenuKind.Confirm; return; }
                if (!Active || FishingHeld) throw new InvalidOperationException("Unsafe shop key");
                if (key != 0x45 && Menu != ShopMenuKind.Quantity) throw new InvalidOperationException("Typing outside quantity menu");
                Keys.Add(key); KeysDown.Add(key);
            }
        }
    }
}
