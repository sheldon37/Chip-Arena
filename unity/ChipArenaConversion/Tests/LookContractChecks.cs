using System;
using System.IO;
using ChipArena.UnityConversion;
internal static class LookContractChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Look contract: "+why);}
        foreach(var allowed in new[]{false,true})foreach(var slot in NativeLook.Slots)foreach(var id in NativeLook.Ids[NativeLook.SlotIndex(slot)])
        {
            string map=NativeLook.With("",slot,id),result=NativeLook.Gate(map,allowed);
            Check(result==(allowed||!NativeLook.DevOnly(id)?map:""),"slot/id DEV gate "+id);
            Check(NativeLook.Normalize(result)==result&&result.Length<=NativeLook.MaxLength,"canonical bounded map");
        }
        // The Hot Dog (owner 2026-11-08: "a dev skin"): a DEV piece of the map (dropped without live DEV access), kept across an
        // account load until the Worker lists it, worn as one whole body.
        string dog=NativeLook.With("feet:sneakers",NativeLook.Upper,NativeHotDog.Id);
        Check(NativeHotDog.Worn(dog)&&!NativeHotDog.Worn("")&&!NativeHotDog.Worn("upper:tee")&&NativeLook.DevOnly(NativeHotDog.Id)&&NativeLook.Gate(dog,true)==dog&&NativeLook.Gate(dog,false)=="feet:sneakers","the Hot Dog's a DEV piece");
        Check(NativeHotDog.Worn(NativeLook.KeepSkin("hair:wild",dog))&&!NativeHotDog.Worn(NativeLook.KeepSkin("hair:wild","upper:tee"))&&NativeHotDog.Worn(NativeRobotSuit.Dress(dog)),"the Hot Dog survives a load and dressing");
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        Check(R("NativeVisualBodyPresentation.cs").Contains("if(NativeHotDog.Worn(look))HotDogWear(root);")&&R("NativeRobotHelmPresentation.cs").Contains("NativeHotDog.Worn(VisualOutfitOf(id))?NativeWeaponSkins.FaceNone")&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeRobotSuit.cs")).Contains("if(NativeHotDog.Worn(look))return 0;"),"the Hot Dog dresses every body, without the robot's helmet or plates");
        Check(R("NativeNetwork.cs").Contains("look=GatedLook()")&&R("NativeNetwork.cs").Contains("look=NativeLook.Gate(c.look,NativeCosmeticRules.Allowed(access,"),"host uses verified access, profile sends look");
        Check(R("NativeNetwork.cs").Contains("s.look.Length>NativeLook.MaxLength")&&R("NativeNetworkFrame.cs").Contains("s.look.Length>NativeLook.MaxLength"),"both wire bounds");
        Check(R("NativeNetworkFrame.cs").Contains("look=s.look,")&&R("NativeLobbyLogic.cs").Contains("look=s.look,")&&R("NativeNetworkFrame.cs").Contains("seat.look=NativeLook.Normalize(sf.look);"),"capture, lobby and apply");
        Check(R("NativeProfileUI.cs").Contains("if(c.look!=null)prefs.look=NativeLook.KeepSkin(NativeLook.Gate(c.look,"),"absent old account preserves local selection (and a local skin tone the account lacks)");
        Check(R("NativeVisualBodyPresentation.cs").Contains("WeaponLoadout?.Match?.Seat(id)")&&R("NativePortraitUI.cs").Contains("PortraitOutfit(key)"),"displayed arena and remote portraits");
        // Cosmetic state may be transported by networking, never used by damage/movement simulation.
        foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
        {
            string name=Path.GetFileName(f);
            if(name=="NativeArena.cs"||name.StartsWith("NativeCombat")||name.StartsWith("NativeDamage")||name.StartsWith("NativeMovement")||name.StartsWith("NativePrediction")||name=="NativeAbilities.cs")
                Check(!File.ReadAllText(f).Contains("NativeLook"),"authority is outfit independent: "+name);
        }
        foreach(var file in new[]{
            "Core/ArenaWorld.cs","Core/NativeCombatRules.cs","Core/NativeMeleeRules.cs","Core/NativePrediction.cs",
            "Runtime/NativeMelee.cs","Runtime/NativeFighters.cs","Runtime/NativeChips.cs","Runtime/NativePveHorde.cs",
            "Runtime/NativeMeleeSpecials.cs","Runtime/NativeProjectiles.cs"})
        {
            string path=Path.Combine(root,"Assets/ChipArena",file);
            Check(File.Exists(path),"authority scan target exists: "+file);
            Check(!File.ReadAllText(path).Contains("NativeLook"),"authority is outfit independent: "+file);
        }
        Console.WriteLine("PASS outfit contract: slot gates, network/profile routes and authority isolation");
    }
}
