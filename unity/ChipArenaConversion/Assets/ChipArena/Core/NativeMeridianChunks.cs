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
            if(quads!=null){var seam=plan.Seams!=null?new Seam():null;foreach(int qi in quads){if(seam!=null)Fan(plan,qi,tier,seam,into);else AddQuad(plan.Quads[qi],tier,into);}}
        }
        /// <summary>Combines a plan's draw units at a tier, a batch in parallel at a time (as many as the cores), and hands each mesh to
        /// `take` on the calling thread in the units' order (the drawer uploads it there, before the batch's meshes are reused).</summary>
        public static void CombineUnits(Plan plan,List<DrawChunk> units,int tier,NativePackBank.Bank bank,Dictionary<string,int[]> modules,Action<int,NativeMeridianMesh> take)
        {
            int batch=Math.Max(1,Math.Min(units.Count,Environment.ProcessorCount));var pool=new NativeMeridianMesh[batch];for(int i=0;i<batch;i++)pool[i]=new NativeMeridianMesh();
            for(int c0=0;c0<units.Count;c0+=batch)
            {
                int n=Math.Min(batch,units.Count-c0),from=c0;
                System.Threading.Tasks.Parallel.For(0,n,i=>{var u=units[from+i];Combine(plan,u.Cell,tier,bank,modules,u.Placements,u.Quads,pool[i]);});
                for(int i=0;i<n;i++)take(from+i,pool[i]);
            }
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
                if(q!=null)foreach(int i in q)n+=QuadTriangles(plan,i,tier);
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
        /// <summary>A generated surface's triangles at a tier (as AddQuad cuts it, without its seams).</summary>
        public static int QuadTriangles(in Quad q,int tier)
        {
            bool flat=Math.Abs(q.N.y)>.5f;float cell=NativeMeridianSurfaces.Cell(tier,flat);
            int nu=Math.Max(1,Math.Min(48,(int)Math.Ceiling(q.U.Length/cell))),nv=Math.Max(1,Math.Min(48,(int)Math.Ceiling(q.V.Length/cell)));return 2*nu*nv;
        }
        /// <summary>A plan's surface's triangles at a tier, its seams' fans included (as <see cref="AddQuad(Plan,int,int,NativeMeridianMesh)"/>
        /// makes it).</summary>
        public static int QuadTriangles(Plan plan,int qi,int tier)=>plan.Seams?.Tris!=null?plan.Seams.Tris[qi*2+(tier==0?0:1)]:QuadTriangles(plan.Quads[qi],tier);

        // ------------------------------------------------------------------------------------------------------------------
        // Seams: no T-junctions between surfaces, at any mix of LODs
        // ------------------------------------------------------------------------------------------------------------------
        /// <summary>The surfaces' seams. Each surface is cut into its own grid of cells, so where two surfaces met, one's vertices lay
        /// part-way along the other's edge (a T-junction): the rasterizer left pinholes there that the eye saw through, which the toon
        /// outline drew as small dark crosses (the greybox under the dress hid them until it gave way on the live map, 2026-11-20).
        /// Now every surface edge on an axis-aligned line takes the vertices its neighbours on that line put within it: on a stretch
        /// covered only by surfaces of one 32 m cell, their cuts at the tier it draws (the drawer switches a cell's chunks between
        /// LOD0 and LOD1 together); on a stretch where surfaces of two cells meet, both tiers' cuts of all of them, so the seam holds
        /// at any mix of LODs. A cell whose side gains vertices is fanned from a corner whose sides gain none, else from its centre.
        /// The surfaces' corners are snapped per axis to one canonical value (the coordinates the planner computed, merged within
        /// <see cref="SeamTolerance"/>), so a vertex two surfaces share is bitwise the same on both sides. Built once per plan,
        /// read-only after (the chunks combine on workers).</summary>
        public sealed class SeamTable
        {
            /// <summary>Per axis: the corners' coordinate clusters, sorted (the first value of each cluster is its canonical value).</summary>
            public readonly float[][] Lo=new float[3][],Hi=new float[3][];
            /// <summary>The axis-aligned lines that two or more surface edges share.</summary>
            public SeamLine[] Lines;
            /// <summary>Per surface edge (quad * 4 + edge): its line where it shares a stretch with another edge (else null), and its
            /// place among the line's edges.</summary>
            public SeamLine[] EdgeLine;public int[] EdgeSlot;
            /// <summary>Each surface's triangles at LOD0 and LOD1 (quad * 2 + 0 or 1), its seams' fans included.</summary>
            public int[] Tris;
        }
        /// <summary>The surface edges on one axis-aligned line, swept once for both tiers. Per edge and tier (slot = edge * 2 + 0 for
        /// LOD0 or 1 for LOD1): the vertices it gains from the others on its stretch (ascending along the axis), and its own cuts that
        /// snap to a neighbour's canonical value (by cut index).</summary>
        public sealed class SeamLine
        {
            /// <summary>The line's axis and its two other coordinates (canonical).</summary>
            public int Axis;public float Fb,Fc;
            /// <summary>The edges (quad * 4 + edge) by their low end along the axis, and their canonical ends.</summary>
            public int[] Edges;public float[] S,E;
            /// <summary>Per edge: whether it shares a stretch with another.</summary>
            public bool[] On;
            /// <summary>Per slot: its gained vertices, Extra[ExtraAt[slot]..ExtraAt[slot+1]).</summary>
            public int[] ExtraAt;public float[] Extra;
            /// <summary>Per slot: its own cuts that snap to another value, FixIndex/FixValue[FixAt[slot]..FixAt[slot+1]).</summary>
            public int[] FixAt,FixIndex;public float[] FixValue;
        }
        /// <summary>Points closer than this (m) along a seam are one vertex.</summary>
        public const float SeamTolerance=1e-4f;
        private static float Axis(V3 v,int a)=>a==0?v.x:a==1?v.y:v.z;
        /// <summary>A surface's edge k, in the winding's order: 0 O to O+U, 1 O+U to O+U+V, 2 O+U+V to O+V, 3 O+V to O.</summary>
        public static void EdgeOf(in Quad q,int k,out V3 s,out V3 e)
        {
            var c1=q.O+q.U;var c2=c1+q.V;var c3=q.O+q.V;
            s=k==0?q.O:k==1?c1:k==2?c2:c3;e=k==0?c1:k==1?c2:k==2?c3:q.O;
        }
        /// <summary>The axis an edge runs along, or -1 where it is not axis-aligned (a slope's side).</summary>
        private static int AxisOf(V3 d)
        {
            float x=Math.Abs(d.x),y=Math.Abs(d.y),z=Math.Abs(d.z),l=Math.Max(x,Math.Max(y,z));if(l<1e-3f)return -1;
            int a=x>=y&&x>=z?0:y>=z?1:2;float rest=a==0?y+z:a==1?x+z:x+y;return rest<=l*1e-4f?a:-1;
        }
        /// <summary>A surface's cells along edge k at a tier (as AddQuad cuts it).</summary>
        private static int Cells(in Quad q,int k,int tier)
        {
            bool flat=Math.Abs(q.N.y)>.5f;float cell=NativeMeridianSurfaces.Cell(tier,flat);
            return Math.Max(1,Math.Min(48,(int)Math.Ceiling(((k&1)==0?q.U.Length:q.V.Length)/cell)));
        }
        /// <summary>Cut i of n along an edge from s to e, its ends exactly s and e (every surface computes a cut the same way).</summary>
        private static float Cut(float s,float e,int i,int n)=>i==0?s:i==n?e:s+(e-s)*((float)i/n);
        /// <summary>The cluster (index) holding a coordinate on an axis, or -1.</summary>
        private static int Cluster(SeamTable t,int a,float x)
        {
            var lo=t.Lo[a];int i=0,j=lo.Length-1,best=-1;
            while(i<=j){int m=(i+j)>>1;if(lo[m]<=x+1e-6f){best=m;i=m+1;}else j=m-1;}
            return best>=0&&x<=t.Hi[a][best]+1e-6f?best:-1;
        }
        private static float Canon(SeamTable t,int a,float x){int c=Cluster(t,a,x);return c<0?x:t.Lo[a][c];}
        private static long LineKey(SeamTable t,int a,V3 s)
        {
            int b=a==0?1:0,c=a==2?1:2;int i1=Cluster(t,b,Axis(s,b)),i2=Cluster(t,c,Axis(s,c));
            return i1<0||i2<0?-1:(long)a|((long)i1<<2)|((long)i2<<33);
        }
        /// <summary>The seams of a plan's surfaces (see <see cref="SeamTable"/>): every shared line swept once, the lines in parallel (each
        /// writes only its own edges), then every surface's triangle counts, in parallel. Deterministic.</summary>
        public static SeamTable BuildSeams(Plan plan)
        {
            var t=new SeamTable();var qs=plan.Quads;
            for(int a=0;a<3;a++)
            {
                var all=new float[qs.Count*4];int n=0;
                foreach(var q in qs){for(int k=0;k<4;k++){EdgeOf(q,k,out var s,out _);all[n++]=Axis(s,a);}}
                Array.Sort(all);var lo=new List<float>();var hi=new List<float>();
                // chained: a corner within the tolerance of the cluster's last value joins it (two computations of one corner)
                foreach(float x in all){if(lo.Count==0||x>hi[hi.Count-1]+SeamTolerance){lo.Add(x);hi.Add(x);}else hi[hi.Count-1]=x;}
                t.Lo[a]=lo.ToArray();t.Hi[a]=hi.ToArray();
            }
            var lines=new Dictionary<long,List<int>>();
            for(int q=0;q<qs.Count;q++)for(int k=0;k<4;k++)
            {
                EdgeOf(qs[q],k,out var s,out var e);int a=AxisOf(e-s);if(a<0)continue;long key=LineKey(t,a,s);if(key<0)continue;
                if(!lines.TryGetValue(key,out var list))lines[key]=list=new List<int>(2);list.Add(q*4+k);
            }
            var shared=new List<SeamLine>();foreach(var kv in lines)if(kv.Value.Count>1)shared.Add(new SeamLine{Axis=(int)(kv.Key&3),Edges=kv.Value.ToArray()});
            t.Lines=shared.ToArray();t.EdgeLine=new SeamLine[qs.Count*4];t.EdgeSlot=new int[qs.Count*4];
            plan.Seams=t;
            System.Threading.Tasks.Parallel.For(0,t.Lines.Length,()=>new Sweeper(),(i,_,sw)=>{sw.Run(plan,t,t.Lines[i]);return sw;},_=>{});
            t.Tris=new int[qs.Count*2];
            System.Threading.Tasks.Parallel.For(0,qs.Count,()=>new Seam(),(q,_,seam)=>{t.Tris[q*2]=Fan(plan,q,0,seam,null);t.Tris[q*2+1]=Fan(plan,q,2,seam,null);return seam;},_=>{});
            return t;
        }
        /// <summary>One line's sweep (a thread's buffers). The line is cut where any of its edges ends; on each piece two or more edges
        /// cover, every one of them takes the others' cuts. The surfaces of one 32 m cell draw at one tier together (the drawer switches
        /// a cell's chunks together), so a piece covered from one cell takes that tier's cuts; a piece covered from two cells can meet at
        /// either tier, so it takes both tiers' cuts of all of them. Each piece's choice is the same for every edge covering it. One
        /// vertex per cluster of cuts: a corner's canonical value where the cluster holds one, else its least value.</summary>
        private sealed class Sweeper
        {
            private float[] lo=new float[16],hi=new float[16],keys=new float[16];private int[] cell=new int[16],n0=new int[16],n2=new int[16],order=new int[16],stamp=new int[64];
            private readonly List<float> ends=new List<float>();private readonly List<int> active=new List<int>();
            private readonly List<(float x,bool corner,int m,int i,bool own,float raw)> cand=new List<(float,bool,int,int,bool,float)>();
            private readonly List<float> start=new List<float>(),rep=new List<float>();private readonly List<int> clusterOf=new List<int>();
            private List<float>[] extra=new List<float>[32];private List<(int i,float v)>[] fix=new List<(int,float)>[32];
            private static readonly Comparison<(float x,bool corner,int m,int i,bool own,float raw)> ByX=(p,r)=>p.x.CompareTo(r.x);
            public void Run(Plan plan,SeamTable t,SeamLine L)
            {
                int M=L.Edges.Length,a=L.Axis;var qs=plan.Quads;
                if(lo.Length<M){int n=Math.Max(M,lo.Length*2);lo=new float[n];hi=new float[n];keys=new float[n];cell=new int[n];n0=new int[n];n2=new int[n];order=new int[n];}
                if(extra.Length<M*2){Array.Resize(ref extra,M*4);Array.Resize(ref fix,M*4);}
                var S=new float[M];var E=new float[M];
                for(int m=0;m<M;m++)
                {
                    int ec=L.Edges[m];EdgeOf(qs[ec>>2],ec&3,out var s,out var e);S[m]=Canon(t,a,Axis(s,a));E[m]=Canon(t,a,Axis(e,a));keys[m]=Math.Min(S[m],E[m]);order[m]=m;
                    if(m==0){int b=a==0?1:0,c=a==2?1:2;L.Fb=Canon(t,b,Axis(s,b));L.Fc=Canon(t,c,Axis(s,c));}
                }
                // the edges by their low end (ties by edge code: deterministic)
                var k0=keys;var codes=L.Edges;
                Array.Sort(order,0,M,Comparer<int>.Create((x,y)=>{int c=k0[x].CompareTo(k0[y]);return c!=0?c:codes[x].CompareTo(codes[y]);}));
                var edges=new int[M];L.S=new float[M];L.E=new float[M];
                for(int r=0;r<M;r++)
                {
                    int m=order[r],ec=codes[m];edges[r]=ec;L.S[r]=S[m];L.E[r]=E[m];lo[r]=Math.Min(S[m],E[m]);hi[r]=Math.Max(S[m],E[m]);
                    var q=qs[ec>>2];int k=ec&3;cell[r]=q.Chunk;n0[r]=Cells(q,k,0);n2[r]=Cells(q,k,2);
                }
                L.Edges=edges;L.On=new bool[M];
                ends.Clear();for(int m=0;m<M;m++){ends.Add(lo[m]);ends.Add(hi[m]);}ends.Sort();
                // a cut within the tolerance of an edge's end on this line is that end (one rule for the whole line, so a point near a
                // piece's end is the same vertex in every piece and for every edge)
                float Snap(float x){int i=0,j=ends.Count-1;while(i<=j){int h=(i+j)>>1;if(ends[h]<x)i=h+1;else j=h-1;}
                    float best=x,d=SeamTolerance;if(i<ends.Count&&Math.Abs(ends[i]-x)<=d){best=ends[i];d=Math.Abs(ends[i]-x);}if(i>0&&Math.Abs(ends[i-1]-x)<d)best=ends[i-1];return best;}
                bool IsEnd(float x){int i=ends.BinarySearch(x);return i>=0;}
                for(int slot=0;slot<M*2;slot++){(extra[slot]??=new List<float>()).Clear();(fix[slot]??=new List<(int,float)>()).Clear();}
                for(int ti=0;ti<2;ti++)
                {
                    int next=0;active.Clear();
                    for(int p=0;p+1<ends.Count;p++)
                    {
                        float c0=ends[p],c1=ends[p+1];if(c1-c0<=SeamTolerance)continue;float mid=(c0+c1)*.5f;
                        while(next<M&&lo[next]<=mid)active.Add(next++);
                        for(int r=active.Count-1;r>=0;r--)if(hi[active[r]]<mid)active.RemoveAt(r);
                        if(active.Count<2)continue;
                        bool mixed=false;foreach(int m in active){L.On[m]=true;if(cell[m]!=cell[active[0]])mixed=true;}
                        // the cuts on the piece (and a tolerance either side): each covering edge's at this tier, at both where two cells meet
                        cand.Clear();
                        foreach(int m in active)for(int tr=0;tr<2;tr++)
                        {
                            bool own=tr==ti;if(!own&&!mixed)continue;int n=tr==0?n0[m]:n2[m];float s=L.S[m],e=L.E[m];
                            float u0=(c0-SeamTolerance-s)/(e-s)*n,u1=(c1+SeamTolerance-s)/(e-s)*n;
                            int i0=Math.Max(0,(int)Math.Floor(Math.Min(u0,u1))-1),i1=Math.Min(n,(int)Math.Ceiling(Math.Max(u0,u1))+1);
                            for(int i=i0;i<=i1;i++){float raw=Cut(s,e,i,n),x=Snap(raw);if(x>=c0-SeamTolerance&&x<=c1+SeamTolerance)cand.Add((x,i==0||i==n||IsEnd(x),m,i,own,raw));}
                        }
                        cand.Sort(ByX);
                        start.Clear();rep.Clear();clusterOf.Clear();bool corner=false;
                        foreach(var c in cand)
                        {
                            if(start.Count==0||c.x>start[start.Count-1]+SeamTolerance){start.Add(c.x);rep.Add(c.x);corner=c.corner;}
                            else if(c.corner&&!corner){rep[rep.Count-1]=c.x;corner=true;}
                            clusterOf.Add(start.Count-1);
                        }
                        if(stamp.Length<start.Count)stamp=new int[start.Count*2];
                        // per covering edge: its own cuts snap to their clusters; the clusters none of its own cuts holds are its gains
                        foreach(int m in active)
                        {
                            int slot=m*2+ti;for(int c=0;c<start.Count;c++)stamp[c]=0;
                            for(int j=0;j<cand.Count;j++)
                            {
                                var c=cand[j];if(c.m!=m||!c.own)continue;int cl=clusterOf[j];stamp[cl]=1;float v=rep[cl];
                                if(v!=c.raw){var fl=fix[slot];if(fl.Count==0||fl[fl.Count-1].i!=c.i)fl.Add((c.i,v));}
                            }
                            float lim0=lo[m]+SeamTolerance,lim1=hi[m]-SeamTolerance;var el=extra[slot];
                            for(int c=0;c<start.Count;c++){if(stamp[c]!=0)continue;float v=rep[c];if(v<=lim0||v>=lim1)continue;if(el.Count==0||el[el.Count-1]!=v)el.Add(v);}
                        }
                    }
                }
                // the results by slot
                int ne=0,nf=0;for(int slot=0;slot<M*2;slot++){if(!L.On[slot>>1])continue;ne+=extra[slot].Count;nf+=fix[slot].Count;}
                L.ExtraAt=new int[M*2+1];L.FixAt=new int[M*2+1];L.Extra=new float[ne];L.FixIndex=new int[nf];L.FixValue=new float[nf];ne=nf=0;
                for(int slot=0;slot<M*2;slot++)
                {
                    L.ExtraAt[slot]=ne;L.FixAt[slot]=nf;if(!L.On[slot>>1])continue;
                    foreach(float v in extra[slot])L.Extra[ne++]=v;foreach(var (i,v) in fix[slot]){L.FixIndex[nf]=i;L.FixValue[nf]=v;nf++;}
                }
                L.ExtraAt[M*2]=ne;L.FixAt[M*2]=nf;
                for(int m=0;m<M;m++)if(L.On[m]){t.EdgeLine[edges[m]]=L;t.EdgeSlot[edges[m]]=m;}
            }
        }
        /// <summary>A surface's four edges at a tier: each seamed edge's points (world, in the edge's order) and where the surface's own
        /// cell corners fall among them.</summary>
        private sealed class Seam
        {
            public readonly List<V3>[] Points={new List<V3>(),new List<V3>(),new List<V3>(),new List<V3>()};
            public readonly List<int>[] Own={new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            public readonly bool[] On=new bool[4];
            public readonly List<int>[] Extra={new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            public readonly List<int> Poly=new List<int>(16);
        }
        /// <summary>Edge k of surface qi at a tier, from its line's sweep: false where it shares no stretch with another edge.</summary>
        private static bool SeamEdge(Plan plan,int qi,int k,int tier,Seam into)
        {
            var t=plan.Seams;var pts=into.Points[k];var own=into.Own[k];pts.Clear();own.Clear();into.On[k]=false;
            int ec=qi*4+k;var L=t.EdgeLine[ec];if(L==null)return false;int m=t.EdgeSlot[ec],slot=m*2+(tier==0?0:1);
            int a=L.Axis,b=a==0?1:0,c=a==2?1:2;float fb=L.Fb,fc=L.Fc,sa=L.S[m],ea=L.E[m];bool up=ea>=sa;int n=Cells(plan.Quads[qi],k,tier);
            V3 At(float x){var p=new V3();if(a==0)p.x=x;else if(a==1)p.y=x;else p.z=x;if(b==0)p.x=fb;else if(b==1)p.y=fb;else p.z=fb;if(c==0)p.x=fc;else if(c==1)p.y=fc;else p.z=fc;return p;}
            int e0=L.ExtraAt[slot],e1=L.ExtraAt[slot+1],f0=L.FixAt[slot],f1=L.FixAt[slot+1];int xi=up?e0:e1-1;
            // the own cuts (snapped where a neighbour's corner holds one) and the gained vertices between them, in the edge's order
            for(int i=0;i<=n;i++)
            {
                float x=Cut(sa,ea,i,n);for(int f=f0;f<f1;f++)if(L.FixIndex[f]==i){x=L.FixValue[f];break;}
                if(up){while(xi<e1&&L.Extra[xi]<x){pts.Add(At(L.Extra[xi]));xi++;}}
                else{while(xi>=e0&&L.Extra[xi]>x){pts.Add(At(L.Extra[xi]));xi--;}}
                own.Add(pts.Count);pts.Add(At(x));
            }
            into.On[k]=true;return true;
        }
        /// <summary>A plan's surface into the mesh at a tier, its cells cut as <see cref="AddQuad(in Quad,int,NativeMeridianMesh)"/>
        /// cuts them, its seams conforming (<see cref="SeamTable"/>).</summary>
        public static void AddQuad(Plan plan,int qi,int tier,NativeMeridianMesh into)
        {
            if(plan.Seams==null){AddQuad(plan.Quads[qi],tier,into);return;}
            Fan(plan,qi,tier,new Seam(),into);
        }
        /// <summary>Makes (into a mesh) or counts (`into` null) a surface's triangles with its seams.</summary>
        private static int Fan(Plan plan,int qi,int tier,Seam seam,NativeMeridianMesh into)
        {
            var q=plan.Quads[qi];var f=NativeMeridianSurfaces.Fins[q.Fin];float lu=q.U.Length,lv=q.V.Length;bool flat=Math.Abs(q.N.y)>.5f;float cell=NativeMeridianSurfaces.Cell(tier,flat);
            int nu=Math.Max(1,Math.Min(48,(int)Math.Ceiling(lu/cell))),nv=Math.Max(1,Math.Min(48,(int)Math.Ceiling(lv/cell)));
            bool any=false;for(int k=0;k<4;k++)any|=SeamEdge(plan,qi,k,tier,seam);
            // the extra vertices on a cell's side: edge k between its own corners m and m+1 (in the edge's order)
            int Extra(int k,int m)=>seam.On[k]?Math.Max(0,seam.Own[k][m+1]-seam.Own[k][m]-1):0;
            int ExtraOf(int i,int j)=>(j==0?Extra(0,i):0)+(i==nu-1?Extra(1,j):0)+(j==nv-1?Extra(2,nu-1-i):0)+(i==0?Extra(3,nv-1-j):0);
            // the sides of cell (i, j) that gain vertices (bit 0 its bottom, 1 its right, 2 its top, 3 its left), and its fan's apex: a
            // corner neither of whose sides gains any (0 its first, 1 second, 2 third, 3 fourth in the winding's order), else -1 its centre
            int SidesOf(int i,int j)=>(j==0&&Extra(0,i)>0?1:0)|(i==nu-1&&Extra(1,j)>0?2:0)|(j==nv-1&&Extra(2,nu-1-i)>0?4:0)|(i==0&&Extra(3,nv-1-j)>0?8:0);
            int Apex(int sides)=>(sides&9)==0?0:(sides&3)==0?1:(sides&6)==0?2:(sides&12)==0?3:-1;
            if(into==null)
            {
                int n=2*nu*nv;if(!any)return n;
                for(int j=0;j<nv;j++)for(int i=0;i<nu;i++){if(j!=0&&j!=nv-1&&i!=0&&i!=nu-1)continue;int x=ExtraOf(i,j);if(x>0)n+=(Apex(SidesOf(i,j))>=0?x:x+2);}
                return n;
            }
            float kind=f.Detail+q.Layer/32f;
            int Vertex(V3 p){int v=into.Reserve(1);into.P[v*3]=p.x;into.P[v*3+1]=p.y;into.P[v*3+2]=p.z;into.Bound(p.x,p.y,p.z);into.N[v*3]=q.N.x;into.N[v*3+1]=q.N.y;into.N[v*3+2]=q.N.z;
                into.Uv0[v*2]=f.Smooth;into.Uv0[v*2+1]=f.Metal;into.Uv1[v*2]=kind;into.Uv1[v*2+1]=f.Param;NativeMeridianSurfaces.Colour(f,p,q.N,q.Floor,into.C,v*4);return v;}
            // the grid: its boundary vertices where a seamed edge puts them (canonical), the rest as cut
            V3 Grid(int i,int j)
            {
                if(j==0&&seam.On[0])return seam.Points[0][seam.Own[0][i]];
                if(i==nu&&seam.On[1])return seam.Points[1][seam.Own[1][j]];
                if(j==nv&&seam.On[2])return seam.Points[2][seam.Own[2][nu-i]];
                if(i==0&&seam.On[3])return seam.Points[3][seam.Own[3][nv-j]];
                // a corner no seam holds still snaps to the canonical corner (an edge that only touches another end to end shares it)
                if((i==0||i==nu)&&(j==0||j==nv)){var t=plan.Seams;EdgeOf(q,i==0?(j==0?0:3):(j==0?1:2),out var corner,out _);return new V3(Canon(t,0,corner.x),Canon(t,1,corner.y),Canon(t,2,corner.z));}
                return q.O+q.U*((float)i/nu)+q.V*((float)j/nv);
            }
            int at=into.Vertices;for(int j=0;j<=nv;j++)for(int i=0;i<=nu;i++)Vertex(Grid(i,j));
            int G(int i,int j)=>at+i+j*(nu+1);
            // the extra vertices, by edge and list position
            var extra=seam.Extra;
            for(int k=0;k<4;k++){var mark=extra[k];mark.Clear();if(!seam.On[k])continue;var pts=seam.Points[k];for(int r=0;r<pts.Count;r++)mark.Add(-1);foreach(int o in seam.Own[k])mark[o]=-2;
                for(int r=0;r<mark.Count;r++)if(mark[r]==-1)mark[r]=Vertex(pts[r]);}
            var poly=seam.Poly;
            void Side(int k,int m){if(!seam.On[k])return;for(int r=seam.Own[k][m]+1;r<seam.Own[k][m+1];r++)poly.Add(extra[k][r]);}
            // the bank's winding: (p1 - p0) x (p2 - p0) out of the face, as u x v is
            for(int j=0;j<nv;j++)for(int i=0;i<nu;i++)
            {
                int a=G(i,j),b=G(i+1,j),c=G(i+1,j+1),d=G(i,j+1);
                if(!any||(j!=0&&j!=nv-1&&i!=0&&i!=nu-1)||ExtraOf(i,j)==0){into.Solid.Add(a);into.Solid.Add(b);into.Solid.Add(c);into.Solid.Add(a);into.Solid.Add(c);into.Solid.Add(d);continue;}
                // a cell whose side gains vertices: its polygon, round in the winding's order, fanned from a corner whose sides gain
                // none (its triangles never lie flat along a side), else from its centre
                poly.Clear();
                int oa=poly.Count;poly.Add(a);if(j==0)Side(0,i);int ob=poly.Count;poly.Add(b);if(i==nu-1)Side(1,j);int oc=poly.Count;poly.Add(c);if(j==nv-1)Side(2,nu-1-i);int od=poly.Count;poly.Add(d);if(i==0)Side(3,nv-1-j);
                int apex=Apex(SidesOf(i,j)),pc=poly.Count;
                if(apex>=0){int o=apex==0?oa:apex==1?ob:apex==2?oc:od;for(int r=1;r<pc-1;r++){into.Solid.Add(poly[o]);into.Solid.Add(poly[(o+r)%pc]);into.Solid.Add(poly[(o+r+1)%pc]);}continue;}
                var centre=(Grid(i,j)+Grid(i+1,j)+Grid(i+1,j+1)+Grid(i,j+1))*.25f;int ce=Vertex(centre);
                for(int r=0;r<pc;r++){into.Solid.Add(ce);into.Solid.Add(poly[r]);into.Solid.Add(poly[(r+1)%pc]);}
            }
            return 0;
        }
        /// <summary>The tiers' reach (m, eye to a chunk's bounds): LOD0 within <see cref="FineReach"/> on High (a little nearer on
        /// Balanced), LOD1 beyond it out to the fog's end plus 25 m (bible 10.2; R4's far layer is D2's), the Low tier LOD1 only.</summary>
        public const float FineReach=38f,BalancedReach=24f,FarHigh=175f,FarLow=125f,Hysteresis=3f;
        public static float Reach(NativeGraphicsTier tier)=>tier==NativeGraphicsTier.High?FineReach:tier==NativeGraphicsTier.Medium?BalancedReach:0;
        public static float Far(NativeGraphicsTier tier)=>tier==NativeGraphicsTier.Low?FarLow:FarHigh;
    }
}
