using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Deployment Station console's TILESET card (docs/EXPEDITION-OVERHAUL.md W2), the console's first: a slim chip beside
    /// the station's title, on its kicker's line, over the ATLAS board it mirrors (Core NativeAtlasBoard, posed in
    /// NativeDeployStage.Atlas). It reads the board, so it says ARRIVING… while line 1 clatters in, then the selected tileset
    /// (CITY · MERIDIAN) IN SERVICE, flashing with the bell. It stays above the board's top from the Overview at every
    /// Interface scale up to 1.5 (the checks pin its rect). Display only: there is nothing to choose yet (A3's SelectTileset
    /// adds the host's pick); it never raises an intent. While the board holds the camera the console's side panels (the
    /// machines' cards, display only) step aside, so the board's shot is clear at any aspect and Interface scale
    /// (<see cref="Side"/>); the title, this card and the controls stay.
    /// </summary>
    public static class NativeAtlasConsole
    {
        public const float Width=336,Height=26,Top=18;
        /// <summary>How much of the console's side panels to draw this frame (the board's NativeAtlasStage.Panels; 1 without it).</summary>
        public static float Side(NativeDeployStage stage)=>stage?.Atlas!=null?Mathf.Clamp01(stage.Atlas.Panels):1;
        private static float titleRight=-1;
        /// <summary>The card's rect on a W-wide canvas: right of the header's title, on its kicker's line.</summary>
        public static Rect Place(float W)
        {
            if(titleRight<0)titleRight=32+U.Measure("DEPLOYMENT STATION",28,true,false,.04f).x;
            return new Rect(Mathf.Max(400,titleRight+34),Top,Width,Height);
        }
        public static void Card(NativeDeployStage stage,float W,float a)
        {
            var board=stage?.Atlas;var t=board?.Selected;if(t==null||a<=.01f)return;
            var r=Place(W);var tone=U.Hex(t.Accent,a);bool landed=board.Since>=NativeAtlasBoard.LandAt;
            H.Panel(r,a,U.Hex(t.Accent),8,H.Corners.Diagonal,true,.9f);
            if(board.Flash>0)U.Glow(r,U.Hex(t.Accent,.45f*board.Flash*a),14);
            // one line: TILESET, the tileset's name (or ARRIVING…), its IN SERVICE tag; the name stops short of the tag (14 when
            // it fits, else as large as fits: a longer name later)
            U.Text(new Rect(r.x+12,r.y,64,r.height),"TILESET",9,tone,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            var tag=new Rect(r.xMax-92,r.y+6,82,14);var label=new Rect(r.x+80,r.y+1,tag.x-8-(r.x+80),r.height-2);
            if(!landed){U.Text(label,"ARRIVING…",12,C.Muted(a),TextAnchor.MiddleLeft,bold:true,tracking:.04f);return;}
            float w=U.Measure(t.Label,14,true,false,.04f).x;
            U.Text(label,t.Label,w>label.width?Mathf.Max(10,14*label.width/w):14,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
            H.Tag(tag,tone,a);U.Text(tag,"IN SERVICE",8,tone,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);
        }
    }
}
