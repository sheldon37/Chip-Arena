using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The third set (owner 2026-11-04, "3 more new abilities" a weapon; docs/WEAPON-TECHNIQUES.md, "The third set"). Generated from one table with the doc's rows.
    // All 36 authority handlers now exist; All includes this table after the original two sets.
    public static partial class NativeTechniques
    {
        public const string Disarm="disarm",Iaido="iaido",Challenge="challenge",MirrorDouble="mirror-double",Envenom="envenom",ChainHook="chain-hook",WarCry="war-cry",GroundSlam="ground-slam",Lodestone="lodestone",Sunder="sunder",ReapingSweep="reaping-sweep",BladeDrag="blade-drag",FlashRound="flash-round",GunSpin="gun-spin",SlideShot="slide-shot",Bipod="bipod",AmmoCrate="ammo-crate",FlakBurst="flak-burst",BreacherRound="breacher-round",Ghillie="ghillie",BolasRound="bolas-round",BrambleArrow="bramble-arrow",VaultShot="vault-shot",TetherArrow="tether-arrow",FlameCloak="flame-cloak",DragonBreath="dragon-breath",Combust="combust",BubbleTrap="bubble-trap",FlashFreeze="flash-freeze",HydroJet="hydro-jet",Magnetize="magnetize",Thunderclap="thunderclap",Overload="overload",SoulSiphon="soul-siphon",Dread="dread",ShadowPuppet="shadow-puppet";
        public static readonly Def[] Third=
        {
            D(Disarm,"sword","Disarm","sword",11,18,0xe8f4ff,"CONTROL"),
            D(Iaido,"sword","Iaido","swords",13,24,0xffe9b0,"BURST"),
            D(Challenge,"sword","Challenge","target",16,15,0xff6a5a,"DUEL"),
            D(MirrorDouble,"daggers","Mirror Double","copy",16,25,0xc8b4ff,"DECEPTION"),
            D(Envenom,"daggers","Envenom","flask-conical",12,15,0x8dff5a,"BUFF"),
            D(ChainHook,"daggers","Chain Hook","spline",13,22,0xd0d8e0,"CONTROL"),
            D(WarCry,"shield","War Cry","audio-lines",15,20,0xffc24a,"TAUNT"),
            D(GroundSlam,"shield","Ground Slam","chevrons-down-up",12,22,0x7fc4ff,"BURST"),
            D(Lodestone,"shield","Lodestone","magnet",14,25,0x9fd0ff,"COUNTER"),
            D(Sunder,"greatsword","Sunder","hammer",12,24,0xff8a4a,"BREAK"),
            D(ReapingSweep,"greatsword","Reaping Sweep","rotate-cw",13,24,0xd8e4f0,"CONTROL"),
            D(BladeDrag,"greatsword","Blade Drag","footprints",13,22,0xffb25a,"MOBILITY"),
            D(FlashRound,"pistol","Flash Round","sun",12,0,0xfff6c0,"BLIND"),
            D(GunSpin,"pistol","Gun Spin","rotate-cw",12,0,0xffd86a,"BUFF"),
            D(SlideShot,"pistol","Slide Shot","move-right",9,0,0xffc98a,"MOBILITY"),
            D(Bipod,"machinegun","Bipod","crosshair",15,0,0xff7a5a,"STANCE"),
            D(AmmoCrate,"machinegun","Ammo Crate","boxes",20,0,0xffd24a,"SUPPORT"),
            D(FlakBurst,"machinegun","Flak Burst","radiation",14,0,0xffa050,"DEFENCE"),
            D(BreacherRound,"rifle","Breacher Round","scan-line",14,0,0x8fe0ff,"PIERCE"),
            D(Ghillie,"rifle","Ghillie","eclipse",16,0,0x9fd08a,"STEALTH"),
            D(BolasRound,"rifle","Bolas Round","orbit",12,0,0xd0c08a,"CONTROL"),
            D(BrambleArrow,"bow","Bramble Arrow","sprout",13,0,0x7fd060,"AREA"),
            D(VaultShot,"bow","Vault Shot","corner-up-right",10,0,0xbff5e4,"MOBILITY"),
            D(TetherArrow,"bow","Tether Arrow","anchor",14,0,0xa6e0ff,"CONTROL"),
            D(FlameCloak,"fire","Flame Cloak","flame",16,26,0xff8a30,"DEFENCE"),
            D(DragonBreath,"fire","Dragon Breath","wind",13,24,0xff5a20,"CHANNEL"),
            D(Combust,"fire","Combust","zap",9,16,0xffc04a,"COMBO"),
            D(BubbleTrap,"water","Bubble Trap","circle-dot",14,24,0x7fe0ff,"CONTROL"),
            D(FlashFreeze,"water","Flash Freeze","snowflake",14,22,0xbfeeff,"AREA"),
            D(HydroJet,"water","Hydro Jet","waves",12,20,0x54d8ff,"MOBILITY"),
            D(Magnetize,"lightning","Magnetize","magnet",13,24,0xa8c0ff,"COMBO"),
            D(Thunderclap,"lightning","Thunderclap","cloud-lightning",12,26,0xd8d0ff,"BURST"),
            D(Overload,"lightning","Overload","battery-charging",14,18,0x7fb4ff,"RISK"),
            D(SoulSiphon,"darkness","Soul Siphon","heart-crack",13,22,0xc060ff,"DRAIN"),
            D(Dread,"darkness","Dread","skull",15,26,0x8a4aff,"FEAR"),
            D(ShadowPuppet,"darkness","Shadow Puppet","person-standing",16,28,0xa070ff,"SUMMON"),
        };
        /// <summary>The Locker card's sentence of a third-set technique.</summary>
        private static string ThirdText(string id)
        {
            switch(id)
            {
                case Disarm:return "A quick flick of the blade. Whoever it catches can't use Q or E for a few seconds.";
                case Iaido:return "Sheathe your blade, then press again to draw: you cross a line and cut everyone on it.";
                case Challenge:return "Call out one enemy: you hit each other harder, and you run faster at them.";
                case MirrorDouble:return "Vanish for a second while a double runs ahead of you. One hit pops it.";
                case Envenom:return "Coat your blades. Your next three hits poison and slow.";
                case ChainHook:return "Throw a hooked chain and drag the first enemy it catches to you.";
                case WarCry:return "Bang your shield: nearby enemies hurt anyone but you less, and you shrug off more.";
                case GroundSlam:return "Leap and bring the shield down: a shockwave throws everyone near the landing.";
                case Lodestone:return "Your shield pulls shots out of the air and holds them, then throws them all back.";
                case Sunder:return "A slow overhead that cracks armour. Whoever it hits takes more damage for a while.";
                case ReapingSweep:return "A low circular sweep that hooks legs: everyone near you is dragged in and tripped.";
                case BladeDrag:return "Run dragging the blade in a trail of sparks, then press again to rip it upward.";
                case FlashRound:return "A round that bursts into blinding light. Anyone looking at it is dazzled, you included.";
                case GunSpin:return "Twirl the pistol: instantly reloaded, and firing faster for a few seconds.";
                case SlideShot:return "Drop into a long slide, still shooting, and rise with a steadier aim.";
                case Bipod:return "Plant the gun: tighter, faster, free fire, but you can't move until you pack up.";
                case AmmoCrate:return "Drop a crate. Anyone on your team who touches it reloads and fires faster.";
                case FlakBurst:return "A shell bursts where you aim, into a cloud that shreds enemy shots passing through.";
                case BreacherRound:return "Charge for a second behind a visible laser, then fire a round that punches through walls.";
                case Ghillie:return "Settle down and hold still: you all but vanish, and your first shot out hits harder.";
                case BolasRound:return "A slow spinning round that ties up whoever it hits, rooting them on the spot.";
                case BrambleArrow:return "An arrow that grows a hedge of thorns where it lands. Crossing it hurts and slows.";
                case VaultShot:return "Flip backward and loose a free arrow from the top of the flip.";
                case TetherArrow:return "Pin an enemy to the floor with a glowing line. They can't stray far until it snaps.";
                case FlameCloak:return "Wrap yourself in fire: shots hurt less, and anyone who hits you up close gets burned.";
                case DragonBreath:return "Breathe a cone of fire for as long as it lasts, walking slowly, unable to do anything else.";
                case Combust:return "Snap your fingers: every enemy already burning takes all of it at once, in a blast.";
                case BubbleTrap:return "A slow orb that floats an enemy in a bubble: helpless, but protected until it pops.";
                case FlashFreeze:return "Stamp a sheet of ice under you. Enemies slide on it, and you skate.";
                case HydroJet:return "A jet of water throws you opposite your aim, and shoves whoever's in the stream.";
                case Magnetize:return "Mark two enemies and they're dragged into each other. They clash for damage and a stun.";
                case Thunderclap:return "Clap your hands: a burst of thunder stuns everyone close and cuts off whatever they were doing.";
                case Overload:return "Run hot: faster attacks and faster feet, and a shock at the end that you have to pay for.";
                case SoulSiphon:return "Hold a beam on one enemy, draining their health into yours while you can keep them.";
                case Dread:return "A scream of darkness: everyone in front of you turns and stumbles away for a moment.";
                case ShadowPuppet:return "Leave your shadow standing where you are. It copies every attack you make.";
                default:return "";
            }
        }
    }
}
