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
        /// .12 sprint lean: the daggers lowest and hardest, the shield and the machine gun upright behind their weight; the rifle
        /// leans into its high port, fire attacks the ground ahead, darkness stalks lowest.</summary>
        public static float Lean(string spec)
        {
            switch(spec)
            {
                case "daggers":return .1f;case "sword":return .07f;case "greatsword":return .14f;case "shield":return .03f;
                case "pistol":return .08f;case "rifle":return .1f;case "machinegun":return .02f;
                case "fire":return .16f;case "water":return .06f;case "lightning":return .12f;case "darkness":return .24f;case "leet":return .14f;
                default:return MeleeStance.MagicSpec(spec)?.04f:0;
            }
        }
        /// <summary>The body under a gun or magic sprint (m; downward only, so the planted feet still reach): a crouch, a heavy
        /// step's dip at the step rate, the share of the gait's stride bob held at its lowest (a glide), and a stride roll
        /// (rad) of the chest: the machine gun lumbers (dip and roll), water and darkness glide (darkness low), fire runs low.</summary>
        public static void Body(string spec,out float crouch,out float dip,out float glide,out float roll)
        {
            crouch=dip=glide=roll=0;
            switch(spec)
            {
                case "machinegun":dip=.03f;roll=.03f;break;
                case "fire":crouch=.02f;break;
                case "water":crouch=.01f;glide=.85f;break;
                case "darkness":crouch=.07f;glide=.8f;break;
                case "lightning":crouch=.01f;break;
            }
        }
        /// <summary>Lightning's crackle and L33t H4x's glitch (Hz): the twitch steps, the glitch's held frames and its bursts (s).</summary>
        public const float TwitchRate=18,GlitchRate=10,GlitchCycle=1.4f,GlitchBurst=.24f;
        /// <summary>The carry each weapon sprints with (the DEV fighters and the Siren keep their own locomotion).</summary>
        public const string Ninja="blade swept low behind",Pump="daggers pumping low",ShieldUp="shield up, shoulder-led",Shoulder="blade on the shoulder",
            Tucked="pistol tucked, arms pumping",HighPort="high port, leaning in",Hip="shoulder carry, lumbering",Embers="embers trailing, attacking",
            Glide="flowing glide",Crackle="crackling stride",Stalk="low stalking glide",Glitch="glitch stride",Trail="hands trailing back",None="none";
        public static string CarryOf(string spec)
        {
            switch(spec)
            {
                case "sword":return Ninja;case "daggers":return Pump;case "shield":return ShieldUp;case "greatsword":return Shoulder;
                case "pistol":return Tucked;case "rifle":return HighPort;case "machinegun":return Hip;
                case "fire":return Embers;case "water":return Glide;case "lightning":return Crackle;case "darkness":return Stalk;case "leet":return Glitch;
                default:return MeleeStance.MagicSpec(spec)?Trail:None;
            }
        }
        /// <summary>A signed hash in [-1, 1] (the crackle and glitch offsets: deterministic, the same on every client).</summary>
        public static float Hash(int n){uint x=unchecked((uint)n*2654435761u);x^=x>>15;x=unchecked(x*2246822519u);x^=x>>13;return (x&0xffff)/32767.5f-1;}

        // ---- first person (NativeViewSway: arena inputs only) ----
        /// <summary>
        /// The view weapon's sprint carry in the camera frame (NativeViewSway pose units: m down, rad of pitch with + the muzzle
        /// up, rad of roll) and the stride bob's extra gain, per weapon as the world rig carries it: the sword dropped low and
        /// tipped down and back, the daggers low with the hardest pump, the shield raised in front, the greatsword lifted onto
        /// the shoulder (its point tipped back over it), the pistol tucked muzzle-down with a hard pump, the rifle canted across
        /// the chest (high port), the machine gun heaved onto the shoulder with a heavy bob, and per school: fire low and pumping, water
        /// level with the least bob (a glide), lightning tight, darkness lowest and still, L33t H4x between them.
        /// </summary>
        public static void ViewCarry(string spec,out float drop,out float pitch,out float roll,out float bob)
        {
            switch(spec)
            {
                case "sword":drop=.045f;pitch=-.24f;roll=.1f;bob=.6f;return;
                case "daggers":drop=.035f;pitch=-.12f;roll=.04f;bob=1;return;
                case "shield":drop=-.012f;pitch=.04f;roll=-.06f;bob=.35f;return;
                case "greatsword":drop=-.015f;pitch=.2f;roll=.16f;bob=.5f;return;
                case "pistol":drop=.03f;pitch=-.22f;roll=.07f;bob=.9f;return;
                case "rifle":drop=.02f;pitch=.14f;roll=.34f;bob=.45f;return;
                case "machinegun":drop=.075f;pitch=-.12f;roll=.16f;bob=.75f;return;
                case "fire":drop=.06f;pitch=-.2f;roll=.06f;bob=.8f;return;
                case "water":drop=.045f;pitch=-.08f;roll=-.05f;bob=.25f;return;
                case "lightning":drop=.035f;pitch=-.12f;roll=.03f;bob=.65f;return;
                case "darkness":drop=.085f;pitch=-.24f;roll=0;bob=.3f;return;
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
    ///  - pistol: both arms pumping hard, the pistol tucked muzzle-down in the gun fist;
    ///  - rifle: a tactical high port across the chest leaning into the run, the rifle bouncing with each step;
    ///  - machine gun: heaved up onto the shoulder in both hands (its run carries it at the hip), a lumbering, dipping, rolling
    ///    stride;
    ///  - fire: an aggressive lean, both hands low and wide clawing hard past the hips like flung embers, the staff swept back;
    ///  - water: a fluid glide (the stride's bob held low), the hands out low and flowing in a slow wave;
    ///  - lightning: a tight, crackling stride, hands and head twitching in sharp steps;
    ///  - darkness: a low stalking glide, crouched and leaning, the hands back and still, the staff trailing low;
    ///  - L33t H4x: both arms flung straight back (the internet's own sprint) in held frames with glitch bursts (its card's
    ///    glitch style).
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
        /// <summary>
        /// The daggers' sprint blade lies (chest space; owner 2026-10-14: "while running/sprinting, the back arm wiggles a bit"):
        /// the run carry's mean reverse-grip lie of each fist, tipped back along the forearm (right .34 rad, left .3: about 6 cm
        /// over the guard at the lowest of a straight sprint). The fists once kept the live run carry's lie, which carries the
        /// run's per-stride wrist motion and, under the sprint lean, hung the .84 m blades 7-15 cm below the floor guard's plane
        /// each pump: MeleeCapture.KeepBladesOffFloor then flicked each wrist up by up to 12 degrees and let go every stride.
        /// </summary>
        public static readonly Quaternion DaggerRight=Weapon(new Vector3(-.09f,-.93f,-.36f),new Vector3(-.26f,-.33f,.91f)),
            DaggerLeft=Weapon(new Vector3(.58f,-.75f,-.33f),new Vector3(.75f,.32f,.58f));
        // The forward lean on the spine about the pelvis's own side axis (premultiplied, so the steady torso's counter-twist
        // never turns it into a roll), half of it given back at the neck so the gaze stays level.
        private static void LeanSpine(MeleeRig r,string spec,float w)
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
                    LeanSpine(r,p.Spec,w);
                    r.BraceWeapon(new Vector3(-.27f,-.36f+.02f*fR,-.15f+.04f*fR),Weapon(new Vector3(-.22f,.14f,-.96f),new Vector3(0,-1,0)),w,Vector3.Normalize(new Vector3(-1,-.2f,-.7f)),false);
                    r.Hand(true,new Vector3(.24f,-.15f+.03f*Math.Max(0,fL),.06f+.22f*fL),qL,w,poleL);break;
                case "daggers":
                    // Each reverse-grip fist pumps at its own steady blade lie (DaggerRight, DaggerLeft), not the live run carry's.
                    LeanSpine(r,p.Spec,w);
                    r.BraceWeapon(new Vector3(-.23f,-.18f+.05f*Math.Max(0,fR),.02f+.3f*fR),DaggerRight,w,poleR,false);
                    r.Hand(true,new Vector3(.23f,-.18f+.05f*Math.Max(0,fL),.02f+.3f*fL),DaggerLeft,w,poleL);break;
                case "shield":
                    // The shield shoulder (the right) leads: the chest turns toward it and the head keeps the facing.
                    LeanSpine(r,p.Spec,w);r.AddE(CapturedRig.Spine,0,.16f*w,0);r.AddE(CapturedRig.Chest,0,.12f*w,0);r.AddE(CapturedRig.Head,0,-.28f*w,0);
                    r.BraceWeapon(new Vector3(-.07f,.07f+.015f*fR,.33f),qR,w,poleR,false);
                    r.Hand(true,new Vector3(.25f,-.14f+.03f*Math.Max(0,fL),.05f+.22f*fL),qL,w,poleL);break;
                case "greatsword":
                    // Heavy strides: a deeper bob at the step rate under the shoulder carry.
                    LeanSpine(r,p.Spec,w);r.P[CapturedRig.Body]-=new Vector3(0,.022f*w*(.5f+.5f*(float)Math.Cos(2*r.Stride)),0);
                    // The support hand takes the hilt once, after the blade is cleared, at the carry's weight (a clearance that
                    // comes and goes never snaps it on).
                    r.BraceWeapon(new Vector3(-.15f,.15f+.01f*fR,.16f),Weapon(new Vector3(-.2f,.6f,-.78f),new Vector3(0,0,1)),w,poleR,false);
                    clear.ClearBlade(r,set,false,w);r.SupportHand(w);break;
                default:Carry=NativeSprintRules.None;break;
            }
        }
        /// <summary>A gun or magic route's sprint weight (GunMotion, MagicMotion: after the gait, before the pack carry and the
        /// gun hold or the magic layer), for Lean and the route's carry.</summary>
        public float Advance(MeleeRig r,Actor a,float time,float dt)
        {
            float w=Advance(NativeSprintRules.Shows(a,time),time,dt)*MeleeMath.Ease((r.Motion-.5f)/.5f);Carry=NativeSprintRules.None;return w>1e-4f?w:0;
        }
        /// <summary>The route's lean and body (NativeSprintRules.Body) at the weight Advance returned (after the pack carry,
        /// Core/NativeRangedLocomotion); `bob` the gait's stride bob this frame (MeleeGait.BodyBob, m) for a glide.</summary>
        public void Lean(MeleeRig r,string spec,float w,float bob)
        {
            Carry=w>1e-4f?NativeSprintRules.CarryOf(spec):NativeSprintRules.None;if(w<=1e-4f)return;LeanSpine(r,spec,w);
            // The gaze stays up the run: the neck gives back more of the lean (and half the route's own .12) than a melee sprint's.
            r.Premultiply(CapturedRig.Neck,MeleeMath.AxisAngle(MeleeMath.X,-(GazeBack*NativeSprintRules.Lean(spec)+.06f)*w));
            NativeSprintRules.Body(spec,out float crouch,out float dip,out float glide,out float roll);
            // The glide holds the body at the bob's trough (the bob's amplitude below its line), so it only ever lowers.
            float amplitude=Math.Abs(bob)>1e-6f?Math.Max(Math.Abs(bob),GlideDepth):GlideDepth,down=w*(crouch+dip*(.5f+.5f*(float)Math.Cos(2*r.Stride))+glide*(bob+amplitude));
            if(down>0)r.P[CapturedRig.Body]-=new Vector3(0,down,0);
            if(roll>0)r.Premultiply(CapturedRig.Chest,MeleeMath.AxisAngle(MeleeMath.Z,roll*w*(float)Math.Sin(r.Stride)));
        }
        /// <summary>The glide's depth: the gait's sprint bob amplitude (m, MeleeGait: .008 + .010 at a sprint).</summary>
        public const float GlideDepth=.018f;
        /// <summary>The extra share of a gun or magic sprint's lean the neck gives back (over LeanSpine's half).</summary>
        public const float GazeBack=.35f;
        private static readonly int[] ArmJoints={CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        /// <summary>The gun carry over the pose's arms after RangedPose.Arms (GunMotion.Sample), blended by the weight Advance returned.</summary>
        public void Gun(CapturedRig.Pose p,Actor a,float w,float stride)
        {
            if(w<=1e-4f||p==null)return;bool pistol=a.Weapon==WeaponId.Pistol,mg=a.Weapon==WeaponId.MachineGun;
            for(int i=0;i<6;i++)saved[i]=p.Rotation[ArmJoints[i]];
            float fR=(float)Math.Cos(stride),fL=-fR,sway=(float)Math.Sin(stride),step=(float)Math.Cos(2*stride);
            if(pistol)
            {
                // Both fists pump hard and close, elbows bent; the pistol rides the gun fist tucked muzzle-down, tipping with it.
                p.World();Quaternion left=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest])*p.WorldRotation[CapturedRig.LHand];
                CapturedRig.Arm(p,false,new Vector3(-.21f,-.15f+.07f*Math.Max(0,fR),.03f+.23f*fR),CapturedRig.Euler(1.05f+.3f*fR,.2f,-.1f),new Vector3(-1,-.6f,-.45f));
                CapturedRig.Arm(p,true,new Vector3(.21f,-.15f+.07f*Math.Max(0,fL),.03f+.23f*fL),left,new Vector3(1,-.6f,-.45f));
            }
            else
            {
                // The rifle's high port across the chest, bouncing with each step and rolling with the shoulders; the machine gun
                // heaved up onto the right shoulder, the barrel back over it, rocking heavily with the stride. The support hand on
                // the handguard.
                Vector3 target=mg?new Vector3(-.25f+.015f*sway,.12f+.03f*step,.14f):new Vector3(-.12f+.015f*sway,.03f+.025f*step,.22f);
                Quaternion aim=mg?CapturedRig.Euler(-2.3f+.08f*step,-.25f+.05f*sway,.15f):CapturedRig.Euler(-.88f+.05f*sway,.58f,.66f+.08f*sway);
                CapturedRig.Arm(p,false,target,aim,new Vector3(-1,-.7f,-.2f));p.World();
                Quaternion chestInv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);Vector3 reached=Vector3.Transform(p.WorldPosition[CapturedRig.RHand]-p.WorldPosition[CapturedRig.Chest],chestInv);
                CapturedRig.Arm(p,true,reached+Vector3.Transform(RangedPose.Support(a.Weapon),aim),aim,new Vector3(1,-.8f,-.25f));
            }
            for(int i=0;i<6;i++)p.Rotation[ArmJoints[i]]=Quaternion.Slerp(saved[i],p.Rotation[ArmJoints[i]],w);
            p.World();
        }
        // L33t H4x's held frame (the glitch: the carry updates GlitchRate times a second) and lightning's twitch steps.
        private int heldStep=int.MinValue;private Vector3 heldL,heldR;private float heldTilt;
        private static float Twitch(float time,int k){float u=time*NativeSprintRules.TwitchRate;int n=(int)Math.Floor(u);float f=MeleeMath.Ease((u-n)*3);return MeleeMath.Lerp(NativeSprintRules.Hash(n*7+k),NativeSprintRules.Hash((n+1)*7+k),f);}
        /// <summary>The magic carry (MagicMotion.Sample, after the magic layer, before the pose is written), per school (`spec`),
        /// the staff (the left hand) angled with it (StaffTilt for MagicPose.Staff).</summary>
        public void Magic(MeleeRig r,string spec,float time,float w)
        {
            StaffTilt=0;if(w<=1e-4f)return;float s=r.Stride,fR=(float)Math.Cos(s),fL=-fR,wave=(float)Math.Sin(.5f*s);
            Quaternion qR=r.ChainQuat(CapturedRig.RHand,CapturedRig.Chest),qL=r.ChainQuat(CapturedRig.LHand,CapturedRig.Chest);
            Vector3 L,R;float tilt;
            switch(spec)
            {
                case "fire":
                    // Attacking: both hands low and wide, clawing hard fore and aft past the hips with the stride like flung
                    // embers; the staff swept back.
                    L=new Vector3(.3f,-.31f+.05f*Math.Max(0,fL),-.12f-.17f*fR);R=new Vector3(-.3f,-.31f+.05f*Math.Max(0,fR),-.12f+.17f*fR);tilt=-.8f;break;
                case "water":
                    // Out low to the sides, flowing in a slow wave at half the stride (the left and right a half wave apart).
                    L=new Vector3(.36f,-.22f+.06f*wave,-.06f+.04f*(float)Math.Cos(.5f*s));R=new Vector3(-.36f,-.22f-.06f*wave,-.06f-.04f*(float)Math.Cos(.5f*s));tilt=-.4f+.06f*wave;break;
                case "lightning":
                    // Tight to the body, elbows bent, a short pump; the hands and staff crackle in sharp twitch steps.
                    L=new Vector3(.24f,-.13f,.06f+.07f*fL)+new Vector3(Twitch(time,1),Twitch(time,2),Twitch(time,3))*.035f;
                    R=new Vector3(-.24f,-.13f,.06f+.07f*fR)+new Vector3(Twitch(time,4),Twitch(time,5),Twitch(time,6))*.035f;
                    tilt=-.6f+.12f*Twitch(time,7);r.AddE(CapturedRig.Head,.03f*Twitch(time,8)*w,.06f*Twitch(time,9)*w,0);break;
                case "darkness":
                    // Crouched and stalking: the hands back and low, nearly still; the staff trailing low behind like a blade.
                    L=new Vector3(.2f,-.38f,-.32f-.02f*fR);R=new Vector3(-.22f,-.38f,-.3f+.02f*fR);tilt=-1.15f;break;
                case "leet":
                {
                    // The internet's own sprint: both arms flung straight back behind, high, in held frames with glitch bursts
                    // that jump the hands and the staff.
                    int step=(int)Math.Floor(time*NativeSprintRules.GlitchRate);
                    if(step!=heldStep)
                    {
                        heldStep=step;bool burst=MeleeMath.Frac(time/NativeSprintRules.GlitchCycle)<NativeSprintRules.GlitchBurst;float j=burst?.07f:.012f;
                        heldL=new Vector3(.22f,-.14f,-.42f-.03f*fR)+new Vector3(NativeSprintRules.Hash(step*5+1),NativeSprintRules.Hash(step*5+2),NativeSprintRules.Hash(step*5+3))*j;
                        heldR=new Vector3(-.22f,-.14f,-.42f+.03f*fR)+new Vector3(NativeSprintRules.Hash(step*5+4),NativeSprintRules.Hash(step*5+5),NativeSprintRules.Hash(step*5+6))*j;
                        heldTilt=-1+(burst?.35f*NativeSprintRules.Hash(step*5+7):0);
                    }
                    L=heldL;R=heldR;tilt=heldTilt;break;
                }
                default:
                    L=new Vector3(.27f,-.32f,-.2f-.04f*fR);R=new Vector3(-.27f,-.32f,-.2f+.04f*fR);tilt=-.9f;break;
            }
            r.Hand(true,L,qL,w,Vector3.Normalize(new Vector3(1,-.3f,.6f)));r.Hand(false,R,qR,w,Vector3.Normalize(new Vector3(-1,-.3f,.6f)));
            StaffTilt=tilt*w;
        }
    }
}
