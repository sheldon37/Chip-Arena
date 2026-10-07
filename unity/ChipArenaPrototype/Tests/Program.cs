using System;
using ChipArena.UnityPrototype;

internal static class Program
{
    private static int Main()
    {
        try
        {
            AnimationChecks.Run();
            var tests = RegressionTests.Run(); tests.AddRange(RangedRegressionTests.Run());
            foreach (string test in tests) Console.WriteLine("PASS " + test);
            Console.WriteLine(tests.Count + " managed-core checks passed. Unity Editor/rendering/PhysX and Windows build NOT exercised.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
