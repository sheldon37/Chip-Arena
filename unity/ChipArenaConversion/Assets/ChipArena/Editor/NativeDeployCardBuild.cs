#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace ChipArena.UnityConversion.Editor
{
    internal static class NativeDeployCardBuild
    {
        internal static void Prepare()
        {
            void Bake(string id,bool mutator,string theme=null)
            {
                string path="Assets/ChipArena/Resources/"+NativeDeployCardCache.Key(id,mutator,theme)+".bytes";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path,NativeDeployCardCache.Pack(NativeDeployArt.Tile(id,mutator,theme)));
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var asset=Resources.Load<TextAsset>(NativeDeployCardCache.Key(id,mutator,theme));
                if(asset==null||asset.bytes.Length!=NativeDeployArt.TilePixels*NativeDeployArt.TilePixels*4)throw new InvalidOperationException("Deployment card failed import: "+path);
            }
            Bake(NativeDeployArt.Back,true);
            foreach(var id in NativeExpeditionMutators.Ids)Bake(id,true);
            Bake("exp-ward-zero",false,"quarantine");Bake("exp-transit",false,"rail");Bake("exp-foundry",false,"industrial");
            Bake(NativeBackroomsGenerator.AreaId,false,"backrooms");Bake(NativePoolroomsGenerator.AreaId,false,"poolrooms");
            // ATLAS's split-flap glyph atlas (Core NativeAtlasArt), once, under a key that changes with what it is drawn from
            {
                string key=NativeAtlasArt.Key,path="Assets/ChipArena/Resources/"+key+".bytes";
                foreach(var old in Directory.GetFiles(Path.GetDirectoryName(path),"atlas-flaps-*.bytes"))if(Path.GetFileName(old)!=Path.GetFileName(path))AssetDatabase.DeleteAsset(old.Replace('\\','/'));
                File.WriteAllBytes(path,NativeAtlasArt.Pack(NativeAtlasArt.Atlas()));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var asset=Resources.Load<TextAsset>(key);if(asset==null||asset.bytes.Length!=NativeAtlasArt.Bytes)throw new InvalidOperationException("ATLAS flap atlas failed import: "+path);
            }
            Debug.Log("PASS deployment card bake: existing catalog art packaged before first roll");
        }
    }
}
#endif
