using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        /// <summary>Where the authority stopped a projectile (its replicated stopping point and time), for the presentation only.</summary>
        public bool ShotEndOf(int id,out NativeShotEnd end)=>shotEnds.Find(id,out end);
        /// <summary>The replicated stopping points (presentation only).</summary>
        public NativeShotEnds ShotEnds=>shotEnds;
    }

    // The gun rounds' and arrows' tracers in the arena (Core/NativeTracers.cs): each round's drawn path (its replicated place on the
    // presentation clock, the muzzle first), the needle along it, its short-lived line, and the end of it: where the authority stopped
    // the round (the replicated stopping point) its head stops, its tail catches up, and what it struck answers: a body's or a prop's own
    // event, or (the map's static colliders raise no event) the presentation's own cast along its last heading. Nothing here writes
    // the match. Hit timing, hitboxes, damage, cooldowns, movement and the network frame are untouched.
    public sealed partial class ArenaPresentation
    {
        private struct LingerLine{public long Key;public Vector3 A,B;public float BornA,BornB,Life,Width,Pixels;public Color Color;}
        private struct RoundMark{public Vector3 At,Normal,Flight;public float Born;public long Seed;public NativeRoundImpact Kind;}
        private const int LingerLines=160,RoundMarks=48,RoundFlights=32;
        // Each drawn hitscan shot's and unseen round's heading, for the landing its own event makes this frame.
        private readonly int[] flightIds=new int[RoundFlights];private readonly Vector3[] flightDirs=new Vector3[RoundFlights];private int flightNext;
        // The rounds a tracer has been made for (live, hitscan or reconstructed), and how far the Hitscan events have been read.
        private readonly HashSet<int> roundDrawn=new HashSet<int>();private int hitscanEvent;
        private readonly LingerLine[] lingers=new LingerLine[LingerLines];private int lingerNext;
        private readonly RoundMark[] roundMarks=new RoundMark[RoundMarks];private int roundMarkNext;
        private readonly NVector3[] tracerWalk=new NVector3[NativeTracerPath.Capacity+1];private readonly Vector3[] tracerPoints=new Vector3[NativeTracerPath.Capacity+1];private readonly float[] tracerAlong=new float[NativeTracerPath.Capacity+1];
        private readonly HashSet<int> roundHits=new HashSet<int>();private int roundHitEvent;private NativeArena roundHitArena;private int roundHitGeneration=-1;
        private int tracerSheaths,tracerHeads,roundRays;private float tracerPixel;private Vector3 tracerEye;private NativeSeat tracerViewer;
        private const int RoundRaysPerFrame=16;
        /// <summary>How close (m) a scoped pilot's own round is drawn: the scope shows it leaving, not the streak past the eye.</summary>
        private const float ScopedClear=9;

        // ---- each frame ----
        private void TracersBegin(NativeArena arena,Camera camera)
        {
            tracerViewer=ViewSeat(arena);tracerSheaths=tracerHeads=roundRays=0;tracerEye=camera!=null?camera.transform.position:Vector3.zero;tracerPixel=camera!=null&&camera.pixelHeight>0?2*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)/camera.pixelHeight:.001f;
            // The rounds an event has already landed (a body's or a prop's): its own effects answer them.
            if(!ReferenceEquals(roundHitArena,arena)||roundHitGeneration!=arena.Generation){roundHits.Clear();roundHitEvent=0;roundHitArena=arena;roundHitGeneration=arena.Generation;roundDrawn.Clear();hitscanEvent=0;}
            int newest=0;foreach(var e in arena.Events)newest=Math.Max(newest,e.Id);if(newest<roundHitEvent)roundHitEvent=0;
            if(roundHits.Count>2048)roundHits.Clear();
            foreach(var e in arena.Events)if(e.Id>roundHitEvent&&e.Projectile>0&&(e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot||e.Kind==EventKind.Impact))roundHits.Add(e.Projectile);
            roundHitEvent=Math.Max(roundHitEvent,newest);
            if(hitscanEvent>newest)hitscanEvent=0;if(roundDrawn.Count>4096)roundDrawn.Clear();
        }
        private void FlightAdd(int id,Vector3 dir){flightIds[flightNext]=id;flightDirs[flightNext]=dir;flightNext=(flightNext+1)%RoundFlights;}
        private bool FlightOf(int id,out Vector3 dir){for(int i=0;i<RoundFlights;i++)if(flightIds[i]==id&&id!=0){dir=flightDirs[i];return true;}dir=default;return false;}
        // A drawn line for a shot with no body to follow: its hot line and sheath in its look, and the look's longer line where it has one.
        private void ShotLine(in NativeTracerStyle s,long key,Vector3 a,Vector3 b,float born,bool trails)
        {
            float lead=NativeTracers.LineLead;
            LineAdd(key,a,b,born-lead*.6f,born,NativeTracers.FlashLife,s.Core,NativeTracers.CorePixels,new Color(s.CoreColor.R*s.CoreGain,s.CoreColor.G*s.CoreGain,s.CoreColor.B*s.CoreGain,0));
            if(!trails)return;
            if(s.Sheath>0)LineAdd(key,a,b,born-lead,born,NativeTracers.SheathLife,s.Sheath,NativeTracers.SheathPixels,new Color(s.Edge.R*s.SheathGain,s.Edge.G*s.SheathGain,s.Edge.B*s.SheathGain,0));
            if(s.LingerLife>0)LineAdd(key,a,b,born-lead,born,s.LingerLife,s.LingerWidth,1.2f,new Color(s.LingerColor.R*s.LingerGain,s.LingerColor.G*s.LingerGain,s.LingerColor.B*s.LingerGain,0));
        }
        private void LineAdd(long key,Vector3 a,Vector3 b,float bornA,float bornB,float life,float width,float pixels,Color color)
        {
            lingers[lingerNext]=new LingerLine{Key=key,A=a,B=b,BornA=bornA,BornB=bornB,Life=life,Width=width,Pixels=pixels,Color=color};lingerNext=(lingerNext+1)%LingerLines;
        }
        // Whose round this is, as a tracer sees it: its look's inputs from the firer's seat (the firer's chips, the frame's viewer).
        private NativeTracerStyle ShotStyle(NativeArena arena,string spec,int firer,int id,float speed,float q,bool low)
        {
            var seat=NativeShotViews.Seat(arena,firer);var w=NativeShotViews.Weapon(arena,spec);var mods=NativeShotViews.Modules(arena,seat?.chips,shotTracker.ModuleCache);
            Actor owner=null;var actors=weaponSnapshot?.Actors;if(actors!=null)for(int i=0;i<actors.Length;i++)if(actors[i]!=null&&actors[i].Id==firer){owner=actors[i];break;}
            float base0=w!=null?(float)w.speed:0;return NativeTracers.Of(spec,NativeTracerLook.Of(owner,kitClock,spec,kitClock),id,speed>0?speed:base0,base0,mods,false,false,NativeTracers.Side(tracerViewer,seat,arena.Match.Teams,arena.Match.Cooperative),q,low);
        }
        // A world collider a shot ended on raises no event: its own cast along the shot's last heading, and the gun's landing there.
        private void WorldLanding(NativeArena arena,NativeRoundImpact kind,string spec,int id,Vector3 end,Vector3 dir,float reach,float born)
        {
            if(kind==NativeRoundImpact.None||roundRays>=RoundRaysPerFrame*2)return;roundRays++;
            if(!arena.PhysicsWorld.Raycast(end-dir*.12f,dir,out var hit,reach+.12f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))return;
            RoundMarkAdd(kind,hit.point,hit.normal,dir,born,id*2654435761L+37);SurfaceWorldHit(arena,hit.point,hit.normal,id,born,NativeRoundImpacts.Debris(spec,id));
        }
        private static bool LandedOnEvent(NativeArena arena,int id,Vector3 end)
        {
            var events=arena.Events;
            for(int i=events.Count-1;i>=0;i--){var e=events[i];if(e.Projectile!=id||e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot&&e.Kind!=EventKind.Impact)continue;if((NativeArena.Vec(e.Position)-end).sqrMagnitude<4)return true;}
            return false;
        }
        /// <summary>
        /// The instant rifle (docs/PROJECTILE-REWORK.md): every fresh Hitscan event is a straight segment of a shot at its authority time, once by its
        /// event id: a hit, a miss or a ricochet's leg. The shot's first leg leaves the firer's drawn muzzle (a scoped pilot's own, the scope's clear
        /// distance on), a later leg throws the ricochet's fan at its corner, the last lands on the world when no event landed it.
        /// </summary>
        private void HitscanFx(NativeArena arena,float now,float q,bool low,bool trails)
        {
            var events=arena.Events;int newest=hitscanEvent;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=hitscanEvent)continue;newest=Math.Max(newest,e.Id);if(e.Kind!=EventKind.Hitscan||now-e.Time>NativeTracers.LineStale)continue;
                Vector3 a=NativeArena.Vec(e.From),b=NativeArena.Vec(e.Position),line=b-a;float length=line.magnitude;if(!(length>.01f))continue;var dir=line/length;
                bool again=FlightOf(e.Projectile,out var before);
                if(again&&Vector3.Dot(before,dir)<NativeTracers.TurnCos)RoundMarkAdd(NativeRoundImpact.Ricochet,a,NormalOf(before,dir),before,e.Time,e.Id*1000003L+23);
                bool scoped=ScopeActive&&e.Source==LocalId;
                if(!again)
                {
                    if(scoped){float clear=Vector3.Dot(a-tracerEye,dir);a+=dir*Mathf.Max(0,ScopedClear-clear);if(Vector3.Dot(b-a,dir)<=0)a=b;}
                    else if(shotTracker.Muzzles.TryGetValue(e.Source,out var m)&&(TU(m)-a).sqrMagnitude<9)a=TU(m);
                }
                FlightAdd(e.Projectile,dir);roundDrawn.Add(e.Projectile);
                if((b-a).sqrMagnitude>.01f){var s=ShotStyle(arena,e.Detail,e.Source,e.Projectile,0,q,low);ShotLine(s,(long)e.Id<<20,a,b,e.Time,trails);}
                bool last=true;for(int k=i+1;k<events.Count;k++)if(events[k].Kind==EventKind.Hitscan&&events[k].Projectile==e.Projectile){last=false;break;}
                if(last&&!LandedOnEvent(arena,e.Projectile,b))WorldLanding(arena,NativeRoundImpacts.Gun(e.Detail),e.Detail,e.Projectile,b,dir,e.Radius+.45f,e.Time);
            }
            hitscanEvent=newest;
        }
        /// <summary>
        /// A round that came and went between frames (a fast round at close range, more so on a guest's 20 Hz frames) was never in a frame's shots: its
        /// stopping point is. Its firer is the event that landed it or, on the world, the newest Shot that can have reached there; it is drawn as a
        /// line from that firer's drawn muzzle to the stopping point at its authority time. Only the guns' rounds.
        /// </summary>
        private void RoundsUnseen(NativeArena arena,float now,float q,bool low,bool trails)
        {
            var ends=arena.ShotEnds;var events=arena.Events;
            for(int n=ends.Count-1;n>=0;n--)
            {
                var end=ends[n];if(now-end.At>NativeTracers.LineStale)break;if(roundDrawn.Contains(end.Id))continue;roundDrawn.Add(end.Id);
                Vector3 at=NativeArena.Vec(end.Position);int firer=0;string spec=null;bool landed=false;var start=Vector3.zero;bool hasStart=false;
                for(int i=events.Count-1;i>=0;i--)
                {
                    var e=events[i];if(e.Projectile!=end.Id)continue;
                    if(e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot||e.Kind==EventKind.Impact){firer=e.Source;landed=true;break;}
                }
                float bestAge=float.MaxValue;
                for(int i=events.Count-1;i>=0;i--)
                {
                    var e=events[i];if(e.Kind!=EventKind.Shot||firer!=0&&e.Source!=firer)continue;float age=end.At-e.Time;if(!(age>=-.02f&&age<bestAge))continue;
                    var seat=NativeShotViews.Seat(arena,e.Source);if(seat==null||NativeRoundImpacts.Gun(seat.spec)==NativeRoundImpact.None)continue;var w=NativeShotViews.Weapon(arena,seat.spec);
                    var from=NativeArena.Vec(e.Position);if(!NativeTracers.Reached((at-from).magnitude,age,w!=null?(float)w.speed:100))continue;
                    bestAge=age;firer=e.Source;spec=seat.spec;start=from;hasStart=true;
                }
                if(!hasStart)continue;
                if(shotTracker.Muzzles.TryGetValue(firer,out var m)&&(TU(m)-start).sqrMagnitude<9)start=TU(m);
                var line=at-start;float length=line.magnitude;if(!(length>.05f))continue;var dir=line/length;FlightAdd(end.Id,dir);
                if(ScopeActive&&firer==LocalId)continue;
                var look=ShotStyle(arena,spec,firer,end.Id,0,q,low);ShotLine(look,(long)end.Id<<20,start,at,end.At,trails);
                if(!landed)WorldLanding(arena,NativeRoundImpacts.Gun(spec),spec,end.Id,at,dir,.5f,end.At);
            }
        }
        // The seat the frame is seen from (arena.ViewPilot, without its Find's closures).
        private static NativeSeat ViewSeat(NativeArena arena)
        {
            var pilots=arena.Pilots;for(int i=0;i<pilots.Count;i++){var p=pilots[i];if(p.Seat.id==arena.LocalId)return (p.Controlling??p).Seat;}return null;
        }
        /// <summary>One gun round or arrow this frame (a first sight fixes its look and its path's start), or any other shot (ShotFx).</summary>
        private void RoundFx(NativeWeaponFxDraw draw,NativeArena arena,NativeBullet b,in NativeShotView v,float now,float q,bool low,bool trails,Vector3 eye)
        {
            bool arrow=NativeBowWire.Is(v.Spec);if(!arrow&&!NativeTracerFx.Traced(v)){ShotFx(draw,v,now,q,low,trails,eye);return;}
            long key=NativeShotScene.Key(v.Kind,v.Id);var at=new Vector3((float)v.X,(float)v.Y,(float)v.Z);var velocity=new Vector3((float)v.VX,(float)v.VY,(float)v.VZ);
            if(!emitters.TryGetValue(key,out var e)){e=spareEmitters.Count>0?spareEmitters.Pop():new Emitter();RoundBegin(e,arena,b,v,at,velocity,arrow,now,q,low);emitters.Add(key,e);}
            // A guest past its newest frame carries a round on by its velocity (NativeShotTracker.Present): never through a wall.
            if(arena.Replica&&!arena.ReplaySampledShots&&now>arena.Time&&roundRays<RoundRaysPerFrame){var step=at-e.Head;float reach=step.magnitude;if(reach>.01f){roundRays++;if(arena.PhysicsWorld.Raycast(e.Head,step/reach,out var wall,reach,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))at=wall.point;}}
            e.Frame=weaponFrame;e.Spent=false;RoundStep(e,key,v,at,velocity,b.Velocity,now,q,low);RoundDraw(draw,e,eye,now,trails,1,velocity.magnitude);
        }
        private void RoundBegin(Emitter e,NativeArena arena,NativeBullet b,in NativeShotView v,Vector3 at,Vector3 velocity,bool arrow,float now,float q,bool low)
        {
            e.Path.Clear();e.Origin=e.Last=e.Head=at;e.LastAt=now;e.Carry=0;e.Emitted=0;e.Count=0;e.Id=v.Id;e.Round=true;e.Bolt=false;e.Spent=false;e.Arrived=false;e.Boost=-10;e.Spec=v.Spec;e.Chips=v.Modules;e.Fire=v.Fire;e.Poison=v.Poison;e.Key=NativeShotScene.Key(v.Kind,v.Id);
            float speed=velocity.magnitude;e.Dir=speed>.01f?velocity/speed:Vector3.forward;e.Speed=speed;e.FirstDir=b.Velocity.sqrMagnitude>.01f?b.Velocity.normalized:e.Dir;e.Straight=true;
            int firer=b.Original!=0?b.Original:b.Owner;var side=NativeTracers.Side(tracerViewer,NativeShotViews.Seat(arena,firer),arena.Match.Teams,arena.Match.Cooperative);
            e.Scoped=ScopeActive&&firer==LocalId;roundDrawn.Add(v.Id);
            if(arrow)
            {
                float d;NativeBow.Arrow kind;if(!NativeBowWire.Arrow(b.Detail,b.Draw,out kind,out d)){if(bowShots.TryGetValue(b.Id,out var shot)){d=shot.draw;kind=shot.kind;}else{d=NativeBowWire.SpeedDraw(b.BaseVelocity.sqrMagnitude>.01f?b.BaseVelocity.magnitude:speed);kind=d>=.999f?NativeBow.Arrow.Full:NativeBow.Arrow.Quick;}}
                e.Style=NativeTracers.Arrow(d,kind,side,q,low);e.Impact=NativeRoundImpact.None;
            }
            else
            {
                var w=NativeShotViews.Weapon(arena,v.Spec);e.Style=NativeTracers.Of(v.Spec,GunKitTracer(b,v),v.Id,speed,w!=null?(float)w.speed:0,v.Modules,v.Fire,v.Poison,side,q,low);e.Impact=NativeRoundImpacts.Gun(v.Spec);
                // A split fragment's tail starts where the round split (its Split event), so the fork shows.
                if(v.Modules.Split>0)foreach(var ev in arena.Events)if(ev.Kind==EventKind.Split&&ev.Source==firer&&Mathf.Abs(ev.Time-now)<.2f&&(NativeArena.Vec(ev.Position)-at).sqrMagnitude<4){e.Path.Push(TN(NativeArena.Vec(ev.Position)));e.Boost=now;break;}
            }
            // First person: the tail runs from far off back to the eye's own barrel, nearly all its screen length in its last metres, so it falls off slower.
            if(!arrow&&FirstPerson&&firer==LocalId)NativeTracers.Own(ref e.Style);
            // The round leaves the muzzle it was drawn from (a first sight a tick past it still reaches back to it).
            if(v.Launched&&e.Path.Count==0&&shotTracker.Muzzles.TryGetValue(firer,out var m)){var from=new Vector3(m.X,m.Y,m.Z);e.Path.Push(TN(from));e.Head=from;}
        }
        private static NVector3 TN(Vector3 v)=>new NVector3(v.x,v.y,v.z);
        private static Vector3 TU(NVector3 v)=>new Vector3(v.X,v.Y,v.Z);
        // The path follows the drawn round; a corner (a bounce, a reflect) goes in where its old and new lines meet.
        private void RoundStep(Emitter e,long key,in NativeShotView v,Vector3 at,Vector3 velocity,Vector3 simulated,float now,float q,bool low)
        {
            float speed=velocity.magnitude;var last=e.Head;
            if(speed>.01f)
            {
                var d=velocity/speed;
                if(e.Path.Count>0&&Vector3.Dot(d,e.Dir)<NativeTracers.TurnCos)
                {
                    var corner=NativeTracers.Corner(TN(e.Head),TN(e.Dir),TN(at),TN(d));e.Path.Push(corner);e.Boost=now;e.Straight=false;
                    if(e.Chips.Bounces>0||e.Chips.Pong>0)RoundMarkAdd(NativeRoundImpact.Ricochet,TU(corner),NormalOf(e.Dir,d),e.Dir,now,e.Key*2654435761L+(long)(now*4096));
                    LingerTo(e,key,last,TU(corner),now);last=TU(corner);
                }
                // While it holds its first heading a round's tail is one straight line from where it left to where it is: the muzzle's offset never shows as a kink.
                // (Its simulated heading: the drawn one bends for a moment as the round joins its path from the muzzle.)
                if(e.Straight&&simulated.sqrMagnitude>.01f&&Vector3.Dot(simulated.normalized,e.FirstDir)<NativeTracers.StraightCos)e.Straight=false;
                e.Dir=d;e.Speed=speed;
            }
            if(e.Path.Count>0&&(e.Path.Newest-TN(at)).LengthSquared()<4e-4f||e.Straight&&e.Path.Count>1)e.Path.MoveNewest(TN(at));else e.Path.Push(TN(at));
            LingerTo(e,key,last,at,now);e.Head=at;
        }
        // The surface a round bounced off: the turn from its old heading to its new one.
        private static Vector3 NormalOf(Vector3 before,Vector3 after){var n=after-before;return n.sqrMagnitude>1e-4f?n.normalized:Vector3.up;}
        // The line a very fast round leaves: one entry for as long as it flies straight.
        private void LingerTo(Emitter e,long key,Vector3 from,Vector3 to,float now)
        {
            ref var s=ref e.Style;if(!(s.LingerLife>0)||(to-from).sqrMagnitude<1e-4f)return;if(e.Scoped&&(from-tracerEye).sqrMagnitude<ScopedClear*ScopedClear)return;
            for(int back=1;back<=6;back++)
            {
                ref var l=ref lingers[(lingerNext+LingerLines-back)%LingerLines];if(l.Key!=key)continue;
                if(l.B==from&&(e.Straight||Vector3.Dot((l.B-l.A).normalized,(to-from).normalized)>NativeTracerPath.Straight)){l.B=to;l.BornB=now;return;}break;
            }
            LineAdd(key,from,to,now,now,s.LingerLife,s.LingerWidth,1.2f,new Color(s.LingerColor.R*s.LingerGain,s.LingerColor.G*s.LingerGain,s.LingerColor.B*s.LingerGain,0));
        }
        private void LingerDraw(NativeWeaponFxDraw draw,Vector3 eye,float now)
        {
            for(int i=0;i<LingerLines;i++)
            {
                ref var l=ref lingers[i];if(l.Life<=0)continue;float a=NativeTracers.Line(now-l.BornA,l.Life),b=NativeTracers.Line(now-l.BornB,l.Life);
                // Gone once its newer end is; a guest's line waits for its authority time (now behind it, on the render clock).
                if(now-l.BornB>=l.Life||now<l.BornB-1){l.Life=0;l.Key=0;continue;}if(now<l.BornB)continue;
                NativeWeaponFxShapes.Line(draw,l.A,l.B,a,b,eye,tracerPixel,l.Color,l.Width,l.Pixels);
            }
        }
        // The needle along the path, its sheath and the bright head. `gain`: the fade of a spent round's tail.
        private void RoundDraw(NativeWeaponFxDraw draw,Emitter e,Vector3 eye,float now,bool trails,float gain,float speed)
        {
            ref var s=ref e.Style;float length=trails?NativeTracers.Length(s,speed>.01f?speed:e.Speed):NativeTracers.PlainLength;if(e.Spent)length=e.Left;float skip=e.Spent?0:s.Skip;
            if(e.Scoped){float clear=Vector3.Distance(eye,TU(e.Path.Newest))-ScopedClear;if(clear<=0)return;length=Mathf.Min(length,clear);}
            int n=e.Path.Walk(skip,length,tracerWalk,tracerAlong);for(int i=0;i<n;i++)tracerPoints[i]=TU(tracerWalk[i]);float boost=gain*(1+.7f*Mathf.Clamp01(1-(now-e.Boost)/.12f));
            if(n>=2)
            {
                NativeWeaponFxShapes.Needle(draw,tracerPoints,tracerAlong,n,eye,tracerPixel,new Color(s.CoreColor.R*s.CoreGain*boost,s.CoreColor.G*s.CoreGain*boost,s.CoreColor.B*s.CoreGain*boost,0),s.Core,NativeTracers.CorePixels,s.Fade);
                if(s.Sheath>0&&trails&&tracerSheaths<NativeTracers.MaxSheaths&&(tracerPoints[0]-eye).sqrMagnitude<NativeTracers.SheathRange*NativeTracers.SheathRange){tracerSheaths++;NativeWeaponFxShapes.Needle(draw,tracerPoints,tracerAlong,n,eye,tracerPixel,new Color(s.Edge.R*s.SheathGain*boost,s.Edge.G*s.SheathGain*boost,s.Edge.B*s.SheathGain*boost,0),s.Sheath,NativeTracers.SheathPixels,s.Fade);}
            }
            if(s.HeadSize>0&&!e.Spent&&!e.Scoped&&tracerHeads<NativeTracers.MaxHeads)
            {
                tracerHeads++;var head=TU(e.Path.Newest);float d=(head-eye).magnitude,near=NativeTracers.HeadNear(d);
                if(near>0)draw.Glow(false,head,new Color(s.CoreColor.R*s.HeadGain*near,s.CoreColor.G*s.HeadGain*near,s.CoreColor.B*s.HeadGain*near,0),NativeTracers.Width(s.HeadSize,NativeTracers.HeadPixels,d,tracerPixel),NativeWeaponFxDraw.HotCore);
            }
        }
        /// <summary>A round no longer in the frame's shots (true while its tail is still catching up to where it stopped).</summary>
        private bool RoundSpent(NativeWeaponFxDraw draw,NativeArena arena,Emitter e,Vector3 eye,float now,float q,bool low,bool trails)
        {
            if(!e.Round){if(e.Bolt){e.Bolt=false;BoltEnded(arena,e,now);}return false;}
            if(!e.Spent)RoundEnded(arena,e,now,trails);
            float u=e.ArriveAt>e.SpentAt?Mathf.Clamp01((now-e.SpentAt)/(e.ArriveAt-e.SpentAt)):1;var head=Vector3.Lerp(e.Head,e.EndPoint,u);
            if((e.Path.Newest-TN(head)).LengthSquared()>4e-4f)e.Path.Push(TN(head));e.Head=head;e.SpentAt=now;
            if(u>=1&&!e.Arrived){e.Arrived=true;e.ArrivedAt=now;RoundLanded(arena,e,now);}
            e.Left=e.Arrived?e.Full-(now-e.ArrivedAt)*Mathf.Max(1,e.Speed):e.Full;
            if(e.Arrived&&e.Left<=.02f&&now>e.ArrivedAt)return false;
            if(now-e.EndedAt>.5f)return false;
            if(e.Style.Core>0)RoundDraw(draw,e,eye,now,trails,.5f+.5f*Mathf.Clamp01(e.Left/Mathf.Max(.01f,e.Full)),e.Speed);
            return true;
        }
        // Where it stopped: the authority's record when it has one in reach, else where it was last drawn; a guest's round is still
        // behind its record by the render delay, so its head goes on to it by the record's time.
        private void RoundEnded(NativeArena arena,Emitter e,float now,bool trails)
        {
            e.Spent=true;e.SpentAt=e.EndedAt=now;e.EndPoint=e.Head;e.ArriveAt=now;e.Arrived=false;e.Struck=false;e.Full=trails?NativeTracers.Length(e.Style,e.Speed):NativeTracers.PlainLength;
            if(arena.ShotEndOf(e.Id,out var rec))
            {
                var p=NativeArena.Vec(rec.Position);float reach=Mathf.Max(3,e.Speed*.35f);if((p-e.Head).sqrMagnitude<reach*reach){e.EndPoint=p;e.ArriveAt=Mathf.Max(now,rec.At);}
            }
            // An event has it (a body, a prop: their own effects), or the static world took it (its own cast, cosmetic), or it ran out in the air.
            if(!roundHits.Contains(e.Id)&&e.Impact!=NativeRoundImpact.None&&roundRays<RoundRaysPerFrame)
            {
                roundRays++;float reach=(e.EndPoint-e.Head).magnitude+.45f;var from=e.Head-e.Dir*.12f;
                if(arena.PhysicsWorld.Raycast(from,e.Dir,out var hit,reach+.12f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)){e.Struck=true;e.EndPoint=hit.point;e.StruckNormal=hit.normal;}
            }
        }
        private void RoundLanded(NativeArena arena,Emitter e,float now)
        {
            if(!e.Struck)return;
            RoundMarkAdd(e.Impact,e.EndPoint,e.StruckNormal,e.Dir,now,e.Key*2654435761L+31);SurfaceWorldHit(arena,e.EndPoint,e.StruckNormal,e.Id,now,NativeRoundImpacts.Debris(e.Spec,e.Id));
        }
        // A school's bolt that struck the static world (no event): its pack burst there, as a prop's.
        private void BoltEnded(NativeArena arena,Emitter e,float now)
        {
            var school=NativeMagicPackFx.School(e.Spec);if(school==NativeMagicSchool.None||!PackLooks||roundHits.Contains(e.Id)||roundRays>=RoundRaysPerFrame||!(e.Speed>.5f))return;roundRays++;
            var from=e.Head-e.Dir*.12f;float reach=Mathf.Max(.5f,e.Speed*.1f);if(!arena.PhysicsWorld.Raycast(from,e.Dir,out var hit,reach,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))return;
            var weapon=NativeShotViews.Weapon(arena,e.Spec);magicHits[magicHitNext]=new MagicHit{At=hit.point,Normal=hit.normal,Born=now,Seed=e.Key*1000003L+11,Tint=NativeWeaponFxShapes.Lin(weapon!=null?(int)weapon.color:0xffffff),School=school,Scale=1.3f};magicHitNext=(magicHitNext+1)%magicHits.Length;
        }
        private void RoundMarkAdd(NativeRoundImpact kind,Vector3 at,Vector3 normal,Vector3 flight,float born,long seed)
        {
            if(kind==NativeRoundImpact.None)return;roundMarks[roundMarkNext]=new RoundMark{At=at,Normal=normal,Flight=flight,Born=born,Seed=seed,Kind=kind};roundMarkNext=(roundMarkNext+1)%RoundMarks;
        }
        private void RoundMarksDraw(NativeWeaponFxDraw draw,float now,float q,bool low)
        {
            for(int i=0;i<RoundMarks;i++){ref var m=ref roundMarks[i];if(m.Kind==NativeRoundImpact.None)continue;double age=now-m.Born;if(age>=NativeRoundImpacts.Life||age<-.5){m.Kind=NativeRoundImpact.None;continue;}if(age>=0)NativeWeaponFxShapes.RoundImpact(draw,m.Kind,m.At,m.Normal,m.Flight,m.Seed,age,q,low);}
        }
        // A gun round into a body: the small star on the hit point (Hit and Headshot events; the blood, the flash and the voxel sparks stay
        // the body's). A gun round into a prop: its Impact event lands the gun's own impact, thrown along the round's flight where it is known.
        private void RoundEventMark(in ArenaEvent e,Vector3 at,Vector3 normal,string spec)
        {
            var kind=NativeRoundImpacts.Gun(spec);if(kind==NativeRoundImpact.None)return;var flight=normal.sqrMagnitude>.25f?-normal.normalized:Vector3.zero;
            if(emitters.TryGetValue(NativeShotScene.Key(NativeShotKind.Bullet,e.Projectile),out var r))flight=r.Dir;else if(FlightOf(e.Projectile,out var known))flight=known;
            bool body=e.Kind!=EventKind.Impact;if(flight.sqrMagnitude>.25f&&(body||normal.sqrMagnitude<.25f))normal=-flight.normalized;if(normal.sqrMagnitude<.25f)normal=Vector3.up;
            RoundMarkAdd(body?NativeRoundImpact.Body:kind,at,normal,flight,e.Time,e.Id*1000003L+13);
        }
        // A replica's ricochet: the nearest round of its firer gives the flight (the event carries neither its round nor the surface's normal).
        private void RoundRicochetEvent(in ArenaEvent ev,Vector3 at)
        {
            Vector3 flight=Vector3.zero;float best=36;
            foreach(var pair in emitters){var r=pair.Value;if(!r.Round||r.Impact==NativeRoundImpact.None)continue;float d=(r.Head-at).sqrMagnitude;if(d<best){best=d;flight=r.Dir;}}
            RoundMarkAdd(NativeRoundImpact.Ricochet,at,flight.sqrMagnitude>.25f?-flight:Vector3.up,flight,ev.Time,ev.Id*1000003L+19);
        }
        // A scoped pilot's own round: the eye sees no muzzle down the tube, so a quick glare rises from under the view with the shot.
        private void ScopeGlare(NativeWeaponFxDraw draw,Camera camera,NativeArena arena,float now)
        {
            if(!ScopeActive||camera==null)return;var pilot=arena.Local;if(pilot==null)return;float age=now-pilot.LastShot;if(!(age>=0&&age<NativeTracers.ScopeGlareLife))return;
            float k=1-age/NativeTracers.ScopeGlareLife;var t=camera.transform;float depth=4,half=depth*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            draw.Glow(false,t.position+t.forward*depth-t.up*(half*1.15f),NativeWeaponFxShapes.Lin(NativeTracers.ScopeGlare,1.5f*k*k),half*3.2f,NativeWeaponFxDraw.Soft);
        }
        private void ClearTracers()
        {
            for(int i=0;i<lingers.Length;i++){lingers[i].Life=0;lingers[i].Key=0;}for(int i=0;i<roundMarks.Length;i++)roundMarks[i].Kind=NativeRoundImpact.None;
            roundHits.Clear();roundHitArena=null;roundHitGeneration=-1;roundHitEvent=0;roundDrawn.Clear();hitscanEvent=0;for(int i=0;i<RoundFlights;i++)flightIds[i]=0;
        }
    }
}
