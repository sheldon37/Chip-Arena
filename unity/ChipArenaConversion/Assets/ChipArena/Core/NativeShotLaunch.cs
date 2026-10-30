using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Where a gun's round is drawn as it leaves the barrel (owner 2026-10-15: "Sometimes the bullet doesn't come from the tip of
    /// barrel"). The simulation fires every round from one fixed body point (NativeArena.Fire: the eye .3 m over the body
    /// centre, .2 m right and .48 m ahead along the yaw, whatever the pitch, the pose or the camera), which sits 0.4-1.3 m
    /// from the drawn muzzle: behind it in the chest, and well below or above it when aiming down or up. The round is drawn
    /// instead from the muzzle the presentation posed (the third-person rig's, or the first-person view weapon's mapped into
    /// the match camera) and joins its simulated path within <see cref="Converge"/> m, leaving the barrel along it, so
    /// everything it meets downrange (hits, impacts, the sounds) is exactly where the simulation puts it.
    /// <para>The offset is frozen the first time the round is seen (it left the barrel then), from replicated data alone: the
    /// round's Shot event (its firer, time and the simulation's start point), its velocity and the drawn muzzle. Distance
    /// along the path, not time, drives it, so a guest or a killcam that first sees the round downrange draws it there. A
    /// round that turns away from its first heading (a ricochet or a reflect) drops it. Cosmetic only: the authority, hit
    /// detection, the network frame and the projectile simulation never read it.</para>
    /// </summary>
    public static class NativeShotLaunch
    {
        /// <summary>The metres past the muzzle over which a drawn round joins its simulated path.</summary>
        public const double Converge=4.5;
        /// <summary>How far (m) the round's start may sit off its Shot event's start before the event is not trusted as its own.</summary>
        public const double Agree=.35;
        /// <summary>Rounds first seen older than this (s) are left on their path: they are long gone from the muzzle.</summary>
        public const double MaxAge=.25;
        /// <summary>The bullets that leave a drawn gun barrel: the pistol, rifle and machine gun's primaries.</summary>
        public static bool Gun(in NativeShotView v)=>v.Kind==NativeShotKind.Bullet&&v.Class=="ranged"&&!v.Siren&&!v.Bowling&&NativeWeaponDressing.Gun(v.Spec);
        /// <summary>One round's launch: the simulation's start, its first heading, the muzzle's lead along it and the offset joined in.</summary>
        public struct State
        {
            public bool Live;public double OX,OY,OZ,DX,DY,DZ,FX,FY,FZ,Lead;
        }
        /// <summary>
        /// The launch of a round that starts at o (the simulation's start) heading v, drawn from muzzle m. False (nothing to
        /// draw differently) for a still round or a muzzle on the start.
        /// </summary>
        public static bool Begin(double mx,double my,double mz,double ox,double oy,double oz,double vx,double vy,double vz,out State s)
        {
            s=default;double speed=Math.Sqrt(vx*vx+vy*vy+vz*vz);if(!(speed>1e-3)||double.IsNaN(mx+my+mz+ox+oy+oz))return false;
            double dx=vx/speed,dy=vy/speed,dz=vz/speed,ex=mx-ox,ey=my-oy,ez=mz-oz;if(ex*ex+ey*ey+ez*ez<1e-8)return false;
            // The muzzle's distance along the path: a round still behind it is held at the crown; the rest of the offset is
            // brought in past it. A muzzle behind the start (a lowered carry) keeps its whole offset to bring in.
            double lead=ex*dx+ey*dy+ez*dz;if(lead<0)lead=0;
            s=new State{Live=true,OX=ox,OY=oy,OZ=oz,DX=dx,DY=dy,DZ=dz,FX=ex-dx*lead,FY=ey-dy*lead,FZ=ez-dz*lead,Lead=lead};return true;
        }
        /// <summary>The share of the offset still drawn `past` metres beyond the muzzle: 1 at the crown, easing to 0 at Converge.</summary>
        public static double Share(double past){double x=Math.Max(0,Math.Min(1,past/Converge));return 1-x*x*(3-2*x);}
        private static double Slope(double past){double x=past/Converge;return x<=0||x>=1?0:-(6*x-6*x*x)/Converge;}
        /// <summary>
        /// Moves v (the round where the simulation has it, on the presentation clock) onto its drawn path, turns its velocity
        /// along that path and sets Launched / Flown (the drawn distance past the muzzle). Drops a launch once the round turns away.
        /// </summary>
        public static void Apply(ref State s,ref NativeShotView v)
        {
            if(!s.Live)return;double speed=Math.Sqrt(v.VX*v.VX+v.VY*v.VY+v.VZ*v.VZ);
            if(speed>1e-3&&(v.VX*s.DX+v.VY*s.DY+v.VZ*s.DZ)/speed<.9){s.Live=false;return;}
            double px=v.X-s.OX,py=v.Y-s.OY,pz=v.Z-s.OZ,along=px*s.DX+py*s.DY+pz*s.DZ,past=along-s.Lead;v.Launched=true;
            if(past<=0)
            {
                // Still in the barrel: held at the crown.
                v.X=s.OX+s.DX*s.Lead+s.FX;v.Y=s.OY+s.DY*s.Lead+s.FY;v.Z=s.OZ+s.DZ*s.Lead+s.FZ;v.Flown=0;return;
            }
            v.Flown=past;if(past>=Converge)return;double k=Share(past),turn=Slope(past)*speed;
            v.X+=s.FX*k;v.Y+=s.FY*k;v.Z+=s.FZ*k;v.VX+=s.FX*turn;v.VY+=s.FY*turn;v.VZ+=s.FZ*turn;
        }
    }
}
