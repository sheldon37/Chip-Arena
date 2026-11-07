using System;

namespace ChipArena.UnityConversion
{
    /// <summary>How a phase moves from its start value to its end: in (accelerating into the end: a strike into contact), out
    /// (decelerating: a follow-through), in-out (slow at both ends: an anticipation), out-back (past the end and back), settle
    /// (from rest, over and back, landing still: a recovery with weight), hold (no move).</summary>
    public enum BossEase:byte{Linear,In,Out,InOut,OutBack,Settle,Hold}
    /// <summary>The parts of an attack phrase.</summary>
    public enum BossPhaseKind:byte{Anticipation,Strike,Contact,FollowThrough,Recover}

    /// <summary>One phase: its kind, its authored length (s), the progress it ends on (the rig's key coordinate: 0 rest, 1 wound,
    /// 2 struck ...), its ease and the ease's shape (0: the ease's default).</summary>
    public struct BossPhase
    {
        public BossPhaseKind Kind;public float Seconds,To,Shape;public BossEase Ease;
        public BossPhase(BossPhaseKind kind,float seconds,float to,BossEase ease,float shape=0){Kind=kind;Seconds=seconds;To=to;Ease=ease;Shape=shape;}
    }

    public static class BossEasing
    {
        /// <summary>The ease at t (0..1): 0 at 0, 1 at 1 (out-back and settle pass 1 on the way).</summary>
        public static float Eval(BossEase e,float t,float shape=0)
        {
            t=BossMath.Clamp01(t);
            switch(e)
            {
                case BossEase.In:return (float)Math.Pow(t,shape>0?shape:2.6f);
                case BossEase.Out:return 1-(float)Math.Pow(1-t,shape>0?shape:2.6f);
                case BossEase.InOut:return BossMath.Smoother(t);
                case BossEase.OutBack:{float s=shape>0?shape:1.7f,u=t-1;return 1+(s+1)*u*u*u+s*u*u;}
                case BossEase.Settle:
                {
                    // A damped wobble from rest (exp(-a t)(cos bt + a/b sin bt): zero slope at 0) windowed by smootherstep so it
                    // lands exactly on 1, still: 1 - (1 - S(t)) g(t). shape is the damping a (lower wobbles more).
                    float a=shape>0?shape:1.6f,b=1.6f*BossMath.Pi,g=(float)Math.Exp(-a*t)*((float)Math.Cos(b*t)+a/b*(float)Math.Sin(b*t));
                    return 1-(1-BossMath.Smoother(t))*g;
                }
                case BossEase.Hold:return 0;
                default:return t;
            }
        }
    }

    /// <summary>
    /// An attack phrase as phases (anticipation, strike, contact, follow-through, recover), each from the last one's progress to
    /// its own on an ease. The strike ends (the first contact phase starts) at <see cref="HitAt"/>: the authority's hit. Blend
    /// in and out over <see cref="BlendIn"/> and <see cref="BlendOut"/> (smootherstep weights) against the locomotion.
    /// </summary>
    public sealed class BossActionDef
    {
        public readonly string Name;public readonly BossPhase[] Phases;public readonly float BlendIn,BlendOut;
        /// <summary>The authored length, the authored hit (the first contact phase's start; the whole length with none), and that
        /// phase's index (-1: no contact, a roar or a shrug).</summary>
        public readonly float Seconds,HitAt;public readonly int ContactIndex;
        public BossActionDef(string name,float blendIn,float blendOut,params BossPhase[] phases)
        {
            Name=name;Phases=phases;BlendIn=Math.Max(1e-3f,blendIn);BlendOut=Math.Max(1e-3f,blendOut);ContactIndex=-1;float t=0,hit=-1;
            for(int i=0;i<phases.Length;i++){if(phases[i].Kind==BossPhaseKind.Contact&&ContactIndex<0){ContactIndex=i;hit=t;}t+=Math.Max(0,phases[i].Seconds);}
            Seconds=t;HitAt=hit<0?t:hit;
        }
        /// <summary>The progress the phrase ends on (the last phase's).</summary>
        public float End=>Phases.Length>0?Phases[Phases.Length-1].To:0;
    }

    /// <summary>An action at a moment: active, its age (s since the authority's start), length and hit (the authority's when given),
    /// progress (the rig's key coordinate), blend weight, the phase it is in and how far through it.</summary>
    public struct BossActionSample
    {
        public bool Active;public int Id;public float Age,Duration,HitAt,Progress,Weight,PhaseT;public int PhaseIndex;public BossPhaseKind Phase;
        public bool Struck=>Active&&Age>=HitAt;public float SinceHit=>Age-HitAt;
    }

    /// <summary>
    /// The timeline: a pure function of the age (time since the authority's start) and the authority's hit and length. The phases
    /// before contact stretch to the hit, those after to the rest of the length, so the hand lands on the authority's hit at any
    /// timing. Every peer (host, guests, a late joiner, the killcam) evaluating the same age gets the same sample, to the bit.
    /// </summary>
    public static class BossTimeline
    {
        public static BossActionSample Sample(BossActionDef def,float age,float hitAt=float.NaN,float duration=float.NaN)
        {
            var s=new BossActionSample{Age=age};if(def==null||def.Phases.Length==0)return s;
            float pre=def.ContactIndex<0?def.Seconds:def.HitAt,post=def.Seconds-pre;
            float hit=float.IsNaN(hitAt)?pre:Math.Max(0,hitAt),dur=float.IsNaN(duration)?hit+post:Math.Max(hit,duration);
            if(def.ContactIndex<0){hit=float.IsNaN(duration)?def.Seconds:dur;dur=hit;}
            float sPre=pre>1e-5f?hit/pre:1,sPost=post>1e-5f?(dur-hit)/post:1;
            s.Duration=dur;s.HitAt=hit;s.Active=age>=0&&age<dur;
            if(age<0){s.Progress=0;s.Weight=0;return s;}
            float start=0,from=0;int last=def.Phases.Length-1;
            for(int i=0;i<=last;i++)
            {
                var p=def.Phases[i];float len=Math.Max(0,p.Seconds)*(def.ContactIndex<0||i<def.ContactIndex?sPre:sPost);
                if(age<start+len||i==last)
                {
                    float t=len>1e-6f?BossMath.Clamp01((age-start)/len):1;s.PhaseIndex=i;s.Phase=p.Kind;s.PhaseT=t;
                    s.Progress=p.Ease==BossEase.Hold?from:from+(p.To-from)*BossEasing.Eval(p.Ease,t,p.Shape);break;
                }
                start+=len;from=p.To;
            }
            s.Weight=age>=dur?0:BossMath.Smoother(age/def.BlendIn)*(1-BossMath.Smoother((age-(dur-def.BlendOut))/def.BlendOut));
            return s;
        }
        /// <summary>A rig's key value at `progress` (keys at 0, 1, 2 ...: rest, wound, struck ...), piecewise linear and extended past
        /// both ends (a settle that overshoots rest carries on past it the way it came).</summary>
        public static float Key(float[] keys,float progress)
        {
            int n=keys.Length;if(n==0)return 0;if(n==1)return keys[0];
            int k=(int)Math.Floor(progress);if(k<0)k=0;if(k>n-2)k=n-2;return keys[k]+(keys[k+1]-keys[k])*(progress-k);
        }
    }

    /// <summary>
    /// The actions a boss is playing: the current one and the one it replaced, each a pure function of its replicated start (and
    /// hit, length, cancel time), so any peer reproduces the same pose at the same time. A new action's blend-in hands the old one
    /// out on the same clock (a crossfade, never a pop); a cancel (a stagger, a death) holds the pose and fades it out.
    /// <see cref="Play"/> is idempotent (call it every frame with the replicated record).
    /// </summary>
    public sealed class BossActionPlayer
    {
        private struct Slot{public BossActionDef Def;public int Id;public double Start,CancelAt;public float HitAt,Duration;}
        private Slot current,previous;
        public BossActionSample Current,Previous;
        public BossActionDef CurrentDef=>current.Def;public BossActionDef PreviousDef=>previous.Def;
        public void Clear(){current=previous=default;Current=Previous=default;}
        public void Play(BossActionDef def,int id,double start,float hitAt=float.NaN,float duration=float.NaN)
        {
            if(def==null)return;if(ReferenceEquals(current.Def,def)&&current.Id==id&&current.Start==start)return;
            previous=current;current=new Slot{Def=def,Id=id,Start=start,CancelAt=double.NaN,HitAt=hitAt,Duration=duration};
        }
        /// <summary>The current action stops at `at` (authority time): its pose holds there and fades over its blend-out.</summary>
        public void Cancel(double at){if(current.Def!=null&&double.IsNaN(current.CancelAt))current.CancelAt=at;}
        private static BossActionSample Of(in Slot s,double time)
        {
            if(s.Def==null)return default;float age=(float)(time-s.Start);bool cancelled=!double.IsNaN(s.CancelAt)&&time>s.CancelAt;
            var r=BossTimeline.Sample(s.Def,cancelled?(float)(s.CancelAt-s.Start):age,s.HitAt,s.Duration);r.Id=s.Id;r.Age=age;
            if(cancelled){r.Weight*=1-BossMath.Smoother((float)(time-s.CancelAt)/s.Def.BlendOut);r.Active=r.Weight>0;}
            return r;
        }
        /// <summary>Both actions at `time` (authority clock); the old one's weight already handed out by the new one's blend-in.</summary>
        public void Sample(double time)
        {
            Current=Of(current,time);Previous=Of(previous,time);
            if(current.Def!=null)Previous.Weight*=1-BossMath.Smoother((float)(time-current.Start)/current.Def.BlendIn);
            if(Previous.Weight<=0)Previous.Active=false;
        }
    }
}
