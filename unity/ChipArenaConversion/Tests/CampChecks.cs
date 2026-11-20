using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ChipArena.UnityConversion;

// Belvedere Camp (Core NativeCamp, NativeCampFx, NativeCampSynth, NativeCampMeshes; Runtime NativeCampDraw, NativeCampAudio,
// NativeCampWalk; plan W5 and Q19; docs/expedition/BELVEDERE-CAMP.md):
//  - the hub plan table (A1's blocks): inside the fence, clear of every gate's volumes, leaf travel, pockets and track, of the 4 m
//    lanes and the mess ring's walkway, the fence-vault rule, and the same rows A0's landing field already keeps clear of;
//  - the landing field against the table over 2,000 seeds (hull, skid apron), and the capsule at ExitReach 2.4 m reported for A1;
//  - the survivors' posts: each inside its structure, behind glass, mesh or a hatch, or seated with less than 1.7 m over the figure;
//  - part 1, the live camp: the same on every peer for a seed; every piece below the gates' track within 0.25 m of the fence where
//    the fence stands and never in a leaf pocket; nothing where an A1 block will stand; strings and sails from 3.5 m and clear of
//    every A1 block; the bulbs, lights and sounds;
//  - the effects: nothing flashes (the fires breathe under 2.4 Hz, under 0.7 Hz with reduced effects), the slots' budget, no
//    allocation per frame; the board lamp's 4 m;
//  - the placeholder sounds: original, finite, loops that close;
//  - the runtime: the hook lines, the preview only in Editor and development builds and only in the Developer walk, no collider,
//    physics or wire in the camp's files;
//  - with CHIPARENA_CAMP_BANK (the bank built by tools/stages/camp_bank.py): every key there, every mesh inside its envelope (the art
//    rules on the real triangles), and the triangle budget.
internal static class CampChecks
{
    private static int checks;
    private static void Check(bool ok, string what) { checks++; if (!ok) throw new Exception("Camp: " + what); }

    public static void Run(string root)
    {
        checks = 0;
        Table(); Field(); Posts(); Live(); Fx(); Synth(); Sources(root); Bank();
        Console.WriteLine("PASS camp: " + checks + " checks (the W5 table and its clearances, 2,000 landing fields, " + NativeCamp.Posts.Length +
            " posts, the live camp's honesty, its light, sound and budgets" + (Environment.GetEnvironmentVariable("CHIPARENA_CAMP_BANK") != null ? ", the bank's meshes" : "") + ")");
    }

    // ---------------- the table ----------------
    private static bool Overlap(V3 lo, V3 hi, V3 lo2, V3 hi2, float pad = 0) =>
        lo.x < hi2.x + pad && hi.x > lo2.x - pad && lo.y < hi2.y + pad && hi.y > lo2.y - pad && lo.z < hi2.z + pad && hi.z > lo2.z - pad;
    private static bool Ring(NativeCamp.Row r) => r.Kind == NativeCamp.RowKind.Fountain || r.Kind == NativeCamp.RowKind.Canopy || r.Kind == NativeCamp.RowKind.CanopyPost || r.Kind == NativeCamp.RowKind.Bench;
    private static readonly (float x, float z)[] Gates = { (0, 25), (25, 0), (0, -25), (-25, 0) };
    private static void Table()
    {
        Check(NativeCamp.Table.Select(r => r.Id).Distinct().Count() == NativeCamp.Table.Length, "row ids are unique");
        Check(NativeCamp.Table.All(r => r.Id.StartsWith("camp-", StringComparison.Ordinal)), "every row is camp-* (NativeMeridianDress leaves camp-* to the hub)");
        Check(NativeMeridianDress.RoleOf("camp-fountain", null) == NativeMeridianDress.RCamp && !NativeMeridianDress.Dressed(NativeMeridianDress.RCamp), "the street dress leaves the camp's blocks alone");
        foreach (var r in NativeCamp.Table)
        {
            V3 lo = r.Min, hi = r.Max;
            Check(lo.x >= -NativeCamp.Inner - 1e-4f && hi.x <= NativeCamp.Inner + 1e-4f && lo.z >= -NativeCamp.Inner - 1e-4f && hi.z <= NativeCamp.Inner + 1e-4f, r.Id + " inside the fence");
            foreach (var (gx, gz) in Gates)
                foreach (float size in new[] { 10f, 12f })
                    Check(!(lo.x < gx + size / 2 && hi.x > gx - size / 2 && lo.z < gz + size / 2 && hi.z > gz - size / 2), r.Id + " clear of a gate's " + (size < 11 ? "interact" : "sensor") + " volume");
            Check(!NativeCamp.InPocket(lo, hi), r.Id + " clear of every gate's leaf travel, pocket and track");
            for (int s = 0; s < 4; s++)
            {
                var a = NativeCamp.OnFence(s, -2, 0, 0); var b = NativeCamp.OnFence(s, 2, 4, 9 - NativeCamp.Inner);
                Check(!Overlap(lo, hi, Min(a, b), Max(a, b)), r.Id + " clear of the 4 m lane " + "NESW"[s]);
            }
            if (!Ring(r)) Check(!Overlap(lo, hi, new V3(-7.1f, 0, -7.1f), new V3(7.1f, 4, 7.1f)), r.Id + " clear of the 2.5 m walkway round the ring");
            bool nearFence = lo.x < -21.8f || hi.x > 21.8f || lo.z < -21.8f || hi.z > 21.8f;
            if (nearFence && r.Y0 < .01f) Check(r.Top <= 1.6f + 1e-4f || r.Top >= NativeCamp.FenceTop - 1e-4f, r.Id + " keeps the fence vault (1.6 m or less, or the fence's 3.2 m or more, within 3 m of it)");
        }
        // the ring's benches: a seated figure with 1.2 m under the canopy, so nobody stands on them
        var canopy = NativeCamp.Table.Where(r => r.Kind == NativeCamp.RowKind.Canopy).Min(r => r.Y0);
        foreach (var b in NativeCamp.Table.Where(r => r.Kind == NativeCamp.RowKind.Bench)) Check(canopy - b.Top < NativeCamp.Headroom, b.Id + " leaves less than 1.7 m under the canopy");
        // A0's landing field keeps clear of the same rows (Core/NativeExpeditionHubPlan.cs Reservations): every block's footprint lies
        // inside one of its reservations, so the spots never move when A1 builds them
        var field = typeof(NativeExpeditionHubPlan).GetField("Reservations", BindingFlags.NonPublic | BindingFlags.Static);
        Check(field != null, "A0's reservations are readable");
        var rows = ((System.Collections.IEnumerable)field.GetValue(null)).Cast<object>().Select(o =>
        {
            var t = o.GetType(); var c = (System.Numerics.Vector3)t.GetField("Item1").GetValue(o);
            return (c, w: (float)t.GetField("Item2").GetValue(o), d: (float)t.GetField("Item3").GetValue(o), yaw: (float)t.GetField("Item4").GetValue(o));
        }).ToList();
        foreach (var r in NativeCamp.Table)
            Check(rows.Any(q => Math.Abs(q.yaw) < 1e-4f && r.Min.x >= q.c.X - q.w / 2 - .011f && r.Max.x <= q.c.X + q.w / 2 + .011f && r.Min.z >= q.c.Z - q.d / 2 - .011f && r.Max.z <= q.c.Z + q.d / 2 + .011f),
                r.Id + " lies inside one of A0's landing-field reservations");
        // the camp's machines: in the corners, facing the fountain, as A0 reserves them
        foreach (var m in NativeCampOverlay.Machines())
        {
            Check(Math.Abs(Math.Abs(m.x) - 13.5f) < 1e-4f && Math.Abs(m.x - m.z) < 1e-4f, m.id + " in its corner");
            var toCentre = new V3(-m.x, 0, -m.z).Normalized; Check(V3.Dot(toCentre, new V3((float)Math.Sin(m.yaw), 0, (float)Math.Cos(m.yaw))) > .999f, m.id + " faces the fountain");
            foreach (var r in NativeCamp.Table) Check(!Overlap(new V3(m.x - .9f, 0, m.z - .9f), new V3(m.x + .9f, 2.2f, m.z + .9f), r.Min, r.Max), m.id + " clear of " + r.Id);
        }
    }
    private static V3 Min(V3 a, V3 b) => new V3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
    private static V3 Max(V3 a, V3 b) => new V3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
    private static float Gap2(float x, float z, NativeCamp.Row r) { float dx = Math.Max(0, Math.Max(r.Min.x - x, x - r.Max.x)), dz = Math.Max(0, Math.Max(r.Min.z - z, z - r.Max.z)); return (float)Math.Sqrt(dx * dx + dz * dz); }

    // ---------------- the landing field ----------------
    private static void Field()
    {
        float worstHull = float.MaxValue, worstSkid = float.MaxValue, worstCapsule = float.MaxValue; int capsuleShort = 0;
        for (uint i = 0; i < 2000; i++)
        {
            uint seed = unchecked(i * 2654435761u); var spots = NativeExpeditionHubPlan.LandingField(seed);
            for (int k = 0; k < 4; k++)
            {
                var p = spots[k]; double a = p.yaw * Math.PI / 180; float fx = (float)Math.Sin(a), fz = (float)Math.Cos(a);
                foreach (var r in NativeCamp.Table)
                {
                    if (r.Y0 > 3) continue; // the lookout's cabin and light stand over its base
                    worstHull = Math.Min(worstHull, Gap2(p.x, p.z, r) - NativeExpeditionHubPlan.HullRadius);
                    for (float t = 0; t <= 1.0001f; t += .125f) worstSkid = Math.Min(worstSkid, Gap2(p.x - fx * NativeExpeditionHubPlan.SkidLength * t, p.z - fz * NativeExpeditionHubPlan.SkidLength * t, r) - NativeExpeditionHubPlan.HullRadius);
                    float cap = Gap2(p.x + fx * 2.4f, p.z + fz * 2.4f, r) - NativeLocomotionRules.Radius; worstCapsule = Math.Min(worstCapsule, cap);
                    if (cap < 1) capsuleShort++;
                }
            }
        }
        Check(worstHull >= 1 - 1e-3f, "every pod hull 1 m clear of every block (worst " + worstHull.ToString("0.00", CultureInfo.InvariantCulture) + " m)");
        Check(worstSkid >= 1 - 1e-3f, "every skid apron 1 m clear of every block (worst " + worstSkid.ToString("0.00", CultureInfo.InvariantCulture) + " m)");
        // (for A1: today's Clear() does not test the capsule at ExitReach 2.4 m; A1 adds it, which may move some draws)
        Console.WriteLine("camp: landing field over 2000 seeds: hull clear by " + worstHull.ToString("0.00", CultureInfo.InvariantCulture) + " m, skid by " + worstSkid.ToString("0.00", CultureInfo.InvariantCulture) +
            " m; the ExitReach 2.4 capsule clear by " + worstCapsule.ToString("0.00", CultureInfo.InvariantCulture) + " m (" + capsuleShort + " block pairs under 1 m)");
    }

    // ---------------- the posts ----------------
    private static void Posts()
    {
        Check(NativeCamp.Posts.Select(p => p.Id).Distinct().Count() == NativeCamp.Posts.Length, "post ids unique");
        Check(NativeCamp.Posts.Count(p => p.Rescued == null) == 13, "13 survivors in the camp from the start (plan R6)");
        var rescued = NativeCamp.Posts.Where(p => p.Rescued != null).Select(p => p.Rescued).ToArray();
        Check(rescued.Length == 5 && rescued.Distinct().Count() == 5 && rescued.All(r => NativeCampDraw_Family(r) != null), "one rescued survivor per instance kind");
        foreach (var p in NativeCamp.Posts)
        {
            Check(!p.Role.Contains("conductor") && !p.Role.Contains("lifeguard"), p.Id + " is not named for an enemy kind");
            if (p.Cover == NativeCamp.Cover.Machine) { Check(NativeReviveMachine.Spot(NativeExpeditionGeneratedWorld.MapId, out _, out _), "Sparks's machine stands in the hub"); continue; }
            var r = NativeCamp.RowOf(p.Structure); float h = NativeCamp.PoseHeight(p.Pose);
            Check(p.At.x >= r.Min.x + .18f && p.At.x <= r.Max.x - .18f && p.At.z >= r.Min.z + .18f && p.At.z <= r.Max.z - .18f, p.Id + " stands inside " + r.Id);
            Check(Math.Abs(p.At.y - r.Y0) < 1e-4f || p.At.y >= r.Y0, p.Id + " on its structure's floor");
            if (p.Cover == NativeCamp.Cover.SeatedRoof)
            {
                var canopy = NativeCamp.Table.Where(x => x.Kind == NativeCamp.RowKind.Canopy).Min(x => x.Y0);
                Check(p.Pose.StartsWith("seated", StringComparison.Ordinal) && r.Kind == NativeCamp.RowKind.Bench && h <= r.Top + .001f && canopy - r.Top < NativeCamp.Headroom, p.Id + " seated inside its bench, under the canopy");
            }
            else Check(p.At.y + h <= r.Top + 1e-3f, p.Id + " inside its structure (behind glass, mesh or a hatch)");
        }
    }
    private static string NativeCampDraw_Family(string instance)
    {
        switch (instance) { case "exp-backrooms": return "level0"; case "exp-poolrooms": return "poolrooms"; case "exp-ward-zero": return "wardzero"; case "exp-transit": return "station"; case "exp-foundry": return "ironworks"; }
        return null;
    }

    // ---------------- part 1 ----------------
    private static void Live()
    {
        var a = NativeCamp.Live(1964354793u); var b = NativeCamp.Live(1964354793u); var c = NativeCamp.Live(4284432158u);
        Check(Sig(a) == Sig(b), "the live camp is the same on every peer for a seed");
        Check(PieceSig(a) == PieceSig(c), "a new run moves no piece (only its bulbs, fires and sails' colours vary)");
        foreach (var p in a.Pieces)
        {
            p.Bounds(out var lo, out var hi);
            Check(lo.x >= -NativeCamp.Outer - .25f && hi.x <= NativeCamp.Outer + .25f && lo.z >= -NativeCamp.Outer - .25f && hi.z <= NativeCamp.Outer + .25f, p.Key + " inside the camp");
            foreach (var r in NativeCamp.Table) Check(!Overlap(lo, hi, r.Min, r.Max), p.Key + " at " + Fmt(p.Light) + " stands where " + r.Id + " will (A1)");
            if (!p.HasBase) { Check(lo.y >= NativeCamp.PocketTop - 1e-3f, p.Key + " has no base, so it stands above the gates' track"); continue; }
            p.BaseBounds(out var blo, out var bhi);
            Check(!NativeCamp.InPocket(blo, bhi), p.Key + " clear of the gates' leaf pockets and track");
            // within 0.25 m of a fence face where the fence stands (not in a gate's opening)
            float best = float.MaxValue;
            for (int s = 0; s < 4; s++)
            {
                var n = NativeCamp.Normal(s); var rr = NativeCamp.Right(s);
                float nmin = Math.Min(V3.Dot(blo, n), V3.Dot(bhi, n)), a0 = Math.Min(V3.Dot(blo, rr), V3.Dot(bhi, rr)), a1 = Math.Max(V3.Dot(blo, rr), V3.Dot(bhi, rr));
                // corners: the box's far point from this face, and the fence stands along all of its run (|a| from 5 to 25)
                for (int i = 0; i < 8; i++) { var q = new V3((i & 1) == 0 ? blo.x : bhi.x, (i & 2) == 0 ? blo.y : bhi.y, (i & 4) == 0 ? blo.z : bhi.z); nmin = Math.Min(nmin, V3.Dot(q, n)); }
                // the fence stands from 5 m either side of the gate to the corner: the base lies wholly along one run of it
                if (!(a0 >= NativeCamp.GateHalf - 1e-3f || a1 <= -NativeCamp.GateHalf + 1e-3f) || a0 < -NativeCamp.Outer - 1e-3f || a1 > NativeCamp.Outer + 1e-3f) continue;
                best = Math.Min(best, NativeCamp.Inner - nmin);
            }
            Check(best <= NativeCamp.WallReach + 1e-3f, p.Key + " at " + Fmt(p.Light) + " within 0.25 m of the fence below 3.4 m (" + best.ToString("0.000", CultureInfo.InvariantCulture) + " m)");
        }
        Check(a.Pieces.Count(p => p.Kind == NativeCamp.Kind.Fire) == 8 && a.Pieces.Count(p => p.Kind == NativeCamp.Kind.WarBoard) == 1 && a.Pieces.Count(p => p.Kind == NativeCamp.Kind.Missing) == 1, "eight fires, the war board, the MISSING board");
        // the war and MISSING boards: flat on the north fence (6 cm at most) where the A1 table leaves them room
        foreach (var p in a.Pieces.Where(p => p.Kind == NativeCamp.Kind.WarBoard || p.Kind == NativeCamp.Kind.Missing || p.Kind == NativeCamp.Kind.Notice))
            Check(p.Side >= 0 && -p.Lo.z <= .061f, p.Key + " flat on the fence (6 cm at most)");
        // the strings: from 3.5 m, every bulb from 4.3 m, clear of every block A1 will build (and its lookout) by 0.3 m
        foreach (var s in a.Strands)
        {
            int n = (int)Math.Ceiling(s.Span / .25f);
            for (int i = 0; i <= n; i++)
            {
                var p = s.At(i / (float)n);
                Check(p.y >= NativeCamp.StringFloor, s.Id + " from 3.5 m");
                Check(Math.Abs(p.x) <= NativeCamp.Outer + .01f && Math.Abs(p.z) <= NativeCamp.Outer + .01f, s.Id + " inside the camp");
                foreach (var r in NativeCamp.Table) Check(!Overlap(p, p, r.Min, r.Max, .3f), s.Id + " clear of " + r.Id + " at " + Fmt(p));
            }
        }
        Check(a.Bulbs.Count >= 300 && a.Bulbs.Count <= 600 && a.Bulbs.All(p => p.y >= 4.3f), "300-600 bulbs, every one from 4.3 m (" + a.Bulbs.Count + ")");
        int dead = a.BulbLit.Count(l => !l); Check(dead > 0 && dead <= a.Bulbs.Count * .12f, "one bulb in about fourteen dead (" + dead + ")");
        for (int i = 1; i < a.BulbLit.Count; i++) Check(a.BulbLit[i] || a.BulbLit[i - 1], "never two dead bulbs side by side");
        foreach (var s in a.Sails)
        {
            Check(Math.Min(s.A.y, Math.Min(s.B.y, s.C.y)) >= 4.4f, s.Id + "'s corners from 4.4 m");
            float low = Math.Min(s.B.y, s.C.y) - 0; float belly = (s.A.y + s.B.y + s.C.y) / 3 - s.Belly; Check(Math.Min(low, belly) >= NativeCamp.StringFloor, s.Id + " from 3.5 m");
            var lo = Min(Min(s.A, s.B), s.C); var hi = Max(Max(s.A, s.B), s.C);
            foreach (var r in NativeCamp.Table) Check(!(lo.x < r.Max.x && hi.x > r.Min.x && lo.z < r.Max.z && hi.z > r.Min.z), s.Id + " over no block (" + r.Id + ")");
        }
        Check(a.Sounds.All(x => NativeCampSynth.Keys.Contains(x.key)) && a.Sounds.Select(x => x.key).Distinct().Count() == 4, "the sound bed: fires, generator, radio, murmur");
        Check(a.Pieces.Where(p => p.Lit).All(p => p.Range > 0 && Math.Abs(p.Light.x) < 25 && Math.Abs(p.Light.z) < 25), "every light inside the camp");
    }
    private static string Fmt(V3 v) => "(" + v.x.ToString("0.0", CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.0", CultureInfo.InvariantCulture) + ", " + v.z.ToString("0.0", CultureInfo.InvariantCulture) + ")";
    private static string PieceSig(NativeCamp.Layout l)
    {
        var w = new StringBuilder(); foreach (var p in l.Pieces) { w.Append(p.Key); foreach (var f in p.Place) w.Append(',').Append(f.ToString("R", CultureInfo.InvariantCulture)); w.Append('\n'); }
        foreach (var s in l.Strands) w.Append(s.Id).Append(Fmt(s.A)).Append(Fmt(s.B)).Append(s.Sag.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        return w.ToString();
    }
    private static string Sig(NativeCamp.Layout l)
    {
        var w = new StringBuilder(PieceSig(l)); for (int i = 0; i < l.Bulbs.Count; i++) w.Append(l.BulbLit[i] ? '1' : '0'); foreach (var s in l.Sails) w.Append(s.Hex); foreach (var p in l.Pieces) w.Append(p.Seed); return w.ToString();
    }

    // ---------------- the effects ----------------
    private static void Fx()
    {
        foreach (bool reduced in new[] { false, true })
            for (int f = 0; f < 8; f++)
            {
                // the swell's fastest frequency, from its turning points over a minute at 240 samples a second
                int turns = 0; double prev = NativeCampFx.FireLevel(f, 0, reduced), slope = 0;
                for (int i = 1; i <= 240 * 60; i++) { double v = NativeCampFx.FireLevel(f, i / 240.0, reduced), s = v - prev; if (s * slope < 0) turns++; if (s != 0) slope = s; prev = v; }
                double hz = turns / 2.0 / 60.0; Check(hz < (reduced ? 1.0 : 3.0), "fire " + f + " breathes under " + (reduced ? "1" : "3") + " Hz (" + hz.ToString("0.00", CultureInfo.InvariantCulture) + " Hz)");
            }
        Check(NativeCampFx.FastestSwell(false) < 3 && NativeCampFx.FastestSwell(true) < 1, "no camp light flashes (the photosensitivity rule)");
        var l = NativeCamp.Live(1964354793u); var fx = new NativeCampFx(l);
        Check(fx.Sources == l.Pieces.Count(p => p.Lit), "every lit piece is a light source");
        // the camp's centre at eye height, the camera looking north: the slots and the quads
        fx.Build(10, 0, 1.6, -6, 1, 0, 0, 0, 1, 0, new V3(0, 0, -6), 1, false, false, 0);
        Check(fx.LightCount >= 1 && fx.LightCount <= NativeCampFx.MaxLights, "1-3 practical slots (" + fx.LightCount + ")");
        Check(fx.Quads.Count > 0 && fx.Quads.Count < NativeCampFx.Capacity && fx.Flames == 40, "the fires' flames and the halos within the effects budget (" + fx.Quads.Count + " quads)");
        fx.Build(10, 0, 1.6, -6, 1, 0, 0, 0, 1, 0, null, 1, false, true, 0); Check(fx.LightCount <= 2 && fx.Embers == 0, "reduced effects: two slots, no embers");
        fx.Build(10, 0, 1.6, -6, 1, 0, 0, 0, 1, 0, null, 1, true, false, 0); Check(fx.LightCount <= 1, "the low tier: one slot");
        // the war board's lamp: on within 4 m of the board, a click on each change, off again past 4.3 m
        var board = l.Pieces.Single(p => p.Kind == NativeCamp.Kind.WarLamp).Light;
        fx.Build(11, board.x, 1.6, board.z - 3.5, 1, 0, 0, 0, 1, 0, new V3(board.x, 0, board.z - 3.5f), 1, false, false, .016f); Check(fx.BoardLamp && fx.BoardLampClicked, "the board lamp clicks on within 4 m");
        fx.Build(11.1, board.x, 1.6, board.z - 4.15, 1, 0, 0, 0, 1, 0, new V3(board.x, 0, board.z - 4.15f), 1, false, false, .016f); Check(fx.BoardLamp && !fx.BoardLampClicked, "and stays on to 4.3 m");
        fx.Build(11.2, board.x, 1.6, board.z - 4.5, 1, 0, 0, 0, 1, 0, new V3(board.x, 0, board.z - 4.5f), 1, false, false, .016f); Check(!fx.BoardLamp && fx.BoardLampClicked, "and clicks off beyond");
        // no allocation per frame
        for (int i = 0; i < 20; i++) fx.Build(i * .016, 3, 1.6, -4, 1, 0, 0, 0, 1, 0, new V3(3, 0, -4), 1, false, false, .016f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 300; i++) fx.Build(20 + i * .016, 3 + i * .02, 1.6, -4, 1, 0, 0, 0, 1, 0, new V3(3 + i * .02f, 0, -4), .6, i % 7 == 0, i % 5 == 0, .016f);
        Check(GC.GetAllocatedBytesForCurrentThread() - before == 0, "the camp's effects allocate nothing per frame");
    }

    // ---------------- the placeholder sounds ----------------
    private static void Synth()
    {
        foreach (var key in NativeCampSynth.Keys)
        {
            var a = NativeCampSynth.Samples(key); var b = NativeCampSynth.Samples(key);
            Check(a != null && a.Length > 1000 && a.SequenceEqual(b), key + " renders, the same every time");
            Check(a.All(x => !float.IsNaN(x) && Math.Abs(x) <= 1), key + " finite and within full scale");
            double rms = Math.Sqrt(a.Average(x => (double)x * x)); Check(rms > .01 && rms < .45, key + " at a sensible level (rms " + rms.ToString("0.000", CultureInfo.InvariantCulture) + ")");
            if (NativeCampSynth.Loop(key)) Check(Math.Abs(a[0] - a[a.Length - 1]) < .25f, key + " closes its loop");
        }
        Check(NativeCampSynth.Samples("camp.nothing") == null, "an unknown key has no placeholder");
    }

    // ---------------- the runtime ----------------
    private static void Sources(string root)
    {
        string R(string f) => File.ReadAllText(Path.Combine(root, "Assets/ChipArena/Runtime", f));
        string Core(string f) => File.ReadAllText(Path.Combine(root, "Assets/ChipArena/Core", f));
        Check(R("NativeSourcePresentation.cs").Contains("CampScenery(arena);"), "the camp draws from SourceWorld (live and killcam)");
        Check(R("NativeAudio.cs").Contains("CampAudioFrame(arena,menu);"), "the camp's sound runs every frame");
        Check(R("NativeStagePrewarm.cs").Contains("if(PrewarmCamp(arena))built++;"), "the camp is combined behind the loading screen, not on its first frame");
        Check(R("NativeExpeditionLiveUI.cs").Contains("NativeCampDraw.Sync(v);"), "the war board follows the live view's cleared instances");
        var walk = R("NativeLayeredCityWalkPreview.cs");
        foreach (var hook in new[] { "CampWalkOpen(cityWalk, index, seed, surveySlots);", "CampWalkClose();", "if (CampWalkKeys()) return true;", "CampWalkLabel();", "CampWalkBanner(width);" })
            Check(walk.Contains(hook), "the walk's hook " + hook);
        var w = R("NativeCampWalk.cs"); Check(w.StartsWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD", StringComparison.Ordinal), "the DEV preview's switches exist only in Editor and development builds");
        var d = R("NativeCampDraw.cs");
        Check(d.Contains("else if(NativeCampWalk.Active(arena.MapId)){walk=true;") && d.Contains("bool live=run?.City!=null&&arena.MapId==run.Map.id;walk=false;show=false;"), "the preview shows only in the walk: a live run never sets it");
        int start = d.IndexOf("private void BuildPreview", StringComparison.Ordinal); Check(start > 0 && d.IndexOf("#if UNITY_EDITOR || DEVELOPMENT_BUILD", start, StringComparison.Ordinal) - start < 200, "the preview's build is compiled only in Editor and development builds");
        foreach (var (name, text) in new[] { ("NativeCampDraw.cs", d), ("NativeCampAudio.cs", R("NativeCampAudio.cs")), ("NativeCampWalk.cs", w), ("NativeCamp.cs", Core("NativeCamp.cs")), ("NativeCampFx.cs", Core("NativeCampFx.cs")), ("NativeCampMeshes.cs", Core("NativeCampMeshes.cs")), ("NativeCampSynth.cs", Core("NativeCampSynth.cs")) })
        {
            foreach (var bad in new[] { "Collider", "Rigidbody", "Physics.", "NativeNetwork", "Emit(", "Replicat", "voxel", "Voxel" })
                if (!(bad == "Replicat" && name == "NativeCamp.cs")) Check(!text.Contains(bad), name + " has no " + bad + " (presentation only)");
        }
        Check(File.Exists(Path.Combine(root, "../../docs/expedition/BELVEDERE-CAMP.md")), "docs/expedition/BELVEDERE-CAMP.md");
        var requests = File.ReadAllText(Path.Combine(root, "../../docs/audio/SOUND-REQUESTS.md"));
        foreach (var key in NativeCampSynth.Keys) Check(requests.Contains("`" + key + "`"), key + " is requested in SOUND-REQUESTS.md");
    }

    // ---------------- the bank (when built) ----------------
    private static void Bank()
    {
        var path = Environment.GetEnvironmentVariable("CHIPARENA_CAMP_BANK"); if (string.IsNullOrEmpty(path)) return;
        var bank = NativePackBank.Read(File.ReadAllBytes(path));
        var l = NativeCamp.Live(1964354793u);
        IEnumerable<V3> Verts(string key, float[] place)
        {
            var prop = bank.Find(key + "/0"); Check(prop != null, "the bank has " + key);
            foreach (var part in prop.Parts)
            {
                var m = bank.Meshes[part.Mesh]; var q = part.Place;
                foreach (var p in m.Positions)
                {
                    float x = p.X * q.M11 + p.Y * q.M21 + p.Z * q.M31 + q.M41, y = p.X * q.M12 + p.Y * q.M22 + p.Z * q.M32 + q.M42, z = p.X * q.M13 + p.Y * q.M23 + p.Z * q.M33 + q.M43;
                    yield return new V3(place[0] * x + place[1] * y + place[2] * z + place[3], place[4] * x + place[5] * y + place[6] * z + place[7], place[8] * x + place[9] * y + place[10] * z + place[11]);
                }
            }
        }
        long fineTris = 0;
        foreach (var p in l.Pieces)
        {
            p.Bounds(out var lo, out var hi); bool baseOk = p.HasBase; V3 blo = default, bhi = default; if (baseOk) p.BaseBounds(out blo, out bhi);
            // the war board is banked once per plan, every one of them held to the board's envelope
            var keys = p.Kind == NativeCamp.Kind.WarBoard ? Enumerable.Range(0, 5).Select(i => "camp/board/war-" + i).ToArray() : new[] { p.Key };
            foreach (var key in keys)
            foreach (var v in Verts(key, p.Place))
            {
                Check(v.x >= lo.x - .02f && v.x <= hi.x + .02f && v.y >= lo.y - .02f && v.y <= hi.y + .02f && v.z >= lo.z - .02f && v.z <= hi.z + .02f, p.Key + "'s mesh inside its envelope at " + Fmt(v));
                if (v.y < NativeCamp.PocketTop - .01f) Check(baseOk && v.x >= blo.x - .015f && v.x <= bhi.x + .015f && v.z >= blo.z - .015f && v.z <= bhi.z + .015f, p.Key + "'s mesh below 3.4 m inside its base at " + Fmt(v));
            }
            fineTris += Tris(bank, keys[0] + "/0");
        }
        for (int plan = 0; plan < 5; plan++) Check(bank.Find("camp/board/war-" + plan + "/0") != null, "the war board painted for plan " + plan);
        foreach (var fam in new[] { "level0", "poolrooms", "wardzero", "station", "ironworks" }) Check(bank.Find("camp/board/poster-" + fam + "/0") != null, "the " + fam + " poster");
        foreach (var key in new[] { "camp/static/strands", "camp/static/sails", "camp/static/cables" })
        {
            foreach (var v in Verts(key, new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0 }))
            {
                bool cables = key.EndsWith("cables", StringComparison.Ordinal);
                if (cables) Check(Math.Max(Math.Abs(v.x), Math.Abs(v.z)) >= NativeCamp.Inner - .1f, "the cables run flat on the fence at " + Fmt(v));
                else Check(v.y >= NativeCamp.StringFloor - .02f, key + " from 3.5 m at " + Fmt(v));
            }
            fineTris += Tris(bank, key + "/0");
        }
        Check(fineTris < 150000, "the live camp's Fine triangles under 150k (" + fineTris + ")");
        foreach (var s in NativeCamp.Preview(1964354793u, 3))
        {
            var prop = bank.Find(s.Key + "/0"); Check(prop != null, "the bank has the preview's " + s.Key);
            if (!s.Key.StartsWith("camp/row/", StringComparison.Ordinal) || s.Key == "camp/row/ring" || s.Key == "camp/row/fountain") continue;
        }
        Console.WriteLine("camp: the live camp's bank meshes " + fineTris + " Fine triangles");
    }
    private static long Tris(NativePackBank.Bank bank, string name) { var p = bank.Find(name); return p == null ? 0 : p.Parts.Sum(x => (long)bank.Meshes[x.Mesh].Submeshes.Sum(s => s.Indices.Length) / 3); }

    // ---------------- the dump for tools/stages/camp_bank.py ----------------
    public static void Dump(string path)
    {
        var w = new StringBuilder();
        string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        string Vec(V3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
        string Str(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        var l = NativeCamp.Live(1964354793u);
        w.Append("{\"inner\":").Append(F(NativeCamp.Inner)).Append(",\"fenceTop\":").Append(F(NativeCamp.FenceTop)).Append(",\"pieces\":[");
        for (int i = 0; i < l.Pieces.Count; i++)
        {
            var p = l.Pieces[i]; if (i > 0) w.Append(',');
            w.Append("{\"key\":").Append(Str(p.Key)).Append(",\"kind\":").Append(Str(p.Kind.ToString())).Append(",\"side\":").Append(p.Side).Append(",\"place\":[").Append(string.Join(",", p.Place.Select(F))).Append(']')
             .Append(",\"lo\":").Append(Vec(p.Lo)).Append(",\"hi\":").Append(Vec(p.Hi)).Append(",\"baseLo\":").Append(Vec(p.BaseLo)).Append(",\"baseHi\":").Append(Vec(p.BaseHi)).Append(",\"light\":").Append(Vec(p.Light)).Append('}');
        }
        w.Append("],\"strands\":[");
        for (int i = 0; i < l.Strands.Count; i++) { var s = l.Strands[i]; if (i > 0) w.Append(','); w.Append("{\"id\":").Append(Str(s.Id)).Append(",\"a\":").Append(Vec(s.A)).Append(",\"b\":").Append(Vec(s.B)).Append(",\"sag\":").Append(F(s.Sag)).Append(",\"pitch\":").Append(F(s.Pitch)).Append('}'); }
        w.Append("],\"bulbs\":[");
        for (int i = 0; i < l.Bulbs.Count; i++) { if (i > 0) w.Append(','); var b = l.Bulbs[i]; w.Append('[').Append(F(b.x)).Append(',').Append(F(b.y)).Append(',').Append(F(b.z)).Append(',').Append(l.BulbLit[i] ? 1 : 0).Append(']'); }
        w.Append("],\"sails\":[");
        for (int i = 0; i < l.Sails.Count; i++) { var s = l.Sails[i]; if (i > 0) w.Append(','); w.Append("{\"id\":").Append(Str(s.Id)).Append(",\"a\":").Append(Vec(s.A)).Append(",\"b\":").Append(Vec(s.B)).Append(",\"c\":").Append(Vec(s.C)).Append(",\"belly\":").Append(F(s.Belly)).Append(",\"hex\":").Append(s.Hex).Append('}'); }
        w.Append("],\"table\":[");
        for (int i = 0; i < NativeCamp.Table.Length; i++) { var r = NativeCamp.Table[i]; if (i > 0) w.Append(','); w.Append("{\"id\":").Append(Str(r.Id)).Append(",\"kind\":").Append(Str(r.Kind.ToString())).Append(",\"min\":").Append(Vec(r.Min)).Append(",\"max\":").Append(Vec(r.Max)).Append('}'); }
        w.Append("],\"posts\":[");
        for (int i = 0; i < NativeCamp.Posts.Length; i++) { var p = NativeCamp.Posts[i]; if (i > 0) w.Append(','); w.Append("{\"id\":").Append(Str(p.Id)).Append(",\"structure\":").Append(Str(p.Structure)).Append(",\"pose\":").Append(Str(p.Pose)).Append(",\"at\":").Append(Vec(p.Id == "sparks" ? NativeCamp.SparksAt() : p.At)).Append(",\"yaw\":").Append(F(p.Yaw)).Append(",\"cover\":").Append(Str(p.Cover.ToString())).Append('}'); }
        w.Append("],\"preview\":[");
        var shown = NativeCamp.Preview(1964354793u, 5);
        for (int i = 0; i < shown.Count; i++) { var s = shown[i]; if (i > 0) w.Append(','); w.Append("{\"key\":").Append(Str(s.Key)).Append(",\"at\":").Append(Vec(s.At)).Append(",\"yaw\":").Append(F(s.Yaw)).Append('}'); }
        // the landing field's four worst draws over 2,000 seeds (the pods closest to a block), for the clearance diagram
        var worst = new List<(float gap, uint seed)>();
        for (uint i = 0; i < 2000; i++)
        {
            uint seed = unchecked(i * 2654435761u); var spots = NativeExpeditionHubPlan.LandingField(seed); float gap = float.MaxValue;
            for (int k = 0; k < 4; k++) foreach (var r in NativeCamp.Table) if (r.Y0 < 3) gap = Math.Min(gap, Gap2(spots[k].x, spots[k].z, r) - NativeExpeditionHubPlan.HullRadius);
            worst.Add((gap, seed));
        }
        w.Append("],\"fields\":[");
        int wi = 0;
        foreach (var (gap, seed) in worst.OrderBy(x => x.gap).Take(4))
        {
            if (wi++ > 0) w.Append(','); var spots = NativeExpeditionHubPlan.LandingField(seed);
            w.Append("{\"seed\":").Append(seed).Append(",\"gap\":").Append(F(gap)).Append(",\"pods\":[").Append(string.Join(",", spots.Take(4).Select(q => "[" + F(q.x) + "," + F(q.z) + "," + F(q.yaw) + "]"))).Append("]}");
        }
        w.Append("],\"plans\":[");
        for (int k = 0; k < NativeCityLayout.Plans.Count; k++)
        {
            var plan = NativeCityLayout.Plans[k]; var walk = new NativeLayeredCityWalk(plan); var map = walk.Map; if (k > 0) w.Append(',');
            var bnd = map.bounds; var treads = new HashSet<string>(map.ramps.SelectMany(r => r.blocks));
            w.Append("{\"index\":").Append(k).Append(",\"seed\":").Append(plan.Seed).Append(",\"name\":").Append(Str(plan.Name)).Append(",\"bounds\":[").Append(F(bnd.minX)).Append(',').Append(F(bnd.maxX)).Append(',').Append(F(bnd.minZ)).Append(',').Append(F(bnd.maxZ)).Append("],\"blocks\":[");
            bool first = true;
            foreach (var b in map.blocks)
            {
                if (treads.Contains(b.id)) continue; float top = b.pos.y + b.size.y / 2; if (!first) w.Append(','); first = false;
                w.Append('[').Append(F(b.pos.x - b.size.x / 2)).Append(',').Append(F(b.pos.x + b.size.x / 2)).Append(',').Append(F(b.pos.z - b.size.z / 2)).Append(',').Append(F(b.pos.z + b.size.z / 2)).Append(',').Append(F(top)).Append(',').Append(b.id.StartsWith("terrace-", StringComparison.Ordinal) ? 1 : 0).Append(']');
            }
            w.Append("],\"routes\":[");
            for (int r = 0; r < walk.Routes.Count; r++) { if (r > 0) w.Append(','); w.Append('[').Append(string.Join(",", walk.Routes[r].Points.Select(Vec))).Append(']'); }
            w.Append("]}");
        }
        w.Append("]}");
        File.WriteAllText(path, w.ToString());
        Console.WriteLine("camp dump: " + l.Pieces.Count + " pieces, " + l.Strands.Count + " strands, " + l.Bulbs.Count + " bulbs, " + shown.Count + " preview pieces, 5 plans -> " + path);
    }
}
