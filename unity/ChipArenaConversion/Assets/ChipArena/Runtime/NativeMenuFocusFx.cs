using UnityEngine;
namespace ChipArena.UnityConversion
{
    // Draws the controller's menu focus (Core NativeMenuFocusMotion): a soft cyan glow round the control, a faint fill, a
    // bright edge that pops in on each move, and the menu hover tick through NativeUi's one UI sound hook. Screen pixels under
    // the identity GUI matrix (NativeController.ControllerCursor), Repaint only.
    public static class NativeMenuFocusFx
    {
        public static readonly Color Ink=new Color(.2f,1,.95f,1);
        private const int Band=20,Size=64;
        private static readonly NativeMenuFocusMotion motion=new NativeMenuFocusMotion();
        private static Texture2D halo;private static GUIStyle haloStyle;
        // The glow: transparent inside a rectangle Band px in from the texture's edges, fading out over Band px round it (round
        // at the corners); nine-sliced so the fade keeps its width on any control.
        private static void Ensure()
        {
            if(halo!=null)return;
            halo=new Texture2D(Size,Size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            var px=new Color32[Size*Size];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                float cx=x+.5f,cy=y+.5f,dx=Mathf.Max(0,Mathf.Max(Band-cx,cx-(Size-Band))),dy=Mathf.Max(0,Mathf.Max(Band-cy,cy-(Size-Band)));
                float a=dx<=0&&dy<=0?0:Mathf.Pow(Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dy*dy)/Band),2.2f);
                px[y*Size+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(a*255));
            }
            halo.SetPixels32(px);halo.Apply(false,true);
            haloStyle=new GUIStyle{border=new RectOffset(Band+4,Band+4,Band+4,Band+4)};haloStyle.normal.background=halo;
        }
        /// <summary>The focus on control `focus` at screen rect `r`; ticks on a move (NativeMenuFocusMotion.Step).</summary>
        public static void Draw(Rect r,int focus,float now,bool reduced,float scale)
        {
            if(Event.current.type!=EventType.Repaint)return;
            if(motion.Step(focus,now))NativeUi.Cue(NativeUiSounds.Hover);
            Ensure();float k=Mathf.Max(1,scale),pop=NativeMenuFocusMotion.PopAt(now-motion.MovedAt,reduced),breath=NativeMenuFocusMotion.Breath(now,reduced);
            var color=GUI.color;var back=GUI.backgroundColor;GUI.backgroundColor=Color.white;
            // The glow: its inner edge on the control's edge, brighter as it arrives.
            GUI.color=new Color(Ink.r,Ink.g,Ink.b,.4f+.15f*breath+.35f*pop);
            haloStyle.Draw(new Rect(r.x-Band,r.y-Band,r.width+2*Band,r.height+2*Band),GUIContent.none,false,false,false,false);
            // A faint fill, and the bright edge standing PopGrow px out as it arrives.
            GUI.color=new Color(Ink.r,Ink.g,Ink.b,.08f+.06f*pop);GUI.DrawTexture(r,Texture2D.whiteTexture);
            float e=NativeMenuFocusMotion.PopGrow*k*pop,t=Mathf.Round(2*k);
            GUI.color=new Color(Ink.r,Ink.g,Ink.b,.8f+.2f*breath);Edge(new Rect(r.x-e,r.y-e,r.width+2*e,r.height+2*e),t);
            GUI.color=color;GUI.backgroundColor=back;
        }
        /// <summary>No focus shown this frame: the next one ticks.</summary>
        public static void Hide()=>motion.Hide();
        private static void Edge(Rect o,float t)
        {
            var w=Texture2D.whiteTexture;GUI.DrawTexture(new Rect(o.x,o.y,o.width,t),w);GUI.DrawTexture(new Rect(o.x,o.yMax-t,o.width,t),w);
            GUI.DrawTexture(new Rect(o.x,o.y+t,t,o.height-2*t),w);GUI.DrawTexture(new Rect(o.xMax-t,o.y+t,t,o.height-2*t),w);
        }
    }
}
