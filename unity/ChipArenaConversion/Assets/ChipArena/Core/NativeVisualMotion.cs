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
    public static class NativeVisualMotion
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
            public bool Gun=>Aim!=null&&Pull==null;
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
            /// <summary>Every clip the set plays (the bake's controller carries them).</summary>
            public string[] All;
        }
        /// <summary>A clip's length and the time of its Hit event (seconds; a clip without one: its middle).</summary>
        public interface IClips { bool Has(string clip); float Length(string clip); float Hit(string clip); }

        // Greatsword: the pack's two-handed sword set.
        private static readonly Set Greatsword=Build(new Set{Spec="greatsword",Idle="2Hand-Sword-Idle",Jump="2Hand-Sword-Jump",Flip="2Hand-Sword-Jump-Flip",Fall="2Hand-Sword-Fall",
            Land="2Hand-Sword-Land",Block="2Hand-Sword-Block",BlockHit="2Hand-Sword-Block-GetHit1",Stunned="2Hand-Sword-Stunned",Roll="2Hand-Sword-Roll-Forward",
            AirAttack="2Hand-Sword-Air-Attack1",SprintAttack="2Hand-Sword-Run-Forward-Attack1",
            Attacks=Names("2Hand-Sword-Attack","1","2","4","3","5","6"),Reactions=Names("2Hand-Sword-GetHit-","F1","B1","L1","R1"),
            KitKinds=new[]{"earthsplitter","whirlwind"},KitClips=new[]{"2Hand-Sword-Attack11","2Hand-Sword-Attack8"},Knockdown="2Hand-Sword-Knockdown1",RollBack="2Hand-Sword-Roll-Backward"},"2Hand-Sword-Run-","2Hand-Sword-Strafe-","2Hand-Sword-Run-Forward");
        // Sword: the pack's armed (one weapon in the right hand) set and its one-handed sword's cuts, in the game's combo of
        // three (a forehand cut right to left, a backhand left to right, the thrust): R1, R3, R4, then R6, R2, R4 (their blade
        // tips' sweeps, measured on the body at the bake).
        private static readonly Set Sword=Build(new Set{Spec="sword",Idle="Armed-Idle",Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-R",BlockHit="Armed-Block-R-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Air-Attack-R1",SprintAttack="Armed-Run-Attack-R1",
            Attacks=Names("Sword-Attack-R","1","3","4","6","2","4"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            KitKinds=new[]{"flash-step","rising-dragon"},KitClips=new[]{"Armed-Dash-Forward","Armed-Boost1"},KitHits=new[]{.2f,float.NaN},KitRecast=new[]{null,"Armed-Air-Attack-R1"},Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward"},"Armed-Run-","Armed-Strafe-","Armed-Run-Forward");
        // Daggers: the armed set's dual block and attacks and the pack's dagger cuts, alternating hands in the game's four
        // (a forehand right to left, then three left to right, the last the finisher): R1, L3, R2, L2 (their blade tips'
        // sweeps on the body); the dual running cut opens both blades as the game's twin sprint sweeps do.
        private static readonly Set Daggers=Build(new Set{Spec="daggers",Idle="Armed-Idle",Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-Dual",BlockHit="Armed-Block-Dual-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Air-Attack-L1",SprintAttack="Armed-Run-Attack-Dual",
            Attacks=Names("Dagger-Attack-","R1","L3","R2","L2"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            KitKinds=new[]{"shadow-step","kunai-mark"},KitClips=new[]{"Armed-Dash-Forward","Armed-Cast-R-Attack2_start"},KitHits=new[]{.2f,float.NaN},KitRecast=new[]{null,"Armed-Dash-Forward"},KitThen=new[]{null,"Armed-Cast-R-Attack2_end"},Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward"},"Armed-Run-","Armed-Strafe-","Armed-Run-Forward");
        // Shield: the game holds its shield in the right hand; the pack's shield set carries it on the left arm, so the set
        // plays mirrored (the bake mirrors a clip named with Mirror: left and right swapped). Its bashes and sweeps, mirrored,
        // sweep left to right as every one of the game's shield cuts does: Attack2, Attack3, Attack1.
        private static readonly Set Shield=Build(new Set{Spec="shield",Idle=M("Armed-Shield-Idle"),Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block=M("Armed-Shield-Block"),BlockHit=M("Armed-Shield-Block-GetHit1"),Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack=M("Armed-Shield-Attack1"),SprintAttack=M("Armed-Shield-Attack1"),
            Attacks=Array.ConvertAll(Names("Armed-Shield-Attack","2","3","1"),M),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            KitKinds=new[]{"bulwark-charge","aegis-ricochet"},KitClips=new[]{M("Armed-Shield-Run-Forward-Charge"),"Armed-Cast-R-Attack2_start"},KitHits=new[]{-1f,float.NaN},KitThen=new[]{null,"Armed-Cast-R-Attack2_end"},Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward"},"Armed-Shield-Run-","Armed-Shield-Strafe-",M("Armed-Shield-Run-Forward"),true);
        // Rifle and machine gun: the pack's two-handed shooting set. A low ready carry by default; shouldered (its aim grid's
        // middle column by the pitch, a recoil on each shot) while aiming or just after a shot (owner 2026-10-24: "Aiming and
        // firing"), the reload fitted to the reload.
        private static readonly Set Rifle=Build(new Set{Spec="rifle",Idle="2Hand-Shooting-Idle",Jump="2Hand-Shooting-Jump",Flip="2Hand-Shooting-Jump-Flip",Fall="2Hand-Shooting-Fall",
            Land="2Hand-Shooting-Land",Block="2Hand-Shooting-Block",BlockHit="2Hand-Shooting-Block-GetHit1",Stunned="2Hand-Shooting-Stunned",Roll="2Hand-Shooting-Roll-Forward",
            AirAttack="2Hand-Shooting-Fall",SprintAttack="2Hand-Shooting-Attack1",Attacks=Names("2Hand-Shooting-Attack","1","2"),Reactions=Names("2Hand-Shooting-GetHit-","F1","B1","L1","R1"),
            Aim=Names("2Hand-Shooting-Aiming-","DM","CM","UM"),Fire=Names("2Hand-Shooting-Aiming-Fire-","DM","CM","UM"),Reload="2Hand-Shooting-Reload-Rifle",Knockdown="2Hand-Shooting-Knockdown1",RollBack="2Hand-Shooting-Roll-Backward",
            AbilityIds=new[]{"overdrive"},AbilityStarts=new[]{"2Hand-Shooting-Boost1"},AbilityEnds=new string[1],AbilityRaise=new[]{"orbital-mark","rocket"}},"2Hand-Shooting-Run-","2Hand-Shooting-Strafe-","2Hand-Shooting-Run-Forward");
        // Pistol: the armed set, held low; raised to the pack's pistol stance (arm out) while aiming or just after a shot (its
        // own firing cut throws the arm overhead: the stance holds, the game's muzzle kick is the recoil), its reload fitted.
        private static readonly Set Pistol=Build(new Set{Spec="pistol",Idle="Armed-Idle",Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-R",BlockHit="Armed-Block-R-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Fall",SprintAttack="Pistol-Attack-R1",Attacks=Names("Pistol-Attack-R","1"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            Aim=new[]{"Armed-Idle-Pistol-R","Armed-Idle-Pistol-R","Armed-Idle-Pistol-R"},Reload="Pistol-Reload-R1",Knockdown="Armed-Knockdown1",RollBack="Armed-Roll-Backward",
            AbilityIds=new[]{"frag-grenade"},AbilityStarts=new[]{"Armed-Cast-L-Attack2_start"},AbilityEnds=new[]{"Armed-Cast-L-Attack2_end"}},"Armed-Run-","Armed-Strafe-","Armed-Run-Forward");
        // The casters (the four schools and L33t H4x): the pack's staff set, mirrored (the game's staff is in the left fist, the
        // pack's in the right); a cast plays its start to the game's release and its end after it, a Q/E ability the pack's area
        // cast likewise, on the upper body. Mirrored reactions swap sides.
        private static readonly Set Staff=Build(new Set{Spec="staff",Idle=M("2Hand-Staff-Idle"),Jump=M("2Hand-Staff-Jump"),Flip=M("2Hand-Staff-Jump-Flip"),Fall=M("2Hand-Staff-Fall"),
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
            var all=new System.Collections.Generic.List<string>{s.Idle,s.Jump,s.Flip,s.Fall,s.Land,s.Block,s.BlockHit,s.Stunned,s.Roll,s.AirAttack,s.SprintAttack};
            foreach(var c in new[]{s.Knockdown,s.RollBack,s.Electric,s.Wave,s.Hadoken,s.ChargeStart,s.ChargeLoop,s.Shoryuken,s.Step})if(c!=null&&!all.Contains(c))all.Add(c);
            if(s.Kicks!=null)foreach(var c in s.Kicks)if(!all.Contains(c))all.Add(c);
            all.AddRange(s.Attacks);all.AddRange(s.Reactions);foreach(var l in s.Locomotion)if(!all.Contains(l))all.Add(l);
            foreach(var l in new[]{s.Aim,s.Fire,s.Pull,s.CastStart,s.CastEnd})if(l!=null)foreach(var c in l)if(!all.Contains(c))all.Add(c);
            foreach(var c in new[]{s.Reload,s.AbilityStart,s.AbilityEnd})if(c!=null&&!all.Contains(c))all.Add(c);
            foreach(var l in new[]{s.KitClips,s.KitRecast,s.KitThen,s.AbilityStarts,s.AbilityEnds})if(l!=null)foreach(var c in l)if(c!=null&&!all.Contains(c))all.Add(c);
            s.All=all.ToArray();return s;
        }
        /// <summary>The set a weapon spec plays on the model, or null (the game's own pose: not moved over yet).</summary>
        public static Set For(string spec)=>spec=="greatsword"?Greatsword:spec=="sword"?Sword:spec=="daggers"?Daggers:spec=="shield"?Shield:
            spec=="rifle"||spec=="machinegun"?Rifle:spec=="pistol"?Pistol:spec=="bow"?Bow:
            spec=="water"||spec=="lightning"||spec=="fire"||spec=="darkness"||spec=="leet"?Staff:spec=="emperor"||spec=="ansatsuken"?Unarmed:null;

        /// <summary>A gun's, bow's or caster's upper-body layer this frame: up to two clips mixed (B by Mix) at their times, and
        /// whether the layer is up (Raised: aimed, drawn, reloading or casting). False for melee sets.</summary>
        public struct Upper { public string A, B; public float TimeA, TimeB, Mix; public bool Raised; }
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
        /// <summary>`ready`: the body's own motion leaves the arms free (idle, locomotion, a jump's rise or fall, a landing): a gun
        /// then follows the look unless sprinting (owner 2026-10-24: the low carry did not point where the pilot looks).</summary>
        public static bool Layer(Set s,Actor a,float time,IClips clips,out Upper up,bool ready=false)
        {
            up=default;
            if(s.CastStart!=null)return Cast(s,a,time,clips,out up);
            if(s.Aim==null)return false;
            if(s.Reload!=null&&time>=a.ReloadAt&&time<a.ReloadUntil&&clips.Has(s.Reload))
            {
                float len=clips.Length(s.Reload);up=new Upper{A=s.Reload,TimeA=len*Clamp01((time-a.ReloadAt)/Math.Max(.05f,a.ReloadUntil-a.ReloadAt)),Raised=true};return true;
            }
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
            float age=time-a.LastShot;bool raised=a.Aiming||casting||age<ShotHold(a)||ready&&s.Gun&&!a.Sprinting;
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
        }
        /// <summary>What the model plays this frame: a clip at a time (looping or held at its end), or the locomotion blend.</summary>
        public struct Pick { public string Clip; public float Time; public bool Locomotion; public float Fade;
            /// <summary>Spin: the body is turned about its hips to Turn (radians from the facing, + toward the pilot's left), less
            /// the clip's own turn.</summary>
            public bool Spin; public float Turn; }

        public const float FadeIn=.08f, FadeOut=.2f, LandFor=.22f, ReactionMin=.25f;
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
            if(Kit(set,a,time,clips,out pick))return true;
            // A mapped kit move's tail (its special still running past the cast) goes on to the idle, not the game's pose.
            bool mapped=set.KitKinds!=null&&a.MeleeKit!=null&&Array.IndexOf(set.KitKinds,a.MeleeKit.Kind)>=0;
            if(!mapped&&NativeSpecialPose.Active(a,time)){Declined="special";return false;}
            // Juggled in the air: the fall's flight.
            if(a.JuggleUntil>time){if(set.Knockdown==null||!clips.Has(set.Knockdown)){Declined="juggle";return false;}pick=new Pick{Clip=set.Knockdown,Time=clips.Length(set.Knockdown)*KnockImpact*KnockFlight,Fade=FadeIn};return true;}
            if(!mapped&&a.MeleeKit!=null&&a.MeleeKit.At>-90&&time>=a.MeleeKit.At&&time<a.MeleeKit.Until){Declined="kit";return false;}
            // The Ansatsuken's roll is its Demon Step: the pack's dash.
            if(a.Rolling(time))return Fit(set.Fighter&&a.SourceSpec=="ansatsuken"&&set.Step!=null?set.Step:set.Roll,time,a.RollAt,a.RollAt+a.VisualRollDuration,clips,FadeIn,out pick);
            // The plunging air attack: its Hit on the impact (the landing), else on the clip's own.
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
                        if(time<end){pick=new Pick{Clip=clip,Time=Retime(time,a.SourceAt,hitAt,end,hit,len),Fade=FadeIn};return true;}
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
                float windup=a.VisualWindup>0?a.VisualWindup:.25f,hitAt=a.SwingAt>=a.AttackAt&&a.SwingUntil>a.SwingAt?(a.SwingAt+a.SwingUntil)*.5f:a.AttackAt+windup;
                pick=new Pick{Clip=clip,Time=Retime(time,a.AttackAt,hitAt,Math.Max(hitAt+.05f,a.AttackUntil),clips.Hit(clip),clips.Length(clip)),Fade=FadeIn};return true;
            }
            if(a.ReactionAt>-90&&time>=a.ReactionAt&&time<Math.Max(a.ReactionUntil,a.ReactionAt+ReactionMin))
            {
                string clip=set.Reactions[Side(a.ReactionYaw)];if(!clips.Has(clip))return false;
                return Fit(clip,time,a.ReactionAt,Math.Max(a.ReactionUntil,a.ReactionAt+ReactionMin),clips,FadeIn,out pick);
            }
            if(a.StunUntil>time&&clips.Has(set.Stunned)){pick=new Pick{Clip=set.Stunned,Time=time,Fade=FadeIn};return true;}
            if(time>=a.BlockAt&&time<a.BlockUntil&&clips.Has(set.Block)){pick=new Pick{Clip=set.Block,Time=time-a.BlockAt,Fade=FadeIn};return true;}
            if(!a.Grounded)
            {
                // The air jump's flip, the rise, the fall.
                if(a.AirJumps>0&&a.AirJumpAt>-90&&time>=a.AirJumpAt&&time-a.AirJumpAt<clips.Length(set.Flip)&&clips.Has(set.Flip)){pick=new Pick{Clip=set.Flip,Time=time-a.AirJumpAt,Fade=FadeIn};return true;}
                if(a.Velocity.y>0&&clips.Has(set.Jump)){pick=new Pick{Clip=set.Jump,Time=time-m.AirSince,Fade=FadeIn};return true;}
                if(clips.Has(set.Fall)){pick=new Pick{Clip=set.Fall,Time=time-m.AirSince,Fade=FadeOut};return true;}
                Declined="air";return false;
            }
            if(!moving&&time-m.LandedAt<LandFor&&clips.Has(set.Land)){pick=new Pick{Clip=set.Land,Time=(time-m.LandedAt)*clips.Length(set.Land)/LandFor,Fade=FadeIn};return true;}
            if(moving){pick=new Pick{Locomotion=true,Fade=FadeOut};return true;}
            if(!clips.Has(set.Idle))return false;
            pick=new Pick{Clip=set.Idle,Time=time,Fade=FadeOut};return true;
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
                var w=MeleeKitMotion.Spin(c,time);
                float t=c.Part==KitPart.Windup||c.Part==KitPart.Recast?hit*Clamp01((time-c.At)/Math.Max(1e-3f,c.Release-c.At)):c.Part==KitPart.Active?hit:hit+(len-hit)*Clamp01((time-c.Start)/Math.Max(1e-3f,c.Until-c.Start));
                pick=new Pick{Clip=clip,Time=t,Fade=FadeIn,Spin=true,Turn=w.Grip};return true;
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
                p=new Pick{Clip=clip,Time=Retime(time,at,hitAt,Math.Max(end,hitAt+(len-hit)*.5f),hit,len),Fade=FadeIn};return true;
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
                    float t=time<=last?hit*Clamp01((time-from)/Math.Max(1e-3f,to-from)):hit+(len-hit)*Clamp01((time-last)/Math.Max(.05f,until-last));
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
