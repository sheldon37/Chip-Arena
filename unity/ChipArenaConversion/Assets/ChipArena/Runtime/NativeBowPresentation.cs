using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NMatrix=System.Numerics.Matrix4x4;

namespace ChipArena.UnityConversion
{
    // The Bow's models in the source bank (Core/NativeWeaponModels.Bow.cs): the live parts of both skins at every tier (the
    // riser and each limb, so the limbs flex about their pockets), the arrow, the quiver, the unit string segment, and the
    // Prototype Series held and loose bows (no exported model exists for a weapon added after the export). The Standard held
    // and loose bows ("weapon-std-bow", "loose-std-bow") come from the Standard skin bake (NativeWeaponSkinModels).
    public sealed partial class NativeSourceModels
    {
        /// <summary>The bow models built at load, for the build checks.</summary>
        public int BowModelCount{get;private set;}
        private void BowModels()
        {
            var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new InvalidOperationException("Source material shader is missing");
            var plain=new Material(shader){name="Native bow",enableInstancing=true};plain.SetInt("_Cull",2);plain.SetFloat("_Ink",0);materials.Add(plain);
            void Bank(string id,WeaponMesh mesh,int bone)
            {
                var parts=new List<Part>();foreach(var s in mesh.Sections){var m=WeaponMeshOf(s,id);if(m!=null)parts.Add(new Part{Name=id,Bone=bone==-2?s.Bone:bone,Mesh=m,Material=plain});}
                models[id]=parts.ToArray();BowModelCount++;
            }
            for(int sk=0;sk<2;sk++)
            {
                bool std=sk==0;
                for(int part=0;part<3;part++)for(int t=0;t<(std?NativeWeaponModels.Tiers:1);t++)Bank(NativeBowModels.PartId(std,part,t),NativeBowModels.Part(std,NativeBowModels.PartMask(part),t),-2);
                for(int t=0;t<(std?NativeWeaponModels.Tiers:1);t++){Bank(NativeBowModels.ArrowId(std,t),NativeBowModels.ArrowMesh(std,t),-1);Bank(NativeBowModels.QuiverId(std,t),NativeBowModels.QuiverMesh(std,t),-1);}
                Bank(std?NativeBowModels.StringId:NativeBowModels.StringProtoId,NativeBowModels.StringMesh(std),-1);
            }
            if(!models.ContainsKey(NativeBowModels.PrototypeHeldId)){var proto=NativeBowModels.Part(false,NativeBowModels.Held,0);Bank(NativeBowModels.PrototypeHeldId,proto,-2);Bank(NativeBowModels.PrototypeLooseId,proto,-1);}
        }
    }

    // The Bow's presentation in the arena (Claude; the rules proposed to Astra in docs/NEW-KITS.md section 2, read only through
    // Core NativeBowWire): the live bow on each posed rig (BowHeld: the riser, the limbs flexed by the pull, the string bent to
    // the nock, the nocked arrow or the Volley's fan, the arrow in the hand while nocking, the quiver on the back, the chip
    // modules on the arrow line and the enchant on the arrowhead), and the effects (BowFx: arrows in flight, glints, the Volley
    // and Rain effects, the slow tint, the stuck arrows riding the struck joint of the rig or the corpse). Host, guests,
    // spectators and the killcam replay draw the same frame from the replicated actors, shots and events. Presentation only.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The first-person bow rig's offset from the camera (Unity camera space).</summary>
        internal static Vector3 NativeBowView(bool _)=>CapturedAnimation.Vec(NativeBowPose.ViewOffset);
        private static Matrix4x4 BowPiece(in NativeBowPiece p)=>Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(p.Position)),CapturedAnimation.Quat(CapturedRig.Mirror(p.Rotation)),CapturedAnimation.Vec(p.Scale));
        private Matrix4x4 bowHand,bowPart;private Func<int,Matrix4x4> bowHandBone,bowPartBone;
        /// <summary>The bow pieces drawn by the last BowHeld (the checks and the debug overlay).</summary>
        public int BowPiecesDrawn{get;private set;}
        private bool BowHeld(CapturedAnimation.Player rig,Actor a,float time,bool first,int layer,Camera camera,Func<int,Color> tint,float flash)
        {
            if(!NativeBowWire.Is(a)||sourceModels==null||rig?.Bones==null)return false;
            bool std=WeaponSkin(a)!=NativeWeaponSkins.PrototypeIndex;float distance=0;var eye=WorldEye;if(!first&&eye!=null)distance=Vector3.Distance(eye.transform.position,rig.Root.transform.position);
            int tier=std?NativeWeaponSkins.Tier(LowGraphics,WeaponEffectsQuality,distance,first):0;
            NativeBow.View v;NativeBowPose.Frame f;
            if(Mathf.Abs(rig.BowAt-time)<1e-4f){v=rig.BowView;f=rig.BowFrame;}else{v=NativeBow.At(NativeBowWire.Of(a),time);f=default;f.Pull=v.Pull;}
            int n=BowDraw(rig,std,tier,v,f,AccentTint(a.AccentColor),std?flash:1,layer,camera,tint,first,time,HeldGlow);if(n<0)return false;
            BowDressing(a,f,v,time,first,layer,camera);BowPiecesDrawn=n;return true;
        }
        /// <summary>
        /// One posed bow on `rig`'s hands (rig frame `f`, bow `v`): the riser on the bow hand, the limbs flexed by the pull, the
        /// two string halves bent to the nock, the nocked arrow or the Volley's fan, the arrow in the string hand while nocking
        /// and (third person) the quiver on the back. Returns the pieces queued, or -1 when the models are not banked.
        /// </summary>
        private int BowDraw(CapturedAnimation.Player rig,bool std,int tier,in NativeBow.View v,in NativeBowPose.Frame f,Color accent,float hf,int layer,Camera camera,Func<int,Color> tint,bool first,float time,Vector4? glow)
        {
            string riser=NativeBowModels.PartId(std,0,tier);if(!sourceModels.Has(riser))return -1;
            bowHand=rig.Bones[CapturedRig.LHand].localToWorldMatrix;var root=rig.Root.transform.localToWorldMatrix;int n=0;
            bowHandBone??=b=>bowHand;bowPartBone??=b=>bowPart;
            sourceModels.Queue(riser,root,bowHandBone,null,layer,camera,accent:accent,hitFlash:hf,partTint:tint,glow:glow);n++;
            for(int l=0;l<2;l++){bowPart=bowHand*BowPiece(NativeBowRig.Limb(l==0,f.Pull));sourceModels.Queue(NativeBowModels.PartId(std,1+l,tier),root,bowPartBone,null,layer,camera,accent:accent,hitFlash:hf,partTint:tint,glow:glow);n++;}
            string str=std?NativeBowModels.StringId:NativeBowModels.StringProtoId;
            for(int s=0;s<2;s++){sourceModels.Queue(str,bowHand*BowPiece(NativeBowRig.String(s==0,f.Pull,v.Buzz,time)),null,null,layer,camera,accent:accent,partTint:tint);n++;}
            string arrow=NativeBowModels.ArrowId(std,tier);
            float cant=NativeBowPose.CantOf(v.Fan,first);
            if(v.Nocked)for(int i=0;i<Mathf.Max(1,v.Arrows);i++){sourceModels.Queue(arrow,bowHand*BowPiece(NativeBowRig.Arrow(f.Pull,i,v.Arrows,v.Arrows>1?NativeBow.VolleyFan:0,cant)),null,null,layer,camera,accent:accent,partTint:tint);n++;}
            if(f.HandArrow)
            {
                var piece=new NativeBowPiece{Position=f.HandArrowAt,Rotation=CapturedRig.FromTo(NVector.UnitZ,f.HandArrowDir),Scale=NVector.One};
                sourceModels.Queue(arrow,rig.Bones[CapturedRig.RHand].localToWorldMatrix*BowPiece(piece),null,null,layer,camera,accent:accent,partTint:tint);n++;
            }
            if(!first){sourceModels.Queue(NativeBowModels.QuiverId(std,tier),rig.Bones[CapturedRig.Chest].localToWorldMatrix,null,null,layer,camera,accent:accent,partTint:tint,hitFlash:hf);n++;}
            return n;
        }
        private readonly NativeBowPiece[] bowIntroArrows=new NativeBowPiece[NativeBowEntrance.MaxArrows];
        /// <summary>
        /// The Arrowfall entrance (Core/NativeBowEntrance): the intro rig's bow in the pilot's own skin for the bow, at the full
        /// tier and without chip dressing like every weapon intro, posed by the script's bow; and its loose arrows (the sky
        /// arrow, the volley falling and standing in the floor) in the world.
        /// </summary>
        private void BowIntro(NativeBowEntrance script,float t)
        {
            if(sourceModels==null||introRig?.Bones==null)return;bool std=NativeWeaponSkins.Index(introSkins,NativeBowWire.Spec)!=NativeWeaponSkins.PrototypeIndex;
            script.BowAt(t,out var v,out var f);if(BowDraw(introRig,std,0,v,f,introAccent,1,0,null,null,false,t,null)<0)return;
            string arrow=NativeBowModels.ArrowId(std,0);int n=script.Arrows(t,bowIntroArrows);
            for(int i=0;i<n;i++)sourceModels.Queue(arrow,BowPiece(bowIntroArrows[i]),null,null,0,null,accent:introAccent);
        }
        /// <summary>
        /// The chip modules on the bow, laid out as on the other ranged weapons (NativeWeaponDressing.Modules, the 'muzzle' just
        /// ahead of the riser) about the arrow line; and the enchant flames (burn, poison: the melee rule's elements, here on the
        /// ranged bow's arrowhead, where the arrow carries them) round the nocked arrowhead. Not in the menus or the intro. A
        /// Standard bow draws its designed modules instead (StandardBowDressing); these are the Prototype Series pieces.
        /// </summary>
        private void BowDressing(Actor a,in NativeBowPose.Frame f,in NativeBow.View v,float time,bool first,int layer,Camera camera)
        {
            if(Viewer)return;var pilot=LoadoutPilot(a.Id);if(pilot?.Stats==null||pilot.Weapon==null||pilot.Seat.chips==null)return;var chips=pilot.Seat.chips;var stats=pilot.Stats;
            // A Standard bow carries the designed modules and arrow enchant (NativeChipModulePresentation.cs); the Prototype keeps these pieces.
            if(StandardBowDressing(a,pilot,f,v,time,first,layer,camera))return;
            var line=bowHand*Matrix4x4.Translate(CapturedAnimation.Vec(CapturedRig.Mirror(NativeBowModels.ModuleOffset)));if(first)line*=Matrix4x4.Scale(Vector3.one*NativeWeaponDressing.FirstPersonModuleScale);
            int count=NativeWeaponDressing.Modules(NativeBowWire.Spec,true,(int)pilot.Weapon.color,chips,stats,weaponPieces);
            for(int i=0;i<count;i++){var p=weaponPieces[i];NativeWeaponDressing.Animate(ref p,time);QueuePiece(p,line,layer,camera,rigFactor);}
            bool fire=stats["burn"]>0,poison=stats["poison"]>0;if(!fire&&!poison||!v.Nocked)return;
            chips.TryGetValue("ember",out int ember);chips.TryGetValue("poison",out int motes);int flames=NativeWeaponDressing.Enchant(NativeBowWire.Spec,fire,poison,ember,motes,weaponPieces,0);
            // The enchant's blade track mapped onto the arrow's front third (its head at the nock plus the arrow's length).
            float blade=NativeWeaponDressing.BladeLength(NativeBowWire.Spec),head=NativeBow.NockAt(f.Pull).Z+NativeBow.ArrowLength;
            var track=bowHand*Matrix4x4.Translate(CapturedAnimation.Vec(CapturedRig.Mirror(new NVector(NativeBow.ArrowX,NativeBow.ArrowY,head-.3f))))*Matrix4x4.Scale(new Vector3(.45f,.45f,.3f/blade));
            for(int i=0;i<flames;i++){var p=weaponPieces[i];NativeWeaponDressing.Animate(ref p,time);QueuePiece(p,track,layer,camera);}
        }

        // ---- effects ----
        private BowScene bowScene;private BowSink bowSink;private readonly NativeBowStuck bowStuck=new NativeBowStuck();
        private NativeArena bowArena;private int bowGeneration=-1,bowCursor;private bool bowRoundOver;
        private readonly Dictionary<int,(float draw,NativeBow.Arrow kind)> bowShots=new Dictionary<int,(float,NativeBow.Arrow)>();
        private readonly Dictionary<int,Vector3> bowVelocity=new Dictionary<int,Vector3>();private readonly List<int> bowStale=new List<int>();
        private readonly Dictionary<int,float> bowSlow=new Dictionary<int,float>(),bowRainSeen=new Dictionary<int,float>();
        private readonly Dictionary<int,bool> bowAlive=new Dictionary<int,bool>();
        // The arrows seen in flight (last place, velocity, draw, skin, owner, frame), the projectiles an event already stuck, and
        // the recent hits on pilots (owner, time, place): an arrow gone with neither struck the world where it vanished.
        private struct BowLast{public Vector3 At,Velocity;public float Draw;public bool Standard;public int Owner,Frame;}
        private readonly Dictionary<int,BowLast> bowLast=new Dictionary<int,BowLast>();private readonly HashSet<int> bowHandled=new HashSet<int>();
        private readonly (int owner,float at,Vector3 point)[] bowHits=new (int,float,Vector3)[32];private int bowHitNext,bowFrame;
        private NativeBowStuck.BoneOf bowBone;private Func<int,NMatrix> bowJoint;private CapturedAnimation.Player bowJointRig;
        /// <summary>The stuck arrows (the checks).</summary>
        internal NativeBowStuck BowStuck=>bowStuck;
        private void BowClear(){ArrowClear();bowLast.Clear();bowHandled.Clear();for(int i=0;i<bowHits.Length;i++)bowHits[i]=(0,-100,Vector3.zero);bowStuck.Clear();bowShots.Clear();bowVelocity.Clear();bowSlow.Clear();bowRainSeen.Clear();bowAlive.Clear();bowCursor=0;}
        private static NMatrix BowN(in Matrix4x4 m)=>new NMatrix(m.m00,m.m10,m.m20,m.m30,m.m01,m.m11,m.m21,m.m31,m.m02,m.m12,m.m22,m.m32,m.m03,m.m13,m.m23,m.m33);
        private static NVector BowN(Vector3 v)=>new NVector(v.x,v.y,v.z);
        private static Vector3 BowU(NVector v)=>new Vector3(v.X,v.Y,v.Z);
        /// <summary>A pilot's joint for a stuck arrow: its corpse's as drawn after a death (severed parts keep their own; a split
        /// body's arrow rides the half it sat on, NativeArrowGibPresentation.ArrowHalf; a crushed part's arrows go with it), else
        /// its posed rig.</summary>
        private bool BowBone(int target,int bone,NVector local,out NMatrix world)
        {
            world=NMatrix.Identity;
            if(corpses.TryGetValue(target,out var corpse)){var half=ArrowHalf(corpse,bone,local);if(!half.ShownBone(bone))return false;world=BowN(half.PresentedBone(bone));return true;}
            // A horde add (NativeHordeGorePresentation.cs): its live or falling rig, then its corpse; none once gibbed or gone.
            if(target>=10000)return HordeArrowBone(target,bone,out world);
            if(bowArena!=null){var p=PilotOf(bowArena,target);if(p==null||p.Hp<=0)return false;}
            if(rigs.TryGetValue(target,out var rig)&&rig?.Bones!=null){world=BowN(rig.Bones[bone].localToWorldMatrix);return true;}
            return false;
        }
        private NativePilot BowSource(NativeArena arena,int id){var p=PilotOf(arena,id);return p!=null&&NativeBowWire.Is(p.Seat.spec)?p:null;}
        /// <summary>WeaponFx, after the gun kit: the frame's bow effects (on the pose clock), the stuck arrows and the slow tints.</summary>
        private void BowFx(NativeArena arena,float now)
        {
            if(vfx==null||sourceModels==null||arena==null)return;var draw=WeaponFxDraw;if(draw==null)return;float time=kitClock;
            if(!ReferenceEquals(bowArena,arena)||bowGeneration!=arena.Generation){BowClear();bowArena=arena;bowGeneration=arena.Generation;}
            // Round end (the corpses' rule): every arrow in a pilot goes with the round; walls keep their own six seconds.
            bool over=NativeCorpseRules.RoundEnded(arena.Match.Cooperative,arena.Match.Phase);if(over&&!bowRoundOver)bowStuck.Clear();bowRoundOver=over;
            // A pilot standing again (respawn, revive) sheds the arrows it carried.
            foreach(var p in arena.Pilots){bool alive=p.Hp>0;if(bowAlive.TryGetValue(p.Seat.id,out bool was)&&alive&&!was)bowStuck.ClearPilot(p.Seat.id);bowAlive[p.Seat.id]=alive;}
            BowEvents(arena,time);
            bowFrame++;bowScene??=new BowScene(this);bowSink??=new BowSink();bowScene.Bind(arena,shotTracker,now,weaponSnapshot);bowSink.Bind(vfx,draw,sourceModels,FirstPerson&&!ScopeActive&&ViewCamera!=null,ViewCamera);
            float q=Mathf.Clamp01(WeaponEffectsQuality);bool low=LowGraphics;
            BowVanished(arena,time);
            NativeBowFx.Draw(bowSink,bowScene,time,q,low);
            bowStuck.Update(time);bowBone??=BowBone;bowStuck.Draw(bowSink,time,bowBone);ArrowBleeds(time);
            // The slow tint on pilots a rain arrow hit.
            foreach(var p in arena.Pilots){if(p.Hp<=0||!bowSlow.TryGetValue(p.Seat.id,out float at))continue;float w=NativeBowFx.SlowWeight(at,time);if(w>0)NativeBowFx.SlowTint(bowSink,BowN(p.Body.position-Vector3.up*.865f),w,time,p.Seat.id,q,low);}
        }
        private void BowEvents(NativeArena arena,float time)
        {
            var events=arena.Events;int newest=0;for(int i=0;i<events.Count;i++)newest=Math.Max(newest,events[i].Id);if(newest<bowCursor)bowCursor=0;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=bowCursor)continue;
                if(NativeBowWire.Shot(e,out var kind,out float d)){bowShots[NativeBowWire.Projectile(e)]=(d,kind);if(kind==NativeBow.Arrow.Rain)bowRainSeen[e.Source]=e.Time;continue;}
                if(e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot&&e.Kind!=EventKind.Impact)continue;var source=BowSource(arena,e.Source);if(source==null)continue;
                bool std=NativeWeaponSkins.Index(source.Seat.weaponSkins,NativeBowWire.Spec)!=NativeWeaponSkins.PrototypeIndex;Vector3 at=NativeArena.Vec(e.Position);
                float draw=bowShots.TryGetValue(e.Projectile,out var shot)?shot.draw:1;
                Vector3 dir=bowVelocity.TryGetValue(e.Projectile,out var vel)&&vel.sqrMagnitude>.01f?vel.normalized:NativeArena.Vec(e.Normal).sqrMagnitude>.25f?(e.Kind==EventKind.Impact?-NativeArena.Vec(e.Normal):NativeArena.Vec(e.Normal)).normalized:(at-source.Body.position).normalized;
                if(e.Kind!=EventKind.Impact){bowHits[bowHitNext]=(e.Source,e.Time,at);bowHitNext=(bowHitNext+1)%bowHits.Length;}else if(e.Projectile>0)bowHandled.Add(e.Projectile);
                if(NativeBowWire.Lodges(e,source.Seat.spec)){bowStuck.AddWall(e.Id,BowN(at),BowN(dir),e.Time,draw,std,LowGraphics);continue;}
                if(!NativeBowWire.Sticks(e,source.Seat.spec))continue;
                var r=NativeBowWire.Of(source.Pose);if(NativeBowFx.RainHit(r,BowN(at),e.Time))bowSlow[e.Target]=e.Time;
                if(e.Detail=="field")continue;
                // A pilot's rig, or a horde add drawn with its kind's model (the arrow lodges in the zombie and stays with its corpse).
                if(!rigs.TryGetValue(e.Target,out var rig)||rig?.Bones==null){if(e.Target<10000||!HordeKindDrawn(e.Target)||!minionRigs.TryGetValue(e.Target,out rig)||rig?.Bones==null)continue;}
                // The struck joint (nearest the replicated hit point on the posed rig) and the arrow in its frame.
                bowJointRig=rig;bowJoint??=b=>BowN(bowJointRig.Bones[b].localToWorldMatrix);int bone=NativeBowStuck.Nearest(bowJoint,BowN(at));
                // Lodged halfway into the part, or gone through it (NativeArrowGibPresentation.cs).
                ArrowStrike(arena,e,rig,bone,at,dir,draw,std);
            }
            bowCursor=Math.Max(bowCursor,newest);
            // Bounded maps: drop shots and velocities no longer in flight.
            if(bowShots.Count>256||bowVelocity.Count>256){bowStale.Clear();foreach(int id in bowVelocity.Keys){bool live=false;foreach(var b in arena.Bullets)if(b.Id==id){live=true;break;}if(!live)bowStale.Add(id);}foreach(int id in bowStale){bowVelocity.Remove(id);bowShots.Remove(id);}}
        }
        /// <summary>
        /// Arrows that left the frame's shots without an event: the map's static walls and floor raise no Impact (only props do),
        /// so an arrow gone with no prop impact and no hit on a pilot near where it vanished struck the world there: cast from just
        /// behind its last place along its flight against the world (cosmetic: the presentation's own query) and stick it.
        /// </summary>
        private void BowVanished(NativeArena arena,float time)
        {
            bowStale.Clear();
            foreach(var pair in bowLast)
            {
                if(pair.Value.Frame==bowFrame)continue;bowStale.Add(pair.Key);var w=pair.Value;if(bowHandled.Contains(pair.Key))continue;bool hit=false;
                for(int i=0;i<bowHits.Length;i++){var h=bowHits[i];if(h.owner==w.Owner&&Mathf.Abs(h.at-time)<.25f&&(h.point-w.At).sqrMagnitude<4){hit=true;break;}}
                float speed=w.Velocity.magnitude;if(hit||!(speed>1))continue;Vector3 dir=w.Velocity/speed;
                if(arena.PhysicsWorld.Raycast(w.At-dir*.3f,dir,out var ray,speed/20+.8f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))bowStuck.AddWall(-pair.Key-1,BowN(ray.point),BowN(dir),time,w.Draw,w.Standard,LowGraphics);
            }
            foreach(int id in bowStale){bowLast.Remove(id);bowHandled.Remove(id);}
        }
        // What the Core effects read of this presentation (allocation-free once bound).
        private sealed class BowScene:IBowFxScene
        {
            private readonly ArenaPresentation p;private readonly List<Actor> actors=new List<Actor>();private readonly List<NativeBowArrowView> arrows=new List<NativeBowArrowView>();
            public BowScene(ArenaPresentation owner){p=owner;}
            public void Bind(NativeArena arena,NativeShotTracker tracker,float now,Snapshot s)
            {
                actors.Clear();if(s?.Actors!=null)actors.AddRange(s.Actors);arrows.Clear();
                foreach(var b in arena.Bullets)
                {
                    if(!NativeBowWire.Is(b.Spec))continue;NativeShotViews.Bullet(arena,b,tracker,now,out var v);var vel=new Vector3((float)v.VX,(float)v.VY,(float)v.VZ);p.bowVelocity[b.Id]=vel;
                    int owner=b.Original!=0?b.Original:b.Owner;float draw;NativeBow.Arrow kind;
                    if(NativeBowWire.Arrow(b.Detail,b.Draw,out kind,out draw)){}else if(p.bowShots.TryGetValue(b.Id,out var shot)){draw=shot.draw;kind=shot.kind;}else{draw=NativeBowWire.SpeedDraw(b.BaseVelocity.sqrMagnitude>.01f?b.BaseVelocity.magnitude:vel.magnitude);kind=draw>=.999f?NativeBow.Arrow.Full:NativeBow.Arrow.Quick;}
                    var seat=NativeShotViews.Seat(arena,owner);bool std=seat==null||NativeWeaponSkins.Index(seat.weaponSkins,NativeBowWire.Spec)!=NativeWeaponSkins.PrototypeIndex;
                    p.bowLast[b.Id]=new BowLast{At=new Vector3((float)v.X,(float)v.Y,(float)v.Z),Velocity=vel,Draw=draw,Standard=std,Owner=owner,Frame=p.bowFrame};
                    arrows.Add(new NativeBowArrowView{Id=b.Id,Owner=owner,Position=new NVector((float)v.X,(float)v.Y,(float)v.Z),Velocity=BowN(vel),Born=b.Born,Draw=draw,Kind=kind,Standard=std});
                }
            }
            public IReadOnlyList<Actor> Actors=>actors;
            public int Viewer=>p.FirstPerson&&!p.ScopeActive&&p.ViewCamera!=null&&p.viewRig!=null?p.LocalId:0;
            public int Arrows=>arrows.Count;public NativeBowArrowView ArrowAt(int i)=>arrows[i];
            public bool Standard(Actor a)=>p.WeaponSkin(a)!=NativeWeaponSkins.PrototypeIndex;
            public bool RainShots(Actor a,float rainAt)=>a!=null&&p.bowRainSeen.TryGetValue(a.Id,out float at)&&at>=rainAt-.05f;
            public bool Bow(Actor a,bool view,out GunFrame frame)
            {
                frame=default;CapturedAnimation.Player rig;
                if(view){if(Viewer==0||a.Id!=p.LocalId)return false;rig=p.viewRig;}else{if(p.FirstPerson&&a.Id==p.LocalId||!p.rigs.TryGetValue(a.Id,out rig))return false;}
                if(rig?.Bones==null)return false;var m=rig.Bones[CapturedRig.LHand].localToWorldMatrix;Vector3 z=m.MultiplyVector(Vector3.forward);float scale=z.magnitude;if(!(scale>1e-4f))return false;
                z/=scale;Vector3 x=m.MultiplyVector(Vector3.right).normalized,y=Vector3.Cross(z,x).normalized;frame=new GunFrame{O=BowN(m.MultiplyPoint3x4(Vector3.zero)),X=BowN(x),Y=BowN(y),Z=BowN(z),Scale=scale};return true;
            }
        }
        // The effects' primitives onto the shared effects renderer, the weapons drawer and the source bank (view: first person).
        private sealed class BowSink:IBowFxSink
        {
            private NativeVfxDraw vfx;private NativeWeaponFxDraw draw;private NativeSourceModels models;private bool viewOk;private Camera viewCamera;
            public void Bind(NativeVfxDraw v,NativeWeaponFxDraw d,NativeSourceModels m,bool view,Camera camera){vfx=v;draw=d;models=m;viewOk=view;viewCamera=camera;}
            private static Color C(NVector c,float a=0)=>new Color(c.X,c.Y,c.Z,a);
            public void Glow(NVector at,NVector color,float coverage,float size,int profile,float u,float seed)=>vfx.Glow(BowU(at),C(color,coverage),size,profile,u,seed);
            public void Streak(NVector head,NVector velocity,NVector color,float width,float minLength,float blur,float taper,float u,float seed)=>vfx.Streak(BowU(head),BowU(velocity),C(color),width,minLength,blur,taper,u,seed);
            public void Smoke(NVector at,NVector albedo,float opacity,float size,float u,float seed,float floor)=>vfx.Smoke(BowU(at),C(albedo),opacity,size,u,seed,floor);
            public void Sprite(bool view,NVector at,NVector color,float size,int profile){if(view&&!viewOk)return;draw.Glow(view,BowU(at),C(color),size,profile);}
            public void Line(bool view,NVector tail,NVector head,NVector color,float width,bool shard){if(view&&!viewOk)return;draw.Axis(view,BowU(tail),BowU(head),C(color),width,shard?NativeWeaponFxDraw.ShardMode:NativeWeaponFxDraw.StreakMode,shard?0:.85f);}
            public void Band(bool view,NVector a,NVector b,NVector c,NVector d,float ua,float ub,NVector color)
            {if(view&&!viewOk)return;draw.Quad(view,BowU(a),BowU(b),BowU(c),BowU(d),new Vector2(ua,-1),new Vector2(ub,-1),new Vector2(ub,1),new Vector2(ua,1),C(color),NativeWeaponFxDraw.Ribbon,0);}
            public void Arrow(NVector nock,NVector dir,float opacity,bool view,bool standard)
            {
                if(opacity<=.01f||view&&!viewOk)return;var d=BowU(dir);if(d.sqrMagnitude<1e-6f)return;
                models.Queue(NativeBowModels.ArrowId(standard,0),Matrix4x4.TRS(BowU(nock),Quaternion.LookRotation(d,Mathf.Abs(d.y)>.95f?Vector3.forward:Vector3.up),Vector3.one),null,null,view?10:0,view?viewCamera:null,opacity:opacity);
            }
        }
    }
}
