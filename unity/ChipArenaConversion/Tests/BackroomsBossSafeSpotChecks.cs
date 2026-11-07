using System;
// No safe place (docs/EXPEDITION-BOSS-BACKROOMS.md section 1): every spot in each arena is threatened within its limit, by the move tables' shapes. Each boss adds its own sweep here.
public static class BackroomsBossSafeSpotChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception(what);count++;}
    public static void Run(string root)
    {
        count=0;BackroomsBossRuleChecks.Run();
        count+=HumBossSweep.Run(root)+DeepEndBossSweep.Run(root);
        Console.WriteLine("PASS backrooms-boss-sweep ("+count+" checks)");
    }
}
