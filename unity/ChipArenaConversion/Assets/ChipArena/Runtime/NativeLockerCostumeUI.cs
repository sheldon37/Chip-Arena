using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Locker's COSTUME section (Appearance, on the model; owner 2026-11-18: "The voxel player model + weapons can be selected
    // as skins or costumes instead"; docs/VOXEL-RETIREMENT.md 2.3, O2: free for every pilot, the Sheldon Clothes DEV only):
    // None (the outfit) or the voxel pilot (Core NativeVoxelCostume, prefs.look's costume key, NativeVoxelCostume.With). Under
    // the voxel pilot, its variant (the skin map's body entry, through the body pick's one setter SetBodySkin: Standard,
    // Prototype and, with live DEV access, the Sheldon Clothes) with the voxel bodies' thumbnails, and the voxel set: every
    // weapon that has a Prototype Series model switched to it, the picks it replaced remembered on this PC only (PlayerPrefs,
    // never sent; Arsenal picks included) and put back when it is turned off (NativeVoxelCostume.SetPut / SetTake). The Katana,
    // Fangs, Glaive, Top Dog and Bombadier keep their own look. The voxel set's card shows whether or not the costume is worn, so
    // it can always be seen and undone (review 2026-11-19); a second tap after one weapon was picked again merges into the list
    // it remembers. Save Pilot stores and sends the look and skin map like every other Locker pick; hovering a variant tries it on
    // the stage (lockerBodyTry). Under the costume the rows it hides (masks, skin tone, outfit) are dimmed and say so.
    public sealed partial class ConversionGame
    {
        // The section's state latched for one GUI event, so its layout and its deferred draw reserve and use the same slots.
        private bool lockerCostume;
        // Thumbnail codes from LkVoxelThumb are the voxel bodies' (LkBodyThumb's own figures), shown on the model too.
        private const int LkVoxelThumb=20;
        private const string VoxelSetKept="ChipArena.VoxelSetKept";
        private static readonly string[] LkCostumeLabels={"Your outfit","Voxel pilot"},LkCostumeTexts={"The clothes you pick below","The voxel blocks, on your model"};
        private int LkCostumeCols()=>Mathf.Max(1,Mathf.Min(2,LkCols(lockerW,LkRowMin)));
        private int LkCostumeRows()=>LkCostumeCols()>=2?1:2;
        private float LkCostumeH()
        {
            float h=LkCostumeRows()*(LkRowH+LkGap)-LkGap;if(lockerCostume)h+=LkGap+LkBodyRowH();
            return h+LkGap+LkRowH;
        }
        /// <summary>The Locker's rows the costume hides, while it is worn: drawn dimmed (their picks still apply under the outfit).</summary>
        private const float LkHiddenAlpha=.42f;
        private const string LkHiddenHint="Hidden under the voxel costume";
        private void LkCostume(Rect grid)
        {
            bool repaint=Event.current.type==EventType.Repaint;int cols=LkCostumeCols();float w=(lockerW-(cols-1)*LkGap)/cols;
            for(int k=0;k<2;k++)
            {
                var r=new Rect(grid.x+(k%cols)*(w+LkGap),grid.y+(k/cols)*(LkRowH+LkGap),w,LkRowH);bool on=(k==1)==lockerCostume;
                bool click=LkRowCard(r,8290+k,on,"person-standing",U.Hex(0xefb587),LkCostumeLabels[k],LkCostumeTexts[k],null,out bool hot,k==1?LkVoxelThumb+BodySkinEquipped():-1);
                if(click&&!on)prefs.look=NativeVoxelCostume.With(prefs.look,k==1);
            }
            float y=grid.y+LkCostumeRows()*(LkRowH+LkGap);
            if(lockerCostume)
            {
                // Its variant: the voxel bodies, as the BODY SKIN row offers them (Classic), with their thumbnails.
                var variants=new Rect(grid.x,y,grid.width,LkBodyRowH());int equipped=BodySkinEquipped(),count=BodySkinChoices;
                for(int i=0;i<count;i++)
                {
                    var r=LkBodyCell(variants,i);bool on=i==equipped;
                    bool click=LkRowCard(r,8296+i,on,null,default,NativeBodySkins.Labels[i],LkBodyTexts[i],null,out bool hot,LkVoxelThumb+i,i==NativeBodySkins.SheldonIndex);
                    if(hot&&repaint)lockerBodyTry=NativeBodySkins.Keys[i];
                    if(click&&!on)SetBodySkin(NativeBodySkins.Keys[i]);
                }
                y=variants.yMax+LkGap;
            }
            // The voxel set (one tap, undone by another), shown with or without the costume.
            var set=new Rect(grid.x,y,w,LkRowH);bool setOn=NativeVoxelCostume.SetOn(prefs.weaponSkins);
            if(LkRowCard(set,8299,setOn,"boxes",U.Hex(0x9fb4ff),"Voxel set",setOn?"On: tap to put your own weapon picks back":"Every weapon's Prototype Series; the newer classes keep theirs",null,out _))
            {
                string kept="";try{kept=PlayerPrefs.GetString(VoxelSetKept,"");}catch(System.Exception){}
                if(setOn)prefs.weaponSkins=NativeVoxelCostume.SetTake(prefs.weaponSkins,kept);
                else{prefs.weaponSkins=NativeVoxelCostume.SetPut(prefs.weaponSkins,kept,out var now);try{PlayerPrefs.SetString(VoxelSetKept,now);PlayerPrefs.Save();}catch(System.Exception){}}
            }
        }
    }
}
