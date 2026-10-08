using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using ChipArena.UnityConversion;

namespace ChipArena.NativeUpdater
{
    public sealed class UpdateInstaller
    {
        private readonly string root,work,app,backup,journalPath;
        private readonly Action<string> log;
        // Tests inject a simulated interruption immediately after durable filesystem transitions.
        public Action<string> Checkpoint;
        public UpdateInstaller(string installRoot,Action<string> progress)
        {
            root=Path.GetFullPath(installRoot);UpdateStorage.ValidateRoot(root);work=UpdateStorage.Work(root);
            NativeUpdateRules.RejectReparsePoints(work);Directory.CreateDirectory(work);
            app=Path.Combine(root,"app");backup=Path.Combine(work,"previous");journalPath=Path.Combine(work,"journal.json");log=progress??delegate{};
        }
        public FileStream Lock(){return new FileStream(Path.Combine(work,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        private void Mark(string phase){if(Checkpoint!=null)Checkpoint(phase);}
        private void Result(string status,string message,string version)
        {UpdateStorage.Write(Path.Combine(work,"result.json"),new NativeUpdateResult{status=status,message=message,version=version});log(message);}
        private void DeleteOwnedDirectory(string path)
        {
            if(!Directory.Exists(path))return;
            NativeUpdateRules.RejectReparsePoints(path);
            foreach(var entry in Directory.GetFileSystemEntries(path))
            {
                if((File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0)throw new IOException("Unexpected link in updater files.");
                if(Directory.Exists(entry))DeleteOwnedDirectory(entry);else File.Delete(entry);
            }
            Directory.Delete(path);
        }
        public void Recover()
        {
            if(!File.Exists(journalPath))return;
            var j=UpdateStorage.Read<NativeUpdateJournal>(journalPath);NativeUpdateRules.ValidateBuild(j.before);NativeUpdateRules.ValidateBuild(j.after);
            string transaction=UpdateStorage.Transaction(root,j.token),ack=Path.Combine(transaction,"startup.txt");
            NativeUpdateRules.RejectReparsePoints(transaction);
            bool currentExists=Directory.Exists(app);
            NativeBuildIdentity current=currentExists?UpdateStorage.Build(app):null;
            if(currentExists&&NativeUpdateRules.SameBuild(current,j.after)&&File.Exists(ack)&&File.ReadAllText(ack).Trim()==j.token+"/"+j.after.version)
            {File.Delete(journalPath);Result("installed","Update installed successfully.",j.after.version);return;}
            if(Directory.Exists(backup))
            {
                if(!NativeUpdateRules.SameBuild(UpdateStorage.Build(backup),j.before))throw new InvalidDataException("Update backup identity differs; leave the installation intact for recovery.");
                if(currentExists&&!NativeUpdateRules.SameBuild(current,j.after)&&!NativeUpdateRules.SameBuild(current,j.before))throw new InvalidDataException("Unexpected player version during recovery.");
                if(currentExists)DeleteOwnedDirectory(app);
                Directory.Move(backup,app);File.Delete(journalPath);
                Result("rolled-back","The update did not confirm startup. The previous version has been restored.",j.before.version);return;
            }
            if(currentExists&&NativeUpdateRules.SameBuild(current,j.before))
            {File.Delete(journalPath);Result("cancelled","The interrupted update left the previous version unchanged.",j.before.version);return;}
            throw new InvalidDataException("The update needs recovery, but no verified player or backup was found.");
        }
        public string Prepare(NativeUpdateRequest request)
        {
            NativeUpdateRules.ValidateManifest(request.manifest);
            string transaction=UpdateStorage.Transaction(root,request.token),zip=Path.Combine(transaction,"download.zip"),stage=Path.Combine(transaction,"staged");
            NativeUpdateRules.RejectReparsePoints(transaction);
            var before=UpdateStorage.Build(app);
            if(!NativeUpdateRules.IsNewer(before,request.manifest.build))throw new InvalidDataException("The update is already installed or is older than this build.");
            if(File.Exists(journalPath))throw new InvalidOperationException("Restart through the launcher to recover the preceding update first.");
            if(Directory.Exists(stage))throw new IOException("This update transaction was already prepared. Check for updates again.");
            var m=request.manifest;NativeUpdateRules.VerifyFile(zip,m.bytes,m.sha256);
            long available=new DriveInfo(Path.GetPathRoot(root)).AvailableFreeSpace;
            if(available<m.unpackedBytes+64L*1024*1024)throw new IOException("There is not enough free disk space to stage this update.");
            Directory.CreateDirectory(stage);
            try
            {
                using(var file=File.OpenRead(zip))using(var archive=new ZipArchive(file,ZipArchiveMode.Read))
                {
                    if(archive.Entries.Count!=m.files)throw new InvalidDataException("Update file count differs from its manifest.");
                    var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
                    foreach(var entry in archive.Entries)
                    {
                        string path=NativeUpdateRules.SafeRelativePath(entry.FullName);
                        if(!paths.Add(path))throw new InvalidDataException("Duplicate or case-colliding paths in update.");
                        int type=(entry.ExternalAttributes>>16)&0xf000;
                        if(type!=0&&type!=0x8000)throw new InvalidDataException("Update archives must contain ordinary files only.");
                        total=checked(total+entry.Length);if(total>m.unpackedBytes)throw new InvalidDataException("Update expands beyond its declared size.");
                    }
                    if(total!=m.unpackedBytes)throw new InvalidDataException("Update unpacked size differs from its manifest.");
                    int number=0;var buffer=new byte[65536];
                    foreach(var entry in archive.Entries)
                    {
                        string destination=Path.GetFullPath(Path.Combine(stage,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
                        if(!destination.StartsWith(stage+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update escaped its staging directory.");
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using(var input=entry.Open())using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                        {
                            long copied=0;int n;
                            while((n=input.Read(buffer,0,buffer.Length))>0){copied+=n;if(copied>entry.Length)throw new InvalidDataException("Update entry exceeds its declared size.");output.Write(buffer,0,n);}
                            if(copied!=entry.Length)throw new InvalidDataException("Update entry is truncated.");output.Flush(true);
                        }
                        if(++number%100==0)log("Preparing update: "+number+" / "+m.files+" files");
                    }
                }
                if(!NativeUpdateRules.SameBuild(UpdateStorage.Build(stage),m.build))throw new InvalidDataException("Packaged build identity differs from its manifest.");
                return stage;
            }
            catch{DeleteOwnedDirectory(stage);throw;}
        }
        public void Swap(NativeUpdateRequest request)
        {
            var before=UpdateStorage.Build(app);var after=request.manifest.build;
            if(!NativeUpdateRules.IsNewer(before,after))throw new InvalidOperationException("Installed build changed before update could apply.");
            string staged=Path.Combine(UpdateStorage.Transaction(root,request.token),"staged");
            if(!NativeUpdateRules.SameBuild(UpdateStorage.Build(staged),after))throw new InvalidDataException("Staged update identity changed.");
            if(File.Exists(journalPath))throw new IOException("Another update needs recovery.");
            DeleteOwnedDirectory(backup);
            var j=new NativeUpdateJournal{token=request.token,before=before,after=after,phase="prepared"};
            UpdateStorage.Write(journalPath,j);Mark("prepared");
            Directory.Move(app,backup);Mark("old-moved");j.phase="old-moved";UpdateStorage.Write(journalPath,j);
            Directory.Move(staged,app);Mark("new-moved");j.phase="new-moved";UpdateStorage.Write(journalPath,j);
        }
        public void Confirm(NativeUpdateRequest request)
        {
            string transaction=UpdateStorage.Transaction(root,request.token);
            if(File.ReadAllText(Path.Combine(transaction,"startup.txt")).Trim()!=request.token+"/"+request.manifest.build.version)throw new InvalidDataException("New player did not acknowledge startup.");
            if(!NativeUpdateRules.SameBuild(UpdateStorage.Build(app),request.manifest.build))throw new InvalidDataException("Installed build differs after restart.");
            File.Delete(journalPath);Result("installed","Update installed successfully.",request.manifest.build.version);
            File.Delete(Path.Combine(transaction,"download.zip"));
        }
        public void Failed(NativeUpdateRequest request,string message)
        {
            string transaction=UpdateStorage.Transaction(root,request.token),ack=Path.Combine(transaction,"startup.txt");
            if(File.Exists(ack))File.Delete(ack);
            Recover();Result("error",message,UpdateStorage.Build(app).version);
            File.WriteAllText(Path.Combine(transaction,"error.txt"),message);
            DeleteOwnedDirectory(Path.Combine(transaction,"staged"));File.Delete(Path.Combine(transaction,"download.zip"));
        }
    }
}
