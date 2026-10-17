using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChipArena.UnityConversion
{
    public sealed class NativeTunnelProcess:IDisposable
    {
        private readonly Process process;private readonly string work;
        private readonly TaskCompletionSource<string> address=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> connected=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration cancellation;private IntPtr job;private int closed;
        public bool Closed=>Volatile.Read(ref closed)!=0;
        public string Origin{get;private set;}
        private NativeTunnelProcess(string executable,int port)
        {
            if(port<1||port>65535)throw new ArgumentOutOfRangeException(nameof(port));
            work=Path.Combine(Path.GetTempPath(),"ChipArenaTunnel",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
            string config=Path.Combine(work,"config.yml");File.WriteAllText(config,"no-autoupdate: true\n");
            var start=new ProcessStartInfo{FileName=Path.GetFullPath(executable),WorkingDirectory=work,UseShellExecute=false,
                CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
                Arguments="tunnel --config "+NativeUpdateRules.QuoteArgument(config)+" --no-autoupdate --edge-ip-version 4 --url tcp://127.0.0.1:"+port+" --metrics 127.0.0.1:0"};
            // A developer's named-tunnel settings must not change the game's isolated, accountless tunnel.
            foreach(string key in start.EnvironmentVariables.Keys.Cast<string>().ToArray())if(key.StartsWith("TUNNEL_",StringComparison.OrdinalIgnoreCase))start.EnvironmentVariables.Remove(key);
            process=new Process{StartInfo=start,EnableRaisingEvents=true};
            process.OutputDataReceived+=Log;process.ErrorDataReceived+=Log;
            process.Exited+=(_,__)=>{Interlocked.Exchange(ref closed,1);var error=new NativeOnlineRoomException("The online tunnel could not connect. Check your internet connection and try again.");address.TrySetException(error);connected.TrySetException(error);};
        }
        public static async Task<NativeTunnelProcess> Start(string executable,int port,CancellationToken token,bool linuxTest=false)
        {
            token.ThrowIfCancellationRequested();await Task.Run(()=>NativeTunnelBinary.Verify(executable,linuxTest),token).ConfigureAwait(false);
            var tunnel=new NativeTunnelProcess(executable,port);
            try
            {
                if(!tunnel.process.Start())throw new IOException("Could not launch the online connector.");
                if(Environment.OSVersion.Platform==PlatformID.Win32NT)tunnel.job=AttachLifetime(tunnel.process.Handle);
                tunnel.process.BeginOutputReadLine();tunnel.process.BeginErrorReadLine();
                tunnel.cancellation=token.Register(tunnel.StopProcess);
                var ready=Task.WhenAll(tunnel.address.Task,tunnel.connected.Task);
                if(await Task.WhenAny(ready,Task.Delay(90000,token)).ConfigureAwait(false)!=ready)
                {token.ThrowIfCancellationRequested();throw new NativeOnlineRoomException("Online hosting took too long to start. Check your connection and try again.");}
                await ready.ConfigureAwait(false);token.ThrowIfCancellationRequested();
                if(tunnel.Closed)throw new NativeOnlineRoomException("The online tunnel stopped. Please create the room again.");
                tunnel.Origin=tunnel.address.Task.Result;return tunnel;
            }
            catch{tunnel.Dispose();throw;}
        }
        private void Log(object sender,DataReceivedEventArgs e)
        {
            string line=e.Data;if(line==null||line.Length>4096)return;
            var match=Regex.Match(line,@"https://[a-z0-9-]+\.trycloudflare\.com(?=[\s|]|$)");
            if(match.Success)address.TrySetResult(NativeRoomDirectory.TunnelOrigin(match.Value));
            if(line.Contains("Registered tunnel connection"))connected.TrySetResult(true);
        }
        private void StopProcess()
        {
            Interlocked.Exchange(ref closed,1);
            try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(Win32Exception){}
            var handle=Interlocked.Exchange(ref job,IntPtr.Zero);if(handle!=IntPtr.Zero)CloseHandle(handle);
        }
        public void Dispose()
        {
            cancellation.Dispose();StopProcess();
            // Kill is asynchronous on Windows. Reap the child before releasing its executable to the updater.
            try{process.WaitForExit(2000);}catch(InvalidOperationException){}catch(Win32Exception){}
            process.Dispose();
            try{Directory.Delete(work,true);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        // Closing the game (including a crash) closes this job handle and kills only its connector child.
        private static IntPtr AttachLifetime(IntPtr child)
        {
            var handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)throw new Win32Exception();
            try
            {
                var limits=new JobLimits{Basic=new BasicLimits{Flags=0x2000}};
                if(!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf(typeof(JobLimits)))||!AssignProcessToJobObject(handle,child))throw new Win32Exception();
                return handle;
            }
            catch{CloseHandle(handle);throw;}
        }
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimits{public long ProcessTime,JobTime;public uint Flags;public UIntPtr MinWorkingSet,MaxWorkingSet;public uint ActiveProcesses;public UIntPtr Affinity;public uint Priority,Scheduling;}
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters{public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
        [StructLayout(LayoutKind.Sequential)] private struct JobLimits{public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateJobObject(IntPtr attributes,string name);
        [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(IntPtr job,int infoClass,ref JobLimits info,uint size);
        [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
        [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    }
}
