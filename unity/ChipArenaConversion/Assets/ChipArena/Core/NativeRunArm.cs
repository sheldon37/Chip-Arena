using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The free arm's run on the player's model (owner 2026-10-29: "For sprinting/running, I noticed the other arm not holding the
    /// weapon tends to extend outward. Its a bit flamboyant looking"). The model plays the animation pack's clips whole
    /// (NativeVisualMotion); the pack's character is built broad (its idle already holds the arms 21-29 degrees out), and its
    /// armed and staff runs fling the free arm up and out on each stride: measured on the clips, the shoulder-to-hand line 37-63
    /// degrees out of the body's middle plane (the staff's 21-57), the hand rising to the shoulder's height and 2.2-2.9 shoulder
    /// half-widths from the middle (the pack's own plain runs: 5-33 degrees, the hand 1.1-2.0 half-widths; the armed walk and idle
    /// 24-36). On the slim model that reads as a flung arm. While running or sprinting, the free arm is lowered in the chest's
    /// frontal plane (turned about the chest's forward axis, out toward down, the whole arm about the shoulder: its forward and
    /// back swing, its bend and its hand are the clip's) as far as brings its shoulder-to-hand line to a soft cap (HandFrom to
    /// HandCap) and its elbow to another (UpperFrom to UpperCap), never so far that the upper arm lies against the chest
    /// (UpperMin), and less as the arm points forward or back out of the frontal plane (FrontalFrom to FrontalFull: there the turn
    /// moves it little and its out share is small anyway). Measured after, on the same clips: the armed run's hand line 33-40
    /// degrees, its upper arm 16-31 (was 28-69), the hand under the shoulder line by 25 degrees or more and 2.1-2.3 half-widths
    /// out; the pack's plain runs, the walks and the idles move by 0-5 degrees; the largest change between two frames 4.9 degrees
    /// at 75 per second (the clips' own: tens). Presentation only.
    /// </summary>
    public static class NativeRunArm
    {
        public const float HandFrom=26, HandCap=34, UpperFrom=30, UpperCap=38, UpperMin=14, FrontalFrom=.6f, FrontalFull=.9f, MaxDrop=60;
        private const float Rad=(float)Math.PI/180;

        /// <summary>An angle (degrees) at most `from` kept; past it eased toward `cap` (never reaching it).</summary>
        public static float Soft(float a,float from,float cap)=>a<=from?a:cap-(cap-from)*(float)Math.Exp(-(a-from)/(cap-from));

        /// <summary>Which of a set's arms runs free (no weapon or handle in its fist): the off hand of a one-handed set (left; the
        /// daggers' second blade swings as a free arm), the right beside a staff or a bow in the left fist, both of a fighter's;
        /// none of a two-handed set (the greatsword, the long guns: the support fist rides the handle).</summary>
        public static bool Free(NativeVisualMotion.Set set,bool left)
        {
            if(set==null||set.Spec=="greatsword"||set.Spec=="rifle") return false;
            if(set.Fighter) return true;
            return set.LeftShaft||set.Pull!=null?!left:left;
        }

        /// <summary>The running share of the locomotion blend (NativeVisualStride's order): its runs and its sprint; the walks
        /// keep their carry.</summary>
        public static float RunShare(float[] weights)
        {
            if(weights==null) return 0;
            float sum=0;
            for(int i=0;i<NativeVisualStride.Directions&&i<weights.Length;i++) sum+=weights[i];
            if(NativeVisualStride.Sprint<weights.Length) sum+=weights[NativeVisualStride.Sprint];
            return Math.Max(0,Math.Min(1,sum));
        }

        /// <summary>How far (radians, at least 0) to lower an arm about the chest's forward axis, out toward down: the shoulder,
        /// elbow and wrist where the clip has them, the arm's outward side (unit, along the shoulder line) and the chest's up
        /// (unit). The turn about the forward axis takes `outward` to `outward`*cos - `up`*sin.</summary>
        public static float Drop(Vector3 shoulder,Vector3 elbow,Vector3 wrist,Vector3 outward,Vector3 up)
        {
            Vector3 hand=wrist-shoulder,upper=elbow-shoulder;
            if(hand.LengthSquared()<1e-10f||upper.LengthSquared()<1e-10f) return 0;
            hand=Vector3.Normalize(hand); upper=Vector3.Normalize(upper);
            float d=Math.Max(Need(hand,outward,up,HandFrom,HandCap),Need(upper,outward,up,UpperFrom,UpperCap));
            if(d<=0) return 0;
            // Never past the turn that leaves the upper arm UpperMin out.
            float now=Out(upper,outward);
            d=now<=UpperMin?0:Math.Min(d,Math.Max(0,TurnTo(upper,outward,up,UpperMin)));
            return Math.Min(MaxDrop,d)*Rad;
        }

        private static float Out(Vector3 v,Vector3 outward)=>(float)Math.Asin(Math.Max(-1,Math.Min(1,Vector3.Dot(v,outward))))/Rad;
        private static float Ease(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        // The turn (degrees) about the forward axis that leaves a unit direction `want` degrees out: its frontal part (x out, y up,
        // length rho) turns by the angle that takes x to sin(want), acos(sin(want)/rho) + atan2(y, x) (well defined across the
        // shoulder's height, where y passes 0); none once the frontal part is no longer than sin(want).
        private static float TurnTo(Vector3 v,Vector3 outward,Vector3 up,float want)
        {
            float x=Vector3.Dot(v,outward),y=Vector3.Dot(v,up),rho=(float)Math.Sqrt(x*x+y*y),t=(float)Math.Sin(want*Rad);
            if(rho<1e-4f||t>=rho) return 0;
            return ((float)Math.Acos(t/rho)+(float)Math.Atan2(y,x))/Rad;
        }
        // The turn that brings a direction's out angle to its soft cap, by its frontal share.
        private static float Need(Vector3 v,Vector3 outward,Vector3 up,float from,float cap)
        {
            float x=Vector3.Dot(v,outward),y=Vector3.Dot(v,up); if(x<=0) return 0;
            float now=Out(v,outward),want=Soft(now,from,cap); if(want>=now) return 0;
            return Math.Max(0,TurnTo(v,outward,up,want))*Ease(((float)Math.Sqrt(x*x+y*y)-FrontalFrom)/(FrontalFull-FrontalFrom));
        }
    }
}
