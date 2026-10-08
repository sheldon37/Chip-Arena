using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The source menu look (style.css visual revision) as an IMGUI skin, so every existing GUILayout menu — setup,
    // settings, profile, network, relay, sandbox, inspect — shares it: #17181d panels with a #45464e edge and 10 px
    // corners, #222329 buttons that warm to an orange edge on hover, orange toggles and slider thumbs, dark fields with an
    // orange focus edge, thin scrollbars. Built lazily on the first GUI event (never in a field initializer).
    public static class NativeMenuSkin
    {
        private static GUISkin skin;
        public static GUISkin Skin{get{if(skin==null)Build();return skin;}}
        private static Color[] RoundedPixels(int size,int radius,Color fill,Color edge,float edgeWidth)
        {
            var px=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float cx=Mathf.Clamp(x+.5f,radius,size-radius),cy=Mathf.Clamp(y+.5f,radius,size-radius),d=Mathf.Sqrt((x+.5f-cx)*(x+.5f-cx)+(y+.5f-cy)*(y+.5f-cy));
                float outside=Mathf.Clamp01(d-radius+.5f),inner=Mathf.Clamp01(d-(radius-edgeWidth)+.5f);float edgeInX=Mathf.Min(x+.5f,size-x-.5f),edgeInY=Mathf.Min(y+.5f,size-y-.5f);
                bool rim=edgeInX<=edgeWidth||edgeInY<=edgeWidth||inner>0;var c=rim?edge:fill;c.a*=1-outside;px[y*size+x]=c;
            }
            return px;
        }
        private static Texture2D Make(int w,int h,Color[] px){var t=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};t.SetPixels(px);t.Apply(false,true);return t;}
        private static Texture2D Rounded(int size,int radius,Color fill,Color edge,float edgeWidth=1)=>Make(size,size,RoundedPixels(size,radius,fill,edge,edgeWidth));
        private static Texture2D Circle(int size,Color fill,Color edge)=>Rounded(size,size/2,fill,edge,1.5f);
        // A checkbox at the left of a wider transparent strip, so the 9-slice keeps the box square beside the label.
        // A checkbox at the left of a wider transparent strip, so the 9-slice keeps the box square beside the label.
        private static Texture2D Check(Color fill,Color edge,bool mark)
        {
            var box=RoundedPixels(18,4,fill,edge,1.5f);var px=new Color[24*18];
            for(int y=0;y<18;y++)for(int x=0;x<24;x++){var c=x<18?box[y*18+x]:Color.clear;if(mark&&x<18){float u=x+.5f,v=17.5f-y;bool tick=Mathf.Abs(v-(u<8?u+3:19-u*.85f))<1.3f&&u>4&&u<14;if(tick)c=NativeUi.Hex(0x111216);}px[y*24+x]=c;}
            return Make(24,18,px);
        }
        private static GUIStyleState State(Texture2D bg,Color text)=>new GUIStyleState{background=bg,textColor=text};
        private static void Build()
        {
            skin=Object.Instantiate(GUI.skin);skin.hideFlags=HideFlags.HideAndDontSave;
            Color text=NativeUi.Hex(0xe6e7ec),bright=NativeUi.Hex(0xf5f5f7),muted=NativeUi.Hex(0xa6a7ad),accent=NativeUi.Hex(0xff6b35);
            var b=new RectOffset(10,10,10,10);
            skin.box.normal=State(Rounded(32,10,NativeUi.Hex(0x17181d,.96f),NativeUi.Hex(0x45464e)),text);skin.box.border=b;skin.box.padding=new RectOffset(16,16,14,14);
            skin.window.normal=skin.box.normal;skin.window.onNormal=skin.box.normal;skin.window.border=b;
            var bb=new RectOffset(6,6,6,6);var btn=skin.button;btn.border=bb;btn.padding=new RectOffset(14,14,9,9);btn.margin=new RectOffset(4,4,4,4);btn.fontStyle=FontStyle.Bold;btn.wordWrap=true;btn.alignment=TextAnchor.MiddleCenter;
            btn.normal=State(Rounded(16,4,NativeUi.Hex(0x222329),NativeUi.Hex(0x484952)),bright);btn.hover=State(Rounded(16,4,NativeUi.Hex(0x303138),NativeUi.Hex(0xff784b)),bright);
            btn.active=State(Rounded(16,4,NativeUi.Hex(0x1c1d22),accent),bright);btn.focused=btn.hover;btn.onNormal=State(Rounded(16,4,NativeUi.Hex(0x36251f),accent),bright);btn.onHover=btn.onNormal;btn.onActive=btn.active;
            var tg=skin.toggle;tg.normal=State(Check(NativeUi.Hex(0x111216),NativeUi.Hex(0x5a5b66),false),text);tg.hover=State(Check(NativeUi.Hex(0x1a1b21),NativeUi.Hex(0xff784b),false),bright);
            tg.onNormal=State(Check(accent,accent,true),bright);tg.onHover=State(Check(NativeUi.Hex(0xff865b),NativeUi.Hex(0xff865b),true),bright);tg.active=tg.hover;tg.onActive=tg.onHover;
            tg.border=new RectOffset(23,1,0,0);tg.padding=new RectOffset(28,4,1,1);tg.overflow=new RectOffset(0,0,0,0);tg.fixedHeight=18;tg.margin=new RectOffset(4,4,5,5);tg.wordWrap=false;
            var tf=skin.textField;tf.normal=State(Rounded(16,4,NativeUi.Hex(0x111216),NativeUi.Hex(0x45464e)),bright);tf.hover=State(Rounded(16,4,NativeUi.Hex(0x15161b),NativeUi.Hex(0x5a5b66)),bright);tf.focused=State(Rounded(16,4,NativeUi.Hex(0x15161b),accent),bright);tf.border=bb;tf.padding=new RectOffset(10,10,7,7);
            skin.textArea.normal=tf.normal;skin.textArea.hover=tf.hover;skin.textArea.focused=tf.focused;skin.textArea.border=bb;skin.textArea.padding=tf.padding;
            skin.horizontalSlider.normal=State(Rounded(12,3,NativeUi.Hex(0x303138),NativeUi.Hex(0x303138)),text);skin.horizontalSlider.border=new RectOffset(4,4,4,4);skin.horizontalSlider.fixedHeight=6;skin.horizontalSlider.margin=new RectOffset(4,4,10,10);
            var thumb=skin.horizontalSliderThumb;thumb.normal=State(Circle(16,accent,NativeUi.Hex(0xffb15c)),text);thumb.hover=State(Circle(16,NativeUi.Hex(0xff865b),Color.white),text);thumb.active=thumb.hover;thumb.fixedWidth=16;thumb.fixedHeight=16;thumb.border=new RectOffset(0,0,0,0);thumb.overflow=new RectOffset(0,0,5,5);
            skin.verticalScrollbar.normal=State(Rounded(8,4,new Color(1,1,1,.06f),new Color(1,1,1,.06f)),text);skin.verticalScrollbar.fixedWidth=8;skin.verticalScrollbar.border=new RectOffset(3,3,3,3);
            skin.verticalScrollbarThumb.normal=State(Rounded(8,4,NativeUi.Hex(0x45464e),NativeUi.Hex(0x45464e)),text);skin.verticalScrollbarThumb.fixedWidth=8;skin.verticalScrollbarThumb.border=new RectOffset(3,3,3,3);
            skin.horizontalScrollbar.normal=State(Rounded(8,4,new Color(1,1,1,.06f),new Color(1,1,1,.06f)),text);skin.horizontalScrollbar.fixedHeight=8;skin.horizontalScrollbarThumb.normal=State(Rounded(8,4,NativeUi.Hex(0x45464e),NativeUi.Hex(0x45464e)),text);skin.horizontalScrollbarThumb.fixedHeight=8;
            skin.label.normal.textColor=text;skin.label.wordWrap=true;
            skin.settings.selectionColor=NativeUi.Hex(0xff6b35,.45f);skin.settings.cursorColor=accent;
            Muted=muted;
        }
        public static Color Muted{get;private set;}
    }
}
