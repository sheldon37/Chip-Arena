using UnityEngine;
using UnityEditor.SceneManagement;
namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckCyberRanged(ConversionContent content)
        {
            var scene=EditorSceneManager.NewPreviewScene();var other=EditorSceneManager.NewPreviewScene();NativeArena arena=null,guest=null;
            try
            {
                var match=new NativeMatch(content,7);for(int id=1;id<=3;id++)match.Seats.Add(new NativeSeat{id=id,name="Ranged check "+id,spec="pistol"});
                arena=new NativeArena(content,match,"foundry"){ReplicaScene=scene};match.Start();arena.Reset();
                guest=new NativeArena(content,new NativeMatch(content),"foundry"){Replica=true,LocalId=2,ReplicaScene=other};
                arena.ValidateCyberRanged(guest);
            }
            finally{guest?.Dispose();arena?.Dispose();EditorSceneManager.ClosePreviewScene(other);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
