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
        public static void Apply(CapturedRig.Pose p,Actor a,float time,float aimWeight)
        {
            bool longGun=a.Weapon!=WeaponId.Pistol,mg=a.Weapon==WeaponId.MachineGun;
            float kick=Recoil(a.Weapon,time-a.LastShot),e,u=ReloadPhase(a,time,out e),tuck=Curve(TuckTimes,TuckValues,u)*e;
            float guard=time<a.BlockUntil?CapturedClip.Ease((time-a.BlockUntil+Rules.BlockDuration)/.07f):0;
            float hold=aimWeight+(1-aimWeight)*guard,blade=(longGun?.75f:.04f)*hold*(1-guard),pitch=a.Pitch*Rad;
            p.Rotation[CapturedRig.Pelvis]=Quaternion.Identity;
            p.Rotation[CapturedRig.Spine]=CapturedRig.Euler(-pitch*.22f*hold+guard*.12f,-blade*.45f);
            p.Rotation[CapturedRig.Chest]=CapturedRig.Euler(-pitch*.18f*hold-kick*.05f*hold+tuck*.06f,-blade*.55f+tuck*.2f);
            p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(-pitch*.10f*hold+(longGun?.15f:0),0,longGun?.12f:0);
            p.Rotation[CapturedRig.Head]=CapturedRig.Euler(pitch*.5f*hold+(longGun?.12f:0)+kick*.05f+tuck*.25f,blade,longGun?.06f:-.08f);
            p.Rotation[CapturedRig.LClav]=CapturedRig.Euler(0,-(longGun?.6f:.15f)*hold);
            p.Rotation[CapturedRig.RClav]=CapturedRig.Euler(0,(longGun?.9f:0)*hold-kick*.08f*hold);
            p.World();
            Quaternion aim=Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest])*CapturedRig.Rot(CapturedRig.Y,-(a.AimYaw-a.Yaw)*Rad)*CapturedRig.Rot(CapturedRig.X,-pitch-kick*.13f);
            Vector3 target=longGun?new Vector3(-.11f,.245f,.155f)-Vector3.Transform(new Vector3(0,-.04f,-.30f),aim):new Vector3(-.035f,.215f,0)+Vector3.Transform(new Vector3(0,.075f,.37f),aim);
            target-=Vector3.Transform(Vector3.UnitZ,aim)*kick*(longGun?.045f:.035f);
            if(hold<1)
            {
                aim=Quaternion.Slerp(CapturedRig.Euler(longGun?.3f:.62f,longGun?.38f:.18f,longGun?-.35f:-.12f),aim,hold);
                target=Vector3.Lerp(new Vector3(-.24f,-.02f,.25f),target,hold);
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
                Vector3[] pts={start,start,wp,strip,pouch,pouch,strip,wp,W(well+new Vector3(0,-.05f,0)),wp,W(c0),W(c1),start,start};
                int k=1; while(k<HandTimes.Length-1&&u>HandTimes[k]) k++;
                left=Vector3.Lerp(start,Vector3.Lerp(pts[k-1],pts[k],CapturedClip.Ease((u-HandTimes[k-1])/(HandTimes[k]-HandTimes[k-1]))),Curve(ReachTimes,ReachValues,u)*e);
            }
            CapturedRig.Arm(p,true,left,aim,new Vector3(1,-.8f,-.25f));
            if(time<a.StunUntil) { p.Rotation[CapturedRig.Spine]*=CapturedRig.Euler(-.12f); p.Rotation[CapturedRig.Head]*=CapturedRig.Euler(.16f); }
            p.World();
        }
    }
}
