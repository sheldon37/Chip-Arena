using System;
using System.Linq;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    public static partial class NativeExpeditionGeneratedWorld
    {
        internal static void PopulateMeridian(ExpeditionGeneratedWorld world, NativeLayeredCityWalk walk)
        {
            world.Map = walk.Map;
            world.City=walk;
            var hubRoutes=new List<ExpeditionHubRoute>();
            var area = world.Area;
            area.map = walk.Map.id;
            var assigned = NativeCityLayout.DealHalls(world.Blueprint.worldSeed, world.Blueprint.instances);
            world.HallSlots=assigned;
            world.Bleed=NativeCityBleed.ForWalk(walk,assigned);
            var doors = new System.Collections.Generic.List<ExpeditionDoorLayout>();
            var anchors = new System.Collections.Generic.List<ExpeditionWorldAnchor>();
            bool NearCache(V3 pos)=>walk.Caches.Any(cache=>
                Math.Pow(pos.x-cache.x,2)+Math.Pow(pos.z-cache.z,2)<64);
            var spawnDraw = new SourceRandom(world.Blueprint.worldSeed ^ 0x902b43c1u);
            for (int side = 1; side < walk.Routes.Count; side++)
            {
                string instance = assigned[side], id = "exp-city:door:" + instance;
                var mouth = walk.Routes[side].Points.Last();
                var points = walk.Routes[side].Points;
                var leg = mouth - points[points.Length - 2];
                var forward = leg / leg.Length;
                var yaw = (float)(Math.Atan2(forward.x, forward.z) * 180 / Math.PI);
                var right = new V3(forward.z,0,-forward.x);
                ExpeditionPoint Spot(float inward, float across)
                {
                    var at = mouth - forward * inward + right * across;
                    return new ExpeditionPoint { x=at.x, y=at.y, z=at.z, yaw=yaw };
                }
                ExpeditionVolume Volume(ExpeditionPoint centre, float width, float depth) => new ExpeditionVolume {
                    x=centre.x, y=centre.y+2, z=centre.z, sx=width, sy=4, sz=depth, yaw=yaw };
                var join = Spot(8,0);
                var route=walk.Routes[side];
                var approach=Spot(4,0);
                float distance=route.Length-4;
                var gate=world.Hub.Gates[side];
                hubRoutes.Add(new ExpeditionHubRoute{To=instance,Gate=gate.Id,Path=distance,
                    Run=distance/6.8f,Sprint=distance/(6.8f*1.35f),Straight=(approach.Position-gate.Outside.Position).Length,
                    Approach=approach,X=approach.x,Z=approach.z,Yaw=yaw,Via=points.Skip(1).Take(points.Length-2)
                        .Select(p=>new ExpeditionPoint{x=p.x,y=p.y,z=p.z}).ToArray()});
                var district=world.Blueprint.districts.Single(d=>d.instance==instance);
                district.side=gate.Side;
                anchors.Add(new ExpeditionWorldAnchor { id=id, area=area.id, kind="door", building=instance,
                    x=mouth.x, y=mouth.y, z=mouth.z, yaw=yaw });
                doors.Add(new ExpeditionDoorLayout { anchor=id, approach=Spot(4,0), closure=Volume(Spot(0,0),8,.5f),
                    interact=Volume(Spot(2,0),5,5), join=new ExpeditionJoinCylinder { x=join.x,y=join.y,z=join.z,r=3,h=3 },
                    entries=Array.Empty<ExpeditionPoint>(), returns=Enumerable.Range(0,8).Select(i => Spot(6+i/4*2,i%4*2-3)).ToArray() });
            }
            anchors.AddRange(walk.Caches);
            area.doors = doors.ToArray();
            world.Hub.Routes=hubRoutes.ToArray();
            area.anchors = anchors.ToArray();
            area.interactions = walk.Caches.Select(NativeExpeditionGeneratedWorld.CacheInteraction).ToArray();
            var spawnRows = new System.Collections.Generic.List<ExpeditionSpawnLayout>();
            var zones = new System.Collections.Generic.List<ExpeditionHubZone>();
            string[] kinds = { "shambler", "runner", "spitter", "brute", "hulk" };
            // Use camp-connected navigation positions for encounter exits, including elevated streets.
            var floorNodes=walk.Map.nav.nodes.Select(n=>n.pos-new V3(0,.9f,0)).ToArray();
            var treadIds=new HashSet<string>(walk.Map.ramps.SelectMany(r=>r.blocks));
            var ready=new Dictionary<V3,bool>();
            bool OpenApron(V3 foot)
            {
                if(ready.TryGetValue(foot,out var result))return result;
                bool floor=false;
                foreach(var b in walk.Map.blocks){

                    float dx=Math.Abs(foot.x-b.pos.x), dz=Math.Abs(foot.z-b.pos.z);
                    float hx=b.size.x/2,hz=b.size.z/2;
                    if(b.rotation!=null&&Math.Abs(2*b.rotation.y*b.rotation.w)>.5f)(hx,hz)=(hz,hx);
                    if(dx>hx+3||dz>hz+3)continue;
                    float top=b.pos.y+b.size.y/2;
                    if(!treadIds.Contains(b.id)&&Math.Abs(top-foot.y)<.03f&&dx+3<=hx&&dz+3<=hz)floor=true;
                    if(top>foot.y+.04f&&b.pos.y-b.size.y/2<foot.y+3.3f)return ready[foot]=false;
                }
                foreach(var ramp in walk.Map.ramps){
                    if(foot.y+3.3f<=ramp.bottom||foot.y>ramp.high.y+.1f)continue;
                    var a=ramp.low;var b=ramp.high;
                    float wx=a.x==b.x?ramp.width*.5f:0,wz=a.z==b.z?ramp.width*.5f:0;
                    if(foot.x>=Math.Min(a.x,b.x)-wx-3&&foot.x<=Math.Max(a.x,b.x)+wx+3&&
                       foot.z>=Math.Min(a.z,b.z)-wz-3&&foot.z<=Math.Max(a.z,b.z)+wz+3)return ready[foot]=false;
                }
                return ready[foot]=floor&&!NearCache(foot);
            }
            V3 NearestExit(V3 opening)
            {
                // Only probe the map's collision boxes for nearby floor nodes.
                foreach(var foot in floorNodes.OrderBy(p=>V3.Dot(p-opening,p-opening)))
                    if(OpenApron(foot))return foot;
                throw new InvalidOperationException("No clear spawn apron in "+walk.Map.name);
            }
            foreach (var route in walk.Routes)
            {
                // Sample the gate-to-hall centre line at measured distances.
                for (int station = 40 + (int)(spawnDraw.Next()*11); station < route.Length - 24; station += 28)
                {
                    float remaining = station;
                    for (int j = 1; j < route.Points.Length; j++)
                    {
                        var a = route.Points[j-1];
                        var d = route.Points[j] - a;
                        if (remaining > d.Length) { remaining -= d.Length; continue; }
                        var pos = NearestExit(a + d * (remaining / d.Length));
                        if(NearCache(pos))break;
                        int id = spawnRows.Count;
                        var point = new PvePoint { x=pos.x, y=pos.y, z=pos.z };
                        spawnRows.Add(new ExpeditionSpawnLayout { allow=kinds, entry=new PveEntry {
                            e=id, id="meridian-street-"+id, kind="door", from=point, @exit=point, clear=3, allowed=kinds } });
                        zones.Add(new ExpeditionHubZone { Id="meridian-zone-"+id, Ring=station<55?"near":station<95?"mid":"far",
                            Near=station-5, Far=station+5, Spawns=new[] { id },
                            Volume=new ExpeditionVolume { x=pos.x, y=pos.y+2, z=pos.z, sx=11, sy=6, sz=11 } });
                        break;
                    }
                }
            }
            foreach (var roof in walk.RoofHatches.Where(p=>!NearCache(p)))
            {
                var landing=NearestExit(roof);
                var zone = zones.OrderBy(z => (new V3(z.Volume.x,z.Volume.y,z.Volume.z)-roof).Length).First();
                int id = spawnRows.Count;
                zone.Spawns = zone.Spawns.Concat(new[] { id }).ToArray();
                var point = new PvePoint { x=landing.x, y=landing.y, z=landing.z };
                spawnRows.Add(new ExpeditionSpawnLayout { allow=kinds, entry=new PveEntry {
                    e=id, id="meridian-roof-"+id, kind="drop", surface="roof", height=6.5f, from=new PvePoint{x=landing.x,y=landing.y+6.5f,z=landing.z}, @exit=point, clear=3, allowed=kinds } });
            }
            world.Hub.Zones = zones.ToArray();
            var court = walk.Routes[0].Points.Last();
            area.bossPoints = new[] { new ExpeditionPoint { x=court.x, y=court.y, z=court.z } };
            var end=walk.Routes[0].Points;
            var outward=(court-end[end.Length-2]).Normalized;
            var barrier=court-outward*12;
            float angle=(float)(Math.Atan2(outward.x,outward.z)*180/Math.PI);
            area.gates=new[]{new ExpeditionGateLayout{id="exp-city:final-gate",opens="final",
                closure=new ExpeditionVolume{x=barrier.x,y=barrier.y+3,z=barrier.z,sx=12,sy=6,sz=.5f,yaw=angle}}};
            area.bossTrigger=new ExpeditionVolume{x=court.x-outward.x*4,y=court.y+2,z=court.z-outward.z*4,sx=8,sy=4,sz=6,yaw=angle};
            area.spawns = spawnRows.ToArray();
        }

    }
}
