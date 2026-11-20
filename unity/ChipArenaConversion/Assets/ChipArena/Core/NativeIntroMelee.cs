using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The melee weapon intros (intros v2, owner 2026-11-20: "a much higher quality standard, great animations and effects, non-block
    /// based background and designs ... With great camera work"; docs/INTROS-MELEE.md, the contract docs/INTROS-V2.md). Each is an
    /// authored scene on its own modelled set in the local bank ChipArenaIntroMelee (tools/intros/melee_set.py, never committed; without
    /// it the dark lit void, tinted to the intro). Shared here: the set's frame (authored in Unity space, mirrored into the source frame),
    /// this frame's camera for streaks laid onto their head's camera plane, and small shapes. Presentation only.
    /// </summary>
    public static class NativeIntroMelee
    {
        /// <summary>The melee intros' set bank (Resources/ChipArenaIntroMelee.bytes, local).</summary>
        public const string Bank="ChipArenaIntroMelee";
        /// <summary>A point of the set bank's Unity space in the source frame (x mirrored), its floor raised onto the stage floor.</summary>
        public static Vector3 U(float x,float y,float z)=>new Vector3(-x,y+NativeEntranceStage.FloorY,z);
        /// <summary>A Unity-space direction or offset in the source frame.</summary>
        public static Vector3 UDir(float x,float y,float z)=>new Vector3(-x,y,z);
        /// <summary>Where a whole set modelled in the set frame is drawn: its floor on the stage floor.</summary>
        public static readonly Vector3 SetAt=new Vector3(0,NativeEntranceStage.FloorY,0);
        public static readonly Vector3 White=Vector3.One;
        /// <summary>0..1 on a beat `at`, eased in over `rise`, held, and out over `fall`.</summary>
        public static float Swell(float t,float at,float rise,float hold,float fall)=>NativeEntranceMath.Ramp(t,at,at+rise)*(1-NativeEntranceMath.Ramp(t,at+rise+hold,at+rise+hold+fall));
        /// <summary>The share of `n` effect elements the quality keeps (at least `min`).</summary>
        public static int Thin(int n,float quality,int min=1)=>Math.Max(min,(int)(n*(.35f+.65f*NativeEntranceMath.Clamp01(quality))));
        /// <summary>A streak from `head` back along `axis` (its whole length): one longer than NativeCacheOpenFx.ExactStreak is laid onto the
        /// plane of its head facing the camera `view`, so its ends stay on its line however the camera looks.</summary>
        public static void Line(INativeEntranceFx fx,in NativeEntranceView view,bool viewSet,Vector3 head,Vector3 axis,Vector3 color,float width,float taper,float u,float seed)
        {
            if(viewSet&&axis.LengthSquared()>NativeCacheOpenFx.ExactStreak*NativeCacheOpenFx.ExactStreak)
            {
                var f=view.Look-view.Eye;float l=f.Length();
                if(l>1e-5f)
                {
                    f/=l;float ax=axis.X,ay=axis.Y,az=axis.Z;
                    if(NativeCacheOpenFx.OntoHeadPlane(view.Eye.X,view.Eye.Y,view.Eye.Z,f.X,f.Y,f.Z,.2f,head.X,head.Y,head.Z,ref ax,ref ay,ref az,ref taper))axis=new Vector3(ax,ay,az);
                    else return;
                }
            }
            fx.Streak(head,axis,color,width,.01f,1,taper,u,seed);
        }
        /// <summary>A whole set's fallback without the bank, in the intro's own light: a floor wider than the fog's reach, a skirt under it
        /// (no horizon line), a soft layered pool where the pilot stands and a faint glow far behind.</summary>
        public static void Plain(INativeIntroSet set,Vector3 floor,Vector3 pool,float poolLevel,Vector3 horizon)
        {
            float y=NativeEntranceStage.FloorY;
            set.Primitive(WeaponShape.Sphere,false,new Vector3(0,y-.03f,0),Quaternion.Identity,new Vector3(400,.06f,400),floor);
            set.Primitive(WeaponShape.Sphere,false,new Vector3(0,y-1.2f,0),Quaternion.Identity,new Vector3(700,2,700),floor*.7f);
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,y+.002f,0),Quaternion.Identity,new Vector3(3.6f,.002f,3.6f),pool*(.03f*poolLevel));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,y+.003f,0),Quaternion.Identity,new Vector3(2.0f,.002f,2.0f),pool*(.05f*poolLevel));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,y+.004f,0),Quaternion.Identity,new Vector3(.95f,.002f,.95f),pool*(.07f*poolLevel));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,6,-150),Quaternion.Identity,new Vector3(240,20,.5f),horizon*.04f);
        }
        /// <summary>A turn about the source frame's axis `axis` (unit) by `angle`.</summary>
        public static Quaternion Turn(Vector3 axis,float angle)=>Quaternion.CreateFromAxisAngle(axis,angle);
        /// <summary>The turn that lays +z along `d` (unit), keeping +y as near up as it can.</summary>
        public static Quaternion Along(Vector3 d)
        {
            var z=Vector3.Normalize(d);var x=Vector3.Cross(Vector3.UnitY,z);if(x.LengthSquared()<1e-6f)x=Vector3.UnitX;x=Vector3.Normalize(x);var y=Vector3.Cross(z,x);
            var m=new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
        }
    }

    /// <summary>
    /// The base of the melee intros whose model plays the pack's clips through the blade entrances' path (Core NativeBladeIntro: the
    /// runtime lays Clip on the model, holds the blades as the match does (NativeBladeGrip) and hands them in each frame; the Katana's saya
    /// by Sheathed), with the v2 contract on top: the camera rig and its speed ramps, the set from the bank with the void as its fallback,
    /// the name card, the lens, the cues. No pylon arena, no warp at Hit, no camera jolt but the rig's own moves.
    /// </summary>
    public abstract class NativeIntroMeleeBlade:NativeBladeIntro,INativeIntroV2,INativeIntroHands
    {
        public readonly float IntroTotal,IntroNameAt;
        public NativeIntroContext Context{get;set;}
        protected readonly NativeIntroCamera Rig=new NativeIntroCamera();
        protected readonly NativeIntroTime Ramps=new NativeIntroTime();
        protected NativeIntroMeleeBlade(string id,float total,float cardAt,float hit,int accent):base(id,hit,accent)
        {
            IntroTotal=total;IntroNameAt=cardAt;SlowRate=1;SlowHold=0;SlowRecover=0;SlowCatchUp=0;
            Context=new NativeIntroContext{Quality=1,Name="Pilot"};
        }
        public override float Length=>IntroTotal;public override float SettleAt=>IntroTotal-.25f;protected override float HeroAt=>IntroNameAt;
        public override float Clock(float age){if(!(age>0))return 0;return Ramps.Any?Ramps.Map(Math.Min(age,IntroTotal+1),IntroTotal+1):Math.Min(age,IntroTotal+1);}
        public override void Camera(float age,out NativeEntranceView view)
        {
            float t=Clock(Math.Min(age,SettleAt));Rig.Evaluate(this,t,Context.Reduced,out view);
            if(view.Eye.Y<NativeEntranceStage.FloorY+.06f)view.Eye.Y=NativeEntranceStage.FloorY+.06f;
        }
        public override Vector3 CameraJolt(float t)=>Vector3.Zero;
        public override int Boxes(float t,Span<Vector4> into)=>0;
        public override void Props(float t,INativeEntranceProps props,float quality){}
        public virtual string SetBank=>NativeIntroMelee.Bank;
        public virtual string Music=>null;
        public virtual NativeIntroCue[] Cues=>Array.Empty<NativeIntroCue>();
        public void Set(float t,INativeIntroSet set){if(!(set.Ready&&Scene(t,set)))Fallback(t,set);}
        /// <summary>The modelled set from the bank; false: the fallback instead (a piece missing).</summary>
        protected abstract bool Scene(float t,INativeIntroSet set);
        /// <summary>The set without the bank.</summary>
        protected abstract void Fallback(float t,INativeIntroSet set);
        public virtual void Card(float t,ref NativeIntroCardState card){card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(.96f,.98f,1),Accent);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);}
        /// <summary>
        /// The final hero frame: a slow push in from a low three-quarter front (`side` +1 from the pilot's left, -1 its right), easing to
        /// stillness, with the pilot off centre by `shift` m (+: the pilot on the frame's left third) so the name card sits in the clean
        /// space beside it (NameBeside) rather than over the legs.
        /// </summary>
        protected void HeroFrame(float at,float side,float height,float distance,float fov,float shift,float blend=.5f,float angle=.42f)
        {
            float yaw=Facing+side*angle;var dir=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));
            var from=dir*(distance+.45f)+new Vector3(0,height-.25f,0);var to=dir*distance+new Vector3(0,height-.18f,0);
            var look=new Vector3(0,height+.04f,0);var right=Vector3.Normalize(Vector3.Cross(look-to,Vector3.UnitY));
            Rig.Dolly(at,from,to,look+right*shift,look+right*shift+new Vector3(0,.02f,0),fov+2,fov).Ease(NativeIntroEase.Out).In(NativeIntroCut.Blend,blend);
        }
        /// <summary>The name card beside the pilot of a HeroFrame (+shift: on the right, -shift: on the left), a little smaller than the
        /// default so a long name stays clear of the body. (Unused while the card's kicker and line stay centred: Runtime
        /// NativeIntroCardScreen draws them at the screen's centre whatever X is, so a name moved aside splits from them.)</summary>
        protected static void NameBeside(ref NativeIntroCardState card,bool right){card.X=right?.71f:.29f;card.Y=.6f;card.Size=.064f;}
        /// <summary>
        /// The final hero frame, full length: a slow push in from a three-quarter front (`side` +1 from the pilot's left, -1 its right,
        /// `angle` rad off its facing), the eye a little over the waist and the look under it, so the pilot stands whole in the upper four
        /// fifths of the frame (its feet at about 0.8 of the height) and the name card has the clean band below them (NameBelow).
        /// </summary>
        protected void HeroFull(float at,float side,float distance,float fov,float angle=.4f,float blend=.5f)
        {
            float yaw=Facing+side*angle;var dir=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));
            var from=dir*(distance+.5f)+new Vector3(0,.92f,0);var to=dir*distance+new Vector3(0,1.0f,0);
            Rig.Dolly(at,from,to,new Vector3(0,.8f,0),new Vector3(0,.83f,0),fov+2,fov).Ease(NativeIntroEase.Out);if(blend>0)Rig.In(NativeIntroCut.Blend,blend);
        }
        /// <summary>The name card in the band under the hero frame's feet (HeroFull), centred with its kicker and line.</summary>
        protected static void NameBelow(ref NativeIntroCardState card){card.X=.5f;card.Y=.9f;card.Size=.062f;}
        public virtual void Lens(float t,out NativeIntroLens lens){Rig.Lens(this,t,Context.Reduced,out lens);}
        /// <summary>A flash level that is safe in Reduced effects (a pulse narrower than 1/6 s widened and dimmed there).</summary>
        protected float Flash(float t,float at,float attack,float decay,float gain=1)=>Context.Reduced?NativeEntranceMath.Pulse(t,at,Math.Max(attack,.08f),Math.Max(decay,.18f))*gain*.6f:NativeEntranceMath.Pulse(t,at,attack,decay)*gain;

        // ---- the hands as drawn (handed in before Props, Set and Effects; a checker never hands them in) ----
        private NativeIntroHand handR,handL;private float handsAt=-100;
        public void Hands(float t,in NativeIntroHand right,in NativeIntroHand left){handR=right;handL=left;handsAt=t;}
        /// <summary>The drawn wrist (0 the right, 1 the left) this frame: false when none was handed in for `t` (then the keyed anchor).</summary>
        protected bool Hand(float t,int hand,out Vector3 at)
        {
            if(Math.Abs(handsAt-t)<1e-4f){at=(hand==0?handR:handL).At;return true;}
            at=Anchor(hand==0?NativeEntranceAnchor.RightHand:NativeEntranceAnchor.LeftHand,t);return false;
        }

        // ---- the game rig in the game's own capture of a pack clip ----
        private readonly float[] captureRow=new float[CapturedClip.Channels];
        /// <summary>
        /// Poses the keyed rig (the anchors, the Classic body) as the game's own capture of a pack clip (Core CapturedClips: a melee set's
        /// idle, walk, cuts, block; `set` 0 sword, 1 daggers, 2 greatsword, 3 shield), the weapon hand on its captured grip. The model holds
        /// its weapon the way it is held on the first frame its clip plays (the visual body's hold: the game hand against the model's), so an
        /// intro whose model opens on the same pack clip as this capture holds it as the match does, where the two rigs play one clip too.
        /// </summary>
        protected void Captured(int set,int clip,float time,CapturedRig.Pose pose)
        {
            var c=CapturedClips.Sets[set][clip];float d=Math.Max(1e-3f,c.Duration);time%=d;if(time<0)time+=d;c.Sample(time,captureRow);CapturedRig.Sample(captureRow,set,pose);
        }

        // ---- this frame's camera (the rig at `t`, as Camera draws it), for streaks laid onto their head's camera plane ----
        protected NativeEntranceView View;protected bool ViewSet;
        protected void Look(float t){float settle=Clock(SettleAt);Rig.Evaluate(this,t<settle?t:settle,Context.Reduced,out View);ViewSet=true;}
        protected void Line(INativeEntranceFx fx,Vector3 head,Vector3 axis,Vector3 color,float width,float taper,float u,float seed)=>NativeIntroMelee.Line(fx,View,ViewSet,head,axis,color,width,taper,u,seed);
    }
}
