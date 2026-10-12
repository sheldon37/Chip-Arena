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
// and the weapon skins tab only picks skins. Drawing itself is checked by the offline menu renders, not here.
internal static class LockerChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Locker: "+what);count++;}
    public static void Run(string root,ConversionContent data)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        string ui=R("NativeLockerUI.cs"),home=R("NativeHomeMenu.cs"),profile=R("NativeProfileUI.cs"),lobby=R("NativeLobbyUI.cs"),portraits=R("NativePortraits.cs");
        var icons=new HashSet<string>();using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json"))))foreach(var i in doc.RootElement.GetProperty("icons").EnumerateArray())icons.Add(i.GetProperty("id").GetString());

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
            &&gate.Contains("public static bool DevOnly(string id)=>id==\"dev-gibber\"||id==NativeHatLooks.SheldonHead||id==SheldonIntro||id==NativeBodySkins.SheldonClothes;")
            &&gate.Contains("public static bool Restricted(string id)=>id!=null&&(id.StartsWith(\"dev-\",StringComparison.Ordinal)||id==\"auditor-halo\"||id==NativeHatLooks.SheldonHead||id==SheldonIntro||id==NativeBodySkins.SheldonClothes);")
            &&gate.Contains("public static bool Allowed(NativeAccess a,string type,string id,double now)=>id!=null&&(!DevOnly(id)||(a??NativeAccess.Guest).Developer)&&(!Restricted(id)||(a??NativeAccess.Guest).Allows(type+\":\"+id,now));"),"the cosmetic gate is unchanged for legacy grants (moved to Core; the Sheldon Head and Sheldon Intro join the Gibber behind live DEV access)");
        var hats=Ids(profile,"Hats");var kills=Ids(profile,"Kills");var intros=Ids(profile,"Intros");
        Check(hats.Length==12&&kills.Length==22&&intros.Length==24,"cosmetic id lists parsed ("+hats.Length+"/"+kills.Length+"/"+intros.Length+")");
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
        Check(home.Contains("private void CustomizeScreen(float age)=>LockerScreen(age);")&&home.Contains("if(screen==MenuScreen.Customize&&menuScreen!=MenuScreen.Customize)LockerEnter();"),"Customize opens the Locker and takes its baseline on entry");
        Check(home.Contains("if(menuScreen==MenuScreen.Customize){LockerBack();return;}")&&ui.Contains("private void LockerBack(){if(lockerConfirm){lockerConfirm=false;return;}if(LockerDirty())lockerConfirm=true;else LockerLeave();}"),"Escape and close ask before dropping unsaved changes");
        Check(ui.Contains("private void LockerLeave(){lockerConfirm=false;Go(InRoom?MenuScreen.Lobby:MenuScreen.Home);}")&&lobby.Contains("(\"palette\",\"Customize\",MenuScreen.Customize)"),"the lobby's Customize entry returns to the lobby");
        foreach(var field in new[]{"name","weapon","lobbyPose","bodyColor","accentColor","hat","killEffect","intro","introText","overrideIntro","weaponSkins","lobbyBack","hatColor","backColor"})
            Check(Regex.IsMatch(ui,@"b\.\w+!=prefs\."+field+@"\b")&&Regex.IsMatch(ui,@"prefs\."+field+@"=b\.\w+;"),"the baseline covers and restores prefs."+field);
        Check(profile.Contains("LockerRebase();};"),"an account profile loaded meanwhile becomes the baseline");

        // 4. The weapon skins tab (Runtime/NativeLockerSkinsUI.cs) picks the body skin and a skin per weapon: its only write is
        // prefs.weaponSkins through the normalizing setter (saved by Save Pilot), hovering only tries one on the stage.
        string skins=Body(R("NativeLockerSkinsUI.cs"),"private void LkSkins(");
        Check(skins.Length>200&&!ui.Contains("private void LkSkins("),"the weapon skins drawing lives in its own file");
        Check(Regex.Matches(skins,@"prefs\.\w+=").Count==1&&skins.Contains("prefs.weaponSkins=NativeWeaponSkins.With(prefs.weaponSkins,entry,keys[k]);")&&skins.Contains("string entry=body?NativeBodySkins.Entry:w.id;var keys=body?NativeBodySkins.Keys:NativeWeaponSkins.Keys;"),"the skin selector writes only the skin map");
        foreach(var bad in new[]{"SetWeapon(","menuClass=","SavePilot(","Save("})Check(!skins.Contains(bad),"the weapon skins tab has no "+bad);

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
            var o0=options.First(o=>o.Id==a.id);Check(o0.Name==a.name&&o0.Text==a.text&&o0.Cooldown==a.cooldown&&o0.Cost==a.cost&&!o0.FromWeapon,a.spec+" "+a.slot+" option fields");
            Check(o0.Meta==(a.kind=="ads"?"Hold":a.cooldown>0?a.cooldown.ToString("0.#",inv)+" s cooldown":"No cooldown")+" · "+(a.cost>0?a.cost.ToString("0.#",inv)+(w.@class=="melee"?" stamina":" mana"):"no cost"),a.spec+" "+a.slot+" meta "+o0.Meta);
            Check(icons.Contains(o0.Icon)&&o0.Icon!="sparkles",a.spec+" "+a.slot+" icon "+o0.Icon);
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

        Console.WriteLine("PASS Locker: "+count+" checks (five categories, gated cosmetic lists, the Save Pilot path and unsaved-changes prompt, the weapon skin selector, content ability slots)");
    }
    private static int Count(string s,string what){int n=0;for(int i=s.IndexOf(what,StringComparison.Ordinal);i>=0;i=s.IndexOf(what,i+what.Length,StringComparison.Ordinal))n++;return n;}
    private static string[] Ids(string source,string name)
    {
        var m=Regex.Match(source,@"public static readonly string\[\] "+name+@"=\{([^}]*)\}");return m.Success?Regex.Matches(m.Groups[1].Value,"\"([^\"]+)\"").Select(x=>x.Groups[1].Value).ToArray():Array.Empty<string>();
    }
    // A method's text from its signature to its closing brace.
    private static string Body(string source,string signature)
    {
        int at=source.IndexOf(signature,StringComparison.Ordinal);if(at<0)return "";int open=source.IndexOf('{',at),depth=0;
        for(int i=open;i<source.Length;i++){if(source[i]=='{')depth++;else if(source[i]=='}'&&--depth==0)return source.Substring(at,i-at+1);}
        return "";
    }
}
