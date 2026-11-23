using System;

namespace ChipArena.UnityConversion
{
    // Aimed recoil in the first-person view (owner 2026-11-23: "The pistol ADS still looks a bit small and simulates no recoil; Is this
    // something we can fix? (I dont know if recoil is in the pack or not as well)"; docs/FPS-TACTICAL.md "Aimed recoil").
    // Aimed, the tactical view laid the gun on the sights and its procedural spring barely moved it (the pistol's fire clip, played
    // before the aim, was wiped by it; the machine gun's post lifted 0.42 degrees; the rifle's scope never moved). The pack does carry
    // recoil: per gun a procedural recoil asset (the muzzle's rise, a yaw and a roll about a pivot, a kick back, each on its own curve,
    // with aiming multipliers and, for automatic fire, smoothing) and a camera shake. Its values are read here as data (sampled every
    // 1/60 s from the pack's assets by the scratch tool kickdata.py; no pack code, in git or at run time) and played by this code:
    // - each shot turns the gun about its pivot and kicks it back toward the eye, then it is back at rest before the next shot is
    //   allowed (a semi-automatic gun's shapes fit within Fit of its interval, quickened where the pack's run longer);
    // - the machine gun's rounds play the pack's automatic shapes, smoothed from the second round of a burst, plus a capped climb that
    //   settles once the next round does not come;
    // - the camera shakes a little (its share of the pack's, camera shake on only; the aim ray is taken before it);
    // - the pistol's slide cycles on the pack's own slide timing; the rifle's scope kicks (its reticle back on the aim ray by the bolt).
    // Presentation only: the rules' aim, spread, recoil, fire rate, hit volumes and the wire are untouched; this reads the pilot's own
    // LastShot and its interval (ShootReady) and writes only its own state.
    public sealed class NativeAimKick
    {
        public WeaponId Weapon;
        // Per shot, the pack's targets with its aiming multipliers applied (degrees): the muzzle's rise, a yaw from one of two ranges (by
        // the shot's coin), a roll from one of two (Alternate: never the last shot's side); metres: the kick back toward the eye.
        public float Rise0,Rise1,YawL0,YawL1,YawR0,YawR1,RollL0,RollL1,RollR0,RollR1,Back0,Back1;public bool Alternate=true;
        // Our readability gains on the turn and the kick back (1: the pack's own).
        public float Turn=1,Move=1;
        // Where the gun turns: from the pack's gun bone, in the gun's frame (metres; the pack's aiming pivot offset).
        public System.Numerics.Vector3 Pivot;
        // The shapes (each axis's share of its target, 60 samples a second from the shot): rise, yaw, roll and kick back; Rate: the
        // pack's play rate. Auto: the pack's automatic-fire shapes, cut at the next round, smoothed from the second round of a burst at
        // Smooth* (per second) and scaled by Extra* (the pack's auto smoothing and extra scale).
        public float[] RiseShape,YawShape,RollShape,BackShape;public float Rate=1;public bool Auto;
        public float SmoothRise,SmoothYaw,SmoothRoll,SmoothBack,ExtraRise=1,ExtraYaw=1,ExtraRoll=1,ExtraBack=1;
        // The camera (degrees: its rise, yaw and roll ranges) on the pack's shared shake shape at CameraRate, CameraShare of it.
        public float CamRise0,CamRise1,CamYaw0,CamYaw1,CamRoll0,CamRoll1,CameraRate=3,CameraShare=.5f;
        // Sustained fire (the machine gun): each round of a burst after the first raises the climb by Climb degrees, to ClimbMax (and
        // ClimbUp metres up per degree); it follows at ClimbRate while the rounds keep coming, then settles at ClimbOmega.
        public float Climb,ClimbMax,ClimbUp,ClimbRate=25,ClimbOmega=40;
        // The slide (the pistol): its travel's share from the shot (the pack's own slide on the Viper, 60 samples a second).
        public float[] SlideShape;
        // A semi-automatic gun's shot is back at rest within this share of its interval.
        public const float Fit=.9f;
        public float Span=>RiseShape!=null?(RiseShape.Length-1)/60f:0;

        // The WK-11 Viper's recoil (pitch 9-12 up, yaw -1.8..-1 or 1.2..2, roll 2-3 either side, kick 14-16 mm; aiming x1) and the
        // pistol's shake (camera pitch 0.7-1 up, yaw 0.2-0.3, roll 1.5-1.9 at 3.4x).
        public static readonly NativeAimKick Pistol=new NativeAimKick{Weapon=WeaponId.Pistol,Rise0=9,Rise1=12,YawL0=-1.8f,YawL1=-1,YawR0=1.2f,YawR1=2,RollL0=-3,RollL1=-2,RollR0=2,RollR1=3,
            Back0=.014f,Back1=.016f,RiseShape=NativeAimKickData.ViperTurnX,YawShape=NativeAimKickData.ViperTurnY,RollShape=NativeAimKickData.ViperTurnZ,BackShape=NativeAimKickData.ViperMoveZ,Rate=1.1f,
            CamRise0=.7f,CamRise1=1,CamYaw0=.2f,CamYaw1=.3f,CamRoll0=1.5f,CamRoll1=1.9f,CameraRate=3.4f,CameraShare=.5f,SlideShape=NativeAimKickData.ViperSlide};
        // The TR15's (pitch 0.7-0.9, yaw -0.7..-0.3 or 0.3..0.6, roll -1.2..-1 or 1..1.5, kick 10-11 mm; aiming x0.8 pitch and roll, x0.5
        // yaw, x0.8 kick; the pivot 4.5 cm under and 3 cm ahead of the gun bone; auto smoothing 30/15/25 and extra 2.5/1/1.2) and the
        // light rifle shake (pitch 0.32-0.42, yaw 0.2-0.3, roll -1.3..-1.5 at 3x). The turn x1.4 and the climb are ours: the pack's
        // aimed TR15 lifts the post about as little as the old spring did.
        public static readonly NativeAimKick MachineGun=new NativeAimKick{Weapon=WeaponId.MachineGun,Rise0=.56f,Rise1=.72f,YawL0=-.35f,YawL1=-.15f,YawR0=.15f,YawR1=.3f,RollL0=-.96f,RollL1=-.8f,RollR0=.8f,RollR1=1.2f,
            Back0=.008f,Back1=.0088f,Turn=1.4f,Pivot=new System.Numerics.Vector3(0,-.045f,.03f),RiseShape=NativeAimKickData.TR15TurnX,YawShape=NativeAimKickData.TR15TurnY,RollShape=NativeAimKickData.TR15TurnZ,BackShape=NativeAimKickData.TR15MoveZ,
            Auto=true,SmoothRise=30,SmoothYaw=15,SmoothRoll=25,SmoothBack=30,ExtraRise=2.5f,ExtraYaw=1,ExtraRoll=1.2f,ExtraBack=1,
            CamRise0=.32f,CamRise1=.42f,CamYaw0=.2f,CamYaw1=.3f,CamRoll0=-1.5f,CamRoll1=-1.3f,CameraRate=3,CameraShare=.4f,Climb=.07f,ClimbMax=.7f,ClimbUp=.002f};
        // The Oryx SRM-12's (pitch 0.9-1.2, yaw -0.3..0.5 or 0.3..0.4, roll 0.5-1 either side, kick 1-1.5 mm; aiming x2 pitch and yaw, x3
        // roll, x10 kick) and the heavy rifle shake (pitch 0.7-0.8, yaw -0.2..-0.3, roll 1.8-2.7 at 2.9x). Aimed in first person the rifle
        // looks through its scope, whose overlay takes this kick (ScopeKick).
        public static readonly NativeAimKick Rifle=new NativeAimKick{Weapon=WeaponId.Rifle,Rise0=1.8f,Rise1=2.4f,YawL0=-.6f,YawL1=1,YawR0=.6f,YawR1=.8f,RollL0=-3,RollL1=-1.5f,RollR0=1.5f,RollR1=3,
            Back0=.01f,Back1=.015f,RiseShape=NativeAimKickData.SRM12TurnX,YawShape=NativeAimKickData.SRM12TurnY,RollShape=NativeAimKickData.SRM12TurnZ,BackShape=NativeAimKickData.SRM12MoveZ,
            CamRise0=.7f,CamRise1=.8f,CamYaw0=-.3f,CamYaw1=-.2f,CamRoll0=1.8f,CamRoll1=2.7f,CameraRate=2.9f,CameraShare=.5f};
        public static NativeAimKick For(WeaponId w)=>w==WeaponId.Pistol?Pistol:w==WeaponId.MachineGun?MachineGun:w==WeaponId.Rifle?Rifle:null;

        /// <summary>A shape's value `seconds` into it (linear between its samples; 0 before and after), faded out over its last 15 %.</summary>
        public static float Shape(float[] s,float seconds)
        {
            if(s==null||s.Length<2||!(seconds>=0))return 0;float x=seconds*60;int k=(int)Math.Floor(x);if(k>=s.Length-1)return 0;
            float span=(s.Length-1)/60f,fade=MathUtil.Smooth((span-seconds)/(.15f*span));return (s[k]+(s[k+1]-s[k])*(x-k))*fade;
        }
        /// <summary>The shapes' clock for a shot `age` seconds old: a semi-automatic gun's quickened to end within Fit of `interval`; an
        /// automatic gun's at the pack's rate.</summary>
        public float ShapeTime(float age,float interval)=>age*(Auto||!(interval>0)?Rate:Math.Max(Rate,Span/(Fit*interval)));
        /// <summary>An automatic gun's round: whole until three quarters of its interval, gone by the next round.</summary>
        public static float Cut(float age,float interval)=>interval>0?MathUtil.Smooth((interval-age)/(.25f*interval)):1;

        /// <summary>One shot's targets, from its time (a replay kicks the same): rise, yaw, roll (Alternate: the side flipped when it
        /// matches `lastRoll`'s), back, and the camera's rise, yaw and roll.</summary>
        public void Targets(float shot,float lastRoll,out float rise,out float yaw,out float roll,out float back,out float camRise,out float camYaw,out float camRoll)
        {
            uint h=(uint)(int)Math.Round(shot*1000)*2654435761u;
            float R(uint salt){uint x=h^(salt*0x9E3779B9u);x^=x>>15;x*=0x2c1b3c6du;x^=x>>12;x*=0x297a2d39u;x^=x>>15;return (x&0xffffff)/16777216f;}
            float L(float a,float b,float t)=>a+(b-a)*t;
            rise=L(Rise0,Rise1,R(1));yaw=R(2)>=.5f?L(YawR0,YawR1,R(3)):L(YawL0,YawL1,R(3));
            roll=R(5)>=.5f?L(RollR0,RollR1,R(4)):L(RollL0,RollL1,R(4));if(Alternate&&roll*lastRoll>0)roll=-roll;
            back=L(Back0,Back1,R(6));camRise=L(CamRise0,CamRise1,R(7));camYaw=L(CamYaw0,CamYaw1,R(8));camRoll=L(CamRoll0,CamRoll1,R(9));
        }

        /// <summary>The scope's kick for the rifle's shot `age` seconds old (the latest shot, at `shot`): its reticle's rise and yaw and
        /// the ring's roll (degrees) and its kick back toward the eye (metres), back at rest within Fit of `interval`. Stateless: the
        /// scope overlay reads it as it draws.</summary>
        public void ScopeKick(float shot,float age,float interval,out float rise,out float yaw,out float roll,out float back)
        {
            rise=yaw=roll=back=0;if(!(age>=0)||!MathUtil.Finite(shot))return;float u=ShapeTime(age,interval);if(u>=Span)return;
            Targets(shot,0,out float tr,out float ty,out float tz,out float tb,out _,out _,out _);
            rise=tr*Turn*Shape(RiseShape,u);yaw=ty*Turn*Shape(YawShape,u);roll=tz*Turn*Shape(RollShape,u);back=tb*Move*Shape(BackShape,u);
        }
    }

    /// <summary>The view's aimed recoil between frames: the latest shot, its targets, the burst, the automatic smoothing, the climb and
    /// the camera. Step once a frame; read the outputs (the gun's turn in degrees, its kick back and lift in metres, the camera's turn in
    /// degrees, the slide's travel share).</summary>
    public sealed class NativeAimKickState
    {
        public float Rise,Yaw,Roll,Back,Up,CamRise,CamYaw,CamRoll,Slide,Climb;
        private float shot=float.NaN,tRise,tYaw,tRoll,tBack,tCamRise,tCamYaw,tCamRoll,lastRoll,climbV;private int burst;private bool live;
        /// <summary>A fresh view: the shot already fired (`lastShot`) does not kick.</summary>
        public void Reset(float lastShot=float.NaN)
        {
            Rise=Yaw=Roll=Back=Up=CamRise=CamYaw=CamRoll=Slide=Climb=climbV=0;shot=lastShot;burst=0;live=false;
        }
        private static float Follow(float v,float target,float rate,float dt)=>rate>0?v+(target-v)*(1-(float)Math.Exp(-rate*dt)):target;
        public void Step(NativeAimKick k,float time,float lastShot,float interval,float dt)
        {
            if(k==null){Reset(lastShot);return;}
            dt=MathUtil.Clamp(MathUtil.Finite(dt)?dt:0,0,.05f);
            // A new shot, once the view's clock reaches it.
            if(MathUtil.Finite(lastShot)&&lastShot<=time+1e-3f&&(float.IsNaN(shot)||lastShot>shot+1e-4f))
            {
                bool chain=live&&!float.IsNaN(shot)&&lastShot-shot<=interval*1.5f;burst=chain?burst+1:1;shot=lastShot;live=true;
                k.Targets(shot,lastRoll,out tRise,out tYaw,out tRoll,out tBack,out tCamRise,out tCamYaw,out tCamRoll);lastRoll=tRoll;
            }
            float age=live?time-shot:-1;
            float rise=0,yaw=0,roll=0,back=0,cr=0,cy=0,cz=0;
            if(age>=0)
            {
                float u=k.ShapeTime(age,interval),cut=k.Auto?NativeAimKick.Cut(age,interval):1;
                rise=tRise*k.Turn*NativeAimKick.Shape(k.RiseShape,u)*cut;yaw=tYaw*k.Turn*NativeAimKick.Shape(k.YawShape,u)*cut;
                roll=tRoll*k.Turn*NativeAimKick.Shape(k.RollShape,u)*cut;back=tBack*k.Move*NativeAimKick.Shape(k.BackShape,u)*cut;
                // The camera's shake: the latest shot's on the shared shape at its rate (a semi-automatic gun's within its interval).
                float cu=age*(k.Auto||!(interval>0)?k.CameraRate:Math.Max(k.CameraRate,1/(NativeAimKick.Fit*interval)));
                cr=tCamRise*NativeAimKick.Shape(NativeAimKickData.ShakeX,cu);cy=tCamYaw*NativeAimKick.Shape(NativeAimKickData.ShakeY,cu);cz=tCamRoll*NativeAimKick.Shape(NativeAimKickData.ShakeZ,cu);
            }
            if(k.Auto)
            {
                // From the second round of a burst the pack smooths its automatic shapes (and scales them); the first round is crisp.
                bool smooth=burst>1;
                Rise=Follow(Rise,rise*(smooth?k.ExtraRise:1),smooth?k.SmoothRise:60,dt);Yaw=Follow(Yaw,yaw*(smooth?k.ExtraYaw:1),smooth?k.SmoothYaw:60,dt);
                Roll=Follow(Roll,roll*(smooth?k.ExtraRoll:1),smooth?k.SmoothRoll:60,dt);Back=Follow(Back,back*(smooth?k.ExtraBack:1),smooth?k.SmoothBack:60,dt);
                // The camera follows its restarted shake smoothly (the pack's smooth speed).
                CamRise=Follow(CamRise,cr,40,dt);CamYaw=Follow(CamYaw,cy,40,dt);CamRoll=Follow(CamRoll,cz,40,dt);
            }
            else{Rise=rise;Yaw=yaw;Roll=roll;Back=back;CamRise=cr;CamYaw=cy;CamRoll=cz;}
            CamRise*=k.CameraShare;CamYaw*=k.CameraShare;CamRoll*=k.CameraShare;
            // The climb: each round of a burst after the first adds to it (capped); it follows while the rounds come (the next one due
            // within a quarter interval and a frame), then settles critically damped.
            if(k.Climb>0)
            {
                bool firing=age>=0&&age<interval*1.25f+.017f;
                if(firing){float target=Math.Min(k.ClimbMax,Math.Max(0,burst-1)*k.Climb);Climb=Follow(Climb,target,k.ClimbRate,dt);climbV=0;}
                else
                {
                    int n=Math.Max(1,(int)Math.Ceiling(dt*240));float h=dt/n,w=k.ClimbOmega;
                    for(int j=0;j<n;j++){climbV+=(-Climb*w*w-2*w*climbV)*h;Climb+=climbV*h;}
                    if(Math.Abs(Climb)<1e-5f&&Math.Abs(climbV)<1e-4f)Climb=climbV=0;
                }
            }
            else Climb=climbV=0;
            Rise+=Climb;Up=Climb*k.ClimbUp;
            // The slide: the pack's own cycle from the shot, at its own pace (any first-person shot: the pistol's mechanism).
            Slide=k.SlideShape!=null&&age>=0?NativeAimKick.Shape(k.SlideShape,age):0;
        }
    }

    /// <summary>The pack's recoil and shake shapes (each axis's share of its target, 60 samples a second from the shot), sampled from its
    /// assets as data by the scratch tool kickdata.py: the Viper's semi-automatic turn (pitch, yaw, roll) and kick back, the TR15's
    /// automatic ones (its first 0.2 s), the SRM-12's semi-automatic ones, the camera shake's shared shape (one second; the pistol's and
    /// both rifles' shakes share it) and the Viper's slide (its bolt bone's travel, 46.5 mm at its peak).</summary>
    internal static class NativeAimKickData
    {
        internal static readonly float[] ViperSlide={0,1,.742f,.26f,0};
        internal static readonly float[] ViperTurnX=new float[]{0,.419f,1,.956f,.566f,.079f,-.3f,-.384f,-.335f,-.252f,-.147f,-.034f,.077f,.172f,.239f,.262f,.258f,.245f,.227f,.204f,.178f,.15f,.124f,.099f,.077f,.058f,.042f,.029f,.018f,.01f,.005f,.001f,0,0};
        internal static readonly float[] ViperTurnY=new float[]{0,.113f,.381f,.692f,.932f,1,.978f,.921f,.833f,.722f,.593f,.453f,.306f,.158f,.015f,-.119f,-.238f,-.336f,-.408f,-.45f,-.454f,-.408f,-.323f,-.217f,-.107f,-.008f,.061f,.085f,.082f,.073f,.06f,.046f,.031f,0};
        internal static readonly float[] ViperTurnZ=new float[]{0,1,.987f,.941f,.853f,.716f,.531f,.307f,.099f,-.037f,-.103f,-.115f,-.101f,-.076f,-.044f,-.011f,.017f,.034f,.036f,.036f,.035f,.034f,.033f,.031f,.029f,.027f,.025f,.022f,.019f,.017f,.014f,.012f,.009f,0};
        internal static readonly float[] ViperMoveZ=new float[]{0,.43f,1,.991f,.646f,.182f,-.199f,-.301f,-.27f,-.199f,-.102f,.005f,.109f,.195f,.252f,.265f,.249f,.228f,.202f,.175f,.147f,.121f,.098f,.079f,.063f,.05f,.038f,.027f,.017f,.01f,.004f,.001f,0,0};
        internal static readonly float[] TR15TurnX=new float[]{0,.348f,.891f,.934f,.521f,.06f,-.234f,-.403f,-.4f,-.366f,-.311f,-.241f,0};
        internal static readonly float[] TR15TurnY=new float[]{0,.581f,.994f,.876f,.658f,.393f,.132f,-.07f,-.161f,-.171f,-.173f,-.167f,0};
        internal static readonly float[] TR15TurnZ=new float[]{0,.304f,.823f,.964f,.475f,.051f,-.228f,-.306f,-.259f,-.167f,-.052f,.067f,0};
        internal static readonly float[] TR15MoveZ=new float[]{0,.99f,.805f,.489f,.17f,-.031f,-.227f,-.315f,-.304f,-.28f,-.248f,-.209f,0};
        internal static readonly float[] SRM12TurnX=new float[]{0,.175f,.426f,.687f,.898f,.99f,1,.982f,.941f,.896f,.854f,.81f,.762f,.712f,.661f,.608f,.554f,.5f,.445f,.391f,.337f,.284f,.233f,.183f,.136f,.091f,.049f,.011f,-.023f,-.053f,-.079f,-.1f,-.115f,-.124f,-.127f,-.108f,-.061f,.004f,.074f,.137f,.182f,.209f,.227f,.238f,.245f,.25f,.254f,.251f,.218f,.15f,.067f,-.021f,-.101f,-.162f,-.192f,-.199f,-.197f,-.19f,-.176f,-.159f,-.138f,-.115f,-.092f,-.069f,-.047f,-.028f,-.013f,-.004f,0,0};
        internal static readonly float[] SRM12TurnY=new float[]{0,.002f,.067f,.084f,.23f,.345f,.424f,.504f,.601f,.654f,.697f,.7f,.686f,.659f,.623f,.568f,.516f,.443f,.375f,.29f,.209f,.117f,.028f,-.067f,-.16f,-.255f,-.343f,-.424f,-.498f,-.545f,-.591f,-.571f,-.552f,-.368f,-.184f,.08f,.34f,.565f,.783f,.862f,.937f,.958f,.979f,.985f,.994f,.996f,1,.997f,.992f,.885f,.803f,.627f,.461f,.278f,.092f,-.044f,-.179f,-.21f,-.24f,-.221f,-.202f,-.169f,-.136f,-.101f,-.066f,-.04f,-.014f,-.007f,0,0};
        internal static readonly float[] SRM12TurnZ=new float[]{0,.147f,.475f,.816f,1,.786f,.251f,-.297f,-.548f,-.475f,-.303f,-.099f,.071f,.184f,.235f,.225f,.199f,.163f,.118f,.079f,.052f,.028f,.007f,-.014f,-.036f,-.133f,-.315f,-.491f,-.57f,-.565f,-.546f,-.451f,-.366f,-.373f,-.399f,-.437f,-.488f,-.56f,-.644f,-.713f,-.742f,-.745f,-.745f,-.603f,-.296f,.023f,.199f,.24f,.255f,.1f,-.122f,-.223f,-.262f,-.221f,-.142f,-.053f,.049f,.135f,.175f,.185f,.188f,.175f,.152f,.122f,.09f,.058f,.03f,.011f,.004f,0};
        internal static readonly float[] SRM12MoveZ=new float[]{0,.017f,.039f,.066f,.567f,.72f,.795f,.871f,.936f,.983f,1,.995f,.98f,.958f,.927f,.882f,.818f,.741f,.658f,.574f,.495f,.42f,.342f,.263f,.186f,.111f,.041f,-.024f,-.086f,-.145f,-.203f,-.261f,-.321f,-.384f,-.479f,-.388f,-.276f,-.159f,-.047f,.049f,.119f,.166f,.203f,.231f,.25f,.269f,.294f,.349f,.389f,.362f,.295f,.189f,.061f,-.05f,-.106f,-.118f,-.118f,-.107f,-.095f,-.081f,-.066f,-.051f,-.037f,-.023f,-.01f,.001f,.009f,.015f,.017f,0};
        internal static readonly float[] ShakeX=new float[]{0,.053f,.135f,.238f,.356f,.482f,.608f,.728f,.834f,.92f,.978f,1.002f,1.007f,1.005f,.996f,.979f,.957f,.928f,.894f,.856f,.813f,.766f,.716f,.664f,.609f,.552f,.493f,.434f,.375f,.316f,.258f,.2f,.145f,.091f,.041f,-.007f,-.051f,-.091f,-.126f,-.156f,-.18f,-.198f,-.209f,-.213f,-.212f,-.206f,-.197f,-.185f,-.171f,-.154f,-.136f,-.118f,-.099f,-.08f,-.062f,-.045f,-.03f,-.018f,-.008f,-.002f,0};
        internal static readonly float[] ShakeY=new float[]{0,.03f,.113f,.233f,.378f,.533f,.686f,.822f,.928f,.99f,.997f,.971f,.919f,.846f,.756f,.653f,.542f,.426f,.309f,.196f,.091f,-.003f,-.081f,-.139f,-.173f,-.18f,-.179f,-.178f,-.175f,-.172f,-.169f,-.165f,-.16f,-.155f,-.149f,-.143f,-.136f,-.13f,-.122f,-.115f,-.108f,-.1f,-.093f,-.085f,-.077f,-.07f,-.062f,-.055f,-.048f,-.041f,-.035f,-.029f,-.024f,-.018f,-.014f,-.01f,-.006f,-.004f,-.002f,0,0};
        internal static readonly float[] ShakeZ=new float[]{0,.092f,.316f,.592f,.843f,.988f,.99f,.941f,.855f,.741f,.605f,.454f,.294f,.133f,-.023f,-.168f,-.293f,-.393f,-.46f,-.488f,-.476f,-.435f,-.371f,-.29f,-.197f,-.098f,0,.093f,.173f,.237f,.277f,.288f,.279f,.258f,.228f,.189f,.145f,.097f,.047f,-.003f,-.05f,-.093f,-.13f,-.158f,-.175f,-.181f,-.178f,-.171f,-.161f,-.148f,-.133f,-.116f,-.098f,-.08f,-.063f,-.046f,-.031f,-.019f,-.009f,-.002f,0};
    }
}
