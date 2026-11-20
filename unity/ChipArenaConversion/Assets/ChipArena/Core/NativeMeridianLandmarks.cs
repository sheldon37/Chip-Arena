using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's landmarks and doors (docs/EXPEDITION-OVERHAUL.md W10 and W8b; the MERIDIAN art bible 2.5, 5.5, 7 and 10): where
    /// every modelled landmark of the five approved street layouts (NativeCityLayout, meridian-b-candidate-3) stands, built from
    /// the layout's records only (its blocks by id, the court's gateway and the three hall doors, and the walk's hall deal), so
    /// the same table serves the Developer walk today and the live run once it moves onto these layouts.
    /// <list type="bullet">
    /// <item>The 20 exit landmarks: one per gate per layout, a set piece in or just past the exit's first 60 m that names it by its
    /// shape (the Hundred Steps' lantern arch, the Iron Bridge's bowstring arches, the Galleria's glass fan ...). They are modelled
    /// in the world's own frame, hugging the built blocks or overhead (the art rules: never more than 0.25 m short of a collider,
    /// nothing proud of one below 3.1 m but flat paint).</item>
    /// <item>The final court (NOCTIEL's): City Hall and its 62 m clock tower at 6:47, the court's walls and gateway, in the court's
    /// frame at its gateway.</item>
    /// <item>The door fronts: each dealt instance's front on its hall (the hall block's footprint and about 12 m in front of it),
    /// a door piece round the 10 x 9 m mouth with bays repeated along the wings, the returns and the back, the roof and the mouth's
    /// lining, and the airlock's two leaves as rig parts (<see cref="LeafSlide"/>).</item>
    /// <item>The far layer: each dealt instance's landmark beyond the Cordon on the camp's line through its door (THRESHOLD MUTUAL,
    /// MERIDIAN PUBLIC BATHS, the Biocontainment slab, the station's train shed and clock, the Ironworks' stacks), and the WMRD 64.7
    /// mast beyond the north-east Cordon.</item>
    /// </list>
    /// The art is the local bank <see cref="Bank"/> (tools/stages/meridian_landmarks.py; git-ignored, from the Drive zip); without it
    /// nothing draws and the greybox stays. Presentation only: never sim data, never a collider, never on the wire. Pure and
    /// deterministic: the same layout, blocks, doors and deal give the same pieces.
    /// </summary>
    public static class NativeMeridianLandmarks
    {
        public const string Bank = "ChipArenaCityLandmarks";
        public const string Version = "meridian-landmarks-1";
        /// <summary>The camp's four gates (N, E, S, W) on its fence line, and how far inside it the read eye stands.</summary>
        public const float GateRadius = 25f, EyeInside = 1f, EyeHeight = 1.65f;
        /// <summary>The door front's reach in front of the hall face (the D2/D3 line) and the join circle's keep-out.</summary>
        public const float ApronDepth = 12f, JoinOut = 8f, JoinKeep = 3.3f;
        /// <summary>The hall mouth (the walk's halls: 10 m wide, 9 m to the hall's upper block), and the door piece's half width.</summary>
        public const float MouthHalf = 5f, MouthHeight = 9f, DoorHalf = 7.5f, Bay = 2.5f, Lining = 2f;
        /// <summary>How far an airlock leaf slides into its wing when open (its pocket is behind the wing's face).</summary>
        public const float LeafTravel = 5.2f;
        /// <summary>The far layer stands this far outside the map's bounds, at least.</summary>
        public const float FarMargin = 55f;

        public enum Kind { Exit, Court, Front, FrontRig, Far, Mast }

        /// <summary>A read point: a feature that has to read from the camp (LandmarkReadChecks), its size across (m) and whether it
        /// glows (the far layer reads by its emissive parts, fogged at most <see cref="FarFogCap"/>).</summary>
        public readonly struct Read
        {
            public readonly V3 At; public readonly float Size; public readonly bool Glow;
            public Read(float x, float y, float z, float size, bool glow = true) { At = new V3(x, y, z); Size = size; Glow = glow; }
            public Read(V3 at, float size, bool glow = true) { At = at; Size = size; Glow = glow; }
        }
        public const float FarFogCap = .65f;
        /// <summary>How far the exit landmarks and the court draw (m), and the far layer (the art bible's far layer and skyline ring).</summary>
        public const float ExitReach = 420f, FarReach = 900f;
        /// <summary>The fog a landmark takes at a distance under a look's linear fog (start, end): the scene's own, up to
        /// <see cref="FarFogCap"/>; where the scene's would pass it the runtime draws the piece with the far material, whose fog
        /// stops at the cap (art bible 2.2: the far layer's emissive parts fogged to at most about 65%).</summary>
        public static float FogOf(float distance, float start, float end)
        {
            float f = end > start ? (distance - start) / (end - start) : 1;
            return Math.Max(0, Math.Min(FarFogCap, f));
        }
        /// <summary>The distance from which a look's fog would pass the cap (the runtime's switch to the far material).</summary>
        public static float FarFrom(float start, float end) => start + (end - start) * FarFogCap;
        /// <summary>The most triangles a bank piece may have at a tier (0 Fine, 2 Low, 3 far; the bank script refuses a piece over its
        /// cap, LandmarkReadChecks sums the caps per view against the frame budget). A tier the bank leaves out draws the next one.</summary>
        public static int TriangleCap(string key, int tier)
        {
            if (key == null) return 0;
            if (key.StartsWith("exit/", StringComparison.Ordinal)) return tier == 0 ? 14000 : tier == 2 ? 2500 : 0;
            if (key == "court/final") return tier == 0 ? 45000 : tier == 2 ? 8000 : 600;
            if (key == "far/mast") return tier == 3 ? 1600 : 0;
            if (key.StartsWith("far/", StringComparison.Ordinal)) return tier == 3 ? 2500 : 0;
            if (key.StartsWith("front/", StringComparison.Ordinal))
            {
                string part = key.Substring(key.LastIndexOf('/') + 1);
                switch (part)
                {
                    case "door": return tier == 0 ? 4000 : tier == 2 ? 1500 : 0;
                    case "rear": return tier == 0 ? 1500 : tier == 2 ? 400 : 0;
                    case "bay": case "side": case "back": return tier == 0 ? 700 : tier == 2 ? 150 : 0;
                    case "lining": return 120;
                    case "roof": return 4;
                    default: return tier == 0 ? 500 : tier == 2 ? 300 : 0;   // the leaves
                }
            }
            return 0;
        }
        /// <summary>The frame budget's room for the landmarks in one view (the owner's complete-view caps less the greybox's measured
        /// most: 1.01 M on High, 0.35 M on Low, docs/MERIDIAN-B-WALK.md), and the draws they may add.</summary>
        public const int HighRoom = 2177224 - 1010000, LowRoom = 821492 - 350000, DrawRoom = 700;

        /// <summary>The far landmarks' footprint radius (m) round their place: all of it stands outside the map.</summary>
        public static float FarRadius(string family)
        {
            switch (family)
            {
                case "level0": return 24f;
                case "poolrooms": return 31f;
                case "wardzero": return 28f;
                case "station": return 50f;
                case "ironworks": return 32f;
                default: return 34f;   // the mast and its guys
            }
        }

        /// <summary>One drawn piece: the bank prop `Key` (its tiers `Key/0`, `Key/2`, `Key/far`) at `Place` (3 x 4 rows, world).</summary>
        public sealed class Piece
        {
            public string Key, Name, Instance;
            public Kind Kind;
            public int Gate = -1, Slot = -1;
            public readonly float[] Place = new float[12];
            public Read[] Reads = Array.Empty<Read>();
            public V3 At => new V3(Place[3], Place[7], Place[11]);
        }

        /// <summary>A layout's placement: the pieces, and the map blocks the art draws in place of (their boxes are left out).</summary>
        public sealed class Layout
        {
            public uint Plan;
            public readonly List<Piece> Pieces = new List<Piece>();
            public readonly HashSet<string> Covers = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<Hall> Halls = new List<Hall>();
        }

        /// <summary>An exit landmark of the table.</summary>
        public sealed class Exit
        {
            public uint Plan; public int Gate; public string Key, Name, Shape; public Read[] Reads; public string[] Covers;
        }

        // The 20 exit landmarks (gate 0 N, 1 E, 2 S, 3 W). Read points are where the bank's model puts the named feature, in the
        // world's frame; tools/stages/meridian_landmarks.py builds each piece round them and checks it holds the art rules.
        private static readonly Exit[] Exits =
        {
            // Crown Hill: a hill town of steps, terraces and the Quay
            X(647, 0, "steps-lanterns", "The Hundred Steps", "an iron lantern arch over the steps and the Crown's stair head", new[] { new Read(0, 8.6f, 46f, 3.2f), new Read(9.5f, 12f, 74.75f, 2.6f), new Read(-9.5f, 12f, 74.75f, 2.6f) }),
            X(647, 1, "belvedere-hotel", "The Promenade", "the Belvedere Hotel's neon blade sign", new[] { new Read(81f, 7.4f, -14.3f, 6.5f) }, "lot-shelf-s-7"),
            X(647, 2, "chute-washing", "The Chute", "washing lines across the tenement slot and the pawnbroker's three balls", new[] { new Read(0, 9.5f, -39f, 6f, false), new Read(-1.9f, 6.3f, -35f, 1.4f, false), new Read(-1.4f, 7.7f, -35f, .8f) }),
            X(647, 3, "cutting-struts", "The Hill Cutting", "a stone flying arch and iron struts across the cutting", new[] { new Read(-111f, 4.6f, 0, 8f) }),
            // The Navigation: a drained canal round Mill Island
            X(784, 0, "iron-bridge-arches", "The Iron Bridge", "twin lattice bowstring arches over the bridge", new[] { new Read(0, 9.2f, 65f, 8f) }),
            X(784, 1, "lock-gates", "The Lock ramp", "the open lock gates, the lock signal and the keeper's lamp", new[] { new Read(64f, 5.6f, -25.6f, 1.6f) }, "lock-cottage"),
            X(784, 2, "causeway-lamps", "The Low Causeway", "swan-neck lamps down the causeway and its water gate", new[] { new Read(0, -3.3f, -61f, 1.2f), new Read(0, -1.3f, -84.7f, 5f) }),
            X(784, 3, "mill-lucam", "The covered mill bridge", "the mill's lucam and sack hoist", new[] { new Read(-50.6f, 10.4f, -14f, 4f) }, "mill"),
            // The Arcades: a covered market, its roofs and its undercroft
            X(1058, 0, "galleria-fan", "The Galleria", "the Galleria's glass fan and its globe lanterns", new[] { new Read(0, 6.3f, 66f, 1.1f), new Read(0, 6.4f, 78f, 1.1f) }),
            X(1058, 1, "shambles-bull", "The Shambles", "the gilded bull's head over the lane", new[] { new Read(72.6f, 7.3f, 22.3f, 2.2f) }),
            X(1058, 2, "undercroft-crypt", "The Undercroft", "the crypt lantern at the jog and the vaulted portal", new[] { new Read(-15f, 4.4f, -50f, 1.6f) }),
            X(1058, 3, "leads-cupola", "The Leads", "the stair tower's lantern cupola", new[] { new Read(-33.8f, 9.4f, .5f, 3.6f) }),
            // The Stride: the viaduct across the city
            X(1332, 0, "arch-nought-clock", "Arch Nought and Crag Road", "the clock hung under Arch Nought", new[] { new Read(0, 4.6f, 51f, 2.4f) }),
            X(1332, 1, "deck-hoarding", "The deck stair", "the railway's lit hoarding on the deck approach", new[] { new Read(36.7f, 6.5f, 27f, 11f) }),
            X(1332, 2, "vale-wheel", "The Vale haul ramp", "the winding house's great wheel", new[] { new Read(-7.6f, 3.6f, -64f, 8f, false), new Read(-9.1f, 8.3f, -64f, .8f) }, "lot-vale-c-109"),
            X(1332, 3, "rialto-marquee", "The Valley Steps", "the Rialto's marquee of bulbs", new[] { new Read(-84f, 4.6f, 9.9f, 9f) }, "lot-mid-131"),
            // The Yards: rail yards on split levels
            X(1606, 0, "gantry-semaphores", "The Signal Gantry", "semaphore signals on the gantry and the signal box", new[] { new Read(4.7f, 13.4f, 73f, 1.2f), new Read(-4.7f, 13.4f, 73f, 1.2f) }, "signal-box"),
            X(1606, 1, "coaling-tower", "The sidings incline", "the coaling tower", new[] { new Read(158f, 21f, 29.6f, 6f) }, "coaling-tower"),
            X(1606, 2, "footbridge-gable", "The covered footbridge", "the footbridge's fretwork gable and its lamp", new[] { new Read(0, 11.2f, -47.4f, 7f) }),
            X(1606, 3, "water-tower", "The High Yard haul ramp", "the water tower", new[] { new Read(-109.6f, 16.4f, -35f, 1.2f), new Read(-109.6f, 17f, -35f, 10f, false) }, "water-tower"),
        };
        private static Exit X(uint plan, int gate, string key, string name, string shape, Read[] reads, params string[] covers) =>
            new Exit { Plan = plan, Gate = gate, Key = "exit/" + plan + "/" + "nesw"[gate] + "-" + key, Name = name, Shape = shape, Reads = reads, Covers = covers };

        public static IReadOnlyList<Exit> ExitTable => Exits;

        /// <summary>The instance families a hall can wear, by the walk's deal names (NativeCityLayout.DealHalls's ids and the bleed's).</summary>
        public static readonly string[] Instances = { "level0", "poolrooms", "wardzero", "station", "ironworks" };
        /// <summary>A dealt slot's family (null: the final court or an unknown name). Matches the bleed's theme ids.</summary>
        public static string Family(string dealt)
        {
            if (string.IsNullOrEmpty(dealt)) return null;
            var s = dealt.ToLowerInvariant();
            if (s.Contains("level 0") || s.Contains("level0") || s.Contains("backrooms")) return "level0";
            if (s.Contains("pool")) return "poolrooms";
            if (s.Contains("ward")) return "wardzero";
            if (s.Contains("station") || s == "exp-transit") return "station";
            if (s.Contains("iron") || s == "exp-foundry") return "ironworks";
            return null;
        }

        /// <summary>The read eye at a gate (0 N, 1 E, 2 S, 3 W): on the gate's line, a metre inside the camp, at eye height.</summary>
        public static V3 GateEye(int gate)
        {
            float r = GateRadius - EyeInside;
            switch (gate)
            {
                case 0: return new V3(0, EyeHeight, r);
                case 1: return new V3(r, EyeHeight, 0);
                case 2: return new V3(0, EyeHeight, -r);
                default: return new V3(-r, EyeHeight, 0);
            }
        }

        /// <summary>A hall's frame, from its blocks (`hall-a` and its wings) and its door point: the door on the face, `Out` the
        /// face's outward normal, `Across` the face's +x seen from outside (Unity: Cross(up, into)).</summary>
        public sealed class Hall
        {
            public string Id; public int Slot; public V3 Door, Out, Across; public float Width, Depth, Height;
            public float MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
            public readonly List<string> Blocks = new List<string>();
        }

        /// <summary>The three halls (slots 1-3: doors a, b, c) of a layout's map; doors[k] is slot k's door point (doors[0], the court's
        /// gateway, is not used here). A hall whose blocks are missing is left out.</summary>
        public static List<Hall> Halls(ContentMap map, IReadOnlyList<V3> doors)
        {
            var halls = new List<Hall>();
            if (map?.blocks == null || doors == null) return halls;
            for (int slot = 1; slot <= 3 && slot < doors.Count; slot++)
            {
                string id = "hall-" + "abc"[slot - 1];
                var h = new Hall { Id = id, Slot = slot, Door = doors[slot], MinX = float.MaxValue, MinY = float.MaxValue, MinZ = float.MaxValue,
                    MaxX = float.MinValue, MaxY = float.MinValue, MaxZ = float.MinValue };
                foreach (var b in map.blocks)
                {
                    if (b?.id == null || !(b.id == id || b.id.StartsWith(id + "-", StringComparison.Ordinal))) continue;
                    h.Blocks.Add(b.id);
                    h.MinX = Math.Min(h.MinX, b.pos.x - b.size.x / 2); h.MaxX = Math.Max(h.MaxX, b.pos.x + b.size.x / 2);
                    h.MinY = Math.Min(h.MinY, b.pos.y - b.size.y / 2); h.MaxY = Math.Max(h.MaxY, b.pos.y + b.size.y / 2);
                    h.MinZ = Math.Min(h.MinZ, b.pos.z - b.size.z / 2); h.MaxZ = Math.Max(h.MaxZ, b.pos.z + b.size.z / 2);
                }
                if (h.Blocks.Count == 0) continue;
                var d = h.Door;
                // the face the door lies on
                float[] gaps = { Math.Abs(d.x - h.MinX), Math.Abs(d.x - h.MaxX), Math.Abs(d.z - h.MinZ), Math.Abs(d.z - h.MaxZ) };
                int face = 0; for (int i = 1; i < 4; i++) if (gaps[i] < gaps[face]) face = i;
                h.Out = face == 0 ? new V3(-1, 0, 0) : face == 1 ? new V3(1, 0, 0) : face == 2 ? new V3(0, 0, -1) : new V3(0, 0, 1);
                var into = h.Out * -1;
                h.Across = new V3(into.z, 0, -into.x);
                h.Width = face < 2 ? h.MaxZ - h.MinZ : h.MaxX - h.MinX;
                h.Depth = face < 2 ? h.MaxX - h.MinX : h.MaxZ - h.MinZ;
                h.Height = h.MaxY - d.y;
                halls.Add(h);
            }
            return halls;
        }

        /// <summary>The airlock leaf's slide into its wing (m along the hall's across axis, outward from the mouth's middle) for an
        /// opening share 0 (shut) .. 1 (open, the leaf wholly inside its wing's pocket). The walk's mouths have no closure collider, so
        /// the walk draws them open (1); the live run passes its airlock's state.</summary>
        public static float LeafSlide(float open) => LeafTravel * (open < 0 ? 0 : open > 1 ? 1 : open);
        /// <summary>A leaf's place (3 x 4 rows, world) in its hall's front for an opening share; side -1 the left leaf, +1 the right.</summary>
        public static void PoseOf(Hall hall, int side, float open, float[] into)
        {
            var t = hall.Door + hall.Across * (side * LeafSlide(open));
            Frame(hall.Across, hall.Out * -1, t, 1, 1, into);
        }

        /// <summary>Places a layout's landmarks. plan: the layout's NativeCityLayout seed; doors: [0] the court's gateway point,
        /// [1..3] the hall doors; slots: the walk's deal (NativeCityLayout.DealHalls: [0] the final court, [1..3] the instances).</summary>
        public static Layout Place(uint plan, ContentMap map, IReadOnlyList<V3> doors, IReadOnlyList<string> slots, float airlockOpen = 1f)
        {
            var layout = new Layout { Plan = plan };
            if (map?.blocks == null) return layout;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in map.blocks) if (b?.id != null) ids.Add(b.id);
            // the exit landmarks: world-framed models
            foreach (var e in Exits)
            {
                if (e.Plan != plan) continue;
                var p = new Piece { Key = e.Key, Name = e.Name, Kind = Kind.Exit, Gate = e.Gate, Reads = e.Reads };
                Identity(p.Place);
                layout.Pieces.Add(p);
                foreach (var c in e.Covers) if (ids.Contains(c)) layout.Covers.Add(c);
            }
            // the final court: City Hall, its tower, the court's walls and gateway, in the gateway's frame (the court faces south)
            if (doors != null && doors.Count > 0 && ids.Contains("city-hall") && ids.Contains("city-hall-tower"))
            {
                var g = doors[0];
                var court = new Piece { Key = "court/final", Name = "City Hall court", Kind = Kind.Court, Gate = 0, Slot = 0,
                    Reads = new[] { new Read(g + new V3(0, 47f, 44.3f), 6f), new Read(g + new V3(0, 73.2f, 50.5f), 2f) } };
                Frame(new V3(1, 0, 0), new V3(0, 0, 1), g, 1, 1, court.Place);
                layout.Pieces.Add(court);
                foreach (var id in CourtBlocks) if (ids.Contains(id)) layout.Covers.Add(id);
            }
            // the door fronts and the far layer, by the deal
            var halls = Halls(map, doors);
            layout.Halls.AddRange(halls);
            foreach (var h in halls)
            {
                string fam = slots != null && h.Slot < slots.Count ? Family(slots[h.Slot]) : null;
                if (fam == null) continue;
                Front(layout, h, fam, airlockOpen);
                foreach (var id in h.Blocks) layout.Covers.Add(id);
                FarPiece(layout, map, h, fam);
            }
            // the WMRD 64.7 mast, beyond the north-east Cordon
            if (map.bounds != null)
            {
                var at = new V3(map.bounds.maxX + 70, -12, map.bounds.maxZ + 35);
                var mast = new Piece { Key = "far/mast", Name = "WMRD 64.7", Kind = Kind.Mast, Reads = new[] { new Read(at + new V3(0, 168, 0), 4f), new Read(at + new V3(0, 112, 0), 4f), new Read(at + new V3(0, 60, 0), 4f) } };
                Frame(new V3(1, 0, 0), new V3(0, 0, 1), at, 1, 1, mast.Place);
                layout.Pieces.Add(mast);
            }
            return layout;
        }

        /// <summary>The blocks a layout's landmarks always draw in place of, whatever the deal: the exits' hero lots, the court and
        /// the three halls (every dealt instance's front covers its hall). The city's own drawer leaves these to the landmarks.</summary>
        public static HashSet<string> FixedCovers(uint plan, ContentMap map)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (map?.blocks == null) return set;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in map.blocks) if (b?.id != null) ids.Add(b.id);
            foreach (var e in Exits) if (e.Plan == plan) foreach (var c in e.Covers) if (ids.Contains(c)) set.Add(c);
            foreach (var c in CourtBlocks) if (ids.Contains(c)) set.Add(c);
            foreach (var id in ids)
                if (id == "hall-a" || id == "hall-b" || id == "hall-c" || id.StartsWith("hall-a-", StringComparison.Ordinal) ||
                    id.StartsWith("hall-b-", StringComparison.Ordinal) || id.StartsWith("hall-c-", StringComparison.Ordinal)) set.Add(id);
            return set;
        }

        /// <summary>The court's blocks the court's model draws in place of.</summary>
        public static readonly string[] CourtBlocks = { "city-hall", "city-hall-tower", "court-wall-S-a", "court-wall-S-b", "court-wall-N",
            "court-wall-W", "court-wall-E", "court-gate-lintel", "court-gate-pier-a", "court-gate-pier-b" };

        // A door front: the door piece, the wings' bays, the returns, the back, the roof, the mouth's lining and the leaves.
        private static void Front(Layout layout, Hall h, string fam, float open)
        {
            string k = "front/" + fam + "/";
            var into = h.Out * -1;
            V3 At(float x, float z) => h.Door + h.Across * x + into * z;
            void Add(string part, V3 x, V3 z, V3 t, float sx, float sz, Kind kind = Kind.Front, Read[] reads = null)
            {
                var p = new Piece { Key = k + part, Name = fam + " " + part, Kind = kind, Slot = h.Slot, Instance = fam, Reads = reads ?? Array.Empty<Read>() };
                Frame(x, z, t, sx, sz, p.Place);
                layout.Pieces.Add(p);
            }
            float half = h.Width / 2;
            Add("door", h.Across, into, h.Door, 1, 1, Kind.Front, new[] { new Read(At(0, -.4f) + new V3(0, MouthHeight + 1.6f, 0), 6f) });
            // the wings: bays from the door piece's edge to the corner, each 2.5 m stretched to fit
            float wing = half - DoorHalf;
            if (wing > .5f)
            {
                int n = Math.Max(1, (int)Math.Round(wing / Bay)); float s = wing / (n * Bay);
                for (int i = 0; i < n; i++)
                {
                    Add("bay", h.Across, into, At(DoorHalf + i * Bay * s, 0), s, 1);
                    Add("bay", h.Across, into, At(-half + i * Bay * s, 0), s, 1);
                }
            }
            // the returns and the back: the same bay kit turned to each face (the bay's face is its local -z)
            {
                int n = Math.Max(1, (int)Math.Round(h.Depth / Bay)); float s = h.Depth / (n * Bay);
                for (int i = 0; i < n; i++)
                {
                    // the right return faces +across; walking along it from the front corner, into the hall
                    Add("side", into, h.Across * -1, At(half, i * Bay * s), s, 1);
                    Add("side", into * -1, h.Across, At(-half, (i + 1) * Bay * s), s, 1);
                }
                // the back: the mouth runs through the hall, so the back has its own piece round it and bays on its wings
                Add("rear", h.Across * -1, h.Out, At(0, h.Depth), 1, 1);
                if (wing > .5f)
                {
                    int m = Math.Max(1, (int)Math.Round(wing / Bay)); float r = wing / (m * Bay);
                    for (int i = 0; i < m; i++)
                    {
                        Add("back", h.Across * -1, h.Out, At(half - i * Bay * r, h.Depth), r, 1);
                        Add("back", h.Across * -1, h.Out, At(-DoorHalf - i * Bay * r, h.Depth), r, 1);
                    }
                }
            }
            // the roof (a unit square stretched over the hall's top) and the mouth's lining (2 m lengths through the hall)
            Add("roof", h.Across, into, At(-half, 0), h.Width, h.Depth);
            Add("lining", h.Across, into, At(0, 0), 1, h.Depth / Lining);
            // the airlock's leaves, posed by PoseOf
            for (int side = -1; side <= 1; side += 2)
            {
                var p = new Piece { Key = k + (side < 0 ? "leaf_l" : "leaf_r"), Name = fam + " airlock leaf", Kind = Kind.FrontRig, Slot = h.Slot, Instance = fam };
                PoseOf(h, side, open, p.Place);
                layout.Pieces.Add(p);
            }
        }

        // The instance's far landmark: beyond the Cordon on the camp's line through the door, facing the camp.
        private static void FarPiece(Layout layout, ContentMap map, Hall h, string fam)
        {
            var b = map.bounds; if (b == null) return;
            var d = new V3(h.Door.x, 0, h.Door.z); float len = d.Length; if (len < 1) return; var dir = d / len;
            // the first distance along the ray that clears the bounds by the margin on every axis it leaves through
            float s = float.MaxValue;
            if (dir.x > 1e-4f) s = Math.Min(s, (b.maxX + FarMargin) / dir.x); else if (dir.x < -1e-4f) s = Math.Min(s, (b.minX - FarMargin) / dir.x);
            if (dir.z > 1e-4f) s = Math.Min(s, (b.maxZ + FarMargin) / dir.z); else if (dir.z < -1e-4f) s = Math.Min(s, (b.minZ - FarMargin) / dir.z);
            var at = dir * s + new V3(0, FarBase, 0);
            var x = new V3(dir.z, 0, -dir.x);
            var p = new Piece { Key = "far/" + fam, Name = fam + " far landmark", Kind = Kind.Far, Slot = h.Slot, Instance = fam, Reads = FarReads(fam, at, dir, x) };
            Frame(x, dir, at, 1, 1, p.Place);
            layout.Pieces.Add(p);
        }
        /// <summary>The far landmarks' foot (m): their lower storeys stand behind the Cordon's 14 m wall.</summary>
        public const float FarBase = -12f;
        // Each far landmark's read features in its own frame (x across, y up, z away from the camp): what the bank models.
        private static Read[] FarReads(string fam, V3 at, V3 dir, V3 x)
        {
            V3 P(float lx, float ly, float lz) => at + x * lx + new V3(0, ly, 0) + dir * lz;
            switch (fam)
            {
                case "level0": return new[] { new Read(P(0, 80f, -9.2f), 30f), new Read(P(0, 91f, -9.2f), 24f) };
                case "poolrooms": return new[] { new Read(P(0, 64f, -10f), 20f), new Read(P(21f, 78f, 4f), 9f) };
                case "wardzero": return new[] { new Read(P(0, 74f, -8.2f), 16f), new Read(P(0, 99f, 0), 4f) };
                case "station": return new[] { new Read(P(0, 80f, -16f), 7f), new Read(P(0, 40f, -2f), 30f) };
                default: return new[] { new Read(P(-10f, 89f, 6f), 4f), new Read(P(0, 94f, 6f), 4f), new Read(P(10f, 86f, 6f), 4f) };
            }
        }

        private static void Identity(float[] m) { Array.Clear(m, 0, 12); m[0] = m[5] = m[10] = 1; }
        // A frame from its local x and z axes (unit, level), a translation and the scales along x and z.
        private static void Frame(V3 x, V3 z, V3 t, float sx, float sz, float[] m)
        {
            m[0] = x.x * sx; m[1] = 0; m[2] = z.x * sz; m[3] = t.x;
            m[4] = x.y * sx; m[5] = 1; m[6] = z.y * sz; m[7] = t.y;
            m[8] = x.z * sx; m[9] = 0; m[10] = z.z * sz; m[11] = t.z;
        }
        /// <summary>A local point through a piece's place.</summary>
        public static V3 Apply(float[] m, V3 p) => new V3(m[0] * p.x + m[1] * p.y + m[2] * p.z + m[3], m[4] * p.x + m[5] * p.y + m[6] * p.z + m[7],
            m[8] * p.x + m[9] * p.y + m[10] * p.z + m[11]);
    }
}
