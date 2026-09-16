using System;
using System.Drawing;

namespace SomeFishingGPO
{
    // Temporary collector: the existing fishing engine is paused between rounds.
    // The shop sequence has no quantity editing, numeric key, or Buy action.
    internal sealed class ReferenceCollector
    {
        internal enum Stage { Stopped, SelectCommon, Preflight, BaitA, BaitB, OpenE, ReleaseE,
            Confirm, AimYes, ClickYes, Quantity, MaxA, MaxB, AimCancel, ClickCancel,
            Closed, AimClose, ClickClose, Fishing, Complete }
        private readonly Settings settings;
        private readonly IGameRuntime game;
        private readonly IShopRuntime shop;
        private readonly IShopPointerRuntime pointer;
        private readonly IShopVisualRuntime visual;
        private readonly IBaitSelectionRuntime selection;
        private readonly Action<int,int,string> capture;
        private readonly Action<int,int> pairSaved;
        private readonly int initial;
        private FishingEngine engine;
        private ShopVisualEvidence evidence;
        private double entered,next,last,started,roundStarted;
        private int aimPhase;
        private bool startedOnce;
        internal Stage State { get; private set; }
        internal string Status { get; private set; }
        internal int Rounds { get; private set; }
        internal int Samples { get; private set; }
        internal int Remaining { get { return initial-Rounds; } }
        internal int RoundLimit { get { return initial-1; } }
        internal bool Running { get { return startedOnce&&State!=Stage.Stopped&&State!=Stage.Complete; } }

        internal static Settings FishingOptions(Settings source,int count)
        {
            if(count<1||count>30)throw new ArgumentOutOfRangeException("count");
            Settings value=source.ForPurchase(1);
            value.AutoBuyBait=false;value.PurchaseByTimer=false;value.MonitorBait=false;
            value.IdleJumpEnabled=false;value.AutoCast=true;value.UseManualBait=true;
            value.ManualCommonBait=count;value.ManualRareBait=0;value.ManualLegendaryBait=0;
            value.ManualInventoryConfirmed=true;value.ManualInventoryUncertain=false;
            value.ActiveBaitKind=BaitKind.Common;value.UseBaitPoints=true;value.KeepOneBait=true;
            value.UseDirectShopFlow=true;value.ShopSequenceOnly=true;value.LongSessionMode=false;
            return value;
        }
        internal static Settings RuntimeOptions(Settings source,int count)
        {
            Settings value=FishingOptions(source,count);
            // Native guards require this permission for E and the initial Yes.
            // The engine gets a DIFFERENT settings object with buying disabled.
            value.AutoBuyBait=true;return value;
        }
        internal ReferenceCollector(Settings source,int count,IGameRuntime game,Action<int,int,string> capture,Action<int,int> pairSaved)
        {
            settings=FishingOptions(source,count);initial=count;this.game=game;
            shop=game as IShopRuntime;pointer=game as IShopPointerRuntime;
            visual=game as IShopVisualRuntime;selection=game as IBaitSelectionRuntime;
            if(shop==null||pointer==null||visual==null||selection==null||capture==null||pairSaved==null)
                throw new ArgumentException("Faltan servicios de recopilación.");
            DirectPurchaseTargets.ForSettings(settings);
            this.capture=capture;this.pairSaved=pairSaved;Status="Lista para recopilar";
        }
        internal void Start(double now)
        {
            if(startedOnce)throw new InvalidOperationException("Crea otra sesión para repetir la recopilación.");
            if(double.IsNaN(now)||double.IsInfinity(now)||now<0)throw new ArgumentOutOfRangeException("now");
            startedOnce=true;started=last=now;
            try
            {
                if(!game.IsActive)throw new InvalidOperationException("Roblox no está en primer plano.");
                game.Release();selection.BeginBaitSelection(BaitKind.Common,now);
                Enter(Stage.SelectCommon,now,"Seleccionando cebo común");
            }
            catch(Exception error){Stop(error.Message);}
        }
        internal void Stop(string reason)
        {
            State=Stage.Stopped;Status=reason;
            try { if(engine!=null)engine.Stop(reason); }
            finally { game.Release(); }
        }
        private void Enter(Stage state,double now,string status,double wait=0)
        {
            State=state;entered=now;next=now+wait;Status=status;evidence=new ShopVisualEvidence(now);aimPhase=0;
        }
        private bool Seen(ShopMenuKind kind,double now)
        {
            bool fresh=evidence.Read(visual.ReadShopVisual(now),now);
            return fresh&&evidence.Stable&&evidence.Kind==kind;
        }
        private void Aim(Point point,Stage click,double now,string label)
        {
            if(aimPhase==0){shop.ShopAim(point);aimPhase=1;return;}
            if(!pointer.TickShopAim(now))return;
            Enter(click,now,label,Math.Max(700,settings.ShopSettleMilliseconds));
        }
        private void BeginPair(double now)
        { game.Release();Enter(Stage.Preflight,now,"Esperando que cierre la ronda",Math.Max(1800,settings.RestMilliseconds)); }
        internal void Tick(double now)
        {
            if(!Running)return;
            try
            {
                if(double.IsNaN(now)||double.IsInfinity(now)||now<last)throw new InvalidOperationException("El reloj dejó de avanzar correctamente.");
                last=now;
                if(!game.IsActive)throw new InvalidOperationException("Detenida: Roblox perdió el foco o se solicitó parar.");
                if(now-started>7200000)throw new InvalidOperationException("Finalizó el límite de dos horas de recopilación.");
                if(State!=Stage.Fishing&&now-entered>15000)throw new InvalidOperationException("No se confirmó el paso: "+State+". Revisa los puntos y el menú.");
                if(now<next)return;
                switch(State)
                {
                    case Stage.SelectCommon:
                        var selected=selection.TickBaitSelection(now);
                        if(!selected.Completed)return;
                        if(!selected.Succeeded)throw new InvalidOperationException(selected.Status);
                        BeginPair(now);break;
                    case Stage.Preflight:
                        Observation frame=game.Observe();
                        if(frame.Found||frame.MenuVisible)return;
                        if(Seen(ShopMenuKind.Absent,now))Enter(Stage.BaitA,now,"Guardando contador de cebos");
                        break;
                    case Stage.BaitA:
                        capture(Rounds,Remaining,"cebos-a");Enter(Stage.BaitB,now,"Guardando segunda captura",300);break;
                    case Stage.BaitB:
                        capture(Rounds,Remaining,"cebos-b");Enter(Stage.OpenE,now,"Preparando diálogo");break;
                    case Stage.OpenE:
                        // Never send E over a newly appeared fishing menu.
                        Observation beforeOpen=game.Observe();
                        if(beforeOpen.Found||beforeOpen.MenuVisible)throw new InvalidOperationException("Apareció otra ronda antes de abrir el diálogo.");
                        shop.ShopKey(0x45,true);Enter(Stage.ReleaseE,now,"Manteniendo E",settings.ShopOpenMilliseconds);break;
                    case Stage.ReleaseE:
                        shop.ShopKey(0x45,false);Enter(Stage.Confirm,now,"Esperando Sí / No",1800);break;
                    case Stage.Confirm:
                        if(Seen(ShopMenuKind.Confirm,now))Enter(Stage.AimYes,now,"Moviendo a Sí");break;
                    case Stage.AimYes: Aim(settings.ShopLeftPoint,Stage.ClickYes,now,"Esperando antes de Sí");break;
                    case Stage.ClickYes:
                        // Yes and Buy share a point. A fresh confirmed Yes/No
                        // layout is mandatory immediately before the only left click.
                        if(Seen(ShopMenuKind.Confirm,now))
                        { shop.ShopClick(settings.ShopLeftPoint,ShopClickKind.Button);Enter(Stage.Quantity,now,"Esperando MAX",1800); }
                        break;
                    case Stage.Quantity:
                        if(Seen(ShopMenuKind.Quantity,now))Enter(Stage.MaxA,now,"Guardando MAX",500);break;
                    case Stage.MaxA:
                        capture(Rounds,Remaining,"max-a");Enter(Stage.MaxB,now,"Guardando segunda captura de MAX",300);break;
                    case Stage.MaxB:
                        capture(Rounds,Remaining,"max-b");Enter(Stage.AimCancel,now,"Moviendo a Cancelar");break;
                    case Stage.AimCancel: Aim(settings.ShopRightPoint,Stage.ClickCancel,now,"Esperando antes de Cancelar");break;
                    case Stage.ClickCancel:
                        if(Seen(ShopMenuKind.Quantity,now))
                        {shop.ShopClick(settings.ShopRightPoint,ShopClickKind.Button);Enter(Stage.Closed,now,"Esperando cierre del diálogo",1800);}
                        break;
                    case Stage.Closed:
                        bool fresh=evidence.Read(visual.ReadShopVisual(now),now);
                        if(!fresh||!evidence.Stable)return;
                        if(evidence.Kind==ShopMenuKind.Done){Enter(Stage.AimClose,now,"Moviendo al cierre …");return;}
                        if(evidence.Kind!=ShopMenuKind.Absent)return;
                        Observation afterClose=game.Observe();
                        if(afterClose.Found||afterClose.MenuVisible)throw new InvalidOperationException("Apareció una ronda inesperada durante las capturas.");
                        game.Release();pairSaved(Rounds,Remaining);Samples++;
                        if(Rounds>=RoundLimit)
                        {
                            if(engine!=null)engine.Stop("Recopilación terminada");
                            State=Stage.Complete;Status="Terminada: "+Samples+" muestras. Revisa cantidades repetidas antes de etiquetar.";return;
                        }
                        if(engine==null){engine=new FishingEngine(settings,game);engine.Start(now);}
                        roundStarted=now;Enter(Stage.Fishing,now,"Pescando la siguiente ronda");break;
                    case Stage.AimClose: Aim(settings.ShopMiddlePoint,Stage.ClickClose,now,"Esperando cierre …");break;
                    case Stage.ClickClose:
                        if(Seen(ShopMenuKind.Done,now))
                        {shop.ShopClick(settings.ShopMiddlePoint,ShopClickKind.Button);Enter(Stage.Closed,now,"Esperando cierre",1800);}
                        break;
                    case Stage.Fishing:
                        if(now-roundStarted>180000)throw new InvalidOperationException("No terminó una ronda en tres minutos. Recopilación detenida.");
                        engine.Tick(now);Status=engine.Status;
                        if(!engine.Running)throw new InvalidOperationException(engine.Status);
                        if(engine.PurchaseAttempts!=0)throw new InvalidOperationException("Una compra no está permitida en el recopilador.");
                        if(engine.Cycles>Rounds)
                        {
                            if(engine.Cycles!=Rounds+1||engine.RoundsStarted!=engine.Cycles)
                                throw new InvalidOperationException("La cuenta de rondas necesita revisión.");
                            Rounds=engine.Cycles;BeginPair(now);
                        }
                        break;
                }
            }
            catch(Exception error)
            {
                string reason=error.Message;
                try{game.Release();if(game.IsActive)capture(Rounds,Remaining,"interrumpido");}catch{}
                Stop(reason);
            }
        }
    }
}

