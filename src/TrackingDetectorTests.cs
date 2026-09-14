using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SomeFishingGPO
{
    internal static class TrackingDetectorTests
    {
        private static Bitmap Sample(int left = 140, bool marker = true)
        {
            var image = new Bitmap(360, 400, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.FromArgb(85, 170, 255));
                using (var gray = new SolidBrush(Color.FromArgb(25, 25, 25))) graphics.FillRectangle(gray, left, 30, 22, 330);
                using (var cyan = new SolidBrush(Color.FromArgb(85, 170, 255)))
                {
                    graphics.FillRectangle(cyan, left + 5, 34, 12, 116);
                    graphics.FillRectangle(cyan, left + 5, 210, 12, 146);
                }
                if (marker) graphics.FillRectangle(Brushes.White, left - 9, 170, 40, 3);
                // The progress meter has a similar vertical dark surround, but
                // its green fill must never participate in track detection.
                using (var gray = new SolidBrush(Color.FromArgb(25, 25, 25))) graphics.FillRectangle(gray, left + 40, 55, 20, 290);
                graphics.FillRectangle(Brushes.Lime, left + 45, 220, 10, 120);
            }
            return image;
        }
        internal static void Run(Action<bool, string> check)
        {
            var options = new Settings();
            using (Bitmap image = Sample())
            {
                Observation seen = TrackingDetector.Analyze(image, options);
                check(seen.Found && seen.MenuVisible && Math.Abs(seen.FishY - 171) <= 1 && Math.Abs(seen.GapY - 179.5) <= 2,
                    "Geometry detector finds the thin fish line and controlled gray gap despite cyan ocean and green progress");
                check(seen.BarBounds.Left >= 136 && seen.BarBounds.Right <= 166 && seen.BarBounds.Top >= 25 && seen.BarBounds.Bottom <= 365,
                    "Detected bounds describe the actual blue track rather than the full selected image height");
            }
            foreach (int offset in new[] { 35, 80, 140, 220, 265 })
            using (Bitmap image = Sample(offset))
            {
                Observation seen = TrackingDetector.Analyze(image, options);
                check(seen.Found && Math.Abs(seen.FishY - 171) <= 1 && seen.BarBounds.Left >= offset - 4 && seen.BarBounds.Right <= offset + 26,
                    "The detector reacquires a sideways-panning blue track at x=" + offset);
            }
            foreach (bool top in new[] { true, false })
            using (Bitmap image = Sample())
            using (Graphics graphics = Graphics.FromImage(image))
            {
                using (var cyan = new SolidBrush(Color.FromArgb(85, 170, 255))) graphics.FillRectangle(cyan, 145, 34, 12, 322);
                using (var gray = new SolidBrush(Color.FromArgb(25, 25, 25))) graphics.FillRectangle(gray, 145, top ? 30 : 300, 12, 60);
                graphics.FillRectangle(Brushes.White, 131, 170, 40, 3);
                Observation seen = TrackingDetector.Analyze(image, options);
                check(seen.Found && Math.Abs(seen.GapY - (top ? 59.5 : 329.5)) <= 2,
                    "The controlled gray gap remains detectable at the " + (top ? "top" : "bottom") + " track endpoint");
            }
            foreach (int offset in new[] { 20, 70, 125 })
            using (var image = new Bitmap(320, 340, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.FromArgb(44, 113, 180));
                using (var gray = new SolidBrush(Color.FromArgb(25, 25, 25))) graphics.FillRectangle(gray, offset, 0, 72, 340);
                using (var cyan = new SolidBrush(Color.FromArgb(85, 170, 255)))
                { graphics.FillRectangle(cyan, offset + 23, 12, 26, 102); graphics.FillRectangle(cyan, offset + 23, 177, 26, 152); }
                graphics.FillRectangle(Brushes.White, offset + 9, 241, 54, 3);
                Observation seen = TrackingDetector.Analyze(image, options);
                check(seen.Found && Math.Abs(seen.FishY - 242) < 2 && Math.Abs(seen.GapY - 145) < 2,
                    "The existing broad synthetic track geometry remains compatible at sideways offset " + offset);
            }
            using (Bitmap image = Sample())
            using (Graphics graphics = Graphics.FromImage(image))
            {
                // Nearby prompt lettering crosses the dark borders, while the
                // fishing bar is composited above it, as in the supplied image.
                graphics.FillRectangle(Brushes.White, 123, 120, 22, 16);
                graphics.FillRectangle(Brushes.White, 157, 120, 33, 16);
                Observation seen = TrackingDetector.Analyze(image, options);
                check(seen.Found && Math.Abs(seen.FishY - 171) <= 1,
                    "White E-prompt lettering outside the narrow cyan interior cannot replace the actual fish line");
            }
            using (Bitmap image = Sample())
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.FillRectangle(Brushes.White, 120, 80, 65, 28);
                Observation seen = TrackingDetector.Analyze(image, options);
                check(seen.Found && Math.Abs(seen.FishY - 171) <= 1 && Math.Abs(seen.GapY - 179.5) <= 2,
                    "A thick text overlay obscuring a short blue section is neither a thin fish line nor a gray controlled gap");
            }
            using (Bitmap image = Sample(140, false))
            using (Graphics graphics = Graphics.FromImage(image))
            using (var font = new Font("Arial", 13, FontStyle.Bold))
            {
                graphics.DrawString("E   Cebo de Pescado", font, Brushes.White, 5, 115);
                Observation seen = TrackingDetector.Analyze(image, options);
                check(seen.MenuVisible && !seen.Found,
                    "A visible track with no fish marker remains partial instead of following unrelated white prompt text");
            }
            using (Bitmap image = Sample())
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.FillRectangle(Brushes.White, 131, 235, 40, 3);
                Observation seen = TrackingDetector.Analyze(image, options);
                check(seen.MenuVisible && !seen.Found,
                    "Two plausible thin white lines are ambiguous and cannot silently select a wrong fish marker");
            }
            using (var image = new Bitmap(360, 400))
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.FromArgb(85, 170, 255));
                check(!TrackingDetector.Analyze(image, options).MenuVisible, "Cyan ocean alone cannot create a vertical track");
                graphics.Clear(Color.FromArgb(25, 25, 25));
                check(!TrackingDetector.Analyze(image, options).MenuVisible, "A neutral dark rectangle alone cannot create a track");
                graphics.Clear(Color.FromArgb(85, 170, 255));
                using (var gray = new SolidBrush(Color.FromArgb(25, 25, 25))) graphics.FillRectangle(gray, 140, 30, 22, 330);
                graphics.FillRectangle(Brushes.Lime, 145, 80, 12, 150);
                graphics.FillRectangle(Brushes.White, 131, 170, 40, 3);
                check(!TrackingDetector.Analyze(image, options).MenuVisible,
                    "A green progress meter with a white highlight still cannot count as a blue fishing track");
                options.BlueArgb = Color.Lime.ToArgb(); options.Tolerance = 90;
                check(!TrackingDetector.Analyze(image, options).MenuVisible,
                    "Green progress is excluded even when green was accidentally picked as the cyan target color");
                options = new Settings();
            }
            using (Bitmap left = Sample(35))
            using (Bitmap right = Sample(220))
            using (Graphics graphics = Graphics.FromImage(left))
            {
                graphics.DrawImage(right, new Rectangle(205, 0, 140, 400), new Rectangle(205, 0, 140, 400), GraphicsUnit.Pixel);
                Observation seen = TrackingDetector.Analyze(left, options);
                check(seen.MenuVisible && !seen.Found,
                    "Two real fishing tracks in the selected area are rejected instead of choosing one by score");
            }
            using (Bitmap source = Sample())
            using (var padded = new Bitmap(1200, 1400))
            using (Graphics graphics = Graphics.FromImage(padded))
            {
                graphics.Clear(Color.FromArgb(85, 170, 255)); graphics.DrawImageUnscaled(source, 130, 500);
                Observation seen = TrackingDetector.Analyze(padded, options);
                check(seen.Found && Math.Abs(seen.FishY - 671) <= 1 && Math.Abs(seen.GapY - 679.5) <= 2,
                    "Large vertical padding does not make a genuine track disappear through a whole-image dark-column threshold");
            }
            using (Bitmap source = Sample())
            using (var scaled = new Bitmap(720, 800))
            using (Graphics graphics = Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor; graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(source, new Rectangle(0, 0, 720, 800));
                Observation seen = TrackingDetector.Analyze(scaled, options);
                check(seen.Found && Math.Abs(seen.FishY - 342.5) <= 2 && Math.Abs(seen.GapY - 359.5) <= 3,
                    "A scaled track preserves the physical marker and gap centers instead of using fixed pixel positions");
            }
            var tracker = new StatefulTrackingDetector();
            using (Bitmap image = Sample(80)) check(tracker.Analyze(image, options).Found, "Stateful tracking learns a confirmed track box");
            using (Bitmap image = Sample(95, false))
            {
                Observation seen = tracker.Analyze(image, options);
                check(seen.MenuVisible && !seen.Found, "Missing marker evidence retains only current visible-menu evidence, never a cached found position");
            }
            using (Bitmap image = Sample(115)) check(tracker.Analyze(image, options).Found, "Current evidence reacquires a panned marker after a partial frame");
            using (var empty = new Bitmap(360, 400))
                check(!tracker.Analyze(empty, options).MenuVisible, "A closed menu is not kept visible solely because a previous track box was remembered");
            tracker.Reset();
            using (Bitmap image = Sample(200)) check(tracker.Analyze(image, options).Found, "Reset permits clean acquisition at a new track location");
        }
        internal static void CheckSuppliedOverlay(Action<bool, string> check, string path)
        {
            using (var image = new Bitmap(path))
            {
                Observation seen = TrackingDetector.Analyze(image, new Settings());
                check(seen.Found && Math.Abs(seen.FishY - 277) <= 2 && seen.GapTop >= 340 && seen.GapBottom <= 409,
                    "The supplied E-prompt screenshot resolves the actual white marker and bottom gray gap from the full frame");
                using (Bitmap crop = image.Clone(new Rectangle(312, 135, 100, 290), PixelFormat.Format32bppArgb))
                {
                    seen = TrackingDetector.Analyze(crop, new Settings());
                    check(seen.Found && Math.Abs(seen.FishY + 135 - 277) <= 2,
                        "The same supplied prompt screenshot works in a selected fishing rectangle with both bars and sideways margin");
                }
                using (Bitmap crop = image.Clone(new Rectangle(374, 174, 28, 220), PixelFormat.Format32bppArgb))
                    check(!TrackingDetector.Analyze(crop, new Settings()).MenuVisible,
                        "The real supplied green progress meter alone does not become a fishing track");
                int falseMenus = 0, inspected = 0;
                for (int top = 0; top + 220 <= image.Height; top += 110)
                for (int left = 0; left + 100 <= image.Width; left += 100)
                {
                    var region = new Rectangle(left, top, 100, 220);
                    if (region.IntersectsWith(new Rectangle(334, 140, 28, 272))) continue;
                    using (Bitmap crop = image.Clone(region, PixelFormat.Format32bppArgb))
                    { if (TrackingDetector.Analyze(crop, new Settings()).MenuVisible) falseMenus++; inspected++; }
                }
                check(inspected > 10 && falseMenus == 0,
                    "Supplied dock, avatar, prompt and bait-menu crops outside the fishing track do not create false visible menus");
            }
        }
    }
}
