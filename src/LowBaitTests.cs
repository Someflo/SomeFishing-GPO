using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;

namespace SomeFishingGPO
{
    internal static class LowBaitTests
    {
        internal static void CheckSamples(Action<bool, string> check, string pathThree, string pathFour)
        {
            var languages = new List<string> { "" };
            foreach (var language in WindowsBaitReader.Languages()) languages.Add(language.Key);
            string[] paths = { pathThree, pathFour };
            for (int index = 0; index < paths.Length; index++)
            using (var image = new Bitmap(paths[index]))
            {
                int expected = index + 3;
                check(CounterGlyphs.MatchLow(image) == expected, "The supplied complete x" + expected + " image matches its own low-count shape");
                foreach (string language in languages)
                    check(WindowsBaitReader.ReadImage(image, language).Count == expected, "The supplied x" + expected + " image is readable with " + (language.Length == 0 ? "automatic OCR language" : language));
                foreach (double scale in new[] { .8, 1.0, 1.25, 1.5, 2.0, 3.0 })
                using (var resized = new Bitmap((int)Math.Round(image.Width * scale), (int)Math.Round(image.Height * scale)))
                {
                    using (var graphics = Graphics.FromImage(resized))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(image, new Rectangle(0, 0, resized.Width, resized.Height));
                    }
                    int? result = CounterGlyphs.MatchLow(resized);
                    check(!result.HasValue || result == expected, "Rescaled x" + expected + " is correct or unknown, never another low count at scale " + scale);
                }
                using (var damaged = new Bitmap(image))
                {
                    using (var graphics = Graphics.FromImage(damaged)) graphics.FillRectangle(Brushes.Black, 0, 0, image.Width / 2, image.Height);
                    check(!CounterGlyphs.MatchLow(damaged).HasValue, "The supplied x" + expected + " image cannot match when its x prefix is hidden");
                }
                using (var recolored = new Bitmap(image))
                {
                    for (int y = 0; y < recolored.Height; y++) for (int x = 0; x < recolored.Width; x++)
                    {
                        Color color = recolored.GetPixel(x, y);
                        if (color.R > 140 && color.G > 75 && color.R - color.G > 20 && color.G - color.B > 35) recolored.SetPixel(x, y, Color.LimeGreen);
                    }
                    check(!CounterGlyphs.MatchLow(recolored).HasValue, "The supplied x" + expected + " shape in green cannot become a yellow bait reading");
                }
            }
        }
        internal static void Run(Action<bool, string> check, string output)
        {
            var monitor = new BaitMonitor(); monitor.Reset();
            monitor.Update(new BaitReading { Count = 4, Sequence = 1, SampledAt = 0 }, 0);
            check(!monitor.NearEmpty && !monitor.Count.HasValue, "A single four-bait frame cannot announce a confirmed low count");
            monitor.Update(new BaitReading { Count = 4, Sequence = 2, SampledAt = 1500 }, 1500);
            check(monitor.NearEmpty && monitor.Count == 4 && !monitor.Empty, "Four bait is confirmed as approaching empty, never empty");
            check(monitor.Detail.Contains("4 cebos"), "The confirmed low-count detail includes four bait");
            monitor.Update(new BaitReading { Count = 3, Sequence = 3, SampledAt = 3000 }, 3000);
            check(!monitor.NearEmpty && !monitor.Count.HasValue, "A changed low count invalidates the previous four until confirmed");
            monitor.Update(new BaitReading { Count = 3, Sequence = 4, SampledAt = 4500 }, 4500);
            check(monitor.NearEmpty && monitor.Count == 3 && !monitor.Empty, "Three bait is an early indication, not an empty counter");
            monitor.Update(new BaitReading { Count = 3, Sequence = 4, SampledAt = 4500 }, 9600);
            check(!monitor.NearEmpty && !monitor.Count.HasValue, "Stale low readings cannot keep the early indication active");
            monitor.Reset();
            monitor.Update(new BaitReading { Count = 2, Sequence = 1, SampledAt = 0 }, 0);
            monitor.Update(new BaitReading { Count = 2, Sequence = 2, SampledAt = 1500 }, 1500);
            check(monitor.Count == 2 && !monitor.NearEmpty, "Two bait remains a distinct purchase threshold, not the early indication");
            monitor.Reset();
            check(!monitor.NearEmpty, "Reset clears the early low-bait indication");
            check(BaitText.Parse("x3") == 3 && BaitText.Parse("x4") == 4, "Counter text accepts the complete x3 and x4 values");

            using (var blank = new Bitmap(48, 40))
            {
                check(!CounterGlyphs.MatchLow(blank).HasValue, "No text is not a low-bait value");
                using (var graphics = Graphics.FromImage(blank)) graphics.Clear(Color.Gold);
                check(!CounterGlyphs.MatchLow(blank).HasValue, "A gold block is not a complete low-bait label");
            }
            foreach (string fontName in new[] { "Arial", "Segoe UI", "Comic Sans MS" })
            foreach (int fontSize in new[] { 10, 14, 20 })
            foreach (string label in new[] { "x0", "x1", "x5", "x6", "x7", "x8", "x9", "x20", "x23", "x24", "x32", "x42", "x300", "3", "4", "x", "xO", "xB", "xA" })
            using (var image = new Bitmap(180, 60))
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font(fontName, fontSize, FontStyle.Bold))
            {
                graphics.Clear(Color.FromArgb(40, 40, 40));
                graphics.DrawString(label, font, Brushes.Orange, new PointF(8, 8));
                check(!CounterGlyphs.MatchLow(image).HasValue, "Low-count fallback rejects unrelated label " + label + " in " + fontName + " " + fontSize);
            }
            foreach (int value in new[] { 2, 3, 4 })
            using (var image = new Bitmap(80, 48))
            using (var graphics = Graphics.FromImage(image))
            using (var font = new Font("Arial", 18, FontStyle.Bold))
            {
                graphics.Clear(Color.FromArgb(40, 40, 40));
                graphics.DrawString("x" + value, font, Brushes.Orange, new PointF(8, 8));
                int? result = CounterGlyphs.MatchLow(image);
                check(!result.HasValue || result == value, "Unfamiliar lettering for x" + value + " is unknown or correct, never another low count");
                graphics.Clear(Color.FromArgb(40, 40, 40));
                graphics.DrawString("x" + value, font, Brushes.LimeGreen, new PointF(8, 8));
                check(!CounterGlyphs.MatchLow(image).HasValue, "Green progress text cannot become the yellow x" + value + " counter");
            }
        }
    }
}
