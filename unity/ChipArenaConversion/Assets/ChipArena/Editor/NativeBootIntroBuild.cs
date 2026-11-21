using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChipArena.UnityConversion.Editor
{
    /// <summary>
    /// The logo intro (Runtime/NativeBootIntro.cs, docs/BOOT-INTRO.md) in the Editor and in builds. It never plays in the
    /// Editor unless asked for here: Chip Arena > Boot intro > Play in Play mode (every entry, until unticked) or Play once
    /// (enters Play mode with it). The local capture tool's runs never see it unless they ask (they set Play once).
    /// </summary>
    public static class NativeBootIntroMenu
    {
        private const string Always="Chip Arena/Boot intro/Play in Play mode",Once="Chip Arena/Boot intro/Play once";
        [MenuItem(Always)] private static void ToggleAlways(){EditorPrefs.SetBool(NativeBootIntro.EditorAlwaysKey,!EditorPrefs.GetBool(NativeBootIntro.EditorAlwaysKey,false));}
        [MenuItem(Always,true)] private static bool ToggleAlwaysShown(){Menu.SetChecked(Always,EditorPrefs.GetBool(NativeBootIntro.EditorAlwaysKey,false));return !EditorApplication.isPlayingOrWillChangePlaymode;}
        [MenuItem(Once)] private static void PlayOnce(){SessionState.SetBool(NativeBootIntro.EditorOnceKey,true);EditorApplication.isPlaying=true;}
        [MenuItem(Once,true)] private static bool PlayOnceShown()=>!EditorApplication.isPlayingOrWillChangePlaymode;
    }

    /// <summary>
    /// Every build carries the intro's clip as committed: before building, the project's copy (Assets/StreamingAssets) must be
    /// the pinned file (NativeBootIntroRules.ClipBytes and ClipSha256); after a standalone build, the player's copy must be too.
    /// </summary>
    public sealed class NativeBootIntroBuildCheck : IPreprocessBuildWithReport,IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public static string ProjectClip=>Path.Combine(Application.dataPath,"StreamingAssets",NativeBootIntroRules.Clip);
        public void OnPreprocessBuild(BuildReport report){Verify(ProjectClip,"the project");}
        public void OnPostprocessBuild(BuildReport report)
        {
            var target=report.summary.platform;if(target!=BuildTarget.StandaloneWindows64&&target!=BuildTarget.StandaloneWindows&&target!=BuildTarget.StandaloneLinux64)return;
            string exe=report.summary.outputPath,data=Path.Combine(Path.GetDirectoryName(exe),Path.GetFileNameWithoutExtension(exe)+"_Data");
            Verify(Path.Combine(data,"StreamingAssets",NativeBootIntroRules.Clip),"the player");
            Debug.Log("BOOT_INTRO_CLIP_IN_BUILD: "+NativeBootIntroRules.Clip+" ("+NativeBootIntroRules.ClipBytes+" bytes) in "+Path.Combine(data,"StreamingAssets"));
        }
        public static void Verify(string path,string where)
        {
            if(!File.Exists(path))throw new BuildFailedException("The boot intro's clip is missing from "+where+": "+path);
            long bytes=new FileInfo(path).Length;string hash;using(var sha=SHA256.Create())using(var f=File.OpenRead(path))hash=string.Concat(sha.ComputeHash(f).Select(b=>b.ToString("x2")));
            if(bytes!=NativeBootIntroRules.ClipBytes||hash!=NativeBootIntroRules.ClipSha256)throw new BuildFailedException("The boot intro's clip in "+where+" is not the committed file ("+bytes+" bytes, SHA-256 "+hash+"): "+path);
        }
    }
}
