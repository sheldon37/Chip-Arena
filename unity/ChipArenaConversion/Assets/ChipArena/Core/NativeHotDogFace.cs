using System;

namespace ChipArena.UnityConversion
{
    /// <summary>What the Hot Dog's face shows (owner 2026-11-09: "animate eyes and mouth with hotdog character. Give them expressions lol").</summary>
    public enum HotDogMood{Idle,Angry,Shout,Grin,Ouch,Surprise,Laugh,Hungry,Wink,Dizzy,Dead}

    /// <summary>The face's pose: each eye's opening (1 open) and lower lid (1 up: a squint or a laugh's crescent), where the pupils look
    /// (-1..1) and their size, each brow's tilt (-1 angry, inner end down; 1 worried, up) and lift, and the mouth: how far open (0..1), its
    /// smile (-1 frown .. 1 grin), its width (0..1), the teeth and tongue showing (0..1). Cross: X eyes.</summary>
    public struct HotDogFacePose
    {
        public float OpenL,OpenR,LowerL,LowerR,LookX,LookY,Pupil,BrowL,BrowR,BrowUp,Open,Smile,Wide,Teeth,Tongue;public bool Cross;
    }

    /// <summary>
    /// The Hot Dog's animated face, pure and deterministic (Runtime NativeHotDogFacePresentation.cs draws it on the head over the texture's
    /// painted face): each mood's pose with its own life (a shout's jabber, a laugh's bounce, dizzy rolling eyes), blinks and glances on top,
    /// eased from one mood to the next; the mood from what its wearer is doing (the Top Dog's casts and feast, a hit, a daze) and the
    /// intro's beats.
    /// </summary>
    public static class NativeHotDogFace
    {
        /// <summary>A hit's wince outlasts its flash (0.14 s) by this long, so it reads.</summary>
        public const float OuchHold=.3f;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Hash(int seed,int i){unchecked{uint h=(uint)seed*2654435761u^(uint)i*2246822519u;h^=h>>13;h*=0x5bd1e995u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        /// <summary>The mood's own pose at `t` s into it.</summary>
        public static HotDogFacePose Target(HotDogMood m,float t,int seed)
        {
            var p=new HotDogFacePose{OpenL=1,OpenR=1,Pupil=1,Wide=.8f};float s=(float)Math.Sin(t*17+seed),b=(float)Math.Abs(Math.Sin(t*9.5f));
            switch(m)
            {
                default:p.LowerL=p.LowerR=.15f;p.BrowL=p.BrowR=-.45f;p.Smile=.45f;p.Open=.24f;p.Teeth=1;p.Wide=.85f;break;                     // the smug, gritted grin of the painted face
                case HotDogMood.Angry:p.LowerL=p.LowerR=.4f;p.BrowL=p.BrowR=-1;p.Smile=-.35f;p.Open=.18f;p.Teeth=1;p.Wide=.9f;p.Pupil=.85f;break;
                case HotDogMood.Shout:p.LowerL=p.LowerR=.2f;p.BrowL=p.BrowR=-.7f;p.Smile=.05f;p.Open=.8f+.2f*s;p.Teeth=.8f;p.Tongue=.5f;p.Wide=1;p.Pupil=.75f;break;
                case HotDogMood.Grin:p.LowerL=p.LowerR=.35f;p.BrowL=p.BrowR=-.25f;p.Smile=1;p.Open=.3f;p.Teeth=1;p.Wide=1;break;
                case HotDogMood.Ouch:p.OpenL=p.OpenR=.05f;p.LowerL=p.LowerR=.6f;p.BrowL=p.BrowR=.8f;p.Smile=-.85f;p.Open=.5f+.1f*s;p.Teeth=.8f;p.Wide=.75f;break;
                case HotDogMood.Surprise:p.BrowL=p.BrowR=1;p.BrowUp=1;p.Pupil=.55f;p.Open=.85f;p.Wide=.42f;p.Smile=0;break;
                case HotDogMood.Laugh:p.OpenL=p.OpenR=.2f;p.LowerL=p.LowerR=.95f;p.BrowL=p.BrowR=.25f;p.BrowUp=.3f*b;p.Smile=1;p.Open=.55f+.35f*b;p.Teeth=.8f;p.Tongue=.7f;p.Wide=1;break;
                case HotDogMood.Hungry:p.LowerL=p.LowerR=.25f;p.BrowL=p.BrowR=.1f;p.BrowUp=.4f;p.Pupil=1.25f;p.Smile=.9f;p.Open=.55f+.1f*s;p.Teeth=.6f;p.Tongue=1;p.Wide=1;p.LookY=.2f;break;
                case HotDogMood.Wink:p.OpenL=0;p.LowerL=.6f;p.BrowL=-.4f;p.BrowR=.35f;p.BrowUp=.2f;p.Smile=1;p.Open=.2f;p.Teeth=1;p.Wide=1;break;
                case HotDogMood.Dizzy:{float a=t*7;p.LookX=.7f*(float)Math.Cos(a);p.LookY=.6f*(float)Math.Sin(a);p.BrowL=.5f;p.BrowR=-.3f;p.Smile=-.25f+.2f*s;p.Open=.3f;p.Wide=.7f;p.Tongue=.4f;break;}
                case HotDogMood.Dead:p.Cross=true;p.BrowL=p.BrowR=.3f;p.Smile=-.5f;p.Open=.35f;p.Tongue=1;p.Wide=.7f;break;
            }
            return p;
        }
        /// <summary>How shut a blink has the eyes at `t` (0 open, 1 shut): one every 2.2 to 4.6 s, 0.14 s long.</summary>
        public static float Blink(float t,int seed)
        {
            float at=0;for(int i=0;i<4096;i++){float gap=2.2f+2.4f*Hash(seed,i);if(at+gap>t){float u=(t-at)/.14f;return u>=0&&u<1?(float)Math.Sin(Math.PI*u):0;}at+=gap;}return 0;
        }
        /// <summary>Where the eyes wander at `t` (-1..1 each): a new spot every 0.7 to 2.2 s, darted to.</summary>
        public static void Glance(float t,int seed,out float x,out float y)
        {
            float at=0;int k=0;for(;k<4096;k++){float gap=.7f+1.5f*Hash(seed+7,k);if(at+gap>t)break;at+=gap;}
            x=(Hash(seed+11,k)*2-1)*.55f;y=(Hash(seed+13,k)*2-1)*.35f;
        }
        /// <summary>The face at `t` for `mood` (begun `since` s ago), with blinks and glances (none in a laugh, a wink, a daze or death).</summary>
        public static HotDogFacePose At(HotDogMood mood,float since,float t,int seed)
        {
            var p=Target(mood,since,seed);
            if(mood!=HotDogMood.Laugh&&mood!=HotDogMood.Dizzy&&mood!=HotDogMood.Dead&&mood!=HotDogMood.Ouch)
            {float b=Blink(t,seed);p.OpenL*=1-b;p.OpenR*=1-b;}
            if(mood==HotDogMood.Idle||mood==HotDogMood.Grin||mood==HotDogMood.Angry){Glance(t,seed,out float gx,out float gy);p.LookX+=gx;p.LookY+=gy;}
            return p;
        }
        /// <summary>`cur` eased toward `target` over `dt` (about a tenth of a second); the X eyes switch at once.</summary>
        public static HotDogFacePose Ease(HotDogFacePose cur,HotDogFacePose target,float dt)
        {
            float k=1-(float)Math.Exp(-dt*16),e=1-(float)Math.Exp(-dt*40);float L(float a,float b,float w)=>a+(b-a)*w;
            return new HotDogFacePose{OpenL=L(cur.OpenL,target.OpenL,e),OpenR=L(cur.OpenR,target.OpenR,e),LowerL=L(cur.LowerL,target.LowerL,k),LowerR=L(cur.LowerR,target.LowerR,k),
                LookX=L(cur.LookX,target.LookX,e),LookY=L(cur.LookY,target.LookY,e),Pupil=L(cur.Pupil,target.Pupil,k),BrowL=L(cur.BrowL,target.BrowL,k),BrowR=L(cur.BrowR,target.BrowR,k),
                BrowUp=L(cur.BrowUp,target.BrowUp,k),Open=L(cur.Open,target.Open,e),Smile=L(cur.Smile,target.Smile,k),Wide=L(cur.Wide,target.Wide,k),Teeth=L(cur.Teeth,target.Teeth,k),
                Tongue=L(cur.Tongue,target.Tongue,k),Cross=target.Cross};
        }
        /// <summary>A live Hot Dog wearer's mood at `time`: hurt (its hit flash), dazed (a pancake, a daze, a wall), the Top Dog's casts and
        /// feast (its kit, null for anyone else), attacking, else idle. `since` is when that mood began, as far as it can tell.</summary>
        public static HotDogMood Of(Actor a,NativeTopDogKit k,bool dazed,float time,out float since)
        {
            since=0;if(a==null)return HotDogMood.Idle;
            if(time<a.FlashUntil+OuchHold){since=Math.Max(0,a.FlashUntil-.14f);return HotDogMood.Ouch;}
            if(dazed)return HotDogMood.Dizzy;
            var f=k?.Feast;
            if(f!=null&&time>=f.At&&time<f.Until)
            {
                if(f.Catch<-50){since=f.At;return HotDogMood.Laugh;}
                if(f.Snap>-50&&time>=f.Snap){since=f.Snap;return HotDogMood.Angry;}
                if(time<f.FeedAt){since=f.Catch;return HotDogMood.Grin;}
                if(time<f.Release){since=f.FeedAt;return HotDogMood.Hungry;}
                since=f.Release;return HotDogMood.Laugh;
            }
            if(k?.CastKind!=null&&time>=k.CastAt&&time<k.CastUntil+.25f)
            {
                since=k.CastAt;
                switch(k.CastKind)
                {
                    case NativeTopDog.Mallet:return k.CastStep==2?HotDogMood.Shout:HotDogMood.Angry;
                    case NativeTopDog.Dash:return HotDogMood.Grin;
                    case NativeTopDog.Slam:return HotDogMood.Shout;
                    case NativeTopDog.Boing:return HotDogMood.Surprise;
                    case NativeTopDog.Zip:case NativeTopDog.Peel:return HotDogMood.Grin;
                    case NativeTopDog.Mustard:return HotDogMood.Shout;
                    case NativeTopDog.Glove:return HotDogMood.Angry;
                    default:return HotDogMood.Laugh;
                }
            }
            if(time-a.AttackAt>=0&&time-a.AttackAt<.45f){since=a.AttackAt;return HotDogMood.Angry;}
            return HotDogMood.Idle;
        }
        /// <summary>The face through Order Up! (NativeTopDogIntro's beats) at `age` s.</summary>
        public static HotDogMood Intro(float age,out float since)
        {
            since=0;if(age<NativeTopDogIntro.LidAt)return HotDogMood.Idle;
            if(age<NativeTopDogIntro.ApexAt){since=NativeTopDogIntro.LidAt;return HotDogMood.Surprise;}
            if(age<NativeTopDogIntro.LandAt){since=NativeTopDogIntro.ApexAt;return HotDogMood.Laugh;}
            if(age<NativeTopDogIntro.SlamAt-.3f){since=NativeTopDogIntro.LandAt;return HotDogMood.Grin;}
            if(age<NativeTopDogIntro.SlamAt+.45f){since=NativeTopDogIntro.SlamAt-.3f;return HotDogMood.Shout;}
            if(age<NativeTopDogIntro.CardAt){since=NativeTopDogIntro.SlamAt+.45f;return HotDogMood.Idle;}
            if(age<NativeTopDogIntro.WinkAt){since=NativeTopDogIntro.CardAt;return HotDogMood.Grin;}
            if(age<NativeTopDogIntro.WinkAt+.7f){since=NativeTopDogIntro.WinkAt;return HotDogMood.Wink;}
            since=NativeTopDogIntro.WinkAt+.7f;return HotDogMood.Grin;
        }
    }
}
