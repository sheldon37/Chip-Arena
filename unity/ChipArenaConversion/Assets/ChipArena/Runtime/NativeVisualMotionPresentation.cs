using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace ChipArena.UnityConversion
{
    // The visual body's own animation (Core NativeVisualMotion; docs/BOZO-INTEGRATION.md): for a weapon whose set has moved over,
    // the model plays the animation pack's clips whole, retimed to the rules, instead of the game's pose retargeted. A second
    // copy of the skeleton (the motion proxy) plays every clip of the set in one manually evaluated mixer: the locomotion
    // blend by the body's travel (the stride's weights and rate) or one clip at its picked time, cross-faded. Its pose then
    // goes onto the body, every bone, by the motion's weight (eased in and out, so a state the set does not cover hands back
    // to the game's pose smoothly). The weapon rides the model's right hand: the hand-to-weapon offset the game pose's hold
    // gives (the fist on the handle) is kept, and the rig's weapon hand bone moves with it, so the model, its trails, blood
    // and effects follow the model's blade; a two-handed weapon's support hand reaches its handle. Presentation only.
    public sealed partial class ArenaPresentation
    {
        private sealed class VisualMotion
        {
            public NativeVisualMotion.Set Set; public GameObject Proxy; public Animator Animator; public PlayableGraph Graph; public AnimationMixerPlayable Mixer;
            public string[] Names; public AnimationClipPlayable[] Plays; public float[] Weights, Lengths, Hits; public readonly Dictionary<string,int> Index=new Dictionary<string,int>();
            public int[] Loco; public readonly float[] LocoWeights=new float[NativeVisualStride.Clips],SpeedX=new float[NativeVisualStride.Clips],SpeedZ=new float[NativeVisualStride.Clips],LocoLength=new float[NativeVisualStride.Clips];
            public float Phase, Weight, LocoShare, Time=float.NaN; public Vector3 LastAt, Velocity;
            public Transform[] ProxyBones, BodyBones; public Transform BodyHips, ProxyHips, RightHand, LeftHand, LeftArm, LeftForearm, LeftFist;
            // The feet's ankles and toes on the body and their rest heights over the floor (the floor guard), and the support
            // fist's eased weight.
            public readonly Transform[] Feet=new Transform[4]; public readonly float[] FeetRest=new float[4]; public float Support, SupportGap;
            public readonly NativeVisualMotion.Memory Memory=new NativeVisualMotion.Memory();
            public bool HasHold; public Vector3 HoldPosition; public Quaternion HoldRotation=Quaternion.identity;
            public Clips Book;
        }
        private sealed class Clips : NativeVisualMotion.IClips
        {
            public VisualMotion M;
            public bool Has(string clip)=>clip!=null&&M.Index.ContainsKey(clip);
            public float Length(string clip)=>M.Index.TryGetValue(clip,out int i)?M.Lengths[i]:1;
            public float Hit(string clip)=>M.Index.TryGetValue(clip,out int i)?M.Hits[i]:.5f;
        }
        // The two-handed support fist's place on the handle (the weapon frame, metres along the blade: behind the right fist).
        private const float SupportAlong=-.10f, SupportReach=.22f, SupportLetGo=.40f, SupportEase=.12f, MotionEase=.15f;
        // Why the last body could not play its set (null: it could): for the local checks.
        private static string visualMotionWhy;

        // A body's motion for `spec` (made once per set the body plays; null without the set or its clips in the bake).
        private static VisualMotion VisualMotionFor(VisualBody body,string spec)
        {
            var set=NativeVisualMotion.For(spec);if(set==null)return null;
            if(body.Motion!=null&&body.Motion.Set==set)return body.Motion;
            VisualMotionRelease(body);body.Motion=null;
            var skeleton=body.Root.transform.Find("VisualSkeleton");var source=skeleton!=null?skeleton.GetComponent<Animator>():null;
            if(source==null||source.avatar==null||!source.avatar.isHuman||source.runtimeAnimatorController==null){visualMotionWhy="no humanoid animator or clips";return null;}
            var byName=new Dictionary<string,AnimationClip>();foreach(var c in source.runtimeAnimatorController.animationClips)if(c!=null&&!byName.ContainsKey(c.name))byName[c.name]=c;
            var names=new List<string>();foreach(var n in set.All)if(byName.ContainsKey(n))names.Add(n);
            if(!names.Contains(set.Idle)){visualMotionWhy="no "+set.Idle+" among "+byName.Count+" clips";return null;}
            var proxy=Instantiate(skeleton.gameObject,body.Root.transform,false);proxy.name="Visual motion";
            foreach(var r in proxy.GetComponentsInChildren<Renderer>(true))Destroy(r);
            var animator=proxy.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.enabled=true;
            var m=new VisualMotion{Set=set,Proxy=proxy,Animator=animator,Names=names.ToArray()};m.Book=new Clips{M=m};
            m.Plays=new AnimationClipPlayable[names.Count];m.Weights=new float[names.Count];m.Lengths=new float[names.Count];m.Hits=new float[names.Count];
            m.Graph=PlayableGraph.Create("Visual motion");m.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            m.Mixer=AnimationMixerPlayable.Create(m.Graph,names.Count);
            var output=AnimationPlayableOutput.Create(m.Graph,"motion",animator);output.SetSourcePlayable(m.Mixer);
            for(int i=0;i<names.Count;i++)
            {
                var clip=byName[names[i]];var p=AnimationClipPlayable.Create(m.Graph,clip);p.SetApplyFootIK(true);m.Graph.Connect(p,0,m.Mixer,i);m.Plays[i]=p;
                m.Index[names[i]]=i;m.Lengths[i]=Mathf.Max(.05f,clip.length);m.Hits[i]=clip.length*.5f;
                foreach(var e in clip.events)if(e.functionName=="Hit"){m.Hits[i]=e.time;break;}
            }
            float scale=animator.humanScale;m.Loco=new int[NativeVisualStride.Clips];
            for(int k=0;k<m.Loco.Length;k++)
            {
                m.Loco[k]=m.Index.TryGetValue(set.Locomotion[k],out int i)?i:-1;
                if(m.Loco[k]>=0){var c=byName[set.Locomotion[k]];m.SpeedX[k]=c.averageSpeed.x*scale;m.SpeedZ[k]=c.averageSpeed.z*scale;m.LocoLength[k]=Mathf.Max(.1f,c.length);}
            }
            // The two skeletons, bone for bone (the proxy is the skeleton's copy: the same order).
            m.ProxyBones=proxy.GetComponentsInChildren<Transform>(true);m.BodyBones=skeleton.GetComponentsInChildren<Transform>(true);
            if(m.ProxyBones.Length!=m.BodyBones.Length){visualMotionWhy="proxy "+m.ProxyBones.Length+" bones, body "+m.BodyBones.Length;m.Graph.Destroy();Destroy(proxy);return null;}
            m.ProxyHips=animator.GetBoneTransform(HumanBodyBones.Hips);m.BodyHips=m.ProxyHips!=null?Twin(proxy.transform,skeleton,m.ProxyHips):null;
            Transform Body(HumanBodyBones b){var t=animator.GetBoneTransform(b);return t!=null?Twin(proxy.transform,skeleton,t):null;}
            m.RightHand=Body(HumanBodyBones.RightHand);m.LeftHand=Body(HumanBodyBones.LeftHand);m.LeftArm=Body(HumanBodyBones.LeftUpperArm);m.LeftForearm=Body(HumanBodyBones.LeftLowerArm);
            m.LeftFist=Body(HumanBodyBones.LeftMiddleProximal);
            // Their rest heights from the bake itself (the body here already wears this frame's pose).
            var feet=new[]{HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};var rest=visualBodyPrefab!=null?visualBodyPrefab.transform.Find("VisualSkeleton"):null;
            for(int f=0;f<feet.Length;f++)
            {
                var pb=animator.GetBoneTransform(feet[f]);m.Feet[f]=pb!=null?Twin(proxy.transform,skeleton,pb):null;
                var r=pb!=null&&rest!=null?Twin(proxy.transform,rest,pb):null;m.FeetRest[f]=r!=null?r.position.y-visualBodyPrefab.transform.position.y:0;
            }
            if(m.BodyHips==null||m.RightHand==null){visualMotionWhy="no hips or right hand";m.Graph.Destroy();Destroy(proxy);return null;}
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
        private static void VisualMotionRelease(VisualBody body)
        {
            var m=body?.Motion;if(m==null)return;if(m.Graph.IsValid())m.Graph.Destroy();if(m.Proxy!=null)Destroy(m.Proxy);body.Motion=null;
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
            // The hand-to-weapon offset of the game pose's hold (its fist on the handle), kept from its first seated frame.
            var weaponHand=rig.Bones[CapturedRig.RHand];
            if(!m.HasHold&&body.Held!=null){var inv=Quaternion.Inverse(m.RightHand.rotation);m.HoldPosition=inv*(weaponHand.position-m.RightHand.position);m.HoldRotation=inv*weaponHand.rotation;m.HasHold=true;}
            var local=Quaternion.Inverse(body.Root.transform.rotation)*velocity;float speed=new Vector2(local.x,local.z).magnitude;
            bool moving=a.Grounded&&speed>NativeVisualStride.Moving;
            bool covered=NativeVisualMotion.Choose(m.Set,a,time,m.Memory,m.Book,moving,out var pick);
            m.Weight=Mathf.MoveTowards(m.Weight,covered?1:0,dt/MotionEase);
            if(m.Weight<=0&&!covered)return;
            // The mixer: the picked clip (or the locomotion blend) fades in over the pick's fade, the rest fade out; the shares
            // are then normalised. Locomotion's clips take its share by the travel weights, all at the shared phase.
            int target=-1;
            if(covered&&!pick.Locomotion&&m.Index.TryGetValue(pick.Clip,out int ti)){target=ti;m.Plays[ti].SetTime(Mathf.Max(0,pick.Time));}
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
            for(int i=0;i<m.Plays.Length;i++)m.Mixer.SetInputWeight(i,m.Weights[i]/sum);
            for(int k=0;k<m.Loco.Length;k++)
            {
                int i=m.Loco[k];float w=m.LocoWeights[k]*m.LocoShare/sum;if(i<0||w<=0)continue;
                m.Mixer.SetInputWeight(i,m.Mixer.GetInputWeight(i)+w);m.Plays[i].SetTime(m.Phase*m.LocoLength[k]);
            }
            m.Graph.Evaluate(0);
            // The proxy's pose onto the body by the motion's weight.
            float wgt=m.Weight;
            for(int i=0;i<m.BodyBones.Length;i++)
            {
                var b=m.BodyBones[i];var p=m.ProxyBones[i];
                b.localRotation=wgt>=.999f?p.localRotation:Quaternion.Slerp(b.localRotation,p.localRotation,wgt);
                if(b==m.BodyHips)b.localPosition=Vector3.Lerp(b.localPosition,p.localPosition,wgt);
            }
            // The floor guard: no ankle or toe under its rest height over the pilot's floor point (the pack's character stands
            // a little lower on its feet than the body), the whole body lifted by the deepest.
            {
                var origin=body.Root.transform;float deepest=0;
                for(int f=0;f<m.Feet.Length;f++)if(m.Feet[f]!=null)deepest=Mathf.Min(deepest,Vector3.Dot(m.Feet[f].position-origin.position,origin.up)-m.FeetRest[f]);
                if(deepest<0)m.BodyHips.position-=origin.up*deepest;
            }
            // The weapon on the model's right hand, the rig's weapon hand bone with it (what draws there follows the model).
            if(m.HasHold)
            {
                // On the hand as drawn, at any weight (a part-blended hand carries its weapon too).
                weaponHand.SetPositionAndRotation(m.RightHand.position+m.RightHand.rotation*m.HoldPosition,m.RightHand.rotation*m.HoldRotation);
                // A two-handed weapon: the support fist onto its handle, behind the right, while the clip keeps it near.
                if(m.Set.Spec=="greatsword"&&m.LeftHand!=null&&m.LeftArm!=null&&m.LeftForearm!=null)
                {
                    Vector3 grip=weaponHand.position+weaponHand.rotation*new Vector3(0,0,SupportAlong);
                    Vector3 fist=m.LeftFist!=null?Vector3.Lerp(m.LeftHand.position,m.LeftFist.position,.6f):m.LeftHand.position;
                    float far=(fist-grip).magnitude,hold=1-Mathf.Clamp01((far-SupportReach)/(SupportLetGo-SupportReach));m.SupportGap=far;
                    m.Support=Mathf.MoveTowards(m.Support,hold,dt/SupportEase);
                    if(m.Support>0){var turnHand=m.LeftHand.rotation;var origin=body.Root.transform;ArmReachTo(m.LeftArm,m.LeftForearm,m.LeftHand,m.LeftHand.position+(grip-fist)*m.Support*wgt,-origin.up-origin.right);m.LeftHand.rotation=turnHand;}
                }
            }
        }
    }
}
