using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace SomeFishingGPO
{
    public static class ShopVisualTests
    {
        private static Bitmap Patch(Color ink,bool solid=false)
        {
            var image=new Bitmap(ShopVisual.PatchWidth,ShopVisual.PatchHeight,PixelFormat.Format32bppArgb);
            using(Graphics graphics=Graphics.FromImage(image)){
                graphics.Clear(Color.FromArgb(30,26,23));
                using(var brush=new SolidBrush(ink)){
                    if(solid)graphics.FillRectangle(brush,0,0,image.Width,image.Height);
                    else for(int x=14;x<=32;x+=6){graphics.FillRectangle(brush,x,11,2,12);graphics.FillRectangle(brush,x,11,5,2);}
                }
            }
            return image;
        }
        public static void Run(Action<bool,string> check)
        {
            Color lime=Color.FromArgb(166,255,0),red=Color.FromArgb(255,16,8);
            using(var left=Patch(lime))using(var middle=Patch(Color.White))using(var right=Patch(red)){
                var reading=ShopVisual.Analyze(left,middle,right);
                check(reading.QuantityMenu,"Visual gate recognizes green left, white middle and red right without OCR");
                check(reading.Detail.Contains("verde izquierdo")&&reading.Detail.Contains("rojo derecho"),"Visual diagnostics explain the button evidence without claiming a successful click");
                using(var blank=Patch(Color.FromArgb(30,26,23))){
                    check(!ShopVisual.Analyze(blank,middle,right).QuantityMenu,"Missing green left cannot authorize the quantity field");
                    check(!ShopVisual.Analyze(left,blank,right).QuantityMenu,"Missing white center cannot authorize the quantity field");
                    check(!ShopVisual.Analyze(left,middle,blank).QuantityMenu,"Missing red right cannot authorize the quantity field");
                    check(!ShopVisual.Analyze(blank,middle,blank).QuantityMenu,"A final dialog with only white central ink is not the quantity menu");
                }
                using(var white=Patch(Color.White))check(!ShopVisual.Analyze(white,middle,white).QuantityMenu,"White Yes and No buttons do not look like the quantity menu");
                check(!ShopVisual.Analyze(right,middle,left).QuantityMenu,"Swapping green and red buttons is rejected");
                using(var yellow=Patch(Color.FromArgb(255,226,0)))check(!ShopVisual.Analyze(yellow,middle,right).QuantityMenu,"Yellow labels or bait counters do not replace the green Buy lettering");
                using(var greenFill=Patch(lime,true))using(var whiteFill=Patch(Color.White,true))using(var redFill=Patch(red,true)){
                    check(!ShopVisual.Analyze(greenFill,middle,right).QuantityMenu,"A flat green panel is not Buy lettering");
                    check(!ShopVisual.Analyze(left,whiteFill,right).QuantityMenu,"A flat white panel is not quantity lettering");
                    check(!ShopVisual.Analyze(left,middle,redFill).QuantityMenu,"A flat red panel is not Cancel lettering");
                }
                using(var speck=Patch(Color.FromArgb(30,26,23))){speck.SetPixel(24,16,lime);check(!ShopVisual.Analyze(speck,middle,right).QuantityMenu,"One colored pixel is insufficient to authorize quantity input");}
                bool badSize=false;using(var small=new Bitmap(48,33))try{ShopVisual.Analyze(small,middle,right);}catch(ArgumentException){badSize=true;}
                check(badSize,"Unexpected patch size fails instead of silently widening the observed area");
            }
            check(ShopVisual.Region(new Point(-1100,400))==new Rectangle(-1124,384,49,33),"Marked-point patches preserve negative monitor coordinates");
            bool overflow=false;try{ShopVisual.Region(new Point(int.MinValue,0));}catch(OverflowException){overflow=true;}
            check(overflow,"A patch with overflowing coordinates is rejected");
        }

        // Screenshots are optional private fixtures supplied by the caller. No
        // user path or screenshot is embedded in the source or distributed app.
        public static void VerifySample(Action<bool,string> check,string path,Point left,Point middle,Point right,bool expected,string label)
        {
            using(var image=new Bitmap(path)){
                Rectangle bounds=new Rectangle(Point.Empty,image.Size);
                Rectangle a=ShopVisual.Region(left),b=ShopVisual.Region(middle),c=ShopVisual.Region(right);
                bool contained=Settings.ContainsSafely(bounds,a)&&Settings.ContainsSafely(bounds,b)&&Settings.ContainsSafely(bounds,c);
                check(contained,label+" contains all three marked-point patches");
                if(!contained)return;
                using(var lp=image.Clone(a,PixelFormat.Format32bppArgb))using(var mp=image.Clone(b,PixelFormat.Format32bppArgb))using(var rp=image.Clone(c,PixelFormat.Format32bppArgb)){
                    ShopVisualReading reading=ShopVisual.Analyze(lp,mp,rp);
                    check(reading.QuantityMenu==expected,label+": "+reading.Detail);
                }
            }
        }
    }
}
