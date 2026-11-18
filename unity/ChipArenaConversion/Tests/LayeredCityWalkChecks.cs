using System;
using System.Collections.Generic;
using System.Linq;
using ChipArena.UnityConversion;

internal static class LayeredCityWalkChecks
{
    public static void Run()
    {
        foreach (bool large in new[] { false, true })
        {
            int minBlocks = int.MaxValue, maxBlocks = 0, maxNodes = 0;
            var assignments = new HashSet<string>();
            for (uint seed = 0; seed < 8; seed++)
            {
                var walk = new NativeLayeredCityWalk(large, 647 + seed * 137);
                var map = walk.Map;
                if (map.bounds.maxX != (large ? 300 : 210) || map.bounds.maxZ != (large ? 240 : 170))
                    throw new Exception("Layered city size");
                var visual = new HashSet<string>(map.ramps.SelectMany(r => r.blocks));
                var solids = map.blocks.Where(b => !visual.Contains(b.id)).ToArray();
                if (walk.Routes.Count != 4 || walk.Routes.Select(r => r.Name).Distinct().Count() != 4)
                    throw new Exception("Expected four different destinations");
                assignments.Add(string.Join("/", walk.Routes.Select(r => r.Name)));
                foreach (var route in walk.Routes)
                {
                    if (route.Length < (large ? 250 : 150) || route.Length > (large ? 300 : 190))
                        throw new Exception(route.Name + " route length " + route.Length);
                    V3 start = new V3();
                    foreach (var end in route.Points)
                    {
                        var delta = end - start;
                        float horizontal = (float)Math.Sqrt(delta.x * delta.x + delta.z * delta.z);
                        if (horizontal <= 0) throw new Exception("Zero-length segment");
                        var across = new V3(-delta.z, 0, delta.x) / horizontal;
                        int count = (int)Math.Ceiling(horizontal / 1.5f);
                        for (int n = 0; n <= count; n++)
                            foreach (float offset in new[] { -NativeHordeKinds.HulkRadius, 0, NativeHordeKinds.HulkRadius })
                            {
                                var foot = start + delta * ((float)n / count) + across * offset;
                                bool supported = false;
                                foreach (var block in solids)
                                {
                                    if (Math.Abs(block.pos.x - foot.x) > block.size.x / 2 + .001f ||
                                        Math.Abs(block.pos.z - foot.z) > block.size.z / 2 + .001f) continue;
                                    float top = block.pos.y + block.size.y / 2, bottom = top - block.size.y;
                                    if (Math.Abs(top - foot.y) < .02f) supported = true;
                                    if (top > foot.y + .05f && bottom < foot.y + NativeHordeKinds.HulkHeight)
                                        throw new Exception("Blocked " + route.Name + " by " + block.id);
                                }
                                if (!supported) supported = map.ramps.Any(r => OnRamp(r, foot));
                                if (!supported) throw new Exception("Unsupported " + route.Name + " at " + foot.x + "," + foot.y + "," + foot.z);
                            }
                        start = end;
                    }
                }
                if (map.nav.links.Length != map.nav.nodes.Length - 1) throw new Exception("Disconnected corridor trace");
                minBlocks = Math.Min(minBlocks, map.blocks.Length);
                maxBlocks = Math.Max(maxBlocks, map.blocks.Length);
                maxNodes = Math.Max(maxNodes, map.nav.nodes.Length);
            }
            if (assignments.Count < 2) throw new Exception("District deals did not vary");
            Console.WriteLine("PASS layered city " + (large ? "B" : "A") + " · 8 seeds · "
                + minBlocks + "–" + maxBlocks + " blocks · up to " + maxNodes + " corridor nodes");
        }
    }

    private static bool OnRamp(MapRamp r, V3 p)
    {
        var d = r.high - r.low;
        float length = (float)Math.Sqrt(d.x * d.x + d.z * d.z);
        var offset = p - r.low;
        float along = (d.x * offset.x + d.z * offset.z) / length;
        float across = (d.z * offset.x - d.x * offset.z) / length;
        return along >= -.001f && along <= length + .001f && Math.Abs(across) <= r.width / 2 + .001f &&
               Math.Abs(p.y - (r.low.y + along / length * d.y)) < .02f;
    }
}
