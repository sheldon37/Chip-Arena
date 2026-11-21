using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Every lamp of a city lit on the surfaces round it (owner, 2026-11-20, Crown Hill at night: "the light doesn't seem to be on the
    /// material properly? (Like this seems to be flat, in the air light)"; docs/expedition/MERIDIAN-DRESS.md "The light").
    /// The eight practical slots reach only the eight lamps nearest the eye (three on Low, three fewer again round the camp, whose kit
    /// lights take the weakest), so every other lamp showed only its flat pool disc and its beam in the air. This is the city's light
    /// table: each lamp's place, reach, colour and (for a flood) its aim, and a grid of <see cref="Cell"/> m cells over the city, each
    /// listing the lamps whose reach touches it (at most <see cref="PerCell"/>, the brightest first). ChipArena/StageLit
    /// (Resources/NativeCityLamps.cginc) reads the cell under each pixel and lights it from those lamps exactly as from a practical slot:
    /// the slots' fall-off (1 - (d/r)^2)^2, N.L, the same highlight, then the anime style's steps. The street dress, the street life, the
    /// bleed and the camp give their lamps (Runtime NativeCityLampLight); the slots keep carrying the nearest to the pilots and zombies,
    /// marked so StageLit takes those from the grid instead (never twice). Pure, deterministic, allocation free per frame once built.
    /// </summary>
    public sealed class NativeCityLamps
    {
        /// <summary>The grid's cell (m), the lamps a cell lists (four texels of four), a spot's light kept behind it.</summary>
        public const float Cell=8f;public const int PerCell=16,CellTexels=PerCell/4;public const float SpotFloor=.08f;
        /// <summary>The most lamps a table holds (the data textures' rows).</summary>
        public const int Capacity=4096;
        /// <summary>A lamp: the light's point, its reach (m), its colour (linear, times its intensity), and its aim (unit) with its
        /// lobe's power in <see cref="Spot"/> (0 lights all round) and the share it keeps off its aim in <see cref="Floor"/> (a flood's
        /// <see cref="SpotFloor"/>; a street lamp's reflector throws most of its light down and keeps half of it sideways).</summary>
        public struct Lamp{public V3 At;public float Range,R,G,B;public V3 Aim;public float Spot,Floor;}
        public readonly Lamp[] Lamps;public readonly float MinX,MinZ;public readonly int Nx,Nz;
        /// <summary>Each cell's lamps (Nx x Nz cells, PerCell each, -1 past the last).</summary>
        public readonly float[] Cells;
        /// <summary>Lamps a full cell could not list (their light there is lost; the checks hold it to 0) and the most a cell wanted.</summary>
        public readonly int Dropped,Fullest;
        private NativeCityLamps(Lamp[] lamps,float minX,float minZ,int nx,int nz,float[] cells,int dropped,int fullest){Lamps=lamps;MinX=minX;MinZ=minZ;Nx=nx;Nz=nz;Cells=cells;Dropped=dropped;Fullest=fullest;}

        /// <summary>The slots' fall-off at distance d of a reach r (NativeStageLit's practical slots and the grid's lamps).</summary>
        public static float Falloff(float d,float r){if(!(r>0))return 0;float q=d/r;if(q>=1)return 0;float a=1-q*q;return a*a;}
        /// <summary>An overhead lamp's reflector (a street lamp's bowl, a mast's lamp): its lobe straight down of power DownPower,
        /// keeping DownFloor of its light sideways.</summary>
        public const float DownPower=1.2f,DownFloor=.45f;
        /// <summary>An overhead lamp at `at`: `peak` (r, g, b) straight down, DownFloor of it sideways and up (a lamp's sideways light as a
        /// practical slot gave it all round is peak x DownFloor).</summary>
        public static Lamp Overhead(V3 at,float range,float r,float g,float b)=>new Lamp{At=at,Range=range,R=r,G=g,B=b,Aim=new V3(0,-1,0),Spot=DownPower,Floor=DownFloor};
        /// <summary>A lobe's share at the cosine `c` off its aim: `floor` all round, its cone (c^spot) toward its aim (spot 0: 1).</summary>
        public static float SpotOf(float c,float spot,float floor)=>spot<=0?1:floor+(1-floor)*(float)Math.Pow(Math.Max(0,c),spot);
        private static float Luma(in Lamp l)=>l.R*.2126f+l.G*.7152f+l.B*.0722f;

        /// <summary>The table for these lamps: the grid over their reach, each cell's lamps ranked by the most light they can give in
        /// it (their luminance times the fall-off at their nearest point of the cell, flat), at most PerCell.</summary>
        public static NativeCityLamps Build(IReadOnlyList<Lamp> lamps)
        {
            int n=Math.Min(lamps?.Count??0,Capacity);var list=new Lamp[n];for(int i=0;i<n;i++)list[i]=lamps[i];
            if(n==0)return new NativeCityLamps(list,0,0,1,1,new float[PerCell].Fill(-1),0,0);
            float x0=float.MaxValue,x1=float.MinValue,z0=float.MaxValue,z1=float.MinValue;
            foreach(var l in list){x0=Math.Min(x0,l.At.x-l.Range);x1=Math.Max(x1,l.At.x+l.Range);z0=Math.Min(z0,l.At.z-l.Range);z1=Math.Max(z1,l.At.z+l.Range);}
            float minX=(float)Math.Floor(x0/Cell)*Cell,minZ=(float)Math.Floor(z0/Cell)*Cell;int nx=Math.Max(1,(int)Math.Ceiling((x1-minX)/Cell)),nz=Math.Max(1,(int)Math.Ceiling((z1-minZ)/Cell));
            var want=new List<(float w,int i)>[nx*nz];
            for(int i=0;i<n;i++)
            {
                var l=list[i];if(!(l.Range>0))continue;float lum=Luma(l);
                int cx0=Math.Max(0,(int)Math.Floor((l.At.x-l.Range-minX)/Cell)),cx1=Math.Min(nx-1,(int)Math.Floor((l.At.x+l.Range-minX)/Cell));
                int cz0=Math.Max(0,(int)Math.Floor((l.At.z-l.Range-minZ)/Cell)),cz1=Math.Min(nz-1,(int)Math.Floor((l.At.z+l.Range-minZ)/Cell));
                for(int cz=cz0;cz<=cz1;cz++)for(int cx=cx0;cx<=cx1;cx++)
                {
                    float ax=minX+cx*Cell,az=minZ+cz*Cell;float dx=Math.Max(0,Math.Max(ax-l.At.x,l.At.x-(ax+Cell))),dz=Math.Max(0,Math.Max(az-l.At.z,l.At.z-(az+Cell)));
                    float d=(float)Math.Sqrt(dx*dx+dz*dz);if(d>=l.Range)continue;
                    int c=cx+cz*nx;(want[c]??=new List<(float,int)>()).Add((lum*Falloff(d,l.Range),i));
                }
            }
            var cells=new float[nx*nz*PerCell];int dropped=0,fullest=0;
            for(int c=0;c<want.Length;c++)
            {
                int o=c*PerCell;for(int k=0;k<PerCell;k++)cells[o+k]=-1;var w=want[c];if(w==null)continue;
                // the brightest first (ties by index: deterministic)
                w.Sort((a,b)=>a.w!=b.w?b.w.CompareTo(a.w):a.i.CompareTo(b.i));fullest=Math.Max(fullest,w.Count);
                for(int k=0;k<w.Count;k++){if(k<PerCell)cells[o+k]=w[k].i;else dropped++;}
            }
            return new NativeCityLamps(list,minX,minZ,nx,nz,cells,dropped,fullest);
        }
        /// <summary>The cell under (x, z), or -1 off the grid.</summary>
        public int CellAt(float x,float z)
        {
            int cx=(int)Math.Floor((x-MinX)/Cell),cz=(int)Math.Floor((z-MinZ)/Cell);
            return cx<0||cz<0||cx>=Nx||cz>=Nz?-1:cx+cz*Nx;
        }
        /// <summary>The light (linear rgb) a surface at p facing n takes from its cell's lamps at their levels (`level` per lamp, 1
        /// full; null all full): the shader's CityLamps without its highlight and the toon steps.</summary>
        public V3 Light(V3 p,V3 n,float[] level=null)
        {
            var sum=new V3();int c=CellAt(p.x,p.z);if(c<0)return sum;
            for(int k=0;k<PerCell;k++)
            {
                int i=(int)Cells[c*PerCell+k];if(i<0)break;var l=Lamps[i];var d=l.At-p;float len=Math.Max(d.Length,.0001f);
                float f=Falloff(len,l.Range);if(f<=0)continue;var dir=d/len;if(l.Spot>0)f*=SpotOf(-V3.Dot(dir,l.Aim),l.Spot,l.Floor);
                float ndl=Math.Max(0,V3.Dot(n,dir))*f*(level==null?1:level[i]);sum=sum+new V3(l.R*ndl,l.G*ndl,l.B*ndl);
            }
            return sum;
        }
        /// <summary>The same light summed over every lamp (the reference the grid must equal: nothing dropped, nothing out of reach).</summary>
        public V3 LightAll(V3 p,V3 n,float[] level=null)
        {
            var sum=new V3();
            for(int i=0;i<Lamps.Length;i++)
            {
                var l=Lamps[i];var d=l.At-p;float len=Math.Max(d.Length,.0001f);float f=Falloff(len,l.Range);if(f<=0)continue;var dir=d/len;
                if(l.Spot>0)f*=SpotOf(-V3.Dot(dir,l.Aim),l.Spot,l.Floor);float ndl=Math.Max(0,V3.Dot(n,dir))*f*(level==null?1:level[i]);sum=sum+new V3(l.R*ndl,l.G*ndl,l.B*ndl);
            }
            return sum;
        }
        /// <summary>The static data texture (2 texels a lamp, a row each): (x, y, z, reach), (aim x, y, z, the lobe's floor).</summary>
        public void PackData(float[] into)
        {
            for(int i=0;i<Lamps.Length;i++){var l=Lamps[i];int o=i*8;into[o]=l.At.x;into[o+1]=l.At.y;into[o+2]=l.At.z;into[o+3]=l.Range;into[o+4]=l.Aim.x;into[o+5]=l.Aim.y;into[o+6]=l.Aim.z;into[o+7]=l.Floor;}
        }
        /// <summary>The cell texture (4 texels a cell, Nx x 4 wide and Nz high): each cell's lamps as Cells holds them.</summary>
        public void PackCells(float[] into)
        {
            for(int cz=0;cz<Nz;cz++)for(int cx=0;cx<Nx;cx++){int c=cx+cz*Nx,o=(cz*Nx*CellTexels+cx*CellTexels)*4;for(int k=0;k<PerCell;k++)into[o+k]=Cells[c*PerCell+k];}
        }
        /// <summary>This frame's light texture (1 texel a lamp): its colour times its level (`level` per lamp, clamped 0..1), w a flood's
        /// cone power. Allocation free.</summary>
        public void PackLight(float[] level,float[] into)
        {
            for(int i=0;i<Lamps.Length;i++){var l=Lamps[i];float k=level==null?1:Math.Max(0,Math.Min(1,level[i]));int o=i*4;into[o]=l.R*k;into[o+1]=l.G*k;into[o+2]=l.B*k;into[o+3]=l.Spot;}
        }
    }
    internal static class NativeCityLampsArrays
    {
        public static float[] Fill(this float[] a,float v){for(int i=0;i<a.Length;i++)a[i]=v;return a;}
    }
}
