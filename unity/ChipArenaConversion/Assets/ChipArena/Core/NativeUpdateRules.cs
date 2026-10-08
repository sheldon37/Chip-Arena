using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // Shared by Unity and the stable Windows launcher. Keep compatible with .NET Framework/C# 5.
    [Serializable,DataContract] public sealed class NativeBuildIdentity
    {
        [DataMember] public int schema=1;
        [DataMember] public string product=NativeUpdateRules.Product,channel=NativeUpdateRules.Channel,platform=NativeUpdateRules.Platform;
        [DataMember] public string version;
        [DataMember] public long revision;
        [DataMember] public int bootstrap=1;
    }
    [Serializable,DataContract] public sealed class NativeUpdatePart
    {
        [DataMember] public string name,sha256;
        [DataMember] public long bytes;
    }
    [Serializable,DataContract] public sealed class NativeUpdateManifest
    {
        [DataMember] public int schema=1;
        [DataMember] public NativeBuildIdentity build;
        [DataMember] public string sha256;
        [DataMember] public long bytes,unpackedBytes;
        [DataMember] public int files;
        [DataMember] public NativeUpdatePart[] parts;
    }
    [Serializable,DataContract] public sealed class NativeUpdateRequest
    {
        [DataMember] public NativeUpdateManifest manifest;
        [DataMember] public string token;
        [DataMember] public int parentPid;
        [DataMember] public long parentStartedUtcTicks;
    }
    [Serializable,DataContract] public sealed class NativeUpdateJournal
    {
        [DataMember] public string token,phase;
        [DataMember] public NativeBuildIdentity before,after;
    }
    [Serializable,DataContract] public sealed class NativeUpdateResult
    {
        [DataMember] public string status,message,version;
    }
    public static class NativeUpdateRules
    {
        public const string Product="chip-arena-unity",Channel="native-test",Platform="windows-x64";
        public const string Executable="ChipArena-Native-Conversion-TEST.exe",DataDirectory="ChipArena-Native-Conversion-TEST_Data";
        public const string Feed="https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/native/windows-x64/";
        public const string WorkDirectory=".native-update",IdentityFile="native-build.json";
        public const int BootstrapVersion=1,MaxFiles=20000,MaxManifestBytes=128*1024;
        public const long MaxArchiveBytes=2L*1024*1024*1024,MaxUnpackedBytes=4L*1024*1024*1024,MaxPartBytes=32L*1024*1024;
        public static bool IsHash(string text){return text!=null&&Regex.IsMatch(text,"^[0-9a-f]{64}$");}
        public static bool IsToken(string text){return text!=null&&Regex.IsMatch(text,"^[0-9a-f]{32}$");}
        public static void ValidateBuild(NativeBuildIdentity b)
        {
            if(b==null||b.schema!=1||b.product!=Product||b.channel!=Channel||b.platform!=Platform||!IsHash(b.version)||b.revision<=0||b.bootstrap<1)
                throw new InvalidDataException("This is not a valid native Windows test build.");
            if(b.bootstrap>BootstrapVersion)throw new InvalidDataException("This update needs a newer launcher. Install the new starter ZIP.");
        }
        public static bool SameBuild(NativeBuildIdentity a,NativeBuildIdentity b)
        {return a!=null&&b!=null&&a.version==b.version&&a.revision==b.revision&&a.product==b.product&&a.channel==b.channel&&a.platform==b.platform&&a.bootstrap==b.bootstrap;}
        public static bool IsNewer(NativeBuildIdentity current,NativeBuildIdentity next)
        {ValidateBuild(current);ValidateBuild(next);return next.version!=current.version&&next.revision>current.revision;}
        public static void ValidateManifest(NativeUpdateManifest m)
        {
            if(m==null||m.schema!=1)throw new InvalidDataException("Unsupported native update manifest.");
            ValidateBuild(m.build);
            if(!IsHash(m.sha256)||m.bytes<=0||m.bytes>MaxArchiveBytes||m.unpackedBytes<=0||m.unpackedBytes>MaxUnpackedBytes||m.files<3||m.files>MaxFiles||m.parts==null||m.parts.Length<1||m.parts.Length>64)
                throw new InvalidDataException("Update size, file count or checksum is invalid.");
            long total=0;
            for(int i=0;i<m.parts.Length;i++)
            {
                var p=m.parts[i];
                if(p==null||p.name!=m.sha256+"-"+i.ToString("D4")+".part"||!IsHash(p.sha256)||p.bytes<=0||p.bytes>MaxPartBytes)
                    throw new InvalidDataException("Update part is invalid.");
                total=checked(total+p.bytes);
            }
            if(total!=m.bytes)throw new InvalidDataException("Update part sizes do not match the archive.");
        }
        public static string PartUrl(NativeUpdatePart part){return Feed+"objects/"+part.name;}
        public static string HashFile(string path)
        {using(var stream=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        public static void VerifyFile(string path,long bytes,string hash)
        {if(new FileInfo(path).Length!=bytes||HashFile(path)!=hash)throw new InvalidDataException("Downloaded update failed its size or SHA-256 check.");}
        public static string SafeRelativePath(string path)
        {
            if(string.IsNullOrEmpty(path)||path.Length>220||path[0]=='/'||path.IndexOf('\\')>=0)throw new InvalidDataException("Unsafe update path.");
            foreach(var part in path.Split('/'))
            {
                if(part.Length==0||part=="."||part==".."||part.EndsWith(".",StringComparison.Ordinal)||part.EndsWith(" ",StringComparison.Ordinal))throw new InvalidDataException("Unsafe update path.");
                foreach(char c in part)if(c<32||c==127||"<>:\"|?*".IndexOf(c)>=0)throw new InvalidDataException("Unsafe update filename.");
                string stem=part.Split('.')[0].ToUpperInvariant();
                if(Regex.IsMatch(stem,"^(CON|PRN|AUX|NUL|CONIN\\$|CONOUT\\$|CLOCK\\$|COM[0-9¹²³]|LPT[0-9¹²³])$"))throw new InvalidDataException("Reserved Windows filename in update.");
            }
            return path;
        }
        public static void RejectReparsePoints(string path)
        {
            var at=new DirectoryInfo(Path.GetFullPath(path));
            while(at!=null)
            {if(at.Exists&&(at.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Install the native game in a regular folder, outside junctions or symbolic links.");at=at.Parent;}
        }
        public static string QuoteArgument(string value)
        {
            // Windows CommandLineToArgvW escaping; arguments never pass through cmd.exe.
            var result=new StringBuilder("\"");int slashes=0;
            foreach(char c in value)
            {
                if(c=='\\'){slashes++;continue;}
                result.Append('\\',c=='\"'?slashes*2+1:slashes);result.Append(c);slashes=0;
            }
            result.Append('\\',slashes*2);result.Append('"');return result.ToString();
        }
    }
}
