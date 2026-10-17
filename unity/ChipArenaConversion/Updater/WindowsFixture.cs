// Compiled only by the Windows updater integration test, never shipped in the player or launcher.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ChipArena.UnityConversion;
using ChipArena.NativeUpdater;
class WindowsFixture
{
    static int Main(string[] args)
    {
        string app=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),root=Path.GetDirectoryName(app),work=UpdateStorage.Work(root);Directory.CreateDirectory(work);
        if(args.Length==1&&args[0]=="--hold")
        {
            using(var p=Process.GetCurrentProcess())File.WriteAllText(Path.Combine(work,"fixture-parent.json"),"{\"pid\":"+p.Id+",\"ticks\":"+p.StartTime.ToUniversalTime().Ticks+"}");
            var end=DateTime.UtcNow.AddSeconds(60);while(!File.Exists(Path.Combine(work,"close-parent"))&&DateTime.UtcNow<end)Thread.Sleep(50);return 0;
        }
        if(args.Length==0)File.WriteAllText(Path.Combine(work,"normal-start.txt"),Environment.CurrentDirectory);
        if(File.Exists(Path.Combine(app,"fail-start.txt")))return 13;
        if(args.Length==2&&args[0]=="--native-update-token")
        {string folder=UpdateStorage.Transaction(root,args[1]);File.WriteAllText(Path.Combine(folder,"startup.txt"),args[1]+"/"+UpdateStorage.Build(app).version);Thread.Sleep(1500);}
        return 0;
    }
}
