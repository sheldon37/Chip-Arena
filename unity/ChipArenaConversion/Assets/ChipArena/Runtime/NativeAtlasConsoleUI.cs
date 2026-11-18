using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Deployment Station console's TILESET card (docs/EXPEDITION-OVERHAUL.md W2), the console's first: beside the station's
    /// title, over the ATLAS board it mirrors (Core NativeAtlasBoard, posed in NativeDeployStage.Atlas). It reads the board, so
    /// it says ARRIVING… while line 1 clatters in, then the selected tileset (CITY · MERIDIAN) IN SERVICE, flashing with the bell.
    /// Display only: there is nothing to choose yet (A3's SelectTileset adds the host's pick); it never raises an intent.
    /// </summary>
    public static class NativeAtlasConsole
    {
        public const float Width=262,Height=50,Top=24;
        private static float titleRight=-1;
        /// <summary>The card's rect on a W-wide canvas: right of the header's title, clear of its kicker and line.</summary>
        public static Rect Place(float W)
        {
            if(titleRight<0)titleRight=32+U.Measure("DEPLOYMENT STATION",28,true,false,.04f).x;
            return new Rect(Mathf.Max(400,titleRight+34),Top,Width,Height);
        }
        public static void Card(NativeDeployStage stage,float W,float a)
        {
            var board=stage?.Atlas;var t=board?.Selected;if(t==null||a<=.01f)return;
            var r=Place(W);var tone=U.Hex(t.Accent,a);bool landed=board.Since>=NativeAtlasBoard.LandAt;
            H.Panel(r,a,U.Hex(t.Accent),10,H.Corners.Diagonal,true,.9f);
            if(board.Flash>0)U.Glow(r,U.Hex(t.Accent,.45f*board.Flash*a),16);
            U.Text(new Rect(r.x+14,r.y+7,r.width-28,14),"TILESET · ATLAS",9.5f,tone,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            U.Text(new Rect(r.x+14,r.y+21,r.width-112,24),landed?t.Label:"ARRIVING…",landed?16:13,landed?C.Ink(a):C.Muted(a),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
            if(landed){var tag=new Rect(r.xMax-98,r.y+26,86,16);H.Tag(tag,tone,a);U.Text(tag,"IN SERVICE",8.5f,tone,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);}
        }
    }
}
