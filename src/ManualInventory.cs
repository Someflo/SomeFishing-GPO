using System;
using System.Collections.Generic;

namespace SomeFishingGPO
{
    public enum BaitKind { Common, Rare, Legendary }

    // An estimate from explicit user quantities and confirmed fishing-round events.
    // This class neither reads the screen nor sends game input. The engine owns
    // event identity and must reuse an ID for repeated observations of one round.
    public sealed class ManualBaitInventory
    {
        public const int MaximumCount = 9999;
        private readonly int[] counts = new int[3];
        private readonly bool[] uncertain = new bool[3];
        private readonly HashSet<long> startedRounds = new HashSet<long>();
        private readonly HashSet<long> finishedRounds = new HashSet<long>();
        private readonly HashSet<long> pendingPurchases = new HashSet<long>();
        private readonly HashSet<long> settledPurchases = new HashSet<long>();

        public BaitKind ActiveKind { get; private set; }
        public int ActiveCount { get { return Count(ActiveKind); } }
        public long RoundsStarted { get; private set; }
        public long RoundsFinished { get; private set; }
        // User-entered quantities minus round events remain an estimate; the
        // ledger does not independently verify the current in-game inventory.
        public bool IsEstimate { get { return true; } }
        public bool LastPurchaseEstimated { get; private set; }
        public bool Uncertain
        {
            get { return HasUncertainCount(BaitKind.Common) || HasUncertainCount(BaitKind.Rare) || HasUncertainCount(BaitKind.Legendary); }
        }
        public BaitKind? HighestAvailable
        {
            get
            {
                foreach (BaitKind kind in new[] { BaitKind.Legendary, BaitKind.Rare, BaitKind.Common })
                    if (!HasUncertainCount(kind) && Count(kind) > 0) return kind;
                return null;
            }
        }

        public ManualBaitInventory(int common, int rare, int legendary, BaitKind activeKind)
        {
            CheckCount(common); CheckCount(rare); CheckCount(legendary); CheckKind(activeKind);
            counts[0] = common; counts[1] = rare; counts[2] = legendary; ActiveKind = activeKind;
        }

        public int Count(BaitKind kind) { return counts[CheckKind(kind)]; }
        public bool HasUncertainCount(BaitKind kind)
        {
            int index = CheckKind(kind);
            return uncertain[index] || (kind == BaitKind.Common && pendingPurchases.Count > 0);
        }
        public void Select(BaitKind kind) { CheckKind(kind); ActiveKind = kind; }

        // An explicit correction is authoritative, including an observed zero.
        // It does not change round history or cause an automatic bait selection.
        public void SetCount(BaitKind kind, int count)
        {
            int index = CheckKind(kind); CheckCount(count);
            counts[index] = count; uncertain[index] = false;
            if (kind == BaitKind.Common)
            {
                // The correction already includes any unresolved order. A delayed
                // confirmation for that order must not add its quantity again.
                foreach (long purchaseId in pendingPurchases) settledPurchases.Add(purchaseId);
                pendingPurchases.Clear();
            }
        }

        public void MarkUncertain(BaitKind kind) { uncertain[CheckKind(kind)] = true; }
        public void MarkAllUncertain()
        {
            for (int i = 0; i < uncertain.Length; i++) uncertain[i] = true;
        }

        // Call once a minigame is confirmed, never merely for a cast click.
        // A zero estimate cannot become negative; the unexpected round instead
        // asks for reconciliation while still counting the real round event.
        public bool ConsumeRound(long roundId)
        {
            CheckId(roundId, "roundId");
            if (!startedRounds.Add(roundId)) return false;
            RoundsStarted++;
            int index = CheckKind(ActiveKind);
            if (counts[index] > 0) counts[index]--;
            else uncertain[index] = true;
            return true;
        }

        // Finished means the round ended; it does not assert a caught fish.
        public bool FinishRound(long roundId)
        {
            CheckId(roundId, "roundId");
            if (!startedRounds.Contains(roundId) || !finishedRounds.Add(roundId)) return false;
            RoundsFinished++;
            return true;
        }

        public bool CanRestock(int threshold)
        {
            CheckCount(threshold);
            return ActiveKind == BaitKind.Common && !HasUncertainCount(BaitKind.Common) && ActiveCount <= threshold;
        }

        public void MarkPurchaseUncertain(long purchaseId)
        {
            CheckId(purchaseId, "purchaseId");
            if (!settledPurchases.Contains(purchaseId)) pendingPurchases.Add(purchaseId);
        }

        // A submitted click or a closed dialog alone is not inventory evidence.
        // Supply true only when the caller has evidence for the actual credited
        // quantity. Repeated callbacks and callbacks after correction are inert.
        public bool ConfirmCommonPurchase(long purchaseId, int quantity, bool confirmationEvidence)
        {
            return CreditCommonPurchase(purchaseId, quantity, confirmationEvidence, false);
        }

        // A completed purchase dialog can support continuing the manual estimate
        // with the requested amount. It does not prove payment or the quantity
        // actually received. Failed or incomplete dialogs cannot receive credit.
        public bool EstimateCommonPurchase(long purchaseId, int quantity, bool completedDialogEvidence)
        {
            return CreditCommonPurchase(purchaseId, quantity, completedDialogEvidence, true);
        }

        private bool CreditCommonPurchase(long purchaseId, int quantity, bool evidence, bool estimated)
        {
            CheckId(purchaseId, "purchaseId"); CheckCount(quantity);
            if (quantity == 0) throw new ArgumentOutOfRangeException("quantity");
            if (settledPurchases.Contains(purchaseId)) return false;
            if (!evidence || counts[0] + quantity > MaximumCount)
            {
                pendingPurchases.Add(purchaseId);
                return false;
            }
            counts[0] += quantity;
            pendingPurchases.Remove(purchaseId);
            settledPurchases.Add(purchaseId);
            LastPurchaseEstimated = estimated;
            return true;
        }

        private static int CheckKind(BaitKind kind)
        {
            if (kind < BaitKind.Common || kind > BaitKind.Legendary) throw new ArgumentOutOfRangeException("kind");
            return (int)kind;
        }
        private static void CheckCount(int count)
        {
            if (count < 0 || count > MaximumCount) throw new ArgumentOutOfRangeException("count");
        }
        private static void CheckId(long id, string name)
        {
            if (id < 1) throw new ArgumentOutOfRangeException(name);
        }
    }
}
