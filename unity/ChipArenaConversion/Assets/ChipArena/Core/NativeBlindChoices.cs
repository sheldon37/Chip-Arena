using System;
namespace ChipArena.UnityConversion
{
    // Blind Pick with N choices, the presentation side (owner 2026-11-18: "make a new blind pick where we get a customizable
    // amount of choices (Default 3) of weapon/class to pick (we only pick 1). This will be similar to a chip screen."). The
    // earlier mode, every allowed weapon in a grid, is now named Blind Pick All to players (its ids and wire stay `blindPick`).
    //
    // The rules are Astra's (PR #411, Core NativeOpeningDraft): the room's blindChoices (0 = Blind Pick All, normalised by
    // OfferCount), the host's private deal (WeaponOffers, CardsFor), the pick checked against it, a random offer locked at 0 s
    // and the dealt protocol's version gate. This file holds only the screen's side: the hook's view, the names and labels,
    // the layout and the deal's motion. No rule, timer or message lives here.
    public static class NativeBlindChoices
    {
        /// <summary>The fewest offers the screen draws as a pick (NativeOpeningDraft.OfferCount deals at least two) and the most
        /// it lays out (a seat's whole pool fits).</summary>
        public const int Min=2,Max=32;
        /// <summary>The mode's name as players read it (room options, the lobby line, the draft headers); null without a blind pick.</summary>
        public static string Name(bool blind,int choices)=>!blind?null:choices>=Min?"Blind Pick · "+choices+" choices":"Blind Pick All";
        /// <summary>The room options' weapon pick select: off, Blind Pick All, Blind Pick (N choices).</summary>
        public static readonly string[] ModeLabels={"Off (lobby loadouts)","Blind Pick All","Blind Pick (choices)"};
        /// <summary>The choice count select's labels, 2 up to `pool` (and Max), the rules' default (NativeOpeningDraft.DefaultBlindChoices) marked.</summary>
        public static string[] CountLabels(int pool)
        {
            int top=Math.Max(Min,Math.Min(Max,pool));var labels=new string[top-Min+1];
            for(int n=Min;n<=top;n++)labels[n-Min]=n+" choices"+(n==NativeOpeningDraft.DefaultBlindChoices?" (default)":"");return labels;
        }

        // ---- the screen's layout -------------------------------------------------------------------------------------------
        // The dealt cards sit in rows as the chip tray's do (a short row centred), each as large as the area allows: the weapon's
        // hero card (stats, role and description) while it is at least HeroMin wide, else its compact grid card.
        public const float HeroAspect=1.48f,CardAspect=1.24f,HeroMin=200,HeroCap=360,CardCap=176,Gap=22;
        public struct Layout{public int Rows,Cols;public float Width,Height;public bool Hero;}
        /// <summary>The largest cards for `n` offers in an area `width` x `height` (virtual units), over one to six rows.</summary>
        public static Layout Fit(int n,float width,float height)
        {
            var best=new Layout{Rows=1,Cols=Math.Max(1,n)};if(n<=0||width<=0||height<=0)return best;
            for(int pass=0;pass<2;pass++)
            {
                bool hero=pass==0;float aspect=hero?HeroAspect:CardAspect,cap=hero?HeroCap:CardCap;bool found=false;
                for(int rows=1;rows<=6;rows++)
                {
                    int cols=(n+rows-1)/rows;float w=Math.Min(cap,(width-(cols-1)*Gap)/cols),h=w*aspect;
                    if(rows*h+(rows-1)*Gap>height){h=(height-(rows-1)*Gap)/rows;w=h/aspect;}
                    if(w<=0)continue;
                    if(!found||w>best.Width+.5f){best=new Layout{Rows=rows,Cols=cols,Width=w,Height=h,Hero=hero};found=true;}
                }
                if(found&&(!hero||best.Width>=HeroMin))return best;
            }
            return best;
        }
        /// <summary>Card `i`'s top-left in the area at (left, top): rows of the layout's columns, a short last row centred, the
        /// block centred in the area.</summary>
        public static void Place(in Layout l,int i,int n,float left,float top,float width,float height,out float x,out float y)
        {
            int row=i/Math.Max(1,l.Cols),col=i%Math.Max(1,l.Cols),inRow=Math.Min(l.Cols,n-row*l.Cols);
            float blockH=l.Rows*l.Height+(l.Rows-1)*Gap,rowW=inRow*l.Width+(inRow-1)*Gap;
            x=left+(width-rowW)*.5f+col*(l.Width+Gap);y=top+Math.Max(0,(height-blockH)*.45f)+row*(l.Height+Gap);
        }

        // ---- the deal ------------------------------------------------------------------------------------------------------
        // Dealt like the chip tray boots its cards, one after another: each card leaves the deck (below the screen's centre)
        // face down, turning as it flies to its place, then flips face up with a flash. Reduced effects deal them in place.
        public const float DealStart=.12f,DealStagger=.16f,DealFly=.42f,DealFlip=.3f,DealLand=.35f;
        public struct Deal{public float Alpha,Dx,Dy,Scale,Degrees,Flip,Land;public bool Face;}
        /// <summary>When the last of `n` cards has turned face up (seconds from the step's start).</summary>
        public static float DealEnd(int n)=>DealStart+Math.Max(0,n-1)*Stagger(n)+DealFly+DealFlip;
        /// <summary>The gap between cards: DealStagger, shorter for a deal of more than eight so the whole deal stays under ~2 s.</summary>
        public static float Stagger(int n)=>n>8?1.12f/(n-1):DealStagger;
        /// <summary>Card `i` of `n` at `t` seconds into the step; (deckX, deckY) is the deck's offset from the card's resting centre.</summary>
        public static Deal At(float t,int i,int n,float deckX,float deckY,bool calm)
        {
            var d=new Deal{Alpha=1,Scale=1,Flip=1,Face=true};if(calm)return d;
            float u=t-(DealStart+i*Stagger(n));if(u<0){d.Alpha=0;d.Face=false;d.Dx=deckX;d.Dy=deckY;d.Scale=.6f;return d;}
            float k=NativeUiMotion.Clamp01(u/DealFly),e=NativeUiMotion.Bezier(.16f,.8f,.22f,1,k),turn=(i%2==0?-1:1)*(9+2.5f*(i%4));
            d.Dx=deckX*(1-e);d.Dy=deckY*(1-e);d.Scale=.6f+.4f*e;d.Degrees=turn*(1-e);d.Alpha=NativeUiMotion.Clamp01(k*4);
            float f=NativeUiMotion.Clamp01((u-DealFly)/DealFlip);d.Flip=Math.Max(.02f,Math.Abs((float)Math.Cos(f*Math.PI)));d.Face=f>=.5f;
            d.Land=f>=1?1-NativeUiMotion.Clamp01((u-DealFly-DealFlip)/DealLand):0;return d;
        }
    }

    /// <summary>The presentation hook: the local seat's N-choice pick as the screen draws it. ConversionGame.BlindChoiceView builds
    /// it from the host's private opening view (Weapon step: pickCards the seat's own offers, chip its current selection, id the
    /// locked-in count of team pilots, remainingSeconds); the screen reads only this.</summary>
    public sealed class NativeBlindChoiceView
    {
        public string Session;public string[] Offers=Array.Empty<string>();public string Selected;public int Ready,Total;public float Remaining;
        /// <summary>The view, or null for a step without offers (Blind Pick All sends none) or with too few or too many to pick from.
        /// `current` counts as the selection only when it was offered; offers repeat nothing.</summary>
        public static NativeBlindChoiceView From(string session,int step,string[] offers,string current,int ready,int total,float remaining)
        {
            if(step!=(int)OpeningStep.Weapon||offers==null||offers.Length<NativeBlindChoices.Min||offers.Length>NativeBlindChoices.Max)return null;
            for(int i=0;i<offers.Length;i++){if(string.IsNullOrEmpty(offers[i]))return null;for(int j=0;j<i;j++)if(offers[j]==offers[i])return null;}
            return new NativeBlindChoiceView{Session=session,Offers=offers,Selected=Array.IndexOf(offers,current)>=0?current:null,Ready=Math.Max(0,ready),Total=Math.Max(1,total),Remaining=Math.Max(0,remaining)};
        }
        public bool Offered(string id)=>id!=null&&Array.IndexOf(Offers,id)>=0;
    }
}
