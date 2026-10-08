using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool showSettings;
        private void SettingsPanel()
        {
            if(GUILayout.Button(showSettings?"Close settings":"Audio, graphics and controls",button))showSettings=!showSettings;if(!showSettings)return;
            prefs.sensitivity=Slider("Mouse sensitivity",prefs.sensitivity,.2f,2.5f);prefs.audio=Slider("Effects volume",prefs.audio,0,1);prefs.music=Slider("Match music",prefs.music,0,1);prefs.menuMusic=Slider("Menu music",prefs.menuMusic,0,1);prefs.uiScale=Mathf.Round(Slider("Interface scale",prefs.uiScale>0?prefs.uiScale:1,.75f,1.5f)*20)/20;
            prefs.graphics=ChoiceGrid(new[]{"auto","high","performance"},prefs.graphics);prefs.trails=GUILayout.Toggle(prefs.trails,"Projectile trails");prefs.reducedEffects=GUILayout.Toggle(prefs.reducedEffects,"Reduced effects");prefs.cameraShake=GUILayout.Toggle(prefs.cameraShake,"Camera shake");
            GUILayout.Label("Siren mode key (V also changes camera)",small);prefs.sirenMode=ChoiceGrid(new[]{"Q","F","V"},prefs.sirenMode);
            if(GUILayout.Button("Save settings",button)){ApplySettings();Save();}
        }
        private float Slider(string name,float value,float min,float max){GUILayout.Label(name+"  "+value.ToString("0.00"),small);return GUILayout.HorizontalSlider(value,min,max);}
        private void ApplySettings()
        {
            prefs.sensitivity=Mathf.Clamp(prefs.sensitivity,.2f,2.5f);prefs.audio=Mathf.Clamp01(prefs.audio);prefs.music=Mathf.Clamp01(prefs.music);prefs.menuMusic=Mathf.Clamp01(prefs.menuMusic);
            bool low=prefs.graphics=="performance"||prefs.graphics=="auto"&&(SystemInfo.graphicsMemorySize<2048||SystemInfo.graphicsShaderLevel<45);QualitySettings.shadows=low?ShadowQuality.Disable:ShadowQuality.All;QualitySettings.antiAliasing=low?0:4;QualitySettings.pixelLightCount=low?3:9;
            if(nativeAudio!=null){nativeAudio.Effects=prefs.audio;nativeAudio.Music=prefs.music;nativeAudio.Menu=prefs.menuMusic;}if(presentation!=null)presentation.TrailsEnabled=prefs.trails;
        }
    }
}
