using System;
using ChipArena.UnityConversion;

internal static class Program
{
    private static int Main()
    {
        try
        {
            AnimationChecks.Run();
            string root=ProjectRoot();
            var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
            var data=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),options);
            ParityLogicChecks.Run(data);ParityFollowupChecks.Run(data);
            var fixtures=System.Text.Json.JsonSerializer.Deserialize<ParityFixtures>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Tests/source-parity.json")),options);
            var presentation=System.Text.Json.JsonSerializer.Deserialize<PresentationFixtures>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Tests/presentation-parity.json")),options);
            var melee=System.Text.Json.JsonSerializer.Deserialize<MeleeFixtures>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Tests/melee-parity.json")),options);
            var specials=System.Text.Json.JsonSerializer.Deserialize<SpecialFixtures>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Tests/specials-parity.json")),options);
            foreach(var result in NativeSpecialChecks.Run(data,specials))Console.WriteLine("PASS "+result);
            foreach(var result in NativeMeleeChecks.Run(data,melee))Console.WriteLine("PASS "+result);
            foreach(var result in NativeMotionChecks.Run())Console.WriteLine("PASS "+result);
            foreach(var result in NativeVisualChecks.Run(data))Console.WriteLine("PASS "+result);
            foreach(var result in NativePresentationChecks.Run(data,presentation))Console.WriteLine("PASS "+result);
            foreach(var result in NativeParityChecks.Run(data,fixtures))Console.WriteLine("PASS "+result);
            foreach(var result in MeleeMotionChecks.Run(root))Console.WriteLine("PASS "+result);
            WeaponPresentationChecks.Run(root,data);
            foreach(var result in UiMotionChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in LoadoutChecks.Run(data))Console.WriteLine("PASS "+result);
            NativeSystemsChecks.Run(root,data);WoundCarveChecks.Run(root);CollapseChecks.Run(root);ExtraPresentationChecks.Run(root);BlastChecks.Run(root);FieldChecks.Run(root);
            foreach(var result in FirstPersonSwayChecks.Run(root,data))Console.WriteLine("PASS "+result);
            StatusVisualChecks.Run(root);GoreBeaconChecks.Run(root);GoreHitChecks.Run();
            var tests = RegressionTests.Run(); tests.AddRange(RangedRegressionTests.Run());
            foreach (string test in tests) Console.WriteLine("PASS " + test);
            Console.WriteLine(tests.Count + " managed-core checks passed. Unity Editor/rendering/PhysX and Windows build NOT exercised.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static string ProjectRoot()
    {
        foreach(string start in new[]{System.IO.Directory.GetCurrentDirectory(),AppContext.BaseDirectory})
            for(var dir=new System.IO.DirectoryInfo(start);dir!=null;dir=dir.Parent)
                if(System.IO.File.Exists(System.IO.Path.Combine(dir.FullName,"Assets/Conversion/Resources/GameContent.json")))return dir.FullName;
        throw new System.IO.DirectoryNotFoundException("Run from the conversion project, or use dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj");
    }
}
