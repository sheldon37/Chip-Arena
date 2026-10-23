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
    // drawn weapon, its muzzle, blade sweeps and wrist effects follow the visual hands. It takes the hit flash and the
    // match-start spawn-in in its own shader (VisualBodyLook), and wears the pilot gear (VisualGear: the chest core and the
    // right-wrist chip device, Core NativePilotGear) at anchors its bake measured. Still voxel: first-person arms, intros,
    // portraits, the draft, corpses and kill effects; hats are not drawn on it yet.
    public sealed partial class ArenaPresentation
    {
        private sealed class VisualBody
        {
            public GameObject Root; public NativeVisualRigBinding Binding; public Renderer[] Cloth, All; public MaterialPropertyBlock Block;
            public string Spec, Accent; public int PosedFrame=-10; public bool Seen, Moved, SpawnOn, Ghosted; public Vector3 At; public float Flash=1;
            public Material[][] Plain, Spawning;
            // The gear's anchors (the bake's VisualGearCore and VisualGearDevice, the band's radius from VisualGearDeviceRim), when
            // the body last appeared or finished forming (the core's flare), the last seated chip (its plant's key, when, colour)
            // and the pilot's accent (linear, the bay's and lens's rest colour).
            public Transform GearCore, GearDevice; public float GearRadius, LitAt=float.NaN, SeatAt=float.NaN; public int SeatKey=int.MinValue; public bool Building;
            public Color ChipColor=Color.cyan, AccentLinear=Color.cyan;
            public readonly Vector3[] HandPosition=new Vector3[2]; public readonly Quaternion[] HandRotation=new Quaternion[2];
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
            body.Moved=false;
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
            body.Binding.Apply(pose,VisualGrasp(a,pose,time,ChipLookOf(a.Id)),a.Grounded&&!roll&&!juggle,roll||juggle);
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
            body.All=root.GetComponentsInChildren<Renderer>(true);body.Plain=new Material[body.All.Length][];
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="VisualGearCore")body.GearCore=t;else if(t.name=="VisualGearDevice")body.GearDevice=t;
                else if(t.name=="VisualGearDeviceRim")body.GearRadius=t.localPosition.magnitude;
            }
            if(body.GearDevice!=null&&!(body.GearRadius>.01f))body.GearDevice=null;
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
            if(!visualBodies.TryGetValue(rig,out var body)||body.Root==null)return;
            var look=new VisualSpawnLook();bool on=SpawnVisualLook(spawn,rig,ref look);body.Building=on&&look.Front.x<1e4f;
            VisualGear(body,a,time,reveal,tint,flash);
            bool ghost=on&&look.Ghost&&VisualGhosts()!=null;
            if(ghost!=body.Ghosted)
            {
                if(ghost&&body.Spawning==null)body.Spawning=VisualSpawnSlots(body);
                for(int k=0;k<body.All.Length;k++)if(body.All[k]!=null)body.All[k].sharedMaterials=ghost?body.Spawning[k]:body.Plain[k];
                body.Ghosted=ghost;
            }
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
        private void VisualGear(VisualBody body,Actor a,float time,Func<int,float> reveal,Func<int,Color> tint,float flash)
        {
            if(body.GearCore==null&&body.GearDevice==null||!sourceModels.PilotGear(body.GearRadius>.01f?body.GearRadius:.04f))return;
            if(!body.Root.activeSelf||body.Building||float.IsNaN(body.LitAt))body.LitAt=time;
            var accent=AccentTint(a.AccentColor);gearBone??=GearBoneOf;
            if(body.GearCore!=null)
            {
                var p=LoadoutPilot(a.Id);float max=p?.Stats!=null?(float)p.Stats["hp"]:100;float health=max>0?(p!=null?p.Hp:a.Hp)/max:1;
                float g=NativePilotGear.CoreGlow(time,health,time-body.LitAt,a.Id);gearMatrix=body.GearCore.localToWorldMatrix;
                sourceModels.Queue(NativeSourceModels.GearCore,gearMatrix,gearBone,null,0,null,Color.white,accent,1,reveal,tint,new Vector4(g,g,g,1),null,flash);
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
            gearMatrix=m;sourceModels.Queue(NativeSourceModels.GearDevice,m,gearBone,null,0,null,Color.white,accent,1,reveal,tint,new Vector4(dev.Strips,dev.Strips,dev.Strips,1),null,flash);
            // The bay shows only while the lid is up; the gear's source frame mirrors into Unity's (x toward the back of the forearm).
            if(dev.Lid>.02f){var bay=(dev.BayChip?body.ChipColor:body.AccentLinear)*dev.Bay;sourceModels.Queue(NativeSourceModels.GearBay,m,gearBone,null,0,null,Color.white,accent,1,reveal,tint,new Vector4(bay.r,bay.g,bay.b,1),null,flash);}
            var h=NativePilotGear.Hinge(r);var lens=(dev.LensChip?body.ChipColor:body.AccentLinear)*dev.Lens;
            gearMatrix=m*Matrix4x4.TRS(new Vector3(-h.X,h.Y,h.Z),Quaternion.AngleAxis(dev.Lid*NativePilotGear.LidOpenDegrees,Vector3.forward),Vector3.one);
            sourceModels.Queue(NativeSourceModels.GearLid,gearMatrix,gearBone,null,0,null,Color.white,accent,1,reveal,tint,new Vector4(lens.r,lens.g,lens.b,1),null,flash);
            if(dev.Chip)
            {
                var s=NativePilotGear.Seat(r);var chip=body.ChipColor*1.6f;gearMatrix=m*Matrix4x4.Translate(new Vector3(-s.X,s.Y,s.Z));
                sourceModels.Queue(NativeSourceModels.GearChip,gearMatrix,gearBone,null,0,null,Color.white,accent,1,reveal,tint,new Vector4(chip.r,chip.g,chip.b,1),null,flash);
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
        private void ClearVisualBodies()
        {
            foreach(var body in visualBodies.Values)if(body.Root!=null)Destroy(body.Root);visualBodies.Clear();
            if(visualGhosts!=null)foreach(var m in visualGhosts)if(m!=null)Destroy(m);visualGhosts=null;
        }
    }
}
