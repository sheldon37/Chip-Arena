using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeClearReply { public string accountId; public int bossClears; }
    public sealed partial class NativeAccount
    {
        private string clearOwner,clearOrigin,clearKey;
        private int localClears,clearGeneration;
        private float clearRetry;
        private bool clearBusy;

        private void LoadClears()
        {
            if(clearOwner==Account?.id && clearOrigin==Origin)return;
            clearOwner=Account?.id;clearOrigin=Origin;clearGeneration++;
            clearKey=clearOwner==null?null:"ChipArena.BossClears."+Origin+"/"+clearOwner;
            localClears=clearKey==null?0:PlayerPrefs.GetInt(clearKey,0)&NativePracticeRules.All;
            clearBusy=false;clearRetry=0;
        }

        public int EarnedBosses { get { LoadClears();return ((Account?.bossClears??0)|localClears)&NativePracticeRules.All; } }
        public bool PendingBossSync { get { LoadClears();return (localClears & ~(Account?.bossClears??0))!=0; } }
        public void RememberBosses(string owner,int mask)
        {
            if(owner==null || owner!=Account?.id)return;
            LoadClears();
            int added=mask & NativePracticeRules.All & ~EarnedBosses;
            if(added==0)return;
            localClears|=added;
            PlayerPrefs.SetInt(clearKey,localClears);PlayerPrefs.Save();
        }
        private void SyncBossClears()
        {
            LoadClears();
            if(Account==null || clearBusy || Time.unscaledTime<clearRetry || session.Length==0)return;
            int pending=localClears & ~Account.bossClears;
            if(pending==0)return;
            string boss=NativePracticeRules.Bosses.First(id=>(pending & NativePracticeRules.Bit(id))!=0);
            clearBusy=true;
            StartCoroutine(SendBossClear(boss,clearGeneration));
        }
        private IEnumerator SendBossClear(string boss,int generation)
        {
            using(var req=new UnityWebRequest(Origin+"/api/accounts/boss-clear","POST"))
            {
                req.timeout=12;req.redirectLimit=0;
                req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes("{\"boss\":\""+boss+"\"}"));
                req.downloadHandler=new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type","application/json");req.SetRequestHeader("Authorization","Bearer "+session);
                yield return req.SendWebRequest();
                if(generation!=clearGeneration || clearOwner!=Account?.id || clearOrigin!=Origin)yield break;
                clearBusy=false;clearRetry=Time.unscaledTime+60;
                if(req.result!=UnityWebRequest.Result.Success || req.downloadedBytes>1024)yield break;
                NativeClearReply reply=null;
                try { reply=JsonUtility.FromJson<NativeClearReply>(req.downloadHandler.text); } catch(ArgumentException) {}
                if(reply?.accountId!=clearOwner || (reply.bossClears & NativePracticeRules.Bit(boss))==0)yield break;
                Account.bossClears|=reply.bossClears & NativePracticeRules.All;
                clearRetry=0;
            }
        }
    }
    public sealed partial class NativeArena
    {
        private void AwardBossClear(string boss)
        {
            if(Replica || PracticePhase>0 || Sandbox || Match.Seats.Any(s=>s.devCommandsRan))return;
            int bit=NativePracticeRules.Bit(boss);
            foreach(var pilot in Members())
            {
                var seat=pilot.Seat;
                if(seat.bot || !seat.connected || string.IsNullOrEmpty(seat.access?.AccountId))continue;
                if(seat.clearOwner!=seat.access.AccountId)seat.clearMask=0;
                seat.clearOwner=seat.access.AccountId;seat.clearMask|=bit;
            }
        }
    }
}
