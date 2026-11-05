using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What the room should show for THE DEEP END at one moment (the hook the level's presenter reads, as NativeStationBossStage is the station's): the tide, the lights, the whirlpool, the caustics,
    /// the wave. A pure function of the record, the clock and the beats (<see cref="NativeDeepEndBossStage.Evaluate"/>). Heights are in the room's frame (the deck at 0).
    /// </summary>
    public struct DeepEndBossStage
    {
        public bool On;
        /// <summary>The water's level; how far past the pool's edge a tide over the decks has run (m; the flood's front: the room's half width means everywhere).</summary>
        public float Tide,Flood;
        /// <summary>The ceiling's lights 0..1 and how far their blackout has run outward from the middle (0 none .. 1 all out); the lights under the water 0..1 and how far their blackout has run (Order); the boss's flank lamps 0..1.</summary>
        public float Ceiling,CeilingOut,Water,WaterOut,Lamps;
        /// <summary>The room's soft light over its level's (1 as authored), and how warm it is (1 the arrival's calm, 0 the fight's cool).</summary>
        public float Ambient,Warm;
        /// <summary>The caustics' strength on the walls, ceiling and floors; the whirlpool's strength 0..1 and how far it has turned (rad); the surface's own ripples' strength (0 mirror still, 1 alive).</summary>
        public float Caustics,Whirl,WhirlTurn,Ripples;
        /// <summary>How much darker the water is than it is (1 as it is; the arrival's black shot more: only what's as bright as a lamp comes through it).</summary>
        public float Murk;
        /// <summary>The room's shudder, how muffled everything is (the wind-ups), the Lull's softening, the wave over the camera.</summary>
        public float Shake,Muffle,Soft,Wash;
        /// <summary>The seiche: the crest's place along its axis (m from the room's centre), its height, and the far wall's draw-back; its axis.</summary>
        public float SeicheAt,SeicheHeight,SeicheDraw;public Vector3 SeicheAxis;
        /// <summary>The arrival's wave, a ring from the break: radius, height.</summary>
        public float RingR,RingH,RingX,RingZ;
        /// <summary>The arrival's clock (NaN after it).</summary>
        public float IntroAge;
        public static readonly DeepEndBossStage Idle=new DeepEndBossStage{On=false,Tide=PoolroomsLayout.WaterY,Ceiling=1,Water=1,Lamps=1,Ambient=1,Warm=0,Ripples=1,Murk=1,IntroAge=float.NaN};
    }

    public static class NativeDeepEndBossStage
    {
        /// <summary>What the level's presenter reads (written every frame the boss draws), on which clock.</summary>
        public static DeepEndBossStage Shown=DeepEndBossStage.Idle;public static double ShownClock;
        private static float S(float x)=>BossMath.Smooth(x);
        public static DeepEndBossStage Evaluate(in DeepEndBossView v,float time,in DeepEndBossBeats b)
        {
            var s=DeepEndBossStage.Idle;if(!v.live)return s;s.On=true;
            // ---- the tide ----
            s.Tide=v.tideY;
            if(DeepEndBossView.At(v.tideAt)&&DeepEndBossView.At(v.tideUntil)&&time>=v.tideAt)s.Tide=v.tideFrom+(v.tideY-v.tideFrom)*S((time-v.tideAt)/Math.Max(.01f,v.tideUntil-v.tideAt));
            else if(DeepEndBossView.At(v.tideAt)&&time<v.tideAt)s.Tide=v.tideFrom;
            // The flood runs out from the pool's edge once the water passes the deck's level.
            if(s.Tide>0)
            {
                float crossed=DeepEndBossView.At(v.tideAt)&&v.tideUntil>v.tideAt?v.tideAt+(v.tideUntil-v.tideAt)*CrossAt(v.tideFrom,v.tideY):time-99;
                float run=time-crossed;s.Flood=run<0?0:Math.Min(NativeDeepEndArena.Half,run*5.2f);
            }
            int phase=Math.Max(1,Math.Min(NativeDeepEndBoss.Phases,v.phase));
            // ---- the phases' light ----
            s.Ceiling=phase==1?1:phase==2?.8f:1;s.Ambient=phase==1?1:phase==2?.74f:.3f;s.Caustics=phase==1?.55f:phase==2?.8f:1;s.Water=1;s.Lamps=1;s.Ripples=phase==1?.55f:phase==2?.85f:1;
            if(phase>=3&&DeepEndBossView.At(v.phaseAt)){float k=time-v.phaseAt;s.CeilingOut=S(k/2.4f);if(k<0)s.CeilingOut=0;}
            if(phase==2&&DeepEndBossView.At(v.phaseAt)){float k=time-v.phaseAt;s.Shake=Math.Max(s.Shake,.5f*Fade(k-.2f,3.2f));}
            if(phase==3&&DeepEndBossView.At(v.phaseAt)){float k=time-v.phaseAt;s.Shake=Math.Max(s.Shake,.4f*Fade(k-.4f,3f));}
            // ---- the arrival ----
            s.IntroAge=float.NaN;
            if(DeepEndBossView.At(v.spawned)&&time-v.spawned<b.Hold+2.5f)
            {
                float a=time-v.spawned;s.IntroAge=a;
                // Calm, warm, the water a mirror; the lights under the water go out one by one toward the pit, the room falling dark with them; black for the spiral.
                s.Warm=1-S((a-b.Hold)/.5f);s.Ripples=.03f+.2f*S((a-b.LowShot)/1.2f);s.Caustics=.34f*(1-S((a-b.LightsOut0)/1.6f));
                s.WaterOut=S((a-b.LightsOut0)/(b.LightsOut1-b.LightsOut0));
                float dark=S((a-b.LightsOut0)/(b.DownShot-b.LightsOut0));float lit=1-dark;s.Murk=1+3.4f*S((a-b.LightsOut1+.5f)/.6f)*(1-S((a-b.Break+.1f)/.5f));
                s.Ceiling=.95f*lit+.0f;s.Ambient=.92f*lit+.05f;
                // It breaks the surface: the room comes back only by what it brings. A rim of the ceiling's lights catching on, low.
                float back=S((a-b.Break+.1f)/.7f);s.Ceiling=Math.Max(s.Ceiling,.42f*back);s.Ambient=Math.Max(s.Ambient,.3f*back);s.Caustics=Math.Max(s.Caustics,.8f*back);s.Ripples=Math.Max(s.Ripples,back);
                s.Lamps=S((a-3.4f)/1f);
                // The drop: every light in the room comes up; the water's lights too.
                float drop=S((a-b.Hold+.05f)/.35f);s.Ceiling=s.Ceiling+(1-s.Ceiling)*drop;s.Ambient=s.Ambient+(1-s.Ambient)*drop;s.WaterOut=s.WaterOut*(1-drop);s.Caustics=s.Caustics+(.55f-s.Caustics)*drop;
                s.Shake=Math.Max(s.Shake,.7f*Fade(a-b.Break,.8f));s.Shake=Math.Max(s.Shake,.45f*S((a-b.Roar)/.3f)*(1-S((a-b.Hold-.3f)/.5f)));
                s.Muffle=a<b.Break?1:0;
                // The arrival's wave: a ring out from the break, tallest as it reaches the camera; over it at the end.
                float w=a-b.WaveAt;if(w>=0&&w<3.2f){s.RingR=5+w*12.6f;s.RingH=1.15f*S(w/.3f)*(1-S((w-1.8f)/1.2f));var fw=BossMath.Forward(v.yaw*BossMath.Deg);s.RingX=-fw.X*NativeDeepEndBoss.BreakBack;s.RingZ=-fw.Z*NativeDeepEndBoss.BreakBack;}
                s.Wash=WashAt(a,b);
            }
            // ---- the move playing ----
            if(v.Moving(time,out var m,out float age,out float hit))
            {
                if(age<hit)s.Muffle=Math.Max(s.Muffle,S(age/.3f));
                if(m.Id=="flop"&&age>=hit)s.Shake=Math.Max(s.Shake,Fade(age-hit,1.1f));
                if(m.Id=="breach"&&age>=hit)s.Shake=Math.Max(s.Shake,.55f*Fade(age-hit,.8f));
                if(m.Id=="drain"){float until=m.Strike+hit;s.Shake=Math.Max(s.Shake,.3f*S((age-hit)/.6f)*(1-S((age-until)/.6f)));}
            }
            // ---- the Drain's turning ----
            if(DeepEndBossView.At(v.whirlAt)&&time>=v.whirlAt)
            {
                float up=time-v.whirlAt,until=DeepEndBossView.At(v.whirlUntil)?v.whirlUntil:v.whirlAt+6;
                s.Whirl=S(up/1.8f)*(1-S((time-until)/2.4f));
                // How far it has turned: a slow start, faster, slowing as it dies (a smooth integral, so the water never jerks).
                s.WhirlTurn=WhirlAngle(up,until-v.whirlAt);
            }
            // ---- Seiche ----
            if(DeepEndBossView.At(v.seicheAt))
            {
                var mv=NativeDeepEndBoss.Move("seiche");float w=time-v.seicheAt;float half=NativeDeepEndArena.Half;var ax=new Vector3(v.aimX,0,v.aimZ);if(ax.LengthSquared()<.01f)ax=new Vector3(0,0,1);s.SeicheAxis=Vector3.Normalize(ax);
                float lead=mv.Anticipation;
                if(w>=-lead&&w<0){float k=S((w+lead)/lead);s.SeicheDraw=k;}
                if(w>=0&&w<=mv.Strike+.8f)
                {
                    float path=w*mv.Speed,len=2*half;float along=path<len?-half+path:half-(path-len);s.SeicheAt=along;s.SeicheHeight=NativeDeepEndBoss.SeicheHeight*S(w/.3f)*(1-S((w-mv.Strike)/.8f))*(path<len?1:.86f);
                    s.SeicheDraw=(1-S(w/.5f));
                }
            }
            // ---- the Lull's warning ----
            s.Soft=Math.Max(0,Math.Min(1,v.lull));s.Soft*=s.Soft;if(s.Soft>0)s.Muffle=Math.Max(s.Muffle,s.Soft);
            // ---- the death: the room comes back; the turning slows; sound returns ----
            if(v.Dead(time))
            {
                float d=time-v.diedAt;s.Muffle=0;float ease=S((d-2.2f)/2.4f);s.Ambient=s.Ambient+(1-s.Ambient)*ease;s.CeilingOut=s.CeilingOut*(1-ease);s.Lamps=1;
                s.Ripples=s.Ripples*(1-.75f*S((d-1)/2));
                // The tide falls, gently, once it has sunk.
                float fall=S((d-3.1f)/5f);s.Tide=s.Tide+(PoolroomsLayout.WaterY-s.Tide)*fall;if(s.Tide<=0)s.Flood=0;
                // A last slow turning down the pit.
                s.Whirl=Math.Max(s.Whirl,.38f*S((d-3.2f)/1.6f)*(1-S((d-9)/3)));s.WhirlTurn=Math.Max(s.WhirlTurn,(d-3.2f)*.5f);
            }
            return s;
        }
        private static float CrossAt(float from,float to){if(to<=from)return 1;float u=(0-from)/(to-from);u=BossMath.Clamp01(u);return InverseSmooth(u);}
        // Where a smoothstep reaches u.
        private static float InverseSmooth(float u)=>.5f-(float)Math.Sin(Math.Asin(1-2*BossMath.Clamp01(u))/3);
        private static float Fade(float t,float len)=>t<0||t>=len?0:(1-t/len)*(1-t/len);
        /// <summary>How far the wave over the camera covers the screen (the arrival's last beat): it rises at the wash's start and clears as the fight begins.</summary>
        public static float WashAt(float a,in DeepEndBossBeats b){float t=a-b.Hold+.28f;return t<0?0:S(t/.12f)*(1-S((t-.26f)/.5f));}
        /// <summary>The whirlpool's total turn (rad) `up` s into a turning `len` s long.</summary>
        public static float WhirlAngle(float up,float len){if(up<=0)return 0;const float rate=1.05f;float spin=up<1.8f?.5f*rate*up*up/1.8f:.5f*rate*1.8f+rate*(up-1.8f);if(up>len){float o=up-len;spin-=rate*Math.Min(o,3)*Math.Min(o,3)/6f;}return spin;}
    }

    /// <summary>
    /// The wave field over the whole room while the boss is here (the level's own <see cref="PoolWaves"/> covers 32 m round the eye over the pool cells and is untouched): a damped wave equation on a
    /// fixed grid of <see cref="Size"/> x Size texels of <see cref="Texel"/> m, stepped at a fixed <see cref="Rate"/>; the room's walls reflect it, and the decks are dry until the tide covers them.
    /// <see cref="Compose"/> adds what is analytic (the tide, the flood's front, the whirlpool's funnel, the seiche's crest, the arrival's ring) and writes the two-channel texture the water draws: the
    /// surface's height and its foam. Heights in metres, the room's frame. Presentation only: what water does to bodies is the rules'. Pure; deterministic for one sequence of calls.
    /// </summary>
    public sealed class DeepEndWaves
    {
        public const int Size=172;public const float Texel=.25f,Rate=60,Speed=4.6f,Damping=.9962f,Limit=1.25f,FoamLife=2.6f,Extent=Size*Texel;
        public const int MaxSteps=4;
        /// <summary>The most the simulated waves raise a pool over the deck's level while the decks are dry (they slop at the lip, they don't stand over it).</summary>
        public const float LipCap=.03f;
        public readonly float[] Height=new float[Size*Size],Prev=new float[Size*Size],Next=new float[Size*Size],Foam=new float[Size*Size];
        /// <summary>Per texel: the floor (from the arena) and whether the walls hold it. The texture's data: (surface height from the room's zero, foam) per texel.</summary>
        public readonly float[] Floor=new float[Size*Size];public readonly float[] Rg=new float[Size*Size*2];public readonly bool[] Wet=new bool[Size*Size],Inside=new bool[Size*Size];
        private bool placed;private bool decksWet;private float carry;private float[] h,p,n;public long Steps{get;private set;}
        public DeepEndWaves(){h=Height;p=Prev;n=Next;}
        private static float C(int i)=>(i+.5f-Size*.5f)*Texel;
        /// <summary>Lays the grid on the arena's floor (once).</summary>
        public void Place()
        {
            if(placed)return;placed=true;h=Height;p=Prev;n=Next;
            for(int z=0;z<Size;z++)for(int x=0;x<Size;x++){int i=x+Size*z;float wx=C(x),wz=C(z);Inside[i]=NativeDeepEndArena.InRoom(wx,wz);Floor[i]=NativeDeepEndArena.Floor(wx,wz);}
            decksWet=true;SetWet(false);
        }
        public void Clear(){Array.Clear(Height,0,Height.Length);Array.Clear(Prev,0,Prev.Length);Array.Clear(Next,0,Next.Length);Array.Clear(Foam,0,Foam.Length);h=Height;p=Prev;n=Next;}
        private void SetWet(bool decks)
        {
            if(decks==decksWet)return;decksWet=decks;
            for(int i=0;i<Wet.Length;i++){Wet[i]=Inside[i]&&(decks||Floor[i]<-.05f);if(!Wet[i]){h[i]=0;p[i]=0;}}
        }
        public float[] Current=>h;
        /// <summary>Pushes the surface by `amount` (m) over a round patch of `radius` m at (x, z, the room's frame), eased to nothing at its rim; foam too if asked.</summary>
        public void Disturb(float x,float z,float radius,float amount,float foam=0)
        {
            float gx=x/Texel+Size*.5f-.5f,gz=z/Texel+Size*.5f-.5f,r=Math.Max(radius/Texel,.75f);
            int x0=Math.Max(0,(int)Math.Floor(gx-r)),x1=Math.Min(Size-1,(int)Math.Ceiling(gx+r)),z0=Math.Max(0,(int)Math.Floor(gz-r)),z1=Math.Min(Size-1,(int)Math.Ceiling(gz+r));
            for(int k=z0;k<=z1;k++)for(int j=x0;j<=x1;j++)
            {
                int i=j+Size*k;if(!Wet[i])continue;float d=(float)Math.Sqrt((j-gx)*(j-gx)+(k-gz)*(k-gz));if(d>=r)continue;
                float w=.5f*(1+(float)Math.Cos(Math.PI*d/r));float v=h[i]+amount*w;h[i]=v<-Limit?-Limit:v>Limit?Limit:v;
                // (The last height moves with it: a displacement of the surface, not a kick: a kick would go on raising the whole pool's level.)
                float q=p[i]+amount*w;p[i]=q<-Limit?-Limit:q>Limit?Limit:q;
                if(foam>0){float f=Foam[i]+foam*w;Foam[i]=f>1?1:f;}
            }
        }
        public void AddFoam(float x,float z,float radius,float amount)
        {
            float gx=x/Texel+Size*.5f-.5f,gz=z/Texel+Size*.5f-.5f,r=Math.Max(radius/Texel,.75f);
            int x0=Math.Max(0,(int)Math.Floor(gx-r)),x1=Math.Min(Size-1,(int)Math.Ceiling(gx+r)),z0=Math.Max(0,(int)Math.Floor(gz-r)),z1=Math.Min(Size-1,(int)Math.Ceiling(gz+r));
            for(int k=z0;k<=z1;k++)for(int j=x0;j<=x1;j++){int i=j+Size*k;if(!Wet[i])continue;float d=(float)Math.Sqrt((j-gx)*(j-gx)+(k-gz)*(k-gz));if(d>=r)continue;float w=1-d/r;float f=Foam[i]+amount*w;Foam[i]=f>1?1:f;}
        }
        /// <summary>Advances the waves `dt` s in fixed steps; the decks are wet while `decks`.</summary>
        public void Step(float dt,bool decks)
        {
            SetWet(decks);if(!(dt>0))return;carry+=dt;int steps=(int)Math.Floor(carry*Rate);if(steps>MaxSteps){steps=MaxSteps;carry=0;}else carry-=steps/Rate;
            float k=(Speed/Rate/Texel)*(Speed/Rate/Texel),fade=(float)Math.Exp(-1/(Rate*FoamLife));
            for(int s=0;s<steps;s++)
            {
                for(int z=0;z<Size;z++)for(int x=0;x<Size;x++)
                {
                    int i=x+Size*z;if(!Wet[i]){n[i]=0;continue;}float c=h[i];
                    float l=x>0&&Wet[i-1]?h[i-1]:c,r=x<Size-1&&Wet[i+1]?h[i+1]:c,d=z>0&&Wet[i-Size]?h[i-Size]:c,u=z<Size-1&&Wet[i+Size]?h[i+Size]:c;
                    float v=(2*c-p[i]+k*(l+r+d+u-4*c))*Damping;n[i]=v<-Limit?-Limit:v>Limit?Limit:v;Foam[i]*=fade;
                }
                var old=p;p=h;h=n;n=old;Steps++;
            }
        }
        /// <summary>The surface's height at (x, z): the level plus the simulated part (bilinear; not the analytic parts).</summary>
        public float WaveAt(float x,float z)
        {
            float gx=x/Texel+Size*.5f-.5f,gz=z/Texel+Size*.5f-.5f;int j=(int)Math.Floor(gx),k=(int)Math.Floor(gz);if(j<0||k<0||j>=Size-1||k>=Size-1)return 0;float fx=gx-j,fz=gz-k;int i=j+Size*k;
            return (h[i]*(1-fx)+h[i+1]*fx)*(1-fz)+(h[i+Size]*(1-fx)+h[i+Size+1]*fx)*fz;
        }
        /// <summary>The whole surface's height at (x, z) at this stage: everything <see cref="Compose"/> writes.</summary>
        public static float Analytic(float x,float z,in DeepEndBossStage s,out float foam)
        {
            float y=s.Tide;foam=0;
            // ---- the flood: while the front runs out over the deck, the deck's water thins to nothing at its edge ----
            if(s.Tide>0&&s.Flood<NativeDeepEndArena.Half)
            {
                float ax=Math.Abs(x),az=Math.Abs(z),past=Math.Max(ax,az)-NativeDeepEndArena.PoolHalf;
                if(past>0){float e=(s.Flood-past);float k=BossMath.Smooth(e/1.6f);y=-.25f+(s.Tide+.25f)*k;foam=Math.Max(foam,(float)Math.Exp(-(e-.3f)*(e-.3f)/.5f));}
            }
            // ---- the whirlpool: a funnel over the pit, spiral arms of foam going down it ----
            if(s.Whirl>.002f)
            {
                float r2=x*x+z*z,r=(float)Math.Sqrt(r2),R=7.5f;float funnel=(R*R)/(r2+R*R*.16f)*.62f;
                float ang=(float)Math.Atan2(z,x);const float arms=3;float spiral=ang*arms+(float)Math.Log(r+1.2f)*5.4f-s.WhirlTurn*4.2f;
                float wall=BossMath.Smooth((19-r)/6f);y+=s.Whirl*wall*(-funnel*1.15f+.14f*(float)Math.Sin(spiral)*BossMath.Smooth((r-1)/3));
                float arm=(float)Math.Pow(.5f+.5f*Math.Sin(spiral),5);foam=Math.Max(foam,s.Whirl*arm*wall*BossMath.Smooth(r/2)*.9f);
                if(r<2.1f)y-=s.Whirl*(2.1f-r)*.9f;
            }
            // ---- Seiche: the crest's line across the room; the water draws back from the far wall and piles up behind ----
            if(s.SeicheHeight>.002f||s.SeicheDraw>.002f)
            {
                float d=x*s.SeicheAxis.X+z*s.SeicheAxis.Z;float half=NativeDeepEndArena.Half;
                if(s.SeicheHeight>.002f)
                {
                    float e=d-s.SeicheAt,side=x*s.SeicheAxis.Z-z*s.SeicheAxis.X;e+=.4f*(float)Math.Sin(side*.34f);
                    y+=s.SeicheHeight*((float)Math.Exp(-e*e/2.4f)-.34f*(float)Math.Exp(-(e+2.8f)*(e+2.8f)/6.5f));
                    foam=Math.Max(foam,Math.Min(1,s.SeicheHeight*1.3f)*(float)Math.Exp(-(e-.5f)*(e-.5f)/.8f));
                }
                if(s.SeicheDraw>.002f){y+=s.SeicheDraw*(.42f*BossMath.Smooth((-d-half*.6f)/(half*.4f))-.46f*BossMath.Smooth((d-half*.3f)/(half*.7f)));}
            }
            // ---- the arrival's ring ----
            if(s.RingH>.002f)
            {
                float r=(float)Math.Sqrt((x-s.RingX)*(x-s.RingX)+(z-s.RingZ)*(z-s.RingZ)),e=r-s.RingR;y+=s.RingH*((float)Math.Exp(-e*e/3.4f)-.3f*(float)Math.Exp(-(e+3.4f)*(e+3.4f)/8f));foam=Math.Max(foam,Math.Min(1,s.RingH*1.1f)*(float)Math.Exp(-(e-.7f)*(e-.7f)/1.2f));
            }
            return y;
        }
        /// <summary>Writes this frame's texture data from the simulated field and the stage.</summary>
        public void Compose(in DeepEndBossStage s)
        {
            // Over the dry deck the sheet stays under the tile (a plain fast path when nothing analytic moves).
            bool simple=s.Whirl<=.002f&&s.SeicheHeight<=.002f&&s.SeicheDraw<=.002f&&s.RingH<=.002f&&!(s.Tide>0&&s.Flood<NativeDeepEndArena.Half);
            float tide=s.Tide;
            for(int z=0;z<Size;z++)for(int x=0;x<Size;x++)
            {
                int i=x+Size*z;float y,foam=0;if(simple)y=tide;else y=Analytic(C(x),C(z),s,out foam);
                if(Wet[i]){float by=y;y+=h[i];if(!decksWet&&y>LipCap&&y>by)y=by>LipCap?by:LipCap;}float f=Foam[i];if(foam>f)f=foam;Rg[i*2]=y;Rg[i*2+1]=f;
            }
        }
        /// <summary>The field's energy (checks).</summary>
        public double Energy(){double e=0;foreach(float v in h)e+=v*v;return e;}
    }
}
