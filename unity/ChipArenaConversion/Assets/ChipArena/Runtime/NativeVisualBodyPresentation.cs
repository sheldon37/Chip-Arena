using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The visual body (docs/BOZO-INTEGRATION.md step 4): a skinned character baked on the owner's PC from the licensed
    // character pack into Resources/ChipArenaVisualBody (git-ignored with the pack), drawn in place of the voxel pilot body
    // and posed from the pilot's own game pose through NativeVisualRig. Presentation only: the 22-joint rig still poses
    // every frame, authority reads neither, and the voxel body draws wherever the bake is absent, with --ca-voxel-bodies or
    // while Settings · Player model is Classic (ClassicBodies, live: the bodies hide the frame they are not posed).
    // The rig's hand bones are moved onto the visual weapon frames after posing (and back before the next pose), so the
    // drawn weapon, its muzzle, blade sweeps and wrist effects follow the visual hands; a weapon the bake carries a life-size
    // model of draws that model instead, with the right hand's frame fitted to it (NativeVisualWeaponPresentation.cs). It takes the hit flash and the
    // match-start spawn-in in its own shader (VisualBodyLook), and wears the pilot gear (VisualGear: the chest core and the
    // right-wrist chip device, Core NativePilotGear) at anchors its bake measured. First person wears its forearms and hands
    // (VisualViewPose). Still voxel: intros, portraits, the draft, corpses and kill effects; hats are not drawn on it yet.
    public sealed partial class ArenaPresentation
    {
        private sealed class VisualBody
        {
            public GameObject Root; public NativeVisualRigBinding Binding; public Renderer[] Cloth, All; public MaterialPropertyBlock Block;
            // The game's joints on the body: per joint, the bone it maps to (or its nearest mapped ancestor's) and the joint's frame
            // relative to that bone as first bound, so a joint's frame follows the model through the pack's clips (VisualJointFrame).
            public int[] JointBone; public Matrix4x4[] JointFrom;
            // A broadcast bust's neck bone and its place in the body (VisualBust).
            public int BustNeck=-1; public Vector3 BustPivot=new Vector3(float.NaN,0,0);
            public string Spec, Accent; public int PosedFrame=-10; public bool Seen, Moved, SpawnOn, Ghosted; public Vector3 At; public float Flash=1, Glow=-1;
            public Material[][] Plain, Spawning;
            // The gear's anchors (the bake's VisualGearCore and VisualGearDevice, the band's radius from VisualGearDeviceRim), when
            // the body last appeared or finished forming (the core's flare), the last seated chip (its plant's key, when, colour)
            // and the pilot's accent (linear, the bay's and lens's rest colour).
            public Transform GearCore, GearDevice; public float GearRadius, LitAt=float.NaN, SeatAt=float.NaN; public int SeatKey=int.MinValue; public bool Building;
            // The outfit it wears (Core/NativeLook.cs; VisualDress) and its back emblem's frame on the chest bone for that top.
            public string Look=""; public Matrix4x4 Emblem=EmblemFrame;
            // Its hair and cloth simulation (NativeVisualCloth: the local install's; null without one) and whether it runs (-1 unset).
            public object ClothSim; public int ClothOn=-1;
            // Its hair pieces' hood shapes (VisualHatHair): the renderers, each one's shape and the outfit's weight, and whether pressed.
            public SkinnedMeshRenderer[] HatHair; public int[] HatHairShape; public float[] HatHairRest; public bool HatHairOn;
            public Color ChipColor=Color.cyan, AccentLinear=Color.cyan;
            public readonly Vector3[] HandPosition=new Vector3[2]; public readonly Quaternion[] HandRotation=new Quaternion[2];
            // Its own weapons (NativeVisualWeaponPresentation.cs): the bake's entries by spec, the one in hand this frame (Held,
            // fitted on the right hand bone, whose scale RightScale restores) and the one shown (when).
            public Dictionary<string,VisualWeapon> Weapons; public VisualWeapon Held, Shown; public int ShownFrame=-1; public Vector3 RightScale=Vector3.one;
            // Its stride: the pack's locomotion clips on a proxy skeleton, for the legs (NativeVisualStridePresentation.cs).
            public VisualStride Stride;
            // Its own animation: the pack's clips whole for a weapon whose set has moved over (NativeVisualMotionPresentation.cs).
            public VisualMotion Motion;
        }
        public const string VisualBodyResource="ChipArenaVisualBody", VoxelBodiesArgument="--ca-voxel-bodies";
        private const int ViewBodyId=-999;
        private static GameObject visualBodyPrefab; private static int visualBodyState; // 0 unread, 1 drawn, -1 absent or turned off
        // The dead pilot's dissolve: the bake's noise (VisualDissolve/Noise, from the owner's dissolve pack) set as the visual body
        // shader's global once the bake loads; without it a dead body goes at half opacity as before.
        private static bool visualDissolve;
        private static readonly int DissolveId=Shader.PropertyToID("_Dissolve"),DissolveEdgeId=Shader.PropertyToID("_DissolveEdge"),DissolveNoiseId=Shader.PropertyToID("_VisualDissolveNoise");
        /// <summary>The dissolve front glows the pilot's accent at this many times its colour (past 1 the bloom takes it).</summary>
        public const float DissolveGlow=3f;
        /// <summary>Settings · Player model is Classic (set each frame from the preferences): every pilot draws its voxel body.</summary>
        public bool ClassicBodies;
        // This client's own outfit (prefs.look, set every frame): its pilot's bodies wear it; every other pilot the default
        // until the look contract carries a seat's (Astra).
        public string LocalLook="";
        // The arena's camera this frame (the cloth's distance hold).
        public Camera VisualEye;
        private string VisualLookOf(int id)=>id==LocalId||id==ViewBodyId||id==RooftopIdTop?LocalLook??"":ModelPortraitOf(id,out var portrait)?portrait.Outfit:"";
        /// <summary>Whether this install draws the visual body at all (its bake found, not turned off at launch): Settings shows the
        /// Player model choice only then.</summary>
        public static bool VisualBodyInstalled=>VisualBodiesOn;
        private readonly Dictionary<CapturedAnimation.Player,VisualBody> visualBodies=new Dictionary<CapturedAnimation.Player,VisualBody>();
        private readonly List<CapturedAnimation.Player> visualBodiesGone=new List<CapturedAnimation.Player>();
        private static readonly string[] ClothSlots={"Top.","Bottom.","Feet.","Gloves.","Hat."};
        private static readonly int[] AccentChannels={Shader.PropertyToID("_Color_3"),Shader.PropertyToID("_Color_6"),Shader.PropertyToID("_Color_9")};
        private static readonly int AccentGlowId=Shader.PropertyToID("_AccentGlow");
        /// <summary>The clothing's accent trims glow at this many times their colour (over the lit colour; Reduced effects .6 of
        /// it), pulsing as the voxel armour's halos do while the block cools down (NativeGlowRules.ArmorPulse). In place of
        /// those halos, which sit at the game rig's joints (NativeCombatGlow skips them on a visual body).</summary>
        public const float AccentGlow=1.2f,AccentGlowReduced=.6f;
        private static readonly int HitFlashId=Shader.PropertyToID("_HitFlash"),SpawnFrontId=Shader.PropertyToID("_SpawnFront"),SpawnTintId=Shader.PropertyToID("_SpawnTint"),SpawnGlitchId=Shader.PropertyToID("_SpawnGlitch"),SpawnHoloId=Shader.PropertyToID("_SpawnHolo"),
            TextureId=Shader.PropertyToID("_Texture2D"),CutoffId=Shader.PropertyToID("_Cutoff"),GhostSideId=Shader.PropertyToID("_GhostSide"),GhostColorId=Shader.PropertyToID("_GhostColor");
        // The spawn-in hologram's slots (Resources/NativeVisualBodyGhost.shader): the slice and flash, the split's two copies.
        private Material[] visualGhosts; private bool visualGhostsFailed;
        // Echoes of the visual body (owner 2026-10-24: "The dodge effect still shows the voxel model"): a roll afterimage, dash
        // ghost or Demon Step ghost of a pilot drawn as the model snapshots its drawn renderers (the skinned ones baked into
        // pooled meshes) and draws them through that ghost's own shader, tinted and fading as the voxel ghosts are.
        private sealed class VisualEcho { public Mesh[] Baked=new Mesh[0], Meshes=new Mesh[0]; public Matrix4x4[] At=new Matrix4x4[0]; public int Count; }
        private readonly Dictionary<string,Material> visualEchoMaterials=new Dictionary<string,Material>(); private MaterialPropertyBlock visualEchoBlock;
        private static readonly int VisualEchoTint=Shader.PropertyToID("_BodyTint");
        // The echo's share of the voxel ghost's opacity (the model's layered meshes, body under clothes, add up under the
        // additive ghost) and its depth offset toward the eye.
        private const float VisualEchoShare=.55f, VisualEchoOffset=-2;
        // The spawn-in as the visual body's shader reads it (Resources/NativeVisualBodyFx.cginc); the spawn-in's
        // SpawnVisualLook fills it from the frame the voxel parts read. Off: formed, untinted, no hologram.
        private struct VisualSpawnLook
        {
            public Vector4 Front, Tint, Glitch, Holo; public bool Ghost;
            public static readonly VisualSpawnLook Off=new VisualSpawnLook{Front=new Vector4(1e5f,1,1,0),Tint=new Vector4(1,1,1,0),Glitch=Vector4.zero,Holo=new Vector4(1,1,1,0)};
        }
        private static bool VisualBodiesOn
        {
            get
            {
                if(visualBodyState==0)
                {
                    visualBodyPrefab=Array.IndexOf(Environment.GetCommandLineArgs(),VoxelBodiesArgument)>=0?null:Resources.Load<GameObject>(VisualBodyResource);
                    visualBodyState=visualBodyPrefab!=null?1:-1;
                    if(visualBodyState==1)Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"Visual body: {0} found; pilots draw it in place of the voxel body ({1} draws voxel).",VisualBodyResource,VoxelBodiesArgument);
                    var noise=visualBodyPrefab!=null?visualBodyPrefab.transform.Find("VisualDissolve/Noise"):null;var noiseTex=noise!=null?noise.GetComponent<Renderer>()?.sharedMaterial?.mainTexture:null;
                    if(noiseTex!=null){Shader.SetGlobalTexture(DissolveNoiseId,noiseTex);visualDissolve=true;}
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
            rig.Bones[CapturedRig.RHand].localScale=body.RightScale;body.Moved=false;
        }

        // After a living arena pilot's rig is posed: pose its visual body from the same game pose, with its weapon's grasp.
        private void VisualBodyPose(Actor a,float time)
        {
            if(Viewer||sourceModels==null||ClassicBodies||a.SourceSpec=="siren"||FirstPerson&&a.Id==LocalId||!rigs.TryGetValue(a.Id,out var rig)||!VisualBodiesOn)return;
            var body=VisualBodyOf(rig,a.Id);if(body==null)return;
            var pose=rig.Pose;pose.World();
            bool roll=a.Rolling(time),juggle=NativeJugglePose.Active(a.JuggleUntil,time);
            var held=VisualBodyBind(body,rig,a,time,pose,a.Grounded&&!roll&&!juggle,roll||juggle);
            VisualStridePose(body,rig,a,time,roll,juggle);
            VisualMotionPose(body,rig,a,time);
            var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
            body.Moved=true;
        }

        // The visual body `rig` wears (made on first use for pilot `id`'s outfit); null without a bake.
        private VisualBody VisualBodyOf(CapturedAnimation.Player rig,int id)
        {
            // A changed outfit makes the body again in it (the Locker's picks apply in the lobby).
            if(visualBodies.TryGetValue(rig,out var body)&&body.Root!=null&&!string.Equals(body.Look,VisualLookOf(id),StringComparison.Ordinal)){VisualStrideRelease(body);VisualMotionRelease(body);body.Root.SetActive(false);Destroy(body.Root);body.Root=null;}
            if(body==null||body.Root==null){body=CreateVisualBody(rig,id);if(body==null)return null;visualBodies[rig]=body;}
            return body;
        }
        // The body bound to `pose` (feet planted when `planted`, the airborne solve when `airborne`), its weapon in hand and the
        // rig's hands onto its weapon frames; its held weapon.
        private VisualWeapon VisualBodyBind(VisualBody body,CapturedAnimation.Player rig,Actor a,float time,CapturedRig.Pose pose,bool planted,bool airborne)
        {
            Vector3 at=rig.Root.transform.position;
            // A fresh solve after a gap (death, first person, a hidden frame), a teleport or a weapon change.
            if(body.PosedFrame<Time.frameCount-1||body.Spec!=a.SourceSpec||(at-body.At).sqrMagnitude>4){body.Binding.State.Forget();if(body.ClothSim!=null)NativeVisualCloth.Reset?.Invoke(body.ClothSim);}
            body.PosedFrame=Time.frameCount;body.Spec=a.SourceSpec;body.At=at;
            // Its own weapon in hand, if the bake carries one: the grasp on its handguard and stock.
            var held=body.Held=VisualWeaponFor(body,a);var grasp=VisualGrasp(a,pose,time,ChipLookOf(a.Id));if(held!=null)VisualWeaponGrasp(ref grasp,held);
            body.Binding.Apply(pose,grasp,planted,airborne);
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
            // After the hands go onto the wrists: what rides a hand (the Cyber Lord's gauntlets, a chip) sits on the model's.
            if(body.JointFrom==null)VisualJointsOf(body,rig);
            return held;
        }
        // VisualBodyBind's first bind (the body on the rig's own pose): each joint's frame on its bone.
        private static void VisualJointsOf(VisualBody body,CapturedAnimation.Player rig)
        {
            var of=body.Binding.Rig.Of;var bones=body.Binding.Bones;int n=Mathf.Min(22,rig.Bones.Length);body.JointBone=new int[n];body.JointFrom=new Matrix4x4[n];
            for(int j=0;j<n;j++)
            {
                int k=j,v=-1;while(k>=0&&v<0){v=k<of.Length?of[k]:-1;if(v<0||v>=bones.Length||bones[v]==null){v=-1;k=CapturedRig.Bones[k].Parent;}}
                body.JointBone[j]=v;body.JointFrom[j]=v>=0?bones[v].worldToLocalMatrix*rig.Bones[j].localToWorldMatrix:Matrix4x4.identity;
            }
        }
        // Joint `j`'s frame where `rig` draws: on its visual body (following the model) when it wears one this frame, else the rig's.
        private Matrix4x4 VisualJointFrame(CapturedAnimation.Player rig,int j)
        {
            if(rig!=null&&visualBodies.TryGetValue(rig,out var body)&&body.Root!=null&&body.JointFrom!=null&&body.PosedFrame==Time.frameCount&&j>=0&&j<body.JointBone.Length&&body.JointBone[j]>=0)
                return body.Binding.Bones[body.JointBone[j]].localToWorldMatrix*body.JointFrom[j];
            return rig.Bones[j].localToWorldMatrix;
        }
        // A hat on the model's head (owner 2026-10-25: "Lets work on all these"): the head joint's frame on the model, where every
        // hat sits on the hair at its own size (the pack's under-hat hair shapes left it nearly bald).
        // Fitted to the model's head (NativeModelHats: scaled and seated on its crown, sunk by the hat's kind), its hair pressed
        // under it (VisualHatHair).
        private Func<int,Matrix4x4> hatBone;private CapturedAnimation.Player hatRig;private string hatWorn;
        private Func<int,Matrix4x4> VisualHatBone(CapturedAnimation.Player rig,string hat){hatRig=rig;hatWorn=hat;return hatBone??=b=>VisualHatFrame(hatRig,hatWorn,b);}
        private Matrix4x4 VisualHatFrame(CapturedAnimation.Player rig,string hat,int b)
        {
            if(b==CapturedRig.Head&&rig!=null&&visualBodies.TryGetValue(rig,out var body)&&body.Root!=null&&body.PosedFrame==Time.frameCount)
            {
                var head=VisualHeadOf(body);
                if(head!=null&&hat==NativeSheldonHead.Hat){var k=NativeModelHats.SheldonScale;var o=NativeModelHats.SheldonOffset;return head.localToWorldMatrix*Matrix4x4.TRS(new Vector3(o.X,o.Y,o.Z),Quaternion.identity,new Vector3(k.X,k.Y,k.Z));}
                if(head!=null){NativeModelHats.Fit(hat,out float scale,out var offset);return head.localToWorldMatrix*Matrix4x4.TRS(new Vector3(offset.X,offset.Y,offset.Z),Quaternion.identity,Vector3.one*scale);}
            }
            return VisualJointFrame(rig,b);
        }
        // The worn hair pressed under a hat: each hair piece's own hood shape (the pack's "Tag_Hood": the spikes laid into a ring
        // round the head, the crown under the hat) full while a hat that presses it is worn (NativeModelHats.Presses), back to the
        // outfit's weight otherwise. Writes only when that changes.
        private void VisualHatHair(CapturedAnimation.Player rig,string hat){if(rig!=null&&visualBodies.TryGetValue(rig,out var body))VisualHatHair(body,hat);}
        private static void VisualHatHair(VisualBody body,string hat)
        {
            bool on=NativeModelHats.Presses(hat);if(body?.Root==null||body.HatHair!=null&&on==body.HatHairOn)return;
            if(body.HatHair==null)
            {
                var hair=new List<SkinnedMeshRenderer>();var shape=new List<int>();var rest=new List<float>();
                foreach(var skin in body.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh=skin.sharedMesh;if(mesh==null)continue;
                    for(int i=0;i<mesh.blendShapeCount;i++)if(mesh.GetBlendShapeName(i).EndsWith("Tag_Hood",StringComparison.Ordinal)){hair.Add(skin);shape.Add(i);rest.Add(skin.GetBlendShapeWeight(i));}
                }
                body.HatHair=hair.ToArray();body.HatHairShape=shape.ToArray();body.HatHairRest=rest.ToArray();
            }
            for(int k=0;k<body.HatHair.Length;k++)if(body.HatHair[k]!=null)body.HatHair[k].SetBlendShapeWeight(body.HatHairShape[k],on?100:body.HatHairRest[k]);
            body.HatHairOn=on;
        }
        // The parry flash on the model: its drawn body snapshotted each frame of the flash, tinted (VisualEchoSnap/Draw).
        private readonly Dictionary<int,VisualEcho> parryEchoes=new Dictionary<int,VisualEcho>();
        private void VisualParryFlash(CapturedAnimation.Player rig,Actor a,Color colour,float opacity)
        {
            if(!(opacity>0))return;parryEchoes.TryGetValue(a.Id,out var echo);
            if(VisualEchoSnap(rig,ref echo)){parryEchoes[a.Id]=echo;VisualEchoDraw(echo,"NativeWeaponGhost",colour,opacity/VisualEchoShare);}
        }
        // The entrance's pilot on its visual body (owner 2026-10-24: "Model everywhere"): the intro rig's own authored pose
        // (`pose`, as the entrance wrote it) retargeted, without the pack's clips or the stride and with the feet as authored;
        // its weapon in hand where the entrance shows it (IntroWeapon). While the entrance still builds the body in (`reveal`
        // under half on the pelvis, chest, head or a hand) the voxel body draws the reveal instead.
        private static readonly int[] IntroSolidBones={CapturedRig.Pelvis,CapturedRig.Chest,CapturedRig.Head,CapturedRig.LHand,CapturedRig.RHand};
        private void VisualIntroPose(CapturedAnimation.Player rig,Actor a,float time,CapturedRig.Pose pose,Func<int,float> reveal)
        {
            if(Viewer||sourceModels==null||ClassicBodies||rig==null||a==null||pose==null||a.SourceSpec=="siren"||!VisualBodiesOn)return;
            if(reveal!=null)foreach(int b in IntroSolidBones)if(reveal(b)<.5f)return;
            var body=VisualBodyOf(rig,a.Id);if(body==null)return;
            pose.World();var held=VisualBodyBind(body,rig,a,time,pose,false,false);
            var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
            body.Moved=true;
        }
        // The draft's pilot on its visual body (owner 2026-10-24: "Model everywhere"): the drafting seat as a stand-in actor (its
        // outfit, accent, no weapon in hand: the hands pick the chips), bound to the stage's pose, with its hat on the model's
        // head; shown this frame (the draft returns before the arena does). False: the voxel body (a Siren, Classic, no bake).
        private readonly Actor draftActor=new Actor();
        private bool VisualDraftPose(CapturedAnimation.Player rig,NativeSeat seat,CapturedRig.Pose pose,float time,Camera camera)
        {
            if(Viewer||sourceModels==null||ClassicBodies||rig==null||seat==null||pose==null||seat.spec=="siren"||!VisualBodiesOn)return false;
            var a=draftActor;a.Id=seat.id;a.SourceSpec=seat.spec;a.AccentColor=seat.accentColor;a.BodyColor=seat.bodyColor;a.Hat=seat.hat;a.HatColor=seat.hatColor;a.WeaponAway=true;a.Grounded=true;
            var body=VisualBodyOf(rig,seat.id);if(body==null)return false;
            // The drafting pose stands lower than the arena's: the body's hips go onto the pose's pelvis, and the rig's hands (on
            // the body's wrists after the bind) with them.
            body.Root.transform.localPosition=new Vector3(0,NativeVisualRig.Floor,0);
            pose.World();var held=VisualBodyBind(body,rig,a,time,pose,false,false);
            int hips=CapturedRig.Pelvis<body.Binding.Rig.Of.Length?body.Binding.Rig.Of[CapturedRig.Pelvis]:-1;
            if(hips>=0&&body.Binding.Bones[hips]!=null){var shift=rig.Bones[CapturedRig.Pelvis].position-body.Binding.Bones[hips].position;body.Root.transform.position+=shift;for(int side=0;side<2;side++){var hand=rig.Bones[side==0?CapturedRig.LHand:CapturedRig.RHand];hand.position+=shift;}}
            var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
            body.Moved=true;body.Seen=true;
            VisualHatHair(body,seat.hat);
            if(!string.IsNullOrEmpty(seat.hat)&&seat.hat!="none"&&seat.hat!=NativeSheldonHead.Hat)
            {var hb=VisualHatBone(rig,seat.hat);if(!AuditorHalo(seat.hat,hb(CapturedRig.Head),time,1,false,0,null,camera,seat.hatColor))sourceModels.Queue(HatModel(seat.hat,seat.hatColor),rig.Root.transform.localToWorldMatrix,hb,body:HatBody(seat.hat,seat.hatColor));}
            VisualFaceGear(VisualHeadOf(body),seat.id,seat.accentColor,0,null,null,null,0);
            return true;
        }
        private VisualBody CreateVisualBody(CapturedAnimation.Player rig,int id)
        {
            var root=Instantiate(visualBodyPrefab,rig.Root.transform,false);root.name="Visual body "+id;
            // Its origin is the pilot's floor point: the rig root stands .865 m over the floor (NativeVisualRig.Floor).
            root.transform.localPosition=new Vector3(0,NativeVisualRig.Floor,0);root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            var skeleton=root.transform.Find("VisualSkeleton");
            if(skeleton==null){Destroy(root);visualBodyState=-1;Debug.LogWarning("Visual body: "+VisualBodyResource+" has no VisualSkeleton; pilots draw voxel.");return null;}
            var body=new VisualBody{Root=root,Binding=new NativeVisualRigBinding(root.transform,skeleton),Block=new MaterialPropertyBlock()};
            // Dressed in the pilot's outfit: only the worn pieces' renderers from here on.
            body.Look=VisualLookOf(id);VisualDress(root,body.Look);
            body.All=VisualWorn(root);body.Plain=new Material[body.All.Length][];
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="VisualGearCore")body.GearCore=t;else if(t.name=="VisualGearDevice")body.GearDevice=t;
                else if(t.name=="VisualGearDeviceRim")body.GearRadius=t.localPosition.magnitude;
            }
            VisualAnchorsFor(body);
            if(body.GearDevice!=null&&!(body.GearRadius>.01f))body.GearDevice=null;
            body.Weapons=VisualWeaponsOf(root.transform);
            for(int k=0;k<body.All.Length;k++)
            {
                var r=body.All[k];body.Plain[k]=r.sharedMaterials;var m=r.sharedMaterial;if(m==null||!m.HasProperty(TextureId))continue;
                // The hologram's slots cut out as the part does: the part's texture and cut-off on its renderer.
                r.GetPropertyBlock(body.Block);var tex=m.GetTexture(TextureId);if(tex!=null)body.Block.SetTexture(TextureId,tex);
                body.Block.SetFloat(CutoffId,m.HasProperty(CutoffId)?m.GetFloat(CutoffId):.5f);r.SetPropertyBlock(body.Block);
            }
            var cloth=new List<Renderer>();
            foreach(var r in body.All)foreach(var slot in ClothSlots)if(r.name.StartsWith(slot,StringComparison.Ordinal)){cloth.Add(r);break;}
            body.Cloth=cloth.ToArray();
            if(id!=ViewBodyId)body.Stride=VisualStrideOf(root.transform,skeleton);
            // Its hair and cloth, when the local install simulates them (never on the first-person body).
            if(id!=ViewBodyId&&NativeVisualCloth.Attach!=null)
            {try{body.ClothSim=NativeVisualCloth.Attach(root,skeleton);}catch(Exception e){NativeVisualCloth.Attach=null;Debug.LogWarning("Visual body: the cloth simulation failed and is off: "+e.Message);}}
            return body;
        }

        // What the visual hands hold: the Standard weapons' handles (NativeVisualRig), with the special moves' held turn; other
        // kits close the weapon hand on nothing seated (their weapon draws at the visual wrist). While the chip-select look is up
        // (`chip`, its weight) the support hand lets go and the stock leaves the shoulder: the left hand taps and inserts the chip.
        private static NativeVisualRig.Grasp VisualGrasp(Actor a,CapturedRig.Pose pose,float time,float chip)
        {
            var g=VisualHold(a,pose,time);float keep=1-Mathf.Clamp01(chip);g.Support*=keep;g.Stock*=keep;return g;
        }
        private static NativeVisualRig.Grasp VisualHold(Actor a,CapturedRig.Pose pose,float time)
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
            body.AccentLinear=color.linear;
            foreach(var r in body.Cloth)
            {
                if(r==null)continue;
                r.GetPropertyBlock(body.Block);foreach(int id in AccentChannels)body.Block.SetColor(id,color);r.SetPropertyBlock(body.Block);
            }
        }

        // While drawing it (SourceCharacter): the pilot's hit flash code and match-start spawn-in on the visual body's shader,
        // per renderer, and the hologram's slots while the spawn-in shows one (none on the low graphics tier). Writes only
        // while a spawn-in runs or the flash changes; allocates only the first time a body shows the hologram.
        private void VisualBodyLook(CapturedAnimation.Player rig,Actor a,float time,SpawnView spawn,float flash,Func<int,float> reveal,Func<int,Color> tint)
        {
            bool first=rig==viewRig&&viewBody!=null;VisualBody body;
            if(first)body=viewBody;else if(!visualBodies.TryGetValue(rig,out body))return;
            if(body.Root==null)return;
            var look=new VisualSpawnLook();bool on=SpawnVisualLook(spawn,rig,ref look);body.Building=on&&look.Front.x<1e4f;
            VisualGear(body,a,time,reveal,tint,flash,first);
            bool ghost=on&&look.Ghost&&VisualGhosts()!=null;
            if(ghost!=body.Ghosted)
            {
                if(ghost&&body.Spawning==null)body.Spawning=VisualSpawnSlots(body);
                for(int k=0;k<body.All.Length;k++)if(body.All[k]!=null)body.All[k].sharedMaterials=ghost?body.Spawning[k]:body.Plain[k];
                body.Ghosted=ghost;
            }
            VisualAccentGlow(body,a.Id,time);
            if(!on&&!body.SpawnOn&&flash==body.Flash)return;
            if(!on)look=VisualSpawnLook.Off;
            foreach(var r in body.All)
            {
                if(r==null)continue;
                r.GetPropertyBlock(body.Block);body.Block.SetFloat(HitFlashId,flash);body.Block.SetVector(SpawnFrontId,look.Front);body.Block.SetVector(SpawnTintId,look.Tint);
                body.Block.SetVector(SpawnGlitchId,look.Glitch);body.Block.SetVector(SpawnHoloId,look.Holo);r.SetPropertyBlock(body.Block);
            }
            body.SpawnOn=on;body.Flash=flash;
        }
        // The pilot gear on the visual body (Core NativePilotGear; the models NativePilotGearModels.cs), each frame it draws: the
        // core at the chest anchor, glowing as CoreGlow gives it (its flare restarts when the body reappears or finishes forming);
        // the device at the forearm anchor, its lid on the hinge and the chip in the bay as Device gives them from the pilot's
        // chip-select look and wrist plant (ChipDeviceLook). The body's reveal, tint and hit flash reach them as they reach the
        // voxel parts at those joints. Allocation-free.
        private void VisualGear(VisualBody body,Actor a,float time,Func<int,float> reveal,Func<int,Color> tint,float flash,bool first)
        {
            if(body.GearCore==null&&body.GearDevice==null||!sourceModels.PilotGear(body.GearRadius>.01f?body.GearRadius:.04f))return;
            if(!body.Root.activeSelf||body.Building||float.IsNaN(body.LitAt))body.LitAt=time;
            var accent=AccentTint(a.AccentColor);gearBone??=GearBoneOf;
            // First person draws the device only (the chest is not shown), on the view model's layer and camera.
            int layer=first?10:gearLayer;var camera=first?ViewCamera:gearCamera;
            if(body.GearCore!=null&&!first)
            {
                var p=LoadoutPilot(a.Id);float max=p?.Stats!=null?(float)p.Stats["hp"]:100;float health=max>0?(p!=null?p.Hp:a.Hp)/max:1;
                float g=NativePilotGear.CoreGlow(time,health,time-body.LitAt,a.Id);gearMatrix=body.GearCore.localToWorldMatrix;
                sourceModels.Queue(NativeSourceModels.GearCore,gearMatrix,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,new Vector4(g,g,g,1),null,flash);
            }
            if(!first)VisualFaceGear(VisualHeadOf(body),a.Id,a.AccentColor,layer,camera,reveal,tint,flash);
            // The back emblem on the chest's model bone, glowing as the core does.
            if(!first)
            {
                var of=body.Binding.Rig.Of;int cv=CapturedRig.Chest<of.Length?of[CapturedRig.Chest]:-1;var chest=cv>=0&&cv<body.Binding.Bones.Length?body.Binding.Bones[cv]:null;
                if(chest!=null){gearMatrix=chest.localToWorldMatrix*body.Emblem;var e=NativePilotGear.CoreGlow(time,1,time-body.LitAt,a.Id);sourceModels.Queue(NativeSourceModels.GearEmblem,gearMatrix,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,new Vector4(e,e,e,1),null,flash);}
            }
            if(body.GearDevice==null)return;
            ChipDeviceLook(a.Id,out float look,out float plant,out int rgb,out int key);
            if(plant>=NativeDraftPlant.CarryEnd&&key!=body.SeatKey)
            {
                body.SeatKey=key;body.SeatAt=time-(plant-NativeDraftPlant.CarryEnd);
                body.ChipColor=new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f).linear;
            }
            var dev=NativePilotGear.Device(look,plant,float.IsNaN(body.SeatAt)?float.NaN:time-body.SeatAt,time);
            var m=body.GearDevice.localToWorldMatrix;float r=Mathf.Round(Mathf.Clamp(body.GearRadius,.02f,.08f)*1000)/1000;
            gearMatrix=m;sourceModels.Queue(sourceModels.GearDeviceFor(body.GearRadius),m,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,new Vector4(dev.Strips,dev.Strips,dev.Strips,1),null,flash);
            // The bay shows only while the lid is up; the gear's source frame mirrors into Unity's (x toward the back of the forearm).
            if(dev.Lid>.02f){var bay=(dev.BayChip?body.ChipColor:body.AccentLinear)*dev.Bay;sourceModels.Queue(sourceModels.GearBayFor(body.GearRadius),m,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,new Vector4(bay.r,bay.g,bay.b,1),null,flash);}
            var h=NativePilotGear.Hinge(r);var lens=(dev.LensChip?body.ChipColor:body.AccentLinear)*dev.Lens;
            gearMatrix=m*Matrix4x4.TRS(new Vector3(-h.X,h.Y,h.Z),Quaternion.AngleAxis(dev.Lid*NativePilotGear.LidOpenDegrees,Vector3.forward),Vector3.one);
            sourceModels.Queue(NativeSourceModels.GearLid,gearMatrix,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,new Vector4(lens.r,lens.g,lens.b,1),null,flash);
            if(dev.Chip)
            {
                var s=NativePilotGear.Seat(r);var chip=body.ChipColor*1.6f;gearMatrix=m*Matrix4x4.Translate(new Vector3(-s.X,s.Y,s.Z));
                sourceModels.Queue(NativeSourceModels.GearChip,gearMatrix,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,new Vector4(chip.r,chip.g,chip.b,1),null,flash);
            }
        }
        private Func<int,Matrix4x4> gearBone;private Matrix4x4 gearMatrix;
        // The chip bay's top on pilot `id`'s visual body this frame (world): its chip-select hologram is projected from there (the
        // game rig's wrist, which the hologram is posed on, sits elsewhere on a body of other proportions). False without a body
        // drawn this frame with the device.
        private bool VisualEmitter(int id,out Vector3 world)
        {
            world=default;if(!rigs.TryGetValue(id,out var rig)||!visualBodies.TryGetValue(rig,out var body)||body.Root==null||body.GearDevice==null||body.PosedFrame!=Time.frameCount)return false;
            float r=Mathf.Clamp(body.GearRadius,.02f,.08f);
            world=body.GearDevice.TransformPoint(new Vector3(r+NativePilotGear.HousingTop+NativePilotGear.FrameHeight,(NativePilotGear.BayNear+NativePilotGear.BayFar)*.5f,0));return true;
        }
        private Matrix4x4 GearBoneOf(int bone)=>gearMatrix;
        // The back emblem's frame on the chest bone (NativePilotGear: out of the back along the bone's +y, tilted with the cloth;
        // up along its -x).
        private static readonly Matrix4x4 EmblemFrame=EmblemFrameAt(NativePilotGear.EmblemOut,NativePilotGear.EmblemSlope);
        private static Matrix4x4 EmblemFrameAt(float outward,float slope)=>Matrix4x4.TRS(new Vector3(NativePilotGear.EmblemDown,outward,0),
            Quaternion.LookRotation(new Vector3(-slope,1,0).normalized,new Vector3(-1,-slope,0).normalized),Vector3.one);

        // ---- the wardrobe (owner 2026-10-26: "integrate switching the clothes on the model") ----
        // The bake's look data (Resources/ChipArenaVisualLook, written with the bake by the local tool): each wardrobe piece's
        // slot and id (Core/NativeLook.cs), its renderers, the body parts it covers and the tags it sets; the tags the always-worn
        // pieces set (the head's); the body parts' renderers; the tag shapes (a piece's blendshape a worn piece's tag pulls in, as boots tuck trousers); and the gear's
        // anchors measured per top (and hands, for the wrist device's band): the core's and device's local frames, the band's
        // radius, the back emblem's distance and slope. Without it (an older bake) a body is the bake as it is.
        [Serializable] public sealed class LookPiece{public string slot,id;public string[] renderers,covers,tags;}
        [Serializable] public sealed class LookPart{public string renderer,part;}
        [Serializable] public sealed class LookShape{public string renderer,tag;public int shape;}
        [Serializable] public sealed class LookAnchor{public string upper,hands;public float[] core,device,emblem;public float radius;}
        [Serializable] public sealed class LookData{public LookPiece[] pieces;public LookPart[] body;public LookShape[] shapes;public LookAnchor[] anchors;public string[] always;}
        public const string VisualLookResource="ChipArenaVisualLook";
        private static LookData lookData;private static int lookState; // 0 unread, 1 read, -1 none
        private static LookData VisualLookData()
        {
            if(lookState==0)
            {
                var text=Resources.Load<TextAsset>(VisualLookResource);
                try{lookData=text!=null?JsonUtility.FromJson<LookData>(text.text):null;}catch(ArgumentException){lookData=null;}
                lookState=lookData?.pieces!=null&&lookData.pieces.Length>0?1:-1;
            }
            return lookState>0?lookData:null;
        }
        private static readonly HashSet<string> lookCovers=new HashSet<string>(StringComparer.Ordinal),lookTags=new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string,Renderer> lookNamed=new Dictionary<string,Renderer>(StringComparer.Ordinal);
        // A model instance in the outfit `look`: each wardrobe piece's renderers on when worn and off otherwise, the body parts
        // the worn pieces cover off (the rest on), and the tag shapes full where a worn piece sets their tag. Runs when a body is
        // made (once per outfit), never per frame.
        private static void VisualDress(GameObject root,string look)
        {
            var d=VisualLookData();if(d==null||root==null)return;
            lookNamed.Clear();foreach(var r in root.GetComponentsInChildren<Renderer>(true))lookNamed[r.name]=r;
            lookCovers.Clear();lookTags.Clear();if(d.always!=null)lookTags.UnionWith(d.always);
            foreach(var p in d.pieces)if(p!=null&&NativeLook.Of(look,p.slot)==p.id){if(p.covers!=null)lookCovers.UnionWith(p.covers);if(p.tags!=null)lookTags.UnionWith(p.tags);}
            // Every piece off, then the worn ones on (two ids may share a piece, as two hair styles share a fringe).
            foreach(var p in d.pieces)if(p?.renderers!=null)foreach(var n in p.renderers)if(lookNamed.TryGetValue(n,out var r))r.gameObject.SetActive(false);
            foreach(var p in d.pieces)if(p?.renderers!=null&&NativeLook.Of(look,p.slot)==p.id)foreach(var n in p.renderers)if(lookNamed.TryGetValue(n,out var r))r.gameObject.SetActive(true);
            if(d.body!=null)foreach(var b in d.body)if(b!=null&&lookNamed.TryGetValue(b.renderer,out var r))r.gameObject.SetActive(!lookCovers.Contains(b.part));
            if(d.shapes!=null)foreach(var t in d.shapes)if(t!=null&&lookNamed.TryGetValue(t.renderer,out var r)&&r is SkinnedMeshRenderer skin&&skin.sharedMesh!=null&&t.shape>=0&&t.shape<skin.sharedMesh.blendShapeCount)skin.SetBlendShapeWeight(t.shape,lookTags.Contains(t.tag)?100:0);
            lookNamed.Clear();
        }
        // The renderers a dressed instance draws (its worn pieces and uncovered body parts).
        private static Renderer[] VisualWorn(GameObject root)
        {
            var all=root.GetComponentsInChildren<Renderer>(true);int n=0;foreach(var r in all)if(r.gameObject.activeSelf)n++;
            if(n==all.Length)return all;var worn=new Renderer[n];n=0;foreach(var r in all)if(r.gameObject.activeSelf)worn[n++]=r;return worn;
        }
        // The gear's anchors for the body's top (and hands): the core's and device's frames, the band's radius, the emblem's
        // frame; the bake's own (the default outfit's) when the data has none for it.
        private static void VisualAnchorsFor(VisualBody body)
        {
            var d=VisualLookData();if(d?.anchors==null)return;string upper=NativeLook.Of(body.Look,NativeLook.Upper),hands=NativeLook.Of(body.Look,NativeLook.Hands);LookAnchor best=null;
            foreach(var a in d.anchors)if(a!=null&&a.upper==upper&&(best==null||a.hands==hands))best=a;
            if(best==null)return;
            if(body.GearCore!=null&&best.core?.Length==7){body.GearCore.localPosition=new Vector3(best.core[0],best.core[1],best.core[2]);body.GearCore.localRotation=new Quaternion(best.core[3],best.core[4],best.core[5],best.core[6]);}
            if(body.GearDevice!=null&&best.device?.Length==7&&best.radius>.01f){body.GearDevice.localPosition=new Vector3(best.device[0],best.device[1],best.device[2]);body.GearDevice.localRotation=new Quaternion(best.device[3],best.device[4],best.device[5],best.device[6]);body.GearRadius=best.radius;}
            if(best.emblem?.Length==2&&best.emblem[0]>.03f)body.Emblem=EmblemFrameAt(best.emblem[0],best.emblem[1]);
        }
        // The model's head bone (the game's head joint's).
        private static Transform VisualHeadOf(VisualBody body){var of=body.Binding.Rig.Of;int v=CapturedRig.Head<of.Length?of[CapturedRig.Head]:-1;return v>=0&&v<body.Binding.Bones.Length?body.Binding.Bones[v]:null;}
        // The face gear (the concept's mask and visor; Core NativePilotGear) on a model's head bone, for a pilot whose body skin
        // wears it, as its "face" pick has it (both, the visor alone or the mask alone; Core NativeWeaponSkins): the live body
        // (VisualGear), the draft's pilot and a whole corpse.
        // The body skin that wears it on the model: the Prototype (the Standard keeps the model's face as authored, owner 2026-10-22).
        private static bool WearsFaceGear(int bodySkin)=>bodySkin==NativeBodySkins.PrototypeIndex;
        private void VisualFaceGear(Transform head,int id,string accentColor,int layer,Camera camera,Func<int,float> reveal,Func<int,Color> tint,float flash)
        {
            if(head==null||!WearsFaceGear(BodySkin(id))||!sourceModels.PilotFace())return;
            int gear=NativeWeaponSkins.Index(SeatWeaponSkins(id),NativeWeaponSkins.FaceEntry);
            gearBone??=GearBoneOf;gearMatrix=head.localToWorldMatrix;var accent=AccentTint(accentColor);var lit=new Vector4(1,1,1,1);
            if(gear!=NativeWeaponSkins.FaceVisor)sourceModels.Queue(NativeSourceModels.GearMask,gearMatrix,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,lit,null,flash);
            if(gear!=NativeWeaponSkins.FaceMask)sourceModels.Queue(NativeSourceModels.GearVisor,gearMatrix,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,lit,null,flash);
        }

        // Each renderer's materials with the hologram's after them (extra slots draw the mesh again).
        private Material[][] VisualSpawnSlots(VisualBody body)
        {
            var slots=new Material[body.All.Length][];
            for(int k=0;k<slots.Length;k++){var plain=body.Plain[k];var s=new Material[plain.Length+visualGhosts.Length];plain.CopyTo(s,0);visualGhosts.CopyTo(s,plain.Length);slots[k]=s;}
            return slots;
        }
        private Material[] VisualGhosts()
        {
            if(visualGhosts!=null||visualGhostsFailed)return visualGhosts;
            var shader=Resources.Load<Shader>("NativeVisualBodyGhost");
            if(shader==null||!shader.isSupported){visualGhostsFailed=true;Debug.LogWarning("Visual body: NativeVisualBodyGhost unavailable; no spawn-in hologram.");return null;}
            // The split's colours are the voxel pilot's (linear, written raw as its are).
            NativeBlastSim.Linear(NativeCyberTheme.MagentaHex,out double mr,out double mg,out double mb);NativeBlastSim.Linear(NativeCyberTheme.CyanHex,out double cr,out double cg,out double cb);
            visualGhosts=new[]{VisualGhost(shader,"slice",0,Vector4.one),VisualGhost(shader,"split magenta",1,new Vector4((float)mr,(float)mg,(float)mb,1)),VisualGhost(shader,"split cyan",-1,new Vector4((float)cr,(float)cg,(float)cb,1))};
            return visualGhosts;
        }
        private static Material VisualGhost(Shader shader,string name,float side,Vector4 color)
        {
            var m=new Material(shader){name="Visual body hologram "+name,hideFlags=HideFlags.HideAndDontSave};m.SetFloat(GhostSideId,side);m.SetVector(GhostColorId,color);return m;
        }

        // The accent trims' glow (AccentGlow) on the clothing renderers, written only when it changes.
        private void VisualAccentGlow(VisualBody body,int id,float time)
        {
            var p=LoadoutPilot(id);float pulse=p!=null?NativeGlowRules.ArmorPulse(time,p.BlockReady-time,time<p.BlockEnd):1;
            float glow=Mathf.Round(AccentGlow*(EffectsQuality<.5f?AccentGlowReduced:1)*pulse*200)/200;
            if(glow==body.Glow)return;
            body.Glow=glow;
            foreach(var r in body.Cloth){if(r==null)continue;r.GetPropertyBlock(body.Block);body.Block.SetFloat(AccentGlowId,glow);r.SetPropertyBlock(body.Block);}
        }
        /// <summary>NativeCombatGlow: whether pilot `id` is drawn with its visual body this frame and, if so, where its armour light
        /// spills from (in front of the chest core, else the game chest). The voxel armour halos sit at the game rig's joints, so
        /// they are skipped on it; its accent trims glow instead (VisualAccentGlow).</summary>
        private NativeCombatGlow.VisualGlow visualGlow;
        private bool VisualGlowAt(int id,out Vector3 at)
        {
            at=default;if(!rigs.TryGetValue(id,out var rig)||!visualBodies.TryGetValue(rig,out var body)||body.Root==null||!body.Root.activeSelf||body.PosedFrame!=Time.frameCount)return false;
            at=body.GearCore!=null?body.GearCore.position+body.GearCore.forward*.14f:rig.Bones[CapturedRig.Chest].TransformPoint(new Vector3(0,-.05f,.16f));return true;
        }
        // ---- First person (owner 2026-10-23: "First person still shows voxel") ----
        // The view model wears the visual body's forearms and hands, as the voxel view shows the elbows and hands only
        // (NativeViewRules.ViewModelBone): a second instance under the view rig, its skinned meshes cut to the triangles weighted
        // below the elbows (ArmMesh, made once per mesh and kept), on the view model's layer without shadows. It is posed from the
        // view rig and placed so its right hand's weapon frame is where the view rig holds the weapon (the sights, the aim and the
        // muzzle stay the game's); the left hand's item rides the visual left hand, as in third person.
        private VisualBody viewBody;private static readonly Dictionary<Mesh,Mesh> armMeshes=new Dictionary<Mesh,Mesh>();
        private bool VisualViewDrawn(CapturedAnimation.Player rig)=>rig==viewRig&&viewBody!=null&&viewBody.Root!=null&&viewBody.PosedFrame==Time.frameCount;
        // RenderFirstPerson, before the view rig poses: its left hand goes back where its own pose put it.
        private void VisualViewRestore()
        {
            if(viewBody==null||!viewBody.Moved||viewRig==null)return;
            var bone=viewRig.Bones[CapturedRig.LHand];bone.localPosition=viewBody.HandPosition[0];bone.localRotation=viewBody.HandRotation[0];viewBody.Moved=false;
        }
        // RenderFirstPerson, once the view rig is placed (aimed, swayed, chip-select layered): pose and place the view body; false
        // (and hidden) when the voxel arms draw instead (Classic, no bake, the Siren, a scope up).
        private bool VisualViewPose(CapturedAnimation.Player view,Actor a,float time,bool scoped)
        {
            bool on=!Viewer&&sourceModels!=null&&!ClassicBodies&&!scoped&&view!=null&&a.SourceSpec!="siren"&&VisualBodiesOn;
            if(!on){if(viewBody?.Root!=null&&viewBody.Root.activeSelf)viewBody.Root.SetActive(false);return false;}
            if(viewBody?.Root!=null&&!string.Equals(viewBody.Look,LocalLook??"",StringComparison.Ordinal)){viewBody.Root.SetActive(false);Destroy(viewBody.Root);viewBody=null;}
            if(viewBody==null||viewBody.Root==null){viewBody=CreateViewBody(view);if(viewBody==null)return false;}
            var pose=view.Pose;pose.World();
            if(viewBody.PosedFrame<Time.frameCount-1||viewBody.Spec!=a.SourceSpec)viewBody.Binding.State.Forget();
            viewBody.PosedFrame=Time.frameCount;viewBody.Spec=a.SourceSpec;
            viewBody.Binding.Apply(pose,VisualGrasp(a,pose,time,ChipLookOf(a.Id)),false,false);
            if(viewBody.Accent!=a.AccentColor)VisualAccent(viewBody,a.AccentColor);
            if(!viewBody.Root.activeSelf)viewBody.Root.SetActive(true);
            // The body moves (rigidly) so its right weapon frame lands on the view rig's right hand, where the weapon draws.
            var root=viewBody.Root.transform;var hand=view.Bones[CapturedRig.RHand];float s=root.lossyScale.x;
            viewBody.Binding.WeaponFrame(pose,out var rp,out var rq);var turn=hand.rotation*Quaternion.Inverse(rq);
            root.SetPositionAndRotation(hand.position-turn*(rp*s),turn);
            viewBody.Binding.LeftWeaponFrame(pose,out var lp,out var lq);var left=view.Bones[CapturedRig.LHand];
            viewBody.HandPosition[0]=left.localPosition;viewBody.HandRotation[0]=left.localRotation;
            left.SetPositionAndRotation(root.TransformPoint(lp),root.rotation*lq);viewBody.Moved=true;
            return true;
        }
        private VisualBody CreateViewBody(CapturedAnimation.Player view)
        {
            var body=CreateVisualBody(view,ViewBodyId);if(body==null)return null;
            body.Root.name="Visual view body";
            var arm=new HashSet<Transform>();
            foreach(int j in new[]{CapturedRig.LElbow,CapturedRig.RElbow}){int v=body.Binding.Rig.Of[j];if(v>=0)foreach(var t in body.Binding.Bones[v].GetComponentsInChildren<Transform>(true))arm.Add(t);}
            foreach(var t in body.Root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=10;
            foreach(var r in body.All)
            {
                if(r==null)continue;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
                var skin=r as SkinnedMeshRenderer;var cut=skin!=null?ArmMesh(skin,arm):null;
                if(cut==null){r.enabled=false;continue;}
                skin.sharedMesh=cut;skin.updateWhenOffscreen=true;
            }
            body.Root.SetActive(false);return body;
        }
        // The triangles of a skinned mesh whose three corners are weighted half or more to the given bones (below the elbows);
        // null when none are, or the mesh cannot be read (its import keeps it readable).
        private static Mesh ArmMesh(SkinnedMeshRenderer skin,HashSet<Transform> arm)
        {
            var mesh=skin.sharedMesh;if(mesh==null||!mesh.isReadable)return null;
            if(armMeshes.TryGetValue(mesh,out var made))return made;
            var bones=skin.bones;var weights=mesh.boneWeights;Mesh cut=null;
            if(weights.Length==mesh.vertexCount)
            {
                var armBone=new bool[bones.Length];for(int b=0;b<bones.Length;b++)armBone[b]=bones[b]!=null&&arm.Contains(bones[b]);
                float On(int b,float w)=>b>=0&&b<armBone.Length&&armBone[b]?w:0;
                var keep=new bool[weights.Length];
                for(int v=0;v<weights.Length;v++){var w=weights[v];keep[v]=On(w.boneIndex0,w.weight0)+On(w.boneIndex1,w.weight1)+On(w.boneIndex2,w.weight2)+On(w.boneIndex3,w.weight3)>=.5f;}
                var parts=new List<int[]>();int kept=0;
                for(int s=0;s<mesh.subMeshCount;s++)
                {
                    var tris=mesh.GetTriangles(s);var list=new List<int>();
                    for(int i=0;i+2<tris.Length;i+=3)if(keep[tris[i]]&&keep[tris[i+1]]&&keep[tris[i+2]]){list.Add(tris[i]);list.Add(tris[i+1]);list.Add(tris[i+2]);}
                    parts.Add(list.ToArray());kept+=list.Count;
                }
                if(kept>0){cut=Instantiate(mesh);cut.name=mesh.name+" (forearms)";for(int s=0;s<parts.Count;s++)cut.SetTriangles(parts[s],s,false);cut.RecalculateBounds();}
            }
            armMeshes[mesh]=cut;return cut;
        }

        // ---- Death (owner 2026-10-23: "Death turns model into voxel") ----
        // A pilot's ragdoll wears its visual body: an instance posed from the rig at death (the corpse's bind), each bone riding
        // the ragdoll part of the game joint at or above it rigidly (NativeCorpse.PartFrom: the part's motion, the corpse's sink,
        // overlay and shrink), the skeleton scaled by the parts' scale. A cut or crushed corpse (a melee slice, a sever wound)
        // and each half of a split one (a blade's killing blow, a saw) wear it severed at the joints (VisualCorpseSever); a
        // smashed one too (its crushed parts dropped, a pinned one flattened by the smash's overlay). A swarm's collapse shrinks
        // it; the soul and frost looks are its own (VisualCorpseLook). It hides while the voxel body would be under half opacity
        // and goes with its corpse.
        private sealed class VisualCorpse{public GameObject Root;public Transform[] Bones;public int[] Part;public Matrix4x4[] Bind;public int Frame=-1;public Renderer[] All;public float Dissolve=-1;public Color Edge;public List<Mesh> Owned;public int Look;public int Head=-2;
            // Each bone's nearest ancestor in Bones (-1: the root) and its rest offset from it (VisualCorpseShown keeps lengths),
            // and whether it takes its part's place (the root, and a loose piece's first bone).
            public int[] Parent;public Vector3[] Rest;public bool[] Placed;}
        private readonly Dictionary<NativeCorpse,VisualCorpse> visualCorpses=new Dictionary<NativeCorpse,VisualCorpse>();
        private readonly List<NativeCorpse> visualCorpsesGone=new List<NativeCorpse>();
        // A death's visual body: an instance posed from the rig (in its last pose), `offset` from it, in the pilot's accent, without
        // shadows (as the voxel corpse); `joint` holds, per bone, the nearest game joint mapped at or above it (the pelvis for the
        // root). Inactive; null without a bake.
        private GameObject VisualDeathBody(CapturedAnimation.Player rig,NativeDeath death,Vector3 offset,out Transform[] bones,out int[] joint)
        {
            bones=null;joint=null;var root=Instantiate(visualBodyPrefab);root.name="Visual corpse "+death.id;VisualDress(root,VisualLookOf(death.id));var skeleton=root.transform.Find("VisualSkeleton");
            if(skeleton==null){Destroy(root);return null;}
            var at=rig.Root.transform;root.transform.SetPositionAndRotation(at.TransformPoint(new Vector3(0,NativeVisualRig.Floor,0))+offset,at.rotation);
            var binding=new NativeVisualRigBinding(root.transform,skeleton);var pose=rig.Pose;pose.World();
            binding.Apply(pose,VisualGrasp(death.actor??new Actor(),pose,death.at,0),false,false);
            bones=binding.Bones;joint=VisualJoints(binding);
            var block=new MaterialPropertyBlock();ColorUtility.TryParseHtmlString(death.actor?.AccentColor??"#70e1d0",out var accent);
            foreach(var r in VisualWorn(root))
            {
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;if(r is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
                bool cloth=false;foreach(var slot in ClothSlots)if(r.name.StartsWith(slot,StringComparison.Ordinal))cloth=true;
                if(!cloth)continue;r.GetPropertyBlock(block);foreach(int id in AccentChannels)block.SetColor(id,accent);r.SetPropertyBlock(block);
            }
            root.SetActive(false);return root;
        }
        private bool VisualDeathFor(NativeDeath death)=>!ClassicBodies&&VisualBodiesOn&&(death.model??"pilot")=="pilot"&&death.actor?.SourceSpec!="siren";
        // PresentDeath, as a pilot's corpse is made from its rig (still in its last pose).
        private void VisualCorpseMake(NativeCorpse corpse,NativeDeath death,CapturedAnimation.Player rig)
        {
            if(rig==null||!VisualDeathFor(death))return;
            // A split body: each half wears the model's parts on its side of the cut (whole parts: a part the cut crosses goes
            // to the side its joint is on; owner 2026-10-24: "Sever at joints").
            // Within a half, each loose piece (a part whose parent went to the other half) is its own mesh, as below.
            if(corpse.Bisect is BisectFx fx&&corpse.Twin!=null&&fx.Request!=null)
            {
                for(int h=0;h<2;h++)
                {
                    var half=h==0?corpse:corpse.Twin;var c=VisualCorpseFor(half,death,rig,out var joint);if(c==null)continue;
                    var piece=new int[c.Bones.Length];for(int v=0;v<piece.Length;v++)piece[v]=c.Bones[v]!=null&&fx.Request.HalfOf(joint[v],CutNum(c.Bones[v].position))==h?VisualCorpsePiece(half,c.Part[v]):-1;
                    VisualCorpseSever(c,piece);visualCorpses[half]=c;
                }
                return;
            }
            if(corpse.Bisect!=null)return;
            var whole=VisualCorpseFor(corpse,death,rig,out _);if(whole==null)return;
            if(corpse.Severed.Count>0||corpse.CrushPart>=0||corpse.ExtraCrush.Count>0)
            {
                var piece=new int[whole.Bones.Length];for(int v=0;v<piece.Length;v++)piece[v]=VisualCorpsePiece(corpse,whole.Part[v]);
                VisualCorpseSever(whole,piece);
            }
            visualCorpses[corpse]=whole;
        }
        // A ragdoll part's piece of the model: -1 when it is crushed (left to its fragments); else the nearest part at or above
        // it that moves on its own (a cut, a part loose of a crushed part or of the other half; owner 2026-10-27: "Bodies aren't
        // gibbing, they seem to still stretch"), its index + 1, with 0 for the pelvis (the body).
        private static int VisualCorpsePiece(NativeCorpse corpse,int part)
        {
            for(int j=part;j>=0;j=CapturedRig.Bones[j].Parent)
            {
                if(corpse.IsCrushed(j))return -1;
                if(corpse.Severed.Contains(j)||corpse.Parts.ContainsKey(j)&&!corpse.Jointed(j))return j==CapturedRig.Pelvis?0:j+1;
            }
            return 0;
        }
        // The visual body of `corpse` (a whole body or one half): each bone on the ragdoll part of the game joint at or above it
        // (the nearest such part the corpse has).
        private VisualCorpse VisualCorpseFor(NativeCorpse corpse,NativeDeath death,CapturedAnimation.Player rig,out int[] joint)
        {
            var root=VisualDeathBody(rig,death,death.position-rig.Root.transform.position,out var bones,out joint);if(root==null)return null;
            var c=new VisualCorpse{Root=root,Bones=bones,Part=new int[bones.Length],Bind=new Matrix4x4[bones.Length],All=VisualWorn(root),Edge=DissolveEdgeOf(death)};
            for(int v=0;v<bones.Length;v++)
            {
                int part=-1;for(int j=corpse.Anchor[joint[v]];j>=0&&part<0;j=CapturedRig.Bones[j].Parent)if(corpse.Parts.ContainsKey(j))part=j;
                if(part<0)foreach(int any in corpse.Parts.Keys){part=any;break;}
                c.Part[v]=part<0?CapturedRig.Pelvis:part;c.Bind[v]=bones[v].localToWorldMatrix;
            }
            var index=new Dictionary<Transform,int>();for(int v=0;v<bones.Length;v++)if(bones[v]!=null)index[bones[v]]=v;
            c.Parent=new int[bones.Length];c.Rest=new Vector3[bones.Length];c.Placed=new bool[bones.Length];
            for(int v=0;v<bones.Length;v++)
            {
                c.Parent[v]=-1;if(bones[v]==null)continue;c.Rest[v]=bones[v].localPosition;
                for(var t=bones[v].parent;t!=null;t=t.parent)if(index.TryGetValue(t,out int u)){c.Parent[v]=u<v?u:-1;break;}
                // The root, and the first bone of a part that moves on its own (no joint to its parent part: a cut, a part loose of a
                // crushed part or of the other half), take their part's place; every other bone keeps its rest offset.
                int p=c.Parent[v];c.Placed[v]=p<0||c.Part[v]!=c.Part[p]&&(corpse.Severed.Contains(c.Part[v])||!corpse.Jointed(c.Part[v]));
            }
            return c;
        }
        // Slash kills on the model (owner 2026-10-24: "Sever at joints"): each skinned mesh split by piece (per visual bone:
        // `pieceOfBone`, -1 dropped). A vertex belongs to the piece most of its weight is in and keeps only that piece's bones,
        // so a piece moves rigidly with its ragdoll part and nothing stretches across the gap; triangles across pieces go, as
        // do the dropped piece's (a crushed part's fragments and the stumps' blood are the kill wound's; the other half of a
        // split body is its twin's). The cut meshes go with the corpse.
        private void VisualCorpseSever(VisualCorpse c,int[] pieceOfBone){VisualSever(c.All,c.Bones,pieceOfBone,ref c.Owned,true);VisualCorpseBones(c,pieceOfBone);}
        // The cut's bones (owner 2026-10-27: "Can you show bones + flesh on gibs too? correct on anamonics too"; Core
        // NativeModelGore): at each joint the cut parts (a bone whose nearest ancestor went with another piece), each side's bones as
        // short stubs on that side's bone, where they lie in its cross-section (the cut piece's sticking out a little, the stump's
        // flush), their marrow dark at the end. In the body's own shader, added to the corpse's renderers so its dissolve and its
        // kill looks reach them; they go with the corpse.
        private static Mesh goreStub,goreDisc;
        private void VisualCorpseBones(VisualCorpse c,int[] pieceOfBone)
        {
            if(c.Root==null||c.All==null||c.Parent==null)return;Material source=null;
            foreach(var r in c.All)if(r is SkinnedMeshRenderer&&r.sharedMaterial!=null){source=r.sharedMaterial;break;}
            if(source==null)return;
            var boneLook=VisualGoreMaterial(source,NativeModelGore.Bone);var marrowLook=VisualGoreMaterial(source,NativeModelGore.Marrow);
            goreStub??=GoreCylinder();goreDisc??=GoreDisc();
            var root=c.Root.transform;var known=new HashSet<Transform>();foreach(var b in c.Bones)if(b!=null)known.Add(b);
            var added=new List<Renderer>();
            for(int w=0;w<c.Bones.Length;w++)
            {
                int v=c.Parent[w];if(v<0||v>=c.Bones.Length||c.Bones[w]==null||c.Bones[v]==null)continue;
                int pw=pieceOfBone[w],pv=pieceOfBone[v];if(pw==pv)continue;
                Transform limb=c.Bones[w],stump=c.Bones[v];var joint=limb.position;var along=joint-stump.position;if(along.sqrMagnitude<1e-8f)continue;along.Normalize();
                // The cut piece's way: toward its bone's farthest child bone (its limb), or on from the stump's.
                var into=along;string kind=NativeModelGore.Kind(limb.name);
                if(kind!="hand"&&kind!="foot"&&kind!="head"){Vector3 far=Vector3.zero;foreach(Transform ch in limb)if(known.Contains(ch)){var d=ch.position-joint;if(d.sqrMagnitude>far.sqrMagnitude)far=d;}if(far.sqrMagnitude>1e-6f)into=far.normalized;}
                if(pw>=0)Stubs(limb,stump.name,joint,into,NativeModelGore.Out);
                if(pv>=0)Stubs(stump,limb.name,joint,-along,NativeModelGore.Stump);
            }
            if(added.Count>0){var all=new Renderer[c.All.Length+added.Count];c.All.CopyTo(all,0);added.CopyTo(all,c.All.Length);c.All=all;}
            // The bones of `on`'s side through the joint at `joint`, running `into` that side, `stick` metres out of its cut.
            void Stubs(Transform on,string across,Vector3 at,Vector3 inward,float stick)
            {
                var sections=NativeModelGore.At(on.name,across);if(sections.Length==0)return;int side=NativeModelGore.Side(on.name);
                var ahead=Vector3.ProjectOnPlane(root.forward,inward);var outward=Vector3.ProjectOnPlane(root.right*(side<0?-1:1),inward);
                if(ahead.sqrMagnitude<1e-6f||outward.sqrMagnitude<1e-6f)return;ahead.Normalize();outward.Normalize();
                var turn=Quaternion.FromToRotation(Vector3.up,inward);
                foreach(var s in sections)
                {
                    var centre=at+outward*s.Outward+ahead*s.Ahead;var end=centre-inward*stick;var deep=centre+inward*NativeModelGore.Depth(on.name);
                    added.Add(GorePiece("Visual gore bone",goreStub,boneLook,on,(end+deep)*.5f,turn,new Vector3(s.Radius*2,(deep-end).magnitude,s.Radius*2)));
                    added.Add(GorePiece("Visual gore marrow",goreDisc,marrowLook,on,end-inward*.0005f,Quaternion.FromToRotation(Vector3.up,-inward),new Vector3(s.Radius*1.1f,1,s.Radius*1.1f)));
                }
            }
        }
        private static Renderer GorePiece(string name,Mesh mesh,Material look,Transform on,Vector3 at,Quaternion turn,Vector3 size)
        {
            var go=new GameObject(name);go.transform.SetPositionAndRotation(at,turn);go.transform.localScale=size;go.transform.SetParent(on,true);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=look;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return r;
        }
        // A unit cylinder (radius 0.5, height 1 along y about its middle, capped) and a unit disc (radius 0.5 facing +y).
        private static Mesh GoreCylinder()
        {
            const int n=12;var v=new List<Vector3>();var nm=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=n;i++){float a=i*2*Mathf.PI/n;var d=new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f);v.Add(d+Vector3.down*.5f);nm.Add(d*2);v.Add(d+Vector3.up*.5f);nm.Add(d*2);}
            for(int i=0;i<n;i++){int a=i*2;t.AddRange(new[]{a,a+1,a+3,a,a+3,a+2});}
            for(int e=0;e<2;e++)
            {
                float y=e==0?-.5f:.5f;int c=v.Count;v.Add(new Vector3(0,y,0));nm.Add(new Vector3(0,e==0?-1:1,0));
                for(int i=0;i<=n;i++){float a=i*2*Mathf.PI/n;v.Add(new Vector3(Mathf.Cos(a)*.5f,y,Mathf.Sin(a)*.5f));nm.Add(new Vector3(0,e==0?-1:1,0));}
                for(int i=0;i<n;i++){if(e==0)t.AddRange(new[]{c,c+1+i,c+2+i});else t.AddRange(new[]{c,c+2+i,c+1+i});}
            }
            var m=new Mesh{name="Visual gore bone"};m.SetVertices(v);m.SetNormals(nm);m.SetTriangles(t,0);m.RecalculateBounds();return m;
        }
        private static Mesh GoreDisc()
        {
            const int n=12;var v=new List<Vector3>{Vector3.zero};var nm=new List<Vector3>{Vector3.up};var t=new List<int>();
            for(int i=0;i<=n;i++){float a=i*2*Mathf.PI/n;v.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));nm.Add(Vector3.up);}
            for(int i=0;i<n;i++)t.AddRange(new[]{0,2+i,1+i});
            var m=new Mesh{name="Visual gore marrow"};m.SetVertices(v);m.SetNormals(nm);m.SetTriangles(t,0);m.RecalculateBounds();return m;
        }
        // A body material's copy in one colour (sRGB `hex`): both sides, no outline or accent glow; one per material and colour.
        private readonly Dictionary<(Material,int),Material> visualGoreMaterials=new Dictionary<(Material,int),Material>();
        private Material VisualGoreMaterial(Material source,int hex)
        {
            if(visualGoreMaterials.TryGetValue((source,hex),out var m))return m;
            var color=new Color((hex>>16&255)/255f,(hex>>8&255)/255f,(hex&255)/255f);
            m=new Material(source){name=source.name+" (gore "+hex.ToString("x6")+")"};for(int i=1;i<=9;i++)m.SetColor("_Color_"+i,color);
            m.SetFloat("_Cull",0);m.SetFloat("_Cutoff",0);m.SetFloat("_UseOutline",0);m.SetFloat("_AccentGlow",0);
            visualGoreMaterials[(source,hex)]=m;return m;
        }
        // Each skinned renderer of `all` on its pieces (per visual bone of `bones`: `pieceOfBone`, -1 dropped; SeverMesh), the
        // cut meshes added to `owned` (their owner frees them); with `caps`, the holes closed in the cut's material.
        private void VisualSever(Renderer[] all,Transform[] bones,int[] pieceOfBone,ref List<Mesh> owned,bool caps=false)
        {
            var index=new Dictionary<Transform,int>();for(int v=0;v<bones.Length;v++)if(bones[v]!=null)index[bones[v]]=v;
            foreach(var r in all)
            {
                if(!(r is SkinnedMeshRenderer skin)||skin.sharedMesh==null)continue;
                var used=skin.bones;var piece=new int[used.Length];
                for(int i=0;i<used.Length;i++)piece[i]=used[i]!=null&&index.TryGetValue(used[i],out int v)?pieceOfBone[v]:0;
                var cut=SeverMesh(skin.sharedMesh,piece,caps);if(cut==null)continue;skin.sharedMesh=cut;(owned??=new List<Mesh>()).Add(cut);
                var mats=skin.sharedMaterials;
                if(cut.subMeshCount>mats.Length&&mats.Length>0&&mats[0]!=null){var cap=VisualCapMaterial(mats[0]);System.Array.Resize(ref mats,cut.subMeshCount);for(int k=skin.sharedMaterials.Length;k<mats.Length;k++)mats[k]=cap;skin.sharedMaterials=mats;}
            }
        }
        // Per visual bone of `binding`, the game joint mapped at or above it (the pelvis for the root).
        private static int[] VisualJoints(NativeVisualRigBinding binding)
        {
            var bones=binding.Bones;var mapped=new int[bones.Length];var joint=new int[bones.Length];for(int v=0;v<bones.Length;v++)mapped[v]=-1;
            for(int j=0;j<CapturedRig.Bones.Length;j++){int v=binding.Rig.Of[j];if(v>=0&&v<bones.Length)mapped[v]=j;}
            for(int v=0;v<bones.Length;v++){int j=-1;for(int u=v;u>=0&&j<0;u=binding.Rig.Parent[u])j=mapped[u];joint[v]=j>=0?j:CapturedRig.Pelvis;}
            return joint;
        }
        // The sky death broadcast's portrait on the model (owner 2026-10-24: "Model everywhere"): the victim's visual body on the
        // broadcast rig, cut to its bust (the portrait's chest, neck, head and collarbones, NativeBroadcastFx.PortraitBone, with
        // the upper spine and arms: the model's shoulders are there), drawn through the ghost shader in the hologram's colour
        // and scaled BustScale about its neck to fill the frame fitted to the voxel head; null without a bake or under Classic
        // (the voxel hologram).
        private static readonly Color BustColor=new Color(.1221f,.7157f,1);
        private const float BustScale=1.6f;
        private static bool BustBone(int joint)=>NativeBroadcastFx.PortraitBone(joint)||joint==CapturedRig.Spine||joint==CapturedRig.LArm||joint==CapturedRig.RArm;
        private Material bustMaterial;
        private VisualBody VisualBust(CapturedAnimation.Player rig,int id,ref List<Mesh> owned)
        {
            if(Viewer||sourceModels==null||ClassicBodies||!VisualBodiesOn)return null;
            if(bustMaterial==null){var shader=Resources.Load<Shader>("NativeWeaponGhost");if(shader==null||!shader.isSupported)return null;bustMaterial=new Material(shader){name="Visual body bust"};}
            var body=CreateVisualBody(rig,id);if(body==null)return null;body.Root.name="Visual bust "+id;
            var joint=VisualJoints(body.Binding);var piece=new int[joint.Length];for(int v=0;v<piece.Length;v++)piece[v]=BustBone(joint[v])?0:-1;
            body.BustNeck=-1;for(int v=0;v<joint.Length&&body.BustNeck<0;v++)if(joint[v]==CapturedRig.Neck)body.BustNeck=v;
            VisualSever(body.All,body.Binding.Bones,piece,ref owned);
            foreach(var r in body.All)
            {
                if(r==null)continue;var mats=new Material[r.sharedMaterials.Length];for(int k=0;k<mats.Length;k++)mats[k]=bustMaterial;
                r.sharedMaterials=mats;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            body.Root.SetActive(false);return body;
        }
        // The bust in the portrait's pose (its head drifting) at the hologram's opacity, shown this frame.
        private void VisualBustPose(VisualBody body,CapturedRig.Pose pose,float alpha)
        {
            pose.World();body.Binding.Apply(pose,VisualGrasp(new Actor(),pose,0,0),false,false);
            // Scaled about the neck (its place in the body, kept from the first pose).
            if(body.BustNeck>=0)
            {
                var root=body.Root.transform;if(float.IsNaN(body.BustPivot.x))body.BustPivot=root.InverseTransformPoint(body.Binding.Bones[body.BustNeck].position);
                root.localScale=Vector3.one*BustScale;root.localPosition=new Vector3(0,NativeVisualRig.Floor,0)+(1-BustScale)*body.BustPivot;
            }
            if(!body.Root.activeSelf)body.Root.SetActive(true);
            foreach(var r in body.All){if(r==null)continue;r.GetPropertyBlock(body.Block);body.Block.SetVector(VisualEchoTint,new Vector4(BustColor.r,BustColor.g,BustColor.b,alpha));r.SetPropertyBlock(body.Block);}
        }
        // The cut's material (a body material's copy): flesh (Core NativeModelGore; the model is a person: the voxel pilots' dark
        // armoured insides read as a robot's) in every colour, both sides, no cut-out, outline or accent glow; it keeps the
        // shader's dissolve and hit flash. One per body material.
        private static readonly Color CutInside=new Color((NativeModelGore.Flesh>>16&255)/255f,(NativeModelGore.Flesh>>8&255)/255f,(NativeModelGore.Flesh&255)/255f);
        private readonly Dictionary<Material,Material> capMaterials=new Dictionary<Material,Material>();
        private Material VisualCapMaterial(Material source)
        {
            if(capMaterials.TryGetValue(source,out var cap))return cap;
            cap=new Material(source){name=source.name+" (cut)"};for(int i=1;i<=9;i++)cap.SetColor("_Color_"+i,CutInside);
            cap.SetFloat("_Cull",0);cap.SetFloat("_Cutoff",0);cap.SetFloat("_UseOutline",0);cap.SetFloat("_AccentGlow",0);
            capMaterials[source]=cap;return cap;
        }
        // `mesh` with each vertex kept to its heaviest piece's bones (renormalised) and only the triangles within one piece (not
        // the crushed, -1); with `caps`, a last submesh closes each piece's cut holes (SeverCaps). Null when nothing is cut.
        private static Mesh SeverMesh(Mesh mesh,int[] piece,bool caps=false)
        {
            int n=mesh.vertexCount;var per=mesh.GetBonesPerVertex();var all=mesh.GetAllBoneWeights();if(per.Length!=n||all.Length==0)return null;
            var of=new int[n];var counts=new byte[n];var kept=new List<BoneWeight1>(all.Length);var sum=new float[24];bool any=false;
            for(int i=0,o=0;i<n;o+=per[i],i++)
            {
                int count=per[i],best=0;float most=-1;
                for(int j=0;j<count;j++){var w=all[o+j];int p=w.boneIndex<piece.Length?piece[w.boneIndex]:0;sum[p+1]+=w.weight;}
                for(int j=0;j<count;j++){var w=all[o+j];int p=w.boneIndex<piece.Length?piece[w.boneIndex]:0;if(sum[p+1]>most){most=sum[p+1];best=p;}}
                for(int j=0;j<count;j++){var w=all[o+j];int p=w.boneIndex<piece.Length?piece[w.boneIndex]:0;sum[p+1]=0;}
                of[i]=best;if(best!=0)any=true;int start=kept.Count;
                for(int j=0;j<count;j++){var w=all[o+j];int p=w.boneIndex<piece.Length?piece[w.boneIndex]:0;if(p==best)kept.Add(new BoneWeight1{boneIndex=w.boneIndex,weight=w.weight/Mathf.Max(1e-6f,most)});}
                counts[i]=(byte)(kept.Count-start);
            }
            if(!any)return null;
            var cut=Instantiate(mesh);cut.name=mesh.name+" (severed)";
            using(var c=new NativeArray<byte>(counts,Allocator.Temp))using(var w=new NativeArray<BoneWeight1>(kept.ToArray(),Allocator.Temp))cut.SetBoneWeights(c,w);
            var keptTris=new List<int>[mesh.subMeshCount];
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                var tris=mesh.GetTriangles(s);var list=new List<int>(tris.Length);
                for(int t=0;t+2<tris.Length;t+=3){int a=of[tris[t]];if(a>=0&&of[tris[t+1]]==a&&of[tris[t+2]]==a){list.Add(tris[t]);list.Add(tris[t+1]);list.Add(tris[t+2]);}}
                cut.SetTriangles(list,s,false);keptTris[s]=list;
            }
            if(caps){var fill=SeverCaps(mesh,of,keptTris);if(fill.Count>0){cut.subMeshCount=mesh.subMeshCount+1;cut.SetTriangles(fill,mesh.subMeshCount,false);}}
            return cut;
        }
        // The cut's holes in each kept piece: its edges whose neighbour went with another piece (counted across the mesh's seams,
        // vertices welded by position; the mesh's own open edges are not cuts), chained into loops and each filled with a fan
        // on the piece's own vertices (they move with it).
        private static List<int> SeverCaps(Mesh mesh,int[] of,List<int>[] kept)
        {
            var at=mesh.vertices;var weld=new int[at.Length];var ids=new Dictionary<(int,int,int),int>();
            for(int i=0;i<at.Length;i++){var k=((int)Mathf.Round(at[i].x*1e4f),(int)Mathf.Round(at[i].y*1e4f),(int)Mathf.Round(at[i].z*1e4f));if(!ids.TryGetValue(k,out int w))ids[k]=w=ids.Count;weld[i]=w;}
            long Key(int a,int b)=>a<b?(long)a<<32|(uint)b:(long)b<<32|(uint)a;
            var everyEdge=new Dictionary<long,int>();var keptEdge=new Dictionary<long,int>();
            for(int s=0;s<mesh.subMeshCount;s++){var t=mesh.GetTriangles(s);for(int i=0;i+2<t.Length;i+=3)for(int e=0;e<3;e++){long k=Key(weld[t[i+e]],weld[t[i+(e+1)%3]]);everyEdge.TryGetValue(k,out int c);everyEdge[k]=c+1;}}
            foreach(var list in kept)for(int i=0;i+2<list.Count;i+=3)for(int e=0;e<3;e++){long k=Key(weld[list[i+e]],weld[list[i+(e+1)%3]]);keptEdge.TryGetValue(k,out int c);keptEdge[k]=c+1;}
            // Each cut edge, directed as its kept triangle runs: (piece, start) to (end, its start and end vertices).
            var next=new Dictionary<long,(int to,int a)>();
            foreach(var list in kept)for(int i=0;i+2<list.Count;i+=3)
            {
                long piece=of[list[i]]+1;
                for(int e=0;e<3;e++){int a=list[i+e],b=list[i+(e+1)%3];long k=Key(weld[a],weld[b]);if(keptEdge[k]==1&&everyEdge[k]>=2)next[piece<<32|(uint)weld[a]]=(weld[b],a);}
            }
            var fill=new List<int>();var used=new HashSet<long>();var loop=new List<int>();
            foreach(var start in next.Keys)
            {
                if(used.Contains(start))continue;long piece=start>>32,here=start;bool closed=false;loop.Clear();
                for(int guard=0;guard<8192;guard++)
                {
                    if(!next.TryGetValue(here,out var edge)||!used.Add(here))break;loop.Add(edge.a);here=piece<<32|(uint)edge.to;if(here==start){closed=true;break;}
                }
                if(!closed||loop.Count<3)continue;
                for(int i=1;i+1<loop.Count;i++){fill.Add(loop[0]);fill.Add(loop[i+1]);fill.Add(loop[i]);}
            }
            return fill;
        }
        private static void VisualCorpseFree(VisualCorpse c){if(c.Root!=null)Destroy(c.Root);if(c.Owned!=null)foreach(var m in c.Owned)if(m!=null)Destroy(m);c.Owned=null;}
        // A co-op revive on the model (owner 2026-10-24: "Model everywhere"; NativeReviveHoloPresentation.cs): the kept body's
        // joints (`joint`, lifted) on the pilot's visual body, made once per revive from its rig as a death scene's body is; a
        // missing part grows back as its repair solidifies (`grow` per joint, the scale of its subtree's top bone). Hidden once a
        // frame passes without it, dropped 2 s after. False: the voxel body.
        private readonly Dictionary<long,VisualScene> visualRevives=new Dictionary<long,VisualScene>();private readonly List<long> visualRevivesGone=new List<long>();
        private bool VisualReviveBody(long key,CapturedAnimation.Player rig,NativeDeath death,Func<int,Matrix4x4> joint,Func<int,float> grow,float alpha)
        {
            if(Viewer||sourceModels==null||ClassicBodies||!VisualBodiesOn||rig==null||death==null||!VisualDeathFor(death))return false;
            if(!visualRevives.TryGetValue(key,out var s)||s.Root==null)
            {
                var root=VisualDeathBody(rig,death,Vector3.zero,out var bones,out var joints);if(root==null)return false;root.name="Visual revive "+death.id;
                s=new VisualScene{Root=root,Bones=bones,Joint=joints,Bind=new Matrix4x4[bones.Length],All=VisualWorn(root),Edge=DissolveEdgeOf(death),Top=new bool[bones.Length]};
                var index=new Dictionary<Transform,int>();for(int v=0;v<bones.Length;v++)if(bones[v]!=null)index[bones[v]]=v;
                for(int v=0;v<bones.Length;v++){s.Bind[v]=bones[v].localToWorldMatrix;s.Top[v]=bones[v]==null||bones[v].parent==null||!index.TryGetValue(bones[v].parent,out int up)||joints[up]!=joints[v];}
                for(int j=0;j<22;j++)s.Inverse[j]=rig.Bones[j].worldToLocalMatrix;
                visualRevives[key]=s;
            }
            s.Frame=Time.frameCount;s.Seen=Time.unscaledTime;
            if(alpha<(visualDissolve?.01f:.5f)){if(s.Root.activeSelf)s.Root.SetActive(false);return true;}
            if(!s.Root.activeSelf)s.Root.SetActive(true);
            if(visualDissolve)VisualDissolve(s.All,ref s.Dissolve,1-alpha,s.Edge);
            for(int v=0;v<s.Bones.Length;v++)if(s.Top[v])s.Bones[v].localScale=Vector3.one;
            for(int j=0;j<22;j++)s.Now[j]=joint(j)*s.Inverse[j];
            for(int v=0;v<s.Bones.Length;v++){var m=s.Now[s.Joint[v]]*s.Bind[v];s.Bones[v].SetPositionAndRotation(m.GetColumn(3),m.rotation);}
            if(grow!=null)for(int v=0;v<s.Bones.Length;v++){if(!s.Top[v])continue;float g=grow(s.Joint[v]);if(g<1)s.Bones[v].localScale=Vector3.one*Mathf.Max(.001f,g);}
            return true;
        }
        // RenderRevives, after the revivals: a revive body not drawn this frame hides; one unseen 2 s goes.
        private void VisualRevivesShown()
        {
            if(visualRevives.Count==0)return;visualRevivesGone.Clear();
            foreach(var pair in visualRevives)
            {
                var s=pair.Value;if(s.Root==null||Time.unscaledTime-s.Seen>2){visualRevivesGone.Add(pair.Key);continue;}
                if(s.Frame!=Time.frameCount&&s.Root.activeSelf)s.Root.SetActive(false);
            }
            foreach(long key in visualRevivesGone){var root=visualRevives[key].Root;if(root!=null)Destroy(root);visualRevives.Remove(key);}
        }
        // The revived pilot standing up from its kept body (NativeInteractionPresentation StandFromBody writes the rig's bones
        // after the visual body bound the live pose): the visual body bound again from those bones. `make`: a stand-in rig the
        // arena does not pose (the revive gallery) gets its body here, shown this frame. False without one.
        private readonly CapturedRig.Pose standPose=new CapturedRig.Pose();
        private bool VisualStandPose(CapturedAnimation.Player rig,Actor a,float time,bool make=false)
        {
            if(Viewer||sourceModels==null||ClassicBodies||!VisualBodiesOn||rig==null||a==null||a.SourceSpec=="siren")return false;
            VisualBody body;
            if(make){body=VisualBodyOf(rig,a.Id);if(body==null)return false;}
            else if(!visualBodies.TryGetValue(rig,out body)||body.Root==null||body.PosedFrame!=Time.frameCount)return false;
            int n=Mathf.Min(rig.Bones.Length,standPose.Position.Length);
            for(int i=0;i<n;i++){var t=rig.Bones[i];var lp=t.localPosition;var lq=t.localRotation;standPose.Position[i]=CapturedRig.Mirror(new System.Numerics.Vector3(lp.x,lp.y,lp.z));standPose.Rotation[i]=CapturedRig.Mirror(new System.Numerics.Quaternion(lq.x,lq.y,lq.z,lq.w));}
            standPose.World();body.Binding.State.Forget();
            var held=VisualBodyBind(body,rig,a,time,standPose,false,false);var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
            body.Moved=true;if(make)body.Seen=true;return true;
        }
        // The authored death scenes (NativeSceneMotion.cs SourceDeathScene) move the victim's 22 joints from where they were at
        // death: each visual bone follows its joint's motion since then (the scene's joint matrices over the rig's at death),
        // hidden while the scene hides the pelvis or the body is under half opacity. True: the voxel victim and its hat are not
        // drawn. Kept per death until it goes undrawn for 2 s.
        private sealed class VisualScene{public bool[] Top;public GameObject Root;public Transform[] Bones;public int[] Joint;public Matrix4x4[] Bind;public readonly Matrix4x4[] Inverse=new Matrix4x4[22],Now=new Matrix4x4[22];public int Frame=-1;public float Seen;public Renderer[] All;public float Dissolve=-1;public Color Edge;}
        private readonly Dictionary<long,VisualScene> visualScenes=new Dictionary<long,VisualScene>();private readonly List<long> visualScenesGone=new List<long>();
        private bool VisualSceneShown(NativeDeath death,Func<int,Matrix4x4> bone,Func<int,bool> visible,float alpha)
        {
            if(!VisualDeathFor(death))return false;long key=((long)death.id<<32)^(uint)death.seed;
            if(!visualScenes.TryGetValue(key,out var s))
            {
                if(!rigs.TryGetValue(death.id,out var rig))return false;
                var root=VisualDeathBody(rig,death,Vector3.zero,out var bones,out var joint);if(root==null)return false;
                s=new VisualScene{Root=root,Bones=bones,Joint=joint,Bind=new Matrix4x4[bones.Length],All=VisualWorn(root),Edge=DissolveEdgeOf(death)};
                for(int v=0;v<bones.Length;v++)s.Bind[v]=bones[v].localToWorldMatrix;
                for(int j=0;j<22;j++)s.Inverse[j]=rig.Bones[j].worldToLocalMatrix;
                visualScenes[key]=s;
            }
            s.Frame=Time.frameCount;s.Seen=Time.unscaledTime;
            if(alpha<(visualDissolve?.01f:.5f)||!visible(CapturedRig.Pelvis)){if(s.Root.activeSelf)s.Root.SetActive(false);return true;}
            if(!s.Root.activeSelf)s.Root.SetActive(true);
            if(visualDissolve)VisualDissolve(s.All,ref s.Dissolve,1-alpha,s.Edge);
            for(int j=0;j<22;j++)s.Now[j]=bone(j)*s.Inverse[j];
            for(int v=0;v<s.Bones.Length;v++){var m=s.Now[s.Joint[v]]*s.Bind[v];s.Bones[v].SetPositionAndRotation(m.GetColumn(3),m.rotation);}
            return true;
        }
        // PresentDeath, where the body draws: pose the corpse's visual body; true when it stands in for the voxel body this frame
        // (also while hidden for a fade under half opacity).
        private bool VisualCorpseShown(NativeCorpse corpse,float alpha)
        {
            if(!visualCorpses.TryGetValue(corpse,out var c)||c.Root==null)return false;
            c.Frame=Time.frameCount;
            // With the bake's noise the body dissolves as the corpse fades (cells away, the front in the pilot's accent); without, it goes at half.
            if(alpha<(visualDissolve?.01f:.5f)){if(c.Root.activeSelf)c.Root.SetActive(false);return true;}
            if(!c.Root.activeSelf)c.Root.SetActive(true);
            if(visualDissolve)VisualDissolve(c.All,ref c.Dissolve,1-alpha,c.Edge);
            // A pinned smash's flattening (a scale about its pivot) rides the root: the bones are placed without it, the root then
            // takes it, so the skinned body flattens as the voxel parts do.
            var root=c.Root.transform;var overlay=corpse.Smash!=null?corpse.Overlay:Matrix4x4.identity;bool flat=!overlay.isIdentity;var undo=flat?overlay.inverse:Matrix4x4.identity;
            Vector3 at=(undo*corpse.PartFrom(c.Part[0])*c.Bind[0]).GetColumn(3);root.SetPositionAndRotation(at,Quaternion.identity);root.localScale=Vector3.one;
            c.Bones[0].localScale=Vector3.one*Mathf.Max(.001f,corpse.PartScale);
            // Every corpse keeps the model's lengths, a split body's halves too (the ragdoll's parts drift apart as it falls, and
            // placing every bone on its part stretched the skin between them; owner 2026-10-27: "they seem to still stretch"):
            // each bone takes its part's turn and keeps its rest offset from its parent; only the root and a loose piece's
            // first bone (Placed) take their part's place, and each loose piece is its own mesh (VisualCorpsePiece).
            for(int v=0;v<c.Bones.Length;v++)
            {
                var b=c.Bones[v];if(b==null)continue;var m=undo*corpse.PartFrom(c.Part[v])*c.Bind[v];
                if(c.Placed[v])b.SetPositionAndRotation(m.GetColumn(3),m.rotation);else{b.localPosition=c.Rest[v];b.rotation=m.rotation;}
            }
            if(flat){var scale=new Vector3(overlay.GetColumn(0).magnitude,overlay.GetColumn(1).magnitude,overlay.GetColumn(2).magnitude);root.localScale=scale;root.position=(Vector3)overlay.GetColumn(3)+Vector3.Scale(scale,at);}
            // The face gear stays on a whole corpse's head as it fades (not on a split half, a crushed head or under a kill look).
            if(c.Look==0&&corpse.Twin==null&&corpse.Host==null&&!corpse.IsCrushed(CapturedRig.Head)&&corpse.Death!=null)
            {
                if(c.Head==-2)c.Head=Array.FindIndex(c.Bones,b=>b!=null&&b.name=="head");
                if(c.Head>=0){corpseFade=alpha;VisualFaceGear(c.Bones[c.Head],corpse.Death.id,corpse.Death.actor?.AccentColor,0,null,alpha<1?corpseFadeOf??=_=>corpseFade:null,null,0);}
            }
            return true;
        }
        private float corpseFade;private Func<int,float> corpseFadeOf;
        // A kill look on the corpse's visual body (owner 2026-10-24: "Model everywhere"): 1 Soul Drift, the body through the ghost
        // shader in the soul's colour at its opacity; 2 Frostbreak, every colour the ice's. False without one (the voxel look).
        private static readonly int[] BodyColours={Shader.PropertyToID("_Color_1"),Shader.PropertyToID("_Color_2"),Shader.PropertyToID("_Color_3"),Shader.PropertyToID("_Color_4"),Shader.PropertyToID("_Color_5"),Shader.PropertyToID("_Color_6"),Shader.PropertyToID("_Color_7"),Shader.PropertyToID("_Color_8"),Shader.PropertyToID("_Color_9")};
        private MaterialPropertyBlock lookBlock;
        private bool VisualCorpseLook(NativeCorpse corpse,int look,Color colour,float opacity)
        {
            if(ClassicBodies||!visualCorpses.TryGetValue(corpse,out var c)||c.Root==null)return false;
            lookBlock??=new MaterialPropertyBlock();
            if(look==1)
            {
                if(c.Look!=1){var ghost=VisualEchoMaterial("NativeWeaponGhost");if(ghost==null)return false;foreach(var r in c.All){if(r==null)continue;var mats=new Material[r.sharedMaterials.Length];for(int k=0;k<mats.Length;k++)mats[k]=ghost;r.sharedMaterials=mats;}c.Look=1;}
                foreach(var r in c.All){if(r==null)continue;r.GetPropertyBlock(lookBlock);lookBlock.SetVector(VisualEchoTint,new Vector4(colour.r,colour.g,colour.b,opacity*VisualEchoShare));r.SetPropertyBlock(lookBlock);}
                return VisualCorpseShown(corpse,1);
            }
            if(c.Look!=2){foreach(var r in c.All){if(r==null)continue;r.GetPropertyBlock(lookBlock);foreach(int id in BodyColours)lookBlock.SetColor(id,colour);lookBlock.SetFloat(AccentGlowId,0);r.SetPropertyBlock(lookBlock);}c.Look=2;}
            return VisualCorpseShown(corpse,opacity);
        }
        // A dead body's dissolve (0 whole to 1 gone; NativeVisualBodyFx.cginc DissolveFront) on its renderers, written when it moves.
        private MaterialPropertyBlock dissolveBlock;
        private void VisualDissolve(Renderer[] all,ref float last,float amount,Color edge)
        {
            amount=Mathf.Round(Mathf.Clamp01(amount)*64)/64;if(amount==last)return;last=amount;dissolveBlock??=new MaterialPropertyBlock();
            foreach(var r in all){if(r==null)continue;r.GetPropertyBlock(dissolveBlock);dissolveBlock.SetFloat(DissolveId,amount);dissolveBlock.SetColor(DissolveEdgeId,edge);r.SetPropertyBlock(dissolveBlock);}
        }
        private static Color DissolveEdgeOf(NativeDeath death){if(!ColorUtility.TryParseHtmlString(death.actor?.AccentColor??"#70e1d0",out var c))c=new Color(.44f,.88f,.82f);return c.linear*DissolveGlow;}
        // After the deaths are presented: hide the corpse bodies not drawn this frame; drop those whose corpse is gone.
        private void VisualCorpsesShown()
        {
            if(visualCorpses.Count==0&&visualScenes.Count==0)return;
            visualCorpsesGone.Clear();
            foreach(var pair in visualCorpses)
            {
                bool kept=false;foreach(var c in corpses.Values)if(c==pair.Key||c.Twin==pair.Key){kept=true;break;}
                if(!kept||pair.Value.Root==null){visualCorpsesGone.Add(pair.Key);continue;}
                if(pair.Value.Frame!=Time.frameCount&&pair.Value.Root.activeSelf)pair.Value.Root.SetActive(false);
            }
            foreach(var gone in visualCorpsesGone){VisualCorpseFree(visualCorpses[gone]);visualCorpses.Remove(gone);}
            if(visualScenes.Count==0)return;
            visualScenesGone.Clear();
            foreach(var pair in visualScenes)
            {
                var s=pair.Value;if(s.Root==null||Time.unscaledTime-s.Seen>2){visualScenesGone.Add(pair.Key);continue;}
                if(s.Frame!=Time.frameCount&&s.Root.activeSelf)s.Root.SetActive(false);
            }
            foreach(var gone in visualScenesGone){var root=visualScenes[gone].Root;if(root!=null)Destroy(root);visualScenes.Remove(gone);}
        }
        private void ClearVisualCorpses()
        {
            foreach(var c in visualCorpses.Values)VisualCorpseFree(c);visualCorpses.Clear();
            foreach(var s in visualScenes.Values)if(s.Root!=null)Destroy(s.Root);visualScenes.Clear();
        }

        // While drawing a third-person arena pilot (SourceCharacter): whether its visual body replaces the voxel one this frame.
        private bool VisualBodyDrawn(CapturedAnimation.Player rig)
        {
            if(!visualBodies.TryGetValue(rig,out var body)||body.Root==null||body.PosedFrame!=Time.frameCount)return false;
            body.Seen=true;return true;
        }

        // After the pilots are drawn: show the visual bodies drawn this frame and hide the rest (dead, first person, hidden). The
        // first-person body is posed after this (RenderFirstPerson), so it hides once a frame has passed without it.
        private void VisualBodiesShown()
        {
            if(viewBody!=null&&viewBody.Root!=null&&viewBody.Root.activeSelf&&viewBody.PosedFrame<Time.frameCount-1)viewBody.Root.SetActive(false);
            if(visualBodies.Count==0)return;
            visualBodiesGone.Clear();
            foreach(var pair in visualBodies)
            {
                var body=pair.Value;
                if(body.Root==null){VisualStrideRelease(body);VisualMotionRelease(body);visualBodiesGone.Add(pair.Key);continue;}
                if(body.Root.activeSelf!=body.Seen)body.Root.SetActive(body.Seen);
                // Its cloth runs while it shows, off the low tier and within Far of the arena's camera.
                if(body.ClothSim!=null)
                {
                    bool sim=body.Seen&&!LowGraphics&&(VisualEye==null||(VisualEye.transform.position-body.At).sqrMagnitude<NativeVisualCloth.Far*NativeVisualCloth.Far);
                    if((sim?1:0)!=body.ClothOn){body.ClothOn=sim?1:0;NativeVisualCloth.Enable?.Invoke(body.ClothSim,sim);}
                }
                VisualWeaponShown(body);body.Seen=false;
            }
            foreach(var rig in visualBodiesGone)visualBodies.Remove(rig);
            VisualLiveGhostsShown();
        }
        // A snapshot of the model `rig` draws this frame into `echo` (made on first use, its meshes reused); false when the rig
        // draws voxel (the caller snapshots the voxel ghost instead).
        private bool VisualEchoSnap(CapturedAnimation.Player rig,ref VisualEcho echo)
        {
            if(rig==null||!visualBodies.TryGetValue(rig,out var body)||body.Root==null||!body.Root.activeInHierarchy||body.PosedFrame<Time.frameCount-1||body.All==null)return false;
            echo??=new VisualEcho();int n=body.All.Length;
            if(echo.Meshes.Length<n){System.Array.Resize(ref echo.Baked,n);System.Array.Resize(ref echo.Meshes,n);System.Array.Resize(ref echo.At,n);}
            int k=0;
            foreach(var r in body.All)
            {
                if(r==null||!r.enabled||!r.gameObject.activeInHierarchy)continue;
                if(r is SkinnedMeshRenderer skin)
                {
                    if(echo.Baked[k]==null)echo.Baked[k]=new Mesh{name="Visual body echo"};skin.BakeMesh(echo.Baked[k],true);
                    var t=skin.transform;echo.Meshes[k]=echo.Baked[k];echo.At[k]=Matrix4x4.TRS(t.position,t.rotation,Vector3.one);
                }
                else if(r.TryGetComponent<MeshFilter>(out var filter)&&filter.sharedMesh!=null){echo.Meshes[k]=filter.sharedMesh;echo.At[k]=r.transform.localToWorldMatrix;}
                else continue;
                k++;
            }
            echo.Count=k;return k>0;
        }
        // The snapshot drawn through `shader` (a ghost shader taking _BodyTint: linear colour, opacity).
        private void VisualEchoDraw(VisualEcho echo,string shader,Color tint,float opacity)
        {
            if(echo==null||echo.Count==0||!(opacity>0))return;
            var material=VisualEchoMaterial(shader);if(material==null)return;visualEchoBlock??=new MaterialPropertyBlock();visualEchoBlock.SetVector(VisualEchoTint,new Vector4(tint.r,tint.g,tint.b,opacity*VisualEchoShare));
            for(int i=0;i<echo.Count;i++){var mesh=echo.Meshes[i];if(mesh==null)continue;for(int sub=0;sub<mesh.subMeshCount;sub++)Graphics.DrawMesh(mesh,echo.At[i],material,0,null,sub,visualEchoBlock);}
        }
        // The ghost shader's material for the model's echoes and live ghosts (a little toward the eye where the shader has the
        // offset); null where the shader is missing.
        private Material VisualEchoMaterial(string shader)
        {
            if(!visualEchoMaterials.TryGetValue(shader,out var material))
            {
                var s=Resources.Load<Shader>(shader);material=s!=null&&s.isSupported?new Material(s){name="Visual body echo"}:null;
                if(material!=null)material.SetFloat("_EchoOffset",VisualEchoOffset);visualEchoMaterials[shader]=material;
            }
            return material;
        }
        // Live ghosts of the model (owner 2026-10-24: "Model everywhere"): a see-through copy of a pilot posed every frame from a
        // pose (the Shadow Step decoy, the entrance's afterimages and hologram, the Cyber Lord's glitch copies), one visual body
        // per key on its holder rig (moved by `shift`), through that ghost's own shader in its tint, opacity and `accent` (the
        // entrance ghost's scanlines). Body only: the ghost weapons stay the voxel ones. It hides once a frame passes without it
        // (VisualLiveGhostsShown) and goes with its holder. False under Classic or without a bake: the caller draws voxel.
        private sealed class VisualLiveGhost{public VisualBody Body;public int Frame;public CapturedAnimation.Player Holder;public string Shader;}
        private readonly Dictionary<long,VisualLiveGhost> visualLiveGhosts=new Dictionary<long,VisualLiveGhost>();private readonly List<long> visualLiveGhostsGone=new List<long>();
        private static readonly int VisualGhostAccent=Shader.PropertyToID("_AccentTint");
        private bool VisualLiveGhostDraw(long key,CapturedAnimation.Player holder,int id,CapturedRig.Pose pose,Vector3 shift,string shader,Color tint,float opacity,Vector4 accent=default)
        {
            if(Viewer||sourceModels==null||ClassicBodies||!VisualBodiesOn||holder==null||pose==null)return false;
            if(!visualLiveGhosts.TryGetValue(key,out var g)||g.Body?.Root==null||g.Holder!=holder||g.Shader!=shader)
            {
                if(g?.Body?.Root!=null)Destroy(g.Body.Root);
                var material=VisualEchoMaterial(shader);if(material==null)return false;
                var body=CreateVisualBody(holder,id);if(body==null)return false;body.Root.name="Visual ghost "+id;
                foreach(var r in body.All)
                {
                    if(r==null)continue;var mats=new Material[r.sharedMaterials.Length];for(int k=0;k<mats.Length;k++)mats[k]=material;
                    r.sharedMaterials=mats;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                visualLiveGhosts[key]=g=new VisualLiveGhost{Body=body,Holder=holder,Shader=shader};
            }
            var b=g.Body;pose.World();b.Binding.Apply(pose,VisualGrasp(new Actor(),pose,0,0),false,false);
            var root=b.Root.transform;root.localPosition=new Vector3(0,NativeVisualRig.Floor,0);if(shift!=Vector3.zero)root.position+=shift;
            if(!b.Root.activeSelf)b.Root.SetActive(true);g.Frame=Time.frameCount;
            foreach(var r in b.All)
            {
                if(r==null)continue;r.GetPropertyBlock(b.Block);b.Block.SetVector(VisualEchoTint,new Vector4(tint.r,tint.g,tint.b,opacity*VisualEchoShare));b.Block.SetVector(VisualGhostAccent,accent);r.SetPropertyBlock(b.Block);
            }
            return true;
        }
        // Whether `rig` drew its visual body this frame (no mark: the ghosts' test whether to follow it).
        private bool VisualBodyPosed(CapturedAnimation.Player rig)=>rig!=null&&visualBodies.TryGetValue(rig,out var body)&&body.Root!=null&&body.PosedFrame==Time.frameCount;
        // VisualBodiesShown: a live ghost not drawn last frame or this one hides; one whose holder is gone is dropped.
        private void VisualLiveGhostsShown()
        {
            if(visualLiveGhosts.Count==0)return;visualLiveGhostsGone.Clear();
            foreach(var pair in visualLiveGhosts)
            {
                var b=pair.Value.Body;if(b?.Root==null){visualLiveGhostsGone.Add(pair.Key);continue;}
                if(pair.Value.Frame<Time.frameCount-1&&b.Root.activeSelf)b.Root.SetActive(false);
            }
            foreach(long key in visualLiveGhostsGone)visualLiveGhosts.Remove(key);
        }
        // The bodies live under their rigs' roots, so disposing a rig destroys its body; this drops the records.
        private void ClearVisualBodies()
        {
            foreach(var body in visualBodies.Values){VisualStrideRelease(body);VisualMotionRelease(body);if(body.Root!=null)Destroy(body.Root);}visualBodies.Clear();
            if(viewBody?.Root!=null)Destroy(viewBody.Root);viewBody=null;
            if(visualGhosts!=null)foreach(var m in visualGhosts)if(m!=null)Destroy(m);visualGhosts=null;
        }
    }
}
