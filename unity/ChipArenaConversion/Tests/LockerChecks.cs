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
// and the weapon skins tab draws nothing interactive. Drawing itself is checked by the offline menu renders, not here.
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
        Check(NativeLocker.Labels.SequenceEqual(new[]{"Appearance","Kill Effects","Entrance","Loadout","Weapon Skins"}),"category labels");
        foreach(var icon in NativeLocker.Icons)Check(icons.Contains(icon),"category icon "+icon+" is in Icons.json");
        for(int i=0;i<5;i++)Check(NativeLocker.Interactive(i)==(i!=NativeLocker.WeaponSkins)&&NativeLocker.Soon(i)==(i==NativeLocker.WeaponSkins),"only the weapon skins are coming soon and non-interactive ("+keys[i]+")");
        foreach(var tab in new[]{"Appearance","KillEffects","Entrance","Loadout"})Check(Count(ui,"case NativeLocker."+tab+":")==2,"category "+tab+" is laid out and drawn");
        Check(ui.Contains("default:LkReserve(76);")&&ui.Contains("default:LkSkins(lockerSlots[0],lockerSlots[1],lockerSlots[2],e);break;"),"the weapon skins category is laid out and drawn");
        Check(ui.Contains("for(int i=0;i<NativeLocker.Count;i++)")&&ui.Contains("NativeLocker.Labels[i]")&&ui.Contains("lockerTabNext=i;"),"the rail offers every category");

        // 2. Restricted cosmetics stay hidden: every list comes through NativeCosmeticRules.Allowed, and the gate itself is
        // unchanged (dev- items and the Auditor Halo need a grant; the Gibber needs DEV access).
        Check(ui.Contains("foreach(var id in NativeCosmeticRules.Hats)if(NativeCosmeticRules.Allowed(access,\"hat\",id))lockerHats.Add(id);")
            &&ui.Contains("foreach(var id in NativeCosmeticRules.Kills)if(NativeCosmeticRules.Allowed(access,\"effect\",id))lockerKills.Add(id);")
            &&ui.Contains("foreach(var id in NativeCosmeticRules.Intros)if(NativeCosmeticRules.Allowed(access,\"intro\",id))lockerIntros.Add(id);")
            &&ui.Contains("foreach(var w in content.weapons)if(AllowedWeapon(w))lockerSkins.Add(w.id);"),"hats, kill effects, intros and skin weapons are filtered by the access rules");
        foreach(var raw in new[]{"NativeCosmeticRules.Hats","NativeCosmeticRules.Kills","NativeCosmeticRules.Intros"})Check(Count(ui,raw)==1,raw+" is read only by the gated list builder");
        Check(Regex.Matches(ui,@"for\(int i=0;i<locker(Hats|Kills|Intros)\.Count;i\+\+\)").Count==3,"the cards draw the gated lists");
        Check(ui.Contains("NativeLoadoutText.InClass")||ui.Contains("MenuWeapons();"),"the weapon cards use the loadout's gated weapon list (MenuWeapons)");
        Check(profile.Contains("public static bool Allowed(NativeAccess a,string type,string id)=>id!=null&&(id!=\"dev-gibber\"||(a??NativeAccess.Guest).Developer)&&( !id.StartsWith(\"dev-\")&&id!=\"auditor-halo\"||(a??NativeAccess.Guest).Allows(type+\":\"+id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));"),"the cosmetic gate is unchanged");
        var hats=Ids(profile,"Hats");var kills=Ids(profile,"Kills");var intros=Ids(profile,"Intros");
        Check(hats.Length==11&&kills.Length==22&&intros.Length==22,"cosmetic id lists parsed ("+hats.Length+"/"+kills.Length+"/"+intros.Length+")");
        Check(intros.SequenceEqual(NativeIntroRules.Entrances.OrderBy(i=>Array.IndexOf(intros,i)))&&intros.OrderBy(i=>i).SequenceEqual(NativeIntroRules.Entrances.OrderBy(i=>i)),"the Locker offers exactly the staged entrances");
        foreach(var id in NativeIntroRules.Procedural)Check(NativeLocker.IntroIcon(id)!=null&&!NativeLocker.DevItem(id),"the procedural entrance "+id+" has its icon and no DEV badge");
        foreach(var id in NativeIntroRules.Retired)Check(Array.IndexOf(intros,id)<0&&NativeLocker.IntroText(id).Length==0&&NativeLocker.IntroIcon(id)==null,"the retired "+id+" is gone from the Locker");
        foreach(var id in hats.Concat(kills).Concat(intros))Check(NativeLocker.DevItem(id)==(id.StartsWith("dev-")||id=="auditor-halo"),"DEV badge for "+id);
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
        foreach(var field in new[]{"name","weapon","lobbyPose","bodyColor","accentColor","hat","killEffect","intro","introText","overrideIntro"})
            Check(Regex.IsMatch(ui,@"b\.\w+!=prefs\."+field+@"\b")&&Regex.IsMatch(ui,@"prefs\."+field+@"=b\.\w+;"),"the baseline covers and restores prefs."+field);
        Check(profile.Contains("LockerRebase();};"),"an account profile loaded meanwhile becomes the baseline");

        // 4. The weapon skins tab is drawing only: no buttons, hover state, clicks or writes.
        string skins=Body(ui,"private void LkSkins(");
        Check(skins.Length>200,"the weapon skins drawing exists");
        foreach(var bad in new[]{"Button(","HoverAmount(","Hover(","Click(","prefs.","SetWeapon(","menuClass="})Check(!skins.Contains(bad),"the weapon skins tab has no "+bad);

        // 5. Nothing inside the scroll clip is drawn rotated or scaled (a transformed draw would carry the clip with it).
        Check(!ui.Contains("U.Push("),"no transformed draws in the Locker");
        // 6. The turntable turns without deriving the portrait again.
        Check(portraits.Contains("public bool Same(in PortraitLook o)=>Spec==o.Spec&&Pose==o.Pose&&Hat==o.Hat&&Body==o.Body&&Accent==o.Accent;")&&portraits.Contains("slot.Yaw=look.Yaw;")&&portraits.Contains("bones[b]=spin*Matrix4x4.TRS("),"yaw is a render input, not part of the look");

        // 7. Ability slots read from content, by spec and slot; several options per slot are kept in content order.
        var options=new List<NativeLocker.AbilityOption>();var inv=CultureInfo.InvariantCulture;
        foreach(var a in data.abilities)
        {
            var w=data.Weapon(a.spec);int n=NativeLocker.SlotOptions(data,w,a.slot,options);var expected=data.abilities.Where(x=>x.spec==a.spec&&x.slot==a.slot).ToArray();
            Check(n==expected.Length&&options.Select(o=>o.Id).SequenceEqual(expected.Select(x=>x.id)),a.spec+" "+a.slot+" options from content");
            var o0=options.First(o=>o.Id==a.id);Check(o0.Name==a.name&&o0.Text==a.text&&o0.Cooldown==a.cooldown&&o0.Cost==a.cost&&!o0.FromWeapon,a.spec+" "+a.slot+" option fields");
            Check(o0.Meta==(a.kind=="ads"?"Hold":a.cooldown>0?a.cooldown.ToString("0.#",inv)+" s cooldown":"No cooldown")+" · "+(a.cost>0?a.cost.ToString("0.#",inv)+" mana":"no cost"),a.spec+" "+a.slot+" meta "+o0.Meta);
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
            Check(NativeLocker.SlotOptions(data,w,"q",options)==0&&NativeLocker.EmptySlot(w,"q").Contains("on the way"),spec+" Q is empty for now");
            Check(NativeLocker.SlotOptions(data,w,"e",options)==1&&options[0].FromWeapon&&options[0].Id==w.secondary&&options[0].Name==NativeLocker.MeleeSpecial(spec)&&options[0].Cooldown==w.secondaryCooldown&&options[0].Cost==w.secondaryCost,spec+" E is its stamina special");
            Check(options[0].Text.Length>10&&!options[0].Text.Contains("stamina")&&options[0].Meta.EndsWith(" stamina")&&icons.Contains(options[0].Icon),spec+" E text, meta and icon");
        }
        Check(NativeLocker.Move(data.Weapon("sword").description,"E: ")=="Rising slash launches enemies for a brief aerial juggle.","the E sentence of a description");
        Check(NativeLocker.SlotOptions(data,data.Weapon("emperor"),"q",options)==1&&NativeLocker.SlotOptions(data,data.Weapon("ansatsuken"),"q",options)==0&&NativeLocker.EmptySlot(data.Weapon("ansatsuken"),"q").Contains("DEV"),"DEV kits show their own keys");
        Check(ui.Contains("private int LockerEquippedAbility(")&&ui.Contains("private static bool LockerAbilityPicks=>false;"),"ability picks wait for the loadout field (no invented persistence)");

        Console.WriteLine("PASS Locker: "+count+" checks (five categories, gated cosmetic lists, the Save Pilot path and unsaved-changes prompt, inert weapon skins, content ability slots)");
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
