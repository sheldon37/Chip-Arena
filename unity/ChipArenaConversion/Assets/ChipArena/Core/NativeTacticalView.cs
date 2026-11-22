using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The first-person tactical view (owner 2026-11-21: "Utilize new FPS system / animations from tactical pack"; docs/FPS-TACTICAL.md).
    // The KINEMATION Tactical Shooter Pack's first-person clips, baked by the local tool into a data bank (ChipArenaViewMotion.bytes,
    // in the git-ignored LocalAssets like the other banks: never committed, carried to the builder in the LocalAssets zip), drive the
    // pilot model's own forearms and hands and the game's own weapon models in the first-person camera. No pack code runs: this file
    // reads the bank and solves the view in the camera frame (Unity axes: +x right, +y up, +z forward, metres), and
    // Runtime/NativeTacticalViewPresentation.cs puts the result on the view body. Without the bank the old view model draws.
    // Presentation only: aim, muzzle, shots, reload and fire timing, hit volumes and the wire are the game's. Every clip is retimed
    // to the game's rules (NativeTacticalTiming): a reload fills ReloadAt..ReloadUntil exactly, the rifle's bolt is home when the
    // next shot is allowed (ShootReady), aiming down the sights settles in the gun's Settle time; nothing the clips do moves a rule.
    public struct NativeTacticalFrame
    {
        public Vector3 Gun; public Quaternion GunTurn;
        public Vector3 Right; public Quaternion RightTurn;
        public Vector3 Left; public Quaternion LeftTurn;
        public Vector3 RightElbow, LeftElbow, RightShoulder, LeftShoulder;
        public Quaternion Camera;
        public Vector3 Add; public Quaternion AddTurn;
        public Quaternion RightPalm,LeftPalm;
    }

    /// <summary>One baked clip: per frame the pack's gun bone (its weapon offset applied: +z the bore, +y up), both hands, elbows
    /// and shoulders in the eye frame, each hand's 15 finger segments as directions in its palm frame (thumb, index, middle, ring,
    /// pinky; proximal to distal), camera_bone's local turn, ik_hand_gun_additive as the pack's procedural layer reads it and each
    /// hand's palm frame (forward wrist to middle_01, across index_01 to pinky_01).</summary>
    public sealed class NativeTacticalClip
    {
        public const int Stride=142,FingerFloats=45;
        public readonly string Key; public readonly int Frames; public readonly float Length,ActionStart,ActionEnd,Fps;
        private readonly float[] data;
        public NativeTacticalClip(string key,int frames,float length,float actionStart,float actionEnd,float fps,float[] data)
        {
            if(frames<1||data==null||data.Length!=frames*Stride||!(fps>0))throw new ArgumentException("bad clip "+key);
            Key=key;Frames=frames;Length=length;ActionStart=actionStart;ActionEnd=actionEnd;Fps=fps;this.data=data;
        }
        /// <summary>Where the clip's action is done (OnActionEnded), seconds; the clip's length without that event.</summary>
        public float ActionSeconds=>ActionEnd>0?ActionEnd*Length:Length;
        public void Sample(float time,out NativeTacticalFrame f,Vector3[] rightFingers=null,Vector3[] leftFingers=null)
        {
            float x=MathUtil.Clamp(MathUtil.Finite(time)?time:0,0,Length)*Fps;int k0=Math.Min(Frames-1,(int)Math.Floor(x)),k1=Math.Min(Frames-1,k0+1);float a=MathUtil.Clamp(x-k0,0,1);
            int o0=k0*Stride,o1=k1*Stride;
            f.Gun=V(o0,o1,0,a);f.GunTurn=Q(o0,o1,3,a);f.Right=V(o0,o1,7,a);f.RightTurn=Q(o0,o1,10,a);f.Left=V(o0,o1,14,a);f.LeftTurn=Q(o0,o1,17,a);
            f.RightElbow=V(o0,o1,21,a);f.LeftElbow=V(o0,o1,24,a);f.RightShoulder=V(o0,o1,27,a);f.LeftShoulder=V(o0,o1,30,a);
            f.Camera=Q(o0,o1,123,a);f.Add=V(o0,o1,127,a);f.AddTurn=Q(o0,o1,130,a);f.RightPalm=Q(o0,o1,134,a);f.LeftPalm=Q(o0,o1,138,a);
            if(rightFingers!=null)for(int i=0;i<15;i++)rightFingers[i]=Dir(o0,o1,33+i*3,a);
            if(leftFingers!=null)for(int i=0;i<15;i++)leftFingers[i]=Dir(o0,o1,78+i*3,a);
        }
        private Vector3 V(int o0,int o1,int at,float a)=>Vector3.Lerp(new Vector3(data[o0+at],data[o0+at+1],data[o0+at+2]),new Vector3(data[o1+at],data[o1+at+1],data[o1+at+2]),a);
        private Quaternion Q(int o0,int o1,int at,float a)=>NativeTacticalMath.Slerp(new Quaternion(data[o0+at],data[o0+at+1],data[o0+at+2],data[o0+at+3]),new Quaternion(data[o1+at],data[o1+at+1],data[o1+at+2],data[o1+at+3]),a);
        private Vector3 Dir(int o0,int o1,int at,float a){var d=V(o0,o1,at,a);float l=d.Length();return l>1e-6f?d/l:Vector3.UnitZ;}
    }

    /// <summary>The bank (ChipArenaViewMotion.bytes): "CAVM", version, fps, clip count, stride; per clip its UTF-8 key, frames,
    /// length, action start and end (shares of the length, -1 without), then frames x stride little-endian floats.</summary>
    public sealed class NativeTacticalBank
    {
        public const string Resource="ChipArenaViewMotion";public const int Version=2;
        public readonly float Fps;private readonly Dictionary<string,NativeTacticalClip> clips=new Dictionary<string,NativeTacticalClip>(StringComparer.Ordinal);
        public int Count=>clips.Count;
        private NativeTacticalBank(float fps){Fps=fps;}
        public NativeTacticalClip Clip(string key)=>key!=null&&clips.TryGetValue(key,out var c)?c:null;
        public static NativeTacticalBank Parse(byte[] b)
        {
            if(b==null||b.Length<20||b[0]!='C'||b[1]!='A'||b[2]!='V'||b[3]!='M')throw new InvalidOperationException("not a view-motion bank");
            int o=4;int version=I(b,ref o);float fps=F(b,ref o);int count=I(b,ref o),stride=I(b,ref o);
            if(version!=Version||stride!=NativeTacticalClip.Stride||count<0||count>4096||!(fps>0&&fps<1000))throw new InvalidOperationException("view-motion bank version "+version+" stride "+stride);
            var bank=new NativeTacticalBank(fps);
            for(int c=0;c<count;c++)
            {
                int len=I(b,ref o);if(len<1||len>256||o+len>b.Length)throw new InvalidOperationException("bad clip name");
                string key=Encoding.UTF8.GetString(b,o,len);o+=len;
                int frames=I(b,ref o);float length=F(b,ref o),a0=F(b,ref o),a1=F(b,ref o);
                if(frames<1||frames>100000||(long)o+(long)frames*stride*4>b.Length)throw new InvalidOperationException("bad clip "+key);
                var data=new float[frames*stride];for(int i=0;i<data.Length;i++){data[i]=BitConverter.ToSingle(b,o);o+=4;if(!MathUtil.Finite(data[i]))throw new InvalidOperationException("non-finite value in "+key);}
                bank.clips[key]=new NativeTacticalClip(key,frames,length,a0,a1,fps,data);
            }
            return bank;
        }
        private static int I(byte[] b,ref int o){if(o+4>b.Length)throw new InvalidOperationException("truncated bank");int v=BitConverter.ToInt32(b,o);o+=4;return v;}
        private static float F(byte[] b,ref int o){if(o+4>b.Length)throw new InvalidOperationException("truncated bank");float v=BitConverter.ToSingle(b,o);o+=4;return v;}
        /// <summary>Whether every clip the gun's set and the locomotion layer need is in the bank.</summary>
        public bool Covers(NativeTacticalGun g)
        {
            return g!=null&&Clip(g.Idle)!=null&&Clip(g.Reload)!=null&&Clip(g.Draw)!=null&&Clip(NativeTacticalGun.IdlePose)!=null&&Clip(NativeTacticalGun.IdleLoop)!=null
                &&Clip(NativeTacticalGun.WalkPose)!=null&&Clip(NativeTacticalGun.WalkLoop)!=null&&Clip(NativeTacticalGun.SprintLoop)!=null&&(g.Fire==null||Clip(g.Fire)!=null);
        }
    }

    /// <summary>One gun's clip set and its look (the pack's TacAnimSettings where it has one; its recoil, sway and aim tuned here).</summary>
    public sealed class NativeTacticalGun
    {
        public const string IdlePose="general.idlepose",IdleLoop="general.idleloop",WalkPose="general.walkpose",WalkLoop="general.walkloop",SprintLoop="general.sprintloop";
        public WeaponId Weapon;public string Family,Idle,Fire,Reload,Draw;
        // The fire clip is an action (the rifle's bolt, played to ShootReady) or an additive kick (the pistol); none: recoil alone.
        public bool FireAction;
        // Seconds the draw is played in (presentation only: no rule times a draw; a shot cuts it).
        public float DrawSeconds;
        // The pack's weaponOffset and sprintPoseOffset (root space); SprintCarry and SprintCarryDegrees add to its sprint (none:
        // the pack's own sprint loop already swings the gun across the body).
        public Vector3 WeaponOffset,SprintOffset;public Quaternion SprintTurn=Quaternion.Identity;
        public Vector3 SprintCarry;public Vector3 SprintCarryDegrees;
        // Eye relief: how far in front of the eye the rear sight sits fully aimed (m).
        public float EyeRelief;
        // The view model's lens (vertical degrees) at the hip and fully aimed (the pack's: 90, and its aimFov).
        public float HipFov=85,AimFov=74;
        // The game's model on the pack's gun: its rear sight on the pack gun's sight line (SightHeight above the gun bone: the
        // pack weapon's AimPoint), its right wrist level with the pack's along the bore, then ModelOffset (gun frame). The left
        // hand: on the right hand as the pack's (a pistol's support), or under the model's own handguard (its Support) as the
        // pack's under its own (PackSupport below the gun bone); then LeftOffset (the pack's leftHandOffset).
        public float SightHeight,PackSupport=-.03f;public bool LeftOnRight;
        // The Standard model's rear sight (weapon frame) where its bake entry names none, measured on the aimed view (the Army
        // pistol's slide and the light machine gun's rear sight; zero: the Standard gun's sight through the model's fit).
        public Vector3 ModelSight;
        public Vector3 ModelOffset,LeftOffset;
        // Procedural recoil per shot: metres back, degrees up, yaw and roll ranges; the springs' rate and damping; the pivot
        // (gun frame) the kick turns about.
        public float KickBack,KickPitch,KickYaw,KickRoll,KickOmega=34,KickZeta=.55f;public Vector3 KickPivot=new Vector3(0,-.04f,-.12f);
        // The reload's time warp: (share of the rule's reload, share of the clip) knots; the action's beats held, its off-screen
        // stretches quickened.
        public float[] ReloadWarp;
        // The pack's idle/walk intensities (standing, aiming).
        public float IdleIntensity=.8f,IdleAimIntensity=.25f,WalkIntensity=1,WalkAimIntensity=.4f;
        public static readonly NativeTacticalGun Pistol=new NativeTacticalGun{Weapon=WeaponId.Pistol,Family="WK-11 Viper",Idle="pistol.idle",Fire="pistol.fire",Reload="pistol.reload",Draw="pistol.quickdraw",
            DrawSeconds=.5f,WeaponOffset=new Vector3(.015f,.03f,0),SprintOffset=new Vector3(0,.03f,.02f),SprintTurn=new Quaternion(0,.17364818f,0,.9848077f),
            EyeRelief=.30f,SightHeight=.0346f,LeftOnRight=true,ModelSight=new Vector3(0,-.008f,-.04f),
            KickBack=.012f,KickPitch=2.2f,KickYaw=.6f,KickRoll=1.2f,
            ReloadWarp=new[]{0f,0f,.12f,.115f,.30f,.245f,.42f,.42f,.62f,.605f,.74f,.678f,.92f,.851f,1f,1f}};
        public static readonly NativeTacticalGun Rifle=new NativeTacticalGun{Weapon=WeaponId.Rifle,Family="Oryx SRM-12",Idle="rifle.idle",Fire="rifle.fire",FireAction=true,Reload="rifle.reload",Draw="rifle.draw",
            DrawSeconds=.9f,WeaponOffset=new Vector3(-.03f,.05f,.025f),EyeRelief=.09f,SightHeight=.0724f,
            KickBack=.02f,KickPitch=1.5f,KickYaw=.4f,KickRoll=.8f};
        public static readonly NativeTacticalGun MachineGun=new NativeTacticalGun{Weapon=WeaponId.MachineGun,Family="TR15",Idle="ar.idle",Reload="ar.reload",Draw="ar.draw",
            DrawSeconds=1f,WeaponOffset=new Vector3(-.043f,.007f,.02f),SprintOffset=new Vector3(0,-.035f,0),EyeRelief=.16f,SightHeight=-.007f,ModelSight=new Vector3(0,.09f,-.09f),ModelOffset=new Vector3(.015f,0,.15f),
            KickBack=.014f,KickPitch=1.1f,KickYaw=.5f,KickRoll=1.1f,KickOmega=40,KickZeta=.6f};
        public static NativeTacticalGun For(WeaponId w)=>w==WeaponId.Pistol?Pistol:w==WeaponId.Rifle?Rifle:w==WeaponId.MachineGun?MachineGun:null;
    }

    /// <summary>The clips' clocks on the game's rules. Pure functions: CoreTests hold every one of them to the rule it follows.</summary>
    public static class NativeTacticalTiming
    {
        public const float ReloadIn=.06f,ReloadOut=.10f,DrawOut=.08f,FireActionOut=.15f;
        /// <summary>A monotone piecewise-linear warp through (u, v) knots (u and v from 0 to 1); identity without knots.</summary>
        public static float Warp(float[] knots,float u)
        {
            u=MathUtil.Clamp(MathUtil.Finite(u)?u:0,0,1);if(knots==null||knots.Length<4)return u;
            for(int i=2;i<knots.Length;i+=2)if(u<=knots[i]){float u0=knots[i-2],u1=knots[i],v0=knots[i-1],v1=knots[i+1];return u1>u0?v0+(v1-v0)*(u-u0)/(u1-u0):v1;}
            return knots[knots.Length-1];
        }
        public static bool Reloading(float now,float reloadAt,float reloadUntil)=>reloadUntil>reloadAt&&now>=reloadAt&&now<reloadUntil;
        /// <summary>The reload clip's time: the rule's ReloadAt..ReloadUntil warped onto the whole clip, so the clip ends, back at
        /// rest, exactly when the reload does.</summary>
        public static float ReloadClipTime(float now,float reloadAt,float reloadUntil,float length,float[] warp)
            =>Reloading(now,reloadAt,reloadUntil)?Warp(warp,(now-reloadAt)/(reloadUntil-reloadAt))*length:-1;
        /// <summary>The reload's weight: in over ReloadIn, out over the last ReloadOut, 0 from ReloadUntil.</summary>
        public static float ReloadWeight(float now,float reloadAt,float reloadUntil)
        {
            if(!Reloading(now,reloadAt,reloadUntil))return 0;
            float span=reloadUntil-reloadAt,inn=Math.Min(ReloadIn,span*.25f),outt=Math.Min(ReloadOut,span*.25f);
            return MathUtil.Smooth((now-reloadAt)/inn)*MathUtil.Smooth((reloadUntil-now)/outt);
        }
        /// <summary>The time between this shot and the next one allowed: ShootReady where the pilot carries it, else the rule.</summary>
        public static float Interval(WeaponId w,float lastShot,float shootReady)
        {float rule=GunRules.Valid(w)&&w!=WeaponId.Sword?GunRules.Get(w).Interval:.5f;float d=shootReady-lastShot;return d>.02f&&d<rule*4?d:rule;}
        /// <summary>A fire action's clip time (the rifle's bolt): its action part (to OnActionEnded) fills the interval, so the
        /// bolt is home when the next shot is allowed; the settle after it runs at the clip's own pace. -1: not firing.</summary>
        public static float FireActionTime(float now,float lastShot,float interval,float actionSeconds,float length)
        {
            float age=now-lastShot;if(!(age>=0)||!(interval>0))return -1;
            if(age<interval)return age/interval*actionSeconds;
            float t=actionSeconds+(age-interval);return t<length?t:-1;
        }
        public static float FireActionWeight(float now,float lastShot,float interval,float actionSeconds,float length)
        {
            float t=FireActionTime(now,lastShot,interval,actionSeconds,length);if(t<0)return 0;
            float age=now-lastShot;return MathUtil.Smooth(age/.03f)*MathUtil.Smooth((length-t)/FireActionOut);
        }
        /// <summary>An additive fire kick's clip time: the clip at its own pace, quickened so it ends within the interval.</summary>
        public static float FireKickTime(float now,float lastShot,float interval,float length)
        {
            float age=now-lastShot;if(!(age>=0)||!(interval>0))return -1;float rate=Math.Max(1,length/interval);float t=age*rate;return t<length?t:-1;
        }
        /// <summary>The draw's clip time: its action part over the draw's seconds, then out; -1 once done or cut by a shot.</summary>
        public static float DrawTime(float now,float drawAt,float seconds,float actionSeconds)
        {float age=now-drawAt;return age>=0&&age<seconds?age/seconds*actionSeconds:-1;}
        public static float DrawWeight(float now,float drawAt,float seconds,float lastShot)
        {
            float age=now-drawAt;if(!(age>=0)||age>=seconds+DrawOut)return 0;
            float w=age<seconds?1:1-(age-seconds)/DrawOut;
            if(lastShot>=drawAt)w*=1-MathUtil.Clamp((now-lastShot)/DrawOut,0,1);
            return MathUtil.Smooth(w);
        }
        /// <summary>Aiming down the sights: the progress moves at 1/Settle a second (the gun's own aim time), eased.</summary>
        public static float AimStep(float progress,bool aiming,WeaponId w,float dt)
        {
            float settle=GunRules.Valid(w)&&w!=WeaponId.Sword?Math.Max(.08f,GunRules.Get(w).Settle):.2f;
            return MathUtil.Clamp(progress+(aiming?1:-1)*MathUtil.Clamp(dt,0,.1f)/settle,0,1);
        }
        public static float AimEase(float progress)=>.5f-.5f*(float)Math.Cos(Math.PI*MathUtil.Clamp(progress,0,1));
    }

    /// <summary>Unity-free quaternion and frame helpers in Unity's axes (left-handed, the same component layout).</summary>
    public static class NativeTacticalMath
    {
        // System.Numerics multiplies in the same order as Unity (a*b turns by b, then a) and rotates vectors the same way.
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)
        {
            if(Quaternion.Dot(a,b)<0)b=-b;var q=Quaternion.Slerp(a,b,MathUtil.Clamp(t,0,1));return q.LengthSquared()>1e-12f?Quaternion.Normalize(q):Quaternion.Identity;
        }
        public static Vector3 Rotate(Quaternion q,Vector3 v)=>Vector3.Transform(v,q);
        public static Quaternion Euler(Vector3 degrees)
        {
            // Unity's Quaternion.Euler: Z, then X, then Y (applied in that order).
            float k=(float)Math.PI/180;
            return Quaternion.CreateFromAxisAngle(Vector3.UnitY,degrees.Y*k)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,degrees.X*k)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,degrees.Z*k);
        }
        public static Quaternion Inverse(Quaternion q)=>Quaternion.Inverse(q);
        public static void Compose(Vector3 ap,Quaternion aq,Vector3 bp,Quaternion bq,out Vector3 p,out Quaternion q){p=ap+Rotate(aq,bp);q=Quaternion.Normalize(aq*bq);}
        public static void Relative(Vector3 ap,Quaternion aq,Vector3 bp,Quaternion bq,out Vector3 p,out Quaternion q){var inv=Quaternion.Inverse(aq);p=Rotate(inv,bp-ap);q=Quaternion.Normalize(inv*bq);}
    }

    /// <summary>A damped spring per recoil axis, kicked on each shot (seeded by the shot's time, so a replay kicks the same).</summary>
    public sealed class NativeTacticalRecoil
    {
        public float Back,Pitch,Yaw,Roll;private float vb,vp,vy,vr;
        public void Reset(){Back=Pitch=Yaw=Roll=vb=vp=vy=vr=0;}
        public void Kick(NativeTacticalGun g,float shotTime,float scale)
        {
            uint h=(uint)(int)Math.Round(shotTime*1000)*2654435761u;float r1=((h>>8)&1023)/1023f*2-1,r2=((h>>18)&1023)/1023f*2-1;
            float k=g.KickOmega;vb+=g.KickBack*scale*k;vp+=g.KickPitch*scale*k;vy+=g.KickYaw*r1*scale*k;vr+=g.KickRoll*r2*scale*k;
        }
        public void Step(NativeTacticalGun g,float dt)
        {
            dt=MathUtil.Clamp(dt,0,.05f);int n=Math.Max(1,(int)Math.Ceiling(dt*240));float h=dt/n,w=g.KickOmega,z=g.KickZeta;
            for(int i=0;i<n;i++){S(ref Back,ref vb);S(ref Pitch,ref vp);S(ref Yaw,ref vy);S(ref Roll,ref vr);}
            void S(ref float x,ref float v){v+=(-x*w*w-2*z*w*v)*h;x+=v*h;}
        }
    }

    /// <summary>The frame's inputs: the shown pilot's state (host or replicated snapshot), the local view's own (aim held, the
    /// camera's gait and look sway) and the view body's calibration.</summary>
    public struct NativeTacticalInput
    {
        public float Time;public WeaponId Weapon;public float LastShot,ShootReady,ReloadAt,ReloadUntil;public bool Aim,Sprinting,Grounded,Blocked;
        // The view's locomotion (NativeViewSway: walk share 0..1, the sprint carry's weight, the stride phase in turns) and look sway
        // (camera frame metres, roll radians) with the jump hop and landing.
        public float Walk,Sprint,Stride,SwayX,SwayY,SwayRoll,Hop,Land;
        // Camera shake share (0 off, .4 reduced, 1 on): the clips' camera motion and the walk bob scale by it.
        public float Shake;
        // The view just came (back) into first person with this gun: the draw plays.
        public bool Fresh;
    }

    /// <summary>The view body's calibration for a gun: its weapon frame in the pack gun's (from the idle's wrist, NativeTacticalView.
    /// Calibrate), its right hand bone in its weapon frame as its own grasp holds the model, and the model's rear sight.</summary>
    public struct NativeTacticalCalibration
    {
        public bool Set;public Vector3 GunToWeapon;public Quaternion GunToWeaponTurn;
        public Vector3 RightGrip;public Quaternion RightGripTurn;public Vector3 Sight,Support;
        // The left hand's shift from the pack's place (gun frame).
        public Vector3 LeftShift;
        // The pack's right hand frame -> the view body's right hand bone (constant per gun and body). The left hand takes the
        // pack's own place and palm (its support on the pack's gun reads true on the game's models, whose grip the wrists share).
        public Vector3 RightKey;public Quaternion RightKeyTurn;
    }

    /// <summary>One solved frame, camera frame: the model's weapon frame, the right hand bone's target, the left wrist and both
    /// palms (the pack's, riding the gun), elbow hints, shoulders, the fingers (palm frame directions, with how much the clip's
    /// fingers replace the grasp's) and the camera's own motion.</summary>
    public sealed class NativeTacticalSolution
    {
        public Vector3 Weapon,Right,Left,RightElbow,LeftElbow,RightShoulder,LeftShoulder;public Quaternion WeaponTurn,RightTurn,Camera,RightPalm,LeftPalm;
        public readonly Vector3[] RightFingers=new Vector3[15],LeftFingers=new Vector3[15];public float RightFingerWeight,LeftFingerWeight;
        public float Ads,ActionWeight;public string Action="";public float ActionTime;
    }

    /// <summary>The view's state between frames (the draw, the aim's progress, the recoil and the last shot seen) and its solve.</summary>
    public sealed class NativeTacticalView
    {
        public readonly NativeTacticalBank Bank;
        private WeaponId gun=WeaponId.Sword;private float at=float.NaN,drawAt=-100,aim,lastShot=float.NaN;
        public readonly NativeTacticalRecoil Recoil=new NativeTacticalRecoil();
        private readonly Vector3[] fr=new Vector3[15],fl=new Vector3[15],fr2=new Vector3[15],fl2=new Vector3[15];
        public NativeTacticalView(NativeTacticalBank bank){Bank=bank??throw new ArgumentNullException(nameof(bank));}
        public float AimProgress=>aim;public float DrawAt=>drawAt;

        /// <summary>The calibration from the gun's idle and the view body's grasp (hand bones in its weapon frame) and sight: the
        /// model's bore and up on the pack gun's, its right wrist on the pack's right wrist.</summary>
        public NativeTacticalCalibration Calibrate(NativeTacticalGun g,Vector3 rightGrip,Quaternion rightGripTurn,Vector3 sight,Vector3 support=default)
        {
            var c=new NativeTacticalCalibration{Set=true,RightGrip=rightGrip,RightGripTurn=rightGripTurn,Sight=sight,Support=support,GunToWeaponTurn=Quaternion.Identity};
            Bank.Clip(g.Idle).Sample(0,out var b);
            // The model's sight on the pack's sight line (bore and up shared), its right wrist level with the pack's along the bore.
            NativeTacticalMath.Relative(b.Gun,b.GunTurn,b.Right,b.RightTurn,out var hrp,out var hrq);
            c.GunToWeapon=new Vector3(-sight.X,g.SightHeight-sight.Y,hrp.Z-rightGrip.Z)+g.ModelOffset;
            // The left hand: on our right as the pack's on its right, or under our handguard as the pack's under its own.
            c.LeftShift=g.LeftOnRight?c.GunToWeapon+rightGrip-hrp:new Vector3(g.ModelOffset.X,c.GunToWeapon.Y+support.Y-g.PackSupport,g.ModelOffset.Z);
            // Pack hand -> our hand: K = inv(pack hand in gun) * C * our grip.
            NativeTacticalMath.Compose(c.GunToWeapon,c.GunToWeaponTurn,rightGrip,rightGripTurn,out var crp,out var crq);
            NativeTacticalMath.Relative(hrp,hrq,crp,crq,out c.RightKey,out c.RightKeyTurn);
            return c;
        }

        /// <summary>Solves the frame into `s`; false when the bank lacks the gun's clips.</summary>
        public bool Solve(in NativeTacticalInput i,in NativeTacticalCalibration cal,NativeTacticalSolution s)
        {
            var g=NativeTacticalGun.For(i.Weapon);if(g==null||!cal.Set||!Bank.Covers(g))return false;
            float dt=float.IsNaN(at)||i.Time<at||i.Time-at>.5f?0:i.Time-at;at=i.Time;
            if(i.Weapon!=gun||i.Fresh){gun=i.Weapon;drawAt=i.Time;aim=0;Recoil.Reset();lastShot=i.LastShot;}
            // A new shot: the recoil's kick (the pistol's and rifle's clips carry most of theirs).
            if(!float.IsNaN(lastShot)&&i.LastShot>lastShot+1e-4f&&i.LastShot<=i.Time+1e-3f)Recoil.Kick(g,i.LastShot,g.Fire==null?1:g.FireAction?.45f:.6f);
            lastShot=i.LastShot;Recoil.Step(g,dt);
            float interval=NativeTacticalTiming.Interval(i.Weapon,i.LastShot,i.ShootReady);
            bool reloading=NativeTacticalTiming.Reloading(i.Time,i.ReloadAt,i.ReloadUntil);
            var idle=Bank.Clip(g.Idle);idle.Sample(0,out var b,fr,fl);
            // The action layer: a reload, else the fire action (the bolt), else the draw.
            NativeTacticalClip act=null;float t=-1,w=0;
            if(reloading){act=Bank.Clip(g.Reload);t=NativeTacticalTiming.ReloadClipTime(i.Time,i.ReloadAt,i.ReloadUntil,act.Length,g.ReloadWarp);w=NativeTacticalTiming.ReloadWeight(i.Time,i.ReloadAt,i.ReloadUntil);}
            else if(g.FireAction&&(t=NativeTacticalTiming.FireActionTime(i.Time,i.LastShot,interval,Bank.Clip(g.Fire).ActionSeconds,Bank.Clip(g.Fire).Length))>=0){act=Bank.Clip(g.Fire);w=NativeTacticalTiming.FireActionWeight(i.Time,i.LastShot,interval,act.ActionSeconds,act.Length);}
            else{var d=Bank.Clip(g.Draw);w=NativeTacticalTiming.DrawWeight(i.Time,drawAt,g.DrawSeconds,i.LastShot);if(w>0){act=d;t=Math.Max(0,NativeTacticalTiming.DrawTime(i.Time,drawAt,g.DrawSeconds,d.ActionSeconds));if(i.Time-drawAt>=g.DrawSeconds)t=d.ActionSeconds;}}
            NativeTacticalFrame f=b;
            if(act!=null&&w>0)
            {
                act.Sample(t,out var a,fr2,fl2);f=Blend(b,a,w);
                for(int k=0;k<15;k++){fr[k]=Vector3.Normalize(Vector3.Lerp(fr[k],fr2[k],w));fl[k]=Vector3.Normalize(Vector3.Lerp(fl[k],fl2[k],w));}
                s.Action=act.Key;s.ActionTime=t;s.ActionWeight=w;
                s.Camera=NativeTacticalMath.Slerp(Quaternion.Identity,Quaternion.Normalize(a.Camera*Quaternion.Inverse(b.Camera)),w*MathUtil.Clamp(i.Shake,0,1));
            }
            else{s.Action="";s.ActionTime=0;s.ActionWeight=0;s.Camera=Quaternion.Identity;}
            // The hands and elbows ride the gun through everything layered on it below.
            NativeTacticalMath.Relative(f.Gun,f.GunTurn,f.Right,f.RightTurn,out var hrp,out var hrq);
            NativeTacticalMath.Relative(f.Gun,f.GunTurn,f.Left,f.LeftTurn,out var hlp,out _);
            var gi=Quaternion.Inverse(f.GunTurn);Quaternion rpq=Quaternion.Normalize(gi*f.RightPalm),lpq=Quaternion.Normalize(gi*f.LeftPalm);
            var er=NativeTacticalMath.Rotate(Quaternion.Inverse(f.GunTurn),f.RightElbow-f.Gun);var el=NativeTacticalMath.Rotate(Quaternion.Inverse(f.GunTurn),f.LeftElbow-f.Gun);
            Vector3 gp=f.Gun;Quaternion gq=f.GunTurn;
            // The pistol's additive kick (its clip from frame 0, quickened into the interval).
            if(g.Fire!=null&&!g.FireAction)
            {
                var fc=Bank.Clip(g.Fire);float ft=NativeTacticalTiming.FireKickTime(i.Time,i.LastShot,interval,fc.Length);
                if(ft>=0){fc.Sample(0,out var f0);fc.Sample(ft,out var f1);var dq=Quaternion.Normalize(f1.GunTurn*Quaternion.Inverse(f0.GunTurn));gp+=f1.Gun-f0.Gun;gq=Quaternion.Normalize(dq*gq);}
            }
            // The pack's weapon offset, the sprint's pose and carry, and the procedural layer (idle breathing, walk and sprint bob).
            aim=NativeTacticalTiming.AimStep(aim,i.Aim&&!reloading&&!i.Sprinting&&!i.Blocked&&w<.5f,i.Weapon,dt);float ads=NativeTacticalTiming.AimEase(aim);s.Ads=ads;
            float sprint=MathUtil.Clamp(i.Sprint,0,1)*(1-ads),walk=MathUtil.Clamp(i.Walk,0,1)*(1-sprint);
            gp+=g.WeaponOffset+g.SprintOffset*sprint+g.SprintCarry*sprint;
            gq=Quaternion.Normalize(NativeTacticalMath.Slerp(Quaternion.Identity,Quaternion.Normalize(g.SprintTurn*NativeTacticalMath.Euler(g.SprintCarryDegrees)),sprint)*gq);
            // Aiming down the sights: the model's rear sight onto the eye's axis, EyeRelief ahead, the bore along the view.
            {
                var wq=Quaternion.Identity;var wp=new Vector3(0,0,g.EyeRelief)-cal.Sight;var aq=Quaternion.Normalize(wq*Quaternion.Inverse(cal.GunToWeaponTurn));var ap=wp-NativeTacticalMath.Rotate(aq,cal.GunToWeapon);
                gp=Vector3.Lerp(gp,ap,ads);gq=NativeTacticalMath.Slerp(gq,aq,ads);
            }
            // The pack's procedural layer on top (its intensities lower while aimed).
            Locomotion(g,i,ads,walk,sprint,out var addP,out var addQ);gp+=addP;gq=Quaternion.Normalize(addQ*gq);
            // Recoil about its pivot, then the look sway, hop and landing (the sway at 30 % aimed).
            {
                var kq=NativeTacticalMath.Euler(new Vector3(-Recoil.Pitch,Recoil.Yaw,Recoil.Roll));var piv=g.KickPivot;
                var nq=Quaternion.Normalize(gq*kq);gp+=NativeTacticalMath.Rotate(gq,piv)-NativeTacticalMath.Rotate(nq,piv)+NativeTacticalMath.Rotate(nq,new Vector3(0,0,-Recoil.Back));gq=nq;
                float damp=1-ads*.7f;gp+=new Vector3(i.SwayX,i.SwayY+i.Hop,0)*damp+new Vector3(0,i.Land*.05f,0);
                gq=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-i.SwayRoll*damp)*gq);
            }
            // The model's weapon frame and the hand bones' targets (pack hand x key), elbows along.
            NativeTacticalMath.Compose(gp,gq,cal.GunToWeapon,cal.GunToWeaponTurn,out s.Weapon,out s.WeaponTurn);
            NativeTacticalMath.Compose(gp,gq,hrp,hrq,out var prp,out var prq);NativeTacticalMath.Compose(prp,prq,cal.RightKey,cal.RightKeyTurn,out s.Right,out s.RightTurn);
            s.Left=gp+NativeTacticalMath.Rotate(gq,hlp+cal.LeftShift+g.LeftOffset);s.LeftPalm=Quaternion.Normalize(gq*lpq);s.RightPalm=Quaternion.Normalize(gq*rpq);
            s.RightElbow=gp+NativeTacticalMath.Rotate(gq,er);s.LeftElbow=gp+NativeTacticalMath.Rotate(gq,el);
            s.RightShoulder=f.RightShoulder;s.LeftShoulder=f.LeftShoulder;
            for(int k=0;k<15;k++){s.RightFingers[k]=fr[k];s.LeftFingers[k]=fl[k];}
            // The clip's fingers replace the grasp's while an action moves the hands (a reload, the bolt, the draw).
            s.RightFingerWeight=w;s.LeftFingerWeight=1;
            return true;
        }

        // The pack's procedural layer (its Movement blend tree on ik_hand_gun_additive): the idle pose to the idle loop by the
        // idle intensity, the walk pose to the walk loop (on the stride; the loop holds three strides, the sprint loop two) by the
        // walk intensity, and the sprint loop, by gait.
        public const float WalkCycles=3,SprintCycles=2;
        private void Locomotion(NativeTacticalGun g,in NativeTacticalInput i,float ads,float walk,float sprint,out Vector3 p,out Quaternion q)
        {
            float idleK=g.IdleIntensity+(g.IdleAimIntensity-g.IdleIntensity)*ads,walkK=(g.WalkIntensity+(g.WalkAimIntensity-g.WalkIntensity)*ads)*MathUtil.Clamp(i.Shake,.3f,1);
            var ip=Bank.Clip(NativeTacticalGun.IdlePose);var il=Bank.Clip(NativeTacticalGun.IdleLoop);ip.Sample(0,out var a0);il.Sample(Loop(i.Time,il.Length),out var a1);
            var wp=Bank.Clip(NativeTacticalGun.WalkPose);var wl=Bank.Clip(NativeTacticalGun.WalkLoop);wp.Sample(0,out var b0);wl.Sample(Phase(i.Stride/WalkCycles)*wl.Length,out var b1);
            var sl=Bank.Clip(NativeTacticalGun.SprintLoop);sl.Sample(Phase(i.Stride/SprintCycles)*sl.Length,out var c1);
            Vector3 idleP=Vector3.Lerp(a0.Add,a1.Add,idleK),walkP=Vector3.Lerp(b0.Add,b1.Add,walkK);
            Quaternion idleQ=NativeTacticalMath.Slerp(a0.AddTurn,a1.AddTurn,idleK),walkQ=NativeTacticalMath.Slerp(b0.AddTurn,b1.AddTurn,walkK);
            p=Vector3.Lerp(Vector3.Lerp(idleP,walkP,walk),c1.Add,sprint);q=NativeTacticalMath.Slerp(NativeTacticalMath.Slerp(idleQ,walkQ,walk),c1.AddTurn,sprint);
        }
        private static float Loop(float t,float length)=>length>0?(float)(t-Math.Floor(t/length)*length):0;
        private static float Phase(float turns)=>MathUtil.Finite(turns)?turns-(float)Math.Floor(turns):0;
        private static NativeTacticalFrame Blend(in NativeTacticalFrame a,in NativeTacticalFrame b,float w)
        {
            NativeTacticalFrame f;
            f.Gun=Vector3.Lerp(a.Gun,b.Gun,w);f.GunTurn=NativeTacticalMath.Slerp(a.GunTurn,b.GunTurn,w);f.Right=Vector3.Lerp(a.Right,b.Right,w);f.RightTurn=NativeTacticalMath.Slerp(a.RightTurn,b.RightTurn,w);
            f.Left=Vector3.Lerp(a.Left,b.Left,w);f.LeftTurn=NativeTacticalMath.Slerp(a.LeftTurn,b.LeftTurn,w);f.RightElbow=Vector3.Lerp(a.RightElbow,b.RightElbow,w);f.LeftElbow=Vector3.Lerp(a.LeftElbow,b.LeftElbow,w);
            f.RightShoulder=Vector3.Lerp(a.RightShoulder,b.RightShoulder,w);f.LeftShoulder=Vector3.Lerp(a.LeftShoulder,b.LeftShoulder,w);f.Camera=NativeTacticalMath.Slerp(a.Camera,b.Camera,w);
            f.Add=Vector3.Lerp(a.Add,b.Add,w);f.AddTurn=NativeTacticalMath.Slerp(a.AddTurn,b.AddTurn,w);
            f.RightPalm=NativeTacticalMath.Slerp(a.RightPalm,b.RightPalm,w);f.LeftPalm=NativeTacticalMath.Slerp(a.LeftPalm,b.LeftPalm,w);return f;
        }
    }
}
