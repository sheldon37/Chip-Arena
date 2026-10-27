using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The visual body's own animation (Core NativeVisualMotion; docs/BOZO-INTEGRATION.md): for a weapon whose set has moved over,
    // the model plays the animation pack's clips whole, retimed to the rules, instead of the game's pose retargeted. The clips
    // come from the bake's motion bank (Resources/ChipArenaVisualMotion.bytes, Core NativeVisualMotionClips: each pack clip
    // sampled on the body, its humanoid pose per frame), read once. Per body, the picked clip (or the locomotion blend by the
    // body's travel, at the stride's weights and rate) fades in and the rest fade out; their poses mix in muscle space and
    // the mix goes onto a second copy of the skeleton (the motion proxy) through its avatar. That pose then goes onto the body,
    // every bone, by the motion's weight (eased in and out, so a state the set does not cover hands back to the game's pose
    // smoothly). The weapon rides the model's right hand: the hand-to-weapon offset the game pose's hold
    // gives (the fist on the handle) is kept, and the rig's weapon hand bone moves with it, so the model, its trails, blood
    // and effects follow the model's blade; a two-handed weapon's support hand reaches its handle. Presentation only.
    public sealed partial class ArenaPresentation
    {
        private sealed class VisualMotion
        {
            public NativeVisualMotion.Set Set; public GameObject Proxy; public Animator Animator; public HumanPoseHandler Handler; public HumanPose Pose; public float[] Mix, UpperMix; public float Upper;
            public string[] Names; public NativeVisualMotionClips.Clip[] Clips; public float[] Weights, Times; public readonly Dictionary<string,int> Index=new Dictionary<string,int>();
            public int[] Loco; public readonly float[] LocoWeights=new float[NativeVisualStride.Clips],SpeedX=new float[NativeVisualStride.Clips],SpeedZ=new float[NativeVisualStride.Clips],LocoLength=new float[NativeVisualStride.Clips];
            public float Phase, Weight, LocoShare, Time=float.NaN, GaitSpeed; public Vector3 LastAt, Velocity;
            public Transform[] ProxyBones, BodyBones; public Transform BodyHips, ProxyHips, RightHand, LeftHand, LeftArm, LeftForearm, LeftFist, LeftIndex, LeftLittle;
            // The contact guard's bones on the body (ankles and toes, knees, hips, chest, head, hands) and each one's clearance
            // over the floor (the feet's rest heights, the rest a little), and the support fist's eased weight.
            public readonly Transform[] Contacts=new Transform[ContactBones.Length]; public readonly float[] Clearance=new float[ContactBones.Length]; public float Support, SupportGap;
            public readonly NativeVisualMotion.Memory Memory=new NativeVisualMotion.Memory();
            public bool HasHold; public Vector3 HoldPosition, LeftHoldPosition; public Quaternion HoldRotation=Quaternion.identity, LeftHoldRotation=Quaternion.identity;
            // The sword's running carry (Core NativeSwordTrail): its arm, fist, spine and head, the carry's eased share and the
            // clip's own arm swing's eased mean and the fist's last twist (NaN: not carrying).
            public Transform RightArm, RightForearm, RightFist, Spine, Head; public float Trail, TrailMean=float.NaN, TrailTwist=float.NaN;
            // Each locomotion clip's stride offset onto the first's (NativeVisualMotionClips.StridePhase).
            public readonly float[] LocoOffset=new float[NativeVisualStride.Clips];
            // The motion's inertia (VisualMotionInertia): the last drawn mix and its rate, the difference being carried off and its
            // rate, the legs' mix under an upper layer, and the upper layer's last clip and time.
            public float[] Out, OutVel, Off, OffVel, Legs; public bool HasOut; public string UpA; public float UpTimeA;
            // The contact guard's switch (pinned or only kept out of the floor), the lift it applied and the difference it carries.
            public bool GuardPin, GuardSeen; public float GuardApplied, GuardCarry, GuardCarryVel;
            // The feet (VisualMotionFeet, left then right): the leg bones, the lock (held, where, the body's heading then, its
            // weight) and a step across (its share done, -1: none; from where and from which heading).
            public readonly Transform[] LegUpper=new Transform[2], LegLower=new Transform[2], LegFoot=new Transform[2];
            public readonly bool[] FootLocked=new bool[2]; public readonly Vector3[] FootLock=new Vector3[2], FootFrom=new Vector3[2];
            public readonly float[] FootYaw=new float[2], FootFromYaw=new float[2], FootLockWeight=new float[2], FootStep={-1f,-1f}; public float Plant;
        }
        // The bake's motion bank (null without one: the game's pose everywhere), read once.
        private static NativeVisualMotionClips.Library visualMotionBank; private static bool visualMotionRead;
        private const string VisualMotionResource="ChipArenaVisualMotion";
        private static NativeVisualMotionClips.Library VisualMotionBank
        {
            get
            {
                if(visualMotionRead)return visualMotionBank;visualMotionRead=true;
                var bytes=Resources.Load<TextAsset>(VisualMotionResource);if(bytes==null){visualMotionWhy="no motion bank";return null;}
                try{visualMotionBank=NativeVisualMotionClips.Read(bytes.bytes);}catch(System.Exception e){visualMotionWhy="motion bank: "+e.Message;Debug.LogWarning("Visual motion: "+e.Message);}
                return visualMotionBank;
            }
        }
        // The two-handed support fist's place on the handle (the weapon frame, metres along the blade: behind the right fist).
        private const float SupportAlong=-.10f, SupportReach=.22f, SupportLetGo=.40f, SupportEase=.12f, MotionEase=.15f, UpperEase=.12f, AimTurnLimit=35f, CarryTilt=20f;
        // The upper body's muscles (a gun's aim, recoil and reload layer): every muscle but the legs'.
        private static bool[] upperMuscles;
        private static bool[] UpperMuscles{get{if(upperMuscles==null){var n=HumanTrait.MuscleName;upperMuscles=new bool[n.Length];for(int i=0;i<n.Length;i++)upperMuscles[i]=!(n[i].Contains("Leg")||n[i].Contains("Foot")||n[i].Contains("Toes"));}return upperMuscles;}}
        // The Whirlwind's legs (NativeVisualMotion.SpinLegs), as muscle offsets: the sit's thigh raise and knee bend, and a
        // step's (each leg's Upper Leg Front-Back, Lower Leg Stretch; muscles left, then right).
        private const float SitThigh=.12f, SitKnee=.2f, StepThigh=.22f, StepKnee=.38f;
        private static int[] spinLegMuscles;
        private static int[] SpinLegMuscles{get{if(spinLegMuscles==null){var n=HumanTrait.MuscleName;spinLegMuscles=new[]{System.Array.IndexOf(n,"Left Upper Leg Front-Back"),System.Array.IndexOf(n,"Left Lower Leg Stretch"),System.Array.IndexOf(n,"Right Upper Leg Front-Back"),System.Array.IndexOf(n,"Right Lower Leg Stretch")};}return spinLegMuscles;}}
        private static void VisualSpinLegs(VisualMotion m,in NativeVisualMotion.Pick pick)
        {
            var leg=SpinLegMuscles;
            for(int side=0;side<2;side++)
            {
                int thigh=leg[side*2],knee=leg[side*2+1];if(thigh<0||knee<0)continue;float lift=side==0?pick.LiftL:pick.LiftR;
                m.Mix[NativeVisualMotionClips.Head+thigh]+=SitThigh*pick.Sit+StepThigh*lift;m.Mix[NativeVisualMotionClips.Head+knee]-=SitKnee*pick.Sit+StepKnee*lift;
            }
        }
        private static readonly HumanBodyBones[] ContactBones={HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightFoot,HumanBodyBones.RightToes,
            HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.Hips,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand};
        private static readonly float[] ContactRadius={-1,-1,-1,-1,.06f,.06f,.10f,.12f,.12f,.05f,.05f};
        // Why the last body could not play its set (null: it could): for the local checks.
        private static string visualMotionWhy;

        // A body's motion for `spec` (made once per set the body plays; null without the set or its clips in the bake).
        private static VisualMotion VisualMotionFor(VisualBody body,string spec)
        {
            var set=NativeVisualMotion.For(spec);if(set==null)return null;
            if(body.Motion!=null&&body.Motion.Set==set)return body.Motion;
            VisualMotionRelease(body);body.Motion=null;
            var bank=VisualMotionBank;if(bank==null)return null;
            var skeleton=body.Root.transform.Find("VisualSkeleton");var source=skeleton!=null?skeleton.GetComponent<Animator>():null;
            if(source==null||source.avatar==null||!source.avatar.isHuman){visualMotionWhy="no humanoid avatar";return null;}
            var names=new List<string>();foreach(var n in set.All)if(bank.Has(n))names.Add(n);
            if(!names.Contains(set.Idle)){visualMotionWhy="no "+set.Idle+" among "+bank.Clips.Count+" clips";return null;}
            var proxy=Instantiate(skeleton.gameObject,body.Root.transform,false);proxy.name="Visual motion";
            foreach(var r in proxy.GetComponentsInChildren<Renderer>(true))Destroy(r);
            var animator=proxy.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.enabled=true;
            var m=new VisualMotion{Set=set,Proxy=proxy,Animator=animator,Names=names.ToArray(),Handler=new HumanPoseHandler(source.avatar,proxy.transform),Mix=new float[NativeVisualMotionClips.Head+bank.Muscles],UpperMix=new float[NativeVisualMotionClips.Head+bank.Muscles]};
            m.Pose.muscles=new float[bank.Muscles];
            m.Clips=new NativeVisualMotionClips.Clip[names.Count];m.Weights=new float[names.Count];m.Times=new float[names.Count];
            for(int i=0;i<names.Count;i++){m.Index[names[i]]=i;m.Clips[i]=bank.Clips[names[i]];}
            m.Loco=new int[NativeVisualStride.Clips];
            for(int k=0;k<m.Loco.Length;k++)
            {
                m.Loco[k]=m.Index.TryGetValue(set.Locomotion[k],out int i)?i:-1;
                if(m.Loco[k]>=0){var c=m.Clips[m.Loco[k]];m.SpeedX[k]=c.SpeedX;m.SpeedZ[k]=c.SpeedZ;m.LocoLength[k]=Mathf.Max(.1f,c.Length);}
            }
            // The two skeletons, bone for bone (the proxy is the skeleton's copy: the same order).
            m.ProxyBones=proxy.GetComponentsInChildren<Transform>(true);m.BodyBones=skeleton.GetComponentsInChildren<Transform>(true);
            if(m.ProxyBones.Length!=m.BodyBones.Length){visualMotionWhy="proxy "+m.ProxyBones.Length+" bones, body "+m.BodyBones.Length;m.Handler.Dispose();Destroy(proxy);return null;}
            m.ProxyHips=animator.GetBoneTransform(HumanBodyBones.Hips);m.BodyHips=m.ProxyHips!=null?Twin(proxy.transform,skeleton,m.ProxyHips):null;
            Transform Body(HumanBodyBones b){var t=animator.GetBoneTransform(b);return t!=null?Twin(proxy.transform,skeleton,t):null;}
            m.RightHand=Body(HumanBodyBones.RightHand);m.LeftHand=Body(HumanBodyBones.LeftHand);m.LeftArm=Body(HumanBodyBones.LeftUpperArm);m.LeftForearm=Body(HumanBodyBones.LeftLowerArm);
            m.LeftIndex=Body(HumanBodyBones.LeftIndexProximal);m.LeftLittle=Body(HumanBodyBones.LeftLittleProximal);
            m.LeftFist=Body(HumanBodyBones.LeftMiddleProximal);
            m.RightArm=Body(HumanBodyBones.RightUpperArm);m.RightForearm=Body(HumanBodyBones.RightLowerArm);m.RightFist=Body(HumanBodyBones.RightMiddleProximal);
            m.Spine=Body(HumanBodyBones.Spine);m.Head=Body(HumanBodyBones.Head);
            m.LegUpper[0]=Body(HumanBodyBones.LeftUpperLeg);m.LegLower[0]=Body(HumanBodyBones.LeftLowerLeg);m.LegFoot[0]=Body(HumanBodyBones.LeftFoot);
            m.LegUpper[1]=Body(HumanBodyBones.RightUpperLeg);m.LegLower[1]=Body(HumanBodyBones.RightLowerLeg);m.LegFoot[1]=Body(HumanBodyBones.RightFoot);
            // Each stride's offset, so the mixed runs, walks and sprint put the same foot down together.
            var muscle=HumanTrait.MuscleName;int lk=System.Array.IndexOf(muscle,"Left Lower Leg Stretch"),rk=System.Array.IndexOf(muscle,"Right Lower Leg Stretch");
            float first=m.Loco[0]>=0?NativeVisualMotionClips.StridePhase(m.Clips[m.Loco[0]],lk,rk):0;
            for(int k=0;k<m.Loco.Length;k++)if(m.Loco[k]>=0)m.LocoOffset[k]=Mathf.Repeat(NativeVisualMotionClips.StridePhase(m.Clips[m.Loco[k]],lk,rk)-first,1);
            // The feet's clearances: their rest heights, from the bake itself (the body here already wears this frame's pose).
            var rest=visualBodyPrefab!=null?visualBodyPrefab.transform.Find("VisualSkeleton"):null;
            for(int f=0;f<ContactBones.Length;f++)
            {
                var pb=animator.GetBoneTransform(ContactBones[f]);m.Contacts[f]=pb!=null?Twin(proxy.transform,skeleton,pb):null;
                var r=pb!=null&&rest!=null&&ContactRadius[f]<0?Twin(proxy.transform,rest,pb):null;m.Clearance[f]=ContactRadius[f]>=0?ContactRadius[f]:r!=null?r.position.y-visualBodyPrefab.transform.position.y+body.Lift:0;
            }
            if(m.BodyHips==null||m.RightHand==null){visualMotionWhy="no hips or right hand";m.Handler.Dispose();Destroy(proxy);return null;}
            visualMotionWhy=null;body.Motion=m;return m;
        }
        // A two-bone reach for an arm: the elbow kept on the clip's side of the shoulder-to-target line, leaning toward `pole`
        // (down and out), so it never flips across the line when the clip's elbow lies near it.
        private static void ArmReachTo(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole)
        {
            Vector3 a=upper.position,b=lower.position,c=hand.position,toward=target-a;float l1=(b-a).magnitude,l2=(c-b).magnitude;
            if(toward.sqrMagnitude<1e-8f||l1<1e-4f||l2<1e-4f)return;
            float d=Mathf.Clamp(toward.magnitude,.01f,(l1+l2)*.999f);var dir=toward.normalized;
            Vector3 Off(Vector3 v)=>v-Vector3.Dot(v,dir)*dir;
            var side=Off(b-a);var lean=Off(pole);if(lean.sqrMagnitude>1e-8f)side+=lean.normalized*.05f;if(side.sqrMagnitude<1e-10f)return;side.Normalize();
            float along=(l1*l1-l2*l2+d*d)/(2*d),high=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            upper.rotation=Quaternion.FromToRotation(b-a,dir*along+side*high)*upper.rotation;
            b=lower.position;c=hand.position;lower.rotation=Quaternion.FromToRotation(c-b,a+dir*d-b)*lower.rotation;
        }
        // The sword's running carry on the body by `w` (0..1): the spine leaned in about the body's right and the head kept up;
        // the arm reaching back and out from the shoulder (a little of the clip's own swing kept, in time with its stride); the
        // fist turned so the blade trails out and back, its twist taken up by the shoulder and the forearm so the wrist only
        // bends (NativeSwordTrail).
        private static void VisualSwordTrail(VisualMotion m,Transform origin,float w,float speed,float dt)
        {
            if(w<=0||m.RightArm==null||m.RightForearm==null)return;
            var turn=origin.rotation;var inv=Quaternion.Inverse(turn);
            if(m.Spine!=null)
            {
                float lean=NativeSwordTrail.LeanAt(speed)*w;m.Spine.rotation=Quaternion.AngleAxis(lean,origin.right)*m.Spine.rotation;
                if(m.Head!=null)m.Head.rotation=Quaternion.AngleAxis(-lean*NativeSwordTrail.HeadKeep,origin.right)*m.Head.rotation;
            }
            Vector3 shoulder=m.RightArm.position,elbow=m.RightForearm.position,fist=m.RightHand.position;
            float reach=((elbow-shoulder).magnitude+(fist-elbow).magnitude)*NativeSwordTrail.Reach,pitch=NativeSwordTrail.Pitch(CapturedAnimation.Num(inv*(fist-shoulder)));
            m.TrailMean=float.IsNaN(m.TrailMean)?pitch:m.TrailMean+(pitch-m.TrailMean)*(1-Mathf.Exp(-dt/NativeSwordTrail.SwingMean));
            var to=shoulder+turn*CapturedAnimation.Vec(NativeSwordTrail.Arm((pitch-m.TrailMean)*NativeSwordTrail.Swing))*reach;
            ArmReachTo(m.RightArm,m.RightForearm,m.RightHand,Vector3.Lerp(fist,to,w),origin.up-origin.forward);
            if(m.RightFist==null)return;
            elbow=m.RightForearm.position;fist=m.RightHand.position;var hand=m.RightHand.rotation;
            var knuckles=Quaternion.Inverse(hand)*(m.RightFist.position-fist);var forearm=fist-elbow;
            if(knuckles.sqrMagnitude<1e-10f||forearm.sqrMagnitude<1e-10f)return;
            var goal=CapturedAnimation.Quat(NativeSwordTrail.Hand(CapturedAnimation.Num(m.HoldRotation*Vector3.forward),CapturedAnimation.Num(knuckles),CapturedAnimation.Num(forearm),CapturedAnimation.Num(turn*CapturedAnimation.Vec(NativeSwordTrail.Blade)),NativeSwordTrail.MaxBend));
            goal=Quaternion.Slerp(hand,goal,w);
            // The fist's twist about the forearm, taken off the wrist: part turned at the shoulder (about the shoulder-to-fist
            // line, the fist kept in place), the rest along the forearm.
            // Only the carry's own turn of the fist (from the clip's) counts: the clip's wrist is its own, so the carry easing in
            // or out never spins the forearm (measured: 75-80 degrees in one frame as it came and went).
            float twist=NativeSwordTrail.Twist(CapturedAnimation.Num(goal*Quaternion.Inverse(hand)),CapturedAnimation.Num(forearm.normalized))*Mathf.Rad2Deg;
            // Unwound from the last frame's, so a twist near a half turn never flips the elbow to the other side.
            if(!float.IsNaN(m.TrailTwist)){twist=m.TrailTwist+Mathf.DeltaAngle(m.TrailTwist,twist);if(Mathf.Abs(twist)>270)twist-=Mathf.Sign(twist)*360;}m.TrailTwist=twist;
            m.RightArm.rotation=Quaternion.AngleAxis(twist*NativeSwordTrail.ShoulderTwist,(fist-shoulder).normalized)*m.RightArm.rotation;
            m.RightForearm.rotation=Quaternion.AngleAxis(twist*(1-NativeSwordTrail.ShoulderTwist),(fist-m.RightForearm.position).normalized)*m.RightForearm.rotation;
            m.RightHand.rotation=goal;
        }
        // The hold's turn that points the weapon's bore (+z) along the body's facing and its top (+y) up while the proxy wears
        // `level` (its first frame); the proxy is posed again by the next mix.
        private static void VisualMotionCalibrate(VisualMotion m,NativeVisualMotionClips.Clip level,Transform origin,bool left=false)
        {
            System.Array.Clear(m.Mix,0,m.Mix.Length);NativeVisualMotionClips.Add(level,0,1,m.Mix);NativeVisualMotionClips.Finish(m.Mix,1);
            m.Pose.bodyPosition=new Vector3(m.Mix[0],m.Mix[1],m.Mix[2]);m.Pose.bodyRotation=new Quaternion(m.Mix[3],m.Mix[4],m.Mix[5],m.Mix[6]);
            System.Array.Copy(m.Mix,NativeVisualMotionClips.Head,m.Pose.muscles,0,m.Pose.muscles.Length);m.Handler.SetHumanPose(ref m.Pose);
            var hand=m.Animator!=null?m.Animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand):null;if(hand==null)return;
            var held=Quaternion.Inverse(hand.rotation)*Quaternion.LookRotation(origin.forward,origin.up);if(left)m.LeftHoldRotation=held;else m.HoldRotation=held;
        }
        // A mix (body frame and muscles) onto the proxy.
        private static void VisualMotionApply(VisualMotion m,float[] mix)
        {
            m.Pose.bodyPosition=new Vector3(mix[0],mix[1],mix[2]);m.Pose.bodyRotation=new Quaternion(mix[3],mix[4],mix[5],mix[6]);
            System.Array.Copy(mix,NativeVisualMotionClips.Head,m.Pose.muscles,0,m.Pose.muscles.Length);
            m.Handler.SetHumanPose(ref m.Pose);
        }
        // The gait's smoothing (1/s: about 0.2 s).
        private const float GaitRate=5f;
        // The locomotion clips' weights for the gait's travel; their rate for the actual speed.
        private static float LocoRate(VisualMotion m,Vector3 gait,float gaitSpeed,float speed)
        {
            float rate=NativeVisualStride.Weights(gait.x,gait.z,m.SpeedX,m.SpeedZ,m.LocoWeights);
            return gaitSpeed>.05f?Mathf.Clamp(rate*speed/gaitSpeed,NativeVisualStride.RateMin,NativeVisualStride.RateMax):rate;
        }
        // A weight eased in and out (a crossfade starts and ends gently instead of at a constant rate).
        private static float Ease(float w)=>w*w*(3-2*w);
        // A critically damped spring carrying `x` (its rate `v`) to nothing: about 2/omega seconds, no overshoot.
        private static void Spring(ref float x,ref float v,float omega,float dt){float e=Mathf.Exp(-omega*dt),j=(v+omega*x)*dt;x=(x+j)*e;v=(v-omega*j)*e;}
        // The inertia's and the guard's springs (rad/s: settled in about 0.2 and 0.25 s).
        private const float InertiaOmega=20f, GuardOmega=16f;
        // The motion's inertia (owner 2026-10-27: "Make the movements look realistic and not glitchy"): when the mixed pose would
        // jump (`jumped`: a clip still showing begun again or re-timed, an upper layer switching clips), the difference from
        // the last drawn pose is kept and carried off by a critically damped spring, with its motion, so the body never pops.
        // `legs` (the legs' mix under an upper layer) takes the same.
        private static void VisualMotionInertia(VisualMotion m,bool jumped,float dt,bool legs)
        {
            int n=m.Mix.Length;
            if(m.Off==null||m.Off.Length!=n){m.Off=new float[n];m.OffVel=new float[n];m.Out=new float[n];m.OutVel=new float[n];m.HasOut=false;}
            if(m.Legs==null||m.Legs.Length!=n)m.Legs=new float[n];
            // The body's turn on the last drawn one's side (a rotation and its negative are one turn).
            if(m.HasOut){float q=0;for(int i=3;i<7;i++)q+=m.Out[i]*m.Mix[i];if(q<0)for(int i=3;i<7;i++){m.Mix[i]=-m.Mix[i];if(legs)m.Legs[i]=-m.Legs[i];}}
            if(jumped&&m.HasOut&&dt>0)for(int i=0;i<n;i++){m.Off[i]=m.Out[i]-m.Mix[i];m.OffVel[i]=m.OutVel[i];}
            bool any=false;
            for(int i=0;i<n;i++)
            {
                float x=m.Off[i],v=m.OffVel[i];if(x==0&&v==0)continue;
                Spring(ref x,ref v,InertiaOmega,dt);if(Mathf.Abs(x)<1e-5f&&Mathf.Abs(v)<1e-4f)x=v=0;
                m.Off[i]=x;m.OffVel[i]=v;m.Mix[i]+=x;if(legs)m.Legs[i]+=x;any=true;
            }
            if(any){Unit(m.Mix);if(legs)Unit(m.Legs);}
            for(int i=0;i<n;i++){m.OutVel[i]=m.HasOut&&dt>0?(m.Mix[i]-m.Out[i])/dt:0;m.Out[i]=m.Mix[i];}
            m.HasOut=true;
        }
        private static void Unit(float[] mix)
        {
            float l=Mathf.Sqrt(mix[3]*mix[3]+mix[4]*mix[4]+mix[5]*mix[5]+mix[6]*mix[6]);
            if(l>1e-6f)for(int i=3;i<7;i++)mix[i]/=l;else{mix[3]=mix[4]=mix[5]=0;mix[6]=1;}
        }
        // The feet (owner 2026-10-27): a foot the clip puts down stays where it landed (its leg reaching it) and keeps its world
        // heading, easing back to the clip as the clip lifts it, so neither the mix's stride nor the body's travel slides it on
        // the floor (measured: 0.8-1.0 m/s on planted feet while running). A lock is taken where the foot is drawn (an old one
        // still easing out included) and kept within FootReach of the clip's foot, so it never jumps. Turning or drifting in
        // place, a planted foot steps across (StepTime, lifted StepLift) once the clip's own foot is StepAt from it or the body
        // has turned StepTurn, one foot at a time (the feet used to pivot with the body); a turn past FootTwist lets go.
        // `plant` (0..1): how far this frame's motion plants its feet (eased over FootEase).
        private const float FootDown=.025f, FootUp=.05f, FootReach=.3f, FootRise=.15f, FootEase=.1f, FootTwist=75f, StepAt=.16f, StepTurn=40f, StepTime=.2f, StepLift=.06f;
        // A lock lets go before its leg is FootStretch of its length from the hip, and the leg never reaches past LegReach of it: a
        // straight leg's knee has no side to keep (a sprint's lock left behind stretched it, its knee swinging 18 cm a frame).
        private const float FootStretch=.95f, LegReach=.97f;
        private static readonly Vector3[] footClip=new Vector3[2];
        private static void VisualMotionFeet(VisualMotion m,Transform origin,float plant,bool locomotion,float dt,float wgt)
        {
            Vector3 up=origin.up,floor=origin.position;float yaw=origin.eulerAngles.y;
            Vector3 Flat(Vector3 v)=>v-Vector3.Dot(v,up)*up;
            m.Plant=Mathf.MoveTowards(m.Plant,plant,dt/FootEase);
            for(int f=0;f<2;f++)
            {
                var foot=m.LegFoot[f];if(foot==null||m.LegUpper[f]==null||m.LegLower[f]==null){m.FootLocked[f]=false;continue;}
                var clip=footClip[f]=foot.position;float h=Vector3.Dot(clip-floor,up)-m.Clearance[f*2];
                if(m.FootLocked[f])
                {
                    // Kept within reach of the clip's foot (dragged, not let go), let go as the clip lifts it, the floor drops
                    // away from it or the body turns too far from it.
                    var off=Flat(m.FootLock[f]-clip);if(off.sqrMagnitude>FootReach*FootReach)m.FootLock[f]=clip+off.normalized*FootReach+up*Vector3.Dot(m.FootLock[f]-clip,up);
                    float leg=(m.LegUpper[f].position-m.LegLower[f].position).magnitude+(m.LegLower[f].position-foot.position).magnitude;
                    if(m.Plant<.05f||(m.FootLock[f]-m.LegUpper[f].position).magnitude>leg*FootStretch||h>FootUp&&m.FootStep[f]<0||Mathf.Abs(Vector3.Dot(m.FootLock[f]-floor,up)-m.Clearance[f*2])>FootRise||Mathf.Abs(Mathf.DeltaAngle(yaw,m.FootYaw[f]))>FootTwist&&m.FootStep[f]<0)
                    {m.FootLocked[f]=false;m.FootStep[f]=-1;}
                }
                else if(m.Plant>=.05f&&h<FootDown)
                {
                    // Taken where the foot is drawn now (a lock still easing out holds it part way).
                    float held=m.FootLockWeight[f];m.FootLock[f]=held>0?Vector3.Lerp(clip,m.FootLock[f],held):clip;
                    m.FootYaw[f]=held>0?yaw+Mathf.DeltaAngle(yaw,m.FootYaw[f])*held:yaw;m.FootLocked[f]=true;m.FootLockWeight[f]=1;m.FootStep[f]=-1;
                }
                if(!m.FootLocked[f])m.FootLockWeight[f]=Mathf.MoveTowards(m.FootLockWeight[f],0,dt/FootEase);
            }
            // A step across: the planted foot furthest from its clip's, while neither steps (the run's own strides lift its feet).
            if(!locomotion&&m.FootStep[0]<0&&m.FootStep[1]<0)
            {
                int best=-1;float need=1;
                for(int f=0;f<2;f++)
                {
                    if(!m.FootLocked[f])continue;
                    float k=Mathf.Max(Flat(footClip[f]-m.FootLock[f]).magnitude/StepAt,Mathf.Abs(Mathf.DeltaAngle(yaw,m.FootYaw[f]))/StepTurn);
                    if(k>need){need=k;best=f;}
                }
                if(best>=0){m.FootStep[best]=0;m.FootFrom[best]=m.FootLock[best];m.FootFromYaw[best]=m.FootYaw[best];}
            }
            for(int f=0;f<2;f++)
            {
                if(m.FootStep[f]>=0&&m.FootLocked[f])
                {
                    m.FootStep[f]+=dt/StepTime;
                    if(m.FootStep[f]>=1){m.FootStep[f]=-1;m.FootLock[f]=footClip[f];m.FootYaw[f]=yaw;}
                }
                float w=m.FootLockWeight[f]*Mathf.Clamp01(m.Plant)*wgt;if(w<.001f||m.LegFoot[f]==null)continue;
                Vector3 target=m.FootLock[f];float heading=Mathf.DeltaAngle(yaw,m.FootYaw[f]);
                if(m.FootStep[f]>=0)
                {
                    float s=m.FootStep[f],e=Ease(s);
                    target=Vector3.Lerp(m.FootFrom[f],footClip[f],e)+up*(StepLift*Mathf.Sin(Mathf.PI*s));heading=Mathf.DeltaAngle(yaw,m.FootFromYaw[f])*(1-e);
                }
                // The knee kept on its side of the hip-to-foot line, leaning ahead (it never flips across).
                var foot=m.LegFoot[f];var turn=Quaternion.AngleAxis(heading*w,up)*foot.rotation;Vector3 hip=m.LegUpper[f].position;
                float length=((hip-m.LegLower[f].position).magnitude+(m.LegLower[f].position-foot.position).magnitude)*LegReach;if((target-hip).sqrMagnitude>length*length)target=hip+(target-hip).normalized*length;
                ArmReachTo(m.LegUpper[f],m.LegLower[f],foot,Vector3.Lerp(foot.position,target,w),origin.forward);foot.rotation=turn;
            }
        }
        private static void VisualMotionRelease(VisualBody body)
        {
            var m=body?.Motion;if(m==null)return;m.Handler?.Dispose();if(m.Proxy!=null)Destroy(m.Proxy);body.Motion=null;
        }

        // VisualBodyPose, after the game pose, the weapon frames and the stride are on the body: the set's clips over them.
        private void VisualMotionPose(VisualBody body,CapturedAnimation.Player rig,Actor a,float time)
        {
            var m=NativeVisualMotion.For(a.SourceSpec)!=null&&!StrideOff?VisualMotionFor(body,a.SourceSpec):null;
            if(m==null){if(body.Motion!=null)body.Motion.Weight=0;return;}
            float dt=float.IsNaN(m.Time)?0:Mathf.Clamp(time-m.Time,0,.1f);m.Time=time;
            // The body's travel (smoothed, as the stride's).
            Vector3 now=rig.Root.transform.position;
            // A teleport (or the first frame) lets go of the feet at once.
            if((now-m.LastAt).sqrMagnitude>4){m.FootLocked[0]=m.FootLocked[1]=false;m.FootLockWeight[0]=m.FootLockWeight[1]=0;m.FootStep[0]=m.FootStep[1]=-1;}
            if(dt>0){var v=(now-m.LastAt)/dt;if(v.sqrMagnitude>400)v=m.Velocity;m.Velocity+=(v-m.Velocity)*(1-Mathf.Exp(-dt*10));}
            m.LastAt=now;var velocity=m.Velocity;
            // The hand-to-weapon offset of the game pose's hold (its fist on the handle; the frame the game's own model draws at
            // where the bake has none), kept from its first frame.
            // The off hand's likewise (its frame: the daggers' second blade, or the bare wrist).
            var weaponHand=rig.Bones[CapturedRig.RHand];var offHand=rig.Bones[CapturedRig.LHand];
            if(!m.HasHold)
            {
                var inv=Quaternion.Inverse(m.RightHand.rotation);m.HoldPosition=inv*(weaponHand.position-m.RightHand.position);m.HoldRotation=inv*weaponHand.rotation;
                if(m.LeftHand!=null){inv=Quaternion.Inverse(m.LeftHand.rotation);m.LeftHoldPosition=inv*(offHand.position-m.LeftHand.position);m.LeftHoldRotation=inv*offHand.rotation;}
                m.HasHold=true;
                // A shaft in the left fist (the staff): its axis (the off-hand frame's most upright, as the game carries it) along
                // the fist's grip, the knuckles' line toward the thumb.
                if(m.Set.LeftShaft&&m.LeftHand!=null&&m.LeftIndex!=null&&m.LeftLittle!=null)
                {
                    Vector3 shaft=Vector3.forward;float best=-2;
                    foreach(var axis in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){float d=Vector3.Dot(offHand.rotation*axis,Vector3.up);if(d>best){best=d;shaft=axis;}}
                    var grip=Quaternion.Inverse(m.LeftHand.rotation)*(m.LeftIndex.position-m.LeftLittle.position);
                    if(grip.sqrMagnitude>1e-8f)m.LeftHoldRotation=Quaternion.FromToRotation(m.LeftHoldRotation*shaft,grip)*m.LeftHoldRotation;
                }
                // A gun's turn in the hand from the pack's own level aim: there its barrel points straight ahead, upright.
                if(m.Set.Gun&&VisualMotionBank.Clips.TryGetValue(m.Set.Aim[1],out var level))VisualMotionCalibrate(m,level,body.Root.transform);
                // A bow's likewise, in the left fist: upright (its limbs the frame's up), its arrow line ahead.
                else if(m.Set.Pull!=null&&VisualMotionBank.Clips.TryGetValue(m.Set.Aim[1],out var drawn))VisualMotionCalibrate(m,drawn,body.Root.transform,true);
            }
            var local=Quaternion.Inverse(body.Root.transform.rotation)*velocity;float speed=new Vector2(local.x,local.z).magnitude;
            bool moving=a.Grounded&&speed>NativeVisualStride.Moving;
            bool covered=NativeVisualMotion.Choose(m.Set,a,time,m.Memory,VisualMotionBank,moving,out var pick);
            m.Weight=Mathf.MoveTowards(m.Weight,covered?1:0,dt/MotionEase);
            if(m.Weight<=0&&!covered)
            {
                // Off the model: what carried its pose over (inertia, the guard's switch, the feet) starts afresh.
                m.HasOut=m.GuardSeen=false;m.GuardCarry=m.GuardCarryVel=0;m.FootLocked[0]=m.FootLocked[1]=false;m.FootLockWeight[0]=m.FootLockWeight[1]=0;m.UpA=null;
                if(m.Off!=null){System.Array.Clear(m.Off,0,m.Off.Length);System.Array.Clear(m.OffVel,0,m.OffVel.Length);}
                return;
            }
            // The mix: the picked clip (or the locomotion blend) fades in over the pick's fade, the rest fade out, going on at
            // their own pace (a pose held at its last time read as a freeze); their poses are averaged by weight, eased in and
            // out. Locomotion's clips take its share by the travel weights, each at the shared phase plus its stride's offset.
            int target=-1;bool jumped=false;
            if(covered&&!pick.Locomotion&&m.Index.TryGetValue(pick.Clip,out int ti))
            {
                // A clip still showing whose time jumps (a strike or a reaction begun again, a time fitted anew) would pop: the
                // difference is carried off instead (VisualMotionInertia).
                target=ti;float next=Mathf.Max(0,pick.Time),d=next-m.Times[ti];
                if(m.Weights[ti]>.02f&&(d<-.02f||d>.05f+dt*2.5f))jumped=true;
                m.Times[ti]=next;
            }
            float step=dt/Mathf.Max(.02f,pick.Fade);
            for(int i=0;i<m.Weights.Length;i++)
            {
                if(i!=target&&m.Weights[i]>0){var c=m.Clips[i];m.Times[i]=c.Loop?m.Times[i]+dt:Mathf.Min(m.Times[i]+dt,c.Length);}
                m.Weights[i]=Mathf.MoveTowards(m.Weights[i],i==target?1:0,step);
            }
            m.LocoShare=Mathf.MoveTowards(m.LocoShare,covered&&pick.Locomotion?1:0,step);
            // The gait's clips by a slower speed along the travel's way (the rules stop a pilot at once: crossing the walk-to-run
            // band in 40 ms whipped the arms between the run's swing and the walk's carry), played at the rate the actual speed
            // needs.
            m.GaitSpeed+=(speed-m.GaitSpeed)*(dt>0?1-Mathf.Exp(-dt*GaitRate):0);float gaitSpeed=Mathf.Max(m.GaitSpeed,speed>.05f?.05f:0);
            var gait=speed>1e-3f?new Vector3(local.x,0,local.z)*(gaitSpeed/speed):Vector3.zero;
            float locoRate=m.LocoShare>0?LocoRate(m,gait,gaitSpeed,speed):1;
            float sum=Ease(m.LocoShare);for(int i=0;i<m.Weights.Length;i++)sum+=Ease(m.Weights[i]);
            if(sum<1e-4f)
            {
                if(target>=0)m.Weights[target]=1;else{m.LocoShare=1;locoRate=LocoRate(m,gait,gaitSpeed,speed);}
                sum=1;
            }
            if(m.LocoShare>0)m.Phase=Mathf.Repeat(m.Phase+dt*locoRate/NativeVisualStride.Cycle(m.LocoWeights,m.LocoLength),1);
            float loco=Ease(m.LocoShare);
            System.Array.Clear(m.Mix,0,m.Mix.Length);
            for(int i=0;i<m.Clips.Length;i++)NativeVisualMotionClips.Add(m.Clips[i],m.Times[i],Ease(m.Weights[i]),m.Mix);
            for(int k=0;k<m.Loco.Length;k++){int i=m.Loco[k];if(i>=0)NativeVisualMotionClips.Add(m.Clips[i],Mathf.Repeat(m.Phase+m.LocoOffset[k],1)*m.LocoLength[k],m.LocoWeights[k]*loco,m.Mix);}
            NativeVisualMotionClips.Finish(m.Mix,sum);
            if(covered&&pick.Spin)VisualSpinLegs(m,pick);
            // A gun's upper body (aimed by the pitch, its recoil, or its reload), a bow's (drawn, loosed) or a caster's (its cast),
            // over the legs' motion by its eased weight.
            // The arms are free while the body idles, moves, rises, falls or lands (not in a roll, strike, reaction or block).
            bool ready=covered&&(pick.Locomotion||pick.Clip==m.Set.Idle||pick.Clip==m.Set.Jump||pick.Clip==m.Set.Fall||pick.Clip==m.Set.Land);
            var up=default(NativeVisualMotion.Upper);bool layered=covered&&NativeVisualMotion.Layer(m.Set,a,time,VisualMotionBank,out up,ready);
            m.Upper=layered&&up.Raised&&up.Snap?1:Mathf.MoveTowards(m.Upper,layered&&up.Raised?1:0,dt/UpperEase);
            NativeVisualMotionClips.Clip ua=default;bool upper=layered&&m.Upper>0&&VisualMotionBank.Clips.TryGetValue(up.A,out ua);
            // An upper layer that switches clips or jumps in time (a throw into the aim, a cast's start into its end, a shot's
            // recoil begun again) is carried over likewise.
            if(upper&&m.UpA!=null&&(up.A!=m.UpA||!(Mathf.Abs(up.TimeA-m.UpTimeA-dt)<.05f+dt*2.5f)))jumped=true;
            m.UpA=upper?up.A:null;m.UpTimeA=up.TimeA;
            // A human pose places the body by its centre of mass: a raised upper body would swing the hips (and the legs with
            // them). The legs' motion is posed first and its hips kept.
            Vector3 hipsAt=default;Quaternion hipsTurn=default;
            if(upper)
            {
                if(m.Legs==null||m.Legs.Length!=m.Mix.Length)m.Legs=new float[m.Mix.Length];System.Array.Copy(m.Mix,m.Legs,m.Mix.Length);
                System.Array.Clear(m.UpperMix,0,m.UpperMix.Length);NativeVisualMotionClips.Add(ua,up.TimeA,1-up.Mix,m.UpperMix);
                if(up.B!=null&&up.Mix>0&&VisualMotionBank.Clips.TryGetValue(up.B,out var ub))NativeVisualMotionClips.Add(ub,up.TimeB,up.Mix,m.UpperMix);
                NativeVisualMotionClips.Finish(m.UpperMix,1);var mask=UpperMuscles;
                for(int i=0;i<mask.Length&&NativeVisualMotionClips.Head+i<m.Mix.Length;i++)if(mask[i]){int k=NativeVisualMotionClips.Head+i;m.Mix[k]+=(m.UpperMix[k]-m.Mix[k])*m.Upper;}
            }
            VisualMotionInertia(m,jumped,dt,upper);
            if(upper){VisualMotionApply(m,m.Legs);hipsAt=m.ProxyHips.localPosition;hipsTurn=m.ProxyHips.localRotation;}
            VisualMotionApply(m,m.Mix);
            if(upper)m.ProxyHips.SetLocalPositionAndRotation(hipsAt,hipsTurn);
            // The proxy's pose onto the body by the motion's weight.
            float wgt=m.Weight;
            for(int i=0;i<m.BodyBones.Length;i++)
            {
                var b=m.BodyBones[i];var p=m.ProxyBones[i];
                b.localRotation=wgt>=.999f?p.localRotation:Quaternion.Slerp(b.localRotation,p.localRotation,wgt);
                if(b==m.BodyHips)b.localPosition=Vector3.Lerp(b.localPosition,p.localPosition,wgt);
            }
            // A spinning kit move (the Whirlwind): the body turned about its hips to the game's spin, less the clip's own turn;
            // by the pick's Align, turned instead so the drawn blade (the hold's +z off the right hand) lies on the game's tip,
            // which crosses the facing on each of the rules' pulses (a near-upright blade has no bearing: the spin's turn).
            if(covered&&pick.Spin&&VisualMotionBank.Clips.TryGetValue(pick.Clip,out var spun))
            {
                float own=(NativeVisualMotionClips.Yaw(spun,pick.Time)-NativeVisualMotionClips.Yaw(spun,0))*Mathf.Rad2Deg,turn=Mathf.DeltaAngle(0,-pick.Turn*Mathf.Rad2Deg-own);
                if(pick.Align>0&&m.HasHold)
                {
                    var blade=Quaternion.Inverse(body.Root.transform.rotation)*(m.RightHand.rotation*m.HoldRotation*Vector3.forward);float flat=new Vector2(blade.x,blade.z).magnitude;
                    float onTip=Mathf.DeltaAngle(Mathf.Atan2(blade.x,blade.z)*Mathf.Rad2Deg,-pick.Tip*Mathf.Rad2Deg);
                    turn+=Mathf.DeltaAngle(turn,onTip)*pick.Align*Mathf.Clamp01((flat-.3f)/.4f);
                }
                m.BodyHips.RotateAround(m.BodyHips.position,body.Root.transform.up,turn*wgt);
            }
            // The contact guard: the body's lowest point (each contact bone less its clearance) on the pilot's floor point, its
            // feet's place (the pack's character stands a little lower on its feet than the body; its jumps, falls and plunges
            // carry their own lift, which the game's own height already gives). Running, or a sprint attack's leaping run (the
            // pick's Flight), only kept out of the floor (the stride's flight). A switch between the two (a jump off a run's
            // stride, a landing into one) carries the difference off over about a quarter second instead of dropping or lifting
            // the body in one frame (measured: 27-37 cm); it never leaves the body under the floor.
            {
                var origin=body.Root.transform;float deepest=float.MaxValue;
                for(int f=0;f<m.Contacts.Length;f++)if(m.Contacts[f]!=null)deepest=Mathf.Min(deepest,Vector3.Dot(m.Contacts[f].position-origin.position,origin.up)-m.Clearance[f]);
                if(deepest<float.MaxValue)
                {
                    bool pin=!(covered&&(pick.Locomotion||pick.Flight));float goal=pin?deepest:Mathf.Min(deepest,0);
                    if(m.GuardSeen&&pin!=m.GuardPin){m.GuardCarry+=m.GuardApplied-goal;m.GuardCarryVel=0;}
                    m.GuardPin=pin;m.GuardSeen=true;Spring(ref m.GuardCarry,ref m.GuardCarryVel,GuardOmega,dt);
                    float applied=m.GuardApplied=Mathf.Min(goal+m.GuardCarry,deepest);
                    m.BodyHips.position-=origin.up*(applied*wgt);
                }
            }
            // The feet planted on the floor: on the ground, in anything but a flight, a spin, a roll, a knockdown or a kit move,
            // and outside the stride only while the body barely travels (a strike's lunge slides as the rules move it).
            {
                bool grounded=covered&&a.Grounded&&!pick.Flight&&!pick.Spin&&!a.Rolling(time)&&!NativeSpecialPose.Down(a,time)&&a.JuggleUntil<=time&&!MeleeKitMotion.Clock(a,time,out _);
                float plant=grounded?pick.Locomotion?1:1-Mathf.Clamp01((speed-1f)/1.5f):0;
                VisualMotionFeet(m,body.Root.transform,plant,pick.Locomotion,dt,wgt);
            }
            // The sword's run: the arm swept back, the blade trailing past the hip, the chest leaning in (NativeSwordTrail). Only
            // the locomotion blend carries it, so a strike, block, roll, jump or reaction takes it away as it comes in.
            m.Trail=Mathf.MoveTowards(m.Trail,covered&&pick.Locomotion&&m.HasHold?NativeSwordTrail.Share(m.Set.Spec,local.x,local.z):0,dt/NativeSwordTrail.Ease);
            if(m.Trail>0)VisualSwordTrail(m,body.Root.transform,m.Trail*m.LocoShare*wgt,speed,dt);else m.TrailMean=m.TrailTwist=float.NaN;
            // The weapon on the model's right hand, the rig's weapon hand bone with it (what draws there follows the model).
            if(m.HasHold)
            {
                // On the hand as drawn, at any weight (a part-blended hand carries its weapon too).
                weaponHand.SetPositionAndRotation(m.RightHand.position+m.RightHand.rotation*m.HoldPosition,m.RightHand.rotation*m.HoldRotation);
                if(m.LeftHand!=null&&m.Set.Spec!="greatsword")offHand.SetPositionAndRotation(m.LeftHand.position+m.LeftHand.rotation*m.LeftHoldPosition,m.LeftHand.rotation*m.LeftHoldRotation);
                // A raised gun's barrel turned onto the aim (about its grip, a little): its rounds leave its muzzle. A raised bow's
                // likewise, about its riser: its arrow line on the aim.
                bool reloading=time>=a.ReloadAt&&time<a.ReloadUntil;
                var aimed=m.Set.Gun&&!reloading?weaponHand:m.Set.Pull!=null?offHand:null;
                if(aimed!=null&&m.Upper>0)
                {
                    var aim=Quaternion.Euler(-a.Pitch,a.Yaw,0)*Vector3.forward;var turn=Quaternion.FromToRotation(aimed.rotation*Vector3.forward,aim);
                    turn.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;
                    aimed.rotation=Quaternion.AngleAxis(Mathf.Clamp(angle,-AimTurnLimit,AimTurnLimit)*m.Upper*wgt,axis)*aimed.rotation;
                }
                // A lowered gun (sprinting): the muzzle tipped down about the grip (the low ready carry; owner 2026-10-24).
                if(m.Set.Gun&&!reloading&&m.Upper<1)weaponHand.rotation=Quaternion.AngleAxis(CarryTilt*(1-m.Upper)*wgt,weaponHand.rotation*Vector3.right)*weaponHand.rotation;
                // A two-handed weapon: the support fist onto its handle (the greatsword's behind the right fist, a long gun's
                // handguard), while the clip keeps it near; not while reloading.
                bool longGun=body.Held!=null&&body.Held.Supported&&(m.Set.Spec=="rifle"||m.Set.Spec=="machinegun");
                if((m.Set.Spec=="greatsword"||longGun)&&m.LeftHand!=null&&m.LeftArm!=null&&m.LeftForearm!=null)
                {
                    Vector3 grip=weaponHand.position+weaponHand.rotation*(longGun?body.Held.Support:new Vector3(0,0,SupportAlong));
                    Vector3 fist=m.LeftFist!=null?Vector3.Lerp(m.LeftHand.position,m.LeftFist.position,.6f):m.LeftHand.position;
                    float far=(fist-grip).magnitude,hold=1-Mathf.Clamp01((far-SupportReach)/(SupportLetGo-SupportReach));m.SupportGap=far;
                    m.Support=Mathf.MoveTowards(m.Support,reloading?0:hold,dt/SupportEase);
                    if(m.Support>0){var turnHand=m.LeftHand.rotation;var origin=body.Root.transform;ArmReachTo(m.LeftArm,m.LeftForearm,m.LeftHand,m.LeftHand.position+(grip-fist)*m.Support*wgt,-origin.up-origin.right);m.LeftHand.rotation=turnHand;}
                }
            }
        }
    }
}
