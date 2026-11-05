using UnityEngine;

namespace ChipArena.UnityConversion
{
    // THE DEEP END in the game (docs/EXPEDITION-BOSS-BACKROOMS.md section 4). Presentation only.
    public sealed partial class ArenaPresentation
    {
        private void DeepEndBossScenery(NativeArena arena){}
        internal bool DeepEndBossPreviewShot(out Vector3 eye,out Vector3 look,out float fov){eye=look=default;fov=60;return false;}
        internal bool DeepEndBossPreviewCard(out BossIntroCard card,out float age){card=null;age=-1;return false;}
    }
    public sealed partial class ConversionGame
    {
        private void DeepEndBossPreviewToggle(){NativeDeepEndBossPreview.On=!NativeDeepEndBossPreview.On;NativeDeepEndBossPreview.Start=double.NaN;DevSay(NativeDeepEndBossPreview.On?"Poolrooms boss preview · on (presentation only)":"Poolrooms boss preview · off",false);}
        private float DeepEndBossPreviewPanel(float x,float y,float w){DevToggleAct(new Rect(x,y,w,32),"preview-deepend-boss",NativeDeepEndBossPreview.On);return y+38;}
        private void DeepEndBossPreviewHud(float width,float height){}
    }
    public sealed partial class NativeAudio
    {
        private void DeepEndBossCues(NativeArena arena,bool menu){}
    }
}
