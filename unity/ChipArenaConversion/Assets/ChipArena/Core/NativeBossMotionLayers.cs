using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Breath and idle life: pure functions of the presentation clock and a seed, so every peer breathes in step. The breath is
    /// in over 40% of the cycle and out over the rest, slope-continuous everywhere (-1 out .. 1 in), its depth drifting on noise;
    /// the idle sway is smooth noise per channel.
    /// </summary>
    public static class BossBreath
    {
        public static float Sample(double time,float rate,uint seed)
        {
            double cycles=time*Math.Max(.01f,rate)+BossMath.Hash(seed,1);float u=(float)(cycles-Math.Floor(cycles));
            float v=u<.4f?-(float)Math.Cos(Math.PI*u/.4f):(float)Math.Cos(Math.PI*(u-.4f)/.6f);
            return v*(1+.25f*BossMath.Noise(seed^0x5bd1e995u,(float)(cycles%4096.0)*.37f));
        }
        /// <summary>Channel `channel`'s idle drift (-1..1) at `rate` Hz of change.</summary>
        public static float Idle(double time,uint seed,int channel,float rate)=>BossMath.Noise(seed+(uint)channel*0x632BE5ABu,(float)((time*rate)%65536.0));
    }

    /// <summary>
    /// Look-at and aim: the yaw and pitch (rad, body frame; pitch + looks down) toward a target, within limits, lagging on springs
    /// with a rate cap (the head snaps round, but never teleports). A target past the limit holds the look there; one well
    /// behind (a quarter radian past, fading over a half) lets it ease back to centre.
    /// Spread the result down the chain (spine, chest, neck, head) with the rig's own shares.
    /// </summary>
    public sealed class BossLookAt
    {
        public float MaxYaw=1.1f,MaxUp=.55f,MaxDown=.6f,HalfLife=.16f,MaxRate=4f,WeightHalfLife=.25f;
        public float Yaw,Pitch,Weight;private BossSpring yaw,pitch,weight;
        public void Reset(){yaw.Reset(0);pitch.Reset(0);weight.Reset(0);Yaw=Pitch=Weight=0;}
        public void Step(Vector3 eye,float bodyYaw,bool hasTarget,Vector3 target,float dt)
        {
            float wy=0,wp=0,ww=0;
            if(hasTarget)
            {
                var d=target-eye;float flat=BossMath.FlatLength(d),rel=BossMath.WrapPi(BossMath.YawOf(d)-bodyYaw);
                wy=BossMath.Clamp(rel,-MaxYaw,MaxYaw);wp=BossMath.Clamp((float)Math.Atan2(-d.Y,Math.Max(.05f,flat)),-MaxUp,MaxDown);
                ww=1-BossMath.Smooth((Math.Abs(rel)-MaxYaw-.25f)/.5f);
            }
            yaw.Step(wy,HalfLife,dt);pitch.Step(wp,HalfLife,dt);weight.Step(ww,WeightHalfLife,dt);
            yaw.Velocity=BossMath.Clamp(yaw.Velocity,-MaxRate,MaxRate);pitch.Velocity=BossMath.Clamp(pitch.Velocity,-MaxRate,MaxRate);
            float w=BossMath.Clamp01(weight.Value);Yaw=yaw.Value*w;Pitch=pitch.Value*w;Weight=w;
        }
    }

    /// <summary>
    /// Hit flinches: each blow (its authority time, the direction it pushes, its size) is an impulse response, t/r e^(1 - t/r)
    /// (peak 1 at `Rise`), with a small damped wobble after; the last few blows add. A pure function of the time and the blows'
    /// records, so every peer flinches alike. Pitch + is pushed forward, bank + toward the right, the shift (body frame) along.
    /// </summary>
    public sealed class BossFlinch
    {
        private struct Blow{public double At;public float X,Z,Size;}
        private readonly Blow[] blows=new Blow[6];private int next;
        public float Rise=.07f,Wobble=.22f,WobbleHz=2.4f,PitchGain=.22f,BankGain=.16f,ShiftGain=.12f,Span=1.6f;
        public BossFlinch(){Clear();}
        public void Clear(){for(int i=0;i<blows.Length;i++)blows[i].At=double.NaN;next=0;}
        /// <summary>A blow at `at` pushing along `push` (world; from the attacker into the boss) with `size` (about 1 a heavy hit),
        /// on a body facing `bodyYaw`. The same blow twice (a replayed event) is ignored.</summary>
        public void Add(double at,Vector3 push,float size,float bodyYaw)
        {
            for(int i=0;i<blows.Length;i++)if(blows[i].At==at&&blows[i].Size==size)return;
            var flat=BossMath.Flat(push);float l=flat.Length();if(l>1e-5f)flat/=l;
            blows[next]=new Blow{At=at,X=Vector3.Dot(flat,BossMath.Right(bodyYaw)),Z=Vector3.Dot(flat,BossMath.Forward(bodyYaw)),Size=size};next=(next+1)%blows.Length;
        }
        /// <summary>The flinch at `time`; `delay` samples it later on a part that lags (the head, the arms).</summary>
        public void Sample(double time,out float pitch,out float bank,out Vector3 shift,float delay=0)
        {
            pitch=bank=0;shift=Vector3.Zero;
            for(int i=0;i<blows.Length;i++)
            {
                var b=blows[i];if(double.IsNaN(b.At))continue;float t=(float)(time-b.At)-delay;if(t<0||t>Span)continue;
                float r=t/Rise,a=r*(float)Math.Exp(1-r)+Wobble*(float)Math.Exp(-t*4)*(float)Math.Sin(BossMath.Tau*WobbleHz*t)*BossMath.Smooth(r),k=a*b.Size;
                pitch+=b.Z*k*PitchGain;bank+=b.X*k*BankGain;shift+=new Vector3(b.X,0,b.Z)*(k*ShiftGain);
            }
        }
    }

    /// <summary>
    /// A stagger: the hips thrown along the blow at once, held while the feet catch up (the gait steps after them: the shift moves
    /// the body off its planted feet), then a weighty return that overshoots a little and settles; the trunk pitches with it, the
    /// knees give. A pure function of the stagger's start, direction, size and length.
    /// </summary>
    public sealed class BossStagger
    {
        public double At=double.NaN;public float X,Z,Size,Seconds=1.1f;
        public float Throw=.09f,Shift=.55f,PitchGain=.32f,BankGain=.22f,Crouch=.16f;
        public void Clear(){At=double.NaN;}
        public void Start(double at,Vector3 push,float size,float seconds,float bodyYaw)
        {
            if(At==at)return;var flat=BossMath.Flat(push);float l=flat.Length();if(l>1e-5f)flat/=l;
            At=at;Size=size;Seconds=Math.Max(.3f,seconds);X=Vector3.Dot(flat,BossMath.Right(bodyYaw));Z=Vector3.Dot(flat,BossMath.Forward(bodyYaw));
        }
        /// <summary>The stagger at `time` (false, all zero, when none is on): the hips' shift (body frame, m), the trunk's pitch and
        /// bank (rad), the knees' crouch (m), and its weight (0..1).</summary>
        public bool Sample(double time,out Vector3 shift,out float pitch,out float bank,out float crouch,out float weight)
        {
            shift=Vector3.Zero;pitch=bank=crouch=weight=0;if(double.IsNaN(At))return false;float t=(float)(time-At);if(t<0||t>=Seconds)return false;
            float hold=.38f*Seconds,thrown=1-(float)Math.Exp(-t/Throw),back=t<hold?0:BossEasing.Eval(BossEase.Settle,(t-hold)/(Seconds-hold),1.3f);
            float amt=Size*thrown*(1-back);shift=new Vector3(X,0,Z)*(amt*Shift);
            // The trunk whips against the push first (the hips go, the shoulders lag), then leans with it while the feet catch up.
            float lag=(float)Math.Exp(-t/.12f)*BossMath.Smooth(t/.03f);
            pitch=Z*PitchGain*(amt-.5f*Size*lag);bank=X*BankGain*(amt-.5f*Size*lag);
            crouch=Crouch*Size*(float)Math.Sin(Math.PI*BossMath.Clamp01(t/(Seconds*.8f)));
            weight=BossMath.Smoother(t/.05f)*(1-BossMath.Smoother((t-(Seconds-.2f))/.2f));return true;
        }
    }
}
