using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;
// The main menu's DEV screen (owner 2026-11-09: "Rework the dev menu (Where we can modify players); Make it cleaner and easier to use.
// Make it look a lot better too. Everytime we make a dev class, add them as an option here."): what it offers, what a save sends, and
// that the grants it lets through are the account server's own (src/shared/access.ts), which refuses a whole save with any other key.
internal static class DevGrantsChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("DEV grants: "+why);n++;}
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));

        // The server's lists, mirrored: the screen may only send these keys.
        string access=Path.GetFullPath(Path.Combine(root,"../../src/shared/access.ts"));bool server=File.Exists(access);
        if(server)
        {
            string ts=File.ReadAllText(access);
            var grantable=Regex.Match(ts,@"GRANTABLE_CONTENT=\{([\s\S]*?)\}\s*as const");Check(grantable.Success,"the server's GRANTABLE_CONTENT is found");
            var keys=Regex.Matches(grantable.Groups[1].Value,@"'([^']+)'\s*:").Select(m=>m.Groups[1].Value).ToArray();
            Check(keys.Length>0&&keys.SequenceEqual(NativeDevGrants.Server),"NativeDevGrants.Server is the server's GRANTABLE_CONTENT ("+string.Join(",",keys)+")");
            var flags=Regex.Match(ts,@"ACCOUNT_FLAGS=\{([^}]*)\}");Check(flags.Success,"the server's ACCOUNT_FLAGS is found");
            var flagKeys=Regex.Matches(flags.Groups[1].Value,@"([A-Za-z_]+)\s*:").Select(m=>m.Groups[1].Value).ToArray();
            Check(flagKeys.SequenceEqual(NativeDevGrants.Flags),"NativeDevGrants.Flags is the server's ACCOUNT_FLAGS ("+string.Join(",",flagKeys)+")");
        }
        // A weapon the client keeps to DEV accounts whatever the grants (NativeAccess.Allows: DeveloperWeapon) is not one the server
        // grants: a grant of it would be saved and never honoured.
        var accessCs=R("Core/NativeAccess.cs");var devWeapons=Regex.Matches(accessCs,"key==\"weapon:([a-z]+)\"").Select(m=>m.Groups[1].Value).ToList();
        if(accessCs.Contains("key==\"weapon:\"+NativeTopDog.Spec"))devWeapons.Add(NativeTopDog.Spec);
        Check(devWeapons.Count>=3,"the client's DEV-account-only weapons are found ("+string.Join(",",devWeapons)+")");
        foreach(var w in devWeapons)Check(!NativeDevGrants.Grantable("weapon:"+w),"weapon:"+w+" is for DEV accounts only, so not offered as a grant");

        // Changes against what the player holds.
        var have=new[]{"weapon:siren","x:unknown"};var changes=new Dictionary<string,bool>();
        NativeDevGrants.Toggle(have,changes,"weapon:emperor",NativeDevGrants.Server);
        Check(NativeDevGrants.On(have,changes,"weapon:emperor")&&NativeDevGrants.Changes(have,changes)==1,"turning a grantable key on is one change");
        NativeDevGrants.Toggle(have,changes,"weapon:emperor",NativeDevGrants.Server);
        Check(!NativeDevGrants.On(have,changes,"weapon:emperor")&&changes.Count==0,"turning it back off is no change at all");
        NativeDevGrants.Toggle(have,changes,"weapon:"+NativeTopDog.Spec,NativeDevGrants.Server);Check(changes.Count==0,"a key the server would refuse cannot be turned on");
        NativeDevGrants.Toggle(have,changes,"weapon:siren",NativeDevGrants.Server);
        Check(!NativeDevGrants.On(have,changes,"weapon:siren")&&NativeDevGrants.Changes(have,changes)==1,"a held key turns off");
        NativeDevGrants.Toggle(have,changes,"intro:dev-siren",NativeDevGrants.Server);
        var sent=NativeDevGrants.Merge(have,changes,NativeDevGrants.Server);
        Check(sent.Length==2&&sent.Contains("x:unknown")&&sent.Contains("intro:dev-siren")&&!sent.Contains("weapon:siren"),"a save sends the held keys less those turned off, plus those turned on, keeping keys the client does not know ("+string.Join(",",sent)+")");
        var forced=new Dictionary<string,bool>{{"weapon:"+NativeTopDog.Spec,true},{"effect:"+NativeHotdogFriends.Style,true}};
        Check(NativeDevGrants.Merge(null,forced,NativeDevGrants.Server).Length==0,"a save never adds a key the server would refuse");
        var all=new Dictionary<string,bool>();NativeDevGrants.SetAll(Array.Empty<string>(),all,new[]{"weapon:siren","weapon:"+NativeTopDog.Spec,"intro:dev-siren"},true,NativeDevGrants.Server);
        Check(all.Count==2&&all["weapon:siren"]&&all["intro:dev-siren"],"ALL turns on only the grantable keys");
        NativeDevGrants.SetAll(new[]{"weapon:siren"},all,new[]{"weapon:siren","intro:dev-siren"},false,NativeDevGrants.Server);
        Check(all.Count==1&&!all["weapon:siren"]&&NativeDevGrants.Changes(new[]{"weapon:siren"},all)==1,"NONE turns them off (a key the player never had needs no change)");
        var flagChanges=new Dictionary<string,bool>();NativeDevGrants.Toggle(new[]{"vip"},flagChanges,"vip",NativeDevGrants.Flags);NativeDevGrants.Toggle(new[]{"vip"},flagChanges,"playtester",NativeDevGrants.Flags);
        var flagsSent=NativeDevGrants.Merge(new[]{"vip"},flagChanges,NativeDevGrants.Flags);Check(flagsSent.Length==1&&flagsSent[0]=="playtester","labels change the same way");

        // What the screen offers: every DEV look a grant unlocks, never the owner's own.
        Check(NativeDevGrants.Offered(NativeHotdogFriends.Style)&&NativeDevGrants.Offered("dev-clown")&&NativeDevGrants.Offered("auditor-halo")&&NativeDevGrants.Offered("dev-topdog"),"the DEV kill effects, the Auditor Halo and the DEV entrances are offered");
        foreach(var own in new[]{"dev-gibber",NativeHatLooks.SheldonHead,NativeCosmeticGate.SheldonIntro,NativeCosmeticGate.SheldonIdle,"ragdoll","crown","weapon-sword"})Check(!NativeDevGrants.Offered(own),own+" is not offered");
        Check(NativeDevGrants.Grantable("hat:auditor-halo")&&!NativeDevGrants.Grantable("effect:"+NativeHotdogFriends.Style),"the Auditor Halo is granted; Hotdog Friends waits for the account server");

        // Every DEV class in the content is a class card with its icon (a new DEV class needs nothing added here).
        using var gameContent=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")));
        using var icons=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json")));
        using var art=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ClassIcons.json")));
        var specIcons=new HashSet<string>(icons.RootElement.GetProperty("specs").EnumerateArray().Concat(art.RootElement.GetProperty("specs").EnumerateArray()).Select(e=>e.GetProperty("id").GetString()));
        int dev=0;
        foreach(var wpn in gameContent.RootElement.GetProperty("weapons").EnumerateArray())
        {
            if(!(wpn.TryGetProperty("devOnly",out var d)&&d.GetBoolean()))continue;dev++;string id=wpn.GetProperty("id").GetString();
            Check(specIcons.Contains(id),"DEV class "+id+" has its card's icon");
        }
        Check(dev>=7,"the content has its DEV classes ("+dev+")");
        var ui=R("Runtime/NativeDevGrantsUI.cs");
        Check(ui.Contains("if(w==null||!w.devOnly)continue;string intro=LockerSignature(w.id);")&&ui.Contains("var item=new AdminItem{Key=\"weapon:\"+w.id,"),"the class cards are the content's devOnly classes, each with its own entrance");
        Check(ui.Contains("foreach(var id in NativeCosmeticRules.Kills)if(NativeDevGrants.Offered(id))")&&ui.Contains("foreach(var id in NativeCosmeticRules.Hats)if(NativeDevGrants.Offered(id))")&&ui.Contains("foreach(var id in NativeCosmeticRules.Intros)if(NativeDevGrants.Offered(id)&&!paired.Contains(id))"),"the looks are the Locker's lists' DEV items");
        Check(ui.Contains("unlocks=NativeDevGrants.Merge(p.unlocks,g,NativeDevGrants.Server),flags=NativeDevGrants.Merge(p.flags,f,NativeDevGrants.Flags)"),"a save sends the merged keys");
        Check(Regex.Matches(ui,@"account\.Manage\(").Count==2&&ui.Contains("account.Manage(\"players\",")&&ui.Contains("account.Manage(\"grant\","),"the screen's only requests are the search and the save");
        Check(Regex.Matches(ui,@"\bp\.(unlocks|flags|grantVersion|developer)\s*=[^=]").Count==4&&ui.Contains("p.grantVersion=a.grantVersion;p.unlocks=a.unlocks;p.flags=a.flags;p.developer=a.developer;"),"a player's record changes only from the server's answer to a save");
        Check(Regex.Matches(ui,@"NativeDevGrants\.(Toggle|SetAll)\(").Count==5,"every toggle goes through NativeDevGrants (classes and entrances, looks, ALL, NONE, labels)");
        Check(R("Runtime/NativeHomeMenu.cs").Contains("private void DevScreen(float age)=>DevGrantsScreen(age);")&&!R("Runtime/NativeAdmin.cs").Contains("AdminBody"),"home · DEV opens the new screen; the old form is gone");
        Console.WriteLine("PASS DEV grants: "+n+" checks (the server's grant keys and labels mirrored"+(server?"":" [server source not present]")+", DEV-account weapons kept out, changes and what a save sends, every DEV class and look offered, never the owner's own)");
    }
}
