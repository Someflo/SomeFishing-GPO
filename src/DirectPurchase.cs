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

    // The user explicitly marks the three buttons. The right button is validated
    // but never clicked: it belongs to No/Cancelar, not to the purchase sequence.
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
        }
        private readonly Settings settings;
        private readonly IGameRuntime game;
        private readonly IShopRuntime shop;
        private readonly List<Step> steps=new List<Step>();
        private int stepIndex;
        private bool started;
        private double startedAt,next,lastTick;
        private bool waitingPointer;
        private double pointerSettle, visualStarted=-1, visualFirst=-1;
        private long visualSequence=-1;
        private int visualFrames;
        private string lastAction="Sin entradas";
        public PurchasePhase State { get; private set; }
        public string Status { get; private set; }
        public bool Submitted { get; private set; }
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
                if(visualStarted<0){visualStarted=now;visualFirst=-1;visualFrames=0;visualSequence=-1;}
                if(now-visualStarted>=8000){Fail("No confirmé el menú de cantidad. La secuencia se detuvo antes de escribir o seguir comprando. Revisa el clic en Sí y los tres puntos.");return;}
                ShopVisualReading reading=((IShopVisualRuntime)shop).ReadShopVisual(now);
                bool fresh=reading!=null&&reading.Sequence>visualSequence&&reading.SampledAt>=visualStarted&&reading.SampledAt<=now&&now-reading.SampledAt<=500;
                if(fresh)
                {
                    visualSequence=reading.Sequence;
                    if(reading.QuantityMenu)
                    { if(visualFrames==0)visualFirst=reading.SampledAt;visualFrames++; }
                    else{visualFrames=0;visualFirst=-1;}
                }
                else if(reading==null||reading.SampledAt<visualStarted||reading.SampledAt>now||now-reading.SampledAt>500)
                { visualFrames=0;visualFirst=-1; }
                lastAction=step.Label+" · "+(reading==null?"Sin imagen":reading.Detail);
                if(!fresh||visualFrames<2||reading.SampledAt-visualFirst<100)return;
                visualStarted=-1;stepIndex++;next=now+step.Delay;
                Status="Menú de cantidad visible · comprobación por colores";return;
            }
            if(step.Kind==ActionKind.KeyDown)shop.ShopKey(step.Key,true);
            else if(step.Kind==ActionKind.KeyUp)shop.ShopKey(step.Key,false);
            else if(step.Kind==ActionKind.Aim)
            {shop.ShopAim(step.Point);waitingPointer=shop is IShopPointerRuntime;pointerSettle=step.Delay;}
            else if(step.Kind==ActionKind.Click){shop.ShopClick(step.Point,step.ClickKind);if(step.IsBuy)Submitted=true;}
            else {game.Release();State=PurchasePhase.Complete;Status="Secuencia enviada; resultado sin verificar";}
            lastAction=step.Label+(step.Kind==ActionKind.Aim||step.Kind==ActionKind.Click?" en "+step.Point.X+", "+step.Point.Y:"");
            stepIndex++;next=now+step.Delay;
        }
        private void Add(ActionKind kind,PurchasePhase phase,string label,double delay,int key=0,Point point=default(Point),ShopClickKind clickKind=ShopClickKind.Button,bool isBuy=false)
        {steps.Add(new Step{Kind=kind,Phase=phase,Label=label,Delay=delay,Key=key,Point=point,ClickKind=clickKind,IsBuy=isBuy});}
        private void Build(DirectPurchaseTargets p,int open,int settle)
        {
            int menuWait=Math.Max(1500,settle);
            Add(ActionKind.KeyDown,PurchasePhase.Opening,"Manteniendo E · sin OCR de menús",open,0x45);
            Add(ActionKind.KeyUp,PurchasePhase.Confirming,"E liberada · esperando el diálogo",menuWait,0x45);
            Add(ActionKind.Aim,PurchasePhase.Confirming,"Apuntando a Sí",settle,point:p.Yes);
            Add(ActionKind.Click,PurchasePhase.Editing,"Sí enviado una vez · esperando cantidad",menuWait,point:p.Yes);
            MenuGate(PurchasePhase.Editing,"Comprobando que Sí abrió el menú de cantidad");
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
            Add(ActionKind.Aim,PurchasePhase.Closing,"Apuntando al botón «…»",settle,point:p.Close);
            Add(ActionKind.Click,PurchasePhase.Closing,"Cierre «…» enviado una vez",menuWait,point:p.Close);
            Add(ActionKind.Finish,PurchasePhase.Complete,"Secuencia enviada; resultado sin verificar",0);
        }
        private void MenuGate(PurchasePhase phase,string label)
        { if(shop is IShopVisualRuntime)Add(ActionKind.VerifyMenu,phase,label,0); }
    }
}
