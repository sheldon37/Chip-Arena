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
        // intro-preview: the entrance plays alone with a slim caption strip and a close action (Escape also closes it).
        private void PreviewHud(float width,float height)
        {
            float age=Time.unscaledTime-previewStarted,e=NativeUi.PanelIn(age);var strip=new Rect(0,height-84,width,84);
            NativeUi.Fade(new Rect(0,height-200,width,200),new Color(0,0,0,.65f*e),false);NativeUi.Fill(new Rect(30,strip.y,width-60,1),NativeUi.Hex(0x415362,.8f*e));
            NativeUi.Text(new Rect(30,strip.y+14,width-260,16),"INTRO PREVIEW",11,NativeUi.Hex(0xff996f,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            NativeUi.Text(new Rect(30,strip.y+32,width-260,30),NativeCosmeticRules.Name(prefs.name)+"  ·  "+prefs.introText,20,NativeUi.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);
            if(NativeUi.Btn(new Rect(width-30-170,strip.y+20,170,44),"Close preview",false,true,e,14))EndPreview();
        }
    }
}
