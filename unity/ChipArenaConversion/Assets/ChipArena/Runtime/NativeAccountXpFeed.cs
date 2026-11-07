using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private string xpExpeditionId,xpRun,xpLastQueued,xpLastOwner,xpMatchKey,xpMatchId;
        private void QueueAccountXp(AccountXpCompletion completion)
        {if(completion.completionId==xpLastQueued&&account.Access.AccountId==xpLastOwner)return;xpLastQueued=completion.completionId;xpLastOwner=account.Access.AccountId;account.QueueXp(completion);}
        private void AccountXpTick()
        {
            if(account?.Account==null||arena==null||previewArena!=null||ReplayActive||arena.Sandbox)return;
            var view=LiveExpedition;
            if(view!=null)
            {
                xpRun=view.run;var completion=view.Member(account.Access.AccountId)?.xpCompletion;
                xpExpeditionId=NativeAccountXpProgression.Valid(completion)?completion.completionId:null;
                if(xpExpeditionId!=null&&!(xpLastQueued==xpExpeditionId&&xpLastOwner==account.Access.AccountId))QueueAccountXp(completion.Copy());return;
            }
            if(match==null||match.Phase!=NativePhase.Match||string.IsNullOrEmpty(match.CompletionId)||HostLost||match.Seats.Any(s=>s.devCommandsRan)||arena.Local==null||!match.CanEarnProgression(arena.Local.Seat.id))return;
            bool pve=arena.Pve!=null;if(pve&&arena.Pve.Outcome!="victory"&&arena.Pve.Outcome!="defeat")return;
            if(xpMatchKey!=match.CompletionId){xpMatchKey=match.CompletionId;xpMatchId=(pve?"pve_":"pvp_")+match.CompletionId;}
            string id=xpMatchId;if(id==xpLastQueued&&xpLastOwner==account.Access.AccountId)return;
            QueueAccountXp(new AccountXpCompletion{completionId=id,mode=pve?"pve":"pvp",seconds=match.CompletionSeconds,won=pve?arena.Pve.Outcome=="victory":match.SameSide(arena.Local.Seat,match.Seat(match.Winner)),bots=!pve&&match.Seats.Any(s=>s.bot)});
        }
        private AccountXpAward CurrentXpAward(AccountXpSurface surface)
        {
            if(account?.Account==null)return null;
            if(surface==AccountXpSurface.Expedition)return LiveExpedition?.run==xpRun?account.XpAward(xpExpeditionId):null;
            if(match==null||match.Phase!=NativePhase.Match)return null;
            return xpMatchKey==match.CompletionId?account.XpAward(xpMatchId):null;
        }
    }
}
