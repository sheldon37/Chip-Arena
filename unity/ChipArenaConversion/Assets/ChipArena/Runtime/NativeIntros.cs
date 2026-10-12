using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool IntroActive=>match?.Phase==NativePhase.Intro||arena?.Pve?.Holding==true&&arena.Pve.Cinematic=="intro";
        private NativePilot IntroPilot=>match.Phase==NativePhase.Intro?arena.Pilots.Find(p=>p.Seat.id==match.IntroSeat):arena.Pve.Boss;
        // The staged entrance (NativeIntroRules.Entrance): the replicated style, or the pilot's own weapon signature when this
        // client cannot stage it (a retired generic entrance from an older host, an unknown id).
        private string IntroStyle=>match.Phase==NativePhase.Intro?NativeIntroRules.Entrance(match.IntroStyle,IntroPilot?.Seat.spec):"dev-siren";
        private float IntroAge=>match.Phase==NativePhase.Intro?(float)(match.Time-match.IntroStarted):arena.Pve.HoldDuration-arena.Pve.HoldLeft;
        // The age the live entrance is drawn at (NativeIntroClock): smooth at any frame rate on the host and on guests, whose
        // IntroAge steps at 20 Hz, and for dev-siren locked to the boss song as this machine plays it (NativeAudio ran first
        // this frame), so the cell's stutters land on the beats heard. The overlay card and the stings keep IntroAge.
        private readonly NativeIntroClock introClock=new NativeIntroClock();
        private NativeArena introClockArena;private double introClockStarted;private int introClockSeat;
        private float IntroClockAge()
        {
            double started=match.Phase==NativePhase.Intro?match.IntroStarted:arena.BossSongAt;int seat=IntroPilot?.Seat.id??0;
            if(introClockArena!=arena||introClockStarted!=started||introClockSeat!=seat){introClock.Reset();introClockArena=arena;introClockStarted=started;introClockSeat=seat;}
            float age=IntroAge;
            return (float)introClock.Sample(NativeIntroClock.Target(age,nativeAudio.SongPlayback(NativeIntroRules.IntroSong(IntroStyle)),arena.IntroStartsBossSong),Time.unscaledDeltaTime);
        }
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player introRig;private int introActor;private string introSkins;private Color introAccent=Color.white;private Func<int,Matrix4x4> introHand;private Matrix4x4 introGrip=Matrix4x4.identity;
        // This frame's baked entrance stage, read by delegates made once (no closure per frame).
        private NativeSceneClip stageClip;private float stageAge;private string stageStyle,stageKey;private bool[] stageGlow;private readonly Dictionary<string,bool[]> stageGlows=new Dictionary<string,bool[]>();
        private Func<int,Matrix4x4> stageBone;private Func<int,bool> stageShown;private Func<int,float> stageOpacity;private Func<int,Color> stageTint;
        private bool[] stageSoft;private readonly Dictionary<string,(bool[] soft,string model)> stageSofts=new Dictionary<string,(bool[],string)>();
        public void RenderIntro(NativeArena arena,NativePilot pilot,string style,float age,Camera camera)
        {
            if(pilot==null)return;introSkins=pilot.Seat.weaponSkins;introAccent=AccentTint(pilot.Seat.accentColor);style=NativeIntroRules.Entrance(style,pilot.Seat.spec);sirenLookCamera=camera;if(introRig==null||introActor!=pilot.Seat.id){introRig?.Dispose();introRig=captured.Create(transform,-100);introActor=pilot.Seat.id;}
            sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            Actor actor=null;foreach(var a in arena.Snapshot().Actors)if(a.Id==pilot.Seat.id){actor=a;break;}actor??=pilot.Pose;
            var script=NativeEntranceDirector.Script(style);if(script!=null){DirectedIntro(script,arena,pilot,actor,style,age,camera);return;}
            NativeSourceMotion.Intro(introRig,style,age,camera);introBeats=style=="dev-siren"?arena.BossTrack?.beats:null;
            if(style!="dev-siren"){SourceCharacter(introRig,actor,arena.Time,weapon:false,halo:age);IntroWeapon(style,age);TeamBands(introRig,NativeStatusVisuals.PilotBand(arena.Match.Teams,arena.Match.Cooperative,pilot.Seat.AuthorityBoss,pilot.Seat.team,pilot.Seat.id==arena.LocalId));}if(!IntroStage(style,age,camera))throw new System.InvalidOperationException("Missing source entrance stage: "+style);
            DrawBatches(null,0,false);sourceModels.Stage=true;try{sourceModels.Flush(UsesInstancing);}finally{sourceModels.Stage=false;}
        }
        // ---- the directed entrances (Core/NativeEntranceDirector: the weapon signatures and the procedural entrances) ----
        private readonly NativeEntranceLights entranceLights=new NativeEntranceLights();
        private readonly CapturedRig.Pose entrancePose=new CapturedRig.Pose(),entranceGhostPose=new CapturedRig.Pose();
        private readonly NativeEntranceGhost[] entranceGhosts=new NativeEntranceGhost[8];private readonly Matrix4x4[] entranceGhostBones=new Matrix4x4[22];
        private Func<int,Matrix4x4> entranceGhostBone,entranceGhostWeapon;private Func<int,float> entranceReveal;private NativeEntranceScript entranceScript;private float entranceTime;
        private EntranceProps entranceProps;private EntranceFx entranceFx;
        // The entrance's own blast sim (the hit's quake, kinetic, fire, void, nova): stepped at 60 Hz from the entrance's
        // start on its presentation clock and replayed from zero on any backwards jump, so a late join, a scrub or a frame
        // rate change shows exactly what an uninterrupted playback shows; pools have NativeBlastSim's fixed capacity.
        private NativeBlastSim entranceSim;private string entranceSimKey;private int entranceSimSteps,entranceSimNext;
        public const uint EntranceSeed=0x1e7a;
        /// <summary>
        /// Stages a directed entrance: the script's pose on the intro rig, its authored camera, its light rig, the arena set and
        /// props, the pilot (revealed per bone where the script builds it in), afterimages and hologram, the closed-form effects
        /// and the blast sim through the shared effects renderer. Presentation only: the match sees only the style and duration.
        /// </summary>
        private void DirectedIntro(NativeEntranceScript script,NativeArena arena,NativePilot pilot,Actor actor,string style,float age,Camera camera)
        {
            if(script is NativeWeaponEntrance)script.Bind(NativeSourceMotion.EntranceClip(style));if(!script.Ready)throw new InvalidOperationException("Missing source entrance clip: "+style);
            float quality=Mathf.Clamp01(WeaponEffectsQuality),t=script.Clock(age);entranceScript=script;entranceTime=t;
            var sheldon=script as NativeSheldonIntroScript;if(sheldon!=null)actor=SheldonIntroBegin(sheldon,actor,pilot,t);
            script.Pose(t,entrancePose,out var root,out var rotation);
            if(NativePoseWriter.Write(introRig.Bones,entrancePose,"the intro rig")){introRig.Root.transform.SetPositionAndRotation(CapturedAnimation.Vec(CapturedRig.Mirror(root)),CapturedAnimation.Quat(CapturedRig.Mirror(rotation)));introRig.Root.transform.localScale=Vector3.one;}
            script.Camera(age,out var view);var eye=CapturedAnimation.Vec(CapturedRig.Mirror(view.Eye));var forward=CapturedAnimation.Vec(CapturedRig.Mirror(view.Look-view.Eye));
            if(forward.sqrMagnitude>1e-8f)camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(forward,CapturedAnimation.Vec(CapturedRig.Mirror(view.Up))));camera.fieldOfView=view.Fov;
            EntranceBlasts(script,style+":"+pilot.Seat.id+":"+Mathf.RoundToInt(quality*10),t,quality);
            script.Lights(t,entranceLights);
            foreach(var f in entranceSim.Flashes)if(f.Intensity>0){NativeBlastSim.Linear(f.Color,out double fr,out double fg,out double fb);entranceLights.Add(new System.Numerics.Vector3(-(float)f.X,(float)f.Y,(float)f.Z),new System.Numerics.Vector3((float)(fr*f.Intensity),(float)(fg*f.Intensity),(float)(fb*f.Intensity)),(float)f.Distance);}
            NativeLighting.Entrance(entranceLights,camera);
            // The pilot, its weapon, its team band; afterimages and the hologram as one-layer see-through silhouettes.
            entranceReveal??=b=>entranceScript.Solid(entranceTime,b);bool solid=script.Solid(t,CapturedRig.Chest)>.5f;
            try{sheldonScripted=sheldon!=null;SourceCharacter(introRig,actor,arena.Time,weapon:false,halo:age,reveal:entranceReveal);}finally{sheldonScripted=false;}if(solid||script.Solid(t,CapturedRig.RHand)>.5f)IntroWeapon(style,age);
            if(solid)TeamBands(introRig,NativeStatusVisuals.PilotBand(arena.Match.Teams,arena.Match.Cooperative,pilot.Seat.AuthorityBoss,pilot.Seat.team,pilot.Seat.id==arena.LocalId));
            EntranceGhosts(script,style,t);
            entranceProps??=new EntranceProps(sourceModels);script.Props(t,entranceProps,quality);
            if(vfx!=null){vfx.Low=LowGraphics;vfx.Begin();entranceFx??=new EntranceFx(vfx);script.Effects(t,entranceFx,quality);vfx.Blasts(entranceSim);}
            DrawBatches(null,0,false);sourceModels.Stage=true;try{sourceModels.Flush(UsesInstancing);}finally{sourceModels.Stage=false;}
            vfx?.Submit(camera);
        }
        private void EntranceBlasts(NativeEntranceScript script,string key,float t,float quality)
        {
            entranceSim??=new NativeBlastSim(new NativeBlastRandom(EntranceSeed).Next);int target=Mathf.Max(0,Mathf.FloorToInt(t*60+1e-4f));
            if(key!=entranceSimKey||target<entranceSimSteps){entranceSim.Reset(new NativeBlastRandom(EntranceSeed).Next);entranceSimKey=key;entranceSimSteps=0;entranceSimNext=0;}
            var blasts=script.Blasts;const double dt=1/60.0;
            for(;entranceSimSteps<target;entranceSimSteps++)
            {
                double from=entranceSimSteps*dt;
                while(entranceSimNext<blasts.Length&&blasts[entranceSimNext].At<from+dt)
                {
                    var b=blasts[entranceSimNext++];var p=b.Position;entranceSim.Solid.SizeScale=entranceSim.Glow.SizeScale=b.Size>0?b.Size:1;
                    if(b.Impact)entranceSim.Impact(-p.X,p.Y,p.Z,b.Radius,b.ImpactColor,quality,false,false,null,NativeEntranceStage.FloorY);
                    else entranceSim.Blast(b.Style,-p.X,p.Y,p.Z,b.Radius,quality,false,b.Tint,NativeEntranceStage.FloorY);
                    entranceSim.Solid.SizeScale=entranceSim.Glow.SizeScale=1;
                }
                entranceSim.Update(dt);
            }
        }
        private void EntranceGhosts(NativeEntranceScript script,string style,float t)
        {
            // The entrance's afterimages and hologram are the pilot's own body (the Sheldon Intro's: its Sheldon Clothes).
            string model=sourceModels.EntranceGhost(BodyModel(script is NativeSheldonIntroScript?NativeBodySkins.SheldonIndex:BodySkin(introActor),BodyCloseTier));if(model==null)return;string weapon=IntroModel(NativeIntroRules.IntroWeapon(style));string weaponGhost=weapon!=null?sourceModels.EntranceGhost(weapon):null;
            entranceGhostBone??=b=>b>=0&&b<22?entranceGhostBones[b]:Matrix4x4.identity;entranceGhostWeapon??=b=>(b>=0&&b<22?entranceGhostBones[b]:Matrix4x4.identity)*introGrip;
            int n=script.Ghosts(t,entranceGhosts);bool holo=script.Holo(t,out var hc,out float ho,out var hoff);
            for(int i=0;i<n+(holo?1:0);i++)
            {
                bool isHolo=i==n;var g=isHolo?new NativeEntranceGhost{Lag=0,Opacity=ho,Color=hc,Offset=hoff}:entranceGhosts[i];if(g.Opacity<=.003f)continue;
                script.Pose(t-g.Lag,entranceGhostPose,out var root,out var rotation);entranceGhostPose.World();
                var rootM=Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(root+g.Offset)),CapturedAnimation.Quat(CapturedRig.Mirror(rotation)),Vector3.one);
                for(int b=0;b<22;b++)entranceGhostBones[b]=rootM*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(entranceGhostPose.WorldPosition[b])),CapturedAnimation.Quat(CapturedRig.Mirror(entranceGhostPose.WorldRotation[b])),Vector3.one);
                var tint=new Color(g.Color.X,g.Color.Y,g.Color.Z);var scan=new Color(isHolo?1:0,t,0,0);
                sourceModels.Queue(model,rootM,entranceGhostBone,null,0,null,tint,scan,g.Opacity);
                if(weaponGhost!=null&&!isHolo){introGrip=Matrix4x4.Translate(CapturedAnimation.Vec(CapturedRig.Mirror(NativeIntroRules.IntroGrip(style))));sourceModels.Queue(weaponGhost,rootM,entranceGhostWeapon,null,0,null,tint,scan,g.Opacity);}
            }
        }
        /// <summary>A directed entrance's set pieces into the source-model piece batches (stage lit; mirrored into Unity's frame).</summary>
        private sealed class EntranceProps:INativeEntranceProps
        {
            private readonly NativeSourceModels models;public EntranceProps(NativeSourceModels models){this.models=models;}
            public void Piece(WeaponShape shape,bool glow,System.Numerics.Vector3 position,System.Numerics.Quaternion rotation,System.Numerics.Vector3 scale,System.Numerics.Vector3 color)
            {
                var m=Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(position)),CapturedAnimation.Quat(CapturedRig.Mirror(rotation)),CapturedAnimation.Vec(scale));
                models.QueuePiece(shape,glow,m,new Color(color.X,color.Y,color.Z,1));
            }
        }
        /// <summary>A directed entrance's effects into the shared effects renderer (mirrored into Unity's frame).</summary>
        private sealed class EntranceFx:INativeEntranceFx
        {
            private readonly NativeVfxDraw vfx;public EntranceFx(NativeVfxDraw vfx){this.vfx=vfx;}
            private static Vector3 U(System.Numerics.Vector3 v)=>new Vector3(-v.X,v.Y,v.Z);
            public void Glow(System.Numerics.Vector3 at,System.Numerics.Vector3 color,float coverage,float size,int profile,float u,float seed)=>vfx.Glow(U(at),new Color(color.X,color.Y,color.Z,coverage),size,profile,u,seed);
            public void Streak(System.Numerics.Vector3 head,System.Numerics.Vector3 velocity,System.Numerics.Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>vfx.Streak(U(head),U(velocity),new Color(color.X,color.Y,color.Z,0),width,minLength,blur,taper,u,seed);
            public void Smoke(System.Numerics.Vector3 at,System.Numerics.Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>vfx.Smoke(U(at),new Color(albedo.X,albedo.Y,albedo.Z,0),opacity,size,u,seed,floor);
        }
        // player-intros.ts: the bare intro rig holds only a weapon intro's own weapon (WEAPON_INTROS[style].spec) or the Emperor's
        // gauntlets, with no chip dressing; every other entrance keeps its hands empty, so the L33T Throne's floating staff (a
        // stage prop, grabbed at 4 s) is the only staff. The model rides the wrist joints it was exported on (staffs: the left
        // hand, where poseWeaponIntro moves the weapon) at NativeIntroRules.IntroGrip; the baked intro clips pose both arms from
        // the armed rig (tools/export_motion.mjs), so the braced weapons keep the free hand on their support grip.
        // The entrance weapon in the pilot's own skin for that weapon (Core/NativeWeaponSkins.cs).
        private string IntroModel(string id){if(id==null||sourceModels==null)return id;var skinned=NativeWeaponSkins.ModelIn(id,introSkins);return sourceModels.Has(skinned)?skinned:id;}
        private void IntroWeapon(string style,float age)
        {
            string model=IntroModel(NativeIntroRules.IntroWeapon(style));if(model==null)return;
            introGrip=Matrix4x4.Translate(CapturedAnimation.Vec(CapturedRig.Mirror(NativeIntroRules.IntroGrip(style))));
            if(introHand==null)introHand=b=>introRig.Bones[b].localToWorldMatrix*introGrip;
            sourceModels.Queue(model,introRig.Root.transform.localToWorldMatrix,introHand,accent:introAccent);
            if(NativeIntroRules.IntroMuzzleFlash(style,age,out var flash))QueuePiece(flash,introHand(CapturedRig.RHand),0,null);
        }
        // The baked entrance stage (tools/export_cinematics.mjs, the rig excluded) with its lights. ChipBlasts' glow voxels are
        // additive in the source and fade by dimming; drawn alpha-blended as baked they turned into opaque dark cubes once spent,
        // so their tracks fade by alpha instead (NativeIntroRules.GlowAlpha).
        private bool IntroStage(string style,float age,Camera camera)
        {
            if(style!=stageStyle){stageStyle=style;stageKey="stage-"+style;}string key=stageKey;var clip=NativeSceneMotion.Get(key);if(clip==null)return false;NativeLighting.Intro(clip,age,camera,introBeats);SirenCell(clip,key,age,camera);
            if(!stageGlows.TryGetValue(key,out var glow))
            {
                glow=new bool[clip.tracks];for(int t=0;t<clip.tracks;t++)glow[t]=NativeIntroRules.TintVaries(clip.values,clip.frames,t)&&sourceModels.TransparentBone(key,t);
                stageGlows.Add(key,glow);
            }
            stageClip=clip;stageAge=age;stageGlow=glow;
            if(stageBone==null)
            {
                stageBone=b=>NativeSceneMotion.Matrix(stageClip,b,stageAge);stageShown=b=>StageOpacity(b)>.001f;stageOpacity=StageOpacity;
                stageTint=b=>{var c=NativeSceneMotion.Tint(stageClip,b,stageAge);if(b>=0&&b<stageGlow.Length&&stageGlow[b])NativeIntroRules.GlowAlpha(ref c.r,ref c.g,ref c.b);if(b==sirenBulb&&introBeats!=null){c.r*=sirenGlow;c.g*=sirenGlow;c.b*=sirenGlow;}return c;};
            }
            sourceModels.Queue(SoftStage(style,key,clip),Matrix4x4.identity,stageBone,stageShown,partOpacity:stageOpacity,partTint:stageTint);
            // The dev-siren face (its stare and the acted smile) rides the bake's head track (NativeSirenFace.cs).
            if(style=="dev-siren")SirenEntranceFace(clip,age,camera);return true;
        }
        private float StageOpacity(int b)
        {
            float opacity=NativeSceneMotion.Opacity(stageClip,b,stageAge);
            if(stageSoft!=null&&b>=0&&b<stageSoft.Length&&stageSoft[b]){var s=NativeSceneMotion.Tint(stageClip,b,stageAge);return opacity*NativeIntroRules.SoftVoxelAlpha(s.r,s.g,s.b);}
            if(b<0||b>=stageGlow.Length||!stageGlow[b])return opacity;
            var c=NativeSceneMotion.Tint(stageClip,b,stageAge);return opacity*NativeIntroRules.GlowAlpha(ref c.r,ref c.g,ref c.b);
        }
        // The gun intros' barrel smoke and dust (ChipBlasts' opaque voxel pool, MeshBasicMaterial in the source) draw unlit and
        // alpha-blended and thin out as they darken (NativeIntroRules.SoftVoxelAlpha) instead of hanging as hard, shaded grey
        // cubes; the stage model variant swaps only those tracks' opaque parts to an unlit alpha-blended material (made once
        // per stage, nothing per frame).
        private string SoftStage(string style,string key,NativeSceneClip clip)
        {
            stageSoft=null;if(!NativeIntroRules.SoftSmoke(style))return key;
            if(!stageSofts.TryGetValue(key,out var soft))
            {
                var tracks=new bool[clip.tracks];for(int t=0;t<clip.tracks;t++)tracks[t]=NativeIntroRules.TintVaries(clip.values,clip.frames,t)&&!sourceModels.TransparentBone(key,t);
                soft=(tracks,sourceModels.Blended(key,tracks));stageSofts.Add(key,soft);
            }
            stageSoft=soft.soft;return soft.model;
        }
    }
    public sealed partial class NativeSourceModels
    {
        private Material entranceGhostMaterial;private bool entranceGhostFailed;
        /// <summary>
        /// Model `id` drawn as a directed entrance's one-layer see-through silhouette (Resources/NativeEntranceGhost.shader:
        /// body tint = linear colour and opacity, accent x = hologram scanlines, y = clock); the Demon Step ghost (Ghost) if the
        /// shader is missing or unsupported; null without the model.
        /// </summary>
        public string EntranceGhost(string model)
        {
            if(string.IsNullOrEmpty(model))return null;string id="entrance-ghost-"+model;if(models.ContainsKey(id))return id;if(!models.TryGetValue(model,out var source))return null;
            if(entranceGhostMaterial==null)
            {
                if(entranceGhostFailed)return Ghost(model);var shader=Resources.Load<Shader>("NativeEntranceGhost");if(shader==null||!shader.isSupported){entranceGhostFailed=true;return Ghost(model);}
                entranceGhostMaterial=new Material(shader){enableInstancing=true,name="Native entrance ghost"};materials.Add(entranceGhostMaterial);
            }
            var parts=new Part[source.Length];for(int i=0;i<parts.Length;i++)parts[i]=new Part{Name=source[i].Name,Bone=source[i].Bone,Mesh=source[i].Mesh,Material=entranceGhostMaterial,NoShadow=true};
            models[id]=parts;return id;
        }
        /// <summary>True when model `id` draws a transparent (alpha-blended) section on joint or track `bone`.</summary>
        internal bool TransparentBone(string id,int bone)
        {
            if(!models.TryGetValue(id,out var parts))return false;
            foreach(var part in parts)if(part.Bone==bone&&part.Material!=null&&part.Material.renderQueue>=(int)UnityEngine.Rendering.RenderQueue.Transparent)return true;return false;
        }
        /// <summary>
        /// Model `id` with the opaque parts on the joints or tracks flagged in `bones` drawn with the alpha-blended source
        /// material (NativeSourceTransparent, which takes the part opacity as alpha) set unlit (`_Unlit`: the flat colour of
        /// three's MeshBasicMaterial, vertex colour x tint, with no light term); the id of that variant ("id~blend").
        /// </summary>
        internal string Blended(string id,bool[] bones)
        {
            string variant=id+"~blend";if(models.ContainsKey(variant))return variant;if(!models.TryGetValue(id,out var parts))return id;Material blended=null;var copy=new Part[parts.Length];
            for(int i=0;i<parts.Length;i++)
            {
                var p=parts[i];bool swap=p.Bone>=0&&p.Bone<bones.Length&&bones[p.Bone]&&p.Material!=null&&p.Material.renderQueue<(int)UnityEngine.Rendering.RenderQueue.Transparent;
                if(swap&&blended==null){var shader=Resources.Load<Shader>("NativeSourceTransparent");if(shader==null)return id;blended=new Material(shader){enableInstancing=true};blended.SetInt("_Cull",2);blended.SetFloat("_Unlit",1);materials.Add(blended);}
                copy[i]=swap?new Part{Name=p.Name,Bone=p.Bone,Mesh=p.Mesh,Material=blended,NoShadow=true}:p;
            }
            models[variant]=copy;return variant;
        }
    }
}
