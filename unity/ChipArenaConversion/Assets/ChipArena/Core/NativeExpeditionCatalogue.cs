using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expeditions screen's presentation catalogue. Every name and line the screen shows lives here, so the owner can rename
    /// them in one place.
    /// <list type="bullet">
    /// <item><b>The run</b> (owner 2026-11-06: "The run is now randomized based on the slot machines ... no point in selecting
    /// different expeditions as its a all-in-one thing"). There is one expedition. Its <see cref="All"/> entry stays because the
    /// Deployment Station reads it (Core ExpeditionDeploymentRoom: the selected id and its live areas are the Instance
    /// Machine's authored pool and part of the run's compatibility digest), so its id and areas must not change. The screen
    /// shows no map and no route for it: the Instance Machine rolls the destinations at the station.</item>
    /// <item><b>The boss roster</b> (owner 2026-11-06: "remove the Wave Defense option, and add boss battle option ... to test
    /// bosses"). Each entry reads its name, card lines and move names from the boss's own Core table, so the screen never drifts
    /// from the fight. <see cref="BossFight"/> is the honest state: what can start from this screen today.</item>
    /// </list>
    /// Engine-free; the managed checks hold the catalogue's shape and its ids against the layout, the maps and the boss tables.
    /// </summary>
    public static class NativeExpeditionCatalogue
    {
        /// <summary>Live: built and playable in the current build. InDevelopment: named, not built. Uncharted: hidden.</summary>
        public enum AreaState{Live,InDevelopment,Uncharted}
        public sealed class Area
        {
            /// <summary>Display name (caps) and its place line.</summary>
            public string Name,Place;
            /// <summary>The area's map (a UnityMaps id), or null for an area with no map yet.</summary>
            public string Map;
            /// <summary>The layout's area id when the area is built (Resources/ExpeditionLayout.json), else null.</summary>
            public string LayoutArea;
            /// <summary>An honest one-line note under the area (what the current build does instead), or null.</summary>
            public string Note;
            public AreaState State;public bool Boss;
        }
        public sealed class Expedition
        {
            /// <summary>The id the Deployment Station is given (part of the run's compatibility digest: never rename it).</summary>
            public string Id;
            public string Title,Kicker,Tagline;
            /// <summary>The hub's name and its map.</summary>
            public string Hub,HubMap;
            /// <summary>The layout's world id this entry presents.</summary>
            public string LayoutWorld;
            /// <summary>Party size and a rough length, as shown.</summary>
            public string Party,Duration;
            /// <summary>The authored areas the Instance Machine may roll, then the final boss (Boss set).</summary>
            public Area[] Areas;
            /// <summary>Never startable (no entry is today; the station refuses a locked id).</summary>
            public bool Locked;
            /// <summary>The entry's accent, 0xRRGGBB.</summary>
            public int Accent;
            public int LiveAreas{get{int n=0;foreach(var a in Areas)if(a.State==AreaState.Live)n++;return n;}}
        }

        public const int PartyCap=4;
        public const string InDevelopment="IN DEVELOPMENT";
        public const string LiveLabel="LIVE";

        /// <summary>The one expedition. Its id, world and areas feed the Deployment Station (see the summary).</summary>
        public static readonly Expedition[] All=
        {
            new Expedition
            {
                Id="black-siren",Title="EXPEDITION",Kicker="PVE · CO-OP RUN",Tagline="Pull the machines, drop into a city built around your roll, clear three areas and their bosses, then the last fight.",
                Hub="Expedition City",HubMap="expedition-city",LayoutWorld="exp-city",Party="1–4 PILOTS",Duration="~25 MIN",Accent=0x7fe9ff,
                Areas=new[]
                {
                    new Area{Name="WARD ZERO",Place="Meridian Biocontainment",Map="expedition-ward-zero",LayoutArea="exp-ward-zero",State=AreaState.Live},
                    new Area{Name="CENTRAL STATION",Place="Central Station",Map="expedition-transit",LayoutArea="exp-transit",State=AreaState.Live},
                    new Area{Name="CITY IRONWORKS",Place="City Ironworks",Map="expedition-foundry",LayoutArea="exp-foundry",State=AreaState.Live},
                    new Area{Name="THE SIREN",Place="City Hall · the clock stopped at 6:47",State=AreaState.InDevelopment,Boss=true,Note="M1 fights her at the hub checkpoint"},
                },
            },
        };
        public static int Count=>All.Length;
        public static Expedition At(int index)=>All[Wrap(index)];
        public static int Wrap(int index){int n=All.Length;return ((index%n)+n)%n;}
        /// <summary>The run every Start Expedition deploys (the first playable entry).</summary>
        public static int Default{get{for(int i=0;i<All.Length;i++)if(!All[i].Locked)return i;return 0;}}
        public static Expedition Run=>All[Default];
        public static string StateLabel(AreaState s)=>s==AreaState.Live?LiveLabel:s==AreaState.InDevelopment?InDevelopment:"LOCKED";

        // ---- The run's briefing -----------------------------------------------------------------------------------------------
        /// <summary>A run has no fixed map to show: the machines roll it at the Deployment Station.</summary>
        public const string Generated="ROLLED AT THE DEPLOYMENT STATION";
        public const string SampleSpin="SAMPLE SPIN · YOUR ROLL HAPPENS AT THE STATION";
        public const string InstanceMachine="INSTANCE MACHINE",MutatorMachine="MUTATOR MACHINE",MutatorOptional="OPTIONAL";
        /// <summary>The Instance Machine's reels, left to right, as the station labels them (Core NativeDeployPresentation).</summary>
        public static readonly string[] Entrances={"EAST","WEST","SOUTH"};
        public sealed class Step{public string Num,Name,Line,Short;}
        /// <summary>How a run goes, in order (the screen's numbered steps).</summary>
        public static readonly Step[] Steps=
        {
            new Step{Num="01",Name="DEPLOYMENT STATION",Line="Pull the Instance Machine for three destinations. Mutators are optional.",Short="Roll three destinations"},
            new Step{Num="02",Name="THE CITY",Line="Generated around your roll: the hub, its machines and the revive.",Short="Built around your roll"},
            new Step{Num="03",Name="THREE AREAS",Line="Fill each area's kill quota, then beat the boss waiting at its end.",Short="Quota, then its boss"},
            new Step{Num="04",Name="THE LAST FIGHT",Line="Every area cleared, the final boss is all that stands.",Short="The final boss"},
        };

        // ---- Boss Battle ------------------------------------------------------------------------------------------------------
        /// <summary>
        /// What Boss Battle can start for a boss today. <b>Direct</b>: straight into its fight from this screen (the Siren: the
        /// boss-test match started at her, solo with bots or a co-op room). <b>InRun</b>: its rules are live but it is only fought
        /// inside a run, at the end of its area; a direct start waits on Astra (docs/EXPEDITIONS-MENU.md, "Boss Battle direct
        /// starts"). <b>Preview</b>: presentation only, behind its DEV preview, until its rules are bound.
        /// </summary>
        public enum BossFight{Direct,InRun,Preview}
        public sealed class Boss
        {
            /// <summary>Presentation id (stable for the screen's state and its emblem; not sent anywhere).</summary>
            public string Id;
            /// <summary>The plate name (caps), the line under it, the eyebrow over it and the intro card's tag line.</summary>
            public string Name,Subtitle,Eyebrow,Line;
            /// <summary>Where it is fought (caps) and one sentence about it.</summary>
            public string Area,Blurb;
            /// <summary>The Instance Machine destination whose area it guards (InRun), else null.</summary>
            public string Destination;
            /// <summary>Phases, and its moves as the fight names them (from the boss's own table).</summary>
            public int Phases;public string[] Moves;
            public BossFight Fight;
            public int Accent;
            /// <summary>Its emblem: markup on a 100-unit box, white with near-black cut-outs (tinted by the draw), and its texture
            /// cache key; null for the Siren, whose face is the lobby's (Runtime NativeLobbyUI SirenFace).</summary>
            public string Emblem,EmblemKey;
        }
        private static string[] Titles<T>(T[] moves,Func<T,string> title){var r=new string[moves.Length];for(int i=0;i<r.Length;i++)r[i]=title(moves[i]);return r;}
        private static string[] ConverterMoves()
        {
            // THE CONVERTER's table names its attacks by id (codes 1-8); its scripted moments (20+) are not moves.
            var list=new System.Collections.Generic.List<string>();
            foreach(var a in NativeIronworksBoss.Actions)if(a.Code>0&&a.Code<20)list.Add(char.ToUpperInvariant(a.Id[0])+a.Id.Substring(1));
            return list.ToArray();
        }
        // The emblems (owner 2026-11-06 boss battle): THE 6:47's carriage nose under its clock stopped at 6:47; Vessel C on its
        // trunnions, pouring; THE HUM's ceiling panel buzzing over a long pale face; THE DEEP END's drain under the surface;
        // NOCTIEL's wings about a falling sword and a broken halo.
        public const string Emblem647="<circle cx=\"50\" cy=\"24\" r=\"16\" fill=\"#2a2a2a\"/><circle cx=\"50\" cy=\"24\" r=\"16\" fill=\"none\" stroke=\"#fff\" stroke-width=\"3\"/><path d=\"M50 13 V15.5 M61 24 H58.5 M50 35 V32.5 M39 24 H41.5\" fill=\"none\" stroke=\"#9a9a9a\" stroke-width=\"1.6\"/><path d=\"M50 24 L46.6 31.8 M50 24 L37.6 21.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.6\"/><circle cx=\"50\" cy=\"24\" r=\"2.2\" fill=\"#fff\"/>"
            +"<path d=\"M20 94 V60 Q20 46 34 44 H66 Q80 46 80 60 V94 Z\" fill=\"#fff\"/><rect x=\"28\" y=\"51\" width=\"44\" height=\"15\" rx=\"3\" fill=\"#07070d\"/><rect x=\"31\" y=\"53.5\" width=\"38\" height=\"10\" rx=\"2\" fill=\"#3c3c3c\"/><circle cx=\"32\" cy=\"78\" r=\"6\" fill=\"#07070d\"/><circle cx=\"68\" cy=\"78\" r=\"6\" fill=\"#07070d\"/><circle cx=\"32\" cy=\"78\" r=\"3.2\" fill=\"#fff\"/><circle cx=\"68\" cy=\"78\" r=\"3.2\" fill=\"#fff\"/><path d=\"M42 72 H58 M42 78 H58 M42 84 H58\" fill=\"none\" stroke=\"#07070d\" stroke-width=\"2.4\"/><rect x=\"14\" y=\"90\" width=\"72\" height=\"6\" rx=\"1\" fill=\"#fff\"/>";
        public const string EmblemConverter="<path d=\"M38 18 L60 16 L66 26 Q82 38 80 58 Q77 78 56 82 Q36 85 28 68 Q20 50 32 32 Z\" fill=\"#fff\"/><path d=\"M23 50 L82 44 L83 54 L24 60 Z\" fill=\"#07070d\"/><path d=\"M25 52.6 L81 47 L81.6 51.4 L25.6 57 Z\" fill=\"#6a6a6a\"/><circle cx=\"20\" cy=\"55\" r=\"6.5\" fill=\"#fff\"/><circle cx=\"86\" cy=\"49\" r=\"6.5\" fill=\"#fff\"/><circle cx=\"20\" cy=\"55\" r=\"2.6\" fill=\"#07070d\"/><circle cx=\"86\" cy=\"49\" r=\"2.6\" fill=\"#07070d\"/>"
            +"<path d=\"M40 19 Q22 20 16 36 Q12 48 13 62\" fill=\"none\" stroke=\"#e0e0e0\" stroke-width=\"4.6\"/><circle cx=\"13\" cy=\"72\" r=\"3\" fill=\"#d0d0d0\"/><circle cx=\"16\" cy=\"81\" r=\"2\" fill=\"#a0a0a0\"/><path d=\"M34 82 L24 97 M66 80 L78 97 M20 97 H82\" fill=\"none\" stroke=\"#fff\" stroke-width=\"4\"/><path d=\"M50 66 H66 M54 72 H64\" fill=\"none\" stroke=\"#07070d\" stroke-width=\"2.6\"/>";
        public const string EmblemHum="<rect x=\"12\" y=\"8\" width=\"76\" height=\"22\" rx=\"2\" fill=\"#3a3a3a\"/><rect x=\"12\" y=\"8\" width=\"76\" height=\"22\" rx=\"2\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2\"/><rect x=\"18\" y=\"13\" width=\"64\" height=\"4\" rx=\"2\" fill=\"#fff\"/><rect x=\"18\" y=\"21\" width=\"64\" height=\"4\" rx=\"2\" fill=\"#fff\"/>"
            +"<path d=\"M10 42 Q16 37 22 42 T34 42 M66 42 Q72 37 78 42 T90 42\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.6\"/><path d=\"M6 54 Q12 49 18 54 T30 54 M70 54 Q76 49 82 54 T94 54\" fill=\"none\" stroke=\"#8a8a8a\" stroke-width=\"2.2\"/><path d=\"M2 66 Q8 61 14 66 T26 66 M74 66 Q80 61 86 66 T98 66\" fill=\"none\" stroke=\"#4a4a4a\" stroke-width=\"2\"/>"
            +"<path d=\"M50 36 C64 36 68 50 66 66 C64 82 58 96 50 96 C42 96 36 82 34 66 C32 50 36 36 50 36 Z\" fill=\"#fff\"/><ellipse cx=\"43\" cy=\"58\" rx=\"3.2\" ry=\"6.2\" fill=\"#07070d\"/><ellipse cx=\"57\" cy=\"58\" rx=\"3.2\" ry=\"6.2\" fill=\"#07070d\"/><ellipse cx=\"50\" cy=\"80\" rx=\"4.6\" ry=\"9\" fill=\"#07070d\"/>";
        public const string EmblemDeepEnd="<path d=\"M6 20 Q14 13 22 20 T38 20 T54 20 T70 20 T86 20 T102 20\" fill=\"none\" stroke=\"#fff\" stroke-width=\"3.2\"/><path d=\"M6 30 Q14 23 22 30 T38 30 T54 30 T70 30 T86 30 T102 30\" fill=\"none\" stroke=\"#7a7a7a\" stroke-width=\"2.2\"/><circle cx=\"50\" cy=\"64\" r=\"30\" fill=\"#262626\"/>"
            +"<path d=\"M50 64 m-4 0 a4 4 0 1 1 8 0 a8 8 0 1 1 -16 0 a12 12 0 1 1 24 0 a16 16 0 1 1 -32 0 a20 20 0 1 1 40 0 a24 24 0 1 1 -48 0\" fill=\"none\" stroke=\"#fff\" stroke-width=\"3.6\"/><circle cx=\"50\" cy=\"64\" r=\"3.4\" fill=\"#fff\"/>";
        public const string EmblemNoctiel="<path d=\"M46 42 C34 28 16 24 4 32 C12 36 16 40 18 46 C10 46 5 52 5 58 C13 54 21 56 25 60 C19 64 17 70 19 77 C29 69 39 63 46 58 Z\" fill=\"#fff\"/><path d=\"M54 42 C66 28 84 24 96 32 C88 36 84 40 82 46 C90 46 95 52 95 58 C87 54 79 56 75 60 C81 64 83 70 81 77 C71 69 61 63 54 58 Z\" fill=\"#fff\"/>"
            +"<path d=\"M22 47 C30 47 38 50 44 54 M21 61 C29 61 37 62 44 58.5 M78 47 C70 47 62 50 56 54 M79 61 C71 61 63 62 56 58.5\" fill=\"none\" stroke=\"#07070d\" stroke-width=\"2\"/><path d=\"M50 20 L55.5 31 L55.5 85 L50 98 L44.5 85 L44.5 31 Z\" fill=\"#07070d\"/><path d=\"M50 22 L54 32 L54 84 L50 96 L46 84 L46 32 Z\" fill=\"#fff\"/><rect x=\"35\" y=\"29\" width=\"30\" height=\"5\" rx=\"1\" fill=\"#fff\"/><path d=\"M50 35 V84\" fill=\"none\" stroke=\"#8a8a8a\" stroke-width=\"1.4\"/>"
            +"<path d=\"M36.5 14 A14 4.5 0 0 1 60 10.4 M63.6 13.2 A14 4.5 0 0 1 52 18.4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.4\"/>";
        /// <summary>The roster in screen order: the boss that can be fought now first, then the run's bosses, then in development.</summary>
        public static readonly Boss[] Bosses=Keyed(new[]
        {
            new Boss{Id="siren",Name="THE SIREN",Subtitle="WARD ZERO'S SINGER",Eyebrow="BOSS · WARD ZERO",Line="SHE IS STILL SINGING",Area="WARD ZERO",
                Blurb="The thing that sang through Ward Zero. Four chips and one song, and every run ends at her checkpoint.",
                Phases=4,Moves=new[]{"Hellfire","Damnation","Fire Rain","Seekers","The Grab"},Fight=BossFight.Direct,Accent=0xb7a8ff},
            new Boss{Id="station",Emblem=Emblem647,Name=NativeStationBoss.Name,Subtitle=NativeStationBoss.Subtitle,Eyebrow="BOSS · CENTRAL STATION",Line="THE LAST TRAIN NEVER LEFT",Area="CENTRAL STATION",
                Blurb="The evacuation special, everyone aboard fused into it, walking on its wheels with the Stationmaster on its roof.",
                Destination=NativeStationBoss.Area,Phases=3,Moves=Titles(NativeStationBoss.Moves,m=>m.Title),Fight=BossFight.Direct,Accent=0x3fd8ff},
            new Boss{Id="converter",Emblem=EmblemConverter,Name=NativeIronworksBoss.PlateName,Subtitle=NativeIronworksBoss.Stencil,Eyebrow="BOSS · CITY IRONWORKS",Line="THE MELT NEVER COOLS",Area="CITY IRONWORKS",
                Blurb="A Bessemer converter walking on its trunnion yoke, the dead foreman still working the pulpit.",
                Destination="exp-foundry",Phases=3,Moves=ConverterMoves(),Fight=BossFight.Direct,Accent=0xff8a2a},
            new Boss{Id="hum",Emblem=EmblemHum,Name=NativeHumBoss.Name,Subtitle="THE BACKROOMS",Eyebrow=NativeHumBoss.Card.Eyebrow,Line=NativeHumBoss.Card.Line,Area="LEVEL 0",
                Blurb="The buzz in the ceiling, given a body. It follows sound through the yellow rooms.",
                Destination=NativeHumBoss.Area,Phases=3,Moves=Titles(NativeHumBoss.Moves,m=>m.Title),Fight=BossFight.Direct,Accent=0xffe680},
            new Boss{Id="deepend",Emblem=EmblemDeepEnd,Name=NativeDeepEndBoss.Name,Subtitle="THE POOLROOMS",Eyebrow=NativeDeepEndBoss.Card.Eyebrow,Line=NativeDeepEndBoss.Card.Line,Area="POOLROOMS",
                Blurb="Something under the tiles that pulls the water, and you with it, toward the drain.",
                Destination=NativeDeepEndBoss.Area,Phases=3,Moves=Titles(NativeDeepEndBoss.Moves,m=>m.Title),Fight=BossFight.Direct,Accent=0x6fe8e8},
            new Boss{Id="noctiel",Emblem=EmblemNoctiel,Name=NativeFallenBoss.Name,Subtitle="THE FALLEN ANGEL",Eyebrow=NativeFallenBoss.Card.Eyebrow,Line=NativeFallenBoss.Card.Line,Area="FINAL BOSS",
                Blurb="A possible last fight: five lives, four phases and a blade that cuts the screen. Built, not yet bound to rules.",
                Phases=4,Moves=Titles(NativeFallenBoss.Moves,m=>m.Title),Fight=BossFight.Preview,Accent=0xe4e4f0},
        });
        private static Boss[] Keyed(Boss[] all){foreach(var b in all)b.EmblemKey="exp-boss-"+b.Id;return all;}
        public static int BossCount=>Bosses.Length;
        public static int WrapBoss(int index){int n=Bosses.Length;return ((index%n)+n)%n;}
        public static Boss BossAt(int index)=>Bosses[WrapBoss(index)];
        /// <summary>The boss a room fights (no room field carries a pick, so a guest sees this one): the first Direct.</summary>
        public static int DefaultBoss{get{for(int i=0;i<Bosses.Length;i++)if(Bosses[i].Fight==BossFight.Direct)return i;return 0;}}
        /// <summary>The roster's state chip and a short one for a small tile.</summary>
        public static string FightLabel(BossFight f)=>f==BossFight.Direct?"FIGHT NOW":f==BossFight.InRun?"IN RUNS":"IN DEVELOPMENT";
        public static string FightShort(BossFight f)=>f==BossFight.Direct?"READY":f==BossFight.InRun?"IN RUNS":"IN DEV";
        /// <summary>Why a boss cannot start here (null when it can): honest, and what to do instead.</summary>
        public static string FightReason(Boss b)=>b.Fight==BossFight.Direct?null:b.Fight==BossFight.InRun?
            "Fought inside a run for now: roll "+DestinationName(b.Destination)+" at the Instance Machine. A direct start is next.":
            "In development: its fight is built, its rules are not bound yet (DEV preview only).";
        /// <summary>A destination's display name as the station's reels show it (Core NativeDeployPresentation).</summary>
        public static string DestinationName(string id)=>NativeDeployPresentation.DestinationOf(id,null).Name;
        /// <summary>The boss guarding a destination (the run's area bosses), or null.</summary>
        public static Boss BossOf(string destination)
        {
            if(destination=="exp-ward-zero")return Bosses[DefaultBoss];
            foreach(var b in Bosses)if(b.Destination!=null&&b.Destination==destination)return b;return null;
        }
    }
}
