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
        var exits = new List<Exit>();
        foreach (int mode in new[] { 0, 1, 2 })
        {
            bool curated = mode == 2, large = mode != 0;
            int minBlocks = int.MaxValue, maxBlocks = 0, maxNodes = 0;
            var assignments = new HashSet<string>();
            for (uint seed = 0; seed < (curated ? NativeCityLayout.Plans.Count : 8); seed++)
            {
                var walk = curated ? new NativeLayeredCityWalk(NativeCityLayout.Plans[(int)seed]) : new NativeLayeredCityWalk(large, 647 + seed * 137);
                var map = walk.Map;
                if (map.bounds.maxX - map.bounds.minX != (large ? 600 : 420) || map.bounds.maxZ - map.bounds.minZ != (large ? 480 : 340))
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
                    foreach (var end in (route.HordePoints ?? route.Points).Concat(route.RoofLoop))
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
                                        throw new Exception($"Blocked {route.Name} by {block.id} at {foot.x},{foot.y},{foot.z}");
                                }
                                if (!supported) supported = map.ramps.Any(r => OnRamp(r, foot, curated && offset != 0 ? .35f : .02f));
                                if (!supported) throw new Exception("Unsupported " + route.Name + " at " + foot.x + "," + foot.y + "," + foot.z);
                            }
                        start = end;
                    }
                }
                if (curated)
                {
                    for (int side = 0; side < 4; side++)
                    {
                        var route = walk.Routes[side];
                        if ((route.Points.Last() - route.Destination).Length > .5f)
                            throw new Exception("Missed destination on " + route.Name);
                        exits.Add(new Exit(route, solids, map.ramps));
                    }
                    // Conservative block pass: every greybox block drawn at once with the
                    // same stage meshes as the generated-city census, before street art.
                    long high = (long)map.blocks.Length * (NativeToonMesh.Wrap().Triangles.Length / 3);
                    long low = (long)map.blocks.Length * (NativeToonMesh.Build().Triangles.Length / 3);
                    if (high > GeneratedFrameChecks.GateHighTris || low > GeneratedFrameChecks.GateLowTris)
                        throw new Exception("Meridian greybox exceeds the block triangle budget");
                    Console.WriteLine($"{map.name}: greybox block-pass triangles High {high}, Low {low}");
                    CheckEscapeClearance(map,solids);
                    MeridianHulkChecks.Run(walk);
                    MeasureCorridors(walk);
                }
                if (map.nav.links.Length != map.nav.nodes.Length - 1) throw new Exception("Disconnected corridor trace");
                minBlocks = Math.Min(minBlocks, map.blocks.Length);
                maxBlocks = Math.Max(maxBlocks, map.blocks.Length);
                maxNodes = Math.Max(maxNodes, map.nav.nodes.Length);
            }
            if (!curated && assignments.Count < 2) throw new Exception("District deals did not vary");
            Console.WriteLine("PASS layered city " + (large ? "B" : "A") + (curated ? " · 5 candidates · " : " · 8 seeds · ")
                + minBlocks + "–" + maxBlocks + " blocks · up to " + maxNodes + " corridor nodes");
        }
        if (exits.Count != 20) throw new Exception("Expected twenty Meridian exits");
        int pairs = 0;
        for (int i = 0; i < exits.Count; i++)
            for (int j = 0; j < i; j++)
            {
                if (exits[i].Differences(exits[j]) < 2)
                    throw new Exception("Similar exits: " + exits[i].Name + " and " + exits[j].Name);
                pairs++;
            }
        Console.WriteLine("PASS " + pairs + " exit pairs at 2 m intervals");
    }

    private static void CheckEscapeClearance(ContentMap map, ContentBlock[] solids)
    {
        var flights = map.ramps.Where(r => r.id.StartsWith("fire-escape-", StringComparison.Ordinal));
        var escapes = new HashSet<string>();
        foreach (var r in flights)
        {
            string escape = r.id.Substring(0,r.id.LastIndexOf('-')) + "-";
            escapes.Add(escape);
            var delta = r.high-r.low;
            int samples = (int)Math.Ceiling(delta.Length*2);
            for (int i=0;i<=samples;i++)
            {
                var foot = r.low + delta*((float)i/samples);
                foreach (var block in solids)
                {
                    float top = block.pos.y+block.size.y/2;
                    if (top<=foot.y+NativeLocomotionRules.StepHeight ||
                        top-block.size.y>=foot.y+2*NativeLocomotionRules.HalfHeight) continue;
                    float dx=Math.Max(0,Math.Abs(block.pos.x-foot.x)-block.size.x/2);
                    float dz=Math.Max(0,Math.Abs(block.pos.z-foot.z)-block.size.z/2);
                    if (dx*dx+dz*dz<NativeLocomotionRules.Radius*NativeLocomotionRules.Radius &&
                        !block.id.StartsWith(escape,StringComparison.Ordinal))
                        throw new Exception($"{map.name} {r.id} blocked by {block.id}");
                }
            }
        }
        Console.WriteLine($"{map.name}: pilot clearance on {escapes.Count} built fire escapes");
    }

    private sealed class Exit
    {
        public readonly string Name;
        readonly float[] profile = new float[31];
        readonly List<(float station, int side)> turns = new List<(float, int)>();
        readonly HashSet<string> cover = new HashSet<string>();
        readonly List<string> approach = new List<string>();
        readonly float width;

        public Exit(NativeLayeredCityWalk.Route route, ContentBlock[] solids, MapRamp[] ramps)
        {
            Name = route.Name;
            for (int i = 0; i < profile.Length; i++)
            {
                route.Sample(i * 2, out var p, out _, out _);
                // Select the physical surface at this route's layer (bridges can span another road).
                var heights = solids.Where(b => Math.Abs(b.pos.x-p.x) <= b.size.x/2 &&
                    Math.Abs(b.pos.z-p.z) <= b.size.z/2).Select(b => b.pos.y+b.size.y/2);
                foreach (var r in ramps)
                    if (OnRamp(r, p)) heights = heights.Append(RampHeight(r,p));
                profile[i] = heights.OrderBy(y => Math.Abs(y-p.y)).First();
                if (Math.Abs(profile[i]-p.y) > .35f) throw new Exception("Exit surface: " + Name);
            }
            var lengths = new Dictionary<float,float>();
            V3 previous = default;
            float station = 0;
            for (int i = 0; i < route.Kinds.Length && station < 60; i++)
            {
                var d = route.Points[i+1]-route.Points[i];
                float length = (float)Math.Sqrt(d.x*d.x+d.z*d.z);
                float span = Math.Min(length,60-station);
                var heading = new V3(d.x/length,0,d.z/length);
                if (i > 0 && (heading-previous).Length > .01f)
                    turns.Add((station, Math.Sign(previous.z*heading.x-previous.x*heading.z)));
                previous = heading;
                float w = route.Widths[i];
                lengths.TryGetValue(w,out float total);
                lengths[w] = total + span;
                cover.Add(route.Covers[i]);
                string kind = route.Kinds[i] + (d.y > 0 ? "+" : d.y < 0 ? "-" : "");
                if (approach.LastOrDefault() != kind) approach.Add(kind);
                station += span;
            }
            width = lengths.OrderByDescending(kv=>kv.Value).ThenBy(kv=>kv.Key).First().Key;
        }

        public int Differences(Exit b)
        {
            double rms = Math.Sqrt(profile.Zip(b.profile,(x,y)=>(x-y)*(x-y)).Average());
            bool turn = turns.Count != b.turns.Count || turns.Where((t,i) =>
                t.side != b.turns[i].side || Math.Abs(t.station-b.turns[i].station)>10).Any();
            return (rms>=1.5 ? 1 : 0) + (turn ? 1 : 0) + (Math.Abs(width-b.width)>=3 ? 1 : 0)
                + (cover.SetEquals(b.cover) ? 0 : 1) + (approach.SequenceEqual(b.approach) ? 0 : 1);
        }
    }

    private static float RampHeight(MapRamp r,V3 p)
    {
        var d=r.high-r.low;
        return r.low.y + d.y*((p.x-r.low.x)*d.x+(p.z-r.low.z)*d.z)/(d.x*d.x+d.z*d.z);
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

    private static bool OnRamp(MapRamp r, V3 p, float tolerance = .02f)
    {
        var d = r.high - r.low;
        float length = (float)Math.Sqrt(d.x * d.x + d.z * d.z);
        var offset = p - r.low;
        float along = (d.x * offset.x + d.z * offset.z) / length;
        float across = (d.z * offset.x - d.x * offset.z) / length;
        return along >= -.001f && along <= length + .001f && Math.Abs(across) <= r.width / 2 + .001f &&
               Math.Abs(p.y - (r.low.y + along / length * d.y)) < tolerance;
    }
}
