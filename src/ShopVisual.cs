using System;
using System.Drawing;

namespace SomeFishingGPO
{
    // Only the three points explicitly marked by the user are observed.
    // This does not read text, a quantity, MAX, or a successful purchase.
    public interface IShopVisualRuntime
    {
        ShopVisualReading ReadShopVisual(double now);
    }

    public enum ShopMenuKind { Unknown, Absent, Confirm, Quantity, Done }

    public sealed class ShopVisualReading
    {
        public ShopMenuKind Menu;
        public bool QuantityMenu;
        public long Sequence;
        public double SampledAt;
        public string Detail = "Esperando los botones de cantidad";
        public ShopMenuKind Kind { get { return QuantityMenu ? ShopMenuKind.Quantity : Menu; } }
    }

    // A cached, old or contradictory frame cannot accumulate confirmation.
    // Each new action creates a new evidence window; timestamps are milliseconds.
    internal sealed class ShopVisualEvidence
    {
        private readonly double started;
        private long sequence=-1;
        private int frames;
        private double first=-1,last=-1;
        internal ShopMenuKind Kind { get; private set; }
        internal bool Stable { get { return frames>=2&&last-first>=100; } }
        internal ShopVisualEvidence(double now){started=now;Kind=ShopMenuKind.Unknown;}
        internal bool Read(ShopVisualReading reading,double now)
        {
            if(reading==null||reading.SampledAt<started||reading.SampledAt>now||now-reading.SampledAt>500)
            {frames=0;first=last=-1;Kind=ShopMenuKind.Unknown;return false;}
            if(reading.Sequence<=sequence)return false;
            sequence=reading.Sequence;
            if(frames==0||Kind!=reading.Kind){frames=0;first=reading.SampledAt;}
            Kind=reading.Kind;frames++;last=reading.SampledAt;return true;
        }
    }

    public static class ShopVisual
    {
        public const int PatchWidth = 49;
        public const int PatchHeight = 33;

        public static Rectangle Region(Point center)
        {
            return new Rectangle(checked(center.X-PatchWidth/2),checked(center.Y-PatchHeight/2),PatchWidth,PatchHeight);
        }

        private sealed class Ink
        {
            internal int Count;
            internal int Left=PatchWidth,Top=PatchHeight,Right=-1,Bottom=-1;
            internal void Add(int x,int y)
            {
                Count++;Left=Math.Min(Left,x);Top=Math.Min(Top,y);Right=Math.Max(Right,x);Bottom=Math.Max(Bottom,y);
            }
            internal bool TextLike(int minimum,int width,int height)
            {
                // Flat fills are not button lettering. A very small speck is not
                // sufficient evidence either; no glyph or digit is recognized.
                return Count>=minimum&&Count<=PatchWidth*PatchHeight/2&&Right-Left+1>=width&&Bottom-Top+1>=height;
            }
        }

        private static Ink Count(Bitmap image,int color)
        {
            var ink=new Ink();
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){
                Color p=image.GetPixel(x,y);bool match;
                if(color==0)match=p.G>=150&&p.R>=45&&p.B<=100&&p.G>=p.R+35;
                else if(color==1)match=p.R>=170&&p.G>=170&&p.B>=170&&Math.Max(p.R,Math.Max(p.G,p.B))-Math.Min(p.R,Math.Min(p.G,p.B))<=55;
                else match=p.R>=170&&p.G<=95&&p.B<=110&&p.R>=p.G+70;
                if(match)ink.Add(x,y);
            }
            return ink;
        }

        private static bool ThreeDots(Bitmap image)
        {
            bool[,] used=new bool[PatchWidth,PatchHeight];
            var dots=new System.Collections.Generic.List<Rectangle>();
            for(int y=0;y<PatchHeight;y++)for(int x=0;x<PatchWidth;x++){
                Color pixel=image.GetPixel(x,y);
                if(used[x,y]||!White(pixel))continue;
                var pending=new System.Collections.Generic.Queue<Point>();pending.Enqueue(new Point(x,y));used[x,y]=true;
                int left=x,right=x,top=y,bottom=y,count=0;
                while(pending.Count>0){Point p=pending.Dequeue();count++;left=Math.Min(left,p.X);right=Math.Max(right,p.X);top=Math.Min(top,p.Y);bottom=Math.Max(bottom,p.Y);
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
                        int nx=p.X+dx,ny=p.Y+dy;if(nx<0||ny<0||nx>=PatchWidth||ny>=PatchHeight||used[nx,ny]||!White(image.GetPixel(nx,ny)))continue;
                        used[nx,ny]=true;pending.Enqueue(new Point(nx,ny));
                    }
                }
                if(count>=3)dots.Add(Rectangle.FromLTRB(left,top,right+1,bottom+1));
            }
            if(dots.Count!=3)return false;
            dots.Sort(delegate(Rectangle a,Rectangle b){return a.Left.CompareTo(b.Left);});
            foreach(Rectangle dot in dots)if(dot.Width<2||dot.Width>9||dot.Height<2||dot.Height>9||Math.Abs(dot.Width-dot.Height)>4)return false;
            return dots[1].Left>dots[0].Right&&dots[2].Left>dots[1].Right&&
                Math.Abs(dots[0].Top-dots[1].Top)<=3&&Math.Abs(dots[1].Top-dots[2].Top)<=3&&
                Math.Abs((dots[1].Left-dots[0].Left)-(dots[2].Left-dots[1].Left))<=4;
        }
        private static bool White(Color p)
        {return p.R>=170&&p.G>=170&&p.B>=170&&Math.Max(p.R,Math.Max(p.G,p.B))-Math.Min(p.R,Math.Min(p.G,p.B))<=55;}

        public static ShopVisualReading Analyze(Bitmap left,Bitmap middle,Bitmap right)
        {
            if(left==null||middle==null||right==null)throw new ArgumentNullException("patch","Falta una imagen de los botones marcados.");
            foreach(Bitmap image in new[]{left,middle,right})
                if(image.Width!=PatchWidth||image.Height!=PatchHeight)throw new ArgumentException("Cada punto necesita una imagen de 49 × 33 píxeles.");
            Ink green=Count(left,0),white=Count(middle,1),red=Count(right,2),leftWhite=Count(left,1),rightWhite=Count(right,1);
            bool visible=green.TextLike(12,4,4)&&white.TextLike(4,2,3)&&red.TextLike(12,4,4);
            bool sidesQuiet=green.Count<4&&red.Count<4&&leftWhite.Count<4&&rightWhite.Count<4;
            ShopMenuKind menu=visible?ShopMenuKind.Quantity:
                leftWhite.TextLike(8,3,4)&&rightWhite.TextLike(8,3,4)&&white.Count<4&&green.Count<4&&red.Count<4?ShopMenuKind.Confirm:
                sidesQuiet&&ThreeDots(middle)?ShopMenuKind.Done:
                sidesQuiet&&white.Count<4?ShopMenuKind.Absent:ShopMenuKind.Unknown;
            return new ShopVisualReading{
                QuantityMenu=visible,Menu=menu,
                Detail="Menú detectado: "+menu+
                    " · verde izquierdo: "+green.Count+" · blanco central: "+white.Count+" · rojo derecho: "+red.Count
            };
        }
    }
}
