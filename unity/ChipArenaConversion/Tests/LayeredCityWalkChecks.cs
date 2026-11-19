using System;
using System.Collections.Generic;
using System.Linq;
using ChipArena.UnityConversion;

internal static class LayeredCityWalkChecks
{
    public static void Run()
    {
        var hands = new HashSet<string>();
        for (int previous = -1; previous < NativeCityLayout.Plans.Count; previous++)
        {
            var picks = new HashSet<int>();
            for (uint seed = 0; seed < 2000; seed++)
            {
                int pick = NativeCityLayout.Choose(seed, previous);
                if (pick == previous) throw new Exception("Repeated city layout");
                picks.Add(pick);
                hands.Add(string.Join("/", NativeCityLayout.DealHalls(seed, new[] { "a", "b", "c" })));
            }
            if (picks.Count != NativeCityLayout.Plans.Count - (previous < 0 ? 0 : 1))
                throw new Exception("Unreachable city layout");
        }
        if (hands.Count != 6) throw new Exception("Missing hall assignment");
        var exitProfiles = new HashSet<string>();
        foreach (int mode in new[] { 0, 1, 2 })
        {
            bool curated = mode == 2, large = mode != 0;
            int minBlocks = int.MaxValue, maxBlocks = 0, maxNodes = 0;
            var assignments = new HashSet<string>();
            for (uint seed = 0; seed < (curated ? NativeCityLayout.Plans.Count : 8); seed++)
            {
                var walk = curated ? new NativeLayeredCityWalk(NativeCityLayout.Plans[(int)seed]) : new NativeLayeredCityWalk(large, 647 + seed * 137);
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
                    foreach (var end in route.Points.Concat(route.RoofLoop))
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
                if (curated)
                {
                    for (int side = 0; side < 4; side++)
                    {
                        string profile = ExitProfile(walk.Routes[side], side);
                        if (!exitProfiles.Add(profile)) throw new Exception("Duplicate gate approach: " + walk.Map.name);
                        Console.WriteLine(walk.Map.name + " / " + walk.Routes[side].Name + " (heading/height/width, every 5 m): " + profile);
                    }
                    MeasureCorridors(walk);
                }
                if (map.nav.links.Length != map.nav.nodes.Length - 1 + (curated ? 4 : 0)) throw new Exception("Disconnected corridor trace");
                minBlocks = Math.Min(minBlocks, map.blocks.Length);
                maxBlocks = Math.Max(maxBlocks, map.blocks.Length);
                maxNodes = Math.Max(maxNodes, map.nav.nodes.Length);
            }
            if (!curated && assignments.Count < 2) throw new Exception("District deals did not vary");
            Console.WriteLine("PASS layered city " + (large ? "B" : "A") + (curated ? " · 5 candidates · " : " · 8 seeds · ")
                + minBlocks + "–" + maxBlocks + " blocks · up to " + maxNodes + " corridor nodes");
        }
    }

    private static string ExitProfile(NativeLayeredCityWalk.Route route, int side)
    {
        var samples = new List<string>();
        float relief = 0;
        for (int station = 0; station <= 60; station += 5)
        {
            route.Sample(station, out var foot, out var direction, out float width);
            double angle = Math.Atan2(direction.x, direction.z) * 180 / Math.PI - side * 90;
            angle -= 360 * Math.Floor((angle + 180) / 360);
            samples.Add(FormattableString.Invariant($"{angle:F0}/{foot.y:F1}/{width:F0}"));
            if (station <= 40) relief = Math.Max(relief, Math.Abs(foot.y));
        }
        if (samples.Count != 13 || relief < 6.99f || relief > 14.01f)
            throw new Exception("Gate lacks its early relief: " + route.Name + " " + relief);
        return string.Join(" ", samples);
    }

    // Isolate graph work from Unity physics and perception. Four independent destination
    // fields and 22 start nodes per field, warmed before sampling on this machine.
    private static void MeasureCorridors(NativeLayeredCityWalk walk)
    {
        var nav = walk.Map.nav;
        var search = new NativeNavSearch(nav);
        var path = new List<string>();
        var cells = nav.nodes;
        var flow = new System.Diagnostics.Stopwatch();
        var attach = new System.Diagnostics.Stopwatch();
        for (int tick = 0; tick < 720; tick++)
        {
            for (int pilot = 0; pilot < 4; pilot++)
            {
                var target = cells[(tick + pilot * cells.Length / 4) % cells.Length];
                if (tick >= 120) flow.Start();
                search.BuildFlow(target.id, _ => true);
                if (tick >= 120) flow.Stop();
                for (int mob = 0; mob < 22; mob++)
                {
                    var origin = cells[(tick + (pilot * 22 + mob) * 23) % cells.Length];
                    if (tick >= 120) attach.Start();
                    var start = search.NearestLocal(origin.pos, _ => true);
                    if (tick >= 120) attach.Stop();
                    if (tick >= 120) flow.Start();
                    search.TraceFlow(start.id, path);
                    if (tick >= 120) flow.Stop();
                    if (path.Count == 0 || path[path.Count - 1] != target.id)
                        throw new Exception("Unreachable corridor target on " + walk.Map.name);
                }
            }
        }
        Console.WriteLine($"{walk.Map.name}: routes {string.Join("/", walk.Routes.Select(r => r.Length.ToString("F1")))} m; " +
            $"{walk.Map.blocks.Length} blocks, {cells.Length} nodes; local attachment {attach.Elapsed.TotalMilliseconds / 600:F3} ms, " +
            $"four field rebuilds + 88 traces {flow.Elapsed.TotalMilliseconds / 600:F3} ms per sample");
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
