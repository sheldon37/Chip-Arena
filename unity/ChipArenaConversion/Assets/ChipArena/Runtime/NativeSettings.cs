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
            // Settings · Camera (Core/NativeCameraModes): the two shoulders, Close action, Classic (the old camera) and first person.
            Field("Camera");NativeCameraModes.Choose(ChoiceGrid(NativeCameraModes.Choices,NativeCameraModes.Choice(prefs.cameraMode,prefs.shoulderLeft,prefs.firstPerson),NativeCameraModes.Labels),ref prefs.cameraMode,ref prefs.shoulderLeft,ref prefs.firstPerson);
            Fine("V switches to first person and back; "+NativeCameraModes.SwapKey+" swaps the shoulder during play.");
            Field("Controls");prefs.sensitivity=Slider("Mouse sensitivity",prefs.sensitivity,.2f,2.5f);
            Field("Audio");prefs.sound=!GUILayout.Toggle(!prefs.sound,"Mute audio");SoundTestRow();prefs.menuMusic=Slider("Menu Music",prefs.menuMusic,0,1);prefs.music=Slider("Stage Music",prefs.music,0,1);prefs.audio=Slider("Sounds",prefs.audio,0,1);
            if(nativeAudio!=null){nativeAudio.Effects=Mathf.Clamp01(prefs.audio);nativeAudio.Music=Mathf.Clamp01(prefs.music);nativeAudio.Menu=Mathf.Clamp01(prefs.menuMusic);}
            Field("Effects");prefs.trails=GUILayout.Toggle(prefs.trails,"Projectile trails");prefs.reducedEffects=GUILayout.Toggle(prefs.reducedEffects,"Reduced effects");prefs.cameraShake=GUILayout.Toggle(prefs.cameraShake,"Camera shake");
            GUILayout.Label("Combat glow",small);int glow=prefs.combatGlow=="Off"?0:prefs.combatGlow=="Low"?1:2;prefs.combatGlow=new[]{"Off","Low","Full"}[ChoiceRow(new[]{"Off","Low","Full"},glow)];
            // The damage screen (NativeDamageScreenPresentation): Off, Subtle (60%, no colour split) or Full; Reduced effects also stills it.
            GUILayout.Label("Damage screen effect",small);int hurt=prefs.damageScreen=="Off"?0:prefs.damageScreen=="Subtle"?1:2;prefs.damageScreen=new[]{"Off","Subtle","Full"}[ChoiceRow(new[]{"Off","Subtle","Full"},hurt)];
            // Alarm lights (the hazard kit's strobes, beacons and signs, NativeHazardKitClock): Full flashes at most 3 times a second,
            // Reduced at most 2 and softly, Calm never (a slow glow). Reduced effects keeps them at Reduced or calmer.
            GUILayout.Label("Alarm lights",small);int alarm=prefs.alarmLights=="Calm"?0:prefs.alarmLights=="Reduced"?1:2;prefs.alarmLights=new[]{"Calm","Reduced","Full"}[ChoiceRow(new[]{"Calm","Reduced","Full"},alarm)];
            // The desktop shell's View menu (desktop/main.cjs:47): togglefullscreen beside the zoom roles that Interface scale
            // stands in for. It applies and saves at once through SetFullscreen, and shows the window's actual mode, which the
            // player's Alt+Enter can change without the saved preference.
            Field("Interface");bool now=Screen.fullScreenMode!=FullScreenMode.Windowed,full=GUILayout.Toggle(now,"Full screen");if(full!=now)SetFullscreen(full);prefs.uiScale=Mathf.Round(Slider("Interface scale",prefs.uiScale>0?prefs.uiScale:1,.75f,1.5f)*20)/20;Fine("Scales every menu and overlay; the canvas follows as you drag.");
            if(account.Developer)
            {
                Field("Developer");bool console=GUILayout.Toggle(prefs.developmentConsole,"Show development console");
                if(console!=prefs.developmentConsole){prefs.developmentConsole=console;SyncDevelopmentConsole();Save();}
                // DEV Abuse (NativeDevPanel.cs, docs/DEV-TOOLS.md): off by default, saved; the DEV panel (F3) in any match, offline or
                // online, host or guest, for this verified dev account only, and Astra's session opt-in DevAbuseEnabled. The host never
                // trusts it: every DEV command is checked against the verified seat (docs/DEV-ABUSE-AUTHORITY.md).
                if(NativeDevGate.ToggleVisible(account.Developer)){bool abuse=GUILayout.Toggle(prefs.devAbuse,"DEV Abuse: DEV tools in every match (F3)");if(abuse!=prefs.devAbuse){prefs.devAbuse=abuse;DevAbuseEnabled=NativeDevGate.CommandsEnabled(abuse,account.Developer);Save();}Fine("Off by default. Turns on this session's DEV commands (DevAbuseEnabled); the match's authority still checks your verified grant for every command.");}
                // The Siren minion kinds before their rules exist (NativeHordeKindPresentation.cs): every horde add drawn as one kind, or a
                // mix by id. Looks only (the add's rules, timing and hit volume stay its own); this session only, never saved.
                GUILayout.Label("Siren minion preview (looks only)",small);NativeHordeKinds.Preview=HordePreviewChoice(ChoiceGrid(HordePreviewIds,HordePreviewId(NativeHordeKinds.Preview),HordePreviewNames));
            }
            ModsBody();
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
        // Development builds keep Player.log, but never pop the overlay before a verified DEV opts in.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HideDevelopmentConsole(){Debug.developerConsoleEnabled=false;Debug.developerConsoleVisible=false;}
        private bool developmentConsoleEnabled;
        private void SyncDevelopmentConsole()
        {
            bool enabled=!dedicated&&account!=null&&account.Developer&&prefs?.developmentConsole==true;
            if(Debug.developerConsoleEnabled!=enabled)Debug.developerConsoleEnabled=enabled;
            // Show once on opt-in; respect Unity's close button until another error or a fresh opt-in.
            if(enabled!=developmentConsoleEnabled)Debug.developerConsoleVisible=enabled;
            else if(!enabled&&Debug.developerConsoleVisible)Debug.developerConsoleVisible=false;
            developmentConsoleEnabled=enabled;
        }
        private bool windowModeInitialized;private int windowModeRequestedAt=-100;
        private void SyncFullscreen()
        {
            if(!windowModeInitialized||Time.frameCount<=windowModeRequestedAt+1)return;
            bool actual=Screen.fullScreenMode!=FullScreenMode.Windowed;
            if(prefs.fullscreen!=actual){prefs.fullscreen=actual;Save();}
        }
        public bool Fullscreen=>Screen.fullScreenMode!=FullScreenMode.Windowed;
        public void SetFullscreen(bool enabled){windowModeInitialized=true;windowModeRequestedAt=Time.frameCount;prefs.fullscreen=enabled;Screen.fullScreenMode=enabled?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;Save();}
        private void ApplySettings()
        {
            SyncDevelopmentConsole();
            if(!windowModeInitialized)SetFullscreen(prefs.fullscreen);else SyncFullscreen();
            prefs.sensitivity=Mathf.Clamp(prefs.sensitivity,.2f,2.5f);prefs.audio=Mathf.Clamp01(prefs.audio);prefs.music=Mathf.Clamp01(prefs.music);prefs.menuMusic=Mathf.Clamp01(prefs.menuMusic);
            bool low=prefs.graphics=="performance"||prefs.graphics=="auto"&&(SystemInfo.graphicsMemorySize<2048||SystemInfo.graphicsShaderLevel<45);QualitySettings.shadows=low?ShadowQuality.Disable:ShadowQuality.All;QualitySettings.antiAliasing=low?0:4;QualitySettings.pixelLightCount=low?3:9;NativeLighting.Graphics(low);
            if(nativeAudio!=null){nativeAudio.Effects=prefs.audio;nativeAudio.Music=prefs.music;nativeAudio.Menu=prefs.menuMusic;}if(presentation!=null){presentation.TrailsEnabled=prefs.trails;presentation.LowGraphics=low;}
            // The match camera's bloom: off on the low tier, x.6 with Reduced effects (NativeBloom.Quality).
            if(bloom!=null)bloom.Quality(low,prefs.reducedEffects);
        }
    }
}
