using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private readonly Dictionary<Renderer,bool> previewVisibility=new Dictionary<Renderer,bool>();private NativeArena previewArena;private Scene previewScene;private string previewStyle;private float previewStarted;private int previewSerial;
        private void StartPreview()
        {
            EndPreview();ClearReplay();previewScene=SceneManager.CreateScene("Appearance preview",new CreateSceneParameters(LocalPhysicsMode.Physics3D));var m=new NativeMatch(content);var seat=SanitizedSeat(1,Profile(),account.Token);m.Seats.Add(seat);previewStyle=NativeIntroRules.Style(seat);previewArena=new NativeArena(content,m,"foundry"){Replica=true};previewArena.Reset();SceneManager.MoveGameObjectToScene(previewArena.Root,previewScene);previewArena.Root.SetActive(false);previewStarted=Time.unscaledTime;previewSerial++;presentation.ResetPresentation();
        }
        private void EndPreview()
        {if(previewArena==null)return;previewArena.Dispose();previewArena=null;foreach(var pair in previewVisibility)if(pair.Key!=null)pair.Key.enabled=pair.Value;previewVisibility.Clear();if(previewScene.IsValid())SceneManager.UnloadSceneAsync(previewScene);presentation?.ResetPresentation();nativeAudio?.Entrance(null,null,0);if(cameraView!=null)NativeLighting.EndIntro(content.Map(arena?.MapId??prefs.map),cameraView);}
        private bool PreviewFrame()
        {
            if(previewArena==null)return false;float age=Time.unscaledTime-previewStarted;if(age>NativeIntroRules.Duration(previewStyle)+.7f){EndPreview();return false;}
            if(arena?.Root!=null)foreach(var r in arena.Root.GetComponentsInChildren<Renderer>()){if(!previewVisibility.ContainsKey(r))previewVisibility[r]=r.enabled;r.enabled=false;}viewModelCamera.enabled=false;presentation.RenderIntro(previewArena,previewArena.Local,previewStyle,age,cameraView);nativeAudio.Entrance("preview:"+previewSerial,previewStyle,age);return true;
        }
        private void PreviewHud(float width,float height)
        {GUI.Box(new Rect(0,height-95,width,95),"");GUI.Label(new Rect(28,height-82,width-230,45),prefs.name+" · "+prefs.introText,heading);if(GUI.Button(new Rect(width-190,height-76,160,45),"Close preview",button))EndPreview();}
    }
}
