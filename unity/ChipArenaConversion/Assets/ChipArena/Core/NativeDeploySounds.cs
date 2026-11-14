using System;
namespace ChipArena.UnityConversion
{
    // Original procedural station sounds. Cue times belong exclusively to NativeDeploySparks.
    public static class NativeDeploySounds
    {
        public const int SampleRate=48000,Count=12;
        public struct Sound {public int Index;public string Key;public float Pan,Gain,Rate;}
        private static readonly string[] keys={"deploy.destination.spin","deploy.destination.brake","deploy.destination.stop","deploy.mutator.spin","deploy.mutator.brake","deploy.mutator.stop","deploy.boon.card","deploy.bane.card","deploy.chaos.card","deploy.boon.sweep","deploy.bane.sweep","deploy.chaos.sweep"};
        public static Sound Of(NativeDeploySparks.Cue cue)
        {
            int i=-1;
            switch(cue.Kind)
            {
                case NativeDeploySparks.CueKind.Spin:i=cue.Machine*3;break;
                case NativeDeploySparks.CueKind.Brake:i=cue.Machine*3+1;break;
                case NativeDeploySparks.CueKind.Stop:i=cue.Machine*3+2;break;
                case NativeDeploySparks.CueKind.Card:case NativeDeploySparks.CueKind.Sweep:
                    int p=cue.Polarity==NativeDeployPresentation.Polarity.Boon?0:cue.Polarity==NativeDeployPresentation.Polarity.Bane?1:cue.Polarity==NativeDeployPresentation.Polarity.Chaos?2:-1;
                    i=p>=0?(cue.Kind==NativeDeploySparks.CueKind.Card?6:9)+p:cue.Machine*3+2;break;
            }
            if(cue.Machine<0||cue.Machine>1||i<0||i>=Count)return new Sound{Index=-1};
            return new Sound{Index=i,Key=keys[i],Pan=cue.Reel<0?0:(cue.Reel-1)*.55f,Gain=i>=9?.8f:i>=6?.65f:cue.Kind==NativeDeploySparks.CueKind.Spin?.55f:.7f,Rate=cue.Kind==NativeDeploySparks.CueKind.Stop?1+(cue.Reel-1)*.035f:1};
        }
        public static double Duration(int i)=>i==0||i==3?NativeDeployPresentation.StopTime(2):i==1||i==4?NativeDeployPresentation.Decel:i<6?.32:i<9?.65:1.35;
        public static float[] Samples(int index)
        {
            if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));
            double duration=Duration(index);var data=new float[(int)Math.Ceiling(duration*SampleRate)];uint seed=(uint)(12345+index*7919);
            double phase=0;
            for(int n=0;n<data.Length;n++)
            {
                double t=n/(double)SampleRate,u=t/duration;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;double noise=seed/(double)uint.MaxValue*2-1;
                double value;
                if(index<6)
                {
                    bool electric=index>=3;int kind=index%3;
                    if(kind==0)
                    {
                        // Lever transient followed by motor/ratchet; each stopped reel takes a third of the bed away.
                        double speed=0;for(int r=0;r<3;r++)speed+=Math.Max(0,Math.Min(1,(NativeDeployPresentation.StopTime(r)-t)/NativeDeployPresentation.Decel));speed/=3;
                        phase+=2*Math.PI*(electric?150:85)*(1+speed)/SampleRate;
                        double teeth=Math.Pow(Math.Max(0,Math.Sin(2*Math.PI*(32*t+6*t*t))),12);
                        value=.22*speed*(Math.Sin(phase)+.35*Math.Sin(phase*(electric?3.01:2))+.6*noise*teeth)+.65*Math.Exp(-t*65)*(noise+Math.Sin(2*Math.PI*210*t));
                    }
                    else if(kind==1){phase+=2*Math.PI*((electric?1550:950)*(1-.7*u))/SampleRate;value=.28*Math.Sin(Math.PI*u)*(.65*Math.Sin(phase)+noise*.45)*(1+.25*Math.Sin(2*Math.PI*47*t));}
                    else value=.65*Math.Exp(-t*20)*(Math.Sin(2*Math.PI*(electric?130:88)*t)+.3*Math.Sin(2*Math.PI*(electric?611:263)*t))+.3*noise*Math.Exp(-t*70);
                }
                else
                {
                    int polarity=(index-6)%3;bool sweep=index>=9;
                    if(polarity==0)
                    {
                        double note=sweep?Math.Floor(Math.Min(3,t/.16)):Math.Min(2,t/.09),frequency=660*Math.Pow(2,note/4);
                        phase+=2*Math.PI*frequency/SampleRate;value=.32*Math.Exp(-t*(sweep?1.7:4))*(Math.Sin(phase)+.35*Math.Sin(phase*2.005)+.18*Math.Sin(phase*3));
                    }
                    else if(polarity==1)
                    {
                        phase+=2*Math.PI*(sweep?400+150*Math.Sin(2*Math.PI*5*t):820*(1-.8*u))/SampleRate;
                        double buzz=Math.Tanh(3*Math.Sin(phase));value=.36*Math.Exp(-t*(sweep?1.8:3))*(.65*buzz+.35*noise)*( .65+.35*Math.Sin(2*Math.PI*37*t));
                    }
                    else
                    {
                        phase+=2*Math.PI*(460+240*Math.Sin(2*Math.PI*(sweep?4:7)*t))/SampleRate;
                        value=.36*Math.Exp(-t*(sweep?1.4:3))*(Math.Sin(phase+2*Math.Sin(2*Math.PI*31*t))+.2*noise*Math.Pow(Math.Max(0,Math.Sin(2*Math.PI*19*t)),8));
                    }
                }
                double envelope=Math.Min(1,t/.002)*Math.Min(1,(data.Length-1-n)/(SampleRate*.015));
                data[n]=(float)(Math.Tanh(value)*envelope);
            }
            return data;
        }
    }
}
