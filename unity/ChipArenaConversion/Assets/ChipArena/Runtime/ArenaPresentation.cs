using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation : MonoBehaviour
    {
        private Mesh cube;
        private CapturedAnimation captured;
        private NativeCombatGlow combatGlow;private NativeBlastPresentation blasts;private NativeFieldPresentation fieldTelegraphs;private NativeMeleeFlashPresentation meleeFlash;
        // The shared effects renderer (owner 2026-10-11 effects overhaul): the blasts, field cores, burst sparks, melee hit sparks
        // and slam dust and the impact contact flashes queue into it during RenderGlow, and it draws them once, sorted, at the
        // end (after the combat glow, also on its early return). It follows the low graphics tier (LowGraphics).
        private NativeVfxDraw vfx;
        public float GlowQuality=1,BlastQuality=1;public bool TrailsEnabled=true;
        // The clock of the field telegraphs, their C6 field emitters, the eruption stand-in (C9) and the projectile models,
        // modules, emitters, halos and trails (C10). A replica's arena.Time only moves when a 20 Hz host frame lands, so a guest
        // uses the continuous snapshot clock it renders at (Render's renderTime, arena.ReplicaRenderTime: about 0.1 s behind the
        // newest frame, as the source's SnapshotBuffer cursor). Fields born in the newest frame then start with a briefly
        // negative age, as in the source. Host and killcam replay: PresentationClock. It never runs backwards within a
        // generation, so the .25 s rewind clears hold.
        private float FieldClock(NativeArena arena)=>arena.Replica&&hazardClock<=arena.Time&&arena.Time-hazardClock<1?hazardClock:PresentationClock(arena);
        // paused: the match itself is stopped (offline menu), so the blasts hold still; see NativeBlastFrame.Step.
        public void RenderGlow(NativeArena arena,Camera camera,bool paused=false)
        {float now=PresentationClock(arena),fields=FieldClock(arena);if(vfx!=null)vfx.Low=LowGraphics;vfx?.Begin();fieldTelegraphs?.Collect(arena,fields);StructureFx(arena,camera,fields);if(blasts!=null)blasts.Trails=TrailsEnabled;blasts?.Render(arena,camera,now,BlastQuality,paused,fieldTelegraphs);EventWorldFx(arena,camera,fields);fieldTelegraphs?.Render(arena,camera,fields,abilityPreview,abilityPreviewClamped);shotTracker.Begin(arena,arena.Generation,arena.Replica,arena.Time);shots?.Render(arena,camera,fields,BlastQuality,paused,shotTracker);MeleeKitFx(arena,camera,fields);meleeFlash?.Render(arena,camera,fields,WeaponEffectsQuality);WeaponFx(arena,camera,fields);DevEffects(arena,camera,fields);JumpFx(arena,fields);AirAttackFx(arena,fields);BloodFx(arena,camera,fields);if(combatGlow==null){vfx?.Submit(camera);return;}combatGlow.Tracker=shotTracker;combatGlow.Clock=fields;combatGlow.Quality=GlowQuality;combatGlow.Trails=TrailsEnabled;combatGlow.Render(arena,camera,ViewCamera,FirstPerson,rigs,ScopeActive?null:viewRig,FirstPerson&&viewRig!=null&&arena.ViewPilot.Pose.MagicVisual>0?(Matrix4x4?)StaffMatrix(viewRig,true):null);vfx?.Submit(camera);}
        private readonly AudioClip[] gunSounds=new AudioClip[4];
        private AudioClip reloadStart,reloadEnd,hitSound;
        private AudioSource localAudio;
        private readonly AudioSource[] shotAudio=new AudioSource[24];
        private int nextAudio;
        public int LocalId=1;
        public bool SoundEnabled=true;
        public float LastHitUntil,LastHitDamage;
        public bool LastHeadshot;
        private static readonly string[] GunStates={"ready","aim","fire","reload","block"};
        private readonly Dictionary<int,CapturedAnimation.Player> rigs=new Dictionary<int,CapturedAnimation.Player>();
        private readonly MeleeImpacts meleeImpacts=new MeleeImpacts();
        private readonly List<int> removedRigs=new List<int>();
        private readonly System.Diagnostics.Stopwatch animationTimer=new System.Diagnostics.Stopwatch();
        public bool CapturedPlayers=true, Viewer;
        public int ViewerSet, ViewerClip;
        public float ViewerTime;
        public double AnimationMs { get; private set; }
        public string AnimationMode => CapturedPlayers?"Captured melee + procedural guns":"Original melee + procedural guns";
        public string PlayerState { get; private set; }="idle";
        public int PreviewCount => ViewerSet<4?CapturedClips.Sets[ViewerSet].Length:GunStates.Length;
        public float PreviewDuration => ViewerSet<4?CapturedClips.Sets[ViewerSet][ViewerClip].Duration:ViewerClip==3?GunRules.Get((WeaponId)(ViewerSet-3)).Reload:1.5f;
        public bool PreviewLoop => ViewerSet<4?CapturedClips.Sets[ViewerSet][ViewerClip].Loop:ViewerClip!=3;
        public string PreviewName => ViewerSet<4?CapturedClips.Sets[ViewerSet][ViewerClip].Name:GunRules.Name((WeaponId)(ViewerSet-3))+" / "+GunStates[ViewerClip];
        public string PreviewSource => ViewerSet<4?CapturedClips.Sets[ViewerSet][ViewerClip].Source:"Source gun pose adaptation";
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Material> compatibilityMaterials = new List<Material>();
        private readonly List<Matrix4x4>[] batches = MakeBatches();
        private readonly Matrix4x4[] drawBuffer = new Matrix4x4[1023];
        private readonly Dictionary<int, Vector3> visualPositions = new Dictionary<int, Vector3>();
        private readonly List<GameObject> debris = new List<GameObject>();
        private readonly List<GameObject> coverObjects = new List<GameObject>();
        private ParticleSystem particles;
        private Material particleMaterial;
        private int lastEvent;
        private Transform stage;
        public bool DebrisEnabled = true;
        public bool ForceCompatibilityRendering;
        public bool UsesInstancing => !ForceCompatibilityRendering && SystemInfo.supportsInstancing;
        public string RenderingMode => UsesInstancing ? "GPU instanced" : "Compatibility";
        public int DebrisCount { get { debris.RemoveAll(o => o == null); return debris.Count; } }
        public int ParticleCount { get { return particles != null ? particles.particleCount : 0; } }
        public int DrawnParts { get; private set; }
        public static Vector3 Vec(V3 v) { return new Vector3(v.x, v.y, v.z); }

        public void Initialize(bool buildArena=true)
        {
            // Unity native allocations must run after AddComponent, never in field initializers.
            pitProperties=new MaterialPropertyBlock();
            GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = sample.GetComponent<MeshFilter>().sharedMesh; CapturedAnimation.Release(sample);
            Color[] colors = { new Color(.17f,.85f,.83f), new Color(.45f,.58f,1), new Color(.92f,.30f,.38f), new Color(.08f,.11f,.17f), new Color(.87f,.95f,1), new Color(.43f,.51f,.56f), new Color(1,.75f,.3f), new Color(1,.32f,.08f), new Color(.15f,.8f,1), new Color(.72f,.76f,1), new Color(.65f,.25f,.9f), new Color(1,.95f,.72f) };
            Shader shader = Resources.Load<Shader>(buildArena?"PrototypeLit":"NativeLit");
            if (shader == null) throw new System.InvalidOperationException("PrototypeLit shader is missing");
            foreach (Color color in colors)
            {
                materials.Add(new Material(shader) { color = color, enableInstancing = true });
                compatibilityMaterials.Add(new Material(shader) { color = color, enableInstancing = false });
            }
            for(int i=7;i<materials.Count;i++){materials[i].SetColor("_Emission",colors[i]*.55f);compatibilityMaterials[i].SetColor("_Emission",colors[i]*.55f);}
            Debug.Log($"Unity prototype renderer: {RenderingMode}; {SystemInfo.graphicsDeviceType}; {SystemInfo.graphicsDeviceName}; shaderSupported={shader.isSupported}");
            captured=new CapturedAnimation();if(!buildArena){vfx=new NativeVfxDraw();combatGlow=new NativeCombatGlow(transform,vfx);blasts=new NativeBlastPresentation(transform,vfx);shots=new NativeShotPresentation(vfx);blasts.Sparks=shots.SparkPool;meleeFlash=new NativeMeleeFlashPresentation(vfx);sourceModels=new NativeSourceModels();}
            if(!buildArena)fieldTelegraphs=new NativeFieldPresentation(vfx);
            string[] soundNames={"","pistol","rifle","machinegun"};
            for(int i=1;i<4;i++) gunSounds[i]=Resources.Load<AudioClip>("GunAudio/"+soundNames[i]);
            reloadStart=Resources.Load<AudioClip>("GunAudio/reload-start"); reloadEnd=Resources.Load<AudioClip>("GunAudio/reload-end");hitSound=Resources.Load<AudioClip>("GunAudio/hit");
            localAudio=gameObject.AddComponent<AudioSource>();localAudio.playOnAwake=false;
            for(int i=0;i<shotAudio.Length;i++) { var go=new GameObject("Shot sound "+i);go.transform.SetParent(transform);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=3;source.maxDistance=50;shotAudio[i]=source; }
            stage = new GameObject("Arena geometry").transform; stage.SetParent(transform);
            if(buildArena)
            {
            Block("Floor", new Vector3(0, -.35f, 0), new Vector3(50, .7f, 50), materials[3], true);
            for (int i = -24; i <= 24; i += 4)
            {
                Block("Grid X", new Vector3(i,.012f,0), new Vector3(.025f,.014f,48), materials[0], false);
                Block("Grid Z", new Vector3(0,.012f,i), new Vector3(48,.014f,.025f), materials[0], false);
            }
            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2; float sign = i % 2 == 0 ? -1 : 1;
                Vector3 pos = alongX ? new Vector3(0,.9f,sign*24.5f) : new Vector3(sign*24.5f,.9f,0);
                Block("Boundary",pos,alongX?new Vector3(50,1.8f,1):new Vector3(1,1.8f,50),materials[3],true);
                pos.y=1.82f; Block("Boundary light",pos,alongX?new Vector3(50,.06f,1.04f):new Vector3(1.04f,.06f,50),materials[0],false);
            }
            }
            var fx = new GameObject("Impact particles"); fx.transform.SetParent(transform); particles = fx.AddComponent<ParticleSystem>(); particles.Stop();
            var main = particles.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20000; main.startLifetime = .55f; main.startSpeed = 0; main.startSize = .12f; main.gravityModifier = .25f;
            var emission = particles.emission; emission.enabled = false; var shape = particles.shape; shape.enabled = false;
            particleMaterial = new Material(Resources.Load<Shader>("PrototypeParticles"));
            var pr = particles.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = particleMaterial;
            var size = particles.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
        }
        private GameObject Block(string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetParent(stage); o.transform.position=position; o.transform.localScale=scale;
            o.GetComponent<Renderer>().sharedMaterial=material; if(!collider) Destroy(o.GetComponent<Collider>()); return o;
        }
        /// <summary>Clears the presented world. <paramref name="keepWorldMarks"/>: the killcam starts or ends, so the destruction
        /// kit keeps its decals and structure records (NativeStructureFxPresentation.Reset), which the event buffer cannot rebuild.</summary>
        public void ResetPresentation(bool keepWorldMarks=false)
        {
            ClearSourceActors();combatGlow?.Clear();blasts?.Clear();ClearStructureFx(keepWorldMarks);ClearBlood(!keepWorldMarks);fieldTelegraphs?.Clear();shots?.Clear();vfx?.Clear();meleeFlash?.Clear();ClearMeleeKit();shotTracker.Clear();hitLedger.Clear();aimMotion.Reset();viewRig?.Dispose();viewRig=null;lastEvent=0; LastHitUntil=0; visualPositions.Clear(); particles.Clear();
            foreach(var source in shotAudio) if(source!=null) source.Stop(); if(localAudio!=null) localAudio.Stop();
            foreach(var rig in rigs.Values) rig.Dispose(); rigs.Clear();
            foreach(var o in debris) if(o!=null) Destroy(o); debris.Clear();
            foreach(var o in coverObjects) if(o!=null) Destroy(o); coverObjects.Clear();
            ClearWeaponPresentation();
        }
        public Vector3 Position(Actor actor) { Vector3 p; return visualPositions.TryGetValue(actor.Id,out p)?p:Vec(actor.Position); }
        public void Render(Snapshot s, float renderTime, bool smooth)
        {
            if(s==null) return;sourceModels?.Begin();hazardClock=renderTime;ObserveHits(s);BeginWeaponFx(s);
            foreach(var list in batches) list.Clear(); DrawnParts=0;
            removedRigs.Clear();
            foreach(int id in rigs.Keys) { bool exists=false; foreach(var a in s.Actors) if(a.Id==id) { exists=true; break; } if(!exists) removedRigs.Add(id); }
            foreach(int id in removedRigs) { rigs[id].Dispose(); rigs.Remove(id); visualPositions.Remove(id); }
            animationTimer.Restart();
            foreach(Actor a in s.Actors)
            {
                Vector3 old; Vector3 target=Vec(a.Position);
                if(!visualPositions.TryGetValue(a.Id,out old)||(old-target).sqrMagnitude>64) old=target;
                Vector3 p=smooth?Vector3.Lerp(old,target,1-Mathf.Exp(-18*Time.unscaledDeltaTime)):target;
                visualPositions[a.Id]=p;
                if(a.Alive&&(CapturedPlayers||Viewer||GunRules.Ranged(a.Weapon)))PrepareCapturedPose(a,p,renderTime);
            }
            // Pose everyone before drawing: a held victim must use this frame's claw, regardless of seat order.
            PrepareInteractions(s,renderTime);
            foreach(Actor a in s.Actors)
            {
                Vector3 p=visualPositions[a.Id];
                if(FirstPerson&&a.Id==LocalId&&a.Alive)HiddenSweep(a,p,renderTime,CapturedPlayers||Viewer||GunRules.Ranged(a.Weapon));
                if(!a.Alive||(FirstPerson&&a.Id==LocalId)) continue;
                int color=a.Bot?2:a.Id-1;
                if(renderTime<a.FlashUntil) color=4;
                if(CapturedPlayers||Viewer||GunRules.Ranged(a.Weapon)) CapturedPose(a,p,renderTime,color);
                else Pose(a,p,renderTime,color);
            }
            animationTimer.Stop(); AnimationMs=animationTimer.Elapsed.TotalMilliseconds;
            RenderMeleeTrails(renderTime);
            // Native arenas draw their projectiles with NativeShotPresentation (source models); only the prototype draws these.
            if(sourceModels==null)foreach(Shot p in s.Shots)
            {
                if(GunRules.Ranged(p.Weapon)) Add(11,Matrix4x4.TRS(Vec(p.Position),Quaternion.LookRotation(Vec(p.Velocity)),new Vector3(.04f,.04f,Mathf.Clamp(p.Velocity.Length*.01f,.25f,1.5f))));
                else Add(p.MagicVisual>0?6+p.MagicVisual:4, Matrix4x4.TRS(Vec(p.Position),Quaternion.Euler(renderTime*140,renderTime*90,0),Vector3.one*Mathf.Max(.18f,p.Radius*2)));
            }
            DrawBatches(null,0,true);
            if(FirstPerson)RenderFirstPerson(s,renderTime);else aimMotion.Sample(renderTime,false);
            while(coverObjects.Count<s.Covers.Length) coverObjects.Add(null);
            for(int i=0;i<s.Covers.Length;i++)
            {
                Cover c=s.Covers[i]; var o=coverObjects[i];
                if(c.Hp<=0) { if(o!=null) Destroy(o); coverObjects[i]=null; continue; }
                if(o==null) coverObjects[i]=Block("Breakable cover "+c.Id,Vec(c.Position)+Vector3.up*c.Height*.5f,new Vector3(c.HalfX*2,c.Height,c.HalfZ*2),materials[3],true);
            }
            foreach(ArenaEvent e in s.Events)
            {
                if(e.Id<=lastEvent) continue; lastEvent=e.Id;
                // Captured melee hit-stop (attacker) and block impact (defender), as the source's block-impacts records.
                if((e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&e.Source>0) meleeImpacts.RecordHit(e.Source.ToString(),e.Time);
                else if(e.Kind==EventKind.Block&&e.Source>0) meleeImpacts.RecordBlock(e.Source.ToString(),e.Time);
                if(e.Kind==EventKind.Shot)
                {
                    int weapon=(int)e.Amount;
                    if(SoundEnabled&&weapon>0&&weapon<4&&gunSounds[weapon]!=null)
                    { var audio=shotAudio[nextAudio++%shotAudio.Length];audio.transform.position=Vec(e.Position);audio.clip=gunSounds[weapon];audio.volume=.35f;audio.Play(); }
                    continue;
                }
                if(e.Kind==EventKind.ReloadStart||e.Kind==EventKind.ReloadEnd)
                {
                    if(SoundEnabled&&e.Source==LocalId) localAudio.PlayOneShot(e.Kind==EventKind.ReloadStart?reloadStart:reloadEnd,.5f);
                    continue;
                }
                if(e.Source==LocalId&&(e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&e.Target>0)
                {
                    LastHitUntil=Time.unscaledTime+.25f;LastHitDamage=e.Amount;LastHeadshot=e.Kind==EventKind.Headshot;
                    if(SoundEnabled) localAudio.PlayOneShot(hitSound,.45f);
                }
                bool native=sourceModels!=null;
                if(DebrisEnabled&&(e.Kind==EventKind.Break||e.Kind==EventKind.Death&&!native)) Debris(Vec(e.Position)+Vector3.up*.3f,e.Kind==EventKind.Death?materials[2]:materials[3]);
                // The prototype lab's particle burst, only for the kinds it was written for. Native arenas draw the source's
                // voxel sparks and streaks for these events instead (NativeImpactCues via NativeBlastPresentation). A Block is the
                // block effect's clash or deflect everywhere (NativeBlockFxPresentation), no burst.
                if(NativeEventFeedback.HitBurst(e.Kind,native)) Burst(Vec(e.Position), e.Kind==EventKind.Death?new Color(1,.2f,.35f):new Color(1,.7f,.3f),e.Kind==EventKind.Death?24:12);
            }
            // The block effect: guards on open block windows, clashes and deflects (after the rigs and the view weapon are posed).
            RenderBlockFx(s,renderTime);
            // The melee Q/E kit: its events, the Shadow Step decoy and the dash ghosts (NativeMeleeKitPresentation.cs).
            RenderMeleeKit(s,renderTime);
        }
        private void PrepareCapturedPose(Actor a,Vector3 position,float time)
        {
            CapturedAnimation.Player rig;
            if(!rigs.TryGetValue(a.Id,out rig)) { rig=captured.Create(transform,a.Id); rig.Impacts=meleeImpacts; rig.Hits=hitLedger; rigs.Add(a.Id,rig); }
            if(Viewer&&ViewerSet>=4) rig.PreviewGun((WeaponId)(ViewerSet-3),ViewerClip,ViewerTime);
            else if(Viewer) rig.Preview(ViewerSet,PreviewName.Substring(PreviewName.IndexOf('/')+1),ViewerTime,Vector3.zero);
            else { PoseReach(rig,a,time); rig.ActorPose(a,position,time); }
        }
        private void CapturedPose(Actor a,Vector3 position,float time,int color)
        {
            var rig=rigs[a.Id];if(!ReviveVisible(a.Id,time))return;
            if(a.Id==LocalId) PlayerState=rig.State;
            if(SourceCharacter(rig,a,time))return;
            foreach(var box in CapturedRig.Boxes)
            {
                int tint=time<a.FlashUntil?4:box.Color<0?color:box.Color;
                Part(rig.Bones[box.Bone].localToWorldMatrix,CapturedAnimation.Vec(CapturedRig.Mirror(box.Position)),CapturedAnimation.Vec(box.Size),Quaternion.identity,tint);
            }
            if(GunRules.Ranged(rig.Weapon))
            {
                Actor shown=a;
                if(Viewer)
                {
                    var w=rig.Weapon; shown=new Actor { Weapon=w,LastShot=ViewerClip==2?ViewerTime-CapturedClip.Wrap(ViewerTime,GunRules.Get(w).Interval):-100 };
                    if(ViewerClip==3) { shown.ReloadAt=ViewerTime-CapturedClip.Wrap(ViewerTime,GunRules.Get(w).Reload+.6f); shown.ReloadUntil=shown.ReloadAt+GunRules.Get(w).Reload; }
                }
                GunPresentation.Draw(rig,shown,Viewer?ViewerTime:time,Add); return;
            }
            // The stand-in staff (without the source models; L33t H4x borrows the first school's).
            int school=MagicPose.School(a);
            if(school>0)
            {
                DrawStaff(rig,school==MagicPose.Leet?1:school);
                return;
            }
            if(!a.WeaponAway)Weapon(MeleeHand(rig,a,time),rig.Set,color);
            if(rig.Set==1) Weapon(MeleeHand(rig,a,time,true),1,color);
        }
        private static Matrix4x4 MeleeHand(CapturedAnimation.Player rig,Actor a,float time,bool left=false)
        {return rig.Bones[left?CapturedRig.LHand:CapturedRig.RHand].localToWorldMatrix*Matrix4x4.Rotate(CapturedAnimation.Quat(CapturedRig.Mirror(NativeSpecialPose.HeldRotation(a,time,left))));}
        private void Weapon(Matrix4x4 hand,int set,int color)
        {
            // Source weapon-visuals.ts proportions. Grip is at the wrist; +z is the captured blade direction.
            if(set==3)
            {
                Part(hand,new Vector3(0,.13f,.18f),new Vector3(.60f,.58f,.12f),Quaternion.identity,4);
                Part(hand,new Vector3(0,-.20f,.18f),new Vector3(.46f,.10f,.12f),Quaternion.identity,4);
                Part(hand,new Vector3(0,-.29f,.18f),new Vector3(.28f,.09f,.12f),Quaternion.identity,4);
                Part(hand,new Vector3(0,.13f,.25f),new Vector3(.5f,.48f,.04f),Quaternion.identity,3);
                Part(hand,new Vector3(0,.05f,.28f),new Vector3(.09f,.62f,.025f),Quaternion.identity,color);
                Part(hand,new Vector3(0,.17f,.28f),new Vector3(.36f,.08f,.025f),Quaternion.identity,color);
                return;
            }
            float length=set==1?.5f:set==2?1.35f:.94f,width=set==2?.18f:set==1?.09f:.10f;
            Part(hand,new Vector3(0,0,.02f),new Vector3(.09f,.09f,set==2?.4f:.27f),Quaternion.identity,3);
            Part(hand,new Vector3(0,0,set==2?-.225f:-.15f),Vector3.one*.08f,Quaternion.identity,color);
            Part(hand,new Vector3(0,0,.22f),new Vector3(set==2?.4f:set==1?.2f:.34f,.065f,.08f),Quaternion.identity,color);
            Part(hand,new Vector3(0,0,.26f+(length-.08f)*.5f),new Vector3(width,.04f,length-.08f),Quaternion.identity,4);
            Part(hand,new Vector3(0,0,.26f+length-.04f),new Vector3(width*.66f,.035f,.08f),Quaternion.identity,4);
            Part(hand,new Vector3(0,0,.26f+length+.035f),new Vector3(width*.33f,.035f,.07f),Quaternion.identity,4);
        }
        private void UseCompatibility(string reason)
        {
            ForceCompatibilityRendering=true;
            Debug.LogWarning("Instanced drawing failed; switched characters and projectiles to compatibility rendering: "+reason);
        }
        private void Pose(Actor a,Vector3 position,float time,int color)
        {
            bool roll=a.Rolling(time), attack=a.Attacking(time), guard=time<a.BlockUntil;
            float speed=new Vector2(a.Velocity.x,a.Velocity.z).magnitude;
            float gait=Mathf.Sin(time*(a.Sprinting?17:12)+a.Id*.4f)*Mathf.Clamp01(speed/Rules.Speed);
            float phase=Mathf.Clamp01((time-a.AttackAt)/Rules.SwordDuration);
            float swing=attack?Mathf.Sin(phase*Mathf.PI):0;
            float rollAngle=roll?(time-a.RollAt)/Rules.RollDuration*360:0;
            Quaternion facing=Quaternion.Euler(0,a.Yaw,0);
            if(roll) facing=Quaternion.LookRotation(Vec(a.RollDirection));
            Matrix4x4 root=Matrix4x4.TRS(position+Vector3.up*(roll?.7f:0),facing*Quaternion.Euler(rollAngle,attack?(a.Combo==1?-1:1)*swing*35:0,0),Vector3.one);
            Part(root,new Vector3(0,1.04f,0),new Vector3(.57f,.66f,.34f),Quaternion.Euler(a.Sprinting?14:3,0,-gait*3),color);
            Part(root,new Vector3(0,.66f,0),new Vector3(.46f,.22f,.32f),Quaternion.identity,3);
            Part(root,new Vector3(0,1.58f,0),new Vector3(.43f,.42f,.39f),Quaternion.Euler(-4,0,0),color);
            Part(root,new Vector3(0,1.62f,.205f),new Vector3(.32f,.09f,.02f),Quaternion.identity,4);
            for(int side=-1;side<=1;side+=2)
            {
                float legAngle=roll?-75:gait*side*32;
                Matrix4x4 hip=root*Matrix4x4.TRS(new Vector3(side*.16f,.66f,0),Quaternion.Euler(legAngle,0,0),Vector3.one);
                Part(hip,new Vector3(0,-.17f,0),new Vector3(.22f,.35f,.25f),Quaternion.identity,color);
                Matrix4x4 knee=hip*Matrix4x4.TRS(new Vector3(0,-.33f,0),Quaternion.Euler(Mathf.Max(0,-legAngle)*.8f,0,0),Vector3.one);
                Part(knee,new Vector3(0,-.14f,0),new Vector3(.20f,.30f,.23f),Quaternion.identity,3);
                Part(knee,new Vector3(0,-.29f,.08f),new Vector3(.25f,.14f,.40f),Quaternion.identity,color);
                float armAngle=roll?-90:guard?-75:attack?(side==1?-135+swing*170:-25-swing*45):-gait*side*28;
                Matrix4x4 shoulder=root*Matrix4x4.TRS(new Vector3(side*.4f,1.32f,0),Quaternion.Euler(armAngle,0,side*(attack?15:6)),Vector3.one);
                Part(shoulder,new Vector3(0,-.18f,0),new Vector3(.22f,.36f,.24f),Quaternion.identity,color);
                Matrix4x4 elbow=shoulder*Matrix4x4.TRS(new Vector3(0,-.34f,0),Quaternion.Euler(guard?-75:attack?-35:-15,0,0),Vector3.one);
                Part(elbow,new Vector3(0,-.16f,0),new Vector3(.19f,.32f,.21f),Quaternion.identity,3);
                if(side==1)
                {
                    Part(elbow,new Vector3(0,-.41f,.1f),new Vector3(.35f,.08f,.14f),Quaternion.identity,color);
                    Part(elbow,new Vector3(0,-.98f,.1f),new Vector3(.10f,1.10f,.08f),Quaternion.identity,4);
                }
            }
            if(guard) Part(root,new Vector3(0,1,.58f),new Vector3(.9f,.9f,.055f),Quaternion.identity,0);
        }
        private void Part(Matrix4x4 parent,Vector3 p,Vector3 scale,Quaternion rotation,int color) { Add(color,parent*Matrix4x4.TRS(p,rotation,scale)); }
        private void Add(int color,Matrix4x4 matrix) { batches[color].Add(matrix); DrawnParts++; }
        // The prototype lab's ParticleSystem burst (native arenas draw NativeImpactCues' source sparks instead).
        private void Burst(Vector3 position,Color color,int count)
        {
            particles.Play();
            for(int i=0;i<count;i++)
            {
                var e=new ParticleSystem.EmitParams { position=position, velocity=Random.onUnitSphere*Random.Range(2,6),startColor=color,startSize=Random.Range(.05f,.17f),startLifetime=Random.Range(.3f,.8f) };
                particles.Emit(e,1);
            }
        }
        private void Debris(Vector3 position,Material material)
        {
            debris.RemoveAll(o=>o==null);
            for(int i=0;i<8&&debris.Count<512;i++)
            {
                var o=Block("Temporary PhysX debris",position+Random.insideUnitSphere*.35f,Vector3.one*Random.Range(.18f,.38f),material,true);
                var body=o.AddComponent<Rigidbody>(); body.mass=.5f; body.AddForce((Random.onUnitSphere+Vector3.up)*5,ForceMode.Impulse); body.AddTorque(Random.onUnitSphere*8,ForceMode.Impulse);
                debris.Add(o); Destroy(o,6);
            }
        }
        private void OnDestroy()
        {
            ClearSourceActors();DisposeDevEffects();DisposeEventWorldFx();DisposeStructureFx();DisposeHalos();DisposeBrickfall();sourceModels?.Dispose();combatGlow?.Dispose();blasts?.Dispose();meleeFlash?.Dispose();fieldTelegraphs?.Dispose();shots?.Dispose();vfx?.Dispose();DisposeStatus();viewRig?.Dispose();foreach(var rig in rigs.Values) rig.Dispose(); rigs.Clear(); captured?.Dispose();
            foreach(var m in materials) CapturedAnimation.Release(m);
            foreach(var m in compatibilityMaterials) CapturedAnimation.Release(m);
            if(particleMaterial!=null) CapturedAnimation.Release(particleMaterial);
            DisposeWeaponPresentation();
        }
    }
}
