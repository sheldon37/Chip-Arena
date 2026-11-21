using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using Cyber=ChipArena.UnityConversion.NativeCyberTheme;
namespace ChipArena.UnityConversion
{
    // The Locker's HAT COLOUR and BACKDROP COLOUR pickers (Core/NativeHatLooks.cs; owner 2026-10-12: "Lets make Hats
    // color-customizable and backdrop (Instead of autosetting)"), in the cyber palette (NativeCyberTheme). Each offers AUTO
    // (the look as it was: the hat's own colours, the backdrop in the accent), twelve swatches and a custom colour from a hue
    // track and a shade track (black through the full colour to a pastel). Hovering AUTO or a swatch tries it on the stage;
    // a click or a drag sets prefs.hatColor / prefs.backColor ("" or #rrggbb), saved and sent by Save Pilot like the backdrop.
    // Drawn inside the Locker's scroll clip: unrotated images and fills only, no per-frame allocation (the two track
    // textures are made once; the shade track is refilled only when its hue changes).
    public sealed partial class ConversionGame
    {
        // This repaint's hovered pick (null: none; "" AUTO), and the custom tracks' positions.
        private string lockerHatColor,lockerBackColor;
        private readonly float[] lkHue={.53f,.53f},lkShade={.5f,.5f},lkShadeFor={-1,-1};private readonly string[] lkSlidFrom={null,null};
        private Texture2D lkHueTrack;private readonly Texture2D[] lkShadeTrack=new Texture2D[2];private readonly Color[] lkShadePixels=new Color[96];
        private const float LkPickAuto=78,LkPickPitch=38,LkPickSwatch=30,LkPickTrack=22,LkPickInfo=150;
        private int LkPickPer=>Mathf.Max(4,Mathf.FloorToInt((lockerW-LkPickAuto+8)/LkPickPitch));
        private float LkPickerH(){int n=NativeHatLooks.HatSwatches.Length,per=LkPickPer,rows=(n+per-1)/per;return rows*LkPickPitch+6+2*LkPickTrack+8;}
        private static Color LkLinear(int rgb)=>U.Hex(rgb);
        private Texture2D LkTrack(int which,float hue)
        {
            const int n=96;
            if(which<0)
            {
                if(lkHueTrack!=null)return lkHueTrack;var px=new Color[n];for(int i=0;i<n;i++)px[i]=U.Hex(NativeHatLooks.FromHsv(i/(n-1f),1,1));
                lkHueTrack=new Texture2D(n,1,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};lkHueTrack.SetPixels(px);lkHueTrack.Apply(false,false);return lkHueTrack;
            }
            var t=lkShadeTrack[which];
            if(t==null){t=new Texture2D(n,1,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};lkShadeTrack[which]=t;lkShadeFor[which]=-1;}
            if(lkShadeFor[which]!=hue)
            {
                lkShadeFor[which]=hue;var px=lkShadePixels;for(int i=0;i<n;i++)px[i]=U.Hex(NativeHatLooks.Custom(hue,i/(n-1f)));
                t.SetPixels(px);t.Apply(false,false);
            }
            return t;
        }
        // The section's hint: what AUTO means here, or why the hat picker is off.
        private string LkPickerHint(bool hatSide)
        {
            if(!hatSide)return "Auto follows your accent colour";string hat=prefs.hat;
            if(!NativeHatLooks.Colorable(hat))return hat=="none"?"Pick headwear above to colour it":"This headwear keeps its own colours";
            return hat==NativeHatLooks.SheldonHead?"Tints the glow; Auto is cyan and the hood stays dark":"Auto keeps the hat's own colours";
        }
        private void LkColorPicker(Rect r,bool hatSide)
        {
            float e=lockerFade;int side=hatSide?0:1;var ev=Event.current;bool repaint=ev.type==EventType.Repaint;
            bool enabled=!hatSide||NativeHatLooks.Colorable(prefs.hat);float a=enabled?e:.35f*e;
            string current=NativeHatLooks.Normalize(hatSide?prefs.hatColor:prefs.backColor);var swatches=hatSide?NativeHatLooks.HatSwatches:NativeHatLooks.BackSwatches;
            // Keep the tracks where the picked colour is (a swatch or a saved colour moves them; a drag sets both).
            if(current.Length>0&&current!=lkSlidFrom[side]){NativeHatLooks.Decompose(NativeHatLooks.Rgb(current),out lkHue[side],out lkShade[side]);lkSlidFrom[side]=current;}
            // AUTO.
            var auto=new Rect(r.x+2,r.y,LkPickAuto-12,LkPickSwatch);bool autoOn=current.Length==0,hot=enabled&&lockerViewHot&&U.Hover(auto);float h=U.HoverAmount(8800+side*40,auto,hot);
            if(autoOn)U.Glow(new Rect(auto.x-2,auto.y-2,auto.width+4,auto.height+4),Cyber.Cyan(.22f*a),8);
            U.Round(auto,U.A(U.Mix(Cyber.Panel(),Cyber.Indigo(),h),a),6,autoOn?Cyber.Cyan(a):U.A(U.Mix(Cyber.Edge(),Cyber.CyanSoft(),h),a),autoOn?2:1);
            U.Text(auto,"AUTO",11,autoOn?Cyber.Ink(a):U.A(U.Mix(Cyber.Muted(),Cyber.Ink(),h),a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.14f);
            if(hot&&repaint){if(hatSide)lockerHatColor="";else lockerBackColor="";}
            if(enabled&&!lockerBlocked&&hot&&U.Button(auto)&&!autoOn){LkSetPick(hatSide,"");Click();}
            // The swatches.
            int per=LkPickPer;
            for(int i=0;i<swatches.Length;i++)
            {
                var s=new Rect(r.x+LkPickAuto+(i%per)*LkPickPitch,r.y+(i/per)*LkPickPitch,LkPickSwatch,LkPickSwatch);bool on=current==swatches[i];bool sh=enabled&&lockerViewHot&&U.Hover(s);float sv=U.HoverAmount(8801+side*40+i,s,sh);
                var g=new Rect(s.x-2*sv,s.y-2*sv,s.width+4*sv,s.height+4*sv);
                if(on)U.RadialStops(new Rect(g.x-5,g.y-5,g.width+10,g.height+10),Cyber.Cyan(a),LkRingAt,LkRingA);
                U.RadialStops(g,U.A(LkColor(swatches[i],0xffffff),a),LkDiscAt,LkDiscA);U.RadialStops(g,U.A(U.Mix(new Color(1,1,1,.22f),Cyber.CyanSoft(),sv),a),LkRingAt,LkRingA);
                if(sh&&repaint){if(hatSide)lockerHatColor=swatches[i];else lockerBackColor=swatches[i];}
                if(enabled&&!lockerBlocked&&sh&&U.Button(s)&&!on){LkSetPick(hatSide,swatches[i]);Click();}
            }
            // CUSTOM: the hue and shade tracks, then the colour and its hex.
            int rows=(swatches.Length+per-1)/per;float ty=r.y+rows*LkPickPitch+2,tw=Mathf.Max(120,lockerW-LkPickAuto-LkPickInfo-8);
            U.Text(new Rect(r.x+2,ty,LkPickAuto-12,LkPickTrack),"CUSTOM",10,Cyber.Muted(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            var hue=new Rect(r.x+LkPickAuto,ty,tw,LkPickTrack-6);var shade=new Rect(r.x+LkPickAuto,ty+LkPickTrack+2,tw,LkPickTrack-6);
            bool custom=current.Length>0&&System.Array.IndexOf(swatches,current)<0;
            LkTrackDraw(hue,LkTrack(-1,0),lkHue[side],a,custom);LkTrackDraw(shade,LkTrack(side,lkHue[side]),lkShade[side],a,custom);
            if(enabled&&!lockerBlocked)
            {
                if(LkTrackDrag(hue,8890+side*2,ref lkHue[side])||LkTrackDrag(shade,8891+side*2,ref lkShade[side]))
                {var pick=NativeHatLooks.Hex(NativeHatLooks.Custom(lkHue[side],lkShade[side]));lkSlidFrom[side]=pick;LkSetPick(hatSide,pick);}
            }
            var info=new Rect(hue.xMax+14,ty,LkPickInfo-14,2*LkPickTrack);string shown=(hatSide?lockerHatColor:lockerBackColor)??current;
            int rgb=NativeHatLooks.Rgb(shown);var disc=new Rect(info.x,info.center.y-14,28,28);
            if(rgb>=0)U.RadialStops(disc,U.A(LkLinear(rgb),a),LkDiscAt,LkDiscA);else U.RadialStops(disc,Cyber.Edge(a),LkRingAt,LkRingA);
            U.Text(new Rect(disc.xMax+10,info.y,info.width-38,info.height),rgb>=0?shown:"AUTO",13,rgb>=0?Cyber.Ink(a):Cyber.Muted(a),TextAnchor.MiddleLeft,mono:true,tracking:.06f);
        }
        private void LkSetPick(bool hatSide,string value){if(hatSide)prefs.hatColor=value;else prefs.backColor=value;GUIUtility.keyboardControl=0;}
        // A track: its gradient in a rounded cyber frame and the thumb at `value` (lit while the pick is a custom colour).
        private static void LkTrackDraw(Rect r,Texture2D track,float value,float a,bool lit)
        {
            U.Round(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),Cyber.Void(.9f*a),5,Cyber.Edge(a));U.Image(r,track,new Color(1,1,1,a));
            float x=r.x+Mathf.Clamp01(value)*r.width;var thumb=new Rect(x-4,r.y-4,8,r.height+8);U.Round(thumb,Cyber.Ink(a),3,lit?Cyber.Cyan(a):Cyber.Dim(a),lit?2:1);
        }
        // Press or drag on a track: the value under the pointer (the pointer is captured while held, so a drag can leave it).
        private static bool LkTrackDrag(Rect r,int hint,ref float value)
        {
            var ev=Event.current;int id=GUIUtility.GetControlID(hint,FocusType.Passive);var m=GUI.matrix;GUI.matrix=Matrix4x4.identity;var p=U.P(new Rect(r.x-4,r.y-6,r.width+8,r.height+12));float mx=ev.mousePosition.x;bool over=p.Contains(ev.mousePosition);GUI.matrix=m;
            switch(ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:if(ev.button==0&&over){GUIUtility.hotControl=id;value=Mathf.Clamp01((mx-p.x-4*U.S)/Mathf.Max(1,p.width-8*U.S));ev.Use();return true;}break;
                case EventType.MouseDrag:if(GUIUtility.hotControl==id){value=Mathf.Clamp01((mx-p.x-4*U.S)/Mathf.Max(1,p.width-8*U.S));ev.Use();return true;}break;
                case EventType.MouseUp:if(GUIUtility.hotControl==id){GUIUtility.hotControl=0;ev.Use();}break;
            }
            return false;
        }
    }
}
