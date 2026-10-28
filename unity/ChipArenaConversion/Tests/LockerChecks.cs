using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// The Locker (the Customize screen): its categories and card text (Core/NativeLocker.cs) and the weapon ability slots
// read from content, engine-free; then source checks of the IMGUI screen (Runtime/NativeLockerUI.cs) that the categories
// are all drawn, restricted cosmetics stay behind NativeCosmeticRules.Allowed, Save Pilot keeps the old save-and-send path
// and the weapon skins tab only picks skins, and the Appearance BODY SKIN row shares the Skins card's body setter and DEV gate.
// Drawing itself is checked by the offline menu renders, not here.
internal static class LockerChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Locker: "+what);count++;}
    public static void Run(string root,ConversionContent data)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        string ui=R("NativeLockerUI.cs"),home=R("NativeHomeMenu.cs"),profile=R("NativeProfileUI.cs"),lobby=R("NativeLobbyUI.cs"),portraits=R("NativePortraits.cs");
        var icons=new HashSet<string>();using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json"))))foreach(var i in doc.RootElement.GetProperty("icons").EnumerateArray())icons.Add(i.GetProperty("id").GetString());
        // The original art set (ClassIcons.json: class, weapon and the Bow's Volley/Rain icons) that NativeIcons also draws.
        var art=new HashSet<string>();using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ClassIcons.json"))))foreach(var i in doc.RootElement.GetProperty("icons").EnumerateArray())art.Add(i.GetProperty("id").GetString());

        // 1. Every category exists, in rail order, with its label, icon and subtitle; only the weapon skins are inert.
        string[] keys={"appearance","kill-effects","entrance","loadout","weapon-skins"};
        Check(NativeLocker.Count==5&&NativeLocker.Keys.SequenceEqual(keys),"the five categories in order");
        Check(NativeLocker.Labels.Length==5&&NativeLocker.Icons.Length==5&&NativeLocker.Subtitles.Length==5,"a label, icon and subtitle per category");
        Check(NativeLocker.Labels.SequenceEqual(new[]{"Appearance","Kill Effects","Entrance","Loadout","Skins"}),"category labels (the skins tab holds the body and the weapons)");
        foreach(var icon in NativeLocker.Icons)Check(icons.Contains(icon),"category icon "+icon+" is in Icons.json");
        for(int i=0;i<5;i++)Check(NativeLocker.Interactive(i)&&!NativeLocker.Soon(i),"every category is live, none coming soon ("+keys[i]+")");
        foreach(var tab in new[]{"Appearance","KillEffects","Entrance","Loadout"})Check(Count(ui,"case NativeLocker."+tab+":")==2,"category "+tab+" is laid out and drawn");
        Check(ui.Contains("default:LkSkinsLayout();break;")&&ui.Contains("default:LkSkins(lockerSlots[0],lockerSlots[1],lockerSlots[2],e);break;"),"the weapon skins category is laid out and drawn");
        Check(ui.Contains("for(int i=0;i<NativeLocker.Count;i++)")&&ui.Contains("NativeLocker.Labels[i]")&&ui.Contains("lockerTabNext=i;"),"the rail offers every category");

        // 2. Restricted cosmetics stay hidden: every list comes through NativeCosmeticRules.Allowed, and the gate itself is
        // unchanged but for the Sheldon Head (dev- items and the Auditor Halo need a grant; the Gibber and the Sheldon Head need
        // DEV access itself).
        Check(ui.Contains("foreach(var id in NativeCosmeticRules.Hats)if(NativeCosmeticRules.Allowed(access,\"hat\",id))lockerHats.Add(id);")
            &&ui.Contains("foreach(var id in NativeCosmeticRules.Kills)if(NativeCosmeticRules.Allowed(access,\"effect\",id))lockerKills.Add(id);")
            &&ui.Contains("foreach(var id in NativeCosmeticRules.Intros)if(NativeCosmeticRules.Allowed(access,\"intro\",id))lockerIntros.Add(id);")
            &&ui.Contains("foreach(var w in content.weapons)if(AllowedWeapon(w)&&NativeWeaponSkins.Skinned(w.id))lockerSkins.Add(w.id);"),"hats, kill effects, intros and skin weapons are filtered by the access rules");
        foreach(var raw in new[]{"NativeCosmeticRules.Hats","NativeCosmeticRules.Kills","NativeCosmeticRules.Intros"})Check(Count(ui,raw)==1,raw+" is read only by the gated list builder");
        Check(Regex.Matches(ui,@"for\(int i=0;i<locker(Hats|Kills|Intros)\.Count;i\+\+\)").Count==3,"the cards draw the gated lists");
        Check(ui.Contains("NativeLoadoutText.InClass")||ui.Contains("MenuWeapons();"),"the weapon cards use the loadout's gated weapon list (MenuWeapons)");
        var gate=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeHatLooks.cs"));
        Check(profile.Contains("public static bool Allowed(NativeAccess a,string type,string id)=>NativeCosmeticGate.Allowed(a,type,id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());")
            &&gate.Contains("public static bool DevOnly(string id)=>id==\"dev-gibber\"||id==NativeHatLooks.SheldonHead||id==SheldonIntro||id==NativeBodySkins.SheldonClothes||id==SheldonIdle;")
            &&gate.Contains("public static bool Restricted(string id)=>id!=null&&(id.StartsWith(\"dev-\",StringComparison.Ordinal)||id==\"auditor-halo\"||id==NativeHatLooks.SheldonHead||id==SheldonIntro||id==NativeBodySkins.SheldonClothes||id==SheldonIdle);")
            &&gate.Contains("public static bool Allowed(NativeAccess a,string type,string id,double now)=>id!=null&&(!DevOnly(id)||(a??NativeAccess.Guest).Developer)&&(!Restricted(id)||(a??NativeAccess.Guest).Allows(type+\":\"+id,now));"),"the cosmetic gate is unchanged for legacy grants (moved to Core; the Sheldon Head, Sheldon Intro and Sheldon Idle join the Gibber behind live DEV access)");
        var hats=Ids(profile,"Hats");var kills=Ids(profile,"Kills");var intros=Ids(profile,"Intros");
        Check(hats.Length==12&&kills.Length==22&&intros.Length==25,"cosmetic id lists parsed ("+hats.Length+"/"+kills.Length+"/"+intros.Length+")");
        // The staged entrances, the DEV Sheldon Intro and the DEV Cyber Lord's Hardlight Boot (staged by the director, listed only with DEV access).
        var staged=NativeIntroRules.Entrances.Concat(new[]{NativeCosmeticGate.SheldonIntro,CyberLordSpec.Entrance}).ToArray();
        Check(intros.SequenceEqual(staged.OrderBy(i=>Array.IndexOf(intros,i)))&&intros.OrderBy(i=>i).SequenceEqual(staged.OrderBy(i=>i))&&NativeIntroRules.Pickable(NativeCosmeticGate.SheldonIntro)&&NativeEntranceDirector.Directed(NativeCosmeticGate.SheldonIntro)&&NativeIntroRules.Pickable(CyberLordSpec.Entrance)&&NativeEntranceDirector.Directed(CyberLordSpec.Entrance),"the Locker offers exactly the staged entrances");
        foreach(var id in NativeIntroRules.Procedural)Check(NativeLocker.IntroIcon(id)!=null&&!NativeLocker.DevItem(id),"the procedural entrance "+id+" has its icon and no DEV badge");
        foreach(var id in NativeIntroRules.Retired)Check(Array.IndexOf(intros,id)<0&&NativeLocker.IntroText(id).Length==0&&NativeLocker.IntroIcon(id)==null,"the retired "+id+" is gone from the Locker");
        foreach(var id in hats.Concat(kills).Concat(intros))Check(NativeLocker.DevItem(id)==(id.StartsWith("dev-")||id=="auditor-halo"||id=="sheldon-head"||id=="sheldon-intro"),"DEV badge for "+id);
        foreach(var id in hats)Check(NativeLocker.HatText(id).Length>0,"headwear text for "+id);
        foreach(var id in kills)Check(NativeLocker.KillText(id).Length>0&&(NativeLocker.KillColor(id)!=0xc0c1ca||id=="ragdoll"),"kill effect text and colour for "+id);
        foreach(var id in intros)Check(NativeLocker.IntroText(id).Length>0,"entrance text for "+id);
        Check(NativeLocker.Clean("Gibber · DEV ONLY")=="Gibber"&&NativeLocker.Clean("DEV · Rain Warrior")=="Rain Warrior"&&NativeLocker.Clean("Crown")=="Crown","DEV markings move to the badge");
        Check(NativeLocker.PoseTexts.Length==NativeLobbyPoses.Keys.Length,"a description per idle pose");

        // 3. Save Pilot keeps the old path exactly; Revert and Discard only restore the entry look; leaving asks first.
        Check(ui.Contains("SavePilot();if(server!=null&&InRoom)SendServer(new NativeNetMessage{type=\"profile\",profile=Profile(),token=account?.Token});if(Hosting)LobbyBroadcast();"),"Save Pilot: SavePilot, the room profile message, the host broadcast");
        Check(ui.Contains("if(MenuButton(save,\"check\",\"Save Pilot\",null,true,dirty,e,15,true))LockerSave();")&&ui.Contains("k==2){LockerSave();LockerLeave();}"),"the Save Pilot and Save & leave buttons save through LockerSave (disabled while nothing changed)");
        Check(Count(ui,"SavePilot();")==1&&Count(ui,"account.Save(")==0&&!Regex.IsMatch(ui,@"(?<![\w.])Save\(\);"),"the Locker saves only through SavePilot");
        Check(profile.Contains("Save();if(account.Account!=null)account.Save(Profile());"),"SavePilot still stores prefs and the account profile");
        Check(home.Contains("private void CustomizeScreen(float age)=>LockerScreen(age);")&&home.Contains("if(screen==MenuScreen.Customize&&from!=MenuScreen.Customize)LockerEnter();")&&home.Contains("var from=MenuTarget;"),"Customize opens the Locker and takes its baseline on entry (from the screen the menu is on or going to, NativeMenuTransitionUI.cs)");
        Check(home.Contains("if(at==MenuScreen.Customize){LockerBack();return;}")&&home.Contains("private void MenuBack(){var at=MenuTarget;")&&ui.Contains("private void LockerBack(){if(lockerConfirm){lockerConfirm=false;return;}if(LockerDirty())lockerConfirm=true;else LockerLeave();}"),"Escape and close ask before dropping unsaved changes");
        // PanelBack (NativeHomeMenu.cs): the room first, else the Expeditions screen Customize was opened from, else home.
        Check(ui.Contains("private void LockerLeave(){lockerConfirm=false;Go(PanelBack);}")&&home.Contains("private MenuScreen PanelBack=>InRoom?MenuScreen.Lobby:expReturn?MenuScreen.Boss:MenuScreen.Home;")&&lobby.Contains("(\"palette\",\"Customize\",MenuScreen.Customize)"),"the lobby's Customize entry returns to the lobby (and the Expeditions screen's to it)");
        foreach(var field in new[]{"name","weapon","lobbyPose","bodyColor","accentColor","hat","killEffect","intro","introText","overrideIntro","weaponSkins","lobbyBack","hatColor","backColor"})
            Check(Regex.IsMatch(ui,@"b\.\w+!=prefs\."+field+@"\b")&&Regex.IsMatch(ui,@"prefs\."+field+@"=b\.\w+;"),"the baseline covers and restores prefs."+field);
        Check(profile.Contains("LockerRebase();};"),"an account profile loaded meanwhile becomes the baseline");

        // 4. The weapon skins tab (Runtime/NativeLockerSkinsUI.cs) picks the body skin and a skin per weapon: its only write is
        // prefs.weaponSkins through the normalizing setter (saved by Save Pilot), hovering only tries one on the stage.
        string skins=Body(R("NativeLockerSkinsUI.cs"),"private void LkSkins(");
        Check(skins.Length>200&&!ui.Contains("private void LkSkins("),"the weapon skins drawing lives in its own file");
        Check(Regex.Matches(skins,@"prefs\.\w+=").Count==1&&skins.Contains("prefs.weaponSkins=NativeWeaponSkins.With(prefs.weaponSkins,entry,keys[k]);")&&skins.Contains("string entry=body?NativeBodySkins.Entry:w.id;var keys=body?NativeBodySkins.Keys:NativeWeaponSkins.Keys;"),"the skin selector writes only the skin map");
        foreach(var bad in new[]{"SetWeapon(","menuClass=","SavePilot(","Save("})Check(!skins.Contains(bad),"the weapon skins tab has no "+bad);

        // 4b. The body is also picked in Appearance (owner: "add Body skin selection in customization"): a BODY SKIN row in the
        // pilot section beside BODY COLOUR. It and the Skins tab's body card read the same choices and equipped body, try a
        // hovered body on the stage the same way and write through one setter, so the two pickers always agree; the setter
        // keeps the DEV gate, and the pick is saved only by Save Pilot (no new field: the "body" entry of prefs.weaponSkins).
        string skinsFile=R("NativeLockerSkinsUI.cs"),row=Body(skinsFile,"private void LkBodyRow("),appearanceLayout=Between(ui,"case NativeLocker.Appearance:","case NativeLocker.KillEffects:"),
            appearance=Between(ui.Substring(ui.IndexOf("private void LockerDeferred()",StringComparison.Ordinal)),"case NativeLocker.Appearance:","case NativeLocker.KillEffects:");
        const string setter="private void SetBodySkin(string key){prefs.weaponSkins=NativeBodySkins.Gate(NativeWeaponSkins.With(prefs.weaponSkins,NativeBodySkins.Entry,key),lockerBodySheldon);}";
        const string choices="private int BodySkinChoices=>lockerBodySheldon?NativeBodySkins.Count:NativeBodySkins.SheldonIndex;";
        const string equippedRule="private int BodySkinEquipped(){int k=NativeBodySkins.Of(prefs.weaponSkins);return k<BodySkinChoices?k:NativeBodySkins.StandardIndex;}";
        Check(Count(skinsFile,setter)==1&&Count(skinsFile,choices)==1&&Count(skinsFile,equippedRule)==1,"one setter, one choice list and one equipped rule for the body");
        Check(Count(skinsFile+ui,"SetBodySkin(")==5&&skinsFile.Contains("if(BodySkinEquipped()!=NativeBodySkins.PrototypeIndex)SetBodySkin(NativeBodySkins.Prototype);")&&skins.Contains("if(body)SetBodySkin(keys[k]);else prefs.weaponSkins=NativeWeaponSkins.With(prefs.weaponSkins,entry,keys[k]);")&&row.Contains("if(click&&!on)SetBodySkin(NativeBodySkins.Keys[i]);"),"the Skins card, the BODY SKIN row, a face gear pick (the Prototype body) and MASKS' no mask equip through SetBodySkin (its only callers)");
        Check(skins.Contains("int count=body?BodySkinChoices:NativeWeaponSkins.Count;")&&skins.Contains("int equipped=body?BodySkinEquipped():")&&row.Contains("int equipped=BodySkinEquipped(),count=BodySkinChoices;"),"both pickers offer the same bodies and mark the same equipped one");
        Check(Count(skinsFile+ui,"NativeBodySkins.Entry,key")==1&&!Regex.IsMatch(row,@"prefs\.\w+=")&&!Regex.IsMatch(ui,@"prefs\.weaponSkins=(?!b\.Skins;)"),"nothing else in the Locker writes the body entry (Revert restores the whole map)");
        foreach(var bad in new[]{"SavePilot(","Save(","account.","SendServer(","LobbyBroadcast("})Check(!row.Contains(bad)&&!setter.Contains(bad),"the BODY SKIN row saves only through Save Pilot (no "+bad+")");
        Check(row.Contains("if(hot&&repaint)lockerBodyTry=NativeBodySkins.Keys[i];")&&skinsFile.Contains("if(lockerBodyTry!=null&&menuScreen==MenuScreen.Customize&&(lockerTab==NativeLocker.WeaponSkins||lockerTab==NativeLocker.Appearance))return lockerBodyTry;")
            &&ui.Contains("lockerHatColor=lockerBackColor=null;lockerBodyTry=null;lockerFaceTry=null;lockerToneTry=-2;}")&&ui.Contains("BodySkin=BodySkinShown()"),"hovering a body tries it on the Locker stage (cleared every repaint, only on the Customize screen); a pick shows there at once");
        // The row sits in the pilot (Appearance) section, laid out and drawn between CALLSIGN and BODY COLOUR.
        int callsign=appearance.IndexOf("\"CALLSIGN\"",StringComparison.Ordinal),bodySkin=appearance.IndexOf("LkHeading(lockerSlots[k++],\"BODY SKIN\",\"Hover a body to try it on\");LkBodyRow(lockerSlots[k++]);",StringComparison.Ordinal),bodyColour=appearance.IndexOf("\"BODY COLOUR\"",StringComparison.Ordinal);
        Check(callsign>=0&&callsign<bodySkin&&bodySkin<bodyColour,"the BODY SKIN row is drawn in the pilot section, after CALLSIGN and right before BODY COLOUR");
        Check(appearanceLayout.Contains("prefs.name=GUILayout.TextField(prefs.name??\"\",20,lockerField);\n                    if(LkOnModel){LkHead();LkReserve(LkMasksH());LkHead();LkReserve(LkToneH());}\n                    else{LkHead();LkReserve(LkBodyRowH());LkHead();LkSwatches(ref prefs.bodyColor,\"#e5e7eb\");}"),"its slots are reserved in the same place (in Classic; MASKS and SKIN TONE take them on the model)");
        int slots=int.Parse(Regex.Match(ui,@"private readonly Rect\[\] lockerSlots=new Rect\[(\d+)\];").Groups[1].Value);
        Check(Count(appearance,"lockerSlots[k++]")<=slots&&Count(appearance,"lockerSlots[k++]")==Count(appearanceLayout,"LkHead(")+Count(appearanceLayout,"LkReserve(")+Count(appearanceLayout,"LkSwatches("),"the Appearance section's "+Count(appearance,"lockerSlots[k++]")+" slots fit the "+slots+" reserved and match its layout");
        Check(row.Contains("LkRowCard(r,8260+i,on,null,default,NativeBodySkins.Labels[i],LkBodyTexts[i],null,out bool hot,i,i==NativeBodySkins.SheldonIndex);")&&ui.Contains("if(body>=0)LkBodyThumb(well,body,e,h);")
            &&Regex.Matches(Regex.Match(skinsFile,@"LkBodyTexts=\{([^}]*)\}").Groups[1].Value,"\"[^\"]+\"").Count==NativeBodySkins.Count,"the row's cards reuse the Locker's row card with a body thumbnail, a line per body and the DEV badge on the Sheldon Clothes");
        Check(!skinsFile.Contains("U.Push("),"the body thumbnails draw untransformed (scroll clip)");
        // The OUTFIT section (Runtime/NativeLockerOutfitUI.cs): Appearance, while the model draws; a click wears the piece through
        // NativeLook.With; Revert and the unsaved-changes baseline cover prefs.look.
        string outfit=R("NativeLockerOutfitUI.cs");
        Check(appearanceLayout.Contains("if(LkOnModel){LkHead();LkReserve(LkOutfitH());}")&&appearance.Contains("if(LkOnModel){LkHeading(lockerSlots[k++],\"OUTFIT\",\"Your pilot's clothes on the model\");LkOutfit(lockerSlots[k++]);}")
            &&outfit.Contains("prefs.look=NativeLook.Gate(NativeLook.With(prefs.look,slot,ids[k]),lockerBodySheldon);Click();")&&ui.Contains("BackColor=prefs.backColor,Look=prefs.look};")&&ui.Contains("||b.Look!=prefs.look;}")&&ui.Contains("prefs.look=b.Look;"),"the OUTFIT section wears a piece per click and is saved and reverted with the look");
        // Owner 2026-10-28 ("It clutters a bit much with the buttons for clothing pieces", "Just want a very clean looking UI"):
        // one fixed-height row a slot however many pieces it lists, its icon and name, and arrows that wrap round the worn piece.
        string picker=outfit.Substring(outfit.IndexOf("public static class NativeOutfitPicker"));
        string pickerRow=picker.Substring(picker.IndexOf("public static int Row("),picker.IndexOf("private static bool Arrow(")-picker.IndexOf("public static int Row("));
        Check(outfit.Contains("private float LkOutfitH()=>NativeOutfitPicker.Height(lockerW,NativeLook.Slots.Length);")&&picker.Contains("return rows<=0?0:rows*RowH+(rows-1)*Gap;")
            &&pickerRow.Contains("pick=(worn-1+shown)%shown;")&&pickerRow.Contains("pick=(worn+1)%shown;")&&pickerRow.Contains("if(!on&&hot&&U.Button(hit)){pick=i;")
            &&picker.Contains("public static readonly string[] Icons={")&&Regex.Matches(picker.Substring(picker.IndexOf("Icons={"),picker.IndexOf("IconKeys=")-picker.IndexOf("Icons={")),"\"<path").Count>=NativeLook.Slots.Length,
            "the OUTFIT picker is a row a slot (icon, name, wrapping arrows, dots) whose height does not grow with the pieces");
        string rowBody=pickerRow.Replace("(worn+1)+\" / \"+shown","");
        Check(pickerRow.Contains("(worn+1)+\" / \"+shown")&&!Regex.IsMatch(rowBody,@"\bnew\s+(?!Rect\()")&&!rowBody.Contains("+\"")&&!rowBody.Contains("$\""),"an OUTFIT row allocates nothing per frame but the count past MaxDots");
        // On the model (owner 2026-10-28: "Remove Body Skin Options 'Prototype' and 'Sheldon Clothes' and just add 'Masks'
        // section"): Appearance's MASKS row picks no mask (Standard) or a face gear (the Prototype body), the Skins tab lists only
        // weapons, and the thumbnails are the model's (Tests/SkinToneChecks.cs pins the section itself).
        Check(skinsFile.Contains("private bool LkOnModel=>ArenaPresentation.VisualBodyInstalled&&prefs.playerModel!=\"Classic\";")&&skinsFile.Contains("if(BodySkinEquipped()!=NativeBodySkins.PrototypeIndex)SetBodySkin(NativeBodySkins.Prototype);}")
            &&!skinsFile.Contains("LkFaceCard")&&!skinsFile.Contains("LkFaceRow")&&!ui.Contains("NativeWeaponSkins.FaceEntry")&&skinsFile.Contains("if(LkOnModel||skin>=LkFaceThumb)LkModelThumb(")
            &&Body(skinsFile,"private void LkMasks(").Contains("k==0?NativeBodySkins.StandardIndex:LkFaceThumb+k-1"),"on the model the face gear is picked in MASKS, with the model's thumbnails");
        {
            // The setter's rule, run engine-free on Core (NativeCosmeticRules.Allowed is NativeCosmeticGate.Allowed, section 2):
            // whatever was stored, a non-DEV pilot is offered, marked, keeps and saves no Sheldon Clothes, a live DEV pilot can
            // pick all three, and the weapon picks around the body entry are untouched.
            double now=1e12;var dev=LockerAccess(true,now+60000);var lapsed=LockerAccess(true,now-1);var granted=LockerAccess(false,now+60000,"body:"+NativeBodySkins.SheldonClothes);
            Check(NativeBodySkins.Keys.Length==NativeBodySkins.Count&&NativeBodySkins.SheldonIndex==NativeBodySkins.Count-1&&NativeBodySkins.Keys[NativeBodySkins.SheldonIndex]==NativeBodySkins.Sheldon,"the DEV body is the last choice, so the non-DEV list stops before it");
            foreach(var (access,live) in new[]{(dev,true),(lapsed,false),(granted,false),(NativeAccess.Guest,false),((NativeAccess)null,false)})
            {
                bool allowed=NativeCosmeticGate.Allowed(access,"body",NativeBodySkins.SheldonClothes,now);Check(allowed==live,"the Sheldon Clothes are offered only with live DEV access ("+(access==null?"null":live?"DEV":"non-DEV")+")");
                int offered=allowed?NativeBodySkins.Count:NativeBodySkins.SheldonIndex;
                foreach(var stored in new[]{"","sword:prototype","body:prototype","body:sheldon","pistol:prototype,body:sheldon","rifle:prototype,body:prototype"})
                {
                    int k0=NativeBodySkins.Of(stored),marked=k0<offered?k0:NativeBodySkins.StandardIndex;
                    Check(marked==NativeBodySkins.Of(NativeBodySkins.Gate(stored,allowed)),"the marked body is the body Save Pilot keeps ("+stored+")");
                    for(int k=0;k<offered;k++)
                    {
                        string after=NativeBodySkins.Gate(NativeWeaponSkins.With(stored,NativeBodySkins.Entry,NativeBodySkins.Keys[k]),allowed);
                        Check(NativeBodySkins.Of(after)==k&&NativeBodySkins.Gate(after,allowed)==after,"picking "+NativeBodySkins.Keys[k]+" over \""+stored+"\" stores it and survives the save gate");
                        foreach(var spec in NativeWeaponSkins.Weapons)Check(NativeWeaponSkins.Index(after,spec)==NativeWeaponSkins.Index(stored,spec),"a body pick keeps the "+spec+" finish");
                    }
                    Check(allowed||NativeBodySkins.Of(NativeBodySkins.Gate(NativeWeaponSkins.With(stored,NativeBodySkins.Entry,NativeBodySkins.Sheldon),allowed))!=NativeBodySkins.SheldonIndex,"even a forced Sheldon pick is dropped for a non-DEV pilot");
                }
            }
        }

        // 5. Nothing inside the scroll clip is drawn rotated or scaled (a transformed draw would carry the clip with it).
        Check(!ui.Contains("U.Push("),"no transformed draws in the Locker");
        // 6. The turntable turns without deriving the portrait again.
        Check(portraits.Contains("public bool Same(in PortraitLook o)=>Spec==o.Spec&&Pose==o.Pose&&Hat==o.Hat&&Body==o.Body&&Accent==o.Accent&&Skin==o.Skin&&HatColor==o.HatColor&&BodySkin==o.BodySkin;")&&portraits.Contains("slot.Yaw=look.Yaw;")&&portraits.Contains("bones[b]=spin*Matrix4x4.TRS("),"yaw is a render input, not part of the look");

        // 7. Ability slots read from content, by spec and slot; several options per slot are kept in content order.
        var options=new List<NativeLocker.AbilityOption>();var inv=CultureInfo.InvariantCulture;
        foreach(var a in data.abilities)
        {
            var w=data.Weapon(a.spec);int n=NativeLocker.SlotOptions(data,w,a.slot,options);var expected=data.abilities.Where(x=>x.spec==a.spec&&x.slot==a.slot).ToArray();
            Check(n==expected.Length&&options.Select(o=>o.Id).SequenceEqual(expected.Select(x=>x.id)),a.spec+" "+a.slot+" options from content");
            var o0=options.First(o=>o.Id==a.id);
            // The DEV Cyber Lord's Q, E and 1-3 show Kit v2 (the shipped kit) over its content's v1 strings; the id stays.
            int k2=NativeCyberLordText.Slot(w,a);double cooldown=k2>=0?CyberKit2Spec.Cooldowns[k2]:a.cooldown,cost=k2>=0?CyberKit2Spec.Costs[k2]:a.cost;
            if(k2>=0)Check(o0.Name==CyberKit2Spec.Names[k2]&&o0.Text==NativeCyberLordText.Texts[k2]&&o0.Icon==CyberKit2Spec.Icons[k2]&&o0.Cooldown==cooldown&&o0.Cost==cost&&!o0.FromWeapon,a.spec+" "+a.slot+" option fields (Kit v2)");
            else Check(o0.Name==a.name&&o0.Text==a.text&&o0.Cooldown==a.cooldown&&o0.Cost==a.cost&&!o0.FromWeapon,a.spec+" "+a.slot+" option fields");
            Check(o0.Meta==(a.kind=="ads"&&k2<0?"Hold":cooldown>0?cooldown.ToString("0.#",inv)+" s cooldown":"No cooldown")+" · "+(cost>0?cost.ToString("0.#",inv)+(w.@class=="melee"&&w.id!="cyberlord"?" stamina":" mana"):"no cost"),a.spec+" "+a.slot+" meta "+o0.Meta);
            Check((icons.Contains(o0.Icon)||art.Contains(o0.Icon))&&o0.Icon!="sparkles",a.spec+" "+a.slot+" icon "+o0.Icon);
        }
        {
            // A second option for one slot (future content) lists both, in order.
            var copy=new ConversionContent{abilities=data.abilities.Concat(new[]{new ContentAbility{spec="pistol",slot="e",id="test-flash",name="Flashbang",text="t",kind="throw",cooldown=6}}).ToArray()};
            Check(NativeLocker.SlotOptions(copy,data.Weapon("pistol"),"e",options)==2&&options[0].Id=="frag-grenade"&&options[1].Id=="test-flash","several options per slot");
        }
        foreach(var spec in new[]{"sword","daggers","shield","greatsword"})
        {
            var w=data.Weapon(spec);
            foreach(var slot in new[]{"q","e"})
            {
                Check(NativeLocker.SlotOptions(data,w,slot,options)==1&&!options[0].FromWeapon&&options[0].Id==data.Ability(spec,slot).id,spec+" content kit "+slot);
                Check(options[0].Meta.EndsWith(" stamina")&&icons.Contains(options[0].Icon),spec+" stamina and icon "+slot);
            }
        }
        Check(NativeLocker.Move(data.Weapon("sword").description,"E: ")=="Rising slash launches enemies for a brief aerial juggle.","the E sentence of a description");
        Check(NativeLocker.SlotOptions(data,data.Weapon("emperor"),"q",options)==1&&NativeLocker.SlotOptions(data,data.Weapon("ansatsuken"),"q",options)==0&&NativeLocker.EmptySlot(data.Weapon("ansatsuken"),"q").Contains("DEV"),"DEV kits show their own keys");
        Check(ui.Contains("private int LockerEquippedAbility(")&&ui.Contains("private static bool LockerAbilityPicks=>false;"),"ability picks wait for the loadout field (no invented persistence)");

        Console.WriteLine("PASS Locker: "+count+" checks (five categories, gated cosmetic lists, the Save Pilot path and unsaved-changes prompt, the weapon skin selector, the Appearance BODY SKIN row sharing the Skins card's setter and DEV gate, content ability slots)");
    }
    private static int Count(string s,string what){int n=0;for(int i=s.IndexOf(what,StringComparison.Ordinal);i>=0;i=s.IndexOf(what,i+what.Length,StringComparison.Ordinal))n++;return n;}
    private static string[] Ids(string source,string name)
    {
        var m=Regex.Match(source,@"public static readonly string\[\] "+name+@"=\{([^}]*)\}");return m.Success?Regex.Matches(m.Groups[1].Value,"\"([^\"]+)\"").Select(x=>x.Groups[1].Value).ToArray():Array.Empty<string>();
    }
    // The text from `from` to the next `to` after it (empty when either is missing).
    private static string Between(string source,string from,string to)
    {
        int a=source.IndexOf(from,StringComparison.Ordinal);if(a<0)return "";int b=source.IndexOf(to,a+from.Length,StringComparison.Ordinal);return b<0?"":source.Substring(a,b-a);
    }
    // A NativeAccess as Verify would make it (reflection: its setters are private), as BodySkinChecks builds one.
    private static NativeAccess LockerAccess(bool developer,double expires,params string[] grants)
    {
        var a=(NativeAccess)Activator.CreateInstance(typeof(NativeAccess),true);var t=typeof(NativeAccess);
        t.GetProperty("Developer").SetValue(a,developer);t.GetProperty("Expires").SetValue(a,expires);
        var set=(HashSet<string>)t.GetField("grants",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(a);foreach(var g in grants)set.Add(g);return a;
    }
    // A method's text from its signature to its closing brace.
    private static string Body(string source,string signature)
    {
        int at=source.IndexOf(signature,StringComparison.Ordinal);if(at<0)return "";int open=source.IndexOf('{',at),depth=0;
        for(int i=open;i<source.Length;i++){if(source[i]=='{')depth++;else if(source[i]=='}'&&--depth==0)return source.Substring(at,i-at+1);}
        return "";
    }
}
