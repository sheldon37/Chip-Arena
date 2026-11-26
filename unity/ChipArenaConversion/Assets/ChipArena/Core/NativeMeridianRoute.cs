using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's described route (owner 2026-11-25, "lets get work started" on Astra's brief section 3; docs/expedition/
    /// MERIDIAN-ROUTE.md): Crown Hill's walk from the camp's north gate up the Hundred Steps and left toward City Hall, dressed so a
    /// squad can say where it is going, and the City Ironworks' door made recognisable wherever the deal puts it.
    /// <list type="bullet">
    /// <item>Leaving camp: the camp's perimeter road along the north fence with a zebra crossing at the gate, its granite kerb dropped
    /// there, and a flagged footway edged in the same granite to the first tread (all flat paint, 6 cm or less).</item>
    /// <item>The Hundred Steps: cast-iron balustrades on Astra's stair and landing guards (StreetHandrails), sloped on each flight and
    /// level on the landings, drawn in place of the guards' boxes (their colliders stay).</item>
    /// <item>Clock Street: the same kerb and footway up both sides from the stair head, the west one turning left at the top and
    /// running on along the first switchback's frontage; Meridian Stores on the corner lot's ground storey (the shopfront hugs the
    /// lot, its fascia and lamps above 3.1 m); the CITY HALL sign high on the terrace wall that closes the street; pitched roofs on
    /// Clock Street's four rows, which the tower stands between from the foot of the steps.</item>
    /// <item>The Ironworks: an Old Industry loading bay in the door front's first two wing bays, and smoke over the far layer's three
    /// stacks (drawn by the runtime as rising puffs).</item>
    /// </list>
    /// The art is the local bank <see cref="Bank"/> (tools/stages/meridian_route.py; git-ignored, from the Drive zip); without it
    /// nothing draws and every drawer keeps its look. Presentation only: never a collider, never sim data, never on the wire. Pure and
    /// deterministic: the same layout, blocks and landmarks give the same plan.
    /// </summary>
    public static class NativeMeridianRoute
    {
        public const string Bank = "ChipArenaCityRoute";
        public const string Version = "meridian-route-1";
        /// <summary>Crown Hill's NativeCityLayout seed: the only layout the walk's dressing is placed on (the brief: dress the whole
        /// walk before spreading it over the other layouts).</summary>
        public const uint CrownHill = 647;
        public const string Rails = "route/647/rails", Crossing = "route/647/crossing", Footway = "route/647/footway",
            Corner = "route/647/corner", Sign = "route/647/sign", Roofs = "route/647/roofs",
            Loading = "route/front/ironworks/loading", Puff = "route/smoke/puff";
        /// <summary>The Hundred Steps' guard stems (Core NativeLayeredCityWalk.StreetHandrails on the north route).</summary>
        public static readonly string[] StepStems = { "n-stair-1", "n-landing-2", "n-stair-3", "n-landing-4", "n-stair-5", "n-landing-6", "n-stair-7" };
        /// <summary>Meridian Stores' lot (Clock Street's north-west corner, turned toward the junction) and Clock Street's rows.</summary>
        public const string CornerLot = "lot-lower-hill-72";
        public static readonly string[] RoofLots = { "lot-lower-hill-71", "lot-lower-hill-72", "lot-lower-hill-73", "lot-lower-hill-74" };
        /// <summary>The storefront trial (issue #450, docs/expedition/MERIDIAN-ROUTE.md): the corner shop's dressing. "" is the
        /// grocer's front with Modern Supermarket's produce (release 27, key Corner); StorefrontCf is Checkout Frenzy's convenience
        /// store (the trial's pick: a lit lightbox fascia and an open, lit shop that read from the steps' head); StorefrontMix is the
        /// grocer's front with Checkout Frenzy's lit interior behind its windows. The same lot faces, keep-outs and collision for
        /// every one; the bank carries the variants it was built with (tools/stages/meridian_route.py --storefronts), and the live
        /// city falls back to the release-27 front when the bank has not this one (Runtime NativeMeridianRouteDraw.StorefrontInBank).</summary>
        public static string Storefront = StorefrontCf;
        public const string StorefrontCf = "cf", StorefrontMix = "mix";
        public static readonly string[] Storefronts = { "", StorefrontCf, StorefrontMix };
        /// <summary>The corner shop's bank key for a storefront ("" Corner itself, otherwise Corner-variant).</summary>
        public static string CornerKeyOf(string storefront) => string.IsNullOrEmpty(storefront) ? Corner : Corner + "-" + storefront;
        public static string CornerKey => CornerKeyOf(Storefront);
        /// <summary>The terrace wall that closes Clock Street (its south face carries the sign) and the camp's north gate's fence.</summary>
        public const string SignWall = "terrace-81", GateWest = "camp-fence-0--1", GateEast = "camp-fence-0-1";
        /// <summary>The shop's storey (D1's ground storey, 4 m) and how far its pieces keep the street life off its faces.</summary>
        public const float Storey = 4f, ShopApron = 1.6f;
        /// <summary>The far Ironworks' three stacks in its frame (x across, the top's height): NativeMeridianLandmarks' far/ironworks,
        /// tools/stages/meridian_lm_far.py; the plumes rise from just over their flames.</summary>
        public static readonly (float x, float top)[] Stacks = { (-10f, 89f), (0f, 94f), (10f, 86f) };
        public const float StackZ = 6f, PlumeOver = 7.5f;

        /// <summary>A plume of smoke: where it leaves its stack (world) and its seed (each stack its own rhythm).</summary>
        public readonly struct Plume { public readonly V3 At; public readonly int Seed; public Plume(V3 at, int seed) { At = at; Seed = seed; } }

        /// <summary>A lot face's ground storey the shop stands in: the face's plane (its normal axis and its coordinate), the span
        /// along it and the heights. The street dress leaves those bays to the shop (NativeMeridianDress.FacadeReplaced).</summary>
        public readonly struct Facade
        {
            public readonly V3 Normal; public readonly float Plane, Lo, Hi, Y0, Y1;
            public Facade(V3 normal, float plane, float lo, float hi, float y0, float y1) { Normal = normal; Plane = plane; Lo = lo; Hi = hi; Y0 = y0; Y1 = y1; }
            public bool Holds(V3 p, V3 n)
            {
                if (n.x * Normal.x + n.y * Normal.y + n.z * Normal.z < .9f) return false;
                bool alongZ = Math.Abs(Normal.x) > .5f;
                float c = alongZ ? p.x : p.z, u = alongZ ? p.z : p.x;
                return Math.Abs(c - Plane) < .3f && u >= Lo - .05f && u <= Hi + .05f && p.y >= Y0 - .05f && p.y <= Y1 + .05f;
            }
        }

        /// <summary>A layout's route: the pieces drawn from <see cref="Bank"/> (landmark pieces: a key, a place, the blocks drawn in
        /// place of), the lot faces the shop covers, what the street life keeps off, the plumes and the landmark pieces the route draws
        /// in place of (the Ironworks' two wing bays).</summary>
        public sealed class Plan
        {
            public uint Layout;
            /// <summary>The corner shop's bank key (the storefront placed).</summary>
            public string CornerKey;
            public readonly NativeMeridianLandmarks.Layout Art = new NativeMeridianLandmarks.Layout();
            public readonly List<Facade> Facades = new List<Facade>();
            public readonly List<NativeCityBleedDress.KeepOut> KeepOut = new List<NativeCityBleedDress.KeepOut>();
            public readonly List<Plume> Plumes = new List<Plume>();
            public readonly List<NativeMeridianLandmarks.Piece> Replaced = new List<NativeMeridianLandmarks.Piece>();
            /// <summary>Whether a facade bay's point lies on a face the shop draws (NativeMeridianDress.FacadeReplaced).</summary>
            public bool Covers(V3 p, V3 n) { foreach (var f in Facades) if (f.Holds(p, n)) return true; return false; }
        }

        /// <summary>The route on a layout. landmarks: the layout's landmark placement (NativeMeridianLandmarks.Place, as its drawer
        /// builds it), for the Ironworks' door front and far stacks; null leaves them out.</summary>
        public static Plan Place(uint layout, ContentMap map, NativeMeridianLandmarks.Layout landmarks, string storefront = null)
        {
            var plan = new Plan { Layout = layout, CornerKey = CornerKeyOf(storefront ?? Storefront) };
            if (map?.blocks == null) return plan;
            var byId = new Dictionary<string, ContentBlock>(StringComparer.Ordinal);
            foreach (var b in map.blocks) if (b?.id != null && !byId.ContainsKey(b.id)) byId[b.id] = b;
            if (layout == CrownHill) CrownHillWalk(plan, map, byId);
            if (landmarks != null) Ironworks(plan, landmarks);
            return plan;
        }

        private static void CrownHillWalk(Plan plan, ContentMap map, Dictionary<string, ContentBlock> byId)
        {
            // the Hundred Steps' balustrades, in place of the guards' boxes
            var guards = new List<string>();
            foreach (var b in map.blocks)
            {
                if (b?.id == null || !b.id.StartsWith("stair-guard-", StringComparison.Ordinal)) continue;
                foreach (var s in StepStems) if (b.id.StartsWith("stair-guard-" + s + "-", StringComparison.Ordinal)) { guards.Add(b.id); break; }
            }
            if (guards.Count > 0)
            {
                Add(plan, Rails, "The Hundred Steps' balustrades", new NativeMeridianLandmarks.Read(0, 12.6f, 74.6f, 1f, false));
                guards.Sort(StringComparer.Ordinal);
                foreach (var id in guards) plan.Art.Covers.Add(id);
            }
            // leaving camp: the perimeter road, the zebra, the footway to the first tread
            if (byId.ContainsKey(GateWest) && byId.ContainsKey(GateEast) && HasRamp(map, "n-stair-1"))
            {
                Add(plan, Crossing, "The camp's north crossing", new NativeMeridianLandmarks.Read(0, .1f, 27.4f, 4f, false));
                plan.KeepOut.Add(new NativeCityBleedDress.KeepOut(new V3(-25.2f, -.2f, 25f), new V3(25.2f, .5f, 29.9f)));
                plan.KeepOut.Add(new NativeCityBleedDress.KeepOut(new V3(-6.4f, -.2f, 29.6f), new V3(6.4f, .5f, 38.1f)));
            }
            // Clock Street's footways, the west one round the corner and along the first switchback
            if (byId.TryGetValue(CornerLot, out var corner) && HasRamp(map, "n-stair-7") && HasRamp(map, "n-ramp-9"))
            {
                float y = corner.pos.y - corner.size.y / 2;
                Add(plan, Footway, "Clock Street's footways", new NativeMeridianLandmarks.Read(-8.5f, y + .1f, 90f, 3f, false));
                plan.KeepOut.Add(new NativeCityBleedDress.KeepOut(new V3(-10.8f, y - .2f, 75f), new V3(-6.4f, y + .5f, 104.8f)));
                plan.KeepOut.Add(new NativeCityBleedDress.KeepOut(new V3(6.4f, y - .2f, 75f), new V3(10.8f, y + .5f, 104.8f)));
                plan.KeepOut.Add(new NativeCityBleedDress.KeepOut(new V3(-60.2f, y - .2f, 100f), new V3(-6.4f, y + .5f, 104.8f)));
                // Meridian Stores on the corner lot's ground storey: its east face on Clock Street and its north face on the switchback
                float x0 = corner.pos.x - corner.size.x / 2, x1 = corner.pos.x + corner.size.x / 2;
                float z0 = corner.pos.z - corner.size.z / 2, z1 = corner.pos.z + corner.size.z / 2;
                plan.Facades.Add(new Facade(new V3(1, 0, 0), x1, z0, z1, y, y + Storey));
                plan.Facades.Add(new Facade(new V3(0, 0, 1), z1, x0, x1, y, y + Storey));
                Add(plan, plan.CornerKey, "Meridian Stores", new NativeMeridianLandmarks.Read(x1 + .3f, y + 4.6f, z1 - 3f, 4f), new NativeMeridianLandmarks.Read(x1 - 2f, y + 4.6f, z1 + .3f, 3f));
                // the shop's faces and their aprons keep the street life off (core: no poster or board on the shopfront)
                plan.KeepOut.Add(new NativeCityBleedDress.KeepOut(new V3(x1 - .3f, y - .2f, z0 - .4f), new V3(x1 + ShopApron, y + 7.2f, z1 + ShopApron), true));
                plan.KeepOut.Add(new NativeCityBleedDress.KeepOut(new V3(x0 - .4f, y - .2f, z1 - .3f), new V3(x1 + ShopApron, y + 7.2f, z1 + ShopApron), true));
            }
            // the CITY HALL sign high on the terrace wall that closes Clock Street (its south face, 3.1 m and more over the street)
            if (byId.TryGetValue(SignWall, out var wall) && HasRamp(map, "n-ramp-9"))
            {
                float face = wall.pos.z - wall.size.z / 2, street = 14f;
                Add(plan, Sign, "The CITY HALL sign", new NativeMeridianLandmarks.Read(-1f, street + 4.4f, face - .2f, 5f));
                plan.KeepOut.Add(new NativeCityBleedDress.KeepOut(new V3(-7.6f, street + 2.6f, face - .6f), new V3(5.6f, street + 7f, face + .2f), true));
            }
            // pitched roofs on Clock Street's rows (the plans mark them pitched; their wedges are Astra's to add)
            int roofs = 0;
            foreach (var id in RoofLots) if (byId.ContainsKey(id)) roofs++;
            if (roofs > 0) Add(plan, Roofs, "Clock Street's roofs", new NativeMeridianLandmarks.Read(-30f, 24f, 95f, 10f, false));
        }

        // The Ironworks' door front: the two wing bays right of the door give way to the loading bay; the far stacks smoke.
        private static void Ironworks(Plan plan, NativeMeridianLandmarks.Layout landmarks)
        {
            foreach (var h in landmarks.Halls)
            {
                NativeMeridianLandmarks.Piece first = null, second = null; float d0 = float.MaxValue, d1 = float.MaxValue;
                foreach (var p in landmarks.Pieces)
                {
                    if (p.Instance != "ironworks" || p.Slot != h.Slot || p.Key != "front/ironworks/bay") continue;
                    var at = p.At - h.Door; float u = at.x * h.Across.x + at.z * h.Across.z, w = at.x * h.Out.x + at.z * h.Out.z;
                    if (u < NativeMeridianLandmarks.DoorHalf - .1f || Math.Abs(w) > .5f) continue;   // the right wing's front bays only
                    if (u < d0) { second = first; d1 = d0; first = p; d0 = u; }
                    else if (u < d1) { second = p; d1 = u; }
                }
                if (first == null || second == null) continue;
                var piece = new NativeMeridianLandmarks.Piece { Key = Loading, Name = "Ironworks loading bay", Kind = NativeMeridianLandmarks.Kind.Front,
                    Slot = h.Slot, Instance = "ironworks", Reads = new[] { new NativeMeridianLandmarks.Read(first.At + h.Out * .6f + new V3(0, 5.2f, 0), 4f) } };
                Array.Copy(first.Place, piece.Place, 12);
                plan.Art.Pieces.Add(piece);
                plan.Replaced.Add(first); plan.Replaced.Add(second);
            }
            int seed = 0;
            foreach (var p in landmarks.Pieces)
            {
                if (p.Kind != NativeMeridianLandmarks.Kind.Far || p.Key != "far/ironworks") continue;
                foreach (var s in Stacks)
                    plan.Plumes.Add(new Plume(NativeMeridianLandmarks.Apply(p.Place, new V3(s.x, s.top + PlumeOver, StackZ)), seed++));
            }
        }

        private static void Add(Plan plan, string key, string name, params NativeMeridianLandmarks.Read[] reads)
        {
            var p = new NativeMeridianLandmarks.Piece { Key = key, Name = name, Kind = NativeMeridianLandmarks.Kind.Exit, Gate = 0, Reads = reads };
            Array.Clear(p.Place, 0, 12); p.Place[0] = p.Place[5] = p.Place[10] = 1;
            plan.Art.Pieces.Add(p);
        }

        private static bool HasRamp(ContentMap map, string id)
        {
            if (map.ramps == null) return false;
            foreach (var r in map.ramps) if (r?.id == id) return true;
            return false;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The smoke (runtime, the far layer): each plume a column of puffs leaving its stack's flame, drifting down the wind and
        // swelling as they rise, then shrinking away (low-poly opaque puffs, as Polygon Arsenal's smoke stacks are drawn: no
        // transparency, so no sorting and nothing to fade). Pure: a puff's place and size at a time.

        /// <summary>The puffs a plume keeps in the air, each one's life (s), its rise (m), its drift (m) and its largest radius (m).</summary>
        public const int PuffsPerPlume = 16;
        public const float PuffLife = 24f, PuffRise = 72f, PuffDrift = 42f, PuffRadius = 9f;
        /// <summary>The wind the smoke leans down (unit, level): off the sea, toward the north-east.</summary>
        public static readonly V3 Wind = new V3(.55f, 0, .835f);

        private static uint PuffHash(in Plume plume, int i) => NativeMeridianDress.Mix((uint)(plume.Seed * 977 + i * 131 + 7));
        /// <summary>Puff i's share of its life at time t (0 leaving the stack .. 1 gone): the puffs are spread evenly through the
        /// life, each nudged by its own phase.</summary>
        public static float AgeOf(in Plume plume, int i, float t)
        {
            float phase = (PuffHash(plume, i) & 1023) / 1023f * .5f / PuffsPerPlume;
            float age = (t / PuffLife + phase + i / (float)PuffsPerPlume) % 1f;
            return age < 0 ? age + 1f : age;
        }
        /// <summary>Puff i of a plume at time t: its centre and radius (0: between lives). Each puff's phase and wobble come from the
        /// plume's seed, so the column never pulses in step.</summary>
        public static void PuffAt(in Plume plume, int i, float t, out V3 centre, out float radius)
        {
            uint h = PuffHash(plume, i);
            float phase = (h & 1023) / 1023f, jitter = ((h >> 10) & 1023) / 1023f - .5f, side = ((h >> 20) & 1023) / 1023f - .5f;
            float age = AgeOf(plume, i, t);
            // rise eases out (the plume slows as it cools), the drift grows with height, a slow sideways weave
            float rise = PuffRise * (1 - (1 - age) * (1 - age)) * (1 + jitter * .12f);
            float drift = PuffDrift * age * age * (1 + jitter * .2f);
            var across = new V3(Wind.z, 0, -Wind.x);
            float weave = (float)Math.Sin((t * .21f + phase * 6.283f) + age * 3.1f) * 2.2f * age + side * 3f * age;
            centre = plume.At + new V3(0, rise, 0) + Wind * drift + across * weave;
            // swell to full size by a third of the life, hold, shrink away over the last quarter
            float grow = age < .33f ? .35f + .65f * (age / .33f) : 1f;
            float shrink = age > .75f ? Math.Max(0, 1 - (age - .75f) / .25f) : 1f;
            radius = PuffRadius * grow * (float)Math.Sqrt(shrink) * (1 + jitter * .25f);
        }
    }
}
