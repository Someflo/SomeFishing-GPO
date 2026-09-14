using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace SomeFishingGPO
{
    public interface IShopPointerRuntime
    {
        bool TickShopAim(double now);
    }
    public interface IPurchaseFlow
    {
        PurchasePhase State { get; }
        string Status { get; }
        string Diagnostic { get; }
        bool Submitted { get; }
        int Quantity { get; }
        void Start(double now);
        void Tick(double now, int? baitCount, double baitConfirmedAt);
        void Fail(string reason);
    }

    // The user explicitly marks the three buttons. Right is reserved for recovery.
    public sealed class DirectPurchaseTargets
    {
        public Point Yes { get; private set; }
        public Point Quantity { get; private set; }
        public Point Buy { get; private set; }
        public Point Close { get; private set; }
        public Point Right { get; private set; }

        public static DirectPurchaseTargets ForSettings(Settings settings)
        {
            if(settings==null)throw new ArgumentNullException("settings");
            if(!settings.ShopButtonsSet)throw new ArgumentException("Marca los tres botones de compra.","settings");
            if(settings.ShopLeftPoint.X>=settings.ShopMiddlePoint.X||settings.ShopMiddlePoint.X>=settings.ShopRightPoint.X)
                throw new ArgumentException("Los puntos deben estar en orden: izquierdo, central y derecho.","settings");
            var result=new DirectPurchaseTargets {
                Yes=settings.ShopLeftPoint,Quantity=settings.ShopMiddlePoint,Buy=settings.ShopLeftPoint,
                Close=settings.ShopMiddlePoint,Right=settings.ShopRightPoint
            };
            foreach(Point point in new[]{result.Yes,result.Quantity,result.Right})
                if(point.X==int.MaxValue||point.Y==int.MaxValue)
                    throw new ArgumentException("Un punto de compra no tiene coordenadas válidas.","settings");
            return result;
        }
    }

    // A one-shot order: marked points replace OCR. A visual runtime can require
    // the quantity menu's green/white/red buttons before advancing.
    // Each tick runs at most one action. No catch-up loop can emit a burst of
    // inputs after a delayed tick, and no action survives a terminal state.
    public sealed class DirectPurchaseController : IPurchaseFlow
    {
        private enum ActionKind { KeyDown, KeyUp, Aim, Click, VerifyMenu, Finish }
        private sealed class Step
        {
            internal ActionKind Kind;
            internal PurchasePhase Phase;
            internal string Label;
            internal double Delay;
            internal int Key;
            internal Point Point;
            internal ShopClickKind ClickKind;
            internal bool IsBuy;
            internal ShopMenuKind Expected,RetryMenu;
            internal int RetryStep=-1;
        }
        private readonly Settings settings;
        private readonly IGameRuntime game;
        private readonly IShopRuntime shop;
        private readonly List<Step> steps=new List<Step>();
        private int stepIndex;
        private bool started;
        private double startedAt,next,lastTick;
        private bool waitingPointer;
        private double pointerSettle, visualStarted=-1;
        private ShopVisualEvidence visualEvidence;
        private readonly Dictionary<int,int> retries=new Dictionary<int,int>();
        private bool doneObserved;
        private bool retryOpening;
        private double verifiedAt=-1,actionStarted=-1;
        private ShopMenuKind verifiedKind;
        private ShopVisualEvidence actionEvidence;
        private string lastAction="Sin entradas";
        public PurchasePhase State { get; private set; }
        public string Status { get; private set; }
        public bool Submitted { get; private set; }
        public bool CompletionObserved { get; private set; }
        public int Quantity { get; private set; }
        public string Diagnostic { get { return "Compra sin OCR de menús · Paso: "+State+" · cantidad solicitada: "+Quantity+
            " · Comprar enviado: "+Submitted+" · "+lastAction+" · puntos marcados; resultado sin verificar"; } }

        public DirectPurchaseController(Settings settings,IGameRuntime game,IShopRuntime shop)
        {
            if(settings==null)throw new ArgumentNullException("settings");
            if(game==null)throw new ArgumentNullException("game");
            if(shop==null)throw new ArgumentNullException("shop");
            this.settings=settings;this.game=game;this.shop=shop;
            State=PurchasePhase.Opening;Status="Compra con puntos preparada";
        }
        public void Start(double now)
        {
            if(started)return;
            started=true;startedAt=lastTick=now;
            try {
                if(double.IsNaN(now)||double.IsInfinity(now)||now<0)throw new ArgumentException("Tiempo de inicio no válido.");
                if(!settings.AutoBuyBait)throw new InvalidOperationException("La compra está desactivada.");
                if(settings.BuyQuantity<1||settings.BuyQuantity>9999)throw new ArgumentException("La cantidad debe estar entre 1 y 9999.");
                if(settings.ShopOpenMilliseconds<100||settings.ShopOpenMilliseconds>3000)throw new ArgumentException("Mantener E debe estar entre 100 y 3000 ms.");
                if(settings.ShopSettleMilliseconds<200||settings.ShopSettleMilliseconds>3000)throw new ArgumentException("La pausa entre pasos debe estar entre 200 y 3000 ms.");
                if(settings.ShopRetryLimit<0||settings.ShopRetryLimit>5)throw new ArgumentException("Los reintentos de compra deben estar entre 0 y 5.");
                if(settings.ShopPhaseTimeoutSeconds<3||settings.ShopPhaseTimeoutSeconds>60)throw new ArgumentException("La espera por fase debe estar entre 3 y 60 segundos.");
                DirectPurchaseTargets targets=DirectPurchaseTargets.ForSettings(settings);
                if(!game.IsActive)throw new InvalidOperationException("Roblox perdió el foco.");
                Quantity=settings.BuyQuantity;
                Build(targets,settings.ShopOpenMilliseconds,settings.ShopSettleMilliseconds);
                game.Release();RunStep(now);
            }
            catch(Exception error){Fail(error.Message);}
        }
        public void Fail(string reason)
        {
            if(State==PurchasePhase.Complete||State==PurchasePhase.Failed)return;
            started=true;State=PurchasePhase.Failed;Status="Compra con puntos detenida: "+reason;
            try{game.Release();}catch(Exception error){Status+=" · No se pudo liberar una entrada: "+error.Message;}
        }
        public void Tick(double now,int? baitCount,double baitConfirmedAt)
        {
            if(!started||State==PurchasePhase.Complete||State==PurchasePhase.Failed)return;
            try {
                if(double.IsNaN(now)||double.IsInfinity(now)||now<lastTick){Fail("El reloj de la secuencia cambió.");return;}
                lastTick=now;
                if(!game.IsActive){Fail("Roblox perdió el foco.");return;}
                if(now-startedAt>90000){Fail("La secuencia superó 90 segundos.");return;}
                if(waitingPointer)
                {
                    if(((IShopPointerRuntime)shop).TickShopAim(now))
                    { waitingPointer=false;next=now+pointerSettle;Status="Puntero colocado · esperando antes del clic"; }
                    return;
                }
                if(now<next)return;
                RunStep(now);
            }
            catch(Exception error){Fail(error.Message);}
        }
        private void RunStep(double now)
        {
            if(stepIndex>=steps.Count){Fail("La secuencia no pudo finalizar.");return;}
            Step step=steps[stepIndex];if(step.Kind!=ActionKind.Finish)State=step.Phase;Status=step.Label;
            if(step.Kind==ActionKind.VerifyMenu)
            {
                if(visualStarted<0){visualStarted=now;visualEvidence=new ShopVisualEvidence(now);}
                ShopVisualReading reading=((IShopVisualRuntime)shop).ReadShopVisual(now);
                bool fresh=visualEvidence.Read(reading,now);
                lastAction=step.Label+" · "+(reading==null?"Sin imagen":reading.Detail);
                if(fresh&&visualEvidence.Stable&&visualEvidence.Kind==step.Expected){
                    verifiedAt=reading.SampledAt;verifiedKind=step.Expected;
                    if(step.Expected==ShopMenuKind.Done&&Submitted)doneObserved=true;
                    if(step.Expected==ShopMenuKind.Absent&&doneObserved&&Submitted)CompletionObserved=true;
                    visualStarted=-1;stepIndex++;next=now+step.Delay;
                    Status="Menú confirmado · "+step.Expected;return;
                }
                if(now-visualStarted>=settings.ShopPhaseTimeoutSeconds*1000){
                    int attempts;retries.TryGetValue(stepIndex,out attempts);
                    if(fresh&&visualEvidence.Stable&&visualEvidence.Kind==step.RetryMenu&&step.RetryStep>=0&&attempts<settings.ShopRetryLimit){
                        // Only E, Yes and the closing button can repeat. Buy never rewinds.
                        retries[stepIndex]=attempts+1;stepIndex=step.RetryStep;visualStarted=-1;game.Release();
                        retryOpening=steps[stepIndex].Kind==ActionKind.KeyDown;actionStarted=-1;
                        next=now+settings.ShopSettleMilliseconds;Status="Reintentando fase de compra · "+(attempts+1);return;
                    }
                    Fail("No confirmé el menú esperado: "+step.Expected+". Menú actual: "+(reading==null?ShopMenuKind.Unknown:reading.Kind)+".");
                }
                return;
            }
            ShopMenuKind required=ShopMenuKind.Unknown;
            if(step.Kind==ActionKind.Click)required=step.IsBuy||step.ClickKind==ShopClickKind.Quantity?ShopMenuKind.Quantity:
                step.Phase==PurchasePhase.Editing?ShopMenuKind.Confirm:ShopMenuKind.Done;
            if(step.Kind==ActionKind.KeyDown)required=step.Key==0x45?(retryOpening?ShopMenuKind.Absent:ShopMenuKind.Unknown):ShopMenuKind.Quantity;
            if(required!=ShopMenuKind.Unknown&&shop is IShopVisualRuntime&&(verifiedKind!=required||now-verifiedAt>500)){
                // A delayed scheduler tick must not act on the menu confirmed
                // several seconds earlier, even though its step was queued.
                if(actionStarted<0){actionStarted=now;actionEvidence=new ShopVisualEvidence(now);}
                ShopVisualReading current=((IShopVisualRuntime)shop).ReadShopVisual(now);
                bool fresh=actionEvidence.Read(current,now);
                if(!(fresh&&actionEvidence.Stable&&actionEvidence.Kind==required)){
                    Status="Volviendo a confirmar el menú antes de la entrada";
                    if(now-actionStarted>=settings.ShopPhaseTimeoutSeconds*1000)Fail("El menú cambió antes de enviar la entrada.");
                    return;
                }
                verifiedKind=required;verifiedAt=current.SampledAt;actionStarted=-1;
            }
            if(step.Kind==ActionKind.KeyDown)shop.ShopKey(step.Key,true);
            else if(step.Kind==ActionKind.KeyUp)shop.ShopKey(step.Key,false);
            else if(step.Kind==ActionKind.Aim)
            {shop.ShopAim(step.Point);waitingPointer=shop is IShopPointerRuntime;pointerSettle=step.Delay;}
            else if(step.Kind==ActionKind.Click){
                // Mark before calling an input driver: an exception can mean the
                // press was already delivered, so it must never be submitted again.
                if(step.IsBuy){if(Submitted){Fail("La compra ya fue enviada.");return;}Submitted=true;}
                shop.ShopClick(step.Point,step.ClickKind);
            }
            else {game.Release();State=PurchasePhase.Complete;Status="Secuencia enviada; resultado sin verificar";}
            lastAction=step.Label+(step.Kind==ActionKind.Aim||step.Kind==ActionKind.Click?" en "+step.Point.X+", "+step.Point.Y:"");
            if(step.Kind==ActionKind.KeyDown&&step.Key==0x45)retryOpening=false;
            stepIndex++;next=now+step.Delay;
        }
        private void Add(ActionKind kind,PurchasePhase phase,string label,double delay,int key=0,Point point=default(Point),ShopClickKind clickKind=ShopClickKind.Button,bool isBuy=false)
        {steps.Add(new Step{Kind=kind,Phase=phase,Label=label,Delay=delay,Key=key,Point=point,ClickKind=clickKind,IsBuy=isBuy});}
        private void Build(DirectPurchaseTargets p,int open,int settle)
        {
            int menuWait=Math.Max(1500,settle);
            int openIndex=steps.Count;
            Add(ActionKind.KeyDown,PurchasePhase.Opening,"Manteniendo E · sin OCR de menús",open,0x45);
            Add(ActionKind.KeyUp,PurchasePhase.Confirming,"E liberada · esperando el diálogo",menuWait,0x45);
            MenuGate(PurchasePhase.Confirming,"Comprobando el diálogo Sí / No",ShopMenuKind.Confirm,ShopMenuKind.Absent,openIndex);
            int yesIndex=steps.Count;
            Add(ActionKind.Aim,PurchasePhase.Confirming,"Apuntando a Sí",settle,point:p.Yes);
            MenuGate(PurchasePhase.Confirming,"Comprobando Sí antes del clic",ShopMenuKind.Confirm);
            Add(ActionKind.Click,PurchasePhase.Editing,"Sí enviado una vez · esperando cantidad",menuWait,point:p.Yes);
            MenuGate(PurchasePhase.Editing,"Comprobando que Sí abrió el menú de cantidad",ShopMenuKind.Quantity,ShopMenuKind.Confirm,yesIndex);
            Add(ActionKind.Aim,PurchasePhase.Selecting,"Apuntando al número central",settle,point:p.Quantity);
            MenuGate(PurchasePhase.Selecting,"Comprobando el menú antes del doble clic");
            Add(ActionKind.Click,PurchasePhase.Selecting,"Cantidad · primer clic",250,point:p.Quantity,clickKind:ShopClickKind.Quantity);
            Add(ActionKind.Click,PurchasePhase.Selecting,"Cantidad · segundo clic",Math.Max(300,settle),point:p.Quantity,clickKind:ShopClickKind.Quantity);
            MenuGate(PurchasePhase.Selecting,"Comprobando el menú antes de escribir");
            Add(ActionKind.KeyDown,PurchasePhase.Selecting,"Seleccionando la cantidad · Ctrl",0,0x11);
            Add(ActionKind.KeyDown,PurchasePhase.Selecting,"Seleccionando la cantidad · A",100,0x41);
            Add(ActionKind.KeyUp,PurchasePhase.Selecting,"Soltando A",0,0x41);
            Add(ActionKind.KeyUp,PurchasePhase.Clearing,"Soltando Ctrl",0,0x11);
            Add(ActionKind.KeyDown,PurchasePhase.Clearing,"Borrando la cantidad anterior",100,0x08);
            Add(ActionKind.KeyUp,PurchasePhase.Typing,"Soltando Retroceso",100,0x08);
            string digits=Quantity.ToString(CultureInfo.InvariantCulture);
            foreach(char digit in digits){
                Add(ActionKind.KeyDown,PurchasePhase.Typing,"Escribiendo cantidad · "+digit,100,0x30+digit-'0');
                Add(ActionKind.KeyUp,PurchasePhase.Typing,"Soltando dígito · "+digit,100,0x30+digit-'0');
            }
            Add(ActionKind.Aim,PurchasePhase.Verifying,"Apuntando a Comprar · sin verificar el número",settle,point:p.Buy);
            MenuGate(PurchasePhase.Verifying,"Comprobando el menú antes de Comprar");
            Add(ActionKind.Click,PurchasePhase.Finishing,"Comprar enviado una vez · esperando cierre",menuWait,point:p.Buy,isBuy:true);
            MenuGate(PurchasePhase.Closing,"Comprobando el diálogo de cierre",ShopMenuKind.Done);
            int closeIndex=steps.Count;
            Add(ActionKind.Aim,PurchasePhase.Closing,"Apuntando al botón «…»",settle,point:p.Close);
            MenuGate(PurchasePhase.Closing,"Comprobando los tres puntos antes de cerrar",ShopMenuKind.Done);
            Add(ActionKind.Click,PurchasePhase.Closing,"Cierre «…» enviado una vez",menuWait,point:p.Close);
            MenuGate(PurchasePhase.Closing,"Comprobando que el diálogo se cerró",ShopMenuKind.Absent,ShopMenuKind.Done,closeIndex);
            Add(ActionKind.Finish,PurchasePhase.Complete,"Secuencia enviada; resultado sin verificar",0);
        }
        private void MenuGate(PurchasePhase phase,string label,ShopMenuKind expected=ShopMenuKind.Quantity,ShopMenuKind retryMenu=ShopMenuKind.Unknown,int retryStep=-1)
        {
            if(!(shop is IShopVisualRuntime))return;
            steps.Add(new Step{Kind=ActionKind.VerifyMenu,Phase=phase,Label=label,Expected=expected,RetryMenu=retryMenu,RetryStep=retryStep});
        }
    }

    // Independent cleanup after a failed order. It cannot open the shop, edit a
    // quantity or submit. Complete means the three marked buttons are stably
    // absent, not that a purchase succeeded. The engine owns returning to water.
    public sealed class ShopRecoveryController : IPurchaseFlow
    {
        private readonly Settings settings;
        private readonly IGameRuntime game;
        private readonly IShopRuntime shop;
        private DirectPurchaseTargets targets;
        private ShopVisualEvidence evidence;
        private ShopMenuKind targetMenu;
        private Point target;
        private double startedAt,lastTick,phaseAt,next;
        private bool started,aiming,waitingAfterClick;
        private int attempts;
        public PurchasePhase State {get;private set;}
        public string Status {get;private set;}
        public bool Submitted {get{return false;}}
        public int Quantity {get{return 0;}}
        public string Diagnostic {get{return "Recuperación de compra · menú: "+targetMenu+" · intentos: "+attempts+" · "+Status;}}
        public ShopRecoveryController(Settings settings,IGameRuntime game,IShopRuntime shop)
        {
            if(settings==null||game==null||shop==null)throw new ArgumentNullException("runtime");
            this.settings=settings;this.game=game;this.shop=shop;
            State=PurchasePhase.Closing;Status="Recuperación preparada";
        }
        public void Start(double now)
        {
            if(started)return;started=true;startedAt=lastTick=phaseAt=now;
            try{
                if(double.IsNaN(now)||double.IsInfinity(now)||now<0)throw new ArgumentException("Tiempo de inicio no válido.");
                if(!(shop is IShopVisualRuntime))throw new InvalidOperationException("La recuperación necesita ver los tres botones.");
                if(!game.IsActive)throw new InvalidOperationException("Roblox perdió el foco.");
                if(settings.ShopRetryLimit<0||settings.ShopRetryLimit>5||settings.ShopPhaseTimeoutSeconds<3||settings.ShopPhaseTimeoutSeconds>60)
                    throw new ArgumentException("Configuración de reintentos no válida.");
                targets=DirectPurchaseTargets.ForSettings(settings);game.Release();evidence=new ShopVisualEvidence(now);
                Status="Identificando el menú para volver a pescar";
            }catch(Exception error){Fail(error.Message);}
        }
        public void Fail(string reason)
        {
            if(State==PurchasePhase.Complete||State==PurchasePhase.Failed)return;
            started=true;State=PurchasePhase.Failed;Status="Recuperación detenida: "+reason;
            try{game.Release();}catch(Exception error){Status+=" · "+error.Message;}
        }
        public void Tick(double now,int? baitCount,double baitConfirmedAt)
        {
            if(!started||State==PurchasePhase.Complete||State==PurchasePhase.Failed)return;
            try{
                if(double.IsNaN(now)||double.IsInfinity(now)||now<lastTick){Fail("El reloj de la secuencia cambió.");return;}lastTick=now;
                if(!game.IsActive){Fail("Roblox perdió el foco.");return;}
                double timeout=settings.ShopPhaseTimeoutSeconds*1000;
                if(now-startedAt>Math.Min(90000,(settings.ShopRetryLimit+2)*timeout+10000)){Fail("La recuperación superó su tiempo límite.");return;}
                if(aiming){
                    if(now-phaseAt>timeout){Fail("El puntero no llegó al botón de recuperación.");return;}
                    if(((IShopPointerRuntime)shop).TickShopAim(now)){aiming=false;next=now+settings.ShopSettleMilliseconds;evidence=new ShopVisualEvidence(next);}
                    return;
                }
                if(now<next)return;
                ShopVisualReading reading=((IShopVisualRuntime)shop).ReadShopVisual(now);
                bool fresh=evidence.Read(reading,now);
                Status="Identificando el menú · "+(reading==null?ShopMenuKind.Unknown:reading.Kind);
                if(fresh&&evidence.Stable&&evidence.Kind==ShopMenuKind.Absent){
                    game.Release();State=PurchasePhase.Complete;Status="Diálogo cerrado · listo para volver al agua";return;
                }
                if(waitingAfterClick){
                    if(now-phaseAt<timeout)return;
                    if(attempts>=settings.ShopRetryLimit+1){Fail("El diálogo no se cerró tras los reintentos.");return;}
                    waitingAfterClick=false;targetMenu=ShopMenuKind.Unknown;phaseAt=now;
                    evidence=new ShopVisualEvidence(now);return;
                }
                if(fresh&&evidence.Stable&&(evidence.Kind==ShopMenuKind.Confirm||evidence.Kind==ShopMenuKind.Quantity||evidence.Kind==ShopMenuKind.Done)){
                    ShopMenuKind menu=evidence.Kind;
                    if(targetMenu!=menu){
                        targetMenu=menu;target=menu==ShopMenuKind.Done?targets.Close:targets.Right;
                        game.Release();shop.ShopAim(target);aiming=shop is IShopPointerRuntime;
                        next=now+settings.ShopSettleMilliseconds;phaseAt=now;evidence=new ShopVisualEvidence(next);
                        Status=menu==ShopMenuKind.Done?"Apuntando a cerrar los tres puntos":"Apuntando a No / Cancelar";return;
                    }
                    // This is a second evidence window after pointer arrival and
                    // settling. A menu transition cannot reuse the first reading.
                    attempts++;shop.ShopClick(target,ShopClickKind.Button);waitingAfterClick=true;phaseAt=now;
                    next=now+Math.Max(1500,settings.ShopSettleMilliseconds);evidence=new ShopVisualEvidence(next);
                    Status="Cierre de recuperación enviado · esperando";return;
                }
                if(fresh&&targetMenu!=ShopMenuKind.Unknown&&evidence.Kind!=targetMenu){targetMenu=ShopMenuKind.Unknown;evidence=new ShopVisualEvidence(now);}
                if(now-phaseAt>=timeout)Fail("No se pudo identificar un menú para cerrar.");
            }catch(Exception error){Fail(error.Message);}
        }
    }
}
