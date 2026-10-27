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
            public float Phase, Weight, LocoShare, Time=float.NaN; public Vector3 LastAt, Velocity;
            public Transform[] ProxyBones, BodyBones; public Transform BodyHips, ProxyHips, RightHand, LeftHand, LeftArm, LeftForearm, LeftFist, LeftIndex, LeftLittle;
            // The contact guard's bones on the body (ankles and toes, knees, hips, chest, head, hands) and each one's clearance
            // over the floor (the feet's rest heights, the rest a little), and the support fist's eased weight.
            public readonly Transform[] Contacts=new Transform[ContactBones.Length]; public readonly float[] Clearance=new float[ContactBones.Length]; public float Support, SupportGap;
            public readonly NativeVisualMotion.Memory Memory=new NativeVisualMotion.Memory();
            public bool HasHold; public Vector3 HoldPosition, LeftHoldPosition; public Quaternion HoldRotation=Quaternion.identity, LeftHoldRotation=Quaternion.identity;
            // The sword's running carry (Core NativeSwordTrail): its arm, fist, spine and head, the carry's eased share and the
            // clip's own arm swing's eased mean and the fist's last twist (NaN: not carrying).
            public Transform RightArm, RightForearm, RightFist, Spine, Head; public float Trail, TrailMean=float.NaN, TrailTwist=float.NaN;
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
            // The feet's clearances: their rest heights, from the bake itself (the body here already wears this frame's pose).
            var rest=visualBodyPrefab!=null?visualBodyPrefab.transform.Find("VisualSkeleton"):null;
            for(int f=0;f<ContactBones.Length;f++)
            {
                var pb=animator.GetBoneTransform(ContactBones[f]);m.Contacts[f]=pb!=null?Twin(proxy.transform,skeleton,pb):null;
                var r=pb!=null&&rest!=null&&ContactRadius[f]<0?Twin(proxy.transform,rest,pb):null;m.Clearance[f]=ContactRadius[f]>=0?ContactRadius[f]:r!=null?r.position.y-visualBodyPrefab.transform.position.y:0;
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
            var below=Quaternion.Inverse(m.RightForearm.rotation);
            float twist=NativeSwordTrail.Twist(CapturedAnimation.Num(below*goal),CapturedAnimation.Num((below*forearm).normalized))*Mathf.Rad2Deg;
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
        // The mix (body frame and muscles) onto the proxy.
        private static void VisualMotionApply(VisualMotion m)
        {
            m.Pose.bodyPosition=new Vector3(m.Mix[0],m.Mix[1],m.Mix[2]);m.Pose.bodyRotation=new Quaternion(m.Mix[3],m.Mix[4],m.Mix[5],m.Mix[6]);
            System.Array.Copy(m.Mix,NativeVisualMotionClips.Head,m.Pose.muscles,0,m.Pose.muscles.Length);
            m.Handler.SetHumanPose(ref m.Pose);
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
            if(m.Weight<=0&&!covered)return;
            // The mix: the picked clip (or the locomotion blend) fades in over the pick's fade, the rest fade out (each held at
            // its last time); their poses are averaged by weight. Locomotion's clips take its share by the travel weights, all
            // at the shared phase.
            int target=-1;
            if(covered&&!pick.Locomotion&&m.Index.TryGetValue(pick.Clip,out int ti)){target=ti;m.Times[ti]=Mathf.Max(0,pick.Time);}
            float step=dt/Mathf.Max(.02f,pick.Fade);
            for(int i=0;i<m.Weights.Length;i++)m.Weights[i]=Mathf.MoveTowards(m.Weights[i],i==target?1:0,step);
            m.LocoShare=Mathf.MoveTowards(m.LocoShare,covered&&pick.Locomotion?1:0,step);
            if(m.LocoShare>0)
            {
                float rate=NativeVisualStride.Weights(local.x,local.z,m.SpeedX,m.SpeedZ,m.LocoWeights);
                m.Phase=Mathf.Repeat(m.Phase+dt*rate/NativeVisualStride.Cycle(m.LocoWeights,m.LocoLength),1);
            }
            float sum=m.LocoShare;for(int i=0;i<m.Weights.Length;i++)sum+=m.Weights[i];
            if(sum<1e-4f){m.LocoShare=sum=1;}
            System.Array.Clear(m.Mix,0,m.Mix.Length);
            for(int i=0;i<m.Clips.Length;i++)NativeVisualMotionClips.Add(m.Clips[i],m.Times[i],m.Weights[i],m.Mix);
            for(int k=0;k<m.Loco.Length;k++){int i=m.Loco[k];if(i>=0)NativeVisualMotionClips.Add(m.Clips[i],m.Phase*m.LocoLength[k],m.LocoWeights[k]*m.LocoShare,m.Mix);}
            NativeVisualMotionClips.Finish(m.Mix,sum);
            if(covered&&pick.Spin)VisualSpinLegs(m,pick);
            // A gun's upper body (aimed by the pitch, its recoil, or its reload), a bow's (drawn, loosed) or a caster's (its cast),
            // over the legs' motion by its eased weight.
            // The arms are free while the body idles, moves, rises, falls or lands (not in a roll, strike, reaction or block).
            bool ready=covered&&(pick.Locomotion||pick.Clip==m.Set.Idle||pick.Clip==m.Set.Jump||pick.Clip==m.Set.Fall||pick.Clip==m.Set.Land);
            var up=default(NativeVisualMotion.Upper);bool layered=covered&&NativeVisualMotion.Layer(m.Set,a,time,VisualMotionBank,out up,ready);
            m.Upper=layered&&up.Raised&&up.Snap?1:Mathf.MoveTowards(m.Upper,layered&&up.Raised?1:0,dt/UpperEase);
            NativeVisualMotionClips.Clip ua=default;bool upper=layered&&m.Upper>0&&VisualMotionBank.Clips.TryGetValue(up.A,out ua);
            // A human pose places the body by its centre of mass: a raised upper body would swing the hips (and the legs with
            // them). The legs' motion is posed first and its hips kept.
            Vector3 hipsAt=default;Quaternion hipsTurn=default;
            if(upper)
            {
                VisualMotionApply(m);hipsAt=m.ProxyHips.localPosition;hipsTurn=m.ProxyHips.localRotation;
                System.Array.Clear(m.UpperMix,0,m.UpperMix.Length);NativeVisualMotionClips.Add(ua,up.TimeA,1-up.Mix,m.UpperMix);
                if(up.B!=null&&up.Mix>0&&VisualMotionBank.Clips.TryGetValue(up.B,out var ub))NativeVisualMotionClips.Add(ub,up.TimeB,up.Mix,m.UpperMix);
                NativeVisualMotionClips.Finish(m.UpperMix,1);var mask=UpperMuscles;
                for(int i=0;i<mask.Length&&NativeVisualMotionClips.Head+i<m.Mix.Length;i++)if(mask[i]){int k=NativeVisualMotionClips.Head+i;m.Mix[k]+=(m.UpperMix[k]-m.Mix[k])*m.Upper;}
            }
            VisualMotionApply(m);
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
            // pick's Flight), only kept out of the floor (the stride's flight).
            {
                var origin=body.Root.transform;float deepest=float.MaxValue;
                for(int f=0;f<m.Contacts.Length;f++)if(m.Contacts[f]!=null)deepest=Mathf.Min(deepest,Vector3.Dot(m.Contacts[f].position-origin.position,origin.up)-m.Clearance[f]);
                if(deepest<float.MaxValue&&(deepest<0||!(covered&&(pick.Locomotion||pick.Flight))))m.BodyHips.position-=origin.up*(deepest*wgt);
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
