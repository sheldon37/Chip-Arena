using UnityEngine;
using N = System.Numerics;

namespace ChipArena.UnityConversion
{
    // Binds NativeVisualRig to a skinned character's bone hierarchy (docs/BOZO-INTEGRATION.md step 3). Reads the rest pose
    // once, mirrored into the source frame, and writes each frame's visual pose back as local rotations (and the pelvis's
    // position). Unity types only: no asset-pack types, so it compiles on machines without the packs. Presentation only.
    public sealed class NativeVisualRigBinding
    {
        public readonly NativeVisualRig.Rig Rig;
        public readonly NativeVisualRig.State State;
        public readonly Transform[] Bones;
        private readonly int pelvis;

        // origin: the character's floor point (soles at y 0, front +z); skeleton: the root of its bone hierarchy.
        public NativeVisualRigBinding(Transform origin,Transform skeleton)
        {
            Bones=skeleton.GetComponentsInChildren<Transform>(true);
            int n=Bones.Length; var names=new string[n]; var parents=new int[n]; var positions=new N.Vector3[n]; var rotations=new N.Quaternion[n];
            for(int v=0;v<n;v++)
            {
                var t=Bones[v]; names[v]=t.name; parents[v]=-1;
                for(int p=v-1;p>=0;p--) if(Bones[p]==t.parent) { parents[v]=p; break; }
                Vector3 position; Quaternion rotation;
                if(parents[v]<0)
                {
                    position=origin.InverseTransformPoint(t.position)+new Vector3(0,NativeVisualRig.Floor,0);
                    rotation=Quaternion.Inverse(origin.rotation)*t.rotation;
                }
                else { position=t.localPosition; rotation=t.localRotation; }
                positions[v]=CapturedRig.Mirror(new N.Vector3(position.x,position.y,position.z));
                rotations[v]=CapturedRig.Mirror(new N.Quaternion(rotation.x,rotation.y,rotation.z,rotation.w));
            }
            Rig=new NativeVisualRig.Rig(NativeVisualRig.Mannequin(names,parents,positions,rotations));
            State=new NativeVisualRig.State(Rig);
            pelvis=Rig.Of[CapturedRig.Pelvis];
        }

        // Poses the bones from a game pose (pose.World() already run). See NativeVisualRig.Pose for the flags.
        public void Apply(CapturedRig.Pose pose,float support,bool plantFeet,bool fitFloor)
        {
            NativeVisualRig.Pose(State,pose,support,plantFeet,fitFloor);
            for(int v=1;v<Bones.Length;v++)
            {
                if(Rig.Parent[v]<0) continue;
                var q=CapturedRig.Mirror(State.Local[v]);
                Bones[v].localRotation=new Quaternion(q.X,q.Y,q.Z,q.W);
                if(v==pelvis)
                {
                    var p=CapturedRig.Mirror(State.LocalPosition[v]);
                    Bones[v].localPosition=new Vector3(p.X,p.Y,p.Z);
                }
            }
        }

        // The weapon frame on the visual right hand (P1), in the origin's space (Unity frame, floor at y 0). Weapons draw at
        // this frame in place of the game wrist's; the grip offset is in their geometry.
        public void WeaponFrame(CapturedRig.Pose pose,out Vector3 position,out Quaternion rotation)
        {
            NativeVisualRig.WeaponFrame(State,pose,out var p,out var q);
            p=CapturedRig.Mirror(p); q=CapturedRig.Mirror(q);
            position=new Vector3(p.X,p.Y-NativeVisualRig.Floor,p.Z); rotation=new Quaternion(q.X,q.Y,q.Z,q.W);
        }
    }
}
