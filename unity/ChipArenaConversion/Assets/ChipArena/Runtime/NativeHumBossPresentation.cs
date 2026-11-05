using UnityEngine;

namespace ChipArena.UnityConversion
{
    // THE HUM in the game (docs/EXPEDITION-BOSS-BACKROOMS.md section 3). Presentation only.
    public sealed partial class ArenaPresentation
    {
        private void HumBossScenery(NativeArena arena){}
        internal bool HumBossPreviewShot(out Vector3 eye,out Vector3 look,out float fov){eye=look=default;fov=60;return false;}
        internal bool HumBossPreviewCard(out BossIntroCard card,out float age){card=null;age=-1;return false;}
    }
    public sealed partial class ConversionGame
    {
        private void HumBossPreviewToggle(){NativeHumBossPreview.On=!NativeHumBossPreview.On;NativeHumBossPreview.Start=double.NaN;DevSay(NativeHumBossPreview.On?"Level 0 boss preview · on (presentation only)":"Level 0 boss preview · off",false);}
        private float HumBossPreviewPanel(float x,float y,float w){DevToggleAct(new Rect(x,y,w,32),"preview-hum-boss",NativeHumBossPreview.On);return y+38;}
        private void HumBossPreviewHud(float width,float height){}
    }
    public sealed partial class NativeAudio
    {
        private void HumBossCues(NativeArena arena,bool menu){}
    }
}
