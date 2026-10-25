using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The melee Q/E kit's presentation in the arena (Claude; Astra's simulation, docs/MELEE-SIMULATION-HANDOFF.md). The pilot
    // poses are Core/MeleeKitMotion (reached through NativeSpecialPose on every rig route); the effects are Core/NativeMeleeKitFx,
    // drawn here from what every peer holds: the snapshot's actors (their replicated MeleeKit) and kit events, the posed rigs
    // (blades, chests, the first-person view weapon), the seat's weapon colour and the melee projectiles in flight. Host, guests,
    // spectators and the killcam replay draw the same frame. Effects quality (Reduced) and the low graphics tier thin them as
    // the other effects do. Two hooks: RenderMeleeKit at the end of Render (event intake, the Shadow Step decoy and the dash
    // ghosts as weapon-ghost models) and MeleeKitFx in RenderGlow before the weapons drawer is submitted (the particles into the
    // shared effects renderer, the bands, sprites and lines into the weapons drawer, the caster's own on the view layer).
    // Presentation only: nothing here reaches the authority, the network frame, hit timing, hitboxes, damage or movement.
    public sealed partial class ArenaPresentation
    {
        private readonly NativeMeleeKitFx meleeKitFx=new NativeMeleeKitFx();
        private KitScene kitScene;private KitSink kitSink;private Snapshot kitSnapshot;private float kitClock;
        private sealed class DecoyRig{public CapturedAnimation.Player Rig;public float Until;public Actor Pose;}
        private readonly Dictionary<int,DecoyRig> decoys=new Dictionary<int,DecoyRig>();private readonly List<int> staleDecoys=new List<int>();
        private sealed class DashGhost{public GhostPose Pose=new GhostPose();public float At;public string Weapon,Body;public Color Tint;public bool Live,Model;public VisualEcho Echo;}
        private readonly DashGhost[] dashGhosts=new DashGhost[NativeMeleeKitFx.DashGhostSlots];private int dashNext;
        private readonly Dictionary<int,(Vector3 At,float Cast)> dashLast=new Dictionary<int,(Vector3,float)>();

        /// <summary>End of Render: the frame's kit events, the decoys and the dash ghosts (after the rigs are posed).</summary>
        private void RenderMeleeKit(Snapshot s,float time)
        {
            kitSnapshot=s;kitClock=time;if(s?.Actors==null)return;meleeKitFx.Observe(s.Events,time);
            if(sourceModels==null)return;
            Decoys(s,time);
            DashGhosts(s,time);
        }
        // The Shadow Step decoy: the caster's departure crouch (its windup's last pose, posed on a rig of its own so a late
        // joiner, the killcam and the caster's own first-person view all see the same figure), drawn as a weapon ghost in the
        // daggers' violet at kit.Afterimage for the decoy's life (NativeMeleeKitFx.DecoyOpacity).
        private void Decoys(Snapshot s,float time)
        {
            foreach(var a in s.Actors)
            {
                var k=a?.MeleeKit;float opacity=NativeMeleeKitFx.DecoyOpacity(k,time);if(opacity<=0)continue;
                // The caster's own body skin (NativeBodySkinPresentation.cs) at the decoy's distance.
                string pilot=Ghost(BodyModel(BodySkin(a.Id),BodyTier(new Vector3(k.Afterimage.x,k.Afterimage.y,k.Afterimage.z),false)));if(pilot==null)continue;
                if(!decoys.TryGetValue(a.Id,out var d)||d.Until!=k.AfterimageUntil)
                {
                    if(d==null){d=new DecoyRig{Rig=captured.Create(transform,-700-a.Id)};decoys[a.Id]=d;}
                    d.Until=k.AfterimageUntil;d.Pose=NativeMeleeKitFx.DecoyActor(a,k);
                }
                var feet=new Vector3(k.Afterimage.x,k.Afterimage.y-.865f,k.Afterimage.z);float at=NativeMeleeKitFx.DecoyPoseTime(k);
                d.Rig.ActorPose(d.Pose,feet,at);
                var tint=NativeWeaponFxShapes.Fx(NativeMeleeKitFx.DecoyTint());var root=d.Rig.Root.transform.localToWorldMatrix;
                sourceModels.Queue(pilot,root,b=>b>=0&&b<d.Rig.Bones.Length?d.Rig.Bones[b].localToWorldMatrix:root,null,0,null,tint,null,opacity);
                string weapon=Ghost(SkinnedHeld(a));
                if(weapon!=null){float reach=WeaponReach(d.Rig,d.Pose,at);sourceModels.Queue(weapon,root,b=>SourceWeaponBone(d.Rig,d.Pose,at,false,b,reach),null,0,null,tint,null,opacity);}
            }
            staleDecoys.Clear();foreach(var pair in decoys){var a=System.Array.Find(s.Actors,x=>x!=null&&x.Id==pair.Key);if(a?.MeleeKit==null||NativeMeleeKitFx.DecoyOpacity(a.MeleeKit,time)<=0)staleDecoys.Add(pair.Key);}
            foreach(int id in staleDecoys){decoys[id].Rig.Dispose();decoys.Remove(id);}
        }
        private readonly Dictionary<string,string> kitGhostModels=new Dictionary<string,string>();
        private string Ghost(string model){if(!kitGhostModels.TryGetValue(model,out var id))kitGhostModels[model]=id=sourceModels.Afterimage(model);return id;}
        // Flash Step and Bulwark Charge leave accent ghosts of the dashing rig every DashGhostSpacing metres (as the roll afterimages).
        private void DashGhosts(Snapshot s,float time)
        {
            if(Ghost(NativeBodySkins.PrototypeModel)==null||!TrailsEnabled)return;float q=WeaponEffectsQuality;
            foreach(var a in s.Actors)
            {
                if(a==null||!a.Alive||FirstPerson&&a.Id==LocalId||!rigs.TryGetValue(a.Id,out var rig)||!NativeMeleeKitFx.Dashing(a,time))continue;
                var root=rig.Root.transform.position;float cast=a.MeleeKit.At;
                if(!dashLast.TryGetValue(a.Id,out var last)||last.Cast!=cast){dashLast[a.Id]=(root,cast);continue;}
                if(Vector3.Distance(root,last.At)<NativeMeleeKitFx.DashGhostSpacing*(q<.5f?1.6f:1))continue;dashLast[a.Id]=(root,cast);
                var g=dashGhosts[dashNext]??(dashGhosts[dashNext]=new DashGhost());dashNext=(dashNext+1)%dashGhosts.Length;
                g.Pose.Root=rig.Root.transform.localToWorldMatrix;for(int b=0;b<22&&b<rig.Bones.Length;b++)g.Pose.Bones[b]=rig.Bones[b].localToWorldMatrix;
                g.Weapon=!a.WeaponAway?Ghost(SkinnedHeld(a)):null;g.Pose.Armed=g.Weapon!=null;g.Body=Ghost(BodyModel(BodySkin(a.Id),BodyTier(root,false)));
                if(g.Pose.Armed){float reach=WeaponReach(rig,a,time);for(int b=0;b<22&&b<rig.Bones.Length;b++)g.Pose.Weapon[b]=SourceWeaponBone(rig,a,time,false,b,reach);}
                g.Model=VisualEchoSnap(rig,ref g.Echo);g.At=time;g.Live=true;g.Tint=NativeWeaponFxShapes.Fx(NativeMeleeKitFx.DashTint(a.SourceSpec,LoadoutPilot(a.Id)?.Weapon!=null?(int)LoadoutPilot(a.Id).Weapon.color:0));
            }
            foreach(var g in dashGhosts)
            {
                if(g==null||!g.Live)continue;float opacity=NativeMeleeKitFx.DashGhostFade(time-g.At);if(opacity<=0){g.Live=false;continue;}
                if(g.Model){VisualEchoDraw(g.Echo,"NativeWeaponGhost",g.Tint,opacity);continue;}
                sourceModels.Queue(g.Body??Ghost(NativeBodySkins.PrototypeModel),g.Pose.Root,g.Pose.Bone,null,0,null,g.Tint,null,opacity);
                if(g.Pose.Armed)sourceModels.Queue(g.Weapon,g.Pose.Root,g.Pose.WeaponBone,null,0,null,g.Tint,null,opacity);
            }
        }
        /// <summary>RenderGlow, before the weapons drawer is submitted: every kit effect of the frame.</summary>
        private void MeleeKitFx(NativeArena arena,Camera camera,float now)
        {
            var s=kitSnapshot;if(s?.Actors==null||vfx==null)return;var draw=WeaponFxDraw;if(draw==null)return;
            kitScene??=new KitScene(this);kitSink??=new KitSink();kitScene.Bind(s,arena,kitClock);kitSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            meleeKitFx.Draw(kitSink,kitScene,kitClock,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics);
        }
        private void ClearMeleeKit()
        {
            meleeKitFx.Clear();kitSnapshot=null;dashLast.Clear();foreach(var g in dashGhosts)if(g!=null)g.Live=false;
            foreach(var d in decoys.Values)d.Rig.Dispose();decoys.Clear();
        }

        // What the Core effects read of this presentation (allocation-free once bound).
        private sealed class KitScene:IKitFxScene
        {
            private readonly ArenaPresentation p;private Snapshot s;private NativeArena arena;private float time;private readonly List<Actor> actors=new List<Actor>();
            public KitScene(ArenaPresentation owner){p=owner;}
            public void Bind(Snapshot snapshot,NativeArena a,float t){s=snapshot;arena=a;time=t;actors.Clear();if(s.Actors!=null)actors.AddRange(s.Actors);}
            public IReadOnlyList<Actor> Actors=>actors;
            public NVector Feet(Actor a){var v=p.visualPositions.TryGetValue(a.Id,out var at)?at:ArenaPresentation.Vec(a.Position);return new NVector(v.x,v.y,v.z);}
            public bool Chest(Actor a,out NVector chest){chest=default;if(!p.rigs.TryGetValue(a.Id,out var rig))return false;var c=rig.Bones[CapturedRig.Chest].position+Vector3.up*.12f;chest=new NVector(c.x,c.y,c.z);return true;}
            public bool Blade(Actor a,bool left,out NVector root,out NVector tip)
            {
                root=tip=default;if(left&&a.SourceSpec!="daggers"||!p.rigs.TryGetValue(a.Id,out var rig))return false;
                if(!p.BladeLine(rig,a,time,false,left,out var r,out var t))return false;root=new NVector(r.x,r.y,r.z);tip=new NVector(t.x,t.y,t.z);return true;
            }
            public int Viewer=>p.FirstPerson&&!p.ScopeActive&&p.ViewCamera!=null&&p.viewRig!=null?p.LocalId:0;
            public bool ViewBlade(bool left,out NVector root,out NVector tip)
            {
                root=tip=default;if(Viewer==0)return false;Actor a=null;foreach(var x in actors)if(x!=null&&x.Id==p.LocalId){a=x;break;}if(a==null||left&&a.SourceSpec!="daggers")return false;
                if(!p.BladeLine(p.viewRig,a,time,true,left,out var r,out var t))return false;root=new NVector(r.x,r.y,r.z);tip=new NVector(t.x,t.y,t.z);return true;
            }
            public int WeaponColor(Actor a){var w=p.LoadoutPilot(a.Id)?.Weapon;return w!=null?(int)w.color:NativeBlockFx.DefaultColor(a.SourceSpec,a.VisualSet);}
            public int Shots=>arena?.MeleeShots.Count??0;
            public bool Shot(int i,out NVector at,out NVector velocity,out string fx,out int owner)
            {
                var m=arena.MeleeShots[i];at=new NVector(m.Position.x,m.Position.y,m.Position.z);velocity=new NVector(m.Velocity.x,m.Velocity.y,m.Velocity.z);fx=m.Fx;owner=m.Original?.Seat.id??0;return true;
            }
        }
        // The effects' primitives onto the shared effects renderer and the weapons drawer (view: the first-person layer).
        private sealed class KitSink:IKitFxSink
        {
            private NativeVfxDraw vfx;private NativeWeaponFxDraw draw;private bool viewOk;
            public void Bind(NativeVfxDraw v,NativeWeaponFxDraw d,bool view){vfx=v;draw=d;viewOk=view;}
            private static Vector3 U(NVector v)=>new Vector3(v.X,v.Y,v.Z);private static Color C(NVector c,float a=0)=>new Color(c.X,c.Y,c.Z,a);
            public void Glow(NVector at,NVector color,float coverage,float size,int profile,float u,float seed)=>vfx.Glow(U(at),C(color,coverage),size,profile,u,seed);
            public void Streak(NVector head,NVector velocity,NVector color,float width,float minLength,float blur,float taper,float u,float seed)=>vfx.Streak(U(head),U(velocity),C(color),width,minLength,blur,taper,u,seed);
            public void Smoke(NVector at,NVector albedo,float opacity,float size,float u,float seed,float floor)=>vfx.Smoke(U(at),C(albedo),opacity,size,u,seed,floor);
            public void Sprite(bool view,NVector at,NVector color,float size,int profile){if(view&&!viewOk)return;draw.Glow(view,U(at),C(color),size,profile);}
            public void Line(bool view,NVector tail,NVector head,NVector color,float width,bool shard){if(view&&!viewOk)return;draw.Axis(view,U(tail),U(head),C(color),width,shard?NativeWeaponFxDraw.ShardMode:NativeWeaponFxDraw.StreakMode,shard?0:.85f);}
            public void Band(bool view,NVector a,NVector b,NVector c,NVector d,float ua,float ub,NVector color)
            {if(view&&!viewOk)return;draw.Quad(view,U(a),U(b),U(c),U(d),new Vector2(ua,-1),new Vector2(ub,-1),new Vector2(ub,1),new Vector2(ua,1),C(color),NativeWeaponFxDraw.Ribbon,0);}
        }
    }
}
