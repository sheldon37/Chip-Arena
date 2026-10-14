using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Drawn faces that share a plane (the surface artifacts on the dressed maps, 2026-10-13). Every arena block and every
    /// dressing-kit part is a chamfered box through NativeBlockLit (its edges bevel by <see cref="NativeBlockLooks.Chamfer"/>,
    /// at most <see cref="NativeBlockLooks.ClampShare"/> of the half extent). Two faces that face the same way, lie in one
    /// plane and overlap on their flat parts draw at the same depth, and the depth test then picks a winner per pixel that
    /// changes with the view and the distance (z-fighting: the flicker, stripes and speckle on the Factory's machine-green
    /// lathes, the specimen tanks, the Brickfall bridge steel). The maps' blocks and the kits make many such pairs by
    /// construction: a part laid flush on its block (a cap, a band, a flange, a lid, paint, a vein of ink), two parts of one
    /// fitting meeting flush, two blocks that run into each other (a pier up through a floor, a step into its landing).
    /// Render only: this decides which face is meant to be seen and moves only that face out along its normal, in whole
    /// <see cref="Step"/>s, so its block or part grows by millimetres on that one side. Colliders, ids, navigation, hazards,
    /// spawns and the content data never change, and no camera or depth state hides anything.
    /// <list type="bullet">
    /// <item>Which face wins: a kit part over the block it shares a plane with; a later part over an earlier one (a kit lays
    /// its base first and its details on it); between blocks, the larger face (a floor over the pier under it, a landing over
    /// the step that runs into it), the later block on a tie. Two parts of one <see cref="Key"/> (look, colour, flags and driver)
    /// may take either order, whichever keeps the lift smaller; they still end apart.</item>
    /// <item>How far: overlapping faces of one facing end at least <see cref="Step"/> apart, faces already that far apart keep
    /// their order, no face moves in and none moves more than <see cref="MaxLift"/> (the solve counts the faces it could not
    /// settle within that; none on today's maps).</item>
    /// <item>Why 2 mm: below a pixel from 2 m away at 1080p, while far above the depth noise of the float depth buffer at any
    /// distance on the maps (about 1e-7 of the distance); a 24-bit depth buffer resolves it to about 30 m with these near
    /// planes.</item>
    /// </list>
    /// The blocks are solved alone (<see cref="Blocks"/>, NativeArenaDressing and the Ward Zero menu backdrop), so a kit
    /// (<see cref="Over"/>, from the Factory, Cyber City and Ward Zero builders) finds them where the dressing draws them.
    /// Deterministic: the same boxes give the same lifts on every machine. Allocates only while a map's dressing is built.
    /// </summary>
    public static class NativeSurfaceLayers
    {
        /// <summary>The least distance (m) between two overlapping drawn faces of one facing; closer than this is one plane.</summary>
        public const float Step=.002f;
        /// <summary>The most any face moves on a map (m).</summary>
        public const float MaxLift=.016f;
        /// <summary>The most a backdrop face moves (m): the skylines stand 50-400 m off, where a few centimetres are under a
        /// pixel, and their flush members stack deeper (a dozen roof members ending on one gable line).</summary>
        public const float BackdropLift=.064f;
        /// <summary>Overlaps of the flat parts smaller than this (m²) are left alone (inside a bevel, under a pixel).</summary>
        public const float MinArea=1e-4f;
        private const double Facing=.9999,Slack=1e-7;

        /// <summary>One drawn box: world centre, half extents, rotation (Unity's x, y, z, w; all zero reads as none), the
        /// chamfer its draw gives the lattice (0: <see cref="NativeBlockLooks.Chamfer"/>, the blocks' and the Cyber City and Ward
        /// Zero kits'; below 0: none, a baked scenery box), the <see cref="Key"/> of its surface (-1: never interchangeable), and
        /// the faces it does not draw (bit f for face f: -x, +x, -y, +y, -z, +z).</summary>
        public struct Box {public double X,Y,Z,Hx,Hy,Hz,Qx,Qy,Qz,Qw,Bevel;public long Material;public int Skip;}

        /// <summary>How a backdrop is solved (its boxes stand tens to hundreds of metres off): overlaps narrower than `MinWidth`
        /// (m) are under a pixel there and left alone, and two faces that share less than `Share` of the smaller one (members
        /// meeting at a corner) may lie either way, so long runs of flush members do not stack up.</summary>
        public sealed class Backdrop {public double MinWidth=.02,Share=.5;}
        /// <summary>The backdrops' solve settings.</summary>
        public static readonly Backdrop Far=new Backdrop();
        /// <summary>A surface key: two parts with one key draw alike (look, colour, flags and the driver that animates them), so
        /// either may lie over the other.</summary>
        public static long Key(int look,int color,int flags,int drive=0)=>((long)(drive&0x3F)<<56)|((long)(look&0xFFFF)<<40)|((long)(flags&0xFFFF)<<24)|(uint)(color&0xFFFFFF);

        /// <summary>A map's blocks as boxes (block i at i; a missing or empty block is a zero box, which never moves).</summary>
        public static Box[] BlockBoxes(ContentBlock[] blocks)
        {
            blocks=blocks??Array.Empty<ContentBlock>();var o=new Box[blocks.Length];
            for(int i=0;i<blocks.Length;i++)
            {
                var b=blocks[i];if(b==null)continue;var r=b.rotation;
                o[i]=new Box{X=b.pos.x,Y=b.pos.y,Z=b.pos.z,Hx=b.size.x*.5,Hy=b.size.y*.5,Hz=b.size.z*.5,Qx=r?.x??0,Qy=r?.y??0,Qz=r?.z??0,Qw=r?.w??1,Material=-1};
            }
            return o;
        }
        /// <summary>A kit part as a world box: offset (x, y, z) from its parent block's rest centre in the block's own axes,
        /// half extents, turned with the block (the Cyber City and Ward Zero kits' parts).</summary>
        public static Box Parented(ContentBlock parent,float x,float y,float z,float hx,float hy,float hz,long material)
        {
            if(parent==null)return default;var r=parent.rotation;double qx=r?.x??0,qy=r?.y??0,qz=r?.z??0,qw=r?.w??1,l=Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);
            if(!(l>1e-9)){qx=qy=qz=0;qw=1;}else{qx/=l;qy/=l;qz/=l;qw/=l;}
            double tx=2*(qy*z-qz*y),ty=2*(qz*x-qx*z),tz=2*(qx*y-qy*x),wx=x+qw*tx+(qy*tz-qz*ty),wy=y+qw*ty+(qz*tx-qx*tz),wz=z+qw*tz+(qx*ty-qy*tx);
            return new Box{X=parent.pos.x+wx,Y=parent.pos.y+wy,Z=parent.pos.z+wz,Hx=hx,Hy=hy,Hz=hz,Qx=qx,Qy=qy,Qz=qz,Qw=qw,Material=material};
        }
        /// <summary>The blocks' lifts, six per block (<see cref="Apply"/>): the larger face of two in one plane is drawn over.</summary>
        public static float[] Blocks(ContentBlock[] blocks)=>Solve(BlockBoxes(blocks),0,null,true,out _);
        /// <summary>A kit's lifts, six per part (<see cref="Apply"/>): `parts` (world boxes, in build order) over the map's
        /// blocks as <see cref="Blocks"/> draws them. `unresolved`: faces left closer than <see cref="Step"/> to one they overlap.</summary>
        public static float[] Over(ContentBlock[] blocks,Box[] parts,out int unresolved)
        {
            var bb=BlockBoxes(blocks);var fixedLifts=Solve(bb,0,null,true,out _);parts=parts??Array.Empty<Box>();
            var all=new Box[bb.Length+parts.Length];Array.Copy(bb,all,bb.Length);Array.Copy(parts,0,all,bb.Length,parts.Length);
            var lift=Solve(all,bb.Length,fixedLifts,false,out unresolved);var o=new float[parts.Length*6];Array.Copy(lift,bb.Length*6,o,0,o.Length);return o;
        }
        /// <summary>Box `box`'s lifts applied: its centre's shift along its own axes and its half extents' growth (m).</summary>
        public static void Apply(float[] lifts,int box,out float sx,out float sy,out float sz,out float gx,out float gy,out float gz)
        {
            int k=box*6;if(lifts==null||k<0||k+6>lifts.Length){sx=sy=sz=gx=gy=gz=0;return;}
            gx=(lifts[k]+lifts[k+1])*.5f;sx=(lifts[k+1]-lifts[k])*.5f;gy=(lifts[k+2]+lifts[k+3])*.5f;sy=(lifts[k+3]-lifts[k+2])*.5f;gz=(lifts[k+4]+lifts[k+5])*.5f;sz=(lifts[k+5]-lifts[k+4])*.5f;
        }
        /// <summary>The flat half extent of a face along an edge of half length `h` (the shader's chamfer `bevel` cut off; 0:
        /// the blocks' chamfer).</summary>
        public static double Flat(double h,double bevel=0)=>bevel<0?h:h-Math.Min(bevel>0?bevel:NativeBlockLooks.Chamfer,NativeBlockLooks.ClampShare*h);

        /// <summary>
        /// Face lifts (m) for `boxes`, six per box: its own -x, +x, -y, +y, -z, +z faces. The first `fixedCount` boxes keep
        /// `fixedLifts` (an earlier pass placed them; they never move here); the others' faces are settled one at a time, by
        /// face area (`byArea`: the smallest first, so the larger is drawn over) or else in box order (the later drawn over), each
        /// at the least lift that keeps it <see cref="Step"/> clear of every settled face it overlaps: over any that shared its
        /// plane (unless both have one <see cref="Key"/>) or stood behind it, and either behind or over one that stood in front.
        /// </summary>
        public static float[] Solve(Box[] boxes,int fixedCount,float[] fixedLifts,bool byArea,out int unresolved,float maxLift=MaxLift,Backdrop backdrop=null)
        {
            double range=Step+maxLift+Step;
            int n=boxes?.Length??0;var lift=new float[n*6];unresolved=0;if(n==0)return lift;fixedCount=Math.Max(0,Math.Min(n,fixedCount));
            if(fixedLifts!=null)Array.Copy(fixedLifts,lift,Math.Min(fixedLifts.Length,fixedCount*6));
            // Each face's frame: outward normal, centre, in-plane axes and flat half extents (F doubles a face); each box's
            // bounds grown by the search range.
            var face=new double[n*6*F];var min=new double[n*3];var max=new double[n*3];var live=new bool[n];
            for(int i=0;i<n;i++)
            {
                var b=boxes[i];live[i]=b.Hx>0&&b.Hy>0&&b.Hz>0&&Finite(b.X)&&Finite(b.Y)&&Finite(b.Z)&&Finite(b.Hx)&&Finite(b.Hy)&&Finite(b.Hz);if(!live[i])continue;
                double qx=b.Qx,qy=b.Qy,qz=b.Qz,qw=b.Qw,l=Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);if(!(l>1e-9)||!Finite(l)){qx=qy=qz=0;qw=1;}else{qx/=l;qy/=l;qz/=l;qw/=l;}
                // The rotation's columns: the box's x, y and z axes in the world.
                double[] ax={1-2*(qy*qy+qz*qz),2*(qx*qy+qw*qz),2*(qx*qz-qw*qy),2*(qx*qy-qw*qz),1-2*(qx*qx+qz*qz),2*(qy*qz+qw*qx),2*(qx*qz+qw*qy),2*(qy*qz-qw*qx),1-2*(qx*qx+qy*qy)};
                double[] h={b.Hx,b.Hy,b.Hz},c={b.X,b.Y,b.Z};
                for(int d=0;d<3;d++){double e=Math.Abs(ax[d])*h[0]+Math.Abs(ax[3+d])*h[1]+Math.Abs(ax[6+d])*h[2]+range;min[i*3+d]=c[d]-e;max[i*3+d]=c[d]+e;}
                for(int f=0;f<6;f++)
                {
                    int a=f>>1,ua=(a+1)%3,va=(a+2)%3,o=(i*6+f)*F;double sg=(f&1)==0?-1:1;
                    for(int d=0;d<3;d++){face[o+d]=ax[a*3+d]*sg;face[o+3+d]=c[d]+ax[a*3+d]*sg*h[a];face[o+6+d]=ax[ua*3+d];face[o+9+d]=ax[va*3+d];}
                    face[o+12]=Flat(h[ua],b.Bevel);face[o+13]=Flat(h[va],b.Bevel);
                }
            }
            // Pairs of boxes whose grown bounds meet (a grid over x and z; a pair is taken in the one cell that holds the low
            // corner of their bounds' overlap), then their faces of one facing that lie within the range and overlap on their flat
            // parts. A face whose whole outside (its flat part a millimetre out: nine points of it inside one box, which is
            // convex) lies inside another box is never seen: it neither moves nor holds another back (a part's foot on the floor,
            // a block's end inside a wall).
            // The cell: the boxes' mean footprint side, 2-32 m (kit parts about 2 m, scenery buildings tens of metres).
            double side=0;int sides=0;for(int i=0;i<n;i++)if(live[i]){side+=Math.Max(max[i*3]-min[i*3],max[i*3+2]-min[i*3+2]);sides++;}
            double cell=Math.Max(2,Math.Min(32,sides>0?side/sides:2));long Cell(double v)=>(long)Math.Floor(v/cell);
            var cells=new Dictionary<long,List<int>>();
            for(int i=0;i<n;i++)
            {
                if(!live[i])continue;
                for(long gx=Cell(min[i*3]);gx<=Cell(max[i*3]);gx++)for(long gz=Cell(min[i*3+2]);gz<=Cell(max[i*3+2]);gz++)
                {long key=gx<<32^(gz&0xFFFFFFFF);if(!cells.TryGetValue(key,out var list))cells[key]=list=new List<int>();list.Add(i);}
            }
            var edges=new List<int>();var poly=new double[32];var next=new double[32];var cover=new int[n*6];
            foreach(var pair in cells)
            {
                var list=pair.Value;long cx=pair.Key>>32,cz=(int)(pair.Key&0xFFFFFFFF);
                for(int p=0;p<list.Count;p++)for(int q=p+1;q<list.Count;q++)
                {
                    int i=list[p],j=list[q];if(i<fixedCount&&j<fixedCount)continue;
                    if(min[i*3]>max[j*3]||min[j*3]>max[i*3]||min[i*3+1]>max[j*3+1]||min[j*3+1]>max[i*3+1]||min[i*3+2]>max[j*3+2]||min[j*3+2]>max[i*3+2])continue;
                    if(Cell(Math.Max(min[i*3],min[j*3]))!=cx||Cell(Math.Max(min[i*3+2],min[j*3+2]))!=cz)continue;
                    for(int fa=0;fa<6;fa++)
                    {
                        // A face the box does not draw neither shows nor fights (fb's own skip is tested below).
                        if((boxes[i].Skip>>fa&1)!=0){if((boxes[j].Skip>>fa&1)==0&&j>=fixedCount&&cover[j*6+fa]!=Seen)cover[j*6+fa]=Covered(face,(j*6+fa)*F,boxes[i],face,i);continue;}
                        if(i>=fixedCount&&cover[i*6+fa]!=Seen)cover[i*6+fa]=Covered(face,(i*6+fa)*F,boxes[j],face,j);if(j>=fixedCount&&cover[j*6+fa]!=Seen)cover[j*6+fa]=Covered(face,(j*6+fa)*F,boxes[i],face,i);
                        for(int fb=0;fb<6;fb++)
                        {
                            if((boxes[j].Skip>>fb&1)!=0||!Touch(face,(i*6+fa)*F,(j*6+fb)*F,poly,next,range,out double shared,out double width))continue;
                            if(backdrop!=null&&width<backdrop.MinWidth)continue;
                            // A small share of both faces (members meeting at a corner) may lie either way on a backdrop.
                            bool either=backdrop!=null&&shared<backdrop.Share*Math.Min(face[(i*6+fa)*F+12]*face[(i*6+fa)*F+13],face[(j*6+fb)*F+12]*face[(j*6+fb)*F+13])*4;
                            edges.Add(either?~(i*6+fa):i*6+fa);edges.Add(j*6+fb);
                        }
                    }
                }
            }
            if(edges.Count==0)return lift;
            // Each face's neighbours, hidden faces left out.
            var count=new int[n*6+1];
            // An edge's first face is stored complemented when its faces may lie either way (adj holds the neighbour complemented then).
            var free=new bool[edges.Count/2];for(int e=0;e<edges.Count;e+=2)if(edges[e]<0){edges[e]=~edges[e];free[e/2]=true;}
            for(int e=0;e<edges.Count;e+=2){if(cover[edges[e]]==Seen||cover[edges[e+1]]==Seen){edges[e]=edges[e+1]=-1;continue;}count[edges[e]+1]++;count[edges[e+1]+1]++;}
            for(int f=0;f<n*6;f++)count[f+1]+=count[f];
            var adj=new int[count[n*6]];var fill=new int[n*6];for(int e=0;e<edges.Count;e+=2){int a=edges[e],b=edges[e+1];if(a<0)continue;bool fr=free[e/2];adj[count[a]+fill[a]++]=fr?~b:b;adj[count[b]+fill[b]++]=fr?~a:a;}
            // The faces to settle, in order; settled ones (the fixed boxes') are final.
            var done=new bool[n*6];for(int f=0;f<fixedCount*6;f++)done[f]=true;var todo=new List<int>();var area=new double[n*6];
            for(int f=fixedCount*6;f<n*6;f++){if(count[f+1]==count[f]||cover[f]==Seen)continue;todo.Add(f);area[f]=face[f*F+12]*face[f*F+13];}
            if(byArea)todo.Sort((a,b)=>{int c=area[a].CompareTo(area[b]);return c!=0?c:a.CompareTo(b);});
            var band=new List<double>();var ends=new List<double>();
            foreach(int f in todo)
            {
                int i=f/6,o=f*F;double lo=0;band.Clear();ends.Clear();
                for(int k=count[f];k<count[f+1];k++)
                {
                    int g=adj[k];bool loose=g<0;if(loose)g=~g;if(!done[g])continue;int j=g/6,p=g*F;
                    // g's plane against f's, along f's normal, as it is drawn now.
                    double d=face[o]*(face[p+3]-face[o+3])+face[o+1]*(face[p+4]-face[o+4])+face[o+2]*(face[p+5]-face[o+5]),at=d+lift[g];
                    bool alike=loose||boxes[i].Material>=0&&boxes[i].Material==boxes[j].Material;
                    // Over one that shared the plane or stood behind; either side of one that stood in front, or of one alike.
                    if(d<Step-Slack&&!(alike&&d>-Step+Slack))lo=Math.Max(lo,at+Step);else{band.Add(at);ends.Add(at+Step);}
                }
                double best=lo;ends.Sort();int e=0;
                for(;;)
                {
                    bool clear=true;foreach(double a in band)if(best>a-Step+Slack&&best<a+Step-Slack){clear=false;break;}
                    if(clear)break;while(e<ends.Count&&ends[e]<=best+Slack)e++;if(e>=ends.Count){best=double.NaN;break;}best=ends[e++];
                }
                if(double.IsNaN(best)||best>maxLift+Slack){unresolved++;best=double.IsNaN(best)?lo:best;}
                lift[f]=(float)Math.Min(maxLift,Math.Max(0,best));done[f]=true;
            }
            return lift;
        }
        private const int F=14,Seen=511;
        private static readonly int[] ProbeU={0,-1,1,-1,0,1,-1,0,1},ProbeV={0,0,0,-1,-1,-1,1,1,1};
        // Seen when all nine of face a's probe points (its flat part's centre, edge middles and corners, a millimetre out) lie in
        // box b (j's frames), else 0; the centre first, and out at the first that misses.
        private static int Covered(double[] f,int a,Box b,double[] frames,int j)
        {
            if(!(b.Hx>0&&b.Hy>0&&b.Hz>0))return 0;int o=j*6*F;
            for(int k=0;k<9;k++)
            {
                int su=ProbeU[k],sv=ProbeV[k];
                double px=f[a+3]+f[a]*.001+f[a+6]*su*f[a+12]+f[a+9]*sv*f[a+13]-b.X,py=f[a+4]+f[a+1]*.001+f[a+7]*su*f[a+12]+f[a+10]*sv*f[a+13]-b.Y,pz=f[a+5]+f[a+2]*.001+f[a+8]*su*f[a+12]+f[a+11]*sv*f[a+13]-b.Z;
                // Along the box's axes (its +x, +y and +z faces' normals); a point on its surface counts (a millimetre slit is no view).
                if(!(Math.Abs(px*frames[o+F]+py*frames[o+F+1]+pz*frames[o+F+2])<=b.Hx+Slack&&Math.Abs(px*frames[o+3*F]+py*frames[o+3*F+1]+pz*frames[o+3*F+2])<=b.Hy+Slack&&Math.Abs(px*frames[o+5*F]+py*frames[o+5*F+1]+pz*frames[o+5*F+2])<=b.Hz+Slack))return 0;
            }
            return Seen;
        }
        private static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        // Whether faces a and b (offsets into the frames) face the same way, lie within the search range and overlap on their
        // flat parts by more than MinArea (b's face clipped to a's, in a's plane).
        private static bool Touch(double[] f,int a,int b,double[] poly,double[] next,double range,out double shared,out double width)
        {
            shared=width=0;
            if(f[a]*f[b]+f[a+1]*f[b+1]+f[a+2]*f[b+2]<Facing)return false;
            double gap=f[a]*(f[b+3]-f[a+3])+f[a+1]*(f[b+4]-f[a+4])+f[a+2]*(f[b+5]-f[a+5]),hu=f[a+12],hv=f[a+13],bhu=f[b+12],bhv=f[b+13];
            if(Math.Abs(gap)>range||!(hu>0&&hv>0&&bhu>0&&bhv>0))return false;
            int m=0;for(int k=0;k<4;k++)
            {
                double su=k==0||k==3?-1:1,sv=k<2?-1:1;
                double px=f[b+3]+f[b+6]*su*bhu+f[b+9]*sv*bhv-f[a+3],py=f[b+4]+f[b+7]*su*bhu+f[b+10]*sv*bhv-f[a+4],pz=f[b+5]+f[b+8]*su*bhu+f[b+11]*sv*bhv-f[a+5];
                poly[m++]=px*f[a+6]+py*f[a+7]+pz*f[a+8];poly[m++]=px*f[a+9]+py*f[a+10]+pz*f[a+11];
            }
            // Quick reject: b's corners all beyond one side of a's flat rectangle.
            double x0=double.MaxValue,x1=double.MinValue,y0=double.MaxValue,y1=double.MinValue;for(int k=0;k<4;k++){x0=Math.Min(x0,poly[k*2]);x1=Math.Max(x1,poly[k*2]);y0=Math.Min(y0,poly[k*2+1]);y1=Math.Max(y1,poly[k*2+1]);}
            if(x0>=hu||x1<=-hu||y0>=hv||y1<=-hv)return false;
            int count=4;
            for(int e=0;e<4&&count>0;e++)
            {
                double ex=e==0?1:e==1?-1:0,ey=e==2?1:e==3?-1:0,lim=e<2?hu:hv;int o=0;
                for(int k=0;k<count;k++)
                {
                    double ax=poly[k*2],ay=poly[k*2+1],bx=poly[(k+1)%count*2],by=poly[(k+1)%count*2+1],da=ax*ex+ay*ey-lim,db=bx*ex+by*ey-lim;
                    if(da<=0&&o<15){next[o*2]=ax;next[o*2+1]=ay;o++;}
                    if((da<=0)!=(db<=0)&&o<15){double t=da/(da-db);next[o*2]=ax+(bx-ax)*t;next[o*2+1]=ay+(by-ay)*t;o++;}
                }
                Array.Copy(next,poly,o*2);count=o;
            }
            if(count<3)return false;double s=0;for(int k=0;k<count;k++){int q=(k+1)%count;s+=poly[k*2]*poly[q*2+1]-poly[q*2]*poly[k*2+1];}
            double u0=double.MaxValue,u1=double.MinValue,v0=double.MaxValue,v1=double.MinValue;for(int k=0;k<count;k++){u0=Math.Min(u0,poly[k*2]);u1=Math.Max(u1,poly[k*2]);v0=Math.Min(v0,poly[k*2+1]);v1=Math.Max(v1,poly[k*2+1]);}
            shared=Math.Abs(s)*.5;width=Math.Min(u1-u0,v1-v0);return shared>MinArea;
        }
    }
}
