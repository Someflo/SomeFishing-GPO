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

    internal sealed class MainForm : Form
    {
        private readonly Color ink = Color.FromArgb(24, 42, 49);
        private readonly Color muted = Color.FromArgb(89, 110, 115);
        private readonly Color accent = Color.FromArgb(0, 113, 99);
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
        private NumericUpDown buyQuantity, purchaseLimit;
        private Button shopAreaButton, shopPreviewButton;
        private Label shopAreaLabel, shopDetail;
        private PictureBox shopPreview;
        private WindowsShopReader previewShopReader;
        private bool previewingShop;

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
            Text = "SomeFishing GPO · v0.3.0";
            ClientSize = new Size(1040, 760);
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 10);
            ForeColor = ink; BackColor = Color.FromArgb(242, 246, 245);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BuildInterface();
            ApplySettings();
            if (loadWarning != null) statusLabel.Text = loadWarning;
            timer.Tick += Tick;
            if (!testMode) timer.Start();
        }

        private Label LabelAt(Control parent, string text, int x, int y, int width, int height, float size, bool bold)
        {
            var label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height),
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = ink, BackColor = Color.Transparent };
            parent.Controls.Add(label); return label;
        }
        private Button ButtonAt(Control parent, string text, int x, int y, int width, EventHandler action, bool primary)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 38),
                FlatStyle = FlatStyle.Flat, BackColor = primary ? accent : Color.White,
                ForeColor = primary ? Color.White : ink, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = primary ? accent : Color.FromArgb(207, 220, 217);
            button.Click += action; parent.Controls.Add(button); return button;
        }
        private CheckBox CheckAt(Control parent, string text, int x, int y, int width)
        {
            var item = new CheckBox { Text = text, Location = new Point(x, y), Size = new Size(width, 28), AutoSize = false };
            parent.Controls.Add(item); return item;
        }
        private NumericUpDown NumberAt(Control parent, string title, int x, int y, int minimum, int maximum, int value)
        {
            LabelAt(parent, title, x, y, 196, 25, 9.5f, false).ForeColor = muted;
            var item = new NumericUpDown { Minimum = minimum, Maximum = maximum, Value = value,
                Location = new Point(x, y + 26), Size = new Size(185, 28), BorderStyle = BorderStyle.FixedSingle };
            parent.Controls.Add(item); return item;
        }
        private void BuildInterface()
        {
            LabelAt(this, "SOMEFISHING GPO", 24, 17, 620, 43, 25, true);
            LabelAt(this, "Mantener para subir. Soltar para bajar. Repetir a tu ritmo.", 26, 63, 760, 28, 11, false).ForeColor = muted;
            LabelAt(this, "CÓDIGO INCLUIDO  /  v0.3.0", 771, 35, 250, 26, 10, true).ForeColor = accent;
            var tabs = new TabControl { Location = new Point(24, 105), Size = new Size(992, 567), Padding = new Point(22, 9) };
            var fishing = new TabPage("Pesca") { BackColor = Color.White };
            var calibration = new TabPage("Calibración") { BackColor = Color.White };
            var guide = new TabPage("Guía rápida") { BackColor = Color.White };
            var baitTab = new TabPage("Cebo y espera") { BackColor = Color.White };
            var shopTab = new TabPage("Comprar cebo") { BackColor = Color.White };
            tabs.TabPages.AddRange(new[] { fishing, calibration, baitTab, shopTab, guide }); Controls.Add(tabs);

            LabelAt(fishing, "01  ELIGE LA BARRA", 20, 18, 410, 28, 12, true);
            LabelAt(fishing, "Barra azul completa y margen para su balanceo.", 20, 52, 430, 24, 10, false).ForeColor = muted;
            areaButton = ButtonAt(fishing, "SELECCIONAR ZONA · F6", 20, 81, 410, delegate { SelectArea(); }, true);
            areaLabel = LabelAt(fishing, "Sin zona seleccionada", 20, 125, 420, 24, 9, false);
            areaLabel.ForeColor = muted;

            LabelAt(fishing, "02  PREPARA EL LANZAMIENTO", 20, 165, 440, 28, 12, true);
            autoCast = CheckAt(fishing, "Volver a lanzar al terminar cada ronda", 20, 199, 425);
            pointButton = ButtonAt(fishing, "Elegir punto sobre el agua", 20, 237, 410, delegate { SelectPoint(); }, false);
            castLabel = LabelAt(fishing, "Equipa la caña manualmente antes de iniciar.", 20, 281, 430, 35, 9, false);
            castLabel.ForeColor = muted;
            castTime = NumberAt(fishing, "Mantener al lanzar (ms)", 20, 325, 50, 3000, 220);
            biteTime = NumberAt(fishing, "Espera de picada (s)", 239, 325, 5, 120, 15);
            allowClicks = CheckAt(fishing, "Permitir clics y teclas al iniciar", 20, 399, 425);
            startButton = ButtonAt(fishing, "Preparar inicio · 3 segundos", 20, 439, 410, delegate { Arm(); }, true);
            var stopLink = new LinkLabel { Text = "Ver última parada", Location = new Point(20, 490),
                Size = new Size(410, 25), LinkColor = accent };
            stopLink.LinkClicked += delegate { ShowLastStop(); }; fishing.Controls.Add(stopLink);

            LabelAt(fishing, "VISTA DEL DETECTOR", 470, 18, 450, 28, 12, true);
            LabelAt(fishing, "Celeste: barra  ·  Naranja: hueco  ·  Rosa: pez", 470, 52, 480, 24, 9.5f, false).ForeColor = muted;
            preview = new PictureBox { Location = new Point(470, 84), Size = new Size(486, 300),
                BackColor = Color.FromArgb(17, 31, 39), SizeMode = PictureBoxSizeMode.Zoom };
            fishing.Controls.Add(preview);
            detectionLabel = LabelAt(fishing, "Selecciona una zona y pulsa «Ver detector».", 470, 397, 480, 36, 10, false);
            previewButton = ButtonAt(fishing, "Ver detector", 470, 439, 228, delegate { TogglePreview(); }, false);
            ButtonAt(fishing, "Mostrar ejemplo", 714, 439, 242, delegate { ShowExample(); }, false);
            cycleLabel = LabelAt(fishing, "Rondas terminadas: 0", 470, 488, 480, 24, 9, false);

            LabelAt(baitTab, "Cebo disponible y espera con saltos", 22, 20, 920, 40, 18, true);
            monitorBait = CheckAt(baitTab, "Leer cantidad de cebo en pantalla", 22, 74, 430);
            LabelAt(baitTab, "Selecciona solo x300 (o el número actual) del cebo\nque tienes equipado. Una segunda zona pequeña.",
                22, 111, 435, 55, 10, false).ForeColor = muted;
            baitAreaButton = ButtonAt(baitTab, "Seleccionar contador de cebo", 22, 177, 412, delegate { SelectBaitArea(); }, true);
            baitAreaLabel = LabelAt(baitTab, "Contador sin zona", 22, 223, 430, 30, 9, false);
            baitPreview = new PictureBox { Location = new Point(470, 75), Size = new Size(486, 91),
                BackColor = Color.FromArgb(40,40,40), SizeMode = PictureBoxSizeMode.Zoom };
            baitTab.Controls.Add(baitPreview);
            baitPreviewButton = ButtonAt(baitTab, "Probar lectura · sin teclas", 470, 177, 486, delegate { ToggleBaitPreview(); }, false);
            baitValueLabel = LabelAt(baitTab, "Cebos: —", 470, 224, 480, 34, 17, true);
            baitDetailLabel = LabelAt(baitTab, "El lector debe confirmar el número antes de usarlo.", 470, 263, 480, 46, 10, false);
            idleJump = CheckAt(baitTab, "Saltar en espera: cebo en 0, contador desaparecido o 3 lanzamientos fallidos", 22, 321, 930);
            jumpSeconds = NumberAt(baitTab, "Segundos entre saltos", 22, 365, 15, 300, 60);
            LabelAt(baitTab, "Pulsa solo Espacio, sin mover al personaje con WASD.\nF10, F8 y cambiar de ventana detienen también los saltos.\nNo guarda objetos ni garantiza evitar una desconexión.",
                250, 369, 710, 84, 10, false).ForeColor = muted;
            ButtonAt(baitTab, "Guardar ajustes", 22, 470, 250, delegate { SaveSettings(); }, true);

            LabelAt(shopTab,"Reponer cebo sin salir del sitio",22,18,930,36,18,true);
            autoBuy=CheckAt(shopTab,"Comprar al confirmar 0 o desaparecer el contador durante 8 s",22,62,930);
            LabelAt(shopTab,"Colócate al alcance de E del barril de cebo. Abre E manualmente para configurar.\nRodea el diálogo entero y la fila de botones, con poco margen: una tercera zona.",22,99,930,54,10,false).ForeColor=muted;
            shopAreaButton=ButtonAt(shopTab,"Seleccionar zona de compra",22,161,420,delegate{SelectShopArea();},true);
            shopAreaLabel=LabelAt(shopTab,"Zona de compra sin seleccionar",22,207,430,35,9,false);
            shopPreview=new PictureBox{Location=new Point(475,161),Size=new Size(480,142),BackColor=Color.FromArgb(40,40,40),SizeMode=PictureBoxSizeMode.Zoom};shopTab.Controls.Add(shopPreview);
            buyMaximum=CheckAt(shopTab,"Comprar el MAX del menú",22,250,430);
            buyQuantity=NumberAt(shopTab,"Cantidad si no usas MAX",22,290,1,9999,5);
            purchaseLimit=NumberAt(shopTab,"Compras por sesión (tope)",245,290,1,100,10);
            buyMaximum.CheckedChanged+=delegate{buyQuantity.Enabled=!buyMaximum.Checked&&!IsRunning;};
            shopPreviewButton=ButtonAt(shopTab,"Probar menús · sin clics",475,314,480,delegate{ToggleShopPreview();},false);
            shopDetail=LabelAt(shopTab,"Prueba Sí y cantidad cambiando los menús manualmente.",475,365,480,69,10,false);
            LabelAt(shopTab,"E → Sí → cantidad → Comprar → … → pesca.\nUsa Peli. Si falla, se detiene sin repetir la compra.\nLos saltos se suspenden mientras compra.",22,383,435,75,9.5f,false).ForeColor=muted;
            ButtonAt(shopTab,"Guardar ajustes",22,470,250,delegate{SaveSettings();},true);

            LabelAt(calibration, "Ajusta lo que ve y cómo responde", 22, 23, 900, 40, 18, true);
            LabelAt(calibration, "Deja margen a los lados para el balanceo. La barra verde puede quedar dentro: se excluye del seguimiento.",
                22, 70, 900, 52, 11, false).ForeColor = muted;
            blueButton = ButtonAt(calibration, "Color azul de la barra", 22, 138, 280, delegate { PickColor(true); }, false);
            markerButton = ButtonAt(calibration, "Color de la línea móvil", 320, 138, 280, delegate { PickColor(false); }, false);
            tolerance = NumberAt(calibration, "Tolerancia de color", 22, 200, 5, 90, 38);
            anticipation = NumberAt(calibration, "Anticipación (ms)", 245, 200, 0, 300, 80);
            restTime = NumberAt(calibration, "Pausa entre rondas (ms)", 468, 200, 500, 10000, 1800);
            holdUp = CheckAt(calibration, "Mantener clic mueve el hueco gris hacia arriba", 22, 290, 650);
            LabelAt(calibration, "Si el hueco se pasa del pez, aumenta la anticipación. Si responde demasiado pronto, bájala.\n" +
                "El frenado aumenta automáticamente cuando el pez está casi quieto.\n" +
                "Si no detecta la línea o el hueco, ajusta primero la zona; después, la tolerancia de color.",
                22, 340, 905, 102, 11, false).ForeColor = muted;
            ButtonAt(calibration, "Guardar ajustes", 22, 452, 250, delegate { SaveSettings(); }, true);
            LabelAt(guide, "Primera pesca, paso a paso", 22, 23, 920, 43, 18, true);
            LabelAt(guide,
                "1. Abre Roblox en ventana o sin bordes y equipa la caña.\n\n" +
                "2. Pulsa F6 y rodea toda la altura de la barra azul, con margen a ambos lados.\n" +
                "    Revisa el rectángulo y confirma con Enter o F6. Esc cancela.\n\n" +
                "3. Pulsa «Ver detector». El contorno celeste debe seguir la barra al balancearse.\n" +
                "    Naranja marca el hueco; rosa, el pez. La barra verde se ignora.\n\n" +
                "4. Elige un punto sobre el agua y marca «Permitir clics». Vuelve a Roblox\n" +
                "    y pulsa F8. También puedes usar el botón de inicio con cuenta atrás.\n\n" +
                "5. F8 alterna inicio/parada. F10 detiene inmediatamente. Cambiar de ventana\n" +
                "    o llevar el ratón a la esquina superior izquierda también detiene la macro.",
                22, 84, 938, 364, 11, false);
            LabelAt(guide, "La barra verde del juego muestra el progreso. El menú cerrado inicia otra ronda,\n" +
                "tanto si el pez se capturó como si escapó; el contador registra rondas, no capturas.",
                22, 462, 900, 50, 10, false).ForeColor = muted;
            statusLabel = LabelAt(this, "Detenida · lista para configurar", 26, 689, 741, 52, 11, true);
            ButtonAt(this, "DETENER · F10", 788, 691, 228, delegate { StopAll("Detenida por ti"); }, false);
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
            SetNumber(buyQuantity,settings.BuyQuantity);SetNumber(purchaseLimit,settings.PurchaseLimit);buyQuantity.Enabled=!buyMaximum.Checked;
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
                MonitorBait = monitorBait.Checked, BaitArea = settings.BaitArea,
                IdleJumpEnabled = idleJump.Checked, IdleJumpSeconds = (int)jumpSeconds.Value,
                AutoBuyBait=autoBuy.Checked,ShopArea=settings.ShopArea,BuyMaximum=buyMaximum.Checked,
                BuyQuantity=(int)buyQuantity.Value,PurchaseLimit=(int)purchaseLimit.Value };
        }
        private void UpdateAreaLabels()
        {
            areaLabel.Text = settings.Area.IsEmpty ? "Sin zona seleccionada" :
                string.Format("Zona lista: {0} × {1} px · posición {2}, {3}", settings.Area.Width, settings.Area.Height, settings.Area.X, settings.Area.Y);
            areaLabel.ForeColor = settings.Area.IsEmpty ? muted : accent;
            areaButton.Text = settings.Area.IsEmpty ? "SELECCIONAR ZONA · F6" : "CAMBIAR ZONA · F6";
            castLabel.Text = !settings.CastPointSet ?
                "Equipa la caña manualmente antes de iniciar." : string.Format("Punto de lanzamiento: {0}, {1} · caña equipada", settings.CastPoint.X, settings.CastPoint.Y);
            blueButton.BackColor = Color.FromArgb(settings.BlueArgb); blueButton.ForeColor = Color.Black;
            markerButton.BackColor = Color.FromArgb(settings.MarkerArgb); markerButton.ForeColor = Color.Black;
            baitAreaLabel.Text = settings.BaitArea.IsEmpty ? "Contador sin zona" :
                string.Format("Contador: {0} × {1} px · posición {2}, {3}", settings.BaitArea.Width, settings.BaitArea.Height, settings.BaitArea.X, settings.BaitArea.Y);
            shopAreaLabel.Text=settings.ShopArea.IsEmpty?"Zona de compra sin seleccionar":string.Format("Compra: {0} × {1} px · posición {2}, {3}",settings.ShopArea.Width,settings.ShopArea.Height,settings.ShopArea.X,settings.ShopArea.Y);
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
            previewShopReader=new WindowsShopReader();previewingShop=true;shopPreviewButton.Text="Detener prueba";statusLabel.Text="Solo lee los menús · no compra ni envía teclas";
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
            previewReader = new WindowsBaitReader(); previewBaitMonitor.Reset(); previewingBait = true;
            baitPreviewButton.Text = "Detener lectura";
            statusLabel.Text = "Solo lectura del contador · sin clics ni saltos";
        }
        private void ShowBaitCount(int? count, string detail)
        {
            baitValueLabel.Text = "Cebos: " + (count.HasValue ? count.Value.ToString() : "—");
            baitValueLabel.ForeColor = count.HasValue && count.Value <= 10 ? Color.FromArgb(174,85,15) : ink;
            baitDetailLabel.Text = detail ?? "Esperando lectura…";
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
            previewing = false; armedUntil = clock.Elapsed.TotalMilliseconds + 3000;
            statusLabel.Text = "Vuelve a Roblox: inicio en 3 segundos. F10 cancela.";
        }
        private void StartNow()
        {
            if (!CanStart()) return;
            IntPtr target = Native.GetForegroundWindow();
            if (!Native.IsRoblox(target)) { statusLabel.Text = "Vuelve a la ventana de Roblox y pulsa F8 para iniciar."; return; }
            StopAll("Iniciando…");
            previewSample = false;
            try
            {
                settings = ReadSettings();
                SaveSettingsQuietly();
                runtime = new GameRuntime(settings, target, true);
                engine = new FishingEngine(settings, runtime);
                runStarted = lastTick = clock.Elapsed.TotalMilliseconds; maxTickGap = 0; nextPreview = 0;
                engine.Start(runStarted);
                statusLabel.Text = engine.Status;
                startButton.Text = "Detener · F8 / F10";
                SetEditable(false);
            }
            catch (Exception error) { StopAll(error.Message); }
        }
        private void SetEditable(bool value)
        {
            foreach (Control control in new Control[] { areaButton, pointButton, autoCast, castTime, biteTime, restTime,
                tolerance, anticipation, holdUp, blueButton, markerButton, allowClicks,
                monitorBait, idleJump, jumpSeconds, baitAreaButton, baitPreviewButton,
                autoBuy,buyMaximum,buyQuantity,purchaseLimit,shopAreaButton,shopPreviewButton }) control.Enabled = value;
            buyQuantity.Enabled=value&&!buyMaximum.Checked;
        }
        private void StopAll(string reason)
        {
            bool hadSession = engine != null;
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
            if (hadSession && !testMode)
            {
                lastStop = string.Format("SomeFishing GPO 0.3.0 · {0:yyyy-MM-dd HH:mm:ss}\r\n\r\n{1}\r\n\r\n" +
                    "Estado al parar: {2}\r\nRondas terminadas: {3}\r\nDuración: {4:F1} s\r\n" +
                    "Mayor intervalo entre revisiones: {5:F0} ms\r\nLanzamiento: {6} ms · Espera: {7} s\r\n" +
                    "Anticipación: {8} ms · Tolerancia: {9}\r\nÚltima detección: {10}\r\n" +
                    "Cebos: {11} · Saltos habilitados: {12} · Espacios solicitados: {13}\r\n" +
                    "Intentos de reposición: {14} · Última compra enviada: {15}\r\n",
                    DateTime.Now, reason.Length > 2000 ? reason.Substring(0, 2000) : reason, phase, engine.Cycles,
                    (clock.Elapsed.TotalMilliseconds - runStarted) / 1000, maxTickGap,
                    settings.CastMilliseconds, settings.BiteSeconds, settings.AnticipationMilliseconds, settings.Tolerance,
                    engine.LastObservation == null ? "Sin imagen" : engine.LastObservation.Detail,
                    engine.BaitCount.HasValue ? engine.BaitCount.Value.ToString() : "Desconocido", settings.IdleJumpEnabled, engine.JumpRequests,engine.PurchaseAttempts,engine.PurchaseSubmitted);
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ultima-parada.txt"), lastStop); }
                catch (Exception error) { reason += " · No se pudo guardar el informe: " + error.Message; }
            }
            if (runtime == null || !runtime.PendingRelease) runtime = null;
            engine = null;
            if (statusLabel != null) statusLabel.Text = reason;
            if (startButton != null) startButton.Text = "Preparar inicio · 3 segundos";
            if (previewButton != null) previewButton.Text = "Ver detector";
            if (baitPreviewButton != null) baitPreviewButton.Text = "Probar lectura · sin teclas";
            if(shopPreviewButton!=null)shopPreviewButton.Text="Probar menús · sin clics";
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
            previewing = true; previewSample = false; previewButton.Text = "Parar vista previa";
            statusLabel.Text = "Vista previa · solo observa, no envía clics";
        }
        private void ShowExample()
        {
            StopAll("Ejemplo de detección · imagen de demostración");
            previewSample = true;
            using (Bitmap sample = SelfTests.CreateWideSample(60))
                SetPreview(sample, Detector.Analyze(sample, new Settings()));
        }
        private void Tick(object sender, EventArgs args)
        {
            if (!IsRunning && runtime != null && !runtime.PendingRelease) { runtime.Dispose(); runtime = null; }
            double now = clock.Elapsed.TotalMilliseconds;
            if (armedUntil > 0)
            {
                if (now >= armedUntil) { armedUntil = 0; StartNow(); }
                else statusLabel.Text = "Vuelve a Roblox: inicio en " + Math.Ceiling((armedUntil - now) / 1000) + " s. F10 cancela.";
                return;
            }
            try
            {
                if (IsRunning)
                {
                    maxTickGap = Math.Max(maxTickGap, now - lastTick); lastTick = now;
                    phaseBeforeTick = engine.State;
                    engine.Tick(now);
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
            foreach (Control control in Controls)
            {
                var tabs = control as TabControl;
                if (tabs == null) continue;
                for (int i = 1; i < tabs.TabPages.Count; i++)
                {
                    tabs.SelectedIndex = i; Application.DoEvents();
                    using (var bitmap = new Bitmap(Width, Height))
                    { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(Path.GetDirectoryName(path), "interfaz-" + i + ".png")); }
                }
            }
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
                e.Graphics.DrawString(pointOnly ? "ELIGE EL PUNTO DE LANZAMIENTO" : shopOnly?"SELECCIONAR DIÁLOGO DE COMPRA":counterOnly ? "SELECCIONAR CONTADOR DE CEBO" : "SELECCIONAR ZONA DE PESCA", font, Brushes.White, monitor.Left + 35, monitor.Top + 32);
                string help = selectionHint ?? (pointOnly ? "Haz clic sobre el agua. Esc cancela." :
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
