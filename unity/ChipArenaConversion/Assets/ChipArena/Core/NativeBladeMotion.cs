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
        // then its first combo's low spinning sweep as the finisher. The set ships as root-motion takes only; the bake plays them in place.
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
                    // A cut that lunges (the third's step in, the finisher's lunge: NativeFangs.CutLunge) takes the whole body, its legs carrying
                    // the lunge (the rules' step read as moving, so the third played on the upper body over a walk); the others the upper body
                    // over the stride while moving.
                    case NativeFangs.Cut:whole=NativeFangs.CutLunge[Math.Max(0,k.CastStep)%NativeFangs.Steps]>0;return On(FangsCuts[Math.Max(0,k.CastStep)%FangsCuts.Length],at,rel,until,time,clips,out clip,out t);
                    case NativeFangs.Pounce:return On(FangsPounce,at,rel+NativeFangs.PounceTime*.5f,until,time,clips,out clip,out t);
                    case NativeFangs.Step:return On(FangsStep,at,rel+.04f,until,time,clips,out clip,out t);
                    case NativeFangs.Veil:return On(FangsVeil,at,rel,until,time,clips,out clip,out t);
                    case NativeFangs.Fan:whole=false;return On(FangsFan,at,rel,until,time,clips,out clip,out t);
                    case NativeFangs.Bloom:whole=false;return On(FangsBloom,at,rel,until,time,clips,out clip,out t);
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
        /// <summary>Every clip the two classes play beyond their sets' own (the bake's controller carries them).</summary>
        public static readonly string[] KatanaExtra={KatanaRush,KatanaIai,KatanaSheathe,KatanaStorm,KatanaCritical,KatanaCriticalHit,KatanaDraw,FangsTakedownHit,FangsCriticalHit,K+"Unequip_Idle",K+"S2_Skill02"};
        public static readonly string[] FangsExtra={FangsPounce,FangsStep,FangsVeil,FangsFan,FangsBloom,FangsTakedown,FangsTakedownHit,FangsCritical,FangsCriticalHit,KatanaCriticalHit,"Frank_Stealth_Kill_Sneak_Idle",A+"Evade_F_IP",A+"Combo05_All_IP"};
    }
}
