using System;
using System.IO;
using ChipArena.UnityConversion;

// The build stamp (Core/NativeBuildLabel.cs): a packaged build shows its 12-character source hash and UTC build time, an
// implausible revision shows no time, a missing identity reads DEV BUILD with the content's source commit, and the runtime
// draws it after every screen (ConversionGame.OnGUI) from the updater's identity or the player's own native-build.json.
internal static class BuildLabelChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Build label: "+what);count++;}
    public static void Run(string root)
    {
        long ticks=new DateTime(2026,10,13,3,50,0,DateTimeKind.Utc).Ticks;
        var build=new NativeBuildIdentity{version="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",revision=ticks};
        Check(NativeBuildLabel.Text(build,"ffff")=="BUILD 0123456789ab · 2026-10-13 03:50 UTC","packaged build reads hash and UTC time");
        Check(NativeBuildLabel.Short(build.version)==NativeUpdater_Short(build.version),"hash matches the footer and update card");
        Check(NativeBuildLabel.Text(new NativeBuildIdentity{version="abc",revision=2},null)=="BUILD abc","small revision shows no time");
        Check(NativeBuildLabel.Text(null,"05950eaef80ff2483a9c")=="DEV BUILD · SRC 05950eaef80f","missing identity reads DEV BUILD with the source commit");
        Check(NativeBuildLabel.Text(new NativeBuildIdentity{version=""},"")=="DEV BUILD","empty identity and source");
        Check(!NativeBuildLabel.Stamp(long.MaxValue,out _)&&!NativeBuildLabel.Stamp(-1,out _),"out-of-range ticks rejected");
        string game=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/ConversionGame.cs")),stamp=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeBuildStamp.cs"));
        Check(game.Contains("GuiFrame();if(error.Length==0)NoticeToast();BuildCorner();"),"drawn after every screen");
        Check(stamp.Contains("Updater?.Installed")&&stamp.Contains("NativeUpdateRules.IdentityFile")&&stamp.Contains("catch(Exception)"),"identity from the updater or the player folder, failure-safe");
        Console.WriteLine("PASS build label ("+count+" checks)");
    }
    // NativeUpdater.Short lives in Runtime (not compiled here); its rule is the first 12 characters.
    private static string NativeUpdater_Short(string version)=>string.IsNullOrEmpty(version)?"unknown":version.Substring(0,Math.Min(12,version.Length));
}
