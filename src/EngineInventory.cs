using System;

namespace SomeFishingGPO
{
    public sealed partial class FishingEngine
    {
        private ManualBaitInventory inventory;
        private bool roundOpen, baitSelected, missRecoveryUsed, automaticBuyingBlocked;
        private IPurchaseFlow recovery;
        private string recoveryReason, ocrSupport = "OCR de apoyo desactivado";
        private double buyRetryAfter;
        public int RoundsStarted { get; private set; }
        public int CastAttempts { get; private set; }
        public int InventoryRevision { get; private set; }
        public ManualBaitInventory Inventory { get { return inventory; } }
        public string InventoryStatus { get { return inventory==null?"Inventario manual desactivado":
            "Estimación · "+BaitName(inventory.ActiveKind)+" · L: "+inventory.Count(BaitKind.Legendary)+
            " · R: "+inventory.Count(BaitKind.Rare)+" · C: "+inventory.Count(BaitKind.Common)+
            (inventory.Uncertain?" · cantidad dudosa":settings.MonitorBait?" · "+ocrSupport:""); } }
        public static string BaitName(BaitKind kind)
        { return kind==BaitKind.Legendary?"Legendario":kind==BaitKind.Rare?"Raro":"Común"; }

        private void ResetInventorySession()
        {
            lastTrackAt=-10000;RoundsStarted=0;CastAttempts=0;InventoryRevision=0;roundOpen=false;recovery=null;
            baitSelected=false;missRecoveryUsed=false;automaticBuyingBlocked=false;buyRetryAfter=0;
            if(settings.UseManualBait&&!IsDiagnostic)
            {
                inventory=new ManualBaitInventory(settings.ManualCommonBait,settings.ManualRareBait,settings.ManualLegendaryBait,settings.ActiveBaitKind);
                if(settings.ManualInventoryUncertain)inventory.MarkAllUncertain();
                else if(inventory.HighestAvailable.HasValue)inventory.Select(inventory.HighestAvailable.Value);
            }
            else inventory=null;
        }
        private void StartObservedRound()
        {
            if(roundOpen||IsDiagnostic)return;
            roundOpen=true;RoundsStarted++;missRecoveryUsed=false;
            if(inventory!=null){inventory.ConsumeRound(RoundsStarted);InventoryRevision++;}
        }
        private void FinishObservedRound()
        {
            if(!roundOpen)return;
            roundOpen=false;Cycles++;
            if(inventory!=null){inventory.FinishRound(RoundsStarted);InventoryRevision++;}
        }
        private bool EnsureBaitSelection(double now)
        {
            if(inventory==null)return false;
            if(inventory.Uncertain){Stop("Cantidad dudosa: actualiza los cebos en Cebos y aplica el inventario.");return true;}
            BaitKind? next=inventory.HighestAvailable;
            if(!next.HasValue){inventory.Select(BaitKind.Common);return false;}
            if(baitSelected&&inventory.ActiveKind==next.Value)return false;
            var selector=runtime as IBaitSelectionRuntime;
            if(selector==null){Stop("No está disponible la selección de tipos de cebo.");return true;}
            runtime.Release();inventory.Select(next.Value);bait.Reset();
            selector.BeginBaitSelection(next.Value,now);State=Phase.SelectingBait;
            Status="Seleccionando cebo: "+BaitName(next.Value);return true;
        }
        private void TickBaitSelection(double now)
        {
            var result=((IBaitSelectionRuntime)runtime).TickBaitSelection(now);
            Status=result.Status;
            if(!result.Completed)return;
            if(!result.Succeeded){Stop(result.Status);return;}
            baitSelected=true;InventoryRevision++;bait.Reset();
            State=Phase.Preparing;deadline=now+700;Status="Cebo seleccionado: "+BaitName(inventory.ActiveKind);
        }
        private bool CanManualBuy(double now)
        {
            return inventory!=null&&!inventory.Uncertain&&settings.AutoBuyBait&&!settings.PurchaseByTimer&&
                !automaticBuyingBlocked&&PurchaseAttempts<settings.PurchaseLimit&&now>=buyRetryAfter;
        }
        private bool HandleManualBait(double now)
        {
            if(inventory.Uncertain){Stop("Compra dudosa: corrige el inventario en Cebos antes de continuar.");return true;}
            if(inventory.ActiveKind!=BaitKind.Common&&inventory.ActiveCount>0)return false;
            if(CanManualBuy(now)&&inventory.ActiveCount<=settings.BuyBaitAt){BeginPurchase(now);return true;}
            if(inventory.ActiveCount>0)return false;
            // A depleted manual inventory may replenish immediately in timer mode;
            // this prevents dead waiting for the scheduled interval with zero bait.
            if(settings.TimedPurchases&&!automaticBuyingBlocked&&PurchaseAttempts<settings.PurchaseLimit&&now>=buyRetryAfter)
            {BeginPurchase(now);return true;}
            EnterIdle(now,"Inventario manual agotado",true);return true;
        }
        private void CheckSupportingOcr(double now)
        {
            if(inventory==null||!settings.MonitorBait||State==Phase.Purchasing)return;
            if(!bait.Count.HasValue||now-bait.ConfirmedAt>2500){ocrSupport="OCR sin lectura clara";return;}
            // OCR is supporting evidence. It cannot silently replace the initial
            // inventory or turn a failed order into a successful purchase.
            ocrSupport=bait.Count.Value==inventory.ActiveCount?"OCR coincide":"OCR: "+bait.Count.Value+" · revisa la diferencia";
        }
        private void CompleteManualPurchase()
        {
            if(inventory==null)return;
            var direct=purchase as DirectPurchaseController;
            bool evidence=direct!=null&&direct.CompletionObserved;
            inventory.EstimateCommonPurchase(PurchaseAttempts,purchase.Quantity,evidence);
            InventoryRevision++;baitSelected=false;
            if(!evidence){automaticBuyingBlocked=true;Stop("Compra enviada sin confirmar cierre: revisa el inventario en Cebos.");}
        }
        private void BeginRecovery(double now,string reason,bool failedOrder)
        {
            runtime.Release();controller.Reset();SuggestedHold=false;
            if(IsDiagnostic){Stop(reason);return;}
            recoveryReason=reason;
            if(failedOrder)
            {
                if(purchase!=null&&purchase.Submitted)
                {
                    automaticBuyingBlocked=true;
                    if(inventory!=null){inventory.MarkPurchaseUncertain(PurchaseAttempts);InventoryRevision++;}
                }
                buyRetryAfter=now+60000;
            }
            var shop=runtime as IShopRuntime;
            if(shop==null||!(runtime is IShopVisualRuntime)){Stop(reason);return;}
            recovery=new ShopRecoveryController(settings,runtime,shop);
            recovery.Start(now);State=Phase.RecoveringShop;Status="Recuperando menú · "+reason;
        }
        private void TickRecovery(double now)
        {
            recovery.Tick(now,null,0);Status=recovery.Status;
            if(recovery.State==PurchasePhase.Failed){Stop(recoveryReason+" · "+recovery.Status);return;}
            if(recovery.State!=PurchasePhase.Complete)return;
            if(inventory!=null&&inventory.Uncertain){Stop("Diálogo cerrado. Compra dudosa: escribe las cantidades actuales en Cebos.");return;}
            failures=0;baitSelected=false;State=Phase.Preparing;deadline=now+1500;
            Status="Menú cerrado · volviendo a pescar";
        }
    }
}
