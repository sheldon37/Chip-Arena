using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        [MenuItem("Chip Arena/Check Cyber City cut exits")]
        public static void CheckCyberCityCutExits()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Leave Play mode before checking cut exits");
            var data=JsonUtility.FromJson<ConversionContent>(Resources.Load<TextAsset>("GameContent").text);NativeUnityMapLoader.Load(data);
            var scene=EditorSceneManager.NewPreviewScene();NativeArena arena=null;var gravity=Physics.gravity;
            try
            {
                if(!scene.GetPhysicsScene().IsValid()||scene.GetPhysicsScene()==Physics.defaultPhysicsScene)
                    throw new Exception("Cut exit check requires isolated PhysX");
                var match=new NativeMatch(data);match.Seats.Add(new NativeSeat{id=1,name="Cut exit check",spec="pistol"});
                arena=new NativeArena(data,match,"cybercity");arena.Reset();SceneManager.MoveGameObjectToScene(arena.Root,scene);
                arena.ValidateCyberCityExits();
            }
            finally{arena?.Dispose();Physics.gravity=gravity;EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
