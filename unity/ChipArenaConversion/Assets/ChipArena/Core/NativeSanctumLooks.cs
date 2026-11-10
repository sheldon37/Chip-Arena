using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hollow Sanctum's presentation (docs/arenas/SANCTUM.md): which map it is, the kit kinds its static detail (Core
    /// NativeSanctumKit) and its rigs (Core NativeSanctumRig) draw with, resolved from MapLooks.json `kitLooks` ("sanctum-kit-*";
    /// an unlisted kind takes <see cref="Defaults"/>), and the places the kit, the glass, the rigs, the effects and the lights
    /// agree on: the moon's direction, the stained-glass windows, the bell, the censer's pivot. Presentation only: none of this
    /// is sim data, collides, damages or enters the network frame. Pure.
    /// </summary>
    public static class NativeSanctumLooks
    {
        public const string MapId="sanctum",Look="sanctum-midnight";
        public static bool Active(string mapId)=>mapId==MapId;

        /// <summary>Kit kinds (index = kind), resolved once per look through `kitLooks`.</summary>
        public static readonly string[] Kinds={"sanctum-kit-stone","sanctum-kit-stone-dark","sanctum-kit-stone-trim","sanctum-kit-marble","sanctum-kit-gold","sanctum-kit-iron",
            "sanctum-kit-bronze","sanctum-kit-wax","sanctum-kit-oak","sanctum-kit-oak-dark","sanctum-kit-cloth-red","sanctum-kit-cloth-blue","sanctum-kit-cloth-gold","sanctum-kit-pipe",
            "sanctum-kit-lead","sanctum-kit-coal","sanctum-kit-inlay","sanctum-kit-rope","sanctum-kit-chain","sanctum-kit-velvet","sanctum-kit-statue","sanctum-kit-brass","sanctum-kit-glow",
            "sanctum-kit-glass","sanctum-kit-carpet","sanctum-kit-bone","sanctum-kit-silver","sanctum-kit-ember"};
        public const int Stone=0,StoneDark=1,Trim=2,Marble=3,Gold=4,Iron=5,Bronze=6,Wax=7,Oak=8,OakDark=9,ClothRed=10,ClothBlue=11,ClothGold=12,Pipe=13,Lead=14,Coal=15,Inlay=16,
            Rope=17,Chain=18,Velvet=19,Statue=20,Brass=21,Glow=22,Glass=23,Carpet=24,Bone=25,Silver=26,Ember=27;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it.</summary>
        public static readonly (string look,int color)[] Defaults={("terracotta",0xA49A8C),("terracotta",0x4A4450),("terracotta",0xBEB4A4),("plain",0xE2DCD2),("plain",0xD8A848),
            ("plain",0x23222A),("plain",0x8A6A3A),("plain",0xEDE2C8),("timber",0x5C3C26),("timber",0x3A2618),("plain",0x7A1620),("plain",0x1C2A6A),("plain",0xC8A040),("plain",0xB8BCC4),
            ("plain",0x18161C),("ward-burner",0x3A2C26),("plain",0x8A7448),("plain",0x8A7454),("plain",0x3A3A42),("plain",0x4A1028),("terracotta",0xC4BCB0),("plain",0xB8903C),
            ("cyber-tube",0xFFC070),("window-lit",0x4A5A7A),("plain",0x5A1418),("plain",0xCEC4AE),("plain",0xC8CCD4),("cyber-tube",0xFF6A20)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "sanctum-kit-*" id wins; else the default).</summary>
        public static NativeFactoryKit.Looks Resolve(MapLookBlock[] kitLooks)
        {
            var l=new NativeFactoryKit.Looks();
            for(int k=0;k<Kinds.Length;k++)
            {
                var c=NativeBlockLooks.Resolve(kitLooks,null,Kinds[k],"",0xFFFFFF);
                if(!c.Listed){var d=Defaults[k];c=NativeBlockLooks.Resolve(new[]{new MapLookBlock{block=Kinds[k],look=d.look,color=d.color}},null,Kinds[k],"",0xFFFFFF);}
                l.Look[k]=c.Look;l.Color[k]=c.Color;l.Flags[k]=c.Flags&~(NativeBlockLooks.Band|NativeBlockLooks.Tag);
            }
            return l;
        }

        // ---------------- the places the kit, the glass, the rigs, the effects and the lights share ----------------
        /// <summary>The moon key (MapLooks: elevation 42, bearing 200, from the south-south-west): the unit direction its light
        /// travels (down, north and a little east), so the south lancets, the south rose and the west rose take it.</summary>
        public const double MoonElevation=42,MoonBearing=200;
        public static void MoonRay(out double x,out double y,out double z){NativeMapLooks.Toward(MoonElevation,MoonBearing,out x,out y,out z);x=-x;y=-y;z=-z;}
        /// <summary>The hall: the vault's springing and slab, the aisle walls' inner face, the transept's side walls and end walls,
        /// the end walls, the galleries' top, the crypt's floor, the crossing's oculus (half size) and the belfry above it.</summary>
        public const double Vault=21,AisleWall=11.5,ArmWall=6,ArmEnd=20,EndWall=30,Gallery=4,Crypt=-3.6,Oculus=2,BelfryFloor=21.6,BelfryTop=30;
        /// <summary>The great bell: its pivot (the headstock) over the oculus, its mouth's radius and height.</summary>
        public const double BellX=0,BellY=27.2,BellZ=0,BellMouth=1.35,BellHeight=2.4;
        /// <summary>The censer's rope pivot (a yoke in the oculus): the pendulum's arc, the rope's length to the censer's centre.</summary>
        public const double CenserPivotY=21.6;

        /// <summary>A stained-glass window: the opening's centre, its half width along the wall, its half height, the wall's
        /// inward normal (x or z, ±1), whether it is a rose (a circle of radius `Half` inside the square opening) and its design.</summary>
        public struct Window {public double X,Y,Z,Half,HalfY,Nx,Nz;public bool Rose;public int Design;}
        /// <summary>Every window of the hall, as the draft (docs/arenas/scripts/sanctum_m1.py) cuts them: the aisles' lancets (eight
        /// a side, sills at 8 m), the transept arms' side lancets, the two end roses over the lofts and the two transept roses.</summary>
        public static readonly Window[] Windows=BuildWindows();
        private static Window[] BuildWindows()
        {
            var w=new System.Collections.Generic.List<Window>();int d=0;
            foreach(int side in new[]{-1,1})foreach(double x in new[]{-27.0,-21.0,-15.0,-9.0,9.0,15.0,21.0,27.0})
                w.Add(new Window{X=x,Y=13,Z=side*(AisleWall+.4),Half=1.2,HalfY=5,Nx=0,Nz=-side,Design=d++%4});
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})w.Add(new Window{X=sx*(ArmWall+.4),Y=13,Z=sz*16,Half=1.2,HalfY=5,Nx=-sx,Nz=0,Design=d++%4});
            foreach(int sx in new[]{-1,1})w.Add(new Window{X=sx*(EndWall+.4),Y=14,Z=0,Half=4,HalfY=4,Nx=-sx,Nz=0,Rose=true,Design=sx<0?4:5});
            foreach(int sz in new[]{-1,1})w.Add(new Window{X=0,Y=13,Z=sz*(ArmEnd+.4),Half=3.8,HalfY=4,Nx=0,Nz=-sz,Rose=true,Design=sz<0?6:7});
            return w.ToArray();
        }
        /// <summary>How much moonlight a window takes (0: it faces away; the cosine of the moon to its outward face otherwise).</summary>
        public static double MoonShare(in Window w){MoonRay(out double x,out _,out double z);double d=x*w.Nx+z*w.Nz;return d>0?d:0;}

        /// <summary>The chandeliers' rest: the crane's carry height is the iron ring's underside; the drawn crown rises above it.</summary>
        public const double CrownHeight=1.6;
        /// <summary>A stable 0..1 hash of three integers.</summary>
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+3571,b+97,c+389);
        public static int Mix(int a,int b,double t)=>NativeNewStageScenery.Mix(a,b,t);
    }
}
