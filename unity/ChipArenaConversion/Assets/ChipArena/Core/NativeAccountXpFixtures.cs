using System;

namespace ChipArena.UnityConversion
{
    // DEV PREVIEW ONLY: MOCK VALUES (Claude, 2026-10-19). The XP panel's and the menu level's preview fixtures for the DEV panel
    // (Previews · ACCOUNT XP) and the managed checks. Every amount, total, level, threshold, label and reason below is a mock made up
    // to exercise the presentation: none of it is a proposed reward, levelling curve, perk or unlock, and none of it reaches an
    // account. Mock completion ids start with NativeAccountXpContract.MockPrefix and are never written to the local record; every
    // label says MOCK and the preview's banner says DEV PREVIEW · ACCOUNT XP · MOCK VALUES.
    public static class NativeAccountXpFixtures
    {
        public const string Banner="DEV PREVIEW · ACCOUNT XP · MOCK VALUES",Account="mock-pilot";
        public enum Kind{Zero,Progress,LevelUp,Multi,Pending,FailedAuto,FailedManual,Mismatch,NoThresholds}
        public static readonly string[] Names={"ZERO","PROGRESS","LEVEL UP","MULTI LEVEL","PENDING","FAILED · AUTO","FAILED · TRY AGAIN","LINE SUM OFF","NO THRESHOLDS"};
        public enum SummaryKind{Off,Unknown,Zero,Progress,Pending,Max}
        public static readonly string[] SummaryNames={"OFF","UNKNOWN","ZERO","PROGRESS","PENDING","MAX LEVEL"};

        // MOCK thresholds (uneven widths, to exercise interpolation across bands of different sizes). Not a curve proposal.
        private static readonly long[] MockStart={0,500,1200,2000,3000,4200,5500,7000,8600,10400};
        /// <summary>MOCK bands for levels from..to (1-based levels; MockStart[level-1] starts each).</summary>
        public static AccountXpBand[] MockBands(int from,int to)
        {
            var b=new AccountXpBand[to-from+1];
            for(int l=from;l<=to;l++)b[l-from]=new AccountXpBand{level=l,start=MockStart[l-1],next=l<MockStart.Length?MockStart[l]:MockStart[l-1]+2000};
            return b;
        }
        private static AccountXpLine Line(string key,string label,long xp)=>new AccountXpLine{key=key,label=label,xp=xp};
        private static AccountXpAward Award(string id,string status,long from,int fromLevel,long to,int toLevel,params AccountXpLine[] lines)
            =>new AccountXpAward{version=NativeAccountXpContract.Version,completionId=NativeAccountXpContract.MockPrefix+id,account=Account,mode=NativeAccountXpContract.Pvp,status=status,revision=1,
                lines=lines,previousTotal=from,newTotal=to,previousLevel=fromLevel,newLevel=toLevel,bands=MockBands(fromLevel,toLevel),attempt=1};

        /// <summary>A fresh MOCK award of `kind` for the results of `mode` (a new object each call; the DEV panel makes one when the
        /// pick changes).</summary>
        public static AccountXpAward Make(Kind kind,string forMode=NativeAccountXpContract.Pvp){var a=Of(kind);if(a!=null)a.mode=forMode??NativeAccountXpContract.Pvp;return a;}
        private static AccountXpAward Of(Kind kind)
        {
            const string C=NativeAccountXpContract.Confirmed,P=NativeAccountXpContract.Pending,F=NativeAccountXpContract.Failed;
            switch(kind)
            {
                case Kind.Zero:return Award("xp-zero",C,0,1,0,1,Line("playtime","MOCK · Active playtime",0));
                case Kind.Progress:return Award("xp-progress",C,2340,4,2760,4,Line("playtime","MOCK · Active playtime",300),Line("objectives","MOCK · Objectives",120));
                case Kind.LevelUp:return Award("xp-level-up",C,2760,4,3380,5,Line("playtime","MOCK · Active playtime",380),Line("objectives","MOCK · Objectives",240));
                case Kind.Multi:return Award("xp-multi",C,1050,2,4560,6,Line("playtime","MOCK · Active playtime",1400),Line("objectives","MOCK · Objectives",1610),Line("bonus","MOCK · First completion",500));
                case Kind.Pending:{var a=Award("xp-pending",P,2760,4,3380,5,Line("playtime","MOCK · Active playtime",380),Line("objectives","MOCK · Objectives",240));return a;}
                case Kind.FailedAuto:{var a=Award("xp-failed-auto",F,2340,4,2760,4,Line("playtime","MOCK · Active playtime",300),Line("objectives","MOCK · Objectives",120));a.attempt=2;a.retry=NativeAccountXpContract.Auto;a.retryIn=12;a.reason="MOCK · The account service did not answer.";return a;}
                case Kind.FailedManual:{var a=Award("xp-failed-manual",F,2340,4,2760,4,Line("playtime","MOCK · Active playtime",300),Line("objectives","MOCK · Objectives",120));a.attempt=3;a.retry=NativeAccountXpContract.Manual;a.reason="MOCK · The save was refused.";return a;}
                case Kind.Mismatch:return Award("xp-mismatch",C,2340,4,2760,4,Line("playtime","MOCK · Active playtime",300),Line("objectives","MOCK · Objectives",90));
                case Kind.NoThresholds:{var a=Award("xp-no-thresholds",C,2760,4,3380,5,Line("playtime","MOCK · Active playtime",380),Line("objectives","MOCK · Objectives",240));a.bands=null;return a;}
                default:return null;
            }
        }
        /// <summary>The authority's next issue of the same MOCK completion: confirmed (or failed with `retry`), revision + 1.</summary>
        public static AccountXpAward Reissue(AccountXpAward a,string status,string retry=null,float retryIn=0,string reason=null)
        {
            if(a==null)return null;
            return new AccountXpAward{version=a.version,completionId=a.completionId,account=a.account,mode=a.mode,status=status,revision=a.revision+1,lines=a.lines,previousTotal=a.previousTotal,
                newTotal=a.newTotal,previousLevel=a.previousLevel,newLevel=a.newLevel,bands=a.bands,attempt=status==NativeAccountXpContract.Confirmed?a.attempt:a.attempt+1,retry=retry,retryIn=retryIn,reason=reason};
        }
        /// <summary>A MOCK menu summary (null for Off and Unknown's known=false).</summary>
        public static AccountXpSummary Summary(SummaryKind kind)
        {
            AccountXpSummary S(long total,int level,long start,long next,int pending)=>new AccountXpSummary{version=NativeAccountXpContract.Version,account=Account,known=true,total=total,level=level,levelStart=start,nextLevelAt=next,pending=pending,revision=1};
            switch(kind)
            {
                case SummaryKind.Unknown:return new AccountXpSummary{version=NativeAccountXpContract.Version,account=Account,known=false};
                case SummaryKind.Zero:return S(0,1,0,500,0);
                case SummaryKind.Progress:return S(2340,4,2000,3000,0);
                case SummaryKind.Pending:return S(2340,4,2000,3000,1);
                case SummaryKind.Max:return S(10400,10,10400,0,0);
                default:return null;
            }
        }
    }
}
