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
        {if(previewArena==null)return;previewArena.Dispose();previewArena=null;foreach(var pair in previewVisibility)if(pair.Key!=null)pair.Key.enabled=pair.Value;previewVisibility.Clear();if(previewScene.IsValid())SceneManager.UnloadSceneAsync(previewScene);presentation?.ResetPresentation();nativeAudio?.Entrance(null,null,0);if(cameraView!=null)NativeLighting.EndIntro(content.Map(arena?.MapId??PreviewMapId),cameraView);}
        private bool PreviewFrame()
        {
            if(previewArena==null)return false;float age=Time.unscaledTime-previewStarted;if(age>NativeIntroRules.Duration(previewStyle)+.7f){EndPreview();return false;}
            if(arena?.Root!=null)foreach(var r in arena.Root.GetComponentsInChildren<Renderer>()){if(!previewVisibility.ContainsKey(r))previewVisibility[r]=r.enabled;r.enabled=false;}viewModelCamera.enabled=false;presentation.WeaponEffectsQuality=prefs.reducedEffects?.3f:1;presentation.RenderIntro(previewArena,previewArena.Local,previewStyle,age,cameraView);nativeAudio.Entrance("preview:"+previewSerial,previewStyle,age);nativeAudio.HoldMusic();return true;
        }
        // intro-preview (main.ts screen==='intro-preview'): exactly the match-intro overlay for the local pilot (01 — 01),
        // with .btn.intro-close "Back to customization" at the top right (right 3vw, top 145 px); Escape also closes it.
        private void PreviewHud(float width,float height)
        {
            float age=Time.unscaledTime-previewStarted;if(previewArena?.Local!=null)IntroCard(previewArena.Local,previewStyle,age,false,0,1,false);
            string back="Back to customization";float w=NativeUi.Measure(back,12,true).x+38;
            if(MenuButton(new Rect(width-width*.03f-w,145,w,48),null,back,null,false,true,1,12,true)){EndPreview();Go(MenuScreen.Customize);}
        }
    }
}
