using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public struct NativeRepairBox {public int Bone;public Matrix4x4 Local;}
    public sealed partial class NativeSourceModels
    {
        private Part repairCube;private Material repairMaterial;
        // Model-local shell bounds, exactly as downed.ts meshBox. The source meshes stay shared and unmodified.
        public void RepairBoxes(string id,List<NativeRepairBox> result)
        {
            result.Clear();if(!models.TryGetValue(id,out var model))return;
            foreach(var part in model)
            {
                if(part.Bone<0||part.Bone>=22)continue;var b=part.Mesh.bounds;
                var size=new Vector3(Mathf.Max(.02f,b.size.x),Mathf.Max(.02f,b.size.y),Mathf.Max(.02f,b.size.z));
                result.Add(new NativeRepairBox{Bone=part.Bone,Local=Matrix4x4.TRS(b.center,Quaternion.identity,size)});
            }
        }
        public void RepairCube(Matrix4x4 matrix,Color color,float opacity,float time)
        {
            if(opacity<=.002f)return;
            if(repairCube==null)
            {
                var shader=Resources.Load<Shader>("NativeRepair");if(shader==null)throw new System.InvalidOperationException("NativeRepair shader is missing");
                repairMaterial=new Material(shader){name="PvE regeneration scan",enableInstancing=true};materials.Add(repairMaterial);
                repairCube=new Part{Name="repair-cube",Bone=-1,Mesh=Cube().Mesh("Regeneration voxel",Color.white,Vector4.zero),Material=repairMaterial,NoShadow=true};models["repair-cube"]=new[]{repairCube};
            }
            repairMaterial.SetFloat("_RepairTime",time);Draw(repairCube,matrix,new Vector4(color.r,color.g,color.b,opacity),Color.white,Vector4.zero);
        }
    }
}
