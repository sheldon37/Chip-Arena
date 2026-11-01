using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Optional native-frame data, independent of impact VFX and damage events. A piercing or reflected shot has no end
    // until authority actually removes it. Old hosts omit this array; old guests can ignore it.
    [Serializable] public struct NativeShotEnd {public int Id;public float At;public V3 Position;}

    public sealed class NativeShotEnds
    {
        public const int Max=1024;public const float Keep=1;
        private readonly List<NativeShotEnd> ends=new List<NativeShotEnd>(Max);
        public int Count=>ends.Count;
        public void Clear()=>ends.Clear();
        private void Prune(float now)
        {int n=0;while(n<ends.Count&&now-ends[n].At>Keep)n++;if(n>0)ends.RemoveRange(0,n);}
        public void Record(int id,float at,V3 position)
        {Prune(at);if(ends.Count==Max)ends.RemoveAt(0);ends.Add(new NativeShotEnd{Id=id,At=at,Position=position});}
        public NativeShotEnd[] Snapshot(float now){Prune(now);return ends.ToArray();}
        public void Restore(NativeShotEnd[] records){ends.Clear();if(records!=null)ends.AddRange(records);}
        public static bool Valid(NativeShotEnd[] records,double frameTime)
        {
            if(records==null||records.Length==0)return true;if(records.Length>Max)return false;
            var ids=new HashSet<int>();float last=-1;
            foreach(var e in records)
            {
                var p=e.Position;
                if(e.Id<=0||!ids.Add(e.Id)||!MathUtil.Finite(e.At)||e.At<0||e.At<last||e.At>frameTime+.001
                    ||!MathUtil.Finite(p.x)||!MathUtil.Finite(p.y)||!MathUtil.Finite(p.z)
                    ||Math.Abs(p.x)>=100000||Math.Abs(p.y)>=100000||Math.Abs(p.z)>=100000)return false;
                last=e.At;
            }
            return true;
        }
    }

    // A replay already has its NEXT recorded frame. Use it, rather than the live guest's velocity extrapolation.
    // Prepared once per frame pair; sampling all models, glows and trails allocates nothing.
    public sealed class NativeReplayShots
    {
        private readonly Dictionary<int,Shot> before=new Dictionary<int,Shot>(),after=new Dictionary<int,Shot>();
        private readonly Dictionary<int,NativeShotEnd> ends=new Dictionary<int,NativeShotEnd>();
        private double from,to;
        public void Window(double from,double to,Shot[] a,Shot[] b,NativeShotEnd[] stopped)
        {
            this.from=from;this.to=to;before.Clear();after.Clear();ends.Clear();
            if(a!=null)foreach(var s in a)before[s.Id]=s;
            if(b!=null)foreach(var s in b)after[s.Id]=s;
            if(stopped!=null)foreach(var e in stopped)if(e.At>=from&&e.At<=to+.001)ends[e.Id]=e;
        }
        public bool Sample(int id,double now,out V3 position,out V3 velocity)
        {
            position=velocity=default;if(!before.TryGetValue(id,out var a))return false;
            position=a.Position;velocity=a.Velocity;
            if(ends.TryGetValue(id,out var end))
            {
                if(now>=end.At)return false;
                position=Mix(a.Position,end.Position,Fraction(now,from,end.At));return true;
            }
            if(after.TryGetValue(id,out var b))
            {float t=Fraction(now,from,to);position=Mix(a.Position,b.Position,t);velocity=Mix(a.Velocity,b.Velocity,t);}
            // Missing endpoint (old host, or a non-impact removal): hold the last recorded position until the next frame.
            // Never invent forward flight into a player or a wall. No extra collision/damage simulation in a replay.
            return true;
        }
        private static float Fraction(double now,double a,double b)=>(float)Math.Max(0,Math.Min(1,(now-a)/Math.Max(.000001,b-a)));
        private static V3 Mix(V3 a,V3 b,float t)=>a+(b-a)*t;
    }
}
