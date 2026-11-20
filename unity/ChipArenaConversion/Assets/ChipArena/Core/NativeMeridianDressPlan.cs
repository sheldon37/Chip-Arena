using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public static partial class NativeMeridianDress
    {
        /// <summary>The floor of storey k of a building standing on `bottom` (a 4 m ground storey, then 3 m storeys).</summary>
        public static float StoreyFloor(float bottom,int k)=>k<=0?bottom:bottom+GroundStorey+(k-1)*Storey;
        public static float StoreyHeight(int k)=>k<=0?GroundStorey:Storey;
        // The shop trades the bank carries (each its own goods in the window; bible 5.2 and board 3).
        public static readonly string[] Trades={"BAKERY","GROCER","NEWS","CHEMIST","COBBLER","CAFE","BUTCHER","DAIRY","LAUNDRY","TAILOR"};
        public static readonly string[] MarketTrades={"GROCER","BUTCHER","DAIRY","BAKERY","CAFE"};
        /// <summary>Lamp heights over their floor: a swan-neck's wall plate (bible 6.2: 4.0-4.6 m), a span pendant's wire (6.5 m).</summary>
        public const float SwanPlate=4.3f,SpanHeight=6.5f,SwanReach=1.0f,SwanDrop=.37f,PendantDrop=.5f;
        /// <summary>Pool radii (bible 6.2) and the spacing of lamps on the side streets (a third fewer than a route's 12 m).</summary>
        public const float SwanPool=9f,PendantPool=12f,FlushPool=5.5f,SideSpacing=18f;
        /// <summary>The owner's lamp rule (2026-11-19): 1 in 5 dead, 1 in 10 flickering; a route home's protected circuit loses none.</summary>
        public const float DeadShare=.2f,FlickerShare=.1f;

        private sealed class Builder
        {
            public Input In;public Plan P;public Solids S;public ContentBlock[] Blocks;public Func<string,bool> Has;public Dictionary<string,string> Lots;
            public string[] Roles;public bool[] Left;public Dictionary<string,MapRamp> TreadRamp=new Dictionary<string,MapRamp>(StringComparer.Ordinal);
            public uint Seed;public readonly List<Mount> Mounts=new List<Mount>();
            public bool IsDressed(int i)=>Dressed(Roles[i])&&Roles[i]!=RLot;
            // (a lot's roof is a generated surface too: a top two blocks share is drawn by the first, lot or not)
            // (a block the dress leaves, the greybox's or the landmarks', draws its own faces: a face of the dress in its plane is not drawn)
            public bool Undressed(int i)=>!Dressed(Roles[i]);
            public bool DrawsTop(int i)=>Dressed(Roles[i])&&Roles[i]!=RTread&&Roles[i]!=RParapet&&Roles[i]!=RRail&&Roles[i]!=RPost&&Roles[i]!=RWagon;
            public void Add(string key,V3 at,V3 along,V3 normal,float sx,float sy,int block,int face,float u0,float u1,float v0,float v1,float floor,bool hugs,bool paint=false,bool dynamic=false,int layer=-1)
            {
                // (what hangs on a wall without a bay of its own, a lamp, a plate, a clock, a siren, draws over the bays)
                if(layer<0)layer=block<0&&!paint?MountLayers:0;
                var p=new Placement{Key=Pick(key,at,Has),Block=block,Face=face,U0=u0,U1=u1,V0=v0,V1=v1,Floor=floor,Hugs=hugs,Paint=paint,Dynamic=dynamic,Layer=layer};
                var up=Cross(normal,along);
                p.M[0]=along.x*sx;p.M[1]=up.x*sy;p.M[2]=normal.x;p.M[3]=at.x;
                p.M[4]=along.y*sx;p.M[5]=up.y*sy;p.M[6]=normal.y;p.M[7]=at.y;
                p.M[8]=along.z*sx;p.M[9]=up.z*sy;p.M[10]=normal.z;p.M[11]=at.z;
                p.Chunk=P.ChunkOf(at.x,at.z);P.Placements.Add(p);
            }
            /// <summary>A generated rectangle, split along the chunk grid so each piece is combined with its own chunk.</summary>
            public void Quad(V3 o,V3 u,V3 v,int fin,int layer,int block,float floor)
            {
                var n=Cross(u,v).Normalized;
                // the chunk lines crossed along u and along v (only horizontal extents are split)
                int su=Splits(o,u),sv=Splits(o,v);
                for(int a=0;a<su;a++)for(int b=0;b<sv;b++)
                {
                    var uo=u*((float)a/su);var vo=v*((float)b/sv);var q=new Quad{O=o+uo+vo,U=u*(1f/su),V=v*(1f/sv),N=n,Fin=fin,Layer=layer,Block=block,Floor=floor};
                    var c=q.O+q.U*.5f+q.V*.5f;q.Chunk=P.ChunkOf(c.x,c.z);P.Quads.Add(q);
                }
            }
            /// <summary>A flat rectangle wound to face up.</summary>
            public void QuadUp(V3 o,V3 u,V3 v,int fin,int layer,int block,float floor){if(Cross(u,v).y<0){o=o+u;u=u*-1;}Quad(o,u,v,fin,layer,block,floor);}
            private static int Splits(V3 o,V3 e){float l=(float)Math.Sqrt(e.x*e.x+e.z*e.z);return l<=ChunkSize*.75f?1:(int)Math.Ceiling(l/(ChunkSize*.5f));}
        }

        /// <summary>The plan for a city. `has`: whether the bank carries a module key (null: every plain key).</summary>
        public static Plan Build(Input input,Func<string,bool> has=null)
        {
            if(input?.Map?.blocks==null)throw new ArgumentException("No map",nameof(input));
            var map=input.Map;var bounds=map.bounds;
            var P=new Plan{Layout=input.Layout,Run=input.Run,MinX=(float)Math.Floor(bounds.minX)-ChunkSize,MinZ=(float)Math.Floor(bounds.minZ)-ChunkSize};
            P.Nx=(int)Math.Ceiling((bounds.maxX+ChunkSize-P.MinX)/ChunkSize);P.Nz=(int)Math.Ceiling((bounds.maxZ+ChunkSize-P.MinZ)/ChunkSize);
            var b=new Builder{In=input,P=P,Blocks=map.blocks,S=new Solids(map.blocks),Has=has,Lots=Lots(input.Layout),Seed=Mix(input.Layout,0x6a09e667u)};
            foreach(var r in map.ramps??Array.Empty<MapRamp>())foreach(var id in r.blocks)b.TreadRamp[id]=r;
            b.Roles=new string[map.blocks.Length];
            // the blocks the landmarks always draw in place of (the exits' host lots, the court, the halls) are theirs: no module,
            // surface or lamp here, and the landmarks hide them while their bank draws
            var left=NativeMeridianLandmarks.FixedCovers(input.Layout,map);b.Left=new bool[map.blocks.Length];
            for(int i=0;i<map.blocks.Length;i++)
            {
                var id=map.blocks[i].id;b.Left[i]=id!=null&&left.Contains(id);b.Roles[i]=b.Left[i]?RLandmark:RoleOf(id,b.Lots);
                if(Dressed(b.Roles[i]))P.Covers.Add(id);else if(b.Left[i])P.Left.Add(id);
            }
            for(int i=0;i<map.blocks.Length;i++)
            {
                var role=b.Roles[i];if(!Dressed(role))continue;
                if(role==RLot)DressLot(b,i);
                else DressBox(b,i,role);
            }
            foreach(var r in map.ramps??Array.Empty<MapRamp>())if(r.kind=="ramp")DressSlope(b,r);
            Lamps(b);
            Marks(b);
            return P;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Exposure: which cells of a face can be seen (the point just outside them is in no block)
        // ------------------------------------------------------------------------------------------------------------------
        /// <summary>The face's grid lines along its two edges (0..1): every neighbouring box's edges that cross it, then cuts so no cell
        /// is wider than `cell` m. A cell between them lies wholly in or wholly out of each axis-aligned neighbour, so its centre decides
        /// it exactly: no part of a face is drawn twice (a coplanar neighbour's) or left bare (a hiding neighbour's).</summary>
        private static void Lines(Builder b,int bi,int f,float cell,out float[] us,out float[] vs,out V3 o,out V3 u,out V3 v)
        {
            FaceFrame(b.S.B,bi*6,f,out o,out u,out v);float lu=u.Length,lv=v.Length;var n=NormalOf(f);var B=b.S.B;
            var lu0=new List<float>{0,1};var lv0=new List<float>{0,1};
            var c0=o-n*.1f;var c1=o+u+v+n*.1f;
            foreach(int j in b.S.Near(Math.Min(c0.x,c1.x),Math.Max(c0.x,c1.x),Math.Min(c0.z,c1.z),Math.Max(c0.z,c1.z)))
            {
                if(j==bi)continue;int q=j*6;
                // the neighbour must reach the face's plane (within 0.1 m either side)
                float plane=V3.Dot(o,n),lo=n.x!=0?B[q]:n.y!=0?B[q+2]:B[q+4],hi=n.x!=0?B[q+1]:n.y!=0?B[q+3]:B[q+5];
                if(n.x+n.y+n.z>0){if(lo*(n.x+n.y+n.z)>plane+.1f||hi<plane-.1f)continue;}else{if(-hi>plane+.1f||-lo<plane-.1f)continue;}
                foreach(var (axis,list,len,origin) in new[]{(u,lu0,lu,o),(v,lv0,lv,o)})
                {
                    var d=axis/len;int ax=Math.Abs(d.x)>.5f?0:Math.Abs(d.y)>.5f?1:2;float sgn=ax==0?d.x:ax==1?d.y:d.z;float org=ax==0?origin.x:ax==1?origin.y:origin.z;
                    foreach(float edge in new[]{B[q+ax*2],B[q+ax*2+1]}){float t=(edge-org)*sgn/len;if(t>.0005f&&t<.9995f)list.Add(t);}
                }
            }
            us=Cut(lu0,lu,cell);vs=Cut(lv0,lv,cell);
        }
        private static float[] Cut(List<float> lines,float len,float cell)
        {
            lines.Sort();var r=new List<float>{0};
            for(int i=1;i<lines.Count;i++)
            {
                float a=r[r.Count-1],c=lines[i];if((c-a)*len<.02f)continue;int k=Math.Max(1,(int)Math.Ceiling((c-a)*len/cell));
                for(int s=1;s<=k;s++)r.Add(a+(c-a)*s/k);
            }
            if(r[r.Count-1]<1)r[r.Count-1]=1;return r.ToArray();
        }
        private static bool[] Exposed(Builder b,int bi,int f,float[] us,float[] vs,V3 o,V3 u,V3 v)
        {
            var n=NormalOf(f);int nu=us.Length-1,nv=vs.Length-1;var e=new bool[nu*nv];
            for(int j=0;j<nv;j++)for(int i=0;i<nu;i++)
            {
                var p=o+u*((us[i]+us[i+1])*.5f)+v*((vs[j]+vs[j+1])*.5f);
                e[i+j*nu]=!b.S.Inside(p.x+n.x*.05f,p.y+n.y*.05f,p.z+n.z*.05f,bi)&&!b.S.Shared(p.x,p.y,p.z,f,bi,f==2?b.DrawsTop:b.IsDressed,b.Undressed);
            }
            return e;
        }
        private static bool OutOfBounds(Builder b,int bi,int f)
        {
            var mb=b.In.Map.bounds;var B=b.S.B;int o=bi*6;const float e=.05f;
            return f==0&&B[o+1]>=mb.maxX-e||f==1&&B[o]<=mb.minX+e||f==4&&B[o+5]>=mb.maxZ-e||f==5&&B[o+4]<=mb.minZ+e;
        }
        /// <summary>Greedy rectangles over a grid's set cells: (i0, j0, i1, j1) exclusive ends.</summary>
        private static List<(int,int,int,int)> Rects(bool[] e,int nu,int nv)
        {
            var used=new bool[e.Length];var r=new List<(int,int,int,int)>();
            for(int j=0;j<nv;j++)for(int i=0;i<nu;i++)
            {
                if(!e[i+j*nu]||used[i+j*nu])continue;int i1=i;while(i1<nu&&e[i1+j*nu]&&!used[i1+j*nu])i1++;
                int j1=j+1;while(j1<nv){bool ok=true;for(int k=i;k<i1;k++)if(!e[k+j1*nu]||used[k+j1*nu]){ok=false;break;}if(!ok)break;j1++;}
                for(int jj=j;jj<j1;jj++)for(int k=i;k<i1;k++)used[k+jj*nu]=true;r.Add((i,j,i1,j1));
            }
            return r;
        }
        /// <summary>Draws every visible part of a face with one finish (and a band finish over its top `band` m on a side face).</summary>
        private static void Surface(Builder b,int bi,int f,int fin,int layer,float cell,int bandFin=-1,float band=0)
        {
            FaceFrame(b.S.B,bi*6,f,out var o0,out var u0,out var v0);float lu=u0.Length,lv=v0.Length;if(lu<.01f||lv<.01f)return;
            // a face on the map's bounds turned out of it is never seen (beyond the Cordon): it is left out
            if(OutOfBounds(b,bi,f))return;
            Lines(b,bi,f,cell,out var us,out var vs,out var o,out var u,out var v);int nu=us.Length-1,nv=vs.Length-1;
            var e=Exposed(b,bi,f,us,vs,o,u,v);float floor=b.S.B[bi*6+2];
            if(f==2||f==3)floor=b.S.B[bi*6+(f==2?3:2)];
            foreach(var (i0,j0,i1,j1) in Rects(e,nu,nv))
            {
                var ro=o+u*us[i0]+v*vs[j0];var ru=u*(us[i1]-us[i0]);var rv=v*(vs[j1]-vs[j0]);
                if(bandFin>=0&&f!=2&&f!=3&&band>0)
                {
                    float top=vs[j1]*lv,bot=vs[j0]*lv,cut=lv-band;
                    if(top>cut+.01f&&bot<cut-.01f){var vb=v*((cut-bot)/lv);b.Quad(ro,ru,vb,fin,layer,bi,floor);b.Quad(ro+vb,ru,rv-vb,bandFin,layer,bi,floor);continue;}
                    if(bot>=cut-.01f){b.Quad(ro,ru,rv,bandFin,layer,bi,floor);continue;}
                }
                b.Quad(ro,ru,rv,fin,layer,bi,floor);
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Every role but the lots: its faces in its finishes (NativeMeridianSurfaces), and the modules that fill it
        // ------------------------------------------------------------------------------------------------------------------
        private static void DressBox(Builder b,int bi,string role)
        {
            var id=b.Blocks[bi].id;int o=bi*6;float[] B=b.S.B;float h=B[o+3]-B[o+2];
            switch(role)
            {
                case RGround:
                    Surface(b,bi,2,NativeMeridianSurfaces.Asphalt,0,4f);
                    for(int f=0;f<6;f++)if(f!=2&&f!=3)Surface(b,bi,f,NativeMeridianSurfaces.BrickBlue,0,1.5f,NativeMeridianSurfaces.Coping,.45f);
                    break;
                case RTread:
                {
                    b.TreadRamp.TryGetValue(id,out var r);bool fire=id.StartsWith("fire-escape-",StringComparison.Ordinal);bool stair=r==null||r.kind=="stair";
                    if(!stair){for(int f=0;f<6;f++)if(f!=2&&f!=3&&!Facing(r,f))Surface(b,bi,f,NativeMeridianSurfaces.BrickBlue,0,1.5f,NativeMeridianSurfaces.Coping,.3f);break;}
                    int treadFin=fire?NativeMeridianSurfaces.IronGrate:NativeMeridianSurfaces.Granite,sideFin=fire?NativeMeridianSurfaces.IronBlack:NativeMeridianSurfaces.Stone;
                    for(int f=0;f<6;f++)Surface(b,bi,f,f==2?treadFin:r!=null&&Facing(r,f)?treadFin:sideFin,0,f==2?1f:1.5f);
                    break;
                }
                case RLanding:for(int f=0;f<6;f++)Surface(b,bi,f,f==2?NativeMeridianSurfaces.IronGrate:NativeMeridianSurfaces.IronBlack,0,1.5f);break;
                case RRail:
                case RParapet:
                {
                    bool iron=role==RRail||id.StartsWith("skybridge-",StringComparison.Ordinal)||id.Contains("footbridge")||id.StartsWith("w-deck-",StringComparison.Ordinal)||id.StartsWith("signal-",StringComparison.Ordinal);
                    // an iron parapet is its module (posts, rails, panels) filling the box; a stone one the balustrade module
                    if(Fill(b,bi,iron?"parapet.iron":"parapet.stone",2.5f,1.1f,.5f,2.5f))break;
                    for(int f=0;f<6;f++)Surface(b,bi,f,iron?NativeMeridianSurfaces.IronGreen:NativeMeridianSurfaces.Stone,0,1.5f);
                    break;
                }
                case RBridge:
                {
                    bool iron=!id.Contains("causeway")&&!id.StartsWith("navigation-road",StringComparison.Ordinal)&&!id.StartsWith("mill-",StringComparison.Ordinal);
                    Surface(b,bi,2,iron?NativeMeridianSurfaces.Planks:NativeMeridianSurfaces.Setts,0,2f);
                    for(int f=0;f<6;f++)if(f!=2)Surface(b,bi,f,iron?NativeMeridianSurfaces.IronGreen:NativeMeridianSurfaces.Stone,0,2f);
                    break;
                }
                case RViaduct:
                    Surface(b,bi,2,NativeMeridianSurfaces.Ballast,0,4f);
                    for(int f=0;f<6;f++)if(f!=2)Surface(b,bi,f,f==3?NativeMeridianSurfaces.BrickBlue:NativeMeridianSurfaces.BrickBlue,0,2f,NativeMeridianSurfaces.Coping,f==3?0:.35f);
                    Arches(b,bi);
                    break;
                case RPier:for(int f=0;f<6;f++)if(f!=3)Surface(b,bi,f,NativeMeridianSurfaces.BrickBlue,0,1.5f,NativeMeridianSurfaces.Coping,f==2?0:.5f);break;
                case RPost:if(!Fill(b,bi,"post.iron",1,6,1,0))for(int f=0;f<6;f++)Surface(b,bi,f,NativeMeridianSurfaces.IronGreen,0,1.5f);break;
                case RArcadeRoof:
                case RLid:
                {
                    bool glass=role==RArcadeRoof||id.StartsWith("n-lid-",StringComparison.Ordinal)||id=="rotunda-dome";
                    Surface(b,bi,2,glass?NativeMeridianSurfaces.Lead:NativeMeridianSurfaces.TarFelt,0,3f);
                    Surface(b,bi,3,glass?NativeMeridianSurfaces.GlassRoof:NativeMeridianSurfaces.Soffit,0,2.5f);
                    for(int f=0;f<6;f++)if(f!=2&&f!=3)Surface(b,bi,f,NativeMeridianSurfaces.IronGreen,0,2f);
                    break;
                }
                case RWagon:if(!Fill(b,bi,"wagon.van",12,3.2f,3,12))for(int f=0;f<6;f++)Surface(b,bi,f,f==2?NativeMeridianSurfaces.Lead:NativeMeridianSurfaces.WagonRed,0,1.5f);break;
                case RWall:
                case RShed:
                    Surface(b,bi,2,NativeMeridianSurfaces.TarFelt,0,4f);
                    for(int f=0;f<6;f++)if(f!=2&&f!=3)Surface(b,bi,f,NativeMeridianSurfaces.BrickBuff,0,1.5f,NativeMeridianSurfaces.Coping,.4f);
                    Surface(b,bi,3,NativeMeridianSurfaces.Soffit,0,3f);
                    break;
                case RCordon:
                case RTower:
                    for(int f=0;f<6;f++)if(f!=3)Surface(b,bi,f,f==2?NativeMeridianSurfaces.ConcreteDark:NativeMeridianSurfaces.Concrete,0,3f,NativeMeridianSurfaces.ConcreteDark,f==2?0:.6f);
                    break;
                case RTunnel:for(int f=0;f<6;f++)Surface(b,bi,f,f==2?NativeMeridianSurfaces.Asphalt:NativeMeridianSurfaces.BrickBlue,0,2f);break;
            }
        }
        private static bool Facing(MapRamp r,int f)
        {
            // the riser faces of a flight's treads face its low end
            var d=r.low-r.high;if(Math.Abs(d.x)>Math.Abs(d.z))return d.x>0?f==0:f==1;return d.z>0?f==4:f==5;
        }
        /// <summary>A module that fills a block's box (a parapet, a post, a wagon), its long side along the module's x: authored at
        /// (ax, ay, az) m with its foot's centre at its origin, stretched to the box; tiled along the box every about `tile` m (0:
        /// one piece). False without the bank's module (the box's faces are drawn plain instead).</summary>
        private static bool Fill(Builder b,int bi,string key,float ax,float ay,float az,float tile)
        {
            if(b.Has!=null&&!b.Has(key))return false;int o=bi*6;var B=b.S.B;float sx=B[o+1]-B[o],sy=B[o+3]-B[o+2],sz=B[o+5]-B[o+4];
            bool alongX=sx>=sz;var along=alongX?new V3(1,0,0):new V3(0,0,-1);var normal=alongX?new V3(0,0,1):new V3(1,0,0);
            float L=alongX?sx:sz,D=alongX?sz:sx;int n=tile>0?Math.Max(1,(int)Math.Round(L/tile)):1;float piece=L/n;
            var start=alongX?new V3(B[o],B[o+2],(B[o+4]+B[o+5])*.5f):new V3((B[o]+B[o+1])*.5f,B[o+2],B[o+5]);
            for(int k=0;k<n;k++)
            {
                var at=start+along*(piece*(k+.5f));
                var p=new Placement{Key=Pick(key,at,b.Has),Block=bi,Face=-1,U0=piece*k,U1=piece*(k+1),V0=B[o+2],V1=B[o+3],Floor=B[o+2],Hugs=true,Layer=k&1};
                float kx=piece/ax,ky=sy/ay,kz=D/az;
                p.M[0]=along.x*kx;p.M[1]=0;p.M[2]=normal.x*kz;p.M[3]=at.x;p.M[4]=0;p.M[5]=ky;p.M[6]=0;p.M[7]=at.y;p.M[8]=along.z*kx;p.M[9]=0;p.M[10]=normal.z*kz;p.M[11]=at.z;
                p.Chunk=b.P.ChunkOf(at.x,at.z);b.P.Placements.Add(p);
            }
            return true;
        }
        /// <summary>A sloped street (MapRamp 'ramp'): its surface on the wedge the pilots walk (setts), drawn in place of its treads' tops.</summary>
        private static void DressSlope(Builder b,MapRamp r)
        {
            var d=r.high-r.low;float run=(float)Math.Sqrt(d.x*d.x+d.z*d.z);if(run<.1f)return;var dir=new V3(d.x/run,0,d.z/run);
            var across=new V3(dir.z,0,-dir.x);int pieces=Math.Max(1,(int)Math.Ceiling(run/8f));
            for(int k=0;k<pieces;k++)
            {
                var a=r.low+d*((float)k/pieces);var e=d*(1f/pieces);var o=a-across*(r.width*.5f);var u=across*r.width;
                // wound so the surface faces up: u (across) x v (up the slope)
                var n=Cross(u,e);if(n.y<0){o=a+across*(r.width*.5f);u=across*-r.width;}
                b.Quad(o,u,e,NativeMeridianSurfaces.Setts,0,-1,Math.Min(a.y,a.y+e.y));
            }
        }
        /// <summary>The Stride's arches: between each pair of piers under the viaduct deck, an arch's spandrels and soffit hung from the
        /// deck (overhead art, above 3.1 m over the floor below).</summary>
        private static void Arches(Builder b,int deck)
        {
            if(b.Has!=null&&!b.Has("viaduct.arch"))return;var B=b.S.B;int o=deck*6;
            var piers=new List<int>();for(int i=0;i<b.Blocks.Length;i++)if(b.Roles[i]==RPier&&b.Blocks[i].id.StartsWith("viaduct-pier",StringComparison.Ordinal))piers.Add(i);
            bool alongX=B[o+1]-B[o]>=B[o+5]-B[o+4];
            piers.Sort((p,q)=>(alongX?B[p*6]:B[p*6+4]).CompareTo(alongX?B[q*6]:B[q*6+4]));
            for(int k=0;k+1<piers.Count;k++)
            {
                int p=piers[k]*6,q=piers[k+1]*6;float a=alongX?B[p+1]:B[p+5],c=alongX?B[q]:B[q+4];float span=c-a;if(span<2||span>14)continue;
                float mid=(a+c)*.5f;float floor=b.S.Floor(alongX?mid:(B[o]+B[o+1])*.5f,alongX?(B[o+4]+B[o+5])*.5f:mid,B[o+2]-.1f);
                if(B[o+2]-floor<6.5f)continue;
                var at=alongX?new V3(mid,B[o+2],B[o+5]):new V3(B[o+1],B[o+2],mid);var along=alongX?new V3(1,0,0):new V3(0,0,-1);var normal=alongX?new V3(0,0,1):new V3(1,0,0);
                float depth=alongX?B[o+5]-B[o+4]:B[o+1]-B[o];
                var pl=new Placement{Key=Pick("viaduct.arch",at,b.Has),Block=deck,Face=-1,U0=a,U1=c,V0=B[o+2]-4,V1=B[o+2],Floor=floor};
                // authored for a 10 m span under a 12 m deep deck (its face at z 0, its depth to z -12), hung from the deck's
                // underside, its face on the deck's side
                pl.M[0]=along.x*span/10f;pl.M[1]=0;pl.M[2]=normal.x*depth/12f;pl.M[3]=at.x;pl.M[4]=0;pl.M[5]=1;pl.M[6]=0;pl.M[7]=at.y;pl.M[8]=along.z*span/10f;pl.M[9]=0;pl.M[10]=normal.z*depth/12f;pl.M[11]=at.z;
                pl.Chunk=b.P.ChunkOf(at.x,at.z);b.P.Placements.Add(pl);
            }
        }
    }
}
