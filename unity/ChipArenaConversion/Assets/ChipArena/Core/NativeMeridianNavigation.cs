using System;
using System.Linq;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
// Build the camp-connected two-metre lattice at every street elevation.
internal static class NativeMeridianNavigation
{
    private sealed class Surface
    {
        public float X, Z, W, D, Bottom, Top;
        public MapRamp Ramp;
        public bool Street;
        public float At(float x, float z)
        {
            if (Ramp == null) return Top;
            var a = Ramp.low;
            var d = Ramp.high-a;
            float t = Math.Max(0,Math.Min(1,((x-a.x)*d.x+(z-a.z)*d.z)/(d.x*d.x+d.z*d.z)));
            return a.y+t*d.y;
        }
    }

    public static MapNav Build(ContentMap map,PveMachine[] machines=null)
    {
        float radius = NativeHordeKinds.HulkRadius, height = NativeHordeKinds.HulkHeight;
        var bins = new Dictionary<(int,int),List<Surface>>();
        int Bin(float v) => (int)Math.Floor(v/12);
        void Add(Surface s)
        {
            for (int z=Bin(s.Z-s.D-radius); z<=Bin(s.Z+s.D+radius); z++)
                for (int x=Bin(s.X-s.W-radius); x<=Bin(s.X+s.W+radius); x++)
                {
                    if (!bins.TryGetValue((x,z),out var list)) bins[(x,z)] = list = new List<Surface>();
                    list.Add(s);
                }
        }

        var treads=map.ramps.SelectMany(r=>r.blocks).ToHashSet();
        foreach (var b in map.blocks.Where(b=>!treads.Contains(b.id)))
            Add(new Surface { X=b.pos.x,Z=b.pos.z,W=b.size.x/2,D=b.size.z/2,
                Bottom=b.pos.y-b.size.y/2,Top=b.pos.y+b.size.y/2,
                Street=!b.id.StartsWith("camp-",StringComparison.Ordinal)&&!b.id.StartsWith("fire-escape-",StringComparison.Ordinal) });
        foreach(var m in machines??Array.Empty<PveMachine>())
        {
            var b=NativeExpeditionLayout.MachineBlock(m);
            float c=Math.Abs((float)Math.Cos(m.yaw)),s=Math.Abs((float)Math.Sin(m.yaw));
            Add(new Surface{X=b.pos.x,Z=b.pos.z,W=(b.size.x*c+b.size.z*s)/2,D=(b.size.x*s+b.size.z*c)/2,
                Bottom=m.y,Top=m.y+b.size.y});
        }
        foreach (var r in map.ramps)
        {
            var d=r.high-r.low;
            Add(new Surface { X=(r.low.x+r.high.x)/2,Z=(r.low.z+r.high.z)/2,
                W=d.x==0?r.width/2:Math.Abs(d.x)/2,D=d.z==0?r.width/2:Math.Abs(d.z)/2,
                Bottom=r.bottom,Top=r.high.y,Ramp=r,Street=!r.id.StartsWith("fire-escape-",StringComparison.Ordinal) });
        }

        bool Inside(Surface s,float x,float z) => Math.Abs(x-s.X)<=s.W+.001f && Math.Abs(z-s.Z)<=s.D+.001f;
        bool Room(List<Surface> solids, float x, float y, float z)
        {
            foreach (var s in solids)
            {
                if (s.Bottom>=y+height || s.Top<=y+.05f) continue;
                float dx=Math.Max(0,Math.Abs(s.X-x)-s.W), dz=Math.Max(0,Math.Abs(s.Z-z)-s.D);
                if (dx*dx+dz*dz>=radius*radius) continue;
                if (s.At(x,z)>y+.05f) return false;
            }
            return true;
        }

        var nodes=new Dictionary<(int x,int z),List<float>>();
        var bounds=map.bounds;
        for(int z=(int)bounds.minZ+2;z<bounds.maxZ;z+=2)
            for(int x=(int)bounds.minX+2;x<bounds.maxX;x+=2)
            {
                if(!bins.TryGetValue((Bin(x),Bin(z)),out var solids)) continue;
                foreach(var s in solids)
                {
                    if(!s.Street || !Inside(s,x,z)) continue;
                    float y=s.At(x,z);
                    if(!Room(solids,x,y,z)) continue;
                    if(!nodes.TryGetValue((x,z),out var levels)) nodes[(x,z)]=levels=new List<float>();
                    if(!levels.Contains(y)) levels.Add(y);
                }
            }

        var graphNodes=new List<MapNavNode>();
        var graphLinks=new List<MapNavLink>();
        var ids=new Dictionary<(int x,float y,int z),int>();
        int Node((int x,float y,int z) foot)
        {
            if(ids.TryGetValue(foot,out int index))return index;
            index=graphNodes.Count;ids.Add(foot,index);
            graphNodes.Add(new MapNavNode{id="m"+index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                pos=new V3(foot.x,foot.y+.9f,foot.z),layer=(int)Math.Round(foot.y/3.5f)});
            return index;
        }
        var visited=new HashSet<(int x,float y,int z)>();
        var queue=new Queue<(int x,float y,int z)>();
        var start=(0,0f,8);
        if(!nodes.TryGetValue((0,8),out var startLevels)||!startLevels.Contains(0)) throw new Exception("Obstructed Meridian camp");
        visited.Add(start);
        queue.Enqueue(start);
        var directions=new[]{(2,0),(-2,0),(0,2),(0,-2)};
        while(queue.Count>0)
        {
            var n=queue.Dequeue();
            foreach(var (dx,dz) in directions)
            {
                int x=n.x+dx,z=n.z+dz;
                if(!nodes.TryGetValue((x,z),out var levels)) continue;
                foreach(float y in levels)
                {
                    var next=(x,y,z);
                    if(Math.Abs(n.y-y)>1.01f)continue;
                    if(ids.TryGetValue(next,out int existing)&&Node(n)>=existing)continue;
                    int mx=n.x+dx/2,mz=n.z+dz/2;
                    var surfaces=bins[(Bin(mx),Bin(mz))];
                    float mid=(y+n.y)/2;
                    var surface=surfaces.FirstOrDefault(s=>s.Street && Inside(s,mx,mz) && Math.Abs(s.At(mx,mz)-mid)<.3f
                        && Room(surfaces,mx,s.At(mx,mz),mz));
                    if(surface==null)continue;
                    int a=Node(n),b=Node((x,y,z));
                    graphLinks.Add(new MapNavLink{id="e"+graphLinks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        a=graphNodes[a].id,b=graphNodes[b].id,kind=y==n.y?"walk":surface.Ramp?.kind??"ramp"});
                    if(visited.Add((x,y,z)))queue.Enqueue((x,y,z));
                }
            }
        }
        return new MapNav{nodes=graphNodes.ToArray(),links=graphLinks.ToArray()};
    }
}
}
