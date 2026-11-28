using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Belvedere Camp's survivors on the pilot model (Core NativeCampSurvivors; docs/expedition/BELVEDERE-CAMP.md "Survivors"): in the live
    // hub since A1 built their structures, and in the Developer walk's DEV preview. The nearest six within 30 m draw on the model, dressed
    // as their roles through the zombies' dressing (clean, a human skin, human eyes), posed from the camp's own motion bank
    // (Resources/ChipArenaCampMotion: local, never committed) and holding their props; the rest stay the bank's posed figures. Without
    // the motion bank (or the model) every survivor is its figure. Never replicated: no collider, physics or wire; who is in the camp
    // comes from the run's cleared instances the live view already carries (NativeCamp.Joined).
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
        private System.Numerics.Vector3[] campAt=Array.Empty<System.Numerics.Vector3>();
        // How many rescued have joined (kept across the hub's visits within a run: a survivor who joined while the pilots were away is
        // cheered as they come back), and the run it counts for.
        private int campMakesFrame=-1,campRescued=-1;private uint campRescuedSeed;private double campCheerAt=double.NaN,campCheerSeen=double.NaN;
        /// <summary>The survivors drawn on the model last frame (the walk's panel and the checks' probes).</summary>
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

        /// <summary>From CampScenery while the camp shows (before it draws: its figures skip the posts drawn here). Who is in the camp:
        /// NativeCamp.Joined by `rescued` (the walk) or `cleared` (live).</summary>
        private void CampSurvivorsFrame(NativeArena arena,float now,int rescued,HashSet<string> cleared)
        {
            camp.Skinned.Clear();CampSkinnedCount=0;
            var bank=CampMotion();var eye=WorldEye;
            if(bank==null||eye==null||!ModelBody((string)null)||visualBodyPrefab==null){CampSurvivorsHide();return;}
            // the posts shown and where they are; a survivor joining (U in the walk, a cleared instance live) or the cheer key (Y)
            // brings the civilians' cheer
            if(campAt.Length!=NativeCamp.Posts.Length)campAt=new System.Numerics.Vector3[NativeCamp.Posts.Length];
            int joined=0;
            for(int i=0;i<campAt.Length;i++)
            {
                var p=NativeCamp.Posts[i];if(!NativeCamp.Joined(p,rescued,cleared)){campAt[i]=new System.Numerics.Vector3(float.NaN,0,0);continue;}
                var at=NativeCamp.PostAt(p);campAt[i]=new System.Numerics.Vector3(at.x,at.y,at.z);if(p.Rescued!=null)joined++;
            }
            if(campRescuedSeed!=camp.Seed){campRescuedSeed=camp.Seed;campRescued=-1;}
            if(campRescued>=0&&joined>campRescued)campCheerAt=now;campRescued=joined;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(cleared==null&&!double.IsNaN(NativeCampWalk.CheerAt)&&NativeCampWalk.CheerAt!=campCheerSeen){campCheerSeen=NativeCampWalk.CheerAt;campCheerAt=now;}
#endif
            var e=eye.transform.position;NativeCampSurvivors.Pick(new System.Numerics.Vector3(e.x,e.y,e.z),campAt,campDrawn);
            foreach(var k in campDrawn)
            {
                if(k<0)continue;var post=NativeCamp.Posts[k];var role=NativeCampSurvivors.Of(post.Id);if(role==null)continue;
                var s=CampSurvivorOf(role);if(s?.Body?.Root==null)continue;
                float yaw=NativeCamp.PostYaw(post);var w=campAt[k];var at=new Vector3(w.X,w.Y,w.Z);
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
                // a prop in hand: the fist seated on its handle (a sword's grip along the wrist's +z), the arm otherwise the clip's
                var grasp=new NativeVisualRig.Grasp{Exact=1};var handle=NativeVisualRig.Blade(System.Numerics.Quaternion.Identity);
                if(prop!=null){if(left){grasp.LeftGrip=1;grasp.Left=handle;}else{grasp.RightGrip=1;grasp.Right=handle;}}
                s.Body.Binding.Apply(campPose,grasp,false,false);
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
                else if(role.Keeps!=null)
                {
                    var rest=NativeCampSurvivors.Rest(role);var key=role.Keeps.StartsWith("l:",StringComparison.Ordinal)?role.Keeps.Substring(2):role.Keeps;
                    camp.DrawProp(key,root*Matrix4x4.TRS(new Vector3(rest.X,rest.Y,rest.Z),Quaternion.Euler(rest.Pitch,rest.Yaw,rest.Roll),Vector3.one));
                }
                if(role.Head!=null&&s.Head!=null)camp.DrawProp(role.Head,s.Head.localToWorldMatrix*s.HeadProp);
            }
            // the bodies not drawn this frame go hidden (kept for when the eye comes back)
            foreach(var s in campSurvivors.Values)if(s.Body?.Root!=null&&s.PosedFrame!=Time.frameCount&&s.Body.Root.activeSelf)s.Body.Root.SetActive(false);
        }
        /// <summary>From PrewarmCamp, behind the loading screen: the bodies of everyone in the camp made before the hub's first frame (about
        /// 8 ms each), so none is made while the pods come down. Nothing without the motion bank or the model.</summary>
        private void CampSurvivorsPrewarm()
        {
            if(CampMotion()==null||!ModelBody((string)null)||visualBodyPrefab==null)return;
            foreach(var p in NativeCamp.Posts)
            {
                if(!NativeCamp.Joined(p,-1,NativeCampDraw.Cleared)||campSurvivors.ContainsKey(p.Id))continue;
                var role=NativeCampSurvivors.Of(p.Id);if(role==null)continue;var s=CampSurvivorMake(role);if(s!=null)campSurvivors[role.Post]=s;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>The REVIVE-O-MATIC where the live hub stands it (NativeReviveMachine.Spot on the generated map): Sparks works at it in the
        /// preview. Idle (its sequence's rest), its lights on the presentation clock; drawn with the preview's machines.</summary>
        private readonly List<(string id,System.Numerics.Matrix4x4 m)> campReviveParts=new List<(string,System.Numerics.Matrix4x4)>(96);
        private void CampPreviewRevive(float t)
        {
            if(sourceModels==null||!NativeReviveMachine.Spot(NativeExpeditionGeneratedWorld.MapId,out var at,out float yaw))return;
            NativeReviveMachine.PoseParts(0,campReviveParts,t);
            var root=Matrix4x4.TRS(new Vector3(at.X,at.Y,at.Z),Quaternion.Euler(0,yaw*Mathf.Rad2Deg,0),Vector3.one);
            for(int i=0;i<campReviveParts.Count;i++){var (id,m)=campReviveParts[i];sourceModels.Queue(id,root*UnityM(m));}
        }
#endif
        /// <summary>The camp not shown (the walk's preview off), or a frame without the bank: every survivor's body hidden, every figure drawn.</summary>
        private void CampSurvivorsHide()
        {
            camp?.Skinned.Clear();CampSkinnedCount=0;for(int k=0;k<campDrawn.Length;k++)campDrawn[k]=-1;
            foreach(var s in campSurvivors.Values)if(s.Body?.Root!=null&&s.Body.Root.activeSelf)s.Body.Root.SetActive(false);
        }
        /// <summary>The camp's map gone (an instance, the walk closed): the survivors' bodies let go (the joined count is kept for the run).</summary>
        private void CampSurvivorsRelease()
        {
            if(campSurvivors.Count==0)return;
            foreach(var s in campSurvivors.Values)if(s.Body?.Root!=null)VisualBodyGone(s.Body.Root);
            campSurvivors.Clear();for(int k=0;k<campDrawn.Length;k++)campDrawn[k]=-1;campCheerAt=double.NaN;
        }
        // A role's body: made once (one a frame at most, about 8 ms; the hub's are made behind its loading screen), dressed as its role,
        // kept while the camp's map stands.
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
