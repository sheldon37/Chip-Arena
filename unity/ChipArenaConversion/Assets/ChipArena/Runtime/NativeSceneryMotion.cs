using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeSnowSeed {public float x,y,z,speed;}
    [Serializable] public sealed class NativeSceneryMap {public string id;public NativeSnowSeed[] snow;public float[] smoke;public int smokeColor;}
    [Serializable] public sealed class NativeSceneryData {public string commit;public NativeSceneryMap[] maps;}
    public sealed partial class ArenaPresentation
    {
        private NativeSceneryData sceneryData;private Mesh smokeMesh;private Material smokeMaterial;private readonly Matrix4x4[] smokeMatrices=new Matrix4x4[1023];
        private void SceneryMotion(NativeArena arena)
        {
            if(sceneryData==null){sceneryData=JsonUtility.FromJson<NativeSceneryData>(Resources.Load<TextAsset>("SceneryMotion").text);if(sceneryData.commit!=arena.Content.sourceCommit)throw new InvalidOperationException("Unverified scenery motion");}
            var map=sceneryData.maps.FirstOrDefault(m=>m.id==arena.MapId);float time=arena.Time;NativeLighting.Clock(time);if(map==null)return;
            for(int i=0;i<map.snow.Length;i++){var p=map.snow[i];sourceModels.Queue("scenery-snow",Matrix4x4.TRS(new Vector3(p.x+Mathf.Sin(time*.4f+i)*.55f,((p.y-time*p.speed)%24+24)%24,p.z+Mathf.Sin(time*.25f+i*.4f)*.35f),Quaternion.identity,new Vector3(.025f,.075f,.025f)));}
            if(arena.MapId=="canal")sourceModels.Queue("scenery-water",Matrix4x4.TRS(new Vector3(0,.018f,0),Quaternion.identity,new Vector3(6,.02f,53)),opacity:(.85f+Mathf.Sin(time*.8f)*.04f)/.9f);
            if(map.smoke.Length==0)return;
            if(smokeMesh==null){smokeMesh=new Mesh{name="Source smoke billboard"};smokeMesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};smokeMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};smokeMesh.triangles=new[]{0,1,2,0,2,3};smokeMesh.bounds=new Bounds(Vector3.zero,Vector3.one*300);smokeMaterial=new Material(Resources.Load<Shader>("NativeSmoke")){enableInstancing=true};}
            smokeMaterial.SetColor("_Color",NativeArena.ColorOf(map.smokeColor));int count=0;
            for(int i=0;i<map.smoke.Length/4;i++){int n=i*4;float age=(time*.7f+map.smoke[n+3]*18)%18;var matrix=Matrix4x4.Translate(new Vector3(map.smoke[n]+age*.28f,map.smoke[n+1]+age,map.smoke[n+2]+Mathf.Sin(age*.15f+i)*1.5f));
                if(UsesInstancing){smokeMatrices[count++]=matrix;if(count==1023){Graphics.DrawMeshInstanced(smokeMesh,0,smokeMaterial,smokeMatrices,count,null,ShadowCastingMode.Off,false,0);count=0;}}
                else Graphics.DrawMesh(smokeMesh,matrix,smokeMaterial,0,null,0,null,ShadowCastingMode.Off,false);}
            if(count>0)Graphics.DrawMeshInstanced(smokeMesh,0,smokeMaterial,smokeMatrices,count,null,ShadowCastingMode.Off,false,0);
        }
        private void ClearSceneryMotion(){CapturedAnimation.Release(smokeMesh);CapturedAnimation.Release(smokeMaterial);smokeMesh=null;smokeMaterial=null;}
    }
}
