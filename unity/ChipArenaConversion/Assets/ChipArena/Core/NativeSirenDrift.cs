using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Siren's floating locomotion and live layers (siren/motion.ts animateSirenRig), added over her baked stance or move
    /// clip (NativeSourceMotion.Pilot, baked standing still with the aim level): she leans into the drift (forward and lateral
    /// velocity eased at rate 6), her arms and legs trail it, her knees tuck while airborne with the hover dropped, the
    /// chest (in ranged mode) and head follow the aim pitch, a hit snaps her back from the blow and the guard crosses her
    /// claws in front of her face. Without it she glided in her standing pose. Additions are three.js XYZ Euler offsets, as
    /// the source adds them to the stance; during a move the arms keep the move's own pose as the move weight rises
    /// (movePose sets the striking arm outright). Presentation only, replicated Actor fields only, no allocation.
    /// </summary>
    public sealed class NativeSirenDrift
    {
        private float forward,lateral,last=-100,modeAt=-100;private bool has,wasRanged;private int id=int.MinValue;
        /// <summary>The eased forward drift over 6.8 m/s (-1..1), as the last Apply left it (motion.ts sm0.fwd / 6.8).</summary>
        public float Lean=>MeleeMath.Clamp(forward/6.8f,-1,1);
        private static float Span(float t,float a,float b)=>MeleeMath.Clamp((t-a)/Math.Max(1e-4f,b-a),0,1);
        private static float Sm(float x){x=MeleeMath.Clamp(x,0,1);return x*x*(3-2*x);}
        /// <summary>movePose's weight: in over .07 s from the move's start, out over its last .18 s.</summary>
        public static float MoveWeight(Actor a,float time)=>string.IsNullOrEmpty(a.SourceAction)||time>=a.SourceUntil?0:Sm(Span(time,a.SourceAt,a.SourceAt+.07f))*(1-Sm(Span(time,a.SourceUntil-.18f,a.SourceUntil)));
        private static void Add(CapturedRig.Pose p,int bone,float x,float y=0,float z=0)
        {
            if(x==0&&y==0&&z==0)return;Vector3 e=MeleeMath.ToEuler(p.Rotation[bone]);p.Rotation[bone]=MeleeMath.Euler(e.X+x,e.Y+y,e.Z+z);
        }
        private static void Set(CapturedRig.Pose p,int bone,float x,float y,float z)=>p.Rotation[bone]=MeleeMath.Euler(x,y,z);
        public void Apply(CapturedRig.Pose p,Actor a,float time)
        {
            const float D=(float)Math.PI/180;
            float yaw=a.Yaw*D,ahead=a.Velocity.x*(float)Math.Sin(yaw)+a.Velocity.z*(float)Math.Cos(yaw),side=a.Velocity.x*(float)Math.Cos(yaw)-a.Velocity.z*(float)Math.Sin(yaw);
            bool ranged=a.SourceRanged;
            if(!has||a.Id!=id||time<last-MeleeGait.REWIND||time-last>.35f){has=true;id=a.Id;forward=ahead;lateral=side;last=time;wasRanged=ranged;modeAt=-100;}
            float dt=MeleeMath.Clamp(time-last,0,.05f);last=Math.Max(last,time);
            float k=1-(float)Math.Exp(-dt*6);forward+=(ahead-forward)*k;lateral+=(side-lateral)*k;
            // The claws withdraw or the palms ignite over the swap (SIREN_TIMING.swap .35 s) from the frame the flag flipped.
            if(ranged!=wasRanged){wasRanged=ranged;modeAt=time;}
            float swap=Sm(Span(time,modeAt,modeAt+.35f)),rangedW=ranged?swap:1-swap;
            float f=MeleeMath.Clamp(forward/6.8f,-1,1),l=MeleeMath.Clamp(lateral/6.8f,-1,1),speed=(float)Math.Sqrt(f*f+l*l),arms=1-MoveWeight(a,time),pitch=a.Pitch*D;
            // Floating locomotion: lean into the drift, arms and legs trail, no steps.
            Add(p,CapturedRig.Body,.17f*f,0,-.18f*l);Add(p,CapturedRig.Spine,.06f*f);Add(p,CapturedRig.Head,-.14f*f);
            Add(p,CapturedRig.LArm,.5f*f*(1-rangedW*.6f)*arms);Add(p,CapturedRig.RArm,.5f*f*(1-rangedW*.6f)*arms);
            Add(p,CapturedRig.LHip,.55f*f+.15f*speed,0,.12f*l);Add(p,CapturedRig.RHip,.48f*f+.15f*speed,0,.12f*l);
            float lk=.2f*speed,rk=.25f*speed;if(!a.Grounded){lk+=.35f;rk+=.3f;p.Position[CapturedRig.Body].Y-=.06f;}
            Add(p,CapturedRig.LKnee,lk);Add(p,CapturedRig.RKnee,rk);
            // Ranged aim follows the camera pitch a little.
            Add(p,CapturedRig.Chest,-pitch*.28f*rangedW);Add(p,CapturedRig.Head,-pitch*.22f);
            // Hit flinch: snap back from the blow.
            if(a.ReactionAt>-90)
            {
                float hit=Span(time,a.ReactionAt,a.ReactionAt+.35f),flinch=hit<1?(float)Math.Sin(Math.PI*hit)*(1-hit):0;
                if(flinch>0){Add(p,CapturedRig.Chest,-.35f*flinch);Add(p,CapturedRig.Head,-.4f*flinch,0,.2f*flinch);Add(p,CapturedRig.LArm,.3f*flinch,0,.2f*flinch);Add(p,CapturedRig.RArm,.3f*flinch,0,-.2f*flinch);}
            }
            // Guarding: claws crossed in front of the face.
            if(time<a.BlockUntil)
            {
                Set(p,CapturedRig.RArm,-1.35f,0,.55f);Set(p,CapturedRig.RElbow,-1.4f,0,0);Set(p,CapturedRig.RHand,0,0,0);
                Set(p,CapturedRig.LArm,-1.3f,0,-.45f);Set(p,CapturedRig.LElbow,-1.45f,0,0);Set(p,CapturedRig.LHand,0,0,0);
                Add(p,CapturedRig.Head,.2f,0,-.3f);
            }
        }
    }
}
