using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The tileset registry (docs/EXPEDITION-OVERHAUL.md R1, W2 and A3): one entry per tileset the Deployment Station's ATLAS
    /// departures board can show (Core NativeAtlasBoard). A shared Core contract file:
    /// <list type="bullet">
    /// <item><b>Display fields (Claude, M1, 2026-11-18):</b> name, place, accent, emblem and the board row (enemies, the instance
    /// count, the final). The board draws every row from these through its one split-flap glyph atlas, so a new tileset's row
    /// needs only its entry here (and, optionally, an emblem): no new code and no baked card art.</item>
    /// <item><b>Rules fields (Astra, A3):</b> the generator and its Version, look, bank families, the pool [{instance, theme}], the
    /// hub enemy table, the mutator pool, finalBosses, ambience and relief profiles. None exist yet.</item>
    /// </list>
    /// Presentation only today: no rule, ticket, digest or wire reads this file. The expedition id stays <c>black-siren</c> and
    /// every run is City (Q3, Q5): <see cref="Selected"/> is the hook A3's SelectTileset fills, and returns City until then.
    /// </summary>
    public static class NativeTilesets
    {
        /// <summary>Live: players can be deployed into it. Dev: DEV builds only (A3's proof tileset); never on a player's board.</summary>
        public enum State{Live,Dev}
        public sealed class Tileset
        {
            /// <summary>The registry id (City is <see cref="City"/>). A3 puts it in the state and on the ticket; today nothing sends it.</summary>
            public string Id;public State State;public WeaponPool WeaponPool;
            // ---- display (Claude) ----
            /// <summary>The name and its place (caps, in the station's stroke font): "CITY", "MERIDIAN".</summary>
            public string Name,Place;
            /// <summary>The colour its row lights in, 0xRRGGBB.</summary>
            public int Accent;
            /// <summary>Its emblem: markup on a 100-unit box, white with knockout cut-outs (NativeIconRaster Art), rasterised into the
            /// board's glyph atlas; null draws a blank emblem flap.</summary>
            public string Emblem;
            /// <summary>The board's ENEMIES and FINAL cells (caps, in the drum's characters).</summary>
            public string Enemies,Final;
            /// <summary>The instances its Instance Machine draws from, as the board counts them (display; managed checks hold it equal
            /// to the station's real pool). A3's rules pool replaces it.</summary>
            public string[] Instances;
            // ---- rules (Astra, A3): generator and Version, look, bank families, pool, hub enemy table, mutator pool, finalBosses,
            //      ambience, relief profiles ----
            /// <summary>"CITY · MERIDIAN": the TILESET cell, the console card and the Expeditions screen.</summary>
            public string Label=>Name+" · "+Place;
        }

        public const string City="city";
        /// <summary>The registry, live rows first (a board's rows follow this order).</summary>
        public static readonly Tileset[] All=
        {
            new Tileset
            {
                Id=City,WeaponPool=WeaponPool.Ranged,State=State.Live,Name="CITY",Place="MERIDIAN",Accent=0xffc65a,Enemies="THE INFECTED",Final=NativeFallenBoss.Name,
                Instances=new[]{"exp-ward-zero","exp-transit","exp-foundry",NativeBackroomsGenerator.AreaId,NativePoolroomsGenerator.AreaId},
                // Meridian's skyline round City Hall's tower, its clock stopped at 6:47 (docs/EXPEDITION-OVERHAUL.md N1).
                Emblem="<rect x=\"6\" y=\"60\" width=\"17\" height=\"33\" fill=\"#fff\"/><rect x=\"21\" y=\"46\" width=\"15\" height=\"47\" fill=\"#fff\"/>"
                    +"<path d=\"M39 93 V31 L50 12 L61 31 V93 Z\" fill=\"#fff\"/><rect x=\"64\" y=\"52\" width=\"14\" height=\"41\" fill=\"#fff\"/><rect x=\"77\" y=\"64\" width=\"17\" height=\"29\" fill=\"#fff\"/>"
                    +"<circle cx=\"50\" cy=\"41\" r=\"7.6\" fill=\"#000\" data-knockout=\"1\"/><circle cx=\"50\" cy=\"41\" r=\"7.6\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.4\"/>"
                    +"<line x1=\"50\" y1=\"41\" x2=\"43.6\" y2=\"39.6\" stroke=\"#fff\" stroke-width=\"1.7\"/><line x1=\"50\" y1=\"41\" x2=\"48.2\" y2=\"45.1\" stroke=\"#fff\" stroke-width=\"2.3\"/>"
                    +"<rect x=\"10\" y=\"66\" width=\"4\" height=\"5\" fill=\"#000\" data-knockout=\"1\"/><rect x=\"16\" y=\"74\" width=\"4\" height=\"5\" fill=\"#000\" data-knockout=\"1\"/>"
                    +"<rect x=\"25\" y=\"53\" width=\"4\" height=\"5\" fill=\"#000\" data-knockout=\"1\"/><rect x=\"29\" y=\"66\" width=\"4\" height=\"5\" fill=\"#000\" data-knockout=\"1\"/>"
                    +"<rect x=\"45\" y=\"58\" width=\"4\" height=\"6\" fill=\"#000\" data-knockout=\"1\"/><rect x=\"52\" y=\"70\" width=\"4\" height=\"6\" fill=\"#000\" data-knockout=\"1\"/>"
                    +"<rect x=\"68\" y=\"58\" width=\"4\" height=\"5\" fill=\"#000\" data-knockout=\"1\"/><rect x=\"82\" y=\"70\" width=\"4\" height=\"5\" fill=\"#000\" data-knockout=\"1\"/>"
                    +"<rect x=\"3\" y=\"92\" width=\"94\" height=\"4\" fill=\"#fff\"/>",
            },
        };
        /// <summary>The board's lines (rows) and columns. A line with no tileset reads <see cref="NotInService"/> in its TILESET
        /// column and nothing else: no invented names and no dates (Q3).</summary>
        public const int Lines=4;
        public static readonly string[] Columns={"TILESET","ENEMIES","INSTANCES","FINAL"};
        public const string NotInService="NOT IN SERVICE";
        /// <summary>The Expeditions screen's line (and the camp's war board's, W5).</summary>
        public static readonly string MenuLine="TILESET: "+All[0].Label;

        /// <summary>The entry for `id` (null and unknown ids read as City, as A3's state does).</summary>
        public static Tileset Of(string id){foreach(var t in All)if(t.Id==id)return t;return All[0];}
        /// <summary>How many tilesets players can be deployed into.</summary>
        public static int Live{get{int n=0;foreach(var t in All)if(t.State==State.Live)n++;return n;}}
        /// <summary>
        /// The tileset this station's run is in: the presentation's one hook for the choice (the board's lit line, the console's
        /// TILESET card). A3 (Astra, SelectTileset): return Of(state?.tileset) once ExpeditionDeploymentState carries it (null reads
        /// as City). Until then every run of the catalogue's expedition (NativeExpeditionCatalogue.Run, <c>black-siren</c>) is City,
        /// whatever the state says: the presentation shows the state and never decides it.
        /// </summary>
        public static Tileset Selected(ExpeditionDeploymentState state)=>Of(City);
        /// <summary>A board cell's text: the tileset's TILESET, ENEMIES, INSTANCES or FINAL (column 0..3); a line with no tileset
        /// (null) is NOT IN SERVICE in its TILESET column and blank elsewhere.</summary>
        public static string Cell(Tileset t,int column)
        {
            if(t==null)return column==0?NotInService:"";
            switch(column)
            {
                case 0:return t.Label;
                case 1:return t.Enemies??"";
                // A run rolls InstanceCount destinations from the tileset's pool (owner, 2026-11-19: "Dont we only roll 3 instances + 1
                // final boss?"), so the cell says what a run holds, not the pool's size.
                case 2:{int n=(t.Instances?.Length??0)==0?0:Math.Min(t.Instances.Length,NativeExpeditionDeployment.InstanceCount);return n==1?"1 INSTANCE":n+" INSTANCES";}
                case 3:return t.Final??"";
                default:return "";
            }
        }
        /// <summary>The tileset on each board line for a selection: the selected one on the large first line (whatever its state:
        /// the board shows the state, so A3's DEV proof tileset shows when a DEV run selects it), then the other live tilesets in
        /// registry order, then empty lines (null); a dev tileset is never one of the other lines. Fills `into` (Lines long).</summary>
        public static Tileset[] Board(Tileset selected,Tileset[] into)
        {
            if(into==null||into.Length<Lines)into=new Tileset[Lines];
            selected=selected??Of(City);into[0]=selected;int n=1;
            foreach(var t in All)if(n<Lines&&t.State==State.Live&&!ReferenceEquals(t,selected))into[n++]=t;
            while(n<Lines)into[n++]=null;
            return into;
        }
    }
}
