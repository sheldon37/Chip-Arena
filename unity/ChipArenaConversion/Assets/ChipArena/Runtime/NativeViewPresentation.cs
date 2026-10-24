using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        public bool FirstPerson,AimHeld;
        private readonly NativeAimMotion aimMotion=new NativeAimMotion();
        public bool ScopeActive=>viewRig!=null&&NativeAimPresentation.Scope(FirstPerson,AimHeld,viewRig.Weapon,aimMotion.Weight);
        public Camera ViewCamera;
        private CapturedAnimation.Player viewRig;
        private static List<Matrix4x4>[] MakeBatches()
        {var result=new List<Matrix4x4>[12];for(int i=0;i<result.Length;i++)result[i]=new List<Matrix4x4>();return result;}
        private void DrawBatches(Camera camera,int layer,bool shadows)
        {
            for(int k=0;k<batches.Length;k++)
            {
                var list=batches[k];
                for(int start=0;start<list.Count;start+=1023)
                {
                    int count=Mathf.Min(1023,list.Count-start);list.CopyTo(start,drawBuffer,0,count);bool drawn=false;
                    if(UsesInstancing)
                    {
                        try {Graphics.DrawMeshInstanced(cube,0,materials[k],drawBuffer,count,null,shadows?ShadowCastingMode.On:ShadowCastingMode.Off,shadows,layer,camera);drawn=true;}
                        catch(System.InvalidOperationException e){UseCompatibility(e.Message);}
                        catch(System.ArgumentException e){UseCompatibility(e.Message);}
                    }
                    if(!drawn)for(int j=0;j<count;j++)Graphics.DrawMesh(cube,drawBuffer[j],compatibilityMaterials[k],layer,camera,0,null,shadows?ShadowCastingMode.On:ShadowCastingMode.Off,shadows);
                }
            }
        }
        private Matrix4x4 StaffMatrix(CapturedAnimation.Player rig,bool firstPerson)
        {
            var wrist=rig.Bones[CapturedRig.LHand];var hand=wrist.localToWorldMatrix;
            if(firstPerson)
            {
                float guard=rig.MagicGuardWeight;
                var rotation=NativeFirstPersonPose.StaffRotation(CapturedAnimation.Num(wrist.rotation),CapturedAnimation.Num(ViewCamera.transform.forward),guard);
                hand=Matrix4x4.TRS(wrist.position,CapturedAnimation.Quat(rotation),wrist.lossyScale*NativeFirstPersonPose.StaffSize(guard));
            }
            return hand;
        }
        private void DrawStaff(CapturedAnimation.Player rig,int element,bool firstPerson=false)
        {
            var hand=StaffMatrix(rig,firstPerson);
            foreach(var part in NativeStaffGeometry.Parts[element-1])
                Part(hand,CapturedAnimation.Vec(CapturedRig.Mirror(part.Position)),CapturedAnimation.Vec(part.Size),CapturedAnimation.Quat(CapturedRig.Mirror(part.Rotation)),part.Color);
        }
        private void RenderFirstPerson(Snapshot snapshot,float time)
        {
            if(ViewCamera==null)return;Actor source=System.Array.Find(snapshot.Actors,a=>a.Id==LocalId&&a.Alive);if(source==null)return;
            foreach(var batch in batches)batch.Clear();
            if(viewRig==null)viewRig=captured.Create(transform,-999);
            VisualViewRestore();
            var shown=source.ViewCopy();shown.Aiming=AimHeld;shown.Pitch=0;shown.Yaw=0;shown.AimYaw=0;shown.Grounded=SpawnContact(shown.Id,shown.Grounded);
            // A camera-attached presentation rig never changes the authoritative pilot or muzzle.
            viewRig.PreviewAirJumpAt=rigs.TryGetValue(LocalId,out var own)?own.PreviewAirJumpAt:-100;// the Dev Sandbox's Cyber Lord boost preview
            viewRig.ViewPitch=source.Pitch;viewRig.ActorPose(shown,Vector3.zero,time,true);
            // Every magic-class pilot (first-person-view.ts cls 'magic': the four schools and the L33t H4x staff) frames the staff view.
            int school=MagicPose.School(shown);bool gun=GunRules.Ranged(shown.Weapon),magic=school>0,bow=NativeBowWire.Is(shown.SourceSpec);
            // first-person-view.ts:69 class bases (NativeViewBase): guns right of centre, the DEV fighters' fists centred and low
            // (their view rig ends on the guard clip, NativeSourceMotion.Pilot), melee raised.
            // The bow (Core/NativeBowPose: its rig is placed so the arrow line runs under and left of the eye).
            Vector3 offset=bow?NativeBowView(true):gun?CapturedAnimation.Vec(NativeViewBase.Gun):magic?CapturedAnimation.Vec(NativeFirstPersonPose.MagicViewOffset(viewRig.MagicGuardWeight)):CapturedAnimation.Vec(NativeViewBase.IsFighter(shown.SourceSpec)?NativeViewBase.Fighter:NativeViewBase.Melee);
            float ads=aimMotion.Sample(time,gun&&AimHeld);
            if(gun)offset+=CapturedAnimation.Vec(NativeAimPresentation.Offset(shown.Weapon))*ads;
            float scale=bow?NativeBowPose.ViewScale:gun?NativeAimPresentation.Scale(ViewCamera.fieldOfView,ads):NativeFirstPersonPose.Scale;
            offset+=HitViewDip(source,time,out float dip);
            viewRig.Root.transform.SetPositionAndRotation(ViewCamera.transform.TransformPoint(offset),ViewCamera.transform.rotation*Quaternion.Euler(dip,0,0));
            viewRig.Root.transform.localScale=Vector3.one*scale;
            if(gun&&ads>0&&shown.ReloadUntil<=time&&time>=shown.BlockUntil&&!shown.Rolling(time)&&time>=shown.StunUntil)
            {
                var hand=viewRig.Bones[CapturedRig.RHand];float y=shown.Weapon==WeaponId.Pistol?.105f:shown.Weapon==WeaponId.Rifle?.175f:.25f;
                float z=shown.Weapon==WeaponId.Pistol?-.03f:shown.Weapon==WeaponId.Rifle?-.075f:-.02f;
                Vector3 rear=hand.TransformPoint(new Vector3(0,y,z));
                Quaternion align=Quaternion.FromToRotation(hand.forward,ViewCamera.transform.forward);
                viewRig.Root.transform.rotation=Quaternion.Slerp(viewRig.Root.transform.rotation,align*viewRig.Root.transform.rotation,ads);
                rear=hand.TransformPoint(new Vector3(0,y,z));Vector3 local=ViewCamera.transform.InverseTransformPoint(rear);
                viewRig.Root.transform.position-=(ViewCamera.transform.right*local.x+ViewCamera.transform.up*local.y)*ads;
            }
            ApplyViewSway(shown,time,ads);
            // PvE chip selection: the raised wrist and its hologram in view (NativeChipSelectPresentation.cs).
            ChipSelectView(viewRig,source,time);
            if(shown.SourceSpec=="siren")SirenViewArms(shown,time);
            // The visual body's forearms and hands in place of the voxel arms (NativeVisualBodyPresentation.cs).
            VisualViewPose(viewRig,shown,time,ScopeActive);
            PlayerState=viewRig.State;
            // A scoped rifle uses the centred reticle; its magnified housing would obscure the whole screen.
            if(!ScopeActive&&!SourceCharacter(viewRig,shown,time,true))
            {
                foreach(var box in CapturedRig.Boxes)if(NativeViewRules.ViewModelBone(box.Bone))
                    Part(viewRig.Bones[box.Bone].localToWorldMatrix,CapturedAnimation.Vec(CapturedRig.Mirror(box.Position)),CapturedAnimation.Vec(box.Size),Quaternion.identity,box.Color<0?0:box.Color);
                if(gun)GunPresentation.Draw(viewRig,shown,time,Add);
                else if(magic)DrawStaff(viewRig,school==MagicPose.Leet?1:school,true);
                else {if(!shown.WeaponAway&&!shown.MainWeaponAway)Weapon(MeleeHand(viewRig,shown,time),viewRig.Set,0);if(viewRig.Set==1)Weapon(MeleeHand(viewRig,shown,time,true),1,0);}
            }
            DrawBatches(ViewCamera,10,false);
        }
    }
}
