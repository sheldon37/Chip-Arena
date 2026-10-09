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
        // armor-glow.ts:23: halos and the chest spill take the pilot's own accent (p.color, bots included) lerped 26 % toward
        // #d1edff in linear light; absent or malformed: the default accent.
        public static System.Numerics.Vector3 ArmorTint(string accent)=>System.Numerics.Vector3.Lerp(NativePortraitRules.Linear(NativePortraitRules.Parse(accent,NativePortraitRules.ExportAccent)),NativePortraitRules.Linear(0xd1edff),.26f);
        public static float ArmorPulse(float time,float cooldown,bool blocking)=>cooldown>0&&!blocking?1-(1-(.70f+.30f*(float)Math.Cos(time*Math.PI*3)))*Math.Min(1,cooldown/.25f):1;
    }
    public sealed class NativeTrace
    {
        public struct Point {public V3 Position;public float At;}
        public readonly List<Point> Points=new List<Point>();public float Seen;public int Color;
        /// <summary>The trail width factor of the latest sample (projectile-trails.ts t.width).</summary>
        public float Width=1;
        public void Sample(V3 position,float now,int color,float width=1)
        {
            if(now<Seen)Points.Clear();
            // projectile-trails.ts sets the ribbon colour once, when the trail is made; later samples keep it.
            if(Points.Count==0)Color=color;
            var next=new Point{Position=position,At=now};int n=Points.Count;
            if(n==0||(Points[n-1].Position-position).Length>.05f)Points.Add(next);else Points[n-1]=new Point{Position=Points[n-1].Position,At=now};
            Seen=now;Width=width;int keep=0;
            for(int i=0;i<Points.Count;i++)if(now-Points[i].At<NativeGlowRules.TrailLife)Points[keep++]=Points[i];
            if(keep<Points.Count)Points.RemoveRange(keep,Points.Count-keep);
            if(Points.Count>12)Points.RemoveRange(0,Points.Count-12);
        }
        public bool Expired(float now)=>now<Seen||now-Seen>NativeGlowRules.TrailLife;
        /// <summary>
        /// projectile-trails.ts render() for segment i (points i-1 to i): a flat quad facing the camera, its half width
        /// (.012 + .00065·camera distance)·Width, and each end's opacity: the vertex fade times .85 times the trail fade.
        /// fallback is the camera's right vector, used when the segment points at the camera.
        /// </summary>
        public void Segment(int i,V3 camera,V3 fallback,float now,out V3 a0,out V3 a1,out V3 b0,out V3 b1,out float alphaA,out float alphaB)
        {
            V3 a=Points[i-1].Position,b=Points[i].Position,mid=(a+b)*.5f,d=b-a,c=camera-mid;
            var side=new V3(d.y*c.z-d.z*c.y,d.z*c.x-d.x*c.z,d.x*c.y-d.y*c.x);
            if(V3.Dot(side,side)<1e-8f)side=fallback;
            float length=side.Length;side=length>0?side*(NativeGlowRules.TrailWidth(c.Length,Width)/length):side;
            a0=a+side;a1=a-side;b0=b+side;b1=b-side;float tail=.85f*NativeGlowRules.TrailFade(now-Seen);
            alphaA=NativeGlowRules.TrailFade(now-Points[i-1].At)*tail;alphaB=NativeGlowRules.TrailFade(now-Points[i].At)*tail;
        }
    }
}
