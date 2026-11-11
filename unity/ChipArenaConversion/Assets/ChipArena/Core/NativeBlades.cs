using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The two blade classes of 2026-11-10 (owner: "Make very high quality designed katana / dev dagger classes; Both with unique chips with
    /// their abilities ... Use the new animation pack for both classes and use really cool effects"): the Katana (public, Core NativeKatana)
    /// and the Fangs (DEV, Core NativeFangs; docs/KATANA.md, docs/FANGS.md). They share one replicated record (NativeBladeKit on
    /// Actor.Blade), its hits, the paired takedowns (an attacker's clip with its victim held in the matching clip) and these helpers. Their
    /// moves play the Frank Slash Pack's clips on the model (Core NativeBladeMotion). Pure.
    /// </summary>
    public static class NativeBlades
    {
        public static bool Is(string spec)=>spec==NativeKatana.Spec||spec==NativeFangs.Spec;
        public const int MaxHits=48,MaxSlots=5;
        /// <summary>The slots each class has: the Katana Q and E; the Fangs Q, E and 1-3.</summary>
        public static int Slots(string spec)=>spec==NativeFangs.Spec?NativeFangs.Slots:spec==NativeKatana.Spec?NativeKatana.Slots:0;
        public static Vector3 N(V3 v)=>new Vector3(v.x,v.y,v.z);
        /// <summary>A flat unit direction from the yaw (degrees).</summary>
        public static Vector3 Facing(float yaw){float r=yaw*(float)Math.PI/180;return new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));}
        /// <summary>A move's travel at `time` along From to To between `at` and `until` (eased out); false outside it.</summary>
        public static bool PathAt(NativeBladeKit k,float time,out Vector3 at,out float u)
        {
            at=default;u=0;if(k==null||k.PathAt<-50||time<k.PathAt||time>k.PathUntil+1e-3f)return false;
            u=Clamp01((time-k.PathAt)/Math.Max(1e-3f,k.PathUntil-k.PathAt));float e=1-(1-u)*(1-u);at=Vector3.Lerp(N(k.From),N(k.To),e);return true;
        }
        public static float Clamp01(float v)=>v<0?0:v>1?1:v;
        /// <summary>Whether a hit kind is one of either class's (the record's Valid).</summary>
        public static bool IsHit(string k)=>NativeKatana.IsHit(k)||NativeFangs.IsHit(k);
        /// <summary>Whether a cast kind is one of either class's.</summary>
        public static bool IsCast(string k)=>k==null||NativeKatana.IsCast(k)||NativeFangs.IsCast(k);
    }

    /// <summary>One moment of contact for the effects and the sound (the last 1.5 s): `Kind` one of the class's hit kinds, where, which way,
    /// who, and an amount the effects read (a venom stack count, a combo step).</summary>
    [Serializable] public sealed class NativeBladeHit
    {
        public int Id,Victim,Amount;public string Kind;public float At;public V3 Pos,Dir;
    }

    /// <summary>
    /// The blade classes' replicated kit (docs/KATANA.md, docs/FANGS.md "The record"): everything the pose, the effects, the sound and the HUD
    /// read. The authority (Runtime NativeBladeAuthority.cs and each class's) writes it; copied in snapshots, late join and the killcam
    /// (`Actor.Blade`), checked with Valid.
    /// </summary>
    [Serializable] public sealed class NativeBladeKit
    {
        /// <summary>What the pilot is doing, for the pose: the move's id and its times (CastRelease: its hit), the combo's step and when the
        /// combo lapses.</summary>
        public string CastKind;public float CastAt=-100,CastRelease,CastUntil,ComboUntil;public int CastStep,Combo;
        /// <summary>Absolute ready times by slot (the Katana Q, E; the Fangs Q, E, 1, 2, 3).</summary>
        public float[] Ready;
        /// <summary>A move's travel (the rush, the iai's dash, the pounce, the shadow step): from From to To between PathAt and PathUntil.</summary>
        public V3 From,To;public float PathAt=-100,PathUntil=-100;
        /// <summary>The Katana's iai: the line it cut (LineFrom to LineTo, drawn at LineAt) and when it splits (SplitAt).</summary>
        public V3 LineFrom,LineTo;public float LineAt=-100,SplitAt=-100;
        /// <summary>The Katana's counter window (a block or parry landed; the next cut is the counter until then).</summary>
        public float CounterUntil=-100;
        /// <summary>The Fangs' veil: unseen by enemies from VeilAt until VeilUntil (an attack ends it).</summary>
        public float VeilAt=-100,VeilUntil=-100;
        /// <summary>A paired move (the Katana's counter, the Fangs' assassination): the victim (seat or minion id), the attacker's clip and its
        /// times (PairHit: the blow).</summary>
        public int Pair;public string PairClip;public float PairAt=-100,PairHit=-100,PairUntil=-100;
        /// <summary>The chips' share of what is seen (each class's chips: a bit per chip held, NativeKatanaChips/NativeFangsChips.Bit), and a
        /// chip moment's clock (the class says which).</summary>
        public int Chips;public float ChipAt=-100;
        public NativeBladeHit[] Hits;
        public void CapturePresence(){}
        public void RestorePresence(){if(string.IsNullOrEmpty(CastKind))CastKind=null;if(string.IsNullOrEmpty(PairClip))PairClip=null;}
        public NativeBladeKit ViewCopy()
        {
            var c=(NativeBladeKit)MemberwiseClone();c.Ready=Ready==null?null:(float[])Ready.Clone();
            if(Hits!=null){c.Hits=new NativeBladeHit[Hits.Length];for(int i=0;i<Hits.Length;i++){var h=Hits[i];c.Hits[i]=h==null?null:new NativeBladeHit{Id=h.Id,Victim=h.Victim,Amount=h.Amount,Kind=h.Kind,At=h.At,Pos=h.Pos,Dir=h.Dir};}}
            return c;
        }
        public bool Valid()
        {
            bool F(float v)=>MathUtil.Finite(v)&&Math.Abs(v)<10000000;bool V(V3 v)=>F(v.x)&&F(v.y)&&F(v.z)&&v.Length<100000;bool K(string s)=>s==null||s.Length<=48;
            if(!K(CastKind)||!NativeBlades.IsCast(CastKind)||!F(CastAt)||!F(CastRelease)||!F(CastUntil)||!F(ComboUntil)||CastStep<0||CastStep>8||Combo<0||Combo>8)return false;
            if(!V(From)||!V(To)||!F(PathAt)||!F(PathUntil)||!V(LineFrom)||!V(LineTo)||!F(LineAt)||!F(SplitAt)||!F(CounterUntil)||!F(VeilAt)||!F(VeilUntil))return false;
            if(!K(PairClip)||!F(PairAt)||!F(PairHit)||!F(PairUntil)||!F(ChipAt))return false;
            if(Ready!=null){if(Ready.Length>NativeBlades.MaxSlots)return false;foreach(var r in Ready)if(!F(r))return false;}
            if(Hits!=null){if(Hits.Length>NativeBlades.MaxHits)return false;foreach(var h in Hits)if(h==null||!NativeBlades.IsHit(h.Kind)||!F(h.At)||!V(h.Pos)||!V(h.Dir)||h.Amount<0||h.Amount>99)return false;}
            return true;
        }
    }
    /// <summary>Where the presentation reads a pilot's blade kit: its snapshot actor's.</summary>
    public static class NativeBladeView
    {
        public static NativeBladeKit Of(Actor a)=>a!=null&&NativeBlades.Is(a.SourceSpec)?a.Blade:null;
        public static NativeBladeKit Katana(Actor a)=>a?.SourceSpec==NativeKatana.Spec?a.Blade:null;
        public static NativeBladeKit Fangs(Actor a)=>a?.SourceSpec==NativeFangs.Spec?a.Blade:null;
        /// <summary>Whether a Fangs pilot is veiled (unseen by enemies) at `time`.</summary>
        public static bool Veiled(Actor a,float time){var k=Fangs(a);return k!=null&&time>=k.VeilAt&&time<k.VeilUntil;}
        /// <summary>A blade class's blade in its weapon frame (its design's sockets: where the blade leaves the hilt, and its tip), for its
        /// trails (the owner, 2026-11-11: "Give both weapon weapon trail vfx"). The standard table (NativeWeaponDressing.BladeTip) keeps to
        /// the standard blades, whose sweeps and slash crescents read it. The left fang is its mirror (its hook the other way).</summary>
        public static bool Line(string spec,bool left,out Vector3 root,out Vector3 tip)
        {
            if(spec==NativeKatana.Spec&&!left){root=NativeWeaponModels.KatanaBladeBase;tip=NativeWeaponModels.KatanaBladeTip;return true;}
            if(spec==NativeFangs.Spec){root=NativeWeaponModels.FangBladeBase;tip=NativeWeaponModels.FangBladeTip;if(left)tip.X=-tip.X;return true;}
            root=tip=default;return false;
        }
    }
}
