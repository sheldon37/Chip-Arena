using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;
using NVector4=System.Numerics.Vector4;

namespace ChipArena.UnityConversion
{
    // A model corpse cut in its shader (owner 2026-10-28: "Can we improve gib slicing? (Kinda like how we did for voxel, we
    // "slice" right on the trail of a hit) Think like how we did the saw for the factory level. Also, prevent fps drops during
    // enemy kill"). No mesh is copied: the toon (NativeVisualBodyToon.shader, and the hologram's NativeWeaponGhost.shader) cuts
    // each body per vertex by a keep value read by the vertex's index and interpolated across each triangle
    // (NativeVisualBodyFx.cginc CutKeep).
    //  - A split body (a blade's killing blow, a Factory saw; NativeBisectPresentation.cs): both halves wear the whole model,
    //    each keeping its side of the bisect's plane, so the cut runs straight through skin, clothes and hair where the blade
    //    went. Per vertex, from the death pose (the corpse's bind): its signed distance from the plane, or half 0 where its body
    //    is outside the request's mask (a head or limb cut leaves the rest whole); each half's bones ride its own parts
    //    (VisualCorpseFor), so what of a part lies past the plane goes with the part on that side. Each half's face is capped as
    //    the voxel halves are (Core NativeBisect.CapOf: the section's union, its rim in the shell's colour, then flesh in layers;
    //    the two faces mirror each other), each cap piece on the crossed bone nearest it, with the bones the plane crosses as
    //    stubs (Core NativeModelGore). The skinning, the section and its cap are worked out on a worker while the halves hold on
    //    the blade: the second half shows once they are in (the first draws the whole body till then), and the halves wait for
    //    them before they part.
    //  - A body severed at its joints (a cut or crushed part, a smash; VisualCorpseSever): one body, keeping each vertex all of
    //    whose weight is on one piece's bones (none on a crushed part's), so no skin stretches between pieces as they part; each
    //    hole is capped (skin rim, flesh to a dark middle) on the piece's bone nearest it, the joint's bones as stubs
    //    (VisualCorpseBones). The keep values go on at once, the caps from the worker a few frames later.
    // A body the cut cannot read (no readable mesh, no float texture) is cut by mesh copies instead (VisualSever). Presentation
    // only.
    public sealed partial class ArenaPresentation
    {
        // A mesh's data for the cut, read once per mesh: rest positions, four bone weights, bind poses, triangles and each blend
        // shape frame's deltas as they are needed; null when the cut cannot use it (unreadable, unweighted, or a submesh drawn
        // from a base vertex, which the vertex index would not count).
        private sealed class CutMesh{public Vector3[] At;public BoneWeight[] Weights;public Matrix4x4[] Bind;public int[] Tris;public readonly Dictionary<int,Vector3[]> Frames=new Dictionary<int,Vector3[]>();}
        private static readonly Dictionary<Mesh,CutMesh> cutMeshes=new Dictionary<Mesh,CutMesh>();
        // One skinned renderer's share: its mesh, its bones' skinning in the death pose, whether the plane may cross each bone's
        // body (a split) or each vertex's piece (-1: none; a sever), its worn blend shapes (a frame's deltas and their share), its
        // index among the body's renderers, its first texel and its shell's paint (linear).
        private sealed class CutPart{public CutMesh Mesh;public Matrix4x4[] Skin;public bool[] Masked;public int[] Piece;public List<(Vector3[] delta,float share)> Shapes;public int Index,Base;public NVector4 Paint;
            // The skeleton's (NativeVisualSkeletonPresentation.cs): each vertex's kind, and each of its bones' index in the corpse's bones.
            public byte[] Kinds;public int[] Visual;}
        // One body's caps (world, death pose): positions, normals, colours (linear; alpha the share they glow by), triangles, and
        // each triangle's bone.
        private sealed class CapSet
        {
            public readonly List<Vector3> At=new List<Vector3>(),Normal=new List<Vector3>();public readonly List<Color> Color=new List<Color>();public readonly List<int> Tris=new List<int>(),Bone=new List<int>();
            // A cap as made (half 0's face) or mirrored (half 1's), each triangle on `bone` of its middle.
            public void Add(WeaponSection cap,float glow,Func<Vector3,int> bone,bool mirror)
            {
                int first=At.Count;
                for(int i=0;i<cap.Positions.Count;i++){var p=cap.Positions[i];var q=cap.Normals[i];var c=cap.Colors[i];At.Add(new Vector3(p.X,p.Y,p.Z));Normal.Add(mirror?new Vector3(-q.X,-q.Y,-q.Z):new Vector3(q.X,q.Y,q.Z));Color.Add(new Color(c.X,c.Y,c.Z,glow));}
                for(int t=0;t+2<cap.Indices.Count;t+=3)
                {
                    int a=first+cap.Indices[t],b=first+cap.Indices[t+1],c=first+cap.Indices[t+2];Tris.Add(a);if(mirror){Tris.Add(c);Tris.Add(b);}else{Tris.Add(b);Tris.Add(c);}
                    Bone.Add(bone((At[a]+At[b]+At[c])/3));
                }
            }
        }
        // A corpse's cut: made on a worker (Run), put on its bodies on the main thread (VisualCutFinish).
        private sealed class CutJob
        {
            // The bodies: a split body's two halves (the plane), or the one severed body (Pieces, its keep values on already).
            public VisualCorpse[] Halves;public CutPart[] Parts;public int Count;public bool Pieces;public Vector3 Point,Normal;public uint Seed;
            // The bones the caps may ride: each one's segment in the death pose, its index in the corpse's bones and its piece.
            public Vector3[] BoneA,BoneB;public int[] BoneOf,BonePiece;
            public float[] Keep;
            // Each body's caps.
            public readonly CapSet[] Caps={new CapSet(),new CapSet()};
            public System.Threading.Tasks.Task Task;public Exception Error;public bool Finished;
            public bool Done=>Finished||Task==null||Task.IsCompleted;
            public void Run(){try{if(Pieces)MakePieces();else MakePlane();}catch(Exception e){Error=e;}}
            // Each vertex where the death pose put it (its blend shapes, then its four bones).
            internal static Vector3[] Posed(CutPart p)
            {
                var m=p.Mesh;int n=m.At.Length;var at=new Vector3[n];
                for(int i=0;i<n;i++)
                {
                    var v=m.At[i];if(p.Shapes!=null)foreach(var s in p.Shapes)v+=s.delta[i]*s.share;
                    var w=m.Weights[i];var q=Vector3.zero;float sum=0;
                    if(w.weight0>0){q+=p.Skin[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0;sum+=w.weight0;}
                    if(w.weight1>0){q+=p.Skin[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1;sum+=w.weight1;}
                    if(w.weight2>0){q+=p.Skin[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2;sum+=w.weight2;}
                    if(w.weight3>0){q+=p.Skin[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;sum+=w.weight3;}
                    at[i]=sum>1e-6f?q/sum:p.Skin[w.boneIndex0].MultiplyPoint3x4(v);
                }
                return at;
            }
            private void MakePlane()
            {
                Keep=new float[Count*2];var a=new List<Vector3>();var b=new List<Vector3>();var paint=new List<NVector4>();var plane=new List<bool>();
                // The skeleton's sections per half (its bones reach NativeModelSkeleton.BoneOut past the plane on either side).
                var bones=new[]{new List<(Vector3 a,Vector3 b,byte kind,int bone)>(),new List<(Vector3 a,Vector3 b,byte kind,int bone)>()};float reach=NativeModelSkeleton.BoneOut;
                foreach(var p in Parts)
                {
                    var m=p.Mesh;int n=m.At.Length;var at=Posed(p);var d=new float[n];var decided=new bool[n];float out_=p.Kinds!=null?reach:0;
                    for(int i=0;i<n;i++)
                    {
                        float dist=Vector3.Dot(at[i]-Point,Normal);d[i]=dist;var w=m.Weights[i];
                        // Its body (its heaviest bone's) outside the mask: wholly on half 0; else the plane decides.
                        int top=w.boneIndex0;float most=w.weight0;if(w.weight1>most){top=w.boneIndex1;most=w.weight1;}if(w.weight2>most){top=w.boneIndex2;most=w.weight2;}if(w.weight3>most)top=w.boneIndex3;
                        float k0,k1;if(!p.Masked[top]){k0=1;k1=-1;}else{k0=out_-dist;k1=out_+dist;decided[i]=true;}
                        Keep[(p.Base+i)*2]=k0;Keep[(p.Base+i)*2+1]=k1;
                    }
                    if(p.Kinds!=null)
                    {
                        for(int h=0;h<2;h++)
                        {
                            var list=bones[h];var dh=new float[n];float off=h==0?reach:-reach;for(int i=0;i<n;i++)dh[i]=d[i]-off;
                            Section(at,dh,m.Tris,decided,(c0,c1,i0,i1,i2)=>list.Add((c0,c1,p.Kinds[i0],p.Visual[m.Weights[i0].boneIndex0])));
                        }
                        continue;
                    }
                    Section(at,d,m.Tris,null,(c0,c1,i0,i1,i2)=>{a.Add(c0);b.Add(c1);paint.Add(p.Paint);plane.Add(decided[i0]&&decided[i1]&&decided[i2]);});
                }
                // Each closed outline is kept where the plane decides most of it (an outline round a body outside the mask, which
                // stays whole, would cap a hole that is not there).
                var join=new Joins();var ends=new int[a.Count];for(int k=0;k<a.Count;k++)ends[k]=join.Link(0,a[k],b[k]);
                var total=new Dictionary<int,float>();var by=new Dictionary<int,float>();
                for(int k=0;k<a.Count;k++){int c=join.Find(ends[k]);float l=(b[k]-a[k]).magnitude;total.TryGetValue(c,out float tl);total[c]=tl+l;if(plane[k]){by.TryGetValue(c,out float bl);by[c]=bl+l;}}
                var ka=new List<NVector3>();var kb=new List<NVector3>();var kp=new List<NVector4>();
                for(int k=0;k<a.Count;k++){int c=join.Find(ends[k]);by.TryGetValue(c,out float bl);if(bl*2<total[c])continue;ka.Add(N(a[k]));kb.Add(N(b[k]));kp.Add(paint[k]);}
                var cap=NativeBisect.CapOf(ka,kb,kp,new BisectPlane(N(Point),N(Normal)),NativeBisect.ModelLook,0,Seed,true);
                // Each cap triangle rides the crossed bone nearest its middle; half 1's face mirrors half 0's.
                Caps[0].Add(cap,1,q=>Nearest(q,-1),false);Caps[1].Add(cap,1,q=>Nearest(q,-1),true);
                // The bones' faces (each kind its own: cortex and marrow, spongy bone, the skull and brain, cartilage), on fine cells,
                // a little proud of the flesh's; each triangle on the bone of the section nearest it.
                for(int h=0;h<2;h++)
                {
                    var list=bones[h];var at=new BisectPlane(N(Point+Normal*(h==0?reach:-reach)),N(Normal));
                    for(byte kind=0;kind<=NativeModelSkeleton.Socket;kind++)
                    {
                        var sa=new List<NVector3>();var sb=new List<NVector3>();var sp=new List<NVector4>();var mids=new List<(Vector3 at,int bone)>();
                        foreach(var s in list)if(s.kind==kind){sa.Add(N(s.a));sb.Add(N(s.b));sp.Add(BonePaint(kind));mids.Add(((s.a+s.b)*.5f,s.bone));}
                        if(sa.Count<3)continue;
                        var face=NativeBisect.CapOf(sa,sb,sp,at,NativeModelSkeleton.CutLook(kind),0,Seed^(uint)(kind*7919+h*131),h==0,.0012f);
                        Caps[h].Add(face,.35f,q=>{float best=float.MaxValue;int on=-1;foreach(var m in mids){float e=(m.at-q).sqrMagnitude;if(e<best){best=e;on=m.bone;}}return on;},false);
                    }
                }
            }
            private static NVector4 BonePaint(byte kind){var c=NativePortraitRules.Linear(kind==NativeModelSkeleton.Cartilage?NativeModelSkeleton.CartilageColor:kind==NativeModelSkeleton.Socket?NativeModelSkeleton.SocketColor:NativeModelSkeleton.Ivory);return new NVector4(c.X,c.Y,c.Z,1);}
            // A section: where each triangle crosses the zero of `d`, running with the solid on its left seen from along the normal
            // (Core NativeBisect's Clip), each crossing computed from its edge's lower end (two faces sharing an edge, even through
            // duplicated vertices, meet at one point); `only` (when given) keeps the triangles all of whose vertices it marks.
            private void Section(Vector3[] at,float[] d,int[] tris,bool[] only,Action<Vector3,Vector3,int,int,int> add)
            {
                for(int t=0;t+2<tris.Length;t+=3)
                {
                    int i0=tris[t],i1=tris[t+1],i2=tris[t+2];bool s0=d[i0]>=0,s1=d[i1]>=0,s2=d[i2]>=0;if(s0==s1&&s1==s2)continue;
                    if(only!=null&&!(only[i0]&&only[i1]&&only[i2]))continue;
                    Vector3 c0,c1;if(s0==s1){c0=Crossing(at,d,i1,i2);c1=Crossing(at,d,i2,i0);}else if(s1==s2){c0=Crossing(at,d,i0,i1);c1=Crossing(at,d,i2,i0);}else{c0=Crossing(at,d,i0,i1);c1=Crossing(at,d,i1,i2);}
                    var face=Vector3.Cross(at[i1]-at[i0],at[i2]-at[i0]);var outward=face-Normal*Vector3.Dot(face,Normal);if(outward.sqrMagnitude<1e-20f)continue;
                    if(Vector3.Dot(c1-c0,Vector3.Cross(Normal,outward))<0){var sw=c0;c0=c1;c1=sw;}
                    if((c1-c0).sqrMagnitude<1e-14f)continue;add(c0,c1,i0,i1,i2);
                }
            }
            // A sever's caps: each hole of a piece (where a kept vertex of it meets one that is not) closed by a fan to its middle,
            // a skin rim, then flesh darkening inward, on the piece's bone nearest it. (The keep values are already on.)
            private void MakePieces()
            {
                var seg=new List<(int piece,Vector3 a,Vector3 b,Vector3 inner,NVector4 paint)>();var mine=new int[3];
                foreach(var p in Parts)
                {
                    var at=Posed(p);var piece=p.Piece;var tris=p.Mesh.Tris;
                    for(int t=0;t+2<tris.Length;t+=3)
                    {
                        int i0=tris[t],i1=tris[t+1],i2=tris[t+2];int q0=piece[i0],q1=piece[i1],q2=piece[i2];if(q0==q1&&q1==q2)continue;
                        for(int k=0;k<3;k++)
                        {
                            int q=k==0?q0:k==1?q1:q2;if(q<0||k==1&&q==q0||k==2&&(q==q0||q==q1))continue;
                            int n=0;if(q0==q)mine[n++]=i0;if(q1==q)mine[n++]=i1;if(q2==q)mine[n++]=i2;
                            int o0=-1,o1=-1;if(q0!=q){if(o0<0)o0=i0;else o1=i0;}if(q1!=q){if(o0<0)o0=i1;else o1=i1;}if(q2!=q){if(o0<0)o0=i2;else o1=i2;}
                            if(n==1)seg.Add((q,(at[mine[0]]+at[o0])*.5f,(at[mine[0]]+at[o1])*.5f,at[mine[0]],p.Paint));
                            else seg.Add((q,(at[mine[0]]+at[o0])*.5f,(at[mine[1]]+at[o0])*.5f,(at[mine[0]]+at[mine[1]])*.5f,p.Paint));
                        }
                    }
                }
                var join=new Joins();var ends=new int[seg.Count];for(int k=0;k<seg.Count;k++)ends[k]=join.Link(seg[k].piece,seg[k].a,seg[k].b);
                var holes=new Dictionary<int,(Vector3 sum,float length,Vector3 out_)>();
                for(int k=0;k<seg.Count;k++){var s=seg[k];int c=join.Find(ends[k]);float l=(s.b-s.a).magnitude;holes.TryGetValue(c,out var h);holes[c]=(h.sum+(s.a+s.b)*.5f*l,h.length+l,h.out_+((s.a+s.b)*.5f-s.inner));}
                var normals=new Dictionary<int,Vector3>();var holePiece=new Dictionary<int,int>();
                for(int k=0;k<seg.Count;k++){var s=seg[k];int c=join.Find(ends[k]);var h=holes[c];var mid=h.length>0?h.sum/h.length:s.a;normals.TryGetValue(c,out var n);normals[c]=n+Vector3.Cross(s.a-mid,s.b-mid);holePiece[c]=s.piece;}
                foreach(var c in new List<int>(normals.Keys)){var n=normals[c];var o=holes[c].out_;if(n.sqrMagnitude<1e-12f)n=o;if(Vector3.Dot(n,o)<0)n=-n;normals[c]=n.normalized;}
                // A piece's layers (skin, clothes over it) each leave a hole at one opening: only the widest is capped (its cap covers
                // the rest; overlapping caps would fight).
                var under=new HashSet<int>();
                foreach(int c in holes.Keys)foreach(int o in holes.Keys)
                {
                    if(c==o||holePiece[c]!=holePiece[o]||holes[o].length<holes[c].length||holes[o].length==holes[c].length&&o>c||!(holes[c].length>0))continue;
                    var mc=holes[c].sum/holes[c].length;var mo=holes[o].sum/holes[o].length;float reach=Mathf.Max(.02f,holes[o].length/(2*Mathf.PI));
                    if((mc-mo).sqrMagnitude<reach*reach&&Vector3.Dot(normals[c],normals[o])>.5f){under.Add(c);break;}
                }
                var bones=new Dictionary<int,int>();
                for(int k=0;k<seg.Count;k++)
                {
                    var s=seg[k];int c=join.Find(ends[k]);var h=holes[c];if(!(h.length>0)||under.Contains(c))continue;var mid=h.sum/h.length;var n=normals[c];
                    if(!bones.TryGetValue(c,out int bone))bones[c]=bone=Nearest(mid,s.piece);if(bone<0)continue;
                    // The rim: half a centimetre of the shell's colour, darkened; the flesh to the middle.
                    Vector3 Rim(Vector3 p){var to=mid-p;float l=to.magnitude;return l>1e-6f?p+to*(Mathf.Min(.005f,l*.5f)/l):p;}
                    var rim=new Color(s.paint.X*.7f,s.paint.Y*.7f,s.paint.Z*.7f,1);
                    var cs=Caps[0];int i=cs.At.Count;cs.At.Add(s.a);cs.At.Add(s.b);cs.At.Add(Rim(s.b));cs.At.Add(Rim(s.a));cs.At.Add(mid);
                    for(int j=0;j<5;j++)cs.Normal.Add(n);cs.Color.Add(rim);cs.Color.Add(rim);cs.Color.Add(FleshOuter);cs.Color.Add(FleshOuter);cs.Color.Add(FleshDeep);
                    cs.Tris.AddRange(new[]{i,i+1,i+2,i,i+2,i+3,i+3,i+2,i+4});cs.Bone.Add(bone);cs.Bone.Add(bone);cs.Bone.Add(bone);
                }
            }
            // The bone of piece `piece` (any piece: -1) nearest `p`.
            private int Nearest(Vector3 p,int piece)
            {
                float best=float.MaxValue;int on=-1;
                for(int s=0;s<BoneOf.Length;s++){if(piece>=0&&BonePiece[s]!=piece)continue;var ab=BoneB[s]-BoneA[s];float l=ab.sqrMagnitude;float t=l>1e-12f?Mathf.Clamp01(Vector3.Dot(p-BoneA[s],ab)/l):0;float e=(BoneA[s]+ab*t-p).sqrMagnitude;if(e<best){best=e;on=BoneOf[s];}}
                return on;
            }
            private static Vector3 Crossing(Vector3[] at,float[] d,int i,int j)
            {
                Vector3 pa=at[i],pb=at[j];float da=d[i],db=d[j];
                if(pb.x<pa.x||pb.x==pa.x&&(pb.y<pa.y||pb.y==pa.y&&pb.z<pa.z)){var t=pa;pa=pb;pb=t;var u=da;da=db;db=u;}
                float l=Mathf.Abs(da-db)>1e-12f?da/(da-db):0;return pa+(pb-pa)*Mathf.Clamp01(l);
            }
            private static NVector3 N(Vector3 v)=>new NVector3(v.x,v.y,v.z);
            private static Vector3 U(NVector3 v)=>new Vector3(v.X,v.Y,v.Z);
        }
        // Segments joined into outlines by their ends (within 10 microns, per group): each segment's outline by Find.
        private sealed class Joins
        {
            private readonly Dictionary<(int,long,long,long),int> nodes=new Dictionary<(int,long,long,long),int>();private readonly List<int> root=new List<int>();
            private int Node(int group,Vector3 p){var k=(group,(long)Math.Round(p.x*1e5),(long)Math.Round(p.y*1e5),(long)Math.Round(p.z*1e5));if(!nodes.TryGetValue(k,out int id)){id=root.Count;nodes[k]=id;root.Add(id);}return id;}
            public int Find(int x){while(root[x]!=x)x=root[x]=root[root[x]];return x;}
            public int Link(int group,Vector3 a,Vector3 b){int u=Find(Node(group,a)),w=Find(Node(group,b));if(u!=w)root[Math.Max(u,w)]=Math.Min(u,w);return Math.Min(u,w);}
        }
        private static readonly int CutTexId=Shader.PropertyToID("_CutTex"),CutBaseId=Shader.PropertyToID("_CutBase"),CutWidthId=Shader.PropertyToID("_CutWidth"),CutPickId=Shader.PropertyToID("_CutPick"),VertexAlbedoId=Shader.PropertyToID("_VertexAlbedo");
        private const int CutWidth=1024;
        // A sever cap's flesh (linear; Core NativeBisect's outer and deep layers).
        private static readonly Color FleshOuter=new Color(.66f,.08f,.12f).linear,FleshDeep=new Color(.31f,.024f,.063f).linear;
        private MaterialPropertyBlock cutBlock;private Material cutCapMaterial;

        // VisualCorpseMake, for both halves of a split body (made from one rig, so their renderers and bones match): starts the cut
        // and puts the crossed bones' stubs on. False when the body cannot be cut this way (the caller severs it at the joints).
        private bool VisualCorpseCut(NativeCorpse corpse,BisectFx fx,VisualCorpse[] halves,int[] joint)
        {
            var r0=halves[0].All;var r1=halves[1].All;if(r0==null||r1==null||r0.Length!=r1.Length||!SystemInfo.SupportsTextureFormat(TextureFormat.RGFloat))return false;
            var request=fx.Request;var normal=CutVec(request.Normal).normalized;var point=CutVec(request.Point);if(normal.sqrMagnitude<.5f)return false;
            var index=VisualCutIndex(halves[0]);var parts=new List<CutPart>();var cut=new bool[r0.Length];int count=0;
            for(int k=0;k<r0.Length;k++)
            {
                if(!(r1[k] is SkinnedMeshRenderer twin)||!(r0[k] is SkinnedMeshRenderer skin)||twin.sharedMesh!=skin.sharedMesh)continue;
                var part=VisualCutPart(skin,k,count,index,joint,request.Mask,null);if(part==null)continue;parts.Add(part);cut[k]=true;count+=part.Mesh.At.Length;
            }
            if(parts.Count==0)return false;
            // The bones the cut may cross: the stubs of those it does now, and the segments the cap's pieces pick from.
            var boneA=new List<Vector3>();var boneB=new List<Vector3>();var boneOf=new List<int>();
            VisualCutBones(halves,joint,point,normal,request.Mask,boneA,boneB,boneOf);
            // A renderer the cut does not read goes whole to the half its middle is on.
            for(int k=0;k<r0.Length;k++)
            {
                if(cut[k]||r0[k]==null||r1[k]==null)continue;bool far=Vector3.Dot(r0[k].bounds.center-point,normal)>=0;(far?r0[k]:r1[k]).enabled=false;
            }
            var job=new CutJob{Halves=halves,Parts=parts.ToArray(),Count=count,Point=point,Normal=normal,Seed=fx.Seed,BoneA=boneA.ToArray(),BoneB=boneB.ToArray(),BoneOf=boneOf.ToArray(),BonePiece=new int[boneOf.Count]};
            halves[0].Cut=job;halves[1].Cut=job;halves[1].Root.SetActive(false);VisualCutStart(job);
            // The halves part only once the cut is on them (as they wait for the voxel split).
            var ready=corpse.SplitReady;var wait=corpse.AwaitSplit;
            corpse.SplitReady=()=>(ready==null||ready())&&job.Done;
            corpse.AwaitSplit=()=>{wait?.Invoke();VisualCutFinish(job);};
            return true;
        }
        // VisualCorpseSever: a body severed at its joints (per visual bone its piece, `pieceOfBone`, -1 dropped) cut in its shader;
        // false when it cannot be (the caller copies its meshes).
        private bool VisualCorpseSeverCut(VisualCorpse c,int[] pieceOfBone)
        {
            var all=c.All;if(all==null||c.Root==null||!SystemInfo.SupportsTextureFormat(TextureFormat.RGFloat))return false;
            var index=VisualCutIndex(c);var parts=new List<CutPart>();int count=0;
            for(int k=0;k<all.Length;k++){if(!(all[k] is SkinnedMeshRenderer skin))continue;var part=VisualCutPart(skin,k,count,index,null,uint.MaxValue,pieceOfBone);if(part==null)continue;parts.Add(part);count+=part.Mesh.At.Length;}
            if(parts.Count==0)return false;
            // The keep values now: a vertex all of whose weight is on one piece's bones.
            var keep=new float[count*2];
            foreach(var p in parts){for(int i=0;i<p.Piece.Length;i++){keep[(p.Base+i)*2]=p.Piece[i]>=0?1:-1;keep[(p.Base+i)*2+1]=-1;}}
            var tex=VisualCutTexture(keep,count);c.CutTexture=tex;cutBlock??=new MaterialPropertyBlock();
            foreach(var p in parts){var r=all[p.Index];r.GetPropertyBlock(cutBlock);cutBlock.SetTexture(CutTexId,tex);cutBlock.SetFloat(CutBaseId,p.Base);cutBlock.SetFloat(CutWidthId,CutWidth);cutBlock.SetVector(CutPickId,new Vector4(1,0,0,0));r.SetPropertyBlock(cutBlock);}
            // The bones the caps may ride: every bone of a piece, at its place.
            var boneA=new List<Vector3>();var bonePiece=new List<int>();var boneOf=new List<int>();
            for(int v=0;v<c.Bones.Length&&v<pieceOfBone.Length;v++)if(c.Bones[v]!=null&&pieceOfBone[v]>=0){boneA.Add(c.Bind[v].GetColumn(3));boneOf.Add(v);bonePiece.Add(pieceOfBone[v]);}
            var job=new CutJob{Halves=new[]{c},Parts=parts.ToArray(),Count=count,Pieces=true,BoneA=boneA.ToArray(),BoneB=boneA.ToArray(),BoneOf=boneOf.ToArray(),BonePiece=bonePiece.ToArray()};
            c.Cut=job;VisualCutStart(job);return true;
        }
        private static Dictionary<Transform,int> VisualCutIndex(VisualCorpse c){var index=new Dictionary<Transform,int>();for(int v=0;v<c.Bones.Length;v++)if(c.Bones[v]!=null)index[c.Bones[v]]=v;return index;}
        // One renderer's share of a cut (its bones' death-pose skinning, their masks or pieces, its worn blend shapes); null when
        // the cut cannot read its mesh. With `pieceOfBone` each vertex's piece: the one all its weight is on, else -1.
        private CutPart VisualCutPart(SkinnedMeshRenderer skin,int k,int first,Dictionary<Transform,int> index,int[] joint,uint mask,int[] pieceOfBone)
        {
            if(skin==null||skin.sharedMesh==null)return null;var mesh=CutMeshOf(skin.sharedMesh);var bones=skin.bones;if(mesh==null||bones.Length<mesh.Bind.Length)return null;
            var part=new CutPart{Mesh=mesh,Skin=new Matrix4x4[mesh.Bind.Length],Masked=new bool[mesh.Bind.Length],Index=k,Base=first,Paint=VisualCutPaint(skin),Visual=new int[mesh.Bind.Length]};var piece=new int[mesh.Bind.Length];
            if(skin.sharedMesh==skeletonMesh)part.Kinds=skeletonKinds;
            for(int i=0;i<part.Skin.Length;i++)
            {
                var bone=bones[i];part.Skin[i]=bone!=null?bone.localToWorldMatrix*mesh.Bind[i]:Matrix4x4.identity;
                int v=-1;for(var t=bone;t!=null&&v<0;t=t.parent)if(!index.TryGetValue(t,out v))v=-1;part.Visual[i]=v;
                if(joint!=null)part.Masked[i]=NativeBisect.Masked(mask,v>=0?joint[v]:CapturedRig.Pelvis);
                if(pieceOfBone!=null)piece[i]=v>=0&&v<pieceOfBone.Length?pieceOfBone[v]:0;
            }
            if(pieceOfBone!=null)
            {
                part.Piece=new int[mesh.At.Length];
                for(int i=0;i<part.Piece.Length;i++)
                {
                    var w=mesh.Weights[i];int q=-2;
                    void On(int b,float weight){if(weight<=0||q==-1)return;int p=piece[b];q=p<0||q>=0&&q!=p?-1:p;}
                    On(w.boneIndex0,w.weight0);On(w.boneIndex1,w.weight1);On(w.boneIndex2,w.weight2);On(w.boneIndex3,w.weight3);part.Piece[i]=q<0?-1:q;
                }
            }
            var shared=skin.sharedMesh;
            for(int s=0;s<shared.blendShapeCount;s++)
            {
                float weight=skin.GetBlendShapeWeight(s);if(Mathf.Abs(weight)<.01f)continue;var frames=CutShape(mesh,shared,s,weight);if(frames!=null)(part.Shapes??=new List<(Vector3[],float)>()).AddRange(frames);
            }
            return part;
        }
        private void VisualCutStart(CutJob job)
        {
            if(SplitInBackground&&Application.platform!=RuntimePlatform.WebGLPlayer)try{job.Task=System.Threading.Tasks.Task.Run(job.Run);}catch(Exception){job.Task=null;}
            if(job.Task==null)job.Run();
        }
        // The data a cut reads, read ahead (owner 2026-10-28: "prevent fps drops"): each new body's skinned meshes and the blend
        // shape frames it wears (a corpse is dressed as its live body was), a renderer a frame (VisualCorpsePoolStep), so the
        // first kill finds them read.
        private static readonly Queue<SkinnedMeshRenderer> cutWarm=new Queue<SkinnedMeshRenderer>();
        private static void VisualCutWarm(Renderer[] all){foreach(var r in all)if(r is SkinnedMeshRenderer skin&&skin.sharedMesh!=null&&!cutMeshes.ContainsKey(skin.sharedMesh))cutWarm.Enqueue(skin);}
        private void VisualCutWarmStep()
        {
            while(cutWarm.Count>0)
            {
                var skin=cutWarm.Dequeue();if(skin==null||skin.sharedMesh==null)continue;var mesh=skin.sharedMesh;bool fresh=!cutMeshes.ContainsKey(mesh);var m=CutMeshOf(mesh);
                if(m!=null)for(int s=0;s<mesh.blendShapeCount;s++){float weight=skin.GetBlendShapeWeight(s);if(Mathf.Abs(weight)>=.01f)CutShape(m,mesh,s,weight);}
                if(fresh)return;
            }
        }
        private CutMesh CutMeshOf(Mesh mesh)
        {
            if(cutMeshes.TryGetValue(mesh,out var m))return m;m=null;
            if(mesh.isReadable&&mesh.bindposeCount>0)
            {
                bool based=false;for(int s=0;s<mesh.subMeshCount;s++)if(mesh.GetBaseVertex(s)!=0)based=true;
                if(!based){var w=mesh.boneWeights;var at=mesh.vertices;if(w.Length==at.Length&&at.Length>0)m=new CutMesh{At=at,Weights=w,Bind=mesh.bindposes,Tris=mesh.triangles};}
            }
            cutMeshes[mesh]=m;return m;
        }
        // A blend shape at `weight` (0 to 100, as the renderer holds it): its frames' deltas and shares (between two frames, or
        // from the rest to the first, as Unity blends them). Null when it cannot be read.
        private static List<(Vector3[],float)> CutShape(CutMesh m,Mesh mesh,int shape,float weight)
        {
            int frames=mesh.GetBlendShapeFrameCount(shape);if(frames<1)return null;
            Vector3[] Frame(int f){int key=shape*64+f;if(!m.Frames.TryGetValue(key,out var d)){d=new Vector3[mesh.vertexCount];mesh.GetBlendShapeFrameVertices(shape,f,d,null,null);m.Frames[key]=d;}return d;}
            int k=0;while(k<frames-1&&mesh.GetBlendShapeFrameWeight(shape,k)<weight)k++;
            float hi=mesh.GetBlendShapeFrameWeight(shape,k),lo=k>0?mesh.GetBlendShapeFrameWeight(shape,k-1):0;
            if(k==0||weight>=hi)return new List<(Vector3[],float)>{(Frame(k),hi!=0?weight/hi:0)};
            float t=hi>lo?(weight-lo)/(hi-lo):1;return new List<(Vector3[],float)>{(Frame(k-1),1-t),(Frame(k),t)};
        }
        // A renderer's shell colour at the cap's rim (linear): a skin part's tone, else its palette's first colour as it draws.
        private NVector4 VisualCutPaint(Renderer r)
        {
            cutBlock??=new MaterialPropertyBlock();r.GetPropertyBlock(cutBlock);var m=r.sharedMaterial;Color c=new Color(.55f,.38f,.32f);
            var tone=cutBlock.GetVector(SkinToneId);
            if(tone.w>.5f)c=new Color(tone.x,tone.y,tone.z);
            else
            {
                var block=cutBlock.GetColor(BodyColours[0]);if(block.a>0)c=block;else if(m!=null&&m.HasProperty(BodyColours[0]))c=m.GetColor(BodyColours[0]);
                if(QualitySettings.activeColorSpace==ColorSpace.Linear)c=c.linear;
            }
            return new NVector4(c.r,c.g,c.b,1);
        }
        // The keep values (two per vertex) as the shader reads them: rows of CutWidth texels.
        private static Texture2D VisualCutTexture(float[] keep,int count)
        {
            int height=Math.Max(1,(count+CutWidth-1)/CutWidth);var data=new float[CutWidth*height*2];Array.Copy(keep,data,Math.Min(keep.Length,data.Length));
            var tex=new Texture2D(CutWidth,height,TextureFormat.RGFloat,false,true){name="Visual corpse cut",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            tex.SetPixelData(data,0);tex.Apply(false,true);return tex;
        }
        // Puts a finished cut on its bodies: a split's keep values (each half its slice of the texture; the second half shows),
        // then the caps. Waits for the worker if it is still running. A failed split's halves both draw whole.
        private void VisualCutFinish(CutJob job)
        {
            if(job==null||job.Finished)return;var halves=job.Halves;
            if(job.Task!=null&&!job.Task.IsCompleted)try{job.Task.Wait();}catch(Exception e){job.Error=e;}
            job.Finished=true;foreach(var h in halves)if(h.Root==null)return;
            if(job.Error!=null||!job.Pieces&&job.Keep==null){Debug.LogWarning("Visual corpse cut failed: "+job.Error?.Message);if(halves.Length>1)halves[1].Root.SetActive(halves[0].Root.activeSelf);return;}
            cutBlock??=new MaterialPropertyBlock();
            if(!job.Pieces)
            {
                var tex=VisualCutTexture(job.Keep,job.Count);halves[0].CutTexture=tex;
                for(int h=0;h<halves.Length;h++)foreach(var p in job.Parts)
                {
                    var r=halves[h].All[p.Index];if(r==null)continue;
                    r.GetPropertyBlock(cutBlock);cutBlock.SetTexture(CutTexId,tex);cutBlock.SetFloat(CutBaseId,p.Base);cutBlock.SetFloat(CutWidthId,CutWidth);
                    cutBlock.SetVector(CutPickId,h==0?new Vector4(1,0,0,0):new Vector4(0,1,0,0));r.SetPropertyBlock(cutBlock);
                }
            }
            for(int h=0;h<halves.Length;h++)VisualCutCaps(halves[h],job.Caps[h]);
            if(halves.Length>1)halves[1].Root.SetActive(halves[0].Root.activeSelf);
        }
        // A body's caps, one mesh per bone they ride, in the body's toon coloured by their vertices (so the dissolve and the kill
        // looks reach them); they go with the corpse.
        private void VisualCutCaps(VisualCorpse c,CapSet caps)
        {
            if(caps.Tris.Count==0)return;var look=VisualCutCapMaterial(c);if(look==null)return;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            var groups=new Dictionary<int,List<int>>();for(int t=0;t<caps.Bone.Count;t++){int b=caps.Bone[t];if(b<0||b>=c.Bones.Length||c.Bones[b]==null)continue;if(!groups.TryGetValue(b,out var list))groups[b]=list=new List<int>();list.Add(t);}
            var added=new List<Renderer>();
            foreach(var g in groups)
            {
                var into=c.Bind[g.Key].inverse;var map=new Dictionary<int,int>();var at=new List<Vector3>();var nm=new List<Vector3>();var col=new List<Color>();var uv=new List<Vector2>();var tris=new List<int>();
                foreach(int t in g.Value)
                {
                    for(int k=0;k<3;k++)
                    {
                        int i=caps.Tris[t*3+k];if(!map.TryGetValue(i,out int o))
                        {
                            o=at.Count;map[i]=o;var p=caps.At[i];at.Add(into.MultiplyPoint3x4(p));nm.Add(into.MultiplyVector(caps.Normal[i]).normalized);
                            var cl=caps.Color[i];col.Add(linear?cl:cl.gamma);uv.Add(new Vector2(p.x+p.z,p.y)*2);
                        }
                        tris.Add(o);
                    }
                }
                var mesh=new Mesh{name="Visual corpse cut",indexFormat=at.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
                mesh.SetVertices(at);mesh.SetNormals(nm);mesh.SetColors(col);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();(c.Owned??=new List<Mesh>()).Add(mesh);
                var go=new GameObject("Visual cut");go.transform.SetParent(c.Bones[g.Key],false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=look;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;added.Add(r);
            }
            if(added.Count>0){var all=new Renderer[c.All.Length+added.Count];c.All.CopyTo(all,0);added.CopyTo(all,c.All.Length);c.All=all;}
        }
        // The caps' material: the body's toon coloured by the cap's vertices (the flesh glows a little, as the voxel caps'), both
        // sides, no cut-out, outline or accent glow. One for every corpse.
        private Material VisualCutCapMaterial(VisualCorpse c)=>VisualCutCapMaterial(c.All);
        private Material VisualCutCapMaterial(Renderer[] all)
        {
            if(cutCapMaterial!=null)return cutCapMaterial;Material source=null;
            foreach(var r in all)if(r is SkinnedMeshRenderer&&r.sharedMaterial!=null&&r.sharedMaterial.HasProperty(VertexAlbedoId)){source=r.sharedMaterial;break;}
            if(source==null)return null;
            cutCapMaterial=new Material(source){name="Visual corpse cut",hideFlags=HideFlags.HideAndDontSave};cutCapMaterial.SetTexture(TextureId,Texture2D.whiteTexture);
            cutCapMaterial.SetFloat(VertexAlbedoId,.3f);cutCapMaterial.SetFloat(UseCustomTextureId,0);cutCapMaterial.SetFloat("_Cull",0);cutCapMaterial.SetFloat(CutoffId,0);cutCapMaterial.SetFloat("_UseOutline",0);cutCapMaterial.SetFloat(AccentGlowId,0);
            return cutCapMaterial;
        }
        // The bones the plane crosses (owner 2026-10-27: "Can you show bones + flesh on gibs too?"; Core NativeModelGore): on
        // every bone the cut may cross (its body in the request's mask) whose segment to the next bone of its limb (or spine) the
        // plane crosses, that bone's stubs on each half where they lie in its cross-section, out of the cap a little, marrow dark
        // at the end (each half's bone rides the part on its side). Every such bone is also a segment in `boneA`/`boneB`/`boneOf`
        // for the cap, crossed or not (an end bone, the hand, foot or head, a point).
        private void VisualCutBones(VisualCorpse[] halves,int[] joint,Vector3 point,Vector3 normal,uint mask,List<Vector3> boneA,List<Vector3> boneB,List<int> boneOf)
        {
            var c=halves[0];var index=VisualCutIndex(c);
            Material source=null;foreach(var r in c.All)if(r is SkinnedMeshRenderer&&r.sharedMaterial!=null){source=r.sharedMaterial;break;}
            var boneLook=source!=null?VisualGoreMaterial(source,NativeModelGore.Bone):null;var marrowLook=source!=null?VisualGoreMaterial(source,NativeModelGore.Marrow):null;
            goreStub??=GoreCylinder();goreDisc??=GoreDisc();var added=new List<Renderer>[2]{new List<Renderer>(),new List<Renderer>()};
            var facing=c.Root.transform.rotation;
            for(int v=0;v<c.Bones.Length;v++)
            {
                var bone=c.Bones[v];if(bone==null||bone.name.Contains("_twist")||NativeModelGore.At(bone.name,"").Length==0||!NativeBisect.Masked(mask,joint[v]))continue;
                string kind=NativeModelGore.Kind(bone.name);Vector3 from=c.Bind[v].GetColumn(3);bool any=false;
                foreach(Transform child in bone)
                {
                    if(!index.TryGetValue(child,out int u)||!CutNext(kind,NativeModelGore.Kind(child.name)))continue;
                    Vector3 to=c.Bind[u].GetColumn(3);boneA.Add(from);boneB.Add(to);boneOf.Add(v);any=true;
                    float df=Vector3.Dot(from-point,normal),dt=Vector3.Dot(to-point,normal);if(df>=0==dt>=0||boneLook==null||c.Skeleton!=null)continue;
                    var sections=NativeModelGore.At(bone.name,child.name);if(sections.Length==0)continue;
                    var cross=from+(to-from)*(df/(df-dt));var axis=(to-from).normalized;int side=NativeModelGore.Side(bone.name);
                    var ahead=Vector3.ProjectOnPlane(facing*Vector3.forward,axis);var outward=Vector3.ProjectOnPlane(facing*Vector3.right*(side<0?-1:1),axis);
                    if(ahead.sqrMagnitude<1e-6f||outward.sqrMagnitude<1e-6f)continue;ahead.Normalize();outward.Normalize();
                    for(int h=0;h<2;h++)
                    {
                        // Into this half: toward the end on its side (half 0 keeps the plane's back).
                        var inward=(h==0)==(df<0)?-axis:axis;var turn=Quaternion.FromToRotation(Vector3.up,inward);var on=halves[h].Bones[v];
                        foreach(var s in sections)
                        {
                            var centre=cross+outward*s.Outward+ahead*s.Ahead;var end=centre-inward*NativeModelGore.Out;var deep=centre+inward*NativeModelGore.Depth(bone.name);
                            added[h].Add(GorePiece("Visual gore bone",goreStub,boneLook,on,(end+deep)*.5f,turn,new Vector3(s.Radius*2,(deep-end).magnitude,s.Radius*2)));
                            added[h].Add(GorePiece("Visual gore marrow",goreDisc,marrowLook,on,end-inward*.0005f,Quaternion.FromToRotation(Vector3.up,-inward),new Vector3(s.Radius*1.1f,1,s.Radius*1.1f)));
                        }
                    }
                }
                if(!any){boneA.Add(from);boneB.Add(from);boneOf.Add(v);}
            }
            for(int h=0;h<2;h++)if(added[h].Count>0){var hc=halves[h];var all=new Renderer[hc.All.Length+added[h].Count];hc.All.CopyTo(all,0);added[h].CopyTo(all,hc.All.Length);hc.All=all;}
        }
        // The next bone along a limb or the spine from a bone of kind `kind` (the one its cross-section's bones run toward).
        private static bool CutNext(string kind,string child)
        {
            switch(kind)
            {
                case "upperarm":return child=="lowerarm";
                case "lowerarm":return child=="hand";
                case "thigh":return child=="calf";
                case "calf":return child=="foot";
                case "clavicle":return child=="upperarm";
                case "pelvis":return child=="spine"||child=="thigh";
                case "spine":return child=="spine"||child=="neck";
                case "neck":return child=="neck"||child=="head";
            }
            return false;
        }
    }
}
