using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The intro rig's pose (source frame, +x the rig's left): the baked entrance clip (tools/export_motion.mjs, 60 Hz) and,
    /// for the eleven weapon intros, a deterministic layer over it. Engine-free so the managed checks run the exact code the
    /// rig runs; allocation-free per frame (fixed scratch, main thread only). A weapon intro's clearance plan is built once,
    /// on its first use, from the clip itself.
    /// </summary>
    public static class NativeIntroMotion
    {
        /// <summary>True when every joint of `pose` is finite (a non-finite one must never reach a Transform).</summary>
        public static bool Finite(CapturedRig.Pose pose)
        {
            for(int i=0;i<pose.Position.Length;i++)
            {
                var p=pose.Position[i];var q=pose.Rotation[i];
                if(!MathUtil.Finite(p.X)||!MathUtil.Finite(p.Y)||!MathUtil.Finite(p.Z)||!MathUtil.Finite(q.X)||!MathUtil.Finite(q.Y)||!MathUtil.Finite(q.Z)||!MathUtil.Finite(q.W))return false;
            }
            return true;
        }
        /// <summary>
        /// The bake at `age` (clamped to the clip; a non-finite age samples its start): 22 joints x (position, quaternion) per
        /// frame, joints lerped/slerped between the two frames around `age`.
        /// </summary>
        public static void Sample(float[] values,int frames,float duration,float age,CapturedRig.Pose pose)
        {
            float u=age/duration;if(!(u>0))u=0;else if(u>1)u=1;float index=u*(frames-1);int a=Math.Min(frames-1,(int)index),b=Math.Min(frames-1,a+1);float t=index-a;
            for(int bone=0;bone<22;bone++)
            {
                int first=(a*22+bone)*7,last=(b*22+bone)*7;
                pose.Position[bone]=Vector3.Lerp(new Vector3(values[first],values[first+1],values[first+2]),new Vector3(values[last],values[last+1],values[last+2]),t);
                pose.Rotation[bone]=Nlerp(new Quaternion(values[first+3],values[first+4],values[first+5],values[first+6]),new Quaternion(values[last+3],values[last+4],values[last+5],values[last+6]),t);
            }
        }
        // ---- the weapon-intro layer ----
        // WEAPON_INTROS (weapon-intros.ts) in NativeIntroRules.Entrances order: the moment each weapon's hit lands.
        private static readonly string[] Weapons={"weapon-sword","weapon-daggers","weapon-shield","weapon-greatsword","weapon-pistol","weapon-rifle","weapon-machinegun","weapon-fire","weapon-water","weapon-lightning","weapon-darkness"};
        private static readonly float[] Hits={1.8f,2.12f,1.45f,1.8f,2.12f,1.8f,2.12f,1.8f,2.12f,1.45f,2.12f};
        private const int Sword=0,Daggers=1,Shield=2,Greatsword=3,Pistol=4,Rifle=5,Machinegun=6,Fire=7,Water=8,Lightning=9,Darkness=10;
        /// <summary>The weapon intro's index (Weapons) or -1.</summary>
        public static int WeaponIntro(string style){for(int i=0;i<Weapons.Length;i++)if(Weapons[i]==style)return i;return -1;}
        public static float Hit(string style){int i=WeaponIntro(style);return i<0?float.NaN:Hits[i];}
        /// <summary>
        /// The intro rig's pose at `age`. Every entrance but the eleven weapon intros is the bake as is. A weapon intro keeps
        /// the bake's choreography, timing, footwork and camera (the clip, NativeIntroRules.Duration and the name card are
        /// unchanged; the legs, hips and root are the bake's own, so planted feet never slide) and layers over its arms and
        /// upper body:
        /// 1. two-hand grips (Braced): the Deadeye support hand meets the pistol at the high ready and cups the shooting fist
        ///    through the press-out, the shot and its recoil; the Longwatch off hand takes the fore-end as the rifle comes up
        ///    (from 0.75 s) instead of the source's off-hand phrase, which sinks 14 cm into the chest until braceWeapon takes
        ///    over at the 1.8 s shot; Belt Rattle and the greatsword keep the bake's brace, so the support hand stays on the
        ///    grip whatever moves the weapon;
        /// 2. gun handling (Body, Gun): the Deadeye and Longwatch square the chest to the target and lean into the aim (both
        ///    arms reach the gun, the head stays on the sights), and the pistol and rifle shots ride back through the hand,
        ///    chest and head as a damped follow-through past the aim; the Deadeye's barrel-up hero pose settles with a little
        ///    overshoot;
        /// 3. clearance (Plan): a forearm, hand or held weapon that sinks into the torso or head is moved out by a correction
        ///    planned once over the whole clip and smoothed in time, so no limb snaps around the body; it is zero while that
        ///    hand anchors a baked effect (Anchored), so muzzles, blade tips and casting hands stay where the stage's voxel
        ///    effects were baked. At a shot, and wherever its effect is fully anchored, the weapon hand is where the bake has it.
        /// Every layer is a continuous function of `age`, and every arm is re-solved with the bake's own elbow plane
        /// (CapturedRig.Arm reproduces the bake's arms to 0.1 degree).
        /// </summary>
        public static void Pose(string style,float[] values,int frames,float duration,float age,CapturedRig.Pose pose)
        {
            if(!MathUtil.Finite(age))age=0;Sample(values,frames,duration,age,pose);
            int w=WeaponIntro(style);if(w<0||frames<2||!(duration>0))return;
            var plan=PlanFor(w,values,frames,duration);int n=plan.Right.Length;
            float u=age/duration;if(!(u>0))u=0;else if(u>1)u=1;float x=u*(n-1);int a=Math.Min(n-1,(int)x),b=Math.Min(n-1,a+1);float t=x-a;
            Layer(w,plan,age,pose,Vector3.Lerp(plan.Right[a],plan.Right[b],t),Vector3.Lerp(plan.Left[a],plan.Left[b],t));
        }
        private static float Smoothstep(float a,float b,float x){float t=(x-a)/(b-a);t=t<0?0:t>1?1:t;return t*t*(3-2*t);}
        // An effect window with .3 s ramps, so a planned correction fades out and back in without a jolt.
        private static float Window(float age,float from,float to)=>Smoothstep(from-.33f,from-.03f,age)*(1-Smoothstep(to+.03f,to+.33f,age));
        /// <summary>
        /// How strongly the support hand is held on its weapon at `age` (0..1): braceWeapon's greatsword and Belt Rattle
        /// throughout, the rifle once its off hand takes the fore-end (from 0.75 s), and the pistol's cupped grip from the high
        /// ready through the shot until the gun comes back for the hero pose.
        /// </summary>
        public static float Braced(int w,float age)=>w==Greatsword||w==Machinegun?1:w==Rifle?Smoothstep(.75f,1.3f,age):w==Pistol?Smoothstep(Hits[w]-.5f,Hits[w]-.28f,age)*(1-Smoothstep(Hits[w]+.42f,Hits[w]+.68f,age)):0;
        // The two-hand aims, squared up and leaning in: the Deadeye from the press-out until the recovery, Longwatch while
        // its shot is lined up (the sight beam, from 1.2 s) through the shot.
        private static float Aim(int w,float age)=>w==Pistol?Smoothstep(Hits[w]-.45f,Hits[w]-.15f,age)*(1-Smoothstep(Hits[w]+.45f,Hits[w]+.8f,age)):w==Rifle?Smoothstep(.8f,1.2f,age)*(1-Smoothstep(Hits[w]+.15f,Hits[w]+.5f,age)):0;
        // The shot's follow-through (peak 1 at ~.07 s, a dip past the aim at ~.27 s, settled by ~.6 s); C1 at the shot.
        private static float Follow(float tau)=>tau<=0?0:1.8f*(1-(float)Math.Exp(-60*tau))*(float)Math.Exp(-7*tau)*(float)Math.Sin(17*tau);
        // The Deadeye hero settle: the barrel-up pose arrives with a small, quickly damped wobble; C1 at its start.
        private static float Settle(float age){float s=age-3.12f;return s<=0?0:(1-(float)Math.Exp(-30*s))*(float)Math.Exp(-5*s)*(float)Math.Sin(10*s);}
        // The Deadeye support hand: palm wrapped round the shooting fist's off side, a little under and behind it.
        private static readonly Vector3 CuppedGrip=new Vector3(.074f,-.042f,-.018f);
        public static Vector3 PistolGrip=>CuppedGrip;
        private static void Layer(int w,Plan plan,float age,CapturedRig.Pose pose,Vector3 fixRight,Vector3 fixLeft)
        {
            pose.World();
            Vector3 r0=pose.WorldPosition[CapturedRig.RHand],l0=pose.WorldPosition[CapturedRig.LHand];Quaternion rq0=pose.WorldRotation[CapturedRig.RHand],lq0=pose.WorldRotation[CapturedRig.LHand];
            // The support grip in the weapon hand's frame: the plan's (the cupped pistol grip; the rifle's fore-end until the
            // bake braces it itself at its 1.8 s shot, from where the two agree) or the bake's own brace at this moment.
            Vector3 gripP;Quaternion gripQ;
            if(plan.FixedGrip&&!(w==Rifle&&age>=Hits[w])){gripP=plan.GripP;gripQ=plan.GripQ;}else{var inv=Quaternion.Inverse(rq0);gripP=Vector3.Transform(l0-r0,inv);gripQ=Quaternion.Normalize(inv*lq0);}
            Body(w,age,pose,r0);
            Vector3 right=r0+fixRight;Quaternion rightQ=rq0;Gun(w,age,rq0,ref right,ref rightQ);
            Reach(pose,false,right,rightQ);
            Vector3 left=l0+fixLeft;Quaternion leftQ=lq0;float g=Braced(w,age);
            if(g>0)
            {
                pose.World();var r=pose.WorldPosition[CapturedRig.RHand];var rq=pose.WorldRotation[CapturedRig.RHand];
                left=Vector3.Lerp(left,r+Vector3.Transform(gripP,rq),g);leftQ=Quaternion.Normalize(Quaternion.Slerp(leftQ,Quaternion.Normalize(rq*gripQ),g));
            }
            Reach(pose,true,left,leftQ);
            pose.World();supportMiss=left-pose.WorldPosition[CapturedRig.LHand];
        }
        /// <summary>
        /// Upper-body gun handling over the bake (rotations of the spine, chest and head only; the arms are re-solved to their
        /// hands afterwards, so the hands stay where they are aimed): the Deadeye and Longwatch square the chest toward the
        /// shooting hand and lean into the aim, the head counter-turning to stay on the sights; the pistol and rifle shots
        /// twist and rock the chest back and nod the head through the follow-through.
        /// </summary>
        private static void Body(int w,float age,CapturedRig.Pose pose,Vector3 hand)
        {
            if(w!=Pistol&&w!=Rifle)return;float a=Aim(w,age),f=Follow(age-Hits[w])*(w==Rifle?1.3f:1);if(a<=0&&f==0)return;
            float yaw=0;
            if(a>0){var h=Vector3.Transform(hand-pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest]));yaw=MathUtil.Clamp(.65f*(float)Math.Atan2(h.X,h.Z),-.3f,.3f)*a;}
            pose.Rotation[CapturedRig.Spine]=pose.Rotation[CapturedRig.Spine]*CapturedRig.Euler(.07f*a,.35f*yaw,0);
            pose.Rotation[CapturedRig.Chest]=pose.Rotation[CapturedRig.Chest]*CapturedRig.Euler(.1f*a-.03f*f,.65f*yaw+.06f*f,0);
            pose.Rotation[CapturedRig.Head]=pose.Rotation[CapturedRig.Head]*CapturedRig.Euler(-.14f*a-.04f*f,-.8f*yaw,0);
        }
        /// <summary>The weapon hand's own gun handling: the follow-through rides the hand back and up along the gun and flips
        /// the muzzle a little further before it returns past the aim; the Deadeye hero pose settles.</summary>
        private static void Gun(int w,float age,Quaternion baked,ref Vector3 right,ref Quaternion rightQ)
        {
            if(w!=Pistol&&w!=Rifle)return;float g=w==Rifle?1.3f:1,f=Follow(age-Hits[w])*g,s=w==Pistol?Settle(age):0;if(f==0&&s==0)return;
            right+=Vector3.Transform(new Vector3(0,.012f*f,-.03f*f),baked);
            rightQ=Quaternion.Normalize(rightQ*CapturedRig.Euler(-.12f*f+.06f*s,0,.025f*s));
        }
        // The hand whose weapon emits a baked stage effect (muzzle flashes, sight beam, smoke and glints; blade trails,
        // motes and floor sparks; the casting hand), and how strongly it is held where it was baked at `age` (1 while an
        // effect is emitted from it).
        private static bool AnchorLeft(int w)=>w==Lightning;
        /// <summary>How strongly hand `left` of weapon intro `style` is held where it was baked at `age` (1 while it anchors a
        /// baked stage effect; 0 for the free hand and for every other entrance).</summary>
        public static float Held(string style,float age,bool left){int w=WeaponIntro(style);return w<0||left!=AnchorLeft(w)?0:Anchored(w,age);}
        // Guns hold the muzzle fully round the shot (the flash cone, tracer and impact spawn on it) and mostly through the
        // trailing smoke; blade trails, motes, floor sparks and casting are looser (their voxels spread anyway).
        private static float Anchored(int w,float age)
        {
            float h=Hits[w];
            switch(w)
            {
                case Pistol:return Math.Max(Math.Max(.5f*Window(age,.5f,1.05f),Window(age,h-.3f,h+.12f)),.6f*Window(age,h-.3f,h+1.1f));
                case Rifle:return Math.Max(Window(age,h-.3f,h+.12f),.6f*Window(age,1.2f,h+1.1f));
                case Machinegun:return Math.Max(Window(age,h-.3f,h+.3f),.6f*Window(age,h-.3f,h+1.6f));
                case Sword:return Math.Max(.7f*Window(age,h-.14f,h+.3f),.4f*Window(age,h-.5f,h-.1f));
                case Daggers:return .6f*Window(age,.9f,h+.28f);
                // The two-hand drag on the walk-in (its tip scrapes the floor sparks; the left forearm lies across the belly as
                // in the source), the wind-up and the slam are held as baked until the blade is hoisted.
                case Greatsword:return 1-Smoothstep(h+.3f,h+.6f,age);
                case Fire:return Math.Max(Window(age,h-.06f,h+.06f),.5f*Window(age,.4f,h));
                case Lightning:return .6f*Window(age,h+.05f,h+1.2f);
                case Darkness:return Window(age,h-.05f,h+.05f);
                default:return 0;
            }
        }
        // ---- the clearance plan ----
        private sealed class Plan{public float[] Values;public int Frames;public float Duration;public Vector3[] Right,Left;public bool FixedGrip;public Vector3 GripP;public Quaternion GripQ;}
        private static readonly Plan[] plans=new Plan[Weapons.Length];
        private static readonly CapturedRig.Pose planPose=new CapturedRig.Pose();
        // How far the last Layer's support wrist fell short of its target (out of reach), for the plan.
        private static Vector3 supportMiss;
        private const int Passes=6;private const float MaxPush=.24f;
        // The plan runs at 30 Hz (every other baked frame); a Gaussian of sigma 1.5 steps (50 ms) over +-5 steps smooths it.
        private static readonly float[] Kernel=MakeKernel();
        private static float[] MakeKernel(){var k=new float[11];float sum=0;for(int i=0;i<11;i++){k[i]=(float)Math.Exp(-(i-5)*(i-5)/4.5);sum+=k[i];}for(int i=0;i<11;i++)k[i]/=sum;return k;}
        /// <summary>Builds the clearance plan of every weapon intro clip up front (NativeSourceMotion calls it as it loads the
        /// clips), so no entrance pays for it on its first frame.</summary>
        public static void Prepare(string style,float[] values,int frames,float duration){int w=WeaponIntro(style);if(w>=0&&frames>=2&&duration>0&&values!=null&&values.Length>=frames*154)PlanFor(w,values,frames,duration);}
        /// <summary>
        /// The weapon intro's clearance plan for this clip (built once, then reused): at 30 Hz over the clip, how far each
        /// wrist moves to keep its forearm, hand and weapon out of the torso and head. Each pass poses every step with the
        /// corrections so far, adds the push out of the body (a braced support limb pushes its weapon hand, which carries
        /// both), smooths the corrections in time and zeroes them where the hand anchors a baked effect; six passes settle it
        /// (about 15 ms per clip). The grips the layer holds (the cupped
        /// pistol grip, the rifle's fore-end from its 1.9 s brace) are read here.
        /// </summary>
        private static Plan PlanFor(int w,float[] values,int frames,float duration)
        {
            var plan=plans[w];if(plan!=null&&plan.Values==values&&plan.Frames==frames&&plan.Duration==duration)return plan;
            int steps=Math.Max(2,(frames+1)/2);plan=new Plan{Values=values,Frames=frames,Duration=duration,Right=new Vector3[steps],Left=new Vector3[steps]};
            if(w==Pistol||w==Rifle)
            {
                Sample(values,frames,duration,w==Pistol?Hits[w]:1.9f,planPose);planPose.World();var gi=Quaternion.Inverse(planPose.WorldRotation[CapturedRig.RHand]);
                plan.FixedGrip=true;plan.GripQ=Quaternion.Normalize(gi*planPose.WorldRotation[CapturedRig.LHand]);
                plan.GripP=w==Pistol?CuppedGrip:Vector3.Transform(planPose.WorldPosition[CapturedRig.LHand]-planPose.WorldPosition[CapturedRig.RHand],gi);
            }
            var rawRight=new Vector3[steps];var rawLeft=new Vector3[steps];bool left=AnchorLeft(w);
            for(int pass=0;pass<Passes;pass++)
            {
                for(int f=0;f<steps;f++)
                {
                    float age=f*duration/(steps-1);Sample(values,frames,duration,age,planPose);Layer(w,plan,age,planPose,plan.Right[f],plan.Left[f]);
                    float g=Braced(w,age);Inverses(planPose);Push(w,planPose,false,out var pushRight);Push(w,planPose,true,out var pushLeft);
                    // A braced weapon hand never moves where its support hand cannot follow: it comes back by what the support missed.
                    rawRight[f]=plan.Right[f]+.9f*(pushRight+g*pushLeft)-g*supportMiss;rawLeft[f]=plan.Left[f]+.9f*(1-g)*pushLeft;
                }
                Blur(rawRight,plan.Right);Blur(rawLeft,plan.Left);
                for(int f=0;f<steps;f++)
                {
                    float age=f*duration/(steps-1),held=Anchored(w,age);
                    plan.Right[f]=Clamp(plan.Right[f]*(1-(left?0:held)));plan.Left[f]=Clamp(plan.Left[f]*(1-(left?held:0)));
                }
            }
            plans[w]=plan;return plan;
        }
        private static void Blur(Vector3[] from,Vector3[] into)
        {
            int n=from.Length;
            for(int i=0;i<n;i++){var sum=Vector3.Zero;for(int k=0;k<Kernel.Length;k++){int j=i+k-5;j=j<0?0:j>=n?n-1:j;sum+=from[j]*Kernel[k];}into[i]=sum;}
        }
        private static Vector3 Clamp(Vector3 v){float l=v.Length();return l>MaxPush?v*(MaxPush/l):v;}
        // The push that clears limb `left`'s deepest probe out of the body (world), if any sinks in beyond contact
        // (`pose` world and Inverses current).
        private static bool Push(int w,CapturedRig.Pose pose,bool left,out Vector3 push)
        {
            push=Vector3.Zero;int n=Limb(Weapons[w],left,probes);float worst=.008f;
            for(int i=0;i<n;i++)
            {
                // A forearm probe moves about as far as its share of the forearm when the wrist moves, so the wrist goes further.
                var p=probes[i];var c=pose.WorldPosition[p.Bone]+Vector3.Transform(p.Offset,pose.WorldRotation[p.Bone]);float d=Exit(pose,c,p.Radius,out var dir);
                float gain=p.Bone==CapturedRig.LElbow||p.Bone==CapturedRig.RElbow?Math.Min(2.2f,.25f/-p.Offset.Y):1;
                if(d>worst){worst=d;push=dir*((d-.004f)*gain);}
            }
            return push!=Vector3.Zero;
        }
        /// <summary>
        /// Re-solves arm `left` so its wrist reaches `target` with rotation `rotation` (both in the rig frame), keeping the
        /// elbow on its present side (CapturedRig.Arm works in the chest frame).
        /// </summary>
        internal static void Reach(CapturedRig.Pose pose,bool left,Vector3 target,Quaternion rotation)
        {
            pose.World();var chest=Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest]);var at=pose.WorldPosition[CapturedRig.Chest];
            int arm=left?CapturedRig.LArm:CapturedRig.RArm,hand=left?CapturedRig.LHand:CapturedRig.RHand;
            var shoulder=pose.WorldPosition[arm];var elbow=pose.WorldPosition[arm+1];var wrist=pose.WorldPosition[hand];
            var line=wrist-shoulder;float len=line.LengthSquared();var off=elbow-shoulder;if(len>1e-8f)off-=line*(Vector3.Dot(off,line)/len);
            var pole=Vector3.Transform(off,chest);
            CapturedRig.Arm(pose,left,Vector3.Transform(target-at,chest),Quaternion.Normalize(chest*rotation),pole);
        }
        // ---- body clearance: forearms, hands and the held weapon against the torso and head (CapturedRig.Boxes) ----
        internal struct Probe{public int Bone;public Vector3 Offset;public float Radius;}
        /// <summary>The weapon held in a hand, as probes along its length in the wrist frame (the bank exports intro weapons
        /// with the grip on the wrist, the barrel or blade along +z).</summary>
        private static int WeaponProbes(string style,bool left,Probe[] into,int n)
        {
            float from,to,r;switch(style)
            {
                case "weapon-pistol":if(left)return n;from=.12f;to=.46f;r=.04f;break;
                case "weapon-rifle":if(left)return n;from=.15f;to=1.15f;r=.045f;break;
                case "weapon-machinegun":if(left)return n;from=.15f;to=.88f;r=.06f;break;
                case "weapon-sword":if(left)return n;from=.2f;to=1.25f;r=.03f;break;
                case "weapon-greatsword":if(left)return n;from=.2f;to=1.62f;r=.04f;break;
                case "weapon-daggers":from=.15f;to=.8f;r=.03f;break;
                case "weapon-shield":if(left)return n;into[n++]=new Probe{Bone=left?CapturedRig.LHand:CapturedRig.RHand,Offset=new Vector3(0,-.02f,.2f),Radius=.16f};return n;
                case "weapon-fire":case "weapon-water":case "weapon-lightning":case "weapon-darkness":if(!left)return n;from=-.34f;to=.86f;r=.035f;break;
                default:return n;
            }
            int bone=left?CapturedRig.LHand:CapturedRig.RHand;for(int i=0;i<6;i++)into[n++]=new Probe{Bone=bone,Offset=new Vector3(0,0,from+(to-from)*i/5f),Radius=r};return n;
        }
        private static readonly Probe[] probes=new Probe[32];
        /// <summary>
        /// How deep (metres) the deepest forearm, hand or weapon probe sinks into the torso or head boxes for `style` at
        /// `pose` (the world pose is recomputed); `what` names it. Contact reads 0. `limb` 0 or 1 measures only the left or
        /// the right arm.
        /// </summary>
        public static float Clearance(string style,CapturedRig.Pose pose,out string what,int limb=-1)
        {
            pose.World();Inverses(pose);what="";float worst=0;
            for(int side=0;side<2;side++)
            {
                if(limb>=0&&side!=limb)continue;
                bool left=side==0;int n=Limb(style,left,probes);
                for(int i=0;i<n;i++)
                {
                    var p=probes[i];var c=pose.WorldPosition[p.Bone]+Vector3.Transform(p.Offset,pose.WorldRotation[p.Bone]);float d=Inside(pose,c,p.Radius);
                    if(d>worst){worst=d;what=(left?"left ":"right ")+(p.Bone==CapturedRig.LElbow||p.Bone==CapturedRig.RElbow?"forearm":p.Offset.Z>.1f||p.Offset.Z<-.1f?"weapon":"hand");}
                }
            }
            return worst;
        }
        private static int Limb(string style,bool left,Probe[] into)
        {
            int elbow=left?CapturedRig.LElbow:CapturedRig.RElbow,hand=left?CapturedRig.LHand:CapturedRig.RHand,n=0;
            into[n++]=new Probe{Bone=elbow,Offset=new Vector3(0,-.11f,0),Radius=.035f};into[n++]=new Probe{Bone=elbow,Offset=new Vector3(0,-.19f,0),Radius=.035f};
            into[n++]=new Probe{Bone=hand,Offset=new Vector3(0,-.05f,0),Radius=.03f};
            return WeaponProbes(style,left,into,n);
        }
        // The torso and head boxes (pelvis, spine, chest, neck, head) and, per pose, their joints' inverse rotations.
        private static readonly int[] Torso=TorsoBoxes();private static readonly Quaternion[] inverse=new Quaternion[CapturedRig.Head+1];
        private static int[] TorsoBoxes(){int n=0;foreach(var b in CapturedRig.Boxes)if(b.Bone>=CapturedRig.Pelvis&&b.Bone<=CapturedRig.Head)n++;var into=new int[n];n=0;for(int i=0;i<CapturedRig.Boxes.Length;i++){var b=CapturedRig.Boxes[i];if(b.Bone>=CapturedRig.Pelvis&&b.Bone<=CapturedRig.Head)into[n++]=i;}return into;}
        private static void Inverses(CapturedRig.Pose pose){for(int b=CapturedRig.Pelvis;b<=CapturedRig.Head;b++)inverse[b]=Quaternion.Inverse(pose.WorldRotation[b]);}
        // Depth of a sphere inside the torso/head boxes (Inverses must be current).
        private static float Inside(CapturedRig.Pose pose,Vector3 c,float r)
        {
            float worst=0;var boxes=CapturedRig.Boxes;
            for(int i=0;i<Torso.Length;i++)
            {
                var b=boxes[Torso[i]];var l=Vector3.Transform(c-pose.WorldPosition[b.Bone],inverse[b.Bone])-b.Position;
                float ex=b.Size.X*.5f+r-Math.Abs(l.X),ey=b.Size.Y*.5f+r-Math.Abs(l.Y),ez=b.Size.Z*.5f+r-Math.Abs(l.Z),d=Math.Min(ex,Math.Min(ey,ez));
                if(d>worst)worst=d;
            }
            return worst;
        }
        // How far a sphere sinks into the body and the way out (world, unit; Inverses must be current). A limb pressed into the torso leaves through
        // the front (the rig faces +z), through the back when it is well behind the middle, or sideways when it only grazes
        // a flank, never up or down (which only slides it into the next box); the exits are blended by inverse cost so the
        // direction turns smoothly as the limb moves, and boxes add up weighted by their depth.
        private static float Exit(CapturedRig.Pose pose,Vector3 c,float r,out Vector3 dir)
        {
            float worst=0;var sum=Vector3.Zero;var boxes=CapturedRig.Boxes;
            for(int i=0;i<Torso.Length;i++)
            {
                var b=boxes[Torso[i]];var q=pose.WorldRotation[b.Bone];
                var l=Vector3.Transform(c-pose.WorldPosition[b.Bone],inverse[b.Bone])-b.Position;
                float hx=b.Size.X*.5f+r,hy=b.Size.Y*.5f+r,hz=b.Size.Z*.5f+r;if(hx-Math.Abs(l.X)<=0||hy-Math.Abs(l.Y)<=0||hz-Math.Abs(l.Z)<=0)continue;
                float front=hz-l.Z,back=hz+l.Z,side=hx-Math.Abs(l.X);
                float wf=Cost(front),wb=Cost(b.Bone>=CapturedRig.Neck?back:2*back),ws=Cost(1.5f*side),total=wf+wb+ws,amount=(wf*front+wb*back+ws*side)/total;
                var n=new Vector3(ws*Math.Sign(l.X),0,wf-wb);if(n.LengthSquared()<1e-12f)n=new Vector3(0,0,1);
                sum+=Vector3.Transform(Vector3.Normalize(n),q)*amount;worst=Math.Max(worst,amount);
            }
            dir=sum.LengthSquared()>1e-12f?Vector3.Normalize(sum):Vector3.Zero;return worst;
        }
        private static float Cost(float distance){float u=Math.Max(.002f,distance);u*=u;return 1/(u*u);}
        // Shortest-arc normalised lerp: one 1/60 s frame apart the joints differ by a few degrees, where it equals slerp, and it
        // can never divide by a vanishing sine.
        private static Quaternion Nlerp(Quaternion a,Quaternion b,float t)
        {
            if(Quaternion.Dot(a,b)<0)b=new Quaternion(-b.X,-b.Y,-b.Z,-b.W);var q=new Quaternion(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t,a.W+(b.W-a.W)*t);
            float l=q.Length();return l>1e-6f?q*(1/l):a;
        }
    }
}
