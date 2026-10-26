using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Locker's OUTFIT section (Appearance, while the model draws: LkOnModel; owner 2026-10-26: "integrate switching the
    // clothes on the model"): a row per slot of Core/NativeLook.cs (hair, top, bottoms, hands, shoes), its label and a chip per
    // piece; a click wears it (prefs.look, through NativeLook.With), which Save Pilot keeps and Revert undoes with the rest of
    // the look. The pilot's model wears it in the lobby, the draft and matches (Runtime/NativeVisualBodyPresentation.cs
    // VisualDress); the Locker's own stage is the portrait pilot.
    public sealed partial class ConversionGame
    {
        private const float LkOutfitLabel=92,LkOutfitChipMin=118,LkOutfitChipH=34,LkOutfitGap=8,LkOutfitRowGap=10;
        private int LkOutfitPerLine(){float w=lockerW-LkOutfitLabel;return Mathf.Max(1,Mathf.FloorToInt((w+LkOutfitGap)/(LkOutfitChipMin+LkOutfitGap)));}
        private float LkOutfitRowH(int slot){int per=LkOutfitPerLine(),lines=(NativeLook.Ids[slot].Length+per-1)/per;return lines*LkOutfitChipH+(lines-1)*LkOutfitGap;}
        private float LkOutfitH(){float h=0;for(int s=0;s<NativeLook.Slots.Length;s++)h+=LkOutfitRowH(s)+(s>0?LkOutfitRowGap:0);return h;}
        private void LkOutfit(Rect grid)
        {
            float e=lockerFade,y=grid.y;int per=LkOutfitPerLine();float cw=(lockerW-LkOutfitLabel-(per-1)*LkOutfitGap)/per;
            for(int s=0;s<NativeLook.Slots.Length;s++)
            {
                string slot=NativeLook.Slots[s];var ids=NativeLook.Ids[s];int worn=NativeLook.Index(prefs.look,slot);float rowH=LkOutfitRowH(s);
                U.Text(new Rect(grid.x,y,LkOutfitLabel-10,LkOutfitChipH),NativeLook.SlotLabels[s].ToUpperInvariant(),10,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                for(int k=0;k<ids.Length;k++)
                {
                    var chip=new Rect(grid.x+LkOutfitLabel+(k%per)*(cw+LkOutfitGap),y+(k/per)*(LkOutfitChipH+LkOutfitGap),cw,LkOutfitChipH);bool on=k==worn;
                    bool hot=lockerViewHot&&U.Hover(chip);float h=on?0:U.HoverAmount(8300+s*10+k,chip,hot);
                    U.Round(chip,on?U.Hex(0x3a2a26,e):U.A(U.Mix(U.Hex(0x23242b),U.Hex(0x2d2e36),h),e),8,on?U.Hex(0xff6b35,e):U.A(U.Mix(U.Hex(0x3b3c45),U.Hex(0x9a8a9e),h),e),on?2:1);
                    U.Text(new Rect(chip.x+12,chip.y,chip.width-20,chip.height),NativeLook.Labels[s][k],12,on?U.Hex(0xfff1e5,e):U.A(U.Mix(U.Hex(0xc9c3d1),U.Hex(0xf4f4f5),h),e),TextAnchor.MiddleLeft,bold:true);
                    if(!lockerBlocked&&lockerViewHot&&U.Button(chip)&&!on){prefs.look=NativeLook.With(prefs.look,slot,ids[k]);Click();}
                }
                y+=rowH+LkOutfitRowGap;
            }
        }
    }
}
