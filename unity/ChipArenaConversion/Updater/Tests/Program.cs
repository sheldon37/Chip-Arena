using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ChipArena.UnityConversion;
using ChipArena.NativeUpdater;

static class Program
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Reject(Action action,string name){bool failed=false;try{action();}catch(Exception){failed=true;}Check(failed,name);}
    static NativeBuildIdentity Identity(char c,long rev){return new NativeBuildIdentity{version=new string(c,64),revision=rev};}
    sealed class Fixture:IDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"chip-native-updater-"+Guid.NewGuid().ToString("N"));
        public string App{get{return Path.Combine(Root,"app");}}
        public Fixture()
        {
            Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,NativeUpdateRules.Executable),"stable launcher");File.WriteAllText(Path.Combine(Root,"native-install.txt"),NativeUpdateRules.Product+"/1");
            Player(App,Identity('a',1));File.WriteAllText(Path.Combine(App,"obsolete.dll"),"old");File.WriteAllText(Path.Combine(Root,"settings-sentinel.json"),"preserve");
        }
        public static void Player(string dir,NativeBuildIdentity build)
        {Directory.CreateDirectory(Path.Combine(dir,NativeUpdateRules.DataDirectory));File.WriteAllText(Path.Combine(dir,NativeUpdateRules.Executable),"player");File.WriteAllText(Path.Combine(dir,"UnityPlayer.dll"),"native DLL");File.WriteAllText(Path.Combine(dir,NativeUpdateRules.DataDirectory,"globalgamemanagers"),"data");UpdateStorage.Write(Path.Combine(dir,NativeUpdateRules.IdentityFile),build);}
        public NativeUpdateRequest Request(NativeBuildIdentity build=null,Action<ZipArchive> extra=null)
        {
            build=build??Identity('b',2);string token=Guid.NewGuid().ToString("N"),tx=UpdateStorage.Transaction(Root,token);Directory.CreateDirectory(tx);
            string source=Path.Combine(tx,"source");Player(source,build);string zip=Path.Combine(tx,"download.zip");
            using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))
            {foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))archive.CreateEntryFromFile(file,Path.GetRelativePath(source,file).Replace('\\','/'));if(extra!=null)extra(archive);}
            string hash=NativeUpdateRules.HashFile(zip);long length=new FileInfo(zip).Length,unpacked;int count;
            using(var archive=ZipFile.OpenRead(zip)){count=archive.Entries.Count;unpacked=archive.Entries.Sum(e=>e.Length);}
            return new NativeUpdateRequest{token=token,manifest=new NativeUpdateManifest{build=build,sha256=hash,bytes=length,unpackedBytes=unpacked,files=count,parts=new[]{new NativeUpdatePart{name=hash+"-0000.part",sha256=hash,bytes=length}}}};
        }
        public UpdateInstaller Installer(){return new UpdateInstaller(Root,Console.WriteLine);}
        public void Ack(NativeUpdateRequest r){File.WriteAllText(Path.Combine(UpdateStorage.Transaction(Root,r.token),"startup.txt"),r.token+"/"+r.manifest.build.version);}
        public void Dispose(){Directory.Delete(Root,true);}
    }
    static void Main()
    {
        CloudPlayerChecks.Run();StarterPackageChecks.Run();
        using(var f=new Fixture())
        {
            string old=Path.Combine(f.Root,NativeUpdateRules.Executable),friendly=Path.Combine(f.Root,NativeUpdateRules.LauncherExecutable);
            Check(NativeUpdateRules.LauncherPath(f.Root)==old,"legacy launcher remains discoverable");
            File.Move(old,friendly);UpdateStorage.ValidateRoot(f.Root);
            Check(NativeUpdateRules.LauncherPath(f.Root)==friendly,"friendly launcher works without a legacy alias");
            var request=f.Request();var installer=f.Installer();installer.Prepare(request);installer.Swap(request);f.Ack(request);installer.Confirm(request);
            Check(UpdateStorage.Build(f.App).version==new string('b',64)&&File.Exists(friendly),"friendly installation updates while preserving launcher");
            File.Delete(friendly);Reject(()=>UpdateStorage.ValidateRoot(f.Root),"missing launcher is rejected");
        }
        foreach(string path in new[]{"../outside","C:/escape","a\\b","/absolute","a/../b","CON.dll","LPT1.txt","COM¹.dll","a. ","a ","a.","a//b","a:stream","dir/NUL","dir/./b"})Reject(()=>NativeUpdateRules.SafeRelativePath(path),"reject unsafe path "+path);
        Check(NativeUpdateRules.SafeRelativePath("MonoBleedingEdge/etc/mono/4.5/machine.config")!="","normal Unity path accepted");
        Check(NativeUpdateRules.IsNewer(Identity('a',1),Identity('b',2)),"new build available");
        Check(!NativeUpdateRules.IsNewer(Identity('a',2),Identity('b',1)),"downgrade refused");
        Check(!NativeUpdateRules.IsNewer(Identity('a',1),Identity('a',2)),"same source version skipped");
        foreach(var mutate in new Action<NativeBuildIdentity>[] {b=>b.product="electron",b=>b.channel="live",b=>b.platform="linux",b=>b.bootstrap=99,b=>b.version="../evil"})
        {var b=Identity('b',2);mutate(b);Reject(()=>NativeUpdateRules.ValidateBuild(b),"reject wrong product/channel/platform/launcher/version");}
        using(var f=new Fixture())
        {
            var r=f.Request();NativeUpdateRules.ValidateManifest(r.manifest);var i=f.Installer();i.Prepare(r);i.Swap(r);f.Ack(r);i.Confirm(r);
            Check(UpdateStorage.Build(f.App).version==new string('b',64),"verified update installed");
            Check(!File.Exists(Path.Combine(f.App,"obsolete.dll")),"obsolete player files removed by directory replacement");
            Check(File.ReadAllText(Path.Combine(f.Root,"settings-sentinel.json"))=="preserve","files outside player untouched");
            Check(UpdateStorage.Build(Path.Combine(UpdateStorage.Work(f.Root),"previous")).version==new string('a',64),"last good player retained");
            var next=f.Request(Identity('c',3));i.Prepare(next);i.Swap(next);f.Ack(next);i.Confirm(next);
            Check(UpdateStorage.Build(Path.Combine(UpdateStorage.Work(f.Root),"previous")).version==new string('b',64),"second update retains one previous version");
        }
        foreach(string phase in new[]{"prepared","old-moved","new-moved"})
        using(var f=new Fixture())
        {
            var r=f.Request();var i=f.Installer();i.Prepare(r);i.Checkpoint=p=>{if(p==phase)throw new IOException("simulated power loss");};
            Reject(()=>i.Swap(r),"interruption after "+phase);f.Installer().Recover();
            Check(UpdateStorage.Build(f.App).version==new string('a',64),"recover interruption after "+phase);
        }
        using(var f=new Fixture())
        {var r=f.Request();var i=f.Installer();i.Prepare(r);i.Swap(r);f.Ack(r);f.Installer().Recover();Check(UpdateStorage.Build(f.App).version==new string('b',64),"acknowledged startup survives launcher interruption");}
        using(var f=new Fixture())
        {var r=f.Request();var i=f.Installer();i.Prepare(r);i.Swap(r);i.Failed(r,"startup failed");Check(UpdateStorage.Build(f.App).version==new string('a',64),"startup failure rolls back");}
        foreach(string evil in new[]{"../escaped.txt","UnityPlayer.DLL","CON.txt","nested/../../escaped.txt"})
        using(var f=new Fixture())
        {
            var r=f.Request(extra:a=>{using(var w=new StreamWriter(a.CreateEntry(evil).Open()))w.Write("bad");});Reject(()=>f.Installer().Prepare(r),"reject malicious archive "+evil);
            Check(UpdateStorage.Build(f.App).version==new string('a',64),"malicious archive leaves current player intact");
        }
        using(var f=new Fixture())
        {var r=f.Request(extra:a=>{var e=a.CreateEntry("link");e.ExternalAttributes=unchecked((int)0xa1ff0000);});Reject(()=>f.Installer().Prepare(r),"reject symlink archive entry");}
        using(var f=new Fixture())
        {var r=f.Request();File.AppendAllText(Path.Combine(UpdateStorage.Transaction(f.Root,r.token),"download.zip"),"tamper");Reject(()=>f.Installer().Prepare(r),"reject tampered archive");}
        using(var f=new Fixture())
        {var r=f.Request();r.manifest.unpackedBytes--;Reject(()=>f.Installer().Prepare(r),"reject decompression beyond declared limit");}
        using(var f=new Fixture())
        {var r=f.Request();r.manifest.parts[0].name="../evil";Reject(()=>NativeUpdateRules.ValidateManifest(r.manifest),"manifest cannot redirect part paths");}
        using(var f=new Fixture())
        {var r=f.Request();r.manifest.parts[0].bytes++;Reject(()=>NativeUpdateRules.ValidateManifest(r.manifest),"manifest part size mismatch");}
        using(var f=new Fixture())
        {using(f.Installer().Lock())Reject(()=>{using(f.Installer().Lock()){}},"parallel installer refused");}
        Console.WriteLine(checks+" native updater checks passed; Unity rendering was not exercised.");
    }
}
