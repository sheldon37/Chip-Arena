using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>Checkpoint state and irreversible extraction tombstones are intentionally separate.</summary>
    [Serializable] public sealed class ExpeditionSavepoint
    {
        public int schema=1;
        public ExpeditionSnapshot checkpoint;
        public string[] extracted=Array.Empty<string>();
        public static ExpeditionSavepoint Capture(NativeExpedition run)
        {
            if(run==null||run.Migrating)throw new InvalidOperationException("Cannot checkpoint during host handoff.");
            var s=run.Snapshot();
            if(s.loading||(s.phase!=ExpeditionPhase.Town&&s.phase!=ExpeditionPhase.Victory)||
                !s.members.Any(p=>p.connected&&p.alive&&!p.extracted)||s.members.Any(p=>p.connected&&p.alive&&!p.extracted&&!p.safe))
                throw new InvalidOperationException("Checkpoint requires the living party at the safe location.");
            return new ExpeditionSavepoint{checkpoint=s,extracted=s.members.Where(p=>p.extracted).Select(p=>p.account).ToArray()};
        }
        public bool Valid()=>schema==1&&ExpeditionValidation.Valid(checkpoint)&&extracted!=null&&extracted.Length<=ExpeditionValidation.MaxMembers&&
            extracted.All(ExpeditionValidation.Id)&&extracted.Distinct().Count()==extracted.Length&&
            (checkpoint.phase==ExpeditionPhase.Town||checkpoint.phase==ExpeditionPhase.Victory)&&!checkpoint.loading&&
            !checkpoint.members.Any(p=>p.connected&&p.alive&&!p.extracted&&!p.safe)&&
            checkpoint.members.Where(p=>p.extracted).All(p=>extracted.Contains(p.account));

        public ExpeditionSavepoint Copy()=>new ExpeditionSavepoint{schema=schema,checkpoint=checkpoint.Copy(),extracted=(string[])extracted.Clone()};

        /// <summary>Persist the returned archive before confirming extraction/rewards to clients.</summary>
        public ExpeditionSavepoint WithExtractions(ExpeditionSnapshot latest)
        {
            if(!Valid()||!ExpeditionValidation.Valid(latest)||latest.run!=checkpoint.run||latest.seed!=checkpoint.seed||latest.world!=checkpoint.world||latest.revision<checkpoint.revision)
                throw new ArgumentException("Extraction journal does not belong to this checkpoint.");
            var result=Copy();result.extracted=extracted.Concat(latest.members.Where(p=>p.extracted).Select(p=>p.account)).Distinct().ToArray();
            if(result.extracted.Length>ExpeditionValidation.MaxMembers)throw new InvalidOperationException("Extraction journal is full.");return result;
        }

        public NativeExpedition Restore(string host)
        {
            if(!Valid()||!checkpoint.members.Any(p=>p.account==host&&!p.extracted)||extracted.Contains(host))throw new ArgumentException("Resume host must belong to this checkpoint and must not have extracted.");
            var s=checkpoint.Copy();s.host=host;s.loading=false;s.exitAt=s.extractAt=-1;s.exitVotes=s.extractVotes=s.continueVotes=Array.Empty<string>();
            if(s.epoch==int.MaxValue)throw new InvalidOperationException("Authority epoch exhausted.");s.epoch++;
            foreach(var p in s.members){p.carriedBy=null;p.connected=p.account==host;p.safe=p.alive;p.extracted|=extracted.Contains(p.account);if(p.extracted)p.connected=p.alive=p.safe=false;}
            // A newcomer who extracted after this checkpoint still cannot rejoin its rollback.
            foreach(string account in extracted.Where(a=>!s.members.Any(p=>p.account==a)))
                s.members=s.members.Concat(new[]{new ExpeditionMember{account=account,weapon="extracted",extracted=true,alive=false,safe=false}}).ToArray();
            return new NativeExpedition(s);
        }
    }

    /// <summary>Local desktop persistence. Codec injected so Core stays independent of Unity/System.Text.Json.
    /// SHA-256 detects corruption, not malicious save editing. Backend progression must not trust these files.</summary>
    public sealed class NativeExpeditionSaveStore
    {
        private readonly string path;
        private readonly Func<ExpeditionSavepoint,string> encode;
        private readonly Func<string,ExpeditionSavepoint> decode;
        private const int MaxBytes=8*1024*1024;
        public bool Exists=>File.Exists(path);
        public NativeExpeditionSaveStore(string directory,string run,Func<ExpeditionSavepoint,string> encode,Func<string,ExpeditionSavepoint> decode)
        {
            if(string.IsNullOrWhiteSpace(directory)||!ExpeditionValidation.Id(run)||run=="."||run==".."||run.Contains(":"))throw new ArgumentException("Invalid save location.");
            this.encode=encode??throw new ArgumentNullException(nameof(encode));this.decode=decode??throw new ArgumentNullException(nameof(decode));
            path=Path.Combine(directory,run+".expedition");
        }
        private static string Digest(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
        public void Write(ExpeditionSavepoint save)
        {
            if(save==null||!save.Valid()||Path.GetFileName(path)!=save.checkpoint.run+".expedition")throw new ArgumentException("Invalid checkpoint.");
            string payload=encode(save.Copy()),text=Digest(payload)+"\n"+payload;
            if(Encoding.UTF8.GetByteCount(text)>MaxBytes)throw new InvalidOperationException("Checkpoint exceeds size limit.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
            using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            {var bytes=Encoding.UTF8.GetBytes(text);file.Write(bytes,0,bytes.Length);file.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
        }
        private ExpeditionSavepoint Read(string file)
        {
            if(!File.Exists(file)||new FileInfo(file).Length>MaxBytes)return null;
            string text=File.ReadAllText(file);int separator=text.IndexOf('\n');if(separator!=64)return null;
            string payload=text.Substring(separator+1);if(text.Substring(0,separator)!=Digest(payload))return null;
            var save=decode(payload);return save!=null&&save.Valid()&&Path.GetFileName(path)==save.checkpoint.run+".expedition"?save:null;
        }
        public ExpeditionSavepoint Read()
        {
            // Fail closed: silently restoring an older backup could resurrect extracted accounts.
            // Keep .bak for explicit recovery tooling, never treat it as the latest extraction journal.
            try{return Read(path);}catch(Exception e)when(e is IOException||e is ArgumentException||e is FormatException){return null;}
        }
    }
}
