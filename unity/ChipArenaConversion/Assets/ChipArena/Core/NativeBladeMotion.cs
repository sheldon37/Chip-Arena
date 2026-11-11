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
        private const string K="Frank_RPG_Katana_",A="Frank_RPG_Assassin_",R="Frank_Rogue_";
        // ---- the Katana's clips ----
        public static readonly string[] KatanaCuts={K+"S1_Combo01_01",K+"S1_Combo01_02",K+"S1_Combo01_03",K+"S1_Combo03_03"};
        public const string KatanaRush=K+"S3_Combo01_02",KatanaIai=K+"S3_Skill01",KatanaSheathe=K+"Unequip",KatanaStorm=K+"S3_Skill02",KatanaIdle=K+"Stance1_Idle",
            KatanaGuard=K+"Guard",KatanaBlock=K+"Block",KatanaTumble=K+"Tumbling_F",KatanaDraw=K+"Stance1_Equip";
        /// <summary>The counter's paired critical: the wielder's clip and its victim's.</summary>
        public const string KatanaCritical="Frank_Damage@Damage_Critical_Katana",KatanaCriticalHit="Frank_Damage@Damage_Critical_Katana_Hit";
        // ---- the Fangs' clips ----
        // The flurry is the Frank Rogue (dual blade) set's third combination chain (a leaping cut, a lunging double, a step cut), its
        // fourth chain's double and the third's low spinning finisher (owner 2026-11-11: "The Fangs need a MUCH better basic combo,
        // utilize the Rogue"); the dash its fifth chain's long low lunge with both blades out.
        public static readonly string[] FangsCuts={R+"Combination_03_1",R+"Combination_03_2",R+"Combination_03_3",R+"Combination_04_2",R+"Combination_03_4"};
        public const string FangsPounce=R+"Combination_05_2",FangsStep=A+"Skill01_IP",FangsVeil=A+"Evade_B_IP",FangsFan=A+"Attack06_IP",FangsBloom=A+"Skill02_IP",FangsIdle=A+"Equip_Idle",
            FangsGuard=A+"Guard",FangsBlock=A+"Block",FangsTumble=A+"Tumbling_F";
        /// <summary>The takedown from behind and the frontal critical: the wielder's clips and their victims'.</summary>
        public const string FangsTakedown="Frank_Stealth_Kill_Behind_05",FangsTakedownHit="Frank_Stealth_Kill_Hit_Behind_05",FangsCritical="Frank_Damage@Damage_Critical_Assassin",FangsCriticalHit="Frank_Damage@Damage_Critical_Assassin_Hit";
        /// <summary>A paired attacker clip's victim clip (null: none).</summary>
        public static string VictimClip(string clip)=>clip==KatanaCritical?KatanaCriticalHit:clip==FangsTakedown?FangsTakedownHit:clip==FangsCritical?FangsCriticalHit:null;

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
                case R+"Combination_03_1":return new Window(.17f,.45f,.9f);
                case R+"Combination_03_2":return new Window(0,.3f,.75f);
                case R+"Combination_03_3":return new Window(0,.317f,.75f);
                case R+"Combination_04_2":return new Window(0,.317f,.8f);
                case R+"Combination_03_4":return new Window(.3f,.7f,1.35f);
                case FangsPounce:return new Window(.95f,1.28f,1.85f);
                case FangsStep:return new Window(.45f,.7f,1.15f);
                case FangsVeil:return new Window(0,.2f,.58f);
                case FangsFan:return new Window(0,.35f,.8f);
                case FangsBloom:return new Window(.45f,.9f,1.5f);
                case FangsTakedown:case FangsTakedownHit:return new Window(0,.9f,1.45f);
                case FangsCritical:case FangsCriticalHit:return new Window(.5f,1.1f,1.7f);
                default:return new Window(float.NaN,float.NaN,float.NaN);
            }
        }
        /// <summary>At most this many times a clip's pace, before its strike (a move too quick starts later into the window).</summary>
        public const float StrikeRate=1.6f;
        /// <summary>The Rogue's dual-blade clips are slower than the daggers' quick cuts: they may run twice their pace before the strike.</summary>
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
            pick=new NativeVisualMotion.Pick{Clip=clip,Time=t,Fade=NativeVisualMotion.FadeIn,Flight=flight};return true;
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
        /// <summary>The clip a blade move plays at `time` and its time; `whole`: it takes the whole body even on the move; `flight`: its own
        /// height is kept (the pounce's leap).</summary>
        public static bool Clip(Actor a,NativeBladeKit k,float time,NativeVisualMotion.IClips clips,out string clip,out float t,out bool whole,out bool flight)
        {
            clip=null;t=0;whole=true;flight=false;float at=k.CastAt,rel=Math.Max(at+.01f,k.CastRelease),until=Math.Max(rel+.05f,k.CastUntil);
            if(a.SourceSpec==NativeKatana.Spec)
            {
                if(time>=until+.12f)return false;
                switch(k.CastKind)
                {
                    case NativeKatana.Cut:whole=k.CastStep>=2;return On(KatanaCuts[Math.Max(0,k.CastStep)%KatanaCuts.Length],at,rel,until,time,clips,out clip,out t);
                    case NativeKatana.Rush:return On(KatanaRush,at,rel+NativeKatana.RushTime*.5f,until,time,clips,out clip,out t);
                    case NativeKatana.Iai:
                    {
                        // The draw stance and the dash cut, then the blade slid home with its click on the split.
                        float dashEnd=rel+NativeKatana.IaiDash;if(time<dashEnd+.06f||k.SplitAt<-50)return On(KatanaIai,at,rel+NativeKatana.IaiDash*.5f,dashEnd+.06f,time,clips,out clip,out t);
                        return On(KatanaSheathe,dashEnd+.06f,k.SplitAt,Math.Max(k.SplitAt+.05f,until),time,clips,out clip,out t);
                    }
                    case NativeKatana.Storm:
                    {
                        // The flurry's span stretched across the cuts (Thousand Petals: more of them).
                        float span=until-rel-NativeKatana.StormRecover;return On(KatanaStorm,at,rel,rel+span+NativeKatana.StormRecover,time,clips,out clip,out t);
                    }
                    case NativeKatana.Counter:return On(KatanaCritical,at,k.PairHit>-50?k.PairHit:rel,until,time,clips,out clip,out t);
                }
                return false;
            }
            if(a.SourceSpec==NativeFangs.Spec)
            {
                if(time>=until+.1f)return false;
                switch(k.CastKind)
                {
                    case NativeFangs.Cut:whole=k.CastStep==NativeFangs.Steps-1;return On(FangsCuts[Math.Max(0,k.CastStep)%FangsCuts.Length],at,rel,until,time,clips,out clip,out t);
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
            float span=Math.Max(.05f,a.PairUntil-a.PairAt),u=Clamp01((time-a.PairAt)/span);
            pick=new NativeVisualMotion.Pick{Clip=a.PairClip,Time=from+(to-from)*u,Fade=NativeVisualMotion.FadeIn};return true;
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        /// <summary>Every clip the two classes play beyond their sets' own (the bake's controller carries them).</summary>
        public static readonly string[] KatanaExtra={KatanaRush,KatanaIai,KatanaSheathe,KatanaStorm,KatanaCritical,KatanaCriticalHit,KatanaDraw,FangsTakedownHit,FangsCriticalHit,K+"Unequip_Idle",K+"S2_Skill02"};
        public static readonly string[] FangsExtra={FangsPounce,FangsStep,FangsVeil,FangsFan,FangsBloom,FangsTakedown,FangsTakedownHit,FangsCritical,FangsCriticalHit,KatanaCriticalHit,"Frank_Stealth_Kill_Sneak_Idle",A+"Evade_F_IP",A+"Combo05_All_IP"};
    }
}
