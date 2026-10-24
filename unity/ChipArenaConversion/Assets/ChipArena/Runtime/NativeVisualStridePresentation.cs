using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace ChipArena.UnityConversion
{
    // The visual body's stride (Core NativeVisualStride; docs/BOZO-INTEGRATION.md, animation revamp): while a pilot runs or
    // walks on the ground, the visual body's legs and hip height come from the animation pack's own humanoid locomotion clips,
    // which the bake carries on the skeleton's (disabled) Animator with the body rig's avatar. A copy of the skeleton without
    // renderers (the proxy) plays them in a manually evaluated graph, mixed by the body's travel and played at the rate that
    // matches its speed; after the game pose is applied, each leg bone takes the proxy's turn (the two skeletons share their
    // root) and the hips its height, by the stride weight. The upper body rides the hips, so the weapon frames move with
    // them; aim, holds and every arm layer are the game's. Off in the air, rolling, attacking, stunned, juggled, in a special
    // or a hit reaction, and standing (the game's stance). Presentation only.
    public sealed partial class ArenaPresentation
    {
        private sealed class VisualStride
        {
            public GameObject Proxy; public PlayableGraph Graph; public AnimationMixerPlayable Mixer; public AnimationClipPlayable[] Clips;
            public readonly float[] SpeedX=new float[NativeVisualStride.Clips],SpeedZ=new float[NativeVisualStride.Clips],Length=new float[NativeVisualStride.Clips],Weights=new float[NativeVisualStride.Clips];
            public Transform ProxyHips, BodyHips; public readonly Transform[] ProxyLegs=new Transform[8],BodyLegs=new Transform[8];
            public float Phase, Weight, Time=float.NaN; public Vector3 LastAt, Velocity;
            // The foot lock (per foot, left then right): locked, where, its weight, and the ankle's rest height over the floor.
            public readonly bool[] Locked=new bool[2]; public readonly Vector3[] Lock=new Vector3[2]; public readonly float[] LockWeight=new float[2],Rest=new float[2];
        }
        // A foot locks as the clip brings its ankle within StrideDown of its rest height and lets go past StrideUp (m), or once
        // the leg would reach StrideReach from where the clip has it; it eases back to the clip over StrideEase seconds.
        private const float StrideDown=.025f, StrideUp=.05f, StrideReach=.3f, StrideEase=.1f;
        // --ca-no-stride: the visual body keeps the game gait's legs (read once).
        public const string NoStrideArgument="--ca-no-stride";private static int strideState;
        private static bool StrideOff{get{if(strideState==0)strideState=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),NoStrideArgument)>=0?-1:1;return strideState<0;}}
        private static readonly HumanBodyBones[] StrideLegs={HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};

        // CreateVisualBody, once its renderers are read: the proxy and its graph, if the bake carries the clips (null otherwise).
        private static VisualStride VisualStrideOf(Transform root,Transform skeleton)
        {
            var source=skeleton.GetComponent<Animator>();if(source==null||source.avatar==null||!source.avatar.isHuman||source.runtimeAnimatorController==null)return null;
            var clips=new AnimationClip[NativeVisualStride.Clips];
            foreach(var c in source.runtimeAnimatorController.animationClips)for(int i=0;i<clips.Length;i++)if(c!=null&&c.name==NativeVisualStride.Names[i])clips[i]=c;
            foreach(var c in clips)if(c==null)return null;
            var proxy=Instantiate(skeleton.gameObject,root,false);proxy.name="Visual stride";
            foreach(var r in proxy.GetComponentsInChildren<Renderer>(true))Destroy(r);
            var animator=proxy.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.enabled=true;
            var s=new VisualStride{Proxy=proxy,Clips=new AnimationClipPlayable[clips.Length]};
            s.ProxyHips=animator.GetBoneTransform(HumanBodyBones.Hips);if(s.ProxyHips==null){Destroy(proxy);return null;}
            s.BodyHips=Twin(proxy.transform,skeleton,s.ProxyHips);
            for(int k=0;k<StrideLegs.Length;k++){var t=animator.GetBoneTransform(StrideLegs[k]);s.ProxyLegs[k]=t;s.BodyLegs[k]=t!=null?Twin(proxy.transform,skeleton,t):null;}
            if(s.BodyHips==null){Destroy(proxy);return null;}
            for(int f=0;f<2;f++){var foot=s.BodyLegs[f*4+2];s.Rest[f]=foot!=null?Vector3.Dot(foot.position-root.position,root.up):0;}
            s.Graph=PlayableGraph.Create("Visual stride");s.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            s.Mixer=AnimationMixerPlayable.Create(s.Graph,clips.Length);
            var output=AnimationPlayableOutput.Create(s.Graph,"stride",animator);output.SetSourcePlayable(s.Mixer);
            float scale=animator.humanScale;
            for(int i=0;i<clips.Length;i++)
            {
                var p=AnimationClipPlayable.Create(s.Graph,clips[i]);p.SetApplyFootIK(true);s.Graph.Connect(p,0,s.Mixer,i);s.Clips[i]=p;
                s.SpeedX[i]=clips[i].averageSpeed.x*scale;s.SpeedZ[i]=clips[i].averageSpeed.z*scale;s.Length[i]=Mathf.Max(.1f,clips[i].length);
            }
            return s;
        }
        // The body skeleton's bone at the proxy bone's place (the proxy is the skeleton's copy: the same names and paths).
        private static Transform Twin(Transform proxyRoot,Transform skeleton,Transform bone)
        {
            string path=bone.name;for(var t=bone.parent;t!=null&&t!=proxyRoot;t=t.parent)path=t.name+"/"+path;
            return skeleton.Find(path);
        }
        // A two-bone reach: the knee kept on its side of the hip-to-target line (the clip's), the ankle on the target (within reach).
        private static void StrideReachTo(Transform hip,Transform knee,Transform foot,Vector3 target)
        {
            Vector3 a=hip.position,b=knee.position,c=foot.position,toward=target-a;float upper=(b-a).magnitude,lower=(c-b).magnitude;
            if(toward.sqrMagnitude<1e-8f||upper<1e-4f||lower<1e-4f)return;
            float d=Mathf.Clamp(toward.magnitude,.01f,(upper+lower)*.999f);var dir=toward.normalized;
            var side=(b-a)-Vector3.Dot(b-a,dir)*dir;if(side.sqrMagnitude<1e-10f)return;side.Normalize();
            float along=(upper*upper-lower*lower+d*d)/(2*d),high=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            hip.rotation=Quaternion.FromToRotation(b-a,dir*along+side*high)*hip.rotation;
            b=knee.position;c=foot.position;knee.rotation=Quaternion.FromToRotation(c-b,a+dir*d-b)*knee.rotation;
        }
        // PilotFootfall: the drawn feet's plants (1 left, 2 right) while pilot `id`'s stride carries its legs this frame.
        private bool VisualStrideContacts(int id,out int contacts)
        {
            contacts=0;if(!rigs.TryGetValue(id,out var rig)||!visualBodies.TryGetValue(rig,out var body)||body.PosedFrame!=Time.frameCount)return false;
            var s=body.Stride;if(s==null||s.Weight<.5f)return false;
            contacts=(s.Locked[0]?1:0)|(s.Locked[1]?2:0);return true;
        }
        private static void VisualStrideRelease(VisualBody body){if(body?.Stride!=null&&body.Stride.Graph.IsValid())body.Stride.Graph.Destroy();}

        // VisualBodyPose, once the game pose is on the body and the rig's hands on the weapon frames: the stride's legs and hips.
        private void VisualStridePose(VisualBody body,CapturedAnimation.Player rig,Actor a,float time,bool roll,bool juggle)
        {
            var s=body.Stride;if(s==null||s.Proxy==null||!s.Proxy.activeInHierarchy||StrideOff)return;
            Vector3 at=rig.Root.transform.position;float dt=float.IsNaN(s.Time)?0:Mathf.Clamp(time-s.Time,0,.1f);s.Time=time;
            if(dt>0){var v=(at-s.LastAt)/dt;if(v.sqrMagnitude>400)v=s.Velocity;s.Velocity+=(v-s.Velocity)*(1-Mathf.Exp(-dt*10));}
            s.LastAt=at;
            var local=Quaternion.Inverse(body.Root.transform.rotation)*s.Velocity;float speed=new Vector2(local.x,local.z).magnitude;
            bool free=a.Grounded&&!roll&&!juggle&&!a.Attacking(time)&&time>=a.StunUntil&&!(time>=a.SpecialAt&&time<a.SpecialUntil)&&!(time>=a.ReactionAt&&time<a.ReactionUntil);
            float goal=free?NativeVisualStride.LegWeight(speed):0;s.Weight+=(goal-s.Weight)*(dt>0?1-Mathf.Exp(-dt*12):0);
            if(s.Weight<.002f){s.Weight=goal>0?s.Weight:0;s.Locked[0]=s.Locked[1]=false;s.LockWeight[0]=s.LockWeight[1]=0;return;}
            float rate=NativeVisualStride.Weights(local.x,local.z,s.SpeedX,s.SpeedZ,s.Weights);
            s.Phase=Mathf.Repeat(s.Phase+dt*rate/NativeVisualStride.Cycle(s.Weights,s.Length),1);
            for(int i=0;i<s.Clips.Length;i++){s.Mixer.SetInputWeight(i,s.Weights[i]);s.Clips[i].SetTime(s.Phase*s.Length[i]);}
            s.Graph.Evaluate(0);
            // The hips to the clip's place over the root (its height and its sway: the feet are planted for it; the upper body
            // and the weapon frames ride them), the legs to its turns.
            Vector3 shift=(s.ProxyHips.position-s.BodyHips.position)*s.Weight;
            s.BodyHips.position+=shift;
            for(int k=0;k<s.BodyLegs.Length;k++){var b=s.BodyLegs[k];if(b!=null)b.rotation=Quaternion.Slerp(b.rotation,s.ProxyLegs[k].rotation,s.Weight);}
            // The foot lock: a foot the clip puts down stays where it landed (its leg reaching it), easing back to the clip as it
            // lifts, so neither the clip's own slip nor the mix's slides it on the floor.
            Vector3 up=body.Root.transform.up,floor=body.Root.transform.position;
            for(int f=0;f<2;f++)
            {
                Transform hip=s.BodyLegs[f*4],knee=s.BodyLegs[f*4+1],foot=s.BodyLegs[f*4+2];if(hip==null||knee==null||foot==null)continue;
                float h=Vector3.Dot(s.ProxyLegs[f*4+2].position-floor,up)-s.Rest[f];
                if(!s.Locked[f]&&h<StrideDown){s.Locked[f]=true;s.Lock[f]=foot.position;s.LockWeight[f]=1;}
                else if(s.Locked[f]&&(h>StrideUp||(foot.position-s.Lock[f]).sqrMagnitude>StrideReach*StrideReach))s.Locked[f]=false;
                if(!s.Locked[f])s.LockWeight[f]=Mathf.MoveTowards(s.LockWeight[f],0,dt/StrideEase);
                // Held fully once the clips carry half the legs (a part-blended leg would otherwise drag its lock along).
                float w=s.LockWeight[f]*Mathf.Clamp01(s.Weight*2);if(w<.001f)continue;
                var turn=foot.rotation;StrideReachTo(hip,knee,foot,Vector3.Lerp(foot.position,s.Lock[f],w));foot.rotation=turn;
            }
            rig.Bones[CapturedRig.LHand].position+=shift;rig.Bones[CapturedRig.RHand].position+=shift;
        }
    }
}
