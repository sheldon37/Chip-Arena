using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Siren zombies' entrance and slam effects (owner 2026-10-12: "Brutes can be big muscle versions, that knockbacks and
    // slam. For now, we will have them enter from the middle of the map by jumping down and creating a small slam effect on
    // their landing"). Presentation only, from what host, guests and the killcam all hold:
    //  - The brute's jump-down (Core/NativeHordeDrop, an add whose replicated entry is a map entry of kind "drop"): the landing
    //    spot's ink ring closing in, the ink tear above it the brute leaps out of, its shadow while it falls, and at touchdown
    //    a small slam: a shockwave ring, cracks, thrown debris, a dust ring and an ink splash, an existing-bank thud, and a short
    //    camera shake for a pilot near it. The landing point and height come from the entry (map data), never from here.
    //  - The brute's smash, upgraded: dust thrown round the authority's disc and cracks run out of it at the strike, and a
    //    shake for pilots near it; a pilot inside the disc (whom the authority knocks back) also gets a short kick of the view
    //    up and back, so the shove is felt. Settings · camera shake gates both (Reduced effects halves them).
    // Fixed pools: no allocation per frame.
    public sealed partial class ArenaPresentation
    {
        private struct HordeDropDraw{public int Id;public Vector3 Land;public float Age,Span,Height;public uint Seed;}
        private readonly HordeDropDraw[] hordeDrops=new HordeDropDraw[8];private int hordeDropCount;
        private struct HordeLanding{public bool Live;public int Id;public Vector3 At;public float Born;public uint Seed;}
        private readonly HordeLanding[] hordeLandings=new HordeLanding[8];
        private readonly NativeHordeFx.Shake[] hordeShakes=new NativeHordeFx.Shake[NativeHordeFx.ShakeSlots];
        /// <summary>The camera's slam shake this frame is read against (the arena's local pilot, for the kick inside a disc).</summary>
        private Vector3 hordeLocalAt;private bool hordeLocalKnown;

        /// <summary>HordeKindAdd and the gallery: one dropping brute this frame (its touchdown spot, the entrance's age and span).</summary>
        private void HordeDropJob(int id,int kind,Vector3 land,float yaw,float height,float age,float span,uint seed)
        {
            if(hordeDropCount<hordeDrops.Length)hordeDrops[hordeDropCount++]=new HordeDropDraw{Id=id,Land=land,Age=age,Span=span,Height=height,Seed=seed};
            // Touchdown (once per drop): the slam's effects, the shake and the cue run on from here.
            float landAge=span-NativeHordeDrop.Recover;if(age<landAge||age>landAge+.6f)return;float born=hordeClock-(age-landAge);
            for(int i=0;i<hordeLandings.Length;i++)if(hordeLandings[i].Live&&hordeLandings[i].Id==id&&Mathf.Abs(hordeLandings[i].Born-born)<.1f)return;
            int o=0;for(int i=0;i<hordeLandings.Length;i++){if(!hordeLandings[i].Live){o=i;break;}if(hordeLandings[i].Born<hordeLandings[o].Born)o=i;}
            hordeLandings[o]=new HordeLanding{Live=true,Id=id,At=land,Born=born,Seed=seed};
            NativeHordeFx.AddShake(hordeShakes,ToN(land),born,.07f,NativeHordeDrop.ShakeRange,0,seed);
        }
        /// <summary>The smash's strike (DrawHordeKindFx, once per blow): the shake round the disc and the kick for a pilot in it.</summary>
        private void HordeSmashShake(Vector3 center,float born,uint seed,float radius)
        {
            float kick=hordeLocalKnown&&new Vector2(hordeLocalAt.x-center.x,hordeLocalAt.z-center.z).magnitude<=radius&&Mathf.Abs(hordeLocalAt.y-center.y)<1.8f?3.2f:0;
            NativeHordeFx.AddShake(hordeShakes,ToN(center),born,kick>0?.09f:.045f,9,kick,seed);
        }
        /// <summary>ConversionGame, with the structure shake: the slams' shake and kick on the live local view (`scale` the setting's).</summary>
        public void HordeShake(Transform view,float scale)
        {
            if(view==null||!(scale>0))return;var off=NativeHordeFx.ShakeAt(hordeShakes,ToN(view.position),hordeClock,out float pitch);
            view.position+=new Vector3(off.X,off.Y,off.Z)*scale;if(pitch!=0)view.rotation*=Quaternion.Euler(-pitch*scale,0,0);
        }
        private void HordeLocal(NativeArena arena)
        {
            hordeLocalKnown=false;if(arena==null)return;var list=arena.Pilots;
            for(int i=0;i<list.Count;i++){var p=list[i];if(p.Seat.id!=arena.LocalId||p.Body==null||p.Hp<=0)continue;hordeLocalAt=p.Body.position;hordeLocalKnown=true;break;}
        }

        /// <summary>DevEffects (from DrawHordeKindFx): the drops' telegraphs and the landings' slams.</summary>
        private void DrawHordeDrops(NativeFxDraw draw,float now,Vector3 right,Vector3 up,bool low)
        {
            var ink=NativeArena.ColorOf(NativeHordeKinds.Seam[NativeHordeKinds.Brute]);var violet=NativeArena.ColorOf(NativeMinionFx.Violet);
            for(int i=0;i<hordeDropCount;i++)
            {
                var d=hordeDrops[i];float landAge=d.Span-NativeHordeDrop.Recover;
                if(NativeHordeFx.DropMark(d.Age,landAge,out float r,out float a,out float tear))
                {
                    var c=d.Land+Vector3.up*.015f;draw.Ring(draw.Effects,c,.86f,r,Alpha(ink,a),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    draw.Ring(draw.Effects,c-Vector3.up*.004f,0,r*.82f,new Color(.02f,.01f,.03f,.45f*a),NativeFxDraw.Authored);
                    // The ink tear it leaps out of, over the spot at the drop's height.
                    if(tear>.01f){var top=d.Land+Vector3.up*(d.Height+1.3f);draw.Sprite(draw.Effects,top,right,up,2.2f*tear,Alpha(violet,.55f*tear),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Sprite(draw.Effects,top,right,up,1.5f*tear,new Color(.01f,0,.02f,.85f*tear),NativeFxDraw.Authored);}
                }
            }
            hordeDropCount=0;
            for(int i=0;i<hordeLandings.Length;i++)
            {
                var l=hordeLandings[i];if(!l.Live)continue;float since=now-l.Born;if(since<-.05f||since>1.8f){if(since>1.8f)hordeLandings[i].Live=false;continue;}
                var c=l.At+Vector3.up*.02f;
                if(NativeHordeFx.LandWave(since,out float rr,out float inner,out float al)){draw.Ring(draw.Effects,c,inner,rr,Alpha(new Color(1,.85f,.7f),al),NativeFxDraw.Authored+NativeFxDraw.Additive);draw.Ring(draw.Effects,c+Vector3.up*.003f,inner*.8f,rr*.82f,Alpha(ink,al*.6f),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    draw.Sprite(draw.Effects,c+Vector3.up*.35f,right,up,2.4f*(1-Mathf.Clamp01(since/.25f)),Alpha(ink,al),NativeFxDraw.Authored+NativeFxDraw.Additive);}
                HordeSlamExtras(draw,c,since,l.Seed,NativeHordeDrop.SlamRadius,right,up,low,ink);
                for(int k=0;k<NativeHordeFx.DebrisCount;k+=low?2:1)if(NativeHordeFx.LandDebris(l.Seed,k,since,out var o,out float sz,out float spin,out float da))draw.Cube(draw.Solid,c+new Vector3(o.X,o.Y+sz*.5f,o.Z),Quaternion.Euler(spin*57.3f,spin*40f,spin*17f),Vector3.one*sz*da,new Color(.07f,.06f,.09f),NativeFxDraw.Authored);
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
        /// <summary>A new arena or a rewind: the drops, landings and shakes in flight are forgotten.</summary>
        private void ClearHordeZombieFx(){hordeDropCount=0;for(int i=0;i<hordeLandings.Length;i++)hordeLandings[i].Live=false;for(int i=0;i<hordeShakes.Length;i++)hordeShakes[i].Live=false;}
    }
}
