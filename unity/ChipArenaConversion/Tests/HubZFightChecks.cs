using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The live Meridian run's hub draws each surface once (owner, 2026-11-20: "There is immense z-fighting on the hub in the new
    /// expedition map."). Every live deal (one per street plan) is built as the station builds it (NativeExpeditionGeneratedWorld),
    /// its street dress planned as ShowMeridian plans it (Runtime NativeExpeditionWorldPresentation), and everything the frame draws
    /// in the hub (the camp, its fence and gates, out to the gates' outside points) is gathered: the greybox boxes the arena dressing
    /// draws (Runtime NativeArenaDressing: NativeBlockLit, the map's face lifts, minus the blocks the dress hides on this map), the
    /// dress's generated surfaces (ChipArena/StageLit, at their layers) and, with the city bank (CHIPARENA_MERIDIAN_BANK), its
    /// modules, and the hub gates at rest (NativeExpeditionHubDraw). No two faces turned the same way may share a plane (4 mm; floors
    /// 30 mm, where a pilot's grazing view parts layers millimetres apart) and overlap, unless StageLit's layers part them
    /// (tools/stages/zfight.py's rules). `--hub-zfight-probe` lists the hub's blocks and every pair, as shared drew them and as this
    /// branch draws them.
    /// </summary>
    internal static class HubZFightChecks
    {
        /// <summary>The hub region: the camp's 49.6 m sanctuary, its fence and gates and their 28 m outside points, with the street
        /// a pilot sees past them (x and z within Reach of the origin, y from Low to High).</summary>
        public const float Reach=32f,Low=-3f,High=12f;
        public const float Eps=.004f,FloorEps=.03f,MinArea=2e-4f;
        private static int count;
        private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Hub zfight: "+what);}

        internal sealed class Live{public ExpeditionGeneratedWorld World;public int Plan;public uint Seed;}

        /// <summary>One live deal per street plan (the station's own path: a room, its roll, the generated world).</summary>
        public static List<Live> Worlds(string root)
        {
            string Read(string name)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name));
            string mapsJson=Read("UnityMaps.json"),layoutJson=Read("ExpeditionLayout.json");
            var maps=JsonSerializer.Deserialize<UnityMapContent>(mapsJson,new JsonSerializerOptions{IncludeFields=true});
            var content=new ConversionContent{maps=maps.maps,contentRevision=NativeMapRules.Revision(mapsJson)};
            var layout=NativeExpeditionLayout.Parse(layoutJson);layout.compatibility=NativeExpeditionLayout.Compatibility(layoutJson,content);
            var seeds=Enumerable.Range(0,64).Select(i=>unchecked((uint)i*2654435761u)).GroupBy(s=>NativeCityLayout.Choose(s,-1)).OrderBy(g=>g.Key).Select(g=>g.First()).ToArray();
            Check(seeds.Length==NativeCityLayout.Plans.Count,"a deal for every street plan");
            var o=new List<Live>();
            foreach(var seed in seeds)
            {
                var room=new ExpeditionDeploymentRoom("hub-zfight","pilot",seed,"black-siren",layout,content);
                Check(room.Execute("pilot",0,ExpeditionDeploymentIntent.RollInstances),"the roll of "+seed);
                o.Add(new Live{World=NativeExpeditionGeneratedWorld.Build(room),Plan=room.Ticket.cityPlan,Seed=seed});
            }
            return o;
        }

        /// <summary>The street dress as the live run plans it (Runtime ShowMeridian): the bleed's contracts only while the bleed draws
        /// (its bank given).</summary>
        public static NativeMeridianDress.Plan Dress(Live live,Func<string,bool> has,bool bleedDraws)
        {
            var world=live.World;var city=world.City;var plan=NativeCityLayout.Plans[live.Plan];uint seed=world.Blueprint.worldSeed;var bleed=world.Bleed;
            var bleedPlan=NativeCityBleedDress.ForWalk(city,bleed,seed);
            var input=new NativeMeridianDress.Input{Map=city.Map,Layout=plan.Seed,Run=seed,Slots=world.HallSlots,Hatches=city.RoofHatches.ToArray(),
                KeepOut=NativeCityBleedDress.CacheKeepOuts(city.Caches,city.Map),
                Routes=city.Routes.Select(r=>new NativeMeridianDress.Route{Name=r.Name,Points=r.Points,Widths=r.Widths,Kinds=r.Kinds}).ToArray()};
            try
            {
                if(bleedDraws)
                {
                    NativeMeridianDress.ThemeAt=bleed.ThemeAt;
                    NativeMeridianDress.LampsReplaced=p=>bleed.ReplacesLamps(p)&&NativeCityBleedDress.NearLamp(bleedPlan,p,14);
                    NativeMeridianDress.FacadeCovered=(p,n)=>NativeCityBleedDress.Covers(bleedPlan,p,n);
                }
                return NativeMeridianDress.Build(input,has);
            }
            finally{NativeMeridianDress.ThemeAt=null;NativeMeridianDress.LampsReplaced=null;NativeMeridianDress.FacadeCovered=null;}
        }

        // ------------------------------------------------------------------------------------------------------------------
        // What the frame draws in the hub, as triangles with their layers
        // ------------------------------------------------------------------------------------------------------------------
        internal sealed class Source{public string Kind,Name,Where;public readonly List<V3> T=new List<V3>();public readonly List<int> L=new List<int>();}
        private static readonly string[] Fins={"asphalt","paving","setts","granite","kerb","stone","civic ashlar","coping","engineering brick","stock brick","buff brick","render",
            "tar felt","lead","black iron","green iron","iron grating","planks","ballast","soffit","glass roof","wagon red","concrete","dark concrete"};
        private static string FinName(int f)=>f>=0&&f<Fins.Length?Fins[f]:"finish "+f;
        private static bool Near(V3 lo,V3 hi,float pad=0)=>hi.x>=-Reach-pad&&lo.x<=Reach+pad&&hi.z>=-Reach-pad&&lo.z<=Reach+pad&&hi.y>=Low-pad&&lo.y<=High+pad;
        private static void Rotate(float qx,float qy,float qz,float qw,V3 p,out V3 o)
        {
            float x=qx,y=qy,z=qz,w=qw;if(x==0&&y==0&&z==0&&w==0)w=1;
            o=new V3((1-2*(y*y+z*z))*p.x+2*(x*y-z*w)*p.y+2*(x*z+y*w)*p.z,2*(x*y+z*w)*p.x+(1-2*(x*x+z*z))*p.y+2*(y*z-x*w)*p.z,2*(x*z-y*w)*p.x+2*(y*z+x*w)*p.y+(1-2*(x*x+y*y))*p.z);
        }
        private static float Axis(V3 v,int a)=>a==0?v.x:a==1?v.y:v.z;
        private static V3 With(V3 v,int a,float f){if(a==0)v.x=f;else if(a==1)v.y=f;else v.z=f;return v;}
        /// <summary>A chamfered box's flat faces (NativeBlockLit: each face's flat part, the bevel's share of its edges cut off),
        /// wound outward, into `s` at `layer`.</summary>
        private static void Box(Source s,V3 c,V3 h,float qx,float qy,float qz,float qw,int layer)
        {
            // (u, v, normal axis): u x v points along the normal's axis
            foreach(var (u,v,a) in new[]{(0,1,2),(1,2,0),(2,0,1)})foreach(int sign in new[]{1,-1})
            {
                float fu=(float)NativeSurfaceLayers.Flat(Axis(h,u)),fv=(float)NativeSurfaceLayers.Flat(Axis(h,v));if(fu<=0||fv<=0)continue;
                V3 P(float i,float j){var p=new V3();p=With(p,u,i*fu);p=With(p,v,j*fv);p=With(p,a,sign*Axis(h,a));Rotate(qx,qy,qz,qw,p,out var r);return c+r;}
                var q=new[]{P(-1,-1),P(1,-1),P(1,1),P(-1,1)};if(sign<0)Array.Reverse(q);
                s.T.Add(q[0]);s.T.Add(q[1]);s.T.Add(q[2]);s.T.Add(q[0]);s.T.Add(q[2]);s.T.Add(q[3]);s.L.Add(layer);s.L.Add(layer);
            }
        }
        /// <summary>The blocks NativeArenaDressing leaves out on the live map: the dress's covers where it hides them on this map
        /// (NativeMeridianDress.HidesGreybox, the runtime's own test) and the landmarks' (their bank draws them).</summary>
        public static List<Source> Gather(Live live,NativeMeridianDress.Plan dress,bool hidesCovers,ICollection<string> landmarkCovers,NativePackBank.Bank bank,Dictionary<string,int[]> modules,int tier,
            NativeMeridianLandmarks.Layout landmarks=null,NativePackBank.Bank landmarksBank=null)
        {
            var map=live.World.Map;var o=new List<Source>();var lifts=NativeSurfaceLayers.Blocks(map.blocks);
            for(int i=0;i<map.blocks.Length;i++)
            {
                var b=map.blocks[i];if(b?.id==null)continue;
                if(hidesCovers&&dress.Covers.Contains(b.id)||landmarkCovers!=null&&landmarkCovers.Contains(b.id))continue;
                var r=b.rotation??new ContentRotation();float ext=b.size.Length*.5f+.05f;
                if(!Near(b.pos-new V3(ext,ext,ext),b.pos+new V3(ext,ext,ext)))continue;
                NativeSurfaceLayers.Apply(lifts,i,out float sx,out float sy,out float sz,out float gx,out float gy,out float gz);
                Rotate(r.x,r.y,r.z,r.w,new V3(sx,sy,sz),out var shift);
                var s=new Source{Kind="greybox",Name=b.id,Where="Runtime/NativeArenaDressing.cs Render (NativeBlockLit, no layer)"};
                Box(s,b.pos+shift,new V3(b.size.x*.5f+gx,b.size.y*.5f+gy,b.size.z*.5f+gz),r.x,r.y,r.z,r.w,0);o.Add(s);
            }
            var B=map.blocks;
            for(int i=0;i<dress.Quads.Count;i++)
            {
                var q=dress.Quads[i];var c1=q.O+q.U+q.V;var lo=new V3(Math.Min(q.O.x,c1.x),Math.Min(q.O.y,c1.y),Math.Min(q.O.z,c1.z));var hi=new V3(Math.Max(q.O.x,c1.x),Math.Max(q.O.y,c1.y),Math.Max(q.O.z,c1.z));
                foreach(var e in new[]{q.O+q.U,q.O+q.V}){lo=new V3(Math.Min(lo.x,e.x),Math.Min(lo.y,e.y),Math.Min(lo.z,e.z));hi=new V3(Math.Max(hi.x,e.x),Math.Max(hi.y,e.y),Math.Max(hi.z,e.z));}
                if(!Near(lo,hi))continue;
                var s=new Source{Kind="dress-surface",Name=FinName(q.Fin)+" on "+(q.Block>=0?B[q.Block].id:"a slope"),Where="Core/NativeMeridianDressPlan.cs (ChipArena/StageLit, layer "+q.Layer+")"};
                // the bank's winding: (p1 - p0) x (p2 - p0) out of the face, as u x v is (NativeMeridianDress.AddQuad)
                s.T.Add(q.O);s.T.Add(q.O+q.U);s.T.Add(q.O+q.U+q.V);s.T.Add(q.O);s.T.Add(q.O+q.U+q.V);s.T.Add(q.O+q.V);s.L.Add(q.Layer);s.L.Add(q.Layer);o.Add(s);
            }
            if(bank!=null)
            {
                var mesh=new NativeMeridianMesh();
                for(int i=0;i<dress.Placements.Count;i++)
                {
                    var p=dress.Placements[i];var at=new V3(p.M[3],p.M[7],p.M[11]);if(!Near(at,at,25))continue;
                    if(!modules.TryGetValue(p.Key,out var props))continue;
                    mesh.Clear();
                    // a flickering lamp's head draws apart each frame from the bank's mesh, without the plan's layers (Heads)
                    if(p.Dynamic)NativeMeridianDress.AddModule(bank,props[tier],p.M,mesh,0);
                    else NativeMeridianDress.Combine(dress,0,tier,bank,modules,new List<int>{i},null,mesh);
                    if(mesh.Vertices==0)continue;
                    var s=new Source{Kind="dress-module",Name=p.Key+(p.Block>=0?" on "+B[p.Block].id:""),Where="Core/NativeMeridianDress*.cs placement "+i+" (ChipArena/StageLit)"};
                    foreach(var list in new[]{mesh.Solid,mesh.Glow})for(int k=0;k+2<list.Count;k+=3)
                    {
                        V3 Vx(int v)=>new V3(mesh.P[v*3],mesh.P[v*3+1],mesh.P[v*3+2]);
                        s.T.Add(Vx(list[k]));s.T.Add(Vx(list[k+1]));s.T.Add(Vx(list[k+2]));
                        float kind=mesh.Uv1[list[k]*2];s.L.Add((int)Math.Floor((kind-Math.Floor(kind+1/64f))*32+.5f));
                    }
                    o.Add(s);
                }
            }
            // the landmarks' pieces (their bank: ChipArenaCityLandmarks), each its Fine mesh (the first part's) through its place, as
            // NativeMeridianLandmarksDraw draws them within 38 m (StageLit, the bank's own layers)
            if(landmarks!=null&&landmarksBank!=null)foreach(var piece in landmarks.Pieces)
            {
                NativePackBank.Prop prop=null;foreach(var t in new[]{"/0","/2","/far"}){prop=landmarksBank.Find(piece.Key+t);if(prop!=null&&prop.Parts.Length>0)break;}
                if(prop==null||prop.Parts.Length==0)continue;var m=landmarksBank.Meshes[prop.Parts[0].Mesh];if(m.Positions==null)continue;var M=piece.Place;
                V3 W(System.Numerics.Vector3 p)=>new V3(M[0]*p.X+M[1]*p.Y+M[2]*p.Z+M[3],M[4]*p.X+M[5]*p.Y+M[6]*p.Z+M[7],M[8]*p.X+M[9]*p.Y+M[10]*p.Z+M[11]);
                var s=new Source{Kind="landmark",Name=piece.Key,Where="Runtime/NativeMeridianLandmarks.cs NativeMeridianLandmarksDraw (ChipArena/StageLit)"};
                foreach(var sub in m.Submeshes)for(int k=0;k+2<sub.Indices.Length;k+=3)
                {
                    V3 a=W(m.Positions[sub.Indices[k]]),b=W(m.Positions[sub.Indices[k+1]]),c=W(m.Positions[sub.Indices[k+2]]);
                    if(!Near(new V3(Math.Min(a.x,Math.Min(b.x,c.x)),Math.Min(a.y,Math.Min(b.y,c.y)),Math.Min(a.z,Math.Min(b.z,c.z))),new V3(Math.Max(a.x,Math.Max(b.x,c.x)),Math.Max(a.y,Math.Max(b.y,c.y)),Math.Max(a.z,Math.Max(b.z,c.z)))))continue;
                    s.T.Add(a);s.T.Add(b);s.T.Add(c);float kind=m.Uv1!=null?m.Uv1[sub.Indices[k]].X:0;s.L.Add((int)Math.Floor((kind-Math.Floor(kind+1/64f))*32+.5f));
                }
                if(s.T.Count>0)o.Add(s);
            }
            // the hub gates at rest (no authority state: open), as NativeExpeditionHubDraw draws them
            var gates=new NativeExpeditionHubGates();gates.Build(live.World.Hub,null,null,0,new ExpeditionEye(),false);
            for(int i=0;i<gates.Parts.Count;i++)
            {
                var p=gates.Parts[i];var s=new Source{Kind="gate",Name="part "+i,Where="Core/NativeExpeditionHub.cs NativeExpeditionHubGates (NativeBlockLit, no layer)"};
                Box(s,new V3(p.X,p.Y,p.Z),new V3(p.Hx,p.Hy,p.Hz),p.Qx,p.Qy,p.Qz,p.Qw,0);o.Add(s);
            }
            return o;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Coplanar, same-facing, overlapping faces (tools/stages/zfight.py's measure, in the hub region)
        // ------------------------------------------------------------------------------------------------------------------
        internal struct Pair{public int A,B,LayerA,LayerB;public float Area,Gap,Plane;public V3 At,N;}
        private static List<(double x,double y)> Clip(List<(double x,double y)> subject,List<(double x,double y)> clipper)
        {
            double Area2(List<(double x,double y)> p){double a=0;for(int i=0;i<p.Count;i++){var u=p[i];var w=p[(i+1)%p.Count];a+=u.x*w.y-w.x*u.y;}return a;}
            if(Area2(clipper)<0){clipper=new List<(double x,double y)>(clipper);clipper.Reverse();}
            var o=subject;
            for(int i=0;i<clipper.Count&&o.Count>0;i++)
            {
                var a=clipper[i];var b=clipper[(i+1)%clipper.Count];var input=o;o=new List<(double x,double y)>();
                bool Inside((double x,double y) p)=>(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x)>=-1e-12;
                (double x,double y) Cut((double x,double y) p,(double x,double y) q)
                {
                    double d=(p.x-q.x)*(a.y-b.y)-(p.y-q.y)*(a.x-b.x);if(Math.Abs(d)<1e-15)return q;
                    double t=((p.x-a.x)*(a.y-b.y)-(p.y-a.y)*(a.x-b.x))/d;return (p.x+t*(q.x-p.x),p.y+t*(q.y-p.y));
                }
                for(int j=0;j<input.Count;j++)
                {
                    var p=input[j];var q=input[(j+1)%input.Count];
                    if(Inside(q)){if(!Inside(p))o.Add(Cut(p,q));o.Add(q);}else if(Inside(p))o.Add(Cut(p,q));
                }
            }
            return o;
        }
        private static double Area(List<(double x,double y)> p){double a=0;for(int i=0;i<p.Count;i++){var u=p[i];var w=p[(i+1)%p.Count];a+=u.x*w.y-w.x*u.y;}return Math.Abs(a)/2;}
        /// <summary>NativeBlockLit's sources (the greybox and the gates): two of them are parted once NativeSurfaceLayers' lifts set
        /// them a Step apart (the arena dressing's own rule, SurfaceChecks).</summary>
        private static bool BlockLit(string kind)=>kind=="greybox"||kind=="gate";
        /// <summary>The pairs of triangles of different sources (or one source's, but for a module's or a landmark's own) in one plane
        /// (within Eps, floors within FloorEps; two NativeBlockLit faces within NativeSurfaceLayers.Step), turned the same way,
        /// overlapping in the hub region by more than MinArea where they can be seen (the point 5 cm out of the overlap's middle lies
        /// in no map block). `parted`: the area StageLit's layers part; `lifted`: the area the greybox's lifts part; `unseen`: the
        /// area pressed against a solid.</summary>
        public static List<Pair> Measure(List<Source> src,NativeMeridianDress.Solids solids,out float parted,out float lifted,out float unseen)
        {
            var T=new List<(int s,int l,V3 a,V3 b,V3 c,V3 n,double d,int ax,bool floor,V3 lo,V3 hi)>();
            for(int s=0;s<src.Count;s++)for(int k=0;k+2<src[s].T.Count;k+=3)
            {
                V3 a=src[s].T[k],b=src[s].T[k+1],c=src[s].T[k+2];var n=NativeMeridianDress.Cross(b-a,c-a);float l=n.Length;if(l<2e-5f)continue;n=n/l;
                int ax=Math.Abs(n.x)>=Math.Abs(n.y)&&Math.Abs(n.x)>=Math.Abs(n.z)?0:Math.Abs(n.y)>=Math.Abs(n.z)?1:2;
                var lo=new V3(Math.Min(a.x,Math.Min(b.x,c.x)),Math.Min(a.y,Math.Min(b.y,c.y)),Math.Min(a.z,Math.Min(b.z,c.z)));
                var hi=new V3(Math.Max(a.x,Math.Max(b.x,c.x)),Math.Max(a.y,Math.Max(b.y,c.y)),Math.Max(a.z,Math.Max(b.z,c.z)));
                if(!Near(lo,hi))continue;
                // one sign per plane, so both facings share a bucket
                float first=Math.Abs(n.x)>1e-6f?n.x:Math.Abs(n.y)>1e-6f?n.y:n.z;var nn=first<0?n*-1f:n;
                T.Add((s,src[s].L[k/3],a,b,c,n,V3.Dot(nn,a),ax,n.y>.97f,lo,hi));
            }
            var pairs=new List<Pair>();float partedArea=0,liftedArea=0,unseenArea=0;
            foreach(bool floors in new[]{false,true})
            {
                float eps=floors?FloorEps:Eps;var buckets=new Dictionary<(int,int,int,long),List<int>>();
                for(int i=0;i<T.Count;i++)
                {
                    if(T[i].floor!=floors)continue;var t=T[i];float first=Math.Abs(t.n.x)>1e-6f?t.n.x:Math.Abs(t.n.y)>1e-6f?t.n.y:t.n.z;var nn=first<0?t.n*-1f:t.n;
                    var key=((int)Math.Round(nn.x*200),(int)Math.Round(nn.y*200),(int)Math.Round(nn.z*200),(long)Math.Floor(t.d/eps));
                    if(!buckets.TryGetValue(key,out var list))buckets[key]=list=new List<int>();list.Add(i);
                }
                foreach(var kv in buckets)foreach(long dd in new long[]{0,1})
                {
                    if(!buckets.TryGetValue((kv.Key.Item1,kv.Key.Item2,kv.Key.Item3,kv.Key.Item4+dd),out var other))continue;
                    foreach(int i in kv.Value)foreach(int j in other)
                    {
                        if(dd==0&&j<=i)continue;var a=T[i];var b=T[j];
                        if(a.s==b.s&&(src[a.s].Kind=="dress-module"||src[a.s].Kind=="landmark"))continue;
                        if(a.lo.x>b.hi.x+eps||b.lo.x>a.hi.x+eps||a.lo.y>b.hi.y+eps||b.lo.y>a.hi.y+eps||a.lo.z>b.hi.z+eps||b.lo.z>a.hi.z+eps)continue;
                        double gap=Math.Abs(a.d-b.d);if(gap>eps)continue;
                        if(V3.Dot(a.n,b.n)<.9995f)continue;
                        int k0=a.ax==0?1:0,k1=a.ax==2?1:2;
                        List<(double x,double y)> Flat(V3 p,V3 q,V3 r)=>new List<(double x,double y)>{(Axis(p,k0),Axis(p,k1)),(Axis(q,k0),Axis(q,k1)),(Axis(r,k0),Axis(r,k1))};
                        var poly=Clip(Flat(a.a,a.b,a.c),Flat(b.a,b.b,b.c));if(poly.Count<3)continue;
                        // only the part inside the hub region counts
                        double L0=k0==1?Low:-Reach,H0=k0==1?High:Reach,L1=k1==1?Low:-Reach,H1=k1==1?High:Reach;
                        poly=Clip(poly,new List<(double x,double y)>{(L0,L1),(H0,L1),(H0,H1),(L0,H1)});if(poly.Count<3)continue;
                        double ar=Area(poly)/Math.Max(Math.Abs(Axis(a.n,a.ax)),1e-6);if(ar<=MinArea)continue;
                        double cx=poly.Average(p=>p.x),cy=poly.Average(p=>p.y);var at=With(With(With(new V3(),a.ax,Axis(a.a,a.ax)),k0,(float)cx),k1,(float)cy);
                        // the plane's own point under the overlap's middle (a sloped face: from its first corner)
                        float along=(float)((V3.Dot(a.n,a.a)-(Axis(a.n,k0)*cx+Axis(a.n,k1)*cy))/Axis(a.n,a.ax));at=With(at,a.ax,along);
                        var outside=at+a.n*.05f;if(solids!=null&&solids.Inside(outside.x,outside.y,outside.z,-1,0)){unseenArea+=(float)ar;continue;}
                        if(a.l!=b.l){partedArea+=(float)ar;continue;}
                        if(BlockLit(src[a.s].Kind)&&BlockLit(src[b.s].Kind)&&gap>=NativeSurfaceLayers.Step*.95f){liftedArea+=(float)ar;continue;}
                        if(!floors&&gap>Eps)continue;
                        pairs.Add(new Pair{A=a.s,B=b.s,LayerA=a.l,LayerB=b.l,Area=(float)ar,Gap=(float)gap,Plane=along,At=at,N=a.n});
                    }
                }
            }
            parted=partedArea;lifted=liftedArea;unseen=unseenArea;
            return pairs;
        }

        /// <summary>The pairs by the two pieces that meet: the pieces, the plane and its height, the area and the widest gap.</summary>
        public static List<string> Report(List<Source> src,List<Pair> pairs,int top=40)
        {
            var o=new List<string>();var inv=System.Globalization.CultureInfo.InvariantCulture;
            var by=pairs.GroupBy(p=>(Math.Min(p.A,p.B),Math.Max(p.A,p.B))).Select(g=>(a:g.Key.Item1,b:g.Key.Item2,area:g.Sum(p=>p.Area),gap:g.Max(p=>p.Gap),
                min:g.Min(p=>p.Gap),first:g.OrderByDescending(p=>p.Area).First(),n:g.Count())).OrderByDescending(x=>x.area).ToList();
            var kinds=by.GroupBy(x=>src[x.a].Kind+" | "+src[x.b].Kind).Select(g=>(k:g.Key,area:g.Sum(x=>x.area),n:g.Count())).OrderByDescending(x=>x.area);
            foreach(var k in kinds)o.Add(string.Format(inv,"  {0,-34} {1,5} pairs of pieces {2,10:F2} m2",k.k,k.n,k.area));
            foreach(var x in by.Take(top))
            {
                var p=x.first;string plane=Math.Abs(p.N.y)>.97f?(p.N.y>0?"floor":"ceiling"):Math.Abs(p.N.x)>.97f?(p.N.x>0?"+x face":"-x face"):Math.Abs(p.N.z)>.97f?(p.N.z>0?"+z face":"-z face"):"slope";
                o.Add(string.Format(inv,"  {0} {1} [{2}] | {3} {4} [{5}]: {6} at {7:F3} m, {8:F2} m2 ({9} triangle pairs, gap {10:F4}-{11:F4} m), near ({12:F1}, {13:F2}, {14:F1})",
                    src[x.a].Kind,src[x.a].Name,src[x.a].Where,src[x.b].Kind,src[x.b].Name,src[x.b].Where,plane,p.Plane,x.area,x.n,x.min,x.gap,p.At.x,p.At.y,p.At.z));
            }
            return o;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The managed check (in the full suite; `--hub-zfight` alone)
        // ------------------------------------------------------------------------------------------------------------------
        public static void Run(string root)
        {
            count=0;var worlds=Worlds(root);
            var bankPath=Environment.GetEnvironmentVariable("CHIPARENA_MERIDIAN_BANK");NativePackBank.Bank bank=null;Dictionary<string,int[]> modules=null;
            if(!string.IsNullOrEmpty(bankPath)&&File.Exists(bankPath)){bank=NativePackBank.Read(File.ReadAllBytes(bankPath));modules=NativeMeridianDress.Modules(bank);}
            bool hides=NativeMeridianDress.HidesGreybox(NativeExpeditionGeneratedWorld.MapId);
            Check(hides,"the arena dressing leaves out the blocks the street dress draws on the live map (NativeMeridianDress.HidesGreybox)");
            foreach(var live in worlds)
            {
                var name=NativeCityLayout.Plans[live.Plan].Name;
                Check(live.World.Map.id==NativeExpeditionGeneratedWorld.MapId,name+": the live map's id");
                var dress=Dress(live,modules!=null?modules.ContainsKey:(Func<string,bool>)null,false);
                var src=Gather(live,dress,hides,null,bank,modules,0);
                Check(src.Count(s=>s.Kind=="dress-surface")>10,name+": the dress draws the hub's floors");
                Check(src.Any(s=>s.Kind=="greybox"&&s.Name.StartsWith("camp-fence-",StringComparison.Ordinal))&&src.Count(s=>s.Kind=="gate")>20,name+": the greybox draws the camp's fence and the gates stand");
                var pairs=Measure(src,new NativeMeridianDress.Solids(live.World.Map.blocks),out _,out _,out _);
                Check(pairs.Count==0,name+": "+pairs.Count+" coplanar overlapping faces in the hub:\n"+string.Join("\n",Report(src,pairs,12)));
            }
            // the runtime's own hide line reads the same test
            var dressing=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeArenaDressing.cs"));
            Check(dressing.Contains("meridianBlocks=NativeMeridianDress.HidesGreybox(mapId);")&&dressing.Contains("if(meridianBlocks&&NativeMeridianDressDraw.Hides(arena.MapId,p.Definition.id))continue;"),
                "NativeArenaDressing hides the street dress's blocks wherever NativeMeridianDress.HidesGreybox says");
            Check(NativeMeridianDress.HidesGreybox("expedition-city-walk-647")&&!NativeMeridianDress.HidesGreybox("expedition-city-walk-b")&&!NativeMeridianDress.HidesGreybox("expedition-city")&&!NativeMeridianDress.HidesGreybox(null),
                "the walk's five layouts and the live map only (the old town and the scale walks keep their boxes)");
            Console.WriteLine("PASS hub zfight: "+count+" checks, the live hub of all five street plans draws every surface once"+(bank==null?" (generated surfaces; the modules need CHIPARENA_MERIDIAN_BANK)":" (with the city bank's modules)"));
        }

        /// <summary>`--hub-zfight-probe`: every block in the hub region (its role, whether the dress covers it, its lifts) and the
        /// coplanar pairs as shared drew the hub (the greybox kept under the dress) and as this branch draws it.</summary>
        public static void Probe(string root)
        {
            var worlds=Worlds(root);var inv=System.Globalization.CultureInfo.InvariantCulture;
            var bankPath=Environment.GetEnvironmentVariable("CHIPARENA_MERIDIAN_BANK");NativePackBank.Bank bank=null;Dictionary<string,int[]> modules=null;
            if(!string.IsNullOrEmpty(bankPath)&&File.Exists(bankPath)){bank=NativePackBank.Read(File.ReadAllBytes(bankPath));modules=NativeMeridianDress.Modules(bank);}
            bool bleed=!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CHIPARENA_BLEED_BANK"));
            if(bank!=null&&bleed)bank=NativeMeridianDress.WithVariants(bank,NativePackBank.Read(File.ReadAllBytes(Environment.GetEnvironmentVariable("CHIPARENA_BLEED_BANK"))));
            if(bank!=null)modules=NativeMeridianDress.Modules(bank);
            var lmPath=Environment.GetEnvironmentVariable("CHIPARENA_LANDMARKS_BANK");var lmBank=!string.IsNullOrEmpty(lmPath)&&File.Exists(lmPath)?NativePackBank.Read(File.ReadAllBytes(lmPath)):null;
            Console.WriteLine("banks: city "+(bank!=null)+", bleed "+bleed+", landmarks "+(lmBank!=null));
            foreach(var live in worlds)
            {
                var info=NativeCityLayout.Plans[live.Plan];var map=live.World.Map;
                var dress=Dress(live,modules!=null?modules.ContainsKey:(Func<string,bool>)null,bleed);
                var lm=NativeMeridianLandmarks.Place(info.Seed,map,live.World.City.Routes.Select(r=>r.Destination).ToArray(),live.World.HallSlots,1);
                Console.WriteLine(string.Format(inv,"=== {0} (plan {1}, seed {2}, map {3}): {4} blocks, dress covers {5}, {6} surfaces, {7} modules; landmarks cover {8}",
                    info.Name,info.Seed,live.Seed,map.id,map.blocks.Length,dress.Covers.Count,dress.Quads.Count,dress.Placements.Count,lm.Covers.Count));
                var lifts=NativeSurfaceLayers.Blocks(map.blocks);var lots=NativeMeridianDress.Lots(info.Seed);
                for(int i=0;i<map.blocks.Length;i++)
                {
                    var b=map.blocks[i];var lo=b.pos-b.size*.5f;var hi=b.pos+b.size*.5f;if(!Near(lo,hi))continue;
                    var l=Enumerable.Range(0,6).Select(k=>lifts[i*6+k]).ToArray();
                    Console.WriteLine(string.Format(inv,"  block {0,-28} role {1,-8} covered {2,-5} landmark {3,-5} x {4:F2}..{5:F2} y {6:F3}..{7:F3} z {8:F2}..{9:F2} lifts [{10}]",
                        b.id,NativeMeridianDress.RoleOf(b.id,lots),dress.Covers.Contains(b.id),lm.Covers.Contains(b.id),lo.x,hi.x,lo.y,hi.y,lo.z,hi.z,string.Join(" ",l.Select(v=>v.ToString("0.###",inv)))));
                }
                // across the whole city: every generated surface lies on a covered block's face, which shared's greybox drew too
                double doubled=dress.Quads.Sum(q=>(double)NativeMeridianDress.Cross(q.U,q.V).Length);
                Console.WriteLine(string.Format(inv,"  the dress's generated surfaces over the whole map: {0:F0} m2 (each on a covered block's face: shared drew the greybox's face under every one)",doubled));
                var solids=new NativeMeridianDress.Solids(map.blocks);
                foreach(var (label,hide) in new[]{("SHARED (the greybox kept under the dress)",false),("BRANCH (the dress's blocks left out)",true)})
                {
                    var src=Gather(live,dress,hide,lmBank!=null?lm.Covers:null,bank,modules,0,lm,lmBank);var pairs=Measure(src,solids,out float parted,out float lifted,out float unseen);
                    Console.WriteLine(string.Format(inv,"  {0}: {1} sources ({2} greybox, {3} surfaces, {4} modules, {5} landmark pieces, {6} gate parts), {7} triangle pairs, {8:F1} m2 unparted; parted: {9:F1} m2 by layer, {10:F1} m2 by the greybox's lifts; {11:F1} m2 unseen",
                        label,src.Count,src.Count(s=>s.Kind=="greybox"),src.Count(s=>s.Kind=="dress-surface"),src.Count(s=>s.Kind=="dress-module"),src.Count(s=>s.Kind=="landmark"),src.Count(s=>s.Kind=="gate"),pairs.Count,pairs.Sum(p=>p.Area),parted,lifted,unseen));
                    foreach(var line in Report(src,pairs,30))Console.WriteLine(line);
                }
            }
        }
    }
}
