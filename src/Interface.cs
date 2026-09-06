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
        private Label pageTitle, pageSubtitle, emptyTestSummary, ocrSummary, baitReadoutLabel, baitReadoutDetail;
        private Panel advancedScroll, testActions, testReadings;
        private Button actionsTabButton, readingsTabButton;
        private readonly ToolTip hints = new ToolTip { InitialDelay = 500, ReshowDelay = 200, AutoPopDelay = 15000 };
        private readonly System.Collections.Generic.Dictionary<Control,string> hintSources=new System.Collections.Generic.Dictionary<Control,string>();
        private readonly System.Collections.Generic.HashSet<Label> fullHints=new System.Collections.Generic.HashSet<Label>();
        private readonly string[] pageNames = { "Inicio", "Zonas", "Pruebas", "Avanzado" };
        private readonly string[] pageDescriptions = {
            "Elige el modo y empieza a pescar.", "Marca las áreas y los botones una vez.",
            "Comprueba la compra y las lecturas.", "Ajustes de detección, tiempos y reposición." };

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
        private void Hint(Control control, string text) { hintSources[control]=text;hints.SetToolTip(control,Localization.T(text)); }
        private void RefreshHints()
        {
            foreach(var pair in hintSources)hints.SetToolTip(pair.Key,Localization.T(pair.Value));
            foreach(var label in fullHints)hints.SetToolTip(label,Localization.T(translations.Source(label)));
        }
        private void FullTextHint(Label label)
        { fullHints.Add(label);label.TextChanged+=delegate{hints.SetToolTip(label,Localization.T(label.Text));};hints.SetToolTip(label,Localization.T(label.Text)); }
        private PictureBox PreviewAt(Control parent, int x, int y, int width, int height, string empty)
        {
            var picture=new PictureBox { Location=new Point(x,y), Size=new Size(width,height),
                BackColor=Color.FromArgb(24,34,44), SizeMode=PictureBoxSizeMode.Zoom };
            picture.Paint+=delegate(object sender,PaintEventArgs e){
                if(picture.Image==null)using(var font=new Font("Segoe UI",10))
                    TextRenderer.DrawText(e.Graphics,Localization.T(empty),font,picture.ClientRectangle,Color.FromArgb(162,174,187),
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
            purchaseCountdown.Visible=timed;
            purchaseModeHint.Text=timed?"Compra una cantidad cada cierto tiempo.":"Repone según el contador de cebo.";
            if(ocrSummary!=null)ocrSummary.Text="Con "+baitThreshold.Value+" cebos o menos,\ncompletar hasta "+baitCapacity.Value+".";
        }
        private void SelectTestView(bool readings)
        {
            testActions.Visible=!readings;testReadings.Visible=readings;
            actionsTabButton.BackColor=readings?Color.White:accent;actionsTabButton.ForeColor=readings?ink:Color.White;
            readingsTabButton.BackColor=readings?accent:Color.White;readingsTabButton.ForeColor=readings?Color.White:ink;
        }
        private void ShowQuickGuide()
        {
            MessageBox.Show(this,Localization.T("1. En Zonas, marca la barra, el agua y los botones de compra.\n2. Elige Contador OCR o Cronómetro en Inicio.\n3. Prueba la compra y las lecturas en Pruebas.\n4. Permite las entradas y vuelve a Roblox para iniciar.\n\nF6 selecciona la barra. F8 inicia o detiene. F10 detiene.\nCambiar de ventana también detiene la macro."),Localization.T("Guía rápida"),MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        private void BuildInterface()
        {
            var rail=new Panel { Dock=DockStyle.Left, Width=188, BackColor=Color.FromArgb(24,34,44) };
            Controls.Add(rail);
            LabelAt(rail,"SomeFishing",18,31,166,32,14,true).ForeColor=Color.White;
            LabelAt(rail,"GPO",22,66,145,22,10,false).ForeColor=Color.FromArgb(145,166,177);
            navigation=new Button[pageNames.Length];pages=new Panel[pageNames.Length];
            for(int i=0;i<pageNames.Length;i++){
                int destination=i;
                navigation[i]=ButtonAt(rail,pageNames[i],12,126+i*49,164,delegate{SelectPage(destination);},false);
                navigation[i].TextAlign=ContentAlignment.MiddleLeft;((ModernButton)navigation[i]).BorderVisible=false;
                pages[i]=new Panel { Location=new Point(212,104), Size=new Size(844,516), BackColor=BackColor, Visible=false };
                Controls.Add(pages[i]);
            }
            var guide=ButtonAt(rail,"Guía rápida",16,358,156,delegate{ShowQuickGuide();},false);
            guide.BackColor=rail.BackColor;guide.ForeColor=Color.FromArgb(181,191,203);((ModernButton)guide).BorderVisible=false;
            LabelAt(rail,"Idioma / Language",20,465,152,23,9.5f,false).ForeColor=Color.FromArgb(181,191,203);
            interfaceLanguage=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,494),Size=new Size(148,30),Font=new Font("Segoe UI",10),AccessibleName="Idioma / Language"};
            interfaceLanguage.Items.AddRange(new object[]{"Español","English"});rail.Controls.Add(interfaceLanguage);
            var save=ButtonAt(rail,"Guardar",16,593,156,delegate{SaveSettings();},false);
            Hint(save,"Guarda las zonas y los ajustes actuales.");save.BackColor=Color.FromArgb(34,48,61);save.ForeColor=Color.White;
            ((ModernButton)save).BorderVisible=false;
            LabelAt(rail,"Local  /  v0.7.1",22,657,145,23,9,false).ForeColor=Color.FromArgb(145,166,177);
            LabelAt(rail,"Código incluido",22,681,145,23,9,false).ForeColor=Color.FromArgb(145,166,177);
            pageTitle=LabelAt(this,"",212,15,800,49,25,true);
            pageSubtitle=LabelAt(this,"",214,69,820,27,10.5f,false);pageSubtitle.ForeColor=muted;

            var mode=Card(pages[0],0,0,372,516);
            LabelAt(mode,"Reposición de cebo",20,20,332,29,14,true);
            autoBuy=CheckAt(mode,"Comprar cebo · usa Peli",20,66,332);
            Hint(autoBuy,"Activa la reposición por contador o cronómetro. Permanece junto al barril de cebo.");
            purchaseMode=new LocalizedComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,109),Size=new Size(332,30),Font=new Font("Segoe UI",11)};
            purchaseMode.Items.AddRange(new object[]{"Contador OCR","Cronómetro"});mode.Controls.Add(purchaseMode);
            purchaseModeHint=LabelAt(mode,"",20,154,332,39,9.5f,false);purchaseModeHint.ForeColor=muted;
            timerCondition=new Panel{Location=new Point(20,211),Size=new Size(332,102),BackColor=Color.Transparent};mode.Controls.Add(timerCondition);
            ocrCondition=new Panel{Location=timerCondition.Location,Size=timerCondition.Size,BackColor=Color.White};mode.Controls.Add(ocrCondition);
            buyQuantity=NumberAt(timerCondition,"Cebos por compra",0,0,1,9999,50,156);
            purchaseMinutes=NumberAt(timerCondition,"Cada (minutos)",174,0,1,1440,40,158);
            Hint(buyQuantity,"Cantidad que se escribe en cada compra. El juego puede limitarla según capacidad y Peli.");
            Hint(purchaseMinutes,"Cuenta desde el inicio. Pausa la pesca para comprar y reinicia el intervalo tras la secuencia.");
            ocrSummary=LabelAt(ocrCondition,"",0,0,332,56,11,true);
            ButtonAt(ocrCondition,"Configurar contador",0,60,332,delegate{SelectPage(1);},false);
            Divider(mode,20,326,332);
            ButtonAt(mode,"Configurar zonas",20,348,332,delegate{SelectPage(1);},true);
            ButtonAt(mode,"Ir a pruebas",20,402,332,delegate{SelectPage(2);},false);
            var last=ButtonAt(mode,"Última parada",20,467,332,delegate{ShowLastStop();},false);last.Height=30;last.ForeColor=muted;((ModernButton)last).BorderVisible=false;
            var session=Card(pages[0],388,0,456,516);
            LabelAt(session,"Sesión",20,20,416,29,14,true);
            cycleLabel=LabelAt(session,"Rondas: 0",20,76,416,28,11,false);cycleLabel.ForeColor=muted;
            baitValueLabel=LabelAt(session,"Cebos: —",20,132,416,48,24,true);
            baitDetailLabel=LabelAt(session,"Configura las zonas para empezar.",20,198,416,61,10,false);FullTextHint(baitDetailLabel);
            purchaseCountdown=LabelAt(session,"El cronómetro empieza al iniciar la pesca.",20,292,416,56,12,true);FullTextHint(purchaseCountdown);
            Divider(session,20,378,416);
            LabelAt(session,"Inicia con Roblox en primer plano.",20,402,416,28,11,true);
            LabelAt(session,"F8 inicia o detiene. F10 detiene.",20,445,416,28,10,false).ForeColor=muted;

            var zones=Card(pages[1],0,0,372,516);
            LabelAt(zones,"Áreas y lanzamiento",20,20,332,29,14,true);
            areaButton=ButtonAt(zones,"Barra de pesca · F6",20,67,332,delegate{SelectArea();},true);
            areaLabel=LabelAt(zones,"Sin seleccionar",20,114,332,26,9.5f,false);
            Hint(areaButton,"Rodea toda la barra azul con margen lateral. Enter o F6 confirma; Esc cancela.");
            Divider(zones,20,161,332);
            pointButton=ButtonAt(zones,"Punto en el agua",20,195,332,delegate{SelectPoint();},false);
            castLabel=LabelAt(zones,"Sin seleccionar",20,242,332,26,9.5f,false);
            Hint(pointButton,"Elige dónde debe apuntar el ratón al volver a lanzar.");
            Divider(zones,20,290,332);
            baitAreaButton=ButtonAt(zones,"Contador de cebo",20,324,332,delegate{SelectBaitArea();},false);
            baitAreaLabel=LabelAt(zones,"Sin seleccionar",20,371,332,26,9.5f,false);
            LabelAt(zones,"Para OCR, rodea solo la x y el número.",20,425,332,46,10,false).ForeColor=muted;
            Hint(baitAreaButton,"Selecciona un solo tipo de cebo, por ejemplo x300, sin el borde amarillo ni otros números.");
            var buttons=Card(pages[1],388,0,456,516);
            LabelAt(buttons,"3 botones de compra",20,20,416,29,14,true);
            LabelAt(buttons,"Abre el menú de cantidad y marca el centro\nde cada botón.",20,65,416,44,10,false).ForeColor=muted;
            shopPointButtons=new Button[3];shopPointLabels=new Label[3];
            string[] pointNames={"1. Izquierda · Sí / Comprar","2. Centro · Número / …","3. Derecha · No / Cancelar"};
            for(int i=0;i<3;i++){
                int pointIndex=i;
                shopPointButtons[i]=ButtonAt(buttons,pointNames[i],20,128+i*76,416,delegate{SelectShopPoint(pointIndex);},i==0);
                shopPointLabels[i]=LabelAt(buttons,"Sin marcar",20,171+i*76,416,22,9,false);
                Hint(shopPointButtons[i],"Solo guarda el punto; el clic de selección no llega al juego.");
            }
            shopAreaButton=shopPointButtons[0];shopAreaLabel=LabelAt(buttons,"",20,372,416,38,9.5f,false);FullTextHint(shopAreaLabel);shopDetail=shopAreaLabel;
            LabelAt(buttons,"Conserva la posición y el tamaño del juego.",20,415,416,25,9.5f,false).ForeColor=muted;
            ButtonAt(buttons,"Probar una compra",20,459,416,delegate{SelectPage(2);SelectTestView(false);},false);
            shopPreview=new PictureBox{Visible=false};shopPreviewButton=new Button{Visible=false};buyMaximum=new CheckBox{Visible=false};

            actionsTabButton=ButtonAt(pages[2],"Compra",0,0,202,delegate{SelectTestView(false);},true);
            readingsTabButton=ButtonAt(pages[2],"Lecturas",218,0,202,delegate{SelectTestView(true);},false);
            testActions=new Panel{Location=new Point(0,54),Size=new Size(844,462),BackColor=BackColor};pages[2].Controls.Add(testActions);
            testReadings=new Panel{Location=testActions.Location,Size=testActions.Size,BackColor=BackColor};pages[2].Controls.Add(testReadings);
            var noBait=Card(testActions,0,0,414,197);
            LabelAt(noBait,"Sin cebo",20,17,374,29,14,true);
            emptyTestSummary=LabelAt(noBait,"",20,59,374,62,10,false);emptyTestSummary.ForeColor=muted;
            emptyTestButton=ButtonAt(noBait,"Probar sin cebo",20,140,374,delegate{ArmDiagnostic(RunKind.EmptyBaitTest);},false);
            var purchase=Card(testActions,430,0,414,197);
            LabelAt(purchase,"Compra",20,17,374,29,14,true);
            testBuyQuantity=NumberAt(purchase,"Cantidad de prueba",20,58,1,9999,1,180);
            LabelAt(purchase,"Usa Peli · un intento.\nSin OCR ni cronómetro.",216,78,178,45,9.5f,false).ForeColor=muted;
            purchaseTestButton=ButtonAt(purchase,"Probar compra",20,140,374,delegate{ArmDiagnostic(RunKind.PurchaseTest);},true);
            LabelAt(testActions,"Inicio en 3 s. Vuelve al juego con los diálogos cerrados. F10 cancela.",4,210,836,26,9.5f,false).ForeColor=muted;
            var result=Card(testActions,0,246,844,216);
            LabelAt(result,"Resultado",20,17,430,29,14,true);
            var copy=ButtonAt(result,"Copiar resultado",626,10,198,delegate{
                try{Clipboard.SetText(diagnosticLog.Text);statusLabel.Text="Resultado copiado.";}catch{statusLabel.Text="No se pudo copiar. El registro sigue disponible.";}
            },false);
            Hint(copy,"También se guarda en ultima-prueba.txt, junto al programa. No se envía automáticamente.");
            diagnosticLog=new TextBox{Location=new Point(20,64),Size=new Size(804,133),Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=muted,Font=new Font("Consolas",9),Text="Ejecuta una prueba para ver el resultado."};result.Controls.Add(diagnosticLog);
            var fishRead=Card(testReadings,0,0,414,462);
            LabelAt(fishRead,"Detector de pesca",20,17,374,29,14,true);
            preview=PreviewAt(fishRead,20,60,374,266,"Selecciona la barra en Zonas");
            detectionLabel=LabelAt(fishRead,"Solo lectura. No envía clics.",20,339,374,48,9.5f,false);FullTextHint(detectionLabel);
            previewButton=ButtonAt(fishRead,"Ver detector",20,402,180,delegate{TogglePreview();},false);
            ButtonAt(fishRead,"Ejemplo",214,402,180,delegate{ShowExample();},false);
            Hint(previewButton,"Celeste: barra. Naranja: hueco. Rosa: pez. El progreso verde se ignora.");
            var counterRead=Card(testReadings,430,0,414,462);
            LabelAt(counterRead,"Contador de cebo",20,17,374,29,14,true);
            baitPreview=PreviewAt(counterRead,20,60,374,90,"Selecciona el contador en Zonas");
            baitReadoutLabel=LabelAt(counterRead,"Cebos: —",20,179,374,45,22,true);
            baitReadoutDetail=LabelAt(counterRead,"Rodea solo la x y el número.",20,251,374,91,10,false);FullTextHint(baitReadoutDetail);
            baitPreviewButton=ButtonAt(counterRead,"Probar lectura",20,402,374,delegate{ToggleBaitPreview();},false);
            Hint(baitPreviewButton,"Solo lee la zona seleccionada. No envía clics, saltos ni compras.");
            Hint(emptyTestButton,"Simula cero: compra una unidad, da un salto o informa que ambas opciones están apagadas, según tus ajustes.");
            Hint(purchaseTestButton,"Una compra de la cantidad indicada, usando los 3 puntos marcados. No requiere OCR ni espera el cronómetro.");
            SelectTestView(false);

            var warning=Card(pages[3],0,0,844,64);
            LabelAt(warning,"Si no conoces estos ajustes, déjalos como están.",20,19,804,30,12,true);
            advancedScroll=new Panel{Location=new Point(0,80),Size=new Size(844,436),BackColor=BackColor,AutoScroll=true,AutoScrollMinSize=new Size(0,730)};pages[3].Controls.Add(advancedScroll);
            var fishing=Card(advancedScroll,0,0,808,326);
            LabelAt(fishing,"Pesca",20,17,768,29,14,true);
            autoCast=CheckAt(fishing,"Volver a lanzar automáticamente",20,60,380);
            holdUp=CheckAt(fishing,"Mantener clic sube el hueco",412,60,384);
            castTime=NumberAt(fishing,"Clic al lanzar (ms)",20,103,50,3000,220,236);
            biteTime=NumberAt(fishing,"Espera de picada (s)",286,103,5,120,15,236);
            restTime=NumberAt(fishing,"Pausa entre rondas (ms)",552,103,500,10000,1800,236);
            tolerance=NumberAt(fishing,"Tolerancia de color",20,183,5,90,38,236);
            anticipation=NumberAt(fishing,"Anticipación (ms)",286,183,0,300,80,236);
            Hint(anticipation,"Si el hueco se pasa del pez, aumenta el valor; si responde antes de tiempo, bájalo.");
            Hint(tolerance,"Ajusta primero la zona si el detector no encuentra el hueco o la línea.");
            blueButton=ButtonAt(fishing,"Color de barra",20,266,376,delegate{PickColor(true);},false);
            markerButton=ButtonAt(fishing,"Color de línea del pez",412,266,376,delegate{PickColor(false);},false);
            var buying=Card(advancedScroll,0,342,808,228);
            LabelAt(buying,"Compra y contador",20,17,768,29,14,true);
            shopOpenTime=NumberAt(buying,"Mantener E (ms)",20,63,100,3000,1000,236);
            shopSettle=NumberAt(buying,"Pausa entre pasos (ms)",286,63,200,3000,700,236);
            purchaseLimit=NumberAt(buying,"Tope por sesión",552,63,1,100,10,236);
            baitThreshold=NumberAt(buying,"Comprar si quedan ≤",20,145,0,9999,2,236);
            baitCapacity=NumberAt(buying,"Capacidad de cebo",286,145,1,9999,300,236);
            LabelAt(buying,"Idioma OCR del contador",552,145,236,23,9.5f,false).ForeColor=muted;
            ocrLanguage=new LocalizedComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(552,172),Size=new Size(236,30),Font=new Font("Segoe UI",10)};
            ocrTags.Add("");ocrLanguage.Items.Add("Automático (Windows)");
            foreach(var language in WindowsBaitReader.Languages()){ocrTags.Add(language.Key);ocrLanguage.Items.Add(language.Value);}
            buying.Controls.Add(ocrLanguage);ocrLanguage.SelectedIndex=0;
            ocrLanguage.SelectedIndexChanged+=delegate{if(!applyingLanguage&&(previewingBait||previewingShop))StopAll("Idioma cambiado. Vuelve a probar la lectura.");};
            Hint(ocrLanguage,"Idiomas OCR instalados en Windows. Solo se aplica al contador.");
            Hint(shopOpenTime,"Duración de la tecla E para abrir la compra.");
            Hint(shopSettle,"Espera entre las acciones del diálogo. Aumenta si el juego tarda en mostrar los botones.");
            Hint(purchaseLimit,"Máximo de intentos de compra por sesión.");
            Hint(baitThreshold,"Umbral que activa la reposición al confirmar el contador.");
            Hint(baitCapacity,"Total de cebo deseado. Con capacidad 300 y contador 2, solicita 298.");
            var waiting=Card(advancedScroll,0,586,808,144);
            LabelAt(waiting,"Espera y lector",20,17,510,29,14,true);
            idleJump=CheckAt(waiting,"Saltar durante la espera",20,65,510);
            monitorBait=CheckAt(waiting,"Leer contador fuera de compras",20,105,510);
            Hint(monitorBait,"La reposición por OCR activa el lector automáticamente. Cronómetro no necesita leer el contador.");
            jumpSeconds=NumberAt(waiting,"Intervalo de salto (s)",552,47,15,300,60,236);
            Hint(idleJump,"Salta en el sitio si falta cebo o tras tres lanzamientos sin minijuego. No garantiza evitar una desconexión.");
            purchaseMode.SelectedIndexChanged+=delegate{UpdatePurchaseControls(!IsRunning&&armedUntil==0);};
            baitThreshold.ValueChanged+=delegate{UpdatePurchaseControls(!IsRunning&&armedUntil==0);};
            baitCapacity.ValueChanged+=delegate{UpdatePurchaseControls(!IsRunning&&armedUntil==0);};
            autoBuy.CheckedChanged+=delegate{UpdateDiagnosticSummary();};idleJump.CheckedChanged+=delegate{UpdateDiagnosticSummary();};UpdateDiagnosticSummary();

            var footer=Card(this,212,636,844,76);
            statusLabel=LabelAt(footer,"Detenida · lista para configurar",20,8,426,25,9.5f,true);FullTextHint(statusLabel);
            allowClicks=CheckAt(footer,"Permitir clics y teclas",20,38,426);allowClicks.Height=25;
            Hint(allowClicks,"Autoriza entradas al iniciar. Se desmarca al abrir el programa de nuevo.");
            startButton=ButtonAt(footer,"Iniciar · 3 s",466,18,192,delegate{Arm();},true);
            var stop=ButtonAt(footer,"Detener · F10",674,18,150,delegate{StopAll("Detenida por ti");},false);stop.ForeColor=Color.FromArgb(180,53,68);
            Hint(startButton,"Vuelve a Roblox durante los 3 segundos. F8 inicia desde el juego; F10 cancela.");
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
