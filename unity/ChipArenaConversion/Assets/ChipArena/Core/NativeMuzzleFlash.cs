using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The muzzle device a gun's flash comes out of: none (the Prototype Series' plain block face), the Standard pistol's
    /// ported compensator, the rifle's muzzle brake or the machine gun's slotted flash hider.</summary>
    public enum MuzzleDevice : byte {Plain,Compensator,Brake,Hider}
    /// <summary>The pistol and machine-gun Q kit a round was fired under (NativeGunKit): none, Fan the Hammer or Overdrive.</summary>
    public enum MuzzleKit : byte {None,Fan,Overdrive}

    /// <summary>A vent of a muzzle device in the source weapon frame (+x the gun's left, +y up the sights, +z along the barrel):
    /// its opening on the model, the jet's direction, the jet's length and width (m), the vent's half length along the barrel
    /// and the finest weapon detail tier whose model cuts it (NativeWeaponSculpt: Mid is tier 1 and finer, Fine tier 0).</summary>
    public readonly struct MuzzlePort
    {
        public readonly Vector3 At,Dir;public readonly float Length,Width,HalfZ;public readonly int Tier;
        public MuzzlePort(Vector3 at,Vector3 dir,float length,float width,float halfZ,int tier){At=at;Dir=Vector3.Normalize(dir);Length=length;Width=width;HalfZ=halfZ;Tier=tier;}
    }

    /// <summary>The look of one shot's muzzle flash (sizes in metres of the weapon frame; the first-person view model's own scale
    /// applies on top, and NativeMuzzleFx.ViewScale with it).</summary>
    public struct MuzzleLook
    {
        public MuzzleDevice Device;public MuzzleKit Kit;
        /// <summary>The flash's life (s) and the crown it leaves from (z in the weapon frame, just ahead of the drawn muzzle face).</summary>
        public float Life,Crown;
        /// <summary>The white-hot core and the fireball (sprite sizes), the forward plume (length and width), the star's petals
        /// (length, width as a share of it, and the tilt toward the barrel out of the muzzle plane), the HDR gain, the per-shot
        /// size spread (Flicker: the machine gun's is widest, so its strobe reads as flicker) and the vent jets' scale.</summary>
        public float Core,Fireball,Plume,PlumeWidth,Star,StarWidth,Tilt,Gain,Flicker,JetScale;
        /// <summary>The chance a shot throws sparks and at most how many; the smoke wisps a shot leaves.</summary>
        public float SparkChance;public int Sparks,Wisps;
        /// <summary>The star: how many petals and whether they sit on the device's own slots (RollBase plus k turns of 2 pi /
        /// Petals, jittered by RollJitter) rather than a seeded roll.</summary>
        public int Petals;public bool FixedRoll;public float RollBase,RollJitter;
        /// <summary>The device's vents still open (bit k for NativeMuzzleFx.Ports(Device)[k]: cut at this detail tier and not
        /// covered by a chip module) and the multishot pods that flash beside the bore.</summary>
        public int PortMask,Pods;
        /// <summary>The gun's chip-module sockets (the Standard skin's, handed over by the module presentation), which place the
        /// pods; null when no module is fitted.</summary>
        public ChipSockets Sockets;
        /// <summary>Overdrive's barrel heat (0..1): a hotter, whiter, larger flash as the barrels heat.</summary>
        public float Heat;
        /// <summary>The pack flash family (NativeMuzzleFx.SheetPistol..SheetTurret; 0 none): its flipbook flame and burst draw
        /// when the drawer holds that family's textures, else the procedural flash alone.</summary>
        public int Sheet;
    }

    /// <summary>One gun family's pack flash (NativeMuzzleFx.SheetLooks): the flame flipbook's texture id, grid (columns, rows,
    /// frame 0 at the top left), the first frame played and how many; the flame's length (share of the look's plume) and width
    /// (share of its length: the cell's 2:1 aspect), its life (flash lives); the burst sprite's id, size (share of the
    /// fireball), life (flash lives) and the HDR gains of both.</summary>
    public readonly struct MuzzleSheet
    {
        public readonly string Flame,Burst;public readonly int Columns,Rows,First,Frames;public readonly float Length,Width,Life,BurstSize,BurstLife,Gain;
        public MuzzleSheet(string flame,int columns,int rows,int first,int frames,float length,float width,float life,string burst,float burstSize,float burstLife,float gain)
        {Flame=flame;Columns=columns;Rows=rows;First=first;Frames=frames;Length=length;Width=width;Life=life;Burst=burst;BurstSize=burstSize;BurstLife=burstLife;Gain=gain;}
    }

    /// <summary>
    /// The guns' muzzle flashes (owner 2026-10-20: "Improve muzzle flash effects from our weapons~ I still want to make use of
    /// lighting"), replacing the weapons milestone's three-petal star. Each shot is a layered flash at the drawn muzzle crown:
    ///  - a white-hot core (the first .6 of the flash) and a soft orange fireball that swells as it fades (1.4 flash lives);
    ///  - the forward plume, a flame card along the barrel;
    ///  - a star of petals from the muzzle shape: on the Standard models the machine gun's four petals come out of its slotted
    ///    hider's four slots (an X), the pistol and rifle throw three at a seeded roll; the Prototype Series' block faces a cross
    ///    (the machine gun's an X);
    ///  - jets out of the Standard devices' own vents: the pistol compensator's two top ports, the rifle brake's three side ports a
    ///    side and its top port, the machine-gun hider's slots; a vent a chip module covers (a ricochet collar, the vacuum intake
    ///    bell, a boomerang vane's foot) stays shut, and a model tier that does not cut the vent shows none;
    ///  - multishot pods flash at their own mouths, small;
    ///  - sparks on some shots (seeded chance), and light smoke wisps.
    /// Sized to the gun: the pistol short and punchy (50 ms), the rifle big and heavy (80 ms), the machine gun fast with a wide
    /// per-shot size spread (45 ms, so its strobe reads as flicker). Fan the Hammer's rounds flash larger and hotter, Overdrive's
    /// whiter and larger as the barrels heat. Every value is a pure function of the shot's replicated time and owner (the
    /// NativeWeaponFx seed), the seat's skin and chips and the presentation clock, so host, guests, late joiners and the killcam
    /// draw the same flash. Runtime/NativeWeaponFxDraw.cs (NativeWeaponFxShapes.Muzzle) turns these into geometry; the light the
    /// flash throws is NativeMuzzleLight's. Cosmetic only: nothing here reaches the authority, the hit rules, the network frame or
    /// the animation clocks.
    /// </summary>
    public static class NativeMuzzleFx
    {
        public const int CoreColor=0xfff2d2,PetalColor=0xffd276,EdgeColor=0xff9f00,PlumeColor=0xffc35a,FireColor=0xff7a22,JetColor=0xffdc96,SparkColor=0xffff72,OverdriveColor=0xffe8c4;
        public const float WispLife=.62f,SparkLifeMax=.25f;
        /// <summary>How long after the shot anything of it may still draw (the smoke wisp).</summary>
        public const double Span=.68;
        public const int MaxPetals=4,MaxWisps=3;
        /// <summary>The crown sits this far ahead of the drawn muzzle face (m).</summary>
        public const float CrownLead=.004f;
        /// <summary>First person: the view weapon's flash at this share of the third-person size (the view model's own scale
        /// already shrinks it) and gain, and no glow wider than this share of the view's height (ViewSize), so the view weapon
        /// stays readable through its own flash.</summary>
        public const float ViewScale=1,ViewGain=.8f,ViewShare=.2f;
        /// <summary>First person: how much further out along the barrel the core and fireball sit (m of the weapon frame), so a
        /// flash seen almost down the barrel is not hidden behind the view gun's own muzzle face.</summary>
        public const float ViewLead=.12f;
        /// <summary>First person: the core and fireball this much larger (the eye sits just over the barrel, so the gun's top and
        /// sights hide a small glow), still capped at ViewShare of the view.</summary>
        public const float ViewGlow=1.7f;
        /// <summary>First person: the petals' tilt along the barrel at this share, so the star faces the eye behind the gun instead
        /// of reading edge-on.</summary>
        public const float ViewTilt=.45f;
        /// <summary>The Prototype Series' block muzzle faces (z, the bank's 'weapon-' boxes): the pistol's, the rifle's, the
        /// machine gun's. The Standard crowns are the models' own (NativeWeaponModels Design.Muzzle).</summary>
        public const float PrototypePistolFace=.48f,PrototypeRifleFace=1.21f,PrototypeMachineGunFace=.9f;
        /// <summary>The chip modules that wrap a device (the module parts): a ricochet collar's half length, the vacuum
        /// bell's length behind its face, a boomerang vane's foot (from and to, along the barrel, at size 1).</summary>
        public const float CollarHalf=.0065f,BellLength=.024f,VaneFoot0=-.003f,VaneFoot1=.027f;

        private static readonly MuzzlePort[] None=Array.Empty<MuzzlePort>();
        // The Standard models' vents (Core/NativeWeaponModels.Guns.cs): the pistol compensator's two top ports (TopSlot at y .0752,
        // z .466 and .482), the rifle brake's three side ports a side (SideSlot at x .032, z 1.115 + .03 i) and its top port (Fine
        // only, y .0281, z 1.17), the machine-gun hider's four slots (radius .0262, z .87, at 45 degrees and every quarter turn).
        private static readonly MuzzlePort[] Compensator={
            new MuzzlePort(new Vector3(0,.0752f,.466f),new Vector3(0,1,.35f),.085f,.034f,.0045f,1),new MuzzlePort(new Vector3(0,.0752f,.482f),new Vector3(0,1,.35f),.085f,.034f,.0045f,1)};
        private static readonly MuzzlePort[] Brake={
            new MuzzlePort(new Vector3(.032f,0,1.115f),new Vector3(1,.04f,.22f),.15f,.05f,.009f,1),new MuzzlePort(new Vector3(-.032f,0,1.115f),new Vector3(-1,.04f,.22f),.15f,.05f,.009f,1),
            new MuzzlePort(new Vector3(.032f,0,1.145f),new Vector3(1,.04f,.22f),.15f,.05f,.009f,1),new MuzzlePort(new Vector3(-.032f,0,1.145f),new Vector3(-1,.04f,.22f),.15f,.05f,.009f,1),
            new MuzzlePort(new Vector3(.032f,0,1.175f),new Vector3(1,.04f,.22f),.15f,.05f,.009f,1),new MuzzlePort(new Vector3(-.032f,0,1.175f),new Vector3(-1,.04f,.22f),.15f,.05f,.009f,1),
            new MuzzlePort(new Vector3(0,.0281f,1.17f),new Vector3(0,1,.25f),.08f,.035f,.016f,0)};
        private static readonly MuzzlePort[] Hider=HiderSlots();
        private static MuzzlePort[] HiderSlots()
        {
            var o=new MuzzlePort[4];
            for(int j=0;j<4;j++){double a=j*Math.PI/2+Math.PI/4;var n=new Vector3((float)Math.Cos(a),(float)Math.Sin(a),0);o[j]=new MuzzlePort(n*.0262f+new Vector3(0,0,.87f),n+new Vector3(0,0,.45f),.07f,.022f,.018f,1);}
            return o;
        }
        /// <summary>The vents of a device (the Prototype's plain face has none).</summary>
        public static MuzzlePort[] Ports(MuzzleDevice device)=>device==MuzzleDevice.Compensator?Compensator:device==MuzzleDevice.Brake?Brake:device==MuzzleDevice.Hider?Hider:None;
        /// <summary>The device a skin's gun carries.</summary>
        public static MuzzleDevice Device(string spec,bool standard)=>!standard?MuzzleDevice.Plain:spec=="pistol"?MuzzleDevice.Compensator:spec=="rifle"?MuzzleDevice.Brake:spec=="machinegun"?MuzzleDevice.Hider:MuzzleDevice.Plain;
        /// <summary>The crown a skin's flash leaves from (z in the weapon frame): CrownLead ahead of the drawn muzzle face.</summary>
        public static float Crown(string spec,bool standard)
        {
            var d=standard?NativeWeaponModels.Get(spec):null;
            float face=d!=null&&!float.IsNaN(d.Muzzle.Z)?d.Muzzle.Z:spec=="pistol"?PrototypePistolFace:spec=="rifle"?PrototypeRifleFace:spec=="machinegun"?PrototypeMachineGunFace:NativeWeaponDressing.MuzzleZ(spec);
            return face+CrownLead;
        }
        /// <summary>The kit a round fired at `shot` belongs to (Overdrive's and Fan the Hammer's schedules, NativeGunKit).</summary>
        public static MuzzleKit KitOf(Actor a,float shot,float time,out float heat)
        {
            heat=0;if(a==null)return MuzzleKit.None;var s=NativeGunKit.Of(a,time);
            if(a.Weapon==WeaponId.MachineGun&&NativeGunKit.DriveRound(s,shot)){heat=NativeGunKit.Heat(s,time);return MuzzleKit.Overdrive;}
            if(a.Weapon==WeaponId.Pistol&&NativeGunKit.FanRound(s,shot))return MuzzleKit.Fan;
            return MuzzleKit.None;
        }

        /// <summary>A plain look (the Prototype Series' block face, no kit, no vents): the hazard kit's turrets and old callers.</summary>
        public static bool Look(string spec,out MuzzleLook look)=>Look(spec,NativeWeaponSkins.PrototypeIndex,default,null,MuzzleKit.None,NativeWeaponModels.Low,0,out look);
        /// <summary>
        /// The flash of `spec` in `skin` with the seat's chip modules (their kinds and the gun's sockets, which the module
        /// presentation hands over: Core leaves the modules to their own files; Standard only), under `kit` (at Overdrive `heat`),
        /// at the weapon model detail `tier` the rig draws (NativeWeaponSkins.Tier: a vent the tier does not cut shows no jet).
        /// False for weapons without a muzzle flash.
        /// </summary>
        public static bool Look(string spec,int skin,in ChipKinds chips,ChipSockets sockets,MuzzleKit kit,int tier,float heat,out MuzzleLook look)
        {
            switch(spec)
            {
                case "pistol":look=new MuzzleLook{Life=.05f,Core=.13f,Fireball=.24f,Plume=.28f,PlumeWidth=.1f,Star=.19f,StarWidth=.5f,Tilt=.75f,Gain=1,Flicker=.12f,JetScale=1,SparkChance=.35f,Sparks=3,Wisps=2,Petals=3};break;
                case "rifle":look=new MuzzleLook{Life=.08f,Core=.19f,Fireball=.4f,Plume=.5f,PlumeWidth=.17f,Star=.24f,StarWidth=.55f,Tilt=.7f,Gain=1.25f,Flicker=.1f,JetScale=1,SparkChance=.6f,Sparks=5,Wisps=3,Petals=3};break;
                case "machinegun":look=new MuzzleLook{Life=.045f,Core=.14f,Fireball=.28f,Plume=.34f,PlumeWidth=.12f,Star=.23f,StarWidth=.32f,Tilt=.45f,Gain=1.1f,Flicker=.25f,JetScale=1,SparkChance=.25f,Sparks=2,Wisps=1,Petals=4,FixedRoll=true,RollBase=(float)(Math.PI/4),RollJitter=.16f};break;
                default:look=default;return false;
            }
            bool standard=skin==NativeWeaponSkins.StandardIndex&&NativeWeaponModels.Designed(spec);
            look.Device=Device(spec,standard);look.Crown=Crown(spec,standard);look.Kit=kit;look.Sheet=Family(spec);
            // The Prototype Series' block face: a cross of four petals (the machine gun keeps its X), a little broader.
            if(!standard){look.Petals=4;look.FixedRoll=true;look.RollBase=spec=="machinegun"?(float)(Math.PI/4):0;look.RollJitter=.3f;look.StarWidth*=1.15f;}
            if(kit==MuzzleKit.Fan){Grow(ref look,1.12f);look.Gain*=1.15f;look.Life*=1.1f;}
            else if(kit==MuzzleKit.Overdrive){float h=Math.Max(0,Math.Min(1,heat));look.Heat=h;Grow(ref look,1.1f+.15f*h);look.Gain*=1.2f+.3f*h;look.Life*=1.05f;}
            if(standard)
            {
                look.Sockets=sockets;look.PortMask=OpenPorts(sockets,look.Device,chips,tier);look.Pods=Math.Min(Math.Max(0,chips.Extra),PodCount(sockets));
                // The vacuum intake's bell throttles the star round it.
                if(chips.Vacuum)look.Star*=.85f;
            }
            return true;
        }
        private static void Grow(ref MuzzleLook l,float k){l.Core*=k;l.Fireball*=k;l.Plume*=k;l.Star*=k;l.JetScale*=k;}
        /// <summary>The vents left open (bit k): cut at `tier` and not covered by a ricochet collar, the vacuum bell or a boomerang
        /// vane's foot (more than half the vent's length under the module).</summary>
        public static int OpenPorts(ChipSockets s,MuzzleDevice device,in ChipKinds chips,int tier)
        {
            var ports=Ports(device);int mask=0;
            for(int k=0;k<ports.Length;k++){if(tier>ports[k].Tier||s!=null&&Covered(s,chips,ports[k]))continue;mask|=1<<k;}
            return mask;
        }
        private static bool Over(in MuzzlePort p,float from,float to){float a=Math.Max(p.At.Z-p.HalfZ,from),b=Math.Min(p.At.Z+p.HalfZ,to);return b-a>p.HalfZ;}
        /// <summary>Whether a chip module covers vent `p`.</summary>
        public static bool Covered(ChipSockets s,in ChipKinds chips,in MuzzlePort p)
        {
            if(s.RicochetZ!=null)for(int i=0;i<chips.Ricochet&&i<s.RicochetZ.Length;i++)if(Over(p,s.RicochetZ[i]-CollarHalf,s.RicochetZ[i]+CollarHalf))return true;
            if(chips.Vacuum&&Over(p,s.IntakeZ-BellLength,s.IntakeZ))return true;
            if(chips.Boomerang&&s.VaneMount.Set&&Math.Abs(p.Dir.X)>.7f)
            {float k=s.VaneMount.Size*(s.PartScale>0?s.PartScale:1);if(Over(p,s.VaneMount.At.Z+VaneFoot0*k,s.VaneMount.At.Z+VaneFoot1*k))return true;}
            return false;
        }
        /// <summary>How many multishot pods a gun's sockets mount (two per pod slot, the module layout's Gun).</summary>
        public static int PodCount(ChipSockets s)=>s?.Pods==null?0:s.Pods.Length*2;
        /// <summary>Multishot pod i's mouth (source weapon frame): its slot round the bore, out along the slot's radial by the
        /// pod's standoff (PodGap + PodR), at the pod's front (PodZ + PodLength / 2); odd pods mirrored across x = 0.</summary>
        public static bool PodMouth(ChipSockets s,int i,out Vector3 at)
        {
            at=default;if(s?.Pods==null||i<0||i>=s.Pods.Length*2)return false;var slot=s.Pods[i/2];
            float r=s.PodGap+s.PodR;var p=new Vector3(slot.X+r*(float)Math.Cos(slot.Z),slot.Y+r*(float)Math.Sin(slot.Z),s.PodZ+s.PodLength*.5f);
            if(i%2==1)p.X=-p.X;at=p;return true;
        }

        // Size over life 1 -> .35 (the reference curve), alpha full to 2/3 of the life, then out.
        public static float SizeOver(double u)=>(float)(1-.65*Math.Max(0,Math.Min(1,u)));
        public static float AlphaOver(double u)=>u<0||u>=1?0:u<2/3.0?1:(float)((1-u)*3);
        // A per-shot spread: 1 +- amp from stream k of the seed.
        private static float J(long seed,long k,float amp)=>1+amp*(float)(2*NativeWeaponFx.Hash(seed,k)-1);
        private static FxColor Hot(in MuzzleLook l,int hex)=>l.Heat>0?FxColor.Lerp(NativeWeaponFx.Linear(hex),NativeWeaponFx.Linear(OverdriveColor),.6f*l.Heat):NativeWeaponFx.Linear(hex);
        /// <summary>The white-hot core (HotCore sprite) over the first .6 of the flash: its size (m) and HDR colour.</summary>
        public static bool Core(in MuzzleLook l,long seed,double age,out float size,out FxColor color)
        {
            double u=age/(l.Life*.6);size=0;color=default;if(!(u>=0&&u<1))return false;
            size=l.Core*J(seed,100,l.Flicker)*(1-.45f*(float)u);color=FxColor.Lerp(new FxColor(1,1,1),Hot(l,CoreColor),(float)u)*(4f*l.Gain*(float)((1-u)*(1-u)));return true;
        }
        /// <summary>The fireball (Soft sprite): orange, swelling a little as it fades over 1.4 flash lives.</summary>
        public static bool Fireball(in MuzzleLook l,long seed,double age,out float size,out FxColor color)
        {
            double u=age/(l.Life*1.4);size=0;color=default;if(!(u>=0&&u<1))return false;
            size=l.Fireball*J(seed,101,l.Flicker)*(.85f+.3f*(float)Math.Sqrt(u));color=FxColor.Lerp(Hot(l,PlumeColor),NativeWeaponFx.Linear(FireColor),(float)u)*(1.5f*l.Gain*(float)((1-u)*(1-u)));return true;
        }
        /// <summary>The forward plume (a flame card from the crown along the barrel): length, width (m) and opacity.</summary>
        public static bool Plume(in MuzzleLook l,long seed,double age,out float length,out float width,out float alpha)
        {
            double u=age/l.Life;length=width=alpha=0;if(!(u>=0&&u<1))return false;
            length=l.Plume*J(seed,102,l.Flicker)*SizeOver(u);width=l.PlumeWidth*J(seed,103,.15f)*SizeOver(u*.6);alpha=AlphaOver(u);return true;
        }
        /// <summary>Petal k: its roll about the barrel (radians), length and width (m) and opacity at `age`.</summary>
        public static bool Petal(in MuzzleLook l,long seed,int k,double age,out float roll,out float length,out float width,out float alpha)
        {
            double u=age/l.Life;roll=length=width=alpha=0;if(k<0||k>=l.Petals||!(u>=0&&u<1))return false;
            double turn=Math.PI*2/l.Petals,jit=NativeWeaponFx.Hash(seed,10+k)-.5;
            roll=(float)(l.FixedRoll?l.RollBase+k*turn+jit*l.RollJitter:(NativeWeaponFx.Hash(seed,0)+k/(double)l.Petals)*Math.PI*2+jit*.7);
            length=l.Star*(.75f+.5f*(float)NativeWeaponFx.Hash(seed,14+k))*SizeOver(u);width=length*l.StarWidth;alpha=AlphaOver(u);return true;
        }
        /// <summary>Vent k's jet (k indexes Ports(Device)): the vent, the jet's length and width (m) and opacity over the first .75
        /// of the flash. False for a shut vent.</summary>
        public static bool Jet(in MuzzleLook l,long seed,int k,double age,out MuzzlePort port,out float length,out float width,out float alpha)
        {
            var ports=Ports(l.Device);port=default;length=width=alpha=0;if(k<0||k>=ports.Length||(l.PortMask&1<<k)==0)return false;
            double u=age/(l.Life*.75);if(!(u>=0&&u<1))return false;port=ports[k];
            length=port.Length*l.JetScale*J(seed,110+k,.25f)*SizeOver(u);width=port.Width*l.JetScale*SizeOver(u*.5);alpha=AlphaOver(u);return true;
        }
        /// <summary>Multishot pod i's small flash: a core sprite (size), a short plume (length) and opacity, as the main flash's
        /// first .7.</summary>
        public static bool Pod(in MuzzleLook l,long seed,int i,double age,out float size,out float plume,out float alpha)
        {
            double u=age/(l.Life*.7);size=plume=alpha=0;if(i<0||i>=l.Pods||!(u>=0&&u<1))return false;
            float j=J(seed,120+i,.2f);size=l.Core*.45f*j*(1-.4f*(float)u);plume=l.Plume*.3f*j*SizeOver(u);alpha=AlphaOver(u);return true;
        }
        /// <summary>The sparks a shot throws: none unless its seeded chance comes up (SparkChance), then 1..Sparks; at most one on
        /// Reduced effects (quality under .5) or the low graphics tier.</summary>
        public static int SparkCount(in MuzzleLook l,long seed,float quality,bool low)
        {
            if(NativeWeaponFx.Hash(seed,19)>=l.SparkChance*(quality<.5f?.5f:1))return 0;int n=1+(int)(NativeWeaponFx.Hash(seed,18)*l.Sparks);n=Math.Min(n,l.Sparks);
            return quality<.5f||low?Math.Min(n,1):n;
        }
        /// <summary>
        /// Spark k: barrel-frame offset (x, y across, z along the barrel) and velocity at `age`, its world drop (m, gravity .7 g)
        /// and opacity. 4-12 m/s in a 6 degree cone, .1-.25 s, damped.
        /// </summary>
        public static bool Spark(long seed,int k,double age,out Vector3 local,out Vector3 velocity,out float drop,out float alpha)
        {
            double life=.1+.15*NativeWeaponFx.Hash(seed,20+k),u=age/life;local=velocity=default;drop=alpha=0;if(!(u>=0&&u<1)||age<.01)return false;
            double t=age-.01,speed=4+8*NativeWeaponFx.Hash(seed,30+k),cone=.105*Math.Sqrt(NativeWeaponFx.Hash(seed,40+k)),a=NativeWeaponFx.Hash(seed,50+k)*Math.PI*2;
            var dir=new Vector3((float)(Math.Sin(cone)*Math.Cos(a)),(float)(Math.Sin(cone)*Math.Sin(a)),(float)Math.Cos(cone));
            double damp=1-.25*u;local=dir*(float)(.04+speed*t*damp);velocity=dir*(float)(speed*(1-.5*u));drop=(float)(.5*6.87*t*t);alpha=(float)(1-u*u);return true;
        }
        /// <summary>Smoke wisps a shot leaves: the look's (one on Reduced effects; none with both Reduced effects and low graphics).</summary>
        public static int WispCount(in MuzzleLook l,float quality,bool low)=>quality<.5f?(low?0:1):Math.Min(MaxWisps,l.Wisps);
        /// <summary>Wisp k: barrel-frame offset, rise (m, world up), size (m) and coverage (premultiplied, light grey).</summary>
        public static bool Wisp(long seed,int k,double age,out Vector3 local,out float rise,out float size,out float alpha)
        {
            double t=age-k*.03,u=t/WispLife;local=default;rise=size=alpha=0;if(!(u>=0&&u<1))return false;
            float h=(float)NativeWeaponFx.Hash(seed,60+k),h2=(float)NativeWeaponFx.Hash(seed,70+k);
            local=new Vector3((h-.5f)*.08f*(float)u,(h2-.5f)*.06f*(float)u,.05f+.22f*(float)(1-Math.Exp(-u*3)));rise=(float)(.3*t+.05*u);
            size=.05f+.2f*(float)Math.Sqrt(u);alpha=.16f*NativeWeaponFx.Smooth(u/.12)*(float)(1-u)*(float)(1-u);return true;
        }
        /// <summary>First person: a glow no wider than ViewShare of the view's height at its eye depth (m; tanHalf: the tangent of
        /// half the vertical field of view).</summary>
        public static float ViewSize(float size,float depth,float tanHalf)=>ViewSize(size,depth,tanHalf,ViewShare);
        /// <summary>The same under another share of the view's height (the pack flash's burst: ViewBurstShare).</summary>
        public static float ViewSize(float size,float depth,float tanHalf,float share)=>Math.Min(size,share*2*Math.Max(.05f,depth)*Math.Max(.05f,tanHalf));
        /// <summary>First person: the pack flash's burst may reach this share of the view's height (its spikes leave the view gun
        /// readable through it, where a solid glow would not), and the eye behind the gun sees it, not the flame along the
        /// barrel, so it is the view's main shape.</summary>
        public const float ViewBurstShare=.3f,ViewBurst=1.35f;

        /// <summary>
        /// The owner's muzzle-flash pack (2026-10-28: muzzle flashes "would be okay (Im okay with combining certain things like
        /// effects and lighting with realistic looks) Of course, as long as it produces proper glow + lighting"): per gun family a
        /// photographic flipbook flame along the barrel in place of the procedural plume, and a burst sprite over the fireball,
        /// both HDR so the match camera's bloom glows them. The white-hot core, the star, the vents' jets, the pods, the sparks,
        /// the smoke and the shot's scene light (NativeMuzzleLight) stay. The textures come through the pack bank
        /// (tools/pack_bank/selections/fx-muzzle.json, loaded by id in Runtime NativeWeaponFxDraw); a family the drawer holds no
        /// textures for draws exactly the procedural flash. Pure functions of the shot's seed and age, as the rest of the flash.
        /// </summary>
        public const int SheetPistol=1,SheetRifle=2,SheetMachineGun=3,SheetTurret=4,Sheets=4;
        /// <summary>The families' sheets (index family - 1): the pistol's short diamond flame (4x4, frames 4-13) and fiery star
        /// burst, the rifle's long tapering flame (2x4) and fireball burst, the machine gun's flame with sparks (2x4) and smoky
        /// burst, the hazard kit's turrets' broad flame (2x4) and the pistol's star.</summary>
        public static readonly MuzzleSheet[] SheetLooks=
        {
            new MuzzleSheet("muzzle-flame-pistol",4,4,4,10,1.3f,.5f,1.8f,"muzzle-burst-star",.95f,1.3f,1),
            new MuzzleSheet("muzzle-flame-rifle",2,4,0,8,1.12f,.42f,1.4f,"muzzle-burst-fire",.9f,1.05f,1.05f),
            new MuzzleSheet("muzzle-flame-machinegun",2,4,0,8,1.3f,.46f,1.7f,"muzzle-burst-smoke",.95f,1.3f,1),
            new MuzzleSheet("muzzle-flame-turret",2,4,0,8,1.3f,.48f,1.7f,"muzzle-burst-star",.9f,1.3f,1)
        };
        /// <summary>The pack texture ids every family takes (the selection lists exactly these).</summary>
        public static readonly string[] SheetIds={"muzzle-flame-pistol","muzzle-flame-rifle","muzzle-flame-machinegun","muzzle-flame-turret","muzzle-burst-star","muzzle-burst-fire","muzzle-burst-smoke"};
        public static bool SheetOf(int family,out MuzzleSheet sheet){bool ok=family>=1&&family<=Sheets;sheet=ok?SheetLooks[family-1]:default;return ok;}
        /// <summary>A gun's family (0 for weapons without a flash).</summary>
        public static int Family(string spec)=>spec=="pistol"?SheetPistol:spec=="rifle"?SheetRifle:spec=="machinegun"?SheetMachineGun:0;
        /// <summary>The hazard kit's turrets: the plain machine-gun look with the turret's own sheet.</summary>
        public static void Turret(ref MuzzleLook look){look.Sheet=SheetTurret;}
        /// <summary>
        /// The flipbook flame from the crown along the barrel: its length and width (m), the frame played (a float: the whole
        /// part the frame, the fraction the blend toward the next), whether it is mirrored across (seeded) and its opacity. It
        /// plays its frames once over Life flash lives and fades over the last two fifths; the size spread follows the look's flicker.
        /// </summary>
        public static bool SheetFlame(in MuzzleLook l,long seed,double age,out float length,out float width,out float frame,out bool mirror,out float alpha)
        {
            length=width=frame=alpha=0;mirror=false;if(!SheetOf(l.Sheet,out var s))return false;double u=age/(l.Life*s.Life);if(!(u>=0&&u<1))return false;
            length=l.Plume*s.Length*J(seed,150,l.Flicker*.6f);width=length*s.Width*J(seed,151,.12f);frame=s.First+(float)Math.Min(s.Frames-1.001,u*s.Frames);
            mirror=NativeWeaponFx.Hash(seed,152)<.5;alpha=u<.6?1:(float)Math.Pow((1-u)/.4,1.5);return true;
        }
        /// <summary>The burst sprite over the fireball: its size (m), roll (radians, seeded), and HDR colour (white-hot to the
        /// plume's orange, out over BurstLife flash lives).</summary>
        public static bool SheetBurst(in MuzzleLook l,long seed,double age,out float size,out float roll,out FxColor color)
        {
            size=roll=0;color=default;if(!SheetOf(l.Sheet,out var s))return false;double u=age/(l.Life*s.BurstLife);if(!(u>=0&&u<1))return false;
            size=l.Fireball*s.BurstSize*J(seed,153,l.Flicker)*(.85f+.3f*(float)Math.Sqrt(u));roll=(float)(NativeWeaponFx.Hash(seed,154)*Math.PI*2);
            color=FxColor.Lerp(Hot(l,CoreColor),Hot(l,PlumeColor),(float)u)*(2.2f*s.Gain*l.Gain*(float)Math.Pow(1-u,1.5));return true;
        }
        /// <summary>The flame's HDR colour at `age` (the plume's: hot orange cooling to the edge colour), times its opacity.</summary>
        public static FxColor SheetFlameColor(in MuzzleLook l,double age,float alpha)
        {
            SheetOf(l.Sheet,out var s);double u=Math.Max(0,Math.Min(1,age/(l.Life*Math.Max(.1f,s.Life))));
            return FxColor.Lerp(Hot(l,PlumeColor),NativeWeaponFx.Linear(EdgeColor),(float)u*.8f)*(1.6f*Math.Max(.1f,s.Gain)*l.Gain*alpha);
        }
    }
}
