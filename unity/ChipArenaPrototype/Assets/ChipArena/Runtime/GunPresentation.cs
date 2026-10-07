using System;
using UnityEngine;

namespace ChipArena.UnityPrototype
{
    // Low-detail source weapon-visuals.ts shapes. Geometry lives at the actual posed wrist.
    public static class GunPresentation
    {
        public static void Draw(CapturedAnimation.Player rig,Actor actor,float time,Action<int,Matrix4x4> add)
        {
            WeaponId id=actor.Weapon; bool pistol=id==WeaponId.Pistol,rifle=id==WeaponId.Rifle,mg=id==WeaponId.MachineGun;
            Matrix4x4 gun=rig.Bones[CapturedRig.RHand].localToWorldMatrix,left=rig.Bones[CapturedRig.LHand].localToWorldMatrix;
            void Box(Matrix4x4 parent,int color,float x,float y,float z,float sx,float sy,float sz,float rx=0)
            { add(color,parent*Matrix4x4.TRS(new Vector3(-x,y,z),Quaternion.Euler(rx*Mathf.Rad2Deg,0,0),new Vector3(sx,sy,sz))); }
            float envelope,u=RangedPose.ReloadPhase(actor,time,out envelope),kick=RangedPose.Recoil(id,time-actor.LastShot);
            if(pistol)
            {
                float slide=-.04f*Mathf.Max(0,kick);
                if(u>.83f&&u<.94f) slide-=.04f*Mathf.Sin((u-.83f)/.11f*Mathf.PI)*envelope;
                Box(gun,5,0,.035f,.14f+slide,.125f,.10f,.40f); Box(gun,3,0,-.045f,.12f,.11f,.06f,.30f);
                Box(gun,3,0,0,.40f,.06f,.06f,.14f); Box(gun,5,0,0,.465f,.08f,.08f,.03f);
                Box(gun,3,0,-.14f,-.045f,.11f,.22f,.12f,.22f); Box(gun,5,0,-.105f,.07f,.03f,.022f,.12f);
                Box(gun,4,0,.095f,.32f,.022f,.02f,.025f); Box(gun,4,0,.095f,-.03f,.07f,.02f,.025f);
            }
            else
            {
                Box(gun,3,0,mg?.01f:0,.09f,mg?.17f:.15f,mg?.22f:.16f,mg?.44f:.40f);
                Box(gun,5,0,mg?.13f:.09f,.09f,mg?.15f:.10f,.025f,mg?.42f:.40f);
                Box(gun,3,0,-.13f,-.035f,.11f,.22f,.11f,.2f); Box(gun,5,0,-.095f,.06f,.03f,.02f,.10f);
                Box(gun,3,0,-.03f,-.20f,.12f,.15f,.20f); Box(gun,3,0,-.045f,-.295f,.13f,.19f,.03f);
                if(rifle)
                {
                    Box(gun,3,0,0,.745f,.06f,.06f,.85f); Box(gun,5,0,-.005f,.46f,.12f,.12f,.32f);
                    Box(gun,6,0,0,1.16f,.11f,.085f,.10f); Box(gun,5,0,-.075f,.74f,.06f,.02f,.26f);
                    Box(gun,3,0,.175f,.12f,.085f,.085f,.36f); Box(gun,5,0,.12f,.12f,.035f,.03f,.24f);
                    Box(gun,6,0,.175f,.32f,.13f,.13f,.07f); Box(gun,0,0,.175f,.357f,.09f,.09f,.012f);
                    Box(gun,3,0,.175f,-.075f,.10f,.10f,.05f); Box(gun,5,-.11f,.035f,-.02f,.12f,.03f,.03f);
                    Box(gun,5,-.17f,.035f,-.02f,.045f,.045f,.045f); Box(gun,3,0,.055f,-.19f,.08f,.04f,.14f);
                }
                else
                {
                    Box(gun,3,0,.01f,.48f,.11f,.11f,.40f); Box(gun,5,0,.01f,.40f,.115f,.035f,.06f); Box(gun,5,0,.01f,.52f,.115f,.035f,.06f);
                    Box(gun,3,0,0,.77f,.05f,.05f,.18f); Box(gun,6,0,0,.865f,.10f,.10f,.07f);
                    Box(gun,5,0,.235f,.09f,.035f,.03f,.22f); Box(gun,5,0,.19f,0,.03f,.07f,.03f); Box(gun,5,0,.19f,.18f,.03f,.07f,.03f);
                }
            }
            Vector3 rest=CapturedAnimation.Vec(CapturedRig.Mirror(RangedPose.Magazine(id)));
            Matrix4x4 mag=gun*Matrix4x4.TRS(rest,Quaternion.identity,Vector3.one); bool visible=true;
            if(envelope>0)
            {
                if(u>=.2f&&u<.3f) mag=gun*Matrix4x4.TRS(rest+new Vector3(0,-.13f*CapturedClip.Ease((u-.2f)/.1f),0),Quaternion.identity,Vector3.one);
                else if(u>=.3f&&u<.5f)
                {
                    float fall=(u-.3f)*GunRules.Get(id).Reload; visible=fall<=.35f;
                    mag=gun*Matrix4x4.TRS(rest+new Vector3(.18f*fall,-.13f-4.9f*fall*fall-.6f*fall,-.02f),Quaternion.Euler(fall*280,0,fall*170),Vector3.one);
                }
                else if(u>=.5f&&u<.68f)
                {
                    Vector3 from=left.MultiplyPoint3x4(new Vector3(0,-.1f,.02f)),to=gun.MultiplyPoint3x4(rest);
                    mag=Matrix4x4.TRS(Vector3.Lerp(from,to,CapturedClip.Ease((u-.6f)/.08f)),Quaternion.Slerp(left.rotation,gun.rotation,CapturedClip.Ease((u-.6f)/.08f)),Vector3.one);
                }
            }
            if(visible) Box(mag,mg?6:5,0,0,mg?.04f:0,pistol?.095f:mg?.10f:.08f,pistol?.13f:mg?.24f:.22f,pistol?.10f:mg?.22f:.15f);
            float age=time-actor.LastShot;
            if(age>=0&&age<.065f&&actor.ReloadUntil<=time)
            {
                float size=1-age/.065f,muzzle=GunRules.Get(id).Muzzle;
                Box(gun,6,0,0,muzzle+.10f,.14f*size,.14f*size,.30f*size);
                Box(gun,4,0,0,muzzle+.08f,.05f*size,.05f*size,.22f*size);
            }
        }
    }
}
