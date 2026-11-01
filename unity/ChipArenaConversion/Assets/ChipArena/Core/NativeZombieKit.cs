using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The voxel kit the Siren's zombies are built with (owner 2026-10-17: "Improve these vastly", then "Keep voxel style, just
    // improve it. The sculpted looks odd"; docs/ZOMBIE-DESIGN.md). Presentation only, engine-free and deterministic, built once
    // when the bank loads. On top of NativeHordeModels' box kit it adds:
    //  - voxel grids (Vox, Voxels): a part filled cell by cell in its joint's frame, each cell knowing its depth under the
    //    surface, so a part is painted in layers (suit over skin over flesh and bone) and a tear, a socket or a wound is cells
    //    taken away, the layer under them showing as inset voxels. Only the faces open to air are drawn, merged into larger
    //    quads where they match, each corner carrying its voxel occlusion (the cells round it);
    //  - the pilot's plates cracked into tiles, offset and tilted, some gone (Tiles); block rags and block cables that sway
    //    (Strand, Chain); stepped cracks, veins and raw splits (Zig);
    //  - per-vertex shading data in WeaponSection.Data (uv0 on the mesh) for ChipArena/ZombieLit: x the surface class
    //    (Surf*: skin, wet flesh, armour shell, cloth, bone, matte, wire) plus .02 + .96 x the baked occlusion, y the missing-piece
    //    variant (1..4, hidden per add) plus .02 + .96 x the sway weight of a dangling part (K.Pack); the vertex colour's alpha names the
    //    proportion region (head, torso width, limb girth) the shader scales per add;
    //  - Finish: the occlusion bake (each part's occluder spheres darken the vertices of the others on its joint and, at .7, on
    //    the neighbouring joints at rest; a grid's own corner occlusion folded in), a stepped shade per block, grime and blood
    //    toward the hands, the feet and the mouth, the regions;
    //  - Variation: the add's own tone, mottling seed and missing pieces by id, as the per-instance body tint (white = the plain
    //    model, so every other draw of these models is unchanged).
    // Every vertex stays inside the authority's capsule with the per-add variation and sway at their largest
    // (Tests/HordeKindChecks); the tiers drop the shade steps, the smallest blocks and the bevels.
    public static partial class NativeHordeModels
    {
        /// <summary>Surface classes (WeaponPaint.Surface, uv0.x's whole part): ChipArena/ZombieLit's wrap, sheen and mottling per class.</summary>
        public const int SurfPlain=0,SurfSkin=1,SurfFlesh=2,SurfShell=3,SurfCloth=4,SurfBone=5,SurfMatte=6,SurfWire=7;
        /// <summary>Proportion regions (vertex colour alpha): 1 none, the head, the torso's width, a limb's girth.</summary>
        public const float RegionNone=1,RegionHead=.75f,RegionTorso=.5f,RegionLimb=.25f;
        /// <summary>The per-add variation's reach (ZombieLit vert): head scale, torso width and limb girth each within 1 +- these,
        /// and a dangling part's sway (m). Tests/HordeKindChecks holds the models inside the capsule at all of them.</summary>
        public const float VaryHead=.05f,VaryWidth=.05f,VaryGirth=.07f,SwayReach=.03f;
        /// <summary>
        /// The add's variation as the per-instance body tint ChipArena/ZombieLit reads (no other zombie draw uses the body tint's
        /// colour): r = 1 + its skin tone (-.45 .. .45: greener and greyer to paler and bruised), g = 1 + its pattern seed (0..1:
        /// the mottling's offset and the proportions), b = 1 + its missing pieces / 16 (four bits, each kept away from the add
        /// about two times in five), a = 1. White draws the model as built.
        /// </summary>
        public static Vector4 Variation(int id)
        {
            uint s=NativeHordeKinds.Seed(id);float tone=(float)(NativeHordeKinds.Hash(s,0x71)-.5)*.9f,seed=.002f+.996f*(float)NativeHordeKinds.Hash(s,0x72);int mask=0;
            for(int b=0;b<4;b++)if(NativeHordeKinds.Hash(s,0x73+b)<.38)mask|=1<<b;if(mask==15)mask=7;
            return new Vector4(1+tone,1+seed,1+mask/16f,1);
        }
        /// <summary>Whether a bank model id is one of the Siren's seven kinds' models or jaws (they take the add's Variation as the
        /// body tint; the zone kinds, the old bank minions and every other model keep white). A set made once: no allocation.</summary>
        public static bool Varies(string model)=>model!=null&&VariedIds.Contains(model);
        private static readonly HashSet<string> VariedIds=MakeVaried();
        private static HashSet<string> MakeVaried()
        {
            var set=new HashSet<string>();
            for(int k=0;k<NativeHordeKinds.Count;k++)for(int l=0;l<NativeHordeKinds.MostLooks;l++)for(int t=0;t<Tiers;t++){set.Add(Ids(false)[(k*NativeHordeKinds.MostLooks+l)*Tiers+t]);set.Add(Ids(true)[(k*NativeHordeKinds.MostLooks+l)*Tiers+t]);}
            for(int l=0;l<NativeHordeKinds.MostLooks;l++)for(int t=0;t<Tiers;t++)set.Add(CreamId(l,t));// the Ironworks' cream-seamed brute
            return set;
        }
        /// <summary>The proportions ZombieLit's vert gives a pattern seed (0..1; 0, a white tint, the model as built): head scale,
        /// torso width, limb girth (its exact expression, for the checks).</summary>
        public static Vector3 Proportions(float seed)
        {
            static float F(float x){return x-(float)Math.Floor(x);}float on=seed>=.0005f?1:0;
            return new Vector3(1+on*VaryHead*(2*F(seed*5.31f+.17f)-1),1+on*VaryWidth*(2*F(seed*9.73f+.61f)-1),1+on*VaryGirth*(2*F(seed*13.37f+.29f)-1));
        }

        /// <summary>A whole part (the class or the variant) and a 0..1 value packed as ZombieLit unpacks them: whole + .02 + .96 x
        /// value, never on a whole number, so the interpolated value never rounds into the next class.</summary>
        public static float Pack(float whole,float value)=>whole+PackLow+PackSpan*Math.Max(0,Math.Min(1,value));
        public const float PackLow=.02f,PackSpan=.96f;

        // ================================================================================================================
        // Voxel grids
        // ================================================================================================================
        /// <summary>
        /// A voxel grid in a joint's frame (left-authored like the rest): NX x NY x NZ cubes of side C from the corner O. A cell
        /// holds a paint (1-based into Paints; 0 empty) and a shade step (0..3, the blocky mottling); Shape fills it and measures
        /// each cell's depth under the surface (0 on the surface), which Paint reads to lay the layers and cut the tears.
        /// </summary>
        private sealed class Vox
        {
            public readonly int NX,NY,NZ;public readonly float C;public readonly Vector3 O;
            private readonly short[] cell,depth;private readonly byte[] shade;
            public readonly List<WeaponPaint> Paints=new List<WeaponPaint>();
            public Vox(int nx,int ny,int nz,float c,Vector3 o){NX=Math.Max(1,nx);NY=Math.Max(1,ny);NZ=Math.Max(1,nz);C=c;O=o;cell=new short[NX*NY*NZ];depth=new short[cell.Length];shade=new byte[cell.Length];}
            /// <summary>A grid of whole cells over bounds (left-authored, metres), its cell as near `c` as fits.</summary>
            public static Vox Over(float x0,float x1,float y0,float y1,float z0,float z1,float c)
            {
                int nx=Math.Max(1,(int)Math.Round((x1-x0)/c)),ny=Math.Max(1,(int)Math.Round((y1-y0)/c)),nz=Math.Max(1,(int)Math.Round((z1-z0)/c));
                float cc=Math.Min((x1-x0)/nx,Math.Min((y1-y0)/ny,(z1-z0)/nz));
                var o=new Vector3((x0+x1)*.5f-nx*cc*.5f,(y0+y1)*.5f-ny*cc*.5f,(z0+z1)*.5f-nz*cc*.5f);return new Vox(nx,ny,nz,cc,o);
            }
            public int Add(in WeaponPaint p){Paints.Add(p);return Paints.Count;}
            public bool In(int x,int y,int z)=>x>=0&&y>=0&&z>=0&&x<NX&&y<NY&&z<NZ;
            private int I(int x,int y,int z)=>(z*NY+y)*NX+x;
            public int Get(int x,int y,int z)=>In(x,y,z)?cell[I(x,y,z)]:0;
            public void Set(int x,int y,int z,int p){if(In(x,y,z))cell[I(x,y,z)]=(short)p;}
            public int Depth(int x,int y,int z)=>In(x,y,z)?depth[I(x,y,z)]:-1;
            public int Shade(int x,int y,int z)=>In(x,y,z)?shade[I(x,y,z)]:0;
            public void SetShade(int x,int y,int z,int s){if(In(x,y,z))shade[I(x,y,z)]=(byte)Math.Max(0,Math.Min(3,s));}
            /// <summary>Fills an inclusive box of cells with paint p (0 clears).</summary>
            public void Fill(int x0,int x1,int y0,int y1,int z0,int z1,int p){for(int z=Math.Max(0,z0);z<=Math.Min(NZ-1,z1);z++)for(int y=Math.Max(0,y0);y<=Math.Min(NY-1,y1);y++)for(int x=Math.Max(0,x0);x<=Math.Min(NX-1,x1);x++)cell[I(x,y,z)]=(short)p;}
            /// <summary>Fills the cells `inside` holds with p, then measures every filled cell's depth (6-connected cells to air; the
            /// grid's outside is air).</summary>
            public void Shape(Func<int,int,int,bool> inside,int p)
            {
                for(int z=0;z<NZ;z++)for(int y=0;y<NY;y++)for(int x=0;x<NX;x++)if(inside(x,y,z))cell[I(x,y,z)]=(short)p;
                Measure();
            }
            /// <summary>Depth of every filled cell by a breadth-first walk in from the air.</summary>
            public void Measure()
            {
                var q=new Queue<int>();
                for(int z=0;z<NZ;z++)for(int y=0;y<NY;y++)for(int x=0;x<NX;x++)
                {
                    int i=I(x,y,z);depth[i]=-1;if(cell[i]==0)continue;
                    if(Get(x-1,y,z)==0||Get(x+1,y,z)==0||Get(x,y-1,z)==0||Get(x,y+1,z)==0||Get(x,y,z-1)==0||Get(x,y,z+1)==0){depth[i]=0;q.Enqueue(i);}
                }
                while(q.Count>0)
                {
                    int i=q.Dequeue();int x=i%NX,y=(i/NX)%NY,z=i/(NX*NY);int d=depth[i]+1;
                    void Visit(int a,int b,int c){if(!In(a,b,c))return;int j=I(a,b,c);if(cell[j]!=0&&depth[j]<0){depth[j]=(short)d;q.Enqueue(j);}}
                    Visit(x-1,y,z);Visit(x+1,y,z);Visit(x,y-1,z);Visit(x,y+1,z);Visit(x,y,z-1);Visit(x,y,z+1);
                }
            }
            /// <summary>Repaints every filled cell: paint(x, y, z, depth) returns its paint (0 takes it away). The depths are the
            /// shape's, so a cut shows the layer under it.</summary>
            public void Paint(Func<int,int,int,int,int> paint)
            {
                for(int z=0;z<NZ;z++)for(int y=0;y<NY;y++)for(int x=0;x<NX;x++){int i=I(x,y,z);if(cell[i]!=0)cell[i]=(short)paint(x,y,z,depth[i]);}
            }
            /// <summary>A cell corner (grid units) in metres.</summary>
            public Vector3 At(float x,float y,float z)=>O+new Vector3(x*C,y*C,z*C);
            public Vector3 Centre(int x,int y,int z)=>At(x+.5f,y+.5f,z+.5f);
            /// <summary>The filled cells' bounds in metres (false when empty).</summary>
            public bool Bounds(out Vector3 min,out Vector3 max)
            {
                int x0=NX,y0=NY,z0=NZ,x1=-1,y1=-1,z1=-1;
                for(int z=0;z<NZ;z++)for(int y=0;y<NY;y++)for(int x=0;x<NX;x++)if(cell[I(x,y,z)]!=0){x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);z0=Math.Min(z0,z);z1=Math.Max(z1,z);}
                min=At(x0,y0,z0);max=At(x1+1,y1+1,z1+1);return x1>=0;
            }
        }

        private sealed partial class K
        {
            /// <summary>The joint being drawn (On sets it).</summary>
            public int Bone;
            private WeaponMesh CurMesh=>SplitJaw&&S==JawSculpt?JawMesh:Mesh;
            private WeaponSection Sec=>CurMesh.Section(Bone,WeaponStretch.None);
            private struct Occluder{public WeaponMesh Mesh;public int Bone,Prim;public Vector3 C;public float R;}
            private struct PrimSpan{public WeaponSection Sec;public int From,To,Prim;}
            private readonly List<Occluder> occluders=new List<Occluder>();private readonly List<PrimSpan> spans=new List<PrimSpan>();private int prim,curPrim;
            /// <summary>The voxel grids' primitives (their shade steps are per cell, not per block).</summary>
            private readonly HashSet<int> voxelPrims=new HashSet<int>();
            /// <summary>Hashes 0..1 for the build's own jags and choices (deterministic per kind and look).</summary>
            public float H(int i,int salt)=>(float)NativeGoreRules.Hash01(seed,i,salt);
            /// <summary>Draws one primitive: its vertices carry its id, so its own occluders never darken it.</summary>
            public void Prim(Action draw){int id=++prim,saved=curPrim;curPrim=id;var sec=Sec;int from=sec.Positions.Count;draw();spans.Add(new PrimSpan{Sec=sec,From=from,To=sec.Positions.Count,Prim=id});curPrim=saved;}
            /// <summary>An occluder sphere on the current joint (left-authored; mirrored with the part).</summary>
            public void Occ(Vector3 c,float r){if(r<=.004f)return;occluders.Add(new Occluder{Mesh=CurMesh,Bone=Bone,Prim=curPrim,C=new Vector3(c.X*side,c.Y,c.Z),R=r});}
            /// <summary>Draws a missing-piece variant (1..4: hidden on the adds whose variation drops it) and/or a dangling part whose
            /// vertices sway by `sway`(joint-frame point, 0..1).</summary>
            public void Piece(int variant,Func<Vector3,float> sway,Action draw)
            {
                var meshes=SplitJaw?new[]{Mesh,JawMesh}:new[]{Mesh};var before=new Dictionary<WeaponSection,int>();
                foreach(var m in meshes)foreach(var s in m.Sections)before[s]=s.Positions.Count;
                draw();
                foreach(var m in meshes)foreach(var s in m.Sections)
                {
                    int from=before.TryGetValue(s,out int f)?f:0;
                    for(int i=from;i<s.Positions.Count;i++)
                    {
                        // Nested pieces compose: an inner part's sway survives an outer variant, an inner variant an outer sway.
                        float y=s.Data[i].Y,whole=(float)Math.Floor(y+1e-4f),frac=y-whole;
                        float w=sway==null?frac:.5f*Math.Max(0,Math.Min(1,sway(s.Positions[i])));s.Data[i]=new Vector2(s.Data[i].X,(variant!=0?variant:whole)+w);
                    }
                }
            }
            /// <summary>A frame at `centre` facing `face` (+z) with `up` (+y), turned by `roll` about its face.</summary>
            public static Matrix4x4 PlateAt(Vector3 centre,Vector3 face,Vector3 up,float roll=0)
            {
                var z=Vector3.Normalize(face);var x=Vector3.Normalize(Vector3.Cross(up,z));var y=Vector3.Cross(z,x);
                var m=new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);if(roll!=0)m=Matrix4x4.CreateRotationZ(roll)*m;m.M41=centre.X;m.M42=centre.Y;m.M43=centre.Z;return m;
            }

            // ================================================================================================================
            // The voxel mesher
            // ================================================================================================================
            /// <summary>The corner occlusion of 0..3 neighbouring cells (MagicaVoxel-style: two sides and the diagonal).</summary>
            private static readonly float[] VoxAo={0,.2f,.36f,.5f};
            /// <summary>
            /// Draws a grid: every cell face open to air, coplanar faces of one paint, shade step and flat occlusion merged into
            /// larger quads, each corner carrying the occlusion of the cells round it. `jitter` is the shade steps' reach (the
            /// blocky mottling; lit paints only; Full only). One primitive, one occluder over its filled bounds.
            /// </summary>
            public void Voxels(Vox v,float jitter=0,bool occlude=true)
            {
                float jit=Fine?jitter:0;
                Prim(()=>
                {
                    voxelPrims.Add(curPrim);Mirror(()=>MeshVox(v,jit));
                    if(occlude&&v.Bounds(out var mn,out var mx)){var e=mx-mn;Occ((mn+mx)*.5f,Math.Min(e.X,Math.Min(e.Y,e.Z))*.45f);}
                });
            }
            private void MeshVox(Vox v,float jitter)
            {
                int[] n={v.NX,v.NY,v.NZ};int most=Math.Max(n[0]*n[1],Math.Max(n[1]*n[2],n[0]*n[2]));var mask=new long[most];var c=new int[3];var f=new int[3];
                for(int a=0;a<3;a++)
                {
                    int u=(a+1)%3,w=(a+2)%3;int nu=n[u],nw=n[w];
                    for(int sg=-1;sg<=1;sg+=2)for(int i=0;i<n[a];i++)
                    {
                        // The open faces of slice i toward sg: paint, shade step and the four corners' occlusion steps as one key.
                        for(int k=0;k<nw;k++)for(int j=0;j<nu;j++)
                        {
                            c[a]=i;c[u]=j;c[w]=k;int p=v.Get(c[0],c[1],c[2]);long key=0;
                            if(p!=0)
                            {
                                f[a]=i+sg;f[u]=j;f[w]=k;
                                if(v.Get(f[0],f[1],f[2])==0)
                                {
                                    int Solid(int du,int dw){int x=f[0],y=f[1],z=f[2];if(u==0)x+=du;else if(u==1)y+=du;else z+=du;if(w==0)x+=dw;else if(w==1)y+=dw;else z+=dw;return v.Get(x,y,z)!=0?1:0;}
                                    int Corner(int du,int dw){int s1=Solid(du,0),s2=Solid(0,dw);return s1==1&&s2==1?3:s1+s2+Solid(du,dw);}
                                    int ao=Corner(-1,-1)|Corner(1,-1)<<2|Corner(1,1)<<4|Corner(-1,1)<<6;
                                    var pp=v.Paints[p-1];int sh=jitter>0&&pp.Lit&&(pp.Surface==SurfSkin||pp.Surface==SurfFlesh||pp.Surface==SurfBone)?v.Shade(c[0],c[1],c[2])+1:0;
                                    key=(long)(uint)p|(long)(uint)sh<<16|(long)(uint)ao<<20|1L<<40;
                                }
                            }
                            mask[k*nu+j]=key;
                        }
                        // Greedy rectangles over equal keys; a face whose corners differ stays one cell (its occlusion is its own).
                        for(int k=0;k<nw;k++)for(int j=0;j<nu;)
                        {
                            long key=mask[k*nu+j];if(key==0){j++;continue;}
                            int ao=(int)(key>>20)&0xff;bool flat=(ao&3)==((ao>>2)&3)&&(ao&3)==((ao>>4)&3)&&(ao&3)==((ao>>6)&3);
                            int wd=1,ht=1;
                            if(flat)
                            {
                                while(j+wd<nu&&mask[k*nu+j+wd]==key)wd++;
                                for(bool grow=true;grow&&k+ht<nw;){for(int t=0;t<wd;t++)if(mask[(k+ht)*nu+j+t]!=key){grow=false;break;}if(grow)ht++;}
                            }
                            for(int kk=0;kk<ht;kk++)for(int t=0;t<wd;t++)mask[(k+kk)*nu+j+t]=0;
                            int p=(int)(key&0xffff),sh=(int)(key>>16)&0xf;var paint=v.Paints[p-1];
                            if(sh>0){float m=1+jitter*((sh-1)-1.5f)/1.5f;paint=new WeaponPaint(paint.Color*m,paint.Emit,paint.Tag,paint.Surface);}
                            float pa=i+(sg>0?1:0);
                            Vector3 Pt(float jj,float kq){var g=new float[3];g[a]=pa;g[u]=jj;g[w]=kq;return v.At(g[0],g[1],g[2]);}
                            Vector3 A=Pt(j,k),B=Pt(j+wd,k),D=Pt(j+wd,k+ht),E=Pt(j,k+ht);
                            float oa=VoxAo[ao&3],ob=VoxAo[(ao>>2)&3],od=VoxAo[(ao>>4)&3],oe=VoxAo[(ao>>6)&3];
                            // u x w is +a: a-b-d-e faces +a; the -a faces turn the other way round.
                            if(sg>0)S.QuadData(paint,A,B,D,E,oa,ob,od,oe);else S.QuadData(paint,A,E,D,B,oa,oe,od,ob);
                            j+=wd;
                        }
                    }
                }
            }

            // ================================================================================================================
            // Plates, rags, cables and cracks
            // ================================================================================================================
            /// <summary>
            /// One of the pilot's plates cracked into tiles: a w x h x t slab in frame `at` (face +z, up +y) split into cols x rows
            /// tiles with `gap` between them (the cracks), a tile gone where its hash falls under `drop` (never the last one), the
            /// rest shifted out by up to `shift` and tilted by up to `tilt` (rad); `accent` a strip down the middle column, `seam`
            /// a dark panel line across the top row. The impostor keeps a single slab (a big plate only); the reduced tier merges
            /// the tiles in pairs.
            /// </summary>
            public void Tiles(in WeaponPaint face,Matrix4x4 at,float w,float h,float t,int cols,int rows,float drop=0,float gap=.006f,float shift=.004f,float tilt=.06f,float bevel=.006f,WeaponPaint? accent=null,bool seam=false)
            {
                var fp=face;
                if(Far){if(w<.09f&&h<.09f)return;Prim(()=>{Mirror(()=>{S.Push(at);S.Box(fp,Vector3.Zero,new Vector3(w,h,t));S.Pop();});Occ(Vector3.Transform(Vector3.Zero,at),Math.Min(w,h)*.45f);});return;}
                if(!Fine){cols=Math.Max(1,(cols+1)/2);rows=Math.Max(1,(rows+1)/2);tilt*=.6f;}
                float tw=(w-gap*(cols-1))/cols,th=(h-gap*(rows-1))/rows;int salt=prim*11+3,kept=0,total=cols*rows;float bv=Fine&&tw>.07f&&th>.07f?bevel:0;var acc=accent;
                for(int r=0;r<rows;r++)for(int q=0;q<cols;q++)
                {
                    int id=r*cols+q;bool last=kept==0&&id==total-1;if(!last&&H(id,salt)<drop)continue;kept++;
                    var cen=new Vector3(-w*.5f+tw*.5f+q*(tw+gap),-h*.5f+th*.5f+r*(th+gap),shift*(H(id,salt+1)-.25f));
                    float rx=tilt*(H(id,salt+2)-.5f),ry=tilt*(H(id,salt+3)-.5f),rz=tilt*.5f*(H(id,salt+4)-.5f);
                    var tm=Matrix4x4.CreateRotationX(rx)*Matrix4x4.CreateRotationY(ry)*Matrix4x4.CreateRotationZ(rz)*Matrix4x4.CreateTranslation(cen)*at;
                    Prim(()=>
                    {
                        Mirror(()=>
                        {
                            S.Push(tm);S.Box(fp,Vector3.Zero,new Vector3(tw,th,t),bv);
                            if(acc!=null&&q==cols/2)S.Box(acc.Value,new Vector3(0,0,t*.5f+.0015f),new Vector3(Math.Min(.012f,tw*.18f),th*.8f,.004f));
                            if(seam&&Fine&&r==rows-1&&th>.04f)S.Box(SeamDark,new Vector3(0,th*.22f,t*.5f+.0008f),new Vector3(tw*.82f,.0035f,.003f));
                            S.Pop();
                        });
                        Occ(Vector3.Transform(Vector3.Zero,tm),Math.Min(tw,th)*.45f);
                    });
                }
            }
            /// <summary>A rag of block strips hanging from `top` along `down` (bent out by `bend`), narrowing in steps, swaying from
            /// its root (a dangling part: Full and Reduced).</summary>
            public void Strand(in WeaponPaint paint,Vector3 top,Vector3 down,Vector3 outward,float len,float w,float bend=.15f,int steps=3)
            {
                if(Far)return;if(!Fine)steps=Math.Max(2,steps-1);down=Vector3.Normalize(down);outward=Vector3.Normalize(outward);var pp=paint;var t0=top;float L=len;
                Piece(0,q=>Vector3.Distance(q,t0)/L,()=>
                {
                    for(int i=0;i<steps;i++)
                    {
                        float a=i/(float)steps,b=(i+1)/(float)steps,seg=len/steps;var c=top+down*len*(a+b)*.5f+outward*len*bend*a*a;
                        float ww=w*(1-.55f*a)*(1+.2f*(H(i,0xc1)-.5f));var at=PlateAt(c,outward,-down);var off=new Vector3((H(i,0xc2)-.5f)*w*.3f,0,0);
                        Prim(()=>{Mirror(()=>{S.Push(Matrix4x4.CreateTranslation(off)*at);S.Box(pp,Vector3.Zero,new Vector3(ww,seg*1.08f,.008f));S.Pop();});});
                    }
                });
            }
            /// <summary>Exposed wiring out of broken armour: a sagging chain of small blocks from a to b, its far end dangling.</summary>
            public void Chain(in WeaponPaint paint,Vector3 a,Vector3 b,float sag,float size=.009f)
            {
                if(!Mid)return;int n=Fine?6:3;var pp=paint;var a0=a;float L=Vector3.Distance(a,b)+sag;
                Piece(0,q=>Vector3.Distance(q,a0)/L,()=>
                {
                    for(int i=0;i<=n;i++){float t=i/(float)n;var c=Vector3.Lerp(a,b,t)-Vector3.UnitY*sag*4*t*(1-t);var s=new Vector3(size,size,size)*(i==n?1.3f:1);var cc=c;
                        Prim(()=>Mirror(()=>S.Box(i==n?Copper:pp,cc,s,0)));}
                });
            }
            /// <summary>
            /// A stepped glowing crack across a face: `steps` axis-aligned blocks from `start` along `along`, each step shifted
            /// sideways by a cell (`across`), a dark split under a core (`split` false for a vein: the core only); `n` the face's
            /// outward normal. On the Siren's seven since 2026-10-19 a split's core is raw muscle and a vein's dead and dark (Seam);
            /// the zone kinds keep their glowing seams (heat, embers).
            /// </summary>
            public void Zig(Vector3 start,Vector3 along,Vector3 n,int steps,float len,float width,bool glow,float k=1,bool split=true)
            {
                if(!Mid)return;if(!Fine)steps=Math.Max(2,steps*2/3);along=Vector3.Normalize(along);n=Vector3.Normalize(n);var across=Vector3.Normalize(Vector3.Cross(n,along));
                var p=start;var g=glow?(Standard&&split?RawFlesh:Seam(k)):SeamDark;
                for(int i=0;i<steps;i++)
                {
                    var c=p+along*len*.5f;var at=PlateAt(c,n,along);var core=g;
                    bool fine=Fine&&split;Prim(()=>Mirror(()=>{S.Push(at);if(fine)S.Box(Socket,new Vector3(0,0,-.002f),new Vector3(width*1.8f,len+width,.008f));S.Box(core,new Vector3(0,0,.0015f),new Vector3(width*.8f,len+width*.6f,.006f));S.Pop();}));
                    p+=along*len+across*width*1.2f*(H(i,0xd7+(int)(len*1000))<.5f?-1:1);
                }
            }
            private static WeaponPaint SeamDark=>new WeaponPaint(WeaponPaint.Linear(0x101215),Vector3.Zero,0,SurfMatte);
            private static WeaponPaint RawFlesh=>new WeaponPaint(WeaponPaint.Linear(0x7a1e22),Vector3.Zero,0,SurfFlesh);
            private static WeaponPaint Socket=>new WeaponPaint(WeaponPaint.Linear(0x0b0607),Vector3.Zero,0,SurfMatte);
            private static WeaponPaint Copper=>new WeaponPaint(WeaponPaint.Linear(0xb36b3c),Vector3.Zero,0,SurfWire);

            // ================================================================================================================
            // Finish: occlusion, the block shade, grime and blood, regions
            // ================================================================================================================
            // Piece leaves the sway as half its weight over the variant (Finish packs it).
            private static float Sway(float y)=>Math.Max(0,Math.Min(1,(y-(float)Math.Floor(y+1e-4f))*2));
            private static Vector3 RestAt(int bone){var p=Vector3.Zero;for(int j=bone;j>=0;j=CapturedRig.Bones[j].Parent)p+=CapturedRig.Mirror(CapturedRig.Bones[j].Position);return p;}
            private static bool Near(int a,int b)=>a==b||CapturedRig.Bones[a].Parent==b||CapturedRig.Bones[b].Parent==a;
            /// <summary>Each block's shade step reach by surface class (a step per block, so a part of many blocks mottles in blocks).</summary>
            private static float Step(int cls)=>cls==SurfSkin?.09f:cls==SurfFlesh?.07f:cls==SurfBone?.06f:cls==SurfCloth?.05f:cls==SurfShell?.035f:cls==SurfMatte||cls==SurfWire?.03f:0;
            /// <summary>Bakes the occlusion into Data.x, the block shade, grime and blood into the colours and the regions into the alpha.</summary>
            public void Finish(float strength=.95f,bool grime=true,bool regions=true)
            {
                var meshes=SplitJaw?new[]{Mesh,JawMesh}:new[]{Mesh};var rest=new Vector3[CapturedRig.Bones.Length];for(int b=0;b<rest.Length;b++)rest[b]=RestAt(b);
                var prims=new Dictionary<WeaponSection,int[]>();
                foreach(var sp in spans){if(!prims.TryGetValue(sp.Sec,out var arr)){arr=new int[0];}if(arr.Length<sp.Sec.Positions.Count)Array.Resize(ref arr,sp.Sec.Positions.Count);for(int i=sp.From;i<sp.To&&i<arr.Length;i++)if(arr[i]==0)arr[i]=sp.Prim;prims[sp.Sec]=arr;}
                var list=new List<(Vector3 c,float r,float w,int prim)>();
                foreach(var mesh in meshes)foreach(var s in mesh.Sections)
                {
                    if(s.Data.Count!=s.Positions.Count)continue;list.Clear();
                    foreach(var o in occluders){if(!Near(o.Bone,s.Bone))continue;bool own=o.Bone==s.Bone;list.Add((o.C+rest[o.Bone]-rest[s.Bone],o.R,own?1:.7f,own?o.Prim:-1));}
                    prims.TryGetValue(s,out var pr);
                    for(int i=0;i<s.Positions.Count;i++)
                    {
                        var p=s.Positions[i];var n=s.Normals[i];int mine=pr!=null&&i<pr.Length?pr[i]:0;float ao=1;
                        foreach(var (c,r,w,id) in list){if(id==mine&&mine!=0)continue;var d=c-p;float l=d.Length();if(l<1e-4f)continue;float ndl=Vector3.Dot(n,d)/l;if(ndl<=0)continue;ao*=1-Math.Min(1,ndl*Math.Min(1,r*r/(l*l))*w*strength);}
                        // A grid's own corner occlusion rides in Data.x's fraction (WeaponSculpt.QuadData).
                        var dd=s.Data[i];float cls=(float)Math.Floor(dd.X+1e-4f),own=Math.Max(0,dd.X-cls);ao*=1-own;
                        float occl=Math.Max(0,Math.Min(.85f,1-ao));s.Data[i]=new Vector2(Pack(cls,occl),Pack((float)Math.Floor(dd.Y+1e-4f),Sway(dd.Y)));
                    }
                    float region=!regions?RegionNone:s.Bone==CapturedRig.Head?RegionHead:s.Bone==CapturedRig.Pelvis||s.Bone==CapturedRig.Spine||s.Bone==CapturedRig.Chest?RegionTorso:
                        s.Bone==CapturedRig.LArm||s.Bone==CapturedRig.RArm||s.Bone==CapturedRig.LElbow||s.Bone==CapturedRig.RElbow||s.Bone==CapturedRig.LHip||s.Bone==CapturedRig.RHip||s.Bone==CapturedRig.LKnee||s.Bone==CapturedRig.RKnee?RegionLimb:RegionNone;
                    for(int i=0;i<s.Colors.Count;i++)
                    {
                        var c=s.Colors[i];var e=s.Emission[i];var col=new Vector3(c.X,c.Y,c.Z);
                        if(grime&&e.W<2.5f&&e.X+e.Y+e.Z<=c.X+c.Y+c.Z)
                        {
                            int cls=(int)Math.Floor(s.Data[i].X+1e-4f);var p=s.Positions[i];float g=Grime(s.Bone,p),bl=Blood(s.Bone,p);int pid=pr!=null&&i<pr.Length?pr[i]:0;
                            if(cls==SurfShell||cls==SurfWire){g*=.6f;bl*=.4f;}else if(cls==SurfMatte){g*=.3f;bl*=.3f;}
                            // A shade step per block (a grid's cells carry their own): the blocky mottling of voxel art.
                            if(pid!=0&&!voxelPrims.Contains(pid)){int lv=(int)Math.Min(3,H(pid,0x9a)*4);col*=1+Step(cls)*(lv-1.5f)/1.5f;}
                            // The big kinds' hide is darker and greener: their mass reads heavy, not chalky, under a key light.
                            if(cls==SurfSkin){if(Kind==NativeHordeKinds.Hulk)col*=new Vector3(.74f,.8f,.72f);else if(Kind==NativeHordeKinds.Brute)col*=new Vector3(.86f,.9f,.84f);}
                            // Grime and blood in steps, so a block darkens by a step rather than a smooth wash.
                            g=(float)Math.Round(g*4)/4;bl=(float)Math.Round(bl*4)/4;
                            col=Vector3.Lerp(col,col*new Vector3(.42f,.36f,.3f),g);col=Vector3.Lerp(col,new Vector3(.16f,.024f,.03f),bl);
                        }
                        s.Colors[i]=new Vector4(col,region);
                    }
                }
            }
            /// <summary>Grime toward the hands and feet (0..1, by joint and the point's depth down the limb).</summary>
            private float Grime(int bone,Vector3 p)
            {
                float down=Math.Max(0,Math.Min(1,-p.Y/.25f));
                switch(bone)
                {
                    case CapturedRig.LHand:case CapturedRig.RHand:return .45f+.3f*Math.Max(0,Math.Min(1,(-p.Y-.06f)/.08f));
                    case CapturedRig.LElbow:case CapturedRig.RElbow:return .06f+.4f*down*down;
                    case CapturedRig.LKnee:case CapturedRig.RKnee:return .08f+.45f*down;
                    case CapturedRig.LFoot:case CapturedRig.RFoot:case CapturedRig.LToe:case CapturedRig.RToe:return .62f;
                    case CapturedRig.LHip:case CapturedRig.RHip:return .06f+.08f*down;
                    case CapturedRig.Pelvis:return .1f;
                    default:return 0;
                }
            }
            /// <summary>Dried blood round the mouth, down the chin and the chest's front, and on the hands.</summary>
            private float Blood(int bone,Vector3 p)
            {
                if(bone==CapturedRig.Head)
                {
                    float y0=.075f+HeadDrop[Math.Min(HeadDrop.Length-1,NativeHordeKinds.Base(Kind))];var d=new Vector3(p.X*1.3f,p.Y-y0,Math.Max(0,.14f-p.Z)*2);float m=(float)Math.Exp(-d.LengthSquared()/(.055f*.055f));
                    return p.Z>.04f?.55f*m:0;
                }
                if(bone==CapturedRig.Chest&&p.Z>.05f&&p.Y>.12f){float m=(float)Math.Exp(-(p.X*p.X)/(.06f*.06f))*Math.Min(1,(p.Y-.12f)/.15f);return .14f*m;}
                if(bone==CapturedRig.LHand||bone==CapturedRig.RHand)return p.Y<-.1f?.35f:0;
                return 0;
            }
        }
    }
}
