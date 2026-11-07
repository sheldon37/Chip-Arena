using System;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace ChipArena.UnityConversion.Editor
{
    // Used by the real player packager and the Windows integration check. No Unity API dependency.
    public static class NativeTunnelBuild
    {
        public static void Stage(string project,string player)
        {
            string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChipArena","BuildCache","cloudflared",NativeTunnelBinary.Version);
            Directory.CreateDirectory(cache);string source=Path.Combine(cache,"cloudflared.exe");
            bool valid=false;try{NativeTunnelBinary.Verify(source);valid=true;}catch(NativeOnlineRoomException){}
            if(!valid)
            {
                string partial=source+"."+Guid.NewGuid().ToString("N")+".part";
                try
                {
                    using(var http=new HttpClient())using(var stop=new CancellationTokenSource(TimeSpan.FromMinutes(3)))
                    using(var response=http.GetAsync(NativeTunnelBinary.DownloadUrl,HttpCompletionOption.ResponseHeadersRead,stop.Token).GetAwaiter().GetResult())
                    {
                        response.EnsureSuccessStatusCode();
                        using(var input=response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())using(var output=File.Create(partial))
                        {
                            byte[] buffer=new byte[65536];long count=0;int n;
                            while((n=input.ReadAsync(buffer,0,buffer.Length,stop.Token).GetAwaiter().GetResult())>0)
                            {count+=n;if(count>NativeTunnelBinary.WindowsBytes)throw new IOException("Online connector download exceeds its pinned size.");output.Write(buffer,0,n);}
                            if(count!=NativeTunnelBinary.WindowsBytes)throw new IOException("Online connector download was incomplete.");
                        }
                    }
                    NativeTunnelBinary.Verify(partial);File.Copy(partial,source,true);
                }
                catch(Exception e){throw new IOException("Could not prepare the verified online connector. Check internet access to GitHub, then rebuild. "+e.Message,e);}
                finally{if(File.Exists(partial))File.Delete(partial);}
            }
            string target=Path.Combine(player,NativeTunnelBinary.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source,target,true);NativeTunnelBinary.Verify(target);
            File.Copy(Path.Combine(project,"tools","cloudflared-LICENSE.txt"),Path.Combine(Path.GetDirectoryName(target),"cloudflared-LICENSE.txt"),true);
        }
    }
}
