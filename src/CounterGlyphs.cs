using System;
using System.Drawing;

namespace SomeFishingGPO
{
    internal static class CounterGlyphs
    {
        // Two 32 x 24 binary references of the same complete x2 counter: the
        // supplied enlarged preview and its 38 x 33 reconstruction. No screenshot,
        // player identity or inventory is stored. This fallback recognizes only 2;
        // it does not infer other digits, exhaustion, or a purchase authorization.
        private static readonly string[] Two = {
            "001100000000000000000001111100001111110000001111000001111111111011111100000111110000111111111110111111100001111110011111111111100111111111111111000111111111111101111111111111110000111110111111001111111111111100001110000111110000111111111100000000000011111100001111111111000000000000111111000001111111110000000000001111100000011111111110000000000111111000001111111111110000000111111110000011111111111100000001111111100000111111111111100001111111100000001111111111111000011111111000001111111111111111001111111100000111111100001111111111111111000001111111000011111111111111111000011111100000011111111111111111000011110000000011111111111111111000111100000000110111111111111110000111000000000000111111111111100000000000000000001111111111100000000000000000000000111100000000",
            "000000000000000000000000110000000000000000000000000000001100000011111100000111100000011111111100111111000001111000000111111111001111111101111111100111111111110011111111011111111001111111111100001111111111111000011110001111110011111111111110000111100011111100001111111110000000000000111100000011111111100000000000001111000000001111111000000000001111110000000011111110000000000011111100000011111111111000000001111111000000111111111110000000011111110000001111111111111000011111110000000011111111111110000111111100000011111100011111100111111100000000111111000111111001111111000000001111110000011111111111111100000011111100000111111111111111000000111100000001111111111111111100001111000000011111111111111111000000000000000000011111111111000000000000000000000111111111110000"
        };

        // Binary shapes of complete x3/x4 labels, normalized to 32 x 24. These
        // contain only lettering, not screenshots, inventory, or player data.
        // They add positive low-count readings; they never supply a zero.
        private const string Three = "111111000000011000011111111111001111110000000110000111111111110011111111000111111001111111111100111111110001111110011111111111000011111111111110000000000011111100111111111111100000000000111111001111111111111000000000001111110000111111111000000000000011111100001111111110000000000000111111000000111111100000000001111111000000001111111000000000011111110000001111111111100000011111110000000011111111111000000111111100000000111111111111100000011111110000001111111111111000000111111100001111110001111110000000111111000011111100011111100000001111110000111100000001111000000011111100001111000000011110000000111111000011110000000111100000001111110000111100000000000111111111111100001111000000000001111111111111000000000000000000011111111111000000000000000000000111111111110000";
        private const string Four = "111111000000011000000111000000001111110000000110000001110000000011111111000111111001111100000000111111110001111110011111000000000011111111111110000111110000000000111111111111100001111100000000001111111111111000011111000000000000111111111000000111110000111100001111111110000001111100001111000000111111100000011111000011110000001111111000000111110000111100001111111111100001111100111111000011111111111000011111001111110000111111111111100111111111111100001111111111111001111111111111001111110001111110011111111111110011111100011111100111111111111100111100000001111000000000111111001111000000011110000000001111110011110000000111100000000011111100111100000000000000000000111100001111000000000000000000001111000000000000000000000000000011110000000000000000000000000000111100";

        internal static int? MatchLow(Bitmap image)
        {
            if (image == null || image.Width < 10 || image.Height < 9) return null;
            bool isTwo = MatchTwo(image);
            string samples = LowSamples(image);
            if (samples == null) return isTwo ? (int?)2 : null;
            int errorThree = Errors(samples, Three), errorFour = Errors(samples, Four);
            int errorTwo = Math.Min(Errors(samples, Two[0]), Errors(samples, Two[1]));
            int best = Math.Min(errorThree, errorFour);
            int runner = Math.Min(errorTwo, Math.Max(errorThree, errorFour));
            // Require a clear winner over the other complete counters. Unknown
            // or conflicting shapes are not rounded to the nearest low count.
            int? near = best <= 100 && runner - best >= 32 ? (int?)(errorThree < errorFour ? 3 : 4) : null;
            if (isTwo && near.HasValue) return null;
            return near.HasValue ? near : isTwo ? (int?)2 : null;
        }
        private static int Errors(string samples, string reference)
        {
            int errors = 0;
            for (int i = 0; i < samples.Length; i++) if (samples[i] != reference[i]) errors++;
            return errors;
        }
        private static string LowSamples(Bitmap image)
        {
            Func<Color, bool> ink = delegate(Color c) { return c.R > 180 && c.G > 100 && c.R - c.G > 20 && c.G - c.B > 35; };
            int[] rows = new int[image.Height];
            for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++) if (ink(image.GetPixel(x, y))) rows[y]++;
            int begin = 0, end = image.Height - 1;
            // A thin gold border at the very top/bottom is not part of x4.
            // Ignore it only when it spans almost the full image and a clear
            // empty gap separates it from the counter. Other extra ink remains.
            int edgeLimit = Math.Max(2, image.Height / 8);
            if (rows[0] >= image.Width * .8)
            {
                int y = 0; while (y < edgeLimit && rows[y] > 0) y++;
                if (y < image.Height - 2 && rows[y] == 0 && rows[y + 1] == 0) begin = y + 2;
            }
            if (rows[image.Height - 1] >= image.Width * .8)
            {
                int y = image.Height - 1; while (y >= image.Height - edgeLimit && rows[y] > 0) y--;
                if (y > 1 && rows[y] == 0 && rows[y - 1] == 0) end = y - 2;
            }
            int l = image.Width, t = image.Height, r = -1, b = -1;
            for (int y = begin; y <= end; y++) for (int x = 0; x < image.Width; x++) if (ink(image.GetPixel(x, y)))
            { l = Math.Min(l, x); t = Math.Min(t, y); r = Math.Max(r, x); b = Math.Max(b, y); }
            int width = r - l + 1, height = b - t + 1;
            if (height < 7 || width < 8 || l <= 0 || t <= 0 || r >= image.Width - 1 || b >= image.Height - 1) return null;
            double ratio = (double)width / height;
            if (ratio < 1.15 || ratio > 1.75) return null;
            var samples = new char[32 * 24];
            for (int y = 0; y < 24; y++) for (int x = 0; x < 32; x++)
                samples[y * 32 + x] = ink(image.GetPixel(l + Math.Min(width - 1, (int)((x + .5) * width / 32)), t + Math.Min(height - 1, (int)((y + .5) * height / 24)))) ? '1' : '0';
            return new string(samples);
        }
        internal static bool MatchTwo(Bitmap image)
        {
            int l=image.Width,t=image.Height,r=-1,b=-1;
            Func<Color,bool> ink=delegate(Color c){return c.R>140&&c.G>75&&c.R-c.G>20&&c.G-c.B>35;};
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(ink(image.GetPixel(x,y)))
            {l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);}
            int width=r-l+1,height=b-t+1;
            if(height<7||width<8||l<=0||t<=0||r>=image.Width-1||b>=image.Height-1)return false;
            double ratio=(double)width/height;if(ratio<1.15||ratio>1.7)return false;
            int[] errors=new int[Two.Length];
            for(int y=0;y<24;y++)for(int x=0;x<32;x++)
            {
                bool pixel=ink(image.GetPixel(l+Math.Min(width-1,(int)((x+.5)*width/32)),t+Math.Min(height-1,(int)((y+.5)*height/24))));
                for(int n=0;n<Two.Length;n++)if(pixel!=(Two[n][y*32+x]=='1'))errors[n]++;
            }
            // At most 10.4% differing samples, across the entire x and digit.
            return errors[0]<=80||errors[1]<=80;
        }
    }
}
