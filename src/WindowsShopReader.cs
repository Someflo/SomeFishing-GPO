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
        private readonly string language;
        internal WindowsShopReader(string language="") { this.language=language; }
        internal ShopReading Latest { get { Poll();return latest; } }
        internal bool Due(double now){Poll();return !disposed&&pending==null&&now>=next;}
        internal void Submit(Bitmap frame,Point origin,double now)
        {
            if(disposed||pending!=null){frame.Dispose();return;}
            long seq=++sequence;next=now+1000;
            pending=Task.Run(delegate {
                using(frame){ShopReading result;
                    try{result=ReadImage(frame,language);}catch{result=new ShopReading{ReadFailed=true,Detail="No se pudo leer el menú de compra con Windows OCR"};}
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
        private static OcrResult ReadWords(OcrEngine engine,Bitmap source,Rectangle rect,int scale)
        {using(var crop=source.Clone(rect,PixelFormat.Format32bppArgb))return WindowsBaitReader.RecognizeResult(engine,crop,scale);}
        private static Point Center(Rectangle rect) { return new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2); }
        private static Point? InkTarget(Bitmap image,Rectangle region)
        {
            Rectangle box=ShopLabels.InkBounds(image,region);
            return box.Width>=2&&box.Height>=3&&region.Contains(box)?(Point?)Center(box):null;
        }
        private static Point? WordTarget(OcrResult result,Rectangle region,int scale,Func<string,bool> accepts)
        {
            if(result.TextAngle.HasValue&&Math.Abs(result.TextAngle.Value)>.5)return null;
            Point? target=null;
            foreach(var line in result.Lines)foreach(var word in line.Words)if(accepts(word.Text))
            {
                var rect=word.BoundingRect;
                var point=new Point(region.X+(int)Math.Round((rect.X+rect.Width/2-20)/scale),region.Y+(int)Math.Round((rect.Y+rect.Height/2-20)/scale));
                if(!region.Contains(point)||target.HasValue)return null;
                target=point;
            }
            return target;
        }
        internal static ShopReading ReadImage(Bitmap image,string language="")
        {
            var result=new ShopReading();
            if(image.Width<150||image.Height<80)return result;
            var engine=WindowsBaitReader.CreateEngine(language);
            if(engine==null){result.ReadFailed=true;result.Detail="El idioma OCR elegido no está disponible en Windows";return result;}
            int row=(int)(image.Height*.73),third=image.Width/3;
            var body=new Rectangle(0,0,image.Width,row);
            var left=new Rectangle(0,row,third,image.Height-row);
            var center=new Rectangle(third,row,third,image.Height-row);
            var right=new Rectangle(third*2,row,image.Width-third*2,image.Height-row);
            string a=Read(engine,image,body,2),b=Read(engine,image,body,3);
            OcrResult leftWords=ReadWords(engine,image,left,3),centerWords=ReadWords(engine,image,center,3);
            string la=leftWords.Text,lb=Read(engine,image,left,5);
            string ca=centerWords.Text,cb=Read(engine,image,center,5);
            bool yes=(ShopText.Yes(la)&&ShopText.Yes(lb))||ShopLabels.Match(image,left,ShopLabels.Yes);
            if(ShopText.Confirmation(a)&&ShopText.Confirmation(b)&&yes&&ShopLabels.Match(image,right,ShopLabels.No))
            {
                Point? yesTarget=WordTarget(leftWords,left,3,ShopText.Yes)??InkTarget(image,left);
                if(!yesTarget.HasValue){result.Detail="Oferta reconocida, pero no se localiza Sí para pulsarlo";return result;}
                result.Left=yesTarget.Value;
                result.Menu=ShopMenu.Confirm;result.Detail="Oferta de cebo en Peli · Sí localizado dentro de la zona en "+result.Left.X+", "+result.Left.Y;return result;
            }
            int? ma=ShopText.Maximum(a),mb=ShopText.Maximum(b);
            if(ma.HasValue&&ma==mb&&ShopText.QuantityConfirmation(la)&&ShopText.QuantityConfirmation(lb))
            {
                result.Menu=ShopMenu.Quantity;result.Maximum=ma;
                int? qa=BaitText.Parse(ca),qb=BaitText.Parse(cb);
                if(qa.HasValue&&qa==qb)result.Quantity=qa;
                if(!result.Quantity.HasValue)result.Quantity=ShopLabels.Number(engine,image,center);
                Point? buyTarget=WordTarget(leftWords,left,3,ShopText.QuantityConfirmation)??InkTarget(image,left);
                if(!buyTarget.HasValue){result.Menu=ShopMenu.Unknown;result.Detail="MAX reconocido, pero no se localiza el botón Comprar";return result;}
                result.Left=buyTarget.Value;
                Rectangle numberBox=ShopLabels.InkBounds(image,center);
                Point? numberTarget=WordTarget(centerWords,center,3,delegate(string text){return BaitText.Parse(text)==result.Quantity&&result.Quantity.HasValue;});
                if(!numberTarget.HasValue&&result.Quantity.HasValue&&numberBox.Width<center.Width*.7&&numberBox.Height<center.Height*.9)numberTarget=InkTarget(image,center);
                if(!numberTarget.HasValue){result.Menu=ShopMenu.Unknown;result.Detail="MAX reconocido, pero no se localiza el número para hacer doble clic";return result;}
                result.Middle=numberTarget.Value;
                result.Detail="MAX: "+ma+" · cantidad: "+(result.Quantity.HasValue?result.Quantity.Value.ToString():"ilegible")+" · número en "+result.Middle.X+", "+result.Middle.Y+" · Comprar en "+result.Left.X+", "+result.Left.Y;return result;
            }
            Rectangle dots=ThreeDotsBounds(image,center);
            if(!dots.IsEmpty)
            {result.Middle=Center(dots);result.Menu=ShopMenu.Done;result.Detail="Botón «…» localizado en "+result.Middle.X+", "+result.Middle.Y;return result;}
            result.Detail="Menú sin reconocer. Incluye el diálogo y la fila de botones, con poco margen.";
            return result;
        }
        internal static bool ThreeDots(Bitmap image,Rectangle region)
        { return !ThreeDotsBounds(image,region).IsEmpty; }
        internal static Rectangle ThreeDotsBounds(Bitmap image,Rectangle region)
        {
            // Locate a distinct row of three dots. HUD labels above or below it
            // must not erase the button or pull its click toward the region centre.
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
            if(blobs.Count>100)return Rectangle.Empty;
            blobs.Sort(delegate(Rectangle x,Rectangle y){return x.X.CompareTo(y.X);});
            Rectangle found=Rectangle.Empty;
            for(int i=0;i<blobs.Count;i++)for(int j=i+1;j<blobs.Count;j++)for(int k=j+1;k<blobs.Count;k++)
            {
                Rectangle p=blobs[i],q=blobs[j],r=blobs[k];
                if(!Dot(p)||!Dot(q)||!Dot(r))continue;
                int gap1=q.Left-p.Right,gap2=r.Left-q.Right;
                if(gap1<1||gap2<1||gap1>16||gap2>16||Math.Abs(gap1-gap2)>3||Math.Abs(p.Y-q.Y)>2||Math.Abs(q.Y-r.Y)>2
                    ||Math.Abs(p.Width-q.Width)>2||Math.Abs(q.Width-r.Width)>2||Math.Abs(p.Height-q.Height)>2||Math.Abs(q.Height-r.Height)>2
                    ||Math.Abs((p.Left+r.Right)/2-region.Width/2)>=region.Width*.2)continue;
                Rectangle box=Rectangle.Union(Rectangle.Union(p,q),r);
                bool extra=false;
                for(int n=0;n<blobs.Count;n++)if(n!=i&&n!=j&&n!=k&&blobs[n].Top<=box.Bottom+2&&blobs[n].Bottom>=box.Top-2){extra=true;break;}
                if(extra)continue;
                if(!found.IsEmpty)return Rectangle.Empty;
                found=box;
            }
            if(!found.IsEmpty)found.Offset(region.Location);
            return found;
        }
        private static bool Dot(Rectangle box) { return box.Width>=2&&box.Height>=2&&box.Width<=12&&box.Height<=12&&Math.Abs(box.Width-box.Height)<=4; }
        public void Dispose(){disposed=true;latest=new ShopReading();}
    }
}
