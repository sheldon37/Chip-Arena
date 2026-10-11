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
            PlayerSettings.bundleVersion="0.5.0-native-updater";
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
            PlayerSettings.colorSpace=ColorSpace.Linear;
            if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new BuildFailedException("Native builds require Linear colour space.");
            Debug.Log("NATIVE_COLOR_SPACE: Linear");
            var graphics=new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var stripping=graphics.FindProperty("m_InstancingStripping");
            if(stripping==null) throw new BuildFailedException("Cannot configure instancing shader retention in this Unity Editor.");
            stripping.intValue=2; // Unity's InstancingStrippingMode.KeepAll (0=StripUnused, 1=StripAll).
            graphics.ApplyModifiedPropertiesWithoutUndo();
            graphics.Update();
            if(graphics.FindProperty("m_InstancingStripping").intValue!=2)
                throw new BuildFailedException("Instancing Variants must be Keep All for runtime-created characters and projectiles.");
            // Review #82: NativeLighting turns linear fog on at runtime and no scene in the build uses fog, so Automatic fog
            // stripping would drop every FOG_LINEAR variant and the player would render without the Editor's fog. Keep
            // linear fog (Custom mode) for the lit, transparent, blast, status and field shaders.
            var fog=graphics.FindProperty("m_FogStripping");var linear=graphics.FindProperty("m_FogKeepLinear");
            if(fog==null||linear==null) throw new BuildFailedException("Cannot configure fog shader retention in this Unity Editor.");
            fog.intValue=1;linear.boolValue=true; // 0=Automatic, 1=Custom.
            graphics.ApplyModifiedPropertiesWithoutUndo();
            graphics.Update();
            if(graphics.FindProperty("m_FogStripping").intValue!=1||!graphics.FindProperty("m_FogKeepLinear").boolValue)
                throw new BuildFailedException("Fog Modes must be Custom with Linear kept: the game enables linear fog at runtime.");
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
            string folder=NativeUpdateBuild.PlayerFolder(root);
            string exe=Path.Combine(folder,"ChipArena-Native-Conversion-TEST.exe");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[] { ScenePath }, locationPathName=exe,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
            if(report.summary.result!=BuildResult.Succeeded||!File.Exists(exe)) throw new Exception("Unity Windows build failed: "+report.summary.result);
            PackageWindows(root,folder);
        }

        // Shared by the local builder and the already-exported Build Automation player.
        internal static NativeBuildIdentity PackageWindows(string root,string folder)
        {
            if(!File.Exists(Path.Combine(folder,"UnityPlayer.dll"))||!Directory.Exists(Path.Combine(folder,"ChipArena-Native-Conversion-TEST_Data")))
                throw new Exception("The Windows player is incomplete: UnityPlayer.dll or its game data folder is missing.");
            var identity=NativeUpdateBuild.Package(root,folder);string install=Path.GetDirectoryName(folder);
            File.WriteAllText(Path.Combine(install,"READ-ME.txt"),
                "CHIP ARENA\r\n\r\nOpen Chip Arena.exe to play.\r\n"+
                "Keep this whole Chip Arena folder together, including the app folder. You can move it or make a shortcut to Chip Arena.exe.\r\n"+
                "Use Update Game from the main menu for future updates. Your settings and account stay saved.\r\n\r\n"+
                "WASD move, double-tap W sprint, mouse look, LMB attack, Q aim/ability, E ability, V camera, R reload, RMB block, Shift dodge, Space jump. Esc opens the menu.\r\n");
            string zip=Path.Combine(root,"Builds","Chip-Arena-Native-Conversion-TEST-Windows.zip");
            NativeUpdateBuild.SeedZip(install,zip);
            File.Copy(zip,Path.Combine(root,"Builds",NativeUpdateRules.PortableZip),true);
            Debug.Log("UNITY_TEST_BUILD_READY: "+zip);
            return identity;
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
