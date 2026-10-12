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
    /// The Sheldon Intro (DEV entrance "sheldon-intro", owner 2026-10-12): a heavy glitch arrival, a blackout on the music's
    /// digital silence, the pilot resolving in the Sheldon Head look (hood, glowing face and broken ring, worn for the intro
    /// whatever hat is equipped), a hand raised to the hood, a slow push-in on the face while the name card builds and the grin
    /// flaring into a stuttered laugh on the last hit, then a final glitch cut. Every beat sits on its music
    /// (intro.sheldon-intro, NativeSoundBank; default clip "glitch-dropout"): the marks below are seconds from the intro start.
    /// It keeps the standard timing (4.8 s, name card 3.2 s) and the presentation clock is the age (no slow-motion beat), so
    /// the marks stay on the music. Presentation only: the match sees the style id and its unchanged duration; DEV access is
    /// checked where every pick is (NativeCosmeticGate, NativeIntroRules.Style). Reduced effects keep every beat but never
    /// strobe (soft glitches stepping at 6 Hz, ramped blackouts, no blocks or specks); the low graphics tier runs one shader
    /// stage and fewer set pieces. Everything is a pure function of the age.
    /// </summary>
    public static class NativeSheldonIntro
    {
        public const string Style=NativeCosmeticGate.SheldonIntro;
        /// <summary>The big slam: the arrival's glitch tear.</summary>
        public const float Slam=.49f;
        /// <summary>The music starts dipping (the glitch thickens, the frame darkens).</summary>
        public const float Dip=1.43f;
        /// <summary>Near-total digital silence: the blackout (a cut frame).</summary>
        public const float Silence=1.90f,SilenceEnd=1.99f;
        /// <summary>The soft re-entry: a dim, flickering projection of the pilot.</summary>
        public const float Return=2.00f;
        /// <summary>The hard re-entry hit: the figure resolves; hood, face and ring snap on.</summary>
        public const float Resolve=2.37f;
        /// <summary>The dip under the name card (the name types in, the grin builds; the brief chromatic aberration).</summary>
        public const float BuildFrom=3.36f,BuildTo=3.76f;
        /// <summary>The last hit: the grin flares, a stuttered glitch laugh, then the exit cut.</summary>
        public const float Flare=4.24f;
        /// <summary>The final glitch cut, to black at the hand-off.</summary>
        public const float Cut=4.6f,End=4.8f;
        /// <summary>The three laugh stutters after Flare (s apart: the face's kill-laugh cadence, NativeSheldonHead.Evaluate).</summary>
        public const float Stutter=.1333f;
        public static readonly float[] Beats={Slam,Dip,Silence,SilenceEnd,Return,Resolve,BuildFrom,BuildTo,Flare};
        /// <summary>Glitch steps per second (the pattern holds between steps): full and reduced effects.</summary>
        public const float StepRate=24,ReducedStepRate=6;
        public static readonly Vector3 Cyan=NativeEntranceFx.Hex(0x22e6ff),Magenta=NativeEntranceFx.Hex(0xff3dcf);
        /// <summary>The intro's whole look (the owner's reference): the suit in the hood's dark fabric and its accents in the glow
        /// colour (the pilot's hat colour, cyan on Auto), over the Sheldon Head. The pilot's own colours return with the match.</summary>
        public const string Suit="#1c2132";
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

        /// <summary>The full-screen glitch at intro age `age`.</summary>
        public static NativeSheldonGlitch Glitch(float age,bool reduced,bool low)
        {
            var g=new NativeSheldonGlitch{Seed=Step(age,reduced),Passes=low?1:3};
            if(!(age>=0)||age>End+.25f)return g;
            int step=g.Seed;float burst=0;
            // Between the hits: short bursts on random steps (none with reduced effects: the bed stays soft and steady).
            if(!reduced&&(age>Slam&&age<Silence||age>Return&&age<Resolve))burst=Hash(step,17)>.7f?.35f+.4f*Hash(step,19):0;
            // The arrival: noise building out of the dark to the slam.
            float pre=1-Smooth((age-Slam)/.05f),build=Smooth(age/Slam);
            g.Scan=.2f+.3f*build*pre;g.Split=.12f*build*pre;g.Black=.6f*(1-Smooth(age/.42f));g.Noise=.25f*build*pre;
            float slam=Hit(age,Slam,.28f,reduced);g.Tear+=slam;g.Split+=slam;g.Blocks+=.9f*slam;g.Chroma+=.6f*slam;g.Noise+=.4f*slam;
            if(age>Slam&&age<Silence){g.Tear+=.12f+burst;g.Split+=.15f+.6f*burst;g.Scan+=.15f;g.Blocks+=.5f*burst;}
            // The dip: thicker glitch and a darkening frame into the silence.
            float dip=Smooth((age-Dip)/(Silence-Dip))*(age<Silence?1:0);g.Tear+=.45f*dip;g.Split+=.45f*dip;g.Scan+=.35f*dip;g.Blocks+=.45f*dip;g.Black+=.35f*dip;
            // The silence: a cut to black with a few dead pixels (reduced: a ramped fade, no specks).
            if(reduced)g.Black=Math.Max(g.Black,.9f*Smooth((age-(Silence-.1f))/.14f)*(1-Smooth((age-SilenceEnd)/.14f)));
            else if(age>=Silence&&age<Return){g.Black=1;g.Tear=g.Split=g.Blocks=g.Scan=0;g.Noise=.12f;}
            // The soft re-entry: the frame comes back dim and scanlined.
            if(age>=Return&&age<Resolve){float u=(age-Return)/(Resolve-Return);g.Black=Math.Max(g.Black,.55f-.3f*u);g.Scan+=.45f;g.Split+=.3f+.3f*burst;g.Tear+=.15f+burst;}
            else if(reduced&&age>=Resolve)g.Black=Math.Max(g.Black,.25f*(1-Smooth((age-Resolve)/.14f)));// reduced: the dim frame lifts, never cuts
            // The hard re-entry: everything snaps in on a torn, split frame.
            float resolve=Hit(age,Resolve,.3f,reduced);g.Tear+=.85f*resolve;g.Split+=resolve;g.Chroma+=resolve;g.Blocks+=.6f*resolve;
            if(age>=Resolve)g.Scan+=.08f;
            // The build: the push-in's brief chromatic aberration and a dip in the light.
            float b=age>BuildFrom&&age<BuildTo+.25f?(float)Math.Pow(Math.Sin(Math.PI*(age-BuildFrom)/(BuildTo+.25f-BuildFrom)),2):0;
            g.Chroma+=.7f*b;g.Split+=.25f*b;g.Scan+=.2f*b;g.Black+=.12f*b;
            // The flare: three laugh stutters (one soft swell with reduced effects).
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
        /// they snap on, the ring clicking into place in four steps (one smooth settle with reduced effects), the grin widening
        /// through the build and flaring into a stuttered laugh at Flare. No random blinks: the stare holds.
        /// </summary>
        public static SheldonFace Face(float age,bool reduced)
        {
            var f=SheldonFace.Rest;float on=age-Resolve;
            f.Glow=1+.04f*(float)Math.Sin(age*3.4f)+(reduced?.5f:1.4f)*Hit(age,Resolve,.25f,reduced);
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

    /// <summary>The Sheldon Intro's choreography (NativeSheldonIntro): pose, camera, lights, set, effects and RGB-split ghosts.</summary>
    public sealed class NativeSheldonIntroScript:NativeProceduralEntrance
    {
        private const float Slam=NativeSheldonIntro.Slam,Dip=NativeSheldonIntro.Dip,Silence=NativeSheldonIntro.Silence,Return=NativeSheldonIntro.Return,Resolve=NativeSheldonIntro.Resolve,
            BuildFrom=NativeSheldonIntro.BuildFrom,Flare=NativeSheldonIntro.Flare,ExitCut=NativeSheldonIntro.Cut;
        /// <summary>When the hand reaches the hood and how long it holds there.</summary>
        public const float HandUp=2.55f,HandAt=2.95f;
        private static readonly Vector3 Cy=NativeSheldonIntro.Cyan,Mg=NativeSheldonIntro.Magenta;
        /// <summary>Reduced effects (set by the presentation before it draws; the checks drive both).</summary>
        public bool Reduced;
        public NativeSheldonIntroScript():base(NativeSheldonIntro.Style,Resolve,0x22e6ff)
        {
            // No slow-motion beat: the clock is the age, so every beat stays on the music.
            SlowRate=1;SlowHold=0;SlowRecover=.1f;SlowCatchUp=.5f;
            Start=Stance(.2f,.34f,.15f,-.17f,-.12f,-.42f);Steps=new[]{Step(false,2.02f,2.32f,-.18f,-.1f,-.42f,.07f),Step(true,2.2f,2.45f,.16f,.07f,.1f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // The arrival crouch: left foot forward, right knee down, a hand on the floor, the head bowed.
            float[] kneel(float drop,float head,float lean)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.Yaw]=-.22f;v[NativeEntranceKeys.BodyY]=drop;v[NativeEntranceKeys.PlantL]=1;v[NativeEntranceKeys.PlantR]=0;
                NativeEntranceKeys.Set(v,B,lean,0,0);NativeEntranceKeys.Set(v,Hd,head,0,0);NativeEntranceKeys.Set(v,LL,-1.4f,0,.15f);NativeEntranceKeys.Set(v,LK,1.9f,0,0);NativeEntranceKeys.Set(v,RL,.22f,0,-.08f);NativeEntranceKeys.Set(v,RK,1.75f,0,0);
                NativeEntranceKeys.Set(v,LA,-.95f,0,.3f);NativeEntranceKeys.Set(v,LE,-1.15f,0,0);NativeEntranceKeys.Set(v,RA,.35f,0,-.35f);NativeEntranceKeys.Set(v,RE,-.4f,0,0);});
            k.Key(0,kneel(-.2f,.3f,.2f));k.Key(Slam,kneel(-.37f,.55f,.42f)).ImpactKey();k.Key(1.0f,kneel(-.36f,.5f,.4f));
            k.Key(Dip,kneel(-.35f,.45f,.36f));k.Key(Silence-.05f,kneel(-.3f,.25f,.3f));
            // Out of the dark already rising; standing tall on the hit, chin down.
            k.Key(Return,stand).Root(0,.88f,0,-.22f).Drop(-.12f).J(B,.12f).J(Hd,.3f).J(LA,-.15f,0,.25f).J(LE,-.4f).J(RA,-.15f,0,-.25f).J(RE,-.4f).Planted(1,0);
            k.Key(Resolve,stand).Root(0,.88f,0,-.22f).J(B,-.02f).J(C,-.04f).J(Hd,.16f).J(LA,-.12f,0,.2f).J(LE,-.35f).J(RA,-.12f,0,-.2f).J(RE,-.3f).ImpactKey();
            // The hand to the hood (Arms), the weight onto the back leg, the head tilting into it.
            k.Key(HandAt,stand).Root(0,.88f,0,-.22f).J(C,-.06f,.08f,0).J(Hd,.12f,.06f,-.06f).J(RA,-.18f,0,-.22f).J(RE,-.5f).J(LA,-.6f,0,.5f).J(LE,-1.6f);
            k.Key(NativeEntranceScript.NameAt,stand).Root(0,.88f,0,-.22f).J(C,-.08f,.1f,0).J(Hd,.1f,.08f,-.08f).J(RA,-.2f,0,-.22f).J(RE,-.55f).J(LA,-.6f,0,.5f).J(LE,-1.6f);
            // The laugh: the head thrown back a touch and the shoulders shaking, then settling.
            k.Key(Flare,stand).Root(0,.88f,0,-.22f).J(C,-.08f,.1f,0).J(Hd,.08f,.08f,-.08f).J(RA,-.2f,0,-.22f).J(RE,-.55f).J(LA,-.6f,0,.5f).J(LE,-1.6f);
            k.Key(Flare+.18f,stand).Root(0,.88f,0,-.22f).J(C,-.14f,.1f,0).J(Hd,-.12f,.08f,-.04f).J(RA,-.2f,0,-.24f).J(RE,-.6f).J(LA,-.6f,0,.5f).J(LE,-1.6f).PassKey();
            k.Key(4.75f,stand).Root(0,.88f,0,-.22f).J(C,-.1f,.1f,0).J(Hd,.04f,.08f,-.06f).J(RA,-.2f,0,-.22f).J(RE,-.55f).J(LA,-.6f,0,.5f).J(LE,-1.6f);
            Keys=k;
            Shots=new[]
            {
                // Wide in the dark: the glitch tearing the stage.
                Orbit(0,NativeEntranceCut.Cut,44,42,18,8,4.4f,4.0f,1.1f,1.0f,V(0,.6f,0),V(0,.55f,0),0,0),
                // The slam: low and close on the crouch.
                Shot(Slam,NativeEntranceCut.Cut,42,39,V(.9f,.38f,2.65f),V(.72f,.42f,2.4f),V(0,.5f,0),V(0,.58f,0),1,0),
                // The dip: a slow drift round the side as the crouch starts to rise.
                Orbit(Dip,NativeEntranceCut.Cut,38,36,-42,-30,3.0f,2.8f,.7f,.8f,V(0,.65f,0),V(0,.8f,0),0,0),
                // The soft re-entry: front on, the projection standing up.
                Shot(Return,NativeEntranceCut.Cut,40,38,V(.1f,1.2f,3.6f),V(.05f,1.25f,3.4f),V(0,1.0f,0),V(0,1.1f,0),1,0),
                // The hard re-entry: low three-quarter as the hood, face and ring snap on.
                Shot(Resolve,NativeEntranceCut.Cut,36,34,V(.95f,.62f,3.0f),V(.8f,.72f,2.75f),V(0,1.2f,0),V(0,1.25f,0),1,0),
                // The push-in: square on the face, to the reference's frame by the hand-off (the whole hood over the chest).
                Shot(BuildFrom,NativeEntranceCut.Cut,34,31,V(-.45f,1.42f,2.35f),V(-.3f,1.47f,1.4f),V(-.02f,1.33f,0),V(-.04f,1.38f,0),0,0),
            };
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
        // Glitch level in the scene (the fragments, streaks and ghosts follow the screen's tear and split).
        private float Level(float t){var g=NativeSheldonIntro.Glitch(t,Reduced,false);return Math.Max(g.Tear,g.Split)*(1-g.Black);}
        private bool On(int bone,float t,float chance){int s=NativeSheldonIntro.Step(t,false);return NativeSheldonIntro.Hash(s*29+bone,31)<chance;}
        public override float Solid(float t,int bone)
        {
            if(t>=Resolve)return 1;
            if(t>=Silence&&t<Return)return 0;
            bool head=bone==CapturedRig.Head;
            if(t>=Return)return head?0:.4f;// soft: dithered, the head (and so the hood) not yet there
            if(t<Slam-.03f)return !Reduced&&t>Slam-.3f&&On(bone,t,.25f*(t-(Slam-.3f))/.27f)?.35f:0;// fragments flickering in
            // Between the slam and the silence a glitching figure: whole bones dropping out on glitch steps (steady with reduced effects).
            // The head is never the bare pilot's: missing, or the hood flashing in on a glitch step (none with reduced effects).
            if(Reduced)return head?0:.6f;
            float chance=.82f-.3f*NativeEntranceMath.Ramp(t,Dip,Silence);
            if(head)return On(bone,t,.3f)?1:0;
            return On(bone,t,chance)?1:.25f;
        }
        public override bool Holo(float t,out Vector3 color,out float opacity,out Vector3 offset)
        {
            float a=t>=Return&&t<Resolve+.05f?1-NativeEntranceMath.Ramp(t,Resolve-.02f,Resolve+.05f):0;color=Cy*1.4f;
            int s=NativeSheldonIntro.Step(t,Reduced);offset=!Reduced&&NativeSheldonIntro.Hash(s,41)>.75f?Local(NativeEntranceMath.Signed(4111,s)*.06f,0,0):Vector3.Zero;
            opacity=.32f*a*(Reduced?1:.75f+.25f*NativeSheldonIntro.Hash(s,43));return a>.01f;
        }
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            // The RGB split in the scene: magenta and cyan copies pulled apart on the glitch hits.
            // Only on the strong hits, and only in the wide shots (in the close push-in a copy this near reads as a tint, so the
            // screen pass alone splits the frame there): a split copy, not a tint over the figure.
            if(t<Slam-.03f||t>=Silence&&t<Return||t>=BuildFrom||into.Length<2)return 0;float k=Level(t);if(k<.28f)return 0;
            int s=NativeSheldonIntro.Step(t,Reduced);float jag=Reduced?1:.6f+.8f*NativeSheldonIntro.Hash(s,47),apart=(Reduced?.025f:.1f)*k*jag,op=(Reduced?.16f:.3f)*NativeEntranceMath.Smooth((k-.25f)/.35f);
            into[0]=new NativeEntranceGhost{Lag=0,Opacity=op,Color=Mg,Offset=Local(apart,0,0)};
            into[1]=new NativeEntranceGhost{Lag=Reduced?0:.03f,Opacity=op,Color=Cy,Offset=Local(-apart,0,.01f)};
            return 2;
        }
        protected override float Pylon(int i,float t)
        {
            // Dead until the resolve (a stray flicker on the slam), then powered on round the ring.
            float flick=!Reduced&&t>Slam&&t<Silence&&NativeSheldonIntro.Hash(NativeSheldonIntro.Step(t,false)*13+i,53)>.93f?.6f:0;
            return Math.Max(flick,PowerOn(i,t,Resolve-.02f,1.2f)*NativeEntranceMath.Ramp(t,Resolve-.02f,Resolve));
        }
        public override void Props(float t,INativeEntranceProps props,float q)
        {
            float grid=t<Silence?.12f:t<Resolve?.06f:.18f+.12f*NativeEntranceMath.Ramp(t,Resolve,Resolve+.4f);Stage(props,t,grid,Resolve,Cy);
            // Flickering fragments: thin glowing slabs round the pilot, re-dealt on every glitch step.
            float k=Level(t);if(k<.05f||t>=Silence&&t<Return)return;int s=NativeSheldonIntro.Step(t,Reduced),n=(int)(18*Math.Max(.25f,q)*Math.Min(1,k*1.5f));var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);
            for(int i=0;i<n;i++)
            {
                float h=NativeSheldonIntro.Hash(s*61+i,59);if(h>.85f)continue;float a=NativeSheldonIntro.Hash(s*61+i,61)*6.2832f,r=.45f+.9f*NativeSheldonIntro.Hash(s*61+i,67),y=.35f+1.7f*NativeSheldonIntro.Hash(s*61+i,71);
                var p=new Vector3(pelvis.X+(float)Math.Sin(a)*r,y,pelvis.Z+(float)Math.Cos(a)*r);float len=.04f+.32f*NativeSheldonIntro.Hash(s*61+i,73);
                var c=h<.4f?Cy:h<.65f?Mg:Vector3.One;props.Piece(WeaponShape.Cube,true,p,NativeEntranceMath.Yaw(a),new Vector3(len,.012f+.03f*NativeSheldonIntro.Hash(s*61+i,79),.012f),c*(1.2f+1.6f*k));
            }
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            if(t>=Resolve)Motes(fx,t,q,Cy,977);var right=RightOf(t);var fwd=ForwardOf(t);var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);
            // Scanline streaks sweeping across the pilot's front, re-dealt on every glitch step.
            float k=Level(t);
            if(k>.05f&&!(t>=Silence&&t<Return))
            {
                int s=NativeSheldonIntro.Step(t,Reduced),n=q<.5f?4:9;
                for(int i=0;i<n;i++)
                {
                    float y=NativeEntranceStage.FloorY+.15f+1.85f*NativeSheldonIntro.Hash(s*37+i,83),w=.4f+1.4f*NativeSheldonIntro.Hash(s*37+i,89),x=NativeEntranceMath.Signed(s*37+i,97)*.4f;
                    var p=new Vector3(pelvis.X,y,pelvis.Z)+fwd*.35f+right*(x+w*.5f);var c=NativeSheldonIntro.Hash(s*37+i,101)>.5f?Cy:Mg;
                    fx.Streak(p,right*w,c*(1.6f*k),.006f+.01f*NativeSheldonIntro.Hash(s*37+i,103),0,1,.1f,0,9100+i);
                }
            }
            // The slam: a burst of cyan sparks and a white flash at the crouch.
            var spot=new Vector3(pelvis.X,NativeEntranceStage.FloorY,pelvis.Z);
            NativeEntranceFx.Sparks(fx,t,Slam,spot+new Vector3(0,.08f,0),new Vector3(0,.6f,0),1.2f,28,3.6f,.7f,9.8f,.8f,Vector3.One*3,Cy*2,.018f,9200,q,NativeEntranceStage.FloorY);
            float pop=NativeEntranceMath.Pulse(t,Slam,.01f,.12f);if(pop>.02f)fx.Glow(spot+new Vector3(0,.4f,0),Cy*(2.4f*pop),0,1.1f*pop,1,0,9240);
            // The resolve: a flash on the face and the ring's snap.
            var head=Anchor(NativeEntranceAnchor.Head,t);float snap=NativeEntranceMath.Pulse(t,Resolve,.01f,.18f);
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
        public override void Lights(float t,NativeEntranceLights l)
        {
            bool dark=t>=Silence&&t<Return;float on=NativeEntranceMath.Ramp(t,Resolve-.02f,Resolve);
            NativeEntranceStage.Rig(l,dark?0:.12f+.3f*on,Cy,dark?0:.5f+.7f*on,dark?0:.1f+.25f*on,dark?.05f:.35f+.4f*on);if(dark)return;
            var head=Anchor(NativeEntranceAnchor.Head,t);var fwd=ForwardOf(t);
            // The face's own light on the hood's opening and the chest once it is on.
            if(t>=Resolve){var f=NativeSheldonIntro.Face(t,Reduced);l.Add(head+fwd*.4f,Cy*(.8f*f.Glow),2.2f);}
            float slam=NativeEntranceMath.Pulse(t,Slam,.01f,.16f),hit=NativeEntranceMath.Pulse(t,Resolve,.01f,.2f),flare=NativeEntranceMath.Pulse(t,Flare,.01f,.16f);
            if(slam>.01f)l.Add(Anchor(NativeEntranceAnchor.Pelvis,t)+new Vector3(0,.4f,.4f),Cy*(16*slam),6);
            if(hit>.01f)l.Add(head+fwd*.8f+new Vector3(0,.2f,0),NativeEntranceFx.Hex(0xe6fdff,24*hit),6);
            if(flare>.01f)l.Add(head+fwd*.5f,Cy*(3*flare),4);
            // Glitch flicker of the key between the slam and the silence (none with reduced effects).
            if(!Reduced&&t>Slam&&t<Silence&&NativeSheldonIntro.Hash(NativeSheldonIntro.Step(t,false),113)>.8f)l.Add(head+fwd*1.2f,Mg*4,5);
        }
        protected override void Plan()=>Blasts=new[]{new NativeEntranceBlast{At=Slam,Style=NativeBlastStyle.Quake,Position=new Vector3(0,NativeEntranceStage.FloorY+.9f,0),Radius=1.3f,Size=.4f}};
    }
}
