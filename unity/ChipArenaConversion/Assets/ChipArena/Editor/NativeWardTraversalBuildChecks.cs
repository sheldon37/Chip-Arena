using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        [MenuItem("Chip Arena/Check Ward Zero traversal")]
        public static void CheckWardZeroTraversal()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Leave Play mode before checking Ward traversal");
            var data=JsonUtility.FromJson<ConversionContent>(Resources.Load<TextAsset>("GameContent").text);NativeUnityMapLoader.Load(data);
            var scene=EditorSceneManager.NewPreviewScene();NativeArena arena=null;var gravity=Physics.gravity;
            try
            {
                if(!scene.GetPhysicsScene().IsValid()||scene.GetPhysicsScene()==Physics.defaultPhysicsScene)
                    throw new Exception("Ward traversal check requires isolated PhysX");
                var match=new NativeMatch(data);match.Seats.Add(new NativeSeat{id=1,name="Ward traversal check",spec="pistol"});
                arena=new NativeArena(data,match,"siren");arena.Reset();SceneManager.MoveGameObjectToScene(arena.Root,scene);
                arena.ValidateWardTraversal();
            }
            finally{arena?.Dispose();Physics.gravity=gravity;EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
