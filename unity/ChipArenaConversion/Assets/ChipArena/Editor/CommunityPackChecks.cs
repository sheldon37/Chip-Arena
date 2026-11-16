using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public static class CommunityPackChecks
    {
        [MenuItem("Chip Arena/Checks/Community content example")]
        public static void Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Run the community content check outside play mode");
            var original=JsonUtility.FromJson<ConversionContent>(Resources.Load<TextAsset>("GameContent").text);
            NativeUnityMapLoader.Load(original);
            string fixture=Path.Combine(Application.dataPath,"../examples/thunderstore/RelayYard/chiparena.pack.json");
            string tmp=Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"arena-mod-"+Guid.NewGuid().ToString("N"))).FullName;
            var root=new GameObject("Community content check");
            try
            {
                string path=Path.Combine(tmp,"chiparena.pack.json");
                File.Copy(fixture,path);
                var content=NativeCommunityPacks.Load(original,tmp,out int count);
                string selected="mod.relay-yard.relay-saber",map="mod.relay-yard.yard";
                var seat=new NativeSeat{spec=selected};
                content.NormalizeSelection(seat);
                if(count!=1||seat.spec!="sword"||content.CleanMotion(seat.motion,seat.spec)!=selected||
                    NativeMapRules.Selection(content,map)!=map)throw new Exception("Content registration failed");
                var empty=new Dictionary<string,int>();
                if(!NativeContentRules.Eligible(content,seat.SelectedWeapon,empty).Any(c=>c.id=="relay-yard:coil-edge")||
                    NativeContentRules.Eligible(content,"sword",empty).Any(c=>c.id=="relay-yard:coil-edge"))throw new Exception("Signature routing failed");
                NativeCommunityPacks.BuildGeometry(map,root.transform,content.materials.ToDictionary(m=>m.id,m=>(Material)null));
                if(root.GetComponentInChildren<MeshCollider>()?.sharedMesh?.vertexCount!=5)throw new Exception("Sample geometry failed");
                using(var library=new CapturedAnimation())
                using(var rig=library.Create(root.transform,1))
                {
                    var pilot=new Actor{Motion=selected,SourceSpec="sword",Hp=100,Grounded=true,AttackAt=1,AttackUntil=1.6f};
                    rig.ActorPose(pilot,Vector3.zero,1.1375f);
                    var shoulder=rig.Bones[Array.FindIndex(CapturedRig.Bones,b=>b.Name=="right-shoulder")];
                    if(!rig.CommunityPoseActive||Quaternion.Angle(shoulder.localRotation,Quaternion.Euler(0,0,75))>.1f)throw new Exception("Sample weapon animation failed");
                }
                string revision=content.contentRevision;
                File.AppendAllText(path,"\n");
                if(NativeCommunityPacks.Load(original,tmp,out _).contentRevision==revision)throw new Exception("Changed package matched original");
                File.WriteAllText(path,"{}");
                try{NativeCommunityPacks.Load(original,tmp,out _);throw new Exception("Invalid pack was accepted");}
                catch(InvalidDataException){}
                Debug.Log("Community content example passed: selection, signatures, geometry, animation, revision, malformed pack");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);NativeCommunityPacks.Clear();Directory.Delete(tmp,true);}
        }
    }
}
