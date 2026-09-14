using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SomeFishingGPO
{
    public static class BaitSelectionTests
    {
        public static void Run(Action<bool, string> check, string output)
        {
            BaitMenuRow row, other;
            using (Bitmap image = Sample(new[] { BaitKind.Legendary, BaitKind.Rare, BaitKind.Common }, null))
            {
                BaitMenuReading menu = BaitMenuVisual.Analyze(image);
                check(menu.Rows.Count == 3, "Bait labels and their own yellow counters identify all three types without OCR");
                check(menu.TryGetRow(BaitKind.Legendary, out row) && row.Bounds.Top < 60 && !row.Selected, "Rainbow legendary lettering is distinguished from the cyan rare label");
                check(menu.TryGetRow(BaitKind.Rare, out row) && row.Bounds.Top > 70, "Rare bait is identified by its label rather than a fixed row index");
                check(menu.TryGetRow(BaitKind.Common, out row) && row.Bounds.Top > 105 && row.Bounds.Contains(row.ClickPoint), "Common bait has a point inside its independently detected row");
                check(row.CounterBounds.Left > row.ClickPoint.X && row.CounterBounds.Width < 65, "The per-type OCR crop contains the yellow counter and excludes the label");
                BaitMenuReading negative = BaitMenuVisual.Analyze(image, new Point(-800, 400));
                negative.TryGetRow(BaitKind.Common, out other);
                check(other.ClickPoint == new Point(row.ClickPoint.X - 800, row.ClickPoint.Y + 400)
                    && other.CounterBounds.Location == new Point(row.CounterBounds.X - 800, row.CounterBounds.Y + 400), "Menu rows and counter crops preserve negative desktop coordinates");
                if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); image.Save(Path.Combine(output, "bait-types.png"), ImageFormat.Png); }
            }
            using (Bitmap image = Sample(new[] { BaitKind.Rare, BaitKind.Common }, BaitKind.Rare))
            {
                BaitMenuReading menu = BaitMenuVisual.Analyze(image);
                check(menu.Rows.Count == 2 && !menu.TryGetRow(BaitKind.Legendary, out row), "Disappearing legendary rows do not leave a phantom selectable item");
                check(menu.TryGetRow(BaitKind.Rare, out row) && row.Selected && row.Bounds.Top < 60, "The shifted rare row is selected only with its own yellow outline");
                check(menu.TryGetRow(BaitKind.Common, out row) && !row.Selected && row.Bounds.Top > 70 && row.Bounds.Top < 100, "Common moves upward after a row disappears without inheriting the previous highlight");
            }
            using (Bitmap image = Sample(new[] { BaitKind.Common }, BaitKind.Common))
            {
                BaitMenuReading menu = BaitMenuVisual.Analyze(image);
                check(menu.Rows.Count == 1 && menu.TryGetRow(BaitKind.Common, out row) && row.Selected && row.Bounds.Top < 60,
                    "The last remaining common row is recognized at the top and its gold border is confirmed");
                if (!string.IsNullOrEmpty(output)) image.Save(Path.Combine(output, "bait-common-only.png"), ImageFormat.Png);
            }
            using (Bitmap image = Sample(new[] { BaitKind.Rare, BaitKind.Rare }, null))
            { check(!BaitMenuVisual.Analyze(image).TryGetRow(BaitKind.Rare, out row), "Duplicate same-colour bait rows are ambiguous and cannot authorize a click"); }
            using (Bitmap image = Sample(new BaitKind[0], null))
            { check(BaitMenuVisual.Analyze(image).Rows.Count == 0, "A white menu title and gold help text cannot impersonate a bait row"); }
            using (var image = new Bitmap(300, 150))
            {
                using (Graphics graphics = Graphics.FromImage(image)) graphics.Clear(Color.Cyan);
                check(BaitMenuVisual.Analyze(image).Rows.Count == 0, "A flat cyan scene without a complete label and quantity is rejected");
            }
            using (Bitmap image = Sample(new[] { BaitKind.Common }, null))
            {
                using (Graphics graphics = Graphics.FromImage(image)) { graphics.FillRectangle(Brushes.Black, 221, 42, 65, 25); }
                check(BaitMenuVisual.Analyze(image).Rows.Count == 0, "A label without a visible associated counter cannot select an exhausted row");
            }
            foreach (string amount in new[] { "1", "2", "4", "14", "300" })
            using (Bitmap image = Sample(new[] { BaitKind.Common }, null))
            {
                using (Graphics graphics = Graphics.FromImage(image)) graphics.FillRectangle(Brushes.Black, 220, 42, 75, 37);
                Glyphs(image, 235, 51, 1, delegate(int i) { return Color.FromArgb(255, 183, 0); });
                for (int digit = 0; digit < amount.Length; digit++) Digit(image, 247 + digit * 7, 52, amount[digit]);
                int last = 247 + (amount.Length - 1) * 7 + 4;
                // A slanted side breaks into short vertical runs, as on the
                // supplied gameplay screenshot. It is deliberately close to xN.
                using (Graphics graphics = Graphics.FromImage(image)) using (var pen = new Pen(Color.FromArgb(255, 183, 0), 2))
                    graphics.DrawLines(pen, new[] { new Point(23, 44), new Point(last + 6, 44), new Point(last + 9, 59),
                        new Point(last + 6, 76), new Point(23, 76), new Point(23, 44) });
                BaitMenuReading menu = BaitMenuVisual.Analyze(image);
                bool found = menu.TryGetRow(BaitKind.Common, out row);
                check(found && row.Selected && row.CounterBounds.Right <= last + 5 && row.CounterBounds.Top >= 48 && row.CounterBounds.Bottom <= 64,
                    "Selected x" + amount + " counter crop excludes the gold top, bottom and slanted side outline");
                bool preserved = found;
                for (int y = 51; y <= 59; y++) for (int x = 235; x <= last; x++)
                {
                    Color pixel = image.GetPixel(x, y);
                    if (pixel.R > 200 && pixel.G > 130 && pixel.B < 30 && (!found || !row.CounterBounds.Contains(x, y))) preserved = false;
                }
                check(preserved, "The complete x" + amount + " glyphs survive the outline crop, including a separated narrow 1");
            }
            using (Bitmap image = Sample(new[] { BaitKind.Legendary, BaitKind.Rare, BaitKind.Common }, BaitKind.Common))
            {
                using (Graphics graphics = Graphics.FromImage(image)) graphics.FillRectangle(Brushes.Black, 229, 117, 40, 18);
                BaitMenuReading menu = BaitMenuVisual.Analyze(image);
                check(menu.Rows.Count == 2 && !menu.TryGetRow(BaitKind.Common, out row),
                    "A remaining gold highlight without common quantity cannot select another type as exhausted common bait");
            }

            var fake = new Fake(); var selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0);
            selection.Tick(0); selection.Tick(100);
            check(fake.Aims == 0 && fake.Clicks == 0, "Bait selection requires fresh stable evidence before moving the mouse");
            selection.Tick(200); selection.Tick(300); selection.Tick(400); selection.Tick(500); selection.Tick(600);
            check(fake.Aims == 1 && fake.Clicks == 1 && fake.Target == new Point(140, 90), "Selection moves smoothly, waits for arrival and settles before one row click");
            BaitSelectionResult result = selection.Tick(700);
            check(!result.Completed, "A sent mouse click alone is never reported as a verified bait selection");
            fake.Selected = true; selection.Tick(1000); result = selection.Tick(1200);
            check(result.Completed && result.Succeeded && fake.Clicks == 1 && fake.Releases > 0, "Two fresh selected-outline frames finish selection and release the input");

            fake = new Fake { Selected = true }; selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0); selection.Tick(0); result = selection.Tick(200);
            check(result.Completed && result.Succeeded && fake.Aims == 0 && fake.Clicks == 0, "An already selected type is confirmed without an unnecessary click");

            fake = new Fake(); selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0); selection.Tick(0); selection.Tick(200);
            fake.Point = new Point(140, 52); selection.Tick(400);
            check(fake.Clicks == 0 && fake.Releases >= 3, "A row shifting while the pointer is travelling cancels the old destination before clicking");
            selection.Tick(600); selection.Tick(800); selection.Tick(1000);
            check(fake.Clicks == 1 && fake.Target == new Point(140, 52), "A newly stable row is reacquired at its current position");

            fake = new Fake(); selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0); selection.Tick(0); selection.Tick(200); fake.Active = false;
            result = selection.Tick(400); int actions = fake.Aims + fake.Clicks; selection.Tick(5000);
            check(result.Completed && !result.Succeeded && fake.Aims + fake.Clicks == actions && fake.Clicks == 0, "Focus loss cancels selection, releases inputs and prevents any delayed click");

            fake = new Fake(); selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0); selection.Tick(0); selection.Tick(200); selection.Cancel(); actions = fake.Aims + fake.Clicks;
            result = selection.Tick(1000);
            check(result.Completed && !result.Succeeded && fake.Aims + fake.Clicks == actions, "An explicit stop cannot resume the pending bait selection");

            fake = new Fake { Fresh = false }; selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0);
            selection.Tick(0); selection.Tick(200); selection.Tick(400); result = selection.Tick(10000);
            check(result.Completed && !result.Succeeded && fake.Clicks == 0 && fake.Aims == 0, "Repeated stale frames cannot authorize a move or a click and eventually time out");

            fake = new Fake { Present = false }; selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0); selection.Tick(0); result = selection.Tick(10000);
            check(result.Completed && !result.Succeeded && fake.Clicks == 0, "A missing requested type ends with a bounded failure rather than choosing another row");

            fake = new Fake(); selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0);
            for (double now = 0; now <= 10000; now += 200) result = selection.Tick(now);
            check(result.Completed && !result.Succeeded && fake.Clicks == 2, "An unconfirmed highlight permits at most two paced selection clicks before timing out");

            fake = new Fake { ThrowClick = true }; selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0);
            for (double now = 0; now <= 1200; now += 200) result = selection.Tick(now);
            check(result.Completed && !result.Succeeded && fake.Clicks == 1 && fake.Releases > 1, "A rejected click is released and reported without repeatedly attempting the same input");

            fake = new Fake(); selection = fake.Controller(); selection.Begin(BaitKind.Rare, 0); selection.Tick(200); result = selection.Tick(100);
            check(result.Completed && !result.Succeeded && fake.Clicks == 0, "A reversed clock cannot advance a pending selection");
        }

        private static Bitmap Sample(BaitKind[] kinds, BaitKind? selected)
        {
            var image = new Bitmap(300, 190, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(image)) graphics.Clear(Color.FromArgb(26, 29, 33));
            Glyphs(image, 72, 14, 20, delegate(int i) { return Color.White; });
            for (int i = 0; i < kinds.Length; i++)
            {
                int y = 51 + i * 35; BaitKind kind = kinds[i];
                using (Graphics graphics = Graphics.FromImage(image))
                using (var background = new SolidBrush(Color.FromArgb(51, 52, 50))) graphics.FillRectangle(background, 23, y - 6, 256, 23);
                Glyphs(image, 35, y, 24, delegate(int part) {
                    if (kind == BaitKind.Common) return Color.White;
                    if (kind == BaitKind.Rare) return Color.FromArgb(64, 229, 242);
                    return new[] { Color.FromArgb(230, 25, 220), Color.FromArgb(20, 30, 245), Color.FromArgb(65, 245, 30), Color.FromArgb(245, 25, 35) }[part / 6];
                });
                Glyphs(image, 235, y, 4, delegate(int part) { return Color.FromArgb(255, 183, 0); });
                if (selected.HasValue && selected.Value == kind)
                    using (Graphics graphics = Graphics.FromImage(image)) using (var pen = new Pen(Color.FromArgb(255, 183, 0), 2)) graphics.DrawRectangle(pen, 23, y - 7, 256, 25);
            }
            Glyphs(image, 38, 171, 33, delegate(int i) { return Color.FromArgb(255, 183, 0); });
            return image;
        }
        private static void Glyphs(Bitmap image, int left, int top, int count, Func<int, Color> colour)
        {
            string[] glyph = { "10001", "11011", "01010", "01110", "00100", "01110", "01010", "11011", "10001" };
            for (int n = 0; n < count; n++) for (int y = 0; y < glyph.Length; y++) for (int x = 0; x < 5; x++)
                if (glyph[y][x] == '1') image.SetPixel(left + n * 7 + x, top + y, colour(n));
        }
        private static void Digit(Bitmap image, int left, int top, char value)
        {
            string[] glyph = value == '1' ? new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } :
                value == '2' ? new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } :
                value == '3' ? new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" } :
                value == '4' ? new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" } :
                new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" };
            for (int y = 0; y < glyph.Length; y++) for (int x = 0; x < 5; x++)
                if (glyph[y][x] == '1') image.SetPixel(left + x, top + y, Color.FromArgb(255, 183, 0));
        }
        private sealed class Fake
        {
            internal bool Active = true, Present = true, Selected, Fresh = true, ThrowClick;
            internal int Aims, Clicks, Releases;
            internal Point Point = new Point(140, 90), Target;
            private long sequence;
            internal BaitSelectionController Controller()
            {
                return new BaitSelectionController(delegate { return Active; }, delegate(double now) {
                    var frame = new BaitMenuReading { Sequence = Fresh ? ++sequence : 1, SampledAt = Fresh ? now : 0 };
                    if (Present) frame.Rows.Add(new BaitMenuRow { Kind = BaitKind.Rare, ClickPoint = Point, Selected = Selected }); return frame;
                }, delegate(Point point) { Aims++; Target = point; }, delegate(double now) { return true; },
                delegate(Point point) { Clicks++; if (ThrowClick) throw new InvalidOperationException("Synthetic input rejection"); }, delegate { Releases++; }, 10);
            }
        }
    }
}
