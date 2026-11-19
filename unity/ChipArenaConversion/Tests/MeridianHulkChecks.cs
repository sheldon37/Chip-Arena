using System;
using System.Collections.Generic;
using System.Linq;
using ChipArena.UnityConversion;

// Sweep the walk's physical boxes and wedges independently of its four HUD traces.
// Work on a 2 m lattice and keep separate levels at the same x/z (bridges and lids).
internal static class MeridianHulkChecks
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
            float t = Math.Clamp(((x-a.x)*d.x+(z-a.z)*d.z)/(d.x*d.x+d.z*d.z),0,1);
            return a.y+t*d.y;
        }
    }

    public static void Run(NativeLayeredCityWalk walk)
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
        var map=walk.Map;
        var treads=map.ramps.SelectMany(r=>r.blocks).ToHashSet();
        foreach (var b in map.blocks.Where(b=>!treads.Contains(b.id)))
            Add(new Surface { X=b.pos.x,Z=b.pos.z,W=b.size.x/2,D=b.size.z/2,
                Bottom=b.pos.y-b.size.y/2,Top=b.pos.y+b.size.y/2,
                Street=!b.id.StartsWith("fire-escape-",StringComparison.Ordinal) });
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

        var visited=new HashSet<(int x,float y,int z)>();
        var queue=new Queue<(int x,float y,int z)>();
        var start=(0,0f,0);
        if(!nodes[(0,0)].Contains(0)) throw new Exception("Obstructed Meridian camp");
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
                    if(Math.Abs(n.y-y)>1.01f || visited.Contains((x,y,z))) continue;
                    int mx=n.x+dx/2,mz=n.z+dz/2;
                    var surfaces=bins[(Bin(mx),Bin(mz))];
                    float mid=(y+n.y)/2;
                    if(!surfaces.Any(s=>s.Street && Inside(s,mx,mz) && Math.Abs(s.At(mx,mz)-mid)<.3f
                        && Room(surfaces,mx,s.At(mx,mz),mz))) continue;
                    visited.Add((x,y,z));
                    queue.Enqueue((x,y,z));
                }
            }
        }
        foreach(var route in walk.Routes)
        {
            var door=route.Destination;
            if(!visited.Any(n=>Math.Abs(n.y-door.y)<.35 && Math.Abs(n.x-door.x)<=2 && Math.Abs(n.z-door.z)<=2))
                throw new Exception("Hulk surface graph cannot reach "+walk.Map.name+" / "+route.Name);
        }
        Console.WriteLine($"{map.name}: Hulk surface graph {visited.Count} nodes connected to camp, {nodes.Values.Sum(v=>v.Count)} standable nodes, all four destinations reached");
    }
}
