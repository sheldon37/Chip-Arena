using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Siren zombies' entrance and slam effects (owner 2026-10-12: "Brutes can be big muscle versions, that knockbacks and
    // slam. For now, we will have them enter from the middle of the map by jumping down and creating a small slam effect on
    // their landing"; 2026-10-13, the Hulk: "huge knockback ... The jump in too, shaking the ground and a even bigger stage effect
    // when landing"). Presentation only, from what host, guests and the killcam all hold:
    //  - The heavy kinds' jump-down (Core/NativeHordeDrop on the kind's profile, an add whose replicated entry is a map entry of
    //    kind "drop"): the landing spot's ink ring closing in, the ink tear above it the add leaps out of, and at touchdown the
    //    slam: a shockwave ring, cracks, thrown debris, a dust ring and an ink splash, an existing-bank thud and a camera shake
    //    for a pilot near it. The Hulk's warning is wider and longer and rumbles the ground; its landing craters the floor,
    //    throws a wall of dust, cracks the floor far out, jolts the props near it (drawn jolted, presentation only: their colliders
    //    never move) and shakes the camera hard and long, falling off with distance. Map data places the drop, never this file.
    //  - The smashes: dust thrown round the authority's disc and cracks run out of it at the strike, and a shake for pilots near
    //    it; a pilot inside the disc (whom the authority knocks back) also gets a kick of the view up and back, so the shove is
    //    felt (the Hulk's rolled and bigger). Settings · camera shake gates all of them (Reduced effects halves them).
    //  - The Hulk's footfalls shake the ground a little and leave cracks round its feet; its death crashes down with a thud of
    //    dust and a shake.
    // Fixed pools: no allocation per frame.
    public sealed partial class ArenaPresentation
    {
        private struct HordeDropDraw{public int Id,Kind;public Vector3 Land;public float Age,Span,Height;public uint Seed;}
        private readonly HordeDropDraw[] hordeDrops=new HordeDropDraw[8];private int hordeDropCount;
        private struct HordeLanding{public bool Live,Heavy;public int Id;public Vector3 At;public float Born,Radius;public uint Seed;}
        private readonly HordeLanding[] hordeLandings=new HordeLanding[8];
        private readonly NativeHordeFx.Shake[] hordeShakes=new NativeHordeFx.Shake[NativeHordeFx.ShakeSlots];
        /// <summary>The camera's slam shake this frame is read against (the arena's local pilot, for the kick inside a disc).</summary>
        private Vector3 hordeLocalAt;private bool hordeLocalKnown;
        private struct HordeStompMark{public bool Live;public Vector3 At;public float Born;public uint Seed;}
        private readonly HordeStompMark[] hordeStomps=new HordeStompMark[24];private int hordeStompNext;
        private struct HordeJolt{public bool Live;public NativePropBody Prop;public Renderer Renderer;public MeshFilter Filter;public Material[] Materials;public float Born,Power;public Vector3 Push;public uint Seed;}
        private readonly HordeJolt[] hordeJolts=new HordeJolt[24];
        public const float JoltSeconds=.75f;

        /// <summary>HordeKindAdd and the gallery: one dropping add this frame (its touchdown spot, the entrance's age and span).</summary>
        private void HordeDropJob(int id,int kind,Vector3 land,float yaw,float height,float age,float span,uint seed)
        {
            var p=NativeHordeDrop.Profile(kind);
            if(hordeDropCount<hordeDrops.Length)hordeDrops[hordeDropCount++]=new HordeDropDraw{Id=id,Kind=kind,Land=land,Age=age,Span=span,Height=height,Seed=seed};
            // The Hulk's warning rumbles the ground under the spot as it builds.
            float rumble=NativeHordeDrop.Rumble(age,span,height,p);if(rumble>0)NativeHordeFx.AddShake(hordeShakes,ToN(land),hordeClock-(age%.3f),.018f*rumble,p.ShakeRange*.6f,0,seed^(uint)(int)(age/.3f),.32f,0);
            // Touchdown (once per drop): the slam's effects, the shake and the cue run on from here.
            float landAge=span-p.Recover;if(age<landAge||age>landAge+.6f)return;float born=hordeClock-(age-landAge);
            for(int i=0;i<hordeLandings.Length;i++)if(hordeLandings[i].Live&&hordeLandings[i].Id==id&&Mathf.Abs(hordeLandings[i].Born-born)<.1f)return;
            HordeLandAt(id,land,born,seed,p.SlamRadius,p.Heavy);
            NativeHordeFx.AddShake(hordeShakes,ToN(land),born,p.ShakePeak,p.ShakeRange,p.Heavy&&hordeLocalKnown&&Vector3.Distance(hordeLocalAt,land)<p.SlamRadius?p.ShakeKick:0,seed,p.Heavy?1.3f:NativeHordeFx.ShakeSeconds,p.Heavy?3:0);
            if(p.Heavy)HordeJoltProps(land,p.SlamRadius*1.6f,born,seed);
        }
        private void HordeLandAt(int id,Vector3 at,float born,uint seed,float radius,bool heavy)
        {
            int o=0;for(int i=0;i<hordeLandings.Length;i++){if(!hordeLandings[i].Live){o=i;break;}if(hordeLandings[i].Born<hordeLandings[o].Born)o=i;}
            hordeLandings[o]=new HordeLanding{Live=true,Heavy=heavy,Id=id,At=at,Born=born,Seed=seed,Radius=radius};
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
            HordeLandAt(id,floor,born,seed,2.6f,false);NativeHordeFx.AddShake(hordeShakes,ToN(floor),born,.1f,16,0,seed,.8f,0);
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

        /// <summary>DevEffects (from DrawHordeKindFx): the drops' telegraphs and the landings' slams.</summary>
        private void DrawHordeDrops(NativeFxDraw draw,float now,Vector3 right,Vector3 up,bool low)
        {
            var violet=NativeArena.ColorOf(NativeMinionFx.Violet);
            for(int i=0;i<hordeDropCount;i++)
            {
                var d=hordeDrops[i];var p=NativeHordeDrop.Profile(d.Kind);var ink=NativeArena.ColorOf(NativeHordeKinds.Seam[d.Kind]);float landAge=d.Span-p.Recover,k=p.SlamRadius/NativeHordeDrop.SlamRadius;
                if(NativeHordeFx.DropMark(d.Age,landAge,out float r,out float a,out float tear))
                {
                    var c=d.Land+Vector3.up*.015f;r*=k;draw.Ring(draw.Effects,c,r-.14f*k,r,Alpha(ink,a),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    draw.Ring(draw.Effects,c-Vector3.up*.004f,0,r*.82f,new Color(.02f,.01f,.03f,.45f*a),NativeFxDraw.Authored);
                    // The Hulk's: an inner ring pulsing in, cracks creeping out of the spot as the ground gives.
                    if(p.Heavy){float pulse=Mathf.Repeat(d.Age*1.6f,1);draw.Ring(draw.Effects,c+Vector3.up*.002f,r*(1-pulse)*.9f,r*(1-pulse)*.9f+.1f,Alpha(ink,a*(1-pulse)),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        HordeHeavyCracks(draw,c,Mathf.Min(d.Age,landAge)*.5f,d.Seed^0x2b,p.SlamRadius*.6f*Mathf.Clamp01(d.Age/Mathf.Max(.1f,landAge)),99,ink);}
                    // The ink tear it leaps out of, over the spot at the drop's height.
                    if(tear>.01f){var top=d.Land+Vector3.up*(d.Height+1.3f*k);draw.Sprite(draw.Effects,top,right,up,2.2f*k*tear,Alpha(violet,.55f*tear),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Sprite(draw.Effects,top,right,up,1.5f*k*tear,new Color(.01f,0,.02f,.85f*tear),NativeFxDraw.Authored);}
                }
            }
            hordeDropCount=0;
            for(int i=0;i<hordeLandings.Length;i++)
            {
                var l=hordeLandings[i];if(!l.Live)continue;float since=now-l.Born,stay=l.Heavy?NativeHordeFx.CraterSeconds:1.8f;if(since<-.05f||since>stay){if(since>stay)hordeLandings[i].Live=false;continue;}
                var c=l.At+Vector3.up*.02f;var ink=NativeArena.ColorOf(NativeHordeKinds.Seam[l.Heavy?NativeHordeKinds.Hulk:NativeHordeKinds.Brute]);
                if(l.Heavy){HordeHeavyLanding(draw,c,since,l.Seed,l.Radius,right,up,low,ink);continue;}
                if(NativeHordeFx.LandWave(since,out float rr,out float inner,out float al)){float k=l.Radius/NativeHordeDrop.SlamRadius;draw.Ring(draw.Effects,c,inner*k,rr*k,Alpha(new Color(1,.85f,.7f),al),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Ring(draw.Effects,c+Vector3.up*.003f,inner*.8f*k,rr*.82f*k,Alpha(ink,al*.6f),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    draw.Sprite(draw.Effects,c+Vector3.up*.35f,right,up,2.4f*(1-Mathf.Clamp01(since/.25f)),Alpha(ink,al),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                HordeSlamExtras(draw,c,since,l.Seed,l.Radius,right,up,low,ink);
                for(int k=0;k<NativeHordeFx.DebrisCount;k+=low?2:1)if(NativeHordeFx.LandDebris(l.Seed,k,since,out var o,out float sz,out float spin,out float da))draw.Cube(draw.Solid,c+new Vector3(o.X,o.Y+sz*.5f,o.Z),Quaternion.Euler(spin*57.3f,spin*40f,spin*17f),Vector3.one*sz*da,new Color(.07f,.06f,.09f),NativeFxDraw.Authored);
            }
        }
        // The Hulk's landing: a big shockwave and a second, the crater and its rubble rim, cracks far out, a wall of dust, debris.
        private void HordeHeavyLanding(NativeFxDraw draw,Vector3 c,float since,uint seed,float radius,Vector3 right,Vector3 up,bool low,Color ink)
        {
            if(NativeHordeFx.Wave(since,radius+1.5f,.8f,out float wr,out float wi,out float wa)){draw.Ring(draw.Effects,c+Vector3.up*.01f,wi,wr,Alpha(new Color(1,.86f,.72f),wa),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Ring(draw.Effects,c+Vector3.up*.012f,wi*.85f,wr*.82f,Alpha(ink,wa*.65f),NativeFxDraw.Authored+NativeFxDraw.Additive);}
            if(NativeHordeFx.Wave(since-.18f,radius*.75f,.6f,out float w2,out float i2,out float a2))draw.Ring(draw.Effects,c+Vector3.up*.014f,i2,w2,Alpha(ink,a2*.7f),NativeFxDraw.Authored+NativeFxDraw.Additive);
            if(since<.3f){float k=1-since/.3f;draw.Sprite(draw.Effects,c+Vector3.up*.6f,right,up,5.5f*k,Alpha(ink,k),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Sprite(draw.Effects,c+Vector3.up*.3f,right,up,3.5f*k,Alpha(new Color(1,.9f,.8f),.8f*k),NativeFxDraw.Authored+NativeFxDraw.Additive);}
            float cr=radius*.32f;
            if(NativeHordeFx.Crater(since,cr,out float r,out float ca)){draw.Ring(draw.Solid,c-Vector3.up*.008f,0,r,new Color(.025f*ca+.0f,.02f*ca,.03f*ca),NativeFxDraw.Authored);draw.Ring(draw.Effects,c-Vector3.up*.006f,r*.6f,r*.96f,Alpha(ink,ca*.25f*(1-Mathf.Clamp01(since/3))),NativeFxDraw.Authored+NativeFxDraw.Additive);
                for(int k=0;k<NativeHordeFx.CraterChunks;k+=low?2:1)if(NativeHordeFx.CraterChunk(seed,k,since,cr,out var co,out float cs,out float tilt,out float cal))draw.Cube(draw.Solid,c+new Vector3(co.X,co.Y,co.Z),Quaternion.Euler(tilt*57.3f,k*37,tilt*30),new Vector3(cs*1.3f,cs*.7f,cs),new Color(.08f,.07f,.09f),NativeFxDraw.Authored);}
            HordeHeavyCracks(draw,c,since,seed^0x77,radius*1.35f,NativeHordeFx.CraterSeconds,ink);
            for(int k=0;k<NativeHordeFx.DustWallCount;k+=low?2:1)if(NativeHordeFx.DustWall(seed,k,since,radius,out var o,out float sz,out float al))draw.Sprite(draw.Effects,c+new Vector3(o.X,o.Y,o.Z),right,up,sz*2,Alpha(new Color(.43f,.39f,.47f),al),NativeFxDraw.Authored);
            for(int k=0;k<NativeHordeFx.DebrisCount;k+=low?2:1)if(NativeHordeFx.HeavyDebris(seed^0x9e3,k,since,radius,out var o2,out float s2,out float spin,out float da))draw.Cube(draw.Solid,c+new Vector3(o2.X,o2.Y+s2*.5f,o2.Z),Quaternion.Euler(spin*57.3f,spin*40f,spin*17f),Vector3.one*s2*da,new Color(.07f,.06f,.09f),NativeFxDraw.Authored);
            HordeSlamExtras(draw,c,since,seed,radius*.7f,right,up,low,ink);
        }
        /// <summary>Long cracks run out of a heavy impact to `radius`, staying `stay` s (the seam's light in them at first).</summary>
        private void HordeHeavyCracks(NativeFxDraw draw,Vector3 c,float since,uint seed,float radius,float stay,Color seam)
        {
            for(int k=0;k<NativeHordeFx.BigCracks;k++)if(NativeHordeFx.BigCrack(seed,k,since,radius,stay,out float ang,out float len,out float ca))
            {
                var dir=new Vector3(Mathf.Cos(ang),0,Mathf.Sin(ang));var side=new Vector3(-dir.z,0,dir.x)*.045f;var p0=c+dir*.4f+Vector3.up*.005f;var bend=new Vector3(-dir.z,0,dir.x)*len*.12f*(k%2==0?1:-1);var mid=p0+dir*len*.55f+bend;var p1=p0+dir*len;
                draw.Quad(draw.Effects,p0-side,mid-side*.6f,mid+side*.6f,p0+side,new Color(.015f,.01f,.02f,ca),NativeFxDraw.Authored);draw.Quad(draw.Effects,mid-side*.6f,p1-side*.1f,p1+side*.1f,mid+side*.6f,new Color(.015f,.01f,.02f,ca),NativeFxDraw.Authored);
                if(since<.8f)draw.Quad(draw.Effects,p0-side*.4f,mid-side*.25f,mid+side*.25f,p0+side*.4f,Alpha(seam,ca*(1-since/.8f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
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
        /// <summary>The Hulk's footfall cracks (DevEffects).</summary>
        private void DrawHordeStomps(NativeFxDraw draw,float now,bool low)
        {
            for(int i=0;i<hordeStomps.Length;i++)
            {
                ref var s=ref hordeStomps[i];if(!s.Live)continue;float since=now-s.Born;if(since>NativeHordeFx.StompSeconds||since<-1){s.Live=false;continue;}
                for(int k=0;k<NativeHordeFx.StompCracks;k+=low?2:1)if(NativeHordeFx.StompCrack(s.Seed,k,since,out float ang,out float len,out float a))
                {var dir=new Vector3(Mathf.Cos(ang),0,Mathf.Sin(ang));var side=new Vector3(-dir.z,0,dir.x)*.03f;var p0=s.At+dir*.18f+Vector3.up*.006f;var p1=p0+dir*len;draw.Quad(draw.Effects,p0-side,p1-side*.15f,p1+side*.15f,p0+side,new Color(.015f,.01f,.02f,a),NativeFxDraw.Authored);}
            }
        }
        /// <summary>The slam's dust ring, ink splash and cracks round a centre (the smash's disc and the drop's landing).</summary>
        private void HordeSlamExtras(NativeFxDraw draw,Vector3 c,float since,uint seed,float radius,Vector3 right,Vector3 up,bool low,Color seam)
        {
            for(int k=0;k<NativeHordeFx.SlamDust;k+=low?2:1)if(NativeHordeFx.SlamPuff(seed,k,since,radius,out var o,out float sz,out float al))draw.Sprite(draw.Effects,c+new Vector3(o.X,o.Y,o.Z),right,up,sz*2,Alpha(new Color(.42f,.38f,.48f),al),NativeFxDraw.Authored);
            for(int k=0;k<NativeHordeFx.Cracks;k++)if(NativeHordeFx.Crack(seed,k,since,radius,out float ang,out float len,out float ca))
            {
                var dir=new Vector3(Mathf.Cos(ang),0,Mathf.Sin(ang));var side=new Vector3(-dir.z,0,dir.x)*.025f;var p0=c+dir*.25f+Vector3.up*.004f;var p1=p0+dir*len;
                draw.Quad(draw.Effects,p0-side,p1-side*.2f,p1+side*.2f,p0+side,new Color(.015f,.01f,.02f,ca),NativeFxDraw.Authored);
                if(since<.5f)draw.Quad(draw.Effects,p0-side*.5f,p1-side*.1f,p1+side*.1f,p0+side*.5f,Alpha(seam,ca*(1-since/.5f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
            }
        }
        /// <summary>A new arena or a rewind: the drops, landings, shakes and jolts in flight are forgotten (the props given back).</summary>
        private void ClearHordeZombieFx()
        {
            hordeDropCount=0;for(int i=0;i<hordeLandings.Length;i++)hordeLandings[i].Live=false;for(int i=0;i<hordeShakes.Length;i++)hordeShakes[i].Live=false;for(int i=0;i<hordeStomps.Length;i++)hordeStomps[i].Live=false;
            for(int i=0;i<hordeJolts.Length;i++){if(hordeJolts[i].Live&&hordeJolts[i].Renderer!=null)hordeJolts[i].Renderer.enabled=true;hordeJolts[i]=default;}
        }
    }
}
