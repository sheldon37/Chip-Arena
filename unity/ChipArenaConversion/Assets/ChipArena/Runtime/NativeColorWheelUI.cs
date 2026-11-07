using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using Cyber=ChipArena.UnityConversion.NativeCyberTheme;
namespace ChipArena.UnityConversion
{
    // The Locker's SKIN TONE section (Appearance, while the model draws: LkOnModel; owner 2026-10-28: "Give skin color
    // customization, along with a 'Color selector circle' for more color options"), in the cyber palette of the hat colour
    // picker (NativeHatColorUI.cs): DEFAULT (the model's own skin), the ten tones light to deep (Core NativeLook.SkinTones) and
    // the colour wheel's toggle. Open, the wheel (Core NativeColorWheel: hue round the disc, saturation out from its centre)
    // sits under them with a brightness track, the picked colour and its hex. Hovering DEFAULT or a tone tries it on the
    // stage (lockerToneTry, read by LookShown); a click, or a press or drag on the wheel or the track, sets the skin entry of
    // prefs.look (NativeLook.WithSkin), which Save Pilot keeps and Revert undoes with the rest of the look. The model takes a
    // new tone in place (Runtime/NativeVisualBodyPresentation.cs VisualRetone), so a drag makes no new body.
    // Drawn inside the Locker's scroll clip: unrotated images and fills only, no per-frame allocation (the disc texture is made
    // once; the brightness track is refilled only when the hue or saturation moves).
    public sealed partial class ConversionGame
    {
        // This repaint's hovered tone (-2 none, -1 DEFAULT), whether the wheel is open, and the wheel's position (kept off the
        // picked colour while it drags, so the thumb does not snap to the nearest 8-bit colour).
        private int lockerToneTry=-2,lkToneFrom=-2;private bool lkWheelOpen;
        private float lkToneHue,lkToneSat,lkToneVal,lkTrackHue=-1,lkTrackSat=-1;
        private Texture2D lkWheel,lkToneTrack;private readonly Color[] lkTonePixels=new Color[96];
        private const float LkWheelSize=148,LkWheelPad=12,LkToneInfo=170;
        private int LkTonePer=>Mathf.Max(4,Mathf.FloorToInt((lockerW-LkPickAuto+8)/LkPickPitch));
        private int LkToneRows{get{int n=NativeLook.SkinTones.Length+1,per=LkTonePer;return (n+per-1)/per;}}
        private float LkToneH()=>LkToneRows*LkPickPitch-(LkPickPitch-LkPickSwatch)+(lkWheelOpen?8+LkWheelSize+2*LkWheelPad:0);
        // The section's hint: what the picked tone is.
        private string LkToneHint()
        {
            int tone=NativeLook.SkinOf(prefs.look),k=System.Array.IndexOf(NativeLook.SkinTones,tone);
            return tone<0?"Default keeps the model's own skin":k>=0?NativeLook.SkinToneLabels[k]:"A custom colour from the wheel";
        }
        /// <summary>The skin the Locker's model thumbnails show: the picked tone, else the model's own.</summary>
        private int LkToneShown(){int t=NativeLook.SkinOf(prefs.look);return t<0?NativeColorWheel.ModelSkin:t;}
        // The one setter: the skin entry of prefs.look (a negative `rgb` removes it). A preset or DEFAULT moves the wheel to it;
        // a drag keeps the wheel where the pointer put it.
        private void SetSkinTone(int rgb,bool fromWheel=false){prefs.look=NativeLook.WithSkin(prefs.look,rgb);if(fromWheel)lkToneFrom=rgb;GUIUtility.keyboardControl=0;}
        private Texture2D LkWheelTexture()
        {
            if(lkWheel!=null)return lkWheel;const int n=160;var px=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                // Texture rows run bottom to top, as the wheel's y does; the rim is anti-aliased over one texel.
                float u=(x+.5f)/n*2-1,v=(y+.5f)/n*2-1,r=Mathf.Sqrt(u*u+v*v);NativeColorWheel.FromPoint(u,v,out float hue,out float sat);
                var c=U.Hex(NativeColorWheel.Rgb(hue,sat,1));c.a=Mathf.Clamp01((1-r)*n*.5f+.5f);px[y*n+x]=c;
            }
            lkWheel=new Texture2D(n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};lkWheel.SetPixels(px);lkWheel.Apply(false,false);
            return lkWheel;
        }
        // The brightness track: black to the wheel's colour at full brightness.
        private Texture2D LkToneTrackTexture()
        {
            const int n=96;
            if(lkToneTrack==null){lkToneTrack=new Texture2D(n,1,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};lkTrackHue=-1;}
            if(lkTrackHue!=lkToneHue||lkTrackSat!=lkToneSat)
            {
                lkTrackHue=lkToneHue;lkTrackSat=lkToneSat;for(int i=0;i<n;i++)lkTonePixels[i]=U.Hex(NativeColorWheel.Rgb(lkToneHue,lkToneSat,i/(n-1f)));
                lkToneTrack.SetPixels(lkTonePixels);lkToneTrack.Apply(false,false);
            }
            return lkToneTrack;
        }
        private void LkSkinTone(Rect r)
        {
            float e=lockerFade;bool repaint=Event.current.type==EventType.Repaint;int current=NativeLook.SkinOf(prefs.look);var tones=NativeLook.SkinTones;
            // The wheel follows the picked tone (a preset, a saved tone or DEFAULT, which shows the model's own skin there).
            if(current!=lkToneFrom){lkToneFrom=current;NativeColorWheel.Hsv(current<0?NativeColorWheel.ModelSkin:current,out lkToneHue,out lkToneSat,out lkToneVal);}
            // DEFAULT.
            var auto=new Rect(r.x+2,r.y,LkPickAuto-12,LkPickSwatch);bool autoOn=current<0,hot=lockerViewHot&&U.Hover(auto);float h=U.HoverAmount(8950,auto,hot);
            if(autoOn)U.Glow(new Rect(auto.x-2,auto.y-2,auto.width+4,auto.height+4),Cyber.Cyan(.22f*e),8);
            U.Round(auto,U.A(U.Mix(Cyber.Panel(),Cyber.Indigo(),h),e),6,autoOn?Cyber.Cyan(e):U.A(U.Mix(Cyber.Edge(),Cyber.CyanSoft(),h),e),autoOn?2:1);
            U.Text(auto,"DEFAULT",10,autoOn?Cyber.Ink(e):U.A(U.Mix(Cyber.Muted(),Cyber.Ink(),h),e),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);
            if(hot&&repaint)lockerToneTry=-1;
            if(!lockerBlocked&&hot&&U.Button(auto)&&!autoOn){SetSkinTone(-1);Click();}
            // The tones, then the wheel's toggle (lit while open; a custom colour shows in its centre).
            int per=LkTonePer;
            for(int i=0;i<=tones.Length;i++)
            {
                var s=new Rect(r.x+LkPickAuto+(i%per)*LkPickPitch,r.y+(i/per)*LkPickPitch,LkPickSwatch,LkPickSwatch);bool wheel=i==tones.Length;
                bool on=wheel?lkWheelOpen:current==tones[i],sh=lockerViewHot&&U.Hover(s);float sv=U.HoverAmount(8951+i,s,sh);
                var g=new Rect(s.x-2*sv,s.y-2*sv,s.width+4*sv,s.height+4*sv);
                if(on)U.RadialStops(new Rect(g.x-5,g.y-5,g.width+10,g.height+10),Cyber.Cyan(e),LkRingAt,LkRingA);
                if(wheel)
                {
                    U.Image(g,LkWheelTexture(),new Color(1,1,1,e));bool custom=current>=0&&System.Array.IndexOf(tones,current)<0;
                    var dot=new Rect(g.center.x-g.width*.22f,g.center.y-g.height*.22f,g.width*.44f,g.height*.44f);
                    U.RadialStops(dot,custom?U.A(U.Hex(current),e):Cyber.Void(.85f*e),LkDiscAt,LkDiscA);if(!custom)NativeIcons.Draw(new Rect(dot.center.x-5,dot.center.y-5,10,10),lkWheelOpen?"chevrons-down-up":"palette",Cyber.Ink(e));
                }
                else U.RadialStops(g,U.A(U.Hex(tones[i]),e),LkDiscAt,LkDiscA);
                U.RadialStops(g,U.A(U.Mix(new Color(1,1,1,.22f),Cyber.CyanSoft(),sv),e),LkRingAt,LkRingA);
                if(sh&&repaint&&!wheel)lockerToneTry=tones[i];
                if(!lockerBlocked&&sh&&U.Button(s)){if(wheel){lkWheelOpen=!lkWheelOpen;Click();}else if(!on){SetSkinTone(tones[i]);Click();}}
            }
            if(!lkWheelOpen)return;
            // The wheel: the disc at the picked brightness, its thumb, the brightness track and the picked colour.
            var panel=new Rect(r.x,r.y+LkToneRows*LkPickPitch-(LkPickPitch-LkPickSwatch)+8,lockerW,LkWheelSize+2*LkWheelPad);
            U.Round(panel,Cyber.Void(.55f*e),10,Cyber.Edge(e));
            var disc=new Rect(panel.x+LkWheelPad+4,panel.y+LkWheelPad,LkWheelSize,LkWheelSize);bool custom2=current>=0&&System.Array.IndexOf(tones,current)<0;
            U.RadialStops(new Rect(disc.x-4,disc.y-4,disc.width+8,disc.height+8),Cyber.Edge(e),LkRingAt,LkRingA);
            float v=Mathf.Lerp(.08f,1,lkToneVal);U.Image(disc,LkWheelTexture(),new Color(v,v,v,e));
            NativeColorWheel.Point(lkToneHue,lkToneSat,out float px,out float py);var at=new Vector2(disc.center.x+px*disc.width*.5f,disc.center.y-py*disc.height*.5f);
            U.RadialStops(new Rect(at.x-10,at.y-10,20,20),Cyber.Ink(e),LkRingAt,LkRingA);U.RadialStops(new Rect(at.x-6,at.y-6,12,12),U.A(U.Hex(NativeColorWheel.Rgb(lkToneHue,lkToneSat,lkToneVal)),e),LkDiscAt,LkDiscA);
            float tx=disc.xMax+24,tw=Mathf.Max(90,panel.xMax-tx-LkWheelPad-6);
            U.Text(new Rect(tx,disc.y+6,tw,16),"BRIGHTNESS",10,Cyber.Muted(e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            var track=new Rect(tx,disc.y+28,tw,LkPickTrack-6);LkTrackDraw(track,LkToneTrackTexture(),lkToneVal,e,custom2);
            int shown=current>=0?current:NativeColorWheel.ModelSkin;var swatch=new Rect(tx,track.yMax+22,40,40);
            U.RadialStops(swatch,U.A(U.Hex(shown),e),LkDiscAt,LkDiscA);U.RadialStops(swatch,U.A(new Color(1,1,1,.22f),e),LkRingAt,LkRingA);
            U.Text(new Rect(swatch.xMax+12,swatch.y+2,tw-52,18),current>=0?NativeLook.SkinHex(current):"DEFAULT",13,Cyber.Ink(e),TextAnchor.MiddleLeft,mono:true,tracking:.06f);
            U.Text(new Rect(swatch.xMax+12,swatch.y+20,tw-52,16),custom2?"Custom":current>=0?NativeLook.SkinToneLabels[System.Array.IndexOf(tones,current)]:"The model's own skin",11,Cyber.Muted(e),TextAnchor.MiddleLeft);
            if(lockerBlocked)return;
            bool moved=LkWheelDrag(disc,8970,ref lkToneHue,ref lkToneSat);
            if(LkTrackDrag(track,8971,ref lkToneVal))moved=true;
            if(moved)SetSkinTone(NativeColorWheel.Rgb(lkToneHue,lkToneSat,lkToneVal),true);
        }
        // Press or drag on the wheel: the hue and saturation under the pointer (captured while held, so a drag past the rim
        // keeps the rim's).
        private static bool LkWheelDrag(Rect r,int hint,ref float hue,ref float saturation)
        {
            var ev=Event.current;int id=GUIUtility.GetControlID(hint,FocusType.Passive);var m=GUI.matrix;GUI.matrix=Matrix4x4.identity;var p=U.P(r);var mouse=ev.mousePosition;GUI.matrix=m;
            float radius=Mathf.Max(1,p.width*.5f),dx=(mouse.x-p.center.x)/radius,dy=(p.center.y-mouse.y)/radius;
            switch(ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:if(ev.button==0&&dx*dx+dy*dy<=1.1f){GUIUtility.hotControl=id;NativeColorWheel.FromPoint(dx,dy,out hue,out saturation);ev.Use();return true;}break;
                case EventType.MouseDrag:if(GUIUtility.hotControl==id){NativeColorWheel.FromPoint(dx,dy,out hue,out saturation);ev.Use();return true;}break;
                case EventType.MouseUp:if(GUIUtility.hotControl==id){GUIUtility.hotControl=0;ev.Use();}break;
            }
            return false;
        }
    }
}
