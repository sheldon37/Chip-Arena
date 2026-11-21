using System;
using System.Linq;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
        internal sealed class NativePoolroomsClearance
        {
            public MapNav Certified {get;private set;}
            readonly ContentMap map; readonly ContentBlock[] boxes;
            readonly Dictionary<(float,float),MapNavNode[]> clearNodes=new Dictionary<(float,float),MapNavNode[]>();
            public MapNavNode Nearest(V3 p,float radius,float height)
            {if(!clearNodes.TryGetValue((radius,height),out var nodes))clearNodes[(radius,height)]=nodes=map.nav.nodes.Where(n=>Supported(n.pos-new V3(0,.85f,0),radius,height)).ToArray();return nodes.OrderBy(n=>(n.pos-p-new V3(0,.85f,0)).Length).FirstOrDefault();}
            readonly Dictionary<(int,int),List<ContentBlock>> grid=new Dictionary<(int,int),List<ContentBlock>>();
            IEnumerable<ContentBlock> Near(V3 p)=>grid.TryGetValue(((int)Math.Floor(p.x/4),(int)Math.Floor(p.z/4)),out var list)?list:Enumerable.Empty<ContentBlock>();
            public NativePoolroomsClearance(ContentMap m){map=m;var off=new HashSet<string>((m.ramps??Array.Empty<MapRamp>()).SelectMany(r=>r.blocks));boxes=m.blocks.Where(b=>!off.Contains(b.id)).ToArray();foreach(var b in boxes)for(int x=(int)Math.Floor((b.pos.x-b.size.x/2-1)/4);x<=(int)Math.Floor((b.pos.x+b.size.x/2+1)/4);x++)for(int z=(int)Math.Floor((b.pos.z-b.size.z/2-1)/4);z<=(int)Math.Floor((b.pos.z+b.size.z/2+1)/4);z++){if(!grid.TryGetValue((x,z),out var list))grid[(x,z)]=list=new List<ContentBlock>();list.Add(b);}}
            static float Dot(V3 a,V3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
            static bool Surface(MapRamp r,V3 p,out float y,out float normal)
            {
                var d=r.high-r.low;float length=(float)Math.Sqrt(d.x*d.x+d.z*d.z),along=((p.x-r.low.x)*d.x+(p.z-r.low.z)*d.z)/length,across=(-(p.x-r.low.x)*d.z+(p.z-r.low.z)*d.x)/length;
                y=r.low.y+d.y*along/length;normal=length/(float)Math.Sqrt(length*length+d.y*d.y);
                return along>=0&&along<=length&&Math.Abs(across)<=r.width/2;
            }
            public bool Ground(V3 p,float up,float down,out V3 floor,out float normal)
            {
                float y=float.NegativeInfinity;normal=1;
                foreach(var b in Near(p)){float top=b.pos.y+b.size.y/2;if(Math.Abs(p.x-b.pos.x)<=b.size.x/2&&Math.Abs(p.z-b.pos.z)<=b.size.z/2&&top<=p.y+up+.00001f&&top>=p.y+up-down-.00001f&&top>y){y=top;normal=1;}}
                foreach(var r in map.ramps??Array.Empty<MapRamp>())if(Surface(r,p,out float top,out float n)&&top<=p.y+up+.00001f&&top>=p.y+up-down-.00001f&&top>y){y=top;normal=n;}
                floor=new V3(p.x,y,p.z);return !float.IsNegativeInfinity(y);
            }
            public bool Supported(V3 p,float radius,float height)
            {
                if(!Ground(p,.35f,.45f,out _,out float normal)||normal<.65f)return false;
                float lift=.04f+radius*(1/normal-1),lo=p.y+radius+lift,hi=p.y+height-radius+lift;
                foreach(var b in Near(p))
                {
                    float dx=Math.Max(0,Math.Abs(p.x-b.pos.x)-b.size.x/2),dz=Math.Max(0,Math.Abs(p.z-b.pos.z)-b.size.z/2),dy=Math.Max(0,Math.Max(b.pos.y-b.size.y/2-hi,lo-(b.pos.y+b.size.y/2)));
                    if(dx*dx+dy*dy+dz*dz<radius*radius-.000001f)return false;
                }
                foreach(var r in map.ramps??Array.Empty<MapRamp>())
                {
                    // Closest point on the finite sloping rectangle to the capsule's bottom sphere.
                    var d=r.high-r.low;float run=(float)Math.Sqrt(d.x*d.x+d.z*d.z);var across=new V3(-d.z/run,0,d.x/run);var centre=new V3(p.x,lo,p.z);
                    float t=Math.Max(0,Math.Min(1,Dot(centre-r.low,d)/Dot(d,d))),u=Math.Max(-r.width/2,Math.Min(r.width/2,Dot(centre-r.low,across)));
                    var delta=centre-(r.low+d*t+across*u);if(Dot(delta,delta)<radius*radius-.000001f)return false;
                }
                return true;
            }
            public HashSet<string> Flood(V3 origin,float radius,float height)
            {
                var nodes=map.nav.nodes.ToDictionary(n=>n.id);var clear=nodes.Values.ToDictionary(n=>n.id,n=>Supported(n.pos-new V3(0,.85f,0),radius,height));
                var edges=nodes.Keys.ToDictionary(id=>id,id=>new List<string>());
                foreach(var link in map.nav.links)
                {
                    if(!clear[link.a]||!clear[link.b])continue;var a=nodes[link.a].pos-new V3(0,.85f,0);var b=nodes[link.b].pos-new V3(0,.85f,0);bool ok=true;int steps=Math.Max(1,(int)Math.Ceiling((b-a).Length/.4));
                    for(int i=1;i<steps&&ok;i++){var p=a+(b-a)*(i/(float)steps);ok=Ground(p,.2f,.4f,out var floor,out float normal)&&normal>=.65f&&Supported(floor,radius,height);}
                    // Generated links cross one cell edge only; wall crossings must also pass the raised LOS.
                    for(int i=0;i<=steps*2&&ok;i++){var p=a+(b-a)*(i/(float)(steps*2))+new V3(0,radius+.4f,0);ok=!Near(p).Any(box=>Math.Abs(p.x-box.pos.x)<box.size.x/2-.00001f&&Math.Abs(p.y-box.pos.y)<box.size.y/2-.00001f&&Math.Abs(p.z-box.pos.z)<box.size.z/2-.00001f);}
                    if(ok){edges[link.a].Add(link.b);edges[link.b].Add(link.a);}
                }
                Certified=new MapNav{nodes=map.nav.nodes,links=edges.SelectMany(e=>e.Value.Where(n=>string.CompareOrdinal(e.Key,n)<0).Select(n=>new MapNavLink{id=e.Key+"/"+n,a=e.Key,b=n})).ToArray()};
                var start=nodes.Values.Where(n=>clear[n.id]).OrderBy(n=>(n.pos-origin-new V3(0,.85f,0)).Length).First();var seen=new HashSet<string>{start.id};var q=new Queue<string>(seen);
                while(q.Count>0)foreach(var next in edges[q.Dequeue()])if(seen.Add(next))q.Enqueue(next);return seen;
            }
        }
}
