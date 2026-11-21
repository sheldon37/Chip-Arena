using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ChipArena.UnityConversion;
using ChipArena.UnityConversion.Editor;
using ChipArena.NativeUpdater;

static class StarterPackageChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static void Run()
    {
        string scratch=Path.Combine(Path.GetTempPath(),"chip starter spaces "+Guid.NewGuid().ToString("N"));
        string install=Path.Combine(scratch,"build"),app=Path.Combine(install,"app"),zip=Path.Combine(scratch,NativeUpdateRules.PortableZip);
        try
        {
            Directory.CreateDirectory(Path.Combine(app,NativeUpdateRules.DataDirectory));
            File.WriteAllText(Path.Combine(install,NativeUpdateRules.LauncherExecutable),"launcher");
            File.WriteAllText(Path.Combine(install,NativeUpdateRules.LauncherExecutable+".config"),"config");
            File.WriteAllText(Path.Combine(install,"native-install.txt"),NativeUpdateRules.Product+"/1\n");
            File.WriteAllText(Path.Combine(install,"READ-ME.txt"),"Open Chip Arena.exe");
            File.WriteAllText(Path.Combine(install,NativeUpdateRules.LegacyExecutable),"stale legacy launcher");
            File.WriteAllText(Path.Combine(install,"build.log"),"do not distribute");
            File.WriteAllText(Path.Combine(app,NativeUpdateRules.Executable),"player");
            File.WriteAllText(Path.Combine(app,"UnityPlayer.dll"),"unity");
            File.WriteAllText(Path.Combine(app,NativeUpdateRules.DataDirectory,"globalgamemanagers"),"data");
            UpdateStorage.Write(Path.Combine(app,NativeUpdateRules.IdentityFile),new NativeBuildIdentity{version=new string('a',64),revision=1});
            NativeStarterPackage.Create(install,zip);
            using(var archive=ZipFile.OpenRead(zip))
            {
                var names=archive.Entries.Select(e=>e.FullName).ToArray();
                Check(names.All(n=>n.StartsWith("Chip Arena/",StringComparison.Ordinal)),"portable ZIP has one Chip Arena root");
                Check(names.Where(n=>n.Count(c=>c=='/')==1&&n.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)).SequenceEqual(new[]{"Chip Arena/Chip Arena.exe"}),"portable ZIP has one clear launcher");
                Check(names.Length==8&&!names.Any(n=>n.EndsWith("build.log")||n=="Chip Arena/"+NativeUpdateRules.LegacyExecutable),"portable ZIP excludes stale launcher and build diagnostics");
            }
            string extracted=Path.Combine(scratch,"moved folder with spaces");ZipFile.ExtractToDirectory(zip,extracted);
            string root=Path.Combine(extracted,"Chip Arena");UpdateStorage.ValidateRoot(root);var build=UpdateStorage.Build(Path.Combine(root,"app"));
            Check(build.version==new string('a',64)&&NativeUpdateRules.LauncherPath(root)==Path.Combine(root,"Chip Arena.exe"),"extracted renamed game folder retains launcher and player contract");
            File.Delete(Path.Combine(install,NativeUpdateRules.LauncherExecutable));bool rejected=false;
            try{NativeStarterPackage.Create(install,zip);}catch(FileNotFoundException){rejected=true;}
            Check(rejected,"packaging refuses a missing friendly launcher even if a stale legacy launcher exists");
        }
        finally{if(Directory.Exists(scratch))Directory.Delete(scratch,true);}
    }
}
