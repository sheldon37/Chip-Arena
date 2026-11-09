using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeDogMallet;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Glaive's moves animated by hand (owner 2026-11-09: "Lets make the combos flow nicely and the animation very nice"; docs/GLAIVE.md),
    /// keyed as the Top Dog's mallet is (Core NativeDogMallet: the same pose, the same solver on the model, Runtime
    /// NativeGlaivePresentation.cs) but one-handed: the left hand is the clip's. The `handle` is the way from the fist to the ring's middle and
    /// the barrel's axis the ring's edge, turned along its travel, so the ring's edge leads every cut. The four cuts run into each other: a flat
    /// forehand from the right to the left, a rising backhand from the left to the right, a whirl (the whole body turning once, the ring held
    /// out), then a backhand flick that throws the ring out on its loop and catches it again. The skim is a low lunge, the throw a frisbee
    /// backhand from across the chest, the halo spun up off the fingertips overhead, the burst a fist thrust at the ring, the catch a recoil.
    /// Pure. Nothing here reaches the rules: each move's strike key is the authority's moment.
    /// </summary>
    public static class NativeGlaiveMotion
    {
        public enum Move{None,Cut1,Cut2,Whirl,Flick,Skim,Throw,Halo,Burst,Catch}
        /// <summary>The ring's middle out from the fist against the mallet's head (the solver's "head").</summary>
        public const float Scale=NativeWeaponModels.GlaiveCentre/NativeWeaponModels.MalletHead;
        public const float CatchTime=.3f;
        private static Key G(float t,float twist,float bend,float squash,float gy,float gp,float gd,float hy,float hp,Ease e=Ease.Smooth,float spin=0,float lean=0,float lift=0)
            =>K(t,twist,bend,squash,gy,gp,gd,hy,hp,e,lift,0,0,0,lean,1,spin);
        // ---- the cuts (from their hit; nominal wind-up and follow-through, scaled to the record's) ----
        private static readonly Key[] Cut1=
        {
            G(-.15f,20,4,1,25,-10,.36f,40,5),
            G(-.1f,45,8,.95f,35,-4,.38f,95,8,Ease.Out),
            G(-.06f,52,8,.93f,38,-2,.37f,105,10,Ease.Smooth,lean:-3),
            G(0,-8,12,1.05f,5,-10,.5f,-10,0,Ease.In,lean:3),
            G(.1f,-45,8,1,-20,-12,.45f,-95,-5,Ease.Out),
            G(.25f,-40,6,1,-15,-8,.42f,-90,0),
        };
        private static readonly Key[] Cut2=
        {
            G(-.15f,-40,6,1,-15,-8,.42f,-90,0),
            G(-.1f,-50,8,.94f,-25,-18,.38f,-120,-15,Ease.Out),
            G(-.06f,-54,8,.92f,-28,-20,.37f,-125,-18,Ease.Smooth,lean:3),
            G(0,10,10,1.06f,10,0,.5f,15,12,Ease.In,lean:-3),
            G(.1f,45,6,1.02f,30,14,.45f,95,25,Ease.Out),
            G(.25f,40,4,1,28,10,.42f,90,15),
        };
        /// <summary>The whirl: a crouch, then the whole body turns once with the ring held out at arm's length, the hit as it sets off (the
        /// rules cut all round at once), the turn carried through and settled facing ahead.</summary>
        private static readonly Key[] Whirl=
        {
            G(-.2f,40,4,1,28,10,.42f,90,15),
            G(-.12f,30,10,.9f,40,-10,.4f,100,0,Ease.Out,spin:-20),
            G(-.06f,30,12,.88f,45,-12,.42f,105,-2,Ease.Smooth,spin:-32),
            G(0,10,10,1.04f,70,-8,.52f,95,0,Ease.In,spin:60),
            G(.12f,10,10,1.05f,70,-8,.52f,95,0,Ease.Linear,spin:250),
            G(.22f,10,8,1.02f,68,-8,.5f,92,0,Ease.Out,spin:345),
            G(.36f,5,6,1,30,-10,.42f,40,0,Ease.Smooth,spin:360),
        };
        /// <summary>The flick: drawn across the body to the left hip, whipped out forward to the right (the release: the ring leaves on its
        /// loop), the open hand held out for it, the catch at FlickTime after the release and its recoil.</summary>
        private static readonly Key[] Flick=
        {
            G(-.16f,5,6,1,30,-10,.42f,40,0),
            G(-.1f,-35,6,.95f,-30,-12,.32f,-90,0,Ease.Out),
            G(-.05f,-42,8,.93f,-35,-14,.3f,-100,0,Ease.Smooth),
            G(0,15,10,1.05f,15,-2,.52f,15,0,Ease.In),
            G(.12f,30,6,1,30,0,.5f,40,0,Ease.Out),
            G(.4f,10,4,1,20,-4,.45f,10,0),
            G(NativeGlaive.FlickTime,5,4,1,15,-6,.4f,0,0),
            G(NativeGlaive.FlickTime+.08f,-5,8,.96f,5,-14,.36f,-10,-10,Ease.Out),
        };
        // ---- the rest (from their start) ----
        private static readonly Key[] SkimKeys=
        {
            G(0,10,10,1,25,-10,.4f,40,0),
            G(.06f,36,28,.92f,44,-30,.41f,70,-10,Ease.Out),
            G(.1f,38,32,1,46,-32,.42f,74,-12),
            G(.18f,-20,24,1.06f,-6,-15,.48f,-30,-5,Ease.In),
            G(.3f,-40,14,1.02f,-25,-10,.45f,-100,0,Ease.Out),
            G(.54f,0,6,1,20,-10,.4f,40,0),
        };
        private static readonly Key[] ThrowKeys=
        {
            G(0,0,4,1,20,-10,.4f,40,0),
            G(.04f,-40,10,.94f,-32,-12,.3f,-90,0,Ease.Out),
            G(.06f,-44,10,.92f,-34,-14,.29f,-92,0),
            G(NativeGlaive.ThrowRelease,20,12,1.06f,18,0,.53f,20,0,Ease.In),
            G(.26f,35,8,1.02f,35,4,.5f,50,5,Ease.Out),
            G(.4f,10,4,1,25,-8,.42f,30,0),
        };
        private static readonly Key[] HaloKeys=
        {
            G(0,0,4,1,20,-10,.4f,40,0),
            G(.12f,0,-6,1.04f,10,60,.42f,0,90,Ease.Out),
            G(NativeGlaive.HaloRelease,0,-8,1.06f,8,70,.44f,0,95),
            G(.32f,0,0,1,10,30,.4f,0,60,Ease.Out),
            G(.44f,0,4,1,15,-15,.36f,20,0),
        };
        private static readonly Key[] BurstKeys=
        {
            G(0,0,4,1,15,-15,.36f,20,0),
            G(.06f,-8,10,1.04f,0,0,.5f,0,0,Ease.In),
            G(.3f,0,4,1,15,-15,.36f,20,0),
        };
        private static readonly Key[] CatchKeys=
        {
            G(0,0,4,1,25,-5,.48f,30,0),
            G(.06f,-12,10,.96f,10,-14,.36f,0,-10,Ease.Out),
            G(CatchTime,0,4,1,20,-10,.4f,40,0),
        };
        private static readonly float[] Wind=NativeGlaive.SwingHit,Follow={.25f,.25f,.36f,.54f};
        private static Key[] CutKeys(int step)=>step<=0?Cut1:step==1?Cut2:step==2?Whirl:Flick;
        private static bool Level(Move m)=>m==Move.Cut1||m==Move.Cut2||m==Move.Whirl||m==Move.Skim||m==Move.Throw||m==Move.Flick;

        /// <summary>The Glaive's pose for its record at `time`, its move and that move's start; false when nothing is keyed (the clip alone).</summary>
        public static bool Sample(NativeGlaiveKit k,float time,out DogMalletPose pose,out Move move,out float from)
        {
            pose=default;move=Move.None;from=-100;if(k==null)return false;
            if(k.CastKind!=null&&time>=k.CastAt)
            {
                float at=k.CastAt,rel=Math.Max(at+.01f,k.CastRelease),until=Math.Max(rel+.05f,k.CastUntil);from=at;
                switch(k.CastKind)
                {
                    case NativeGlaive.Swing:
                    {
                        int step=Math.Max(0,Math.Min(3,k.CastStep));move=step==0?Move.Cut1:step==1?Move.Cut2:step==2?Move.Whirl:Move.Flick;
                        // The flick runs on until the catch, whatever the record's end.
                        float end=step==3?Math.Max(until,rel+NativeGlaive.FlickTime+.08f):until;if(time>=end+FadeOut)break;
                        float wind=rel-at,follow=step==3?NativeGlaive.FlickTime+.08f:until-rel,t=time-rel;
                        float kt=t<0?t*Wind[step]/Math.Max(.01f,wind):t*(step==3?1:Follow[step]/Math.Max(.01f,follow));
                        pose=KeyedPose(CutKeys(step),kt,Level(move),Scale);pose.Weight=Fade(time,at,end);return true;
                    }
                    case NativeGlaive.Skim:
                    {
                        move=Move.Skim;if(time>=until+FadeOut)break;float hit=rel+NativeGlaive.SkimTime*.5f,kh=.18f,t=time-at;float kt=time<hit?t*kh/Math.Max(.01f,hit-at):kh+(time-hit)*(.54f-kh)/Math.Max(.01f,until-hit);
                        pose=KeyedPose(SkimKeys,kt,true,Scale);pose.Weight=Fade(time,at,until);return true;
                    }
                    case NativeGlaive.Throw:if(time<until+FadeOut){move=Move.Throw;pose=KeyedPose(ThrowKeys,time-at,true,Scale);pose.Weight=Fade(time,at,until);return true;}break;
                    case NativeGlaive.Halo:if(time<until+FadeOut){move=Move.Halo;pose=KeyedPose(HaloKeys,time-at,false,Scale);pose.Weight=Fade(time,at,until);return true;}break;
                    case NativeGlaive.Burst:if(time<until+FadeOut){move=Move.Burst;pose=KeyedPose(BurstKeys,time-at,false,Scale);pose.Weight=Fade(time,at,until);return true;}break;
                }
            }
            // The ring caught back from a throw or the halo: the arm taken back by it.
            if(k.Disc==NativeGlaive.Hand&&!k.Reformed&&k.DiscAt>-50&&time>=k.DiscAt&&time<k.DiscAt+CatchTime+FadeOut&&!(k.CastKind==NativeGlaive.Swing&&k.CastStep==3&&time<k.CastUntil+FadeOut))
            {
                move=Move.Catch;from=k.DiscAt;pose=KeyedPose(CatchKeys,time-k.DiscAt,true,Scale);pose.Weight=Fade(time,k.DiscAt,k.DiscAt+CatchTime);return true;
            }
            return false;
        }
        private static float Fade(float time,float at,float until)=>Math.Max(0,Math.Min(1,(time-at)/FadeIn))*(1-Math.Max(0,Math.Min(1,(time-until)/FadeOut)));
        /// <summary>A move's pose at its own clock (from the hit for the cuts, from the start for the rest), weight 1: for the checks.</summary>
        public static DogMalletPose At(Move m,float t)
        {
            switch(m)
            {
                case Move.Cut1:return KeyedPose(Cut1,t,true,Scale);case Move.Cut2:return KeyedPose(Cut2,t,true,Scale);case Move.Whirl:return KeyedPose(Whirl,t,true,Scale);
                case Move.Flick:return KeyedPose(Flick,t,true,Scale);case Move.Skim:return KeyedPose(SkimKeys,t,true,Scale);case Move.Throw:return KeyedPose(ThrowKeys,t,true,Scale);
                case Move.Halo:return KeyedPose(HaloKeys,t,false,Scale);case Move.Burst:return KeyedPose(BurstKeys,t,false,Scale);case Move.Catch:return KeyedPose(CatchKeys,t,true,Scale);
            }
            return default;
        }
        public static float[] KeyTimes(Move m)
        {
            Key[] k=m==Move.Cut1?Cut1:m==Move.Cut2?Cut2:m==Move.Whirl?Whirl:m==Move.Flick?Flick:m==Move.Skim?SkimKeys:m==Move.Throw?ThrowKeys:m==Move.Halo?HaloKeys:m==Move.Burst?BurstKeys:m==Move.Catch?CatchKeys:new Key[0];
            var t=new float[k.Length];for(int i=0;i<k.Length;i++)t[i]=k[i].T;return t;
        }
        /// <summary>The ring's middle on the nominal body for a pose (for the checks and the trail).</summary>
        public static Vector3 RingAt(in DogMalletPose p)=>GripAt(p)+HandleAt(p)*NativeWeaponModels.GlaiveCentre;
    }
}
