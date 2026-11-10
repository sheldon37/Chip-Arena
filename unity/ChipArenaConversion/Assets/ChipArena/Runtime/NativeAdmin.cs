using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeAdminQuery {public string query;}
    [Serializable] public sealed class NativeGrantRequest {public string accountId;public int grantVersion;public string[] unlocks,flags;}
    public sealed partial class NativeAccount
    {
        public bool Developer=>Access.Developer&&Access.Expires>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public void Manage(string action,string body,Action<NativeAccountResponse> done)
        {if(!Developer||Busy||action!="players"&&action!="grant")return;Busy=true;StartCoroutine(AdminRequest(action,body,done));}
        private IEnumerator AdminRequest(string action,string body,Action<NativeAccountResponse> done)
        {
            using(var request=new UnityWebRequest(Origin+"/api/accounts/"+action,"POST"))
            {
                request.redirectLimit=0;request.timeout=20;request.downloadHandler=new DownloadHandlerBuffer();request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Authorization","Bearer "+session);yield return request.SendWebRequest();Busy=false;
                if(request.downloadedBytes>131072){Status="Invalid admin response.";yield break;}NativeAccountResponse response=null;try{response=JsonUtility.FromJson<NativeAccountResponse>(request.downloadHandler.text);}catch(ArgumentException){}
                if(request.result!=UnityWebRequest.Result.Success){Status=response?.error??"Admin request failed.";yield break;}if(response==null){Status="Invalid admin response.";yield break;}Status=action=="grant"?"Saved. Player should refresh their account and rejoin.":"Player search completed.";done?.Invoke(response);
            }
        }
    }
    // The DEV screen that uses these requests: Runtime/NativeDevGrantsUI.cs.
}
