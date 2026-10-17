using System;
using ChipArena.UnityConversion;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length==1&&args[0]=="--expedition-fresh"){ExpeditionFreshRunChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--cyber-v2-authority"){CyberKit2AuthorityChecks.Run();CyberStepChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--expedition-areas"){ExpeditionAreaRegistryChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-director"){ExpeditionDirectorChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-integration"){ExpeditionIntegrationChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition"){ExpeditionChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--expedition-view"){ExpeditionChecks.Run();ExpeditionPresentationChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-layout"){ExpeditionLayoutChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-dressing"){ExpeditionDressingChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-hub"){ExpeditionHubChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-states"){ExpeditionStatesChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-acceptance"){ExpeditionAcceptanceChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-pick"){ExpeditionPickChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--chooser-camera"){var root1=ProjectRoot();ChooserCameraChecks.Run(root1,System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(root1,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));return 0;}
            if(args.Length==1&&args[0]=="--chip-select"){var chipData=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(ProjectRoot(),"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});foreach(var result in ChipSelectChecks.Run(chipData))Console.WriteLine("PASS "+result);return 0;}
            if(args.Length==1&&args[0]=="--chip-screen"){ChipScreenChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--dev-abuse"){DevAbuseChecks.Run();return 0;}
            DevAbuseChecks.Run();ExpeditionChecks.Run();ExpeditionFreshRunChecks.Run(ProjectRoot());ExpeditionAreaRegistryChecks.Run(ProjectRoot());
            if(args.Length==1&&args[0]=="--navigation"){NavSearchChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--wardzero-simulation"){var wardRoot=ProjectRoot();WardSimulationChecks.Run(wardRoot);HulkBreachChecks.Run(wardRoot);WardZeroChecks.Run(wardRoot);StageVentChecks.Run(wardRoot);HordeKindChecks.Run(wardRoot);ZombieMotionChecks.Run(wardRoot);HulkSpitterChecks.Run(wardRoot);HulkBreachFxChecks.Run(wardRoot);HulkMotionChecks.Run(wardRoot);return 0;}
            if(args.Length==1&&args[0]=="--cloudflare-smoke"){CloudflareChecks.Live(ProjectRoot()).GetAwaiter().GetResult();return 0;}
            if(args.Length==2&&args[0]=="--relay-smoke")
            {var json=System.IO.File.ReadAllText(System.IO.Path.Combine(ProjectRoot(),"Assets/Conversion/Resources/GameContent.json"));using(var doc=System.Text.Json.JsonDocument.Parse(json))RelayChecks.Run(args[1],doc.RootElement.GetProperty("sourceCommit").GetString()).GetAwaiter().GetResult();return 0;}
            if(args.Length==1&&args[0]=="--cyber-lord"){var cyberRoot=ProjectRoot();var cyberData=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(cyberRoot,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});KernelPanicChecks.Run();CyberChecks.Run();foreach(var result in CyberLordChecks.Run(cyberRoot,cyberData))Console.WriteLine("PASS "+result);foreach(var result in CyberLordRunChecks.Run(cyberRoot))Console.WriteLine("PASS "+result);foreach(var result in CyberLordRangedChecks.Run(cyberRoot))Console.WriteLine("PASS "+result);foreach(var result in CyberLordAuraChecks.Run(cyberRoot))Console.WriteLine("PASS "+result);foreach(var result in CyberKit2MeleeChecks.Run(cyberRoot))Console.WriteLine("PASS "+result);return 0;}
            if(args.Length==1&&args[0]=="--cyber-ranged"){CyberRangedSimulationChecks.Run();CyberChecks.Run();foreach(var result in CyberLordRangedChecks.Run(ProjectRoot()))Console.WriteLine("PASS "+result);return 0;}
            if(args.Length==1&&args[0]=="--cyber-names"){var namesRoot=ProjectRoot();CyberLordNameChecks.Run(namesRoot,System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(namesRoot,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));return 0;}
            if(args.Length==1&&args[0]=="--cyber-kit2-ranged"){foreach(var result in CyberLordRangedChecks.Run(ProjectRoot()))Console.WriteLine("PASS "+result);foreach(var result in CyberKit2RangedChecks.Run(ProjectRoot()))Console.WriteLine("PASS "+result);return 0;}
            if(args.Length==1&&args[0]=="--horde-drops"){HordeDropChecks.Run(ProjectRoot());HulkSpitterChecks.Run(ProjectRoot());ZombieFxChecks.Run(ProjectRoot());HulkMotionChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--hulk-motion"){HulkMotionChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--station-enemies"){StationEnemyChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());ZombieMotionChecks.Run(ProjectRoot());HulkSpitterChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--horde-roam"){HordeRoamChecks.Run(ProjectRoot());ZombieMotionChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--hulk-charge"){HulkMotionChecks.Run(ProjectRoot());HulkChargeChecks.Run(ProjectRoot());HulkHuntChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());ZombieMotionChecks.Run(ProjectRoot());HulkSpitterChecks.Run(ProjectRoot());ZombieFxChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--dev-tools"){DevToolsChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--hazard-kit"){HazardKitChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--foot-surfaces"){FootSurfaceChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--footfalls"){FootfallAudioChecks.Run(ProjectRoot());FootSurfaceChecks.Run(ProjectRoot());foreach(var result in EffectsAudioChecks.Run(ProjectRoot()))Console.WriteLine("PASS "+result);HordeRoamChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--ward-lighting"){WardLightingChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--machines"){MachineModelChecks.Run(ProjectRoot());MachinePlacementChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--transitions"){TransitionLoadingChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expeditions-menu"){ExpeditionsMenuChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--weapon-sockets"){var socketRoot=ProjectRoot();var socketData=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(socketRoot,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});WeaponSocketChecks.Run(socketRoot,socketData);HitFeelChecks.Run(socketRoot,socketData);return 0;}
            if(args.Length==1&&args[0]=="--chips"){MachineModelChecks.Run(ProjectRoot());ChipCartridgeChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--scenery"){SurfaceChecks.Scenery();return 0;}
            if(args.Length==1&&args[0]=="--surfaces"){SurfaceChecks.Run(ProjectRoot());return 0;}
            if(args.Length>=1&&args[0]=="--surface-audit"){if(args.Length>1&&args[1]=="differ")SurfaceChecks.Differ(ProjectRoot());else if(args.Length>1&&args[1]=="dupes")SurfaceChecks.Dupes(ProjectRoot());else SurfaceChecks.Audit(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--melee-bisect"){BisectChecks.Run(ProjectRoot());MeleeBisectChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--sprint-arms"){SprintArmChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--greatsword-sprint"){var gsRoot=ProjectRoot();GreatswordSprintChecks.Run(gsRoot,System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(gsRoot,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));return 0;}
            if(args.Length==1&&args[0]=="--bow"){BowChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--gib"){GibChecks.Run(ProjectRoot());BowChecks.Run(ProjectRoot());return 0;}
            CyberRangedSimulationChecks.Run();
            HitchChecks.Run();KernelPanicChecks.Run();OnlineRoomChecks.Run();
            CloudflareChecks.Run().GetAwaiter().GetResult();
            AnimationChecks.Run();
            string root=ProjectRoot();
            ExpeditionPresentationChecks.Run(root);ExpeditionIntegrationChecks.Run(root);ExpeditionDirectorChecks.Run(root);ExpeditionStatesChecks.Run(root);ExpeditionAcceptanceChecks.Run(root);ExpeditionPickChecks.Run(root);
            PveSpitterChecks.Run(root);NavSearchChecks.Run(root);WardSimulationChecks.Run(root);HulkBreachChecks.Run(root);InteractionChecks.Run(root);
            SirenDropChecks.Run(root);
            FactoryChecks.Run(root);StageHazardChecks.Run(root);SurfaceChecks.Run(root);
            var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};
            var data=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),options);
            FinalKillMatchChecks.Run(data);BowInputChecks.Run(root,data);
            MeleeKitChecks.Run(data);WhirlwindChecks.Run(data);RangedAbilityChecks.Run(data);NativeMeleeFeelChecks.Run(data);SprintAttackChecks.Run(data);NightMeleeChecks.Run(data);JumpAttackFollowupChecks.Run(data);JumpSimulationChecks.Run(data);
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
            foreach(var result in LoadoutGridChecks.Run(data))Console.WriteLine("PASS "+result);
            LockerChecks.Run(root,data);
            ClassIconChecks.Run(root,data);
            RooftopChecks.Run(root);
            PortraitBackChecks.Run(root);
            BuildLabelChecks.Run(root);
            FinalKillcamChecks.Run(root);ReplayShotChecks.Run(data);
            HatLookChecks.Run(root);
            NativeSystemsChecks.Run(root,data);WoundCarveChecks.Run(root);WoundSkinChecks.Run(root);BisectChecks.Run(root);MeleeBisectChecks.Run(root);GibChecks.Run(root);ReviveHoloChecks.Run(root);CollapseChecks.Run(root);ExtraPresentationChecks.Run(root);BlastChecks.Run(root);FieldChecks.Run(root);ShotChecks.Run(root,data);
            foreach(var result in FirstPersonSwayChecks.Run(root,data))Console.WriteLine("PASS "+result);
            StatusVisualChecks.Run(root);GoreBeaconChecks.Run(root);GoreHitChecks.Run();KillEffectChecks.Run(root);SirenFxChecks.Run(root);SirenMarkChecks.Run(root);SirenLookChecks.Run(root);DevFxChecks.Run(root);
            SirenSmileChecks.Run(root);SirenDetailChecks.Run(root);SirenSmileFaceChecks.Run(root);
            HudPlaceholderChecks.Run(root);HudCyberChecks.Run(root,data);
            DeathFxChecks.Run(root);CorpseChecks.Run(data);
            StageVentChecks.Run(root);MinionFxChecks.Run(root);MinionHintChecks.Run(root);
            HordeDropChecks.Run(root);HordeKindChecks.Run(root);StationEnemyChecks.Run(root);ZombieMotionChecks.Run(root);HordeRoamChecks.Run(root);HulkSpitterChecks.Run(root);HulkBreachFxChecks.Run(root);ZombieFxChecks.Run(root);HulkMotionChecks.Run(root);HulkChargeChecks.Run(root);HulkHuntChecks.Run(root);
            SummonCeilingPickupChecks.Run(root,data);
            CameraRigChecks.Run(root);
            ChooserCameraChecks.Run(root,data);ChipScreenChecks.Run(root);
            StructureFxChecks.Run(root);
            CyberCityChecks.Run(root);CutExitChecks.Run(root);WardZeroChecks.Run(root);ExpeditionLayoutChecks.Run(root);ExpeditionDressingChecks.Run(root);ExpeditionHubChecks.Run(root);HazardKitChecks.Run(root);MachineModelChecks.Run(root);MachinePlacementChecks.Run(root);TransitionLoadingChecks.Run(root);ExpeditionsMenuChecks.Run(root);ChipCartridgeChecks.Run(root);WardLightingChecks.Run(root);
            DevToolsChecks.Run(root);
            SpawnInChecks.Run(root,data);
            VfxChecks.Run(root);VfxRuntimeChecks.Run(root);
            LobbyPoseChecks.Run(root);FighterMotionChecks.Run(root);LeetMotionChecks.Run();LeetPathChecks.Run(data);GunMotionChecks.Run();AuditorHaloChecks.Run(root);HitReactionChecks.Run(root);PackReactionChecks.Run(root);HitFeelChecks.Run(root,data);JumpMotionChecks.Run(root);PackLocomotionChecks.Run(root);SprintMotionChecks.Run(root);SprintArmChecks.Run(root);GreatswordSprintChecks.Run(root,data);RangedLocomotionChecks.Run(root);AirAttackChecks.Run(root,data);DownAttackChecks.Run(data);BowRuleChecks.Run();PlungeStabChecks.Run(root,data);CyberChecks.Run();SprintAttackPoseChecks.Run(root,data);
            ArmorLightChecks.Run(root);
            BlockFxChecks.Run(root,data);
            MeleeKitPresentationChecks.Run(root,data);
            MagicFxChecks.Run(root);
            GunKitChecks.Run(root,data);
            BloomChecks.Run(root);
            WeaponFxChecks.Run(root,data);
            WeaponSkinChecks.Run(root,data);
            WeaponSocketChecks.Run(root,data);
            ChipModuleChecks.Run(root,data);
            BowChecks.Run(root);
            BodySkinChecks.Run(root,data);
            BrickfallSceneryChecks.Run(root);
            foreach(var result in EventFeedbackChecks.Run(root))Console.WriteLine("PASS "+result);
            HealNumberChecks.Run(root);
            foreach(var result in EffectsAudioChecks.Run(root))Console.WriteLine("PASS "+result);
            FootfallAudioChecks.Run(root);
            FootSurfaceChecks.Run(root);
            foreach(var result in IntroMusicChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in ImpactCueChecks.Run(root,data))Console.WriteLine("PASS "+result);
            foreach(var result in DraftPlantChecks.Run())Console.WriteLine("PASS "+result);
            foreach(var result in ChipSelectChecks.Run(data))Console.WriteLine("PASS "+result);
            foreach(var result in DraftStageChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in DraftHoverChecks.Run())Console.WriteLine("PASS "+result);
            DraftBackdropChecks.Run(root);
            foreach(var result in CinematicCameraChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in IntroPresentationChecks.Run())Console.WriteLine("PASS "+result);
            foreach(var result in SirenIntroChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in IntroMotionChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in EntranceDirectorChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in SheldonIntroChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in PackIntroChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in IntroHoldChecks.Run(root))Console.WriteLine("PASS "+result);
            CyberKit2AuthorityChecks.Run();CyberStepChecks.Run();
            foreach(var result in CyberLordChecks.Run(root,data))Console.WriteLine("PASS "+result);
            foreach(var result in CyberLordRunChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in CyberLordRangedChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in CyberLordAuraChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in CyberKit2RangedChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in CyberKit2MeleeChecks.Run(root))Console.WriteLine("PASS "+result);
            CyberLordNameChecks.Run(root,data);
            foreach(var result in SheldonIdleChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in BowFlipChecks.Run())Console.WriteLine("PASS "+result);
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
