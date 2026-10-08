using System;
using System.IO;
using System.Linq;

namespace ChipArena.UnityConversion.Editor
{
    // No Unity API dependency: exercised with real directories on Linux and Windows CI.
    public static class NativeCloudPlayer
    {
        public const string PlayerName="ChipArena-Native-Conversion-TEST";

        public static string FindExecutable(string exportPath)
        {
            string path=Path.GetFullPath(exportPath);
            if(Directory.Exists(path))
            {
                var candidates=Directory.GetFiles(path,"*.exe").Where(p=>Directory.Exists(Path.Combine(path,Path.GetFileNameWithoutExtension(p)+"_Data"))).ToArray();
                if(candidates.Length!=1) throw new IOException("Cloud export must contain exactly one Unity player.");
                path=candidates[0];
            }
            string folder=Path.GetDirectoryName(path);
            if(!File.Exists(path)||!path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||
               !File.Exists(Path.Combine(folder,"UnityPlayer.dll"))||
               !Directory.Exists(Path.Combine(folder,Path.GetFileNameWithoutExtension(path)+"_Data")))
                throw new IOException("Cloud export is not a complete Windows Unity player.");
            return path;
        }

        public static void Stage(string exportPath,string destination)
        {
            string exe=FindExecutable(exportPath),source=Path.GetDirectoryName(exe);
            destination=Path.GetFullPath(destination);
            string a=source.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string b=destination.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(a.StartsWith(b,StringComparison.OrdinalIgnoreCase)||b.StartsWith(a,StringComparison.OrdinalIgnoreCase))
                throw new IOException("Cloud export and staging directories must be separate.");
            if(Directory.Exists(destination)&&Directory.EnumerateFileSystemEntries(destination).Any())
                throw new IOException("Cloud staging directory must be empty.");
            string name=Path.GetFileNameWithoutExtension(exe);
            if(name!=PlayerName&&(File.Exists(Path.Combine(source,PlayerName+".exe"))||Directory.Exists(Path.Combine(source,PlayerName+"_Data"))))
                throw new IOException("Cloud player name collides with an existing export file.");
            Copy(source,destination);
            if(name!=PlayerName)
            {
                File.Move(Path.Combine(destination,Path.GetFileName(exe)),Path.Combine(destination,PlayerName+".exe"));
                Directory.Move(Path.Combine(destination,name+"_Data"),Path.Combine(destination,PlayerName+"_Data"));
            }
        }

        private static void Copy(string source,string destination)
        {
            if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0) throw new IOException("Cloud export contains a link.");
            Directory.CreateDirectory(destination);
            foreach(string file in Directory.GetFiles(source))
            {
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0) throw new IOException("Cloud export contains a link.");
                File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));
            }
            foreach(string dir in Directory.GetDirectories(source)) Copy(dir,Path.Combine(destination,Path.GetFileName(dir)));
        }
    }
}
