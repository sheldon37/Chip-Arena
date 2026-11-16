using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    internal static class CommunityMapBundle
    {
        [MenuItem("Assets/Chip Arena/Export selected map prefab for Windows",true)]
        private static bool CanExport()=>Selection.activeObject is GameObject&&AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".prefab");

        [MenuItem("Assets/Chip Arena/Export selected map prefab for Windows")]
        private static void Export()
        {
            string folder=EditorUtility.OpenFolderPanel("Content pack directory","","");
            if(string.IsNullOrEmpty(folder))return;
            string asset=AssetDatabase.GetAssetPath(Selection.activeObject);
            // Build into Library, keeping Unity build receipts and sidecar manifests out of the package.
            string name="arena-"+AssetDatabase.AssetPathToGUID(asset);
            string cache=Path.GetFullPath(Path.Combine("Library","CommunityBundles",name));
            Directory.CreateDirectory(cache);
            var build=new AssetBundleBuild{assetBundleName=name,assetNames=new[]{asset},addressableNames=new[]{"arena"}};
            var manifest=BuildPipeline.BuildAssetBundles(cache,new[]{build},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
            if(manifest==null)throw new System.InvalidOperationException("Unity could not build the map bundle");
            File.Copy(Path.Combine(cache,name),Path.Combine(folder,"arena-assets.bundle"),true);
            Debug.Log("Exported map prefab. Use the prefab address arena in chiparena.pack.json.");
        }
    }
}
