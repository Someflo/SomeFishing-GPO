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

    public sealed class ShopVisualReading
    {
        public bool QuantityMenu;
        public long Sequence;
        public double SampledAt;
        public string Detail = "Esperando los botones de cantidad";
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

        public static ShopVisualReading Analyze(Bitmap left,Bitmap middle,Bitmap right)
        {
            if(left==null||middle==null||right==null)throw new ArgumentNullException("patch","Falta una imagen de los botones marcados.");
            foreach(Bitmap image in new[]{left,middle,right})
                if(image.Width!=PatchWidth||image.Height!=PatchHeight)throw new ArgumentException("Cada punto necesita una imagen de 49 × 33 píxeles.");
            Ink green=Count(left,0),white=Count(middle,1),red=Count(right,2);
            bool visible=green.TextLike(12,4,4)&&white.TextLike(4,2,3)&&red.TextLike(12,4,4);
            return new ShopVisualReading{
                QuantityMenu=visible,
                Detail=(visible?"Botones de cantidad visibles":"Esperando botones de cantidad")+
                    " · verde izquierdo: "+green.Count+" · blanco central: "+white.Count+" · rojo derecho: "+red.Count
            };
        }
    }
}
