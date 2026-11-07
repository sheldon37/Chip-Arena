using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeXpResponse {public string error;public AccountXpAward award;public AccountXpSummary summary;}
    public sealed partial class NativeAccount
    {
        private readonly List<AccountXpPending> xpEntries=new List<AccountXpPending>();
        private readonly Dictionary<string,AccountXpPending> xpLookup=new Dictionary<string,AccountXpPending>();
        private string xpOwner,xpOrigin;private int xpEpoch;
        private string xpIdentity="",xpPath="";private bool xpBusy;private float xpNext,xpRefresh;
        public AccountXpSummary XpSummary{get;private set;}

        private void ResetXp(){xpEpoch++;xpIdentity="";xpBusy=false;xpEntries.Clear();xpLookup.Clear();xpOwner=xpOrigin=null;XpSummary=null;xpNext=xpRefresh=0;}
        private void LoadXp()
        {
            if(xpOwner==Account?.id&&xpOrigin==Origin)return;ResetXp();xpOwner=Account?.id;xpOrigin=Origin;if(Account==null)return;xpIdentity=Origin+"\n"+Account.id;
            using(var sha=System.Security.Cryptography.SHA256.Create())xpPath=Path.Combine(ConversionGame.NativeUserDataPath,"account-xp-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(xpIdentity))).Replace("-","")+".json");
            try{if(File.Exists(xpPath)){
                var box=JsonUtility.FromJson<AccountXpOutbox>(File.ReadAllText(xpPath));
                if(box?.version==1&&box.account==Account.id&&box.origin==Origin&&box.entries!=null)foreach(var entry in box.entries)
                    if(NativeAccountXpProgression.Valid(entry?.completion)&&entry.award!=null&&entry.award.account==Account.id&&entry.award.completionId==entry.completion.completionId&&!xpEntries.Any(e=>e.completion.completionId==entry.completion.completionId)){if(entry.award.retry=="manual")entry.award.retry="later";xpEntries.Add(entry);xpLookup[entry.completion.completionId]=entry;}
            }}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException){Status="XP retry history could not be read.";}
        }
        private bool SaveXp()
        {
            try{
                Directory.CreateDirectory(ConversionGame.NativeUserDataPath);
                var keep=xpEntries.Where(e=>e.award.status!="confirmed").Concat(xpEntries.Where(e=>e.award.status=="confirmed").Reverse().Take(200).Reverse()).ToArray();
                File.WriteAllText(xpPath+".new",JsonUtility.ToJson(new AccountXpOutbox{account=Account.id,origin=Origin,entries=keep}));
                if(File.Exists(xpPath))File.Replace(xpPath+".new",xpPath,null);else File.Move(xpPath+".new",xpPath);return true;
            }catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){Status="XP retry history could not be saved. Keep the game open to retry.";return false;}
        }
        public AccountXpAward XpAward(string completion){LoadXp();return completion!=null&&xpLookup.TryGetValue(completion,out var entry)?entry.award:null;}
        public void QueueXp(AccountXpCompletion c)
        {
            LoadXp();if(Account==null||!NativeAccountXpProgression.Valid(c)||xpLookup.ContainsKey(c.completionId))return;
            var entry=new AccountXpPending{completion=c,award=NativeAccountXpProgression.Pending(Account.id,c,XpSummary)};xpEntries.Add(entry);xpLookup[c.completionId]=entry;SaveXp();CountXp();
        }
        private void CountXp(){if(XpSummary!=null)XpSummary.pending=xpEntries.Count(e=>e.award.status!="confirmed"&&e.award.retry!="none");}
        public void RetryXp(string completion)
        {LoadXp();var e=xpEntries.Find(x=>x.completion.completionId==completion);if(e==null||e.award.status!="failed"||e.award.retry=="none")return;e.award.status="pending";e.award.reason="";e.award.revision++;xpNext=0;SaveXp();}
        private void TickXp()
        {
            LoadXp();if(Account==null||xpBusy||session.Length==0)return;
            if(Time.unscaledTime>=xpRefresh){xpRefresh=Time.unscaledTime+300;xpBusy=true;StartCoroutine(XpRequest(null));return;}
            if(Time.unscaledTime<xpNext)return;
            var entry=xpEntries.Find(e=>e.award.status=="pending"||e.award.status=="failed"&&(e.award.retry=="auto"||e.award.retry=="later"));
            if(entry!=null){xpBusy=true;entry.award.status="pending";entry.award.attempt++;entry.award.revision++;entry.award.reason="";SaveXp();StartCoroutine(XpRequest(entry));}
        }
        private IEnumerator XpRequest(AccountXpPending entry)
        {
            string identity=xpIdentity,owner=Account.id;int epoch=xpEpoch;
            using(var request=new UnityWebRequest(Origin+"/api/accounts/xp",entry==null?"GET":"POST"))
            {
                request.timeout=15;request.redirectLimit=0;request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Authorization","Bearer "+session);request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("Cache-Control","no-store");
                if(entry!=null)request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(entry.completion)));
                yield return request.SendWebRequest();
                if(epoch!=xpEpoch||owner!=Account?.id||xpOrigin!=Origin||identity!=xpIdentity)yield break;
                xpBusy=false;NativeXpResponse response=null;
                if(request.downloadedBytes<=131072)try{response=JsonUtility.FromJson<NativeXpResponse>(request.downloadHandler.text);}catch(ArgumentException){}
                bool success=request.result==UnityWebRequest.Result.Success&&response?.summary?.version==1&&response.summary.known&&response.summary.account==owner&&response.summary.total>=0&&response.summary.total<=NativeAccountXpProgression.Cap;
                if(entry!=null)success=success&&response.award?.version==1&&response.award.account==owner&&response.award.completionId==entry.completion.completionId&&response.award.status=="confirmed";
                if(success){if(XpSummary==null||response.summary.revision>=XpSummary.revision)XpSummary=response.summary;
                    if(entry!=null){response.award.revision=entry.award.revision+1;response.award.attempt=entry.award.attempt;entry.award=response.award;SaveXp();}CountXp();xpNext=Time.unscaledTime+1;
                }else if(entry!=null){var a=entry.award;a.status="failed";a.revision++;bool permanent=request.responseCode==400||request.responseCode==403||request.responseCode==409;
                    a.retry=permanent?"none":request.responseCode==401?"later":a.attempt>=5?"manual":"auto";a.retryIn=a.retry=="auto"?Math.Min(300,15*(1<<Math.Min(a.attempt,4))):0;
                    a.reason=permanent?"This completion was rejected by the account service.":request.responseCode==401?"Sign in again to confirm this reward.":"Account service unavailable. Your reward is queued for retry.";
                    xpNext=Time.unscaledTime+(a.retry=="later"?60:a.retryIn);SaveXp();CountXp();
                }else xpRefresh=Time.unscaledTime+60;
            }
        }
    }
}
