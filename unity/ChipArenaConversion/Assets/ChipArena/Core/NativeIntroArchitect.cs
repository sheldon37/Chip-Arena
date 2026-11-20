using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "DEV · Architect of Reality", the owner's dev intro (owner 2026-11-20: "a new dev intro for myself ... something 'bad-ass,
    /// godlike' really show the authority of the dev"; docs/INTRO-DEV-ARCHITECT.md). 15.7 s on the owner's song "Obey" (intro time =
    /// song time - 5.17 s), the name at the drop, 12.63 s. Every beat sits on a transient measured in the song:
    /// - 0.00 the strike: a column of light slams into the old arena (the entrance stage every intro used, modelled); its floor
    ///   cracks with light and the world glitches with the music's stutters;
    /// - 1.97 the break: the arena lets go; plates, dais and pylons lift, their stone falls in to wireframe, the wire breaks into
    ///   code glyphs and the glyphs into light; under it the void and its grid (2.97 a tear runs across the world);
    /// - 3.505 the sub returns: the presence, a point of light before any body, the old world's shards orbiting it, glyphs pouring in;
    /// - 5.035 the draft: a body of light (its silhouette glowing cyan at the edges, pens of light tracing it, a scan sweeping it);
    ///   6.38-6.66 it fills solid from the core out and bursts the shards away; the visor ignites; rim lit, an aura round it;
    /// - 8.03 stillness: the architect floats, perfectly still, while pens of light draw the new world's plan in the void;
    /// - 10.5 the gathering: the arms rise and the new world flies in from the void as wireframes, a step nearer on each of the
    ///   riser's five stutters; 11.82 the inhale, close and low on the visor as it kindles; 12.27/12.50 the hands close in;
    /// - 12.62 THE DROP: everything snaps into place at once, solid; light erupts up every monolith and on into the sky, floods the
    ///   floor's seams; the architect hovers over the dais, the arms lowering from the V with light pouring from the hands into
    ///   the seams, the visor blazing; the live name is forged in the sky out of code glyphs, pulses and settles; 14.0 the command,
    ///   an open palm settling over the world; 15.3 fade with the song.
    /// Presentation only: pure functions of the presentation clock, allocation-free per frame (the name's strokes are laid out
    /// once per name), thinned by quality and Low; Reduced effects keeps every beat without shake or fast flashes. No rings
    /// anywhere (owner): the floor's seams, the crown's spires and the name's light are rays.
    /// </summary>
    public sealed class NativeIntroArchitect:NativeIntroV2Script,INativeIntroHands
    {
        // ---- length and the song's beats (intro s; measured from the owner's track, intro = song - 5.17 s) ----
        public const float Total=15.7f,CardAt=12.63f;
        public const float Strike=0f,Break=1.97f,Tear=2.97f,Presence=3.505f,PulseA=3.74f,PulseB=3.895f,Draft=5.035f,Condense=6.38f,Body=6.66f,
            Still=8.03f,Gather=10.5f,Inhale=11.82f,DipA=12.27f,DipB=12.5f,Drop=12.62f,FadeFrom=15.3f;
        /// <summary>After the drop: the arms lower from the V, pouring light into the seams (Lower); the command, the right palm out over
        /// the world, settling (Command).</summary>
        public const float Lower=13.0f,Command=14.0f;
        /// <summary>The riser's five stutters: each pulls the new world a step nearer.</summary>
        public static readonly float[] Stutters={11.19f,11.31f,11.44f,11.565f,11.69f};
        /// <summary>The glitch stutters before the break (the song's high-band gates and onsets).</summary>
        public static readonly float[] Glitches={.06f,.3f,.58f,1.07f,1.43f,1.86f};
        public const string SongId="music/architect-obey",BankId="ChipArenaIntroDev",CardEffect="architect";
        /// <summary>The presence (and the hovering chest) height, and how high the architect floats until the drop.</summary>
        public const float PresenceY=1.5f,Hover=.45f;
        /// <summary>How high the architect hovers over the dais from the drop to the end (the rig's root over its standing height): it
        /// never lands.</summary>
        public const float Reign=.26f;
        /// <summary>The drawn model's eyes over the head anchor (the skull-base pivot) along the head's own up axis, and the visor's face
        /// in front of them (integrator 2026-11-20; the first version's hero frames put the visor .2 m over the anchor).</summary>
        public const float EyeUp=.18f,VisorFront=.13f;

        private static readonly Vector3 Cyan=NativeEntranceFx.Hex(0x8ff0ff),Ice=NativeEntranceFx.Hex(0xd8fbff),Gold=NativeEntranceFx.Hex(0xffd690),
            GlyphTint=NativeEntranceFx.Hex(0x5ff0ff),Rose=NativeEntranceFx.Hex(0xff4d7a),Blue=NativeEntranceFx.Hex(0x3f6dff),Neon=NativeEntranceFx.Hex(0xbfe8ff),
            Molten=NativeEntranceFx.Hex(0xfff0c8),Ember=NativeEntranceFx.Hex(0xff8424),Deep=NativeEntranceFx.Hex(0x3d78ff),Dust=NativeEntranceFx.Hex(0x1c2230);
        private static readonly Vector3 Up=Vector3.UnitY;

        public NativeIntroArchitect():base(NativeIntroCatalog.ArchitectId,Total,CardAt,Drop,0x8ff0ff)
        {
            Start=Stance(.13f,.05f,.08f,-.14f,-.06f,-.36f);RootPath=Float;
            BuildKeys();BuildKit();BuildCamera();
        }
        /// <summary>The rig's camera, jolted by the strike and the drop (a short decaying shake; none in Reduced effects).</summary>
        public override void Camera(float age,out NativeEntranceView view)
        {
            base.Camera(age,out view);if(Context.Reduced)return;float t=Clock(Math.Min(age,SettleAt));
            float s=NativeEntranceMath.Pulse(t,Strike,.004f,.12f)*.035f+NativeEntranceMath.Pulse(t,Drop,.004f,.16f)*.045f+NativeEntranceMath.Pulse(t,Body,.004f,.1f)*.012f;if(s<1e-4f)return;
            var j=new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);view.Eye+=j;view.Look+=j*.6f;
        }
        public override string SetBank=>BankId;
        public override string Music=>SongId;
        public override NativeIntroCue[] Cues=>CueSheet;

        // =====================================================================================================================
        // The body: keyed on the rig (no pack clips), the drawn model follows it. Its hands as drawn come in each frame (Hands).
        // =====================================================================================================================
        private const float HoverY=.88f+Hover,ReignY=.88f+Reign;
        /// <summary>The reign's slow float (from the drop): the root rising and settling a hand's width, never touching the dais.</summary>
        private static Vector3 Float(float t,Vector3 root)=>t<Drop?root:root+new Vector3(0,.02f*(float)Math.Sin((t-Drop)*2.1f)*NativeEntranceMath.Ramp(t,Drop+.3f,Drop+1.3f),0);
        private void BuildKeys()
        {
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();float f=Facing;
            // Floating, head bowed, arms a little out, one knee drawn up, the feet pointed: the arrival.
            float[] form=NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.RootY]=HoverY;v[NativeEntranceKeys.Yaw]=f;v[NativeEntranceKeys.PlantL]=0;v[NativeEntranceKeys.PlantR]=0;
                NativeEntranceKeys.Set(v,Hd,.48f,0,0);NativeEntranceKeys.Set(v,C,.1f,0,0);NativeEntranceKeys.Set(v,Sp,.04f,0,0);
                NativeEntranceKeys.Set(v,LA,-.06f,0,.3f);NativeEntranceKeys.Set(v,RA,-.06f,0,-.3f);NativeEntranceKeys.Set(v,LE,-.28f,0,0);NativeEntranceKeys.Set(v,RE,-.24f,0,0);
                NativeEntranceKeys.Set(v,LL,-.2f,0,.05f);NativeEntranceKeys.Set(v,LK,.42f,0,0);NativeEntranceKeys.Set(v,RL,.04f,0,-.05f);NativeEntranceKeys.Set(v,RK,.14f,0,0);
                NativeEntranceKeys.Set(v,CapturedRig.LFoot,.5f,0,0);NativeEntranceKeys.Set(v,CapturedRig.RFoot,.42f,0,0);});
            k.Key(0,form);k.Key(Draft,form);
            k.Key(Body-.06f,NativeEntranceKeys.With(form,v=>NativeEntranceKeys.Set(v,Hd,.52f,0,0)));
            // The body completes: the chest lifts, the arms are thrown a little out by its own pressure wave.
            k.Key(Body,form).J(Hd,.3f).J(C,-.12f).J(Sp,-.04f).J(LA,-.22f,0,.58f).J(RA,-.22f,0,-.58f).J(LE,-.12f).J(RE,-.1f).ImpactKey();
            k.Key(Body+.32f,form).J(Hd,.34f).J(C,-.04f).J(LA,-.1f,0,.4f).J(RA,-.1f,0,-.4f).J(LE,-.22f).J(RE,-.2f);
            // The head rises to look ahead (by the stillness).
            k.Key(Still-.15f,form).J(Hd,.0f).J(C,-.06f).J(LA,-.1f,0,.36f).J(RA,-.1f,0,-.36f).J(LE,-.22f).J(RE,-.2f);
            k.Key(Still+.5f,form).J(Hd,-.02f).J(C,-.07f).J(LA,-.1f,0,.36f).J(RA,-.1f,0,-.36f).J(LE,-.22f).J(RE,-.2f);
            // The stillness: the arms open out wide and low, slowly, the chest open.
            k.Key(Gather-.05f,form).J(Hd,-.06f).J(C,-.12f).J(LA,-.42f,-.25f,1.05f).J(RA,-.42f,.25f,-1.05f).J(LE,-.32f).J(RE,-.3f);
            // The gathering: the arms come forward and up, a step on each stutter, the head lifting with them.
            float[] lift={-.7f,-1.1f,-1.5f,-1.9f,-2.3f,-2.65f};float[] spread={1.0f,.9f,.8f,.7f,.62f,.55f};float[] head={-.1f,-.13f,-.16f,-.19f,-.21f,-.22f};
            k.Key(Stutters[0],form).J(Hd,head[0]).J(C,-.16f).J(LA,lift[0],0,spread[0]).J(RA,lift[0],0,-spread[0]).J(LE,-.35f).J(RE,-.35f);
            for(int s=0;s<Stutters.Length;s++)
            {
                float at=Stutters[s]+.065f;
                k.Key(at,form).J(Hd,head[s+1]).J(C,-.16f-.03f*s).J(LA,lift[s+1],0,spread[s+1]).J(RA,lift[s+1],0,-spread[s+1]).J(LE,-.3f+.02f*s).J(RE,-.3f+.02f*s);
            }
            // The inhale: held high, the gaze coming level (gathering itself), the hands open over the head; on the two dips the hands close
            // and draw in beside the helmet, down into the close-up's frame.
            k.Key(Inhale+.1f,form).J(Hd,-.06f).J(C,-.16f).J(Sp,-.04f).J(LA,-2.72f,0,.52f).J(RA,-2.72f,0,-.52f).J(LE,-.22f).J(RE,-.22f);
            k.Key(DipA,form).J(Hd,.0f).J(C,-.14f).J(Sp,-.04f).J(LA,-2.78f,0,.46f).J(RA,-2.78f,0,-.46f).J(LE,-.55f).J(RE,-.55f);
            k.Key(DipB+.06f,form).J(Hd,.06f).J(C,-.1f).J(Sp,-.03f).J(LA,-2.88f,0,.32f).J(RA,-2.88f,0,-.32f).J(LE,-1.2f).J(RE,-1.2f);
            // THE DROP: the hands fling open into the V; the body sinks to its reign over the dais and hovers there (it never lands).
            float[] reign=NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.RootY]=ReignY;v[NativeEntranceKeys.Yaw]=f;v[NativeEntranceKeys.PlantL]=0;v[NativeEntranceKeys.PlantR]=0;
                NativeEntranceKeys.Set(v,Hd,-.1f,0,0);NativeEntranceKeys.Set(v,C,-.18f,0,0);NativeEntranceKeys.Set(v,Sp,-.03f,0,0);
                NativeEntranceKeys.Set(v,LA,-2.5f,0,.85f);NativeEntranceKeys.Set(v,RA,-2.5f,0,-.85f);NativeEntranceKeys.Set(v,LE,-.1f,0,0);NativeEntranceKeys.Set(v,RE,-.1f,0,0);
                NativeEntranceKeys.Set(v,LL,-.07f,0,.04f);NativeEntranceKeys.Set(v,LK,.18f,0,0);NativeEntranceKeys.Set(v,RL,.02f,0,-.04f);NativeEntranceKeys.Set(v,RK,.1f,0,0);
                NativeEntranceKeys.Set(v,CapturedRig.LFoot,.42f,0,0);NativeEntranceKeys.Set(v,CapturedRig.RFoot,.36f,0,0);});
            k.Key(Drop,reign).Root(0,HoverY-.04f,0,f).ImpactKey();
            k.Key(Drop+.2f,reign).Root(0,ReignY+.05f,0,f).J(Hd,-.14f).J(C,-.22f).J(LA,-2.62f,0,.98f).J(RA,-2.62f,0,-.98f);
            // The arms lower from the V out through the sides, the light pouring from the hands into the seams.
            k.Key(Lower,reign).J(Hd,.0f).J(C,-.14f).J(LA,-1.55f,0,1.3f).J(RA,-1.55f,0,-1.3f).J(LE,-.12f).J(RE,-.12f);
            k.Key(Lower+.55f,reign).J(Hd,.12f).J(C,-.12f).J(LA,-.5f,0,.95f).J(RA,-.5f,0,-.95f).J(LE,-.14f).J(RE,-.14f);
            k.Key(Command-.06f,reign).J(Hd,.2f).J(C,-.12f).J(LA,-.3f,0,.74f).J(RA,-.3f,0,-.74f).J(LE,-.16f).J(RE,-.16f);
            // The command: the right palm out over the world, rising to the chest's height, a touch past, settling there; the left hand
            // low, still pouring; the head bowed a little to look down the lens.
            k.Key(Command+.42f,reign).J(Hd,.26f).J(C,-.14f).J(LA,-.28f,0,.68f).J(RA,-1.44f,0,-.3f).J(LE,-.18f).J(RE,-.2f);
            k.Key(Command+.9f,reign).J(Hd,.3f).J(C,-.14f).J(LA,-.27f,0,.66f).J(RA,-1.3f,0,-.32f).J(LE,-.18f).J(RE,-.16f);
            k.Key(Total,reign).J(Hd,.3f).J(C,-.14f).J(LA,-.26f,0,.65f).J(RA,-1.32f,0,-.32f).J(LE,-.18f).J(RE,-.17f);
            Keys=k;
        }
        /// <summary>The body is built from the core out as it condenses (6.38-6.66): the chest first, the hands and feet last.</summary>
        public override float Solid(float t,int bone)
        {
            if(t>=Body+.02f)return 1;if(t<Condense)return 0;
            float order;switch(bone){case CapturedRig.Chest:case CapturedRig.Spine:order=0;break;case CapturedRig.Body:case CapturedRig.Pelvis:case CapturedRig.Neck:case CapturedRig.LClav:case CapturedRig.RClav:order=.25f;break;
                case CapturedRig.Head:case CapturedRig.LArm:case CapturedRig.RArm:case CapturedRig.LHip:case CapturedRig.RHip:order=.5f;break;case CapturedRig.LElbow:case CapturedRig.RElbow:case CapturedRig.LKnee:case CapturedRig.RKnee:order=.72f;break;default:order=.9f;break;}
            return NativeEntranceMath.Smooth((t-Condense-order*(Body-Condense-.04f))/.05f);
        }
        /// <summary>The draft's body of light: the model's own silhouette in cyan, brighter at its edges and filled with a soft inner glow
        /// (a ghost at no lag; no scanlines), denser as it condenses; and two afterimages of the arms flung open at the drop.</summary>
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            int n=0;
            float draft=NativeEntranceMath.Ramp(t,Draft+.1f,Draft+.7f)*(1-NativeEntranceMath.Ramp(t,Body-.05f,Body+.1f));
            if(draft>.004f&&n<into.Length)into[n++]=new NativeEntranceGhost{Lag=0,Opacity=draft*(.26f+.12f*NativeEntranceMath.Ramp(t,Condense-.5f,Condense+.1f)),Color=Cyan*1.4f,Offset=Vector3.Zero};
            float a=t-Drop+.02f;
            if(a>=0&&a<.44f)
            {
                float o=1-a/.44f;
                if(n<into.Length)into[n++]=new NativeEntranceGhost{Lag=.04f,Opacity=.36f*o,Color=Molten*1.3f,Offset=Vector3.Zero};
                if(n<into.Length)into[n++]=new NativeEntranceGhost{Lag=.085f,Opacity=.22f*o,Color=Cyan*1.2f,Offset=Vector3.Zero};
            }
            return n;
        }
        protected override Vector3 Focus(float t,Vector3 chest)=>t<Draft?new Vector3(0,PresenceY,0):chest;
        // The visor and trims: dark until the body ignites, ice while it floats; drawn in on the inhale and kindling through the
        // close-up; white at the drop; then blazing white-gold with the name.
        private const string AccentDark="#05080c",AccentIce="#bff6ff",AccentFlash="#ffffff",AccentDim="#3f8a9c",AccentKindle="#9fe8f6",AccentHot="#e8fbff",AccentBlaze="#fff2d2";
        public override string AccentAt(float t)=>t<Body-.02f?AccentDark:t<Body+.12f?AccentFlash:t<Inhale+.05f?AccentIce:t<DipA?AccentDim:t<DipB?AccentKindle:t<Drop?AccentHot:t<Drop+.12f?AccentFlash:AccentBlaze;

        // ---- the hands as drawn (INativeIntroHands): the model's wrists this frame, for the light poured from them ----
        private Vector3 drawnLeft,drawnRight;private float handsAt=-100;private bool handsModel;
        public void Hands(float t,in NativeIntroHand right,in NativeIntroHand left){drawnRight=right.At;drawnLeft=left.At;handsModel=right.Model&&left.Model;handsAt=t;}
        /// <summary>A hand at `t`: the drawn model's wrist when the runtime handed it in this frame (the model re-places its own arms, so its
        /// hands sit off the keyed rig's), else the keyed hand anchor.</summary>
        private Vector3 HandAt(bool left,float t)
        {
            var keyed=Anchor(left?NativeEntranceAnchor.LeftHand:NativeEntranceAnchor.RightHand,t);
            if(handsModel&&Math.Abs(handsAt-t)<1e-4f){var p=left?drawnLeft:drawnRight;if((p-keyed).LengthSquared()<.81f)return p;}
            return keyed;
        }
        // ---- the keyed head's frame (its up and forward axes), sampled with the anchors at 120 Hz: where the visor is ----
        private Vector3[] headAxes;private readonly CapturedRig.Pose axisPose=new CapturedRig.Pose();
        protected override void Plan()
        {
            int n=(int)(Length*AnchorRate)+2;var a=new Vector3[n*2];
            for(int i=0;i<n;i++)
            {
                float t=i/(float)AnchorRate;Pose(t,axisPose,out _,out var rot);axisPose.World();var q=NativeEntrancePose.WorldRotation(axisPose,rot,CapturedRig.Head);
                a[i*2]=Vector3.Transform(Vector3.UnitY,q);a[i*2+1]=Vector3.Transform(Vector3.UnitZ,q);
            }
            headAxes=a;
        }
        /// <summary>The head's own up and forward at `t` (the keyed rig's; the drawn model follows its turns).</summary>
        public void HeadFrame(float t,out Vector3 up,out Vector3 forward)
        {
            if(headAxes==null)Prepare();
            int n=headAxes.Length/2;float x=Math.Max(0,Math.Min(n-1.001f,t*AnchorRate));int i=(int)x;float f=x-i;
            up=Vector3.Normalize(Vector3.Lerp(headAxes[i*2],headAxes[i*2+2],f));forward=Vector3.Normalize(Vector3.Lerp(headAxes[i*2+1],headAxes[i*2+3],f));
        }
        /// <summary>The visor's face at `t`: EyeUp along the head's up from the head anchor (the skull-base pivot), VisorFront ahead.</summary>
        public Vector3 VisorAt(float t){HeadFrame(t,out var up,out var fwd);return Anchor(NativeEntranceAnchor.Head,t)+up*EyeUp+fwd*VisorFront;}

        // =====================================================================================================================
        // The camera: cut on the song's transients.
        // =====================================================================================================================
        /// <summary>The hero frame's settled eye (the end of the drop's push), what the sky's name faces.</summary>
        public static readonly Vector3 HeroEye=new Vector3(-.5f,.3f,3.85f);
        private void BuildCamera()
        {
            var f=new Vector3((float)Math.Sin(Facing),0,(float)Math.Cos(Facing));
            Rig.Dolly(Strike,V(3.4f,1.05f,6.5f),V(2.7f,.95f,5.5f),V(0,1.0f,0),V(0,1.12f,0),54,50).Ease(NativeIntroEase.Out).Handheld(.6f)
               .Dolly(1.07f,V(-2.5f,.3f,2.7f),V(-1.75f,.26f,1.75f),V(-4.6f,.38f,-2.6f),V(-4.6f,.5f,-3.1f),40,38).Handheld(.35f)
               .Crane(Break,V(4.6f,2.1f,8.8f),V(7.2f,3.6f,14.5f),3.2f,V(0,.6f,0),V(0,2.1f,0),50,56).Ease(NativeIntroEase.InOut)
               .Orbit(Presence,Vector3.Zero,40,-38,7.6f,6.2f,1.2f,1.95f,V(0,PresenceY,0),V(0,PresenceY,0),40,36).Ease(NativeIntroEase.InOut)
               // the draft: the whole figure of light, a slow push
               .Dolly(Draft,V(1.1f,1.85f,4.0f),V(.84f,1.8f,3.05f),V(0,PresenceY-.02f,0),V(0,PresenceY-.04f,0),38,35).Focus(0,0,.35f)
               // the body: low, the figure rising over the lens, rim lit against the void
               .Dolly(Body,V(.92f,.44f,2.85f),V(.76f,.5f,2.45f),V(0,PresenceY+.16f,0),V(0,PresenceY+.12f,0),46,43).Handheld(.25f)
               .Crane(Still,V(11.0f,4.2f,17.0f),V(9.6f,4.4f,14.6f),1.4f,V(0,2.3f,0),V(0,2.9f,0),42,38).Ease(NativeIntroEase.InOut)
               .Dolly(Gather,V(2.7f,.42f,5.1f),V(1.85f,.5f,3.45f),V(0,3.3f,-1.1f),V(0,2.8f,-.5f),52,44).Ease(NativeIntroEase.In)
               // the inhale: low and frontal on the visor as it kindles (the eye held on the head's ground point), the hands coming down
               // into the frame on the dips; outside the arms, so nothing passes through the lens
               .Dolly(Inhale,f*1.42f+V(-.06f,1.64f,0),f*1.16f+V(-.05f,1.7f,0),f*VisorFront+V(0,EyeUp+.02f,0),f*VisorFront+V(0,EyeUp+.03f,0),33,29).Follow(NativeEntranceAnchor.Head,0,1).Ease(NativeIntroEase.Out)
               // THE DROP: the hero frame from just over the floor, looking up: a slow push and orbit to the front, the architect hovering
               // between two monoliths, the sky's name above, the crown's spires leaning in at the top of the frame
               .Spline(Drop,new[]{V(1.35f,.36f,5.2f),V(.45f,.33f,4.5f),HeroEye},V(.1f,2.25f,-1.2f),V(.05f,1.95f,-1f),56,50).Ease(NativeIntroEase.Out)
               .Bars(0,.85f).Bars(Drop,.55f).Bars(Drop+.9f,.2f).Black(0,0).Black(FadeFrom+.12f,1);
        }

        // =====================================================================================================================
        // The kit: the old arena that comes apart and the architect's world that is gathered (tools/stages/intro_dev_architect.py
        // models every piece in its own frame; these are its numbers).
        // =====================================================================================================================
        private const byte OldPlate=0,OldWedge=1,OldPylon=2,OldSky=3,Dais=4,Slab=5,Plinth=6,Obelisk=7,Crown=8,Spire=9;
        private struct Piece
        {
            public string Prop,Wire;public byte Kind;public Vector3 Rest,Centroid,Outward;public Quaternion Turn;public float Yaw,Lift,Size,Delay,Reach;public int Seed;
            public Vector3 From,Hover,Swirl;public Quaternion Tumble,Residual;
        }
        private const float PlateSize=4f,Ground=NativeEntranceStage.FloorY-.132f,PylonRing=7.5f,MonoRing=9.3f,GridY=NativeEntranceStage.FloorY-.42f;
        /// <summary>The floor (the set script's): sixteen wedges round a sixteen-sided dais, each cut once across at an apothem alternating
        /// round the floor (Splits), the seams at (k + .5) x 22.5 degrees; the dais's and the rim's circumradii.</summary>
        public const int Wedges=16;public const float WedgeHalf=(float)(Math.PI/16),DaisR=1.5f,FloorR=7.4f;private static readonly float[] Splits={3.5f,4.6f};
        /// <summary>The crown: eight gold spires rooted CrownR out and CrownY up over the monoliths, leaning in toward the point CrownApex
        /// over the dais (rays, never a ring); each SpireLength long.</summary>
        public const float CrownR=10.2f,CrownY=14f,CrownApex=23f,SpireLength=9f;
        /// <summary>The monoliths' obelisks: their feet on the plinths and their capstones' points.</summary>
        private const float MonoFoot=NativeEntranceStage.FloorY+.6f,MonoTip=MonoFoot+4*2.4f+1.6f;
        private Piece[] old,neo,spires;
        private static readonly string[] SlabProps={"slab-00","slab-01","slab-10","slab-11"},SlabWires={"slab-00.wire","slab-01.wire","slab-10.wire","slab-11.wire"},SkyProps={"old-skyline-0","old-skyline-1","old-skyline-2","old-skyline-3"},
            SkyWires={"old-skyline-0.wire","old-skyline-1.wire","old-skyline-2.wire","old-skyline-3.wire"},SpireProps={"spire-a","spire-b","spire-c"},ShardProps={"shard-a","shard-b","shard-c","shard-d"};
        private static Vector3 Polar(float r,float a,float y)=>new Vector3(r*(float)Math.Sin(a),y,r*(float)Math.Cos(a));
        private static float R(int seed,int i)=>NativeEntranceMath.Rand(seed,i);
        private static float S(int seed,int i)=>NativeEntranceMath.Signed(seed,i);
        private static float Apothem(float r)=>r*(float)Math.Cos(WedgeHalf);
        /// <summary>A crown spire's rest turn at azimuth `a`: its blade (+y) leaning in toward the apex, its ridges toward the centre.</summary>
        public static Quaternion SpireTurn(float a)
        {
            float lean=(float)Math.Atan2(CrownR,CrownApex-CrownY);return Quaternion.CreateFromAxisAngle(new Vector3(-(float)Math.Cos(a),0,(float)Math.Sin(a)),lean)*YawQ(a);
        }
        private void BuildKit()
        {
            var o=new List<Piece>();
            for(int i=-2;i<2;i++)for(int j=-2;j<2;j++){var at=new Vector3(i*PlateSize+PlateSize*.5f,Ground,j*PlateSize+PlateSize*.5f);o.Add(new Piece{Prop="old-plate",Wire="old-plate.wire",Kind=OldPlate,Rest=at,Centroid=new Vector3(0,-.06f,0),Size=2.8f});}
            for(int k=0;k<6;k++)o.Add(new Piece{Prop="old-dais-wedge",Wire="old-dais-wedge.wire",Kind=OldWedge,Rest=new Vector3(0,NativeEntranceStage.FloorY,0),Yaw=k*1.0471976f,Centroid=new Vector3(0,-.066f,1.5f),Size=1.6f});
            for(int i=0;i<12;i++){float a=(i+.5f)/12*6.2831853f;o.Add(new Piece{Prop="old-pylon",Wire="old-pylon.wire",Kind=OldPylon,Rest=Polar(PylonRing,a,Ground),Yaw=a,Centroid=new Vector3(0,1.8f,0),Size=1.9f});}
            // (a skyline group is one mesh about the arena centre, modelled in Unity space: the middle of its towers, mirrored into the source frame)
            for(int k=0;k<4;k++){float a=(k*6+3)/24f*6.2831853f;var mid=Polar(25,a,6);o.Add(new Piece{Prop=SkyProps[k],Wire=SkyWires[k],Kind=OldSky,Rest=new Vector3(0,Ground,0),Centroid=new Vector3(-mid.X,mid.Y,mid.Z),Size=9});}
            old=o.ToArray();
            for(int i=0;i<old.Length;i++)
            {
                ref var p=ref old[i];p.Turn=YawQ(p.Yaw);var c=World(p);float d=new Vector2(c.X,c.Z).Length();
                p.Seed=7100+i*13;p.Lift=Break+.03f+Math.Min(d,14)*.03f+.05f*R(p.Seed,1);p.Outward=d>.01f?Vector3.Normalize(new Vector3(c.X,0,c.Z)):Vector3.UnitZ;
                p.Swirl=Vector3.Normalize(new Vector3(S(p.Seed,2),.4f,S(p.Seed,3)));
            }
            var n=new List<Piece>();
            n.Add(new Piece{Prop="dais",Wire="dais.wire",Kind=Dais,Rest=new Vector3(0,NativeEntranceStage.FloorY,0),Centroid=new Vector3(0,-.5f,0),Size=1.4f});
            for(int w=0;w<Wedges;w++)for(int part=0;part<2;part++)
            {
                float a=w*2*WedgeHalf,h0=part==0?Apothem(DaisR):Splits[w&1],h1=part==0?Splits[w&1]:Apothem(FloorR),rm=(h0+h1)*.5f;
                n.Add(new Piece{Prop=SlabProps[part*2+(w&1)],Wire=SlabWires[part*2+(w&1)],Kind=Slab,Rest=Polar(rm,a,NativeEntranceStage.FloorY),Yaw=a,Centroid=new Vector3(0,-.2f,0),Size=1+part*.4f,Reach=rm});
            }
            for(int i=0;i<8;i++){float a=(i+.5f)/8*6.2831853f;var at=Polar(MonoRing,a,MonoFoot);n.Add(new Piece{Prop="mono-plinth",Wire="mono-plinth.wire",Kind=Plinth,Rest=at,Yaw=a,Centroid=new Vector3(0,-1.1f,0),Size=2,Reach=MonoRing});n.Add(new Piece{Prop="mono-obelisk",Wire="mono-obelisk.wire",Kind=Obelisk,Rest=at,Yaw=a,Centroid=new Vector3(0,5.4f,0),Size=6,Reach=MonoRing});}
            for(int i=0;i<8;i++){float a=(i+.5f)/8*6.2831853f;n.Add(new Piece{Prop="crown-spire",Wire="crown-spire.wire",Kind=Crown,Rest=Polar(CrownR,a,CrownY),Yaw=a,Centroid=new Vector3(0,SpireLength*.5f,0),Size=4.5f,Reach=CrownR});}
            neo=n.ToArray();
            for(int i=0;i<neo.Length;i++)
            {
                ref var p=ref neo[i];p.Seed=8300+i*17;p.Turn=p.Kind==Crown?SpireTurn(p.Yaw):YawQ(p.Yaw);var c=World(p);float d=new Vector2(c.X,c.Z).Length();p.Outward=d>.01f?Vector3.Normalize(new Vector3(c.X,0,c.Z)):Vector3.UnitZ;
                var side=new Vector3(-p.Outward.Z,0,p.Outward.X);float far=30+14*R(p.Seed,1);
                switch(p.Kind)
                {
                    // the floor rises out of the abyss under it; the monoliths fly in from the horizon; the crown falls from the sky
                    case Dais:case Slab:p.From=c+p.Outward*far*.55f+side*(S(p.Seed,2)*12)+new Vector3(0,-14-10*R(p.Seed,3),0);p.Hover=p.Rest+new Vector3(0,-.85f-.35f*R(p.Seed,4),0)+p.Outward*.35f;p.Delay=.05f+.25f*R(p.Seed,5)+d*.012f;break;
                    case Plinth:case Obelisk:p.From=c+p.Outward*far+side*(S(p.Seed,2)*10)+new Vector3(0,-4+12*R(p.Seed,3),0);p.Hover=p.Rest+p.Outward*1.7f+new Vector3(0,p.Kind==Plinth?-.9f:1.1f,0);p.Delay=(p.Kind==Plinth?.12f:0)+.05f*R(p.Seed,5);break;
                    default:p.From=c+p.Outward*far*.5f+new Vector3(0,26+10*R(p.Seed,3),0);p.Hover=p.Rest+new Vector3(0,2.4f,0)+p.Outward*1.4f;p.Delay=.18f+.08f*R(p.Seed,5);break;
                }
                p.Swirl=side*(S(p.Seed,6)>0?1:-1)*(6+6*R(p.Seed,7));
                p.Tumble=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(S(p.Seed,8),S(p.Seed,9)*.5f,S(p.Seed,10))),2.2f+2.4f*R(p.Seed,11));
                p.Residual=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(S(p.Seed,12),S(p.Seed,13),S(p.Seed,14))),.09f+.06f*R(p.Seed,15));
            }
            spires=new Piece[6];
            for(int i=0;i<6;i++){float a=(i+.3f*R(5,i))/6*6.2831853f+.4f,r=48+22*R(6,i);spires[i]=new Piece{Prop=SpireProps[i%3],Kind=Spire,Rest=Polar(r,a,-26+12*R(7,i)),Yaw=a,Seed=9100+i,Delay=.2f*R(8,i),Size=30};}
        }
        private static Vector3 World(in Piece p)=>p.Rest+Vector3.Transform(p.Centroid,p.Turn);
        private static Vector3 World(Vector3 rest,float yaw,Vector3 local)=>rest+NativeEntranceMath.RotateY(local,yaw);
        private static Quaternion YawQ(float a)=>Quaternion.CreateFromAxisAngle(Up,a);

        // ---- the old arena's pieces ----
        /// <summary>Old piece `i` at `t`: its pose, its solid's scale (1 whole .. 0 gone, about its centroid) and its wire's level.</summary>
        private void OldPose(int i,float t,out Vector3 at,out Quaternion rot,out float solid,out float wire,out Vector3 centre)
        {
            ref var p=ref old[i];var q0=YawQ(p.Yaw);at=p.Rest;rot=q0;solid=1;wire=0;
            // The tremor before the break and the glitch tears (pieces thrown sideways in a band, for a few frames).
            if(t<p.Lift)
            {
                float shake=NativeEntranceMath.Ramp(t,.35f,Break)*.012f;if(!Context.Reduced&&shake>0)at+=new Vector3((float)Math.Sin(t*57+p.Seed)*shake,(float)Math.Abs(Math.Sin(t*43+p.Seed*.7f))*shake*.6f,(float)Math.Sin(t*61+p.Seed*1.3f)*shake);
                at+=TearShift(t,World(p.Rest,p.Yaw,p.Centroid).Y)*(p.Kind==OldSky?3f:1f);
                wire=.25f*TearLevel(t);
            }
            else
            {
                float tau=t-p.Lift,rise=NativeEntranceMath.EaseOut(tau/3.2f);float up=(p.Kind==OldSky?3.5f:.7f)+(p.Kind==OldSky?4:2.4f)*R(p.Seed,4);
                at+=new Vector3(0,up*rise,0)+p.Outward*((.25f+.5f*R(p.Seed,5))*tau)+new Vector3(0,.18f*tau*tau,0);
                float spin=(p.Kind==OldSky?.08f:.35f+.6f*R(p.Seed,6))*tau*(1+.35f*tau);var turn=Quaternion.CreateFromAxisAngle(p.Swirl,spin);
                // about its centroid: the turn moves the frame round the centre of the piece
                var c=NativeEntranceMath.RotateY(p.Centroid,p.Yaw);at+=c-Vector3.Transform(c,turn);rot=turn*q0;
                solid=1-NativeEntranceMath.Smooth((tau-.28f)/.7f);
                wire=(1-solid)*(1-NativeEntranceMath.Smooth((tau-.8f)/.5f))*1.6f+.35f*(1-NativeEntranceMath.Smooth(tau/.4f));
            }
            centre=at+Vector3.Transform(p.Centroid,rot);
        }
        // Tears: a band of the world (centred `TearY`) thrown sideways for a moment on each glitch and on the break's tear.
        private float TearLevel(float t)
        {
            float v=0;for(int i=0;i<Glitches.Length;i++)v=Math.Max(v,Flash(t,Glitches[i],.005f,.05f));v=Math.Max(v,Flash(t,Tear,.005f,.09f));return v;
        }
        private static float TearY(float t)=>t<Break?.4f+1.6f*NativeEntranceMath.Rand(731,(int)(t*9)):1.3f;
        private Vector3 TearShift(float t,float y)
        {
            float lvl=TearLevel(t);if(lvl<.01f)return Vector3.Zero;float band=Math.Max(0,1-Math.Abs(y-TearY(t))/.9f);if(band<=0)return Vector3.Zero;
            float dir=NativeEntranceMath.Rand(733,(int)(t*9))>.5f?1:-1;return new Vector3(dir*.22f*lvl*band*(Context.Reduced?.4f:1),0,0);
        }

        // ---- the architect's pieces ----
        private static readonly float[] Marks={.52f,.64f,.76f,.88f,1f};
        private static float Step(float t,float at,float span)=>NativeEntranceMath.EaseOut((t-at)/span);
        /// <summary>How far a new piece has come from the void to its hover point (0..1): a quickening approach, then a step on each stutter.</summary>
        private static float Approach(float t,float appear)
        {
            if(t<appear)return 0;float u=.38f*NativeEntranceMath.EaseIn(Math.Min(1,(t-appear)/Math.Max(.2f,Stutters[0]-appear)));
            if(t<Stutters[0])return u;u=.38f;
            for(int s=0;s<Stutters.Length;s++){if(t<Stutters[s])break;float from=s==0?.38f:Marks[s-1];u=from+(Marks[s]-from)*Step(t,Stutters[s],.075f);}
            return u;
        }
        /// <summary>New piece `i` at `t`: shown (false before it appears), its pose, the solid shown (after the drop) and the wire's level.</summary>
        private bool NewPose(int i,float t,out Vector3 at,out Quaternion rot,out bool solid,out float wire,out float flash)
        {
            ref var p=ref neo[i];var rest=p.Turn;float appear=Gather+p.Delay;at=p.Rest;rot=rest;solid=false;wire=0;flash=0;
            if(t<appear)return false;
            if(t>=Drop)
            {
                // Locked: a hair of settle, the seams' flash travelling out with the wave of light.
                float s=t-Drop;at=p.Rest-new Vector3(0,.012f*(float)Math.Exp(-14*s)*(float)Math.Sin(s*30),0);solid=true;
                flash=Flash(t,Drop+Math.Max(0,p.Reach-.5f)/18f,.01f,.3f)*(p.Kind==Crown?.7f:p.Kind==Obelisk||p.Kind==Plinth?.45f:.75f);
                wire=1.8f*(1-NativeEntranceMath.Smooth(s/(p.Kind==Crown||p.Kind==Slab||p.Kind==Dais?.14f:.38f)));return true;
            }
            float snapFrom=Drop-.045f;
            if(t>=snapFrom)
            {
                // The snap: from the hover point into the slot, accelerating, arriving exactly on the drop.
                float u=NativeEntranceMath.EaseIn((t-snapFrom)/(Drop-snapFrom));at=Vector3.Lerp(p.Hover,p.Rest,u);rot=Quaternion.Slerp(p.Residual*rest,rest,u);wire=2.6f;return true;
            }
            float a=Approach(t,appear);
            // a curved flight: in along the swirl, nearing the hover point
            var mid=(p.From+p.Hover)*.5f+p.Swirl;float b=a;var pos=(1-b)*(1-b)*p.From+2*(1-b)*b*mid+b*b*p.Hover;
            // the hold: breathing; the dips: a tremble (small and slow in Reduced effects)
            if(t>=Stutters[4])
            {
                float tr=NativeEntranceMath.Bump(t,DipA-.05f,DipB+.08f);float amp=Context.Reduced?.004f:.016f,rate=Context.Reduced?2.4f:23;
                pos+=new Vector3(0,.01f*(float)Math.Sin(t*2.1f+p.Seed),0)+new Vector3((float)Math.Sin(t*rate+p.Seed),(float)Math.Sin(t*rate*1.13f+p.Seed*2),(float)Math.Sin(t*rate*.91f+p.Seed*3))*(amp*tr);
            }
            at=pos;rot=Quaternion.Slerp(p.Tumble*rest,p.Residual*rest,NativeEntranceMath.Smooth(a));
            float pulse=0;for(int s=0;s<Stutters.Length;s++)pulse=Math.Max(pulse,Flash(t,Stutters[s],.01f,.09f));
            float inhale=1-.55f*NativeEntranceMath.Bump(t,Inhale-.04f,DipA+.1f);
            wire=(.35f+1.0f*NativeEntranceMath.Ramp(t,appear,appear+.4f)+1.2f*pulse)*inhale*(.6f+.4f*a);return true;
        }
        /// <summary>A crown spire `i` (0..7) as placed at `t` after the drop (the crown turning slowly): its root and its point.</summary>
        private void SpireLine(int i,float t,out Vector3 root,out Vector3 tip)
        {
            var turn=YawQ(CrownTurn(t));ref var p=ref neo[CrownFirst+i];root=Vector3.Transform(p.Rest,turn);tip=root+Vector3.Transform(Vector3.Transform(new Vector3(0,SpireLength,0),p.Turn),turn);
        }
        private const int CrownFirst=1+Wedges*2+16;
        private Vector3 SpireAt(int i,float t)
        {
            ref var p=ref spires[i];float u=NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01((t-Gather-p.Delay)/(Drop-Gather-p.Delay+.4f)));return p.Rest-new Vector3(0,46*(1-u),0);
        }

        // ---- the set ----
        private const string GridProp="grid";
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("dais")||!set.Has("old-plate")||!set.Has("mono-obelisk")||!set.Has("slab-00")||!set.Has("crown-spire"))return false;
            bool low=Context.Low;
            // The old arena until it is gone.
            if(t<Gather)
            {
                for(int i=0;i<old.Length;i++)
                {
                    OldPose(i,t,out var at,out var rot,out float solid,out float wire,out _);ref var p=ref old[i];
                    if(solid>.003f)
                    {
                        // the solid falls in on its own centroid, inside its wire
                        var c=Vector3.Transform(p.Centroid,rot);float neon=OldNeon(t,i);
                        set.Piece(p.Prop,at+c*(1-solid),rot,solid,Vector3.One,new Vector3(neon,neon,neon)*(1+1.5f*(1-solid)));
                    }
                    if(wire>.01f&&!(low&&p.Kind==OldPlate))set.Piece(p.Wire,at,rot,1,Vector3.One,Ice*wire);
                }
            }
            // The old world's shards orbit the presence, are thrown back by the body, wait, and are drawn in by the gathering.
            if(t>=Break+.8f&&t<Drop)for(int i=0;i<12;i++){if(Shard(i,t,out var at,out var rot,out float sc))set.Piece(ShardProps[i&3],at,rot,sc,new Vector3(.8f),Cyan*(.8f+1.4f*Flash(t,Body,.01f,.3f)));}
            // The void's grid: it shows as the arena lets go, breathes through the stillness and sinks to a trace under the new floor.
            float grid=NativeEntranceMath.Ramp(t,Break,Break+1.1f)*(1-.72f*NativeEntranceMath.Ramp(t,Drop,Drop+.6f))*(.85f+.25f*NativeEntranceMath.Bump(t,Still,Gather))*(1+.8f*Flash(t,Presence,.01f,.4f));
            if(grid>.01f)set.Piece(GridProp,new Vector3(0,GridY,0),Quaternion.Identity,1,Vector3.One,new Vector3(grid,grid,grid));
            // The architect's world: wireframes in flight, solid from the drop.
            for(int i=0;i<neo.Length;i++)
            {
                if(!NewPose(i,t,out var at,out var rot,out bool solid,out float wire,out float flash))continue;ref var p=ref neo[i];
                if(p.Kind==Crown&&t>=Drop){var turn=YawQ(CrownTurn(t));at=Vector3.Transform(at,turn);rot=turn*rot;}
                // (Low keeps the wireframe flight for the obelisks only: the rest fly as dark stone with their seams ablaze)
                bool wired=!low||p.Kind==Obelisk;
                if(solid)set.Piece(p.Prop,at,rot,1,Vector3.One,Settled(t,p.Kind,p.Reach)+Molten*(5*flash));
                else if(!wired)set.Piece(p.Prop,at,rot,1,new Vector3(.45f),Ice*(1.6f*wire));
                if(wire>.01f&&wired)set.Piece(p.Wire,at,rot,1,Vector3.One,Ice*wire);
            }
            if(t>=Gather)for(int i=0;i<spires.Length;i++){ref var p=ref spires[i];float g=.4f+.9f*NativeEntranceMath.Ramp(t,Drop-.3f,Drop+.4f)+2*Flash(t,Drop+.25f,.02f,.5f);set.Piece(p.Prop,SpireAt(i,t),YawQ(p.Yaw),1,Vector3.One,new Vector3(g,g,g));}
            return true;
        }
        /// <summary>The old pylons' neon (and the stage's lit grid) through the strike: full, stuttering with the glitches, out at the break.</summary>
        private float OldNeon(float t,int i)
        {
            float on=.7f+.5f*Flash(t,Strike,.005f,.5f);float cut=0;for(int k=0;k<Glitches.Length;k++)cut=Math.Max(cut,Flash(t,Glitches[k],.005f,.06f));
            return on*(1-.75f*cut)*(1-NativeEntranceMath.Ramp(t,Break-.05f,Break+.25f))+.15f;
        }
        /// <summary>A new piece's glow once locked: the seams steady (cyan to gold, as the bank grades them) and lifted by the light the
        /// hands pour out, a slow swell running out from the dais; the monoliths' veins and the crown's brighter; the name's sweep.</summary>
        private Vector3 Settled(float t,byte kind,float reach)
        {
            float b=kind==Crown?1.1f:kind==Obelisk?1.15f:1;float sweep=.12f*NativeEntranceMath.Bump(t,13.8f,15.0f);
            if(kind==Slab||kind==Dais)b*=1+.32f*Swell(t,reach);
            return new Vector3(b+sweep,b+sweep,b+sweep);
        }
        /// <summary>The poured light's swell at `reach` m from the dais (0..1): one slow wave out every 1.4 s once the hands pour.</summary>
        private static float Swell(float t,float reach)
        {
            float on=NativeEntranceMath.Ramp(t,Drop+.4f,Lower+.4f);if(on<=0)return 0;float ph=(t-Drop-.4f-reach/5.5f)/1.4f;ph-=(float)Math.Floor(ph);
            float s=(float)Math.Sin(Math.PI*ph);return on*s*s*s*s;
        }
        private static float CrownTurn(float t)=>.11f*NativeEntranceMath.EaseOut((t-Drop)/2.5f)+.02f*Math.Max(0,t-Drop-2.5f);
        /// <summary>Shard `i`: born from the old world near the break, orbiting the presence, thrown back by the body at 6.66, hanging
        /// still through the stillness, drawn in to the new floor's rim by the gathering (gone at the drop).</summary>
        private bool Shard(int i,float t,out Vector3 at,out Quaternion rot,out float scale)
        {
            int seed=9500+i*7;float born=Break+.8f+.5f*R(seed,1);at=Vector3.Zero;rot=Quaternion.Identity;scale=0;if(t<born)return false;
            float r0=3.2f+3.4f*R(seed,2),h=.6f+3.6f*R(seed,3),a0=R(seed,4)*6.2831853f,w=(.22f+.2f*R(seed,5))*(i%2==0?1:-1);
            // time runs for them until the stillness, then barely
            float run=Math.Min(t,Still)-born+.04f*Math.Max(0,t-Still);
            float push=NativeEntranceMath.EaseOut((t-Body)/.9f)*(t>Body?1:0)*(4+3*R(seed,6));float r=r0+push;
            var pos=new Vector3((float)Math.Sin(a0+w*run)*r,h+.25f*(float)Math.Sin(run*.7f+i),(float)Math.Cos(a0+w*run)*r);
            float draw=NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01((t-Gather)/(Drop-.1f-Gather)));
            var rim=Polar(7.6f,a0+w*run,NativeEntranceStage.FloorY-.3f);pos=Vector3.Lerp(pos,rim,draw);
            at=pos;rot=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(S(seed,7),.6f,S(seed,8))),run*(.8f+R(seed,9)));
            scale=(.35f+.55f*R(seed,10))*NativeEntranceMath.Ramp(t,born,born+.5f)*(1-NativeEntranceMath.Smooth((draw-.8f)/.2f));return scale>.01f;
        }
        protected override NativeIntroVoidLook VoidLook(float t)
        {
            var look=NativeIntroVoidLook.Of(Cyan);look.Far=NativeEntranceFx.Hex(0x03050b);look.Floor=NativeEntranceFx.Hex(0x080b12);look.Pool=t<Drop?Cyan:Gold;look.Rim=Cyan;look.Key=t<Break?1:.7f;look.PoolLevel=t<Draft?.3f:1;return look;
        }

        // =====================================================================================================================
        // Light
        // =====================================================================================================================
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();l.DriveProbe=true;
            // (the drawn model's chest sits about .2 m over the keyed chest anchor: hold 3's frames)
            var chest=Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,.2f,0);
            float arena=1-NativeEntranceMath.Ramp(t,Break,Break+.9f),drop=NativeEntranceMath.Ramp(t,Drop-.02f,Drop+.15f);
            // the sky: near black; at the drop it wells up deep blue and settles a shade over black
            float well=Flash(t,Drop+.12f,.08f,.9f);
            l.Background=Vector3.Lerp(NativeEntranceFx.Hex(0x02040a),NativeEntranceFx.Hex(0x05070f),arena)+NativeEntranceFx.Hex(0x060a1a)*drop+NativeEntranceFx.Hex(0x16245a)*(.8f*well);l.Fog=l.Background;
            l.FogNear=t<Break?10:t<Drop?18:30;l.FogFar=t<Break?40:t<Drop?150:185;
            l.Sky=NativeEntranceFx.Hex(0x6f8fbf,.18f+.2f*arena+.06f*drop);l.Ground=NativeEntranceFx.Hex(0x0f1a26,.25f+.03f*drop);
            float strike=Flash(t,Strike,.004f,.35f);
            // the key: soft and cool, low after the drop (the black glass keeps its gloss, the architect its form); the crown's gold
            // from above and behind, a rim
            l.Directional(new Vector3(2.5f,5,4),NativeEntranceFx.Hex(0xdfe8ff,.55f*arena+.16f*NativeEntranceMath.Ramp(t,Body,Body+.5f)*(1-drop)+.22f*drop));
            if(drop>0)l.Directional(new Vector3(-.6f,4,-3),Gold*(.5f*drop));
            // The strike: white at the centre, decaying to the thread of light that holds until the break.
            if(t<Break+.3f)l.Add(new Vector3(0,1.6f,0),Ice*(36*strike+2.5f*(1-NativeEntranceMath.Ramp(t,Break,Break+.3f))),14);
            // The old pylons' neon on the floor (two nearest the lens), out at the break.
            if(arena>.01f){float n=OldNeon(t,0)*arena;l.Add(new Vector3(3.6f,2,6.4f),Neon*(2.2f*n),7);l.Add(new Vector3(-6.4f,2,3.6f),Neon*(1.8f*n),7);}
            // The void's cold light from the grid below.
            float voidL=NativeEntranceMath.Ramp(t,Break,Break+1.2f)*(1-.6f*drop);if(voidL>.01f)l.Add(new Vector3(0,-1.2f,0),Cyan*(1.4f*voidL),12);
            // The presence: a point of light, pulsing on the sub's hits, becoming the chest's core as the body forms.
            if(t>=Presence-.05f&&t<Body+.7f)
            {
                float pres=NativeEntranceMath.Ramp(t,Presence-.02f,Presence+.1f)*(1+1.2f*Flash(t,PulseA,.01f,.15f)+1.2f*Flash(t,PulseB,.01f,.15f)+2.5f*Flash(t,Body,.01f,.3f))*(1-NativeEntranceMath.Ramp(t,Body,Body+.6f));
                if(pres>.01f)l.Add(t<Body?new Vector3(0,PresenceY,0):chest+new Vector3(0,0,.3f),Ice*(4.5f*pres),6);
            }
            // The body's rims from the ignition (cyan from behind its left, ice from behind its right), so it stands out of the void.
            if(t>=Body-.1f){float rim=NativeEntranceMath.Ramp(t,Body-.1f,Body+.2f);l.Add(chest+new Vector3(.95f,.75f,-1.45f),Cyan*(11*rim*(1-.35f*drop)),5);l.Add(chest+new Vector3(-1.05f,.45f,-1.3f),Ice*(8*rim*(1-.3f*drop)),5);}
            // The visor's own light on the helmet and the shoulders.
            float visor=VisorLevel(t);if(visor>.02f)l.Add(VisorAt(t)+ForwardOf(t)*.3f,Molten*(1.1f*visor),1.6f);
            // The gathering's wires lighting the void; the inhale dims everything.
            if(t>=Gather&&t<Drop+.1f){float g=NativeEntranceMath.Ramp(t,Gather,Inhale)*(1-.6f*NativeEntranceMath.Bump(t,Inhale-.05f,DipB+.1f));l.Add(new Vector3(0,3,0),Cyan*(3.2f*g),16);}
            // THE DROP: the flash; the poured light low under the architect (the seams' glow lifting the black glass's gloss); the crown's
            // gold from above; the name's light.
            float hit=Flash(t,Drop,.004f,.18f);if(hit>.01f)l.Add(new Vector3(0,2.3f,.9f),Ice*(26*hit),12);
            if(drop>0)
            {
                float pour=NativeEntranceMath.Ramp(t,Drop+.25f,Lower+.3f);
                l.Add(new Vector3(0,.55f,.2f),Cyan*(2.4f*pour+1.2f*drop),4.5f);
                l.Add(new Vector3(0,CrownY+2,0),Gold*(3.4f*drop),26);
                float name=NameLevel(t);if(name>.01f)l.Add(NameCentre+new Vector3(0,0,.6f),Gold*(3.6f*name),10);
            }
        }

        // =====================================================================================================================
        // Effects
        // =====================================================================================================================
        private NativeEntranceView view;
        /// <summary>The camera this frame (the runtime's own: the rig at the presentation clock, held after SettleAt), for laying the
        /// long streaks onto their heads' camera planes.</summary>
        private void ViewAt(float t)=>Camera(t,out view);
        /// <summary>A line of light from `tail` to `head` (its head the bright end), laid onto the head's camera plane when over 1 m.</summary>
        private void Line(INativeEntranceFx fx,Vector3 head,Vector3 tail,Vector3 color,float width,float taper,int seed)
        {
            var axis=head-tail;float len=axis.Length();if(len<1e-4f)return;
            if(len>NativeCacheOpenFx.ExactStreak)
            {
                var fw=view.Look-view.Eye;if(fw.LengthSquared()>1e-8f)fw=Vector3.Normalize(fw);float ax=axis.X,ay=axis.Y,az=axis.Z;
                if(NativeCacheOpenFx.OntoHeadPlane(view.Eye.X,view.Eye.Y,view.Eye.Z,fw.X,fw.Y,fw.Z,.2f,head.X,head.Y,head.Z,ref ax,ref ay,ref az,ref taper))axis=new Vector3(ax,ay,az);else return;
            }
            fx.Streak(head,axis,color,width,.001f,1,taper,0,seed);
        }
        /// <summary>A line of light lying on the floor, in pieces under a metre (each at its own depth, so the black glass never hides a
        /// part of it as one laid on a far camera plane would be); `head` the bright end.</summary>
        private void FloorLine(INativeEntranceFx fx,Vector3 head,Vector3 tail,Vector3 color,float width,int seed)
        {
            var axis=head-tail;float len=axis.Length();if(len<1e-4f)return;int n=Math.Max(1,(int)Math.Ceiling(len/.9f));
            for(int i=0;i<n;i++){var a=tail+axis*((float)i/n);var b=tail+axis*((float)(i+1)/n);fx.Streak(b,b-a,color,width,.001f,1,0,0,seed*16+i);}
        }
        private Vector3 CamRight{get{var f=view.Look-view.Eye;if(f.LengthSquared()<1e-8f)return Vector3.UnitX;var r=Vector3.Cross(Vector3.Normalize(f),Up);return r.LengthSquared()<1e-8f?Vector3.UnitX:Vector3.Normalize(r);}}
        private Vector3 CamUp(Vector3 right){var f=view.Look-view.Eye;if(f.LengthSquared()<1e-8f)return Up;return Vector3.Normalize(Vector3.Cross(right,Vector3.Normalize(f)));}

        // ---- code glyphs: tiny 3 x 5 characters drawn as two or three strokes, facing the camera ----
        // (an original set; points in a 2 x 4 cell, stroke pairs)
        private static readonly float[][] Glyph=
        {
            new[]{1f,0,1,4},new[]{0f,4,2,4,2,4,.6f,0},new[]{1f,1,1,3,0,2,2,2},new[]{0f,1.4f,2,1.4f,0,2.6f,2,2.6f},new[]{0f,4,0,0,0,0,2,0},new[]{0f,4,2,4,1,4,1,0},
            new[]{2f,4,0,2,0,2,2,0},new[]{0f,4,2,2,2,2,0,0},new[]{0f,0,2,4},new[]{0f,4,2,0},new[]{0f,0,0,4,0,4,2,4,2,4,2,0},new[]{0f,0,0,4,2,0,2,4,0,2,2,2},
            new[]{0f,0,0,4,0,4,2,0,2,0,2,4},new[]{0f,4,2,4,2,4,0,0,0,0,2,0},new[]{1.6f,0,1.6f,4,1.6f,4,0,1.4f,0,1.4f,2,1.4f},new[]{0f,4,2,4,.4f,2,1.6f,2,0,0,2,0},
        };
        private void DrawGlyph(INativeEntranceFx fx,int g,Vector3 at,float size,Vector3 color,Vector3 right,Vector3 up,int seed)
        {
            var s=Glyph[g&15];float k=size*.25f;var o=at-right*k-up*(k*2);float w=size*.11f;
            for(int i=0;i+3<s.Length;i+=4){var a=o+right*(s[i]*k)+up*(s[i+1]*k);var b=o+right*(s[i+2]*k)+up*(s[i+3]*k);fx.Streak(b,b-a,color,w,.001f,1,.15f,0,seed+i);}
        }

        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            ViewAt(t);float floor=NativeEntranceStage.FloorY;var right=CamRight;var up=CamUp(right);bool low=Context.Low;float thin=low?.42f:1;
            StrikeFx(fx,t,q,thin);
            if(t<Break+.5f)Cracks(fx,t,thin);
            TearFx(fx,t);
            if(t>=Break&&t<Gather+.5f)Dissolve(fx,t,thin,right,up);
            if(t>=Break+.4f&&t<Drop+.3f)Field(fx,t,thin,right,up);
            if(t>=Presence-.02f&&t<Body+.5f)PresenceFx(fx,t,thin,right,up);
            if(t>=Draft&&t<Body+.5f)DraftFx(fx,t,thin,right,up,q);
            if(t>=Body-.05f)VisorFx(fx,t,thin);
            if(t>=Body-.05f)Aura(fx,t,thin,right,up,q);
            if(t>=Still&&t<Drop+.15f)Blueprint(fx,t,thin);
            if(t>=Gather&&t<Drop+.1f)Trails(fx,t,thin);
            if(t>=Drop-.02f)DropFx(fx,t,thin,floor,q);
            if(t>=Drop)HandsFx(fx,t,thin,floor);
            if(t>=NameStart-GlyphFlight)NameFx(fx,t,thin,right,up);
            if(t>=Drop)NativeIntroVoid.Motes(fx,t,q,Gold*.8f,4300);
        }
        // The visor's light: kindled with the body, steady while it floats, drawn in on the inhale, kindling through the close-up, white
        // at the drop and blazing with the name: a glow on the visor's face and a thin flare across it (along the head's right).
        private float VisorLevel(float t)
        {
            if(t<Body-.03f)return 0;float on=NativeEntranceMath.Ramp(t,Body-.03f,Body+.08f);
            float steady=.6f*on*(1-.65f*NativeEntranceMath.Ramp(t,Inhale-.05f,Inhale+.15f));
            float kindle=t>=Inhale?.25f+.85f*NativeEntranceMath.Ramp(t,Inhale+.15f,Drop):0;
            float blaze=t>=Drop?1.25f:0;
            return Math.Max(steady,Math.Max(kindle,blaze))+1.4f*Flash(t,Body,.01f,.2f)+1.6f*Flash(t,Drop,.01f,.3f);
        }
        private void VisorFx(INativeEntranceFx fx,float t,float thin)
        {
            float lvl=VisorLevel(t);if(lvl<.01f)return;var v=VisorAt(t);HeadFrame(t,out var hu,out var hf);var hr=Vector3.Cross(hu,hf);if(hr.LengthSquared()<1e-6f)return;hr=Vector3.Normalize(hr);
            // (ice while the body floats, as its accent is; white-gold from the drop, with the name)
            var hot=t<Drop?Ice:Molten;fx.Glow(v,hot*(1.5f*lvl),0,.13f+.06f*lvl,1,0,12100);
            fx.Glow(v,Ice*(.45f*lvl),0,.5f+.3f*lvl,0,0,12101);
            float flare=.14f+.12f*lvl;Line(fx,v+hr*flare,v-hr*flare,Ice*(1.5f*lvl),.007f+.004f*lvl,.6f,12102);
            if(thin>=1&&lvl>.9f){float k=lvl-.9f;Line(fx,v+hr*(flare*2.4f),v-hr*(flare*2.4f),Cyan*(.6f*k),.004f,.85f,12103);}
        }
        // The architect's own light: glyph-light circling the body while it floats, a soft glow behind it and cyan motes rising round it
        // (the aura); after the drop a soft gold glow behind the head.
        private void Aura(INativeEntranceFx fx,float t,float thin,Vector3 right,Vector3 up,float q)
        {
            var chest=Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,.2f,0);float on=NativeEntranceMath.Ramp(t,Body,Body+.6f)*(1-NativeEntranceMath.Ramp(t,Drop-.05f,Drop+.25f));
            if(on>.01f)
            {
                int n=(int)(12*thin+.5f);
                for(int i=0;i<n;i++)
                {
                    int seed=11000+i*7;float a=R(seed,1)*6.2831853f+t*(.55f+.3f*R(seed,2))*(i%2==0?1:-1),r=.6f+.35f*R(seed,3),h=-1.05f+1.75f*R(seed,4)+.08f*(float)Math.Sin(t*1.3f+i);
                    var p=new Vector3(chest.X+(float)Math.Sin(a)*r,chest.Y+h,chest.Z+(float)Math.Cos(a)*r);float lvl=on*(.5f+.5f*(float)Math.Sin(t*1.7f+i*2.1f));
                    if(i%3==0)fx.Glow(p,Ice*(1.2f*lvl),0,.04f,0,0,seed);else DrawGlyph(fx,(int)(R(seed,5)*16),p,.07f,GlyphTint*(1.3f*lvl),right,up,seed);
                }
                // a soft cyan glow behind the body (away from the lens), and motes rising round it from its feet
                var back=view.Eye-chest;back=back.LengthSquared()>1e-6f?Vector3.Normalize(back):Vector3.UnitZ;
                fx.Glow(chest-back*.45f,Cyan*(.34f*on),0,2.1f,1,0,11180);fx.Glow(chest-back*.4f-new Vector3(0,.5f,0),Deep*(.22f*on),0,2.6f,1,0,11181);
                var feet=(Anchor(NativeEntranceAnchor.LeftFoot,t)+Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;
                NativeEntranceFx.Embers(fx,t,Body+.1f,Drop,feet+new Vector3(0,.1f,0),.5f,thin<1?6:14,.9f,1.7f,Cyan*(.9f*on),.022f,11200,q,.5f);
            }
            float after=NativeEntranceMath.Ramp(t,Drop+.05f,Drop+.6f)*(1-NativeEntranceMath.Ramp(t,FadeFrom,Total));
            if(after>.01f){var head=Anchor(NativeEntranceAnchor.Head,t);var f=ForwardOf(t);fx.Glow(head-f*.32f+new Vector3(0,.18f,0),Gold*(.5f*after),0,1.2f,1,0,11250);}
            else if(t>=Still&&t<Drop)fx.Glow(new Vector3(0,GridY+.03f,0),Cyan*(.35f*NativeEntranceMath.Ramp(t,Still,Still+1)),0,2.4f,0,0,11252);
        }
        // The strike: a column of light out of the dark onto the dais; sparks thrown over the deck; a thread of light until the break.
        private void StrikeFx(INativeEntranceFx fx,float t,float q,float thin)
        {
            if(t>=Break+.35f)return;float hit=Flash(t,Strike,.004f,.22f),hold=(1-NativeEntranceMath.Ramp(t,Break-.1f,Break+.3f));
            float width=.06f+.55f*hit;var top=new Vector3(0,42,0);var foot=new Vector3(0,NativeEntranceStage.FloorY+.02f,0);
            Line(fx,foot,top,Ice*(2.2f+6*hit)*hold,width,.2f,6001);Line(fx,foot,top,Cyan*(1.1f+2*hit)*hold,width*3.2f,.5f,6002);
            fx.Glow(foot+new Vector3(0,.15f,0),Ice*(3+9*hit)*hold,0,.8f+2.4f*hit,1,0,6003);
            if(t<.9f)NativeEntranceFx.Sparks(fx,t,Strike,foot+new Vector3(0,.05f,0),new Vector3(0,.35f,0),1.2f,(int)(46*thin),7.5f,.75f,9.8f,.6f,Ice*3,Cyan*1.5f,.016f,6010,Math.Max(q,.6f),NativeEntranceStage.FloorY);
        }
        // Cracks of light racing out over the deck from the strike, cooling; they go with the plates.
        private void Cracks(INativeEntranceFx fx,float t,float thin)
        {
            int lines=(int)(11*thin+.5f);float fade=1-NativeEntranceMath.Ramp(t,Break,Break+.4f);if(fade<=0)return;
            for(int l=0;l<lines;l++)
            {
                float ang=l/(float)lines*6.2831853f+S(6100,l)*.22f;var prev=new Vector3(0,NativeEntranceStage.FloorY+.004f,0);float born=.02f*l;
                for(int k=1;k<=12;k++)
                {
                    float reachAt=born+k*.085f;if(t<reachAt)break;ang+=S(6101+l,k)*.38f;float step=.55f+.2f*R(6102+l,k);
                    var p=prev+new Vector3((float)Math.Sin(ang)*step,0,(float)Math.Cos(ang)*step);float rr=new Vector2(p.X,p.Z).Length();p.Y=(rr<2.2f?NativeEntranceStage.FloorY:Ground)+.004f;
                    float heat=(1-NativeEntranceMath.Smooth((t-reachAt)/1.6f)*.65f)*fade*(1+1.5f*TearLevel(t));
                    Line(fx,p,prev,Vector3.Lerp(Ice*2.6f,Cyan*1.4f,NativeEntranceMath.Clamp01((t-reachAt)*2))*heat,.022f-.001f*k,0,6200+l*16+k);prev=p;
                }
            }
        }
        // The tears: two thin lines across the band, split in colour, for the glitch's frames.
        private void TearFx(INativeEntranceFx fx,float t)
        {
            float lvl=TearLevel(t);if(lvl<.02f)return;float y=TearY(t);var right=CamRight;var centre=view.Look+(new Vector3(0,y,0)-new Vector3(0,view.Look.Y,0));
            float span=7;var a=centre-right*span;var b=centre+right*span;float off=Context.Reduced?.01f:.035f;
            Line(fx,b+new Vector3(0,.9f,0),a+new Vector3(0,.9f,0),Ice*(1.6f*lvl),.012f,0,6400);Line(fx,b-new Vector3(0,.9f,0),a-new Vector3(0,.9f,0),Ice*(1.2f*lvl),.01f,0,6401);
            Line(fx,b+new Vector3(0,.9f+off,0)+right*.05f,a+new Vector3(0,.9f+off,0)+right*.05f,Rose*(1.1f*lvl),.01f,0,6402);Line(fx,b-new Vector3(0,.9f+off,0)-right*.05f,a-new Vector3(0,.9f+off,0)-right*.05f,Blue*(1.1f*lvl),.01f,0,6403);
        }
        // The dissolve: glyphs boiling off each piece as it falls in to its wire, rising and fading into light.
        private void Dissolve(INativeEntranceFx fx,float t,float thin,Vector3 right,Vector3 up)
        {
            for(int i=0;i<old.Length;i++)
            {
                ref var p=ref old[i];float tau=t-p.Lift;if(tau<.3f||tau>2.6f)continue;int n=(int)((p.Kind==OldSky?7:p.Kind==OldPlate?4:3)*thin+.5f);
                for(int g=0;g<n;g++)
                {
                    int seed=p.Seed*31+g;float born=p.Lift+.35f+.85f*R(seed,1);float a=t-born;if(a<0||a>1.25f)continue;
                    OldPose(i,born,out _,out _,out _,out _,out var c);
                    var at=c+new Vector3(S(seed,2),S(seed,3)*.6f,S(seed,4))*(p.Size*.5f)+new Vector3(0,.55f*a+.25f*a*a,0);float u=a/1.25f;
                    float size=(p.Kind==OldSky?.5f:.11f)*(1+.4f*R(seed,5));var col=Vector3.Lerp(Ice*2.2f,GlyphTint*1.2f,u)*(1-u*u);
                    if(u<.7f)DrawGlyph(fx,(int)(R(seed,6)*16+t*3),at,size,col,right,up,seed);else fx.Glow(at,col*1.5f,0,size*.6f,0,u,seed);
                }
            }
        }
        // The field: glyphs adrift round the centre; frozen through the stillness; swept to the new floor by the gathering.
        private void Field(INativeEntranceFx fx,float t,float thin,Vector3 right,Vector3 up)
        {
            int n=(int)(46*thin);float frozen=Math.Min(t,Still)+.03f*Math.Max(0,t-Still);float show=NativeEntranceMath.Ramp(t,Break+.4f,Break+1.4f)*(1-NativeEntranceMath.Smooth((t-Drop+.25f)/.25f));
            float gather=NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01((t-Gather)/(Drop-Gather)));
            for(int i=0;i<n;i++)
            {
                int seed=6600+i*11;float r=2.2f+6.5f*R(seed,1),a0=R(seed,2)*6.2831853f,h=.2f+4.8f*R(seed,3),w=(.05f+.1f*R(seed,4))*(i%2==0?1:-1);
                float a=a0+w*frozen;var pos=new Vector3((float)Math.Sin(a)*r,h+.12f*(float)Math.Sin(frozen*.8f+i),(float)Math.Cos(a)*r);
                var dest=Polar(1.5f+5.5f*R(seed,5),a,NativeEntranceStage.FloorY+.05f);pos=Vector3.Lerp(pos,dest,gather);
                float lvl=show*(.55f+.45f*(float)Math.Sin(i*1.7f+frozen*1.3f))*(1+.8f*Flash(t,Presence,.01f,.4f));if(lvl<.03f)continue;
                DrawGlyph(fx,(int)(R(seed,6)*16+frozen*.7f),pos,.1f+.06f*R(seed,7),GlyphTint*(1.1f*lvl),right,up,seed);
            }
        }
        // The presence: a point of light, its halo and flare, glyphs spiralling into it, pulses on the sub's hits.
        private void PresenceFx(INativeEntranceFx fx,float t,float thin,Vector3 right,Vector3 up)
        {
            var c=new Vector3(0,PresenceY,0);float on=NativeEntranceMath.Ramp(t,Presence-.02f,Presence+.08f)*(1-NativeEntranceMath.Ramp(t,Body+.05f,Body+.45f));if(on<=0)return;
            float pulse=1+1.4f*Flash(t,PulseA,.01f,.14f)+1.4f*Flash(t,PulseB,.01f,.14f)+2.2f*Flash(t,Presence,.005f,.3f)+3*Flash(t,Body,.005f,.25f);float grow=1+.6f*NativeEntranceMath.Ramp(t,Draft,Body);
            // (from the draft the point sinks into the body of light: dimmer, so the figure, not the star, holds the frame)
            float fade=1-.6f*NativeEntranceMath.Ramp(t,Draft+.2f,Draft+.9f);
            fx.Glow(c,Vector3.One*(3.2f*on*pulse*fade),0,.14f*grow,1,0,6800);fx.Glow(c,Ice*(1.4f*on*pulse*fade),0,.55f*grow,1,0,6801);fx.Glow(c,Cyan*(.45f*on*pulse*fade),0,1.8f*grow,0,0,6802);
            Line(fx,c+right*(.55f*grow*pulse),c-right*(.55f*grow*pulse),Ice*(2.2f*on*fade),.012f,.9f,6803);Line(fx,c-right*(.55f*grow*pulse),c+right*(.55f*grow*pulse),Ice*(2.2f*on*fade),.012f,.9f,6804);
            Line(fx,c+up*(.32f*grow*pulse),c-up*(.32f*grow*pulse),Ice*(1.5f*on*fade),.01f,.9f,6805);
            // the streams: glyphs on spirals into the point
            int n=(int)(28*thin*fade);
            for(int i=0;i<n;i++)
            {
                int seed=6900+i*13;float period=1.6f+.8f*R(seed,1);float u=((t-Presence)/period+R(seed,2))%1f;if(u<0)u+=1;float a=R(seed,3)*6.2831853f+u*5*(i%2==0?1:-1);
                float rr=(2.8f+1.6f*R(seed,4))*(1-u)*(1-u);var pos=c+new Vector3((float)Math.Sin(a)*rr,(S(seed,5)*1.6f)*(1-u),(float)Math.Cos(a)*rr);
                var col=Vector3.Lerp(GlyphTint,Ice*2,u)*(on*NativeEntranceMath.Smooth(u*4)*(1-u*u));
                if(u<.75f)DrawGlyph(fx,(int)(R(seed,6)*16),pos,.09f*(1-u*.5f),col,right,up,seed);else fx.Glow(pos,col*2,0,.05f,0,u,seed);
            }
            // the bursts on the hits
            if(t<PulseB+.8f){NativeEntranceFx.Sparks(fx,t,Presence,c,Vector3.Zero,1,(int)(30*thin),3.2f,.7f,0,1.6f,Ice*2.4f,GlyphTint,.012f,7000,1,-10);NativeEntranceFx.Sparks(fx,t,PulseA,c,Vector3.Zero,1,(int)(16*thin),2.2f,.5f,0,1.8f,Ice*2,GlyphTint,.01f,7040,1,-10);}
        }
        // ---- the draft ----
        // The body of light is the model's own silhouette (Ghosts: cyan, its edges bright, a soft fill). Over it: pens of light trace its
        // outline in wire from the chest out (each limb's two silhouette edges as the lens sees them, and its centre line), a scan
        // sweeps up it and back down, light falls into it as it condenses, and at the ignition a burst throws rays, sparks and the
        // shards out. (The keyed shoulders, pelvis, knees and feet sit on the model's; its hands are its own, handed in each frame.)
        private enum Pt:byte{Pelvis,Neck,Crown,Skull,LShoulder,LElbow,LHand,RShoulder,RElbow,RHand,LHip,LKnee,LFoot,RHip,RKnee,RFoot,Count}
        private static readonly byte[] LimbA={(byte)Pt.Pelvis,(byte)Pt.Skull,(byte)Pt.LShoulder,(byte)Pt.LElbow,(byte)Pt.RShoulder,(byte)Pt.RElbow,(byte)Pt.LHip,(byte)Pt.LKnee,(byte)Pt.RHip,(byte)Pt.RKnee};
        private static readonly byte[] LimbB={(byte)Pt.Neck,(byte)Pt.Crown,(byte)Pt.LElbow,(byte)Pt.LHand,(byte)Pt.RElbow,(byte)Pt.RHand,(byte)Pt.LKnee,(byte)Pt.LFoot,(byte)Pt.RKnee,(byte)Pt.RFoot};
        private static readonly float[] LimbRA={.17f,.13f,.075f,.062f,.075f,.062f,.095f,.072f,.095f,.072f},LimbRB={.2f,.12f,.062f,.05f,.062f,.05f,.072f,.058f,.072f,.058f};
        private static readonly float[] LimbOrder={0,.5f,1,2,1,2,1,2,1,2};
        private readonly Vector3[] pts=new Vector3[(int)Pt.Count];
        /// <summary>The body's points at `t` as the model stands (see the draft's note), into `pts`.</summary>
        private void BodyPoints(float t)
        {
            var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);var ls=Anchor(NativeEntranceAnchor.LeftShoulder,t);var rs=Anchor(NativeEntranceAnchor.RightShoulder,t);var skull=Anchor(NativeEntranceAnchor.Head,t);
            HeadFrame(t,out var hu,out _);
            pts[(int)Pt.Pelvis]=pelvis;pts[(int)Pt.Neck]=(ls+rs)*.5f;pts[(int)Pt.Skull]=skull;pts[(int)Pt.Crown]=skull+hu*.36f;pts[(int)Pt.LShoulder]=ls;pts[(int)Pt.RShoulder]=rs;
            var lh=HandAt(true,t);var rh=HandAt(false,t);pts[(int)Pt.LHand]=lh;pts[(int)Pt.RHand]=rh;
            // the elbows where the model bends them: the keyed elbow carried half the way the drawn hand moved from the keyed one
            pts[(int)Pt.LElbow]=Anchor(NativeEntranceAnchor.LeftElbow,t)+(lh-Anchor(NativeEntranceAnchor.LeftHand,t))*.5f;pts[(int)Pt.RElbow]=Anchor(NativeEntranceAnchor.RightElbow,t)+(rh-Anchor(NativeEntranceAnchor.RightHand,t))*.5f;
            var side=(ls-rs)*.24f;pts[(int)Pt.LHip]=pelvis+side;pts[(int)Pt.RHip]=pelvis-side;
            pts[(int)Pt.LKnee]=Anchor(NativeEntranceAnchor.LeftKnee,t);pts[(int)Pt.RKnee]=Anchor(NativeEntranceAnchor.RightKnee,t);pts[(int)Pt.LFoot]=Anchor(NativeEntranceAnchor.LeftFoot,t);pts[(int)Pt.RFoot]=Anchor(NativeEntranceAnchor.RightFoot,t);
        }
        private void DraftFx(INativeEntranceFx fx,float t,float thin,Vector3 right,Vector3 up,float q)
        {
            float fade=1-NativeEntranceMath.Ramp(t,Condense+.05f,Body+.08f);BodyPoints(t);
            if(fade>.01f)
            {
                // the pens: each limb's silhouette edges (both sides, as the lens sees them) and its centre line, drawn out from the chest
                for(int b=0;b<LimbA.Length;b++)
                {
                    float start=Draft+.12f+LimbOrder[b]*.3f;float u=NativeEntranceMath.EaseOut((t-start)/.42f);if(u<=0)continue;
                    var a=pts[LimbA[b]];var e=pts[LimbB[b]];var seg=e-a;if(seg.LengthSquared()<1e-6f)continue;var look=(a+e)*.5f-view.Eye;
                    var perp=Vector3.Cross(seg,look);if(perp.LengthSquared()<1e-8f)continue;perp=Vector3.Normalize(perp);
                    float shimmer=.85f+.15f*(float)Math.Sin(t*4.1f+b*1.7f);var lvl=fade*shimmer;var head=Vector3.Lerp(a,e,u);float ra=LimbRA[b],rb=NativeEntranceMath.Lerp(LimbRA[b],LimbRB[b],u);
                    Line(fx,head+perp*rb,a+perp*ra,Cyan*(1.9f*lvl),.011f,.25f,7100+b*4);Line(fx,head-perp*rb,a-perp*ra,Cyan*(1.9f*lvl),.011f,.25f,7101+b*4);
                    if(thin>=1)Line(fx,head,a,GlyphTint*(.7f*lvl),.006f,.4f,7102+b*4);
                    if(u<1){fx.Glow(head+perp*rb,Vector3.One*(2.2f*lvl),0,.045f,1,0,7120+b*2);fx.Glow(head-perp*rb,Vector3.One*(2.2f*lvl),0,.045f,1,0,7121+b*2);}
                }
                // the scan: a plane of light sweeping up the body and back down (a line across it, a soft band under it, sparks where it
                // meets the edges)
                float sweep=-1;if(t>=Draft+.25f&&t<Draft+.85f)sweep=NativeEntranceMath.Smooth((t-Draft-.25f)/.6f);else if(t>=Draft+.9f&&t<Condense+.05f)sweep=1-NativeEntranceMath.Smooth((t-Draft-.9f)/(Condense+.05f-Draft-.9f));
                if(sweep>=0)
                {
                    float y0=Math.Min(pts[(int)Pt.LFoot].Y,pts[(int)Pt.RFoot].Y)-.05f,y1=pts[(int)Pt.Crown].Y+.06f;float y=y0+(y1-y0)*sweep;var c=new Vector3(pts[(int)Pt.Pelvis].X,y,pts[(int)Pt.Pelvis].Z);
                    var across=new Vector3(right.X,0,right.Z);if(across.LengthSquared()<1e-6f)across=Vector3.UnitX;across=Vector3.Normalize(across);float half=.46f;
                    Line(fx,c+across*half,c-across*half,Ice*(2.6f*fade),.01f,0,7180);Line(fx,c+across*half-new Vector3(0,.035f,0),c-across*half-new Vector3(0,.035f,0),Cyan*(.5f*fade),.07f,0,7181);
                    fx.Glow(c+across*half,Ice*(1.2f*fade),0,.06f,1,0,7182);fx.Glow(c-across*half,Ice*(1.2f*fade),0,.06f,1,0,7183);
                }
                // the inner glow: the core of light in the chest the body is drawn round, swelling toward the condense
                var core=(pts[(int)Pt.Neck]*.6f+pts[(int)Pt.Pelvis]*.4f);float swell=NativeEntranceMath.Ramp(t,Draft,Condense);
                fx.Glow(core,Cyan*(.7f*fade*(.6f+.6f*swell)),0,.55f+.35f*swell,1,0,7190);fx.Glow(core,Ice*(1.4f*fade*swell),0,.16f,1,0,7191);
            }
            // the condensing: glyph-light falling in to every part of the body from all round
            if(t>=Condense-.35f&&t<Body+.1f)
            {
                int n=(int)(40*thin);
                for(int i=0;i<n;i++)
                {
                    int seed=7200+i*5;float born=Condense-.35f+.55f*R(seed,1);float u=(t-born)/.32f;if(u<0||u>1)continue;var target=pts[2+i%((int)Pt.Count-2)];
                    var from=target+NativeEntranceMath.Direction(seed,2)*(1.4f+1.2f*R(seed,3));var p=Vector3.Lerp(from,target,NativeEntranceMath.EaseIn(u));
                    Line(fx,p,Vector3.Lerp(from,target,NativeEntranceMath.EaseIn(Math.Max(0,u-.15f))),Vector3.Lerp(GlyphTint,Ice*2.4f,u),.012f,.6f,seed);
                }
            }
            // the ignition: a white flash in the chest and rays of light thrown out all round (never a ring), the shell of sparks
            float s=t-Body;
            if(s>=0&&s<.5f)
            {
                var chest=(pts[(int)Pt.Neck]*.6f+pts[(int)Pt.Pelvis]*.4f);float flash=Flash(t,Body,.005f,.14f);
                fx.Glow(chest,Ice*(7*flash),0,.9f+1.6f*flash,1,0,7290);
                int rays=(int)(18*thin+.5f);float u=s/.5f;
                for(int i=0;i<rays;i++)
                {
                    var d=NativeEntranceMath.Direction(7400,i);float len=.5f+2.6f*NativeEntranceMath.EaseOut(u)*(.6f+.4f*R(7401,i));float w=(1-u)*(1-u);
                    Line(fx,chest+d*len,chest+d*(len*.3f),Ice*(3.2f*w),.016f*(1-.5f*u),.7f,7410+i);
                }
            }
            NativeEntranceFx.Sparks(fx,t,Body,(pts[(int)Pt.Neck]*.6f+pts[(int)Pt.Pelvis]*.4f),Vector3.Zero,1,(int)(44*thin),6.5f,.6f,0,1.4f,Ice*2.6f,Cyan*1.2f,.016f,7300,1,-10);
        }
        // The blueprint: pens of light draw the new world's plan in the void (the floor's sixteen seams out from the dais's rays, the
        // monoliths' edges up out of the dark, their capstones, the crown's eight spires leaning in), the lines holding, faint, until the
        // pieces arrive into them.
        private void Blueprint(INativeEntranceFx fx,float t,float thin)
        {
            float fade=(1-NativeEntranceMath.Smooth((t-Drop+.05f)/.12f))*(1-.4f*NativeEntranceMath.Bump(t,Inhale-.04f,DipA+.1f));float y=NativeEntranceStage.FloorY+.01f;
            // the seams: from the dais's centre out to its corner, then on to the rim
            for(int i=0;i<Wedges;i++)
            {
                if(thin<1&&(i&1)==1)continue;
                float a=(i+.5f)*2*WedgeHalf;float start=Still+.1f+.15f*R(7400,i);float u=NativeEntranceMath.EaseOut((t-start)/.8f);if(u<=0)continue;
                var a0=Polar(.3f,a,y);var a1=Polar(FloorR,a,y);var head=Vector3.Lerp(a0,a1,u);float lvl=(1.3f+2.0f*(1-NativeEntranceMath.Smooth((t-start-.8f)/.6f)))*fade;
                Line(fx,head,a0,Cyan*lvl,.014f,.2f,7500+i);if(u<1)fx.Glow(head,Ice*2.2f,0,.07f,1,0,7600+i);
            }
            // the monoliths: four edges each, drawn up from the plinth to the capstone, then the capstone's four lines to its point
            for(int m=0;m<8;m++)
            {
                if(thin<1&&(m&1)==1)continue;
                float a=(m+.5f)/8*6.2831853f;var foot=Polar(MonoRing,a,MonoFoot);float start=Still+.9f+.11f*m;float u=NativeEntranceMath.EaseOut((t-start)/.7f);if(u<=0)continue;
                float lvl=(.8f+1.8f*(1-NativeEntranceMath.Smooth((t-start-.7f)/.7f)))*fade;var tip=new Vector3(foot.X,MonoTip,foot.Z);
                for(int c=0;c<4;c++)
                {
                    var off=NativeEntranceMath.RotateY(new Vector3(c<2?.55f:-.55f,0,c==0||c==3?.55f:-.55f),a);var off1=off*(.35f/.55f);
                    var b0=foot+off;var b1=foot+new Vector3(0,9.6f,0)+off1;var head=Vector3.Lerp(b0,b1,u);Line(fx,head,b0,Cyan*lvl,.016f,.15f,7700+m*8+c);
                    float v=NativeEntranceMath.EaseOut((t-start-.75f)/.35f);if(v>0)Line(fx,Vector3.Lerp(b1,tip,v),b1,Ice*(lvl*1.2f),.014f,.1f,7740+m*8+c);
                }
                if(u<1)fx.Glow(foot+new Vector3(0,9.6f*u,0),Ice*2.4f,0,.12f,1,0,7800+m);
            }
            // the crown: each spire's line drawn from its root in toward the apex, gold
            for(int i=0;i<8;i++)
            {
                if(thin<1&&(i&1)==1)continue;
                ref var p=ref neo[CrownFirst+i];float start=Gather-1.2f+.06f*i;float u=NativeEntranceMath.EaseOut((t-start)/.6f);if(u<=0)continue;
                var tip=p.Rest+Vector3.Transform(new Vector3(0,SpireLength,0),p.Turn);var head=Vector3.Lerp(p.Rest,tip,u);
                Line(fx,head,p.Rest,Gold*(1.1f*fade),.016f,.2f,7860+i);if(u<1)fx.Glow(head,Molten*1.8f,0,.12f,1,0,7870+i);
            }
            // the axis: one line up from the dais to where the crown's spires lean together
            float ax=NativeEntranceMath.EaseOut((t-(Gather-.55f))/.5f);if(ax>0)Line(fx,new Vector3(0,.2f+(CrownApex-.2f)*ax,0),new Vector3(0,.2f,0),Ice*(1.4f*fade),.014f,.3f,7820);
        }
        // The gathering's trails: every flying piece draws a streak of light behind it (long on the stutters' steps).
        private void Trails(INativeEntranceFx fx,float t,float thin)
        {
            for(int i=0;i<neo.Length;i++)
            {
                if(thin<1&&neo[i].Kind==Slab&&(i&1)==1)continue;
                if(!NewPose(i,t,out var at,out var rot,out bool solid,out float wire,out _)||solid)continue;if(!NewPose(i,t-.07f,out var back,out var brot,out _,out _,out _))continue;
                ref var p=ref neo[i];var c=at+Vector3.Transform(p.Centroid,rot);var cb=back+Vector3.Transform(p.Centroid,brot);if((c-cb).LengthSquared()<.0004f)continue;
                Line(fx,c,cb,Cyan*(.9f*Math.Min(1.4f,wire)),.03f+.012f*p.Size,.7f,8000+i);
            }
            // the inhale: a breath of light drawn in to the architect's chest
            float inh=NativeEntranceMath.Bump(t,Inhale-.05f,DipA);if(inh>.01f){var chest=Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,.2f,0);fx.Glow(chest,Ice*(1.6f*inh),0,.25f,1,0,8090);}
        }
        // THE DROP: light floods out along the dais's rays and the floor's sixteen seams; it erupts up every monolith and on into the sky as
        // a beam; it runs up the crown's spires; the sky wells up behind the name; dust and sparks off the landing world; gold embers rise
        // from the seams for the rest of it.
        private void DropFx(INativeEntranceFx fx,float t,float thin,float floor,float q)
        {
            float s=t-Drop;if(s<-.02f)return;
            // the flood: a white front running out along each seam from the centre at 18 m/s, cooling to the seam's own light behind it
            if(s>=0&&s<.9f)
            {
                float front=.3f+s*18;float lvl=3.4f*(1-NativeEntranceMath.Smooth((s-.42f)/.4f));
                if(lvl>.02f)for(int i=0;i<Wedges;i++)
                {
                    if(thin<1&&(i&1)==1)continue;float a=(i+.5f)*2*WedgeHalf;float reach=Math.Min(front,FloorR);if(reach<=.35f)continue;
                    FloorLine(fx,Polar(reach,a,floor+.012f),Polar(Math.Max(.3f,reach-2.2f),a,floor+.012f),Vector3.Lerp(Molten,Cyan,NativeEntranceMath.Clamp01(s*2))*lvl,.04f,8100+i);
                }
            }
            if(s>=0)
            {
                // the monoliths: a surge climbing each one's inner face in .3 s, then a beam from its point into the sky, flaring and holding
                for(int m=0;m<8;m++)
                {
                    if(thin<1&&(m&1)==1)continue;
                    float a=(m+.5f)/8*6.2831853f;var face=Polar(MonoRing-.58f,a,0);float u=NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01((s-.02f)/.3f));
                    if(u<1){float y=MonoFoot+(MonoTip-MonoFoot)*u;Line(fx,new Vector3(face.X,y,face.Z),new Vector3(face.X,Math.Max(MonoFoot,y-2.4f),face.Z),Molten*3.4f,.11f,.45f,8200+m);}
                    float beam=NativeEntranceMath.Ramp(s,.28f,.38f)*(1-NativeEntranceMath.Ramp(t,FadeFrom,Total));if(beam<=0)continue;float flare=Flash(t,Drop+.33f,.02f,.4f);
                    var tip=Polar(MonoRing,a,MonoTip+.12f);var top=tip+new Vector3(0,72,0);
                    Line(fx,tip,top,Vector3.Lerp(Ice,Gold,.4f)*(.55f*beam+2.4f*flare),.08f+.3f*flare,.8f,8220+m);
                    if(thin>=1)Line(fx,tip,top,Deep*(.25f*beam+.8f*flare),.5f+.6f*flare,.9f,8230+m);
                    fx.Glow(tip,Molten*(1.1f*beam+3.2f*flare),0,.5f+1.3f*flare,1,0,8240+m);
                }
                // the crown: light running up each spire from its collar to its point as the beams rise past it
                for(int i=0;i<8;i++)
                {
                    if(thin<1&&(i&1)==1)continue;float u=NativeEntranceMath.EaseOut(NativeEntranceMath.Clamp01((s-.4f)/.45f));if(u<=0)continue;
                    SpireLine(i,t,out var root,out var tip);var dir=tip-root;float len=dir.Length();if(len<1e-3f)continue;dir/=len;
                    float run=1-NativeEntranceMath.Smooth((s-.85f)/.5f);
                    if(run>.01f)Line(fx,root+dir*(len*u),root+dir*Math.Max(0,len*u-2.6f),Molten*(2.6f*run),.07f,.4f,8260+i);
                    fx.Glow(tip,Gold*(.5f+1.6f*Flash(t,Drop+.85f,.02f,.5f)),0,.35f,1,0,8270+i);
                }
                // the sky answers: a vast glow wells up behind the name and the crown and settles
                float sky=NativeEntranceMath.Ramp(s,.15f,.8f)*(1-.4f*NativeEntranceMath.Ramp(s,1.2f,2.8f))*(1-NativeEntranceMath.Ramp(t,FadeFrom,Total))+1.5f*Flash(t,Drop+.3f,.06f,.7f);
                if(sky>.01f){fx.Glow(new Vector3(0,20,-32),Deep*(.32f*sky),0,48,1,0,8500);fx.Glow(new Vector3(0,12,-24),Gold*(.1f*sky),0,26,1,0,8501);
                    if(thin>=1){fx.Glow(new Vector3(-16,15,-20),Deep*(.16f*sky),0,30,1,0,8502);fx.Glow(new Vector3(16,15,-20),Deep*(.16f*sky),0,30,1,0,8503);}}
                // dust shaken off the world as it lands: round each plinth's top and out from the platform's rim
                if(s<2.4f)for(int m=0;m<8;m++)
                {
                    if(thin<1&&(m&1)==1)continue;float a=(m+.5f)/8*6.2831853f;
                    NativeEntranceFx.GroundDust(fx,t,Drop+.02f,Polar(MonoRing,a,MonoFoot),thin<1?2:4,.8f,2.4f,1.9f,Dust,.32f,1.2f,8600+m*17,q,MonoFoot);
                    NativeEntranceFx.GroundDust(fx,t,Drop+.03f+.02f*m,Polar(FloorR-.3f,a+.4f,floor),thin<1?2:3,.2f,1.6f,1.6f,Dust,.26f,.9f,8700+m*17,q,floor);
                }
            }
            NativeEntranceFx.Sparks(fx,t,Drop,new Vector3(0,floor+.05f,0),new Vector3(0,.25f,0),1.4f,(int)(52*thin),9,.9f,6,.9f,Ice*2.6f,Gold*1.4f,.018f,8300,1,floor);
            if(s>=0&&s<.4f)fx.Glow(new Vector3(0,1.4f,0),Ice*(4.5f*(1-s/.4f)),0,2.5f*(1-s/.4f)+.4f,1,0,8390);
            // embers of gold rising from the seams for the rest of it
            NativeEntranceFx.Embers(fx,t,Drop+.2f,Total+.5f,new Vector3(0,floor+.05f,0),7.2f,thin<1?14:34,.7f,2.4f,Gold*1.1f,.03f,8400,1,.25f);
        }
        // The light the hands pour as the arms lower (from just after the drop): a shaft from each palm down to the dais, motes falling
        // in it, a pool where it lands, and pulses running out along the seams from under the architect, one after another round the
        // floor (the world fed from the hands); the right hand stops pouring as it rises in the command.
        private void HandsFx(INativeEntranceFx fx,float t,float thin,float floor)
        {
            float pour=NativeEntranceMath.Ramp(t,Drop+.25f,Lower+.3f)*(1-NativeEntranceMath.Ramp(t,FadeFrom,Total));if(pour<.01f)return;
            for(int side=0;side<2;side++)
            {
                bool left=side==0;float lvl=pour*(left?1:1-.85f*NativeEntranceMath.Ramp(t,Command,Command+.45f));if(lvl<.01f)continue;
                var h=HandAt(left,t);var land=new Vector3(h.X,floor+.02f,h.Z);if(h.Y-land.Y<.1f)continue;
                fx.Glow(h,Molten*(1.5f*lvl),0,.16f,1,0,12200+side);fx.Glow(h,Ice*(.4f*lvl),0,.5f,0,0,12202+side);
                NativeEntranceFx.Shaft(fx,t,h,land,.05f,.2f,Ice*.8f,lvl,thin<1?3:5,thin<1?2:6,12210+side*40,1);
                Line(fx,land,h,Ice*(1.3f*lvl),.014f,.3f,12290+side);
                fx.Glow(land,Cyan*(1.2f*lvl),0,.55f,1,0,12300+side);
            }
            if(t<Drop+.6f)return;
            for(int i=0;i<Wedges;i++)
            {
                if(thin<1&&(i&1)==1)continue;float a=(i+.5f)*2*WedgeHalf;float ph=(t-Drop-.6f)/1.2f+i*.37f;ph-=(float)Math.Floor(ph);
                float r=.4f+ph*(FloorR-.4f);float lvl=pour*(float)Math.Sin(Math.PI*ph)*.9f;if(lvl<.02f)continue;
                FloorLine(fx,Polar(r,a,floor+.012f),Polar(Math.Max(.35f,r-1.1f),a,floor+.012f),Vector3.Lerp(Cyan,Gold,ph)*lvl,.03f,12400+i);
            }
        }

        // =====================================================================================================================
        // The name, forged in the sky out of code glyphs (NameFx), and on the card (the "architect" effect, Runtime NativeIntroArchitectCard)
        // =====================================================================================================================
        /// <summary>Where the name hangs: above and behind the architect, high in the hero frame between the two monoliths behind it,
        /// facing the hero frame's settled eye; its letters' height and the widest it may be.</summary>
        public static readonly Vector3 NameCentre=new Vector3(.3f,5.2f,-3.6f);
        public const float LetterHeight=1.25f,NameMaxWidth=12f;
        /// <summary>Letter i's glyphs fly in for GlyphFlight s from the floor's seams, then its pen writes it in LetterTime s; a letter
        /// every LetterStep s from NameStart.</summary>
        public const float LetterStep=.06f,LetterTime=.2f,GlyphFlight=.34f,NameStart=Drop+.1f;
        public static float LetterAt(int i)=>NameStart+i*LetterStep;
        private float NameLevel(float t)=>NativeEntranceMath.Ramp(t,NameStart,NameStart+.3f)*(1-NativeEntranceMath.Ramp(t,FadeFrom,Total));
        private string nameKey;private readonly List<float> nameSegs=new List<float>(512);private readonly List<int> nameLetter=new List<int>(64);private int nameLetters;private float nameScale,nameWidth;
        /// <summary>Lays the live name out once per name: each stroke segment (x0, y0, x1, y1 in letter units, its letter, its share
        /// of its letter's ink) in the name's plane. Names the font can't write mostly (an unknown script) are left to the card.</summary>
        private void LayName(string name)
        {
            if(ReferenceEquals(name,nameKey))return;nameKey=name;nameSegs.Clear();nameLetter.Clear();nameLetters=0;if(string.IsNullOrEmpty(name))return;
            int known=0,shown=0;foreach(char ch in name){if(ch==' ')continue;shown++;if(NativeArchitectFont.Has(ch))known++;}
            if(shown==0||known*3<shown*2)return;
            float x=0;int letter=0;
            foreach(char ch in name)
            {
                if(ch==' '){x+=NativeArchitectFont.Space;continue;}
                int first=nameSegs.Count;float ink=NativeArchitectFont.Strokes(ch,x,nameSegs);
                // each segment's share of the letter's ink (for the pen), cumulative
                float run=0;for(int s=first;s<nameSegs.Count;s+=6){float len=new Vector2(nameSegs[s+2]-nameSegs[s],nameSegs[s+3]-nameSegs[s+1]).Length();nameSegs[s+4]=letter;nameSegs[s+5]=ink>0?run/ink:0;run+=len;}
                nameLetter.Add(first);x+=NativeArchitectFont.Advance;letter++;
            }
            nameLetters=letter;float width=Math.Max(1,x-NativeArchitectFont.Advance+NativeArchitectFont.Width);
            nameScale=Math.Min(LetterHeight/NativeArchitectFont.Height,NameMaxWidth/width);nameWidth=width;
        }
        /// <summary>The point at share `share` of letter `letter`'s ink, in letter units.</summary>
        private Vector2 InkAt(int letter,float share)
        {
            int first=nameLetter[letter],end=letter+1<nameLetters?nameLetter[letter+1]:nameSegs.Count;
            for(int s=first;s<end;s+=6)
            {
                float a=nameSegs[s+5],b=s+6<end?nameSegs[s+11]:1;if(share>b&&s+6<end)continue;float u=b>a?NativeEntranceMath.Clamp01((share-a)/(b-a)):0;
                return new Vector2(nameSegs[s]+(nameSegs[s+2]-nameSegs[s])*u,nameSegs[s+1]+(nameSegs[s+3]-nameSegs[s+1])*u);
            }
            return new Vector2(nameSegs[first],nameSegs[first+1]);
        }
        private void NameFx(INativeEntranceFx fx,float t,float thin,Vector3 camRight,Vector3 camUp)
        {
            LayName(Context.Name);if(nameLetters==0)return;float fadeOut=1-NativeEntranceMath.Ramp(t,FadeFrom,Total);if(fadeOut<.01f)return;float level=NameLevel(t);
            // the plane faces the hero frame's settled eye, so the name reads square there
            var f=NameCentre-HeroEye;f=f.LengthSquared()>1e-6f?Vector3.Normalize(f):-Vector3.UnitZ;
            var right=Vector3.Normalize(Vector3.Cross(f,Up));var up=Vector3.Normalize(Vector3.Cross(right,f));float k=nameScale,letterH=NativeArchitectFont.Height*k;
            var origin=NameCentre-right*(nameWidth*k*.5f)-up*(letterH*.5f);
            // ---- the code glyphs flying up out of the floor's seams to each letter's strokes, the letter lit as they arrive ----
            int per=thin<1?3:8;
            for(int i=0;i<nameLetters;i++)
            {
                float arrive=LetterAt(i);if(t>arrive+LetterTime||t<arrive-GlyphFlight)continue;
                for(int g=0;g<per;g++)
                {
                    int seed=13000+i*31+g;float share=(g+.5f)/per;float land=arrive+share*LetterTime;float leave=land-GlyphFlight*(.8f+.2f*R(seed,1));float u=(t-leave)/(land-leave);if(u<0||u>1)continue;
                    float a=(R(seed,2)*Wedges+.5f)*2*WedgeHalf;var from=Polar(1.6f+5.4f*R(seed,3),a,NativeEntranceStage.FloorY+.05f);var ink=InkAt(i,share);var to=origin+right*(ink.X*k)+up*(ink.Y*k);
                    float e=NativeEntranceMath.EaseIn(u);var p=Vector3.Lerp(from,to,e)+Up*(1.8f*(float)Math.Sin(Math.PI*u))+right*(S(seed,4)*.8f*(float)Math.Sin(Math.PI*u));
                    DrawGlyph(fx,(int)(R(seed,5)*16),p,.16f*(1-.4f*u),Vector3.Lerp(GlyphTint*1.6f,Molten*2.4f,u)*fadeOut,camRight,camUp,seed);
                }
            }
            if(level<.01f)return;
            // ---- the forged strokes: an ember glow, a gold body and a white-gold molten core, each letter white-hot as it closes,
            // pulsing as the whole name completes and settling to a slow molten shimmer ----
            float done=LetterAt(nameLetters-1)+LetterTime;float pulse=1.1f*Flash(t,done,.03f,.55f);
            int segs=nameSegs.Count/6;int layers=thin<1?(segs>70?1:2):(segs>120?2:3);
            float core=letterH*.085f,mid=letterH*.19f,outer=letterH*.44f;
            float sweep=NativeEntranceMath.Clamp01((t-13.9f)/1.0f);float sweepX=-.15f+1.3f*sweep;
            for(int s=0;s<nameSegs.Count;s+=6)
            {
                int letter=(int)nameSegs[s+4];float share=nameSegs[s+5];float start=LetterAt(letter);float u=(t-start)/LetterTime;if(u<=share)continue;
                float segShare=s+6<nameSegs.Count&&(int)nameSegs[s+10]==letter?nameSegs[s+11]-share:1-share;float part=segShare<=1e-4f?1:NativeEntranceMath.Clamp01((u-share)/segShare);
                float age=t-start-LetterTime;float fresh=age<0?1:(float)Math.Exp(-age*(Context.Reduced?2.5f:4.5f));
                var a=origin+right*(nameSegs[s]*k)+up*(nameSegs[s+1]*k);var b0=origin+right*(nameSegs[s+2]*k)+up*(nameSegs[s+3]*k);var b=Vector3.Lerp(a,b0,part);
                float mx=(nameSegs[s]+nameSegs[s+2])*.5f/Math.Max(1,nameWidth);float shine=1+1.4f*Math.Max(0,1-Math.Abs(mx-sweepX)*6)*sweep*(1-sweep);
                float shimmer=.9f+.1f*(float)Math.Sin(mx*14+t*1.8f);float heat=level*shine*shimmer*(1+pulse);
                if(layers>=3)Line(fx,b,a,Ember*(.32f*heat*(1+.6f*fresh)),outer,0,14000+s);
                if(layers>=2)Line(fx,b,a,Gold*(1.05f*heat*(1+.5f*fresh)),mid,0,14200+s);
                Line(fx,b,a,Vector3.Lerp(Ice*3.4f,Molten*3.0f,NativeEntranceMath.Clamp01(age*2.5f))*(heat*(1+.7f*fresh)),core,0,14400+s);
                if(part<1)fx.Glow(b,Molten*(3.4f*level),0,core*3.2f,1,0,14600+s);
            }
            // ---- light rays from behind the name: a slow sunburst fanned above and beside it (rays, never a ring), flaring as it is
            // forged and settling ----
            int rays=(int)(22*thin);var behind=NameCentre+f*.7f;float rl=level*(.2f+.9f*pulse+.5f*Flash(t,NameStart+.15f,.05f,.9f));
            for(int i=0;i<rays;i++)
            {
                float ang=(-.35f+3.84f*(i+.5f*R(13100,i))/rays)+.02f*(t-NameStart);var d=right*(float)Math.Cos(ang)+up*(float)Math.Sin(ang);
                float r0=nameWidth*k*(.18f+.2f*R(13101,i)),len=2.6f+5f*R(13102,i);
                Line(fx,behind+d*(r0+len),behind+d*r0,Gold*(rl*(.35f+.65f*R(13103,i))),.035f+.05f*R(13104,i),.92f,13110+i);
            }
            // glyph-embers drifting up off the name once it is forged
            int n=(int)(14*thin);for(int i=0;i<n;i++){int seed=10400+i*7;float born=NameStart+.7f+2.2f*R(seed,1);float a=t-born;if(a<0||a>1.4f)continue;var p=NameCentre+right*(S(seed,2)*nameWidth*k*.5f)+up*(S(seed,3)*.3f+.4f*a);float uu=a/1.4f;DrawGlyph(fx,(int)(R(seed,4)*16),p,.09f,Gold*(1.4f*level*(1-uu)),camRight,camUp,seed);}
        }
        public override void Card(float t,ref NativeIntroCardState card)
        {
            // the sky's name is the hero; the card is its quiet caption (small, low in the black glass under the architect)
            card=NativeIntroCardState.Default(t-CardAt,new Vector3(1,.97f,.9f),Gold);card.Style=NativeIntroCardStyle.Assemble;card.Effect=CardEffect;card.Kicker="THE ARCHITECT";card.Size=.042f;card.Y=.93f;card.X=.5f;
            // EffectA: how far the build has run (s since the name beat); EffectB: the distortion left (1 at the beat .. 0 settled)
            card.EffectA=Math.Max(0,t-CardAt);card.EffectB=(float)Math.Exp(-Math.Max(0,t-CardAt-.3f)*2.2f);card.Exit=NativeEntranceMath.Ramp(t,FadeFrom,Total-.05f);
        }

        // =====================================================================================================================
        // Sound: the song carries the big hits; the cues fill the gaps and texture the beats (key, then an existing bank sound, then a clip).
        // =====================================================================================================================
        private const string Cue="intro2.architect.";
        public static readonly NativeIntroCue[] CueSheet=
        {
            new NativeIntroCue(Strike,Cue+"strike","magic.lightning.hit","lightning",.55f),
            new NativeIntroCue(.06f,Cue+"glitch","dev.leet.glitch","ui",.4f),
            new NativeIntroCue(.58f,Cue+"glitch","dev.leet.glitch","ui",.32f,1.12f),
            new NativeIntroCue(1.07f,Cue+"crack","structure.crack","stone",.5f),
            new NativeIntroCue(1.43f,Cue+"glitch","dev.leet.glitch","ui",.3f,.9f),
            new NativeIntroCue(1.86f,Cue+"glitch","dev.leet.glitch","ui",.36f,1.2f),
            new NativeIntroCue(Break,Cue+"dissolve","death.style.matrix","darkness",.75f),
            new NativeIntroCue(Tear,Cue+"tear","blast.static","electric-hook",.6f),
            new NativeIntroCue(Presence,Cue+"presence","blast.implosion","darkness",.45f),
            new NativeIntroCue(PulseA,Cue+"pulse","chip.teleport","install",.28f),
            new NativeIntroCue(Draft,Cue+"draft","turret.scan","electric-hook",.55f),
            new NativeIntroCue(Condense,Cue+"condense","magic.singularity","install",.45f),
            new NativeIntroCue(Body,Cue+"form","blast.nova","explosion",.42f),
            new NativeIntroCue(Still,Cue+"blueprint","field.time","ui",.5f),
            new NativeIntroCue(Gather,Cue+"gather","structure.whoosh","dash",.55f),
            new NativeIntroCue(11.19f,Cue+"snap","ui.install.lock","metal",.28f),
            new NativeIntroCue(11.31f,Cue+"snap","ui.install.lock","metal",.28f,1.06f),
            new NativeIntroCue(11.44f,Cue+"snap","ui.install.lock","metal",.3f,1.12f),
            new NativeIntroCue(11.565f,Cue+"snap","ui.install.lock","metal",.32f,1.18f),
            new NativeIntroCue(11.69f,Cue+"snap","ui.install.lock","metal",.34f,1.25f),
            new NativeIntroCue(Inhale,Cue+"inhale","blast.implosion","darkness",.42f,1.2f),
            new NativeIntroCue(Drop,Cue+"lock","blast.quake","explosion",.6f),
            new NativeIntroCue(CardAt+.05f,Cue+"forge","chip.critical","win",.5f),
            new NativeIntroCue(13.35f,Cue+"settle","field.heal","install",.32f),
        };
    }
    /// <summary>
    /// The stroke font the architect writes the live name in (an original design: angular, single strokes, the curves cut at 45
    /// degrees; a 4 x 6 cell). Letters (any case, the common accented Latin folded to their base), digits and common punctuation;
    /// anything else is left to the name card.
    /// </summary>
    public static class NativeArchitectFont
    {
        public const float Width=4,Height=6,Advance=5.6f,Space=3.2f;
        // per character: polylines "x,y x,y ...|x,y ..." in the 4 x 6 cell
        private static readonly string[] Spec=
        {
            "A:0,0 0,4 2,6 4,4 4,0|0,2.6 4,2.6","B:0,0 0,6 3,6 4,5 4,4 3,3 0,3|3,3 4,2 4,1 3,0 0,0","C:4,6 1,6 0,5 0,1 1,0 4,0","D:0,0 0,6 3,6 4,5 4,1 3,0 0,0",
            "E:4,6 0,6 0,0 4,0|0,3 3,3","F:4,6 0,6 0,0|0,3 3,3","G:4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,3 2,3","H:0,0 0,6|4,0 4,6|0,3 4,3","I:2,0 2,6|1,6 3,6|1,0 3,0",
            "J:4,6 4,1 3,0 1,0 0,1","K:0,0 0,6|4,6 0,2.4|1.3,3.3 4,0","L:0,6 0,0 4,0","M:0,0 0,6 2,3 4,6 4,0","N:0,0 0,6 4,0 4,6","O:1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0",
            "P:0,0 0,6 3,6 4,5 4,4 3,3 0,3","Q:1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0|2.5,1.5 4.3,-.3","R:0,0 0,6 3,6 4,5 4,4 3,3 0,3|2,3 4,0",
            "S:4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1","T:0,6 4,6|2,6 2,0","U:0,6 0,1 1,0 3,0 4,1 4,6","V:0,6 2,0 4,6","W:0,6 1,0 2,4 3,0 4,6",
            "X:0,6 4,0|4,6 0,0","Y:0,6 2,3 4,6|2,3 2,0","Z:0,6 4,6 0,0 4,0",
            "0:1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0|1,1 3,5","1:1,5 2,6 2,0|1,0 3,0","2:0,5 1,6 3,6 4,5 4,4 0,0 4,0","3:0,6 4,6 2,3.5 3,3.5 4,2.5 4,1 3,0 1,0 0,1",
            "4:3,0 3,6 0,2 4,2","5:4,6 0,6 0,3.5 3,3.5 4,2.5 4,1 3,0 0,0","6:3.5,6 1,6 0,5 0,1 1,0 3,0 4,1 4,2.5 3,3.5 0,3.5","7:0,6 4,6 1.5,0",
            "8:1,3 0,4 0,5 1,6 3,6 4,5 4,4 3,3 1,3 0,2 0,1 1,0 3,0 4,1 4,2 3,3","9:.5,0 3,0 4,1 4,5 3,6 1,6 0,5 0,3.5 1,2.5 4,2.5",
            "-:.5,3 3.5,3","_:0,0 4,0",".:1.7,0 2.3,0","!:2,6 2,1.6|2,.4 2,0","?:0,5 1,6 3,6 4,5 4,4 2,2.5 2,1.6|2,.4 2,0","#:1.3,0 1.3,6|2.7,0 2.7,6|0,2 4,2|0,4 4,4",
            "':2,6 2,4.6","+:2,1 2,5|0,3 4,3","*:2,1 2,5|.4,2 3.6,4|.4,4 3.6,2","/:0,0 4,6","\\:0,6 4,0","::2,4 2,4.4|2,1 2,1.4","(:3,6 2,5 2,1 3,0","):1,6 2,5 2,1 1,0",
            "[:3,6 1.5,6 1.5,0 3,0","]:1,6 2.5,6 2.5,0 1,0","=:0,2 4,2|0,4 4,4","~:0,3 1,4 3,2 4,3","|:2,0 2,6","^:1,4.5 2,6 3,4.5","<:4,6 0,3 4,0",">:0,6 4,3 0,0",
            "$:4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1|2,6.6 2,-.6","%:0,0 4,6|0,6 1,6 1,5 0,5 0,6|3,1 4,1 4,0 3,0 3,1",",:2,.3 1.6,-.6",";:2,4 2,4.4|2,.3 1.6,-.6",
            "&:4,0 1,4 1,5 2,6 3,5 3,4 0,1.5 0,.8 1,0 2.5,0 4,2","@:3,2 3,4 1,4 1,2 3,2 4,2 4,5 3,6 1,6 0,5 0,1 1,0 4,0",
        };
        private static readonly Dictionary<char,float[][]> font=Build();
        private static Dictionary<char,float[][]> Build()
        {
            var d=new Dictionary<char,float[][]>();var inv=System.Globalization.CultureInfo.InvariantCulture;
            foreach(var e in Spec)
            {
                char c=e[0];var lines=e.Substring(2).Split('|');var polys=new float[lines.Length][];
                for(int i=0;i<lines.Length;i++){var pts=lines[i].Split(' ');var p=new float[pts.Length*2];for(int j=0;j<pts.Length;j++){var xy=pts[j].Split(',');p[j*2]=float.Parse(xy[0],inv);p[j*2+1]=float.Parse(xy[1],inv);}polys[i]=p;}
                d[c]=polys;
            }
            return d;
        }
        /// <summary>The common accented Latin letters folded to their base letter; everything upper case.</summary>
        public static char Fold(char c)
        {
            if(c>='a'&&c<='z')return (char)(c-32);if(c<128)return c;
            const string from="ÀÁÂÃÄÅàáâãäåÇçÈÉÊËèéêëÌÍÎÏìíîïÑñÒÓÔÕÖØòóôõöøÙÚÛÜùúûüÝýÿ",to="AAAAAAAAAAAACCEEEEEEEEIIIIIIIINNOOOOOOOOOOOOUUUUUUUUYYY";
            int i=from.IndexOf(c);return i>=0?to[i]:c;
        }
        public static bool Has(char c)=>font.ContainsKey(Fold(c));
        /// <summary>Appends character `c`'s stroke segments at pen x (letter units) as (x0, y0, x1, y1, 0, 0) and returns its ink length;
        /// an unknown character draws a small open box.</summary>
        public static float Strokes(char c,float x,List<float> into)
        {
            float ink=0;
            if(!font.TryGetValue(Fold(c),out var polys)){float[] box={x+.6f,0,x+3.4f,0,x+3.4f,0,x+3.4f,4,x+3.4f,4,x+.6f,4,x+.6f,4,x+.6f,0};for(int i=0;i<box.Length;i+=4){into.Add(box[i]);into.Add(box[i+1]);into.Add(box[i+2]);into.Add(box[i+3]);into.Add(0);into.Add(0);ink+=Math.Abs(box[i+2]-box[i])+Math.Abs(box[i+3]-box[i+1]);}return ink;}
            foreach(var p in polys)for(int i=0;i+3<p.Length;i+=2){into.Add(x+p[i]);into.Add(p[i+1]);into.Add(x+p[i+2]);into.Add(p[i+3]);into.Add(0);into.Add(0);ink+=new Vector2(p[i+2]-p[i],p[i+3]-p[i+1]).Length();}
            return ink;
        }
        /// <summary>Every character the font writes.</summary>
        public static IEnumerable<char> Characters=>font.Keys;
    }
}
