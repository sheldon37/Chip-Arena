using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The organic kit the Siren's zombies are sculpted with (owner 2026-10-17: "fix the zombies animations (Design as well, it
    // seems they are still on the lower quality end) Improve these vastly"; docs/ZOMBIE-DESIGN.md). Presentation only, engine-free
    // and deterministic, built once when the bank loads. On top of NativeHordeModels' box kit it adds:
    //  - smooth surfaces over grids (Grid): muscled limbs with a profile of rings (Limb), sculpted (super)ellipsoid masses with a
    //    displacement (Mass: the skull's brow, sockets, cheekbones and nose), curved armour shells with chipped, broken edges,
    //    dents and panel seams (Shell), tubes for fingers, ribs, cables and tendons (Tube), strips laid on a surface for the ink
    //    veins, seams and cracks (Strip) and torn hanging tatters (Tatter);
    //  - per-vertex shading data in WeaponSection.Data (uv0 on the mesh) for ChipArena/ZombieLit: x the surface class
    //    (Surf*: skin, wet flesh, armour shell, cloth, bone, matte, wire) plus .02 + .96 x the baked occlusion, y the missing-piece
    //    variant (1..4, hidden per add) plus .02 + .96 x the sway weight of a dangling part (K.Pack); the vertex colour's alpha names the
    //    proportion region (head, torso width, limb girth) the shader scales per add;
    //  - Finish: the occlusion bake (each part's occluder spheres darken the vertices of the others on its joint and, at .7, on
    //    the neighbouring joints at rest: armour over skin, the mouth, the sockets, between the fingers, the crotch), grime and
    //    blood toward the hands, the feet and the mouth, the regions;
    //  - Variation: the add's own tone, mottling seed and missing pieces by id, as the per-instance body tint (white = the plain
    //    model, so every other draw of these models is unchanged).
    // Every vertex stays inside the authority's capsule with the per-add variation and sway at their largest
    // (Tests/HordeKindChecks); the tiers thin the rings, the sides and the details.
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
        /// <summary>A point and normal on a surface at (u, v) (veins and seams are laid on it).</summary>
        private delegate void SurfEval(float u,float v,out Vector3 p,out Vector3 n);

        private sealed partial class K
        {
            /// <summary>The joint being drawn (On sets it).</summary>
            public int Bone;
            private WeaponMesh CurMesh=>SplitJaw&&S==JawSculpt?JawMesh:Mesh;
            private WeaponSection Sec=>CurMesh.Section(Bone,WeaponStretch.None);
            private struct Occluder{public WeaponMesh Mesh;public int Bone,Prim;public Vector3 C;public float R;}
            private struct PrimSpan{public WeaponSection Sec;public int From,To,Prim;}
            private readonly List<Occluder> occluders=new List<Occluder>();private readonly List<PrimSpan> spans=new List<PrimSpan>();private int prim,curPrim;
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
            // ---- tier thinning ----
            public int Around(int full)=>Fine?full:Mid?Math.Max(6,(full*2+1)/3):Math.Max(4,(full+1)/2);
            private static float[] Rows(float[] prof,int stride,int tier)
            {
                int n=prof.Length/stride;if(tier==Full||n<=3)return prof;var keep=new List<int>{0};
                if(tier==Reduced){for(int i=2;i<n-1;i+=2)keep.Add(i);if(n%2==0&&n>4)keep.Add(n-2);}else if(n>=6)keep.Add(n/2);
                keep.Add(n-1);keep.Sort();var o=new float[keep.Count*stride];for(int i=0;i<keep.Count;i++)Array.Copy(prof,keep[i]*stride,o,i*stride,stride);return o;
            }

            // ================================================================================================================
            // Smooth grids, caps, oriented quads, strips
            // ================================================================================================================
            /// <summary>A smooth surface through a grid of points (rows along v, columns along u, u x v facing out; `ring` joins the
            /// last column to the first). paint(r, c) paints quad (r, c); null leaves it out (a tear).</summary>
            public void Grid(Vector3[][] p,bool ring,Func<int,int,WeaponPaint?> paint,bool blend=true)
            {
                int R=p.Length,C=p[0].Length,qc=ring?C:C-1;var n=new Vector3[R,C];
                for(int r=0;r<R-1;r++)for(int c=0;c<qc;c++){int c1=(c+1)%C;var f=Vector3.Cross(p[r+1][c1]-p[r][c],p[r+1][c]-p[r][c1]);n[r,c]+=f;n[r,c1]+=f;n[r+1,c1]+=f;n[r+1,c]+=f;}
                for(int r=0;r<R;r++)
                {
                    bool pole=true;for(int c=1;c<C&&pole;c++)if(Vector3.DistanceSquared(p[r][c],p[r][0])>1e-10f)pole=false;
                    if(pole){var s=Vector3.Zero;for(int c=0;c<C;c++)s+=n[r,c];for(int c=0;c<C;c++)n[r,c]=s;}
                }
                // Each quad keeps its paint's class, emission and tag; its corners take the albedo of the quads round them (glows
                // aside), so painted regions blend over one quad instead of stepping at the grid.
                var q=new WeaponPaint?[R-1,qc];var col=new Vector3[R,C];var cnt=new float[R,C];
                for(int r=0;r<R-1;r++)for(int c=0;c<qc;c++){var pp=paint(r,c);q[r,c]=pp;if(pp==null||!pp.Value.Lit)continue;int c1=(c+1)%C;foreach(var (rr,cc) in new[]{(r,c),(r,c1),(r+1,c1),(r+1,c)}){col[rr,cc]+=pp.Value.Color;cnt[rr,cc]++;}}
                Mirror(()=>
                {
                    for(int r=0;r<R-1;r++)for(int c=0;c<qc;c++)
                    {
                        var pp=q[r,c];if(pp==null)continue;int c1=(c+1)%C;var v=pp.Value;
                        if(!v.Lit||!blend){S.SmoothQuad(v,p[r][c],p[r][c1],p[r+1][c1],p[r+1][c],n[r,c],n[r,c1],n[r+1,c1],n[r+1,c]);continue;}
                        Vector3 A(int rr,int cc)=>cnt[rr,cc]>0?col[rr,cc]/cnt[rr,cc]:v.Color;
                        S.SmoothQuad(v,p[r][c],p[r][c1],p[r+1][c1],p[r+1][c],n[r,c],n[r,c1],n[r+1,c1],n[r+1,c],A(r,c),A(r,c1),A(r+1,c1),A(r+1,c));
                    }
                });
            }
            /// <summary>A flat fan closing a ring about its centre, facing `outward`.</summary>
            public void Cap(in WeaponPaint paint,Vector3[] ring,Vector3 outward)
            {
                var c=Vector3.Zero;foreach(var q in ring)c+=q;c/=ring.Length;var pp=paint;int n=ring.Length;
                Mirror(()=>{for(int i=0;i<n;i++){var a=ring[i];var b=ring[(i+1)%n];if(Vector3.Dot(Vector3.Cross(a-c,b-c),outward)>=0)S.Tri(pp,c,a,b);else S.Tri(pp,c,b,a);}});
            }
            /// <summary>A flat quad facing along `front` (left-authored).</summary>
            public void Facing(in WeaponPaint paint,Vector3 a,Vector3 b,Vector3 d,Vector3 e,Vector3 front)
            {
                if(Vector3.Dot(Vector3.Cross(d-a,e-b),front)<0){var t=b;b=e;e=t;}var pp=paint;Mirror(()=>S.Quad(pp,a,b,d,e));
            }
            /// <summary>A ribbon along points with their surface normals, `width` across (tapering by `taper` to the end), lifted
            /// `lift` off the surface (veins, seams, cracks, accent strips).</summary>
            public void Strip(in WeaponPaint paint,Vector3[] pts,Vector3[] nrm,float width,float taper=1,float lift=.0015f)
            {
                int n=pts.Length;if(n<2)return;var l=new Vector3[n];var r=new Vector3[n];
                for(int i=0;i<n;i++)
                {
                    var t=i==0?pts[1]-pts[0]:i==n-1?pts[n-1]-pts[n-2]:pts[i+1]-pts[i-1];var nn=Vector3.Normalize(nrm[i]);var side=Vector3.Cross(nn,t);float sl=side.Length();if(sl<1e-7f)side=Vector3.UnitX;else side/=sl;
                    float w=width*.5f*(1-(1-taper)*i/(n-1f));var o=pts[i]+nn*lift;l[i]=o-side*w;r[i]=o+side*w;
                }
                for(int i=0;i<n-1;i++)Facing(paint,l[i],r[i],r[i+1],l[i+1],nrm[i]+nrm[i+1]);
            }
            /// <summary>A strip laid on a surface along a path of (u, v) points.</summary>
            public void StripOn(in WeaponPaint paint,SurfEval s,(float u,float v)[] path,float width,float taper=1,float lift=.0015f)
            {
                var p=new Vector3[path.Length];var n=new Vector3[path.Length];for(int i=0;i<path.Length;i++)s(path[i].u,path[i].v,out p[i],out n[i]);Strip(paint,p,n,width,taper,lift);
            }
            /// <summary>An ink vein on a surface: a wandering, branching glowing strip from (u, v) heading `dir` (in u, v units),
            /// each branch its own flicker cell (NativeMinionFx glow code).</summary>
            public void Vein(SurfEval s,float u,float v,float du,float dv,int steps,float width,float k=1,int branches=1,int salt=0)
            {
                if(Far)return;if(!Fine){steps=Math.Max(2,steps*2/3);branches=0;}
                var path=new (float,float)[steps+1];float a=u,b=v;
                for(int i=0;i<=steps;i++){path[i]=(a,b);float w=(H(i,0x91+salt)-.5f)*1.1f;float cu=du*(float)Math.Cos(w)-dv*(float)Math.Sin(w)*.5f,cv=dv*(float)Math.Cos(w)+du*(float)Math.Sin(w)*.5f;a+=cu;b+=cv;}
                StripOn(Seam(k),s,path,width,.35f,.0018f);
                for(int j=0;j<branches;j++){int at=1+(int)(H(j,0x92+salt)*(steps-1));var (bu,bv)=path[at];float sgn=j%2==0?1:-1;Vein(s,bu,bv,du*.6f-dv*sgn*.8f,dv*.6f+du*sgn*.8f,Math.Max(2,steps/2),width*.7f,k*.85f,0,salt+17+j);}
            }

            // ================================================================================================================
            // Limbs and tubes
            // ================================================================================================================
            /// <summary>
            /// A muscled limb from a to b (joint frame, left-authored): rings across the axis (`across` its x, by default the joint's
            /// x) from a profile of rows (u along, rx, rz, offset across, offset forward), smooth; paint(row, col) per quad (null a
            /// tear); `capA` / `capB` close the ends (a stump's wound where a severed joint shows it). Returns the surface (u along,
            /// v around 0..1 from +across toward +forward) for veins. Occluder spheres along the axis.
            /// </summary>
            public SurfEval Limb(Vector3 a,Vector3 b,float[] prof,Func<int,int,WeaponPaint?> paint,WeaponPaint? capA=null,WeaponPaint? capB=null,int sides=10,Vector3? across=null,bool occlude=true)
            {
                var along=Vector3.Normalize(b-a);var x=across??Vector3.UnitX;x-=along*Vector3.Dot(x,along);x=Vector3.Normalize(x);var z=Vector3.Cross(along,x);
                var rows=Rows(prof,5,Tier);int R=rows.Length/5,C=Far?5:Fine?sides:Math.Max(6,sides*3/5);
                Vector3 Centre(float u,float ox,float oz)=>a+(b-a)*u+x*ox+z*oz;
                var pts=new Vector3[R][];
                for(int r=0;r<R;r++){float u=rows[r*5],rx=rows[r*5+1],rz=rows[r*5+2];var c=Centre(u,rows[r*5+3],rows[r*5+4]);pts[r]=new Vector3[C];for(int q=0;q<C;q++){double t=Math.PI*2*q/C;pts[r][q]=c+x*(float)Math.Cos(t)*rx+z*(float)Math.Sin(t)*rz;}}
                Prim(()=>
                {
                    var orig=new int[R];for(int r=0;r<R;r++){int o=0;for(int i=0;i<prof.Length/5;i++)if(Math.Abs(prof[i*5]-rows[r*5])<1e-6f){o=i;break;}orig[r]=Math.Min(prof.Length/5-2,o);}
                    Grid(pts,true,(r,q)=>paint(orig[r],q*sides/C));
                    if(capA!=null&&!Far)Cap(capA.Value,pts[0],-along);if(capB!=null&&!Far)Cap(capB.Value,pts[R-1],along);
                    if(occlude)for(int r=0;r<R;r++){float rr=(rows[r*5+1]+rows[r*5+2])*.45f;Occ(Centre(rows[r*5],rows[r*5+3],rows[r*5+4]),rr);}
                });
                // The full profile for the surface (veins follow the true shape on every tier).
                return (float u,float v,out Vector3 p,out Vector3 n)=>
                {
                    int m=prof.Length/5;float uu=Math.Max(0,Math.Min(1,u));int i=0;while(i<m-2&&prof[(i+1)*5]<uu)i++;float f=(uu-prof[i*5])/Math.Max(1e-5f,prof[(i+1)*5]-prof[i*5]);f=Math.Max(0,Math.Min(1,f));
                    float L(int k)=>prof[i*5+k]+(prof[(i+1)*5+k]-prof[i*5+k])*f;double t=Math.PI*2*v;float cs=(float)Math.Cos(t),sn=(float)Math.Sin(t);
                    var c=Centre(uu,L(3),L(4));p=c+x*cs*L(1)+z*sn*L(2);n=Vector3.Normalize(x*cs/Math.Max(1e-4f,L(1))+z*sn/Math.Max(1e-4f,L(2)));
                };
            }
            /// <summary>A limb painted one paint (with optional stump caps).</summary>
            public SurfEval Limb(Vector3 a,Vector3 b,float[] prof,WeaponPaint paint,WeaponPaint? capA=null,WeaponPaint? capB=null,int sides=10,Vector3? across=null)
            {var p=paint;return Limb(a,b,prof,(r,c)=>p,capA,capB,sides,across);}
            /// <summary>A smooth tube along a polyline (radius per point), `sides` around (fingers, ribs, cables, tendons); the end
            /// capped (a fingertip) unless `open`.</summary>
            public void Tube(in WeaponPaint paint,Vector3[] path,float[] radius,int sides,bool capEnd=true,bool capStart=false,float squash=1,bool occlude=false)
            {
                if(Far)return;int n=path.Length,C=Fine?sides:Math.Max(3,sides-2);var pts=new Vector3[n][];Vector3 normal=Vector3.Zero;
                for(int i=0;i<n;i++)
                {
                    var t=i==0?path[1]-path[0]:i==n-1?path[n-1]-path[n-2]:path[i+1]-path[i-1];t=Vector3.Normalize(t);
                    if(i==0){var up=Math.Abs(t.Y)>.9f?Vector3.UnitZ:Vector3.UnitY;normal=Vector3.Normalize(up-t*Vector3.Dot(up,t));}else{normal-=t*Vector3.Dot(normal,t);normal=Vector3.Normalize(normal);}
                    var bi=Vector3.Cross(t,normal);pts[i]=new Vector3[C];
                    // Around from the normal toward the binormal, so u x v faces out (rows run along the path).
                    for(int q=0;q<C;q++){double a=Math.PI*2*q/C;pts[i][q]=path[i]+(normal*(float)Math.Cos(a)*squash+bi*(float)Math.Sin(a))*radius[i];}
                }
                var pp=paint;
                Prim(()=>
                {
                    Grid(pts,true,(r,q)=>pp);
                    if(capEnd)Cap(pp,pts[n-1],path[n-1]-path[n-2]);if(capStart)Cap(pp,pts[0],path[0]-path[1]);
                    if(occlude)for(int i=0;i<n;i++)Occ(path[i],radius[i]*.9f);
                });
            }

            // ================================================================================================================
            // Masses: (super)ellipsoids with a displacement
            // ================================================================================================================
            /// <summary>
            /// A smooth (super)ellipsoid at c with radii r (exponent `pw`: 2 round, ~2.6 the pilots' rounded-cube skull), turned by
            /// `turn` about its centre, its points pushed out along the radial direction by `disp`(unit point, joint-frame point);
            /// paint(row, col) per quad (rows from the top pole down, columns round from +x toward +z). `rows`/`cols` its Full grid.
            /// Returns the surface (u from the top 0 to the bottom 1, v around 0..1) for veins.
            /// </summary>
            public SurfEval Mass(Func<int,int,WeaponPaint?> paint,Vector3 c,Vector3 r,float pw=2,Matrix4x4? turn=null,int rows=8,int cols=12,Func<Vector3,Vector3,float> disp=null,bool occlude=true)
            {
                int R=Far?3:Fine?rows:Math.Max(4,rows*2/3),C=Far?Math.Max(4,cols/2):Fine?cols:Math.Max(6,cols*2/3);var m=turn??Matrix4x4.Identity;float big=Math.Max(r.X,Math.Max(r.Y,r.Z));
                bool skip=Far&&big<.045f||!Fine&&big<.013f;
                Vector3 At(float u,float v)
                {
                    double ph=Math.PI*u,th=Math.PI*2*v;var d=new Vector3((float)(Math.Sin(ph)*Math.Cos(th)),(float)Math.Cos(ph),(float)(Math.Sin(ph)*Math.Sin(th)));
                    float s=pw==2?1:(float)Math.Pow(Math.Pow(Math.Abs(d.X),pw)+Math.Pow(Math.Abs(d.Y),pw)+Math.Pow(Math.Abs(d.Z),pw),1/pw);var q=d/Math.Max(1e-6f,s);
                    var local=q*r;var p=c+Vector3.TransformNormal(local,m);if(disp!=null){float k=disp(q,p);if(k!=0)p+=Vector3.TransformNormal(Vector3.Normalize(local),m)*k;}return p;
                }
                var pts=new Vector3[R][];for(int i=0;i<R;i++){pts[i]=new Vector3[C];for(int q=0;q<C;q++)pts[i][q]=At(i/(R-1f),q/(float)C);}
                if(!skip)Prim(()=>
                {
                    Grid(pts,true,(i,q)=>paint(i*(rows-1)/Math.Max(1,R-1),q*cols/C));
                    if(occlude){float mn=Math.Min(r.X,Math.Min(r.Y,r.Z)),mx=Math.Max(r.X,Math.Max(r.Y,r.Z));
                        if(mx<mn*1.6f)Occ(c,(r.X+r.Y+r.Z)/3*.95f);
                        else{var axis=r.X==mx?Vector3.UnitX:r.Y==mx?Vector3.UnitY:Vector3.UnitZ;axis=Vector3.TransformNormal(axis,m);for(int i=-1;i<=1;i++)Occ(c+axis*(mx-mn)*i*.7f,mn*1.05f);}}
                });
                return (float u,float v,out Vector3 p,out Vector3 n)=>{p=At(u,v);var a=At(u+.01f,v);var b=At(u,v+.01f);n=Vector3.Cross(b-p,a-p);n=n.LengthSquared()<1e-14f?Vector3.Normalize(p-c):Vector3.Normalize(n);};
            }
            public SurfEval Mass(WeaponPaint paint,Vector3 c,Vector3 r,float pw=2,Matrix4x4? turn=null,int rows=8,int cols=12,bool occlude=true){var p=paint;return Mass((i,q)=>p,c,r,pw,turn,rows,cols,null,occlude);}

            // ================================================================================================================
            // Armour: curved shells with broken, chipped edges
            // ================================================================================================================
            /// <summary>
            /// A plate of the pilot's armour: the part of a (super)ellipsoid shell (centre c, radii r, turned by `turn`) between
            /// angles th0..th1 round +y (from +x toward +z) and heights y1 (top) .. y0 (bottom) as the unit sphere's y, `thick`
            /// deep. `broken` edges (1 top, 2 bottom, 4 the th0 side, 8 the th1 side) are jagged by `jag` (in the angle or height
            /// units) and their walls show chipped metal (`edge`); the face is dented a little. Returns the outer surface (u across
            /// th0..th1, v from the top 0 to the bottom 1) for seams and cracks. The impostor keeps only a `big` plate, one-sided.
            /// </summary>
            public SurfEval Shell(in WeaponPaint face,in WeaponPaint edge,Vector3 c,Vector3 r,float th0,float th1,float y0,float y1,float thick,int broken=0,float jag=.12f,Matrix4x4? turn=null,float pw=2.3f,int cols=7,int rows=5,bool big=false,float dent=.0025f)
            {
                var m=turn??Matrix4x4.Identity;int C=Far?2:Fine?cols:Math.Max(3,cols*2/3),Rr=Far?2:Fine?rows:Math.Max(3,rows*2/3);
                float Th0(int i)=>th0+((broken&4)!=0?jag*(H(i,0xa1)-.15f):0);float Th1(int i)=>th1-((broken&8)!=0?jag*(H(i,0xa2)-.15f):0);
                float Y1(int i)=>y1-((broken&1)!=0?jag*(H(i,0xa3)-.15f):0);float Y0(int i)=>y0+((broken&2)!=0?jag*(H(i,0xa4)-.15f):0);
                Vector3 Unit(float th,float y){y=Math.Max(-.999f,Math.Min(.999f,y));float s=(float)Math.Sqrt(1-y*y);var d=new Vector3(s*(float)Math.Cos(th),y,s*(float)Math.Sin(th));
                    float k=(float)Math.Pow(Math.Pow(Math.Abs(d.X),pw)+Math.Pow(Math.Abs(d.Y),pw)+Math.Pow(Math.Abs(d.Z),pw),1/pw);return d/Math.Max(1e-6f,k);}
                Vector3 Point(float th,float y,float inset,float dn)
                {
                    var q=Unit(th,y);var local=q*r;var dir=Vector3.Normalize(new Vector3(q.X/r.X,q.Y/r.Y,q.Z/r.Z));local+=dir*(dn-inset);return c+Vector3.TransformNormal(local,m);
                }
                float Dent(int i,int j)=>Fine?dent*(H(i*31+j,0xa5)-.5f)*2:0;
                (float th,float y) Param(int i,int j){float u=j/(C-1f),v=i/(Rr-1f);float t0=Th0(i),t1=Th1(i);float yy1=Y1(j),yy0=Y0(j);return (t0+(t1-t0)*u,yy1+(yy0-yy1)*v);}
                var outer=new Vector3[Rr][];var inner=new Vector3[Rr][];
                for(int i=0;i<Rr;i++){outer[i]=new Vector3[C];inner[i]=new Vector3[C];for(int j=0;j<C;j++){var (th,y)=Param(i,j);outer[i][j]=Point(th,y,0,Dent(i,j));inner[i][C-1-j]=Point(th,y,thick,0);}}
                var f=face;var e=edge;
                Prim(()=>
                {
                    Grid(outer,false,(i,j)=>f);
                    if(Far&&!big)return;
                    if(!Far)
                    {
                        Grid(inner,false,(i,j)=>e.Scaled(.55f));
                        // The walls round the boundary: chipped metal where the plate broke, its face colour elsewhere.
                        var mid=(outer[Rr/2][C/2]+inner[Rr/2][C/2])*.5f;
                        void Wall(Vector3 a,Vector3 b,Vector3 ai,Vector3 bi,bool chip){var mm=(a+b+ai+bi)*.25f;Facing(chip?e:f.Scaled(.8f),a,b,bi,ai,mm-mid);}
                        for(int j=0;j<C-1;j++){Wall(outer[0][j],outer[0][j+1],inner[0][C-1-j],inner[0][C-2-j],(broken&1)!=0);Wall(outer[Rr-1][j],outer[Rr-1][j+1],inner[Rr-1][C-1-j],inner[Rr-1][C-2-j],(broken&2)!=0);}
                        for(int i=0;i<Rr-1;i++){Wall(outer[i][0],outer[i+1][0],inner[i][C-1],inner[i+1][C-1],(broken&4)!=0);Wall(outer[i][C-1],outer[i+1][C-1],inner[i][0],inner[i+1][0],(broken&8)!=0);}
                    }
                    float mr=(r.X+r.Y+r.Z)/3;Occ(c,mr*.92f);
                });
                return (float u,float v,out Vector3 p,out Vector3 n)=>
                {
                    float th=th0+(th1-th0)*u,y=y1+(y0-y1)*v;p=Point(th,y,0,0);var a=Point(th+.01f,y,0,0);var b=Point(th,y-.01f,0,0);n=Vector3.Normalize(Vector3.Cross(a-p,b-p));
                    var q=Unit(th,y);var radial=Vector3.TransformNormal(new Vector3(q.X/r.X,q.Y/r.Y,q.Z/r.Z),m);if(Vector3.Dot(n,radial)<0)n=-n;
                };
            }
            /// <summary>
            /// A plate of the pilot's armour as the pilot wears it (Core/NativeBodyModels: chunky chamfered slabs with panel seams and
            /// accent strips), broken: a w x h slab `thick` deep in its frame `at` (front +z), its corners chamfered by `bevel`; corner
            /// `broken` (0 top-right, 1 top-left, 2 bottom-left, 3 bottom-right; -1 whole) bitten away along a jagged line `bite` of the
            /// way along both edges, the break's walls bared metal. `accent` 0 none, 1 a strip across the top, 2 one down the middle,
            /// its paint `accentPaint` (a dead strip or the Siren's flickering ink); `seams` 1 a seam across, 2 one down; `crack` a dark
            /// crack with the ink in it. The impostor keeps only the slab. Registers an occluder over what it covers.
            /// </summary>
            public void Armor(in WeaponPaint face,in WeaponPaint edge,Matrix4x4 at,float w,float h,float thick,int broken=-1,float bite=.45f,int accent=0,WeaponPaint? accentPaint=null,int seams=0,bool crack=false,float bevel=.007f)
            {
                if(Far){if(w<.09f||h<.09f)return;var fp=face;var at0=at;Prim(()=>{Mirror(()=>{S.Push(at0);S.Box(fp,Vector3.Zero,new Vector3(w,h,thick));S.Pop();});Occ(Vector3.Transform(Vector3.Zero,at0),Math.Min(w,h)*.47f);});return;}
                float hw=w*.5f,hh=h*.5f,cc=0;int salt=prim*13+7;
                // The chamfered rectangle, counter-clockwise from the bottom of the right side, with corner k replaced by a jagged break.
                var corners=new[]{new Vector2(hw,hh),new Vector2(-hw,hh),new Vector2(-hw,-hh),new Vector2(hw,-hh)};var pts=new System.Collections.Generic.List<Vector2>();var chip=new System.Collections.Generic.List<bool>();
                for(int q=0;q<4;q++)
                {
                    var cn=corners[q];var prev=corners[(q+3)%4];var next=corners[(q+1)%4];var toPrev=Vector2.Normalize(prev-cn);var toNext=Vector2.Normalize(next-cn);
                    if(q==broken&&!Far)
                    {
                        float la=Vector2.Distance(prev,cn)*bite,lb=Vector2.Distance(next,cn)*bite;var a=cn+toPrev*la;var b=cn+toNext*lb;int n=Fine?5:3;
                        pts.Add(a);chip.Add(true);
                        for(int i=1;i<n;i++){float t=i/(float)n;var m=Vector2.Lerp(a,b,t);var inward=-Vector2.Normalize((toPrev+toNext));float j=(H(i,salt)-.35f)*Math.Min(la,lb)*.5f;pts.Add(m+inward*j);chip.Add(true);}
                        pts.Add(b);chip.Add(false);
                    }
                    else if(cc>0){pts.Add(cn+toPrev*cc);chip.Add(false);pts.Add(cn+toNext*cc);chip.Add(false);}else{pts.Add(cn);chip.Add(false);}
                }
                var outline=new Vector3[pts.Count];for(int i=0;i<outline.Length;i++)outline[i]=new Vector3(pts[i].X,pts[i].Y,0);
                if(Area(outline)<0){Array.Reverse(outline);chip.Reverse();var c2=new bool[chip.Count];for(int i=0;i<c2.Length;i++)c2[i]=chip[(i+1)%c2.Length];chip.Clear();chip.AddRange(c2);}
                var f=face;var e=edge;var back=face.Scaled(.45f);var chips=chip.ToArray();float bv=Fine?bevel:0;var acc=accentPaint??ZDeadAccent;
                Prim(()=>
                {
                    Mirror(()=>
                    {
                        S.Push(at);
                        S.Slab(i=>chips[i%chips.Length]?e:f,f,back,outline,-thick*.5f,thick*.5f,bv);
                        if(Mid)
                        {
                            float z=thick*.5f+.0012f;
                            if(accent==1&&h>.05f)S.Box(acc,new Vector3(0,hh*.62f,z),new Vector3(w*.62f,Math.Min(.012f,h*.12f),.004f));
                            if(accent==2)S.Box(acc,new Vector3(0,0,z),new Vector3(Math.Min(.012f,w*.12f),h*.66f,.004f));
                            if(Fine&&(seams&1)!=0)S.Box(SeamDark,new Vector3(0,-hh*.25f,z-.0006f),new Vector3(w*.84f,.0035f,.003f));
                            if(Fine&&(seams&2)!=0)S.Box(SeamDark,new Vector3(w*.22f,0,z-.0006f),new Vector3(.0035f,h*.8f,.003f));
                        }
                        S.Pop();
                    });
                    if(crack&&Mid)
                    {
                        // A crack from a hashed point on the plate's edge toward its middle, the ink glowing in it.
                        var path=new Vector3[5];float x0=(H(1,salt+1)-.5f)*w*.6f,y0=hh*.95f;for(int i=0;i<5;i++){float t=i/4f;path[i]=new Vector3(x0+(H(i,salt+2)-.5f)*w*.18f-t*x0*.4f,y0-t*h*.6f,thick*.5f);}
                        var nrm=new Vector3[5];for(int i=0;i<5;i++){path[i]=Vector3.Transform(path[i],at);nrm[i]=Vector3.TransformNormal(Vector3.UnitZ,at);}
                        Strip(Socket,path,nrm,.007f,.4f,.0014f);if(Fine)Strip(Seam(1.1f),path,nrm,.0026f,.3f,.002f);
                    }
                    var cen=Vector3.Transform(Vector3.Zero,at);Occ(cen,Math.Min(hw,hh)*.95f);
                });
            }
            private static float Area(Vector3[] o){float a=0;for(int i=0;i<o.Length;i++){var p=o[i];var q=o[(i+1)%o.Length];a+=p.X*q.Y-q.X*p.Y;}return a;}
            /// <summary>An armour plate's frame: its centre and the direction its face looks (`up` the plate's +y), rolled by `roll`.</summary>
            public static Matrix4x4 PlateAt(Vector3 centre,Vector3 face,Vector3 up,float roll=0)
            {
                var z=Vector3.Normalize(face);var x=Vector3.Normalize(Vector3.Cross(up,z));var y=Vector3.Cross(z,x);
                var m=new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);if(roll!=0)m=Matrix4x4.CreateRotationZ(roll)*m;m.M41=centre.X;m.M42=centre.Y;m.M43=centre.Z;return m;
            }
            /// <summary>A crack across a plate or hide: a dark jagged strip, the Siren's ink glowing in its middle where `glow`.</summary>
            public void Crack(SurfEval s,float u,float v,float du,float dv,int steps,float width,bool glow,float k=1)
            {
                if(!Mid)return;var path=new (float,float)[steps+1];float a=u,b=v;
                for(int i=0;i<=steps;i++){path[i]=(a,b);float w=(H(i,0xb1+(int)(u*97))-.5f)*1.4f;a+=du*(float)Math.Cos(w)-dv*(float)Math.Sin(w);b+=dv*(float)Math.Cos(w)+du*(float)Math.Sin(w);}
                StripOn(Socket,s,path,width,.4f,.0012f);if(glow&&Fine)StripOn(Seam(k),s,path,width*.38f,.3f,.0019f);
            }
            /// <summary>A panel seam along a plate (a fine dark groove line).</summary>
            public void Seamline(SurfEval s,float u0,float v0,float u1,float v1,int steps=4,float width=.0035f,WeaponPaint? paint=null)
            {
                if(!Fine)return;var path=new (float,float)[steps+1];for(int i=0;i<=steps;i++){float t=i/(float)steps;path[i]=(u0+(u1-u0)*t,v0+(v1-v0)*t);}StripOn(paint??SeamDark,s,path,width,1,.0011f);
            }

            // ================================================================================================================
            // Small parts: teeth, claws, tatters, cables
            // ================================================================================================================
            /// <summary>A tooth: a tapered, slightly bent wedge from its root (centre of its base) along `down`, `w` wide, `d` deep.</summary>
            public void ToothAt(in WeaponPaint paint,Vector3 root,Vector3 down,Vector3 outward,float w,float len,float d,float tip=.36f)
            {
                down=Vector3.Normalize(down);var side=Vector3.Normalize(Vector3.Cross(outward,down));var o=Vector3.Normalize(Vector3.Cross(down,side));
                var b0=root-side*w*.5f-o*d*.5f;var b1=root+side*w*.5f-o*d*.5f;var b2=root+side*w*.5f+o*d*.5f;var b3=root-side*w*.5f+o*d*.5f;
                var tipC=root+down*len+o*d*.15f;var t0=tipC-side*w*tip*.5f;var t1=tipC+side*w*tip*.5f;var mid=root+down*len*.45f;var pp=paint;
                Prim(()=>{Facing(pp,b3,b2,t1,t0,o);Facing(pp,b1,b0,t0,t1,-o);Out3(pp,b0,b3,t0,mid);Out3(pp,b2,b1,t1,mid);});
            }
            private void Out3(in WeaponPaint p,Vector3 a,Vector3 b,Vector3 c,Vector3 inside){var n=Vector3.Cross(b-a,c-a);var pp=p;if(Vector3.Dot(n,(a+b+c)/3-inside)<0){var t=b;b=c;c=t;}Mirror(()=>S.Tri(pp,a,b,c));}
            /// <summary>A claw or nail: a curved point from `root` along `dir`, bending toward `curl`, `w` wide at the root.</summary>
            public void Claw(in WeaponPaint paint,Vector3 root,Vector3 dir,Vector3 curl,float len,float w)
            {
                dir=Vector3.Normalize(dir);var mid=root+dir*len*.55f+curl*len*.12f;var tip=root+dir*len+curl*len*.38f;
                Tube(paint,new[]{root,mid,tip},new[]{w*.5f,w*.32f,w*.04f},3,true,true,.75f);
            }
            /// <summary>
            /// A torn tatter hanging from `top` along `down` (cloth strips of a torn suit, a shawl's rags): `len` long, `w` wide,
            /// narrowing to a ragged point, bent out by `out`; two-sided; its vertices sway with their depth (a dangling part).
            /// </summary>
            public void Tatter(in WeaponPaint paint,Vector3 top,Vector3 down,Vector3 outward,float len,float w,float bend=.15f)
            {
                if(Far)return;down=Vector3.Normalize(down);outward=Vector3.Normalize(outward);var side=Vector3.Normalize(Vector3.Cross(outward,down));
                int n=Fine?3:2;var l=new Vector3[n+1];var rr=new Vector3[n+1];
                for(int i=0;i<=n;i++){float t=i/(float)n;var c=top+down*len*t+outward*len*bend*t*t;float ww=w*.5f*(1-.8f*t)*(1+.25f*(H(i,0xc1)-.5f));l[i]=c-side*ww;rr[i]=c+side*ww*(i==n?.2f:1);}
                var pp=paint;var t0=top;float L=len;
                Piece(0,q=>Vector3.Distance(q,t0)/L,()=>Prim(()=>{for(int i=0;i<n;i++){Facing(pp,l[i],rr[i],rr[i+1],l[i+1],outward);Facing(pp.Scaled(.7f),l[i],rr[i],rr[i+1],l[i+1],-outward);}}));
            }
            /// <summary>Exposed wiring out of broken armour: a sagging cable from a to b (hanging by `sag`), its far end dangling.</summary>
            public void Cable(in WeaponPaint paint,Vector3 a,Vector3 b,float sag,float radius=.0045f)
            {
                if(!Mid)return;int n=Fine?6:3;var path=new Vector3[n+1];var rad=new float[n+1];for(int i=0;i<=n;i++){float t=i/(float)n;path[i]=Vector3.Lerp(a,b,t)-Vector3.UnitY*sag*4*t*(1-t);rad[i]=radius;}
                var pp=paint;var a0=a;float L=Vector3.Distance(a,b)+sag;Piece(0,q=>Vector3.Distance(q,a0)/L,()=>{Tube(pp,path,rad,4,true,false);Tube(Copper,new[]{b,b+(b-path[n-1])*.4f},new[]{radius*.7f,radius*.5f},3,true,false);});
            }

            // ================================================================================================================
            // Finish: occlusion, grime and blood, regions
            // ================================================================================================================
            // Piece leaves the sway as half its weight over the variant (Finish packs it).
            private static float Sway(float y)=>Math.Max(0,Math.Min(1,(y-(float)Math.Floor(y+1e-4f))*2));
            private static Vector3 RestAt(int bone){var p=Vector3.Zero;for(int j=bone;j>=0;j=CapturedRig.Bones[j].Parent)p+=CapturedRig.Mirror(CapturedRig.Bones[j].Position);return p;}
            private static bool Near(int a,int b)=>a==b||CapturedRig.Bones[a].Parent==b||CapturedRig.Bones[b].Parent==a;
            /// <summary>Bakes the occlusion into Data.x, grime and blood into the colours and the regions into the alpha.</summary>
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
                        float occl=Math.Max(0,Math.Min(.85f,1-ao));var dd=s.Data[i];s.Data[i]=new Vector2(Pack((float)Math.Floor(dd.X+1e-4f),occl),Pack((float)Math.Floor(dd.Y+1e-4f),Sway(dd.Y)));
                    }
                    float region=!regions?RegionNone:s.Bone==CapturedRig.Head?RegionHead:s.Bone==CapturedRig.Pelvis||s.Bone==CapturedRig.Spine||s.Bone==CapturedRig.Chest?RegionTorso:
                        s.Bone==CapturedRig.LArm||s.Bone==CapturedRig.RArm||s.Bone==CapturedRig.LElbow||s.Bone==CapturedRig.RElbow||s.Bone==CapturedRig.LHip||s.Bone==CapturedRig.RHip||s.Bone==CapturedRig.LKnee||s.Bone==CapturedRig.RKnee?RegionLimb:RegionNone;
                    for(int i=0;i<s.Colors.Count;i++)
                    {
                        var c=s.Colors[i];var e=s.Emission[i];var col=new Vector3(c.X,c.Y,c.Z);
                        if(grime&&e.W<2.5f&&e.X+e.Y+e.Z<=c.X+c.Y+c.Z)
                        {
                            int cls=(int)Math.Floor(s.Data[i].X+1e-4f);var p=s.Positions[i];float g=Grime(s.Bone,p),bl=Blood(s.Bone,p);
                            if(cls==SurfShell||cls==SurfWire){g*=.6f;bl*=.4f;}else if(cls==SurfMatte){g*=.3f;bl*=.3f;}
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
            /// <summary>Blood (the Siren's dark ink-red) round the mouth, down the chin and the chest's front.</summary>
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
