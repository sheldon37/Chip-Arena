using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The bow's pilot pose (presentation only), third person over the ranged gait (NativeBowMotion) and the first-person view
    /// model (View): the bladed archer's stance (the left shoulder to the target, the upper body leaning to keep the bow arm on
    /// the arrow line, the aim pitch taken in the lean), the head along the aim, then the arms by IK on the arrow line: the bow
    /// hand where the arrow line meets the full draw length from the anchor (the right cheek), the string hand on the nock at
    /// the replicated draw's pull, reaching the cheek at full draw. Phases (NativeBow.At): the carry with an arrow nocked, the
    /// raise, the draw, the hold with its fatigue tremor after 2.5 s, the release's follow-through past the ear, the nock (the
    /// hand back over the right shoulder to the quiver, an arrow drawn and set on the string), the let-down, the Volley's canted
    /// fan draw and the Rain of Arrows' sky shot. Timing and shape keys are derived from the owner's Explosive pack 2Hand-Bow
    /// takes (tools/export_pack_bow.mjs, tools/pack-bow-provenance.json): the shares of the bladed turn, the aim lean, the
    /// release's travel and timing, the carry. No allocation per frame.
    /// </summary>
    public static class NativeBowPose
    {
        // ---- derived from the pack (Tests/BowChecks compares them with tools/pack-bow-provenance.json) ----
        public const float PackBlade=1.5449f,PackHips=.7206f,PackSpine=.4997f,PackChest=.3246f,PackLeanSpine=.213f,PackLeanChest=.2547f,PackPitchUp=.1269f;
        public const float PackFollowBack=.2402f,PackFollowDown=.1182f,PackFollowOut=.0574f,PackFollowPeak=.1f,PackFollowSettle=.2667f,PackDrawToAnchor=.3222f;
        // ---- the voxel archer (the pack's stance adapted to the rig: far wider shoulders, a far larger head, shorter arms) ----
        /// <summary>The bladed turn at full raise (rad), its shares (the hips only while standing: a walking draw keeps the hips on
        /// the gait and the spine takes their share), the head's turn back to the aim.</summary>
        public const float Blade=1.5f,HipsShare=PackHips/PackBlade,SpineShare=PackSpine/PackBlade,ChestShare=PackChest/PackBlade;
        /// <summary>The standing lean (rad: the chest rolled about its front so the bow shoulder rises toward the arrow line from
        /// the cheek; the voxel head sits far above the shoulders) and the share of the aim pitch the lean takes (the pack's up
        /// hold: lean over lean plus the residual arrow pitch), split spine/chest as the pack's.</summary>
        public const float Lean=.34f,LeanMax=.62f,PitchLean=(PackLeanSpine+PackLeanChest)/(PackLeanSpine+PackLeanChest+PackPitchUp),SpineLean=PackLeanSpine/(PackLeanSpine+PackLeanChest);
        /// <summary>The anchor in chest space of the bladed, leaning chest (the full-draw nock: by the right jaw, where the voxel
        /// arms reach it), the draw's start (m ahead of the anchor along the arrow: the push-pull draw starts with the bow in close
        /// and the string hand ahead, then the bow arm pushes out as the string comes back), the head's tilt and nod toward the
        /// string (rad), the bow's cant (rad: the top limb toward the archer's right), the Volley's cant and the sky shot's pitch.</summary>
        public static readonly Vector3 Anchor=new Vector3(-.08f,.37f,.12f);
        public const float Start=.15f,HeadTilt=.3f,HeadNod=.1f,Cant=.17f,VolleyCant=1.15f,SkyPitch=1.08f;
        /// <summary>The release's follow-through (m): back along the arrow, out from the face, down; the bow tipping forward (rad).</summary>
        public const float FollowBack=.2f,FollowOut=.07f,FollowDown=.045f,BowTip=.24f;
        /// <summary>The carry (chest space): the bow hand low at the left front, the bow pointing forward and down, canted; the
        /// string hand free (the bow hand's finger keeps the nocked arrow on the shelf).</summary>
        public static readonly Vector3 CarryWrist=new Vector3(.33f,-.19f,.27f),CarryForward=new Vector3(.12f,-.62f,.78f),CarryUp=new Vector3(-.42f,.42f,.4f);
        /// <summary>The sprint carry (chest space): the bow swept low and back at the left side.</summary>
        public static readonly Vector3 SprintWrist=new Vector3(.36f,-.24f,-.02f),SprintForward=new Vector3(.1f,-.35f,-.93f),SprintUp=new Vector3(.2f,.9f,-.3f);

        private static Quaternion Look(Vector3 fwd,Vector3 up)=>CapturedRig.Look(fwd,up);
        private static Vector3 Rot(Quaternion q,Vector3 v)=>Vector3.Transform(v,q);

        /// <summary>The frame's posed bow (pose-world space: the rig root's frame), for the model, effects and checks.</summary>
        public struct Frame
        {
            public Vector3 Aim,Anchor,Nock,BowWrist,DrawWrist,Left,Up;public Quaternion BowRotation,DrawRotation;public float Pull,Raised,Extend,BowReach,DrawReach,DrawWeight;
            /// <summary>The draw hand is carrying an arrow from the quiver, and that arrow's nock end and direction (hand space).</summary>
            public bool HandArrow;public Vector3 HandArrowAt,HandArrowDir;
        }
        /// <summary>The aim direction in the rig's frame: the replicated look yaw offset (AimYaw - Yaw) and pitch (deg, up +),
        /// as RangedPose.Arms aims a gun (the camera modes converge the sent look on the crosshair point).</summary>
        public static Vector3 AimOf(float yawOffsetDeg,float pitchDeg)
        {
            const float D=(float)Math.PI/180;return Vector3.Transform(Vector3.UnitZ,CapturedRig.Rot(CapturedRig.Y,-yawOffsetDeg*D)*CapturedRig.Rot(CapturedRig.X,-pitchDeg*D));
        }
        /// <summary>The bladed stance over a posed rig (local rotations): `aim` the rig-frame aim, `k` the raise, `still` 1 standing
        /// (the hips take their share). World() current after.</summary>
        public static void Torso(CapturedRig.Pose p,Vector3 aim,float k)=>Torso(p,aim,k,1,0);
        public static void Torso(CapturedRig.Pose p,Vector3 aim,float k,float still,float sky)
        {
            if(k<=1e-4f){p.World();return;}float yaw=(float)Math.Atan2(aim.X,aim.Z),pitch=(float)Math.Asin(MeleeMath.Clamp(aim.Y,-1,1));
            float turn=(yaw-Blade)*k,hips=turn*HipsShare*still,spine=turn*(SpineShare+HipsShare*(1-still)),chest=turn*ChestShare;
            float lean=MeleeMath.Clamp(Lean+PitchLean*pitch,-.35f,LeanMax)*k;
            p.Rotation[CapturedRig.Pelvis]=CapturedRig.Rot(CapturedRig.Y,hips)*p.Rotation[CapturedRig.Pelvis];
            p.Rotation[CapturedRig.Spine]=p.Rotation[CapturedRig.Spine]*CapturedRig.Rot(CapturedRig.Y,spine)*CapturedRig.Rot(CapturedRig.Z,lean*SpineLean);
            p.Rotation[CapturedRig.Chest]=p.Rotation[CapturedRig.Chest]*CapturedRig.Rot(CapturedRig.Y,chest)*CapturedRig.Rot(CapturedRig.Z,lean*(1-SpineLean));
            p.World();
            // The head along the aim, tilted and nodded toward the string (the jaw comes down to the anchor); the neck takes 40%.
            Vector3 left=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,aim));Vector3 up=Vector3.Normalize(Vector3.Cross(aim,left));
            float tilt=HeadTilt*(1-sky);up=Vector3.Normalize(up*(float)Math.Cos(tilt)-left*(float)Math.Sin(tilt));
            Quaternion want=Look(aim,up)*CapturedRig.Rot(CapturedRig.X,HeadNod*(1-sky)),head=p.WorldRotation[CapturedRig.Head],delta=want*Quaternion.Inverse(head);
            Quaternion neckW=Quaternion.Slerp(Quaternion.Identity,delta,.4f*k)*p.WorldRotation[CapturedRig.Neck];
            p.Rotation[CapturedRig.Neck]=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest])*neckW;p.World();
            p.Rotation[CapturedRig.Head]=Quaternion.Inverse(p.WorldRotation[CapturedRig.Neck])*Quaternion.Slerp(p.WorldRotation[CapturedRig.Head],want,k);p.World();
        }
        private static Vector3 Chest(CapturedRig.Pose p,Vector3 world)=>Vector3.Transform(world-p.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]));
        public static Vector3 World(CapturedRig.Pose p,Vector3 chest)=>p.WorldPosition[CapturedRig.Chest]+Vector3.Transform(chest,p.WorldRotation[CapturedRig.Chest]);
        private static Quaternion ChestQ(CapturedRig.Pose p,Quaternion world)=>Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest])*world;
        /// <summary>A shoulder's distance to a wrist target (the arm reaches .515 m).</summary>
        public static float Reach(CapturedRig.Pose p,bool left,Vector3 target)
        {
            int c=left?CapturedRig.LClav:CapturedRig.RClav;Vector3 shoulder=p.WorldPosition[c]+Vector3.Transform(CapturedRig.Bones[c+1].Position,p.WorldRotation[c]);
            return Vector3.Distance(shoulder,target);
        }
        /// <summary>The nock stroke at `s` (0..1): a Catmull-Rom curve through the follow-through, the quiver's mouth (at k1), the
        /// drawn arrow (at k2) and the string at rest, tangents from the neighbouring points over their knot spans.</summary>
        private static Vector3 NockStroke(Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3,float k1,float k2,float s)
        {
            Vector3 m1=(p2-p0)/Math.Max(1e-3f,k2),m2=(p3-p1)/Math.Max(1e-3f,1-k1),m0=(p1-p0)/Math.Max(1e-3f,k1),m3=(p3-p2)/Math.Max(1e-3f,1-k2);
            Vector3 a,b,ma,mb;float lo,hi;
            if(s<k1){a=p0;b=p1;ma=m0;mb=m1;lo=0;hi=k1;}else if(s<k2){a=p1;b=p2;ma=m1;mb=m2;lo=k1;hi=k2;}else{a=p2;b=p3;ma=m2;mb=m3;lo=k2;hi=1;}
            float span=Math.Max(1e-4f,hi-lo),t=MeleeMath.Clamp((s-lo)/span,0,1),t2=t*t,t3=t2*t;
            return (2*t3-3*t2+1)*a+(t3-2*t2+t)*span*ma+(-2*t3+3*t2)*b+(t3-t2)*span*mb;
        }
        /// <summary>The nock path's beats (fractions of NativeBow.Nock): the hand at the quiver's mouth, the arrow drawn out.</summary>
        public const float Mouth=.58f,Drawn=.7f;
        private static readonly int[] RightArm={CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        private static readonly Quaternion[] savedRight=new Quaternion[3];

        /// <summary>
        /// The arms on the arrow line over the posed torso (World() current): the frame's bow from NativeBow.At (`v`), the
        /// rig-frame `aim`, the full-draw `anchor` (rig frame), the draw's `start`, the raise `k`, the carry's bow wrist (chest
        /// space; the pack carry while travelling), the sprint carry weight. The bow wrist rides the arrow line at the bow arm's
        /// extension, the string bends to the pull's nock, the string hand holds it (the follow-through, the nock path from the
        /// quiver and the hand-held arrow), free while carrying. Returns the posed bow frame.
        /// </summary>
        public static Frame Arms(CapturedRig.Pose p,in NativeBow.Read r,in NativeBow.View v,float time,Vector3 aim,Vector3 anchor,float start,float k,Vector3 carryBow,float sprint,bool firstPerson)
        {
            var f=new Frame{Aim=aim,Raised=k,Extend=v.Extend};float kk=MeleeMath.Clamp(k,0,1);
            Vector3 left=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,aim)),up=Vector3.Normalize(Vector3.Cross(aim,left)),right=-left;
            // The aimed bow: canted (more for the Volley's fan), tipped forward after a release, trembling when tired.
            float cant=firstPerson?ViewCant-(VolleyCant-Cant)*v.Fan:Cant+(VolleyCant-Cant)*v.Fan;Vector3 upC=Vector3.Normalize(up*(float)Math.Cos(cant)-left*(float)Math.Sin(cant));
            Quaternion bow=Look(aim,upC);
            NativeBow.Tremor(r.Seed,time,v.Tremor,out var shake,out float ty,out float tp);
            if(v.Tremor>0)bow=bow*CapturedRig.Euler(tp,ty,0);
            float tip=v.Follow*BowTip;if(tip>0)bow=bow*CapturedRig.Rot(CapturedRig.X,tip);
            float pull=Math.Max(0,v.Pull),e=MeleeMath.Clamp(Math.Max(v.Extend,pull),0,1);
            // The bow wrist on the arrow line: the extension's nock (the start ahead of the anchor .. the anchor) less the bow frame's
            // nock at that extension, so the arrow line passes the anchor along the aim at every extension.
            Vector3 lineNock=anchor+aim*(start*(1-e));
            Vector3 aimed=lineNock-Rot(bow,NativeBow.NockAt(e))+Rot(bow,shake)+aim*(.02f*v.Follow);
            Quaternion chest=p.WorldRotation[CapturedRig.Chest];
            Vector3 cw=carryBow;Quaternion cq=chest*Look(CarryForward,CarryUp);
            if(sprint>1e-4f){cw=Vector3.Lerp(cw,SprintWrist+new Vector3(0,.03f*(float)Math.Sin(time*9),.06f*(float)Math.Sin(time*9)),sprint);cq=Quaternion.Slerp(cq,chest*Look(SprintForward,SprintUp),sprint);}
            f.BowWrist=Vector3.Lerp(World(p,cw),aimed,kk);f.BowRotation=Quaternion.Slerp(cq,bow,kk);
            // The string hand: on the nock at the pull, along the arrow line.
            Quaternion dq=Look(Rot(f.BowRotation,Vector3.UnitZ),Rot(f.BowRotation,Vector3.UnitY));dq=Quaternion.Slerp(dq,Look(aim,up),kk);
            Vector3 nock=f.BowWrist+Rot(f.BowRotation,NativeBow.NockAt(pull));f.Nock=nock;f.Pull=pull;
            Vector3 hand=nock-Rot(dq,NativeBowModels.Hook);float hold=kk,onString=1;
            if(e>pull+.02f&&!v.Nocked)
            {
                onString=0;
                // Released (the string has left the fingers): the hand flies back along the arrow past the ear, out from the face
                // and a little down.
                hand=anchor-Rot(dq,NativeBowModels.Hook)+(-aim*FollowBack+right*FollowOut-Vector3.UnitY*FollowDown)*v.Follow;
            }
            if(v.Nocking>0&&v.Nocking<1)
            {
                // The nock: from the follow-through up over the right shoulder to the quiver's mouth, the arrow drawn out along the
                // quiver, then brought down onto the string at rest.
                float u=v.Nocking;Vector3 mouth=World(p,NativeBowModels.QuiverMouth+new Vector3(.03f,.05f,.07f));
                Vector3 axis=Vector3.Normalize(World(p,NativeBowModels.QuiverMouth)-World(p,NativeBowModels.QuiverBase));Vector3 drawn=mouth+axis*.16f+Vector3.UnitY*.02f;
                Vector3 rest=f.BowWrist+Rot(f.BowRotation,NativeBow.NockAt(0))-Rot(dq,NativeBowModels.Hook);
                // From the follow-through as it is now (the casts start their nock while it settles), so the path leaves the hand where it is.
                Vector3 from=anchor-Rot(dq,NativeBowModels.Hook)+(-aim*FollowBack+right*FollowOut-Vector3.UnitY*FollowDown)*v.Follow;
                float a0=NativeBow.Follow/NativeBow.Nock;Vector3 path;
                float on;
                if(u<a0){path=hand;on=onString;}
                else
                {
                    // One smooth stroke through the mouth and the drawn arrow (a spline, eased in and out as a whole: no stop at the quiver).
                    float sNorm=MeleeMath.Ease((u-a0)/(1-a0)),k1=(Mouth-a0)/(1-a0),k2=(Drawn-a0)/(1-a0);
                    path=NockStroke(from,mouth,drawn,rest,k1,k2,sNorm);on=sNorm>k2?MeleeMath.Ease((sNorm-k2)/(1-k2)):0;
                }
                float w=MeleeMath.Ease(Math.Min(1,(1-u)/.08f));hand=Vector3.Lerp(hand,path,w);hold=Math.Max(hold,w);onString=MeleeMath.Lerp(onString,on,w);
                if(u>=Mouth&&u<.97f)
                {
                    // The arrow in the hand: along the quiver as it is drawn, turning onto the arrow line as it reaches the string.
                    f.HandArrow=true;float turn=MeleeMath.Ease((u-Drawn)/(.98f-Drawn));Vector3 dir=Vector3.Normalize(Vector3.Lerp(axis,Rot(f.BowRotation,Vector3.UnitZ),turn));
                    f.HandArrowDir=Vector3.Transform(dir,Quaternion.Inverse(dq));f.HandArrowAt=NativeBowModels.Hook-f.HandArrowDir*(.02f*(1-turn));
                }
            }
            f.DrawWrist=hand;f.DrawRotation=dq;f.Anchor=anchor;f.Left=left;f.Up=up;f.DrawWeight=hold;
            // IK: chest-space targets; the bow elbow down and out, the draw elbow back and high at full draw; the string hand blends
            // from its free swing (the gait, the pack carry, the sprint pump) by its hold.
            Vector3 aimC=Vector3.Transform(aim,Quaternion.Inverse(chest));
            Vector3 drawPole=Vector3.Lerp(new Vector3(-1,-.7f,-.2f),Vector3.Normalize(-aimC+new Vector3(0,.55f,0)),kk);
            CapturedRig.Arm(p,true,Chest(p,f.BowWrist),ChestQ(p,f.BowRotation),new Vector3(1,-.8f,-.25f));p.World();
            // Where the bow hand got to (the arm folds no tighter than its reach allows): the string, and the hand on it, follow
            // the bow as drawn.
            f.BowReach=Reach(p,true,f.BowWrist);Vector3 slip=p.WorldPosition[CapturedRig.LHand]-f.BowWrist;
            if(slip.LengthSquared()>1e-10f){f.BowWrist+=slip;f.Nock+=slip;hand+=slip*onString;f.DrawWrist=hand;}
            for(int i=0;i<3;i++)savedRight[i]=p.Rotation[RightArm[i]];
            CapturedRig.Arm(p,false,Chest(p,f.DrawWrist),ChestQ(p,f.DrawRotation),drawPole);
            if(hold<1)for(int i=0;i<3;i++)p.Rotation[RightArm[i]]=Quaternion.Slerp(savedRight[i],p.Rotation[RightArm[i]],Math.Max(0,hold));
            p.World();
            f.DrawReach=Reach(p,false,f.DrawWrist);
            return f;
        }
        /// <summary>The rig-frame full-draw anchor of a posed (bladed) chest.</summary>
        public static Vector3 ChestAnchor(CapturedRig.Pose p)=>World(p,Anchor);

        // ---- the aim the pose takes for each phase ----
        /// <summary>The aim the frame poses: the replicated look, raised toward the sky shot's steep pitch through Rain of Arrows.</summary>
        public static Vector3 PhaseAim(Actor a,in NativeBow.View v,bool firstPerson)
        {
            float pitch=firstPerson?0:a.Pitch;float yaw=firstPerson?0:a.AimYaw-a.Yaw;
            if(v.Sky>0){const float D=(float)Math.PI/180;pitch=MeleeMath.Lerp(pitch,(firstPerson?ViewSkyPitch:SkyPitch)/D,v.Sky);}
            return AimOf(yaw,pitch);
        }

        // ---- first person (the generic view-model route: the rig's frame is the camera's, the aim straight ahead) ----
        /// <summary>The view model's layout (rig frame, source: x the left, y up, z the view): the arrow line straight ahead
        /// (ViewAnchor: the full-draw nock, under and left of the eye so the arrow points from the lower right at the crosshair),
        /// the chest bladed (unseen; it brings both shoulders within reach of the line), the bow canted away from the centre;
        /// the eye's place in the rig (ViewEye), so the root sits at the camera less ViewEye x ViewScale.</summary>
        public static readonly Vector3 ViewAnchor=new Vector3(0,.38f,-.03f),ViewEye=new Vector3(-.167f,.572f,-.056f);
        public const float ViewCarryYaw=-14,ViewCarryPitch=-24,ViewSkyPitch=.42f,ViewBlade=1.3f,ViewCant=-.32f,ViewStart=.2f,ViewScale=NativeFirstPersonPose.Scale;
        /// <summary>The root's offset from the camera (camera space, Unity frame: x right) for RenderFirstPerson.</summary>
        public static Vector3 ViewOffset=>new Vector3(ViewEye.X,-ViewEye.Y,-ViewEye.Z)*ViewScale;
        /// <summary>The first-person bow over the view rig's rest pose: the torso bladed (unseen), the arms on the arrow line
        /// straight ahead (it converges on the crosshair), the same phases. Returns the posed bow frame.</summary>
        public static Frame View(CapturedRig.Pose p,Actor a,float time)
        {
            var r=NativeBowWire.Of(a);var v=NativeBow.At(r,time);
            // In view the sky shot's recovery lowers the bow as the next arrow is set, as after a normal shot (the raised sky bow
            // would bring the string arm across the lens).
            if(v.Phase==NativeBow.Phase.Rain&&v.Nocking>0){float down=1-NativeBow.Smooth(v.Nocking/.6f);v.Raised=Math.Min(v.Raised,down);v.Sky=Math.Min(v.Sky,down);}
            p.Rotation[CapturedRig.Pelvis]=p.Rotation[CapturedRig.Spine]=Quaternion.Identity;p.Rotation[CapturedRig.Chest]=CapturedRig.Rot(CapturedRig.Y,-ViewBlade);p.World();
            // In view the bow stays in the hands (a lowered bow would leave the frame): the carry points it down and left of the
            // aim, raised onto the arrow line through the draw, the release and the casts.
            float low=1-v.Raised;var aim=AimOf(ViewCarryYaw*low,ViewCarryPitch*low);
            if(v.Sky>0)aim=Vector3.Normalize(Vector3.Lerp(aim,PhaseAim(a,v,true),v.Sky));
            float k=1;Vector3 anchor=ViewAnchor+new Vector3(.02f,-.05f,0)*low;
            return Arms(p,r,v,time,aim,anchor,ViewStart,k,ViewCarry,0,true);
        }
        /// <summary>The view's lowered bow (chest space of the bladed view torso).</summary>
        public static readonly Vector3 ViewCarry=new Vector3(.12f,-.02f,.38f);
    }

    /// <summary>
    /// A third-person bow pilot (host, guests, bots, the killcam), as GunMotion poses a gun pilot: the gait in the bow's
    /// stance (planted feet, the replicated velocity's stride, the jump layer), the owner's pack 2Hand-Bow travel carry
    /// (NativeRangedLocomotion: walk, strafe and run in eight directions, the reach seat), the sprint lean and the bow's sprint
    /// carry, then the bow hold (NativeBowPose) over it: raised through the draw, the release, the casts; lowered to the carry
    /// otherwise. Finish runs the ground pass after the native overlays (juggle, roll, reactions, recovery) and the pose
    /// transitions. Replicated Actor fields only (through NativeBowWire); no allocation per frame.
    /// </summary>
    public sealed class NativeBowMotion
    {
        public readonly MeleeRig Rig=new MeleeRig();
        private readonly MeleeGait gait=new MeleeGait();
        private readonly MeleeView view=new MeleeView();
        private readonly NativePoseTransition transition=new NativePoseTransition();
        public readonly NativeBowWatch Watch=new NativeBowWatch();
        private float last=-100,dt=1/60f,sprintBlend;private int viewId=int.MinValue;
        public NativeJumpMotion JumpLayer;public NativeSprintMotion SprintLayer;public NativeRangedLocomotion Locomotion;
        /// <summary>The frame's posed bow (rig frame), as the last Sample left it.</summary>
        public NativeBowPose.Frame Shot;public NativeBow.View Bow;public NativeBow.Read Read;
        public bool GroundSkipped{get;private set;}
        public bool Planted(bool left)=>gait.Planted(left);
        public const string Carry="bow carry",Draw="bow draw",Hold="bow hold",Release="bow release",Nock="bow nock",LetDown="bow let-down",Volley="volley",Rain="rain of arrows";
        public static string StateOf(in NativeBow.View v)
        {
            switch(v.Phase){case NativeBow.Phase.Draw:return Draw;case NativeBow.Phase.Hold:return Hold;case NativeBow.Phase.Release:return Release;case NativeBow.Phase.Nock:return Nock;
                case NativeBow.Phase.LetDown:return LetDown;case NativeBow.Phase.Volley:return Volley;case NativeBow.Phase.Rain:return Rain;default:return Carry;}
        }
        private Actor shown;private bool shownJuggled;
        /// <summary>One third-person frame after the caller placed the rig (Rig.GroupPosition / GroupYaw); then Finish.</summary>
        public string Pose(Actor a,float time,CapturedRig.Pose pose,bool juggled,NativeJuggleTrack juggle,out bool react)
        {
            bool roll=!juggled&&a.Rolling(time),special=!juggled&&NativeSpecialPose.Active(a,time),down=!juggled&&NativeSpecialPose.Down(a,time);
            Sample(a,time,pose);string state=StateOf(Bow);
            if(juggled){NativeJugglePose.Apply(pose,time,juggle,a.Velocity.y);state="juggle";}
            if(roll){CapturedRig.Roll(pose,(time-a.RollAt)/a.VisualRollDuration);state="roll (adapted)";}
            react=!juggled&&!roll&&!special&&!down&&a.ReactionAt>-90;
            if(special){NativeSpecialPose.Special(pose,a,time,false);state=a.SpecialKind;}
            if(down){NativeSpecialPose.Recover(pose,a,time);state="recovery";}
            GroundSkipped=a.Hp<=0||juggled||down||roll;shown=a;shownJuggled=juggled;
            return state;
        }
        public void Finish(CapturedRig.Pose pose,float time)
        {
            Rig.ReadPose(pose);gait.GroundPass(Rig,view,time,dt,GroundSkipped);Locomotion?.Seat(Rig,gait,view.Grounded,time,GroundSkipped);
            if(shown!=null)transition.Apply(Rig,shown,time,shownJuggled);shown=null;
            Rig.WritePose(pose);pose.World();
        }
        /// <summary>The gait, the pack carry and the bow hold for one frame. The caller sets Rig.GroupPosition / GroupYaw first.</summary>
        public void Sample(Actor a,float time,CapturedRig.Pose pose)
        {
            bool restart=last<-90||time<last-MeleeGait.REWIND||time-last>.35f;
            dt=last<-90||time<last||time-last>.35f?1/60f:Math.Min(.1f,time-last);last=time;
            const float D=(float)Math.PI/180;var v=view;var r=Rig;
            if(a.Id!=viewId){viewId=a.Id;v.Id=a.Id.ToString();restart=true;Watch.Clear();}
            v.Spec=NativeBowWire.Spec;v.Yaw=-a.Yaw*D;v.Pitch=a.Pitch*D;v.Grounded=a.Grounded;v.Hp=a.Hp;v.Sprinting=a.Sprinting;v.HitStunUntil=a.StunUntil;
            v.RollAt=a.RollAt;v.RollUntil=a.RollAt>-90?a.RollAt+a.VisualRollDuration:-100;
            bool planted=v.HitStunUntil>time||v.Rolling(time);
            r.Reset();r.LegsAuthored=r.LegsLayered=r.MocapPosed=false;r.WeaponReach=1;
            if(planted)r.Motion*=(float)Math.Exp(-MeleeMath.Clamp(dt,0,.05f)*22);
            v.Vel=planted?new Vector3(0,a.Velocity.y,0):new Vector3(-a.Velocity.x,a.Velocity.y,a.Velocity.z);
            gait.PelvisYawScale=1;gait.Locomotion(r,v,time,dt,planted);
            v.Vel=new Vector3(-a.Velocity.x,a.Velocity.y,a.Velocity.z);
            float run=MeleeMath.Ease((r.Motion-.1f)/.65f);
            sprintBlend+=((v.Sprinting?1:0)-sprintBlend)*(1-(float)Math.Exp(-Math.Min(.05f,Math.Max(0,dt))*10));
            float sprint=SprintLayer?.Advance(r,a,time,dt)??0;
            float move=Locomotion?.Bow(r,v,v.Spec,time,sprint)??0;
            if(sprintBlend>1e-4f)r.AddE(CapturedRig.Spine,.2f*sprintBlend*run,0,0);
            JumpLayer?.Ranged(r,a,v.Spec,time,dt,.4f);
            // The bow: its phase from the replicated draw and casts (a let-down the presentation watched when not stamped).
            Read=NativeBowWire.Of(a);Watch.Observe(Read,time);Bow=NativeBow.At(Read,time,Watch.CancelAt,Watch.CancelPull);
            r.WritePose(pose);pose.World();
            var aim=NativeBowPose.PhaseAim(a,Bow,false);float k=Bow.Raised,still=1-MeleeMath.Ease(r.Motion/.4f);
            NativeBowPose.Torso(pose,aim,k,still,Bow.Sky);
            Vector3 anchor=NativeBowPose.ChestAnchor(pose);
            Vector3 carryBow=Locomotion!=null&&move>0?NativeBowPose.CarryWrist+(Locomotion.BowHand-Locomotion.BowHandMean)*.6f:NativeBowPose.CarryWrist;
            Shot=NativeBowPose.Arms(pose,Read,Bow,time,aim,anchor,NativeBowPose.Start,k,carryBow,sprint*(1-k),false);
        }
    }
}
