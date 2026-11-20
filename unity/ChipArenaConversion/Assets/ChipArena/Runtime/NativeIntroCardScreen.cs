using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One frame of a v2 name card for a bespoke effect (docs/INTROS-V2.md "The name card"): the live display name, its letters
    /// (cached strings), each letter's left edge and width, the font size and the name's box, all in the virtual canvas.
    /// </summary>
    public readonly struct NativeIntroCardFrame
    {
        public readonly string Name;public readonly string[] Letters;public readonly float[] X,Widths;public readonly float Size;public readonly Rect Box;
        public readonly NativeIntroCardState Card;public readonly float W,H,Alpha;public readonly bool Reduced;
        public NativeIntroCardFrame(string name,string[] letters,float[] x,float[] widths,float size,Rect box,NativeIntroCardState card,float w,float h,float alpha,bool reduced)
        {Name=name;Letters=letters;X=x;Widths=widths;Size=size;Box=box;Card=card;W=w;H=h;Alpha=alpha;Reduced=reduced;}
    }
    /// <summary>A bespoke name effect: drawn before the plain name (return true to replace it) and again after it.</summary>
    public interface INativeIntroCardEffect{bool Draw(in NativeIntroCardFrame frame,bool before);}
    /// <summary>The bespoke name effects by id (NativeIntroCardState.Effect). One line per effect, on its slot.</summary>
    public static class NativeIntroCardEffects
    {
        private static readonly Dictionary<string,Func<INativeIntroCardEffect>> makers=new Dictionary<string,Func<INativeIntroCardEffect>>(StringComparer.Ordinal)
        {
            // ---- the dev intro's (its author): {"architect",()=>new NativeArchitectCard()}, ----
            //
        };
        private static readonly Dictionary<string,INativeIntroCardEffect> made=new Dictionary<string,INativeIntroCardEffect>(StringComparer.Ordinal);
        public static INativeIntroCardEffect Find(string id){if(id==null)return null;if(made.TryGetValue(id,out var e))return e;e=makers.TryGetValue(id,out var make)?make():null;made[id]=e;return e;}
    }

    // The v2 name card (Core NativeIntroCardState, filled by the intro): the lens under it (the letterbox, the black, the focus
    // vignette) and the live display name in one of the reveal styles, with the kicker (the weapon) and the line (the intro text).
    // Drawn by IntroCard for a registered v2 intro, in place of the plain card. Allocation-free per frame once a name's letters are cached.
    public sealed partial class ConversionGame
    {
        private string cardNameKey;private string[] cardLetters=Array.Empty<string>();private float[] cardX=Array.Empty<float>(),cardW=Array.Empty<float>();private float cardSize=-1;
        private string cardKickerSpec,cardKicker,cardLineRaw,cardLine;
        private static readonly float[] CardVignetteAt={0,.55f,1},CardVignetteAlpha={0,.25f,1};
        /// <summary>Draws the v2 card for `style` when it is a registered v2 intro; false otherwise (the caller draws its own).</summary>
        private bool IntroCardV2(NativePilot pilot,string style,float rawAge,int index,int total,bool team)
        {
            if(!NativeIntroCatalog.Registered(style)||!(NativeEntranceDirector.Script(style) is NativeEntranceScript script)||!(script is INativeIntroV2 v2))return false;
            float duration=(float)NativeIntroRules.OverlayDuration(style),age=Mathf.Min(rawAge,duration),W=U.W,H=U.H;float t=script.Clock(age);bool reduced=prefs.reducedEffects;
            // The lens: the black, the cinema bars, the focus vignette.
            v2.Lens(t,out var lens);
            if(lens.Aperture>.01f){float a=Mathf.Clamp01(lens.Aperture)*.6f;U.RadialStops(new Rect(-W*.18f,-H*.25f,W*1.36f,H*1.5f),new Color(0,0,0,a),CardVignetteAt,CardVignetteAlpha);}
            float bar=Mathf.Max(0,(H-W/2.39f)*.5f)*Mathf.Clamp01(lens.Letterbox);if(bar>.5f){U.Fill(new Rect(0,0,W,bar),Color.black);U.Fill(new Rect(0,H-bar,W,bar),Color.black);}
            if(lens.Black>.002f)U.Fill(new Rect(0,0,W,H),new Color(0,0,0,Mathf.Clamp01(lens.Black)));
            // The topline (quiet, inside the top bar): which entrance of how many.
            float top=M.EaseOut(M.Progress(age,0,.45f))*(1-M.Progress(age,duration-.35f,.34f));
            U.Text(new Rect(W*.06f,Mathf.Max(18,bar*.5f-8),W*.88f,15),IntroTopline(index,total),11,U.A(U.Hex(0xc9ccd8),.7f*top),TextAnchor.UpperLeft,mono:true,tracking:.22f);
            var card=default(NativeIntroCardState);v2.Card(t,ref card);if(!card.Shown)return true;
            var seat=pilot.Seat;string name=v2.Context.Name??NativeCosmeticRules.Name(seat.name);
            float exit=Mathf.Clamp01(card.Exit),alpha=1-M.Bezier(.4f,0,1,1,exit);if(alpha<=.002f)return true;
            float size=Mathf.Clamp((card.Size>0?card.Size:.075f)*H,26,120);CardLetters(name,size);
            float width=0;for(int i=0;i<cardW.Length;i++)width=Mathf.Max(width,cardX[i]+cardW[i]);float fit=W*.88f;if(width>fit){size*=fit/width;CardLetters(name,size);width=0;for(int i=0;i<cardW.Length;i++)width=Mathf.Max(width,cardX[i]+cardW[i]);}
            float cx=(card.X>0?card.X:.5f)*W,cy=(card.Y>0?card.Y:.8f)*H-6*exit;float left=cx-width*.5f;var box=new Rect(left,cy-size*.6f,width,size*1.2f);
            var ink=Lin(card.Ink,alpha);var glow=Lin(card.Glow,.65f*alpha);
            var frame=new NativeIntroCardFrame(name,cardLetters,cardX,cardW,size,box,card,W,H,alpha,reduced);var effect=NativeIntroCardEffects.Find(card.Effect);
            bool replaced=effect!=null&&effect.Draw(frame,true);
            if(!replaced)CardName(card,box,size,ink,glow,alpha,reduced);
            effect?.Draw(frame,false);
            // The kicker above (the weapon) and the line below (the intro text), each easing in after the name.
            float k=M.Bezier(.16f,.84f,.24f,1,M.Progress(card.Age,.18f,.55f))*alpha;string kicker=card.Kicker??Kicker(seat.spec);
            U.Text(new Rect(0,box.y-24+10*(1-k),W,16),kicker,12,U.A(Lin(card.Glow,1),k),TextAnchor.UpperCenter,bold:true,tracking:.34f,shadow:new Color(0,0,0,.7f*k));
            if(team){float tk=M.Bezier(.16f,.84f,.24f,1,M.Progress(card.Age,.24f,.55f))*alpha;U.Text(new Rect(0,box.y-46+10*(1-tk),W,16),NativeMatch.TeamNames[TeamIndex(seat.team)].ToUpperInvariant()+" TEAM",12,U.A(TeamColor(seat.team),tk),TextAnchor.UpperCenter,bold:true,tracking:.14f);}
            float l=M.Bezier(.16f,.84f,.24f,1,M.Progress(card.Age,.32f,.6f))*alpha;
            U.Text(new Rect(0,box.yMax+6-8*(1-l),W,16),card.Line??IntroLine(seat.introText),12,U.A(U.Hex(0xb9bac4),l),TextAnchor.UpperCenter,tracking:.3f,blur:5*(1-l));
            return true;
        }
        private static Color Lin(System.Numerics.Vector3 c,float a)=>new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(c.X)),Mathf.LinearToGammaSpace(Mathf.Clamp01(c.Y)),Mathf.LinearToGammaSpace(Mathf.Clamp01(c.Z)),a);
        private readonly string[] toplines=new string[64];
        private string IntroTopline(int index,int total){int k=Mathf.Clamp(index,0,7)*8+Mathf.Clamp(total-1,0,7);return toplines[k]??=("MATCH INTRO · "+(index+1).ToString("00")+" / "+total.ToString("00"));}
        private string Kicker(string spec){if(spec==cardKickerSpec&&cardKicker!=null)return cardKicker;cardKickerSpec=spec;cardKicker=(content.weapons.FirstOrDefault(w=>w.id==spec)?.name??spec??"").ToUpperInvariant();return cardKicker;}
        private string IntroLine(string raw){if(ReferenceEquals(raw,cardLineRaw)&&cardLine!=null)return cardLine;cardLineRaw=raw;cardLine=(string.IsNullOrWhiteSpace(raw)?"Ready to make an impact":raw).ToUpperInvariant();return cardLine;}
        /// <summary>The name's letters as cached strings with their left edges and widths at `size` (rebuilt when the name or size changes).</summary>
        private void CardLetters(string name,float size)
        {
            if(name==cardNameKey&&Mathf.Abs(size-cardSize)<.01f)return;cardNameKey=name;cardSize=size;int n=name.Length;
            if(cardLetters.Length!=n){cardLetters=new string[n];cardX=new float[n];cardW=new float[n];}
            float x=0,gap=-.02f*size;for(int i=0;i<n;i++){cardLetters[i]=name[i].ToString();float w=name[i]==' '?.32f*size:U.Measure(cardLetters[i],size,true).x;cardX[i]=x;cardW[i]=w;x+=w+gap;}
        }
        /// <summary>The plain name in the card's style.</summary>
        private void CardName(in NativeIntroCardState card,Rect box,float size,Color ink,Color glow,float alpha,bool reduced)
        {
            float a=card.Age;var shadow=new Color(0,0,0,.75f*alpha);
            switch(card.Style)
            {
                case NativeIntroCardStyle.Slam:
                {
                    // From 1.6x down to 1 in .22 s with a hard settle (a small overshoot), then a light sweep across the letters.
                    float u=M.Progress(a,0,.22f),s=u<1?1.6f-.6f*u*u:1-.03f*Mathf.Exp(-(a-.22f)*14)*Mathf.Cos((a-.22f)*40);float o=Mathf.Clamp01(a/.08f)*alpha;
                    U.Push(box.center,Vector2.zero,s);for(int i=0;i<cardLetters.Length;i++)U.Text(new Rect(box.x+cardX[i],box.y,cardW[i]+4,box.height),cardLetters[i],size,U.A(ink,o),TextAnchor.MiddleLeft,bold:true,glow:U.A(glow,o),shadow:U.A(shadow,o));U.Pop();
                    float sweep=M.Progress(a,.3f,.5f);if(sweep>0&&sweep<1){float sx=box.x+box.width*sweep;U.HGradient(new Rect(sx-60,box.y,60,box.height),new Color(1,1,1,0),new Color(1,1,1,.35f*alpha*(1-sweep)));}
                    break;
                }
                case NativeIntroCardStyle.Type:
                {
                    // Typed on at 22 letters a second behind a caret that blinks (slower in Reduced effects).
                    int shown=Mathf.Min(cardLetters.Length,Mathf.FloorToInt(a*22));
                    for(int i=0;i<shown;i++)U.Text(new Rect(box.x+cardX[i],box.y,cardW[i]+4,box.height),cardLetters[i],size,ink,TextAnchor.MiddleLeft,bold:true,mono:false,glow:glow,shadow:shadow);
                    float caretX=shown>0?box.x+cardX[shown-1]+cardW[shown-1]+4:box.x;float blink=reduced?1:(Mathf.Repeat(a,.8f)<.5f?1:0);
                    if(a<cardLetters.Length/22f+1.2f)U.Fill(new Rect(caretX,box.y+box.height*.18f,Mathf.Max(3,size*.06f),box.height*.64f),U.A(glow,blink*alpha));
                    break;
                }
                case NativeIntroCardStyle.Assemble:
                {
                    // Each letter flies in from a scattered spot, turning, and lands in its place, 30 ms apart.
                    for(int i=0;i<cardLetters.Length;i++)
                    {
                        float u=M.Bezier(.2f,.9f,.25f,1,M.Progress(a,i*.03f,.6f));if(u<=0)continue;float h=Mathf.Sin(i*12.9898f)*43758.5453f;h-=Mathf.Floor(h);float g=Mathf.Sin(i*78.233f)*12543.1f;g-=Mathf.Floor(g);
                        var off=new Vector2((h-.5f)*size*3,(g-.5f)*size*2.2f)*(1-u);var r=new Rect(box.x+cardX[i],box.y,cardW[i]+4,box.height);
                        U.Push(r.center,off,.6f+.4f*u,(1-u)*(h-.5f)*120);U.Text(r,cardLetters[i],size,U.A(ink,u),TextAnchor.MiddleLeft,bold:true,glow:U.A(glow,u),shadow:U.A(shadow,u),blur:6*(1-u));U.Pop();
                    }
                    break;
                }
                case NativeIntroCardStyle.Wipe:
                {
                    // A bright line draws across in .3 s, then the name is revealed from it, letter by letter, left to right.
                    float line=M.EaseOut(M.Progress(a,0,.3f));float fade=1-M.Progress(a,.55f,.4f);
                    U.HGradient(new Rect(box.x-20,box.center.y-1,(box.width+40)*line*.5f,2),new Color(glow.r,glow.g,glow.b,0),U.A(glow,fade*alpha*1.5f));U.HGradient(new Rect(box.x-20+(box.width+40)*line*.5f,box.center.y-1,(box.width+40)*line*.5f,2),U.A(glow,fade*alpha*1.5f),new Color(glow.r,glow.g,glow.b,0));
                    for(int i=0;i<cardLetters.Length;i++){float u=M.Bezier(.16f,.84f,.24f,1,M.Progress(a,.18f+cardX[i]/Mathf.Max(1,box.width)*.35f,.4f));if(u<=0)continue;var r=new Rect(box.x+cardX[i],box.y,cardW[i]+4,box.height);U.Push(r.center,new Vector2(0,(1-u)*size*.15f),1,0,u);U.Text(r,cardLetters[i],size,U.A(ink,u),TextAnchor.MiddleLeft,bold:true,glow:U.A(glow,u),shadow:U.A(shadow,u));U.Pop();}
                    break;
                }
                default:
                {
                    // Rise: up 22 px from scale .96 while the tracking closes and a 10 px blur clears.
                    float n=M.Bezier(.16f,.84f,.24f,1,M.Progress(a,0,.72f)),nb=10*(1-M.Bezier(.16f,.84f,.24f,1,M.Progress(a,0,.72f*.6f)));
                    U.Push(box.center,new Vector2(0,22*(1-n)),.96f+.04f*n);float spread=(1-n)*size*.14f;
                    for(int i=0;i<cardLetters.Length;i++){float dx=(i-(cardLetters.Length-1)*.5f)*spread;U.Text(new Rect(box.x+cardX[i]+dx,box.y,cardW[i]+4,box.height),cardLetters[i],size,U.A(ink,n),TextAnchor.MiddleLeft,bold:true,glow:U.A(glow,n),shadow:U.A(shadow,n),blur:nb);}
                    U.Pop();break;
                }
            }
        }
    }
}
