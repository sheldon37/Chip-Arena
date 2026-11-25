using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Locker's BODY section (Appearance, on the model; Astra's brief 2026-11-24 section 1: "a clearly labeled Male / Female
    // choice in the Locker with a live turntable"): two cards, Male (today's pilot) and Female (Core NativeBodyKind), each with
    // its figure, first in Appearance. Hovering a card tries that body on the stage; a pick writes prefs.look's body key
    // (NativeBodyKind.With: Revert and the unsaved-changes baseline cover it with the rest of the look) and turns the stage's
    // pilot once round on its turntable (LkSpin, in LockerYawShown) so the new body shows from every side. Every hairstyle and
    // outfit fits both. Under the voxel costume the row is dimmed and says so. Until NativeLook carries the key (Astra's part of
    // the contract, docs/DEV_LOG.md 2026-11-25), Save Pilot keeps the pick on this PC only: the account and a room take the look
    // without it, so other players see today's pilot until then.
    public sealed partial class ConversionGame
    {
        // The body hovered in the row this repaint (-1 none: reset with the other try-ons), and when the last pick was made.
        private int lockerBodyKindTry=-1;private float lockerSpinAt=-9;
        private static readonly string[] LkBodyKindTexts={"The original pilot body","The female pilot body"};
        private static readonly string[] LkBodyKindKeys={"locker-body-male","locker-body-female"};
        private const string LkFigure=" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";
        // The two figures (24-unit boxes, the Locker's line style): square shoulders and a straight body; a fitted waist and a skirt line.
        private static readonly string[] LkBodyKindFigures={
            "<circle cx=\"12\" cy=\"4.6\" r=\"2.4\""+LkFigure+"/><path d=\"M8.5 9h7a1.5 1.5 0 0 1 1.5 1.5v5h-1.8V21h-2.4v-5h-1.6v5H8.8v-5.5H7v-5A1.5 1.5 0 0 1 8.5 9Z\""+LkFigure+"/>",
            "<circle cx=\"12\" cy=\"4.6\" r=\"2.4\""+LkFigure+"/><path d=\"M10.2 9h3.6l1.6 2.2-1 1.2 3.1 4.6h-3.1V21h-1.6v-4h-1.6v4H9.6v-4H6.5l3.1-4.6-1-1.2Z\""+LkFigure+"/>"};
        private float LkBodyKindH()=>LkCostumeRows()*(LkRowH+LkGap)-LkGap;
        private void LkBodyKind(Rect grid)
        {
            bool repaint=Event.current.type==EventType.Repaint;int cols=LkCostumeCols();float w=(lockerW-(cols-1)*LkGap)/cols;int worn=NativeBodyKind.Of(prefs.look);
            for(int k=0;k<NativeBodyKind.Ids.Length;k++)
            {
                var r=new Rect(grid.x+(k%cols)*(w+LkGap),grid.y+(k/cols)*(LkRowH+LkGap),w,LkRowH);bool on=k==worn;
                bool click=LkRowCard(r,8280+k,on,null,U.Hex(0xefb587),NativeBodyKind.Labels[k],LkBodyKindTexts[k],null,out bool hot,figure:LkBodyKindFigures[k],figureKey:LkBodyKindKeys[k]);
                if(hot&&repaint&&!on&&!lockerCostume)lockerBodyKindTry=k;
                if(click&&!on){prefs.look=NativeBodyKind.With(prefs.look,k);lockerSpinAt=Time.unscaledTime;}
            }
        }
        // The shown look with the hovered body tried on (LookShown's last step), made again only when either changes.
        private string lookBodyFrom,lookBodyLook;private int lookBodyOf=-1;
        private string LookBodyTried(string look)
        {
            if(lockerBodyKindTry<0||menuScreen!=MenuScreen.Customize||lockerTab!=NativeLocker.Appearance)return look;
            if(!ReferenceEquals(look,lookBodyFrom)||lookBodyOf!=lockerBodyKindTry){lookBodyFrom=look;lookBodyOf=lockerBodyKindTry;lookBodyLook=NativeBodyKind.With(look,lockerBodyKindTry);}
            return lookBodyLook;
        }
        // The turntable's once-round after a body pick: eased in and out over LkSpinTime; none with Reduced effects, or once the
        // pilot is dragged.
        private const float LkSpinTime=3.2f;
        private float LkSpin()
        {
            if(prefs.reducedEffects||lockerTurnAt>lockerSpinAt)return 0;float k=Mathf.Clamp01((Time.unscaledTime-lockerSpinAt)/LkSpinTime);return k>=1?0:360*k*k*(3-2*k);
        }
    }
}
