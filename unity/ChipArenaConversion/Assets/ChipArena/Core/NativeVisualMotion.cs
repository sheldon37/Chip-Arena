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
            /// <summary>A caster's casts: each one's start (to the release) and end (after it), cycling; its Q/E ability's
            /// likewise. Null for the rest.</summary>
            public string[] CastStart, CastEnd; public string AbilityStart, AbilityEnd;
            /// <summary>The weapon in the model's left fist is a shaft (the staff): it runs along the fist's grip.</summary>
            public bool LeftShaft;
            /// <summary>A gun (its aim layer): the hold calibrated from its level aim, its barrel onto the aim.</summary>
            public bool Gun=>Aim!=null;
            /// <summary>Every clip the set plays (the bake's controller carries them).</summary>
            public string[] All;
        }
        /// <summary>A clip's length and the time of its Hit event (seconds; a clip without one: its middle).</summary>
        public interface IClips { bool Has(string clip); float Length(string clip); float Hit(string clip); }

        // Greatsword: the pack's two-handed sword set.
        private static readonly Set Greatsword=Build(new Set{Spec="greatsword",Idle="2Hand-Sword-Idle",Jump="2Hand-Sword-Jump",Flip="2Hand-Sword-Jump-Flip",Fall="2Hand-Sword-Fall",
            Land="2Hand-Sword-Land",Block="2Hand-Sword-Block",BlockHit="2Hand-Sword-Block-GetHit1",Stunned="2Hand-Sword-Stunned",Roll="2Hand-Sword-Roll-Forward",
            AirAttack="2Hand-Sword-Air-Attack1",SprintAttack="2Hand-Sword-Run-Forward-Attack1",
            Attacks=Names("2Hand-Sword-Attack","1","2","4","3","5","6"),Reactions=Names("2Hand-Sword-GetHit-","F1","B1","L1","R1")},"2Hand-Sword-Run-","2Hand-Sword-Strafe-","2Hand-Sword-Run-Forward");
        // Sword: the pack's armed (one weapon in the right hand) set and its one-handed sword's cuts, in the game's combo of
        // three (a forehand cut right to left, a backhand left to right, the thrust): R1, R3, R4, then R6, R2, R4 (their blade
        // tips' sweeps, measured on the body at the bake).
        private static readonly Set Sword=Build(new Set{Spec="sword",Idle="Armed-Idle",Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-R",BlockHit="Armed-Block-R-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Air-Attack-R1",SprintAttack="Armed-Run-Attack-R1",
            Attacks=Names("Sword-Attack-R","1","3","4","6","2","4"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1")},"Armed-Run-","Armed-Strafe-","Armed-Run-Forward");
        // Daggers: the armed set's dual block and attacks and the pack's dagger cuts, alternating hands in the game's four
        // (a forehand right to left, then three left to right, the last the finisher): R1, L3, R2, L2 (their blade tips'
        // sweeps on the body); the dual running cut opens both blades as the game's twin sprint sweeps do.
        private static readonly Set Daggers=Build(new Set{Spec="daggers",Idle="Armed-Idle",Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-Dual",BlockHit="Armed-Block-Dual-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Air-Attack-L1",SprintAttack="Armed-Run-Attack-Dual",
            Attacks=Names("Dagger-Attack-","R1","L3","R2","L2"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1")},"Armed-Run-","Armed-Strafe-","Armed-Run-Forward");
        // Shield: the game holds its shield in the right hand; the pack's shield set carries it on the left arm, so the set
        // plays mirrored (the bake mirrors a clip named with Mirror: left and right swapped). Its bashes and sweeps, mirrored,
        // sweep left to right as every one of the game's shield cuts does: Attack2, Attack3, Attack1.
        private static readonly Set Shield=Build(new Set{Spec="shield",Idle=M("Armed-Shield-Idle"),Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block=M("Armed-Shield-Block"),BlockHit=M("Armed-Shield-Block-GetHit1"),Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack=M("Armed-Shield-Attack1"),SprintAttack=M("Armed-Shield-Attack1"),
            Attacks=Array.ConvertAll(Names("Armed-Shield-Attack","2","3","1"),M),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1")},"Armed-Shield-Run-","Armed-Shield-Strafe-",M("Armed-Shield-Run-Forward"),true);
        // Rifle and machine gun: the pack's two-handed shooting set. A low ready carry by default; shouldered (its aim grid's
        // middle column by the pitch, a recoil on each shot) while aiming or just after a shot (owner 2026-10-24: "Aiming and
        // firing"), the reload fitted to the reload.
        private static readonly Set Rifle=Build(new Set{Spec="rifle",Idle="2Hand-Shooting-Idle",Jump="2Hand-Shooting-Jump",Flip="2Hand-Shooting-Jump-Flip",Fall="2Hand-Shooting-Fall",
            Land="2Hand-Shooting-Land",Block="2Hand-Shooting-Block",BlockHit="2Hand-Shooting-Block-GetHit1",Stunned="2Hand-Shooting-Stunned",Roll="2Hand-Shooting-Roll-Forward",
            AirAttack="2Hand-Shooting-Fall",SprintAttack="2Hand-Shooting-Attack1",Attacks=Names("2Hand-Shooting-Attack","1","2"),Reactions=Names("2Hand-Shooting-GetHit-","F1","B1","L1","R1"),
            Aim=Names("2Hand-Shooting-Aiming-","DM","CM","UM"),Fire=Names("2Hand-Shooting-Aiming-Fire-","DM","CM","UM"),Reload="2Hand-Shooting-Reload-Rifle"},"2Hand-Shooting-Run-","2Hand-Shooting-Strafe-","2Hand-Shooting-Run-Forward");
        // Pistol: the armed set, held low; raised to the pack's pistol stance (arm out) while aiming or just after a shot (its
        // own firing cut throws the arm overhead: the stance holds, the game's muzzle kick is the recoil), its reload fitted.
        private static readonly Set Pistol=Build(new Set{Spec="pistol",Idle="Armed-Idle",Jump="Armed-Jump",Flip="Armed-Jump-Flip",Fall="Armed-Fall",
            Land="Armed-Land",Block="Armed-Block-R",BlockHit="Armed-Block-R-GetHit1",Stunned="Armed-Stunned",Roll="Armed-Roll-Forward",
            AirAttack="Armed-Fall",SprintAttack="Pistol-Attack-R1",Attacks=Names("Pistol-Attack-R","1"),Reactions=Names("Armed-GetHit-","F1","B1","L1","R1"),
            Aim=new[]{"Armed-Idle-Pistol-R","Armed-Idle-Pistol-R","Armed-Idle-Pistol-R"},Reload="Pistol-Reload-R1"},"Armed-Run-","Armed-Strafe-","Armed-Run-Forward");
        // The casters (the four schools and L33t H4x): the pack's staff set, mirrored (the game's staff is in the left fist, the
        // pack's in the right); a cast plays its start to the game's release and its end after it, a Q/E ability the pack's area
        // cast likewise, on the upper body. Mirrored reactions swap sides.
        private static readonly Set Staff=Build(new Set{Spec="staff",Idle=M("2Hand-Staff-Idle"),Jump=M("2Hand-Staff-Jump"),Flip=M("2Hand-Staff-Jump-Flip"),Fall=M("2Hand-Staff-Fall"),
            Land=M("2Hand-Staff-Land"),Block=M("2Hand-Staff-Block"),BlockHit=M("2Hand-Staff-Block-GetHit1"),Stunned=M("2Hand-Staff-Stunned"),Roll=M("2Hand-Staff-Roll-Forward"),
            AirAttack=M("2Hand-Staff-Air-Attack1"),SprintAttack=M("2Hand-Staff-Run-Forward-Attack1"),Attacks=Array.ConvertAll(Names("2Hand-Staff-Attack","1","2","3"),M),
            Reactions=Array.ConvertAll(Names("2Hand-Staff-GetHit-","F1","B1","R1","L1"),M),
            CastStart=Array.ConvertAll(Names("2Hand-Staff-Cast-Attack","1_start","2_start","3_start"),M),CastEnd=Array.ConvertAll(Names("2Hand-Staff-Cast-Attack","1_end","2_end","3_end"),M),
            AbilityStart=M("2Hand-Staff-Cast-L-AOE1_start"),AbilityEnd=M("2Hand-Staff-Cast-L-AOE1_end"),LeftShaft=true},"2Hand-Staff-Run-","2Hand-Staff-Strafe-",M("2Hand-Staff-Run-Forward"),true);
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
            all.AddRange(s.Attacks);all.AddRange(s.Reactions);foreach(var l in s.Locomotion)if(!all.Contains(l))all.Add(l);
            foreach(var l in new[]{s.Aim,s.Fire,s.CastStart,s.CastEnd})if(l!=null)foreach(var c in l)if(!all.Contains(c))all.Add(c);
            foreach(var c in new[]{s.Reload,s.AbilityStart,s.AbilityEnd})if(c!=null&&!all.Contains(c))all.Add(c);
            s.All=all.ToArray();return s;
        }
        /// <summary>The set a weapon spec plays on the model, or null (the game's own pose: not moved over yet).</summary>
        public static Set For(string spec)=>spec=="greatsword"?Greatsword:spec=="sword"?Sword:spec=="daggers"?Daggers:spec=="shield"?Shield:
            spec=="rifle"||spec=="machinegun"?Rifle:spec=="pistol"?Pistol:
            spec=="water"||spec=="lightning"||spec=="fire"||spec=="darkness"||spec=="leet"?Staff:null;

        /// <summary>A gun's or caster's upper-body layer this frame: up to two clips mixed (B by Mix) at their times, and whether
        /// the layer is up (Raised: aimed, reloading or casting). False for melee sets.</summary>
        public struct Upper { public string A, B; public float TimeA, TimeB, Mix; public bool Raised; }
        /// <summary>A caster's upper body: the ability's (Q/E) or the cast's start fitted to its release, its end after it at
        /// its own pace; afterwards the end's last pose, lowered (Raised false).</summary>
        private static bool Cast(Set s,Actor a,float time,IClips clips,out Upper up)
        {
            up=new Upper{A=s.CastEnd[0],TimeA=clips.Has(s.CastEnd[0])?clips.Length(s.CastEnd[0]):0};
            if(a.AbilityId!=null&&a.AbilityAt>a.AttackAt&&time>=a.AbilityAt)
            {
                float rel=a.AbilityRelease>a.AbilityAt?a.AbilityRelease:a.AbilityAt+.4f;
                if(Phase(s.AbilityStart,s.AbilityEnd,a.AbilityAt,rel,time,clips,ref up))return true;
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
            float age=time-a.LastShot;bool raised=a.Aiming||age<ShotHold(a)||ready&&s.Gun&&!a.Sprinting;
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
        public struct Pick { public string Clip; public float Time; public bool Locomotion; public float Fade; }

        public const float FadeIn=.08f, FadeOut=.2f, LandFor=.22f, ReactionMin=.25f;
        /// <summary>Why the last Choose declined (null when it chose): for the local checks.</summary>
        public static string Declined;

        /// <summary>The model's clip for pilot `a` at `time` (false: not covered; the game's pose shows).</summary>
        public static bool Choose(Set set,Actor a,float time,Memory m,IClips clips,bool moving,out Pick pick)
        {
            pick=default;
            if(a.Grounded!=m.WasGrounded){if(a.Grounded)m.LandedAt=time;else m.AirSince=time;m.WasGrounded=a.Grounded;}
            Declined=null;
            if(NativeSpecialPose.Down(a,time)){Declined="down";return false;}
            if(NativeSpecialPose.Active(a,time)){Declined="special";return false;}
            if(a.JuggleUntil>time){Declined="juggle";return false;}
            if(a.MeleeKit!=null&&a.MeleeKit.At>-90&&time>=a.MeleeKit.At&&time<a.MeleeKit.Until){Declined="kit";return false;}
            if(a.Rolling(time))return Fit(set.Roll,time,a.RollAt,a.RollAt+a.VisualRollDuration,clips,FadeIn,out pick);
            // The plunging air attack: its Hit on the impact (the landing), else on the clip's own.
            if(a.AirAttack&&a.AttackAt>-90&&time>=a.AttackAt)
            {
                if(!clips.Has(set.AirAttack))return false;
                float hit=clips.Hit(set.AirAttack),len=clips.Length(set.AirAttack),impact=a.ImpactAt>a.AttackAt?a.ImpactAt:a.AttackAt+hit;
                pick=new Pick{Clip=set.AirAttack,Time=Retime(time,a.AttackAt,impact,impact+(len-hit),hit,len),Fade=FadeIn};return true;
            }
            // A caster's cast plays on the upper-body layer (Layer); its strike rules are not melee.
            if(a.Attacking(time)&&set.CastStart==null)
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
