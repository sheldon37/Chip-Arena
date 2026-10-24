using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The drawn zombie's own surface for the gore (owner 2026-10-20: "Zombie wounds appear off their body (Happens to other
    /// zombies as well)"; docs/ZOMBIE-GORE.md). Each joint's triangles of a kind's look at the Full tier, in the joint's own
    /// frame (the bank frame the model is drawn in: the runtime draws every part on its joint's world matrix), so a wound, its
    /// gash, its blood run and its drips are placed on what is drawn. They had been placed on a capsule round each joint (the
    /// Standard pilot's radii times a per-kind girth and the drawn scale), which on the big kinds' heads is about twice the head
    /// (the Hulk's 0.43 m against a drawn head of at most 0.25 m), and the run was a straight strip laid across the shot.
    /// <list type="bullet">
    /// <item>The add's own look as ZombieLit draws it (<see cref="ShapeOf"/>): its head, torso and limb proportions about each
    /// joint and its missing pieces folded away; the zone kinds draw as built.</item>
    /// <item>The hinged kinds' jaw is on the head, closed. A dangling part's sway (at most 2.4 cm) is not followed.</item>
    /// <item>Built once per kind and look: the runtime hands over the Full model it builds when the bank loads
    /// (<see cref="Keep"/>); anything else builds it on first use (<see cref="Of"/>). Queries allocate nothing.</item>
    /// </list>
    /// Presentation only: nothing here is read by a rule.
    /// </summary>
    public sealed class NativeHordeGoreBody
    {
        /// <summary>Triangles per bounding sphere (the queries skip whole clusters).</summary>
        public const int Cluster=16;
        public readonly int Kind,Look;
        private readonly float[][] pos=new float[22][];private readonly int[][] tri=new int[22][];private readonly byte[][] info=new byte[22][];private readonly float[][] sphere=new float[22][];
        /// <summary>Each joint's bounding sphere (its own frame, as built).</summary>
        public readonly Vector3[] Centre=new Vector3[22];public readonly float[] Radius=new float[22];
        public int Triangles(int joint)=>joint>=0&&joint<22&&tri[joint]!=null?tri[joint].Length/3:0;

        /// <summary>How an add's body is drawn (ZombieLit's vert): head scale, torso width and limb girth about each joint, and
        /// the missing-piece variants (1..4) folded away.</summary>
        public struct Shape{public float Head,Width,Girth;public int Mask;public static Shape Plain=>new Shape{Head=1,Width=1,Girth=1};}
        /// <summary>Add `id`'s shape: its variation (NativeHordeModels.Variation) on the kinds whose models take it, else as built.</summary>
        public static Shape ShapeOf(int kind,int look,int id)
        {
            kind=NativeHordeKinds.Index(kind);if(!NativeHordeModels.Varies(NativeHordeModels.Id(kind,Math.Max(0,Math.Min(NativeHordeKinds.Looks[kind]-1,look)),NativeHordeModels.Full)))return Shape.Plain;
            var v=NativeHordeModels.Variation(id);var p=NativeHordeModels.Proportions(v.Y-1);
            return new Shape{Head=p.X,Width=p.Y,Girth=p.Z,Mask=(int)Math.Floor((v.Z-1)*16+.5f)};
        }

        // ---- the bodies, one per kind and look ----
        private static readonly object gate=new object();private static NativeHordeGoreBody[] bodies;
        private static int Slot(int kind,int look)=>kind*NativeHordeKinds.MostLooks+look;
        /// <summary>Kind `kind`'s look `look` (its Full model), built on first use unless the runtime kept it at load.</summary>
        public static NativeHordeGoreBody Of(int kind,int look)
        {
            kind=NativeHordeKinds.Index(kind);look=Math.Max(0,Math.Min(NativeHordeKinds.Looks[kind]-1,look));
            var all=bodies;var b=all!=null?all[Slot(kind,look)]:null;if(b!=null)return b;
            return Keep(NativeHordeModels.Build(kind,look,NativeHordeModels.Full));
        }
        /// <summary>Keeps a built Full model's surface (the runtime calls it as the bank loads, so no hit builds one).</summary>
        public static NativeHordeGoreBody Keep(HordeModel model)
        {
            if(model==null)return null;int kind=NativeHordeKinds.Index(model.Kind),look=Math.Max(0,Math.Min(NativeHordeKinds.Looks[kind]-1,model.Look));
            lock(gate)
            {
                bodies??=new NativeHordeGoreBody[NativeHordeKinds.All*NativeHordeKinds.MostLooks];var b=bodies[Slot(kind,look)];
                if(b==null){b=new NativeHordeGoreBody(kind,look,model);var copy=(NativeHordeGoreBody[])bodies.Clone();copy[Slot(kind,look)]=b;bodies=copy;}
                return b;
            }
        }

        private NativeHordeGoreBody(int kind,int look,HordeModel model)
        {
            Kind=kind;Look=look;var vc=new int[22];var tc=new int[22];
            void Count(WeaponMesh m){if(m==null)return;foreach(var s in m.Sections)if(s.Bone>0&&s.Bone<22){vc[s.Bone]+=s.Positions.Count;tc[s.Bone]+=s.Indices.Count/3;}}
            Count(model.Mesh);Count(model.Jaw);
            for(int j=0;j<22;j++){if(tc[j]==0)continue;pos[j]=new float[vc[j]*3];tri[j]=new int[tc[j]*3];info[j]=new byte[tc[j]];vc[j]=tc[j]=0;}
            void Add(WeaponMesh m)
            {
                if(m==null)return;
                foreach(var s in m.Sections)
                {
                    int j=s.Bone;if(j<=0||j>=22)continue;int v0=vc[j];
                    for(int i=0;i<s.Positions.Count;i++){var p=s.Positions[i];pos[j][(v0+i)*3]=p.X;pos[j][(v0+i)*3+1]=p.Y;pos[j][(v0+i)*3+2]=p.Z;}
                    for(int i=0;i+2<s.Indices.Count;i+=3)
                    {
                        int a=s.Indices[i],b=s.Indices[i+1],c=s.Indices[i+2];var pa=s.Positions[a];var f=Vector3.Cross(s.Positions[b]-pa,s.Positions[c]-pa);
                        // Its outside is the side its vertex normals face (the builders' winding agrees on all but a few).
                        if(s.Normals.Count==s.Positions.Count&&Vector3.Dot(f,s.Normals[a]+s.Normals[b]+s.Normals[c])<0){int t=b;b=c;c=t;}
                        int k=tc[j]++;tri[j][k*3]=v0+a;tri[j][k*3+1]=v0+b;tri[j][k*3+2]=v0+c;
                        float region=a<s.Colors.Count?s.Colors[a].W:1;int r=Math.Abs(region-NativeHordeModels.RegionHead)<.01f?1:Math.Abs(region-NativeHordeModels.RegionTorso)<.01f?2:Math.Abs(region-NativeHordeModels.RegionLimb)<.01f?3:0;
                        int variant=a<s.Data.Count?(int)Math.Floor(s.Data[a].Y):0;if(variant<0||variant>4)variant=0;
                        info[j][k]=(byte)(variant|r<<4);
                    }
                    vc[j]+=s.Positions.Count;
                }
            }
            Add(model.Mesh);Add(model.Jaw);
            for(int j=0;j<22;j++)
            {
                if(tri[j]==null)continue;int n=tri[j].Length/3,clusters=(n+Cluster-1)/Cluster;sphere[j]=new float[clusters*5];var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);
                for(int c=0;c<clusters;c++)
                {
                    var cl=new Vector3(float.MaxValue);var ch=new Vector3(float.MinValue);
                    for(int k=c*Cluster;k<Math.Min(n,(c+1)*Cluster);k++)for(int e=0;e<3;e++){var p=P(j,tri[j][k*3+e]);cl=Vector3.Min(cl,p);ch=Vector3.Max(ch,p);}
                    var mid=(cl+ch)*.5f;float r=0;for(int k=c*Cluster;k<Math.Min(n,(c+1)*Cluster);k++)for(int e=0;e<3;e++)r=Math.Max(r,Vector3.Distance(P(j,tri[j][k*3+e]),mid));
                    sphere[j][c*5]=mid.X;sphere[j][c*5+1]=mid.Y;sphere[j][c*5+2]=mid.Z;sphere[j][c*5+3]=r;sphere[j][c*5+4]=mid.Length();lo=Vector3.Min(lo,cl);hi=Vector3.Max(hi,ch);
                }
                Centre[j]=(lo+hi)*.5f;Radius[j]=Vector3.Distance(hi,lo)*.5f;
            }
        }
        private Vector3 P(int j,int v)=>new Vector3(pos[j][v*3],pos[j][v*3+1],pos[j][v*3+2]);
        private static Vector3 Scale(in Shape s,int region)=>region==1?new Vector3(s.Head):region==2?new Vector3(s.Width,1,1):region==3?new Vector3(s.Girth,1,s.Girth):Vector3.One;
        private static bool Hidden(in Shape s,int variant)=>variant>0&&(s.Mask>>(variant-1)&1)!=0;
        // How far a shaped point can be from where it was built (for the cluster spheres).
        private static float Slack(in Shape s)=>Math.Max(Math.Abs(s.Head-1),Math.Max(Math.Abs(s.Width-1),Math.Abs(s.Girth-1)));
        // Cluster c of joint j: its centre and its radius grown by how far the shape can move its points.
        private Vector3 Mid(float[] sp,int c,float slack,out float r){r=sp[c*5+3]+slack*(sp[c*5+4]+sp[c*5+3]);return new Vector3(sp[c*5],sp[c*5+1],sp[c*5+2]);}
        private bool Corners(int j,int k,in Shape s,out Vector3 a,out Vector3 b,out Vector3 c)
        {
            int f=info[j][k];a=b=c=default;if(Hidden(s,f&15))return false;var sc=Scale(s,f>>4);
            a=P(j,tri[j][k*3])*sc;b=P(j,tri[j][k*3+1])*sc;c=P(j,tri[j][k*3+2])*sc;return true;
        }

        /// <summary>
        /// The first place a ray (joint `j`'s own frame: from `o` along `d`, up to `most` times d) enters the joint's drawn part
        /// from outside (its outward face toward the ray). Writes the ray parameter and the face's outward unit normal.
        /// </summary>
        public bool Ray(int j,Vector3 o,Vector3 d,float most,in Shape s,out float t,out Vector3 normal)
        {
            t=most;normal=Vector3.Zero;if(Triangles(j)==0)return false;float slack=Slack(s),dd=d.LengthSquared();if(!(dd>1e-12f))return false;bool hit=false;
            var sp=sphere[j];int n=tri[j].Length/3;
            for(int c=0;c*Cluster<n;c++)
            {
                var mid=Mid(sp,c,slack,out float r);
                // The ray's closest approach to the cluster's sphere.
                float u=Math.Max(0,Math.Min(t,Vector3.Dot(mid-o,d)/dd));if(Vector3.DistanceSquared(o+d*u,mid)>r*r)continue;
                for(int k=c*Cluster;k<Math.Min(n,(c+1)*Cluster);k++)
                {
                    if(!Corners(j,k,s,out var a,out var b,out var cc))continue;var e1=b-a;var e2=cc-a;var f=Vector3.Cross(e1,e2);if(!(Vector3.Dot(f,d)<0))continue;
                    var pv=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,pv);if(Math.Abs(det)<1e-12f)continue;float inv=1/det;var tv=o-a;float bu=Vector3.Dot(tv,pv)*inv;if(bu<0||bu>1)continue;
                    var qv=Vector3.Cross(tv,e1);float bv=Vector3.Dot(d,qv)*inv;if(bv<0||bu+bv>1)continue;float tt=Vector3.Dot(e2,qv)*inv;
                    if(tt>=0&&tt<t){t=tt;normal=f;hit=true;}
                }
            }
            if(hit)normal=Vector3.Normalize(normal);return hit;
        }
        /// <summary>The nearest point of joint `j`'s drawn part to `p` (its own frame) within `within`, and that face's outward unit
        /// normal; the distance (float.MaxValue with no part that near).</summary>
        public float Closest(int j,Vector3 p,in Shape s,out Vector3 at,out Vector3 normal,float within=float.MaxValue)
        {
            at=p;normal=Vector3.UnitY;if(Triangles(j)==0)return float.MaxValue;float slack=Slack(s),best=within<1e18f?within*within:float.MaxValue,start=best;var sp=sphere[j];int n=tri[j].Length/3;Vector3 bf=Vector3.Zero;
            for(int c=0;c*Cluster<n;c++)
            {
                var mid=Mid(sp,c,slack,out float r);float dc=Vector3.Distance(p,mid)-r;if(dc>0&&dc*dc>=best)continue;
                for(int k=c*Cluster;k<Math.Min(n,(c+1)*Cluster);k++)
                {
                    if(!Corners(j,k,s,out var a,out var b,out var cc)||Box(p,a,b,cc)>=best)continue;var q=ClosestOnTriangle(p,a,b,cc);float d=Vector3.DistanceSquared(q,p);
                    if(d<best){var f=Vector3.Cross(b-a,cc-a);if(f.LengthSquared()<1e-14f)continue;best=d;at=q;bf=f;}
                }
            }
            if(!(best<start)||bf==Vector3.Zero)return float.MaxValue;normal=Vector3.Normalize(bf);return (float)Math.Sqrt(best);
        }
        /// <summary>Whether joint `j`'s drawn part comes within `within` of `p` (its own frame): the footprint's test, which stops
        /// at the first face close enough.</summary>
        public bool Near(int j,Vector3 p,in Shape s,float within)
        {
            if(Triangles(j)==0)return false;float slack=Slack(s),w2=within*within;var sp=sphere[j];int n=tri[j].Length/3;
            for(int c=0;c*Cluster<n;c++)
            {
                var mid=Mid(sp,c,slack,out float r);r+=within;if(Vector3.DistanceSquared(p,mid)>r*r)continue;
                for(int k=c*Cluster;k<Math.Min(n,(c+1)*Cluster);k++)if(Corners(j,k,s,out var a,out var b,out var cc)&&Box(p,a,b,cc)<=w2&&Vector3.DistanceSquared(ClosestOnTriangle(p,a,b,cc),p)<=w2)return true;
            }
            return false;
        }
        /// <summary>Whether a ray (joint `j`'s frame, from `o` along `d` up to `most` times d) passes joint `j`'s bounding sphere.</summary>
        public bool Passes(int j,Vector3 o,Vector3 d,float most,in Shape s)
        {
            if(Triangles(j)==0)return false;float dd=d.LengthSquared();if(!(dd>1e-12f))return false;var c=Centre[j];float r=Radius[j]+Slack(s)*(c.Length()+Radius[j]);
            float u=Math.Max(0,Math.Min(most,Vector3.Dot(c-o,d)/dd));return Vector3.DistanceSquared(o+d*u,c)<=r*r;
        }
        // The squared distance from p to the box round triangle abc (never more than to the triangle).
        private static float Box(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            float x=Math.Max(0,Math.Max(Math.Min(a.X,Math.Min(b.X,c.X))-p.X,p.X-Math.Max(a.X,Math.Max(b.X,c.X))));
            float y=Math.Max(0,Math.Max(Math.Min(a.Y,Math.Min(b.Y,c.Y))-p.Y,p.Y-Math.Max(a.Y,Math.Max(b.Y,c.Y))));
            float z=Math.Max(0,Math.Max(Math.Min(a.Z,Math.Min(b.Z,c.Z))-p.Z,p.Z-Math.Max(a.Z,Math.Max(b.Z,c.Z))));return x*x+y*y+z*z;
        }
        /// <summary>The nearest point of triangle abc to p (Ericson, Real-Time Collision Detection 5.1.5).</summary>
        public static Vector3 ClosestOnTriangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float den=1/(va+vb+vc);return a+ab*(vb*den)+ac*(vc*den);
        }
    }
}
