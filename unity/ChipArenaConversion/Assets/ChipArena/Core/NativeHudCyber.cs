using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The cyber HUD's layout and motion rules (owner 2026-10-12: "Also make the Hud match the cyber theme~"). Presentation
    /// only: Runtime/NativeHudChrome.cs draws with them in the palette of Runtime/NativeCyberTheme.cs, and the managed checks
    /// (Tests/HudCyberChecks.cs) cover the contrast, segment, envelope and fit rules here. Nothing in the simulation reads it.
    /// </summary>
    public static class NativeHudCyber
    {
        // ---- Contrast (WCAG 2.x) -----------------------------------------------------------------------------------------
        /// <summary>Relative luminance of an sRGB 0xRRGGBB colour.</summary>
        public static double Luminance(int rgb)
        {
            double C(int v){double c=v/255.0;return c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);}
            return .2126*C((rgb>>16)&255)+.7152*C((rgb>>8)&255)+.0722*C(rgb&255);
        }
        /// <summary>Contrast ratio of two opaque colours (1..21).</summary>
        public static double Contrast(int a,int b){double la=Luminance(a),lb=Luminance(b);return (Math.Max(la,lb)+.05)/(Math.Min(la,lb)+.05);}
        /// <summary>A translucent layer `fg` at `alpha` over an opaque `bg`, blended on sRGB values as the IMGUI pass does.</summary>
        public static int Over(int fg,float alpha,int bg)
        {
            alpha=Math.Max(0,Math.Min(1,alpha));int o=0;
            for(int s=16;s>=0;s-=8){int f=(fg>>s)&255,b=(bg>>s)&255;o|=(int)Math.Round(f*alpha+b*(1-alpha))<<s;}
            return o;
        }
        /// <summary>The weakest contrast of `text` over a panel `fill` at `alpha` laid over any scene (black to white).</summary>
        public static double PanelContrast(int text,int fill,float alpha)=>Math.Min(Contrast(text,Over(fill,alpha,0x000000)),Contrast(text,Over(fill,alpha,0xffffff)));
        /// <summary>WCAG AA for normal text.</summary>
        public const double TextContrast=4.5;

        // ---- Panels ------------------------------------------------------------------------------------------------------
        /// <summary>The holographic panel fill's opacity over the 3D scene: high enough that every text tone keeps 4.5:1 over a
        /// white sky (the checks prove it), low enough that the scene still reads through as glass.</summary>
        public const float PanelAlpha=.9f;
        /// <summary>The chamfer (cut corner) of the HUD panels, cards and tags, in canvas units.</summary>
        public const float Cut=9,CardCut=7,TagCut=5;
        /// <summary>Scanline period and line height in canvas units (a static texture: no motion, so no reduced-effects gate).</summary>
        public const float ScanPeriod=3,ScanAlpha=.045f;

        // ---- Segmented bars ----------------------------------------------------------------------------------------------
        private static readonly float[] Steps={1,2,4,5,6,8,10,12,15,20,25,50,100,200,250,500,1000,2500,5000,10000};
        /// <summary>The value one segment stands for: the smallest "nice" step that splits `max` into at most `limit` segments
        /// (100 HP in 10s, 48 rounds in 4s, a 6000 HP boss in 500s). A non-positive max gives one segment of 1.</summary>
        public static float SegmentStep(float max,int limit)
        {
            if(!(max>0)||float.IsInfinity(max))return 1;limit=Math.Max(1,limit);
            foreach(float s in Steps)if(Math.Ceiling(max/s-1e-4)<=limit)return s;
            return (float)Math.Ceiling(max/limit);
        }
        /// <summary>How many segments `max` takes at `step` (the last one may be partial).</summary>
        public static int SegmentCount(float max,float step)=>!(max>0)||!(step>0)?1:Math.Max(1,(int)Math.Ceiling(max/step-1e-4));
        /// <summary>Segment i's span as fractions of the bar: [from, to).</summary>
        public static void SegmentSpan(int i,float step,float max,out float from,out float to)
        {
            if(!(max>0)){from=0;to=1;return;}
            from=Math.Max(0,Math.Min(1,i*step/max));to=Math.Max(0,Math.Min(1,(i+1)*step/max));
        }
        /// <summary>Segment i's fill (0..1) for `value` of `max` at `step`.</summary>
        public static float SegmentFill(int i,float step,float max,float value)
        {
            if(!(max>0)||!(step>0))return 0;float lo=i*step,hi=Math.Min(max,(i+1)*step);if(hi<=lo)return 0;
            return Math.Max(0,Math.Min(1,(value-lo)/(hi-lo)));
        }

        // ---- Health tone ---------------------------------------------------------------------------------------------------
        /// <summary>The vitals turn to the danger tone at or under 30% (the overhead bars' old low tint was 25%).</summary>
        public const float LowHp=.3f;
        public static bool Low(float ratio)=>ratio>0&&ratio<=LowHp;

        // ---- Event envelopes (glitch accents only on events) ---------------------------------------------------------------
        /// <summary>The magenta flare on a card that just became ready: full at once, gone by .6 s (ease out).</summary>
        public const float FlareLife=.6f;
        public static float Flare(float age){if(!(age>=0)||age>=FlareLife)return 0;float u=1-age/FlareLife;return u*u;}
        /// <summary>A glitch burst's length: a hit taken, dropping into low health, a card turning ready.</summary>
        public const float GlitchLife=.18f;
        public static bool Glitching(float age)=>age>=0&&age<GlitchLife;
        /// <summary>The burst's moment as the Siren split's cycle position (NativeUi.SirenSplit shows its three banded steps
        /// between .65 and .72 of a cycle), so the HUD reuses the one glitch primitive.</summary>
        public static float GlitchCycle(float age)=>.65f+.0699f*Math.Max(0,Math.Min(1,age/GlitchLife));
        /// <summary>The low-health edge breathing (1.4 s), held at full with Reduced effects (no flicker).</summary>
        public static float Breath(float time,bool reduced)=>reduced?1:.55f+.45f*(float)(.5+.5*Math.Cos(time*2*Math.PI/1.4));

        // ---- Overhead self bar ---------------------------------------------------------------------------------------------
        /// <summary>The own-pilot resource rows fade to this when every resource is full and has not changed for a while.</summary>
        public const float IdleAlpha=.28f,IdleAfter=1.6f,IdleFadeTime=.6f;
        public static float IdleFade(float sinceChange,bool full)
        {
            if(!full||!(sinceChange>IdleAfter))return 1;float u=Math.Min(1,(sinceChange-IdleAfter)/IdleFadeTime);return 1-(1-IdleAlpha)*u;
        }
        /// <summary>The overhead bar keeps clear of the aim point: fully shown from `Clear` canvas units away, down to
        /// `AimAlpha` when it covers the crosshair (shoulder views can bring the head close to the screen centre).</summary>
        public const float Clear=48,Near=14,AimAlpha=.22f;
        public static float AimFade(float x,float y,float w,float h,float cx,float cy)
        {
            float dx=Math.Max(Math.Max(x-cx,0),cx-(x+w)),dy=Math.Max(Math.Max(y-cy,0),cy-(y+h)),d=(float)Math.Sqrt(dx*dx+dy*dy);
            float u=Math.Max(0,Math.Min(1,(d-Near)/(Clear-Near)));return AimAlpha+(1-AimAlpha)*u;
        }
        /// <summary>Which resource rows the own pilot's overhead bar carries.</summary>
        [Flags] public enum Rows{None=0,Stamina=1,Mana=2,Ammo=4,Reload=8,Heat=16,Vent=32,Charge=64}
        /// <summary>The rows for a weapon class at a moment: melee stamina, magic mana, ranged magazine (with the reload sweep
        /// while reloading) and the machine gun's Overdrive heat / vent while they run; a DEV fighter's charge while held.</summary>
        public static Rows SelfRows(string weaponClass,bool reloading,bool overdrive,bool vent,bool charging)
        {
            var r=weaponClass=="ranged"?Rows.Ammo|(reloading?Rows.Reload:0):weaponClass=="magic"?Rows.Mana:Rows.Stamina;
            if(overdrive)r|=Rows.Heat;if(vent)r|=Rows.Vent;if(charging)r|=Rows.Charge;return r;
        }
        /// <summary>Overdrive heat 0..1 over the 4 s buff, and the vent's cooling 1..0 over its .8 s.</summary>
        public static float Heat(float time,float at,float until)=>until>at?Math.Max(0,Math.Min(1,(time-at)/(until-at))):0;
        public static float Cooling(float time,float until,float ventUntil)=>ventUntil>until?Math.Max(0,Math.Min(1,(ventUntil-time)/(ventUntil-until))):0;
        /// <summary>The reload sweep 0..1 between ReloadAt and ReloadEnd.</summary>
        public static float Reload(float time,float at,float end)=>end>at?Math.Max(0,Math.Min(1,(time-at)/(end-at))):0;

        // ---- Layout fit ----------------------------------------------------------------------------------------------------
        /// <summary>The bottom HUD row fits a canvas when the vitals (left, from 24) and the cooldown cards (right, to 24) keep
        /// `gap` between them; the canvas is NativeUiMotion.Measure's for the window and interface scale.</summary>
        public const float Edge=24,RowGap=24;
        public static bool RowFits(float canvasWidth,float vitalsWidth,float cardsWidth)=>Edge+vitalsWidth+RowGap+cardsWidth+Edge<=canvasWidth+.01f;
    }

    /// <summary>A bar's damage trail (the ghost of what was just lost): it holds .22 s after the last loss, then drains at .9
    /// of the bar per second, and snaps up on a gain; the same rule as the overhead HealthLag and NativeDamageScreen.LagStep.</summary>
    public sealed class NativeHudTrail
    {
        public const float Hold=.22f,Drain=.9f,Gap=.35f;
        private float value=-1,last,time=-100,hold;
        public void Reset(){value=-1;time=-100;hold=0;}
        public float Step(float now,float ratio)
        {
            float dt=now-time;time=now;
            if(value<0||dt<0||dt>Gap||ratio>=value){value=ratio;hold=0;}
            else{hold=ratio<last?0:hold+Math.Min(dt,.05f);if(hold>Hold)value=Math.Max(ratio,value-Math.Min(dt,.05f)*Drain);}
            last=ratio;return value;
        }
    }

    /// <summary>The moments a HUD element changed, for the event accents: when a value last dropped (a hit), crossed into
    /// low health, or a flag (a card's READY) last turned on. Keyed by the caller's slot; cleared with the match.</summary>
    public sealed class NativeHudEvents
    {
        private readonly float[] value=new float[16],at=new float[16],last=new float[16];private readonly bool[] seen=new bool[16],flag=new bool[16];
        public void Clear(){for(int i=0;i<seen.Length;i++){seen[i]=false;flag[i]=false;at[i]=-100;last[i]=0;}}
        // A clock that ran backwards (a new match or round restarting the arena time) starts the slot over.
        private bool Fresh(int slot,float now){bool fresh=!seen[slot]||now<last[slot];seen[slot]=true;last[slot]=now;if(fresh)at[slot]=-100;return fresh;}
        public NativeHudEvents(){Clear();}
        /// <summary>Seconds since `slot`'s value last fell (a loss), or a large age; a rise or the first sample is no event.</summary>
        public float Drop(int slot,float now,float v)
        {
            if(slot<0||slot>=value.Length)return 100;
            if(Fresh(slot,now)){value[slot]=v;return 100;}
            if(v<value[slot]-1e-4f)at[slot]=now;value[slot]=v;return now-at[slot];
        }
        /// <summary>Seconds since `slot`'s flag last turned on (false to true), or a large age; the first sample is no event.</summary>
        public float Rise(int slot,float now,bool on)
        {
            if(slot<0||slot>=flag.Length)return 100;
            if(Fresh(slot,now)){flag[slot]=on;return 100;}
            if(on&&!flag[slot])at[slot]=now;flag[slot]=on;return now-at[slot];
        }
    }
}
