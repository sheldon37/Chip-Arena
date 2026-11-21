using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The zombies' emergence on the ground (owner 2026-11-21: "Can we animate them digging out of the ground? Or have them laying
    // there, and just rising?"): Core/NativeHordeEmerge's pure shapes through the shared effects renderer and the drawer's decal
    // layer, on the add's replicated clock (host, guests and the killcam draw the same).
    //  - Dig out: the tell (cracks creeping out of the spot, the floor's plates heaving), the break (the plates thrown aside, clods
    //    and a puff of toon dust as the first hand claws through, more as the head and shoulders come), grit trickling off the body
    //    as it hauls itself up, the dark hole it leaves; a heavy kind's (the brute's, the Hulk's) bigger and heavier, its hole and
    //    cracks staying as a scar a while.
    //  - Lie and rise: a dark stain seeps out under where the body surfaces, wet, then dries and fades after it has risen.
    // No ring, no telegraph disc (the old drop-in's was removed): the ground cracking a beat before is the only tell. Every stream
    // follows the tier (Reduced effects and the low tier halve them, the grit is the full tier's). No allocation per frame.
    public sealed partial class NativeZombieFxDraw
    {
        private static readonly Color Earth=L(0x4f4236),EarthDark=L(0x30271f),Slab=L(0x5d5853);
        /// <summary>One emergence whose spot is `c` (on the floor), `age` s after its SpawnAt, on its marks and style; `look` the
        /// kind's (Core NativeHordeEmerge.LookOf), `scale` the kind's drawn scale (the stain's reach).</summary>
        public void Emerge(Vector3 c,float age,in NativeHordeEmerge.Marks m,NativeHordeEmerge.Style style,in NativeHordeEmerge.Look look,float scale,uint seed)
        {
            if(vfx==null||age<0)return;
            if(style==NativeHordeEmerge.Style.Rise){EmergeStain(c,age,m,scale,seed);return;}
            EmergeDecals(c,age,m,look,seed);float floor=c.y;
            // The floor's plates heave with the bulge, then are thrown aside at the break and settle into the rim.
            int plates=N(look.Plates);
            for(int k=0;k<plates;k++)if(NativeHordeEmerge.Plate(seed,k,plates,age,m,look.Reach,out var o,out float tilt,out float s,out float left))
            {
                var at=c+U(o);var outward=new Vector3(at.x-c.x,0,at.z-c.z);var axis=Vector3.Cross(Vector3.up,outward.sqrMagnitude>1e-6f?outward.normalized:Vector3.forward);
                var turn=Quaternion.AngleAxis(tilt*Mathf.Rad2Deg,axis)*Quaternion.Euler(0,k*67+(float)(seed%90u),0);
                vfx.Block(at+Vector3.up*.02f,turn,new Vector3(s,look.Heavy?.08f:.05f,s*.8f)*left,Slab,.05f,.1f,Slab*.02f,Slab*.4f);
            }
            // Clods and dust at the break (the first hand) and again as the head and shoulders come through; heavier for a heavy kind.
            float burst=m.Break+(m.Torso-m.Break)*.48f,stay=m.Span-m.Break+(look.Heavy?4f:2.5f);int clods=N(look.Chunks),puffs=N(look.Dust);
            for(int b=0;b<2;b++)
            {
                float since=age-(b==0?m.Break:burst);if(since<0)continue;uint sd=b==0?seed:seed^0x7f4a1u;int n=b==0?Math.Max(2,clods/2):clods;
                for(int i=0;i<n;i++)if(NativeHordeEmerge.Clod(sd,i,n,since,look.Hole,look.Chunk,stay-(b==0?0:burst-m.Break),out var o,out float spin,out float s,out float left))
                    vfx.Rubble(c+U(o),Quaternion.Euler(spin*57.3f,i*41,spin*23),s*left,(int)(sd%997)+i,i%3==0?EarthDark:Earth,Earth*.02f);
                int d=b==0?Math.Max(2,puffs/2):puffs;
                for(int i=0;i<d;i++)if(NativeHordeEmerge.Dust(sd,i,d,since,look.Hole,out var o,out float s,out float a,out float u))
                    vfx.Smoke(c+U(o),Color.Lerp(DustDark,Earth,.35f+.4f*u),a,s,u,F(sd,400+i),floor);
            }
            // Grit trickling off the body as it hauls itself up (the full tier's).
            if(extras)for(int i=0;i<6;i++)if(NativeHordeEmerge.Grit(seed,i,age,m,look.Hole,out var o,out float a))
                vfx.Rubble(c+U(o),Quaternion.Euler(age*300+i*50,i*70,0),look.Chunk*.35f*a,(int)(seed%991)+i,EarthDark,Earth*.01f);
        }
        // The dig-out's floor marks: the cracks creeping out (they stay with a heavy kind's scar), the dark hole and its earth rim.
        private void EmergeDecals(Vector3 c,float age,in NativeHordeEmerge.Marks m,in NativeHordeEmerge.Look look,uint seed)
        {
            var g=c+Vector3.up*.006f;int n=NativeZombieFx.Edge;
            if(NativeHordeEmerge.Hole(age,m,look.Hole,look.Scar,out float hr,out float ha))
            {
                for(int k=0;k<n;k++){float a=k*Mathf.PI*2/n,r=hr*NativeZombieFx.BlotchEdge(seed^0x51u,k);var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));ringA[k]=g+d*r*.7f+Vector3.up*.001f;ringB[k]=g+d*r+Vector3.up*.001f;ringC[k]=g+d*r*1.6f+Vector3.up*.0005f;}
                Fan(g+Vector3.up*.001f,ringA,n,S(0x050403,ha),S(0x120d0a,ha*.95f),Authored);Band(ringA,ringB,n,S(0x120d0a,ha*.95f),S(0x2a1f17,ha*.7f),Authored);Band(ringB,ringC,n,S(0x2a1f17,ha*.7f),S(0x2a1f17,0),Authored);
            }
            float alpha=NativeHordeEmerge.CrackAlpha(age,m,look.Scar);if(alpha<=0)return;float grow=NativeHordeEmerge.Tell(age,m);int count=N(look.Cracks);
            for(int i=0;i<count;i++)
            {
                var prev=NativeHordeEmerge.CrackPoint(seed,i,count,0,grow,look.Reach,out float pw);
                for(int j=1;j<=NativeHordeEmerge.CrackSegments;j++)
                {
                    var next=NativeHordeEmerge.CrackPoint(seed,i,count,j,grow,look.Reach,out float nw);
                    Strip(g+new Vector3(prev.X,.002f,prev.Y),g+new Vector3(next.X,.002f,next.Y),pw,nw,Vector3.up,S(0x07060a,alpha),Authored);prev=next;pw=nw;
                }
            }
        }
        // The lie-and-rise stain: a dark, wet blot seeping out under the body (its ragged edge feathered out), drying after it rises.
        private void EmergeStain(Vector3 c,float age,in NativeHordeEmerge.Marks m,float scale,uint seed)
        {
            if(!NativeHordeEmerge.Stain(age,m,NativeHordeEmerge.StainReach(scale),out float r,out float a,out float wet))return;
            var g=c+Vector3.up*.006f;int n=Tier==NativeZombieFx.Low?NativeZombieFx.Edge/2:NativeZombieFx.Edge;
            for(int k=0;k<n;k++){float ang=k*Mathf.PI*2/n,e=r*NativeHordeEmerge.StainEdge(seed,k*(NativeZombieFx.Edge/n));var d=new Vector3(Mathf.Cos(ang),0,Mathf.Sin(ang));ringA[k]=g+d*e*.72f+Vector3.up*.001f;ringB[k]=g+d*e+Vector3.up*.001f;}
            Fan(g+Vector3.up*.001f,ringA,n,S(0x0d0605,a),S(0x1c0f0b,a*.92f),Authored);Band(ringA,ringB,n,S(0x1c0f0b,a*.92f),S(0x1c0f0b,0),Authored);
            // The wet sheen while it is fresh (a faint lighter core).
            if(extras&&wet>.05f){for(int k=0;k<n;k++){ringC[k]=g+(ringA[k]-g)*.45f+Vector3.up*.0015f;}Fan(g+Vector3.up*.0015f,ringC,n,S(0x3a2620,a*.35f*wet),S(0x3a2620,0),Authored);}
        }
    }
}
