using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The blade a slash crescent is drawn from (owner 2026-10-15: "weapon trails are too far / too early for melee weapons").
    /// The crescents were a fixed arc round the pilot (NativeSlashFx: a radius of .55-.72 x the weapon's reach, 0.7-0.85 m up,
    /// tilted 17-20 degrees toward the front, swept on the replicated hit window): measured against the posed blade of every
    /// cut, it sat .2-.7 m beyond the blade tip (the shield's 1.2-1.3 m arc round a .6-.85 m bash), up to a metre above or
    /// below it (the Explosive pack's cuts slash diagonally down to the floor guard; the arc rises toward the front), and it
    /// ran on the wall clock while the blade waited out its hit-stop. The crescent now sweeps the blade itself: the posed
    /// grip-to-tip line is sampled each frame while the blade is in its fast arc by the swing's own clock (MeleeMotion.CutAge:
    /// the hit-stop hold and the air attacks' landing freeze included), turned about the grip between frames so a fast arc stays
    /// round, and drawn as a band from a share of the blade out to its tip, through the blade's latest pass across the front
    /// (100 degrees either side of the swing's heading, as MeleeSwing.VisibleSweep) up to where it is now. It can never run ahead of
    /// the blade, sits on the blade's radius and plane (reach included) and fades after the window as before. Fixed buffers,
    /// deterministic; presentation only.
    /// </summary>
    public sealed class NativeSlashTrail
    {
        public const int Capacity=72;
        /// <summary>The blade's fast arc as a share of the swing's window: from its start, and a short follow-through past its end.</summary>
        public const float From=0,Follow=.2f;
        /// <summary>A sample beyond this bearing from the swing's heading (radians) is behind the pilot (MeleeSwing.VisibleSweep).</summary>
        public const float Front=1.75f;
        /// <summary>Largest turn (radians) between two stored samples: a longer frame step is filled by turning the blade about its grip.</summary>
        public const float MaxTurn=.16f;
        public struct Sample{public Vector3 Grip,Dir;public float Length,At;public Vector3 Tip=>Grip+Dir*Length;}
        private readonly Sample[] samples=new Sample[Capacity];
        public int Count{get;private set;}
        public Sample this[int i]=>samples[i];
        public float Last=>Count>0?samples[Count-1].At:float.NaN;
        public void Reset()=>Count=0;
        /// <summary>Whether the blade is in the swing's fast arc at share u of its window (MeleeMotion.CutAge against its windup and window).</summary>
        public static bool Sweeping(float age,float windup,float window)
        {
            if(float.IsNaN(age))return false;float u=(age-windup)/Math.Max(.001f,window);return u>=From&&u<=1+Follow;
        }
        /// <summary>One blade pose: its grip and tip (world) at `at`. A time running backwards restarts the trail.</summary>
        public void Add(Vector3 grip,Vector3 tip,float at)
        {
            float length=Vector3.Distance(tip,grip);if(!(length>1e-4f)||float.IsNaN(at))return;var dir=(tip-grip)/length;
            if(Count>0)
            {
                var last=samples[Count-1];if(at<last.At-.1f)Count=0;else if(at<=last.At){samples[Count-1]=new Sample{Grip=grip,Dir=dir,Length=length,At=last.At};return;}
            }
            if(Count>0)
            {
                var last=samples[Count-1];float turn=(float)Math.Acos(MeleeMath.Clamp(Vector3.Dot(last.Dir,dir),-1,1));int steps=Math.Min(6,(int)Math.Ceiling(turn/MaxTurn)-1);
                if(steps>0)
                {
                    var q=Turn(last.Dir,dir);
                    for(int i=1;i<=steps;i++)
                    {
                        float f=(float)i/(steps+1);
                        Push(new Sample{Grip=Vector3.Lerp(last.Grip,grip,f),Dir=Vector3.Normalize(Vector3.Transform(last.Dir,Quaternion.Slerp(Quaternion.Identity,q,f))),Length=last.Length+(length-last.Length)*f,At=last.At+(at-last.At)*f});
                    }
                }
            }
            Push(new Sample{Grip=grip,Dir=dir,Length=length,At=at});
        }
        private void Push(Sample s)
        {
            // Full: every other of the older half goes (the head keeps its detail).
            if(Count==Capacity){int half=Capacity/2,k=0;for(int i=0;i<half;i+=2)samples[k++]=samples[i];for(int i=half;i<Capacity;i++)samples[k++]=samples[i];Count=k;}
            samples[Count++]=s;
        }
        private static Quaternion Turn(Vector3 from,Vector3 to)
        {
            var axis=Vector3.Cross(from,to);float s=axis.Length(),c=Vector3.Dot(from,to);if(s<1e-6f)return Quaternion.Identity;
            return Quaternion.CreateFromAxisAngle(axis/s,(float)Math.Atan2(s,c));
        }
        /// <summary>The tip's bearing from `centre` against the swing heading `yaw` (native radians, + toward the pilot's right).</summary>
        public static float Bearing(Vector3 tip,Vector3 centre,float yaw)=>MeleeMath.Wrap((float)Math.Atan2(tip.X-centre.X,tip.Z-centre.Z)-yaw);
        /// <summary>
        /// The blade's latest crossing of the front: the run of samples in front ending at the newest one there (a cut that
        /// loops round behind the body before it comes through shows only the pass through the front). False without one.
        /// </summary>
        public bool Crossing(Vector3 centre,float yaw,out int first,out int last)
        {
            first=last=-1;for(int i=Count-1;i>=0;i--)if(Math.Abs(Bearing(samples[i].Tip,centre,yaw))<Front){last=i;break;}
            if(last<0)return false;first=last;while(first>0&&Math.Abs(Bearing(samples[first-1].Tip,centre,yaw))<Front)first--;return last>first;
        }
        /// <summary>
        /// The drawn band: per sample through the blade's latest crossing of the front (Crossing), its inner point (`inner` of the
        /// way out along the blade) and outer point (the tip, times `outer`), and its share of the tip's path (0 tail, 1 head).
        /// Writes from 0 and returns the count (0 or at least 2).
        /// </summary>
        public int Band(Vector3 centre,float yaw,float inner,float outer,Vector3[] innerOut,Vector3[] outerOut,float[] share)
        {
            if(!Crossing(centre,yaw,out int first,out int last)||innerOut.Length<last-first+1||outerOut.Length<last-first+1||share.Length<last-first+1)return 0;
            float path=0;for(int i=first+1;i<=last;i++)path+=Vector3.Distance(samples[i].Tip,samples[i-1].Tip);
            float run=0;int n=0;
            for(int i=first;i<=last;i++)
            {
                var s=samples[i];if(i>first)run+=Vector3.Distance(s.Tip,samples[i-1].Tip);
                innerOut[n]=s.Grip+s.Dir*(s.Length*inner);outerOut[n]=s.Grip+s.Dir*(s.Length*outer);share[n]=path>1e-5f?run/path:(float)(i-first)/Math.Max(1,last-first);n++;
            }
            return n;
        }
        /// <summary>The tip's path length through the front (for choosing which dagger's blade carries a twin crescent).</summary>
        public float Swept(Vector3 centre,float yaw)
        {
            if(!Crossing(centre,yaw,out int first,out int last))return 0;float path=0;for(int i=first+1;i<=last;i++)path+=Vector3.Distance(samples[i].Tip,samples[i-1].Tip);
            return path;
        }
        /// <summary>The band's inner edge as a share of the blade (grip 0, tip 1) for a slash look: thicker looks reach further in.</summary>
        public static float Inner(in SlashLook look)=>MeleeMath.Clamp(1-look.Thickness*1.25f,.35f,.8f);
    }
}
