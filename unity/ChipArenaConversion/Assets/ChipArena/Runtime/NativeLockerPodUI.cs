using System;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Locker's Pod tab (owner 2026-11-02: "We can probably make the pods customizable too via customization"): your
    /// Expedition drop pod's paint, trim and lights (Core/NativePodDrop.cs's look map, kept in prefs.pod), each with an Auto
    /// swatch (the default: your body colour, gunmetal, your accent), and a preview card of the pod in those colours with its
    /// LED sign spelling your callsign as the hub will (the real sign texture). Hovering a swatch tries it on the card. Your
    /// own pod in a run wears it (ArenaPresentation.LocalPodLook); carrying it to the other pilots is Astra's (the seats).
    /// </summary>
    public sealed partial class ConversionGame
    {
        private string lockerPodTry;private Texture2D lockerPodSign;private string lockerPodSignFor;
        private const float LkPodCardH=232;
        private static readonly int[] PodLights={0x70e1d0,0xff6b35,0xffd27a,0x7ed78a,0x69a6ff,0xba94f5,0xff6575,0x65dce0,0xf18dcc,0xffa565,0xb8aabb,0xffffff};
        private int LkPodPer=>Mathf.Clamp(Mathf.FloorToInt((lockerW+8)/38),4,13);
        private float LkPodRowH(int count){int per=LkPodPer,rows=(count+per-1)/per;return rows*38-4;}
        private void LkPodLayout()
        {
            LkHead(true);LkReserve(LkPodCardH);
            LkHead();LkReserve(LkPodRowH(NativePodDrop.Paints.Length+1));LkHead();LkReserve(LkPodRowH(NativePodDrop.Paints.Length+1));LkHead();LkReserve(LkPodRowH(PodLights.Length+1));
            GUILayout.Space(LkGap);LkReserve(36);
        }
        private void LkPod(int k,float e)
        {
            prefs.pod=NativePodDrop.Normalize(prefs.pod);bool repaint=Event.current.type==EventType.Repaint;if(repaint)lockerPodTry=null;
            LkHeading(lockerSlots[k++],"YOUR POD","How you drop into the Expedition hub, and how you leave it");var card=lockerSlots[k++];
            LkHeading(lockerSlots[k++],"PAINT","The hull and nose");LkPodRow(lockerSlots[k++],"paint",NativePodDrop.Paints,8200,e);
            LkHeading(lockerSlots[k++],"TRIM","Bands, fins and the landing legs");LkPodRow(lockerSlots[k++],"trim",NativePodDrop.Paints,8230,e);
            LkHeading(lockerSlots[k++],"LIGHTS","Seams, beacon and your name sign");LkPodRow(lockerSlots[k++],"light",PodLights,8260,e);
            var reset=lockerSlots[k++];
            if(U.Btn(new Rect(reset.x,reset.y,150,32),"Reset to Auto",false,!lockerBlocked&&prefs.pod.Length>0,e,12)){prefs.pod="";Click();}
            LkPodCard(card,lockerPodTry??prefs.pod,e);
        }
        // A row of swatches for one entry: Auto first (the default), then the presets; the picked one ringed.
        private void LkPodRow(Rect r,string key,int[] presets,int hoverBase,float e)
        {
            int per=LkPodPer,current=NativePodDrop.Of(prefs.pod,key);
            for(int i=0;i<=presets.Length;i++)
            {
                int rgb=i==0?-1:presets[i-1];var s=new Rect(r.x+2+(i%per)*38,r.y+(i/per)*38,30,30);bool on=current==rgb;bool hot=lockerViewHot&&U.Hover(s);float h=U.HoverAmount(hoverBase+i,s,hot);
                var g=new Rect(s.x-2*h,s.y-2*h,s.width+4*h,s.height+4*h);if(on)U.RadialStops(new Rect(g.x-5,g.y-5,g.width+10,g.height+10),U.Hex(0xff6b35,e),LkRingAt,LkRingA);
                if(i==0){U.RadialStops(g,U.A(U.Hex(0x2a2d36),e),LkDiscAt,LkDiscA);U.Text(g,"AUTO",8,U.Hex(0xe4e4e7,e),TextAnchor.MiddleCenter,bold:true,mono:true);}
                else U.RadialStops(g,U.A(U.Hex(rgb),e),LkDiscAt,LkDiscA);
                U.RadialStops(g,U.A(U.Mix(new Color(1,1,1,.25f),U.Hex(0xffc590),h),e),LkRingAt,LkRingA);
                if(hot&&Event.current.type==EventType.Repaint)lockerPodTry=NativePodDrop.With(prefs.pod,key,rgb);
                if(!lockerBlocked&&hot&&U.Button(s)&&!on){prefs.pod=NativePodDrop.With(prefs.pod,key,rgb);GUIUtility.keyboardControl=0;Click();}
            }
        }
        // The preview card: the pod front on, in its paint, trim and lights, its door shut, the LED sign over it.
        private void LkPodCard(Rect r,string look,float e)
        {
            if(Event.current.type!=EventType.Repaint)return;
            NativePodDrop.Colors(look,prefs.bodyColor,prefs.accentColor,out int paintRgb,out int trimRgb,out int lightRgb);
            Color paint=U.A(U.Hex(paintRgb),e),trim=U.A(U.Hex(trimRgb),e),light=U.A(U.Hex(lightRgb),e),frame=U.Hex(0x15181f,e),dark=U.A(U.Mix(U.Hex(paintRgb),Color.black,.35f),e);
            U.Fill(r,U.Hex(0x0d0f14,.85f*e));U.Fill(new Rect(r.x,r.yMax-26,r.width,26),U.Hex(0x171a22,e));
            float cx=r.center.x,base_=r.yMax-30,w=118,hull=118,top=base_-hull;
            // the legs and the base band, the hull (paint) with its seams (lights) and trim bands, the doorway and door
            foreach(int sg in new[]{-1,1}){U.Fill(new Rect(cx+sg*(w/2+6)-(sg>0?0:10),base_-58,10,58),trim);U.Fill(new Rect(cx+sg*(w/2+10)-(sg>0?0:22),base_-5,22,5),frame);}
            U.Fill(new Rect(cx-w/2,top,w,hull),paint);
            foreach(float f in new[]{-.36f,.36f})U.Fill(new Rect(cx+f*w-1.5f,top+6,3,hull-12),light);
            foreach(float v in new[]{.3f,.86f})U.Fill(new Rect(cx-w/2,top+hull*v,w,5),trim);
            U.Fill(new Rect(cx-w/2-4,base_-12,w+8,12),trim);U.Fill(new Rect(cx-w/2-3,base_-4,w+6,2),light);
            U.Fill(new Rect(cx-24,top+18,48,hull-30),frame);U.Fill(new Rect(cx-21,top+21,42,hull-36),dark);U.Fill(new Rect(cx-10,top+hull*.5f,20,3),light);U.Fill(new Rect(cx-8,top+30,16,6),frame);
            // the nose (stepped), its collar and the beacon
            for(int i=0;i<6;i++){float nw=w*(1-.14f*i)-8,y=top-(i+1)*9;U.Fill(new Rect(cx-nw/2,y,nw,9),paint);}
            U.Fill(new Rect(cx-w/2-4,top-6,w+8,8),trim);U.Fill(new Rect(cx-14,top-60,28,5),trim);U.Fill(new Rect(cx-4,top-68,8,8),light);
            // the LED sign over the door: the hub's own dot texture for this callsign
            var sign=new Rect(cx-w/2+4,top+2,w-8,15);U.Fill(new Rect(sign.x-3,sign.y-3,sign.width+6,sign.height+6),frame);
            var tex=LkPodSign();if(tex!=null)U.Image(sign,tex,new Color(light.r*1.15f,light.g*1.15f,light.b*1.15f,e));
            U.Text(new Rect(r.x+12,r.yMax-24,r.width-24,20),NativePodDrop.Of(look,"paint")<0&&NativePodDrop.Of(look,"trim")<0&&NativePodDrop.Of(look,"light")<0?"AUTO · YOUR BODY COLOUR AND ACCENT":"CUSTOM",10,U.Hex(0x8f909a,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
        }
        // The sign as the hub spells the callsign, laid along x for the card (the first ten letters; the hub scrolls longer names).
        private Texture2D LkPodSign()
        {
            string text=NativePodDrop.SignText(prefs.name);if(lockerPodSign!=null&&lockerPodSignFor==text)return lockerPodSign;
            if(lockerPodSign!=null)CapturedAnimation.Release(lockerPodSign);
            var px=NativePodDrop.SignPixelsFlat(text,4,out int w,out int h);var c=new Color32[w*h];for(int i=0;i<c.Length;i++)c[i]=new Color32(px[i*4],px[i*4+1],px[i*4+2],px[i*4+3]);
            lockerPodSign=new Texture2D(w,h,TextureFormat.RGBA32,false,false){name="Locker pod sign",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            lockerPodSign.SetPixels32(c);lockerPodSign.Apply(false,true);lockerPodSignFor=text;return lockerPodSign;
        }
    }
}
