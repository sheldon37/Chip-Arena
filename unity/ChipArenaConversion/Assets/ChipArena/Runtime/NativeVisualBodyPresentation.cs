using System;
using System.Collections.Generic;
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
            public string Spec, Accent; public int PosedFrame=-10; public bool Seen, Moved, SpawnOn, Ghosted; public Vector3 At; public float Flash=1, Glow=-1;
            public Material[][] Plain, Spawning;
            // The gear's anchors (the bake's VisualGearCore and VisualGearDevice, the band's radius from VisualGearDeviceRim), when
            // the body last appeared or finished forming (the core's flare), the last seated chip (its plant's key, when, colour)
            // and the pilot's accent (linear, the bay's and lens's rest colour).
            public Transform GearCore, GearDevice; public float GearRadius, LitAt=float.NaN, SeatAt=float.NaN; public int SeatKey=int.MinValue; public bool Building;
            public Color ChipColor=Color.cyan, AccentLinear=Color.cyan;
            public readonly Vector3[] HandPosition=new Vector3[2]; public readonly Quaternion[] HandRotation=new Quaternion[2];
            // Its own weapons (NativeVisualWeaponPresentation.cs): the bake's entries by spec, the one in hand this frame (Held,
            // fitted on the right hand bone, whose scale RightScale restores) and the one shown (when).
            public Dictionary<string,VisualWeapon> Weapons; public VisualWeapon Held, Shown; public int ShownFrame=-1; public Vector3 RightScale=Vector3.one;
        }
        public const string VisualBodyResource="ChipArenaVisualBody", VoxelBodiesArgument="--ca-voxel-bodies";
        private static GameObject visualBodyPrefab; private static int visualBodyState; // 0 unread, 1 drawn, -1 absent or turned off
        /// <summary>Settings · Player model is Classic (set each frame from the preferences): every pilot draws its voxel body.</summary>
        public bool ClassicBodies;
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
            // Its own weapon in hand, if the bake carries one: the grasp on its handguard and stock.
            var held=body.Held=VisualWeaponFor(body,a);var grasp=VisualGrasp(a,pose,time,ChipLookOf(a.Id));if(held!=null)VisualWeaponGrasp(ref grasp,held);
            body.Binding.Apply(pose,grasp,a.Grounded&&!roll&&!juggle,roll||juggle);
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
            var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
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
            body.All=root.GetComponentsInChildren<Renderer>(true);body.Plain=new Material[body.All.Length][];
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="VisualGearCore")body.GearCore=t;else if(t.name=="VisualGearDevice")body.GearDevice=t;
                else if(t.name=="VisualGearDeviceRim")body.GearRadius=t.localPosition.magnitude;
            }
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
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var slot in ClothSlots)if(r.name.StartsWith(slot,StringComparison.Ordinal)){cloth.Add(r);break;}
            body.Cloth=cloth.ToArray();
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
            int layer=first?10:0;var camera=first?ViewCamera:null;
            if(body.GearCore!=null&&!first)
            {
                var p=LoadoutPilot(a.Id);float max=p?.Stats!=null?(float)p.Stats["hp"]:100;float health=max>0?(p!=null?p.Hp:a.Hp)/max:1;
                float g=NativePilotGear.CoreGlow(time,health,time-body.LitAt,a.Id);gearMatrix=body.GearCore.localToWorldMatrix;
                sourceModels.Queue(NativeSourceModels.GearCore,gearMatrix,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,new Vector4(g,g,g,1),null,flash);
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
            gearMatrix=m;sourceModels.Queue(NativeSourceModels.GearDevice,m,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,new Vector4(dev.Strips,dev.Strips,dev.Strips,1),null,flash);
            // The bay shows only while the lid is up; the gear's source frame mirrors into Unity's (x toward the back of the forearm).
            if(dev.Lid>.02f){var bay=(dev.BayChip?body.ChipColor:body.AccentLinear)*dev.Bay;sourceModels.Queue(NativeSourceModels.GearBay,m,gearBone,null,layer,camera,Color.white,accent,1,reveal,tint,new Vector4(bay.r,bay.g,bay.b,1),null,flash);}
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
            var body=CreateVisualBody(view,-999);if(body==null)return null;
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
        // overlay and shrink), the skeleton scaled by the parts' scale. A whole body only: a cut, crushed, smashed or bisected
        // corpse, and a swarm, soul or frost look, keep the voxel body (their effects are made of its parts). It hides while the
        // voxel body would be under half opacity and goes with its corpse.
        private sealed class VisualCorpse{public GameObject Root;public Transform[] Bones;public int[] Part;public Matrix4x4[] Bind;public int Frame=-1;}
        private readonly Dictionary<NativeCorpse,VisualCorpse> visualCorpses=new Dictionary<NativeCorpse,VisualCorpse>();
        private readonly List<NativeCorpse> visualCorpsesGone=new List<NativeCorpse>();
        // A death's visual body: an instance posed from the rig (in its last pose), `offset` from it, in the pilot's accent, without
        // shadows (as the voxel corpse); `joint` holds, per bone, the nearest game joint mapped at or above it (the pelvis for the
        // root). Inactive; null without a bake.
        private GameObject VisualDeathBody(CapturedAnimation.Player rig,NativeDeath death,Vector3 offset,out Transform[] bones,out int[] joint)
        {
            bones=null;joint=null;var root=Instantiate(visualBodyPrefab);root.name="Visual corpse "+death.id;var skeleton=root.transform.Find("VisualSkeleton");
            if(skeleton==null){Destroy(root);return null;}
            var at=rig.Root.transform;root.transform.SetPositionAndRotation(at.TransformPoint(new Vector3(0,NativeVisualRig.Floor,0))+offset,at.rotation);
            var binding=new NativeVisualRigBinding(root.transform,skeleton);var pose=rig.Pose;pose.World();
            binding.Apply(pose,VisualGrasp(death.actor??new Actor(),pose,death.at,0),false,false);
            bones=binding.Bones;var mapped=new int[bones.Length];joint=new int[bones.Length];for(int v=0;v<bones.Length;v++)mapped[v]=-1;
            for(int j=0;j<CapturedRig.Bones.Length;j++){int v=binding.Rig.Of[j];if(v>=0&&v<bones.Length)mapped[v]=j;}
            for(int v=0;v<bones.Length;v++){int j=-1;for(int u=v;u>=0&&j<0;u=binding.Rig.Parent[u])j=mapped[u];joint[v]=j>=0?j:CapturedRig.Pelvis;}
            var block=new MaterialPropertyBlock();ColorUtility.TryParseHtmlString(death.actor?.AccentColor??"#70e1d0",out var accent);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
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
            if(rig==null||!VisualDeathFor(death)||corpse.Bisect!=null||corpse.Smash!=null||corpse.Severed.Count>0||corpse.CrushPart>=0)return;
            var root=VisualDeathBody(rig,death,death.position-rig.Root.transform.position,out var bones,out var joint);if(root==null)return;
            var c=new VisualCorpse{Root=root,Bones=bones,Part=new int[bones.Length],Bind=new Matrix4x4[bones.Length]};
            for(int v=0;v<bones.Length;v++){int part=corpse.Anchor[joint[v]];if(!corpse.Parts.ContainsKey(part))part=CapturedRig.Pelvis;c.Part[v]=part;c.Bind[v]=bones[v].localToWorldMatrix;}
            visualCorpses[corpse]=c;
        }
        // The authored death scenes (NativeSceneMotion.cs SourceDeathScene) move the victim's 22 joints from where they were at
        // death: each visual bone follows its joint's motion since then (the scene's joint matrices over the rig's at death),
        // hidden while the scene hides the pelvis or the body is under half opacity. True: the voxel victim and its hat are not
        // drawn. Kept per death until it goes undrawn for 2 s.
        private sealed class VisualScene{public GameObject Root;public Transform[] Bones;public int[] Joint;public Matrix4x4[] Bind;public readonly Matrix4x4[] Inverse=new Matrix4x4[22],Now=new Matrix4x4[22];public int Frame=-1;public float Seen;}
        private readonly Dictionary<long,VisualScene> visualScenes=new Dictionary<long,VisualScene>();private readonly List<long> visualScenesGone=new List<long>();
        private bool VisualSceneShown(NativeDeath death,Func<int,Matrix4x4> bone,Func<int,bool> visible,float alpha)
        {
            if(!VisualDeathFor(death))return false;long key=((long)death.id<<32)^(uint)death.seed;
            if(!visualScenes.TryGetValue(key,out var s))
            {
                if(!rigs.TryGetValue(death.id,out var rig))return false;
                var root=VisualDeathBody(rig,death,Vector3.zero,out var bones,out var joint);if(root==null)return false;
                s=new VisualScene{Root=root,Bones=bones,Joint=joint,Bind=new Matrix4x4[bones.Length]};
                for(int v=0;v<bones.Length;v++)s.Bind[v]=bones[v].localToWorldMatrix;
                for(int j=0;j<22;j++)s.Inverse[j]=rig.Bones[j].worldToLocalMatrix;
                visualScenes[key]=s;
            }
            s.Frame=Time.frameCount;s.Seen=Time.unscaledTime;
            if(alpha<.5f||!visible(CapturedRig.Pelvis)){if(s.Root.activeSelf)s.Root.SetActive(false);return true;}
            if(!s.Root.activeSelf)s.Root.SetActive(true);
            for(int j=0;j<22;j++)s.Now[j]=bone(j)*s.Inverse[j];
            for(int v=0;v<s.Bones.Length;v++){var m=s.Now[s.Joint[v]]*s.Bind[v];s.Bones[v].SetPositionAndRotation(m.GetColumn(3),m.rotation);}
            return true;
        }
        // PresentDeath, where the body draws: pose the corpse's visual body; true when it stands in for the voxel body this frame
        // (also while hidden for a fade under half opacity).
        private bool VisualCorpseShown(NativeCorpse corpse,float alpha)
        {
            if(!visualCorpses.TryGetValue(corpse,out var c)||c.Root==null||corpse.Swarm!=null)return false;
            c.Frame=Time.frameCount;
            if(alpha<.5f){if(c.Root.activeSelf)c.Root.SetActive(false);return true;}
            if(!c.Root.activeSelf)c.Root.SetActive(true);
            c.Bones[0].localScale=Vector3.one*Mathf.Max(.001f,corpse.PartScale);
            for(int v=0;v<c.Bones.Length;v++){var m=corpse.PartFrom(c.Part[v])*c.Bind[v];c.Bones[v].SetPositionAndRotation(m.GetColumn(3),m.rotation);}
            return true;
        }
        // After the deaths are presented: hide the corpse bodies not drawn this frame; drop those whose corpse is gone.
        private void VisualCorpsesShown()
        {
            if(visualCorpses.Count==0&&visualScenes.Count==0)return;
            visualCorpsesGone.Clear();
            foreach(var pair in visualCorpses)
            {
                bool kept=false;foreach(var c in corpses.Values)if(c==pair.Key){kept=true;break;}
                if(!kept||pair.Value.Root==null){visualCorpsesGone.Add(pair.Key);continue;}
                if(pair.Value.Frame!=Time.frameCount&&pair.Value.Root.activeSelf)pair.Value.Root.SetActive(false);
            }
            foreach(var gone in visualCorpsesGone){var root=visualCorpses[gone].Root;if(root!=null)Destroy(root);visualCorpses.Remove(gone);}
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
            foreach(var c in visualCorpses.Values)if(c.Root!=null)Destroy(c.Root);visualCorpses.Clear();
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
                if(body.Root==null){visualBodiesGone.Add(pair.Key);continue;}
                if(body.Root.activeSelf!=body.Seen)body.Root.SetActive(body.Seen);
                VisualWeaponShown(body);body.Seen=false;
            }
            foreach(var rig in visualBodiesGone)visualBodies.Remove(rig);
        }
        // The bodies live under their rigs' roots, so disposing a rig destroys its body; this drops the records.
        private void ClearVisualBodies()
        {
            foreach(var body in visualBodies.Values)if(body.Root!=null)Destroy(body.Root);visualBodies.Clear();
            if(viewBody?.Root!=null)Destroy(viewBody.Root);viewBody=null;
            if(visualGhosts!=null)foreach(var m in visualGhosts)if(m!=null)Destroy(m);visualGhosts=null;
        }
    }
}
