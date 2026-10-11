using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using ChipArena.UnityConversion;

namespace ChipArena.NativeUpdater
{
    public static class UpdateStorage
    {
        public static T Read<T>(string path)
        {
            if(new FileInfo(path).Length>NativeUpdateRules.MaxManifestBytes)throw new InvalidDataException("Update metadata is too large.");
            using(var file=File.OpenRead(path))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(file);
        }
        public static void Write<T>(string path,T value)
        {
            string temp=path+".tmp";
            using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            {new DataContractJsonSerializer(typeof(T)).WriteObject(file,value);file.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        public static NativeBuildIdentity Build(string folder)
        {
            NativeUpdateRules.RejectReparsePoints(folder);
            var build=Read<NativeBuildIdentity>(Path.Combine(folder,NativeUpdateRules.IdentityFile));NativeUpdateRules.ValidateBuild(build);
            if(!File.Exists(Path.Combine(folder,NativeUpdateRules.Executable))||!File.Exists(Path.Combine(folder,"UnityPlayer.dll"))||!Directory.Exists(Path.Combine(folder,NativeUpdateRules.DataDirectory)))
                throw new InvalidDataException("The native player folder is incomplete.");
            return build;
        }
        public static string Work(string root){return Path.Combine(root,NativeUpdateRules.WorkDirectory);}
        public static string Transaction(string root,string token)
        {if(!NativeUpdateRules.IsToken(token))throw new InvalidDataException("Invalid update transaction.");return Path.Combine(Work(root),token);}
        public static void ValidateRoot(string root)
        {
            root=Path.GetFullPath(root);NativeUpdateRules.RejectReparsePoints(root);
            if(root==Path.GetPathRoot(root)||!File.Exists(NativeUpdateRules.LauncherPath(root))||File.ReadAllText(Path.Combine(root,"native-install.txt")).Trim()!=NativeUpdateRules.Product+"/1")
                throw new InvalidDataException("Launch updates from the complete native starter installation.");
        }
    }
}
