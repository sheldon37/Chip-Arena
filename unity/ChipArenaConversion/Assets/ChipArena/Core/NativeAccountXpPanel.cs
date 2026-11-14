using System;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The post-match XP panel's frame (Claude, 2026-10-19; the contract in NativeAccountXp.cs). One instance per screen, single
    /// thread; Build every repaint with the award the authority supplies (or null), this client's unscaled clock, the reduced-effects
    /// setting and the local record of completions already celebrated.
    ///  - Pending: the saved level and progress, the pending gain as a ghost on the bar, the lines rising in, "CONFIRMING REWARD…",
    ///    "Not saved to your account yet." No level-up, no "saved" or "earned" wording.
    ///  - Failed: the same, nothing saved, with the authority's reason and its retry state (a countdown, Try again, later, or none).
    ///  - Confirmed, first time ever for this account and completion: the lines lock in one by one, then the bar fills from the old
    ///    total to the new one inside the supplied thresholds, a level-up pop for every level crossed (a chain for several), then
    ///    the final state and "SAVED TO YOUR ACCOUNT". The completion is written to the local record when the animation starts.
    ///  - Confirmed again (reopened, re-issued, another session, or reduced effects): the final state at once, nothing replayed.
    /// Nothing here decides an amount, a level, a threshold or a save. Allocation-free per frame: words are built when the award
    /// changes, fixed arrays hold the timeline and the lines.
    /// </summary>
    public sealed class NativeAccountXpPanel
    {
        /// <summary>Lines drawn (more show as "+n MORE" in the last row), and level segments animated one by one (more collapse into one
        /// jump with a single "LEVEL UP ×n").</summary>
        public const int MaxShownLines=8,MaxSegments=32;
        /// <summary>Motion (s): the lines' first entrance and stagger and their rise, the gap before the fill, a whole bar's fill, the
        /// shortest segment, a level-up's hold (and its floor when a long chain is compressed), the chain's soft cap, the settle.</summary>
        public const float LinesAt=.35f,LineStep=.2f,LineRise=.32f,FillGap=.22f,FullBar=1.15f,MinSegment=.2f,Hold=.68f,MinHold=.24f,MaxFill=4.8f,Settle=.45f;
        /// <summary>A level-up's badge pop and burst ring (sampled from UpAge).</summary>
        public const float Pop=.42f,Burst=.6f;
        /// <summary>UpAge with no level-up yet.</summary>
        public const float Never=1e6f;

        // ---- the frame (the HUD reads these) ------------------------------------------------------------------------------------------
        /// <summary>Draw the panel; the award may be shown at all; its numbers are consistent; the bar can be drawn.</summary>
        public bool Shown,Valid,Usable,Bars;
        /// <summary>A developer is viewing (the DEV flaw line); a DEV preview fixture (the MOCK tag).</summary>
        public bool Dev,Mock;
        public AccountXpStatus Status;public AccountXpRetry Retry;public AccountXpFlaw Flaws;
        /// <summary>The level the badge shows now, levels crossed so far, levels in this award, before and after.</summary>
        public int Level,Ups,Levels,PreviousLevel,NewLevel;
        /// <summary>The bar's solid fill in the shown level (0..1; -1 no bar), the ghost's end (pending or failed gain; -1 none) and
        /// whether the ghost runs past this level, the top level shown.</summary>
        public float Fill,Ghost;public bool GhostOver,Max;
        /// <summary>Seconds since the current level-up began (Never: none), the summary's and the progress line's fade (0..1).</summary>
        public float UpAge,SummaryIn,ProgressIn;
        /// <summary>Seconds since this award was first seen, and since its current issue (revision or status) was.</summary>
        public float Age,IssueAge;
        /// <summary>The confirmed animation is running; the panel stands at its final state.</summary>
        public bool Animating,Final;
        /// <summary>The failed award may be retried by the player (retry "manual"): the HUD shows Try again.</summary>
        public bool CanRetry;
        public string Mode,GainText,ProgressText,NextText,UpText,Summary,StatusText,DetailText,RetryText,AttemptText,MoreText,FlawText;
        /// <summary>Breakdown lines held (at most MaxShownLines-1 when more follow), and how many more the authority listed (the
        /// "+n MORE" row). The HUD draws Rows of them, fewer when the layout clips.</summary>
        public int Lines,More;
        public int Rows=>Lines+(More>0?1:0);
        public readonly string[] LineLabel=new string[MaxShownLines],LineAmount=new string[MaxShownLines];
        /// <summary>Each row's entrance (0..1) and its lock-in once confirmed (0 pending or failed, 1 saved).</summary>
        public readonly float[] LineIn=new float[MaxShownLines],LineLock=new float[MaxShownLines];
        public readonly bool[] LineNegative=new bool[MaxShownLines];
        public AccountXpCheck Check{get;private set;}
        public string CompletionId{get;private set;}
        /// <summary>The local record's key for this award (account and completion), or null.</summary>
        public string Key{get;private set;}
        public string LevelText=>NativeAccountXpText.Level(Level);
        public string BadgeText=>NativeAccountXpText.Number(Level);
        /// <summary>The fill timeline's start and end (s after the animation began), for the HUD's own accents and the checks.</summary>
        public float FillStart=>fillStart;public float FillEnd=>fillEnd;

        // ---- state --------------------------------------------------------------------------------------------------------------------
        private AccountXpAward source;private int seenRevision,seenAttempt,seenLines,seenBands,seenPreviousLevel,seenNewLevel;private long seenPrevious,seenNew;
        private string seenStatus,seenRetry,seenReason,seenId,seenAccount,seenMode,seenViewer;private float seenRetryIn;private int seenVersion;
        private float firstSeen,issueAt,animStart=float.NaN;
        private string progressFrom,progressTo,nextFrom,nextTo,upsAll;
        private int segs;private float fillStart,fillEnd,hold;
        private readonly int[] segLevel=new int[MaxSegments+1],segUps=new int[MaxSegments+1];
        private readonly float[] segFrom=new float[MaxSegments+1],segTo=new float[MaxSegments+1],segStart=new float[MaxSegments+1],segDur=new float[MaxSegments+1],holdAt=new float[MaxSegments+1];

        /// <summary>No panel this frame (the completion's memory is kept: the same award coming back does not replay).</summary>
        public void Clear(){Shown=false;Status=AccountXpStatus.None;}

        /// <summary>DEV preview only: forget that the current mock completion was celebrated, so it plays again.</summary>
        public void Replay(NativeAccountXpSeen seen){if(!Mock||Key==null)return;seen?.ForgetMock(Key);animStart=float.NaN;}

        /// <summary>
        /// The frame for award `a` as `viewer` (null: any account), at unscaled time `now`. `seen` is the local record (null: none,
        /// every confirmed award animates). `dev`: a developer views (an award the players would not see is drawn with its flaws).
        /// </summary>
        public void Build(AccountXpAward a,string viewer,float now,bool reduced,NativeAccountXpSeen seen,bool dev)
        {
            if(a==null){Clear();return;}
            // A producer may hand a new object each frame: only a changed value rebuilds the words and the timeline.
            if(source==null||viewer!=seenViewer||Changed(a))Refresh(a,viewer,now);else source=a;
            Dev=dev;var c=Check;Shown=c.Shown||dev;Valid=c.Shown;Age=Math.Max(0,now-firstSeen);IssueAge=Math.Max(0,now-issueAt);
            Status=c.Shown?c.Status:AccountXpStatus.None;
            Level=PreviousLevel;Ups=0;UpAge=Never;Fill=Bars?c.FromFill:-1;Ghost=-1;GhostOver=false;Max=c.FromMax;
            SummaryIn=0;ProgressIn=1;ProgressText=progressFrom;NextText=nextFrom;Animating=false;Final=true;UpText=null;
            if(!Shown||!Usable){for(int i=0;i<Lines;i++){LineIn[i]=1;LineLock[i]=0;}return;}
            if(Status==AccountXpStatus.Confirmed)
            {
                // Once ever: the first sight of this confirmed completion (reduced effects or an earlier celebration: the final state).
                if(float.IsNaN(animStart))
                {
                    bool done=reduced||seen!=null&&seen.Contains(Key);seen?.Mark(Key,Mock);
                    animStart=done?float.NegativeInfinity:now;
                }
                if(float.IsNegativeInfinity(animStart)||reduced)FinalState();else Sample(now-animStart,reduced);
                UpText=Ups<=0?null:Ups>=MaxSegments&&upsAll!=null?upsAll:NativeAccountXpText.Ups(Ups);return;
            }
            // Pending or failed: the saved state with the gain as a ghost; the lines rise in on first sight, nothing locks in.
            if(Bars&&!c.FromMax){Ghost=c.Levels>0?1:c.ToFill;GhostOver=c.Levels>0;}
            for(int i=0;i<Lines;i++){LineIn[i]=reduced?1:Rise(Age-(LinesAt+i*LineStep));LineLock[i]=0;}
            if(Status==AccountXpStatus.Failed&&Retry==AccountXpRetry.Auto)RetryText=NativeAccountXpText.RetryIn(source.retryIn>0&&!float.IsInfinity(source.retryIn)?source.retryIn-IssueAge:0);
        }

        private static float Rise(float t)=>NativeUiMotion.Bezier(.2f,.8f,.2f,1,NativeUiMotion.Clamp01(t/LineRise));

        // The confirmed animation at t seconds: the lines lock in, then the fill runs its segments with a hold at every level crossed.
        private void Sample(float t,bool reduced)
        {
            for(int i=0;i<Lines;i++){float at=LinesAt+i*LineStep;LineIn[i]=Math.Max(reduced?1:Rise(Age-at),Rise(t-at));LineLock[i]=Rise(t-at);}
            var c=Check;Animating=true;Final=false;
            if(t>=fillEnd)
            {
                Level=NewLevel;Ups=Levels;Fill=Bars?c.ToFill:-1;Max=c.ToMax;UpAge=segs>1?t-holdAt[segs-2]:Never;
                SummaryIn=NativeUiMotion.Clamp01((t-fillEnd)/Settle);ProgressText=progressTo;NextText=nextTo;ProgressIn=SummaryIn;
                if(t>=fillEnd+Settle&&UpAge>=Burst){Animating=false;Final=true;}
                return;
            }
            // Before the fill the old progress shows, fading out as the fill starts; during it neither (the bar is the number).
            if(t<fillStart){ProgressIn=1-NativeUiMotion.Clamp01((t-(fillStart-.15f))/.15f);}else{ProgressIn=0;ProgressText=progressTo;NextText=nextTo;}
            for(int j=0;j<segs;j++)
            {
                if(t<segStart[j]+segDur[j])
                {
                    float u=segDur[j]>0?NativeUiMotion.Clamp01((t-segStart[j])/segDur[j]):(t>=segStart[j]?1:0);
                    Level=segLevel[j];Ups=segUps[j];UpAge=j>0?t-holdAt[j-1]:Never;Max=j==segs-1?c.ToMax:j==0&&c.FromMax;
                    Fill=Bars?segFrom[j]+(segTo[j]-segFrom[j])*NativeUiMotion.EaseInOut(u):-1;return;
                }
                if(j<segs-1&&t<holdAt[j]+hold){Level=segLevel[j+1];Ups=segUps[j+1];UpAge=t-holdAt[j];Fill=Bars?1:-1;Max=false;return;}
            }
            Level=NewLevel;Ups=Levels;Fill=Bars?c.ToFill:-1;
        }
        private void FinalState()
        {
            var c=Check;Level=NewLevel;Ups=Levels;UpAge=Never;Fill=Bars?c.ToFill:-1;Max=c.ToMax;SummaryIn=1;ProgressIn=1;ProgressText=progressTo;NextText=nextTo;
            for(int i=0;i<Lines;i++){LineIn[i]=1;LineLock[i]=1;}Animating=false;Final=true;
        }

        private bool Changed(AccountXpAward a)=>a.revision!=seenRevision||a.status!=seenStatus||a.previousTotal!=seenPrevious||a.newTotal!=seenNew||a.previousLevel!=seenPreviousLevel||a.newLevel!=seenNewLevel||
            (a.lines?.Length??-1)!=seenLines||(a.bands?.Length??-1)!=seenBands||a.attempt!=seenAttempt||a.retry!=seenRetry||!a.retryIn.Equals(seenRetryIn)||a.reason!=seenReason||
            a.completionId!=seenId||a.account!=seenAccount||a.mode!=seenMode||a.version!=seenVersion;

        // A new award, or a new issue of it: validate, build its words and its timeline (allocates; never per frame).
        private void Refresh(AccountXpAward a,string viewer,float now)
        {
            bool fresh=source==null||a.completionId!=seenId||a.account!=seenAccount;bool reissue=fresh||a.revision!=seenRevision||a.status!=seenStatus;
            source=a;seenViewer=viewer;seenMode=a.mode;seenVersion=a.version;seenRevision=a.revision;seenStatus=a.status;seenPrevious=a.previousTotal;seenNew=a.newTotal;seenPreviousLevel=a.previousLevel;seenNewLevel=a.newLevel;
            seenLines=a.lines?.Length??-1;seenBands=a.bands?.Length??-1;seenAttempt=a.attempt;seenRetry=a.retry;seenRetryIn=a.retryIn;seenReason=a.reason;seenId=a.completionId;seenAccount=a.account;
            var c=NativeAccountXpContract.Check(a,viewer);Check=c;Usable=c.Usable;Bars=c.Bars;Retry=c.Retry;Flaws=c.Flaws;Mock=NativeAccountXpContract.Mock(a);
            string key=c.Shown?NativeAccountXpSeen.KeyOf(a.account,a.completionId):null;
            if(key!=Key||fresh&&key==null){Key=key;CompletionId=a.completionId;firstSeen=now;animStart=float.NaN;}
            if(reissue)issueAt=now;
            PreviousLevel=c.Usable?a.previousLevel:0;NewLevel=c.Usable?a.newLevel:0;Levels=c.Usable?c.Levels:0;
            Mode=NativeAccountXpText.Mode(a.mode);GainText=c.Usable?NativeAccountXpText.Xp(c.Gain):null;FlawText=NativeAccountXpContract.Describe(c.Flaws);
            // The breakdown as given; past MaxShownLines the last row says how many more.
            int n=0,total=0;if(a.lines!=null)for(int i=0;i<a.lines.Length&&i<NativeAccountXpContract.MaxLines;i++)if(a.lines[i]!=null)total++;
            int shown=total>MaxShownLines?MaxShownLines-1:total;More=total-shown;MoreText=NativeAccountXpText.More(More);
            if(c.Usable&&a.lines!=null)for(int i=0;i<a.lines.Length&&n<shown;i++){var l=a.lines[i];if(l==null)continue;LineLabel[n]=NativeAccountXpText.Label(l);LineAmount[n]=NativeAccountXpText.Xp(l.xp);LineNegative[n]=l.xp<0;n++;}
            Lines=c.Usable?n:0;if(!c.Usable){More=0;MoreText=null;}
            // The progress lines, before and after, from the supplied thresholds only.
            progressFrom=progressTo=nextFrom=nextTo=null;Summary=null;
            if(c.Bars){progressFrom=NativeAccountXpText.Progress(a.previousTotal,a.bands[c.From]);progressTo=NativeAccountXpText.Progress(a.newTotal,a.bands[c.To]);nextFrom=NativeAccountXpText.Next(a.bands[c.From]);nextTo=NativeAccountXpText.Next(a.bands[c.To]);}
            else if(c.Usable){progressFrom=a.previousTotal.ToString("#,0",System.Globalization.CultureInfo.InvariantCulture)+" XP";progressTo=a.newTotal.ToString("#,0",System.Globalization.CultureInfo.InvariantCulture)+" XP";}
            if(c.Usable&&c.Levels>0)Summary=NativeAccountXpText.Climb(a.previousLevel,a.newLevel);
            upsAll=c.Usable&&c.Levels>=MaxSegments?NativeAccountXpText.LevelUp+" ×"+c.Levels.ToString(System.Globalization.CultureInfo.InvariantCulture):null;
            // The status words: saved only once confirmed.
            CanRetry=false;RetryText=null;AttemptText=null;DetailText=null;
            switch(c.Shown?c.Status:AccountXpStatus.None)
            {
                case AccountXpStatus.Confirmed:StatusText=NativeAccountXpText.Saved;break;
                case AccountXpStatus.Pending:StatusText=NativeAccountXpText.Confirming;DetailText=NativeAccountXpText.NotSavedYet;AttemptText=NativeAccountXpText.Attempt(a.attempt);break;
                case AccountXpStatus.Failed:
                    StatusText=NativeAccountXpText.FailedTitle;DetailText=NativeAccountXpText.Reason(a.reason)??NativeAccountXpText.FailedDetail;AttemptText=NativeAccountXpText.Attempt(a.attempt);
                    switch(c.Retry)
                    {
                        case AccountXpRetry.Auto:RetryText=NativeAccountXpText.RetryingNow;break;
                        case AccountXpRetry.Manual:RetryText=NativeAccountXpText.TryAgain;CanRetry=true;break;
                        case AccountXpRetry.Later:RetryText=NativeAccountXpText.RetryLater;break;
                        case AccountXpRetry.None:RetryText=NativeAccountXpText.NoRetry;break;
                    }
                    break;
                default:StatusText=NativeAccountXpText.Invalid;break;
            }
            if(c.Shown&&!c.Usable)DetailText=c.Status==AccountXpStatus.Failed?DetailText:NativeAccountXpText.Unavailable;
            BuildTimeline(c);
        }

        // The fill: one segment per level from the previous total to the new one (the bar's position inside each supplied band), with
        // a hold at every threshold crossed. A long chain is compressed toward MaxFill (holds no shorter than MinHold); past
        // MaxSegments levels it collapses into two segments and one "LEVEL UP ×n". Without bars the holds still chain the levels.
        private void BuildTimeline(in AccountXpCheck c)
        {
            segs=0;fillStart=LinesAt+(Lines>0?(Lines-1)*LineStep+LineRise:0)+FillGap;fillEnd=fillStart;hold=Hold;if(!c.Usable)return;
            int k=c.Levels;float from=c.Bars?c.FromFill:0,to=c.Bars?c.ToFill:0;
            if(k>=MaxSegments){Add(PreviousLevel,0,from,1);Add(NewLevel,k,0,to);}
            else for(int j=0;j<=k;j++)Add(PreviousLevel+j,j,j==0?from:0,j==k?to:1);
            float run=0;for(int j=0;j<segs;j++){float d=segTo[j]-segFrom[j];segDur[j]=c.Bars&&d>0?Math.Max(MinSegment,d*FullBar):0;run+=segDur[j];}
            int holds=segs-1;float total=run+holds*Hold;
            if(total>MaxFill){float s=MaxFill/total;hold=Math.Max(MinHold,Hold*s);float left=Math.Max(MaxFill*.35f,MaxFill-holds*hold);if(run>left){float k2=left/run;for(int j=0;j<segs;j++)segDur[j]*=k2;}}
            float t=fillStart;for(int j=0;j<segs;j++){segStart[j]=t;t+=segDur[j];if(j<holds){holdAt[j]=t;t+=hold;}}
            fillEnd=t;
        }
        private void Add(int level,int ups,float from,float to){segLevel[segs]=level;segUps[segs]=ups;segFrom[segs]=from;segTo[segs]=to;segs++;}

        /// <summary>Checks only: the timeline's segments (level, levels crossed at its start, fill from, fill to, start, duration).</summary>
        internal int Segments=>segs;
        internal void Segment(int j,out int level,out int ups,out float from,out float to,out float start,out float duration,out float holdStart)
        {level=segLevel[j];ups=segUps[j];from=segFrom[j];to=segTo[j];start=segStart[j];duration=segDur[j];holdStart=j<segs-1?holdAt[j]:-1;}
        internal float HoldLength=>hold;
    }

    /// <summary>
    /// The local record of completions already celebrated (UI state only: never account XP, never sent anywhere). Bounded: the
    /// newest Capacity keys, oldest dropped first. DEV preview fixtures (mock ids) are kept in memory only. Text: a header line, then
    /// one key per line ("account TAB completionId"); anything unreadable is skipped, a wrong header reads as empty.
    /// </summary>
    public sealed class NativeAccountXpSeen
    {
        public const int Capacity=200,MaxKey=300;public const string Header="ChipArena account-xp celebrated v1";
        public const string File="account-xp-celebrated-v1.txt";
        private readonly string[] keys=new string[Capacity];private int next,count;
        private readonly string[] mock=new string[16];private int mockNext;
        /// <summary>A key was added since the last save.</summary>
        public bool Dirty{get;private set;}
        public int Count=>count;
        public static string KeyOf(string account,string completionId)=>(account??"")+"\t"+completionId;
        public bool Contains(string key)
        {
            if(string.IsNullOrEmpty(key))return false;for(int i=0;i<count;i++)if(keys[i]==key)return true;
            for(int i=0;i<mock.Length;i++)if(mock[i]==key)return true;return false;
        }
        /// <summary>Records `key`; true when it was new. A mock key is remembered for this session only.</summary>
        public bool Mark(string key,bool isMock)
        {
            if(string.IsNullOrEmpty(key)||key.Length>MaxKey||Contains(key))return false;
            if(isMock){mock[mockNext]=key;mockNext=(mockNext+1)%mock.Length;return true;}
            keys[next]=key;next=(next+1)%Capacity;if(count<Capacity)count++;Dirty=true;return true;
        }
        public void ForgetMock(string key){for(int i=0;i<mock.Length;i++)if(mock[i]==key)mock[i]=null;}
        public void Saved()=>Dirty=false;
        /// <summary>The record as text, oldest first (mock keys left out).</summary>
        public string Serialize()
        {
            var s=new StringBuilder(Header).Append('\n');int start=count<Capacity?0:next;
            for(int i=0;i<count;i++)s.Append(keys[(start+i)%Capacity]).Append('\n');return s.ToString();
        }
        public static NativeAccountXpSeen Parse(string text)
        {
            var r=new NativeAccountXpSeen();if(string.IsNullOrEmpty(text))return r;var lines=text.Split('\n');
            if(lines.Length==0||lines[0].TrimEnd('\r')!=Header)return r;
            for(int i=1;i<lines.Length;i++)
            {
                var k=lines[i].TrimEnd('\r');if(k.Length==0||k.Length>MaxKey||k.IndexOf('\t')<0)continue;bool ok=true;
                foreach(char ch in k)if(ch<' '&&ch!='\t'){ok=false;break;}if(ok&&!k.Substring(k.IndexOf('\t')+1).StartsWith(NativeAccountXpContract.MockPrefix,StringComparison.Ordinal))r.Mark(k,false);
            }
            r.Dirty=false;return r;
        }
    }

    /// <summary>The main menu's account level (NativeAccountXpContract.Check on the summary): the saved level and progress only; a
    /// pending award shows a marker and never adds to the total. Hidden when unknown. Allocation-free.</summary>
    public sealed class NativeAccountXpBadge
    {
        public const string Pending="PENDING",MockTag="MOCK";
        public bool Shown,Bar,Max,HasPending,Mock;public int Level;public float Fill;public AccountXpFlaw Flaws;
        public string LevelText=>NativeAccountXpText.Level(Level);public string Number=>NativeAccountXpText.Number(Level);
        public void Build(AccountXpSummary s,string viewer)
        {
            var c=NativeAccountXpContract.Check(s,viewer);Shown=c.Shown;Bar=c.Bar;Max=c.Max;Level=c.Level;Fill=c.Fill;HasPending=c.Shown&&c.Pending>0;Flaws=c.Flaws;
            Mock=c.Shown&&NativeAccountXpContract.Mock(s);
        }
    }

    /// <summary>A box in canvas units.</summary>
    public struct AccountXpBox
    {
        public float X,Y,W,H;public AccountXpBox(float x,float y,float w,float h){X=x;Y=y;W=w;H=h;}
        public float Right=>X+W;public float Bottom=>Y+H;
        public bool Inside(float width,float height)=>X>=-.01f&&Y>=-.01f&&Right<=width+.01f&&Bottom<=height+.01f&&W>=0&&H>=0;
        public bool Overlaps(AccountXpBox o)=>X<o.Right&&o.X<Right&&Y<o.Bottom&&o.Y<Bottom;
        public bool Within(AccountXpBox o)=>X>=o.X-.01f&&Y>=o.Y-.01f&&Right<=o.Right+.01f&&Bottom<=o.Bottom+.01f;
    }

    /// <summary>
    /// Where the XP panel goes (canvas units, NativeUiMotion.Measure). Docked: a column of its own beside the PvP or PvE results,
    /// the two centred together, the results narrowed to make room (never below their minimum); the panel's top level with the
    /// results'. Card: centred over the Expedition screen with a Close row. Mini (a canvas too narrow for both): a compact card at the
    /// top right without the breakdown. A tall panel turns compact, then clips its lines (the last row says +n MORE).
    /// </summary>
    public struct NativeAccountXpLayout
    {
        public const float Gap=16,Margin=16,MinWidth=300,MaxWidth=380,MiniWidth=264,CardWidth=440;
        public AccountXpBox Panel;public float HostX,HostW;public bool Docked,Card,Mini,Compact;
        /// <summary>Rows: the inner left and width; the eyebrow, title, bar, progress, first rule, first line, second rule, status,
        /// detail, retry, DEV and foot rows' tops; one line's height; the lines that fit (the breakdown rows drawn).</summary>
        public float InnerX,InnerW,HeadY,TitleY,BarY,ProgressY,RuleA,LinesY,RuleB,StatusY,DetailY,RetryY,DevY,FootY,Row,BadgeSize,TitleH;
        public int ShownLines;

        /// <summary>The XP column's width on a canvas W wide.</summary>
        public static float Column(float W)=>Math.Max(MinWidth,Math.Min(MaxWidth,(W-2*Margin)*.26f));
        /// <summary>The XP panel docks beside a host of at least hostMin on this canvas.</summary>
        public static bool Fits(float W,float hostMin)=>W-2*Margin-Column(W)-Gap>=hostMin;
        /// <summary>The width the host may take: the canvas minus the XP column when it docks, else the whole canvas.</summary>
        public static float HostRoom(float W,float hostMin,bool xp)=>xp&&Fits(W,hostMin)?W-Column(W)-Gap:W;
        /// <summary>The host's left edge: centred with the XP column when docked, else centred alone.</summary>
        public static float HostLeft(float W,float hostW,float hostMin,bool xp)=>xp&&Fits(W,hostMin)?Math.Max(Margin,(W-(hostW+Gap+Column(W)))/2):(W-hostW)/2;

        private struct Sizes{public float Pad,Head,HeadGap,Title,TitleGap,Bar,BarGap,Progress,Rule,Line,Status,Detail,Retry,Dev,Foot,Badge;}
        private static Sizes Of(bool compact)=>compact
            ?new Sizes{Pad=12,Head=14,HeadGap=6,Title=38,TitleGap=8,Bar=8,BarGap=4,Progress=14,Rule=15,Line=19,Status=18,Detail=14,Retry=24,Dev=12,Foot=34,Badge=34}
            :new Sizes{Pad=18,Head=16,HeadGap=10,Title=48,TitleGap=12,Bar=10,BarGap=6,Progress=16,Rule=23,Line=24,Status=20,Detail=16,Retry=28,Dev=14,Foot=40,Badge=44};
        private static int DetailLines(AccountXpStatus s)=>s==AccountXpStatus.Failed?2:s==AccountXpStatus.Pending?1:0;
        private static bool RetryRow(AccountXpStatus s)=>s==AccountXpStatus.Failed;

        /// <summary>The panel's height for `lines` breakdown rows (0 shows one "no XP" row), its status, a DEV line, a Close row.</summary>
        public static float Height(int lines,AccountXpStatus status,bool dev,bool card,bool compact,bool mini=false)
        {
            var z=Of(compact);float h=2*z.Pad+z.Head+z.HeadGap+z.Title+z.TitleGap+z.Bar+z.BarGap+z.Progress+z.Rule+z.Status;
            if(!mini)h+=Math.Max(1,lines)*z.Line+z.Rule+DetailLines(status)*z.Detail+(RetryRow(status)?z.Retry:0);
            return h+(dev?z.Dev:0)+(card?z.Foot:0);
        }

        /// <summary>Docks the panel beside a host at hostX, hostW, hostY (the host already narrowed by HostRoom), or places the mini card.</summary>
        public static NativeAccountXpLayout Dock(float W,float H,float hostX,float hostW,float hostY,float hostMin,int lines,AccountXpStatus status,bool dev)
        {
            var l=new NativeAccountXpLayout{HostX=hostX,HostW=hostW};
            if(!Fits(W,hostMin)){l.Mini=true;float mw=Math.Min(MiniWidth,W-2*Margin);float mh=Height(0,status,dev,false,true,true);l.Compact=true;l.Panel=new AccountXpBox(W-Margin-mw,Margin,mw,Math.Min(mh,H-2*Margin));l.Rows(0,status,dev);return l;}
            float w=Column(W);l.Docked=true;Fit(ref l,H,lines,status,dev,false,out float h);
            float y=Math.Max(Margin,Math.Min(hostY,H-Margin-h));l.Panel=new AccountXpBox(hostX+hostW+Gap,y,w,h);l.Rows(lines,status,dev);return l;
        }
        /// <summary>The Expedition card: centred, at most CardWidth wide, with its Close row.</summary>
        public static NativeAccountXpLayout CardOn(float W,float H,int lines,AccountXpStatus status,bool dev)
        {
            var l=new NativeAccountXpLayout{Card=true};float w=Math.Min(CardWidth,W-2*Margin);Fit(ref l,H,lines,status,dev,true,out float h);
            l.Panel=new AccountXpBox((W-w)/2,Math.Max(Margin,(H-h)/2),w,h);l.HostX=l.Panel.X;l.HostW=0;l.Rows(lines,status,dev);return l;
        }
        // Normal rows, else compact, else as many lines as fit.
        private static void Fit(ref NativeAccountXpLayout l,float H,int lines,AccountXpStatus status,bool dev,bool card,out float h)
        {
            float room=H-2*Margin;int n=Math.Max(0,lines);l.ShownLines=n;l.Compact=false;h=Height(n,status,dev,card,false);
            if(h>room){l.Compact=true;h=Height(n,status,dev,card,true);}
            if(h>room){var z=Of(true);int fit=Math.Max(1,(int)Math.Floor((room-Height(0,status,dev,card,true)+z.Line)/z.Line));l.ShownLines=Math.Min(n,fit);h=Math.Min(room,Height(l.ShownLines,status,dev,card,true));}
        }
        private void Rows(int lines,AccountXpStatus status,bool dev)
        {
            var z=Of(Compact);var p=Panel;InnerX=p.X+z.Pad;InnerW=p.W-2*z.Pad;Row=z.Line;BadgeSize=z.Badge;TitleH=z.Title;
            float y=p.Y+z.Pad;HeadY=y;y+=z.Head+z.HeadGap;TitleY=y;y+=z.Title+z.TitleGap;BarY=y;y+=z.Bar+z.BarGap;ProgressY=y;y+=z.Progress;
            RuleA=y+z.Rule*.45f;y+=z.Rule;
            if(Mini){LinesY=RuleB=y;ShownLines=0;StatusY=y;y+=z.Status;DetailY=RetryY=y;}
            else
            {
                LinesY=y;y+=Math.Max(1,ShownLines)*z.Line;RuleB=y+z.Rule*.45f;y+=z.Rule;StatusY=y;y+=z.Status;DetailY=y;y+=DetailLines(status)*z.Detail;RetryY=y;y+=RetryRow(status)?z.Retry:0;
            }
            DevY=y;y+=dev?z.Dev:0;FootY=y;
        }
        /// <summary>Breakdown row i's box (the rows that fit).</summary>
        public AccountXpBox LineBox(int i)=>new AccountXpBox(InnerX,LinesY+i*Row,InnerW,Row);
    }
}
