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
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        Check(R("NativeNetwork.cs").Contains("look=GatedLook()")&&R("NativeNetwork.cs").Contains("look=NativeLook.Gate(c.look,NativeCosmeticRules.Allowed(access,"),"host uses verified access, profile sends look");
        Check(R("NativeNetwork.cs").Contains("s.look.Length>NativeLook.MaxLength")&&R("NativeNetworkFrame.cs").Contains("s.look.Length>NativeLook.MaxLength"),"both wire bounds");
        Check(R("NativeNetworkFrame.cs").Contains("look=s.look,")&&R("NativeLobbyLogic.cs").Contains("look=s.look,")&&R("NativeNetworkFrame.cs").Contains("seat.look=NativeLook.Normalize(sf.look);"),"capture, lobby and apply");
        Check(R("NativeProfileUI.cs").Contains("if(c.look!=null)prefs.look=NativeLook.Gate"),"absent old account preserves local selection");
        Check(R("NativeVisualBodyPresentation.cs").Contains("WeaponLoadout?.Match?.Seat(id)")&&R("NativePortraitUI.cs").Contains("PortraitOutfit(key)"),"displayed arena and remote portraits");
        // Cosmetic state may be transported by networking, never used by damage/movement simulation.
        foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
        {
            string name=Path.GetFileName(f);
            if(name=="NativeArena.cs"||name.StartsWith("NativeCombat")||name.StartsWith("NativeDamage")||name.StartsWith("NativeMovement")||name.StartsWith("NativePrediction")||name=="NativeAbilities.cs")
                Check(!File.ReadAllText(f).Contains("NativeLook"),"authority is outfit independent: "+name);
        }
        Console.WriteLine("PASS outfit contract: slot gates, network/profile routes and authority isolation");
    }
}
