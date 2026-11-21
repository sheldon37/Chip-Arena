using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // ---- the exported pilot's part boxes the wounds carve (Resources/WoundParts; NativeSourceModels.WoundParts loads them) ----
    [Serializable] public sealed class NativeWoundPart {public string name,root;public int bone,tag;public WoundVector center,size;public float[] matrix,color,emission;public WoundSolid[] solids;}
    [Serializable] public sealed class NativeWoundParts {public string commit;public NativeWoundPart[] parts;}
    /// <summary>Where one wound lands on one exported part: the part, its carve, and whether it is the first part the shot met
    /// (a thin plate passes the shot on to the part behind it, which is carved too).</summary>
    public sealed class NativeWoundAnchor {public NativeWoundPart Part;public WoundCarveSpec Spec;public bool First;}
    /// <summary>One wounded exported part: its boxes after every carve on it (what the Prototype draws), and each carve's cells
    /// with whether its wound met this part first (Steps and First run in carve order).</summary>
    public sealed class WoundCarveGroup {public NativeWoundPart Part;public List<WoundSolid> Solids;public readonly List<WoundCarveCells> Steps=new List<WoundCarveCells>();public readonly List<bool> First=new List<bool>();}

    // Engine-free wound placement and carving (moved verbatim from the Runtime's NativeWoundGeometry so the managed checks
    // and the offline renders run the code the game runs): each wound's anchors on the exported part boxes, then each
    // wounded part's boxes carved by NativeWoundCarve in wound order.
    public static class NativeWoundAnchors
    {
        public static string Root(int b)=>b==5?"head":b==2?"spine":b==7?"left-shoulder":b==11?"right-shoulder":b==8?"left-elbow":b==12?"right-elbow":b==14?"left-hip":b==18?"right-hip":b==15?"left-knee":"right-knee";
        private static WoundVector ClampVector(WoundVector p,WoundVector min,WoundVector max)=>new WoundVector(Math.Max(min.x,Math.Min(max.x,p.x)),Math.Max(min.y,Math.Min(max.y,p.y)),Math.Max(min.z,Math.Min(max.z,p.z)));
        private static (double first,double last,int axis)? RayBox(WoundVector o,WoundVector d,WoundVector min,WoundVector max)
        {double first=double.NegativeInfinity,last=double.PositiveInfinity;int axis=0;for(int a=0;a<3;a++){if(Math.Abs(d[a])<1e-9){if(o[a]<min[a]||o[a]>max[a])return null;continue;}double n=(min[a]-o[a])/d[a],f=(max[a]-o[a])/d[a];if(n>f){double swap=n;n=f;f=swap;}if(n>first){first=n;axis=a;}last=Math.Min(last,f);if(first>last)return null;}return(first,last,axis);}
        private static List<NativeWoundAnchor> All(NativeWoundPart[] all,NativeWound w,bool low,WoundVector body)
        {
            string root=Root(w.part);var parts=all.Where(p=>p.root==root||root=="spine"&&p.root=="pelvis").ToArray();if(parts.Length==0)return new List<NativeWoundAnchor>();var point=new WoundVector(w.point.x,w.point.y,w.point.z);var travel=new WoundVector(w.direction.x,w.direction.y,w.direction.z);if(travel.Length<1e-9)travel=new WoundVector(0,0,-1);travel=travel.Normal;
            NativeWoundAnchor On(NativeWoundPart p,WoundVector hit,int axis,int sign,WoundVector direction){hit=hit-p.center;hit=ClampVector(hit,p.size*-.5,p.size*.5);hit[axis]=sign*p.size[axis]/2;var inward=new WoundVector();inward[axis]=-sign;direction=direction.Length>1e-9?direction.Normal:inward;double dot=direction[axis]*-sign;if(dot<.5)direction=(direction+inward*(.85-dot)).Normal;return new NativeWoundAnchor{Part=p,Spec=new WoundCarveSpec{hit=hit,axis=axis,sign=sign,dir=direction,size=p.size,kind=w.kind,trauma=w.trauma,radius=w.radius,severity=w.severity,seed=NativeWoundCarve.Seed(w.id.ToString(System.Globalization.CultureInfo.InvariantCulture)),low=low,body=body,vertical=w.direction.y}};}
            List<NativeWoundAnchor> Along(WoundVector origin,WoundVector direction,WoundVector keep)
            {var hits=parts.Select(p=>(p,ray:RayBox(origin,direction,p.center-p.size*.5,p.center+p.size*.5))).Where(v=>v.ray.HasValue&&v.ray.Value.last>=0).OrderBy(v=>v.ray.Value.first).ToArray();if(hits.Length==0)return null;var first=hits[0];int axis=first.ray.Value.axis;var result=new List<NativeWoundAnchor>{On(first.p,origin+direction*first.ray.Value.first,axis,direction[axis]>0?-1:1,keep)};if(first.p.size[axis]<.05){foreach(var next in hits.Skip(1))if(next.ray.Value.first>=first.ray.Value.last-.01&&next.ray.Value.first<=first.ray.Value.last+.08){int a=next.ray.Value.axis;result.Add(On(next.p,origin+direction*next.ray.Value.first,a,direction[a]>0?-1:1,keep));break;}}return result;}
            var shot=Along(point-travel*.6,travel,travel);if(shot!=null)return shot;var nearest=parts.OrderBy(p=>(ClampVector(point,p.center-p.size*.5,p.center+p.size*.5)-point).Length+(Math.Min(p.size.x,Math.Min(p.size.y,p.size.z))<.05?.05:0)).First();var local=point-nearest.center;int face=0;double score=double.NegativeInfinity;for(int a=0;a<3;a++){double n=Math.Abs(local[a])/Math.Max(1e-6,nearest.size[a]/2);if(n>score){score=n;face=a;}}int side=local[face]>=0?1:-1;var spot=ClampVector(point,nearest.center-nearest.size*.5,nearest.center+nearest.size*.5);var dir=new WoundVector();dir[face]=-side;return Along(spot-dir*.3,dir,dir)??new List<NativeWoundAnchor>{On(nearest,spot,face,side,dir)};
        }

        /// <summary>The anchors of one wound on the exported parts `all` (the first is the part the shot met first).</summary>
        public static List<NativeWoundAnchor> Of(NativeWoundPart[] all,NativeWound w,bool low,WoundVector body)
        {
            var list=All(all,w,low,body);for(int i=0;i<list.Count;i++)list[i].First=i==0;return list;
        }
        /// <summary>The wounded parts in first-anchor order, each carved by its anchors in order (the Prototype's carve; each
        /// carve's cells are recorded, which changes nothing it returns).</summary>
        public static WoundCarveGroup[] Carve(IEnumerable<NativeWoundAnchor> anchors)
        {
            var groups=anchors.GroupBy(a=>a.Part.name).ToArray();var result=new WoundCarveGroup[groups.Length];
            for(int i=0;i<groups.Length;i++)
            {
                var d=groups[i].First().Part;var solids=d.solids.ToList();var g=new WoundCarveGroup{Part=d};
                foreach(var a in groups[i]){var cells=new WoundCarveCells();solids=NativeWoundCarve.Carve(solids,a.Spec,cells);g.Steps.Add(cells);g.First.Add(a.First);}
                g.Solids=solids;result[i]=g;
            }
            return result;
        }
    }

    /// <summary>How a sculpted body shows a wound in its own surface (each skin's WoundLook): torn cloth or breached armour,
    /// the layer under the surface, the bare edges among the broken rim and the conduits glowing in the accent below it.</summary>
    public readonly struct WoundSkinLook
    {
        /// <summary>Torn cloth (a coat) rather than breached armour plates.</summary>
        public readonly bool Cloth;
        /// <summary>The layer under the surface (linear): the dark undersuit under the plates, the coat's lining.</summary>
        public readonly Vector3 Inner;
        /// <summary>Bare edges among the broken rim (linear): scraped metal, frayed fibre.</summary>
        public readonly Vector3 Edge;
        /// <summary>A conduit in the inner layer (linear, tag 2: the pilot's accent) and its glow; level 0 for none.</summary>
        public readonly Vector3 Conduit;public readonly float ConduitLevel;
        /// <summary>The rim's own surface darkened by this (a gunshot or a bruise; Cut for a blade's clean edge).</summary>
        public readonly float Shade,CutShade;
        public WoundSkinLook(bool cloth,Vector3 inner,Vector3 edge,Vector3 conduit,float conduitLevel,float shade,float cutShade)
        {Cloth=cloth;Inner=inner;Edge=edge;Conduit=conduit;ConduitLevel=conduitLevel;Shade=shade;CutShade=cutShade;}
    }
    /// <summary>One mesh of a wounded body (bank frame, ready to upload as the body's parts are): a body part with the wounds
    /// cut out of it (Part, its index in the body), or a wound's inside (Part -1), drawn on Bone with body part Material's
    /// material.</summary>
    public sealed class WoundSkinMesh {public int Part=-1,Bone,Material=-1;public string Name;public readonly WeaponSection Mesh=new WeaponSection();}

    // A wound on a sculpted body (the Standard armour, the Sheldon Clothes): the Prototype's carve, cell for cell, cut into the
    // body's own mesh. Each carve (WoundCarveCells: the same shape, seed and cells the Prototype gets) is laid onto the body:
    //  - every column of its grid finds the body's outer surface along the carve's face axis (on the wounded part's joint),
    //    and the column's own surface layer of the exported part drops to it (a part that steps in, as the ribcage's lower
    //    half, drops from that step, not from its face);
    //  - the body's triangles in the hole and the rim are cut away (polygons clipped on the column and layer planes; a
    //    removed cell cuts no deeper than its exported part, so a thin plate keeps the body behind it);
    //  - the cells are drawn in the body's own colours: the rim is its broken surface (darkened, bare-edged, blood-flecked,
    //    pieces standing up), the layer below is its undersuit or lining with an accent conduit here and there, and the
    //    flesh below keeps the Prototype's colours;
    //  - the cut is closed: walls step down onto lower neighbours, a cut deeper than its cells gets a floor, a 3 mm skirt
    //    lines each cut side the body leaves open (a coat off its frame, a hollow plate), and a later wound that opens into
    //    an earlier one's hollow is lined with flesh;
    //  - a shot through a thin plate carves the part behind it too: under the plate's hole it drops by the plate's surface,
    //    so the hole runs on through; beside it the part lies on the body's surface as any carve.
    // Presentation only, deterministic and engine-free: nothing here is read by hit volumes, damage or rules, and the
    // Prototype keeps drawing NativeWoundCarve's boxes unchanged.
    public static class NativeWoundSkin
    {
        /// <summary>How far above the exported face a column looks for the body's surface (m): the relief and anything on the
        /// joint in front of the part (the Standard sternum plate over the ribcage), all cut over the hole.</summary>
        public const double Above=.05;
        private struct Vtx {public WoundVector P;public Vector3 N;public Vector4 C,E;}
        private static Vtx Lerp(in Vtx a,in Vtx b,double t)
        {
            float f=(float)t;return new Vtx{P=a.P+(b.P-a.P)*t,N=a.N+(b.N-a.N)*f,C=a.C+(b.C-a.C)*f,E=a.E+(b.E-a.E)*f};
        }
        private static double Dot(WoundVector a,WoundVector b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        private static WoundVector Cross(WoundVector a,WoundVector b)=>new WoundVector(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        private static WoundVector D(Vector3 v)=>new WoundVector(v.X,v.Y,v.Z);

        // The exported part's box frame (the carve's) against the bank frame of the body: q = X(A p + t), X the x mirror.
        private sealed class Frame
        {
            private readonly double[] a=new double[9],ai=new double[9];private readonly WoundVector t;
            /// <summary>Box coordinate `axis` of bank point q is Dot(Row[axis], q) + Off[axis].</summary>
            public readonly WoundVector[] Row=new WoundVector[3];public readonly double[] Off=new double[3];
            public Frame(NativeWoundPart d)
            {
                // matrix: column-major (Unity's m[i%4, i/4]), times Scale(1/size) as the Runtime's CarveMesh composes it.
                for(int r=0;r<3;r++)for(int c=0;c<3;c++)a[r*3+c]=d.matrix[c*4+r]/d.size[c];t=new WoundVector(d.matrix[12],d.matrix[13],d.matrix[14]);
                double det=a[0]*(a[4]*a[8]-a[5]*a[7])-a[1]*(a[3]*a[8]-a[5]*a[6])+a[2]*(a[3]*a[7]-a[4]*a[6]);if(Math.Abs(det)<1e-12)det=1e-12;
                ai[0]=(a[4]*a[8]-a[5]*a[7])/det;ai[1]=(a[2]*a[7]-a[1]*a[8])/det;ai[2]=(a[1]*a[5]-a[2]*a[4])/det;
                ai[3]=(a[5]*a[6]-a[3]*a[8])/det;ai[4]=(a[0]*a[8]-a[2]*a[6])/det;ai[5]=(a[2]*a[3]-a[0]*a[5])/det;
                ai[6]=(a[3]*a[7]-a[4]*a[6])/det;ai[7]=(a[1]*a[6]-a[0]*a[7])/det;ai[8]=(a[0]*a[4]-a[1]*a[3])/det;
                for(int r=0;r<3;r++){Row[r]=new WoundVector(-ai[r*3],ai[r*3+1],ai[r*3+2]);Off[r]=-(ai[r*3]*t.x+ai[r*3+1]*t.y+ai[r*3+2]*t.z);}
                for(int k=0;k<6;k++){var n=new WoundVector();n[k/2]=(k&1)!=0?1:-1;var bn=BankNormal(n);Normals[k]=new Vector3((float)bn.x,(float)bn.y,(float)bn.z);}
            }
            /// <summary>The bank-frame normals of the box faces -x, +x, -y, +y, -z, +z.</summary>
            public readonly Vector3[] Normals=new Vector3[6];
            public double Local(int axis,WoundVector q)=>Dot(Row[axis],q)+Off[axis];
            public WoundVector Bank(WoundVector p)
            {
                double x=a[0]*p.x+a[1]*p.y+a[2]*p.z+t.x,y=a[3]*p.x+a[4]*p.y+a[5]*p.z+t.y,z=a[6]*p.x+a[7]*p.y+a[8]*p.z+t.z;return new WoundVector(-x,y,z);
            }
            public WoundVector BankDirection(WoundVector d){double x=a[0]*d.x+a[1]*d.y+a[2]*d.z,y=a[3]*d.x+a[4]*d.y+a[5]*d.z,z=a[6]*d.x+a[7]*d.y+a[8]*d.z;return new WoundVector(-x,y,z);}
            public WoundVector BankNormal(WoundVector n){double x=ai[0]*n.x+ai[3]*n.y+ai[6]*n.z,y=ai[1]*n.x+ai[4]*n.y+ai[7]*n.z,z=ai[2]*n.x+ai[5]*n.y+ai[8]*n.z;return new WoundVector(-x,y,z).Normal;}
        }

        // One carve laid on the body: its columns' surface depth and paint, the cut intervals, the cells to draw.
        private sealed class Lay
        {
            public WoundCarveCells Cells;public Frame F;public int Size;public double[] Sigma;public int[] Top;public bool[] Under;public Vector4[] PaintC,PaintE;
            public List<(double lo,double hi)>[] Cuts;public int I0=int.MaxValue,I1=int.MinValue,J0=int.MaxValue,J1=int.MinValue;
            public int Column(int i,int j)=>(j+Cells.N)*Size+(i+Cells.N);
            public bool Inside(int i,int j)=>i>=-Cells.N&&i<=Cells.N&&j>=-Cells.N&&j<=Cells.N;
        }

        /// <summary>
        /// The body `body` (one tier of a sculpted skin) with the carved wounds `groups` cut in: a mesh for every body part the
        /// wounds touch (the part with its hole), then one for each wounded exported part's inside, coloured by `look`.
        /// Parts on other joints, and every part the cuts miss, are not listed (they draw as they are). `holes`, when given,
        /// receives every cell the cuts take out, where they lie on this body (a revive refills them: NativeReviveHolo);
        /// recording changes nothing the cut returns.
        /// </summary>
        public static List<WoundSkinMesh> Cut(PilotBodyMesh body,IReadOnlyList<WoundCarveGroup> groups,in WoundSkinLook look,List<WoundHole> holes=null)
        {
            var result=new List<WoundSkinMesh>();if(body?.Parts==null||groups==null)return result;
            var polys=new Dictionary<int,List<Vtx[]>>();var cut=new HashSet<int>();
            List<int> Candidates(NativeWoundPart d){var list=new List<int>();for(int p=0;p<body.Parts.Length;p++)if(body.Parts[p]!=null&&body.Parts[p].Bone==d.bone&&body.Parts[p].Mesh.Positions.Count>0)list.Add(p);return list;}
            // First the surface under every wound's first part; a part the shot reached through it (a thin plate's backing)
            // then drops by that same surface, so the two stay stacked as the Prototype carves them.
            var first=new Dictionary<uint,Lay>();
            foreach(var g in groups)
            {
                if(g.Part==null)continue;var candidates=Candidates(g.Part);if(candidates.Count==0)continue;var frame=new Frame(g.Part);
                for(int s=0;s<g.Steps.Count;s++)if(g.First[s]&&!first.ContainsKey(g.Steps[s].Seed)){var lay=Surface(body,candidates,frame,g.Steps[s],g.Part);if(lay!=null)first[g.Steps[s].Seed]=lay;}
            }
            foreach(var g in groups)
            {
                var d=g.Part;if(d==null)continue;var frame=new Frame(d);var candidates=Candidates(d);
                if(candidates.Count==0)continue;var inside=new WoundSkinMesh{Bone=d.bone,Name="wound "+d.name};
                foreach(int p in candidates)if(body.Parts[p].Name==d.name){inside.Material=p;break;}if(inside.Material<0)inside.Material=candidates[0];
                for(int s=0;s<g.Steps.Count;s++)
                {
                    var step=g.Steps[s];if(!first.TryGetValue(step.Seed,out var lay))continue;
                    if(!g.First[s]){lay=Behind(lay,frame,step,Surface(body,candidates,frame,step,d));if(lay==null)continue;}else if(lay.Cells!=step)continue;
                    var cells=Cells(lay,g.Steps,s,look,d,inside.Mesh,holes);if(cells==0)continue;
                    foreach(int p in candidates)
                    {
                        if(!polys.TryGetValue(p,out var list)){list=Polygons(body.Parts[p].Mesh);polys[p]=list;}
                        var next=new List<Vtx[]>(list.Count+64);bool changed=false;foreach(var poly in list)changed|=CutPolygon(poly,lay,next);
                        if(changed){polys[p]=next;cut.Add(p);}
                    }
                }
                if(inside.Mesh.Indices.Count>0)result.Add(inside);
            }
            var meshes=new List<WoundSkinMesh>();
            foreach(int p in cut.OrderBy(v=>v))
            {
                var m=new WoundSkinMesh{Part=p,Bone=body.Parts[p].Bone,Material=p,Name=body.Parts[p].Name};
                foreach(var poly in polys[p])Emit(m.Mesh,poly);meshes.Add(m);
            }
            meshes.AddRange(result);return meshes;
        }

        /// <summary>The Prototype's wound cells (its carve's own, which its boxes lose) as holes in its joints' bank frames.</summary>
        public static void PrototypeHoles(IReadOnlyList<WoundCarveGroup> groups,List<WoundHole> into)
        {
            if(groups==null||into==null)return;
            foreach(var g in groups){if(g?.Part==null)continue;var f=new Frame(g.Part);foreach(var step in g.Steps)foreach(var r in step.Removed)into.Add(Hole(f,g.Part.bone,r.cell,r.k*step.G));}
        }
        private static WoundHole Hole(Frame f,int bone,WoundSolid b,double depth)
        {
            var mid=(b.min+b.max)*.5;var c=f.Bank(mid);WoundVector Axis(int a){var d=new WoundVector();d[a]=(b.max[a]-b.min[a])*.5;return f.BankDirection(d);}
            var u=Axis(0);var v=Axis(1);var w=Axis(2);
            return new WoundHole{Bone=bone,Center=new Vector3((float)c.x,(float)c.y,(float)c.z),U=new Vector3((float)u.x,(float)u.y,(float)u.z),V=new Vector3((float)v.x,(float)v.y,(float)v.z),W=new Vector3((float)w.x,(float)w.y,(float)w.z),Depth=(float)depth};
        }
        private static List<Vtx[]> Polygons(WeaponSection m)
        {
            var list=new List<Vtx[]>(m.Indices.Count/3);
            Vtx V(int i)=>new Vtx{P=D(m.Positions[i]),N=m.Normals[i],C=m.Colors[i],E=m.Emission[i]};
            for(int k=0;k+2<m.Indices.Count;k+=3)list.Add(new[]{V(m.Indices[k]),V(m.Indices[k+1]),V(m.Indices[k+2])});
            return list;
        }
        private static void Emit(WeaponSection m,Vtx[] poly)
        {
            if(poly.Length<3)return;int start=m.Positions.Count;
            foreach(var v in poly){m.Positions.Add(new Vector3((float)v.P.x,(float)v.P.y,(float)v.P.z));m.Normals.Add(v.N.LengthSquared()>1e-12f?Vector3.Normalize(v.N):Vector3.UnitY);m.Colors.Add(v.C);m.Emission.Add(v.E);}
            for(int i=1;i+1<poly.Length;i++){m.Indices.Add(start);m.Indices.Add(start+i);m.Indices.Add(start+i+1);}
        }

        // ---- the body's surface under each column ----
        private static Lay Surface(PilotBodyMesh body,List<int> candidates,Frame f,WoundCarveCells c,NativeWoundPart d)
        {
            int n=c.N;var lay=new Lay{Cells=c,F=f,Size=2*n+1};int count=lay.Size*lay.Size;
            foreach(var r in c.Removed){lay.I0=Math.Min(lay.I0,r.i);lay.I1=Math.Max(lay.I1,r.i);lay.J0=Math.Min(lay.J0,r.j);lay.J1=Math.Max(lay.J1,r.j);}
            foreach(var r in c.Laid){lay.I0=Math.Min(lay.I0,r.i);lay.I1=Math.Max(lay.I1,r.i);lay.J0=Math.Min(lay.J0,r.j);lay.J1=Math.Max(lay.J1,r.j);}
            if(lay.I0>lay.I1)return null;
            lay.Sigma=new double[count];lay.PaintC=new Vector4[count];lay.PaintE=new Vector4[count];for(int k=0;k<count;k++)lay.Sigma[k]=double.NaN;
            int ua=c.Ua,va=c.Va,ax=c.Axis,sign=c.Sign;double g=c.G,thick=d.size[ax];
            int i0=Math.Max(-n,lay.I0-1),i1=Math.Min(n,lay.I1+1),j0=Math.Max(-n,lay.J0-1),j1=Math.Min(n,lay.J1+1);
            double u0=c.Hu+(i0-.5)*g,u1=c.Hu+(i1+.5)*g,v0=c.Hv+(j0-.5)*g,v1=c.Hv+(j1+.5)*g;
            // The joint's front-facing triangles over the sampled columns (bank coordinates), bucketed by the columns they span.
            var inward=new WoundVector();inward[ax]=-sign;var dir=f.BankDirection(inward).Normal;double reach=Above+thick+.02;
            var tris=new List<(WoundVector a,WoundVector b,WoundVector c,int part,int index)>();int bw=i1-i0+1;var buckets=new List<int>[bw*(j1-j0+1)];
            foreach(int p in candidates)
            {
                var m=body.Parts[p].Mesh;
                for(int k=0;k+2<m.Indices.Count;k+=3)
                {
                    var A=D(m.Positions[m.Indices[k]]);var B=D(m.Positions[m.Indices[k+1]]);var C=D(m.Positions[m.Indices[k+2]]);
                    double au=f.Local(ua,A),bu=f.Local(ua,B),cu=f.Local(ua,C),av=f.Local(va,A),bv=f.Local(va,B),cv=f.Local(va,C);
                    if(Math.Max(au,Math.Max(bu,cu))<u0||Math.Min(au,Math.Min(bu,cu))>u1||Math.Max(av,Math.Max(bv,cv))<v0||Math.Min(av,Math.Min(bv,cv))>v1)continue;
                    if(Dot(Cross(B-A,C-A),dir)>=0)continue;
                    int ti0=Math.Max(i0,(int)Math.Floor((Math.Min(au,Math.Min(bu,cu))-c.Hu)/g-1e-6)),ti1=Math.Min(i1,(int)Math.Ceiling((Math.Max(au,Math.Max(bu,cu))-c.Hu)/g+1e-6));
                    int tj0=Math.Max(j0,(int)Math.Floor((Math.Min(av,Math.Min(bv,cv))-c.Hv)/g-1e-6)),tj1=Math.Min(j1,(int)Math.Ceiling((Math.Max(av,Math.Max(bv,cv))-c.Hv)/g+1e-6));
                    int t=tris.Count;tris.Add((A,B,C,p,k));for(int tj=tj0;tj<=tj1;tj++)for(int ti=ti0;ti<=ti1;ti++)(buckets[(tj-j0)*bw+(ti-i0)]??=new List<int>()).Add(t);
                }
            }
            for(int j=j0;j<=j1;j++)for(int i=i0;i<=i1;i++)
            {
                var o=new WoundVector();o[ua]=c.Hu+i*g;o[va]=c.Hv+j*g;o[ax]=c.Face+sign*Above;var origin=f.Bank(o);
                double best=reach;int hit=-1;double hu=0,hv=0;var bucket=buckets[(j-j0)*bw+(i-i0)];if(bucket==null)continue;
                foreach(int t in bucket)
                {
                    var tri=tris[t];var e1=tri.b-tri.a;var e2=tri.c-tri.a;
                    var pv=Cross(dir,e2);double det=Dot(e1,pv);if(Math.Abs(det)<1e-14)continue;double inv=1/det;var tv=origin-tri.a;double bu=Dot(tv,pv)*inv;if(bu<-1e-9||bu>1+1e-9)continue;
                    var qv=Cross(tv,e1);double bv=Dot(dir,qv)*inv;if(bv<-1e-9||bu+bv>1+1e-9)continue;double dist=Dot(e2,qv)*inv;if(dist<0||dist>=best)continue;
                    best=dist;hit=t;hu=bu;hv=bv;
                }
                if(hit<0)continue;int col=lay.Column(i,j);lay.Sigma[col]=best-Above;
                var h=tris[hit];var mesh=body.Parts[h.part].Mesh;int ia=mesh.Indices[h.index],ib=mesh.Indices[h.index+1],ic=mesh.Indices[h.index+2];float wa=(float)(1-hu-hv),wb=(float)hu,wc=(float)hv;
                lay.PaintC[col]=mesh.Colors[ia]*wa+mesh.Colors[ib]*wb+mesh.Colors[ic]*wc;var e=mesh.Emission[ia]*wa+mesh.Emission[ib]*wb+mesh.Emission[ic]*wc;e.W=mesh.Emission[ia].W;lay.PaintE[col]=e;
            }
            // The exported part's own surface layer under each column (the first whose cells lie in its boxes: 0 at its face,
            // deeper where the part steps in, as the ribcage's lower half): that layer, not the face, drops to the body's surface.
            lay.Top=new int[count];
            for(int j=-n;j<=n;j++)for(int i=-n;i<=n;i++)
            {
                double u=c.Hu+i*g,v=c.Hv+j*g,outer=double.NaN;
                foreach(var b in d.solids)if(u>b.min[ua]&&u<b.max[ua]&&v>b.min[va]&&v<b.max[va]){double w=sign>0?b.max[ax]:b.min[ax];if(double.IsNaN(outer)||sign*(w-outer)>0)outer=w;}
                if(!double.IsNaN(outer))lay.Top[lay.Column(i,j)]=Math.Max(0,(int)Math.Ceiling(sign*(c.Face-outer)/g-.5-1e-6));
            }
            for(int k=0;k<count;k++)if(!double.IsNaN(lay.Sigma[k]))lay.Sigma[k]-=lay.Top[k]*g;
            // Columns that see no surface take their nearest neighbour's; none at all: this body has no surface here.
            var queue=new Queue<int>();for(int k=0;k<count;k++)if(!double.IsNaN(lay.Sigma[k]))queue.Enqueue(k);if(queue.Count==0)return null;
            while(queue.Count>0)
            {
                int k=queue.Dequeue(),ci=k%lay.Size,cj=k/lay.Size;
                foreach(var (di,dj) in Steps4){int ni=ci+di,nj=cj+dj;if(ni<0||nj<0||ni>=lay.Size||nj>=lay.Size)continue;int nk=nj*lay.Size+ni;if(!double.IsNaN(lay.Sigma[nk]))continue;lay.Sigma[nk]=lay.Sigma[k];lay.PaintC[nk]=lay.PaintC[k];lay.PaintE[nk]=lay.PaintE[k];queue.Enqueue(nk);}
            }
            // A column far off the hit's own (a bevel or an edge seen past the silhouette) stays within two cells of it.
            double centre=lay.Sigma[lay.Column(0,0)];for(int k=0;k<count;k++)lay.Sigma[k]=Math.Max(centre-2*g,Math.Min(centre+2*g,lay.Sigma[k]));
            lay.Cuts=new List<(double,double)>[count];return lay;
        }
        /// <summary>
        /// The carve of a part the shot reached through its first one (`front`, laid on the body's surface). Under the
        /// front's hole its columns drop by the front's surface depth, so the hole runs on through and those cells are all
        /// inside the wound (no rim of the body's surface, nothing cut above them); beside it (where the Prototype shows this
        /// part's own face) they lie on the body's surface as any carve's (`own`, or none where the body has no surface
        /// there). Null when the two carves do not share a face axis.
        /// </summary>
        private static Lay Behind(Lay front,Frame f,WoundCarveCells c,Lay own)
        {
            var a=front.Cells;if(a.Axis!=c.Axis||a.Sign!=c.Sign)return null;int n=c.N;
            var hole=new HashSet<(int,int)>();foreach(var r in a.Removed)hole.Add((r.i,r.j));foreach(var r in a.Laid)hole.Add((r.i,r.j));
            var lay=own;
            if(lay==null)
            {
                lay=new Lay{Cells=c,F=f,Size=2*n+1};int count0=lay.Size*lay.Size;
                foreach(var r in c.Removed){lay.I0=Math.Min(lay.I0,r.i);lay.I1=Math.Max(lay.I1,r.i);lay.J0=Math.Min(lay.J0,r.j);lay.J1=Math.Max(lay.J1,r.j);}
                foreach(var r in c.Laid){lay.I0=Math.Min(lay.I0,r.i);lay.I1=Math.Max(lay.I1,r.i);lay.J0=Math.Min(lay.J0,r.j);lay.J1=Math.Max(lay.J1,r.j);}
                if(lay.I0>lay.I1)return null;lay.Sigma=new double[count0];lay.Top=new int[count0];lay.PaintC=new Vector4[count0];lay.PaintE=new Vector4[count0];lay.Cuts=new List<(double,double)>[count0];
            }
            lay.Under=new bool[lay.Size*lay.Size];
            for(int j=-n;j<=n;j++)for(int i=-n;i<=n;i++)
            {
                var p=new WoundVector();p[c.Ua]=c.Hu+i*c.G;p[c.Va]=c.Hv+j*c.G;p[c.Axis]=c.Face;var q=f.Bank(p);
                int fi=(int)Math.Round((front.F.Local(a.Ua,q)-a.Hu)/a.G),fj=(int)Math.Round((front.F.Local(a.Va,q)-a.Hv)/a.G);
                if(own!=null&&!hole.Contains((fi,fj)))continue;
                fi=Math.Max(-a.N,Math.Min(a.N,fi));fj=Math.Max(-a.N,Math.Min(a.N,fj));
                int from=front.Column(fi,fj),to=lay.Column(i,j);lay.Under[to]=true;lay.Top[to]=0;lay.Sigma[to]=front.Sigma[from];lay.PaintC[to]=front.PaintC[from];lay.PaintE[to]=front.PaintE[from];
            }
            return lay;
        }
        private static readonly (int,int)[] Steps4={(1,0),(-1,0),(0,1),(0,-1)},Steps8={(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)};
        /// <summary>The blood flecks on a body's broken rim (linear; the Prototype's rim mixes it into its grey).</summary>
        private static readonly Vector3 Blood=new Vector3(.23f,.006f,.011f);

        // ---- the cells, dropped to the surface, recoloured and drawn; their columns' cut intervals ----
        // A later wound on the same part re-lays an earlier one's cells it borders (Old) and drops the ones in its grid: on a
        // body each carve draws its own cells, less those a later carve took out, so the earlier hole stays lined.
        private static bool Taken(WoundSolid cell,IReadOnlyList<WoundCarveCells> steps,int after)
        {
            var mid=(cell.min+cell.max)*.5;
            for(int t=after+1;t<steps.Count;t++)foreach(var r in steps[t].Removed){var b=r.cell;if(mid.x>b.min.x&&mid.x<b.max.x&&mid.y>b.min.y&&mid.y<b.max.y&&mid.z>b.min.z&&mid.z<b.max.z)return true;}
            return false;
        }
        private static bool InSolid(WoundVector p,WoundSolid[] solids){foreach(var b in solids)if(p.x>b.min.x&&p.x<b.max.x&&p.y>b.min.y&&p.y<b.max.y&&p.z>b.min.z&&p.z<b.max.z)return true;return false;}
        private static bool Earlier(WoundVector mid,IReadOnlyList<WoundCarveCells> steps,int before)
        {
            for(int t=0;t<before;t++)foreach(var r in steps[t].Removed){var b=r.cell;if(mid.x>b.min.x&&mid.x<b.max.x&&mid.y>b.min.y&&mid.y<b.max.y&&mid.z>b.min.z&&mid.z<b.max.z)return true;}
            return false;
        }
        private static readonly (int,int,int)[] Steps6={(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)};
        private static readonly Vector3 DeepFlesh=new Vector3(.078f,.002f,.005f);
        private static int Cells(Lay lay,IReadOnlyList<WoundCarveCells> steps,int step,in WoundSkinLook look,NativeWoundPart part,WeaponSection into,List<WoundHole> holes=null)
        {
            var c=lay.Cells;int ax=c.Axis,sign=c.Sign;double g=c.G;bool cut=c.Kind=="cut",gun=c.Kind=="puncture"||c.Kind=="cavity",bruise=c.Kind=="bruise";int drawn=0;
            var top=new bool[lay.Size*lay.Size];var drawnW=new List<(double lo,double hi)>[lay.Size*lay.Size];
            void Draw(int i,int j,WoundSolid box,Vector3 color,Vector3 glow,float tag)
            {
                (drawnW[lay.Column(i,j)]??=new List<(double,double)>()).Add((box.min[ax],box.max[ax]));Box(into,lay.F,box,new Vector4(color,1),new Vector4(glow,tag));drawn++;
            }
            void Interval(int i,int j,WoundSolid box,int k)
            {
                int col=lay.Column(i,j);(lay.Cuts[col]??=new List<(double,double)>()).Add((box.min[ax],box.max[ax]));if(k<=lay.Top[col])top[col]=true;
            }
            WoundSolid Shift(WoundSolid box,int i,int j,bool extend)
            {
                double s=lay.Sigma[lay.Column(i,j)];var min=box.min;var max=box.max;min[ax]-=sign*s;max[ax]-=sign*s;
                if(extend)
                {
                    // Down to the deepest neighbour's surface, so a wall meets the floor beside it.
                    double drop=0;foreach(var (di,dj) in Steps4){int ni=i+di,nj=j+dj;if(!lay.Inside(ni,nj))continue;drop=Math.Max(drop,lay.Sigma[lay.Column(ni,nj)]-s);}
                    if(drop>0){if(sign>0)min[ax]-=drop;else max[ax]+=drop;}
                }
                return new WoundSolid{min=min,max=max,color=box.color,flesh=box.flesh};
            }
            // A removed cell cuts no deeper than the exported part it was taken from (a 3 cm bruise cell in a 2 cm plate), so
            // the body under a thin plate stays.
            double back=-.5*part.size[ax]*sign;
            foreach(var r in c.Removed)
            {
                var cell=r.cell;if(sign>0?cell.min[ax]<back:cell.max[ax]>back){var min=cell.min;var max=cell.max;if(sign>0)min[ax]=Math.Min(max[ax],back);else max[ax]=Math.Max(min[ax],back);cell=new WoundSolid{min=min,max=max};}
                var shifted=Shift(cell,r.i,r.j,false);Interval(r.i,r.j,shifted,r.k);holes?.Add(Hole(lay.F,part.bone,shifted,r.k*g));
            }
            uint seed=c.Seed^0xa5b35705u;
            foreach(var l in c.Laid)
            {
                if(l.role==WoundCarveCells.Old||Taken(l.cell,steps,step))continue;var box=Shift(l.cell,l.i,l.j,true);int col=lay.Column(l.i,l.j);
                double h=NativeWoundCarve.Hash(seed,l.i,l.j,l.k),h2=NativeWoundCarve.Hash(seed^0x3c6ef372u,l.i,l.j,l.k);
                Vector4 paint=lay.PaintC[col],paintE=lay.PaintE[col];var flesh=new Vector3((float)l.cell.color.x,(float)l.cell.color.y,(float)l.cell.color.z);
                Vector3 color;Vector3 glow;float tag=0;
                if(lay.Under!=null&&lay.Under[col]){color=l.role==WoundCarveCells.Rim||l.role==WoundCarveCells.PlainRim?Blood:flesh;glow=color*.3f;}
                else if(l.role==WoundCarveCells.Rim||l.role==WoundCarveCells.PlainRim||l.role==WoundCarveCells.Inside&&!bruise&&l.k<=lay.Top[col])
                {
                    // The broken surface: blood-flecked, some bare edge, the rest the body's own paint darkened; pieces stand up (grown
                    // out, not moved, so no gap opens under them).
                    float shade=cut?look.CutShade:look.Shade;
                    if(l.role!=WoundCarveCells.PlainRim&&h<(look.Cloth?.22:.26)){color=Blood;glow=Blood*.3f;}
                    else if(!bruise&&h<(look.Cloth?.48:.4)){color=look.Edge;glow=look.Edge*.1f;}
                    else{color=new Vector3(paint.X,paint.Y,paint.Z)*shade;glow=new Vector3(paintE.X,paintE.Y,paintE.Z)*shade;tag=paintE.W;}
                    double lift=look.Cloth?(h2>.6?(.08+.3*(h2-.6)/.4)*g:0):gun&&h2>.7?(.1+.25*(h2-.7)/.3)*g:0;
                    if(lift>0){var min=box.min;var max=box.max;if(sign>0)max[ax]+=lift;else min[ax]-=lift;box=new WoundSolid{min=min,max=max};}
                }
                else if(l.role==WoundCarveCells.Inside&&bruise){color=new Vector3(paint.X,paint.Y,paint.Z)*.5f;glow=new Vector3(paintE.X,paintE.Y,paintE.Z)*.5f;tag=paintE.W;}
                else if(l.role==WoundCarveCells.Inside&&l.k==lay.Top[col]+1&&h>=.25)
                {
                    // The layer under the surface: the undersuit or the lining, with a conduit glowing in the accent here and there.
                    if(!look.Cloth&&look.ConduitLevel>0&&h>.88){color=look.Conduit;glow=look.Conduit*look.ConduitLevel;tag=2;}
                    else{color=look.Inner;glow=look.Inner*.12f;}
                }
                else{color=flesh;glow=flesh*.3f;}
                Interval(l.i,l.j,box,l.k);Draw(l.i,l.j,box,color,glow,tag);
            }
            // An earlier carve leaves the part hollow inside its grid (the Prototype draws only the cells it can show and the
            // boxes round them); where this hole opens into that hollow (inside the exported boxes, not taken by an earlier
            // hole), the body has nothing there, so the hollow's cells next to the hole are laid as deep flesh.
            if(step>0)
            {
                var have=new HashSet<(int,int,int)>();foreach(var r in c.Removed)have.Add((r.i,r.j,r.k));foreach(var l in c.Laid)have.Add((l.i,l.j,l.k));
                double half=g*.5;
                foreach(var r in c.Removed)foreach(var (di,dj,dk) in Steps6)
                {
                    int i=r.i+di,j=r.j+dj,k=r.k+dk;if(k<0||!lay.Inside(i,j)||!have.Add((i,j,k)))continue;
                    var mid=new WoundVector();mid[c.Ua]=c.Hu+i*g;mid[c.Va]=c.Hv+j*g;mid[ax]=c.Face-sign*(k+.5)*g;
                    if(!InSolid(mid,part.solids)||Earlier(mid,steps,step))continue;
                    var min=new WoundVector();var max=new WoundVector();for(int a=0;a<3;a++){min[a]=mid[a]-half;max[a]=mid[a]+half;}
                    var box=Shift(new WoundSolid{min=min,max=max},i,j,true);Interval(i,j,box,k);Draw(i,j,box,DeepFlesh,DeepFlesh*.3f,0);
                }
            }
            // Each column's cuts merged; a column with a face-layer cell is cut clear of anything above it (relief, plates).
            for(int k=0;k<lay.Cuts.Length;k++)
            {
                var list=lay.Cuts[k];if(list==null)continue;
                if(top[k]&&(lay.Under==null||!lay.Under[k])){double outer=sign>0?double.NegativeInfinity:double.PositiveInfinity;foreach(var v in list)outer=sign>0?Math.Max(outer,v.hi):Math.Min(outer,v.lo);list.Add(sign>0?(outer,outer+Above):(outer-Above,outer));}
                list.Sort((a,b)=>a.lo.CompareTo(b.lo));var merged=new List<(double,double)>();
                foreach(var v in list){if(merged.Count>0&&v.lo<=merged[merged.Count-1].Item2+1e-7){var last=merged[merged.Count-1];merged[merged.Count-1]=(last.Item1,Math.Max(last.Item2,v.hi));}else merged.Add(v);}
                lay.Cuts[k]=merged;
            }
            // Closing the cut. A column cut open from the surface but deeper than its deepest cell gets a floor; an uncut
            // column beside it gets a skirt: a 3 mm slab against the shared side (a post at a shared corner), from just under
            // the lower of the two surfaces to the cut's bottom (the body's own cut edge is open: the coat hangs off the
            // frame, a plate is hollow), so no crack shows through and nothing lies on the body's surface to flicker.
            const double slab=.003,under=.0012;
            double SurfaceW(int col)=>c.Face-sign*(lay.Sigma[col]+lay.Top[col]*g);
            WoundSolid Square(int i,int j,double w0,double w1)
            {
                var min=new WoundVector();var max=new WoundVector();min[c.Ua]=c.Hu+(i-.5)*g;max[c.Ua]=c.Hu+(i+.5)*g;min[c.Va]=c.Hv+(j-.5)*g;max[c.Va]=c.Hv+(j+.5)*g;min[ax]=Math.Min(w0,w1);max[ax]=Math.Max(w0,w1);
                return new WoundSolid{min=min,max=max};
            }
            for(int j=-c.N;j<=c.N;j++)for(int i=-c.N;i<=c.N;i++)
            {
                int k=lay.Column(i,j);var list=lay.Cuts[k];if(list==null||list.Count==0||!top[k]||lay.Under!=null&&lay.Under[k])continue;
                var outerSeg=sign>0?list[list.Count-1]:list[0];double inner=sign>0?outerSeg.Item1:outerSeg.Item2;bool covered=false;
                if(drawnW[k]!=null)foreach(var d in drawnW[k])if(d.lo<=inner+1e-6&&d.hi>=inner-1e-6){covered=true;break;}
                if(!covered){Draw(i,j,Square(i,j,inner,inner-sign*g),DeepFlesh,DeepFlesh*.3f,0);}
                foreach(var (di,dj) in Steps8)
                {
                    int ni=i+di,nj=j+dj;if(!lay.Inside(ni,nj))continue;int nk=lay.Column(ni,nj);if(lay.Cuts[nk]!=null)continue;
                    double a=SurfaceW(k),b=SurfaceW(nk),wTop=(sign>0?Math.Min(a,b):Math.Max(a,b))-sign*under;if((wTop-inner)*sign<=1e-6)continue;
                    var box=Square(ni,nj,inner,wTop);var min=box.min;var max=box.max;
                    if(di>0)max[c.Ua]=min[c.Ua]+slab;else if(di<0)min[c.Ua]=max[c.Ua]-slab;
                    if(dj>0)max[c.Va]=min[c.Va]+slab;else if(dj<0)min[c.Va]=max[c.Va]-slab;
                    Draw(ni,nj,new WoundSolid{min=min,max=max},look.Inner,look.Inner*.12f,0);
                }
            }
            return drawn;
        }
        // A cell's six faces (corner bits: 1 the max on the first axis, 2 the second, 4 the third), each wound out by the
        // cross product of its edges in the bank frame (the mirror is checked once per frame).
        private static readonly int[][] Faces={new[]{0,4,6,2},new[]{1,3,7,5},new[]{0,1,5,4},new[]{2,6,7,3},new[]{0,2,3,1},new[]{4,5,7,6}};
        private static void Box(WeaponSection m,Frame f,WoundSolid b,Vector4 color,Vector4 emission)
        {
            var q=new Vector3[8];
            for(int k=0;k<8;k++){var p=new WoundVector((k&1)!=0?b.max.x:b.min.x,(k&2)!=0?b.max.y:b.min.y,(k&4)!=0?b.max.z:b.min.z);var w=f.Bank(p);q[k]=new Vector3((float)w.x,(float)w.y,(float)w.z);}
            for(int face=0;face<6;face++)
            {
                var n=f.Normals[face];var idx=Faces[face];int start=m.Positions.Count;
                Vector3 a=q[idx[0]],bq=q[idx[1]],c=q[idx[2]];bool flip=Vector3.Dot(Vector3.Cross(bq-a,c-a),n)<0;
                for(int k=0;k<4;k++){m.Positions.Add(q[idx[flip?(4-k)%4:k]]);m.Normals.Add(n);m.Colors.Add(color);m.Emission.Add(emission);}
                m.Indices.Add(start);m.Indices.Add(start+1);m.Indices.Add(start+2);m.Indices.Add(start);m.Indices.Add(start+2);m.Indices.Add(start+3);
            }
        }

        // ---- cutting a polygon: halves on the column planes until one column, then the cut layers away ----
        private static bool CutPolygon(Vtx[] poly,Lay lay,List<Vtx[]> into,int depth=0)
        {
            var c=lay.Cells;var f=lay.F;int ua=c.Ua,va=c.Va,ax=c.Axis;double g=c.G;
            double umin=double.PositiveInfinity,umax=double.NegativeInfinity,vmin=umin,vmax=umax,wmin=umin,wmax=umax;
            foreach(var p in poly){double u=f.Local(ua,p.P),v=f.Local(va,p.P),w=f.Local(ax,p.P);umin=Math.Min(umin,u);umax=Math.Max(umax,u);vmin=Math.Min(vmin,v);vmax=Math.Max(vmax,v);wmin=Math.Min(wmin,w);wmax=Math.Max(wmax,w);}
            const double eps=1e-7;
            int ri0=(int)Math.Floor((umin-c.Hu)/g+.5+eps),ri1=(int)Math.Floor((umax-c.Hu)/g+.5-eps),rj0=(int)Math.Floor((vmin-c.Hv)/g+.5+eps),rj1=(int)Math.Floor((vmax-c.Hv)/g+.5-eps);
            int i0=Math.Max(lay.I0,ri0),i1=Math.Min(lay.I1,ri1),j0=Math.Max(lay.J0,rj0),j1=Math.Min(lay.J1,rj1);
            bool any=false;
            for(int j=j0;j<=j1&&!any;j++)for(int i=i0;i<=i1&&!any;i++){var cuts=lay.Cuts[lay.Column(i,j)];if(cuts==null)continue;foreach(var v in cuts)if(v.hi>wmin+eps&&v.lo<wmax-eps){any=true;break;}}
            if(!any||depth>64){into.Add(poly);return false;}
            // What lies past the cut columns is split off first and kept whole, so a cut never runs past its columns.
            if(ri0<i0)return Halve(poly,lay,into,depth,f.Row[ua],f.Off[ua]-(c.Hu+(i0-.5)*g));
            if(ri1>i1)return Halve(poly,lay,into,depth,f.Row[ua],f.Off[ua]-(c.Hu+(i1+.5)*g));
            if(rj0<j0)return Halve(poly,lay,into,depth,f.Row[va],f.Off[va]-(c.Hv+(j0-.5)*g));
            if(rj1>j1)return Halve(poly,lay,into,depth,f.Row[va],f.Off[va]-(c.Hv+(j1+.5)*g));
            if(i1>i0)return Halve(poly,lay,into,depth,f.Row[ua],f.Off[ua]-(c.Hu+(i0+(i1-i0)/2+.5)*g));
            if(j1>j0)return Halve(poly,lay,into,depth,f.Row[va],f.Off[va]-(c.Hv+(j0+(j1-j0)/2+.5)*g));
            var pieces=new List<Vtx[]>{poly};
            foreach(var v in lay.Cuts[lay.Column(i0,j0)])
            {
                var next=new List<Vtx[]>();
                foreach(var piece in pieces)
                {
                    Split(piece,f.Row[ax],f.Off[ax]-v.lo,out var below,out var rest);if(below!=null)next.Add(below);
                    if(rest==null)continue;Split(rest,f.Row[ax],f.Off[ax]-v.hi,out _,out var above);if(above!=null)next.Add(above);
                }
                pieces=next;
            }
            foreach(var piece in pieces)if(Area(piece)>1e-11)into.Add(piece);
            return true;
        }
        private static bool Halve(Vtx[] poly,Lay lay,List<Vtx[]> into,int depth,WoundVector row,double off)
        {
            Split(poly,row,off,out var a,out var b);if(a==null||b==null){into.Add(poly);return false;}
            bool cut=CutPolygon(a,lay,into,depth+1);cut|=CutPolygon(b,lay,into,depth+1);return cut;
        }
        /// <summary>Splits a convex polygon by the plane Dot(row, q) + off = 0 into its negative and positive sides (null when empty).</summary>
        private static void Split(Vtx[] poly,WoundVector row,double off,out Vtx[] below,out Vtx[] above)
        {
            int n=poly.Length;var d=new double[n];bool neg=false,pos=false;for(int k=0;k<n;k++){d[k]=Dot(row,poly[k].P)+off;if(d[k]<-1e-9)neg=true;else if(d[k]>1e-9)pos=true;}
            if(!pos){below=poly;above=null;return;}if(!neg){below=null;above=poly;return;}
            var a=new List<Vtx>(n+2);var b=new List<Vtx>(n+2);
            for(int k=0;k<n;k++)
            {
                int l=(k+1)%n;var p=poly[k];double dp=d[k],dq=d[l];
                if(dp<=1e-9)a.Add(p);if(dp>=-1e-9)b.Add(p);
                if(dp<-1e-9&&dq>1e-9||dp>1e-9&&dq<-1e-9){var x=Lerp(p,poly[l],dp/(dp-dq));a.Add(x);b.Add(x);}
            }
            below=a.Count>=3?a.ToArray():null;above=b.Count>=3?b.ToArray():null;
        }
        private static double Area(Vtx[] poly){var s=new WoundVector();for(int k=1;k+1<poly.Length;k++)s=s+Cross(poly[k].P-poly[0].P,poly[k+1].P-poly[0].P);return .5*s.Length;}
    }
}
