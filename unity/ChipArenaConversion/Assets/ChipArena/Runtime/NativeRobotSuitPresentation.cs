using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The robot suit (Core/NativeRobotSuit: the robot's four clothing pieces' plates) in the source-model bank: its parts on one
    // instanced NativeSourceLit material, each keeping the suit's bone index, authored on the pack body at rest (no mirror: they
    // are in Unity's frame already).
    public sealed partial class NativeSourceModels
    {
        public const string Suit="pilot-suit";
        /// <summary>The plates fitted to the female body at a level (Core/NativeBodyKind; ArenaPresentation SuitFemaleEnsure).</summary>
        public const string SuitFemale="pilot-suit-female";
        public static string SuitFemaleOf(int level)=>SuitFemale+"-"+level;
        private Material suitMaterial;
        private void SuitModels()
        {
            var lit=Resources.Load<Shader>("NativeSourceLit");if(lit==null)return;
            var material=new Material(lit){enableInstancing=true,name="Robot suit"};material.SetInt("_Cull",2);material.SetFloat("_Ink",0);materials.Add(material);suitMaterial=material;
            models[Suit]=SuitParts(material,null);
        }
        // The suit's parts, each vertex moved by `carry` (its suit bone, its position: where it goes) when given.
        private static Part[] SuitParts(Material material,Func<int,Vector3,Vector3> carry)
        {
            var sections=NativeRobotSuit.Build();var parts=new Part[sections.Length];
            for(int p=0;p<sections.Length;p++)
            {
                var s=sections[p];int n=s.Positions.Count;var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var emission=new List<Vector4>(n);
                for(int i=0;i<n;i++){var v=s.Positions[i];var nn=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];vertices[i]=new Vector3(v.X,v.Y,v.Z);normals[i]=new Vector3(nn.X,nn.Y,nn.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);emission.Add(new Vector4(e.X,e.Y,e.Z,e.W));}
                if(carry!=null)for(int i=0;i<n;i++)vertices[i]=carry(s.Bone,vertices[i]);
                var t=s.Indices.ToArray();
                var mesh=new Mesh{name="Robot suit / "+s.Part+(carry!=null?" (female)":""),indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=new Vector2[n];mesh.SetUVs(1,emission);mesh.triangles=t;mesh.RecalculateBounds();mesh.UploadMeshData(true);
                parts[p]=new Part{Name=s.Part,Bone=s.Bone,Mesh=mesh,Material=material,Core=CoreOf(vertices,normals,colors,emission,t)};
            }
            return parts;
        }
        /// <summary>The suit fitted to the female body: today's plates, each vertex where `carry` moves it (the garment under it, by
        /// that garment's own shape deltas), on the same instanced material. Made once.</summary>
        public void SuitFemaleModels(string id,Func<int,Vector3,Vector3> carry)
        {
            if(suitMaterial==null||carry==null||models.ContainsKey(id))return;models[id]=SuitParts(suitMaterial,carry);
        }
    }

    public sealed partial class ArenaPresentation
    {
        // A model pilot's robot pieces (Core/NativeRobotSuit; owner 2026-10-30: "design clothing that resembles our previous
        // player model (Robotic style)"; four clothing pieces, each its slot's default, since owner 2026-11-01: "Can we make the
        // armor portion into clothing? Like separate them like we have other clothes currently."; the Locker's OUTFIT rows): each
        // one worn over its garment (VisualLookOf: NativeRobotSuit.Dress), its plates on their bones, each drawn with its bone's
        // matrix times that bone's bind pose (read off the body's own skinned meshes), in the pilot's body colour with its
        // accent's light; the live body, the draft's seat and a corpse alike. The back emblem stays under the armour's back
        // plate. Under the robot's helmet or half mask (Core NativeRobotHelm, its own ear modules) the armour's ear modules are
        // left off, and under arm bands its vambraces (NativeRobotSuit.Shown). A corpse keeps them (VisualCorpseSuit).
        // (The first-person arms are the local pilot's: they wear the armour's sweater sleeves and its gauntlets' gloves.)
        private int SuitShown(int id)=>NativeRobotSuit.Shown(VisualOutfitOf(id),HelmEars(id));
        private bool WearsArmour(int id)=>NativeRobotSuit.Worn(VisualOutfitOf(id),NativeRobotSuit.BoneSlot[NativeRobotSuit.Chest]);
        private sealed class SuitRig{public Transform[] Bones;public Matrix4x4[] Bind;public int Found;}
        private readonly Dictionary<GameObject,SuitRig> suitRigs=new Dictionary<GameObject,SuitRig>();
        // The game joint each suit bone answers to (a pilot's per-joint reveal and tint: the spawn-in's, a corpse's fade).
        private static readonly int[] SuitJoints={CapturedRig.Chest,CapturedRig.Pelvis,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LKnee,CapturedRig.RKnee,
            CapturedRig.LHand,CapturedRig.RHand,CapturedRig.LFoot,CapturedRig.RFoot};
        // The plates a corpse keeps (owner 2026-11-01: "Death is removing suits"; NativeVisualBodyPresentation.cs, the corpse's
        // frame): a bit per suit bone; a crushed head drops its own, and each half of a split body keeps those on its side of
        // the cut (the bisect's HalfOf, as the helmet's VisualCorpseHead); while the cut is being made the first half draws the
        // whole body and keeps them all, the second none (Core NativeSplitShow).
        private int VisualCorpseSuit(NativeCorpse corpse)
        {
            int keep=-1;if(corpse.IsCrushed(CapturedRig.Head))keep&=~(1<<NativeRobotSuit.Head);
            var first=corpse.Host??corpse;if(first.Twin==null)return corpse.Host==null?keep:0;
            if(!(first.Bisect is BisectFx fx)||fx.Request==null)return 0;var stage=VisualSplitStage(first);int mine=corpse.Host==null?0:1;
            for(int b=0;b<SuitJoints.Length;b++){int j=SuitJoints[b],half=fx.Request.HalfOf(j,CutNum(first.Bind[j].GetColumn(3)));if(!NativeSplitShow.Wears(mine,half==1?1:0,stage))keep&=~(1<<b);}
            return keep;
        }
        // A body's suit bones (Found: a bit each one found; a piece whose bone the body lacks is left off).
        private SuitRig SuitRigOf(GameObject root)
        {
            if(root==null)return null;if(suitRigs.TryGetValue(root,out var rig)&&(rig==null||rig.Bones[0]!=null))return rig;
            if(suitRigs.Count>64)suitRigs.Clear();
            int count=NativeRobotSuit.Bones.Length,all=(1<<count)-1;rig=new SuitRig{Bones=new Transform[count],Bind=new Matrix4x4[count]};
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=skin.sharedMesh;if(mesh==null)continue;var bones=skin.bones;var bind=mesh.bindposes;
                for(int b=0;b<bones.Length&&b<bind.Length;b++){if(bones[b]==null)continue;int i=Array.IndexOf(NativeRobotSuit.Bones,bones[b].name);if(i>=0&&rig.Bones[i]==null){rig.Bones[i]=bones[b];rig.Bind[i]=bind[b];rig.Found|=1<<i;}}
                if(rig.Found==all)break;
            }
            if(rig.Bones[0]==null)rig=null;suitRigs[root]=rig;return rig;
        }
        private readonly Matrix4x4[] suitMatrices=new Matrix4x4[NativeRobotSuit.Bones.Length];private Func<int,Matrix4x4> suitBone;
        private Func<int,float> suitReveal,suitRevealOf;private Func<int,Color> suitTint,suitTintOf;
        private int suitShown;private Func<int,bool> suitVisible;
        // `accentOverride` and `glowGain`: the draft's install runs its light through the trims bone by bone (DraftSuitSurge).
        private void VisualSuit(GameObject root,int id,string bodyColor,string accentColor,int layer,Camera camera,Func<int,float> reveal,Func<int,Color> tint,float flash,int keep=-1,Color? accentOverride=null,float glowGain=1)
        {
            int shown=SuitShown(id)&keep;if(shown==0||sourceModels==null||!sourceModels.Has(NativeSourceModels.Suit))return;var rig=SuitRigOf(root);if(rig==null||(shown&=rig.Found)==0)return;
            for(int i=0;i<suitMatrices.Length;i++)suitMatrices[i]=rig.Bones[i]!=null?rig.Bones[i].localToWorldMatrix*rig.Bind[i]:Matrix4x4.identity;
            suitBone??=i=>suitMatrices[i];suitReveal=reveal;suitTint=tint;suitShown=shown;suitVisible??=b=>(suitShown>>b&1)!=0;
            // (On the female body, the plates fitted to it: NativeBodyKindPresentation.cs.)
            sourceModels.Queue(SuitModelOf(root,rig),Matrix4x4.identity,suitBone,suitVisible,layer,camera,BodyTint(bodyColor),accentOverride??AccentTint(accentColor),1,
                reveal==null?null:suitRevealOf??=i=>suitReveal(SuitJoints[i]),tint==null?null:suitTintOf??=i=>suitTint(SuitJoints[i]),new Vector4(glowGain,glowGain,glowGain,1),null,flash);
        }
    }
}
