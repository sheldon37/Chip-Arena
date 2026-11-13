using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The blade classes' kill cuts (owner 2026-11-12: "I also want all bladed weapons to support gibbing (Katanas, Glaive, etc.)").
    /// A lethal blow by the Katana (the DEV Katana plays its rules), the Fangs or the Glaive takes the body apart as the standard blades'
    /// do: NativeMeleeSlice plans it, NativeMeleeBisect cuts the pilot's mesh, a zombie's parts fly as its model's own. All from
    /// replicated data: the Hit event's damage kind names the move ("blade-" or "glaive-" and the move's kind, which the arena's Damage
    /// puts in the event's detail; a "direct" hit is the plunging slam, the standard air attack every blade class plays), the attacker's replicated
    /// kit its combo step (and the Glaive's ring in its flick), and the clip the move played (NativeBladeMotion.Clip, the DEV Katana's
    /// own included) the cut's plane.
    /// <list type="bullet">
    /// <item>Through the body: the Katana's big swings (its third and fourth cuts), the rush, the iai's split along its line, the storm,
    /// the counter and the Crescent Wave; the Glaive's whirl and skim; the Katana's and the Glaive's plunging slam (top-down).</item>
    /// <item>A part: the Katana's and the Glaive's first two cuts (the head where the cut crosses the neck or above, else an arm),
    /// the Glaive's flick and thrown ring (its throw, ricochets and return: the head or an arm, by the death's seed), its halo, its
    /// burst and Shatterpoint's shards (an arm).</item>
    /// <item>The Fangs as the daggers: a slit throat on the combo's first four cuts; an arm (a forearm when the cut is low) on its
    /// finisher, the pounce, the shadow step's stab, the takedown, the critical and a thrown knife; the slam's chop takes an arm. The
    /// bloom (a venom burst) cuts nothing.</item>
    /// </list>
    /// The cut's plane is a slash across the wielder's front: the blade's travel in its frontal plane (an angle) through the height
    /// where it crosses the victim's centre line, the plane holding the line from the wielder to the victim (a slash through what is
    /// in front of it). The Katana's own clips (the Frank Slash Pack) are measured on the pack's blade at each strike (Blender: the
    /// tip's travel, flattened onto the front, through the blade's sweet spot 70% of the way to its tip); the DEV Katana's (Grruzam's
    /// files are ASCII FBX, which Blender cannot read), the Fangs' and the Glaive's hand-keyed moves are written from their clips'
    /// descriptions. A move that flies (the iai's line, the wave, the thrown ring, a knife) cuts level along its flight; a dash level
    /// along the dash; the whirl and the halo level around the wielder.
    /// Pure, deterministic, presentation only: nothing here reaches the rules.
    /// </summary>
    public static class NativeBladeCuts
    {
        public static bool Is(string spec)=>spec==NativeKatana.Spec||spec==NativeFangs.Spec||spec==NativeGlaive.Spec;

        public enum Move{None,Cut,Rush,Iai,Storm,Counter,Wave,Slam,Pounce,Stab,Takedown,Knife,Whirl,Flick,Skim,Throw,Halo,Burst,Shard}
        // The Glaive's hits carry "glaive-" and its move's id (GlaiveHurt), the Katana's and the Fangs' "blade-" and the hit's kind (BladeHurt).
        private const string B="blade-",G="glaive-";
        /// <summary>The move a blade class's Hit event names (its damage kind; `air`: the swing's replicated jump flag, for the slam).
        /// None: not a blade class, a hit that cuts nothing (the Fangs' venom bloom, a tick), or a kind this does not know.</summary>
        public static Move MoveOf(string spec,string detail,bool air)
        {
            if(!Is(spec)||string.IsNullOrEmpty(detail))return Move.None;
            if(detail=="direct")return air?Move.Slam:Move.None;
            if(spec==NativeGlaive.Spec)
            {
                if(detail==G+NativeGlaive.Swing)return Move.Cut;
                if(detail==G+NativeGlaive.Skim)return Move.Skim;
                if(detail==G+NativeGlaive.Throw)return Move.Throw;
                if(detail==G+NativeGlaive.Halo)return Move.Halo;
                if(detail==G+NativeGlaive.Burst)return Move.Burst;
                if(detail==G+NativeGlaive.HitShard)return Move.Shard;
                return Move.None;
            }
            if(!detail.StartsWith(B,StringComparison.Ordinal))return Move.None;
            switch(detail.Substring(B.Length))
            {
                case "cut":return Move.Cut;
                case "rush":return spec==NativeKatana.Spec?Move.Rush:Move.None;
                case "iai":case "sever":return spec==NativeKatana.Spec?Move.Iai:Move.None;
                case "storm":return spec==NativeKatana.Spec?Move.Storm:Move.None;
                case "counter":return spec==NativeKatana.Spec?Move.Counter:Move.None;
                case "wave":return spec==NativeKatana.Spec?Move.Wave:Move.None;
                case "pounce":return spec==NativeFangs.Spec?Move.Pounce:Move.None;
                case "step":return spec==NativeFangs.Spec?Move.Stab:Move.None;
                case "takedown":case "critical":return spec==NativeFangs.Spec?Move.Takedown:Move.None;
                case "knife":return spec==NativeFangs.Spec?Move.Knife:Move.None;
                default:return Move.None;
            }
        }
        /// <summary>A recorded blow's move: by its kind, then the combo step (the Glaive's third cut is the whirl, its fourth the flick).
        /// No blow seen (a late join): the class's light cut.</summary>
        public static Move MoveOf(in MeleeBlow blow,bool hasBlow)
        {
            if(!hasBlow)return Move.Cut;var m=MoveOf(blow.Spec,blow.Detail,blow.Air);
            if(m==Move.Cut&&blow.Spec==NativeGlaive.Spec){int step=Math.Max(1,blow.Index)-1;if(step==2)return Move.Whirl;if(step==3)return Move.Flick;}
            return m;
        }

        // ---- the slashes ----
        /// <summary>How a cut's plane runs: a slash across the front (Swing), level along a dash, a flight or round the wielder, or the
        /// top-down chop (NativeMeleeSlice's own).</summary>
        public enum Sweep{Swing,Dash,Line,Orbit,Chop}
        /// <summary>A cut: its sweep; for a Swing the blade's travel across the wielder's front (`Angle`, degrees in its frontal plane:
        /// 0 level toward its left, 90 straight down, 180 level toward its right, -90 straight up); where the plane crosses the victim's
        /// centre line (`Height`, m above its feet).</summary>
        public readonly struct Slash
        {
            public readonly Sweep Sweep;public readonly float Angle,Height;
            public Slash(Sweep sweep,float angle,float height){Sweep=sweep;Angle=angle;Height=height;}
            /// <summary>The travel across the front, source frame (+x the wielder's left, +y up).</summary>
            public Vector2 Across=>new Vector2((float)Math.Cos(Angle*Math.PI/180),-(float)Math.Sin(Angle*Math.PI/180));
            /// <summary>The same slash from the other side.</summary>
            public Slash Mirrored=>new Slash(Sweep,180-Angle,Height);
        }
        private static Slash S(float angle,float height)=>new Slash(Sweep.Swing,angle,height);
        /// <summary>The DEV Katana's counter clip (Grruzam's katana set: NativeDevKatanaMotion.Counter).</summary>
        public const string DevCounter=NativeDevKatanaMotion.Counter;
        private static Slash Level(Sweep sweep,float height)=>new Slash(sweep,0,height);
        private static readonly Dictionary<string,Slash> clips=Clips();
        private static Dictionary<string,Slash> Clips()
        {
            var d=new Dictionary<string,Slash>(StringComparer.Ordinal);void Add(string clip,Slash s){if(!string.IsNullOrEmpty(clip))d[clip]=s;}
            // The DEV Katana's (Grruzam's katana set), written from its clips' descriptions (NativeDevKatanaMotion): its first four-hit
            // chain (a diagonal cut down across the front, a rising backhand, a low spinning cut, the finisher's high cut) by name, so a
            // later pick takes its move's cut until its own is surveyed; the storm's low sweeps end in a rising cut.
            const string Dk="M_katana_Blade@";
            Add(Dk+"Attack_4Combo_1_Inplace",S(35,1.15f));Add(Dk+"Attack_4Combo_2_Inplace",S(-145,1.02f));Add(Dk+"Attack_4Combo_3_Inplace",S(0,.92f));Add(Dk+"Attack_4Combo_4_Inplace",S(-170,1.25f));
            Add(NativeDevKatanaMotion.Storm,S(-155,1f));
            // Its counter (the DEV classes' pick, 2026-11-12, in place of the Frank critical; surveyed by the DEV classes agent at 200 fps):
            // the blow in front comes down from the wielder's high left to its low right, 33-34 degrees below level, at 0.40-0.43 s,
            // crossing the centre line 1.31-1.39 m high (the rising stroke before it passes behind the body).
            Add(DevCounter,S(147,1.35f));
            // The Katana's (the Frank Slash Pack), measured on the pack's blade at each strike: a level forehand at the chest (toward the
            // left), a rising backhand that strikes low (the forearm), a cut down from the right shoulder, the finisher rising from the
            // left hip; the storm's strike rises across the waist; the counter's critical cuts down across the waist.
            var cuts=NativeBladeMotion.KatanaCuts;
            Add(cuts[0],S(6,1.16f));Add(cuts[1],S(-143,.59f));Add(cuts[2],S(28,1.05f));Add(cuts[3],S(-152,.84f));
            Add(NativeBladeMotion.KatanaStorm,S(-159,1.01f));Add(NativeBladeMotion.KatanaCritical,S(32,.94f));
            return d;
        }
        /// <summary>Whether this clip's cut was measured or written for it (the rest take their move's).</summary>
        public static bool Known(string clip)=>clip!=null&&clips.ContainsKey(clip);
        /// <summary>The cut of a blow by `spec` (no blow seen: a default one): its clip's, else its move's. `mirror` (the death's seed)
        /// turns the storm's slash the other way round (its cuts go both ways).</summary>
        public static Slash SlashOf(in MeleeBlow blow,string spec,Move move,bool mirror)
        {
            int step=Math.Max(1,blow.Index)-1;
            if(move==Move.Cut||move==Move.Storm||move==Move.Counter)
                if(blow.Clip!=null&&clips.TryGetValue(blow.Clip,out var known))return move==Move.Storm&&mirror?known.Mirrored:known;
            switch(move)
            {
                case Move.Cut:
                    if(spec==NativeGlaive.Spec)return step==1?S(-150,1.05f):S(0,1.2f);// the forehand from the right to the left; the rising backhand
                    if(spec==NativeFangs.Spec)return step>=NativeFangs.Steps-1?S(0,.95f):S(0,1.3f);// the low spinning finisher
                    return step==0?S(10,1.15f):step==1?S(-145,.95f):step==2?S(25,1.05f):S(-155,.9f);
                case Move.Storm:{var s=S(-159,1.01f);return mirror?s.Mirrored:s;}
                case Move.Counter:return S(32,.94f);
                case Move.Rush:case Move.Pounce:return Level(Sweep.Dash,1f);
                case Move.Skim:return Level(Sweep.Dash,.95f);
                case Move.Iai:return Level(Sweep.Line,1f);
                case Move.Wave:case Move.Knife:case Move.Burst:case Move.Shard:return Level(Sweep.Line,1.1f);
                case Move.Throw:case Move.Flick:return Level(Sweep.Line,1.2f);// the ring flies at the hand's height
                case Move.Whirl:return Level(Sweep.Orbit,1.15f);
                case Move.Halo:return Level(Sweep.Orbit,1.2f);
                case Move.Stab:return S(60,1.15f);
                case Move.Takedown:return S(20,1.2f);
                case Move.Slam:return new Slash(Sweep.Chop,90,1.25f);
                default:return S(0,1.1f);
            }
        }

        // ---- what the cut takes ----
        public enum Take{Part,Through,Slit,Limb,Arm}
        /// <summary>What a move's kill takes: a part (the head or an arm by the cut's height), through the body, a slit throat or a limb (the
        /// Fangs' as the daggers'), an arm alone (the Glaive's halo, burst and shards). The slam chops, top-down.</summary>
        public static Take TakeOf(string spec,Move move,int index)
        {
            int step=Math.Max(1,index)-1;
            if(spec==NativeFangs.Spec)return move==Move.Cut&&step<NativeFangs.Steps-1?Take.Slit:Take.Limb;
            switch(move)
            {
                case Move.Cut:return spec==NativeKatana.Spec&&step>=2?Take.Through:Take.Part;
                case Move.Rush:case Move.Iai:case Move.Storm:case Move.Counter:case Move.Wave:case Move.Whirl:case Move.Skim:case Move.Slam:return Take.Through;
                case Move.Halo:case Move.Burst:case Move.Shard:return Take.Arm;
                default:return Take.Part;
            }
        }
        /// <summary>A part cut takes the head when the plane crosses at the neck or above (m above the feet, lower than a sword's: the cut
        /// itself falls on the neck, NativeMeleeBisect), else an arm (a forearm below this).</summary>
        public const float HeadAt=1.1f,ArmAt=.92f;
        /// <summary>A cut through the body crosses the trunk, between the hips and the shoulders (m above the feet), never the legs.</summary>
        public const float TrunkLow=.85f,TrunkHigh=1.3f;

        // ---- the record ----
        /// <summary>Every clip, for NativeBladeMotion.Clip's choice of the clip a move plays (the name only; the bank's own copy is the
        /// presentation's).</summary>
        private sealed class Every:NativeVisualMotion.IClips{public bool Has(string clip)=>!string.IsNullOrEmpty(clip);public float Length(string clip)=>4;public float Hit(string clip)=>2;}
        private static readonly Every every=new Every();
        /// <summary>Whether a Hit event is a blade class's blow that cuts (by `attacker`, the frame's actor).</summary>
        public static bool Cuts(in ArenaEvent e,Actor attacker)=>attacker!=null&&e.Source>0&&e.Amount>0&&MoveOf(attacker.SourceSpec,e.Detail,attacker.AirAttack)!=Move.None;
        /// <summary>The blow a blade class's Hit event records, as the hit ledger reads a standard blade's (NativeHitLedger.MeleeHit): the
        /// attacker's frame (its feet, yaw, the swing's air flag), the event's direction (attacker to victim; the iai's and the wave's
        /// line, the counter's facing, the ring's flight) and, from the attacker's kit at that tick, the move, its combo step and the clip
        /// it played (the Glaive's ring in its flick is the combo's fourth step).</summary>
        public static MeleeBlow Blow(in ArenaEvent e,Actor a)
        {
            float aim=e.Normal.x*e.Normal.x+e.Normal.y*e.Normal.y+e.Normal.z*e.Normal.z;string move=null,clip=null;int step=0;
            if(a.SourceSpec==NativeGlaive.Spec&&a.Glaive!=null){move=a.Glaive.CastKind;step=a.Glaive.Disc==NativeGlaive.Flick?3:a.Glaive.CastStep;}
            else if(a.Blade!=null){move=a.Blade.CastKind;step=a.Blade.CastStep;if(NativeBladeMotion.Clip(a,a.Blade,e.Time,every,out var c,out _,out _,out _))clip=c;}
            return new MeleeBlow{At=e.Time,Source=e.Source,Spec=a.SourceSpec,CommitYaw=a.CommitYaw,Index=Math.Max(0,step)+1,SwingAt=a.SwingAt,SwingUntil=a.SwingUntil,Air=a.AirAttack,
                From=a.Position,Yaw=a.Yaw,Detail=e.Detail,Amount=e.Amount,Toward=e.Normal,Aimed=aim>.81f&&aim<1.21f,Struck=e.From,Move=move,Clip=clip};
        }
        /// <summary>The blade's pass through the body: how long the cut holds (s) and how fast the blade goes (m/s).</summary>
        public static float Hitch(string spec)=>spec==NativeFangs.Spec?.045f:.05f;
        public static float Speed(string spec)=>spec==NativeKatana.Spec?11:10;
    }
}
