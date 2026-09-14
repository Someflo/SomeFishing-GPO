using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
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
        private readonly string language;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly Func<Bitmap,string,CancellationToken,BaitReading> read;
        internal WindowsBaitReader(string language="", Func<Bitmap,string,CancellationToken,BaitReading> read=null)
        { this.language=language; this.read=read??ReadImage; }
        internal static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string,string>> Languages()
        {
            var result=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string,string>>();
            try { foreach(var item in OcrEngine.AvailableRecognizerLanguages)result.Add(new System.Collections.Generic.KeyValuePair<string,string>(item.LanguageTag,item.NativeName+" ("+item.LanguageTag+")")); } catch { }
            return result;
        }
        internal static OcrEngine CreateEngine(string language)
        {
            if(string.IsNullOrEmpty(language))return OcrEngine.TryCreateFromUserProfileLanguages();
            foreach(var item in OcrEngine.AvailableRecognizerLanguages)
                if(string.Equals(item.LanguageTag,language,StringComparison.OrdinalIgnoreCase))return OcrEngine.TryCreateFromLanguage(item);
            return null;
        }
        internal BaitReading Latest { get { Poll(); return latest; } }
        internal bool Due(double now) { Poll(); return !disposed && pending == null && !WinRtWait.Shared.Busy && now >= nextSample; }
        internal void Submit(Bitmap frame, double now)
        {
            if (disposed || pending != null || WinRtWait.Shared.Busy) { frame.Dispose(); return; }
            long currentSequence = ++sequence; nextSample = now + 1500;
            pending = Task.Run(delegate
            {
                using (frame)
                {
                    BaitReading reading;
                    try { reading = read(frame,language,stop.Token); }
                    catch (Exception) { reading = new BaitReading { Detail = "No se pudo usar el lector de Windows. Revisa la zona y el OCR instalado." }; }
                    reading.Sequence = currentSequence; reading.SampledAt = now; return reading;
                }
            });
        }
        private void Poll()
        {
            if (pending == null || !pending.IsCompleted) return;
            if (!disposed)
            {
                if (pending.Status != TaskStatus.RanToCompletion) latest = new BaitReading { Detail = "Falló la lectura del contador" };
                else if (pending.Result.Sequence == sequence) latest = pending.Result;
            }
            if (pending.IsFaulted) { var ignored = pending.Exception; }
            pending = null;
        }
        internal static BaitReading ReadImage(Bitmap image,string language="", CancellationToken token=default(CancellationToken))
        {
            using (WinRtWait.BeginRead(token))
            {
                token.ThrowIfCancellationRequested();
                try { return ReadImageCore(image,language); }
                catch (TimeoutException) { return new BaitReading { Detail = WinRtWait.TimeoutDetail }; }
                catch (InvalidOperationException error)
                {
                    if (error.Message != WinRtWait.BusyDetail) throw;
                    return new BaitReading { Detail = WinRtWait.BusyDetail };
                }
            }
        }
        private static BaitReading ReadImageCore(Bitmap image,string language)
        {
            if (!HasCounterInk(image)) return new BaitReading { VisuallyAbsent = true, Detail = "No se ve el texto amarillo del contador" };
            if (MultipleYellowRows(image)) return new BaitReading { Detail = "Hay varias filas en la zona. Selecciona un solo contador." };
            var engine = CreateEngine(language);
            if (engine == null) return new BaitReading { Detail = "El idioma OCR elegido no está disponible en Windows." };
            string first = Recognize(engine, image, 3), second = Recognize(engine, image, 5);
            int? a = ParseCounter(first), b = ParseCounter(second);
            int? isolatedFirst=null, isolatedSecond=null;
            if (a.HasValue && a == b) return new BaitReading { Count = a, Detail = "Lectura: " + a.Value };
            // Conflicting numeric evidence must stay unknown; do not choose a preferred result.
            if (!a.HasValue || !b.HasValue || a == b)
            using (Bitmap isolated = NormalizeCounterText(image))
            {
                if (isolated != null)
                {
                    int? c = ParseCounter(Recognize(engine, isolated, 3));
                    int? d = ParseCounter(Recognize(engine, isolated, 5));
                    isolatedFirst=c;isolatedSecond=d;
                    if (c.HasValue && c == d && (!a.HasValue || a == c) && (!b.HasValue || b == c))
                        return new BaitReading { Count = c, Detail = "Lectura: " + c.Value + " · texto amarillo aislado" };
                }
            }
            // Preserve antialiased edges in tiny digits. A hard yellow/white mask
            // turns e.g. 295 into letters on the supplied 9-pixel-high counter.
            // Use two scales of each soft contrast image, retaining disagreements.
            int? softValue=null,softEvidence=null;
            bool conflict=(a.HasValue&&b.HasValue&&a!=b)||(isolatedFirst.HasValue&&isolatedSecond.HasValue&&isolatedFirst!=isolatedSecond);
            foreach(int variant in new[]{0,1,2})
            {
                if(conflict)break;
                using(Bitmap soft=NormalizeSoftCounter(image,variant))
                {
                    if(soft==null)continue;
                    int? c=ParseCounter(Recognize(engine,soft,2)),d=ParseCounter(Recognize(engine,soft,3));
                    foreach(int? evidence in new[]{c,d})if(evidence.HasValue)
                    {
                        if((a.HasValue&&a!=evidence)||(b.HasValue&&b!=evidence)
                            ||(isolatedFirst.HasValue&&isolatedFirst!=evidence)||(isolatedSecond.HasValue&&isolatedSecond!=evidence)
                            ||(softEvidence.HasValue&&softEvidence!=evidence))conflict=true;
                        softEvidence=evidence;
                    }
                    if(c.HasValue&&c==d)softValue=c;
                }
            }
            if(!conflict&&softValue.HasValue)return new BaitReading{Count=softValue,Detail="Lectura: "+softValue.Value+" · contraste suave"};
            // Narrow positive-only visual references handle complete x2/x3/x4
            // glyphs when Windows returns no text. They never supply a zero and
            // cannot override contradictory numeric OCR evidence at any scale.
            int? visual = CounterGlyphs.MatchLow(image);
            if(!conflict&&visual.HasValue&&(!softEvidence.HasValue||softEvidence==visual)&&(!a.HasValue||a==visual)&&(!b.HasValue||b==visual)
                &&(!isolatedFirst.HasValue||isolatedFirst==visual)&&(!isolatedSecond.HasValue||isolatedSecond==visual))
                return new BaitReading{Count=visual,Detail="Lectura: "+visual.Value+" · referencia visual x"+visual.Value};
            return new BaitReading { Detail = conflict?"OCR contradictorio; cantidad desconocida. Ajusta la zona o usa Cronómetro.":"OCR sin coincidencia: "+ShortText(first)+" / "+ShortText(second)+". Rodea x y el número o usa Cronómetro." };
        }
        private static string ShortText(string text)
        {
            text=(text??"").Replace("\r"," ").Replace("\n"," ").Trim();
            return text.Length==0?"vacío":text.Length>18?text.Substring(0,18)+"…":text;
        }
        internal static int? ParseCounter(string text)
        {
            // This region is configured around x and the complete quantity. If
            // OCR drops the prefix/first digit (x42 -> 2), a bare suffix is unsafe.
            return System.Text.RegularExpressions.Regex.IsMatch(text??"",@"\A\s*[xX×*]\s*[0-9]{1,5}\s*\z")?BaitText.Parse(text):null;
        }
        internal static Bitmap NormalizeSoftCounter(Bitmap image,int variant)
        {
            int left=image.Width,top=image.Height,right=-1,bottom=-1;
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
            {
                Color c=image.GetPixel(x,y);
                if(c.R>140&&c.G>75&&c.R-c.G>20&&c.G-c.B>35)
                {left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            }
            int width=right-left+1,height=bottom-top+1;
            if(width<3||height<5||width>height*12)return null;
            int targetHeight=variant==2?32:24;
            int targetWidth=(int)Math.Round(width*targetHeight*(variant==1?1.3:1.0)/height);
            using(var glyph=new Bitmap(width,height,PixelFormat.Format32bppArgb))
            {
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    Color c=image.GetPixel(left+x,top+y);
                    int value=255-Math.Min(255,Math.Max(0,c.R-c.B-25)*255/200);
                    glyph.SetPixel(x,y,Color.FromArgb(value,value,value));
                }
                var result=new Bitmap(targetWidth+16,targetHeight+16,PixelFormat.Format32bppArgb);
                using(var g=Graphics.FromImage(result))
                {g.Clear(Color.White);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(glyph,8,8,targetWidth,targetHeight);}
                return result;
            }
        }
        internal static Bitmap NormalizeCounterText(Bitmap image)
        {
            // Keep the yellow/orange glyphs, discard white UI strips, then normalize their
            // height. This is only an OCR fallback: it never changes letters into digits.
            var ink = new bool[image.Width, image.Height];
            int left=image.Width, top=image.Height, right=-1, bottom=-1;
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
            {
                Color c=image.GetPixel(x,y);
                ink[x,y]=c.R>140 && c.G>75 && c.R-c.G>20 && c.G-c.B>35;
                if(ink[x,y]){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            }
            int width=right-left+1, height=bottom-top+1;
            if(height<3 || width<3 || width>height*12) return null;
            using(var glyphs=new Bitmap(width,height,PixelFormat.Format32bppArgb))
            {
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)glyphs.SetPixel(x,y,ink[x+left,y+top]?Color.Black:Color.White);
                var normalized=new Bitmap((int)Math.Ceiling(width*16.0/height)+16,32,PixelFormat.Format32bppArgb);
                using(Graphics graphics=Graphics.FromImage(normalized))
                {
                    graphics.Clear(Color.White);graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(glyphs,new Rectangle(8,8,normalized.Width-16,16));
                }
                return normalized;
            }
        }
        private static bool HasCounterInk(Bitmap image)
        {
            int count = 0;
            for (int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++)
            {
                Color p=image.GetPixel(x,y);
                if(p.R>180 && p.G>100 && p.B<140 && ++count>=3)return true;
            }
            return false;
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
        internal static string Recognize(OcrEngine engine, Bitmap image, int scale)
        { return RecognizeResult(engine,image,scale).Text; }
        internal static OcrResult RecognizeResult(OcrEngine engine, Bitmap image, int scale)
        {
            // The native task owns its own pixels. A timed-out caller can dispose
            // its crop while the pending native operation finishes cancellation.
            Bitmap owned = (Bitmap)image.Clone();
            return WinRtWait.Recognize(delegate(CancellationToken token)
                { return RecognizeAsync(engine, owned, scale, token); }, owned);
        }
        private static async Task<OcrResult> RecognizeAsync(OcrEngine engine, Bitmap image, int scale, CancellationToken token)
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
                    token.ThrowIfCancellationRequested();
                    var decoder = await WinRtWait.Native(BitmapDecoder.CreateAsync(random),token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    using (var bitmap = await WinRtWait.Native(decoder.GetSoftwareBitmapAsync(),token).ConfigureAwait(false))
                    {
                        token.ThrowIfCancellationRequested();
                        return await WinRtWait.Native(engine.RecognizeAsync(bitmap),token).ConfigureAwait(false);
                    }
                }
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; ++sequence; latest = new BaitReading();
            if (pending == null) stop.Dispose();
            else
            {
                stop.CancelAfter(1);
                pending.ContinueWith(delegate(Task<BaitReading> completed)
                { if (completed.IsFaulted) { var ignored = completed.Exception; } stop.Dispose(); }, TaskScheduler.Default);
            }
        }
    }
}
