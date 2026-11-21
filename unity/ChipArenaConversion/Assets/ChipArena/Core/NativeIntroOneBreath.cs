using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "One Breath", the rifle's v2 intro (I4, owner 2026-11-20: replaces Longwatch; docs/INTROS-RANGED.md). 8.4 s, the name at 6.4 s.
    /// A rain-soaked rooftop at night, sixty metres over the avenue. The camera cranes down out of the rain onto the pilot knelt at
    /// the parapet, the rifle laid on the coping; across the avenue a surveillance drone hangs between the towers with its red eye.
    /// Close on the scope's lens, the drone's eye in it; from the drone's side, far off, the scope glints and the eye turns. One
    /// breath, held: the rain slows to a hang. The shot: the brake's jets, the rain thrown back from the muzzle, and the camera
    /// rides the round across the avenue between the towers into the eye. It bursts; the drone falls burning between the towers.
    /// The spent case rings on the wet coping and steams. The pilot rises with the rifle low, the beacon blinking behind, the name.
    /// Presentation only: pure functions of the presentation clock, thinned by quality; every beat kept in Reduced effects.
    /// </summary>
    public sealed class NativeIntroOneBreath:NativeRangedIntro,INativeIntroGrip
    {
        public const string Id=NativeIntroCatalog.WeaponPrefix+"rifle";
        public const float Total=8.4f,CardAt=6.4f;
        public const float Lightning=.95f,LensAt=1.3f,GlintAt=2.22f,BreathFrom=2.62f,ShotAt=3.3f,HitAt=3.6f,CaseLands=4.72f,FallTo=5.5f,RiseFrom=5.35f,RiseTo=6.05f;
        /// <summary>The drone's centre (source frame), and the rifle's line to it while the pilot aims: the drawn rifle's grip (the weapon
        /// frame's origin on the drawn model, shouldered, the breath held; Tests/IntroRangedChecks holds it there) and the bore to the drone.</summary>
        public static readonly Vector3 Drone=S(6,13,134),AimWrist=L(-.181f,1.008f,.425f);
        public static readonly Vector3 AimDir=Vector3.Normalize(Drone-AimWrist);
        /// <summary>The kneel's facing (the hips square to the drone; the chest bladed off it by the aim) and the aim's pitch up to it.</summary>
        public static readonly float KneelYaw=(float)Math.Atan2(AimDir.X,AimDir.Z),AimPitch=(float)Math.Asin(AimDir.Y);
        private static readonly Vector3 Up=Vector3.Normalize(Vector3.UnitY-AimDir*AimDir.Y),Right=Vector3.Normalize(Vector3.Cross(AimDir,Vector3.UnitY));
        /// <summary>The drawn rifle's scope (its objective lens .31 ahead of the grip and .055 over the weapon frame's origin, as the Editor
        /// draws it: hold 6's lens macro put the model's .175 a hand over the glass) and the muzzle's crown along the bore (NativeMuzzleFx.Crown,
        /// the Standard skin).</summary>
        public const float ScopeY=.055f,ScopeAt=.31f,EyepieceAt=-.135f,MuzzleAt=1.21f;
        /// <summary>The scope's lens on the aim (for the camera).</summary>
        private static readonly Vector3 AimLens=AimWrist+AimDir*ScopeAt+Up*ScopeY;
        private static readonly Vector3 Ice=NativeEntranceFx.Hex(0x9fd8ff),Red=NativeEntranceFx.Hex(0xff2a20),Warm=NativeEntranceFx.Hex(0xffb070),Rain=NativeEntranceFx.Hex(0x9ab0d0);
        private static readonly Vector3 Beacon=S(-7.4f,.012f+8.3f,-.6f),Bulb=S(6.0f,2.4f,-8.2f+1.9f);
        private static readonly Vector3[] Rotor={new Vector3(.813f,.1f,.813f),new Vector3(-.813f,.1f,.813f),new Vector3(.813f,.1f,-.813f),new Vector3(-.813f,.1f,-.813f)};
        // the near city blocks (ranged_onebreath.py BLOCKS): block letter, Unity x, z, yaw, scale
        private static readonly string[] BlockIds={"onebreath/block-a","onebreath/block-b","onebreath/block-c","onebreath/block-d","onebreath/block-e"};
        private static readonly float[] Blocks={0,-40,120,0,1, 1,48,145,.15f,.9f, 2,-105,70,-1.1f,.85f, 3,110,60,1.1f,.85f, 4,-60,-110,(float)Math.PI-.5f,.85f};
        private readonly Vector3[] at=new Vector3[12];private readonly Quaternion[] turn=new Quaternion[12];

        public NativeIntroOneBreath():base(Id,Total,CardAt,ShotAt,0x9fd8ff)
        {
            // ---- the keyed body (ModelFeet: the stance is the drawn model's ankles, the drops the drawn hips', docs/INTROS-V2.md 9c) ----
            // Knelt at the parapet on the right knee, the left foot planted ahead, square to the drone; the torso bladed as the match aims a
            // long gun (NativeIntroPoses.RifleAim: the left shoulder forward, the cheek down on the stock), leaning into the rifle. The rifle
            // shouldered on the drone with its fore-end over the coping (Arms: NativeIntroPoses.AimRifle), the off fist on the handguard by
            // the grip (GripAt). One breath: the chest fills and empties, the shoulders settle; the shot's kick rocks the body back through
            // the shoulder. Then up off the knee: the weight forward over the front foot, the knee lifts, the back foot steps up beside it as
            // the hips rise, the rifle down from the shoulder to the low carry, squared to the facing for the name.
            Start=Stance(.13f,.42f,.1f,-.13f,-.1f,-.3f,KneelYaw);
            Steps=new[]{Step(false,RiseFrom+.2f,RiseFrom+.52f,-.15f,.3f,-.18f,.07f),Step(true,RiseTo-.06f,RiseTo+.22f,.14f,.4f,.12f,.035f)};
            float[] kneel(float breath,float lean,float head)=>NativeEntranceKeys.With(NativeIntroPoses.Kneel(),v=>
            {
                v[NativeEntranceKeys.Yaw]=KneelYaw;v[NativeEntranceKeys.BodyY]=-.41f+.012f*breath;// (a high kneel: the fore-end over the coping)
                // (RifleAim's blade on the kneel: the spine and chest turned right, the clavicles drawn; the neck bowed forward and the head
                // tilted back level onto the line, rolled onto the stock: the visor at the scope's eyepiece, Tests/IntroRangedChecks)
                NativeEntranceKeys.Set(v,B,lean,0,0);NativeEntranceKeys.Set(v,Sp,.02f+.035f*breath,-.27f,0);NativeEntranceKeys.Set(v,C,.03f-.05f*breath,-.33f,0);
                NativeEntranceKeys.Set(v,CapturedRig.Neck,.45f,0,.25f);NativeEntranceKeys.Set(v,Hd,-.45f+head,.6f,.15f);
                NativeEntranceKeys.Set(v,CapturedRig.LClav,0,-.6f,0);NativeEntranceKeys.Set(v,CapturedRig.RClav,0,.9f-.08f*breath,0);
            });
            var k=new NativeEntranceKeys();
            k.Key(0,kneel(0,.1f,0));
            k.Key(BreathFrom,kneel(0,.1f,0));
            // the breath in, held at the top (the scope steadies), the shot on the held breath
            k.Key(BreathFrom+.38f,kneel(1,.09f,.01f)).PassKey();
            k.Key(ShotAt,kneel(.9f,.095f,.015f));
            // the kick: the shoulder driven back, the head off the stock a touch, rippling out; then the breath out
            k.Key(ShotAt+.05f,kneel(.6f,.03f,-.04f)).ImpactKey();
            k.Key(ShotAt+.55f,kneel(.1f,.09f,0));
            k.Key(RiseFrom,kneel(0,.11f,.02f));
            // up: the weight over the front foot (the hips forward, the chest over the knee), the knee lifting off the floor
            k.Key(RiseFrom+.22f,NativeIntroPoses.Kneel()).Root(Fwd(.08f).X,.88f,Fwd(.08f).Z,KneelYaw+(Facing-KneelYaw)*.3f).Drop(-.42f).J(B,.32f).J(Sp,.06f,-.12f).J(C,.04f,-.1f).J(Hd,-.12f,.18f).PassKey();
            // the drive: the front leg straightening, the back foot coming through to land beside it, the chest lifting
            k.Key(RiseFrom+.48f,NativeEntranceKeys.Stand()).Root(Fwd(.26f).X,.88f,Fwd(.26f).Z,Facing).Drop(-.16f).J(B,.16f).J(Sp,.03f).J(C,.02f,.04f).J(Hd,-.08f,.06f).PassKey();
            // standing: a small settle past the top, then the weight back on the heels, the chin up, for the name
            k.Key(RiseTo,NativeIntroPoses.Hero()).Root(Fwd(.3f).X,.88f,Fwd(.3f).Z,Facing).Drop(-.01f).J(B,.02f).J(C,-.04f,-.02f).J(Hd,-.02f,-.06f);
            k.Key(RiseTo+.35f,NativeIntroPoses.Hero()).Root(Fwd(.3f).X,.88f,Fwd(.3f).Z,Facing).Drop(-.03f).J(C,-.06f,-.06f).J(Hd,.01f,-.05f);
            k.Key(Total,NativeIntroPoses.Hero()).Root(Fwd(.3f).X,.88f,Fwd(.3f).Z,Facing).Drop(-.03f).J(C,-.06f,-.08f).J(Hd,.02f,-.07f);Keys=k;

            // ---- the camera ----
            var scope=AimLens;var muzzle=AimWrist+AimDir*MuzzleAt;
            var flyFrom=muzzle+AimDir*.6f+Right*.5f+Up*.22f;var flyTo=Drone-AimDir*4.5f+Right*1.4f+Up*.6f;
            Rig.Spline(0,new[]{L(-3.6f,8.2f,-9.4f),L(-2.7f,5.2f,-6.2f),L(-1.5f,2.3f,-3.1f)},L(0,.9f,3),L(0,1.25f,9),46,40).Ease(NativeIntroEase.InOut).Focus(0,0,.25f)
               .Dolly(LensAt,scope+Right*.34f+AimDir*.42f+Up*.05f,scope+Right*.3f+AimDir*.36f+Up*.05f,scope,scope,20,18).Focus(0,0,.8f)
               .Dolly(2.0f,Drone-AimDir*3.5f+Right*1.2f+Up*.3f,Drone-AimDir*3.6f+Right*1.1f+Up*.3f,scope+new Vector3(0,.5f,0),scope+new Vector3(0,.55f,0),2.3f,1.9f).Focus(0,0,.15f)
               .Dolly(BreathFrom,L(-1.5f,1.3f,.52f),L(-1.38f,1.28f,.58f),L(-.1f,1.2f,.36f),L(-.1f,1.21f,.38f),30,27).Focus(0,0,.6f)
               .Dolly(ShotAt-.03f,muzzle+Right*1.25f+AimDir*1.4f+Up*.25f,muzzle+Right*1.2f+AimDir*1.32f+Up*.28f,muzzle-AimDir*.4f,muzzle-AimDir*.35f,40,38)
               .Dolly(ShotAt+.06f,flyFrom,flyTo,flyFrom+AimDir*6,Drone,48,30).Ease(NativeIntroEase.Linear)
               .Dolly(HitAt+.02f,L(-.95f,1.78f,-1.65f),L(-.9f,1.76f,-1.55f),Drone,Drone-new Vector3(0,9,0),7,9.5f).Focus(0,0,.1f)
               .Dolly(CaseLands-.32f,L(-1.08f,1.16f,1.96f),L(-1.0f,1.14f,1.88f),L(-.48f,1.05f,1.32f),L(-.5f,1.05f,1.3f),30,28).Focus(0,0,.75f)
               .Dolly(RiseFrom,L(-2.25f,.72f,.85f),L(-2.05f,.82f,1.0f),L(0,1.02f,.15f),L(0,1.36f,.3f),36,34).Focus(0,0,.4f)
               .Dolly(RiseTo-.1f,L(1.4f,1.32f,3.6f),L(1.08f,1.34f,3.1f),L(0,1.22f,.3f),L(0,1.27f,.3f),34,31).Ease(NativeIntroEase.Out).In(NativeIntroCut.Blend,.5f).Focus(0,0,.45f)
               .Bars(0,1).Bars(CardAt-.35f,.55f).Black(0,1).Black(.08f,0);
            // The breath held: the shot and the round's flight slowed; repaid while the drone falls.
            Ramps.Ramp(3.22f,.3f,1.15f,1.3f);
        }
        // ---- the body on the model (docs/INTROS-V2.md 9a-9c) ----
        /// <summary>The stance and steps are the drawn model's ankles, the drops the drawn hips'.</summary>
        protected override bool ModelFeet=>true;
        /// <summary>The right knee on the floor until the rise lifts it.</summary>
        protected override float ModelKneel(float t,bool left)=>left?0:1-NativeEntranceMath.Ramp(t,RiseFrom+.06f,RiseFrom+.24f);
        /// <summary>The off hand on the handguard throughout (the grip IK); the stock in the drawn shoulder while the pilot aims, off it as
        /// the rifle comes down to the carry.</summary>
        public bool GripAt(float t,out NativeIntroGripAt g){g=new NativeIntroGripAt(1,default,0,0,1-NativeEntranceMath.Ramp(t,RiseFrom+.04f,RiseFrom+.36f));return true;}
        /// <summary>Only the main hand is keyed here (the off hand is the grip's): the match's long-gun aim (the wrist under the cheek, the butt
        /// in the shoulder pocket) onto the drone, the shot's kick lifting the muzzle a hair; then down to the low carry (the owner's rest-low
        /// rule: the muzzle down and across the body), the stock off the shoulder.</summary>
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            float kick=NativeEntranceMath.Pulse(t,ShotAt,.015f,.14f),carry=NativeEntranceMath.Ramp(t,RiseFrom+.04f,RiseFrom+.46f);
            var f=Vector3.Transform(Vector3.UnitZ,rotation);float facing=(float)Math.Atan2(f.X,f.Z);
            if(carry<.999f)NativeIntroPoses.AimRifle(pose,"rifle",Wrap(KneelYaw-facing),AimPitch+.06f*kick);
            if(carry>.001f)NativeIntroGrip.Hold(pose,false,"rifle",CarryAt,NativeIntroGrip.Aim(pose,CarryYaw,CarryPitch),new Vector3(-1,-.7f,-.2f),carry);
        }
        /// <summary>The low carry: the right wrist by the right hip (the chest's frame, the keyed rig's proportions), the bore down and across.</summary>
        private static readonly Vector3 CarryAt=new Vector3(-.2f,-.25f,.22f);private const float CarryYaw=.5f,CarryPitch=-.8f;
        private static float Wrap(float a){while(a>Math.PI)a-=2*(float)Math.PI;while(a<-Math.PI)a+=2*(float)Math.PI;return a;}
        /// <summary>A point `z` metres ahead along the hero facing (the root's floor point).</summary>
        private static Vector3 Fwd(float z)=>Local(0,0,z);
        /// <summary>The rifle at `t` (model-aware anchors, docs/INTROS-V2.md 2a): the drawn grip (the weapon frame's origin), the bore, the
        /// rifle's up (the bore has no roll here) and its muzzle.</summary>
        private void Rifle(float t,out Vector3 grip,out Vector3 bore,out Vector3 up,out Vector3 muzzle)
        {
            grip=Anchor(NativeEntranceAnchor.RightGrip,t);bore=WeaponBore(false,t);if(bore.LengthSquared()<1e-6f)bore=AimDir;
            up=Vector3.UnitY-bore*bore.Y;up=up.LengthSquared()>1e-6f?Vector3.Normalize(up):Vector3.UnitY;muzzle=Anchor(NativeEntranceAnchor.RightMuzzle,t);
        }
        private static readonly Vector3 Facing0=Local(0,0,1);
        /// <summary>Where the spent case comes to rest on the coping, right of the barrel.</summary>
        private static readonly Vector3 CaseRest=L(-.5f,1.035f,1.3f);

        // ---- the drone: hovering, turning its eye to the glint, struck, falling burning between the towers ----
        public static Vector3 DroneAt(float t,out Quaternion q)
        {
            var face=Vector3.Normalize(new Vector3(AimWrist.X-Drone.X,0,AimWrist.Z-Drone.Z));float yaw=(float)Math.Atan2(face.X,face.Z);
            float look=.35f*(1-NativeEntranceMath.Ramp(t,GlintAt,GlintAt+.5f));float bob=.18f*(float)Math.Sin(t*1.7f),sway=.12f*(float)Math.Sin(t*.9f+1);
            if(t<HitAt){q=Quaternion.CreateFromYawPitchRoll(yaw+look,.05f*(float)Math.Sin(t*1.3f),.04f*(float)Math.Sin(t*1.1f));return Drone+new Vector3(sway,bob,0);}
            float a=t-HitAt;var back=-face;var p=Drone+new Vector3(sway,bob,0)+back*(2.2f*(1-(float)Math.Exp(-a*3)))+new Vector3(0,-10f*a*a,0)+Right*(1.2f*a);
            q=Quaternion.CreateFromYawPitchRoll(yaw+a*2.4f,.6f*a+.4f*NativeEntranceMath.Ramp(a,0,.25f),1.6f*a);return p;
        }

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("onebreath/roof"))return false;View(t,out var eye,out _);var ground=new Vector3(eye.X,0,eye.Z);
            float bolt=Flash(t,Lightning,.02f,.25f);
            set.Piece("onebreath/sky",ground,Turn,1,Vector3.One,Vector3.One*(1+.9f*bolt));
            set.Piece("onebreath/farcity",ground,Turn,1,Vector3.One,Vector3.One);
            set.Piece("onebreath/roof",Vector3.Zero,Turn,1,Vector3.One,Vector3.One);
            set.Piece("onebreath/street",Vector3.Zero,Turn,1,Vector3.One,Vector3.One);
            for(int i=0;i<5;i++){int o=i*5;set.Piece(BlockIds[(int)Blocks[o]],S(Blocks[o+1],0,Blocks[o+2]),SYaw(-Blocks[o+3]),Blocks[o+4],Vector3.One,Vector3.One);}
            set.Piece("onebreath/billboard",S(4,26,153),Turn,1,Vector3.One,Vector3.One*(.85f+.15f*(float)Math.Sin(t*2.3f)));
            // the beacon, blinking once a second (well under 3 Hz)
            float blink=.15f+.85f*NativeEntranceMath.Bump(t%1.2f,0,.45f);set.Piece("onebreath/beacon",Beacon,Turn,1,Vector3.One,Vector3.One*blink);
            // the drone, its eye (out once struck) and its four rotors
            if(t<FallTo+.3f)
            {
                var d=DroneAt(t,out var dq);set.Piece("onebreath/drone",d,dq,1,Vector3.One,Vector3.One);
                float eyeOn=t<HitAt?.9f+.1f*(float)Math.Sin(t*5):0;set.Piece("onebreath/eye",d,dq,1,Vector3.One,Vector3.One*eyeOn);
                float spin=t<HitAt?t*42:HitAt*42+(t-HitAt)*42*(float)Math.Exp(-(t-HitAt)*1.5f);
                for(int r=0;r<4;r++)set.Piece("onebreath/rotor",d+Vector3.Transform(Rotor[r],dq),dq*Quaternion.CreateFromAxisAngle(Vector3.UnitY,spin*(r%2==0?1:-1)+r),1,Vector3.One,Vector3.One);
            }
            // the eye's shards and torn panels, flung out from the strike (instanced)
            if(t>=HitAt&&t<HitAt+2.5f)
            {
                var o=DroneAt(HitAt,out _);int n=0;float a=t-HitAt;
                for(int i=0;i<8;i++){var v=(NativeEntranceMath.Direction(8100,i)+(AimDir*-1.2f))*(3+4*NativeEntranceMath.Rand(8100,i+20));at[n]=Ballistic(o,v,a,6);turn[n]=Quaternion.CreateFromAxisAngle(Vector3.Normalize(NativeEntranceMath.Direction(8120,i)+Vector3.UnitY),a*(8+9*NativeEntranceMath.Rand(8130,i)));n++;}
                set.Batch("onebreath/shard",new ReadOnlySpan<Vector3>(at,0,n),new ReadOnlySpan<Quaternion>(turn,0,n),2.2f,Vector3.One);
                n=0;for(int i=0;i<4;i++){var v=(NativeEntranceMath.Direction(8200,i)-AimDir*.6f)*(2.5f+2*NativeEntranceMath.Rand(8200,i+20));at[n]=Ballistic(o,v,a,9.8f);turn[n]=Quaternion.CreateFromAxisAngle(Vector3.Normalize(NativeEntranceMath.Direction(8220,i)+Vector3.UnitX),a*7);n++;}
                set.Batch("onebreath/panel",new ReadOnlySpan<Vector3>(at,0,n),new ReadOnlySpan<Quaternion>(turn,0,n),2.0f,Vector3.One);
            }
            // the spent case: kicked out at the shot, tumbling (slowed), ringing down onto the wet coping and rolling to rest
            if(t>=ShotAt)
            {
                Case(t,out var p,out var q);at[0]=p;turn[0]=q;set.Batch("onebreath/casing",new ReadOnlySpan<Vector3>(at,0,1),new ReadOnlySpan<Quaternion>(turn,0,1),1,Vector3.One);
            }
            return true;
        }
        private void Case(float t,out Vector3 p,out Quaternion q)
        {
            var port=Anchor(NativeEntranceAnchor.RightGrip,ShotAt)+WeaponBore(false,ShotAt)*.07f+Right*.05f+Up*.02f;var v=Right*1.4f+Up*1.9f-AimDir*.4f;float a=t-ShotAt;float land=CaseLands-ShotAt;
            var end=CaseRest;
            if(a<land){float u=a/land;p=Vector3.Lerp(port,end,u)+new Vector3(0,(1.3f)*4*u*(1-u),0);q=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(.3f,1,.2f)),a*22)*Look(AimDir,Vector3.UnitY);return;}
            float r=Math.Min(1,(t-CaseLands)/.6f);float roll=1-(1-r)*(1-r);p=end+Facing0*(-.12f*roll)+Right*(.03f*roll)+new Vector3(0,.004f+.03f*NativeEntranceMath.Pulse(t,CaseLands,.01f,.06f),0);
            q=Quaternion.CreateFromAxisAngle(Facing0,roll*5)*Look(Vector3.Normalize(Right+Facing0*.3f),Vector3.UnitY);
        }
        protected override NativeIntroVoidLook VoidLook(float t){var v=NativeIntroVoidLook.Of(Ice);v.Far=NativeEntranceFx.Hex(0x080b14);v.Floor=NativeEntranceFx.Hex(0x10141c);v.Pool=Ice;v.Rim=Red;return v;}

        // ---- the light: rain-dark night, the city's glow, the bulb and the beacon, the lightning, the shot ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();float bolt=Flash(t,Lightning,.02f,.22f);var far=NativeEntranceFx.Hex(0x1a2234);l.Background=far;l.Fog=far;l.FogNear=14;l.FogFar=420;l.DriveProbe=true;
            // The intro owns its light (docs/INTROS-V2.md 3a): the roof takes the directionals and the ambient; the wet deck mirrors the
            // city's neon haze at the horizon.
            l.Sky=NativeEntranceFx.Hex(0x4a5a7a,.5f+.9f*bolt);l.Ground=NativeEntranceFx.Hex(0x2a2a38,.38f);
            l.ReflectSky=NativeEntranceFx.Hex(0x1a2234,.8f);l.ReflectHorizon=NativeEntranceFx.Hex(0x6a4a8a,.7f+.8f*bolt);l.ReflectGround=NativeEntranceFx.Hex(0x101018);
            l.Directional(SDir(-.4f,1.2f,-.6f),NativeEntranceFx.Hex(0x8aa2d8,.38f+2.2f*bolt));
            l.Directional(SDir(.2f,.25f,1),NativeEntranceFx.Hex(0xff9a60,.16f));
            var chest=Anchor(NativeEntranceAnchor.Chest,t);
            l.Add(Bulb,NativeEntranceFx.Hex(0xffd8a0,1.6f),6);
            float blink=NativeEntranceMath.Bump(t%1.2f,0,.45f);l.Add(Beacon,Red*(1.2f*blink),9);
            l.Add(chest+SDir(-.6f,.9f,-1.8f),NativeEntranceFx.Hex(0x8fb8ff,1.4f),6);
            float shot=Flash(t,ShotAt,.01f,.09f);if(shot>.01f)l.Add(Anchor(NativeEntranceAnchor.RightMuzzle,t),NativeEntranceFx.Hex(0xfff0d0,16*shot),6);
            float lens=NativeEntranceMath.Bump(t,LensAt,2.0f);if(lens>.01f)l.Add(AimLens+AimDir*.5f+Up*.1f,Red*(.6f*lens),2.2f);
            float card=NativeEntranceMath.Ramp(t,CardAt-.4f,CardAt+.4f);if(card>.01f)l.Add(chest+SDir(-1.5f,.5f,1.6f),Ice*(1.6f*card),6);
        }

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            View(t,out var eye,out var fwd);float floor=Floor;Rifle(t,out var grip,out var bore,out var rup,out var muzzle);
            var scope=grip+bore*ScopeAt+rup*ScopeY;
            // the rain round the eye: short falling streaks (each under a metre), slowed with the clock; splashes on the roof
            float slow=t>3.15f&&t<HitAt+.1f?.35f:1;int drops=q<.5f?70:150;
            for(int i=0;i<drops;i++)
            {
                float speed=8.5f*slow,fall=6f,phase=(NativeEntranceMath.Rand(8300,i)+t*speed/fall)%1f;
                var p=eye+fwd*(1.2f+7*NativeEntranceMath.Rand(8301,i))+new Vector3(NativeEntranceMath.Signed(8302,i)*4.5f,fall*(.5f-phase),NativeEntranceMath.Signed(8303,i)*4.5f);
                // the shot throws the hanging rain back from the muzzle
                var fromMuzzle=p-muzzle;float d2=fromMuzzle.LengthSquared();float push=NativeEntranceMath.Pulse(t,ShotAt,.01f,.25f)*(float)Math.Exp(-d2*.4f);if(push>.01f)p+=Vector3.Normalize(fromMuzzle)*(1.6f*push);
                fx.Streak(p,new Vector3(.4f,-speed,.1f)*.07f,Rain*(.32f+.1f*NativeEntranceMath.Rand(8304,i)),.006f,.05f,1,.6f,0,8300+i);
            }
            int splashes=q<.5f?10:24;
            for(int i=0;i<splashes;i++){float ph=(NativeEntranceMath.Rand(8400,i)+t*2.2f*slow)%1f;if(ph>.25f)continue;var p=L(NativeEntranceMath.Signed(8401,i)*4.5f,floor+.01f,-1-6*NativeEntranceMath.Rand(8402,i));float u=ph/.25f;fx.Streak(p+new Vector3(0,.06f*u,0),new Vector3(0,.5f,0),Rain*(.5f*(1-u)),.004f,.03f,.12f,.5f,u,8400+i);}
            // the beacon's halo; the bulb's
            float blink=NativeEntranceMath.Bump(t%1.2f,0,.45f);fx.Glow(Beacon,Red*(1.4f*blink),0,.5f,1,0,8500);fx.Glow(Bulb,NativeEntranceFx.Hex(0xffd8a0,1.2f),0,.35f,1,0,8501);
            // the drone: its eye's halo, the searchlight's cone of mist, swinging to the glint
            if(t<HitAt)
            {
                var d=DroneAt(t,out var dq);var face=Vector3.Transform(Vector3.UnitZ,dq);fx.Glow(d+face*.62f,Red*2.2f,0,.9f,1,0,8510);fx.Glow(d+face*.62f,Red*.8f,0,2.6f,0,0,8511);
                var beam=Vector3.Normalize(face+new Vector3(0,-.25f,0));for(int i=0;i<(q<.5f?4:8);i++){float u=(i+1)/9f;fx.Glow(d+beam*(14*u),NativeEntranceFx.Hex(0xcfe4ff,.22f*(1-u)),0,.8f+3*u,0,0,8520+i);}
            }
            // the scope's lens: the eye's red in the glass, rain beading on it (close shot), and the glint seen from afar
            float lens=NativeEntranceMath.Bump(t,LensAt-.05f,2.0f);
            if(lens>.01f){fx.Glow(scope+bore*.008f,Red*(1.1f*lens),0,.03f,1,0,8600);for(int i=0;i<(q<.5f?3:6);i++){var p=scope+bore*.012f+Right*(NativeEntranceMath.Signed(8610,i)*.026f)+Up*(NativeEntranceMath.Signed(8611,i)*.026f-.02f*((t*.6f+i*.3f)%1f));fx.Glow(p,Ice*(.6f*lens),0,.006f,0,0,8610+i);}}
            float glint=Flash(t,GlintAt,.03f,.28f);if(glint>.02f)Star(fx,eye,fwd,scope+bore*.012f,NativeEntranceFx.Hex(0xe8f6ff,3.4f*glint),(t>2.0f&&t<BreathFrom?.75f:.25f)*glint+.05f,.6f,8650);
            // the breath: a faint plume from the drawn visor's mouth (just under the eyes, in front of the glass) as the hold breaks after the shot
            {var head=Anchor(NativeEntranceAnchor.Eyes,t);float b=NativeEntranceMath.Bump(t,ShotAt+.25f,ShotAt+1.1f);if(b>.02f)for(int i=0;i<3;i++){float u=(t*.8f+i/3f)%1f;fx.Smoke(head+new Vector3(0,-.07f+.06f*u,0)+bore*(.12f+.2f*u),new Vector3(.75f,.8f,.88f),.12f*b*(1-u),.06f+.12f*u,u,8700+i,floor);}}
            // the shot: the flash, the brake's side jets, the round and its vapour line across the avenue, the casing's steam
            if(t>=ShotAt-.01f&&t<ShotAt+.12f)Muzzle(fx,t,ShotAt,muzzle,bore,.24f,NativeEntranceFx.Hex(0xffd8a0),8800,q,.1f);
            for(int s=-1;s<=1;s+=2)NativeEntranceFx.Jet(fx,t,ShotAt,ShotAt+.08f,muzzle,Right*s+bore*.15f,90,2.4f,1.2f,.22f,NativeEntranceFx.Hex(0xa8b0c0),.4f,8820+s*10,q,floor,.15f,.2f);
            if(t>=ShotAt&&t<HitAt+.06f)
            {
                var m0=AimWrist+AimDir*MuzzleAt;var target=DroneAt(HitAt,out var hq)+Vector3.Transform(Vector3.UnitZ,hq)*.6f;var d=target-m0;float len=d.Length();var dir=d/len;
                float u=NativeEntranceMath.Clamp01((t-ShotAt)/(HitAt-ShotAt));var head=m0+d*u;
                fx.Glow(head,NativeEntranceFx.Hex(0xe8f4ff,3),0,.07f,1,0,8850);
                Line(fx,eye,fwd,head,head-dir*Math.Min(3.2f,len*u),NativeEntranceFx.Hex(0xcfe8ff,1.6f),.02f,.9f,0,8851);
                // the vapour it leaves: a thin line lingering behind (laid on the camera plane), fading
                Line(fx,eye,fwd,head-dir*Math.Min(3.2f,len*u),m0+dir*1.2f,NativeEntranceFx.Hex(0x9ab8e0,.22f),.012f,.4f,0,8852);
                // the rain it punches through: drops scattered off its line
                for(int i=0;i<(q<.5f?6:12);i++){float v=((i+.5f)/12f);float along=len*u-6*v;if(along<1)continue;var p=m0+dir*along+NativeEntranceMath.Direction(8860,i)*.25f;fx.Glow(p,Rain*(.5f*(1-v)),0,.02f,0,0,8860+i);}
            }
            // the strike: the eye bursting red and white, sparks, a flash; then the fall: fire and black smoke trailing it
            var hit=DroneAt(HitAt,out var hq2)+Vector3.Transform(Vector3.UnitZ,hq2)*.6f;
            NativeEntranceFx.Sparks(fx,t,HitAt,hit,-AimDir,1.2f,q<.5f?16:40,9,.9f,9.8f,1,NativeEntranceFx.Hex(0xfff0d0,3.4f),Red*2.4f,.03f,8900,q,floor-60);
            float f=Flash(t,HitAt,.01f,.16f);if(f>.02f){fx.Glow(hit,Vector3.One*(4*f),0,1.4f,1,0,8950);fx.Glow(hit,Red*(2.6f*f),0,4,0,0,8951);Star(fx,eye,fwd,hit,NativeEntranceFx.Hex(0xffe0d0,2.4f*f),2.2f*f+.4f,.3f,8952);}
            if(t>HitAt&&t<FallTo+.4f)
            {
                var fires=fx as INativeEntranceFire;int n=q<.5f?6:14;
                for(int i=0;i<n;i++)
                {
                    float back=i*.06f,tb=t-back;if(tb<HitAt)continue;var p=DroneAt(tb,out _);float u=back/(n*.06f);
                    if(fires!=null&&i<n/2)fires.Fire(p+new Vector3(0,.2f,0),new Vector3(1,.45f,.12f),.9f*(1-u),.9f+.8f*u,u,8960+i,floor-62);
                    fx.Smoke(p+new Vector3(0,.4f+1.2f*u,0),new Vector3(.08f,.08f,.09f),.7f*(1-u),1.0f+2.2f*u,u,8980+i,floor-62);
                }
            }
            // the crash far below: an orange flash between the towers
            float crash=Flash(t,FallTo,.02f,.4f);if(crash>.02f){var c=DroneAt(FallTo,out _);c.Y=-50;fx.Glow(c,NativeEntranceFx.Hex(0xff8a40,3*crash),0,9,1,0,9000);}
            // the casing's steam on the wet coping
            if(t>CaseLands){Case(t,out var cp,out _);for(int i=0;i<3;i++){float u=((t-CaseLands)*.9f+i/3f)%1f;fx.Smoke(cp+new Vector3(0,.02f+.25f*u,0),new Vector3(.8f,.82f,.86f),.15f*(1-u)*(1-NativeEntranceMath.Ramp(t,CaseLands+1.2f,CaseLands+2.2f)),.03f+.08f*u,u,9010+i,floor);}}
            // under the name: cold motes in the rain light
            if(t>CardAt-.5f)Motes(fx,t,q,L(0,1.4f,.5f),new Vector3(2.4f,1.2f,1.5f),Ice*.14f,.025f,18,9100,.2f);
        }

        // ---- sound: its own cues (no bed) ----
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(.02f,"intro2.onebreath.rain","field.storm","water",.4f,.8f),
            new NativeIntroCue(Lightning+.35f,"intro2.onebreath.thunder","structure.rumble","explosion",.55f,.55f),
            new NativeIntroCue(1.6f,"intro2.onebreath.drone","turret.servo","electric-hook",.35f),
            new NativeIntroCue(GlintAt,"intro2.onebreath.glint","ui.install.catch","ui",.4f,1.4f),
            new NativeIntroCue(GlintAt+.25f,"intro2.onebreath.lock","turret.lock","ui",.5f),
            new NativeIntroCue(BreathFrom+.02f,"intro2.onebreath.inhale","pve.ironworks.stoker.breath",null,.5f),
            new NativeIntroCue(BreathFrom+.4f,"intro2.onebreath.heart","structure.thud","hit",.5f,.6f),
            new NativeIntroCue(BreathFrom+.62f,"intro2.onebreath.heart","structure.thud","hit",.45f,.6f),
            new NativeIntroCue(ShotAt,"intro2.onebreath.shot","gun.rifle.shot","rifle",1.0f),
            new NativeIntroCue(ShotAt+.03f,"intro2.onebreath.crack","gun.flyby","dash",.6f,.6f),
            new NativeIntroCue(HitAt,"intro2.onebreath.hit","impact.glass","explosion",.8f),
            new NativeIntroCue(HitAt+.05f,"intro2.onebreath.fall","turret.break","explosion",.6f),
            new NativeIntroCue(CaseLands,"intro2.onebreath.case","gun.reload.end","reload-end",.45f,1.5f),
            new NativeIntroCue(CaseLands+.1f,"intro2.onebreath.hiss","chip.bomb.fuse-hiss",null,.3f),
            new NativeIntroCue(FallTo,"intro2.onebreath.crash","blast.debris","explosion",.45f,.7f),
            new NativeIntroCue(RiseFrom+.2f,"intro2.onebreath.rise","gun.reload.start","reload-start",.35f,.8f),
        };
        public override NativeIntroCue[] Cues=>cues;
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.Y=.85f;card.Style=NativeIntroCardStyle.Type;card.Ink=NativeEntranceFx.Hex(0xe8f4ff);card.Glow=NativeEntranceFx.Hex(0x7fc8ff,1.1f);
        }
    }
}
