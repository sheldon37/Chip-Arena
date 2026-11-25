using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's described route (owner 2026-11-25; docs/expedition/MERIDIAN-ROUTE.md; Core NativeMeridianRoute, Runtime
    /// NativeMeridianRouteDraw, tools/stages/meridian_route.py): every street plan's live deal placed as ShowMeridian places it.
    /// Crown Hill alone carries the walk's pieces (the crossing, footways, balustrades, corner shop, sign, roofs); the balustrades
    /// replace exactly the Hundred Steps' guard boxes (every other flight keeps its own); the shop covers exactly its lot's ground
    /// storey on the two faces it turns to the junction; the keep-outs stand on the route; on every plan with the Ironworks dealt its
    /// loading bay takes the two wing bays right of the door and three plumes rise over the far stacks, outside the map; the smoke
    /// stays in the sky; the plan is deterministic; the runtime hooks are in place. With CHIPARENA_ROUTE_BANK (a path to
    /// ChipArenaCityRoute.bytes) every piece's key is in the bank, under its triangle cap. `--meridian-route-dump OUT.json` writes
    /// Crown Hill's blocks and the placement for the bank script.
    /// </summary>
    internal static class MeridianRouteChecks
    {
        private static int count;
        private static void Check(bool ok, string what) { count++; if (!ok) throw new Exception("Meridian route: " + what); }

        /// <summary>The most triangles a route piece may have at a tier (0 Fine, 2 Low): the bank script refuses more.</summary>
        public static int Cap(string key, int tier)
        {
            switch (key)
            {
                case NativeMeridianRoute.Rails: return tier == 0 ? 24000 : 5000;
                case NativeMeridianRoute.Crossing: return tier == 0 ? 4000 : 1500;
                case NativeMeridianRoute.Footway: return tier == 0 ? 6000 : 2000;
                case NativeMeridianRoute.Corner: return tier == 0 ? 26000 : 6000;
                case NativeMeridianRoute.Sign: return tier == 0 ? 6000 : 1500;
                case NativeMeridianRoute.Roofs: return tier == 0 ? 9000 : 3000;
                case NativeMeridianRoute.Loading: return tier == 0 ? 24000 : 5000;
                case NativeMeridianRoute.Puff: return tier == 0 ? 160 : 0;
                default: return 0;
            }
        }

        public static void Run(string root)
        {
            count = 0;
            var worlds = HubZFightChecks.Worlds(root);
            int crown = 0, iron = 0;
            foreach (var live in worlds)
            {
                var world = live.World; var city = world.City; var plan = NativeCityLayout.Plans[live.Plan];
                var landmarks = NativeMeridianLandmarks.Place(plan.Seed, city.Map, city.Routes.Select(r => r.Destination).ToArray(), world.HallSlots, 1);
                var route = NativeMeridianRoute.Place(plan.Seed, city.Map, landmarks);
                var again = NativeMeridianRoute.Place(plan.Seed, city.Map, NativeMeridianLandmarks.Place(plan.Seed, city.Map, city.Routes.Select(r => r.Destination).ToArray(), world.HallSlots, 1));
                Check(route.Art.Pieces.Count == again.Art.Pieces.Count && route.Art.Pieces.Zip(again.Art.Pieces, (a, b) => a.Key == b.Key && a.Place.SequenceEqual(b.Place)).All(x => x)
                    && route.Plumes.Select(p => p.At).SequenceEqual(again.Plumes.Select(p => p.At)) && route.Art.Covers.SetEquals(again.Art.Covers), plan.Name + ": the same plan twice");
                var ids = new HashSet<string>(city.Map.blocks.Select(b => b.id), StringComparer.Ordinal);
                Check(route.Art.Covers.All(ids.Contains), plan.Name + ": every covered block is the map's");
                var walkKeys = route.Art.Pieces.Where(p => p.Key.StartsWith("route/647/", StringComparison.Ordinal)).Select(p => p.Key).ToList();
                if (plan.Seed == NativeMeridianRoute.CrownHill)
                {
                    crown++;
                    CrownHill(city.Map, route, walkKeys);
                }
                else
                {
                    Check(walkKeys.Count == 0 && route.Art.Covers.Count == 0 && route.Facades.Count == 0, plan.Name + ": the walk's dressing is Crown Hill's alone");
                }
                // the Ironworks wherever the deal puts it
                var hall = landmarks.Halls.FirstOrDefault(h => h.Slot < world.HallSlots.Length && NativeMeridianLandmarks.Family(world.HallSlots[h.Slot]) == "ironworks");
                if (hall == null)
                {
                    Check(route.Plumes.Count == 0 && route.Art.Pieces.All(p => p.Key != NativeMeridianRoute.Loading) && route.Replaced.Count == 0, plan.Name + ": no Ironworks, no loading bay, no smoke");
                    continue;
                }
                iron++;
                Ironworks(plan.Name, city.Map, landmarks, hall, route);
            }
            Check(crown == 1, "one live deal on Crown Hill");
            // a deal of every plan with the Ironworks: the loading bay and the smoke on each
            foreach (var plan in NativeCityLayout.Plans)
            {
                var walk = new NativeLayeredCityWalk(plan);
                var slots = new[] { "Final court", "Ward Zero", "Central Station", "City Ironworks" };
                var landmarks = NativeMeridianLandmarks.Place(plan.Seed, walk.Map, walk.Routes.Select(r => r.Destination).ToArray(), slots, 1);
                var route = NativeMeridianRoute.Place(plan.Seed, walk.Map, landmarks);
                var hall = landmarks.Halls.First(h => h.Slot == 3);
                Ironworks(plan.Name + " (walk deal)", walk.Map, landmarks, hall, route);
                iron++;
            }
            Smoke();
            Hooks(root);
            Bank();
            Console.WriteLine("PASS meridian route: " + count + " checks; Crown Hill's walk dressed on its live deal, the Ironworks' loading bay and smoke on " + iron + " deals"
                + (Environment.GetEnvironmentVariable("CHIPARENA_ROUTE_BANK") == null ? " (the bank's pieces need CHIPARENA_ROUTE_BANK)" : " (with the route bank)"));
        }

        private static void CrownHill(ContentMap map, NativeMeridianRoute.Plan route, List<string> keys)
        {
            foreach (var k in new[] { NativeMeridianRoute.Rails, NativeMeridianRoute.Crossing, NativeMeridianRoute.Footway, NativeMeridianRoute.Corner, NativeMeridianRoute.Sign, NativeMeridianRoute.Roofs })
                Check(keys.Count(x => x == k) == 1, "Crown Hill places " + k + " once");
            foreach (var p in route.Art.Pieces.Where(p => p.Key.StartsWith("route/647/", StringComparison.Ordinal)))
                Check(p.Place.SequenceEqual(new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0 }) && p.Kind == NativeMeridianLandmarks.Kind.Exit && p.Reads.Length > 0, p.Key + " in the world's frame, with read points");
            // the balustrades replace exactly the Hundred Steps' guards: both sides of the four flights and three landings
            var guards = map.blocks.Where(b => b.id.StartsWith("stair-guard-", StringComparison.Ordinal)).Select(b => b.id).ToList();
            var steps = guards.Where(id => NativeMeridianRoute.StepStems.Any(s => id.StartsWith("stair-guard-" + s + "-", StringComparison.Ordinal))).ToList();
            Check(steps.Count > 0 && route.Art.Covers.SetEquals(steps), "the balustrades replace the Hundred Steps' guards and nothing else");
            foreach (var s in NativeMeridianRoute.StepStems)
                foreach (var side in new[] { "-left-", "-right-" })
                    Check(steps.Any(id => id.StartsWith("stair-guard-" + s + side + "post-", StringComparison.Ordinal)) && steps.Any(id => id.StartsWith("stair-guard-" + s + side + "grip-", StringComparison.Ordinal)),
                        "posts and grips on " + s + side.Trim('-'));
            Check(guards.Except(steps).Any(), "the other flights keep their own guards");
            // the shop: its lot's ground storey, the faces it turns to the junction (east onto Clock Street, north onto the switchback)
            var lot = map.blocks.Single(b => b.id == NativeMeridianRoute.CornerLot);
            float x0 = lot.pos.x - lot.size.x / 2, x1 = lot.pos.x + lot.size.x / 2, z0 = lot.pos.z - lot.size.z / 2, z1 = lot.pos.z + lot.size.z / 2, y0 = lot.pos.y - lot.size.y / 2;
            Check(Math.Abs(y0 - 14f) < .01f && Math.Abs(x1 - (-25f)) < .01f && Math.Abs(z1 - 100f) < .01f, "the corner lot stands on Clock Street at the junction");
            Check(route.Facades.Count == 2, "the shop covers two faces");
            Check(route.Covers(new V3(x1, y0 + 2, (z0 + z1) / 2), new V3(1, 0, 0)) && route.Covers(new V3((x0 + x1) / 2, y0 + 2, z1), new V3(0, 0, 1)), "the shop covers its ground storey's east and north faces");
            Check(!route.Covers(new V3(x1, y0 + NativeMeridianRoute.Storey + 1.5f, (z0 + z1) / 2), new V3(1, 0, 0)), "the upper storey keeps the street dress's windows");
            Check(!route.Covers(new V3(x0, y0 + 2, (z0 + z1) / 2), new V3(-1, 0, 0)) && !route.Covers(new V3((x0 + x1) / 2, y0 + 2, z0), new V3(0, 0, -1)), "the lot's other faces keep their dress");
            var next = map.blocks.Single(b => b.id == "lot-lower-hill-71");
            Check(!route.Covers(new V3(next.pos.x, y0 + 2, next.pos.z + next.size.z / 2), new V3(0, 0, 1)), "the next lot keeps its dress");
            // the keep-outs stand where the route draws: the gate's crossing, Clock Street, the shop and the sign, never over a cache
            Check(route.KeepOut.Count >= 7, "keep-outs for the crossing, the footways, the shop and the sign");
            Check(route.KeepOut.Any(k => k.Core && k.Hits(new V3(x1 - .01f, y0 + 1, z0 + 1), new V3(x1 + .01f, y0 + 2, z0 + 2))), "the shopfront keeps wall art off");
            Check(route.KeepOut.Any(k => k.Hits(new V3(-.1f, 0, 27f), new V3(.1f, .05f, 28f))) && route.KeepOut.Any(k => k.Hits(new V3(-.1f, 0, 34f), new V3(.1f, .05f, 35f))), "the crossing and the footway keep the street life's paint off");
            var sign = map.blocks.Single(b => b.id == NativeMeridianRoute.SignWall);
            float face = sign.pos.z - sign.size.z / 2;
            Check(Math.Abs(face - 117f) < .01f && sign.pos.y + sign.size.y / 2 >= 20.9f, "the sign's wall closes Clock Street at z 117, 7 m over it");
            var signKeep = route.KeepOut.Single(k => k.Core && k.Lo.z < face && k.Hi.z > face - .1f && k.Lo.y > 15f);
            Check(signKeep.Lo.y >= 14f + 2.5f, "the sign hangs high on the wall (its keep-out over the street's reach)");
            // the walk is still the walk: the north route's points the crossing and footways lie on
            var routeN = map.ramps.Single(r => r.id == "n-stair-1");
            Check(Math.Abs(routeN.low.z - 38f) < .01f && Math.Abs(routeN.width - 12f) < .01f, "the first flight starts at z 38, 12 m wide (the footway's end)");
            foreach (var id in NativeMeridianRoute.RoofLots) Check(map.blocks.Any(b => b.id == id), "Clock Street's row " + id);
        }

        private static void Ironworks(string name, ContentMap map, NativeMeridianLandmarks.Layout landmarks, NativeMeridianLandmarks.Hall hall, NativeMeridianRoute.Plan route)
        {
            var loading = route.Art.Pieces.Where(p => p.Key == NativeMeridianRoute.Loading).ToList();
            Check(loading.Count == 1 && loading[0].Slot == hall.Slot && loading[0].Instance == "ironworks" && loading[0].Kind == NativeMeridianLandmarks.Kind.Front, name + ": one loading bay on the Ironworks' hall");
            Check(route.Replaced.Count == 2 && route.Replaced.All(p => p.Key == "front/ironworks/bay" && p.Slot == hall.Slot), name + ": it takes two of the front's bays");
            var us = route.Replaced.Select(p => { var d = p.At - hall.Door; return d.x * hall.Across.x + d.z * hall.Across.z; }).OrderBy(u => u).ToArray();
            float bay = us[1] - us[0];
            Check(Math.Abs(us[0] - NativeMeridianLandmarks.DoorHalf) < .01f && bay > 2f && bay < 3.2f, name + ": the two bays beside the door on its right wing");
            Check(loading[0].Place.SequenceEqual(route.Replaced[0].Place), name + ": the loading bay in the first bay's frame");
            var bays = landmarks.Pieces.Where(p => p.Key == "front/ironworks/bay" && p.Slot == hall.Slot).ToList();
            Check(bays.Count >= 4 && route.Replaced.All(bays.Contains), name + ": the replaced bays are the landmarks' own pieces");
            // the smoke: three plumes over the far stacks, outside the map, over the Cordon
            Check(route.Plumes.Count == NativeMeridianRoute.Stacks.Length, name + ": a plume over each stack");
            var b = map.bounds;
            foreach (var pl in route.Plumes)
                Check((pl.At.x > b.maxX + 20 || pl.At.x < b.minX - 20 || pl.At.z > b.maxZ + 20 || pl.At.z < b.minZ - 20) && pl.At.y > 70f, name + ": the plume at " + pl.At.x.ToString("0") + "," + pl.At.z.ToString("0") + " leaves its stack beyond the map");
        }

        // The puffs: every one in the sky over its stack, swelling then shrinking, the column never all at the same age.
        private static void Smoke()
        {
            var plume = new NativeMeridianRoute.Plume(new V3(400, 90, 100), 3);
            float least = float.MaxValue, most = 0; var ages = new List<float>();
            for (float t = 0; t < 60; t += .25f)
                for (int i = 0; i < NativeMeridianRoute.PuffsPerPlume; i++)
                {
                    NativeMeridianRoute.PuffAt(plume, i, t, out var c, out float r);
                    float age = NativeMeridianRoute.AgeOf(plume, i, t);
                    Check(age >= 0 && age < 1, "a puff's age in its life");
                    Check(r >= 0 && r <= NativeMeridianRoute.PuffRadius * 1.2f, "a puff's radius");
                    Check(c.y >= plume.At.y - .01f && c.y <= plume.At.y + NativeMeridianRoute.PuffRise * 1.15f, "a puff over its stack");
                    var d = c - plume.At; float down = d.x * NativeMeridianRoute.Wind.x + d.z * NativeMeridianRoute.Wind.z;
                    Check(down >= -.01f && down <= NativeMeridianRoute.PuffDrift * 1.25f, "a puff leans down the wind");
                    least = Math.Min(least, r); most = Math.Max(most, r);
                    if (t == 0) ages.Add(age);
                }
            Check(most > NativeMeridianRoute.PuffRadius * .9f && least < .5f, "the puffs swell to full size and shrink away");
            Check(ages.Distinct().Count() == NativeMeridianRoute.PuffsPerPlume && ages.Max() - ages.Min() > .8f, "the column's puffs spread through their life");
        }

        private static void Hooks(string root)
        {
            string Src(string rel) => File.ReadAllText(Path.Combine(root, "Assets/ChipArena", rel)).Replace("\r\n", "\n");
            var show = Src("Runtime/NativeExpeditionWorldPresentation.cs");
            Check(show.Contains("liveRoutePlan=NativeMeridianRouteDraw.HasBank?NativeMeridianRoute.Place(plan.Seed,city.Map,landmarks):null;"), "ShowMeridian places the route with the landmarks it replaces pieces of");
            Check(show.Contains("NativeMeridianDress.FacadeReplaced=(p,n)=>route.Covers(p,n);") && show.Contains("NativeMeridianDress.FacadeReplaced=null;"), "the street dress leaves the shop's ground storey to the route (and lets go after)");
            var lots = Src("Core/NativeMeridianDressLots.cs");
            Check(lots.Contains("if(BayReplaced(at,uh,N,w,fh))continue;"), "a replaced bay draws nothing of the street dress");
            Check(show.Contains("if(liveRoutePlan!=null)foreach(var piece in liveRoutePlan.Replaced)landmarks.Pieces.Remove(piece);"), "the landmarks leave the loading bay's two bays to the route");
            Check(show.Contains("liveRoute.Build(liveRoutePlan,cameraView);") && show.Contains("if(liveRoute!=null){liveRoute.Release();Destroy(liveRoute.gameObject);}"), "the route is drawn and dropped with the run's city");
            var life = Src("Runtime/NativeMeridianStreetLifeLive.cs");
            Check(life.Contains("if(liveRoutePlan!=null)input.KeepOut.AddRange(liveRoutePlan.KeepOut);"), "the street life keeps off the route's pieces");
            var draw = Src("Runtime/NativeMeridianRoute.cs");
            Check(draw.Contains("art.Build(street, camera, NativeMeridianRoute.Bank, false)") && draw.Contains("fronts.Build(front, camera, NativeMeridianRoute.Bank)") && draw.Contains("public static bool HasBank => NativePackBanks.Bank(NativeMeridianRoute.Bank) != null;"), "the route's pieces come from its own bank, through the landmarks' drawer");
            var lm = Src("Runtime/NativeMeridianLandmarks.cs");
            Check(lm.Contains("bankName = bankName ?? NativeMeridianLandmarks.Bank;") && lm.Contains("return NativePackBanks.Mesh(bankName, prop.Parts[0].Mesh);"), "the landmarks' drawer reads the bank it is given");
            Check(!Src("Core/NativeMeridianRoute.cs").Contains("UnityEngine"), "the placement is pure");
        }

        private static void Bank()
        {
            var path = Environment.GetEnvironmentVariable("CHIPARENA_ROUTE_BANK");
            if (string.IsNullOrEmpty(path)) return;
            var bank = NativePackBank.Read(File.ReadAllBytes(path));
            foreach (var key in new[] { NativeMeridianRoute.Rails, NativeMeridianRoute.Crossing, NativeMeridianRoute.Footway, NativeMeridianRoute.Corner, NativeMeridianRoute.Sign,
                NativeMeridianRoute.Roofs, NativeMeridianRoute.Loading, NativeMeridianRoute.Puff })
            {
                var fine = bank.Find(key + "/0");
                Check(fine != null && fine.Parts.Length == 1, "the bank has " + key);
                foreach (var tier in new[] { 0, 2 })
                {
                    var prop = bank.Find(key + "/" + tier);
                    if (prop == null) continue;
                    int tris = bank.Meshes[prop.Parts[0].Mesh].Submeshes.Sum(s => s.Indices.Length) / 3;
                    Check(tris <= Cap(key, tier), key + " tier " + tier + ": " + tris + " triangles, over its cap " + Cap(key, tier));
                }
            }
        }

        /// <summary>`--meridian-route-dump OUT.json`: Crown Hill's blocks (the guards with their turns), ramps and routes, the walk's
        /// placement and the Ironworks' loading frame, for tools/stages/meridian_route.py to build and check on.</summary>
        public static void Dump(string path)
        {
            var w = new StringBuilder();
            string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
            string Vec(V3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
            string Str(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var plan = NativeCityLayout.Plans.Single(p => p.Seed == NativeMeridianRoute.CrownHill);
            var walk = new NativeLayeredCityWalk(plan);
            var map = walk.Map;
            var slots = new[] { "Final court", "City Ironworks", "Ward Zero", "Central Station" };
            var landmarks = NativeMeridianLandmarks.Place(plan.Seed, map, walk.Routes.Select(r => r.Destination).ToArray(), slots, 1);
            var route = NativeMeridianRoute.Place(plan.Seed, map, landmarks);
            var b = map.bounds;
            w.Append("{\"version\":").Append(Str(NativeMeridianRoute.Version)).Append(",\"seed\":").Append(plan.Seed).Append(",\"name\":").Append(Str(plan.Name))
                .Append(",\"bounds\":[").Append(F(b.minX)).Append(',').Append(F(b.maxX)).Append(',').Append(F(b.minZ)).Append(',').Append(F(b.maxZ)).Append(',').Append(F(b.ceiling)).Append(']');
            var treads = new HashSet<string>(map.ramps.SelectMany(r => r.blocks));
            w.Append(",\"blocks\":[");
            for (int i = 0; i < map.blocks.Length; i++)
            {
                var k = map.blocks[i]; if (i > 0) w.Append(',');
                var q = k.rotation ?? new ContentRotation();
                w.Append("[").Append(Str(k.id)).Append(',').Append(F(k.pos.x - k.size.x / 2)).Append(',').Append(F(k.pos.x + k.size.x / 2)).Append(',')
                    .Append(F(k.pos.y - k.size.y / 2)).Append(',').Append(F(k.pos.y + k.size.y / 2)).Append(',')
                    .Append(F(k.pos.z - k.size.z / 2)).Append(',').Append(F(k.pos.z + k.size.z / 2)).Append(',').Append(treads.Contains(k.id) ? "1" : "0").Append(',')
                    .Append(Str(k.role)).Append(",[").Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z)).Append(',').Append(F(q.w)).Append("]]");
            }
            w.Append("],\"ramps\":[");
            for (int i = 0; i < map.ramps.Length; i++)
            {
                var r = map.ramps[i]; if (i > 0) w.Append(',');
                w.Append("{\"id\":").Append(Str(r.id)).Append(",\"kind\":").Append(Str(r.kind)).Append(",\"low\":").Append(Vec(r.low)).Append(",\"high\":").Append(Vec(r.high))
                    .Append(",\"width\":").Append(F(r.width)).Append(",\"bottom\":").Append(F(r.bottom)).Append('}');
            }
            w.Append("],\"pieces\":[");
            var all = route.Art.Pieces;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i]; if (i > 0) w.Append(',');
                w.Append("{\"key\":").Append(Str(p.Key)).Append(",\"kind\":").Append(Str(p.Kind.ToString())).Append(",\"place\":[").Append(string.Join(",", p.Place.Select(F))).Append("],\"reads\":[")
                    .Append(string.Join(",", p.Reads.Select(r => "[" + F(r.At.x) + "," + F(r.At.y) + "," + F(r.At.z) + "," + F(r.Size) + "]"))).Append("]}");
            }
            w.Append("],\"covers\":[").Append(string.Join(",", route.Art.Covers.OrderBy(x => x, StringComparer.Ordinal).Select(Str))).Append(']');
            w.Append(",\"facades\":[").Append(string.Join(",", route.Facades.Select(f => "{\"normal\":" + Vec(f.Normal) + ",\"plane\":" + F(f.Plane) + ",\"lo\":" + F(f.Lo) + ",\"hi\":" + F(f.Hi) + ",\"y0\":" + F(f.Y0) + ",\"y1\":" + F(f.Y1) + "}"))).Append(']');
            w.Append(",\"keepout\":[").Append(string.Join(",", route.KeepOut.Select(k => "{\"lo\":" + Vec(k.Lo) + ",\"hi\":" + Vec(k.Hi) + ",\"core\":" + (k.Core ? "true" : "false") + "}"))).Append(']');
            var hall = landmarks.Halls.First(h => h.Slot == 1);
            var loading = route.Art.Pieces.First(p => p.Key == NativeMeridianRoute.Loading);
            w.Append(",\"loading\":{\"place\":[").Append(string.Join(",", loading.Place.Select(F))).Append("],\"hall\":{\"door\":").Append(Vec(hall.Door)).Append(",\"out\":").Append(Vec(hall.Out))
                .Append(",\"across\":").Append(Vec(hall.Across)).Append(",\"height\":").Append(F(hall.Height)).Append("}}");
            w.Append(",\"bay\":").Append(F(NativeMeridianLandmarks.Bay)).Append(",\"doorHalf\":").Append(F(NativeMeridianLandmarks.DoorHalf)).Append('}');
            File.WriteAllText(path, w.ToString());
            Console.WriteLine("Wrote " + path);
        }
    }
}
