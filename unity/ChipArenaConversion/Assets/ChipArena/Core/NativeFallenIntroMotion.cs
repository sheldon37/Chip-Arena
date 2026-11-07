using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The intro's body layer at one moment (NativeFallenIntroMotion.At): what the cutscene's key poses add on top of the stance
    /// clip and the kneel preset. Lengths are metres in the boss's own frame (x its right, y up from the water, z its front),
    /// angles degrees. Runtime NativeFallenBossBody.cs applies it with the rig's IK.
    /// </summary>
    public struct FallenIntroPose
    {
        public bool On;
        /// <summary>The kneel preset's weight on the legs and on the spine, head and free arm (1 kneeling, bowed).</summary>
        public float KneelLegs,KneelUpper;
        /// <summary>The hips' travel from where the boss stands (forward over the front foot as it rises, down into the lunge).</summary>
        public Vector3 Root;
        /// <summary>The torso pitched forward (+), twisted to its right (+); the head nodded down (+); the chest's breath (-1..1).</summary>
        public float Lean,Twist,Head,Breath;
        /// <summary>The right foot: held where it stood when the rise began (weight 0..1).</summary>
        public float FootR;
        /// <summary>The left foot: held where it knelt, then (by LeftBlend) on its keyed spot, with a step's lift. The spot's z is
        /// measured from the right foot's held spot, so the stance stays square to the front foot.</summary>
        public float FootL,LeftBlend,LeftLift;public Vector3 LeftSpot;
        /// <summary>The planted sword drawn up out of the water (NativeFallenSongPlant.LiftBy times this), and how far the keyed path
        /// has the fist (0 the planted grip, 1 the key).</summary>
        public float SwordLift,Sword;
        /// <summary>The keyed fist on the grip, the blade's way and a hint for its flat's normal (the edge leads the cut).</summary>
        public Vector3 Grip,Blade,Flat;
        /// <summary>The free (left) hand's target from the hips, and its weight (the strike's balance).</summary>
        public float LeftHand;public Vector3 LeftHandAt;
        /// <summary>The top wing pair opened further (the windup), the wings thrown (the impact), the blade's trail.</summary>
        public float WingTop,Flare,Trail;
        /// <summary>The body is the owner's reference clips (Core NativeFallenIntroMocap), sampled at MocapAt (the cutscene's seconds),
        /// and the keys above stand down; Plant is the fist's weight on the planted sword (until the reference's pull).</summary>
        public bool Mocap;public float MocapAt,Plant;
    }

    /// <summary>
    /// NOCTIEL's intro choreography (owner's four reference clips, 2026-11-06, option A; docs/EXPEDITION-BOSS-FINAL.md section 18):
    /// the feather's fall, the kneel's breath and the head lifting, a weighted rise over the planted front foot with the back foot
    /// stepping up, the pull, the sword wheeled up on its right side to a salute, out to the side, raised up the side to a cocked
    /// windup, the load and a 0.20 s downswing into a lunge that lands on the drop. Keyed to the cutscene's existing beats
    /// (NativeFallenBossPreview), so the music, cues, shots and effects keep their times. Pure: the same clock, the same pose.
    /// Presentation only; no rule reads it.
    /// </summary>
    public static class NativeFallenIntroMotion
    {
        public const float PullFrom=47.3f,PullTo=47.7f,KeyFrom=47.6f,KeyTo=47.75f,LoadAt=50.43f,TopAt=50.61f;
        /// <summary>The downswing, top to impact (owner: the reference's swing is slightly too slow; it took about 0.34 s).</summary>
        public const float Downswing=.20f;
        public static float ImpactAt=>NativeFallenPilots.CutsceneEnd;
        public const float RiseAt=NativeFallenBossPreview.RiseAt,StepFrom=36.2f,StepTo=37.0f,LungeFrom=50.55f,LungeTo=50.75f;

        // ---- the keys (times are the intro's seconds; the beats they hang on are the cutscene's own) ----
        private static readonly float[] RootT={RiseAt,35.6f,36.2f,37f,37.6f,47f,47.3f,47.7f,48.7f,49.2f,49.9f,LoadAt,TopAt,50.71f,50.81f};
        private static readonly Vector3[] RootV={V(0,0,0),V(0,-.04f,.18f),V(0,0,.42f),V(0,.03f,.7f),V(0,0,.72f),V(0,0,.72f),V(0,-.07f,.72f),V(0,.02f,.72f),V(0,0,.72f),V(0,-.02f,.72f),V(0,0,.66f),V(0,.03f,.62f),V(0,.04f,.62f),V(0,-.08f,.85f),V(0,-.32f,1.18f)};
        private static readonly float[] LeanT={RiseAt,35.6f,36.2f,37f,37.6f,47f,47.3f,47.7f,48.7f,49.2f,49.9f,LoadAt,TopAt,50.66f,50.71f,50.76f,50.81f};
        private static readonly float[] LeanV={0,16,12,4,0,0,5,-2,-3,0,-6,-8,-9,-4,8,18,24};
        private static readonly float[] TwistT={48.4f,48.7f,49.1f,49.9f,LoadAt,TopAt,50.71f,50.81f};
        private static readonly float[] TwistV={0,6,10,14,16,16,4,-14};
        private static readonly float[] HeadT={TopAt,50.81f};private static readonly float[] HeadV={0,10};
        private static readonly float[] KneelLegsT={35.6f,36.2f,37f};private static readonly float[] KneelLegsV={1,.45f,0};
        private static readonly float[] KneelUpperT={33.5f,35f,35.6f,36.2f};private static readonly float[] KneelUpperV={1,.55f,.15f,0};
        // The sword: the pull's lift, then the keyed fist. The wheel turns the blade on its right side (down, forward, up), the flat's
        // normal turning with it; then out and down to the side; up the side as the wrist rolls the edge forward; cocked overhead
        // and back; the load to vertical; the cut forward and down to the water toward the lens.
        private static readonly float[] LiftT={PullFrom,PullTo};private static readonly float[] LiftV={0,.55f};
        private static readonly float[] KeyT={KeyFrom,KeyTo,48.05f,48.35f,48.7f,48.9f,49.1f,49.2f,49.62f,49.9f,LoadAt,TopAt,50.66f,50.71f,50.76f,50.81f};
        private static readonly Vector3[] GripV={V(.85f,2.67f,.72f),V(.85f,2.8f,.74f),V(.86f,2.92f,.92f),V(.86f,2.98f,1.02f),V(.85f,2.85f,.9f),V(1.05f,2.8f,.82f),V(1.3f,2.55f,.76f),V(1.28f,2.45f,.74f),
            V(1.05f,3.35f,.66f),V(.48f,3.78f,.62f),V(.45f,3.86f,.56f),V(.42f,3.98f,.6f),V(.4f,3.86f,.95f),V(.33f,3.4f,1.45f),V(.22f,2.62f,1.82f),V(.12f,1.85f,1.95f)};
        private static readonly Vector3[] BladeV={V(0,-1,0),V(0,-1,.06f),V(0,-.54f,.84f),V(0,.09f,1f),V(0,1f,.09f),V(.71f,.71f,0),V(1f,-.035f,0),V(.99f,-.14f,-.03f),
            V(.17f,.98f,-.05f),V(.1f,.7f,-.71f),V(.08f,.62f,-.78f),V(.04f,.99f,.1f),V(.02f,.93f,.37f),V(-.06f,.55f,.83f),V(-.15f,-.05f,.99f),V(-.22f,-.6f,.77f)};
        private static readonly Vector3[] FlatV={V(0,0,1),V(0,.06f,1),V(0,.84f,.54f),V(0,1f,-.09f),V(0,.09f,-1),V(0,0,-1),V(0,0,-1),V(0,0,-1),
            V(0,.05f,-1),V(1,0,.14f),V(1,0,.1f),V(1,0,0),V(1,0,0),V(1,0,.05f),V(.98f,0,.15f),V(.96f,0,.27f)};
        // The free hand thrown back for balance through the cut.
        private static readonly float[] LeftHandT={LoadAt,50.7f};private static readonly float[] LeftHandV={0,1};
        private static readonly float[] WingTopT={49.45f,49.9f,50.7f,50.81f};private static readonly float[] WingTopV={0,.25f,.25f,.35f};
        private static readonly float[] LeftLiftT={StepFrom,36.6f,StepTo,LungeFrom,50.65f,LungeTo};private static readonly float[] LeftLiftV={0,.28f,0,0,.22f,0};
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        public static readonly Vector3 LeftStand=V(-.48f,0,-.04f),LeftLunge=V(-.56f,0,.92f),LeftHandFromHips=V(-.85f,2.15f,-.3f);

        /// <summary>The intro's body layer at p (the cutscene's seconds, 0..CutsceneEnd); off outside it.</summary>
        public static FallenIntroPose At(float p)
        {
            var o=new FallenIntroPose();if(!(p>=0&&p<=ImpactAt))return o;o.On=true;
            o.KneelLegs=p<KneelLegsT[0]?1:Key(KneelLegsT,KneelLegsV,p);o.KneelUpper=p<KneelUpperT[0]?1:Key(KneelUpperT,KneelUpperV,p);
            o.Root=Key(RootT,RootV,p);o.Lean=Key(LeanT,LeanV,p);o.Twist=Key(TwistT,TwistV,p);o.Head=Key(HeadT,HeadV,p);
            // Kneeling and standing it breathes: slow, a little slower while it kneels.
            o.Breath=(float)Math.Sin(2*Math.PI*p/(p<RiseAt?4.6f:3.8f))*(1-Smooth((p-49.75f)/.4f));
            // The feet: the right holds from the rise on; the left holds where it knelt, steps up beside, and steps into the lunge.
            o.FootR=o.FootL=p>=RiseAt?1:0;o.LeftBlend=Smooth((p-StepFrom)/(StepTo-StepFrom));o.LeftLift=Key(LeftLiftT,LeftLiftV,p);
            o.LeftSpot=Vector3.Lerp(LeftStand,LeftLunge,Smooth((p-LungeFrom)/(LungeTo-LungeFrom)));
            o.SwordLift=Key(LiftT,LiftV,p);o.Sword=Smooth((p-KeyFrom)/(KeyTo-KeyFrom));
            o.Grip=Key(KeyT,GripV,p);o.Blade=Normal(Key(KeyT,BladeV,p),Vector3.UnitY);o.Flat=Normal(Key(KeyT,FlatV,p),Vector3.UnitZ);
            o.LeftHand=Key(LeftHandT,LeftHandV,p);o.LeftHandAt=LeftHandFromHips;
            o.WingTop=Key(WingTopT,WingTopV,p);o.Flare=Smooth((p-50.72f)/.09f);o.Trail=Smooth((p-50.56f)/.05f);
            var m=NativeFallenIntroMocap.Active;
            if(m!=null)
            {
                // The extracted motion carries the body, the feet, both arms and the sword's path: the keyed layer stands down. While the
                // kneel is held for the cutscene's length the chest keeps the keyed breath (a held frame would look frozen); after, none.
                o.Mocap=true;o.MocapAt=p;o.KneelLegs=o.KneelUpper=0;o.Root=Vector3.Zero;o.Lean=o.Twist=o.Head=0;o.FootR=o.FootL=0;o.LeftBlend=o.LeftLift=0;o.LeftSpot=Vector3.Zero;
                o.SwordLift=0;o.Sword=0;o.LeftHand=0;o.Breath=p<m.HoldEnd?o.Breath*(1-Smooth((p-m.HoldEnd+.6f)/.6f)):0;o.Plant=m.PlantAt(p);
            }
            return o;
        }
        /// <summary>When the fist takes the sword from the water: the reference's pull when the extracted motion plays, else the cut's beat.</summary>
        public static float PullTime=>NativeFallenIntroMocap.Active?.PullAt??NativeFallenBossPreview.PullAt;

        // ---- the feather (clip 01): a falling leaf ----
        /// <summary>The cutscene's one feather at p: it rocks side to side like a falling leaf, each swing slowing at its turn and
        /// dipping through its middle, smaller as it nears the water; it lies flat on the water at FeatherLand and floats there,
        /// bobbing a little, until the shot cuts. pos is in the shot's frame (Cutscene's), yaw/pitch/roll in degrees.</summary>
        public static void Feather(float p,out Vector3 pos,out float yaw,out float pitch,out float roll,out bool shown)
        {
            const float from=1.4f,period=1.55f;float land=NativeFallenBossPreview.FeatherLand;shown=p<11;
            float u=Clamp01((p-from)/(land-from));float phase=(float)(2*Math.PI*(p-from)/period);float swing=(float)Math.Sin(phase),turn=(float)Math.Cos(phase);
            float reach=.55f*(float)Math.Pow(1-u,.6f)+.04f*(1-u);
            // It falls faster through each swing's bottom than at its turns.
            // Like a pendulum it rides higher at each turn of its swing and lowest through the middle, and tilts up into each turn.
            float y=3.7f*(1-u)+.07f*(1-u)*(1-Math.Abs(turn));if(p>=land){float a=p-land;y=.004f*(float)Math.Sin(a*6.5f)*(float)Math.Exp(-a*2.2f);reach=0;}
            if(y<0)y=0;
            pos=new Vector3(.25f+reach*swing,y,-50);
            float tilt=(float)Math.Pow(1-u,.5f);yaw=p*12;roll=p>=land?0:26*swing*tilt;pitch=p>=land?0:9*turn*tilt;
        }

        // ---- keys: a cubic through them (Catmull-Rom inside, flat at a turning point, one-sided at the ends) ----
        public static float Key(float[] t,float[] v,float p)
        {
            int n=t.Length;if(p<=t[0])return v[0];if(p>=t[n-1])return v[n-1];int i=0;while(i<n-2&&p>t[i+1])i++;
            float h=t[i+1]-t[i],u=(p-t[i])/h;
            float m0=i>0?(v[i+1]-v[i-1])/(t[i+1]-t[i-1]):(v[1]-v[0])/(t[1]-t[0]),m1=i+2<n?(v[i+2]-v[i])/(t[i+2]-t[i]):(v[n-1]-v[n-2])/(t[n-1]-t[n-2]);
            if(i>0&&(v[i]-v[i-1])*(v[i+1]-v[i])<=0)m0=0;if(i+2<n&&(v[i+1]-v[i])*(v[i+2]-v[i+1])<=0)m1=0;
            float u2=u*u,u3=u2*u;return (2*u3-3*u2+1)*v[i]+(u3-2*u2+u)*h*m0+(-2*u3+3*u2)*v[i+1]+(u3-u2)*h*m1;
        }
        public static Vector3 Key(float[] t,Vector3[] v,float p)=>new Vector3(KeyAxis(t,v,p,0),KeyAxis(t,v,p,1),KeyAxis(t,v,p,2));
        private static float KeyAxis(float[] t,Vector3[] v,float p,int c)
        {
            float G(int i)=>c==0?v[i].X:c==1?v[i].Y:v[i].Z;
            int n=t.Length;if(p<=t[0])return G(0);if(p>=t[n-1])return G(n-1);int i=0;while(i<n-2&&p>t[i+1])i++;
            float h=t[i+1]-t[i],u=(p-t[i])/h,a=G(i),b=G(i+1);
            float m0=i>0?(b-G(i-1))/(t[i+1]-t[i-1]):(G(1)-G(0))/(t[1]-t[0]),m1=i+2<n?(G(i+2)-a)/(t[i+2]-t[i]):(G(n-1)-G(n-2))/(t[n-1]-t[n-2]);
            if(i>0&&(a-G(i-1))*(b-a)<=0)m0=0;if(i+2<n&&(b-a)*(G(i+2)-b)<=0)m1=0;
            float u2=u*u,u3=u2*u;return (2*u3-3*u2+1)*a+(u3-2*u2+u)*h*m0+(-2*u3+3*u2)*b+(u3-u2)*h*m1;
        }
        private static Vector3 Normal(Vector3 v,Vector3 fallback){float l=v.Length();return l>1e-5f?v/l:fallback;}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
    }
}
