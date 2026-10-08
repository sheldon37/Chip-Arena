using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ChipArena.UnityConversion;

namespace ChipArena.NativeUpdater
{
    internal static class Launcher
    {
        private static string root,work;
        [STAThread] private static int Main(string[] args)
        {
            root=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);work=UpdateStorage.Work(root);
            try
            {
                var installer=new UpdateInstaller(root,Log);
                using(installer.Lock())
                {
                    if(args.Length==0)
                    {
                        if(File.Exists(Path.Combine(work,"journal.json"))&&AnyGameRunning())throw new IOException("Close the open native game before the launcher recovers its interrupted update.");
                        installer.Recover();UpdateStorage.Build(Path.Combine(root,"app"));StartGame(null);return 0;
                    }
                    if(args.Length!=2||args[0]!="--install"||!NativeUpdateRules.IsToken(args[1]))throw new InvalidOperationException("Invalid launcher command.");
                    string transaction=UpdateStorage.Transaction(root,args[1]);
                    var request=UpdateStorage.Read<NativeUpdateRequest>(Path.Combine(transaction,"request.json"));
                    if(request.token!=args[1])throw new InvalidDataException("Update request identity differs.");
                    Process started=null;
                    try
                    {
                        installer.Prepare(request);
                        string ready=Path.Combine(transaction,"ready.txt");File.WriteAllText(ready+".tmp",request.token);File.Move(ready+".tmp",ready);
                        WaitForParent(request);
                        if(AnyGameRunning())throw new IOException("Close the other native game windows, then retry the update.");
                        installer.Swap(request);started=StartGame(request.token);
                        string ack=Path.Combine(transaction,"startup.txt");var deadline=DateTime.UtcNow.AddSeconds(90);
                        while(DateTime.UtcNow<deadline&&!started.HasExited)
                        {
                            if(File.Exists(ack)&&File.ReadAllText(ack).Trim()==request.token+"/"+request.manifest.build.version)
                            {installer.Confirm(request);return 0;}
                            Thread.Sleep(100);
                        }
                        throw new IOException("The updated game did not finish startup. Restoring the previous version.");
                    }
                    catch(Exception e)
                    {
                        if(started!=null&&!started.HasExited)
                        {started.CloseMainWindow();if(!started.WaitForExit(5000)){started.Kill();started.WaitForExit(5000);}}
                        installer.Failed(request,e.Message);
                        if(!AnyGameRunning())StartGame(null);
                        throw;
                    }
                    finally{if(started!=null)started.Dispose();}
                }
            }
            catch(Exception e)
            {
                try{Log(e.ToString());}catch{}
                // A visible, actionable error when no game remains; never silently report success.
                if(Environment.GetEnvironmentVariable("CHIP_UPDATER_TEST_NO_DIALOG")!="1")MessageBox.Show(e.Message+"\n\nDetails: "+Path.Combine(work,"updater.log"),"Chip Arena update",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
            }
        }
        private static void Log(string message)
        {Directory.CreateDirectory(work);File.AppendAllText(Path.Combine(work,"updater.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);}
        private static Process StartGame(string token)
        {
            string app=Path.Combine(root,"app");
            return Process.Start(new ProcessStartInfo{FileName=Path.Combine(app,NativeUpdateRules.Executable),WorkingDirectory=app,
                Arguments=token==null?"":"--native-update-token "+token,UseShellExecute=false});
        }
        private static void WaitForParent(NativeUpdateRequest request)
        {
            if(request.parentPid<=0||request.parentStartedUtcTicks<=0)throw new InvalidDataException("The update has no valid requesting game process.");
            try
            {
                using(var parent=Process.GetProcessById(request.parentPid))
                {
                    if(parent.StartTime.ToUniversalTime().Ticks!=request.parentStartedUtcTicks)return;
                    string expected=LongPath(Path.Combine(root,"app",NativeUpdateRules.Executable));
                    try
                    {if(!string.Equals(LongPath(parent.MainModule.FileName),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The requesting process is not this native game.");}
                    catch(InvalidOperationException){if(parent.HasExited)return;throw;}
                    if(!parent.WaitForExit(120000))throw new IOException("The game did not close; its files were left unchanged.");
                }
            }
            catch(ArgumentException){} // It already exited. Never kill a PID supplied in the request.
        }
        private static bool AnyGameRunning()
        {
            // Recovery may resume between moving app to backup and moving the staged player in.
            if(!Directory.Exists(Path.Combine(root,"app")))return false;
            string expected=LongPath(Path.Combine(root,"app",NativeUpdateRules.Executable));
            foreach(var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(NativeUpdateRules.Executable)))
            using(p)
            {
                try{if(string.Equals(LongPath(p.MainModule.FileName),expected,StringComparison.OrdinalIgnoreCase))return true;}
                catch(System.ComponentModel.Win32Exception){throw new IOException("Cannot verify that other native game instances have closed.");}
                catch(InvalidOperationException){}
            }
            return false;
        }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern uint GetLongPathName(string path,StringBuilder buffer,uint length);
        private static string LongPath(string path)
        {
            var buffer=new StringBuilder(32768);uint length=GetLongPathName(Path.GetFullPath(path),buffer,(uint)buffer.Capacity);
            if(length==0||length>=buffer.Capacity)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return buffer.ToString();
        }
    }
}
