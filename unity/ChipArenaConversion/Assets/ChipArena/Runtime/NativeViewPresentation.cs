using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        public bool FirstPerson;
        public Camera ViewCamera;
        private CapturedAnimation.Player viewRig;
        private static List<Matrix4x4>[] MakeBatches()
        {var result=new List<Matrix4x4>[11];for(int i=0;i<result.Length;i++)result[i]=new List<Matrix4x4>();return result;}
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
        private void DrawStaff(CapturedAnimation.Player rig,int element)
        {
            var hand=rig.Bones[CapturedRig.LHand].localToWorldMatrix;int color=6+element;
            Part(hand,new Vector3(0,0,.28f),new Vector3(.075f,.075f,1.40f),Quaternion.identity,3);
            foreach(float z in new[]{-.38f,-.14f,.13f,.70f})Part(hand,new Vector3(0,0,z),new Vector3(.10f,.10f,.05f),Quaternion.identity,6);
            Part(hand,new Vector3(0,0,.95f),new Vector3(.24f,.24f,.28f),Quaternion.Euler(0,0,45),color);
            Part(hand,new Vector3(0,0,.97f),new Vector3(.10f,.10f,.33f),Quaternion.identity,4);
        }
        private void RenderFirstPerson(Snapshot snapshot,float time)
        {
            if(ViewCamera==null)return;Actor source=System.Array.Find(snapshot.Actors,a=>a.Id==LocalId&&a.Alive);if(source==null)return;
            foreach(var batch in batches)batch.Clear();
            if(viewRig==null)viewRig=captured.Create(transform,-999);
            var shown=source.ViewCopy();shown.Pitch=0;shown.Yaw=0;shown.AimYaw=0;
            // A camera-attached presentation rig never changes the authoritative pilot or muzzle.
            viewRig.ActorPose(shown,Vector3.zero,time,true);
            bool gun=GunRules.Ranged(shown.Weapon),magic=shown.MagicVisual>0;
            Vector3 offset=gun?new Vector3(.24f,-.48f,.20f):magic?new Vector3(.20f,-.50f,.30f):new Vector3(.16f,-.22f,.30f);
            float scale=.78f;
            if(gun&&shown.Aiming)scale*=Mathf.Tan(ViewCamera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Tan(NativeViewRules.FirstFov*Mathf.Deg2Rad*.5f);
            viewRig.Root.transform.SetPositionAndRotation(ViewCamera.transform.TransformPoint(offset),ViewCamera.transform.rotation);
            viewRig.Root.transform.localScale=Vector3.one*scale;
            if(gun&&shown.Aiming&&shown.ReloadUntil<=time&&time>=shown.BlockUntil)
            {
                var hand=viewRig.Bones[CapturedRig.RHand];float y=shown.Weapon==WeaponId.Pistol?.105f:shown.Weapon==WeaponId.Rifle?.175f:.25f;
                float z=shown.Weapon==WeaponId.Pistol?-.03f:shown.Weapon==WeaponId.Rifle?-.075f:-.02f;
                Vector3 rear=hand.TransformPoint(new Vector3(0,y,z));
                Quaternion align=Quaternion.FromToRotation(hand.forward,ViewCamera.transform.forward);
                viewRig.Root.transform.rotation=align*viewRig.Root.transform.rotation;
                rear=hand.TransformPoint(new Vector3(0,y,z));Vector3 local=ViewCamera.transform.InverseTransformPoint(rear);
                viewRig.Root.transform.position-=ViewCamera.transform.right*local.x+ViewCamera.transform.up*local.y;
            }
            PlayerState=viewRig.State;
            // A scoped rifle uses the centred reticle; its magnified housing would obscure the whole screen.
            if(!(gun&&shown.Weapon==WeaponId.Rifle&&shown.Aiming))
            {
                foreach(var box in CapturedRig.Boxes)if(NativeViewRules.ViewModelBone(box.Bone))
                    Part(viewRig.Bones[box.Bone].localToWorldMatrix,CapturedAnimation.Vec(CapturedRig.Mirror(box.Position)),CapturedAnimation.Vec(box.Size),Quaternion.identity,box.Color<0?0:box.Color);
                if(gun)GunPresentation.Draw(viewRig,shown,time,Add);
                else if(magic)DrawStaff(viewRig,shown.MagicVisual);
                else {Weapon(viewRig.Bones[CapturedRig.RHand].localToWorldMatrix,viewRig.Set,0);if(viewRig.Set==1)Weapon(viewRig.Bones[CapturedRig.LHand].localToWorldMatrix,1,0);}
            }
            DrawBatches(ViewCamera,10,false);
        }
    }
}
