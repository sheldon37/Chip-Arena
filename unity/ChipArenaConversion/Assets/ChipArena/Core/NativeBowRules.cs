using System;
namespace ChipArena.UnityConversion
{
    public static class NativeBowRules
    {
        public const float Nock=.35f,Move=.7f,RainRadius=3.5f;
        public static float Fraction(float held,float rate)=>held*Math.Max(.01f,rate)<.25f?0:MathUtil.Clamp((held*Math.Max(.01f,rate)-.25f)/.55f,0,1);
        public static float Damage(float draw)=>18+44*draw;
        public static float Speed(float draw)=>45+65*draw;
        public static float Gravity(float draw)=>30-16*draw;
        public static float Impulse(float draw)=>120+180*draw;
        public static float Spread(float held,float rate)
        {float draw=Fraction(held,rate),fatigue=MathUtil.Clamp((held-.8f/Math.Max(.01f,rate)-2.5f)/1.5f,0,1);return (.012f-.0105f*draw)*(1-fatigue)+.01f*fatigue;}
        // Keep one real arrow on the aim point; the old nearest sample was .714 m away.
        public static float RainSpawnHeight(V3 ground,float radius,float overhead)
        {
            float height=Math.Max(radius+.04f,Math.Min(12,overhead-radius-.04f));
            if(Math.Abs(ground.x)<=32+radius&&Math.Abs(ground.z)<=28+radius&&ground.y<24-radius-.04f)
                height=Math.Min(height,24-radius-.04f-ground.y);
            return height;
        }
        public static V3 RainOffset(int index)
        {float angle=index*2.39996323f,r=index==0?0:RainRadius*(float)Math.Sqrt((index+.5f)/12);return new V3((float)Math.Cos(angle)*r,0,(float)Math.Sin(angle)*r);}
    }
}
