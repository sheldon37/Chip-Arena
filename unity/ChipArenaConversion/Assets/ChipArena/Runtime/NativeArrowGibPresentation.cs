using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NMatrix=System.Numerics.Matrix4x4;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Arrow gibbing (owner 2026-10-14: "Add gibbing from arrows (Can pierce through enemies if player has pierce modifiers, can
    /// go in body/head and stop halfway) Blood coming out of arrow gibbing points"), presentation only (Core NativeArrowGib):
    /// <list type="bullet">
    /// <item>A bow hit's arrow lodges or passes by the hit's own replicated flag (ArenaEvent.Pierced with its Projectile id;
    /// an older frame's hit falls back to the live arrows' paths and their pierce chips, lodging in doubt).</item>
    /// <item>A lodged arrow buries about halfway into the struck part (a head shot halfway into the head), riding the joint
    /// (the live rig, then the corpse, the half of a split body it sat on); its entry drips a short, decaying trickle.</item>
    /// <item>A pass-through leaves no arrow: its entry and exit bleed (the exit sprays along the flight, then both drip) and the
    /// flying arrow goes on.</item>
    /// </list>
    /// The wound's own blood is the existing wound path's (the bow's puncture or cavity wound, LivingWounds); this adds only the
    /// arrow points' trickle and a pass-through's exit spray, under the per-target blood budget and at most
    /// NativeArrowGib.BleedCap points at once. Gore off: no arrow blood (arrows still lodge or pass). No hit, damage, hitbox,
    /// timing or network rule reads any of it.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        /// <summary>A pass-through's exit hole's record id (its entry keeps the hit event's).</summary>
        private const int ArrowExitHole=0x40000000;
        private ArrowShot[] arrowShots=new ArrowShot[64];
        private readonly Dictionary<int,int> arrowPasses=new Dictionary<int,int>();
        private readonly Dictionary<long,NativeShotModules> arrowModules=new Dictionary<long,NativeShotModules>();
        private void ArrowClear(){arrowPasses.Clear();arrowSeats.Clear();}
        /// <summary>The live bow arrows for an older frame's hit (no projectile id): each with the pierces its chips allow less
        /// the ones this presentation saw it spend.</summary>
        private int ArrowShots(NativeArena arena)
        {
            int n=0;
            foreach(var b in arena.Bullets)
            {
                if(n>=arrowShots.Length)break;if(!NativeBowWire.Is(b.Spec))continue;int pierces=(int)Math.Round(NativeShotViews.Modules(arena,b.Mods,arrowModules).Pierces);arrowPasses.TryGetValue(b.Id,out int spent);
                arrowShots[n++]=new ArrowShot{Id=b.Id,Owner=b.Owner,Pierces=Math.Max(0,pierces-spent),At=arena.Time,Gravity=b.Gravity,Position=BowN(b.Position),Velocity=BowN(b.Velocity)};
            }
            return n;
        }
        /// <summary>A bow arrow striking pilot `e.Target` at `at` along `dir` (world) on the posed `rig` drawn at `time`: lodged
        /// halfway into the part, or (passed through) its entry and exit holes and the exit's spray. On a pilot the hit is first
        /// moved onto the body as drawn (ArrowOnDrawn) and, on a drawn model body, where it met the skin is then found on a worker
        /// (ArrowSeatAsk); the game joint nearest it (the voxel body's, the fallback's) as before.</summary>
        private void ArrowStrike(NativeArena arena,in ArenaEvent e,CapturedAnimation.Player rig,Vector3 at,Vector3 dir,float draw,bool std,float time)
        {
            bool pilot=e.Target<10000;if(pilot)at=ArrowOnDrawn(arena,e,at,time);
            // The struck joint (nearest the hit point on the posed rig) and the arrow in its frame.
            bowJointRig=rig;bowJoint??=b=>BowN(bowJointRig.Bones[b].localToWorldMatrix);int bone=NativeBowStuck.Nearest(bowJoint,BowN(at));
            var joint=rig.Bones[bone].localToWorldMatrix;var inv=rig.Bones[bone].worldToLocalMatrix;var local=BowN(inv.MultiplyPoint3x4(at));var along=inv.MultiplyVector(dir);
            float scale=along.sqrMagnitude>1e-8f?1/along.magnitude:1;var localDir=CapturedRig.Unit(BowN(along),NVector.UnitZ);
            int count=e.Projectile>0?0:ArrowShots(arena);
            var fate=NativeArrowGib.Fate(e.Projectile,e.Pierced,e.Source,BowN(at),e.Time,arrowShots,count,out int shot);
            if(fate==ArrowFate.Pass&&e.Projectile<=0&&shot>0){arrowPasses.TryGetValue(shot,out int spent);if(arrowPasses.Count>256)arrowPasses.Clear();arrowPasses[shot]=spent+1;}
            var victim=PilotOf(arena,e.Target);bool lethal=victim!=null&&victim.Hp<=0&&victim.Death!=null&&Mathf.Abs(victim.Death.at-e.Time)<.02f;
            bool gore=Gore&&!(FirstPerson&&e.Target==LocalId);
            uint seed=unchecked((uint)e.Id*2246822519u^0xa770u);
            if(fate==ArrowFate.Pass)
            {
                NativeArrowGib.Through(bone,local,localDir,out var entry,out var exit);
                bowStuck.AddHole(e.Id,e.Target,bone,entry,localDir,e.Time,NativeArrowGib.Strength(gore,draw,lethal,false),LowGraphics);
                bowStuck.AddHole(e.Id^ArrowExitHole,e.Target,bone,exit,localDir,e.Time,NativeArrowGib.Strength(gore,draw,lethal,true),LowGraphics);
                var from=joint.MultiplyPoint3x4(BowU(exit));bool seating=pilot&&ArrowSeatAsk(arena,e,rig,at,dir,true,seed);
                if(!gore)return;
                // The exit sprays along the flight (the wound's own spray leaves its entry, LivingWounds); on a model body from where
                // the flight left its skin, once found (ArrowSeatsDone), else from the game joint's exit now.
                NativeArrowGib.Exit(draw,lethal,out float severity,out int extra);
                if(seating)ArrowSeatSpray(e.Id,from,severity,extra,lethal);else ArrowExitSpray(e.Id,e.Target,e.Time,from,dir,severity,extra,lethal);
                return;
            }
            NativeArrowGib.Lodge(bone,local,localDir,seed,out var point,out float depth);
            bowStuck.AddPilot(e.Id,e.Target,bone,point,localDir,e.Time,draw,std,LowGraphics,depth*scale,NativeArrowGib.Strength(gore,draw,lethal,false));
            if(pilot)ArrowSeatAsk(arena,e,rig,at,dir,false,seed);
        }
        /// <summary>The replicated hit point `at` moved onto pilot `e.Target`'s body as drawn this frame (Core NativeArrowGib.OnDrawn):
        /// by the drawn root's offset from the replicated place and the pilot's velocity from the hit to the drawn pose's moment
        /// (the host's newest step; a guest's render clock, a guest's own predicted pilot taken as drawn at the hit).</summary>
        private Vector3 ArrowOnDrawn(NativeArena arena,in ArenaEvent e,Vector3 at,float time)
        {
            Actor a=null;var actors=weaponSnapshot?.Actors;if(actors!=null)foreach(var x in actors)if(x!=null&&x.Id==e.Target){a=x;break;}
            if(a==null)return at;var placed=Vec(a.Position);var offset=visualPositions.TryGetValue(e.Target,out var drawn)?drawn-placed:Vector3.zero;
            float poseAt=!arena.Replica?arena.Time:e.Target==LocalId?e.Time:time;
            return BowU(NativeArrowGib.OnDrawn(BowN(at),BowN(offset),BowN(Vec(a.Velocity)),e.Time,poseAt));
        }

        // ---- the arrow on the drawn model (owner 2026-11-18: "it'll look like someone is running around with floating arrows") ----
        // The game joint the arrow used to ride is the voxel rig's: the model body follows the pack's clips, its stride and hand fits
        // instead, and the replicated hit lies on the game's capsule, outside the model. So where each arrow's flight met the body's
        // skin as drawn at the hit is found (Core NativeArrowGib.Seat: the cut's skinned parts posed on a worker, one search per body
        // per frame), and the arrow then rides that skin's own bone: on the live body, then the corpse (which wears the drawn pose
        // and the same bones). Its record waits unseen meanwhile (a frame or two); found nothing, it keeps the game joint's place.
        // One struck flight to place: its hit's id, whether it went through (and then the exit's spray, waiting for its place: from
        // the game joint's exit `From` without one), a head shot, the hit and flight (world, as drawn), the bury's seed; what was found.
        private struct ArrowSeatQuery{public int Event,Target,Extra;public bool Pass,Head,Spray,Lethal;public NVector Hit,Dir;public Vector3 From;public float At,Severity;public uint Seed;}
        private sealed class ArrowSeatJob
        {
            public int Target,Frame;public bool Launched;public float Started;public CutPart[] Parts;public int[] Region;public Matrix4x4[] Bones;
            // The flights (fixed once launched) and, once the worker is through, what it found for each (Seats, in their order).
            public readonly List<ArrowSeatQuery> Asks=new List<ArrowSeatQuery>();public NativeArrowGib.ModelSeat[] Seats;public System.Threading.Tasks.Task Task;
            public bool Done=>Launched&&(Task==null||Task.IsCompleted);
            public void Run()
            {
                try
                {
                    int n=0,t=0;foreach(var p in Parts){n+=p.Mesh.At.Length;t+=p.Mesh.Tris.Length;}
                    var at=new NVector[n];var bone=new int[n];var tris=new int[t];int vo=0,to=0;
                    foreach(var p in Parts)
                    {
                        var posed=CutJob.Posed(p);var w=p.Mesh.Weights;
                        for(int i=0;i<posed.Length;i++)
                        {
                            var q=posed[i];at[vo+i]=new NVector(q.x,q.y,q.z);var bw=w[i];int b=bw.boneIndex0;float most=bw.weight0;
                            if(bw.weight1>most){most=bw.weight1;b=bw.boneIndex1;}if(bw.weight2>most){most=bw.weight2;b=bw.boneIndex2;}if(bw.weight3>most)b=bw.boneIndex3;
                            bone[vo+i]=b>=0&&b<p.Visual.Length?p.Visual[b]:-1;
                        }
                        var pt=p.Mesh.Tris;for(int i=0;i<pt.Length;i++)tris[to+i]=pt[i]+vo;vo+=posed.Length;to+=pt.Length;
                    }
                    var seats=new NativeArrowGib.ModelSeat[Asks.Count];for(int k=0;k<seats.Length;k++){var a=Asks[k];seats[k]=NativeArrowGib.Seat(at,n,tris,t/3,bone,Region,a.Hit,a.Dir,a.Head);}
                    Seats=seats;
                }
                catch(Exception){Seats=null;}
            }
        }
        private readonly List<ArrowSeatJob> arrowSeats=new List<ArrowSeatJob>();
        /// <summary>Asks where pilot `e.Target`'s arrow (struck at `at` along `dir`, world, as drawn) met its drawn model, holding its
        /// record (and a pass-through's exit hole) unseen until it is found. Only a body drawn this frame, or (a killing arrow)
        /// last drawn this frame or the one before, whose corpse wears that pose. False: no model body to seat on (the game
        /// joint's place stands).</summary>
        private bool ArrowSeatAsk(NativeArena arena,in ArenaEvent e,CapturedAnimation.Player rig,Vector3 at,Vector3 dir,bool pass,uint seed)
        {
            if(!ModelLook(null)||!visualBodies.TryGetValue(rig,out var body)||body.Root==null||body.Binding?.Bones==null||body.All==null)return false;
            var victim=PilotOf(arena,e.Target);bool alive=victim!=null&&victim.Hp>0;
            if(alive?!(body.Root.activeSelf&&body.PosedFrame==Time.frameCount):!NativeCorpseDress.Drawn(body.PosedFrame,Time.frameCount))return false;
            ArrowSeatJob job=null;foreach(var j in arrowSeats)if(j.Target==e.Target&&j.Frame==Time.frameCount&&!j.Launched){job=j;break;}
            if(job==null)
            {
                var bones=body.Binding.Bones;var index=new Dictionary<Transform,int>();for(int v=0;v<bones.Length;v++)if(bones[v]!=null)index[bones[v]]=v;
                var parts=new List<CutPart>();int count=0;
                foreach(var r in body.All)if(r is SkinnedMeshRenderer skin&&skin.enabled&&r.name!=SkeletonRenderer&&ArrowSkinShown(skin.transform,body.Root.transform,alive)){var part=VisualCutPart(skin,parts.Count,count,index,null,0,null);if(part==null)continue;parts.Add(part);count+=part.Mesh.At.Length;}
                if(parts.Count==0)return false;
                var joints=VisualJoints(body.Binding);var region=new int[joints.Length];for(int v=0;v<region.Length;v++)region[v]=NativeBisect.Anchor(joints[v]);
                var frames=new Matrix4x4[bones.Length];for(int v=0;v<bones.Length;v++)frames[v]=bones[v]!=null?bones[v].localToWorldMatrix:Matrix4x4.identity;
                job=new ArrowSeatJob{Target=e.Target,Frame=Time.frameCount,Parts=parts.ToArray(),Region=region,Bones=frames};arrowSeats.Add(job);
            }
            job.Asks.Add(new ArrowSeatQuery{Event=e.Id,Target=e.Target,At=e.Time,Pass=pass,Head=e.Kind==EventKind.Headshot,Hit=BowN(at),Dir=BowN(dir.sqrMagnitude>1e-8f?dir.normalized:Vector3.forward),Seed=seed});
            bowStuck.Seating(e.Id,true);if(pass)bowStuck.Seating(e.Id^ArrowExitHole,true);return true;
        }
        // Whether a skinned renderer of a body draws: on and shown in the body (a killed one's body is already hidden: then up to its root).
        private static bool ArrowSkinShown(Transform t,Transform root,bool alive)
        {
            if(alive)return t.gameObject.activeInHierarchy;for(;t!=null&&t!=root;t=t.parent)if(!t.gameObject.activeSelf)return false;return true;
        }
        /// <summary>The frame's searches started (after its hits), each on a worker (on the main thread where there are none).</summary>
        private void ArrowSeatsStart()
        {
            foreach(var j in arrowSeats)
            {
                if(j.Launched)continue;j.Launched=true;j.Started=Time.unscaledTime;
                if(Application.platform!=RuntimePlatform.WebGLPlayer)try{j.Task=System.Threading.Tasks.Task.Run(j.Run);}catch(Exception){j.Task=null;}
                if(j.Task==null)j.Run();
            }
        }
        /// <summary>A pass-through's exit spray held for its place on the model (`from`: the game joint's exit, without one).</summary>
        private void ArrowSeatSpray(int evt,Vector3 from,float severity,int extra,bool lethal)
        {
            foreach(var j in arrowSeats)if(!j.Launched)for(int k=0;k<j.Asks.Count;k++){var a=j.Asks[k];if(a.Event!=evt)continue;a.Spray=true;a.From=from;a.Severity=severity;a.Extra=extra;a.Lethal=lethal;j.Asks[k]=a;return;}
        }
        /// <summary>A pass-through's exit spray along its flight `dir` from `from` (world).</summary>
        private void ArrowExitSpray(int evt,int target,float at,Vector3 from,Vector3 dir,float severity,int extra,bool lethal)
        {
            long key=((long)(target&0xffff)<<40)^((long)(evt&0xffffff)<<12)^(1L<<57);bool stain=stainedWounds.Add(key);
            blood.Spray("cavity",severity,unchecked((uint)evt*2654435761u^0xa7f0u),key,BowN(from),BowN(dir),BowN(dir),at,BloodQuality,LowGraphics,stain,extra,target,lethal);
        }
        /// <summary>Each frame before the arrows draw: every finished search's places put into their records (in the found bone's
        /// frame as it was posed at the hit), the rest let go after half a second (their game joint's place stands).</summary>
        private void ArrowSeatsDone()
        {
            for(int j=arrowSeats.Count-1;j>=0;j--)
            {
                var job=arrowSeats[j];if(!job.Launched||!job.Done&&Time.unscaledTime-job.Started<.5f)continue;arrowSeats.RemoveAt(j);var seats=job.Done?job.Seats:null;
                for(int k=0;k<job.Asks.Count;k++)
                {
                    var a=job.Asks[k];int exitHole=a.Event^ArrowExitHole;var s=seats!=null&&k<seats.Length?seats[k]:default;bool found=s.Found&&s.Bone>=0&&s.Bone<job.Bones.Length;
                    if(a.Spray)ArrowExitSpray(a.Event,a.Target,a.At,found?BowU(s.Exit):a.From,BowU(a.Dir),a.Severity,a.Extra,a.Lethal);
                    if(!found){bowStuck.Seating(a.Event,false);if(a.Pass)bowStuck.Seating(exitHole,false);continue;}
                    var into=job.Bones[s.Bone].inverse;var along=BowN(into.MultiplyVector(BowU(a.Dir)));var entry=BowN(into.MultiplyPoint3x4(BowU(s.Entry)));
                    if(a.Pass){bowStuck.Seat(a.Event,s.Bone,entry,along,0);bowStuck.Seat(exitHole,s.Bone,BowN(into.MultiplyPoint3x4(BowU(s.Exit))),along,0);}
                    else bowStuck.Seat(a.Event,s.Bone,entry,along,NativeArrowGib.Bury(s.Thick,a.Seed));
                }
            }
        }
        /// <summary>
        /// A stuck arrow on pilot corpse `corpse`: one seated on the model rides the model corpse's own bone, on the half of a split
        /// body its point lay on as it died (the first half, whole, while the cut is made), hidden with a crushed part or a hidden
        /// body (dissolved, sunk down a vent), fading as it dissolves. Otherwise the corpse's part of its game joint, as before
        /// (ArrowHalf), hidden while its model corpse is.
        /// </summary>
        private bool ArrowOnCorpse(NativeCorpse corpse,in NativeBowStuck.Record r,out NMatrix world,out bool model,out float opacity)
        {
            world=NMatrix.Identity;model=false;opacity=1;
            if(r.Model>=0&&visualCorpses.TryGetValue(corpse,out var c)&&c.Root!=null&&c.Bones!=null&&r.Model<c.Bones.Length)
            {
                var on=corpse;var shown=c;
                if(corpse.Twin!=null&&corpse.Bisect is BisectFx fx&&fx.Request!=null&&c.Bind!=null&&c.Joint!=null)
                {
                    int side=fx.Request.HalfOf(c.Joint[r.Model],CutNum(c.Bind[r.Model].MultiplyPoint3x4(BowU(r.ModelPoint))))==1?1:0;var stage=VisualSplitStage(corpse);
                    if(NativeSplitShow.Wears(1,side,stage))on=corpse.Twin;else if(!NativeSplitShow.Wears(0,side,stage))return false;
                    if(!visualCorpses.TryGetValue(on,out shown)||shown.Root==null||shown.Bones==null||r.Model>=shown.Bones.Length)return false;
                }
                var b=shown.Bones[r.Model];if(b==null||!shown.Root.activeSelf||shown.Joint==null||!on.ShownBone(shown.Joint[r.Model]))return false;
                world=BowN(b.localToWorldMatrix);model=true;opacity=shown.Dissolve>0?1-shown.Dissolve:1;return true;
            }
            var half=ArrowHalf(corpse,r.Bone,r.Point);if(!half.ShownBone(r.Bone))return false;
            if(visualCorpses.TryGetValue(half,out var hc)&&hc.Root!=null&&!hc.Root.activeSelf)return false;
            world=BowN(half.PresentedBone(r.Bone));return true;
        }
        /// <summary>The arrow points' trickle this frame: each bleeding point (the newest NativeArrowGib.BleedCap) drips the drops
        /// its schedule has reached, from where it rides now, out of the wound along the shaft and falling.</summary>
        private void ArrowBleeds(float time)
        {
            if(bowBone==null)return;int cap=NativeArrowGib.BleedCap(BloodQuality,LowGraphics);
            for(int i=0;i<bowStuck.Count;i++)
            {
                var r=bowStuck[i];if(r.Kind!=NativeBowStuck.Kind.Pilot||!(r.Blood>0))continue;
                int due=NativeArrowGib.Drips(time-r.Born,r.Blood,BloodQuality,LowGraphics);if(due<=r.Bled)continue;
                if(r.Seating)continue;
                if(!Gore||FirstPerson&&r.Target==LocalId||!NativeArrowGib.Bleeds(bowStuck,i,time,cap)||!bowBone(r,out NMatrix m,out bool model,out _)){bowStuck.Bleed(i,due);continue;}
                NativeBowStuck.Placed(r,m,model,out var at,out var dir,out _);var outward=r.Hole&&(r.Event&ArrowExitHole)!=0?dir:-dir;
                for(int k=r.Bled;k<due&&k<r.Bled+2;k++)
                {
                    long key=((long)(r.Target&0xffff)<<40)^((long)(r.Event&0x7fffffff)<<8)^(k+1)^(1L<<56);bool stain=stainedWounds.Add(key);
                    blood.Spray("drip",1,unchecked((uint)r.Event*2654435761u^(uint)(k+1)*0x9e3779b1u),key,at+outward*.012f,outward,-NVector.UnitY,time,BloodQuality,LowGraphics,stain);
                }
                bowStuck.Bleed(i,due);
            }
        }
        /// <summary>The half of a split corpse an arrow rides: the half its joint went to, or (a joint the cut crossed) the side
        /// its point was on as the body died.</summary>
        private static NativeCorpse ArrowHalf(NativeCorpse corpse,int bone,NVector local)
        {
            if(corpse.Twin==null||corpse.Request==null)return corpse;var p=corpse.Bind[bone].MultiplyPoint3x4(BowU(local));
            return NativeArrowGib.Half(corpse.Request,bone,BowN(p))==1?corpse.Twin:corpse;
        }
    }
}
