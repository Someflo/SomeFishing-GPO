using System;
using System.Drawing;

namespace SomeFishingGPO
{
    internal static class BaitPointSelectionTests
    {
        internal static void Run(Action<bool, string> check)
        {
            Settings settings = MarkedSettings(); Point point;
            var client = new Rectangle(-1920, 0, 1920, 1080);
            check(BaitPointTargets.TryGet(settings, BaitKind.Common, out point) && point == new Point(-1600, 700),
                "Common bait resolves only the explicitly marked common point");
            check(BaitPointTargets.TryGet(settings, BaitKind.Rare, out point) && point == new Point(-1600, 650) &&
                BaitPointTargets.TryGet(settings, BaitKind.Legendary, out point) && point == new Point(-1600, 600),
                "Rare and legendary bait resolve their independent marked points");
            check(BaitPointTargets.ConfiguredInside(settings, client) && settings.BaitMenuArea.IsEmpty,
                "Direct bait points accept a negative-coordinate monitor without requiring a menu image area");
            check(BaitPointTargets.Authorized(settings, BaitKind.Legendary, new Point(-1600, 600), client) &&
                !BaitPointTargets.Authorized(settings, BaitKind.Legendary, new Point(-1600, 650), client) &&
                !BaitPointTargets.Authorized(settings, BaitKind.Legendary, new Point(-1599, 600), client),
                "Only the exact point marked for the requested bait type is authorized");
            settings.BaitPointsSet = 3;
            check(!BaitPointTargets.TryGet(settings, BaitKind.Legendary, out point) &&
                !BaitPointTargets.Authorized(settings, BaitKind.Legendary, settings.BaitLegendaryPoint, client),
                "A stored coordinate without its marked bit cannot authorize selection");
            check(!BaitPointTargets.TryGet(settings, (BaitKind)99, out point), "An unknown bait type never falls back to another marked point");
            settings.BaitPointsSet = 7; settings.BaitCommonPoint = new Point(0, 700);
            check(!BaitPointTargets.ConfiguredInside(settings, client), "A bait point on the excluded window edge fails bounds validation");
            settings = MarkedSettings(); settings.UseBaitPoints = false;
            check(!BaitPointTargets.TryGet(settings, BaitKind.Common, out point), "Disabled point mode grants no point-selection authority");
            settings = MarkedSettings(); settings.UseManualBait = false;
            check(!BaitPointTargets.ConfiguredInside(settings, client), "Disabled manual inventory grants no bait-point authority");
            settings = MarkedSettings(); settings.BaitPointsSet = 8;
            check(!BaitPointTargets.ConfiguredInside(settings, client), "Unsupported point-mask bits are rejected");
            settings = MarkedSettings(); settings.ManualLegendaryBait = 0; settings.ManualRareBait = 1;
            settings.BaitLegendaryPoint = new Point(2000, 2000); settings.BaitRarePoint = new Point(2000, 1900);
            check(BaitPointTargets.ConfiguredInside(settings, client),
                "Old points belonging only to absent or reserved bait cannot block a usable common point");
            settings = MarkedSettings(); settings.ManualCommonBait = 1; settings.ManualRareBait = 0; settings.ManualLegendaryBait = 1; settings.BaitPointsSet = 0;
            check(BaitPointTargets.ConfiguredInside(settings, client),
                "A reserved-only inventory can wait without marking points that cannot be selected");
            settings.AutoBuyBait = true;
            check(!BaitPointTargets.ConfiguredInside(settings, client),
                "Automatic common replenishment requires its marked point even when only the reserve remains");

            var fake = new Fake(); var selection = fake.Controller(); selection.Begin(BaitKind.Legendary, 0);
            check(fake.Aims == 0 && fake.Clicks == 0, "Starting a direct selection queues a transaction without sending a click");
            selection.Tick(0); selection.Tick(100);
            check(fake.Aims == 1 && fake.Clicks == 0 && fake.Target == fake.Settings.BaitLegendaryPoint,
                "Direct selection aims at legendary bait and waits for smooth movement completion");
            fake.Ready = true; selection.Tick(200); selection.Tick(549);
            check(fake.Clicks == 0, "Direct selection waits at least 350 ms after the pointer settles");
            selection.Tick(550); BaitSelectionResult result = selection.Tick(799);
            check(fake.Clicks == 1 && fake.Pending && !result.Completed,
                "Exactly one bait press is sent and completion waits for button release");
            result = selection.Tick(800); selection.Tick(20000);
            check(result.Completed && result.Succeeded && fake.Clicks == 1 && !fake.Pending && result.Status.Contains("sin verificar"),
                "A released direct click completes without OCR and explicitly reports unverified game selection");

            fake = new Fake { Ready = true }; selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0);
            selection.Tick(0); selection.Tick(100); selection.Cancel(); int actions = fake.Aims + fake.Clicks;
            result = selection.Tick(5000);
            check(result.Completed && !result.Succeeded && fake.Aims + fake.Clicks == actions,
                "Stopping a pending point selection prevents every later move and click");

            fake = new Fake { Ready = true }; selection = fake.Controller(); selection.Begin(BaitKind.Common, 0);
            selection.Tick(0); selection.Tick(100); fake.Active = false; result = selection.Tick(450);
            check(result.Completed && !result.Succeeded && fake.Clicks == 0,
                "Focus loss during the settle period cancels before the bait click");

            fake = new Fake { Ready = true }; selection = fake.Controller(); selection.Begin(BaitKind.Legendary, 0);
            selection.Tick(0); selection.Tick(100); fake.Settings.BaitLegendaryPoint = new Point(-1500, 600); result = selection.Tick(450);
            check(result.Completed && !result.Succeeded && fake.Clicks == 0,
                "A changed configured target cannot redirect an already pending bait transaction");

            fake = new Fake(); fake.Settings.BaitPointsSet = 1; selection = fake.Controller(); selection.Begin(BaitKind.Legendary, 0);
            result = selection.Tick(1000);
            check(result.Completed && !result.Succeeded && fake.Aims == 0 && fake.Clicks == 0,
                "An unmarked requested type fails without waiting for OCR or clicking common bait");

            fake = new Fake(); selection = fake.Controller(); selection.Begin(BaitKind.Common, 0); selection.Tick(0); result = selection.Tick(10000);
            check(result.Completed && !result.Succeeded && fake.Clicks == 0,
                "A pointer movement that never settles has a bounded failure");

            fake = new Fake { Ready = true, ThrowClick = true }; selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0);
            selection.Tick(0); selection.Tick(100); result = selection.Tick(450); selection.Tick(5000);
            check(result.Completed && !result.Succeeded && fake.Clicks == 1 && !fake.Pending,
                "An input rejection releases the attempted press and never retries a second click");

            fake = new Fake { Ready = true, StickyRelease = true }; selection = fake.Controller(); selection.Begin(BaitKind.Common, 0);
            selection.Tick(0); selection.Tick(100); selection.Tick(450); result = selection.Tick(700);
            check(!result.Completed && fake.Pending && fake.Clicks == 1,
                "Pending button-up prevents a successful direct bait selection result");
            result = selection.Tick(10000);
            check(result.Completed && !result.Succeeded && fake.Clicks == 1,
                "A button-up that remains pending ends with bounded failure and no repeated press");

            fake = new Fake { Ready = true }; selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0);
            selection.Tick(0); selection.Tick(100); selection.Tick(450); fake.Active = false; result = selection.Tick(700);
            check(result.Completed && !result.Succeeded && !fake.Pending && fake.Clicks == 1,
                "Focus loss after a bait press releases it and cannot report selection success");

            fake = new Fake(); selection = fake.Controller(); selection.Begin(BaitKind.Common, 100); result = selection.Tick(99);
            check(result.Completed && !result.Succeeded && fake.Aims == 0 && fake.Clicks == 0,
                "A reversed selection clock cannot move or click a bait point");
        }
        private static Settings MarkedSettings()
        {
            return new Settings { UseManualBait = true, UseBaitPoints = true, BaitPointsSet = 7, KeepOneBait = true,
                ManualCommonBait = 17, ManualRareBait = 108, ManualLegendaryBait = 11,
                BaitCommonPoint = new Point(-1600, 700), BaitRarePoint = new Point(-1600, 650),
                BaitLegendaryPoint = new Point(-1600, 600) };
        }
        private sealed class Fake
        {
            internal Settings Settings = MarkedSettings();
            internal bool Active = true, Ready, Pending, StickyRelease, ThrowClick;
            internal int Aims, Clicks, Releases;
            internal Point Target;
            internal BaitPointSelectionController Controller()
            {
                return new BaitPointSelectionController(delegate { return Active; }, delegate(BaitKind kind) {
                    Point point; return BaitPointTargets.TryGet(Settings, kind, out point) ? (Point?)point : null;
                }, delegate(BaitKind kind, Point point) { return BaitPointTargets.Authorized(Settings, kind, point, new Rectangle(-1920, 0, 1920, 1080)); },
                    delegate(Point point) { Aims++; Target = point; }, delegate(double now) { return Ready; },
                    delegate(Point point) { Clicks++; Pending = true; if (ThrowClick) throw new InvalidOperationException("Synthetic press rejection"); },
                    delegate { Releases++; if (!StickyRelease) Pending = false; }, delegate { return Pending; }, 10);
            }
        }
    }
}
