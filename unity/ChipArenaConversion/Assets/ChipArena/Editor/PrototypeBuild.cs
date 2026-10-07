using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion.Editor
{
    public static class PrototypeBuild
    {
        public const string ScenePath = "Assets/ChipArena/Scenes/Prototype.unity";
        public const string IconPath = "Assets/ChipArena/Branding/CAVoxelIcon.png";
        [MenuItem("Chip Arena/Open test arena")]
        public static void OpenArena() { Prepare(); EditorSceneManager.OpenScene(ScenePath); }

        [MenuItem("Chip Arena/Run core and project checks")]
        public static void Validate()
        {
            Prepare();
            NativeBuildChecks.Run();
            foreach(string name in RegressionTests.Run()) Debug.Log("PASS " + name);
            foreach(string name in RangedRegressionTests.Run()) Debug.Log("PASS " + name);
            using(var animations=new CapturedAnimation()) animations.ValidatePlayback();
            foreach(string name in new[] { "pistol", "rifle", "machinegun", "reload-start", "reload-end", "hit" })
                if(Resources.Load<AudioClip>("GunAudio/"+name)==null) throw new Exception("Gun audio failed to import: "+name);
            foreach(string name in new[] { "PrototypeLit", "PrototypeParticles", "NativeLit", "NativeGlow" })
            {
                Shader shader=Resources.Load<Shader>(name);
                if(shader==null||ShaderUtil.ShaderHasError(shader)) throw new Exception("Missing or invalid shader: "+name);
            }
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null) throw new Exception("Test scene missing");
            Debug.Log("Core and asset-import checks passed. Interactive visuals, PhysX and two-window gameplay still require playtesting.");
        }

        private static void Prepare()
        {
            PlayerSettings.companyName="ChipArenaConversion";
            PlayerSettings.productName="Chip Arena Native Conversion TEST";
            PlayerSettings.bundleVersion="0.4.1-native-melee-specials";
            PlayerSettings.defaultScreenWidth=1600; PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=true;
            ConfigureIcon();
            ConfigureRendering();
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");
            if(input!=null&&input.intValue!=0) { input.intValue=0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            EditorBuildSettings.scenes=new[] { new EditorBuildSettingsScene(ScenePath,true) };
            AssetDatabase.SaveAssets();
        }

        // Runtime-created actor/bolt materials are invisible to scene-based variant stripping.
        internal static void ConfigureRendering()
        {
            var graphics=new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var stripping=graphics.FindProperty("m_InstancingStripping");
            if(stripping==null) throw new BuildFailedException("Cannot configure instancing shader retention in this Unity Editor.");
            stripping.intValue=2; // Unity's InstancingStrippingMode.KeepAll (0=StripUnused, 1=StripAll).
            graphics.ApplyModifiedPropertiesWithoutUndo();
            graphics.Update();
            if(graphics.FindProperty("m_InstancingStripping").intValue!=2)
                throw new BuildFailedException("Instancing Variants must be Keep All for runtime-created characters and projectiles.");
        }

        private static void ConfigureIcon()
        {
            Texture2D icon=AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if(icon==null||icon.width!=icon.height||icon.width<256)
                throw new Exception("The square voxel CA launcher icon is missing or failed to import: "+IconPath);
            foreach(var target in new[] { NamedBuildTarget.Unknown, NamedBuildTarget.Standalone })
            {
                int[] sizes=PlayerSettings.GetIconSizes(target,IconKind.Application);
                if(sizes.Length==0)
                {
                    if(target.Equals(NamedBuildTarget.Standalone))
                        throw new Exception("Unity reported no standalone application icon slots. Check the Editor installation.");
                    continue;
                }
                var icons=new Texture2D[sizes.Length];
                for(int i=0;i<icons.Length;i++) icons[i]=icon;
                PlayerSettings.SetIcons(target,icons,IconKind.Application);
                var assigned=PlayerSettings.GetIcons(target,IconKind.Application);
                if(assigned.Length!=icons.Length) throw new Exception("Unity did not save all launcher icon slots.");
                foreach(var assignedIcon in assigned)
                    if(assignedIcon!=icon) throw new Exception("Unity did not save the voxel CA launcher icon.");
            }
        }

        [MenuItem("Chip Arena/Build Windows TEST ZIP")]
        public static void BuildWindows()
        {
            Validate();
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))
                throw new Exception("Install Windows Build Support for this Unity Editor version via Unity Hub.");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string folder=Path.Combine(root,"Builds","Windows"); Directory.CreateDirectory(folder);
            string exe=Path.Combine(folder,"ChipArena-Native-Conversion-TEST.exe");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[] { ScenePath }, locationPathName=exe,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
            if(report.summary.result!=BuildResult.Succeeded||!File.Exists(exe)) throw new Exception("Unity Windows build failed: "+report.summary.result);
            if(!File.Exists(Path.Combine(folder,"UnityPlayer.dll"))||!Directory.Exists(Path.Combine(folder,"ChipArena-Native-Conversion-TEST_Data")))
                throw new Exception("The Windows player is incomplete: UnityPlayer.dll or its game data folder is missing.");
            File.WriteAllText(Path.Combine(folder,"READ-ME.txt"),
                "Chip Arena Native Conversion - MELEE AND RIPOSTE TEST\r\n\r\nRun ChipArena-Native-Conversion-TEST.exe. Keep the entire folder together.\r\n"+
                "The EXE uses the voxel CA icon. Create a Windows shortcut to the EXE for a desktop launcher; do not move the EXE alone.\r\n"+
                "WASD move, double-tap W sprint, mouse look, LMB fire/attack, Q aim/magic ability, E ability, V first/third-person, R reload, RMB block, Shift dodge, Space jump.\r\n"+
                "Choose weapon and map before starting. Tab scoreboard. Esc menu. This native foundation is local solo vs bots.\r\n"+
                "15 source map collision layouts and native round/stat-Chip flow. Full conversion is IN PROGRESS.\r\n"+
                "Read CONVERSION-STATUS.md: special Chip mechanics, DEV styles, PvE/Siren, online play and remaining presentation parity are pending.\r\n"+
                "Built with Unity "+Application.unityVersion+". Core checks and player build passed on the build machine; human gameplay acceptance is separate.\r\n");
            string zip=Path.Combine(root,"Builds","Chip-Arena-Native-Conversion-TEST-Windows.zip");
            if(File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(folder,zip,System.IO.Compression.CompressionLevel.Optimal,false);
            Debug.Log("UNITY_TEST_BUILD_READY: "+zip);
        }
    }

    // Protect builds started from the standard Build Profiles window as well as our build menu.
    public sealed class PrototypeRenderingBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            PrototypeBuild.ConfigureRendering();
            AssetDatabase.SaveAssets();
            Debug.Log("UNITY_INSTANCING_VARIANTS: Keep All (runtime actor/projectile rendering)");
        }
    }
}
