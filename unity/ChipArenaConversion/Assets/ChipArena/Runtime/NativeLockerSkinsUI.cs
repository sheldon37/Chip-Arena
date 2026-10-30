using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Locker's Weapon Skins category (Core/NativeWeaponSkins.cs): a card for the pilot's body first (Core/NativeBodySkins.cs:
    // Standard, Prototype and, with live DEV access, the Sheldon Clothes), then a card per weapon the player may use, each with
    // its two finishes, Standard (the default production model) and the Prototype Series (the original build). Hovering a
    // finish tries it on the stage's pilot; a click equips it in prefs.weaponSkins (the body is its "body" entry), which Save
    // Pilot stores and sends with the profile like every other Locker pick (the unsaved-changes baseline covers it). Kept
    // apart from Runtime/NativeLockerUI.cs, which only reserves the category's slots and calls LkSkinsLayout / LkSkins.
    // The body is also picked in Appearance (owner 2026-10-12: "add Body skin selection in customization"): the BODY SKIN row
    // beside BODY COLOUR (LkBodyRow), small cards with a drawn body thumbnail. Both pickers read the same choices and equipped
    // body (BodySkinChoices, BodySkinEquipped), try a hovered body on the stage the same way (lockerBodyTry) and write through
    // the one setter (SetBodySkin), so they always agree.
    // While the model draws (the visual body installed and the Player model not Classic: LkOnModel) the body is no longer
    // picked as a skin (owner 2026-10-28: "Remove Body Skin Options 'Prototype' and 'Sheldon Clothes' and just add 'Masks'
    // section for selecting masks"): Appearance's MASKS row (LkMasks) offers the face gear ("face" entry, FaceGearEquipped,
    // SetFaceGear): the robot's helmet (the default, owner 2026-10-30) or half mask, the concept's mask and visor both, the visor,
    // the mask, or no mask (the pilot's own face). The Skins tab then lists only the weapons. Classic keeps the BODY SKIN row and
    // the body card, the voxel bodies being different builds.
    public sealed partial class ConversionGame
    {
        private string lockerSkinTry,lockerBodyTry,lockerFaceTry;private bool lockerBodySheldon;
        private const float LkSkinCardMin=300,LkSkinCardH=112;
        private void LkSkinsLayout(){LkReserve(76);LkHead();LkReserve(LkGrid(lockerW,lockerSkins.Count,LkSkinCardMin,LkSkinCardH));}
        /// <summary>The skin the stage shows for `spec`: the hovered finish on this tab, else the equipped one.</summary>
        private string LockerSkinShown(string spec)=>lockerTab==NativeLocker.WeaponSkins&&lockerSkinTry!=null&&lockerWeapon==spec?lockerSkinTry:NativeWeaponSkins.Of(prefs.weaponSkins,spec);
        /// <summary>The body the stage and this pilot's own cards show: the hovered body on this tab, else the equipped one
        /// (the Sheldon Clothes only with live DEV access; allocation free, so a card may ask every frame).</summary>
        private string BodySkinShown()
        {
            if(lockerBodyTry!=null&&menuScreen==MenuScreen.Customize&&(lockerTab==NativeLocker.WeaponSkins||lockerTab==NativeLocker.Appearance))return lockerBodyTry;int k=NativeBodySkins.Of(prefs.weaponSkins);
            if(k==NativeBodySkins.SheldonIndex&&!NativeBodySkins.LiveDeveloper(account?.Access,System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))k=NativeBodySkins.StandardIndex;
            return NativeBodySkins.Keys[k];
        }
        /// <summary>The skin map this pilot sends and shows (save, profile, the rooftop): prefs.weaponSkins through the DEV gate.</summary>
        private string GatedSkins()=>NativeBodySkins.Gate(prefs.weaponSkins,NativeCosmeticRules.Allowed(account?.Access??NativeAccess.Guest,"body",NativeBodySkins.SheldonClothes));
        // ---- the body pick, shared by the Skins card and Appearance's BODY SKIN row ----
        /// <summary>How many bodies this pilot may pick, in NativeBodySkins order: Standard and Prototype, and the Sheldon
        /// Clothes only with live DEV access (lockerBodySheldon, from NativeCosmeticRules.Allowed in LockerLists).</summary>
        private int BodySkinChoices=>lockerBodySheldon?NativeBodySkins.Count:NativeBodySkins.SheldonIndex;
        /// <summary>The equipped body both pickers mark (a Sheldon Clothes pick without DEV access reads as Standard).</summary>
        private int BodySkinEquipped(){int k=NativeBodySkins.Of(prefs.weaponSkins);return k<BodySkinChoices?k:NativeBodySkins.StandardIndex;}
        /// <summary>The one setter of the body pick: the "body" entry of prefs.weaponSkins, through the DEV gate (a non-DEV pilot
        /// never keeps the Sheldon Clothes); Save Pilot stores and sends it with the rest of the map.</summary>
        private void SetBodySkin(string key){prefs.weaponSkins=NativeBodySkins.Gate(NativeWeaponSkins.With(prefs.weaponSkins,NativeBodySkins.Entry,key),lockerBodySheldon);}
        // ---- the model's face gear ----
        private bool LkOnModel=>ArenaPresentation.VisualBodyInstalled&&prefs.playerModel!="Classic";
        /// <summary>The equipped face gear (NativeWeaponSkins.FaceHelm, the default, .. FaceNone).</summary>
        private int FaceGearEquipped()=>NativeWeaponSkins.Index(prefs.weaponSkins,NativeWeaponSkins.FaceEntry);
        /// <summary>The one setter of the face gear: its "face" entry (on the model it no longer needs the Prototype body).</summary>
        private void SetFaceGear(string key){prefs.weaponSkins=NativeWeaponSkins.With(prefs.weaponSkins,NativeWeaponSkins.FaceEntry,key);}
        private void LkSkins(Rect banner,Rect head,Rect grid,float e)
        {
            bool repaint=Event.current.type==EventType.Repaint;if(repaint){lockerSkinTry=null;lockerBodyTry=null;}
            U.RoundGradient(banner,U.Hex(0x2a2333,e),U.Hex(0x1d1e24,e),10,U.Hex(0x4c4653,e),1,true);var well=new Rect(banner.x+18,banner.center.y-22,44,44);U.Round(well,U.Hex(0x15161b,e),10,U.Hex(0x6b5b8a,e));
            NativeIcons.Draw(new Rect(well.x+11,well.y+11,22,22),"gem",U.Hex(0xc9bfff,e));
            U.Text(new Rect(well.xMax+16,banner.y+16,banner.width-100,22),LkOnModel?"Pick a finish for every weapon":"Pick a body and a finish for every weapon",16,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);
            var lines=U.Wrap(LkOnModel?"Standard is the production model, Prototype the original build. Masks and skin tone are in Appearance.":"Standard is the current production model, lit in your accent colour. Prototype is the original build.",13,banner.width-well.xMax+banner.x-32,false,2);
            for(int i=0;i<lines.Count;i++)U.Text(new Rect(well.xMax+16,banner.y+40+i*17,banner.width-100,17),lines[i],13,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);
            LkHeading(head,lockerSkinsLabel,"Hover a finish to try it on");
            for(int i=0;i<lockerSkins.Count;i++)
            {
                // The body card (its "body" entry; Classic only) or a weapon's.
                bool body=lockerSkins[i]==NativeBodySkins.Entry;var w=body?null:LockerWeaponById(lockerSkins[i]);if(!body&&w==null)continue;
                string entry=body?NativeBodySkins.Entry:w.id;var keys=body?NativeBodySkins.Keys:NativeWeaponSkins.Keys;var labels=body?NativeBodySkins.Labels:NativeWeaponSkins.Labels;
                int count=body?BodySkinChoices:NativeWeaponSkins.Count;
                var r=LkCell(grid,i,LkSkinCardMin,LkSkinCardH);int equipped=body?BodySkinEquipped():NativeWeaponSkins.Index(prefs.weaponSkins,entry);if(equipped>=count)equipped=NativeWeaponSkins.StandardIndex;
                U.Round(r,U.Hex(0x1d1e24,e),10,U.Hex(0x3b3c45,e));
                // Its icon in a well, name and the equipped finish.
                var icon=new Rect(r.x+12,r.y+12,40,40);U.Round(icon,U.Hex(0x121318,e),8,U.Hex(0x2f3038,e));NativeIcons.Draw(new Rect(icon.x+8,icon.y+8,24,24),body?"person-standing":NativeIcons.Spec(w.id),body?LkColor(prefs.accentColor,0x70e1d0)*new Color(1,1,1,e):U.Hex((int)w.color,e));
                string title=body?"Pilot body":w.name;var name=U.Wrap(title,14,r.width-80,true,1);U.Text(new Rect(icon.xMax+12,r.y+12,r.width-icon.width-36,20),name.Count>0?name[0]:title,14,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);
                U.Text(new Rect(icon.xMax+12,r.y+33,r.width-icon.width-36,16),labels[equipped].ToUpperInvariant(),9,U.Hex(0xffc590,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                // The finishes.
                float gap=8,cw=(r.width-24-gap*(count-1))/count,cy=r.y+62;
                for(int k=0;k<count;k++)
                {
                    var chip=new Rect(r.x+12+k*(cw+gap),cy,cw,r.height-62-12);bool on=k==equipped;bool hot=lockerViewHot&&U.Hover(chip);float h=on?0:U.HoverAmount(8800+i*3+k,chip,hot);
                    U.Round(chip,on?U.Hex(0x3a2a26,e):U.A(U.Mix(U.Hex(0x23242b),U.Hex(0x2d2e36),h),e),8,on?U.Hex(0xff6b35,e):U.A(U.Mix(U.Hex(0x3b3c45),U.Hex(0x9a8a9e),h),e),on?2:1);
                    // A swatch for the finish: the accent-lit Standard strip, the Prototype's plain blocks, the Sheldon Clothes' dark
                    // cloth with a cyan seam.
                    var sw=new Rect(chip.x+10,chip.center.y-7,14,14);
                    if(k==NativeWeaponSkins.StandardIndex){U.Round(sw,U.Hex(0x2b3a42,e),3);U.Fill(new Rect(sw.x+2,sw.center.y-1,sw.width-4,2),LkColor(prefs.accentColor,0x70e1d0)*new Color(1,1,1,e));}
                    else if(k==NativeBodySkins.SheldonIndex){U.Round(sw,U.Hex(0x1c2132,e),3);U.Fill(new Rect(sw.center.x-1,sw.y+2,2,sw.height-4),U.Hex(NativeBodySkins.CyberCyan,e));}
                    else{U.Fill(new Rect(sw.x,sw.y+3,8,8),U.Hex(0x81999f,e));U.Fill(new Rect(sw.x+6,sw.y,8,8),U.Hex(0x263a43,e));}
                    string label=body&&k==NativeBodySkins.SheldonIndex?"Sheldon · DEV":labels[k];
                    U.Text(new Rect(sw.xMax+8,chip.y,chip.width-sw.width-24,chip.height),label,12,on?U.Hex(0xfff1e5,e):U.A(U.Mix(U.Hex(0xc9c3d1),U.Hex(0xf4f4f5),h),e),TextAnchor.MiddleLeft,bold:true);
                    if(hot&&repaint){if(body)lockerBodyTry=keys[k];else{lockerWeapon=w.id;lockerSkinTry=keys[k];}}
                    if(!lockerBlocked&&U.Button(chip)&&!on){if(body)SetBodySkin(keys[k]);else prefs.weaponSkins=NativeWeaponSkins.With(prefs.weaponSkins,entry,keys[k]);Click();}
                }
            }
        }

        // ---- Appearance's BODY SKIN row (laid out and headed beside BODY COLOUR by Runtime/NativeLockerUI.cs) ----
        // Short enough for one line beside the tick and the DEV badge at a third of the narrowest Locker column: the voxel
        // bodies', and the masks' (no mask, then the face gears).
        private static readonly string[] LkBodyTexts={"Armour plates, visor","The original blocky build","Long glowing coat"};
        private static readonly string[] LkMaskLabels={"Helmet","Half mask","Mask + visor","Visor","Mask","No mask"};
        private static readonly string[] LkMaskTexts={"Full robot helmet","Robot plate, nose to chin","Visor and lower mask","Band over the eyes","Nose and mouth covered","Your pilot's own face"};
        // The bodies share the row as segments (two or three across, wrapping only when a card would be under LkRowMin).
        private int LkBodyCols()=>Mathf.Max(1,Mathf.Min(BodySkinChoices,LkCols(lockerW,LkRowMin)));
        private float LkBodyRowH(){int cols=LkBodyCols(),rows=(BodySkinChoices+cols-1)/cols;return rows*LkRowH+(rows-1)*LkGap;}
        private Rect LkBodyCell(Rect grid,int i){int cols=LkBodyCols();float w=(lockerW-(cols-1)*LkGap)/cols;return new Rect(grid.x+(i%cols)*(w+LkGap),grid.y+(i/cols)*(LkRowH+LkGap),w,LkRowH);}
        // A row card per body (LkRowCard with the body's thumbnail; the Sheldon Clothes badged DEV): hovering tries it on the
        // stage, a click equips it through SetBodySkin.
        private void LkBodyRow(Rect grid)
        {
            int equipped=BodySkinEquipped(),count=BodySkinChoices;bool repaint=Event.current.type==EventType.Repaint;
            for(int i=0;i<count;i++)
            {
                var r=LkBodyCell(grid,i);bool on=i==equipped;
                bool click=LkRowCard(r,8260+i,on,null,default,NativeBodySkins.Labels[i],LkBodyTexts[i],null,out bool hot,i,i==NativeBodySkins.SheldonIndex);
                if(hot&&repaint)lockerBodyTry=NativeBodySkins.Keys[i];
                if(click&&!on)SetBodySkin(NativeBodySkins.Keys[i]);
            }
        }
        // ---- Appearance's MASKS row (on the model; laid out and headed by Runtime/NativeLockerUI.cs) ----
        // The face gears in NativeWeaponSkins.FaceKeys order (the robot's helmet, the default, first; No mask last), as row cards
        // with the model's thumbnail: hovering one tries it on the stage (lockerFaceTry), a click equips it through SetFaceGear.
        private int LkMaskCols()=>Mathf.Max(1,Mathf.Min(LkMaskLabels.Length,LkCols(lockerW,LkRowMin)));
        private float LkMasksH(){int cols=LkMaskCols(),rows=(LkMaskLabels.Length+cols-1)/cols;return rows*LkRowH+(rows-1)*LkGap;}
        private void LkMasks(Rect grid)
        {
            bool repaint=Event.current.type==EventType.Repaint;int worn=FaceGearEquipped(),cols=LkMaskCols();float w=(lockerW-(cols-1)*LkGap)/cols;
            for(int k=0;k<LkMaskLabels.Length;k++)
            {
                var r=new Rect(grid.x+(k%cols)*(w+LkGap),grid.y+(k/cols)*(LkRowH+LkGap),w,LkRowH);bool on=k==worn;
                bool click=LkRowCard(r,8270+k,on,null,default,LkMaskLabels[k],LkMaskTexts[k],null,out bool hot,LkFaceThumb+k);
                string key=NativeWeaponSkins.FaceKeys[k];
                if(hot&&repaint)lockerFaceTry=key;
                if(click&&!on)SetFaceGear(key);
            }
        }
        // A body skin's thumbnail in a row card's 44 unit well: a front-view voxel figure from rects in the pilot's colours (nothing
        // rotated or scaled: the Locker's scroll clip). Standard: plates over a dark undersuit, the visor slit and accent strips,
        // cyber-cyan ear lights. Prototype: the original plain blocks with accent caps. Sheldon Clothes: the long open coat in the
        // body-tinted cloth (the default body colour gives the hood's navy, lifted a little to read on the well), piped in the seam
        // glow (NativeBodySkins.SheldonGlowRgb), with a sash, glowing cuffs and the mask helmet. Hovering brightens it and runs a
        // cyan scan line (none with Reduced effects).
        // Thumbnail codes from LkFaceThumb are the face gears' (LkModelThumb), shown on the model.
        private const int LkFaceThumb=10;
        private void LkBodyThumb(Rect well,int skin,float e,float hover)
        {
            int bodyRgb=NativePortraitRules.Parse(prefs.bodyColor,0xe5e7eb),accentRgb=NativePortraitRules.Parse(prefs.accentColor,0x70e1d0);
            Color body=U.Hex(bodyRgb),accent=U.Hex(accentRgb),glow=skin==NativeBodySkins.SheldonIndex?U.Hex(NativeBodySkins.SheldonGlowRgb(prefs.hat,prefs.hatColor,accentRgb)):accent;
            U.RadialStops(new Rect(well.x+2,well.y+1,well.width-4,well.height-2),U.A(glow,(.26f+.18f*hover)*e),LkGlowAt,LkGlowA);
            var f=new Vector2(well.center.x,well.yMax-4);
            if(LkOnModel||skin>=LkFaceThumb)LkModelThumb(f,skin>=LkFaceThumb?skin-LkFaceThumb:FaceGearEquipped(),accent,e);
            else if(skin==NativeBodySkins.StandardIndex)
            {
                Color under=U.Mix(U.Hex(0x1b242b),body,.12f),plate=body,lit=accent,cyan=U.Hex(NativeBodySkins.CyberCyan);
                LkVox2(f,.3f,1.9f,.7f,4.4f,under,e);LkVox2(f,.2f,2.1f,0,.8f,plate,e);LkVox2(f,.45f,1.8f,1.2f,2.6f,plate,e);LkVox2(f,.8f,1.45f,1.6f,2.1f,lit,e);
                LkVox2(f,.5f,1.75f,2.75f,3.2f,LkShade(plate,.8f),e);LkVox2(f,.4f,1.9f,3.35f,4.4f,plate,e);
                LkVox(f,-2.1f,2.1f,4.35f,4.95f,under,e);LkVox(f,-1.5f,1.5f,4.55f,4.75f,lit,e);
                LkVox(f,-2.1f,2.1f,4.9f,8.3f,under,e);LkVox(f,-1.4f,1.4f,5.05f,5.55f,plate,e);LkVox(f,-1.4f,1.4f,5.7f,6.2f,plate,e);LkVox2(f,.2f,2,6.4f,8.2f,plate,e);LkVox(f,-.35f,.35f,6.6f,7.6f,lit,e);
                LkVox2(f,2.25f,3.1f,4.6f,8,under,e);LkVox2(f,2.2f,3.15f,4.6f,5.9f,plate,e);LkVox2(f,2,3.5f,7.4f,8.7f,plate,e);LkVox2(f,2,3.5f,7.4f,7.65f,lit,e);LkVox2(f,2.3f,3.05f,4,4.6f,under,e);
                LkVox(f,-.6f,.6f,8.3f,8.8f,under,e);LkVox(f,-1.45f,1.45f,8.8f,11.7f,plate,e);LkVox(f,-1.2f,1.2f,9.8f,10.7f,under,e);LkVox(f,-1.05f,1.05f,10.1f,10.4f,lit,e);LkVox2(f,1.45f,1.75f,9.9f,10.6f,cyan,e);
            }
            else if(skin==NativeBodySkins.PrototypeIndex)
            {
                Color joint=LkShade(body,.42f);
                LkVox2(f,.25f,2,0,.7f,LkShade(body,.85f),e);LkVox2(f,.35f,1.85f,.7f,4.3f,body,e);LkVox2(f,.6f,1.6f,2.4f,3.1f,accent,e);
                LkVox(f,-2,2,4.2f,4.9f,joint,e);LkVox(f,-2,2,4.85f,5.15f,accent,e);LkVox(f,-2.2f,2.2f,5.1f,8.4f,body,e);LkVox(f,-.35f,.35f,6,8,accent,e);
                LkVox2(f,2.4f,3.1f,4.7f,7.6f,body,e);LkVox2(f,2.3f,3.3f,7.4f,8.5f,accent,e);LkVox2(f,2.4f,3.1f,4.1f,4.7f,joint,e);
                LkVox(f,-.6f,.6f,8.4f,8.8f,joint,e);LkVox(f,-1.5f,1.5f,8.8f,11.8f,body,e);
            }
            else
            {
                Color cloth=new Color(Mathf.Min(1,.165f*body.r/.9f),Mathf.Min(1,.195f*body.g/.906f),Mathf.Min(1,.29f*body.b/.922f),1),inner=U.Hex(0x0b0d14),sash=LkShade(cloth,.62f);
                LkVox2(f,.4f,1.6f,0,1.1f,inner,e);LkVox2(f,.45f,1.5f,1.1f,1.9f,LkShade(cloth,.8f),e);
                LkVox(f,-2.3f,2.3f,1.8f,4.8f,cloth,e);LkVox(f,-1.95f,1.95f,4.8f,8.3f,cloth,e);LkVox(f,-2.3f,-2.05f,1.8f,4.8f,LkShade(cloth,1.5f),e);LkVox(f,2.05f,2.3f,1.8f,4.8f,LkShade(cloth,.75f),e);
                LkVox(f,-.35f,.35f,1.8f,6.4f,inner,e);LkVox(f,-.65f,.65f,6.4f,7.3f,inner,e);LkVox(f,-.95f,.95f,7.3f,8.2f,inner,e);
                LkVox(f,-.12f,.12f,5.1f,6.2f,glow,e);LkVox2(f,.35f,.55f,1.8f,6.4f,glow,e);LkVox2(f,.65f,.85f,6.4f,7.3f,glow,e);LkVox2(f,.95f,1.15f,7.3f,8.2f,glow,e);LkVox(f,-2.3f,2.3f,1.8f,2,U.A(glow,.7f),e);
                LkVox(f,-1.95f,1.95f,4.55f,5.1f,sash,e);LkVox(f,.9f,1.5f,3.9f,5.1f,sash,e);
                LkVox2(f,2,2.8f,4.9f,8,cloth,e);LkVox2(f,1.85f,2.95f,4.6f,5,glow,e);LkVox2(f,2.05f,2.75f,4,4.6f,inner,e);LkVox2(f,1.7f,2.6f,7.5f,8.4f,cloth,e);
                LkVox(f,-1.15f,1.15f,8.2f,8.8f,cloth,e);LkVox(f,-1.35f,1.35f,8.8f,11.6f,U.Hex(0x14161d),e);LkVox(f,-1.05f,1.05f,9.3f,10.9f,U.Hex(0x050608),e);LkVox(f,-.85f,.85f,9.95f,10.25f,glow,e);
            }
            if(hover>.01f&&!prefs.reducedEffects){float k=Mathf.Repeat(Time.unscaledTime*.9f,1);U.Fill(new Rect(well.x+5,well.y+4+k*(well.height-8),well.width-10,1),U.Hex(NativeCyberTheme.CyanHex,.45f*hover*e));}
        }
        // A model pilot's thumbnail: head and shoulders in the hood's dark cloth with accent trims, spiky hair, the skin in the
        // picked tone (LkToneShown: the model's own without one), and the face bare
        // (NativeWeaponSkins.FaceNone) or under the face gear: the visor's glowing band in its frame and
        // temple pods, the mask's dark block with its vents; the robot's helmet (in the body colour, its visor bar, grille and ear
        // modules over the whole head) or its half mask (the plate with the grille over the lower face, the ear modules either
        // side). `u` pixels a unit, the shoulders' foot at `f`.
        private void LkModelThumb(Vector2 f,int gear,Color accent,float e,float u=3.5f)
        {
            void Box(float x0,float x1,float y0,float y1,Color c)=>U.Fill(new Rect(f.x+x0*u,f.y-y1*u,(x1-x0)*u,(y1-y0)*u),U.A(c,e));
            Color cloth=U.Hex(0x2c303a),hood=U.Hex(0x3a3f4b),hair=U.Hex(0x313a5a),skin=U.Hex(LkToneShown()),dark=U.Hex(0x0b0c10),eye=U.Hex(0x4a3036);
            Box(-6,6,0,2.3f,cloth);Box(-3.7f,3.7f,2.3f,3.3f,hood);Box(-6,6,.35f,.6f,accent);Box(-1.1f,-.5f,.6f,2.3f,accent);Box(.5f,1.1f,.6f,2.3f,accent);
            Box(-.9f,.9f,2.3f,3.6f,LkShade(skin,.82f));Box(-1.3f,1.3f,3.4f,4.1f,skin);Box(-2.2f,2.2f,4,8.6f,skin);
            Box(-2.9f,2.9f,8.2f,10.4f,hair);Box(-2.9f,-2.2f,5.8f,8.6f,hair);Box(2.2f,2.9f,5.8f,8.6f,hair);Box(-2.2f,2.2f,7.7f,8.6f,hair);
            Box(-2.5f,-1.5f,10.4f,11,hair);Box(-.5f,.5f,10.4f,11.3f,hair);Box(1.5f,2.5f,10.4f,11,hair);
            bool visor=gear==NativeWeaponSkins.FaceBoth||gear==NativeWeaponSkins.FaceVisor,mask=gear==NativeWeaponSkins.FaceBoth||gear==NativeWeaponSkins.FaceMask;
            if(visor){Box(-2.6f,2.6f,5.8f,7.5f,dark);Box(-2.4f,2.4f,6.05f,7.25f,accent);Box(-3.1f,-2.6f,6,7.3f,hood);Box(2.6f,3.1f,6,7.3f,hood);}
            else{Box(-1.5f,-.6f,6.1f,7.1f,eye);Box(.6f,1.5f,6.1f,7.1f,eye);}
            if(mask){Box(-2.3f,2.3f,3.5f,5.8f,dark);Box(-1.4f,-.8f,4.5f,4.7f,accent);Box(.8f,1.4f,4.5f,4.7f,accent);Box(-1.4f,-.8f,4.95f,5.15f,accent);Box(.8f,1.4f,4.95f,5.15f,accent);}
            else Box(-.45f,.45f,4.6f,4.8f,LkShade(skin,.7f));
            if(gear!=NativeWeaponSkins.FaceHelm&&gear!=NativeWeaponSkins.FaceHalf)return;
            Color shell=U.Hex(NativePortraitRules.Parse(prefs.bodyColor,0xe5e7eb)),side=LkShade(shell,.78f);
            if(gear==NativeWeaponSkins.FaceHelm)
            {
                Box(-3,3,3.3f,10.6f,shell);Box(-2.5f,2.5f,10.6f,11.4f,shell);Box(-3,3,3.3f,3.55f,dark);
                Box(-2.7f,2.7f,6.6f,8.3f,dark);Box(-2.45f,2.45f,6.85f,8.05f,accent);
                Box(-1.2f,1.2f,5.6f,5.85f,dark);Box(-1.2f,1.2f,5.05f,5.3f,dark);Box(-.85f,.85f,4.5f,4.75f,dark);
                Box(-3.6f,-3,6,8.2f,side);Box(3,3.6f,6,8.2f,side);Box(-3.5f,-3.3f,7.5f,7.9f,accent);Box(3.3f,3.5f,7.5f,7.9f,accent);
            }
            else
            {
                Box(-2.5f,2.5f,3.9f,6.1f,shell);Box(-1.8f,1.8f,3.4f,3.9f,shell);
                Box(-1.1f,1.1f,5.3f,5.5f,dark);Box(-1.1f,1.1f,4.8f,5,dark);Box(-.8f,.8f,4.3f,4.5f,dark);Box(-2.1f,-1.9f,5.1f,5.8f,accent);Box(1.9f,2.1f,5.1f,5.8f,accent);
                Box(-3.1f,-2.4f,5.4f,7.4f,side);Box(2.4f,3.1f,5.4f,7.4f,side);Box(-3,-2.8f,6.8f,7.2f,accent);Box(2.8f,3,6.8f,7.2f,accent);
            }
        }
        // One rect of the thumbnail in body units (3 per unit, feet at `f`, y up), and its mirror pair (x0..x1 and -x1..-x0).
        private static void LkVox(Vector2 f,float x0,float x1,float y0,float y1,Color c,float e)=>U.Fill(new Rect(f.x+x0*3,f.y-y1*3,(x1-x0)*3,(y1-y0)*3),U.A(c,e));
        private static void LkVox2(Vector2 f,float x0,float x1,float y0,float y1,Color c,float e){LkVox(f,x0,x1,y0,y1,c,e);LkVox(f,-x1,-x0,y0,y1,c,e);}
        private static Color LkShade(Color c,float k)=>new Color(Mathf.Min(1,c.r*k),Mathf.Min(1,c.g*k),Mathf.Min(1,c.b*k),1);
    }
}
