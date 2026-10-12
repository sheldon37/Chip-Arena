using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws the dagger air jump's burst and afterimage (Core/NativeJumpFx) through the shared effects renderer: each pilot's
    /// presented root is watched by a NativeJumpTrack (the same rule the rig's jump layer uses), and an air jump by a dagger
    /// pilot leaves a camera-facing accent ring with short streaks under its feet and a few soft accent puffs where its body
    /// was. Everything is upright or round (never a flat disc) and stays above the floor it jumped from. The view pilot's
    /// own afterimage is skipped in first person (it would fill the camera). Reduced effects and Low graphics thin it.
    /// Presentation only: host, guests, killcam and spectators each draw it from what they present.
    /// </summary>
    public sealed class NativeJumpFxPresentation
    {
        private struct Burst{public float At,Floor;public Vector3 Feet;public int Seed,Owner;public Color Accent;}
        private readonly NativeVfxDraw vfx;
        private readonly Dictionary<int,NativeJumpTrack> tracks=new Dictionary<int,NativeJumpTrack>();
        private readonly List<Burst> bursts=new List<Burst>(16);
        private NativeArena last;private int generation=-1,seeds;private float lastTime=-1;
        public int Live=>bursts.Count;
        public NativeJumpFxPresentation(NativeVfxDraw vfx){this.vfx=vfx;}
        public void Clear(){bursts.Clear();tracks.Clear();lastTime=-1;}
        /// <summary>now: the snapshot clock (ArenaPresentation.FieldClock); quality: the effects quality (1, .3 Reduced).</summary>
        public void Render(NativeArena arena,float now,float quality,bool low,int viewId,bool firstPerson)
        {
            if(arena==null||vfx==null)return;
            if(arena!=last||generation!=arena.Generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;}
            lastTime=Mathf.Max(lastTime,now);
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;var a=p.Pose;
                if(!tracks.TryGetValue(p.Seat.id,out var t))tracks[p.Seat.id]=t=new NativeJumpTrack();
                int jumps=t.AirJumps;Vector3 feet=p.Body.transform.position-Vector3.up*.865f;
                bool blocked=a.Hp<=0||a.Rolling(now)||a.StunUntil>now||a.JuggleUntil>now||NativeSpecialPose.Active(a,now)||NativeSpecialPose.Down(a,now);
                t.Observe(now,a.Grounded,feet.y,blocked);
                if(t.AirJumps>jumps&&a.Hp>0&&NativeJumpRules.Flips(p.Seat.spec))
                {
                    var tint=NativeGlowRules.ArmorTint(a.AccentColor);if(bursts.Count>=16)bursts.RemoveAt(0);
                    bursts.Add(new Burst{At=now,Feet=feet,Floor=t.Floor,Seed=++seeds,Owner=p.Seat.id,Accent=new Color(tint.X,tint.Y,tint.Z,0)});
                }
            }
            for(int i=bursts.Count-1;i>=0;i--)if(now-bursts[i].At>=NativeJumpFx.Life||now<bursts[i].At-.25f)bursts.RemoveAt(i);
            int streaks=NativeJumpFx.Count(NativeJumpFx.Streaks,NativeJumpFx.ReducedStreaks,quality,low),puffs=NativeJumpFx.Count(NativeJumpFx.Puffs,NativeJumpFx.ReducedPuffs,quality,low);
            foreach(var b in bursts)
            {
                float age=now-b.At;var feet=new NVector(b.Feet.x,b.Feet.y,b.Feet.z);var centre=NativeJumpFx.Centre(feet,b.Floor);Vector3 c=new Vector3(centre.X,centre.Y,centre.Z);
                if(NativeJumpFx.Ring(age,out float size,out float alpha))vfx.Glow(c,b.Accent*(NativeJumpFx.RingGain*alpha),size,NativeVfxDraw.Ring,age/NativeJumpFx.RingLife,b.Seed);
                for(int k=0;k<streaks;k++)
                    if(NativeJumpFx.Streak(k,streaks,b.Seed,age,centre,b.Floor,out var head,out var dir,out float length,out float fade))
                        vfx.Streak(new Vector3(head.X,head.Y,head.Z),new Vector3(dir.X,dir.Y,dir.Z)*length,b.Accent*(NativeJumpFx.StreakGain*fade),NativeJumpFx.StreakWidth,length,1,.8f,age/NativeJumpFx.StreakLife,b.Seed*31+k);
                if(firstPerson&&b.Owner==viewId)continue;
                for(int k=0;k<puffs;k++)
                    if(NativeJumpFx.Puff(k,puffs,b.Seed,age,feet,out var at,out float ps,out float opacity))
                    {
                        Vector3 u=new Vector3(at.X,at.Y,at.Z);float life=age/NativeJumpFx.PuffLife;
                        vfx.SoftSmoke(u,b.Accent,opacity,ps,life,b.Seed*17+k,b.Floor);vfx.Glow(u,b.Accent*(NativeJumpFx.PuffGain*opacity),ps*.8f,NativeVfxDraw.Mote,life,b.Seed*13+k);
                    }
            }
        }
    }
    public sealed partial class ArenaPresentation
    {
        // The dagger air jump's burst (Runtime/NativeJumpFxPresentation), queued into the shared effects renderer in RenderGlow.
        private NativeJumpFxPresentation jumpFx;
        private void JumpFx(NativeArena arena,float now)
        {
            if(vfx==null||arena==null)return;jumpFx??=new NativeJumpFxPresentation(vfx);
            jumpFx.Render(arena,now,WeaponEffectsQuality,LowGraphics,arena.ViewPilot?.Seat.id??-1,FirstPerson);
        }
    }
}
