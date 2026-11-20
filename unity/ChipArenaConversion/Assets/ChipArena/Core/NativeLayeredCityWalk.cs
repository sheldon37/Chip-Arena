#if UNITY_EDITOR || DEVELOPMENT_BUILD || ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using static ChipArena.UnityConversion.NativeCityLayout;

namespace ChipArena.UnityConversion
{
    // The A4 scale walk. Geometry uses the same ContentBlock/MapRamp collision path as shipped arenas.
    // This map never enters deployment tickets, the PvP rotation, or the shared map catalogue.
    public sealed partial class NativeLayeredCityWalk
    {
        public readonly ContentMap Map;
        public readonly List<Route> Routes = new List<Route>();
        public sealed class Route
        {
            public string Name;
            public V3[] Points;
            public float[] Widths;
            public string[] Kinds, Covers;
            public V3 Destination;
            public V3[] HordePoints;
            public V3[] RoofLoop = Array.Empty<V3>();
            // Horizontal station measured from the gate, in the route's own frame.
            public void Sample(float station, out V3 foot, out V3 direction, out float width)
            {
                for (int i = 0; i < Points.Length - 1; i++)
                {
                    var delta = Points[i + 1] - Points[i];
                    float length = (float)Math.Sqrt(delta.x * delta.x + delta.z * delta.z);
                    if (length <= 0) continue;
                    if (station < length || i == Points.Length - 2)
                    {
                        foot = Points[i] + delta * (Math.Min(station, length) / length);
                        direction = new V3(delta.x, 0, delta.z) / length;
                        width = i < Widths.Length ? Widths[i] : 15;
                        return;
                    }
                    station -= length;
                }
                throw new InvalidOperationException("Route has no horizontal extent.");
            }
            public float Length => Points.Skip(1).Select((p, i) => (p - Points[i]).Length).Sum();
        }

        private readonly List<ContentBlock> blocks = new List<ContentBlock>();
        private readonly List<MapRamp> ramps = new List<MapRamp>();
        private readonly SourceRandom random;
        private readonly float halfX, halfZ;
        private const float Cut = -21;

        public readonly List<V3> RoofHatches = new List<V3>();

        public ExpeditionWorldAnchor[] Caches { get; private set; } = Array.Empty<ExpeditionWorldAnchor>();
        public NativeLayeredCityWalk(Plan plan) : this(true, plan.Seed, plan) { }
        public NativeLayeredCityWalk(Plan plan,uint worldSeed) : this(true,worldSeed,plan) { }
        public NativeLayeredCityWalk(bool large, uint seed) : this(large, seed, null) { }
        private NativeLayeredCityWalk(bool large, uint seed, Plan plan)
        {

            halfX = large ? 300 : 210;
            halfZ = large ? 240 : 170;
            random = new SourceRandom(seed);
            // The gate openings and ring stay the same dimensions in both options.
            for (int side = 0; side < 4; side++)
                foreach (int sign in new[] { -1, 1 })
                    Slab(side, "camp-fence-" + side + "-" + sign, 24.8f, 25, sign * 15, 20, 0, 3.2f);

            MapBounds bounds;
            if (plan != null)
            {
                bounds = BuildStreetPlan(plan.Seed);
                // The Navigation's towpath roof meets the upper quay at grade. Pilots and Hulks can walk on from the bank.
                if(plan.Seed==784)Box("towpath-store-roof",-30,-80,12,10,-.6f,0);
                Caches=NativeCityLayout.CacheAnchors(plan,seed);
                foreach(var cache in Caches)
                {
                    if(!NativeExpeditionGeneratedWorld.CacheColumnClear(cache,blocks))
                        throw new InvalidOperationException("Meridian cache under an overhang: "+cache.id);
                    blocks.Add(NativeExpeditionGeneratedWorld.CacheBlock(cache));
                }
                var roof=Caches[0];
                var f=NativeExpeditionGeneratedWorld.CacheForward(roof.yaw);
                Box("cache-roof-kerb",roof.x-.55f*f.x,roof.z-.55f*f.z,Math.Abs(f.x)<.5f?2:.35f,Math.Abs(f.x)<.5f?.35f:2,roof.y,roof.y+.25f);
            }
            else
            {
                Box("cut-ground", 0, 0, halfX * 2, halfZ * 2, Cut - 2, Cut, "foundation");
                Box("terrace-sanctuary", 0, 0, 50, 50, Cut, 0);
                var districts = new List<(string name, float level)> {
                    ("Ward Zero", -7), ("Central Station", Cut), ("City Ironworks", -14),
                    ("The Poolrooms", -17.5f), ("Level 0", -14)
                };
                BuildRoute(0, "City Hall", 14, large);
                for (int side = 1; side < 4; side++)
                {
                    int pick = (int)(random.Next() * districts.Count);
                    BuildRoute(side, districts[pick].name, districts[pick].level, large);
                    districts.RemoveAt(pick);
                }
                BuildCarPark();
                BuildViaduct();
                BuildLots();
                Cordon();
                bounds = new MapBounds { minX = -halfX, maxX = halfX, minZ = -halfZ, maxZ = halfZ, ceiling = 60, killY = -25 };
            }
            var spawns = Enumerable.Range(0, 8).Select(i => new MapSpawn {
                pos = new V3(-6 + 4 * (i % 4), .9f, -4 + 4 * (i / 4))
            }).ToArray();
            Map = new ContentMap {
                id = "expedition-city-walk-" + (plan == null ? (large ? "b" : "a") : plan.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)), name = plan?.Name ?? "Meridian scale walk",
                theme = "expedition", look = "expedition-city", unity = true, sky = 0x617488,
                floor = 0x555a62, accent = 0xc3a572, blocks = blocks.ToArray(), ramps = ramps.ToArray(),
                bounds = bounds,
                spawns = new MapSpawns { ffa = spawns, teams = new[] { 0, 1 }.Select(i => new MapTeamSpawns {
                    side = i, points = spawns.Skip(4 * i).Take(4).ToArray()
                }).ToArray() },
                segments = Array.Empty<StructureDefinition>(), hazards = Array.Empty<MapHazard>(),
                pilotBlocks = plan == null ? Array.Empty<MapPilotBlock>() : blocks
                    .Where(b => b.id == "cordon-n" || b.id == "cordon-e" || b.id == "cordon-s" || b.id == "cordon-w")
                    .Select(b => new MapPilotBlock { id = b.id + "-edge", center = b.pos, size = b.size,
                        bottom = b.pos.y + b.size.y / 2, bots = true }).ToArray(),
                nav = RouteNavigation()
            };
            NativeMapRules.Validate(Map);
        }


        // Sample the walk's centre lines at two metres for the route traces. The M5 release
        // needs surface-wide navigation; these four preview corridors are intentionally bounded.
        private MapNav RouteNavigation()
        {
            var nodes = new List<MapNavNode> { new MapNavNode { id = "camp", pos = new V3(0, .9f, 0) } };
            var links = new List<MapNavLink>();
            for (int side = 0; side < Routes.Count; side++)
            {
                var previous = nodes[0];
                var start = new V3();
                void Sample(V3 end, string grade)
                {
                    int steps = (int)Math.Ceiling((end - start).Length / 2f);
                    for (int step = 1; step <= steps; step++)
                    {
                        var foot = start + (end - start) * ((float)step / steps);
                        var node = new MapNavNode { id = "path-" + nodes.Count, pos = foot + new V3(0, .9f, 0),
                            layer = (int)Math.Round(foot.y / 3.5f) };
                        nodes.Add(node);
                        links.Add(new MapNavLink { id = "link-" + links.Count, a = previous.id, b = node.id,
                            kind = start.y == end.y ? "walk" : grade });
                        previous = node;
                    }
                    start = end;
                }
                var route = Routes[side];
                var path = route.HordePoints ?? route.Points;
                foreach (var end in path)
                {
                    string grade = route.Kinds == null && side == 0 ? "stair" : "ramp";
                    if (end.y != start.y && ramps.Any(r => r.kind == "stair" &&
                        Math.Min(start.y,end.y) == r.low.y && Math.Max(start.y,end.y) == r.high.y &&
                        (end-r.high).Length + (start-r.low).Length < .01f))
                        grade = "stair";
                    Sample(end, grade);
                }
            }
            return new MapNav { nodes = nodes.ToArray(), links = links.ToArray() };
        }

        private void Box(string id, float x, float z, float width, float depth, float bottom, float top, string material = "stone")
        {
            blocks.Add(new ContentBlock { id = id, role = "static", material = material,
                pos = new V3(x, (bottom + top) / 2, z), size = new V3(width, top - bottom, depth), rotation = new ContentRotation() });
        }

        // u leads out from the gate; v runs to a pilot's right.
        private static V3 Point(int side, float u, float v, float y)
        {
            switch (side)
            {
                case 0: return new V3(v, y, u);
                case 1: return new V3(u, y, -v);
                case 2: return new V3(-v, y, -u);
                default: return new V3(-u, y, v);
            }
        }

        private void Slab(int side, string id, float u0, float u1, float v, float width, float bottom, float top)
        {
            var p = Point(side, (u0 + u1) / 2, v, top);
            bool eastWest = side % 2 != 0;
            Box(id, p.x, p.z, eastWest ? u1 - u0 : width, eastWest ? width : u1 - u0, bottom, top);
        }

        private void Ramp(string id, V3 a, V3 b, float width, bool stairs = false)
        {
            var low = a.y < b.y ? a : b;
            var high = a.y < b.y ? b : a;
            int steps = (int)Math.Ceiling((high.y - low.y) / (stairs ? .25f : .5f));
            var visual = new string[steps];
            bool alongX = low.x != high.x;
            var delta = (high - low) * (1f / steps);
            for (int i = 0; i < steps; i++)
            {
                var mid = low + delta * (i + .5f);
                visual[i] = id + "-tread-" + i;
                Box(visual[i], mid.x, mid.z, alongX ? Math.Abs(delta.x) : width,
                    alongX ? width : Math.Abs(delta.z), low.y - .25f, mid.y);
            }
            ramps.Add(new MapRamp { id = id, kind = stairs ? "stair" : "ramp", low = low, high = high,
                width = width, bottom = low.y - .25f, blocks = visual });
        }

        private void BuildRoute(int side, string district, float level, bool large)
        {
            string id = "street-" + side;
            float end = (side % 2 == 0 ? halfZ : halfX) - 12;
            // North/south are shorter axes; a right-angle jog at the terrace restores the walked scale.
            float offset = (side % 2 == 0 ? (large ? 57 : 30) : (large ? 12 : 10)) * (random.Next() < .5 ? -1 : 1);
            float firstLevel = side == 0 ? 14 : -7;
            float start = 25, bend = side == 0 ? 65 : 74;
            var path = new List<V3> { Point(side, start, 0, 0) };
            float exitOffset = 0;
            var widths = new List<float>();
            if (side == 0 && firstLevel == 14 && bend == 65)
            {
                float u = start;
                for (int i = 0; i < 4; i++)
                {
                    Ramp("hundred-steps-" + i, Point(side, u, 0, 3.5f * i), Point(side, u + 7, 0, 3.5f * (i + 1)), 14, true);
                    path.Add(Point(side, u + 7, 0, 3.5f * (i + 1)));
                    Slab(side, "stair-landing-" + i, u + 7, u + 10, 0, 14, 3.5f * (i + 1) - 1, 3.5f * (i + 1));
                    u += 10;
                    path.Add(Point(side, u, 0, 3.5f * (i + 1)));
                }
            }
            else
            {
                Ramp(id + "-knoll-slope", path[0], Point(side, bend, 0, firstLevel), 15);
                path.Add(Point(side, bend, 0, firstLevel));
            }
            Slab(side, "terrace-bend-" + side, bend, bend + 16, (offset + exitOffset) / 2, Math.Abs(offset - exitOffset) + 16, firstLevel - 2, firstLevel);
            path.Add(Point(side, bend + 8, exitOffset, firstLevel));
            widths.Add(16);
            if (side == 3)
            {
                // Every seed has a two-way walk between the lowest street and the camp.
                float lip = Math.Min(0, offset) - 8;
                Ramp("cut-haul-ramp", Point(side, bend + 8, lip - 84, Cut), Point(side, bend + 8, lip, firstLevel), 8);
            }
            path.Add(Point(side, bend + 8, offset, firstLevel)); widths.Add(16);
            float next = bend + 16;
            path.Add(Point(side, next, offset, firstLevel));
            if (firstLevel != level)
            {
                // A turn in the descent makes room for the rail cut at the end of the short axis.
                float run = Math.Min(Math.Abs(firstLevel - level) * 6, end - next - 12);
                Ramp(id + "-descent", path[path.Count - 1], Point(side, next + run, offset, level), 15);
                next += run;
                path.Add(Point(side, next, offset, level));
            }
            if (next < end - 30) Slab(side, "terrace-" + side + "-approach", next, end - 30, offset, 15, level - 1, level);
            path.Add(Point(side, end, offset, level));
            // Build the plaza, a roof-level bypass and the mass of the hall beside its entrance.
            float plazaStart = end - 30;
            float frontage = Math.Max(next, plazaStart);
            Slab(side, "terrace-hall-plaza-" + side, frontage, end + 3, offset, 62, level - 1, level);
            float roof = level + 7;
            Slab(side, "lot-hall-wing-" + side, end - 9, end + 3, offset + 23, 15, level, roof);
            Slab(side, "roof-parapet-" + side, end - 9, end + 3, offset + 30, 1, roof, roof + 1);
            // A stair beside the hall brings an unchipped pilot onto its roof.
            Ramp("roof-fire-escape-" + side, Point(side, end - 23, offset + 23, level),
                Point(side, end - 9, offset + 23, roof), 3, true);
            if (frontage > end - 23)
                Slab(side, "roof-landing-" + side, end - 26, frontage, offset + 23, 6, level - 1, level);
            Slab(side, "lot-hall-annex-" + side, end - 9, end + 3, offset - 23, 15, level, roof);
            Slab(side, "bridge-hall-" + side, end - 8, end - 5, offset, 31, roof - .4f, roof);
            // Keep each district legible in untextured collision geometry.
            if (district == "Central Station")
            {
                foreach (int sign in new[] { -1, 1 })
                    Slab(side, "cut-track-" + sign, frontage, end + 3, offset + sign * 5, .18f, level, level + .12f);
            }
            else if (district == "The Poolrooms")
            {
                foreach (int sign in new[] { -1, 1 })
                    Slab(side, "basin-rim-" + sign, frontage, end - 8, offset + sign * 10, 1, level, level + .6f);
            }
            else if (district == "Ward Zero")
            {
                foreach (int sign in new[] { -1, 1 })
                    Slab(side, "ridge-wall-" + sign, frontage, end - 10, offset + sign * 12, 1.5f, level, level + 4);
            }
            // Hall mouth is open at the approach point.
            foreach (int sign in new[] { -1, 1 })
                Slab(side, "landmark-" + side + "-pier-" + sign, end - 2, end + 3, offset + 6 * sign, 3, level, level + 9);
            Slab(side, "landmark-" + side + "-lintel", end - 2, end + 3, offset, 15, level + 9, level + 12);
            var route = new Route { Name = district, Points = path.ToArray(), Widths = widths.ToArray() };
            Routes.Add(route);
        }

        private void BuildCarPark()
        {
            float x = 57, z = -47;
            const float width = 20;
            for (int i = 0; i < 3; i++)
            {
                float low = Cut + i * 3.5f, high = low + 3.5f;
                bool east = i % 2 == 0;
                float lane = z - 8 + i * 7;
                Ramp("carpark-ramp-" + i, new V3(x + (east ? 0 : width), low, lane),
                    new V3(x + (east ? width : 0), high, lane), 6);
                Box("carpark-deck-" + i, x + (east ? width + 3 : -3), lane + 3.5f, 6, 14, high - .4f, high);
            }
            Box("carpark-roof-plaza", x + 29, z + 6, 6, 18, Cut + 10, Cut + 10.5f);
        }

        private void BuildViaduct()
        {
            // The viaduct crosses the station tracks, or passes under the Crag if the station was not dealt.
            int station = Routes.FindIndex(r => r.Name == "Central Station");
            int side = station < 0 ? 0 : station;
            float u = station < 0 ? 105 : (side % 2 == 0 ? halfZ : halfX) - 43;
            Slab(side, "viaduct-deck", u - 4, u + 4, 0, 196, -8, -7);
            foreach (int v in new[] { -94, -73, 73, 94 })
                Slab(side, "viaduct-pier-" + v, u - 3, u + 3, v, 3, Cut, -8);
            foreach (int sign in new[] { -1, 1 })
                Ramp("viaduct-approach-" + sign, Point(side, u, sign * 154, Cut), Point(side, u, sign * 98, -7), 8);
        }

        private void BuildLots()
        {
            // Index the footprint of the reserved structures, ramps and hall routes before filling lots.
            var reserved = blocks.Where(b => b.id != "cut-ground").ToArray();
            for (float z = -halfZ + 20; z < halfZ - 10; z += 25)
                for (float x = -halfX + 20; x < halfX - 10; x += 25)
                {
                    float width = random.Next() < .5 ? 10 : 15;
                    if (reserved.Any(b => Math.Abs(b.pos.x - x) < (b.size.x + width) / 2 + 5 &&
                                          Math.Abs(b.pos.z - z) < (b.size.z + 15) / 2 + 5)) continue;
                    float height = random.Next() < .09 ? 25 + 5 * (int)(random.Next() * 7) : 7 + 3 * (int)(random.Next() * 6);
                    Box("lot-" + blocks.Count, x, z, width, 15, Cut, Cut + height);
                }
        }

        private void Cordon()
        {
            for (int side = 0; side < 4; side++)
            {
                float u = side % 2 == 0 ? halfZ : halfX, v = side % 2 == 0 ? halfX : halfZ;
                Slab(side, "cordon-" + side, u - 1, u, 0, 2 * v, Cut, Cut + 14);
                for (float offset = -v + 10; offset < v; offset += 60)
                    Slab(side, "cordon-tower-" + blocks.Count, u - 4, u, offset, 4, Cut, Cut + 23);
            }
        }
    }
}
#endif
