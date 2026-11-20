using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's street dress has no see-through seams (the integrator's follow-up to the live hub's z-fighting, 2026-11-20: once the
    /// greybox gave way to the dress on the live map, its pinholes showed, and the toon outline drew each as a small dark cross).
    /// Over the five street plans, on the Developer walk and on a live deal, every generated surface is combined as the drawer
    /// combines it (NativeMeridianDress.Combine, one surface at a time, at LOD0 and LOD1), and:
    ///  - no T-junction: no vertex of one surface's mesh lies inside an edge of another's boundary on an axis-aligned line, floors and
    ///    walls alike, at one tier within a 32 m cell and at any pair of tiers across cells (a cell at LOD0 stands beside one at LOD1;
    ///    the drawer switches a cell's chunks together, which this pins);
    ///  - no floor-line gap at the camp fence: the floor runs on under each fence slab's whole footprint (the greybox draws the fence
    ///    with chamfered edges; its foot's groove showed the void under the floor).
    /// `--dress-seams-probe` prints the counts with the seams and with each surface on its own grid (shared's tessellation).
    /// </summary>
    internal static class DressSeamChecks
    {
        private static int count;
        private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Dress seams: "+what);}

        internal sealed class Result{public int Floor,Wall,Mixed,Same,Near;public readonly List<string> Worst=new List<string>();}

        /// <summary>The T-junctions of a plan's surfaces as the drawer combines them (each surface alone, at both tiers).</summary>
        public static Result TJunctions(NativeMeridianDress.Plan plan,int keep=8)
        {
            var mesh=new NativeMeridianMesh();var r=new Result();
            // a boundary segment of one surface's mesh on an axis-aligned line, and a vertex on one
            var segs=new Dictionary<(int a,long f1,long f2),List<(int q,int t,float lo,float hi,bool floor)>>();
            int[] tiers={0,2};
            for(int qi=0;qi<plan.Quads.Count;qi++)foreach(int tier in tiers)
            {
                NativeMeridianDress.Combine(plan,0,tier,null,null,null,new List<int>{qi},mesh);
                var edges=new Dictionary<(int,int),int>();
                for(int k=0;k+2<mesh.Solid.Count;k+=3)for(int e=0;e<3;e++)
                {
                    int i=mesh.Solid[k+e],j=mesh.Solid[k+(e+1)%3];var key=i<j?(i,j):(j,i);edges.TryGetValue(key,out int c);edges[key]=c+1;
                }
                bool floor=plan.Quads[qi].N.y>.97f;
                foreach(var kv in edges)
                {
                    if(kv.Value!=1)continue;var (i,j)=kv.Key;
                    var p=new V3(mesh.P[i*3],mesh.P[i*3+1],mesh.P[i*3+2]);var q=new V3(mesh.P[j*3],mesh.P[j*3+1],mesh.P[j*3+2]);var d=q-p;
                    float x=Math.Abs(d.x),y=Math.Abs(d.y),z=Math.Abs(d.z),l=Math.Max(x,Math.Max(y,z));if(l<1e-4f)continue;
                    int a=x>=y&&x>=z?0:y>=z?1:2;if((a==0?y+z:a==1?x+z:x+y)>l*1e-4f)continue;
                    int b=a==0?1:0,c2=a==2?1:2;float pa=Ax(p,a),qa=Ax(q,a);
                    var line=(a,(long)Math.Round(Ax(p,b)*1e4),(long)Math.Round(Ax(p,c2)*1e4));
                    if(!segs.TryGetValue(line,out var list))segs[line]=list=new List<(int,int,float,float,bool)>();
                    list.Add((qi,tier,Math.Min(pa,qa),Math.Max(pa,qa),floor));
                }
            }
            const float eps=1e-5f;
            var seen=new HashSet<(int,long,long,long)>();
            foreach(var kv in segs)
            {
                var list=kv.Value;list.Sort((m,n)=>m.lo.CompareTo(n.lo));
                // every segment end of every surface against every other surface's segments that span it
                for(int s=0;s<list.Count;s++)foreach(float x in new[]{list[s].lo,list[s].hi})
                {
                    for(int o=0;o<list.Count;o++)
                    {
                        var g=list[o];if(g.lo>x)break;if(g.q==list[s].q||x>=g.hi)continue;
                        // one cell's surfaces draw at one tier together (the drawer switches a cell's chunks together)
                        if(g.t!=list[s].t&&plan.Quads[g.q].Chunk==plan.Quads[list[s].q].Chunk)continue;
                        if(x<=g.lo+eps||x>=g.hi-eps){if(Math.Abs(x-g.lo)>0&&Math.Abs(x-g.lo)<=eps||Math.Abs(x-g.hi)>0&&Math.Abs(x-g.hi)<=eps)r.Near++;continue;}
                        if(!seen.Add((kv.Key.a,kv.Key.f1,kv.Key.f2,(long)Math.Round(x*1e4))))continue;
                        if(list[s].floor&&g.floor)r.Floor++;else r.Wall++;
                        if(list[s].t==g.t)r.Same++;else r.Mixed++;
                        if(r.Worst.Count<keep)r.Worst.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"surface {0} (tier {1}) has a vertex at {2:F4} inside surface {3}'s edge {4:F4}..{5:F4} (tier {6}) on the {7} line at ({8}, {9})",
                            list[s].q,list[s].t,x,g.q,g.lo,g.hi,g.t,"xyz"[kv.Key.a],kv.Key.f1/1e4,kv.Key.f2/1e4));
                    }
                }
            }
            return r;
        }
        private static float Ax(V3 v,int a)=>a==0?v.x:a==1?v.y:v.z;

        /// <summary>The points of each camp fence slab's footprint (at its foot) that no floor surface turned up covers.</summary>
        public static int FenceGaps(ContentMap map,NativeMeridianDress.Plan plan,out int samples)
        {
            samples=0;int gaps=0;var floors=plan.Quads.Where(q=>q.N.y>.97f).ToList();
            foreach(var b in map.blocks)
            {
                if(b?.id==null||!b.id.StartsWith("camp-fence-",StringComparison.Ordinal))continue;
                float x0=b.pos.x-b.size.x*.5f,x1=b.pos.x+b.size.x*.5f,z0=b.pos.z-b.size.z*.5f,z1=b.pos.z+b.size.z*.5f,y=b.pos.y-b.size.y*.5f;
                int nx=Math.Max(2,(int)Math.Ceiling((x1-x0)/.25f)),nz=Math.Max(2,(int)Math.Ceiling((z1-z0)/.25f));
                for(int i=0;i<=nx;i++)for(int k=0;k<=nz;k++)
                {
                    float px=x0+.005f+(x1-x0-.01f)*i/nx,pz=z0+.005f+(z1-z0-.01f)*k/nz;samples++;
                    bool on=false;
                    foreach(var q in floors)
                    {
                        if(Math.Abs(q.O.y-y)>.001f)continue;var c=q.O+q.U+q.V;
                        if(px>=Math.Min(q.O.x,c.x)-1e-4f&&px<=Math.Max(q.O.x,c.x)+1e-4f&&pz>=Math.Min(q.O.z,c.z)-1e-4f&&pz<=Math.Max(q.O.z,c.z)+1e-4f){on=true;break;}
                    }
                    if(!on)gaps++;
                }
            }
            return gaps;
        }

        private static IEnumerable<(string name,ContentMap map,NativeMeridianDress.Plan plan)> Plans(string root)
        {
            for(int li=0;li<NativeCityLayout.Plans.Count;li++)
            {
                var info=NativeCityLayout.Plans[li];var walk=new NativeLayeredCityWalk(info);
                yield return (info.Name+" (walk)",walk.Map,NativeMeridianDress.Build(MeridianDressChecks.InputOf(walk,info.Seed,77)));
            }
            foreach(var live in HubZFightChecks.Worlds(root))
                yield return (NativeCityLayout.Plans[live.Plan].Name+" (live deal "+live.Seed+")",live.World.Map,HubZFightChecks.Dress(live,null,false));
        }

        public static void Run(string root)
        {
            count=0;int surfaces=0;
            foreach(var (name,map,plan) in Plans(root))
            {
                Check(plan.Seams!=null&&plan.Seams.Tris.Length==plan.Quads.Count*2,name+": the plan carries its seams");
                var t=TJunctions(plan);surfaces+=plan.Quads.Count;
                Check(t.Floor+t.Wall==0,name+": "+t.Floor+" T-junctions on floors and "+t.Wall+" on walls and corners ("+t.Same+" at one tier, "+t.Mixed+" across LOD0 and LOD1):\n  "+string.Join("\n  ",t.Worst));
                int gaps=FenceGaps(map,plan,out int samples);
                Check(samples>100&&gaps==0,name+": the floor runs under the camp fence ("+gaps+" of "+samples+" footprint points bare)");
                // the counts the drawer splits its chunks by are the meshes' own
                var mesh=new NativeMeridianMesh();
                for(int qi=0;qi<plan.Quads.Count;qi+=97)foreach(int tier in new[]{0,2})
                {
                    NativeMeridianDress.Combine(plan,0,tier,null,null,null,new List<int>{qi},mesh);
                    Check(mesh.Triangles==NativeMeridianDress.QuadTriangles(plan,qi,tier),name+": surface "+qi+"'s triangle count at tier "+tier);
                }
            }
            // the drawer switches a 32 m cell's chunks between LOD0 and LOD1 together (the seams conform within a cell at one tier)
            var draw=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeMeridianDressDraw.cs"));
            Check(draw.Contains("cellWant[cell]=d<reach+(cellShown[cell]==0?NativeMeridianDress.Hysteresis:0)?0:d<far?2:-1;")&&draw.Contains("int want=cellWant[cell];if(want==0&&!cellReady[cell])want=2;")
                &&draw.Contains("if(c.NearMesh==null)cellReady[cell]=false;"),"the drawer switches a cell's chunks together, LOD0 once all of them have it");
            Console.WriteLine("PASS dress seams: "+count+" checks, "+surfaces+" surfaces over the five plans on the walk and a live deal: no T-junction at any LOD mix the drawer draws, the floor under the camp fence");
        }

        /// <summary>`--dress-seams-probe`: each plan's T-junctions with the seams and with each surface on its own grid, and the fence's
        /// floor-line gaps.</summary>
        public static void Probe(string root)
        {
            foreach(var (name,map,plan) in Plans(root))
            {
                // load costs: the seam table, and every surface combined at LOD1 (the drawer's load), with the seams and without
                var clock=System.Diagnostics.Stopwatch.StartNew();NativeMeridianDress.BuildSeams(plan);double seamMs=clock.Elapsed.TotalMilliseconds;
                var all=Enumerable.Range(0,plan.Quads.Count).ToList();var lod1=new NativeMeridianMesh();
                clock.Restart();NativeMeridianDress.Combine(plan,0,2,null,null,null,all,lod1);double withMs=clock.Elapsed.TotalMilliseconds;
                var kept=plan.Seams;plan.Seams=null;clock.Restart();NativeMeridianDress.Combine(plan,0,2,null,null,null,all,lod1);double withoutMs=clock.Elapsed.TotalMilliseconds;plan.Seams=kept;
                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0}: seam table {1:F0} ms; LOD1 combine of every surface {2:F0} ms with the seams, {3:F0} ms without",name,seamMs,withMs,withoutMs));
                var watch=System.Diagnostics.Stopwatch.StartNew();var seams=plan.Seams;
                var with=TJunctions(plan,3);plan.Seams=null;var without=TJunctions(plan,3);plan.Seams=seams;
                int gaps=FenceGaps(map,plan,out int samples);
                int t0=Enumerable.Range(0,plan.Quads.Count).Sum(q=>NativeMeridianDress.QuadTriangles(plan,q,0)),t2=Enumerable.Range(0,plan.Quads.Count).Sum(q=>NativeMeridianDress.QuadTriangles(plan,q,2));
                int o0=plan.Quads.Sum(q=>NativeMeridianDress.QuadTriangles(q,0)),o2=plan.Quads.Sum(q=>NativeMeridianDress.QuadTriangles(q,2));
                Console.WriteLine(name+": "+plan.Quads.Count+" surfaces, "+(seams?.Lines.Count??0)+" seam lines; T-junctions with seams: floors "+with.Floor+", walls "+with.Wall+" (near "+with.Near+"); each on its own grid: floors "+without.Floor+", walls "+without.Wall+" ("+without.Same+" at one tier, "+without.Mixed+" across tiers); fence footprint bare "+gaps+"/"+samples+
                    "; surface triangles LOD0 "+o0+" -> "+t0+", LOD1 "+o2+" -> "+t2+" ("+watch.ElapsedMilliseconds+" ms)");
                foreach(var w in with.Worst)Console.WriteLine("  with: "+w);
                foreach(var w in without.Worst)Console.WriteLine("  own grid: "+w);
            }
        }
    }
}
