using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The robot suit (Core/NativeRobotSuit) in the source-model bank: its parts on one instanced NativeSourceLit material, each
    // keeping the suit's bone index, authored on the pack body at rest (no mirror: they are in Unity's frame already).
    public sealed partial class NativeSourceModels
    {
        public const string Suit="pilot-suit";
        private void SuitModels()
        {
            var lit=Resources.Load<Shader>("NativeSourceLit");if(lit==null)return;
            var material=new Material(lit){enableInstancing=true,name="Robot suit"};material.SetInt("_Cull",2);material.SetFloat("_Ink",0);materials.Add(material);
            var sections=NativeRobotSuit.Build();var parts=new Part[sections.Length];
            for(int p=0;p<sections.Length;p++)
            {
                var s=sections[p];int n=s.Positions.Count;var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var emission=new List<Vector4>(n);
                for(int i=0;i<n;i++){var v=s.Positions[i];var nn=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];vertices[i]=new Vector3(v.X,v.Y,v.Z);normals[i]=new Vector3(nn.X,nn.Y,nn.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);emission.Add(new Vector4(e.X,e.Y,e.Z,e.W));}
                var t=s.Indices.ToArray();
                var mesh=new Mesh{name="Robot suit / "+s.Part,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=new Vector2[n];mesh.SetUVs(1,emission);mesh.triangles=t;mesh.RecalculateBounds();mesh.UploadMeshData(true);
                parts[p]=new Part{Name=s.Part,Bone=s.Bone,Mesh=mesh,Material=material,Core=CoreOf(vertices,normals,colors,emission,t)};
            }
            models[Suit]=parts;
        }
    }

    public sealed partial class ArenaPresentation
    {
        // A model pilot whose skin map picks the robot suit (its "suit" entry: NativeWeaponSkins.SuitEntry; the Locker's OUTFIT
        // section) wears it (owner 2026-10-30: "design clothing that resembles our previous player model (Robotic style)"): its
        // under-suit in place of its clothes (VisualLookOf), the plates on its bones,
        // each drawn with its bone's matrix times that bone's bind pose (read off the body's own skinned meshes), in the pilot's
        // body colour with its accent's light; the live body, the draft's seat and a corpse alike. The back emblem stays under
        // the back plate.
        // (The first-person arms are the local pilot's: they wear its under-suit's sleeves.)
        private bool WearsSuit(int id)=>NativeWeaponSkins.Index(SeatWeaponSkins(id==ViewBodyId?LocalId:id),NativeWeaponSkins.SuitEntry)==NativeWeaponSkins.SuitRobot;
        private sealed class SuitRig{public Transform[] Bones;public Matrix4x4[] Bind;}
        private readonly Dictionary<GameObject,SuitRig> suitRigs=new Dictionary<GameObject,SuitRig>();
        // The game joint each suit bone answers to (a pilot's per-joint reveal and tint: the spawn-in's, a corpse's fade).
        private static readonly int[] SuitJoints={CapturedRig.Chest,CapturedRig.Pelvis,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LKnee,CapturedRig.RKnee};
        private SuitRig SuitRigOf(GameObject root)
        {
            if(root==null)return null;if(suitRigs.TryGetValue(root,out var rig)&&(rig==null||rig.Bones[0]!=null))return rig;
            if(suitRigs.Count>64)suitRigs.Clear();
            int count=NativeRobotSuit.Bones.Length,found=0;rig=new SuitRig{Bones=new Transform[count],Bind=new Matrix4x4[count]};
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=skin.sharedMesh;if(mesh==null)continue;var bones=skin.bones;var bind=mesh.bindposes;
                for(int b=0;b<bones.Length&&b<bind.Length;b++){if(bones[b]==null)continue;int i=Array.IndexOf(NativeRobotSuit.Bones,bones[b].name);if(i>=0&&rig.Bones[i]==null){rig.Bones[i]=bones[b];rig.Bind[i]=bind[b];found++;}}
                if(found==count)break;
            }
            if(found<count)rig=null;suitRigs[root]=rig;return rig;
        }
        private readonly Matrix4x4[] suitMatrices=new Matrix4x4[NativeRobotSuit.Bones.Length];private Func<int,Matrix4x4> suitBone;
        private Func<int,float> suitReveal,suitRevealOf;private Func<int,Color> suitTint,suitTintOf;
        private void VisualSuit(GameObject root,int id,string bodyColor,string accentColor,int layer,Camera camera,Func<int,float> reveal,Func<int,Color> tint,float flash)
        {
            if(!WearsSuit(id)||sourceModels==null||!sourceModels.Has(NativeSourceModels.Suit))return;var rig=SuitRigOf(root);if(rig==null)return;
            for(int i=0;i<suitMatrices.Length;i++)suitMatrices[i]=rig.Bones[i].localToWorldMatrix*rig.Bind[i];
            suitBone??=i=>suitMatrices[i];suitReveal=reveal;suitTint=tint;
            sourceModels.Queue(NativeSourceModels.Suit,Matrix4x4.identity,suitBone,null,layer,camera,BodyTint(bodyColor),AccentTint(accentColor),1,
                reveal==null?null:suitRevealOf??=i=>suitReveal(SuitJoints[i]),tint==null?null:suitTintOf??=i=>suitTint(SuitJoints[i]),new Vector4(1,1,1,1),null,flash);
        }
    }
}
