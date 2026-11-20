using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Intros v2's canonical key poses (docs/INTROS-V2.md 9b "Pose craft"): bases to start a key from (NativeEntranceKeys.Key(t, basis)) and
    /// tweak with .J(...), the walk and the run as key-and-step builders, and the weapon holds as Arms helpers (the main hand by
    /// NativeIntroGrip.Hold, the off hand by the grip IK). Every one is checked on the model and inside the anatomical limits
    /// (Tests/IntroPoseChecks: the gallery). Source frame: +x the pilot's left, +y up, +z ahead; a basis is a full channel set facing its own
    /// +z (the key's Root sets the yaw). Built once, in an intro's constructor; nothing here runs per frame but the Arms helpers.
    /// </summary>
    public static class NativeIntroPoses
    {
        private const int B=CapturedRig.Body,P=CapturedRig.Pelvis,Sp=CapturedRig.Spine,C=CapturedRig.Chest,N=CapturedRig.Neck,Hd=CapturedRig.Head,
            LA=CapturedRig.LArm,LE=CapturedRig.LElbow,LH=CapturedRig.LHand,RA=CapturedRig.RArm,RE=CapturedRig.RElbow,RH=CapturedRig.RHand,
            LL=CapturedRig.LHip,LK=CapturedRig.LKnee,LF=CapturedRig.LFoot,RL=CapturedRig.RHip,RK=CapturedRig.RKnee,RF=CapturedRig.RFoot,
            LC=CapturedRig.LClav,RC=CapturedRig.RClav;
        private static void S(float[] v,int bone,float x,float y=0,float z=0)=>NativeEntranceKeys.Set(v,bone,x,y,z);
        private static float[] From(float[] basis,Action<float[]> edit)=>NativeEntranceKeys.With(basis,edit);

        // ---- the stances ----
        /// <summary>A hero stance, the weight settled back over the hips, the chest proud and turned a little, the chin level, the arms loose
        /// by the body with soft elbows. Feet a little wider than the hips (ModelFeet: Stance(.13, .04, .15, -.13, -.04, -.3)).</summary>
        public static float[] Hero()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.02f;S(v,Sp,-.03f);S(v,C,-.06f,-.12f);S(v,Hd,.02f,.1f);
            S(v,LA,-.08f,.1f,.11f);S(v,LE,-.42f);S(v,RA,-.08f,-.1f,-.11f);S(v,RE,-.42f);});
        /// <summary>A wide power stance: feet well apart (ModelFeet: Stance(.24, .02, .3, -.24, -.02, -.3)), the hips sunk 8 cm into bent
        /// knees (the model's metres), the fists low and a little out.</summary>
        public static float[] HeroWide()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.08f;S(v,B,.03f);S(v,C,-.05f);S(v,Hd,.05f);
            S(v,LA,-.1f,.12f,.2f);S(v,LE,-.5f);S(v,RA,-.1f,-.12f,-.2f);S(v,RE,-.5f);});
        /// <summary>A crouch, ready to spring: both feet planted (ModelFeet: Stance(.15, .1, .2, -.15, -.12, -.3)), the hips sunk half a
        /// metre (the model's) and back, the chest over the knees, the head up to look ahead, the hands forward for balance.</summary>
        public static float[] Crouch()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.5f;S(v,B,.42f);S(v,Sp,.08f);S(v,C,.02f);S(v,N,-.15f);S(v,Hd,-.35f);
            S(v,LA,-.75f,.1f,.2f);S(v,LE,-.7f);S(v,RA,-.75f,-.1f,-.2f);S(v,RE,-.7f);});
        /// <summary>A landing: an impact key (ImpactKey) into a deep squash (the hips .6 m down, the model's), the arms thrown out for balance,
        /// the chest over the knees and the eyes forward; feet planted wide (ModelFeet: Stance(.2, .05, .25, -.2, -.08, -.35)). Follow it
        /// .2-.3 s later with a rebound key a little over standing.</summary>
        public static float[] Landing()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.6f;S(v,B,.5f);S(v,Sp,.1f);S(v,N,-.15f);S(v,Hd,-.4f);
            S(v,LA,-.45f,0,.95f);S(v,LE,-.35f);S(v,RA,-.45f,0,-.95f);S(v,RE,-.35f);});
        /// <summary>A one-knee kneel, the torso upright: the left foot planted ahead (ModelFeet: Stance(.13, .42, .1, -.13, -.1, -.3)), the
        /// right knee down on the floor under the hips with its toes tucked (ModelKneel(t, false) 1), the hips half a metre down (the
        /// model's).</summary>
        public static float[] Kneel()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.47f;S(v,B,.04f);S(v,Hd,.04f);
            S(v,LA,-.2f,0,.22f);S(v,LE,-.55f);S(v,RA,-.1f,0,-.22f);S(v,RE,-.35f);});

        // ---- the weapons' bodies (the hands placed in Arms: AimRifle, AimPistol, DrawBow, HoldStaff, BraceShield, Guard) ----
        /// <summary>The long gun's aim: the torso bladed to put the left shoulder forward (the match's RangedPose: spine and chest turned
        /// right, the right clavicle back, the left forward), the cheek down onto the stock. Pair with AimRifle in Arms and the grip on.</summary>
        public static float[] RifleAim()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.03f;S(v,Sp,0,-.34f);S(v,C,0,-.41f);S(v,N,.15f,0,.12f);S(v,Hd,.12f,.75f,.06f);
            S(v,LC,0,-.6f);S(v,RC,0,.9f);S(v,LL,0,0,.12f);S(v,RL,0,0,-.1f);});
        /// <summary>The pistol's two-handed aim (isosceles): square to the target, the arms out level, the chin over them.</summary>
        public static float[] PistolAim()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.05f;S(v,B,.04f);S(v,Sp,0,-.02f);S(v,C,0,-.02f);S(v,Hd,0,.04f,-.08f);S(v,LC,0,-.15f);});
        /// <summary>The bow at full draw: side-on (the left shoulder to the target: key the root's yaw a quarter turn right of the shot), the
        /// head turned along the arrow, the bow arm's clavicle down. Pair with DrawBow in Arms and the grip on (Pull 1).</summary>
        public static float[] BowDraw()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.03f;S(v,C,-.04f,.15f);S(v,N,0,.25f);S(v,Hd,.05f,.9f,.05f);S(v,LC,0,-.1f,-.05f);});
        /// <summary>The staff held across the body in both hands, a fighting stance (left foot forward).</summary>
        public static float[] StaffHold()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.08f;S(v,B,.06f);S(v,C,0,-.15f);S(v,Hd,0,.12f);});
        /// <summary>The shield braced: crouched behind it, the shoulder into it, the chin down behind the rim.</summary>
        public static float[] ShieldBrace()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.2f;S(v,B,.22f);S(v,Sp,.05f);S(v,C,.03f,-.2f);S(v,N,-.08f);S(v,Hd,-.15f,.15f);});
        /// <summary>A blade's guard (the sword one-handed, the greatsword and the Katana two-handed): a forward stance, the hips turned a
        /// little off the line, the eyes on it.</summary>
        public static float[] Guard()=>From(NativeEntranceKeys.Stand(),v=>{v[NativeEntranceKeys.BodyY]=-.08f;S(v,B,.06f);S(v,P,0,-.1f);S(v,C,0,-.15f);S(v,Hd,.02f,.22f);});

        // ---- the hands on the weapons (call from Arms, in the chest's frame as the match keys its holds; the grip IK then takes the off hand) ----
        /// <summary>The rifle or machine gun shouldered and aimed `yaw`, `pitch` (rad) off the pilot's facing: the match's own long-gun hold
        /// (RangedPose.Arms: the wrist under the cheek, the stock's butt .30 m behind it at the shoulder, the elbow out and down).</summary>
        public static void AimRifle(CapturedRig.Pose pose,string spec,float yaw=0,float pitch=0,float weight=1)
        {
            var aim=NativeIntroGrip.Aim(pose,yaw,pitch);var at=new Vector3(-.11f,.245f,.155f)-Vector3.Transform(new Vector3(0,-.04f,-.30f),aim);
            NativeIntroGrip.Hold(pose,false,spec,at,aim,new Vector3(-1,-.7f,-.2f),weight);
        }
        /// <summary>The pistol out on the line (the match's two-handed hold: the wrist .37 m ahead of the chin's line, a little over the
        /// shoulders); the support fist closes round the shooting fist by the grip.</summary>
        public static void AimPistol(CapturedRig.Pose pose,float yaw=0,float pitch=0,float weight=1)
        {
            var aim=NativeIntroGrip.Aim(pose,yaw,pitch);var at=new Vector3(-.035f,.215f,0)+Vector3.Transform(new Vector3(0,.075f,.37f),aim);
            NativeIntroGrip.Hold(pose,false,"pistol",at,aim,new Vector3(-1,-.7f,-.2f),weight);
        }
        /// <summary>The draw that anchors the string at the drawn model's jaw (the bow's own full draw, 1, pulls the nock .82 m behind the
        /// riser: the keyed rig's draw length, 30 cm past the model's head). Give the grip and the Bow view the same pull.</summary>
        public const float BowAnchorPull=.55f;
        /// <summary>The bow drawn `pull` (0..1) toward `yaw`, `pitch` off the pilot's facing, raised `raised` (0 carried .. 1 on the line): the
        /// match's own bow pose (NativeBowPose: the bladed torso, the bow arm on the arrow line, the string hand on the nock, its elbow back and
        /// high). Key the root a quarter turn right of the shot (its left shoulder to the target); turn the grip on with the same Pull.</summary>
        public static void DrawBow(CapturedRig.Pose pose,float yaw,float pitch,float pull,float raised=1)
        {
            var q=CapturedRig.Rot(CapturedRig.Y,yaw)*CapturedRig.Rot(CapturedRig.X,-pitch);var aim=Vector3.Transform(Vector3.UnitZ,q);
            NativeBowPose.Torso(pose,aim,raised,1,0);
            var view=new NativeBow.View{Phase=NativeBow.Phase.Draw,Pull=pull,Extend=pull,Raised=raised,Nocked=true,Arrows=1};
            NativeBowPose.Arms(pose,NativeBow.Read.None,view,0,aim,NativeBowPose.ChestAnchor(pose),NativeBowPose.Start,raised,NativeBowPose.CarryWrist,0,false);
            NativeIntroGrip.Forearm(pose,true,NativeIntroGrip.Natural(NativeIntroGrip.Kind.Bow,false));NativeIntroGrip.Forearm(pose,false,NativeIntroGrip.Natural(NativeIntroGrip.Kind.Bow,true));pose.World();
        }
        /// <summary>A staff in the left fist low before the left hip, its head up and forward across the body (`lean` rad off upright); the right
        /// hand comes up the shaft by the grip (Along to slide it).</summary>
        public static void HoldStaff(CapturedRig.Pose pose,string spec,float lean=.6f,float weight=1)
        {
            var shaft=Vector3.Normalize(new Vector3(-.45f*(float)Math.Sin(lean),(float)Math.Cos(lean),.85f*(float)Math.Sin(lean)));
            NativeIntroGrip.Hold(pose,true,spec,new Vector3(.17f,-.33f,.24f),NativeIntroGrip.Look(shaft,new Vector3(.2f,.25f,-1)),new Vector3(1,-.8f,-.2f),weight);
        }
        /// <summary>The shield up before the chest, its face `yaw` off the pilot's facing, the forearm behind it (the off fist braces its rim by
        /// the grip).</summary>
        public static void BraceShield(CapturedRig.Pose pose,float yaw=0,float weight=1)
        {
            NativeIntroGrip.Hold(pose,false,"shield",new Vector3(-.1f,.02f,.3f),NativeIntroGrip.Aim(pose,yaw,0),new Vector3(-1,-.7f,-.2f),weight);
        }
        /// <summary>A blade's guard: the hilt before the navel, the blade forward and up `pitch` rad over level and `yaw` in toward the line, its
        /// edge down; the two-handers' off hand on the hilt by the grip.</summary>
        public static void Guard(CapturedRig.Pose pose,string spec,float pitch=.7f,float yaw=.12f,float weight=1)
        {
            bool two=NativeIntroGrip.Of(spec)==NativeIntroGrip.Kind.TwoHand;var at=two?new Vector3(-.02f,-.13f,.3f):new Vector3(-.12f,-.12f,.3f);
            var blade=NativeIntroGrip.Aim(pose,yaw,pitch);var up=Vector3.Transform(new Vector3(0,1,0),blade);
            NativeIntroGrip.Hold(pose,false,spec,at,blade,new Vector3(-1,-.8f,-.2f),weight);
        }

        // ---- the walk and the run (on the model: ModelFeet on; Core NativeIntroFeet) ----
        /// <summary>
        /// A walk (ModelFeet on: the steps are the drawn model's ankles): `count` steps along `track` (a NativeIntroFeet.Track: the hips'
        /// line, its start-up, steady speed and slow-down; set RootPath from it for the walk's span), the first with the left foot when
        /// `left`, one lifting every `cadence` s from `t0` and swinging `swing` s. Keys at each contact (the hips low, turned with the leading
        /// leg, the chest countering them, the arm opposite the leading leg swung forward with its elbow bent, the other back and straighter,
        /// the head steady) and at each passing (the hips high over the stance foot, the arms by the body); `arms` scales the swing. The drawn
        /// steps are the track's metres: a model's natural step is .6-.75 m every .5-.6 s. Returns the last landing.
        /// </summary>
        public static float Walk(NativeEntranceKeys k,List<NativeEntrancePose.Step> plan,NativeIntroFeet.Track track,float t0,int count,float cadence=.55f,float swing=.48f,bool left=true,float arms=1)
        {
            NativeIntroFeet.Walk(plan,track,left,count,t0,cadence,swing);
            var stand=NativeEntranceKeys.Stand();float yaw=track.Yaw,last=t0;
            for(int i=0;i<count;i++)
            {
                bool l=(i%2==0)==left;float s=l?1:-1,lift=t0+i*cadence,land=lift+swing,pass=lift+swing*.5f;last=land;
                float amp=arms*(i==count-1?.5f:1);var p=track.At(pass);var c=track.At(land);
                // passing: the hips over the stance foot, at their highest; the pelvis dropping a little on the swing side; the arms by the body
                k.Key(pass,stand).Root(p.X,.88f,p.Z,yaw).Drop(-.005f).J(B,.03f).J(P,0,0,-s*.04f).J(C,-.02f).J(Hd,.02f)
                    .J(LA,-.06f,0,.12f).J(LE,-.3f).J(RA,-.06f,0,-.12f).J(RE,-.3f).PassKey();
                // contact: the hips between the feet, at their lowest; turned with the leading leg, the chest against it, the head steady; the arm
                // opposite the leading leg forward (elbow bent), the other back (straighter)
                k.Key(land,stand).Root(c.X,.88f,c.Z,yaw).Drop(-.04f).J(B,.05f).J(P,0,s*.11f*amp).J(Sp,0,-s*.05f*amp).J(C,-.02f,-s*.12f*amp).J(Hd,.02f,s*.07f*amp)
                    .J(LA,(l?.3f:-.34f)*amp,0,.12f).J(LE,l?-.16f:-.32f-.28f*amp).J(RA,(l?-.34f:.3f)*amp,0,-.12f).J(RE,l?-.32f-.28f*amp:-.16f).PassKey();
            }
            return last;
        }
        /// <summary>
        /// A run (ModelFeet on), already under way at `t0`: `count` strikes along `track`, one every `cadence` s, the body leaning `lean` rad
        /// into it. Each foot strikes .22 m ahead of the hips and stays planted through its stance (the hips low, the knee taking the weight)
        /// to its toe-off half a step later; then a flight, both feet up, the trailing heel tucked high behind and the other knee driving
        /// forward to reach for its strike; the arms pump against the legs with the elbows near a right angle. The leg in the air is keyed
        /// (unplanted). Returns the last strike.
        /// </summary>
        public static float Run(NativeEntranceKeys k,List<NativeEntrancePose.Step> plan,NativeIntroFeet.Track track,float t0,int count,float cadence=.34f,float lean=.2f,bool left=true)
        {
            var stand=NativeEntranceKeys.Stand();float yaw=track.Yaw,last=t0;
            for(int i=0;i<count;i++)
            {
                bool l=(i%2==0)==left;float s=l?1:-1,strike=t0+(i+1)*cadence,off=strike+cadence*.5f,fly=strike+cadence*.78f;last=strike;
                // (the foot leaves the floor at its last toe-off, two strikes back, and swings high to this strike)
                var at=track.At(strike)+track.Dir*.22f+track.Side*(l?.08f:-.08f);
                plan.Add(new NativeEntrancePose.Step(l,strike-2*cadence+cadence*.5f-.02f,strike,at.X,at.Z,yaw+(l?.05f:-.05f),.3f));
                var r0=track.At(strike);var r1=track.At(off);var r2=track.At(fly);
                int stance=l?LL:RL,swing=l?RL:LL;
                // the strike: the foot planted ahead of the hips, the hips at their lowest, the other heel tucked behind; the arm opposite
                // the striking leg forward
                k.Key(strike,stand).Root(r0.X,.88f,r0.Z,yaw).Drop(-.09f).Planted(l?1:0,l?0:1).J(B,lean+.03f).J(P,0,s*.12f).J(C,0,-s*.16f).J(Hd,-lean*.7f,s*.06f)
                    .J(swing,.35f,0,-s*.05f).J(swing+1,1.75f)
                    .J(LA,l?-.75f:.55f,0,.14f).J(LE,l?-1.6f:-1.15f).J(RA,l?.55f:-.75f,0,-.14f).J(RE,l?-1.15f:-1.6f).PassKey();
                // the toe-off: still planted, the body rising over it, the other knee driving forward and up
                k.Key(off,stand).Root(r1.X,.88f,r1.Z,yaw).Drop(-.02f).Planted(l?1:0,l?0:1).J(B,lean).J(P,0,s*.04f).J(C,0,-s*.05f).J(Hd,-lean*.7f)
                    .J(swing,-1.05f,0,-s*.04f).J(swing+1,1.35f)
                    .J(LA,-.1f,0,.14f).J(LE,-1.4f).J(RA,-.1f,0,-.14f).J(RE,-1.4f).PassKey();
                // the flight: both feet up, the body at its highest, the leg that pushed off trailing with its heel tucked, the other reaching
                k.Key(fly,stand).Root(r2.X,.88f,r2.Z,yaw).Drop(.05f).Planted(0).J(B,lean).J(P,0,-s*.08f).J(C,0,s*.12f).J(Hd,-lean*.7f)
                    .J(stance,.3f,0,s*.05f).J(stance+1,1.6f).J(swing,-.85f,0,-s*.05f).J(swing+1,.7f)
                    .J(LA,l?.4f:-.65f,0,.14f).J(LE,l?-1.2f:-1.5f).J(RA,l?-.65f:.4f,0,-.14f).J(RE,l?-1.5f:-1.2f).PassKey();
            }
            return last;
        }
    }
}
