using System;
// THE DEEP END (docs/EXPEDITION-BOSS-BACKROOMS.md section 4).
public static class DeepEndBossChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception(what);count++;}
    public static void Run(string root)
    {
        count=0;
        Check(ChipArena.UnityConversion.NativeDeepEndBoss.Archetype=="poolrooms-deep-end","the archetype Astra binds");
        Console.WriteLine("PASS deepend-boss ("+count+" checks)");
    }
}
