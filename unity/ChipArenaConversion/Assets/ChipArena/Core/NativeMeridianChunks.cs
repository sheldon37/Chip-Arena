using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The generated surfaces' finishes (the MERIDIAN art bible's finish table, section 4.2, in ChipArena/StageLit's vertex terms:
    /// sRGB albedo with its grime and contact shade, smoothness and metallic, the fine surface and its parameter), and their
    /// tessellation into a chunk: a rectangle cut into cells (the vertex colours' resolution), each corner coloured by the world's
    /// own mottle, the grime rising from the floor and the shade at a wall's foot. Pure and deterministic.
    /// </summary>
    public static class NativeMeridianSurfaces
    {
        // stagekit's DETAIL_* (Resources/NativeStageLit.shader)
        public const int DNone=0,DFlag=2,DWood=5,DIron=6,DPlaster=10,DBrick=21;
        public struct Fin{public int Rgb,Detail;public float Param,Smooth,Metal,Dirt;}
        public const int Asphalt=0,Paving=1,Setts=2,Granite=3,Kerb=4,Stone=5,StoneCivic=6,Coping=7,BrickBlue=8,BrickStock=9,BrickBuff=10,Render=11,TarFelt=12,Lead=13,
            IronBlack=14,IronGreen=15,IronGrate=16,Planks=17,Ballast=18,Soffit=19,GlassRoof=20,WagonRed=21,Concrete=22,ConcreteDark=23,Count=24;
        public static readonly Fin[] Fins={
            new Fin{Rgb=0x2E3038,Detail=DNone,Smooth=.5f,Dirt=.5f},        // asphalt (wet)
            new Fin{Rgb=0x6E6A64,Detail=DFlag,Param=.62f,Smooth=.4f,Dirt=.9f},  // paving slabs
            new Fin{Rgb=0x5A5652,Detail=DFlag,Param=.22f,Smooth=.45f,Dirt=.9f}, // setts
            new Fin{Rgb=0x7A7670,Detail=DFlag,Param=.5f,Smooth=.35f,Dirt=.8f},  // granite steps
            new Fin{Rgb=0x8A857C,Detail=DFlag,Param=.5f,Smooth=.35f,Dirt=.6f},  // the kerb's lit top edge
            new Fin{Rgb=0xCFC5AF,Detail=DFlag,Param=.6f,Smooth=.2f,Dirt=1f},    // stone-dress
            new Fin{Rgb=0xD6CDB8,Detail=DFlag,Param=1f,Smooth=.22f,Dirt=.8f},   // civic ashlar
            new Fin{Rgb=0xBDB39C,Detail=DFlag,Param=.6f,Smooth=.25f,Dirt=1f},   // copings
            new Fin{Rgb=0x4E4450,Detail=DBrick,Param=1f,Smooth=.32f,Dirt=.8f},  // engineering brick
            new Fin{Rgb=0x8C4B36,Detail=DBrick,Param=0f,Smooth=.18f,Dirt=1f},   // stock brick
            new Fin{Rgb=0xB48A5E,Detail=DBrick,Param=1.05f,Smooth=.15f,Dirt=1.3f}, // buff brick
            new Fin{Rgb=0xD8CAA8,Detail=DPlaster,Smooth=.12f,Dirt=1.1f},        // cream render
            new Fin{Rgb=0x302E34,Detail=DPlaster,Smooth=.2f,Dirt=.5f},          // tar felt
            new Fin{Rgb=0x6E767C,Detail=DNone,Smooth=.45f,Metal=.6f,Dirt=.5f},  // lead
            new Fin{Rgb=0x26262C,Detail=DNone,Smooth=.45f,Metal=.3f,Dirt=.7f},  // black iron
            new Fin{Rgb=0x24463A,Detail=DNone,Smooth=.4f,Metal=.25f,Dirt=.8f},  // municipal green iron
            new Fin{Rgb=0x2C2C32,Detail=DNone,Smooth=.4f,Metal=.35f,Dirt=.7f},  // iron grating
            new Fin{Rgb=0x8A6E52,Detail=DWood,Param=0f,Smooth=.15f,Dirt=1f},    // deck planks
            new Fin{Rgb=0x4A4844,Detail=DFlag,Param=.18f,Smooth=.2f,Dirt=.8f},  // ballast
            new Fin{Rgb=0x3A3A3E,Detail=DPlaster,Smooth=.15f,Dirt=.6f},         // soffits
            new Fin{Rgb=0x1A2230,Detail=DNone,Smooth=.9f,Dirt=.2f},             // dark glazing (a glass roof from below)
            new Fin{Rgb=0x6A2626,Detail=DWood,Param=1f,Smooth=.45f,Dirt=.8f},   // a wagon's oxide red boards
            new Fin{Rgb=0x8A8882,Detail=DPlaster,Smooth=.1f,Dirt=1.4f},         // concrete
            new Fin{Rgb=0x6E6D69,Detail=DPlaster,Smooth=.1f,Dirt=1.2f},         // dark concrete
        };
        /// <summary>The cell size (m) of a generated surface at a tier (0 LOD0, 2 LOD1).</summary>
        public static float Cell(int tier,bool flat)=>tier==0?(flat?2.5f:1.5f):(flat?8f:6f);

        private static float Hash3(int x,int y,int z,int seed){uint h=(uint)(x*73856093)^(uint)(y*19349663)^(uint)(z*83492791)^(uint)seed*2654435761u;h^=h>>13;h*=0x5bd1e995u;h^=h>>15;return (h&0xffff)/65535f;}
        /// <summary>Value noise in 0..1 at a world point (the mottle that keeps a surface painted, not printed).</summary>
        public static float Noise(float x,float y,float z,float freq,int seed)
        {
            x*=freq;y*=freq;z*=freq;int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y),iz=(int)Math.Floor(z);float fx=x-ix,fy=y-iy,fz=z-iz;
            fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);fz=fz*fz*(3-2*fz);float v=0;
            for(int c=0;c<8;c++){int dx=c&1,dy=(c>>1)&1,dz=(c>>2)&1;float w=(dx==1?fx:1-fx)*(dy==1?fy:1-fy)*(dz==1?fz:1-fz);v+=w*Hash3(ix+dx,iy+dy,iz+dz,seed);}
            return v;
        }
        /// <summary>A corner's colour: the finish's albedo, mottled, the grime rising from the floor, wet upward faces a touch darker;
        /// alpha the contact shade at a wall's foot (1 open).</summary>
        public static void Colour(in Fin f,V3 p,V3 n,float floor,byte[] into,int at)
        {
            float r=(f.Rgb>>16&255)/255f,g=(f.Rgb>>8&255)/255f,b=(f.Rgb&255)/255f;
            float mott=(Noise(p.x,p.y,p.z,.35f,1)-.5f)*.26f+(Noise(p.x,p.y,p.z,2.2f,2)-.5f)*.1f;float k=1+mott*f.Dirt;
            float above=p.y-floor;float grime=0,ao=1;
            if(Math.Abs(n.y)<.5f){float t=Math.Max(0,Math.Min(1,above/1.5f));grime=(1-t*t*(3-2*t))*.3f*f.Dirt;float s=Math.Max(0,Math.Min(1,above/.35f));ao=.62f+.38f*s*s*(3-2*s);}
            else if(n.y>.5f)k*=.94f;
            r=r*k*(1-grime)+.16f*grime;g=g*k*(1-grime)+.13f*grime;b=b*k*(1-grime)+.10f*grime;
            into[at]=(byte)Math.Round(Math.Max(0,Math.Min(1,r))*255);into[at+1]=(byte)Math.Round(Math.Max(0,Math.Min(1,g))*255);into[at+2]=(byte)Math.Round(Math.Max(0,Math.Min(1,b))*255);into[at+3]=(byte)Math.Round(ao*255);
        }
    }

    /// <summary>
    /// A chunk's mesh data at one tier, as the combine writes it (Unity space): positions, normals, uv0 (smoothness, metallic), uv1
    /// (fine surface + layer / 32, its parameter or glow gain), sRGB colours with the occlusion in alpha, and two index lists: the
    /// solid sub-mesh and the glow sub-mesh. Reused between chunks (Clear), so building them allocates only as they grow.
    /// </summary>
    public sealed class NativeMeridianMesh
    {
        public float[] P=new float[3*4096],N=new float[3*4096],Uv0=new float[2*4096],Uv1=new float[2*4096];public byte[] C=new byte[4*4096];
        public int Vertices;public readonly List<int> Solid=new List<int>(),Glow=new List<int>();
        public float MinX,MinY,MinZ,MaxX,MaxY,MaxZ;
        public int Triangles=>(Solid.Count+Glow.Count)/3;
        public void Clear(){Vertices=0;Solid.Clear();Glow.Clear();MinX=MinY=MinZ=float.MaxValue;MaxX=MaxY=MaxZ=float.MinValue;}
        public NativeMeridianMesh(){Clear();}
        public int Reserve(int n)
        {
            int need=Vertices+n;if(need*3>P.Length){int cap=Math.Max(need,P.Length/3*2);Array.Resize(ref P,cap*3);Array.Resize(ref N,cap*3);Array.Resize(ref Uv0,cap*2);Array.Resize(ref Uv1,cap*2);Array.Resize(ref C,cap*4);}
            int at=Vertices;Vertices=need;return at;
        }
        public void Bound(float x,float y,float z){if(x<MinX)MinX=x;if(y<MinY)MinY=y;if(z<MinZ)MinZ=z;if(x>MaxX)MaxX=x;if(y>MaxY)MaxY=y;if(z>MaxZ)MaxZ=z;}
    }

    public static partial class NativeMeridianDress
    {
        /// <summary>The bank's modules: key to its prop index at each tier (0 LOD0, 1 shares LOD0, 2 LOD1; -1 where missing, the
        /// nearest present tier standing in), from the props named module/&lt;key&gt;/&lt;tier&gt;.</summary>
        public static Dictionary<string,int[]> Modules(NativePackBank.Bank bank)
        {
            var d=new Dictionary<string,int[]>(StringComparer.Ordinal);if(bank==null)return d;
            for(int i=0;i<bank.Props.Count;i++)
            {
                var p=bank.Props[i];if(p.Group!="module"||!NativeStageArt.Split(p.Name,out var kind,out var id,out int tier)||kind!="module")continue;
                if(!d.TryGetValue(id,out var t)){t=new[]{-1,-1,-1};d[id]=t;}t[tier]=i;
            }
            foreach(var t in d.Values)for(int i=0;i<3;i++)if(t[i]<0)for(int k=1;k<3&&t[i]<0;k++){if(i-k>=0&&t[i-k]>=0)t[i]=t[i-k];else if(i+k<3&&t[i+k]>=0)t[i]=t[i+k];}
            return d;
        }
        /// <summary>The city bank with another bank's modules (the bleed's themed variants, module/&lt;key&gt;@&lt;theme&gt;) added: a new
        /// bank sharing the first's meshes and props, the second's module props and their meshes appended (indices moved past the
        /// first's). The banks themselves are untouched; without the second, the first.</summary>
        public static NativePackBank.Bank WithVariants(NativePackBank.Bank city,NativePackBank.Bank variants)
        {
            if(city==null||variants==null)return city;
            var b=new NativePackBank.Bank();b.Textures.AddRange(city.Textures);b.Materials.AddRange(city.Materials);b.Meshes.AddRange(city.Meshes);
            for(int i=0;i<city.Props.Count;i++){b.Props.Add(city.Props[i]);b.PropIndex[city.Props[i].Name]=i;}
            var moved=new Dictionary<int,int>();
            foreach(var p in variants.Props)
            {
                if(p.Group!="module"||b.PropIndex.ContainsKey(p.Name))continue;var parts=new NativePackBank.Part[p.Parts.Length];
                for(int k=0;k<parts.Length;k++)
                {
                    int m=p.Parts[k].Mesh;if(!moved.TryGetValue(m,out int at)){at=b.Meshes.Count;b.Meshes.Add(variants.Meshes[m]);moved[m]=at;}
                    parts[k]=new NativePackBank.Part{Mesh=at,Place=p.Parts[k].Place};
                }
                b.PropIndex[p.Name]=b.Props.Count;b.Props.Add(new NativePackBank.Prop{Name=p.Name,Group=p.Group,Min=p.Min,Max=p.Max,Parts=parts});
            }
            return b;
        }
        /// <summary>Combines a chunk at a tier (0 LOD0, 2 LOD1): its placed modules (all but the dynamic ones) and its generated
        /// surfaces, into `into` (cleared first). Pure; reads the bank's arrays only.</summary>
        public static void Combine(Plan plan,int chunk,int tier,NativePackBank.Bank bank,Dictionary<string,int[]> modules,List<int> placements,List<int> quads,NativeMeridianMesh into)
        {
            into.Clear();
            if(bank!=null&&placements!=null)foreach(int pi in placements)
            {
                var pl=plan.Placements[pi];if(pl.Dynamic||!modules.TryGetValue(pl.Key,out var props))continue;AddModule(bank,props[tier],pl.M,into,(pl.Paint?PaintLayers:0)+pl.Layer);
            }
            if(quads!=null)foreach(int qi in quads)AddQuad(plan.Quads[qi],tier,into);
        }
        /// <summary>The layers a piece of paint (road marks, puddles, ghost signs, plates, hatches) draws up over the surface it lies
        /// on (ChipArena/StageLit's layers: nearer the eye along its ray, so a mark 6 mm over the street never fights it).</summary>
        public const int PaintLayers=3;
        /// <summary>One module prop placed by 3 x 4 rows into the mesh: positions through the place, normals through its inverse
        /// transpose (the facades' stretch), every other channel as banked.</summary>
        public static void AddModule(NativePackBank.Bank bank,int prop,float[] M,NativeMeridianMesh into,int layers=0)
        {
            float lift=layers/32f;
            if(prop<0||prop>=bank.Props.Count)return;var pr=bank.Props[prop];
            foreach(var part in pr.Parts)
            {
                var m=bank.Meshes[part.Mesh];if(m.Positions==null)continue;var q=part.Place;
                // the composite: M (3 x 4 rows) times the part's place (System.Numerics, row vectors: p' = p * Place)
                float a00=M[0]*q.M11+M[1]*q.M12+M[2]*q.M13,a01=M[0]*q.M21+M[1]*q.M22+M[2]*q.M23,a02=M[0]*q.M31+M[1]*q.M32+M[2]*q.M33,a03=M[0]*q.M41+M[1]*q.M42+M[2]*q.M43+M[3];
                float a10=M[4]*q.M11+M[5]*q.M12+M[6]*q.M13,a11=M[4]*q.M21+M[5]*q.M22+M[6]*q.M23,a12=M[4]*q.M31+M[5]*q.M32+M[6]*q.M33,a13=M[4]*q.M41+M[5]*q.M42+M[6]*q.M43+M[7];
                float a20=M[8]*q.M11+M[9]*q.M12+M[10]*q.M13,a21=M[8]*q.M21+M[9]*q.M22+M[10]*q.M23,a22=M[8]*q.M31+M[9]*q.M32+M[10]*q.M33,a23=M[8]*q.M41+M[9]*q.M42+M[10]*q.M43+M[11];
                // the cofactors (the inverse transpose times the determinant: a stretch's normals, never mirrored here)
                float c00=a11*a22-a12*a21,c01=a12*a20-a10*a22,c02=a10*a21-a11*a20,c10=a02*a21-a01*a22,c11=a00*a22-a02*a20,c12=a01*a20-a00*a21,c20=a01*a12-a02*a11,c21=a02*a10-a00*a12,c22=a00*a11-a01*a10;
                int n=m.Positions.Length,at=into.Reserve(n);bool uv1=m.Uv1!=null,col=m.Colors!=null;
                for(int i=0;i<n;i++)
                {
                    var p=m.Positions[i];float x=a00*p.X+a01*p.Y+a02*p.Z+a03,y=a10*p.X+a11*p.Y+a12*p.Z+a13,z=a20*p.X+a21*p.Y+a22*p.Z+a23;int v=at+i;
                    into.P[v*3]=x;into.P[v*3+1]=y;into.P[v*3+2]=z;into.Bound(x,y,z);
                    var nn=m.Normals[i];float nx=c00*nn.X+c01*nn.Y+c02*nn.Z,ny=c10*nn.X+c11*nn.Y+c12*nn.Z,nz=c20*nn.X+c21*nn.Y+c22*nn.Z;float l=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);if(l<1e-8f)l=1;
                    into.N[v*3]=nx/l;into.N[v*3+1]=ny/l;into.N[v*3+2]=nz/l;
                    into.Uv0[v*2]=m.Uv0[i].X;into.Uv0[v*2+1]=m.Uv0[i].Y;
                    into.Uv1[v*2]=(uv1?m.Uv1[i].X:0)+lift;into.Uv1[v*2+1]=uv1?m.Uv1[i].Y:0;
                    if(col){into.C[v*4]=m.Colors[i*4];into.C[v*4+1]=m.Colors[i*4+1];into.C[v*4+2]=m.Colors[i*4+2];into.C[v*4+3]=m.Colors[i*4+3];}
                    else{into.C[v*4]=into.C[v*4+1]=into.C[v*4+2]=into.C[v*4+3]=255;}
                }
                for(int s=0;s<m.Submeshes.Length;s++){var list=s==0?into.Solid:into.Glow;foreach(int ix in m.Submeshes[s].Indices)list.Add(at+ix);}
            }
        }
        /// <summary>A generated rectangle into the mesh, cut into cells at the tier's size.</summary>
        public static void AddQuad(in Quad q,int tier,NativeMeridianMesh into)
        {
            var f=NativeMeridianSurfaces.Fins[q.Fin];float lu=q.U.Length,lv=q.V.Length;bool flat=Math.Abs(q.N.y)>.5f;float cell=NativeMeridianSurfaces.Cell(tier,flat);
            int nu=Math.Max(1,Math.Min(48,(int)Math.Ceiling(lu/cell))),nv=Math.Max(1,Math.Min(48,(int)Math.Ceiling(lv/cell)));
            int at=into.Reserve((nu+1)*(nv+1));float kind=f.Detail+q.Layer/32f;
            for(int j=0;j<=nv;j++)for(int i=0;i<=nu;i++)
            {
                var p=q.O+q.U*((float)i/nu)+q.V*((float)j/nv);int v=at+i+j*(nu+1);
                into.P[v*3]=p.x;into.P[v*3+1]=p.y;into.P[v*3+2]=p.z;into.Bound(p.x,p.y,p.z);into.N[v*3]=q.N.x;into.N[v*3+1]=q.N.y;into.N[v*3+2]=q.N.z;
                into.Uv0[v*2]=f.Smooth;into.Uv0[v*2+1]=f.Metal;into.Uv1[v*2]=kind;into.Uv1[v*2+1]=f.Param;NativeMeridianSurfaces.Colour(f,p,q.N,q.Floor,into.C,v*4);
            }
            // the bank's winding: (p1 - p0) x (p2 - p0) out of the face, as u x v is
            for(int j=0;j<nv;j++)for(int i=0;i<nu;i++)
            {
                int a=at+i+j*(nu+1),b=a+1,c=a+nu+2,d=a+nu+1;into.Solid.Add(a);into.Solid.Add(b);into.Solid.Add(c);into.Solid.Add(a);into.Solid.Add(c);into.Solid.Add(d);
            }
        }
        /// <summary>The plan's placements and surfaces by chunk (index lists), in the plan's order.</summary>
        public static void ByChunk(Plan plan,out List<int>[] placements,out List<int>[] quads)
        {
            int n=plan.Nx*plan.Nz;placements=new List<int>[n];quads=new List<int>[n];
            for(int i=0;i<plan.Placements.Count;i++){int c=plan.Placements[i].Chunk;(placements[c]??=new List<int>()).Add(i);}
            for(int i=0;i<plan.Quads.Count;i++){int c=plan.Quads[i].Chunk;(quads[c]??=new List<int>()).Add(i);}
        }
        /// <summary>The art bible's caps per chunk (10.1): triangles at LOD0 and at LOD1 (a chunk draws its solid and its glow).</summary>
        public const int FineCap=60000,FarCap=7000;
        /// <summary>The least a heavy chunk is cut down to (m).</summary>
        public const float LeastChunk=8f;
        /// <summary>A chunk as the drawer draws it: a 32 m cell of the plan's grid, or a quarter (an eighth, ...) of a cell too heavy for
        /// the caps, with its placements and surfaces and their triangles at LOD0 and LOD1.</summary>
        public sealed class DrawChunk{public int Cell;public float X0,Z0,Size;public List<int> Placements,Quads;public int Fine,Far;}
        /// <summary>The chunks of a plan: each 32 m cell whose placements and surfaces come to the caps or under (FineCap at LOD0,
        /// FarCap at LOD1), the others cut in four (by each piece's place, a surface's by its centre) until every part does or is
        /// LeastChunk across. Pure and deterministic; counts the bank's triangles without combining.</summary>
        public static List<DrawChunk> DrawChunks(Plan plan,NativePackBank.Bank bank,Dictionary<string,int[]> modules)
        {
            ByChunk(plan,out var pl,out var qs);var units=new List<DrawChunk>();var tris=new Dictionary<int,int>();
            int PropTris(int prop)
            {
                if(bank==null||prop<0||prop>=bank.Props.Count)return 0;if(tris.TryGetValue(prop,out int n))return n;
                foreach(var part in bank.Props[prop].Parts){var m=bank.Meshes[part.Mesh];if(m.Submeshes!=null)foreach(var s in m.Submeshes)n+=s.Indices.Length/3;}
                tris[prop]=n;return n;
            }
            int Tris(List<int> ps,List<int> q,int tier)
            {
                int n=0;
                if(ps!=null&&bank!=null)foreach(int i in ps){var p=plan.Placements[i];if(!p.Dynamic&&modules.TryGetValue(p.Key,out var t))n+=PropTris(t[tier]);}
                if(q!=null)foreach(int i in q)n+=QuadTriangles(plan.Quads[i],tier);
                return n;
            }
            void Split(int cell,float x0,float z0,float size,List<int> ps,List<int> q)
            {
                if((ps==null||ps.Count==0)&&(q==null||q.Count==0))return;
                int fine=Tris(ps,q,0),far=Tris(ps,q,2);
                if(fine<=FineCap&&far<=FarCap||size<=LeastChunk+.01f){units.Add(new DrawChunk{Cell=cell,X0=x0,Z0=z0,Size=size,Placements=ps,Quads=q,Fine=fine,Far=far});return;}
                float h=size*.5f;var sp=new List<int>[4];var sq=new List<int>[4];
                int Part(float x,float z)=>(x>=x0+h?1:0)+(z>=z0+h?2:0);
                if(ps!=null)foreach(int i in ps){var p=plan.Placements[i];int k=Part(p.M[3],p.M[11]);(sp[k]??=new List<int>()).Add(i);}
                if(q!=null)foreach(int i in q){var c=plan.Quads[i].O+plan.Quads[i].U*.5f+plan.Quads[i].V*.5f;int k=Part(c.x,c.z);(sq[k]??=new List<int>()).Add(i);}
                for(int k=0;k<4;k++)Split(cell,x0+(k&1)*h,z0+(k>>1)*h,h,sp[k],sq[k]);
            }
            for(int c=0;c<plan.Nx*plan.Nz;c++)Split(c,plan.MinX+(c%plan.Nx)*ChunkSize,plan.MinZ+(c/plan.Nx)*ChunkSize,ChunkSize,pl[c],qs[c]);
            return units;
        }
        /// <summary>A generated surface's triangles at a tier (as AddQuad cuts it).</summary>
        public static int QuadTriangles(in Quad q,int tier)
        {
            bool flat=Math.Abs(q.N.y)>.5f;float cell=NativeMeridianSurfaces.Cell(tier,flat);
            int nu=Math.Max(1,Math.Min(48,(int)Math.Ceiling(q.U.Length/cell))),nv=Math.Max(1,Math.Min(48,(int)Math.Ceiling(q.V.Length/cell)));return 2*nu*nv;
        }
        /// <summary>The tiers' reach (m, eye to a chunk's bounds): LOD0 within <see cref="FineReach"/> on High (a little nearer on
        /// Balanced), LOD1 beyond it out to the fog's end plus 25 m (bible 10.2; R4's far layer is D2's), the Low tier LOD1 only.</summary>
        public const float FineReach=38f,BalancedReach=24f,FarHigh=175f,FarLow=125f,Hysteresis=3f;
        public static float Reach(NativeGraphicsTier tier)=>tier==NativeGraphicsTier.High?FineReach:tier==NativeGraphicsTier.Medium?BalancedReach:0;
        public static float Far(NativeGraphicsTier tier)=>tier==NativeGraphicsTier.Low?FarLow:FarHigh;
    }
}
