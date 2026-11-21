using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Brass Storm", the machine gun's v2 intro (I4, owner 2026-11-20: replaces Belt Rattle; docs/INTROS-RANGED.md). 7.8 s, the name
    /// at 6.0 s. A freight depot's yard at night: the belt clanks home in the dark; three sodium floodlights bang on one after another,
    /// showing the wet yard, the corrugated warehouse with its roll-up shutter, oil drums and crates, and the pilot behind a sandbag
    /// emplacement with the gun low. The pilot steps up, racks the handle and lets the belt run: a long burst traversing the shutter,
    /// tracers, sparks, a drum knocked over, a crate burst apart, brass pouring out in a slowed waterfall and heaping round the boots.
    /// The belt runs dry; a click; the barrel glows. In the drifting smoke the work light inside the warehouse shines out through the
    /// shutter's bullet holes in hundreds of rays, and the camera pushes back through them to the pilot, gun lowered, for the name.
    /// Presentation only: pure functions of the presentation clock, thinned by quality; in Reduced effects the muzzle burns steady
    /// instead of strobing.
    /// </summary>
    public sealed class NativeIntroBrassStorm:NativeRangedIntro,INativeIntroGrip
    {
        public const string Id=NativeIntroCatalog.WeaponPrefix+"machinegun";
        public const float Total=7.8f,CardAt=6.0f;
        public const float Flood1=.45f,Flood2=.8f,Flood3=1.15f,StepUp=1.45f,Rack=1.72f,FireFrom=1.95f,FireTo=4.35f,Dry=4.38f,Lower=5.1f,LastCase=5.5f;
        public const int Rounds=38;
        private const float Rate=(FireTo-FireFrom)/Rounds,TracerSpeed=110,ShutterZ=12,CaseScale=1.5f;
        /// <summary>The work light inside the warehouse (source frame): each hole's ray runs out from it.</summary>
        private static readonly Vector3 Lamp=L(.3f,1.25f,ShutterZ+8);
        private static readonly Vector3 Sodium=NativeEntranceFx.Hex(0xffa040),Hot=NativeEntranceFx.Hex(0xffd890),Brass=NativeEntranceFx.Hex(0xffc76a),Smoke=new Vector3(.32f,.3f,.29f),Work=NativeEntranceFx.Hex(0xffe8c8);
        // the floods (Unity x, y, z, yaw; ranged_brass.py FLOODS) and the drums and crates
        private static readonly float[] Floods={-8.6f,7.6f,2.6f,-.55f, 8.8f,7.6f,4.2f,.6f, 0,7.0f,11.5f,(float)Math.PI};
        private static readonly float[] FloodOn={Flood1,Flood2,Flood3};
        private static readonly float[] Drums={2.6f,8.4f, 3.35f,8.9f, 2.95f,9.6f, 3.9f,9.9f, 2.2f,9.9f};
        private static readonly float[] Crates={-4.6f,8.6f,1.0f,.1f, -5.7f,9.1f,1.0f,-.15f, -4.9f,9.4f,.9f,.05f};
        /// <summary>The drum knocked over and the crate burst (their indices) and when.</summary>
        private const int Knocked=0,Burst=0;
        private static readonly float KnockAt=FireFrom+DrumRound*Rate+.075f,BurstAt=FireFrom+CrateRound*Rate+.075f;
        /// <summary>The drawn gun's grip while firing (the weapon frame's origin on the drawn model, shouldered; Tests/IntroRangedChecks holds
        /// the bore on each round's strike from there).</summary>
        private static readonly Vector3 Grip=L(-.17f,1.22f,.24f);
        private readonly Vector3[] at=new Vector3[96];private readonly Quaternion[] turn=new Quaternion[96];

        public NativeIntroBrassStorm():base(Id,Total,CardAt,2.6f,0xffa040)
        {
            // ---- the keyed body (ModelFeet: the stance is the drawn model's ankles, the drops the drawn hips', docs/INTROS-V2.md 9c) ----
            // In the dark behind the bags: the gun at the low carry (the owner's rest-low rule), the weight back, the head turning to each
            // flood as it bangs on (left, right, then up the yard to the shutter). The step in: the left foot to the bags, the gun up into the
            // shoulder as the off hand racks the handle back and home. The burst: the match's long-gun stance (NativeIntroPoses.RifleAim's
            // blade, the cheek down on the stock), the knees soft and the weight driven into the gun, the hips swinging with the traverse (the
            // root's yaw takes half of it, the arms the rest), each round's kick in the muzzle. The belt runs dry: a beat on the stock, then
            // the head up out of the smoke and the gun down to the carry, squared to the facing for the name.
            Start=Stance(.13f,-.1f,.12f,-.15f,-.34f,-.35f);
            Steps=new[]{Step(true,StepUp,StepUp+.3f,.14f,.18f,.3f,.07f),Step(false,StepUp+.12f,StepUp+.42f,-.14f,-.16f,-.6f,.05f),
                Step(false,Lower+.05f,Lower+.38f,-.15f,-.1f,-.35f,.04f),Step(true,Lower+.22f,Lower+.5f,.14f,.12f,.15f,.035f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            float[] carry(float head,float turn,float drop)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.Yaw]=Facing;v[NativeEntranceKeys.BodyY]=drop;
                NativeEntranceKeys.Set(v,B,.03f,0,0);NativeEntranceKeys.Set(v,C,-.02f,.06f+turn*.3f,0);NativeEntranceKeys.Set(v,Hd,head,turn,0);});
            k.Key(0,carry(.06f,0,-.03f));
            k.Key(Flood1-.05f,carry(.06f,0,-.03f));
            // each flood bangs on: a start, the head snapping toward it (left, right, then up the yard)
            k.Key(Flood1+.12f,carry(-.04f,.38f,-.05f)).ImpactKey();
            k.Key(Flood2+.12f,carry(-.06f,-.34f,-.05f)).ImpactKey();
            k.Key(Flood3+.14f,carry(-.1f,0,-.045f)).ImpactKey();
            k.Key(StepUp,carry(-.02f,0,-.05f));
            // the step in, the gun coming up (the hips forward, the chest blading)
            k.Key(StepUp+.26f,NativeIntroPoses.RifleAim()).Root(Fwd(.08f).X,.88f,Fwd(.08f).Z,Facing).Drop(-.07f).J(B,.1f).J(Sp,0,-.2f).J(C,0,-.25f).J(CapturedRig.Neck,.3f,0,.12f).J(Hd,-.15f,.45f,.15f).PassKey();
            // the burst: the hips swing with the traverse, the weight in the gun
            k.Key(FireFrom-.12f,Aim(.12f,-.075f)).Root(Fwd(.12f).X,.88f,Fwd(.12f).Z,Facing+HipShare*Bearing(0));
            foreach(int r in TurnRounds){float lean=.13f+.035f*(r%2);k.Key(Fired(r),Aim(lean,-.08f-.012f*(r%2))).Root(Fwd(.12f).X,.88f,Fwd(.12f).Z,Facing+HipShare*Bearing(r));}
            // dry: a beat on the stock, then the head up out of the smoke and the gun down
            k.Key(Dry+.3f,Aim(.11f,-.075f)).Root(Fwd(.12f).X,.88f,Fwd(.12f).Z,Facing+HipShare*Bearing(Rounds-1));
            k.Key(Lower+.2f,stand).Root(Fwd(.06f).X,.88f,Fwd(.06f).Z,Facing).Drop(-.05f).J(B,.05f).J(C,-.02f,-.1f).J(Hd,-.08f,.1f).PassKey();
            k.Key(Lower+.6f,NativeIntroPoses.Hero()).Root(0,.88f,0,Facing).Drop(-.03f).J(C,-.05f,-.08f).J(Hd,-.02f,-.04f);
            k.Key(Total,NativeIntroPoses.Hero()).Root(0,.88f,0,Facing).Drop(-.03f).J(C,-.06f,-.1f).J(Hd,0,-.06f);Keys=k;

            // ---- the camera ----
            Rig.Spline(0,new[]{L(-9.6f,6.6f,-4.6f),L(-8.2f,4.8f,-4.0f),L(-6.8f,3.4f,-3.2f)},L(0,1.3f,6.5f),L(0,1.2f,5),50,46).Ease(NativeIntroEase.InOut).Focus(0,0,.2f)
               .Dolly(StepUp-.03f,L(1.32f,.36f,2.65f),L(1.06f,.4f,2.3f),L(0,1.24f,0),L(0,1.3f,0),38,35).Focus(0,0,.45f)
               .Dolly(FireFrom-.02f,L(-.76f,1.56f,-1.46f),L(-.68f,1.5f,-1.3f),L(-.4f,1.9f,12),L(-2.4f,1.5f,12),42,40).Handheld(.9f).Focus(0,0,.15f)
               // over the left shoulder down the gun to the drum as the rounds walk onto it and knock it over
               .Dolly(2.42f,L(1.4f,1.72f,-1.5f),L(1.3f,1.68f,-1.36f),L(-2.4f,.6f,8.2f),L(-2.5f,.5f,8.3f),34,32).Handheld(.5f).Focus(0,0,.2f)
               // the brass pouring out (slowed), then the heap round the boots at the floor
               .Dolly(2.9f,L(-2.3f,1.32f,1.05f),L(-2.1f,1.24f,.4f),L(-.25f,1.22f,.5f),L(-.3f,1.08f,.3f),36,34).Focus(0,0,.55f)
               .Dolly(3.4f,L(-2.25f,.1f,.95f),L(-2.1f,.12f,.78f),L(-.5f,.62f,.3f),L(-.5f,.66f,.28f),52,50).Focus(0,0,.4f)
               // the crate bursting in the foreground, the gunner walking the belt onto it
               .Dolly(3.78f,L(6.2f,1.3f,10.5f),L(6.0f,1.32f,10.1f),L(1.6f,1.0f,4.0f),L(1.5f,1.05f,3.9f),33,31).Handheld(.6f).Focus(0,0,.15f)
               .Dolly(Dry+.04f,L(.25f,1.32f,9.7f),L(.45f,1.26f,4.5f),L(0,1.25f,0),L(0,1.22f,0),40,36).Ease(NativeIntroEase.Out).Focus(0,0,.35f)
               .Dolly(Lower+.25f,L(1.58f,.62f,3.28f),L(1.26f,.66f,2.86f),L(-.23f,1.08f,.1f),L(-.23f,1.13f,.1f),35,31).Ease(NativeIntroEase.Out).In(NativeIntroCut.Blend,.5f).Focus(0,0,.45f)
               .Bars(0,1).Bars(CardAt-.35f,.55f).Black(0,1).Black(Flood1-.02f,.55f).Black(Flood2,0);
            // The brass waterfall slowed; repaid as the burst runs on.
            Ramps.Ramp(2.95f,.3f,1.0f,1.3f);
        }

        // ---- the gun: where it points over the burst (one traverse, as a gunner walks a belt across a target), and the hands on it ----
        /// <summary>The rounds that strike the drum and the crate (three each, as the sweep crosses them).</summary>
        public const int DrumRound=10,CrateRound=30;
        private static readonly Vector3 DrumHit=S(Drums[Knocked*2],.55f,Drums[Knocked*2+1]-.25f),CrateHit=S(Crates[Burst*4],.55f,Crates[Burst*4+1]-.5f);
        /// <summary>A bearing from the gun (rad off the facing, + toward the pilot's left) and its point in the facing's frame.</summary>
        private static float BearingOf(Vector3 p){var d=Vector3.Transform(p-Grip,NativeEntranceMath.Yaw(-Facing));return (float)Math.Atan2(d.X,d.Z);}
        private static readonly float DrumBearing=BearingOf(DrumHit),CrateBearing=BearingOf(CrateHit);
        /// <summary>Round `i`'s bearing: from the shutter's middle round to the drum on the right, one long walk back across the shutter and
        /// the wall to the crate on the left, then back onto the shutter as the belt runs out (eased at each turn).</summary>
        public static float Bearing(int i)
        {
            if(i<DrumRound-1)return NativeEntranceMath.Lerp(-.06f,DrumBearing,NativeEntranceMath.Smooth(i/(float)(DrumRound-1)));
            if(i<=DrumRound+1)return DrumBearing;
            if(i<CrateRound-1)return NativeEntranceMath.Lerp(DrumBearing,CrateBearing,NativeEntranceMath.Smooth((i-DrumRound-1)/(float)(CrateRound-DrumRound-2)));
            if(i<=CrateRound+1)return CrateBearing;
            return NativeEntranceMath.Lerp(CrateBearing,.2f,NativeEntranceMath.Smooth((i-CrateRound-1)/(float)(Rounds-CrateRound-2)));
        }
        /// <summary>Where round `i` strikes (source frame): the drum, the crate, or the shutter (the wall beside it past its edges) at the
        /// round's bearing, the height wandering with the gun's climb.</summary>
        public static Vector3 Strike(int i)
        {
            if(Math.Abs(i-DrumRound)<=1)return DrumHit;
            if(Math.Abs(i-CrateRound)<=1)return CrateHit;
            var g=Vector3.Transform(Grip,NativeEntranceMath.Yaw(-Facing));float b=Bearing(i),z=ShutterZ-.04f,x=g.X+(float)Math.Tan(b)*(z-g.Z);
            if(Math.Abs(x)>3.6f){z=ShutterZ+.16f;x=g.X+(float)Math.Tan(b)*(z-g.Z);}
            float y=1.05f+.45f*(float)Math.Sin(i*.5f+1)+.22f*NativeEntranceMath.Signed(5101,i);
            return L(x,Math.Max(.3f,y),z);
        }
        /// <summary>Whether round `i` holes the steel (the shutter or the wall), not the drum or the crate.</summary>
        public static bool Holed(int i)=>Math.Abs(i-DrumRound)>1&&Math.Abs(i-CrateRound)>1;
        public static float Fired(int i)=>FireFrom+i*Rate;
        /// <summary>The aim's body at the burst: RifleAim's blade with the cheek down on the stock (the neck bowed, the head tilted back level and
        /// rolled onto it: the fit in Tests/IntroRangedChecks), leaning `lean` into the gun, the hips `drop` lower (the model's metres).</summary>
        private static float[] Aim(float lean,float drop)=>NativeEntranceKeys.With(NativeIntroPoses.RifleAim(),v=>{v[NativeEntranceKeys.BodyY]=drop;
            NativeEntranceKeys.Set(v,B,lean,0,0);NativeEntranceKeys.Set(v,CapturedRig.Neck,.6f,0,.25f);NativeEntranceKeys.Set(v,Hd,-.3f,.75f,.3f);});
        /// <summary>The world yaw and pitch from the gun to where it points at `t` (the traverse across the shutter; the first strike before it).</summary>
        public static void TargetAngles(float t,Vector3 from,out float yaw,out float pitch)
        {
            float f=(t-FireFrom)/Rate;int i=Math.Max(0,Math.Min(Rounds-1,(int)Math.Floor(f)));float u=t<FireFrom?0:NativeEntranceMath.Clamp01(f-i);
            var d=Vector3.Lerp(Strike(i),Strike(Math.Min(Rounds-1,i+1)),u)-from;yaw=(float)Math.Atan2(d.X,d.Z);pitch=(float)Math.Atan2(d.Y,Math.Sqrt(d.X*d.X+d.Z*d.Z));
        }
        /// <summary>The drawn gun's grip in the root's frame while it is shouldered (Tests/IntroRangedChecks holds the bore on every strike
        /// from there): the aim's line starts at the drawn gun as the hips swing it.</summary>
        private static readonly Vector3 GripOnRoot=new Vector3(-.174f,1.3f-.88f,.398f);
        /// <summary>How much of the sweep the hips take (the arms the rest), so the head stays on the gun.</summary>
        private const float HipShare=.75f;
        /// <summary>The rounds the hips are keyed on through the burst (the sweep's turns among them).</summary>
        private static readonly int[] TurnRounds={0,5,DrumRound,15,20,25,CrateRound,34,Rounds-1};
        private static float Wrap(float a){while(a>Math.PI)a-=2*(float)Math.PI;while(a<-Math.PI)a+=2*(float)Math.PI;return a;}
        private static Vector3 Fwd(float z)=>Local(0,0,z);
        /// <summary>How far the gun is up in the shoulder at `t` (0: the low carry).</summary>
        private static float Up(float t)=>NativeEntranceMath.Ramp(t,StepUp+.04f,FireFrom-.14f)*(1-NativeEntranceMath.Ramp(t,Dry+.32f,Lower+.32f));

        // ---- the body on the model (docs/INTROS-V2.md 9a-9c) ----
        protected override bool ModelFeet=>true;
        /// <summary>The off hand on the handguard throughout: racked back along the gun to the handle and home as it comes up, the stock in the
        /// drawn shoulder while it is up.</summary>
        public bool GripAt(float t,out NativeIntroGripAt g){g=new NativeIntroGripAt(1,default,-.3f*NativeEntranceMath.Bump(t,Rack-.14f,Rack+.2f),0,Up(t));return true;}
        /// <summary>Only the main hand is keyed here (the off hand is the grip's): the low carry (the muzzle down and across the body), then the
        /// match's long-gun aim along the traverse (the yaw the hips leave), each round's kick lifting the muzzle (a steady lean in Reduced).</summary>
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            float up=Up(t);
            if(up<.999f)NativeIntroGrip.Hold(pose,false,"machinegun",CarryAt,NativeIntroGrip.Aim(pose,.45f,-.75f),new Vector3(-1,-.7f,-.2f));
            if(up>.001f)
            {
                var f=Vector3.Transform(Vector3.UnitZ,rotation);float facing=(float)Math.Atan2(f.X,f.Z);TargetAngles(t,root+Vector3.Transform(GripOnRoot,rotation),out float yaw,out float pitch);
                float kick=t>=FireFrom&&t<FireTo+.05f?(Context.Reduced?.5f:(float)Math.Exp(-((t-FireFrom)%Rate)/(Rate*.35f))):0;
                NativeIntroPoses.AimRifle(pose,"machinegun",Wrap(yaw-facing),pitch+.03f*kick,up);
            }
        }
        /// <summary>The low carry: the right wrist by the right hip (the chest's frame, the keyed rig's proportions).</summary>
        private static readonly Vector3 CarryAt=new Vector3(-.2f,-.25f,.22f);
        private Vector3 MuzzleAt(float t)=>Anchor(NativeEntranceAnchor.RightMuzzle,t);
        private Vector3 BoreAt(float t){var b=WeaponBore(false,t);return b.LengthSquared()>1e-6f?b:Vector3.Normalize(Local(0,-.72f,.7f));}

        // ---- the brass: each round's case kicked out to the right, falling round the boots ----
        private bool Case(int i,float t,out Vector3 p,out Quaternion q)
        {
            float born=Fired(i)+.02f,a=t-born;p=default;q=Quaternion.Identity;if(a<0)return false;
            // from the drawn gun's ejection port as the round left it (model-aware anchors: the weapon frame and its bore at that moment)
            var bore=WeaponBore(false,born);var right=Vector3.Normalize(Vector3.Cross(bore,Vector3.UnitY));var port=Anchor(NativeEntranceAnchor.RightGrip,born)+bore*.12f+right*.06f+new Vector3(0,.04f,0);
            var v=right*(.9f+.45f*NativeEntranceMath.Rand(5200,i))+new Vector3(0,1.0f+.7f*NativeEntranceMath.Rand(5201,i),0)-bore*.25f;
            float land=(v.Y+(float)Math.Sqrt(v.Y*v.Y+2*9.8f*(port.Y-Floor)))/9.8f;
            if(a<land){p=Ballistic(port,v,a);q=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(NativeEntranceMath.Signed(5202,i),1,.3f)),a*(25+15*NativeEntranceMath.Rand(5203,i)));return true;}
            // a bounce and a skitter, then lying still
            float b=a-land;var rest=Ballistic(port,v,land);rest.Y=Floor+.007f;var skid=new Vector3(v.X,0,v.Z)*.18f*(1-(float)Math.Exp(-b*6));
            p=rest+skid+new Vector3(0,.08f*Math.Max(0,(float)Math.Sin(Math.Min(Math.PI,b*14))),0);q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,i*1.7f)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,1.5708f);return true;
        }

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("brass/yard"))return false;View(t,out var eye,out _);
            set.Piece("brass/sky",new Vector3(eye.X,0,eye.Z),Turn,1,Vector3.One,Vector3.One);
            set.Piece("brass/yard",Vector3.Zero,Turn,1,Vector3.One,Vector3.One);
            for(int i=0;i<3;i++){float on=Flood(i,t);set.Piece("brass/flood",S(Floods[i*4],Floods[i*4+1],Floods[i*4+2]),SYaw(-Floods[i*4+3]),1,Vector3.One,Vector3.One*(.04f+.96f*on));}
            for(int i=0;i<5;i++)
            {
                var p=S(Drums[i*2],0,Drums[i*2+1]);var q=Turn;
                if(i==Knocked&&t>KnockAt){float u=NativeEntranceMath.Clamp01((t-KnockAt)/.42f);float fall=NativeEntranceMath.EaseIn(u)*1.5708f;var away=SDir(.3f,0,1);var axis=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,away));
                    q=Quaternion.CreateFromAxisAngle(axis,fall)*Turn;p+=away*(.29f*(float)Math.Sin(fall))+new Vector3(0,.29f*(1-(float)Math.Cos(fall))*.0f,0);if(u>=1)p+=away*(.35f*(1-(float)Math.Exp(-(t-KnockAt-.42f)*3)));}
                set.Piece("brass/drum",p,q,1,Vector3.One,Vector3.One);
            }
            for(int i=0;i<3;i++)
            {
                if(i==Burst&&t>=BurstAt)continue;// (it is in pieces: the boards below)
                if(i==2&&t>=BurstAt+.05f)continue;// (the crate on top falls with it: in the boards too)
                float y=i==2?1.0f:0;set.Piece("brass/crate",S(Crates[i*4],y,Crates[i*4+1]),SYaw(-Crates[i*4+3]),Crates[i*4+2],Vector3.One,Vector3.One);
            }
            // the ammunition can on the bags; the heap of brass growing round the boots
            set.Piece("brass/can",S(-.55f,.012f+.31f,1.05f),SYaw(-.2f),1,Vector3.One,Vector3.One);
            float heap=NativeEntranceMath.Ramp(t,FireFrom+.3f,FireTo+.4f);if(heap>.01f)set.Piece("brass/pile",Vector3.Zero,Turn,.35f+.65f*heap,Vector3.One,Vector3.One);
            // the cases in flight and lying (instanced) and the burst crate's boards
            int n=0;for(int i=0;i<Rounds&&n<at.Length;i++){if(Case(i,t,out var p,out var q)){at[n]=p;turn[n]=q;n++;}}
            if(t>LastCase-.6f&&n<at.Length){LastCaseAt(t,out var p,out var q);at[n]=p;turn[n]=q;n++;}
            if(n>0)set.Batch("brass/casing",new ReadOnlySpan<Vector3>(at,0,n),new ReadOnlySpan<Quaternion>(turn,0,n),CaseScale,Vector3.One);
            if(t>=BurstAt)
            {
                n=0;var c=S(Crates[Burst*4],.5f,Crates[Burst*4+1]);float a=t-BurstAt;
                for(int i=0;i<12;i++){var v=(NativeEntranceMath.Direction(5300,i,true)+SDir(0,.2f,.6f))*(2.5f+2.5f*NativeEntranceMath.Rand(5301,i));var p=Ballistic(c,v,a);bool down=p.Y<Floor+.02f;if(down){float land=(v.Y+(float)Math.Sqrt(v.Y*v.Y+2*9.8f*(c.Y-Floor)))/9.8f;p=Ballistic(c,v,land);p.Y=Floor+.015f;}
                    at[n]=p;turn[n]=down?Quaternion.CreateFromAxisAngle(Vector3.UnitY,i*2.1f):Quaternion.CreateFromAxisAngle(Vector3.Normalize(NativeEntranceMath.Direction(5302,i)+Vector3.UnitY),a*(6+8*NativeEntranceMath.Rand(5303,i)));n++;}
                set.Batch("brass/board",new ReadOnlySpan<Vector3>(at,0,n),new ReadOnlySpan<Quaternion>(turn,0,n),1,Vector3.One);
            }
            return true;
        }
        /// <summary>The last case: left on the bags' edge, it rolls off and drops by the boots.</summary>
        private static void LastCaseAt(float t,out Vector3 p,out Quaternion q)
        {
            var edge=L(-.38f,.47f,1.0f);float a=t-(LastCase-.35f);
            if(a<0){p=edge;q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.4f)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,1.5708f);return;}
            var v=SDir(.2f,0,-.35f);if(a<.35f){p=edge+v*a+new Vector3(0,-4.9f*a*a*1.0f,0);if(p.Y<Floor+.007f)p.Y=Floor+.007f;q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.4f+a*3)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,1.5708f);return;}
            p=edge+v*.35f;p.Y=Floor+.007f;q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,1.45f)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,1.5708f);
        }
        /// <summary>Flood `i` switching on: a sodium lamp's stutter and warm-up (Reduced effects: a smooth rise).</summary>
        private float Flood(int i,float t)
        {
            float on=FloodOn[i];if(t<on)return 0;float a=t-on;
            if(Context.Reduced)return .3f+.7f*NativeEntranceMath.Ramp(a,0,.6f);
            float stutter=a<.12f?(a<.03f||a>.07f?1:.15f):1;return stutter*(.35f+.65f*NativeEntranceMath.Ramp(a,.05f,.7f));
        }
        /// <summary>How many holes the shutter has at `t` and how bright the light through them is.</summary>
        private static int Holes(float t){int n=0;for(int i=0;i<Rounds;i++){if(!Holed(i))continue;if(Fired(i)+Vector3.Distance(Grip,Strike(i))/TracerSpeed<=t)n++;}return n;}
        protected override NativeIntroVoidLook VoidLook(float t){var v=NativeIntroVoidLook.Of(Sodium);v.Far=NativeEntranceFx.Hex(0x0e0a08);v.Floor=NativeEntranceFx.Hex(0x1a1612);v.Pool=Sodium;v.Rim=Hot;return v;}

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();var far=NativeEntranceFx.Hex(0x140e0a);l.Background=far;l.Fog=far;l.FogNear=12;l.FogFar=240;l.DriveProbe=true;
            float lit=(Flood(0,t)+Flood(1,t)+Flood(2,t))/3;
            // The intro owns its light (docs/INTROS-V2.md 3a): the bank's set takes only the directionals and the ambient, so the floods'
            // sodium is the set's key from above the yard (rising with each flood) and the ambient warms with them; the point lights below
            // (the floods, the muzzle, the work light) light the pilot. The wet concrete mirrors the sodium at the horizon.
            l.Sky=NativeEntranceFx.Hex(0x5a4430,.14f+.5f*lit);l.Ground=NativeEntranceFx.Hex(0x2a1e14,.1f+.38f*lit);
            if(lit>.01f)l.Directional(SDir(0,1.2f,.35f),Sodium*(1.15f*lit));
            l.Directional(SDir(.4f,1.4f,-.8f),NativeEntranceFx.Hex(0x6a7aa0,.16f));
            l.ReflectSky=NativeEntranceFx.Hex(0x0e0a08);l.ReflectHorizon=Sodium*(.12f+.55f*lit);l.ReflectGround=NativeEntranceFx.Hex(0x1a1410,.3f);
            for(int i=0;i<3;i++){float on=Flood(i,t);if(on<=.01f)continue;var p=S(Floods[i*4],Floods[i*4+1],Floods[i*4+2]);l.Add(p+(S(0,0,6)-p)*.08f,Sodium*(9*on),18);}
            // the muzzle: strobing with each round (steady in Reduced), the hot barrel after
            if(t>=FireFrom&&t<FireTo+.06f)
            {
                float k=Context.Reduced?.55f:(float)Math.Exp(-((t-FireFrom)%Rate)/(Rate*.3f));l.Add(MuzzleAt(t)+BoreAt(t)*.15f,Hot*(10*k),7);
            }
            float heat=NativeEntranceMath.Ramp(t,FireFrom+.6f,FireTo)*(1-NativeEntranceMath.Ramp(t,FireTo+.5f,Total));if(heat>.02f)l.Add(MuzzleAt(t)-BoreAt(t)*.2f,NativeEntranceFx.Hex(0xff5a1c,1.4f*heat),1.6f);
            // the work light leaking out through the holes onto the yard and the pilot
            float leak=Math.Min(1,Holes(t)/24f);if(leak>.01f)l.Add(L(0,2.2f,ShutterZ-1.5f),Work*(5*leak),16);
            var chest=Anchor(NativeEntranceAnchor.Chest,t);float card=NativeEntranceMath.Ramp(t,CardAt-.4f,CardAt+.4f);if(card>.01f)l.Add(chest+SDir(1.4f,.6f,1.5f),Sodium*(1.8f*card),6);
        }

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            View(t,out var eye,out var fwd);float floor=Floor;bool reduced=Context.Reduced;
            // the floods' haloes and the dust hanging in their cones
            for(int i=0;i<3;i++){float on=Flood(i,t);if(on<=.01f)continue;var p=S(Floods[i*4],Floods[i*4+1],Floods[i*4+2]);fx.Glow(p,Sodium*(2.2f*on),0,.9f,1,0,6000+i);fx.Glow(p,Sodium*(.5f*on),0,3.2f,0,0,6010+i);
                NativeEntranceFx.Shaft(fx,t,p,Vector3.Lerp(p,S(0,0,6),.75f)+new Vector3(0,-p.Y*.6f,0),.3f,2.6f,Sodium*.06f,on,q<.5f?4:6,q<.5f?4:10,6020+i*20,q);}
            // the burst: each round's flash (one at a time; a steady flame in Reduced), tracer, strike, case
            var wrist=Anchor(NativeEntranceAnchor.RightGrip,t);var bore=BoreAt(t);var muzzle=MuzzleAt(t);
            if(t>=FireFrom&&t<FireTo+.06f)
            {
                if(reduced){fx.Glow(muzzle+bore*.12f,Hot*1.6f,0,.35f,1,0,6100);fx.Glow(muzzle+bore*.2f,Sodium*1.1f,0,.8f,0,0,6101);}
                else{int i=Math.Min(Rounds-1,(int)((t-FireFrom)/Rate));Muzzle(fx,t,Fired(i),muzzle,bore,.26f,Hot,6110+(i%4)*12,q,Rate*.9f);}
                NativeEntranceFx.Jet(fx,t,FireFrom,FireTo,muzzle,bore+new Vector3(0,.3f,0),30,1.2f,1.4f,.28f,Smoke,.28f,6170,q,floor,.4f,.35f);
            }
            int first=Math.Max(0,(int)((t-FireFrom-.6f)/Rate)),last=Math.Min(Rounds-1,(int)((t-FireFrom)/Rate));
            for(int i=first;i<=last;i++)
            {
                float fired=Fired(i);if(t<fired)continue;var strike=Strike(i);var from=Anchor(NativeEntranceAnchor.RightMuzzle,fired);var d=strike-from;float len=d.Length();var dir=d/len;float a=t-fired,travel=len/TracerSpeed;
                if(i%3==0&&a<travel)
                {
                    float u=a/travel;var head=from+d*u;Line(fx,eye,fwd,head,head-dir*Math.Min(2.6f,len*u),NativeEntranceFx.Hex(0xffb050,2.6f),.022f,.85f,0,6200+i);
                    if(!reduced)fx.Glow(head,Hot*1.4f,0,.12f,0,0,6250+i);
                }
                if(a>=travel&&a<travel+.45f&&last-i<8)
                {
                    bool steel=Math.Abs(i-CrateRound)>1;NativeEntranceFx.Sparks(fx,t,fired+travel,strike,-dir+new Vector3(0,.3f,0),1.1f,q<.5f?4:9,5.5f,.4f,9.8f,1.2f,Hot*2.6f,Sodium*2,.012f,6300+i*13,q,floor);
                    if(!steel)NativeEntranceFx.Sparks(fx,t,fired+travel,strike,-dir,.9f,q<.5f?2:5,3,.6f,9.8f,1,NativeEntranceFx.Hex(0xb08a5a,1.2f),NativeEntranceFx.Hex(0x5a4430,.6f),.02f,6340+i*13,q,floor);
                }
            }
            // the brass: a glint on each case as it turns in the light (not in Reduced: glints would flicker)
            if(!reduced)for(int i=first;i<=last;i++){if(Case(i,t,out var p,out _)&&t-Fired(i)<.5f){float g=(float)Math.Max(0,Math.Sin((t-Fired(i))*38+i));if(g>.75f)fx.Glow(p,Brass*(1.6f*(g-.75f)*4),0,.025f,0,0,6400+i);}}
            // the knocked drum's sparks and its oil, the crate bursting
            NativeEntranceFx.Sparks(fx,t,KnockAt,S(Drums[0],.6f,Drums[1]),SDir(0,.4f,-1),1,q<.5f?6:14,5,.5f,9.8f,1,Hot*2.6f,Sodium*2,.014f,6500,q,floor);
            NativeEntranceFx.GroundDust(fx,t,KnockAt+.42f,S(Drums[0],0,Drums[1]+.4f),q<.5f?4:9,.2f,1.4f,.8f,new Vector3(.25f,.22f,.2f),.35f,.3f,6520,q,floor);
            NativeEntranceFx.GroundDust(fx,t,BurstAt,S(Crates[0],.3f,Crates[1]),q<.5f?6:12,.3f,2.2f,1.0f,NativeEntranceFx.Hex(0x8a7050),.4f,.4f,6540,q,floor);
            // the yard filling with gun smoke, then thinning (what the light rays need to be seen in)
            float smoke=NativeEntranceMath.Ramp(t,FireFrom+.4f,FireTo)*(1-.75f*NativeEntranceMath.Ramp(t,Dry+.3f,Total));int puffs=q<.5f?10:22;
            for(int i=0;i<puffs;i++)
            {
                var p=L(NativeEntranceMath.Signed(6600,i)*4.5f,.4f+1.8f*NativeEntranceMath.Rand(6601,i),1.2f+9.5f*NativeEntranceMath.Rand(6602,i))+new Vector3((float)Math.Sin(t*.3f+i)*.4f,t*.05f,(float)Math.Cos(t*.27f+i)*.4f);
                fx.Smoke(p,Smoke,.28f*smoke*(.6f+.4f*NativeEntranceMath.Rand(6603,i)),1.4f+1.2f*NativeEntranceMath.Rand(6604,i),.5f,6600+i,floor);
            }
            // the light through the holes: a point of white on the steel at each, and its ray running out from the work light through it
            // into the smoke (drawn from its far end when the hole is behind the camera, so it streams past); the haze stays for the name
            int holes=0,rays=q<.5f?20:44;float haze=NativeEntranceMath.Ramp(t,FireFrom+.4f,FireTo)*(1-.3f*NativeEntranceMath.Ramp(t,Dry+.3f,Total));
            for(int i=0;i<Rounds;i++)
            {
                if(!Holed(i))continue;float hitAt=Fired(i)+Vector3.Distance(Grip,Strike(i))/TracerSpeed;if(t<hitAt)continue;
                for(int s=0;s<2;s++)
                {
                    var h=Strike(i)+(s==0?Vector3.Zero:SDir(NativeEntranceMath.Signed(6700,i)*.22f,NativeEntranceMath.Signed(6701,i)*.18f,0));float glow=NativeEntranceMath.Ramp(t,hitAt,hitAt+.25f);
                    fx.Glow(h,Work*(1.8f*glow),0,.035f,1,0,6800+i*2+s);
                    if(holes<rays)
                    {
                        int r=i*2+s;float ray=glow*haze*(.55f+.45f*NativeEntranceMath.Rand(6702,r));
                        if(ray>.02f)
                        {
                            var far=h+Vector3.Normalize(h-Lamp)*(8+4*NativeEntranceMath.Rand(6704,r));var c=Work*(.15f*ray);float w=.12f+.12f*NativeEntranceMath.Rand(6705,r);
                            if(Vector3.Dot(h-eye,fwd)>.4f)Line(fx,eye,fwd,h,far,c,w,.85f,0,6900+r);else Line(fx,eye,fwd,far,h,c,w,.45f,0,6900+r);
                        }
                    }
                    holes++;
                }
            }
            // the dry click's last wisp; the barrel glowing and smoking
            float heat=NativeEntranceMath.Ramp(t,FireFrom+.6f,FireTo)*(1-NativeEntranceMath.Ramp(t,FireTo+.5f,Total));
            if(heat>.02f){for(int k=0;k<4;k++)fx.Glow(wrist+bore*(.4f+.1f*k),NativeEntranceFx.Hex(0xff5a1c,1.6f*heat*(1-.15f*k)),0,.05f,1,0,7000+k);
                NativeEntranceFx.Jet(fx,t,FireTo,Total,muzzle,new Vector3(0,1,0),9,.2f,1.6f,.14f,new Vector3(.55f,.53f,.52f),.35f*heat,7010,q,floor,.35f,.3f);}
            // under the name: sodium dust drifting in the rays
            if(t>CardAt-.5f)Motes(fx,t,q,L(0,1.4f,2.5f),new Vector3(3,1.2f,2.5f),Sodium*.16f,.025f,24,7100,.2f);
        }

        // ---- sound: its own cues, a round each on the presentation clock (so the slowed rounds slow with the picture) ----
        private static readonly NativeIntroCue[] cues=BuildCues();
        private static NativeIntroCue[] BuildCues()
        {
            var l=new System.Collections.Generic.List<NativeIntroCue>
            {
                new NativeIntroCue(.12f,"intro2.brass.belt","gun.reload.end","reload-end",.7f,.8f),
                new NativeIntroCue(Flood1,"intro2.brass.flood","ui.install.lock","metal",.6f,.7f),
                new NativeIntroCue(Flood2,"intro2.brass.flood","ui.install.lock","metal",.6f,.68f),
                new NativeIntroCue(Flood3,"intro2.brass.flood","ui.install.lock","metal",.6f,.72f),
                new NativeIntroCue(Flood1+.05f,"intro2.brass.hum","boss.hum.row.buzz",null,.35f),
                new NativeIntroCue(StepUp+.28f,"intro2.brass.boot","move.land","jump",.4f),
                new NativeIntroCue(Rack,"intro2.brass.rack","gun.reload.start","reload-start",.8f),
            };
            for(int i=0;i<Rounds;i++)l.Add(new NativeIntroCue(Fired(i),"intro2.brass.round","gun.machinegun.shot","machinegun",i%3==0?.95f:.8f,.96f+.08f*NativeEntranceMath.Rand(5400,i)));
            for(int i=2;i<Rounds;i+=5)l.Add(new NativeIntroCue(Fired(i)+.1f,"intro2.brass.impacts","impact.metal","metal",.45f,1.1f));
            l.Add(new NativeIntroCue(FireFrom+.35f,"intro2.brass.casings",null,"metal",.25f,1.9f));
            l.Add(new NativeIntroCue(KnockAt+.42f,"intro2.brass.drum","impact.metal","metal",.6f,.6f));
            l.Add(new NativeIntroCue(BurstAt,"intro2.brass.crate","break.wood","wood",.7f));
            l.Add(new NativeIntroCue(Dry,"intro2.brass.dry","gun.dryfire","reload-end",.8f));
            l.Add(new NativeIntroCue(Dry+.12f,"intro2.brass.hiss",null,"fire",.3f,.7f));
            l.Add(new NativeIntroCue(LastCase,"intro2.brass.tink","ui.click","metal",.35f,1.7f));
            l.Add(new NativeIntroCue(CardAt,"intro2.brass.name","impact.metal","metal",.55f,.55f));
            l.Sort((a,b)=>a.At.CompareTo(b.At));return l.ToArray();
        }
        public override NativeIntroCue[] Cues=>cues;
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.X=.26f;card.Y=.36f;card.Style=NativeIntroCardStyle.Slam;card.Ink=NativeEntranceFx.Hex(0xfff0dc);card.Glow=NativeEntranceFx.Hex(0xff9a30,1.2f);
        }
    }
}
