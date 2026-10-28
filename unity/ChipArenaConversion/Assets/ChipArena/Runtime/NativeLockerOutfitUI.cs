using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Locker's OUTFIT section (Appearance, while the model draws: LkOnModel; owner 2026-10-26: "integrate switching the
    // clothes on the model"): a picker row per slot of Core/NativeLook.cs (hair, top, bottoms, hands, shoes; NativeOutfitPicker
    // below); a step wears the slot's next or previous piece (prefs.look, through NativeLook.With), which Save Pilot keeps and
    // Revert undoes with the rest of the look. The pilot's model wears it in the lobby, the draft and matches
    // (Runtime/NativeVisualBodyPresentation.cs VisualDress); the Locker's own stage is the portrait pilot. The DEV pieces
    // (Sheldon's clothes and bare feet, each slot's last; Core NativeSheldonWear) show, marked DEV, only with live DEV access
    // (lockerBodySheldon, the Sheldon Clothes' gate), and the pilot wears its pick through that gate (LookShown).
    public sealed partial class ConversionGame
    {
        private readonly NativeOutfitPicker.Motion lockerOutfitMotion=new NativeOutfitPicker.Motion();
        // The pieces a slot lists: all of them with live DEV access, else those before its DEV pieces (each slot's last).
        private int LkOutfitCount(int slot){var ids=NativeLook.Ids[slot];int n=ids.Length;if(!lockerBodySheldon)while(n>1&&NativeLook.DevOnly(ids[n-1]))n--;return n;}
        private float LkOutfitH()=>NativeOutfitPicker.Height(lockerW,NativeLook.Slots.Length);
        private void LkOutfit(Rect grid)
        {
            for(int s=0;s<NativeLook.Slots.Length;s++)
            {
                string slot=NativeLook.Slots[s];var ids=NativeLook.Ids[s];int shown=LkOutfitCount(s),worn=NativeLook.Index(prefs.look,slot);if(worn>=shown)worn=0;
                int k=NativeOutfitPicker.Row(NativeOutfitPicker.Cell(grid,lockerW,s),s,worn,shown,lockerFade,lockerViewHot&&!lockerBlocked,Time.unscaledTime,lockerOutfitMotion);
                if(k!=worn){prefs.look=NativeLook.Gate(NativeLook.With(prefs.look,slot,ids[k]),lockerBodySheldon);Click();}
            }
        }
        // The outfit this pilot wears: prefs.look through the DEV gate (NativeLook.Gate), made again only when the pick or the
        // access changes (read every frame).
        private string lookShownFrom,lookShown="";private int lookShownDev=-1;
        private string LookShown()
        {
            var look=prefs.look??"";int dev=NativeBodySkins.LiveDeveloper(account?.Access??NativeAccess.Guest,System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())?1:0;
            if(!ReferenceEquals(look,lookShownFrom)||dev!=lookShownDev){lookShownFrom=look;lookShownDev=dev;lookShown=NativeLook.Gate(look,dev==1);}
            return lookShown;
        }
    }

    /// <summary>
    /// The OUTFIT picker (owner 2026-10-28: "It clutters a bit much with the buttons for clothing pieces (I do plan to add more,
    /// so maybe we can do some kind of Icon with arrows to the left and right?)", "Just want a very clean looking UI"): one row a
    /// slot, the same height however many pieces it holds. The slot's line icon and name on the left; on the right a carousel:
    /// the previous and next arrows (wrapping) around the worn piece's name, which slides in from the side it came from, a DEV
    /// tag on a DEV piece, and a dot a piece under it (the worn one a bar; past MaxDots a count instead) that jumps to that
    /// piece. The icon takes the Locker's accent while the slot wears something other than its default. Two columns once the
    /// Locker is wide enough. Engine-free beyond the IMGUI kit, so the offline UI harness draws this exact code.
    /// </summary>
    public static class NativeOutfitPicker
    {
        public const float RowH=52,Gap=10,TwoColumns=700,LabelW=118,ArrowW=34,CarouselW=300,MaxDots=14;
        /// <summary>Each slot's last step: when (presentation time) and which way, for the name's slide.</summary>
        public sealed class Motion{public readonly float[] At=new float[8],Dir=new float[8];public Motion(){for(int i=0;i<At.Length;i++)At[i]=-10;}}
        public static int Columns(float width)=>width>=TwoColumns?2:1;
        public static float Height(float width,int slots){int rows=(slots+Columns(width)-1)/Columns(width);return rows<=0?0:rows*RowH+(rows-1)*Gap;}
        public static Rect Cell(Rect grid,float width,int i)
        {
            int cols=Columns(width);float w=(width-(cols-1)*Gap)/cols;
            return new Rect(grid.x+(i%cols)*(w+Gap),grid.y+(i/cols)*(RowH+Gap),w,RowH);
        }

        // The slots' line icons (24-unit boxes, round 1.8 strokes in white, tinted when drawn): hair, top, bottoms, hands, shoes.
        private const string Stroke=" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";
        public static readonly string[] Icons={
            "<path d=\"M5.5 14C5 8.5 8 4.5 12 4.5s7 4 6.5 9.5\""+Stroke+"/><path d=\"M5.5 12.5c2.6.2 5.2-1 6.5-3.6 1.3 2.4 3.6 3.6 6.5 3.6\""+Stroke+"/><path d=\"M7.5 14v1.5a4.5 4.5 0 0 0 9 0V14\""+Stroke+"/>",
            "<path d=\"M9 4 4 6.5 2.5 11l3 1.5L7 11v9h10v-9l1.5 1.5 3-1.5L20 6.5 15 4c-.5 1.7-1.6 2.5-3 2.5S9.5 5.7 9 4Z\""+Stroke+"/>",
            "<path d=\"M7 3.5h10l1.5 17h-4.2L12 11l-2.3 9.5H5.5Z\""+Stroke+"/><path d=\"M6.8 6.5h10.4\""+Stroke+"/>",
            "<path d=\"M8 12.5V6a1.5 1.5 0 0 1 3 0v5\""+Stroke+"/><path d=\"M11 10V4.5a1.5 1.5 0 0 1 3 0V10\""+Stroke+"/><path d=\"M14 10.5V6a1.5 1.5 0 0 1 3 0v6c0 4.7-2.6 8-6.5 8-2.4 0-4-1.1-5.3-3l-2-3.1a1.5 1.5 0 0 1 2.4-1.8L8 14\""+Stroke+"/>",
            "<path d=\"M3 15.5V11l3.5-1 3 2.5 5 1.2c3.6.8 6.5 2.3 6.5 4.3v.5H3Z\""+Stroke+"/><path d=\"M3 18.5h18\""+Stroke+"/><path d=\"M10 12.8 11.2 11M12.6 13.3l1.1-1.7\""+Stroke+"/>"};
        private static readonly string[] IconKeys={"outfit-hair","outfit-top","outfit-bottoms","outfit-hands","outfit-shoes"};
        private const string Prev="<path d=\"m14.5 6-6 6 6 6\""+Stroke+"/>",Next="<path d=\"m9.5 6 6 6-6 6\""+Stroke+"/>";

        /// <summary>
        /// One slot's row in `r` (slot `s`, its `worn` piece of the `shown` it lists, faded by `e`): returns the piece the pilot
        /// picks this frame (`worn` without a click). `hot` is false while the Locker is blocked or the pointer is elsewhere.
        /// </summary>
        public static int Row(Rect r,int s,int worn,int shown,float e,bool hot,float now,Motion motion)
        {
            var ids=NativeLook.Ids[s];int pick=worn;bool custom=worn!=0;
            bool over=hot&&U.Hover(r);float h=U.HoverAmount(8400+s,r,over);
            U.Round(r,U.A(U.Mix(U.Hex(0x1c1d23),U.Hex(0x212228),h),e),10,U.A(U.Mix(U.Hex(0x30313a),U.Hex(0x45464f),h),e),1);
            // The slot: its icon on a tile, the accent while it wears something of its own, and its name.
            var tile=new Rect(r.x+10,r.y+(r.height-32)/2,32,32);
            U.Round(tile,custom?U.Hex(0x3a2a26,e):U.Hex(0x26272e,e),8,custom?U.Hex(0x6b3a26,e):U.Hex(0x34353d,e),1);
            if(s<Icons.Length)NativeIcons.DrawMarkup(new Rect(tile.x+6,tile.y+6,20,20),IconKeys[s],Icons[s],24,custom?U.Hex(0xff8a5c,e):U.Hex(0xb9b6c2,e));
            U.Text(new Rect(tile.xMax+10,r.y,LabelW-52,r.height),NativeLook.SlotLabels[s].ToUpperInvariant(),10,U.Hex(0x9d9ca6,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            // The carousel: the arrows round the piece's name and its dots.
            // (at most CarouselW wide, against the row's right edge, so the arrows stay close round the name on a wide Locker)
            float x1=r.xMax-8,x0=Mathf.Max(r.x+LabelW,x1-CarouselW);if(x1-x0<ArrowW*2+80)x0=x1-(ArrowW*2+80);
            var left=new Rect(x0,r.y+(r.height-ArrowW)/2,ArrowW,ArrowW);var right=new Rect(x1-ArrowW,left.y,ArrowW,ArrowW);
            if(Arrow(left,8410+s*2,Prev,e,hot&&shown>1)){pick=(worn-1+shown)%shown;motion.At[s]=now;motion.Dir[s]=-1;}
            if(Arrow(right,8411+s*2,Next,e,hot&&shown>1)){pick=(worn+1)%shown;motion.At[s]=now;motion.Dir[s]=1;}
            var stage=new Rect(left.xMax+6,r.y+5,right.x-left.xMax-12,r.height-10);
            // The name slides 14 px in from the side it came from and fades up over .18 s (a value per slot, no allocation).
            float k=Mathf.Clamp01((now-motion.At[s])/.18f),ease=1-(1-k)*(1-k)*(1-k),dx=(1-ease)*14*motion.Dir[s];
            string label=NativeLook.Labels[s][worn];bool dev=NativeLook.DevOnly(ids[worn]);float size=14;
            float tw=U.Measure(label,size,true).x,tagW=dev?34:0,total=tw+(dev?tagW+8:0),nx=stage.center.x-total/2+dx;
            U.Text(new Rect(nx,stage.y,tw+4,22),label,size,U.Hex(0xf4f4f5,e*(.35f+.65f*ease)),TextAnchor.MiddleLeft,bold:true);
            if(dev){var tag=new Rect(nx+tw+8,stage.y+4,tagW,15);U.Round(tag,U.Hex(0x3b2f12,e),4,U.Hex(0xb8860b,e),1);U.Text(tag,"DEV",9,U.Hex(0xffd479,e),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);}
            // A dot a piece (the worn one a bar), a click on one wears it; past MaxDots the place as a count.
            float dy=stage.y+29;
            if(shown<=MaxDots)
            {
                const float step=10,dot=5,bar=14;float width=(shown-1)*step+bar,dx0=stage.center.x-width/2;
                for(int i=0;i<shown;i++)
                {
                    float px=dx0+i*step+(i>worn?bar-dot:0);bool on=i==worn;var d=new Rect(px,dy,on?bar:dot,dot);
                    var hit=new Rect(px-2,dy-5,(on?bar:dot)+4,dot+10);bool dh=hot&&!on&&U.Hover(hit);
                    U.Round(d,on?U.Hex(0xff6b35,e):NativeLook.DevOnly(ids[i])?U.Hex(dh?0xffd479:0x8a6d2b,e):U.Hex(dh?0xd4d2dc:0x4a4b55,e),dot/2);
                    if(!on&&hot&&U.Button(hit)){pick=i;motion.At[s]=now;motion.Dir[s]=i>worn?1:-1;}
                }
            }
            else U.Text(new Rect(stage.x,dy-4,stage.width,14),(worn+1)+" / "+shown,10,U.Hex(0x8b8a94,e),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);
            return pick;
        }
        // A round arrow button: its chevron brightens and the ring fills on hover; dimmed when the slot has one piece.
        private static bool Arrow(Rect r,int id,string chevron,float e,bool enabled)
        {
            bool over=enabled&&U.Hover(r);float h=U.HoverAmount(id,r,over);
            U.Round(r,U.A(U.Mix(U.Hex(0x24252c),U.Hex(0x3a2a26),h),e),r.height/2,U.A(U.Mix(U.Hex(0x3a3b44),U.Hex(0xff6b35),h),e),1);
            NativeIcons.DrawMarkup(new Rect(r.x+8,r.y+8,r.width-16,r.height-16),chevron==Prev?"outfit-prev":"outfit-next",chevron,24,enabled?U.Mix(U.Hex(0xc9c3d1,e),U.Hex(0xfff1e5,e),h):U.Hex(0x55565e,e));
            return enabled&&U.Button(r);
        }
    }
}
