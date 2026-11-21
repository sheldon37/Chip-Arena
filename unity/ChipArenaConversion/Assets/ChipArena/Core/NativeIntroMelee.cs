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
    /// A keyed weapon path (owner 2026-11-20: bespoke animation, no match clips): where the hand holds the hilt and which way the blade
    /// points through the intro's beats, keyed in the pilot's frame (x its left, y up from the floor, z ahead; the keyed root's travel and
    /// lift carry it) and sampled with the keys' own curve (pass keys flow through, impact keys arrive travelling and ring out). The arm is
    /// solved onto it (NativeIntroMeleeBlade.Wield): the wrist at the grip, the blade along the hand's +z (the match's grip on the drawn
    /// hand follows the keyed wrist), the hand rolled toward the shoulder so the wrist stays natural. Presentation only; built once.
    /// </summary>
    public sealed class NativeIntroWield
    {
        private const int Stride=7;// grip xyz, blade xyz, weight
        private float[] times=Array.Empty<float>(),values=Array.Empty<float>();private uint pass,impact;private int count;
        /// <summary>Keyed in the keyed chest's frame (x its left, y up it, z ahead of it, from the chest joint; the keyed rig's proportions:
        /// shoulders at (±.345, .18, 0), the arm .52 long), so the hold rides the body's lean, drop and turn as a body carries a weapon;
        /// false: the pilot's frame over its floor spot (for a contact with the world: a blade in the floor).</summary>
        public bool Chest;
        public NativeIntroWield InChest(){Chest=true;return this;}
        public NativeIntroWield Key(float t,Vector3 grip,Vector3 blade,float weight=1)
        {
            Array.Resize(ref times,count+1);Array.Resize(ref values,(count+1)*Stride);times[count]=t;blade=Vector3.Normalize(blade);int o=count*Stride;
            values[o]=grip.X;values[o+1]=grip.Y;values[o+2]=grip.Z;values[o+3]=blade.X;values[o+4]=blade.Y;values[o+5]=blade.Z;values[o+6]=weight;count++;return this;
        }
        public NativeIntroWield Pass(){pass|=1u<<(count-1);return this;}
        public NativeIntroWield Impact(){impact|=1u<<(count-1);return this;}
        private float C(int c,float t)=>NativeEntranceMath.Curve(times,values,c,Stride,t,pass,impact);
        public void At(float t,out Vector3 grip,out Vector3 blade,out float weight)
        {
            if(count==0){grip=blade=Vector3.Zero;weight=0;return;}
            grip=new Vector3(C(0,t),C(1,t),C(2,t));blade=new Vector3(C(3,t),C(4,t),C(5,t));blade=blade.LengthSquared()>1e-8f?Vector3.Normalize(blade):Vector3.UnitZ;weight=NativeEntranceMath.Clamp01(C(6,t));
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
        public override float Length=>IntroTotal;public override float SettleAt=>IntroTotal-.25f;
        /// <summary>The living hold (breath, weight shift, a drifting head: NativeEntrancePose.Idle) from the first moments, under every key:
        /// the keyed holds before the action breathe too.</summary>
        protected override float HeroAt=>.25f;
        protected const int N=CapturedRig.Neck,LC=CapturedRig.LClav,RC=CapturedRig.RClav,LF=CapturedRig.LFoot,RF=CapturedRig.RFoot;
        /// <summary>Planted legs as true hinges (the v2 intros': no sideways knees).</summary>
        protected override bool HingedLegs=>true;
        /// <summary>The feet keyed on the model (docs/INTROS-V2.md 9c): Start and Steps are where the drawn ankles go, a Drop is the drawn
        /// hips' own metres; the model's legs are 1.7 times the keyed rig's, so keyed feet slid and keyed drops sank through the floor.</summary>
        protected override bool ModelFeet=>true;
        public override float Clock(float age){if(!(age>0))return 0;return Ramps.Any?Ramps.Map(Math.Min(age,IntroTotal+1),IntroTotal+1):Math.Min(age,IntroTotal+1);}
        public override void Camera(float age,out NativeEntranceView view)
        {
            float t=Clock(Math.Min(age,SettleAt));Rig.Evaluate(this,t,Context.Reduced,out view);Frame(t,ref view);
            if(view.Eye.Y<NativeEntranceStage.FloorY+.06f)view.Eye.Y=NativeEntranceStage.FloorY+.06f;
        }
        /// <summary>The rig's view at `t` moved where a shot frames the body as drawn (none by default); Camera and Look both take it.</summary>
        protected virtual void Frame(float t,ref NativeEntranceView view){}
        public override Vector3 CameraJolt(float t)=>Vector3.Zero;
        /// <summary>Builds the anchor table on the first frame, under the opening black, rather than at the first beat that reads an anchor
        /// (it poses the whole intro once: a hitch mid-intro on a session's first play otherwise). Call from Lights.</summary>
        protected void WarmAnchors(float t){if(t<.2f)Anchor(NativeEntranceAnchor.Root,0);}
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
        /// The final hero frame, full length: a slow push in from a three-quarter front (`side` +1 from the pilot's left, -1 its right,
        /// `angle` rad off its facing), the eye a little over the waist and the look under it, so the pilot stands whole in the upper four
        /// fifths of the frame (its feet at about 0.8 of the height) and the name card has the clean band below them (NameBelow).
        /// </summary>
        protected void HeroFull(float at,float side,float distance,float fov,float angle=.4f,float blend=.5f,float lift=0)
        {
            // (`lift`: eye and look raised together, for a weapon held high: the great sword's tip)
            float yaw=Facing+side*angle;var dir=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));
            var from=dir*(distance+.5f)+new Vector3(0,.92f+lift,0);var to=dir*distance+new Vector3(0,1.0f+lift,0);
            Rig.Dolly(at,from,to,new Vector3(0,.8f+lift,0),new Vector3(0,.83f+lift,0),fov+2,fov).Ease(NativeIntroEase.Out);if(blend>0)Rig.In(NativeIntroCut.Blend,blend);
        }
        /// <summary>The name card in the band under the hero frame's feet (HeroFull), centred with its kicker and line.</summary>
        protected static void NameBelow(ref NativeIntroCardState card){card.X=.5f;card.Y=.9f;card.Size=.062f;}
        public virtual void Lens(float t,out NativeIntroLens lens){Rig.Lens(this,t,Context.Reduced,out lens);}
        /// <summary>A flash level that is safe in Reduced effects (a pulse narrower than 1/6 s widened and dimmed there).</summary>
        protected float Flash(float t,float at,float attack,float decay,float gain=1)=>Context.Reduced?NativeEntranceMath.Pulse(t,at,Math.Max(attack,.08f),Math.Max(decay,.18f))*gain*.6f:NativeEntranceMath.Pulse(t,at,attack,decay)*gain;

        // ---- the hands as drawn (handed in before Props, Set and Effects; a checker never hands them in) ----
        private NativeIntroHand handR,handL;private float handsAt=-100;
        public void Hands(float t,in NativeIntroHand right,in NativeIntroHand left){handR=right;handL=left;handsAt=t;Record(t,0,right);Record(t,1,left);}
        /// <summary>The drawn wrist (0 the right, 1 the left) this frame: false when none was handed in for `t` (then the keyed anchor).</summary>
        protected bool Hand(float t,int hand,out Vector3 at)
        {
            if(Math.Abs(handsAt-t)<1e-4f){at=(hand==0?handR:handL).At;return true;}
            at=Anchor(hand==0?NativeEntranceAnchor.RightHand:NativeEntranceAnchor.LeftHand,t);return false;
        }

        // ---- the blades as drawn ----
        // The model's hand carries its weapon on the hand joint's frame as the runtime hands it in (Runtime NativeVisualBodyPresentation:
        // VisualJointsOf takes each joint's frame on its bone once the hands are on the weapon frames, and the motion keeps the game pose's
        // hold from its first frame), so a blade lies along that frame's +z, its tip TipLength out. The keyed rig's capture holds the
        // blade its own way: in the sword's idle some 40 degrees off the drawn one. So the effects on a blade take the drawn one, and its
        // last frames for the trails; the keyed anchors only where no model is drawn (the Classic body, a checker).
        private const int DrawnMax=64;
        private readonly Vector3[] drawnRoot=new Vector3[2*DrawnMax],drawnTip=new Vector3[2*DrawnMax];private readonly float[] drawnAt=new float[2*DrawnMax];
        private readonly int[] drawnCount=new int[2],drawnHead=new int[2];private float drawnClock=-1;
        private void Record(float t,int hand,in NativeIntroHand h)
        {
            if(t<drawnClock-1e-4f)drawnCount[0]=drawnCount[1]=0;// (a clock that runs back, a replay, starts the trails over)
            drawnClock=t;
            if(!h.Model||!float.IsFinite(h.At.X)||!float.IsFinite(h.Turn.W)){drawnCount[hand]=0;return;}
            // (the weapon anchors as drawn, the framework's: the weapon frame's origin and its tip socket; else along the learnt axis)
            float len=TipLength(hand==1);Vector3 root,tip;var gripAt=hand==1?NativeEntranceAnchor.LeftGrip:NativeEntranceAnchor.RightGrip;var tipAt=hand==1?NativeEntranceAnchor.LeftTip:NativeEntranceAnchor.RightTip;
            if(IsDrawn(gripAt)&&IsDrawn(tipAt)){var g=Anchor(gripAt,t);tip=Anchor(tipAt,t);var d=tip-g;float l=d.Length();root=l>1e-4f?g+d*(Math.Min(.18f,l*.2f)/l):g;}
            else{var axis=Vector3.Transform(Vector3.UnitZ,h.Turn);root=h.At+axis*Math.Min(.18f,len*.2f);tip=h.At+axis*len;}
            int c=drawnCount[hand],k=drawnHead[hand],o=hand*DrawnMax;
            if(c>0&&t-drawnAt[o+k]<1/240f){drawnRoot[o+k]=root;drawnTip[o+k]=tip;return;}
            k=(k+1)%DrawnMax;drawnRoot[o+k]=root;drawnTip[o+k]=tip;drawnAt[o+k]=t;drawnHead[hand]=k;drawnCount[hand]=Math.Min(DrawnMax,c+1);
        }
        /// <summary>The blade in `hand` (0 the right) as drawn at `t`: where it leaves the hilt and its tip. False: no model this frame,
        /// the keyed rig's instead (its hand and tip anchors).</summary>
        protected bool DrawnBlade(float t,int hand,out Vector3 root,out Vector3 tip,float within=1e-4f)
        {
            // (`within`: the latest frame's as near as that, for a light set before this frame's hands are handed in)
            int o=hand*DrawnMax+drawnHead[hand];
            if(drawnCount[hand]>0&&Math.Abs(drawnAt[o]-t)<within){root=drawnRoot[o];tip=drawnTip[o];return true;}
            var at=Anchor(hand==0?NativeEntranceAnchor.RightHand:NativeEntranceAnchor.LeftHand,t);tip=Anchor(hand==0?NativeEntranceAnchor.RightTip:NativeEntranceAnchor.LeftTip,t);
            root=Vector3.Lerp(at,tip,.14f);return false;
        }
        protected Vector3 DrawnTip(float t,int hand){DrawnBlade(t,hand,out _,out var tip);return tip;}
        /// <summary>The drawn wrist (0 the right) at `t`, the latest frame's within `within` s (for the camera, set before the hands come in):
        /// false where no model was drawn.</summary>
        protected bool DrawnHand(float t,int hand,float within,out Vector3 at)
        {
            var h=hand==0?handR:handL;at=h.At;return h.Model&&Math.Abs(handsAt-t)<within&&float.IsFinite(h.At.X);
        }
        /// <summary>A point fixed in the drawn hand's weapon frame (`local`, source frame, +z along the weapon) at `t`: false when no model
        /// was drawn for `t` (within `within` s), the caller's own fallback then.</summary>
        protected bool DrawnFrame(float t,int hand,Vector3 local,float within,out Vector3 at)
        {
            var h=hand==0?handR:handL;
            if(h.Model&&Math.Abs(handsAt-t)<within&&float.IsFinite(h.At.X)){at=h.At+Vector3.Transform(local,h.Turn);return true;}
            at=default;return false;
        }
        /// <summary>
        /// The sweep behind the drawn blade over the last `life` s (from `from` on): its tip's path as a chain of lines (Catmull-Rom between
        /// the frames, so a fast cut's few frames still draw an arc), hot at the head, with the blade's afterimages between root and tip
        /// filling it as a sheet. Without a model the keyed tip's path (`until`: no keyed sample past it, where the model only fades into
        /// the next clip).
        /// </summary>
        protected void BladeSweep(INativeEntranceFx fx,float t,int hand,float from,float life,Vector3 head,Vector3 tail,float width,float sheet,int seed,bool low,float until=1e6f,float reach=.33f)
        {
            int n=0;
            if(drawnCount[hand]>1&&Math.Abs(drawnAt[hand*DrawnMax+drawnHead[hand]]-t)<1e-4f)
            {
                // (the frames within `life`, at most 16 of them evenly: a fast display's many frames draw no more lines)
                int c=0;for(;c<drawnCount[hand];c++){int i=hand*DrawnMax+((drawnHead[hand]-c)%DrawnMax+DrawnMax)%DrawnMax;if(t-drawnAt[i]>life||drawnAt[i]<from)break;}
                int stride=Math.Max(1,(c+14)/15);
                for(int j=0;j<c&&n<sweepTip.Length;j+=stride)
                {
                    int i=hand*DrawnMax+((drawnHead[hand]-j)%DrawnMax+DrawnMax)%DrawnMax;sweepTip[n]=drawnTip[i];sweepRoot[n]=drawnRoot[i];sweepAge[n]=t-drawnAt[i];n++;
                }
            }
            else
            {
                int m=low?8:14;var hold=hand==0?NativeEntranceAnchor.RightHand:NativeEntranceAnchor.LeftHand;var end=hand==0?NativeEntranceAnchor.RightTip:NativeEntranceAnchor.LeftTip;
                for(int i=0;i<m&&n<sweepTip.Length;i++){float s=t-life*i/(m-1);if(s<from)break;s=Math.Min(s,until-1e-3f);sweepTip[n]=Anchor(end,s);sweepRoot[n]=Vector3.Lerp(Anchor(hold,s),sweepTip[n],.14f);sweepAge[n]=t-s;n++;}
            }
            if(n<2)return;int steps=low?1:3;
            for(int j=0;j<n-1;j++)
            {
                var p0=sweepTip[Math.Max(0,j-1)];var p1=sweepTip[j];var p2=sweepTip[j+1];var p3=sweepTip[Math.Min(n-1,j+2)];
                for(int k=0;k<steps;k++)
                {
                    float u0=(float)k/steps,u1=(float)(k+1)/steps;var a=CatmullRom(p0,p1,p2,p3,u0);var b=CatmullRom(p0,p1,p2,p3,u1);if((a-b).LengthSquared()<1e-8f)continue;
                    float age=NativeEntranceMath.Lerp(sweepAge[j],sweepAge[j+1],u0),f=1-NativeEntranceMath.Clamp01(age/life),k2=f*f;
                    Line(fx,a,a-b,Vector3.Lerp(tail,head,f)*k2,width*(.35f+.65f*f),.2f,1-f,seed+j*8+k);
                    // The sheet: the blade's afterimage from `reach` of the way up to the tip, faint and soft.
                    if(sheet>0&&!low){var r=Vector3.Lerp(sweepRoot[j],sweepRoot[j+1],u0);var from3=Vector3.Lerp(r,a,reach);var ax=a-from3;if(ax.LengthSquared()>1e-6f)Line(fx,a,ax,tail*(sheet*k2),width*2.6f,.1f,1-f,seed+j*8+4+k);}
                }
            }
        }
        private readonly Vector3[] sweepTip=new Vector3[16],sweepRoot=new Vector3[16];private readonly float[] sweepAge=new float[16];
        private static Vector3 CatmullRom(Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3,float u){float u2=u*u,u3=u2*u;return .5f*(2*p1+(p2-p0)*u+(2*p0-5*p1+4*p2-p3)*u2+(3*p1-p0-3*p2+p3)*u3);}

        // ---- the keyed weapon (no match clips: the model follows the keyed rig, the arms solved onto the intro's wield paths) ----
        /// <summary>No motion-bank clip (owner 2026-11-20: "we dont need the matches own annimation system"): the model follows the keyed rig.</summary>
        public override bool Clip(float t,out string clip,out float time){clip=null;time=0;return false;}
        /// <summary>
        /// Solves arm `left` onto `path` at `t`: the wrist at its grip (the pilot's frame from the keyed root: its floor spot, its yaw and its
        /// lift off the floor) and the weapon's axis (a blade's line, the shield's face) along its key (HoldAt). `offset` slides the hand along
        /// the blade.
        /// </summary>
        protected void Wield(NativeIntroWield path,float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation,bool left,float offset=0)
        {
            if(WieldTarget(path,t,pose,root,rotation,out var at,out var axis,out float weight,offset))HoldAt(pose,root,rotation,left,at,axis,weight);
        }
        /// <summary>Where `path` puts the wrist at `t` and which way the weapon's axis points (world, source frame), without solving the arm:
        /// to blend two holds by their targets, so one solve makes the arm between them (a blend of two solved arms can swing the elbow round).</summary>
        protected bool WieldTarget(NativeIntroWield path,float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation,out Vector3 at,out Vector3 axis,out float weight,float offset=0)
        {
            path.At(t,out var grip,out var blade,out weight);at=axis=default;if(weight<=.001f)return false;
            if(path.Chest)
            {
                pose.World();var turn=pose.WorldRotation[CapturedRig.Chest];var c=pose.WorldPosition[CapturedRig.Chest];axis=Vector3.Transform(Vector3.Transform(blade,turn),rotation);
                at=root+Vector3.Transform(c+Vector3.Transform(grip,turn),rotation)+axis*offset;return true;
            }
            var f=Vector3.Transform(Vector3.UnitZ,rotation);float yaw=(float)Math.Atan2(f.X,f.Z);
            var origin=new Vector3(root.X,root.Y-.88f+NativeEntranceStage.FloorY,root.Z);
            axis=NativeEntranceMath.RotateY(blade,yaw);at=origin+NativeEntranceMath.RotateY(grip,yaw)+axis*offset;return true;
        }
        /// <summary>The weapon's convention in the hand that holds it (Core NativeIntroGrip.Natural): a blade runs through the fist along the
        /// hand's +z (none), the shield's face lies along the hand's length (a quarter turn about x).</summary>
        protected virtual Quaternion HandNatural=>NativeIntroGrip.Natural(NativeIntroGrip.Of(Spec),false);
        private bool FaceAlongHand=>NativeIntroGrip.Of(Spec)==NativeIntroGrip.Kind.Shield;
        /// <summary>
        /// The hold (intros v2 pose craft, docs/INTROS-V2.md 9a-9b): arm `left`'s wrist at `at` (world, source frame) with the weapon's axis
        /// along `axis` (a blade's line; the shield's face), blended in by `weight`, solved as a body holds it rather than by turning the
        /// wrist. The elbow goes to the side away from the axis (the arm's bend then carries a blade's line: a raised guard's elbow drops
        /// under it, a low point's goes back), never in toward the body and up only when the hand is over the shoulder; the hand points along
        /// the forearm (a straight wrist), its roll the weapon's; the roll the elbow could not give is carried by the forearm (Core
        /// NativeIntroGrip.Forearm), never the wrist or the upper arm. Two passes: the elbow, then the hand on its forearm. Allocation-free.
        /// </summary>
        protected void HoldAt(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,bool left,Vector3 at,Vector3 axis,float weight)
        {
            if(weight<=.001f||axis.LengthSquared()<1e-10f)return;pose.World();
            int arm=left?CapturedRig.LArm:CapturedRig.RArm;var a0=pose.Rotation[arm];var e0=pose.Rotation[arm+1];var h0=pose.Rotation[arm+2];
            // (the chest's frame: +x its left, +y up it, +z ahead of it, as CapturedRig.Arm takes its target, turn and pole)
            var inv=Quaternion.Inverse(rotation);var chestInv=Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest]);var chestAt=pose.WorldPosition[CapturedRig.Chest];
            var w=Vector3.Transform(Vector3.Transform(at-root,inv)-chestAt,chestInv);var z=Vector3.Normalize(Vector3.Transform(Vector3.Transform(axis,inv),chestInv));
            var s=Vector3.Transform(pose.WorldPosition[arm]-chestAt,chestInv);var line=w-s;float reach=line.Length();line=reach>1e-5f?line/reach:-Vector3.UnitY;
            // The elbow goes round the shoulder-wrist line to where the body holds this best (a soft minimum over the circle, so it moves
            // smoothly): the roll the forearm must carry, the upper arm's twist and the wrist's bend each kept in their ranges, the elbow out
            // of the torso, and near the arm's own rest (down and out) where nothing else decides it.
            var u=Vector3.Cross(line,Vector3.UnitY);if(u.LengthSquared()<1e-4f)u=Vector3.Cross(line,Vector3.UnitZ);u=Vector3.Normalize(u);var v=Vector3.Cross(u,line);
            var rest=left?new Vector3(.78f,-.6f,-.18f):new Vector3(-.78f,-.6f,-.18f);float best=float.MaxValue;
            for(int k=0;k<PoleSamples;k++)
            {
                float ang=k*(float)(2*Math.PI/PoleSamples);var pole=u*(float)Math.Cos(ang)+v*(float)Math.Sin(ang);poleTry[k]=pole;
                Solve(pose,left,w,z,pole,chestAt,chestInv);float cost=HoldCost(pose,left,pole,rest);poleCost[k]=cost;if(cost<best)best=cost;
            }
            var chosen=Vector3.Zero;for(int k=0;k<PoleSamples;k++)chosen+=poleTry[k]*(float)Math.Exp(-(poleCost[k]-best)/PoleSoft);
            if(chosen.LengthSquared()<1e-8f)chosen=rest;
            Solve(pose,left,w,z,chosen,chestAt,chestInv);
            if(weight<.999f){pose.Rotation[arm]=Quaternion.Slerp(a0,pose.Rotation[arm],weight);pose.Rotation[arm+1]=Quaternion.Slerp(e0,pose.Rotation[arm+1],weight);pose.Rotation[arm+2]=Quaternion.Slerp(h0,pose.Rotation[arm+2],weight);}
            pose.World();
        }
        /// <summary>How much the hilt is held in both hands this frame (0..1; set in Arms before the hold, from the grip): the main hand then
        /// points between its own forearm and the other arm's line to the hilt.</summary>
        protected float TwoHands;
        /// <summary>
        /// The pose, then (a two-hander with its grip on) the keyed off arm held as a body holds a hilt: the framework's grip IK (Core
        /// NativeIntroGrip.Solve) puts the off fist at the lower grip turned exactly as the main hand, but a hand closes round a hilt at any
        /// roll; so the off arm is solved again at the same point on the same line (HoldAt: its elbow and its roll free), the wrist then as
        /// straight as the main hand's. The drawn model's off fist is the runtime's grasp on the drawn hilt either way.
        /// </summary>
        public override void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            base.Pose(t,pose,out root,out rotation);
            if(!(this is INativeIntroGrip g)||NativeIntroGrip.Of(Spec)!=NativeIntroGrip.Kind.TwoHand||!g.GripAt(t,out var at)||!(at.Weight>.001f))return;
            pose.World();var rm=pose.WorldRotation[CapturedRig.RHand];var support=pose.WorldPosition[CapturedRig.RHand]+Vector3.Transform(new Vector3(0,0,-.11f+at.Along),rm);
            float both=TwoHands;TwoHands=0;
            HoldAt(pose,root,rotation,true,root+Vector3.Transform(support,rotation),Vector3.Transform(Vector3.Transform(Vector3.UnitZ,rm),rotation),Math.Min(1,at.Weight));
            TwoHands=both;
        }
        private const int PoleSamples=24;private const float PoleSoft=.35f;
        private readonly Vector3[] poleTry=new Vector3[PoleSamples];private readonly float[] poleCost=new float[PoleSamples];
        /// <summary>One solve of the hold for elbow pole `pole` (chest frame): the elbow, then the hand straight along its forearm with the
        /// weapon's axis `z` and its roll, then the roll the elbow left carried by the forearm.</summary>
        private void Solve(CapturedRig.Pose pose,bool left,Vector3 w,Vector3 z,Vector3 pole,Vector3 chestAt,Quaternion chestInv)
        {
            int arm=left?CapturedRig.LArm:CapturedRig.RArm;
            CapturedRig.Arm(pose,left,w,Quaternion.Identity,pole);pose.World();
            var elbow=Vector3.Transform(pose.WorldPosition[arm+1]-chestAt,chestInv);var fore=w-elbow;fore=fore.LengthSquared()>1e-8f?Vector3.Normalize(fore):-Vector3.UnitY;
            Quaternion hand;
            if(FaceAlongHand)
            {
                // (the shield: its face along the hand's length, the fist's axis (its bar) on the forearm's own bend side)
                var y=-z;var fz=Vector3.Transform(Vector3.UnitZ,Quaternion.Normalize(chestInv*pose.WorldRotation[arm+1]));var thumb=fz-y*Vector3.Dot(fz,y);
                if(thumb.LengthSquared()<1e-6f){thumb=Vector3.UnitY-y*y.Y;if(thumb.LengthSquared()<1e-6f)thumb=Vector3.UnitZ;}
                thumb=Vector3.Normalize(thumb);hand=Quaternion.Normalize(CapturedRig.Basis(Vector3.Cross(y,thumb),y,thumb)*HandNatural);
            }
            else
            {
                // (both hands on one hilt: the hand points between this forearm and the other arm's line to the hilt, so both wrists share the
                // turn the hands make round it instead of one wrist taking all of it)
                if(TwoHands>.001f)
                {
                    // (the other forearm, estimated: its elbow half way from its shoulder to the hilt, out to its side and down a little)
                    int other=left?CapturedRig.RArm:CapturedRig.LArm;var so=Vector3.Transform(pose.WorldPosition[other]-chestAt,chestInv);
                    var eo=(so+w)*.5f+new Vector3(left?-.12f:.12f,-.08f,0);var o=w-eo;if(o.LengthSquared()>1e-6f)fore=Vector3.Normalize(fore+Vector3.Normalize(o)*TwoHands);
                }
                var y=-(fore-z*Vector3.Dot(fore,z));if(y.LengthSquared()<1e-6f){y=Vector3.UnitY-z*z.Y;if(y.LengthSquared()<1e-6f)y=Vector3.UnitZ;}
                y=Vector3.Normalize(y);hand=Quaternion.Normalize(CapturedRig.Basis(Vector3.Cross(y,z),y,z)*HandNatural);
            }
            CapturedRig.Arm(pose,left,w,hand,pole);NativeIntroGrip.Forearm(pose,left,HandNatural);pose.World();
        }
        /// <summary>What a solved hold costs the body (the pose-craft limits as soft walls, in their own degrees): the forearm's whole roll past
        /// 70, the upper arm's twist past 80, the wrist's bend past 55, the elbow inside the torso, and the elbow's way off the rest pole.</summary>
        private float HoldCost(CapturedRig.Pose pose,bool left,Vector3 pole,Vector3 rest)
        {
            int arm=left?CapturedRig.LArm:CapturedRig.RArm;const float Deg=57.29578f;
            float et=Twist(pose.Rotation[arm+1],out _),wt=Twist(Quaternion.Normalize(pose.Rotation[arm+2]*Quaternion.Inverse(HandNatural)),out var hand);
            float bend=(float)Math.Acos(Math.Clamp(-hand.Y,-1,1))*Deg;
            float st=Twist(Quaternion.Normalize(Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest])*pose.WorldRotation[arm]),out var upper);
            float roll=Math.Abs(et+wt),c=Wall(roll,70)+4*Wall(roll,88)+Wall(Math.Abs(wt),40)+Wall(Math.Abs(st),80)+4*Wall(Math.Abs(st),100)+Wall(bend,55)+4*Wall(bend,74);
            // (the upper arm behind the body or across it: the pose-craft's shoulder ranges)
            float back=(float)Math.Atan2(-upper.Z,Math.Sqrt(upper.X*upper.X+upper.Y*upper.Y))*Deg,across=(float)Math.Asin(Math.Clamp(-upper.X*(left?1:-1),-1,1))*Deg;
            c+=Wall(back,60)+4*Wall(back,66)+(upper.Z<.35f?Wall(across,45)+4*Wall(across,51):0);
            // (the elbow inside the torso's boxes, by how deep)
            var e=pose.WorldPosition[arm+1];
            foreach(var box in TorsoBoxes)
            {
                var cBox=pose.WorldPosition[box.Bone]+Vector3.Transform(box.Position,pose.WorldRotation[box.Bone]);var l=Vector3.Transform(e-cBox,Quaternion.Inverse(pose.WorldRotation[box.Bone]));
                float dx=box.Size.X*.5f-Math.Abs(l.X),dy=box.Size.Y*.5f-Math.Abs(l.Y),dz=box.Size.Z*.5f-Math.Abs(l.Z);if(dx>-.02f&&dy>-.02f&&dz>-.02f)c+=100*(Math.Min(dx,Math.Min(dy,dz))+.02f)*10;
            }
            float off=(float)Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(pole),Vector3.Normalize(rest)),-1,1))*Deg;return c+off*off/3600*.6f;
        }
        private static float Wall(float value,float from){float x=(value-from)/10;return x>0?x*x:0;}
        /// <summary>A joint's swing direction (its limb's -y in the parent's frame) and its twist about its own length (degrees).</summary>
        private static float Twist(Quaternion local,out Vector3 down)
        {
            down=Vector3.Transform(-Vector3.UnitY,local);var swing=CapturedRig.FromTo(-Vector3.UnitY,down);var q=Quaternion.Normalize(Quaternion.Inverse(swing)*local);
            float a=2*(float)Math.Atan2(-q.Y,q.W)*57.29578f;while(a>180)a-=360;while(a<-180)a+=360;return a;
        }
        private static readonly CapturedRig.Box[] TorsoBoxes=Torso();
        private static CapturedRig.Box[] Torso()
        {
            int n=0;foreach(var b in CapturedRig.Boxes)if((b.Bone==CapturedRig.Chest||b.Bone==CapturedRig.Spine||b.Bone==CapturedRig.Pelvis)&&b.Size.X>.3f)n++;
            var r=new CapturedRig.Box[n];n=0;foreach(var b in CapturedRig.Boxes)if((b.Bone==CapturedRig.Chest||b.Bone==CapturedRig.Spine||b.Bone==CapturedRig.Pelvis)&&b.Size.X>.3f)r[n++]=b;return r;
        }
        /// <summary>
        /// HoldAt for a contact with the world on the drawn model (a blade driven into the floor, a hand on a hilt that stands in it): `at` is
        /// where the DRAWN grip goes. The model copies the keyed arm's turns from its own shoulder, which stands .21 m further in and .32 m
        /// higher than the keyed rig's on hips that move 1.7 times as far (ModelFeet), its arm .94 as long; so the keyed wrist is put where
        /// that arm, hung from the keyed shoulder, reaches the same way. Measured on the model by the stick dumps (the drawn grip within a few
        /// centimetres where the arm can reach).
        /// </summary>
        protected void HoldDrawn(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,bool left,Vector3 at,Vector3 axis,float weight)
        {
            if(weight<=.001f)return;HoldAt(pose,root,rotation,left,DrawnTarget(pose,root,rotation,left,at),axis,weight);
        }
        /// <summary>The keyed wrist (world) that puts the drawn grip of arm `left` at `at` (HoldDrawn's mapping, without the solve).</summary>
        protected Vector3 DrawnTarget(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,bool left,Vector3 at)
        {
            pose.World();var inv=Quaternion.Inverse(rotation);
            var pelvis=NativeIntroFeet.ModelPelvis+(pose.WorldPosition[CapturedRig.Pelvis]-GamePelvis)*NativeIntroFeet.LegScale;
            var shoulder=pelvis+Vector3.Transform(new Vector3(left?ModelShoulder.X:-ModelShoulder.X,ModelShoulder.Y,ModelShoulder.Z),pose.WorldRotation[CapturedRig.Chest]);
            var keyed=pose.WorldPosition[left?CapturedRig.LArm:CapturedRig.RArm]+(Vector3.Transform(at-root,inv)-shoulder)/ModelArm;
            return root+Vector3.Transform(keyed,rotation);
        }
        /// <summary>The measured model's left shoulder from its pelvis at rest (rig frame) and its arm's length over the keyed rig's.</summary>
        private static readonly Vector3 ModelShoulder=new Vector3(.133f,.438f,-.01f);private const float ModelArm=.94f;
        private static readonly Vector3 GamePelvis=CapturedRig.Bones[CapturedRig.Body].Position+CapturedRig.Bones[CapturedRig.Pelvis].Position;
        /// <summary>Solves arm `left` so its wrist is at `at` (world, source frame) with the weapon's axis along `blade` (HoldAt).</summary>
        protected void WieldAt(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,bool left,Vector3 at,Vector3 blade,float weight)=>HoldAt(pose,root,rotation,left,at,blade,weight);

        // ---- this frame's camera (the rig at `t`, as Camera draws it), for streaks laid onto their head's camera plane ----
        protected NativeEntranceView View;protected bool ViewSet;
        /// <summary>How much of a soft puff of `size` m at `at` to draw this frame (Look first): none while the lens is inside it, all of it
        /// once it is clear (smoke that drifts over the camera otherwise fills the frame).</summary>
        protected float NearFade(Vector3 at,float size)=>ViewSet?NativeEntranceMath.Smooth((Vector3.Distance(at,View.Eye)-.55f*size)/Math.Max(.1f,size)):1;
        // The drawn eyes' offset from the keyed ones as last drawn (the camera reads the keyed rig, the runtime clears the drawn body before
        // it: Frame takes this to put a shot on the drawn head).
        private Vector3 eyesOffset;private float eyesSeenAt=-100;
        protected bool EyesOffset(float t,float within,out Vector3 offset){offset=eyesOffset;return Math.Abs(eyesSeenAt-t)<within;}
        protected void Look(float t){if(IsDrawn(NativeEntranceAnchor.Eyes)){eyesOffset=Anchor(NativeEntranceAnchor.Eyes,t)-KeyedAnchor(NativeEntranceAnchor.Eyes,t);eyesSeenAt=t;}
            float settle=Clock(SettleAt),at=t<settle?t:settle;Rig.Evaluate(this,at,Context.Reduced,out View);Frame(at,ref View);ViewSet=true;}
        protected void Line(INativeEntranceFx fx,Vector3 head,Vector3 axis,Vector3 color,float width,float taper,float u,float seed)=>NativeIntroMelee.Line(fx,View,ViewSet,head,axis,color,width,taper,u,seed);
    }
}
