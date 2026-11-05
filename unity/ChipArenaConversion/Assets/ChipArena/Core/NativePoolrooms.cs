using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Poolrooms' layout (docs/BACKROOMS.md "The Poolrooms"): a grid of <see cref="Cell"/> m cells, each a tiled deck, a wading
    /// pool, a deep pool or a deep pool's steps, under a ceiling its room sets; room walls on the cell edges (the outer border always
    /// walled), the openings between rooms arched by the presentation; round pillars and round lights over cells. The generator
    /// fills it (Astra's; <see cref="NativePoolrooms.Preview"/> stands in until it lands) and the presentation builds whatever it
    /// says. Pure.
    /// </summary>
    public sealed class PoolroomsLayout
    {
        /// <summary>The proportions of the owner's Backrooms add-on's "poolrooms" archetype: 2 m cells, a 3.6 m ceiling at its
        /// lowest rising 1.2 m a step by room, 0.3 m walls, round 0.7 m pillars, arches springing at 1.5 m; pools 1.3 m deep, wading
        /// pools 0.4 m, the water 0.15 m below the deck.</summary>
        public const float Cell=2f,Height=3.6f,Rise=1.2f,Thickness=.3f,PillarSize=.7f,ArchSpring=1.5f,Freeboard=.15f,DeepDepth=1.3f,ShallowDepth=.4f;
        public const byte Deck=0,Shallow=1,Deep=2,Steps=3,Abyss=4;
        /// <summary>An abyss cell's floor (THE DEEP END's pit; Core NativeDeepEndArena): the visible floor stops, the tile going down into the dark. Collision stands at the deep pool's floor (the rules').</summary>
        public const float AbyssFloor=-38f;
        public const byte Open=0,Wall=1;
        /// <summary>Which way a steps cell descends (<see cref="StepDir"/>): toward +x, -x, +z or -z.</summary>
        public const byte DownPosX=0,DownNegX=1,DownPosZ=2,DownNegZ=3;
        /// <summary>A deep pool's steps: this many treads from the deck down to its floor, the last at the floor.</summary>
        public const int Treads=4;
        /// <summary>The water's surface (the deck at 0).</summary>
        public const float WaterY=-Freeboard;
        public readonly int Seed,Width,Depth;
        /// <summary>Per cell (<see cref="Index"/>): floor, which way its steps descend, its ceiling's height, a round pillar at its
        /// centre, a round light over it, and its room.</summary>
        public readonly byte[] Floor,StepDir;public readonly float[] Ceiling;public readonly bool[] Pillar,Light;public readonly int[] Room;
        /// <summary>Walls along z on the line x (0..Width) beside row z: <see cref="WallX"/>[(Width+1)*z+x]; walls along x on the line z
        /// (0..Depth) beside column x: <see cref="WallZ"/>[Width*z+x]. Open or Wall.</summary>
        public readonly byte[] WallX,WallZ;
        public int EntryX,EntryZ;
        public PoolroomsLayout(int seed,int width,int depth)
        {
            Seed=seed;Width=Math.Max(4,width);Depth=Math.Max(4,depth);int n=Width*Depth;
            Floor=new byte[n];StepDir=new byte[n];Ceiling=new float[n];Pillar=new bool[n];Light=new bool[n];Room=new int[n];
            for(int i=0;i<n;i++)Ceiling[i]=Height;
            WallX=new byte[(Width+1)*Depth];WallZ=new byte[Width*(Depth+1)];
            for(int z=0;z<Depth;z++){WallX[(Width+1)*z]=Wall;WallX[(Width+1)*z+Width]=Wall;}
            for(int x=0;x<Width;x++){WallZ[x]=Wall;WallZ[Width*Depth+x]=Wall;}
        }
        public int Index(int x,int z)=>x+Width*z;
        public bool Inside(int x,int z)=>x>=0&&z>=0&&x<Width&&z<Depth;
        /// <summary>The wall between cell (x, z) and its neighbour one step along x (+1 or -1) or z.</summary>
        public byte Between(int x,int z,int dx,int dz)=>dx!=0?WallX[(Width+1)*z+(dx>0?x+1:x)]:WallZ[Width*(dz>0?z+1:z)+x];
        public void Set(int x,int z,int dx,int dz,byte wall){if(dx!=0)WallX[(Width+1)*z+(dx>0?x+1:x)]=wall;else WallZ[Width*(dz>0?z+1:z)+x]=wall;}
        /// <summary>Whether a body can cross from (x, z) one step: no wall and the neighbour inside (pools included: wading,
        /// swimming and climbing out are the rules').</summary>
        public bool Passable(int x,int z,int dx,int dz)=>Inside(x+dx,z+dz)&&Between(x,z,dx,dz)!=Wall;
        public static bool Pool(byte floor)=>floor!=Deck;
        /// <summary>A flat floor's height: the deck 0, a wading pool's bottom, a deep pool's.</summary>
        public static float FloorY(byte floor)=>floor==Deck?0:floor==Shallow?-ShallowDepth:floor==Abyss?AbyssFloor:-DeepDepth;
        /// <summary>The floor's height at (fx, fz) across cell `i` (0..1 each): a steps cell's tread there (tread k, k = 0 at the
        /// deck's side, at -(k+1) of <see cref="Treads"/> of the deep pool's depth), any other cell its flat floor.</summary>
        public float FloorAt(int i,float fx,float fz)
        {
            if(Floor[i]!=Steps)return FloorY(Floor[i]);
            byte d=StepDir[i];float along=d==DownPosX?fx:d==DownNegX?1-fx:d==DownPosZ?fz:1-fz;
            int k=Math.Min(Treads-1,Math.Max(0,(int)(along*Treads)));return -(k+1)*DeepDepth/Treads;
        }
    }

    public static class NativePoolrooms
    {
        public const string Level="poolrooms";
        private struct Rng{public uint S;public Rng(int seed){S=(uint)seed*2654435761u^0x7F4A7C15u;if(S==0)S=1;}public float Next(){S^=S<<13;S^=S>>17;S^=S<<5;return (S&0xffffff)/16777216f;}public int Range(int n)=>(int)(Next()*n)%Math.Max(1,n);}
        private static readonly (int dx,int dz)[] Dirs={(1,0),(-1,0),(0,1),(0,-1)};
        /// <summary>What fills a room: a tiled hall (pillars on a 2-cell pitch), a deep pool or a wading pool in a deck one cell wide.</summary>
        public const int Hall=0,PoolRoom=1,Wading=2;
        /// <summary>A room's sides (cells) and the opening widths between rooms (cells), as the add-on lays them.</summary>
        public const int MinRoom=4,MaxRoom=10;
        private static readonly int[] OpeningWidths={2,2,3};

        /// <summary>
        /// A Poolrooms layout for the DEV preview (deterministic from `seed`; the add-on's "poolrooms" archetype): the grid split
        /// again and again along its longer side into rooms of 4 to 10 cells a side; each a deep pool (about 38%), a wading pool
        /// (18%) or a hall, the pools inside a deck one cell wide and a deep pool's steps on one of its sides; the rooms walled from
        /// each other and joined by a random spanning tree of openings 2 or 3 cells wide, plus a share more for loops; ceilings
        /// rising by room (a deep pool's at least one step); round lights over every third cell in both directions.
        /// </summary>
        public static PoolroomsLayout Preview(int seed,int width,int depth)
        {
            var l=new PoolroomsLayout(seed,width,depth);var r=new Rng(seed);int w=l.Width,d=l.Depth;
            var rooms=new List<(int x0,int z0,int x1,int z1)>();Split(0,0,w,d,ref r,rooms);
            int entry=0;for(int k=0;k<rooms.Count;k++){var q=rooms[k];if(q.x0<=1&&q.z0<=1&&q.x1>1&&q.z1>1)entry=k;}
            var kind=new int[rooms.Count];
            for(int k=0;k<rooms.Count;k++)
            {
                var (x0,z0,x1,z1)=rooms[k];float s=r.Next();
                kind[k]=k==entry?Hall:s<.38f?PoolRoom:s<.56f?Wading:Hall;
                float h=PoolroomsLayout.Height+PoolroomsLayout.Rise*(kind[k]==PoolRoom?1+r.Range(2):r.Range(3));
                for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++){int i=l.Index(x,z);l.Room[i]=k;l.Ceiling[i]=h;}
            }
            // Walls between rooms; inside a room none.
            for(int z=0;z<d;z++)for(int x=0;x<w;x++)
            {
                int a=l.Room[l.Index(x,z)];
                if(x+1<w&&l.Room[l.Index(x+1,z)]!=a)l.Set(x,z,1,0,PoolroomsLayout.Wall);
                if(z+1<d&&l.Room[l.Index(x,z+1)]!=a)l.Set(x,z,0,1,PoolroomsLayout.Wall);
            }
            // Openings: the walls each pair of neighbouring rooms shares (one straight run), joined by a random spanning tree, plus
            // a share more for loops; each opening 2 or 3 cells wide (the whole run when shorter), round the run's middle.
            var shared=new Dictionary<long,List<(int x,int z,int dx,int dz)>>();
            for(int z=0;z<d;z++)for(int x=0;x<w;x++)
            {
                int a=l.Room[l.Index(x,z)];
                if(x+1<w){int b=l.Room[l.Index(x+1,z)];if(b!=a)Add(shared,a,b,(x,z,1,0));}
                if(z+1<d){int b=l.Room[l.Index(x,z+1)];if(b!=a)Add(shared,a,b,(x,z,0,1));}
            }
            var pairs=new List<long>(shared.Keys);pairs.Sort();
            for(int i=pairs.Count-1;i>0;i--){int j=r.Range(i+1);(pairs[i],pairs[j])=(pairs[j],pairs[i]);}
            var parent=new int[rooms.Count];for(int i=0;i<parent.Length;i++)parent[i]=i;
            int Find(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
            foreach(long p in pairs)
            {
                int a=Find((int)(p>>20)),b=Find((int)(p&0xfffff));bool tree=a!=b;
                if(!tree&&r.Next()>=.35f)continue;if(tree)parent[a]=b;
                var run=shared[p];int n=Math.Min(run.Count,OpeningWidths[r.Range(OpeningWidths.Length)]);
                int start=(run.Count-n)/2+(run.Count>n+1?r.Range(3)-1:0);start=Math.Max(0,Math.Min(run.Count-n,start));
                for(int k=start;k<start+n;k++){var e=run[k];l.Set(e.x,e.z,e.dx,e.dz,PoolroomsLayout.Open);}
            }
            // The pools: inside a deck one cell wide; a deep pool's steps on a side, off its corners, descending inward.
            for(int k=0;k<rooms.Count;k++)
            {
                if(kind[k]==Hall)continue;var (x0,z0,x1,z1)=rooms[k];int bx0=x0+1,bz0=z0+1,bx1=x1-1,bz1=z1-1;
                byte floor=kind[k]==PoolRoom?PoolroomsLayout.Deep:PoolroomsLayout.Shallow;
                for(int z=bz0;z<bz1;z++)for(int x=bx0;x<bx1;x++)l.Floor[l.Index(x,z)]=floor;
                if(kind[k]!=PoolRoom)continue;
                int side=r.Range(4);bool alongZ=side<2;int lo=alongZ?bz0:bx0,hi=alongZ?bz1:bx1;
                int at=hi-lo>=3?lo+1+r.Range(hi-lo-2):lo+r.Range(hi-lo);
                int sx=side==0?bx0:side==1?bx1-1:at,sz=side==2?bz0:side==3?bz1-1:at;
                int si=l.Index(sx,sz);l.Floor[si]=PoolroomsLayout.Steps;
                l.StepDir[si]=side==0?PoolroomsLayout.DownPosX:side==1?PoolroomsLayout.DownNegX:side==2?PoolroomsLayout.DownPosZ:PoolroomsLayout.DownNegZ;
            }
            // Halls: round pillars on a 2-cell pitch off the walls.
            for(int k=0;k<rooms.Count;k++)
            {
                if(kind[k]!=Hall)continue;var (x0,z0,x1,z1)=rooms[k];
                for(int z=z0+1;z<z1-1;z+=2)for(int x=x0+1;x<x1-1;x+=2)if(!(x==1&&z==1))l.Pillar[l.Index(x,z)]=true;
            }
            for(int z=0;z<d;z++)for(int x=0;x<w;x++){int i=l.Index(x,z);l.Light[i]=x%3==1&&z%3==1&&!l.Pillar[i];}
            l.EntryX=1;l.EntryZ=1;
            return l;
        }
        private static void Add(Dictionary<long,List<(int,int,int,int)>> shared,int a,int b,(int,int,int,int) e)
        {
            long key=((long)Math.Min(a,b)<<20)|(long)Math.Max(a,b);
            if(!shared.TryGetValue(key,out var run)){run=new List<(int,int,int,int)>();shared[key]=run;}run.Add(e);
        }
        // Splits the rectangle along its longer side while it is over MaxRoom, or now and then past 7, each part at least MinRoom.
        private static void Split(int x0,int z0,int x1,int z1,ref Rng r,List<(int,int,int,int)> rooms)
        {
            int w=x1-x0,d=z1-z0;bool alongX=w>=d;int len=alongX?w:d;
            if(len>MaxRoom||len>7&&len>=2*MinRoom&&r.Next()<.55f)
            {
                int cut=MinRoom+r.Range(len-2*MinRoom+1);
                if(alongX){Split(x0,z0,x0+cut,z1,ref r,rooms);Split(x0+cut,z0,x1,z1,ref r,rooms);}
                else{Split(x0,z0,x1,z0+cut,ref r,rooms);Split(x0,z0+cut,x1,z1,ref r,rooms);}
                return;
            }
            rooms.Add((x0,z0,x1,z1));
        }
        /// <summary>Marks the cells reachable from the entry; returns how many it did not reach.</summary>
        public static int Flood(PoolroomsLayout l,bool[] seen,int[] stack)
        {
            Array.Clear(seen,0,seen.Length);int top=0,start=l.Index(l.EntryX,l.EntryZ);seen[start]=true;stack[top++]=start;
            while(top>0){int c=stack[--top];int x=c%l.Width,z=c/l.Width;foreach(var (dx,dz) in Dirs)if(l.Passable(x,z,dx,dz)){int n=l.Index(x+dx,z+dz);if(!seen[n]){seen[n]=true;stack[top++]=n;}}}
            int left=0;for(int i=0;i<seen.Length;i++)if(!seen[i])left++;return left;
        }
        /// <summary>The `max` lights nearest the eye (positions and ranges into `pos`, x y z range; linear colours into `rgb`), just
        /// under their ceilings; the rest of the slots dark. Returns how many are lit.</summary>
        public static int NearLamps(PoolroomsLayout l,Vector3 origin,Vector3 eye,int max,float[] pos,float[] rgb,Vector3 color,float range)
        {
            Span<float> best=stackalloc float[16];Span<int> at=stackalloc int[16];max=Math.Min(max,16);int count=0;float c=PoolroomsLayout.Cell;
            int cx=(int)Math.Floor((eye.X-origin.X)/c),cz=(int)Math.Floor((eye.Z-origin.Z)/c);
            for(int z=cz-7;z<=cz+7;z++)for(int x=cx-7;x<=cx+7;x++)
            {
                if(!l.Inside(x,z))continue;int i=l.Index(x,z);if(!l.Light[i])continue;
                float dx=origin.X+(x+.5f)*c-eye.X,dz=origin.Z+(z+.5f)*c-eye.Z,d2=dx*dx+dz*dz;
                if(count<max){best[count]=d2;at[count]=i;count++;}else{int worst=0;for(int k=1;k<count;k++)if(best[k]>best[worst])worst=k;if(d2<best[worst]){best[worst]=d2;at[worst]=i;}}
            }
            for(int k=0;k<max;k++)
            {
                if(k>=count){pos[k*4]=0;pos[k*4+1]=-10000;pos[k*4+2]=0;pos[k*4+3]=.0001f;rgb[k*3]=rgb[k*3+1]=rgb[k*3+2]=0;continue;}
                int i=at[k],x=i%l.Width,z=i/l.Width;
                pos[k*4]=origin.X+(x+.5f)*c;pos[k*4+1]=origin.Y+l.Ceiling[i]-.3f;pos[k*4+2]=origin.Z+(z+.5f)*c;pos[k*4+3]=range;
                rgb[k*3]=color.X;rgb[k*3+1]=color.Y;rgb[k*3+2]=color.Z;
            }
            return count;
        }
    }

    /// <summary>
    /// The geometry of a block of a Poolrooms layout (the owner's Backrooms add-on's look): tiled decks; pools lined in turquoise
    /// tile (their walls from the deck down to the floor, a deep pool's steps tread by tread); the water's sheet over every pool cell;
    /// tiled walls to the ceiling, an elliptical arch over each opening between rooms (springing at <see cref="PoolroomsLayout.ArchSpring"/>,
    /// under the lower ceiling) with tiled spandrels up to the higher; plaster ceilings; round tiled pillars; round lights set in
    /// the ceiling. One list of triangles per surface (positions, normals, uv0 in world metres over its texture's repeat). Pure.
    /// </summary>
    public sealed class PoolroomsMesh
    {
        public enum Surface{Deck,Wall,Basin,Ceiling,Water,Light,Count}
        /// <summary>The metres one texture repeat covers, as the add-on lays its baked textures (docs/BACKROOMS.md "The add-on's
        /// textures").</summary>
        public const float DeckRepeat=1.12f,WallRepeat=.48f,BasinRepeat=.8f,CeilingRepeat=2f,WaterRepeat=2f;
        /// <summary>A light's radius and drop below its ceiling; segments round a pillar or a light, and along an arch.</summary>
        public const float LightRadius=.45f,LightDrop=.012f;public const int Round=20,ArchSegments=14;
        public sealed class Part{public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();public readonly List<Vector2> Uv=new List<Vector2>();public readonly List<int> Indices=new List<int>();public void Clear(){Positions.Clear();Normals.Clear();Uv.Clear();Indices.Clear();}}
        public readonly Part[] Parts=new Part[(int)Surface.Count];
        public PoolroomsMesh(){for(int i=0;i<Parts.Length;i++)Parts[i]=new Part();}

        public void Build(PoolroomsLayout l,Vector3 origin)=>Build(l,origin,0,0,l.Width,l.Depth);
        /// <summary>Builds the cells x0..x1-1, z0..z1-1 of the layout with its corner cell (0, 0) at `origin`: their floors, water,
        /// ceilings, pillars and lights, and the edges they own (each cell's -x and -z edges, the far border's too).</summary>
        public void Build(PoolroomsLayout l,Vector3 origin,int x0,int z0,int x1,int z1)
        {
            foreach(var p in Parts)p.Clear();float c=PoolroomsLayout.Cell;x1=Math.Min(x1,l.Width);z1=Math.Min(z1,l.Depth);
            for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)
            {
                int i=l.Index(x,z);float ax=origin.X+x*c,az=origin.Z+z*c,y=origin.Y;byte f=l.Floor[i];
                if(f==PoolroomsLayout.Deck)Quad(Surface.Deck,new Vector3(ax,y,az),new Vector3(c,0,0),new Vector3(0,0,c),Vector3.UnitY,DeckRepeat);
                else if(f!=PoolroomsLayout.Steps)Quad(Surface.Basin,new Vector3(ax,y+PoolroomsLayout.FloorY(f),az),new Vector3(c,0,0),new Vector3(0,0,c),Vector3.UnitY,BasinRepeat);
                else Treads(l,i,new Vector3(ax,y,az));
                if(PoolroomsLayout.Pool(f))Quad(Surface.Water,new Vector3(ax,y+PoolroomsLayout.WaterY,az),new Vector3(c,0,0),new Vector3(0,0,c),Vector3.UnitY,WaterRepeat);
                float h=l.Ceiling[i];
                Quad(Surface.Ceiling,new Vector3(ax,y+h,az),new Vector3(0,0,c),new Vector3(c,0,0),-Vector3.UnitY,CeilingRepeat);
                var mid=new Vector3(ax+c*.5f,y,az+c*.5f);
                if(l.Pillar[i])Pillar(mid,h);
                if(l.Light[i])Disc(Surface.Light,mid+new Vector3(0,h-LightDrop,0),LightRadius);
                // Its -x and -z edges (and the far border's).
                Edge(l,origin,x,z,true);Edge(l,origin,x,z,false);
                if(x==l.Width-1)Edge(l,origin,x+1,z,true);
                if(z==l.Depth-1)Edge(l,origin,x,z+1,false);
            }
            Walls(l,origin,x0,z0,x1,z1);
        }

        // ---- floors ----
        // A steps cell: its treads, each a strip across the cell at its height, and the risers between them (facing the descent).
        private void Treads(PoolroomsLayout l,int i,Vector3 corner)
        {
            float c=PoolroomsLayout.Cell;int t=PoolroomsLayout.Treads;byte d=l.StepDir[i];
            bool alongX=d==PoolroomsLayout.DownPosX||d==PoolroomsLayout.DownNegX;bool neg=d==PoolroomsLayout.DownNegX||d==PoolroomsLayout.DownNegZ;
            var down=alongX?new Vector3(neg?-1:1,0,0):new Vector3(0,0,neg?-1:1);
            for(int k=0;k<t;k++)
            {
                float a0=(float)k/t,a1=(float)(k+1)/t,hy=-(k+1)*PoolroomsLayout.DeepDepth/t,prev=-k*PoolroomsLayout.DeepDepth/t;
                // The strip's corner and sides in world terms (along the descent from the deck's side).
                float s0=neg?1-a1:a0;var o=alongX?corner+new Vector3(s0*c,hy,0):corner+new Vector3(0,hy,s0*c);
                var u=alongX?new Vector3(c/t,0,0):new Vector3(0,0,c/t);var v=alongX?new Vector3(0,0,c):new Vector3(c,0,0);
                Quad(Surface.Basin,o,u,v,Vector3.UnitY,BasinRepeat);
                if(k==0)continue;
                // The riser at the strip's start, from this tread up to the one before.
                float sa=neg?1-a0:a0;var ro=alongX?corner+new Vector3(sa*c,hy,0):corner+new Vector3(0,hy,sa*c);
                Quad(Surface.Basin,ro,alongX?new Vector3(0,0,c):new Vector3(c,0,0),new Vector3(0,prev-hy,0),down,BasinRepeat,true);
            }
        }
        // The edge on line x (along z, beside row z) or on line z (along x, beside column x): the pool's wall wherever the floors
        // on its two sides differ (in Treads pieces, so a steps cell's side meets each tread), facing the lower side.
        private void Edge(PoolroomsLayout l,Vector3 origin,int x,int z,bool lineX)
        {
            float c=PoolroomsLayout.Cell;int t=PoolroomsLayout.Treads;
            int ax=lineX?x-1:x,az=lineX?z:z-1,bx=x,bz=z;bool aIn=l.Inside(ax,az),bIn=l.Inside(bx,bz);if(!aIn||!bIn)return;
            int a=l.Index(ax,az),b=l.Index(bx,bz);if(l.Floor[a]==PoolroomsLayout.Deck&&l.Floor[b]==PoolroomsLayout.Deck)return;
            for(int k=0;k<t;k++)
            {
                float m=(k+.5f)/t;float ha=lineX?l.FloorAt(a,.999f,m):l.FloorAt(a,m,.999f),hb=lineX?l.FloorAt(b,0,m):l.FloorAt(b,m,0);
                if(Math.Abs(ha-hb)<1e-4f)continue;float lo=Math.Min(ha,hb),hi=Math.Max(ha,hb);
                // Facing the lower side: toward b (+) when b is lower.
                var n=lineX?new Vector3(hb<ha?1:-1,0,0):new Vector3(0,0,hb<ha?1:-1);
                var o=lineX?new Vector3(origin.X+x*c,origin.Y+lo,origin.Z+(z+(float)k/t)*c):new Vector3(origin.X+(x+(float)k/t)*c,origin.Y+lo,origin.Z+z*c);
                var u=lineX?new Vector3(0,0,c/t):new Vector3(c/t,0,0);
                Quad(Surface.Basin,o,u,new Vector3(0,hi-lo,0),n,BasinRepeat,true);
            }
        }

        // ---- walls and arches ----
        // Along each line the walls in runs (consecutive walled edges of one height, as boxes running past their ends by half the
        // thickness so they meet at corners), and each opening between rooms (consecutive open edges between the same two rooms)
        // arched; the block owns lines x0..x1-1 (and the far border) over its rows, likewise for z.
        private void Walls(PoolroomsLayout l,Vector3 origin,int x0,int z0,int x1,int z1)
        {
            for(int x=x0;x<=x1;x++){if(x==x1&&x1!=l.Width)break;Line(l,origin,x,z0,z1,true);}
            for(int z=z0;z<=z1;z++){if(z==z1&&z1!=l.Depth)break;Line(l,origin,z,x0,x1,false);}
        }
        private void Line(PoolroomsLayout l,Vector3 origin,int line,int from,int to,bool lineX)
        {
            int s=from;
            while(s<to)
            {
                int cx=lineX?line:s,cz=lineX?s:line;byte kind=EdgeKind(l,cx,cz,lineX);float top=Top(l,cx,cz,lineX);var pair=Rooms(l,cx,cz,lineX);
                int e=s+1;
                while(e<to){int ex=lineX?line:e,ez=lineX?e:line;if(EdgeKind(l,ex,ez,lineX)!=kind||Math.Abs(Top(l,ex,ez,lineX)-top)>1e-4f||!Rooms(l,ex,ez,lineX).Equals(pair))break;e++;}
                if(kind==PoolroomsLayout.Wall)WallRun(l,origin,line,s,e,top,lineX);
                else if(pair.a!=pair.b)Arch(l,origin,line,s,e,top,lineX);
                s=e;
            }
        }
        private static byte EdgeKind(PoolroomsLayout l,int x,int z,bool lineX)=>lineX?l.WallX[(l.Width+1)*z+x]:l.WallZ[l.Width*z+x];
        // The rooms on the edge's two sides (the border's outside as -1).
        private static (int a,int b) Rooms(PoolroomsLayout l,int x,int z,bool lineX)
        {
            int ax=lineX?x-1:x,az=lineX?z:z-1;return (l.Inside(ax,az)?l.Room[l.Index(ax,az)]:-1,l.Inside(x,z)?l.Room[l.Index(x,z)]:-1);
        }
        // The edge's height: the higher ceiling of its two sides.
        private static float Top(PoolroomsLayout l,int x,int z,bool lineX)
        {
            int ax=lineX?x-1:x,az=lineX?z:z-1;float h=0;
            if(l.Inside(ax,az))h=l.Ceiling[l.Index(ax,az)];if(l.Inside(x,z))h=Math.Max(h,l.Ceiling[l.Index(x,z)]);return h;
        }
        private static float Low(PoolroomsLayout l,int x,int z,bool lineX)
        {
            int ax=lineX?x-1:x,az=lineX?z:z-1;float h=float.MaxValue;
            if(l.Inside(ax,az))h=l.Ceiling[l.Index(ax,az)];if(l.Inside(x,z))h=Math.Min(h,l.Ceiling[l.Index(x,z)]);return h;
        }
        private void WallRun(PoolroomsLayout l,Vector3 origin,int line,int s,int e,float top,bool lineX)
        {
            float c=PoolroomsLayout.Cell,t=PoolroomsLayout.Thickness*.5f;
            // Past an end only where the line goes on walled (a corner or a junction), not into an opening's jamb.
            bool before=s>0&&EdgeKind(l,lineX?line:s-1,lineX?s-1:line,lineX)==PoolroomsLayout.Wall||Joins(l,line,s,lineX),after=Joins(l,line,e,lineX)||e<(lineX?l.Depth:l.Width)&&EdgeKind(l,lineX?line:e,lineX?e:line,lineX)==PoolroomsLayout.Wall;
            float a0=s*c-(before?t:0),a1=e*c+(after?t:0),y=origin.Y;
            Vector3 lo=lineX?new Vector3(origin.X+line*c-t,y,origin.Z+a0):new Vector3(origin.X+a0,y,origin.Z+line*c-t);
            Vector3 hi=lineX?new Vector3(origin.X+line*c+t,y+top,origin.Z+a1):new Vector3(origin.X+a1,y+top,origin.Z+line*c+t);
            Box(lo,hi);
        }
        // Whether a wall on the crossing line meets this line's point `at` (a corner or a T).
        private static bool Joins(PoolroomsLayout l,int line,int at,bool lineX)
        {
            // The crossing line runs along the other axis through the point (line, at): its edges on either side of `line`.
            for(int k=-1;k<=0;k++)
            {
                int cx=lineX?line+k:at,cz=lineX?at:line+k;
                if(cx<0||cz<0||(lineX?cx>=l.Width:cz>=l.Depth))continue;
                if(lineX?cz<=l.Depth&&EdgeKind(l,cx,cz,false)==PoolroomsLayout.Wall:cx<=l.Width&&EdgeKind(l,cx,cz,true)==PoolroomsLayout.Wall)return true;
            }
            return false;
        }
        // A box's four sides (its ends too: an opening's jambs; those inside a crossing wall are hidden), no top or bottom (under
        // the ceiling, on the floor).
        private void Box(Vector3 lo,Vector3 hi)
        {
            float w=hi.X-lo.X,h=hi.Y-lo.Y,d=hi.Z-lo.Z;
            Quad(Surface.Wall,new Vector3(lo.X,lo.Y,lo.Z),new Vector3(w,0,0),new Vector3(0,h,0),-Vector3.UnitZ,WallRepeat,true);
            Quad(Surface.Wall,new Vector3(hi.X,lo.Y,hi.Z),new Vector3(-w,0,0),new Vector3(0,h,0),Vector3.UnitZ,WallRepeat,true);
            Quad(Surface.Wall,new Vector3(hi.X,lo.Y,lo.Z),new Vector3(0,0,d),new Vector3(0,h,0),Vector3.UnitX,WallRepeat,true);
            Quad(Surface.Wall,new Vector3(lo.X,lo.Y,hi.Z),new Vector3(0,0,-d),new Vector3(0,h,0),-Vector3.UnitX,WallRepeat,true);
        }
        // An opening's arch: half an ellipse from jamb to jamb (the walls' ends, at the opening's edge points), springing at ArchSpring and rising under the lower
        // ceiling; its soffit, and both faces' spandrels up to the edge's height.
        private void Arch(PoolroomsLayout l,Vector3 origin,int line,int s,int e,float top,bool lineX)
        {
            float c=PoolroomsLayout.Cell,t=PoolroomsLayout.Thickness*.5f,y=origin.Y;
            float low=Low(l,lineX?line:s,lineX?s:line,lineX);
            float a0=s*c,a1=e*c,half=(a1-a0)*.5f,mid=(a0+a1)*.5f,spring=PoolroomsLayout.ArchSpring,rise=Math.Max(.3f,Math.Min(half,low-spring-.25f));
            Vector3 P(float along,float up,float side)=>lineX?new Vector3(origin.X+line*c+side,y+up,origin.Z+along):new Vector3(origin.X+along,y+up,origin.Z+line*c+side);
            var axis=lineX?Vector3.UnitZ:Vector3.UnitX;var across=lineX?Vector3.UnitX:Vector3.UnitZ;
            float arc=0;
            for(int k=0;k<ArchSegments;k++)
            {
                double t0=Math.PI*k/ArchSegments,t1=Math.PI*(k+1)/ArchSegments;
                float u0=mid-half*(float)Math.Cos(t0),v0=spring+rise*(float)Math.Sin(t0),u1=mid-half*(float)Math.Cos(t1),v1=spring+rise*(float)Math.Sin(t1);
                // The soffit's piece, facing the opening's middle.
                double tm=(t0+t1)*.5;var n=Vector3.Normalize(axis*(float)(half*Math.Cos(tm))-Vector3.UnitY*(float)(rise*Math.Sin(tm)));
                float len=MathF.Sqrt((u1-u0)*(u1-u0)+(v1-v0)*(v1-v0));
                float r=WallRepeat,ua=arc/r,ub=(arc+len)/r;
                Strip(Surface.Wall,P(u0,v0,-t),P(u1,v1,-t),P(u1,v1,t),P(u0,v0,t),n,new Vector2(ua,-t/r),new Vector2(ub,-t/r),new Vector2(ub,t/r),new Vector2(ua,t/r));
                arc+=len;
                // The spandrels' strips up to the top, on both faces (uv as a wall's: along the run and up).
                Strip(Surface.Wall,P(u0,v0,-t),P(u0,top,-t),P(u1,top,-t),P(u1,v1,-t),-across,new Vector2(u0/r,v0/r),new Vector2(u0/r,top/r),new Vector2(u1/r,top/r),new Vector2(u1/r,v1/r));
                Strip(Surface.Wall,P(u1,v1,t),P(u1,top,t),P(u0,top,t),P(u0,v0,t),across,new Vector2(u1/r,v1/r),new Vector2(u1/r,top/r),new Vector2(u0/r,top/r),new Vector2(u0/r,v0/r));
            }
        }

        // ---- pillars and lights ----
        private void Pillar(Vector3 at,float h)
        {
            float rad=PoolroomsLayout.PillarSize*.5f,circ=MathF.PI*2*rad;
            for(int k=0;k<Round;k++)
            {
                float a0=MathF.PI*2*k/Round,a1=MathF.PI*2*(k+1)/Round;
                var d0=new Vector3(MathF.Cos(a0),0,MathF.Sin(a0));var d1=new Vector3(MathF.Cos(a1),0,MathF.Sin(a1));var dm=Vector3.Normalize(d0+d1);
                float u0=circ*k/Round/WallRepeat,u1=circ*(k+1)/Round/WallRepeat,v1=h/WallRepeat;
                Strip(Surface.Wall,at+d0*rad,at+d0*rad+new Vector3(0,h,0),at+d1*rad+new Vector3(0,h,0),at+d1*rad,dm,new Vector2(u0,0),new Vector2(u0,v1),new Vector2(u1,v1),new Vector2(u1,0),d0,d1);
            }
        }
        // A flat disc facing down at `at` (its uv the unit disc's, centre at 0.5).
        private void Disc(Surface s,Vector3 at,float rad)
        {
            var p=Parts[(int)s];int c=p.Positions.Count;p.Positions.Add(at);p.Normals.Add(-Vector3.UnitY);p.Uv.Add(new Vector2(.5f,.5f));
            for(int k=0;k<=Round;k++){float a=MathF.PI*2*k/Round;p.Positions.Add(at+new Vector3(MathF.Cos(a)*rad,0,MathF.Sin(a)*rad));p.Normals.Add(-Vector3.UnitY);p.Uv.Add(new Vector2(.5f+.5f*MathF.Cos(a),.5f+.5f*MathF.Sin(a)));}
            for(int k=0;k<Round;k++)Tri(p,c,c+1+k,c+2+k,-Vector3.UnitY);
        }

        // ---- the primitives ----
        // A quad from `o` along u and v facing n; uv0 from the world position (`wall`: by its run along x or z and its height;
        // otherwise by x and z) over `repeat` metres.
        private void Quad(Surface s,Vector3 o,Vector3 u,Vector3 v,Vector3 n,float repeat,bool wall=false)
        {
            var p=Parts[(int)s];int k=p.Positions.Count;var corners=new[]{o,o+u,o+u+v,o+v};
            foreach(var q in corners)
            {
                p.Positions.Add(q);p.Normals.Add(n);
                p.Uv.Add(wall?new Vector2((Math.Abs(n.X)>.5f?q.Z:q.X)/repeat,q.Y/repeat):new Vector2(q.X/repeat,q.Z/repeat));
            }
            Tri(p,k,k+1,k+2,n);Tri(p,k,k+2,k+3,n);
        }
        // A quad a, b, c, d (in order round it) facing n, with each corner's uv. Optional per-side normals (n0 at a and b, n1 at c
        // and d) smooth a round surface.
        private void Strip(Surface s,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud,Vector3? n0=null,Vector3? n1=null)
        {
            var p=Parts[(int)s];int k=p.Positions.Count;
            p.Positions.Add(a);p.Positions.Add(b);p.Positions.Add(c);p.Positions.Add(d);
            var na=n0??n;var nd=n1??n;p.Normals.Add(na);p.Normals.Add(na);p.Normals.Add(nd);p.Normals.Add(nd);
            p.Uv.Add(ua);p.Uv.Add(ub);p.Uv.Add(uc);p.Uv.Add(ud);
            Tri(p,k,k+1,k+2,n);Tri(p,k,k+2,k+3,n);
        }
        // One triangle wound to face n: (b-a)x(c-a) along n is Unity's front (clockwise seen from n's side).
        private static void Tri(Part p,int a,int b,int c,Vector3 n)
        {
            var f=Vector3.Cross(p.Positions[b]-p.Positions[a],p.Positions[c]-p.Positions[a]);
            if(Vector3.Dot(f,n)>=0){p.Indices.Add(a);p.Indices.Add(b);p.Indices.Add(c);}else{p.Indices.Add(a);p.Indices.Add(c);p.Indices.Add(b);}
        }
    }
}
