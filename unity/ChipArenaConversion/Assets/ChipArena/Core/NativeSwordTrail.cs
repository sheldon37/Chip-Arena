using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The sword's run on the visual body (owner 2026-10-27: "Fix run for sword as well, it looks not very styled lol";
    // Runtime NativeVisualMotionPresentation.cs). The pack has no run of its own for a one-handed sword: the set runs on the
    // pack's armed run, which pumps the fist before the chest with the blade upright like a baton. Running ahead, the sword arm
    // now sweeps back and out from the shoulder, nearly straight, keeping a little of the run's own swing in time with the
    // stride; the fist turns so the blade trails out and back past the hip, a little low (the fist's twist goes to the shoulder
    // and the forearm, the wrist only bends, and no further than MaxBend); the chest leans into the run, more at a sprint, the
    // head kept up. A walk, a strafe or a back-pedal keeps the armed gait, and the carry goes as any strike, block, roll, jump
    // or reaction comes in (only the locomotion blend carries it).
    // Presentation only: the game's movement, swings, hit timing, reach and every rule are unchanged.
    //
    // Directions are in the body's own frame, Unity's: +x right, +y up, +z ahead.
    public static class NativeSwordTrail
    {
        // The arm: degrees behind straight down and out to the right, its reach (of the arm's length: the elbow a little soft),
        // and the share of the clip's own arm swing kept (about its eased mean over SwingMean seconds, within SwingLimit).
        public const float ArmBack=35f, ArmOut=15f, Reach=.94f, Swing=.3f, SwingLimit=15f, SwingMean=.6f;
        // The fist: the wrist's largest bend (degrees) and the share of its twist the shoulder takes (the forearm the rest).
        public const float MaxBend=35f, ShoulderTwist=.55f;
        // The chest's lean into the run (over the armed run's own) and its sprint's extra (degrees), the head's share kept up, and the
        // carry's ease (s).
        public const float Lean=4f, SprintLean=4f, HeadKeep=.7f, Ease=.15f;
        /// <summary>The blade's way while trailing: out to the right and back, a little low.</summary>
        public static readonly Vector3 Blade=Vector3.Normalize(new Vector3(.62f,-.28f,-.73f));

        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static float Smooth(float t)=>t*t*(3-2*t);

        /// <summary>The carry's share for a weapon spec at the body's travel (m/s, its own frame): none for any weapon but the
        /// sword, none at a walk, full from the run; only heading ahead (none strafing or back-pedalling).</summary>
        public static float Share(string spec,float vx,float vz)
        {
            if(spec!="sword")return 0;
            float speed=(float)Math.Sqrt(vx*vx+vz*vz);if(speed<1e-3f)return 0;
            float run=Clamp01((speed-NativeVisualStride.WalkTop)/(NativeVisualStride.RunFrom-NativeVisualStride.WalkTop));
            float ahead=Clamp01((vz/speed-.4f)/.4f);
            return Smooth(run)*Smooth(ahead);
        }
        /// <summary>The chest's lean (degrees) at a travel speed (m/s): Lean, and SprintLean more over the sprint's ramp.</summary>
        public static float LeanAt(float speed)=>Lean+SprintLean*Smooth(Clamp01((speed-NativeVisualStride.SprintFrom)/(NativeVisualStride.SprintTop-NativeVisualStride.SprintFrom)));

        /// <summary>The way from the shoulder to the trailing fist, `swing` degrees further back (clamped to SwingLimit).</summary>
        public static Vector3 Arm(float swing)
        {
            float back=(ArmBack+Math.Max(-SwingLimit,Math.Min(SwingLimit,swing)))*(float)Math.PI/180,side=ArmOut*(float)Math.PI/180;
            return Vector3.Normalize(new Vector3((float)Math.Sin(side),-(float)(Math.Cos(side)*Math.Cos(back)),-(float)(Math.Cos(side)*Math.Sin(back))));
        }
        /// <summary>How far behind straight down (degrees) a shoulder-to-fist offset swings, in the body's side view.</summary>
        public static float Pitch(Vector3 arm)=>(float)(Math.Atan2(-arm.Z,-arm.Y)*180/Math.PI);

        /// <summary>The fist's turn (world) that lays the blade along `blade` with the wrist as straight as the grip allows.
        /// `bladeLocal` and `knuckleLocal` are the blade's way and the wrist-to-knuckles way in the hand's own frame; `forearm`
        /// the elbow-to-wrist way. The knuckles go as near the forearm's line as the grip's angle between blade and knuckles
        /// lets them; past `maxBend` degrees of wrist bend the blade gives way toward the straight wrist instead.</summary>
        public static Quaternion Hand(Vector3 bladeLocal,Vector3 knuckleLocal,Vector3 forearm,Vector3 blade,float maxBend)
        {
            bladeLocal=Vector3.Normalize(bladeLocal);knuckleLocal=Vector3.Normalize(knuckleLocal);forearm=Vector3.Normalize(forearm);blade=Vector3.Normalize(blade);
            float grip=Angle(bladeLocal,knuckleLocal);
            // The knuckles' way at the grip's angle from the blade, toward the forearm's line.
            var across=forearm-Vector3.Dot(forearm,blade)*blade;
            if(across.LengthSquared()<1e-8f)across=Perpendicular(blade);
            across=Vector3.Normalize(across);
            var knuckles=(float)Math.Cos(grip)*blade+(float)Math.Sin(grip)*across;
            // Past the wrist's bend: the whole fist turned back toward the straight wrist by the excess.
            float bend=Angle(knuckles,forearm)*180/(float)Math.PI;
            if(bend>maxBend)
            {
                var axis=Vector3.Cross(knuckles,forearm);
                if(axis.LengthSquared()>1e-10f)
                {
                    var back=Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis),(bend-maxBend)*(float)Math.PI/180);
                    blade=Vector3.Transform(blade,back);knuckles=Vector3.Transform(knuckles,back);
                }
            }
            return Quaternion.Normalize(Basis(blade,knuckles)*Quaternion.Inverse(Basis(bladeLocal,knuckleLocal)));
        }
        /// <summary>The twist of `turn` about `axis` (radians, -pi..pi; `axis` a unit vector in the turn's own frame).</summary>
        public static float Twist(Quaternion turn,Vector3 axis)
        {
            float along=turn.X*axis.X+turn.Y*axis.Y+turn.Z*axis.Z;
            float t=2*(float)Math.Atan2(along,turn.W);
            if(t>Math.PI)t-=2*(float)Math.PI;else if(t<-Math.PI)t+=2*(float)Math.PI;
            return t;
        }
        /// <summary>The angle between two unit vectors (radians).</summary>
        public static float Angle(Vector3 a,Vector3 b)=>(float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(a,b))));
        // The turn carrying +z onto `forward` and +y onto `up` (made square to it), as Unity's LookRotation.
        private static Quaternion Basis(Vector3 forward,Vector3 up)
        {
            forward=Vector3.Normalize(forward);up-=Vector3.Dot(up,forward)*forward;
            up=up.LengthSquared()<1e-10f?Perpendicular(forward):Vector3.Normalize(up);
            var right=Vector3.Cross(up,forward);
            return Quaternion.CreateFromRotationMatrix(new Matrix4x4(right.X,right.Y,right.Z,0,up.X,up.Y,up.Z,0,forward.X,forward.Y,forward.Z,0,0,0,0,1));
        }
        private static Vector3 Perpendicular(Vector3 v)=>Vector3.Normalize(Vector3.Cross(v,Math.Abs(v.Y)<.9f?Vector3.UnitY:Vector3.UnitX));
    }
}
