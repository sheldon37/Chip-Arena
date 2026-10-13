using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;

namespace ChipArena.UnityConversion
{
    // The DEV Cyber Lord in the arena (Claude's presentation; Astra builds the rules, docs/NEW-KITS.md section 3 and
    // docs/CYBER-LORD.md). Every frame reads the replicated actors through CyberLordFeed (the one field mapping) into views:
    //  - BeginCyberLord (start of Render): the views, the cyber events, the look's form times;
    //  - CyberPoseTime / CyberVictimPose (PrepareCapturedPose): Kernel Panic's participants on their hit-stopped clock, and its
    //    victim's tumble layered over the victim's own route (Core/CyberLordMotion.VictimPose);
    //  - CyberLordBody / CyberReveal (SourceCharacter): the look on the rig's joints in the pilot's skin (its "cyberlord" pick in
    //    the seat's skin map: Standard the glitch aura round the hands, Core/NativeCyberLordAura; the Prototype Series the
    //    hardlight gauntlets and fist shells; both with the seams, crest and shimmer, Core/NativeCyberLordLook.PiecesIn) under
    //    the idle stance's scanline band (Core/CyberLordIdle.cs), and the Data Dash's dithered body;
    //  - CyberLordFx (RenderGlow, before the weapons drawer is submitted): the effects (Core/NativeCyberLordFx), the glitched
    //    pilots' RGB-split ghosts and pixel blocks, the idle stance's split shimmer, and the fork-bomb clones posed on rigs of
    //    their own.
    // Host, guests, spectators and the killcam replay draw the same frame from the same data. Effects quality (Reduced) and the
    // low graphics tier thin it as the other effects do. Presentation only: nothing here reaches the authority, the network
    // frame, hit timing, hitboxes, damage or movement.
    public sealed partial class ArenaPresentation
    {
        /// <summary>Settings camera shake (Kernel Panic's camera shakes only with it on).</summary>
        public bool CameraShake;
        private readonly NativeCyberLordFx cyberFx=new NativeCyberLordFx();
        private readonly Dictionary<int,CyberLordView> cyberViews=new Dictionary<int,CyberLordView>();
        private readonly Dictionary<int,float> cyberFormAt=new Dictionary<int,float>();
        private readonly List<int> cyberStale=new List<int>();
        private Snapshot cyberSnapshot;private float cyberClock;
        private readonly CyberPiece[] cyberPieces=new CyberPiece[NativeCyberLordLook.Capacity];
        private float cyberSolid=1;private Func<int,float> cyberReveal;
        private Func<CapturedRig.Pose,bool> cyberVictimLayer;private CyberLordView cyberVictimCaster;private int cyberVictimId;private float cyberVictimTime;
        private readonly CyberLordMotion cyberVictimMotion=new CyberLordMotion();
        private CyberScene cyberScene;private CyberSink cyberSink;
        private readonly CapturedAnimation.Player[] cyberCloneRigs=new CapturedAnimation.Player[3];private readonly Actor[] cyberCloneActors={new Actor(),new Actor(),new Actor()};
        private readonly GhostPose[] cyberGhostPoses={new GhostPose(),new GhostPose(),new GhostPose(),new GhostPose(),new GhostPose(),new GhostPose()};
        // The idle stance's RGB-split shimmer: two ghosts each for up to four glitching pilots at once.
        private readonly GhostPose[] cyberIdleGhosts={new GhostPose(),new GhostPose(),new GhostPose(),new GhostPose(),new GhostPose(),new GhostPose(),new GhostPose(),new GhostPose()};
        /// <summary>The idle split ghosts' strength at a full glitch step (the stun's glitched ghosts reach about .42).</summary>
        private const float CyberIdleGhostOpacity=.3f;

        /// <summary>Start of Render: this frame's Cyber Lord views (CyberLordFeed), the cyber events, the look's form times.</summary>
        private void BeginCyberLord(Snapshot s,float time)
        {
            cyberSnapshot=s;cyberClock=time;CyberRangedStep(s,time);if(s?.Actors==null)return;cyberFx.Observe(s.Events,time);
            foreach(var a in s.Actors)
            {
                if(!CyberLordFeed.Handles(a))continue;if(!cyberViews.TryGetValue(a.Id,out var v)){v=new CyberLordView();cyberViews.Add(a.Id,v);}CyberLordFeed.Read(a,v);
                // The look forms from light when the pilot is first seen alive (the aura ignites, the gauntlets build; a respawn
                // re-forms it; a rewound killcam starts formed).
                if(!a.Alive)cyberFormAt.Remove(a.Id);else if(!cyberFormAt.TryGetValue(a.Id,out float at)||time<at-.25f)cyberFormAt[a.Id]=cyberFormAt.ContainsKey(a.Id)?time-10:time;
            }
            cyberStale.Clear();foreach(var id in cyberViews.Keys){bool live=false;foreach(var a in s.Actors)if(a!=null&&a.Id==id&&CyberLordFeed.Handles(a)){live=true;break;}if(!live)cyberStale.Add(id);}
            foreach(int id in cyberStale){cyberViews.Remove(id);cyberFormAt.Remove(id);}
        }
        private void ClearCyberLord()
        {
            cyberFx.Clear();cyberViews.Clear();cyberFormAt.Clear();cyberSnapshot=null;cyberRanged.Clear();cyberRangedStart=-100;
            for(int i=0;i<cyberCloneRigs.Length;i++){cyberCloneRigs[i]?.Dispose();cyberCloneRigs[i]=null;}
        }
        /// <summary>The Dev Sandbox's "Preview Cyber Double Jump" (developer sandbox only): the local Cyber Lord's own jumps show the
        /// second jump's boost (pose and effect) as if the authority had granted the air jump (Core/CyberLordRun.cs
        /// CyberLordAirBoost.PreviewAt; no impulse). Off everywhere else; a granted air jump always wins.</summary>
        public bool CyberBoostPreviewOn;
        private void CyberBoostPreview(CapturedAnimation.Player rig,Actor a,float time)
        {
            float at=CyberBoostPreviewOn&&a.Id==LocalId&&CyberLordFeed.Handles(a)?CyberLordAirBoost.PreviewAt(rig.JumpTrack,time):-100;
            rig.PreviewAirJumpAt=at;if(a.Id==LocalId)cyberFx.PreviewAirJump(a.Id,at);
        }
        /// <summary>The live Kernel Panic sequence `a` is in (caster or victim), or null.</summary>
        private CyberLordView CyberSequenceOf(int id,float time)
        {
            foreach(var v in cyberViews.Values)if((v.Id==id||v.PanicVictim==id)&&(v.PanicLive(time)||v.PanicStart>-90&&time>=v.PanicStart&&time<v.PanicStart+KernelPanicPath.End+.4f))return v;
            return null;
        }
        /// <summary>The time a rig is posed at: Kernel Panic's participants, seen by one of them, on the hit-stopped clock.</summary>
        private float CyberPoseTime(Actor a,float time)
        {
            if(cyberViews.Count==0||a==null)return time;var v=CyberSequenceOf(a.Id,time);
            return v!=null&&KernelPanicFeel.Participant(v,LocalId)?KernelPanicFeel.Clock(v,time,WeaponEffectsQuality<.5f):time;
        }
        /// <summary>After a rig is posed: Kernel Panic's victim tumbles through the caster's sequence, whatever its own route.</summary>
        private void CyberVictimPose(CapturedAnimation.Player rig,Actor a,float time)
        {
            if(cyberViews.Count==0||a==null||rig==null)return;CyberLordView caster=null;foreach(var v in cyberViews.Values)if(v.PanicVictim==a.Id&&v.Id!=a.Id){caster=v;break;}if(caster==null)return;
            cyberVictimCaster=caster;cyberVictimId=a.Id;cyberVictimTime=KernelPanicFeel.Participant(caster,LocalId)?KernelPanicFeel.Clock(caster,time,WeaponEffectsQuality<.5f):time;
            cyberVictimLayer??=pose=>cyberVictimMotion.VictimPose(pose,cyberVictimCaster,cyberVictimId,cyberVictimTime);rig.Overlay(cyberVictimLayer);
        }
        /// <summary>SourceCharacter: the Data Dash's (and the fork bomb's) dithered body, unless the caller already reveals parts.</summary>
        private Func<int,float> CyberReveal(Actor a,float time,Func<int,float> reveal)
        {
            if(reveal!=null||!CyberLordFeed.Handles(a)||!cyberViews.TryGetValue(a.Id,out var v))return reveal;
            float s=NativeCyberLordLook.BodySolid(v,time);if(s>=.999f)return null;cyberSolid=Mathf.Max(0,s);return cyberReveal??=b=>cyberSolid;
        }
        /// <summary>The Cyber Lord skin a rig shows: the entrance rig its pilot's seat map, every other rig its pilot's seat
        /// (live, guest replica or killcam replay; the viewer rigs the Locker's map). Standard (the aura) when absent.</summary>
        private int CyberSkin(CapturedAnimation.Player rig,int id)=>NativeCyberLordAura.Skin(rig!=null&&rig==introRig?introSkins:SeatWeaponSkins(id));
        /// <summary>SourceCharacter: the hardlight look on a Cyber Lord's joints (arena, first-person view model, entrance rigs).</summary>
        private void CyberLordBody(CapturedAnimation.Player rig,Actor a,float time,bool first,int layer,Camera camera)
        {
            if(sourceModels==null||!CyberLordFeed.Handles(a)||rig?.Bones==null)return;
            float solid=cyberViews.TryGetValue(a.Id,out var v)?NativeCyberLordLook.BodySolid(v,time):1,formAt=cyberFormAt.TryGetValue(a.Id,out float f)?f:-100;var flare=cyberFx.FlareOf(a.Id,time);
            // The Hardlight Boot entrance forms and flares the look on its own clock (Core/NativeCyberLordIntro.cs).
            // The pilot's skin picks what forms there: the gauntlets build, or the aura ignites with its haze (the entrance's effects).
            if(rig==introRig&&entranceScript is NativeCyberLordIntroScript intro){time=entranceTime;formAt=NativeCyberLordIntroScript.FormAt;flare=intro.Flare(time);solid=1;intro.Aura=CyberSkin(rig,a.Id)!=NativeWeaponSkins.PrototypeIndex;}
            // The idle stance's scanline band over the hardlight parts and its fist clench (Core/CyberLordIdle.cs; none in first person or the entrance).
            var glitch=!first&&rig!=introRig&&rig.CyberMotion!=null?rig.CyberMotion.IdleGlitch:default;bool reduced=WeaponEffectsQuality<.5f;flare=CyberLordIdlePose.Clench(glitch,flare);
            // The ranged mode's emitters build over the fists (the aura condenses into them) through the key-5 switch
            // (Core/CyberLordRanged.cs; not in the entrance); a guard packs the aura's shimmer denser.
            var mode=v!=null&&rig!=introRig?NativeCyberLordLook.ModeOf(v,time):default;float guard=rig!=introRig?NativeCyberLordAura.Guard(time,a.BlockAt,a.BlockUntil):0;
            int n=NativeCyberLordLook.PiecesIn(CyberSkin(rig,a.Id),a.Id,time,formAt,flare,first,solid,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics,cyberPieces,mode,guard);
            for(int i=0;i<n;i++)QueueCyberPiece(cyberPieces[i],rig.Bones[cyberPieces[i].Bone].localToWorldMatrix,CyberLordIdlePose.ScanGain(glitch,cyberPieces[i],i,reduced),layer,camera);
        }
        private void QueueCyberPiece(in CyberPiece p,Matrix4x4 bone,float gain,int layer,Camera camera)
        {
            var m=bone*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(p.Position)),CapturedAnimation.Quat(CapturedRig.Mirror(p.Rotation)),CapturedAnimation.Vec(p.Scale));
            // The aura's data strands rise straight up in the world (at the rig's scale: the view model's too).
            if(p.Rise!=0)m.m13+=p.Rise*((Vector3)bone.GetColumn(1)).magnitude;
            sourceModels.QueuePiece(p.Shape,true,m,new Color(p.Color.X*gain,p.Color.Y*gain,p.Color.Z*gain,1),layer,camera);
        }
        /// <summary>RenderGlow, before the weapons drawer is submitted: every Cyber Lord effect, the glitched ghosts and the clones.</summary>
        private void CyberLordFx(NativeArena arena,Camera camera,float now)
        {
            var s=cyberSnapshot;if(s?.Actors==null||vfx==null||sourceModels==null||cyberViews.Count==0&&cyberFx.Records.Count==0)return;var draw=WeaponFxDraw;if(draw==null)return;
            bool viewOk=FirstPerson&&!ScopeActive&&ViewCamera!=null&&viewRig!=null;float q=Mathf.Clamp01(WeaponEffectsQuality);
            cyberArena=arena;cyberScene??=new CyberScene(this);cyberSink??=new CyberSink(this);cyberScene.Bind(s,camera,arena,cyberRanged);cyberSink.Bind(vfx,draw,viewOk);
            cyberFx.Draw(cyberSink,cyberScene,cyberClock,q,LowGraphics);
            CyberAuraHaze(s,cyberClock,q,viewOk);
            CyberGhosts(s,cyberClock,q);
        }
        // The Standard skin's aura haze (Core/NativeCyberLordAura.Halo): soft glows round each Cyber Lord's hands in that skin, the
        // first-person pilot's own on the view layer; nothing for the Prototype Series, a hidden or unbuilt body.
        private static readonly Vector3 AuraFist=CapturedAnimation.Vec(CapturedRig.Mirror(NativeCyberLordAura.FistCentre)),AuraArm=CapturedAnimation.Vec(CapturedRig.Mirror(NativeCyberLordAura.ArmPoint)),
            AuraEmitter=CapturedAnimation.Vec(CapturedRig.Mirror(NativeCyberLordLook.EmitterAt));
        private static NVector AuraN(Vector3 v)=>new NVector(v.x,v.y,v.z);
        private void CyberAuraHaze(Snapshot s,float time,float q,bool viewOk)
        {
            foreach(var a in s.Actors)
            {
                if(!CyberLordFeed.Handles(a)||!a.Alive||!cyberViews.TryGetValue(a.Id,out var v)||CyberSkin(null,a.Id)==NativeWeaponSkins.PrototypeIndex)continue;
                bool me=a.Id==LocalId&&FirstPerson;if(me&&!viewOk)continue;
                CapturedAnimation.Player rig=me?viewRig:rigs.TryGetValue(a.Id,out var r)?r:null;if(rig?.Bones==null||!SpawnShowsFor(a.Id,CapturedRig.RHand))continue;
                Transform lh=rig.Bones[CapturedRig.LHand],rh=rig.Bones[CapturedRig.RHand],le=rig.Bones[CapturedRig.LElbow],re=rig.Bones[CapturedRig.RElbow];
                var at=new NativeCyberLordAura.HandPoints{LFist=AuraN(lh.TransformPoint(AuraFist)),RFist=AuraN(rh.TransformPoint(AuraFist)),LArm=AuraN(le.TransformPoint(AuraArm)),RArm=AuraN(re.TransformPoint(AuraArm)),
                    LEmitter=AuraN(lh.TransformPoint(AuraEmitter)),REmitter=AuraN(rh.TransformPoint(AuraEmitter)),Right=AuraN(me&&ViewCamera!=null?ViewCamera.transform.right:rig.Root.transform.right),Scale=lh.lossyScale.y};
                var glitch=!me&&rig.CyberMotion!=null?rig.CyberMotion.IdleGlitch:default;float formAt=cyberFormAt.TryGetValue(a.Id,out float f)?f:-100;
                NativeCyberLordAura.Halo(cyberSink,me,at,a.Id,time,formAt,CyberLordIdlePose.Clench(glitch,cyberFx.FlareOf(a.Id,time)),NativeCyberLordLook.BodySolid(v,time),q,LowGraphics,NativeCyberLordLook.ModeOf(v,time),NativeCyberLordAura.Guard(time,a.BlockAt,a.BlockUntil));
            }
        }
        // The glitched pilots' RGB-split silhouettes (their posed rig, offset) with pixel blocks, and the fork-bomb clones (a
        // stand-in actor posed on a clone rig at the strike), as see-through weapon ghosts of the pilot model.
        private void CyberGhosts(Snapshot s,float time,float q)
        {
            string pilot=Ghost("pilot");if(pilot==null)return;int pose=0;
            CyberIdleGhosts(s,pilot);
            for(int i=0;i<cyberFx.Ghosts;i++)
            {
                var g=cyberFx.Ghost(i);var tint=new Color(g.Tint.X,g.Tint.Y,g.Tint.Z,0);
                if(g.Kind==CyberGhost.Split)
                {
                    if(!rigs.TryGetValue(g.Actor,out var rig)||rig?.Bones==null||pose>=cyberGhostPoses.Length)continue;var gp=cyberGhostPoses[pose++];var off=Matrix4x4.Translate(new Vector3(g.Offset.X,g.Offset.Y,g.Offset.Z));
                    gp.Root=off*rig.Root.transform.localToWorldMatrix;for(int b=0;b<22&&b<rig.Bones.Length;b++)gp.Bones[b]=off*rig.Bones[b].localToWorldMatrix;
                    // The glitched pilot in its own body skin (NativeBodySkinPresentation.cs; the Prototype ghost only as a fallback).
                    sourceModels.Queue(Ghost(BodyModel(BodySkin(g.Actor),BodyTier(rig.Root.transform.position,false)))??pilot,gp.Root,gp.Bone,null,0,null,tint,null,g.Opacity);
                    if(i%2==0){var c=rig.Bones[CapturedRig.Chest].position+Vector3.up*.1f;cyberFx.GlitchBlocks(cyberSink,g.Actor,new NVector(c.x,c.y,c.z),g.Opacity/.42f,time,q,LowGraphics);}
                }
                else if(g.Kind==CyberGhost.Clone&&g.Index>=0&&g.Index<cyberCloneRigs.Length)
                {
                    Actor caster=null;foreach(var a in s.Actors)if(a!=null&&a.Id==g.Actor){caster=a;break;}
                    var clone=cyberCloneActors[g.Index];NativeCyberLordFx.CloneActor(clone,caster,g);var rig=cyberCloneRigs[g.Index]??=captured.Create(transform,-820-g.Index);float at=NativeCyberLordFx.CloneTime(g);
                    rig.ActorPose(clone,new Vector3(g.Position.X,g.Position.Y,g.Position.Z),at);var root=rig.Root.transform.localToWorldMatrix;
                    if(pose>=cyberGhostPoses.Length)continue;var gp=cyberGhostPoses[pose++];gp.Root=root;for(int b=0;b<22&&b<rig.Bones.Length;b++)gp.Bones[b]=rig.Bones[b].localToWorldMatrix;
                    sourceModels.Queue(Ghost(BodyModel(BodySkin(g.Actor),BodyTier(rig.Root.transform.position,false)))??pilot,gp.Root,gp.Bone,null,0,null,tint,null,g.Opacity);
                    var flare=new NativeCyberLordLook.Flare{Left=1,Right=1};int n=NativeCyberLordLook.PiecesIn(CyberSkin(null,g.Actor),g.Actor*3+g.Index,at,-100,flare,false,g.Opacity,.3f,true,cyberPieces,default);
                    for(int k=0;k<n;k++)QueueCyberPiece(cyberPieces[k],rig.Bones[cyberPieces[k].Bone].localToWorldMatrix,1,0,null);
                }
            }
        }

        // The idle stance's RGB-split shimmer (Core/CyberLordIdlePose.Glitch): a magenta and a cyan ghost of the posed rig (in its
        // body skin), a few centimetres apart across it, for the glitch's steps; never on the local pilot's own body in first person.
        private void CyberIdleGhosts(Snapshot s,string pilot)
        {
            int used=0;var magenta=NativeCyberLordFx.Lin(NativeCyberLordFx.Magenta);var cyan=NativeCyberLordFx.Lin(NativeCyberLordFx.Cyan);
            foreach(var a in s.Actors)
            {
                if(!CyberLordFeed.Handles(a)||FirstPerson&&a.Id==LocalId||used+2>cyberIdleGhosts.Length)continue;
                if(!rigs.TryGetValue(a.Id,out var rig)||rig?.Bones==null||rig.CyberMotion==null)continue;var g=rig.CyberMotion.IdleGlitch;if(!(g.Split>1e-3f))continue;
                var across=rig.Root.transform.right*(g.SplitOffset*.5f);string shell=Ghost(BodyModel(BodySkin(a.Id),BodyTier(rig.Root.transform.position,false)))??pilot;
                for(int k=0;k<2;k++)
                {
                    var gp=cyberIdleGhosts[used++];var off=Matrix4x4.Translate(k==0?across:-across);var c=k==0?magenta:cyan;
                    gp.Root=off*rig.Root.transform.localToWorldMatrix;for(int b=0;b<22&&b<rig.Bones.Length;b++)gp.Bones[b]=off*rig.Bones[b].localToWorldMatrix;
                    sourceModels.Queue(shell,gp.Root,gp.Bone,null,0,null,new Color(c.X,c.Y,c.Z,0),null,g.Split*CyberIdleGhostOpacity);
                }
            }
        }
        // What the Core effects read of this presentation (allocation-free once bound).
        private sealed class CyberScene:ICyberFxScene,ICyberShots
        {
            private readonly ArenaPresentation p;private readonly List<Actor> actors=new List<Actor>();private Camera camera;private readonly CyberShotList shots=new CyberShotList();
            public CyberScene(ArenaPresentation owner){p=owner;}
            public void Bind(Snapshot s,Camera c,NativeArena arena,CyberRangedPreview preview){camera=c;actors.Clear();if(s.Actors!=null)actors.AddRange(s.Actors);shots.Bind(arena,preview);}
            public int CyberShots=>shots.Shots;public CyberShot CyberShotAt(int i)=>shots.ShotAt(i);public int CyberImpacts=>shots.Impacts;public CyberImpact CyberImpactAt(int i)=>shots.ImpactAt(i);
            public IReadOnlyList<Actor> Actors=>actors;
            public NVector Feet(Actor a){var v=p.visualPositions.TryGetValue(a.Id,out var at)?at:ArenaPresentation.Vec(a.Position);return new NVector(v.x,v.y,v.z);}
            public bool Chest(Actor a,out NVector chest){chest=default;if(!p.rigs.TryGetValue(a.Id,out var rig))return false;var c=rig.Bones[CapturedRig.Chest].position+Vector3.up*.12f;chest=new NVector(c.x,c.y,c.z);return true;}
            private static readonly Vector3 FistLocal=CapturedAnimation.Vec(CapturedRig.Mirror(new NVector(0,-.06f,.03f)));
            public bool Fist(Actor a,bool left,out NVector fist){fist=default;if(!p.rigs.TryGetValue(a.Id,out var rig))return false;var f=rig.Bones[left?CapturedRig.LHand:CapturedRig.RHand].TransformPoint(FistLocal);fist=new NVector(f.x,f.y,f.z);return true;}
            public int Viewer=>p.FirstPerson&&!p.ScopeActive&&p.ViewCamera!=null&&p.viewRig!=null?p.LocalId:0;
            public int Local=>p.LocalId;
            public bool ViewFist(bool left,out NVector fist){fist=default;if(Viewer==0)return false;var f=p.viewRig.Bones[left?CapturedRig.LHand:CapturedRig.RHand].TransformPoint(FistLocal);fist=new NVector(f.x,f.y,f.z);return true;}
            public NVector Eye{get{var c=p.ViewCamera!=null&&p.FirstPerson?p.ViewCamera:camera;var e=c!=null?c.transform.position:Vector3.zero;return new NVector(e.x,e.y,e.z);}}
            public float Floor(NVector at)=>0;
        }
        // The effects' primitives onto the shared effects renderer, the weapons drawer and the glow pieces (view: the first-person layer).
        private sealed class CyberSink:ICyberFxSink
        {
            private readonly ArenaPresentation p;private NativeVfxDraw vfx;private NativeWeaponFxDraw draw;private bool viewOk;
            public CyberSink(ArenaPresentation owner){p=owner;}
            public void Bind(NativeVfxDraw v,NativeWeaponFxDraw d,bool view){vfx=v;draw=d;viewOk=view;}
            private static Vector3 U(NVector v)=>new Vector3(v.X,v.Y,v.Z);private static Color C(NVector c,float a=0)=>new Color(c.X,c.Y,c.Z,a);
            public void Glow(NVector at,NVector color,float coverage,float size,int profile,float u,float seed)=>vfx.Glow(U(at),C(color,coverage),size,profile,u,seed);
            public void Streak(NVector head,NVector velocity,NVector color,float width,float minLength,float blur,float taper,float u,float seed)=>vfx.Streak(U(head),U(velocity),C(color),width,minLength,blur,taper,u,seed);
            public void Smoke(NVector at,NVector albedo,float opacity,float size,float u,float seed,float floor)=>vfx.Smoke(U(at),C(albedo),opacity,size,u,seed,floor);
            public void Sprite(bool view,NVector at,NVector color,float size,int profile){if(view&&!viewOk)return;draw.Glow(view,U(at),C(color),size,profile);}
            public void Line(bool view,NVector tail,NVector head,NVector color,float width,bool shard){if(view&&!viewOk)return;draw.Axis(view,U(tail),U(head),C(color),width,shard?NativeWeaponFxDraw.ShardMode:NativeWeaponFxDraw.StreakMode,shard?0:.85f);}
            public void Band(bool view,NVector a,NVector b,NVector c,NVector d,float ua,float ub,NVector color)
            {if(view&&!viewOk)return;draw.Quad(view,U(a),U(b),U(c),U(d),new Vector2(ua,-1),new Vector2(ub,-1),new Vector2(ub,1),new Vector2(ua,1),C(color),NativeWeaponFxDraw.Ribbon,0);}
            public void Voxel(bool view,WeaponShape shape,NVector at,NQuaternion rotation,NVector scale,NVector color)
            {
                if(view&&!viewOk||p.sourceModels==null)return;
                p.sourceModels.QueuePiece(shape,true,Matrix4x4.TRS(U(at),new Quaternion(rotation.X,rotation.Y,rotation.Z,rotation.W),U(scale)),new Color(color.X,color.Y,color.Z,1),view?10:0,view?p.ViewCamera:null);
            }
        }

        // ---- Kernel Panic's private camera and screen glitch (NativeCinematicCamera.Direct; Core/KernelPanicCamera) ----
        /// <summary>The Kernel Panic sequence the local pilot is in this frame (caster or victim), or null.</summary>
        internal CyberLordView KernelPanicMine(float time)
        {
            foreach(var v in cyberViews.Values)if(KernelPanicFeel.Participant(v,LocalId)&&KernelPanicCamera.Active(v,time))return v;
            return null;
        }
        /// <summary>
        /// Directs the arena camera for a Kernel Panic participant: the shot on its hit-stopped clock, blended with the player's
        /// own camera in and out, kept out of walls, with the shake only under Settings camera shake, and the screen glitch staged
        /// for the frame. False (the camera untouched) when the local pilot is in no sequence.
        /// </summary>
        internal bool KernelPanicDirect(NativeArena arena,Camera view,float time,bool shake,bool reduced)
        {
            var v=KernelPanicMine(time);if(v==null||view==null)return false;float t=KernelPanicCamera.Clock(v,time,reduced);bool victim=v.Id!=LocalId;
            var shot=KernelPanicCamera.At(v,t,victim,reduced,shake,view.aspect);if(shot.Blend<=0)return false;
            Vector3 position=new Vector3(shot.Position.X,shot.Position.Y,shot.Position.Z),look=new Vector3(shot.Look.X,shot.Look.Y,shot.Look.Z),ray=position-look;float distance=ray.magnitude;
            if(distance>.05f&&arena?.Root!=null)
            {
                ray/=distance;
                if(arena.Root.scene.GetPhysicsScene().Raycast(look+ray*.12f,ray,out var hit,Mathf.Max(0,distance-.12f),NativeArena.WorldMask,QueryTriggerInteraction.Ignore))
                {var moved=NativeSirenCutscene.Occlude(new V3(position.x,position.y,position.z),new V3(look.x,look.y,look.z),hit.distance+.12f);position=new Vector3(moved.x,moved.y,moved.z);}
            }
            NativeSirenCutscene.Shake(time,shot.Shake,out float dx,out float dy,out float roll);
            var forward=look-position;if(forward.sqrMagnitude<1e-8f)forward=view.transform.forward;
            var rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.AngleAxis(-(roll+shot.Roll)*Mathf.Rad2Deg,Vector3.forward);
            float k=shot.Blend;view.transform.SetPositionAndRotation(Vector3.Lerp(view.transform.position,position+new Vector3(-dx,dy,0),k),Quaternion.Slerp(view.transform.rotation,rotation,k));
            view.fieldOfView=Mathf.Lerp(view.fieldOfView,shot.Fov,k);FirstPerson=false;AimHeld=false;
            Glitch=KernelPanicFeel.Screen(v,time,reduced,LowGraphics);GlitchTint=new Color(.13f,.9f,1,1);GlitchFrame=Time.frameCount;
            return true;
        }
    }

    // ---- audio (Core/NativeCyberLordSounds: existing bank keys, built-in fallbacks) ----
    public sealed partial class NativeAudio
    {
        private readonly NativeKitSound[] cyberSounds=new NativeKitSound[NativeCyberLordSounds.Max];private readonly CyberLordView cyberView=new CyberLordView();
        private readonly Dictionary<int,string[]> cyberKeys=new Dictionary<int,string[]>();
        /// <summary>A Cyber Lord pilot's state cues (the swing, each release, the wall's end, the link's end, Kernel Panic's beats), each once while fresh.</summary>
        private void CyberLordCues(NativePilot p,Vector3 pos,bool mine,float t)
        {
            if(p?.Pose==null||!CyberLordFeed.Read(p.Pose,cyberView))return;int id=p.Seat?.id??0;
            if(!cyberKeys.TryGetValue(id,out var keys)){keys=new string[NativeCyberLordSounds.Slots];for(int i=0;i<keys.Length;i++)keys[i]=id+":cyber"+i;cyberKeys.Add(id,keys);}
            for(int slot=0;slot<NativeCyberLordSounds.Slots;slot++)
            {
                int n=NativeCyberLordSounds.State(cyberView,slot,cyberSounds,out float at);if(n==0||!(t>=at&&t-at<.15f)||!Once(keys[slot],at))continue;
                for(int i=0;i<n;i++)Cue(cyberSounds[i].Key,cyberSounds[i].Clip,pos,cyberSounds[i].Gain,mine:mine);
            }
        }
        /// <summary>A fresh cyber event's cues (hits, absorbs, heals by their Detail).</summary>
        private void CyberLordEvent(in ArenaEvent e,Vector3 pos,bool targetMe,bool mine)
        {
            if(!CyberLordFeed.Kind(e.Kind))return;var hit=CyberLordFeed.Hit(e.Detail);if(hit==CyberHit.None)return;
            int n=NativeCyberLordSounds.Event(e.Kind,hit,targetMe,cyberSounds);for(int i=0;i<n;i++)Cue(cyberSounds[i].Key,cyberSounds[i].Clip,pos,cyberSounds[i].Gain,mine:mine);
        }
    }
}
