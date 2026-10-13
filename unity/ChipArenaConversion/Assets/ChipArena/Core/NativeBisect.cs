using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One mesh a bisect cuts, in its own draw frame (the mesh exactly as uploaded), drawn on joint <see cref="Bone"/>.</summary>
    public sealed class BisectSource
    {
        public int Bone;
        /// <summary>The cap look its section takes: an index into the looks passed to <see cref="NativeBisect.Split"/> (the
        /// caller's grouping: 0 the body, 1 the hat ...). Caps are built per ragdoll body and group.</summary>
        public int Group;
        public WeaponSection Mesh;
        /// <summary>A closed stand-in whose section fills the cap where <see cref="Mesh"/> is open (a wound cut's unwounded
        /// part); never drawn.</summary>
        public WeaponSection Fill;
        /// <summary>False: cut but never capped (glass, inset lines, a floating halo).</summary>
        public bool Cap=true;
    }

    /// <summary>One piece of a half: a source wholly on that side (<see cref="Whole"/>: draw the source itself), a source's
    /// cut geometry, or a cap (<see cref="Source"/> -1) sealing one ragdoll body's section, in that body joint's draw frame.</summary>
    public sealed class BisectPiece
    {
        public int Half,Source=-1,Bone,Body,Group;public bool Whole;public WeaponSection Mesh;
        public bool IsCap=>Source<0;
    }

    /// <summary>
    /// How a section's cap looks, from its edge inward: the shell's own paint darkened (<see cref="Rim"/> m, some cells blood),
    /// then a lining band (<see cref="Inner"/> m: a suit's undersuit with glints of conduit, a coat's lining; none on the
    /// Prototype), then flesh in layers (brighter at the lining, darker to the core, blood glints). <see cref="Solid"/> caps
    /// in the section's own paint throughout (a hat).
    /// </summary>
    public sealed class BisectLook
    {
        public bool Solid;public float Rim=.011f,Inner=.018f,RimShade=.62f,ConduitShare,ConduitLevel=1.4f;
        public Vector3 InnerColor=new Vector3(.047f,.065f,.078f),Conduit;public int InnerTag;
        public static readonly BisectLook Hat=new BisectLook{Solid=true,RimShade=.55f};
    }

    /// <summary>The cut plane (Unity world): half 1 lies along <see cref="Normal"/>, half 0 against it.</summary>
    public readonly struct BisectPlane{public readonly Vector3 Point,Normal;public BisectPlane(Vector3 point,Vector3 normal){Point=point;Normal=normal;}}

    /// <summary>A split body: the pieces of each half, which ragdoll bodies each half keeps and their bounds (in the body
    /// joint's draw frame, every piece of that body included), and the recess each body's cut keeps from the plane.</summary>
    public sealed class BisectCut
    {
        public readonly List<BisectPiece> Pieces=new List<BisectPiece>();
        public readonly bool[][] Bodies={new bool[22],new bool[22]};
        public readonly Vector3[][] Min={new Vector3[22],new Vector3[22]},Max={new Vector3[22],new Vector3[22]};
        public readonly float[] Recess=new float[22];
        public Vector3 Point,Normal;public int CutTriangles,CapTriangles;
    }

    /// <summary>
    /// A death that cuts the body in two (presentation only): what cut it (<see cref="Cause"/>), the plane, and how the halves
    /// leave it. Each cause maps itself to one of these (NativeSawBisect for the Factory's saws); everything after (the split,
    /// the corpses, the blood and sparks) is shared. Half 1 lies along <see cref="Normal"/>.
    /// </summary>
    public sealed class BisectRequest
    {
        public string Cause;public Vector3 Point,Normal;
        /// <summary>The cutter's velocity (m/s; zero when still): pushes the halves a little and drags the blood with it.</summary>
        public Vector3 Travel;
        /// <summary>Each half's parting velocity (m/s) and tumble (rad/s), Unity world.</summary>
        public Vector3 Away0,Away1,Spin0,Spin1;
        /// <summary>Seconds the halves hold together on the cutter before they part; blood scale (1 a sword cut's).</summary>
        public float Hitch,Blood=1;
        /// <summary>A metal cutter: sparks along the cut.</summary>
        public bool Sparks;
        /// <summary>A spinning blade (zero radius for none): its centre at the cut, unit axle, signed spin (rad/s about the axle)
        /// and radius. Blood is flung off its rim and sparks leave it along the rim's motion.</summary>
        public Vector3 Hub,Axle;public float Rate,Radius;
        public uint Seed;
        public float Side(Vector3 p)=>Vector3.Dot(p-Point,Normal);
    }

    /// <summary>A spark of the cut at one moment: its head, velocity (m/s), width (m), age 0..1 and heat 0..1.</summary>
    public struct BisectSpark{public Vector3 Head,Velocity;public float Width,U,Heat;public int Seed;}

    /// <summary>
    /// Splits a posed body in two along a plane (owner 2026-10-12: "For the buzzsaw death on factory, can we add essentially a
    /// "split in the middle" gib? This can be useful for future things too"). Engine free, deterministic, presentation only.
    /// <list type="bullet">
    /// <item>Input: every mesh the body draws (the body skin's parts with their wound cuts, the hat, anything attached) in its
    /// own joint's draw frame, each joint's draw matrix (mesh to world) and the plane. A mesh wholly on one side goes to that
    /// half untouched; a crossed one is clipped exactly (shared edges give bit-identical points).</item>
    /// <item>Caps: per ragdoll body (<see cref="Bodies"/>) and look group, the union of its closed meshes' sections
    /// (open chains such as inset lines are dropped; a wound cut's open part is filled by its unwounded stand-in), scanned in
    /// exact trapezoids (split where outlines cross), coloured on a cell grid by depth from the edge (<see cref="BisectLook"/>).
    /// Each body's cut keeps its own small recess from the plane where its section overlaps a neighbour's (no coplanar caps
    /// fighting at a joint), so every half is sealed: nothing is see-through.</item>
    /// <item>After the split nothing allocates: the caller uploads the pieces once; <see cref="Shudder"/>, <see cref="Sparks"/>
    /// and <see cref="Gush"/> are pure functions of the request and the age.</item>
    /// </list>
    /// </summary>
    public static class NativeBisect
    {
        /// <summary>The ragdoll bodies (NativeCorpse's part roots): a joint's geometry moves with its nearest one.</summary>
        public static readonly int[] Bodies={1,2,5,7,8,11,12,14,15,18,19};
        public static int Anchor(int bone){for(int b=bone;b>=0&&b<CapturedRig.Bones.Length;b=CapturedRig.Bones[b].Parent)if(Array.IndexOf(Bodies,b)>=0)return b;return CapturedRig.Pelvis;}
        /// <summary>The recess (m) a body's cut keeps per overlap colour, and the most colours used.</summary>
        public const float Recess=.0003f;public const int Colours=4;
        /// <summary>Cap cell sizes (m) by body tier (Full, Reduced, Low).</summary>
        public static readonly float[] Cells={.012f,.016f,.022f};
        /// <summary>
        /// Whether a death is split: gore on, a pilot's own body (not a Siren, not a revivable PvE body) and a stock ending
        /// (a killer's purchased kill effect keeps its own look). Gore off: the current death, unchanged.
        /// </summary>
        public static bool Applies(bool gore,string style,bool kept,string model)=>gore&&!kept&&(model??"pilot")=="pilot"&&(style==null||style=="impact-ragdoll"||style=="ragdoll");

        /// <summary>A request for a cut along `normal` through `point`: the halves part along the normal (`part` m/s), lift
        /// (`lift`), ride the cutter's travel (`push` of it) and tip outward (`topple` rad/s); they hold `hitch` s first.</summary>
        public static BisectRequest Request(string cause,Vector3 point,Vector3 normal,Vector3 travel,uint seed,float part=1.5f,float lift=.9f,float push=.18f,float topple=2.4f,float hitch=.16f,float blood=1.4f,bool sparks=false)
        {
            var n=CapturedRig.Unit(normal,Vector3.UnitX);if(!Finite(travel))travel=Vector3.Zero;
            var r=new BisectRequest{Cause=cause,Point=point,Normal=n,Travel=travel,Hitch=Math.Max(0,hitch),Blood=blood,Sparks=sparks,Seed=seed};
            // Half 1 tips with its top moving along +n: (up x n) x up = n.
            var tip=Vector3.Cross(Vector3.UnitY,n);if(tip.LengthSquared()<1e-4f)tip=Vector3.Zero;else tip=Vector3.Normalize(tip);
            var drift=travel*push;r.Away1=n*part+Vector3.UnitY*lift+drift;r.Away0=-n*part+Vector3.UnitY*lift+drift;
            // A level cut (a horizontal plane): the upper half lifts off, the lower one stays.
            if(tip==Vector3.Zero){r.Away1=n*(part+lift)+drift;r.Away0=drift*.5f;}
            r.Spin1=tip*topple;r.Spin0=-tip*topple;return r;
        }

        // ================================================================================================ the split
        private struct Seg{public Vector2 A,B;public Vector4 Color,Emission;}
        private sealed class Frame{public Vector3 N,O,U,V;public float D,Scale;}
        /// <summary>
        /// Splits the body: `sources` the meshes it draws, `draw` each joint's draw matrix (System.Numerics row vectors: mesh
        /// point times matrix is the world point), `looks` the cap look of each group (null: no cap), `tier` the body tier (cap
        /// cell size) and `seed` the cap's cell pattern.
        /// </summary>
        public static BisectCut Split(IReadOnlyList<BisectSource> sources,Matrix4x4[] draw,in BisectPlane plane,BisectLook[] looks,int tier,uint seed)
        {
            var cut=new BisectCut();var n=CapturedRig.Unit(plane.Normal,Vector3.UnitX);var p0=plane.Point;cut.Normal=n;cut.Point=p0;
            Basis(n,out var t1,out var t2);int groups=Math.Max(1,looks?.Length??1);float cell=Cells[Math.Max(0,Math.Min(Cells.Length-1,tier))];
            // The plane in each joint's draw frame (any affine draw matrix: three points and the positive side mapped back).
            var frames=new Frame[22];var inv=new Matrix4x4[22];
            for(int k=0;k<22;k++)
            {
                if(!Matrix4x4.Invert(draw[k],out inv[k]))inv[k]=Matrix4x4.Identity;
                Vector3 a=Vector3.Transform(p0,inv[k]),b=Vector3.Transform(p0+t1,inv[k]),c=Vector3.Transform(p0+t2,inv[k]),d=Vector3.Transform(p0+n,inv[k]);
                var fn=CapturedRig.Unit(Vector3.Cross(b-a,c-a),Vector3.UnitX);if(Vector3.Dot(fn,d-a)<0)fn=-fn;float scale=Vector3.Dot(fn,d-a);if(!(scale>1e-6f))scale=1;
                Basis(fn,out var u,out var v);frames[k]=new Frame{N=fn,D=Vector3.Dot(fn,a),Scale=scale,U=u,V=v};
            }
            int count=sources?.Count??0;var dist=new float[count][];var fill=new float[count][];var side=new int[count];
            const float Reach=Recess*(Colours-1)+1e-5f;
            for(int i=0;i<count;i++)
            {
                var s=sources[i];if(s?.Mesh==null||s.Bone<0||s.Bone>=22){side[i]=2;continue;}var f=frames[s.Bone];dist[i]=Distances(s.Mesh,f);
                if(s.Fill!=null)fill[i]=Distances(s.Fill,f);
                float lo=float.MaxValue,hi=float.MinValue;foreach(float x in dist[i]){if(x<lo)lo=x;if(x>hi)hi=x;}
                side[i]=dist[i].Length==0?2:hi<-Reach?0:lo>Reach?1:-1;
            }
            // Bodies whose sections overlap a neighbour's take different recesses (greedy, in body order).
            var lo2=new Vector2[22];var hi2=new Vector2[22];var crossed=new bool[22];
            for(int i=0;i<count;i++)
            {
                if(side[i]!=-1)continue;var s=sources[i];int body=Anchor(s.Bone);var m=s.Mesh;var d=dist[i];
                for(int t=0;t+2<m.Indices.Count;t+=3)
                {
                    int ia=m.Indices[t],ib=m.Indices[t+1],ic=m.Indices[t+2];float da=d[ia],db=d[ib],dc=d[ic];
                    if(Math.Max(da,Math.Max(db,dc))<-Reach||Math.Min(da,Math.Min(db,dc))>Reach)continue;
                    for(int corner=0;corner<3;corner++){int vi=corner==0?ia:corner==1?ib:ic;var w=Vector3.Transform(m.Positions[vi],draw[s.Bone]);var q=new Vector2(Vector3.Dot(w-p0,t1),Vector3.Dot(w-p0,t2));if(!crossed[body]){lo2[body]=hi2[body]=q;crossed[body]=true;}else{lo2[body]=Vector2.Min(lo2[body],q);hi2[body]=Vector2.Max(hi2[body],q);}}
                }
            }
            var colour=new int[22];
            foreach(int b in Bodies)
            {
                if(!crossed[b])continue;int used=0;
                foreach(int o in Bodies){if(o==b)break;if(crossed[o]&&lo2[o].X<=hi2[b].X&&hi2[o].X>=lo2[b].X&&lo2[o].Y<=hi2[b].Y&&hi2[o].Y>=lo2[b].Y)used|=1<<colour[o];}
                int k=0;while(k<Colours-1&&(used&(1<<k))!=0)k++;colour[b]=k;cut.Recess[b]=k*Recess;
            }
            // Each half: whole sources, cut sources and their sections, then the caps.
            var buckets=new List<Seg>[2*22*groups];
            for(int h=0;h<2;h++)
            {
                int keep=h==1?1:-1;
                for(int i=0;i<count;i++)
                {
                    if(side[i]==2)continue;var s=sources[i];int body=Anchor(s.Bone);
                    if(side[i]==h){cut.Pieces.Add(new BisectPiece{Half=h,Source=i,Bone=s.Bone,Body=body,Group=s.Group,Whole=true});Grow(cut,h,body,s.Mesh.Positions,draw[s.Bone]*inv[body]);continue;}
                    if(side[i]!=-1)continue;
                    float off=keep*cut.Recess[body];var f=frames[s.Bone];var fb=frames[body];var toBody=draw[s.Bone]*inv[body];
                    bool capped=s.Cap&&looks!=null&&s.Group>=0&&s.Group<groups&&looks[s.Group]!=null;
                    List<Seg> segs=null;if(capped){int key=(h*22+body)*groups+s.Group;segs=buckets[key]??=new List<Seg>();}
                    var plane2=PlaneIn(fb,off);
                    // A mesh with a stand-in (a wound cut's skin, open at its hole) is capped by its stand-in's section alone.
                    var piece=Clip(s.Mesh,dist[i],off,keep,s.Fill!=null?null:segs,toBody,plane2);
                    if(capped&&s.Fill!=null)Clip(s.Fill,fill[i],off,keep,segs,toBody,plane2,false);
                    if(piece!=null&&piece.Indices.Count>0){cut.Pieces.Add(new BisectPiece{Half=h,Source=i,Bone=s.Bone,Body=body,Group=s.Group,Mesh=piece});cut.CutTriangles+=piece.Indices.Count/3;Grow(cut,h,body,piece.Positions,toBody);}
                }
                foreach(int body in Bodies)for(int g=0;g<groups;g++)
                {
                    var segs=buckets[(h*22+body)*groups+g];if(segs==null||segs.Count==0)continue;var fb=frames[body];
                    var cap=new WeaponSection{Bone=body};Cap(segs,looks[g],cell,seed^(uint)(body*977+g*131+h*7919),PlaneIn(fb,keep*cut.Recess[body]),h==0,cap);
                    if(cap.Indices.Count==0)continue;cut.Pieces.Add(new BisectPiece{Half=h,Source=-1,Bone=body,Body=body,Group=g,Mesh=cap});cut.CapTriangles+=cap.Indices.Count/3;Grow(cut,h,body,cap.Positions,Matrix4x4.Identity);
                }
            }
            // Both halves keep the pelvis (the corpse's root), even when every pelvis mesh fell to one side.
            for(int h=0;h<2;h++)if(!cut.Bodies[h][CapturedRig.Pelvis]){cut.Bodies[h][CapturedRig.Pelvis]=true;cut.Min[h][CapturedRig.Pelvis]=new Vector3(-.05f);cut.Max[h][CapturedRig.Pelvis]=new Vector3(.05f);}
            return cut;
        }
        /// <summary>
        /// The split's plan, before (and much faster than) the split: which ragdoll bodies each half keeps and their bounds in
        /// each body joint's draw frame, from the meshes' vertices on each side and the points where their edges cross the
        /// plane. A caller can build the halves' corpses from it while <see cref="Split"/> runs elsewhere (it only reads the
        /// sources). No pieces.
        /// </summary>
        public static BisectCut Plan(IReadOnlyList<BisectSource> sources,Matrix4x4[] draw,in BisectPlane plane)
        {
            var cut=new BisectCut();var n=CapturedRig.Unit(plane.Normal,Vector3.UnitX);var p0=plane.Point;cut.Normal=n;cut.Point=p0;var inv=new Matrix4x4[22];
            for(int k=0;k<22;k++)if(!Matrix4x4.Invert(draw[k],out inv[k]))inv[k]=Matrix4x4.Identity;
            var one=new List<Vector3>(1){Vector3.Zero};
            for(int i=0;i<(sources?.Count??0);i++)
            {
                var s=sources[i];if(s?.Mesh==null||s.Bone<0||s.Bone>=22)continue;int body=Anchor(s.Bone);var m=s.Mesh;var toWorld=draw[s.Bone];var toBody=toWorld*inv[body];
                var d=new float[m.Positions.Count];
                for(int v=0;v<d.Length;v++){d[v]=Vector3.Dot(Vector3.Transform(m.Positions[v],toWorld)-p0,n);one[0]=m.Positions[v];Grow(cut,d[v]>=0?1:0,body,one,toBody);}
                for(int t=0;t+2<m.Indices.Count;t+=3)for(int e=0;e<3;e++)
                {
                    int a=m.Indices[t+e],b=m.Indices[t+(e+1)%3];if(d[a]>=0==d[b]>=0)continue;float l=d[a]/(d[a]-d[b]);one[0]=m.Positions[a]+(m.Positions[b]-m.Positions[a])*l;Grow(cut,0,body,one,toBody);Grow(cut,1,body,one,toBody);
                }
            }
            for(int h=0;h<2;h++)if(!cut.Bodies[h][CapturedRig.Pelvis]){cut.Bodies[h][CapturedRig.Pelvis]=true;cut.Min[h][CapturedRig.Pelvis]=new Vector3(-.05f);cut.Max[h][CapturedRig.Pelvis]=new Vector3(.05f);}
            return cut;
        }
        private static bool Finite(Vector3 v)=>!(float.IsNaN(v.X)||float.IsNaN(v.Y)||float.IsNaN(v.Z)||float.IsInfinity(v.X)||float.IsInfinity(v.Y)||float.IsInfinity(v.Z));
        /// <summary>Two unit vectors completing `n` (u x v = n), the same for the same normal.</summary>
        public static void Basis(Vector3 n,out Vector3 u,out Vector3 v)
        {
            var a=Math.Abs(n.Y)<.9f?Vector3.UnitY:Vector3.UnitX;u=Vector3.Normalize(Vector3.Cross(a,n));v=Vector3.Cross(n,u);
        }
        private static float[] Distances(WeaponSection m,Frame f){var d=new float[m.Positions.Count];for(int i=0;i<d.Length;i++)d[i]=(Vector3.Dot(f.N,m.Positions[i])-f.D)/f.Scale;return d;}
        /// <summary>The plane `off` metres along its normal, in a body joint's frame: origin, basis and normal.</summary>
        private static Frame PlaneIn(Frame f,float off)=>new Frame{N=f.N,D=f.D+off*f.Scale,Scale=f.Scale,U=f.U,V=f.V,O=f.N*(f.D+off*f.Scale)};
        private static void Grow(BisectCut cut,int h,int body,List<Vector3> points,Matrix4x4 toBody)
        {
            ref bool has=ref cut.Bodies[h][body];
            foreach(var p in points){var q=Vector3.Transform(p,toBody);if(!has){cut.Min[h][body]=cut.Max[h][body]=q;has=true;}else{cut.Min[h][body]=Vector3.Min(cut.Min[h][body],q);cut.Max[h][body]=Vector3.Max(cut.Max[h][body],q);}}
        }

        // ---- clipping: one half of a mesh, and its section's oriented segments in the body's plane ----
        private const float Eps=1e-7f;
        private static WeaponSection Clip(WeaponSection m,float[] dist,float off,int keep,List<Seg> segs,Matrix4x4 toBody,Frame plane,bool draw=true)
        {
            var o=draw?new WeaponSection{Bone=m.Bone}:null;int nv=m.Positions.Count;var map=draw?new int[nv]:null;if(map!=null)for(int i=0;i<nv;i++)map[i]=-1;
            if(o!=null){o.Positions.Capacity=o.Normals.Capacity=o.Colors.Capacity=o.Emission.Capacity=nv;o.Indices.Capacity=m.Indices.Count;}
            var edges=draw?new Dictionary<long,int>():null;
            // r: the signed distance from this half's plane (on it counts as the far side of half 0, the near side of half 1).
            float R(int i){float r=dist[i]-off;return Math.Abs(r)<Eps?Eps:r;}
            var ids=new int[3];var poly=new int[4];var cross=new Vector3[2];var paint=new Vector4[2];var glow=new Vector4[2];
            for(int t=0;t+2<m.Indices.Count;t+=3)
            {
                ids[0]=m.Indices[t];ids[1]=m.Indices[t+1];ids[2]=m.Indices[t+2];int inside=0;
                for(int k=0;k<3;k++)if(R(ids[k])*keep>0)inside++;
                if(inside==0)continue;
                if(inside==3){if(o!=null){for(int k=0;k<3;k++)o.Indices.Add(Keep(m,o,map,ids[k]));}continue;}
                int pn=0,cn=0;
                for(int k=0;k<3;k++)
                {
                    int a=ids[k],b=ids[(k+1)%3];bool ia=R(a)*keep>0,ib=R(b)*keep>0;
                    if(ia&&o!=null)poly[pn++]=Keep(m,o,map,a);
                    if(ia!=ib)
                    {
                        Crossing(m,dist,off,a,b,out var p,out var c,out var e,out var nn);
                        if(o!=null){long key=a<b?((long)a<<32)|(uint)b:((long)b<<32)|(uint)a;if(!edges.TryGetValue(key,out int idx)){idx=o.Positions.Count;o.Positions.Add(p);o.Normals.Add(nn);o.Colors.Add(c);o.Emission.Add(e);edges[key]=idx;}poly[pn++]=idx;}
                        if(cn<2){cross[cn]=p;paint[cn]=c;glow[cn]=e;cn++;}
                    }
                }
                if(o!=null)for(int k=1;k+1<pn;k++){o.Indices.Add(poly[0]);o.Indices.Add(poly[k]);o.Indices.Add(poly[k+1]);}
                if(segs!=null&&cn==2)
                {
                    // The section runs with the solid on its left seen from +N: d = N x (the face's outward normal in the plane).
                    Vector3 pa=m.Positions[ids[0]],pb=m.Positions[ids[1]],pc=m.Positions[ids[2]];var normal=Vector3.TransformNormal(Vector3.Cross(pb-pa,pc-pa),toBody);
                    var outward=normal-plane.N*Vector3.Dot(normal,plane.N);if(outward.LengthSquared()<1e-24f)continue;var along=Vector3.Cross(plane.N,outward);
                    Vector3 qa=Vector3.Transform(cross[0],toBody),qb=Vector3.Transform(cross[1],toBody);
                    if(Vector3.Dot(qb-qa,along)<0){var sw=qa;qa=qb;qb=sw;}
                    var a2=new Vector2(Vector3.Dot(qa-plane.O,plane.U),Vector3.Dot(qa-plane.O,plane.V));var b2=new Vector2(Vector3.Dot(qb-plane.O,plane.U),Vector3.Dot(qb-plane.O,plane.V));
                    if((b2-a2).LengthSquared()<1e-16f)continue;segs.Add(new Seg{A=a2,B=b2,Color=(paint[0]+paint[1])*.5f,Emission=(glow[0]+glow[1])*.5f});
                }
            }
            return o;
        }
        private static int Keep(WeaponSection m,WeaponSection o,int[] map,int i)
        {
            if(map[i]>=0)return map[i];map[i]=o.Positions.Count;o.Positions.Add(m.Positions[i]);o.Normals.Add(m.Normals[i]);o.Colors.Add(m.Colors[i]);o.Emission.Add(m.Emission[i]);return map[i];
        }
        /// <summary>Where edge (a, b) meets the plane: computed from the edge's lower vertex by position, so the two faces
        /// sharing it (even through duplicated vertices) get the same point.</summary>
        private static void Crossing(WeaponSection m,float[] dist,float off,int a,int b,out Vector3 p,out Vector4 c,out Vector4 e,out Vector3 n)
        {
            Vector3 pa=m.Positions[a],pb=m.Positions[b];if(Less(pb,pa)||pa==pb&&b<a){int t=a;a=b;b=t;var tp=pa;pa=pb;pb=tp;}
            float ra=dist[a]-off,rb=dist[b]-off;if(Math.Abs(ra)<Eps)ra=Eps;if(Math.Abs(rb)<Eps)rb=Eps;float l=ra/(ra-rb);if(!(l>=0))l=0;else if(l>1)l=1;
            p=pa+(pb-pa)*l;c=m.Colors[a]+(m.Colors[b]-m.Colors[a])*l;e=m.Emission[a]+(m.Emission[b]-m.Emission[a])*l;e.W=l<.5f?m.Emission[a].W:m.Emission[b].W;
            n=CapturedRig.Unit(m.Normals[a]+(m.Normals[b]-m.Normals[a])*l,m.Normals[a]);
        }
        private static bool Less(Vector3 a,Vector3 b)=>a.X<b.X||a.X==b.X&&(a.Y<b.Y||a.Y==b.Y&&a.Z<b.Z);

        // ---- the cap: the union of the closed sections, scanned in exact trapezoids, coloured on a cell grid ----
        private const float Snap=1e-6f;
        /// <summary>Drops the open chains (an inset line, a wound cut's open shell): what remains closes into loops.</summary>
        private static List<Seg> Closed(List<Seg> segs)
        {
            var nodes=new Dictionary<long,int>();var at=new List<Vector2>();var from=new int[segs.Count];var to=new int[segs.Count];
            int Node(Vector2 p)
            {
                long qx=(long)Math.Floor(p.X/Snap),qy=(long)Math.Floor(p.Y/Snap);
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)if(nodes.TryGetValue(((qx+dx)<<32)^((qy+dy)&0xffffffffL),out int found)&&(at[found]-p).LengthSquared()<=Snap*Snap*2.25f)return found;
                int id=at.Count;at.Add(p);nodes[(qx<<32)^(qy&0xffffffffL)]=id;return id;
            }
            for(int i=0;i<segs.Count;i++){from[i]=Node(segs[i].A);to[i]=Node(segs[i].B);}
            var outs=new int[at.Count];var ins=new int[at.Count];var alive=new bool[segs.Count];
            for(int i=0;i<segs.Count;i++){if(from[i]==to[i])continue;alive[i]=true;outs[from[i]]++;ins[to[i]]++;}
            for(bool changed=true;changed;)
            {
                changed=false;
                for(int i=0;i<segs.Count;i++)if(alive[i]&&(ins[from[i]]==0||outs[to[i]]==0)){alive[i]=false;outs[from[i]]--;ins[to[i]]--;changed=true;}
            }
            var o=new List<Seg>(segs.Count);for(int i=0;i<segs.Count;i++)if(alive[i])o.Add(segs[i]);return o;
        }
        /// <summary>Welds the outline: endpoints within <see cref="Snap"/> become one point, a level step's two ends one height
        /// (so a scan line through a corner counts each outline exactly once), and a stretch left twice counts once.</summary>
        private static List<Seg> Level(List<Seg> segs)
        {
            var nodes=new Dictionary<long,int>();var at=new List<Vector2>();var ends=new int[segs.Count*2];
            int Node(Vector2 p)
            {
                long qx=(long)Math.Floor(p.X/Snap),qy=(long)Math.Floor(p.Y/Snap);
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)if(nodes.TryGetValue(((qx+dx)<<32)^((qy+dy)&0xffffffffL),out int found)&&(at[found]-p).LengthSquared()<=Snap*Snap*2.25f)return found;
                int id=at.Count;at.Add(p);nodes[(qx<<32)^(qy&0xffffffffL)]=id;return id;
            }
            for(int i=0;i<segs.Count;i++){ends[i*2]=Node(segs[i].A);ends[i*2+1]=Node(segs[i].B);}
            var root=new int[at.Count];for(int i=0;i<root.Length;i++)root[i]=i;
            int Find(int x){while(root[x]!=x)x=root[x]=root[root[x]];return x;}
            for(int i=0;i<segs.Count;i++)if(Math.Abs(at[ends[i*2]].Y-at[ends[i*2+1]].Y)<2*Snap){int a=Find(ends[i*2]),b=Find(ends[i*2+1]);if(a!=b)root[Math.Max(a,b)]=Math.Min(a,b);}
            // Two solids sharing a face's line (stacked plates flush on one side) leave that stretch of outline twice: once is the union's.
            var o=new List<Seg>(segs.Count);var seen=new HashSet<long>();
            for(int i=0;i<segs.Count;i++)
            {
                int a=ends[i*2],b=ends[i*2+1];if(a==b||!seen.Add(((long)a<<32)|(uint)b))continue;var q=segs[i];q.A=new Vector2(at[a].X,at[Find(a)].Y);q.B=new Vector2(at[b].X,at[Find(b)].Y);if(q.A==q.B)continue;o.Add(q);
            }
            return o;
        }
        /// <summary>The union's outline: segments split where they cross or overlap another, those with solid on their outer
        /// side (inside another outline) dropped. What remains bounds the union once, so its winding is 0 or 1.</summary>
        private static List<Seg> Outline(List<Seg> segs)
        {
            int n=segs.Count;if(n<3)return segs;Vector2 lo=segs[0].A,hi=lo;foreach(var q in segs){lo=Vector2.Min(lo,Vector2.Min(q.A,q.B));hi=Vector2.Max(hi,Vector2.Max(q.A,q.B));}
            // Pairs whose boxes overlap, swept along u (sorted by their left end, the index breaking ties).
            var minx=new float[n];var maxx=new float[n];var miny=new float[n];var maxy=new float[n];var order=new int[n];
            for(int i=0;i<n;i++){var a=segs[i].A;var b=segs[i].B;minx[i]=Math.Min(a.X,b.X);maxx[i]=Math.Max(a.X,b.X);miny[i]=Math.Min(a.Y,b.Y);maxy[i]=Math.Max(a.Y,b.Y);order[i]=i;}
            Array.Sort(order,(x,y)=>minx[x]!=minx[y]?minx[x].CompareTo(minx[y]):x.CompareTo(y));
            var cuts=new List<float>[n];
            void Cut(int i,float t){if(t>1e-5f&&t<1-1e-5f)(cuts[i]??=new List<float>()).Add(t);}
            for(int oi=0;oi<n;oi++)for(int oj=oi+1;oj<n;oj++)
            {
                int i=order[oi],j=order[oj];if(minx[j]>maxx[i]+1e-6f)break;if(miny[j]>maxy[i]+1e-6f||maxy[j]<miny[i]-1e-6f)continue;if(i>j){int t=i;i=j;j=t;}
                Vector2 a=segs[i].A,b=segs[i].B,c=segs[j].A,d=segs[j].B,r=b-a,s=d-c;float den=Cross(r,s),rl=r.Length(),sl=s.Length();if(rl<1e-9f||sl<1e-9f)continue;
                if(Math.Abs(den)>1e-7f*rl*sl)
                {
                    float t=Cross(c-a,s)/den,u=Cross(c-a,r)/den;
                    if(t>=-1e-5f&&t<=1+1e-5f&&u>=-1e-5f&&u<=1+1e-5f){Cut(i,t);Cut(j,u);}
                }
                else if(Math.Abs(Cross(c-a,r))/rl<1e-6f)
                {
                    // Collinear: each splits where the other ends.
                    float rr=Vector2.Dot(r,r),ss=Vector2.Dot(s,s);Cut(i,Vector2.Dot(c-a,r)/rr);Cut(i,Vector2.Dot(d-a,r)/rr);Cut(j,Vector2.Dot(a-c,s)/ss);Cut(j,Vector2.Dot(b-c,s)/ss);
                }
            }
            var parts=new List<Seg>(n*2);
            for(int i=0;i<n;i++)
            {
                var q=segs[i];if(cuts[i]==null){parts.Add(q);continue;}var ts=cuts[i];ts.Sort();float prev=0;var from=q.A;
                foreach(float t in ts){if(t-prev<1e-5f)continue;var at=q.A+(q.B-q.A)*t;parts.Add(new Seg{A=from,B=at,Color=q.Color,Emission=q.Emission});from=at;prev=t;}
                parts.Add(new Seg{A=from,B=q.B,Color=q.Color,Emission=q.Emission});
            }
            // The winding at a point: the crossings of a level line on its left (down +1, up -1), by height bands.
            int m=parts.Count;float bh=Math.Max(1e-3f,(hi.Y-lo.Y)/96);int bands=(int)Math.Floor((hi.Y-lo.Y)/bh)+1;var band=new List<int>[bands];
            for(int i=0;i<m;i++){float a=Math.Min(parts[i].A.Y,parts[i].B.Y),b=Math.Max(parts[i].A.Y,parts[i].B.Y);if(b-a<1e-12f)continue;for(int k=Math.Max(0,(int)((a-lo.Y)/bh));k<=Math.Min(bands-1,(int)((b-lo.Y)/bh));k++)(band[k]??=new List<int>()).Add(i);}
            int Winding(Vector2 p)
            {
                int k=(int)Math.Floor((p.Y-lo.Y)/bh);if(k<0||k>=bands||band[k]==null)return 0;int w=0;
                foreach(int i in band[k]){var q=parts[i];float a=Math.Min(q.A.Y,q.B.Y),b=Math.Max(q.A.Y,q.B.Y);if(p.Y<a||p.Y>=b)continue;float u=q.A.X+(p.Y-q.A.Y)*(q.B.X-q.A.X)/(q.B.Y-q.A.Y);if(u<p.X)w+=q.B.Y<q.A.Y?1:-1;}
                return w;
            }
            var o=new List<Seg>(m);const float Off=1e-5f;
            foreach(var q in parts)
            {
                var d=q.B-q.A;float l=d.Length();if(l<1e-9f)continue;var outward=new Vector2(d.Y,-d.X)/l;var mid=(q.A+q.B)*.5f;
                if(Winding(mid+outward*Off)<=0&&Winding(mid-outward*Off)>0)o.Add(q);
            }
            return o;
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.X*b.Y-a.Y*b.X;
        private struct Paint:IEquatable<Paint>
        {
            public Vector4 C,E;public bool Equals(Paint p)=>C==p.C&&E==p.E;public override bool Equals(object o)=>o is Paint p&&Equals(p);public override int GetHashCode()=>C.GetHashCode()*31+E.GetHashCode();
        }
        private static uint Mix(uint h){h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;return h;}
        private static float Hash(uint seed,int a,int b,int c){uint h=Mix(seed^Mix((uint)a*0x9e3779b1u^Mix((uint)b*0x85ebca77u^(uint)c*0xc2b2ae3du)));return (h&0xffffff)/16777216f;}
        private static Vector4 V4(Vector3 c,float w)=>new Vector4(c.X,c.Y,c.Z,w);
        private static readonly Vector3 FleshOuter=NativePortraitRules.Linear(0xa8141f),FleshMid=NativePortraitRules.Linear(0x780a14),FleshDeep=NativePortraitRules.Linear(0x4f0610),
            BloodBright=NativePortraitRules.Linear(0xbd1a24),BloodDark=NativePortraitRules.Linear(0x2c0307);
        private static void Cap(List<Seg> raw,BisectLook look,float cell,uint seed,Frame plane,bool front,WeaponSection into)
        {
            var segs=Level(Outline(Closed(raw)));if(segs.Count<3)return;
            Vector2 lo=segs[0].A,hi=segs[0].A;foreach(var s in segs){lo=Vector2.Min(lo,Vector2.Min(s.A,s.B));hi=Vector2.Max(hi,Vector2.Max(s.A,s.B));}
            float c=cell;while((hi.X-lo.X)/c*(hi.Y-lo.Y)/c>90000)c*=1.5f;
            float u0=(float)Math.Floor(lo.X/c)*c-c,v0=(float)Math.Floor(lo.Y/c)*c-c;int W=(int)Math.Ceiling((hi.X-u0)/c)+2,H=(int)Math.Ceiling((hi.Y-v0)/c)+2;
            int n=segs.Count;var smin=new Vector2[n];var smax=new Vector2[n];for(int i=0;i<n;i++){smin[i]=Vector2.Min(segs[i].A,segs[i].B);smax[i]=Vector2.Max(segs[i].A,segs[i].B);}
            // Segments by the rows they span (horizontal ones never cross a scan line), and every cell an outline touches.
            var rows=new List<int>[H];var flat=new List<int>[H];var edge=new bool[W*H];
            for(int i=0;i<n;i++)
            {
                int r0=Math.Max(0,(int)Math.Floor((smin[i].Y-v0)/c)),r1=Math.Min(H-1,(int)Math.Floor((smax[i].Y-v0)/c));
                if(smax[i].Y-smin[i].Y>1e-12f)for(int r=r0;r<=r1;r++)(rows[r]??=new List<int>()).Add(i);else for(int r=r0;r<=r1;r++)(flat[r]??=new List<int>()).Add(i);
                int j0=Math.Max(0,(int)Math.Floor((smin[i].X-u0)/c)),j1=Math.Min(W-1,(int)Math.Floor((smax[i].X-u0)/c));
                for(int r=Math.Max(0,r0-1);r<=Math.Min(H-1,r1+1);r++)for(int j=Math.Max(0,j0-1);j<=Math.Min(W-1,j1+1);j++)
                    if(!edge[r*W+j]&&Touches(segs[i].A,segs[i].B,u0+j*c,u0+(j+1)*c,v0+r*c,v0+(r+1)*c))edge[r*W+j]=true;
            }
            float U(int i,float v){var s=segs[i];return s.A.X+(v-s.A.Y)*(s.B.X-s.A.X)/(s.B.Y-s.A.Y);}
            int Sign(int i)=>segs[i].B.Y<segs[i].A.Y?1:-1;
            // The coverage at each row's centre line (winding > 0), then each cell's depth from the edge (3-4 chamfer).
            var inside=new bool[W*H];var cross=new List<(float u,int w)>();
            for(int r=0;r<H;r++)
            {
                if(rows[r]==null)continue;float vc=v0+(r+.5f)*c;cross.Clear();
                foreach(int i in rows[r]){if(vc<smin[i].Y||vc>=smax[i].Y)continue;cross.Add((U(i,vc),Sign(i)));}
                cross.Sort((x,y)=>x.u.CompareTo(y.u));int w=0;
                for(int k=0;k<cross.Count;k++)
                {
                    int was=w;w+=cross[k].w;if(was>0&&k>0){int j0=Math.Max(0,(int)Math.Ceiling((cross[k-1].u-u0)/c-.5f)),j1=Math.Min(W-1,(int)Math.Floor((cross[k].u-u0)/c-.5f));for(int j=j0;j<=j1;j++)inside[r*W+j]=true;}
                }
            }
            var depth=new int[W*H];const int Far=1<<28;for(int i=0;i<depth.Length;i++)depth[i]=inside[i]?Far:0;
            for(int r=1;r<H-1;r++)for(int j=1;j<W-1;j++){int i=r*W+j;if(depth[i]==0)continue;int d=depth[i];
                d=Math.Min(d,depth[i-1]+3);d=Math.Min(d,depth[i-W]+3);d=Math.Min(d,depth[i-W-1]+4);d=Math.Min(d,depth[i-W+1]+4);depth[i]=d;}
            for(int r=H-2;r>=1;r--)for(int j=W-2;j>=1;j--){int i=r*W+j;if(depth[i]==0)continue;int d=depth[i];
                d=Math.Min(d,depth[i+1]+3);d=Math.Min(d,depth[i+W]+3);d=Math.Min(d,depth[i+W+1]+4);d=Math.Min(d,depth[i+W-1]+4);depth[i]=d;}
            // Each cell's paint: by its depth from the edge, the shell's own paint (the nearest outline's) at the rim.
            var palette=new List<Paint>();var index=new Dictionary<Paint,int>();var paint=new int[W*H];
            int Id(Vector4 col,Vector4 em){var p=new Paint{C=col,E=em};if(!index.TryGetValue(p,out int id)){id=palette.Count;palette.Add(p);index[p]=id;}return id;}
            int reach=(int)Math.Ceiling(look.Rim/c)+2;
            for(int r=0;r<H;r++)for(int j=0;j<W;j++)
            {
                int i=r*W+j;if(!inside[i]&&!edge[i]){paint[i]=-1;continue;}
                float d=Math.Max(0,depth[i]/3f*c-c*.5f);float h1=Hash(seed,r,j,1),h2=Hash(seed,r,j,2);Vector4 col,em;
                if(look.Solid||d<look.Rim)
                {
                    // The nearest outline's paint (its tag kept, so a tinted shell stays tinted).
                    var p=new Vector2(u0+(j+.5f)*c,v0+(r+.5f)*c);float best=float.MaxValue;Vector4 bc=new Vector4(.5f,.5f,.5f,1),be=Vector4.Zero;
                    float near=reach*c;for(int rr=Math.Max(0,r-reach);rr<=Math.Min(H-1,r+reach);rr++)if(rows[rr]!=null)foreach(int si in rows[rr]){if(smin[si].X>p.X+near||smax[si].X<p.X-near)continue;float q=SegDist(p,segs[si].A,segs[si].B);if(q<best){best=q;bc=segs[si].Color;be=segs[si].Emission;}}
                    if(best==float.MaxValue)for(int si=0;si<n;si++){float q=SegDist(p,segs[si].A,segs[si].B);if(q<best){best=q;bc=segs[si].Color;be=segs[si].Emission;}}
                    float k=look.RimShade*(h1<.25f?.9f:1);
                    if(!look.Solid&&h2<.16f){col=V4(h1<.5f?BloodBright:FleshOuter,1);em=V4(new Vector3(col.X,col.Y,col.Z)*.3f,0);}
                    else{col=new Vector4(bc.X*k,bc.Y*k,bc.Z*k,1);em=new Vector4(be.X*k,be.Y*k,be.Z*k,be.W);}
                }
                else if(d<look.Rim+look.Inner)
                {
                    if(look.ConduitShare>0&&h2<look.ConduitShare){col=V4(look.Conduit,1);em=V4(look.Conduit*look.ConduitLevel,2);}
                    else{float k=h1<.1f?.78f:1;col=V4(look.InnerColor*k,1);em=V4(look.InnerTag==1?look.InnerColor*k*.12f:Vector3.Zero,look.InnerTag);}
                }
                else
                {
                    float f=d-look.Rim-look.Inner;int layer=f<.014f?0:f<.036f?1:2;if(h1<.07f)layer=Math.Min(2,layer+1);
                    var flesh=layer==0?FleshOuter:layer==1?FleshMid:FleshDeep;if(h2<.05f)flesh=BloodBright;else if(layer==2&&h2>.96f)flesh=BloodDark;
                    col=V4(flesh,1);em=V4(flesh*.3f,0);
                }
                paint[i]=Id(col,em);
            }
            var normal=front?plane.N:-plane.N;
            void Poly(Span<Vector2> a,int na,Paint p)
            {
                if(na<3)return;int first=into.Positions.Count;
                for(int k=0;k<na;k++){into.Positions.Add(plane.O+plane.U*a[k].X+plane.V*a[k].Y);into.Normals.Add(normal);into.Colors.Add(p.C);into.Emission.Add(p.E);}
                for(int k=1;k+1<na;k++){into.Indices.Add(first);if(front){into.Indices.Add(first+k);into.Indices.Add(first+k+1);}else{into.Indices.Add(first+k+1);into.Indices.Add(first+k);}}
            }
            // Cells no outline touches lie wholly inside: rectangles of one paint, merged greedily (a hair over their sides,
            // so a seam with the neighbouring edge pieces never opens).
            var done=new bool[W*H];const float Hair=.00004f;Span<Vector2> quad=stackalloc Vector2[4];
            for(int r=0;r<H;r++)for(int j=0;j<W;j++)
            {
                int i=r*W+j;if(done[i]||!inside[i]||edge[i])continue;int id=paint[i],w=1,h=1;
                while(j+w<W&&!done[i+w]&&inside[i+w]&&!edge[i+w]&&paint[i+w]==id)w++;
                for(bool grow=true;grow&&r+h<H;){for(int k=0;k<w;k++){int q=(r+h)*W+j+k;if(done[q]||!inside[q]||edge[q]||paint[q]!=id){grow=false;break;}}if(grow)h++;}
                for(int y=0;y<h;y++)for(int k=0;k<w;k++)done[(r+y)*W+j+k]=true;
                float ux0=u0+j*c-Hair,ux1=u0+(j+w)*c+Hair,vy0=v0+r*c-Hair,vy1=v0+(r+h)*c+Hair;
                quad[0]=new Vector2(ux0,vy0);quad[1]=new Vector2(ux1,vy0);quad[2]=new Vector2(ux1,vy1);quad[3]=new Vector2(ux0,vy1);Poly(quad,4,palette[id]);
            }
            // The cells an outline touches: the region's exact part of each run of one paint, as trapezoids between the
            // outlines near it (slabs split at their corners and where they cross).
            var local=new List<int>();var mark=new int[n];int stamp=0;var events=new List<float>();var active=new List<int>();var mids=new List<(float u,int i)>();
            for(int r=0;r<H;r++)
            {
                if(rows[r]==null)continue;float ya=v0+r*c,yb=ya+c;
                for(int j=0;j<W;)
                {
                    int i0=r*W+j;if(!edge[i0]||paint[i0]<0){j++;continue;}int id=paint[i0],k=j;while(k+1<W&&edge[r*W+k+1]&&paint[r*W+k+1]==id)k++;
                    float x0=u0+j*c,x1=u0+(k+1)*c;j=k+1;stamp++;local.Clear();
                    foreach(int si in rows[r])if(smax[si].X>=x0-1e-7f&&smin[si].X<=x1+1e-7f){local.Add(si);mark[si]=stamp;}
                    events.Clear();events.Add(ya);events.Add(yb);foreach(int si in local){var s=segs[si];if(s.A.Y>ya&&s.A.Y<yb)events.Add(s.A.Y);if(s.B.Y>ya&&s.B.Y<yb)events.Add(s.B.Y);}
                    // A level outline across the run changes the winding at its height, wherever its ends lie.
                    if(flat[r]!=null)foreach(int si in flat[r])if(smax[si].X>=x0-1e-7f&&smin[si].X<=x1+1e-7f&&smin[si].Y>ya&&smin[si].Y<yb)events.Add(smin[si].Y);
                    // Heights a hair apart (corners of outlines that do not meet) make one event: no slab thinner than a micron.
                    events.Sort();var pnt=palette[id];int kept=1;for(int e=1;e<events.Count;e++)if(events[e]-events[kept-1]>1e-6f)events[kept++]=events[e];else if(e==events.Count-1)events[kept-1]=events[e];
                    for(int e=0;e+1<kept;e++)Slab(events[e],events[e+1],0);
                    void Slab(float va,float vb,int deep)
                    {
                        if(vb-va<1e-9f)return;active.Clear();foreach(int si in local)if(smin[si].Y<=va+1e-6f&&smax[si].Y>=vb-1e-6f)active.Add(si);
                        float vm=(va+vb)*.5f;mids.Clear();foreach(int si in active)mids.Add((U(si,vm),si));mids.Sort((x,y)=>x.u!=y.u?x.u.CompareTo(y.u):x.i.CompareTo(y.i));
                        if(deep<24)for(int q=0;q+1<mids.Count;q++)
                        {
                            int s0=mids[q].i,s1=mids[q+1].i;float da=U(s1,va)-U(s0,va),db=U(s1,vb)-U(s0,vb);
                            if(da<-1e-7f||db<-1e-7f){float t=da/(da-db);if(!(t>0&&t<1))t=.5f;float vx=va+(vb-va)*t;if(vx-va>1e-9f&&vb-vx>1e-9f){Slab(va,vx,deep+1);Slab(vx,vb,deep+1);return;}}
                        }
                        // The winding just left of the run: the row's outlines wholly left of it (the local ones are swept).
                        int w=0;foreach(int si in rows[r])if(mark[si]!=stamp&&smin[si].Y<=vm&&smax[si].Y>vm&&U(si,vm)<x0)w+=Sign(si);
                        int left=-1;
                        for(int q=0;q<mids.Count;q++)
                        {
                            int si=mids[q].i,was=w;w+=Sign(si);
                            if(was>0)Piece(left,si,va,vb);
                            left=si;
                        }
                        if(w>0)Piece(left,-1,va,vb);
                    }
                    void Piece(int l,int rr,float va,float vb)
                    {
                        float la=l<0?x0-1:U(l,va),lb=l<0?x0-1:U(l,vb),qa=rr<0?x1+1:U(rr,va),qb=rr<0?x1+1:U(rr,vb);if(qa-la<1e-9f&&qb-lb<1e-9f)return;
                        Span<Vector2> ba=stackalloc Vector2[8];Span<Vector2> bb=stackalloc Vector2[8];ba[0]=new Vector2(la,va);ba[1]=new Vector2(qa,va);ba[2]=new Vector2(qb,vb);ba[3]=new Vector2(lb,vb);
                        int nb=ClipU(ba,4,bb,x0,1);int na=ClipU(bb,nb,ba,x1,-1);Poly(ba,na,pnt);
                    }
                }
            }
        }
        /// <summary>Whether segment a-b meets the square [x0, x1] x [y0, y1] (Liang-Barsky).</summary>
        private static bool Touches(Vector2 a,Vector2 b,float x0,float x1,float y0,float y1)
        {
            float t0=0,t1=1,dx=b.X-a.X,dy=b.Y-a.Y;
            bool Edge(float p,float q){if(Math.Abs(p)<1e-12f)return q>=0;float t=q/p;if(p<0){if(t>t1)return false;if(t>t0)t0=t;}else{if(t<t0)return false;if(t<t1)t1=t;}return true;}
            return Edge(-dx,a.X-x0)&&Edge(dx,x1-a.X)&&Edge(-dy,a.Y-y0)&&Edge(dy,y1-a.Y);
        }
        private static int ClipU(Span<Vector2> src,int n,Span<Vector2> dst,float x,int sign)
        {
            int m=0;
            for(int i=0;i<n;i++)
            {
                var a=src[i];var b=src[(i+1)%n];float da=(a.X-x)*sign,db=(b.X-x)*sign;
                if(da>=0)dst[m++]=a;if(da>=0!=db>=0&&m<dst.Length){float t=da/(da-db);dst[m++]=new Vector2(x,a.Y+(b.Y-a.Y)*t);}
                if(m>=dst.Length-1)break;
            }
            return m;
        }
        private static float SegDist(Vector2 p,Vector2 a,Vector2 b){var ab=b-a;float l=ab.LengthSquared();float t=l>1e-20f?Math.Max(0,Math.Min(1,Vector2.Dot(p-a,ab)/l)):0;return (a+ab*t-p).LengthSquared();}

        // ================================================================================================ the moment
        /// <summary>How far (m) each half stands off the plane while it holds on the cutter (a grinding shudder), 0 after.</summary>
        public static float Shudder(BisectRequest r,float age)=>r==null||!(age>=0)||age>=r.Hitch?0:.0022f+.0016f*(float)Math.Sin(age*230);
        public const int MaxSparks=64;
        /// <summary>
        /// The cut's sparks at `age` s after the death (a metal cutter only): `quality` thins them (1 full). Each leaves the
        /// blade's rim where it meets the body along the rim's motion, over the hold and a little after, and falls. Writes into
        /// `into` (at least <see cref="MaxSparks"/> long) and returns how many; allocation free.
        /// </summary>
        public static int Sparks(BisectRequest r,float age,float quality,BisectSpark[] into)
        {
            if(r==null||!r.Sparks||into==null||!(age>=0)||age>r.Hitch+.6f)return 0;int total=Math.Min(Math.Min(MaxSparks,into.Length),(int)Math.Round(MaxSparks*Math.Max(0,Math.Min(1,quality))));int n=0;
            for(int i=0;i<total;i++)
            {
                float born=(r.Hitch+.07f)*Hash(r.Seed,i,0,11),life=.18f+.28f*Hash(r.Seed,i,0,12),t=age-born;if(t<0||t>life)continue;
                Edge(r,i,out var p,out var tangent);float h1=Hash(r.Seed,i,0,13),h2=Hash(r.Seed,i,0,14),h3=Hash(r.Seed,i,0,15);
                var v=tangent*(5.5f+6.5f*h1)+r.Normal*((h2-.5f)*3.2f)+Vector3.UnitY*(.4f+1.4f*h3)+r.Travel*.4f;
                var g=new Vector3(0,-9.8f,0);into[n++]=new BisectSpark{Head=p+v*t+g*(.5f*t*t),Velocity=v+g*t,Width=.010f+.006f*h3,U=t/life,Heat=1-.6f*t/life,Seed=(int)(r.Seed&0xffff)*64+i};
            }
            return n;
        }
        /// <summary>Where spark (or fling) `i` leaves the blade: a rim point where the blade passes the body (the body's
        /// centre ± its half width along the travel, ± .85 m up and down) and the rim's direction of motion there.</summary>
        private static void Edge(BisectRequest r,int i,out Vector3 p,out Vector3 tangent)
        {
            var axle=CapturedRig.Unit(r.Axle,r.Normal);var up=CapturedRig.Unit(Vector3.UnitY-axle*Vector3.Dot(Vector3.UnitY,axle),Vector3.UnitY);var along=Vector3.Cross(up,axle);
            if(Vector3.Dot(along,r.Travel)<0)along=-along;float R=Math.Max(.05f,r.Radius);var rel=r.Point-r.Hub;float dx=Vector3.Dot(rel,along),dy=Vector3.Dot(rel,up);
            float theta=(float)Math.Atan2(dy,dx);
            for(int k=0;k<6;k++)
            {
                float a=(float)(Hash(r.Seed,i,k,21)*Math.PI*2);float x=R*(float)Math.Cos(a),y=R*(float)Math.Sin(a);
                if(Math.Abs(x-dx)<=.24f&&Math.Abs(y-dy)<=.85f){theta=a;break;}
            }
            var radial=along*(float)Math.Cos(theta)+up*(float)Math.Sin(theta);p=r.Hub+radial*R;
            tangent=CapturedRig.Unit(Vector3.Cross(axle*(r.Rate>=0?1:-1),radial),along);
        }
        /// <summary>Gushes a cut sprays (the first <see cref="CutGushes"/> along the cut, then the blade's flings).</summary>
        public const int CutGushes=5,Gushes=8;
        /// <summary>
        /// Gush `k` of <see cref="Gushes"/>: when (s after the death), from where, out of which way and along what (Unity world)
        /// for the blood spray (NativeBloodSim kinds "sever" along the cut, "cut" off the blade). `low`/`high`: the body's
        /// lowest and highest point. False: no such gush (a cutter with no blade flings nothing).
        /// </summary>
        public static bool Gush(BisectRequest r,int k,float low,float high,out float at,out Vector3 origin,out Vector3 normal,out Vector3 along,out bool blade)
        {
            at=0;origin=normal=along=Vector3.Zero;blade=k>=CutGushes;if(r==null||k<0||k>=Gushes)return false;
            var travel=CapturedRig.Unit(r.Travel,Vector3.Cross(Vector3.UnitY,r.Normal));
            if(!blade)
            {
                float f=(k+.5f)/CutGushes;at=r.Hitch*.9f*k/(CutGushes-1);float y=low+.12f+(high-low-.22f)*(.15f+.7f*(1-f));
                origin=new Vector3(r.Point.X,y,r.Point.Z)+r.Normal*(((k&1)==0?-1:1)*.01f);normal=CapturedRig.Unit(r.Normal*((k&1)==0?-1:1)+Vector3.UnitY*.35f,r.Normal);along=-travel;return true;
            }
            if(!(r.Radius>0))return false;at=.04f+.13f*(k-CutGushes);Edge(r,100+k,out var p,out var tangent);origin=p+r.Travel*at;normal=tangent;along=tangent;return true;
        }
    }
}
