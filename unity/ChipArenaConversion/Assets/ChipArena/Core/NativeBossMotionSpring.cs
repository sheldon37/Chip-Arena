using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A critically damped spring on a half-life, solved exactly (no integration error: ten 1/144 s steps land where one
    /// 10/144 s step does, for a still goal). Half-life: a spring at rest is halfway to a still goal after `halfLife` seconds; it
    /// never overshoots a still goal from rest. The goal-velocity overload follows a moving goal with no steady lag (the error
    /// decays as if the goal stood still), which is how a root follows a target that is itself moving.
    /// </summary>
    public struct BossSpring
    {
        public float Value,Velocity;
        /// <summary>c with (1 + c) e^-c = 1/2: the decay rate of a critically damped spring whose step response halves in one unit.</summary>
        public const float HalfLifeConstant=1.6783469900166605f;
        public static float Rate(float halfLife)=>HalfLifeConstant/Math.Max(1e-5f,halfLife);
        public void Reset(float value){Value=value;Velocity=0;}
        public float Step(float goal,float halfLife,float dt)=>Step(goal,0,halfLife,dt);
        /// <summary>Steps toward a goal moving at `goalVelocity` (the goal given is where it is at the start of the step).</summary>
        public float Step(float goal,float goalVelocity,float halfLife,float dt)
        {
            if(!(dt>0))return Value;float y=Rate(halfLife),j0=Value-goal,v=Velocity-goalVelocity,j1=v+j0*y,e=(float)Math.Exp(-y*dt);
            Value=e*(j0+j1*dt)+goal+goalVelocity*dt;Velocity=e*(v-j1*y*dt)+goalVelocity;return Value;
        }
    }

    /// <summary><see cref="BossSpring"/> on a vector.</summary>
    public struct BossSpring3
    {
        public Vector3 Value,Velocity;
        public void Reset(Vector3 value){Value=value;Velocity=Vector3.Zero;}
        public Vector3 Step(Vector3 goal,float halfLife,float dt)=>Step(goal,Vector3.Zero,halfLife,dt);
        public Vector3 Step(Vector3 goal,Vector3 goalVelocity,float halfLife,float dt)
        {
            if(!(dt>0))return Value;float y=BossSpring.Rate(halfLife),e=(float)Math.Exp(-y*dt);var j0=Value-goal;var v=Velocity-goalVelocity;var j1=v+j0*y;
            Value=e*(j0+j1*dt)+goal+goalVelocity*dt;Velocity=e*(v-j1*y*dt)+goalVelocity;return Value;
        }
    }

    /// <summary><see cref="BossSpring"/> on a rotation (the error and the angular velocity live in the rotation's log, world axes).</summary>
    public struct BossSpringQ
    {
        public Quaternion Value;public Vector3 Velocity;
        public void Reset(Quaternion value){Value=Quaternion.Normalize(value);Velocity=Vector3.Zero;}
        public Quaternion Step(Quaternion goal,float halfLife,float dt)
        {
            if(!(dt>0))return Value;if(Value.LengthSquared()<.5f)Value=Quaternion.Identity;
            float y=BossSpring.Rate(halfLife),e=(float)Math.Exp(-y*dt);var j0=BossMath.Log(Value*BossMath.Inverse(goal));var j1=Velocity+j0*y;
            Value=Quaternion.Normalize(BossMath.Exp(e*(j0+j1*dt))*goal);Velocity=e*(Velocity-j1*y*dt);return Value;
        }
    }

    /// <summary>
    /// A facing that turns toward its goal the short way, never faster than `maxRate` (rad/s), speeding up and braking at no
    /// more than `maxAccel` (rad/s^2), and stopping on the goal without overshoot: a heavy body winds into a turn and settles out
    /// of it instead of snapping round at a constant rate. Small errors close on a critically damped spring (`halfLife`).
    /// </summary>
    public struct BossTurn
    {
        /// <summary>The facing (rad, unwrapped: it counts whole turns) and its rate (rad/s).</summary>
        public float Yaw,Rate;
        public void Reset(float yaw){Yaw=yaw;Rate=0;}
        public float Step(float goal,float halfLife,float maxRate,float maxAccel,float dt)
        {
            if(!(dt>0))return Yaw;float err=BossMath.WrapPi(goal-Yaw);
            // A goal straight behind keeps the way the body is already turning (no flip-flop on the half turn).
            if(Math.Abs(err)>BossMath.Pi-.15f&&Rate!=0&&Math.Sign(err)!=Math.Sign(Rate))err+=Math.Sign(Rate)*BossMath.Tau;
            // The fastest rate that can still brake to rest on the goal (v^2 = 2 a d, with a margin), the cap, and the spring's
            // own approach for the last few degrees (its rate at this error), whichever is slowest.
            float y=BossSpring.Rate(halfLife),brake=(float)Math.Sqrt(2*maxAccel*.8f*Math.Abs(err)),near=Math.Abs(err)*y*.6f;
            float want=Math.Sign(err)*Math.Min(maxRate,Math.Min(brake,near));
            // Speeding up, the rate eases toward that (a short spring on the rate: a gentle wind into the turn); slowing down it
            // follows the braking curve exactly (a lag there would arrive with speed left). Both within the acceleration limit.
            float k=Math.Abs(want)<Math.Abs(Rate)&&Math.Sign(want)!=-Math.Sign(Rate)?1:1-(float)Math.Exp(-dt*y*2.5f);
            float dv=BossMath.Clamp((want-Rate)*k,-maxAccel*dt,maxAccel*dt),next=Rate+dv;
            float step=(Rate+next)*.5f*dt;
            // Never step past the goal (the braking margin makes this rare): land on it and stop.
            if(err!=0&&Math.Sign(step)==Math.Sign(err)&&Math.Abs(step)>Math.Abs(err)){step=err;next=0;}
            Yaw+=step;Rate=next;return Yaw;
        }
    }

    /// <summary>
    /// Second-order dynamics (frequency `f` Hz, damping `zeta`, response `r`): a value that follows its input like a mass on a
    /// spring. zeta below 1 overshoots and wobbles to rest (weight, follow-through); zeta 1 settles without; r above 1 overshoots
    /// the input's start, below 0 anticipates (moves the wrong way first). Stepped on fixed sub-steps of at most 1/240 s so it
    /// is stable at any frame rate. <see cref="Configure"/> before use.
    /// </summary>
    public struct BossSecondOrder
    {
        public float Value,Velocity;private float previous,k1,k2,k3;private bool ready;
        public const float MaxStep=1/240f;
        public void Configure(float f,float zeta,float r)
        {
            f=Math.Max(.01f,f);k1=zeta/(BossMath.Pi*f);k2=1/((BossMath.Tau*f)*(BossMath.Tau*f));k3=r*zeta/(BossMath.Tau*f);
        }
        public void Reset(float value){Value=previous=value;Velocity=0;ready=true;}
        /// <summary>Steps toward input `x` over `dt` (its velocity estimated from the last input).</summary>
        public float Step(float x,float dt)
        {
            if(!ready)Reset(x);if(!(dt>0))return Value;float xd=(x-previous)/dt;return Step(x,xd,dt);
        }
        /// <summary>Steps with the input's velocity given.</summary>
        public float Step(float x,float xd,float dt)
        {
            if(!ready)Reset(x);if(!(dt>0))return Value;
            int n=(int)Math.Ceiling(dt/MaxStep-1e-4f);if(n<1)n=1;float h=dt/n,x0=previous;
            float k2s=Math.Max(k2,Math.Max(h*h/2+h*k1/2,h*k1));
            for(int i=0;i<n;i++)
            {
                float xi=x0+(x-x0)*(i+1)/n;
                Value+=h*Velocity;Velocity+=h*(xi+k3*xd-Value-k1*Velocity)/k2s;
            }
            previous=x;return Value;
        }
        /// <summary>Adds an impulse to the velocity (a footfall's dip, a blow).</summary>
        public void Kick(float velocity){Velocity+=velocity;}
    }

    /// <summary><see cref="BossSecondOrder"/> on a vector.</summary>
    public struct BossSecondOrder3
    {
        public Vector3 Value,Velocity;private Vector3 previous;private float k1,k2,k3;private bool ready;
        public void Configure(float f,float zeta,float r)
        {
            f=Math.Max(.01f,f);k1=zeta/(BossMath.Pi*f);k2=1/((BossMath.Tau*f)*(BossMath.Tau*f));k3=r*zeta/(BossMath.Tau*f);
        }
        public void Reset(Vector3 value){Value=previous=value;Velocity=Vector3.Zero;ready=true;}
        public Vector3 Step(Vector3 x,float dt){if(!ready)Reset(x);if(!(dt>0))return Value;return Step(x,(x-previous)/dt,dt);}
        public Vector3 Step(Vector3 x,Vector3 xd,float dt)
        {
            if(!ready)Reset(x);if(!(dt>0))return Value;
            int n=(int)Math.Ceiling(dt/BossSecondOrder.MaxStep-1e-4f);if(n<1)n=1;float h=dt/n;var x0=previous;
            float k2s=Math.Max(k2,Math.Max(h*h/2+h*k1/2,h*k1));
            for(int i=0;i<n;i++)
            {
                var xi=Vector3.Lerp(x0,x,(i+1)/(float)n);
                Value+=h*Velocity;Velocity+=h*(xi+k3*xd-Value-k1*Velocity)/k2s;
            }
            previous=x;return Value;
        }
        public void Kick(Vector3 velocity){Velocity+=velocity;}
    }
}
