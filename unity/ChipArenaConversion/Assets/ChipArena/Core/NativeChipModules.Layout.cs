using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Where each Standard Chip module sits (source weapon frame: +x the rig's left, +y up the sights or the blade's flat, +z
    // along the barrel or blade). Socket values come from the Standard Full models' own geometry (the sculpt's sizes, and the
    // blade profiles sliced from the built meshes, NativeChipModules.Parts.cs Slice; ChipModuleChecks proves each socket lies
    // on its model), not the Prototype's offsets: guns stack collars and coils on the exposed barrel or compensator, give each
    // rail part a fixed slot on the top, flank and belly runs, hang pods round the bore and cells under the magazine; staffs
    // ring the bay between the grips and mount on the head collar and the lower shaft; blades mount on the guard, pommel,
    // ricasso and along the real edge; the shield on its boss, face and rim.
    public static partial class NativeChipModules
    {
        private static Dictionary<string,ChipSockets> sockets;
        /// <summary>The socket table of a designed weapon (null otherwise); built once, on first use.</summary>
        public static ChipSockets Sockets(string spec)
        {
            if(spec==null)return null;if(sockets==null)sockets=BuildSockets();return sockets.TryGetValue(spec,out var s)?s:null;
        }
        private static Dictionary<string,ChipSockets> BuildSockets()
        {
            var d=new Dictionary<string,ChipSockets>(StringComparer.Ordinal);
            foreach(var spec in NativeWeaponModels.Specs){if(!Designed(spec))continue;var s=Family(spec)==ChipFamily.Gun?GunSockets(spec):Family(spec)==ChipFamily.Staff?StaffSockets(spec):spec=="shield"?ShieldSockets():BladeSockets(spec);s.Spec=spec;s.Family=Family(spec);d[spec]=s;}
            return d;
        }
        private const float Pi=(float)Math.PI;

        // ---- guns ----
        private static ChipSockets GunSockets(string spec)
        {
            var s=new ChipSockets();
            if(spec=="pistol")
            {
                // Compensator (.456-.498) and slide nose (.434-.456): collars; arches over the slide behind the front sight; the
                // flywheel on the frame's left flank above the trigger guard; pods down the frame flanks; under-rail on the dust
                // cover; the slide flanks for side parts; cells under the raked grip's base plate.
                s.Sections=new[]{ChipSection.Box(.049f,.0405f,.0345f,.008f),ChipSection.Box(.052f,.047f,.035f,.008f),ChipSection.Box(.052f,.047f,.035f,.008f,true)};
                s.RicochetZ=new[]{.487f,.469f,.447f};s.RicochetS=new byte[]{0,0,1};s.VelocityZ=new[]{.395f,.379f,.363f,.347f};s.VelocityS=new byte[]{2,2,2,2};
                s.Flywheel=At(.048f,-.04f,.105f,Ry(Pi/2));
                s.Device=s.Sections[0];s.DeviceZ=.462f;s.DeviceR=.074f;s.IntakeZ=.493f;s.IntakeR=.066f;s.VaneMount=At(.049f,.0345f,.476f,Rz(-Pi/2),.8f);
                s.Pods=new[]{new Vector3(.048f,-.0225f,0),new Vector3(.048f,-.0435f,0),new Vector3(.048f,-.0645f,0)};s.PodZ=.36f;s.PodLength=.15f;s.PodR=.0095f;s.PodGap=.003f;s.PodBridges=new[]{-.05f,.05f};
                s.Top=default;s.Side=new ChipRun{X=.052f,Y=.024f,Z0=-.05f,Z1=.3f};s.Under=new ChipRun{X=0,Y=-.074f,Z0=.18f,Z1=.43f};
                var down=Vector3.Transform(-Vector3.UnitY,Rx(.22f));s.MagAt=new Vector3(0,-.2604f,-.0767f)+down*.007f;s.MagTurn=Rx(.22f);s.MagCell=new Vector3(.094f,.005f,.104f);
                // Fixed slots (z): the under-rail laser, prism and launcher; the slide flanks' fins and dampers (paired), the charges
                // on the right (-x) flank and the capacitors on the left; no top rail (it would sit in the sights).
                s.LanceLength=.08f;s.PistonLength=.11f;s.LauncherLength=.08f;s.PartScale=.95f;
                s.LanceZ=.385f;s.LanceUnder=true;s.SeekerZ=float.NaN;s.FinZ=.26f;s.KineticZ=.15f;s.ChargesZ=.045f;s.ChargesTop=false;s.CapsZ=.045f;s.PrismZ=.31f;s.PrismY=-.074f;s.LauncherZ=.225f;s.LauncherY=-.074f;
            }
            else if(spec=="rifle")
            {
                // The exposed fluted barrel (.80-1.097, r .0255) takes the rate coil, the coils and the collars ahead of the
                // handguard; the brake carries the intake, gimbal and vanes; the long top rail ahead of the scope, the handguard
                // flanks over the M-LOK slots and its belly ahead of the folded bipod are the runs; pods tuck into the handguard.
                s.Sections=new[]{ChipSection.Circle(.0255f)};
                s.RicochetZ=new[]{1.083f,1.064f,1.045f};s.RicochetS=new byte[3];s.VelocityZ=new[]{1.017f,1.002f,.987f,.972f};s.VelocityS=new byte[4];
                s.HelixZ=.885f;s.HelixLength=.1f;s.HelixR=.034f;
                s.Device=ChipSection.Box(.032f,.028f,0,.011f);s.DeviceZ=1.15f;s.DeviceR=.052f;s.IntakeZ=1.198f;s.IntakeR=.048f;s.VaneMount=At(.032f,0,1.15f,Rz(-Pi/2),.9f);
                s.Pods=new[]{Slot(.0255f,0),Slot(.0255f,-Pi/4),Slot(.0255f,Pi/4)};s.PodZ=.93f;s.PodLength=.3f;s.PodR=.011f;s.PodGap=.0135f;s.PodBridges=new[]{.025f};
                s.Top=new ChipRun{Y=.066f,Z0=.33f,Z1=.78f};s.Side=new ChipRun{X=.046f,Y=-.008f,Z0=.3f,Z1=.77f};s.Under=new ChipRun{Y=-.053f,Z0=.27f,Z1=.5f};
                var down=Vector3.Transform(-Vector3.UnitY,Rx(-.12f));s.MagAt=new Vector3(0,-.164f,.078f)+down*.006f;s.MagTurn=Rx(-.12f);s.MagCell=new Vector3(.06f,.012f,.094f);
                s.LanceLength=.16f;s.PistonLength=.16f;s.LauncherLength=.13f;s.PartScale=1.1f;
                s.LanceZ=.69f;s.SeekerZ=.585f;s.ChargesZ=.505f;s.ChargesTop=true;s.FinZ=.725f;s.KineticZ=.59f;s.CapsZ=.455f;s.PrismZ=.47f;s.PrismY=-.053f;s.LauncherZ=.345f;s.LauncherY=-.053f;
            }
            else
            {
                // Machine gun: collars on the barrel between the gas block and the hider, coils and the rate coil round the vented
                // shroud, pods round the shroud, the feed cover under the carry handle for top parts, the receiver's upper flanks,
                // under the gas block and barrel, and cell discs stacking off the drum's left face.
                s.Sections=new[]{ChipSection.Circle(.019f),ChipSection.Circle(.056f,.012f)};
                s.RicochetZ=new[]{.821f,.802f,.783f};s.RicochetS=new byte[3];s.VelocityZ=new[]{.655f,.637f,.619f,.601f};s.VelocityS=new byte[]{1,1,1,1};
                s.HelixZ=.46f;s.HelixLength=.12f;s.HelixR=.066f;s.HelixY=.012f;
                s.Device=ChipSection.Circle(.026f);s.DeviceZ=.862f;s.DeviceR=.04f;s.IntakeZ=.89f;s.IntakeR=.04f;s.VaneMount=At(.026f,0,.865f,Rz(-Pi/2),.75f);
                s.Pods=new[]{Slot(.056f,0,.012f),Slot(.056f,-Pi*2/9,.012f),Slot(.056f,Pi*2/9,.012f)};s.PodZ=.55f;s.PodLength=.3f;s.PodR=.012f;s.PodGap=.017f;s.PodBridges=new[]{0,.13f};
                s.Top=new ChipRun{Y=.136f,Z0=.02f,Z1=.185f};s.Side=new ChipRun{X=.076f,Y=.075f,Z0=-.12f,Z1=.29f};s.Under=new ChipRun{Y=-.029f,Z0=.705f,Z1=.83f};
                s.MagAt=new Vector3(-.068f,-.19f,.2f);s.MagTurn=Rz(-Pi/2);s.MagCell=new Vector3(.062f,.006f,0);s.MagDisc=true;
                s.LanceLength=.12f;s.PistonLength=.13f;s.LauncherLength=.1f;s.PartScale=1.12f;
                s.LanceZ=.12f;s.SeekerZ=.04f;s.ChargesZ=.155f;s.ChargesTop=false;s.FinZ=.25f;s.KineticZ=.03f;s.CapsZ=.155f;s.PrismZ=.35f;s.PrismY=-.042f;s.LauncherZ=.77f;s.LauncherY=-.029f;
            }
            return s;
        }
        // A pod slot on a round bore of radius r about (0, cy): the surface point and the outward angle.
        private static Vector3 Slot(float r,float theta,float cy=0)=>new Vector3(r*(float)Math.Cos(theta),cy+r*(float)Math.Sin(theta),theta);
        private static int Gun(ChipSockets s,ChipKinds k,ChipModule[] o)
        {
            int n=0;float ps=s.PartScale;var none=Quaternion.Identity;
            // Multishot: auxiliary barrels clamped round the bore, one each side per pair, side first, then below, then above.
            for(int i=0;i<k.Extra&&i<s.Pods.Length*2;i++)
            {
                var slot=s.Pods[i/2];var m=new ChipMount{At=new Vector3(slot.X,slot.Y,s.PodZ),Turn=Rz(slot.Z-Pi/2),Size=1,Set=true};
                if(i%2==0)Put(o,ref n,ChipPart.Pod,PodGlow,m.At,m.Turn);else Put(o,ref n,ChipPart.Pod,PodGlow,new Vector3(-m.At.X,m.At.Y,m.At.Z),Mirrored(m.Turn));
            }
            // Ricochet deflector collars from the muzzle back, velocity accelerator coils behind them (the source's ring stacks).
            for(int i=0;i<k.Ricochet&&i<s.RicochetZ.Length;i++)Put(o,ref n,ChipPart.Collar,Ricochet,new Vector3(0,s.Sections[s.RicochetS[i]].Y,s.RicochetZ[i]),none,1,s.RicochetS[i]);
            for(int i=0;i<k.Velocity&&i<s.VelocityZ.Length;i++)Put(o,ref n,ChipPart.Coil,Velocity,new Vector3(0,s.Sections[s.VelocityS[i]].Y,s.VelocityZ[i]),none,1,s.VelocityS[i]);
            // The rate coil spins about the barrel (the pistol's flywheel on its flank).
            if(k.Rate>0)
            {
                if(s.HelixLength>0){if(Put(o,ref n,ChipPart.Helix,Rate,new Vector3(0,s.HelixY,s.HelixZ),none))o[n-1].Spin=true;}
                else if(s.Flywheel.Set&&Put(o,ref n,ChipPart.Flywheel,Rate,s.Flywheel.At,s.Flywheel.Turn,ps))o[n-1].Spin=true;
            }
            // The muzzle device: the vacuum intake on its face, the pong rally gimbal round it, boomerang return vanes on its flanks.
            if(k.Vacuum)Put(o,ref n,ChipPart.Intake,Vacuum,new Vector3(0,s.Device.Y,s.IntakeZ),none);
            if(k.Pong)for(int i=0;i<3;i++)Put(o,ref n,ChipPart.Gimbal,Pong,new Vector3(0,s.Device.Y,s.DeviceZ),Ry(i*Pi/3));
            if(k.Boomerang)Pair(o,ref n,ChipPart.Vane,Boomerang,s.VaneMount,ps);
            // The rail slots (fixed per gun, so a module never moves when another is added): the top rail, the flanks (paired;
            // charges and capacitors one each side), the belly.
            float R=-Pi/2,L=Pi/2;
            if(k.Pierce)Put(o,ref n,ChipPart.Lance,Pierce,new Vector3(0,s.LanceUnder?s.Under.Y:s.Top.Y,s.LanceZ),s.LanceUnder?Rz(Pi):none);
            if(k.Homing){Pair(o,ref n,ChipPart.Fin,Homing,At(s.Side.X,s.Side.Y,s.FinZ,Rz(R)),ps);if(!float.IsNaN(s.SeekerZ))Put(o,ref n,ChipPart.Seeker,Homing,new Vector3(0,s.Top.Y,s.SeekerZ),none,ps);}
            if(k.Demolition)for(int i=0;i<3;i++)
            {
                float z=s.ChargesZ+(i-1)*.027f*ps;if(s.ChargesTop)Put(o,ref n,ChipPart.Charge,Demolition,new Vector3(0,s.Top.Y,z),Ry(Pi/2),ps);else Put(o,ref n,ChipPart.Charge,Demolition,new Vector3(-s.Side.X,s.Side.Y,z),Rz(L),ps);
            }
            if(k.Kinetic)Pair(o,ref n,ChipPart.Piston,Kinetic,At(s.Side.X,s.Side.Y,s.KineticZ,Rz(R)));
            if(k.Static)for(int i=0;i<3;i++)Put(o,ref n,ChipPart.Capacitor,Static,new Vector3(s.Side.X,s.Side.Y,s.CapsZ+(i-1)*.025f*ps),Rz(R),ps);
            if(k.Split)Put(o,ref n,ChipPart.Prism,Split,new Vector3(0,s.PrismY,s.PrismZ),Rz(Pi),ps);
            if(k.Shrapnel)Put(o,ref n,ChipPart.Launcher,Shrapnel,new Vector3(0,s.LauncherY,s.LauncherZ),Rz(Pi));
            // Magazine: one cell per chip (up to eight, as the source's width) stacked down the magazine, then the end cap.
            if(k.Magazine>0)Cells(s,Math.Min(8,k.Magazine),o,ref n);
            Rails(k,o,ref n);
            return n;
        }
        // Reach and echo strike on a gun or staff: the flank rails (NativeChipModules.Parts.cs RunRails).
        private static void Rails(ChipKinds k,ChipModule[] o,ref int n){if(k.Reach)Put(o,ref n,ChipPart.EdgeRail,Reach,Vector3.Zero,Quaternion.Identity);if(k.Echo)Put(o,ref n,ChipPart.Echo,Echo,Vector3.Zero,Quaternion.Identity);}
        private static void Cells(ChipSockets s,int cells,ChipModule[] o,ref int n)
        {
            var down=Vector3.Transform(-Vector3.UnitY,s.MagTurn);float h=s.MagCell.Y;
            for(int c=0;c<cells;c++)Put(o,ref n,ChipPart.Cell,Magazine,s.MagAt+down*(h*(c+.5f)),s.MagTurn);
            Put(o,ref n,ChipPart.Cap,Magazine,s.MagAt+down*(h*cells),s.MagTurn);
        }

        // ---- staffs (the four schools and L33t H4x: the left-hand staff, round about z) ----
        private static ChipSockets StaffSockets(string spec)
        {
            var s=new ChipSockets();bool leet=spec=="leet";
            if(!leet)
            {
                // The shared staff body (NativeWeaponModels.StaffBody at x/y 1.711): the bay between the grips (.10-.33, r .032)
                // takes the rate coil, the coils and the collars; the head collar (.48-.58) the radial mounts; the lower shaft
                // (-.33..-.10) the pistons, the charges and the cells; the heel the frag collar and the penetrator sleeve.
                s.Sections=new[]{ChipSection.Circle(.0322f)};s.RicochetZ=new[]{.318f,.3f,.282f};s.RicochetS=new byte[3];s.VelocityZ=new[]{.243f,.228f,.213f,.198f};s.VelocityS=new byte[4];
                s.HelixZ=.145f;s.HelixLength=.07f;s.HelixR=.046f;s.ShaftR=.0335f;s.LowZ0=-.31f;s.LowZ1=-.13f;s.ChargeZ=-.118f;s.HeelZ=-.322f;s.SpikeZ=-.37f;s.SpikeSize=.75f;
                s.CollarZ=.52f;s.CollarR=.056f;s.SeekerAt=new Vector3(0,.052f,.506f);s.StaticZ=.497f;s.StaticR=.045f;
                float headZ=spec=="fire"?.75f:spec=="water"?.745f:spec=="lightning"?.79f:.775f,headR=spec=="fire"?.094f:spec=="water"?.112f:spec=="lightning"?.1f:.155f;
                // Drones circle where the head is narrower: under the fire claws, round the water cage's foot and the lightning
                // coil stack, and round the void orb inside the darkness crescent's horns.
                s.HeadZ=headZ;s.HeadR=headR;s.IntakeR=headR+.022f;s.GimbalR=headR+.038f;
                // The darkness head is a flat crescent (x +-.153, y +-.03): its rings and drones stay inside the horns, round the orb.
                if(spec=="darkness"){s.IntakeR=.124f;s.GimbalR=.112f;}
                s.DroneZ=spec=="fire"?.69f:spec=="water"?.62f:spec=="lightning"?.645f:.775f;s.DroneR=spec=="fire"?.115f:spec=="water"?.1f:spec=="lightning"?.082f:.088f;s.DroneSize=.85f;
            }
            else
            {
                // L33t H4x: the hex shaft (r .041 to its corners) between the bands above the cyberdeck for the rings, the drones
                // (lower, between two bands: up by the head they would cross the first-person crosshair), the emitter collar
                // (1.06-1.13) for radial mounts, the octahedron's equator for the ring and gimbal, the lower shaft under the deck.
                s.Sections=new[]{ChipSection.Circle(.0425f)};s.RicochetZ=new[]{1.033f,1.015f,.997f};s.RicochetS=new byte[3];s.VelocityZ=new[]{.86f,.845f,.83f,.815f};s.VelocityS=new byte[4];
                s.HelixZ=.65f;s.HelixLength=.12f;s.HelixR=.056f;s.ShaftR=.04f;s.LowZ0=-.33f;s.LowZ1=-.19f;s.ChargeZ=-.08f;s.HeelZ=-.44f;s.SpikeZ=-.455f;s.SpikeSize=.7f;
                s.CollarZ=1.095f;s.CollarR=.05f;s.SeekerAt=new Vector3(0,.046f,1.083f);s.StaticZ=1.07f;s.StaticR=.036f;s.HeadZ=1.33f;s.HeadR=.13f;s.DroneZ=.472f;s.DroneR=.085f;s.DroneSize=1;s.IntakeR=.165f;s.GimbalR=.2f;
            }
            s.MagCell=new Vector3(.03f,.012f,.03f);s.PistonLength=.14f;s.PartScale=1;
            return s;
        }
        private static int Staff(ChipSockets s,ChipKinds k,int weaponColor,ChipModule[] o)
        {
            int n=0;var none=Quaternion.Identity;bool leet=s.Spec=="leet";
            for(int i=0;i<k.Ricochet&&i<s.RicochetZ.Length;i++)Put(o,ref n,ChipPart.Collar,Ricochet,new Vector3(0,0,s.RicochetZ[i]),none);
            for(int i=0;i<k.Velocity&&i<s.VelocityZ.Length;i++)Put(o,ref n,ChipPart.Coil,Velocity,new Vector3(0,0,s.VelocityZ[i]),none);
            if(k.Rate>0&&Put(o,ref n,ChipPart.Helix,Rate,new Vector3(0,0,s.HelixZ),none))o[n-1].Spin=true;
            // Multishot: hardlight drones circling just under the head (the source's orbiting orbs, in the weapon colour).
            for(int i=0;i<k.Extra&&n<o.Length;i++){Put(o,ref n,ChipPart.Drone,weaponColor,new Vector3(0,0,s.DroneZ),none,s.DroneSize);o[n-1].Orbit=(sbyte)i;o[n-1].Orbits=(byte)k.Extra;o[n-1].Radius=s.DroneR;o[n-1].RadiusY=s.DroneR;}
            // The head: a hardlight vacuum ring round its widest point, the pong gimbal of three rings tilted off that plane.
            if(k.Vacuum)Put(o,ref n,ChipPart.Intake,Vacuum,new Vector3(0,0,s.HeadZ),none);
            if(k.Pong)for(int i=0;i<3;i++)Put(o,ref n,ChipPart.Gimbal,Pong,new Vector3(0,0,s.HeadZ),Rz(i*Pi*2/3)*Rx(.42f));
            // The collar ring: homing fins (0, pi) and a seeker eye, boomerang vanes, splitter prisms and static capacitors between.
            float r=s.CollarR,cz=s.CollarZ;
            if(k.Homing){Pair(o,ref n,ChipPart.Fin,Homing,Radial(r,0,cz));Put(o,ref n,ChipPart.Seeker,Homing,s.SeekerAt,none,.8f);}
            if(k.Boomerang)Pair(o,ref n,ChipPart.Vane,Boomerang,Radial(r,Pi/3,cz));
            if(k.Split)Pair(o,ref n,ChipPart.Prism,Split,Radial(r-.004f,-Pi/3,cz),.85f);
            if(k.Static)for(int i=0;i<3;i++){var m=Radial(s.StaticR,Pi*(.5f+i*2/3f),s.StaticZ);Put(o,ref n,ChipPart.Capacitor,Static,m.At,m.Turn,.75f);}
            // The lower shaft: twin kinetic dampers, three charges strapped under the grip, the cells down its belly; the heel.
            float low=(s.LowZ0+s.LowZ1)/2;
            if(k.Kinetic)Pair(o,ref n,ChipPart.Piston,Kinetic,At(s.ShaftR,0,low,Rz(-Pi/2)));
            if(k.Demolition)for(int i=0;i<3;i++){var m=Radial(s.ShaftR,Pi/2+i*Pi*2/3,s.ChargeZ);Put(o,ref n,ChipPart.Charge,Demolition,m.At,m.Turn,.9f);}
            if(k.Magazine>0){int cells=Math.Min(8,k.Magazine);for(int c=0;c<cells;c++)Put(o,ref n,ChipPart.Cell,Magazine,new Vector3(0,-s.ShaftR-.012f,s.LowZ1-.012f-c*s.MagCell.Y),Rx(Pi/2));}
            if(k.Shrapnel)Put(o,ref n,ChipPart.Frag,Shrapnel,new Vector3(0,0,s.HeelZ),none,leet?1.1f:1);
            if(k.Pierce)Put(o,ref n,ChipPart.Spike,Pierce,new Vector3(0,0,s.SpikeZ),Rx(Pi),s.SpikeSize);
            Rails(k,o,ref n);
            return n;
        }

        // ---- blades (sword, daggers, greatsword; the right-hand blade, the Runtime mirrors the left dagger) ----
        private static ChipSockets BladeSockets(string spec)
        {
            var s=new ChipSockets();var d=NativeWeaponModels.Get(spec);Profile(spec,s);s.Mirror=d.MirrorLeft;
            if(spec=="sword")
            {
                // Guard bar (.205 + .024u^2, x +-.165): split prisms, kinetic weights and static arc horns outward along it; the
                // wheel pommel (-.156) the gimbal and cells, the grip above it the charges; the ricasso (.232-.30) the spinner; the
                // blade base the collars and coils; the blade's own edges and flats the strips (.33 to 1.19).
                s.Sections=new[]{ChipSection.Box(.0505f,.0135f,0,.006f)};s.RicochetZ=new[]{.39f,.371f,.352f};s.RicochetS=new byte[3];s.VelocityZ=new[]{.47f,.455f,.44f,.425f};s.VelocityS=new byte[4];
                s.Spinner=At(0,0,.266f,Quaternion.Identity);s.SpinnerR=.05f;
                s.Split=At(.066f,0,.216f,Rz(Pi/2)*Rx(Pi/2),.75f);s.Kinetic=At(.104f,0,.218f,Quaternion.Identity);s.Static=At(.138f,0,.236f,Rx(Pi/2),.75f);
                s.Homing=At(0,.023f,.205f,Quaternion.Identity,.7f);s.Vane=At(.047f,0,-.156f,Rz(-Pi/2),.8f);s.TipZ=1.185f;
                s.Charges=At(0,0,-.095f,Quaternion.Identity,.0285f);s.Gimbal=At(0,0,-.156f,Quaternion.Identity);s.GimbalR=.068f;s.Cells=At(0,0,-.199f,Rx(Pi/2));
                s.Vacuum=At(0,0,.95f,Quaternion.Identity);s.VacuumR=.068f;s.DroneZ=.5f;s.DroneR=.1f;s.DroneRY=.04f;s.StripZ0=.33f;s.StripZ1=1.19f;s.DoubleEdge=true;s.EdgeSign=1;s.FlameSize=.9f;
                s.Sawback=At(0,0,0,Quaternion.Identity);s.MagCell=new Vector3(.024f,.005f,.024f);
            }
            else if(spec=="daggers")
            {
                // The curved dagger (edge +x, spine -x): the guard block at .168, the skull-crusher cap (-.186..-.14), the blade base
                // (.18-.30) for the spinner, collars and coils, strips along the real edge and the flats (.20 to .78).
                s.Sections=new[]{ChipSection.Box(.037f,.009f,0,.004f)};s.SectionX=.013f;s.RicochetZ=new[]{.318f,.301f,.284f};s.RicochetS=new byte[3];s.VelocityZ=new[]{.39f,.376f,.362f,.348f};s.VelocityS=new byte[4];
                s.Spinner=At(.006f,0,.222f,Quaternion.Identity);s.SpinnerR=.047f;
                s.Split=At(-.02f,.0195f,.162f,Quaternion.Identity,.6f);s.Kinetic=At(.042f,0,.168f,Quaternion.Identity,.75f);s.Static=At(0,.029f,-.166f,Quaternion.Identity,.6f);
                s.Homing=At(.03f,.0195f,.168f,Quaternion.Identity,.6f);s.Vane=At(.029f,0,-.162f,Rz(-Pi/2),.6f);s.TipZ=.75f;
                s.Charges=At(0,0,-.115f,Quaternion.Identity,.023f);s.Gimbal=At(0,0,-.165f,Quaternion.Identity);s.GimbalR=.048f;s.Cells=At(0,0,-.186f,Rx(Pi/2));
                s.Vacuum=At(.02f,0,.6f,Quaternion.Identity);s.VacuumR=.05f;s.DroneZ=.36f;s.DroneX=.015f;s.DroneR=.072f;s.DroneRY=.034f;s.StripZ0=.21f;s.StripZ1=.79f;s.DoubleEdge=false;s.EdgeSign=1;s.FlameSize=.65f;
                s.Sawback=At(0,0,0,Quaternion.Identity);s.MagCell=new Vector3(.022f,.0045f,.022f);
            }
            else
            {
                // Greatsword: the wide guard (.228, x +-.235) for prisms, weights and arc horns, the pear pommel (-.238) for the
                // gimbal and cells, the lower grip the charges, the wrapped ricasso (.29-.43) the spinner, the blade base (.48+) the
                // collars and coils, the strips along the edges and flats (.5 to 1.6).
                s.Sections=new[]{ChipSection.Box(.077f,.0165f,0,.007f)};s.RicochetZ=new[]{.535f,.516f,.497f};s.RicochetS=new byte[3];s.VelocityZ=new[]{.62f,.605f,.59f,.575f};s.VelocityS=new byte[4];
                s.Spinner=At(0,0,.36f,Quaternion.Identity);s.SpinnerR=.05f;
                s.Split=At(.09f,0,.236f,Rz(Pi/2)*Rx(Pi/2),.85f);s.Kinetic=At(.15f,0,.233f,Quaternion.Identity,1.15f);s.Static=At(.203f,0,.262f,Rx(Pi/2),.85f);
                s.Homing=At(0,.025f,.228f,Quaternion.Identity,.8f);s.Vane=At(.05f,0,-.238f,Rz(-Pi/2),.9f);s.TipZ=1.575f;
                s.Charges=At(0,0,-.165f,Quaternion.Identity,.0255f);s.Gimbal=At(0,0,-.238f,Quaternion.Identity);s.GimbalR=.075f;s.Cells=At(0,0,-.276f,Rx(Pi/2));
                s.Vacuum=At(0,0,1.25f,Quaternion.Identity);s.VacuumR=.088f;s.DroneZ=.7f;s.DroneR=.13f;s.DroneRY=.045f;s.StripZ0=.5f;s.StripZ1=1.6f;s.DoubleEdge=true;s.EdgeSign=1;s.FlameSize=1.2f;
                s.Sawback=At(0,0,0,Quaternion.Identity);s.MagCell=new Vector3(.028f,.005f,.028f);
            }
            return s;
        }
        private static int Blade(ChipSockets s,ChipKinds k,int weaponColor,ChipModule[] o)
        {
            int n=0;var none=Quaternion.Identity;
            // The blade itself: the reach hardlight edge, the echo inlays, the shrapnel sawback (strips that stretch with it).
            if(k.Reach&&Put(o,ref n,ChipPart.EdgeRail,Reach,Vector3.Zero,none))o[n-1].Deform=true;
            if(k.Echo&&Put(o,ref n,ChipPart.Echo,Echo,Vector3.Zero,none))o[n-1].Deform=true;
            if(k.Shrapnel&&Put(o,ref n,ChipPart.Sawback,Shrapnel,Vector3.Zero,none))o[n-1].Deform=true;
            if(k.Rate>0&&Put(o,ref n,ChipPart.Spinner,Rate,s.Spinner.At,none))o[n-1].Spin=true;
            if(k.Kinetic)Pair(o,ref n,ChipPart.Clamp,Kinetic,s.Kinetic);
            if(k.Static){if(s.Spec=="daggers")Opposite(o,ref n,ChipPart.Capacitor,Static,s.Static);else Pair(o,ref n,ChipPart.Capacitor,Static,s.Static);}
            if(k.Demolition)for(int i=0;i<3;i++){var m=Radial(s.Charges.Size,Pi/2+i*Pi*2/3,s.Charges.At.Z);Put(o,ref n,ChipPart.Charge,Demolition,m.At,m.Turn,.85f);}
            if(k.Vacuum)Put(o,ref n,ChipPart.Intake,Vacuum,s.Vacuum.At,none);
            if(k.Pong)for(int i=0;i<3;i++)Put(o,ref n,ChipPart.Gimbal,Pong,s.Gimbal.At,Ry(i*Pi/3));
            // Drones fly a flat ellipse round the blade, close to its plane (the blade is thin across its flats).
            for(int i=0;i<k.Extra&&n<o.Length;i++){Put(o,ref n,ChipPart.Drone,0,new Vector3(s.DroneX,0,s.DroneZ),none,s.FlameSize*.85f);o[n-1].Orbit=(sbyte)i;o[n-1].Orbits=(byte)k.Extra;o[n-1].Radius=s.DroneR;o[n-1].RadiusY=s.DroneRY;}
            if(k.Homing)Opposite(o,ref n,ChipPart.Seeker,Homing,s.Homing);
            if(k.Split){if(s.Spec=="daggers")Opposite(o,ref n,ChipPart.Prism,Split,s.Split);else Pair(o,ref n,ChipPart.Prism,Split,s.Split);}
            if(k.Pierce&&Put(o,ref n,ChipPart.Tip,Pierce,Vector3.Zero,none))o[n-1].Deform=true;
            if(k.Boomerang)Pair(o,ref n,ChipPart.Vane,Boomerang,s.Vane);
            for(int i=0;i<k.Ricochet&&i<s.RicochetZ.Length;i++)Put(o,ref n,ChipPart.Collar,Ricochet,new Vector3(s.SectionX,0,s.RicochetZ[i]),none);
            for(int i=0;i<k.Velocity&&i<s.VelocityZ.Length;i++)Put(o,ref n,ChipPart.Coil,Velocity,new Vector3(s.SectionX,0,s.VelocityZ[i]),none);
            if(k.Magazine>0){int cells=Math.Min(8,k.Magazine);for(int c=0;c<cells;c++)Put(o,ref n,ChipPart.Cell,Magazine,s.Cells.At-new Vector3(0,0,s.MagCell.Y*(c+.5f)),s.Cells.Turn);}
            for(int i=0;i<n;i++)if(o[i].Part==ChipPart.Drone)o[i].Color=weaponColor;
            return n;
        }

        // ---- the shield (the heater face toward +z, the boss at (0, .06), the rim round NativeWeaponModels.HeaterOutline) ----
        private static ChipSockets ShieldSockets()
        {
            var s=new ChipSockets();
            float Face(float x,float y)=>ShieldFace(x,y);
            s.Spinner=At(0,.06f,ShieldFace(0,.06f)+.022f,Quaternion.Identity);s.SpinnerR=.072f;
            s.DroneZ=ShieldFace(0,.06f)+.04f;s.DroneR=.17f;s.Gimbal=At(0,.06f,ShieldFace(0,.06f)+.033f,Quaternion.Identity);s.GimbalR=.104f;
            s.Vacuum=At(0,.06f,ShieldFace(0,.06f)+.004f,Quaternion.Identity);s.VacuumR=.136f;
            // Rim mounts (on the rim's outer band, +y out of the rim): ricochet clamps at the upper corners then down the sides,
            // static capacitors up top, splitter prisms and kinetic bumpers at mid height, boomerang vanes lower down.
            s.Static=Rim(.302f,.31f,0,.8f);s.Split=Rim(.3f,.17f,0,.9f);s.Kinetic=Rim(.292f,.04f,-.21f,.95f);s.Vane=Rim(.248f,-.1f,-.5f,.9f);
            s.Mounts=new[]{Rim(.27f,.412f,1.2f),Rim(.302f,.24f,0),Rim(.2f,-.181f,-.73f)};
            s.Homing=At(0,.33f,Face(0,.33f),Rx(Pi/2),.8f);s.Pierce=At(0,-.31f,.232f,Rx(Pi/2),.7f);
            s.Charges=At(0,.422f,.232f,Quaternion.Identity,.9f);s.Cells=At(0,.09f,.135f,Rx(Pi/2));s.MagCell=new Vector3(.05f,.0055f,.034f);
            s.VelocityZ=new[]{-.11f,-.165f,-.215f,-.26f};s.Sawback=default;s.FlameSize=.85f;
            return s;
        }
        // The shield's front face height at (x, y): the field's bow (NativeWeaponModels.Bow) plus the dish toward the centre.
        private static float ShieldFace(float x,float y){float r=Vector2.Distance(new Vector2(x,y),new Vector2(0,.06f));return .2475f+.0085f*Math.Max(0,1-r/.3f)+NativeWeaponModels.Bow(x)+.006f;}
        // A rim mount on the +x side at (x, y): on the rim's outer band, its +y pointing out along the rim's normal at `tilt`.
        private static ChipMount Rim(float x,float y,float tilt,float size=1)=>new ChipMount{At=new Vector3(x,y,.214f+NativeWeaponModels.Bow(x)*.5f),Turn=Rz(-Pi/2+tilt),Size=size,Set=true};
        private static int Shield(ChipSockets s,ChipKinds k,int weaponColor,ChipModule[] o)
        {
            int n=0;var none=Quaternion.Identity;
            if(k.Reach&&Put(o,ref n,ChipPart.EdgeRail,Reach,Vector3.Zero,none))o[n-1].Deform=true;
            if(k.Echo&&Put(o,ref n,ChipPart.Echo,Echo,Vector3.Zero,none))o[n-1].Deform=true;
            if(k.Shrapnel&&Put(o,ref n,ChipPart.Sawback,Shrapnel,Vector3.Zero,none))o[n-1].Deform=true;
            if(k.Rate>0&&Put(o,ref n,ChipPart.Spinner,Rate,s.Spinner.At,none))o[n-1].Spin=true;
            for(int i=0;i<k.Extra&&n<o.Length;i++){Put(o,ref n,ChipPart.Drone,weaponColor,new Vector3(0,.06f,s.DroneZ),none);o[n-1].Orbit=(sbyte)i;o[n-1].Orbits=(byte)k.Extra;o[n-1].Radius=s.DroneR;o[n-1].RadiusY=s.DroneR;}
            if(k.Pong)for(int i=0;i<3;i++)Put(o,ref n,ChipPart.Gimbal,Pong,s.Gimbal.At,Ry((i-1)*.32f)*Rx((i-1)*.2f));
            if(k.Vacuum)Put(o,ref n,ChipPart.Intake,Vacuum,s.Vacuum.At,none);
            for(int i=0;i<k.Ricochet&&i<s.Mounts.Length;i++)Pair(o,ref n,ChipPart.Clamp,Ricochet,s.Mounts[i],.9f);
            for(int i=0;i<k.Velocity&&i<s.VelocityZ.Length;i++)Put(o,ref n,ChipPart.Chevron,Velocity,new Vector3(0,s.VelocityZ[i],ShieldFace(0,s.VelocityZ[i])),none,1-i*.08f);
            if(k.Homing)Put(o,ref n,ChipPart.Seeker,Homing,s.Homing.At,Rx(Pi/2),s.Homing.Size);
            if(k.Split)Pair(o,ref n,ChipPart.Prism,Split,s.Split);
            if(k.Static)Pair(o,ref n,ChipPart.Capacitor,Static,s.Static);
            if(k.Kinetic)Pair(o,ref n,ChipPart.Clamp,Kinetic,s.Kinetic);
            if(k.Boomerang)Pair(o,ref n,ChipPart.Vane,Boomerang,s.Vane);
            if(k.Pierce)Put(o,ref n,ChipPart.Spike,Pierce,s.Pierce.At,s.Pierce.Turn,s.Pierce.Size);
            if(k.Demolition)for(int i=0;i<3;i++)Put(o,ref n,ChipPart.Charge,Demolition,s.Charges.At+new Vector3((i-1)*.07f,0,0),Rz(0),s.Charges.Size);
            if(k.Magazine>0){int cells=Math.Min(8,k.Magazine);for(int c=0;c<cells;c++)Put(o,ref n,ChipPart.Cell,Magazine,s.Cells.At-new Vector3(0,0,s.MagCell.Y*(c+.5f)),s.Cells.Turn);}
            return n;
        }

        // ---- enchants (enchantWeapon on the Standard blades and the shield) ----
        /// <summary>
        /// The Standard blade enchant: a heated edge (fire) or a venom coat (poison) along the blade's real edge and flats, with
        /// 10 + min(6, ember or poison chips, at least 1) flames licking off the real edge or motes rising off the flats, fire first,
        /// as the source's counts (weapon-visuals.ts:126-133). Appends at `start`, returns the new count; fists and guns get none.
        /// </summary>
        public static int Enchant(string spec,bool fire,bool poison,int ember,int poisonStacks,ChipModule[] o,int start)
        {
            int n=start;var s=Sockets(spec);if(s==null||s.Family!=ChipFamily.Blade&&s.Family!=ChipFamily.Shield)return n;bool shield=s.Family==ChipFamily.Shield;
            for(int e=0;e<2;e++)
            {
                bool flame=e==0;if(flame?!fire:!poison)continue;int count=10+Math.Min(6,Math.Max(1,flame?ember:poisonStacks));
                if(Put(o,ref n,flame?ChipPart.HeatEdge:ChipPart.Coat,flame?Flame:Poison,Vector3.Zero,Quaternion.Identity)){o[n-1].Deform=true;o[n-1].Enchant=(byte)(flame?NativeWeaponDressing.FireEnchant:NativeWeaponDressing.PoisonEnchant);o[n-1].Phase=-1;}
                for(int i=0;i<count&&n<o.Length;i++)
                {
                    float u=(float)i/(count-1);int side=i%2!=0?1:-1;Vector3 at,drift;Quaternion turn;
                    if(shield)
                    {
                        // Up both side rims (the source's x +-.27 rims from y -.30 to .40), off the rim's outer band.
                        float y=-.3f+u*.7f,x=side*(ShieldHalfWidth(y)+.004f);at=new Vector3(x,y,.232f+NativeWeaponModels.Bow(x)*.5f);
                        turn=Rz(side>0?-Pi/2:Pi/2)*Rx(.8f);drift=new Vector3(side,0,.6f);if(!flame){at=new Vector3(side*(ShieldHalfWidth(y)-.04f),y,ShieldFace(side*(ShieldHalfWidth(y)-.04f),y));drift=Vector3.UnitZ;turn=Quaternion.Identity;}
                    }
                    else
                    {
                        float z=s.StripZ0+.03f+u*(s.StripZ1-s.StripZ0-.08f);bool edge=s.DoubleEdge||side>0||!flame;
                        float x=side>0?s.MaxX(z):s.MinX(z);
                        if(flame){at=new Vector3(x-side*.001f,0,z);turn=Rz(side>0?-Pi/2:Pi/2)*Rx(.12f);drift=new Vector3(side,0,0);}
                        else{int flat=i%4<2?1:-1;float c=(s.MinX(z)+s.MaxX(z))/2,mx=c+side*(s.MaxX(z)-s.MinX(z))*.25f;at=new Vector3(mx,flat*(s.HalfY(z)+.003f),z);turn=Quaternion.Identity;drift=new Vector3(0,flat,0);}
                    }
                    o[n++]=new ChipModule{Part=flame?ChipPart.Flame:ChipPart.Mote,Color=flame?(i%3!=0?Flame:FlameHot):Poison,Position=at,Rotation=turn,Drift=drift,Scale=new Vector3(s.FlameSize*(flame?1:.9f)),Orbit=-1,
                        Enchant=(byte)(flame?NativeWeaponDressing.FireEnchant:NativeWeaponDressing.PoisonEnchant),Phase=i*.81f};
                }
            }
            return n;
        }
        // The heater outline's half width at height y (linear between its points on the +x side).
        private static float ShieldHalfWidth(float y)
        {
            var o=NativeWeaponModels.HeaterOutline;float best=0;
            for(int i=0;i+3<o.Length/2*2;i+=2){float x0=o[i],y0=o[i+1],x1=o[i+2],y1=o[i+3];if(x0<0||x1<0)continue;if((y-y0)*(y-y1)<=0&&Math.Abs(y1-y0)>1e-6f){float t=(y-y0)/(y1-y0);best=Math.Max(best,x0+(x1-x0)*t);}}
            return best;
        }

        // ---- per frame ----
        /// <summary>
        /// The per-frame motion: drones circle the axis (bobbing a little along it), the rate coil spins, flames flicker and sway
        /// off the edge, motes drift off the flat and swell (the source's rhythms, weapon-visuals.ts:110,134). Everything else is
        /// static. No allocation.
        /// </summary>
        public static void Animate(ref ChipModule p,float time)
        {
            if(p.Orbit>=0)
            {
                float a=time*OrbitSpeed+p.Orbit*2*Pi/Math.Max(1,(int)p.Orbits);
                float c=(float)Math.Cos(a),sn=(float)Math.Sin(a);p.Position+=new Vector3(p.Radius*c,p.RadiusY*sn,(float)Math.Sin((double)time*2+p.Orbit*2.1)*.012f);
                p.Rotation=Rz((float)Math.Atan2(p.Radius*sn,p.RadiusY*c)-Pi/2)*p.Rotation;
            }
            if(p.Spin)p.Rotation=p.Rotation*Rz(time*SpinSpeed);
            if(p.Enchant!=0&&p.Phase>=0)
            {
                bool fire=p.Enchant==NativeWeaponDressing.FireEnchant;double phase=time*(fire?7:1.2)+p.Phase;float k=(float)(Math.Sin(phase)+1)/2,s=p.Scale.X;
                if(fire){p.Scale=new Vector3(s,s*(.6f+.75f*k),s);p.Rotation=p.Rotation*Rz((float)Math.Sin(phase)*.22f);p.Position+=p.Drift*(k*.003f);}
                else{p.Position+=p.Drift*(k*.045f);p.Scale=new Vector3(s*(.5f+.9f*k));}
            }
        }
        /// <summary>
        /// The z scale and offset (z' = z * scale + offset, inside the reach-scaled weapon frame) a Standard piece takes under the
        /// melee reach stretch (NativeWeaponModels.Stretch): a Deform strip follows the blade and the rigid hilt exactly; any
        /// other piece keeps its own size and rides to where its origin lands on the stretched weapon. False: no correction.
        /// </summary>
        public static bool Frame(string spec,in ChipModule p,float reach,out float scale,out float offset)
        {
            scale=1;offset=0;var d=NativeWeaponModels.Get(spec);if(d==null||float.IsNaN(d.StretchBase)||!(reach>0)||Math.Abs(reach-1)<1e-4f||float.IsInfinity(reach))return false;
            var kind=p.Position.Z>=d.StretchBase?WeaponStretch.Blade:WeaponStretch.Rigid;
            if(p.Deform)return NativeWeaponModels.Stretch(spec,kind,reach,out scale,out offset);
            float z=p.Position.Z;scale=1/reach;offset=(NativeWeaponModels.Stretched(spec,kind,reach,z)-z)/reach;return true;
        }
    }
}
