using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A Backrooms level's layout (docs/BACKROOMS.md "The layout contract"): a grid of <see cref="Cell"/> m cells with a
    /// <see cref="Height"/> m ceiling, what lies on each cell (floor, pillar, the ceiling's lamps and vents, the lamps' circuit),
    /// and the walls on the cell edges (the outer border always walled). The generator fills it (Astra's; <see
    /// cref="NativeBackrooms.Preview"/> stands in until it lands) and the presentation draws whatever it says. Pure.
    /// </summary>
    public sealed class BackroomsLayout
    {
        public const float Cell=3,Height=3;
        public const byte Carpet=0,Linoleum=1,Pit=2;
        public const byte Open=0,Wall=1,Short=2;
        public const byte NoPillar=0,SmallPillar=1,LargePillar=2;
        public const byte Wallpaper=0,Plaster=1,White=2;
        public const byte On=0,Flicker=1,Dead=2;
        public readonly string Level;public readonly int Seed,Width,Depth;
        /// <summary>Per cell (<see cref="Index"/>): floor, pillar, wall finish (of its walls), lamp circuit; the ceiling's nine 1 m
        /// tile slots (bit tz*3+tx, tx and tz 0..2 along x and z) holding a lamp or a vent.</summary>
        public readonly byte[] Floor,Pillar,Finish,Circuit;public readonly ushort[] Lamps,Vents;
        /// <summary>Walls along z on the line x (0..Width) beside row z: <see cref="WallX"/>[(Width+1)*z+x]; walls along x on the line z
        /// (0..Depth) beside column x: <see cref="WallZ"/>[Width*z+x]. Open, Wall or Short (a 2 m wall and a 1 m gap).</summary>
        public readonly byte[] WallX,WallZ;
        /// <summary>Each circuit's state (On, Flicker, Dead).</summary>
        public readonly byte[] Circuits=new byte[256];
        public int EntryX,EntryZ,ExitX,ExitZ;
        public BackroomsLayout(string level,int seed,int width,int depth)
        {
            Level=level;Seed=seed;Width=Math.Max(2,width);Depth=Math.Max(2,depth);int n=Width*Depth;
            Floor=new byte[n];Pillar=new byte[n];Finish=new byte[n];Circuit=new byte[n];Lamps=new ushort[n];Vents=new ushort[n];
            WallX=new byte[(Width+1)*Depth];WallZ=new byte[Width*(Depth+1)];
            for(int z=0;z<Depth;z++){WallX[(Width+1)*z]=Wall;WallX[(Width+1)*z+Width]=Wall;}
            for(int x=0;x<Width;x++){WallZ[x]=Wall;WallZ[Width*Depth+x]=Wall;}
        }
        public int Index(int x,int z)=>x+Width*z;
        public bool Inside(int x,int z)=>x>=0&&z>=0&&x<Width&&z<Depth;
        /// <summary>The wall between cell (x, z) and its neighbour one step along x (+1 or -1) or z.</summary>
        public byte Between(int x,int z,int dx,int dz)
        =>dx!=0?WallX[(Width+1)*z+(dx>0?x+1:x)]:WallZ[Width*(dz>0?z+1:z)+x];
        public void Set(int x,int z,int dx,int dz,byte wall)
        {if(dx!=0)WallX[(Width+1)*z+(dx>0?x+1:x)]=wall;else WallZ[Width*(dz>0?z+1:z)+x]=wall;}
        /// <summary>Whether a body can cross from (x, z) one step: no full wall, and the neighbour inside and no large pillar.</summary>
        public bool Passable(int x,int z,int dx,int dz)=>Inside(x+dx,z+dz)&&Between(x,z,dx,dz)!=Wall&&Pillar[Index(x+dx,z+dz)]!=LargePillar;
    }

    public static class NativeBackrooms
    {
        public const string Level0="level-0";
        // ---- the stand-in generator (DEV preview; Astra's generator replaces it) ----
        private struct Rng{public uint S;public Rng(int seed){S=(uint)seed*2654435761u^0x9E3779B9u;if(S==0)S=1;}public float Next(){S^=S<<13;S^=S>>17;S^=S<<5;return (S&0xffffff)/16777216f;}public int Range(int n)=>(int)(Next()*n)%Math.Max(1,n);}
        /// <summary>
        /// A Level 0 layout for the DEV preview (deterministic from `seed`): straight wall runs with gaps and short walls, open
        /// rooms with scattered pillars, the fluorescent panels on a chequer of cells (a few dead or flickering by circuit), the odd
        /// vent; every open cell reachable from the entry.
        /// </summary>
        public static BackroomsLayout Preview(int seed,int width,int depth)
        {
            var l=new BackroomsLayout(Level0,seed,width,depth);var r=new Rng(seed);int w=l.Width,d=l.Depth;
            int runs=w*d/5;
            for(int i=0;i<runs;i++)
            {
                bool alongX=r.Next()<.5f;int len=1+r.Range(5);
                if(alongX){int z=1+r.Range(d-1),x=r.Range(w);for(int k=0;k<len&&x+k<w;k++){float g=r.Next();l.WallZ[w*z+x+k]=g<.16f?BackroomsLayout.Open:g<.28f?BackroomsLayout.Short:BackroomsLayout.Wall;}}
                else{int x=1+r.Range(w-1),z=r.Range(d);for(int k=0;k<len&&z+k<d;k++){float g=r.Next();l.WallX[(w+1)*(z+k)+x]=g<.16f?BackroomsLayout.Open:g<.28f?BackroomsLayout.Short:BackroomsLayout.Wall;}}
            }
            l.EntryX=1;l.EntryZ=1;l.ExitX=w-2;l.ExitZ=d-2;
            for(int z=0;z<d;z++)for(int x=0;x<w;x++)
            {
                int c=l.Index(x,z);bool walls=l.Between(x,z,1,0)!=0||l.Between(x,z,-1,0)!=0||l.Between(x,z,0,1)!=0||l.Between(x,z,0,-1)!=0;
                bool keep=(x==l.EntryX&&z==l.EntryZ)||(x==l.ExitX&&z==l.ExitZ);float p=r.Next();
                if(!walls&&!keep)l.Pillar[c]=p<.025f?BackroomsLayout.LargePillar:p<.11f?BackroomsLayout.SmallPillar:BackroomsLayout.NoPillar;
                // The panels: a chequer of cells with the centre tile lit (the long rows of Level 0), now and then a pair.
                if(((x+z)&1)==0)l.Lamps[c]=(ushort)(r.Next()<.15f?(1<<3)|(1<<5):1<<4);
                if(r.Next()<.06f)l.Vents[c]=(ushort)(1<<(r.Next()<.5f?0:8));
                l.Circuit[c]=(byte)((x/4+(z/4)*((w+3)/4))&255);
            }
            for(int i=0;i<l.Circuits.Length;i++){float s=r.Next();l.Circuits[i]=s<.07f?BackroomsLayout.Dead:s<.2f?BackroomsLayout.Flicker:BackroomsLayout.On;}
            Connect(l,ref r);return l;
        }
        // Opens walls until every cell without a large pillar is reachable from the entry.
        private static void Connect(BackroomsLayout l,ref Rng r)
        {
            int w=l.Width,d=l.Depth;var seen=new bool[w*d];var stack=new int[w*d];
            for(int guard=0;guard<w*d;guard++)
            {
                int unreached=Flood(l,seen,stack);if(unreached==0)return;
                // Every wall between a reached cell and an unreached open one, opened (a short wall stays a short wall: it has a gap).
                for(int z=0;z<d;z++)for(int x=0;x<w;x++)
                {
                    if(!seen[l.Index(x,z)])continue;
                    foreach(var (dx,dz) in Steps){int nx=x+dx,nz=z+dz;if(!l.Inside(nx,nz)||seen[l.Index(nx,nz)]||l.Pillar[l.Index(nx,nz)]==BackroomsLayout.LargePillar)continue;if(l.Between(x,z,dx,dz)==BackroomsLayout.Wall&&r.Next()<.5f)l.Set(x,z,dx,dz,BackroomsLayout.Open);}
                }
            }
        }
        private static readonly (int dx,int dz)[] Steps={(1,0),(-1,0),(0,1),(0,-1)};
        /// <summary>Marks the cells reachable from the entry; returns how many open cells (no large pillar) it did not reach.</summary>
        public static int Flood(BackroomsLayout l,bool[] seen,int[] stack)
        {
            Array.Clear(seen,0,seen.Length);int top=0,start=l.Index(l.EntryX,l.EntryZ);seen[start]=true;stack[top++]=start;
            while(top>0){int c=stack[--top];int x=c%l.Width,z=c/l.Width;foreach(var (dx,dz) in Steps)if(l.Passable(x,z,dx,dz)){int n=l.Index(x+dx,z+dz);if(!seen[n]){seen[n]=true;stack[top++]=n;}}}
            int left=0;for(int i=0;i<seen.Length;i++)if(!seen[i]&&l.Pillar[i]!=BackroomsLayout.LargePillar)left++;return left;
        }

        // ---- what to draw ----
        public enum Kind:byte{Floor,Ceiling,Lamp,Vent,Wall,ShortWall,SmallPillar,LargePillar,Post,Tile}
        /// <summary>One kit piece: its kind, where its footprint's centre stands on the floor (world), its turn about y (degrees) and
        /// its length along its own x as a share of the full piece (a short wall: 2/3).</summary>
        public struct Piece{public Kind Kind;public float X,Y,Z,Yaw,Length;public int Cell;}
        /// <summary>
        /// The kit pieces of the layout (its corner cell (0, 0) at `origin`) within `reach` m of the eye on the ground plane, into
        /// `o` (at most its length). Floors and ceilings per cell, a lamp or vent per tile slot just under the ceiling, pillars,
        /// walls on the cell edges (a short wall's gap on its far side), a post wherever walls end or turn, and a plain tile in
        /// each ceiling panel's open centre slot that holds no lamp or vent.
        /// </summary>
        public static int Plan(BackroomsLayout l,Vector3 origin,Vector3 eye,float reach,Piece[] o)
        {
            int n=0;float c=BackroomsLayout.Cell,r2=reach*reach;
            int x0=Math.Max(0,(int)Math.Floor((eye.X-origin.X-reach)/c)),x1=Math.Min(l.Width-1,(int)Math.Floor((eye.X-origin.X+reach)/c));
            int z0=Math.Max(0,(int)Math.Floor((eye.Z-origin.Z-reach)/c)),z1=Math.Min(l.Depth-1,(int)Math.Floor((eye.Z-origin.Z+reach)/c));
            bool Near(float x,float z){float dx=x-eye.X,dz=z-eye.Z;return dx*dx+dz*dz<=r2;}
            void Add(Kind k,float x,float y,float z,float yaw,float len,int cell){if(n<o.Length)o[n++]=new Piece{Kind=k,X=x,Y=y,Z=z,Yaw=yaw,Length=len,Cell=cell};}
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                float cx=origin.X+(x+.5f)*c,cz=origin.Z+(z+.5f)*c;if(!Near(cx,cz))continue;int i=l.Index(x,z);
                if(l.Floor[i]!=BackroomsLayout.Pit)Add(Kind.Floor,cx,origin.Y,cz,0,1,i);
                Add(Kind.Ceiling,cx,origin.Y+BackroomsLayout.Height,cz,0,1,i);
                // (The ceiling panel's centre slot is open: a lamp, a vent or a plain tile fills it.)
                if(((l.Lamps[i]|l.Vents[i])&(1<<4))==0)Add(Kind.Tile,cx,origin.Y+BackroomsLayout.Height,cz,0,1,i);
                for(int t=0;t<9;t++){int bit=1<<t;if(((l.Lamps[i]|l.Vents[i])&bit)==0)continue;float tx=cx+(t%3-1),tz=cz+(t/3-1);Add((l.Lamps[i]&bit)!=0?Kind.Lamp:Kind.Vent,tx,origin.Y+BackroomsLayout.Height,tz,0,1,i);}
                if(l.Pillar[i]!=BackroomsLayout.NoPillar)Add(l.Pillar[i]==BackroomsLayout.LargePillar?Kind.LargePillar:Kind.SmallPillar,cx,origin.Y,cz,0,1,i);
            }
            // Walls along x on the lines z, then along z on the lines x; a post at each grid point where walls end or turn.
            for(int z=z0;z<=z1+1;z++)for(int x=x0;x<=x1;x++)
            {
                byte wl=l.WallZ[l.Width*z+x];if(wl==BackroomsLayout.Open)continue;float px=origin.X+(x+.5f)*c,pz=origin.Z+z*c;if(!Near(px,pz))continue;
                int cell=l.Index(x,Math.Min(z,l.Depth-1));if(wl==BackroomsLayout.Short)Add(Kind.ShortWall,px-.5f,origin.Y,pz,0,2f/3,cell);else Add(Kind.Wall,px,origin.Y,pz,0,1,cell);
            }
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1+1;x++)
            {
                byte wl=l.WallX[(l.Width+1)*z+x];if(wl==BackroomsLayout.Open)continue;float px=origin.X+x*c,pz=origin.Z+(z+.5f)*c;if(!Near(px,pz))continue;
                int cell=l.Index(Math.Min(x,l.Width-1),z);if(wl==BackroomsLayout.Short)Add(Kind.ShortWall,px,origin.Y,pz-.5f,90,2f/3,cell);else Add(Kind.Wall,px,origin.Y,pz,90,1,cell);
            }
            for(int z=z0;z<=z1+1;z++)for(int x=x0;x<=x1+1;x++)
            {
                float px=origin.X+x*c,pz=origin.Z+z*c;if(!Near(px,pz))continue;
                bool e=x<l.Width&&l.WallZ[l.Width*z+x]!=BackroomsLayout.Open,w=x>0&&l.WallZ[l.Width*z+x-1]!=BackroomsLayout.Open;
                bool nn=z<l.Depth&&l.WallX[(l.Width+1)*z+x]!=BackroomsLayout.Open,s=z>0&&l.WallX[(l.Width+1)*(z-1)+x]!=BackroomsLayout.Open;
                int k=(e?1:0)+(w?1:0)+(nn?1:0)+(s?1:0);if(k==0||k==2&&(e&&w||nn&&s))continue;
                Add(Kind.Post,px,origin.Y,pz,0,1,l.Index(Math.Min(x,l.Width-1),Math.Min(z,l.Depth-1)));
            }
            return n;
        }

        // ---- the lamps' light ----
        /// <summary>A lamp's light level at `time` (0..1): steady on; dead off; a flickering circuit stutters (seeded by its cell),
        /// mostly on with dips and dead moments, never a strobe faster than 10 Hz.</summary>
        public static float Level(byte state,int cell,double time)
        {
            if(state==BackroomsLayout.On)return 1;if(state==BackroomsLayout.Dead)return 0;
            double t=time*7+cell*1.37;int k=(int)Math.Floor(t);float h=Hash(cell,k);
            return h<.12f?0:h<.3f?.35f+.4f*(float)(t-k):1;
        }
        private static float Hash(int a,int b){unchecked{uint h=(uint)a*2246822519u^(uint)b*3266489917u;h^=h>>15;h*=2654435761u;h^=h>>13;return (h&0xffffff)/16777216f;}}
        /// <summary>The `max` lit lamps nearest the eye (positions and ranges into `pos`, x y z range; linear colours times their level
        /// into `rgb`); the rest of the slots dark. Returns how many are lit.</summary>
        public static int NearLamps(BackroomsLayout l,Vector3 origin,Vector3 eye,double time,int max,float[] pos,float[] rgb,Vector3 color,float range)
        {
            Span<float> best=stackalloc float[16];Span<int> at=stackalloc int[16];max=Math.Min(max,16);int count=0;float c=BackroomsLayout.Cell;
            int cx=(int)Math.Floor((eye.X-origin.X)/c),cz=(int)Math.Floor((eye.Z-origin.Z)/c);
            for(int z=cz-5;z<=cz+5;z++)for(int x=cx-5;x<=cx+5;x++)
            {
                if(!l.Inside(x,z))continue;int i=l.Index(x,z);if(l.Lamps[i]==0||l.Circuits[l.Circuit[i]]==BackroomsLayout.Dead)continue;
                float dx=origin.X+(x+.5f)*c-eye.X,dz=origin.Z+(z+.5f)*c-eye.Z,d2=dx*dx+dz*dz;
                if(count<max){best[count]=d2;at[count]=i;count++;}else{int worst=0;for(int k=1;k<count;k++)if(best[k]>best[worst])worst=k;if(d2<best[worst]){best[worst]=d2;at[worst]=i;}}
            }
            int lit=0;
            for(int k=0;k<max;k++)
            {
                if(k>=count){pos[k*4]=0;pos[k*4+1]=-10000;pos[k*4+2]=0;pos[k*4+3]=.0001f;rgb[k*3]=rgb[k*3+1]=rgb[k*3+2]=0;continue;}
                int i=at[k],x=i%l.Width,z=i/l.Width;float v=Level(l.Circuits[l.Circuit[i]],i,time);
                pos[k*4]=origin.X+(x+.5f)*c;pos[k*4+1]=origin.Y+BackroomsLayout.Height-.15f;pos[k*4+2]=origin.Z+(z+.5f)*c;pos[k*4+3]=range;
                rgb[k*3]=color.X*v;rgb[k*3+1]=color.Y*v;rgb[k*3+2]=color.Z*v;if(v>0)lit++;
            }
            return lit;
        }
    }
}
