using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckLocomotion(ConversionContent content)
        {
            var scene=EditorSceneManager.NewPreviewScene();NativeArena arena=null;var gravity=Physics.gravity;
            try
            {
                if(!scene.GetPhysicsScene().IsValid()||scene.GetPhysicsScene()==Physics.defaultPhysicsScene)throw new Exception("Locomotion gate requires isolated physics");
                var match=new NativeMatch(content);match.Seats.Add(new NativeSeat{id=1,name="Stair check",spec="pistol"});
                arena=new NativeArena(content,match,content.maps.First(m=>!m.unity).id);arena.Reset();SceneManager.MoveGameObjectToScene(arena.Root,scene);arena.ValidateLocomotion();
                arena.Reset();SceneManager.MoveGameObjectToScene(arena.Root,scene);arena.ValidateAirJumps();
            }
            finally{arena?.Dispose();Physics.gravity=gravity;EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
