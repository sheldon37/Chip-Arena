using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ChipArena.UnityConversion;

// Meridian's landmarks (Core NativeMeridianLandmarks; plan W10's acceptance "LandmarkReadChecks pass for every landmark from the
// camp, at High and at Low"), on the five approved street layouts and every hall deal:
//  - the 20 exit landmarks: four per layout, one per gate, all different; each reads from its own gate's eye (a clear line to one
//    of its features) at High (1080 lines) and at Low (720), inside its draw reach, never inside a block, and either 3.1 m over the
//    floor beneath it or on a block's face (the art rules: the bank's own check holds the meshes to them);
//  - the final court: City Hall's 6:47 dial reads from the camp, the court's blocks are the ones its model replaces;
//  - the door fronts: each dealt hall wears its instance's front, its bays and returns covering the hall's faces exactly, its
//    airlock leaves inside their wings when open;
//  - the far layer: each dealt instance's landmark and the WMRD mast stand outside the map (out of reach) and read from the camp,
//    their glowing features fogged at most FarFogCap;
//  - determinism: the same layout and deal give the same pieces, a new deal moves only the fronts and the far landmarks.
internal static class LandmarkReadChecks
{
    private const float FovDeg = 60f, HighLines = 1080f, LowLines = 720f, HighPx = 5f, LowPx = 3.5f;
    // the night look's fog (art bible 3.2: linear 10-150 m, 10-100 m on Low) and the walk's own look today (30-175 m)
    private static readonly (string tier, float start, float end, float lines, float px)[] Tiers =
        { ("High", 10, 150, HighLines, HighPx), ("Low", 10, 100, LowLines, LowPx), ("walk", 30, 175, HighLines, HighPx) };

    public static void Run()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var shapes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in NativeMeridianLandmarks.ExitTable)
        {
            if (!keys.Add(e.Key)) throw new Exception("Exit landmark key repeated: " + e.Key);
            if (!shapes.Add(e.Shape)) throw new Exception("Two exit landmarks share a shape: " + e.Shape);
        }
        if (keys.Count != 20) throw new Exception("Expected 20 exit landmarks, found " + keys.Count);
        var deals = Deals();
        int views = 0;
        foreach (var plan in NativeCityLayout.Plans)
        {
            var walk = new NativeLayeredCityWalk(plan);
            var map = walk.Map;
            var doors = walk.Routes.Select(r => r.Destination).ToArray();
            var solids = Solids(map);
            var exits = NativeMeridianLandmarks.ExitTable.Where(e => e.Plan == plan.Seed).ToArray();
            if (exits.Length != 4 || exits.Select(e => e.Gate).Distinct().Count() != 4)
                throw new Exception(plan.Name + ": expected one exit landmark per gate");
            string first = null;
            foreach (var deal in deals)
            {
                var a = NativeMeridianLandmarks.Place(plan.Seed, map, doors, deal);
                var b = NativeMeridianLandmarks.Place(plan.Seed, map, doors, deal);
                string sa = Signature(a), sb = Signature(b);
                if (sa != sb) throw new Exception(plan.Name + ": placement is not deterministic");
                string fixedPart = Signature(a, fixedOnly: true);
                if (first == null) first = fixedPart;
                else if (first != fixedPart) throw new Exception(plan.Name + ": a new deal moved a fixed landmark");
                foreach (var c in a.Covers)
                    if (!map.blocks.Any(x => x.id == c)) throw new Exception(plan.Name + ": covers a missing block " + c);
                Exits(plan, a, solids, map);
                Court(plan, a, solids, doors);
                Fronts(plan, a, deal);
                Far(plan, a, solids, map);
                views++;
            }
        }
        Console.WriteLine("PASS landmark reads: 20 exit landmarks, the final court, fronts and far layer on 5 layouts x " + deals.Count + " deals (" + views + " placements)");
    }

    // Every order of the walk's three instances, and deals with Level 0 and the Poolrooms (the bleed's picker deals any three).
    private static List<string[]> Deals()
    {
        var deals = new List<string[]>();
        foreach (uint seed in new uint[] { 1, 2, 3, 5, 8, 13, 21, 34 })
            deals.Add(NativeCityLayout.DealHalls(seed, new[] { "Ward Zero", "Central Station", "City Ironworks" }));
        deals.Add(new[] { "Final court", "Level 0", "The Poolrooms", "Ward Zero" });
        deals.Add(new[] { "Final court", "The Poolrooms", "City Ironworks", "Level 0" });
        deals.Add(new[] { "Final court", "Central Station", "Level 0", "The Poolrooms" });
        return deals;
    }

    private static string Signature(NativeMeridianLandmarks.Layout l, bool fixedOnly = false)
    {
        var sb = new StringBuilder();
        foreach (var p in l.Pieces)
        {
            if (fixedOnly && (p.Kind == NativeMeridianLandmarks.Kind.Front || p.Kind == NativeMeridianLandmarks.Kind.FrontRig || p.Kind == NativeMeridianLandmarks.Kind.Far)) continue;
            sb.Append(p.Key).Append(':');
            foreach (var f in p.Place) sb.Append(f.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(';');
        }
        if (!fixedOnly) foreach (var c in l.Covers.OrderBy(x => x, StringComparer.Ordinal)) sb.Append(c).Append('|');
        return sb.ToString();
    }

    private sealed class Box { public string Id; public float X0, X1, Y0, Y1, Z0, Z1; }
    private static List<Box> Solids(ContentMap map) => map.blocks.Select(b => new Box { Id = b.id,
        X0 = b.pos.x - b.size.x / 2, X1 = b.pos.x + b.size.x / 2, Y0 = b.pos.y - b.size.y / 2, Y1 = b.pos.y + b.size.y / 2,
        Z0 = b.pos.z - b.size.z / 2, Z1 = b.pos.z + b.size.z / 2 }).ToList();

    // A clear line from the eye to the point (pulled back toward the eye by `pull` so the face it lies on does not count).
    private static string Blocked(List<Box> boxes, V3 eye, V3 point, float pull = .4f)
    {
        var d = point - eye; float len = d.Length; var end = point - d / len * pull;
        foreach (var b in boxes)
        {
            float t0 = 0, t1 = 1; bool hit = true;
            for (int ax = 0; ax < 3 && hit; ax++)
            {
                float a = ax == 0 ? eye.x : ax == 1 ? eye.y : eye.z, e = ax == 0 ? end.x : ax == 1 ? end.y : end.z;
                float lo = (ax == 0 ? b.X0 : ax == 1 ? b.Y0 : b.Z0) + 1e-3f, hi = (ax == 0 ? b.X1 : ax == 1 ? b.Y1 : b.Z1) - 1e-3f;
                float dd = e - a;
                if (Math.Abs(dd) < 1e-9f) { if (a <= lo || a >= hi) hit = false; continue; }
                float u0 = (lo - a) / dd, u1 = (hi - a) / dd; if (u0 > u1) (u0, u1) = (u1, u0);
                t0 = Math.Max(t0, u0); t1 = Math.Min(t1, u1); if (t0 > t1) hit = false;
            }
            if (hit && t1 > 1e-3f && t0 < 1 - 1e-3f) return b.Id;
        }
        return null;
    }
    private static bool Inside(List<Box> boxes, V3 p) => boxes.Any(b => p.x > b.X0 + .01f && p.x < b.X1 - .01f && p.y > b.Y0 + .01f && p.y < b.Y1 - .01f && p.z > b.Z0 + .01f && p.z < b.Z1 - .01f);
    private static float Px(float size, float distance, float lines) => (float)(size / distance / (FovDeg * Math.PI / 180) * lines);
    private static float Fog(float d, float start, float end) => Math.Max(0, Math.Min(1, (d - start) / (end - start)));

    private static void Exits(NativeCityLayout.Plan plan, NativeMeridianLandmarks.Layout l, List<Box> solids, ContentMap map)
    {
        foreach (var p in l.Pieces.Where(x => x.Kind == NativeMeridianLandmarks.Kind.Exit))
        {
            var eye = NativeMeridianLandmarks.GateEye(p.Gate);
            bool read = false; string why = null;
            foreach (var r in p.Reads)
            {
                if (Inside(solids, r.At)) throw new Exception(p.Key + ": a read point lies inside a block");
                if (!Honest(solids, r.At)) throw new Exception(p.Key + ": a read point stands free under 3.1 m and off every block face");
                float d = (r.At - eye).Length;
                if (d > NativeMeridianLandmarks.ExitReach) throw new Exception(p.Key + ": beyond the exits' draw reach");
                var by = Blocked(solids, eye, r.At);
                if (by != null) { why = "hidden by " + by; continue; }
                foreach (var t in Tiers)
                {
                    if (Px(r.Size, d, t.lines) < t.px) { why = t.tier + " too small"; goto next; }
                    if (r.Glow && NativeMeridianLandmarks.FogOf(d, t.start, t.end) > NativeMeridianLandmarks.FarFogCap + 1e-4f) { why = t.tier + " fogged"; goto next; }
                }
                read = true;
                next:;
            }
            if (!read) throw new Exception(plan.Name + " " + p.Key + " does not read from its gate: " + why);
        }
    }

    // A point is honest if it hangs 3.1 m or more over the highest top beneath it, or lies within 1.2 m of a block's face.
    private static bool Honest(List<Box> boxes, V3 p)
    {
        float floor = float.MinValue;
        foreach (var b in boxes)
            if (p.x >= b.X0 - .5f && p.x <= b.X1 + .5f && p.z >= b.Z0 - .5f && p.z <= b.Z1 + .5f && b.Y1 <= p.y + .01f) floor = Math.Max(floor, b.Y1);
        if (p.y - floor >= 3.1f) return true;
        foreach (var b in boxes)
        {
            float dx = Math.Max(Math.Max(b.X0 - p.x, 0), p.x - b.X1), dy = Math.Max(Math.Max(b.Y0 - p.y, 0), p.y - b.Y1), dz = Math.Max(Math.Max(b.Z0 - p.z, 0), p.z - b.Z1);
            if (dx * dx + dy * dy + dz * dz <= 1.2f * 1.2f) return true;
        }
        return false;
    }

    private static readonly V3[] CampEyes = { NativeMeridianLandmarks.GateEye(0), NativeMeridianLandmarks.GateEye(1), NativeMeridianLandmarks.GateEye(2),
        NativeMeridianLandmarks.GateEye(3), new V3(0, NativeMeridianLandmarks.EyeHeight, 0) };

    private static bool ReadsFromCamp(List<Box> solids, NativeMeridianLandmarks.Read r, out string why)
    {
        why = "hidden";
        foreach (var eye in CampEyes)
        {
            if (Blocked(solids, eye, r.At, .6f) != null) continue;
            float d = (r.At - eye).Length;
            bool ok = true;
            foreach (var t in Tiers)
            {
                if (Px(r.Size, d, t.lines) < t.px) { ok = false; why = t.tier + " too small at " + d.ToString("0") + " m"; }
                if (r.Glow && NativeMeridianLandmarks.FogOf(d, t.start, t.end) > NativeMeridianLandmarks.FarFogCap + 1e-4f) { ok = false; why = t.tier + " fogged"; }
            }
            if (d > NativeMeridianLandmarks.FarReach) { ok = false; why = "beyond the far reach"; }
            if (ok) return true;
        }
        return false;
    }

    private static void Court(NativeCityLayout.Plan plan, NativeMeridianLandmarks.Layout l, List<Box> solids, V3[] doors)
    {
        var court = l.Pieces.SingleOrDefault(p => p.Kind == NativeMeridianLandmarks.Kind.Court) ?? throw new Exception(plan.Name + ": no final court");
        if (!ReadsFromCamp(solids, court.Reads[0], out var why)) throw new Exception(plan.Name + ": City Hall's dial does not read from the camp: " + why);
        foreach (var id in NativeMeridianLandmarks.CourtBlocks)
            if (!l.Covers.Contains(id)) throw new Exception(plan.Name + ": the court's model does not replace " + id);
        // the tower's dial sits on the tower block's south face, 47 m over the court
        var tower = solids.Single(b => b.Id == "city-hall-tower");
        var dial = court.Reads[0].At;
        if (Math.Abs(dial.z - (tower.Z0 - .2f)) > .3f || dial.y - doors[0].y != 47f) throw new Exception(plan.Name + ": the dial is off the tower's face");
        if (tower.Y1 - doors[0].y != 62f) throw new Exception(plan.Name + ": the tower is not 62 m over its court");
    }

    private static void Fronts(NativeCityLayout.Plan plan, NativeMeridianLandmarks.Layout l, string[] deal)
    {
        if (l.Halls.Count != 3) throw new Exception(plan.Name + ": expected three halls");
        foreach (var h in l.Halls)
        {
            string fam = NativeMeridianLandmarks.Family(deal[h.Slot]);
            if (fam == null) throw new Exception("Unknown instance " + deal[h.Slot]);
            var pieces = l.Pieces.Where(p => p.Slot == h.Slot && (p.Kind == NativeMeridianLandmarks.Kind.Front || p.Kind == NativeMeridianLandmarks.Kind.FrontRig)).ToList();
            if (pieces.Any(p => p.Instance != fam)) throw new Exception(plan.Name + " " + h.Id + ": a front piece of the wrong instance");
            if (pieces.Count(p => p.Key.EndsWith("/door", StringComparison.Ordinal)) != 1) throw new Exception(plan.Name + " " + h.Id + ": expected one door piece");
            // the bays span the face: two wings of (width/2 - DoorHalf) each
            float bays = pieces.Where(p => p.Key.EndsWith("/bay", StringComparison.Ordinal)).Sum(p => Scale(p.Place, 0) * NativeMeridianLandmarks.Bay);
            if (Math.Abs(bays + 2 * NativeMeridianLandmarks.DoorHalf - h.Width) > .01f) throw new Exception(plan.Name + " " + h.Id + ": the bays do not span the face");
            float sides = pieces.Where(p => p.Key.EndsWith("/side", StringComparison.Ordinal)).Sum(p => Scale(p.Place, 0) * NativeMeridianLandmarks.Bay);
            if (Math.Abs(sides - 2 * h.Depth) > .01f) throw new Exception(plan.Name + " " + h.Id + ": the returns do not span the sides");
            float back = pieces.Where(p => p.Key.EndsWith("/back", StringComparison.Ordinal)).Sum(p => Scale(p.Place, 0) * NativeMeridianLandmarks.Bay);
            if (Math.Abs(back + 2 * NativeMeridianLandmarks.DoorHalf - h.Width) > .01f || pieces.Count(p => p.Key.EndsWith("/rear", StringComparison.Ordinal)) != 1)
                throw new Exception(plan.Name + " " + h.Id + ": the back does not span the hall");
            float lining = pieces.Where(p => p.Key.EndsWith("/lining", StringComparison.Ordinal)).Sum(p => Scale(p.Place, 2) * NativeMeridianLandmarks.Lining);
            if (Math.Abs(lining - h.Depth) > .01f) throw new Exception(plan.Name + " " + h.Id + ": the lining does not run through the hall");
            if (h.Height < NativeMeridianLandmarks.MouthHeight + 6.9f || h.Height > NativeMeridianLandmarks.MouthHeight + 7.1f) throw new Exception(plan.Name + " " + h.Id + ": not a 16 m hall");
            // open, each leaf (half the mouth wide) lies wholly behind its wing's face
            if (NativeMeridianLandmarks.LeafSlide(1) < NativeMeridianLandmarks.MouthHalf) throw new Exception("An open leaf still shows in the mouth");
            if (NativeMeridianLandmarks.LeafSlide(1) + NativeMeridianLandmarks.MouthHalf > h.Width / 2)
                throw new Exception(plan.Name + " " + h.Id + ": an open leaf runs out of its wing");
            foreach (var c in h.Blocks) if (!l.Covers.Contains(c)) throw new Exception(plan.Name + ": the front leaves " + c + " uncovered");
        }
    }
    private static float Scale(float[] m, int col) => (float)Math.Sqrt(m[col] * m[col] + m[4 + col] * m[4 + col] + m[8 + col] * m[8 + col]);

    private static void Far(NativeCityLayout.Plan plan, NativeMeridianLandmarks.Layout l, List<Box> solids, ContentMap map)
    {
        var b = map.bounds;
        foreach (var p in l.Pieces.Where(x => x.Kind == NativeMeridianLandmarks.Kind.Far || x.Kind == NativeMeridianLandmarks.Kind.Mast))
        {
            float r = NativeMeridianLandmarks.FarRadius(p.Kind == NativeMeridianLandmarks.Kind.Mast ? "mast" : p.Instance);
            var at = p.At;
            bool outside = at.x - r > b.maxX || at.x + r < b.minX || at.z - r > b.maxZ || at.z + r < b.minZ;
            if (!outside) throw new Exception(plan.Name + " " + p.Key + ": the far landmark stands inside the map");
            bool read = false; string why = "no read";
            foreach (var rd in p.Reads) if (ReadsFromCamp(solids, rd, out why)) { read = true; break; }
            if (!read) throw new Exception(plan.Name + " " + p.Key + " does not read from the camp: " + why);
        }
        if (l.Pieces.Count(x => x.Kind == NativeMeridianLandmarks.Kind.Far) != 3) throw new Exception(plan.Name + ": expected three far landmarks");
    }

    /// <summary>`--meridian-landmarks-dump OUT.json`: the five layouts' blocks, ramps, routes, halls and placements, for the bank
    /// script (tools/stages/meridian_landmarks.py) to build on and check against.</summary>
    public static void Dump(string path)
    {
        var w = new StringBuilder();
        string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        string Vec(V3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
        string Str(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        w.Append("{\"version\":").Append(Str(NativeMeridianLandmarks.Version)).Append(",\"layouts\":[");
        bool firstPlan = true;
        foreach (var plan in NativeCityLayout.Plans)
        {
            var walk = new NativeLayeredCityWalk(plan);
            var map = walk.Map; var doors = walk.Routes.Select(r => r.Destination).ToArray();
            if (!firstPlan) w.Append(','); firstPlan = false;
            var b = map.bounds;
            w.Append("{\"seed\":").Append(plan.Seed).Append(",\"name\":").Append(Str(plan.Name))
                .Append(",\"bounds\":[").Append(F(b.minX)).Append(',').Append(F(b.maxX)).Append(',').Append(F(b.minZ)).Append(',').Append(F(b.maxZ)).Append(',').Append(F(b.ceiling)).Append(',').Append(F(b.killY)).Append(']');
            var treads = new HashSet<string>(map.ramps.SelectMany(r => r.blocks));
            w.Append(",\"blocks\":[");
            for (int i = 0; i < map.blocks.Length; i++)
            {
                var k = map.blocks[i]; if (i > 0) w.Append(',');
                w.Append("[").Append(Str(k.id)).Append(',').Append(F(k.pos.x - k.size.x / 2)).Append(',').Append(F(k.pos.x + k.size.x / 2)).Append(',')
                    .Append(F(k.pos.y - k.size.y / 2)).Append(',').Append(F(k.pos.y + k.size.y / 2)).Append(',')
                    .Append(F(k.pos.z - k.size.z / 2)).Append(',').Append(F(k.pos.z + k.size.z / 2)).Append(',').Append(treads.Contains(k.id) ? "1" : "0").Append(']');
            }
            w.Append("],\"ramps\":[");
            for (int i = 0; i < map.ramps.Length; i++)
            {
                var r = map.ramps[i]; if (i > 0) w.Append(',');
                w.Append("{\"id\":").Append(Str(r.id)).Append(",\"kind\":").Append(Str(r.kind)).Append(",\"low\":").Append(Vec(r.low)).Append(",\"high\":").Append(Vec(r.high)).Append(",\"width\":").Append(F(r.width)).Append('}');
            }
            w.Append("],\"routes\":[");
            for (int i = 0; i < walk.Routes.Count; i++)
            {
                var r = walk.Routes[i]; if (i > 0) w.Append(',');
                w.Append("{\"name\":").Append(Str(r.Name)).Append(",\"to\":").Append(Vec(r.Destination)).Append(",\"points\":[").Append(string.Join(",", r.Points.Select(Vec))).Append("]}");
            }
            var all = NativeMeridianLandmarks.Place(plan.Seed, map, doors, new[] { "Final court", "Ward Zero", "Central Station", "City Ironworks" });
            w.Append("],\"halls\":[");
            for (int i = 0; i < all.Halls.Count; i++)
            {
                var h = all.Halls[i]; if (i > 0) w.Append(',');
                w.Append("{\"id\":").Append(Str(h.Id)).Append(",\"slot\":").Append(h.Slot).Append(",\"door\":").Append(Vec(h.Door)).Append(",\"out\":").Append(Vec(h.Out))
                    .Append(",\"across\":").Append(Vec(h.Across)).Append(",\"width\":").Append(F(h.Width)).Append(",\"depth\":").Append(F(h.Depth)).Append(",\"height\":").Append(F(h.Height)).Append('}');
            }
            w.Append("],\"pieces\":[");
            for (int i = 0; i < all.Pieces.Count; i++)
            {
                var p = all.Pieces[i]; if (i > 0) w.Append(',');
                w.Append("{\"key\":").Append(Str(p.Key)).Append(",\"kind\":").Append(Str(p.Kind.ToString())).Append(",\"gate\":").Append(p.Gate).Append(",\"slot\":").Append(p.Slot)
                    .Append(",\"place\":[").Append(string.Join(",", p.Place.Select(F))).Append("],\"reads\":[")
                    .Append(string.Join(",", p.Reads.Select(r => "[" + F(r.At.x) + "," + F(r.At.y) + "," + F(r.At.z) + "," + F(r.Size) + "," + (r.Glow ? "1" : "0") + "]"))).Append("]}");
            }
            w.Append("],\"covers\":[").Append(string.Join(",", all.Covers.OrderBy(x => x, StringComparer.Ordinal).Select(Str))).Append("]}");
        }
        w.Append("],\"exits\":[");
        bool fe = true;
        foreach (var e in NativeMeridianLandmarks.ExitTable)
        {
            if (!fe) w.Append(','); fe = false;
            w.Append("{\"plan\":").Append(e.Plan).Append(",\"gate\":").Append(e.Gate).Append(",\"key\":").Append(Str(e.Key)).Append(",\"name\":").Append(Str(e.Name)).Append(",\"shape\":").Append(Str(e.Shape)).Append('}');
        }
        w.Append("]}");
        File.WriteAllText(path, w.ToString());
        Console.WriteLine("Wrote " + path);
    }
}
