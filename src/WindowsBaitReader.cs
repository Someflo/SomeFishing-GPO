using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace SomeFishingGPO
{
    // Windows' local OCR runs on a worker. It has no input-sending capability.
    // There is at most one pending read; late results after Stop are discarded.
    internal sealed class WindowsBaitReader : IDisposable
    {
        private Task<BaitReading> pending;
        private BaitReading latest = new BaitReading();
        private long sequence;
        private double nextSample;
        private bool disposed;
        internal BaitReading Latest { get { Poll(); return latest; } }
        internal bool Due(double now) { Poll(); return !disposed && pending == null && now >= nextSample; }
        internal void Submit(Bitmap frame, double now)
        {
            if (disposed || pending != null) { frame.Dispose(); return; }
            long currentSequence = ++sequence; nextSample = now + 1500;
            pending = Task.Run(delegate
            {
                using (frame)
                {
                    BaitReading reading;
                    try { reading = ReadImage(frame); }
                    catch (Exception) { reading = new BaitReading { Detail = "No se pudo usar el lector de Windows. Revisa la zona y el OCR instalado." }; }
                    reading.Sequence = currentSequence; reading.SampledAt = now; return reading;
                }
            });
        }
        private void Poll()
        {
            if (pending == null || !pending.IsCompleted) return;
            if (!disposed) latest = pending.Status == TaskStatus.RanToCompletion ? pending.Result :
                new BaitReading { Detail = "Falló la lectura del contador" };
            if (pending.IsFaulted) { var ignored = pending.Exception; }
            pending = null;
        }
        internal static BaitReading ReadImage(Bitmap image)
        {
            if (MultipleYellowRows(image)) return new BaitReading { Detail = "Hay varias filas en la zona. Selecciona un solo contador." };
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine == null) return new BaitReading { Detail = "Windows no tiene un idioma de OCR disponible." };
            string first = Recognize(engine, image, 3), second = Recognize(engine, image, 5);
            int? a = BaitText.Parse(first), b = BaitText.Parse(second);
            if (!a.HasValue || !b.HasValue || a != b)
                return new BaitReading { Detail = "Número no reconocido con claridad. Selecciona solo x y la cantidad." };
            return new BaitReading { Count = a, Detail = "Lectura: " + a.Value };
        }
        private static bool MultipleYellowRows(Bitmap image)
        {
            int groups=0, inkRows=0, holes=0;
            for(int y=0;y<image.Height+3;y++)
            {
                int ink=0;
                if(y<image.Height)for(int x=0;x<image.Width;x++)
                {
                    Color p=image.GetPixel(x,y);
                    if(p.R>180 && p.G>100 && p.B<140)ink++;
                }
                if(ink>=2){inkRows++;holes=0;}
                else if(++holes>=3){if(inkRows>=3)groups++;inkRows=0;}
            }
            return groups>1;
        }
        private static string Recognize(OcrEngine engine, Bitmap image, int scale)
        {
            using (var enlarged = new Bitmap(image.Width * scale + 40, image.Height * scale + 40, PixelFormat.Format32bppArgb))
            using (var stream = new MemoryStream())
            {
                using (Graphics graphics = Graphics.FromImage(enlarged))
                {
                    graphics.Clear(Color.FromArgb(40, 40, 40));
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, new Rectangle(20, 20, image.Width * scale, image.Height * scale),
                        0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
                }
                enlarged.Save(stream, ImageFormat.Png); stream.Position = 0;
                using (var random = stream.AsRandomAccessStream())
                {
                    var decoder = BitmapDecoder.CreateAsync(random).AsTask().GetAwaiter().GetResult();
                    using (var bitmap = decoder.GetSoftwareBitmapAsync().AsTask().GetAwaiter().GetResult())
                        return engine.RecognizeAsync(bitmap).AsTask().GetAwaiter().GetResult().Text;
                }
            }
        }
        public void Dispose() { disposed = true; latest = new BaitReading(); }
    }
}
