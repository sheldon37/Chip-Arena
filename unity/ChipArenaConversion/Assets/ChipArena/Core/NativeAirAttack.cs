using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The melee jump attack's presentation (owner 2026-10-12: "Give jump attacks to melee classes. (Think of Elden ring style
    /// jump attack animations) or if theres anything in the pack."): a swing that starts while the pilot is presented airborne
    /// plays the weapon's pack air attack (tools/export_pack_air_attacks.mjs, NativePackAirAttacks.Generated) instead of its
    /// ground cut. The take raises the weapon overhead and hangs there through the windup, then chops down across the real hit
    /// window (the sword's diving cut; the daggers' reverse grip stabs down; the shield slams its face; the greatsword brings
    /// the two-handed blade over the top). A landing during the attack holds the chop low through a heavy landing crouch; an
    /// attack that ends in the air settles back to the guard the jump's air pose rides on. Presentation only: the swing's
    /// timing, hitbox, damage and every simulation rule are the authority's (a plunging attack with its own timing is Astra's;
    /// docs/MELEE-FEEL.md "Jump attacks"). No allocation.
    /// </summary>
    public static class NativeAirAttackRules
    {
        /// <summary>The weapon's air take (null for a weapon without one: it keeps its ground cuts in the air).</summary>
        public static CapturedClip Clip(string spec)
        {
            var names=NativePackAirAttacks.Names;for(int i=0;i<names.Length;i++)if(names[i]==spec)return NativePackAirAttacks.Clips[i];return null;
        }
        /// <summary>
        /// The takes' shared keys (clip seconds, from the posed blade tip; the pack holds its keys between them): the weapon is
        /// raised overhead by Raised and held to SweepStart, chops down to SweepEnd (60-150 m/s at the tip), holds the impact
        /// to Impact and follows through to FollowEnd. MeleeSwing plays SweepStart to Impact across the hit window (the chop
        /// two thirds of the way in, at 0.6x its authored speed, then the held impact) and the follow-through after it.
        /// </summary>
        public const float Start=0,Raised=.067f,SweepStart=.133f,SweepEnd=.2f,Impact=.233f,FollowEnd=.3f;
        /// <summary>The share of the windup that raises the weapon (eased out: up fast; at least MinRaise seconds, at most 85% of
        /// a short windup, so the daggers' 90 ms raise stays slower than their chop); the rest is the hang.</summary>
        public const float RaiseShare=.45f,MinRaise=.075f;
        public static float RaiseTime(float windup)=>Math.Min(.85f*windup,Math.Max(RaiseShare*windup,MinRaise));
        /// <summary>The clip time `age` seconds into an air attack: the raise and hang over the windup, then the strike across
        /// the hit window and the follow-through (MeleeSwing). A heavy landing holds the pose by holding `age` (Frozen).</summary>
        public static float ClipTime(CapturedClip c,float age,float windup,float active,float recovery)
        {
            windup=Math.Max(.001f,windup);
            if(age<windup)
            {
                float raise=RaiseTime(windup),x=Math.Max(0,age);
                if(x<raise){float k=x/raise;return Start+(Raised-Start)*(1-(1-k)*(1-k));}
                return Raised+(SweepStart-Raised)*MeleeMath.Smooth((x-raise)/(windup-raise));
            }
            return MeleeSwing.ClipTime(c,age,windup,active,recovery);
        }
        // ---- the first-person view model ----
        /// <summary>
        /// The view model's chest bow (radians, forward) `age` seconds into an air attack: the rig's raise holds the weapon
        /// overhead behind the eyes and out to the weapon side, so the view model bows the chest ViewBow forward (and turns it
        /// ViewTurn toward the centre with the bow) through the raise and the hang, the weapon cocked high in front of the
        /// camera; it eases to ViewChop as the chop comes down across the hit window and back to upright over the first half of
        /// the recovery: an overhead-to-down chop in view.
        /// </summary>
        public const float ViewBow=1.05f,ViewChop=.3f,ViewTurn=.55f;
        public static float ViewPitch(float age,float windup,float active,float recovery)
        {
            windup=Math.Max(.001f,windup);active=Math.Max(.001f,active);
            if(age<windup)return ViewBow*MeleeMath.Ease(Math.Max(0,age)/RaiseTime(windup));
            if(age<windup+active)return ViewBow+(ViewChop-ViewBow)*MeleeMath.Smooth((age-windup)/active);
            return ViewChop*(1-MeleeMath.Smooth((age-windup-active)/Math.Max(.001f,.5f*recovery)));
        }
        /// <summary>Bows the view model's chest (CapturedRig.Pose local rotations) by ViewPitch, turning it with the bow, and
        /// refreshes its world pose.</summary>
        public static void ViewPose(CapturedRig.Pose pose,float pitch)
        {
            if(pose==null||Math.Abs(pitch)<1e-5f)return;
            pose.Rotation[CapturedRig.Chest]=MeleeMath.AxisAngle(MeleeMath.Y,ViewTurn*pitch/ViewBow)*MeleeMath.AxisAngle(MeleeMath.X,pitch)*pose.Rotation[CapturedRig.Chest];pose.World();
        }
        // ---- detection: the clean hook for an explicit replicated field ----
        /// <summary>
        /// Whether a swing is an air attack. `explicitAir` is the replicated flag once the simulation sends one (1 air, 0
        /// ground; Astra may add Swing.Air with its own plunge and land times), -1 while none exists: then the presented
        /// airtime decides (NativeJumpTrack, the same root reading the jump animation uses), so host, guests, killcam and
        /// spectators agree with what they draw.
        /// </summary>
        public static bool Air(int explicitAir,bool airborne)=>explicitAir>=0?explicitAir==1:airborne;
        /// <summary>A landing this long after the swing's end still counts as its impact.</summary>
        public const float LandGrace=.05f;
        /// <summary>The impact's strength (0..1) from the fall speed (m/s): .55 from a short hop, 1 from a 3 m drop.</summary>
        public static float Strength(float fallSpeed)=>MeleeMath.Clamp(.4f+NativeJumpRules.Impact(fallSpeed)*.6f,.4f,1);
        /// <summary>The heavy landing, all by the strength: the jump layer's landing drop is scaled by up to HeavyLand
        /// (NativeJumpMotion.LandScale), the chop's pose is held up to HeavyHold seconds before the follow-through, and the hips
        /// come down from the take's to the stance's height and sink a further HeavyCrouch (metres) under a folded trunk while it
        /// holds.</summary>
        public const float HeavyLand=2.1f,HeavyHold=.16f,HeavyCrouch=.12f;
        public static float LandScale(float strength)=>1+(HeavyLand-1)*MeleeMath.Clamp(strength,0,1);
        public static float Hold(float strength)=>HeavyHold*MeleeMath.Clamp(strength,0,1);
        /// <summary>How long the pose has been held at `age` (seconds into the swing): from the later of the landing (`landAge`,
        /// NaN for none) and the window's end, for up to Hold(strength).</summary>
        public static float Frozen(float age,float landAge,float windup,float active,float strength)
            =>float.IsNaN(landAge)?0:MeleeMath.Clamp(age-Math.Max(landAge,windup+active),0,Hold(strength));
    }

    /// <summary>
    /// One presented pilot's air attacks, from its jump track and its swing fields: whether the current swing started in the
    /// air, and the impact when the pilot lands during it (or within LandGrace after it). The rig (MeleeMotion), the first-person
    /// view model and the impact effects and sound each keep one over the same presented state. A rewind or a gap restarts it.
    /// </summary>
    public sealed class NativeAirAttackWatch
    {
        public float SwingAt=-100,SwingEnd=-100,ImpactAt=-100,Strength,Time=-100;public bool Air;public int Impacts;
        private int landings;
        public void Reset(){SwingAt=SwingEnd=ImpactAt=Time=-100;Air=false;Strength=0;landings=0;}
        /// <summary>`attackAt` the swing's start (-100 for none), `attackEnd` its end while known, `explicitAir` the hook
        /// (NativeAirAttackRules.Air), `track` the pilot's jump track as observed this frame, `owned` whether a Q/E special, a stun,
        /// a roll, a juggle or a knockdown owns the pilot: an airborne Wolf Knight or Skyfall is a special, never a jump attack,
        /// so a swing begun while owned is a ground swing and a landing while owned is no impact.</summary>
        public void Observe(NativeJumpTrack track,float attackAt,float attackEnd,int explicitAir,float time,bool owned=false)
        {
            if(track==null||!MathUtil.Finite(time))return;
            if(Time<-90||time<Time-MeleeGait.REWIND||time-Time>.35f){Reset();Time=time;landings=track.Landings;}
            Time=Math.Max(Time,time);
            if(attackAt>-90&&attackAt!=SwingAt&&time>=attackAt-1e-4f)
            {
                SwingAt=attackAt;SwingEnd=attackEnd;
                // Airborne as the swing begins: the track is airborne now, or it landed after the swing's start (a late sight).
                bool airborne=track.Airborne||track.Landings>landings&&track.LandAt>=attackAt-1e-4f;
                Air=!owned&&NativeAirAttackRules.Air(explicitAir,airborne&&track.AirAt<=attackAt+1e-4f);
            }
            if(attackAt==SwingAt&&attackEnd>SwingEnd)SwingEnd=attackEnd;
            if(track.Landings!=landings)
            {
                landings=track.Landings;
                if(Air&&!owned&&track.LandAt>=SwingAt-1e-4f&&track.LandAt<=SwingEnd+NativeAirAttackRules.LandGrace){ImpactAt=track.LandAt;Strength=NativeAirAttackRules.Strength(track.FallSpeed);Impacts++;}
            }
        }
        /// <summary>The current swing is an air attack that has landed (seconds since; NaN otherwise).</summary>
        public float Landed(float time)=>Air&&ImpactAt>=SwingAt-1e-4f&&ImpactAt>-90?time-ImpactAt:float.NaN;
    }

    /// <summary>
    /// The jump attack's impact effect (drawn by Runtime/NativeAirAttackFxPresentation): a dust ring rolling out over the floor,
    /// short cracks running out from the weapon's point of impact, and a weapon-accent shock ring there, all scaled by the
    /// impact's strength; and the camera shake it adds (the existing structure shake rules: distance falloff, the camera-shake
    /// setting, reduced effects). Positions in the Unity world; everything stays above the floor. No allocation.
    /// </summary>
    public static class NativeAirAttackFx
    {
        public const float Life=.6f,ShockLife=.22f,CrackLife=.4f,DustLife=.55f,Reach=1.15f,FloorClear=.04f;
        public const int Dust=10,ReducedDust=4,Cracks=7,ReducedCracks=3;
        /// <summary>The shake: peak offset (m) at the impact and its length (s), by strength.</summary>
        public static float ShakePeak(float strength)=>.025f+.04f*MeleeMath.Clamp(strength,0,1);
        public const float ShakeSeconds=.35f;
        public static int Count(int full,int reduced,float quality,bool low){int n=quality<.5f?reduced:full;return low?Math.Max(1,(n+1)/2):n;}
        private static float Out(float u)=>1-(1-u)*(1-u)*(1-u);
        private static float Hash(int seed,int k){uint h=(uint)(seed*73856093)^(uint)(k*19349663);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return (h&0xffffff)/16777216f;}
        /// <summary>Where the weapon meets the floor: Reach ahead of the feet along the facing (Unity yaw, degrees).</summary>
        public static Vector3 Point(Vector3 feet,float yawDegrees,float floor)
        {
            float a=yawDegrees*(float)Math.PI/180;return new Vector3(feet.X+(float)Math.Sin(a)*Reach,floor,feet.Z+(float)Math.Cos(a)*Reach);
        }
        /// <summary>The accent shock: a camera-facing ring at the point of impact, growing from .25 to 1.4 m by strength.</summary>
        public static bool Shock(float age,float strength,out float size,out float alpha)
        {
            size=alpha=0;float u=age/ShockLife;if(!(u>=0&&u<1))return false;
            size=(.25f+(.75f+.4f*strength)*Out(u));alpha=(float)Math.Pow(1-u,1.4)*(.6f+.4f*strength);return true;
        }
        /// <summary>Crack k of n: a short streak running out over the floor from the point of impact, fast then stopping.</summary>
        public static bool Crack(int k,int n,int seed,float age,float strength,Vector3 point,out Vector3 head,out Vector3 dir,out float length,out float alpha)
        {
            head=point;dir=Vector3.UnitX;length=alpha=0;float u=age/CrackLife;if(!(u>=0&&u<1)||n<=0)return false;
            float a=k*2.39996323f+Hash(seed,k)*.7f;dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));
            float reach=(.35f+.45f*Hash(seed,k+17))*(.6f+.4f*strength),run=reach*Out(Math.Min(1,u*2.2f));
            head=point+dir*run+new Vector3(0,FloorClear,0);length=Math.Max(.05f,run);alpha=(1-u)*(1-u);return true;
        }
        /// <summary>Dust puff k of n: round the feet, rolling out over the floor and up a little, growing and fading.</summary>
        public static bool Puff(int k,int n,int seed,float age,float strength,Vector3 feet,float floor,out Vector3 at,out float size,out float opacity)
        {
            at=feet;size=opacity=0;float u=age/DustLife;if(!(u>=0&&u<1)||n<=0)return false;
            float a=MeleeMath.TAU*k/n+Hash(seed,k+5)*.5f,r=(.25f+(.9f+.5f*strength)*Out(u))*(.85f+.3f*Hash(seed,k+9));
            at=new Vector3(feet.X+(float)Math.Cos(a)*r,floor+.12f+.18f*Out(u),feet.Z+(float)Math.Sin(a)*r);
            size=.3f+.35f*Out(u)*(.7f+.3f*strength);opacity=.5f*(float)Math.Pow(1-u,1.2)*Math.Min(1,age/.03f)*(.6f+.4f*strength);return true;
        }
    }
}
