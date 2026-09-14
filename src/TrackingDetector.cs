using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SomeFishingGPO
{
    // Current-frame geometry owns every returned position. The remembered box is
    // only a search window; stale positions are never reported as a found fish.
    public sealed class StatefulTrackingDetector
    {
        private Rectangle previous;
        private Size size;
        private int blue, marker, tolerance;
        public void Reset() { previous = Rectangle.Empty; size = Size.Empty; }
        public Observation Analyze(Bitmap image, Settings settings)
        {
            if (image == null || settings == null) throw new ArgumentNullException("image/settings");
            if (size != image.Size || blue != settings.BlueArgb || marker != settings.MarkerArgb || tolerance != settings.Tolerance) Reset();
            size = image.Size; blue = settings.BlueArgb; marker = settings.MarkerArgb; tolerance = settings.Tolerance;
            Observation seen = TrackingDetector.Analyze(image, settings);
            if (seen.Found) { previous = seen.BarBounds; return seen; }
            if (seen.MenuVisible || previous.IsEmpty) return seen;
            Rectangle search = previous; search.Inflate(Math.Max(20, previous.Width), 12);
            search.Intersect(new Rectangle(Point.Empty, image.Size));
            if (search.Width < 8 || search.Height < 50) return seen;
            using (Bitmap crop = image.Clone(search, PixelFormat.Format32bppArgb))
            {
                Observation local = TrackingDetector.Analyze(crop, settings);
                if (!local.MenuVisible) return seen;
                local.BarBounds.Offset(search.Location);
                if (local.Found)
                {
                    local.FishY += search.Y; local.GapY += search.Y; local.GapTop += search.Y; local.GapBottom += search.Y;
                    previous = local.BarBounds;
                }
                return local;
            }
        }
    }

    // Searches for cyan supported by paired vertical neutral borders. Foreground
    // text outside that narrow interior cannot become the fish marker, and the
    // green progress meter never supplies cyan, gray or white evidence.
    public static class TrackingDetector
    {
        private const byte Cyan = 1, Dark = 2, White = 4;
        private sealed class Pixels
        {
            internal readonly int Width, Height;
            internal readonly byte[] Mask;
            internal readonly int[] BlueColumns, DarkColumns;
            internal Pixels(Bitmap image, Settings settings)
            {
                Width = image.Width; Height = image.Height; Mask = new byte[Width * Height];
                BlueColumns = new int[Width]; DarkColumns = new int[Width];
                Color cyan = Color.FromArgb(settings.BlueArgb), white = Color.FromArgb(settings.MarkerArgb);
                BitmapData data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] row = new byte[Math.Abs(data.Stride)];
                    for (int y = 0; y < Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                        for (int x = 0; x < Width; x++)
                        {
                            int i = x * 4, b = row[i], g = row[i + 1], r = row[i + 2];
                            if (g > 70 && g > r + 18 && g > b + 18) continue;
                            byte mask = 0;
                            if (Match(r, g, b, cyan, settings.Tolerance)) { mask |= Cyan; BlueColumns[x]++; }
                            int high = Math.Max(r, Math.Max(g, b)), low = Math.Min(r, Math.Min(g, b));
                            if (low >= 12 && high <= 55 && high - low <= 20) { mask |= Dark; DarkColumns[x]++; }
                            if (Match(r, g, b, white, settings.Tolerance)) mask |= White;
                            Mask[y * Width + x] = mask;
                        }
                    }
                }
                finally { image.UnlockBits(data); }
            }
            internal bool Is(int x, int y, byte kind) { return x >= 0 && x < Width && y >= 0 && y < Height && (Mask[y * Width + x] & kind) != 0; }
            internal bool Edge(int x, int y) { return Is(x - 1, y, Dark) || Is(x, y, Dark) || Is(x + 1, y, Dark); }
        }
        private sealed class Candidate { internal Observation Seen; internal double Score; }
        public static Observation Analyze(Bitmap image, Settings settings)
        {
            if (image == null || settings == null) throw new ArgumentNullException("image/settings");
            if (image.Width < 8 || image.Height < 50 || image.Width > 1800 || image.Height > 1800)
                return new Observation();
            var pixels = new Pixels(image, settings);
            var candidates = new List<Candidate>();
            var pairs = new HashSet<long>();
            for (int center = 2; center < image.Width - 2; center++)
            {
                if (pixels.BlueColumns[center] < 30) continue;
                int left = Border(pixels, center, -1), right = Border(pixels, center, 1);
                if (left < 0 || right < 0 || right - left < 4 || right - left > 120) continue;
                long key = (long)left * image.Width + right; if (!pairs.Add(key)) continue;
                Candidate candidate = AnalyzeTrack(pixels, left, right);
                if (candidate == null) continue;
                bool merged = false;
                for (int i = 0; i < candidates.Count; i++)
                {
                    Rectangle a = candidates[i].Seen.BarBounds, b = candidate.Seen.BarBounds, intersection = Rectangle.Intersect(a, b);
                    if (intersection.Width <= 0 || intersection.Height <= 0 || intersection.Height < Math.Min(a.Height, b.Height) * .65) continue;
                    if (Math.Abs((a.Left + a.Right) - (b.Left + b.Right)) > Math.Max(a.Width, b.Width)) continue;
                    if (candidate.Score > candidates[i].Score) candidates[i] = candidate;
                    merged = true; break;
                }
                if (!merged) candidates.Add(candidate);
            }
            Candidate best = null;
            foreach (Candidate candidate in candidates)
            {
                if (best == null || (candidate.Seen.Found && !best.Seen.Found) || candidate.Seen.Found == best.Seen.Found && candidate.Score > best.Score) best = candidate;
            }
            if (best == null) return new Observation { Detail = "Buscando azul entre dos bordes verticales" };
            foreach (Candidate candidate in candidates)
                if (candidate != best && candidate.Seen.Found && best.Seen.Found)
                    return new Observation { MenuVisible = true, Detail = "Veo más de una barra azul. Reduce la zona para incluir un solo minijuego." };
            return best.Seen;
        }
        private static int Border(Pixels p, int center, int direction)
        {
            int minimumDifference = Math.Max(16, p.Height / 40), minimumDarkDifference = Math.Max(8, p.Height / 70);
            for (int distance = 2; distance <= 60; distance++)
            {
                int x = center + distance * direction; if (x < 0 || x >= p.Width) break;
                if (p.DarkColumns[x] >= Math.Max(35, p.DarkColumns[center] + minimumDarkDifference)
                    && p.BlueColumns[x] <= p.BlueColumns[center] - minimumDifference) return x;
            }
            return -1;
        }
        private static Candidate AnalyzeTrack(Pixels p, int left, int right)
        {
            int inner = right - left - 1;
            int requiredBlue = Math.Max(2, (int)Math.Ceiling(inner * .40)), requiredDark = Math.Max(2, (int)Math.Ceiling(inner * .55));
            int[] blue = new int[p.Height], dark = new int[p.Height], white = new int[p.Height];
            bool[] supported = new bool[p.Height], strong = new bool[p.Height];
            int maxBlueColumn = 0;
            for (int x = left + 1; x < right; x++) maxBlueColumn = Math.Max(maxBlueColumn, p.BlueColumns[x]);
            for (int y = 0; y < p.Height; y++)
            {
                for (int x = left + 1; x < right; x++)
                { if (p.Is(x, y, Cyan)) blue[y]++; if (p.Is(x, y, Dark)) dark[y]++; if (p.Is(x, y, White)) white[y]++; }
                bool l = p.Edge(left, y), r = p.Edge(right, y), fill = blue[y] >= requiredBlue || dark[y] >= requiredDark;
                strong[y] = l && r && fill;
                supported[y] = strong[y] || (l || r) && blue[y] >= requiredBlue;
            }
            int allowedHole = Math.Max(12, Math.Min(35, maxBlueColumn / 5));
            int first = -1, last = -1, bestFirst = -1, bestLast = -1, count = 0, bestCount = 0;
            for (int y = 0; y <= p.Height; y++)
            {
                if (y < p.Height && supported[y])
                { if (first < 0) first = y; last = y; count++; }
                if (first >= 0 && (y == p.Height || y - last > allowedHole))
                { if (count > bestCount) { bestFirst = first; bestLast = last; bestCount = count; } first = last = -1; count = 0; }
            }
            if (bestFirst < 0) return null;
            int span = bestLast - bestFirst + 1, blueRows = 0, strongRows = 0;
            for (int y = bestFirst; y <= bestLast; y++) { if (blue[y] >= requiredBlue) blueRows++; if (strong[y]) strongRows++; }
            if (span < Math.Max(50, inner * 4) || blueRows < Math.Max(25, span * .18) || bestCount < span * .68 || strongRows < span * .42) return null;
            var seen = new Observation { MenuVisible = true, BarBounds = Rectangle.FromLTRB(Math.Max(0, left - 2), bestFirst, Math.Min(p.Width, right + 3), bestLast + 1),
                Detail = "Barra visible; esperando hueco y línea blanca" };
            var candidate = new Candidate { Seen = seen, Score = strongRows + blueRows * 2.0 - inner * .1 };

            bool[] markerRows = new bool[p.Height];
            int minWhite = Math.Max(2, (int)Math.Ceiling(inner * .70));
            int markerStart = -1, markerEnd = -1, run = -1, markers = 0;
            int maxThickness = Math.Max(4, Math.Min(8, span / 40));
            for (int y = Math.Max(0, bestFirst - 4); y <= Math.Min(p.Height - 1, bestLast + 4) + 1; y++)
            {
                bool line = false;
                if (y < p.Height && white[y] >= minWhite)
                {
                    int extent = Math.Max(3, inner / 2), firstWhite = -1, lastWhite = -1, longest = 0, current = 0;
                    for (int x = Math.Max(0, left - extent); x <= Math.Min(p.Width - 1, right + extent); x++)
                    {
                        if (p.Is(x, y, White)) { if (firstWhite < 0) firstWhite = x; lastWhite = x; current++; longest = Math.Max(longest, current); }
                        else current = 0;
                    }
                    line = longest >= Math.Max(4, inner) && firstWhite <= left + 1 && lastWhite >= right - 1;
                }
                if (line) { markerRows[y] = true; if (run < 0) run = y; }
                else if (run >= 0)
                {
                    if (y - run <= maxThickness) { markerStart = run; markerEnd = y - 1; markers++; }
                    else for (int row = run; row < y; row++) markerRows[row] = false;
                    run = -1;
                }
            }
            if (markers != 1)
            { seen.Detail = markers > 1 ? "Barra visible; hay varias líneas blancas posibles" : "Barra visible; línea blanca tapada o incompleta"; return candidate; }

            int gapStart = -1, gapEnd = -1, gapRun = -1;
            for (int y = bestFirst; y <= bestLast + 1; y++)
            {
                bool gray = y <= bestLast && blue[y] < requiredBlue && (dark[y] >= requiredDark || markerRows[y]);
                if (gray && gapRun < 0) gapRun = y;
                if (!gray && gapRun >= 0)
                { if (y - gapRun > gapEnd - gapStart + 1) { gapStart = gapRun; gapEnd = y - 1; } gapRun = -1; }
            }
            int gapSize = gapEnd - gapStart + 1;
            if (gapStart < 0 || gapSize < Math.Max(6, span / 50) || gapSize > span * .65)
            { seen.Detail = "Barra visible; hueco gris tapado o incompleto"; return candidate; }
            int grayRows = 0;
            for (int y = gapStart; y <= gapEnd; y++) if (dark[y] >= requiredDark) grayRows++;
            if (grayRows < gapSize * .70) return candidate;
            seen.Found = true; seen.FishY = (markerStart + markerEnd) / 2.0;
            seen.GapTop = gapStart; seen.GapBottom = gapEnd; seen.GapY = (gapStart + gapEnd) / 2.0;
            seen.Detail = "Pez y hueco detectados entre los bordes de la barra";
            return candidate;
        }
        private static bool Match(int r, int g, int b, Color target, int tolerance)
        { return Math.Abs(r - target.R) <= tolerance && Math.Abs(g - target.G) <= tolerance && Math.Abs(b - target.B) <= tolerance; }
    }
}
