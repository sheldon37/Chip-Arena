using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // Draws the pure NativeBlastSim (source chip-blasts.ts) in the arena through the shared effects renderer (NativeVfxDraw:
    // each pooled particle as the primitive its role names, each shock front as one fresnel shell; owner 2026-10-11 effects
    // overhaul, in place of the source's flat cube per voxel), and three pooled point lights. Blasts are keyed to what
    // host and guests both hold: the ability/chip effect list with its Explosion events, ability zones, chip fields, ability
    // shots and the event stream. The event stream also feeds the source's event-vfx.ts draws (NativeImpactCues): voxel
    // spark bursts and lit streaks into the projectile visuals' BurstPool port (Sparks), impacts, lines and paddles here.
    // The Siren's own attack records are not blasts in the source (siren/fx.ts, NativeDevEffects) and draw nothing here.
    // Every blast and impact, field arrivals included, gets the real floor under it (FloorUnder; owner 2026-10-11: no flat area
    // disc, so a wall, ceiling or airburst hit is a sphere whose pieces fall to that floor). Cosmetic only; nothing here touches
    // the match.
    public sealed class NativeBlastPresentation : IDisposable
    {
        private struct Marker{public Vector3 Position;public string Tag;public float Reach;}
        private readonly NativeBlastSim sim=new NativeBlastSim();
        private readonly NativeVfxDraw vfx;private readonly bool ownsVfx;private readonly GameObject root;private readonly Light[] lights=new Light[NativeBlastSim.LightCapacity];
        private HashSet<long> seen=new HashSet<long>(),seenBefore=new HashSet<long>();
        private List<Marker> sites=new List<Marker>(),sitesBefore=new List<Marker>(),shots=new List<Marker>(),shotsBefore=new List<Marker>();
        private List<NativeBlastFrame.SirenSite> sirens=new List<NativeBlastFrame.SirenSite>(),sirensBefore=new List<NativeBlastFrame.SirenSite>();private readonly HashSet<int> sirenSeats=new HashSet<int>();
        private readonly List<Vector3> landings=new List<Vector3>();
        private readonly List<NativeBlastFrame.Candidate> fresh=new List<NativeBlastFrame.Candidate>();private readonly List<NativeAbilityFx> freshFx=new List<NativeAbilityFx>();
        /// <summary>The source BurstPool the event sparks and streaks go into (NativeShotPresentation.SparkPool, drawn and stepped
        /// with the projectile visuals). Null: no sparks.</summary>
        public NativeBurstPool Sparks;
        /// <summary>The projectile trails setting: the source streaks an impact from its from point only with trails on.</summary>
        public bool Trails=true;
        /// <summary>The destruction kit draws Break events as material bursts (NativeStructureFxPresentation): skip the source's
        /// 18-spark break burst then.</summary>
        public bool MaterialBursts;
        private readonly NativeImpactCommand[] plan=new NativeImpactCommand[NativeImpactCues.MaxCommands];
        private readonly NativeSpikeRises spikeRises=new NativeSpikeRises();
        private const float FloorLift=.05f,FloorReach=40;private readonly RaycastHit[] floorHits=new RaycastHit[16];private PhysicsScene physics;private bool world;
        private int generation=-1,lastEvent;private float lastTime=-1;
        /// <summary>vfx: the shared effects renderer the owner (ArenaPresentation) begins and submits each frame; null makes
        /// this presentation own one and submit it at the end of its own Render.</summary>
        public NativeBlastPresentation(Transform parent,NativeVfxDraw vfx=null)
        {
            ownsVfx=vfx==null;this.vfx=vfx??new NativeVfxDraw();
            root=new GameObject("Native blast light pool");root.transform.SetParent(parent,false);
            for(int i=0;i<lights.Length;i++){var go=new GameObject("Blast flash "+i);go.transform.SetParent(root.transform,false);var light=go.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;light.cullingMask=~(1<<10|1<<NativeSceneryBuilder.BackdropLayer);light.enabled=false;lights[i]=light;}
        }
        public void Clear()
        {
            sim.Clear();seen.Clear();seenBefore.Clear();spikeRises.Clear();sites.Clear();sitesBefore.Clear();shots.Clear();shotsBefore.Clear();sirens.Clear();sirensBefore.Clear();
            lastEvent=0;lastTime=-1;if(ownsVfx)vfx.Clear();foreach(var light in lights)if(light!=null)light.enabled=false;
        }
        private static long Mix(long h,int v){unchecked{h^=(uint)v;h*=0x100000001b3L;return h;}}
        private static int Bits(float v)=>BitConverter.SingleToInt32Bits(v);
        private static long Key(NativeAbilityFx fx)
        {long h=unchecked((long)0xcbf29ce484222325UL);h=Mix(h,Bits(fx.Until));h=Mix(h,Bits(fx.Position.x));h=Mix(h,Bits(fx.Position.y));h=Mix(h,Bits(fx.Position.z));h=Mix(h,Bits(fx.Radius));return Mix(h,fx.Element*2+(fx.Beam?1:0));}
        private static bool Explodes(string kind)=>kind=="bomb"||kind=="mine"||kind=="implosion";
        private static string Fx(ContentAbility d)=>string.IsNullOrEmpty(d.fx)?null:d.fx;
        private static string Site(List<Marker> list,Vector3 at){foreach(var s in list)if((s.Position-at).sqrMagnitude<.0025f)return s.Tag;return null;}
        private static void Nearest(List<Marker> list,Vector3 at,ref float best,ref string tag){foreach(var s in list){float d=(s.Position-at).sqrMagnitude;if(d<s.Reach*s.Reach&&d<best){best=d;tag=s.Tag;}}}
        // The source tag of the field, zone or ability shot that ended at this effect, this frame or the frame before.
        private string TagAt(Vector3 at)
        {
            // Fields and zones detonate at their position lifted .15 m (NativeChips.StepChipFields, NativeAbilities.StepAbilityWorld).
            string tag=Site(sites,at)??Site(sitesBefore,at);if(tag!=null)return tag;
            float best=float.MaxValue;Nearest(shots,at,ref best,ref tag);Nearest(shotsBefore,at,ref best,ref tag);return tag;
        }
        private void Remember(NativeArena arena,NativeFieldPresentation fields)
        {
            var swap=sitesBefore;sitesBefore=sites;sites=swap;sites.Clear();swap=shotsBefore;shotsBefore=shots;shots=swap;shots.Clear();
            foreach(var z in arena.AbilityZones)if(z.Definition!=null)sites.Add(new Marker{Position=z.Position+Vector3.up*.15f,Tag=Fx(z.Definition)});
            // ChipField.Effect (the chip id the source keys styles on) exists on the host only; guests recover it from the field's
            // Field event (NativeFieldPresentation.Fx) and fall back to the kind.
            foreach(var f in arena.ChipFields)if(Explodes(f.Kind))sites.Add(new Marker{Position=f.Position+Vector3.up*.15f,Tag=fields?.Fx(f)??(string.IsNullOrEmpty(f.Effect)?f.Kind:f.Effect)});
            foreach(var s in arena.AbilityShots)if(s.Definition!=null)shots.Add(new Marker{Position=s.Position,Tag=Fx(s.Definition),Reach=Math.Max(1,s.Velocity.magnitude*.1f)+s.Radius});
            // Where the Siren's attacks record their darkness effects (NativeSiren SirenArea and StepSirenShot, NativeSirenStage).
            var swapSirens=sirensBefore;sirensBefore=sirens;sirens=swapSirens;sirens.Clear();sirenSeats.Clear();
            foreach(var p in arena.Pilots)if(p.Seat.spec=="siren"){sirenSeats.Add(p.Seat.id);Vector3 at=p.Body.position;sirens.Add(new NativeBlastFrame.SirenSite{X=at.x,Y=at.y,Z=at.z,Reach=.6,Below=true});}
            if(sirenSeats.Count>0)foreach(var b in arena.Bullets)if(sirenSeats.Contains(b.Owner))sirens.Add(new NativeBlastFrame.SirenSite{X=b.Position.x,Y=b.Position.y,Z=b.Position.z,Reach=Math.Max(1,b.Velocity.magnitude*.1f)+b.Radius});
            foreach(var f in arena.SirenFields)sirens.Add(new NativeBlastFrame.SirenSite{X=f.Position.x,Y=f.Position.y,Z=f.Position.z,Reach=NativeBlastFrame.SameSpot});
            if(arena.MapId=="siren")foreach(var v in NativeArena.StageVents)sirens.Add(new NativeBlastFrame.SirenSite{X=v.x,Y=v.y,Z=v.z,Reach=NativeBlastFrame.SameSpot});
        }
        private bool AtSiren(Vector3 at)=>NativeBlastFrame.AtSiren(sirens,at.x,at.y,at.z)||NativeBlastFrame.AtSiren(sirensBefore,at.x,at.y,at.z);
        // The authoritative event the match emitted with this record: ChipBlast and AbilityBlast emit an Explosion, Vacuum() a
        // Vacuum, from the same Vector3 and radius in the same tick (fx.Until is that tick + .22 s). Host, guests (the snapshot
        // carries the event buffer) and the killcam replay all hold it. Newest first; the buffer is in time order.
        private static bool ExplosionAt(NativeArena arena,NativeAbilityFx fx,out string detail)
        {
            detail=null;var events=arena.Events;float born=fx.Until-.22f;const float same=(float)(NativeBlastFrame.SameSpot*NativeBlastFrame.SameSpot);
            for(int i=events.Count-1;i>=0;i--)
            {
                var e=events[i];if(e.Time<born-.05f)break;
                if(e.Kind!=EventKind.Explosion&&e.Kind!=EventKind.Vacuum||e.Time>born+.05f||Math.Abs(e.Radius-fx.Radius)>1e-3f)continue;
                float dx=e.Position.x-fx.Position.x,dy=e.Position.y-fx.Position.y,dz=e.Position.z-fx.Position.z;
                if(dx*dx+dy*dy+dz*dz<same){detail=e.Detail;return true;}
            }
            return false;
        }
        private static bool WeaponSpec(NativeArena arena,string id)
        {var weapons=arena.Content?.weapons;if(weapons==null||string.IsNullOrEmpty(id))return false;for(int i=0;i<weapons.Length;i++)if(weapons[i].id==id)return true;return false;}
        private void Effects(NativeArena arena,float now,double quality)
        {
            var swap=seenBefore;seenBefore=seen;seen=swap;seen.Clear();
            landings.Clear();fresh.Clear();freshFx.Clear();
            foreach(var fx in arena.AbilityEffects)if(fx.Beam&&NativeBlastCues.Classify(true,fx.Radius,fx.Element,null)==NativeBlastCues.Cue.Teleport)landings.Add(fx.Position);
            foreach(var fx in arena.AbilityEffects)
            {
                // A second record with the same key in one frame is the same event recorded twice (Blink Strike: ChipBlink, then Blink).
                long key=Key(fx);if(!seen.Add(key))continue;
                if(seenBefore.Contains(key))continue;
                if(now-(fx.Until-.22f)>.4f)continue;
                // A beam record that stands for a source event with no Unity event: the L33t possession and Slenderman hop
                // (green burst, the hop's streak) and the toy soldier's shot line. The first port's cube bars are gone.
                if(fx.Beam){int count=NativeImpactCues.Record(fx.Element,fx.Radius,NativeArena.Vec(fx.Position),NativeArena.Vec(fx.From),Hop(arena,fx.From),plan);Run(count,quality);}
                bool siren=!fx.Beam&&fx.Element==4&&AtSiren(fx.Position);
                // The source look: the Explosion event's message, else a teleport landing or the field, zone or ability shot
                // that ended here. A record with neither is not one of the source's blasts (NativeBlastCues.Classify).
                string tag=null;bool explosion=false;double q=1;
                if(!fx.Beam&&!siren)
                {
                    if(ExplosionAt(arena,fx,out string detail)){explosion=true;tag=NativeBlastCues.ExplosionTag(detail,fx.Radius,WeaponSpec(arena,detail),out q);}
                    else{foreach(var p in landings)if((p-fx.Position).sqrMagnitude<.0025f){tag=NativeBlastCues.Colocated(fx.Element);break;}tag??=TagAt(fx.Position);q=NativeBlastCues.Quality(tag);}
                }
                var cue=NativeBlastCues.Classify(fx.Beam,fx.Radius,fx.Element,tag,siren,explosion);
                if(cue==NativeBlastCues.Cue.None)continue;
                fresh.Add(new NativeBlastFrame.Candidate{X=fx.Position.x,Y=fx.Position.y,Z=fx.Position.z,Radius=fx.Radius,Quality=q,Tag=tag,Cue=cue});freshFx.Add(fx);
            }
            NativeBlastFrame.Collapse(fresh);
            for(int i=0;i<fresh.Count;i++)
            {
                var fx=freshFx[i];string tag=fresh[i].Tag;Vector3 at=fx.Position;
                switch(fresh[i].Cue)
                {
                    case NativeBlastCues.Cue.Blast:sim.Blast(NativeBlastCues.Style(tag),at.x,at.y,at.z,fx.Radius,quality*fresh[i].Quality,false,NativeBlastSim.TintOf(tag),FloorUnder(at));break;
                    case NativeBlastCues.Cue.Vacuum:sim.Blast(NativeBlastStyle.Void,at.x,at.y,at.z,fx.Radius,quality,false,null,FloorUnder(at));break;
                    case NativeBlastCues.Cue.Dud:{int count=NativeImpactCues.Dud(NativeArena.Vec(at),plan);Run(count,quality);break;}
                    case NativeBlastCues.Cue.Teleport:
                    {
                        Vector3 from=fx.From;
                        sim.Blast(NativeBlastStyle.Void,at.x,at.y,at.z,NativeBlastCues.TeleportRadius,quality*NativeBlastCues.TeleportQuality,false,null,FloorUnder(at));
                        sim.Impact(from.x,from.y,from.z,NativeBlastCues.TeleportImpactRadius,NativeBlastCues.TeleportColor,quality,false,false,null,FloorUnder(from));
                        sim.Line(from.x,from.y,from.z,at.x,at.y,at.z,0xffffff,NativeBlastCues.TeleportColor,quality,NativeBlastCues.TeleportLineJitter,NativeBlastCues.TeleportLineLife,NativeBlastCues.TeleportLineSweep);break;
                    }
                }
            }
        }
        // The floor under a blast or impact; null (the source's y - .95) without a physics scene.
        private double? FloorUnder(Vector3 at)=>world?FloorUnder(physics,at,floorHits):(double?)null;
        /// <summary>The arena's physics scene; false when it has none (nothing for FloorUnder to cast against).</summary>
        internal static bool World(NativeArena arena,out PhysicsScene physics)
        {physics=arena.Root!=null?arena.Root.scene.GetPhysicsScene():default;return arena.Root!=null&&physics.IsValid();}
        /// <summary>The floor under a point for NativeBlastSim (blasts, impacts, field views): the nearest upward-facing world
        /// surface straight below it, cast from just above it so a hit on the floor finds that floor (a wall face or a ceiling
        /// above is never taken). NativeBlastSim.NoFloor over a pit or the sky. hits: a reused buffer.</summary>
        internal static double FloorUnder(PhysicsScene physics,Vector3 at,RaycastHit[] hits)
        {
            int n=physics.Raycast(at+Vector3.up*FloorLift,Vector3.down,hits,FloorReach+FloorLift,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);
            float best=float.MaxValue;double floor=NativeBlastSim.NoFloor;
            for(int i=0;i<n&&i<hits.Length;i++){var h=hits[i];if(h.normal.y>.5f&&h.distance>1e-3f&&h.distance<best){best=h.distance;floor=h.point.y;}}
            return floor;
        }
        // Runs a plan of source draws; its impacts first get the floor under them.
        private void Run(int count,double quality)
        {
            for(int i=0;i<count&&i<plan.Length;i++)if(plan[i].Op==NativeImpactOp.Impact)plan[i].Floor=FloorUnder(NativeArena.Vec(plan[i].A));
            NativeImpactCues.Run(plan,count,sim,Sparks,quality);
        }
        // Slenderman's hop record starts where Slender stood: the tree's From (host and replicated alike).
        private static bool Hop(NativeArena arena,Vector3 from)
        {const float same=(float)(NativeBlastFrame.SameSpot*NativeBlastFrame.SameSpot);foreach(var f in arena.LeetFields)if(f.Kind=="tree"&&(f.From-from).sqrMagnitude<same)return true;return false;}
        // event-vfx.ts event() for every fresh event (NativeImpactCues.Plan): the hit/block/break/impact/collapse sparks and
        // impact streaks, proc impacts, summon landings, pong bounces and rallies (PR #88: Detail 'rally', Target, From = the
        // bounce), stage hazard sparks, L33t bursts and streaks and arc lines. Then the surface impact procs: a water pilot's
        // splash and the kinetic and demolition chips.
        private void Impacts(NativeArena arena,float now,double quality)
        {
            foreach(var e in arena.Events)
            {
                if(e.Id<=lastEvent)continue;lastEvent=e.Id;if(now-e.Time>.4f||e.Kind==EventKind.Break&&MaterialBursts)continue;
                // A Block is the block effect's clash or deflect (NativeBlockFxPresentation), not event-vfx.ts's 6-spark blockHit burst.
                if(e.Kind==EventKind.Block)continue;
                NativePilot source=null,target=null;foreach(var p in arena.Pilots){if(p.Seat.id==e.Source)source=p;if(p.Seat.id==e.Target)target=p;}
                // The colour the source event carries where Unity's has none: a break's material (none for the Siren's seeker
                // breaks), an impact's weapon colour.
                int color=e.Color;
                if(color==0&&e.Kind==EventKind.Break&&source?.Seat.spec!="siren")color=NativeImpactCues.MaterialColor(arena.Content?.materials,e.Detail);
                else if(color==0&&e.Kind==EventKind.Impact){var w=NativeShotViews.Weapon(arena,source?.Seat.spec);if(w!=null)color=(int)w.color;}
                var cue=new NativeImpactEvent{Kind=e.Kind,Detail=e.Detail,Position=e.Position,From=e.From,Amount=e.Amount,Radius=e.Radius,Color=color,TargetPilot=target!=null,Trails=Trails,HasTarget=target!=null,Target=target!=null?NativeArena.Vec(target.Body.position):default};
                int count=NativeImpactCues.Plan(cue,plan);Run(count,quality);
                if(e.Kind!=EventKind.Impact||e.Source<=0)continue;
                double x=e.Position.x,y=e.Position.y,z=e.Position.z;
                NativeSeat seat=null;foreach(var s in arena.Match.Seats)if(s.id==e.Source){seat=s;break;}if(seat==null)continue;
                bool water=seat.spec=="water",kinetic=seat.chips.TryGetValue("kinetic",out int kinetics)&&kinetics>0,demolition=seat.chips.TryGetValue("demolition",out int demolitions)&&demolitions>0;
                double? floor=water||kinetic||demolition?FloorUnder(NativeArena.Vec(e.Position)):null;
                if(water)sim.Impact(x,y,z,.6,0x8de9ee,quality,false,false,null,floor);
                if(kinetic)sim.Impact(x,y,z,.65+Math.Min(3,kinetics)*.25,0xffaa64,quality,false,false,null,floor);
                if(demolition)sim.Impact(x,y,z,.5+.12*demolitions,0xffd45f,quality,false,false,null,floor);
            }
        }
        // stage-spikes-rise (stage-hazards.ts:21): Unity's hazards emit no rise event, so it is derived from the same clock
        // NativeAudio plays its cue from, and bursts the bed's dust.
        private void SpikeRises(NativeArena arena,double quality)
        {
            var match=arena.Match;if(match==null)return;
            int rose=spikeRises.Step(arena.MapId,match.Phase==NativePhase.Fight&&match.Time-match.FightAt>=2,match.Time);
            for(int bed=0;rose!=0&&bed<NativeSpikeRises.Beds;bed++)if((rose&1<<bed)!=0){int count=NativeImpactCues.SpikeRise(NativeSpikeRises.Position(bed),plan);Run(count,quality);}
        }
        // The field views (chip fields and ability zones: key, fx, birth, the raycast floor under them) are built once per frame
        // by NativeFieldPresentation, which draws their glow, cores and domes from the same views. The emitters run on the views'
        // clock (fields.Clock: continuous on guests), so births and emission stay in step with the telegraphs; the arrival and
        // Orbital Mark lock impacts sit on the view's floor.
        private void Fields(NativeFieldPresentation fields,double quality)
        {
            if(fields==null)return;var views=fields.Views;double now=fields.Clock;
            for(int i=0;i<views.Count;i++)sim.Field(views[i],now,quality);
            sim.Prune(fields.Live);
        }
        /// <summary>now is the arena presentation clock (record ages, field emitters, rewinds). paused: the match itself is
        /// stopped (offline menu); the blasts then hold still, otherwise they advance with the render frame as in the source.
        /// fields: this frame's field views and their clock (NativeFieldPresentation.Collect already ran).</summary>
        public void Render(NativeArena arena,Camera camera,float now,float quality,bool paused,NativeFieldPresentation fields)
        {
            // A rewind (killcam, new round) clears.
            if(generation!=arena.Generation||now<lastTime-.25f){Clear();generation=arena.Generation;}
            lastTime=Math.Max(lastTime,now);double dt=NativeBlastFrame.Step(Time.unscaledDeltaTime,paused);
            world=World(arena,out physics);
            double q=Math.Max(0,Math.Min(1,quality));
            // Source renderer order: event blasts, then fields(state), then update(dt), then draw.
            Remember(arena,fields);Effects(arena,now,q);Impacts(arena,now,q);SpikeRises(arena,q);Fields(fields,q);sim.Update(dt);
            // Every particle as its role's primitive and every front as one shell (NativeVfxDraw); no cube per voxel.
            if(ownsVfx)vfx.Begin();vfx.Blasts(sim);if(ownsVfx)vfx.Submit(camera);
            for(int i=0;i<lights.Length;i++)
            {
                var f=sim.Flashes[i];var light=lights[i];float intensity=NativeBlastSim.UnityLightIntensity(f.Intensity);light.enabled=intensity>.001f;if(!light.enabled)continue;
                light.transform.position=new Vector3((float)f.X,(float)f.Y,(float)f.Z);light.color=NativeArena.ColorOf(f.Color);light.intensity=intensity;light.range=(float)f.Distance;
            }
            // NativeBlockLit has no add pass, so these point lights never reach dressed arena blocks; they take the flashes through its feed.
            NativeArenaDressing.Flashes(sim.Flashes);
        }
        public void Dispose(){Clear();if(ownsVfx)vfx.Dispose();CapturedAnimation.Release(root);}
    }
}
