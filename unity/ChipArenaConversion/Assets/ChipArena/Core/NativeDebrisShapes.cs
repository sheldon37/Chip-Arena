using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>How loose debris is drawn and thrown: broken pieces that follow their material (the default), or the game's
    /// older voxel cubes (kept for a voxel map).</summary>
    public enum NativeDebrisStyle{Realistic,Voxel}
    /// <summary>A debris piece's shape. Block is the voxel cube (and the kit's plain boxes: the preview stand-in's walls);
    /// the rest are broken pieces: a rough concrete chunk, a brick snapped at an end, a board with torn ends, a splinter,
    /// a bent metal panel, a glass shard and a small stone.</summary>
    public enum NativeDebrisShape:byte{Block,Rock,Brick,Plank,Splinter,Panel,Shard,Pebble}

    /// <summary>
    /// The debris style switch (owner 2026-11-13: slams and breaking boxes throw real pieces, "we can keep the information
    /// however in case I want to make a 'voxel' map"). One switch for everything the destruction kit throws: prop breaks, the
    /// loose fragments a broken prop leaves, collapse rubble and the slams' floor debris.
    /// A map turns the voxel style on in MapLooks.json: `"debris":"voxel"` on its look (MapLook.debris; anything else, or
    /// nothing, is the realistic style). <see cref="VoxelMaps"/> does the same from code by map id, and <see cref="Preview"/>
    /// forces a style everywhere (a preview hook for tools; null follows the map). Presentation only.
    /// </summary>
    public static class NativeDebrisLook
    {
        public const NativeDebrisStyle Default=NativeDebrisStyle.Realistic;
        public const string VoxelKey="voxel";
        /// <summary>Map ids that draw voxel debris whatever their look says (empty: no shipped map is a voxel map).</summary>
        public static readonly HashSet<string> VoxelMaps=new HashSet<string>(StringComparer.Ordinal);
        /// <summary>A style forced on every map (DEV preview); null follows the map.</summary>
        public static NativeDebrisStyle? Preview;
        /// <summary>The style a look's `debris` value names.</summary>
        public static NativeDebrisStyle Parse(string debris)=>string.Equals(debris,VoxelKey,StringComparison.OrdinalIgnoreCase)?NativeDebrisStyle.Voxel:Default;
        /// <summary>The style of map <paramref name="map"/> whose look carries <paramref name="debris"/>.</summary>
        public static NativeDebrisStyle For(string map,string debris)=>Preview??(map!=null&&VoxelMaps.Contains(map)?NativeDebrisStyle.Voxel:Parse(debris));
    }

    /// <summary>One piece shape as flat-shaded triangles inside the unit box (-.5 .. .5 on each axis, filling it): nine
    /// floats a triangle in <see cref="P"/>, its facet normal in <see cref="N"/>.</summary>
    public sealed class NativeDebrisMesh{public float[] P,N;public int Triangles;}

    /// <summary>
    /// The broken-piece meshes, made in code once (nothing loaded, nothing in the asset zip): a few variants of each shape,
    /// all low-poly and flat-shaded so they sit in the game's look. A piece's record gives its box (full edges), so one
    /// mesh serves every size; the drawer scales the mesh by the box and turns the facet normals by the inverse scale.
    /// Pure and deterministic: the same variant is the same mesh on every client.
    /// </summary>
    public static class NativeDebrisShapes
    {
        public const int Variants=6;
        private static readonly NativeDebrisMesh[] cache=new NativeDebrisMesh[8*Variants];
        private static readonly object gate=new object();
        /// <summary>Variant <paramref name="variant"/> (any int) of <paramref name="shape"/>; Block has no mesh (null: the cube).</summary>
        public static NativeDebrisMesh Mesh(NativeDebrisShape shape,int variant)
        {
            if(shape==NativeDebrisShape.Block)return null;int v=((variant%Variants)+Variants)%Variants,k=(int)shape*Variants+v;var m=cache[k];if(m!=null)return m;
            lock(gate){m=cache[k];if(m==null)cache[k]=m=Build(shape,v);}return m;
        }
        /// <summary>The most triangles any piece draws (the drawer's budget per piece).</summary>
        public const int MaxTriangles=48;

        private sealed class Builder
        {
            public readonly List<V3> Points=new List<V3>();public readonly List<int> Faces=new List<int>();
            public int Add(float x,float y,float z){Points.Add(new V3(x,y,z));return Points.Count-1;}
            public void Tri(int a,int b,int c){Faces.Add(a);Faces.Add(b);Faces.Add(c);}
            public void Quad(int a,int b,int c,int d){Tri(a,b,c);Tri(a,c,d);}
            /// <summary>Fits the points to the unit box and writes flat triangles; with <paramref name="orient"/> each
            /// triangle is turned to face away from the shape's middle (shapes that are convex enough for that).</summary>
            public NativeDebrisMesh Finish(bool orient)
            {
                V3 lo=Points[0],hi=Points[0];foreach(var p in Points){lo=new V3(Math.Min(lo.x,p.x),Math.Min(lo.y,p.y),Math.Min(lo.z,p.z));hi=new V3(Math.Max(hi.x,p.x),Math.Max(hi.y,p.y),Math.Max(hi.z,p.z));}
                V3 mid=(lo+hi)*.5f;float sx=1/Math.Max(1e-5f,hi.x-lo.x),sy=1/Math.Max(1e-5f,hi.y-lo.y),sz=1/Math.Max(1e-5f,hi.z-lo.z);
                var fit=new V3[Points.Count];for(int i=0;i<fit.Length;i++){var p=Points[i]-mid;fit[i]=new V3(p.x*sx,p.y*sy,p.z*sz);}
                var P=new List<float>();var N=new List<float>();
                for(int f=0;f<Faces.Count;f+=3)
                {
                    V3 a=fit[Faces[f]],b=fit[Faces[f+1]],c=fit[Faces[f+2]],e1=b-a,e2=c-a;
                    var n=new V3(e1.y*e2.z-e1.z*e2.y,e1.z*e2.x-e1.x*e2.z,e1.x*e2.y-e1.y*e2.x);float l=(float)Math.Sqrt(n.x*n.x+n.y*n.y+n.z*n.z);if(l<1e-7f)continue;n=n*(1/l);
                    if(orient){V3 g=(a+b+c)*(1f/3);if(n.x*g.x+n.y*g.y+n.z*g.z<0){var t=b;b=c;c=t;n=n*-1;}}
                    P.Add(a.x);P.Add(a.y);P.Add(a.z);P.Add(b.x);P.Add(b.y);P.Add(b.z);P.Add(c.x);P.Add(c.y);P.Add(c.z);N.Add(n.x);N.Add(n.y);N.Add(n.z);
                }
                return new NativeDebrisMesh{P=P.ToArray(),N=N.ToArray(),Triangles=N.Count/3};
            }
        }
        private static float H(int shape,int variant,int k)=>(float)NativeWoundCarve.Hash(0x5eb7150u+(uint)shape*977u,variant,k);

        private static NativeDebrisMesh Build(NativeDebrisShape shape,int v)
        {
            switch(shape)
            {
                case NativeDebrisShape.Rock:return Rock(v);
                case NativeDebrisShape.Pebble:return Pebble(v);
                case NativeDebrisShape.Brick:return Brick(v);
                case NativeDebrisShape.Plank:return Plank(v);
                case NativeDebrisShape.Splinter:return Splinter(v);
                case NativeDebrisShape.Panel:return Panel(v);
                default:return Shard(v);
            }
        }
        // A rough chunk: an icosahedron with every corner pulled in or out, then cut by two or three planes, so it shows a few
        // broad flat breaks among the small facets (broken concrete, not a ball).
        private static NativeDebrisMesh Rock(int v)
        {
            var b=new Builder();const float t=1.618034f;int s=(int)NativeDebrisShape.Rock;
            float[] ico={-1,t,0,1,t,0,-1,-t,0,1,-t,0,0,-1,t,0,1,t,0,-1,-t,0,1,-t,t,0,-1,t,0,1,-t,0,-1,-t,0,1};
            for(int i=0;i<12;i++){float r=(.74f+.3f*H(s,v,i))/1.902f;b.Add(ico[i*3]*r,ico[i*3+1]*r*(.8f+.25f*H(s,v,40)),ico[i*3+2]*r);}
            int cuts=2+(v%2);
            for(int c=0;c<cuts;c++)
            {
                float u=H(s,v,50+c)*2-1,a=H(s,v,60+c)*6.2831855f,q=(float)Math.Sqrt(Math.Max(0,1-u*u));var d=new V3(q*(float)Math.Cos(a),u,q*(float)Math.Sin(a));float cut=.42f+.2f*H(s,v,70+c);
                for(int i=0;i<12;i++){var p=b.Points[i];float along=p.x*d.x+p.y*d.y+p.z*d.z;if(along>cut)b.Points[i]=p-d*(along-cut);}
            }
            int[] f={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
            for(int i=0;i<f.Length;i+=3)b.Tri(f[i],f[i+1],f[i+2]);return b.Finish(true);
        }
        // A small stone: a pulled octahedron.
        private static NativeDebrisMesh Pebble(int v)
        {
            var b=new Builder();int s=(int)NativeDebrisShape.Pebble;float[] o={1,0,0,-1,0,0,0,1,0,0,-1,0,0,0,1,0,0,-1};
            for(int i=0;i<6;i++){float r=.6f+.4f*H(s,v,i);b.Add(o[i*3]*r+(H(s,v,10+i)-.5f)*.3f,o[i*3+1]*r+(H(s,v,20+i)-.5f)*.3f,o[i*3+2]*r+(H(s,v,30+i)-.5f)*.3f);}
            int[] f={0,2,4,4,2,1,1,2,5,5,2,0,4,3,0,1,3,4,5,3,1,0,3,5};for(int i=0;i<f.Length;i+=3)b.Tri(f[i],f[i+1],f[i+2]);return b.Finish(true);
        }
        // An end of a box torn off: its four corners pulled back by different amounts round a point that sticks out or in.
        private static void TornEnd(Builder b,int s,int v,int salt,float sign,float halfX,float halfY,float end,float bite,int[] ring,out int tip)
        {
            float[] cx={-1,1,1,-1},cy={-1,-1,1,1};
            for(int i=0;i<4;i++)ring[i]=b.Add(cx[i]*halfX*(.86f+.14f*H(s,v,salt+i)),cy[i]*halfY*(.86f+.14f*H(s,v,salt+4+i)),sign*(end-bite*H(s,v,salt+8+i)));
            tip=b.Add((H(s,v,salt+12)-.5f)*halfX,(H(s,v,salt+13)-.5f)*halfY,sign*(end-bite*(.2f+.9f*H(s,v,salt+14))+bite*.35f));
        }
        private static NativeDebrisMesh Prism(int s,int v,float halfX,float halfY,float halfZ,float biteNear,float biteFar)
        {
            var b=new Builder();var near=new int[4];var far=new int[4];TornEnd(b,s,v,100,-1,halfX,halfY,halfZ,biteNear,near,out int nt);TornEnd(b,s,v,200,1,halfX,halfY,halfZ,biteFar,far,out int ft);
            for(int i=0;i<4;i++){int j=(i+1)%4;b.Quad(near[i],near[j],far[j],far[i]);b.Tri(near[i],near[j],nt);b.Tri(far[i],far[j],ft);}
            return b.Finish(true);
        }
        // A brick snapped across: one end torn (every other variant, both).
        private static NativeDebrisMesh Brick(int v){int s=(int)NativeDebrisShape.Brick;return Prism(s,v,.5f,.5f,.5f,v%2==0?.03f:.34f,.38f);}
        // A board: long, its ends torn to points and steps.
        private static NativeDebrisMesh Plank(int v){int s=(int)NativeDebrisShape.Plank;return Prism(s,v,.5f,.5f,.5f,.1f+.12f*H(s,v,1),.16f+.16f*H(s,v,2));}
        // A splinter: three-sided, thick near one end and running out to a point.
        private static NativeDebrisMesh Splinter(int v)
        {
            var b=new Builder();int s=(int)NativeDebrisShape.Splinter;int[] back=new int[3],mid=new int[3];float at=-.5f+.25f*H(s,v,1);
            for(int i=0;i<3;i++){float a=i*2.0943951f+H(s,v,2)*2;back[i]=b.Add((float)Math.Cos(a)*.22f,(float)Math.Sin(a)*.22f,-.5f-.05f*H(s,v,5+i));mid[i]=b.Add((float)Math.Cos(a+.3f)*(.4f+.1f*H(s,v,8+i)),(float)Math.Sin(a+.3f)*(.4f+.1f*H(s,v,11+i)),at);}
            int tip=b.Add((H(s,v,14)-.5f)*.3f,(H(s,v,15)-.5f)*.3f,.5f);b.Tri(back[0],back[1],back[2]);
            for(int i=0;i<3;i++){int j=(i+1)%3;b.Quad(back[i],back[j],mid[j],mid[i]);b.Tri(mid[i],mid[j],tip);}
            return b.Finish(true);
        }
        // A sheet-metal panel: a thin plate four by three corners, creased along a slanting line and curled at one corner,
        // its outline torn. Both faces and the rim are drawn.
        private static NativeDebrisMesh Panel(int v)
        {
            var b=new Builder();int s=(int)NativeDebrisShape.Panel;const int nx=4,nz=3;const float thick=.16f;
            float ca=H(s,v,1)*3.14159f,cx=(float)Math.Cos(ca),cz=(float)Math.Sin(ca),off=(H(s,v,2)-.5f)*.3f,fold=.55f+.5f*H(s,v,3),curl=.5f+.6f*H(s,v,4);int corner=(int)(H(s,v,5)*4)%4;
            var top=new int[nx*nz];var bottom=new int[nx*nz];
            for(int pass=0;pass<2;pass++)for(int j=0;j<nz;j++)for(int i=0;i<nx;i++)
            {
                float x=i/(float)(nx-1)-.5f,z=j/(float)(nz-1)-.5f;bool edge=i==0||i==nx-1||j==0||j==nz-1;
                if(edge){x+=(H(s,v,20+j*nx+i)-.5f)*.14f;z+=(H(s,v,40+j*nx+i)-.5f)*.18f;}
                float d=Math.Abs(x*cx+z*cz-off),y=fold*d;float kx=corner%2==0?-.5f:.5f,kz=corner<2?-.5f:.5f,near=1-Math.Min(1,(float)Math.Sqrt((x-kx)*(x-kx)+(z-kz)*(z-kz))/.6f);y+=curl*near*near;
                int id=b.Add(x,y-(pass==0?0:thick),z);if(pass==0)top[j*nx+i]=id;else bottom[j*nx+i]=id;
            }
            for(int j=0;j<nz-1;j++)for(int i=0;i<nx-1;i++)
            {
                int a=j*nx+i,c=a+1,d=a+nx,e=d+1;b.Tri(top[a],top[d],top[e]);b.Tri(top[a],top[e],top[c]);b.Tri(bottom[a],bottom[e],bottom[d]);b.Tri(bottom[a],bottom[c],bottom[e]);
            }
            // The rim, walked round the outline (counter-clockwise seen from above).
            var rim=new List<int>();for(int i=0;i<nx;i++)rim.Add(i);for(int j=1;j<nz;j++)rim.Add(j*nx+nx-1);for(int i=nx-2;i>=0;i--)rim.Add((nz-1)*nx+i);for(int j=nz-2;j>=1;j--)rim.Add(j*nx);
            for(int i=0;i<rim.Count;i++){int a=rim[i],c=rim[(i+1)%rim.Count];b.Tri(top[a],top[c],bottom[c]);b.Tri(top[a],bottom[c],bottom[a]);}
            return b.Finish(false);
        }
        // A glass shard: a thin sliver with three or four corners.
        private static NativeDebrisMesh Shard(int v)
        {
            var b=new Builder();int s=(int)NativeDebrisShape.Shard,n=3+v%2;var top=new int[n];var bottom=new int[n];
            for(int i=0;i<n;i++){float a=i*6.2831855f/n+(H(s,v,i)-.5f)*.9f,r=.3f+.2f*H(s,v,10+i);float x=(float)Math.Cos(a)*r,z=(float)Math.Sin(a)*r*(1.2f+.8f*H(s,v,20));top[i]=b.Add(x,.5f,z);bottom[i]=b.Add(x*.94f,-.5f,z*.94f);}
            for(int i=1;i<n-1;i++){b.Tri(top[0],top[i],top[i+1]);b.Tri(bottom[0],bottom[i+1],bottom[i]);}
            for(int i=0;i<n;i++){int j=(i+1)%n;b.Quad(top[i],top[j],bottom[j],bottom[i]);}
            return b.Finish(true);
        }
    }

    /// <summary>
    /// How heavy a melee slam lands on the floor, 0..1 (owner 2026-11-13: "a Greatsword slam is big and heavy, a Fangs
    /// landing is light"): the weapon's own weight, raised by the fall for a plunge. It sizes the slam's floor debris
    /// (NativeStructureFx.Slam): how many chunks, how big, how far, and whether a dust ring rolls out. Presentation only.
    /// </summary>
    public static class NativeSlamDebris
    {
        /// <summary>The weapon's weight by its source spec or weapon id: the greatswords 1, the shield .8, the sword, katanas
        /// and glaive .55, the daggers and Fangs .25.</summary>
        public static float Weapon(string spec)
        {
            if(string.IsNullOrEmpty(spec))return .55f;
            if(spec.IndexOf("greatsword",StringComparison.Ordinal)>=0)return 1;if(spec=="shield")return .8f;
            if(spec=="daggers"||spec=="fangs"||spec.IndexOf("dagger",StringComparison.Ordinal)>=0||spec.IndexOf("fangs",StringComparison.Ordinal)>=0)return .25f;
            return .55f;
        }
        /// <summary>A plunge's weight: the weapon's, raised by the fall's 2 to 8 m share <paramref name="heavy"/>.</summary>
        public static float Plunge(string spec,float heavy){float w=Weapon(spec),h=heavy<0?0:heavy>1?1:heavy;return Math.Min(1,w*(.6f+.55f*h)+.12f*h);}
        /// <summary>A jump attack's weight: the weapon's by the impact's strength (0..1).</summary>
        public static float Jump(string spec,float strength){float s=strength<0?0:strength>1?1:strength;return Weapon(spec)*(.45f+.4f*s);}
        /// <summary>The heaviest slams (from here up) roll a dust ring out over the floor.</summary>
        public const float RingFrom=.6f;
        /// <summary>The floor chunks a slam of <paramref name="weight"/> throws at full effects, and its small stones.</summary>
        public static int Chunks(float weight)=>weight<=0?0:(int)Math.Round(3+11*Clamp(weight));
        public static int Stones(float weight)=>weight<=0?0:(int)Math.Round(5+13*Clamp(weight));
        /// <summary>The biggest chunk's edge (m): .1 for a light landing to .34 for the heaviest.</summary>
        public static float Edge(float weight)=>.1f+.24f*Clamp(weight);
        /// <summary>How far the debris ring sits from the point of impact (m).</summary>
        public static float Reach(float weight)=>.25f+.75f*Clamp(weight);
        private static float Clamp(float v)=>v<0?0:v>1?1:v;
    }
}
