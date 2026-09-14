using System;
using System.Drawing;
using System.Windows.Forms;

namespace SomeFishingGPO
{
    internal sealed partial class MainForm
    {
        private NumericUpDown manualCommon,manualRare,manualLegendary,castRetries,shopRetries,phaseTimeout;
        private Button applyInventory,selectBaitMenu;
        private Label inventoryReady,baitMenuLabel;
        private int lastInventoryRevision=-1;
        private bool changingInventory;
        private void BuildInventoryPage()
        {
            var counts=Card(pages[4],0,0,844,256);
            LabelAt(counts,"Tus cebos actuales",20,17,804,32,16,true);
            LabelAt(counts,"Escribe 0 si no tienes ese tipo. Se usarán en este orden:",20,62,804,28,10,false).ForeColor=muted;
            manualLegendary=NumberAt(counts,"1. Legendario",20,107,0,9999,0,246);
            manualRare=NumberAt(counts,"2. Raro",298,107,0,9999,0,246);
            manualCommon=NumberAt(counts,"3. Común",576,107,0,9999,0,246);
            applyInventory=ButtonAt(counts,"Aplicar inventario",20,194,246,delegate{ApplyManualInventory();},true);
            inventoryReady=LabelAt(counts,"Escribe las cantidades y aplica.",298,194,524,42,10,false);FullTextHint(inventoryReady);
            foreach(var number in new[]{manualCommon,manualRare,manualLegendary})number.ValueChanged+=delegate{
                if(changingInventory)return;settings.ManualInventoryConfirmed=false;UpdateInventoryLabels();
            };
            var menu=Card(pages[4],0,272,844,244);
            LabelAt(menu,"Menú de cebos",20,17,804,30,14,true);
            selectBaitMenu=ButtonAt(menu,"Seleccionar menú completo",20,68,374,delegate{SelectBaitMenu();},true);
            baitMenuLabel=LabelAt(menu,"Sin seleccionar",416,76,406,40,10,false);FullTextHint(baitMenuLabel);
            LabelAt(menu,"Incluye las filas de los tres tipos, con margen. El programa busca la fila\ny comprueba el borde amarillo al cambiar de cebo.",20,124,804,50,10,false).ForeColor=muted;
            LabelAt(menu,"La cuenta es estimada: 1 cebo por ronda detectada. Solo compra común.\nEl OCR opcional contrasta la cantidad; no reemplaza tu inventario.",20,187,804,47,9.5f,false).ForeColor=muted;
            Hint(applyInventory,"Confirma las cantidades actuales. Corrígelas si gastaste cebos fuera de la macro o una compra quedó dudosa.");
        }
        private void ApplyInventorySettings()
        {
            changingInventory=true;
            try{SetNumber(manualCommon,settings.ManualCommonBait);SetNumber(manualRare,settings.ManualRareBait);SetNumber(manualLegendary,settings.ManualLegendaryBait);}
            finally{changingInventory=false;}
            SetNumber(castRetries,settings.CastRetryLimit);SetNumber(shopRetries,settings.ShopRetryLimit);SetNumber(phaseTimeout,settings.ShopPhaseTimeoutSeconds);
        }
        private void UpdateInventoryLabels()
        {
            if(inventoryReady==null)return;
            inventoryReady.Text=settings.ManualInventoryUncertain?"Compra dudosa: corrige y aplica.":settings.ManualInventoryConfirmed?"Inventario listo · seguimiento estimado":"Escribe las cantidades y aplica.";
            inventoryReady.ForeColor=settings.ManualInventoryConfirmed&&!settings.ManualInventoryUncertain?accent:muted;
            baitMenuLabel.Text=settings.BaitMenuArea.IsEmpty?"Sin seleccionar":"Menú: "+settings.BaitMenuArea.Width+" × "+settings.BaitMenuArea.Height+" px";
        }
        private void ApplyManualInventory()
        {
            if(IsRunning||armedUntil>0)return;
            settings.ManualInventoryConfirmed=true;settings.ManualInventoryUncertain=false;
            settings.ManualCommonBait=(int)manualCommon.Value;settings.ManualRareBait=(int)manualRare.Value;settings.ManualLegendaryBait=(int)manualLegendary.Value;
            settings.ActiveBaitKind=settings.ManualLegendaryBait>0?BaitKind.Legendary:settings.ManualRareBait>0?BaitKind.Rare:BaitKind.Common;
            settings.UseManualBait=true;UpdateInventoryLabels();
            if(!testMode)SaveSettingsQuietly();
            statusLabel.Text="Inventario aplicado · legendario, raro y común";
        }
        private void SyncInventory()
        {
            if(engine==null||engine.IsDiagnostic||engine.Inventory==null||engine.InventoryRevision==lastInventoryRevision)return;
            var inventory=engine.Inventory;lastInventoryRevision=engine.InventoryRevision;
            changingInventory=true;
            try{SetNumber(manualCommon,inventory.Count(BaitKind.Common));SetNumber(manualRare,inventory.Count(BaitKind.Rare));SetNumber(manualLegendary,inventory.Count(BaitKind.Legendary));}
            finally{changingInventory=false;}
            settings.ManualCommonBait=inventory.Count(BaitKind.Common);settings.ManualRareBait=inventory.Count(BaitKind.Rare);settings.ManualLegendaryBait=inventory.Count(BaitKind.Legendary);
            settings.ActiveBaitKind=inventory.ActiveKind;settings.ManualInventoryUncertain=inventory.Uncertain;
            settings.ManualInventoryConfirmed=!inventory.Uncertain;UpdateInventoryLabels();
            if(!testMode)SaveSettingsQuietly();
        }
        private void SelectBaitMenu()
        {
            if(activePicker!=null)return;StopAll("Seleccionando menú de cebos…");Hide();
            try{using(var picker=new SelectionOverlay(false,settings.BaitMenuArea,null,SystemInformation.VirtualScreen,false,true)){
                picker.AreaTitle="MENÚ COMPLETO DE CEBOS";
                picker.AreaHelp="Incluye todas las filas de cebo con margen. Enter confirma; Esc cancela.";
                activePicker=picker;
                if(picker.ShowDialog()==DialogResult.OK){
                    string issue=Settings.ValidateBaitMenuArea(picker.Selection,SystemInformation.VirtualScreen);
                    if(issue!=null){statusLabel.Text=issue;return;}
                    settings.BaitMenuArea=picker.Selection;UpdateAreaLabels();if(!testMode)SaveSettingsQuietly();
                    statusLabel.Text="Menú de cebos guardado · las filas se buscan por su color";
                }
            }}catch(Exception error){statusLabel.Text=error.Message;}finally{activePicker=null;Show();Activate();}
        }
        internal void VerifyInventoryInputs(Action<bool,string> check)
        {
            manualLegendary.Value=11;manualRare.Value=108;manualCommon.Value=17;
            check(!ReadSettings().ManualInventoryConfirmed,"Typing inventory requires explicit Apply before fishing");
            ApplyManualInventory();var saved=ReadSettings();
            check(saved.UseManualBait&&saved.ManualInventoryConfirmed&&!saved.ManualInventoryUncertain&&saved.ManualLegendaryBait==11&&saved.ManualRareBait==108&&saved.ManualCommonBait==17,"Applying preserves three independent quantities as the primary inventory");
            settings.ManualInventoryUncertain=true;manualCommon.Value=14;ApplyManualInventory();
            check(!ReadSettings().ManualInventoryUncertain&&ReadSettings().ManualCommonBait==14,"An explicit inventory correction clears a previous uncertain order");
            purchaseMode.SelectedIndex=1;monitorBait.Checked=true;saved=ReadSettings();
            check(saved.UseManualBait&&saved.UsesBaitCounter&&saved.PurchaseByTimer,"Timer mode retains manual inventory and optional supporting OCR");
            manualLegendary.Value=0;check(!ReadSettings().ManualInventoryConfirmed,"Changing a quantity invalidates the previous inventory confirmation");
            ApplyManualInventory();check(ReadSettings().ActiveBaitKind==BaitKind.Rare,"With zero legendary bait the applied inventory chooses rare next");
            var snapshot=LocalizationTests.Snapshot(ReadSettings());interfaceLanguage.SelectedIndex=1;var english=ReadSettings();english.InterfaceLanguage="es";
            check(LocalizationTests.Snapshot(english)==snapshot,"Changing language preserves applied inventory, quantities, timer and OCR assistance");
            interfaceLanguage.SelectedIndex=0;
        }
    }
}
