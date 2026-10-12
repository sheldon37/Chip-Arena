using System;
using ChipArena.UnityConversion;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length==1&&args[0]=="--cloudflare-smoke"){CloudflareChecks.Live(ProjectRoot()).GetAwaiter().GetResult();return 0;}
            if(args.Length==2&&args[0]=="--relay-smoke")
            {var json=System.IO.File.ReadAllText(System.IO.Path.Combine(ProjectRoot(),"Assets/Conversion/Resources/GameContent.json"));using(var doc=System.Text.Json.JsonDocument.Parse(json))RelayChecks.Run(args[1],doc.RootElement.GetProperty("sourceCommit").GetString()).GetAwaiter().GetResult();return 0;}
            OnlineRoomChecks.Run();
            CloudflareChecks.Run().GetAwaiter().GetResult();
            AnimationChecks.Run();
            string root=ProjectRoot();
            InteractionChecks.Run(root);
            SirenDropChecks.Run(root);
            var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
            var data=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),options);
            BrickfallChecks.Run(root,data);
            LocomotionChecks.Run(data);
            ParityLogicChecks.Run(data);ParityFollowupChecks.Run(data);
            var fixtures=System.Text.Json.JsonSerializer.Deserialize<ParityFixtures>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Tests/source-parity.json")),options);
            var presentation=System.Text.Json.JsonSerializer.Deserialize<PresentationFixtures>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Tests/presentation-parity.json")),options);
            var melee=System.Text.Json.JsonSerializer.Deserialize<MeleeFixtures>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Tests/melee-parity.json")),options);
            var specials=System.Text.Json.JsonSerializer.Deserialize<SpecialFixtures>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Tests/specials-parity.json")),options);
            foreach(var result in NativeSpecialChecks.Run(data,specials))Console.WriteLine("PASS "+result);
            foreach(var result in NativeMeleeChecks.Run(data,melee))Console.WriteLine("PASS "+result);
            foreach(var result in NativeMotionChecks.Run())Console.WriteLine("PASS "+result);
            foreach(var result in NativeVisualChecks.Run(data))Console.WriteLine("PASS "+result);
            foreach(var result in NativeDamageScreenChecks.Run())Console.WriteLine("PASS "+result);
            foreach(var result in NativePresentationChecks.Run(data,presentation))Console.WriteLine("PASS "+result);
            foreach(var result in NativeParityChecks.Run(data,fixtures))Console.WriteLine("PASS "+result);
            foreach(var result in MeleeMotionChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in MeleeFeelChecks.Run(root,data))Console.WriteLine("PASS "+result);
            foreach(var result in MagicMotionChecks.Run(root))Console.WriteLine("PASS "+result);
            WeaponPresentationChecks.Run(root,data);
            RigDressingChecks.Run(root,data);
            foreach(var result in UiMotionChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in LoadoutChecks.Run(data))Console.WriteLine("PASS "+result);
            LockerChecks.Run(root,data);
            PortraitBackChecks.Run(root);
            NativeSystemsChecks.Run(root,data);WoundCarveChecks.Run(root);CollapseChecks.Run(root);ExtraPresentationChecks.Run(root);BlastChecks.Run(root);FieldChecks.Run(root);ShotChecks.Run(root,data);
            foreach(var result in FirstPersonSwayChecks.Run(root,data))Console.WriteLine("PASS "+result);
            StatusVisualChecks.Run(root);GoreBeaconChecks.Run(root);GoreHitChecks.Run();KillEffectChecks.Run(root);SirenFxChecks.Run(root);SirenMarkChecks.Run(root);SirenLookChecks.Run(root);DevFxChecks.Run(root);
            SirenSmileChecks.Run(root);SirenDetailChecks.Run(root);SirenSmileFaceChecks.Run(root);
            HudPlaceholderChecks.Run(root);
            DeathFxChecks.Run(root);
            StageVentChecks.Run(root);MinionFxChecks.Run(root);MinionHintChecks.Run(root);
            SummonCeilingPickupChecks.Run(root,data);
            StructureFxChecks.Run(root);
            VfxChecks.Run(root);VfxRuntimeChecks.Run(root);
            LobbyPoseChecks.Run(root);FighterMotionChecks.Run(root);LeetMotionChecks.Run();LeetPathChecks.Run(data);GunMotionChecks.Run();AuditorHaloChecks.Run(root);HitReactionChecks.Run(root);PackReactionChecks.Run(root);
            ArmorLightChecks.Run(root);
            BlockFxChecks.Run(root,data);
            BloomChecks.Run(root);
            WeaponFxChecks.Run(root,data);
            BrickfallSceneryChecks.Run(root);
            foreach(var result in EventFeedbackChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in EffectsAudioChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in IntroMusicChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in ImpactCueChecks.Run(root,data))Console.WriteLine("PASS "+result);
            foreach(var result in DraftPlantChecks.Run())Console.WriteLine("PASS "+result);
            foreach(var result in DraftStageChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in DraftHoverChecks.Run())Console.WriteLine("PASS "+result);
            foreach(var result in CinematicCameraChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in IntroPresentationChecks.Run())Console.WriteLine("PASS "+result);
            foreach(var result in SirenIntroChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in IntroMotionChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in EntranceDirectorChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in PackIntroChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in MapLookChecks.Run(root,data))Console.WriteLine("PASS "+result);
            foreach(var result in BlockLookChecks.Run(root,data))Console.WriteLine("PASS "+result);
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
