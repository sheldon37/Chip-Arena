using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Locker's Weapon Skins category (Core/NativeWeaponSkins.cs): a card per weapon the player may use, each with its two
    // finishes, Standard (the default production model) and the Prototype Series (the original build). Hovering a finish
    // tries it on the stage's pilot (that weapon, that skin); a click equips it in prefs.weaponSkins, which Save Pilot stores
    // and sends with the profile like every other Locker pick (the unsaved-changes baseline covers it). Kept apart from
    // Runtime/NativeLockerUI.cs, which only reserves the category's slots and calls LkSkinsLayout / LkSkins.
    public sealed partial class ConversionGame
    {
        private string lockerSkinTry;
        private const float LkSkinCardMin=300,LkSkinCardH=112;
        private void LkSkinsLayout(){LkReserve(76);LkHead();LkReserve(LkGrid(lockerW,lockerSkins.Count,LkSkinCardMin,LkSkinCardH));}
        /// <summary>The skin the stage shows for `spec`: the hovered finish on this tab, else the equipped one.</summary>
        private string LockerSkinShown(string spec)=>lockerTab==NativeLocker.WeaponSkins&&lockerSkinTry!=null&&lockerWeapon==spec?lockerSkinTry:NativeWeaponSkins.Of(prefs.weaponSkins,spec);
        private void LkSkins(Rect banner,Rect head,Rect grid,float e)
        {
            bool repaint=Event.current.type==EventType.Repaint;if(repaint)lockerSkinTry=null;
            U.RoundGradient(banner,U.Hex(0x2a2333,e),U.Hex(0x1d1e24,e),10,U.Hex(0x4c4653,e),1,true);var well=new Rect(banner.x+18,banner.center.y-22,44,44);U.Round(well,U.Hex(0x15161b,e),10,U.Hex(0x6b5b8a,e));
            NativeIcons.Draw(new Rect(well.x+11,well.y+11,22,22),"gem",U.Hex(0xc9bfff,e));
            U.Text(new Rect(well.xMax+16,banner.y+16,banner.width-100,22),"Pick a finish for every weapon",16,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);
            var lines=U.Wrap("Standard is the current production model, lit in your accent colour. The Prototype Series is the original build.",13,banner.width-well.xMax+banner.x-32,false,2);
            for(int i=0;i<lines.Count;i++)U.Text(new Rect(well.xMax+16,banner.y+40+i*17,banner.width-100,17),lines[i],13,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);
            LkHeading(head,lockerSkinsLabel,"Hover a finish to try it on");
            for(int i=0;i<lockerSkins.Count;i++)
            {
                var w=LockerWeaponById(lockerSkins[i]);if(w==null)continue;var r=LkCell(grid,i,LkSkinCardMin,LkSkinCardH);int equipped=NativeWeaponSkins.Index(prefs.weaponSkins,w.id);
                U.Round(r,U.Hex(0x1d1e24,e),10,U.Hex(0x3b3c45,e));
                // The weapon: its icon in a well, name and the equipped finish.
                var icon=new Rect(r.x+12,r.y+12,40,40);U.Round(icon,U.Hex(0x121318,e),8,U.Hex(0x2f3038,e));NativeIcons.Draw(new Rect(icon.x+8,icon.y+8,24,24),NativeIcons.Spec(w.id),U.Hex((int)w.color,e));
                var name=U.Wrap(w.name,14,r.width-80,true,1);U.Text(new Rect(icon.xMax+12,r.y+12,r.width-icon.width-36,20),name.Count>0?name[0]:w.name,14,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true);
                U.Text(new Rect(icon.xMax+12,r.y+33,r.width-icon.width-36,16),NativeWeaponSkins.Labels[equipped].ToUpperInvariant(),9,U.Hex(0xffc590,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                // The two finishes.
                float gap=8,cw=(r.width-24-gap)/2,cy=r.y+62;
                for(int k=0;k<NativeWeaponSkins.Count;k++)
                {
                    var chip=new Rect(r.x+12+k*(cw+gap),cy,cw,r.height-62-12);bool on=k==equipped;bool hot=lockerViewHot&&U.Hover(chip);float h=on?0:U.HoverAmount(8800+i*2+k,chip,hot);
                    U.Round(chip,on?U.Hex(0x3a2a26,e):U.A(U.Mix(U.Hex(0x23242b),U.Hex(0x2d2e36),h),e),8,on?U.Hex(0xff6b35,e):U.A(U.Mix(U.Hex(0x3b3c45),U.Hex(0x9a8a9e),h),e),on?2:1);
                    // A swatch for the finish: the accent-lit Standard strip, the Prototype's plain blocks.
                    var sw=new Rect(chip.x+10,chip.center.y-7,14,14);
                    if(k==NativeWeaponSkins.StandardIndex){U.Round(sw,U.Hex(0x2b3a42,e),3);U.Fill(new Rect(sw.x+2,sw.center.y-1,sw.width-4,2),LkColor(prefs.accentColor,0x70e1d0)*new Color(1,1,1,e));}
                    else{U.Fill(new Rect(sw.x,sw.y+3,8,8),U.Hex(0x81999f,e));U.Fill(new Rect(sw.x+6,sw.y,8,8),U.Hex(0x263a43,e));}
                    U.Text(new Rect(sw.xMax+8,chip.y,chip.width-sw.width-24,chip.height),NativeWeaponSkins.Labels[k],12,on?U.Hex(0xfff1e5,e):U.A(U.Mix(U.Hex(0xc9c3d1),U.Hex(0xf4f4f5),h),e),TextAnchor.MiddleLeft,bold:true);
                    if(hot&&repaint){lockerWeapon=w.id;lockerSkinTry=NativeWeaponSkins.Keys[k];}
                    if(!lockerBlocked&&U.Button(chip)&&!on){prefs.weaponSkins=NativeWeaponSkins.With(prefs.weaponSkins,w.id,NativeWeaponSkins.Keys[k]);Click();}
                }
            }
        }
    }
}
