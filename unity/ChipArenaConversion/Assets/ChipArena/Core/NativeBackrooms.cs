using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A Backrooms level's layout (docs/BACKROOMS.md "The layout contract"): a grid of <see cref="Cell"/> m cells under a
    /// <see cref="Height"/> m drop ceiling, what lies on each cell (floor, pillar, a light fixture and its circuit) and the walls
    /// on the cell edges (the outer border always walled). The generator fills it (Astra's; <see cref="NativeBackrooms.Preview"/>
    /// stands in until it lands) and the presentation builds whatever it says. Pure.
    /// </summary>
    public sealed class BackroomsLayout
    {
        /// <summary>Level 0's proportions (the owner's reference photo, 2026-11-02): 8 ft cells, a 9 ft drop ceiling, thin
        /// partition walls, 1 m doorways cut to the ceiling.</summary>
        public const float Cell=2.4f,Height=2.7f,Thickness=.14f,Doorway=1.0f,PillarSize=.5f;
        public const byte Carpet=0,Pit=2;
        public const byte Open=0,Wall=1,Short=2;
        public const byte NoPillar=0,SmallPillar=1;
        public const byte On=0,Flicker=1,Dead=2;
        public readonly string Level;public readonly int Seed,Width,Depth;
        /// <summary>Per cell (<see cref="Index"/>): floor, pillar, whether a light fixture hangs over it, and its circuit.</summary>
        public readonly byte[] Floor,Pillar,Circuit;public readonly bool[] Light;
        /// <summary>Walls along z on the line x (0..Width) beside row z: <see cref="WallX"/>[(Width+1)*z+x]; walls along x on the line z
        /// (0..Depth) beside column x: <see cref="WallZ"/>[Width*z+x]. Open, Wall, or Short (a wall with a <see cref="Doorway"/> m
        /// doorway in its middle).</summary>
        public readonly byte[] WallX,WallZ;
        /// <summary>Each circuit's state (On, Flicker, Dead).</summary>
        public readonly byte[] Circuits=new byte[256];
        public int EntryX,EntryZ,ExitX,ExitZ;
        public BackroomsLayout(string level,int seed,int width,int depth)
        {
            Level=level;Seed=seed;Width=Math.Max(2,width);Depth=Math.Max(2,depth);int n=Width*Depth;
            Floor=new byte[n];Pillar=new byte[n];Circuit=new byte[n];Light=new bool[n];
            WallX=new byte[(Width+1)*Depth];WallZ=new byte[Width*(Depth+1)];
            for(int z=0;z<Depth;z++){WallX[(Width+1)*z]=Wall;WallX[(Width+1)*z+Width]=Wall;}
            for(int x=0;x<Width;x++){WallZ[x]=Wall;WallZ[Width*Depth+x]=Wall;}
        }
        public int Index(int x,int z)=>x+Width*z;
        public bool Inside(int x,int z)=>x>=0&&z>=0&&x<Width&&z<Depth;
        /// <summary>The wall between cell (x, z) and its neighbour one step along x (+1 or -1) or z.</summary>
        public byte Between(int x,int z,int dx,int dz)=>dx!=0?WallX[(Width+1)*z+(dx>0?x+1:x)]:WallZ[Width*(dz>0?z+1:z)+x];
        public void Set(int x,int z,int dx,int dz,byte wall){if(dx!=0)WallX[(Width+1)*z+(dx>0?x+1:x)]=wall;else WallZ[Width*(dz>0?z+1:z)+x]=wall;}
        /// <summary>Whether a body can cross from (x, z) one step: no full wall and the neighbour inside.</summary>
        public bool Passable(int x,int z,int dx,int dz)=>Inside(x+dx,z+dz)&&Between(x,z,dx,dz)!=Wall;
    }

    public static class NativeBackrooms
    {
        public const string Level0="level-0";
        private struct Rng{public uint S;public Rng(int seed){S=(uint)seed*2654435761u^0x9E3779B9u;if(S==0)S=1;}public float Next(){S^=S<<13;S^=S>>17;S^=S<<5;return (S&0xffffff)/16777216f;}public int Range(int n)=>(int)(Next()*n)%Math.Max(1,n);}
        private static readonly (int dx,int dz)[] Steps={(1,0),(-1,0),(0,1),(0,-1)};

        /// <summary>
        /// A Level 0 layout for the DEV preview (deterministic from `seed`; the reference photo and the pack's "yellow offices"
        /// archetype: a grid maze of rooms): every inner edge starts walled; rooms of 2 to 5 cells a side and a few long corridors are
        /// opened up; the rooms and corridors are joined by a random spanning tree of doorways (a 1 m doorway, or the whole cell
        /// open), plus a share more for loops; the odd big room gets slim pillars on a 3-cell pitch; a fixture hangs over every
        /// other cell in both directions, a few circuits flickering or dead.
        /// </summary>
        public static BackroomsLayout Preview(int seed,int width,int depth)
        {
            var l=new BackroomsLayout(Level0,seed,width,depth);var r=new Rng(seed);int w=l.Width,d=l.Depth;
            for(int z=0;z<d;z++)for(int x=1;x<w;x++)l.WallX[(w+1)*z+x]=BackroomsLayout.Wall;
            for(int z=1;z<d;z++)for(int x=0;x<w;x++)l.WallZ[w*z+x]=BackroomsLayout.Wall;
            // Regions: rooms and corridors carved over the cells (a cell keeps its first region; the rest are single-cell rooms).
            var region=new int[w*d];for(int i=0;i<region.Length;i++)region[i]=-1;int regions=0;
            int rooms=w*d/10;
            for(int k=0;k<rooms;k++)
            {
                bool corridor=r.Next()<.22f;int rw,rd;
                if(corridor){bool alongX=r.Next()<.5f;rw=alongX?4+r.Range(8):1;rd=alongX?1:4+r.Range(8);}else{rw=2+r.Range(4);rd=2+r.Range(4);}
                int x0=r.Range(Math.Max(1,w-rw+1)),z0=r.Range(Math.Max(1,d-rd+1));int id=regions++;
                for(int z=z0;z<Math.Min(d,z0+rd);z++)for(int x=x0;x<Math.Min(w,x0+rw);x++)if(region[l.Index(x,z)]<0)region[l.Index(x,z)]=id;
            }
            for(int i=0;i<region.Length;i++)if(region[i]<0)region[i]=regions++;
            // (A room carved over part of an earlier one can be left in pieces: each connected piece is its own region.)
            {
                var label=new int[w*d];for(int i=0;i<label.Length;i++)label[i]=-1;var stack=new int[w*d];int pieces=0;
                for(int i=0;i<label.Length;i++)
                {
                    if(label[i]>=0)continue;int top=0;label[i]=pieces;stack[top++]=i;
                    while(top>0){int cc=stack[--top];int x=cc%w,z=cc/w;foreach(var (dx,dz) in Steps){int nx=x+dx,nz=z+dz;if(!l.Inside(nx,nz))continue;int nn=l.Index(nx,nz);if(label[nn]<0&&region[nn]==region[cc]){label[nn]=pieces;stack[top++]=nn;}}}
                    pieces++;
                }
                region=label;regions=pieces;
            }
            // Inside a region its walls come down.
            for(int z=0;z<d;z++)for(int x=0;x<w;x++){int a=region[l.Index(x,z)];if(x+1<w&&region[l.Index(x+1,z)]==a)l.Set(x,z,1,0,BackroomsLayout.Open);if(z+1<d&&region[l.Index(x,z+1)]==a)l.Set(x,z,0,1,BackroomsLayout.Open);}
            // Doorways: a random spanning tree over the regions (union-find over the walls between them, in a shuffled order).
            var edges=new List<(int x,int z,int dx,int dz)>();
            for(int z=0;z<d;z++)for(int x=0;x<w;x++){if(x+1<w&&l.Between(x,z,1,0)==BackroomsLayout.Wall)edges.Add((x,z,1,0));if(z+1<d&&l.Between(x,z,0,1)==BackroomsLayout.Wall)edges.Add((x,z,0,1));}
            for(int i=edges.Count-1;i>0;i--){int j=r.Range(i+1);(edges[i],edges[j])=(edges[j],edges[i]);}
            var parent=new int[regions];for(int i=0;i<regions;i++)parent[i]=i;
            int Find(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
            foreach(var e in edges)
            {
                int a=Find(region[l.Index(e.x,e.z)]),b=Find(region[l.Index(e.x+e.dx,e.z+e.dz)]);
                if(a!=b){parent[a]=b;l.Set(e.x,e.z,e.dx,e.dz,Doorway(l,e,r.Next()<.7f));}
                else if(r.Next()<.12f)l.Set(e.x,e.z,e.dx,e.dz,Doorway(l,e,r.Next()<.65f));
            }
            l.EntryX=1;l.EntryZ=1;l.ExitX=w-2;l.ExitZ=d-2;
            for(int z=0;z<d;z++)for(int x=0;x<w;x++)
            {
                int c=l.Index(x,z);
                // Slim pillars on a 3-cell pitch only deep in the empty halls (no wall on any edge of this cell or its neighbours),
                // and only now and then.
                if(x%3==1&&z%3==1&&!(x==l.EntryX&&z==l.EntryZ)&&Clear(l,x,z)&&r.Next()<.5f)l.Pillar[c]=BackroomsLayout.SmallPillar;
                l.Light[c]=(x&1)==0&&(z&1)==0;
                l.Circuit[c]=(byte)((x/4+(z/4)*((w+3)/4))&255);
            }
            for(int i=0;i<l.Circuits.Length;i++){float s=r.Next();l.Circuits[i]=s<.06f?BackroomsLayout.Dead:s<.16f?BackroomsLayout.Flicker:BackroomsLayout.On;}
            return l;
        }
        // A doorway cut into a wall that runs on past both its ends (a 1 m opening in a partition, as in the photo); where the wall
        // would be left as two stubs standing on their own, the whole cell edge opens instead.
        private static byte Doorway(BackroomsLayout l,(int x,int z,int dx,int dz) e,bool narrow)
        {
            if(!narrow)return BackroomsLayout.Open;
            // The collinear edges on either side: along z for a wall between x-neighbours, along x for one between z-neighbours.
            int sx=e.dx!=0?0:1,sz=e.dx!=0?1:0;
            bool before=l.Inside(e.x-sx,e.z-sz)&&l.Between(e.x-sx,e.z-sz,e.dx,e.dz)==BackroomsLayout.Wall,after=l.Inside(e.x+sx,e.z+sz)&&l.Between(e.x+sx,e.z+sz,e.dx,e.dz)==BackroomsLayout.Wall;
            return before&&after?BackroomsLayout.Short:BackroomsLayout.Open;
        }
        // No wall on any edge of the 3 x 3 cells round (x, z).
        private static bool Clear(BackroomsLayout l,int x,int z)
        {
            for(int zz=z-1;zz<=z+1;zz++)for(int xx=x-1;xx<=x+1;xx++){if(!l.Inside(xx,zz))return false;if(l.Between(xx,zz,1,0)!=0||l.Between(xx,zz,-1,0)!=0||l.Between(xx,zz,0,1)!=0||l.Between(xx,zz,0,-1)!=0)return false;}
            return true;
        }
        /// <summary>Marks the cells reachable from the entry; returns how many it did not reach.</summary>
        public static int Flood(BackroomsLayout l,bool[] seen,int[] stack)
        {
            Array.Clear(seen,0,seen.Length);int top=0,start=l.Index(l.EntryX,l.EntryZ);seen[start]=true;stack[top++]=start;
            while(top>0){int c=stack[--top];int x=c%l.Width,z=c/l.Width;foreach(var (dx,dz) in Steps)if(l.Passable(x,z,dx,dz)){int n=l.Index(x+dx,z+dz);if(!seen[n]){seen[n]=true;stack[top++]=n;}}}
            int left=0;for(int i=0;i<seen.Length;i++)if(!seen[i])left++;return left;
        }

        // ---- the lights ----
        /// <summary>A fixture's light level at `time` (0..1): steady on; dead off; a flickering circuit stutters (seeded by its cell),
        /// mostly on with dips and dead moments, never a strobe faster than 10 Hz.</summary>
        public static float Level(byte state,int cell,double time)
        {
            if(state==BackroomsLayout.On)return 1;if(state==BackroomsLayout.Dead)return 0;
            double t=time*7+cell*1.37;int k=(int)Math.Floor(t);float h=Hash(cell,k);
            return h<.12f?0:h<.3f?.35f+.4f*(float)(t-k):1;
        }
        private static float Hash(int a,int b){unchecked{uint h=(uint)a*2246822519u^(uint)b*3266489917u;h^=h>>15;h*=2654435761u;h^=h>>13;return (h&0xffffff)/16777216f;}}
        /// <summary>The `max` lit fixtures nearest the eye (positions and ranges into `pos`, x y z range; linear colours times their
        /// level into `rgb`); the rest of the slots dark. Returns how many are lit.</summary>
        public static int NearLamps(BackroomsLayout l,Vector3 origin,Vector3 eye,double time,int max,float[] pos,float[] rgb,Vector3 color,float range)
        {
            Span<float> best=stackalloc float[16];Span<int> at=stackalloc int[16];max=Math.Min(max,16);int count=0;float c=BackroomsLayout.Cell;
            int cx=(int)Math.Floor((eye.X-origin.X)/c),cz=(int)Math.Floor((eye.Z-origin.Z)/c);
            for(int z=cz-6;z<=cz+6;z++)for(int x=cx-6;x<=cx+6;x++)
            {
                if(!l.Inside(x,z))continue;int i=l.Index(x,z);if(!l.Light[i]||l.Circuits[l.Circuit[i]]==BackroomsLayout.Dead)continue;
                float dx=origin.X+(x+.5f)*c-eye.X,dz=origin.Z+(z+.5f)*c-eye.Z,d2=dx*dx+dz*dz;
                if(count<max){best[count]=d2;at[count]=i;count++;}else{int worst=0;for(int k=1;k<count;k++)if(best[k]>best[worst])worst=k;if(d2<best[worst]){best[worst]=d2;at[worst]=i;}}
            }
            int lit=0;
            for(int k=0;k<max;k++)
            {
                if(k>=count){pos[k*4]=0;pos[k*4+1]=-10000;pos[k*4+2]=0;pos[k*4+3]=.0001f;rgb[k*3]=rgb[k*3+1]=rgb[k*3+2]=0;continue;}
                int i=at[k],x=i%l.Width,z=i/l.Width;float v=Level(l.Circuits[l.Circuit[i]],i,time);
                pos[k*4]=origin.X+(x+.5f)*c;pos[k*4+1]=origin.Y+BackroomsLayout.Height-.2f;pos[k*4+2]=origin.Z+(z+.5f)*c;pos[k*4+3]=range;
                rgb[k*3]=color.X*v;rgb[k*3+1]=color.Y*v;rgb[k*3+2]=color.Z*v;if(v>0)lit++;
            }
            return lit;
        }
    }

    /// <summary>
    /// The geometry of a block of a Backrooms layout (Level 0's look, the owner's reference photo): carpet on the floor, the drop
    /// ceiling's tiles, wallpaper on every face of the walls (to the ceiling, doorways cut through, the jambs papered too) and of the
    /// slim pillars, a baseboard round their foot, and the fluorescent fixtures set in the ceiling. Each surface is its own list
    /// of triangles (positions, normals, uv0 in world metres scaled to its texture's repeat), so a renderer draws one mesh per
    /// surface per block. Pure: the same layout and block give the same geometry.
    /// </summary>
    public sealed class BackroomsMesh
    {
        public enum Surface{Carpet,Ceiling,Wallpaper,Trim,Light,DeadLight,Count}
        /// <summary>The metres one texture repeat covers: the carpet's, the ceiling grid's (its 4 x 2 tiles on one cell), the
        /// wallpaper's, the baseboard's.</summary>
        public const float CarpetRepeat=2f,WallpaperRepeat=.9f,TrimRepeat=1f;
        /// <summary>The baseboard's height and how far it stands off the wall; a fixture's size (along x, along z) and its drop.</summary>
        public const float TrimHeight=.09f,TrimOut=.012f,FixtureLength=1.2f,FixtureWidth=.3f,FixtureDrop=.012f;
        public sealed class Part{public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();public readonly List<Vector2> Uv=new List<Vector2>();public readonly List<int> Indices=new List<int>();public void Clear(){Positions.Clear();Normals.Clear();Uv.Clear();Indices.Clear();}}
        public readonly Part[] Parts=new Part[(int)Surface.Count];
        /// <summary>The flickering fixtures of the block (centre, circuit, cell): drawn one by one at their level.</summary>
        public readonly List<(Vector3 at,int cell)> Flickers=new List<(Vector3,int)>();
        public BackroomsMesh(){for(int i=0;i<Parts.Length;i++)Parts[i]=new Part();}

        /// <summary>Builds the cells x0..x1-1, z0..z1-1 of the layout with its corner cell (0, 0) at `origin`.</summary>
        public void Build(BackroomsLayout l,Vector3 origin,int x0,int z0,int x1,int z1)
        {
            Origin=origin;foreach(var p in Parts)p.Clear();Flickers.Clear();
            float c=BackroomsLayout.Cell,h=BackroomsLayout.Height,t=BackroomsLayout.Thickness*.5f;x1=Math.Min(x1,l.Width);z1=Math.Min(z1,l.Depth);
            for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)
            {
                int i=l.Index(x,z);float ax=origin.X+x*c,az=origin.Z+z*c,y=origin.Y;
                if(l.Floor[i]!=BackroomsLayout.Pit)Quad(Surface.Carpet,new Vector3(ax,y,az),new Vector3(c,0,0),new Vector3(0,0,c),Vector3.UnitY,CarpetRepeat,CarpetRepeat);
                // The ceiling: one tile sheet per cell, facing down, its grid lined up with the cell.
                Quad(Surface.Ceiling,new Vector3(ax,y+h,az),new Vector3(0,0,c),new Vector3(c,0,0),-Vector3.UnitY,c,c,true);
                if(l.Light[i])
                {
                    var at=new Vector3(ax+c*.5f,y+h-FixtureDrop,az+c*.5f);byte state=l.Circuits[l.Circuit[i]];
                    if(state==BackroomsLayout.Flicker)Flickers.Add((at,i));
                    else Fixture(state==BackroomsLayout.Dead?Surface.DeadLight:Surface.Light,at);
                }
                if(l.Pillar[i]==BackroomsLayout.SmallPillar){float s=BackroomsLayout.PillarSize*.5f;Box(new Vector3(ax+c*.5f-s,y,az+c*.5f-s),new Vector3(ax+c*.5f+s,y+h,az+c*.5f+s));}
            }
            // Walls along x on the lines z (their own block's lines and its far edge), then along z on the lines x. Each runs past
            // its ends by half the thickness, so walls meet at corners and junctions without a gap.
            for(int z=z0;z<=z1;z++)for(int x=x0;x<x1;x++)
            {
                if(z==z1&&z1<l.Depth)continue;byte wl=l.WallZ[l.Width*z+x];if(wl==BackroomsLayout.Open)continue;float px=origin.X+x*c,pz=origin.Z+z*c;
                if(wl==BackroomsLayout.Short){float g=(c-BackroomsLayout.Doorway)*.5f;Wall(px-t,px+g,pz,true);Wall(px+c-g,px+c+t,pz,true);}else Wall(px-t,px+c+t,pz,true);
            }
            for(int z=z0;z<z1;z++)for(int x=x0;x<=x1;x++)
            {
                if(x==x1&&x1<l.Width)continue;byte wl=l.WallX[(l.Width+1)*z+x];if(wl==BackroomsLayout.Open)continue;float px=origin.X+x*c,pz=origin.Z+z*c;
                if(wl==BackroomsLayout.Short){float g=(c-BackroomsLayout.Doorway)*.5f;Wall(pz-t,pz+g,px,false);Wall(pz+c-g,pz+c+t,px,false);}else Wall(pz-t,pz+c+t,px,false);
            }
        }
        // A wall from a to b along x (alongX, on the line z = at) or along z (on the line x = at): a papered box from the floor to the
        // ceiling with a baseboard round its foot.
        private void Wall(float a,float b,float at,bool alongX)
        {
            float t=BackroomsLayout.Thickness*.5f,y=Origin.Y;
            if(alongX)Box(new Vector3(a,y,at-t),new Vector3(b,y+BackroomsLayout.Height,at+t));else Box(new Vector3(at-t,y,a),new Vector3(at+t,y+BackroomsLayout.Height,b));
        }
        private Vector3 Origin;
        /// <summary>Builds the whole layout.</summary>
        public void Build(BackroomsLayout l,Vector3 origin)=>Build(l,origin,0,0,l.Width,l.Depth);
        // A box's four sides papered, with a baseboard strip standing off each side at its foot (top and bottom hidden by the
        // ceiling and the floor).
        private void Box(Vector3 lo,Vector3 hi)
        {
            float w=hi.X-lo.X,d=hi.Z-lo.Z,h=hi.Y-lo.Y,o=TrimOut,th=TrimHeight;
            Quad(Surface.Wallpaper,new Vector3(lo.X,lo.Y,lo.Z),new Vector3(w,0,0),new Vector3(0,h,0),-Vector3.UnitZ,WallpaperRepeat,WallpaperRepeat,false,true);
            Quad(Surface.Wallpaper,new Vector3(hi.X,lo.Y,hi.Z),new Vector3(-w,0,0),new Vector3(0,h,0),Vector3.UnitZ,WallpaperRepeat,WallpaperRepeat,false,true);
            Quad(Surface.Wallpaper,new Vector3(hi.X,lo.Y,lo.Z),new Vector3(0,0,d),new Vector3(0,h,0),Vector3.UnitX,WallpaperRepeat,WallpaperRepeat,false,true);
            Quad(Surface.Wallpaper,new Vector3(lo.X,lo.Y,hi.Z),new Vector3(0,0,-d),new Vector3(0,h,0),-Vector3.UnitX,WallpaperRepeat,WallpaperRepeat,false,true);
            // The baseboard: the same four faces stood off by `o`, `th` tall, and its top.
            var a=new Vector3(lo.X-o,lo.Y,lo.Z-o);var b=new Vector3(hi.X+o,lo.Y+th,hi.Z+o);float bw=b.X-a.X,bd=b.Z-a.Z;
            Quad(Surface.Trim,new Vector3(a.X,a.Y,a.Z),new Vector3(bw,0,0),new Vector3(0,th,0),-Vector3.UnitZ,TrimRepeat,TrimRepeat,false,true);
            Quad(Surface.Trim,new Vector3(b.X,a.Y,b.Z),new Vector3(-bw,0,0),new Vector3(0,th,0),Vector3.UnitZ,TrimRepeat,TrimRepeat,false,true);
            Quad(Surface.Trim,new Vector3(b.X,a.Y,a.Z),new Vector3(0,0,bd),new Vector3(0,th,0),Vector3.UnitX,TrimRepeat,TrimRepeat,false,true);
            Quad(Surface.Trim,new Vector3(a.X,a.Y,b.Z),new Vector3(0,0,-bd),new Vector3(0,th,0),-Vector3.UnitX,TrimRepeat,TrimRepeat,false,true);
            Quad(Surface.Trim,new Vector3(a.X,b.Y,a.Z),new Vector3(0,0,bd),new Vector3(bw,0,0),Vector3.UnitY,TrimRepeat,TrimRepeat);
        }
        /// <summary>A fixture set in the ceiling at `at`: a flat lit panel facing down, long along x.</summary>
        public void Fixture(Surface s,Vector3 at)
        {
            float lx=FixtureLength*.5f,lz=FixtureWidth*.5f;
            Quad(s,new Vector3(at.X-lx,at.Y,at.Z-lz),new Vector3(0,0,FixtureWidth),new Vector3(FixtureLength,0,0),-Vector3.UnitY,1,1,true);
        }
        // A quad from `o` along u and v (the front face is the one `n` points out of: Unity's clockwise winding seen from there).
        // uv0 from the world position: walls by their run (along x or z) and height, floors and ceilings by x and z, each over
        // its surface's repeat; `unit` maps the quad's own 0..1 instead (a fixture's panel).
        private void Quad(Surface s,Vector3 o,Vector3 u,Vector3 v,Vector3 n,float ru,float rv,bool unit=false,bool wall=false)
        {
            var p=Parts[(int)s];int k=p.Positions.Count;var corners=new[]{o,o+u,o+u+v,o+v};
            foreach(var q in corners)
            {
                p.Positions.Add(q);p.Normals.Add(n);
                Vector2 uv;
                if(s==Surface.Light||s==Surface.DeadLight)uv=new Vector2(Vector3.Dot(q-o,Vector3.Normalize(u))/u.Length(),Vector3.Dot(q-o,Vector3.Normalize(v))/v.Length());
                else if(wall)uv=new Vector2((Math.Abs(n.X)>.5f?q.Z:q.X)/ru,q.Y/rv);
                else uv=new Vector2(q.X/ru,q.Z/rv);
                p.Uv.Add(uv);
            }
            // Front face toward n: (b-a)x(c-a) along n is Unity's front (clockwise seen from n's side).
            var f=Vector3.Cross(corners[1]-corners[0],corners[2]-corners[0]);
            if(Vector3.Dot(f,n)>=0){p.Indices.Add(k);p.Indices.Add(k+1);p.Indices.Add(k+2);p.Indices.Add(k);p.Indices.Add(k+2);p.Indices.Add(k+3);}
            else{p.Indices.Add(k);p.Indices.Add(k+2);p.Indices.Add(k+1);p.Indices.Add(k);p.Indices.Add(k+3);p.Indices.Add(k+2);}
        }
    }
}
