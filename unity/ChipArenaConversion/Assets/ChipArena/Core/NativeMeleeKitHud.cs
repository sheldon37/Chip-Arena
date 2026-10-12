using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Q / E cooldown cards of a standard melee pilot (FightHud.Cooldowns, AbilityStatus) from the replicated kit and the
    /// slot's ready time: the recast window (Rising Dragon's .9 s after a launch, Kunai Mark's 4 s mark) as its own counting
    /// state with a draining dial, the cast while it runs, the cooldown with a dial over the cooldown actually started (the
    /// Flash Step refund, Rising Dragon's 7 s no-recast cooldown and the Kunai miss's 4 s all shorten what is left, so the
    /// content cooldown alone misreads them), and stamina gating once ready. Presentation only: the rules are the simulation's.
    /// </summary>
    public static class NativeMeleeKitHud
    {
        public struct Card{public string State;public bool Ready,Active,Recast,Low;public float Dial;}
        /// <summary>The recast window's length for an E ability (Rising Dragon .9 s, Kunai Mark the 4 s mark).</summary>
        public static float RecastWindow(string id)=>id=="rising-dragon"?.9f:id=="kunai-mark"?4:1;
        /// <summary>One card. remaining: the slot's ready time less the clock; span: the cooldown it counts down
        /// (<see cref="Span"/>); cost: the ability's stamina cost at the pilot's stats.</summary>
        public static Card Of(string slot,string id,NativeMeleeKit k,float remaining,float span,float stamina,float cost,float time)
        {
            var c=new Card();
            if(slot=="e"&&k!=null&&k.RecastUntil>time&&k.RecastKind==id&&k.Target!=0)
            {
                // Recasts cost no stamina: the window is the state, its dial draining as it closes.
                float left=k.RecastUntil-time;c.State="RECAST "+left.ToString("0.0")+"s";c.Recast=c.Active=true;c.Dial=MathUtil.Clamp(left/RecastWindow(id),0,1);return c;
            }
            if(k!=null&&k.Kind==id&&time<k.Until){c.State="ACTIVE";c.Active=true;c.Dial=remaining>0&&span>0?1-MathUtil.Clamp(remaining/span,0,1):1;return c;}
            if(remaining>0){c.State=remaining.ToString("0.0")+"s";c.Dial=span>0?1-MathUtil.Clamp(remaining/span,0,1):0;c.Low=stamina<cost;return c;}
            if(stamina<cost){c.State="LOW STAMINA";c.Low=true;c.Dial=1;return c;}
            c.State="READY";c.Ready=true;c.Dial=1;return c;
        }
        /// <summary>
        /// The cooldown a card's dial counts down: the remaining time when it rose (a new cooldown started), kept while it falls
        /// (a refund or a shortened cooldown moves the dial on), forgotten once ready. fallback: the content cooldown.
        /// </summary>
        public sealed class Span
        {
            private float last=-1,span;
            public float Update(float remaining,float fallback)
            {
                if(!(remaining>0)){last=-1;span=0;return fallback;}
                if(last<0||remaining>last+.05f)span=Math.Max(remaining,.01f);last=remaining;return span>0?span:fallback;
            }
        }
        /// <summary>The shield's RMB card while its shield is away (Aegis Ricochet): block is unavailable until it returns.</summary>
        public static bool ShieldAway(string spec,NativeMeleeKit k)=>spec=="shield"&&k!=null&&k.ShieldOut;
        /// <summary>The Kunai mark on `me`: its owner and seconds left (every pilot's replicated kit), else 0.</summary>
        public static float MarkedFor(Actor[] actors,int me,float time,out int owner)
        {
            owner=0;float left=0;if(actors==null||me==0)return 0;
            foreach(var a in actors){float l=MarkLeft(a?.MeleeKit,me,time);if(l>left){left=l;owner=a.Id;}}
            return left;
        }
        /// <summary>Seconds left on a kit's mark of pilot `me` (0: none).</summary>
        public static float MarkLeft(NativeMeleeKit k,int me,float time)=>k!=null&&me!=0&&k.MarkTarget==me&&k.MarkUntil>time?k.MarkUntil-time:0;
    }
}
