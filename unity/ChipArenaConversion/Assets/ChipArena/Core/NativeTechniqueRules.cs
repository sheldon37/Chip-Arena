using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Authority helpers shared by the Unity simulation and deterministic regression checks.
    public static class NativeTechniqueRules
    {
        public const float CoinSeconds=1.4f,SmokeRadius=4,SpringRadius=4,SpringHeal=6,SpringSlow=.85f;
        public static bool Placement(Snapshot previous,Snapshot current,int id)
        {
            int seen=0;if(previous?.Events!=null)foreach(var e in previous.Events)seen=Math.Max(seen,e.Id);
            if(current?.Events!=null)foreach(var e in current.Events)if(e.Id>seen&&e.Kind==EventKind.Teleport&&(e.Source==id||e.Target==id)&&(e.Detail=="static-recall"||e.Detail=="shade-swap"))return true;
            return false;
        }
        public static void End(NativeTechnique k,float now)
        {if(k==null)return;k.Until=Math.Max(k.Release,Math.Min(k.Until,now));k.RecoverUntil=Math.Min(k.RecoverUntil,now);k.Phase="recovery";}
        public static bool Live(NativeTechnique k,float now)=>k?.Kind!=null&&now>=k.Release&&now<k.Until;
        public static bool Busy(NativeTechnique k,float now)=>k?.Kind!=null&&now<k.RecoverUntil;
        public static bool Moving(NativeTechnique k,float now)=>Live(k,now)&&(k.Kind=="chain-hook"||k.Kind=="slide-shot"||k.Kind=="iaido"||k.Kind=="blade-drag"&&k.Phase=="drag"||k.Kind=="lodestone"&&k.Phase=="draw"||k.Kind=="hydro-jet"||k.Kind=="disarm"&&k.Phase=="lunge"||k.Kind=="ground-slam"&&k.Phase=="leap"||k.Kind=="vault-shot"&&now<k.RecoverUntil||k.Kind=="bipod"||k.Kind=="dragon-breath"||k.Kind=="soul-siphon"||k.Kind=="parting-line"&&k.Phase=="hop"&&now<k.Release+.28f||k.Kind=="shield-surf"&&!k.Recast(now)||k.Kind=="recoil-burst"||k.Kind=="grave-walk"||k.Kind=="iron-vow"||k.Kind==NativeTechniques.GrappleLine&&now>=k.Arm||k.Kind==NativeTechniques.AnchorThrow&&k.Recast(now));
        public static bool Recast(NativeTechnique k,float now)=>Live(k,now)&&((k.Kind=="iaido"||k.Kind=="blade-drag"||k.Kind=="lodestone"||k.Kind=="hydro-jet"||k.Kind=="bipod"||k.Kind=="dragon-breath"||k.Kind=="soul-siphon"||k.Kind=="overload"||k.Kind=="shield-surf"||k.Kind=="iron-vow"||k.Kind=="grave-walk")&&!k.Recast(now)||k.Kind==NativeTechniques.Bastion||k.Kind==NativeTechniques.StaticRecall&&!k.Recast(now)||k.Kind==NativeTechniques.AnchorThrow&&now>=k.Arm&&now<k.Arm+3&&!k.Recast(now));
        public static bool Rooted(NativeTechnique k,float now)=>k!=null&&now>=k.Release&&now<k.RecoverUntil&&(k.Kind=="iron-vow"||k.Kind=="grave-walk"&&now>=k.Until);
        public static bool Front(V3 facing,V3 incoming)=>V3.Dot(facing.Normalized,(incoming*-1).Normalized)>.25f;
        public static bool Hidden(V3 observer,V3 target,NativeTechnique cloud,float now)=>Live(cloud,now)&&cloud.Kind==NativeTechniques.SmokeVeil&&Horizontal(target-cloud.Point)<=SmokeRadius&&Math.Abs(target.y-cloud.Point.y)<3&&!(Horizontal(observer-cloud.Point)<=SmokeRadius&&Math.Abs(observer.y-cloud.Point.y)<3);
        private static float Horizontal(V3 v)=>(float)Math.Sqrt(v.x*v.x+v.z*v.z);
        public static bool SegmentSphere(V3 from,V3 delta,V3 center,float radius,out float fraction)
        {
            var offset=from-center;float a=V3.Dot(delta,delta),b=V3.Dot(offset,delta),c=V3.Dot(offset,offset)-radius*radius;fraction=0;
            if(c<=0)return true;if(a<1e-9f)return false;float disc=b*b-a*c;if(disc<0)return false;fraction=(-b-(float)Math.Sqrt(disc))/a;return fraction>=0&&fraction<=1;
        }
        public static bool Wall(V3 from,V3 delta,NativeTechnique wall,float radius,out float fraction)
        {
            fraction=0;var normal=new V3(wall.Dir.x,0,wall.Dir.z).Normalized;float d=V3.Dot(delta,normal);if(Math.Abs(d)<1e-6f)return false;
            fraction=V3.Dot(wall.Point-from,normal)/d;if(fraction<0||fraction>1)return false;var at=from+delta*fraction-wall.Point;var side=new V3(normal.z,0,-normal.x);
            return Math.Abs(V3.Dot(at,side))<=1.3f+radius&&at.y>=-radius&&at.y<=1.8f+radius;
        }
        public static float LineDistance(V3 point,V3 a,V3 b)
        {var d=b-a;float length=V3.Dot(d,d);return (point-(a+d*MathUtil.Clamp(length<1e-6f?0:V3.Dot(point-a,d)/length,0,1))).Length;}
        public static float LeapSpeed(float gravity)=>(float)Math.Sqrt(2*Math.Max(1,gravity)*7);
    }
    public sealed class NativeTechniqueSnapshotSource:ITechniqueSource
    {
        private readonly Snapshot snapshot;public NativeTechniqueSnapshotSource(Snapshot snapshot){this.snapshot=snapshot;}
        public NativeTechnique Of(int actor)=>snapshot?.Actors?.FirstOrDefault(a=>a!=null&&a.Id==actor)?.Technique;
    }
}
