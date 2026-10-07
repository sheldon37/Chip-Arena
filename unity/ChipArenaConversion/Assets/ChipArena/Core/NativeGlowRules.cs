using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Pure envelopes from projectile-glow.ts, impact-glows.ts and projectile-trails.ts.
    public static class NativeGlowRules
    {
        public const float ImpactLife=.85f,TrailLife=.18f;
        public static float HaloAlpha(float radiusSquared)=>(float)Math.Exp(-radiusSquared*4.5f)*Math.Max(0,1-radiusSquared);
        public static float HaloSize(float radius,float quality)=>Math.Min(4,Math.Max(.70f,radius*8.5f))*(quality<.5f?.82f:1);
        public static float ImpactFade(float age)=>age<0||age>=ImpactLife?0:(float)Math.Pow(1-age/ImpactLife,2)*.85f;
        public static float ImpactSize(float age)=>.7f+age*.7f;
        public static float ImpactLight(float age)=>age<0?0:2.5f*(float)Math.Pow(Math.Max(0,1-age/.35f),2);
        public static float TrailFade(float age)=>Math.Max(0,1-age/TrailLife);
        public static float TrailWidth(float distance,float width)=>(.012f+distance*.00065f)*width;
        public static float ArmorPulse(float time,float cooldown,bool blocking)=>cooldown>0&&!blocking?1-(1-(.70f+.30f*(float)Math.Cos(time*Math.PI*3)))*Math.Min(1,cooldown/.25f):1;
    }
    public sealed class NativeTrace
    {
        public struct Point {public V3 Position;public float At;}
        public readonly List<Point> Points=new List<Point>();public float Seen;public int Color;
        public void Sample(V3 position,float now,int color)
        {
            if(now<Seen)Points.Clear();
            var next=new Point{Position=position,At=now};int n=Points.Count;
            if(n==0||(Points[n-1].Position-position).Length>.05f)Points.Add(next);else Points[n-1]=new Point{Position=Points[n-1].Position,At=now};
            Seen=now;Color=color;Points.RemoveAll(p=>now-p.At>=NativeGlowRules.TrailLife);
            if(Points.Count>12)Points.RemoveRange(0,Points.Count-12);
        }
        public bool Expired(float now)=>now<Seen||now-Seen>NativeGlowRules.TrailLife;
    }
}
