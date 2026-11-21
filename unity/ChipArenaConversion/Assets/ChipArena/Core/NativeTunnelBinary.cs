using System;
using System.IO;
using System.Security.Cryptography;

namespace ChipArena.UnityConversion
{
    public static class NativeTunnelBinary
    {
        // Same official connector version already shipped with Electron. Never execute an unverified download.
        public const string Version="2026.9.3";
        public const string RelativePath="runtime/cloudflared.exe";
        public const string WindowsSha256="f096265ec2fcbe9bb6e2d64268db167ced3fcbb83d894bdb9e2fcdb26f2ea7e2";
        public const string LinuxSha256="77e26d8d900e0b8469f416239d14b5f296525fdf79fee6f511ef55609e3fbac2";
        public const long WindowsBytes=55366080;
        public const string DownloadUrl="https://github.com/cloudflare/cloudflared/releases/download/"+Version+"/cloudflared-windows-amd64.exe";
        public static void Verify(string path,bool linuxTest=false)
        {
            if(!File.Exists(path))throw new NativeOnlineRoomException("Online hosting files are missing. Update or reinstall Chip Arena.");
            using(var file=File.OpenRead(path))using(var hash=SHA256.Create())
            {
                string actual=BitConverter.ToString(hash.ComputeHash(file)).Replace("-","").ToLowerInvariant();
                if(actual!=(linuxTest?LinuxSha256:WindowsSha256))throw new NativeOnlineRoomException("Online hosting files failed verification. Update or reinstall Chip Arena.");
            }
        }
    }
}
