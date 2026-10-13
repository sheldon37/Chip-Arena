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
    // Fixed pools; no allocation per frame.
    public sealed partial class ArenaPresentation
    {
        private struct HordeGlob{public Vector3 From,To;public float At,Flight;public uint Seed;public int Id;public float Radius,Duration;public bool Landed;}
        private readonly HordeGlob[] hordeGlobs=new HordeGlob[16];private int hordeGlobNext;private readonly Dictionary<int,float> hordeSpat=new Dictionary<int,float>();private readonly HashSet<int> hordeSpitEvents=new HashSet<int>();
        private struct HordeCorrode{public bool Live;public NativePropBody Prop;public Collider Col;public Vector3 Contact,Normal;public float Start,Duration;public uint Seed;}
        private readonly HordeCorrode[] hordeCorrodes=new HordeCorrode[16];private int hordeCorrodeNext;
        private struct HordeAcidHit{public bool Live;public int Seat;public float At;public uint Seed;}
        private readonly HordeAcidHit[] hordeAcidHits=new HordeAcidHit[8];private int hordeAcidHitNext;
        private struct HordeFizzleJob{public bool Live;public Vector3 Mouth;public float Floor,At;public uint Seed;}
        private readonly HordeFizzleJob[] hordeFizzles=new HordeFizzleJob[8];private int hordeFizzleNext;
        /// <summary>The proposed direct-hit radius round the glob's landing (m; docs/SIREN-ZOMBIES.md).</summary>
        public const float GlobHitRadius=.6f;
        public const float AcidHitSeconds=1.8f;

        /// <summary>A glob in flight from the mouth `from` to `to`, launched at `at` over `flight` s; its puddle `radius` for `duration` s.</summary>
        private void HordeLaunchGlob(Vector3 from,Vector3 to,float at,float flight,uint seed,int id,float radius,float duration)
        {
            hordeGlobs[hordeGlobNext++%hordeGlobs.Length]=new HordeGlob{From=from,To=to,At=at,Flight=flight,Seed=seed,Id=id,Radius=radius,Duration=duration};
        }
        /// <summary>An interrupted spit (HordeHit): the acid dribbles from the mouth to the floor at `floor` and fizzles.</summary>
        private void HordeFizzle(HordeKindState s,float floor,float at)
        {
            if(!s.MouthKnown)return;hordeFizzles[hordeFizzleNext++%hordeFizzles.Length]=new HordeFizzleJob{Live=true,Mouth=s.Mouth,Floor=floor,At=at,Seed=NativeHordeKinds.Seed(s.Id)^(uint)System.BitConverter.SingleToInt32Bits(at)};
        }

        /// <summary>DevEffects (from DrawHordeKindFx): the globs, their splashes and puddles, the corroding blocks, splashed pilots, fizzles.</summary>
        private void HordeAcidDraw(NativeFxDraw draw,NativeArena arena,float now,Vector3 eye,Vector3 right,Vector3 up,float quality)
        {
            var acid=NativeArena.ColorOf(NativeHordeKinds.Seam[NativeHordeKinds.Spitter]);bool low=quality<.5f;var dark=new Color(.035f,.07f,.02f);
            for(int i=0;i<hordeGlobs.Length;i++)
            {
                ref var g=ref hordeGlobs[i];if(g.Flight<=0)continue;float since=now-g.At;if(since<-.05f)continue;if(since>g.Flight+g.Duration+.1f){g.Flight=0;continue;}
                var from=ToN(g.From);var to=ToN(g.To);
                if(NativeHordeFx.Glob(from,to,g.Flight,since,out var at))
                {
                    // The glob: a dripping green clot, its glow, the trail of droplets behind it and drips shaken off it, sizzling.
                    Vector3 p=new Vector3(at.X,at.Y,at.Z);draw.Cube(draw.Solid,p,Quaternion.Euler(since*400,since*260,0),new Vector3(.12f,.1f,.12f),new Color(.06f,.11f,.03f),NativeFxDraw.Authored);draw.Sprite(draw.Effects,p,right,up,.38f,Alpha(acid,.65f),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    for(int k=0;k<NativeHordeFx.TrailDots;k+=low?2:1)if(NativeHordeFx.GlobTrail(from,to,g.Flight,since,k,out var tp,out float ts,out float ta))draw.Sprite(draw.Effects,new Vector3(tp.X,tp.Y,tp.Z),right,up,.16f*ts,Alpha(acid,ta),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    if(!low&&NativeHordeFx.Sizzle(g.Seed,0,since,.05f,out var so,out float ss,out float sa))draw.Sprite(draw.Effects,p+new Vector3(so.X,so.Y*.4f,so.Z),right,up,ss*1.4f,Alpha(new Color(.55f,.62f,.45f),sa*.6f),NativeFxDraw.Authored);
                }
                for(int k=0;k<NativeHordeFx.TrailDrips;k+=low?2:1)if(NativeHordeFx.TrailDrip(from,to,g.Flight,g.Seed,k,since,out var dp,out float dpa))draw.Sprite(draw.Effects,new Vector3(dp.X,dp.Y,dp.Z),right,up,.05f,Alpha(acid,dpa),NativeFxDraw.Authored+NativeFxDraw.Additive);
                if(since<g.Flight)continue;
                float landed=since-g.Flight;Vector3 c=g.To+Vector3.up*.01f;
                if(!g.Landed){g.Landed=true;if(landed<.3f)HordeAcidLanded(arena,g,now);}
                // The splash, a flash of acid.
                if(landed<.18f)draw.Sprite(draw.Effects,c+Vector3.up*.15f,right,up,1.1f*(1-landed/.18f),Alpha(acid,.8f*(1-landed/.18f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
                for(int k=0;k<NativeHordeFx.Splashes;k+=low?2:1)if(NativeHordeFx.Splash(g.Seed,k,landed,out var o2,out float s2,out float sa2))draw.Cube(draw.Solid,c+new Vector3(o2.X,o2.Y,o2.Z),Quaternion.identity,Vector3.one*s2,new Color(.06f,.12f,.03f),NativeFxDraw.Authored);
                // The poison puddle: a dark pool, a clear bright edge at its radius, bubbles and fumes.
                if(NativeHordeFx.Puddle(landed,g.Duration,g.Radius,out float r,out float al))
                {
                    draw.Ring(draw.Solid,c,0,r,new Color(dark.r*al,dark.g*al,dark.b*al),NativeFxDraw.Authored);
                    draw.Ring(draw.Effects,c+Vector3.up*.003f,.55f*r,r*.97f,Alpha(acid,al*.22f),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    draw.Ring(draw.Effects,c+Vector3.up*.004f,r*.95f,r,Alpha(acid,al*.95f),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    draw.Ring(draw.Solid,c-Vector3.up*.002f,r,r+.025f,new Color(.01f,.02f,.005f),NativeFxDraw.Authored);
                    for(int k=0;k<NativeHordeFx.Bubbles;k+=low?2:1)if(NativeHordeFx.Bubble(g.Seed,k,landed,r,out var o,out float sz,out float ba))draw.Sprite(draw.Effects,c+new Vector3(o.X,o.Y,o.Z),right,up,sz*2,Alpha(acid,ba*.7f),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    for(int k=0;k<NativeHordeFx.Fumes;k+=low?2:1)if(NativeHordeFx.Fume(g.Seed,k,landed,g.Duration,r,out var fo,out float fs,out float fa))draw.Sprite(draw.Effects,c+new Vector3(fo.X,fo.Y,fo.Z),right,up,fs*2,Alpha(new Color(.45f,.7f,.3f),fa*.5f),NativeFxDraw.Authored);
                }
            }
            HordeCorrodeDraw(draw,now,right,up,low,acid);HordeAcidHitsDraw(draw,arena,now,right,up,low,acid);HordeFizzlesDraw(draw,now,right,up,low,acid);
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
        // The corroding blocks: pits eaten into the face round the contact, smoke off it, the corroded tint running down it.
        private void HordeCorrodeDraw(NativeFxDraw draw,float now,Vector3 right,Vector3 up,bool low,Color acid)
        {
            for(int i=0;i<hordeCorrodes.Length;i++)
            {
                ref var c=ref hordeCorrodes[i];if(!c.Live)continue;float since=now-c.Start;
                // The block broke (its structure effects carry on) or the acid is spent.
                if(c.Prop==null||c.Col==null||!c.Prop.isActiveAndEnabled||c.Prop.Fragment||since>c.Duration+NativeHordeFx.CorrodeFade){c.Live=false;c.Prop=null;c.Col=null;continue;}
                if(since<0)continue;var t=c.Prop.transform;Vector3 at=t.TransformPoint(c.Contact),n=t.TransformDirection(c.Normal).normalized;
                Vector3 tu=Mathf.Abs(n.y)>.8f?Vector3.Cross(n,Vector3.forward).normalized:Vector3.Cross(n,Vector3.up).normalized,tv=Vector3.Cross(n,tu);
                Vector3 down=Vector3.down-n*Vector3.Dot(Vector3.down,n);down=down.sqrMagnitude>1e-4f?down.normalized:tv;
                float fade=1-Mathf.SmoothStep(0,1,(since-c.Duration)/NativeHordeFx.CorrodeFade),spread=.32f;
                // The corroded tint over the patch the acid touched.
                Vector3 o=at+n*.006f;draw.Quad(draw.Effects,o-tu*spread-tv*spread,o+tu*spread-tv*spread,o+tu*spread+tv*spread,o-tu*spread+tv*spread,new Color(.22f,.32f,.06f,.42f*fade*Mathf.SmoothStep(0,1,since/.4f)),NativeFxDraw.Authored);
                for(int k=0;k<NativeHordeFx.Pits;k++)if(NativeHordeFx.Pit(c.Seed,k,since,c.Duration,spread*.8f,out var pa,out float ps,out float pal))
                {Vector3 pc=at+n*.008f+tu*pa.X+tv*pa.Y;draw.Quad(draw.Effects,pc-tu*ps-tv*ps,pc+tu*ps-tv*ps,pc+tu*ps+tv*ps,pc-tu*ps+tv*ps,new Color(.02f,.03f,.01f,pal),NativeFxDraw.Authored);draw.Sprite(draw.Effects,pc+n*.01f,right,up,ps*1.6f,Alpha(acid,pal*.35f),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                for(int k=0;k<NativeHordeFx.CorrodeDrips;k++)if(NativeHordeFx.CorrodeDrip(c.Seed,k,since,c.Duration,spread,out var da,out float run,out float dal))
                {Vector3 d0=at+n*.01f+tu*da.X,d1=d0+down*run;Vector3 side=tu*.012f;draw.Quad(draw.Effects,d0-side,d0+side,d1+side*.4f,d1-side*.4f,Alpha(acid,dal*.75f),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                for(int k=0;k<NativeHordeFx.Sizzles;k+=low?2:1)if(NativeHordeFx.Sizzle(c.Seed,k,since,spread,out var so,out float ss,out float sa))draw.Sprite(draw.Effects,at+n*(.05f+so.Y*.3f)+tu*so.X+tv*so.Z+Vector3.up*so.Y,right,up,ss*2,Alpha(new Color(.55f,.62f,.45f),sa*fade*.7f),NativeFxDraw.Authored);
            }
        }
        // Pilots the acid splashed: the burst on the body, then it sizzles and drips off them a moment.
        private void HordeAcidHitsDraw(NativeFxDraw draw,NativeArena arena,float now,Vector3 right,Vector3 up,bool low,Color acid)
        {
            if(arena==null)return;
            for(int i=0;i<hordeAcidHits.Length;i++)
            {
                ref var h=ref hordeAcidHits[i];if(!h.Live)continue;float since=now-h.At;if(since>AcidHitSeconds){h.Live=false;continue;}if(since<0)continue;
                NativePilot p=null;var list=arena.Pilots;for(int k=0;k<list.Count;k++)if(list[k].Seat.id==h.Seat){p=list[k];break;}if(p?.Body==null){h.Live=false;continue;}
                Vector3 chest=p.Body.position+Vector3.up*.35f;float fade=1-Mathf.SmoothStep(0,1,(since-1.1f)/.7f);
                if(since<.5f)for(int k=0;k<NativeHordeFx.Splashes;k+=low?2:1)if(NativeHordeFx.Splash(h.Seed,k,since,out var o,out float sz,out float sa))draw.Cube(draw.Solid,chest+new Vector3(o.X,o.Y*.6f,o.Z)*.7f,Quaternion.identity,Vector3.one*sz,new Color(.07f,.14f,.03f),NativeFxDraw.Authored);
                if(since<.16f)draw.Sprite(draw.Effects,chest,right,up,.9f*(1-since/.16f),Alpha(acid,.7f*(1-since/.16f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
                // The corrosion on the body: a green sheen on the chest, shoulders and head, sizzling smoke, drips.
                for(int b=0;b<4;b++)
                {
                    Vector3 at=b==0?chest:b==1?chest+Vector3.up*.42f:chest+right*(b==2?.24f:-.24f)+Vector3.up*.18f;float k2=.5f+.5f*Mathf.Sin(since*9+b*1.7f);
                    draw.Sprite(draw.Effects,at,right,up,.22f+.06f*k2,Alpha(acid,.3f*fade*k2),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    if(!low&&NativeHordeFx.Sizzle(h.Seed,b,since,.12f,out var so,out float ss,out float sa))draw.Sprite(draw.Effects,at+new Vector3(so.X,so.Y,so.Z),right,up,ss*1.6f,Alpha(new Color(.55f,.62f,.45f),sa*fade),NativeFxDraw.Authored);
                    if(NativeHordeFx.Drool(h.Seed^(uint)b,b,since+.06f,AcidHitSeconds,out var dp,out float ds,out float dpa))draw.Sprite(draw.Effects,at+new Vector3(dp.X,dp.Y,dp.Z),right,up,ds*2.2f,Alpha(acid,dpa*fade),NativeFxDraw.Authored+NativeFxDraw.Additive);
                }
            }
        }
        // The interrupted spits: the acid dribbles off the jaw to the floor and fizzles out there, harmless.
        private void HordeFizzlesDraw(NativeFxDraw draw,float now,Vector3 right,Vector3 up,bool low,Color acid)
        {
            for(int i=0;i<hordeFizzles.Length;i++)
            {
                ref var f=ref hordeFizzles[i];if(!f.Live)continue;float since=now-f.At;if(since>NativeHordeFx.FizzleSeconds){f.Live=false;continue;}if(since<0)continue;
                float drop=Mathf.Max(.1f,f.Mouth.y-f.Floor);
                for(int k=0;k<NativeHordeFx.FizzleDrops;k+=low?2:1)if(NativeHordeFx.Fizzle(f.Seed,k,since,drop,out var o,out float sz,out float al))draw.Sprite(draw.Effects,f.Mouth+new Vector3(o.X,o.Y,o.Z),right,up,sz*2.4f,Alpha(acid,al),NativeFxDraw.Authored+NativeFxDraw.Additive);
                Vector3 c=new Vector3(f.Mouth.x,f.Floor+.012f,f.Mouth.z);
                if(NativeHordeFx.FizzlePool(since,out float r,out float pa)){draw.Ring(draw.Effects,c,0,r,Alpha(acid,pa*.35f),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Ring(draw.Effects,c+Vector3.up*.002f,r*.85f,r,Alpha(acid,pa*.6f),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    for(int k=0;k<3;k++)if(NativeHordeFx.Sizzle(f.Seed,k,since,r,out var so,out float ss,out float sa))draw.Sprite(draw.Effects,c+new Vector3(so.X,so.Y*.6f,so.Z),right,up,ss*1.3f,Alpha(new Color(.55f,.62f,.45f),sa*pa),NativeFxDraw.Authored);}
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
