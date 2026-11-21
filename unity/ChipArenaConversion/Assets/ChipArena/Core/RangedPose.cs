using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Adapted from ranged-motion.ts: shouldered rifles, two-hand pistol, recoil and magazine/charge choreography.
    // These are procedural poses in the original game too, not part of the imported melee capture pack.
    public static class RangedPose
    {
        private const float Rad=(float)Math.PI/180;
        public static float Recoil(WeaponId weapon,float age)
        {
            if(age<0||age>.8f) return 0;
            if(age<.03f) return (float)Math.Sin(Math.PI*.5*age/.03);
            float u=age-.03f;
            if(weapon==WeaponId.Pistol) { double wd=24*Math.Sqrt(1-.6*.6); return (float)(Math.Exp(-.6*24*u)*(Math.Cos(wd*u)+.6*24/wd*Math.Sin(wd*u))); }
            return (1+18*u)*(float)Math.Exp(-18*u)*(weapon==WeaponId.Rifle?2.1f:.65f);
        }
        public static float ReloadPhase(Actor a,float time,out float envelope)
        {
            if(a.ReloadUntil<=time||!GunRules.Ranged(a.Weapon)) { envelope=0;return 0; }
            float elapsed=Math.Max(0,time-a.ReloadAt),duration=GunRules.Get(a.Weapon).Reload;
            envelope=CapturedClip.Ease(elapsed/.14f)*CapturedClip.Ease((a.ReloadUntil-time)/.18f);
            return MathUtil.Clamp(elapsed/duration,0,1);
        }
        public static float Curve(float[] times,float[] values,float t)
        {
            if(t<=times[0]) return values[0]; for(int i=1;i<times.Length;i++) if(t<=times[i]) return values[i-1]+(values[i]-values[i-1])*CapturedClip.Ease((t-times[i-1])/(times[i]-times[i-1]));
            return values[values.Length-1];
        }
        private static readonly float[] TuckTimes={0,.12f,.8f,.95f,1},TuckValues={0,1,1,0,0};
        private static readonly float[] HandTimes={0,.1f,.2f,.3f,.42f,.5f,.6f,.68f,.71f,.74f,.83f,.89f,.97f,1};
        private static readonly float[] ReachTimes={0,.08f,.14f,.94f,1},ReachValues={0,0,1,1,0};
        public static Vector3 Support(WeaponId w) => w==WeaponId.Pistol?new Vector3(.06f,-.10f,-.01f):new Vector3(.025f,-.03f,.10f);
        public static Vector3 Magazine(WeaponId w) => w==WeaponId.Pistol?new Vector3(0,-.22f,-.065f):new Vector3(0,-.19f,.16f);
        /// <summary>
        /// The frame's gun hold from authoritative timestamps (no allocation): the recoil, the reload phase and its envelope,
        /// the reload tuck, the guard weight, the hold (the aim weight raised by the guard), the long guns' torso blade and the
        /// aim pitch (radians).
        /// </summary>
        public struct Hold {public bool LongGun,MachineGun;public float Kick,Phase,Envelope,Tuck,Guard,Weight,Blade,Pitch;}
        public static Hold HoldOf(Actor a,float time,float aimWeight)
        {
            var h=new Hold{LongGun=a.Weapon!=WeaponId.Pistol,MachineGun=a.Weapon==WeaponId.MachineGun,Kick=Recoil(a.Weapon,time-a.LastShot),Pitch=a.Pitch*Rad};
            h.Phase=ReloadPhase(a,time,out h.Envelope);h.Tuck=Curve(TuckTimes,TuckValues,h.Phase)*h.Envelope;
            h.Guard=time<a.BlockUntil?CapturedClip.Ease((time-a.BlockUntil+Rules.BlockDuration)/.07f):0;
            h.Weight=aimWeight+(1-aimWeight)*h.Guard;h.Blade=(h.LongGun?.75f:.04f)*h.Weight*(1-h.Guard);return h;
        }
        // The aimed torso as three.js XYZ Euler angles (radians, source frame): spine, chest, neck, head and both clavicles.
        public static Vector3 SpineAim(in Hold h)=>new Vector3(-h.Pitch*.22f*h.Weight+h.Guard*.12f,-h.Blade*.45f,0);
        public static Vector3 ChestAim(in Hold h)=>new Vector3(-h.Pitch*.18f*h.Weight-h.Kick*.05f*h.Weight+h.Tuck*.06f,-h.Blade*.55f+h.Tuck*.2f,0);
        public static Vector3 NeckAim(in Hold h)=>new Vector3(-h.Pitch*.10f*h.Weight+(h.LongGun?.15f:0),0,h.LongGun?.12f:0);
        public static Vector3 HeadAim(in Hold h)=>new Vector3(h.Pitch*.5f*h.Weight+(h.LongGun?.12f:0)+h.Kick*.05f+h.Tuck*.25f,h.Blade,h.LongGun?.06f:-.08f);
        public static Vector3 LClavAim(in Hold h)=>new Vector3(0,-(h.LongGun?.6f:.15f)*h.Weight,0);
        public static Vector3 RClavAim(in Hold h)=>new Vector3(0,(h.LongGun?.9f:0)*h.Weight-h.Kick*.08f*h.Weight,0);
        private static Quaternion E(Vector3 e)=>CapturedRig.Euler(e.X,e.Y,e.Z);
        /// <summary>The chest-space carry orientation of the gun hand (rifle/MG barrel forward-down across the body, pistol
        /// muzzle-down low ready), with the stride's wobble added (radians).</summary>
        public static Quaternion CarryOrientation(bool longGun,float swayX,float swayY)=>CapturedRig.Euler((longGun?.3f:.62f)+swayX,(longGun?.38f:.18f)+swayY,longGun?-.35f:-.12f);
        /// <summary>The chest-space grip the lowered gun is carried at.</summary>
        public static readonly Vector3 CarryTarget=new Vector3(-.24f,-.02f,.25f);
        /// <summary>The whole hold over a rest pose (first-person view model, the gun viewer and the checks): the aimed torso
        /// with the pelvis square, then the arms and the gun.</summary>
        public static void Apply(CapturedRig.Pose p,Actor a,float time,float aimWeight)
        {
            var h=HoldOf(a,time,aimWeight);
            p.Rotation[CapturedRig.Pelvis]=Quaternion.Identity;
            p.Rotation[CapturedRig.Spine]=E(SpineAim(h));
            p.Rotation[CapturedRig.Chest]=E(ChestAim(h));
            p.Rotation[CapturedRig.Neck]=E(NeckAim(h));
            p.Rotation[CapturedRig.Head]=E(HeadAim(h));
            p.Rotation[CapturedRig.LClav]=E(LClavAim(h));
            p.Rotation[CapturedRig.RClav]=E(RClavAim(h));
            p.World();
            Arms(p,a,time,h,1,0,0,0);
        }
        // Scratch for the arms (presentation runs on one thread): no allocation per frame.
        private static readonly int[] ArmJoints={CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        private static readonly Quaternion[] SwingArms=new Quaternion[6];
        private static readonly Vector3[] ReloadPoints=new Vector3[14];
        /// <summary>
        /// The arms and the held gun over the posed torso (World() current): the gun hand braced on the aim in chest space
        /// (recoil, guard and the reload tuck), lowered toward the chest-space carry as the hold drops, and the support hand on
        /// the grip (or running the reload choreography). `brace` below 1 (the third-person pistol carry, ranged-motion.ts
        /// braceWeapon's weight) blends both arms from the arms already posed (the gait's swing) onto that hold; `carry` then
        /// turns the gun hand toward the carry orientation in chest space (orientHeld); swayX/swayY add the stride's wobble to
        /// the carry orientation. Apply passes (1, 0, 0, 0): the hold as it has always been. `carryTarget` / `carryAim` replace
        /// the lowered carry's grip and orientation (Core/NativeRangedLocomotion's run carries); null keeps CarryTarget and
        /// CarryOrientation.
        /// </summary>
        public static void Arms(CapturedRig.Pose p,Actor a,float time,in Hold h,float brace,float carry,float swayX,float swayY,Vector3? carryTarget=null,Quaternion? carryAim=null)
        {
            bool longGun=h.LongGun,mg=h.MachineGun;float kick=h.Kick,e=h.Envelope,u=h.Phase,tuck=h.Tuck,guard=h.Guard,hold=h.Weight,pitch=h.Pitch;
            if(brace<1)for(int i=0;i<6;i++)SwingArms[i]=p.Rotation[ArmJoints[i]];
            Quaternion aim=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest])*CapturedRig.Rot(CapturedRig.Y,-(a.AimYaw-a.Yaw)*Rad)*CapturedRig.Rot(CapturedRig.X,-pitch-kick*.13f);
            Vector3 target=longGun?new Vector3(-.11f,.245f,.155f)-Vector3.Transform(new Vector3(0,-.04f,-.30f),aim):new Vector3(-.035f,.215f,0)+Vector3.Transform(new Vector3(0,.075f,.37f),aim);
            target-=Vector3.Transform(Vector3.UnitZ,aim)*kick*(longGun?.045f:.035f);
            if(hold<1)
            {
                aim=Quaternion.Slerp(carryAim??CarryOrientation(longGun,swayX,swayY),aim,hold);
                target=Vector3.Lerp(carryTarget??CarryTarget,target,hold);
            }
            if(guard>0) { aim=Quaternion.Slerp(aim,CapturedRig.Euler(longGun?-1.05f:-1.25f,longGun?.55f:.1f,longGun?.75f:0),guard);target=Vector3.Lerp(target,new Vector3(-.1f,.18f,.28f),guard); }
            aim*=CapturedRig.Euler(-.25f*tuck,longGun?.1f*tuck:-.25f*tuck,.5f*tuck);
            target+=new Vector3(longGun?.05f:.09f,longGun?-.02f:-.04f,longGun?-.06f:-.12f)*tuck;
            CapturedRig.Arm(p,false,target,aim,new Vector3(-1,-.7f,-.2f));p.World();
            Quaternion chestInv=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]);
            Vector3 reached=Vector3.Transform(p.WorldPosition[CapturedRig.RHand]-p.WorldPosition[CapturedRig.Chest],chestInv);
            Vector3 start=reached+Vector3.Transform(Support(a.Weapon),aim),left=start;
            if(e>0)
            {
                Vector3 well=a.Weapon==WeaponId.Pistol?new Vector3(0,-.30f,-.07f):new Vector3(mg?.02f:0,-.27f,mg?.2f:.16f);
                Vector3 c0=a.Weapon==WeaponId.Pistol?new Vector3(.06f,.02f,-.04f):mg?new Vector3(0,.24f,.02f):new Vector3(-.12f,.06f,-.02f);
                Vector3 c1=a.Weapon==WeaponId.Pistol?new Vector3(.06f,.02f,-.1f):mg?new Vector3(0,.16f,.02f):new Vector3(-.12f,.06f,-.09f);
                Vector3 W(Vector3 v) => reached+Vector3.Transform(v,aim);
                Vector3 wp=W(well),strip=W(well+new Vector3(0,-.13f,-.02f)),pouch=new Vector3(.15f,-.33f,.07f);
                var pts=ReloadPoints;pts[0]=start;pts[1]=start;pts[2]=wp;pts[3]=strip;pts[4]=pouch;pts[5]=pouch;pts[6]=strip;pts[7]=wp;pts[8]=W(well+new Vector3(0,-.05f,0));pts[9]=wp;pts[10]=W(c0);pts[11]=W(c1);pts[12]=start;pts[13]=start;
                int k=1; while(k<HandTimes.Length-1&&u>HandTimes[k]) k++;
                left=Vector3.Lerp(start,Vector3.Lerp(pts[k-1],pts[k],CapturedClip.Ease((u-HandTimes[k-1])/(HandTimes[k]-HandTimes[k-1]))),Curve(ReachTimes,ReachValues,u)*e);
            }
            CapturedRig.Arm(p,true,left,aim,new Vector3(1,-.8f,-.25f));
            if(brace<1)
            {
                // ranged-motion.ts: the running arm pump stays under a partial brace, and the gun keeps a stable chest-space carry
                // orientation instead of windmilling with the forearm.
                for(int i=0;i<6;i++)p.Rotation[ArmJoints[i]]=Quaternion.Slerp(SwingArms[i],p.Rotation[ArmJoints[i]],Math.Max(0,brace));
                if(carry>0)
                {
                    p.World();Quaternion chest=p.WorldRotation[CapturedRig.Chest],held=Quaternion.Inverse(chest)*p.WorldRotation[CapturedRig.RHand];
                    p.Rotation[CapturedRig.RHand]=Quaternion.Inverse(p.WorldRotation[CapturedRig.RElbow])*chest*Quaternion.Slerp(held,CarryOrientation(longGun,swayX,swayY),Math.Min(1,carry));
                }
            }
            if(time<a.StunUntil) { p.Rotation[CapturedRig.Spine]*=CapturedRig.Euler(-.12f); p.Rotation[CapturedRig.Head]*=CapturedRig.Euler(.16f); }
            p.World();
        }
    }
}
