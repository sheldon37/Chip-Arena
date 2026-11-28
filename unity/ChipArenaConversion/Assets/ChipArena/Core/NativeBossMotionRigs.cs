using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The two reference rigs (docs/BOSS-MOTION.md "Example wiring"): how a boss puts the motion library together, run by the
    /// checks (Tests/BossMotionChecks.cs) and the offline renders. Not a shipped boss: the boss designs take their own bodies,
    /// keys and tuning from here.
    /// <list type="bullet">
    /// <item><see cref="BossBipedRig"/>: a heavy biped about 4.5 m tall with long arms (the gait's two legs, arms swinging against
    /// the feet on second-order dynamics, an overhead slam on the action timeline with IK fists at contact, shackle chains on the
    /// wrists, breath, look-at, flinch and stagger).</item>
    /// <item><see cref="BossCrawlerRig"/>: a six-legged mechanical crawler on a tripod gait with a dragging cable tail and a
    /// sensor head that tracks its target.</item>
    /// </list>
    /// Each joint's pose is its pivot (the hip, the knee, the shoulder) and its segment's rotation (local -Y down a limb, +Z
    /// forward): a model part hangs from it.
    /// </summary>
    public sealed class BossBipedRig
    {
        public const int Pelvis=0,Belly=1,Chest=2,Neck=3,Head=4,Jaw=5,LThigh=6,LShin=7,LFoot=8,RThigh=9,RShin=10,RFoot=11,
            LUpperArm=12,LForearm=13,LFist=14,RUpperArm=15,RForearm=16,RFist=17,LShoulder=18,RShoulder=19,Count=20;
        public const float UpperArm=1.22f,Forearm=1.28f,FistReach=.24f,SlamReach=2.9f;
        public static readonly BossGaitDef Gait=new BossGaitDef
        {
            Legs=new[]
            {
                new BossLegDef{Hip=new Vector3(-.42f,-.08f,0),Home=new Vector3(-.5f,0,.06f),Pole=new Vector3(-.12f,0,1),Upper=1.12f,Lower=1.08f,Phase=0,Side=-1,Ankle=.15f},
                new BossLegDef{Hip=new Vector3(.42f,-.08f,0),Home=new Vector3(.5f,0,.06f),Pole=new Vector3(.12f,0,1),Upper=1.12f,Lower=1.08f,Phase=.5f,Side=1,Ankle=.15f}
            },
            WalkSpeed=1.3f,RunSpeed=3.6f,WalkStride=2.3f,RunStride=4.1f,WalkDuty=.64f,RunDuty=.5f,WalkLift=.2f,RunLift=.42f,
            MinFrequency=.52f,MinSwing=.26f,MaxSwing=.8f,SettleDistance=.2f,SettleSwing=.5f,ReachMargin=.3f,
            BodyHeight=2.27f,Bob=.07f,RunBob=.12f,Sway=.11f,Roll=.06f,Twist=.11f,LandDip=.35f,SupportFrequency=1.6f,SupportDamping=.5f,
            LiftPeak=.38f,ToeOff=.4f,HeelStrike=.22f,FloorFollow=.6f
        };
        /// <summary>The overhead slam: wind up 0.75 s (arms overhead, leaning back, crouching), strike 0.26 s accelerating into
        /// contact (the authority's hit), the impact's squash, the follow-through, a settle that overshoots rest and lands still.
        /// Keys: 0 rest, 1 wound, 2 struck, 3 through, 4 rest.</summary>
        public static readonly BossActionDef Slam=new BossActionDef("slam",.16f,.4f,
            new BossPhase(BossPhaseKind.Anticipation,.75f,1,BossEase.InOut),
            new BossPhase(BossPhaseKind.Strike,.26f,2,BossEase.In,2.2f),
            new BossPhase(BossPhaseKind.Contact,.14f,2.3f,BossEase.Out,2),
            new BossPhase(BossPhaseKind.FollowThrough,.34f,3,BossEase.InOut),
            new BossPhase(BossPhaseKind.Recover,.85f,4,BossEase.Settle,1.4f));
        // Key tables (rest, wound, struck, through, rest): shoulder pitch (0 hanging, pi overhead), elbow bend, arm spread, the
        // chest's forward bend, the crouch (m), the pelvis pitch, the head's pitch, the jaw, the fists' IK weight.
        private static readonly float[] kShoulder={.12f,3.02f,1.42f,1.3f,.12f},kElbow={.32f,.62f,.06f,.12f,.32f},kSpread={.2f,.14f,.3f,.34f,.2f},
            kSpine={0,-.3f,.62f,.7f,0},kCrouch={0,.2f,.56f,.62f,0},kPelvis={0,-.06f,.24f,.28f,0},kHead={0,-.28f,-.42f,-.36f,0},
            kJaw={.04f,.5f,.7f,.25f,.04f},kIk={0,0,1,1,0};

        public readonly BossPose Pose=new BossPose(Count);
        public readonly BossRootFollow Follow=new BossRootFollow{MaxTurnRate=1.9f,MaxTurnAccel=6.5f,TurnHalfLife=.16f,MaxBank=.16f,MaxPitch=.14f,LeanFrequency=1.1f,LeanDamping=.55f};
        public readonly BossGait Legs=new BossGait(Gait);
        public readonly BossActionPlayer Actions=new BossActionPlayer();
        public readonly BossLookAt Look=new BossLookAt{MaxYaw=1.0f,HalfLife=.2f,MaxRate=3f};
        public readonly BossFlinch Flinch=new BossFlinch();public readonly BossStagger Stagger=new BossStagger();
        public readonly BossChain LeftShackle=new BossChain(ShackleDef),RightShackle=new BossChain(ShackleDef);
        private static BossChainDef ShackleDef=>new BossChainDef{Count=7,Length=.17f,Rest=new Vector3(0,-1,0),Stiffness=0,TipStiffness=0,Damping=.7f,Radius=.05f,Friction=5};
        public uint Seed=0x51A3u;
        private BossSecondOrder lArm,rArm,lElbow,rElbow,chestBank,chestPitch,headYaw;private bool configured;
        /// <summary>The slam's contact point (the authority's disc centre, on the ground ahead) while one plays.</summary>
        public Vector3 SlamAt;
        private struct Channels{public float ShoulderL,ShoulderR,ElbowL,ElbowR,Spread,Spine,Crouch,Pelvis,Head,Jaw,Ik;}
        private static void Keys(float p,ref Channels c,float w)
        {
            float s=BossTimeline.Key(kShoulder,p),e=BossTimeline.Key(kElbow,p);
            c.ShoulderL+=s*w;c.ShoulderR+=s*w;c.ElbowL+=e*w;c.ElbowR+=e*w;c.Spread+=BossTimeline.Key(kSpread,p)*w;c.Spine+=BossTimeline.Key(kSpine,p)*w;
            c.Crouch+=BossTimeline.Key(kCrouch,p)*w;c.Pelvis+=BossTimeline.Key(kPelvis,p)*w;c.Head+=BossTimeline.Key(kHead,p)*w;c.Jaw+=BossTimeline.Key(kJaw,p)*w;c.Ik+=BossMath.Clamp01(BossTimeline.Key(kIk,p))*w;
        }
        /// <summary>The slam's own channels at a progress (the pure action layer the determinism check compares).</summary>
        public static void SlamChannels(float progress,out float shoulder,out float elbow,out float spine,out float crouch,out float head)
        {shoulder=BossTimeline.Key(kShoulder,progress);elbow=BossTimeline.Key(kElbow,progress);spine=BossTimeline.Key(kSpine,progress);crouch=BossTimeline.Key(kCrouch,progress);head=BossTimeline.Key(kHead,progress);}
        /// <summary>
        /// Poses the rig at `time` (the authority's timeline: the actions, flinches and stagger read it) after a frame of `dt`.
        /// Feed <see cref="Follow"/> first (Observe + Step on a guest, Follow on the host or offline), and the replicated action,
        /// blows and stagger into <see cref="Actions"/>, <see cref="Flinch"/> and <see cref="Stagger"/>.
        /// </summary>
        public void Update(double time,float dt,IBossGround ground,bool hasTarget,Vector3 target)
        {
            var f=Follow;if(!f.Started)return;
            if(!configured)
            {
                lArm.Configure(1.7f,.48f,0);rArm.Configure(1.7f,.48f,0);lElbow.Configure(2f,.45f,0);rElbow.Configure(2f,.45f,0);
                chestBank.Configure(1.5f,.45f,0);chestPitch.Configure(1.7f,.5f,0);headYaw.Configure(2.2f,.6f,0);configured=true;
            }
            float yaw=f.Yaw;var facing=BossMath.Yaw(yaw);var fwd=BossMath.Forward(yaw);var right=BossMath.Right(yaw);
            Stagger.Sample(time,out var sShift,out float sPitch,out float sBank,out float sCrouch,out _);
            Flinch.Sample(time,out float fPitch,out float fBank,out var fShift);
            Flinch.Sample(time,out float lagPitch,out float lagBank,out _,.06f);
            var root=f.Position+BossMath.Rotate(facing,sShift+fShift*.5f);
            if(f.Jumped)Legs.Reset(root,yaw,ground);
            // ---- the actions (a pure function of the authority's clock) mixed over the locomotion ----
            Actions.Sample(time);var a=Actions.Current;var b=Actions.Previous;
            var act=new Channels();float wa=a.Active?a.Weight:0,wb=b.Active?b.Weight:0,W=Math.Min(1,wa+wb);
            if(wa>0)Keys(a.Progress,ref act,wa);if(wb>0)Keys(b.Progress,ref act,wb);
            if(wa+wb>1e-4f){float n=W/(wa+wb);act.ShoulderL*=n;act.ShoulderR*=n;act.ElbowL*=n;act.ElbowR*=n;act.Spread*=n;act.Spine*=n;act.Crouch*=n;act.Pelvis*=n;act.Head*=n;act.Jaw*=n;act.Ik*=n;}
            // The contact point is ahead of the body until the hit, then held where the fists land.
            if(!(a.Active&&a.Age>=a.HitAt))SlamAt=new Vector3(root.X,ground.Height(root.X+fwd.X*SlamReach,root.Z+fwd.Z*SlamReach),root.Z)+fwd*SlamReach;
            float crouch=act.Crouch+sCrouch,pelvisPitch=act.Pelvis;
            Legs.ExtraOffset=new Vector3(0,-crouch,0)-fwd*(crouch*.25f);Legs.ExtraRotation=BossMath.AxisAngle(Vector3.UnitX,pelvisPitch+sPitch*.4f);
            Legs.Step(root,yaw,f.YawRate,f.Velocity,f.Body,dt,ground);
            var pelvis=Legs.BodyPosition;var rp=Legs.BodyRotation;Pose.Set(Pelvis,pelvis,rp);Pose.RootPosition=root;Pose.RootRotation=facing;
            // ---- the trunk: counter-twist against the pelvis, the breath, the lean with speed, the chest lagging the support ----
            float breath=BossBreath.Sample(time,.22f,Seed),idle=BossBreath.Idle(time,Seed,1,.3f),move=BossMath.Smooth(Legs.Speed/Gait.WalkSpeed);
            Look.Step(pelvis+BossMath.Rotate(rp,new Vector3(0,1.9f,.4f)),yaw,hasTarget,target,dt);
            float locoSpine=.1f*move+.1f*Legs.Run;float spine=BossMath.Lerp(locoSpine,act.Spine,W)+sPitch;
            float bankLag=chestBank.Step(-Legs.BankOffset*.9f+fBank+sBank,dt),pitchLag=chestPitch.Step(spine+fPitch,dt);
            var rb=rp*BossMath.AxisAngle(Vector3.UnitY,-Legs.TwistOffset*.7f)*BossMath.AxisAngle(Vector3.UnitX,pitchLag*.45f-.012f*breath)*BossMath.AxisAngle(Vector3.UnitZ,-bankLag*.5f);
            var belly=pelvis+BossMath.Rotate(rp,new Vector3(0,.42f,.04f));Pose.Set(Belly,belly,rb);
            var rc=rb*BossMath.AxisAngle(Vector3.UnitY,-Legs.TwistOffset*.8f+Look.Yaw*.3f)*BossMath.AxisAngle(Vector3.UnitX,pitchLag*.55f-.025f*breath)*BossMath.AxisAngle(Vector3.UnitZ,-bankLag*.5f+.03f*idle);
            var chest=belly+BossMath.Rotate(rb,new Vector3(0,.56f,.06f))+Vector3.UnitY*(.018f*breath);Pose.Set(Chest,chest,rc);
            // ---- the head: the look's share, the slam's lift (eyes stay on the target), a flinch that lags ----
            float hy=headYaw.Step(Look.Yaw*.5f+.06f*BossBreath.Idle(time,Seed,2,.22f),dt);
            float hp=BossMath.Lerp(-.1f*move,act.Head,W)+Look.Pitch*.8f+lagPitch*1.3f-pitchLag*.25f;
            var rn=rc*BossMath.AxisAngle(Vector3.UnitY,hy*.4f)*BossMath.AxisAngle(Vector3.UnitX,hp*.4f);var neck=chest+BossMath.Rotate(rc,new Vector3(0,.5f,.24f));Pose.Set(Neck,neck,rn);
            var rh=rn*BossMath.AxisAngle(Vector3.UnitY,hy*.6f)*BossMath.AxisAngle(Vector3.UnitX,hp*.6f)*BossMath.AxisAngle(Vector3.UnitZ,-lagBank*1.4f);var head=neck+BossMath.Rotate(rn,new Vector3(0,.3f,.12f));Pose.Set(Head,head,rh);
            float jaw=BossMath.Lerp(.04f+.03f*Math.Max(0,breath),act.Jaw,W);Pose.Set(Jaw,head+BossMath.Rotate(rh,new Vector3(0,-.12f,.18f)),rh*BossMath.AxisAngle(Vector3.UnitX,jaw*.6f));
            // ---- the legs (the gait's IK) ----
            for(int i=0;i<2;i++)
            {
                int t=i==0?LThigh:RThigh;Pose.Set(t,Legs.Hip[i],Legs.Upper[i]);Pose.Set(t+1,Legs.Knee[i],Legs.Lower[i]);Pose.Set(t+2,Legs.Foot[i],Legs.FootRotation[i]);
            }
            // ---- the arms: swung against the feet through second-order dynamics (they lag, carry on and settle), the slam's
            // keys over them, IK fists onto the slam point at contact; the shackles hang off the wrists ----
            float swingAmp=(.3f+.12f*Legs.Run)*move;
            float wantL=.12f+swingAmp*Legs.Forward[1]+.06f*breath,wantR=.12f+swingAmp*Legs.Forward[0]+.06f*breath;
            float sl=lArm.Step(wantL+lagPitch*1.6f,dt),sr=rArm.Step(wantR+lagPitch*1.6f,dt);
            float el=lElbow.Step(.3f+.3f*Math.Max(0,sl-.1f)+.12f*Legs.Run,dt),er=rElbow.Step(.3f+.3f*Math.Max(0,sr-.1f)+.12f*Legs.Run,dt);
            float spread=BossMath.Lerp(.2f+.04f*Legs.Run,act.Spread,W);
            for(int side=-1;side<=1;side+=2)
            {
                bool left=side<0;float shoulder=BossMath.Lerp(left?sl:sr,left?act.ShoulderL:act.ShoulderR,W),elbow=BossMath.Lerp(left?el:er,left?act.ElbowL:act.ElbowR,W);
                var at=chest+BossMath.Rotate(rc,new Vector3(side*.98f,.34f,.02f));Pose.Set(left?LShoulder:RShoulder,at,rc);
                var upper=rc*BossMath.AxisAngle(Vector3.UnitZ,side*spread)*BossMath.AxisAngle(Vector3.UnitX,-shoulder);var fore=upper*BossMath.AxisAngle(Vector3.UnitX,-elbow);
                // The arm is always solved by IK toward a point: the FK fist, drawn toward the slam's contact point by the IK
                // weight; the elbow bends in the FK arm's own plane (its -Z) and each bone is the FK bone swung onto the IK one (the
                // shortest swing keeps the roll). At weight 0 this is
                // the FK arm exactly; blending points (not rotations a half turn apart) can never flip the arm.
                var fist=at+BossMath.Rotate(upper,new Vector3(0,-UpperArm,0))+BossMath.Rotate(fore,new Vector3(0,-Forearm-FistReach,0));
                var goal=act.Ik>0?Vector3.Lerp(fist,SlamAt+right*(side*.55f)+Vector3.UnitY*.22f,act.Ik):fist;
                var bend=BossMath.Rotate(upper,-Vector3.UnitZ);var knee=BossIk.TwoBone(at,goal,UpperArm,Forearm+FistReach,bend,out var reached,.06f);
                upper=BossMath.FromTo(BossMath.Rotate(upper,-Vector3.UnitY),knee-at)*upper;fore=BossMath.FromTo(BossMath.Rotate(fore,-Vector3.UnitY),reached-knee)*fore;
                var elbowAt=at+BossMath.Rotate(upper,new Vector3(0,-UpperArm,0));var wrist=elbowAt+BossMath.Rotate(fore,new Vector3(0,-Forearm,0));
                Pose.Set(left?LUpperArm:RUpperArm,at,upper);Pose.Set(left?LForearm:RForearm,elbowAt,fore);Pose.Set(left?LFist:RFist,wrist,fore);
                (left?LeftShackle:RightShackle).Step(wrist+BossMath.Rotate(fore,new Vector3(0,-.06f,0)),fore,dt,ground);
            }
        }
    }

    /// <summary>A six-legged mechanical crawler: a tripod gait, a cable tail dragging behind, a sensor head on its target.</summary>
    public sealed class BossCrawlerRig
    {
        public const int Hull=0,Head=1,Count=2+6*3;
        /// <summary>Leg i's coxa (hip housing), femur and tibia joints.</summary>
        public static int Coxa(int i)=>2+i*3;public static int Femur(int i)=>3+i*3;public static int Tibia(int i)=>4+i*3;
        private static BossLegDef L(float side,float z,float phase)=>new BossLegDef{Hip=new Vector3(side*.86f,-.05f,z),Home=new Vector3(side*2.2f,0,z*1.18f+.12f*Math.Sign(z)),
            Pole=new Vector3(side*.45f,1,0),Upper=1.45f,Lower=1.75f,Phase=phase,Side=side,Ankle=.08f};
        public static readonly BossGaitDef Gait=new BossGaitDef
        {
            // Front, middle, rear on each side; the tripods (L1 R2 L3) and (R1 L2 R3).
            Legs=new[]{L(-1,1.05f,0),L(1,1.05f,.5f),L(-1,0,.5f),L(1,0,0),L(-1,-1.05f,0),L(1,-1.05f,.5f)},
            WalkSpeed=1.6f,RunSpeed=4.8f,WalkStride=1.7f,RunStride=3.3f,WalkDuty=.6f,RunDuty=.46f,WalkLift=.3f,RunLift=.46f,
            MinFrequency=.55f,MinSwing=.16f,MaxSwing=.6f,SettleDistance=.24f,SettleSwing=.34f,ReachMargin=.22f,
            BodyHeight=1.25f,Bob=.035f,RunBob=.06f,Sway=.03f,Roll=.035f,Twist=.02f,LandDip=.12f,SupportFrequency=2.6f,SupportDamping=.6f,
            LiftPeak=.45f,ToeOff=0,HeelStrike=0,FloorFollow=.8f
        };
        public static readonly BossChainDef TailDef=new BossChainDef{Count=16,Length=.28f,Rest=Vector3.Normalize(new Vector3(0,-.3f,-1)),Pole=Vector3.UnitY,
            Stiffness=3.2f,TipStiffness=.15f,Damping=1.1f,Radius=.07f,Friction=5};
        public readonly BossPose Pose=new BossPose(Count);
        public readonly BossRootFollow Follow=new BossRootFollow{MaxTurnRate=2.2f,MaxTurnAccel=8f,TurnHalfLife=.13f,MaxBank=.08f,BankScale=.6f,MaxPitch=.09f,PitchScale=.5f,LeanFrequency=1.6f,LeanDamping=.6f};
        public readonly BossGait Legs=new BossGait(Gait);
        public readonly BossChain Tail=new BossChain(TailDef);
        public readonly BossLookAt Look=new BossLookAt{MaxYaw=1.4f,MaxUp=.5f,MaxDown=.5f,HalfLife=.1f,MaxRate=5f};
        public readonly BossFlinch Flinch=new BossFlinch{PitchGain=.12f,BankGain=.12f,ShiftGain=.18f};
        public uint Seed=0xC4A1u;
        public void Update(double time,float dt,IBossGround ground,bool hasTarget,Vector3 target)
        {
            var f=Follow;if(!f.Started)return;float yaw=f.Yaw;var facing=BossMath.Yaw(yaw);
            Flinch.Sample(time,out float fp,out float fb,out var fs);var root=f.Position+BossMath.Rotate(facing,fs);
            if(f.Jumped){Legs.Reset(root,yaw,ground);}
            float hum=BossBreath.Sample(time,.6f,Seed);
            Legs.ExtraOffset=Vector3.UnitY*(.012f*hum);Legs.ExtraRotation=BossMath.AxisAngle(Vector3.UnitX,fp)*BossMath.AxisAngle(Vector3.UnitZ,-fb);
            Legs.Step(root,yaw,f.YawRate,f.Velocity,f.Body,dt,ground);
            var hull=Legs.BodyPosition;var r=Legs.BodyRotation;Pose.Set(Hull,hull,r);Pose.RootPosition=root;Pose.RootRotation=facing;
            var eye=hull+BossMath.Rotate(r,new Vector3(0,.18f,1.75f));Look.Step(eye,yaw,hasTarget,target,dt);
            Pose.Set(Head,eye,r*BossMath.AxisAngle(Vector3.UnitY,Look.Yaw+.05f*BossBreath.Idle(time,Seed,3,.5f))*BossMath.AxisAngle(Vector3.UnitX,Look.Pitch*.7f));
            for(int i=0;i<6;i++){Pose.Set(Coxa(i),Legs.Hip[i],r);Pose.Set(Femur(i),Legs.Hip[i],Legs.Upper[i]);Pose.Set(Tibia(i),Legs.Knee[i],Legs.Lower[i]);}
            Tail.Step(hull+BossMath.Rotate(r,new Vector3(0,.05f,-1.62f)),r,dt,ground);
        }
    }
}
