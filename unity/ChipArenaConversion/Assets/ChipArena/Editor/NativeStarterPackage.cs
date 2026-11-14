using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ChipArena.UnityConversion.Editor
{
    // Shared by the Unity packager and managed archive checks; no Unity API dependency.
    public static class NativeStarterPackage
    {
        public static void Create(string install,string zip)
        {
            NativeUpdateRules.RejectReparsePoints(install);
            string app=Path.Combine(install,"app"),prefix=NativeUpdateRules.DistributionFolder+"/";
            string[] required={NativeUpdateRules.LauncherExecutable,NativeUpdateRules.LauncherExecutable+".config","native-install.txt","READ-ME.txt"};
            foreach(string name in required)if(!File.Exists(Path.Combine(install,name)))throw new FileNotFoundException("Incomplete portable game folder: "+name);
            var player=Directory.GetFiles(app,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            if(!File.Exists(Path.Combine(app,NativeUpdateRules.Executable))||!File.Exists(Path.Combine(app,"UnityPlayer.dll"))||!File.Exists(Path.Combine(app,NativeUpdateRules.IdentityFile))||!Directory.Exists(Path.Combine(app,NativeUpdateRules.DataDirectory)))throw new IOException("Portable package is missing the Unity player.");
            if(File.Exists(zip))File.Delete(zip);
            using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))
            {
                foreach(string name in required)
                {
                    string file=Path.Combine(install,name);if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Portable package contains a symbolic link.");
                    archive.CreateEntryFromFile(file,prefix+name,CompressionLevel.Optimal);
                }
                foreach(string file in player)
                {
                    NativeUpdateRules.RejectReparsePoints(Path.GetDirectoryName(file));if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Portable package contains a symbolic link.");
                    string rel=file.Substring(app.Length+1).Replace('\\','/');NativeUpdateRules.SafeRelativePath(rel);
                    archive.CreateEntryFromFile(file,prefix+"app/"+rel,CompressionLevel.Optimal);
                }
            }
        }
    }
}
