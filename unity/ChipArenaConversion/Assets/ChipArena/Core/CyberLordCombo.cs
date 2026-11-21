using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's hardlight martial-arts string (owner 2026-10-15: "better basic attacks, (much better combo animation on
    /// melee)", then "Improve basic attacks for melee~ ... more expressive martial art attacks rather than just simple arm
    /// movement. Throw a little "Cyber movement" into it as well."), authored pose to pose on the replicated swing: each strike
    /// is a few key poses of the hips, spine, chest, head, both fists and the striking leg in the body's own frame (metres and
    /// radians; +x the pilot's left, +z ahead, as the rig's group), placed on the swing's windup (its start to the replicated
    /// hit, the kit's Release) and follow-through (the hit to its end), and sampled with C1 Hermite curves. The striking limb
    /// accelerates into full extension exactly on the replicated hit, holds for a beat, snaps back, and the follow-through
    /// settles into the guard the next step starts from. Orthodox, left foot and fist leading:
    ///  1. glitch-step jab (left): a frame-skip step-in (the windup's clock snaps in Stutter steps), the lead fist driving out,
    ///     the rear fist chambered at the hip;
    ///  2. rear roundhouse kick (right leg, mid): the support foot pivots on its ball, the hips turn over, the body leans away,
    ///     the rear arm counter-swings down and back, the lead hand guards the face;
    ///  3. spinning heel kick (right leg): a full turn to the right on the pivoting lead foot, the head spotting over the
    ///     shoulder, the rear heel driven out behind as the back comes round to the target (the rules' SpinArc swept on), landing
    ///     back bladed;
    ///  4. launcher: a jumping knee (the rear knee driven up as the body hops) into a rising uppercut (right) through the hit;
    ///  5. Pulse Palm: the lead knee raised and stomped wide into a low horse stance, both palms thrust out on the hit and held
    ///     for a glitch freeze-frame.
    /// The fists are drawn through the rig's own arm IK (MeleeCapture rows), the feet and the striking leg by CyberComboFeet
    /// (a kicking leg follows the keys' KX..KA channels: an ankle path in the hips' turned frame, a knee direction and the
    /// foot's point). The same keys give the effects their fist and foot paths (the hardlight trails).
    /// Pure: no allocation, no state; nothing reaches the authority, hit timing, hitboxes, damage or movement.
    /// </summary>
    public static class CyberCombo
    {
        /// <summary>The steps by kind. The names are the wire's step kinds (CyberKit2Spec.ComboKinds): "cyber-cross" is the
        /// string's second step, drawn as the rear roundhouse kick; "cyber-spin" the spinning heel kick; "cyber-rise" the launcher.</summary>
        public const int Jab=0,Cross=1,Spin=2,Rise=3,Palm=4,Kinds=5;
        public static readonly string[] States={"cyber-jab","cyber-cross","cyber-spin","cyber-rise","cyber-palm"};
        /// <summary>What each step draws (the docs' and renders' names).</summary>
        public static readonly string[] Techniques={"glitch-step jab","rear roundhouse kick","spinning heel kick","jumping knee into a rising uppercut","horse-stance Pulse Palm"};
        /// <summary>The striking hand per kind: 0 the left (lead), 1 the right (rear), 2 both, -1 none (a kick).</summary>
        public static readonly int[] Hand={0,-1,-1,1,2};
        /// <summary>The leg the KX..KA channels drive per kind (0 the left / lead, 1 the right / rear, -1 none): the kicks, the
        /// launcher's knee and the palm's stomp.</summary>
        public static readonly int[] Leg={-1,1,1,1,0};
        /// <summary>Whether the kind strikes with that leg's foot (the foot's hardlight trail): the two kicks.</summary>
        public static bool Kicks(int kind)=>kind==Cross||kind==Spin;
        /// <summary>v1's three steps (Jab, Cross, Pulse Palm) and v2's five (CyberKit2Spec.ComboKinds order).</summary>
        public static int V1Kind(int step)=>step==1?Cross:step==2?Palm:Jab;
        public static int V2Kind(int step)=>step<0?Jab:step>4?Palm:step;
        /// <summary>The kind before `kind` in a chained string (v1 skips the spin and the rise).</summary>
        public static int Previous(int kind,bool v2)=>kind==Palm&&!v2?Cross:kind-1;
        /// <summary>A chained step blends from where the last one left the body over this share of its windup, and a
        /// finished string eases back to the stance over Settle (s).</summary>
        public const float ChainShare=.55f,Settle=.22f;

        // ---- the pose channels (body space) ----
        public const int HY=0,HP=1,HR=2,SY=3,SP=4,SR=5,CY=6,CP=7,CR=8,NY=9,NP=10,NR=11,Drop=12,ShX=13,ShZ=14,
            L=15,R=18,LK=21,RK=24,LP=27,RP=30,LO=33,RO=36,LC=39,RC=40,Follow=41,LT=42,RT=43,LKn=44,RKn=45,Air=46,
            KX=47,KY=48,KZ=49,KPX=50,KPY=51,KPZ=52,KW=53,KA=54,N=55;
        /// <summary>A key pose: its place on the swing (Phase 0 the windup, 1 the follow-through; U along it), how the curve
        /// meets it (Flow: smooth through; Stop: eases to rest; Impact: arrives at speed and stops dead), and its channels.</summary>
        public struct Key{public byte Phase,How;public float U;public float[] V;}
        public const byte Flow=0,Stop=1,Impact=2;
        public sealed class B
        {
            public readonly float[] V=new float[N];
            public B(){}
            public B(B o){Array.Copy(o.V,V,N);}
            public B Hips(float yaw,float pitch,float roll){V[HY]=yaw;V[HP]=pitch;V[HR]=roll;return this;}
            public B Spine(float yaw,float pitch,float roll){V[SY]=yaw;V[SP]=pitch;V[SR]=roll;return this;}
            public B Chest(float yaw,float pitch,float roll){V[CY]=yaw;V[CP]=pitch;V[CR]=roll;return this;}
            public B Head(float yaw,float pitch,float roll=0){V[NY]=yaw;V[NP]=pitch;V[NR]=roll;return this;}
            public B Body(float drop,float x,float z){V[Drop]=drop;V[ShX]=x;V[ShZ]=z;return this;}
            public B Left(float x,float y,float z){V[L]=x;V[L+1]=y;V[L+2]=z;return this;}
            public B Right(float x,float y,float z){V[R]=x;V[R+1]=y;V[R+2]=z;return this;}
            public B LFist(float kx,float ky,float kz,float px,float py,float pz){V[LK]=kx;V[LK+1]=ky;V[LK+2]=kz;V[LP]=px;V[LP+1]=py;V[LP+2]=pz;return this;}
            public B RFist(float kx,float ky,float kz,float px,float py,float pz){V[RK]=kx;V[RK+1]=ky;V[RK+2]=kz;V[RP]=px;V[RP+1]=py;V[RP+2]=pz;return this;}
            public B Poles(float lx,float ly,float lz,float rx,float ry,float rz){V[LO]=lx;V[LO+1]=ly;V[LO+2]=lz;V[RO]=rx;V[RO+1]=ry;V[RO+2]=rz;return this;}
            public B Reach(float left,float right){V[LC]=left;V[RC]=right;return this;}
            /// <summary>The fists ride this share of the torso's turn away from the guard's (the spin: 1).</summary>
            public B Turn(float follow){V[Follow]=follow;return this;}
            /// <summary>The legs for an airborne pose (Move.Legs): each thigh's swing (+ forward) and knee bend, the lead (left) first.</summary>
            public B Legs(float leadThigh,float rearThigh,float leadKnee,float rearKnee){V[LT]=leadThigh;V[RT]=rearThigh;V[LKn]=leadKnee;V[RKn]=rearKnee;V[Air]=1;return this;}
            /// <summary>Back on planted feet (the legs the gait's and CyberComboFeet's again).</summary>
            public B Ground(){V[LT]=V[RT]=V[LKn]=V[RKn]=0;V[Air]=0;return this;}
            public B Lean(float hips,float spine,float chest){V[HP]=hips;V[SP]=spine;V[CP]=chest;return this;}
            /// <summary>Leans the trunk further (added to the hips', spine's and chest's pitch; + forward).</summary>
            public B Tilt(float hips,float spine,float chest){V[HP]+=hips;V[SP]+=spine;V[CP]+=chest;return this;}
            /// <summary>The momentum pass: the whole body carried `dz` further ahead and `dy` lower (the hips' shift and drop and
            /// both fists with them, so the arms keep their shape; a kick's keys ride the hips' shift already).</summary>
            public B Drive(float dz,float dy=0){V[ShZ]+=dz;V[L+2]+=dz;V[R+2]+=dz;V[Drop]+=dy;V[L+1]+=dy;V[R+1]+=dy;return this;}
            /// <summary>The striking leg's ankle (Leg[kind]) fully on the keys: x and z in the hips' turned frame about the body's
            /// shift (the hips' yaw HY carries it round), y the group height (the floor's ankle is FLOOR).</summary>
            public B Kick(float x,float y,float z){V[KX]=x;V[KY]=y;V[KZ]=z;V[KW]=1;return this;}
            /// <summary>The striking leg's knee direction in the hips' turned frame (the shin's front faces it).</summary>
            public B Knee(float x,float y,float z){V[KPX]=x;V[KPY]=y;V[KPZ]=z;return this;}
            /// <summary>The striking foot's point about the ankle (rad; + toes pointed down the shin, - the heel pushed out).</summary>
            public B Point(float a){V[KA]=a;return this;}
            /// <summary>The striking leg back on its footwork (KW 0) with its keys at the stance spot of `leg` (so the curve from
            /// a lifted key comes down toward the floor, not through the hips).</summary>
            public B Rest(int leg)
            {
                CyberLordStance.Stance.Foot(leg==0,out float x,out float z,out _);x-=V[ShX];z-=V[ShZ];float c=(float)Math.Cos(-V[HY]),s=(float)Math.Sin(-V[HY]);
                V[KX]=x*c+z*s;V[KZ]=-x*s+z*c;V[KY]=MeleeRig.FLOOR;V[KPX]=0;V[KPY]=.3f;V[KPZ]=1;V[KA]=0;V[KW]=0;return this;
            }
            public Key At(byte phase,float u,byte how=Flow)=>new Key{Phase=phase,U=u,How=how,V=(float[])V.Clone()};
        }
        /// <summary>The guard every string starts from and settles into: bladed (the hips and chest turned .48 rad off the
        /// facing to the right), the lead fist out in front of the face, the rear fist at the chin, elbows in, the head kept on
        /// the facing.</summary>
        public static B Guard()=>new B().Hips(-.30f,.06f,0).Spine(-.08f,.16f,0).Chest(-.10f,.14f,0).Head(.42f,-.30f).Body(0,0,0)
            .Left(.14f,.30f,.40f).LFist(-.05f,.45f,.9f,-1,0,.15f).Right(-.12f,.29f,.27f).RFist(.1f,.6f,.8f,1,0,.15f).Poles(.35f,-1,-.1f,-.35f,-1,-.1f);
        /// <summary>The guard's whole torso turn (hips, spine and chest yaw): the fists' Follow turns from it.</summary>
        public const float GuardTurn=-.48f;
        private static readonly Key[][] keys=Build();
        /// <summary>A kind's keys: the five strikes here, the v2 abilities' poses (CyberKit2MeleePoses) after them.</summary>
        public static Key[] Keys(int kind)=>kind>=Kinds?CyberKit2MeleePoses.Keys(kind):keys[Math.Max(0,kind)];
        /// <summary>Whether a kind stands on planted feet (CyberComboFeet); the rest leave the legs to the gait or author them in the air.</summary>
        public static bool PlantsFeet(int kind)=>kind<Kinds||CyberKit2MeleePoses.Plants(kind);
        /// <summary>The frame-skip steps of a kind's windup (Stutter; 0 smooth): the glitch-step jab snaps into its blow.</summary>
        public static readonly int[] StutterSteps={3,0,0,0,0};
        /// <summary>The palm's glitch freeze-frame: the share of its follow-through it holds dead still on the hit pose.</summary>
        public const float PalmFreeze=.24f;
        private static Key[][] Build()
        {
            var k=new Key[Kinds][];var g=Guard();
            // Each strike is authored as the boxed-in pose (the martial pass) and then carried by its momentum (Drive): the body
            // sits back a little in the anticipation, drives in over the stepping foot into the blow (the hips first; the chest,
            // the head and the free hand follow a beat behind: Overlap), carries on a few centimetres past it as it sinks onto
            // the planted foot, and comes back over the capsule by the end (MaxShift: the hips stay well inside the capsule).
            // 1 Glitch-step jab (left): the lead fist shoots straight out at head height with the lead hip and shoulder behind it
            // as the lead foot steps in (the step and the blow snap in frame-skip steps: Stutter) and the rear heel drives off the
            // floor, the rear fist pulled back to the hip, palm up (a karate chamber); the body carries on onto the planted lead
            // foot, the fist snaps back on the same line, the lead foot steps back out and the rear fist returns to the chin.
            {
                var start=new B(g);
                var load=new B(g).Hips(-.38f,.08f,0).Spine(-.08f,.2f,0).Chest(-.15f,.16f,0).Head(.5f,-.34f).Body(-.035f,0,.01f).Left(.15f,.28f,.34f).LFist(0,.3f,1,-.7f,-.7f,0)
                    .Right(-.16f,.14f,.16f).RFist(0,.5f,1,.4f,.6f,0).Poles(.35f,-1,-.1f,-.4f,-.7f,-.6f);
                var hit=new B(g).Hips(-.64f,.09f,0).Spine(-.12f,.25f,0).Chest(-.28f,.24f,0).Head(.88f,-.40f).Body(-.055f,.02f,.075f)
                    .Left(.05f,.34f,.92f).LFist(-.08f,.06f,1,0,-1,0).Poles(.7f,-.7f,0,-.45f,-.4f,-1).Reach(.32f,0)
                    .Right(-.2f,-.05f,-.01f).RFist(0,.15f,1,0,1,0);
                var held=new B(hit).Left(.055f,.335f,.89f).Head(.84f,-.38f);
                var back=new B(g).Hips(-.4f,.07f,0).Spine(-.08f,.2f,0).Chest(-.15f,.18f,0).Head(.56f,-.34f).Body(-.03f,.01f,.045f).Left(.13f,.31f,.5f).LFist(0,.4f,1,-.8f,-.5f,0)
                    .Right(-.16f,.16f,.16f).RFist(.05f,.6f,.8f,.8f,.3f,.3f).Poles(.35f,-1,-.1f,-.4f,-.8f,-.4f).Reach(.05f,0);
                var ready=new B(g).Body(-.02f,0,.01f);
                load.Drive(-.035f,-.012f).Tilt(-.02f,0,0);hit.Drive(.1f,-.015f).Tilt(.02f,.05f,.02f).Head(.9f,-.49f);held.Drive(.113f,-.035f).Tilt(.03f,.06f,.03f).Head(.86f,-.5f);back.Drive(.05f,-.015f);
                k[Jab]=new[]{start.At(0,0,Stop),load.At(0,.4f),hit.At(0,1,Impact),held.At(1,.2f,Stop),back.At(1,.55f),ready.At(1,1,Stop)};
            }
            // 2 Rear roundhouse kick (right leg, mid): a skip in on the lead foot (forward and turned out) as the rear knee
            // chambers up and the hips start round, then the hips drive through and turn over (.95 of a quarter turn past the
            // guard) on the lead foot pivoting on its ball, the hips carried forward under a trunk leaning back and away, and the
            // shin snaps out across the front at belt height, the foot pointed; the rear arm swings down and back against it, the
            // lead fist guards the face. It re-chambers, the foot comes home to the rear spot and the lead foot steps back.
            {
                var start=new B(g).Rest(1);
                var chamber=new B(g).Hips(.35f,.02f,0).Spine(-.2f,.1f,0).Chest(-.18f,.1f,0).Head(.4f,-.3f).Body(-.045f,.03f,0)
                    .Left(.1f,.33f,.32f).LFist(-.05f,.5f,.86f,-1,0,.15f).Right(-.22f,.06f,.04f).RFist(0,.2f,1,.7f,0,.6f).Poles(.35f,-1,-.1f,-.4f,-.6f,-.5f)
                    .Kick(-.37f,-.42f,-.12f).Knee(-.75f,.6f,.6f).Point(1.1f);
                var hit=new B(g).Hips(1.4f,-.24f,0).Spine(-.36f,-.06f,0).Chest(-.38f,.0f,0).Head(-.6f,-.16f).Body(-.06f,.06f,.03f)
                    .Left(.06f,.36f,.25f).LFist(0,.6f,.8f,-1,0,.2f).Right(-.28f,-.25f,-.28f).RFist(0,-.9f,-.4f,.9f,0,.2f).Poles(.4f,-1,-.1f,-.4f,-.2f,-1)
                    .Kick(-.53f,-.22f,-.03f).Knee(.2f,.5f,1).Point(1.3f);
                var held=new B(hit).Hips(1.55f,-.24f,0).Head(-.72f,-.16f).Kick(-.47f,-.25f,.06f);
                var rechamber=new B(g).Hips(.75f,.0f,0).Spine(-.25f,.08f,0).Chest(-.25f,.08f,0).Head(-.1f,-.25f).Body(-.045f,.03f,0)
                    .Left(.08f,.34f,.3f).Right(-.2f,.05f,.06f).RFist(0,.3f,1,.8f,0,.4f).Poles(.35f,-1,-.1f,-.4f,-.6f,-.5f)
                    .Kick(-.27f,-.44f,-.25f).Knee(-.6f,.5f,.7f).Point(.9f);
                var land=new B(g).Hips(-.1f,.05f,0).Body(-.035f,.01f,0).Head(.25f,-.3f);
                var ready=new B(g).Body(-.02f,0,.01f).Rest(1);
                chamber.Drive(.06f,-.012f).Tilt(.03f,0,0);hit.Drive(.095f,-.005f);held.Drive(.105f,-.014f);rechamber.Drive(.07f,-.01f);land.Drive(.03f,-.012f).Rest(1);
                k[Cross]=new[]{start.At(0,0,Stop),chamber.At(0,.45f),hit.At(0,1,Impact),held.At(1,.28f,Stop),rechamber.At(1,.58f),land.At(1,.82f),ready.At(1,1,Stop)};
            }
            // 3 Spinning heel kick (right leg): the lead foot steps in across the line, then the whole body winds round to the
            // right on it (pivoting on its ball with the hips), travelling into the turn, the head whipping round to spot over the
            // right shoulder, the rear knee chambered tight and the fists in the guard riding the turn; with the back to the target
            // the rear heel drives out behind at belt height, the body leaning away from it; the turn carries on, the heel hooking
            // across, the leg folds and lands back on its spot, bladed again a full turn later, and the lead foot steps back.
            // (The coil takes more of the turn than before so the windup's angular speed is even: the whip is gentler.)
            {
                var start=new B(g).Rest(1);
                var coil=new B(g).Hips(-1.6f,.04f,0).Spine(-.35f,.06f,0).Chest(-.3f,.04f,0).Head(-.2f,-.12f).Body(-.06f,.04f,0).Turn(1)
                    .Kick(-.13f,-.62f,-.06f).Knee(0,.2f,1).Point(.4f);
                var hit=new B(g).Hips(-3.55f,-.28f,0).Spine(-.3f,-.08f,0).Chest(-.3f,-.04f,0).Head(-1.1f,-.12f).Body(-.05f,.05f,.03f).Turn(1)
                    .Left(.2f,.3f,.3f).LFist(-.05f,.45f,.9f,-1,0,.15f).Right(-.24f,.26f,.18f).Poles(.35f,-1,-.1f,-.5f,-.8f,0)
                    .Kick(-.1f,-.24f,-.42f).Knee(0,-1,.3f).Point(-.3f);
                var sweep=new B(hit).Hips(-4.15f,-.24f,0).Head(-.9f,-.14f).Kick(-.2f,-.27f,-.38f);
                var recoil=new B(g).Hips(-5.5f,.02f,0).Spine(-.2f,.1f,0).Chest(-.15f,.1f,0).Head(-.3f,-.2f).Body(-.05f,.03f,0).Turn(1)
                    .Kick(-.14f,-.62f,-.08f).Knee(0,.2f,1).Point(.3f);
                var land=new B(g).Hips(-6.2f,.05f,0).Spine(-.1f,.12f,0).Chest(-.1f,.12f,0).Head(.3f,-.24f).Body(-.045f,0,0).Turn(1);
                var ready=new B(g).Hips(-MeleeMath.TAU-.30f,.06f,0).Body(-.03f,0,0).Turn(1);ready.Rest(1);
                coil.Drive(.04f,-.01f);hit.Drive(.09f,-.005f);sweep.Drive(.11f,-.014f);recoil.Drive(.08f,-.01f);land.Drive(.04f,-.012f).Rest(1);
                k[Spin]=new[]{start.At(0,0,Stop),coil.At(0,.5f),hit.At(0,1),sweep.At(1,.28f),recoil.At(1,.58f),land.At(1,.82f),ready.At(1,1,Stop)};
            }
            // 4 Launcher (right): a deep drop, sitting back, with the rear fist low by the hip, then the legs drive the body up
            // and forward into a hop, the rear knee driven up high in front (a jumping knee) as the fist rises through the chin
            // line on the hit (knuckles up, palm to the face) and on overhead, the lead fist pulled down; it lands soft on both
            // feet in the guard, the lead foot stepping back to its spot.
            {
                var start=new B(g).Rest(1);
                var load=new B(g).Hips(-.5f,.14f,0).Spine(-.12f,.25f,-.06f).Chest(-.15f,.2f,-.12f).Head(.6f,-.32f).Body(-.14f,0,.01f)
                    .Right(-.2f,.0f,.28f).RFist(.1f,.8f,.6f,1,0,.1f).Left(.1f,.24f,.66f).LFist(-.05f,.4f,.9f,-1,0,.15f).Poles(.35f,-1,-.1f,-.4f,-1,.3f);
                var hit=new B(g).Hips(.3f,-.06f,0).Spine(.1f,-.08f,.04f).Chest(.14f,-.1f,.06f).Head(-.5f,-.16f).Body(.09f,0,.06f)
                    .Right(-.04f,.47f,.48f).RFist(.05f,1,.35f,0,.1f,-1).Left(.2f,.06f,.22f).LFist(0,-.2f,1,-1,0,0).Poles(.6f,-.8f,-.3f,-.5f,-.8f,.4f).Reach(0,.2f)
                    .Kick(-.17f,-.44f,.14f).Knee(0,.6f,.8f).Point(.75f);
                var high=new B(hit).Hips(.38f,-.08f,0).Chest(.16f,-.13f,.06f).Head(-.45f,-.24f).Body(.1f,0,.06f).Right(-.03f,.64f,.40f).Kick(-.18f,-.45f,.12f);
                var down=new B(g).Hips(.1f,.04f,0).Spine(0,.08f,0).Chest(.02f,.08f,0).Head(-.05f,-.16f).Body(-.07f,0,.04f).Right(-.12f,.38f,.30f).RFist(0,.7f,.6f,.8f,0,-.2f);
                var ready=new B(g).Hips(-.22f,.05f,0).Body(-.035f,0,.02f);
                load.Drive(-.03f,-.015f).Rest(1);hit.Drive(.07f,.012f).Tilt(.03f,0,0);high.Drive(.085f,.015f);down.Drive(.06f,-.02f).Rest(1);ready.Rest(1);
                k[Rise]=new[]{start.At(0,0,Stop),load.At(0,.5f),hit.At(0,1,Impact),high.At(1,.25f),down.At(1,.6f),ready.At(1,1,Stop)};
            }
            // 5 Pulse Palm (both): both palms chambered by the rear hip as the body gathers back over the rear leg and the lead knee
            // rises, then a stomp forward and wide into a low horse stance, the body travelling over it and the hips squared, and
            // both palms thrust out side by side at chest height on the hit, held dead still for a glitch freeze-frame as the pulse
            // fires, then back to the guard, the lead foot stepping back.
            {
                var start=new B(g).Rest(0);
                var load=new B(g).Hips(-.6f,.12f,0).Spine(-.15f,.2f,0).Chest(-.22f,.14f,0).Head(.85f,-.30f).Body(-.06f,-.02f,-.02f)
                    .Left(.0f,.02f,.18f).LFist(0,.7f,.7f,-.7f,0,.7f).Right(-.17f,-.02f,.08f).RFist(0,.7f,.7f,.7f,0,.7f).Poles(.5f,-1,-.3f,-.5f,-1,-.3f)
                    .Kick(.2f,-.58f,.18f).Knee(.2f,.6f,1).Point(.3f);
                var stomp=new B(g).Hips(-.3f,.12f,0).Spine(-.08f,.18f,0).Chest(-.12f,.12f,0).Head(.5f,-.32f).Body(-.13f,-.01f,.04f)
                    .Left(.1f,.0f,.4f).LFist(0,1,.15f,0,-.1f,1).Right(-.15f,-.02f,.34f).RFist(0,1,.15f,0,-.1f,1).Poles(.6f,-.9f,-.3f,-.6f,-.9f,-.3f);
                var hit=new B(g).Hips(-.04f,.12f,0).Spine(.04f,.2f,0).Chest(.04f,.1f,0).Head(-.06f,-.34f).Body(-.16f,.02f,.08f)
                    .Left(.15f,.05f,.84f).LFist(.1f,1,.25f,0,-.25f,1).Right(-.13f,.03f,.84f).RFist(-.1f,1,.25f,0,-.25f,1).Poles(.8f,-.6f,0,-.8f,-.6f,0).Reach(.32f,.32f);
                var back=new B(g).Hips(-.2f,.08f,0).Spine(-.05f,.18f,0).Chest(-.05f,.14f,0).Head(.25f,-.32f).Body(-.07f,0,.05f).Left(.14f,.26f,.48f).Right(-.13f,.26f,.36f);
                var ready=new B(g).Body(-.035f,0,.01f).Rest(0);
                load.Drive(-.035f,-.01f).Tilt(-.02f,0,0);stomp.Drive(.08f,-.012f).Rest(0);hit.Drive(.105f,-.015f).Tilt(.03f,.03f,0).Head(-.06f,-.4f).Rest(0);back.Drive(.045f,-.012f).Rest(0);
                k[Palm]=new[]{start.At(0,0,Stop),load.At(0,.45f),stomp.At(0,.74f),hit.At(0,1,Impact),new B(hit).At(1,PalmFreeze,Stop),back.At(1,.6f),ready.At(1,1,Stop)};
            }
            return k;
        }

        /// <summary>One swing on the replicated clock: its kind and its start, hit (the kit's Release) and end.</summary>
        public struct Swing
        {
            public int Kind;public float At,Hit,Until;
            /// <summary>The step window (absolute; the nominal contract window, StepFrom..StepTo, unless the view carries the
            /// simulation's own) and how far the simulation moves the capsule along the facing over it (0: rooted, today's rules).</summary>
            public float StepAt,StepUntil,Travel;
            public Swing(int kind,float at,float hit,float until)
            {
                Kind=kind;At=at;Hit=Math.Max(at+1e-3f,hit);Until=Math.Max(Hit+1e-3f,until);StepAt=At;StepUntil=Until;Travel=0;
                if(kind>=0&&kind<Kinds){StepAt=MoveTime(this,StepFromPhase[kind],StepFromU[kind]);StepUntil=Math.Max(StepAt+1e-3f,MoveTime(this,StepToPhase[kind],StepToU[kind]));}
            }
            /// <summary>This swing with the simulation's step (absolute window, metres along the facing; clamped to MaxTravel).</summary>
            public Swing Step(float at,float until,float distance)
            {
                var o=this;if(!(distance>1e-4f)||!(until>at)||float.IsNaN(at))return o;o.StepAt=at;o.StepUntil=until;o.Travel=Math.Min(MaxTravel,distance);return o;
            }
            /// <summary>This swing with the view's replicated step when it belongs to it (the combo's; docs/CYBER-LORD.md "Melee
            /// step contract"), else unchanged: the rooted, in-capsule step.</summary>
            public Swing Stepped(CyberKit2MeleeView k)=>k!=null&&k.StepDistance>0&&k.StepAt>=At-1e-3f&&k.StepAt<Until?Step(k.StepAt,k.StepUntil,k.StepDistance):this;
            /// <summary>How far the capsule has stepped by `time` (the contract's smoothstep over the window; 0 when rooted).</summary>
            public float TravelAt(float time)=>Travel<=0?0:Travel*MeleeMath.Smooth((time-StepAt)/Math.Max(1e-4f,StepUntil-StepAt));
            public float KeyTime(in Key k)=>k.Phase==0?At+k.U*(Hit-At):Hit+k.U*(Until-Hit);
            /// <summary>The swing before this one in a string chained at its start (the same pace).</summary>
            public Swing Before(int kind)
            {
                float scale=(Hit-At)/Math.Max(1e-3f,Windup(Kind)),until=At,hit=until-(Seconds(kind)-Windup(kind))*scale;return new Swing(kind,hit-Windup(kind)*scale,hit,until);
            }
        }
        /// <summary>The nominal windup and length of a kind at attack rate 1 (v2's; v1's Jab / Cross / Palm scale the same keys).</summary>
        public static float Windup(int kind)=>CyberKit2Spec.ComboWindups[Math.Max(0,Math.Min(4,kind))];
        public static float Seconds(int kind)=>CyberKit2Spec.ComboSeconds[Math.Max(0,Math.Min(4,kind))];

        // ---- the step (momentum pass; docs/CYBER-LORD.md "Melee step contract") ----
        /// <summary>
        /// The proposed simulation step per kind (Astra's to implement or amend): the capsule moved StepDistance metres along the
        /// facing (less when a target or a wall stops it) over StepFrom..StepTo (a phase and a share, as the keys: 0 the windup,
        /// 1 the follow-through), smoothstepped. The presentation reads the replicated step (Swing.Step) and plants the feet
        /// for it; with none (today's rules root the pilot through a string step) it steps inside the capsule instead (MaxShift).
        /// </summary>
        public static readonly float[] StepDistance={.40f,.25f,.30f,.20f,.45f};
        public static readonly byte[] StepFromPhase={0,0,0,0,0},StepToPhase={1,0,1,1,0};
        public static readonly float[] StepFromU={.25f,.1f,.3f,.4f,.2f},StepToU={.3f,1,.35f,.1f,.9f};
        /// <summary>The longest step the presentation follows (m), and the most the drawn hips leave the capsule by the keys
        /// alone (m, well inside its .42 m radius) and with a run's carried momentum on top (MaxCarried).</summary>
        public const float MaxTravel=1.5f,MaxShift=.2f,MaxCarried=.25f;
        /// <summary>With the simulation's step the in-capsule surge mostly gives way to the real travel (this share of it).</summary>
        public const float SurgeGive=.6f;
        /// <summary>Overlapping action: the chest, the head and the free hand trail the hips by these delays (s at the nominal pace;
        /// the Overclock compresses them with the swing). The striking limb and the hips are never delayed: every apex stays on
        /// its replicated hit.</summary>
        public const float ChestLag=.01f,HeadLag=.024f,HandLag=.02f;

        /// <summary>Channel `c` (`n` wide) of the swing's keys at `time` into `o` (C1 Hermite through the keys; before the first
        /// key its pose, after the last the last).</summary>
        public static void Sample(in Swing s,float time,float[] o)=>Sample(s,time,o,0,N);
        public static void Sample(in Swing s,float time,float[] o,int c,int n)
        {
            var k=Keys(s.Kind);int last=k.Length-1;
            if(time<=s.KeyTime(k[0])){for(int i=c;i<c+n;i++)o[i]=k[0].V[i];return;}
            if(time>=s.KeyTime(k[last])){for(int i=c;i<c+n;i++)o[i]=k[last].V[i];return;}
            int seg=0;while(seg<last-1&&time>=s.KeyTime(k[seg+1]))seg++;
            float t0=s.KeyTime(k[seg]),t1=s.KeyTime(k[seg+1]),dt=Math.Max(1e-4f,t1-t0),u=(time-t0)/dt;
            for(int i=c;i<c+n;i++)o[i]=MeleeMath.Hermite(k[seg].V[i],k[seg+1].V[i],Out(s,k,seg,i)*dt,In(s,k,seg+1,i)*dt,u);
        }
        // The curve's slope leaving key `j` and arriving at key `j` (per second): Catmull-Rom on the swing's own clock, zero at a
        // Stop and at either end, an Impact arriving at 1.6 times its segment's average speed and leaving at rest.
        private static float Slope(in Swing s,Key[] k,int j,int i)
        {
            if(j<=0||j>=k.Length-1||k[j].How!=Flow)return 0;float a=s.KeyTime(k[j-1]),b=s.KeyTime(k[j+1]);return (k[j+1].V[i]-k[j-1].V[i])/Math.Max(1e-4f,b-a);
        }
        private static float Out(in Swing s,Key[] k,int j,int i)=>k[j].How==Impact?0:Slope(s,k,j,i);
        private static float In(in Swing s,Key[] k,int j,int i)
        {
            if(k[j].How!=Impact)return Slope(s,k,j,i);float a=s.KeyTime(k[j-1]),b=s.KeyTime(k[j]);return 1.6f*(k[j].V[i]-k[j-1].V[i])/Math.Max(1e-4f,b-a);
        }
        /// <summary>The swing's pose at `time`, eased in from `from` (the pose the last swing left at this one's start, or none)
        /// over the first ChainShare of the windup. Yaw channels blend the short way round (the spin's turn).</summary>
        public static void Pose(in Swing s,float time,float[] o,in Swing from,bool chained,float[] scratch)
        {
            Sample(s,time,o);Overlap(s,time,o);Travelled(s,o);if(!chained)return;float k=MeleeMath.Ease((time-s.At)/Math.Max(1e-4f,(s.Hit-s.At)*ChainShare));if(k>=1)return;
            Sample(from,s.At,scratch);
            for(int i=0;i<N;i++){float a=scratch[i];if(i==HY||i==SY||i==CY)a=o[i]+MeleeMath.Wrap(a-o[i]);o[i]=a+(o[i]-a)*k;}
        }

        /// <summary>The trailing parts a beat behind the hips (ChestLag, HeadLag, HandLag): the chest's turn (its lean stays with
        /// the blow, so a fist or palm keeps its reach on the hit), the head,
        /// and the hand that is not striking (the punches'; a kick's arms counter-swing on their own keys, the palm uses both).</summary>
        public static void Overlap(in Swing s,float time,float[] o)
        {
            if(s.Kind<0||s.Kind>=Kinds)return;float pace=(s.Hit-s.At)/Math.Max(1e-3f,Windup(s.Kind));
            Sample(s,time-ChestLag*pace,o,CY,1);Sample(s,time-HeadLag*pace,o,NY,3);
            int hand=Hand[s.Kind];float t=time-HandLag*pace;
            if(hand==1){Sample(s,t,o,L,3);Sample(s,t,o,LK,3);Sample(s,t,o,LP,3);Sample(s,t,o,LO,3);}
            if(hand==0){Sample(s,t,o,R,3);Sample(s,t,o,RK,3);Sample(s,t,o,RP,3);Sample(s,t,o,RO,3);}
        }
        /// <summary>With the simulation's step the keys' forward surge gives way (SurgeGive of it) to the real travel: the hips
        /// and both fists drawn back together, so the arms keep their shape.</summary>
        public static void Travelled(in Swing s,float[] o)
        {
            if(s.Travel<=1e-4f||s.Kind<0||s.Kind>=Kinds)return;float k=-SurgeGive*Math.Min(1,s.Travel/StepDistance[s.Kind])*Math.Max(0,o[ShZ]);
            o[ShZ]+=k;o[L+2]+=k;o[R+2]+=k;
        }

        // ---- into a capture row (MeleeCapture's channels; the fists through the rig's arm IK) ----
        private static Vector3 V3(float[] p,int i)=>new Vector3(p[i],p[i+1],p[i+2]);
        private static void Put(float[] row,int i,Vector3 v){row[i]=v.X;row[i+1]=v.Y;row[i+2]=v.Z;}
        private static void PutQ(float[] row,int i,Quaternion q)
        {
            q=Quaternion.Normalize(q);if(q.W<0)q=new Quaternion(-q.X,-q.Y,-q.Z,-q.W);float s=(float)Math.Sqrt(Math.Max(0,1-q.W*q.W));
            if(s<1e-6f){row[i]=row[i+1]=row[i+2]=0;return;}float a=2*(float)Math.Acos(MeleeMath.Clamp(q.W,-1,1));row[i]=q.X/s*a;row[i+1]=q.Y/s*a;row[i+2]=q.Z/s*a;
        }
        /// <summary>The hips' height channel the stance takes ride (the alert take's mean); a pose's Drop is under it.</summary>
        public const float BaseBy=-.10f;
        /// <summary>
        /// A pose (channels from Pose) as a capture row: the hips, spine, chest, neck and head rotations, the hips' height, the
        /// clavicles' reach, and each fist as a chest-space IK target and wrist frame (its knuckles along K, its palm side to P)
        /// with an elbow pole, measured through the row's own torso (the drawn body's lean and sway move it a little). `torso`
        /// scales the torso's turn (the first-person view keeps a third). Fists with Follow ride the torso's whole turn.
        /// </summary>
        public static void Row(float[] p,float torso,float[] row)=>Row(p,torso,row,false);
        /// <summary>`view`: the first-person view model: the fists keep their guard-frame places (no Follow) and the spin's
        /// followed turn is left out of the torso, so a spinning step keeps the hands in view instead of whirling them round
        /// the camera.</summary>
        public static void Row(float[] p,float torso,float[] row,bool view)
        {
            Array.Clear(row,0,row.Length);
            float unturn=view?p[Follow]:0,hy=(p[HY]-(p[HY]-Guard0[HY])*unturn)*torso,sy=(p[SY]-(p[SY]-Guard0[SY])*unturn)*torso,cy=(p[CY]-(p[CY]-Guard0[CY])*unturn)*torso;
            // Each segment's lean and side bend are toward the facing (the body's frame), its twist about its own axis: the hips,
            // then the spine and chest adding theirs, the neck and head turning and pitching on from the chest (the head's
            // pitch relative to upright, so -lean looks level).
            Quaternion W(float lean,float side,float yaw)=>MeleeMath.AxisAngle(MeleeMath.X,lean)*MeleeMath.AxisAngle(MeleeMath.Z,side)*MeleeMath.AxisAngle(MeleeMath.Y,yaw);
            float l1=p[HP],l2=l1+p[SP],l3=l2+p[CP],r1=p[HR],r2=r1+p[SR],r3=r2+p[CR],y3=hy+sy+cy;
            Quaternion wp=W(l1,r1,hy),ws=W(l2,r2,hy+sy),wc=W(l3,r3,y3),wn=W(l3+p[NP]*.45f,r3+p[NR]*.45f,y3+p[NY]*.45f*torso),wh=W(l3+p[NP],r3+p[NR],y3+p[NY]*torso);
            Quaternion qp=wp,qs=MeleeMath.Inverse(wp)*ws,qc=MeleeMath.Inverse(ws)*wc;
            PutQ(row,MeleeCapture.Pelvis,qp);PutQ(row,MeleeCapture.Spine,qs);PutQ(row,MeleeCapture.Chest,qc);
            PutQ(row,MeleeCapture.Neck,MeleeMath.Inverse(wc)*wn);PutQ(row,MeleeCapture.Head,MeleeMath.Inverse(wn)*wh);
            PutQ(row,MeleeCapture.LClav,MeleeMath.AxisAngle(MeleeMath.Y,-p[LC]));PutQ(row,MeleeCapture.RClav,MeleeMath.AxisAngle(MeleeMath.Y,p[RC]));
            row[MeleeCapture.By]=BaseBy+p[Drop];
            // The chest's frame in the body's (the rig's bone offsets; the body itself unturned at the hips' height and shift).
            var body=new Vector3(p[ShX],row[MeleeCapture.By]-.025f,p[ShZ]);var pelvis=body+CapturedRig.Bones[CapturedRig.Pelvis].Position;
            var spine=pelvis+Vector3.Transform(CapturedRig.Bones[CapturedRig.Spine].Position,qp);var qps=qp*qs;
            var chest=spine+Vector3.Transform(CapturedRig.Bones[CapturedRig.Chest].Position,qps);var qch=qps*qc;var inv=MeleeMath.Inverse(qch);
            var follow=MeleeMath.AxisAngle(MeleeMath.Y,view?0:(hy+sy+cy-GuardTurn*torso)*p[Follow]);
            var pivot=new Vector3(p[ShX],0,p[ShZ]);
            Fist(p,L,LK,LP,LO,follow,pivot,chest,inv,out var lt,out var lf,out var lu,out var lpole);
            Fist(p,R,RK,RP,RO,follow,pivot,chest,inv,out var rt,out var rf,out var ru,out var rpole);
            Put(row,MeleeCapture.Grip,rt);Put(row,MeleeCapture.Blade,rf);Put(row,MeleeCapture.Edge,Vector3.Cross(ru,rf));Put(row,MeleeCapture.RPole,rpole);
            Put(row,MeleeCapture.LHand,lt);Put(row,MeleeCapture.LFwd,lf);Put(row,MeleeCapture.LUp,lu);Put(row,MeleeCapture.LPole,lpole);
            // The legs (used only by airborne poses, Move.Legs): each thigh swung about the hips' axis, slightly apart; the knee bent.
            PutQ(row,MeleeCapture.LHip,MeleeMath.AxisAngle(MeleeMath.X,-p[LT])*MeleeMath.AxisAngle(MeleeMath.Z,.06f));PutQ(row,MeleeCapture.RHip,MeleeMath.AxisAngle(MeleeMath.X,-p[RT])*MeleeMath.AxisAngle(MeleeMath.Z,-.06f));
            row[MeleeCapture.LKnee]=Math.Max(0,p[LKn]);row[MeleeCapture.RKnee]=Math.Max(0,p[RKn]);
            PutQ(row,MeleeCapture.LAnkle,MeleeMath.AxisAngle(MeleeMath.X,.35f*Math.Min(1,p[LKn])));PutQ(row,MeleeCapture.RAnkle,MeleeMath.AxisAngle(MeleeMath.X,.35f*Math.Min(1,p[RKn])));
        }
        // A fist into chest space: the target, the wrist's +z (the palm side) and +y (back up the forearm: against the knuckles).
        // A followed turn swings it round the hips' shift (`pivot`), so a body carried forward keeps its fists with it.
        private static void Fist(float[] p,int at,int knuckle,int palm,int pole,Quaternion follow,Vector3 pivot,Vector3 chest,Quaternion inv,out Vector3 target,out Vector3 fwd,out Vector3 up,out Vector3 elbow)
        {
            var pos=pivot+Vector3.Transform(V3(p,at)-pivot,follow);var k=MeleeMath.Unit(Vector3.Transform(V3(p,knuckle),follow));var z=Vector3.Transform(V3(p,palm),follow);
            if(k.LengthSquared()<1e-6f)k=MeleeMath.Z;var y=-k;z-=y*Vector3.Dot(z,y);if(z.LengthSquared()<1e-6f)z=Vector3.Cross(y,MeleeMath.X);z=Vector3.Normalize(z);
            target=Vector3.Transform(pos-chest,inv);fwd=Vector3.Transform(z,inv);up=Vector3.Transform(y,inv);elbow=Vector3.Transform(Vector3.Transform(V3(p,pole),follow),inv);
        }
        /// <summary>A fist's body-space point on the swing at `time` (the keys alone: the effects' trails).</summary>
        public static Vector3 FistAt(in Swing s,float time,bool left,float[] scratch)
        {
            Sample(s,time,scratch);Travelled(s,scratch);var f=MeleeMath.AxisAngle(MeleeMath.Y,(scratch[HY]+scratch[SY]+scratch[CY]-GuardTurn)*scratch[Follow]);var pivot=new Vector3(scratch[ShX],0,scratch[ShZ]);
            return pivot+Vector3.Transform(V3(scratch,left?L:R)-pivot,f);
        }
        private static readonly float[] Guard0=Guard().V;
        /// <summary>A point in the hips' turned frame (x, z about the body's shift; yaw `hips`) in the body's (group) frame.</summary>
        public static Vector3 FromHips(float hips,float shx,float shz,float x,float y,float z){float c=(float)Math.Cos(hips),s=(float)Math.Sin(hips);return new Vector3(shx+x*c+z*s,y,shz-x*s+z*c);}
        /// <summary>The striking leg's keyed ankle in the body's (group) frame, from sampled channels (Leg[kind]; weight KW).</summary>
        public static Vector3 KickAnkle(float[] p)=>FromHips(p[HY],p[ShX],p[ShZ],p[KX],p[KY],p[KZ]);
        /// <summary>The striking leg's knee direction in the body's (group) frame (unit).</summary>
        public static Vector3 KickKnee(float[] p){var d=FromHips(p[HY],0,0,p[KPX],p[KPY],p[KPZ]);return d.LengthSquared()<1e-8f?MeleeMath.Z:Vector3.Normalize(d);}
        /// <summary>The striking leg's ankle at `time` from the keys alone (body space; the effects' foot trail) and its weight on
        /// the keys (0: the leg is on its footwork, the point then sits on the stance spot).</summary>
        public static Vector3 FootAt(in Swing s,float time,float[] scratch,out float weight)
        {
            Sample(s,time,scratch);Travelled(s,scratch);weight=MeleeMath.Clamp(scratch[KW],0,1);var a=KickAnkle(scratch);
            int leg=s.Kind>=0&&s.Kind<Kinds?Leg[s.Kind]:-1;if(leg>=0&&weight<1){CyberLordStance.Stance.Foot(leg==0,out float x,out float z,out _);a=Vector3.Lerp(new Vector3(x,MeleeRig.FLOOR,z),a,weight);}
            return a;
        }
        /// <summary>The striking leg's knee at `time` from the keys alone (body space; the launcher's knee flare): the two-bone
        /// knee between the keyed hip and ankle, bent toward the keys' knee direction.</summary>
        public static Vector3 KneeAt(in Swing s,float time,float[] scratch)
        {
            Sample(s,time,scratch);Travelled(s,scratch);int leg=s.Kind>=0&&s.Kind<Kinds?Leg[s.Kind]:1;var hip=FromHips(scratch[HY],scratch[ShX],scratch[ShZ],leg==0?.13f:-.13f,BaseBy-.32f+scratch[Drop],0);
            var ankle=KickAnkle(scratch);var d=ankle-hip;float l=d.Length();if(l<1e-4f)return hip;var u=d/l;var pole=KickKnee(scratch);pole-=u*Vector3.Dot(pole,u);
            pole=pole.LengthSquared()<1e-6f?MeleeMath.Z:Vector3.Normalize(pole);float half=Math.Min(.25f,l*.5f);return hip+u*half+pole*(float)Math.Sqrt(Math.Max(0,.0625f-half*half));
        }
        /// <summary>Where a swing's footwork first lands `foot` (0 the lead, 1 the rear; body space on the floor, the capsule's
        /// step done): its stance spot plus the step's offset (the palm's stomp).</summary>
        public static Vector3 LandSpot(in Swing s,int foot)
        {
            CyberLordStance.Stance.Foot(foot==0,out float x,out float z,out _);var moves=Feet(s.Kind);
            for(int i=0;i<moves.Length;i++){var m=moves[i];if(m.Foot!=foot||m.Type!=StepMove&&m.Type!=KickMove||m.Real&&s.Travel<=0)continue;x+=m.X;z+=Ahead(m,s.Travel)-s.Travel;break;}
            return new Vector3(x,MeleeRig.FLOOR,z);
        }
        /// <summary>A step's target ahead of its stance spot in the swing's frame: an in-capsule step (In) gives way to the
        /// simulation's travel, and a carried one (Carry) lands that share of the travel further on.</summary>
        public static float Ahead(in FootMove m,float travel)=>(m.In?Math.Max(0,m.Z-travel):m.Z)+m.Carry*travel;
        /// <summary>The hips' own turn (HY) at `time`: the spin's support foot pivots with it.</summary>
        public static float HipsTurn(in Swing s,float time,float[] scratch){Sample(s,time,scratch,HY,1);return scratch[HY];}
        /// <summary>
        /// The frame-skip clock ("cyber movement"): over a kind's windup the pose holds and snaps forward in StutterSteps equal
        /// steps, the last snap landing the blow exactly on the replicated hit; the rest of the swing runs smooth. Reduced
        /// effects and the first-person view keep the smooth clock.
        /// </summary>
        public static float Stutter(in Swing s,float time)
        {
            int n=s.Kind>=0&&s.Kind<Kinds?StutterSteps[s.Kind]:0;if(n<=0||time<=s.At||time>=s.Hit)return time;
            float span=s.Hit-s.At;return s.At+span*(float)Math.Floor((time-s.At)/span*n)/n;
        }
        /// <summary>The whole torso's turn (hips, spine and chest yaw) at `time`: the spin's progress.</summary>
        public static float Turn(in Swing s,float time,float[] scratch){Sample(s,time,scratch,HY,1);float h=scratch[HY];Sample(s,time,scratch,SY,1);float sp=scratch[SY];Sample(s,time,scratch,CY,1);return h+sp+scratch[CY];}
        /// <summary>The striking fist's speed share at `time` (0 at rest, 1 at the drive into the hit): the trails' strength.</summary>
        public static float Drive(in Swing s,float time)
        {
            if(time<s.At||time>s.Until)return 0;float lead=(s.Hit-s.At)*.65f;
            return time<=s.Hit?MeleeMath.Smooth((time-(s.Hit-lead))/Math.Max(1e-4f,lead)):1-MeleeMath.Smooth((time-s.Hit)/Math.Max(1e-4f,(s.Until-s.Hit)*.35f));
        }

        // ---- the footwork plan (CyberComboFeet) ----
        /// <summary>One foot move in a swing: a Step (lifted, carried on an arc, landed flat on its stance spot plus X/Z, turned
        /// by Yaw), a Pivot (the heel raised by Lift on the pinned ball and the foot turned by Yaw, or with the hips' own turn
        /// when Follow), a Hop (both feet off the floor by Lift, carried round with the hips' turn and landed on their stance
        /// spots), a Kick (a Step whose foot also follows the keys' striking leg by their KW weight: Leg[kind]) or a Lift (the
        /// foot raised in place by Lift on its toes and set back down). Momentum: a step's Z is an in-capsule step-in that the
        /// simulation's step replaces (In), Carry lands it that share of the simulation's travel further on, and a Real move
        /// happens only with that travel (the rear foot following up). Phase 2 times a move on the step window (StepAt..StepUntil).</summary>
        public struct FootMove{public int Foot;public byte Type;public byte P0,P1;public float U0,U1,X,Z,Yaw,Lift,Carry;public bool Follow,In,Real;}
        public const byte StepMove=0,PivotMove=1,HopMove=2,KickMove=3,LiftMove=4;
        /// <summary>The momentum flags on a move (FootMove's Carry, In and Real).</summary>
        private static FootMove T(FootMove m,float carry,bool inStep,bool real=false){m.Carry=carry;m.In=inStep;m.Real=real;return m;}
        public const int LeadFoot=0,RearFoot=1,BothFeet=2;
        private static FootMove Step(int foot,byte p0,float u0,byte p1,float u1,float x,float z,float yaw,float lift)=>new FootMove{Foot=foot,Type=StepMove,P0=p0,U0=u0,P1=p1,U1=u1,X=x,Z=z,Yaw=yaw,Lift=lift};
        private static FootMove Pivot(int foot,byte p0,float u0,byte p1,float u1,float yaw,float heel)=>new FootMove{Foot=foot,Type=PivotMove,P0=p0,U0=u0,P1=p1,U1=u1,Yaw=yaw,Lift=heel};
        private static FootMove Spot(int foot,byte p0,float u0,byte p1,float u1,float heel)=>new FootMove{Foot=foot,Type=PivotMove,P0=p0,U0=u0,P1=p1,U1=u1,Lift=heel,Follow=true};
        private static FootMove Hop(byte p0,float u0,byte p1,float u1,float lift)=>new FootMove{Foot=BothFeet,Type=HopMove,P0=p0,U0=u0,P1=p1,U1=u1,Lift=lift};
        private static FootMove Kick(int foot,byte p0,float u0,byte p1,float u1,float x,float z,float lift)=>new FootMove{Foot=foot,Type=KickMove,P0=p0,U0=u0,P1=p1,U1=u1,X=x,Z=z,Lift=lift};
        private static FootMove Raise(int foot,byte p0,float u0,byte p1,float u1,float lift)=>new FootMove{Foot=foot,Type=LiftMove,P0=p0,U0=u0,P1=p1,U1=u1,Lift=lift};
        private static readonly FootMove[][] feet=
        {
            // Jab: the lead foot steps in 30 cm, landing just ahead of the fist (on the frame-skip clock), the rear heel driving
            // off the floor; the lead foot steps back out to its spot in the recovery (in and out), so the next step starts from
            // the stance. With the simulation's step the lead lands where the capsule stops and the rear foot follows up instead.
            new[]{T(Step(LeadFoot,0,.05f,0,.85f,0,.3f,0,.045f),1,true),Pivot(RearFoot,0,.1f,1,.35f,0,.42f),T(Step(LeadFoot,1,.35f,1,.85f,0,0,0,.03f),1,true),
                T(Step(RearFoot,2,.4f,2,1.3f,0,0,0,.035f),1,false,true)},
            // Roundhouse: a skip in on the lead foot (forward, turned out), which then pivots on its ball as the hips turn over; the
            // rear leg kicks and comes home; the lead foot steps back to its spot and heading.
            new[]{T(Step(LeadFoot,0,0,0,.4f,.02f,.11f,.45f,.03f),1,true),Pivot(LeadFoot,0,.4f,1,.3f,.75f,.35f),T(Kick(RearFoot,0,0,1,.9f,0,0,.05f),1,false),
                T(Step(LeadFoot,1,.45f,1,.9f,0,0,0,.03f),1,true)},
            // Spinning heel kick: the lead foot steps in across the line, turns a full turn on its ball with the hips; the rear leg
            // kicks and lands home; the lead foot steps back to its spot.
            new[]{T(Step(LeadFoot,0,0,0,.35f,-.05f,.1f,-.45f,.03f),1,true),Spot(LeadFoot,0,.35f,1,.65f,.4f),T(Kick(RearFoot,0,.08f,1,.9f,0,0,.05f),1,false),
                T(Step(LeadFoot,1,.65f,1,.85f,0,0,0,.025f),1,true)},
            // Launcher: the lead heel up as the legs drive, the lead foot hopping 8 cm forward and down again; the rear knee drives
            // up (the keys) and the foot lands home; the lead foot steps back to its spot.
            new[]{Pivot(LeadFoot,0,.3f,1,.2f,0,.45f),T(Step(LeadFoot,0,.8f,1,.4f,0,.08f,0,.07f),1,true),T(Kick(RearFoot,0,.2f,1,.75f,0,0,.04f),1,false),
                T(Step(LeadFoot,1,.5f,1,.85f,0,0,0,.03f),1,true)},
            // Pulse Palm: the lead knee rises and stomps 30 cm in and 8 cm out, landing just before the hit; the rear foot turns
            // its toes forward on its ball for the squared horse stance; both come back in the recovery (with the simulation's
            // step the rear foot follows up instead).
            new[]{T(Kick(LeadFoot,0,.08f,0,.9f,.08f,.3f,.05f),1,true),Pivot(RearFoot,0,.4f,1,.2f,.45f,.2f),T(Step(RearFoot,2,.5f,2,1.4f,0,0,.45f,.035f),1,false,true),
                T(Step(LeadFoot,1,.45f,1,.9f,0,0,0,.03f),1,true),Pivot(RearFoot,1,.45f,1,.9f,-.45f,.12f)},
        };
        public static FootMove[] Feet(int kind)=>kind>=Kinds?CyberKit2MeleePoses.Feet(kind):feet[Math.Max(0,kind)];
        public static FootMove MakeStep(int foot,byte p0,float u0,byte p1,float u1,float x,float z,float yaw,float lift)=>Step(foot,p0,u0,p1,u1,x,z,yaw,lift);
        public static FootMove MakePivot(int foot,byte p0,float u0,byte p1,float u1,float yaw,float heel)=>Pivot(foot,p0,u0,p1,u1,yaw,heel);
        public static float MoveTime(in Swing s,byte phase,float u)=>phase==0?s.At+u*(s.Hit-s.At):phase==2?s.StepAt+u*(s.StepUntil-s.StepAt):s.Hit+u*(s.Until-s.Hit);
    }

    /// <summary>
    /// The combo's feet on one rig (Core/CyberLordCombo.cs): a foot on the floor never moves along it (pinned flat, or on its
    /// ball with the heel raised while it pivots); otherwise it is lifted on a real step (off its ball, on an arc that clears
    /// the floor, landing flat), in a hop, or as the striking leg (a kick, the launcher's knee, the palm's stomp: the leg follows
    /// the keys' ankle path, knee and foot point by their weight, then comes down onto its step home). Each swing's moves
    /// start from where the feet actually stand as it begins (the gait's feet, or the last swing's), held in the world (the
    /// swing's own frame), so a turn of the aim mid-swing never slides them; the targets are the stance spots
    /// (CyberLordStance) plus each move's offset. After the string the gait takes them back to the stance with its own settle
    /// steps. Momentum: a foot the swing finds in the air or off its stance spot (a run's stride, a cut kick) settles onto
    /// the spot over the windup instead of snapping down; the simulation's step (Swing.Travel) carries the targets along the
    /// facing; and a strike entered at a run carries the body on a few centimetres (RunCarry). Rig-local presentation state
    /// only; a rewound clock starts the swing over.
    /// </summary>
    public sealed class CyberComboFeet
    {
        private struct Foot{public float X,Z,Yaw,Pitch,Y;}
        private readonly Foot[] start=new Foot[2],now=new Foot[2];
        private float swingAt=-100,last=-100;private Vector3 origin;private float frameYaw;private bool has,idle=true;
        private readonly bool[] settle=new bool[2];private Vector3 trackPos;private float trackAt=float.NaN,runSpeed,carried;
        /// <summary>The hop's lift of the body this frame (m) and whether the feet are posed.</summary>
        public float Hop{get;private set;}
        public bool Active{get;private set;}
        /// <summary>The striking leg's weight on its keys this frame (0: on the footwork) and which leg (0 left, 1 right, -1 none).</summary>
        public float KickWeight{get;private set;}public int KickLeg{get;private set;}=-1;
        /// <summary>Whether the moves run on the frame-skip clock (CyberCombo.Stutter; off with reduced effects).</summary>
        public bool Stutter=true;
        public void Reset(){has=false;swingAt=-100;last=-100;Active=false;Hop=0;KickWeight=0;KickLeg=-1;seenAt=float.NaN;trackAt=float.NaN;runSpeed=carried=0;Carried=0;}
        /// <summary>A strike entered at a run (RunSpeed m/s and up) carries the drawn body on by RunCarry (m) over its windup,
        /// easing back over the follow-through; the forward speed is read off the capsule's own travel before the swing. A foot
        /// found in the air or off its spot settles onto it over SettleShare of the windup.</summary>
        public const float RunCarry=.055f,RunSpeed=6.8f,SettleShare=.8f;
        /// <summary>This frame's carried shift (m along the facing).</summary>
        public float Carried{get;private set;}
        private readonly Vector3[] seen=new Vector3[2];private readonly Quaternion[] seenQ=new Quaternion[2];private float seenAt=float.NaN;
        /// <summary>Notes where the gait stands the feet this frame, before the strike's torso moves the pelvis over them: a new
        /// swing's feet start there (read after the torso, they would jump with it).</summary>
        public void Before(MeleeRig r,float time){for(int i=0;i<2;i++)seen[i]=r.ReadAnkle(i==0,out seenQ[i],out _);seenAt=time;}
        private static readonly float[] turnScratch=new float[CyberCombo.N],kickScratch=new float[CyberCombo.N];
        private static void Stance(int foot,out float x,out float z,out float yaw)=>CyberLordStance.Stance.Foot(foot==0,out x,out z,out yaw);
        // The ball (toe joint) of a foot whose ankle is `ankle` with heading `yaw` and heel pitch `pitch` (group space).
        private static Vector2 Ball(Vector3 ankle,float yaw,float pitch){float d=MeleeFootwork.BallAnkleZ(pitch);return new Vector2(ankle.X-(float)Math.Sin(yaw)*d,ankle.Z-(float)Math.Cos(yaw)*d);}
        private Vector3 ToGroup(Vector2 swingXZ,float y,MeleeRig r)
        {
            // swing frame -> world -> the rig's group now
            float c=(float)Math.Cos(frameYaw),s=(float)Math.Sin(frameYaw);var w=new Vector3(origin.X+swingXZ.X*c+swingXZ.Y*s,0,origin.Z-swingXZ.X*s+swingXZ.Y*c);
            float dx=w.X-r.GroupPosition.X,dz=w.Z-r.GroupPosition.Z,gc=(float)Math.Cos(-r.GroupYaw),gs=(float)Math.Sin(-r.GroupYaw);return new Vector3(dx*gc+dz*gs,y,-dx*gs+dz*gc);
        }
        /// <summary>
        /// Poses both legs for `swing` at `time` over the rig (after the arms and torso), at `weight` from the legs it holds.
        /// `fresh`: the swing is new to this rig (its feet start where they stand now).
        /// </summary>
        public void Pose(MeleeRig r,in CyberCombo.Swing swing,float time,float weight)
        {
            Active=false;Hop=0;KickWeight=0;KickLeg=-1;Carried=0;
            // the capsule's forward speed (its own frame), held a moment so the frame the simulation stops it still reads the run
            {
                float gdt=time-trackAt;var gp=r.GroupPosition;
                if(gdt>1e-4f&&gdt<.1f){float dx=gp.X-trackPos.X,dz=gp.Z-trackPos.Z,gc=(float)Math.Cos(-r.GroupYaw),gs=(float)Math.Sin(-r.GroupYaw);runSpeed=Math.Max((-dx*gs+dz*gc)/gdt,runSpeed-gdt*40);}
                else if(!(gdt>=0&&gdt<=1e-4f))runSpeed=0;
                trackPos=gp;trackAt=time;
            }
            if(weight<=1e-4f){last=time;idle=true;return;}
            bool rewound=time<last-.05f||time-last>.25f;
            if(!has||Math.Abs(swing.At-swingAt)>1e-4f||rewound||idle)
            {
                // Where the feet stand as the swing starts: the last swing's own (continuous), else the rig's posed ankles.
                bool carry=has&&!rewound&&!idle&&time-last<.1f;var prevOrigin=origin;float prevYaw=frameYaw;
                origin=r.GroupPosition;frameYaw=r.GroupYaw;swingAt=swing.At;
                for(int i=0;i<2;i++)
                {
                    if(carry)
                    {
                        // re-express the last swing's foot in the new frame
                        float c=(float)Math.Cos(prevYaw),s=(float)Math.Sin(prevYaw);var f=now[i];var w=new Vector3(prevOrigin.X+f.X*c+f.Z*s,0,prevOrigin.Z-f.X*s+f.Z*c);
                        float dx=w.X-origin.X,dz=w.Z-origin.Z,gc=(float)Math.Cos(-frameYaw),gs=(float)Math.Sin(-frameYaw);start[i]=new Foot{X=dx*gc+dz*gs,Z=-dx*gs+dz*gc,Yaw=MeleeMath.Wrap(f.Yaw+prevYaw-frameYaw),Pitch=f.Pitch,Y=f.Y};
                    }
                    else
                    {
                        Vector3 ankle;Quaternion q;if(seenAt==time){ankle=seen[i];q=seenQ[i];}else ankle=r.ReadAnkle(i==0,out q,out _);var fwd=Vector3.Transform(MeleeMath.Z,q);float yaw=(float)Math.Atan2(fwd.X,fwd.Z),pitch=(float)Math.Asin(MeleeMath.Clamp(-fwd.Y,-1,1));
                        pitch=Math.Max(0,pitch);var b=Ball(ankle,yaw,pitch);bool up=ankle.Y>MeleeRig.FLOOR+.02f;
                        // (a foot the gait has in the air keeps its height: it settles down instead of snapping to the floor)
                        float y=up?ankle.Y-Math.Max(MeleeRig.FLOOR,MeleeFootwork.BallAnkleY(pitch)):0;start[i]=new Foot{X=b.X,Z=b.Y,Yaw=yaw,Pitch=up?pitch:0,Y=y>.008f?y:0};
                    }
                    // a foot in the air or off its stance spot settles onto it over the windup (the authored moves run on from there)
                    Stance(i,out float sx,out float sz,out float syaw);var home=Ball(new Vector3(sx,MeleeRig.FLOOR,sz),syaw,0);
                    settle[i]=start[i].Y>0||new Vector2(start[i].X-home.X,start[i].Z-home.Y).Length()>.05f||Math.Abs(MeleeMath.Wrap(start[i].Yaw-syaw))>.3f;
                }
                // a strike entered at a run carries its momentum on (not a chained step: its body was already striking)
                carried=carry?0:MeleeMath.Clamp(runSpeed/RunSpeed,0,1);
                has=true;idle=false;
            }
            last=time;
            // A body that leaves the swing's spot (a knockback, a host move) takes its feet with it: the gait's again. The swing's
            // own step (the simulation moving the capsule along the facing) is where the body is meant to be, not a drift.
            float drift;
            {
                float dx=r.GroupPosition.X-origin.X,dz=r.GroupPosition.Z-origin.Z,gc=(float)Math.Cos(-frameYaw),gs=(float)Math.Sin(-frameYaw);
                drift=new Vector2(dx*gc+dz*gs,-dx*gs+dz*gc-swing.TravelAt(time)).Length();
            }
            weight*=1-MeleeMath.Smooth((drift-.15f)/.2f);if(weight<=1e-4f){idle=true;return;}
            for(int i=0;i<2;i++)now[i]=start[i];
            // The moves in order, each from where the earlier ones left its foot: a pure function of the swing's start and the time
            // (on the frame-skip clock while the blow's windup snaps).
            var moves=CyberCombo.Feet(swing.Kind);float clock=Stutter?CyberCombo.Stutter(swing,time):time;int kick=-1;
            // the settle first: off the floor or the spot, onto its stance spot over SettleShare of the windup (a short low step
            // when it has far to go), the heel and toes flattening; on the smooth clock (a foot coming out of a stride never snaps)
            {
                float t1=Math.Max(swing.At+1e-3f,CyberCombo.MoveTime(swing,0,SettleShare)),u=MeleeMath.Clamp((time-swing.At)/(t1-swing.At),0,1);
                for(int i=0;i<2;i++)
                {
                    if(!settle[i])continue;Stance(i,out float sx,out float sz,out float syaw);var spot=Ball(new Vector3(sx,MeleeRig.FLOOR,sz),syaw,0);var f=now[i];
                    float far=new Vector2(spot.X-f.X,spot.Y-f.Z).Length(),e=MeleeMath.Ease(u),y0=f.Y,p0=f.Pitch;
                    f.X+=(spot.X-f.X)*e;f.Z+=(spot.Y-f.Z)*e;f.Yaw+=MeleeMath.Wrap(syaw-f.Yaw)*e;
                    f.Y=y0*(1-MeleeMath.Smooth(u))+Math.Min(.04f,far*.3f)*4*u*(1-u);f.Pitch=p0*(1-MeleeMath.Smooth(u));if(u>=1){f.Y=0;f.Pitch=0;}now[i]=f;
                }
            }
            for(int m=0;m<moves.Length;m++)
            {
                var mv=moves[m];if(mv.Real&&swing.Travel<=1e-4f)continue;
                float t0=CyberCombo.MoveTime(swing,mv.P0,mv.U0),t1=Math.Max(t0+1e-3f,CyberCombo.MoveTime(swing,mv.P1,mv.U1));
                if(mv.Type==CyberCombo.KickMove&&swing.Kind<CyberCombo.Kinds)kick=mv.Foot;
                if(clock<t0)continue;float u=MeleeMath.Clamp((clock-t0)/(t1-t0),0,1);
                for(int i=0;i<2;i++)
                {
                    if(mv.Foot!=CyberCombo.BothFeet&&mv.Foot!=i)continue;
                    Stance(i,out float sx,out float sz,out float syaw);var spot=Ball(new Vector3(sx,MeleeRig.FLOOR,sz),syaw,0);var f=now[i];float yaw0=f.Yaw,pitch0=f.Pitch;
                    switch(mv.Type)
                    {
                        case CyberCombo.StepMove:case CyberCombo.KickMove:
                        {
                            float tx=spot.X+mv.X,tz=spot.Y+CyberCombo.Ahead(mv,swing.Travel),tyaw=syaw+mv.Yaw;
                            if(Math.Abs(tx-f.X)+Math.Abs(tz-f.Z)<.02f&&Math.Abs(MeleeMath.Wrap(tyaw-f.Yaw))<.05f){f.Pitch=pitch0*(1-MeleeMath.Smooth(u));break;}
                            float e=MeleeMath.Ease(u),yIn=f.Y;f.X+=(tx-f.X)*e;f.Z+=(tz-f.Z)*e;f.Yaw=yaw0+MeleeMath.Wrap(tyaw-yaw0)*e;
                            // off the ball (the heel first, rising over the step's first frames), over the arc, down flat; a foot
                            // still coming down from a stride keeps its height until the arc takes over
                            f.Y=Math.Max(yIn,mv.Lift*4*u*(1-u));f.Pitch=Math.Max(pitch0*(1-MeleeMath.Smooth(u/.4f)),.3f*MeleeMath.Smooth(u/.15f)*(1-MeleeMath.Smooth(u/.45f)));break;
                        }
                        case CyberCombo.PivotMove:
                        {
                            // the heel up quickly, held, down by the end; the foot turns on its pinned ball (by Yaw, or with the hips)
                            float heel=mv.Lift*MeleeMath.Smooth(u/.3f)*(1-MeleeMath.Smooth((u-.7f)/.3f));f.Pitch=Math.Max(pitch0*(1-MeleeMath.Smooth(u/.3f)),heel);
                            f.Yaw=mv.Follow?yaw0+CyberCombo.HipsTurn(swing,Math.Min(clock,t1),turnScratch)-CyberCombo.HipsTurn(swing,t0,turnScratch):yaw0+mv.Yaw*MeleeMath.Ease(u/.6f);break;
                        }
                        case CyberCombo.LiftMove:
                        {
                            // raised in place off its toes and set back down where it stood
                            float a=(float)Math.Sin(Math.PI*u);f.Y=mv.Lift*a;f.Pitch=Math.Max(f.Pitch,.35f*a);break;
                        }
                        default:
                        {
                            // both feet leave the floor, carried round with the hips' turn (eased in as they lift, so nothing
                            // drags along the floor), and land on the stance spots: R(turn so far) of the way from where they
                            // stood to the spots taken back by the whole turn, so it starts where they stand and ends on the spots
                            float e=MeleeMath.Ease(u),t0Turn=CyberCombo.Turn(swing,t0,turnScratch),whole=CyberCombo.Turn(swing,t1,turnScratch)-t0Turn;
                            float sofar=u>=1?whole:(CyberCombo.Turn(swing,clock,turnScratch)-t0Turn)*MeleeMath.Smooth(u/.3f);
                            float bc=(float)Math.Cos(-whole),bs=(float)Math.Sin(-whole),sx2=spot.X*bc+spot.Y*bs,sz2=-spot.X*bs+spot.Y*bc;
                            float x=f.X+(sx2-f.X)*e,z=f.Z+(sz2-f.Z)*e,c=(float)Math.Cos(sofar),s=(float)Math.Sin(sofar);
                            f.X=x*c+z*s;f.Z=-x*s+z*c;f.Yaw=yaw0+MeleeMath.Wrap(syaw-whole-yaw0)*e+sofar;f.Y=mv.Lift*(float)Math.Sin(Math.PI*u);f.Pitch=Math.Max(pitch0*(1-e),.35f*(float)Math.Sin(Math.PI*u));
                            Hop=Math.Max(Hop,f.Y);break;
                        }
                    }
                    if(u>=1){f.Y=0;if(mv.Type!=CyberCombo.PivotMove)f.Pitch=0;}
                    now[i]=f;
                }
            }
            Active=true;
            // a strike entered at a run: the body carried on over the windup and eased back over the follow-through
            if(carried>0)
            {
                float k=MeleeMath.Smooth((clock-swing.At)/Math.Max(1e-4f,(swing.Hit-swing.At)*.7f))*(1-MeleeMath.Smooth((time-swing.Hit)/Math.Max(1e-4f,(swing.Until-swing.Hit)*.85f)));
                Carried=RunCarry*carried*k*weight;var b=r.P[CapturedRig.Body];b.Z+=Carried;r.P[CapturedRig.Body]=b;
            }
            // A planted foot the hips have left out of the leg's reach (a kick's lean and turn after a step-in) would lift off its
            // spot: the body sinks just enough (at most MaxSink) for the leg to reach it with the knee still soft.
            float sink=0;
            for(int i=0;i<2;i++)
            {
                if(now[i].Y>.005f||i==kick)continue;Ankle(r,now[i],out var a,out _,out _);var hip=r.HipInGroup(i==0);float h=new Vector2(a.X-hip.X,a.Z-hip.Z).Length();
                if(h<Reach)sink=Math.Max(sink,hip.Y-a.Y-(float)Math.Sqrt(Reach*Reach-h*h));
            }
            if(sink>0){var b=r.P[CapturedRig.Body];b.Y-=Math.Min(MaxSink,sink)*weight;r.P[CapturedRig.Body]=b;}
            for(int i=0;i<2;i++)
            {
                Ankle(r,now[i],out var ankle,out float yaw,out float pitch);var f=now[i];
                r.StepFoot(i==0,ankle,yaw,pitch,-Math.Max(0,pitch)*.85f*(f.Y>.005f?.4f:1),weight);
                if(i==kick)Strike(r,swing,clock,i==0,weight);
            }
        }
        /// <summary>A planted leg's reach (m, hip to ankle: the knee kept soft) and the most the body sinks to keep it.</summary>
        public const float Reach=.47f,MaxSink=.1f;
        /// <summary>How high (m) a striking foot is lifted before the keys carry it across the floor.</summary>
        public const float Lift=.04f;
        private static readonly Vector3 BallLocal=new Vector3(0,MeleeFootwork.BallY,MeleeFootwork.BallZ);
        // A foot's ankle, heading and heel pitch in the rig's group now.
        private void Ankle(MeleeRig r,in Foot f,out Vector3 ankle,out float yaw,out float pitch)
        {
            var ball=ToGroup(new Vector2(f.X,f.Z),0,r);yaw=MeleeMath.Wrap(f.Yaw+frameYaw-r.GroupYaw);pitch=f.Pitch;
            float d=MeleeFootwork.BallAnkleZ(pitch);ankle=new Vector3(ball.X+(float)Math.Sin(yaw)*d,Math.Max(MeleeRig.FLOOR,MeleeFootwork.BallAnkleY(pitch))+f.Y,ball.Z+(float)Math.Cos(yaw)*d);
        }
        /// <summary>
        /// The striking leg over its footwork: the ankle drawn to the keys' path (CyberCombo.KickAnkle, in the body's frame) by
        /// their weight KW, the knee turned toward the keys' knee direction and the foot pointed about the ankle (KA, eased off
        /// near the floor so the toes never dip into it). At weight 0 it is exactly the footwork's leg.
        /// </summary>
        private void Strike(MeleeRig r,in CyberCombo.Swing swing,float time,bool left,float weight)
        {
            CyberCombo.Sample(swing,time,kickScratch);float w=MeleeMath.Clamp(kickScratch[CyberCombo.KW],0,1)*weight;if(w<=1e-4f)return;
            KickWeight=w;KickLeg=left?0:1;
            var now=r.ReadAnkle(left,out var foot,out var knee);var hip=r.HipInGroup(left);var keyed=CyberCombo.KickAnkle(kickScratch);
            // (across the floor only once lifted: a foot still on it is raised first, so it never drags along it)
            float y=Math.Max(MeleeRig.FLOOR,MeleeMath.Lerp(now.Y,keyed.Y,w)),ball=Math.Max(now.Y,y)+Vector3.Transform(BallLocal,foot).Y-(MeleeRig.FLOOR+MeleeFootwork.BallY),across=w*MeleeMath.Smooth(ball/Lift);
            var target=new Vector3(MeleeMath.Lerp(now.X,keyed.X,across),y,MeleeMath.Lerp(now.Z,keyed.Z,across));
            // the knee's pole swung from where the footwork bends it to the keys' direction (round the hip, never through it)
            var a=knee-hip;float la=a.Length();var b=CyberCombo.KickKnee(kickScratch);var pole=la>1e-4f?Vector3.Transform(a/la,MeleeMath.Slerp(Quaternion.Identity,MeleeMath.FromTo(a/la,b),w)):b;
            r.SolveLeg(left,target,0,0,0,foot,hip+pole*.25f);
            // the foot kept as the footwork set it (flat, rising straight off the floor) until the ankle is clear of the floor, then
            // pointed along the shin
            int hipBone=left?CapturedRig.LHip:CapturedRig.RHip;float clear=MeleeMath.Smooth((target.Y-MeleeRig.FLOOR-Lift)/.25f);
            r.SetQ(hipBone+2,MeleeMath.Slerp(r.Q[hipBone+2],MeleeMath.AxisAngle(MeleeMath.X,kickScratch[CyberCombo.KA]),w*clear));
            r.SetE(hipBone+3,r.E[hipBone+3].X*(1-w),r.E[hipBone+3].Y,r.E[hipBone+3].Z);
        }
    }
}
