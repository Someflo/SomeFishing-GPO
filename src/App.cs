using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace SomeFishingGPO
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--self-test") return SelfTests.Run(args);
            Application.Run(new MainForm(false));
            return 0;
        }
    }

    internal sealed partial class MainForm : Form
    {
        private readonly Color ink = Color.FromArgb(32, 45, 60);
        private readonly Color muted = Color.FromArgb(103, 119, 135);
        private readonly Color accent = Color.FromArgb(20, 121, 101);
        private readonly bool testMode;
        private readonly string settingsPath;
        private Settings settings;
        private readonly Timer timer = new Timer { Interval = 50 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private FishingEngine engine;
        private GameRuntime runtime;
        private bool previewing, stopHotkey, previewSample;
        private double armedUntil;
        private Label areaLabel, castLabel, statusLabel, detectionLabel, cycleLabel;
        private Button areaButton, pointButton, startButton, previewButton, blueButton, markerButton;
        private CheckBox autoCast, holdUp, allowClicks;
        private NumericUpDown castTime, biteTime, restTime, tolerance, anticipation;
        private PictureBox preview;
        private double nextPreview, runStarted, lastTick, maxTickGap;
        private string lastStop;
        private Phase phaseBeforeTick;
        private SelectionOverlay activePicker;
        private CheckBox monitorBait, idleJump;
        private NumericUpDown jumpSeconds;
        private Button baitAreaButton, baitPreviewButton;
        private Label baitAreaLabel, baitValueLabel, baitDetailLabel;
        private PictureBox baitPreview;
        private WindowsBaitReader previewReader;
        private readonly BaitMonitor previewBaitMonitor = new BaitMonitor();
        private bool previewingBait;
        private CheckBox autoBuy, buyMaximum;
        private NumericUpDown buyQuantity, purchaseLimit, shopOpenTime, purchaseMinutes, testBuyQuantity;
        private ComboBox purchaseMode;
        private Label purchaseCountdown, purchaseModeHint;
        private NumericUpDown baitThreshold, shopSettle, baitCapacity;
        private ComboBox ocrLanguage;
        private readonly System.Collections.Generic.List<string> ocrTags=new System.Collections.Generic.List<string>();
        private Panel timerCondition, ocrCondition;
        private Panel purchaseQuantityCondition, capacityCondition;
        private Button[] shopPointButtons;
        private Label[] shopPointLabels;
        private int selectedShopPoints;
        private Button shopAreaButton, shopPreviewButton;
        private Label shopAreaLabel, shopDetail;
        private PictureBox shopPreview;
        private WindowsShopReader previewShopReader;
        private bool previewingShop;
        private RunKind armedKind;
        private Settings sessionSettings;
        private Button emptyTestButton, purchaseTestButton;
        private TextBox diagnosticLog;
        private string lastDiagnosticStep;
        private bool diagnosticPending;

        public MainForm(bool testMode)
        {
            this.testMode = testMode;
            settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ajustes.xml");
            if (!testMode)
            {
                try
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ultima-parada.txt");
                    if (File.Exists(path) && new FileInfo(path).Length <= 16384) lastStop = File.ReadAllText(path);
                }
                catch { lastStop = "No se pudo leer el informe de la última parada."; }
            }
            string loadWarning = null;
            try { settings = testMode ? new Settings() : Settings.Load(settingsPath); }
            catch (Exception error) { settings = new Settings(); loadWarning = "No se pudieron cargar los ajustes: " + error.Message; }
            Text = "SomeFishing GPO · v0.6.1";
            ClientSize = new Size(1080, 730);
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 10);
            ForeColor = ink; BackColor = Color.FromArgb(245, 247, 250);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BuildInterface();
            ApplySettings();
            if(!testMode)try
            {
                string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ultima-prueba.txt");
                if(File.Exists(report)&&new FileInfo(report).Length<=65536)diagnosticLog.Text=File.ReadAllText(report);
            }
            catch { diagnosticLog.Text="No se pudo leer la última prueba."; }
            if (loadWarning != null) statusLabel.Text = loadWarning;
            timer.Tick += Tick;
            if (!testMode) timer.Start();
        }

        private void ApplySettings()
        {
            autoCast.Checked = settings.AutoCast; holdUp.Checked = settings.HoldMovesUp;
            SetNumber(castTime, settings.CastMilliseconds); SetNumber(biteTime, settings.BiteSeconds);
            SetNumber(restTime, settings.RestMilliseconds); SetNumber(tolerance, settings.Tolerance);
            SetNumber(anticipation, settings.AnticipationMilliseconds);
            monitorBait.Checked = settings.MonitorBait; idleJump.Checked = settings.IdleJumpEnabled;
            SetNumber(jumpSeconds, settings.IdleJumpSeconds);
            autoBuy.Checked=settings.AutoBuyBait;buyMaximum.Checked=settings.BuyMaximum;
            selectedShopPoints=settings.ShopButtonsSet?7:0;
            SetNumber(baitCapacity,settings.BaitCapacity);
            SetNumber(buyQuantity,settings.BuyQuantity);SetNumber(purchaseLimit,settings.PurchaseLimit);
            SetNumber(purchaseMinutes,settings.PurchaseIntervalMinutes);SetNumber(testBuyQuantity,settings.TestBuyQuantity);
            purchaseMode.SelectedIndex=settings.PurchaseByTimer?1:0;UpdatePurchaseControls(true);
            SetNumber(shopOpenTime,settings.ShopOpenMilliseconds);
            SetNumber(baitThreshold,settings.BuyBaitAt);SetNumber(shopSettle,settings.ShopSettleMilliseconds);
            int languageIndex=ocrTags.IndexOf(settings.OcrLanguage??"");
            if(languageIndex<0){ocrTags.Add(settings.OcrLanguage);ocrLanguage.Items.Add("No disponible: "+settings.OcrLanguage);languageIndex=ocrTags.Count-1;}
            ocrLanguage.SelectedIndex=languageIndex;
            UpdateAreaLabels();
        }
        private static void SetNumber(NumericUpDown control, int value)
        { control.Value = Math.Max(control.Minimum, Math.Min(control.Maximum, value)); }
        private Settings ReadSettings()
        {
            return new Settings { Area = settings.Area, CastPoint = settings.CastPoint, CastPointSet = settings.CastPointSet,
                AutoCast = autoCast.Checked, HoldMovesUp = holdUp.Checked,
                CastMilliseconds = (int)castTime.Value, BiteSeconds = (int)biteTime.Value,
                RestMilliseconds = (int)restTime.Value, Tolerance = (int)tolerance.Value,
                AnticipationMilliseconds = (int)anticipation.Value,
                BlueArgb = settings.BlueArgb, MarkerArgb = settings.MarkerArgb,
                MonitorBait = monitorBait.Checked||(autoBuy.Checked&&purchaseMode.SelectedIndex==0), BaitArea = settings.BaitArea,
                IdleJumpEnabled = idleJump.Checked, IdleJumpSeconds = (int)jumpSeconds.Value,
                AutoBuyBait=autoBuy.Checked,ShopArea=settings.ShopArea,BuyMaximum=false,
                UseDirectShopFlow=true,ShopButtonsSet=settings.ShopButtonsSet,
                ShopLeftPoint=settings.ShopLeftPoint,ShopMiddlePoint=settings.ShopMiddlePoint,ShopRightPoint=settings.ShopRightPoint,
                BaitCapacity=(int)baitCapacity.Value,
                BuyQuantity=(int)buyQuantity.Value,PurchaseLimit=(int)purchaseLimit.Value,ShopOpenMilliseconds=(int)shopOpenTime.Value,
                PurchaseByTimer=purchaseMode.SelectedIndex==1,PurchaseIntervalMinutes=(int)purchaseMinutes.Value,TestBuyQuantity=(int)testBuyQuantity.Value,
                BuyBaitAt=(int)baitThreshold.Value,ShopSettleMilliseconds=(int)shopSettle.Value,OcrLanguage=ocrLanguage.SelectedIndex>=0?ocrTags[ocrLanguage.SelectedIndex]:"" };
        }
        private void UpdateAreaLabels()
        {
            areaLabel.Text=settings.Area.IsEmpty?"Sin seleccionar":string.Format("Zona: {0} × {1} px",settings.Area.Width,settings.Area.Height);
            areaLabel.ForeColor=settings.Area.IsEmpty?muted:accent;
            areaButton.Text=settings.Area.IsEmpty?"Seleccionar zona · F6":"Cambiar zona · F6";
            castLabel.Text=settings.CastPointSet?"Punto de lanzamiento listo":"Punto sin seleccionar";
            castLabel.ForeColor=settings.CastPointSet?accent:muted;
            ((ModernButton)blueButton).SwatchColor=Color.FromArgb(settings.BlueArgb);
            ((ModernButton)markerButton).SwatchColor=Color.FromArgb(settings.MarkerArgb);
            blueButton.Invalidate();markerButton.Invalidate();
            baitAreaLabel.Text=settings.BaitArea.IsEmpty?"Sin seleccionar":string.Format("Zona: {0} × {1} px",settings.BaitArea.Width,settings.BaitArea.Height);
            baitAreaLabel.ForeColor=settings.BaitArea.IsEmpty?muted:accent;
            string pointIssue=settings.ShopButtonsSet?settings.ValidateShopButtons(SystemInformation.VirtualScreen):null;
            shopAreaLabel.Text=settings.ShopButtonsSet?(pointIssue??"Los 3 botones están listos."):"Marca los 3 botones antes de comprar.";
            shopAreaLabel.ForeColor=settings.ShopButtonsSet&&pointIssue==null?accent:muted;
            Point[] positions={settings.ShopLeftPoint,settings.ShopMiddlePoint,settings.ShopRightPoint};
            for(int i=0;i<shopPointLabels.Length;i++){
                bool marked=(selectedShopPoints&(1<<i))!=0;
                shopPointLabels[i].Text=marked?"Punto guardado: "+positions[i].X+", "+positions[i].Y:"Sin marcar";
                shopPointLabels[i].ForeColor=marked?accent:muted;
            }
            Hint(areaLabel,"Zona del minijuego: "+settings.Area);
            Hint(castLabel,settings.CastPointSet?"Punto en el agua: "+settings.CastPoint:"Equipa la caña y elige un punto sobre el agua.");
            Hint(baitAreaLabel,"Zona del contador: "+settings.BaitArea);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (testMode) return;
            bool toggle = Native.RegisterHotKey(Handle, 1, 0x4000, (uint)Keys.F8);
            stopHotkey = Native.RegisterHotKey(Handle, 2, 0x4000, (uint)Keys.F10);
            Native.RegisterHotKey(Handle, 3, 0x4000, (uint)Keys.F6);
            if (!stopHotkey) statusLabel.Text = "F10 está ocupado. Cierra la otra macro y vuelve a abrir esta aplicación.";
            else if (!toggle) statusLabel.Text = "F8 está ocupado. Puedes usar el botón de inicio y F10 para parar.";
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0312)
            {
                int id = message.WParam.ToInt32();
                if (id == 2) { if (activePicker != null) activePicker.CancelSelection(); StopAll("Detenida con F10"); }
                if (id == 1 && activePicker == null)
                {
                    if (IsRunning || armedUntil > 0) StopAll("Detenida con F8");
                    else StartNow();
                }
                if (id == 3)
                {
                    if (activePicker != null) activePicker.TryConfirm();
                    else if (!IsRunning && armedUntil == 0) SelectArea();
                }
            }
            base.WndProc(ref message);
        }
        private bool IsRunning { get { return engine != null && engine.Running; } }
        private void SelectShopPoint(int index)
        {
            if(activePicker!=null)return;
            string[] names={"IZQUIERDA · SÍ / COMPRAR","CENTRO · NÚMERO / …","DERECHA · NO / CANCELAR"};
            StopAll("Marcando botón de compra…");Hide();
            try{using(var picker=new SelectionOverlay(true)){
                picker.PointTitle="MARCA "+names[index];
                picker.PointHelp="Haz clic en el centro de ese botón. Solo guarda el punto; no pulsa en el juego. Esc cancela.";
                activePicker=picker;
                if(picker.ShowDialog()==DialogResult.OK){
                    Point point=picker.Selection.Location;
                    if(index==0)settings.ShopLeftPoint=point;else if(index==1)settings.ShopMiddlePoint=point;else settings.ShopRightPoint=point;
                    selectedShopPoints|=1<<index;settings.ShopButtonsSet=selectedShopPoints==7;
                    UpdateAreaLabels();ReadSettings().Save(settingsPath);
                    string issue=settings.ShopButtonsSet?settings.ValidateShopButtons(SystemInformation.VirtualScreen):null;
                    statusLabel.Text=issue??(settings.ShopButtonsSet?"Tres puntos guardados. Cierra el diálogo y prueba una compra.":"Punto guardado. Marca los botones que faltan.");
                }else statusLabel.Text="Selección cancelada. Se conserva el punto anterior.";
            }}catch(Exception error){statusLabel.Text=error.Message;}finally{activePicker=null;Show();Activate();}
        }
        private void SelectShopArea()
        {
            if(activePicker!=null)return;StopAll("Seleccionando diálogo de compra…");Hide();
            try{using(var picker=new SelectionOverlay(false,settings.ShopArea,null,SystemInformation.VirtualScreen,false,true)){
                activePicker=picker;if(picker.ShowDialog()==DialogResult.OK){settings.ShopArea=picker.Selection;UpdateAreaLabels();ReadSettings().Save(settingsPath);statusLabel.Text="Zona guardada. Prueba cada menú antes de habilitar las compras.";}
            }}catch(Exception error){statusLabel.Text=error.Message;}finally{activePicker=null;Show();Activate();}
        }
        private void ToggleShopPreview()
        {
            bool wasActive=previewingShop;StopAll("Prueba de compra detenida");if(wasActive)return;
            string issue=Settings.ValidateShopArea(settings.ShopArea,SystemInformation.VirtualScreen);if(issue!=null){statusLabel.Text=issue;return;}
            previewShopReader=new WindowsShopReader(ReadSettings().OcrLanguage);previewingShop=true;shopPreviewButton.Text="Detener lectura";statusLabel.Text="Solo lee los menús · no compra ni envía teclas";
        }
        private void SelectBaitArea()
        {
            if (activePicker != null) return;
            StopAll("Seleccionando contador…"); Hide();
            try
            {
                using (var picker = new SelectionOverlay(false, settings.BaitArea, null, SystemInformation.VirtualScreen, true))
                {
                    activePicker = picker;
                    if (picker.ShowDialog() == DialogResult.OK)
                    {
                        settings.BaitArea = picker.Selection; monitorBait.Checked = true;
                        UpdateAreaLabels(); ReadSettings().Save(settingsPath);
                        statusLabel.Text = "Contador guardado. Usa «Probar lectura» antes de iniciar.";
                    }
                }
            }
            catch (Exception error) { statusLabel.Text = error.Message; }
            finally { activePicker = null; Show(); Activate(); }
        }
        private void ToggleBaitPreview()
        {
            bool wasActive = previewingBait; StopAll("Lectura del contador detenida");
            if (wasActive) return;
            string problem = Settings.ValidateBaitArea(settings.BaitArea, SystemInformation.VirtualScreen);
            if (problem != null) { statusLabel.Text = problem; return; }
            previewReader = new WindowsBaitReader(ReadSettings().OcrLanguage); previewBaitMonitor.Reset(); previewingBait = true;
            baitPreviewButton.Text = "Detener lectura";
            statusLabel.Text = "Solo lectura del contador · sin clics ni saltos";
        }
        private void ShowBaitCount(int? count, string detail)
        {
            baitValueLabel.Text = "Cebos: " + (count.HasValue ? count.Value.ToString() : "—");
            baitValueLabel.ForeColor = count.HasValue && count.Value <= 10 ? Color.FromArgb(174,85,15) : ink;
            baitDetailLabel.Text = count.HasValue&&autoBuy.Checked&&purchaseMode.SelectedIndex==0&&count.Value>(int)baitThreshold.Value&&count.Value<=(int)baitThreshold.Value+2?
                "Compra próxima · se activa con "+baitThreshold.Value+" cebos o menos.":detail ?? "Esperando lectura…";
        }
        private void SelectArea()
        {
            if (activePicker != null) return;
            StopAll("Seleccionando zona…"); Hide();
            try
            {
                using (var picker = new SelectionOverlay(false, settings.Area))
                {
                    activePicker = picker;
                    if (picker.ShowDialog() == DialogResult.OK)
                    {
                        settings.Area = picker.Selection; UpdateAreaLabels();
                        ReadSettings().Save(settingsPath);
                        statusLabel.Text = "Zona guardada. Usa «Ver detector» para comprobarla.";
                    }
                    else statusLabel.Text = "Selección cancelada. Se conserva la zona anterior.";
                }
            }
            catch (Exception error) { statusLabel.Text = error.Message; }
            finally { activePicker = null; Show(); Activate(); }
        }
        private void SelectPoint()
        {
            if (activePicker != null) return;
            StopAll("Seleccionando punto de lanzamiento…"); Hide();
            try
            {
                using (var picker = new SelectionOverlay(true))
                {
                    activePicker = picker;
                    if (picker.ShowDialog() == DialogResult.OK)
                    { settings.CastPoint = picker.Selection.Location; settings.CastPointSet = true; UpdateAreaLabels(); }
                }
            }
            catch (Exception error) { statusLabel.Text = error.Message; }
            finally { activePicker = null; Show(); Activate(); }
        }
        private void PickColor(bool blue)
        {
            StopAll("Ajustando color…");
            using (var dialog = new ColorDialog { FullOpen = true, Color = Color.FromArgb(blue ? settings.BlueArgb : settings.MarkerArgb) })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                { if (blue) settings.BlueArgb = dialog.Color.ToArgb(); else settings.MarkerArgb = dialog.Color.ToArgb(); UpdateAreaLabels(); }
        }
        private bool CanStart()
        {
            if (runtime != null && runtime.PendingRelease)
            { statusLabel.Text = "Hay una liberación de clic pendiente. Espera antes de iniciar otra ronda."; return false; }
            if (!stopHotkey) { statusLabel.Text = "F10 no está disponible. Cierra otras macros y vuelve a abrir SomeFishing GPO."; return false; }
            if (!allowClicks.Checked) { statusLabel.Text = "Marca «Permitir clics y teclas» antes de iniciar. Puedes usar las vistas de prueba."; return false; }
            string problem = ReadSettings().Validate(SystemInformation.VirtualScreen, true);
            if (problem != null) { statusLabel.Text = problem; return false; }
            return true;
        }
        private void Arm()
        {
            if (IsRunning || armedUntil > 0) { StopAll("Detenida por ti"); return; }
            if (!CanStart()) return;
            StopAll("Preparando inicio…");
            armedKind=RunKind.Fishing; armedUntil = clock.Elapsed.TotalMilliseconds + 3000;SetEditable(false);
            statusLabel.Text = "Vuelve a Roblox: inicio en 3 segundos. F10 cancela.";
        }
        private static string DiagnosticName(RunKind kind)
        {return kind==RunKind.EmptyBaitTest?"SIN CEBO SIMULADO":"COMPRA REAL";}
        private bool CanStartDiagnostic(RunKind kind)
        {
            string issue=runtime!=null&&runtime.PendingRelease?"Hay una liberación de entrada pendiente. Espera antes de probar.":
                !stopHotkey?"F10 no está disponible. Cierra otras macros y vuelve a abrir SomeFishing GPO.":
                ReadSettings().ForDiagnostic(kind).ValidateDiagnostic(SystemInformation.VirtualScreen);
            if(issue==null)return true;
            statusLabel.Text=issue;AppendDiagnostic("No se inició: "+issue);return false;
        }
        private void ArmDiagnostic(RunKind kind)
        {
            StopAll("Preparando prueba…");
            diagnosticLog.Clear();lastDiagnosticStep=null;
            AppendDiagnostic("SomeFishing GPO 0.6.1 · "+DiagnosticName(kind));
            if(testMode){AppendDiagnostic("Render de interfaz: entradas reales desactivadas.");return;}
            if(!CanStartDiagnostic(kind))return;
            Settings selected=ReadSettings().ForDiagnostic(kind);
            AppendDiagnostic("Compra: "+selected.AutoBuyBait+" · saltos: "+selected.IdleJumpEnabled+" · lector: "+selected.UsesBaitCounter+" · modo: "+(selected.PurchaseByTimer?"Cronómetro":"Contador OCR"));
            AppendDiagnostic("Tres puntos manuales · pausa entre pasos: "+selected.ShopSettleMilliseconds+" ms");
            if(selected.AutoBuyBait)AppendDiagnostic("Mantener E: "+selected.ShopOpenMilliseconds+" ms · cantidad de prueba: "+selected.BuyQuantity);
            if(selected.Area.IsEmpty)AppendDiagnostic("Sin zona de pesca: comprueba manualmente que no esté abierto el minijuego.");
            if(selected.AutoBuyBait)AppendDiagnostic("Prueba inmediata, sin OCR de menús. Movimiento relativo y comprobación de botones verde/blanco/rojo. Comprueba la cantidad y el cierre en el juego.");
            AppendDiagnostic("Preparada. Vuelve a Roblox en 3 s. F10/F8 cancela. Como máximo 1 compra de "+selected.BuyQuantity+" cebos o 1 salto.");
            armedKind=kind;diagnosticPending=true;armedUntil=clock.Elapsed.TotalMilliseconds+3000;SetEditable(false);
            statusLabel.Text="Prueba preparada · vuelve a Roblox en 3 s. F10 cancela.";
        }
        private void AppendDiagnostic(string message)
        {
            if(diagnosticLog==null)return;
            diagnosticLog.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+message+Environment.NewLine);
            if(diagnosticLog.TextLength>32000)diagnosticLog.Text=diagnosticLog.Text.Substring(diagnosticLog.TextLength-28000);
            diagnosticLog.SelectionStart=diagnosticLog.TextLength;diagnosticLog.ScrollToCaret();
            if(!testMode)try{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ultima-prueba.txt"),diagnosticLog.Text);}
            catch{statusLabel.Text="No se pudo guardar ultima-prueba.txt; el resultado sigue visible en Pruebas.";}
        }
        private void TraceDiagnostic()
        {
            if(engine==null||!engine.IsDiagnostic)return;
            string step=engine.Status+"\r\n    "+engine.PurchaseDetail+"\r\n    Cebos: "+
                (engine.BaitCount.HasValue?engine.BaitCount.Value.ToString():"desconocido")+" · "+engine.BaitStatus;
            if(runtime!=null)step+="\r\n    Entrada: "+runtime.InputStatus;
            if(step==lastDiagnosticStep)return;
            lastDiagnosticStep=step;AppendDiagnostic(step);
        }
        private void StartNow(RunKind kind=RunKind.Fishing)
        {
            bool diagnostic=kind!=RunKind.Fishing;
            if (diagnostic?!CanStartDiagnostic(kind):!CanStart()) {StopAll(statusLabel.Text);return;}
            IntPtr target = Native.GetForegroundWindow();
            if (!Native.IsRoblox(target)) { StopAll(diagnostic?"Prueba cancelada: Roblox no estaba en primer plano. Pulsa de nuevo el botón de prueba.":"Vuelve a la ventana de Roblox y pulsa F8 para iniciar."); return; }
            diagnosticPending=false;StopAll("Iniciando…");
            previewSample = false;
            try
            {
                if(diagnostic)sessionSettings=ReadSettings().ForDiagnostic(kind);
                else{settings=ReadSettings();SaveSettingsQuietly();sessionSettings=settings;}
                diagnosticPending=diagnostic;
                runtime = new GameRuntime(sessionSettings, target, true, !diagnostic);
                engine = new FishingEngine(sessionSettings, runtime,kind);
                diagnosticPending=false;
                runStarted = lastTick = clock.Elapsed.TotalMilliseconds; maxTickGap = 0; nextPreview = 0;
                engine.Start(runStarted);
                statusLabel.Text = engine.Status;
                startButton.Text = "Detener · F8";
                SetEditable(false);
                TraceDiagnostic();
            }
            catch (Exception error) { StopAll(error.Message); }
        }
        private void SetEditable(bool value)
        {
            foreach (Control control in new Control[] { areaButton, pointButton, autoCast, castTime, biteTime, restTime,
                tolerance, anticipation, holdUp, blueButton, markerButton, allowClicks,
                monitorBait, idleJump, jumpSeconds, baitAreaButton, baitPreviewButton,
                autoBuy,buyMaximum,buyQuantity,purchaseLimit,shopOpenTime,shopAreaButton,shopPreviewButton,
                purchaseMode,purchaseMinutes,testBuyQuantity,baitThreshold,shopSettle,baitCapacity,ocrLanguage,emptyTestButton,purchaseTestButton }) control.Enabled = value;
            foreach(var button in shopPointButtons)button.Enabled=value;
            UpdatePurchaseControls(value);
        }
        private void StopAll(string reason)
        {
            bool hadSession = engine != null;
            bool wasDiagnostic=diagnosticPending||(hadSession&&engine.IsDiagnostic);
            diagnosticPending=false;
            Phase phase = hadSession ? (engine.Running ? engine.State : phaseBeforeTick) : Phase.Stopped;
            armedUntil = 0; previewing = false;
            previewingBait = false;
            if (previewReader != null) { previewReader.Dispose(); previewReader = null; }
            previewingShop=false;if(previewShopReader!=null){previewShopReader.Dispose();previewShopReader=null;}
            try { if (engine != null) engine.Stop(reason); }
            catch (Exception error) { reason += " · Revisa el botón del ratón: " + error.Message; }
            try { if (runtime != null) runtime.Dispose(); }
            catch (Exception error) { reason += " · " + error.Message; }
            if (runtime != null && runtime.FaultReason != null) reason = runtime.FaultReason;
            if(wasDiagnostic)
            {
                TraceDiagnostic();
                AppendDiagnostic("FIN: "+reason+(hadSession?"\r\n    Intentos: "+engine.PurchaseAttempts+" · Comprar enviado: "+engine.PurchaseSubmitted+" · Espacios solicitados: "+engine.JumpRequests:" · No se enviaron entradas de la prueba."));
            }
            if (hadSession && !testMode)
            {
                lastStop = string.Format("SomeFishing GPO 0.6.1 · {0:yyyy-MM-dd HH:mm:ss}\r\n\r\n{1}\r\n\r\n" +
                    "Estado al parar: {2}\r\nRondas terminadas: {3}\r\nDuración: {4:F1} s\r\n" +
                    "Mayor intervalo entre revisiones: {5:F0} ms\r\nLanzamiento: {6} ms · Espera: {7} s\r\n" +
                    "Anticipación: {8} ms · Tolerancia: {9}\r\nÚltima detección: {10}\r\n" +
                    "Cebos: {11} · Saltos habilitados: {12} · Espacios solicitados: {13}\r\n" +
                    "Intentos de reposición: {14} · Última compra enviada: {15}\r\n",
                    DateTime.Now, reason.Length > 2000 ? reason.Substring(0, 2000) : reason, phase, engine.Cycles,
                    (clock.Elapsed.TotalMilliseconds - runStarted) / 1000, maxTickGap,
                    sessionSettings.CastMilliseconds, sessionSettings.BiteSeconds, sessionSettings.AnticipationMilliseconds, sessionSettings.Tolerance,
                    engine.LastObservation == null ? "Sin imagen" : engine.LastObservation.Detail,
                    engine.BaitCount.HasValue ? engine.BaitCount.Value.ToString() : "Desconocido", sessionSettings.IdleJumpEnabled, engine.JumpRequests,engine.PurchaseAttempts,engine.PurchaseSubmitted);
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ultima-parada.txt"), lastStop); }
                catch (Exception error) { reason += " · No se pudo guardar el informe: " + error.Message; }
            }
            if (runtime == null || !runtime.PendingRelease) runtime = null;
            engine = null;
            sessionSettings=null;
            if (statusLabel != null) statusLabel.Text = reason;
            if (startButton != null) startButton.Text = "Iniciar · 3 s";
            if (previewButton != null) previewButton.Text = "Ver detector";
            if (baitPreviewButton != null) baitPreviewButton.Text = "Probar lectura";
            if(shopPreviewButton!=null)shopPreviewButton.Text="Probar menú";
            if (areaButton != null) SetEditable(true);
        }
        private void ShowLastStop()
        {
            MessageBox.Show(this, lastStop ?? "Todavía no hay una sesión detenida registrada.", "Última parada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void TogglePreview()
        {
            bool wasPreviewing = previewing;
            StopAll("Vista previa detenida");
            if (wasPreviewing) return;
            string issue = ReadSettings().Validate(SystemInformation.VirtualScreen, false);
            if (issue != null) { statusLabel.Text = issue; return; }
            previewing = true; previewSample = false; previewButton.Text = "Detener vista";
            statusLabel.Text = "Vista previa · solo observa, no envía clics";
        }
        private void ShowExample()
        {
            StopAll("Ejemplo · no envía clics");
            previewSample = true;
            using (Bitmap sample = SelfTests.CreateWideSample(60))
                SetPreview(sample, Detector.Analyze(sample, new Settings()));
        }
        private void Tick(object sender, EventArgs args)
        {
            if (!IsRunning && runtime != null && !runtime.PendingRelease) { runtime.Dispose(); runtime = null; }
            double now = clock.Elapsed.TotalMilliseconds;
            purchaseCountdown.Text=IsRunning?engine.TimerStatus(now):"El cronómetro empieza al iniciar la pesca.";
            if (armedUntil > 0)
            {
                if (now >= armedUntil) { armedUntil = 0; StartNow(armedKind); }
                else statusLabel.Text = (armedKind==RunKind.Fishing?"Vuelve a Roblox: inicio en ":"Prueba: vuelve a Roblox en ")+ Math.Ceiling((armedUntil - now) / 1000) + " s. F10 cancela.";
                return;
            }
            try
            {
                if (IsRunning)
                {
                    maxTickGap = Math.Max(maxTickGap, now - lastTick); lastTick = now;
                    phaseBeforeTick = engine.State;
                    engine.Tick(now);
                    TraceDiagnostic();
                    if (runtime.LastFrame != null && now >= nextPreview)
                    { SetPreview(runtime.LastFrame, engine.LastObservation); nextPreview = now + 150; }
                    cycleLabel.Text = "Rondas: " + engine.Cycles + " · Cebos: " + (engine.BaitCount.HasValue ? engine.BaitCount.Value.ToString() : "—");
                    ShowBaitCount(engine.BaitCount, engine.BaitStatus);
                    statusLabel.Text = engine.Status;
                    if (!engine.Running) { string reason = engine.Status; StopAll(reason); }
                }
                else if (previewing)
                {
                    Settings current = ReadSettings();
                    string issue = current.Validate(SystemInformation.VirtualScreen, false);
                    if (issue != null) { StopAll(issue); return; }
                    using (Bitmap frame = Native.Capture(current.Area)) SetPreview(frame, Detector.Analyze(frame, current));
                }
                else if (previewingBait && previewReader != null)
                {
                    if (previewReader.Due(now))
                    {
                        Bitmap frame = Native.Capture(settings.BaitArea);
                        Image old = baitPreview.Image; baitPreview.Image = (Bitmap)frame.Clone(); if (old != null) old.Dispose();
                        previewReader.Submit(frame, now);
                    }
                    previewBaitMonitor.Update(previewReader.Latest, now);
                    ShowBaitCount(previewBaitMonitor.Count, previewBaitMonitor.Detail);
                }
                else if(previewingShop&&previewShopReader!=null)
                {
                    if(previewShopReader.Due(now)){
                        Bitmap frame=Native.Capture(settings.ShopArea);Image old=shopPreview.Image;shopPreview.Image=(Bitmap)frame.Clone();if(old!=null)old.Dispose();
                        previewShopReader.Submit(frame,settings.ShopArea.Location,now);
                    }
                    shopDetail.Text=previewShopReader.Latest.Detail;
                }
            }
            catch (Exception error) { StopAll("Error: " + error.Message); }
        }
        private void SetPreview(Bitmap frame, Observation observation)
        {
            var display = new Bitmap(frame.Width + 30, frame.Height);
            using (Graphics graphics = Graphics.FromImage(display))
            {
                graphics.Clear(Color.FromArgb(17, 31, 39)); graphics.DrawImageUnscaled(frame, 15, 0);
                Rectangle bar = observation == null ? Rectangle.Empty : observation.BarBounds;
                if (!bar.IsEmpty)
                {
                    bar.Offset(15, 0);
                    using (var outline = new Pen(Color.FromArgb(60, 225, 255), 2))
                        graphics.DrawRectangle(outline, bar.X, bar.Y + 1, bar.Width - 1, bar.Height - 3);
                }
                if (observation != null && observation.Found)
                {
                    int left = bar.IsEmpty ? 15 : bar.Left;
                    int right = bar.IsEmpty ? display.Width - 15 : bar.Right;
                    using (var zone = new SolidBrush(Color.FromArgb(85, 255, 181, 70)))
                        graphics.FillRectangle(zone, left, observation.GapTop, right-left, observation.GapBottom - observation.GapTop + 1);
                    using (var target = new Pen(Color.FromArgb(255, 181, 70), 2)) graphics.DrawLine(target, left, (float)observation.GapY, right, (float)observation.GapY);
                    using (var marker = new Pen(Color.FromArgb(255, 116, 187), 2)) graphics.DrawLine(marker, Math.Max(0,left-7), (float)observation.FishY, Math.Min(display.Width,right+7), (float)observation.FishY);
                }
            }
            Image old = preview.Image; preview.Image = display; if (old != null) old.Dispose();
            detectionLabel.Text = (previewSample ? "Ejemplo · " : "") + (observation == null ? "Esperando imagen…" : observation.Detail);
        }
        private void SaveSettingsQuietly()
        {
            try { ReadSettings().Save(settingsPath); }
            catch (Exception error) { statusLabel.Text = "No se guardaron los ajustes: " + error.Message; }
        }
        private void SaveSettings()
        {
            try { settings = ReadSettings(); settings.Save(settingsPath); statusLabel.Text = "Ajustes guardados junto al programa."; }
            catch (Exception error) { statusLabel.Text = "No se guardaron los ajustes: " + error.Message; }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop(); StopAll("Cerrando");
            if (!testMode)
            {
                SaveSettingsQuietly();
                for (int id = 1; id <= 3; id++) Native.UnregisterHotKey(Handle, id);
            }
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                hints.Dispose();
                if (runtime != null) runtime.Dispose();
                if (previewReader != null) previewReader.Dispose();
                if(previewShopReader!=null)previewShopReader.Dispose();
                if(shopPreview!=null&&shopPreview.Image!=null){shopPreview.Image.Dispose();shopPreview.Image=null;}
                if (baitPreview != null && baitPreview.Image != null) { baitPreview.Image.Dispose(); baitPreview.Image = null; }
                if (preview != null && preview.Image != null) { preview.Image.Dispose(); preview.Image = null; }
            }
            base.Dispose(disposing);
        }
        internal void RenderExample(string path)
        {
            // An off-screen, non-activating form lets WinForms initialize child controls
            // for bitmap rendering. Test mode has no timer, hotkeys or game runtime.
            ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            Location = new Point(SystemInformation.VirtualScreen.Right + 100, 0);
            ShowExample(); Show(); Application.DoEvents();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path); }
            for(int i=1;i<pages.Length;i++)
            {
                SelectPage(i);Application.DoEvents();
                using(var bitmap=new Bitmap(Width,Height))
                {DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(Path.GetDirectoryName(path),"interfaz-"+i+".png"));}
            }
            purchaseMode.SelectedIndex=1;SelectPage(2);Application.DoEvents();
            using(var bitmap=new Bitmap(Width,Height))
            {DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(Path.GetDirectoryName(path),"cronometro.png"));}
            Close();
        }
        protected override bool ShowWithoutActivation { get { return testMode; } }
    }

    internal sealed class SelectionOverlay : Form
    {
        private readonly bool pointOnly;
        private readonly bool counterOnly;
        private readonly bool shopOnly;
        private readonly Bitmap screenshot;
        private Point origin, current;
        private bool dragging;
        private Rectangle draft;
        private string selectionHint;
        internal string PointTitle="ELIGE EL PUNTO DE LANZAMIENTO";
        internal string PointHelp="Haz clic sobre el agua. Esc cancela.";
        public Rectangle Selection { get; private set; }
        public SelectionOverlay(bool pointOnly) : this(pointOnly, Rectangle.Empty) { }
        public SelectionOverlay(bool pointOnly, Rectangle initialArea)
            : this(pointOnly, initialArea, null, SystemInformation.VirtualScreen) { }
        internal SelectionOverlay(bool pointOnly, Rectangle initialArea, Bitmap background, Rectangle desktop, bool counterOnly = false, bool shopOnly = false)
        {
            this.pointOnly = pointOnly; this.counterOnly = counterOnly; this.shopOnly=shopOnly;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
            Bounds = desktop; TopMost = true; ShowInTaskbar = false;
            Cursor = Cursors.Cross; KeyPreview = true; DoubleBuffered = true;
            screenshot = background == null ? Native.Capture(Bounds) : (Bitmap)background.Clone();
            if (!pointOnly && Settings.ContainsSafely(desktop, initialArea))
            { draft = initialArea; draft.Offset(-desktop.X, -desktop.Y); }
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) CancelSelection();
                else if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; TryConfirm(); }
            };
        }
        public void CancelSelection() { DialogResult = DialogResult.Cancel; Close(); }
        public bool TryConfirm()
        {
            if (pointOnly || dragging) return false;
            Rectangle proposed = draft; proposed.Offset(Bounds.Location);
            selectionHint = shopOnly?Settings.ValidateShopArea(proposed,Bounds):counterOnly ? Settings.ValidateBaitArea(proposed, Bounds) : new Settings { Area = proposed, AutoCast = false }.Validate(Bounds, false);
            if (selectionHint != null) { Invalidate(); return false; }
            Selection = proposed; DialogResult = DialogResult.OK; Close(); return true;
        }
        private Point Clamp(Point point)
        { return new Point(Math.Max(0, Math.Min(ClientSize.Width, point.X)), Math.Max(0, Math.Min(ClientSize.Height, point.Y))); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            origin = current = Clamp(e.Location); draft = Rectangle.Empty; selectionHint = null;
            dragging = true; Capture = true; Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e) { current = Clamp(e.Location); if (dragging) Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging || e.Button != MouseButtons.Left) return;
            dragging = false; Capture = false; current = Clamp(e.Location);
            if (pointOnly)
            {
                Rectangle rect = new Rectangle(current, new Size(1, 1)); rect.Offset(Bounds.Location);
                if (!Settings.ContainsSafely(Bounds, rect)) return;
                Selection = rect; DialogResult = DialogResult.OK; Close();
            }
            else { draft = DragRectangle(); Invalidate(); }
        }
        private Rectangle DragRectangle()
        { return Rectangle.FromLTRB(Math.Min(origin.X, current.X), Math.Min(origin.Y, current.Y), Math.Max(origin.X, current.X), Math.Max(origin.Y, current.Y)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(screenshot, 0, 0);
            using (var shade = new SolidBrush(Color.FromArgb(100, 0, 0, 0))) e.Graphics.FillRectangle(shade, ClientRectangle);
            Rectangle rect = dragging ? DragRectangle() : draft;
            if (!pointOnly && rect.Width > 0 && rect.Height > 0)
            {
                e.Graphics.DrawImage(screenshot, rect, rect, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.FromArgb(42, 242, 166), 2)) e.Graphics.DrawRectangle(pen, rect);
                using (var font = new Font("Segoe UI", 11, FontStyle.Bold))
                using (var fill = new SolidBrush(Color.FromArgb(230, 17, 31, 39)))
                {
                    string size = rect.Width + " × " + rect.Height + " px";
                    SizeF measured = e.Graphics.MeasureString(size, font);
                    int labelX = Math.Max(0, Math.Min(ClientSize.Width - (int)measured.Width - 16, rect.Left));
                    int labelY = Math.Max(0, Math.Min(ClientSize.Height - 32, rect.Bottom + 8));
                    e.Graphics.FillRectangle(fill, labelX, labelY, measured.Width + 16, 30);
                    e.Graphics.DrawString(size, font, Brushes.White, labelX + 8, labelY + 4);
                }
            }
            // Put instructions on the monitor containing the pointer, including negative origins.
            Rectangle monitor = Screen.FromPoint(Cursor.Position).Bounds; monitor.Offset(-Bounds.X, -Bounds.Y);
            if (!ClientRectangle.Contains(monitor)) monitor = ClientRectangle;
            using (var font = new Font("Segoe UI", 16, FontStyle.Bold))
            using (var helpFont = new Font("Segoe UI", 11))
            using (var background = new SolidBrush(Color.FromArgb(230, 17, 31, 39)))
            {
                int bannerWidth = Math.Min(1080, monitor.Width - 40);
                e.Graphics.FillRectangle(background, monitor.Left + 20, monitor.Top + 20, bannerWidth, 106);
                e.Graphics.DrawString(pointOnly ? PointTitle : shopOnly?"SELECCIONAR DIÁLOGO DE COMPRA":counterOnly ? "SELECCIONAR CONTADOR DE CEBO" : "SELECCIONAR ZONA DE PESCA", font, Brushes.White, monitor.Left + 35, monitor.Top + 32);
                string help = selectionHint ?? (pointOnly ? PointHelp :
                    shopOnly?"Rodea la burbuja entera y su fila de botones (Sí/No o Comprar/cantidad/Cancelar), con poco margen.\nEnter o F6: guardar · Esc: cancelar":counterOnly ? "Rodea solo x y la cantidad del cebo equipado (por ejemplo, x300), sin nombres ni otros números.\nEnter o F6: guardar · Esc: cancelar" :
                    "Incluye toda la altura de la barra azul y margen lateral para el balanceo. La verde se ignora.\nEnter o F6: guardar la selección · Esc: cancelar");
                e.Graphics.DrawString(help, helpFont, selectionHint == null ? Brushes.White : Brushes.Salmon,
                    new RectangleF(monitor.Left + 35, monitor.Top + 66, bannerWidth - 30, 55));
            }
        }
        internal void RenderPreview(string path)
        {
            CreateControl();
            using (var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height))
            { DrawToBitmap(bitmap, ClientRectangle); bitmap.Save(path); }
        }
        protected override void Dispose(bool disposing) { if (disposing) screenshot.Dispose(); base.Dispose(disposing); }
    }
}
