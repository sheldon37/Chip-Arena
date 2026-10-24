using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ChipArena.UnityConversion.Editor
{
    public static class NativeCloudBuild
    {
        private static bool prepared;
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        private static string ReceiptPath => Path.Combine(Root,"Builds","cloud-build-receipt.json");

        // Configure these two methods in Build Automation's Advanced settings.
        public static void PreExport()
        {
            prepared=false;
            if(File.Exists(ReceiptPath)) File.Delete(ReceiptPath);
            Context();
            if(Application.platform!=RuntimePlatform.WindowsEditor||EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64)
                throw new BuildFailedException("Native cloud builds require a Windows builder and Windows x64 target.");
            if(Application.unityVersion!="6000.3.24f1") throw new BuildFailedException("Use the project's pinned Unity 6000.3.24f1 Editor.");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            EditorUserBuildSettings.development=true;
            PrototypeBuild.Validate();
            prepared=true;
        }

        public static void PostExport(string exportPath)
        {
            if(!prepared) throw new BuildFailedException("NativeCloudBuild.PreExport must pass before packaging.");
            var receipt=Context();
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64||
               PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)!=ScriptingImplementation.Mono2x||PlayerSettings.colorSpace!=ColorSpace.Linear)
                throw new BuildFailedException("Cloud target must remain Windows x64 / Mono / Linear colour space.");
            // UBA builds once. Adapt its exported player to the stable updater's naming/layout.
            string player=NativeUpdateBuild.PlayerFolder(Root);
            NativeCloudPlayer.Stage(exportPath,player);
            receipt.build=PrototypeBuild.PackageWindows(Root,player);
            receipt.unityVersion=Application.unityVersion;receipt.colorSpace=PlayerSettings.colorSpace.ToString();
            receipt.editorLog=Application.consoleLogPath;
            File.WriteAllText(ReceiptPath,JsonUtility.ToJson(receipt,true));
            Debug.Log("NATIVE_CLOUD_EXPORT_READY: "+receipt.sourceCommit+" / "+receipt.buildNumber);
        }

        private static Receipt Context()
        {
            var value=new Receipt {sourceCommit=Environment.GetEnvironmentVariable("BUILD_REVISION"),
                buildTarget=Environment.GetEnvironmentVariable("BUILD_TARGET"),buildNumber=Environment.GetEnvironmentVariable("UCB_BUILD_NUMBER")};
            if(value.sourceCommit==null||!Regex.IsMatch(value.sourceCommit,"\\A[0-9a-f]{40}\\z")||
               string.IsNullOrWhiteSpace(value.buildTarget)||string.IsNullOrWhiteSpace(value.buildNumber))
                throw new BuildFailedException("Build Automation must supply BUILD_REVISION (full Git SHA), BUILD_TARGET and UCB_BUILD_NUMBER.");
            return value;
        }

        [Serializable]
        private sealed class Receipt
        {
            public int schema=1;
            public string sourceCommit,buildTarget,buildNumber,unityVersion,editorLog,colorSpace;
            public NativeBuildIdentity build;
        }
    }
}
