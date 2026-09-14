using System;

namespace SomeFishingGPO
{
    public sealed partial class FishingEngine
    {
        private bool IsLongSession { get { return settings.LongSessionMode&&!IsDiagnostic; } }
        private bool PurchaseBudgetAvailable { get { return PurchaseBudgetUsed<settings.PurchaseLimit; } }
        public int PurchaseBudgetUsed { get { return IsLongSession?OrdersSubmitted:PurchaseAttempts; } }
        public int OrdersSubmitted { get; private set; }
        public int Recoveries { get; private set; }
        private int consecutiveRecoveries;
        private bool orderSubmissionCounted,resumeCleanup;
        private double sessionStarted,resumeAfter,resumeClearSince,roundRecoveryStarted,roundClearSince;
        private string resumeReason;
        private int recoveredFrames;
        private void ResetLongSession(double now)
        {
            OrdersSubmitted=0;Recoveries=0;consecutiveRecoveries=0;orderSubmissionCounted=false;
            sessionStarted=now;resumeAfter=0;resumeClearSince=-1;roundClearSince=-1;recoveredFrames=0;
        }
        private void CountSubmittedOrder()
        {
            if(purchase!=null&&purchase.Submitted&&!orderSubmissionCounted){orderSubmissionCounted=true;OrdersSubmitted++;}
        }
        public string SessionHealth(double now)
        {
            var elapsed=TimeSpan.FromMilliseconds(Math.Max(0,now-sessionStarted));
            return string.Format("Tiempo: {0:00}:{1:00} · Compras: {2}/{3} · Recuperaciones: {4}",(int)elapsed.TotalHours,elapsed.Minutes,PurchaseBudgetUsed,settings.PurchaseLimit,Recoveries);
        }
        private bool CanResumeKnownInventory()
        {
            if(inventory!=null&&inventory.Uncertain)return false;
            if(inventory!=null&&NextUsableBait().HasValue)return true;
            return settings.AutoBuyBait&&!automaticBuyingBlocked&&PurchaseBudgetAvailable;
        }
        private bool RegisterRecovery(string reason)
        {
            if(consecutiveRecoveries>=settings.RecoveryLimit){Stop("Recuperación agotada: "+reason);return false;}
            consecutiveRecoveries++;Recoveries++;return true;
        }
        private void ScheduleResume(double now,string reason,bool alreadyCounted,bool cleanup)
        {
            runtime.Release();controller.Reset();SuggestedHold=false;
            if(inventory!=null&&inventory.Uncertain){Stop("Compra dudosa: corrige el inventario antes de continuar.");return;}
            if(!CanResumeKnownInventory()){Stop(reason+" · sin cebo ni reposición disponible");return;}
            if(!alreadyCounted&&!RegisterRecovery(reason))return;
            resumeReason=reason;resumeCleanup=cleanup;resumeClearSince=-1;
            double pause=Math.Min(300000,settings.RecoveryPauseSeconds*1000.0*Math.Pow(2,Math.Max(0,consecutiveRecoveries-1)));
            resumeAfter=Math.Max(now+pause,buyRetryAfter);
            State=Phase.Resuming;Status="Pausa de recuperación · "+reason;
        }
        private void TickResume(double now)
        {
            Status="Reintento en "+Math.Max(0,Math.Ceiling((resumeAfter-now)/1000))+" s · "+resumeReason;
            if(now<resumeAfter)return;
            if(resumeCleanup){BeginRecovery(now,resumeReason,false);return;}
            LastObservation=runtime.Observe();
            if(LastObservation.Found||LastObservation.MenuVisible)
            {BeginRoundRecovery(now,"Minijuego visible durante la recuperación",false);return;}
            if(resumeClearSince<0)resumeClearSince=now;
            if(now-resumeClearSince<1600)return;
            failures=0;missRecoveryUsed=false;baitSelected=false;
            State=Phase.Preparing;deadline=now+500;Status="Reanudando tras la pausa";
        }
        private void BeginRoundRecovery(double now,string reason,bool count)
        {
            runtime.Release();controller.Reset();SuggestedHold=false;
            if(count&&!RegisterRecovery(reason))return;
            roundRecoveryStarted=now;roundClearSince=-1;recoveredFrames=0;
            State=Phase.RecoveringRound;Status="Esperando recuperar la barra · "+reason;
        }
        private void TickRoundRecovery(double now)
        {
            LastObservation=runtime.Observe();
            if(LastObservation.Found)
            {
                roundClearSince=-1;
                if(++recoveredFrames<2)return;
                if(inventory!=null&&!roundOpen&&!baitSelected){Stop("No pude confirmar el tipo de cebo de esta ronda.");return;}
                StartObservedRound();trackingStarted=lastTrackAt=now;missingSince=invalidSince=-1;stableFrames=2;
                controller.Reset();State=Phase.Tracking;Status="Barra recuperada · misma ronda";return;
            }
            recoveredFrames=0;
            if(LastObservation.MenuVisible)roundClearSince=-1;
            else
            {
                if(roundClearSince<0)roundClearSince=now;
                if(now-roundClearSince>=1600)
                {
                    FinishObservedRound();
                    ScheduleResume(now,"La ronda terminó durante la recuperación",true,false);return;
                }
            }
            Status="Esperando la barra o el cierre del minijuego · clic liberado";
            if(now-roundRecoveryStarted>=90000)Stop("El minijuego no se pudo recuperar en 90 segundos. Revisa la zona.");
        }
    }
}
