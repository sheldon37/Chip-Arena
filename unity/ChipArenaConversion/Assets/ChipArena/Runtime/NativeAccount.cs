using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeCosmetics
    {public bool overrideWeaponIntro;public string lobbyPose="weapon",preferredSpec="pistol",intro="neon-drop",introText="Ready to make an impact",bodyColor="#e5e7eb",hat="none",killEffect="impact-ragdoll";public string lobbyBack;}
    [Serializable] public sealed class NativeProfile {public string name="Pilot",@class="ranged",color="#70e1d0";public NativeCosmetics cosmetics=new NativeCosmetics();}
    [Serializable] public sealed class NativeAccountData {public string id,username;public NativeProfile profile;public bool hasProfile,developer;public string[] unlocks,flags;public int revision,grantVersion;}
    [Serializable] public sealed class NativeAccountResponse {public string error,token,session,recoveryCode;public NativeAccountData account;public NativeAccountData[] players;}
    [Serializable] public sealed class NativeLoginRequest {public string username,password,recoveryCode;public bool remember;}
    [Serializable] public sealed class NativeProfileRequest {public int revision;public NativeProfile profile;}
    public sealed partial class NativeAccount:MonoBehaviour
    {
        public const string DefaultOrigin="https://chip-arena-prototype.sheldon37.chatgpt.site";
        public string Origin{get;private set;}="";public string Token{get;private set;}="";public NativeAccess Access{get;private set;}=NativeAccess.Guest;
        private const string OfflineStatus="Signed out / offline";
        public NativeAccountData Account{get;private set;}public string RecoveryCode{get;private set;}="";public string Status{get;private set;}=OfflineStatus;public bool Busy{get;private set;}public Action<NativeAccountData,bool> Accepted;
        private NativeProfile pendingProfile;private bool reloadProfile;private string session="";private bool remember;private float refreshAt;
        private string SessionPath=>Path.Combine(Application.persistentDataPath,"conversion-account.bin");
        public static string ValidateOrigin(string value)
        {if(!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||uri.AbsolutePath!="/"||uri.Scheme!="https"&&!(uri.Scheme=="http"&&uri.IsLoopback))throw new ArgumentException("Use an HTTPS account service origin, or HTTP on localhost for development.");return uri.GetLeftPart(UriPartial.Authority);}
        public void Configure(string origin,bool loadRemembered=false)
        {
            string next=string.IsNullOrWhiteSpace(origin)?"":ValidateOrigin(origin);if(Origin!=next){StopAllCoroutines();Busy=false;Clear(false);Origin=next;}
            if(loadRemembered&&Origin.Length>0&&File.Exists(SessionPath))try{string value=Encoding.UTF8.GetString(NativeSessionVault.Unprotect(File.ReadAllBytes(SessionPath),Origin));if(ValidSession(value)){session=value;remember=true;Refresh();}}catch(Exception e)when(e is IOException||e is System.Security.Cryptography.CryptographicException||e is NotSupportedException){Status="Saved sign-in could not be opened; sign in again.";}
        }
        private static bool ValidSession(string value)=>value!=null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^[A-Za-z0-9_-]{43}$");
        public void Login(string action,string username,string password,string recovery,bool save)
        {if(action!="login"&&action!="register"&&action!="recover")throw new ArgumentException(nameof(action));remember=save;BeginRequest(action,JsonUtility.ToJson(new NativeLoginRequest{username=username,password=password,recoveryCode=recovery,remember=save}),true);}
        public void Refresh(){if(session.Length>0)BeginRequest("me",null,false);}
        public void ReloadSavedProfile(){if(Account==null)return;reloadProfile=true;refreshAt=0;Refresh();}
        public void Save(NativeProfile profile){if(Account==null)return;if(Busy){pendingProfile=profile;return;}BeginRequest("profile",JsonUtility.ToJson(new NativeProfileRequest{revision=Account.revision,profile=profile}),false);}
        public void Logout(){if(session.Length==0){Clear();return;}BeginRequest("logout","{}",false);}
        private void BeginRequest(string action,string body,bool authenticating)
        {if(Busy)return;if(Origin.Length==0){Status=OfflineStatus;return;}Busy=true;refreshAt=Time.unscaledTime+60;Status="Connecting…";StartCoroutine(Request(action,body,authenticating));}
        private IEnumerator Request(string action,string body,bool authenticating)
        {
            using(var request=new UnityWebRequest(Origin+"/api/accounts/"+action,body==null?"GET":"POST"))
            {
                request.redirectLimit=0;request.timeout=action=="me"?5:20;request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Cache-Control","no-store");
                if(body!=null)request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));if(!authenticating&&session.Length>0)request.SetRequestHeader("Authorization","Bearer "+session);
                yield return request.SendWebRequest();Busy=false;
                if(action=="logout"){Clear();yield break;}
                if(request.downloadedBytes>131072){Status="Invalid account response.";yield break;}
                NativeAccountResponse response=null;try{response=JsonUtility.FromJson<NativeAccountResponse>(request.downloadHandler.text);}catch(ArgumentException){}
                if(request.result!=UnityWebRequest.Result.Success){if(request.responseCode==401)Clear();Status=response?.error??"Account service could not be reached.";refreshAt=Time.unscaledTime+60;yield break;}
                var access=NativeAccess.Verify(response?.token,s=>JsonUtility.FromJson<NativePlayerClaim>(s),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if(response?.account==null||string.IsNullOrEmpty(access.AccountId)||access.AccountId!=response.account.id||response.session!=null&&response.session.Length>0&&!ValidSession(response.session)){Status="Account response could not be verified.";yield break;}
                bool loadProfile=authenticating||Account==null||reloadProfile&&action=="me";if(action=="me")reloadProfile=false;Access=access;Token=response.token;Account=response.account;if(!string.IsNullOrEmpty(response.recoveryCode))RecoveryCode=response.recoveryCode;if(!string.IsNullOrEmpty(response.session))session=response.session;
                Status="Signed in as "+Account.username;refreshAt=Time.unscaledTime+300;
                try{if(remember&&session.Length>0){var protectedBytes=NativeSessionVault.Protect(Encoding.UTF8.GetBytes(session),Origin);Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllBytes(SessionPath+".new",protectedBytes);if(File.Exists(SessionPath))File.Delete(SessionPath);File.Move(SessionPath+".new",SessionPath);}else if(File.Exists(SessionPath))File.Delete(SessionPath);}
                catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is System.Security.Cryptography.CryptographicException||e is NotSupportedException){remember=false;Status+=" · sign-in kept for this session only";}
                Accepted?.Invoke(Account,loadProfile);
            }
        }
        private void Update(){if(Account==null||Busy)return;if(pendingProfile!=null){var profile=pendingProfile;pendingProfile=null;Save(profile);}else if(Time.unscaledTime>=refreshAt)Refresh();}
        public void Clear(bool erase=true){pendingProfile=null;reloadProfile=false;Account=null;Token=session=RecoveryCode="";Access=NativeAccess.Guest;Status=OfflineStatus;if(erase)try{if(File.Exists(SessionPath))File.Delete(SessionPath);}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){Status="Signed out; saved session could not be removed.";}}
    }
    public static class NativeSessionVault
    {
        [StructLayout(LayoutKind.Sequential)] private struct Blob {public int length;public IntPtr data;}
        [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool CryptProtectData(ref Blob input,string description,ref Blob entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,ref Blob entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        public static byte[] Protect(byte[] value,string origin)=>Transform(value,origin,true);
        public static byte[] Unprotect(byte[] value,string origin)=>Transform(value,origin,false);
        private static byte[] Transform(byte[] value,string origin,bool protect)
        {
            if(Application.platform!=RuntimePlatform.WindowsPlayer&&Application.platform!=RuntimePlatform.WindowsEditor)throw new NotSupportedException("Remembered sessions require Windows DPAPI.");
            byte[] salt=Encoding.UTF8.GetBytes("ChipArenaUnity/account/v1/"+origin);var input=new Blob{length=value.Length,data=Marshal.AllocHGlobal(value.Length)};var entropy=new Blob{length=salt.Length,data=Marshal.AllocHGlobal(salt.Length)};var output=new Blob();
            try{Marshal.Copy(value,0,input.data,value.Length);Marshal.Copy(salt,0,entropy.data,salt.Length);bool ok=protect?CryptProtectData(ref input,"Chip Arena Unity account",ref entropy,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,ref entropy,IntPtr.Zero,IntPtr.Zero,1,out output);if(!ok)throw new System.Security.Cryptography.CryptographicException(Marshal.GetLastWin32Error());var data=new byte[output.length];Marshal.Copy(output.data,data,0,data.Length);return data;}
            finally{for(int i=0;i<input.length;i++)Marshal.WriteByte(input.data,i,0);Marshal.FreeHGlobal(input.data);Marshal.FreeHGlobal(entropy.data);if(output.data!=IntPtr.Zero)LocalFree(output.data);}
        }
    }
}
