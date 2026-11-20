#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Belvedere Camp's survivors on the pilot model in the DEV preview (Core NativeCampSurvivors; docs/expedition/BELVEDERE-CAMP.md "Survivors").
    // Editor and development builds only, the Developer walk only: never a live run. The nearest six within 30 m draw on the model, dressed
    // as their roles through the zombies' dressing (clean, a human skin, human eyes), posed from the camp's own motion bank
    // (Resources/ChipArenaCampMotion: local, never committed) and holding their props; the rest stay the bank's posed figures. Never
    // replicated: no collider, physics or wire.
    public sealed partial class ArenaPresentation
    {
        private sealed class CampSurvivor
        {
            public NativeCampSurvivors.Role Role;public VisualBody Body;public Transform Head;public Matrix4x4 HeadProp;public int PosedFrame=-10;public double PoseTime;public uint Seed;
        }
        private readonly Dictionary<string,CampSurvivor> campSurvivors=new Dictionary<string,CampSurvivor>(StringComparer.Ordinal);
        private static NativeIntroPack.Bank campMotion;private static int campMotionState;
        private readonly int[] campDrawn={-1,-1,-1,-1,-1,-1};
        private readonly NativeCampSurvivors.Scratch campScratch=new NativeCampSurvivors.Scratch();
        private readonly CapturedRig.Pose campPose=new CapturedRig.Pose();
        private readonly List<(NativeCamp.Post post,Vector3 at,float yaw)> campShown=new List<(NativeCamp.Post,Vector3,float)>();
        private System.Numerics.Vector3[] campAt=Array.Empty<System.Numerics.Vector3>();
        private int campMakesFrame=-1,campRescued=-1;private double campCheerAt=double.NaN,campCheerSeen=double.NaN;
        /// <summary>DEV: the survivors drawn on the model last frame (the walk's panel and the checks' probes).</summary>
        public int CampSkinnedCount{get;private set;}

        /// <summary>The camp's motion bank (read once; null without it: every survivor stays the bank's posed figure).</summary>
        private static NativeIntroPack.Bank CampMotion()
        {
            if(campMotionState!=0)return campMotion;campMotionState=-1;
            var asset=Resources.Load<TextAsset>(NativeCampSurvivors.MotionResource);if(asset==null)return null;
            try{campMotion=NativeIntroPack.Read(asset.bytes);campMotionState=1;Debug.Log("Belvedere Camp: survivors' motion bank "+campMotion.Clips.Count+" clips");}
            catch(Exception e){Debug.LogWarning("Belvedere Camp: the survivors' motion bank failed to read: "+e.Message);campMotion=null;}
            Resources.UnloadAsset(asset);return campMotion;
        }

        /// <summary>From CampScenery while the preview shows (before the camp draws: its figures skip the posts drawn here).</summary>
        private void CampSurvivorsFrame(NativeArena arena,float now,int rescued)
        {
            camp.Skinned.Clear();CampSkinnedCount=0;
            var bank=CampMotion();var eye=WorldEye;
            if(bank==null||eye==null||!VisualBodiesOn||visualBodyPrefab==null){CampSurvivorsHide();return;}
            // the posts shown and where they are; a rescue joining (U) or the cheer key (Y) brings the civilians' cheer
            campShown.Clear();foreach(var (p,at,yaw) in NativeCamp.PreviewPosts(rescued))campShown.Add((p,new Vector3(at.x,at.y,at.z),yaw));
            if(campRescued>=0&&rescued>campRescued)campCheerAt=now;campRescued=rescued;
            if(!double.IsNaN(NativeCampWalk.CheerAt)&&NativeCampWalk.CheerAt!=campCheerSeen){campCheerSeen=NativeCampWalk.CheerAt;campCheerAt=now;}
            if(campAt.Length!=NativeCamp.Posts.Length)campAt=new System.Numerics.Vector3[NativeCamp.Posts.Length];
            for(int i=0;i<campAt.Length;i++)campAt[i]=new System.Numerics.Vector3(float.NaN,0,0);
            foreach(var s in campShown){int i=Array.IndexOf(NativeCamp.Posts,s.post);if(i>=0)campAt[i]=new System.Numerics.Vector3(s.at.x,s.at.y,s.at.z);}
            var e=eye.transform.position;NativeCampSurvivors.Pick(new System.Numerics.Vector3(e.x,e.y,e.z),campAt,campDrawn);
            foreach(var k in campDrawn)
            {
                if(k<0)continue;var post=NativeCamp.Posts[k];var role=NativeCampSurvivors.Of(post.Id);if(role==null)continue;
                var s=CampSurvivorOf(role);if(s?.Body?.Root==null)continue;
                float yaw=0;Vector3 at=default;foreach(var c in campShown)if(c.post.Id==post.Id){yaw=c.yaw;at=c.at;}
                string react=null;double reactAt=double.NaN;
                if(role.Cheers&&!double.IsNaN(campCheerAt)){react=NativeCampSurvivors.Reaction(s.Seed);reactAt=campCheerAt+(s.Seed%7)*.12;}
                if(!NativeCampSurvivors.Sample(role,bank,now,s.Seed,campPose,campScratch,out string prop,react,reactAt))continue;
                var t=s.Body.Root.transform;var turn=Quaternion.Euler(0,yaw,0);
                // a sitter sits back on the bench's seat (the rigid figure's hips, 5 cm behind the post's line)
                t.SetPositionAndRotation(at+turn*new Vector3(0,0,role.Stance==NativeCampSurvivors.Stance.Seated?-.05f:0),turn);
                if(!s.Body.Root.activeSelf)s.Body.Root.SetActive(true);
                if(s.PosedFrame<Time.frameCount-1)s.Body.Binding.State.Forget();
                double clock=Time.unscaledTimeAsDouble;s.Body.Binding.State.Dt=(float)Math.Max(1e-4,Math.Min(.1,clock-s.PoseTime));s.PoseTime=clock;
                bool left=prop!=null&&prop.StartsWith("l:",StringComparison.Ordinal);
                s.Body.Binding.Apply(campPose,new NativeVisualRig.Grasp{Exact=1,RightGrip=prop!=null&&!left?.85f:0,LeftGrip=left?.85f:0},false,false);
                s.PosedFrame=Time.frameCount;camp.Skinned.Add(post.Id);CampSkinnedCount++;
                // what it holds: the beat's prop in its fist (the binding's weapon frames), a worn one on its head
                var root=t.localToWorldMatrix;
                if(prop!=null)
                {
                    var key=left?prop.Substring(2):prop;Vector3 p;Quaternion q;
                    if(left)s.Body.Binding.LeftWeaponFrame(campPose,out p,out q);else s.Body.Binding.WeaponFrame(campPose,out p,out q);
                    var hold=NativeCampSurvivors.Hold(key,left);
                    camp.DrawProp(key,root*Matrix4x4.TRS(p,q,Vector3.one)*Matrix4x4.TRS(new Vector3(hold.X,hold.Y,hold.Z),Quaternion.Euler(hold.Pitch,hold.Yaw,hold.Roll),Vector3.one));
                }
                if(role.Head!=null&&s.Head!=null)camp.DrawProp(role.Head,s.Head.localToWorldMatrix*s.HeadProp);
            }
            // the bodies not drawn this frame go hidden (kept for when the eye comes back)
            foreach(var s in campSurvivors.Values)if(s.Body?.Root!=null&&s.PosedFrame!=Time.frameCount&&s.Body.Root.activeSelf)s.Body.Root.SetActive(false);
        }
        /// <summary>The preview off, or a frame without the bank: every survivor's body hidden, every figure drawn.</summary>
        private void CampSurvivorsHide()
        {
            camp?.Skinned.Clear();CampSkinnedCount=0;for(int k=0;k<campDrawn.Length;k++)campDrawn[k]=-1;
            foreach(var s in campSurvivors.Values)if(s.Body?.Root!=null&&s.Body.Root.activeSelf)s.Body.Root.SetActive(false);
        }
        /// <summary>The walk closed (the camp's map gone): the survivors' bodies let go.</summary>
        private void CampSurvivorsRelease()
        {
            if(campSurvivors.Count==0)return;
            foreach(var s in campSurvivors.Values)if(s.Body?.Root!=null)VisualBodyGone(s.Body.Root);
            campSurvivors.Clear();for(int k=0;k<campDrawn.Length;k++)campDrawn[k]=-1;campRescued=-1;campCheerAt=double.NaN;
        }
        // A role's body: made once (one a frame at most, about 8 ms), dressed as its role, kept for the walk.
        private CampSurvivor CampSurvivorOf(NativeCampSurvivors.Role role)
        {
            if(campSurvivors.TryGetValue(role.Post,out var s))return s;
            if(campMakesFrame==Time.frameCount)return null;campMakesFrame=Time.frameCount;
            s=CampSurvivorMake(role);if(s!=null)campSurvivors[role.Post]=s;return s;
        }
        private CampSurvivor CampSurvivorMake(NativeCampSurvivors.Role role)
        {
            var root=VisualCorpseInstance();if(root==null)return null;
            root.transform.SetParent(null,false);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;root.SetActive(true);root.name="Camp survivor "+role.Post;
            var skeleton=root.transform.Find("VisualSkeleton");if(skeleton==null){VisualBodyGone(root);return null;}
            var v=role.Look;var worn=ZombieDress(root,v);ZombieGarmentsMake(root,v);float lift=ZombieLift(worn);if(lift!=0)skeleton.localPosition+=new Vector3(0,lift,0);
            ZombieBuild(skeleton,v.Build);
            // human eyes: the dressing gives every variant the zombies' blank ones (_ZombieEye w on, the outline off)
            zombieBlock??=new MaterialPropertyBlock();
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if(!ZombieEyePart(r))continue;var m=r.sharedMaterial;r.GetPropertyBlock(zombieBlock);
                zombieBlock.SetVector(ZombieEyeId,Vector4.zero);zombieBlock.SetFloat(UseOutlineId,m!=null&&m.HasProperty(UseOutlineId)?m.GetFloat(UseOutlineId):1);r.SetPropertyBlock(zombieBlock);
            }
            var body=new VisualBody{Root=root,Binding=new NativeVisualRigBinding(root.transform,skeleton),Block=new MaterialPropertyBlock(),Look=v.Id,Lift=lift};
            body.All=VisualWorn(root);body.Cloth=Array.Empty<Renderer>();
            if(v.Gear.Length>0)body.All=ZombieGear(root,skeleton,v,body.All);
            foreach(var r in body.All){if(r==null)continue;r.shadowCastingMode=ShadowCastingMode.On;if(r is SkinnedMeshRenderer sm)sm.updateWhenOffscreen=false;}
            var s=new CampSurvivor{Role=role,Body=body,Seed=(uint)NativeExpeditionStates.StableHash(role.Post)};
            // the head's prop (the radio operator's headset) placed on the head at rest, then carried by the head bone
            foreach(var tr in skeleton.GetComponentsInChildren<Transform>(true))if(tr.name=="head"){s.Head=tr;break;}
            if(s.Head!=null)
            {
                var rest=s.Head.localToWorldMatrix;var at=rest.MultiplyPoint3x4(Vector3.zero);
                s.HeadProp=rest.inverse*Matrix4x4.TRS(at+new Vector3(0,NativeCampSurvivors.HeadsetUp,NativeCampSurvivors.HeadsetForward),Quaternion.identity,Vector3.one);
            }
            root.SetActive(false);
            return s;
        }
    }
}
#endif
