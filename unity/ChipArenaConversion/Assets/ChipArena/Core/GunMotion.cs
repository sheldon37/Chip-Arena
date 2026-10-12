using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A third-person gun pilot (pistol, rifle, machine gun; host, guests, bots and the killcam), posed as the source's
    /// sampleCharacter poses every class (presentation only): motion-quality locomotion in the gun's stance (stances.ts:
    /// planted gait feet, stride scaled by the replicated velocity, body bob, sway and lean into turns, pelvis/spine/chest
    /// counter-twist, the head kept on the aim, arm swing, sprint lean, push-off/apex/descent air poses and the landing
    /// spring), then the gun hold over that gait instead of over a reset rest pose. While the gun is up (aiming, firing,
    /// reloading, guarding, strafing, backpedalling or standing; ranged-motion.ts updateAimWeight) the torso blends onto the
    /// native hold (RangedPose: the aimed torso, the shouldered or isosceles brace, recoil, the reload choreography, guard),
    /// cancelling the gait's lean, roll and hip twist so the chest and the gun stay on the aim while the hips and legs move
    /// under it. Running forward lowers the gun: the torso keeps the gait's bounce and counter-twist; the long guns stay in
    /// both hands in the chest-space carry (riding the torso, with the stride's wobble), the pistol is carried one-handed at
    /// low ready (orientHeld) while both arms keep the running pump. Finish runs the ground pass after the native overlays
    /// (juggle, roll, reactions, recovery) and pose-transitions' finishPose blends. Replicated Actor fields only; no
    /// allocation per frame. The first-person view model keeps RangedPose.Apply on the generic path.
    /// </summary>
    public sealed class GunMotion
    {
        public readonly MeleeRig Rig=new MeleeRig();
        private readonly MeleeGait gait=new MeleeGait();
        private readonly MeleeView view=new MeleeView();
        private readonly NativePoseTransition transition=new NativePoseTransition();
        private float last=-100,dt=1/60f,sprintBlend,aim=1,aimVelocity;private bool aimHas;private int viewId=int.MinValue;
        // The frame's state (the native debug label): the gun hold's state, else the overlay that owns the frame.
        public const string Ready="ready",Carry="carry",Aim="aim",Fire="fire",Reload="reload",Block="block",Jump="jump",Roll="roll (adapted)",Juggle="juggle",Recovery="recovery";
        /// <summary>The gun-up weight (0 running forward with the gun lowered, 1 aimed), as the last Sample left it.</summary>
        public float AimWeight=>aim;
        /// <summary>Whether this frame's ground pass is skipped (dead, juggled, knocked down or rolling), set by Pose for Finish.</summary>
        public bool GroundSkipped{get;private set;}
        /// <summary>The jump layer (Core/NativeJumpMotion) under the gun hold; null keeps the source port (parity checks).</summary>
        public NativeJumpMotion JumpLayer;
        public static string Spec(WeaponId w)=>w==WeaponId.Rifle?"rifle":w==WeaponId.MachineGun?"machinegun":"pistol";
        public static string StateOf(Actor a,float time,float motion)=>a.ReloadUntil>time?Reload:time<a.BlockUntil?Block:time-a.LastShot<.2f?Fire:a.Aiming?Aim:!a.Grounded?Jump:motion>.05f?Carry:Ready;
        private Actor shown;private bool shownJuggled;
        /// <summary>
        /// One third-person frame after the caller placed the rig (Rig.GroupPosition / GroupYaw, source frame): the gait and the
        /// gun hold (Sample), then sampleCharacter's overrides: a juggle (`juggled`, observed into `juggle`) owns the frame with
        /// animateJuggle over the gait; otherwise the dodge roll, the E special and the knockdown recovery pose over it.
        /// `react` tells the caller to add the hit reaction (never during a juggle, roll, special or recovery); Finish follows.
        /// Returns the state. No allocation.
        /// </summary>
        public string Pose(Actor a,float time,CapturedRig.Pose pose,bool juggled,NativeJuggleTrack juggle,out bool react)
        {
            bool roll=!juggled&&a.Rolling(time),special=!juggled&&NativeSpecialPose.Active(a,time),down=!juggled&&NativeSpecialPose.Down(a,time);
            Sample(a,time,pose);string state=StateOf(a,time,Rig.Motion);
            if(juggled){NativeJugglePose.Apply(pose,time,juggle,a.Velocity.y);state=Juggle;}
            if(roll){CapturedRig.Roll(pose,(time-a.RollAt)/a.VisualRollDuration);state=Roll;}
            react=!juggled&&!roll&&!special&&!down&&a.ReactionAt>-90;
            if(special){NativeSpecialPose.Special(pose,a,time,false);state=a.SpecialKind;}
            if(down){NativeSpecialPose.Recover(pose,a,time);state=Recovery;}
            GroundSkipped=a.Hp<=0||juggled||down||roll;shown=a;shownJuggled=juggled;
            return state;
        }
        /// <summary>After Pose (and the caller's hit reaction): the ground pass, then pose-transitions.ts finishPose.</summary>
        public void Finish(CapturedRig.Pose pose,float time)
        {
            Rig.ReadPose(pose);gait.GroundPass(Rig,view,time,dt,GroundSkipped);
            if(shown!=null)transition.Apply(Rig,shown,time,shownJuggled);shown=null;
            Rig.WritePose(pose);pose.World();
        }
        // ranged-motion.ts springStep: semi-implicit, sub-stepped (<= 1/240 s) so the stiff spring is stable at 30 Hz.
        private static void SpringStep(ref float x,ref float v,float target,float omega,float zeta,float step)
        {
            int n=Math.Max(1,(int)Math.Ceiling(step*240));float h=step/n;
            for(int i=0;i<n;i++){v+=(-(x-target)*omega*omega-2*zeta*omega*v)*h;x+=v*h;}
        }
        /// <summary>
        /// ranged-motion.ts updateAimWeight for a gun: the gun stays up unless the pilot runs forward (stride blend at least .18,
        /// heading within ~53 degrees of the travel) with no shot for max(.38, interval + .15) s, no reload, guard or ADS. A
        /// critically damped spring (stiffer, 32/s, when a shot raises it from a low carry); it starts on its target for a new
        /// pilot, after a rewind or a gap (the killcam and joining guests never ease in from a stale weight).
        /// </summary>
        public static float AimTarget(Actor a,float time,float motion)
        {
            const float D=(float)Math.PI/180;float speed=(float)Math.Sqrt(a.Velocity.x*a.Velocity.x+a.Velocity.z*a.Velocity.z),yaw=a.Yaw*D;
            float ahead=speed>1?(a.Velocity.x*(float)Math.Sin(yaw)+a.Velocity.z*(float)Math.Cos(yaw))/speed:1,shotAge=time-a.LastShot;
            return shotAge<Math.Max(.38f,GunRules.Get(a.Weapon).Interval+.15f)||a.ReloadUntil>time||motion<.18f||ahead<.6f||time<a.BlockUntil||a.Aiming?1:0;
        }
        private void UpdateAim(Actor a,float time,bool restart)
        {
            float target=AimTarget(a,time,Rig.Motion);
            if(!aimHas||restart){aimHas=true;aim=target;aimVelocity=0;return;}
            float omega=target>aim&&time-a.LastShot<.1f&&aim<.6f?32:24,x=aim;SpringStep(ref x,ref aimVelocity,target,omega,1,MeleeMath.Clamp(dt,0,.05f));
            aim=MeleeMath.Clamp(x,0,1);if(aim!=x)aimVelocity=0;
        }
        private void Toward(int bone,Vector3 target,float k){Vector3 e=Rig.E[bone];Rig.SetE(bone,e+(target-e)*k);}
        /// <summary>The gait and the gun hold for one frame (Pose calls it). The caller sets Rig.GroupPosition / GroupYaw first.</summary>
        public void Sample(Actor a,float time,CapturedRig.Pose pose)
        {
            bool restart=last<-90||time<last-MeleeGait.REWIND||time-last>.35f;
            dt=last<-90||time<last||time-last>.35f?1/60f:Math.Min(.1f,time-last);last=time;
            const float D=(float)Math.PI/180;var v=view;var r=Rig;
            if(a.Id!=viewId){viewId=a.Id;v.Id=a.Id.ToString();restart=true;}
            v.Spec=Spec(a.Weapon);v.Yaw=-a.Yaw*D;v.Pitch=a.Pitch*D;v.Grounded=a.Grounded;v.Hp=a.Hp;v.Sprinting=a.Sprinting;v.HitStunUntil=a.StunUntil;
            v.RollAt=a.RollAt;v.RollUntil=a.RollAt>-90?a.RollAt+a.VisualRollDuration:-100;
            // sampleCharacter: hitstun and rolls hold the stride (the action moves the root, not running steps).
            bool planted=v.HitStunUntil>time||v.Rolling(time);
            r.Reset();r.LegsAuthored=r.LegsLayered=r.MocapPosed=false;r.WeaponReach=1;
            if(planted)r.Motion*=(float)Math.Exp(-MeleeMath.Clamp(dt,0,.05f)*22);
            v.Vel=planted?new Vector3(0,a.Velocity.y,0):new Vector3(-a.Velocity.x,a.Velocity.y,a.Velocity.z);
            // stances.ts: the rifle's hips turn behind the gun as far as it is raised (the previous frame's weight, as the source
            // reads r.aimWeight before updateAimWeight).
            gait.PelvisYawScale=a.Weapon==WeaponId.Rifle?aim:1;
            gait.Locomotion(r,v,time,dt,planted);
            v.Vel=new Vector3(-a.Velocity.x,a.Velocity.y,a.Velocity.z);
            // melee-guard sprintLean: the guns have no stance lean, only the sprint lean.
            float run=MeleeMath.Ease((r.Motion-.1f)/.65f);
            sprintBlend+=((v.Sprinting?1:0)-sprintBlend)*(1-(float)Math.Exp(-Math.Min(.05f,Math.Max(0,dt))*10));
            if(sprintBlend>1e-4f)r.AddE(CapturedRig.Spine,.12f*sprintBlend*run,0,0);
            JumpLayer?.Ranged(r,a,v.Spec,time,dt,.4f);
            UpdateAim(a,time,restart);
            var h=RangedPose.HoldOf(a,time,aim);float k=h.Weight;
            if(k>1e-4f)
            {
                // ranged-motion.ts rangedLayer: the raised gun cancels the travel lean (spine) and roll (chest), and here the hip
                // twist too, so the chest, head and gun hold exactly as the native hold aims them while the hips swing under it.
                Vector3 body=r.E[CapturedRig.Body],pelvis=r.E[CapturedRig.Pelvis],chest=RangedPose.ChestAim(h);chest.Z=-body.Z-pelvis.Z;
                Toward(CapturedRig.Spine,RangedPose.SpineAim(h)-new Vector3(body.X,pelvis.Y,0),k);Toward(CapturedRig.Chest,chest,k);
                Toward(CapturedRig.Neck,RangedPose.NeckAim(h),k);Toward(CapturedRig.Head,RangedPose.HeadAim(h),k);
                Toward(CapturedRig.LClav,RangedPose.LClavAim(h),k);Toward(CapturedRig.RClav,RangedPose.RClavAim(h),k);
            }
            r.WritePose(pose);pose.World();
            // The carry's stride wobble (carryOrientation), less the chest's forward pitch (the run and sprint lean would
            // otherwise tip the chest-space carry's muzzle toward the floor), and the pistol's one-handed low ready over the
            // arm pump.
            Vector3 ahead=Vector3.Transform(Vector3.UnitZ,pose.WorldRotation[CapturedRig.Chest]);float lean=(float)Math.Asin(MeleeMath.Clamp(-ahead.Y,-1,1));
            float sway=MeleeMath.Clamp(r.Motion,0,1),sx=(float)Math.Cos(2*r.Stride)*(h.LongGun?.05f:.04f)*sway-lean,sy=(float)Math.Sin(r.Stride)*(h.LongGun?.06f:.05f)*sway;
            bool pistol=!h.LongGun;
            RangedPose.Arms(pose,a,time,h,pistol?k:1,pistol?(1-k)*MeleeMath.Ease(r.Motion/.35f):0,sx,sy);
            RangedAbilityPose.Apply(pose,a,time,false);
        }
    }
}
