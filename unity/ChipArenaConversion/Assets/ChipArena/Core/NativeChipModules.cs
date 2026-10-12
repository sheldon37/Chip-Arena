using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Chip modules on the Standard weapon skins (owner 2026-10-12: "improve chip upgrade displays on newer weapons (Change
    // based on skin). The upgrades look out of place on the modern weapons"). The Prototype Series keeps the source's
    // weaponModifiers / enchantWeapon pieces exactly (NativeWeaponDressing, untouched). A Standard weapon carries one designed
    // attachment per Chip kind instead: sculpted in the Standard models' language (NativeWeaponSculpt: chamfers, panel lines,
    // Chip-coloured emissive strips; NativeChipModules.Parts.cs) and mounted on sockets measured from that model's own geometry
    // (ChipSockets, one table per weapon below). A module shows under exactly the source's conditions, read the same way
    // (chip counts and stats), so a loadout shows the same kinds on both skins; only the look and the placement differ.
    // Presentation only: nothing here is read by the authority, the network frame or the melee rules, and no module defines
    // reach, a hit volume or timing (weapon-visuals.ts:20).
    public enum ChipFamily : byte { None, Gun, Staff, Blade, Shield, Bow }
    /// <summary>The designed attachments; each is one small mesh per weapon (or shared), variant and detail tier.</summary>
    public enum ChipPart : byte { Pod, Drone, Collar, Coil, Helix, Gimbal, Intake, Fin, Seeker, Prism, Lance, Cell, Cap, Vane, Piston, Charge, Launcher, Sawback, Frag, Capacitor, EdgeRail, Echo, HeatEdge, Flame, Coat, Mote, Spinner, Spike, Flywheel, Clamp, Chevron, Tip, Count }
    /// <summary>One placed attachment in the source weapon frame (the Runtime mirrors x once, as for the weapon models).</summary>
    public struct ChipModule
    {
        public ChipPart Part;public byte Variant;public int Color;public Vector3 Position,Scale,Drift;public Quaternion Rotation;
        /// <summary>
        /// Orbit: this drone's index round the weapon axis through Position on the Radius x RadiusY ellipse (-1: none; Orbits drones in all). Spin:
        /// turns about its own z (the rate coil). Enchant: NativeWeaponDressing.FireEnchant / PoisonEnchant. Deform: a blade
        /// strip that stretches with the Standard blade under reach (every other piece rides rigid to its stretched place).
        /// Ride (the bow): the live part the piece is on, NativeChipModules.OnWeapon (the riser), OnUpperLimb / OnLowerLimb (given
        /// at rest, turned with the limb's flex) or OnArrow (in the nocked arrow's frame: the nock at 0, +z to the head).
        /// </summary>
        public sbyte Orbit;public byte Orbits;public bool Spin,Deform;public byte Enchant,Ride;public float Radius,RadiusY,Phase;
    }
    /// <summary>The kinds a loadout shows, from the chips and stats exactly as weaponModifiers reads them (weapon-visuals.ts:89-106).</summary>
    public struct ChipKinds
    {
        public int Extra,Ricochet,Velocity,Magazine,Rate;public bool Homing,Split,Pierce,Pong,Boomerang,Vacuum,Kinetic,Demolition,Shrapnel,Static,Reach,Echo;
        public bool Any=>Extra>0||Ricochet>0||Velocity>0||Magazine>0||Rate>0||Homing||Split||Pierce||Pong||Boomerang||Vacuum||Kinetic||Demolition||Shrapnel||Static||Reach||Echo;
    }
    /// <summary>A cross-section an axial attachment wraps: centre height, half extents (Round: a circle of HalfX), corner cut; Arch: open underneath.</summary>
    public struct ChipSection
    {
        public float Y,HalfX,HalfY,Corner;public bool Round,Arch;
        public static ChipSection Circle(float r,float y=0)=>new ChipSection{Y=y,HalfX=r,HalfY=r,Round=true};
        public static ChipSection Box(float hx,float hy,float y,float corner,bool arch=false)=>new ChipSection{Y=y,HalfX=hx,HalfY=hy,Corner=corner,Arch=arch};
        /// <summary>The radius that clears every point of the section from the axis through (0, Y).</summary>
        public float Reach=>Round?HalfX:(float)Math.Sqrt(HalfX*HalfX+HalfY*HalfY);
    }
    /// <summary>A straight mounting run on the model's surface (Side on +x, mirrored to -x): the surface point and its z span.</summary>
    public struct ChipRun{public float X,Y,Z0,Z1;public bool Open=>Z1>Z0;}
    /// <summary>A fixed mount: the surface point, the turn that points the part's +y out of that surface, and a size.</summary>
    public struct ChipMount{public Vector3 At;public Quaternion Turn;public float Size;public bool Set;}
    /// <summary>
    /// One weapon's chip sockets, measured from its Standard Full model (Core/NativeWeaponModels*.cs; ChipModuleChecks proves
    /// each lies on the model). Sections index the axial parts' variants (the collar or coil a station wraps).
    /// </summary>
    public sealed class ChipSockets
    {
        public string Spec;public ChipFamily Family;public ChipSection[] Sections;
        // Axial stations: ricochet collars and velocity coils (z, section index), the rate coil / spinner run, the muzzle device.
        public float[] RicochetZ,VelocityZ;public byte[] RicochetS,VelocityS;
        public float HelixZ,HelixLength,HelixR,HelixY,SectionX;public ChipMount Flywheel;public ChipMount[] Mounts;
        public float DeviceZ,DeviceR,IntakeZ,IntakeR;public ChipSection Device;public ChipMount VaneMount;
        // Gun runs, pods and the magazine.
        public ChipRun Top,Side,Under;public Vector3[] Pods;public float PodZ,PodLength,PodR,PodGap;public float[] PodBridges;
        public Vector3 MagAt;public Quaternion MagTurn;public Vector3 MagCell;public bool MagDisc;
        // Lengths of the per-weapon run parts.
        public float LanceLength,PistonLength,LauncherLength,PartScale=1;
        /// <summary>The guns' fixed rail slots (z along the run; NaN: none) and the belly heights.</summary>
        public float LanceZ,SeekerZ,FinZ,KineticZ,ChargesZ,CapsZ,PrismZ,PrismY,LauncherZ,LauncherY;public bool LanceUnder,ChargesTop;
        // Staffs: the head (drones, vacuum ring, pong gimbal), the collar ring of side mounts, the lower shaft and heel.
        public float HeadZ,HeadR,CollarZ,CollarR,LowZ0,LowZ1,ShaftR,HeelZ,SpikeZ,SpikeSize=1,ChargeZ,StaticZ,StaticR,TipZ;public Vector3 SeekerAt;
        // Blades and the shield: fixed mounts and the strip run along the blade.
        public ChipMount Kinetic,Static,Split,Homing,Vane,Charges,Pierce,Gimbal,Cells,Spinner,Vacuum,Sawback,Drones,RicochetM,VelocityM,Seeker;
        public float StripZ0,StripZ1,EdgeSign,GimbalR,SpinnerR,VacuumR,DroneZ,DroneX,DroneR,DroneRY,DroneSize=1,FlameSize=1;public bool DoubleEdge,Mirror;
        // The bow's limb-tip cams and the frag collar on its stabilizer weight (the bow reuses the mounts above for the rest).
        public ChipMount Cam,Frag;
        /// <summary>The blade's measured half profile along z (blades), or the rim outline (shield): min/max x and the flat's half thickness.</summary>
        public float[] ProfileZ,ProfileMinX,ProfileMaxX,ProfileHalfY;
        public float MinX(float z)=>Sample(ProfileMinX,z);public float MaxX(float z)=>Sample(ProfileMaxX,z);public float HalfY(float z)=>Sample(ProfileHalfY,z);
        private float Sample(float[] v,float z)
        {
            if(ProfileZ==null||ProfileZ.Length==0)return 0;if(z<=ProfileZ[0])return v[0];int last=ProfileZ.Length-1;if(z>=ProfileZ[last])return v[last];
            int i=0;while(i<last-1&&ProfileZ[i+1]<z)i++;float t=(z-ProfileZ[i])/Math.Max(1e-6f,ProfileZ[i+1]-ProfileZ[i]);return v[i]+(v[i+1]-v[i])*t;
        }
    }
    public static partial class NativeChipModules
    {
        public const int MaxModules=NativeWeaponDressing.MaxPieces,Variants=4;
        public const float Gap=.006f,OrbitSpeed=1.4f,SpinSpeed=8;
        /// <summary>ChipModule.Ride: the bow's live parts (every other weapon's pieces are OnWeapon).</summary>
        public const byte OnWeapon=0,OnUpperLimb=1,OnLowerLimb=2,OnArrow=3;
        // The source's piece colours (weapon-visuals.ts:89-106), one per kind: every Standard attachment is tinted by its kind's.
        public const int Ricochet=0xffcc49,Velocity=0x79eaff,Homing=0xf482d4,Split=0x96ffb7,Pierce=0xffffff,Magazine=0xe3b779,Pong=0xff5269,Boomerang=0x7fffe0,Vacuum=0xbc72ff,
            Rate=0xffae64,Kinetic=0xffad68,Demolition=0xffd45f,Shrapnel=0xff855c,Static=0xc4a0ff,Reach=0x92f5e5,Echo=0xc1a1ff,PodGlow=0xffd88d,Flame=0xff6c27,FlameHot=0xffd06a,Poison=0x88f37f;
        public static ChipFamily Family(string spec)=>NativeWeaponDressing.Gun(spec)?ChipFamily.Gun:NativeWeaponDressing.Staff(spec)||spec=="leet"?ChipFamily.Staff:spec=="shield"?ChipFamily.Shield:spec=="sword"||spec=="daggers"||spec=="greatsword"?ChipFamily.Blade:NativeBowWire.Is(spec)?ChipFamily.Bow:ChipFamily.None;
        /// <summary>A weapon whose Standard model carries designed modules (every Standard weapon but the fists; the Siren has no skin).</summary>
        public static bool Designed(string spec)=>Family(spec)!=ChipFamily.None&&NativeWeaponModels.Designed(spec);
        private static int Count(IReadOnlyDictionary<string,int> chips,string id)=>chips!=null&&chips.TryGetValue(id,out int n)&&n>0?n:0;
        /// <summary>weaponModifiers' conditions (NativeWeaponDressing.Modules reads exactly these, in this order).</summary>
        public static ChipKinds Kinds(IReadOnlyDictionary<string,int> chips,ChipStats stats)
        {
            var k=new ChipKinds();if(stats==null)return k;double extra=Math.Min(6,stats["extraProjectiles"]);k.Extra=extra>0?(int)Math.Ceiling(extra):0;
            k.Ricochet=Count(chips,"ricochet");k.Velocity=Count(chips,"velocity");k.Homing=stats["homing"]!=0;k.Split=stats["split"]!=0;k.Pierce=stats["pierces"]!=0;k.Magazine=Count(chips,"magazine");
            k.Pong=stats["pong"]!=0;k.Boomerang=stats["boomerang"]!=0;k.Vacuum=stats["vacuum"]!=0;
            int rate=Count(chips,"fire-rate");if(rate==0)rate=Count(chips,"cast-rate");if(rate==0)rate=Count(chips,"swing-rate");k.Rate=rate+Count(chips,"overclock");
            k.Kinetic=stats["impulse"]>1.3;k.Demolition=Count(chips,"demolition")>0;k.Shrapnel=stats["shrapnel"]!=0;k.Static=stats["staticEvery"]!=0;k.Reach=stats["reach"]>1;k.Echo=stats["extraStrikes"]!=0;
            return k;
        }
        private static Quaternion Rz(float a)=>Quaternion.CreateFromAxisAngle(Vector3.UnitZ,a);
        private static Quaternion Rx(float a)=>Quaternion.CreateFromAxisAngle(Vector3.UnitX,a);
        private static Quaternion Ry(float a)=>Quaternion.CreateFromAxisAngle(Vector3.UnitY,a);
        // The turn that points a part's +y radially out at angle `theta` about z (Rz(theta - pi/2)), and its mount point.
        private static ChipMount Radial(float r,float theta,float z,float cx=0,float cy=0)=>new ChipMount{At=new Vector3(cx+r*(float)Math.Cos(theta),cy+r*(float)Math.Sin(theta),z),Turn=Rz(theta-(float)Math.PI/2),Size=1,Set=true};
        private static ChipMount At(float x,float y,float z,Quaternion turn,float size=1)=>new ChipMount{At=new Vector3(x,y,z),Turn=turn,Size=size,Set=true};
        private static bool Put(ChipModule[] o,ref int n,ChipPart part,int color,Vector3 at,Quaternion turn,float scale=1,byte variant=0)
        {
            if(n>=o.Length)return false;o[n++]=new ChipModule{Part=part,Variant=variant,Color=color,Position=at,Rotation=turn,Scale=new Vector3(scale),Orbit=-1};return true;
        }
        // A part and its reflection across the weapon's x = 0 plane. Paired parts are built symmetric about their own x = 0
        // plane, so the reflection is the mirrored turn (M R M) on the same mesh.
        private static void Pair(ChipModule[] o,ref int n,ChipPart part,int color,ChipMount m,float scale=1,byte variant=0)
        {
            if(!m.Set)return;Put(o,ref n,part,color,m.At,m.Turn,scale*m.Size,variant);Put(o,ref n,part,color,new Vector3(-m.At.X,m.At.Y,m.At.Z),Mirrored(m.Turn),scale*m.Size,variant);
        }
        // A part and its copy turned half round the weapon's z axis (both flats of a blade's guard, the dagger's cap).
        private static void Opposite(ChipModule[] o,ref int n,ChipPart part,int color,ChipMount m,float scale=1)
        {
            if(!m.Set)return;var half=Rz((float)Math.PI);Put(o,ref n,part,color,m.At,m.Turn,scale*m.Size);Put(o,ref n,part,color,new Vector3(-m.At.X,-m.At.Y,m.At.Z),half*m.Turn,scale*m.Size);
        }
        /// <summary>A turn reflected across the x = 0 plane (M R M with M = diag(-1, 1, 1)).</summary>
        public static Quaternion Mirrored(Quaternion q)=>new Quaternion(q.X,-q.Y,-q.Z,q.W);

        /// <summary>
        /// The Standard modules for `spec` (null or a non-designed weapon: none), at most o.Length, no allocation once the socket
        /// tables are built (on the first call). `ranged` is WEAPONS[spec].class; the drones take the weapon colour.
        /// </summary>
        public static int Modules(string spec,bool ranged,int weaponColor,IReadOnlyDictionary<string,int> chips,ChipStats stats,ChipModule[] o)
        {
            var s=Sockets(spec);if(s==null||stats==null)return 0;var k=Kinds(chips,stats);if(!k.Any)return 0;
            switch(s.Family){case ChipFamily.Gun:return Gun(s,k,o);case ChipFamily.Staff:return Staff(s,k,weaponColor,o);case ChipFamily.Blade:return Blade(s,k,weaponColor,o);case ChipFamily.Shield:return Shield(s,k,weaponColor,o);case ChipFamily.Bow:return Bow(s,k,o);}
            return 0;
        }
    }
}
