using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The sprint animation and its feedback (owner 2026-10-12: "Also, add a sprinting animation for all weapons. (Different
    /// on weapon of course)" and "And a sprint effect to know when we're sprinting"): when a pilot sprints, how far into the
    /// sprint carry it is, and the per-weapon carries, lean, first-person kick and effect timing. Sprint is read from the
    /// replicated Actor.Sprinting flag (the authority sets it; host, guests, killcam and spectators all have it) while grounded
    /// and travelling. Presentation only: never read by authority; sprint speed, stamina and input are untouched.
    /// </summary>
    public static class NativeSprintRules
    {
        /// <summary>The blend (s): a ramp in or out over .15 s, eased at both ends (Shape), so the carry never pops.</summary>
        public const float Blend=.15f;
        /// <summary>The least horizontal speed that shows a sprint (a flag held at a standstill shows none).</summary>
        public const float MinSpeed=4;
        public static bool Shows(bool flag,bool grounded,float speed)=>flag&&grounded&&speed>MinSpeed;
        /// <summary>The actions that drop the sprint carry and its first-person feedback: a swing or cast and its recovery, a
        /// block, aim, reload or shot, a roll, stun, juggle or knockdown, a special, or death.</summary>
        public static bool Busy(Actor a,float time)=>a.Hp<=0||a.Rolling(time)||a.StunUntil>time||a.JuggleUntil>time||a.ReloadUntil>time||time<a.BlockUntil||time-a.LastShot<.3f||a.Aiming
            ||time-a.AbilityAt<.6f||time-a.AttackAt<.6f||a.Attacking(time)||a.CommitAt>-90&&time<a.CommitUntil||NativeSpecialPose.Active(a,time)||NativeSpecialPose.Down(a,time);
        /// <summary>Whether a presented pilot shows its sprint now (the replicated flag, grounded, travelling, not busy).</summary>
        public static bool Shows(Actor a,float time)=>a!=null&&!Busy(a,time)&&Shows(a.Sprinting,a.Grounded,(float)Math.Sqrt(a.Velocity.x*a.Velocity.x+a.Velocity.z*a.Velocity.z));
        /// <summary>The blend's linear ramp toward on (1) or off (0) after `dt` seconds.</summary>
        public static float Step(float ramp,bool on,float dt){float d=Math.Max(0,dt)/Blend;return on?Math.Min(1,ramp+d):Math.Max(0,ramp-d);}
        /// <summary>The weight shown for a ramp: smoothstep, so the carry starts and settles at rest (a reversal stays continuous).</summary>
        public static float Shape(float ramp){float x=MeleeMath.Clamp(ramp,0,1);return x*x*(3-2*x);}
        /// <summary>The extra forward lean (rad, on the spine; the neck keeps half the gaze) per weapon, over the routes' own
        /// .12 sprint lean: the daggers lowest and hardest, the shield and the machine gun upright behind their weight.</summary>
        public static float Lean(string spec)
        {
            switch(spec)
            {
                case "daggers":return .1f;case "sword":return .07f;case "greatsword":return .14f;case "shield":return .03f;
                case "pistol":return .04f;case "rifle":return .03f;case "machinegun":return .02f;default:return MeleeStance.MagicSpec(spec)?.04f:0;
            }
        }
        /// <summary>The carry each weapon sprints with (the DEV fighters and the Siren keep their own locomotion).</summary>
        public const string Ninja="blade swept low behind",Pump="daggers pumping low",ShieldUp="shield up, shoulder-led",Shoulder="blade on the shoulder",
            Port="pistol at port arms",HighPort="high port",LowReady="low ready",Trail="hands trailing back",None="none";
        public static string CarryOf(string spec)
        {
            switch(spec)
            {
                case "sword":return Ninja;case "daggers":return Pump;case "shield":return ShieldUp;case "greatsword":return Shoulder;
                case "pistol":return Port;case "rifle":return HighPort;case "machinegun":return LowReady;default:return MeleeStance.MagicSpec(spec)?Trail:None;
            }
        }

        // ---- first person (NativeViewSway: arena inputs only) ----
        /// <summary>
        /// The view weapon's sprint carry in the camera frame (NativeViewSway pose units: m down, rad of pitch with + the muzzle
        /// up, rad of roll) and the stride bob's extra gain, per weapon as the world rig carries it: the sword dropped low and
        /// tipped down and back, the daggers low with the hardest pump, the shield raised in front, the greatsword lifted onto
        /// the shoulder (its point tipped back over it), the pistol at port arms (muzzle up), the rifle canted across the chest
        /// (high port), the machine gun at a low ready, magic hands dropped back.
        /// </summary>
        public static void ViewCarry(string spec,out float drop,out float pitch,out float roll,out float bob)
        {
            switch(spec)
            {
                case "sword":drop=.045f;pitch=-.24f;roll=.1f;bob=.6f;return;
                case "daggers":drop=.035f;pitch=-.12f;roll=.04f;bob=1;return;
                case "shield":drop=-.012f;pitch=.04f;roll=-.06f;bob=.35f;return;
                case "greatsword":drop=-.015f;pitch=.2f;roll=.16f;bob=.5f;return;
                case "pistol":drop=.015f;pitch=.26f;roll=.05f;bob=.5f;return;
                case "rifle":drop=.02f;pitch=.12f;roll=.32f;bob=.45f;return;
                case "machinegun":drop=.05f;pitch=-.22f;roll=.1f;bob=.4f;return;
                case "siren":case "emperor":case "ansatsuken":drop=pitch=roll=bob=0;return;
                default:drop=.05f;pitch=-.14f;roll=0;bob=.6f;return;
            }
        }
        /// <summary>The camera's FOV kick (degrees, eased by the sprint weight; half with Reduced effects; none with camera
        /// shake off) and its vertical bob and roll at the stride (m, degrees; camera shake only).</summary>
        public const float FovKick=5,ReducedFovKick=2.5f,CameraBob=.012f,CameraRoll=.5f;
        public static float Fov(float weight,bool shake,bool reduced)=>!shake?0:MeleeMath.Clamp(weight,0,1)*(reduced?ReducedFovKick:FovKick);
        /// <summary>The camera's sprint bob (m, down at each footfall) and roll (degrees) at the view stride phase
        /// (NativeViewSway.Stride); none with camera shake off, half with Reduced effects.</summary>
        public static void CameraSway(float weight,float stride,bool shake,bool reduced,out float up,out float roll)
        {
            float k=shake?MeleeMath.Clamp(weight,0,1)*(reduced?.5f:1):0;up=-CameraBob*k*Math.Abs((float)Math.Cos(stride));roll=CameraRoll*k*(float)Math.Sin(stride);
        }

        // ---- the effect (Core/NativeSprintFx) ----
        /// <summary>Footfall spacing (m of travel per foot at 9.18 m/s, the gait's 3.7 Hz stride) and the audio's sprint step.</summary>
        public const float StepLength=1.24f;
    }

    /// <summary>
    /// The per-weapon sprint carry over a route's pose (Core/MeleeMotion, GunMotion, MagicMotion), on top of the set's captured
    /// run carry and the gait (the stride, its cadence and the planted feet stay the gait's, so nothing slides; the steady
    /// torso keeps its counter-twist):
    ///  - sword: the blade swept low behind the hip, the free arm pumping (a ninja run);
    ///  - daggers: both reverse-grip fists pumping hard and low, the deepest lean;
    ///  - shield: the shield up in front of the chest, the shield shoulder leading;
    ///  - greatsword: the blade on the right shoulder in both hands, heavier strides (a deeper bob);
    ///  - pistol: port arms, the muzzle up close to the chest (the free arm keeps its pump);
    ///  - rifle: high port across the chest, both hands; machine gun: a low ready, both hands;
    ///  - magic and L33t H4x: both hands trailing back, the staff angled back.
    /// The weight follows Actor.Sprinting (grounded, travelling) with a .15 s blend; swings, blocks, specials, casts, stuns,
    /// rolls, juggles and knockdowns drop it. Presentation only, no allocation per frame.
    /// </summary>
    public sealed class NativeSprintMotion
    {
        private float weight,last=-100;
        private readonly NativePackPose clear=new NativePackPose();
        private readonly Quaternion[] saved=new Quaternion[8];
        public float Weight=>NativeSprintRules.Shape(weight);
        /// <summary>The carry shown this frame (NativeSprintRules carries), or None.</summary>
        public string Carry{get;private set;}=NativeSprintRules.None;
        /// <summary>The staff's extra tilt back (rad) for the magic route's staff (MagicPose.Staff), set by Magic.</summary>
        public float StaffTilt{get;private set;}
        private float Advance(bool on,float time,float dt)
        {
            bool restart=last<-90||time<last-MeleeGait.REWIND||time-last>.35f;last=time;
            weight=restart?(on?1:0):NativeSprintRules.Step(weight,on,MeleeMath.Clamp(dt,0,.05f));
            return NativeSprintRules.Shape(weight);
        }
        private static float Hyp(float a,float b)=>(float)Math.Sqrt(a*a+b*b);
        private static Quaternion Weapon(Vector3 blade,Vector3 edgeHint)
        {
            blade=Vector3.Normalize(blade);Vector3 edge=edgeHint-blade*Vector3.Dot(edgeHint,blade);edge=edge.LengthSquared()<1e-8f?Vector3.UnitX:Vector3.Normalize(edge);
            return MeleeMath.Basis(edge,Vector3.Cross(blade,edge),blade);
        }
        // The forward lean on the spine about the pelvis's own side axis (premultiplied, so the steady torso's counter-twist
        // never turns it into a roll), half of it given back at the neck so the gaze stays level.
        private static void Lean(MeleeRig r,string spec,float w)
        {
            float k=NativeSprintRules.Lean(spec)*w;if(k<=0)return;
            r.Premultiply(CapturedRig.Spine,MeleeMath.AxisAngle(MeleeMath.X,k));r.Premultiply(CapturedRig.Neck,MeleeMath.AxisAngle(MeleeMath.X,-.5f*k));
        }
        /// <summary>The captured melee route (MeleeMotion.Sample, after the action layers and before the jump layer).</summary>
        public void Melee(MeleeRig r,MeleeView p,float time,float dt)
        {
            bool busy=p.Hp<=0||p.Juggle||p.Down||p.Rolling(time)||p.HitStunUntil>time||p.Action(time)||MeleeMotion.AttackPose(p,time).Active||p.Guarding||p.BlockTime>0;
            float w=Advance(!busy&&NativeSprintRules.Shows(p.Sprinting,p.Grounded,Hyp(p.Vel.X,p.Vel.Z)),time,dt)*MeleeMath.Ease((r.Motion-.5f)/.5f);
            Carry=NativeSprintRules.None;if(w<=1e-4f)return;
            var set=MeleeSet.For(p.Spec);Carry=NativeSprintRules.CarryOf(p.Spec);
            float fR=(float)Math.Cos(r.Stride),fL=-fR;
            Quaternion qR=r.ChainQuat(CapturedRig.RHand,CapturedRig.Chest),qL=r.ChainQuat(CapturedRig.LHand,CapturedRig.Chest);
            Vector3 poleR=Vector3.Normalize(new Vector3(-1,-.6f,-.35f)),poleL=Vector3.Normalize(new Vector3(1,-.8f,-.25f));
            switch(p.Spec)
            {
                case "sword":
                    Lean(r,p.Spec,w);
                    r.BraceWeapon(new Vector3(-.27f,-.36f+.02f*fR,-.15f+.04f*fR),Weapon(new Vector3(-.22f,.14f,-.96f),new Vector3(0,-1,0)),w,Vector3.Normalize(new Vector3(-1,-.2f,-.7f)),false);
                    r.Hand(true,new Vector3(.24f,-.15f+.03f*Math.Max(0,fL),.06f+.22f*fL),qL,w,poleL);break;
                case "daggers":
                    Lean(r,p.Spec,w);
                    r.BraceWeapon(new Vector3(-.23f,-.18f+.05f*Math.Max(0,fR),.02f+.3f*fR),qR,w,poleR,false);
                    r.Hand(true,new Vector3(.23f,-.18f+.05f*Math.Max(0,fL),.02f+.3f*fL),qL,w,poleL);break;
                case "shield":
                    // The shield shoulder (the right) leads: the chest turns toward it and the head keeps the facing.
                    Lean(r,p.Spec,w);r.AddE(CapturedRig.Spine,0,.16f*w,0);r.AddE(CapturedRig.Chest,0,.12f*w,0);r.AddE(CapturedRig.Head,0,-.28f*w,0);
                    r.BraceWeapon(new Vector3(-.07f,.07f+.015f*fR,.33f),qR,w,poleR,false);
                    r.Hand(true,new Vector3(.25f,-.14f+.03f*Math.Max(0,fL),.05f+.22f*fL),qL,w,poleL);break;
                case "greatsword":
                    // Heavy strides: a deeper bob at the step rate under the shoulder carry.
                    Lean(r,p.Spec,w);r.P[CapturedRig.Body]-=new Vector3(0,.022f*w*(.5f+.5f*(float)Math.Cos(2*r.Stride)),0);
                    // The support hand takes the hilt once, after the blade is cleared, at the carry's weight (a clearance that
                    // comes and goes never snaps it on).
                    r.BraceWeapon(new Vector3(-.15f,.15f+.01f*fR,.16f),Weapon(new Vector3(-.2f,.6f,-.78f),new Vector3(0,0,1)),w,poleR,false);
                    clear.ClearBlade(r,set,false,w);r.SupportHand(w);break;
                default:Carry=NativeSprintRules.None;break;
            }
        }
        /// <summary>A gun or magic route's lean (GunMotion, MagicMotion: after the gait, before the gun hold or the magic
        /// layer). Returns the weight for the route's carry.</summary>
        public float Lean(MeleeRig r,Actor a,string spec,float time,float dt)
        {
            float w=Advance(NativeSprintRules.Shows(a,time),time,dt)*MeleeMath.Ease((r.Motion-.5f)/.5f);
            Carry=w>1e-4f?NativeSprintRules.CarryOf(spec):NativeSprintRules.None;if(w>1e-4f)Lean(r,spec,w);
            return w;
        }
        private static readonly int[] ArmJoints={CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        /// <summary>The gun carry over the pose's arms after RangedPose.Arms (GunMotion.Sample), blended by the weight Lean returned.</summary>
        public void Gun(CapturedRig.Pose p,Actor a,float w,float stride)
        {
            if(w<=1e-4f||p==null)return;bool pistol=a.Weapon==WeaponId.Pistol,mg=a.Weapon==WeaponId.MachineGun;
            for(int i=0;i<6;i++)saved[i]=p.Rotation[ArmJoints[i]];
            float sway=(float)Math.Sin(stride)*.04f,bob=(float)Math.Cos(2*stride)*.015f;
            if(pistol)
            {
                // Port arms: the pistol up close to the chest, the muzzle up; the free arm keeps the gait's pump.
                CapturedRig.Arm(p,false,new Vector3(-.07f,.1f+bob,.17f),CapturedRig.Euler(-1.2f+sway,.12f,0),new Vector3(-1,-.7f,-.2f));
            }
            else
            {
                // High port (rifle) across the chest, or the machine gun's low ready; the support hand on the handguard.
                Vector3 target=mg?new Vector3(-.17f,-.09f+bob,.25f):new Vector3(-.12f,.02f+bob,.23f);Quaternion aim=mg?CapturedRig.Euler(-.3f+sway,.45f,.42f):CapturedRig.Euler(-.88f+sway,.58f,.66f);
                CapturedRig.Arm(p,false,target,aim,new Vector3(-1,-.7f,-.2f));p.World();
                Quaternion chestInv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);Vector3 reached=Vector3.Transform(p.WorldPosition[CapturedRig.RHand]-p.WorldPosition[CapturedRig.Chest],chestInv);
                CapturedRig.Arm(p,true,reached+Vector3.Transform(RangedPose.Support(a.Weapon),aim),aim,new Vector3(1,-.8f,-.25f));
            }
            for(int i=0;i<6;i++)p.Rotation[ArmJoints[i]]=Quaternion.Slerp(saved[i],p.Rotation[ArmJoints[i]],w);
            p.World();
        }
        /// <summary>The magic carry (MagicMotion.Sample, after the magic layer, before the pose is written): both hands trailing
        /// back, the staff angled back with them (StaffTilt for MagicPose.Staff).</summary>
        public void Magic(MeleeRig r,float w)
        {
            StaffTilt=0;if(w<=1e-4f)return;float fR=(float)Math.Cos(r.Stride);
            Quaternion qR=r.ChainQuat(CapturedRig.RHand,CapturedRig.Chest),qL=r.ChainQuat(CapturedRig.LHand,CapturedRig.Chest);
            r.Hand(true,new Vector3(.27f,-.32f,-.2f-.04f*fR),qL,w,Vector3.Normalize(new Vector3(1,-.3f,.6f)));r.Hand(false,new Vector3(-.27f,-.32f,-.2f+.04f*fR),qR,w,Vector3.Normalize(new Vector3(-1,-.3f,.6f)));
            StaffTilt=-.9f*w;
        }
    }
}
