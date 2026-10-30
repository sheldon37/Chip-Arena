using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The robot's helmet and half mask (Core/NativeRobotHelm) in the source-model bank: each one model on one instanced
    // NativeSourceLit material, its part on the head (authored in the head bone's frame, Unity's: no mirror).
    public sealed partial class NativeSourceModels
    {
        public const string Helm="pilot-helm",HalfMask="pilot-half-mask";
        private void HelmModels()
        {
            var lit=Resources.Load<Shader>("NativeSourceLit");if(lit==null)return;
            var material=new Material(lit){enableInstancing=true,name="Robot helmet"};material.SetInt("_Cull",2);material.SetFloat("_Ink",0);materials.Add(material);
            models[Helm]=HelmParts(NativeRobotHelm.Helm(),material,"Robot helmet");models[HalfMask]=HelmParts(NativeRobotHelm.Half(),material,"Robot half mask");
        }
        private Part[] HelmParts(SheldonSection[] sections,Material material,string name)
        {
            var parts=new Part[sections.Length];
            for(int p=0;p<sections.Length;p++)
            {
                var s=sections[p];int n=s.Positions.Count;var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var emission=new List<Vector4>(n);
                for(int i=0;i<n;i++){var v=s.Positions[i];var nn=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];vertices[i]=new Vector3(v.X,v.Y,v.Z);normals[i]=new Vector3(nn.X,nn.Y,nn.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);emission.Add(new Vector4(e.X,e.Y,e.Z,e.W));}
                var t=s.Indices.ToArray();
                var mesh=new Mesh{name=name+" / "+s.Part,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=new Vector2[n];mesh.SetUVs(1,emission);mesh.triangles=t;mesh.RecalculateBounds();mesh.UploadMeshData(true);
                parts[p]=new Part{Name=s.Part,Bone=s.Bone,Mesh=mesh,Material=material,Core=CoreOf(vertices,normals,colors,emission,t)};
            }
            return parts;
        }
    }

    public sealed partial class ArenaPresentation
    {
        // The robot's helmet and half mask (owner 2026-10-30: "Can we develop essentially a full helmet and a half mask similar to
        // what we had on voxel?") are face gears (the skin map's "face" entry, the Locker's MASKS row) and draw where the face gear
        // does (VisualFaceGear: the live body, the draft's seat, the menu's and the Locker's pilots, a corpse and a death scene), in
        // the pilot's body colour with its accent's light. Under the helmet the hair is hidden (VisualHatHair; a corpse's when it
        // is made) and a hat sits on the helmet's shell (VisualHatFit: Core NativeRobotHelm.HatFit). Either one takes the place of
        // the robot suit's ear modules (VisualSuit).
        // The face gear pilot `id` wears on the model (NativeWeaponSkins.FaceBoth .. FaceHalf), or -1 with none.
        private int FaceGearOf(int id)=>WearsFaceGear(BodySkin(id))?NativeWeaponSkins.Index(SeatWeaponSkins(id),NativeWeaponSkins.FaceEntry):-1;
        private bool WearsHelm(int id)=>FaceGearOf(id)==NativeWeaponSkins.FaceHelm;
        // A hat's place on the helmet, in the head bone's frame.
        private static Matrix4x4 HelmHatFit(string hat){NativeRobotHelm.HatFit(hat,out float k,out var move);return Matrix4x4.TRS(new Vector3(move.X,move.Y,move.Z),Quaternion.identity,Vector3.one*k);}
        // Whether a corpse holds its head: a whole one, or the half of a split one its cut left the head on (VisualCorpseHat's test).
        private bool VisualCorpseHead(NativeCorpse corpse)
        {
            var first=corpse.Host??corpse;if(first.Twin==null)return corpse.Host==null;
            int half=first.Bisect is BisectFx fx&&fx.Request!=null?fx.Request.HalfOf(CapturedRig.Head,CutNum(first.Bind[CapturedRig.Head].GetColumn(3))):0;
            return corpse.Host==null?half!=1:half==1;
        }
    }
}
