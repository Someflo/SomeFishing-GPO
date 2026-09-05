using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SomeFishingGPO
{
    public enum ShopMenu { Unknown, Confirm, Quantity, Done }
    public sealed class ShopReading
    {
        public ShopMenu Menu;
        public int? Maximum, Quantity;
        public Point Left, Middle;
        public long Sequence;
        public double SampledAt;
        public bool ReadFailed;
        public string Detail = "Esperando menú de compra";
    }
    public static class ShopText
    {
        public static string Clean(string text)
        {
            var result = new StringBuilder();
            foreach(char c in (text ?? "").Normalize(NormalizationForm.FormD))
                if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)result.Append(char.ToUpperInvariant(c));
            return result.ToString().Normalize(NormalizationForm.FormC).Trim();
        }
        public static int? Maximum(string text)
        {
            // Windows sometimes reads the X of this fixed MAX label as Y.
            MatchCollection matches=Regex.Matches(Clean(text), @"\bMA[XY][.:\s]*([0-9]{1,4})(?=\s|$)");
            int value;
            return matches.Count==1 && int.TryParse(matches[0].Groups[1].Value,out value) && value<=9999 ? (int?)value:null;
        }
        public static bool Confirmation(string text)
        {
            string t=Clean(text);
            return t.Contains("PURCHASE") && t.Contains("FISH") && t.Contains("BAITS") && t.Contains("PELI") && !t.Contains("ROBUX");
        }
        public static bool Yes(string text) { string t=Clean(text);return t=="SI"||t=="YES"; }
        public static bool Buy(string text) { string t=Clean(text);return t=="COMPRAR"||t=="BUY"; }
        public static bool QuantityConfirmation(string text) { return Buy(text)||Yes(text); }
        public static bool Dots(string text) { return Regex.IsMatch(Clean(text),@"\A(?:\.[\s]*){3}\z") || Clean(text)=="…"; }
    }
    public enum ShopClickKind { Button, Quantity }
    public interface IShopRuntime
    {
        ShopReading ReadShop(double now);
        void ShopAim(Point point);
        void ShopClick(Point point,ShopClickKind kind);
        void ShopKey(int key, bool held);
    }
    public enum PurchasePhase { Opening, Confirming, Editing, Selecting, Clearing, Typing, Verifying, Finishing, Closing, Complete, Failed }
    public sealed class PurchaseController
    {
        private readonly Settings settings;
        private readonly IShopRuntime shop;
        private readonly IGameRuntime game;
        private double deadline, next, after, started, boughtAt;
        private long lastSequence=-1;
        private ShopReading candidate;
        private int stable, digitIndex, key, numberClicks;
        private bool keyHeld;
        private Point aimedPoint, quantityPoint;
        private double aimedAt;
        private string aimedAction;
        public int YesAttempts { get; private set; }
        private string lastClick = "Sin clics";
        private string digits;
        public PurchasePhase State { get; private set; }
        public string Status { get; private set; }
        public int Quantity { get; private set; }
        public bool Submitted { get; private set; }
        public ShopReading LastReading { get; private set; }
        public string Diagnostic { get { return "Paso: "+State+" · cantidad solicitada: "+Quantity+" · Comprar enviado: "+Submitted+" · Sí: "+YesAttempts+" · "+lastClick+" · "+(LastReading==null?"Sin lectura de menú":LastReading.Detail); } }
        public PurchaseController(Settings settings, IGameRuntime game, IShopRuntime shop)
        { this.settings=settings;this.game=game;this.shop=shop; }
        public void Start(double now)
        {
            if(settings.ShopOpenMilliseconds<100||settings.ShopOpenMilliseconds>3000){Fail("Mantener E debe estar entre 100 y 3000 ms");return;}
            if(settings.ShopSettleMilliseconds<200||settings.ShopSettleMilliseconds>3000){Fail("Pausa entre pasos fuera de rango");return;}
            game.Release(); started=now; State=PurchasePhase.Opening; Status="Manteniendo E durante "+settings.ShopOpenMilliseconds+" ms para abrir compra…";
            shop.ShopKey(0x45,true); keyHeld=true; key=0x45; next=now+settings.ShopOpenMilliseconds; deadline=now+settings.ShopOpenMilliseconds+20000; after=now;
        }
        public void Fail(string reason) { game.Release();State=PurchasePhase.Failed;Status="Compra detenida: "+reason; }
        private void Wait(PurchasePhase state,double now,string message)
        { State=state;after=now+settings.ShopSettleMilliseconds;deadline=now+20000;candidate=null;stable=0;aimedAction=null;Status=message; }
        private void Click(string name,Point target,ShopClickKind kind=ShopClickKind.Button)
        {
            if(Math.Abs((long)target.X-aimedPoint.X)>3||Math.Abs((long)target.Y-aimedPoint.Y)>3)throw new InvalidOperationException("El destino del clic cambió después de apuntar.");
            shop.ShopClick(target,kind);lastClick=name+" pulsado en "+target.X+", "+target.Y+" · "+(kind==ShopClickKind.Quantity?"posición en la pulsación":"clic clásico");
        }
        private bool FreshStable(ShopReading reading,double now)
        {
            if(reading==null||reading.ReadFailed||reading.SampledAt<after||reading.SampledAt>now||now-reading.SampledAt>4000||reading.Sequence<=lastSequence)return false;
            lastSequence=reading.Sequence;
            bool same=candidate!=null&&reading.SampledAt-candidate.SampledAt<=4000&&candidate.Menu==reading.Menu&&candidate.Maximum==reading.Maximum&&candidate.Quantity==reading.Quantity
                && Math.Abs(candidate.Left.X-reading.Left.X)<12&&Math.Abs(candidate.Left.Y-reading.Left.Y)<12
                && Math.Abs(candidate.Middle.X-reading.Middle.X)<12&&Math.Abs(candidate.Middle.Y-reading.Middle.Y)<12;
            stable=same?stable+1:1;candidate=reading;
            bool clickable=(State==PurchasePhase.Confirming&&reading.Menu==ShopMenu.Confirm)
                ||(State==PurchasePhase.Editing&&reading.Menu==ShopMenu.Quantity&&reading.Maximum>0)
                ||(State==PurchasePhase.Verifying&&reading.Menu==ShopMenu.Quantity&&reading.Quantity==Quantity&&reading.Maximum>=Quantity)
                ||(State==PurchasePhase.Finishing&&reading.Menu==ShopMenu.Done);
            if(clickable)
            {
                string action=reading.Menu==ShopMenu.Confirm?"Sí":State==PurchasePhase.Editing?"cantidad":State==PurchasePhase.Verifying?"Comprar":"…";
                Point target=(reading.Menu==ShopMenu.Quantity&&State==PurchasePhase.Editing)||State==PurchasePhase.Finishing?reading.Middle:reading.Left;
                if(State==PurchasePhase.Editing&&reading.Menu==ShopMenu.Quantity&&(long)reading.Middle.X-reading.Left.X<12)
                { Fail("El número central se confunde con el botón de compra. Revisa la zona del menú");return false; }
                if(!same || action!=aimedAction || Math.Abs(target.X-aimedPoint.X)>3 || Math.Abs(target.Y-aimedPoint.Y)>3)
                {
                    shop.ShopAim(target);aimedPoint=target;aimedAt=now;aimedAction=action;
                    Status="Apuntando a "+action+" · esperando confirmación visual";
                    return false;
                }
                if(now-aimedAt<settings.ShopSettleMilliseconds)return false;
            }
            return stable>=2;
        }
        private void Press(int virtualKey,double now)
        { shop.ShopKey(virtualKey,true);key=virtualKey;keyHeld=true;next=now+100; }
        public void Tick(double now,int? baitCount,double baitConfirmedAt)
        {
            if(State==PurchasePhase.Complete||State==PurchasePhase.Failed)return;
            if(!game.IsActive){Fail("Roblox perdió el foco");return;}
            if(now-started>90000||now>deadline){Fail(TimeoutReason());return;}
            if(State==PurchasePhase.Opening)
            {
                if(now<next)return;shop.ShopKey(key,false);keyHeld=false;Wait(PurchasePhase.Confirming,now,"Esperando Sí y la oferta de cebo en Peli…");return;
            }
            if(State==PurchasePhase.Selecting)
            {
                if(now<next)return;
                if(numberClicks==1){Click("Cantidad · segundo clic",quantityPoint,ShopClickKind.Quantity);numberClicks=2;next=now+Math.Max(300,settings.ShopSettleMilliseconds);return;}
                if(!keyHeld){shop.ShopKey(0x11,true);Press(0x41,now);return;}
                shop.ShopKey(0x41,false);shop.ShopKey(0x11,false);keyHeld=false;Press(0x08,now);State=PurchasePhase.Clearing;return;
            }
            if(State==PurchasePhase.Clearing)
            {
                if(now<next)return;shop.ShopKey(0x08,false);keyHeld=false;State=PurchasePhase.Typing;digitIndex=0;next=now+100;return;
            }
            if(State==PurchasePhase.Typing)
            {
                if(now<next)return;
                if(keyHeld){shop.ShopKey(key,false);keyHeld=false;digitIndex++;next=now+100;return;}
                if(digitIndex<digits.Length){Press(0x30+digits[digitIndex]-'0',now);return;}
                Wait(PurchasePhase.Verifying,now,"Verificando la cantidad escrita antes de Comprar…");return;
            }
            ShopReading reading=shop.ReadShop(now);
            LastReading=reading;
            if(!FreshStable(reading,now))return;
            if(State==PurchasePhase.Confirming&&reading.Menu==ShopMenu.Quantity&&reading.Maximum>0&&reading.Quantity>0&&reading.Quantity<=reading.Maximum)
            { Wait(PurchasePhase.Editing,now,"Menú de cantidad ya abierto · continuando sin repetir Sí…");return; }
            if(State==PurchasePhase.Confirming&&reading.Menu==ShopMenu.Confirm)
            { Click("Sí",reading.Left);YesAttempts++;Wait(PurchasePhase.Editing,now,"Sí pulsado una vez · esperando cantidad y MAX…");return; }
            if(State==PurchasePhase.Editing&&reading.Menu==ShopMenu.Quantity&&reading.Maximum.HasValue)
            {
                Quantity=settings.BuyMaximum&&!settings.PurchaseByTimer?reading.Maximum.Value:Math.Min(settings.BuyQuantity,reading.Maximum.Value);
                if(Quantity<1){Fail("el máximo disponible es 0");return;}
                digits=Quantity.ToString(CultureInfo.InvariantCulture);quantityPoint=reading.Middle;Click("Cantidad · primer clic",quantityPoint,ShopClickKind.Quantity);numberClicks=1;
                State=PurchasePhase.Selecting;deadline=now+20000;next=now+250;Status="Doble clic en cantidad · luego escribir "+digits+"…";return;
            }
            if(State==PurchasePhase.Verifying&&reading.Menu==ShopMenu.Quantity&&reading.Quantity==Quantity&&reading.Maximum>=Quantity)
            {
                Click("Comprar",reading.Left);Submitted=true;boughtAt=now;
                Wait(PurchasePhase.Finishing,now,"Compra enviada una vez · esperando cebo y «…»");return;
            }
            if(State==PurchasePhase.Finishing&&reading.Menu==ShopMenu.Done&&reading.SampledAt>boughtAt)
            { Click("…",reading.Middle);Wait(PurchasePhase.Closing,now,settings.PurchaseByTimer?"Cerrando «…» · sin lectura del contador":"Cerrando «…» y comprobando reposición…");return; }
            if(State==PurchasePhase.Verifying&&reading.Menu==ShopMenu.Done)
            { Fail("El diálogo final apareció antes de confirmar la cantidad. El juego pudo recibir el clic en otro botón; no se repetirá la compra");return; }
            if(State==PurchasePhase.Closing&&reading.Menu==ShopMenu.Unknown&&(settings.PurchaseByTimer||(baitCount>0&&baitConfirmedAt>after)))
            { game.Release();State=PurchasePhase.Complete;Status=settings.PurchaseByTimer?"Compra enviada · diálogo cerrado; inventario sin verificar":"Cebo repuesto · reanudando pesca"; }
        }
        private string TimeoutReason()
        {
            string reason;
            switch(State)
            {
                case PurchasePhase.Confirming:reason="Windows aceptó E durante "+settings.ShopOpenMilliseconds+" ms, pero no se confirmó la oferta con Sí/No. Revisa la distancia al barril, Mantener E y la zona";break;
                case PurchasePhase.Editing:reason=YesAttempts==0?"el menú abierto manualmente dejó de reconocerse antes de editar su cantidad":"se envió Sí, pero no se confirmó el menú de cantidad y MAX";break;
                case PurchasePhase.Verifying:reason="la cantidad escrita no se confirmó como "+Quantity+" dentro del MAX. Comprar no se pulsó";break;
                case PurchasePhase.Finishing:reason="Comprar se envió una vez, pero no se reconoció el botón final «…». Revisa el diálogo y el saldo";break;
                case PurchasePhase.Closing:reason=settings.PurchaseByTimer?"se pulsó «…», pero no se confirmó el cierre del diálogo; no se repetirá Comprar":"se pulsó «…», pero no se confirmó cebo disponible después. Revisa el contador; no se repetirá Comprar";break;
                default:reason="se agotó el tiempo durante "+State;break;
            }
            return reason+". "+(LastReading==null?"Sin lectura del menú.":LastReading.Detail);
        }
    }
}
