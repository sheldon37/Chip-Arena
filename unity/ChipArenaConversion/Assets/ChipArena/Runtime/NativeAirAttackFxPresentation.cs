using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The melee jump attack's impact (Core/NativeAirAttackFx): each pilot's presented root is watched by a NativeJumpTrack and
    /// its swings by a NativeAirAttackWatch (the rules the rig's jump attack uses), and a landing during a jump attack leaves a
    /// dust ring rolling out over the floor, cracks running out from the weapon's point of impact with a crack mark there, and a
    /// weapon-accent shock ring, all by the impact's strength, through the shared effects renderer; it also adds a short
    /// camera shake under the structure shake's rules (distance falloff, the camera-shake setting, reduced effects).
    /// Reduced effects and Low graphics thin it. Presentation only: host, guests, killcam and spectators each draw it from what
    /// they present; the hit itself is the authority's.
    /// </summary>
    public sealed class NativeAirAttackFxPresentation
    {
        private struct Burst{public float At,Floor,Strength;public Vector3 Feet,Point;public int Seed;public Color Accent;}
        private readonly NativeVfxDraw vfx;
        private readonly Dictionary<int,NativeJumpTrack> tracks=new Dictionary<int,NativeJumpTrack>();
        private readonly Dictionary<int,NativeAirAttackWatch> watches=new Dictionary<int,NativeAirAttackWatch>();
        private readonly List<Burst> bursts=new List<Burst>(8);
        private NativeArena last;private int generation=-1,seeds;private float lastTime=-1;
        public int Live=>bursts.Count;
        public NativeAirAttackFxPresentation(NativeVfxDraw vfx){this.vfx=vfx;}
        public void Clear(){bursts.Clear();tracks.Clear();watches.Clear();lastTime=-1;}
        /// <summary>now: the snapshot clock (ArenaPresentation.FieldClock); quality: the effects quality (1, .3 Reduced);
        /// structures: the destruction kit whose shake and crack marks the impact adds to (null before its first frame).</summary>
        public void Render(NativeArena arena,float now,float quality,bool low,NativeStructureFx structures)
        {
            if(arena==null||vfx==null)return;
            if(arena!=last||generation!=arena.Generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;}
            lastTime=Mathf.Max(lastTime,now);
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;var a=p.Pose;
                if(!tracks.TryGetValue(p.Seat.id,out var t))tracks[p.Seat.id]=t=new NativeJumpTrack();
                if(!watches.TryGetValue(p.Seat.id,out var w))watches[p.Seat.id]=w=new NativeAirAttackWatch();
                Vector3 feet=p.Body.transform.position-Vector3.up*.865f;
                bool blocked=a.Hp<=0||a.Rolling(now)||a.StunUntil>now||a.JuggleUntil>now||NativeSpecialPose.Active(a,now)||NativeSpecialPose.Down(a,now);
                t.Observe(now,a.Grounded,feet.y,blocked);int impacts=w.Impacts;
                w.Observe(t,a.AttackAt>-90?a.AttackAt:-100,a.CommitUntil>-90?a.CommitUntil:a.AttackAt+a.VisualDuration,a.SprintAttack?0:-1,now,blocked);
                if(w.Impacts>impacts&&a.Hp>0&&NativeAirAttackRules.Clip(p.Seat.spec)!=null)
                {
                    var tint=NativeGlowRules.ArmorTint(a.AccentColor);var point=NativeAirAttackFx.Point(new NVector(feet.x,feet.y,feet.z),a.Yaw,t.Floor);
                    var b=new Burst{At=now,Feet=feet,Floor=t.Floor,Strength=w.Strength,Point=new Vector3(point.X,point.Y,point.Z),Seed=++seeds,Accent=new Color(tint.X,tint.Y,tint.Z,0)};
                    if(bursts.Count>=8)bursts.RemoveAt(0);bursts.Add(b);
                    var at=new V3(b.Point.x,b.Point.y,b.Point.z);
                    structures?.Quake(at,NativeAirAttackFx.ShakePeak(b.Strength),NativeAirAttackFx.ShakeSeconds,(uint)b.Seed,now);
                    structures?.AddMark(NativeStructureMark.Crack,at,new V3(0,1,0),.5f+.4f*b.Strength,(uint)b.Seed,now);
                }
            }
            for(int i=bursts.Count-1;i>=0;i--)if(now-bursts[i].At>=NativeAirAttackFx.Life||now<bursts[i].At-.25f)bursts.RemoveAt(i);
            int dust=NativeAirAttackFx.Count(NativeAirAttackFx.Dust,NativeAirAttackFx.ReducedDust,quality,low),cracks=NativeAirAttackFx.Count(NativeAirAttackFx.Cracks,NativeAirAttackFx.ReducedCracks,quality,low);
            var dustTone=new Color(.55f,.52f,.48f,0);
            foreach(var b in bursts)
            {
                float age=now-b.At;var feet=new NVector(b.Feet.x,b.Feet.y,b.Feet.z);var point=new NVector(b.Point.x,b.Point.y,b.Point.z);
                if(NativeAirAttackFx.Shock(age,b.Strength,out float size,out float alpha))vfx.Glow(b.Point+Vector3.up*.25f,b.Accent*(2.4f*alpha),size,NativeVfxDraw.Ring,age/NativeAirAttackFx.ShockLife,b.Seed);
                for(int k=0;k<cracks;k++)
                    if(NativeAirAttackFx.Crack(k,cracks,b.Seed,age,b.Strength,point,out var head,out var dir,out float length,out float fade))
                        vfx.Streak(new Vector3(head.X,head.Y,head.Z),new Vector3(dir.X,dir.Y,dir.Z)*length,b.Accent*(1.8f*fade),.035f,length,.2f,.6f,age/NativeAirAttackFx.CrackLife,b.Seed*29+k);
                for(int k=0;k<dust;k++)
                    if(NativeAirAttackFx.Puff(k,dust,b.Seed,age,b.Strength,feet,b.Floor,out var at,out float ps,out float opacity))
                        vfx.SoftSmoke(new Vector3(at.X,at.Y,at.Z),dustTone,opacity,ps,age/NativeAirAttackFx.DustLife,b.Seed*17+k,b.Floor);
            }
        }
    }
    public sealed partial class ArenaPresentation
    {
        // The jump attack's impact (Runtime/NativeAirAttackFxPresentation), queued into the shared effects renderer in RenderGlow.
        private NativeAirAttackFxPresentation airAttackFx;
        private void AirAttackFx(NativeArena arena,float now)
        {
            if(vfx==null||arena==null)return;airAttackFx??=new NativeAirAttackFxPresentation(vfx);
            airAttackFx.Render(arena,now,WeaponEffectsQuality,LowGraphics,structureFx?.Fx);
        }
    }
    public sealed partial class NativeAudio
    {
        // ---------- the melee jump attack's impact (Core/NativeAirAttack) ----------
        // Each pilot's presented root and swings are watched by the rules the rig uses; a landing during a jump attack plays the
        // existing ground impact under a heavier landing thump, louder and lower for a harder impact. No new bank key.
        private readonly Dictionary<int,NativeJumpTrack> airTracks=new Dictionary<int,NativeJumpTrack>();
        private readonly Dictionary<int,NativeAirAttackWatch> airWatches=new Dictionary<int,NativeAirAttackWatch>();
        private int airGeneration=-1;
        public const float AirImpactGain=.9f,AirImpactOwnGain=.7f,AirLandGain=.8f;
        private void AirAttackCues(NativeArena arena)
        {
            if(arena==null)return;if(arena.Generation!=airGeneration){airGeneration=arena.Generation;airTracks.Clear();airWatches.Clear();}
            float t=arena.Replica?arena.ReplicaRenderTime:arena.Time;var me=arena.Local;
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;var a=p.Pose;
                if(!airTracks.TryGetValue(p.Seat.id,out var track))airTracks[p.Seat.id]=track=new NativeJumpTrack();
                if(!airWatches.TryGetValue(p.Seat.id,out var watch))airWatches[p.Seat.id]=watch=new NativeAirAttackWatch();
                bool blocked=a.Hp<=0||a.Rolling(t)||a.StunUntil>t||a.JuggleUntil>t||NativeSpecialPose.Active(a,t)||NativeSpecialPose.Down(a,t);
                track.Observe(t,a.Grounded,p.Body.transform.position.y,blocked);int impacts=watch.Impacts;
                watch.Observe(track,a.AttackAt>-90?a.AttackAt:-100,a.CommitUntil>-90?a.CommitUntil:a.AttackAt+a.VisualDuration,a.SprintAttack?0:-1,t,blocked);
                if(watch.Impacts>impacts&&a.Hp>0&&NativeAirAttackRules.Clip(p.Seat.spec)!=null)
                {
                    bool mine=p==me;float k=watch.Strength,rate=p.Seat.spec=="greatsword"?.85f:1;
                    BankPlay("impact.ground",p.Body.position,(mine?AirImpactOwnGain:AirImpactGain)*(.6f+.4f*k),0,null,mine,null,rate*(1.05f-.15f*k));
                    BankPlay("move.land",p.Body.position,AirLandGain*(.6f+.4f*k),0,null,mine,null,.9f*rate);
                }
            }
        }
    }
}
