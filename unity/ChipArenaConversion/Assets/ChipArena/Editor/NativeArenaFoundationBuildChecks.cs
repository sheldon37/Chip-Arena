using System;
using UnityEngine;
using UnityEditor.SceneManagement;
namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckArenaFoundation(ConversionContent legacy)
        {
            var catalog=Resources.Load<TextAsset>("ArenaMaps");if(catalog==null)throw new Exception("Missing experimental arena catalog");
            var content=JsonUtility.FromJson<ConversionContent>(JsonUtility.ToJson(legacy));NativeMapRules.Append(content,JsonUtility.FromJson<MapCatalog>(catalog.text));
            var hostScene=EditorSceneManager.NewPreviewScene();var guestScene=EditorSceneManager.NewPreviewScene();var lateScene=EditorSceneManager.NewPreviewScene();
            NativeArena host=null,guest=null,late=null;var gravity=Physics.gravity;
            try
            {
                var match=new NativeMatch(content);for(int i=0;i<8;i++)match.Seats.Add(new NativeSeat{id=i+1,name="Structure check "+i,spec="pistol",team=i%2});
                host=new NativeArena(content,match,"arena-1-lab"){ReplicaScene=hostScene};host.Reset();
                guest=new NativeArena(content,new NativeMatch(content),"arena-1-lab"){Replica=true,ReplicaScene=guestScene};
                late=new NativeArena(content,new NativeMatch(content),"arena-1-lab"){Replica=true,ReplicaScene=lateScene};
                host.ValidateArenaFoundation(guest,late);
            }
            finally
            {
                host?.Dispose();guest?.Dispose();late?.Dispose();Physics.gravity=gravity;
                EditorSceneManager.ClosePreviewScene(hostScene);EditorSceneManager.ClosePreviewScene(guestScene);EditorSceneManager.ClosePreviewScene(lateScene);
            }
        }
    }
}
