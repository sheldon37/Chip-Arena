using System;
// THE HUM (docs/EXPEDITION-BOSS-BACKROOMS.md section 3).
public static class HumBossChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception(what);count++;}
    public static void Run(string root)
    {
        count=0;
        Check(ChipArena.UnityConversion.NativeHumBoss.Archetype=="level0-hum","the archetype Astra binds");
        Console.WriteLine("PASS hum-boss ("+count+" checks)");
    }
}
