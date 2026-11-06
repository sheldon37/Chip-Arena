using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // NOCTIEL's intro from the owner's reference clips on the rig (Core NativeFallenIntroMocap; docs/EXPEDITION-BOSS-FINAL.md section 19;
    // tools/motion). Every bone under the pelvis is turned to the clip's turn from its rest (its bind pose in the mesh's frame), parents
    // first, so no turn is left over from the clip pose or a blend: a bone the clips don't carry (spine_01, spine_04, the clavicles, the
    // twist bones) turns with the nearest one above it, and the fingers keep the clip's fist. The pelvis goes where the clip's root is,
    // on the water. Then the existing IK: the fist on the planted sword until the pull (FallenGrip, its weight the clip's plant), and
    // the left fist on the handle in the lunge. Blended in and out with the intro's weight, so the hand-back to the fight is as before.
    // Presentation only.
    public sealed partial class ArenaPresentation
    {
        [Serializable] private sealed class FbMocapFile{public string source;public float fps;public string[] bones,restBones;public int[] restParent;public float[] restPos,restRot,bladeInHand,gripInHand;public FbMocapClip[] clips;}
        [Serializable] private sealed class FbMocapClip{public string name,video;public int frames;public float[] q,root,grip,left,plant,two,blade,bladeErr;public int[] bodyHeld,swordFilled;}
        private static bool fbMocapRead;private static Quaternion fbMocapMesh=Quaternion.identity;
        private Transform[] fbMocapBone;private int[] fbMocapTurn;private Quaternion[] fbMocapRest;private int fbMocapPelvis=-1;private NativeFallenIntroMocap.Pose fbMocapPose;
        /// <summary>Bones the extracted intro set last frame (0 when it was not playing), for the DEV readout and the checks.</summary>
        internal int FallenMocapBones{get;private set;}

        /// <summary>Resources/FallenIntroMocap.json into Core's NativeFallenIntroMocap.Current (once). Without it the keyed intro plays.</summary>
        private static void FallenMocapRead()
        {
            if(fbMocapRead)return;fbMocapRead=true;var a=Resources.Load<TextAsset>("FallenIntroMocap");if(a==null){Debug.LogWarning("NOCTIEL: no extracted intro (FallenIntroMocap); the keyed intro plays");return;}
            try
            {
                var d=JsonUtility.FromJson<FbMocapFile>(a.text);var clips=new NativeFallenIntroMocap.Clip[d?.clips?.Length??0];
                for(int i=0;i<clips.Length;i++){var c=d.clips[i];clips[i]=new NativeFallenIntroMocap.Clip{Name=c.name,Video=c.video,Frames=c.frames,Q=c.q,Root=c.root,Grip=c.grip,Left=c.left,Plant=c.plant,Two=c.two,Blade=c.blade,BladeErr=c.bladeErr,BodyHeld=c.bodyHeld,SwordFilled=c.swordFilled};}
                var m=new NativeFallenIntroMocap(d?.bones,d?.fps??0,clips,d?.restBones,d?.restParent,d?.restPos,d?.restRot,d?.bladeInHand,d?.gripInHand);
                if(!m.Valid){Debug.LogWarning("NOCTIEL: the extracted intro is incomplete; the keyed intro plays");return;}
                NativeFallenIntroMocap.Current=m;
            }
            catch(Exception e){Debug.LogWarning("NOCTIEL: extracted intro: "+e.Message);}
            finally{Resources.UnloadAsset(a);}
        }

        // The rig's side, once with the body: each bone's rest (its bind pose in the mesh's frame) and which clip turn moves it.
        private void FallenMocapRig(SkinnedMeshRenderer most,Transform skeleton)
        {
            FallenMocapRead();var m=NativeFallenIntroMocap.Current;fbMocapBone=null;fbMocapPelvis=-1;if(m==null||most==null||most.sharedMesh==null)return;
            var rest=new Dictionary<string,Quaternion>(StringComparer.Ordinal);var bind=most.sharedMesh.bindposes;var bones=most.bones;
            for(int i=0;i<bones.Length&&i<bind.Length;i++)if(bones[i]!=null)rest[bones[i].name]=bind[i].inverse.rotation;
            fbMocapMesh=Quaternion.Inverse(fbRoot.transform.rotation)*most.transform.rotation;
            var list=new List<Transform>();var turn=new List<int>();var restTurn=new List<Quaternion>();
            foreach(var t in skeleton.GetComponentsInChildren<Transform>(true))
            {
                if(!rest.TryGetValue(t.name,out var r))continue;
                bool finger=false,under=false;for(var p=t.parent;p!=null;p=p.parent){if(p.name=="hand_l"||p.name=="hand_r")finger=true;if(p.name=="pelvis")under=true;}
                if(finger||(!under&&t.name!="pelvis"))continue;
                int k=-1;for(var p=t;p!=null&&k<0;p=p.parent)k=m.BoneIndex(p.name);if(k<0)continue;
                if(t.name=="pelvis")fbMocapPelvis=list.Count;list.Add(t);turn.Add(k);restTurn.Add(r);
            }
            if(fbMocapPelvis<0){Debug.LogWarning("NOCTIEL: the extracted intro needs the rig's pelvis; the keyed intro plays");NativeFallenIntroMocap.Current=null;return;}
            fbMocapBone=list.ToArray();fbMocapTurn=turn.ToArray();fbMocapRest=restTurn.ToArray();fbMocapPose=m.NewPose();
        }

        // The clip's body this frame: after the clip pose and the floor, before the feet, the grip and the head.
        private bool FallenMocapApply(in FallenIntroPose ip,float w,float age,Vector3 at,float floor,Quaternion yawRot)
        {
            var m=NativeFallenIntroMocap.Active;if(!ip.Mocap||m==null||fbMocapBone==null||w<=0)return false;
            if(!m.Sample(ip.MocapAt+age,fbMocapPose))return false;var pose=fbMocapPose;float s=NativeFallenBoss.Scale;var mesh=yawRot*fbMocapMesh;
            var hips=fbMocapBone[fbMocapPelvis];var place=new Vector3(at.x,floor,at.z)+mesh*(NativeBossPoseApply.ToUnity(pose.Root)*s);
            hips.position=w>=1?place:Vector3.Lerp(hips.position,place,w);
            for(int i=0;i<fbMocapBone.Length;i++)
            {
                var q=pose.Turn[fbMocapTurn[i]];var goal=mesh*new Quaternion(q.X,q.Y,q.Z,q.W)*fbMocapRest[i];
                fbMocapBone[i].rotation=w>=1?goal:Quaternion.Slerp(fbMocapBone[i].rotation,goal,w);
            }
            // The held kneel breathes (the keyed breath, small): a frame held for twenty seconds would look frozen.
            if(pose.Held&&ip.Breath!=0&&fbSpine!=null)fbSpine.rotation=Quaternion.AngleAxis(-1.2f*ip.Breath*w,yawRot*Vector3.right)*fbSpine.rotation;
            FallenMocapBones=fbMocapBone.Length;return true;
        }

        // The lunge's two-handed grip: the left fist on the handle under the right.
        private void FallenMocapLeftHand(float w)
        {
            if(fbMocapPose==null||!fbMocapPose.On||fbUpperL==null||fbLowerL==null||fbHandL==null||fbHandR==null)return;float k=fbMocapPose.Two*w;if(k<=0)return;float s=NativeFallenBoss.Scale;
            var way=fbHandR.rotation*FbBladeWay;var grip=fbHandR.position+fbHandR.rotation*(FbGrip*s);var goal=grip-way*(.24f*s);
            FallenLegTo(fbUpperL,fbLowerL,fbHandL,Vector3.Lerp(fbHandL.position,goal,k),fbHandL.rotation);
        }

        /// <summary>Where the planted sword's grip stands when the extracted intro plays (the reference's fist on it in the kneel),
        /// from the boss (world offset, unscaled origin); false when the keyed intro's plant point applies.</summary>
        private static bool FallenMocapPlant(FallenBossFrame f,out Vector3 point)
        {
            point=default;var m=NativeFallenIntroMocap.Active;if(m==null||!f.Intro.Mocap)return false;
            var g=m.PlantGrip;point=NativeBossPoseApply.ToUnity(f.Boss)+Quaternion.AngleAxis(f.Yaw,Vector3.up)*fbMocapMesh*(NativeBossPoseApply.ToUnity(g)*NativeFallenBoss.Scale);return true;
        }
    }
}
