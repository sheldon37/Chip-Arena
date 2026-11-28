using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Kernel Panic's private cinematic camera (the C16 rules, NativeCinematicRules: seen only by the pilots in it, out of
    /// walls by the runtime's raycast, Settings camera shake and Reduced effects respected; everyone else keeps the arena
    /// camera). One shot per step on the sequence's replicated data (start, anchor, facing, waypoints) and the participants'
    /// hit-stopped clock (KernelPanicFeel.Clock): a low two-shot held on the connect through the cut-in, the Breaker tracked
    /// side-on, a low angle under the rising kick, a high angle over the hammer-fist, an orbit through the fork bomb, and a
    /// low wide on the code column with a slow push-in, then a blend back to the player's camera through the recovery. The
    /// victim's shots mirror the caster's side and take the reverse angle on the hammer. Reduced effects: a gentler path
    /// (no hard cuts: every change blends over .3 s, no orbit, no roll, no shake, a wider lens). Unity world coordinates.
    /// </summary>
    public static class KernelPanicCamera
    {
        public struct Shot{public Vector3 Position,Look;public float Fov,Roll,Shake,Blend;public int Index;}
        public const float BlendIn=.12f,BlendOut=.6f,ReducedCut=.3f;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static Vector3 Up=>Vector3.UnitY;
        /// <summary>The camera's clock at match time `time`: the participants' hit-stopped clock (KernelPanicFeel.Clock), or with
        /// reduced effects the match's, so a hit-stop's catch-up never whips the gentler path.</summary>
        public static float Clock(CyberLordView v,float time,bool reduced)=>reduced?time:KernelPanicFeel.Clock(v,time,false);
        /// <summary>Whether the camera directs at match time `time` (from the connect to the end of the recovery's blend).</summary>
        public static bool Active(CyberLordView v,float time)
        {
            if(!(v.PanicConnect>-90)||v.PanicOutcome!=CyberLordSpec.Connect&&v.PanicOutcome!=CyberLordSpec.EndedEarly)return false;
            if(v.PanicOutcome==CyberLordSpec.EndedEarly&&v.PanicEndedAt>-90&&time>=v.PanicEndedAt+BlendOut*.5f)return false;
            return time>=v.PanicConnect&&time<v.PanicStart+KernelPanicPath.End;
        }
        /// <summary>The shot at presentation time `t` (the participant's clock) for `victim` (true: the victim's camera).</summary>
        public static Shot At(CyberLordView v,float t,bool victim,bool reduced,bool shake,float aspect)
        {
            float age=t-v.PanicStart;var S=KernelPanicPath.Steps;int index=age<0?0:KernelPanicPath.Step(age);
            var s=Raw(v,age,index,victim,reduced);
            // Reduced effects: the previous shot blends into this one over ReducedCut instead of cutting.
            if(reduced&&index>0){float from=index==0?0:index<=S.Length?S[index-1]:KernelPanicPath.Recovery;float k=Smooth((age-from)/ReducedCut);if(k<1){var p=Raw(v,from-1e-3f,index-1,victim,true);s.Position=Vector3.Lerp(p.Position,s.Position,k);s.Look=Vector3.Lerp(p.Look,s.Look,k);s.Fov=p.Fov+(s.Fov-p.Fov)*k;}}
            // In from the player's camera at the connect, back out through the recovery (or an early end).
            float end=v.PanicOutcome==CyberLordSpec.EndedEarly&&v.PanicEndedAt>-90?v.PanicEndedAt:v.PanicStart+KernelPanicPath.Recovery+.2f;
            s.Blend=Smooth((t-v.PanicConnect)/BlendIn)*(1-Smooth((t-end)/BlendOut));
            if(!shake||reduced){s.Shake=0;s.Roll=0;}
            if(aspect<1.35f){var d=s.Position-s.Look;s.Position=s.Look+d*(1.35f/Math.Max(.65f,aspect));}
            if(reduced)s.Fov=Math.Min(70,s.Fov+6);
            s.Index=index;return s;
        }
        private static Shot Raw(CyberLordView v,float age,int index,bool victim,bool reduced)
        {
            var S=KernelPanicPath.Steps;var f=KernelPanicPath.Forward(v.PanicYaw);var r=new Vector3(f.Z,0,-f.X)*(victim?-1:1);
            var vic=KernelPanicPath.Victim(v,age);var cas=KernelPanicPath.Caster(v,age,out _);var chest=vic+Up*1.1f;
            switch(index)
            {
                case 0:
                {
                    // The cut-in: a low two-shot on the contact, the lunge's line across the frame.
                    var hit=v.Point(KernelPanicPath.Anchor)+Up*1.15f-f*.4f;float u=Clamp01((age+CyberLordSpec.CutIn)/CyberLordSpec.CutIn);
                    return new Shot{Position=hit+r*(2.4f-.2f*u)-f*(victim?-1.2f:1.4f)+Up*(-.15f),Look=hit,Fov=36,Roll=victim?.05f:-.05f};
                }
                case 1:
                {
                    // The Breaker: tracked side-on as the victim flies, the lens pulling wider.
                    float u=Clamp01(age/S[1]);var mid=Vector3.Lerp(v.Point(KernelPanicPath.Anchor),v.Point(KernelPanicPath.LaunchEnd),.5f);
                    return new Shot{Position=mid+r*4.0f+Up*1.5f-f*1.4f,Look=Vector3.Lerp(mid,chest,.7f),Fov=42+8*u,Shake=age<.12f?.05f:0};
                }
                case 2:
                {
                    // The rising kick: low behind the blink spot, looking up the victim's climb.
                    var b=v.Point(KernelPanicPath.Behind);float u=Clamp01((age-S[1])/(S[2]-S[1]));
                    return new Shot{Position=b-f*2.2f+r*1.8f+Up*.5f,Look=Vector3.Lerp(b+Up*1.6f,chest,.75f),Fov=50+6*u,Roll=(victim?-1:1)*.06f*(1-u),Shake=age-S[1]<.18f&&age-S[1]>.08f?.04f:0};
                }
                case 3:
                {
                    // The hammer-fist: high over the caster's shoulder down onto the victim (the victim looks up at it coming).
                    var a=v.Point(KernelPanicPath.Above);
                    if(victim)return new Shot{Position=vic+Up*.2f+r*1.6f-f*.8f,Look=a+Up*1.2f,Fov=58,Shake=age-S[2]>.06f&&age-S[2]<.2f?.05f:0};
                    return new Shot{Position=a+Up*2.4f-f*2.2f+r*1.4f,Look=chest,Fov=50,Shake=age-S[2]>.06f&&age-S[2]<.2f?.05f:0};
                }
                case 4:
                {
                    // The fork bomb: orbiting the falling victim a quarter turn (held still with reduced effects).
                    float u=Clamp01((age-S[3])/(S[4]-S[3])),ang=(reduced?0:Smooth(u)*1.4f)-.5f;var d=Vector3.Transform(r,Quaternion.CreateFromAxisAngle(Up,ang));
                    return new Shot{Position=chest+d*4.2f+Up*.9f,Look=chest,Fov=48};
                }
                default:
                {
                    // The slam: a low wide on the landing and the column, pushing in slowly through the slow beat.
                    var land=v.Point(KernelPanicPath.Landing);float u=Clamp01((age-S[4]+.15f)/.9f),ease=Smooth(u);
                    return new Shot{Position=land+r*(6.2f-1.6f*ease)-f*(2.6f-.6f*ease)+Up*(1.1f+.3f*ease),Look=land+Up*(1.6f-.4f*ease),Fov=56-8*ease,Shake=age>=S[4]&&age<S[4]+.35f?.08f*(1-(age-S[4])/.35f):0};
                }
            }
        }
    }

    /// <summary>
    /// The skill cut-in (.6 s from the connect; owner: "a "skill cut-in" animation"): for the caster and the victim a full-screen
    /// splash of the caster's live 3D portrait (C14, NativePortraits: the pilot's hat and colours) on diagonal hardlight
    /// panels, the glitch text KERNEL PANIC and a speed-line burst; everyone else gets a slim banner along the top that never
    /// covers the crosshair. Layout numbers on the HUD's 1600 x 900 virtual canvas. Reduced effects: no flash or strobe, the
    /// panels ease in, the text resolves without RGB jitter. Pure functions of the age since the connect.
    /// </summary>
    public static class KernelPanicCutIn
    {
        public const float Seconds=CyberLordSpec.CutIn,InSeconds=.09f,OutFrom=.47f,BannerSeconds=1.5f;
        public const string Title="KERNEL PANIC",Tag="ULTIMATE // SKILL CUT-IN";
        /// <summary>The panels' angle (degrees: rising to the right) and their bands (centre offset from the screen's centre line,
        /// thickness, as shares of the screen height), the middle band holding the portrait.</summary>
        public const float Angle=-16;
        public static readonly float[] BandAt={-.34f,0,.36f},BandThick={.16f,.46f,.12f};
        public struct Splash{public float Alpha,Flash,Slide,Text,Split,Speed,Out,Tear;public int Typed,Seed;}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        /// <summary>The splash at `age` s from the connect (Alpha 0 outside it).</summary>
        public static Splash At(float age,bool reduced)
        {
            var s=new Splash{Seed=(int)Math.Floor(Math.Max(0,age)*(reduced?6:30))};if(!(age>=0)||age>=Seconds)return s;
            float into=Smooth(age/InSeconds),outU=Smooth((age-OutFrom)/(Seconds-OutFrom));
            s.Alpha=into*(1-outU);s.Out=outU;s.Slide=1-into+outU;s.Flash=reduced?0:(float)Math.Exp(-age/.05f);
            // The title types in over .2 s from .06 s, glitching (RGB split) as it lands; the speed lines burst from the portrait.
            float typed=Clamp01((age-.06f)/.2f);s.Typed=(int)Math.Ceiling(typed*Title.Length);s.Text=typed;
            s.Split=reduced?0:(1-typed)*.9f+.25f*(float)Math.Exp(-(age-.26f)*(age-.26f)/.002f);s.Speed=Clamp01(age/.4f);
            s.Tear=reduced?0:outU;return s;
        }
        /// <summary>The top banner for everyone else: its slide (0 hidden, 1 shown) at `age` s from the connect.</summary>
        public static float Banner(float age)=>!(age>=0)||age>=BannerSeconds?0:Smooth(age/.12f)*(1-Smooth((age-(BannerSeconds-.25f))/.25f));
        /// <summary>The banner's box on the 1600 x 900 canvas: under the SET / ROUND tag (y 20-46), never near the centre.</summary>
        public const float BannerTop=54,BannerHeight=26,BannerWidth=440;
        /// <summary>Who sees which: the caster and the victim the splash, everyone else the banner; nothing for an unrelated replay.</summary>
        public static bool Splashes(CyberLordView v,int local)=>KernelPanicFeel.Participant(v,local);
        /// <summary>A glitched copy of the title: `n` letters typed, the rest random block glyphs on the seed (no allocation:
        /// writes into `into`, returns the length).</summary>
        public static int Typed(int n,int seed,char[] into)
        {
            const string blocks="#%@&$?!/\\<>[]{}=+*";int len=Math.Min(into.Length,Title.Length);
            for(int i=0;i<len;i++){char c=Title[i];if(i>=n&&c!=' '){uint h=(uint)(seed*31+i*977)*2654435761u;c=blocks[(int)(h>>16)%blocks.Length];}into[i]=c;}
            return len;
        }
    }
}
