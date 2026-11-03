using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeOnlinePlayer {public string id,username;}
    [Serializable] public sealed class NativePlayerInvite {public string id,username;public double expires;}
    [Serializable] public sealed class NativeSocialResponse {public string error,code;public bool ok;public NativeOnlinePlayer[] players;public NativePlayerInvite[] invites;}
    [Serializable] public sealed class NativeInviteRequest {public string target,code,id;public bool accept;}
    public sealed partial class NativeAccount
    {
        public NativeOnlinePlayer[] OnlinePlayers=Array.Empty<NativeOnlinePlayer>();
        public NativePlayerInvite[] Invitations=Array.Empty<NativePlayerInvite>();
        public string SocialStatus="";
        public bool SocialBusy{get;private set;}
        private int socialEpoch;private float socialNext;private Action<string> invitationAccepted;
        private void ResetSocial(){socialEpoch++;SocialBusy=false;socialNext=0;OnlinePlayers=Array.Empty<NativeOnlinePlayer>();Invitations=Array.Empty<NativePlayerInvite>();SocialStatus="";invitationAccepted=null;}
        private void TickSocial(){if(Account!=null&&session.Length>0&&!SocialBusy&&Time.unscaledTime>=socialNext)SocialRequest("social","{}");}
        public void InvitePlayer(string target,string code){if(!NativeRoomCodes.Valid(code))return;SocialRequest("invite",JsonUtility.ToJson(new NativeInviteRequest{target=target,code=code}));}
        public void AnswerInvite(string id,bool accept,Action<string> accepted){if(SocialBusy)return;invitationAccepted=accepted;SocialRequest("invite-answer",JsonUtility.ToJson(new NativeInviteRequest{id=id,accept=accept}));}
        private void SocialRequest(string action,string body){if(Account==null||session.Length==0||SocialBusy)return;SocialBusy=true;socialNext=Time.unscaledTime+20;StartCoroutine(SocialSend(action,body));}
        private IEnumerator SocialSend(string action,string body)
        {
            int epoch=socialEpoch;string owner=Account.id,origin=Origin;
            using(var request=new UnityWebRequest(origin+"/api/accounts/"+action,"POST"))
            {
                request.timeout=8;request.redirectLimit=0;request.downloadHandler=new DownloadHandlerBuffer();request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Authorization","Bearer "+session);
                yield return request.SendWebRequest();
                if(epoch!=socialEpoch||owner!=Account?.id||origin!=Origin)yield break;
                SocialBusy=false;NativeSocialResponse r=null;
                if(request.downloadedBytes<=65536)try{r=JsonUtility.FromJson<NativeSocialResponse>(request.downloadHandler.text);}catch(ArgumentException){}
                if(request.result!=UnityWebRequest.Result.Success||r==null){OnlinePlayers=Array.Empty<NativeOnlinePlayer>();Invitations=Array.Empty<NativePlayerInvite>();SocialStatus=request.responseCode==403||request.responseCode==404?"Online invitations need the updated account service.":r?.error??"Online players unavailable.";invitationAccepted=null;socialNext=Time.unscaledTime+30;yield break;}
                if(action=="social"){
                    if(r.players==null||r.invites==null||r.players.Length>100||r.invites.Length>20){SocialStatus="Invalid online-player response.";yield break;}
                    OnlinePlayers=r.players;Invitations=r.invites;SocialStatus="";
                }else{
                    SocialStatus=action=="invite"?"Invitation sent.":"Invitation answered.";socialNext=0;
                    var callback=invitationAccepted;invitationAccepted=null;
                    if(action=="invite-answer"&&r.ok&&NativeRoomCodes.Valid(r.code))callback?.Invoke(r.code);
                }
            }
        }
    }
}
