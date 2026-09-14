using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace SomeFishingGPO
{
    internal static class LocalizationTests
    {
        internal static string Snapshot(Settings settings)
        {
            using(var writer=new StringWriter(CultureInfo.InvariantCulture))
            { new XmlSerializer(typeof(Settings)).Serialize(writer,settings);return writer.ToString(); }
        }
        internal static void Run(Action<bool,string> check,string output)
        {
            string previous=Localization.Language;CultureInfo culture=CultureInfo.CurrentCulture;
            try
            {
                check(new Settings().InterfaceLanguage=="es","Existing installations default to Spanish when no interface language is saved");
                string legacyPath=Path.Combine(output,"legacy-language.xml");
                File.WriteAllText(legacyPath,"<Settings><BuyQuantity>73</BuyQuantity><OcrLanguage>es-MX</OcrLanguage></Settings>");
                var loaded=Settings.Load(legacyPath);
                check(loaded.InterfaceLanguage=="es"&&loaded.OcrLanguage=="es-MX"&&loaded.BuyQuantity==73,"Old XML loads with Spanish without losing the OCR language or purchase quantity");
                string newPath=Path.Combine(output,"saved-language.xml");
                loaded.InterfaceLanguage="en";loaded.Save(newPath);loaded=Settings.Load(newPath);
                check(loaded.InterfaceLanguage=="en"&&loaded.OcrLanguage=="es-MX","Interface language persists independently of the Windows OCR language");
                check(Localization.Normalize(null)=="es"&&Localization.Normalize("unexpected")=="es"&&Localization.Normalize("en")=="en","Unknown interface languages have a defined Spanish fallback");
                Localization.Language="en";
                bool catalog=true;int entries=0;
                foreach(var item in Localization.Entries){entries++;if(Localization.T(item.Key)!=item.Value)catalog=false;}
                check(catalog&&entries>350,"Every embedded English translation resolves exactly");
                check(Localization.T("Con 2 cebos o menos,\ncompletar hasta 300.")=="With 2 bait or less,\nrefill to 300.","Dynamic OCR threshold and capacity summary translates without changing amounts");
                check(Localization.T("Rondas: 14 · Cebos: 298")=="Rounds: 14 · Bait: 298","Dynamic session counts translate correctly");
                check(Localization.T("Próxima compra en 39:59")=="Next purchase in 39:59","Timer display keeps the exact countdown");
                check(Localization.T("Botones de cantidad visibles · verde izquierdo: 30 · blanco central: 15 · rojo derecho: 25")=="Quantity buttons visible · left green: 30 · middle white: 15 · right red: 25","Purchase color diagnostics translate with their measured counts intact");
                check(Localization.T("Compra con puntos detenida: Roblox perdió el foco.")=="Purchase by points stopped: Roblox lost focus.","Nested purchase failure and reason translate together");
                check(Localization.T("Punto guardado: -1280, 720")=="Saved point: -1280, 720","Translation preserves signed screen coordinates");
                check(Localization.T("Cebos: 2\r\nRondas: 4")=="Bait: 2\r\nRounds: 4","Multiline logs preserve Windows line endings");
                check(Localization.T("SomeFishingGPO.exe · es-MX · F8 · 0x45")=="SomeFishingGPO.exe · es-MX · F8 · 0x45","Identifiers, OCR tags, filenames and keys are not translated");
                check(Localization.T("ForemostConsolasCronómetroExtra")=="ForemostConsolasCronómetroExtra","Translation phrases do not replace parts of unrelated words");
                check(CultureInfo.CurrentCulture==culture,"Selecting an interface language never changes number formatting or the system culture");
                var cfg=new Settings{AutoCast=false};
                using(var bitmap=SelfTests.CreateSample())
                {
                    var observation=Detector.Analyze(bitmap,cfg);
                    check(observation.Found&&observation.Detail=="Pez (línea blanca) y hueco gris detectados","Detector data remains in its canonical language when English is displayed");
                }
                check(BaitText.Parse("x4")==4&&BaitText.Parse("x300")==300,"OCR parsing is independent of interface language");
                Localization.Language="es";
                check(Localization.T("Inicio")=="Inicio"&&Localization.T("Cebos: 2")=="Cebos: 2","Spanish display keeps original text unchanged");
                using(var form=new MainForm(true))form.VerifyLanguageSwitch(check);
                using(var form=new MainForm(true,new Settings{InterfaceLanguage="en",OcrLanguage="es-MX"}))
                {check(form.CurrentLanguageTitle=="Home","A saved English preference applies on startup");}
                var englishOutput=Path.Combine(output,"en");Directory.CreateDirectory(englishOutput);
                using(var form=new MainForm(true,new Settings{InterfaceLanguage="en"}))form.RenderExample(Path.Combine(englishOutput,"interfaz.png"));
                Localization.Language="en";
                using(var background=new Bitmap(1080,720))
                using(var picker=new SelectionOverlay(false,new Rectangle(450,180,160,340),background,new Rectangle(0,0,1080,720)))
                    picker.RenderPreview(Path.Combine(englishOutput,"selector.png"));
            }
            finally { Localization.Language=previous; }
        }
    }

    internal sealed partial class MainForm
    {
        internal string CurrentLanguageTitle { get { return pageTitle.Text; } }
        internal void VerifyLanguageSwitch(Action<bool,string> check)
        {
            var originalButton=areaButton;
            settings.Area=new Rectangle(-900,100,300,500);settings.CastPoint=new Point(-600,400);settings.CastPointSet=true;
            settings.BaitArea=new Rectangle(100,100,40,30);settings.ShopButtonsSet=true;
            settings.ShopLeftPoint=new Point(100,300);settings.ShopMiddlePoint=new Point(200,300);settings.ShopRightPoint=new Point(300,300);
            buyQuantity.Value=73;purchaseMinutes.Value=27;baitThreshold.Value=3;baitCapacity.Value=400;
            purchaseMode.SelectedIndex=1;allowClicks.Checked=true;
            ocrTags.Add("es-MX");ocrLanguage.Items.Add("Español (es-MX)");ocrLanguage.SelectedIndex=ocrTags.Count-1;
            UpdateAreaLabels();
            string before=LocalizationTests.Snapshot(ReadSettings());
            previewingBait=true; // No reader or timer in test mode; detects accidental stop during relabeling.
            interfaceLanguage.SelectedIndex=1;
            var after=ReadSettings();after.InterfaceLanguage="es";
            check(LocalizationTests.Snapshot(after)==before,"Switching to English preserves every existing setting, area, button and OCR tag");
            check(ReferenceEquals(areaButton,originalButton)&&allowClicks.Checked&&previewingBait,"Language switching keeps controls and previews alive without changing input permission");
            check(pageTitle.Text=="Home"&&navigation[3].Text=="Advanced"&&areaButton.Text=="Change area · F6","Navigation and existing area labels switch to English immediately");
            check(purchaseMode.GetItemText(purchaseMode.Items[0])=="Inventory and rounds"&&purchaseMode.GetItemText(purchaseMode.Items[1])=="Timer","Purchase mode choices display in English without changing their indexes");
            check(ocrLanguage.GetItemText(ocrLanguage.Items[0])=="Automatic (Windows)"&&ocrTags[ocrLanguage.SelectedIndex]=="es-MX","OCR automatic option translates while the selected OCR language stays intact");
            check(hints.GetToolTip(areaButton).StartsWith("Select the entire blue bar"),"Tooltips follow the interface language");
            ShowBaitCount(4,"Cantidad confirmada · 4 cebos");
            check(baitValueLabel.Text=="Bait: 4"&&baitReadoutLabel.Text=="Bait: 4"&&baitReadoutDetail.Text=="Quantity confirmed · 4 bait","Live counter readings and their mirror use English");
            statusLabel.Text="Moviendo el puntero al agua…";
            check(statusLabel.Text=="Moving the pointer to the water…"&&translations.Source(statusLabel)=="Moviendo el puntero al agua…","Live status text retains its canonical source separately from its translation");
            AppendDiagnostic("Cebos: 4 · Cantidad confirmada");
            check(diagnosticLog.Text.Contains("Bait: 4 · Quantity confirmed"),"Diagnostic output translates before copying or saving");
            interfaceLanguage.SelectedIndex=0;
            check(pageTitle.Text=="Inicio"&&baitReadoutLabel.Text=="Cebos: 4"&&statusLabel.Text=="Moviendo el puntero al agua…","Switching back restores original static and live Spanish text");
            check(diagnosticLog.Text.Contains("Cebos: 4 · Cantidad confirmada"),"Switching back restores the canonical diagnostic log without reverse translation");
            check(LocalizationTests.Snapshot(ReadSettings())==before&&previewingBait,"Round-trip language changes preserve every setting and preview state");
            foreach(int page in new[]{1,2,3,4,0})
            {SelectPage(page);interfaceLanguage.SelectedIndex=1;interfaceLanguage.SelectedIndex=0;}
            check(LocalizationTests.Snapshot(ReadSettings())==before&&pageTitle.Text=="Inicio","The always-visible selector works across every page without rebuilding forms");
            previewingBait=false;
        }
    }
}
