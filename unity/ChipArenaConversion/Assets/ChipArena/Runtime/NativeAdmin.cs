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
        private string adminQuery="";private NativeAccountData[] managedPlayers=Array.Empty<NativeAccountData>();
        private readonly Dictionary<string,HashSet<string>> editedGrants=new Dictionary<string,HashSet<string>>(),editedFlags=new Dictionary<string,HashSet<string>>();
        private static readonly string[] Grantable={"weapon:siren","weapon:emperor","weapon:leet","weapon:ansatsuken","intro:dev-siren","intro:dev-emperor","intro:dev-matrix","intro:dev-ansatsuken","hat:auditor-halo","effect:dev-singularity","effect:dev-raging-demon","effect:dev-clown","effect:dev-clown-love","effect:dev-bass-van"};
        // DEV Menu (main.ts dev-menu): find registered players by username prefix and edit their restricted items and flags.
        private void AdminBody()
        {
            if(!account.Developer)return;Fine("Grant restricted weapons, intros, headwear and effects, and set player flags. Players refresh their account and rejoin to receive changes.");
            Field("Username prefix");adminQuery=GUILayout.TextField(adminQuery,32);GUI.enabled=!account.Busy;if(NativeControllerPointer.Button("Find players",button))account.Manage("players",JsonUtility.ToJson(new NativeAdminQuery{query=adminQuery}),response=>{managedPlayers=(response.players??Array.Empty<NativeAccountData>()).Where(p=>p!=null&&!string.IsNullOrEmpty(p.id)).Take(100).ToArray();editedGrants.Clear();editedFlags.Clear();});
            GUILayout.Label(account.Status,small);if(managedPlayers.Length==0)Fine("Search by the player's registered username.");
            foreach(var p in managedPlayers)
            {
                GUILayout.Space(10);GUILayout.Label(p.username+(p.developer?" · DEV":""),heading);if(!editedGrants.TryGetValue(p.id,out var grants)){grants=new HashSet<string>(p.unlocks??Array.Empty<string>());editedGrants[p.id]=grants;}if(!editedFlags.TryGetValue(p.id,out var flags)){flags=new HashSet<string>(p.flags??Array.Empty<string>());editedFlags[p.id]=flags;}
                Field("Restricted items");foreach(var key in Grantable){bool selected=NativeControllerPointer.Toggle(grants.Contains(key),NativeCosmeticRules.Label(key));if(selected)grants.Add(key);else grants.Remove(key);}
                Field("Player flags");foreach(var flag in new[]{"playtester","vip"}){bool selected=NativeControllerPointer.Toggle(flags.Contains(flag),NativeCosmeticRules.Label(flag));if(selected)flags.Add(flag);else flags.Remove(flag);}
                if(NativeControllerPointer.Button("Save grants for "+p.username,button))account.Manage("grant",JsonUtility.ToJson(new NativeGrantRequest{accountId=p.id,grantVersion=p.grantVersion,unlocks=grants.ToArray(),flags=flags.ToArray()}),response=>{if(response.account?.id!=p.id)return;p.grantVersion=response.account.grantVersion;p.unlocks=response.account.unlocks;p.flags=response.account.flags;});
            }GUI.enabled=true;
        }
    }
}
