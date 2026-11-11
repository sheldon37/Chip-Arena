using System;

namespace ChipArena.UnityConversion
{
    // The visual body's own animation (owner 2026-10-24: "Pack animations whole, and adjust hit timing as needed", "Retime clips
    // to rules"; Runtime NativeVisualMotionPresentation.cs, docs/BOZO-INTEGRATION.md): which of the animation pack's humanoid
    // clips a pilot's model plays for its replicated state, and where in the clip. A weapon's set names its idle, its
    // locomotion (the stride's order: runs, walks, the sprint), its combo's attacks and its other moves. An attack plays from
    // the strike's start, its Hit (the pack's own event) on the middle of the rules' hit window, its recovery to the strike's
    // end; a roll fills the roll; a jump, the air jump's flip, the fall and the landing follow the pilot's air state. A state the
    // set does not cover (a special, a kit move, a knockdown, a juggle) returns false: the game's own pose shows there.
    // Presentation only: when and where a move hits, its reach and every rule are the game's; the clips are timed to them.
    public static partial class NativeVisualMotion
    {
        public sealed class Set
        {
            public string Spec, Idle, Jump, Flip, Fall, Land, Block, BlockHit, Stunned, Roll, AirAttack, SprintAttack;
            /// <summary>The combo's clips by attack index from 1 (cycling past the end).</summary>
            public string[] Attacks;
            /// <summary>Hit reactions from the front, back, left and right.</summary>
            public string[] Reactions;
            /// <summary>Locomotion, NativeVisualStride's order: 8 runs, 8 walks (clockwise from forward), the sprint.</summary>
            public string[] Locomotion;
            /// <summary>A gun's upper-body layer: its aim poses (down, level, up: blended by the pilot's pitch, AimPitch degrees
            /// apart), its recoil per shot (likewise; FireFromHit: the clip's Hit lands on the shot, else it starts there) and
            /// its reload (fitted to the reload). Null for melee.</summary>
            public string[] Aim, Fire; public string Reload; public float AimPitch=45; public bool FireFromHit;
            /// <summary>A bow's drawn poses (down, level, up) while it draws. Null for the rest.</summary>
            public string[] Pull;
            /// <summary>A caster's casts: each one's start (to the release) and end (after it), cycling; its Q/E ability's
            /// likewise. Null for the rest.</summary>
            public string[] CastStart, CastEnd; public string AbilityStart, AbilityEnd;
            /// <summary>Per Q/E ability (by its id): its start (to the release) and end (after it; null: the start alone at its own
            /// pace) on the upper body; AbilityRaise: the gun's abilities that only raise it onto the aim.</summary>
            public string[] AbilityIds, AbilityStarts, AbilityEnds, AbilityRaise;
            /// <summary>An unarmed fighter: its strikes follow the replicated jab and combo (the last of Attacks ends a chain).</summary>
            public bool Fighter;
            /// <summary>A fighter's own moves (FighterMove): the Emperor's electric punch and wavedash; the Ansatsuken's tatsu kicks
            /// (alternating), shoryuken, hadoken (the push), shinku charge (gathering, then held) and Demon Step.</summary>
            public string Electric, Wave, Hadoken, ChargeStart, ChargeLoop, Shoryuken, Step; public string[] Kicks;
            /// <summary>The weapon in the model's left fist is a shaft (the staff): it runs along the fist's grip.</summary>
            public bool LeftShaft;
            /// <summary>A gun (its aim layer without a bow's draw): the hold calibrated from its level aim, its barrel onto the
            /// aim.</summary>
            public bool Gun=>Aim!=null&&Pull==null&&Spec!=NativeBombs.Spec;
            /// <summary>A long gun at rest (owner 2026-11-10: "a resting pose (They're holding it downward) only show the aiming
            /// animation when were shooting / aiming"): with the arms free it stays at the low carry instead of following the look.</summary>
            public bool RestLow;
            /// <summary>A resting long gun's standing arms: a still frame (RestArmsAt, s) of a clip in the set whose arms carry it low (the
            /// pack walk's two-hand carry; the idle's own ready hold keeps the stock at the shoulder). Null: the idle's arms.</summary>
            public string RestArms; public float RestArmsAt;
            /// <summary>Standing at rest, a one-handed low ready (owner 2026-11-10, from a reference picture): the gun hand before the right
            /// hip with the muzzle to the floor ahead, the off hand at the belt beside it (Runtime VisualPistolReady).</summary>
            public bool RestReady;
            /// <summary>The kit moves (Q/E, by MeleeKitMotion's kind) the set plays on the pack's clips, each with its clip: the clip's
            /// Hit on the release, its end on the cast's end (the Whirlwind: the body turned by the game's spin). The rest keep
            /// the game's pose. Null: none.</summary>
            public string[] KitKinds, KitClips;
            /// <summary>Per kit move: where the release lands in its clip (a share of its length; NaN: the clip's own Hit;
            /// negative: the clip plays at its own pace from the cast, looping), and its recast's clip (null: the cast's).</summary>
            public float[] KitHits; public string[] KitRecast;
            /// <summary>Per kit move, a clip after the release (null: none): then the cast's clip is fitted to the release and
            /// this one to the cast's end (a throw's wind-back, then its throw).</summary>
            public string[] KitThen;
            /// <summary>A knockdown (the pack's blown-back fall: its flight, its landing, lying) and the backward roll a downed
            /// pilot gets up with.</summary>
            public string Knockdown, RollBack;
            /// <summary>The clips the set's technique plays beyond its own (NativeTechniques; the bake's controller carries them).</summary>
            public string[] TechClips;
            /// <summary>Every clip the set plays (the bake's controller carries them).</summary>
            public string[] All;
        }
        /// <summary>A clip's length and the time of its Hit event (seconds; a clip without one: its middle).</summary>
        public interface IClips { bool Has(string clip); float Length(string clip); float Hit(string clip); }

        // Greatsword (owner 2026-11-11: "Great Sword and Katana"): Grruzam's great sword set (Core NativeGreatswordMotion: its stance, shoulder
        // carry stride, jumps, guard, dodge roll, reactions, knockdown and rolling get-up, its combo's cuts alternating as the rules sweep, its
        // leaping slam on the sprint, its jump attack, its ground slam and spinning sweep for the kit, its guard for the vow); the pack has no
        // dazed loop, so the stun keeps the old two-handed set's.
        private static readonly Set Greatsword=Strides(new Set{Spec="greatsword",Idle=NativeGreatswordMotion.Idle,Jump=NativeGreatswordMotion.Jump,Flip=NativeGreatswordMotion.Flip,Fall=NativeGreatswordMotion.Fall,
            Land=NativeGreatswordMotion.Land,Block=NativeGreatswordMotion.Guard,BlockHit=NativeGreatswordMotion.GuardHit,Stunned="2Hand-Sword-Stunned",Roll=NativeGreatswordMotion.Roll,
            AirAttack=NativeGreatswordMotion.JumpCut,SprintAttack=NativeGreatswordMotion.Cleave,Attacks=NativeGreatswordMotion.Cuts,Reactions=NativeGreatswordMotion.Reactions,
            TechClips=new[]{TechThrowStart,TechThrowEnd,TechVowStart,TechVowHold,MeleeOverhead,MeleeSweep},KitKinds=new[]{"earthsplitter","whirlwind"},KitClips=new[]{NativeGreatswordMotion.Slam,NativeGreatswordMotion.Spin},KitHits=new[]{float.NaN,WhirlHold},
            Knockdown=NativeGreatswordMotion.Knockdown,RollBack=NativeGreatswordMotion.RollBack},NativeGreatswordMotion.Runs,NativeGreatswordMotion.Walks,NativeGreatswordMotion.Sprint);
        // Sword: the pack's armed (one weapon in the right hand) set and its one-handed sword's cuts, in the game's combo of
        // three (a forehand cut right to left, a backhand left to right, the thrust): R1, R3, R4, then R6, R2, R4 (their blade
        // tips' sweeps, measured on the body at the bake).
        private static readonly Set Sword=Build(new Set{Spec="sword",Idle="Armed-Idle",Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-R",BlockHit="Armed-Block-R-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Air-Attack-R1",SprintAttack="Armed-Run-Attack-R1",
            Attacks=Names("Sword-Attack-R","1","3","4","6","2","4"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            TechClips=new[]{TechCounter,TechDashBack},KitKinds=new[]{"flash-step","rising-dragon"},KitClips=new[]{"Armed-Dash-Forward","Armed-Boost1"},KitHits=new[]{.2f,float.NaN},KitRecast=new[]{null,"Armed-Air-Attack-R1"},Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward"},"Armed-Run-","Armed-Strafe-","Armed-Run-Forward");
        // The Glaive (owner 2026-11-09): the one-handed sword's stance, stride, jumps, guard and reactions under it; every move of the ring keyed
        // by hand over them (Core NativeGlaiveMotion), the skim's legs a sprint (GlaiveCast).
        private static readonly Set Glaive=Build(new Set{Spec=NativeGlaive.Spec,Idle="Armed-Idle",Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-R",BlockHit="Armed-Block-R-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Air-Attack-R1",SprintAttack="Armed-Run-Attack-R1",Attacks=Names("Sword-Attack-R","1","3","4","6"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward"},"Armed-Run-","Armed-Strafe-","Armed-Run-Forward");
        private const string GlaiveRun="Armed-Run-Forward";
        // The Katana (owner 2026-11-10: "Use the new animation pack"): the Frank Slash Pack's katana set: its first stance, its stride (the
        // root-motion runs, the guard walk its walk, its run its sprint), its guard, block, tumble and reactions; the armed set's jumps and
        // falls under it. Its moves are clip windows fitted to the rules (Core NativeBladeMotion: the whole body; a cut on the move the upper body).
        private static readonly Set Katana=Frank(new Set{Spec=NativeKatana.Spec,Idle=NativeBladeMotion.KatanaIdle,Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",Land="Armed-Land",
            Block=NativeBladeMotion.KatanaGuard,BlockHit=NativeBladeMotion.KatanaBlock,Stunned="Armed-Stunned",Roll=NativeBladeMotion.KatanaTumble,AirAttack="Armed-Air-Attack-R1",SprintAttack=NativeBladeMotion.KatanaRush,
            Attacks=NativeBladeMotion.KatanaCuts,Reactions=new[]{"Frank_RPG_Katana_Hit01","Frank_RPG_Katana_Hit02","Frank_RPG_Katana_Hit03","Frank_RPG_Katana_Hit02"},
            TechClips=NativeBladeMotion.KatanaExtra,Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward"},"Frank_RPG_Katana@Velocity_8Way_Run_","Frank_RPG_Katana@Velocity_8Way_GuardWalk_","Frank_RPG_Katana_Run01_Velocity");
        // The Fangs (owner 2026-11-10: "Testing different abilities and animation sets"): the pack's assassin set, low and quick: its stance,
        // root-motion stride, guard, block, tumble and reactions; its moves (and its stealth kills and criticals) in Core NativeBladeMotion.
        private static readonly Set Fangs=Frank(new Set{Spec=NativeFangs.Spec,Idle=NativeBladeMotion.FangsIdle,Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",Land="Armed-Land",
            Block=NativeBladeMotion.FangsGuard,BlockHit=NativeBladeMotion.FangsBlock,Stunned="Armed-Stunned",Roll=NativeBladeMotion.FangsTumble,AirAttack="Armed-Air-Attack-L1",SprintAttack=NativeBladeMotion.FangsPounce,
            Attacks=NativeBladeMotion.FangsCuts,Reactions=new[]{"Frank_RPG_Assassin_Hit01","Frank_RPG_Assassin_Hit02","Frank_RPG_Assassin_Hit03","Frank_RPG_Assassin_Hit02"},
            TechClips=NativeBladeMotion.FangsExtra,Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward"},"Frank_RPG_Assassin@Velocity_8Way_Run_","Frank_RPG_Assassin@Velocity_8Way_Walk_","Frank_RPG_Assassin_Run01_Velocity");
        /// <summary>A Frank Slash Pack set's stride (its 8-way clips end in F, FR, R, BR, B, BL, L, FL) and every clip it plays.</summary>
        private static Set Frank(Set s,string run,string walk,string sprint)
        {
            string[] dirs={"F","FR","R","BR","B","BL","L","FL"};s.Locomotion=new string[NativeVisualStride.Clips];
            for(int i=0;i<dirs.Length;i++){s.Locomotion[i]=run+dirs[i];s.Locomotion[NativeVisualStride.Walks+i]=walk+dirs[i];}
            s.Locomotion[NativeVisualStride.Sprint]=sprint;
            var all=new System.Collections.Generic.List<string>{s.Idle,s.Jump,s.Flip,s.Fall,s.Land,s.Block,s.BlockHit,s.Stunned,s.Roll,s.AirAttack,s.SprintAttack};
            foreach(var c in new[]{s.Knockdown,s.RollBack})if(c!=null&&!all.Contains(c))all.Add(c);
            foreach(var l in new[]{s.Attacks,s.Reactions,s.Locomotion,s.TechClips})if(l!=null)foreach(var c in l)if(c!=null&&!all.Contains(c))all.Add(c);
            s.All=all.ToArray();return s;
        }
        /// <summary>The clips a set plays as loops though the pack marks them single (the Frank Slash Pack's stances and strides, the Grruzam
        /// pack's fast run): its idle, its stride and its guard (the bake loops them).</summary>
        public static string[] Loops(string spec){var s=For(spec);if(s==null||!NativeBlades.Is(spec)&&spec!="greatsword")return Array.Empty<string>();var l=new System.Collections.Generic.List<string>{s.Idle,s.Block};l.AddRange(s.Locomotion);return l.ToArray();}
        // Daggers: the armed set's dual block and attacks and the pack's dagger cuts, alternating hands in the game's four
        // (a forehand right to left, then three left to right, the last the finisher): R1, L3, R2, L2 (their blade tips'
        // sweeps on the body); the dual running cut opens both blades as the game's twin sprint sweeps do.
        private static readonly Set Daggers=Build(new Set{Spec="daggers",TechClips=new[]{TechFlickStart,TechFlickEnd},Idle="Armed-Idle",Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-Dual",BlockHit="Armed-Block-Dual-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Air-Attack-L1",SprintAttack="Armed-Run-Attack-Dual",
            Attacks=Names("Dagger-Attack-","R1","L3","R2","L2"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            KitKinds=new[]{"shadow-step","kunai-mark"},KitClips=new[]{"Armed-Dash-Forward","Armed-Cast-R-Attack2_start"},KitHits=new[]{.2f,float.NaN},KitRecast=new[]{null,"Armed-Dash-Forward"},KitThen=new[]{null,"Armed-Cast-R-Attack2_end"},Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward"},"Armed-Run-","Armed-Strafe-","Armed-Run-Forward");
        // Shield: the game holds its shield in the right hand; the pack's shield set carries it on the left arm, so the set
        // plays mirrored (the bake mirrors a clip named with Mirror: left and right swapped). Its bashes and sweeps, mirrored,
        // sweep left to right as every one of the game's shield cuts does: Attack2, Attack3, Attack1.
        private static readonly Set Shield=Build(new Set{Spec="shield",TechClips=new[]{TechPlant,TechSurf},Idle=M("Armed-Shield-Idle"),Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block=M("Armed-Shield-Block"),BlockHit=M("Armed-Shield-Block-GetHit1"),Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack=M("Armed-Shield-Attack1"),SprintAttack=M("Armed-Shield-Attack1"),
            Attacks=Array.ConvertAll(Names("Armed-Shield-Attack","2","3","1"),M),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            KitKinds=new[]{"bulwark-charge","aegis-ricochet"},KitClips=new[]{M("Armed-Shield-Run-Forward-Charge"),"Armed-Cast-R-Attack2_start"},KitHits=new[]{-1f,float.NaN},KitThen=new[]{null,"Armed-Cast-R-Attack2_end"},Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward"},"Armed-Shield-Run-","Armed-Shield-Strafe-",M("Armed-Shield-Run-Forward"),true);
        // Rifle and machine gun: the pack's two-handed shooting set. A low ready carry at rest, idle or moving (RestLow; owner
        // 2026-11-10); shouldered (its aim grid's middle column by the pitch, a recoil on each shot) while aiming or just after
        // a shot (owner 2026-10-24: "Aiming and firing"), the reload fitted to the reload.
        private static readonly Set Rifle=Build(new Set{Spec="rifle",RestLow=true,RestArms="2Hand-Shooting-Strafe-Forward",TechClips=new[]{TechDrop,TechShootLine},Idle=StandIdle,Jump="2Hand-Shooting-Jump",Flip="2Hand-Shooting-Jump-Flip",Fall="2Hand-Shooting-Fall",
            Land="2Hand-Shooting-Land",Block="2Hand-Shooting-Block",BlockHit="2Hand-Shooting-Block-GetHit1",Stunned="2Hand-Shooting-Stunned",Roll="2Hand-Shooting-Roll-Forward",
            AirAttack="2Hand-Shooting-Fall",SprintAttack="2Hand-Shooting-Attack1",Attacks=Names("2Hand-Shooting-Attack","1","2"),Reactions=Names("2Hand-Shooting-GetHit-","F1","B1","L1","R1"),
            Aim=Names("2Hand-Shooting-Aiming-","DM","CM","UM"),Fire=Names("2Hand-Shooting-Aiming-Fire-","DM","CM","UM"),Reload="2Hand-Shooting-Reload-Rifle",Knockdown="2Hand-Shooting-Knockdown1",RollBack="2Hand-Shooting-Roll-Backward",
            AbilityIds=new[]{"overdrive"},AbilityStarts=new[]{"2Hand-Shooting-Boost1"},AbilityEnds=new string[1],AbilityRaise=new[]{"orbital-mark","rocket"}},"2Hand-Shooting-Run-","2Hand-Shooting-Strafe-","2Hand-Shooting-Run-Forward");
        // The guns stand on the pack bow idle's natural stance, upright with the feet about shoulder width (owner 2026-11-10: "maybe
        // not have their legs spread out like they currently are ... more realistic"): the armed set's idle is a melee fighting crouch,
        // the rifle's a wide combat stance. A raise lays the upper body over it; the legs keep the stance, so it never shifts on a shot.
        public const string StandIdle="2Hand-Bow-Idle";
        // Pistol: the armed set, held low at rest (RestLow; owner 2026-11-10: "apply a resting position to pistols as well"); raised
        // to the pack's pistol stance (arm out) while aiming or just after a shot (its
        // own firing cut throws the arm overhead: the stance holds, the game's muzzle kick is the recoil), its reload fitted.
        private static readonly Set Pistol=Build(new Set{Spec="pistol",RestLow=true,RestReady=true,TechClips=new[]{TechCoin},Idle=StandIdle,Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-R",BlockHit="Armed-Block-R-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Fall",SprintAttack="Pistol-Attack-R1",Attacks=Names("Pistol-Attack-R","1"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            Aim=new[]{"Armed-Idle-Pistol-R","Armed-Idle-Pistol-R","Armed-Idle-Pistol-R"},Reload="Pistol-Reload-R1",Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward",
            AbilityIds=new[]{"frag-grenade"},AbilityStarts=new[]{"Armed-Cast-L-Attack2_start"},AbilityEnds=new[]{"Armed-Cast-L-Attack2_end"}},"2Hand-Bow-Run-","2Hand-Bow-Strafe-","2Hand-Bow-Run-Forward");
        // The Bombadier: a bundle of TNT in each fist (owner 2026-11-04: "holding TNTs instead of a grenade launcher. Modify animations however needed"), so the pack's bare-handed set (upright, the fists up by the chest, so the TNT
        // shows from behind), its casts from the one-handed set. Held fire throws overhand, a hand at a time; nothing is raised to aim (Layer: the stance's own arms carry on, so its Aim is only there to make the reload layer run); a reload is the right hand going to the satchel on the hip.
        // Its abilities are in BombUpper (thrown on the move, the arms only) and BombCast (the whole body).
        private static readonly Set Bomb=Build(new Set{Spec=NativeBombs.Spec,TechClips=new[]{BombThrowStart,BombThrowEnd,BombThrowStart+Mirror,BombThrowEnd+Mirror,TechThrowStart,TechThrowEnd,TechFlickStart,TechFlickEnd,BombHeaveStart,BombHeaveEnd,BombLay,BombClick,BombStoop,BombSlam},
            Idle="Unarmed-Idle",Jump="Unarmed-Jump",Flip="Unarmed-Jump-Flip",Fall="Unarmed-Fall",Land="Unarmed-Land",Block="Unarmed-Block",BlockHit="Unarmed-Block-GetHit1",Stunned="Unarmed-Stunned",Roll="Unarmed-Roll-Forward",
            AirAttack="Unarmed-Fall",SprintAttack="Unarmed-Sprint",Attacks=new[]{BombThrowEnd},Reactions=Names("Unarmed-GetHit-","F1","B1","L1","R1"),Aim=new[]{"Unarmed-Idle","Unarmed-Idle","Unarmed-Idle"},Reload=BombReload,Knockdown="Unarmed-Knockdown1",RollBack="Unarmed-Roll-Backward"},"Unarmed-Run-","Unarmed-Strafe-","Unarmed-Sprint");
        // The Top Dog (owner 2026-11-08: a cartoon mallet, swung two-handed): the pack's two-handed sword set, its casts in DogCast (the whole body).
        private static readonly Set TopDog=Build(new Set{Spec=NativeTopDog.Spec,Idle="2Hand-Sword-Idle",Jump="2Hand-Sword-Jump",Flip="2Hand-Sword-Jump-Flip",Fall="2Hand-Sword-Fall",
            Land="2Hand-Sword-Land",Block="2Hand-Sword-Block",BlockHit="2Hand-Sword-Block-GetHit1",Stunned="2Hand-Sword-Stunned",Roll="2Hand-Sword-Roll-Forward",
            AirAttack="2Hand-Sword-Air-Attack1",SprintAttack="2Hand-Sword-Run-Forward-Attack1",
            Attacks=Names("2Hand-Sword-Attack","1","2","11"),Reactions=Names("2Hand-Sword-GetHit-","F1","B1","L1","R1"),
            TechClips=new[]{DogPoint,DogPointEnd,TechFlickStart,TechFlickEnd,TechThrowStart,TechThrowEnd,DogPunch,DogPunchL,DogKick,DogRun},Knockdown="2Hand-Sword-Knockdown1",RollBack="2Hand-Sword-Roll-Backward"},"2Hand-Sword-Run-","2Hand-Sword-Strafe-","2Hand-Sword-Run-Forward");
        // The casters (the four schools and L33t H4x): the pack's staff set, mirrored (the game's staff is in the left fist, the
        // pack's in the right); a cast plays its start to the game's release and its end after it, a Q/E ability the pack's area
        // cast likewise, on the upper body. Mirrored reactions swap sides.
        private static readonly Set Staff=Build(new Set{Spec="staff",TechClips=new[]{TechBuffStart,TechBuffEnd,TechTouch,TechSink},Idle=M("2Hand-Staff-Idle"),Jump=M("2Hand-Staff-Jump"),Flip=M("2Hand-Staff-Jump-Flip"),Fall=M("2Hand-Staff-Fall"),
            Land=M("2Hand-Staff-Land"),Block=M("2Hand-Staff-Block"),BlockHit=M("2Hand-Staff-Block-GetHit1"),Stunned=M("2Hand-Staff-Stunned"),Roll=M("2Hand-Staff-Roll-Forward"),
            AirAttack=M("2Hand-Staff-Air-Attack1"),SprintAttack=M("2Hand-Staff-Run-Forward-Attack1"),Attacks=Array.ConvertAll(Names("2Hand-Staff-Attack","1","2","3"),M),
            Reactions=Array.ConvertAll(Names("2Hand-Staff-GetHit-","F1","B1","R1","L1"),M),
            CastStart=Array.ConvertAll(Names("2Hand-Staff-Cast-Attack","1_start","2_start","3_start"),M),CastEnd=Array.ConvertAll(Names("2Hand-Staff-Cast-Attack","1_end","2_end","3_end"),M),
            AbilityStart=M("2Hand-Staff-Cast-L-AOE1_start"),AbilityEnd=M("2Hand-Staff-Cast-L-AOE1_end"),LeftShaft=true,Knockdown=M("2Hand-Staff-Knockdown1"),RollBack=M("2Hand-Staff-Roll-Backward"),
            AbilityIds=new[]{"flame-lance","meteor-strike","tidal-wave","geyser-wall","blink-strike","ball-lightning","shadow-shackles","singularity"},
            AbilityStarts=Array.ConvertAll(new[]{"Attack3","L-Buff1","L-AOE1","L-Summon2","L-Buff2","L-Summon1","Attack2","L-AOE2"},c=>M("2Hand-Staff-Cast-"+c+"_start")),
            AbilityEnds=Array.ConvertAll(new[]{"Attack3","L-Buff1","L-AOE1","L-Summon2","L-Buff2","L-Summon1","Attack2","L-AOE2"},c=>M("2Hand-Staff-Cast-"+c+"_end"))},"2Hand-Staff-Run-","2Hand-Staff-Strafe-",M("2Hand-Staff-Run-Forward"),true);
        // The bow: the pack's bow set (its block clip is named "Hand-Bow-Block" inside), the bow in the left fist as the game's.
        // Raised while drawing (its drawn poses by the pitch), just after a loose (its release) or while aiming; else carried
        // at rest.
        private static readonly Set Bow=Build(new Set{Spec="bow",Idle="2Hand-Bow-Idle",Jump="2Hand-Bow-Jump",Flip="2Hand-Bow-Jump-Flip",Fall="2Hand-Bow-Fall",
            Land="2Hand-Bow-Land",Block="Hand-Bow-Block",BlockHit="2Hand-Bow-Block-GetHit1",Stunned="2Hand-Bow-Stunned",Roll="2Hand-Bow-Roll-Forward",
            AirAttack="2Hand-Bow-Fall",SprintAttack="2Hand-Bow-Attack1",Attacks=Names("2Hand-Bow-Attack","1","2","3"),Reactions=Names("2Hand-Bow-GetHit-","F1","B1","L1","R1"),
            Aim=Names("2Hand-Bow-Aiming-","DM","CM","UM"),Pull=Names("2Hand-Bow-Aiming-Pull-","DM","CM","UM"),Fire=Names("2Hand-Bow-Aiming-Fire-","DM","CM","UM"),Knockdown="2Hand-Bow-Knockdown1",RollBack="2Hand-Bow-Roll-Backward"},"2Hand-Bow-Run-","2Hand-Bow-Strafe-","2Hand-Bow-Run-Forward");
        // The unarmed fighters (Emperor, Ansatsuken): the pack's unarmed set, its strikes alternating hands, a chain's last the
        // heavy right.
        private static readonly Set Unarmed=Build(new Set{Spec="unarmed",Idle="Unarmed-Idle",Jump="Unarmed-Jump",Flip="Unarmed-Jump-Flip",Fall="Unarmed-Fall",
            Land="Unarmed-Land",Block="Unarmed-Block",BlockHit="Unarmed-Block-GetHit1",Stunned="Unarmed-Stunned",Roll="Unarmed-Roll-Forward",
            AirAttack="Unarmed-Air-Attack1",SprintAttack="Unarmed-Run-Forward-Attack1-Right",Attacks=Names("Unarmed-Attack-","R1","L1","R2","L2","R3"),Reactions=Names("Unarmed-GetHit-","F1","B1","L1","R1"),Fighter=true,Knockdown="Unarmed-Knockdown1",RollBack="Unarmed-Roll-Backward",
            Electric="Unarmed-Run-Forward-Attack1-Right",Wave="Unarmed-Dash-Forward",Step="Unarmed-Dash-Forward",Hadoken="Unarmed-Cast-Dual-Attack1_end",
            ChargeStart="Unarmed-Cast-Dual-AOE1_start",ChargeLoop="Unarmed-Cast-Dual-AOE1_loop",Shoryuken="Unarmed-Boost1",Kicks=Names("Unarmed-Attack-Kick-","R2","L2")},"Unarmed-Run-","Unarmed-Strafe-","Unarmed-Sprint");
        /// <summary>A clip's name suffix asking the bake for its mirror image (left and right swapped).</summary>
        public const string Mirror="~mirror";
        private static string M(string clip)=>clip+Mirror;
        private static string[] Names(string prefix,params string[] parts)=>Array.ConvertAll(parts,p=>prefix+p);
        private static Set Build(Set s,string run,string walk,string sprint,bool mirrored=false)
        {
            // A mirrored run to the left is a run to the right.
            string[] dirs={"Forward","Forward-Right","Right","Backward-Right","Backward","Backward-Left","Left","Forward-Left"};
            string Dir(int i)=>mirrored?dirs[i].Replace("Right","#").Replace("Left","Right").Replace("#","Left"):dirs[i];
            s.Locomotion=new string[NativeVisualStride.Clips];
            for(int i=0;i<dirs.Length;i++){s.Locomotion[i]=run+Dir(i)+(mirrored?Mirror:"");s.Locomotion[NativeVisualStride.Walks+i]=walk+Dir(i)+(mirrored?Mirror:"");}
            s.Locomotion[NativeVisualStride.Sprint]=sprint;
            return Every(s);
        }
        /// <summary>A set whose stride is named clip by clip (NativeVisualStride's order: 8 runs, 8 walks, the sprint).</summary>
        private static Set Strides(Set s,string[] runs,string[] walks,string sprint)
        {
            s.Locomotion=new string[NativeVisualStride.Clips];
            for(int i=0;i<NativeVisualStride.Directions;i++){s.Locomotion[i]=runs[i];s.Locomotion[NativeVisualStride.Walks+i]=walks[i];}
            s.Locomotion[NativeVisualStride.Sprint]=sprint;return Every(s);
        }
        /// <summary>Every clip the set plays (All), from its fields.</summary>
        private static Set Every(Set s)
        {
            var all=new System.Collections.Generic.List<string>{s.Idle,s.Jump,s.Flip,s.Fall,s.Land,s.Block,s.BlockHit,s.Stunned,s.Roll,s.AirAttack,s.SprintAttack};
            foreach(var c in new[]{s.Knockdown,s.RollBack,s.Electric,s.Wave,s.Hadoken,s.ChargeStart,s.ChargeLoop,s.Shoryuken,s.Step})if(c!=null&&!all.Contains(c))all.Add(c);
            if(s.Kicks!=null)foreach(var c in s.Kicks)if(!all.Contains(c))all.Add(c);
            all.AddRange(s.Attacks);all.AddRange(s.Reactions);foreach(var l in s.Locomotion)if(!all.Contains(l))all.Add(l);
            foreach(var l in new[]{s.Aim,s.Fire,s.Pull,s.CastStart,s.CastEnd})if(l!=null)foreach(var c in l)if(!all.Contains(c))all.Add(c);
            foreach(var c in new[]{s.Reload,s.AbilityStart,s.AbilityEnd})if(c!=null&&!all.Contains(c))all.Add(c);
            foreach(var l in new[]{s.KitClips,s.KitRecast,s.KitThen,s.AbilityStarts,s.AbilityEnds,s.TechClips})if(l!=null)foreach(var c in l)if(c!=null&&!all.Contains(c))all.Add(c);
            s.All=all.ToArray();return s;
        }
        /// <summary>The set a weapon spec plays on the model, or null (the game's own pose: not moved over yet).</summary>
        public static Set For(string spec)=>spec=="greatsword"?Greatsword:spec=="sword"?Sword:spec=="daggers"?Daggers:spec=="shield"?Shield:
            spec=="rifle"||spec=="machinegun"?Rifle:spec==NativeBombs.Spec?Bomb:spec==NativeTopDog.Spec?TopDog:spec==NativeGlaive.Spec?Glaive:spec==NativeKatana.Spec?Katana:spec==NativeFangs.Spec?Fangs:spec=="pistol"?Pistol:spec=="bow"?Bow:
            spec=="water"||spec=="lightning"||spec=="fire"||spec=="darkness"||spec=="leet"?Staff:spec=="emperor"||spec=="ansatsuken"?Unarmed:null;

        /// <summary>A gun's, bow's or caster's upper-body layer this frame: up to two clips mixed (B by Mix) at their times, and
        /// whether the layer is up (Raised: aimed, drawn, reloading or casting). False for melee sets.</summary>
        public struct Upper { public string A, B; public float TimeA, TimeB, Mix; public bool Raised;
            /// <summary>Laid on at once rather than eased in (the layer continues what the whole body already played).</summary>
            public bool Snap; }

        // ---- The Whirlwind on the model (owner 2026-10-26: "need to fix whirlwind on greatsword, the player spin is not good").
        // The held swing (Attack8's Hit: upright, the blade raised) turned as one rigid pose read as a statue on a turntable.
        // Now the upper body is the set's level sweep held at WhirlHold of its clip (the arms long and low before the belly, the
        // blade level out past the knees) over the stance's legs, sat into a crouch by the spin's Low; while the blade spins each
        // foot marks time once per turn; the body turns so the drawn blade rides the game's own tip (MeleeKitMotion.Spin).
        /// <summary>Where the Whirlwind's sweep is held through the spin (a share of its clip: the level, low blade). The great sword pack's
        /// spinning sweep (2026-11-11) at 0.66 s of its 1.83: the arms out, the blade level at the waist, 1.8 m out.</summary>
        public const float WhirlHold=.36f;
        /// <summary>A step's share of a turn (each foot is down the rest; the other foot steps half a turn later) and the speed
        /// share (of the full spin) the steps start at and are full by.</summary>
        public const float StepShare=.45f, StepFrom=.25f, StepFull=.75f;
        /// <summary>The Whirlwind's upper body: its kit clip into its hold through the wind-up, held there through the spin,
        /// on to its end through the stop. False for anything else.</summary>
        public static bool SpinUpper(Set s,Actor a,float time,IClips clips,out Upper up)
        {
            up=default;if(s.KitKinds==null||!MeleeKitMotion.Clock(a,time,out var c)||c.Kind!="whirlwind")return false;
            int i=Array.IndexOf(s.KitKinds,c.Kind);if(i<0||!clips.Has(s.Idle))return false;string clip=s.KitClips[i];if(!clips.Has(clip))return false;
            float len=clips.Length(clip),share=s.KitHits!=null?s.KitHits[i]:float.NaN,hold=share>0&&share<1?share*len:clips.Hit(clip);if(!(hold>0&&hold<len))hold=len*.5f;
            float t=c.Part==KitPart.Windup||c.Part==KitPart.Recast?hold*Clamp01((time-c.At)/Math.Max(1e-3f,c.Release-c.At))
                :c.Part==KitPart.Active?hold:hold+(len-hold)*Clamp01((time-c.Start)/Math.Max(1e-3f,c.Until-c.Start));
            up=new Upper{A=clip,TimeA=t,Raised=true};return true;
        }
        /// <summary>How far the body is turned onto the blade (0: by the hands' turn, Grip): rising through the uncoil to the
        /// release, held through the spin, falling as the stopped pilot settles to its guard.</summary>
        public static float SpinAlign(in KitClock c,in MeleeKitMotion.Whirl w)
            =>c.Part==KitPart.Active?1:c.Part==KitPart.Recovery?1-w.Settle:Smooth01(w.Uncoil);
        /// <summary>The Whirlwind's legs: the sit (the spin's Low) and each foot's lift, once per turn of the hands (Grip), the
        /// left on the turn, the right half a turn on, by the spin's speed.</summary>
        public static void SpinLegs(in KitClock c,in MeleeKitMotion.Whirl w,out float sit,out float left,out float right)
        {
            sit=Clamp01(w.Low);float k=Smooth01((w.Speed-StepFrom)/(StepFull-StepFrom)),turns=w.Grip/(float)(2*Math.PI);
            left=k*Step(turns-(float)Math.Floor(turns));float half=turns+.5f;right=k*Step(half-(float)Math.Floor(half));
        }
        private static float Step(float phase)=>phase<StepShare?(float)Math.Sin(Math.PI*phase/StepShare):0;
        private static float Smooth01(float x){x=Clamp01(x);return x*x*(3-2*x);}
        /// <summary>A caster's upper body: the ability's (Q/E) or the cast's start fitted to its release, its end after it at
        /// its own pace; afterwards the end's last pose, lowered (Raised false).</summary>
        private static bool Cast(Set s,Actor a,float time,IClips clips,out Upper up)
        {
            up=new Upper{A=s.CastEnd[0],TimeA=clips.Has(s.CastEnd[0])?clips.Length(s.CastEnd[0]):0};
            if(a.AbilityId!=null&&a.AbilityAt>a.AttackAt&&time>=a.AbilityAt)
            {
                // Each ability its own cast (AbilityIds), else the area cast.
                float rel=a.AbilityRelease>a.AbilityAt?a.AbilityRelease:a.AbilityAt+.4f;int i=s.AbilityIds!=null?Array.IndexOf(s.AbilityIds,a.AbilityId):-1;
                if(Phase(i>=0?s.AbilityStarts[i]:s.AbilityStart,i>=0?s.AbilityEnds[i]:s.AbilityEnd,a.AbilityAt,rel,time,clips,ref up))return true;
            }
            else if(a.AttackAt>-90&&time>=a.AttackAt)
            {
                int k=(int)Math.Abs(Math.Round(a.AttackAt*10))%s.CastStart.Length;float rel=a.AttackAt+MagicPose.Windup(MagicPose.School(a),Math.Max(.15f,a.CastRate));
                if(Phase(s.CastStart[k],s.CastEnd[k],a.AttackAt,rel,time,clips,ref up))return true;
            }
            return clips.Has(up.A);
        }
        private static bool Phase(string start,string end,float at,float release,float time,IClips clips,ref Upper up)
        {
            if(!clips.Has(start)||!clips.Has(end))return false;
            if(time<release){up=new Upper{A=start,TimeA=clips.Length(start)*Clamp01((time-at)/Math.Max(1e-3f,release-at)),Raised=true};return true;}
            float len=clips.Length(end);up=new Upper{A=end,TimeA=Math.Min(len,time-release),Raised=time-release<len};return true;
        }
        /// <summary>How long after a loose the bow stays up.</summary>
        public const float BowHold=.6f;
        /// <summary>How long after a shot the gun stays up (the game's own rule: the interval and a little).</summary>
        public static float ShotHold(Actor a)=>GunRules.Valid(a.Weapon)&&GunRules.Get(a.Weapon)!=null?Math.Max(.38f,GunRules.Get(a.Weapon).Interval+.15f):.38f;
        /// <summary>The held guard (Runtime NativeGuard.cs; owner 2026-11-09: right click "a held block for melee"): the set's block
        /// clip on the upper body while it is up, from when it went up, over the legs' own walk (the guard walks).</summary>
        public static bool GuardUpper(Set s,Actor a,float time,IClips clips,out Upper up)
        {
            up=default;if(a==null||!a.Guarding||s.Block==null||!clips.Has(s.Block))return false;
            up=new Upper{A=s.Block,TimeA=Math.Max(0,time-a.GuardAt),Raised=true};return true;
        }
        /// <summary>`ready`: the body's own motion leaves the arms free (idle, locomotion, a jump's rise or fall, a landing): the
        /// pistol then follows the look unless sprinting (owner 2026-10-24: the low carry did not point where the pilot looks);
        /// the long guns stay at rest (RestLow).</summary>
        public static bool Layer(Set s,Actor a,float time,IClips clips,out Upper up,bool ready=false)
        {
            up=default;
            if(SpinUpper(s,a,time,clips,out up))return true;
            if(DashUpper(s,a,time,clips,out up))return true;
            if(GuardUpper(s,a,time,clips,out up))return true;
            if(BombUpper(s,a,time,clips,out up))return true;
            if(DogUpper(s,a,time,clips,out up))return true;
            if(NativeBlades.Is(s.Spec)&&NativeBladeMotion.Upper(s,a,time,clips,out up))return true;
            if(s.CastStart!=null)return Cast(s,a,time,clips,out up);
            if(s.Aim==null)return false;
            if(s.Reload!=null&&time>=a.ReloadAt&&time<a.ReloadUntil&&clips.Has(s.Reload))
            {
                float len=clips.Length(s.Reload);up=new Upper{A=s.Reload,TimeA=len*Clamp01((time-a.ReloadAt)/Math.Max(.05f,a.ReloadUntil-a.ReloadAt)),Raised=true};return true;
            }
            if(s.Spec==NativeBombs.Spec)return false;
            // A gun's Q/E: its own clip (a throw's wind-up to the release, then the throw; a flourish at its own pace), or held on
            // the aim (AbilityRaise) for its cast and a little after.
            bool casting=false;
            if(a.AbilityId!=null&&a.AbilityAt>-90&&time>=a.AbilityAt)
            {
                float rel=Math.Max(a.AbilityRelease,a.AbilityAt);int i=s.AbilityIds!=null?Array.IndexOf(s.AbilityIds,a.AbilityId):-1;
                if(i>=0&&clips.Has(s.AbilityStarts[i]))
                {
                    string end=s.AbilityEnds?[i];
                    if(end!=null){var cast=new Upper();if(Phase(s.AbilityStarts[i],end,a.AbilityAt,Math.Max(rel,a.AbilityAt+.15f),time,clips,ref cast)&&cast.Raised){up=cast;return true;}}
                    else if(time-a.AbilityAt<clips.Length(s.AbilityStarts[i])){up=new Upper{A=s.AbilityStarts[i],TimeA=time-a.AbilityAt,Raised=true};return true;}
                }
                else casting=s.AbilityRaise!=null&&Array.IndexOf(s.AbilityRaise,a.AbilityId)>=0&&time<Math.Max(rel,a.AbilityAt+.5f)+.4f;
            }
            float age=time-a.LastShot;bool raised=a.Aiming||casting||age<ShotHold(a)||ready&&s.Gun&&!s.RestLow&&!a.Sprinting;
            // A bow: drawn (its drawn poses by the pitch) from the draw to the loose, its release after the loose or any arrow
            // (the Volley's and the Rain's stamp only the last shot; NativeBowWire's fields: the draw start, the release time).
            if(s.Pull!=null)
            {
                bool drawing=a.BowDrawAt>0&&time>=a.BowDrawAt&&a.BowDrawAt>=a.BowReleaseAt;age=time-Math.Max(a.BowReleaseAt,a.LastShot);raised=a.Aiming||drawing||age<BowHold;
                float pp=Math.Max(-1,Math.Min(1,a.Pitch/s.AimPitch));
                if(drawing){up=new Upper{A=s.Pull[1],B=s.Pull[pp>=0?2:0],Mix=Math.Abs(pp),Raised=true};return clips.Has(up.A);}
            }
            // The pitch: level to up (positive) or down, between the grid's rows.
            float p=Math.Max(-1,Math.Min(1,a.Pitch/s.AimPitch));int b=p>=0?2:0;
            var row=s.Aim;float clipAge=0;
            if(s.Fire!=null&&age>=0&&clips.Has(s.Fire[1]))
            {
                float flen=clips.Length(s.Fire[1]),start=s.FireFromHit?clips.Hit(s.Fire[1]):0;
                if(start+age<flen){row=s.Fire;clipAge=start+age;}
            }
            up=new Upper{A=row[1],B=row[b],TimeA=clipAge,TimeB=clipAge,Mix=Math.Abs(p),Raised=raised};
            return clips.Has(up.A);
        }

        /// <summary>A body's memory across frames: when it left the ground and landed, and the last strike seen.</summary>
        public sealed class Memory
        {
            public bool WasGrounded=true;public float AirSince=-100,LandedAt=-100,AirAttackAt=-100;
            /// <summary>A block held on (the rules raise it again every few tenths of a second while it is held): when it began,
            /// and when the last seen one ends.</summary>
            public float BlockFrom=-100,BlockSeenUntil=-100;
        }
        /// <summary>What the model plays this frame: a clip at a time (looping or held at its end), or the locomotion blend.</summary>
        public struct Pick { public string Clip; public float Time; public bool Locomotion; public float Fade;
            /// <summary>Spin: the body is turned about its hips to Turn (radians from the facing, + toward the pilot's left), less
            /// the clip's own turn; by Align (0..1) the drawn blade's bearing is put on Tip instead (the game's blade tip, same
            /// frame). Sit (0..1): the legs bent into the spin's crouch; LiftL, LiftR (0..1): each foot's step.</summary>
            public bool Spin; public float Turn, Tip, Align, Sit, LiftL, LiftR;
            /// <summary>The clip's own flight is kept (the body only kept out of the floor, as the run's stride): a grounded sprint
            /// attack's leaping run (Flies).</summary>
            public bool Flight; }

        public const float FadeIn=.1f, FadeOut=.2f, ReactionMin=.25f;
        // ---- Motion polish (owner 2026-10-27: "Make the movements look realistic and not glitchy. For everything, movements,
        // attacks, for all weapons"). Measured frame by frame in play, every weapon:
        /// <summary>A strike's clip plays at most this much faster than its own pace. The pack's wind-ups are slow beside the
        /// rules' (a dagger cut's Hit 0.5 s in, the rules' 0.22 s), so fitted whole they played at two to four times: a wind-up
        /// that short starts further into its clip instead, and a recovery that short is cut by what comes next.</summary>
        public const float MaxStrikeRate=1.5f;
        /// <summary>The jump clip's launch (s): before it, its crouch and push, which the game's jump has already made; played
        /// from the press, the legs snapped from the crouch to straight in one frame.</summary>
        public const float JumpLaunch=.067f, JumpFade=.12f;
        /// <summary>A landing plays at LandRate times its own pace, for at most LandFor seconds (squeezed into 0.22 s it played
        /// at twice its pace).</summary>
        public const float LandFor=.45f, LandRate=1.15f;
        /// <summary>A hit reaction plays between ReactSlow and MaxStrikeRate times its own pace (fitted whole to the rules'
        /// quarter-second reaction it twitched at two to three times); past the reaction it fades into what follows, going on.</summary>
        public const float ReactSlow=.7f;
        /// <summary>A roll plays at most MaxRollRate times its pace (the pack's 0.8 s roll fitted to the rules' half second tumbled
        /// at 1.6 times): it is still getting up as the roll ends, and finishes as it fades into what follows.</summary>
        public const float MaxRollRate=1.3f;
        /// <summary>Whether a weapon's sprint (dash) attack keeps its clip's own flight (owner 2026-10-26: "dash attacks need
        /// fixing on melee, it looks like they need to be in the air when dash attacking?"). The pack's run attacks leap through
        /// their strides; pinned to the floor, their flights read as a crawl. The greatsword's is the game's own hop
        /// (NativeSprintAttackRules.Leaps), whose height the body already has, so its clip stays on the pilot's feet.</summary>
        public static bool Flies(string spec)=>!NativeSprintAttackRules.Leaps(spec);
        /// <summary>A swing's clip time: its wind-up to the clip's Hit on the middle of the rules' hit window, its end on the
        /// swing's, at most MaxStrikeRate times the clip's pace.</summary>
        private static float AttackTime(Actor a,float time,string clip,IClips clips)
        {
            float hitAt=HitAt(a);
            return Retime(time,a.AttackAt,hitAt,Math.Max(hitAt+.05f,a.AttackUntil),clips.Hit(clip),clips.Length(clip),MaxStrikeRate);
        }
        /// <summary>The middle of a swing's hit window: the replicated window while it lasts, then the same from the swing's own
        /// wind-up and active span (the window resets once it closes; reading the wind-up alone jumped the clip 0.08-0.11 s on).</summary>
        public static float HitAt(Actor a)=>a.SwingAt>=a.AttackAt&&a.SwingUntil>a.SwingAt?(a.SwingAt+a.SwingUntil)*.5f
            :a.AttackAt+(a.VisualWindup>0?a.VisualWindup:.25f)+Math.Max(0,a.VisualActive)*.5f;
        /// <summary>The greatsword's leaping cleave has landed (the game's hop comes down as the hit window opens: SwingAt while it
        /// is replicated, then the cast's wind-up from AttackAt, Touchdown): the pack's
        /// run attack strides on through its follow-through, which on a stopped pilot read as a crawl, so its legs become the
        /// set's landing (Land, from the touchdown) under the take's upper body, planted as the Classic lands it.</summary>
        public static bool DashLanded(Set s,Actor a,float time,IClips clips)=>a.SprintAttack&&!Flies(a.SourceSpec)&&a.Grounded&&time>=Touchdown(a)
            &&a.Attacking(time)&&clips.Has(s.Land)&&clips.Has(s.SprintAttack);
        public static float Touchdown(Actor a)=>a.SwingAt>=a.AttackAt?a.SwingAt:a.AttackAt+(a.VisualWindup>0?a.VisualWindup:.25f);
        /// <summary>The landed cleave's upper body: the take on its own timing, laid on at once (Snap: it is already the whole pose).</summary>
        public static bool DashUpper(Set s,Actor a,float time,IClips clips,out Upper up)
        {
            up=default;if(!DashLanded(s,a,time,clips))return false;
            up=new Upper{A=s.SprintAttack,TimeA=AttackTime(a,time,s.SprintAttack,clips),Raised=true,Snap=true};return true;
        }
        /// <summary>The pack's knockdown: its back reaches the floor at KnockImpact of the clip; airborne, it holds at KnockFlight of
        /// that (the legs-up flight). The backward roll is on its back from RollFromBack of the clip.</summary>
        public const float KnockImpact=.52f, KnockFlight=.8f, RollFromBack=.22f;
        /// <summary>A fighter strike's hit as a share of it (NativeFighters: a jab's .075 of .17 s, a combo's .12 of .25 s; a
        /// chain's last, .35).</summary>
        public const float StrikeHit=.45f, FinisherHit=.35f;
        /// <summary>The wavedash plays the dash's first WaveShare (its lunge); the shinku charge gathers for ChargeIn s, then holds.</summary>
        public const float WaveShare=.6f, ChargeIn=.3f;
        /// <summary>Why the last Choose declined (null when it chose): for the local checks.</summary>
        public static string Declined;

        /// <summary>The model's clip for pilot `a` at `time` (false: not covered; the game's pose shows).</summary>
        public static bool Choose(Set set,Actor a,float time,Memory m,IClips clips,bool moving,out Pick pick)
        {
            pick=default;
            if(a.Grounded!=m.WasGrounded){if(a.Grounded)m.LandedAt=time;else m.AirSince=time;m.WasGrounded=a.Grounded;}
            Declined=null;
            // Held in a blade class's paired move: the victim's clip (Core NativeBladeMotion).
            if(NativeBladeMotion.Victim(a,time,clips,out pick))return true;
            // Knocked down: the pack's fall (its flight while airborne, held short of the impact; its landing and lying once the
            // pilot lands), then the backward roll from on its back fitted to the get-up (the roll and the rise).
            if(NativeSpecialPose.Down(a,time))
            {
                if(set.Knockdown==null||!clips.Has(set.Knockdown)){Declined="down";return false;}
                float len=clips.Length(set.Knockdown),impact=len*KnockImpact;
                string phase=NativeSpecialRules.Phase(a.DownLanded,a.DownPickup,a.DownRoll,a.DownRollEnd,a.DownUsed,time);
                if(phase=="air"){pick=new Pick{Clip=set.Knockdown,Time=Math.Min(time-a.DownAt,impact*KnockFlight),Fade=FadeIn};return true;}
                if((phase=="roll"||phase=="rise")&&set.RollBack!=null&&clips.Has(set.RollBack))
                {
                    float roll=clips.Length(set.RollBack);
                    pick=new Pick{Clip=set.RollBack,Time=roll*(RollFromBack+(1-RollFromBack)*Clamp01((time-a.DownRoll)/Math.Max(.05f,a.DownUntil-a.DownRoll))),Fade=FadeIn};return true;
                }
                pick=new Pick{Clip=set.Knockdown,Time=Math.Min(len,impact+Math.Max(0,time-a.DownLanded)),Fade=FadeIn};return true;
            }
            if(BombCast(set,a,time,clips,out pick))return true;
            if(DogCast(set,a,time,clips,moving||!a.Grounded,out pick))return true;
            if(GlaiveCast(set,a,time,clips,out pick))return true;
            if(NativeBlades.Is(set.Spec)&&NativeBladeMotion.Cast(set,a,time,clips,moving||!a.Grounded,out pick))return true;
            if(Technique(set,a,time,clips,out pick))return true;
            if(Kit(set,a,time,clips,out pick))return true;
            // A mapped kit move's tail (its special still running past the cast) goes on to the idle, not the game's pose.
            bool mapped=set.KitKinds!=null&&a.MeleeKit!=null&&Array.IndexOf(set.KitKinds,a.MeleeKit.Kind)>=0;
            if(!mapped&&NativeSpecialPose.Active(a,time)){Declined="special";return false;}
            // Juggled in the air: the fall's flight.
            if(a.JuggleUntil>time){if(set.Knockdown==null||!clips.Has(set.Knockdown)){Declined="juggle";return false;}pick=new Pick{Clip=set.Knockdown,Time=clips.Length(set.Knockdown)*KnockImpact*KnockFlight,Fade=FadeIn};return true;}
            if(!mapped&&a.MeleeKit!=null&&a.MeleeKit.At>-90&&time>=a.MeleeKit.At&&time<a.MeleeKit.Until){Declined="kit";return false;}
            // The Ansatsuken's roll is its Demon Step: the pack's dash.
            if(a.Rolling(time))
            {
                if(set.Fighter&&a.SourceSpec=="ansatsuken"&&set.Step!=null)return Fit(set.Step,time,a.RollAt,a.RollAt+a.VisualRollDuration,clips,FadeIn,out pick);
                if(!clips.Has(set.Roll))return false;float len=clips.Length(set.Roll),rate=Math.Max(1,Math.Min(MaxRollRate,len/Math.Max(.05f,a.VisualRollDuration)));
                pick=new Pick{Clip=set.Roll,Time=Math.Min(len,(time-a.RollAt)*rate),Fade=FadeIn};return true;
            }
            // The plunging stab (an air attack of kind "down": docs/MELEE-FEEL.md "Plunging stab"): the pack has no take for it, so
            // the model takes the game's own plunge pose (Core NativePlungePose on the rig: blade point-down, the drop, the plant,
            // stuck, pulled out) rather than the forward jump attack's chop (owner 2026-11-02: "Ever since we changed player
            // models, we dont really have a slam attack anymore").
            if(a.AirAttack&&!a.SprintAttack&&a.AttackAt>-90&&time>=a.AttackAt&&NativePlungeFields.Kind(a)==NativePlungeFields.DownKind&&NativePlungeRules.Handles(a.SourceSpec)){Declined="plunge";return false;}
            // The forward jump attack: its Hit on the impact (the landing), else on the clip's own.
            if(a.AirAttack&&a.AttackAt>-90&&time>=a.AttackAt)
            {
                if(!clips.Has(set.AirAttack))return false;
                float hit=clips.Hit(set.AirAttack),len=clips.Length(set.AirAttack),impact=a.ImpactAt>a.AttackAt?a.ImpactAt:a.AttackAt+hit;
                pick=new Pick{Clip=set.AirAttack,Time=Retime(time,a.AttackAt,impact,impact+(len-hit),hit,len),Fade=FadeIn};return true;
            }
            // A fighter's jab or combo strike: its Hit on the rules' hit (a share of the strike, as NativeFighters times it), its
            // recovery played out at up to twice its pace; the fighters' other moves keep the game's own pose for now.
            if(set.Fighter)
            {
                bool strike=a.SourceAction=="jab"||a.SourceAction=="combo";
                if(strike&&a.SourceUntil>a.SourceAt&&time>=a.SourceAt)
                {
                    int n=set.Attacks.Length;bool last=a.SourceTotal>1&&a.SourceIndex==a.SourceTotal-1;
                    string clip=last?set.Attacks[n-1]:set.Attacks[Math.Max(0,a.SourceIndex)%(n-1)];
                    if(clips.Has(clip))
                    {
                        float hit=clips.Hit(clip),len=clips.Length(clip),hitAt=a.SourceAt+(a.SourceUntil-a.SourceAt)*(last?FinisherHit:StrikeHit),end=Math.Max(a.SourceUntil,hitAt+(len-hit)*.5f);
                        if(time<end){pick=new Pick{Clip=clip,Time=Retime(time,a.SourceAt,hitAt,end,hit,len,MaxStrikeRate),Fade=FadeIn};return true;}
                    }
                }
                else if(NativeFighterPose.Acting(a.SourceAction,a.SourceUntil,time))
                {
                    if(FighterMove(set,a,time,clips,out pick))return true;
                    Declined="fighter";return false;
                }
            }
            // A caster's cast plays on the upper-body layer (Layer); its strike rules are not melee. A fighter's strikes are its own.
            if(a.Attacking(time)&&set.CastStart==null&&!set.Fighter)
            {
                string clip=a.SprintAttack?set.SprintAttack:set.Attacks[Math.Max(0,a.Combo)%set.Attacks.Length];if(!clips.Has(clip))return false;
                // The greatsword's leaping cleave, landed: the landing's legs (its follow-through is the upper body, DashUpper).
                if(DashLanded(set,a,time,clips)){pick=new Pick{Clip=set.Land,Time=time-Touchdown(a),Fade=FadeIn};return true;}
                pick=new Pick{Clip=clip,Time=AttackTime(a,time,clip,clips),Fade=FadeIn,Flight=a.SprintAttack&&Flies(a.SourceSpec)};return true;
            }
            if(a.ReactionAt>-90&&time>=a.ReactionAt&&time<Math.Max(a.ReactionUntil,a.ReactionAt+ReactionMin))
            {
                string clip=set.Reactions[Side(a.ReactionYaw)];if(!clips.Has(clip))return false;
                float len=clips.Length(clip),span=Math.Max(a.ReactionUntil,a.ReactionAt+ReactionMin)-a.ReactionAt,rate=Math.Max(ReactSlow,Math.Min(MaxStrikeRate,len/Math.Max(.05f,span)));
                pick=new Pick{Clip=clip,Time=Math.Min(len,(time-a.ReactionAt)*rate),Fade=FadeIn};return true;
            }
            if(a.StunUntil>time&&clips.Has(set.Stunned)){pick=new Pick{Clip=set.Stunned,Time=time,Fade=FadeIn};return true;}
            if(time>=a.BlockAt&&time<a.BlockUntil&&clips.Has(set.Block))
            {
                // Held on, the block's clip keeps going (raised again by the rules, begun again it pumped the arms).
                if(a.BlockAt>m.BlockSeenUntil+.05f||a.BlockAt<m.BlockFrom)m.BlockFrom=a.BlockAt;m.BlockSeenUntil=a.BlockUntil;
                pick=new Pick{Clip=set.Block,Time=time-m.BlockFrom,Fade=FadeIn};return true;
            }
            if(!a.Grounded)
            {
                // The air jump's flip, the rise, the fall.
                if(a.AirJumps>0&&a.AirJumpAt>-90&&time>=a.AirJumpAt&&time-a.AirJumpAt<clips.Length(set.Flip)&&clips.Has(set.Flip)){pick=new Pick{Clip=set.Flip,Time=time-a.AirJumpAt,Fade=FadeIn};return true;}
                if(a.Velocity.y>0&&clips.Has(set.Jump)){pick=new Pick{Clip=set.Jump,Time=time-m.AirSince+JumpLaunch,Fade=JumpFade};return true;}
                if(clips.Has(set.Fall)){pick=new Pick{Clip=set.Fall,Time=time-m.AirSince,Fade=FadeOut};return true;}
                Declined="air";return false;
            }
            if(!moving&&clips.Has(set.Land))
            {
                float rate=Math.Max(LandRate,clips.Length(set.Land)/LandFor),since=time-m.LandedAt;
                if(since<clips.Length(set.Land)/rate){pick=new Pick{Clip=set.Land,Time=since*rate,Fade=FadeIn};return true;}
            }
            if(moving){pick=new Pick{Locomotion=true,Fade=FadeOut};return true;}
            if(!clips.Has(set.Idle))return false;
            pick=new Pick{Clip=set.Idle,Time=time,Fade=FadeOut};return true;
        }

        // The techniques' clips (docs/WEAPON-TECHNIQUES.md): throws and gestures out of the pack's casts, pickups and activations.
        private const string TechDashBack="Armed-Dash-Backward",TechSurf="Armed-Crouch-Idle",TechVowStart=NativeGreatswordMotion.Crouch,TechVowHold=NativeGreatswordMotion.Crouched,TechSink="2Hand-Staff-Crouch-Idle";
        private const string TechCounter="Sword-Attack-R4",TechFlickStart="Armed-Cast-L-Attack2_start",TechFlickEnd="Armed-Cast-L-Attack2_end",TechThrowStart="Armed-Cast-R-Attack2_start",TechThrowEnd="Armed-Cast-R-Attack2_end",TechPlant="Armed-Shield-Activate",TechCoin="Armed-Activate-Left",
            TechDrop="2Hand-Shooting-Pickup",TechShootLine="2Hand-Shooting-Activate",TechBuffStart="2Hand-Staff-Cast-L-Buff1_start"+Mirror,TechBuffEnd="2Hand-Staff-Cast-L-Buff1_end"+Mirror,TechTouch="2Hand-Staff-Activate"+Mirror;
        /// <summary>The pilot's technique on the pack's clips, from its record: each clip fitted to its moment. False: none, or the clip is not in
        /// the bank (the game's pose carries on).</summary>
        // The Bombadier's casts that take the whole body: a flip off its own blast, a two-handed heave of the big bundle, a stoop to light the line; the Finale's stoop over the box, the hands on its handle.
        private static bool BombCast(Set set,Actor a,float time,IClips clips,out Pick pick)
        {
            pick=default;var k=NativeBombs.Of(a);if(k?.CastKind==null||time<k.CastAt||time>=k.CastUntil)return false;float at=k.CastAt,rel=Math.Max(at+.01f,k.CastRelease),until=Math.Max(rel+.05f,k.CastUntil);
            bool Part(string clip,float from,float to,out Pick p){p=default;return time>=from&&time<to&&clip!=null&&clips.Has(clip)&&Fit(clip,time,from,to,clips,FadeIn,out p);}
            switch(k.CastKind)
            {
                case NativeBombs.BlastJump:return Part(set.Flip,at,until,out pick);
                case NativeBombs.ClusterLob:return Part(BombHeaveStart,at,rel,out pick)||Part(BombHeaveEnd,rel,until,out pick);
                case NativeBombs.FirecrackerLine:return Part(BombLay,at,until,out pick);
                case NativeBombs.GrandFinale:
                {
                    if(time<rel)return Part(BombLay,at,rel,out pick);float lockAt=k.Finale?.Lock??rel+1.5f;
                    // Both hands pull the handle up and drive it home at the Lock, then the pilot stands and watches. The clip's own raise (overhead, a sword's) is cut short: from BombSlamFrom it falls to its low point at BombSlamHit.
                    if(time>=lockAt-BombSlamIn&&time<lockAt+.34f&&clips.Has(BombSlam))
                    {
                        float len=clips.Length(BombSlam),at_=time<lockAt?BombSlamFrom+(BombSlamHit-BombSlamFrom)*Clamp01((time-lockAt+BombSlamIn)/BombSlamIn):BombSlamHit+(Math.Max(BombSlamHit,len)-BombSlamHit)*Clamp01((time-lockAt)/.34f);
                        pick=new Pick{Clip=BombSlam,Time=at_,Fade=FadeIn};return true;
                    }
                    if(time<lockAt&&clips.Has(BombStoop)){pick=new Pick{Clip=BombStoop,Time=time-rel,Fade=FadeIn};return true;}return false;
                }
            }
            return false;
        }
        // The Top Dog's casts (docs/TOP-DOG.md "Motion"): each clip fitted to the record's moments, a swing's own Hit on the rules' hit, so
        // the mallet lands when the hit does. The arm casts (DogArms: the mallet's swings, the twirl and throw, the anvil, the peel, the bottle,
        // the glove and the pie) take the whole body only standing on the ground; on the move or in the air the legs keep the stride, the jump
        // or the fall and the cast is the upper body over them (DogUpper; owner 2026-11-09: "when they cast abilities while running, they start
        // sliding"). The dash, the slam, the spring, the zip and the feeding stay whole-body moves.
        private const string DogPoint="Armed-Cast-R-Attack3_start",DogPointEnd="Armed-Cast-R-Attack3_end",DogPunch="Unarmed-Attack-R2",DogPunchL="Unarmed-Attack-L1",DogKick="Unarmed-Attack-Kick-R2",DogRun="2Hand-Sword-Run-Forward";
        /// <summary>The Glaive's skim: the legs sprint through its lunge (its ring keyed by hand over them).</summary>
        private static bool GlaiveCast(Set set,Actor a,float time,IClips clips,out Pick pick)
        {
            pick=default;if(set.Spec!=NativeGlaive.Spec)return false;if(NativeBladeMotion.Slam(set,a,time,clips,out pick))return true;var k=NativeGlaiveView.Of(a);if(k?.CastKind!=NativeGlaive.Skim||time<k.CastAt||time>=k.CastRelease+NativeGlaive.SkimTime||!clips.Has(GlaiveRun))return false;
            float len=Math.Max(.05f,clips.Length(GlaiveRun));pick=new Pick{Clip=GlaiveRun,Time=((time-k.CastAt)*2.4f)%len,Fade=FadeIn};return true;
        }
        /// <summary>The Top Dog's arm cast at `time` (its clip and that clip's time), or false: none, or a whole-body move.</summary>
        public static bool DogArms(Set set,Actor a,float time,IClips clips,out string clip,out float at)
        {
            clip=null;at=0;if(set.Spec!=NativeTopDog.Spec)return false;var k=NativeDogView.Of(a);if(k==null)return false;string c=null;float t=0;
            bool Part(string name,float from,float to){if(time<from||time>=to||name==null||!clips.Has(name))return false;c=name;t=clips.Length(name)*Clamp01((time-from)/Math.Max(1e-3f,to-from));return true;}
            bool Hit(string name,float from,float hit,float to,float rate=2.4f){if(time<from||time>=to||name==null||!clips.Has(name))return false;c=name;t=Retime(time,from,hit,to,clips.Hit(name),clips.Length(name),rate);return true;}
            // All You Can Eat's twirl and throw (the rest of the feast is the whole body's).
            var f=k.Feast;
            if(f!=null&&time>=f.At&&time<f.Until)
            {
                if(time<f.Throw){bool ok=Part(DogPoint,f.At,f.Throw);if(ok){clip=c;at=t;}return ok;}
                if(f.Catch<-50){bool ok=Part(DogPointEnd,f.Throw,f.Throw+.45f);if(ok){clip=c;at=t;}return ok;}
                return false;
            }
            if(k.CastKind==null||time<k.CastAt||time>=k.CastUntil+.05f)return false;float from0=k.CastAt,rel=Math.Max(from0+.01f,k.CastRelease),until=Math.Max(rel+.05f,k.CastUntil);
            switch(k.CastKind)
            {
                // The mallet's swings are keyed by hand over the stance or the stride (Core NativeDogMallet; Runtime NativeDogMalletPresentation.cs).
                case NativeTopDog.Mallet:return false;
                case NativeTopDog.Anvil:{bool ok=Part(DogPoint,from0,rel)||Part(DogPointEnd,rel,until);if(ok){clip=c;at=t;}return ok;}
                case NativeTopDog.Peel:{bool ok=Part(TechFlickStart,from0,rel)||Part(TechFlickEnd,rel,until);if(ok){clip=c;at=t;}return ok;}
                case NativeTopDog.Mustard:
                {
                    if(time<rel){bool ok=Part(TechFlickStart,from0,rel);if(ok){clip=c;at=t;}return ok;}if(!clips.Has(TechFlickStart))return false;
                    // Held at the aim through the squirt, a small pump on each squeeze.
                    clip=TechFlickStart;at=clips.Length(TechFlickStart)*(.92f+.05f*(float)Math.Sin((time-rel)*18));return true;
                }
                case NativeTopDog.Glove:{bool ok=Hit(DogPunch,from0,rel,until,1.6f);if(ok){clip=c;at=t;}return ok;}
                case NativeTopDog.Pie:{bool ok=Part(TechThrowStart,from0,rel)||Part(TechThrowEnd,rel,until);if(ok){clip=c;at=t;}return ok;}
            }
            return false;
        }
        /// <summary>The Top Dog's arm cast on the upper body (over the stride on the move; the same clip as the whole body standing).</summary>
        public static bool DogUpper(Set s,Actor a,float time,IClips clips,out Upper up)
        {
            up=default;if(!DogArms(s,a,time,clips,out string clip,out float t))return false;up=new Upper{A=clip,TimeA=t,Raised=true};return true;
        }
        private static bool DogCast(Set set,Actor a,float time,IClips clips,bool travel,out Pick pick)
        {
            pick=default;if(set.Spec!=NativeTopDog.Spec)return false;var k=NativeDogView.Of(a);if(k==null)return false;
            // An arm cast: the whole body standing; on the move or in the air the legs' own motion, the cast over it (DogUpper).
            if(DogArms(set,a,time,clips,out string arms,out float armsAt)){if(travel)return false;pick=new Pick{Clip=arms,Time=armsAt,Fade=FadeIn};return true;}
            bool Part(string clip,float from,float to,out Pick p){p=default;return time>=from&&time<to&&clip!=null&&clips.Has(clip)&&Fit(clip,time,from,to,clips,FadeIn,out p);}
            bool Hit(string clip,float from,float hit,float to,out Pick p,float rate=2.4f){p=default;if(time<from||time>=to||clip==null||!clips.Has(clip))return false;p=new Pick{Clip=clip,Time=Retime(time,from,hit,to,clips.Hit(clip),clips.Length(clip),rate),Fade=FadeIn};return true;}
            bool Loop(string clip,float from,float to,float rate,out Pick p){p=default;if(time<from||time>=to||!clips.Has(clip))return false;float len=Math.Max(.05f,clips.Length(clip));p=new Pick{Clip=clip,Time=((time-from)*rate)%len,Fade=FadeIn};return true;}
            // All You Can Eat: the wait for the first hot dog, then the kick that lets them go (the twirl and throw are DogArms').
            var f=k.Feast;
            if(f!=null&&time>=f.At&&time<f.Until)
            {
                if(time<f.Throw||f.Catch<-50)return false;
                if(f.Snap>-50&&time>=f.Snap)return false;
                if(time<f.FeedAt)return Part(DogPointEnd,f.Throw,f.FeedAt,out pick);
                // Hand fed: the stance under it, each arm reaching by IK (Runtime NativeTopDogPresentation.cs DogFeedArms).
                if(time<f.Release-.25f)return false;
                return Hit(DogKick,f.Release-.25f,f.Release,f.Release+.6f,out pick);
            }
            if(k.CastKind==null||time<k.CastAt||time>=k.CastUntil+.05f)return false;float at=k.CastAt,rel=Math.Max(at+.01f,k.CastRelease),until=Math.Max(rel+.05f,k.CastUntil);
            switch(k.CastKind)
            {
                // The mallet's dash, smash and slam: the legs' clip here (a stride through the charge; the fall, then the landing's squat), the
                // mallet and the upper body keyed by hand over it (Core NativeDogMallet).
                case NativeTopDog.Dash:
                {
                    if(time<rel)return Part(DogRun,at,rel+.6f,out pick);
                    if(time<rel+NativeTopDog.DashTime)return Loop(DogRun,rel,rel+NativeTopDog.DashTime,2.6f,out pick);
                    return false;
                }
                case NativeTopDog.Slam:
                case NativeTopDog.Smash:
                {
                    float land=NativeTopDog.Landed(k);
                    if(land<-50)return Loop(set.Fall,at,until+.1f,1,out pick);
                    return Part(set.Land,land,until+.15f,out pick);
                }
                case NativeTopDog.Boing:return Part(set.Flip,at,until+.5f,out pick);
                case NativeTopDog.Zip:return time<rel?Loop(DogRun,at,rel,2.6f,out pick):Loop(DogRun,rel,until,1.8f,out pick);
            }
            return false;
        }
        private const string BombThrowStart="Armed-Cast-R-Attack3_start",BombThrowEnd="Armed-Cast-R-Attack3_end",BombReload="Armed-Unsheath-R-FromArmed-Hips",BombHeaveStart="Unarmed-Cast-Dual-Attack1_start",BombHeaveEnd="Unarmed-Cast-Dual-Attack1_end",
            BombLay="Armed-Pickup-Right",BombClick="Armed-Activate-Left",BombStoop="2Hand-Sword-Crouch-Idle",BombSlam="2Hand-Sword-Attack11";
        private const float BombSlamIn=.2f,BombSlamFrom=.46f,BombSlamHit=.9f;
        /// <summary>What the Bombadier throws on the move: the arms only, the legs carrying on. Held fire goes overhand from each fist in turn (the left is the bake's mirror), a sticky fuse is a snap of the right, a mine a flick of the left, and the switch a press of the left thumb.</summary>
        private static bool BombUpper(Set s,Actor a,float time,IClips clips,out Upper up)
        {
            up=default;if(s.Spec!=NativeBombs.Spec)return false;var k=NativeBombs.Of(a);if(k?.CastKind==null||time<k.CastAt||time>=k.CastUntil)return false;float rel=Math.Max(k.CastAt+.03f,k.CastRelease);
            switch(k.CastKind)
            {
                case NativeBombs.PopBomb:{string hand=NativeBombs.ThrowsLeft(k.CastAt)?Mirror:"";return Phase(BombThrowStart+hand,BombThrowEnd+hand,k.CastAt,rel,time,clips,ref up);}
                case NativeBombs.StickyFuse:return Phase(TechThrowStart,TechThrowEnd,k.CastAt,rel,time,clips,ref up);
                case NativeBombs.BounceMine:return Phase(TechFlickStart,TechFlickEnd,k.CastAt,rel,time,clips,ref up);
                case NativeBombs.DeadMansSwitch:if(!clips.Has(BombClick))return false;up=new Upper{A=BombClick,TimeA=clips.Length(BombClick)*Clamp01((time-k.CastAt)/Math.Max(.05f,k.CastUntil-k.CastAt)),Raised=true};return true;
            }
            return false;
        }
        private static bool Technique(Set set,Actor a,float time,IClips clips,out Pick pick)
        {
            pick=default;var k=NativeTechniques.Of(a);if(k?.Kind==null||time<k.At)return false;float back=Math.Max(k.Release+.05f,k.RecoverUntil);
            bool Part(string clip,float from,float to,out Pick p){p=default;return time>=from&&time<to&&clip!=null&&clips.Has(clip)&&Fit(clip,time,from,to,clips,FadeIn,out p);}
            switch(k.Kind)
            {
                case NativeTechniques.Riposte:
                    if(k.Hit(time))return Part(TechCounter,k.HitAt,k.HitAt+.42f,out pick);
                    if(time<k.Until&&set.Block!=null&&clips.Has(set.Block)){pick=new Pick{Clip=set.Block,Time=Math.Min(time-k.At,clips.Length(set.Block)*.4f),Fade=FadeIn};return true;}
                    return false;
                case NativeTechniques.SmokeVeil:return Part(TechFlickStart,k.At,k.Release,out pick)||Part(TechFlickEnd,k.Release,back,out pick);
                case NativeTechniques.Bastion:return Part(TechPlant,k.At,back,out pick);
                case NativeTechniques.AnchorThrow:
                    if(k.Recast(time))return Part(set.Flip,k.RecastAt,Math.Max(k.RecastAt+.1f,k.Until),out pick);
                    return Part(TechThrowStart,k.At,k.Release,out pick)||Part(TechThrowEnd,k.Release,back,out pick);
                case NativeTechniques.RicochetCoin:return Part(TechCoin,k.At,back,out pick);
                case NativeTechniques.SentryDrop:return Part(TechDrop,k.At,back,out pick);
                case NativeTechniques.GrappleLine:
                    if(time>=k.Arm&&time<k.Until&&set.Fall!=null&&clips.Has(set.Fall)){pick=new Pick{Clip=set.Fall,Time=time-k.Arm,Fade=FadeIn};return true;}
                    return Part(TechShootLine,k.At,Math.Max(k.Arm,k.At+.05f),out pick);
                case NativeTechniques.GaleArrow:
                    return Part(set.Pull!=null?set.Pull[1]:null,k.At,k.Release,out pick)||Part(set.Fire!=null?set.Fire[1]:null,k.Release,back,out pick);
                case NativeTechniques.IgnitionLeap:return Part(set.AbilityStart,k.At,k.Release,out pick)||Part(set.Flip,k.Release,back,out pick);
                case NativeTechniques.HealingSpring:return Part(TechBuffStart,k.At,k.Release,out pick)||Part(TechBuffEnd,k.Release,back,out pick);
                case NativeTechniques.StaticRecall:return Part(TechTouch,k.At,back,out pick);
                case NativeTechniques.PartingLine:return Part(TechDashBack,k.At,Math.Max(k.Release+.3f,back),out pick);
                case NativeTechniques.Tripwire:return Part(TechFlickStart,k.At,k.Release,out pick)||Part(TechFlickEnd,k.Release,back,out pick);
                case NativeTechniques.ShieldSurf:
                    if(k.Recast(time))return Part(set.Flip,k.RecastAt,Math.Max(k.RecastAt+.1f,k.Until),out pick);
                    if(time>=k.At&&time<k.Until&&clips.Has(TechSurf)){pick=new Pick{Clip=TechSurf,Time=time-k.At,Fade=FadeIn};return true;}return false;
                case NativeTechniques.IronVow:
                    if(time<k.Release)return Part(TechVowStart,k.At,k.Release,out pick);
                    if(time<k.End&&clips.Has(TechVowHold)){pick=new Pick{Clip=TechVowHold,Time=time-k.Release,Fade=FadeIn};return true;}return false;
                case NativeTechniques.BountyRound:return Part(set.Fire!=null?set.Fire[0]:null,k.At,back,out pick);
                case NativeTechniques.RecoilBurst:
                    if(time>=k.Release&&time<k.Until&&set.Fall!=null&&clips.Has(set.Fall)){pick=new Pick{Clip=set.Fall,Time=time-k.Release,Fade=FadeIn};return true;}
                    return Part(set.Fire!=null?set.Fire[0]:null,k.At,Math.Max(k.Release,k.At+.05f),out pick);
                case NativeTechniques.SonarDart:return Part(TechShootLine,k.At,back,out pick);
                case NativeTechniques.SpiritHawk:return Part(set.Pull!=null?set.Pull[1]:null,k.At,k.Release,out pick)||Part(set.Fire!=null?set.Fire[1]:null,k.Release,back,out pick);
                case NativeTechniques.WildfireRun:return Part(set.AbilityStart,k.At,k.Release,out pick);
                case NativeTechniques.Slipstream:return Part(TechBuffStart,k.At,k.Release,out pick)||Part(TechBuffEnd,k.Release,back,out pick);
                case NativeTechniques.LightningRod:return Part(set.AbilityStart,k.At,k.Release,out pick)||Part(set.AbilityEnd,k.Release,back,out pick);
                case NativeTechniques.GraveWalk:
                    if(time<k.RecoverUntil&&clips.Has(TechSink)){pick=new Pick{Clip=TechSink,Time=time-k.At,Fade=FadeIn};return true;}return false;
                case NativeTechniques.ShadeSwap:return Part(set.CastStart!=null?set.CastStart[2]:null,k.At,k.Release,out pick)||Part(set.CastEnd!=null?set.CastEnd[2]:null,k.Release,k.Release+.38f,out pick);
            }
            return ThirdPose(set,k,time,back,clips,out pick);
        }
        /// <summary>A kit move the set plays on the pack's clips (KitKinds), from the replicated cast (MeleeKitMotion.Clock): the
        /// clip's Hit on the release and its end on the cast's end. The Whirlwind: the clip into its Hit through the wind-up,
        /// held there (the blade out) through the spin, its end through the stop, the body turned to the game's own spin.</summary>
        private static bool Kit(Set set,Actor a,float time,IClips clips,out Pick pick)
        {
            pick=default;if(set.KitKinds==null||!MeleeKitMotion.Clock(a,time,out var c))return false;
            int i=Array.IndexOf(set.KitKinds,c.Kind);if(i<0)return false;
            string clip=c.Recast&&set.KitRecast!=null&&set.KitRecast[i]!=null?set.KitRecast[i]:set.KitClips[i];if(!clips.Has(clip))return false;
            float len=clips.Length(clip),share=set.KitHits!=null&&!c.Recast?set.KitHits[i]:float.NaN,hit=share>0?share*len:clips.Hit(clip);if(!(hit>0&&hit<len))hit=len*.5f;
            if(share<0){pick=new Pick{Clip=clip,Time=time-c.At,Fade=FadeIn};return true;}
            string then=!c.Recast&&set.KitThen!=null?set.KitThen[i]:null;
            if(then!=null&&clips.Has(then))
            {
                if(time<c.Release)return Fit(clip,time,c.At,c.Release,clips,FadeIn,out pick);
                return Fit(then,time,c.Release,Math.Max(c.Release+.05f,c.Until),clips,FadeIn,out pick);
            }
            if(c.Kind=="whirlwind")
            {
                // The legs are the set's stance (the pack's level sweeps end in lunges, whose trailing leg the spin would sweep
                // round); the kit clip is the upper body (Layer, SpinUpper).
                if(!clips.Has(set.Idle))return false;
                var w=MeleeKitMotion.Spin(c,time);SpinLegs(c,w,out float sit,out float left,out float right);
                pick=new Pick{Clip=set.Idle,Time=time,Fade=FadeIn,Spin=true,Turn=w.Grip,Tip=w.Tip,Align=SpinAlign(c,w),Sit=sit,LiftL=left,LiftR=right};return true;
            }
            pick=new Pick{Clip=clip,Time=Retime(time,c.At,c.Release,Math.Max(c.Release+.05f,c.Until),hit,len),Fade=FadeIn};return true;
        }
        /// <summary>A fighter's own move from its replicated action (NativeFighters' timings; the attack rate, not replicated, from
        /// the move's own span): the electric punch's Hit at a quarter of it; the wavedash the dash's lunge (WaveShare of it);
        /// the hadoken's and shinku's push across the move; the shinku charge's gathering (ChargeIn s), then its hold; the
        /// tatsu's kicks alternating, each kick's wind-up to its Hit in the gap before its hit, the last one's recovery after;
        /// the shoryuken's rise with its Hit on the first hit. False: no clip (the game's pose).</summary>
        private static bool FighterMove(Set set,Actor a,float time,IClips clips,out Pick pick)
        {
            pick=default;float at=a.SourceAt,until=Math.Max(a.SourceAt+.05f,a.SourceUntil);int total=Math.Max(1,a.SourceTotal);
            bool Hit(string clip,float hitAt,float end,out Pick p)
            {
                p=default;if(clip==null||!clips.Has(clip))return false;float len=clips.Length(clip),hit=clips.Hit(clip);if(!(hit>0&&hit<len))hit=len*.5f;
                p=new Pick{Clip=clip,Time=Retime(time,at,hitAt,Math.Max(end,hitAt+(len-hit)*.5f),hit,len,MaxStrikeRate),Fade=FadeIn};return true;
            }
            switch(a.SourceAction)
            {
                case "electric":return Hit(set.Electric,at+(until-at)*.25f,until,out pick);
                case "wave":
                    if(set.Wave==null||!clips.Has(set.Wave))return false;
                    pick=new Pick{Clip=set.Wave,Time=clips.Length(set.Wave)*WaveShare*Clamp01((time-at)/(until-at)),Fade=FadeIn};return true;
                case "hadoken":case "shinku":return Fit(set.Hadoken,time,at,until,clips,FadeIn,out pick);
                case "charge":
                    if(time<at+ChargeIn||set.ChargeLoop==null||!clips.Has(set.ChargeLoop))return Fit(set.ChargeStart,time,at,at+ChargeIn,clips,FadeIn,out pick);
                    pick=new Pick{Clip=set.ChargeLoop,Time=time-at-ChargeIn,Fade=FadeIn};return true;
                case "tatsu":
                {
                    if(set.Kicks==null)return false;
                    float rate=(.12f+.18f*(total-1))/Math.Max(.05f,until-at-.22f),hitAt=at+.12f/rate,gap=.18f/rate,last=hitAt+(total-1)*gap;
                    int k=time<hitAt?0:Math.Min(total-1,(int)((time-hitAt)/gap)+1);string clip=set.Kicks[k%set.Kicks.Length];if(!clips.Has(clip))return false;
                    float len=clips.Length(clip),hit=clips.Hit(clip);if(!(hit>0&&hit<len))hit=len*.5f;
                    float from=k==0?at:hitAt+(k-1)*gap,to=k==0?hitAt:hitAt+k*gap;
                    float start=Math.Max(0,hit-Math.Max(1e-3f,to-from)*MaxStrikeRate);
                    float t=time<=last?start+(hit-start)*Clamp01((time-from)/Math.Max(1e-3f,to-from)):hit+(len-hit)*Clamp01((time-last)/Math.Max(Math.Max(.05f,until-last),(len-hit)/MaxStrikeRate));
                    pick=new Pick{Clip=clip,Time=t,Fade=FadeIn};return true;
                }
                case "shoryuken":
                {
                    float rate=(.08f+.16f*(total-1))/Math.Max(.05f,until-at-.68f);return Hit(set.Shoryuken,at+.08f/rate,until,out pick);
                }
            }
            return false;
        }
        /// <summary>A clip played across [start, end], its whole length.</summary>
        private static bool Fit(string clip,float time,float start,float end,IClips clips,float fade,out Pick pick)
        {
            pick=default;if(!clips.Has(clip))return false;float len=clips.Length(clip);
            pick=new Pick{Clip=clip,Time=len*Clamp01((time-start)/Math.Max(1e-3f,end-start)),Fade=fade};return true;
        }
        /// <summary>The clip time at `time` for a move from `start`, hitting at `hitAt`, ending at `end`: the clip's own Hit (`hit` s
        /// in, of `length`) on hitAt, each side stretched to fit.</summary>
        public static float Retime(float time,float start,float hitAt,float end,float hit,float length)
        {
            if(time<=hitAt)return hit*Clamp01((time-start)/Math.Max(1e-3f,hitAt-start));
            return hit+(length-hit)*Clamp01((time-hitAt)/Math.Max(1e-3f,end-hitAt));
        }
        /// <summary>Retime at most `maxRate` times the clip's pace: a wind-up too short for the clip's starts later into it (the
        /// Hit still on hitAt), and a recovery too short for its clip plays on past `end` (what comes next fades it out).</summary>
        public static float Retime(float time,float start,float hitAt,float end,float hit,float length,float maxRate)
        {
            float from=Math.Max(0,hit-Math.Max(1e-3f,hitAt-start)*maxRate);
            if(time<=hitAt)return from+(hit-from)*Clamp01((time-start)/Math.Max(1e-3f,hitAt-start));
            return hit+(length-hit)*Clamp01((time-hitAt)/Math.Max(Math.Max(1e-3f,end-hitAt),(length-hit)/maxRate));
        }
        /// <summary>The reaction's side from its yaw (the struck pilot's facing frame, +x its left, radians): 0 front, 1 back,
        /// 2 left, 3 right.</summary>
        public static int Side(float yaw)
        {
            float y=(float)Math.IEEERemainder(yaw,2*Math.PI),deg=y*57.29578f;
            if(Math.Abs(deg)<=45)return 0;if(Math.Abs(deg)>=135)return 1;return deg>0?2:3;
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }
}
