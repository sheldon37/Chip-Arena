using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The base of the five generic intros (owner 2026-11-20: "5 new intros ... a much higher quality standard, great animations and
    /// effects, non-block based background and designs ... With great camera work"; docs/INTROS-GENERIC.md): Cloudbreak (the new
    /// default), Whiteout, Slipstream, Waygate and Flashbulb. Weapon-agnostic: empty hands, the body keyed on the intro rig (Keys and
    /// Steps) and drawn on the model. Each has its own modelled set in the local bank ChipArenaIntroGeneric (tools/intros/generic_sets.py,
    /// never committed), authored in Unity space with the hero mark at the origin: <see cref="U"/> and the turn helpers carry its
    /// coordinates into the source frame the set is drawn in. Without the bank each falls back to the dark lit void, tinted to it.
    /// Presentation only: pure functions of the presentation clock, nothing kept between frames, nothing allocated per frame.
    /// </summary>
    public abstract class NativeIntroGenericScript:NativeIntroV2Script
    {
        /// <summary>The five generic intros' set bank (Resources/ChipArenaIntroGeneric.bytes, local).</summary>
        public const string Bank="ChipArenaIntroGeneric";
        protected NativeIntroGenericScript(string id,float total,float cardAt,float hit,int accent):base(id,total,cardAt,hit,accent){}
        public override string SetBank=>Bank;
        /// <summary>The intro's name (the Locker card and the docs; the name card shows the pilot's).</summary>
        public abstract string Title{get;}

        // ---- the set's frame: authored in Unity space (x mirrored against the source frame) ----
        /// <summary>A point of the set bank's Unity space in the source frame.</summary>
        protected static Vector3 U(float x,float y,float z)=>new Vector3(-x,y,z);
        protected static Vector3 U(Vector3 unity)=>new Vector3(-unity.X,unity.Y,unity.Z);
        /// <summary>A Unity-space turn (a quaternion of the bank's frame) as the source frame's (the runtime mirrors it back).</summary>
        protected static Quaternion UTurn(Quaternion unity)=>new Quaternion(unity.X,-unity.Y,-unity.Z,unity.W);
        /// <summary>A Unity-space yaw about +y (Unity's turn: +z toward +x) as the source frame's rotation.</summary>
        protected static Quaternion UYaw(float yaw)=>Quaternion.CreateFromAxisAngle(Vector3.UnitY,-yaw);
        /// <summary>A Unity-space turn about +z (a ring's spin seen from +z) as the source frame's rotation.</summary>
        protected static Quaternion URoll(float roll)=>Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-roll);
        protected static readonly Quaternion Still=Quaternion.Identity;
        protected static readonly Vector3 White=Vector3.One;

        // ---- the closing frame and the head ----
        /// <summary>The name card's height (a screen share from the top; Runtime/NativeIntroCardScreen.cs centres the name there, its
        /// kicker and line above and below): a band under the feet. The closing frames show the whole figure above it (the crown near
        /// .2 of the height, the soles near .72), so the name sits on the floor in front of the pilot, never on its legs.</summary>
        public const float CardY=.855f;
        /// <summary>The head anchor is the head joint's pivot at the skull's base: the eyes sit about this far above it (I5's measure).</summary>
        protected const float HeadLift=.16f;
        /// <summary>A point `distance` m out from the mark toward the pilot's front, turned side*.42 rad toward its left (the
        /// framework's hero angle; side .524 is straight down +z), `height` m up.</summary>
        protected static Vector3 Front(float side,float distance,float height)
        {
            float yaw=NativeProceduralEntrance.Facing+side*.42f;return new Vector3((float)Math.Sin(yaw)*distance,height,(float)Math.Cos(yaw)*distance);
        }
        /// <summary>The closing push for the name, added as the rig's next shot: from `side` (as Hero's) a slow push in to `distance`,
        /// the eye `eye` m up, looking at the mark `look` m up, easing out to stillness; it comes in on a blend of `blend` s.</summary>
        protected void Closing(float at,float side,float distance,float eye,float look,float fov,float blend)
        {
            Rig.Dolly(at,Front(side,distance+.45f,eye-.06f),Front(side,distance,eye),new Vector3(0,look-.04f,0),new Vector3(0,look,0),fov+2,fov)
               .Ease(NativeIntroEase.Out).In(NativeIntroCut.Blend,blend);
        }

        // ---- the camera this frame, for streaks laid onto their head's camera plane ----
        private NativeEntranceView view;private bool viewSet;
        /// <summary>Reads this frame's camera (the rig at `t`, as Camera draws it) before effects that lay long streaks.</summary>
        protected void Look(float t){float settle=Clock(SettleAt);Rig.Evaluate(this,t<settle?t:settle,Context.Reduced,out view);viewSet=true;}
        /// <summary>A streak from `head` back along `axis` (its whole length; any length): one longer than
        /// NativeCacheOpenFx.ExactStreak is laid onto the plane of its head facing this frame's camera (Look first), so its ends
        /// stay on its line however the camera looks.</summary>
        protected void Line(INativeEntranceFx fx,Vector3 head,Vector3 axis,Vector3 color,float width,float taper,float u,float seed)
        {
            if(viewSet&&axis.LengthSquared()>NativeCacheOpenFx.ExactStreak*NativeCacheOpenFx.ExactStreak)
            {
                var f=view.Look-view.Eye;float l=f.Length();
                if(l>1e-5f)
                {
                    f/=l;float ax=axis.X,ay=axis.Y,az=axis.Z;
                    if(NativeCacheOpenFx.OntoHeadPlane(view.Eye.X,view.Eye.Y,view.Eye.Z,f.X,f.Y,f.Z,.2f,head.X,head.Y,head.Z,ref ax,ref ay,ref az,ref taper))axis=new Vector3(ax,ay,az);
                }
            }
            fx.Streak(head,axis,color,width,.01f,1,taper,u,seed);
        }
        /// <summary>The eye of this frame's camera and the point it looks at (after Look).</summary>
        protected Vector3 Eye=>view.Eye;
        protected Vector3 LookAt=>view.Look;
        /// <summary>Whether point p lies within r m of this frame's eye (after Look): an effect that close would fill the lens.</summary>
        protected bool Near(Vector3 p,float r)=>viewSet&&(p-view.Eye).LengthSquared()<r*r;

        // ---- small shared shapes ----
        /// <summary>0..1 on a beat `at`, eased in over `rise` and out over `fall` (a light's or a glow's level).</summary>
        protected static float Swell(float t,float at,float rise,float hold,float fall)=>NativeEntranceMath.Ramp(t,at,at+rise)*(1-NativeEntranceMath.Ramp(t,at+rise+hold,at+rise+hold+fall));
        /// <summary>A slow blink (period `period`, lit for `duty` of it), soft-edged so it never flashes (well under 3 Hz).</summary>
        protected static float Blink(float t,float period,float duty,float phase=0)
        {
            float u=((t+phase)/period)%1f;if(u<0)u+=1;float edge=.08f;
            return NativeEntranceMath.Smooth(u/edge)*(1-NativeEntranceMath.Smooth((u-duty)/edge));
        }
        /// <summary>
        /// The fallback without the bank, in the intro's own light (its Lights never depend on the bank, so the frame is the same
        /// whichever draws): a floor far wider than the fog's reach (no horizon line), a softer skirt under it, and a layered pool
        /// of light where the pilot stands, with a faint glow on the horizon behind.
        /// </summary>
        protected static void Plain(INativeIntroSet set,float t,Vector3 floor,Vector3 pool,float poolLevel,Vector3 horizon)
        {
            float y=NativeEntranceStage.FloorY;
            set.Primitive(WeaponShape.Sphere,false,new Vector3(0,y-.03f,0),Quaternion.Identity,new Vector3(900,.06f,900),floor);
            set.Primitive(WeaponShape.Sphere,false,new Vector3(0,y-1.2f,0),Quaternion.Identity,new Vector3(1400,2,1400),floor*.7f);
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,y+.002f,0),Quaternion.Identity,new Vector3(3.6f,.002f,3.6f),pool*(.03f*poolLevel));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,y+.003f,0),Quaternion.Identity,new Vector3(2.0f,.002f,2.0f),pool*(.05f*poolLevel));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,y+.004f,0),Quaternion.Identity,new Vector3(.95f,.002f,.95f),pool*(.07f*poolLevel));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,6,-160),Quaternion.Identity,new Vector3(260,22,.5f),horizon*.04f);
        }
        /// <summary>The fraction `quality` keeps of `n` effect elements (at least `min`).</summary>
        protected static int Thin(int n,float quality,int min=1)=>Math.Max(min,(int)(n*(.35f+.65f*NativeEntranceMath.Clamp01(quality))));
    }
}
