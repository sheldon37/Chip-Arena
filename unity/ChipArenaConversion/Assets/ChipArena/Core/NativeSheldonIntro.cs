using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>The Sheldon Intro's full-screen glitch for one frame (NativeSheldonIntro.Glitch; drawn by NativeGlitchScreen.shader).</summary>
    public struct NativeSheldonGlitch
    {
        /// <summary>Slice offsets (rows torn sideways), the RGB split, scanline streaks, datamosh blocks, the blackout, the
        /// radial chromatic aberration and digital specks, each 0..1; the pattern's seed (it jumps on glitch steps) and how
        /// many of the shader's stages run (1: tear, split and blackout; 2: + scanlines and specks; 3: + blocks).</summary>
        public float Tear,Split,Scan,Blocks,Black,Chroma,Noise;public int Seed,Passes;
        /// <summary>Whether anything shows (an idle frame skips the pass).</summary>
        public bool Visible=>Tear>.002f||Split>.002f||Scan>.002f||Blocks>.002f||Black>.002f||Chroma>.002f||Noise>.002f;
    }

    /// <summary>
    /// The Sheldon Intro (DEV entrance "sheldon-intro", owner 2026-10-12; revised the same day: "They enter the screen with a
    /// cyber effect; still glitchy screen, they dont "nod" at end. They just stare breathing", "a better background for this
    /// kind of intro", and the music re-cut from the track's opening). The pilot rezzes in out of converging data streams, a
    /// band of the body locking on each of the four glitch ticks (landed in a crouch, the last tick the slam); the signal
    /// jolts and cuts to black in the music's gap; the figure comes back on the next hit as a projection and rises through the
    /// bass swell; on the groove's first downbeat it resolves in the Sheldon Head look (hood, glowing face and broken ring,
    /// worn for the intro whatever hat is equipped) with the left hand already on the hood, and from there only breathes and
    /// stares into the camera (the head holds still: no nod, laugh throw or settle) while the camera pushes in, the name card
    /// builds and the grin flares on a later downbeat (the face only), then a final glitch cut. It plays in its own set
    /// (NativeSheldonSet). Every beat sits on its music (intro.sheldon-intro, NativeSoundBank: the opening of the owner's
    /// track): the marks below are seconds from the intro start. It keeps the standard timing (4.8 s, name card 3.2 s) and the
    /// presentation clock is the age (no slow-motion beat), so the marks stay on the music. Presentation only: the match sees
    /// the style id and its unchanged duration; DEV access is checked where every pick is (NativeCosmeticGate,
    /// NativeIntroRules.Style). Reduced effects keep every beat but never strobe (soft glitches stepping at 6 Hz, ramped
    /// blackouts and rez steps, no blocks or specks); the low graphics tier runs one shader stage and a cheaper set.
    /// Everything is a pure function of the age.
    /// </summary>
    public static class NativeSheldonIntro
    {
        public const string Style=NativeCosmeticGate.SheldonIntro;
        /// <summary>The four stuttered glitch ticks out of the silent lead-in (.12 s apart, near-silent between them): one rez
        /// step each. The first draws the figure's outline; the last is the slam (the arrival's tear).</summary>
        public static readonly float[] Ticks={.15f,.27f,.39f,.51f};
        public const float FirstTick=.15f,Slam=.51f;
        /// <summary>A hit: the signal jolts and starts failing (the glitch thickens, the frame darkens into the gap).</summary>
        public const float Jolt=.86f;
        /// <summary>The music's near-silent gap: a cut to black (a dead frame) from Silence to SilenceEnd, its dead pixels flaring
        /// on the small hit at Click.</summary>
        public const float Silence=1.05f,Click=1.09f,SilenceEnd=1.20f;
        /// <summary>The strong hit out of the gap: the hard re-entry (the frame torn back in, dim; the figure a projection, still down).</summary>
        public const float Return=1.21f;
        /// <summary>The rise begins: the figure stands up through the swell.</summary>
        public const float Rise=1.56f;
        /// <summary>The bass swell (from, peak, to): the projection charges and the glitch thickens to the peak.</summary>
        public const float SwellFrom=1.83f,SwellPeak=2.17f,SwellTo=2.44f;
        /// <summary>The groove locks in on its first downbeat: the figure resolves; hood, face and ring snap on; the stare begins.</summary>
        public const float Resolve=2.38f;
        /// <summary>The groove's downbeat spacing (about 128 BPM) and its downbeats in the cut, from Resolve.</summary>
        public const float Downbeat=.47f;
        public static readonly float[] Downbeats={2.38f,2.85f,3.32f,3.79f,4.26f,4.73f};
        /// <summary>The name build over two downbeats (the name types in, the grin builds; the brief chromatic aberration).</summary>
        public const float BuildFrom=3.32f,BuildTo=3.79f;
        /// <summary>The fifth downbeat: the grin flares in three stutters (the face only; the head holds), then the exit cut.</summary>
        public const float Flare=4.26f;
        /// <summary>The final glitch cut, to black at the hand-off (the cut fades out mid-groove at End).</summary>
        public const float Cut=4.6f,End=4.8f;
        /// <summary>The hi-hats' spacing in the groove: the flare's three stutters fall on them.</summary>
        public const float Stutter=.12f;
        public static readonly float[] Beats={.15f,.27f,.39f,Slam,Jolt,Silence,Click,SilenceEnd,Return,Rise,SwellFrom,SwellPeak,Resolve,SwellTo,BuildFrom,BuildTo,Flare};
        /// <summary>The stare's breath: one every four downbeats (in over two, out over two).</summary>
        public const float BreathPeriod=4*Downbeat;
        /// <summary>Glitch steps per second (the pattern holds between steps): full and reduced effects.</summary>
        public const float StepRate=24,ReducedStepRate=6;
        public static readonly Vector3 Cyan=NativeEntranceFx.Hex(0x22e6ff),Magenta=NativeEntranceFx.Hex(0xff3dcf);
        /// <summary>The set's data blue (the owner's picture: electric blue streaks round the cyan glow) and its void.</summary>
        public static readonly Vector3 Blue=NativeEntranceFx.Hex(0x2f6bff),Navy=NativeEntranceFx.Hex(0x03060e);
        /// <summary>The intro's whole look (the owner's reference): the Sheldon Clothes body skin (whatever body is
        /// equipped, as the hood is worn whatever hat is) in the hood's own dark fabric (the export's body colour: the coat's
        /// cloth untinted) and its accents in the glow colour (the pilot's hat colour, cyan on Auto), under the Sheldon Head.
        /// The pilot's own body and colours return with the match.</summary>
        public const string Suit="#e5e7eb";
        public static string Accent(string hatColor)=>NativeHatLooks.Hex(NativeHatLooks.Tint(NativeHatLooks.SheldonHead,hatColor));
        public static int Step(float age,bool reduced)=>(int)Math.Floor(Math.Max(0,age)*(reduced?ReducedStepRate:StepRate));
        public static float Hash(int n,int salt)=>NativeSheldonHead.Hash01(n,salt);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        // A glitch hit: instant on (a cut), decaying; soft (a short ramp in) with reduced effects.
        private static float Hit(float age,float at,float decay,bool reduced)
        {
            float s=age-at;if(s<0)return 0;float k=(float)Math.Exp(-s/decay);return reduced?k*Smooth(s/.07f):k;
        }
        /// <summary>The bass swell's envelope (0..1, peaking at SwellPeak).</summary>
        public static float Swell(float age)=>age<=SwellFrom||age>=SwellTo?0:age<SwellPeak?Smooth((age-SwellFrom)/(SwellPeak-SwellFrom)):1-Smooth((age-SwellPeak)/(SwellTo-SwellPeak));
        /// <summary>The stare's breath (0 out .. 1 in) at intro age `age`: none before the resolve, then in on the next two
        /// downbeats and out on the two after (starting still, so the resolve does not jolt).</summary>
        public static float Breath(float age)=>age<=Resolve?0:.5f-.5f*(float)Math.Cos(2*Math.PI*(age-Resolve)/BreathPeriod);
        /// <summary>How many rez steps have landed (0 before the first tick .. 4 from the slam); reduced effects: one smooth rise.</summary>
        public static float Rezzed(float age,bool reduced)
        {
            if(reduced)return 4*Smooth((age-(FirstTick-.05f))/(Slam-FirstTick+.05f));
            int n=0;for(int i=0;i<Ticks.Length;i++)if(age>=Ticks[i])n++;return n;
        }

        /// <summary>The full-screen glitch at intro age `age`.</summary>
        public static NativeSheldonGlitch Glitch(float age,bool reduced,bool low)
        {
            var g=new NativeSheldonGlitch{Seed=Step(age,reduced),Passes=low?1:3};
            if(!(age>=0)||age>End+.25f)return g;
            int step=g.Seed;float burst=0;
            // Between the hits: short bursts on random steps (none with reduced effects: the bed stays soft and steady).
            if(!reduced&&(age>Slam&&age<Silence||age>Return&&age<Resolve))burst=Hash(step,17)>.7f?.35f+.4f*Hash(step,19):0;
            // The silent lead-in: a dark frame with faint scanlines and specks, lifted a step and torn by each tick (one smooth
            // lift with reduced effects); the slam tears it wide open.
            float lift=Rezzed(age,reduced)/4;
            g.Black=.8f*(1-lift);g.Scan=.2f;g.Noise=.04f*(1-lift);
            for(int i=0;i<3;i++){float k=Hit(age,Ticks[i],.07f,reduced);g.Tear+=.45f*k;g.Split+=.5f*k;g.Scan+=.25f*k;g.Noise+=.1f*k;g.Blocks+=.35f*k;g.Chroma+=.2f*k;}
            float slam=Hit(age,Slam,.28f,reduced);g.Tear+=slam;g.Split+=slam;g.Blocks+=.9f*slam;g.Chroma+=.6f*slam;g.Noise+=.4f*slam;
            if(age>Slam&&age<Silence){g.Tear+=.12f+burst;g.Split+=.15f+.6f*burst;g.Scan+=.15f;g.Blocks+=.5f*burst;}
            // The jolt: a hit, then thicker glitch and a darkening frame into the gap.
            float jolt=Hit(age,Jolt,.2f,reduced);g.Tear+=.7f*jolt;g.Split+=.8f*jolt;g.Chroma+=.5f*jolt;g.Blocks+=.5f*jolt;
            float fail=Smooth((age-Jolt)/(Silence-Jolt))*(age<Silence?1:0);g.Tear+=.4f*fail;g.Split+=.4f*fail;g.Scan+=.3f*fail;g.Blocks+=.4f*fail;g.Black+=.35f*fail;
            // The gap: a cut to black with a few dead pixels, flaring on the click (reduced: a ramped fade, no specks).
            if(reduced)g.Black=Math.Max(g.Black,.9f*Smooth((age-(Silence-.1f))/.14f)*(1-Smooth((age-SilenceEnd)/.14f)));
            else if(age>=Silence&&age<Return){g.Black=1;g.Tear=g.Split=g.Blocks=g.Scan=0;g.Noise=.12f+.5f*Hit(age,Click,.04f,false);}
            // The re-entry: the strong hit tears the frame back in, dim and scanlined, lifting through the rise; the swell
            // thickens the glitch to its peak.
            float back=Hit(age,Return,.25f,reduced);g.Tear+=.85f*back;g.Split+=back;g.Chroma+=.8f*back;g.Blocks+=.5f*back;
            if(age>=Return&&age<Resolve){float dim=(.45f-.35f*Smooth((age-Return)/(SwellFrom-Return)))*(1-Smooth((age-(Resolve-.3f))/.3f));g.Black=Math.Max(g.Black,dim);g.Scan+=.4f;g.Split+=.25f+.3f*burst;g.Tear+=.12f+burst;}
            float swell=Swell(age);g.Tear+=.3f*swell;g.Split+=.35f*swell;g.Chroma+=.35f*swell;g.Scan+=.2f*swell;g.Blocks+=.25f*swell;
            // The resolve on the first downbeat: everything snaps in on a torn, split frame; then a faint scan pulse on each downbeat.
            float resolve=Hit(age,Resolve,.3f,reduced);g.Tear+=.85f*resolve;g.Split+=resolve;g.Chroma+=resolve;g.Blocks+=.6f*resolve;
            if(age>=Resolve){g.Scan+=.08f;for(int i=1;i<Downbeats.Length;i++){float d=Hit(age,Downbeats[i],.1f,reduced);g.Scan+=.12f*d;g.Split+=.06f*d;}}
            // The build: the push-in's brief chromatic aberration and a dip in the light.
            float b=age>BuildFrom&&age<BuildTo+.25f?(float)Math.Pow(Math.Sin(Math.PI*(age-BuildFrom)/(BuildTo+.25f-BuildFrom)),2):0;
            g.Chroma+=.7f*b;g.Split+=.25f*b;g.Scan+=.2f*b;g.Black+=.12f*b;
            // The flare: three stutters on the hi-hats (one soft swell with reduced effects).
            float laugh=reduced?.8f*Hit(age,Flare,.35f,true):Hit(age,Flare,.07f,false)+.8f*Hit(age,Flare+Stutter,.07f,false)+.6f*Hit(age,Flare+2*Stutter,.07f,false);
            g.Tear+=.6f*laugh;g.Split+=.8f*laugh;g.Chroma+=.5f*laugh;g.Blocks+=.3f*laugh;
            // The exit: a glitch cut to black at the hand-off.
            float exit=Smooth((age-Cut)/(End-Cut));g.Tear+=exit;g.Blocks+=.9f*exit;g.Split+=.6f*exit;g.Scan+=.3f*exit;g.Black=Math.Max(g.Black,(reduced?.85f:1)*Smooth((age-(End-.12f))/.12f));
            if(reduced){g.Tear=Math.Min(g.Tear,.3f);g.Split=Math.Min(g.Split,.35f);g.Scan=Math.Min(g.Scan,.3f);g.Chroma=Math.Min(g.Chroma,.4f);g.Blocks=0;g.Noise=0;}
            g.Tear=Clamp01(g.Tear);g.Split=Clamp01(g.Split);g.Scan=Clamp01(g.Scan);g.Blocks=Clamp01(g.Blocks);g.Black=Clamp01(g.Black);g.Chroma=Clamp01(g.Chroma);g.Noise=Clamp01(g.Noise);
            if(low){g.Blocks=0;g.Scan=0;g.Noise=0;}
            return g;
        }

        /// <summary>
        /// The face and ring through the intro (the hood is hidden until Resolve, NativeSheldonIntroScript.Solid): a flash as
        /// they snap on, the ring clicking into place in four steps (one smooth settle with reduced effects), the glow rising
        /// a touch with each in-breath, the grin widening through the build and flaring into a stuttered grin at Flare. No
        /// random blinks: the stare holds. The face moves; the head never does (NativeSheldonIntroScript.Pose).
        /// </summary>
        public static SheldonFace Face(float age,bool reduced)
        {
            var f=SheldonFace.Rest;float on=age-Resolve;
            f.Glow=1+.06f*Breath(age)+(reduced?.5f:1.4f)*Hit(age,Resolve,.25f,reduced);
            float build=Smooth((age-BuildFrom)/(BuildTo-BuildFrom));f.GrinWide+=.07f*build;f.JawOpen+=.12f*build;f.EyeOpen*=1-.15f*build;f.Glow*=1+.2f*build;
            float k=Smooth((age-Flare)/.08f)*(1-Smooth((age-(Flare+.42f))/.14f));
            float stutter=reduced?.8f:.55f+.45f*Math.Abs((float)Math.Sin((age-Flare)*Math.PI/Stutter));
            f.GrinWide+=.17f*k;f.JawOpen+=.8f*k*stutter;f.EyeLift+=.008f*k;f.EyeOpen*=1-.35f*k;f.Glow*=1+.3f*k;
            if(!reduced)f.Jitter=.003f*k*(float)Math.Sin(age*83);
            f.JawOpen=Math.Min(1.2f,f.JawOpen);
            // The ring: from a quarter turn back and 25% larger to rest over .24 s.
            float u=Clamp01(on/.24f),settle=reduced?Smooth(u):(float)Math.Floor(u*4)/4;
            f.RingSpin=-1.0f*(1-settle)+Math.Max(0,on)*NativeSheldonHead.RingRate*(reduced?NativeSheldonHead.ReducedRate:1);f.RingGrow=.25f*(1-settle);
            return f;
        }

        // ---- the overlay's glitch-typed text ---------------------------------------------------------------------------
        private const string Junk="#%&@$01<>/\\|=+*?!";
        /// <summary>Characters revealed per second as the name types in.</summary>
        public const float TypeRate=22;
        /// <summary>
        /// `text` as shown `since` s after it starts typing: revealed left to right at TypeRate, the next few characters as
        /// junk glyphs that change every glitch step (every reduced step with reduced effects), the rest not yet there. Writes
        /// into `into` (no allocation) and returns the shown length; done when every character is revealed.
        /// </summary>
        public static int Typed(string text,float since,bool reduced,int seed,Span<char> into,out bool done)
        {
            done=false;if(string.IsNullOrEmpty(text)||since<0)return 0;int n=Math.Min(text.Length,into.Length);
            int shown=(int)Math.Floor(since*TypeRate),junk=reduced?2:4,step=Step(since,reduced),count=Math.Min(n,shown+junk);
            for(int i=0;i<count;i++)
            {
                char c=text[i];
                if(i>=shown&&c!=' ')c=Junk[(int)(Hash(step*131+i*7+seed,23)*Junk.Length)%Junk.Length];
                into[i]=c;
            }
            done=shown>=n;return count;
        }
    }

    /// <summary>
    /// The Sheldon Intro's own set (owner 2026-10-12: "Also, a better background for this kind of intro"), after the owner's
    /// picture: a deep navy void over a dark floor etched with circuit traces that run in to a socket round the pilot (the
    /// pilot is the chip: data pulses run in along them, and the beats send waves out), blue horizontal data streaks drifting
    /// at every depth behind the pilot, faint falling code far back, and a large dashed holo ring behind the head, centred on
    /// the hood's own ring from the closing shot. A soft blue glow behind the shoulders (NativeSheldonIntroScript.Effects)
    /// keeps the dark hood reading against the void; the set stays blue and dimmer than the face and ring, so the face keeps
    /// its contrast. Built once with the script from a hash (no textures or assets, primitives only); a frame only reads the
    /// arrays, so drawing it allocates nothing. The tiers draw a prefix of each list (reduced effects fewer streaks, traces,
    /// code and ticks; the low graphics tier fewer still, no code or ticks) and only full effects flash its glitch lines.
    /// Hero frame: +z the pilot's front (the closing shot's axis), +x its left; Draw turns it to the pilot's facing.
    /// </summary>
    public sealed class NativeSheldonSet
    {
        public const int Streaks=150,Traces=18,Columns=12,Glyphs=9,RingTicks=60;
        /// <summary>What each tier draws (full, reduced effects, low graphics): streaks, traces, code columns and ring ticks.</summary>
        public static readonly int[] StreakCount={150,72,44},TraceCount={18,12,8},ColumnCount={12,6,0},TickCount={60,30,0};
        /// <summary>The socket round the pilot's feet (half size) and the floor's colour.</summary>
        public const float Socket=.78f;
        public static readonly Vector3 FloorColor=NativeEntranceFx.Hex(0x060a14);
        /// <summary>The holo ring: centre (hero frame, on the closing shot's line through the hood's ring), radius and the
        /// dash ribbon's width; its arcs (degrees, counter-clockwise from +x) and its slow turn (rad/s).</summary>
        public static readonly Vector3 RingCentre=new Vector3(0,1.64f,-1.9f);
        public const float RingRadius=.86f,RingWidth=.022f,RingTickRadius=.95f,RingRate=.12f;
        public static readonly float[] RingArcs={4,44, 52,64, 72,136, 146,152, 160,214, 224,238, 246,292, 300,308, 316,350};
        // The streaks: radius from the pilot, angle round the back (0 straight behind), height, length, thickness, drift
        // (rad/s), level and colour.
        public readonly float[] StreakR=new float[Streaks],StreakA=new float[Streaks],StreakY=new float[Streaks],StreakLen=new float[Streaks],StreakH=new float[Streaks],StreakRate=new float[Streaks],StreakLevel=new float[Streaks];
        public readonly Vector3[] StreakColor=new Vector3[Streaks];
        // The traces: polylines on the floor (xz), trace i over points [TraceStart[i], TraceStart[i+1]); their length and the
        // speed and phase of the pulse running in along each.
        public readonly Vector2[] TracePoints;public readonly int[] TraceStart=new int[Traces+1];public readonly float[] TraceLength=new float[Traces],TraceSpeed=new float[Traces],TracePhase=new float[Traces];
        // The code columns: radius, angle, fall speed (m/s), phase and each glyph's width.
        public readonly float[] ColumnR=new float[Columns],ColumnA=new float[Columns],ColumnRate=new float[Columns],ColumnPhase=new float[Columns],GlyphWidth=new float[Columns*Glyphs];
        // The ring's dash chunks (angles in radians, at most 6 degrees each).
        public readonly float[] DashFrom,DashTo;
        private static float R(int seed,int i)=>NativeEntranceMath.Rand(seed,i);
        public NativeSheldonSet()
        {
            for(int i=0;i<Streaks;i++)
            {
                float d=R(7001,i);StreakR[i]=3.2f+8*d*d;StreakA[i]=(R(7002,i)*2-1)*1.4f;StreakY[i]=.05f+4.6f*R(7003,i)*R(7003,i+500)+.4f*R(7004,i);
                float l=R(7005,i);StreakLen[i]=.15f+2.4f*l*l*l;StreakH[i]=.006f+.022f*R(7006,i)*R(7006,i+300);
                float v=.15f+1.0f*R(7007,i);StreakRate[i]=(R(7008,i)<.5f?-1:1)*v/StreakR[i];
                float c=R(7009,i);StreakColor[i]=c<.68f?NativeSheldonIntro.Blue:c<.9f?NativeSheldonIntro.Cyan:Vector3.One;float lv=R(7010,i)*R(7010,i+700);StreakLevel[i]=c<.68f?.55f+.9f*lv:c<.9f?.3f+.6f*lv:.2f+.35f*lv;
            }
            // The traces: out from the socket's edges in steps along the eight directions, turning 45 degrees now and then.
            var pts=new Vector2[Traces*9];int n=0;
            for(int i=0;i<Traces;i++)
            {
                TraceStart[i]=n;float a=(i+.5f+.6f*(R(7020,i)-.5f))/Traces*6.2831853f;int dir=(int)Math.Round(a/(Math.PI/4))&7;
                var p=new Vector2((float)Math.Sin(a),(float)Math.Cos(a));p=p/Math.Max(Math.Abs(p.X),Math.Abs(p.Y))*Socket;pts[n++]=p;float len=0;
                int segs=4+(int)(4*R(7021,i));
                for(int s=0;s<segs;s++)
                {
                    if(s>0&&R(7022,i*16+s)<.45f){int turn=R(7023,i*16+s)<.5f?1:7;int nd=(dir+turn)&7;var o=Dir(nd);if(Vector2.Dot(o,p)>0)dir=nd;}
                    float step=.5f+1.1f*R(7024,i*16+s);var q=p+Dir(dir)*step;len+=step;pts[n++]=q;p=q;
                }
                TraceLength[i]=len;TraceSpeed[i]=2.2f+1.8f*R(7025,i);TracePhase[i]=R(7026,i)*20;
            }
            TraceStart[Traces]=n;TracePoints=new Vector2[n];Array.Copy(pts,TracePoints,n);
            for(int i=0;i<Columns;i++)
            {
                ColumnR[i]=5+4*R(7030,i);ColumnA[i]=(i+.5f)/Columns*1.9f-.95f+.08f*(R(7031,i)-.5f);ColumnRate[i]=.45f+.8f*R(7032,i);ColumnPhase[i]=R(7033,i)*7;
                for(int k=0;k<Glyphs;k++)GlyphWidth[i*Glyphs+k]=.03f+.05f*R(7034,i*Glyphs+k);
            }
            int chunks=0;for(int k=0;k+1<RingArcs.Length;k+=2)chunks+=Math.Max(1,(int)Math.Ceiling((RingArcs[k+1]-RingArcs[k])/6));
            DashFrom=new float[chunks];DashTo=new float[chunks];int c0=0;
            for(int k=0;k+1<RingArcs.Length;k+=2)
            {
                float a0=RingArcs[k],a1=RingArcs[k+1];int m=Math.Max(1,(int)Math.Ceiling((a1-a0)/6));
                for(int j=0;j<m;j++){DashFrom[c0]=(a0+(a1-a0)*j/m)*(float)Math.PI/180;DashTo[c0]=(a0+(a1-a0)*(j+1)/m)*(float)Math.PI/180;c0++;}
            }
        }
        private static Vector2 Dir(int d){float a=d*(float)Math.PI/4;return new Vector2((float)Math.Sin(a),(float)Math.Cos(a));}
        /// <summary>A point `s` m in along trace `i` from its outer end (clamped to the trace).</summary>
        public Vector2 Along(int i,float s,out Vector2 heading)
        {
            int a=TraceStart[i],b=TraceStart[i+1]-1;float left=TraceLength[i]-Math.Max(0,s);heading=Vector2.UnitX;
            for(int k=a;k<b;k++){var d=TracePoints[k+1]-TracePoints[k];float l=d.Length();if(left<=l||k==b-1){heading=d/Math.Max(1e-4f,l);return TracePoints[k]+heading*Math.Min(l,Math.Max(0,left));}left-=l;}
            return TracePoints[a];
        }
        /// <summary>
        /// The set at `t`: `power` its light (0 dark .. 1 full; flashes above), `wave` the beats' waves out along the floor
        /// (a function of distance from the pilot, NativeSheldonIntroScript.Wave), `charge` how much of the ring has powered
        /// on (0..1, round from the top), `ring` its extra level (downbeat pulses, the flare), `glitch` this glitch `step`'s
        /// level (only full effects flash lines), `hats` whether the code's glyphs change on the groove's hi-hats, `tier` 0 full,
        /// 1 reduced effects, 2 low graphics. `centre` is the pilot's floor point and `facing` its heading (radians).
        /// </summary>
        public void Draw(INativeEntranceProps props,float t,int tier,float power,Func<float,float> wave,float charge,float ring,float glitch,int step,bool hats,Vector3 centre,float facing)
        {
            var yaw=NativeEntranceMath.Yaw(facing);Vector3 L(float x,float y,float z)=>centre+NativeEntranceMath.RotateY(new Vector3(x,y,z),facing);
            float floorY=NativeEntranceStage.FloorY;
            props.Piece(WeaponShape.Cube,false,new Vector3(centre.X,floorY-.5f,centre.Z),Quaternion.Identity,new Vector3(70,1,70),FloorColor);
            var blue=NativeSheldonIntro.Blue;var cyan=NativeSheldonIntro.Cyan;
            // The socket: a square of traces round the feet, brightest on the waves.
            {
                float w0=wave(Socket),lv=(.25f+.35f*power)*(1+.8f*w0);var c=Vector3.Lerp(blue,cyan,.4f)*lv;
                for(int s=0;s<4;s++){float a=s*(float)Math.PI/2;var q=yaw*NativeEntranceMath.Yaw(a);var mid=L((float)Math.Sin(a)*Socket,floorY+.003f,(float)Math.Cos(a)*Socket);props.Piece(WeaponShape.Cube,true,new Vector3(mid.X,floorY+.003f,mid.Z),q,new Vector3(2*Socket+.02f,.004f,.016f),c);}
            }
            // The traces: faint lines fading out with distance, lit by the waves, each with a pulse running in to the socket.
            int traces=TraceCount[tier];
            for(int i=0;i<traces;i++)
            {
                float run=0;
                for(int k=TraceStart[i];k<TraceStart[i+1]-1;k++)
                {
                    var a=TracePoints[k];var b=TracePoints[k+1];var d=b-a;float l=d.Length();var m=(a+b)*.5f;float dist=m.Length();run+=l;
                    float fade=NativeEntranceMath.Clamp01(1-dist/9.5f);fade*=fade;float lv=(.12f+.28f*power)*fade*(1+1.4f*wave(dist));if(lv<=.004f)continue;
                    var at=L(m.X,0,m.Y);props.Piece(WeaponShape.Cube,true,new Vector3(at.X,floorY+.002f,at.Z),yaw*NativeEntranceMath.Yaw((float)Math.Atan2(d.X,d.Y)),new Vector3(.014f,.004f,l+.014f),blue*lv);
                    if(k==TraceStart[i+1]-2)props.Piece(WeaponShape.Cube,true,new Vector3(L(b.X,0,b.Y).X,floorY+.003f,L(b.X,0,b.Y).Z),yaw,new Vector3(.06f,.005f,.06f),Vector3.Lerp(blue,cyan,.5f)*(lv*1.3f));
                }
                if(power<.05f)continue;
                float s=(t*TraceSpeed[i]+TracePhase[i])%(TraceLength[i]+2.5f);if(s>TraceLength[i])continue;
                var p=Along(i,s,out var h);float dd=p.Length(),f2=NativeEntranceMath.Clamp01(1-dd/9.5f);var pw=L(p.X,0,p.Y);
                props.Piece(WeaponShape.Cube,true,new Vector3(pw.X,floorY+.004f,pw.Z),yaw*NativeEntranceMath.Yaw((float)Math.Atan2(h.X,h.Y)),new Vector3(.018f,.005f,.22f),cyan*(.9f*power*f2));
            }
            // The data streaks: drifting round the back on their circles, fading at the sides.
            int streaks=StreakCount[tier];
            for(int i=0;i<streaks;i++)
            {
                float a=StreakA[i]+StreakRate[i]*t;a=(a+1.4f)%2.8f;if(a<0)a+=2.8f;a-=1.4f;float edge=NativeEntranceMath.Smooth((1.4f-Math.Abs(a))/.3f);float lv=StreakLevel[i]*power*edge;if(lv<=.01f)continue;
                float r=StreakR[i];var at=L(r*(float)Math.Sin(a),StreakY[i],-r*(float)Math.Cos(a));
                props.Piece(WeaponShape.Cube,true,at,yaw*NativeEntranceMath.Yaw(-a),new Vector3(StreakLen[i],StreakH[i],.01f),StreakColor[i]*lv);
            }
            // The glitch lines: long hard lines flashed across the void on glitch steps (full effects only).
            if(tier==0&&glitch>.15f)
            {
                int lines=(int)(4*glitch);
                for(int i=0;i<lines;i++)
                {
                    float h=NativeSheldonIntro.Hash(step*53+i,601),r=3+6*NativeSheldonIntro.Hash(step*53+i,603),a=(NativeSheldonIntro.Hash(step*53+i,605)*2-1)*.7f;if(h>.85f)continue;
                    var c=h<.45f?cyan:h<.7f?blue*1.6f:NativeSheldonIntro.Magenta;var at=L(r*(float)Math.Sin(a),.2f+3.4f*NativeSheldonIntro.Hash(step*53+i,607),-r*(float)Math.Cos(a));
                    props.Piece(WeaponShape.Cube,true,at,yaw*NativeEntranceMath.Yaw(-a),new Vector3(3+6*NativeSheldonIntro.Hash(step*53+i,609),.004f+.01f*NativeSheldonIntro.Hash(step*53+i,611),.01f),c*(.6f+.6f*glitch));
                }
            }
            // The code: faint glyph columns falling far back, the head bright; the glyphs change on the hi-hats (full effects).
            int columns=ColumnCount[tier],hat=(int)Math.Floor((t-NativeSheldonIntro.Resolve)/NativeSheldonIntro.Stutter);
            for(int i=0;i<columns;i++)
            {
                float r=ColumnR[i],a=ColumnA[i],span=6.5f,head=span-((t*ColumnRate[i]+ColumnPhase[i])%span)-.6f;var dir=new Vector3((float)Math.Sin(a),0,-(float)Math.Cos(a));
                for(int k=0;k<Glyphs;k++)
                {
                    float y=head+k*.12f;if(y<floorY+.05f||y>5.4f)continue;float lv=(k==0?1.1f:.5f*(1-k/(float)Glyphs))*power;if(lv<=.01f)continue;
                    float w=GlyphWidth[i*Glyphs+k];if(hats)w=.03f+.05f*NativeSheldonIntro.Hash((i*Glyphs+k)*977+hat,613);
                    var at=L(dir.X*r,y,dir.Z*r);props.Piece(WeaponShape.Cube,true,at,yaw*NativeEntranceMath.Yaw(-a),new Vector3(w,.07f,.01f),(k==0?Vector3.Lerp(cyan,Vector3.One,.4f)*.65f:blue)*lv);
                }
            }
            // The holo ring: dash chunks powering on round from the top through the swell, turning slowly; ticks outside it.
            if(charge>.001f)
            {
                float spin=t*RingRate,lv=(.75f+ring)*Math.Max(.35f,power);var c=Vector3.Lerp(blue,cyan,.45f);
                for(int i=0;i<DashFrom.Length;i++)
                {
                    float a0=DashFrom[i]+spin,a1=DashTo[i]+spin,top=(float)((Math.PI/2-(a0+a1)*.5f)%(2*Math.PI));if(top<0)top+=(float)(2*Math.PI);if(top/(2*Math.PI)>charge)continue;
                    float mid=(a0+a1)*.5f,len=RingRadius*(a1-a0);var at=L(RingCentre.X+RingRadius*(float)Math.Cos(mid),RingCentre.Y+RingRadius*(float)Math.Sin(mid),RingCentre.Z);
                    props.Piece(WeaponShape.Cube,true,at,yaw*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,mid+(float)Math.PI/2),new Vector3(len*.94f,RingWidth,.012f),c*lv);
                }
                int ticks=TickCount[tier];
                for(int i=0;i<ticks;i++)
                {
                    float a=i*6.2831853f/RingTicks-t*RingRate*.5f;if(NativeSheldonIntro.Hash(i,615)<.2f)continue;float top=(float)((Math.PI/2-a)%(2*Math.PI));if(top<0)top+=(float)(2*Math.PI);if(top/(2*Math.PI)>charge)continue;
                    float l=i%5==0?.07f:.035f;var at=L(RingCentre.X+(RingTickRadius+l*.5f)*(float)Math.Cos(a),RingCentre.Y+(RingTickRadius+l*.5f)*(float)Math.Sin(a),RingCentre.Z);
                    props.Piece(WeaponShape.Cube,true,at,yaw*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,a),new Vector3(l,.007f,.008f),blue*(.8f*lv));
                }
            }
        }
    }

    /// <summary>
    /// The Sheldon Intro's choreography (NativeSheldonIntro): pose, camera, lights, set, effects, the hologram and RGB-split
    /// ghosts. From the resolve the pose only breathes: the keys hold, the head holds one world turn (the stare), and the
    /// chest and shoulders rise and fall on the groove with the neck kept where it was (no head travel).
    /// </summary>
    public sealed class NativeSheldonIntroScript:NativeProceduralEntrance
    {
        private const float Slam=NativeSheldonIntro.Slam,Jolt=NativeSheldonIntro.Jolt,Silence=NativeSheldonIntro.Silence,Return=NativeSheldonIntro.Return,Rise=NativeSheldonIntro.Rise,
            SwellFrom=NativeSheldonIntro.SwellFrom,SwellPeak=NativeSheldonIntro.SwellPeak,Resolve=NativeSheldonIntro.Resolve,BuildFrom=NativeSheldonIntro.BuildFrom,Flare=NativeSheldonIntro.Flare,ExitCut=NativeSheldonIntro.Cut;
        /// <summary>When the left hand rises to the hood and arrives (in the rise, so it is in place as the figure resolves).</summary>
        public const float HandUp=2.0f,HandAt=2.33f;
        /// <summary>The stare: the head's turn in the rig frame from the resolve (square to the pilot's facing, the closing
        /// shot's axis; the chin a touch down), and the breath's chest lift, the spine's counter-turn (1.68x the lift: keeps the
        /// neck in place) and the shoulders' rise (radians at a full in-breath; the drapes and shoulder caps rise about a centimetre).</summary>
        public static readonly Quaternion StareTurn=CapturedRig.Euler(.07f,0,0);
        public const float BreathChest=.013f,BreathSpine=.022f,BreathShoulder=.08f;
        private static readonly Vector3 Cy=NativeSheldonIntro.Cyan,Mg=NativeSheldonIntro.Magenta,Bl=NativeSheldonIntro.Blue;
        /// <summary>Reduced effects and the low graphics tier (set by the presentation before it draws; the checks drive both).</summary>
        public bool Reduced,Low;
        /// <summary>The intro's set, built once with the script.</summary>
        public readonly NativeSheldonSet Set=new NativeSheldonSet();
        private readonly float[] stance=new float[8];private readonly Func<float,float> wave;private float waveT;private bool waveReduced;
        private const float NoIdle=1e6f;
        public NativeSheldonIntroScript():base(NativeSheldonIntro.Style,Resolve,0x22e6ff)
        {
            // No slow-motion beat: the clock is the age, so every beat stays on the music.
            SlowRate=1;SlowHold=0;SlowRecover=.1f;SlowCatchUp=.5f;
            Start=Stance(.2f,.34f,.15f,-.17f,-.12f,-.42f);Steps=new[]{Step(false,1.86f,2.12f,-.18f,-.1f,-.42f,.07f),Step(true,2.04f,2.3f,.16f,.07f,.1f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // The landed crouch: left foot forward, right knee down, the head bowed.
            float[] kneel(float drop,float head,float lean)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.Yaw]=-.22f;v[NativeEntranceKeys.BodyY]=drop;v[NativeEntranceKeys.PlantL]=1;v[NativeEntranceKeys.PlantR]=0;
                NativeEntranceKeys.Set(v,B,lean,0,0);NativeEntranceKeys.Set(v,Hd,head,0,0);NativeEntranceKeys.Set(v,LL,-1.4f,0,.15f);NativeEntranceKeys.Set(v,LK,1.9f,0,0);NativeEntranceKeys.Set(v,RL,.22f,0,-.08f);NativeEntranceKeys.Set(v,RK,1.75f,0,0);
                NativeEntranceKeys.Set(v,LA,-.95f,0,.3f);NativeEntranceKeys.Set(v,LE,-1.15f,0,0);NativeEntranceKeys.Set(v,RA,.35f,0,-.35f);NativeEntranceKeys.Set(v,RE,-.4f,0,0);});
            // Rezzed in already landed; settling into it on the slam; still down through the jolt, the gap and the re-entry.
            k.Key(0,kneel(-.34f,.48f,.38f));k.Key(Slam,kneel(-.37f,.55f,.42f)).ImpactKey();k.Key(Jolt,kneel(-.36f,.5f,.4f));
            k.Key(Return,kneel(-.35f,.45f,.37f));k.Key(Rise,kneel(-.34f,.4f,.34f));
            // The rise through the swell: up off the knee, the chin still down, then tall.
            k.Key(1.98f,stand).Root(0,.88f,0,-.22f).Drop(-.15f).J(B,.16f).J(Hd,.3f).J(LA,-.2f,0,.25f).J(LE,-.5f).J(RA,-.15f,0,-.25f).J(RE,-.4f).Planted(1,.5f);
            k.Key(2.2f,stand).Root(0,.88f,0,-.22f).Drop(-.04f).J(B,.04f).J(C,-.03f,-.03f,0).J(Hd,.16f).J(LA,-.5f,0,.45f).J(LE,-1.3f).J(RA,-.12f,0,-.2f).J(RE,-.32f);
            // The stare (held from the resolve: no later key): weight a touch back, the chest turned a little to the raised
            // hand, the right arm loose; the head's turn is StareTurn (Pose), the left arm reaches the hood (Arms).
            k.Key(Resolve,stand).Root(0,.88f,0,-.22f).J(B,-.02f).J(C,-.04f,-.06f,0).J(Hd,.07f,.06f,0).J(LA,-.6f,0,.5f).J(LE,-1.6f).J(RA,-.12f,0,-.2f).J(RE,-.3f);
            Keys=k;wave=Wave;
            Shots=new[]
            {
                // Wide in the dark: the data streams converging on the figure, a band locking on each tick.
                Orbit(0,NativeEntranceCut.Cut,44,42,18,8,4.4f,4.0f,1.1f,1.0f,V(0,.6f,0),V(0,.55f,0),0,0),
                // The slam: low and close on the landed crouch.
                Shot(Slam,NativeEntranceCut.Cut,42,39,V(.9f,.38f,2.65f),V(.72f,.42f,2.4f),V(0,.5f,0),V(0,.58f,0),1,0),
                // The jolt: a slow drift round the side as the signal fails.
                Orbit(Jolt,NativeEntranceCut.Cut,38,36,-42,-32,3.0f,2.85f,.7f,.75f,V(0,.62f,0),V(0,.7f,0),0,0),
                // The re-entry: front on and low, tilting up as the projection rises through the swell.
                Shot(Return,NativeEntranceCut.Cut,40,37,V(.2f,.72f,3.5f),V(.1f,1.0f,3.25f),V(0,.62f,0),V(0,1.12f,0),0,0),
                // The resolve on the downbeat: low and nearly square, the stare meeting the lens.
                Shot(Resolve,NativeEntranceCut.Cut,36,34,V(.42f,.66f,2.95f),V(.32f,.76f,2.7f),V(0,1.22f,0),V(0,1.27f,0),1,0),
                // The push-in: square on the face, to the reference's frame by the hand-off (the whole hood over the chest).
                Shot(BuildFrom,NativeEntranceCut.Cut,34,31,V(-.45f,1.42f,2.35f),V(-.3f,1.47f,1.4f),V(-.02f,1.33f,0),V(-.04f,1.38f,0),0,0),
            };
        }
        public override void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            // The keys without the shared hero idle (its weight shift and head drift would bob the stare), then the stare.
            NativeEntrancePose.Feet(t,Start,Steps,stance);NativeEntrancePose.Keyed(Keys,t,pose,out root,out rotation,stance,NoIdle,RootPath);Stare(t,pose);Arms(t,pose,root,rotation);
        }
        /// <summary>
        /// The stare from the resolve: the breath lifts the chest against the spine (turned so the neck stays put) and raises
        /// the shoulders, and the head is set to StareTurn whatever is under it (eased in over the last .14 s of the rise, while
        /// only the hologram shows it), so nothing reaches it but about a millimetre of the breath's neck travel.
        /// </summary>
        private void Stare(float t,CapturedRig.Pose pose)
        {
            float hold=NativeEntranceMath.Ramp(t,Resolve-.14f,Resolve);if(hold<=0)return;float b=NativeSheldonIntro.Breath(t);
            if(b>0)
            {
                pose.Rotation[Sp]=pose.Rotation[Sp]*CapturedRig.Euler(BreathSpine*b,0,0);pose.Rotation[C]=pose.Rotation[C]*CapturedRig.Euler(-(BreathChest+BreathSpine)*b,0,0);
                float sh=BreathShoulder*b;pose.Rotation[CapturedRig.LClav]=pose.Rotation[CapturedRig.LClav]*CapturedRig.Euler(0,0,sh);pose.Rotation[CapturedRig.RClav]=pose.Rotation[CapturedRig.RClav]*CapturedRig.Euler(0,0,-sh);
                // The arms ride the shoulders up but keep hanging as they were.
                pose.Rotation[CapturedRig.LArm]=CapturedRig.Euler(0,0,-sh)*pose.Rotation[CapturedRig.LArm];pose.Rotation[CapturedRig.RArm]=CapturedRig.Euler(0,0,sh)*pose.Rotation[CapturedRig.RArm];
            }
            pose.World();var head=hold>=1?StareTurn:Quaternion.Slerp(pose.WorldRotation[Hd],StareTurn,hold);
            pose.Rotation[Hd]=Quaternion.Normalize(Quaternion.Inverse(pose.WorldRotation[CapturedRig.Neck])*head);
        }
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            // The left hand on the hood's front edge at the temple, the fingers over the brim (the owner's reference).
            float w=NativeEntranceMath.Ramp(t,HandUp,HandAt);if(w<=.001f)return;pose.World();
            var head=NativeEntrancePose.WorldRotation(pose,rotation,CapturedRig.Head);
            var target=NativeEntrancePose.Point(pose,root,rotation,CapturedRig.Head,HandOnHood);
            NativeEntrancePose.Reach(pose,root,rotation,true,target,head*HandTurn,w);
        }
        /// <summary>The wrist on the hood (head space, source frame: +x the rig's left) and its turn (fingers up over the brim).</summary>
        public static readonly Vector3 HandOnHood=new Vector3(.2f,.17f,.12f);
        public static readonly Quaternion HandTurn=CapturedRig.Euler(-2.5f,.3f,-.5f);
        // Glitch level in the scene (the fragments, streaks, lines and ghosts follow the screen's tear and split).
        private float Level(float t){var g=NativeSheldonIntro.Glitch(t,Reduced,false);return Math.Max(g.Tear,g.Split)*(1-g.Black);}
        private bool On(int bone,float t,float chance){int s=NativeSheldonIntro.Step(t,false);return NativeSheldonIntro.Hash(s*29+bone,31)<chance;}
        /// <summary>The rez-in band of a bone, locked on that tick (NativeSheldonIntro.Ticks): the legs on the second, the hips,
        /// waist, forearms and hands on the third, the chest, shoulders and upper arms on the slam; the head (the hood) waits for
        /// the resolve. The first tick draws only the outline (Holo).</summary>
        public static int Band(int bone)
        {
            if(bone>=CapturedRig.LHip)return 1;
            switch(bone){case CapturedRig.Body:case CapturedRig.Pelvis:case CapturedRig.Spine:case CapturedRig.LElbow:case CapturedRig.LHand:case CapturedRig.RElbow:case CapturedRig.RHand:return 2;case CapturedRig.Head:return 4;}
            return 3;
        }
        public override float Solid(float t,int bone)
        {
            if(t>=Resolve)return 1;
            if(t>=Silence&&t<Return)return 0;
            bool head=bone==CapturedRig.Head;
            // The projection from the re-entry: dithered and charging through the swell (glitch drop-outs with full effects),
            // the head (and so the hood) not yet there.
            if(t>=Return){if(head)return 0;float p=.35f+.4f*NativeEntranceMath.Ramp(t,SwellFrom,Resolve);return Reduced||On(bone,t,.88f)?p:p*.4f;}
            int band=Band(bone);
            if(head)return Reduced||t<Slam?0:On(bone,t,.25f)?1:0;// never the bare pilot's head: missing, or the hood flashing in on a glitch step
            float lockAt=NativeSheldonIntro.Ticks[band];if(t<lockAt)return 0;
            // Locked on its tick (reduced: faded in over .12 s, then steady); from the slam whole bones drop out on glitch steps,
            // more of them as the signal fails into the gap.
            if(Reduced)return .6f*NativeEntranceMath.Smooth((t-lockAt)/.12f);
            if(t<Slam+.04f)return 1;
            float chance=.9f-.35f*NativeEntranceMath.Ramp(t,Jolt,Silence);return On(bone,t,chance)?1:.25f;
        }
        public override bool Holo(float t,out Vector3 color,out float opacity,out Vector3 offset)
        {
            // The outline from the first tick (a step brighter on each, gone as the slam locks the last band), then the
            // projection from the re-entry, brightening through the swell, gone as the figure resolves.
            float outline=t>=NativeSheldonIntro.FirstTick&&t<Slam+.08f?(.2f+.05f*NativeSheldonIntro.Rezzed(t,Reduced))*(1-NativeEntranceMath.Ramp(t,Slam-.01f,Slam+.08f)):0;
            float proj=t>=Return&&t<Resolve+.05f?(.34f+.14f*NativeEntranceMath.Ramp(t,SwellFrom,SwellPeak))*(1-NativeEntranceMath.Ramp(t,Resolve-.02f,Resolve+.05f)):0;
            float a=Math.Max(outline,proj);color=Cy*1.4f;
            int s=NativeSheldonIntro.Step(t,Reduced);offset=!Reduced&&NativeSheldonIntro.Hash(s,41)>.75f?Local(NativeEntranceMath.Signed(4111,s)*.06f,0,0):Vector3.Zero;
            opacity=a*(Reduced?1:.75f+.25f*NativeSheldonIntro.Hash(s,43));return opacity>.01f;
        }
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            // The RGB split in the scene: magenta and cyan copies pulled apart on the glitch hits.
            // Only on the strong hits, and only in the wide shots (in the close push-in a copy this near reads as a tint, so the
            // screen pass alone splits the frame there): a split copy, not a tint over the figure. Never over the outline or
            // the projection (each copy's depth pass would hide the hologram behind it).
            if(t<Slam+.2f||t>=Silence&&t<Resolve+.05f||t>=BuildFrom||into.Length<2)return 0;float k=Level(t);if(k<.3f)return 0;
            int s=NativeSheldonIntro.Step(t,Reduced);float jag=Reduced?1:.6f+.8f*NativeSheldonIntro.Hash(s,47),apart=(Reduced?.025f:.1f)*k*jag,op=(Reduced?.16f:.3f)*NativeEntranceMath.Smooth((k-.25f)/.35f);
            into[0]=new NativeEntranceGhost{Lag=0,Opacity=op,Color=Mg,Offset=Local(apart,0,0)};
            into[1]=new NativeEntranceGhost{Lag=Reduced?0:.03f,Opacity=op,Color=Cy,Offset=Local(-apart,0,.01f)};
            return 2;
        }
        /// <summary>No solid set pieces: the void has nothing the camera could enter.</summary>
        public override int Boxes(float t,Span<Vector4> into)=>0;
        /// <summary>How lit the set is: dark in the lead-in, a step up on each tick (one smooth rise with reduced effects), full
        /// on the slam, failing into the gap, dim from the re-entry, charging through the swell, full from the resolve, with
        /// a flash on the slam and the resolve.</summary>
        public static float Power(float t,bool reduced)
        {
            float p;
            if(t<Slam)p=.1f+.12f*NativeSheldonIntro.Rezzed(t,reduced);
            else if(t<Return)p=.7f-.35f*NativeEntranceMath.Ramp(t,Jolt,Silence);
            else if(t<Resolve)p=.35f+.35f*NativeEntranceMath.Ramp(t,SwellFrom,SwellPeak);
            else p=1;
            float attack=reduced?.08f:.01f;return p+.5f*NativeEntranceMath.Pulse(t,Slam,attack,.2f)+.6f*NativeEntranceMath.Pulse(t,Resolve,attack,.15f);
        }
        /// <summary>The beats' waves out along the floor at distance `d` m from the pilot at the time Props set (the slam, the
        /// re-entry and the resolve strong, each later downbeat soft): a smooth band travelling out at 7 m/s.</summary>
        private float Wave(float d)
        {
            float t=waveT-d/7,w=1.2f*NativeEntranceMath.Bump(t,Slam,Slam+.35f)+.6f*NativeEntranceMath.Bump(t,Return,Return+.35f)+1.4f*NativeEntranceMath.Bump(t,Resolve,Resolve+.4f);
            for(int i=1;i<NativeSheldonIntro.Downbeats.Length;i++)w+=(waveReduced?.2f:.35f)*NativeEntranceMath.Bump(t,NativeSheldonIntro.Downbeats[i],NativeSheldonIntro.Downbeats[i]+.3f);
            return w;
        }
        /// <summary>The ring's charge (0 until the swell, round to full by the resolve) and its extra level on the downbeats and the flare.</summary>
        private float RingLevel(float t)
        {
            float r=0;for(int i=1;i<NativeSheldonIntro.Downbeats.Length;i++)r+=(Reduced?.12f:.25f)*NativeEntranceMath.Pulse(t,NativeSheldonIntro.Downbeats[i],.01f,.18f);
            float flare=Reduced?.8f*NativeEntranceMath.Pulse(t,Flare,.08f,.3f):NativeEntranceMath.Pulse(t,Flare,.01f,.09f)+.8f*NativeEntranceMath.Pulse(t,Flare+NativeSheldonIntro.Stutter,.01f,.09f)+.6f*NativeEntranceMath.Pulse(t,Flare+2*NativeSheldonIntro.Stutter,.01f,.09f);
            return r+.6f*flare+.3f*NativeEntranceMath.Pulse(t,Resolve,.01f,.3f);
        }
        /// <summary>The set alone at `t` (Props draws it under the fragments): the tier from the effects setting and the low graphics tier.</summary>
        public void DrawSet(float t,INativeEntranceProps props,float q)
        {
            int tier=Low?2:Reduced||q<.5f?1:0;waveT=t;waveReduced=Reduced;float charge=t>=Resolve?1:NativeEntranceMath.Ramp(t,SwellFrom,Resolve);
            float level=t>=Silence&&t<Return||Reduced?0:Level(t);
            Set.Draw(props,t,tier,Power(t,Reduced),wave,charge,RingLevel(t),level,NativeSheldonIntro.Step(t,false),!Reduced&&t>=Resolve,Vector3.Zero,Facing);
        }
        /// <summary>The time of the last key: the stare's pose is held from it (Pose adds only the breath).</summary>
        public float KeysEnd=>Keys.End;
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            DrawSet(t,props,q);float level=t>=Silence&&t<Return?0:Level(t);
            // Flickering fragments: thin glowing slabs round the pilot, re-dealt on every glitch step.
            if(level<(t>=Resolve+.3f?.3f:.05f))return;int s=NativeSheldonIntro.Step(t,Reduced),n=(int)(18*Math.Max(.25f,q)*Math.Min(1,level*1.5f)*(Low?.5f:1));var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);
            for(int i=0;i<n;i++)
            {
                float h=NativeSheldonIntro.Hash(s*61+i,59);if(h>.85f)continue;float a=NativeSheldonIntro.Hash(s*61+i,61)*6.2832f,r=.45f+.9f*NativeSheldonIntro.Hash(s*61+i,67),y=.35f+1.7f*NativeSheldonIntro.Hash(s*61+i,71);
                var p=new Vector3(pelvis.X+(float)Math.Sin(a)*r,y,pelvis.Z+(float)Math.Cos(a)*r);float len=.04f+.32f*NativeSheldonIntro.Hash(s*61+i,73);
                var c=h<.4f?Cy:h<.6f?Bl:h<.72f?Mg:Vector3.One;props.Piece(WeaponShape.Cube,true,p,NativeEntranceMath.Yaw(a),new Vector3(len,.012f+.03f*NativeSheldonIntro.Hash(s*61+i,79),.012f),c*(1.2f+1.6f*level));
            }
        }
        // ---- the data streams: built once (time, travel, side, target anchor and its offset, start distance, colour) ----
        private struct Stream{public float At,Travel,Side,From,Width;public NativeEntranceAnchor Target;public Vector3 Offset,Color;}
        private static readonly Stream[] streams=MakeStreams();
        private static Stream[] MakeStreams()
        {
            var list=new System.Collections.Generic.List<Stream>();int seed=0;
            void Add(float at,NativeEntranceAnchor target,int count,float spread)
            {
                for(int i=0;i<count;i++,seed++)
                {
                    float c=NativeEntranceMath.Rand(8100,seed);
                    list.Add(new Stream{At=at,Travel=.26f+.16f*NativeEntranceMath.Rand(8101,seed),Side=seed%2==0?1:-1,From=3.5f+3*NativeEntranceMath.Rand(8102,seed),Width=.008f+.012f*NativeEntranceMath.Rand(8103,seed),Target=target,
                        Offset=new Vector3(NativeEntranceMath.Signed(8104,seed)*spread,NativeEntranceMath.Signed(8105,seed)*spread,NativeEntranceMath.Signed(8106,seed)*spread*.5f),Color=c<.55f?Cy:c<.85f?Bl:Vector3.One});
                }
            }
            var t=NativeSheldonIntro.Ticks;
            // Each tick's band fed by streams arriving on it (the first draws the outline: they hit the whole figure).
            Add(t[0],NativeEntranceAnchor.Pelvis,3,.2f);Add(t[0],NativeEntranceAnchor.Chest,3,.2f);
            Add(t[1],NativeEntranceAnchor.LeftKnee,2,.06f);Add(t[1],NativeEntranceAnchor.RightKnee,2,.06f);Add(t[1],NativeEntranceAnchor.LeftFoot,2,.05f);Add(t[1],NativeEntranceAnchor.RightFoot,1,.05f);
            Add(t[2],NativeEntranceAnchor.Pelvis,3,.1f);Add(t[2],NativeEntranceAnchor.LeftHand,2,.05f);Add(t[2],NativeEntranceAnchor.RightHand,2,.05f);
            Add(t[3],NativeEntranceAnchor.Chest,4,.12f);Add(t[3],NativeEntranceAnchor.LeftShoulder,2,.05f);Add(t[3],NativeEntranceAnchor.RightShoulder,2,.05f);
            Add(Jolt,NativeEntranceAnchor.Chest,3,.2f);
            // Through the re-entry and the swell a thickening feed into the upper body, all converging on the resolve.
            for(int i=0;i<16;i++){float u=i/16f;Add(Return+.1f+(Resolve-Return-.25f)*(1-(1-u)*(1-u)),i%3==0?NativeEntranceAnchor.Head:i%3==1?NativeEntranceAnchor.Chest:NativeEntranceAnchor.Pelvis,1,.15f);}
            Add(Resolve,NativeEntranceAnchor.Head,5,.12f);Add(Resolve,NativeEntranceAnchor.Chest,4,.18f);Add(Resolve,NativeEntranceAnchor.LeftShoulder,2,.05f);Add(Resolve,NativeEntranceAnchor.RightShoulder,2,.05f);
            return list.ToArray();
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            if(t>=Resolve)Motes(fx,t,q,Cy,977);var right=RightOf(t);var fwd=ForwardOf(t);var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);var spot=new Vector3(pelvis.X,NativeEntranceStage.FloorY,pelvis.Z);
            bool dark=t>=Silence&&t<Return;float power=Power(t,Reduced);
            // The soft volumetric glow behind the shoulders and a wider one low behind the pilot (the hood's silhouette against the void).
            if(!dark&&t>=NativeSheldonIntro.FirstTick-.05f){fx.Glow(Anchor(NativeEntranceAnchor.Chest,t)-fwd*1.1f+new Vector3(0,.3f,0),Bl*(.045f*power),0,1.7f,1,0,9400);if(q>=.5f&&!Low)fx.Glow(spot-fwd*1.8f+new Vector3(0,.6f,0),Bl*(.018f*power),0,3.0f,1,0,9401);}
            // The data streams: horizontal streaks racing in from both sides along the pilot's right, accelerating into their
            // target, which flashes as they land.
            for(int i=0;i<streams.Length;i++)
            {
                var st=streams[i];float a=t-(st.At-st.Travel);if(a<0||a>st.Travel+.1f||st.At>=Silence&&st.At<Return)continue;if(NativeEntranceMath.Rand(8110,i)>Math.Max(.3f,q))continue;
                var target=Anchor(st.Target,st.At)+st.Offset;
                if(a<st.Travel)
                {
                    float u=a/st.Travel,x=st.From*(1-u*u),speed=2*st.From*u/st.Travel;var p=target+right*(st.Side*(.12f+x))+new Vector3(0,(1-u)*st.Offset.Y*2,0);
                    fx.Streak(p,-right*(st.Side*speed),st.Color*(1.6f+1.4f*u),st.Width,.25f,.035f,.9f,u,8200+i);
                }
                else{float k=1-(a-st.Travel)/.1f;fx.Glow(target,Vector3.Lerp(st.Color,Vector3.One,.5f)*(1.3f*k),0,.08f+.05f*k,0,0,8300+i);}
            }
            // The rez steps: a teleport column on the first tick and the slam, and a scan ring round each band as it locks.
            for(int i=0;i<NativeSheldonIntro.Ticks.Length;i++)
            {
                float at=NativeSheldonIntro.Ticks[i],k=NativeEntranceMath.Pulse(t,at,Reduced?.05f:.008f,Reduced?.09f:.07f);if(k<.02f)continue;
                if(i==0||i==3){float h=i==3?3.4f:2.8f,g=i==3?1.15f:1;fx.Streak(spot,new Vector3(0,-h,0),Vector3.One*(3*k*g),.035f*g,h,1,0,0,8400+i);fx.Streak(spot,new Vector3(0,-h,0),Cy*(.9f*k*g),.22f*g,h,1,0,0,8410+i);fx.Glow(spot+new Vector3(0,.1f,0),Cy*(1.2f*k*g),0,.8f*g,1,0,8420+i);}
                if(i==0)continue;
                float y=i==1?(Anchor(NativeEntranceAnchor.LeftKnee,at).Y+Anchor(NativeEntranceAnchor.RightKnee,at).Y)*.5f:i==2?Anchor(NativeEntranceAnchor.Pelvis,at).Y:Anchor(NativeEntranceAnchor.Chest,at).Y;
                ScanRing(fx,new Vector3(pelvis.X,y,pelvis.Z),.42f,Vector3.Lerp(Cy,Vector3.One,.4f)*(2.4f*k),q,8500+i*20);
            }
            // Scanline streaks sweeping across the pilot's front, re-dealt on every glitch step.
            // In the stare only the build and the flare reach the figure (the face stays clear).
            float lv=Level(t);
            if(lv>(t>=Resolve+.3f?.3f:.05f)&&!dark)
            {
                int s=NativeSheldonIntro.Step(t,Reduced),n=q<.5f?4:9;
                for(int i=0;i<n;i++)
                {
                    float y=NativeEntranceStage.FloorY+.15f+1.85f*NativeSheldonIntro.Hash(s*37+i,83),w=.4f+1.4f*NativeSheldonIntro.Hash(s*37+i,89),x=NativeEntranceMath.Signed(s*37+i,97)*.4f;
                    var p=new Vector3(pelvis.X,y,pelvis.Z)+fwd*.35f+right*(x+w*.5f);var c=NativeSheldonIntro.Hash(s*37+i,101)>.5f?Cy:Mg;
                    fx.Streak(p,right*w,c*(1.6f*lv),.006f+.01f*NativeSheldonIntro.Hash(s*37+i,103),0,1,.1f,0,9100+i);
                }
            }
            // The slam: a burst of cyan sparks and a white flash at the crouch.
            NativeEntranceFx.Sparks(fx,t,Slam,spot+new Vector3(0,.08f,0),new Vector3(0,.6f,0),1.2f,28,3.6f,.7f,9.8f,.8f,Vector3.One*3,Cy*2,.018f,9200,q,NativeEntranceStage.FloorY);
            float pop=NativeEntranceMath.Pulse(t,Slam,.01f,.12f);if(pop>.02f)fx.Glow(spot+new Vector3(0,.4f,0),Cy*(1.6f*pop),0,1.0f*pop,1,0,9240);
            // The re-entry hit: the projection beamed back in (a fainter column).
            float back=NativeEntranceMath.Pulse(t,Return,Reduced?.05f:.008f,.08f);if(back>.02f)fx.Streak(spot,new Vector3(0,-2.6f,0),Cy*(1.6f*back),.12f,2.6f,1,0,0,9245);
            // The resolve: a compile sweep down the body, a flash on the face and the ring's snap.
            var head=Anchor(NativeEntranceAnchor.Head,t);float snap=NativeEntranceMath.Pulse(t,Resolve,.01f,.18f);
            float sweep=(t-Resolve)/.16f;if(sweep>=0&&sweep<1)ScanRing(fx,new Vector3(pelvis.X,1.9f-1.8f*sweep,pelvis.Z),.4f,Vector3.Lerp(Cy,Vector3.One,.6f)*(2.8f*(1-.5f*sweep)),q,9500);
            if(snap>.02f){fx.Glow(head+fwd*.12f,Cy*(3*snap),0,.6f*snap+.15f,1,0,9250);fx.Glow(head-fwd*.3f,Cy*(1.6f*snap),0,.9f*snap+.2f,0,0,9251);}
            NativeEntranceFx.Sparks(fx,t,Resolve,head-fwd*.3f,Vector3.Zero,1,20,2.2f,.5f,2,1.6f,Vector3.One*2.5f,Cy*2,.012f,9260,q,NativeEntranceStage.FloorY);
            // The flare: the grin's light blooming out of the hood on each stutter.
            float flare=NativeEntranceMath.Pulse(t,Flare,.01f,.09f)+.8f*NativeEntranceMath.Pulse(t,Flare+NativeSheldonIntro.Stutter,.01f,.09f)+.6f*NativeEntranceMath.Pulse(t,Flare+2*NativeSheldonIntro.Stutter,.01f,.09f);
            if(Reduced)flare=.8f*NativeEntranceMath.Pulse(t,Flare,.08f,.3f);
            if(flare>.02f)fx.Glow(head+fwd*.18f-new Vector3(0,.05f,0),Cy*(1.0f*flare),0,.16f+.12f*flare,1,0,9280);
            // The exit: the stage dissolving into horizontal shards.
            float exit=NativeEntranceMath.Ramp(t,ExitCut,NativeSheldonIntro.End);
            if(exit>.02f){int s=NativeSheldonIntro.Step(t,Reduced),n=q<.5f?5:12;for(int i=0;i<n;i++){float y=NativeEntranceStage.FloorY+.2f+2.2f*NativeSheldonIntro.Hash(s*43+i,107);var p=new Vector3(pelvis.X,y,pelvis.Z)+fwd*.6f+right*(NativeEntranceMath.Signed(s*43+i,109)*1.2f);fx.Streak(p,right*(1.5f*exit),Vector3.One*(2*exit),.02f,0,1,0,0,9300+i);}}
        }
        // A ring of short tangent streaks round the body at a height (a band locking, the compile sweep).
        private static void ScanRing(INativeEntranceFx fx,Vector3 centre,float radius,Vector3 color,float q,int seed)
        {
            int n=q<.5f?6:12;
            for(int i=0;i<n;i++){float a=i*6.2831853f/n,c=(float)Math.Cos(a),s=(float)Math.Sin(a);var p=centre+new Vector3(c*radius,0,s*radius);fx.Streak(p,new Vector3(-s,0,c)*(radius*.5f),color,.012f,0,1,.6f,0,seed+i);}
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            bool dark=t>=Silence&&t<Return;float on=NativeEntranceMath.Ramp(t,Resolve-.02f,Resolve),power=Power(t,Reduced);
            NativeEntranceStage.Rig(l,dark?0:.1f+.28f*on,Bl,dark?0:.35f+.75f*on,0,dark?.05f:.25f+.35f*on);
            l.Background=NativeSheldonIntro.Navy;l.Fog=l.Background;l.FogNear=6;l.FogFar=24;if(dark)return;
            var head=Anchor(NativeEntranceAnchor.Head,t);var fwd=ForwardOf(t);
            // The set's blue behind the shoulders, rimming the hood.
            l.Add(Anchor(NativeEntranceAnchor.Chest,t)-fwd*1.0f+new Vector3(0,.5f,0),Bl*(2.6f*Math.Min(1.2f,power)),4.5f);
            // The face's own light on the hood's opening and the chest once it is on.
            if(t>=Resolve){var f=NativeSheldonIntro.Face(t,Reduced);l.Add(head+fwd*.4f,Cy*(.8f*f.Glow),2.2f);}
            float slam=NativeEntranceMath.Pulse(t,Slam,.01f,.16f),hit=NativeEntranceMath.Pulse(t,Resolve,.01f,.2f),flare=NativeEntranceMath.Pulse(t,Flare,.01f,.16f),tick=0;
            for(int i=0;i<3;i++)tick+=NativeEntranceMath.Pulse(t,NativeSheldonIntro.Ticks[i],Reduced?.05f:.01f,.08f);
            var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);
            if(tick>.01f)l.Add(pelvis+new Vector3(0,.5f,.4f),Cy*(5*tick),4);
            if(slam>.01f)l.Add(pelvis+new Vector3(0,.4f,.4f),Cy*(10*slam),6);
            if(hit>.01f)l.Add(head+fwd*.8f+new Vector3(0,.2f,0),NativeEntranceFx.Hex(0xe6fdff,24*hit),6);
            if(flare>.01f)l.Add(head+fwd*.5f,Cy*(3*flare),4);
            // Glitch flicker of the key between the slam and the gap (none with reduced effects).
            if(!Reduced&&t>Slam&&t<Silence&&NativeSheldonIntro.Hash(NativeSheldonIntro.Step(t,false),113)>.8f)l.Add(head+fwd*1.2f,Mg*4,5);
        }
        protected override void Plan()=>Blasts=new[]{new NativeEntranceBlast{At=Slam,Style=NativeBlastStyle.Quake,Position=new Vector3(0,NativeEntranceStage.FloorY+.9f,0),Radius=1.3f,Size=.4f}};
    }
}
