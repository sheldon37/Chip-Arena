using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Siren zombies' emergence and slam effects (owner 2026-10-12: "Brutes can be big muscle versions, that knockbacks and
    // slam"; 2026-11-21: "rework zombie spawn animation ... Can we animate them digging out of the ground? Or have them laying
    // there, and just rising? The hit indicator from the old drop in effect is massive and inaccurate, so I want to remove that
    // drop in animation entirely"). Presentation only, from what host, guests and the killcam all hold:
    //  - The emergence (Core/NativeHordeEmerge, an add whose replicated entry emerges: a drop entry, a ceiling vent, a door with
    //    no opening), on its entry's clock: the ground cracking and bulging a beat before, then the dig-out's broken ground
    //    (plates thrown aside, clods, toon dust, grit, the hole) or the lie-and-rise's stain (NativeZombieEmergeDraw), a heavy
    //    kind's bigger with a shake at the break and a scar that stays a while. No ring, no ink tear, no slam: the drop-in's are
    //    gone (and the rules' landing slam with them).
    //  - The smashes: dust thrown round the authority's disc and cracks run out of it at the strike, and a shake for pilots near
    //    it; a pilot inside the disc (whom the authority knocks back) also gets a kick of the view up and back, so the shove is
    //    felt (the Hulk's rolled and bigger). Settings · camera shake gates all of them (Reduced effects halves them).
    //  - The Hulk's footfalls shake the ground a little and leave cracks round its feet; its death crashes down with a thud of
    //    dust and a shake.
    // High-quality pass (owner 2026-10-14: "Slam/landing/wall break [effects] also needs improvements from hulk/brute"): every
    // strike, landing and crash is one impact (Core/NativeZombieFx.Impact on the authority's radius: the smash's disc, the drop
    // profile's slam radius) held where it struck and drawn by Runtime/NativeZombieFxDraw.cs: a flash, the upright slam wave to
    // the radius (never a flat ring), a crushed patch and jagged branching cracks with a hot seam, toon dust rolling out low over
    // the floor, a dust column, lit rubble thrown on arcs that bounces and comes to rest inside the radius, sparks; the Hulk's
    // landing a second wave and its crater's rubble rim. Timed only from the replicated clocks (LastAttack + hit, LandAt), so
    // the body's animation (its own branch) and these effects meet on the same marks.
    // Fixed pools: no allocation per frame.
    public sealed partial class ArenaPresentation
    {
        private struct HordeEmergeRec{public bool Live;public int Id,Kind;public Vector3 At;public float SpawnAt,Scale;public NativeHordeEmerge.Marks Marks;public NativeHordeEmerge.Style Style;public uint Seed;}
        private readonly HordeEmergeRec[] hordeEmerges=new HordeEmergeRec[48];
        private struct HordeLanding{public bool Live,Heavy;public int Id;public Vector3 At;public float Born,Radius;public uint Seed;public NativeZombieFx.Impact Profile;public int Ink;}
        private readonly HordeLanding[] hordeLandings=new HordeLanding[16];
        private NativeZombieFxDraw zombieFx;
        private readonly NativeHordeFx.Shake[] hordeShakes=new NativeHordeFx.Shake[NativeHordeFx.ShakeSlots];
        /// <summary>The camera's slam shake this frame is read against (the arena's local pilot, for the kick inside a disc).</summary>
        private Vector3 hordeLocalAt;private bool hordeLocalKnown;
        private struct HordeStompMark{public bool Live;public Vector3 At;public float Born;public uint Seed;}
        private readonly HordeStompMark[] hordeStomps=new HordeStompMark[24];private int hordeStompNext;
        private struct HordeJolt{public bool Live;public NativePropBody Prop;public Renderer Renderer;public MeshFilter Filter;public Material[] Materials;public float Born,Power;public Vector3 Push;public uint Seed;}
        private readonly HordeJolt[] hordeJolts=new HordeJolt[24];
        public const float JoltSeconds=.75f;

        /// <summary>HordeKindAdd and the galleries: one emerging add this frame (its spot on the floor, its replicated SpawnAt and
        /// marks): its ground effects are remembered by the add's id and SpawnAt (so its scar stays after it has risen) and a heavy
        /// kind's breaking-through shakes the ground once (the brute's a little, the Hulk's hard, falling off with distance).</summary>
        private void HordeEmergeJob(int id,int kind,Vector3 at,float spawnAt,in NativeHordeEmerge.Marks marks,NativeHordeEmerge.Style style,uint seed,float scale)
        {
            int o=-1;
            for(int i=0;i<hordeEmerges.Length;i++){ref var r=ref hordeEmerges[i];if(r.Live&&r.Id==id&&Mathf.Abs(r.SpawnAt-spawnAt)<.05f){r.At=at;r.Marks=marks;r.Style=style;r.Kind=kind;r.Scale=scale;o=i;break;}}
            if(o<0)
            {
                for(int i=0;i<hordeEmerges.Length;i++){if(!hordeEmerges[i].Live){o=i;break;}if(o<0||hordeEmerges[i].SpawnAt<hordeEmerges[o].SpawnAt)o=i;}
                hordeEmerges[o]=new HordeEmergeRec{Live=true,Id=id,Kind=kind,At=at,SpawnAt=spawnAt,Scale=scale,Marks=marks,Style=style,Seed=seed};
            }
            if(style!=NativeHordeEmerge.Style.Dig)return;var look=NativeHordeEmerge.LookOf(kind);if(!(look.Shake>0))return;
            // The heavy kinds' break and haul shake the ground near them (Settings · camera shake gates it; Reduced effects halves it).
            float range=kind==NativeHordeKinds.Hulk?26:12;
            for(int b=0;b<2;b++){float born=spawnAt+(b==0?marks.Break:marks.Torso-.15f);if(hordeClock>=born&&hordeClock-born<.4f)NativeHordeFx.AddShake(hordeShakes,ToN(at),born,look.Shake*(b==0?1:.6f),range,0,seed^(uint)(b*7919),kind==NativeHordeKinds.Hulk?.9f:NativeHordeFx.ShakeSeconds,0);}
        }
        /// <summary>The emergence's style for an add (Core NativeHordeEmerge.Choose: open ground digs out; inside, under a ceiling vent
        /// or in the bleed most lie and rise; the heavy kinds dig), picked once per add and SpawnAt from facts every peer shares.</summary>
        private NativeHordeEmerge.Style HordeEmergeStyle(NativeArena arena,HordeKindState s,int kind,PveEntry e,Vector3 spot,uint seed,float spawnAt)
        {
            if(s.EmergeStyle>=0&&s.EmergeFor==spawnAt)return (NativeHordeEmerge.Style)s.EmergeStyle;
            bool interior=!NativeHordeEmerge.Outdoor(arena?.MapId)||e?.surface=="ceiling",bleed=false;var theme=NativeMeridianDress.ThemeAt;
            if(theme!=null)bleed=theme(new V3(spot.x,spot.y,spot.z))!=null;
            var style=NativeHordeEmerge.Choose(interior,bleed,NativeHordeEmerge.Heavy(kind),seed);s.EmergeStyle=(int)style;s.EmergeFor=spawnAt;return style;
        }
        /// <summary>An impact (a smash's strike, a landing, a crash) at `at` on the floor, born at `born`: kept where it struck until
        /// its marks are gone; the same one twice (an add's id and its birth) is ignored, so the first sight fixes its place.</summary>
        private void HordeImpactAt(int id,Vector3 at,float born,uint seed,in NativeZombieFx.Impact p,int ink)
        {
            int o=0;for(int i=0;i<hordeLandings.Length;i++){if(hordeLandings[i].Live&&hordeLandings[i].Id==id&&Mathf.Abs(hordeLandings[i].Born-born)<.1f)return;}
            for(int i=0;i<hordeLandings.Length;i++){if(!hordeLandings[i].Live){o=i;break;}if(hordeLandings[i].Born<hordeLandings[o].Born)o=i;}
            hordeLandings[o]=new HordeLanding{Live=true,Heavy=p.Heavy,Id=id,At=at,Born=born,Seed=seed,Radius=p.Radius,Profile=p,Ink=ink};
        }
        /// <summary>The smash's strike (DrawHordeKindFx, once per blow): the shake round the disc and the kick for a pilot in it.</summary>
        private void HordeSmashShake(Vector3 center,float born,uint seed,float radius,bool hulk=false)
        {
            bool inside=hordeLocalKnown&&new Vector2(hordeLocalAt.x-center.x,hordeLocalAt.z-center.z).magnitude<=radius&&Mathf.Abs(hordeLocalAt.y-center.y)<(hulk?3:1.8f);
            if(hulk){NativeHordeFx.AddShake(hordeShakes,ToN(center),born,inside?.16f:.08f,20,inside?6:0,seed,inside?.95f:.7f,inside?5:0);return;}
            float kick=inside?3.2f:0;NativeHordeFx.AddShake(hordeShakes,ToN(center),born,kick>0?.09f:.045f,9,kick,seed);
        }
        /// <summary>ConversionGame, with the structure shake: the slams' shake and kick on the live local view (`scale` the setting's).</summary>
        public void HordeShake(Transform view,float scale)
        {
            if(view==null||!(scale>0))return;var off=NativeHordeFx.ShakeAt(hordeShakes,ToN(view.position),hordeClock,out float pitch,out float roll);
            view.position+=new Vector3(off.X,off.Y,off.Z)*scale;if(pitch!=0||roll!=0)view.rotation*=Quaternion.Euler(-pitch*scale,0,roll*scale);
        }
        private void HordeLocal(NativeArena arena)
        {
            hordeLocalKnown=false;if(arena==null)return;var list=arena.Pilots;
            for(int i=0;i<list.Count;i++){var p=list[i];if(p.Seat.id!=arena.LocalId||p.Body==null||p.Hp<=0)continue;hordeLocalAt=p.Body.position;hordeLocalKnown=true;break;}
        }
        /// <summary>A Hulk's footfall (HordeDust): a short thump of shake and cracks round the foot.</summary>
        private void HordeStomp(Vector3 at,float now,uint seed)
        {
            hordeStomps[hordeStompNext++%hordeStomps.Length]=new HordeStompMark{Live=true,At=at,Born=now,Seed=seed};
            NativeHordeFx.AddShake(hordeShakes,ToN(at),now,.03f,14,0,seed,.3f,0);
        }
        /// <summary>A Hulk's death hitting the floor (HordeKindDeath, once): a thud of dust, cracks and a shake.</summary>
        private void HordeCrash(int id,Vector3 floor,float born,uint seed)
        {
            for(int i=0;i<hordeLandings.Length;i++)if(hordeLandings[i].Live&&hordeLandings[i].Id==id&&Mathf.Abs(hordeLandings[i].Born-born)<.1f)return;
            HordeImpactAt(id,floor,born,seed,NativeZombieFx.Crash,NativeHordeKinds.Seam[NativeHordeKinds.Hulk]);NativeHordeFx.AddShake(hordeShakes,ToN(floor),born,.1f,16,0,seed,.8f,0);
        }
        // The props near a heavy landing jolt: drawn jumping and settling (their renderer hidden meanwhile), their colliders untouched.
        private void HordeJoltProps(Vector3 at,float radius,float born,uint seed)
        {
            var arena=hordeKindArena;if(arena==null)return;JoltNear(arena.Props,at,radius,born,seed);JoltNear(arena.StaticBlocks,at,radius,born,seed);
        }
        private void JoltNear(System.Collections.Generic.List<NativePropBody> props,Vector3 at,float radius,float born,uint seed)
        {
            for(int i=0;i<props.Count;i++)
            {
                var p=props[i];if(p==null||!p.isActiveAndEnabled)continue;var d=p.transform.position-at;d.y=0;float dist=d.magnitude;if(dist>radius)continue;
                var r=p.GetComponent<MeshRenderer>();var f=p.GetComponent<MeshFilter>();if(r==null||f==null||f.sharedMesh==null||!r.enabled)continue;
                int o=-1;for(int k=0;k<hordeJolts.Length;k++)if(!hordeJolts[k].Live){o=k;break;}if(o<0)return;
                r.enabled=false;hordeJolts[o]=new HordeJolt{Live=true,Prop=p,Renderer=r,Filter=f,Materials=r.sharedMaterials,Born=born,Power=1-dist/radius,Push=dist>1e-3f?d/dist:Vector3.forward,Seed=seed^(uint)(i*7919)};
            }
        }
        /// <summary>SourceWorld (HordeKindsEnd): the jolted props drawn at their jolt, given back when it settles.</summary>
        private void HordeJoltsDraw(float now)
        {
            for(int i=0;i<hordeJolts.Length;i++)
            {
                ref var j=ref hordeJolts[i];if(!j.Live)continue;float t=now-j.Born;
                if(j.Prop==null||j.Renderer==null||j.Filter==null||t>JoltSeconds||t<-1){if(j.Renderer!=null)j.Renderer.enabled=true;j=default;continue;}
                float e=Mathf.Max(0,t),hop=j.Power*.22f*Mathf.Sin(Mathf.Min(1,e/.3f)*Mathf.PI)*Mathf.Exp(-e*2.2f)+j.Power*.04f*Mathf.Sin(e*38)*Mathf.Exp(-e*6),tilt=j.Power*9*Mathf.Sin(e*24)*Mathf.Exp(-e*5);
                var axis=Vector3.Cross(Vector3.up,j.Push);var tr=j.Prop.transform;
                var m=Matrix4x4.Translate(Vector3.up*hop)*Matrix4x4.Translate(tr.position)*Matrix4x4.Rotate(Quaternion.AngleAxis(tilt,axis))*Matrix4x4.Translate(-tr.position)*tr.localToWorldMatrix;
                var mats=j.Materials;for(int s=0;s<mats.Length&&s<j.Filter.sharedMesh.subMeshCount;s++)if(mats[s]!=null)Graphics.DrawMesh(j.Filter.sharedMesh,m,mats[s],j.Prop.gameObject.layer,null,s,null,j.Renderer.shadowCastingMode,j.Renderer.receiveShadows);
            }
        }

        /// <summary>DevEffects (from DrawHordeKindFx): the emergences' ground and the impacts.</summary>
        private void DrawHordeEmerges(NativeFxDraw draw,float now,Vector3 right,Vector3 up,bool low)
        {
            var z=zombieFx;
            // The emergences (NativeZombieEmergeDraw): each on its add's replicated clock, kept until its scar or stain has gone.
            for(int i=0;i<hordeEmerges.Length;i++)
            {
                ref var r=ref hordeEmerges[i];if(!r.Live)continue;float age=now-r.SpawnAt;var look=NativeHordeEmerge.LookOf(r.Kind);bool rise=r.Style==NativeHordeEmerge.Style.Rise;
                if(age>NativeHordeEmerge.Life(r.Marks,look,rise)||age<-5){r.Live=false;continue;}
                if(z!=null&&z.Within(Vector3.Distance(z.Eye,r.At),look.Heavy))z.Emerge(r.At,age,r.Marks,r.Style,look,r.Scale,r.Seed);
            }
            // The impacts: the smashes, a Rigger's landing and a Hulk's crash, each where it struck, on its own profile (NativeZombieFxDraw).
            for(int i=0;i<hordeLandings.Length;i++)
            {
                var l=hordeLandings[i];if(!l.Live)continue;float since=now-l.Born,stay=NativeZombieFx.Life(l.Profile);if(since<-.05f||since>stay){if(since>stay)hordeLandings[i].Live=false;continue;}
                if(z!=null&&z.Within(Vector3.Distance(z.Eye,l.At),l.Heavy))z.Impact(l.At+Vector3.up*.012f,since,l.Seed,l.Profile,l.Ink);
            }
        }
        /// <summary>The shove a Hulk's smash gives the pilots in its disc (they fly; the authority throws them): a burst at their feet
        /// and a streak along the push.</summary>
        private void HordeShove(NativeFxDraw draw,NativeArena arena,Vector3 center,float radius,float since,Vector3 fwd,Vector3 right,Vector3 up)
        {
            if(arena==null||since<0||since>.4f)return;var list=arena.Pilots;float k=1-since/.4f;
            for(int i=0;i<list.Count;i++)
            {
                var p=list[i];if(p.Hp<=0||p.Body==null)continue;var d=p.Body.position-center;float dy=d.y;d.y=0;if(d.magnitude>radius||Mathf.Abs(dy)>3)continue;
                var dir=d.sqrMagnitude>1e-4f?d.normalized:fwd;var feet=p.Body.position-Vector3.up*.8f;
                draw.Ring(draw.Effects,feet+Vector3.up*.02f,.3f+since*2,.5f+since*3,new Color(.9f,.8f,.7f,.5f*k),NativeFxDraw.Authored+NativeFxDraw.Additive);
                for(int s=0;s<3;s++){var a=p.Body.position+Vector3.up*(-.3f+.35f*s)-dir*(.2f+since*2);var b=a-dir*(.9f*k);var w=Vector3.Cross(dir,Vector3.up)*.03f;draw.Quad(draw.Effects,a-w,b-w*.2f,b+w*.2f,a+w,new Color(1,.9f,.8f,.45f*k),NativeFxDraw.Authored+NativeFxDraw.Additive);}
            }
        }
        /// <summary>The Hulk's footfall cracks (DevEffects; decals, NativeZombieFxDraw.StompCracks).</summary>
        private void DrawHordeStomps(NativeFxDraw draw,float now,bool low)
        {
            var z=zombieFx;
            for(int i=0;i<hordeStomps.Length;i++)
            {
                ref var s=ref hordeStomps[i];if(!s.Live)continue;float since=now-s.Born;if(since>NativeHordeFx.StompSeconds||since<-1){s.Live=false;continue;}
                if(z!=null&&z.Within(Vector3.Distance(z.Eye,s.At),true))z.StompCracks(s.At,since,s.Seed);
            }
        }
        /// <summary>A new arena or a rewind: the emergences, landings, shakes and jolts in flight are forgotten (the props given back).</summary>
        private void ClearHordeZombieFx()
        {
            for(int i=0;i<hordeEmerges.Length;i++)hordeEmerges[i].Live=false;for(int i=0;i<hordeLandings.Length;i++)hordeLandings[i].Live=false;for(int i=0;i<hordeShakes.Length;i++)hordeShakes[i].Live=false;for(int i=0;i<hordeStomps.Length;i++)hordeStomps[i].Live=false;
            for(int i=0;i<hordeJolts.Length;i++){if(hordeJolts[i].Live&&hordeJolts[i].Renderer!=null)hordeJolts[i].Renderer.enabled=true;hordeJolts[i]=default;}
        }
    }
}
