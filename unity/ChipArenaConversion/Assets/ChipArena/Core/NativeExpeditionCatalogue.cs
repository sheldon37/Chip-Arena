using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expeditions screen's presentation catalogue (owner 2026-10-15: "There will be different expeditions ... (3 zombie / evil
    /// spirit themed areas + 1 final boss)"; "for now we can use placeholders and our current one expedition"). Every name, line
    /// and placeholder the screen shows lives here, so the owner can rename them in one place. Nothing here reaches the game: the
    /// ids the runtime uses stay the layout's (<see cref="Expedition.LayoutWorld"/>, <see cref="Area.LayoutArea"/>, the map ids
    /// in Resources/ExpeditionLayout.json and UnityMaps.json). A placeholder expedition is <see cref="Expedition.Locked"/>: drawn
    /// as a silhouette with COMING SOON and never startable. Engine-free; the managed checks hold the catalogue's shape.
    /// </summary>
    public static class NativeExpeditionCatalogue
    {
        /// <summary>Live: built and playable in the current build. InDevelopment: named, not built. Uncharted: a placeholder's hidden area.</summary>
        public enum AreaState{Live,InDevelopment,Uncharted}
        public sealed class Area
        {
            /// <summary>Display name (caps) and its place line.</summary>
            public string Name,Place;
            /// <summary>The map drawn as the area's scan (a UnityMaps id), or null for an area with no map yet.</summary>
            public string Map;
            /// <summary>The layout's area id when the area is built (Resources/ExpeditionLayout.json), else null.</summary>
            public string LayoutArea;
            /// <summary>An honest one-line note under the area (what the current build does instead), or null.</summary>
            public string Note;
            public AreaState State;public bool Boss;
        }
        public sealed class Expedition
        {
            /// <summary>Presentation id (stable for the screen's state; not sent anywhere).</summary>
            public string Id;
            /// <summary>Title (caps), the line over it and the tag line under it.</summary>
            public string Title,Kicker,Tagline;
            /// <summary>The hub's name and its map (the key art's backdrop scan).</summary>
            public string Hub,HubMap;
            /// <summary>The layout's world id this entry presents (null for a placeholder).</summary>
            public string LayoutWorld;
            /// <summary>Party size and a rough length, as shown.</summary>
            public string Party,Duration;
            public string[] Threats;
            /// <summary>Three areas, then the final boss (Boss set), in route order.</summary>
            public Area[] Areas;
            /// <summary>A placeholder: silhouette art, COMING SOON, never startable.</summary>
            public bool Locked;
            /// <summary>The entry's accent, 0xRRGGBB.</summary>
            public int Accent;
            /// <summary>Whether Wave Defense (the Ward Zero waves and the Siren, the boss-test match) belongs to this entry.</summary>
            public bool WaveDefense;
            public int LiveAreas{get{int n=0;foreach(var a in Areas)if(a.State==AreaState.Live)n++;return n;}}
        }

        public const int PartyCap=4;
        public const string WorkingTitle="WORKING TITLE";
        public const string ComingSoon="COMING SOON";
        public const string InDevelopment="IN DEVELOPMENT";
        public const string LiveLabel="LIVE";

        private static Area Uncharted(int i)=>new Area{Name="UNCHARTED",Place="Signal lost · area "+(i+1).ToString("00"),State=AreaState.Uncharted};
        private static Area UnknownBoss()=>new Area{Name="???",Place="Final boss · unknown",State=AreaState.Uncharted,Boss=true};

        /// <summary>The expeditions in carousel order; the first is the default pick.</summary>
        public static readonly Expedition[] All=
        {
            new Expedition
            {
                Id="black-siren",Title="BLACK SIREN",Kicker="EXPEDITION 01",Tagline="The city stopped at 6:47. Whatever sang through Ward Zero is still out there.",
                Hub="Expedition City",HubMap="expedition-city",LayoutWorld="exp-city",Party="1–4 PILOTS",Duration="~25 MIN",Threats=new[]{"UNDEAD","SPIRITS"},Accent=0xb7a8ff,WaveDefense=true,
                Areas=new[]
                {
                    new Area{Name="WARD ZERO",Place="Meridian Biocontainment",Map="expedition-ward-zero",LayoutArea="exp-ward-zero",State=AreaState.Live},
                    new Area{Name="CENTRAL STATION",Place="The boarded transit hub",State=AreaState.InDevelopment},
                    new Area{Name="CITY IRONWORKS",Place="The sealed foundry",State=AreaState.InDevelopment},
                    new Area{Name="THE SIREN",Place="City Hall · the clock stopped at 6:47",State=AreaState.InDevelopment,Boss=true,Note="M1 fights her at the hub checkpoint"},
                },
            },
            new Expedition
            {
                Id="hollow-choir",Title="HOLLOW CHOIR",Kicker="EXPEDITION 02",Tagline="A drowned cathedral quarter where the choir never stopped singing.",
                Hub="Unknown",Party="1–4 PILOTS",Duration="—",Threats=new[]{"SPIRITS","WRAITHS"},Accent=0x6fe3c8,Locked=true,
                Areas=new[]{Uncharted(0),Uncharted(1),Uncharted(2),UnknownBoss()},
            },
            new Expedition
            {
                Id="ashen-parish",Title="ASHEN PARISH",Kicker="EXPEDITION 03",Tagline="The suburb burned for a week. The dead never noticed.",
                Hub="Unknown",Party="1–4 PILOTS",Duration="—",Threats=new[]{"UNDEAD","BURNERS"},Accent=0xff5c7a,Locked=true,
                Areas=new[]{Uncharted(0),Uncharted(1),Uncharted(2),UnknownBoss()},
            },
            new Expedition
            {
                Id="night-marrow",Title="NIGHT MARROW",Kicker="EXPEDITION 04",Tagline="Under the old quarter, the catacombs are counting the living.",
                Hub="Unknown",Party="1–4 PILOTS",Duration="—",Threats=new[]{"UNDEAD","SPIRITS"},Accent=0x8fb8ff,Locked=true,
                Areas=new[]{Uncharted(0),Uncharted(1),Uncharted(2),UnknownBoss()},
            },
        };
        public static int Count=>All.Length;
        /// <summary>The entry at <paramref name="index"/>, wrapping both ways (the carousel loops).</summary>
        public static Expedition At(int index)=>All[Wrap(index)];
        public static int Wrap(int index){int n=All.Length;return ((index%n)+n)%n;}
        /// <summary>The playable entry the screen starts on, and the one a room plays (no room field carries a pick yet).</summary>
        public static int Default{get{for(int i=0;i<All.Length;i++)if(!All[i].Locked)return i;return 0;}}
        public static string StateLabel(AreaState s)=>s==AreaState.Live?LiveLabel:s==AreaState.InDevelopment?InDevelopment:"LOCKED";
        /// <summary>The state on a short route row (a compact card).</summary>
        public static string ShortLabel(AreaState s)=>s==AreaState.Live?LiveLabel:s==AreaState.InDevelopment?"IN DEV":"LOCKED";
    }
}
