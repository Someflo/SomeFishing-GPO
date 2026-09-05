using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SomeFishingGPO
{
    internal partial class MainForm
    {
        private Panel[] pages;
        private Button[] navigation;
        private Label pageTitle, pageSubtitle, emptyTestSummary;
        private readonly ToolTip hints = new ToolTip { InitialDelay = 500, ReshowDelay = 200, AutoPopDelay = 15000 };
        private readonly string[] pageNames = { "Pesca", "Cebo", "Compra", "Pruebas", "Ajustes", "Guía" };
        private readonly string[] pageDescriptions = {
            "Prepara la zona y el lanzamiento.", "Lee el contador y configura la espera.",
            "Repón cebo desde el barril.", "Comprueba una acción antes de dejar la macro funcionando.",
            "Ajusta la detección y el movimiento.", "Lo esencial para empezar." };

        private Label LabelAt(Control parent, string text, int x, int y, int width, int height, float size, bool bold)
        {
            var label = new Label { Text = text, Location = new Point(x,y), Size = new Size(width,height),
                Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI",size), ForeColor=ink,
                BackColor=Color.Transparent, AutoEllipsis=true };
            parent.Controls.Add(label); return label;
        }
        private Button ButtonAt(Control parent, string text, int x, int y, int width, EventHandler action, bool primary)
        {
            var button = new ModernButton { Text=text, Location=new Point(x,y), Size=new Size(width,40),
                BackColor=primary?accent:Color.White, ForeColor=primary?Color.White:ink,
                Font=new Font("Segoe UI Semibold",10), Cursor=Cursors.Hand, TabIndex=parent.Controls.Count };
            button.Click+=action; parent.Controls.Add(button); return button;
        }
        private CheckBox CheckAt(Control parent, string text, int x, int y, int width)
        {
            var item=new CheckBox { Text=text, Location=new Point(x,y), Size=new Size(width,28),
                AutoSize=false, Cursor=Cursors.Hand, BackColor=Color.Transparent, TabIndex=parent.Controls.Count };
            parent.Controls.Add(item); return item;
        }
        private NumericUpDown NumberAt(Control parent, string title, int x, int y, int minimum, int maximum, int value, int width=185)
        {
            LabelAt(parent,title,x,y,width,23,9.5f,false).ForeColor=muted;
            var item=new NumericUpDown { Minimum=minimum, Maximum=maximum, Value=value,
                Location=new Point(x,y+27), Size=new Size(width,30), Font=new Font("Segoe UI",11),
                BorderStyle=BorderStyle.FixedSingle, TabIndex=parent.Controls.Count };
            parent.Controls.Add(item); return item;
        }
        private Panel Card(Control parent, int x, int y, int width, int height)
        {
            var panel=new SurfacePanel { Location=new Point(x,y), Size=new Size(width,height) };
            parent.Controls.Add(panel);return panel;
        }
        private void Divider(Control parent, int x, int y, int width)
        { parent.Controls.Add(new Panel { Location=new Point(x,y), Size=new Size(width,1), BackColor=Color.FromArgb(231,235,239) }); }
        private void Hint(Control control, string text) { hints.SetToolTip(control,text); }
        private void FullTextHint(Label label)
        { label.TextChanged+=delegate{Hint(label,label.Text);};Hint(label,label.Text); }
        private PictureBox PreviewAt(Control parent, int x, int y, int width, int height, string empty)
        {
            var picture=new PictureBox { Location=new Point(x,y), Size=new Size(width,height),
                BackColor=Color.FromArgb(24,34,44), SizeMode=PictureBoxSizeMode.Zoom };
            picture.Paint+=delegate(object sender,PaintEventArgs e){
                if(picture.Image==null)using(var font=new Font("Segoe UI",10))
                    TextRenderer.DrawText(e.Graphics,empty,font,picture.ClientRectangle,Color.FromArgb(162,174,187),
                        TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
            };
            parent.Controls.Add(picture); return picture;
        }
        private void SelectPage(int index)
        {
            for(int i=0;i<pages.Length;i++)
            {
                pages[i].Visible=i==index;
                navigation[i].BackColor=i==index?Color.FromArgb(37,64,67):Color.FromArgb(24,34,44);
                navigation[i].ForeColor=i==index?Color.FromArgb(154,232,211):Color.FromArgb(181,191,203);
            }
            pageTitle.Text=pageNames[index];pageSubtitle.Text=pageDescriptions[index];
        }
        private void UpdateDiagnosticSummary()
        {
            emptyTestSummary.Text=autoBuy.Checked?"Simula 0 y compra 1 cebo con Peli.":idleJump.Checked?
                "Simula 0 y da un solo salto en el sitio.":"Compra y saltos desactivados.\nLa prueba lo indicará sin actuar.";
        }
        private void UpdatePurchaseControls(bool editable)
        {
            bool timed=purchaseMode.SelectedIndex==1;
            buyMaximum.Enabled=false;
            buyQuantity.Enabled=editable&&timed;baitCapacity.Enabled=editable&&!timed;
            purchaseMinutes.Enabled=editable&&timed;
            timerCondition.Visible=timed;ocrCondition.Visible=!timed; baitThreshold.Enabled=editable&&!timed;
            purchaseQuantityCondition.Visible=timed;capacityCondition.Visible=!timed;
            purchaseCountdown.Visible=timed;
            purchaseModeHint.Text=timed?"Compra una cantidad cada cierto tiempo.":"Activa OCR y completa la capacidad.";
        }
        private void BuildInterface()
        {
            var rail=new Panel { Dock=DockStyle.Left, Width=188, BackColor=Color.FromArgb(24,34,44) };
            Controls.Add(rail);
            LabelAt(rail,"SomeFishing",18,31,166,32,14,true).ForeColor=Color.White;
            LabelAt(rail,"GPO",22,66,145,22,10,false).ForeColor=Color.FromArgb(145,166,177);
            navigation=new Button[pageNames.Length];pages=new Panel[pageNames.Length];
            for(int i=0;i<pageNames.Length;i++)
            {
                int destination=i;
                navigation[i]=ButtonAt(rail,pageNames[i],12,126+i*49,164,delegate{SelectPage(destination);},false);
                navigation[i].TextAlign=ContentAlignment.MiddleLeft;
                ((ModernButton)navigation[i]).BorderVisible=false;
                pages[i]=new Panel { Location=new Point(212,104), Size=new Size(844,516), BackColor=BackColor, Visible=false };
                Controls.Add(pages[i]);
            }
            var save=ButtonAt(rail,"Guardar",16,593,156,delegate{SaveSettings();},false);
            Hint(save,"Guarda las zonas y los ajustes actuales.");
            save.BackColor=Color.FromArgb(34,48,61);save.ForeColor=Color.White;
            ((ModernButton)save).BorderVisible=false;
            LabelAt(rail,"Local  /  v0.6.0",22,657,145,23,9,false).ForeColor=Color.FromArgb(145,166,177);
            LabelAt(rail,"Código incluido",22,681,145,23,9,false).ForeColor=Color.FromArgb(145,166,177);
            pageTitle=LabelAt(this,"",212,15,800,49,25,true);
            pageSubtitle=LabelAt(this,"",214,69,820,27,10.5f,false);pageSubtitle.ForeColor=muted;

            var setup=Card(pages[0],0,0,372,516);
            LabelAt(setup,"Preparación",20,20,330,29,14,true);
            areaButton=ButtonAt(setup,"Seleccionar zona · F6",20,67,332,delegate{SelectArea();},true);
            areaLabel=LabelAt(setup,"Sin seleccionar",20,118,330,23,9.5f,false);
            Hint(areaButton,"Rodea toda la barra azul y deja margen para su balanceo. Enter o F6 confirma; Esc cancela.");
            Divider(setup,20,158,332);
            LabelAt(setup,"Lanzamiento",20,178,330,27,12,true);
            autoCast=CheckAt(setup,"Volver a lanzar automáticamente",20,214,333);
            pointButton=ButtonAt(setup,"Elegir punto en el agua",20,253,332,delegate{SelectPoint();},false);
            castLabel=LabelAt(setup,"Punto sin seleccionar",20,303,332,23,9.5f,false);castLabel.ForeColor=muted;
            castTime=NumberAt(setup,"Clic al lanzar (ms)",20,347,50,3000,220,156);
            biteTime=NumberAt(setup,"Espera de picada (s)",194,347,5,120,15,158);
            allowClicks=CheckAt(setup,"Permitir clics y teclas",20,433,332);
            Hint(allowClicks,"Autoriza la pesca al pulsar Iniciar o F8. Se desmarca al abrir el programa de nuevo.");
            var last=ButtonAt(setup,"Última parada",20,473,332,delegate{ShowLastStop();},false);last.Height=30;
            ((ModernButton)last).BorderVisible=false;last.ForeColor=muted;

            var detector=Card(pages[0],388,0,456,516);
            LabelAt(detector,"Detector",20,20,160,28,14,true);
            cycleLabel=LabelAt(detector,"Rondas: 0",188,25,248,25,9,false);cycleLabel.TextAlign=ContentAlignment.TopRight;cycleLabel.ForeColor=muted;
            preview=PreviewAt(detector,20,66,416,326,"Selecciona una zona\npara ver el detector");
            detectionLabel=LabelAt(detector,"Solo lectura. No envía clics.",20,405,416,43,9.5f,false);FullTextHint(detectionLabel);
            previewButton=ButtonAt(detector,"Ver detector",20,459,202,delegate{TogglePreview();},false);
            ButtonAt(detector,"Ejemplo",234,459,202,delegate{ShowExample();},false);
            Hint(previewButton,"Celeste: barra. Naranja: hueco. Rosa: pez. El progreso verde se ignora.");

            var counter=Card(pages[1],0,0,372,340);
            LabelAt(counter,"Contador",20,20,332,29,14,true);
            monitorBait=CheckAt(counter,"Leer cebo en pantalla",20,67,332);
            LabelAt(counter,"Rodea solo la x y el número.",20,115,330,26,10,false).ForeColor=muted;
            baitAreaButton=ButtonAt(counter,"Seleccionar contador",20,162,332,delegate{SelectBaitArea();},true);
            baitAreaLabel=LabelAt(counter,"Sin seleccionar",20,214,332,25,9.5f,false);
            Hint(baitAreaButton,"Selecciona un solo tipo de cebo, por ejemplo x300, sin el borde amarillo ni otros números.");
            LabelAt(counter,"Idioma OCR del contador",20,258,332,23,9.5f,false).ForeColor=muted;
            ocrLanguage=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,288),Size=new Size(332,30),Font=new Font("Segoe UI",10)};
            ocrTags.Add("");ocrLanguage.Items.Add("Automático (Windows)");
            foreach(var language in WindowsBaitReader.Languages()){ocrTags.Add(language.Key);ocrLanguage.Items.Add(language.Value);}
            counter.Controls.Add(ocrLanguage);ocrLanguage.SelectedIndex=0;
            ocrLanguage.SelectedIndexChanged+=delegate{if(previewingBait||previewingShop)StopAll("Idioma cambiado. Vuelve a probar la lectura.");};
            Hint(ocrLanguage,"Solo muestra idiomas OCR instalados en Windows. Lee el contador de cebo; los botones de compra se marcan manualmente.");
            var reading=Card(pages[1],388,0,456,340);
            LabelAt(reading,"Lectura",20,20,416,29,14,true);
            baitPreview=PreviewAt(reading,20,65,416,90,"Vista del contador");
            baitValueLabel=LabelAt(reading,"Cebos: —",20,166,416,48,22,true);
            baitDetailLabel=LabelAt(reading,"Selecciona el contador para empezar.",20,220,416,44,9.5f,false);FullTextHint(baitDetailLabel);
            baitPreviewButton=ButtonAt(reading,"Probar lectura",20,282,416,delegate{ToggleBaitPreview();},false);
            Hint(baitPreviewButton,"Solo lee la zona seleccionada. No envía clics, saltos ni compras.");
            var waiting=Card(pages[1],0,356,844,160);
            LabelAt(waiting,"Espera",20,20,590,28,14,true);
            idleJump=CheckAt(waiting,"Saltar durante la espera",20,58,570);
            LabelAt(waiting,"Un salto en el sitio, sin caminar.",20,106,570,26,10,false).ForeColor=muted;
            jumpSeconds=NumberAt(waiting,"Intervalo de salto (s)",628,49,15,300,60,196);
            Hint(idleJump,"Se activa con cebo en 0, contador desaparecido o tres lanzamientos sin minijuego. No garantiza evitar una desconexión ni conservar objetos.");

            var buying=Card(pages[2],0,0,372,516);
            LabelAt(buying,"Compra automática",20,20,332,29,14,true);
            autoBuy=CheckAt(buying,"Reponer cebo · usa Peli",20,66,332);
            Hint(autoBuy,"Activa compras por contador o cronómetro. Permanece junto al barril de cebo.");
            purchaseMode=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,109),Size=new Size(332,30),Font=new Font("Segoe UI",11)};
            purchaseMode.Items.AddRange(new object[]{"Contador OCR","Cronómetro"});buying.Controls.Add(purchaseMode);
            purchaseModeHint=LabelAt(buying,"",20,146,332,38,9.5f,false);purchaseModeHint.ForeColor=muted;
            purchaseQuantityCondition=new Panel{Location=new Point(20,196),Size=new Size(156,65),BackColor=Color.Transparent};buying.Controls.Add(purchaseQuantityCondition);
            capacityCondition=new Panel{Location=purchaseQuantityCondition.Location,Size=purchaseQuantityCondition.Size,BackColor=Color.Transparent};buying.Controls.Add(capacityCondition);
            buyQuantity=NumberAt(purchaseQuantityCondition,"Cebos por compra",0,0,1,9999,50,156);
            baitCapacity=NumberAt(capacityCondition,"Capacidad de cebo",0,0,1,9999,300,156);
            Hint(buyQuantity,"Cantidad que se escribe en cada compra programada. El juego puede limitarla según capacidad y Peli.");
            Hint(baitCapacity,"Total que quieres tener tras reponer. Por ejemplo, con capacidad 300 y 2 cebos restantes solicita 298.");
            timerCondition=new Panel{Location=new Point(194,196),Size=new Size(158,65),BackColor=Color.Transparent};buying.Controls.Add(timerCondition);
            ocrCondition=new Panel{Location=timerCondition.Location,Size=timerCondition.Size,BackColor=Color.Transparent};buying.Controls.Add(ocrCondition);
            purchaseMinutes=NumberAt(timerCondition,"Cada (minutos)",0,0,1,1440,40,158);
            baitThreshold=NumberAt(ocrCondition,"Comprar si quedan ≤",0,0,0,9999,2,158);
            Hint(baitThreshold,"Con 2 repone cuando confirma 2, 1 o 0 cebos. La cantidad se calcula con la capacidad y el contador, sin leer MAX del menú.");
            buyMaximum=new CheckBox{Visible=false};
            Divider(buying,20,286,332);
            LabelAt(buying,"Secuencia",20,307,332,27,12,true);
            LabelAt(buying,"E → Sí → número → Comprar → …",20,348,332,26,10,false).ForeColor=muted;
            LabelAt(buying,"Doble clic solo en el número central.",20,379,332,26,9.5f,false).ForeColor=muted;
            purchaseLimit=NumberAt(buying,"Tope por sesión",194,419,1,100,10,158);
            shopOpenTime=NumberAt(buying,"Mantener E (ms)",20,419,100,3000,1000,156);
            Hint(shopOpenTime,"Duración de la tecla E para abrir el diálogo. Si no abre con 1000 ms, prueba 1500 ms.");
            Hint(purchaseLimit,"Máximo de intentos por sesión. Si falla un paso, la compra se detiene sin repetirse.");
            purchaseMode.SelectedIndexChanged+=delegate{UpdatePurchaseControls(!IsRunning&&armedUntil==0);};
            var shopView=Card(pages[2],388,0,456,516);
            LabelAt(shopView,"Marca 3 botones",20,20,416,29,14,true);
            LabelAt(shopView,"Abre el menú con el número y marca el centro\nde cada botón. Conserva la posición de la ventana.",20,65,416,43,9.5f,false).ForeColor=muted;
            shopPointButtons=new Button[3];shopPointLabels=new Label[3];
            string[] pointNames={"1. Izquierda · Sí / Comprar","2. Centro · Número / …","3. Derecha · No / Cancelar"};
            for(int i=0;i<3;i++){
                int pointIndex=i;
                shopPointButtons[i]=ButtonAt(shopView,pointNames[i],20,116+i*66,416,delegate{SelectShopPoint(pointIndex);},i==0);
                shopPointLabels[i]=LabelAt(shopView,"Sin marcar",20,158+i*66,416,20,9,false);
                Hint(shopPointButtons[i],"Muestra una captura y guarda un solo punto. El clic de selección no llega al juego.");
            }
            shopAreaButton=shopPointButtons[0];
            shopAreaLabel=LabelAt(shopView,"Marca los 3 botones antes de comprar.",20,322,416,34,9.5f,false);FullTextHint(shopAreaLabel);
            shopDetail=shopAreaLabel;
            shopPreview=new PictureBox{Visible=false};shopPreviewButton=new Button{Visible=false};
            shopSettle=NumberAt(shopView,"Pausa entre pasos (ms)",20,374,200,3000,700,202);
            Hint(shopSettle,"Espera entre las acciones del diálogo. Si avanza antes de que cargue el menú, aumenta este valor. Doble clic solo en el número.");
            purchaseCountdown=LabelAt(shopView,"El cronómetro empieza al iniciar la pesca.",236,380,200,64,9.5f,true);FullTextHint(purchaseCountdown);
            Hint(purchaseMinutes,"Cuenta desde el inicio. Pausa la pesca para comprar y reinicia el intervalo tras la secuencia. Al detener la macro se cancela.");
            ButtonAt(shopView,"Ir a pruebas",20,459,416,delegate{SelectPage(3);},false);

            var noBait=Card(pages[3],0,0,414,213);
            LabelAt(noBait,"Sin cebo",20,20,374,29,14,true);
            emptyTestSummary=LabelAt(noBait,"",20,66,374,57,10.5f,false);emptyTestSummary.ForeColor=muted;
            emptyTestButton=ButtonAt(noBait,"Probar sin cebo",20,153,374,delegate{ArmDiagnostic(RunKind.EmptyBaitTest);},true);
            var purchase=Card(pages[3],430,0,414,213);
            LabelAt(purchase,"Compra",20,20,374,29,14,true);
            testBuyQuantity=NumberAt(purchase,"Cebos para esta prueba",20,64,1,9999,1,180);
            LabelAt(purchase,"Usa Peli · un intento.\nSin OCR ni cronómetro.",216,84,178,45,9.5f,false).ForeColor=muted;
            purchaseTestButton=ButtonAt(purchase,"Probar compra",20,153,374,delegate{ArmDiagnostic(RunKind.PurchaseTest);},true);
            LabelAt(pages[3],"Inicio en 3 s. Vuelve a Roblox con los diálogos cerrados. F10 cancela.",4,235,836,34,10,false).ForeColor=muted;
            var result=Card(pages[3],0,282,844,234);
            LabelAt(result,"Resultado",20,20,430,29,14,true);
            var copy=ButtonAt(result,"Copiar resultado",626,15,198,delegate{
                try{Clipboard.SetText(diagnosticLog.Text);statusLabel.Text="Resultado copiado.";}
                catch{statusLabel.Text="No se pudo copiar. El registro sigue disponible en Pruebas.";}
            },false);
            Hint(copy,"El registro también se guarda en ultima-prueba.txt, junto al programa. No se envía automáticamente.");
            diagnosticLog=new TextBox { Location=new Point(20,66), Size=new Size(804,147), Multiline=true,
                ReadOnly=true, ScrollBars=ScrollBars.Vertical, BorderStyle=BorderStyle.None,
                BackColor=Color.White, ForeColor=muted, Font=new Font("Consolas",9),
                Text="Ejecuta una prueba para ver el resultado." };
            result.Controls.Add(diagnosticLog);
            autoBuy.CheckedChanged+=delegate{UpdateDiagnosticSummary();};idleJump.CheckedChanged+=delegate{UpdateDiagnosticSummary();};UpdateDiagnosticSummary();
            Hint(emptyTestButton,"Simula 0; no comprueba el OCR real. Compra una unidad, da un salto o informa de que ambas opciones están apagadas, según tus ajustes.");
            Hint(purchaseTestButton,"Ejecuta una sola compra con la cantidad indicada y los 3 puntos marcados. No requiere OCR, no espera el cronómetro ni lanza pesca al terminar.");

            var response=Card(pages[4],0,0,844,246);
            LabelAt(response,"Respuesta",20,20,804,29,14,true);
            tolerance=NumberAt(response,"Tolerancia de color",20,82,5,90,38,248);
            anticipation=NumberAt(response,"Anticipación (ms)",298,82,0,300,80,248);
            restTime=NumberAt(response,"Pausa entre rondas (ms)",576,82,500,10000,1800,248);
            holdUp=CheckAt(response,"Mantener clic sube el hueco",20,175,804);
            Hint(anticipation,"Si el hueco se pasa del pez, aumenta este valor. Si responde demasiado pronto, bájalo.");
            Hint(tolerance,"Margen de color del detector. Ajusta primero la zona si no encuentra el hueco o la línea.");
            var colors=Card(pages[4],0,262,844,254);
            LabelAt(colors,"Colores del minijuego",20,20,804,29,14,true);
            blueButton=ButtonAt(colors,"Barra azul",20,79,390,delegate{PickColor(true);},false);
            markerButton=ButtonAt(colors,"Línea del pez",430,79,394,delegate{PickColor(false);},false);
            LabelAt(colors,"Cambia los colores solo si la detección falla.",20,144,804,29,10,false).ForeColor=muted;
            ButtonAt(colors,"Ver guía",20,194,200,delegate{SelectPage(5);},false);

            string[] steps={"1. Selecciona la barra","2. Prepara la pesca","3. Inicia desde Roblox"};
            string[] details={"Equipa la caña y lanza una vez. Rodea toda la barra azul, con margen lateral.\nEnter o F6 confirma la zona; Esc cancela.",
                "Comprueba el detector y elige un punto en el agua.\nActiva el permiso de clics y teclas antes de iniciar.",
                "Pulsa F8, o usa Iniciar y vuelve al juego en 3 segundos.\nF10 detiene. Cambiar de ventana también detiene la macro."};
            for(int i=0;i<3;i++)
            {
                var step=Card(pages[5],0,i*119,844,105);
                LabelAt(step,steps[i],20,16,804,27,12,true);
                LabelAt(step,details[i],20,52,804,46,10,false).ForeColor=muted;
            }
            var keys=Card(pages[5],0,357,844,159);
            LabelAt(keys,"Atajos",20,18,804,29,14,true);
            LabelAt(keys,"F6   Seleccionar zona",20,65,265,26,10,true);
            LabelAt(keys,"F8   Iniciar / detener",300,65,265,26,10,true);
            LabelAt(keys,"F10   Detener",580,65,244,26,10,true);
            LabelAt(keys,"Para comprar: marca los 3 botones en Compra y verifica una compra en Pruebas.",20,112,804,28,9.5f,false).ForeColor=muted;

            var footer=Card(this,212,636,844,76);
            statusLabel=LabelAt(footer,"Detenida · lista para configurar",20,17,426,44,10,true);FullTextHint(statusLabel);
            startButton=ButtonAt(footer,"Iniciar · 3 s",466,18,192,delegate{Arm();},true);
            var stop=ButtonAt(footer,"Detener · F10",674,18,150,delegate{StopAll("Detenida por ti");},false);
            stop.ForeColor=Color.FromArgb(180,53,68);
            Hint(startButton,"Vuelve a Roblox durante los 3 segundos. F8 inicia directamente desde el juego; F10 siempre cancela.");
            SelectPage(0);
        }
    }

    internal static class UiShape
    {
        internal static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            var path=new GraphicsPath();int d=radius*2;
            path.AddArc(bounds.Left,bounds.Top,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);
            path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);path.CloseFigure();return path;
        }
    }
    internal sealed class SurfacePanel : Panel
    {
        internal SurfacePanel() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var shape=UiShape.Rounded(new Rectangle(0,0,Width-1,Height-1),12))
            using(var fill=new SolidBrush(Color.White))using(var line=new Pen(Color.FromArgb(228,234,239)))
            {e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(line,shape);}
            base.OnPaint(e);
        }
    }
    internal sealed class ModernButton : Button
    {
        private bool hover;
        internal bool BorderVisible=true;
        internal Color? SwatchColor;
        internal ModernButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
        protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?SystemColors.Control:Parent is SurfacePanel?Color.White:Parent.BackColor);
            Color fill=Enabled?BackColor:Color.FromArgb(239,242,245);
            if(hover&&Enabled)fill=ControlPaint.Dark(fill,.04f);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var shape=UiShape.Rounded(new Rectangle(1,1,Width-3,Height-3),7))
            using(var brush=new SolidBrush(fill))using(var line=new Pen(Color.FromArgb(216,224,231)))
            {e.Graphics.FillPath(brush,shape);if(BorderVisible&&BackColor==Color.White)e.Graphics.DrawPath(line,shape);}
            var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine;
            flags|=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(14,1,Width-28-(SwatchColor.HasValue?23:0),Height-2),Enabled?ForeColor:Color.FromArgb(151,162,174),flags);
            if(SwatchColor.HasValue)using(var brush=new SolidBrush(SwatchColor.Value))using(var line=new Pen(Color.FromArgb(188,199,208)))
            {e.Graphics.FillEllipse(brush,Width-34,Height/2-8,16,16);e.Graphics.DrawEllipse(line,Width-34,Height/2-8,16,16);}
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(6,6,Width-12,Height-12),ForeColor,fill);
        }
    }
}
