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
            public string Spec, Accent; public int PosedFrame=-10; public bool Seen, Moved, SpawnOn, Ghosted, RevealOn; public Vector3 At; public float Flash=1, Glow=-1;
            public Material[][] Plain, Spawning;
            // The gear's anchors (the bake's VisualGearCore and VisualGearDevice, the band's radius from VisualGearDeviceRim), when
            // the body last appeared or finished forming (the core's flare), the last seated chip (its plant's key, when, colour)
            // and the pilot's accent (linear, the bay's and lens's rest colour).
            public Transform GearCore, GearDevice; public float GearRadius, LitAt=float.NaN, SeatAt=float.NaN; public int SeatKey=int.MinValue; public bool Building;
            // The outfit it wears (Core/NativeLook.cs; VisualDress) and its back emblem's frame on the chest bone for that top.
            public string Look=""; public Matrix4x4 Emblem=EmblemFrame;
            // Its hair and cloth simulation (NativeVisualCloth: the local install's; null without one) and whether it runs (-1 unset).
            public object ClothSim; public int ClothOn=-1;
            // Its hair under a hat (VisualHatHair): the renderers, each one's hood shape (-1: none) and the outfit's weight on it, each
            // one's own mesh, and the hat they are worn under (null: as styled).
            public SkinnedMeshRenderer[] HatHair; public int[] HatHairShape; public float[] HatHairRest; public Mesh[] HatHairMesh; public string HatHairWorn;
            // Whether its pilot wears the robot's helmet (Core NativeRobotHelm; set as it is posed, VisualBodyBind): its hair hidden
            // under it, a hat on its shell.
            public bool Helm;
            // Its Sheldon pieces' renderers (Core NativeSheldonWear; VisualDress), the glow they last took (VisualSheldonGlow) and
            // the lift its feet piece set the skeleton by (VisualLiftOf: bare feet down onto the floor).
            public Renderer[] Sheldon=Array.Empty<Renderer>(); public string SheldonGlow; public float Lift;
            // Its face (Core NativeFace; NativeVisualFacePresentation): made on its first pose from its worn face meshes.
            public VisualFaceRig Face;
            // Whether it was made for a pilot in the Sheldon Head (VisualHooded) and its cloth hood (NativeSheldonHoodPresentation.cs).
            public bool Hood; public SkinnedMeshRenderer HoodRenderer;
            public Color ChipColor=Color.cyan, AccentLinear=Color.cyan;
            public readonly Vector3[] HandPosition=new Vector3[2]; public readonly Quaternion[] HandRotation=new Quaternion[2];
            // Its own weapons (NativeVisualWeaponPresentation.cs): the bake's entries by spec, the one in hand this frame (Held,
            // fitted on the right hand bone, whose scale RightScale restores) and the one shown (when). LeftScale: the first-person view
            // rig's left hand scale, which VisualViewRestore puts back (NativeFirstPersonPose.ModelView).
            public Dictionary<string,VisualWeapon> Weapons; public VisualWeapon Held, Shown; public int ShownFrame=-1; public Vector3 RightScale=Vector3.one, LeftScale=Vector3.one;
            // An arena pilot's own body (its rig the arena poses: the pack bank's weapons draw on it, NativePackWeaponPresentation.cs),
            // those made for it by spec (null: none for that spec), and whether its main dagger is thrown this frame.
            public bool Arena, MainAway; public Dictionary<string,VisualWeapon> Packs;
            // Its stride: the pack's locomotion clips on a proxy skeleton, for the legs (NativeVisualStridePresentation.cs).
            public VisualStride Stride;
            // Its own animation: the pack's clips whole for a weapon whose set has moved over (NativeVisualMotionPresentation.cs).
            public VisualMotion Motion;
            // Its open wounds' holes and its skeleton, on while one shows (NativeVisualWoundPresentation.cs; not in All).
            public List<VisualHole> Holes; public SkinnedMeshRenderer Skeleton;
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
        // The look a pilot's body is dressed in: its outfit with each robot piece as the garment under it (Core NativeRobotSuit.Dress;
        // its plates: NativeRobotSuitPresentation), made once an outfit (read every frame).
        private readonly Dictionary<string,string> robotDressed=new Dictionary<string,string>(StringComparer.Ordinal);
        private string VisualLookOf(int id)
        {
            var look=VisualOutfitOf(id)??"";
            if(!robotDressed.TryGetValue(look,out var dressed)){if(robotDressed.Count>64)robotDressed.Clear();robotDressed[look]=dressed=NativeRobotSuit.Dress(look);}
            return dressed;
        }
        private string VisualOutfitOf(int id)
        {
            if(id==ViewBodyId||id==RooftopIdTop)return LocalLook??"";
            // The rooftop's crowd keeps the concept's clothes (Runtime/NativeRooftopPresentation.cs).
            if(id<RooftopIdTop&&id>RooftopIdTop-8)return NativeLook.Concept;
            if(DeployOutfit(id,out var deploy))return deploy;
            if(ModelPortraitOf(id,out var portrait))return portrait.Outfit;
            // Use the displayed arena, including replay/guest seats, rather than current local preferences.
            var seat=WeaponLoadout?.Match?.Seat(id);if(seat!=null)return seat.look??"";
            return id==LocalId?LocalLook??"":"";
        }
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
                    if(visualBodyState==1)VisualWarmDeaths();
                }
                return visualBodyState==1;
            }
        }

        // The death code compiled ahead (owner 2026-10-28: "prevent fps drops during enemy kill"): the first kill of a session
        // ran it all for the first time, and compiling it cost the frame some 60 ms. On a worker, once the model bodies load:
        // every method of the corpse, bisect, kill effect, blood and gore types (and the presentation's nested ones), and every
        // other method of the game's whose name is about a death, a corpse, a cut, gore, blood, a hat, the broadcast or the visual
        // body (generic ones are left to their first call), each compiled by taking its entry point. Nothing runs; a failure is
        // only the old first-kill cost.
        private static bool visualWarmed;
        private static void VisualWarmDeaths()
        {
            if(visualWarmed||Application.platform==RuntimePlatform.WebGLPlayer)return;visualWarmed=true;
            try{System.Threading.Tasks.Task.Run(()=>
            {
                const System.Reflection.BindingFlags All=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.DeclaredOnly;
                string[] kinds={"Corpse","Bisect","Kill","Blood","Bleed","Gore","Slice","Smash","Sever","Death","Broadcast","Wound","Cut","Spray","Hat","Bust","Ragdoll","Blow","Fountain","Split","Settle","Swarm","Debris","Dissolve","Visual"};
                bool Named(string n){foreach(var k in kinds)if(n.IndexOf(k,StringComparison.Ordinal)>=0)return true;return false;}
                var arena=typeof(ArenaPresentation);
                foreach(var t in arena.Assembly.GetTypes())
                {
                    if(t.Namespace!=arena.Namespace||t.ContainsGenericParameters)continue;bool every=t!=arena&&(t.DeclaringType==arena||Named(t.Name));
                    foreach(var m in t.GetMethods(All))
                    {
                        if(m.IsAbstract||m.ContainsGenericParameters||!every&&!Named(m.Name))continue;
                        try{m.MethodHandle.GetFunctionPointer();}catch(Exception){}
                    }
                    if(every)foreach(var c in t.GetConstructors(All))try{c.MethodHandle.GetFunctionPointer();}catch(Exception){}
                }
            });}catch(Exception){}
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
            var body=VisualBodyOf(rig,a.Id,VisualHooded(a));if(body==null)return;
            var pose=rig.Pose;pose.World();
            bool roll=a.Rolling(time),juggle=NativeJugglePose.Active(a.JuggleUntil,time);
            // (While the pack's clips covered the whole body last frame they overwrite the arms: no relaxing them.)
            var held=VisualBodyBind(body,rig,a,time,pose,a.Grounded&&!roll&&!juggle,roll||juggle,body.Motion!=null&&body.Motion.Weight>=1?VisualArms.Reach:VisualArms.Live);
            VisualStridePose(body,rig,a,time,roll,juggle);
            VisualMotionPose(body,rig,a,time);
            var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
            body.Moved=true;
        }

        // The visual body `rig` wears (made on first use for pilot `id`'s outfit, with the Sheldon hood when `hood`); null without
        // a bake.
        private VisualBody VisualBodyOf(CapturedAnimation.Player rig,int id,bool hood)
        {
            // A changed outfit or hood makes the body again in it (the Locker's picks apply in the lobby).
            if(visualBodies.TryGetValue(rig,out var body)&&body.Root!=null&&(body.Hood!=hood||!VisualRetone(body,VisualLookOf(id)))){VisualStrideRelease(body);VisualMotionRelease(body);VisualBodyGone(body.Root);body.Root=null;}
            if(body==null||body.Root==null){body=CreateVisualBody(rig,id,hood);if(body==null)return null;body.Arena=rigs.TryGetValue(id,out var own)&&own==rig;visualBodies[rig]=body;}
            return body;
        }
        // How a bind places the arms on the model (owner 2026-10-28: "Fix idle animations, lobby card poses, and intros to fully
        // support the new player model"; Core NativeVisualRig.Relax): Live, the free arms re-placed by the model's own body (the
        // arena, the intros, a revive); Still, the arms holding a weapon too (a menu stance: the portraits, the rooftop); Unarmed,
        // Live with nothing in the hands (an entrance that shows no weapon, whose fists closed on the pilot's unseen one);
        // Reach, the game's arms as the voxel body takes them (the draft's hands reach for the chips on screen).
        private enum VisualArms { Live, Still, Unarmed, Reach }
        // The body bound to `pose` (feet planted when `planted`, the airborne solve when `airborne`), its weapon in hand and the
        // rig's hands onto its weapon frames; its held weapon.
        private VisualWeapon VisualBodyBind(VisualBody body,CapturedAnimation.Player rig,Actor a,float time,CapturedRig.Pose pose,bool planted,bool airborne,VisualArms arms=VisualArms.Live,float support=0)
        {
            Vector3 at=rig.Root.transform.position;
            // A fresh solve after a gap (death, first person, a hidden frame), a teleport or a weapon change.
            bool fresh=body.PosedFrame<Time.frameCount-1;
            if(fresh||body.Spec!=a.SourceSpec||(at-body.At).sqrMagnitude>4){body.Binding.State.Forget();if(body.ClothSim!=null)NativeVisualCloth.Reset?.Invoke(body.ClothSim);}
            body.PosedFrame=Time.frameCount;body.Spec=a.SourceSpec;body.At=at;
            body.Helm=!body.Hood&&WearsHelm(a.Id);
            // Its own weapon in hand, if the bake carries one: the grasp on its handguard and stock.
            var held=body.Held=VisualWeaponFor(body,a);var grasp=VisualGrasp(a,pose,time,ChipLookOf(a.Id));if(held!=null)VisualWeaponGrasp(ref grasp,held);
            VisualArmsOf(ref grasp,a,arms,ChipLookOf(a.Id));
            // The draft's plant on the device (`support`): the left hand keeps the stage's offset from the right wrist on the
            // model's (the stage's palm is on the bay), so the model's palm meets its own bay whatever its arms' proportions.
            if(support>0){grasp.Support=support;grasp.SupportHold=default;grasp.Bound=true;}
            body.Binding.Apply(pose,grasp,planted,airborne);
            VisualFacePilot(body,a,time,fresh);
            if(body.Accent!=a.AccentColor)VisualAccent(body,a.AccentColor);
            if(body.Sheldon.Length>0)VisualSheldonGlow(body,NativeBodySkins.SheldonGlow(a.Hat,a.HatColor,a.AccentColor));
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
        // The arms' mode on the grasp: the free arms relaxed (and the held ones for a Still stance), except while the chip-select
        // look is up (the left hand taps the right wrist's device) and in Reach; Unarmed lets go of the unseen weapon (the
        // fighters keep their fists). A caster's staff and the bow ride the left hand unseated: it carries them.
        private static void VisualArmsOf(ref NativeVisualRig.Grasp g,Actor a,VisualArms arms,float chip)
        {
            if(arms==VisualArms.Unarmed){bool fists=NativeLobbyPoses.Fighter(a.SourceSpec);g=new NativeVisualRig.Grasp{RightGrip=fists?g.RightGrip:0,LeftGrip=fists?g.LeftGrip:0};}
            else g.LeftCarries=!a.WeaponAway&&NativeLobbyPoses.HeldBone(a.SourceSpec)==CapturedRig.LHand;
            float relax=arms==VisualArms.Reach?0:1-Mathf.Clamp01(chip);g.Relax=relax;g.Still=arms==VisualArms.Still?relax:0;
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
            {var head=VisualHeadOf(body);if(head!=null)return VisualHatFit(head,hat,body.Helm);}
            return VisualJointFrame(rig,b);
        }
        // Hat `hat` fitted to the model's head bone `head` (the Sheldon Head's face on its own fit), on the robot's helmet when
        // `helm` (Core NativeRobotHelm.HatFit).
        private static Matrix4x4 VisualHatFit(Transform head,string hat,bool helm=false)
        {
            if(hat==NativeSheldonHead.Hat){var k=NativeModelHats.SheldonScale;var o=NativeModelHats.SheldonOffset;return head.localToWorldMatrix*Matrix4x4.TRS(new Vector3(o.X,o.Y,o.Z),Quaternion.identity,new Vector3(k.X,k.Y,k.Z));}
            var frame=helm?head.localToWorldMatrix*HelmHatFit(hat):head.localToWorldMatrix;
            // (A hat made for the model is authored in its head bone's frame.)
            if(NativeModelHatMeshes.Has(hat))return frame;
            NativeModelHats.Fit(hat,out float scale,out var offset);return frame*Matrix4x4.TRS(new Vector3(offset.X,offset.Y,offset.Z),Quaternion.identity,Vector3.one*scale);
        }
        // The worn hair under a hat (owner 2026-10-30: "Hats still cutting hair completely": the pack's one under-hat shape laid
        // the whole style flat, so the scalp showed): a hat made for the model holds it as its own crown would
        // (NativeModelHatMeshes.HairPress: the hair over its band tucked inside it, the hair under its band and brim as styled),
        // each hair piece drawn with its mesh so pressed (VisualPressedHair); the Sheldon Head's hood lays it flat with the pack's
        // own hood shape ("Tag_Hood"). Under the robot's helmet (body.Helm) every piece is hidden. Any other hat, or none: the hair
        // as styled. Writes only when that changes; a piece whose mesh was replaced since (a cut) keeps it.
        private void VisualHatHair(CapturedAnimation.Player rig,string hat){if(rig!=null&&visualBodies.TryGetValue(rig,out var body))VisualHatHair(body,hat);}
        private static void VisualHatHair(VisualBody body,string hat)
        {
            string worn=body!=null&&body.Helm?NativeRobotHelm.Hair:hat==NativeHatLooks.SheldonHead||NativeModelHatMeshes.PressesHair(hat)?hat:null;if(body?.Root==null||body.HatHair!=null&&worn==body.HatHairWorn)return;
            if(body.HatHair==null)
            {
                var hair=new List<SkinnedMeshRenderer>();var shape=new List<int>();var rest=new List<float>();var meshes=new List<Mesh>();
                foreach(var skin in body.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh=skin.sharedMesh;if(mesh==null)continue;int hood=-1;
                    for(int i=0;i<mesh.blendShapeCount;i++)if(mesh.GetBlendShapeName(i).EndsWith("Tag_Hood",StringComparison.Ordinal)){hood=i;break;}
                    bool piece=VisualHairPiece(skin);if(hood<0&&!piece)continue;
                    hair.Add(skin);shape.Add(hood);rest.Add(hood>=0?skin.GetBlendShapeWeight(hood):0);meshes.Add(piece?mesh:null);
                }
                body.HatHair=hair.ToArray();body.HatHairShape=shape.ToArray();body.HatHairRest=rest.ToArray();body.HatHairMesh=meshes.ToArray();
            }
            bool flat=worn==NativeHatLooks.SheldonHead,ready=true;
            for(int k=0;k<body.HatHair.Length;k++)
            {
                var skin=body.HatHair[k];if(skin==null)continue;var mesh=body.HatHairMesh[k];var now=skin.sharedMesh;
                if(mesh!=null&&now!=null&&(now==mesh||pressedHairOf.TryGetValue(now,out var of)&&of==mesh))
                {
                    var want=worn!=null&&!flat?VisualPressedHair(mesh,skin,worn):mesh;
                    if(want==null)ready=false;else if(want!=now)VisualHairMesh(skin,want);
                }
                if(body.HatHairShape[k]>=0)skin.SetBlendShapeWeight(body.HatHairShape[k],flat?100:body.HatHairRest[k]);
            }
            // (While a piece's press is still being made the hair stays as styled, and this looks again next frame.)
            if(ready)body.HatHairWorn=worn;
        }
        private static bool VisualHairPiece(SkinnedMeshRenderer skin)=>skin.name.StartsWith("HairFront.",StringComparison.Ordinal)||skin.name.StartsWith("HairBack.",StringComparison.Ordinal);
        // A piece's mesh swapped, its blend shapes' weights kept.
        private static void VisualHairMesh(SkinnedMeshRenderer skin,Mesh mesh)
        {
            int n=Math.Min(skin.sharedMesh.blendShapeCount,mesh.blendShapeCount);var w=new float[n];for(int i=0;i<n;i++)w[i]=skin.GetBlendShapeWeight(i);
            skin.sharedMesh=mesh;for(int i=0;i<n;i++)skin.SetBlendShapeWeight(i,w[i]);
        }
        // Hair piece `mesh` (worn by `skin`) as hat `hat` holds it, made once a piece and hat (from the first body seen wearing
        // them: every body's hair rests alike), off the main thread. Null while it is being made; the piece itself when the hat
        // leaves it as it is (or it cannot be read, or is not on the head).
        private static readonly Dictionary<(Mesh,string),Mesh> pressedHair=new Dictionary<(Mesh,string),Mesh>();
        private static readonly Dictionary<(Mesh,string),System.Threading.Tasks.Task<(Vector3[],BoneWeight[])>> pressingHair=new Dictionary<(Mesh,string),System.Threading.Tasks.Task<(Vector3[],BoneWeight[])>>();
        private static readonly Dictionary<Mesh,Mesh> pressedHairOf=new Dictionary<Mesh,Mesh>();
        // A vertex's weights `w` with share `hold` moved onto bone `held` (its four heaviest kept, summing to one).
        private static BoneWeight VisualHeldHair(BoneWeight w,int held,float hold)
        {
            var b=new int[5];var k=new float[5];int n=0;
            HeldAdd(b,k,ref n,held,hold);HeldAdd(b,k,ref n,w.boneIndex0,w.weight0*(1-hold));HeldAdd(b,k,ref n,w.boneIndex1,w.weight1*(1-hold));HeldAdd(b,k,ref n,w.boneIndex2,w.weight2*(1-hold));HeldAdd(b,k,ref n,w.boneIndex3,w.weight3*(1-hold));
            for(int i=1;i<n;i++)for(int j=i;j>0&&k[j]>k[j-1];j--){(k[j],k[j-1])=(k[j-1],k[j]);(b[j],b[j-1])=(b[j-1],b[j]);}
            n=Math.Min(n,4);float sum=0;for(int j=0;j<n;j++)sum+=k[j];if(sum<=0)return new BoneWeight{boneIndex0=held,weight0=1};
            var r=new BoneWeight{boneIndex0=b[0],weight0=k[0]/sum};if(n>1){r.boneIndex1=b[1];r.weight1=k[1]/sum;}if(n>2){r.boneIndex2=b[2];r.weight2=k[2]/sum;}if(n>3){r.boneIndex3=b[3];r.weight3=k[3]/sum;}return r;
        }
        private static void HeldAdd(int[] b,float[] k,ref int n,int bone,float weight){if(weight<=0)return;for(int j=0;j<n;j++)if(b[j]==bone){k[j]+=weight;return;}b[n]=bone;k[n]=weight;n++;}
        private static Mesh VisualPressedHair(Mesh mesh,SkinnedMeshRenderer skin,string hat)
        {
            var key=(mesh,hat);if(pressedHair.TryGetValue(key,out var made))return made;
            // Under the robot's helmet (NativeRobotHelm.Hair) the piece draws nothing: a copy of it (its vertices, weights and shapes)
            // without triangles.
            if(hat==NativeRobotHelm.Hair)
            {
                made=mesh;if(mesh.isReadable){made=UnityEngine.Object.Instantiate(mesh);made.name=mesh.name+" under the helmet";for(int i=0;i<made.subMeshCount;i++)made.SetTriangles(Array.Empty<int>(),i,false);made.bounds=mesh.bounds;pressedHairOf[made]=mesh;}
                pressedHair[key]=made;return made;
            }
            if(pressingHair.TryGetValue(key,out var task))
            {
                if(!task.IsCompleted)return null;pressingHair.Remove(key);made=mesh;
                if(task.Status==System.Threading.Tasks.TaskStatus.RanToCompletion&&task.Result.Item1!=null)
                {made=UnityEngine.Object.Instantiate(mesh);made.name=mesh.name+" under "+hat;made.vertices=task.Result.Item1;made.boneWeights=task.Result.Item2;made.bounds=mesh.bounds;pressedHairOf[made]=mesh;}
                pressedHair[key]=made;return made;
            }
            // Each vertex at rest on the head (through a bone that moves with it: the head, or the piece's own bone on it, which some
            // pieces hang off; at rest every bone of a piece places it alike, so its cloth's swing then does not count), pressed,
            // and then held by that bone as far as the hat holds it (HairPress.Hold).
            var press=NativeModelHatMeshes.HairPress.Of(hat);var bones=skin.bones;var bind=mesh.bindposes;Transform head=null;
            foreach(var b in bones)if(b!=null&&b.name=="head"){head=b;break;}
            for(var t=skin.rootBone;head==null&&t!=null;t=t.parent)if(t.name=="head")head=t;
            int held=Array.IndexOf(bones,head);
            if(held<0)for(int i=0;i<bones.Length;i++)if(bones[i]!=null&&bones[i].parent==head&&!bones[i].name.StartsWith("Hair_",StringComparison.Ordinal)){held=i;break;}
            if(press==null||head==null||held<0||bind.Length!=bones.Length||!mesh.isReadable){pressedHair[key]=mesh;return mesh;}
            var m=head.worldToLocalMatrix*bones[held].localToWorldMatrix*bind[held];
            var rest=new System.Numerics.Matrix4x4(m.m00,m.m10,m.m20,m.m30,m.m01,m.m11,m.m21,m.m31,m.m02,m.m12,m.m22,m.m32,m.m03,m.m13,m.m23,m.m33);
            if(!System.Numerics.Matrix4x4.Invert(rest,out var back)){pressedHair[key]=mesh;return mesh;}
            var v=mesh.vertices;var weights=mesh.boneWeights;var triangles=mesh.triangles;if(weights.Length!=v.Length){pressedHair[key]=mesh;return mesh;}
            pressingHair[key]=System.Threading.Tasks.Task.Run(()=>
            {
                var at=new System.Numerics.Vector3[v.Length];var pressed=new System.Numerics.Vector3[v.Length];var hold=new float[v.Length];
                for(int i=0;i<v.Length;i++)at[i]=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(v[i].x,v[i].y,v[i].z),rest);
                if(!press.PressPiece(at,triangles,pressed,hold))return (null,null);
                for(int i=0;i<v.Length;i++)
                {
                    if(pressed[i]==at[i])continue;var r=System.Numerics.Vector3.Transform(pressed[i],back);v[i]=new Vector3(r.X,r.Y,r.Z);weights[i]=VisualHeldHair(weights[i],held,hold[i]);
                }
                return (v,weights);
            });
            return null;
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
        // its weapon in hand where the entrance shows it (IntroWeapon). While the entrance builds the body in or glitches it
        // (`reveal`: the Sheldon Intro's drop-outs; owner 2026-10-28: "it uses voxel model in the "glitching""), the model
        // takes the reveal bone by bone in its shader (VisualRevealLook), as the voxel parts did. `armed`: the entrance shows a
        // weapon in hand (else the hands hold nothing: VisualArms.Unarmed).
        private void VisualIntroPose(CapturedAnimation.Player rig,Actor a,float time,CapturedRig.Pose pose,Func<int,float> reveal,bool armed=true)
        {
            if(Viewer||sourceModels==null||ClassicBodies||rig==null||a==null||pose==null||a.SourceSpec=="siren"||!VisualBodiesOn)return;
            var body=VisualBodyOf(rig,a.Id,VisualHooded(a));if(body==null)return;
            pose.World();var held=VisualBodyBind(body,rig,a,time,pose,false,false,armed?VisualArms.Live:VisualArms.Unarmed);
            var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
            body.Moved=true;
        }
        // A directed entrance's per-bone reveal on the model (NativeVisualBodyFx.cginc RevealClip): each game bone of `rig` as its
        // world segment (Core NativeBodyReveal.Segment) with its share of `reveal`; written while some bone is under whole, and
        // cleared once after.
        private static readonly int RevealOnId=Shader.PropertyToID("_RevealOn"),RevealAId=Shader.PropertyToID("_RevealA"),RevealBId=Shader.PropertyToID("_RevealB");
        private readonly Vector4[] revealA=new Vector4[NativeBodyReveal.Segments],revealB=new Vector4[NativeBodyReveal.Segments];
        private readonly System.Numerics.Vector3[] revealAt=new System.Numerics.Vector3[CapturedRig.Bones.Length];
        private void VisualRevealLook(VisualBody body,CapturedAnimation.Player rig,Func<int,float> reveal)
        {
            bool on=false;
            if(reveal!=null&&rig.Bones.Length>=CapturedRig.Bones.Length)
            {
                for(int b=0;b<revealAt.Length;b++){var p=rig.Bones[b].position;revealAt[b]=new System.Numerics.Vector3(p.x,p.y,p.z);}
                var up=rig.Bones[CapturedRig.Head].up;var headUp=new System.Numerics.Vector3(up.x,up.y,up.z);
                for(int b=1;b<=NativeBodyReveal.Segments;b++)
                {
                    float share=Mathf.Clamp01(reveal(b));if(share<.999f)on=true;
                    NativeBodyReveal.Segment(b,revealAt,headUp,out var from,out var to);
                    revealA[b-1]=new Vector4(from.X,from.Y,from.Z,share);revealB[b-1]=new Vector4(to.X,to.Y,to.Z,0);
                }
            }
            if(!on&&!body.RevealOn)return;
            foreach(var r in body.All)
            {
                if(r==null)continue;r.GetPropertyBlock(body.Block);body.Block.SetFloat(RevealOnId,on?1:0);
                if(on){body.Block.SetVectorArray(RevealAId,revealA);body.Block.SetVectorArray(RevealBId,revealB);}
                r.SetPropertyBlock(body.Block);
            }
            body.RevealOn=on;
        }
        // The draft's pilot on its visual body (owner 2026-10-24: "Model everywhere"): the drafting seat as a stand-in actor (its
        // outfit, accent, no weapon in hand: the hands pick the chips), bound to the stage's pose, with its hat on the model's
        // head; shown this frame (the draft returns before the arena does). False: the voxel body (a Siren, Classic, no bake).
        private readonly Actor draftActor=new Actor();
        // Whether the drafting seat draws on its visual body (VisualDraftPose's own test; the stage frames the model's head).
        private bool VisualDraftModel(NativeSeat seat)=>!Viewer&&sourceModels!=null&&!ClassicBodies&&seat!=null&&seat.spec!="siren"&&VisualBodiesOn;
        private bool VisualDraftPose(CapturedAnimation.Player rig,NativeSeat seat,CapturedRig.Pose pose,float time,Camera camera,float support=0)
        {
            draftBody=null;if(rig==null||pose==null||!VisualDraftModel(seat))return false;
            var a=draftActor;a.Id=seat.id;a.SourceSpec=seat.spec;a.AccentColor=seat.accentColor;a.BodyColor=seat.bodyColor;a.Hat=seat.hat;a.HatColor=seat.hatColor;a.WeaponAway=true;a.Grounded=true;
            var body=VisualBodyOf(rig,seat.id,VisualHooded(a));if(body==null)return false;draftBody=body;
            // The drafting pose stands lower than the arena's: the body's hips go onto the pose's pelvis, and the rig's hands (on
            // the body's wrists after the bind) with them.
            body.Root.transform.localPosition=new Vector3(0,NativeVisualRig.Floor,0);
            pose.World();var held=VisualBodyBind(body,rig,a,time,pose,false,false,VisualArms.Reach,support);
            int hips=CapturedRig.Pelvis<body.Binding.Rig.Of.Length?body.Binding.Rig.Of[CapturedRig.Pelvis]:-1;
            if(hips>=0&&body.Binding.Bones[hips]!=null){var shift=rig.Bones[CapturedRig.Pelvis].position-body.Binding.Bones[hips].position;body.Root.transform.position+=shift;for(int side=0;side<2;side++){var hand=rig.Bones[side==0?CapturedRig.LHand:CapturedRig.RHand];hand.position+=shift;}}
            var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
            body.Moved=true;body.Seen=true;
            VisualHatHair(body,seat.hat);
            if(!string.IsNullOrEmpty(seat.hat)&&seat.hat!="none"&&seat.hat!=NativeSheldonHead.Hat)
            {var hb=VisualHatBone(rig,seat.hat);if(!AuditorHalo(seat.hat,hb(CapturedRig.Head),time,1,false,0,null,camera,seat.hatColor))sourceModels.Queue(VisualHatModel(seat.hat,seat.hatColor),rig.Root.transform.localToWorldMatrix,hb,body:HatBody(seat.hat,seat.hatColor));}
            // The Sheldon Head: the hood's void and face (the body wears the hood; without them the opening showed the model's face).
            else SheldonHoodStill(body,seat.hat,seat.hatColor,rig.Root.transform.localToWorldMatrix,VisualHatBone(rig,seat.hat),time,seat.id,HaloReduced,0,null);
            if(!body.Hood)VisualFaceGear(VisualHeadOf(body),seat.id,seat.bodyColor,seat.accentColor,0,null,null,null,0);
            VisualSuit(body.Root,seat.id,seat.bodyColor,seat.accentColor,0,null,null,null,0);
            return true;
        }
        // The draft's visual body this frame (VisualDraftPose; null when the pilot draws voxel): its wrist device takes the
        // drafted chip (NativeDraftPlant.Device).
        private VisualBody draftBody;
        // The bay's chip seat on the draft body (world) and the bay's outward normal (the back of the forearm); false without the
        // device.
        private bool VisualDraftBay(out Vector3 at,out Vector3 outward)
        {
            at=outward=default;var body=draftBody;if(body?.GearDevice==null)return false;var s=NativePilotGear.Seat(Mathf.Clamp(body.GearRadius,.02f,.08f));
            at=body.GearDevice.TransformPoint(new Vector3(-s.X,s.Y,s.Z));outward=body.GearDevice.TransformDirection(Vector3.right).normalized;return true;
        }
        // The device on the draft body through the pick, as VisualGear draws it in the arena but on the plant's clock: the lid
        // opens as the receiving arm rises and shuts on the return, the bay glows ready in the accent, shows the chip from the
        // carry's end and pulses in its colour, the lens keeps the colour after; the band's strips flare with the pulse.
        private void VisualDraftDevice(NativeSeat seat,float age,float time,Color chip,float flash)
        {
            var body=draftBody;if(body?.GearDevice==null||!sourceModels.PilotGear(body.GearRadius>.01f?body.GearRadius:.04f))return;
            gearBone??=GearBoneOf;var accent=AccentTint(seat.accentColor);
            var dev=NativePilotGear.Device(age>=0?NativeDraftPlant.Raise(age):0,age,age>=NativeDraftPlant.CarryEnd?age-NativeDraftPlant.CarryEnd:float.NaN,time);
            var m=body.GearDevice.localToWorldMatrix;float r=Mathf.Round(Mathf.Clamp(body.GearRadius,.02f,.08f)*1000)/1000,strips=dev.Strips*(1+2*flash);
            gearMatrix=m;sourceModels.Queue(sourceModels.GearDeviceFor(body.GearRadius),m,gearBone,null,0,null,Color.white,accent,1,null,null,new Vector4(strips,strips,strips,1),null,0);
            if(dev.Lid>.02f){var bay=(dev.BayChip?chip:body.AccentLinear)*dev.Bay;sourceModels.Queue(sourceModels.GearBayFor(body.GearRadius),m,gearBone,null,0,null,Color.white,accent,1,null,null,new Vector4(bay.r,bay.g,bay.b,1),null,0);}
            var h=NativePilotGear.Hinge(r);var lens=(dev.LensChip?chip:body.AccentLinear)*dev.Lens;
            gearMatrix=m*Matrix4x4.TRS(new Vector3(-h.X,h.Y,h.Z),Quaternion.AngleAxis(dev.Lid*NativePilotGear.LidOpenDegrees,Vector3.forward),Vector3.one);
            sourceModels.Queue(NativeSourceModels.GearLid,gearMatrix,gearBone,null,0,null,Color.white,accent,1,null,null,new Vector4(lens.r,lens.g,lens.b,1),null,0);
            if(dev.Chip){var s=NativePilotGear.Seat(r);var c=chip*1.6f;gearMatrix=m*Matrix4x4.Translate(new Vector3(-s.X,s.Y,s.Z));sourceModels.Queue(NativeSourceModels.GearChip,gearMatrix,gearBone,null,0,null,Color.white,accent,1,null,null,new Vector4(c.r,c.g,c.b,1),null,0);}
        }
        private VisualBody CreateVisualBody(CapturedAnimation.Player rig,int id,bool hood=false,bool simulate=true)
        {
            var root=VisualCorpseInstance();root.transform.SetParent(rig.Root.transform,false);root.SetActive(true);root.name="Visual body "+id;
            // Its origin is the pilot's floor point: the rig root stands .865 m over the floor (NativeVisualRig.Floor).
            root.transform.localPosition=new Vector3(0,NativeVisualRig.Floor,0);root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            var skeleton=root.transform.Find("VisualSkeleton");
            if(skeleton==null){Destroy(root);visualBodyState=-1;Debug.LogWarning("Visual body: "+VisualBodyResource+" has no VisualSkeleton; pilots draw voxel.");return null;}
            // Dressed in the pilot's outfit (only the worn pieces' renderers from here on), its skeleton set by its shoes' lift
            // before the binding reads the rest pose.
            var look=VisualLookOf(id);var sheldon=new List<Renderer>();VisualDress(root,look,sheldon);float lift=VisualLiftOf(look);if(lift!=0)skeleton.localPosition+=new Vector3(0,lift,0);
            // The Sheldon hood, when its pilot wears the Sheldon Head (its piping in the Sheldon glow with the Sheldon pieces').
            var hoodRenderer=hood?VisualHoodMake(root,skeleton):null;if(hoodRenderer!=null)sheldon.Add(hoodRenderer);
            // Under it the hair, the head and the face are hidden (VisualUnderHood: the void covers the opening).
            if(hoodRenderer!=null)VisualUnderHood(root);
            var body=new VisualBody{Root=root,Binding=new NativeVisualRigBinding(root.transform,skeleton),Block=new MaterialPropertyBlock(),Look=look,Sheldon=sheldon.ToArray(),Lift=lift,Hood=hood,HoodRenderer=hoodRenderer};
            body.All=VisualWorn(root);body.Plain=new Material[body.All.Length][];VisualCutWarm(body.All);
            // Its skeleton, off until a wound opens into it (an arena pilot's body; not the view body, busts, ghosts or portraits).
            if(simulate&&id!=ViewBodyId){body.Skeleton=VisualSkeletonMake(root,skeleton);if(body.Skeleton!=null)body.Skeleton.enabled=false;}
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
                r.GetPropertyBlock(body.Block);var tex=m.GetTexture(TextureId);if(tex!=null&&!body.Block.HasTexture(TextureId))body.Block.SetTexture(TextureId,tex);
                body.Block.SetFloat(CutoffId,m.HasProperty(CutoffId)?m.GetFloat(CutoffId):.5f);r.SetPropertyBlock(body.Block);
            }
            var cloth=new List<Renderer>();
            foreach(var r in body.All)foreach(var slot in ClothSlots)if(r.name.StartsWith(slot,StringComparison.Ordinal)){cloth.Add(r);break;}
            body.Cloth=cloth.ToArray();
            if(id!=ViewBodyId)body.Stride=VisualStrideOf(root.transform,skeleton);
            // Its hair and cloth, when the local install simulates them: the live and draft pilots and whole corpses, never the
            // first-person body, the broadcast's bust, the ghosts or the menu portraits (`simulate` false: nothing holds them still).
            if(simulate&&id!=ViewBodyId&&NativeVisualCloth.Attach!=null)
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
                if(r==null||Array.IndexOf(body.Sheldon,r)>=0)continue;
                r.GetPropertyBlock(body.Block);foreach(int id in AccentChannels)body.Block.SetColor(id,color);r.SetPropertyBlock(body.Block);
            }
        }
        // The Sheldon pieces' piping in the Sheldon glow (NativeBodySkins.SheldonGlow: a picked hat colour, cyan under the hood,
        // else the accent), written only when it changes.
        private static void VisualSheldonGlow(VisualBody body,string glow)
        {
            if(string.Equals(body.SheldonGlow,glow,StringComparison.Ordinal))return;
            body.SheldonGlow=glow;if(!ColorUtility.TryParseHtmlString(glow??NativeBodySkins.SheldonCyan,out var color))return;
            foreach(var r in body.Sheldon){if(r==null)continue;r.GetPropertyBlock(body.Block);foreach(int id in AccentChannels)body.Block.SetColor(id,color);r.SetPropertyBlock(body.Block);}
        }

        // While drawing it (SourceCharacter): the pilot's hit flash code and match-start spawn-in on the visual body's shader,
        // per renderer, and the hologram's slots while the spawn-in shows one (none on the low graphics tier). Writes only
        // while a spawn-in runs or the flash changes; allocates only the first time a body shows the hologram.
        private void VisualBodyLook(CapturedAnimation.Player rig,Actor a,float time,SpawnView spawn,float flash,Func<int,float> reveal,Func<int,Color> tint)
        {
            bool first=rig==viewRig&&viewBody!=null;VisualBody body;
            if(first)body=viewBody;else if(!visualBodies.TryGetValue(rig,out body))return;
            if(body.Root==null)return;
            if(!first)VisualWounds(body,rig,a);
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
            if(rig==introRig)VisualRevealLook(body,rig,reveal);
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
            if(!first&&!body.Hood)VisualFaceGear(VisualHeadOf(body),a.Id,a.BodyColor,a.AccentColor,layer,camera,reveal,tint,flash);
            if(!first)VisualSuit(body.Root,a.Id,a.BodyColor,a.AccentColor,layer,camera,reveal,tint,flash);
            // The back emblem on the chest's model bone, glowing as the core does (under the robot armour's back plate: none).
            if(!first&&!WearsArmour(a.Id))
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
        [Serializable] public sealed class LookPiece{public string slot,id;public string[] renderers,covers,tags;public float lift;}
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
        // the worn pieces cover off (the rest on), the tag shapes full where a worn piece sets their tag, and the Sheldon pieces in
        // their palette (VisualSheldonPalette; their renderers added to `sheldon`), and the skin parts in the pilot's tone
        // (VisualSkinToneOne). Runs when a body is made (once per outfit), never per frame.
        private static void VisualDress(GameObject root,string look,List<Renderer> sheldon=null)
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
            foreach(var p in d.pieces)if(p?.renderers!=null&&NativeSheldonWear.IsSheldon(p.id)&&NativeLook.Of(look,p.slot)==p.id)foreach(var n in p.renderers)if(lookNamed.TryGetValue(n,out var r)){VisualSheldonPalette(r);sheldon?.Add(r);}
            // The skin tone (none: the parts are left as they are). Every palette's base is read before a textured part takes the body's.
            int tone=NativeLook.SkinOf(look);
            if(tone>=0){foreach(var r in lookNamed.Values)if(VisualSkinPart(r))VisualBaseSkin(r.sharedMaterial);foreach(var r in lookNamed.Values)VisualSkinToneOne(r,tone);}
            lookNamed.Clear();
        }
        // The pilot's skin tone (Core NativeLook's skin entry; owner 2026-10-28: "Give skin color customization"): written on the
        // skin parts when a body is dressed, and again in place when only the tone changes (VisualRetone), never per frame. A
        // skin part is a body part of the bake's look data or the head or body mesh; the model's shader scales its skin colours
        // by the tone over the part's base skin (Resources/NativeVisualBodyFx.cginc SkinToned). The base is the brightest skin
        // colour of the part's palette (Core NativeColorWheel.BaseSkin), or, for a part drawn from its texture, the body's: the
        // brightest base of the palettes seen (the model's own skin, NativeColorWheel.ModelSkin, when none has one).
        private static readonly int SkinToneId=Shader.PropertyToID("_SkinTone"),SkinRefId=Shader.PropertyToID("_SkinRef"),UseCustomTextureId=Shader.PropertyToID("_UseCustomTexture");
        private static readonly HashSet<string> skinParts=new HashSet<string>(StringComparer.Ordinal);private static LookData skinPartsOf;
        private static readonly Dictionary<Material,int> skinBases=new Dictionary<Material,int>();private static int skinBase=-1;
        private static readonly int[] skinPalette=new int[9];private static MaterialPropertyBlock toneBlock;
        private static bool VisualSkinPart(Renderer r)
        {
            var d=VisualLookData();
            if(!ReferenceEquals(d,skinPartsOf)){skinPartsOf=d;skinParts.Clear();if(d?.body!=null)foreach(var b in d.body)if(!string.IsNullOrEmpty(b?.renderer))skinParts.Add(b.renderer);}
            string n=r.name;return skinParts.Contains(n)||n.StartsWith("Head.",StringComparison.Ordinal)||n.StartsWith("Body.",StringComparison.Ordinal);
        }
        // A part's base skin (0xRRGGBB, -1 when its palette has none or it draws its texture as it is), once per material.
        private static int VisualBaseSkin(Material m)
        {
            if(m==null)return -1;if(skinBases.TryGetValue(m,out int known))return known;int found=-1;
            if(!m.HasProperty(UseCustomTextureId)||m.GetFloat(UseCustomTextureId)>.5f)
            {
                for(int i=0;i<BodyColours.Length;i++){if(!m.HasProperty(BodyColours[i])){skinPalette[i]=-1;continue;}var c=m.GetColor(BodyColours[i]);skinPalette[i]=Mathf.RoundToInt(Mathf.Clamp01(c.r)*255)<<16|Mathf.RoundToInt(Mathf.Clamp01(c.g)*255)<<8|Mathf.RoundToInt(Mathf.Clamp01(c.b)*255);}
                found=NativeColorWheel.BaseSkin(skinPalette);
            }
            skinBases[m]=found;if(found>=0&&(skinBase<0||NativeColorWheel.Luma(found)>NativeColorWheel.Luma(skinBase)))skinBase=found;return found;
        }
        private static void VisualSkinToneOne(Renderer r,int tone)
        {
            if(r==null||!VisualSkinPart(r))return;
            toneBlock??=new MaterialPropertyBlock();r.GetPropertyBlock(toneBlock);
            int base0=tone<0?-1:VisualBaseSkin(r.sharedMaterial);if(base0<0)base0=skinBase>=0?skinBase:NativeColorWheel.ModelSkin;
            if(tone<0){if(toneBlock.GetVector(SkinToneId).w<.5f)return;toneBlock.SetVector(SkinToneId,Vector4.zero);} // only a toned part is cleared
            else
            {
                bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;Color t=WearColor(tone),b=WearColor(base0);if(linear){t=t.linear;b=b.linear;}
                toneBlock.SetVector(SkinToneId,new Vector4(t.r,t.g,t.b,1));toneBlock.SetVector(SkinRefId,new Vector4(b.r,b.g,b.b,1));
            }
            r.SetPropertyBlock(toneBlock);
        }
        /// <summary>Whether a body made in its look can show `look` as it is: the same look, or the same outfit in another skin
        /// tone, which it then takes in place (no new instance while a colour is dragged on the wheel).</summary>
        private static bool VisualRetone(VisualBody body,string look)
        {
            if(string.Equals(body.Look,look,StringComparison.Ordinal))return true;
            if(!NativeLook.SameOutfit(body.Look,look))return false;
            int tone=NativeLook.SkinOf(look);if(body.All!=null)foreach(var r in body.All)VisualSkinToneOne(r,tone);body.Look=look;return true;
        }
        // Sheldon's clothes (Core NativeSheldonWear): a piece's renderer in the Sheldon palette (the cloth and trim colours of each
        // layer, the glow cyan until VisualSheldonGlow sets the pilot's) on the bake's piped colour map for it, when there is one.
        private static MaterialPropertyBlock wearBlock;
        private static readonly Dictionary<string,Texture2D> wearMaps=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
        private static readonly Color WearCloth=WearColor(NativeSheldonWear.Cloth),WearTrim=WearColor(NativeSheldonWear.Trim),WearGlow=WearColor(NativeBodySkins.CyberCyan);
        private static Color WearColor(int rgb)=>new Color((rgb>>16&255)/255f,(rgb>>8&255)/255f,(rgb&255)/255f);
        private static void VisualSheldonPalette(Renderer r)
        {
            wearBlock??=new MaterialPropertyBlock();r.GetPropertyBlock(wearBlock);
            for(int i=0;i<BodyColours.Length;i++)wearBlock.SetColor(BodyColours[i],i%3==0?WearCloth:i%3==1?WearTrim:WearGlow);
            if(!wearMaps.TryGetValue(r.name,out var map)){map=Resources.Load<Texture2D>(NativeSheldonWear.MapFolder+r.name);wearMaps[r.name]=map;}
            if(map!=null)wearBlock.SetTexture(TextureId,map);
            r.SetPropertyBlock(wearBlock);
        }
        // The lift a look's feet piece sets the body's skeleton by (the bake's lift for it against its rest shoes', the concept's:
        // NativeLook.Concept): bare feet set down onto the floor.
        private static float VisualLiftOf(string look)
        {
            var d=VisualLookData();if(d==null)return 0;string feet=NativeLook.Of(look,NativeLook.Feet),first=NativeLook.Of(NativeLook.Concept,NativeLook.Feet);float worn=0,rest=0;
            foreach(var p in d.pieces){if(p==null||p.slot!=NativeLook.Feet)continue;if(p.id==feet)worn=p.lift;if(p.id==first)rest=p.lift;}
            return Mathf.Clamp(worn-rest,-.05f,.05f);
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
        // The face gear on a model's head bone, as the pilot's "face" pick has it (Core NativeWeaponSkins; FaceGearOf): the robot's
        // helmet (the default) or half mask (Core NativeRobotHelm) in its body colour, the concept's mask and visor (Core
        // NativePilotGear) both or either alone, or none (the model's own face): the live body (VisualGear), the draft's pilot, a
        // whole corpse, a death scene; the helmet also on the half of a split corpse that holds the head.
        private void VisualFaceGear(Transform head,int id,string bodyColor,string accentColor,int layer,Camera camera,Func<int,float> reveal,Func<int,Color> tint,float flash)
        {
            int gear=FaceGearOf(id);if(head==null||gear==NativeWeaponSkins.FaceNone||!sourceModels.PilotFace())return;
            gearBone??=GearBoneOf;gearMatrix=head.localToWorldMatrix;var accent=AccentTint(accentColor);var lit=new Vector4(1,1,1,1);
            bool helm=gear==NativeWeaponSkins.FaceHelm;
            if(helm||gear==NativeWeaponSkins.FaceHalf)
            {
                string model=helm?NativeSourceModels.Helm:NativeSourceModels.HalfMask;
                if(sourceModels.Has(model))sourceModels.Queue(model,gearMatrix,gearBone,null,layer,camera,BodyTint(bodyColor),accent,1,reveal,tint,lit,null,flash);
                return;
            }
            if(gear==NativeWeaponSkins.FaceBoth||gear==NativeWeaponSkins.FaceMask)sourceModels.Queue(NativeSourceModels.GearMask,gearMatrix,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,lit,null,flash);
            if(gear==NativeWeaponSkins.FaceBoth||gear==NativeWeaponSkins.FaceVisor)sourceModels.Queue(NativeSourceModels.GearVisor,gearMatrix,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,lit,null,flash);
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
        // view rig and placed so its right hand's weapon frame is where the view rig holds the weapon (the aim stays the game's);
        // the left hand's item rides the visual left hand, as in third person. It holds the visual body's own weapon models
        // (NativeVisualWeaponPresentation.cs, its "First person"): the right hand on the fitted frame, the sights the model's.
        private VisualBody viewBody;private static readonly Dictionary<Mesh,Mesh> armMeshes=new Dictionary<Mesh,Mesh>();
        private bool VisualViewDrawn(CapturedAnimation.Player rig)=>rig==viewRig&&viewBody!=null&&viewBody.Root!=null&&viewBody.PosedFrame==Time.frameCount;
        // RenderFirstPerson, before the view rig poses: its hands go back where its own pose put them (the right off the fit).
        private void VisualViewRestore()
        {
            if(viewBody==null||!viewBody.Moved||viewRig==null)return;
            var bone=viewRig.Bones[CapturedRig.LHand];bone.localPosition=viewBody.HandPosition[0];bone.localRotation=viewBody.HandRotation[0];bone.localScale=viewBody.LeftScale;
            var right=viewRig.Bones[CapturedRig.RHand];right.localPosition=viewBody.HandPosition[1];right.localRotation=viewBody.HandRotation[1];right.localScale=viewBody.RightScale;viewBody.Moved=false;
        }
        // RenderFirstPerson, once the view rig is placed (aimed, swayed, chip-select layered): pose and place the view body; false
        // (and hidden) when the voxel arms draw instead (Classic, no bake, the Siren, a scope up).
        private bool VisualViewPose(CapturedAnimation.Player view,Actor a,float time,bool scoped)
        {
            bool on=!Viewer&&sourceModels!=null&&!ClassicBodies&&!scoped&&view!=null&&a.SourceSpec!="siren"&&VisualBodiesOn;
            if(!on){if(viewBody?.Root!=null&&viewBody.Root.activeSelf)viewBody.Root.SetActive(false);return false;}
            if(viewBody?.Root!=null&&!VisualRetone(viewBody,LocalLook??"")){viewBody.Root.SetActive(false);Destroy(viewBody.Root);viewBody=null;}
            if(viewBody==null||viewBody.Root==null){viewBody=CreateViewBody(view);if(viewBody==null)return false;}
            var pose=view.Pose;pose.World();
            if(viewBody.PosedFrame<Time.frameCount-1||viewBody.Spec!=a.SourceSpec)viewBody.Binding.State.Forget();
            viewBody.PosedFrame=Time.frameCount;viewBody.Spec=a.SourceSpec;
            // Its own model of the weapon in hand, as the arena body's: the grasp on its handguard and stock, a staff's shaft.
            var held=viewBody.Held=VisualWeaponFor(viewBody,a);if(held!=null&&!held.ViewReady)VisualViewWeaponReady(held);
            var grasp=VisualGrasp(a,pose,time,ChipLookOf(a.Id));if(held!=null)VisualWeaponGrasp(ref grasp,held);
            viewBody.Binding.Apply(pose,grasp,false,false);
            if(viewBody.Accent!=a.AccentColor)VisualAccent(viewBody,a.AccentColor);
            if(!viewBody.Root.activeSelf)viewBody.Root.SetActive(true);
            // The body moves (rigidly) so its right weapon frame lands on the view rig's right hand, where the weapon draws; with a
            // life-size model in hand, the body, both hands and what draws on them are larger about that frame (ModelView).
            float enlarge=held!=null?NativeFirstPersonPose.ModelScale(VisualViewGun(a)):1;
            var root=viewBody.Root.transform;root.localScale=Vector3.one*enlarge;var hand=view.Bones[CapturedRig.RHand];float s=root.lossyScale.x;
            viewBody.Binding.WeaponFrame(pose,out var rp,out var rq);var turn=hand.rotation*Quaternion.Inverse(rq);
            root.SetPositionAndRotation(hand.position-turn*(rp*s),turn);
            viewBody.Binding.LeftWeaponFrame(pose,out var lp,out var lq);var left=view.Bones[CapturedRig.LHand];
            viewBody.HandPosition[0]=left.localPosition;viewBody.HandRotation[0]=left.localRotation;viewBody.LeftScale=left.localScale;
            left.SetPositionAndRotation(root.TransformPoint(lp),root.rotation*lq);left.localScale=viewBody.LeftScale*enlarge;
            // The right hand on the model's fitted frame (VisualWeaponFit), for what draws there.
            viewBody.HandPosition[1]=hand.localPosition;viewBody.HandRotation[1]=hand.localRotation;viewBody.RightScale=hand.localScale;hand.localScale=viewBody.RightScale*enlarge;VisualWeaponFit(hand,held);
            viewBody.Moved=true;
            return true;
        }
        // A gun's view model (not the bow): ModelView, its sights and its lift (NativeFirstPersonPose).
        private static bool VisualViewGun(Actor a)=>GunRules.Ranged(a.Weapon)&&!NativeBowWire.Is(a.SourceSpec);
        // RenderFirstPerson, before the view rig is placed: the hold's rise (camera frame) while the view body holds a model (as
        // it did last frame; none before it is made, and none for the voxel arms or a scope).
        private Vector3 VisualViewLift(Actor a,bool magic,bool fighter)
        {
            if(viewBody?.Root==null||viewBody.PosedFrame<Time.frameCount-1||ScopeActive||VisualWeaponFor(viewBody,a)==null)return Vector3.zero;
            bool gun=VisualViewGun(a);
            return CapturedAnimation.Vec(NativeFirstPersonPose.ModelLift(gun,a.Weapon==WeaponId.Pistol,!gun&&!magic&&!fighter&&!NativeBowWire.Is(a.SourceSpec)));
        }
        private VisualBody CreateViewBody(CapturedAnimation.Player view)
        {
            var body=CreateVisualBody(view,ViewBodyId);if(body==null)return null;
            // The local pilot's own body in first person: it holds the pack bank's weapons too (VisualWeaponFor).
            body.Root.name="Visual view body";body.Arena=true;
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
        // wears it severed at the joints (VisualCorpseSever); each half of a split one (a blade's killing blow, a saw) the whole
        // model cut along the blade (NativeVisualCutPresentation.cs); a smashed one severed too (its crushed parts dropped, a
        // pinned one flattened by the smash's overlay). A swarm's collapse shrinks it; the soul and frost looks are its own
        // (VisualCorpseLook). It wears its hat (VisualCorpseHat). It hides while the voxel body would be under half opacity and
        // goes with its corpse.
        private sealed class VisualCorpse{public GameObject Root;public Transform[] Bones;public int[] Part;public Matrix4x4[] Bind;public int Frame=-1;public Renderer[] All;public float Dissolve=-1;public Color Edge;public List<Mesh> Owned;public int Look;public int Head=-2;
            // A split body's cut (both halves share it; the first half owns its texture), whether it wears the Sheldon hood, and its
            // skeleton (NativeVisualSkeletonPresentation.cs).
            public CutJob Cut;public Texture2D CutTexture;public bool Hood;public Renderer Skeleton;
            // Its pilot's open wounds' holes (NativeVisualWoundPresentation.cs).
            public List<VisualHole> Holes;
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
            bones=null;joint=null;var root=VisualCorpseInstance();root.name="Visual corpse "+death.id;var look=VisualLookOf(death.id);var sheldon=new List<Renderer>();VisualDress(root,look,sheldon);var skeleton=root.transform.Find("VisualSkeleton");
            if(skeleton==null){Destroy(root);return null;}
            float lift=VisualLiftOf(look);if(lift!=0)skeleton.localPosition+=new Vector3(0,lift,0);
            // The Sheldon hood the live body wore (its hair, head and face hidden under it, as there; VisualCorpseHat and the death
            // scenes draw its face).
            var hood=VisualHooded(death.actor)?VisualHoodMake(root,skeleton):null;
            if(hood!=null){sheldon.Add(hood);VisualUnderHood(root);}
            // The skeleton inside (a cut or wound shows it).
            VisualSkeletonMake(root,skeleton);
            var at=rig.Root.transform;root.transform.SetPositionAndRotation(at.TransformPoint(new Vector3(0,NativeVisualRig.Floor,0))+offset,at.rotation);
            var binding=new NativeVisualRigBinding(root.transform,skeleton);var pose=rig.Pose;pose.World();
            binding.Apply(pose,VisualGrasp(death.actor??new Actor(),pose,death.at,0),false,false);
            bones=binding.Bones;joint=VisualJoints(binding);
            // Its hair under its hat as the live body wore it (VisualHatHair), hidden under the robot's helmet.
            string under=hood==null&&death.actor!=null&&WearsHelm(death.id)?NativeRobotHelm.Hair:NativeModelHatMeshes.PressesHair(death.actor?.Hat)?death.actor.Hat:null;
            if(under!=null)
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if(skin.sharedMesh!=null&&VisualHairPiece(skin)){var pressed=VisualPressedHair(skin.sharedMesh,skin,under);if(pressed!=null&&pressed!=skin.sharedMesh)VisualHairMesh(skin,pressed);}
            var block=new MaterialPropertyBlock();ColorUtility.TryParseHtmlString(death.actor?.AccentColor??"#70e1d0",out var accent);
            ColorUtility.TryParseHtmlString(NativeBodySkins.SheldonGlow(death.actor?.Hat,death.actor?.HatColor,death.actor?.AccentColor??"#70e1d0"),out var glow);
            foreach(var r in VisualWorn(root))
            {
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;if(r is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
                bool cloth=false;foreach(var slot in ClothSlots)if(r.name.StartsWith(slot,StringComparison.Ordinal))cloth=true;
                if(!cloth)continue;r.GetPropertyBlock(block);foreach(int id in AccentChannels)block.SetColor(id,sheldon.Contains(r)?glow:accent);r.SetPropertyBlock(block);
            }
            root.SetActive(false);return root;
        }
        private bool VisualDeathFor(NativeDeath death)=>!ClassicBodies&&VisualBodiesOn&&(death.model??"pilot")=="pilot"&&death.actor?.SourceSpec!="siren";
        // Model bodies made ahead (owner 2026-10-28: "prevent fps drops during enemy kill"): a kill (its corpse, two for a split
        // body; the death broadcast's bust) and a new pilot's body take a ready instance of the bake (made off the main thread,
        // InstantiateAsync, under an inactive holder) instead of making one then (about 8 ms each); the next is started once one
        // is taken. A body that goes is taken apart a little each frame (VisualBodyGone), not destroyed at once (about 10 ms).
        private const int CorpsePoolSize=4;
        private static readonly Stack<GameObject> corpsePool=new Stack<GameObject>();private static AsyncInstantiateOperation<GameObject> corpseFill;private static GameObject corpseHolder;
        private static GameObject VisualCorpseInstance()
        {
            while(corpsePool.Count>0){var ready=corpsePool.Pop();if(ready==null)continue;ready.SetActive(false);ready.transform.SetParent(null,false);return ready;}
            return Instantiate(visualBodyPrefab);
        }
        private static readonly Queue<GameObject> visualGone=new Queue<GameObject>();
        private const int VisualGonePerFrame=12;
        private static void VisualBodyGone(GameObject root){if(root==null)return;root.SetActive(false);root.transform.SetParent(null,false);root.name="Visual body (gone)";visualGone.Enqueue(root);}
        // Once a frame (VisualCorpsesShown): a few of the gone bodies' pieces destroyed, the finished instance into the pool, the
        // next one started.
        private void VisualCorpsePoolStep()
        {
            for(int left=VisualGonePerFrame;left>0&&visualGone.Count>0;)
            {
                var root=visualGone.Peek();if(root==null){visualGone.Dequeue();continue;}
                var t=root.transform;if(t.childCount==0){Destroy(root);visualGone.Dequeue();left--;continue;}
                // Its last piece (a worn or spare part, the skeleton), out of the body so the next one is reached this frame.
                var piece=t.GetChild(t.childCount-1).gameObject;piece.SetActive(false);piece.transform.SetParent(null,false);Destroy(piece);left--;
            }
            if(visualBodyPrefab==null||ClassicBodies||!VisualBodiesOn)return;
            VisualCutWarmStep();
            if(corpseFill!=null){if(!corpseFill.isDone)return;var ready=corpseFill.Result;if(ready!=null)foreach(var made in ready)if(made!=null){made.SetActive(false);made.name="Visual corpse (ready)";corpsePool.Push(made);}corpseFill=null;}
            if(corpsePool.Count>=CorpsePoolSize)return;
            if(corpseHolder==null){corpseHolder=new GameObject("Visual corpse pool");corpseHolder.SetActive(false);DontDestroyOnLoad(corpseHolder);}
            corpseFill=InstantiateAsync(visualBodyPrefab,corpseHolder.transform);
        }
        // PresentDeath, as a pilot's corpse is made from its rig (still in its last pose).
        private void VisualCorpseMake(NativeCorpse corpse,NativeDeath death,CapturedAnimation.Player rig)
        {
            if(rig==null||!VisualDeathFor(death))return;
            // A split body: both halves wear the whole model, cut along the blade (VisualCorpseCut, owner 2026-10-28: "slice right on
            // the trail of a hit"). A body the cut cannot read wears the model's parts on each side of the cut instead (whole parts:
            // a part the cut crosses goes to the side its joint is on; owner 2026-10-24: "Sever at joints"), each loose piece of a
            // half (a part whose parent went to the other half) its own mesh, as below.
            if(corpse.Bisect is BisectFx fx&&corpse.Twin!=null&&fx.Request!=null)
            {
                var halves=new VisualCorpse[2];int[] joint=null;
                for(int h=0;h<2;h++){halves[h]=VisualCorpseFor(h==0?corpse:corpse.Twin,death,rig,out var j);joint??=j;}
                if(halves[0]==null||halves[1]==null){foreach(var c in halves)if(c!=null)VisualCorpseFree(c);return;}
                visualCorpses[corpse]=halves[0];visualCorpses[corpse.Twin]=halves[1];
                bool cut=VisualCorpseCut(corpse,fx,halves,joint);
                for(int h=0;h<2;h++){int side=h;halves[h].Holes=VisualCorpseHoles(rig,halves[h],(hole,at)=>fx.Request.HalfOf(joint[hole.Bone],CutNum(at))==side);}
                if(cut)return;
                for(int h=0;h<2;h++)
                {
                    var half=h==0?corpse:corpse.Twin;var c=halves[h];
                    var piece=new int[c.Bones.Length];for(int v=0;v<piece.Length;v++)piece[v]=c.Bones[v]!=null&&fx.Request.HalfOf(joint[v],CutNum(c.Bones[v].position))==h?VisualCorpsePiece(half,c.Part[v]):-1;
                    VisualCorpseSever(c,piece);
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
            whole.Holes=VisualCorpseHoles(rig,whole,null);
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
        // (the nearest such part the corpse has). On a half whose pelvis is a stand-in (NativeCorpse.RootStandIn) a bone with no
        // part of its own above it rides the nearest one below it instead (a cut-off head's neck rides the head, so what of the
        // head the neck's weights reach stays on it), else its parent bone.
        private VisualCorpse VisualCorpseFor(NativeCorpse corpse,NativeDeath death,CapturedAnimation.Player rig,out int[] joint)
        {
            var root=VisualDeathBody(rig,death,death.position-rig.Root.transform.position,out var bones,out joint);if(root==null)return null;
            var c=new VisualCorpse{Root=root,Bones=bones,Part=new int[bones.Length],Bind=new Matrix4x4[bones.Length],All=VisualWorn(root),Edge=DissolveEdgeOf(death),Hood=root.transform.Find(NativeSheldonHood.Renderer)!=null,Skeleton=root.transform.Find(SkeletonRenderer)?.GetComponent<Renderer>()};
            var parentOf=new Dictionary<Transform,int>();for(int v=0;v<bones.Length;v++)if(bones[v]!=null)parentOf[bones[v]]=v;
            bool stand=corpse.RootStandIn;bool Own(int j)=>corpse.Parts.ContainsKey(j)&&!(stand&&j==CapturedRig.Pelvis);
            for(int v=0;v<bones.Length;v++)
            {
                int part=-1;for(int j=corpse.Anchor[joint[v]];j>=0&&part<0;j=CapturedRig.Bones[j].Parent)if(Own(j))part=j;
                if(part<0&&stand)part=VisualOwnBelow(corpse.Anchor[joint[v]],Own);
                if(part<0&&stand&&bones[v]!=null&&bones[v].parent!=null&&parentOf.TryGetValue(bones[v].parent,out int up)&&up<v)part=c.Part[up];
                if(part<0&&corpse.Parts.ContainsKey(CapturedRig.Pelvis))part=CapturedRig.Pelvis;
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
        // The nearest game joint below `from` (breadth first) that `own` holds, or -1.
        private static int VisualOwnBelow(int from,Func<int,bool> own)
        {
            var queue=new Queue<int>();queue.Enqueue(from);
            while(queue.Count>0){int j=queue.Dequeue();for(int k=0;k<CapturedRig.Bones.Length;k++)if(CapturedRig.Bones[k].Parent==j){if(own(k))return k;queue.Enqueue(k);}}
            return -1;
        }
        // Slash kills on the model (owner 2026-10-24: "Sever at joints"): the body cut by piece (per visual bone: `pieceOfBone`,
        // -1 dropped) in its shader, each vertex all of whose weight is on one piece's bones kept, so nothing stretches across the
        // gap as the pieces part, its holes capped (NativeVisualCutPresentation.cs VisualCorpseSeverCut; owner 2026-10-28:
        // "prevent fps drops during enemy kill"); a body it cannot read has each skinned mesh split by piece instead (a vertex
        // belongs to the piece most of its weight is in and keeps only that piece's bones; triangles across pieces go, as do the
        // dropped piece's: a crushed part's fragments and the stumps' blood are the kill wound's; the other half of a split body is
        // its twin's), the copies going with the corpse.
        private void VisualCorpseSever(VisualCorpse c,int[] pieceOfBone){if(!VisualCorpseSeverCut(c,pieceOfBone))VisualSever(c.All,c.Bones,pieceOfBone,ref c.Owned,true);VisualCorpseBones(c,pieceOfBone);}
        // The cut's bones (owner 2026-10-27: "Can you show bones + flesh on gibs too? correct on anamonics too"; Core
        // NativeModelGore): at each joint the cut parts (a bone whose nearest ancestor went with another piece), each side's bones as
        // short stubs on that side's bone, where they lie in its cross-section (the cut piece's sticking out a little, the stump's
        // flush), their marrow dark at the end. In the body's own shader, added to the corpse's renderers so its dissolve and its
        // kill looks reach them; they go with the corpse.
        private static Mesh goreStub,goreDisc;
        private void VisualCorpseBones(VisualCorpse c,int[] pieceOfBone)
        {
            // The skeleton shows a corpse's own bones at its cuts (NativeVisualSkeletonPresentation.cs): no stubs then.
            if(c.Root==null||c.All==null||c.Parent==null||c.Skeleton!=null)return;Material source=null;
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
            var body=CreateVisualBody(rig,id,simulate:false);if(body==null)return null;body.Root.name="Visual bust "+id;
            var joint=VisualJoints(body.Binding);var piece=new int[joint.Length];for(int v=0;v<piece.Length;v++)piece[v]=BustBone(joint[v])?0:-1;
            body.BustNeck=-1;for(int v=0;v<joint.Length&&body.BustNeck<0;v++)if(joint[v]==CapturedRig.Neck)body.BustNeck=v;
            // Each mesh cut to the bust per vertex in the hologram's shader (VisualBustKeep; owner 2026-10-28: "prevent fps drops"),
            // else severed (a copy).
            var index=new Dictionary<Transform,int>();var bones=body.Binding.Bones;for(int v=0;v<bones.Length;v++)if(bones[v]!=null)index[bones[v]]=v;
            cutBlock??=new MaterialPropertyBlock();
            foreach(var r in body.All)
            {
                if(!(r is SkinnedMeshRenderer skin)||skin.sharedMesh==null)continue;var keep=VisualBustKeep(skin,index,joint);
                if(keep==null){VisualSever(new Renderer[]{r},bones,piece,ref owned);continue;}
                r.GetPropertyBlock(cutBlock);cutBlock.SetTexture(CutTexId,keep);cutBlock.SetFloat(CutBaseId,0);cutBlock.SetFloat(CutWidthId,CutWidth);cutBlock.SetVector(CutPickId,new Vector4(1,0,0,0));r.SetPropertyBlock(cutBlock);
            }
            foreach(var r in body.All)
            {
                if(r==null)continue;var mats=new Material[r.sharedMaterials.Length];for(int k=0;k<mats.Length;k++)mats[k]=bustMaterial;
                r.sharedMaterials=mats;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            body.Root.SetActive(false);return body;
        }
        // A mesh's keep values for the bust (NativeVisualBodyFx.cginc CutKeep): 1 where most of a vertex's weight is on the bust's
        // bones, else -1; made once per mesh (every body's skeleton is the bake's). Null when the cut cannot read the mesh.
        private static readonly Dictionary<Mesh,Texture2D> bustKeeps=new Dictionary<Mesh,Texture2D>();
        private Texture2D VisualBustKeep(SkinnedMeshRenderer skin,Dictionary<Transform,int> index,int[] joint)
        {
            var mesh=skin.sharedMesh;if(bustKeeps.TryGetValue(mesh,out var tex))return tex;var m=CutMeshOf(mesh);var bones=skin.bones;
            if(m!=null&&bones.Length>=m.Bind.Length&&SystemInfo.SupportsTextureFormat(TextureFormat.RGFloat))
            {
                var bust=new bool[m.Bind.Length];for(int i=0;i<bust.Length;i++){int v=-1;for(var t=bones[i];t!=null&&v<0;t=t.parent)if(!index.TryGetValue(t,out v))v=-1;bust[i]=v>=0&&BustBone(joint[v]);}
                int n=m.At.Length,height=Math.Max(1,(n+CutWidth-1)/CutWidth);var data=new float[CutWidth*height*2];
                for(int i=0;i<n;i++){var w=m.Weights[i];float on=(bust[w.boneIndex0]?w.weight0:-w.weight0)+(bust[w.boneIndex1]?w.weight1:-w.weight1)+(bust[w.boneIndex2]?w.weight2:-w.weight2)+(bust[w.boneIndex3]?w.weight3:-w.weight3);data[i*2]=on>=0?1:-1;data[i*2+1]=-data[i*2];}
                tex=new Texture2D(CutWidth,height,TextureFormat.RGFloat,false,true){name="Visual bust cut "+mesh.name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                tex.SetPixelData(data,0);tex.Apply(false,true);
            }
            bustKeeps[mesh]=tex;return tex;
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
        private static void VisualCorpseFree(VisualCorpse c){VisualBodyGone(c.Root);c.Root=null;if(c.Owned!=null)foreach(var m in c.Owned)if(m!=null)Destroy(m);c.Owned=null;if(c.CutTexture!=null)Destroy(c.CutTexture);c.CutTexture=null;}
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
                s=new VisualScene{Root=root,Bones=bones,Joint=joints,Bind=new Matrix4x4[bones.Length],All=VisualWorn(root),Edge=DissolveEdgeOf(death),Top=new bool[bones.Length]};VisualSceneHooded(s);
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
            VisualSceneHood(s,death,alpha);VisualSceneFace(s,death,alpha);
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
            if(make){body=VisualBodyOf(rig,a.Id,VisualHooded(a));if(body==null)return false;}
            else if(!visualBodies.TryGetValue(rig,out body)||body.Root==null||body.PosedFrame!=Time.frameCount)return false;
            int n=Mathf.Min(rig.Bones.Length,standPose.Position.Length);
            for(int i=0;i<n;i++){var t=rig.Bones[i];var lp=t.localPosition;var lq=t.localRotation;standPose.Position[i]=CapturedRig.Mirror(new System.Numerics.Vector3(lp.x,lp.y,lp.z));standPose.Rotation[i]=CapturedRig.Mirror(new System.Numerics.Quaternion(lq.x,lq.y,lq.z,lq.w));}
            standPose.World();body.Binding.State.Forget();
            var held=VisualBodyBind(body,rig,a,time,standPose,false,false);var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
            body.Moved=true;if(make)body.Seen=true;return true;
        }
        // The revive gallery's stand-in wears its hat on the model's head as the arena's pilots do (no arena pass draws it): the
        // Sheldon Head as the hood's void and face.
        private void VisualStandHat(CapturedAnimation.Player rig,Actor a,float time)
        {
            if(!visualBodies.TryGetValue(rig,out var body)||body.Root==null||string.IsNullOrEmpty(a.Hat)||a.Hat=="none")return;
            VisualHatHair(body,a.Hat);
            if(a.Hat!=NativeSheldonHead.Hat){var hb=VisualHatBone(rig,a.Hat);if(!AuditorHalo(a.Hat,hb(CapturedRig.Head),time,1,HaloReduced,0,null,color:a.HatColor))sourceModels.Queue(VisualHatModel(a.Hat,a.HatColor),rig.Root.transform.localToWorldMatrix,hb,body:HatBody(a.Hat,a.HatColor));}
            else SheldonHoodStill(body,a.Hat,a.HatColor,rig.Root.transform.localToWorldMatrix,VisualHatBone(rig,a.Hat),time,a.Id,HaloReduced,0,null);
        }
        // The authored death scenes (NativeSceneMotion.cs SourceDeathScene) move the victim's 22 joints from where they were at
        // death: each visual bone follows its joint's motion since then (the scene's joint matrices over the rig's at death),
        // hidden while the scene hides the pelvis or the body is under half opacity. True: the voxel victim and its hat are not
        // drawn. Kept per death until it goes undrawn for 2 s.
        private sealed class VisualScene{public bool[] Top;public GameObject Root;public Transform[] Bones;public int[] Joint;public Matrix4x4[] Bind;public readonly Matrix4x4[] Inverse=new Matrix4x4[22],Now=new Matrix4x4[22];public int Frame=-1;public float Seen;public Renderer[] All;public float Dissolve=-1;public Color Edge;public bool Hood;public int Head=-1;}
        // A scene's or revive's body made with the Sheldon hood: its model head, where the hood's void and face ride (the scene and
        // the revive draw no hat; the hood is the body's own).
        private static void VisualSceneHooded(VisualScene s){s.Hood=s.Root.transform.Find(NativeSheldonHood.Renderer)!=null;s.Head=Array.FindIndex(s.Bones,b=>b!=null&&b.name=="head");}
        // The face gear on a death scene's or a revive's head (the robot's helmet over its hidden hair), fading with it.
        private float sceneFade;private Func<int,float> sceneFadeOf;
        private void VisualSceneFace(VisualScene s,NativeDeath death,float alpha){if(s.Hood||s.Head<0)return;sceneFade=alpha;VisualFaceGear(s.Bones[s.Head],death.id,death.actor?.BodyColor,death.actor?.AccentColor,0,null,alpha<1?sceneFadeOf??=_=>sceneFade:null,null,0);}
        private void VisualSceneHood(VisualScene s,NativeDeath death,float alpha){if(s.Hood&&s.Head>=0)VisualDeadHood(VisualHatFit(s.Bones[s.Head],NativeSheldonHead.Hat),death,alpha);}
        private readonly Dictionary<long,VisualScene> visualScenes=new Dictionary<long,VisualScene>();private readonly List<long> visualScenesGone=new List<long>();
        private bool VisualSceneShown(NativeDeath death,Func<int,Matrix4x4> bone,Func<int,bool> visible,float alpha)
        {
            if(!VisualDeathFor(death))return false;long key=((long)death.id<<32)^(uint)death.seed;
            if(!visualScenes.TryGetValue(key,out var s))
            {
                if(!rigs.TryGetValue(death.id,out var rig))return false;
                var root=VisualDeathBody(rig,death,Vector3.zero,out var bones,out var joint);if(root==null)return false;
                s=new VisualScene{Root=root,Bones=bones,Joint=joint,Bind=new Matrix4x4[bones.Length],All=VisualWorn(root),Edge=DissolveEdgeOf(death)};VisualSceneHooded(s);
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
            VisualSceneHood(s,death,alpha);if(visible(CapturedRig.Head))VisualSceneFace(s,death,alpha);
            return true;
        }
        // PresentDeath, where the body draws: pose the corpse's visual body; true when it stands in for the voxel body this frame
        // (also while hidden for a fade under half opacity).
        private bool VisualCorpseShown(NativeCorpse corpse,float alpha)
        {
            if(!visualCorpses.TryGetValue(corpse,out var c)||c.Root==null)return false;
            c.Frame=Time.frameCount;
            // A split body's cut being made: the first half draws the whole body meanwhile, the second waits (VisualCorpseCut).
            if(c.Cut!=null&&!c.Cut.Finished){if(c.Cut.Done)VisualCutFinish(c.Cut);else if(c.Cut.Halves.Length>1&&c==c.Cut.Halves[1]){if(c.Root.activeSelf)c.Root.SetActive(false);return true;}}
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
            if(c.Holes!=null)VisualHolesCut(c.All,c.Bones,c.Holes,null);
            // The face gear stays on a whole corpse's head as it fades (not on a split half, a crushed head, under a kill look or
            // under the Sheldon hood, whose face it would break through).
            if(c.Look==0&&!c.Hood&&corpse.Twin==null&&corpse.Host==null&&!corpse.IsCrushed(CapturedRig.Head)&&corpse.Death!=null)
            {
                if(c.Head==-2)c.Head=Array.FindIndex(c.Bones,b=>b!=null&&b.name=="head");
                if(c.Head>=0){corpseFade=alpha;VisualFaceGear(c.Bones[c.Head],corpse.Death.id,corpse.Death.actor?.BodyColor,corpse.Death.actor?.AccentColor,0,null,alpha<1?corpseFadeOf??=_=>corpseFade:null,null,0);}
                corpseFade=alpha;VisualSuit(c.Root,corpse.Death.id,corpse.Death.actor?.BodyColor,corpse.Death.actor?.AccentColor,0,null,alpha<1?corpseFadeOf??=_=>corpseFade:null,null,0);
            }
            // A split corpse keeps the robot's helmet on the half that holds its head (its hair is hidden under it).
            else if(c.Look==0&&!c.Hood&&(corpse.Twin!=null||corpse.Host!=null)&&!corpse.IsCrushed(CapturedRig.Head)&&corpse.Death!=null&&WearsHelm(corpse.Death.id)&&VisualCorpseHead(corpse))
            {
                if(c.Head==-2)c.Head=Array.FindIndex(c.Bones,b=>b!=null&&b.name=="head");
                if(c.Head>=0){corpseFade=alpha;VisualFaceGear(c.Bones[c.Head],corpse.Death.id,corpse.Death.actor?.BodyColor,corpse.Death.actor?.AccentColor,0,null,alpha<1?corpseFadeOf??=_=>corpseFade:null,null,0);}
            }
            return true;
        }
        private float corpseFade;private Func<int,float> corpseFadeOf;
        // PresentDeath, once a pilot's model corpse is shown: its hat on its head (owner 2026-10-28: "hat still seems to remove
        // on death"), fitted as the live body wears it (VisualHatFit), on the half its head went to; the Sheldon Head as the
        // body's own cloth hood with its void and the face as it died. False when the corpse has no model head.
        private Matrix4x4 corpseHat;private Func<int,Matrix4x4> corpseHatBone;
        private bool VisualCorpseHat(NativeCorpse corpse,NativeDeath death,float alpha,float clock)
        {
            var a=death.actor;string hat=a?.Hat;if(string.IsNullOrEmpty(hat)||hat=="none"||a.SourceSpec=="siren")return false;var on=corpse;
            if(corpse.Twin!=null&&corpse.Bisect is BisectFx fx&&fx.Request!=null&&fx.Request.HalfOf(CapturedRig.Head,CutNum(corpse.Bind[CapturedRig.Head].GetColumn(3)))==1)on=corpse.Twin;
            if(!visualCorpses.TryGetValue(on,out var c)||c.Root==null||!c.Root.activeSelf||c.Look!=0)return false;
            if(c.Head==-2)c.Head=Array.FindIndex(c.Bones,b=>b!=null&&b.name=="head");if(c.Head<0)return false;
            corpseHat=VisualHatFit(c.Bones[c.Head],hat,!c.Hood&&WearsHelm(death.id));corpseHatBone??=_=>corpseHat;
            if(hat==NativeSheldonHead.Hat)return c.Hood&&VisualDeadHood(corpseHat,death,alpha);
            if(!AuditorHalo(hat,corpseHat,clock,alpha,HaloReduced,0,null,color:a.HatColor)&&corpse.HatModel!=null)sourceModels.QueueShadowless(VisualHatModel(hat,a.HatColor)??corpse.HatModel,Matrix4x4.identity,corpseHatBone,null,HatBody(hat,a.HatColor),null,alpha);
            return true;
        }
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
                if(c.Look!=1){var ghost=VisualEchoMaterial("NativeWeaponGhost");if(ghost==null)return false;foreach(var r in c.All){if(r==null)continue;var mats=new Material[r.sharedMaterials.Length];for(int k=0;k<mats.Length;k++)mats[k]=ghost;r.sharedMaterials=mats;}if(c.Skeleton!=null)c.Skeleton.enabled=false;c.Look=1;}
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
            VisualCorpsePoolStep();
            if(visualCorpses.Count==0&&visualScenes.Count==0)return;
            visualCorpsesGone.Clear();
            foreach(var pair in visualCorpses)
            {
                bool kept=galleryCorpse!=null&&(galleryCorpse==pair.Key||galleryCorpse.Twin==pair.Key);foreach(var c in corpses.Values)if(c==pair.Key||c.Twin==pair.Key){kept=true;break;}
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
        // The same through a shader that also reads the accent tint (the pack afterimage: the copy's life share and seed); false
        // without the material (the caller draws the ghost).
        private MaterialPropertyBlock visualPackEchoBlock;
        private bool VisualEchoDraw(VisualEcho echo,string shader,Color tint,float opacity,Vector4 accent)
        {
            if(echo==null||echo.Count==0)return false;var material=VisualEchoMaterial(shader);if(material==null)return false;if(!(opacity>0))return true;
            visualPackEchoBlock??=new MaterialPropertyBlock();visualPackEchoBlock.SetVector(VisualEchoTint,new Vector4(tint.r,tint.g,tint.b,opacity*VisualEchoShare));visualPackEchoBlock.SetVector(VisualGhostAccent,accent);
            for(int i=0;i<echo.Count;i++){var mesh=echo.Meshes[i];if(mesh==null)continue;for(int sub=0;sub<mesh.subMeshCount;sub++)Graphics.DrawMesh(mesh,echo.At[i],material,0,null,sub,visualPackEchoBlock);}
            return true;
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
                var body=CreateVisualBody(holder,id,simulate:false);if(body==null)return false;body.Root.name="Visual ghost "+id;
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
