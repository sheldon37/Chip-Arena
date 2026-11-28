using System;
using System.Globalization;
using System.Text;

namespace ChipArena.UnityConversion
{
    // Permanent account XP, the presentation side (Claude, 2026-10-19; owner: "please start the presentation side of permanent
    // account XP for Chip Arena. XP is planned but not implemented yet. PvP and PvE completions should award permanent account XP,
    // mainly from active playtime and objectives"). docs/ACCOUNT-XP-PRESENTATION.md is the proposed contract.
    //
    // Every number comes from gameplay/account code (Astra): the completion's id, the award's status and retry state, the
    // breakdown's keys, labels and amounts, the totals and levels before and after, and the level thresholds. The presentation
    // validates what it is given and degrades when it cannot trust it, interpolates only inside the supplied thresholds for its
    // animation, and never computes a reward, a levelling curve, an unlock or an account write. XP is kept apart from in-run chips
    // and currency: its own violet tone (never the wallet's gold or the chips' cyan), its own words (XP, LEVEL; never $, chips or
    // credits) and its own panel beside the results, never inside them.

    /// <summary>The award's confirmation as the account authority reports it (AccountXpAward.status).</summary>
    public enum AccountXpStatus { None, Pending, Confirmed, Failed }
    /// <summary>What the authority will do about a failed confirmation (AccountXpAward.retry).</summary>
    public enum AccountXpRetry { Unknown, Auto, Manual, Later, None }
    /// <summary>The results surface an award is shown on: the PvP match end, the PvE results, the Expedition screen.</summary>
    public enum AccountXpSurface { Pvp, Pve, Expedition }

    /// <summary>One line of the breakdown: a stable key, the label account code supplies (shown as given), the XP it gave.</summary>
    [Serializable] public sealed class AccountXpLine { public string key,label; public long xp; }
    /// <summary>
    /// One level's thresholds as account code supplies them: the account is at `level` while start &lt;= total &lt; next. A band whose
    /// next is not above its start is the top level (no next); only the last band may be one. The presentation never extends,
    /// guesses or computes a band.
    /// </summary>
    [Serializable] public sealed class AccountXpBand { public int level; public long start,next; }

    /// <summary>
    /// One completion's award for the viewing account (proposed contract v1, docs/ACCOUNT-XP-PRESENTATION.md). Field-only, so Unity's
    /// JsonUtility and the managed codec both round-trip it; an absent number reads 0, so `version` 0 means "not set" (refused).
    /// The same `completionId` is re-issued as the award moves pending → confirmed, or pending → failed → (retry) pending →
    /// confirmed; `revision` rises with each issue. The presentation animates a confirmed completion once, ever (per account and
    /// id), and shows a pending or failed one as not saved.
    /// </summary>
    [Serializable] public sealed class AccountXpAward
    {
        /// <summary>Contract version (NativeAccountXpContract.Version); 0 is refused, a newer one is shown and flagged in DEV.</summary>
        public int version;
        /// <summary>Unique per completed match or run, per account; stable across reconnects and re-issues. At most 128 characters,
        /// no control characters.</summary>
        public string completionId;
        /// <summary>The account the award belongs to (the viewer's); an award for anyone else is never shown.</summary>
        public string account;
        /// <summary>"pvp", "pve" or "expedition": which results it belongs to (a display tag only).</summary>
        public string mode;
        /// <summary>"pending" (completion recorded, account write not confirmed), "confirmed" (the account write succeeded) or
        /// "failed" (the write was refused or could not be confirmed; nothing saved). Anything else is refused.</summary>
        public string status;
        /// <summary>Rises each time the authority re-issues this completion's award (a status change, a retry, a correction).</summary>
        public int revision;
        /// <summary>The breakdown, in the order to show it. Keys and labels are account code's; amounts are shown as given.</summary>
        public AccountXpLine[] lines;
        /// <summary>The account's saved total and level before this award, and its total and level once this award is saved.</summary>
        public long previousTotal,newTotal;public int previousLevel,newLevel;
        /// <summary>Contiguous, ascending level thresholds covering previousLevel..newLevel at least.</summary>
        public AccountXpBand[] bands;
        /// <summary>Confirmation attempts so far (1 is the first).</summary>
        public int attempt;
        /// <summary>For "failed": "auto" (the authority retries by itself, see retryIn), "manual" (the player may ask for a retry),
        /// "later" (retried when the account service is reachable again), "none" (not retried: the award is lost). Absent: unknown.</summary>
        public string retry;
        /// <summary>For an "auto" retry: seconds from this issue to the next attempt; 0 or less: no countdown known.</summary>
        public float retryIn;
        /// <summary>For "failed": the authority's short reason, shown verbatim (at most 200 characters are used).</summary>
        public string reason;
    }

    /// <summary>
    /// The signed-in account's XP for the main menu (proposed contract v1): the saved total and level, that level's thresholds and
    /// how many awards still wait for confirmation. Saved values only; a pending award never adds to the total shown.
    /// </summary>
    [Serializable] public sealed class AccountXpSummary
    {
        public int version;public string account;
        /// <summary>False (or the whole summary null): unknown, the menu shows no level.</summary>
        public bool known;
        public long total;public int level;
        /// <summary>The level's thresholds: start &lt;= total &lt; nextLevelAt; nextLevelAt not above levelStart: the top level.</summary>
        public long levelStart,nextLevelAt;
        /// <summary>Awards recorded but not yet confirmed (a "pending" marker only; never counted into the total).</summary>
        public int pending;
        public int revision;
    }

    /// <summary>Why an award or summary could not be taken as given (DEV builds show the names).</summary>
    [Flags] public enum AccountXpFlaw
    {
        None=0,NoAward=1,NoId=1<<1,Version=1<<2,Status=1<<3,Account=1<<4,Totals=1<<5,Levels=1<<6,NoBands=1<<7,Bands=1<<8,
        LineSum=1<<9,Lines=1<<10,NegativeLine=1<<11,Retry=1<<12,Newer=1<<13,ManyLevels=1<<14,
    }

    /// <summary>One award as the panel may use it (NativeAccountXpContract.Check).</summary>
    public struct AccountXpCheck
    {
        public AccountXpStatus Status;public AccountXpRetry Retry;public AccountXpFlaw Flaws;
        /// <summary>The award may be shown at all: an id, a known status and version, the viewer's own. Otherwise nothing is drawn
        /// (a DEV viewer sees the flaws).</summary>
        public bool Shown;
        /// <summary>Totals and levels are consistent: the numbers can be shown. Otherwise the panel says the details are unavailable.</summary>
        public bool Usable;
        /// <summary>The bands cover previousLevel..newLevel and hold both totals: the bar is drawn and animated inside them.</summary>
        public bool Bars;
        /// <summary>newTotal - previousTotal (the authority's own totals), and the breakdown's sum as given.</summary>
        public long Gain,LineSum;
        /// <summary>Levels gained (newLevel - previousLevel) and the bands' indices for the two levels (-1 without bars).</summary>
        public int Levels,From,To;
        /// <summary>Where each total stands inside its own level's band (0..1; 1 at the top level), -1 without bars.</summary>
        public float FromFill,ToFill;
        /// <summary>The previous or the new level is the top one (its band has no next).</summary>
        public bool FromMax,ToMax;
    }

    /// <summary>The summary as the menu may use it.</summary>
    public struct AccountXpSummaryCheck
    {
        public bool Shown,Bar,Max;public int Level,Pending;public float Fill;public AccountXpFlaw Flaws;
    }

    /// <summary>The proposed contract's words, limits and validation (docs/ACCOUNT-XP-PRESENTATION.md). Engine-free.</summary>
    public static class NativeAccountXpContract
    {
        public const int Version=1,MaxId=128,MaxReason=200,MaxLines=32,MaxBands=512;
        public const string Pending="pending",Confirmed="confirmed",Failed="failed";
        public const string Auto="auto",Manual="manual",Later="later",NoRetry="none";
        public const string Pvp="pvp",Pve="pve",Expedition="expedition";
        /// <summary>DEV preview fixtures only (NativeAccountXpFixtures): ids with this prefix are never written to the local record.</summary>
        public const string MockPrefix="mock-";

        public static AccountXpStatus ParseStatus(string s)=>s==Pending?AccountXpStatus.Pending:s==Confirmed?AccountXpStatus.Confirmed:s==Failed?AccountXpStatus.Failed:AccountXpStatus.None;
        public static AccountXpRetry ParseRetry(string s)=>s==Auto?AccountXpRetry.Auto:s==Manual?AccountXpRetry.Manual:s==Later?AccountXpRetry.Later:s==NoRetry?AccountXpRetry.None:AccountXpRetry.Unknown;
        public static bool Mock(AccountXpAward a)=>a?.completionId!=null&&a.completionId.StartsWith(MockPrefix,StringComparison.Ordinal);
        public static bool Mock(AccountXpSummary s)=>s?.account!=null&&s.account.StartsWith(MockPrefix,StringComparison.Ordinal);
        private static bool Clean(string s){if(s==null)return true;for(int i=0;i<s.Length;i++)if(s[i]<' '||s[i]==(char)127)return false;return true;}

        /// <summary>Where `total` stands inside band b (0..1); 1 when b is the top level (no next). NaN-free for any longs.</summary>
        public static float Fraction(long total,AccountXpBand b,out bool max)=>Fraction(total,b.start,b.next,out max);
        /// <summary>Where `total` stands between `start` and `next` (0..1); 1 when next is not above start (the top level).</summary>
        public static float Fraction(long total,long start,long next,out bool max)
        {
            max=next<=start;if(max)return 1;double span=(double)next-start,at=(double)total-start;
            float f=(float)(at/span);return f<0?0:f>1?1:f;
        }
        private static bool Holds(AccountXpBand b,long total)=>Holds(b.start,b.next,total);
        private static bool Holds(long start,long next,long total)=>total>=start&&(next<=start||total<next);

        /// <summary>
        /// Validates an award for `viewer` (null: the viewer is not known yet, any account passes). Never throws; computes nothing
        /// but the differences and positions of the values it is given.
        /// </summary>
        public static AccountXpCheck Check(AccountXpAward a,string viewer)
        {
            var c=new AccountXpCheck{From=-1,To=-1,FromFill=-1,ToFill=-1};
            if(a==null){c.Flaws=AccountXpFlaw.NoAward;return c;}
            c.Status=ParseStatus(a.status);if(c.Status==AccountXpStatus.None)c.Flaws|=AccountXpFlaw.Status;
            if(string.IsNullOrWhiteSpace(a.completionId)||a.completionId.Length>MaxId||!Clean(a.completionId))c.Flaws|=AccountXpFlaw.NoId;
            if(a.version<1)c.Flaws|=AccountXpFlaw.Version;else if(a.version>Version)c.Flaws|=AccountXpFlaw.Newer;
            if(!Clean(a.account)||!string.IsNullOrEmpty(viewer)&&!string.IsNullOrEmpty(a.account)&&a.account!=viewer)c.Flaws|=AccountXpFlaw.Account;
            if(c.Status==AccountXpStatus.Failed){c.Retry=ParseRetry(a.retry);if(c.Retry==AccountXpRetry.Unknown&&!string.IsNullOrEmpty(a.retry))c.Flaws|=AccountXpFlaw.Retry;}
            c.Shown=(c.Flaws&(AccountXpFlaw.Status|AccountXpFlaw.NoId|AccountXpFlaw.Version|AccountXpFlaw.Account))==0;
            bool totals=a.previousTotal>=0&&a.newTotal>=a.previousTotal,levels=a.previousLevel>=0&&a.newLevel>=a.previousLevel;
            if(!totals)c.Flaws|=AccountXpFlaw.Totals;if(!levels)c.Flaws|=AccountXpFlaw.Levels;
            c.Usable=c.Shown&&totals&&levels;
            if(totals)c.Gain=a.newTotal-a.previousTotal;if(levels){c.Levels=a.newLevel-a.previousLevel;if(c.Levels>=NativeAccountXpPanel.MaxSegments)c.Flaws|=AccountXpFlaw.ManyLevels;}
            // The breakdown, as given: its sum is compared with the totals' difference, never used in its place.
            var lines=a.lines;long sum=0;bool overflow=false;
            if(lines!=null)
            {
                if(lines.Length>MaxLines)c.Flaws|=AccountXpFlaw.Lines;
                for(int i=0;i<lines.Length&&i<MaxLines;i++)
                {
                    var l=lines[i];if(l==null){c.Flaws|=AccountXpFlaw.Lines;continue;}if(l.xp<0)c.Flaws|=AccountXpFlaw.NegativeLine;
                    if(l.xp>0&&sum>long.MaxValue-l.xp||l.xp<0&&sum<long.MinValue-l.xp)overflow=true;else sum+=l.xp;
                }
            }
            c.LineSum=sum;if(overflow||totals&&sum!=c.Gain)c.Flaws|=AccountXpFlaw.LineSum;
            // The bands: contiguous, ascending, covering both levels and holding both totals. Otherwise no bar (numbers still shown).
            var b=a.bands;
            if(b==null||b.Length==0)c.Flaws|=AccountXpFlaw.NoBands;
            else if(c.Usable)
            {
                bool ok=b.Length<=MaxBands;int from=-1,to=-1;
                for(int i=0;ok&&i<b.Length;i++)
                {
                    var x=b[i];if(x==null){ok=false;break;}
                    if(x.start<0||x.next<=x.start&&i!=b.Length-1)ok=false;
                    if(i>0&&(x.level!=b[i-1].level+1||x.start!=b[i-1].next))ok=false;
                    if(x.level==a.previousLevel)from=i;if(x.level==a.newLevel)to=i;
                }
                ok=ok&&from>=0&&to>=from&&Holds(b[from],a.previousTotal)&&Holds(b[to],a.newTotal);
                if(!ok)c.Flaws|=AccountXpFlaw.Bands;
                else{c.Bars=true;c.From=from;c.To=to;c.FromFill=Fraction(a.previousTotal,b[from],out c.FromMax);c.ToFill=Fraction(a.newTotal,b[to],out c.ToMax);}
            }
            return c;
        }

        /// <summary>Validates the menu's summary for `viewer`: unknown, someone else's or inconsistent shows no level; thresholds that
        /// do not hold the total show the level without a bar.</summary>
        public static AccountXpSummaryCheck Check(AccountXpSummary s,string viewer)
        {
            var c=new AccountXpSummaryCheck{Fill=-1};
            if(s==null||!s.known){c.Flaws=AccountXpFlaw.NoAward;return c;}
            if(s.version<1){c.Flaws|=AccountXpFlaw.Version;return c;}
            if(!Clean(s.account)||!string.IsNullOrEmpty(viewer)&&!string.IsNullOrEmpty(s.account)&&s.account!=viewer){c.Flaws|=AccountXpFlaw.Account;return c;}
            if(s.total<0||s.level<0){c.Flaws|=AccountXpFlaw.Totals;return c;}
            c.Shown=true;c.Level=s.level;c.Pending=Math.Max(0,s.pending);
            if(s.levelStart>=0&&Holds(s.levelStart,s.nextLevelAt,s.total)){c.Bar=true;c.Fill=Fraction(s.total,s.levelStart,s.nextLevelAt,out c.Max);}else c.Flaws|=AccountXpFlaw.Bands;
            return c;
        }

        /// <summary>The DEV line for a set of flaws (built when an award changes, never per frame).</summary>
        public static string Describe(AccountXpFlaw f)
        {
            if(f==AccountXpFlaw.None)return null;var s=new StringBuilder("DEV · ");bool first=true;
            foreach(AccountXpFlaw x in Enum.GetValues(typeof(AccountXpFlaw))){if(x==AccountXpFlaw.None||(f&x)==0)continue;if(!first)s.Append(", ");s.Append(FlawName(x));first=false;}
            return s.ToString();
        }
        private static string FlawName(AccountXpFlaw x)
        {
            switch(x)
            {
                case AccountXpFlaw.NoAward:return "NO AWARD";case AccountXpFlaw.NoId:return "NO COMPLETION ID";case AccountXpFlaw.Version:return "NO VERSION";
                case AccountXpFlaw.Status:return "UNKNOWN STATUS";case AccountXpFlaw.Account:return "OTHER ACCOUNT";case AccountXpFlaw.Totals:return "TOTALS INCONSISTENT";
                case AccountXpFlaw.Levels:return "LEVELS INCONSISTENT";case AccountXpFlaw.NoBands:return "NO THRESHOLDS";case AccountXpFlaw.Bands:return "THRESHOLDS DO NOT HOLD THE TOTALS";
                case AccountXpFlaw.LineSum:return "LINES DO NOT ADD UP TO THE TOTALS";case AccountXpFlaw.Lines:return "BAD LINES";case AccountXpFlaw.NegativeLine:return "NEGATIVE LINE";
                case AccountXpFlaw.Retry:return "UNKNOWN RETRY";case AccountXpFlaw.Newer:return "NEWER VERSION";case AccountXpFlaw.ManyLevels:return "MANY LEVELS (CHAIN COLLAPSED)";
                default:return x.ToString().ToUpperInvariant();
            }
        }
    }

    /// <summary>
    /// The XP panel's words. Built when an award changes, or cached once per value (levels, counts, seconds), so a frame allocates
    /// nothing. XP wording only: XP, LEVEL, SAVED, PENDING; never currency or chips.
    /// </summary>
    public static class NativeAccountXpText
    {
        public const string Eyebrow="ACCOUNT XP",Saved="SAVED TO YOUR ACCOUNT",Confirming="CONFIRMING REWARD…",NotSavedYet="Not saved to your account yet.",
            PendingTag="PENDING",NotSavedTag="NOT SAVED",FailedTitle="COULDN'T CONFIRM YOUR XP",FailedDetail="Your XP was not saved.",
            RetryingNow="RETRYING AUTOMATICALLY…",TryAgain="TRY AGAIN",RetryLater="WILL RETRY WHEN YOU'RE BACK ONLINE",NoRetry="THIS REWARD WAS NOT SAVED",
            Unavailable="XP details are unavailable for this completion.",MaxLevel="MAX LEVEL",LevelUp="LEVEL UP",Invalid="DEV · AWARD NOT SHOWN TO PLAYERS",
            NoLines="NO XP THIS TIME",Retrying="RETRYING…";
        public const int MaxCountdown=120;
        private static readonly string[] seconds=Make(MaxCountdown+1,i=>"RETRYING IN "+i+"s"),minutes=Make(61,i=>"RETRYING IN "+i+"m");
        private static readonly string[] attempts=Make(21,i=>"ATTEMPT "+i),ups=Make(NativeAccountXpPanel.MaxSegments+1,i=>i<=1?LevelUp:LevelUp+" ×"+i);
        private static readonly string[] more=Make(NativeAccountXpContract.MaxLines+1,i=>"+"+i+" MORE");
        private static string[] Make(int n,Func<int,string> f){var s=new string[n];for(int i=0;i<n;i++)s[i]=f(i);return s;}
        private static readonly System.Collections.Generic.Dictionary<int,string> levels=new System.Collections.Generic.Dictionary<int,string>(),numbers=new System.Collections.Generic.Dictionary<int,string>();
        private static readonly object gate=new object();

        /// <summary>"LEVEL n", cached per level.</summary>
        public static string Level(int n){lock(gate){if(!levels.TryGetValue(n,out var s)){s="LEVEL "+n.ToString(CultureInfo.InvariantCulture);levels[n]=s;}return s;}}
        /// <summary>The level badge's number, cached per level.</summary>
        public static string Number(int n){lock(gate){if(!numbers.TryGetValue(n,out var s)){s=n.ToString(CultureInfo.InvariantCulture);numbers[n]=s;}return s;}}
        /// <summary>"LEVEL UP", "LEVEL UP ×k".</summary>
        public static string Ups(int k)=>ups[k<0?0:k>=ups.Length?ups.Length-1:k];
        /// <summary>The auto-retry countdown (whole seconds up to two minutes, then minutes), or the plain line when unknown.</summary>
        public static string RetryIn(float left)
        {
            if(!(left>0)||float.IsInfinity(left))return RetryingNow;int s=(int)Math.Ceiling(left);
            return s<=MaxCountdown?seconds[s]:minutes[Math.Min(60,(int)Math.Ceiling(s/60f))];
        }
        public static string Attempt(int n)=>n<=1?null:attempts[Math.Min(n,attempts.Length-1)];
        public static string More(int n)=>n<=0?null:more[Math.Min(n,more.Length-1)];
        /// <summary>An amount as given: "+1,240 XP", "−20 XP", "+0 XP".</summary>
        public static string Xp(long v)=>(v<0?"−":"+")+(v<0?(v==long.MinValue?"9,223,372,036,854,775,808":(-v).ToString("#,0",CultureInfo.InvariantCulture)):v.ToString("#,0",CultureInfo.InvariantCulture))+" XP";
        /// <summary>A saved total against its level's next threshold: "3,240 / 4,000 XP"; the top level: "3,240 XP · MAX LEVEL".</summary>
        public static string Progress(long total,AccountXpBand b)
        {
            string t=total.ToString("#,0",CultureInfo.InvariantCulture);
            return b.next<=b.start?t+" XP · "+MaxLevel:t+" / "+b.next.ToString("#,0",CultureInfo.InvariantCulture)+" XP";
        }
        public static string Next(AccountXpBand b)=>b.next<=b.start?MaxLevel:"NEXT · "+Level(b.level+1);
        public static string Climb(int from,int to)=>Level(from)+"  →  "+to.ToString(CultureInfo.InvariantCulture);
        /// <summary>The results tag for the award's mode (display only).</summary>
        public static string Mode(string mode)=>mode==NativeAccountXpContract.Pvp?"PVP MATCH":mode==NativeAccountXpContract.Pve?"PVE RUN":mode==NativeAccountXpContract.Expedition?"EXPEDITION":null;
        /// <summary>A breakdown line's label: account code's label, else its key, else XP.</summary>
        public static string Label(AccountXpLine l)=>!string.IsNullOrWhiteSpace(l?.label)?l.label.Trim():!string.IsNullOrWhiteSpace(l?.key)?l.key.Trim().ToUpperInvariant():"XP";
        /// <summary>The authority's reason, trimmed to MaxReason characters (shown verbatim).</summary>
        public static string Reason(string r){if(string.IsNullOrWhiteSpace(r))return null;r=r.Trim();return r.Length>NativeAccountXpContract.MaxReason?r.Substring(0,NativeAccountXpContract.MaxReason-1)+"…":r;}
    }

    /// <summary>
    /// The XP palette (0xRRGGBB, NativeUi.Hex): an ultraviolet of its own, away from the wallet's gold (NativeCyberTheme.Warn) and
    /// the chips' cyan, so XP never reads as currency or an in-run chip. Soft is the text tone (4.5:1 on the navy glass); Bar and Glow
    /// are decoration; Deep is the bar's track and the badge's fill.
    /// </summary>
    public static class NativeAccountXpTone
    {
        public const int Bar=0x9d7bff,Soft=0xcfc2ff,Glow=0xb59cff,Deep=0x2a1f66,Hatch=0x7a63d9;
    }
}
