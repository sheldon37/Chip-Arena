using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The spitter's acid (owner 2026-10-13: "The spitter should shoot do a windup animation and shoot acidic spit from their mouth,
    // creating a small puddle of poison (Can corrode destructibles). Can interupt their spit by hitting them."). Presentation only,
    // drawn from what every peer holds: the glob is Astra's minion-spit event once the rules emit it (until then the DEV preview
    // predicts it from the strike), launched from the posed mouth socket (NativeHordeModels.MouthSocket), never the chest.
    //  - The glob flies its arc (Core/NativeHordeFx.Glob) with a dripping, sizzling trail and splashes where it lands.
    //  - A pilot it lands on is splashed: the acid bursts on the body, sizzles and drips off it a moment (the poison itself, its
    //    ticks and tint, are the existing poison status and damage screen once the rules apply it).
    //  - The poison puddle bubbles and fumes, creeps out to the authority's radius with a clear bright edge, and fades by its end.
    //  - Destructible blocks the glob or puddle touches (the arena's props and static blocks, e.g. Brickfall's structure blocks)
    //    corrode: smoking pits eaten round the contact, a dripping corroded tint; when the block breaks its own structure effects
    //    take over (the corrosion ends with the block).
    //  - An interrupted spit (any damaging hit in its wind-up: NativeHordeKinds.Interrupts, the replicated cancel once it exists)
    //    dribbles from the mouth and fizzles harmlessly at the spitter's feet.
    // High-quality pass (owner 2026-10-14: "Improve visual fx from spitter/hulk zombie (make them high quality) acid needs to look
    // better"): everything above is drawn by Runtime/NativeZombieFxDraw.cs from Core/NativeZombieFx.cs (a glossy viscous core
    // with a membrane, halo, stringy tail, drips and glowing fumes; a crown, drops landing inside the pool and spatter; a lobed
    // film to the authority's radius with a meniscus, a ring holding that radius, bubbles, glints, fumes, evaporation and an etch;
    // eaten blotches with cooling rims). The pool's radius and duration are the record's (NativeSpitterRules), never changed here.
    // Fixed pools; no allocation per frame.
    public sealed partial class ArenaPresentation
    {
        private struct HordeGlob{public Vector3 From,To;public float At,Flight,Floor;public uint Seed;public int Id;public float Radius,Duration;public bool Landed;}
        private readonly HordeGlob[] hordeGlobs=new HordeGlob[24];private int hordeGlobNext;private readonly Dictionary<int,float> hordeSpat=new Dictionary<int,float>();private readonly HashSet<int> hordeSpitEvents=new HashSet<int>();
        private struct HordeCorrode{public bool Live;public NativePropBody Prop;public Collider Col;public Vector3 Contact,Normal;public float Start,Duration;public uint Seed;}
        private readonly HordeCorrode[] hordeCorrodes=new HordeCorrode[16];private int hordeCorrodeNext;
        private struct HordeAcidHit{public bool Live;public int Seat;public float At;public uint Seed;}
        private readonly HordeAcidHit[] hordeAcidHits=new HordeAcidHit[8];private int hordeAcidHitNext;
        private struct HordeFizzleJob{public bool Live;public Vector3 Mouth;public float Floor,At;public uint Seed;}
        private readonly HordeFizzleJob[] hordeFizzles=new HordeFizzleJob[8];private int hordeFizzleNext;
        /// <summary>The proposed direct-hit radius round the glob's landing (m; docs/SIREN-ZOMBIES.md).</summary>
        public const float GlobHitRadius=.6f;
        public const float AcidHitSeconds=1.8f;

        /// <summary>A glob in flight from the mouth `from` to `to`, launched at `at` over `flight` s; its puddle `radius` for `duration` s.
        /// `lift`: how far `to` is above the floor (the authority's landing point is .025 m over it; the DEV preview's is on it).</summary>
        private void HordeLaunchGlob(Vector3 from,Vector3 to,float at,float flight,uint seed,int id,float radius,float duration,float lift=.025f)
        {
            // A free slot, else the one whose acid ended (or ends) first: a live glob is never taken while an ended one's etch is left.
            int o=0;float first=float.MaxValue;hordeGlobNext++;
            for(int i=0;i<hordeGlobs.Length;i++){if(hordeGlobs[i].Flight<=0){o=i;break;}float end=hordeGlobs[i].At+hordeGlobs[i].Flight+hordeGlobs[i].Duration;if(end<first){first=end;o=i;}}
            hordeGlobs[o]=new HordeGlob{From=from,To=to,At=at,Flight=flight,Floor=to.y-lift,Seed=seed,Id=id,Radius=radius,Duration=duration};
        }
        /// <summary>An interrupted spit (HordeHit): the acid dribbles from the mouth to the floor at `floor` and fizzles.</summary>
        private void HordeFizzle(HordeKindState s,float floor,float at)
        {
            if(!s.MouthKnown)return;hordeFizzles[hordeFizzleNext++%hordeFizzles.Length]=new HordeFizzleJob{Live=true,Mouth=s.Mouth,Floor=floor,At=at,Seed=NativeHordeKinds.Seed(s.Id)^(uint)System.BitConverter.SingleToInt32Bits(at)};
        }

        /// <summary>DevEffects (from DrawHordeKindFx): the globs, their splashes and pools, the corroding blocks, splashed pilots,
        /// fizzles, drawn by NativeZombieFxDraw (the high-quality pass, owner 2026-10-14) on the authority's radius and duration.</summary>
        private void HordeAcidDraw(NativeFxDraw draw,NativeArena arena,float now,Vector3 eye,Vector3 right,Vector3 up,float quality)
        {
            var z=zombieFx;if(z==null)return;
            for(int i=0;i<hordeGlobs.Length;i++)
            {
                ref var g=ref hordeGlobs[i];if(g.Flight<=0)continue;float since=now-g.At;if(since<-.05f)continue;
                // The pool's etch and last fumes outlive it a while (NativeZombieFx.EtchSeconds).
                if(since>g.Flight+g.Duration+NativeZombieFx.EtchSeconds+.1f){g.Flight=0;continue;}
                if(since<g.Flight)
                {
                    NativeHordeFx.Glob(ToN(g.From),ToN(g.To),g.Flight,Mathf.Max(0,since),out var at);
                    if(z.Within(Vector3.Distance(eye,new Vector3(at.X,at.Y,at.Z))))z.Glob(g.From,g.To,g.Flight,since,g.Seed,g.Floor);continue;
                }
                float landed=since-g.Flight;
                if(!g.Landed){g.Landed=true;if(landed<.3f)HordeAcidLanded(arena,g,now);}
                if(!z.Within(Vector3.Distance(eye,g.To)))continue;
                if(landed<=g.Duration){z.Pool(g.To,g.Floor,landed,g.Radius,g.Duration,g.Seed);z.Splash(g.To,g.Floor,landed,g.Radius,g.Duration,g.Seed);}
                else z.PoolAfter(g.To,g.Floor,landed,g.Radius,g.Duration,g.Seed);
            }
            HordeCorrodeDraw(z,now);HordeAcidHitsDraw(z,arena,now);HordeFizzlesDraw(z,now);
        }
        // A glob has landed: the pilots it splashed and the blocks it (and its puddle) eat into.
        private void HordeAcidLanded(NativeArena arena,in HordeGlob g,float now)
        {
            if(arena==null)return;float landAt=g.At+g.Flight;
            var pilots=arena.Pilots;for(int i=0;i<pilots.Count;i++)
            {
                var p=pilots[i];if(p.Hp<=0||p.Body==null)continue;var d=p.Body.position-g.To;if(Mathf.Abs(d.y)>1.9f)continue;d.y=0;if(d.magnitude>GlobHitRadius+.42f)continue;
                hordeAcidHits[hordeAcidHitNext++%hordeAcidHits.Length]=new HordeAcidHit{Live=true,Seat=p.Seat.id,At=landAt,Seed=g.Seed^(uint)(p.Seat.id*7919)};
            }
            HordeCorrodeNear(arena.Props,g,landAt);HordeCorrodeNear(arena.StaticBlocks,g,landAt);
        }
        private void HordeCorrodeNear(List<NativePropBody> props,in HordeGlob g,float at)
        {
            for(int i=0;i<props.Count;i++)
            {
                var p=props[i];if(p==null||p.Indestructible||p.Fragment||!p.isActiveAndEnabled)continue;var col=p.GetComponent<Collider>();if(col==null)continue;
                var probe=g.To+Vector3.up*.05f;var near=col.ClosestPoint(probe);var d=probe-near;if(d.magnitude>g.Radius)continue;
                var n=d.sqrMagnitude>1e-6f?d.normalized:Vector3.up;var t=p.transform;
                hordeCorrodes[hordeCorrodeNext++%hordeCorrodes.Length]=new HordeCorrode{Live=true,Prop=p,Col=col,Contact=t.InverseTransformPoint(near),Normal=t.InverseTransformDirection(n),Start=at,Duration=g.Duration,Seed=g.Seed^(uint)(i*2654435761u)};
            }
        }
        // The corroding blocks: a ragged blotch eaten round the contact with a cooling acid rim, glowing pits, drips running down,
        // sizzle (NativeZombieFxDraw.Corrode).
        private void HordeCorrodeDraw(NativeZombieFxDraw z,float now)
        {
            for(int i=0;i<hordeCorrodes.Length;i++)
            {
                ref var c=ref hordeCorrodes[i];if(!c.Live)continue;float since=now-c.Start;
                // The block broke (its structure effects carry on) or the acid is spent.
                if(c.Prop==null||c.Col==null||!c.Prop.isActiveAndEnabled||c.Prop.Fragment||since>c.Duration+NativeHordeFx.CorrodeFade){c.Live=false;c.Prop=null;c.Col=null;continue;}
                if(since<0)continue;var t=c.Prop.transform;Vector3 at=t.TransformPoint(c.Contact),n=t.TransformDirection(c.Normal).normalized;if(!z.Within(Vector3.Distance(z.Eye,at)))continue;
                Vector3 tu=Mathf.Abs(n.y)>.8f?Vector3.Cross(n,Vector3.forward).normalized:Vector3.Cross(n,Vector3.up).normalized,tv=Vector3.Cross(n,tu);
                Vector3 down=Vector3.down-n*Vector3.Dot(Vector3.down,n);down=down.sqrMagnitude>1e-4f?down.normalized:tv;
                z.Corrode(at,n,tu,tv,down,since,c.Duration,c.Seed);
            }
        }
        // Pilots the acid splashed: the burst on the body, then its sheen, sizzle and drips a moment (NativeZombieFxDraw.Body).
        private void HordeAcidHitsDraw(NativeZombieFxDraw z,NativeArena arena,float now)
        {
            if(arena==null)return;
            for(int i=0;i<hordeAcidHits.Length;i++)
            {
                ref var h=ref hordeAcidHits[i];if(!h.Live)continue;float since=now-h.At;if(since>AcidHitSeconds){h.Live=false;continue;}if(since<0)continue;
                NativePilot p=null;var list=arena.Pilots;for(int k=0;k<list.Count;k++)if(list[k].Seat.id==h.Seat){p=list[k];break;}if(p?.Body==null){h.Live=false;continue;}
                Vector3 chest=p.Body.position+Vector3.up*.35f;if(!z.Within(Vector3.Distance(z.Eye,chest)))continue;z.Body(chest,p.Body.position.y-.85f,since,AcidHitSeconds,h.Seed);
            }
        }
        // The interrupted spits: the acid dribbles off the jaw to the floor and fizzles out there, harmless.
        private void HordeFizzlesDraw(NativeZombieFxDraw z,float now)
        {
            for(int i=0;i<hordeFizzles.Length;i++)
            {
                ref var f=ref hordeFizzles[i];if(!f.Live)continue;float since=now-f.At;if(since>NativeHordeFx.FizzleSeconds){f.Live=false;continue;}if(since<0)continue;
                if(z.Within(Vector3.Distance(z.Eye,f.Mouth)))z.Fizzle(f.Mouth,f.Floor,since,f.Seed);
            }
        }
        /// <summary>A new arena or a rewind: the acid in flight, the corrosion and the splashes are forgotten.</summary>
        private void ClearHordeAcid()
        {
            for(int i=0;i<hordeGlobs.Length;i++)hordeGlobs[i].Flight=0;for(int i=0;i<hordeCorrodes.Length;i++){hordeCorrodes[i].Live=false;hordeCorrodes[i].Prop=null;hordeCorrodes[i].Col=null;}
            for(int i=0;i<hordeAcidHits.Length;i++)hordeAcidHits[i].Live=false;for(int i=0;i<hordeFizzles.Length;i++)hordeFizzles[i].Live=false;
        }
    }
}
