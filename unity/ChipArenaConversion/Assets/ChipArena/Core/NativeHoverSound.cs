using System;
namespace ChipArena.UnityConversion
{
    // A tray is a per-frame layout object. Only the highlighted slot/chip (or an explicit new-turn reset) is an audio edge.
    public sealed class NativeHoverFocus
    {
        private int slot=-1;private string chip;
        public void Reset(){slot=-1;chip=null;}
        public bool Step(int next,string id,bool enabled)
        {
            if(!enabled||next<0||id==null){Reset();return false;}
            bool changed=next!=slot||id!=chip;slot=next;chip=id;return changed;
        }
    }

    // Original, fixed-pitch UI ticks: no sample bag, pitch jitter, sweep or loop, with short click-free envelopes.
    // Kept independent of LocalAssets so every installation gets the same hover feedback.
    public static class NativeHoverSound
    {
        public const int SampleRate=48000;
        public const double Cooldown=.09;
        public static float[] Samples(bool chip)
        {
            int count=chip?3360:1920; // Chip 70 ms; menu 40 ms.
            double frequency=chip?880:680,decay=chip?75:110;
            var data=new float[count];
            for(int i=0;i<count;i++)
            {
                double t=i/(double)SampleRate,attack=Math.Min(1,i/(SampleRate*.002)),tail=Math.Min(1,(count-1-i)/(SampleRate*.008));
                double phase=2*Math.PI*frequency*t;
                data[i]=(float)(.55*attack*tail*Math.Exp(-decay*t)*(Math.Sin(phase)+.22*Math.Sin(phase*2)));
            }
            return data;
        }
    }
}
