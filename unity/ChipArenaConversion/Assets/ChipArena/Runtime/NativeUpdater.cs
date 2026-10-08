using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ChipArena.UnityConversion
{
    public enum NativeUpdateState { Unavailable,Idle,Checking,Current,Available,Downloading,Verifying,Preparing,Restarting,Error }
    public sealed class NativeUpdater : MonoBehaviour
    {
        public NativeUpdateState State { get;private set; }=NativeUpdateState.Unavailable;
        public string Status { get;private set; }="Updates are available in the Windows starter build.";
        public float Progress { get;private set; }
        public NativeBuildIdentity Installed { get;private set; }
        public NativeBuildIdentity Available { get { return manifest==null?null:manifest.build; } }
        public bool Busy { get { return State==NativeUpdateState.Checking||State==NativeUpdateState.Downloading||State==NativeUpdateState.Verifying||State==NativeUpdateState.Preparing||State==NativeUpdateState.Restarting; } }
        public bool CanCancel { get { return State==NativeUpdateState.Checking||State==NativeUpdateState.Downloading; } }
        public bool CanInstall { get { return State==NativeUpdateState.Available&&canInstall!=null&&canInstall(); } }
        private string root,transaction;private Func<bool> canInstall;private Action beforeQuit;
        private NativeUpdateManifest manifest;private bool cancelled;private UnityWebRequest active;
        private IEnumerator running;private FileStream downloadFile,downloadLock;

        public void Initialize(Func<bool> installationAllowed,Action saveBeforeQuit)
        {
            canInstall=installationAllowed;beforeQuit=saveBeforeQuit;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            string app=Path.GetFullPath(Path.Combine(Application.dataPath,".."));root=Path.GetFullPath(Path.Combine(app,".."));
            try
            {
                if(Path.GetFileName(app)!="app"||!File.Exists(Path.Combine(root,NativeUpdateRules.Executable))||File.ReadAllText(Path.Combine(root,"native-install.txt")).Trim()!=NativeUpdateRules.Product+"/1")
                    throw new InvalidOperationException("Install the Windows starter ZIP once to enable updates.");
                NativeUpdateRules.RejectReparsePoints(root);
                Installed=JsonUtility.FromJson<NativeBuildIdentity>(File.ReadAllText(Path.Combine(app,NativeUpdateRules.IdentityFile)));NativeUpdateRules.ValidateBuild(Installed);
                AcknowledgeStartup();State=NativeUpdateState.Idle;Status="Check for a Unity update.";CheckForUpdates();
            }
            catch(Exception e){State=NativeUpdateState.Unavailable;Status=e.Message;}
#endif
        }
        private void AcknowledgeStartup()
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="--native-update-token")
            {
                string token=args[i+1];if(!NativeUpdateRules.IsToken(token))throw new InvalidDataException("Invalid update startup token.");
                string folder=Path.Combine(root,NativeUpdateRules.WorkDirectory,token);NativeUpdateRules.RejectReparsePoints(folder);
                var request=JsonUtility.FromJson<NativeUpdateRequest>(File.ReadAllText(Path.Combine(folder,"request.json")));
                if(request==null||request.token!=token||request.manifest==null||!NativeUpdateRules.SameBuild(Installed,request.manifest.build))throw new InvalidDataException("Update startup identity differs.");
                string receipt=Path.Combine(folder,"startup.txt");File.WriteAllText(receipt+".tmp",token+"/"+Installed.version);File.Move(receipt+".tmp",receipt);return;
            }
        }
        public void CheckForUpdates()
        {if(Busy||Installed==null)return;StartOperation(Check());}
        public void InstallAvailableUpdate()
        {if(!CanInstall)return;StartOperation(Download());}
        public void Cancel(){if(CanCancel){cancelled=true;if(active!=null)active.Abort();}}
        private void StartOperation(IEnumerator routine)
        {cancelled=false;Progress=0;running=routine;StartCoroutine(Guard(routine));}
        private IEnumerator Guard(IEnumerator routine)
        {
            try
            {
                while(true)
                {
                    bool more=false;Exception failure=null;
                    try{more=routine.MoveNext();}catch(Exception e){failure=e;}
                    if(failure!=null)
                    {
                        State=cancelled?NativeUpdateState.Idle:NativeUpdateState.Error;Status=cancelled?"Update cancelled. Your installed version is unchanged.":failure.Message;
                        yield break;
                    }
                    if(!more)yield break;yield return routine.Current;
                }
            }
            finally{(routine as IDisposable)?.Dispose();running=null;active=null;downloadFile=null;downloadLock=null;}
        }
        private IEnumerator Check()
        {
            State=NativeUpdateState.Checking;Status="Checking for Unity updates…";manifest=null;
            using(var bytes=new MemoryStream())
            using(var request=new UnityWebRequest(NativeUpdateRules.Feed+"latest.json",UnityWebRequest.kHttpVerbGET))
            {
                var handler=new BoundedDownload(bytes,NativeUpdateRules.MaxManifestBytes);request.downloadHandler=handler;request.timeout=20;request.redirectLimit=0;active=request;
                var op=request.SendWebRequest();while(!op.isDone){if(cancelled)throw new OperationCanceledException();yield return null;}
                if(cancelled)throw new OperationCanceledException();
                if(request.responseCode==404){State=NativeUpdateState.Idle;Status="No Unity update has been published yet.";yield break;}
                CheckResponse(request,handler);var json=new UTF8Encoding(false,true).GetString(bytes.ToArray());
                manifest=JsonUtility.FromJson<NativeUpdateManifest>(json);NativeUpdateRules.ValidateManifest(manifest);
            }
            if(NativeUpdateRules.IsNewer(Installed,manifest.build))
            {State=NativeUpdateState.Available;Status="Update available · "+Short(manifest.build.version)+" · "+(manifest.bytes/(1024d*1024)).ToString("0.0")+" MB";}
            else{State=NativeUpdateState.Current;Status=Installed.version==manifest.build.version?"You have the latest published Unity build.":"Your installed build is newer than the published update.";}
        }
        private IEnumerator Download()
        {
            NativeUpdateRules.ValidateManifest(manifest);
            if(!canInstall())throw new InvalidOperationException("Finish the match and leave the room before updating.");
            string work=Path.Combine(root,NativeUpdateRules.WorkDirectory);NativeUpdateRules.RejectReparsePoints(work);Directory.CreateDirectory(work);
            if(File.Exists(Path.Combine(work,"journal.json")))throw new IOException("Close the game and launch it through the starter to recover the preceding update.");
            using(downloadLock=new FileStream(Path.Combine(work,"download.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                string token=Guid.NewGuid().ToString("N");transaction=Path.Combine(work,token);Directory.CreateDirectory(transaction);
                string zip=Path.Combine(transaction,"download.zip"),partPath=Path.Combine(transaction,"part.tmp");bool handedOff=false;
                try
                {
                    long done=0;State=NativeUpdateState.Downloading;
                    using(var archive=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    foreach(var part in manifest.parts)
                    {
                        using(downloadFile=new FileStream(partPath,FileMode.Create,FileAccess.Write,FileShare.None))
                        using(var request=new UnityWebRequest(NativeUpdateRules.PartUrl(part),UnityWebRequest.kHttpVerbGET))
                        {
                            var handler=new BoundedDownload(downloadFile,part.bytes);request.downloadHandler=handler;request.timeout=300;request.redirectLimit=0;active=request;
                            var op=request.SendWebRequest();
                            while(!op.isDone)
                            {if(cancelled)throw new OperationCanceledException();Progress=(float)((done+handler.Bytes)/(double)manifest.bytes);Status="Downloading update · "+(Progress*100).ToString("0")+"%";yield return null;}
                            if(cancelled)throw new OperationCanceledException();CheckResponse(request,handler);downloadFile.Flush(true);
                        }
                        State=NativeUpdateState.Verifying;Status="Verifying downloaded files…";
                        var verify=Task.Run(()=>NativeUpdateRules.VerifyFile(partPath,part.bytes,part.sha256));
                        while(!verify.IsCompleted)yield return null;if(verify.IsFaulted)throw verify.Exception.GetBaseException();
                        using(var input=File.OpenRead(partPath))input.CopyTo(archive);File.Delete(partPath);done+=part.bytes;State=NativeUpdateState.Downloading;
                    }
                    State=NativeUpdateState.Verifying;Status="Verifying complete update…";Progress=1;
                    var whole=Task.Run(()=>NativeUpdateRules.VerifyFile(zip,manifest.bytes,manifest.sha256));while(!whole.IsCompleted)yield return null;if(whole.IsFaulted)throw whole.Exception.GetBaseException();
                    if(!canInstall())throw new InvalidOperationException("The update is ready, but a match or room is active. Leave it and retry.");
                    NativeUpdateRequest install;
                    using(var parent=Process.GetCurrentProcess())install=new NativeUpdateRequest{manifest=manifest,token=token,parentPid=parent.Id,parentStartedUtcTicks=parent.StartTime.ToUniversalTime().Ticks};
                    File.WriteAllText(Path.Combine(transaction,"request.json"),JsonUtility.ToJson(install));
                    State=NativeUpdateState.Preparing;Status="Preparing the update. The game will restart automatically…";
                    using(var helper=Process.Start(new ProcessStartInfo{FileName=Path.Combine(root,NativeUpdateRules.Executable),Arguments="--install "+token,WorkingDirectory=root,UseShellExecute=false}))
                    {
                        handedOff=true;float timeout=Time.realtimeSinceStartup+300;
                        while(!File.Exists(Path.Combine(transaction,"ready.txt")))
                        {
                            string error=Path.Combine(transaction,"error.txt");if(File.Exists(error))throw new IOException(File.ReadAllText(error));
                            if(helper.HasExited)throw new IOException("The updater could not prepare the installation. See .native-update/updater.log.");
                            if(Time.realtimeSinceStartup>timeout)throw new IOException("Update preparation timed out. Your installed game is unchanged.");yield return null;
                        }
                        if(File.ReadAllText(Path.Combine(transaction,"ready.txt"))!=token)throw new IOException("Update preparation receipt differs.");
                        if(!canInstall())throw new InvalidOperationException("A match or room became active. Close it before retrying the update.");
                        beforeQuit?.Invoke();State=NativeUpdateState.Restarting;Status="Restarting into the updated game…";Application.Quit();
                    }
                }
                finally
                {
                    if(!handedOff){if(File.Exists(partPath))File.Delete(partPath);if(File.Exists(zip))File.Delete(zip);}
                }
            }
        }
        public static string Short(string version){return string.IsNullOrEmpty(version)?"unknown":version.Substring(0,Math.Min(12,version.Length));}
        private static void CheckResponse(UnityWebRequest request,BoundedDownload handler)
        {if(handler.Failure!=null)throw new IOException(handler.Failure);if(request.result!=UnityWebRequest.Result.Success||request.responseCode!=200)throw new IOException("Could not download the Unity update. Check your connection and try again. (HTTP "+request.responseCode+")");}
        private void OnDestroy()
        {cancelled=true;if(active!=null)active.Abort();(running as IDisposable)?.Dispose();downloadFile?.Dispose();downloadLock?.Dispose();}
        private sealed class BoundedDownload : DownloadHandlerScript
        {
            private readonly Stream target;private readonly long limit;
            public long Bytes { get;private set; }public string Failure { get;private set; }
            public BoundedDownload(Stream stream,long maximum):base(new byte[65536]){target=stream;limit=maximum;}
            protected override bool ReceiveData(byte[] data,int length)
            {
                if(data==null||length<0||Bytes>limit-length){Failure="The update download exceeds its declared size.";return false;}
                try{target.Write(data,0,length);Bytes+=length;return true;}catch(Exception e){Failure=e.Message;return false;}
            }
        }
    }
}
