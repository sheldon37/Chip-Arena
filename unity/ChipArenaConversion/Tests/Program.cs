using System;
using ChipArena.UnityConversion;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            CrowdPoseChecks.Run();
            if(args.Length==1&&args[0]=="--new-stages"){NewStagesChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--crowd-pose")return 0;
            SnapshotChecks.Run();
            TransportBacklogChecks.Run();
            if(args.Length==1&&args[0]=="--transport-backlog")return 0;
            if(args.Length==1&&args[0]=="--chip-perf")return 0;
            if(args.Length==1&&args[0]=="--killcam"){FinalKillcamChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--movement-mods"){MovementModsChecks.Run(ProjectRoot());RoundsCardsChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zone-boss-rules"){ZoneBossRulesChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--zone-combat"){ZoneCombatChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-deployment"){ExpeditionDeploymentChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--deploy-presentation"){DeployPresentationChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--expedition-split"){ExpeditionSplitChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--expedition-fresh"){ExpeditionFreshRunChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-area-wire"){ExpeditionAreaWireChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--expedition-joins"){ExpeditionJoinChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-cohorts"){ExpeditionCohortChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--cyber-v2-authority"){CyberKit2AuthorityChecks.Run();CyberStepChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--expedition-areas"){ExpeditionAreaRegistryChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-director"){ExpeditionDirectorChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-integration"){ExpeditionIntegrationChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition"){ExpeditionChecks.Run();return 0;}
            if(args.Length==1&&args[0]=="--expedition-view"){ExpeditionChecks.Run();ExpeditionPresentationChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-layout"){ExpeditionLayoutChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-dressing"){ExpeditionDressingChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-hub"){ExpeditionHubChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-city"){ExpeditionCityChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--map-style"){MapStyleChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--menu-style"){MenuStyleChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--visual-rig"){VisualRigChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--model-poses"){ModelPoseChecks.Run(ProjectRoot());LobbyPoseChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--pack-bank"){PackBankChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--body-reveal"){BodyRevealChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--pack-weapons"){PackWeaponChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--pack-fx-hits"){PackFxHitsChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--pack-fx-blasts"){PackFxBlastsChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--stage-kits"){StageKitChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--skin-tone"){SkinToneChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zombie-models"){ZombieModelChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zombie-posture"){ZombiePostureChecks.Run(ProjectRoot());ZombieGaitChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zombie-gait"){ZombieGaitChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--run-arm"){RunArmChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--faces"){FaceChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--hats"){ModelHatChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--suit"){RobotSuitChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--helm"){RobotHelmChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--body-chip-model"){var bcRoot=ProjectRoot();BodyChipModelChecks.Run(bcRoot,System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(bcRoot,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));return 0;}
            if(args.Length==1&&args[0]=="--advanced-lighting"){var alRoot=ProjectRoot();var alData=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(alRoot,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});AdvancedLightingChecks.Run(alRoot);foreach(var result in MapLookChecks.Run(alRoot,alData))Console.WriteLine("PASS "+result);foreach(var result in BlockLookChecks.Run(alRoot,alData))Console.WriteLine("PASS "+result);MapStyleChecks.Run(alRoot);WardLightingChecks.Run(alRoot);ZombieLightChecks.Run(alRoot);ArmorLightChecks.Run(alRoot);BloomChecks.Run(alRoot);FactoryChecks.Run(alRoot);BrickfallSceneryChecks.Run(alRoot);CyberCityChecks.Run(alRoot);StageKitChecks.Run(alRoot);return 0;}
            if(args.Length==1&&args[0]=="--expedition-join-view"){ExpeditionJoinPresentationChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--station-visuals"){ExpeditionStationVisualChecks.Run(ProjectRoot());ZombieLightChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zombie-light"){ZombieLightChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-states"){ExpeditionStatesChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--laser-door"){ExpeditionLaserDoorChecks.Run(ProjectRoot());ExpeditionStatesChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--quota-marks"){QuotaMarkChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-ending"){ExpeditionEndingChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-acceptance"){ExpeditionAcceptanceChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expedition-pick"){ExpeditionPickChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zone-music"){ZoneMusicChecks.Run(ProjectRoot());ZoneBossMusicChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zone-boss-music"){ZoneBossMusicChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--chooser-camera"){var root1=ProjectRoot();ChooserCameraChecks.Run(root1,System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(root1,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));return 0;}
            if(args.Length==1&&args[0]=="--chip-select"){var chipData=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(ProjectRoot(),"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});foreach(var result in ChipSelectChecks.Run(chipData))Console.WriteLine("PASS "+result);return 0;}
            if(args.Length==1&&args[0]=="--chip-screen"){ChipScreenChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--draft-stage"){foreach(var result in DraftPlantChecks.Run())Console.WriteLine("PASS "+result);foreach(var result in DraftStageChecks.Run(ProjectRoot()))Console.WriteLine("PASS "+result);DraftBackdropChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--dev-abuse"){DevAbuseChecks.Run();return 0;}
            ZoneBossRulesChecks.Run();ZoneCombatChecks.Run(ProjectRoot());DevAbuseChecks.Run();ExpeditionDeploymentChecks.Run();DeployPresentationChecks.Run();ExpeditionChecks.Run();ExpeditionSplitChecks.Run();ExpeditionAreaRegistryChecks.Run(ProjectRoot());ExpeditionCohortChecks.Run();ExpeditionJoinChecks.Run(ProjectRoot());ExpeditionFreshRunChecks.Run(ProjectRoot());ExpeditionAreaWireChecks.Run();
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
            if(args.Length==1&&args[0]=="--ironworks-kinds"){IronworksKindChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--ironworks-props"){IronworksPropChecks.Run(ProjectRoot());IronworksKindChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--station-boss"){StationBossModelChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--station-boss-presentation"){StationBossPresentationChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--boss-motion"){BossMotionChecks.Run(ProjectRoot());return 0;}
            HordeHitChecks.Run();
            if(args.Length==1&&args[0]=="--horde-hit")return 0;
            if(args.Length==1&&args[0]=="--zombie-gore"){ZombieGoreChecks.Run(ProjectRoot());MinionFxChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zombie-twitch"){ZombieTwitchChecks.Run(ProjectRoot());ZombieLocoChecks.Run(ProjectRoot());ZombieFlowChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zombie-loco"){ZombieLocoChecks.Run(ProjectRoot());ZombieTwitchChecks.Run(ProjectRoot());ZombieMotionChecks.Run(ProjectRoot());ZombieFlowChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--zombie-flow"){ZombieFlowChecks.Run(ProjectRoot());ZombieTwitchChecks.Run(ProjectRoot());ZombieMotionChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--station-enemies"){StationEnemyChecks.Run(ProjectRoot());IronworksKindChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());ZombieMotionChecks.Run(ProjectRoot());HulkSpitterChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--horde-roam"){HordeRoamChecks.Run(ProjectRoot());ZombieMotionChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--hulk-charge"){HulkMotionChecks.Run(ProjectRoot());HulkChargeChecks.Run(ProjectRoot());HulkHuntChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());ZombieMotionChecks.Run(ProjectRoot());HulkSpitterChecks.Run(ProjectRoot());ZombieFxChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--dev-tools"){DevToolsChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--hazard-kit"){HazardKitChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--ironworks-visuals"){IronworksVisualChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--ironworks-boss"){IronworksBossModelChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--ironworks-boss-presentation"){IronworksBossPresentationChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--boss-vfx"){BossVfxRealismChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--foot-surfaces"){FootSurfaceChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--footfalls"){FootfallAudioChecks.Run(ProjectRoot());FootSurfaceChecks.Run(ProjectRoot());foreach(var result in EffectsAudioChecks.Run(ProjectRoot()))Console.WriteLine("PASS "+result);HordeRoamChecks.Run(ProjectRoot());HordeKindChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--ward-lighting"){WardLightingChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--machines"){MachineModelChecks.Run(ProjectRoot());MachinePlacementChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--transitions"){TransitionLoadingChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--party-loading"){PartyLoadingChecks.Run(ProjectRoot());TransitionLoadingChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--account-xp"){AccountXpChecks.Run(ProjectRoot());AccountXpProgressionChecks.RunChecks(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--move-visuals"){MoveVisualChecks.Run(ProjectRoot());JumpMotionChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--rounds-visuals"){RoundsVisualChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--expeditions-menu"){ExpeditionsMenuChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--weapon-sockets"){var socketRoot=ProjectRoot();var socketData=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(socketRoot,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});WeaponSocketChecks.Run(socketRoot,socketData);HitFeelChecks.Run(socketRoot,socketData);return 0;}
            if(args.Length==1&&args[0]=="--muzzle-flash"){var mfRoot=ProjectRoot();var mfData=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(mfRoot,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});MuzzleFlashChecks.Run(mfRoot);WeaponFxChecks.Run(mfRoot,mfData);GunKitChecks.Run(mfRoot,mfData);ChipModuleChecks.Run(mfRoot,mfData);return 0;}
            if(args.Length==1&&args[0]=="--chips"){MachineModelChecks.Run(ProjectRoot());ChipCartridgeChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--scenery"){SurfaceChecks.Scenery();return 0;}
            if(args.Length==1&&args[0]=="--surfaces"){SurfaceChecks.Run(ProjectRoot());return 0;}
            if(args.Length>=1&&args[0]=="--surface-audit"){if(args.Length>1&&args[1]=="differ")SurfaceChecks.Differ(ProjectRoot());else if(args.Length>1&&args[1]=="dupes")SurfaceChecks.Dupes(ProjectRoot());else SurfaceChecks.Audit(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--melee-bisect"){BisectChecks.Run(ProjectRoot());MeleeBisectChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--sprint-arms"){SprintArmChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--sprint"){SprintMotionChecks.Run(ProjectRoot());SprintArmChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--greatsword-sprint"){var gsRoot=ProjectRoot();GreatswordSprintChecks.Run(gsRoot,System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(System.IO.File.ReadAllText(System.IO.Path.Combine(gsRoot,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));return 0;}
            if(args.Length==1&&args[0]=="--bow"){BowChecks.Run(ProjectRoot());return 0;}
            if(args.Length==1&&args[0]=="--gib"){GibChecks.Run(ProjectRoot());BowChecks.Run(ProjectRoot());return 0;}
            CyberRangedSimulationChecks.Run();
            HitchChecks.Run();KernelPanicChecks.Run();OnlineRoomChecks.Run();
            CloudflareChecks.Run().GetAwaiter().GetResult();
            AnimationChecks.Run();
            string root=ProjectRoot();
            ExpeditionPresentationChecks.Run(root);ExpeditionIntegrationChecks.Run(root);ExpeditionDirectorChecks.Run(root);ExpeditionStatesChecks.Run(root);ExpeditionLaserDoorChecks.Run(root);QuotaMarkChecks.Run(root);ExpeditionEndingChecks.Run(root);ExpeditionAcceptanceChecks.Run(root);ExpeditionPickChecks.Run(root);
            PveSpitterChecks.Run(root);NavSearchChecks.Run(root);WardSimulationChecks.Run(root);HulkBreachChecks.Run(root);InteractionChecks.Run(root);
            SirenDropChecks.Run(root);
            ExpeditionStationVisualChecks.Run(root);ZombieLightChecks.Run(root);
            NewStagesChecks.Run(root);FactoryChecks.Run(root);StageHazardChecks.Run(root);SurfaceChecks.Run(root);
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
            RigDressingChecks.Run(root,data);BodyChipModelChecks.Run(root,data);
            foreach(var result in UiMotionChecks.Run(root))Console.WriteLine("PASS "+result);
            foreach(var result in LoadoutChecks.Run(data))Console.WriteLine("PASS "+result);
            foreach(var result in LoadoutGridChecks.Run(data))Console.WriteLine("PASS "+result);
            LockerChecks.Run(root,data);
            ClassIconChecks.Run(root,data);
            RooftopChecks.Run(root);
            PortraitBackChecks.Run(root);
            BuildLabelChecks.Run(root);
            FinalKillcamChecks.Run(root);ReplayShotChecks.Run(data);
            HatLookChecks.Run(root);ModelHatChecks.Run(root);RobotSuitChecks.Run(root);RobotHelmChecks.Run(root);
            LookContractChecks.Run(root);SkinToneChecks.Run(root);ZombieModelChecks.Run(root);ZombiePostureChecks.Run(root);ZombieGaitChecks.Run(root);RunArmChecks.Run(root);FaceChecks.Run(root);
            MovementModsChecks.Run(root);RoundsCardsChecks.Run(root);NativeSystemsChecks.Run(root,data);WoundCarveChecks.Run(root);WoundSkinChecks.Run(root);BisectChecks.Run(root);MeleeBisectChecks.Run(root);GibChecks.Run(root);ReviveHoloChecks.Run(root);CollapseChecks.Run(root);ExtraPresentationChecks.Run(root);BlastChecks.Run(root);FieldChecks.Run(root);ShotChecks.Run(root,data);
            foreach(var result in FirstPersonSwayChecks.Run(root,data))Console.WriteLine("PASS "+result);
            StatusVisualChecks.Run(root);GoreBeaconChecks.Run(root);GoreHitChecks.Run();KillEffectChecks.Run(root);SirenFxChecks.Run(root);SirenMarkChecks.Run(root);SirenLookChecks.Run(root);DevFxChecks.Run(root);
            SirenSmileChecks.Run(root);SirenDetailChecks.Run(root);SirenSmileFaceChecks.Run(root);
            HudPlaceholderChecks.Run(root);HudCyberChecks.Run(root,data);
            DeathFxChecks.Run(root);CorpseChecks.Run(data);
            StageVentChecks.Run(root);MinionFxChecks.Run(root);MinionHintChecks.Run(root);
            HordeDropChecks.Run(root);HordeKindChecks.Run(root);IronworksKindChecks.Run(root);IronworksPropChecks.Run(root);StationEnemyChecks.Run(root);StationBossModelChecks.Run(root);StationBossPresentationChecks.Run(root);ZombieMotionChecks.Run(root);ZombieFlowChecks.Run(root);ZombieTwitchChecks.Run(root);ZombieLocoChecks.Run(root);ZombieGoreChecks.Run(root);HordeRoamChecks.Run(root);HulkSpitterChecks.Run(root);HulkBreachFxChecks.Run(root);ZombieFxChecks.Run(root);HulkMotionChecks.Run(root);HulkChargeChecks.Run(root);HulkHuntChecks.Run(root);
            SummonCeilingPickupChecks.Run(root,data);
            CameraRigChecks.Run(root);
            ChooserCameraChecks.Run(root,data);ChipScreenChecks.Run(root);
            StructureFxChecks.Run(root);
            IronworksVisualChecks.Run(root);IronworksBossModelChecks.Run(root);IronworksBossPresentationChecks.Run(root);BossVfxRealismChecks.Run(root);
            CyberCityChecks.Run(root);CutExitChecks.Run(root);WardZeroChecks.Run(root);ExpeditionLayoutChecks.Run(root);ExpeditionDressingChecks.Run(root);ExpeditionHubChecks.Run(root);ExpeditionCityChecks.Run(root);ExpeditionJoinPresentationChecks.Run(root);HazardKitChecks.Run(root);MachineModelChecks.Run(root);MachinePlacementChecks.Run(root);TransitionLoadingChecks.Run(root);ExpeditionsMenuChecks.Run(root);ChipCartridgeChecks.Run(root);WardLightingChecks.Run(root);
            PartyLoadingChecks.Run(root);
            AccountXpChecks.Run(root);AccountXpProgressionChecks.RunChecks(root);
            MoveVisualChecks.Run(root);
            RoundsVisualChecks.Run(root);
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
            MapStyleChecks.Run(root);
            MenuStyleChecks.Run(root);
            WeaponFxChecks.Run(root,data);
            MuzzleFlashChecks.Run(root);
            WeaponSkinChecks.Run(root,data);
            WeaponSocketChecks.Run(root,data);
            ChipModuleChecks.Run(root,data);
            BowChecks.Run(root);
            BodySkinChecks.Run(root,data);
            VisualRigChecks.Run(root);ModelPoseChecks.Run(root);
            PackBankChecks.Run(root);
            BodyRevealChecks.Run(root);
            PackWeaponChecks.Run(root);
            PackFxHitsChecks.Run(root);
            PackFxBlastsChecks.Run(root);
            StageKitChecks.Run(root);
            AdvancedLightingChecks.Run(root);
            BrickfallSceneryChecks.Run(root);
            foreach(var result in EventFeedbackChecks.Run(root))Console.WriteLine("PASS "+result);
            HealNumberChecks.Run(root);
            foreach(var result in EffectsAudioChecks.Run(root))Console.WriteLine("PASS "+result);
            ZoneMusicChecks.Run(root);
            ZoneBossMusicChecks.Run(root);
            BossMotionChecks.Run(root);
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
