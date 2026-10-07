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
    public sealed partial class ConversionGame
    {
        private string adminQuery="";private bool showAdmin;private NativeAccountData[] managedPlayers=Array.Empty<NativeAccountData>();
        private readonly Dictionary<string,HashSet<string>> editedGrants=new Dictionary<string,HashSet<string>>(),editedFlags=new Dictionary<string,HashSet<string>>();
        private static readonly string[] Grantable={"weapon:siren","weapon:emperor","weapon:leet","weapon:ansatsuken","intro:dev-siren","intro:dev-emperor","intro:dev-matrix","intro:dev-ansatsuken","hat:auditor-halo","effect:dev-singularity","effect:dev-raging-demon","effect:dev-clown","effect:dev-clown-love","effect:dev-bass-van"};
        private void AdminPanel()
        {
            if(!account.Developer)return;if(GUILayout.Button(showAdmin?"Close player management":"Developer · manage players",button))showAdmin=!showAdmin;if(!showAdmin)return;
            adminQuery=GUILayout.TextField(adminQuery,32);GUI.enabled=!account.Busy;if(GUILayout.Button("Search registered players",button))account.Manage("players",JsonUtility.ToJson(new NativeAdminQuery{query=adminQuery}),response=>{managedPlayers=(response.players??Array.Empty<NativeAccountData>()).Where(p=>p!=null&&!string.IsNullOrEmpty(p.id)).Take(100).ToArray();editedGrants.Clear();editedFlags.Clear();});
            foreach(var p in managedPlayers)
            {
                GUILayout.Label(p.username+(p.developer?" · Developer":""),label);if(!editedGrants.TryGetValue(p.id,out var grants)){grants=new HashSet<string>(p.unlocks??Array.Empty<string>());editedGrants[p.id]=grants;}if(!editedFlags.TryGetValue(p.id,out var flags)){flags=new HashSet<string>(p.flags??Array.Empty<string>());editedFlags[p.id]=flags;}
                foreach(var key in Grantable){bool selected=GUILayout.Toggle(grants.Contains(key),key);if(selected)grants.Add(key);else grants.Remove(key);}foreach(var flag in new[]{"playtester","vip"}){bool selected=GUILayout.Toggle(flags.Contains(flag),flag);if(selected)flags.Add(flag);else flags.Remove(flag);}
                if(GUILayout.Button("Save grants for "+p.username,button))account.Manage("grant",JsonUtility.ToJson(new NativeGrantRequest{accountId=p.id,grantVersion=p.grantVersion,unlocks=grants.ToArray(),flags=flags.ToArray()}),response=>{if(response.account?.id!=p.id)return;p.grantVersion=response.account.grantVersion;p.unlocks=response.account.unlocks;p.flags=response.account.flags;});
            }GUI.enabled=true;
        }
    }
}
