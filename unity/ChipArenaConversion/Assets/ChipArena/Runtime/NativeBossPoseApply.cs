using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Puts a Core <see cref="BossPose"/> (docs/BOSS-MOTION.md) on a boss model's parts: part i takes joint i's world position and
    /// rotation. Core works in Unity's axes with System.Numerics types, so the conversion is the type only. Parts are set in index
    /// order, so in a hierarchy a parent (a lower index) is placed before its children; null parts are skipped. Presentation only,
    /// no allocation.
    /// </summary>
    public static class NativeBossPoseApply
    {
        public static Vector3 ToUnity(System.Numerics.Vector3 v)=>new Vector3(v.X,v.Y,v.Z);
        public static Quaternion ToUnity(System.Numerics.Quaternion q)=>new Quaternion(q.X,q.Y,q.Z,q.W);
        public static void Apply(BossPose pose,Transform[] parts)
        {
            if(pose==null||parts==null)return;int n=Mathf.Min(pose.Count,parts.Length);
            for(int i=0;i<n;i++){var t=parts[i];if(t!=null)t.SetPositionAndRotation(ToUnity(pose.Position[i]),ToUnity(pose.Rotation[i]));}
        }
        /// <summary>Joint i as a matrix for a part hung off it (its centre `offset` in the joint's frame, its `scale`): for drawing
        /// instanced parts without transforms (Graphics.DrawMesh / RenderMeshInstanced).</summary>
        public static Matrix4x4 Matrix(BossPose pose,int i,Vector3 offset,Vector3 scale)
        {
            var r=ToUnity(pose.Rotation[i]);return Matrix4x4.TRS(ToUnity(pose.Position[i])+r*offset,r,scale);
        }
    }
}
