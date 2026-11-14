using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The blade classes on the model (owner 2026-11-10: "Use the new animation pack for both classes"): the Frank Slash Pack's katana set for
    /// the Katana and its assassin set (with its stealth kills and criticals) for the Fangs, every move a clip window fitted to the rules: the
    /// window's strike (measured on the pack's own blade: its fastest moment) on the move's hit, its wind-up before it and its recovery after,
    /// at most StrikeRate times the clip's pace (a wind-up too short starts further into the window). A paired move's victim plays the matching
    /// clip from the same moment. The clips' names are the pack's (a file's own clip when it names none). Presentation only.
    /// </summary>
    public static class NativeBladeMotion
    {
        private const string K="Frank_RPG_Katana_",A="Frank_RPG_Assassin_",R="Frank_Rogue_",D="Frank_RPG_Dual_";
        // ---- the Katana's clips ----
        public static readonly string[] KatanaCuts={K+"S1_Combo01_01",K+"S1_Combo01_02",K+"S1_Combo01_03",K+"S1_Combo03_03"};
        public const string KatanaRush=K+"S3_Combo01_02",KatanaIai=K+"S3_Skill01",KatanaSheathe=K+"Unequip",KatanaStorm=K+"S3_Skill02",KatanaIdle=K+"Stance1_Idle",
            KatanaGuard=K+"Guard",KatanaBlock=K+"Block",KatanaTumble=K+"Tumbling_F",KatanaDraw=K+"Stance1_Equip";
        /// <summary>The counter's paired critical: the wielder's clip and its victim's.</summary>
        public const string KatanaCritical="Frank_Damage@Damage_Critical_Katana",KatanaCriticalHit="Frank_Damage@Damage_Critical_Katana_Hit";
        // ---- the Fangs' clips ----
        // The flurry is the Frank Dual (twin swords) set's chain (the owner's pick 2026-11-13; it was the Rogue set's third and fourth
        // combinations): its first combo's flat forehand, springing double and bent-over lunging cut, its sixth combo's long forehand lunge,
        // then its first combo's low spinning sweep as the finisher. The set ships as root-motion takes only; the bake plays them in place,
        // and the rules carry the pilot along their roots' way (FangsTravelShare, owner 2026-11-13).
        // The dash is the Rogue (dual blade) set's fifth chain's long low lunge with both blades out (owner 2026-11-11: "utilize the Rogue").
        public static readonly string[] FangsCuts={D+"Combo01_1",D+"Combo01_2",D+"Combo01_3",D+"Combo06_1",D+"Combo01_4"};
        public const string FangsPounce=R+"Combination_05_2",FangsStep=A+"Skill01_IP",FangsVeil=A+"Evade_B_IP",FangsFan=A+"Attack06_IP",FangsBloom=A+"Skill02_IP",FangsIdle=A+"Equip_Idle",
            FangsGuard=A+"Guard",FangsBlock=A+"Block",FangsTumble=A+"Tumbling_F";
        /// <summary>The takedown from behind and the frontal critical: the wielder's clips and their victims'. The takedown is the stealth
        /// kill's throat cut (the owner's pick 2026-11-13: `Behind_01`, the foe's head held with a fang at its throat, the fang drawn across
        /// on the blow; it was `Behind_05`), its foe on the matching `Hit_Behind_01`.</summary>
        public const string FangsTakedown="Frank_Stealth_Kill_Behind_01",FangsTakedownHit="Frank_Stealth_Kill_Hit_Behind_01",FangsCritical="Frank_Damage@Damage_Critical_Assassin",FangsCriticalHit="Frank_Damage@Damage_Critical_Assassin_Hit";
        /// <summary>A paired attacker clip's victim clip (null: none).</summary>
        public static string VictimClip(string clip)=>clip==KatanaCritical?KatanaCriticalHit:clip==FangsTakedown?FangsTakedownHit:clip==FangsCritical?FangsCriticalHit:null;
        /// <summary>Every paired move's victim clip, as the host names them on the held foe (Actor.PairClip, NativeDevMotions.VictimClip): the
        /// Katana's counter, the DEV Katana's, the Fangs' takedown and frontal critical. Any pilot's model plays the one it is held in, whatever
        /// its class (owner 2026-11-13: "Give one to everyone"): a set that does not list it borrows it from the motion bank the first time the
        /// host names it (Lent; Runtime VisualMotionLend), and a class with no set of its own plays it on NativeVisualMotion.Held.</summary>
        public static readonly string[] VictimClips={KatanaCriticalHit,NativeDevKatanaMotion.CounterHit,FangsTakedownHit,FangsCriticalHit};
        /// <summary>Whether `clip` is the paired victim clip the host names on pilot `a` (a body whose set lacks it borrows it; nothing else is
        /// borrowed, and the client never picks one of its own).</summary>
        public static bool Lent(Actor a,string clip)=>a!=null&&clip!=null&&clip==a.PairClip&&Array.IndexOf(VictimClips,clip)>=0;
        /// <summary>Whether `clip` is a counter's victim clip, shown from the blow only (Victim): the Katana's and the DEV Katana's counters
        /// answer a parry, so their foe is dazed until struck. The Fangs' takedown and critical keep their whole paired window (the foe is
        /// seized before the blow).</summary>
        public static bool StruckOnBlow(string clip)=>clip==KatanaCriticalHit||clip==NativeDevKatanaMotion.CounterHit;
        /// <summary>Whether a paired move holds pilot `a` at `time` (the span Victim plays its clip over, the last 0.15 s fading back).</summary>
        public static bool Holds(Actor a,float time)=>a!=null&&!string.IsNullOrEmpty(a.PairClip)&&a.PairAt>-50&&time>=a.PairAt&&time<a.PairUntil+.15f;
        /// <summary>Whether `spec`'s weapon hangs head-down from the grip while its pilot plays a victim clip (Runtime VisualVictimHang: its
        /// length along the hand's forward, as every gun's barrel, blade and the mallet's handle run). One rule for every class: a weapon
        /// held flat-on across the hand (its tip off that axis: the shield, whose rim stands up the hand's side) keeps its own hang.</summary>
        public static bool Hangs(string spec)=>!(NativeWeaponDressing.BladeTip(spec,out var tip)&&Math.Abs(tip.Z)<Math.Abs(tip.Y));
        /// <summary>How far `spec`'s weapon reaches from the wrist along its length (m: a blade's tip, the mallet's head, a gun's muzzle): a
        /// hung weapon tips out to its hand's side just enough to keep that end above the floor (Runtime VisualVictimHang).</summary>
        public static float HangReach(string spec)
        {
            if(spec==NativeTopDog.Spec)return NativeWeaponModels.MalletHead+NativeWeaponModels.MalletRadius;
            if(NativeWeaponDressing.BladeTip(spec,out var tip))return tip.Length();
            return spec=="pistol"?.35f:spec=="rifle"||spec=="machinegun"?1.05f:NativeWeaponDressing.BladeLength(spec)+.28f;
        }

        /// <summary>A clip window: where it starts, its strike and where it ends (s into the clip).</summary>
        public readonly struct Window { public readonly float From,Hit,To;public Window(float from,float hit,float to){From=from;Hit=hit;To=to;} }
        /// <summary>Each clip's window, measured on the pack's blade (its fastest moment is the strike; docs/KATANA.md, docs/FANGS.md "Motion").</summary>
        public static Window WindowOf(string clip)
        {
            switch(clip)
            {
                case K+"S1_Combo01_01":return new Window(0,.367f,.85f);
                case K+"S1_Combo01_02":return new Window(0,.2f,.8f);
                case K+"S1_Combo01_03":return new Window(0,.183f,.9f);
                case K+"S1_Combo03_03":return new Window(0,.333f,1.2f);
                case KatanaRush:return new Window(0,.183f,.9f);
                case KatanaIai:return new Window(.7f,1.3f,1.75f);
                case KatanaSheathe:return new Window(1.3f,1.85f,2.35f);
                case KatanaStorm:return new Window(1f,1.3f,1.78f);
                case KatanaCritical:case KatanaCriticalHit:return new Window(.9f,1.45f,2.25f);
                // The Dual chain: each strike where a fang is fastest (the left in the first four cuts, the right in the finisher).
                case D+"Combo01_1":return new Window(0,.43f,.9f);
                case D+"Combo01_2":return new Window(0,.26f,.72f);
                case D+"Combo01_3":return new Window(0,.24f,.7f);
                case D+"Combo06_1":return new Window(0,.3f,.75f);
                case D+"Combo01_4":return new Window(0,.24f,.85f);
                case FangsPounce:return new Window(.95f,1.28f,1.85f);
                case FangsStep:return new Window(.45f,.7f,1.15f);
                case FangsVeil:return new Window(0,.2f,.58f);
                case FangsFan:return new Window(0,.35f,.8f);
                case FangsBloom:return new Window(.45f,.9f,1.5f);
                // The throat cut: the fang drawn across the throat at 1.3 s (the foe falls from about 1.56 s); both bodies on one window.
                case FangsTakedown:case FangsTakedownHit:return new Window(.02f,1.3f,1.98f);
                case FangsCritical:case FangsCriticalHit:return new Window(.5f,1.1f,1.7f);
                default:return NativeDevKatanaMotion.WindowOf(clip);
            }
        }
        /// <summary>At most this many times a clip's pace, before its strike (a move too quick starts later into the window).</summary>
        public const float StrikeRate=1.6f;
        /// <summary>The Rogue's dual-blade clips (the dash's) are slower than the daggers' quick cuts: they may run twice their pace before the
        /// strike. The Dual chain's keep StrikeRate.</summary>
        public static float RateOf(string clip)=>clip!=null&&clip.StartsWith(R,StringComparison.Ordinal)?2f:StrikeRate;
        /// <summary>The clip time at `time` for a move from `start` striking at `hitAt` and ending at `end`, on the clip's window (the clip's
        /// own length `len` when it has none: its middle the strike).</summary>
        public static float Fit(string clip,float len,float time,float start,float hitAt,float end)
        {
            var w=WindowOf(clip);float from=float.IsNaN(w.From)?0:w.From,hit=float.IsNaN(w.Hit)?len*.5f:Math.Min(w.Hit,len),to=float.IsNaN(w.To)?len:Math.Min(w.To,len);
            float rate=RateOf(clip),first=Math.Max(from,hit-Math.Max(1e-3f,hitAt-start)*rate);
            if(time<=hitAt)return first+(hit-first)*Clamp01((time-start)/Math.Max(1e-3f,hitAt-start));
            return hit+(to-hit)*Clamp01((time-hitAt)/Math.Max(Math.Max(1e-3f,end-hitAt),(to-hit)/rate));
        }

        /// <summary>The move a blade pilot's body plays at `time` (the whole body), or false (none: the set's own stance and stride). `moving`:
        /// the pilot travels under its own stride (a cut then plays on the upper body over the stride, Upper).</summary>
        public static bool Cast(NativeVisualMotion.Set set,Actor a,float time,NativeVisualMotion.IClips clips,bool moving,out NativeVisualMotion.Pick pick)
        {
            pick=default;
            // The class's entrance: the intro body plays the clip the script names (Core NativeBladeIntros.Clip).
            if(IntroActor>=0&&a!=null&&a.Id==IntroActor&&IntroClip!=null&&clips.Has(IntroClip)){pick=new NativeVisualMotion.Pick{Clip=IntroClip,Time=IntroTime,Fade=IntroFade};return true;}
            if(Slam(set,a,time,clips,out pick))return true;
            var k=NativeBladeView.Of(a);if(k?.CastKind==null||time<k.CastAt)return false;
            if(!Clip(a,k,time,clips,out string clip,out float t,out bool whole,out bool flight))return false;
            if(!whole&&moving)return false;
            pick=new NativeVisualMotion.Pick{Clip=clip,Time=t,Fade=BlendIn(clip),Flight=flight};return true;
        }
        /// <summary>The plunging slam (the standard swing's air attack of kind "down", Core NativeAirAttackSimulation; the Glaive, the
        /// Katana and the Fangs take it, owner 2026-11-11): the set's air attack clip, run to just short of its strike through the drop and
        /// held there, its strike on the landing, then its recovery. The Glaive's motion takes it too (NativeVisualMotion.GlaiveCast).</summary>
        public static bool Slam(NativeVisualMotion.Set set,Actor a,float time,NativeVisualMotion.IClips clips,out NativeVisualMotion.Pick pick)
        {
            pick=default;
            if(a==null||set?.AirAttack==null||!a.AirAttack||a.AttackAt<-90||time<a.AttackAt||NativePlungeFields.Kind(a)!=NativePlungeFields.DownKind||!clips.Has(set.AirAttack))return false;
            float hit=clips.Hit(set.AirAttack),len=clips.Length(set.AirAttack);
            float impact=a.ImpactAt>a.AttackAt?a.ImpactAt:Math.Max(a.AttackAt+hit,time+.08f);
            pick=new NativeVisualMotion.Pick{Clip=set.AirAttack,Time=NativeVisualMotion.Retime(time,a.AttackAt,impact,impact+(len-hit),hit,len),Fade=NativeVisualMotion.FadeIn};return true;
        }
        /// <summary>The entrance's intro body (its actor id, -1: none) and the clip, time and fade its script names this frame (set around
        /// the intro body's motion only; Core NativeBladeIntro.Fade).</summary>
        public static int IntroActor=-1;public static string IntroClip;public static float IntroTime,IntroFade=.12f;
        /// <summary>A cut on the move: the cut's clip on the upper body over the stride.</summary>
        public static bool Upper(NativeVisualMotion.Set set,Actor a,float time,NativeVisualMotion.IClips clips,out NativeVisualMotion.Upper up)
        {
            up=default;var k=NativeBladeView.Of(a);if(k?.CastKind==null||time<k.CastAt)return false;
            if(!Clip(a,k,time,clips,out string clip,out float t,out bool whole,out _)||whole)return false;
            up=new NativeVisualMotion.Upper{A=clip,TimeA=t,Raised=true};return true;
        }
        /// <summary>A move's blend in: NativeVisualMotion.FadeIn, or its clip's own (the DEV Katana's finisher, NativeDevKatanaMotion.BlendIn).</summary>
        public static float BlendIn(string clip){float f=NativeDevKatanaMotion.BlendIn(clip);return float.IsNaN(f)?NativeVisualMotion.FadeIn:f;}
        /// <summary>The DEV Katana cut a blade pilot's kit last played (null: none, or not the DEV Katana's) and when that move ends
        /// (`ends`: where Clip stops showing it).</summary>
        private static string DevCut(Actor a,NativeBladeKit k,out float ends)
        {
            ends=float.NaN;if(a==null||k==null||a.SourceSpec!=NativeKatana.Spec||a.Motion!=NativeDevMotions.Katana||k.CastKind!=NativeKatana.Cut)return null;
            float at=k.CastAt,rel=Math.Max(at+.01f,k.CastRelease);ends=Math.Max(rel+.05f,k.CastUntil)+.12f;var cuts=NativeDevKatanaMotion.Cuts;return cuts[Math.Max(0,k.CastStep)%cuts.Length];
        }
        /// <summary>The stance's (or the stride's) blend in at `time`: `fade`, or longer through the settle after a move whose clip has its
        /// own (NativeDevKatanaMotion.BlendOut: the finisher's lunge, turned away from the stance).</summary>
        public static float SettleFade(Actor a,float time,float fade)
        {
            string clip=DevCut(a,NativeBladeView.Of(a),out float ends);if(clip==null)return fade;float f=NativeDevKatanaMotion.BlendOut(clip);
            return !float.IsNaN(f)&&time>=ends-.05f&&time<ends+f+.1f?f:fade;
        }
        /// <summary>Whether a clip, fading out, holds its last pose (NativeDevKatanaMotion.BlendOut: it runs on into other cuts).</summary>
        public static bool HeldOut(string clip)=>!float.IsNaN(NativeDevKatanaMotion.BlendOut(clip));
        /// <summary>When a blade cut's trail starts (the arena's clock): the move's start, or for a clip whose wind-up leaves none
        /// (NativeDevKatanaMotion.TrailFrom, s into the clip) the moment its fitted wind-up reaches that time (as Fit maps it).</summary>
        public static float TrailFrom(Actor a,NativeBladeKit k)
        {
            if(k==null)return float.NaN;string clip=DevCut(a,k,out _);float from=clip!=null?NativeDevKatanaMotion.TrailFrom(clip):float.NaN;if(float.IsNaN(from))return k.CastAt;
            var w=WindowOf(clip);float at=k.CastAt,rel=Math.Max(at+.01f,k.CastRelease),first=Math.Max(float.IsNaN(w.From)?0:w.From,w.Hit-Math.Max(1e-3f,rel-at)*RateOf(clip));
            return from<=first?at:from>=w.Hit?rel:at+(from-first)/(w.Hit-first)*(rel-at);
        }
        /// <summary>The clip a blade move plays at `time` and its time; `whole`: it takes the whole body even on the move; `flight`: its own
        /// height is kept (the pounce's leap).</summary>
        public static bool Clip(Actor a,NativeBladeKit k,float time,NativeVisualMotion.IClips clips,out string clip,out float t,out bool whole,out bool flight)
        {
            clip=null;t=0;whole=true;flight=false;float at=k.CastAt,rel=Math.Max(at+.01f,k.CastRelease),until=Math.Max(rel+.05f,k.CastUntil);
            if(a.SourceSpec==NativeKatana.Spec)
            {
                if(time>=until+.12f)return false;
                // The DEV Katana plays these moves on its own clips (Core NativeDevKatanaMotion; the iai and the counter keep these).
                bool dev=a.Motion==NativeDevMotions.Katana;string[] cuts=dev?NativeDevKatanaMotion.Cuts:KatanaCuts;
                switch(k.CastKind)
                {
                    case NativeKatana.Cut:whole=k.CastStep>=2;return On(cuts[Math.Max(0,k.CastStep)%cuts.Length],at,rel,until,time,clips,out clip,out t);
                    case NativeKatana.Rush:return On(dev?NativeDevKatanaMotion.Rush:KatanaRush,at,rel+NativeKatana.RushTime*.5f,until,time,clips,out clip,out t);
                    case NativeKatana.Iai:
                    {
                        // The draw stance and the dash cut, then the blade slid home with its click on the split.
                        float dashEnd=rel+NativeKatana.IaiDash;if(time<dashEnd+.06f||k.SplitAt<-50)return On(KatanaIai,at,rel+NativeKatana.IaiDash*.5f,dashEnd+.06f,time,clips,out clip,out t);
                        return On(KatanaSheathe,dashEnd+.06f,k.SplitAt,Math.Max(k.SplitAt+.05f,until),time,clips,out clip,out t);
                    }
                    case NativeKatana.Storm:
                    {
                        // The flurry's span stretched across the cuts (Thousand Petals: more of them).
                        float span=until-rel-NativeKatana.StormRecover;return On(dev?NativeDevKatanaMotion.Storm:KatanaStorm,at,rel,rel+span+NativeKatana.StormRecover,time,clips,out clip,out t);
                    }
                    case NativeKatana.Counter:return On(dev?NativeDevKatanaMotion.Counter:KatanaCritical,at,k.PairHit>-50?k.PairHit:rel,until,time,clips,out clip,out t);
                }
                return false;
            }
            if(a.SourceSpec==NativeFangs.Spec)
            {
                if(time>=until+.1f)return false;
                switch(k.CastKind)
                {
                    // A cut that lunges (NativeFangs.CutLunge: every one but the fourth, along its clip's root, FangsTravelShare) takes the
                    // whole body, its legs carrying the lunge (the rules' step read as moving, so a lunge played on the upper body over a walk);
                    // the fourth the upper body over the stride while moving.
                    case NativeFangs.Cut:whole=NativeFangs.CutLunge[Math.Max(0,k.CastStep)%NativeFangs.Steps]>0;return On(FangsCuts[Math.Max(0,k.CastStep)%FangsCuts.Length],at,rel,until,time,clips,out clip,out t);
                    case NativeFangs.Pounce:return On(FangsPounce,at,rel+NativeFangs.PounceTime*.5f,until,time,clips,out clip,out t);
                    case NativeFangs.Step:return On(FangsStep,at,rel+.04f,until,time,clips,out clip,out t);
                    case NativeFangs.Veil:return On(FangsVeil,at,rel,until,time,clips,out clip,out t);
                    case NativeFangs.Fan:whole=false;return On(FangsFan,at,rel,until,time,clips,out clip,out t);
                    // The bloom's strike lunges (NativeFangs.BloomLunge), so its legs carry it even on the move.
                    case NativeFangs.Bloom:return On(FangsBloom,at,rel,until,time,clips,out clip,out t);
                    case NativeFangs.Assassinate:
                    {
                        string c=k.PairClip??FangsCritical;return On(c,at,k.PairHit>-50?k.PairHit:rel,until,time,clips,out clip,out t);
                    }
                }
            }
            return false;
        }
        /// <summary>A clip window fitted to a move (false: the bank lacks the clip).</summary>
        private static bool On(string c,float start,float hitAt,float end,float time,NativeVisualMotion.IClips clips,out string clip,out float t){clip=null;t=0;if(c==null||!clips.Has(c))return false;clip=c;t=Fit(c,clips.Length(c),time,start,hitAt,end);return true;}
        /// <summary>A pilot held in a paired move: its body plays the victim's clip from the move's start (false: not held).</summary>
        public static bool Victim(Actor a,float time,NativeVisualMotion.IClips clips,out NativeVisualMotion.Pick pick)
        {
            pick=default;if(a==null||string.IsNullOrEmpty(a.PairClip)||a.PairAt<-50||time<a.PairAt||time>=a.PairUntil+.15f||!clips.Has(a.PairClip))return false;
            var w=WindowOf(a.PairClip);float len=clips.Length(a.PairClip);float hit=float.IsNaN(w.Hit)?len*.5f:w.Hit,from=float.IsNaN(w.From)?0:w.From,to=float.IsNaN(w.To)?len:Math.Min(len,w.To);
            // The victim's clip runs on the same window as its attacker's, from the same moment.
            float span=Math.Max(.05f,a.PairUntil-a.PairAt),u=Clamp01((time-a.PairAt)/span),t=from+(to-from)*u;
            // A counter's foe is struck on the blow (StruckOnBlow: the Katana's and the DEV Katana's, both answering a parry): its clip shows
            // from the blow, and before it the foe plays on what it played, the parry's daze, rather than standing through the wind-up or
            // holding the hit's first frame (owner 2026-11-13: "They still kind of just sit there").
            if(t<0||StruckOnBlow(a.PairClip)&&t<hit){pick=default;return false;}
            pick=new NativeVisualMotion.Pick{Clip=a.PairClip,Time=t,Fade=NativeVisualMotion.FadeIn};return true;
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        /// <summary>How far a paired move's victim is thrown back at `time` (m, on along its way from the wielder), as its clip carries it: the
        /// Katana's critical (`attackerClip`, the record's) throws its victim's body 1.23 m back after the blow (the victim clip's root travel,
        /// read from the pack's FBX in Blender), and the hold follows it (owner 2026-11-11: "Parry with katana make enemy walk backwards in
        /// place": held where it stood, the victim stepped back on the spot). The victim's clip time as Victim plays it; 0 for the other
        /// paired moves.</summary>
        public static float VictimTravel(string attackerClip,float time,float at,float until)
        {
            if(attackerClip!=KatanaCritical||!(until>at))return 0;
            var w=WindowOf(KatanaCriticalHit);float c=w.From+(w.To-w.From)*Clamp01((time-at)/(until-at));
            if(c<=KatanaKnock[0])return 0;
            for(int i=2;i<KatanaKnock.Length;i+=2)if(c<=KatanaKnock[i]){float u=(c-KatanaKnock[i-2])/(KatanaKnock[i]-KatanaKnock[i-2]);return KatanaKnock[i-1]+(KatanaKnock[i+1]-KatanaKnock[i-1])*u;}
            return KatanaKnock[KatanaKnock.Length-1];
        }
        /// <summary>The Katana's critical's victim clip's travel back (clip s, m), from its root in the pack's FBX.</summary>
        private static readonly float[] KatanaKnock={1.45f,0,1.5f,.1f,1.75f,.67f,2f,1.08f,2.25f,1.23f};

        // ---- the Fangs' own travel (owner 2026-11-13: "Review fangs animations; The movements look like they should be moving the character location") ----
        /// <summary>How far each travelling Fangs clip's root has gone along its facing (m on the game body), 30 a second from its window's
        /// start: the pack's root-motion takes (the Dual chain ships as nothing else; the assassin set's sit beside its in-place takes) read
        /// in the FBX, the root bone's forward way at 0.835 of the pack's skeleton (the bake body against it, fitted over the drawn frames of
        /// the Dual clips). The bake leaves every root's travel out and draws the clips in place, so the rules carry the pilot along it: the
        /// length is the rules' (NativeFangs.CutLunge, VeilHop, BloomLunge), this the timing. The root's sideways drift (under 7 cm) is
        /// left out; the root's forward is the facing (where the clip strikes). The fourth cut's comes back to where it started (17 cm out,
        /// 6 cm net), the fan's barely moves, the pounce's dash and the shadow step's blink keep their own (docs/FANGS.md "Travel").</summary>
        private static readonly float[] Cut1Way={0,.008f,.014f,.019f,.024f,.029f,.036f,.045f,.057f,.072f,.093f,.127f,.179f,.245f,.321f,.42f,.539f,.65f,.721f,.751f,.76f,.754f,.741f,.727f,.716f,.701f,.683f,.663f},
            Cut2Way={0,.105f,.212f,.316f,.41f,.492f,.568f,.64f,.711f,.784f,.866f,.953f,1.029f,1.079f,1.108f,1.134f,1.156f,1.176f,1.193f,1.208f,1.221f,1.233f,1.24f},
            Cut3Way={0,.089f,.179f,.268f,.356f,.448f,.542f,.628f,.696f,.748f,.795f,.836f,.873f,.906f,.935f,.96f,.975f,.977f,.974f,.972f,.979f,1.001f},
            Cut5Way={0,.062f,.117f,.175f,.25f,.354f,.565f,.833f,.986f,1.016f,1.037f,1.056f,1.075f,1.1f,1.134f,1.176f,1.222f,1.273f,1.325f,1.379f,1.433f,1.487f,1.537f,1.584f,1.627f,1.664f,1.679f},
            VeilWay={0,-.219f,-.438f,-.654f,-.862f,-1.07f,-1.301f,-1.557f,-1.81f,-2.029f,-2.203f,-2.337f,-2.422f,-2.464f,-2.485f,-2.492f,-2.494f,-2.499f};
        /// <summary>The bloom's two-handed strike (Skill02; the Fangs' entrance strikes on it too, Core NativeFangsIntro): its lunge, from
        /// clip .45 s.</summary>
        public static readonly float[] BloomWay={0,.004f,.009f,.018f,.024f,.022f,.03f,.042f,.049f,.055f,.084f,.082f,.51f,.974f,1.205f,1.226f,1.241f,1.251f,1.256f,1.257f,1.254f,1.248f,1.24f,1.23f,1.219f,1.208f,1.196f,1.185f,1.176f,1.162f,1.114f,1.037f,.944f,.847f,.759f,.722f};
        public const float WayRate=30,BloomWayFrom=.45f;
        private static float[] WayOf(string clip,out float from)
        {
            from=0;
            switch(clip){case D+"Combo01_1":return Cut1Way;case D+"Combo01_2":return Cut2Way;case D+"Combo01_3":return Cut3Way;case D+"Combo01_4":return Cut5Way;case FangsVeil:return VeilWay;case FangsBloom:from=BloomWayFrom;return BloomWay;default:return null;}
        }
        /// <summary>A table's value at clip time `c` (`from`: its first sample's time), between its samples, held at its ends.</summary>
        public static float Sample(float[] way,float from,float c)
        {
            if(way==null||way.Length==0)return 0;float f=(c-from)*WayRate;if(!(f>0))return way[0];int last=way.Length-1;if(f>=last)return way[last];int i=(int)f;return way[i]+(way[i+1]-way[i])*(f-i);
        }
        /// <summary>How far `clip`'s root has gone along its facing at clip time `c` (m; 0 for a clip without a table).</summary>
        public static float Way(string clip,float c){var w=WayOf(clip,out float from);return w==null?0:Sample(w,from,c);}
        /// <summary>The clip a travelling Fangs move plays and the times Clip fits it on (false: the move has no travel of its own).</summary>
        private static bool Travels(NativeBladeKit k,out string clip,out float at,out float rel,out float until)
        {
            clip=null;at=rel=until=0;if(k?.CastKind==null)return false;at=k.CastAt;rel=Math.Max(at+.01f,k.CastRelease);until=Math.Max(rel+.05f,k.CastUntil);
            switch(k.CastKind){case NativeFangs.Cut:clip=FangsCuts[Math.Max(0,k.CastStep)%FangsCuts.Length];break;case NativeFangs.Veil:clip=FangsVeil;break;case NativeFangs.Bloom:clip=FangsBloom;break;default:return false;}
            return WayOf(clip,out _)!=null;
        }
        /// <summary>A travelling Fangs move's share of its way at `time`: its clip's root from where the slice the game plays starts (as Fit
        /// lays it) to the window's end, at the clip time Clip shows, so the pilot moves when the drawn feet do. It passes 1 where the root
        /// runs on and comes back (the first cut, the bloom). False for a move without travel (the rules' path keeps its own easing).</summary>
        public static bool FangsTravelShare(NativeBladeKit k,float time,out float share)
        {
            share=0;if(!Travels(k,out string clip,out float at,out float rel,out float until))return false;
            var w=WindowOf(clip);float first=Math.Max(w.From,w.Hit-Math.Max(1e-3f,rel-at)*RateOf(clip)),from=Way(clip,first),span=Way(clip,w.To)-from;
            if(Math.Abs(span)<1e-3f)return false;share=(Way(clip,Fit(clip,w.To,time,at,rel,until))-from)/span;return true;
        }
        /// <summary>When a travelling Fangs move's way ends (its clip at its window's end, no later than Clip shows the move) and its farthest
        /// share on the way (1, or more where the root runs on and comes back).</summary>
        public static bool FangsTravelSpan(NativeBladeKit k,out float ends,out float peak)
        {
            ends=0;peak=1;if(!Travels(k,out string clip,out float at,out float rel,out float until))return false;
            var w=WindowOf(clip);ends=Math.Min(rel+Math.Max(Math.Max(1e-3f,until-rel),(w.To-w.Hit)/RateOf(clip)),until+.1f);
            if(!FangsTravelShare(k,at,out _))return false;for(float t=at;t<=ends+1e-4f;t+=1/60f){FangsTravelShare(k,t,out float s);peak=Math.Max(peak,s);}
            return true;
        }
        // ---- the Fangs' cuts' own turn (presentation; the travel's follow-up: "the feet still slide through it", docs/FANGS.md "Travel") ----
        /// <summary>The turn the bake took out of each Fangs cut (degrees about the vertical, + toward the pilot's right, 30 a second from
        /// clip 0). The Dual chain ships as root-motion takes, and the bake keeps their body facing the rig's forward frame by frame (the
        /// clip's own turn went to the root motion it drops), so the drawn legs pivoted round a body that did not turn while the clips spin
        /// it through up to 200 degrees a cut; with the pilot carried along the roots (FangsTravelShare) the feet slid. These put it back:
        /// the bearing of the drawn body against the pack's authored one, root forward on the pilot's facing (a flat fit of the bake body's
        /// joints, frame by frame, onto the root-motion take's at 0.835 of its skeleton). With it the clip strikes along its root's forward
        /// (the facing; the drawn body at the strike within about 25 degrees of it) and the feet it plants stay planted while the rules carry
        /// the pilot. Runtime VisualMotionTurn turns the drawn body about its hips by the mix's share of it; the rules' facing is untouched.</summary>
        private static readonly float[] Cut1Turn={59,76,96,114,125,132,137,140,139,134,123,100,63,23,-14,-52,-91,-122,-143,-154,-160,-162,-159,-149,-134,-112,-84,-55,-28,-4,16,32,42,50,54,57,58,59,59},Cut2Turn={-143,-162,-184,-197,-190,-160,-113,-59,-3,41,74,103,124,133,134,131,127,121,115,107,100,92,85,78,72,68,65,62,61,59,59},Cut3Turn={133,133,134,131,117,86,47,2,-39,-69,-95,-115,-130,-138,-141,-138,-130,-119,-106,-91,-75,-59,-43,-28,-13,0,11,21,31,40,48,55,59},Cut4Turn={59,70,90,109,120,128,128,115,80,26,-26,-58,-82,-96,-95,-71,-14,53,105,127,130,125,120,113,105,96,88,80,73,68,65,63,61,60,59,59},Cut5Turn={-137,-147,-161,-174,-178,-166,-108,-28,9,15,18,20,20,20,19,20,21,23,25,28,31,35,40,44,49,52,55,57,58,59,59,59};
        private static float[] TurnOf(string clip)
        {
            switch(clip){case D+"Combo01_1":return Cut1Turn;case D+"Combo01_2":return Cut2Turn;case D+"Combo01_3":return Cut3Turn;case D+"Combo06_1":return Cut4Turn;case D+"Combo01_4":return Cut5Turn;default:return null;}
        }
        /// <summary>Whether `clip`'s own turn is put back on the drawn body (the Fangs' five cuts; no other clip of any set).</summary>
        public static bool Turns(string clip)=>clip!=null&&TurnOf(clip)!=null;
        /// <summary>`clip`'s own turn at clip time `c` (degrees, + toward the pilot's right; held at the clip's ends), false for a clip without.</summary>
        public static bool Turns(string clip,float c,out float degrees){var w=clip!=null?TurnOf(clip):null;degrees=w==null?0:Sample(w,0,c);return w!=null;}
        /// <summary>Every clip the two classes play beyond their sets' own (the bake's controller carries them).</summary>
        public static readonly string[] KatanaExtra={KatanaRush,KatanaIai,KatanaSheathe,KatanaStorm,KatanaCritical,KatanaCriticalHit,KatanaDraw,FangsTakedownHit,FangsCriticalHit,K+"Unequip_Idle",K+"S2_Skill02"};
        public static readonly string[] FangsExtra={FangsPounce,FangsStep,FangsVeil,FangsFan,FangsBloom,FangsTakedown,FangsTakedownHit,FangsCritical,FangsCriticalHit,KatanaCriticalHit,"Frank_Stealth_Kill_Sneak_Idle",A+"Evade_F_IP",A+"Combo05_All_IP"};
    }
}
