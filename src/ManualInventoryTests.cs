using System;

namespace SomeFishingGPO
{
    internal static class ManualInventoryTests
    {
        private static bool Rejects(Action action)
        {
            try { action(); return false; }
            catch (ArgumentOutOfRangeException) { return true; }
        }

        internal static void Run(Action<bool, string> check)
        {
            var ledger = new ManualBaitInventory(17, 108, 11, BaitKind.Legendary);
            check(ledger.Count(BaitKind.Common) == 17 && ledger.Count(BaitKind.Rare) == 108 && ledger.ActiveCount == 11,
                "Manual inventory retains independently entered common, rare and legendary quantities");
            check(ledger.HighestAvailable == BaitKind.Legendary && !ledger.Uncertain,
                "Known positive bait has legendary, rare, common selection priority");
            check(ledger.RoundsStarted == 0 && ledger.RoundsFinished == 0,
                "Entering quantities does not invent prior fishing rounds");
            check(ledger.ConsumeRound(1) && ledger.ActiveCount == 10 && ledger.RoundsStarted == 1 && ledger.RoundsFinished == 0,
                "One confirmed minigame consumes one active bait and starts one unfinished round");
            check(!ledger.ConsumeRound(1) && ledger.ActiveCount == 10 && ledger.RoundsStarted == 1,
                "Repeated frames for the same round cannot consume bait twice");
            check(!ledger.FinishRound(500) && ledger.RoundsFinished == 0,
                "An unstarted round cannot be counted as finished");
            check(ledger.FinishRound(1) && !ledger.FinishRound(1) && ledger.RoundsFinished == 1,
                "A disappearing completed minigame counts one finished round regardless of catch outcome");
            ledger.Select(BaitKind.Rare);
            check(!ledger.ConsumeRound(1) && ledger.ActiveCount == 108,
                "Changing selected bait cannot charge another bait for an already recorded round");
            ledger.ConsumeRound(2); ledger.FinishRound(2);
            check(ledger.Count(BaitKind.Rare) == 107 && ledger.Count(BaitKind.Legendary) == 10 && ledger.Count(BaitKind.Common) == 17,
                "Round consumption touches only the selected bait type");

            ledger = new ManualBaitInventory(17, 2, 1, BaitKind.Legendary);
            ledger.ConsumeRound(1); ledger.FinishRound(1);
            check(ledger.ActiveKind == BaitKind.Legendary && ledger.ActiveCount == 0 && ledger.HighestAvailable == BaitKind.Rare,
                "Finishing the last legendary bait proposes rare without pretending a game selection occurred");
            ledger.Select(BaitKind.Rare); ledger.ConsumeRound(2); ledger.FinishRound(2); ledger.ConsumeRound(3); ledger.FinishRound(3);
            check(ledger.HighestAvailable == BaitKind.Common && !ledger.CanRestock(2),
                "Exhausted rare bait proposes common and cannot trigger a common purchase before selection");
            ledger.Select(BaitKind.Common); ledger.SetCount(BaitKind.Common, 2);
            check(ledger.CanRestock(2) && !ledger.CanRestock(1) && ledger.RoundsStarted == 3,
                "Common restocking uses the entered remaining count and configurable threshold without resetting rounds");
            ledger.SetCount(BaitKind.Legendary, 25);
            check(ledger.HighestAvailable == BaitKind.Legendary && ledger.ActiveKind == BaitKind.Common,
                "Reconciliation can suggest another bait without changing the active type behind the engine");
            ledger.MarkUncertain(BaitKind.Legendary);
            check(ledger.HighestAvailable == BaitKind.Common && ledger.Uncertain && !ledger.HasUncertainCount(BaitKind.Common),
                "Uncertain legendary inventory is excluded from selection without invalidating known common bait");

            ledger = new ManualBaitInventory(0, 0, 0, BaitKind.Common);
            check(!ledger.HighestAvailable.HasValue && ledger.CanRestock(2), "An explicitly empty inventory has no available bait and can request common restock");
            check(ledger.ConsumeRound(1) && ledger.ActiveCount == 0 && ledger.RoundsStarted == 1 && ledger.Uncertain && !ledger.CanRestock(2),
                "A real round at estimated zero stays nonnegative and marks that estimate uncertain");
            check(!ledger.ConsumeRound(1) && ledger.RoundsStarted == 1, "Repeated zero-count frames still count the real round only once");
            ledger.SetCount(BaitKind.Common, 8);
            check(!ledger.Uncertain && !ledger.ConsumeRound(1) && ledger.ActiveCount == 8 && ledger.RoundsStarted == 1,
                "Correcting inventory clears uncertainty but preserves the identity of previously charged rounds");
            ledger.ConsumeRound(2);
            check(ledger.ActiveCount == 7 && ledger.RoundsStarted == 2, "The next distinct round uses the corrected quantity");

            ledger = new ManualBaitInventory(2, 108, 11, BaitKind.Common);
            check(!ledger.ConfirmCommonPurchase(1, 298, false) && ledger.ActiveCount == 2 && ledger.HasUncertainCount(BaitKind.Common),
                "An order without inventory evidence cannot invent a successful MAX purchase");
            check(!ledger.CanRestock(2), "Ambiguous purchase inventory prevents repeated threshold spending");
            check(ledger.ConfirmCommonPurchase(1, 298, true) && ledger.ActiveCount == 300 && !ledger.HasUncertainCount(BaitKind.Common),
                "Later evidence resolves one ambiguous purchase and credits its actual common quantity");
            check(!ledger.ConfirmCommonPurchase(1, 298, true) && ledger.ActiveCount == 300,
                "Duplicate purchase confirmation cannot credit the same order twice");
            ledger.MarkPurchaseUncertain(1);
            check(!ledger.Uncertain && ledger.Count(BaitKind.Rare) == 108 && ledger.Count(BaitKind.Legendary) == 11,
                "A late ambiguous callback for a settled purchase cannot alter any bait inventory");
            ledger.MarkPurchaseUncertain(2); ledger.MarkPurchaseUncertain(3);
            check(ledger.ConfirmCommonPurchase(2, 4, true) && ledger.HasUncertainCount(BaitKind.Common),
                "Confirming one order leaves uncertainty from a different unresolved order intact");
            ledger.SetCount(BaitKind.Common, 309);
            check(!ledger.ConfirmCommonPurchase(3, 5, true) && ledger.ActiveCount == 309 && !ledger.Uncertain,
                "An explicit corrected count includes pending orders so delayed confirmation cannot double-credit them");
            ledger.MarkUncertain(BaitKind.Common); ledger.ConfirmCommonPurchase(4, 1, true);
            check(ledger.ActiveCount == 310 && ledger.HasUncertainCount(BaitKind.Common),
                "A confirmed purchase does not repair pre-existing uncertainty about the starting inventory");
            ledger.Select(BaitKind.Legendary); ledger.ConfirmCommonPurchase(5, 10, true);
            check(ledger.ActiveCount == 11 && ledger.Count(BaitKind.Common) == 320,
                "Even while legendary is selected, the common vendor credits common bait only");

            ledger = new ManualBaitInventory(9998, 1, 1, BaitKind.Common);
            check(!ledger.ConfirmCommonPurchase(1, 2, true) && ledger.ActiveCount == 9998 && ledger.Uncertain,
                "An impossible out-of-range purchase total is unresolved rather than overflowed or silently truncated");
            ledger.MarkAllUncertain();
            check(!ledger.HighestAvailable.HasValue && ledger.HasUncertainCount(BaitKind.Rare) && ledger.HasUncertainCount(BaitKind.Legendary),
                "Persisted uncertain inventory can disable all guessed bait selections until the user reconciles it");
            ledger.SetCount(BaitKind.Rare, 108);
            check(ledger.HighestAvailable == BaitKind.Rare && ledger.Uncertain, "One corrected bait type becomes usable while other uncertain quantities remain uncertain");

            check(Rejects(delegate { new ManualBaitInventory(-1, 0, 0, BaitKind.Common); }) && Rejects(delegate { new ManualBaitInventory(0, 10000, 0, BaitKind.Common); }),
                "Manual quantities reject negative values and values above the supported entry range");
            check(Rejects(delegate { ledger.SetCount(BaitKind.Rare, -1); }) && ledger.Count(BaitKind.Rare) == 108,
                "An invalid correction cannot replace a valid known quantity");
            check(Rejects(delegate { ledger.Select((BaitKind)3); }) && Rejects(delegate { ledger.Count((BaitKind)(-1)); }),
                "Unknown bait enum values are rejected rather than accessing another inventory slot");
            check(Rejects(delegate { ledger.ConsumeRound(0); }) && Rejects(delegate { ledger.FinishRound(-1); }) && Rejects(delegate { ledger.MarkPurchaseUncertain(0); }),
                "Invalid round and purchase identities are rejected before mutating history");
            check(Rejects(delegate { ledger.ConfirmCommonPurchase(2, 0, true); }) && Rejects(delegate { ledger.ConfirmCommonPurchase(2, 10000, true); }),
                "Zero and oversized purchase credits cannot become confirmed inventory");

            ledger = new ManualBaitInventory(2, 108, 11, BaitKind.Common);
            check(ledger.IsEstimate && !ledger.LastPurchaseEstimated,
                "Manual round inventory identifies itself as an estimate before any purchase credit");
            check(ledger.EstimateCommonPurchase(1, 298, true) && ledger.ActiveCount == 300 && ledger.LastPurchaseEstimated && ledger.IsEstimate && !ledger.Uncertain,
                "A completed dialog can advance the common estimate by the requested quantity without claiming inventory verification");
            check(!ledger.EstimateCommonPurchase(1, 298, true) && !ledger.ConfirmCommonPurchase(1, 298, true) && ledger.ActiveCount == 300 && ledger.LastPurchaseEstimated,
                "Repeated estimated credit and late confirmed credit share one settled order identity");
            check(ledger.Count(BaitKind.Rare) == 108 && ledger.Count(BaitKind.Legendary) == 11,
                "Estimated common purchases preserve rare and legendary inventory");
            check(!ledger.EstimateCommonPurchase(2, 50, false) && ledger.ActiveCount == 300 && ledger.HasUncertainCount(BaitKind.Common) && !ledger.CanRestock(300),
                "An incomplete purchase dialog creates uncertainty without adding even an estimated quantity");
            check(ledger.EstimateCommonPurchase(2, 50, true) && ledger.ActiveCount == 350 && !ledger.HasUncertainCount(BaitKind.Common),
                "Later completed-dialog evidence can resolve its pending order as one estimated credit");
            ledger.ConfirmCommonPurchase(3, 1, true);
            check(!ledger.EstimateCommonPurchase(3, 1, true) && ledger.ActiveCount == 351 && !ledger.LastPurchaseEstimated && ledger.IsEstimate,
                "A confirmed order cannot be credited again by the estimate path or have its last-credit evidence downgraded");
            ledger.MarkPurchaseUncertain(4); ledger.SetCount(BaitKind.Common, 400);
            check(!ledger.EstimateCommonPurchase(4, 49, true) && ledger.ActiveCount == 400,
                "A corrected quantity also retires pending orders against delayed estimated credits");
            ledger.SetCount(BaitKind.Common, ManualBaitInventory.MaximumCount);
            check(!ledger.EstimateCommonPurchase(5, 1, true) && ledger.ActiveCount == ManualBaitInventory.MaximumCount && ledger.HasUncertainCount(BaitKind.Common),
                "An estimated order exceeding the ledger range stays unresolved instead of overflowing or truncating");
        }
    }
}
