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
        public enum Move{None,Cut1,Cut2,Whirl,Flick,Skim,Throw,Halo,Burst,Catch,Ready,Intro}
        /// <summary>The ring's middle out from the fist against the mallet's head (the solver's "head").</summary>
        public const float Scale=NativeWeaponModels.GlaiveCentre/NativeWeaponModels.MalletHead;
        public const float CatchTime=.3f;
        private static Key G(float t,float twist,float bend,float squash,float gy,float gp,float gd,float hy,float hp,Ease e=Ease.Smooth,float spin=0,float lean=0,float lift=0,float tuck=0)
            =>K(t,twist,bend,squash,gy,gp,gd,hy,hp,e,lift,tuck,0,0,lean,1,spin);
        // Reworked at the owner's word (2026-11-09: "improve the animations significantly"): every move uses the whole body. The cuts run at
        // chest and shoulder height, not the hip: each coils deep (the chest wound 70 degrees round, the knees sunk, a held beat), whips through
        // at full reach and follows round until the ring wraps the other side; the backhand rises onto the toes; the whirl is a leap, the
        // legs tucked under a full turn in the air, landed in a crouch; the throw winds the ring right across the chest; the flick whips
        // out and the catch yanks the arm back; the skim is a low dive; the halo is flung up from overhead.
        // ---- the cuts (from their hit; nominal wind-up and follow-through, scaled to the record's) ----
        /// <summary>A flat forehand from the right to the left: the ring cocked behind the right shoulder, the chest wound round, a held
        /// beat, then whipped across at full reach and carried round until it wraps the left side.</summary>
        private static readonly Key[] Cut1=
        {
            G(-.15f,30,6,1,40,-5,.42f,80,6),
            G(-.11f,62,12,.9f,50,4,.38f,106,10,Ease.Out,lean:-7),
            G(-.06f,68,14,.88f,50,5,.4f,110,12,Ease.Smooth,lean:-8),
            G(0,-5,14,1.05f,10,-2,.52f,10,-2,Ease.In,lean:5),
            G(.08f,-55,12,1.02f,-30,-4,.46f,-55,-6,Ease.Out,lean:8),
            G(.17f,-68,9,1,-42,-6,.42f,-82,-4,Ease.Out,lean:6),
            G(.25f,-60,8,1,-40,-8,.42f,-80,0,lean:4),
        };
        /// <summary>A rising backhand from low on the left to high on the right, up onto the toes, the ring ending above the right shoulder.</summary>
        private static readonly Key[] Cut2=
        {
            G(-.15f,-60,8,1,-40,-8,.42f,-80,0,lean:4),
            G(-.11f,-70,18,.86f,-45,-28,.41f,-85,-26,Ease.Out,lean:7),
            G(-.06f,-74,20,.84f,-41,-30,.42f,-76,-28,Ease.Smooth,lean:8),
            G(0,10,8,1.06f,5,14,.52f,10,30,Ease.In,lean:-4,lift:.04f),
            G(.08f,55,0,1.08f,45,38,.46f,85,55,Ease.Out,lean:-8,lift:.08f),
            G(.17f,62,-4,1.04f,43,50,.44f,88,68,Ease.Out,lean:-6,lift:.04f),
            G(.25f,50,0,1,45,44,.43f,90,60,lean:-3),
        };
        /// <summary>The whirl: a deep crouch with the ring swung out wide, then a leap, the legs tucked, the whole body turning once in the
        /// air with the ring at arm's length (the hit as it sets off: the rules cut all round at once), landed in a crouch facing ahead.</summary>
        private static readonly Key[] Whirl=
        {
            G(-.2f,50,0,1,45,44,.43f,90,60,lean:-3),
            G(-.13f,40,22,.82f,80,-8,.46f,90,0,Ease.Out,spin:-25,lean:-4),
            G(-.06f,38,24,.8f,82,-10,.47f,92,-2,Ease.Smooth,spin:-35),
            G(0,5,6,1.12f,85,-2,.55f,92,0,Ease.In,spin:60,lift:.18f,tuck:.3f),
            G(.1f,5,2,1.1f,86,-2,.56f,92,0,Ease.Linear,spin:220,lift:.3f,tuck:.6f),
            G(.2f,5,6,1,84,-4,.55f,92,0,Ease.Out,spin:340,lift:.12f,tuck:.3f),
            G(.28f,10,20,.84f,60,-10,.48f,80,0,Ease.Out,spin:360),
            G(.36f,5,8,1,25,-10,.42f,40,0,Ease.Smooth,spin:360),
        };
        /// <summary>The flick: wound right across to the left hip, whipped out ahead (the release: the ring leaves on its loop), the open
        /// hand held out for it, the catch at FlickTime after the release yanking the arm back.</summary>
        private static readonly Key[] Flick=
        {
            G(-.16f,5,8,1,25,-10,.42f,40,0),
            G(-.1f,-48,14,.88f,-45,-20,.39f,-78,-10,Ease.Out,lean:6),
            G(-.07f,-52,16,.86f,-46,-22,.38f,-82,-12,Ease.Smooth,lean:7),
            G(0,18,18,1.08f,15,2,.53f,20,4,Ease.In,lean:-6),
            G(.1f,45,10,1.02f,25,6,.5f,40,8,Ease.Out,lean:-5),
            G(.4f,15,6,1,20,0,.48f,15,0),
            G(NativeGlaive.FlickTime,10,6,1,18,-4,.47f,5,0),
            G(NativeGlaive.FlickTime+.08f,-20,12,.92f,5,-12,.38f,-30,-10,Ease.Out,lean:4),
        };
        // ---- the rest (from their start) ----
        /// <summary>The skim: a low dive, the ring cocked low behind the right hip, swept across low at the hit, the body rising out of it.</summary>
        private static readonly Key[] SkimKeys=
        {
            G(0,10,10,1,25,-10,.42f,40,0),
            G(.06f,48,38,.82f,62,-40,.43f,102,-22,Ease.Out,lean:-6),
            G(.1f,50,40,.8f,64,-42,.44f,105,-24,Ease.Smooth,lean:-7),
            G(.18f,-20,34,.9f,10,-36,.52f,0,-14,Ease.In,lean:6),
            G(.3f,-60,18,1.02f,-40,-10,.42f,-80,5,Ease.Out,lean:7),
            G(.54f,0,6,1,20,-10,.42f,40,0),
        };
        /// <summary>The throw: a frisbee backhand, the ring wound right across the chest with the body coiled over it, then everything
        /// unwinds into the release at full reach and follows through to the right.</summary>
        private static readonly Key[] ThrowKeys=
        {
            G(0,0,4,1,20,-10,.42f,40,0),
            G(.055f,-52,16,.86f,-45,-6,.35f,-85,0,Ease.Out,lean:6),
            G(.075f,-55,18,.84f,-46,-5,.34f,-88,0,Ease.Smooth,lean:7),
            G(NativeGlaive.ThrowRelease,20,20,1.08f,8,4,.53f,18,4,Ease.In,lean:-6),
            G(.28f,62,10,1.02f,38,6,.46f,58,8,Ease.Out,lean:-7),
            G(.42f,15,4,1,25,-8,.44f,30,0),
        };
        /// <summary>The halo: a dip to gather, the ring swept up overhead on a stretch onto the toes and flung up off the fingertips.</summary>
        private static readonly Key[] HaloKeys=
        {
            G(0,0,4,1,20,-10,.42f,40,0),
            G(.08f,15,12,.88f,20,15,.36f,30,50,Ease.Out),
            G(.16f,0,-10,1.1f,5,72,.5f,0,92,Ease.In,lift:.06f),
            G(NativeGlaive.HaloRelease,0,-12,1.12f,4,80,.52f,0,96,Ease.Smooth,lift:.08f),
            G(.34f,0,-4,1.02f,8,58,.48f,0,80,Ease.Out,lift:.02f),
            G(.48f,0,4,1,18,-15,.38f,20,0),
        };
        /// <summary>The burst: the fist drawn back, then punched out at the ring.</summary>
        private static readonly Key[] BurstKeys=
        {
            G(0,0,4,1,15,-15,.36f,20,0),
            G(.05f,25,8,.9f,25,-8,.32f,40,0,Ease.Out),
            G(.1f,-12,18,1.06f,5,0,.53f,0,0,Ease.In,lean:3),
            G(.3f,0,4,1,15,-15,.38f,20,0),
        };
        /// <summary>The catch: the arm yanked back by the ring's pull, the chest turned with it, then brought round to the ready.</summary>
        private static readonly Key[] CatchKeys=
        {
            G(0,10,4,1,25,-5,.5f,30,0),
            G(.06f,-28,14,.9f,5,-14,.34f,-25,-10,Ease.Out,lean:5),
            G(.16f,10,6,1.02f,25,-8,.42f,45,6,Ease.Out),
            G(CatchTime,8,4,1,35,-20,.37f,15,40),
        };
        /// <summary>The ready hold between moves: the ring up at the right side, upright, its edge forward (the clip's sword grip laid it flat
        /// round the hip).</summary>
        private static readonly Key[] ReadyKeys={G(0,8,4,1,35,-20,.37f,15,40),G(1,8,4,1,35,-20,.37f,15,40)};
        public const float ReadyShare=.9f;
        // ---- the intro (Core NativeGlaiveIntro, "Crescent Return", on its own clock) ----
        /// <summary>The ring's arm through the intro: reaching up for it as the pilot drops in, the catch high in the air, the whirl as it
        /// falls (all spin from here on: a full turn hands back without unwinding), the landing crouch, the fling overhead, the free hand
        /// conducting the orbit, the catch's recoil, one big forehand, and the hero hold: the ring upright beside the face.</summary>
        private static readonly Key[] IntroKeys=
        {
            G(NativeGlaiveIntro.DropAt-.05f,10,-10,1.1f,25,70,.52f,0,85),
            G(NativeGlaiveIntro.CatchAt,8,-8,1.08f,25,72,.52f,0,88),
            G(NativeGlaiveIntro.CatchAt+.06f,10,0,.96f,30,60,.46f,20,70,Ease.Out),
            G(NativeGlaiveIntro.CatchAt+.12f,20,6,1,70,10,.52f,85,10,Ease.Smooth),
            G(2.0f,10,8,1,86,-2,.55f,92,0,Ease.Linear,spin:300),
            G(NativeGlaiveIntro.LandAt,10,22,.84f,70,-15,.48f,80,-5,Ease.Out,spin:360),
            G(NativeGlaiveIntro.LandAt+.3f,8,6,1,35,-10,.42f,40,0,spin:360),
            G(NativeGlaiveIntro.FlingAt-.07f,15,12,.88f,20,15,.36f,30,50,Ease.Out,spin:360),
            G(NativeGlaiveIntro.FlingAt,0,-12,1.12f,4,80,.52f,0,96,Ease.In,spin:360),
            G(NativeGlaiveIntro.OrbitFrom,20,0,1,35,40,.45f,10,20,Ease.Out,spin:360),
            G(NativeGlaiveIntro.OrbitTo,-20,2,1,20,25,.45f,-20,10,spin:360),
            G(NativeGlaiveIntro.ReturnAt-.08f,10,4,1,30,0,.5f,30,0,spin:360),
            G(NativeGlaiveIntro.ReturnAt,10,4,1,25,-5,.5f,30,0,spin:360),
            G(NativeGlaiveIntro.ReturnAt+.06f,-25,14,.9f,5,-14,.34f,-25,-10,Ease.Out,spin:360,lean:5),
            G(NativeGlaiveIntro.CoilAt,62,12,.9f,50,4,.38f,106,10,Ease.Out,spin:360,lean:-7),
            G(NativeGlaiveIntro.SlashAt-.06f,68,14,.88f,50,5,.4f,110,12,Ease.Smooth,spin:360,lean:-8),
            G(NativeGlaiveIntro.SlashAt,-5,14,1.05f,10,-2,.52f,10,-2,Ease.In,spin:360,lean:5),
            G(NativeGlaiveIntro.SlashAt+.1f,-55,12,1.02f,-30,-4,.46f,-55,-6,Ease.Out,spin:360,lean:8),
            G(NativeGlaiveIntro.SlashAt+.25f,-62,9,1,-40,-6,.42f,-80,-4,Ease.Out,spin:360,lean:5),
            G(NativeGlaiveIntro.StandAt,15,2,1,30,10,.3f,5,80,Ease.Smooth,spin:360),
            G(NativeGlaiveIntro.Total,15,2,1,30,10,.3f,5,80,spin:360),
        };
        /// <summary>The intro's arm and chest at intro time `t` (its share of the body: none before the drop).</summary>
        public static bool Intro(float t,out DogMalletPose pose)
        {
            pose=default;float from=NativeGlaiveIntro.DropAt-.05f;if(t<from)return false;pose=KeyedPose(IntroKeys,t,true,Scale);pose.Weight=Math.Min(1,(t-from)/.1f);return true;
        }
        /// <summary>The pose for an actor at `time`: its move's (Sample), else the ready hold while it stands, walks or runs ready (not
        /// guarding, rolling, stunned, reeling or dead, and with the ring in the hand).</summary>
        public static bool Pose(Actor a,NativeGlaiveKit k,float time,out DogMalletPose pose,out Move move,out float from)
        {
            if(Sample(k,time,out pose,out move,out from))return true;
            if(a==null||k==null||!a.Alive||NativeGlaive.InHand(k,time)<.5f||a.Rolling(time)||time>=a.BlockAt&&time<a.BlockUntil||time<a.StunUntil||time>=a.ReactionAt&&time<a.ReactionUntil)return false;
            move=Move.Ready;from=-1;pose=KeyedPose(ReadyKeys,0,false,Scale);pose.Weight=ReadyShare;return true;
        }
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
                case Move.Halo:return KeyedPose(HaloKeys,t,false,Scale);case Move.Burst:return KeyedPose(BurstKeys,t,false,Scale);case Move.Catch:return KeyedPose(CatchKeys,t,true,Scale);case Move.Ready:return KeyedPose(ReadyKeys,t,false,Scale);case Move.Intro:return KeyedPose(IntroKeys,t,true,Scale);
            }
            return default;
        }
        public static float[] KeyTimes(Move m)
        {
            Key[] k=m==Move.Cut1?Cut1:m==Move.Cut2?Cut2:m==Move.Whirl?Whirl:m==Move.Flick?Flick:m==Move.Skim?SkimKeys:m==Move.Throw?ThrowKeys:m==Move.Halo?HaloKeys:m==Move.Burst?BurstKeys:m==Move.Catch?CatchKeys:m==Move.Ready?ReadyKeys:m==Move.Intro?IntroKeys:new Key[0];
            var t=new float[k.Length];for(int i=0;i<k.Length;i++)t[i]=k[i].T;return t;
        }
        /// <summary>The ring's middle on the nominal body for a pose (for the checks and the trail).</summary>
        public static Vector3 RingAt(in DogMalletPose p)=>GripAt(p)+HandleAt(p)*NativeWeaponModels.GlaiveCentre;
    }
}
