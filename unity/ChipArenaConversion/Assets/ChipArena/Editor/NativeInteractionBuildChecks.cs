using System;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckInteractionFeedback(ConversionContent content)
        {
            var scene=EditorSceneManager.NewPreviewScene();var guestScene=EditorSceneManager.NewPreviewScene();NativeArena arena=null,guest=null;GameObject host=null,remote=null;
            try
            {
                var match=new NativeMatch(content,7);match.Seats.Add(new NativeSeat{id=1,name="Victim",spec="pistol"});match.Seats.Add(new NativeSeat{id=8,name="Siren",spec="siren",team=7});
                arena=new NativeArena(content,match,"siren"){ReplicaScene=scene};match.Start();arena.Reset();match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10});
                guest=new NativeArena(content,new NativeMatch(content),"siren"){Replica=true,LocalId=1,ReplicaScene=guestScene};
                host=new GameObject("Host interaction check");host.transform.SetParent(arena.Root.transform);var view=host.AddComponent<ArenaPresentation>();view.Initialize(false);
                remote=new GameObject("Guest interaction check");var other=remote.AddComponent<ArenaPresentation>();other.Initialize(false);
                arena.ValidateInteractionFeedback(guest,view,other);
            }
            finally{if(host!=null)CapturedAnimation.Release(host);if(remote!=null)CapturedAnimation.Release(remote);guest?.Dispose();arena?.Dispose();EditorSceneManager.ClosePreviewScene(guestScene);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
