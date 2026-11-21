using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class AccountXpCompletion
    {public int version=1;public string completionId,mode;public bool won,bots;public int seconds,cleared;public AccountXpCompletion Copy()=>(AccountXpCompletion)MemberwiseClone();}
    [Serializable] public sealed class AccountXpPending
    {public AccountXpCompletion completion;public AccountXpAward award;}
    [Serializable] public sealed class AccountXpOutbox
    {public int version=1;public string account,origin;public AccountXpPending[] entries=Array.Empty<AccountXpPending>();}
    public static class NativeAccountXpProgression
    {
        public const long Cap=1237500;
        public static int Level(long total){int level=1;while(level<100&&total>=250L*level*(level+1)/2)level++;return level;}
        public static long Start(int level)=>250L*(level-1)*level/2;
        public static AccountXpSummary Summary(string account,long total=0,int revision=0)
        {int level=Level(total);return new AccountXpSummary{version=1,account=account,known=true,total=total,level=level,levelStart=Start(level),nextLevelAt=level==100?Start(level):Start(level+1),revision=revision};}
        public static bool Valid(AccountXpCompletion c)=>c!=null&&c.version==1&&c.completionId!=null&&System.Text.RegularExpressions.Regex.IsMatch(c.completionId,@"^[-a-zA-Z0-9_:]{1,128}\z")&&(c.mode=="pvp"||c.mode=="pve"||c.mode=="expedition")&&c.seconds>=60&&c.seconds<=604800&&c.cleared>=0&&c.cleared<=3&&(c.mode=="expedition"||c.cleared==0);
        // Estimates only. The service returns the authoritative breakdown and thresholds at confirmation.
        public static AccountXpAward Pending(string account,AccountXpCompletion c,AccountXpSummary saved)
        {
            long total=saved?.known==true?saved.total:0;int half=c.mode=="pvp"&&c.bots?2:1;
            var lines=new System.Collections.Generic.List<AccountXpLine>{new AccountXpLine{key="completion",label="Completion",xp=(c.mode=="pvp"?100:c.mode=="pve"?150:200)/half}};
            if(c.cleared>0)lines.Add(new AccountXpLine{key="objectives",label="Instances cleared",xp=c.cleared*100});
            if(c.won)lines.Add(new AccountXpLine{key="victory",label="Victory",xp=(c.mode=="pvp"?50:c.mode=="pve"?150:400)/half});
            long next=Math.Min(Cap,total+lines.Sum(l=>l.xp)),remaining=next-total;foreach(var line in lines){line.xp=Math.Min(line.xp,remaining);remaining-=line.xp;}
            int before=Level(total),after=Level(next);var bands=Enumerable.Range(before,after-before+1).Select(l=>new AccountXpBand{level=l,start=Start(l),next=l==100?Start(l):Start(l+1)}).ToArray();
            return new AccountXpAward{version=1,account=account,completionId=c.completionId,mode=c.mode,status="pending",revision=1,previousTotal=saved?.known==true?total:-1,newTotal=saved?.known==true?next:-1,previousLevel=before,newLevel=after,bands=saved?.known==true?bands:Array.Empty<AccountXpBand>(),lines=lines.ToArray()};
        }
    }
}
