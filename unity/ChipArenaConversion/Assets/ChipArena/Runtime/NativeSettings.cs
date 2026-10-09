using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // Settings (main.ts settings screen): graphics, camera, controls, audio (volumes apply as they move), effects,
        // interface and the Siren mode key. Save Settings applies quality and writes the preferences; Connection report
        // sits beside it as in the source's actions row (NativeHomeMenu SettingsScreen).
        private void SettingsBody()
        {
            // The Siren mode key comes first and only for pilots who can play the Siren (main.ts settings).
            if(account.Developer||(account.Access??NativeAccess.Guest).Allows("weapon:siren",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())){Field("Siren mode key");prefs.sirenMode=ChoiceGrid(new[]{"Q","F","V"},prefs.sirenMode);Fine("V also changes the camera view.");}
            Field("Graphics");prefs.graphics=ChoiceGrid(new[]{"auto","high","performance"},prefs.graphics,new[]{"Auto · targets 60 FPS","High · sharper image","Performance · no shadows"});
            Field("Camera view");prefs.firstPerson=ChoiceRow(new[]{"Third person","First person"},prefs.firstPerson?1:0)==1;Fine("Press V during play to switch views.");
            Field("Controls");prefs.sensitivity=Slider("Mouse sensitivity",prefs.sensitivity,.2f,2.5f);
            Field("Audio");prefs.sound=!GUILayout.Toggle(!prefs.sound,"Mute audio");SoundTestRow();prefs.audio=Slider("Effects volume",prefs.audio,0,1);prefs.music=Slider("Arena music volume",prefs.music,0,1);prefs.menuMusic=Slider("Menu music volume",prefs.menuMusic,0,1);
            if(nativeAudio!=null){nativeAudio.Effects=Mathf.Clamp01(prefs.audio);nativeAudio.Music=Mathf.Clamp01(prefs.music);nativeAudio.Menu=Mathf.Clamp01(prefs.menuMusic);}
            Field("Effects");prefs.trails=GUILayout.Toggle(prefs.trails,"Projectile trails");prefs.reducedEffects=GUILayout.Toggle(prefs.reducedEffects,"Reduced effects");prefs.cameraShake=GUILayout.Toggle(prefs.cameraShake,"Camera shake");
            GUILayout.Label("Combat glow",small);int glow=prefs.combatGlow=="Off"?0:prefs.combatGlow=="Low"?1:2;prefs.combatGlow=new[]{"Off","Low","Full"}[ChoiceRow(new[]{"Off","Low","Full"},glow)];
            // The desktop shell's View menu (desktop/main.cjs:47): togglefullscreen beside the zoom roles that Interface scale
            // stands in for. It applies and saves at once through SetFullscreen, and shows the window's actual mode, which the
            // player's Alt+Enter can change without the saved preference.
            Field("Interface");bool now=Screen.fullScreenMode!=FullScreenMode.Windowed,full=GUILayout.Toggle(now,"Full screen");if(full!=now)SetFullscreen(full);prefs.uiScale=Mathf.Round(Slider("Interface scale",prefs.uiScale>0?prefs.uiScale:1,.75f,1.5f)*20)/20;Fine("Scales every menu and overlay; the canvas follows as you drag.");
            Field("Credits");Fine("Icons: Lucide (ISC licence), from the source client.");
        }
        // .sound-test: Test Sound (13 px .btn with the volume icon) and the status in 12 px mono #aac5ce beside it.
        private void SoundTestRow()
        {
            var r=GUILayoutUtility.GetRect(0,46,GUILayout.ExpandWidth(true));float w=NativeUi.Measure("Test Sound",13,true).x+19+18+36;
            NativeUi.Defer(()=>
            {
                if(MenuButton(new Rect(r.x+4,r.y+4,w,38),"volume-2","Test Sound",null,false,true,1,13,true))StartCoroutine(TestSound());
                NativeUi.Text(new Rect(r.x+4+w+18,r.y+4,r.width-w-26,38),soundStatus,12,NativeUi.Hex(0xaac5ce),TextAnchor.MiddleLeft,mono:true);
            });
        }
        private float Slider(string name,float value,float min,float max){GUILayout.Label(name+"  "+value.ToString("0.00"),small);return GUILayout.HorizontalSlider(value,min,max);}
        public bool Fullscreen=>prefs.fullscreen;
        public void SetFullscreen(bool enabled){prefs.fullscreen=enabled;Screen.fullScreenMode=enabled?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;Save();}
        private void ApplySettings()
        {
            Screen.fullScreenMode=prefs.fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
            prefs.sensitivity=Mathf.Clamp(prefs.sensitivity,.2f,2.5f);prefs.audio=Mathf.Clamp01(prefs.audio);prefs.music=Mathf.Clamp01(prefs.music);prefs.menuMusic=Mathf.Clamp01(prefs.menuMusic);
            bool low=prefs.graphics=="performance"||prefs.graphics=="auto"&&(SystemInfo.graphicsMemorySize<2048||SystemInfo.graphicsShaderLevel<45);QualitySettings.shadows=low?ShadowQuality.Disable:ShadowQuality.All;QualitySettings.antiAliasing=low?0:4;QualitySettings.pixelLightCount=low?3:9;
            if(nativeAudio!=null){nativeAudio.Effects=prefs.audio;nativeAudio.Music=prefs.music;nativeAudio.Menu=prefs.menuMusic;}if(presentation!=null)presentation.TrailsEnabled=prefs.trails;
        }
    }
}
