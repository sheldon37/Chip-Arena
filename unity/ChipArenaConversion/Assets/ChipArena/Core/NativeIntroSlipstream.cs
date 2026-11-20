using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Slipstream" (docs/INTROS-GENERIC.md "Slipstream"). 7.4 s, the name at 5.6 s. Underground, at speed: the pilot rides crouched on the
    /// roof of a maglev racing through a tunnel, its light strips streaking past. The train bursts into a station hall; the pilot rises,
    /// sprints along the roof and leaps off for the platform, lands in a slide with sparks off its boots and a hand down, and stops on
    /// the mark as the train howls away into the far tunnel, its tail lights fading. Camera: kinetic (low on the roof behind it, beside
    /// the train keeping pace, a whip to the platform for the leap in slow motion, low on the platform as it slides at the lens).
    /// </summary>
    public sealed class NativeIntroSlipstream:NativeIntroGenericScript
    {
        public const string Id=NativeIntroCatalog.Prefix+"slipstream";
        public const float Total=7.4f,CardAt=5.6f;
        public const float BurstAt=1.66f,StandAt=1.55f,RunAt=1.78f,LeapAt=2.7f,LandAt=3.3f,StopAt=4.3f,GlanceAt=4.45f,ChimeAt=5.0f;
        public override string Title=>"Slipstream";
        // The set (Unity space, generic_slipstream.py): the track's centre line, the platform edge, the hall's ends; the roof the pilot
        // rides (its surface where the pilot runs, a little to the platform's side of the car's centre line).
        public const float TrackZ=-2.95f,EdgeZ=-1.25f,Half=34,Car=16,RoofY=2.888f,RideZ=TrackZ+.95f;
        /// <summary>The tunnel's mouth (source x; the tunnel runs on beyond it toward -x) and its ceiling over z: the set's lining
        /// (tools/intros/generic_slipstream.py ST_TUNNEL; keep them matched) less the ribs hanging .11 m under it. A metre taller than
        /// the first build, whose curved ceiling the helmet reached into as the pilot stood and ran on the roof (the owner's review).</summary>
        public const float MouthX=-Half;
        private static readonly float[] LiningZ={-5.3f,-4.9f,-4.0f,-2.95f,-1.9f,-1.0f,-.6f},LiningY={3.4f,4.5f,5.15f,5.35f,5.15f,4.5f,3.4f};
        public static float TunnelCeiling(float z)
        {
            if(z<=LiningZ[0])return LiningY[0]-.11f;
            for(int i=1;i<LiningZ.Length;i++)if(z<=LiningZ[i]){float u=(z-LiningZ[i-1])/(LiningZ[i]-LiningZ[i-1]);return LiningY[i-1]+(LiningY[i]-LiningY[i-1])*u-.11f;}
            return LiningY[LiningY.Length-1]-.11f;
        }
        private const float V0=14,Accel=15;
        private static readonly Vector3 Cool=NativeEntranceFx.Hex(0xd8f0ff),Hall=NativeEntranceFx.Hex(0xbfe6ff),Spark=NativeEntranceFx.Hex(0xffb050),SparkHot=NativeEntranceFx.Hex(0xfff0c0),
            Tail=NativeEntranceFx.Hex(0xff2a3a),Teal=NativeEntranceFx.Hex(0x16b6c8),Flap=NativeEntranceFx.Hex(0xffd98a),Head=NativeEntranceFx.Hex(0xffffff);
        /// <summary>The middle car's centre (source x; the train runs toward +x): steady through the tunnel and the ride, pulling away after the leap.</summary>
        public static float CarX(float t){float s=t-LeapAt;return s<=0?-23.7f+V0*s:-23.7f+V0*s+.5f*Accel*s*s;}
        /// <summary>Where the pilot is along the middle car's roof (m from its centre, + toward the front).</summary>
        private static float Along(float t){float u=NativeEntranceMath.Clamp01((t-(RunAt-.05f))/(LeapAt-RunAt+.05f));return -3+8.28f*u*u*(2-u);}
        // The leap: off the roof at LeapAt, onto the platform at LandAt (a low vault: a little lift, then down hard).
        private static readonly Vector3 LeapFrom=new Vector3(CarX(LeapAt)+Along(LeapAt),RoofY+.88f,RideZ),LandOn=new Vector3(-8.5f,.88f,-.35f);
        private const float FlyUp=1.5f,FlyG=21;
        /// <summary>The slide along the platform: from the landing to the mark, decelerating to a stop.</summary>
        public static Vector3 Slide(float t)
        {
            float d=StopAt-LandAt,s=NativeEntranceMath.Clamp01((t-LandAt)/d);float x=LandOn.X+(0-LandOn.X)*(1-(1-s)*(1-s));
            return new Vector3(x,.88f,LandOn.Z+(0-LandOn.Z)*NativeEntranceMath.Smooth(s));
        }
        /// <summary>The sprint's i-th foot strike (0..5, the right first): every .155 s, the last (the left foot's take-off plant) just before the leap.</summary>
        public static float Strike(int i)=>RunAt+.08f+i*Stride;
        public const float Stride=.155f,Contact=.07f;
        /// <summary>The root's path: riding the roof, flying, sliding (the keys hold the root after the stop).</summary>
        public static Vector3 Ride(float t)
        {
            if(t<LeapAt)return new Vector3(CarX(t)+Along(t),RoofY+.88f,RideZ);
            if(t<LandAt){float s=t-LeapAt,d=LandAt-LeapAt,u=s/d;return new Vector3(LeapFrom.X+(LandOn.X-LeapFrom.X)*u,LeapFrom.Y+FlyUp*s-.5f*FlyG*s*s,LeapFrom.Z+(LandOn.Z-LeapFrom.Z)*NativeEntranceMath.Smooth(u));}
            return Slide(t);
        }
        public NativeIntroSlipstream():base(Id,Total,CardAt,LeapAt,0x16d6ff)
        {
            // (The model's feet. On the roof they are written in the car's frame at the start and ride with it (Shift), on its roof
            // (Ground): a surfer's crouch, then the sprint, each foot landing a quarter of a metre ahead of the hips and off again .07 s
            // later, the last (the left) the take-off. Through the slide Shift carries the right foot out in front and the left knee is
            // down (Kneel); at the stop both are put where the slide left them, and they step onto the mark.)
            float side=(float)(Math.PI/2),x0=CarX(0);
            Start=Stance(.17f,.3f,.15f,-.15f,-.32f,-.4f,side,x0-3,RideZ);
            var run=new System.Collections.Generic.List<NativeEntrancePose.Step>();
            for(int i=0;i<6;i++)
            {
                bool l=i%2==1;float land=Strike(i),lift=i==0?StandAt+.07f:i==1?Strike(0)+.03f:Strike(i-2)+Contact;
                run.Add(new NativeEntrancePose.Step(l,lift,land,x0+Along(land)+.25f,RideZ+(l?-.06f:.06f),side+(l?.08f:-.08f),.32f));
            }
            // (off the take-off the right knee drives on up into the leap)
            run.Add(new NativeEntrancePose.Step(false,Strike(4)+Contact,LeapAt+.06f,x0+Along(LeapAt)+.55f,RideZ+.06f,side-.08f,.5f));
            float ys=Facing+.25f,sy=(float)Math.Sin(ys),cy=(float)Math.Cos(ys);
            run.Add(new NativeEntrancePose.Step(false,StopAt-.002f,StopAt,sy*Reach(StopAt)-cy*.12f,cy*Reach(StopAt)+sy*.12f,ys-.2f,0));
            run.Add(new NativeEntrancePose.Step(true,StopAt-.002f,StopAt,cy*.1f-sy*.3f,-sy*.1f-cy*.3f,ys,0));
            run.Add(Step(true,StopAt+.12f,StopAt+.5f,.16f,.06f,.14f,.12f,Facing,0,Mark));run.Add(Step(false,StopAt+.36f,StopAt+.56f,-.15f,-.08f,-.36f,.04f,Facing,0,Mark));
            Steps=run.ToArray();
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            // Crouched low on the roof facing the way it runs, the left foot forward: leaning into the wind, the left hand low near the
            // roof, the right arm out for balance; riding it out, the body swaying over its feet with the car, the arm working against it.
            k.Key(0,stand).Root(0,.88f,0,side).Planted(1).Drop(Sink(-.36f)).J(B,.32f).J(Sp,.1f).J(Hd,-.28f).J(LA,-.85f,0,.3f).J(LE,-.5f).J(RA,-.4f,0,-1.0f).J(RE,-.3f);
            k.Key(.5f,stand).Root(0,.88f,0,side).Spin(0,.05f).Planted(1).Drop(Sink(-.37f)).J(B,.33f).J(Sp,.1f).J(Hd,-.28f,0,-.04f).J(LA,-.85f,0,.28f).J(LE,-.5f).J(RA,-.42f,0,-1.12f).J(RE,-.28f);
            k.Key(1f,stand).Root(0,.88f,0,side).Spin(0,-.04f).Planted(1).Drop(Sink(-.36f)).J(B,.32f).J(Sp,.1f).J(Hd,-.29f,0,.03f).J(LA,-.82f,0,.32f).J(LE,-.52f).J(RA,-.38f,0,-.92f).J(RE,-.32f);
            k.Key(StandAt-.2f,stand).Root(0,.88f,0,side).Planted(1).Drop(Sink(-.38f)).J(B,.34f).J(Sp,.1f).J(Hd,-.3f).J(LA,-.8f,0,.35f).J(LE,-.55f).J(RA,-.45f,0,-1.05f).J(RE,-.3f);
            // Up, and away: a push off the back foot, leaning into it, the arms starting to drive.
            k.Key(StandAt+.12f,stand).Root(0,.88f,0,side).Planted(1).Drop(Sink(-.16f)).J(B,.3f).J(Hd,-.15f).J(LA,-.6f,0,.12f).J(LE,-1.3f).J(RA,.5f,0,-.12f).J(RE,-1.2f);
            // The sprint: leaning into it, two keys a step. At each strike the body is lowest, the hips turned with the leg landing, the
            // shoulders countering, and the arms pumping hard (the arm opposite that leg driven forward and up, the elbow at a right
            // angle, the other thrown back); in the flight between the body rides up, the arms crossing. (s: +1 when the left lands.)
            for(int i=0;i<6;i++)
            {
                float land=Strike(i),fly=land+Stride*.5f;float s=i%2==1?1:-1;
                k.Key(land,stand).Root(0,.88f,0,side).Planted(1).Drop(Sink(-.11f)).J(B,.36f).J(Sp,.05f).J(P,0,-s*.16f,0).J(C,0,s*.22f,0).J(Hd,-.24f,-s*.08f,0)
                    .J(LA,s>0?.75f:-1.0f,0,.14f).J(LE,s>0?-1.25f:-1.6f).J(RA,s>0?-1.0f:.75f,0,-.14f).J(RE,s>0?-1.6f:-1.25f).PassKey();
                if(i<5)k.Key(fly,stand).Root(0,.88f,0,side).Planted(1).Drop(Sink(-.03f)).J(B,.33f).J(Sp,.05f).J(Hd,-.22f)
                    .J(LA,-.12f,0,.14f).J(LE,-1.45f).J(RA,-.12f,0,-.14f).J(RE,-1.45f).PassKey();
            }
            // The take-off: loaded low on the left foot, then thrown out over the platform, the right knee driving up, arms swung through.
            k.Key(LeapAt-.03f,stand).Root(0,.88f,0,side).Planted(1).Drop(Sink(-.2f)).J(B,.42f).J(Sp,.08f).J(Hd,-.28f).J(LA,.7f,0,.2f).J(LE,-.5f).J(RA,.6f,0,-.2f).J(RE,-.5f).J(RL,-.9f,0,-.05f).J(RK,1.4f);
            k.Key(LeapAt+.04f,stand).Root(0,.88f,0,side).Planted(0).Drop(Sink(-.04f)).J(B,.22f).J(Hd,-.16f).J(LA,-1.5f,0,.5f).J(LE,-.45f).J(RA,-1.2f,0,-.55f).J(RE,-.5f).J(RL,-1.35f,0,-.08f).J(RK,1.25f).J(LL,.45f,0,.06f).J(LK,.55f);
            // Its top: a running leap split wide (the right leg reaching forward, the left trailing), the arms in opposition.
            k.Key(LeapAt+.3f,stand).Root(0,.88f,0,side+.15f).Planted(0).Spin(-.1f,.08f).J(B,.14f).J(Hd,-.08f).J(RA,.45f,0,-.95f).J(RE,-.35f).J(LA,-1.45f,0,.45f).J(LE,-.35f).J(RL,-1.3f,0,-.1f).J(RK,.42f).J(LL,.5f,0,.08f).J(LK,1.0f).PassKey();
            // Coming down: the right foot reaching for the floor, the left knee tucking under for the slide, the left hand reaching back.
            k.Key(LandAt-.06f,stand).Root(0,.88f,0,side+.2f).Planted(0).Spin(-.04f,.04f).Drop(Sink(-.25f)).J(B,.12f).J(Hd,-.12f).J(RA,-.6f,0,-.9f).J(RE,-.4f).J(LA,.5f,0,.5f).J(LE,-.25f).J(RL,-1.0f,0,-.1f).J(RK,.55f).J(LL,.3f,0,.1f).J(LK,1.6f).PassKey();
            // The slide: down on the left knee, the right foot out in front, leaning back a little against it, the left hand dragging on
            // the floor behind, the right arm out for balance, eyes on the lens; it turns to the lens as it slows.
            k.Key(LandAt,stand).Root(0,.88f,0,side+.2f).Planted(1).Drop(Sink(-.56f)).J(B,.04f).J(Sp,-.02f,0,-.36f).J(C,-.04f,0,-.3f).J(Hd,-.1f,-.2f,.3f).J(LA,-.08f,.5f,.53f).J(LE,-.05f).J(RA,-.95f,0,-.7f).J(RE,-.55f).ImpactKey();
            k.Key(LandAt+.5f,stand).Root(0,.88f,0,.9f).Planted(1).Drop(Sink(-.56f)).J(B,.04f).J(Sp,-.02f,0,-.36f).J(C,-.04f,0,-.3f).J(Hd,-.1f,-.18f,.3f).J(LA,-.08f,.5f,.53f).J(LE,-.05f).J(RA,-.9f,0,-.65f).J(RE,-.55f);
            k.Key(StopAt-.25f,stand).Root(0,.88f,0,(.9f+ys)*.5f).Planted(1).Drop(Sink(-.55f)).J(B,.06f).J(Sp,-.02f,0,-.33f).J(C,-.04f,0,-.27f).J(Hd,-.08f,-.14f,.26f).J(LA,-.08f,.5f,.53f).J(LE,-.05f).J(RA,-.85f,0,-.6f).J(RE,-.6f);
            k.Key(StopAt,stand).Root(0,.88f,0,ys).Planted(1).Drop(Sink(-.5f)).J(B,.14f).J(Sp,.04f).J(Hd,-.05f,-.1f,0).J(LA,.3f,0,.45f).J(LE,-.3f).J(RA,-.5f,0,-.45f).J(RE,-.7f).ImpactKey();
            // Up from the slide; a glance after the train; back to the lens.
            k.Key(StopAt+.45f,stand).Root(0,.88f,Mark,Facing).Planted(1).Drop(Sink(-.05f)).J(B,.04f).J(Hd,-.05f,-.15f,0).J(LA,-.15f,0,.12f).J(LE,-.35f).J(RA,-.15f,0,-.12f).J(RE,-.35f);
            k.Key(GlanceAt+.35f,stand).Root(0,.88f,Mark,Facing).Planted(1).Drop(Sink(-.04f)).J(C,0,.25f,0).J(Hd,-.05f,.75f,0).J(LA,-.12f,0,.1f).J(LE,-.35f).J(RA,-.12f,0,-.1f).J(RE,-.35f);
            // The hero: it turns back easy, brushes the slide's dust off its left shoulder with the right hand, and settles loose and
            // cocked, the weight on its left leg, the shoulders tilted, the head tilting to the lens on the name.
            k.Key(CardAt-.5f,stand).Root(0,.88f,Mark,Facing).Planted(1).Drop(Sink(-.03f)).J(P,0,-.03f,-.03f).J(C,0,.1f,.03f).J(Hd,-.04f,.2f,0).J(RA,RBrushX,RBrushY,RBrushZ).J(RE,RBrushE).J(LA,-.1f,0,.06f).J(LE,-.38f).PassKey();
            k.Key(CardAt-.28f,stand).Root(0,.88f,Mark,Facing).Planted(1).Drop(Sink(-.03f)).J(P,0,-.04f,-.04f).J(C,0,.06f,.04f).J(Hd,-.04f,.14f,.02f).J(RA,RBrushX+.25f,RBrushY,RBrushZ-.15f).J(RE,RBrushE+.45f).J(LA,-.1f,0,.06f).J(LE,-.38f).PassKey();
            k.Key(CardAt+.1f,stand).Root(0,.88f,Mark,Facing).Planted(1).Drop(Sink(-.035f)).J(P,0,-.05f,-.06f).J(Sp,0,0,.03f).J(C,-.05f,-.08f,.06f).J(Hd,-.04f,.06f,.1f).J(RA,-.08f,0,-.05f).J(RE,-.36f).J(LA,-.12f,0,.04f).J(LE,-.42f);
            k.Key(Total,stand).Root(0,.88f,Mark,Facing).Planted(1).Drop(Sink(-.035f)).J(P,0,-.05f,-.06f).J(Sp,0,0,.03f).J(C,-.06f,-.1f,.06f).J(Hd,-.05f,.08f,.11f).J(RA,-.08f,0,-.05f).J(RE,-.36f).J(LA,-.12f,0,.04f).J(LE,-.42f);Keys=k;
            RootPath=(t,r)=>t<StopAt?Ride(t):new Vector3(0,r.Y,Mark*NativeEntranceMath.Smooth((t-StopAt)/.5f));
            // The camera: low on the roof behind it in the tunnel, beside the train keeping pace into the hall, a whip to the platform
            // for the leap (slowed), low on the platform as it slides in, the glance, the hero frame.
            Rig.Dolly(0,Ride(0)+new Vector3(-2.5f,.12f,.05f),Ride(.8f)+new Vector3(-2.3f,.14f,.05f),new Vector3(2.8f,-.2f,0),new Vector3(3.0f,-.25f,0),58,60).Follow(NativeEntranceAnchor.Chest).Ease(NativeIntroEase.Linear).Handheld(.5f)
               .Spline(.8f,Beside(.8f,LeapAt-.02f),new Vector3(.6f,-.1f,0),new Vector3(1.2f,-.15f,0),62,52).Follow(NativeEntranceAnchor.Chest).Ease(NativeIntroEase.Linear).Handheld(.4f)
               .Dolly(LeapAt-.02f,new Vector3(-5.6f,1.35f,4.4f),new Vector3(-5.9f,1.25f,4.0f),new Vector3(-13,2.6f,-1.6f),new Vector3(-9.5f,1.6f,-.6f),50,46)
               .Dolly(LandAt-.03f,new Vector3(2.7f,.32f,1.0f),new Vector3(2.55f,.36f,.95f),new Vector3(-.2f,0,0),new Vector3(0,.15f,0),40,38).Follow(NativeEntranceAnchor.Chest,.06f)
               .Orbit(StopAt+.1f,Vector3.Zero,55,28,3.0f,3.15f,.55f,.9f,new Vector3(0,0,0),new Vector3(0,.12f,0),38,35).Follow(NativeEntranceAnchor.Chest,.05f).In(NativeIntroCut.Blend,.35f);
            Closing(CardAt-.5f,-1,5.2f,.5f,.78f,37,.5f);
            Rig.Bars(0,1).Bars(CardAt-.5f,.45f).Black(0,1).Black(.04f,0);
            // The leap hangs at its take-off and apex, then snaps back to speed for the landing.
            Ramps.Ramp(LeapAt-.06f,.3f,.32f,.4f);
        }
        /// <summary>The eye beside the train keeping pace: the pilot's ride sampled at even times, set off to its platform side and
        /// rising out of the tunnel into the hall.</summary>
        private static Vector3[] Beside(float from,float to)
        {
            var p=new Vector3[7];
            for(int i=0;i<p.Length;i++)
            {
                // (it swings out and up only once it is out of the tunnel's mouth: the pilot on the middle car leaves the tunnel well
                // after the train's nose bursts into the hall)
                float t=from+(to-from)*i/(p.Length-1f),out_=NativeEntranceMath.Ramp(Ride(t).X+1.7f,MouthX-1,MouthX+5);
                p[i]=Ride(t)+new Vector3(1.7f+1.2f*out_,-.2f+1.1f*out_,EdgeZ+.1f-RideZ+1.4f*out_);
                if(p[i].X<MouthX+.5f){p[i].Z=Math.Min(p[i].Z,-1.12f);p[i].Y=Math.Min(p[i].Y,4.4f);}
            }
            return p;
        }
        /// <summary>The plan (the model's feet) on the moving roof while riding (written in the car's frame at the start, carried with
        /// it), and through the slide the right foot carried along out in front (the left knee down: Kneel).</summary>
        protected override void Shift(float t,float[] feet)
        {
            if(t<LeapAt){float dx=CarX(t)-CarX(0);feet[0]+=dx;feet[4]+=dx;return;}
            if(t<LandAt-.06f||t>=StopAt)return;
            var r=Slide(Math.Max(t,LandAt));float yaw=Keys.At(NativeEntranceKeys.Yaw,t);float sy=(float)Math.Sin(yaw),cy=(float)Math.Cos(yaw);
            float reach=Reach(t);feet[4]=r.X+sy*reach-cy*.12f;feet[5]=r.Z+cy*reach+sy*.12f;feet[6]=yaw-.2f;feet[7]=0;
        }
        protected override float Ground(float t)=>t<LeapAt?RoofY:NativeEntranceStage.FloorY;
        /// <summary>The dragging left hand's fingertips (on from the palm, away from the wrist) and how far they touch the floor (1 within
        /// 3 cm of it, none 15 cm up).</summary>
        private Vector3 Fingertips(float t,out float touch)
        {
            var palm=Anchor(NativeEntranceAnchor.LeftHand,t);var d=palm-Anchor(NativeEntranceAnchor.LeftWrist,t);var tips=palm+d*1.6f;
            touch=1-NativeEntranceMath.Clamp01((tips.Y-NativeEntranceStage.FloorY-.03f)/.12f);return tips;
        }
        public override bool Sliding(float t)=>t>LandAt-.1f&&t<StopAt+.08f;
        /// <summary>How far out in front the right foot slides (m): drawn in as the slide slows.</summary>
        private static float Reach(float t)=>.5f-.2f*NativeEntranceMath.Smooth((t-(StopAt-.35f))/.35f);
        /// <summary>The mark: the hips rise forward over the right foot from where the slide stops (the root's z).</summary>
        public const float Mark=.38f;
        /// <summary>The right hand brushing the left shoulder (keyed so the model's hand lands there: solved on the measured skeleton).</summary>
        private const float RBrushX=-2.12f,RBrushY=.99f,RBrushZ=1.28f,RBrushE=-1.37f;
        /// <summary>The left knee down through the slide.</summary>
        protected override float Kneel(float t,bool left)=>left?NativeEntranceMath.Smooth((t-(LandAt-.05f))/.06f)*(1-NativeEntranceMath.Smooth((t-(StopAt+.06f))/.1f)):0;
        // The sprint, the leap and the slide are all keyed on the intro rig (no motion-bank clip: the owner wants these intros' own
        // animation, as the Top Dog's and the Bombadier's are).
        public override string AccentAt(float t)=>t<BurstAt?"#3fd8ff":t<LandAt?"#7ff0ff":"#16d6ff";
        protected override Vector3 Focus(float t,Vector3 chest)=>chest;

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("slipstream/hall"))return false;
            set.Piece("slipstream/hall",Vector3.Zero,Still,1,White,Vector3.Zero);
            set.Piece("slipstream/tunnel",Vector3.Zero,Still,1,White,Cool*1.6f);
            set.Piece("slipstream/tunnel",new Vector3(0,0,2*TrackZ),UYaw((float)Math.PI),1,White,Cool*1.6f);
            set.Piece("slipstream/lights",Vector3.Zero,Still,1,White,Hall*2.2f);
            // The platform edge's light line, brightening as the train comes in.
            float near=NativeEntranceMath.Ramp(t,1.2f,BurstAt)*(1-.5f*NativeEntranceMath.Ramp(t,4.4f,5.2f));
            set.Piece("slipstream/edge",Vector3.Zero,Still,1,White,Teal*(1.2f+2.4f*near));
            // The departure board clattering over at the name (a few slow flickers, never faster than 3 Hz).
            float clatter=Swell(t,CardAt-.05f,.05f,.45f,.3f);float flap=Context.Reduced?1:1-.45f*clatter*(.5f+.5f*(float)Math.Cos((t-CardAt)*15));
            set.Piece("slipstream/board",Vector3.Zero,Still,1,White,Flap*(1.6f*flap));
            // The train: the nose leading toward +x, the middle car the pilot rides, a second nose at the tail; its windows lit.
            float x=CarX(t);var turn=UYaw((float)Math.PI);
            set.Piece("slipstream/nose",new Vector3(x+Car,0,TrackZ),turn,1,White,White*1.7f);
            set.Piece("slipstream/car",new Vector3(x,0,TrackZ),turn,1,White,White*1.7f);
            set.Piece("slipstream/nose",new Vector3(x-Car,0,TrackZ),Still,1,White,White*1.7f);
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,t,NativeEntranceFx.Hex(0x3a4450),Teal,.6f+1.2f*Flash(t,LandAt,.03f,.6f),Hall);

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();l.DriveProbe=true;float hall=NativeEntranceMath.Ramp(t,BurstAt-.25f,BurstAt+.3f);
            var fog=Vector3.Lerp(NativeEntranceFx.Hex(0x05080c),NativeEntranceFx.Hex(0x1a2430),hall);l.Background=fog;l.Fog=fog;l.FogNear=Lerp(14,26,hall);l.FogFar=Lerp(90,150,hall);
            // The hall's light on the set is the ambient and the directional (the stage material takes no point lights: docs/INTROS-V2.md 3a).
            l.Sky=NativeEntranceFx.Hex(0xcfe6ff,.24f+.36f*hall);l.Ground=NativeEntranceFx.Hex(0x6a7480,.17f+.25f*hall);
            l.Directional(new Vector3(.1f,1,.25f),Cool*(.2f+.75f*hall));
            var chest=Anchor(NativeEntranceAnchor.Chest,t);
            // In the tunnel: the strips sweeping past the pilot (steady in Reduced effects), the station's light ahead.
            if(hall<.99f)
            {
                float sweep=Context.Reduced?.7f:.55f+.45f*(float)Math.Abs(Math.Sin(t*11));l.Add(chest+new Vector3(0,.9f,1.2f),Cool*(2.2f*sweep*(1-hall)),4.5f);
                l.Add(new Vector3(-Half+2,3,-1.5f),Hall*(4*(1-hall)+2),24);
            }
            // The hall's ceiling light over the platform.
            if(hall>.01f){l.Add(new Vector3(-7,6.4f,1.2f),Hall*(3.2f*hall),15);l.Add(new Vector3(7,6.4f,1.2f),Hall*(3.2f*hall),15);}
            // The train's windows spilling light onto the pilot while it is near, its headlights ahead, its tail lights leaving.
            float x=CarX(t);float near=1-NativeEntranceMath.Clamp01((Math.Abs(chest.X-Clamp(chest.X,x-Car*1.5f,x+Car*1.5f)))/8f);
            if(near>.01f)l.Add(new Vector3(Clamp(chest.X,x-Car*1.5f,x+Car*1.5f),1.6f,TrackZ+1.8f),Cool*(2.2f*near),5);
            l.Add(new Vector3(x+Car+9,1.0f,TrackZ),Head*3.5f,9);
            l.Add(new Vector3(x-Car-9f,.8f,TrackZ),Tail*2.2f,5);
            // The slide's sparks lighting the pilot from below.
            float slide=NativeEntranceMath.Ramp(t,LandAt,LandAt+.05f)*(1-NativeEntranceMath.Ramp(t,StopAt-.25f,StopAt+.05f));
            if(slide>.01f)l.Add(Anchor(NativeEntranceAnchor.LeftKnee,t)+new Vector3(0,.25f,0),Spark*(3.5f*slide),3.5f);
            if(t>=CardAt-.3f){float c=NativeEntranceMath.Ramp(t,CardAt-.3f,CardAt+.2f);l.Add(chest+new Vector3(1.4f,.9f,1.8f),Teal*(1.4f*c),6);}
        }
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static float Clamp(float x,float lo,float hi)=>x<lo?lo:x>hi?hi:x;

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);float floor=NativeEntranceStage.FloorY;bool low=q<.5f||Context.Low;float x=CarX(t);
            // Speed in the tunnel: lines of light rushing back past the roof.
            float tunnel=1-NativeEntranceMath.Ramp(t,BurstAt-.1f,BurstAt+.3f);
            if(tunnel>.01f)
            {
                var p0=Ride(t);int n=low?12:30;
                for(int i=0;i<n;i++)
                {
                    float u=((t*2.6f+NativeEntranceMath.Rand(23000,i))%1f);
                    var at=new Vector3(p0.X+6-14*u,p0.Y-1+2.6f*NativeEntranceMath.Rand(23001,i),TrackZ+NativeEntranceMath.Signed(23002,i)*2.2f);
                    fx.Streak(at,new Vector3(-V0*2,0,0),Cool*(.35f*tunnel*(float)Math.Sin(Math.PI*u)),.012f,.1f,.03f,.8f,u,23000+i);
                }
            }
            // The burst into the hall: a pressure wave of dust out of the tunnel's mouth.
            {
                float a=t-BurstAt+.15f;
                if(a>=0&&a<1.6f){int n=low?6:14;for(int i=0;i<n;i++){float u=a/1.6f;var p=new Vector3(-Half+.5f+3.5f*u*(.5f+NativeEntranceMath.Rand(23100,i)),.6f+3.2f*NativeEntranceMath.Rand(23101,i),TrackZ+NativeEntranceMath.Signed(23102,i)*2.4f+1.6f*u);fx.Smoke(p,new Vector3(.5f,.52f,.55f),.25f*(1-u),.6f+1.6f*u,u,23100+i,-1.55f);}}
            }
            // The leap's trail.
            if(t>=LeapAt-.02f&&t<LandAt+.1f)
            {
                int n=low?4:9;
                for(int i=1;i<=n;i++){float back=t-i*.03f;if(back<LeapAt-.05f)break;var p=Ride(back)+new Vector3(0,.25f,0);var p2=Ride(back-.03f)+new Vector3(0,.25f,0);fx.Streak(p,(p-p2)/.03f,Teal*(.9f*(1-i/(float)(n+1))),.05f,.05f,.03f,.5f,i/(float)n,23200+i);}
            }
            // The slide: sparks spraying off the boots and the dragging hand, a hot glow at each contact, a scorch of light on the floor.
            float slide=NativeEntranceMath.Ramp(t,LandAt-.01f,LandAt+.03f)*(1-NativeEntranceMath.Ramp(t,StopAt-.3f,StopAt));
            if(t>=LandAt-.01f&&t<StopAt+.9f)
            {
                var tips=Fingertips(t,out float touch);var knee=Anchor(NativeEntranceAnchor.LeftKnee,t);
                if(slide*touch>.01f)fx.Glow(new Vector3(tips.X,floor+.03f,tips.Z),SparkHot*(2.2f*slide*touch),0,.2f,1,0,23300);
                if(slide>.01f)fx.Glow(new Vector3(knee.X,floor+.03f,knee.Z),SparkHot*(1.8f*slide),0,.18f,1,0,23301);
                int bursts=low?4:8;
                for(int b=0;b<bursts;b++)
                {
                    float born=LandAt+((float)Math.Floor((t-LandAt)/.06f)-b)*.06f;if(born<LandAt||born>StopAt-.2f)continue;
                    float level=1-NativeEntranceMath.Ramp(born,StopAt-.6f,StopAt-.2f),then=1;var src=b%2==0?Fingertips(born,out then):Anchor(NativeEntranceAnchor.LeftKnee,born);if(then<.3f)continue;
                    var back=Ride(born)-Ride(born+.02f);back=back.LengthSquared()>1e-6f?Vector3.Normalize(back):-Vector3.UnitX;
                    NativeEntranceFx.Sparks(fx,t,born,new Vector3(src.X,floor+.03f,src.Z),back*.8f+new Vector3(0,.45f,0),.7f,low?3:6,4.5f*level+1,.45f,9.8f,.6f,SparkHot*2.4f,Spark,.012f,23310+b*13,q,floor);
                }
                // The scorch: glowing streaks left on the floor along the slide, cooling.
                int n=low?6:14;
                for(int i=0;i<n;i++){float at=LandAt+i*(StopAt-LandAt-.15f)/n;if(at>t)break;float cool=1-NativeEntranceMath.Clamp01((t-at)/1.1f);if(cool<=0)continue;var p=Anchor(NativeEntranceAnchor.LeftKnee,at);var p2=Anchor(NativeEntranceAnchor.LeftKnee,at+.06f);p.Y=p2.Y=floor+.006f;Line(fx,p2,p2-p,Spark*(1.1f*cool*cool),.02f,.2f,1-cool,23400+i);}
            }
            // The train's wake on the platform: dust swirling along the edge as it pulls away; its tail lights shrinking into the tunnel.
            {
                float wake=NativeEntranceMath.Ramp(t,LandAt,LandAt+.4f)*(1-NativeEntranceMath.Ramp(t,4.8f,5.6f));
                if(wake>.01f){int n=low?5:12;for(int i=0;i<n;i++){float u=((t*.9f+i/(float)n)%1f);var p=new Vector3(-12+24*NativeEntranceMath.Rand(23500,i)+6*u,floor+.15f+.6f*u,EdgeZ+.2f+.5f*NativeEntranceMath.Rand(23501,i));fx.Smoke(p,new Vector3(.55f,.56f,.6f),.16f*wake*(float)Math.Sin(Math.PI*u),.35f+.7f*u,u,23500+i,floor);}}
                var tail=new Vector3(x-Car-8.35f,.68f,TrackZ);float far=1-NativeEntranceMath.Ramp(t,5.0f,5.8f);
                if(t>LandAt&&far>.01f)for(int s=0;s<2;s++){var p=tail+new Vector3(0,0,s==0?-.75f:.75f);fx.Glow(p,Tail*(1.6f*far),0,.25f,1,0,23600+s);fx.Glow(p,Tail*(.5f*far),0,.9f,1,0,23602+s);}
                var nose=new Vector3(x+Car+8.45f,.65f,TrackZ);for(int s=0;s<2;s++)fx.Glow(nose+new Vector3(0,0,s==0?-.75f:.75f),Head*1.6f,0,.35f,1,0,23610+s);
            }
            // Dust in the hall's light after it has gone.
            if(t>BurstAt){int n=low?6:16;for(int i=0;i<n;i++){var p=new Vector3(NativeEntranceMath.Signed(23700,i)*7,.4f+3.6f*NativeEntranceMath.Rand(23701,i),.5f+5*NativeEntranceMath.Rand(23702,i))+new Vector3((float)Math.Sin(t*.4f+i)*.3f,(float)Math.Sin(t*.3f+i*1.7f)*.2f,0);fx.Glow(p,Hall*.08f,0,.03f,0,0,23700+i);}}
        }
        /// <summary>Afterimages through the leap.</summary>
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            float w=Swell(t,LeapAt,.05f,.35f,.25f);if(w<.02f||into.Length<4)return 0;
            for(int i=0;i<4;i++)into[i]=new NativeEntranceGhost{Lag=.045f*(i+1),Opacity=.26f*w*(1-i*.22f),Color=Teal};
            return 4;
        }

        // ---- the name card and the sound ----
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.Y=CardY;card.Style=NativeIntroCardStyle.Type;card.Glow=Teal*1.6f;
        }
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.slipstream.rush","structure.rumble","dash",.6f),
            new NativeIntroCue(BurstAt-.08f,"intro2.slipstream.burst","structure.whoosh","dash",.7f),
            new NativeIntroCue(Strike(0),"intro2.slipstream.run","move.step.metal","metal",.45f),
            new NativeIntroCue(Strike(2),"intro2.slipstream.run","move.step.metal","metal",.45f,1.05f),
            new NativeIntroCue(Strike(4),"intro2.slipstream.run","move.step.metal","metal",.45f,.95f),
            new NativeIntroCue(LeapAt,"intro2.slipstream.leap","move.jump","jump",.6f),
            new NativeIntroCue(LandAt,"intro2.slipstream.slide","hazard.saw","saw-cut",.65f),
            new NativeIntroCue(LandAt+.08f,"intro2.slipstream.pass","structure.whoosh","dash",.6f,.8f),
            new NativeIntroCue(StopAt,"intro2.slipstream.stop","move.land","stone",.45f),
            new NativeIntroCue(ChimeAt,"intro2.slipstream.chime","ui.notice","ui",.45f),
            new NativeIntroCue(CardAt,"intro2.slipstream.name","chip.critical","install",.75f),
            new NativeIntroCue(CardAt+.02f,"intro2.slipstream.board","gun.reload.start","reload-start",.35f),
        };
        public override NativeIntroCue[] Cues=>cues;
    }
}
