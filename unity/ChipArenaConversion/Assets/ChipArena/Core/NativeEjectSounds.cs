using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The eject's sounds (docs/EXPEDITION-OVERHAUL.md W3 and W12: the klaxon, the hatches, the drop whoosh, the tube rush, the seat
    /// thunk, the harness clamp, the roof hatch's seal, the release and the white rush): a Claude presentation table beside Astra's
    /// machine sounds (NativeDeploySounds stays as it is). Placeholders synthesized here, deterministic PCM like the machines', for
    /// the owner's listening pass before the ElevenLabs batch (N5). The moments come from the eject's beats (NativeDeployEject) on
    /// the viewer's own pad, heard exactly once across frames (a late joiner hears nothing already past). Presentation sound keys
    /// only: no gameplay event, rule or wire field. Runtime/NativeEjectAudio.cs plays them through the effects voices.
    /// </summary>
    public static class NativeEjectSounds
    {
        public const int SampleRate=48000;
        public enum Kind{Klaxon,Hatch,Whoosh,Tube,Seat,Harness,Seal,Release,Rush}
        public const int Count=9,Limit=12;
        private static readonly string[] keys={"eject.klaxon","eject.hatch","eject.whoosh","eject.tube","eject.seat","eject.harness","eject.seal","eject.release","eject.rush"};
        private static readonly double[] durations={1.0,.46,.62,1.05,.4,.42,.7,.85,.95};
        private static readonly float[] gains={.5f,.55f,.6f,.5f,.75f,.6f,.8f,.7f,.55f};
        public static string Key(Kind k)=>keys[(int)k];
        public static double Duration(int i)=>durations[i];
        public static float Gain(Kind k)=>gains[(int)k];
        /// <summary>One moment heard: what it is and its pan (-1 left .. 1 right; the eject's are near the middle).</summary>
        public struct Cue{public Kind Kind;public float Pan;}
        /// <summary>When each of the eject's sounds plays for the viewer on pad `mine` (s after Deploy; NaN: not in this cut).</summary>
        public static float At(Kind k,int mine,NativeDeployEject.Cut cut)
        {
            switch(k)
            {
                case Kind.Klaxon:return .15f;
                case Kind.Hatch:return NativeDeployPresentation.HatchAt;
                case Kind.Whoosh:return NativeDeployEject.DropStart(mine);
                case Kind.Tube:return NativeDeployEject.DropStart(mine)+NativeDeployEject.TubeLead;
                case Kind.Seat:return NativeDeployEject.LandAt(mine);
                case Kind.Harness:return NativeDeployEject.LandAt(mine)+NativeDeployEject.HarnessAt+NativeDeployEject.HarnessSeconds;
                case Kind.Seal:return NativeDeployEject.LandAt(mine)+NativeDeployEject.SealAt;
                case Kind.Release:return cut==NativeDeployEject.Cut.Full?NativeDeployEject.ReleaseOf(mine):float.NaN;
                default:return cut==NativeDeployEject.Cut.Short?NativeDeployEject.ShortRushAt:NativeDeployEject.RushAt;
            }
        }
        /// <summary>
        /// The moments between the last frame's clock `before` and this one's `now` (s after Deploy) into `into`; returns how many.
        /// Each moment is heard once: a first sight (`before` NaN), a jump or a rewind hears only the last frame's worth.
        /// </summary>
        public static int Heard(int mine,NativeDeployEject.Cut cut,float before,float now,Cue[] into)
        {
            if(!(now>=0)||into==null)return 0;if(!(before>=0)||before>now||now-before>.25f)before=now-.05f;int n=0;
            for(int i=0;i<Count&&n<into.Length;i++){var k=(Kind)i;float at=At(k,mine,cut);if(at>before&&at<=now)into[n++]=new Cue{Kind=k,Pan=k==Kind.Klaxon?-.15f:0};}
            return n;
        }
        /// <summary>The clip of sound `index` (deterministic: the same samples every call, no global random state).</summary>
        public static float[] Samples(int index)
        {
            if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));
            double duration=durations[index];var data=new float[(int)Math.Ceiling(duration*SampleRate)];uint seed=(uint)(40503+index*7919);
            double phase=0,phase2=0,low=0,band=0;
            for(int n=0;n<data.Length;n++)
            {
                double t=n/(double)SampleRate,u=t/duration;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;double noise=seed/(double)uint.MaxValue*2-1;
                low+=(noise-low)*.04;band+=(noise-band)*.3;double value;
                switch((Kind)index)
                {
                    case Kind.Klaxon:
                    {
                        // a two-tone alarm horn, four tones a second, its square-ish buzz rounded
                        double f=((int)(t*4))%2==0?620:470;phase+=2*Math.PI*f/SampleRate;double sq=Math.Tanh(3.2*Math.Sin(phase))+.3*Math.Sin(phase*2);
                        value=.34*sq*(.75+.25*Math.Sin(2*Math.PI*23*t))*Math.Min(1,u*8)*(1-Math.Max(0,(u-.85)/.15));break;
                    }
                    case Kind.Hatch:
                    {
                        // the hatches' servo whine rising, then their stop clunk
                        phase+=2*Math.PI*(280+520*Math.Min(1,t/.32))/SampleRate;double whine=.2*Math.Sin(phase)*(t<.34?Math.Sin(Math.PI*t/.34):0);
                        double c=t-.33;double clunk=c>0?.7*Math.Exp(-c*28)*(Math.Sin(2*Math.PI*95*c)+.5*band):0;value=whine+clunk;break;
                    }
                    case Kind.Whoosh:
                    {
                        // a drop: filtered air falling away in pitch
                        phase+=2*Math.PI*(900-650*u)/SampleRate;value=.55*Math.Sin(Math.PI*u)*(band*.7+low*1.6)*(1+.3*Math.Sin(phase));break;
                    }
                    case Kind.Tube:
                    {
                        // the tube's rush and its rings ticking past every ChasePeriod
                        double tick=(t%NativeDeployEject.ChasePeriod);double ping=Math.Exp(-tick*55)*Math.Sin(2*Math.PI*(2400+300*Math.Sin(t*9))*tick);
                        value=(.35*low*2.2+.18*band)*Math.Sin(Math.PI*Math.Min(1,u*1.2))+.22*ping*(1-u*.6);break;
                    }
                    case Kind.Seat:
                    {
                        // the seat taking the pilot: a heavy low thunk and a creak of its springs
                        phase+=2*Math.PI*(84*(1-.35*Math.Min(1,t/.2)))/SampleRate;value=.9*Math.Exp(-t*14)*Math.Sin(phase)+.35*Math.Exp(-t*60)*band+.08*Math.Exp(-t*9)*Math.Sin(2*Math.PI*330*t);break;
                    }
                    case Kind.Harness:
                    {
                        // the bar slamming home: an inharmonic clank and its latch's click
                        double e=Math.Exp(-t*18);value=.42*e*(Math.Sin(2*Math.PI*1180*t)+.6*Math.Sin(2*Math.PI*2630*t)+.35*Math.Sin(2*Math.PI*3910*t))/1.95+.3*Math.Exp(-t*90)*band;
                        double c=t-.06;if(c>0)value+=.25*Math.Exp(-c*120)*Math.Sin(2*Math.PI*4200*c);break;
                    }
                    case Kind.Seal:
                    {
                        // the roof hatch sealing overhead: a bass thump, its pitch falling, and the cabin's pressure settling
                        phase+=2*Math.PI*(62-24*Math.Min(1,t/.25))/SampleRate;value=.95*Math.Exp(-t*6.5)*Math.Sin(phase)+.14*low*Math.Exp(-t*3)*2;break;
                    }
                    case Kind.Release:
                    {
                        // the clamps firing (two sharp clacks), then the vents' hiss as the pod drops away
                        double a=t,b=t-.07;double clacks=.6*Math.Exp(-a*70)*band+(b>0?.55*Math.Exp(-b*60)*band:0);
                        phase+=2*Math.PI*70/SampleRate;double boom=.4*Math.Exp(-t*9)*Math.Sin(phase);value=clacks+boom+.2*band*Math.Sin(Math.PI*Math.Min(1,u*1.3))*(t>.08?1:0);break;
                    }
                    default:
                    {
                        // the white rush: air roaring up through the clouds, brighter as it builds
                        double bright=.3+.7*u;value=.5*Math.Min(1,u*2.2)*(low*1.8*(1-bright)+band*.8*bright);phase2+=2*Math.PI*(180+400*u)/SampleRate;value*=1+.25*Math.Sin(phase2);break;
                    }
                }
                double envelope=Math.Min(1,t/.002)*Math.Min(1,(data.Length-1-n)/(SampleRate*.015));
                data[n]=(float)(Math.Tanh(value)*envelope);
            }
            return data;
        }
    }
}
