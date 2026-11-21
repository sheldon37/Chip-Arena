using System;
using System.IO;
using ChipArena.UnityConversion.Editor;

static class CloudPlayerChecks
{
    static void Check(bool condition,string name)
    {if(!condition)throw new Exception(name);Console.WriteLine("PASS cloud export: "+name);}
    static void Reject(Action action,string name)
    {bool failed=false;try{action();}catch(IOException){failed=true;}Check(failed,name);}

    public static void Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"chip cloud "+Guid.NewGuid().ToString("N"));
        string source=Path.Combine(root,"Export with spaces"),target=Path.Combine(root,"app");
        Directory.CreateDirectory(Path.Combine(source,"Cloud Name_Data","Managed"));
        File.WriteAllText(Path.Combine(source,"Cloud Name.exe"),"player");
        File.WriteAllText(Path.Combine(source,"UnityPlayer.dll"),"engine");
        File.WriteAllText(Path.Combine(source,"Cloud Name_Data","Managed","game.dll"),"assembly");
        try
        {
            string exe=NativeCloudPlayer.FindExecutable(source);
            Check(Path.GetFileName(exe)=="Cloud Name.exe","directory input identifies the player");
            NativeCloudPlayer.Stage(exe,target);
            Check(File.ReadAllText(Path.Combine(target,NativeCloudPlayer.PlayerName+"_Data","Managed","game.dll"))=="assembly"&&
                  File.ReadAllText(Path.Combine(target,NativeCloudPlayer.PlayerName+".exe"))=="player","rename player and data together");
            Check(File.Exists(exe)&&Directory.Exists(Path.Combine(source,"Cloud Name_Data")),"original cloud export preserved");
            Reject(()=>NativeCloudPlayer.Stage(exe,target),"refuse nonempty destination");
            Reject(()=>NativeCloudPlayer.Stage(exe,Path.Combine(source,"inside")),"refuse recursive output");
            Reject(()=>NativeCloudPlayer.Stage(exe,root),"refuse ancestor output");
            File.WriteAllText(Path.Combine(source,NativeCloudPlayer.PlayerName+".exe"),"collision");
            Reject(()=>NativeCloudPlayer.Stage(exe,Path.Combine(root,"collision")),"refuse name collision");
            File.Delete(Path.Combine(source,NativeCloudPlayer.PlayerName+".exe"));
            if(!OperatingSystem.IsWindows())
            {
                File.CreateSymbolicLink(Path.Combine(source,"linked-file"),exe);
                Reject(()=>NativeCloudPlayer.Stage(exe,Path.Combine(root,"linked")),"refuse symbolic links");
                File.Delete(Path.Combine(source,"linked-file"));
            }
            File.Delete(Path.Combine(source,"UnityPlayer.dll"));
            Reject(()=>NativeCloudPlayer.FindExecutable(exe),"refuse incomplete export");
        }
        finally{Directory.Delete(root,true);}
    }
}
