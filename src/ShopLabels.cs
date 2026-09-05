using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Windows.Media.Ocr;

namespace SomeFishingGPO
{
    internal static class ShopLabels
    {
        // Tiny binary glyph references for the supplied Sí / No buttons (32 x 24).
        // No player image, name, inventory or full screenshot is embedded.
        internal const string Yes = "000000000000000000000000000001000000000000000000000000000011111100000000000000000000000000111111000000001110000000000011111111110001111111111111000001111111100000111111111111111100001111111000001111111111111111000011111110001111110000000111000000001111100011111100000000000000000011111000111111000000000000000000111110001111110000000000000000001111100000111111111000000000000011111000000111111111111100000000111110000000011111111111111000001111100000000111111111111110000011111000000000000011111111111011111110000000000000000011111110111110000000000000000000001111101111100000000000000000000011111011111000000000000000000011111110111110000011111111111111111111101111100000111111111111111111100011111000001111111111111111111000111110000000111111111111110000000000000000";
        internal const string No = "011110000000000000000000000000000111110000000011000000000000000001111100000000110000000000000000011111000000111100000011110000000111110000001111000000111100000011111110000011110000011111100000111111100001111100001111111111001111111000011110001111111111110011111110000111100011111111111100111111111001111000111110001111101111111110011110011111000001111011111111100111100111110000011110011111111001111001111000000011110111111111011110011110000000111101111111110111100111100000001111011110111101111001111000000111110111101111011110011110000001111101111011111111100111100000011110011110011111111001111111111111100111100111111110011111111111111001111000011111000011111111111100010000000011110000001111111100000100000000111100000011111111000000000000000000000000011111000000";
        private static bool White(Color c){return Math.Min(c.R,Math.Min(c.G,c.B))>185&&Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))<50;}
        internal static Rectangle InkBounds(Bitmap image,Rectangle region)
        {
            int l=region.Right,t=region.Bottom,r=region.Left-1,b=region.Top-1;
            for(int y=region.Top;y<region.Bottom;y++)for(int x=region.Left;x<region.Right;x++)if(White(image.GetPixel(x,y))){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);}
            return r>=l&&b>=t?Rectangle.FromLTRB(l,t,r+1,b+1):Rectangle.Empty;
        }
        internal static Rectangle GreenButtonBounds(Bitmap image,Rectangle region)
        {
            int l=region.Right,t=region.Bottom,r=region.Left-1,b=region.Top-1;
            for(int y=region.Top;y<region.Bottom;y++)for(int x=region.Left;x<region.Right;x++){
                Color c=image.GetPixel(x,y);
                if(c.G>150&&c.G-c.B>90&&c.G>c.R*.8){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);}
            }
            Rectangle box=r>=l&&b>=t?Rectangle.FromLTRB(l,t,r+1,b+1):Rectangle.Empty;
            return box.Width>=6&&box.Height>=6&&box.Width<region.Width*.8&&box.Height<region.Height*.9?box:Rectangle.Empty;
        }
        internal static bool Match(Bitmap image,Rectangle region,string reference)
        {
            Rectangle box=InkBounds(image,region);
            if(box.Height<6||box.Width<6||box.Width>region.Width*.8||box.Height>region.Height*.9)return false;
            double ratio=(double)box.Width/box.Height;if(ratio<.8||ratio>2.2)return false;
            int errors=0;
            for(int y=0;y<24;y++)for(int x=0;x<32;x++){
                bool pixel=White(image.GetPixel(box.X+Math.Min(box.Width-1,(int)((x+.5)*box.Width/32)),box.Y+Math.Min(box.Height-1,(int)((y+.5)*box.Height/24))));
                if(pixel!=(reference[y*32+x]=='1'))errors++;
            }
            return errors<=100;
        }
        internal static int? RepeatedNumber(string text)
        {
            string value=Regex.Replace(text??"",@"\s","");
            if(!Regex.IsMatch(value,@"\A[0-9]{3,12}\z")||value.Length%3!=0)return null;
            int length=value.Length/3;
            string a=value.Substring(0,length);
            if(value.Substring(length,length)!=a||value.Substring(length*2,length)!=a)return null;
            int count;return int.TryParse(a,out count)?(int?)count:null;
        }
        internal static int? Number(OcrEngine engine,Bitmap image,Rectangle region)
        {
            Rectangle box=InkBounds(image,region);
            if(box.Height<5||box.Width<2||box.Width>region.Width*.7||box.Height>region.Height*.9)return null;
            // Repeating the same isolated glyph image helps Windows segment very short fields.
            // All three copies must yield the same digits at each of two scales.
            using(var tile=new Bitmap((box.Width+20)*3+20,box.Height+20,PixelFormat.Format32bppArgb)){
                using(var g=Graphics.FromImage(tile))g.Clear(Color.White);
                for(int c=0;c<3;c++)for(int y=0;y<box.Height;y++)for(int x=0;x<box.Width;x++)
                    tile.SetPixel(10+c*(box.Width+20)+x,10+y,White(image.GetPixel(box.X+x,box.Y+y))?Color.Black:Color.White);
                int? a=RepeatedNumber(WindowsBaitReader.Recognize(engine,tile,2)),b=RepeatedNumber(WindowsBaitReader.Recognize(engine,tile,3));
                return a.HasValue&&a==b?a:null;
            }
        }
    }
}
