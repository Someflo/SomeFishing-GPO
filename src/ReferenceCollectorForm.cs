using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SomeFishingGPO
{
    internal static class ReferenceCollectorProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0&&args[0]=="--self-test")
            {
                int result=SelfTests.Run(args);if(result!=0)return result;
                return ReferenceCollectorTests.Run(args.Length>1?args[1]:"pruebas");
            }
            Application.Run(new ReferenceCollectorForm(false));return 0;
        }
    }

    internal sealed class ReferenceCaptureStore
    {
        internal string DirectoryPath { get; private set; }
        private readonly Settings settings;
        private readonly Rectangle client;
        private readonly Func<bool> allowed;
        private readonly Func<Rectangle,Bitmap> acquire;
        private bool finished;
        internal ReferenceCaptureStore(string root,Settings settings,Rectangle client,Func<bool> allowed,Func<Rectangle,Bitmap> acquire,int initial)
        {
            this.settings=settings;this.client=client;this.allowed=allowed;this.acquire=acquire;
            DirectoryPath=Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath,"revisar.csv"),"muestra,ronda,cebos_estimados,cebos_reales_revisados,max_revisado,serie_completa\r\n",Encoding.UTF8);
            File.WriteAllText(Path.Combine(DirectoryPath,"LEEME.txt"),
                "Referencias temporales de SomeFishing GPO 0.9.1\r\n"+
                "Cantidad inicial declarada: "+initial+". Maximo de rondas: "+(initial-1)+".\r\n"+
                "Cada serie guarda cebos-a/b ANTES de E y max-a/b tras E > Si.\r\n"+
                "Se cancela el dialogo sin editar cantidades ni pulsar Comprar.\r\n"+
                "Los PNG completos son el area cliente de Roblox, sin cambiar su escala.\r\n"+
                "Los recortes contador/menu son auxiliares; conserva los PNG completos.\r\n"+
                "La estimacion NO etiqueta las imagenes. Rellena cebos_reales_revisados y max_revisado\r\n"+
                "en revisar.csv despues de verlas. Una devolucion puede repetir cantidades.\r\n"+
                "No se interpreta MAX como inventario real. 270 con 30 cebos es una expectativa, no una lectura.\r\n"+
                "La ultima serie puede corresponder a mas de 1 cebo si hubo devoluciones.\r\n"+
                "Para completar faltantes, inicia otra sesion indicando la cantidad REAL que ves (1 a 30).\r\n"+
                "Las capturas pueden incluir nombres y chat del juego. Se guardan solo aqui; no se suben.\r\n",Encoding.UTF8);
        }
        internal void Capture(int round,int estimated,string stage)
        {
            if(!allowed())throw new InvalidOperationException("La ventana cambió antes de la captura.");
            if(round<0||round>29||estimated<1||estimated>30)throw new ArgumentOutOfRangeException("round");
            if(stage!="cebos-a"&&stage!="cebos-b"&&stage!="max-a"&&stage!="max-b"&&stage!="interrumpido")throw new ArgumentException("Etapa no válida.");
            string prefix="muestra-"+round.ToString("000")+"-"+stage;
            using(Bitmap bitmap=acquire(client))
            {
                if(!allowed())throw new InvalidOperationException("La ventana cambió durante la captura.");
                SaveNew(bitmap,Path.Combine(DirectoryPath,prefix+".png"));
                if(stage.StartsWith("cebos-",StringComparison.Ordinal))
                {
                    Crop(bitmap,settings.BaitMenuArea,prefix+"-menu.png");
                    Crop(bitmap,settings.BaitArea,prefix+"-contador.png");
                }
            }
        }
        private void Crop(Bitmap bitmap,Rectangle desktopArea,string name)
        {
            if(desktopArea.Width<1||desktopArea.Height<1||!Settings.ContainsSafely(client,desktopArea))return;
            Rectangle local=desktopArea;local.Offset(-client.X,-client.Y);
            using(Bitmap crop=bitmap.Clone(local,PixelFormat.Format32bppArgb))SaveNew(crop,Path.Combine(DirectoryPath,name));
        }
        private static void SaveNew(Bitmap bitmap,string path)
        {using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))bitmap.Save(output,ImageFormat.Png);}
        internal void CompletePair(int round,int estimated)
        {
            foreach(string stage in new[]{"cebos-a","cebos-b","max-a","max-b"})
                if(!File.Exists(Path.Combine(DirectoryPath,"muestra-"+round.ToString("000")+"-"+stage+".png")))throw new IOException("Falta una captura de la serie.");
            File.AppendAllText(Path.Combine(DirectoryPath,"revisar.csv"),round.ToString("000")+","+round+","+estimated+",,,si\r\n",Encoding.UTF8);
        }
        internal void Finish(string reason)
        {if(finished)return;File.WriteAllText(Path.Combine(DirectoryPath,"resultado.txt"),DateTime.Now.ToString("s")+"\r\n"+reason,Encoding.UTF8);finished=true;}
    }

    internal sealed class ReferenceCollectorForm : Form
    {
        private readonly bool testMode;
        private readonly Timer timer=new Timer{Interval=50};
        private readonly Stopwatch clock=Stopwatch.StartNew();
        private Settings settings;
        private GameRuntime runtime;
        private ReferenceCollector collector;
        private ReferenceCaptureStore store;
        private readonly NumericUpDown count=new NumericUpDown{Minimum=1,Maximum=30,Value=30};
        private readonly CheckBox confirm=new CheckBox();
        private readonly Label status=new Label(),progress=new Label(),profile=new Label(),plan=new Label();
        private Button start,load,folder;
        private bool f10Registered;
        private double armedUntil,sessionStart;
        private string outputRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Referencias");
        internal ReferenceCollectorForm(bool testMode)
        {
            this.testMode=testMode;Text="SomeFishing GPO · Recopilar referencias";
            AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(740,480);Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(245,247,250);
            ForeColor=Color.FromArgb(32,45,60);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
            StartPosition=FormStartPosition.CenterScreen;
            AddLabel("Referencias de cebos",24,20,690,40,22,true);
            AddLabel("Herramienta temporal · pesca, captura y cancela el diálogo",26,65,685,28,11,false);
            AddLabel("Cebos comunes reales al empezar",26,115,370,26,11,true);
            count.SetBounds(26,148,110,32);Controls.Add(count);
            plan.SetBounds(154,150,545,35);Controls.Add(plan);
            confirm.Text="Confirmo la cantidad, estoy junto al vendedor y el diálogo está cerrado.";
            confirm.SetBounds(26,196,690,40);Controls.Add(confirm);
            confirm.CheckedChanged+=delegate{RefreshStart();};
            count.ValueChanged+=delegate{confirm.Checked=false;RefreshPlan();};
            start=AddButton("Iniciar · 3 segundos",26,252,225,delegate{Arm();},true);
            AddButton("Detener · F10",267,252,200,delegate{StopSession("Detenida por el usuario.");},false);
            folder=AddButton("Ver capturas",483,252,230,delegate{OpenOutput();},false);
            progress.SetBounds(26,310,687,32);progress.Font=new Font("Segoe UI Semibold",12);Controls.Add(progress);
            status.SetBounds(26,348,687,62);status.Text="Cierra la macro normal antes de usar esta copia. F8 inicia; F10 detiene.";Controls.Add(status);
            profile.SetBounds(26,420,470,42);profile.Font=new Font("Segoe UI",9);Controls.Add(profile);
            load=AddButton("Cargar ajustes…",528,420,185,delegate{LoadProfile();},false);
            try
            {
                settings=testMode?new Settings():Settings.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ajustes.xml"));
                profile.Text="Puntos y tiempos cargados de esta carpeta. No se modifican los originales.";
            }
            catch(Exception error){settings=null;profile.Text="Carga tus ajustes para continuar.";status.Text=error.Message;}
            RefreshPlan();
            timer.Tick+=Tick;FormClosing+=delegate{StopSession("Aplicación cerrada.");};
            RefreshStart();if(!testMode)timer.Start();
        }
        private void AddLabel(string text,int x,int y,int width,int height,float size,bool bold)
        {Controls.Add(new Label{Text=text,Location=new Point(x,y),Size=new Size(width,height),Font=new Font(bold?"Segoe UI Semibold":"Segoe UI",size)});}
        private Button AddButton(string text,int x,int y,int width,EventHandler action,bool primary)
        {
            var button=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,40),FlatStyle=FlatStyle.Flat,
                BackColor=primary?Color.FromArgb(20,121,101):Color.White,ForeColor=primary?Color.White:ForeColor};
            button.FlatAppearance.BorderColor=Color.FromArgb(217,227,235);button.Click+=action;Controls.Add(button);return button;
        }
        private bool Busy {get{return armedUntil>0||(collector!=null&&collector.Running);}}
        private void RefreshPlan()
        {
            int initial=(int)count.Value;
            plan.Text=initial+" cebos → hasta "+(initial-1)+" rondas → "+initial+" series de capturas";
            if(!Busy)progress.Text="Tiempo 00:00:00    Rondas 0 / "+(initial-1)+"    Series 0 / "+initial;
        }
        private void RefreshStart()
        {
            if(start==null)return;
            start.Enabled=!Busy&&confirm.Checked&&settings!=null&&(testMode||f10Registered)&&runtime==null;
            count.Enabled=confirm.Enabled=load.Enabled=!Busy&&runtime==null;
        }
        protected override void OnHandleCreated(EventArgs args)
        {
            base.OnHandleCreated(args);if(testMode)return;
            f10Registered=Native.RegisterHotKey(Handle,72,0x4000,(uint)Keys.F10);
            bool f8=Native.RegisterHotKey(Handle,71,0x4000,(uint)Keys.F8);
            if(!f10Registered)status.Text="F10 está ocupado. Cierra la macro normal y vuelve a abrir este recopilador.";
            else if(!f8)status.Text="F8 está ocupado; usa Iniciar y vuelve al juego antes de tres segundos.";
            RefreshStart();
        }
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x0312)
            {
                if(message.WParam.ToInt32()==72)StopSession("Detenida con F10.");
                if(message.WParam.ToInt32()==71){if(Busy)StopSession("Detenida con F8.");else Arm();}
            }
            base.WndProc(ref message);
        }
        private void LoadProfile()
        {
            if(Busy||runtime!=null)return;
            using(var dialog=new OpenFileDialog{Filter="Ajustes de SomeFishing (*.xml)|*.xml",Title="Selecciona ajustes.xml o predeterminados.xml"})
            if(dialog.ShowDialog(this)==DialogResult.OK)
            {
                try{settings=Settings.Load(dialog.FileName);profile.Text="Cargado: "+Path.GetFileName(dialog.FileName);confirm.Checked=false;status.Text="Revisa tu cantidad real y confirma antes de iniciar.";}
                catch(Exception error){status.Text=error.Message;}
                RefreshStart();
            }
        }
        private void Arm()
        {
            if(testMode||!start.Enabled)return;
            Settings chosen=ReferenceCollector.RuntimeOptions(settings,(int)count.Value);
            string issue=chosen.Validate(SystemInformation.VirtualScreen,true);
            if(issue!=null){status.Text=issue;return;}
            armedUntil=clock.Elapsed.TotalMilliseconds+3000;
            status.Text="Vuelve a Roblox: iniciará en tres segundos. Debes tener el diálogo cerrado.";RefreshStart();
        }
        private void StartSession()
        {
            armedUntil=0;
            collector=null;store=null;
            try
            {
                IntPtr target=Native.GetForegroundWindow();if(!Native.IsRoblox(target))throw new InvalidOperationException("Roblox debe estar en primer plano. No se inició la recopilación.");
                Settings selected=ReferenceCollector.RuntimeOptions(settings,(int)count.Value);
                Rectangle client=Native.ClientBounds(target);string issue=selected.Validate(client,true);
                if(issue!=null)throw new InvalidOperationException(issue);
                foreach(Point point in new[]{selected.ShopLeftPoint,selected.ShopMiddlePoint,selected.ShopRightPoint})
                    if(!Settings.ContainsSafely(client,ShopVisual.Region(point)))throw new InvalidOperationException("Los botones necesitan margen dentro de Roblox.");
                runtime=new GameRuntime(selected,target,true);
                store=new ReferenceCaptureStore(outputRoot,selected,client,
                    delegate{return runtime!=null&&runtime.IsActive&&Native.ClientBounds(target)==client;},Native.Capture,(int)count.Value);
                collector=new ReferenceCollector(selected,(int)count.Value,runtime,store.Capture,store.CompletePair);
                sessionStart=clock.Elapsed.TotalMilliseconds;collector.Start(sessionStart);
                if(!collector.Running)StopSession(collector.Status);
            }
            catch(Exception error){StopSession(error.Message);}
            RefreshStart();
        }
        private void Tick(object sender,EventArgs args)
        {
            try
            {
                if(runtime!=null&&!Busy&&!runtime.PendingRelease){runtime.Dispose();runtime=null;RefreshStart();}
                double now=clock.Elapsed.TotalMilliseconds;
                if(armedUntil>0){if(now>=armedUntil)StartSession();return;}
                if(collector==null||!collector.Running)return;
                collector.Tick(now);status.Text=collector.Status;
                TimeSpan elapsed=TimeSpan.FromMilliseconds(now-sessionStart);
                progress.Text=string.Format("Tiempo {0:hh\\:mm\\:ss}    Rondas {1} / {2}    Series {3} / {4}",elapsed,collector.Rounds,collector.RoundLimit,collector.Samples,collector.RoundLimit+1);
                if(!collector.Running)StopSession(collector.Status);
            }
            catch(Exception error){StopSession(error.Message);}
        }
        private void StopSession(string reason)
        {
            armedUntil=0;
            try{if(collector!=null&&collector.Running)collector.Stop(reason);}catch(Exception error){reason+=" · "+error.Message;}
            try{if(store!=null)store.Finish(reason);}catch(Exception error){reason+=" · No se guardó resultado.txt: "+error.Message;}
            try{if(runtime!=null){runtime.Dispose();if(!runtime.PendingRelease)runtime=null;}}catch(Exception error){reason+=" · "+error.Message;}
            status.Text=reason;confirm.Checked=false;RefreshStart();
        }
        private void OpenOutput()
        {
            string path=store==null?outputRoot:store.DirectoryPath;
            if(!Directory.Exists(path)){status.Text="Todavía no hay capturas guardadas.";return;}
            Process.Start("explorer.exe","\""+path+"\"");
        }
        protected override bool ShowWithoutActivation {get{return testMode;}}
        internal void Render(string path)
        {
            if(!testMode)throw new InvalidOperationException("El render solo está disponible en pruebas.");
            ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(SystemInformation.VirtualScreen.Right+100,0);
            Show();Application.DoEvents();
            using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(path);}
            Hide();
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing){timer.Stop();timer.Dispose();if(!testMode&&IsHandleCreated){Native.UnregisterHotKey(Handle,71);Native.UnregisterHotKey(Handle,72);}if(runtime!=null)runtime.Dispose();}
            base.Dispose(disposing);
        }
    }
}

