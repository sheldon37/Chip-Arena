using System;

namespace ChipArena.UnityConversion
{
    // Inputs of the source view layer, in the source frame: yaw rises turning left, pitch rises looking up (radians), velocity
    // in the source world (Unity x mirrored). Ads is the view model's ADS blend (NativeAimMotion: the source's s.ads spring).
    public struct NativeViewSwayInput
    {
        public int Pilot;public float Time,Yaw,Pitch,VelocityX,VelocityY,VelocityZ,Ads;
        public bool Grounded,Planted,Gait;
        // Ranged view layers: the gun, its ADS kick factor, the last shot, the reload span and the latest Q/E phrase.
        public bool Ranged;public WeaponId Weapon;public float AdsKick,LastShot,ReloadAt,ReloadUntil,AbilityAt,AbilityRelease;public string AbilityId;
    }
    // View-layer offsets in the source camera frame (x right, y up, z toward the viewer) and the X/Z view-root rotation (radians).
    public struct NativeViewSwayPose
    {
        public float X,Y,Z,Pitch,Roll;
        // Unity camera frame (+z forward). The source root is Rx(Pitch)·Ry(π)·Rz(Roll) = Rx(Pitch)·Rz(-Roll)·Ry(π); the Ry(π) and
        // the rig's x mirror cancel against the camera's z mirror, leaving Rx(-Pitch)·Rz(-Roll) = Quaternion.Euler(UnityPitch,0,UnityRoll).
        public float UnityZ=>-Z;public float UnityPitchDegrees=>-Pitch*180/(float)Math.PI;public float UnityRollDegrees=>-Roll*180/(float)Math.PI;
    }
    // client/first-person-view.ts poseFirstPersonView view layer (lines 48-73): look-sway springs from aim deltas, stride bob
    // from the first-person rig's locomotion (motion-quality.ts locomotion for a first-person rig: no root travel, the group
    // never turns), landing kick, recoil push, reload dip, run-low carry, rocket kick and mark flick. Presentation only: it
    // reads the replicated pilot view and the local camera, and never writes simulation state.
    public sealed class NativeViewSway
    {
        private const float Tau=(float)(Math.PI*2);
        private bool has,gaitHas;private int pilot;private float t,yaw,pitch,sx,sxv,sy,syv,roll,rollv,land,landv,vy,gaitTime,forward=1;private bool grounded;
        public float Motion {get;private set;}
        public float Stride {get;private set;}
        public float SwayX=>sx;public float SwayY=>sy;public float SwayRoll=>roll;public float Land=>land;
        // A new rig: everything starts at rest, as createCharacterRig and a fresh view state do.
        public void Reset(){has=gaitHas=false;Motion=Stride=0;forward=1;sx=sxv=sy=syv=roll=rollv=land=landv=0;}
        public static float SmoothMotion(float x){x=MathUtil.Clamp(x,0,1);return x*x*x*(x*(x*6-15)+10);}
        // gun-kit.ts ADS_PROFILES kick (recoil scale at full ADS); the managed check compares it with GameContent.json.
        public static float AdsKick(WeaponId weapon)=>weapon==WeaponId.Pistol?.5f:weapon==WeaponId.MachineGun?.45f:weapon==WeaponId.Rifle?.6f:1;
        public static float Wrap(float a)=>(float)Math.Atan2(Math.Sin(a),Math.Cos(a));
        // ranged-motion.ts rocketKick: a 30 ms rise, then a .32 s ease back.
        public static float RocketKick(float rel)=>rel<0?0:rel<.03f?rel/.03f:(float)Math.Pow(Math.Max(0,1-(rel-.03f)/.32f),2);
        // motion-quality.ts cycleLength (GAIT cycleBase .62, cyclePerSpeed .22, cycleMax 2.1, runSpeed 6.8, dashCycle 2.6, dashSpeed 15).
        public static float CycleLength(float speed)=>speed<=6.8f?MathUtil.Clamp(.62f+.22f*speed,.62f,2.1f):2.1f+(2.6f-2.1f)*MathUtil.Clamp((speed-6.8f)/(15-6.8f),0,1);
        // ranged-motion.ts reloadPhase envelope: smoothMotion(elapsed/.14)*smoothMotion(remaining/.18), 0 when not reloading.
        public static float ReloadEnvelope(float time,float reloadAt,float reloadUntil)
        {float left=reloadUntil-time;return left>0?SmoothMotion(Math.Max(0,time-reloadAt)/.14f)*SmoothMotion(left/.18f):0;}
        // character-animation.ts sampleCharacter: commitment, hitstun and dodge rolls hold the stride (souls-combat.ts committed/rolling).
        public static bool Planted(Actor a,float time)=>a.CommitAt>-90&&time<a.CommitUntil||a.StunUntil>time||a.Rolling(time);
        // The camera's look in the source frame from its Unity forward vector: Unity yaw turns clockwise and pitch looks down.
        public static float SourceYaw(float forwardX,float forwardZ)=>-(float)Math.Atan2(forwardX,forwardZ);
        public static float SourcePitch(float forwardY)=>(float)Math.Asin(MathUtil.Clamp(forwardY,-1,1));
        private static float Finite(float v)=>MathUtil.Finite(v)?v:0;
        // The view layer's inputs from a presented pilot (host or replicated snapshot) and the local camera's look.
        public static NativeViewSwayInput Input(Actor a,float time,float yaw,float pitch,float ads)
        {
            bool ranged=GunRules.Ranged(a.Weapon);
            return new NativeViewSwayInput{Pilot=a.Id,Time=time,Yaw=yaw,Pitch=pitch,VelocityX=-Finite(a.Velocity.x),VelocityY=Finite(a.Velocity.y),VelocityZ=Finite(a.Velocity.z),Ads=ads,
                Grounded=a.Grounded,Planted=Planted(a,time),Gait=a.SourceSpec!="siren",Ranged=ranged,Weapon=a.Weapon,AdsKick=AdsKick(a.Weapon),LastShot=a.LastShot,
                ReloadAt=a.ReloadAt,ReloadUntil=a.ReloadUntil,AbilityId=a.AbilityId,AbilityAt=a.AbilityAt,AbilityRelease=a.AbilityRelease};
        }
        // The first-person rig's locomotion (character-animation.ts sampleCharacter + motion-quality.ts locomotion): planted
        // actions decay the motion and zero the ground velocity; the gait blends toward speed/6.8 and the stride phase advances
        // at speed over the cycle length. The rig's group never turns, so its "forward" share is the world z travel. Another
        // pilot starts the gait from rest (motion-quality.ts:154).
        private void Locomotion(in NativeViewSwayInput i,float dt)
        {
            if(gaitHas&&i.Pilot!=pilot){gaitHas=false;Motion=Stride=0;}
            if(i.Planted)Motion*=(float)Math.Exp(-MathUtil.Clamp(dt,0,.05f)*22);
            dt=MathUtil.Clamp(dt,0,.05f);
            if(!gaitHas||i.Time<gaitTime-.1f||i.Time-gaitTime>.35f){gaitHas=true;pilot=i.Pilot;gaitTime=i.Time;forward=1;}
            gaitTime=Math.Max(i.Time,gaitTime);
            float vx=i.Planted?0:i.VelocityX,vz=i.Planted?0:i.VelocityZ,speed=(float)Math.Sqrt(vx*vx+vz*vz);
            float blend=1-(float)Math.Exp(-dt*13),goal=i.Grounded?MathUtil.Clamp(speed/6.8f,0,1.2f):0;Motion+=(goal-Motion)*blend;
            if(speed>.1f)forward+=(vz/speed-forward)*blend;
            float back=SmoothMotion((-forward-.3f)/.4f)*MathUtil.Clamp(speed/1.5f,0,1),cycle=CycleLength(speed)*(1+(.8f-1)*back);
            float rate=i.Grounded&&speed>.05f&&!i.Planted?Math.Min(speed/cycle,3.7f):0;Stride+=Tau*rate*dt;
        }
        // One presentation frame. The step is taken from the presentation clock (the source's frame dt while that clock runs),
        // so a paused or slowed match holds or slows the view layer with the world.
        public NativeViewSwayPose Sample(in NativeViewSwayInput i)
        {
            // The rig animates first (locomotion on the frame step), then the view layer reads its stride.
            if(i.Gait)Locomotion(i,has&&i.Time>=t?i.Time-t:0);
            // ranged-motion.ts clockStep: fresh on the first sample, backward time or a jump over .35 s; the step clamps to .05.
            float step=has?i.Time-t:-1;bool fresh=!has||step<0||step>.35f;float h=fresh?0:Math.Min(step,.05f);
            if(fresh){has=true;t=i.Time;yaw=i.Yaw;pitch=i.Pitch;sx=sxv=sy=syv=roll=rollv=land=landv=0;grounded=i.Grounded;vy=i.VelocityY;}
            // Look sway: the weapon follows a turn by a few centimetres at most, then springs back.
            float dyaw=Wrap(i.Yaw-yaw),dpitch=i.Pitch-pitch,rate=h>0?1/h:0;
            float tx=MathUtil.Clamp(-dyaw*rate*.004f,-.03f,.03f),ty=MathUtil.Clamp(-dpitch*rate*.004f,-.025f,.025f),tr=MathUtil.Clamp(dyaw*rate*.008f,-.05f,.05f);
            if(h>0)
            {
                NativeFirstPersonPose.Spring(ref sx,ref sxv,tx,14,.8f,h);NativeFirstPersonPose.Spring(ref sy,ref syv,ty,14,.8f,h);NativeFirstPersonPose.Spring(ref roll,ref rollv,tr,14,.8f,h);
                // Landing: the grounded edge kicks the view model down in proportion to the fall speed, then it springs back.
                if(i.Grounded&&!grounded)landv-=Math.Min(1,Math.Max(0,-vy)/9)*.9f;
                NativeFirstPersonPose.Spring(ref land,ref landv,0,16,.55f,h);
            }
            t=i.Time;yaw=i.Yaw;pitch=i.Pitch;grounded=i.Grounded;if(!i.Grounded)vy=i.VelocityY;
            // Stride bob from the rig's own stride phase, scaled by how much it is running.
            float run=Math.Min(1,Motion),bx=(float)Math.Sin(Stride)*.012f*run,by=-Math.Abs((float)Math.Cos(Stride))*.015f*run,a=MathUtil.Clamp(i.Ads,0,1);
            // Recoil push, reload dip, run-low carry, rocket kick and mark flick: guns only.
            float kick=0,reload=0,runLow=0,rocket=0,flick=0;
            if(i.Ranged)
            {
                float shotAge=i.Time-i.LastShot;bool rifle=i.Weapon==WeaponId.Rifle;
                if(shotAge>=0)kick=(float)Math.Exp(-shotAge*(rifle?14:20))*(rifle?1.4f:i.Weapon==WeaponId.MachineGun?.6f:1)*(1+(i.AdsKick-1)*a);
                // ranged-motion.ts abilityPhrase: the latest Q/E cast for three seconds; rel is the time since its release.
                float age=i.Time-i.AbilityAt,rel=i.Time-i.AbilityRelease;
                if(i.AbilityId!=null&&age>=0&&age<=3){if(i.AbilityId=="rocket")rocket=RocketKick(rel);else if(i.AbilityId=="orbital-mark"&&rel>=0&&rel<.2f)flick=(float)Math.Sin(Math.PI*rel/.2f);}
                reload=ReloadEnvelope(i.Time,i.ReloadAt,i.ReloadUntil);runLow=run*MathUtil.Clamp((shotAge-.3f)/.3f,0,1)*(1-reload);
            }
            // ADS damps the sway and bob to 30 %; the dips, kicks and flick ride on top of the (aligned) ADS pose.
            float damp=1-a*.7f;
            return new NativeViewSwayPose{X=(bx+sx)*damp,Y=(by+sy)*damp+land*.05f-.06f*reload-.03f*runLow-.07f*rocket,Z=.04f*kick+.08f*rocket,Pitch=.06f*kick-.1f*runLow+.12f*rocket+.06f*flick,Roll=roll+.25f*reload};
        }
    }
}
