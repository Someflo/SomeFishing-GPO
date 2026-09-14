using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SomeFishingGPO
{
    public interface IBaitSelectionRuntime
    {
        void BeginBaitSelection(BaitKind kind, double now);
        BaitSelectionResult TickBaitSelection(double now);
    }

    public sealed class BaitSelectionResult
    {
        public bool Completed, Succeeded;
        public string Status = "Esperando seleccionar el cebo";
    }

    public sealed class BaitMenuRow
    {
        public BaitKind Kind;
        public Rectangle Bounds, CounterBounds;
        public Point ClickPoint;
        public bool Selected;
    }

    public sealed class BaitMenuReading
    {
        public readonly List<BaitMenuRow> Rows = new List<BaitMenuRow>();
        public long Sequence;
        public double SampledAt;
        public string Detail = "No se distinguen las filas de cebo";
        public bool TryGetRow(BaitKind kind, out BaitMenuRow row)
        {
            row = null;
            foreach (BaitMenuRow candidate in Rows)
            {
                if (candidate.Kind != kind) continue;
                // Two matches are ambiguous, never choose the first by position.
                if (row != null) { row = null; return false; }
                row = candidate;
            }
            return row != null;
        }
    }

    // Locates the label and its own yellow counter together. Positions are
    // measured anew on every image; disappearing rows never shift a saved index.
    // This is colour/shape detection only and does not read or infer quantities.
    public static class BaitMenuVisual
    {
        private sealed class Pixels
        {
            internal readonly int Width, Height;
            private readonly int[] values;
            internal Pixels(Bitmap image)
            {
                Width = image.Width; Height = image.Height; values = new int[checked(Width * Height)];
                Bitmap bitmap = image;
                if (image.PixelFormat != PixelFormat.Format32bppArgb)
                {
                    bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
                    using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(image, Point.Empty);
                }
                try
                {
                    BitmapData data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try { for (int y = 0; y < Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, checked(y * data.Stride)), values, y * Width, Width); }
                    finally { bitmap.UnlockBits(data); }
                }
                finally { if (!object.ReferenceEquals(bitmap, image)) bitmap.Dispose(); }
            }
            internal Color GetPixel(int x, int y) { return Color.FromArgb(values[y * Width + x]); }
        }
        private sealed class Band { internal int Top, Bottom; }
        private sealed class Ink
        {
            internal int Count, Left = int.MaxValue, Top = int.MaxValue, Right = -1, Bottom = -1;
            internal void Add(int x, int y) { Count++; Left = Math.Min(Left, x); Right = Math.Max(Right, x); Top = Math.Min(Top, y); Bottom = Math.Max(Bottom, y); }
            internal int Width { get { return Right - Left + 1; } }
            internal int Height { get { return Bottom - Top + 1; } }
            internal bool TextLike { get { return Count >= 18 && Width >= 24 && Height >= 5 && Count < Width * Height * .80; } }
        }
        private static bool Yellow(Color p)
        { return p.R >= 165 && p.G >= 95 && p.B <= 110 && p.R - p.B >= 95 && p.G - p.B >= 50 && p.R >= p.G * .88; }
        private static int LabelColour(Color p)
        {
            if (Yellow(p)) return 0;
            int hi = Math.Max(p.R, Math.Max(p.G, p.B)), lo = Math.Min(p.R, Math.Min(p.G, p.B));
            if (lo >= 165 && hi - lo <= 65) return 1; // common: white
            if (p.G >= 145 && p.B >= 145 && Math.Min(p.G, p.B) >= p.R + 38) return 2; // rare: cyan
            if ((p.R >= 145 && p.R >= p.G + 65 && p.R >= p.B + 40)
                || (p.B >= 140 && p.R >= 90 && p.G <= Math.Min(p.R, p.B) - 45)) return 3; // red/magenta/purple
            if ((p.B >= 150 && p.B >= p.R + 65 && p.B >= p.G + 45)
                || (p.G >= 155 && p.G >= p.R + 40 && p.G >= p.B + 60)) return 4; // blue/green
            return 0;
        }
        public static BaitMenuReading Analyze(Bitmap image) { return Analyze(image, Point.Empty); }
        public static BaitMenuReading Analyze(Bitmap image, Point origin)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (image.Width < 60 || image.Height < 16 || image.Width > 1600 || image.Height > 1400) return new BaitMenuReading();
            return AnalyzePixels(new Pixels(image), origin);
        }
        private static BaitMenuReading AnalyzePixels(Pixels image, Point origin)
        {
            var result = new BaitMenuReading();
            var borderRows = new bool[image.Height];
            for (int y = 0; y < image.Height; y++)
            {
                int gold = 0;
                for (int x = 0; x < image.Width; x++) if (Yellow(image.GetPixel(x, y))) gold++;
                borderRows[y] = gold >= Math.Max(45, image.Width * .48);
            }
            // Right-hand yellow glyph rows seed the search. A title has no
            // counter; a gold instruction line has no white/cyan/rainbow label.
            var bands = new List<Band>(); Band current = null; int lastInk = -100;
            for (int y = 0; y < image.Height; y++)
            {
                int n = 0;
                for (int x = image.Width / 2; x < image.Width; x++) if (Yellow(image.GetPixel(x, y))) n++;
                if (n < 2) continue;
                if (current == null || y - lastInk > 3) { current = new Band { Top = y, Bottom = y }; bands.Add(current); }
                current.Bottom = y; lastInk = y;
            }
            foreach (Band band in bands)
            {
                if (band.Bottom - band.Top < 4 || band.Bottom - band.Top > 100) continue;
                // Collapse vertical gold border pixels and wide horizontal lines
                // before finding the rightmost group of actual counter glyphs.
                var columns = new List<Band>(); Band group = null; int lastX = -100;
                for (int x = image.Width / 2; x < image.Width; x++)
                {
                    int n = 0;
                    for (int y = band.Top; y <= band.Bottom; y++) if (!borderRows[y] && Yellow(image.GetPixel(x, y))) n++;
                    if (n < 2 || (n >= 15 && n >= (band.Bottom - band.Top + 1) * .75)) continue;
                    if (group == null || x - lastX > Math.Max(5, (band.Bottom - band.Top) / 2)) { group = new Band { Top = x, Bottom = x }; columns.Add(group); }
                    group.Bottom = x; lastX = x;
                }
                if (columns.Count == 0) continue;
                Band counterX = columns[columns.Count - 1];
                // Include solid vertical strokes immediately adjacent to a glyph.
                int counterLeft = Math.Max(image.Width / 2, counterX.Top - 2), counterRight = Math.Min(image.Width - 1, counterX.Bottom + 2);
                var counter = new Ink();
                for (int y = band.Top; y <= band.Bottom; y++) for (int x = counterLeft; x <= counterRight; x++)
                    if (!borderRows[y] && Yellow(image.GetPixel(x, y))) counter.Add(x, y);
                if (counter.Count < 12 || counter.Width < 6 || counter.Width > image.Width * .35 || counter.Height < 5 || counter.Height > 80) continue;
                var all = new Ink(); var white = new Ink(); var cyan = new Ink(); var warm = new Ink(); var cool = new Ink();
                int labelRight = counter.Left - Math.Max(5, counter.Height / 2);
                int top = Math.Max(0, counter.Top - 3), bottom = Math.Min(image.Height - 1, counter.Bottom + 3);
                for (int y = top; y <= bottom; y++) for (int x = 0; x < labelRight; x++)
                {
                    int colour = LabelColour(image.GetPixel(x, y)); if (colour == 0) continue;
                    all.Add(x, y);
                    if (colour == 1) white.Add(x, y); else if (colour == 2) cyan.Add(x, y); else if (colour == 3) warm.Add(x, y); else cool.Add(x, y);
                }
                if (!all.TextLike || all.Width < counter.Height * 3 || all.Height > counter.Height * 2 + 4) continue;
                BaitKind kind;
                if (warm.Count >= 12 && cool.Count >= 12 && warm.Count + cool.Count >= all.Count * .30) kind = BaitKind.Legendary;
                else if (cyan.TextLike && cyan.Count >= all.Count * .60 && warm.Count + cool.Count <= all.Count * .10) kind = BaitKind.Rare;
                else if (white.TextLike && white.Count >= all.Count * .78 && cyan.Count + warm.Count + cool.Count <= all.Count * .15) kind = BaitKind.Common;
                else continue;
                // The selected item's gold outline may be joined into the broad
                // seed band. Rebuild the numeric bounds using the actual label
                // height and separated glyph groups, excluding that border.
                counter = RefineCounter(image, all, borderRows);
                if (counter == null) continue;
                Rectangle local = Rectangle.FromLTRB(Math.Max(0, all.Left - 7), Math.Max(0, Math.Min(all.Top, counter.Top) - 5),
                    Math.Min(image.Width, counter.Right + 7), Math.Min(image.Height, Math.Max(all.Bottom, counter.Bottom) + 6));
                var row = new BaitMenuRow { Kind = kind, Bounds = Offset(local, origin),
                    CounterBounds = Offset(Rectangle.FromLTRB(Math.Max(0, counter.Left - 3), Math.Max(0, counter.Top - 3),
                        Math.Min(image.Width, counter.Right + 4), Math.Min(image.Height, counter.Bottom + 4)), origin),
                    ClickPoint = new Point(checked(origin.X + (all.Left + all.Right) / 2), checked(origin.Y + (all.Top + all.Bottom) / 2)),
                    Selected = SelectedOutline(image, local, all.Height) };
                result.Rows.Add(row);
            }
            if (result.Rows.Count > 0) result.Detail = "Filas de cebo visibles: " + result.Rows.Count;
            return result;
        }
        private static Rectangle Offset(Rectangle value, Point origin)
        { return new Rectangle(checked(value.X + origin.X), checked(value.Y + origin.Y), value.Width, value.Height); }
        private static Ink RefineCounter(Pixels image, Ink label, bool[] borderRows)
        {
            int top = Math.Max(0, label.Top - Math.Max(2, label.Height / 3));
            int bottom = Math.Min(image.Height - 1, label.Bottom + Math.Max(2, label.Height / 2));
            int gapLimit = Math.Max(3, label.Height / 3), lastX = -100;
            var groups = new List<Band>(); Band group = null;
            for (int x = label.Right + Math.Max(3, label.Height / 2); x < image.Width; x++)
            {
                int n = 0;
                for (int y = top; y <= bottom; y++) if (!borderRows[y] && Yellow(image.GetPixel(x, y))) n++;
                // A side of the selection rectangle extends through almost the
                // entire text strip. A real glyph retains top/bottom margins.
                if (n == 0 || n >= (bottom - top + 1) * .85) continue;
                if (group == null || x - lastX > gapLimit) { group = new Band { Top = x, Bottom = x }; groups.Add(group); }
                group.Bottom = x; lastX = x;
            }
            Ink found = null;
            foreach (Band candidate in groups)
            {
                var ink = new Ink();
                for (int y = top; y <= bottom; y++) for (int x = candidate.Top; x <= candidate.Bottom; x++)
                    if (!borderRows[y] && Yellow(image.GetPixel(x, y))) ink.Add(x, y);
                bool edge = ink.Width < label.Height * 1.5 && ink.Height > label.Height
                    && ink.Top < label.Top - 1 && ink.Bottom > label.Bottom + 1;
                if (edge || ink.Count < 4 || ink.Height < 4 || ink.Height > label.Height * 1.7 || ink.Width > image.Width * .35)
                { if (found != null) break; continue; }
                if (found == null) found = ink;
                else
                {
                    // A narrow standalone digit (especially 1) may be separated
                    // from x. Keep it; the rejected tall side outline terminates
                    // the quantity rather than becoming another digit.
                    if (ink.Left - found.Right > Math.Max(8, label.Height * 1.5)) break;
                    found.Count += ink.Count; found.Left = Math.Min(found.Left, ink.Left); found.Right = Math.Max(found.Right, ink.Right);
                    found.Top = Math.Min(found.Top, ink.Top); found.Bottom = Math.Max(found.Bottom, ink.Bottom);
                }
            }
            return found != null && found.Count >= 12 && found.Width >= Math.Max(6, label.Height * .6)
                && found.Width <= image.Width * .35 ? found : null;
        }
        private static bool SelectedOutline(Pixels image, Rectangle row, int textHeight)
        {
            int left = Math.Max(0, row.Left - 20), right = Math.Min(image.Width - 1, row.Right + 12);
            int up = -1, down = -1, minimum = Math.Max(35, (int)(row.Width * .68));
            for (int y = Math.Max(0, row.Top - Math.Max(12, textHeight)); y <= Math.Min(image.Height - 1, row.Top + 3); y++)
                if (HorizontalGold(image, y, left, right) >= minimum) up = y;
            for (int y = Math.Max(0, row.Bottom - 4); y <= Math.Min(image.Height - 1, row.Bottom + Math.Max(12, textHeight)); y++)
                if (HorizontalGold(image, y, left, right) >= minimum) { down = y; break; }
            if (up < 0 || down < 0 || down - up < textHeight + 4) return false;
            // At least one joining side prevents two unrelated horizontal HUD
            // lines being mistaken for the selected item's rectangle.
            for (int x = left; x <= right; x++)
            {
                int n = 0;
                for (int y = up; y <= down; y++) if (Yellow(image.GetPixel(x, y))) n++;
                if (n >= (down - up + 1) * .55) return true;
            }
            return false;
        }
        private static int HorizontalGold(Pixels image, int y, int left, int right)
        {
            int n = 0, best = 0, gap = 0;
            for (int x = left; x <= right; x++)
            {
                if (Yellow(image.GetPixel(x, y))) { n++; gap = 0; best = Math.Max(best, n); }
                else if (++gap > 3) { n = 0; gap = 0; }
            }
            return best;
        }
    }

    // A small nonblocking selection transaction. All effects are supplied by the
    // guarded runtime, so fake runtimes can exercise interruption and stale frames.
    public sealed class BaitSelectionController
    {
        private readonly Func<bool> allowed;
        private readonly Func<double, BaitMenuReading> read;
        private readonly Action<Point> aim, click;
        private readonly Func<double, bool> tickAim;
        private readonly Action release;
        private readonly double timeout;
        private BaitSelectionResult result = new BaitSelectionResult { Completed = true };
        private BaitKind kind;
        private Point target;
        private long sequence = -1;
        private double startedAt, lastNow, stableAt, arrivedAt, clickedAt;
        private int phase, stableFrames, selectedFrames, attempts;
        public BaitSelectionController(Func<bool> allowed, Func<double, BaitMenuReading> read, Action<Point> aim,
            Func<double, bool> tickAim, Action<Point> click, Action release, int timeoutSeconds)
        {
            if (allowed == null || read == null || aim == null || tickAim == null || click == null || release == null) throw new ArgumentNullException("runtime");
            this.allowed = allowed; this.read = read; this.aim = aim; this.tickAim = tickAim; this.click = click; this.release = release;
            timeout = Math.Max(3, Math.Min(60, timeoutSeconds)) * 1000;
        }
        public void Begin(BaitKind value, double now)
        {
            Cancel();
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0) throw new ArgumentOutOfRangeException("now");
            if (!allowed()) throw new InvalidOperationException("Roblox perdió el foco antes de seleccionar el cebo.");
            kind = value; startedAt = lastNow = now; phase = stableFrames = selectedFrames = attempts = 0;
            sequence = -1; arrivedAt = -1;
            result = new BaitSelectionResult { Status = "Buscando el cebo " + Name(kind) };
        }
        public BaitSelectionResult Tick(double now)
        {
            if (result.Completed) return result;
            try
            {
                if (double.IsNaN(now) || double.IsInfinity(now) || now < lastNow) return Finish(false, "El reloj cambió durante la selección del cebo.");
                lastNow = now;
                if (!allowed()) return Finish(false, "Roblox perdió el foco durante la selección del cebo.");
                if (now - startedAt >= timeout) return Finish(false, "No se pudo confirmar el cebo " + Name(kind) + ". Revisa la zona completa del menú.");
                BaitMenuReading frame = read(now); BaitMenuRow row;
                if (frame == null || frame.SampledAt > now || now - frame.SampledAt > 700 || !frame.TryGetRow(kind, out row))
                {
                    release(); phase = stableFrames = selectedFrames = 0; arrivedAt = -1;
                    result.Status = "Esperando una fila clara de cebo " + Name(kind); return result;
                }
                bool fresh = frame.Sequence > sequence;
                if (fresh) sequence = frame.Sequence;
                if (phase == 3 && now - clickedAt < 350) return result;
                if (fresh)
                {
                    bool same = stableFrames > 0 && Near(target, row.ClickPoint);
                    if (!same)
                    {
                        release(); target = row.ClickPoint; stableAt = now; stableFrames = 1; selectedFrames = row.Selected ? 1 : 0;
                        phase = 0; arrivedAt = -1;
                    }
                    else { stableFrames++; selectedFrames = row.Selected ? selectedFrames + 1 : 0; }
                    if (selectedFrames >= 2 && now - stableAt >= 150) return Finish(true, "Cebo " + Name(kind) + " seleccionado");
                }
                if (phase == 0 && fresh && stableFrames >= 2 && now - stableAt >= 150)
                {
                    aim(target); phase = 1; result.Status = "Moviendo el puntero al cebo " + Name(kind); return result;
                }
                if (phase == 1 && tickAim(now)) { phase = 2; arrivedAt = now; return result; }
                if (phase == 2 && fresh && now - arrivedAt >= 200)
                {
                    if (!Near(target, row.ClickPoint)) return result;
                    if (!allowed()) return Finish(false, "Roblox perdió el foco antes del clic de cebo.");
                    click(target); attempts++; phase = 3; clickedAt = now; selectedFrames = 0;
                    result.Status = "Comprobando la selección del cebo " + Name(kind); return result;
                }
                if (phase == 3 && now - clickedAt >= 1800 && attempts < 2 && fresh)
                { release(); phase = 0; stableFrames = selectedFrames = 0; arrivedAt = -1; }
                return result;
            }
            catch (Exception error) { return Finish(false, "Falló la selección del cebo: " + error.Message); }
        }
        public void Cancel()
        {
            release();
            if (!result.Completed) result = new BaitSelectionResult { Completed = true, Status = "Selección de cebo cancelada" };
            phase = 0;
        }
        private BaitSelectionResult Finish(bool success, string status)
        { release(); result = new BaitSelectionResult { Completed = true, Succeeded = success, Status = status }; return result; }
        private static bool Near(Point a, Point b) { return Math.Abs((long)a.X - b.X) <= 3 && Math.Abs((long)a.Y - b.Y) <= 3; }
        public static string Name(BaitKind value) { return value == BaitKind.Legendary ? "legendario" : value == BaitKind.Rare ? "raro" : "común"; }
    }
}
