using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The five M1 stages' presentation (docs/arenas/NEW-STAGES.md "Stage looks (presentation)"): Breakwater, Pier 9, Skyframe,
    /// Deepwell and Polaris Station. Shared here: which maps these are, the kit kinds their static detail (Core NativeNewStageKit)
    /// and hazard rigs (Core NativeNewStageRig) draw with, resolved per look from MapLooks.json `kitLooks` ("stage-kit-*" ids;
    /// an unlisted kind takes <see cref="Defaults"/>), and the few places the scenery, the rigs and the effects agree on (the
    /// sea's level, the lighthouse, the flare, the radome's dome). Presentation only: none of this is sim data, collides,
    /// damages or enters the network frame. Pure.
    /// </summary>
    public static class NativeNewStageLooks
    {
        public const string Breakwater="breakwater",Harbor="harbor",Skyframe="skyframe",Rig="rig",Polar="polar";
        public static readonly string[] Maps={Breakwater,Harbor,Skyframe,Rig,Polar};
        /// <summary>The look each stage names (UnityMaps.json `look`; MapLooks.json carries one entry for each).</summary>
        public static readonly string[] Looks={"breakwater-blue-hour","harbor-night","skyframe-high-noon","rig-storm","polar-aurora"};
        /// <summary>The stage's index in <see cref="Maps"/>, or -1 for every other map (whose presentation never changes here).</summary>
        public static int Index(string mapId){for(int i=0;i<Maps.Length;i++)if(Maps[i]==mapId)return i;return -1;}
        public static bool Active(string mapId)=>Index(mapId)>=0;

        /// <summary>Kit kinds (index = kind), resolved once per look through `kitLooks`; at most NativeFactoryKit.Kinds.Length,
        /// as NativeFactoryKit.Looks holds them.</summary>
        public static readonly string[] Kinds={"stage-kit-dark","stage-kit-steel","stage-kit-crane","stage-kit-crane-dark","stage-kit-stripe","stage-kit-lamp","stage-kit-glass",
            "stage-kit-frame","stage-kit-cable","stage-kit-load","stage-kit-paint","stage-kit-paint-zone","stage-kit-concrete","stage-kit-timber","stage-kit-grate","stage-kit-rail",
            "stage-kit-pipe","stage-kit-pipe-hot","stage-kit-white","stage-kit-red","stage-kit-rubber","stage-kit-weight","stage-kit-snow","stage-kit-frost","stage-kit-door",
            "stage-kit-sign","stage-kit-wheel","stage-kit-burner","stage-kit-panel","stage-kit-glass-dark","stage-kit-light","stage-kit-crane-alt"};
        public const int Dark=0,Steel=1,Crane=2,CraneDark=3,Stripe=4,Lamp=5,Glass=6,Frame=7,Cable=8,Load=9,Paint=10,PaintZone=11,Concrete=12,Timber=13,Grate=14,Rail=15,
            Pipe=16,PipeHot=17,White=18,Red=19,Rubber=20,Weight=21,Snow=22,Frost=23,Door=24,Sign=25,Wheel=26,Burner=27,Panel=28,GlassDark=29,Light=30,CraneAlt=31;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it.</summary>
        public static readonly (string look,int color)[] Defaults={("plain",0x1C1D20),("steel-green",0x4A5056),("steel-green",0xE0A21E),("plain",0x2E3034),("demolition",0),("neon",0xFFB040),
            ("window-lit",0xFFC88A),("plain",0x2A2C30),("plain",0x1A1A1C),("corrugated",0xC4502A),("plain",0xD8D4C8),("plain",0xE0B020),("concrete-board",0x9A968C),("timber",0),
            ("factory-grating",0x7A8086),("plain",0xE8B820),("steel-green",0x6A7076),("steel-green",0x5A4A40),("plain",0xE6E6E0),("plain",0xB8322A),("plain",0x18181A),
            ("concrete-board",0x8C8880),("plain",0xE8EEF4),("steel-green",0xB8C4CC),("corrugated",0x5A626A),("plain",0xE8E4D8),("plain",0x1E1E20),("plain",0x3A3430),("steel-green",0x8C949A),
            ("window-lit",0x40546A),("neon",0xFFE6B0),("steel-green",0xD6D4CC)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "stage-kit-*" id wins; else the default).</summary>
        public static NativeFactoryKit.Looks Resolve(MapLookBlock[] kitLooks)
        {
            var l=new NativeFactoryKit.Looks();
            for(int k=0;k<Kinds.Length;k++)
            {
                var c=NativeBlockLooks.Resolve(kitLooks,null,Kinds[k],"",0xFFFFFF);
                if(!c.Listed){var d=Defaults[k];c=new NativeBlockLooks.Choice{Look=NativeBlockLooks.Id(d.look),Color=d.color!=0?d.color:Math.Max(0,NativeBlockLooks.Palette[NativeBlockLooks.Id(d.look)])};}
                l.Look[k]=c.Look;l.Color[k]=c.Color;l.Flags[k]=c.Flags&~(NativeBlockLooks.Band|NativeBlockLooks.Tag);
            }
            return l;
        }

        // ---------------- the places the scenery, rigs and effects share ----------------
        /// <summary>The sea's mean level under each stage (m; NaN: no sea). Breakwater's water stands 1.6 m under the pier head's
        /// deck, Pier 9's basin at the yards' quay level (NativePackScenery.Water), Deepwell's storm sea 5 m under the cellar
        /// deck (a pilot who falls through a landing reaches it just before killY, -6). Skyframe's street lies 112 m down.</summary>
        public static double SeaLevel(int stage)=>stage==0?-1.6:stage==1?NativePackScenery.Water:stage==3?-5.0:double.NaN;
        /// <summary>Breakwater's lighthouse beyond the east parapet (outside the bounds): its base, the lantern's centre, the beam's
        /// turn (rad/s) and its two lamps' reach (m).</summary>
        public const double LighthouseX=31,LighthouseZ=0,LanternY=17.6,BeamTurn=.42,BeamReach=60;
        /// <summary>The lighthouse beam's heading (rad, from +z toward +x) at render time `t`: a slow, steady sweep.</summary>
        public static double BeamHeading(double t)=>(t*BeamTurn)%(2*Math.PI);
        /// <summary>Deepwell's flare tip, at the end of the boom leaning north from the platform (outside the bounds).</summary>
        public const double FlareX=-4,FlareY=34,FlareZ=58;
        /// <summary>Polaris's radome: the dome over the radome block's top (the block's 11 m roof is unstandable).</summary>
        public const double DomeX=0,DomeY=11,DomeZ=0,DomeRadius=4.2;
        /// <summary>A stable 0..1 hash of three integers (the stages' seeds).</summary>
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+7919,b+31,c+61);
    }
}
