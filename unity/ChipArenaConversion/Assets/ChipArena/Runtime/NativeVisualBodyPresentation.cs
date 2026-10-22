using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The visual body (docs/BOZO-INTEGRATION.md step 4): a skinned character baked on the owner's PC from the licensed
    // character pack into Resources/ChipArenaVisualBody (git-ignored with the pack), drawn in place of the voxel pilot body
    // and posed from the pilot's own game pose through NativeVisualRig. Presentation only: the 22-joint rig still poses
    // every frame, authority reads neither, and the voxel body draws wherever the bake is absent or with --ca-voxel-bodies.
    // The rig's hand bones are moved onto the visual weapon frames after posing (and back before the next pose), so the
    // drawn weapon, its muzzle, blade sweeps and wrist effects follow the visual hands. Still voxel: first-person arms,
    // intros, portraits, the draft, corpses and kill effects; hats and the hit flash are not drawn on it yet.
    public sealed partial class ArenaPresentation
    {
        private sealed class VisualBody
        {
            public GameObject Root; public NativeVisualRigBinding Binding; public Renderer[] Cloth; public MaterialPropertyBlock Block;
            public string Spec, Accent; public int PosedFrame=-10; public bool Seen, Moved; public Vector3 At;
            public readonly Vector3[] HandPosition=new Vector3[2]; public readonly Quaternion[] HandRotation=new Quaternion[2];
        }
        public const string VisualBodyResource="ChipArenaVisualBody", VoxelBodiesArgument="--ca-voxel-bodies";
        private static GameObject visualBodyPrefab; private static int visualBodyState; // 0 unread, 1 drawn, -1 absent or turned off
        private readonly Dictionary<CapturedAnimation.Player,VisualBody> visualBodies=new Dictionary<CapturedAnimation.Player,VisualBody>();
        private readonly List<CapturedAnimation.Player> visualBodiesGone=new List<CapturedAnimation.Player>();
        private static readonly string[] ClothSlots={"Top.","Bottom.","Feet.","Gloves.","Hat."};
        private static readonly int[] AccentChannels={Shader.PropertyToID("_Color_3"),Shader.PropertyToID("_Color_6"),Shader.PropertyToID("_Color_9")};
        private static bool VisualBodiesOn
        {
            get
            {
                if(visualBodyState==0)
                {
                    visualBodyPrefab=Array.IndexOf(Environment.GetCommandLineArgs(),VoxelBodiesArgument)>=0?null:Resources.Load<GameObject>(VisualBodyResource);
                    visualBodyState=visualBodyPrefab!=null?1:-1;
                    if(visualBodyState==1)Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"Visual body: {0} found; pilots draw it in place of the voxel body ({1} draws voxel).",VisualBodyResource,VoxelBodiesArgument);
                }
                return visualBodyState==1;
            }
        }

        // Before a rig poses: its hand bones go back where its own pose put them, so the rig's route blends never read the
        // moved ones.
        private void VisualBodyRestore(int id)
        {
            if(!rigs.TryGetValue(id,out var rig)||!visualBodies.TryGetValue(rig,out var body)||!body.Moved)return;
            for(int side=0;side<2;side++){var bone=rig.Bones[side==0?CapturedRig.LHand:CapturedRig.RHand];bone.localPosition=body.HandPosition[side];bone.localRotation=body.HandRotation[side];}
            body.Moved=false;
        }

        // After a living arena pilot's rig is posed: pose its visual body from the same game pose, with its weapon's grasp.
        private void VisualBodyPose(Actor a,float time)
        {
            if(Viewer||sourceModels==null||a.SourceSpec=="siren"||FirstPerson&&a.Id==LocalId||!rigs.TryGetValue(a.Id,out var rig)||!VisualBodiesOn)return;
            if(!visualBodies.TryGetValue(rig,out var body)||body.Root==null)
            {
                body=CreateVisualBody(rig,a.Id);if(body==null)return;visualBodies[rig]=body;
            }
            var pose=rig.Pose;pose.World();
            Vector3 at=rig.Root.transform.position;
            // A fresh solve after a gap (death, first person, a hidden frame), a teleport or a weapon change.
            if(body.PosedFrame<Time.frameCount-1||body.Spec!=a.SourceSpec||(at-body.At).sqrMagnitude>4)body.Binding.State.Forget();
            body.PosedFrame=Time.frameCount;body.Spec=a.SourceSpec;body.At=at;
            bool roll=a.Rolling(time),juggle=NativeJugglePose.Active(a.JuggleUntil,time);
            body.Binding.Apply(pose,VisualGrasp(a,pose,time),a.Grounded&&!roll&&!juggle,roll||juggle);
            if(body.Accent!=a.AccentColor)VisualAccent(body,a.AccentColor);
            // The rig's hands onto the visual weapon frames (the right's weapon, the left's off-hand blade or bare wrist).
            var origin=body.Root.transform;
            body.Binding.WeaponFrame(pose,out var rp,out var rq);body.Binding.LeftWeaponFrame(pose,out var lp,out var lq);
            for(int side=0;side<2;side++)
            {
                var bone=rig.Bones[side==0?CapturedRig.LHand:CapturedRig.RHand];
                body.HandPosition[side]=bone.localPosition;body.HandRotation[side]=bone.localRotation;
                bone.SetPositionAndRotation(origin.TransformPoint(side==0?lp:rp),origin.rotation*(side==0?lq:rq));
            }
            body.Moved=true;
        }

        private VisualBody CreateVisualBody(CapturedAnimation.Player rig,int id)
        {
            var root=Instantiate(visualBodyPrefab,rig.Root.transform,false);root.name="Visual body "+id;
            // Its origin is the pilot's floor point: the rig root stands .865 m over the floor (NativeVisualRig.Floor).
            root.transform.localPosition=new Vector3(0,NativeVisualRig.Floor,0);root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            var skeleton=root.transform.Find("VisualSkeleton");
            if(skeleton==null){Destroy(root);visualBodyState=-1;Debug.LogWarning("Visual body: "+VisualBodyResource+" has no VisualSkeleton; pilots draw voxel.");return null;}
            var body=new VisualBody{Root=root,Binding=new NativeVisualRigBinding(root.transform,skeleton),Block=new MaterialPropertyBlock()};
            var cloth=new List<Renderer>();
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var slot in ClothSlots)if(r.name.StartsWith(slot,StringComparison.Ordinal)){cloth.Add(r);break;}
            body.Cloth=cloth.ToArray();
            return body;
        }

        // What the visual hands hold: the Standard weapons' handles (NativeVisualRig), with the special moves' held turn; other
        // kits close the weapon hand on nothing seated (their weapon draws at the visual wrist).
        private static NativeVisualRig.Grasp VisualGrasp(Actor a,CapturedRig.Pose pose,float time)
        {
            var g=new NativeVisualRig.Grasp();
            if(a.WeaponAway)return g;
            var w=a.Weapon;
            if(GunRules.Ranged(w))
            {
                g.RightGrip=1;g.Support=1;g.Right=NativeVisualRig.PistolGrip(w);g.SupportHold=NativeVisualRig.GunSupport(w,pose);g.Stock=NativeVisualRig.Stock(w);
                return g;
            }
            if(!CapturedAnimation.Player.CapturedMelee(a)){g.RightGrip=NativeWeaponDressing.SocketShown(a.SourceSpec)?1:0;return g;}
            int set=Mathf.Clamp(a.VisualSet,0,3);string spec=set==0?"sword":set==1?"daggers":set==2?"greatsword":"shield";
            var held=NativeSpecialPose.HeldRotation(a,time,false);
            if(!a.MainWeaponAway){g.RightGrip=1;g.Right=NativeVisualRig.Melee(spec,held);}
            if(set==1){g.LeftGrip=1;g.Left=NativeVisualRig.Melee(spec,NativeSpecialPose.HeldRotation(a,time,true));}
            if(set==2&&!a.MainWeaponAway){g.Support=1;g.SupportHold=NativeVisualRig.GreatswordSupport(held);}
            return g;
        }

        // The pilot's colour on the outfit's accent channels (the baked palette keeps the third colour of each layer for it).
        private static void VisualAccent(VisualBody body,string accent)
        {
            body.Accent=accent;
            if(!ColorUtility.TryParseHtmlString(accent??"#70e1d0",out var color))return;
            foreach(var r in body.Cloth)
            {
                if(r==null)continue;
                r.GetPropertyBlock(body.Block);foreach(int id in AccentChannels)body.Block.SetColor(id,color);r.SetPropertyBlock(body.Block);
            }
        }

        // While drawing a third-person arena pilot (SourceCharacter): whether its visual body replaces the voxel one this frame.
        private bool VisualBodyDrawn(CapturedAnimation.Player rig)
        {
            if(!visualBodies.TryGetValue(rig,out var body)||body.Root==null||body.PosedFrame!=Time.frameCount)return false;
            body.Seen=true;return true;
        }

        // After the pilots are drawn: show the visual bodies drawn this frame and hide the rest (dead, first person, hidden).
        private void VisualBodiesShown()
        {
            if(visualBodies.Count==0)return;
            visualBodiesGone.Clear();
            foreach(var pair in visualBodies)
            {
                var body=pair.Value;
                if(body.Root==null){visualBodiesGone.Add(pair.Key);continue;}
                if(body.Root.activeSelf!=body.Seen)body.Root.SetActive(body.Seen);
                body.Seen=false;
            }
            foreach(var rig in visualBodiesGone)visualBodies.Remove(rig);
        }
        // The bodies live under their rigs' roots, so disposing a rig destroys its body; this drops the records.
        private void ClearVisualBodies(){foreach(var body in visualBodies.Values)if(body.Root!=null)Destroy(body.Root);visualBodies.Clear();}
    }
}
