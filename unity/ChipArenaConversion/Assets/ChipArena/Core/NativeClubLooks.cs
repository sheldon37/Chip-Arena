using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Substation 128 (map `club`, look `club-substation`; docs/arenas/CLUB.md): what the club's presentation shares. Which map
    /// this is, the kit kinds its static detail (Core NativeClubKit) and rigs (Core NativeClubRig) draw with (resolved per look
    /// from MapLooks.json `kitLooks`, "club-kit-*" ids; an unlisted kind takes <see cref="Defaults"/>), the 128 bpm bar grid
    /// the hazards keep (counted from the arm time, FightAt + 4 s, on the match clock: the same on every client) and the
    /// places the scenery, the kit, the rigs and the effects agree on (the dance floor's tiles, the truss, the fixtures, the
    /// video cube, the LED wall, the speaker stacks). Presentation only: none of this is sim data, collides, damages or enters
    /// the network frame. Pure.
    /// </summary>
    public static class NativeClubLooks
    {
        public const string MapId="club",LookId="club-substation";
        public static bool Active(string mapId)=>mapId==MapId;

        // ---------------- the bar grid (128 bpm) ----------------
        public const double Bpm=128,Beat=60/Bpm,Bar=4*Beat,Phrase=16*Bar,ArmDelay=NativeFactoryHazards.ArmDelay;
        /// <summary>Beats since the arm time on the match clock while the fight (or its result) runs; else the render clock's
        /// beats (the floor keeps playing behind the countdown, an intro or the menu). The hazards keep this grid; the show steps
        /// on it so its telegraphs and its party patterns never drift apart.</summary>
        public static double Beats(NativePhase phase,double fightAt,double now,double render)
        =>phase==NativePhase.Fight||phase==NativePhase.Result?(now-fightAt-ArmDelay)/Beat:render/Beat;
        /// <summary>Whether the match clock owns the grid (the fight and its result).</summary>
        public static bool Fighting(NativePhase phase)=>phase==NativePhase.Fight||phase==NativePhase.Result;

        // ---------------- kit kinds ----------------
        /// <summary>Kit kinds (index = kind), resolved once per look through `kitLooks`.</summary>
        public static readonly string[] Kinds={"club-kit-dark","club-kit-metal","club-kit-chrome","club-kit-led","club-kit-screen","club-kit-cone","club-kit-glass","club-kit-velvet",
            "club-kit-wood","club-kit-truss","club-kit-bottle","club-kit-crowd","club-kit-mirror","club-kit-paint","club-kit-brass","club-kit-lens","club-kit-rubber","club-kit-tile",
            "club-kit-brick","club-kit-sign","club-kit-grille","club-kit-cable","club-kit-plant","club-kit-insulator","club-kit-steel","club-kit-hazard"};
        public const int Dark=0,Metal=1,Chrome=2,Led=3,Screen=4,Cone=5,Glass=6,Velvet=7,Wood=8,Truss=9,Bottle=10,Crowd=11,Mirror=12,Paint=13,Brass=14,Lens=15,Rubber=16,Tile=17,
            Brick=18,Sign=19,Grille=20,Cable=21,Plant=22,Insulator=23,Steel=24,Hazard=25;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it (0: the look's palette colour).</summary>
        public static readonly (string look,int color)[] Defaults={("plain",0x121016),("cyber-metal",0x34303E),("steel-green",0xC8CCD8),("cyber-tube",0xFFFFFF),("cyber-holo",0xFFFFFF),
            ("plain",0x0C0C0E),("cyber-glass",0x141A2C),("plain",0x5A1236),("timber",0x4A2618),("steel-green",0x2A2A30),("cyber-tube",0xFFB45E),("plain",0x07060A),("steel-green",0xE0E4F0),
            ("plain",0xD8D4E8),("steel-green",0xC8A040),("cyber-tube",0xFFFFFF),("plain",0x18181A),("cyber-tile",0x2A2534),("brick-red",0x5E3434),("cyber-tube",0xFF3DCF),("plain",0x1A1A20),
            ("plain",0x101012),("plain",0x1E4A2E),("plain",0x8A6A4A),("cyber-metal",0x4A4E5C),("demolition",0)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "club-kit-*" id wins; else the default, resolved
        /// through the same rules as an entry, so a Cyber City bank key gets its bank flag).</summary>
        public static NativeFactoryKit.Looks Resolve(MapLookBlock[] kitLooks)
        {
            var l=new NativeFactoryKit.Looks();
            for(int k=0;k<Kinds.Length;k++)
            {
                var c=NativeBlockLooks.Resolve(kitLooks,null,Kinds[k],"",0xFFFFFF);
                if(!c.Listed)c=NativeBlockLooks.Resolve(new[]{new MapLookBlock{block=Kinds[k],look=Defaults[k].look,color=Defaults[k].color}},null,Kinds[k],"",0xFFFFFF);
                l.Look[k]=c.Look;l.Color[k]=c.Color;l.Flags[k]=c.Flags&~(NativeBlockLooks.Band|NativeBlockLooks.Tag);
            }
            return l;
        }

        // ---------------- the places ----------------
        /// <summary>The LED dance floor (x0, x1, z0, z1) and its 1 m tiles (the sim's `club-floor-dance`, the grid hazards' and the
        /// laser wall's footprint).</summary>
        public const double FloorX0=-9,FloorX1=9,FloorZ0=-7.5,FloorZ1=7.5,TileSize=1;public const int TilesX=18,TilesZ=15,Tiles=TilesX*TilesZ;
        /// <summary>The lighting truss over the floor: its chords' height, half extents (corner towers at ±TrussX, ±TrussZ), and the
        /// fixtures hung under it.</summary>
        public const double TrussY=10.4,TrussX=9.8,TrussZ=8.2,TrussDepth=.42;
        /// <summary>The video cube (the sim's `club-cube`): centre and half size.</summary>
        public const double CubeY=6.7,CubeHalf=2.0,CubeHalfY=1.5;
        /// <summary>The two mirror balls hung either side of the cube from the centre truss.</summary>
        public const double BallX=6.2,BallY=7.4,BallZ=0,BallRadius=.55;
        /// <summary>The LED wall (the face of `club-wall-n` behind the stage) and the stage deck.</summary>
        public const double WallZ=19.6,WallX=10.8,WallY0=1.0,WallY1=12.0,StageY=1.0,StageZ0=13,StageX=8;
        /// <summary>The speaker stacks (`club-speaker-w/e`): inner and outer x, front face z, depth end and top.</summary>
        public const double SpeakerX0=8.0,SpeakerX1=10.8,SpeakerZ=13.5,SpeakerZ1=17.8,SpeakerTop=4.6;
        /// <summary>The crowd galleries' floor beyond the side walls' arches (the sim's arch sills).</summary>
        public const double GalleryY=8.4;
        /// <summary>The DJ's place behind the booth (on the stage).</summary>
        public const double DjX=0,DjZ=17.0;
        /// <summary>The moving heads: under the north and south trusses at these x (12 fixtures), and four on the tower tops.</summary>
        public static readonly double[] HeadX={-7.5,-4.5,-1.5,1.5,4.5,7.5};
        public const int Heads=16;
        /// <summary>The laser wall's emitter rails just outside the floor's north and south edges (z), the carriage's height.</summary>
        public const double LaserRailZ=7.75,LaserTop=.65;

        // ---------------- colour ----------------
        /// <summary>The party palette (sRGB): cyan, violet, magenta, blue, pink, aqua, ice. Amber and red are kept for the
        /// telegraphs (2.1 rule 4): the floor never plays them as a pattern.</summary>
        public static readonly int[] Party={0x22E6FF,0x8B5CFF,0xFF3DCF,0x3D6BFF,0xFF6FD8,0x3DFFD2,0xC8F4FF};
        public const int Amber=0xFFB000,Danger=0xFF2A3D,LiveCore=0xFFF2E8,LaserRed=0xFF1E2A,Cyan=0x22E6FF,Magenta=0xFF3DCF,Violet=0x8B5CFF;
        /// <summary>Linear 0..1 channels of an sRGB colour.</summary>
        public static double R(int hex)=>NativeSceneryBuilder.Channel(hex,16);public static double G(int hex)=>NativeSceneryBuilder.Channel(hex,8);public static double B(int hex)=>NativeSceneryBuilder.Channel(hex,0);
        /// <summary>A stable 0..1 hash of three integers.</summary>
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+15485863,b+97,c+193);
        public static double Frac(double v)=>v-Math.Floor(v);
        public static double Clamp01(double v)=>v<0?0:v>1?1:v;
        public static double Smooth(double t){t=Clamp01(t);return t*t*(3-2*t);}
    }
}
