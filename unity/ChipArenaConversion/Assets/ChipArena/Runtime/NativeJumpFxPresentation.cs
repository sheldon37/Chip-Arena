using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws the air jump's burst and afterimage (Core/NativeJumpFx shapes, Core/NativeAirJumpFx per weapon and charge) through
    /// the shared effects renderer: each pilot's presented root is watched by a NativeJumpTrack (the same rule the rig's jump
    /// layer uses), and every air jump, once per spent charge, leaves a camera-facing accent ring with short streaks under its
    /// feet and a few soft accent puffs where its body was. The daggers and the Bow keep their burst exactly; every other weapon
    /// (the movement chips' air jumps) shows the same burst, a little smaller, brighter and whiter per chained charge; the Cyber
    /// Lord's boost has its own effects. Everything is upright or round (never a flat disc) and stays above the floor it jumped
    /// from. The view pilot's own afterimage is skipped in first person (it would fill the camera). Reduced effects and Low
    /// graphics thin it. Presentation only: host, guests, killcam and spectators each draw it from what they present.
    /// </summary>
    public sealed class NativeJumpFxPresentation
    {
        private sealed class Sink:IAirJumpFxSink
        {
            public NativeVfxDraw Vfx;
            private static Vector3 U(NVector v)=>new Vector3(v.X,v.Y,v.Z);private static Color C(NVector v)=>new Color(v.X,v.Y,v.Z,0);
            public void Ring(NVector at,NVector color,float size,float u,float seed)=>Vfx.Glow(U(at),C(color),size,NativeVfxDraw.Ring,u,seed);
            public void Streak(NVector head,NVector velocity,NVector color,float width,float minLength,float blur,float taper,float u,float seed)=>Vfx.Streak(U(head),U(velocity),C(color),width,minLength,blur,taper,u,seed);
            public void Puff(NVector at,NVector albedo,float opacity,float size,float u,float seed,float floor)=>Vfx.SoftSmoke(U(at),C(albedo),opacity,size,u,seed,floor);
            public void Mote(NVector at,NVector color,float size,float u,float seed)=>Vfx.Glow(U(at),C(color),size,NativeVfxDraw.Mote,u,seed);
        }
        private readonly NativeVfxDraw vfx;private readonly Sink sink;
        private readonly NativeAirJumpBursts bursts=new NativeAirJumpBursts();
        private NativeArena last;private int generation=-1;private float lastTime=-1;
        public int Live=>bursts.Live;
        public NativeJumpFxPresentation(NativeVfxDraw vfx){this.vfx=vfx;sink=new Sink{Vfx=vfx};}
        public void Clear(){bursts.Clear();lastTime=-1;}
        /// <summary>now: the snapshot clock (ArenaPresentation.FieldClock); quality: the effects quality (1, .3 Reduced).</summary>
        public void Render(NativeArena arena,float now,float quality,bool low,int viewId,bool firstPerson)
        {
            if(arena==null||vfx==null)return;
            if(arena!=last||generation!=arena.Generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;}
            lastTime=Mathf.Max(lastTime,now);
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;var a=p.Pose;Vector3 feet=p.Body.transform.position-Vector3.up*.865f;
                bool blocked=a.Hp<=0||a.Rolling(now)||a.StunUntil>now||a.JuggleUntil>now||NativeSpecialPose.Active(a,now)||NativeSpecialPose.Down(a,now);
                arena.PresentedJump(p,out bool grounded,out float airJumpAt);
                bursts.Observe(p.Seat.id,p.Seat.spec,now,grounded,new NVector(feet.x,feet.y,feet.z),blocked,airJumpAt,a.Hp>0,a.AccentColor);
            }
            bursts.Draw(sink,now,quality,low,viewId,firstPerson);
        }
    }
    public sealed partial class ArenaPresentation
    {
        // The air jump's burst (Runtime/NativeJumpFxPresentation) and the sprint's effects (Runtime/NativeSprintFxPresentation),
        // queued into the shared effects renderer in RenderGlow.
        private NativeJumpFxPresentation jumpFx;
        private void JumpFx(NativeArena arena,float now)
        {
            if(vfx==null||arena==null)return;jumpFx??=new NativeJumpFxPresentation(vfx);
            jumpFx.Render(arena,now,WeaponEffectsQuality,LowGraphics,arena.ViewPilot?.Seat.id??-1,FirstPerson);
            SprintFx(arena,now);
        }
    }
}
