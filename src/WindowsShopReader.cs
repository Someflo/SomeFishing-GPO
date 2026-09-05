using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using Windows.Media.Ocr;

namespace SomeFishingGPO
{
    internal sealed class WindowsShopReader : IDisposable
    {
        private Task<ShopReading> pending;
        private ShopReading latest=new ShopReading();
        private long sequence;
        private double next;
        private bool disposed;
        internal ShopReading Latest { get { Poll();return latest; } }
        internal bool Due(double now){Poll();return !disposed&&pending==null&&now>=next;}
        internal void Submit(Bitmap frame,Point origin,double now)
        {
            if(disposed||pending!=null){frame.Dispose();return;}
            long seq=++sequence;next=now+1000;
            pending=Task.Run(delegate {
                using(frame){ShopReading result;
                    try{result=ReadImage(frame);}catch{result=new ShopReading{ReadFailed=true,Detail="No se pudo leer el menú de compra con Windows OCR"};}
                    result.Left.Offset(origin);result.Middle.Offset(origin);result.SampledAt=now;result.Sequence=seq;return result;
                }
            });
        }
        private void Poll()
        {
            if(pending==null||!pending.IsCompleted)return;
            if(!disposed)latest=pending.Status==TaskStatus.RanToCompletion?pending.Result:new ShopReading{ReadFailed=true};
            if(pending.IsFaulted){var ignored=pending.Exception;}pending=null;
        }
        private static string Read(OcrEngine engine,Bitmap source,Rectangle rect,int scale)
        {using(var crop=source.Clone(rect,PixelFormat.Format32bppArgb))return WindowsBaitReader.Recognize(engine,crop,scale);}
        internal static ShopReading ReadImage(Bitmap image)
        {
            var result=new ShopReading();
            if(image.Width<150||image.Height<80)return result;
            var engine=OcrEngine.TryCreateFromUserProfileLanguages();
            if(engine==null){result.ReadFailed=true;result.Detail="Windows no tiene un idioma OCR disponible";return result;}
            int row=(int)(image.Height*.73),third=image.Width/3;
            var body=new Rectangle(0,0,image.Width,row);
            var left=new Rectangle(0,row,third,image.Height-row);
            var center=new Rectangle(third,row,third,image.Height-row);
            var right=new Rectangle(third*2,row,image.Width-third*2,image.Height-row);
            string a=Read(engine,image,body,2),b=Read(engine,image,body,3);
            string la=Read(engine,image,left,3),lb=Read(engine,image,left,5);
            string ca=Read(engine,image,center,3),cb=Read(engine,image,center,5);
            result.Left=new Point(third/2,row+(image.Height-row)/2);
            result.Middle=new Point(third+third/2,row+(image.Height-row)/2);
            bool yes=(ShopText.Yes(la)&&ShopText.Yes(lb))||ShopLabels.Match(image,left,ShopLabels.Yes);
            if(ShopText.Confirmation(a)&&ShopText.Confirmation(b)&&yes&&ShopLabels.Match(image,right,ShopLabels.No))
            {result.Menu=ShopMenu.Confirm;result.Detail="Oferta de cebo en Peli y botón Sí reconocidos";return result;}
            int? ma=ShopText.Maximum(a),mb=ShopText.Maximum(b);
            if(ma.HasValue&&ma==mb&&ShopText.Buy(la)&&ShopText.Buy(lb))
            {
                result.Menu=ShopMenu.Quantity;result.Maximum=ma;
                int? qa=BaitText.Parse(ca),qb=BaitText.Parse(cb);
                if(qa.HasValue&&qa==qb)result.Quantity=qa;
                if(!result.Quantity.HasValue)result.Quantity=ShopLabels.Number(engine,image,center);
                result.Detail="MAX: "+ma+" · cantidad: "+(result.Quantity.HasValue?result.Quantity.Value.ToString():"ilegible");return result;
            }
            if((ShopText.Dots(ca)&&ShopText.Dots(cb))||ThreeDots(image,center))
            {result.Menu=ShopMenu.Done;result.Detail="Botón central «…» reconocido";return result;}
            result.Detail="Menú sin reconocer. Incluye el diálogo y la fila de botones, con poco margen.";
            return result;
        }
        internal static bool ThreeDots(Bitmap image,Rectangle region)
        {
            // Require exactly three small white connected components, aligned and evenly spaced.
            bool[,] ink=new bool[region.Width,region.Height];
            for(int y=0;y<region.Height;y++)for(int x=0;x<region.Width;x++){
                Color c=image.GetPixel(region.X+x,region.Y+y);int min=Math.Min(c.R,Math.Min(c.G,c.B)),max=Math.Max(c.R,Math.Max(c.G,c.B));ink[x,y]=min>185&&max-min<50;
            }
            var blobs=new System.Collections.Generic.List<Rectangle>();
            var queue=new System.Collections.Generic.Queue<Point>();
            for(int y=0;y<region.Height;y++)for(int x=0;x<region.Width;x++)if(ink[x,y]){
                int l=x,r=x,t=y,b=y,pixels=0;queue.Enqueue(new Point(x,y));ink[x,y]=false;
                while(queue.Count>0){Point p=queue.Dequeue();pixels++;l=Math.Min(l,p.X);r=Math.Max(r,p.X);t=Math.Min(t,p.Y);b=Math.Max(b,p.Y);
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int xx=p.X+dx,yy=p.Y+dy;if(xx>=0&&yy>=0&&xx<region.Width&&yy<region.Height&&ink[xx,yy]){ink[xx,yy]=false;queue.Enqueue(new Point(xx,yy));}}
                }
                if(pixels>=2)blobs.Add(Rectangle.FromLTRB(l,t,r+1,b+1));
            }
            if(blobs.Count!=3)return false;blobs.Sort(delegate(Rectangle x,Rectangle y){return x.X.CompareTo(y.X);});
            foreach(var blob in blobs)if(blob.Height<2||blob.Height>12||blob.Width<2||blob.Width>12||Math.Abs(blob.Width-blob.Height)>4)return false;
            int gap1=blobs[1].X-blobs[0].Right,gap2=blobs[2].X-blobs[1].Right;
            return gap1>=1&&gap2>=1&&gap1<=16&&gap2<=16&&Math.Abs(gap1-gap2)<=3
                &&Math.Abs(blobs[0].Y-blobs[1].Y)<=2&&Math.Abs(blobs[1].Y-blobs[2].Y)<=2
                &&Math.Abs((blobs[0].Left+blobs[2].Right)/2-region.Width/2)<region.Width*.2;
        }
        public void Dispose(){disposed=true;latest=new ShopReading();}
    }
}
