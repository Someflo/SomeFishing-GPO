using System;
using System.Collections.Generic;
using System.Drawing;

namespace SomeFishingGPO
{
    internal static class BaitReserveTests
    {
        internal static void Run(Action<bool, string> check)
        {
            var options = Options(1, 0, 0); options.IdleJumpEnabled = true; options.MonitorBait = true;
            var game = new Fake { OcrCount = 1 }; var engine = new FishingEngine(options, game); engine.Start(0);
            bool seenIdle = false, returnedToPreparing = false;
            for (double now = 0; now <= 60000; now += 50)
            {
                engine.Tick(now);
                if (engine.State == Phase.IdleWaiting || engine.State == Phase.Jumping) seenIdle = true;
                if (seenIdle && engine.State == Phase.Preparing) returnedToPreparing = true;
            }
            check(engine.Running && seenIdle && !returnedToPreparing,
                "An OCR reading of one reserved common bait cannot repeatedly reactivate exhausted manual inventory");
            check(engine.JumpRequests >= 4 && game.JumpPresses == engine.JumpRequests,
                "Keeping one common bait without purchasing preserves scheduled idle jumps instead of resetting their deadline");
            check(engine.Inventory.ActiveCount == 1 && engine.RoundsStarted == 0 && engine.CastAttempts == 0 && game.CastMoves == 0 && game.MousePresses == 0,
                "Reserved-only idle waiting cannot consume bait, invent rounds or send fishing input");
            engine.Stop("test");

            options = Options(1, 0, 0); options.IdleJumpEnabled = false; options.MonitorBait = true;
            game = new Fake { OcrCount = 1 }; engine = new FishingEngine(options, game); engine.Start(0);
            for (double now = 0; now <= 5000; now += 50) engine.Tick(now);
            check(!engine.Running && engine.Inventory.ActiveCount == 1 && engine.CastAttempts == 0 && engine.PurchaseAttempts == 0,
                "One common bait with buying and idle jumps disabled stops while preserving its reserve");
            check(game.CastMoves == 0 && game.MousePresses == 0 && game.JumpPresses == 0 && game.Selected.Count == 0,
                "A reserved-only stopped session sends no selection, fishing or jump input");

            options = Options(0, 0, 0); options.IdleJumpEnabled = false;
            game = new Fake { OcrCount = 99 }; engine = new FishingEngine(options, game); engine.Start(0);
            for (double now = 0; now <= 5000; now += 50) engine.Tick(now);
            check(!engine.Running && engine.Inventory.ActiveCount == 0 && game.Selected.Count == 0 && game.CastMoves == 0,
                "An all-zero manual inventory cannot be revived by unrelated positive OCR evidence");

            options = Options(3, 1, 1); options.AutoCast = false; options.MonitorBait = false;
            game = new Fake(); engine = new FishingEngine(options, game); engine.Start(0);
            double clock = 0;
            Until(engine, ref clock, delegate { return engine.State == Phase.Waiting; });
            check(game.Selected.Count == 1 && game.Selected[0] == BaitKind.Common && engine.Inventory.ActiveKind == BaitKind.Common,
                "Legendary and rare quantities of one are reserved and selection starts on usable common bait");
            check(engine.Inventory.Count(BaitKind.Legendary) == 1 && engine.Inventory.Count(BaitKind.Rare) == 1 && engine.Inventory.ActiveCount == 3,
                "Skipping reserved higher-tier bait never changes its quantities or consumes a round");
            CompleteRound(engine, game, ref clock);
            Until(engine, ref clock, delegate { return engine.State == Phase.Waiting; });
            CompleteRound(engine, game, ref clock);
            Until(engine, ref clock, delegate { return !engine.Running; });
            check(!engine.Running && engine.RoundsStarted == 2 && engine.Cycles == 2 && engine.Inventory.ActiveCount == 1,
                "Two confirmed rounds from three common bait stop at one reserve without starting a third round");
            check(engine.Inventory.Count(BaitKind.Legendary) == 1 && engine.Inventory.Count(BaitKind.Rare) == 1 && game.Selected.Count == 1,
                "Common depletion to reserve keeps higher-tier rows present and does not select either reserved type");

            options = Options(2, 1, 0); options.AutoCast = false;
            game = new Fake(); engine = new FishingEngine(options, game); engine.Start(0); clock = 0;
            Until(engine, ref clock, delegate { return engine.State == Phase.Waiting; });
            check(game.Selected.Count == 1 && game.Selected[0] == BaitKind.Common && engine.Inventory.Count(BaitKind.Legendary) == 0,
                "An absent legendary row and reserved rare row require only the usable common selection");
            engine.Stop("test");

            options = Options(2, 2, 1); options.AutoCast = false;
            game = new Fake(); engine = new FishingEngine(options, game); engine.Start(0); clock = 0;
            Until(engine, ref clock, delegate { return engine.State == Phase.Waiting; });
            check(game.Selected.Count == 1 && game.Selected[0] == BaitKind.Rare,
                "A reserved legendary quantity does not block usable rare bait from being selected first");
            CompleteRound(engine, game, ref clock);
            Until(engine, ref clock, delegate { return engine.State == Phase.Waiting && engine.Inventory.ActiveKind == BaitKind.Common; });
            check(game.Selected.Count == 2 && game.Selected[1] == BaitKind.Common && engine.Inventory.Count(BaitKind.Rare) == 1,
                "The last usable rare bait switches to common while retaining one rare bait");
            engine.Stop("test");

            foreach (bool timer in new[] { false, true })
            {
                options = Options(1, 0, 0); options.AutoBuyBait = true; options.PurchaseByTimer = timer;
                options.BaitCapacity = 1; options.BuyBaitAt = 0; options.ShopButtonsSet = true;
                options.ShopLeftPoint = new Point(1100, 930); options.ShopMiddlePoint = new Point(1250, 930); options.ShopRightPoint = new Point(1400, 930);
                string issue = options.Validate(new Rectangle(0, 0, 1920, 1080), true);
                check(issue != null && issue.Contains("reserva"),
                    (timer ? "Timed" : "Threshold") + " manual buying rejects capacity equal to the preserved reserve");
                options.BaitCapacity = 2;
                check(options.Validate(new Rectangle(0, 0, 1920, 1080), true) == null,
                    (timer ? "Timed" : "Threshold") + " manual buying permits capacity above the preserved reserve");
            }
        }
        private static Settings Options(int common, int rare, int legendary)
        {
            return new Settings { UseManualBait = true, ManualInventoryConfirmed = true,
                ManualCommonBait = common, ManualRareBait = rare, ManualLegendaryBait = legendary,
                KeepOneBait = true, UseBaitPoints = true, BaitPointsSet = 7,
                BaitCommonPoint = new Point(700, 600), BaitRarePoint = new Point(700, 550), BaitLegendaryPoint = new Point(700, 500),
                Area = new Rectangle(100, 100, 70, 320), CastPointSet = true, CastPoint = new Point(900, 500),
                AutoCast = true, AutoBuyBait = false, UseDirectShopFlow = true, MonitorBait = true,
                IdleJumpSeconds = 15, LongSessionMode = true, RecoveryLimit = 3, RestMilliseconds = 500 };
        }
        private static void Until(FishingEngine engine, ref double clock, Func<bool> ready)
        {
            for (int i = 0; i < 400 && !ready(); i++) { clock += 50; engine.Tick(clock); }
            if (!ready()) throw new Exception("Reserve test did not reach its expected state: " + engine.State + " / " + engine.Status);
        }
        private static void CompleteRound(FishingEngine engine, Fake game, ref double clock)
        {
            game.Frame = new Observation { Found = true, MenuVisible = true, GapTop = 80, GapBottom = 130, GapY = 105, FishY = 110 };
            Until(engine, ref clock, delegate { return engine.State == Phase.Tracking; });
            game.Frame = new Observation();
            Until(engine, ref clock, delegate { return engine.State == Phase.Resting; });
        }
        private sealed class Fake : IGameRuntime, IBaitSelectionRuntime
        {
            internal readonly List<BaitKind> Selected = new List<BaitKind>();
            internal int OcrCount = 1, CastMoves, MousePresses, JumpPresses;
            internal Observation Frame = new Observation();
            private long sequence;
            public bool IsActive { get { return true; } }
            public void MoveToCastPoint() { CastMoves++; }
            public void SetHeld(bool held) { if (held) MousePresses++; }
            public void SetJumpHeld(bool held) { if (held) JumpPresses++; }
            public void Release() { }
            public Observation Observe() { return Frame; }
            public BaitReading ReadBait(double now) { return new BaitReading { Sequence = ++sequence, SampledAt = now, Count = OcrCount }; }
            public void BeginBaitSelection(BaitKind kind, double now) { Selected.Add(kind); }
            public BaitSelectionResult TickBaitSelection(double now)
            { return new BaitSelectionResult { Completed = true, Succeeded = true, Status = "Synthetic point click released" }; }
        }
    }
}
