#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Permanent account XP, the presentation side (Claude, 2026-10-19; Core NativeAccountXp, NativeAccountXpPanel,
    // NativeAccountXpFixtures; Runtime NativeAccountXpHud, NativeAccountXpUI; docs/ACCOUNT-XP-PRESENTATION.md): the proposed
    // contract's validation and graceful degradation, the animation of every fixture (single and chained level-ups crossing exactly
    // the supplied thresholds, never computing one), pending never shown as saved and never added to the menu's total, the failed
    // state's retry messaging, animate-once across reopen, restart and pending → confirmed, the bounded local record, separation
    // from chips and currency, the unbound adapters and the tripwire, zero per-frame allocation, the layouts at 720p @1.5 through
    // 4K and 21:9, and the wiring: presentation only, no authority, network or account-write file touched.
    internal static class AccountXpChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Account XP: "+what);count++;}
        private const string Viewer="nova";
        // Pending and failed awards never read as saved: no confirmed phrase, no earned/secured/banked, and SAVED only as NOT SAVED.
        private static readonly Regex SavedWording=new Regex(@"\bEARNED\b|\bSECURED\b|\bBANKED\b|(?<!NOT )\bSAVED\b");

        private static AccountXpAward Award(string status="confirmed",long from=2340,int fromLevel=4,long to=2760,int toLevel=4,AccountXpLine[] lines=null,AccountXpBand[] bands=null,string id="c-1",string account=Viewer)
            =>new AccountXpAward{version=1,completionId=id,account=account,mode="pvp",status=status,revision=1,previousTotal=from,newTotal=to,previousLevel=fromLevel,newLevel=toLevel,
                lines=lines??new[]{new AccountXpLine{key="playtime",label="Active playtime",xp=to-from}},bands=bands??NativeAccountXpFixtures.MockBands(fromLevel,toLevel),attempt=1};

        public static void Run(string root)
        {
            count=0;var R=new Func<string,string>(f=>File.ReadAllText(Path.Combine(root,f)));
            Contract();Summary();Fixtures();Sequences();Pending();Failed();Once();Record();Separation(R);Adapters(R,root);Allocation();Layouts();Wiring(R,root);
            Console.WriteLine("PASS account XP: "+count+" checks (contract validation and degradation; every fixture's sequence inside the supplied thresholds, single and chained level-ups; pending never saved and never added to the menu; failed retry states; animate once across reopen, restart and pending → confirmed; bounded local record; apart from chips and currency; adapters unbound with the tripwire; allocation-free; layouts 720p @1.5 to 4K and 21:9; presentation only)");
        }

        // ---- the contract: validation and graceful degradation ------------------------------------------------------------------------
        private static void Contract()
        {
            var ok=NativeAccountXpContract.Check(Award(),Viewer);
            Check(ok.Shown&&ok.Usable&&ok.Bars&&ok.Status==AccountXpStatus.Confirmed&&ok.Flaws==AccountXpFlaw.None&&ok.Gain==420&&ok.LineSum==420&&ok.Levels==0,"a consistent award passes as given");
            Check(Math.Abs(ok.FromFill-.34f)<1e-6f&&Math.Abs(ok.ToFill-.76f)<1e-6f&&ok.From==0&&ok.To==0,"positions inside the supplied band (2,340 and 2,760 of 2,000..3,000)");
            Check(!NativeAccountXpContract.Check((AccountXpAward)null,Viewer).Shown&&NativeAccountXpContract.Check((AccountXpAward)null,Viewer).Flaws==AccountXpFlaw.NoAward,"no award: nothing");
            foreach(var (bad,flaw,name) in new (Action<AccountXpAward>,AccountXpFlaw,string)[]{
                (x=>x.status="saved",AccountXpFlaw.Status,"an unknown status"),(x=>x.status=null,AccountXpFlaw.Status,"no status"),(x=>x.status="Confirmed",AccountXpFlaw.Status,"a status in the wrong case"),
                (x=>x.completionId=null,AccountXpFlaw.NoId,"no completion id"),(x=>x.completionId="  ",AccountXpFlaw.NoId,"a blank id"),(x=>x.completionId="a\nb",AccountXpFlaw.NoId,"an id with a control character"),
                (x=>x.completionId=new string('x',129),AccountXpFlaw.NoId,"an id over 128"),(x=>x.version=0,AccountXpFlaw.Version,"no version (an absent field reads 0)"),
                (x=>x.account="someone-else",AccountXpFlaw.Account,"another account's award")})
            {
                var a=Award();bad(a);var c=NativeAccountXpContract.Check(a,Viewer);Check(!c.Shown&&!c.Usable&&(c.Flaws&flaw)!=0,name+": never shown to players");
                var p=new NativeAccountXpPanel();p.Build(a,Viewer,1,false,null,false);Check(!p.Shown,name+": the panel draws nothing");
                p.Build(a,Viewer,2,false,null,true);Check(p.Shown&&!p.Valid&&p.StatusText==NativeAccountXpText.Invalid&&p.FlawText!=null&&p.FlawText.StartsWith("DEV · "),name+": a developer sees it flagged");
            }
            Check(NativeAccountXpContract.Check(Award(account:null),Viewer).Shown&&NativeAccountXpContract.Check(Award(),null).Shown,"an award without an account, or a viewer not known yet, passes");
            {var a=Award();a.version=2;var c=NativeAccountXpContract.Check(a,Viewer);Check(c.Shown&&c.Usable&&(c.Flaws&AccountXpFlaw.Newer)!=0,"a newer version is shown, flagged in DEV");}
            // Totals and levels that do not hold together: shown, numbers withheld.
            foreach(var (a,name) in new[]{(Award(from:3000,to:2000),"totals backwards"),(Award(from:-5,to:10),"a negative total"),(Award(fromLevel:5,toLevel:4,bands:NativeAccountXpFixtures.MockBands(4,5)),"levels backwards"),(Award(fromLevel:-1,toLevel:0,bands:new AccountXpBand[0]),"a negative level")})
            {
                var c=NativeAccountXpContract.Check(a,Viewer);Check(c.Shown&&!c.Usable&&!c.Bars,name+": shown, numbers unusable");
                var p=new NativeAccountXpPanel();p.Build(a,Viewer,1,false,null,false);
                Check(p.Shown&&p.GainText==null&&p.Lines==0&&p.Fill==-1&&p.DetailText==NativeAccountXpText.Unavailable&&p.StatusText==NativeAccountXpText.Saved,name+": the status as given, \"details unavailable\", no numbers");
            }
            // Thresholds: missing or not holding the totals → no bar, numbers still shown; never extended or guessed.
            var gap=NativeAccountXpFixtures.MockBands(4,5);gap[1].start+=10;
            var skip=NativeAccountXpFixtures.MockBands(4,5);skip[1].level=7;
            var outside=Award(from:2340,to:2760,bands:new[]{new AccountXpBand{level=4,start=2400,next=3000}});
            var topMiddle=NativeAccountXpFixtures.MockBands(4,6);topMiddle[1].next=topMiddle[1].start;
            foreach(var (a,flaw,name) in new[]{(Award(bands:new AccountXpBand[0]),AccountXpFlaw.NoBands,"no thresholds"),(NullBands(),AccountXpFlaw.NoBands,"null thresholds"),
                (Award(to:3380,toLevel:5,bands:gap),AccountXpFlaw.Bands,"a gap between bands"),(Award(to:3380,toLevel:5,bands:skip),AccountXpFlaw.Bands,"a skipped level"),
                (outside,AccountXpFlaw.Bands,"a total outside its band"),(Award(to:3380,toLevel:5,bands:NativeAccountXpFixtures.MockBands(5,5)),AccountXpFlaw.Bands,"the previous level not covered"),
                (Award(from:2760,to:4560,toLevel:6,bands:topMiddle),AccountXpFlaw.Bands,"a top band in the middle"),(Award(bands:new AccountXpBand[]{null}),AccountXpFlaw.Bands,"a null band")})
            {
                var c=NativeAccountXpContract.Check(a,Viewer);Check(c.Shown&&c.Usable&&!c.Bars&&(c.Flaws&flaw)!=0&&c.FromFill==-1&&c.ToFill==-1,name+": no bar");
                var p=new NativeAccountXpPanel();p.Build(a,Viewer,1,false,null,false);Check(p.Fill==-1&&p.GainText!=null&&p.ProgressText!=null&&!p.ProgressText.Contains("/"),name+": numbers shown, no progress fraction");
            }
            // The top level (a last band with no next): full, MAX LEVEL.
            {var top=new[]{new AccountXpBand{level=10,start=10400,next=0}};var a=Award(from:10500,fromLevel:10,to:10800,toLevel:10,bands:top);var c=NativeAccountXpContract.Check(a,Viewer);
             Check(c.Bars&&c.FromMax&&c.ToMax&&c.FromFill==1&&c.ToFill==1,"the top level: full bar");var p=new NativeAccountXpPanel();p.Build(a,Viewer,1,true,null,false);Check(p.Max&&p.ProgressText.Contains(NativeAccountXpText.MaxLevel)&&p.NextText==NativeAccountXpText.MaxLevel,"MAX LEVEL");}
            // The breakdown as given: a sum that differs from the totals is flagged (DEV), never corrected; a negative line shown as given.
            {
                var a=Award(lines:new[]{new AccountXpLine{key="a",label="One",xp=300},new AccountXpLine{key="b",label="Two",xp=90}});var c=NativeAccountXpContract.Check(a,Viewer);
                Check(c.Usable&&(c.Flaws&AccountXpFlaw.LineSum)!=0&&c.Gain==420&&c.LineSum==390,"lines ≠ totals flagged");var p=new NativeAccountXpPanel();p.Build(a,Viewer,1,true,null,true);
                Check(p.GainText=="+420 XP"&&p.LineAmount[0]=="+300 XP"&&p.LineAmount[1]=="+90 XP"&&p.FlawText.Contains("LINES DO NOT ADD UP"),"both shown as the authority gave them, the DEV line says so");
                var n=Award(to:2340,lines:new[]{new AccountXpLine{key="a",xp=20},new AccountXpLine{key="pen",label="Penalty",xp=-20}});c=NativeAccountXpContract.Check(n,Viewer);
                Check((c.Flaws&AccountXpFlaw.NegativeLine)!=0&&(c.Flaws&AccountXpFlaw.LineSum)==0,"a negative line flagged");p=new NativeAccountXpPanel();p.Build(n,Viewer,1,true,null,false);
                Check(p.LineAmount[1]=="−20 XP"&&p.LineNegative[1]&&p.LineLabel[0]=="A"&&p.GainText=="+0 XP","shown as given (−20 XP), a key for a missing label, +0 XP");
                var big=Award(lines:new[]{new AccountXpLine{xp=long.MaxValue},new AccountXpLine{xp=long.MaxValue}});c=NativeAccountXpContract.Check(big,Viewer);Check((c.Flaws&AccountXpFlaw.LineSum)!=0,"an overflowing sum is flagged, not wrapped");
                var holes=Award(lines:new[]{null,new AccountXpLine{key="x",label="X",xp=420},null});c=NativeAccountXpContract.Check(holes,Viewer);p=new NativeAccountXpPanel();p.Build(holes,Viewer,1,true,null,false);
                Check((c.Flaws&AccountXpFlaw.Lines)!=0&&p.Lines==1&&p.LineLabel[0]=="X","null lines skipped");
                var many=Award(lines:Enumerable.Range(0,12).Select(i=>new AccountXpLine{key="k"+i,label="Line "+i,xp=i==0?420-0:0}).ToArray());p=new NativeAccountXpPanel();p.Build(many,Viewer,1,true,null,false);
                Check(p.Lines==NativeAccountXpPanel.MaxShownLines-1&&p.More==12-p.Lines&&p.MoreText=="+5 MORE"&&p.Rows==NativeAccountXpPanel.MaxShownLines,"past eight lines: seven and \"+5 MORE\"");
                var huge=Award(lines:Enumerable.Range(0,40).Select(i=>new AccountXpLine{key="k",xp=0}).ToArray());Check((NativeAccountXpContract.Check(huge,Viewer).Flaws&AccountXpFlaw.Lines)!=0,"over 32 lines flagged");
            }
            Check(NativeAccountXpContract.ParseRetry("auto")==AccountXpRetry.Auto&&NativeAccountXpContract.ParseRetry("manual")==AccountXpRetry.Manual&&NativeAccountXpContract.ParseRetry("later")==AccountXpRetry.Later&&NativeAccountXpContract.ParseRetry("none")==AccountXpRetry.None&&NativeAccountXpContract.ParseRetry("AUTO")==AccountXpRetry.Unknown&&NativeAccountXpContract.ParseRetry(null)==AccountXpRetry.Unknown,"retry words");
            Check(NativeAccountXpContract.Fraction(long.MaxValue,new AccountXpBand{start=long.MinValue,next=long.MaxValue},out _)==1&&!float.IsNaN(NativeAccountXpContract.Fraction(5,new AccountXpBand{start=0,next=long.MaxValue},out _)),"fractions stay finite for any longs");
            // Fuzz: no award ever throws, and every frame value stays in range.
            var rng=new Random(19);
            for(int i=0;i<3000;i++)
            {
                var a=new AccountXpAward{version=rng.Next(-1,3),completionId=rng.Next(6)==0?null:"f"+rng.Next(50),account=rng.Next(3)==0?"x":Viewer,status=new[]{"pending","confirmed","failed","x",null}[rng.Next(5)],revision=rng.Next(4),
                    previousTotal=rng.Next(-100,5000),newTotal=rng.Next(-100,9000),previousLevel=rng.Next(-1,8),newLevel=rng.Next(-1,10),attempt=rng.Next(-1,5),retry=new[]{"auto","manual","later","none","?",null}[rng.Next(6)],retryIn=new[]{-1f,0,float.NaN,float.PositiveInfinity,5,600}[rng.Next(6)]};
                if(rng.Next(4)>0){int l0=Math.Max(1,Math.Min(9,a.previousLevel)),l1=Math.Max(l0,Math.Min(10,a.newLevel));a.bands=NativeAccountXpFixtures.MockBands(l0,l1);if(rng.Next(5)==0&&a.bands.Length>0)a.bands[rng.Next(a.bands.Length)].start+=rng.Next(-50,50);}
                if(rng.Next(3)>0)a.lines=Enumerable.Range(0,rng.Next(0,12)).Select(k=>rng.Next(9)==0?null:new AccountXpLine{key="k",label=rng.Next(2)==0?null:"L",xp=rng.Next(-50,900)}).ToArray();
                var p=new NativeAccountXpPanel();var seen=new NativeAccountXpSeen();
                for(float t=0;t<9;t+=.37f){p.Build(a,Viewer,10+t,rng.Next(5)==0,seen,rng.Next(2)==0);
                    Check(p.Fill>=-1&&p.Fill<=1&&p.Ghost>=-1&&p.Ghost<=1&&!float.IsNaN(p.Fill)&&!float.IsNaN(p.UpAge)&&p.Lines>=0&&p.Lines<=NativeAccountXpPanel.MaxShownLines,"fuzz: frame values in range");
                    if(p.Valid&&p.Usable)Check(p.Level>=a.previousLevel&&p.Level<=a.newLevel,"fuzz: the shown level stays between the supplied levels");}
            }
            Check(NativeAccountXpContract.Describe(AccountXpFlaw.None)==null&&NativeAccountXpContract.Describe(AccountXpFlaw.NoBands|AccountXpFlaw.LineSum)=="DEV · NO THRESHOLDS, LINES DO NOT ADD UP TO THE TOTALS","the DEV flaw line");
        }
        private static AccountXpAward NullBands(){var a=Award();a.bands=null;return a;}

        // ---- the menu summary ---------------------------------------------------------------------------------------------------------
        private static void Summary()
        {
            AccountXpSummary S(long total=2340,int level=4,long start=2000,long next=3000,int pending=0,bool known=true,int version=1,string account=Viewer)=>new AccountXpSummary{version=version,account=account,known=known,total=total,level=level,levelStart=start,nextLevelAt=next,pending=pending};
            var b=new NativeAccountXpBadge();
            b.Build(S(),Viewer);Check(b.Shown&&b.Bar&&b.Level==4&&Math.Abs(b.Fill-.34f)<1e-6f&&!b.HasPending&&!b.Max&&b.LevelText=="LEVEL 4"&&b.Number=="4","the saved level and its progress");
            float saved=b.Fill;b.Build(S(pending:2),Viewer);Check(b.Shown&&b.HasPending&&b.Fill==saved&&b.Level==4,"pending awards: a marker; the level and the bar unchanged (never added)");
            foreach(var (s,name) in new (AccountXpSummary,string)[]{(null,"none"),(S(known:false),"unknown"),(S(version:0),"no version"),(S(account:"x"),"another account"),(S(total:-1),"a negative total"),(S(level:-1),"a negative level")})
            {b.Build(s,Viewer);Check(!b.Shown,name+": no level shown");}
            b.Build(S(total:3500),Viewer);Check(b.Shown&&!b.Bar&&b.Level==4,"thresholds not holding the total: the level without a bar");
            b.Build(S(total:10500,level:10,start:10400,next:0),Viewer);Check(b.Shown&&b.Bar&&b.Max&&b.Fill==1,"the top level: full");
            b.Build(S(pending:-3),Viewer);Check(!b.HasPending,"a negative pending count is none");
            b.Build(NativeAccountXpFixtures.Summary(NativeAccountXpFixtures.SummaryKind.Pending),null);Check(b.Shown&&b.Mock&&b.HasPending,"a MOCK summary is tagged MOCK");
        }

        // ---- the DEV fixtures: MOCK values, each the state it names --------------------------------------------------------------------
        private static void Fixtures()
        {
            Check(NativeAccountXpFixtures.Names.Length==Enum.GetValues(typeof(NativeAccountXpFixtures.Kind)).Length&&NativeAccountXpFixtures.SummaryNames.Length==Enum.GetValues(typeof(NativeAccountXpFixtures.SummaryKind)).Length,"a name per fixture");
            Check(NativeAccountXpFixtures.Banner.Contains("MOCK VALUES")&&NativeAccountXpFixtures.Banner.Contains("DEV PREVIEW"),"the banner says DEV PREVIEW and MOCK VALUES");
            foreach(NativeAccountXpFixtures.Kind k in Enum.GetValues(typeof(NativeAccountXpFixtures.Kind)))
            {
                var a=NativeAccountXpFixtures.Make(k);Check(a!=null&&NativeAccountXpContract.Mock(a)&&a.completionId.StartsWith("mock-")&&a.account.StartsWith("mock-"),k+": a mock id and account");
                Check(a.lines.All(l=>l.label.StartsWith("MOCK · "))&&(a.reason==null||a.reason.StartsWith("MOCK · ")),k+": every label and reason says MOCK");
                var c=NativeAccountXpContract.Check(a,null);Check(c.Shown&&c.Usable,k+": shown and usable");
                switch(k)
                {
                    case NativeAccountXpFixtures.Kind.Zero:Check(c.Gain==0&&c.Levels==0&&a.previousTotal==0&&c.Status==AccountXpStatus.Confirmed,"zero XP");break;
                    case NativeAccountXpFixtures.Kind.Progress:Check(c.Gain>0&&c.Levels==0&&c.Bars&&c.Flaws==AccountXpFlaw.None,"ordinary progress");break;
                    case NativeAccountXpFixtures.Kind.LevelUp:Check(c.Levels==1&&c.Bars&&c.Flaws==AccountXpFlaw.None,"one level-up");break;
                    case NativeAccountXpFixtures.Kind.Multi:Check(c.Levels==4&&c.Bars&&c.Flaws==AccountXpFlaw.None,"multiple level-ups");break;
                    case NativeAccountXpFixtures.Kind.Pending:Check(c.Status==AccountXpStatus.Pending&&c.Levels==1&&c.Flaws==AccountXpFlaw.None,"pending (a level-up waiting)");break;
                    case NativeAccountXpFixtures.Kind.FailedAuto:Check(c.Status==AccountXpStatus.Failed&&c.Retry==AccountXpRetry.Auto&&a.retryIn>0&&a.attempt==2,"failed, automatic retry");break;
                    case NativeAccountXpFixtures.Kind.FailedManual:Check(c.Status==AccountXpStatus.Failed&&c.Retry==AccountXpRetry.Manual,"failed, try again");break;
                    case NativeAccountXpFixtures.Kind.Mismatch:Check((c.Flaws&AccountXpFlaw.LineSum)!=0&&c.Bars,"lines ≠ totals");break;
                    case NativeAccountXpFixtures.Kind.NoThresholds:Check((c.Flaws&AccountXpFlaw.NoBands)!=0&&!c.Bars&&c.Levels==1,"no thresholds");break;
                }
            }
            Check(NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.Progress,"pve").mode=="pve","the preview's mode follows its surface");
            var re=NativeAccountXpFixtures.Reissue(NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.Pending),"confirmed");Check(re.completionId=="mock-xp-pending"&&re.revision==2&&re.status=="confirmed","a re-issue keeps the completion id and raises the revision");
            foreach(NativeAccountXpFixtures.SummaryKind k in Enum.GetValues(typeof(NativeAccountXpFixtures.SummaryKind))){var s=NativeAccountXpFixtures.Summary(k);Check(k==NativeAccountXpFixtures.SummaryKind.Off?s==null:s!=null&&s.account.StartsWith("mock-"),k+": a mock summary");}
        }

        // ---- the confirmed animation of every fixture: inside the supplied thresholds -----------------------------------------------
        private static void Sequences()
        {
            foreach(var k in new[]{NativeAccountXpFixtures.Kind.Zero,NativeAccountXpFixtures.Kind.Progress,NativeAccountXpFixtures.Kind.LevelUp,NativeAccountXpFixtures.Kind.Multi,NativeAccountXpFixtures.Kind.Mismatch,NativeAccountXpFixtures.Kind.NoThresholds})
                Sequence(NativeAccountXpFixtures.Make(k),k.ToString());
            // Confirmed versions of the pending and failed fixtures, and wide bands of uneven size.
            Sequence(NativeAccountXpFixtures.Reissue(NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.Pending),"confirmed"),"pending confirmed");
            var uneven=new[]{new AccountXpBand{level=7,start=100,next=130},new AccountXpBand{level=8,start=130,next=100000},new AccountXpBand{level=9,start=100000,next=100007},new AccountXpBand{level=10,start=100007,next=0}};
            Sequence(Award(from:101,fromLevel:7,to:100200,toLevel:10,bands:uneven),"uneven bands into the top level");
            // A chain longer than MaxSegments collapses into one jump (one hold, LEVEL UP ×n), still inside the thresholds.
            var bands=Enumerable.Range(1,41).Select(l=>new AccountXpBand{level=l,start=(l-1)*100L,next=l*100L}).ToArray();
            var chain=Award(from:50,fromLevel:1,to:4050,toLevel:41,bands:bands);Check((NativeAccountXpContract.Check(chain,null).Flaws&AccountXpFlaw.ManyLevels)!=0,"40 levels: flagged as collapsed");
            Sequence(chain,"40-level chain");
            // The timeline: chained holds compressed toward MaxFill, never under MinHold.
            var m=new NativeAccountXpPanel();m.Build(NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.Multi),null,1,false,null,false);
            Check(m.Segments==5&&m.HoldLength>=NativeAccountXpPanel.MinHold-1e-6f&&m.FillEnd-m.FillStart<=NativeAccountXpPanel.MaxFill+1e-3f,"four level-ups: five segments within "+NativeAccountXpPanel.MaxFill+" s");
            for(int j=0;j<m.Segments;j++){m.Segment(j,out int level,out int ups,out float from,out float to,out _,out float d,out _);Check(level==2+j&&ups==j&&(j==0?Math.Abs(from-(1050-500)/700f)<1e-5f:from==0)&&(j==4?Math.Abs(to-(4560-4200)/1300f)<1e-5f:to==1)&&d>=0,"segment "+j+": level "+level+" from its supplied position to the threshold");}
            m=new NativeAccountXpPanel();m.Build(NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.LevelUp),null,1,false,null,false);Check(m.Segments==2&&Math.Abs(m.HoldLength-NativeAccountXpPanel.Hold)<1e-6f,"one level-up: two segments, a full hold");
            // Reduced effects: the final state at once (still recorded as celebrated).
            var seen=new NativeAccountXpSeen();var r=new NativeAccountXpPanel();var a=NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.Multi);r.Build(a,null,5,true,seen,false);
            Check(r.Final&&!r.Animating&&r.Level==6&&r.Ups==4&&r.SummaryIn==1&&r.LineIn.Take(r.Lines).All(x=>x==1)&&r.UpAge==NativeAccountXpPanel.Never&&seen.Contains(r.Key),"reduced: final at once, nothing pops, recorded");
        }
        private static void Sequence(AccountXpAward a,string name)
        {
            var c=NativeAccountXpContract.Check(a,null);var m=new NativeAccountXpPanel();var seen=new NativeAccountXpSeen();const float t0=50,dt=1/240f;
            m.Build(a,null,t0,false,seen,false);Check(m.Animating&&!m.Final&&seen.Contains(m.Key),name+": the first sight animates and is recorded at once");
            Check(m.Level==a.previousLevel&&(c.Bars?m.Fill==c.FromFill:m.Fill==-1)&&m.Ups==0&&m.SummaryIn==0,name+": starts at the previous level and saved progress");
            int lastLevel=m.Level,ups=0;double lastTotal=a.previousTotal;var visited=new List<int>{m.Level};float lastLock=0;
            float end=m.FillEnd+NativeAccountXpPanel.Settle+NativeAccountXpPanel.Burst+.05f;bool collapsed=c.Levels>=NativeAccountXpPanel.MaxSegments;
            for(float t=dt;t<=end;t+=dt)
            {
                m.Build(a,null,t0+t,false,seen,false);
                Check(m.Level>=lastLevel&&m.Level>=a.previousLevel&&m.Level<=a.newLevel,name+": the level only climbs, inside the supplied levels");
                bool hold=m.Level>a.previousLevel&&m.UpAge<m.HoldLength&&(m.Fill==1||m.Fill==-1)&&t<m.FillEnd;
                if(m.Level!=lastLevel)
                {
                    Check(collapsed?m.Level==a.newLevel:m.Level==lastLevel+1,name+": one level at a time"+(collapsed?" (collapsed: one jump)":""));
                    Check(hold&&m.UpAge<dt+1e-4f,name+": a level is crossed at its threshold, the hold starting there (t="+t+" level="+m.Level+" fill="+m.Fill+" upAge="+m.UpAge+" hold="+m.HoldLength+" end="+m.FillEnd+")");
                    Check(m.Ups==m.Level-a.previousLevel&&m.UpText==(collapsed?"LEVEL UP ×"+c.Levels:NativeAccountXpText.Ups(m.Ups)),name+": LEVEL UP counts the levels crossed ("+m.UpText+")");
                    visited.Add(m.Level);ups++;
                }
                if(c.Bars)
                {
                    // The bar's position as a total: inside the shown level's supplied band, and only ever rising from the old total to the new.
                    var band=a.bands[c.From+(m.Level-a.previousLevel)];bool max=band.next<=band.start;
                    double total=hold?band.start:max?band.start:band.start+(double)m.Fill*(band.next-band.start);
                    if(max&&!hold)total=Math.Max(band.start,lastTotal);
                    Check(m.Fill>=0&&m.Fill<=1,name+": the fill stays in the bar");
                    Check(total>=lastTotal-Math.Max(1e-3,(band.next-band.start)*2e-6)&&total>=a.previousTotal-1e-3&&total<=a.newTotal+Math.Max(1e-3,Math.Abs(band.next-band.start)*2e-6),name+": the bar's total only rises, from "+a.previousTotal+" to "+a.newTotal+" (t="+t+", "+total+")");
                    lastTotal=Math.Max(lastTotal,total);
                }
                else Check(m.Fill==-1,name+": no thresholds, no bar");
                for(int i=1;i<m.Lines;i++)Check(m.LineIn[i]<=m.LineIn[i-1]+1e-6f&&m.LineLock[i]<=m.LineLock[i-1]+1e-6f,name+": lines in order");
                if(m.Lines>0){Check(m.LineLock[0]>=lastLock-1e-6f,name+": lines lock in once");lastLock=m.LineLock[0];}
                if(t<m.FillStart-1e-3f)Check(m.Level==a.previousLevel&&(!c.Bars||m.Fill==c.FromFill),name+": nothing moves before the lines are in");
                lastLevel=m.Level;
            }
            Check(m.Final&&!m.Animating&&m.Level==a.newLevel&&m.Ups==c.Levels&&(c.Bars?m.Fill==c.ToFill:m.Fill==-1)&&m.SummaryIn==1&&m.ProgressIn==1,name+": ends at the new level and progress, settled");
            Check(m.LineIn.Take(m.Lines).All(x=>x==1)&&m.LineLock.Take(m.Lines).All(x=>x==1),name+": every line in and locked");
            Check(collapsed?visited.SequenceEqual(new[]{a.previousLevel,a.newLevel})&&ups==1:visited.SequenceEqual(Enumerable.Range(a.previousLevel,a.newLevel-a.previousLevel+1)),name+": every level from "+a.previousLevel+" to "+a.newLevel+" shown in turn");
            Check(m.StatusText==NativeAccountXpText.Saved&&(c.Levels>0?m.Summary==NativeAccountXpText.Climb(a.previousLevel,a.newLevel):m.Summary==null),name+": SAVED once confirmed; the climb summary");
        }

        // ---- pending: never shown as saved, never celebrated, never added --------------------------------------------------------------
        private static void Pending()
        {
            var a=NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.Pending);var c=NativeAccountXpContract.Check(a,null);var m=new NativeAccountXpPanel();var seen=new NativeAccountXpSeen();
            for(float t=0;t<30;t+=1/30f)
            {
                m.Build(a,null,100+t,t>20,seen,false);
                Check(m.Status==AccountXpStatus.Pending&&m.Level==a.previousLevel&&m.Fill==c.FromFill&&m.Ups==0&&m.UpText==null&&m.UpAge==NativeAccountXpPanel.Never&&m.SummaryIn==0,"pending: the saved level and fill only, no level-up");
                Check(m.Ghost==1&&m.GhostOver&&m.StatusText==NativeAccountXpText.Confirming&&m.DetailText==NativeAccountXpText.NotSavedYet,"pending: the gain as a ghost past the threshold, CONFIRMING REWARD…, not saved yet");
                Check(m.ProgressText==NativeAccountXpText.Progress(a.previousTotal,a.bands[0])&&m.LineLock.Take(m.Lines).All(x=>x==0),"pending: the saved progress, nothing locked in");
                foreach(var s in new[]{m.StatusText,m.DetailText,m.GainText,m.ProgressText,m.NextText,m.RetryText,m.Summary}.Concat(m.LineAmount.Take(m.Lines)))
                    if(s!=null)Check(!SavedWording.IsMatch(s.ToUpperInvariant()),"pending: no saved or earned wording ("+s+")");
            }
            Check(!seen.Contains(m.Key)&&seen.Count==0&&!seen.Dirty,"pending is never recorded as celebrated");
            var two=a;two=NativeAccountXpFixtures.Reissue(a,"pending");two.attempt=2;m.Build(two,null,200,false,seen,false);Check(m.AttemptText=="ATTEMPT 2"&&m.Status==AccountXpStatus.Pending,"a retried pending says its attempt");
            var g=Award("pending",2340,4,2760,4);m=new NativeAccountXpPanel();m.Build(g,Viewer,1,false,null,false);Check(m.Ghost==.76f&&!m.GhostOver&&m.Fill==.34f,"a pending gain inside the level: the ghost ends where it would land");
        }

        // ---- failed: the reason and the retry state from the authority; nothing saved --------------------------------------------------
        private static void Failed()
        {
            var a=NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.FailedAuto);var m=new NativeAccountXpPanel();var seen=new NativeAccountXpSeen();
            m.Build(a,null,10,false,seen,false);
            Check(m.Status==AccountXpStatus.Failed&&m.StatusText==NativeAccountXpText.FailedTitle&&m.DetailText==a.reason&&m.AttemptText=="ATTEMPT 2"&&!m.CanRetry,"failed: the title, the authority's reason verbatim, the attempt");
            Check(m.RetryText=="RETRYING IN 12s","auto: the countdown from the authority's retryIn");m.Build(a,null,13.2f,false,seen,false);Check(m.RetryText=="RETRYING IN 9s","the countdown runs on this issue's clock");
            m.Build(a,null,40,false,seen,false);Check(m.RetryText==NativeAccountXpText.RetryingNow,"past it: retrying automatically");
            Check(m.Level==a.previousLevel&&m.Fill==.34f&&m.Ghost==.76f&&m.Ups==0&&!seen.Contains(m.Key)&&seen.Count==0,"failed: nothing saved, nothing celebrated, the gain a ghost");
            var b=NativeAccountXpFixtures.Reissue(a,"failed","auto",30,"MOCK · again");m.Build(b,null,41,false,seen,false);Check(m.RetryText=="RETRYING IN 30s"&&m.DetailText=="MOCK · again"&&m.AttemptText=="ATTEMPT 3","a new issue restarts the countdown");
            var mm=new NativeAccountXpPanel();mm.Build(NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.FailedManual),null,1,false,seen,false);Check(mm.CanRetry&&mm.RetryText==NativeAccountXpText.TryAgain,"manual: Try again");
            foreach(var (retry,text) in new (string,string)[]{("later",NativeAccountXpText.RetryLater),("none",NativeAccountXpText.NoRetry),(null,(string)null),("soon",(string)null)})
            {var f=Award("failed");f.retry=retry;var p=new NativeAccountXpPanel();p.Build(f,Viewer,1,false,null,true);Check(p.RetryText==text&&!p.CanRetry&&p.DetailText==NativeAccountXpText.FailedDetail,"retry "+(retry??"absent")+": "+(text??"no promise"));
             if(retry=="soon")Check(p.FlawText.Contains("UNKNOWN RETRY"),"an unknown retry word is flagged in DEV");}
            var w=Award("failed");w.retry="auto";w.retryIn=float.NaN;var q=new NativeAccountXpPanel();q.Build(w,Viewer,1,false,null,false);Check(q.RetryText==NativeAccountXpText.RetryingNow,"a NaN retryIn: no countdown");
            w.retryIn=100000;q.Build(NativeAccountXpFixtures.Reissue(w,"failed","auto",100000),Viewer,2,false,null,false);Check(q.RetryText=="RETRYING IN 60m","a long wait in minutes, capped");
            Check(NativeAccountXpText.RetryIn(125)=="RETRYING IN 3m"&&NativeAccountXpText.RetryIn(.2f)=="RETRYING IN 1s"&&NativeAccountXpText.Reason(new string('r',300)).Length==NativeAccountXpContract.MaxReason,"countdown words; a long reason trimmed");
            var lr=Award("failed");lr.reason="  Account write refused (409).  ";var lp=new NativeAccountXpPanel();lp.Build(lr,Viewer,1,false,null,false);Check(lp.DetailText=="Account write refused (409).","the reason shown as given");
            foreach(var s in new[]{m.StatusText,m.DetailText,m.RetryText,mm.RetryText,NativeAccountXpText.FailedDetail,NativeAccountXpText.NoRetry,NativeAccountXpText.RetryLater})Check(!SavedWording.IsMatch(s.ToUpperInvariant()),"failed: never saved wording ("+s+")");
        }

        // ---- animate once ---------------------------------------------------------------------------------------------------------------
        private static void Once()
        {
            var a=NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.LevelUp);var seen=new NativeAccountXpSeen();var m=new NativeAccountXpPanel();
            m.Build(a,null,10,false,seen,false);Check(m.Animating&&seen.Contains(m.Key)&&m.Key=="mock-pilot\tmock-xp-level-up","first sight: the animation starts, the completion recorded");
            m.Build(a,null,11.5f,false,seen,false);float fill=m.Fill;int level=m.Level;
            // Reopening mid-way (the panel hidden then shown: Clear, then the same award): it carries on, never restarts.
            m.Clear();m.Build(a,null,11.5f,false,seen,false);Check(m.Animating&&m.Fill==fill&&m.Level==level,"reopened mid-way: the same frame, no restart");
            m.Clear();m.Build(a,null,60,false,seen,false);Check(m.Final&&!m.Animating&&m.Level==5,"reopened after: the final state");
            // The producer handing a fresh copy every frame: the same completion, no restart.
            var copy=NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.LevelUp);m.Build(copy,null,61,false,seen,false);Check(m.Final&&m.Level==5,"a fresh copy of the same award: unchanged");
            // Another screen (a new panel), another session (the record read back): the final state at once.
            var other=new NativeAccountXpPanel();other.Build(a,null,70,false,seen,false);Check(other.Final&&!other.Animating&&other.Level==5&&other.UpAge==NativeAccountXpPanel.Never,"a new panel with the record: no replay (mock ids are kept for the session)");
            var real=Award();var s1=new NativeAccountXpSeen();new NativeAccountXpPanel().Build(real,Viewer,1,false,s1,false);Check(s1.Dirty&&s1.Count==1,"a real completion is recorded for saving");
            var s2=NativeAccountXpSeen.Parse(s1.Serialize());var next=new NativeAccountXpPanel();next.Build(real,Viewer,500,false,s2,false);Check(next.Final&&!next.Animating&&!s2.Dirty,"the next session (the record read back): no replay, nothing new to save");
            // A re-issue of a confirmed completion (a correction, revision + 1) does not replay.
            var fixedUp=NativeAccountXpFixtures.Reissue(a,"confirmed");m.Build(fixedUp,null,80,false,seen,false);Check(m.Final&&!m.Animating,"a confirmed re-issue: no replay");
            // Pending → confirmed: celebrated at the confirmation, once; the lines already in stay in.
            var p=NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.Pending);var pm=new NativeAccountXpPanel();var ps=new NativeAccountXpSeen();
            pm.Build(p,null,10,false,ps,false);pm.Build(p,null,14,false,ps,false);Check(pm.Status==AccountXpStatus.Pending&&!ps.Contains(pm.Key)&&pm.LineIn[1]==1,"pending for four seconds: lines in, not recorded");
            var conf=NativeAccountXpFixtures.Reissue(p,"confirmed");pm.Build(conf,null,14.5f,false,ps,false);Check(pm.Animating&&ps.Contains(pm.Key)&&pm.Level==4&&pm.LineIn[0]==1&&pm.LineIn[1]==1&&pm.LineLock[0]==0,"confirmed: the animation starts now, lines stay in and lock in");
            pm.Build(conf,null,30,false,ps,false);Check(pm.Final&&pm.Level==5,"and ends at the new level");pm.Clear();pm.Build(conf,null,31,false,ps,false);Check(pm.Final&&!pm.Animating&&pm.UpAge>=NativeAccountXpPanel.Burst,"reopened: final, not replayed");
            // Failed → pending (retry) → confirmed: once, at the confirmation.
            var f=NativeAccountXpFixtures.Make(NativeAccountXpFixtures.Kind.FailedAuto);var fm=new NativeAccountXpPanel();var fs=new NativeAccountXpSeen();fm.Build(f,null,1,false,fs,false);
            var retry=NativeAccountXpFixtures.Reissue(f,"pending");fm.Build(retry,null,5,false,fs,false);Check(fm.Status==AccountXpStatus.Pending&&!fs.Contains(fm.Key),"failed then retrying: still not recorded");
            fm.Build(NativeAccountXpFixtures.Reissue(retry,"confirmed"),null,6,false,fs,false);Check(fm.Animating&&fs.Contains(fm.Key),"confirmed after a retry: celebrated once");
            // Two completions: each its own celebration; the same id on another account is another completion.
            var b=Award(id:"c-2");var mb=new NativeAccountXpPanel();var sb=new NativeAccountXpSeen();mb.Build(real,Viewer,1,false,sb,false);mb.Build(real,Viewer,30,false,sb,false);mb.Build(b,Viewer,31,false,sb,false);Check(mb.Animating&&sb.Count==2,"a second completion animates too");
            Check(NativeAccountXpSeen.KeyOf("a","x")!=NativeAccountXpSeen.KeyOf("b","x"),"the record keys by account and completion");
            // DEV preview replay: mock ids only.
            m.Replay(seen);m.Build(a,null,90,false,seen,false);Check(m.Animating,"DEV replay restarts a mock completion");
            next.Replay(s2);next.Build(real,Viewer,600,false,s2,false);Check(next.Final,"replay never touches a real completion");
        }

        // ---- the local record: bounded, tolerant, mock-free ---------------------------------------------------------------------------
        private static void Record()
        {
            var s=new NativeAccountXpSeen();for(int i=0;i<250;i++)Check(s.Mark(NativeAccountXpSeen.KeyOf(Viewer,"c"+i),false),"new keys are added");
            Check(s.Count==NativeAccountXpSeen.Capacity&&!s.Contains(NativeAccountXpSeen.KeyOf(Viewer,"c0"))&&!s.Contains(NativeAccountXpSeen.KeyOf(Viewer,"c49"))&&s.Contains(NativeAccountXpSeen.KeyOf(Viewer,"c50"))&&s.Contains(NativeAccountXpSeen.KeyOf(Viewer,"c249")),"bounded: the newest 200 kept, the oldest dropped");
            Check(!s.Mark(NativeAccountXpSeen.KeyOf(Viewer,"c100"),false),"a key already there is not added twice");
            var text=s.Serialize();var lines=text.Split('\n');Check(lines[0]==NativeAccountXpSeen.Header&&lines[1]==NativeAccountXpSeen.KeyOf(Viewer,"c50")&&lines[200]==NativeAccountXpSeen.KeyOf(Viewer,"c249"),"serialized oldest first under its header");
            var back=NativeAccountXpSeen.Parse(text);Check(back.Count==200&&back.Contains(NativeAccountXpSeen.KeyOf(Viewer,"c50"))&&!back.Dirty,"read back whole");
            back.Mark(NativeAccountXpSeen.KeyOf(Viewer,"new"),false);Check(!back.Contains(NativeAccountXpSeen.KeyOf(Viewer,"c50"))&&back.Contains(NativeAccountXpSeen.KeyOf(Viewer,"c51")),"still bounded after reading back");
            Check(NativeAccountXpSeen.Parse("something else\nnova\tc1\n").Count==0&&NativeAccountXpSeen.Parse(null).Count==0&&NativeAccountXpSeen.Parse("").Count==0,"a wrong header or nothing reads as empty");
            var mixed=NativeAccountXpSeen.Parse(NativeAccountXpSeen.Header+"\r\nnova\tc1\r\n\nno-tab\n"+new string('x',400)+"\tc\nnova\tc\u0001x\nmock-pilot\tmock-xp-zero\nnova\tc2\n");
            Check(mixed.Count==2&&mixed.Contains("nova\tc1")&&mixed.Contains("nova\tc2"),"unreadable lines and mock ids skipped, CRLF tolerated");
            var m=new NativeAccountXpSeen();Check(m.Mark("mock-pilot\tmock-xp-zero",true)&&m.Contains("mock-pilot\tmock-xp-zero")&&!m.Dirty&&m.Count==0&&!m.Serialize().Contains("mock"),"mock completions: remembered for the session, never saved");
            m.ForgetMock("mock-pilot\tmock-xp-zero");Check(!m.Contains("mock-pilot\tmock-xp-zero"),"and forgotten for a DEV replay");
            Check(!m.Mark(null,false)&&!m.Mark("",false)&&!m.Mark(new string('k',NativeAccountXpSeen.MaxKey+1),false),"empty or oversized keys refused");
            var w=new NativeAccountXpSeen();w.Mark("a\tb",false);Check(w.Dirty,"a new key marks the record for saving");w.Saved();Check(!w.Dirty,"saved");
        }

        // ---- apart from chips and currency ------------------------------------------------------------------------------------------
        private static double Hue(int rgb){double r=((rgb>>16)&255)/255.0,g=((rgb>>8)&255)/255.0,b=(rgb&255)/255.0,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min;if(d<=0)return 0;double h=max==r?(g-b)/d%6:max==g?(b-r)/d+2:(r-g)/d+4;h*=60;return h<0?h+360:h;}
        private static double HueGap(int a,int b){double d=Math.Abs(Hue(a)-Hue(b));return Math.Min(d,360-d);}
        private static IEnumerable<string> Literals(string code)=>Regex.Matches(code,"\"((?:[^\"\\\\\\n]|\\\\.)*)\"").Select(x=>x.Groups[1].Value);
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Where(l=>!l.TrimStart().StartsWith("//")&&!l.TrimStart().StartsWith("///")));
        private static void Separation(Func<string,string> R)
        {
            const int Warn=0xffc24d,Cyan=0x22e6ff,Navy=0x0a1024;
            Check(HueGap(NativeAccountXpTone.Bar,Warn)>=90&&HueGap(NativeAccountXpTone.Soft,Warn)>=90,"XP's violet is far from the wallet's gold ("+HueGap(NativeAccountXpTone.Bar,Warn).ToString("0")+"°)");
            Check(HueGap(NativeAccountXpTone.Bar,Cyan)>=45&&HueGap(NativeAccountXpTone.Soft,Cyan)>=45,"and from the chips' and HUD's cyan ("+HueGap(NativeAccountXpTone.Bar,Cyan).ToString("0")+"°)");
            Check(NativeHudCyber.PanelContrast(NativeAccountXpTone.Soft,Navy,NativeHudCyber.PanelAlpha)>=NativeHudCyber.TextContrast,"XP text reads 4.5:1 on the navy glass");
            var hud=R("Assets/ChipArena/Runtime/NativeAccountXpHud.cs");
            foreach(var t in new[]{"Bar","Glow","Deep","Hatch","Tone"})foreach(var st in Code(hud).Split(';'))if(st.Contains("U.Text("))Check(!Regex.IsMatch(st,@"\b"+t+@"\([^)]*\)\s*,\s*TextAnchor"),"XP decoration tones never colour text ("+t+")");
            // Words: XP and LEVEL; never money, credits or chips; the wallet's gold never used for XP.
            foreach(var f in new[]{"Core/NativeAccountXp.cs","Core/NativeAccountXpPanel.cs","Core/NativeAccountXpFixtures.cs","Runtime/NativeAccountXpHud.cs","Runtime/NativeAccountXpUI.cs"})
            {
                var code=Code(R("Assets/ChipArena/"+f));
                foreach(var s in Literals(code))Check(!Regex.IsMatch(s,@"\$|\bchips?\b|\bwallet\b|\bcredits?\b|\bmoney\b|\bcoins?\b|\bcurrency\b",RegexOptions.IgnoreCase)||s.StartsWith("<path"),f+": no currency or chip wording (\""+s+"\")");
                Check(!code.Contains("C.Warn(")&&!code.Contains("NativeCyberTheme.Warn(")&&!code.Contains("Money("),f+": never the wallet's gold or money format");
            }
            Check(NativeAccountXpText.Xp(1240)=="+1,240 XP"&&NativeAccountXpText.Level(12)=="LEVEL 12"&&NativeAccountXpText.Eyebrow=="ACCOUNT XP","XP wording");
            // Structure: its own panel beside the results, drawn after them; nothing of XP inside the results' own rows.
            var overlays=R("Assets/ChipArena/Runtime/NativeMatchOverlays.cs");
            Check(!Regex.IsMatch(Body(overlays,"private void PveResultCard(")??"","AccountXp")&&!Regex.IsMatch(Body(overlays,"private static (string label,string value)[] StatRows(")??"","AccountXp|XP"),"the PvE cards' stats (and their money) have no XP in them");
        }

        // ---- the bindings: unbound today, with the tripwire --------------------------------------------------------------------------
        private const string AwardUnbound="internal AccountXpAward AccountXpAwardOf(AccountXpSurface surface)=>null;",SummaryUnbound="internal AccountXpSummary AccountXpSummaryOf()=>null;",RetryUnbound="internal Action<string> AccountXpRetryOf()=>null;";
        private static readonly Regex Producer=new Regex(@"new\s+AccountXpAward\b|new\s+AccountXpSummary\b|\bcompletionId\b|\baccountXp[A-Z]?\w*\s*[=;:,)]|\bxpTotal\b|\bxpAward\b|\bAccountXpRecord\b|""xp-award""|""account-xp""|""accountXp""");
        private static readonly string[] Presentation={"NativeAccountXp.cs","NativeAccountXpPanel.cs","NativeAccountXpFixtures.cs","NativeAccountXpHud.cs","NativeAccountXpUI.cs"};
        /// <summary>The tripwire: account XP data in a non-presentation file while a binding is still unbound. Returns the reason or null.</summary>
        internal static string Tripwire(IEnumerable<(string file,string text)> files,string ui)
        {
            ui=Code(ui);bool unbound=ui.Contains(AwardUnbound)||ui.Contains(SummaryUnbound);
            foreach(var (file,text) in files)
            {
                if(Presentation.Any(p=>file.EndsWith(p,StringComparison.Ordinal)))continue;var code=Code(text);
                var hit=Producer.Match(code);if(!hit.Success)hit=Regex.Match(Body(code,"class NativeAccountData")??"",@"\b(xp|level|accountLevel|totalXp)\b");
                if(hit.Success&&unbound)return file+" carries account XP data (\""+hit.Value.Trim()+"\") while the presentation's binding is unbound: bind AccountXpAwardOf / AccountXpSummaryOf in Runtime/NativeAccountXpUI.cs";
            }
            return null;
        }
        private static void Adapters(Func<string,string> R,string root)
        {
            var ui=R("Assets/ChipArena/Runtime/NativeAccountXpUI.cs");var live=Code(ui);
            bool award=live.Contains(AwardUnbound),summary=live.Contains(SummaryUnbound),retry=live.Contains(RetryUnbound);
            Check(Regex.Matches(live,@"internal AccountXpAward AccountXpAwardOf\(AccountXpSurface surface\)=>[^;\n]+;").Count==1&&Regex.Matches(live,@"internal AccountXpSummary AccountXpSummaryOf\(\)=>[^;\n]+;").Count==1&&Regex.Matches(live,@"internal Action<string> AccountXpRetryOf\(\)=>[^;\n]+;").Count==1,"each binding is one line");
            if(award&&summary)Check(retry,"today: all three bindings return null (no award, no level, no retry anywhere)");
            else Check(!award&&!summary,"once bound, both the award and the summary are bound");
            var files=new List<(string,string)>();
            foreach(var dir in new[]{"Assets/ChipArena","Relay"}){var d=Path.Combine(root,dir);if(Directory.Exists(d))foreach(var f in Directory.GetFiles(d,"*.cs",SearchOption.AllDirectories))if(!f.Contains("/bin/")&&!f.Contains("/obj/"))files.Add((f.Replace('\\','/'),File.ReadAllText(f)));}
            var trip=Tripwire(files,ui);Check(trip==null,trip??"");
            // The tripwire fires on the producer shapes it guards (self-test on synthetic files).
            foreach(var p in new[]{"var a=new AccountXpAward{completionId=id};","public string completionId;","accountXpFeed=new Feed();","[Serializable] public sealed class NativeAccountData {public string id;public long xp;public int level;}","Send(\"account-xp\",award);"})
                Check(Tripwire(new[]{("Runtime/NativeNetwork.cs",p)},ui)!=null==(award||summary),"the tripwire fires on a producer: "+p);
            Check(Tripwire(new[]{("Runtime/NativeMatchOverlays.cs","AccountXpDocked(AccountXpSurface.Pvp,px,pw,py,480,enter);"),("Runtime/NativeAccountXpUI.cs","completionId")},AwardUnbound+SummaryUnbound)==null,"the presentation's own hooks do not trip it");
            Check(Tripwire(new[]{("Runtime/NativeNetwork.cs","public string completionId;")},"internal AccountXpAward AccountXpAwardOf(AccountXpSurface surface)=>feed.Award(surface);internal AccountXpSummary AccountXpSummaryOf()=>account?.Account?.xp;")==null,"bound: quiet");
            Check(!typeof(ExpeditionView).GetFields().Concat(typeof(ExpeditionViewPersonal).GetFields()).Concat(typeof(ExpeditionViewMember).GetFields()).Any(f=>Regex.IsMatch(f.Name,"xp|Xp|completion|accountLevel"))||!(award||summary),"no Expedition view field carries account XP while unbound");
        }

        // ---- per frame, nothing allocates ---------------------------------------------------------------------------------------------
        private static void Allocation()
        {
            var kinds=(NativeAccountXpFixtures.Kind[])Enum.GetValues(typeof(NativeAccountXpFixtures.Kind));
            var panels=kinds.Select(_=>new NativeAccountXpPanel()).ToArray();var a=kinds.Select(k=>NativeAccountXpFixtures.Make(k)).ToArray();var b=kinds.Select(k=>NativeAccountXpFixtures.Make(k)).ToArray();
            var seen=new NativeAccountXpSeen();var badge=new NativeAccountXpBadge();var sums=Enum.GetValues(typeof(NativeAccountXpFixtures.SummaryKind)).Cast<NativeAccountXpFixtures.SummaryKind>().Select(NativeAccountXpFixtures.Summary).ToArray();
            float sink=0;var dims=new[]{NativeUiMotion.Measure(1280,720,1.5f),NativeUiMotion.Measure(3440,1440,1),NativeUiMotion.Measure(1280,720,2)};
            for(int warm=0;warm<2;warm++)
            {
                for(int i=0;i<panels.Length;i++)panels[i].Replay(seen);
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int f=0;f<720;f++)
                {
                    float now=100+warm*1000+f/60f;
                    for(int i=0;i<panels.Length;i++)
                    {
                        var m=panels[i];m.Build(f%2==0?a[i]:b[i],null,now,f%97==0,seen,f%3==0);
                        sink+=m.Fill+m.Ghost+m.UpAge+m.SummaryIn+m.ProgressIn+m.Level+m.Ups+(m.LevelText?.Length??0)+(m.BadgeText?.Length??0)+(m.UpText?.Length??0)+(m.RetryText?.Length??0)+(m.ProgressText?.Length??0)+(m.StatusText?.Length??0);
                        for(int j=0;j<m.Lines;j++)sink+=m.LineIn[j]+m.LineLock[j]+m.LineAmount[j].Length+m.LineLabel[j].Length;
                        var d=dims[f%dims.Length];float hw=Math.Min(640,NativeAccountXpLayout.HostRoom(d.Width,480,true)-32),hx=NativeAccountXpLayout.HostLeft(d.Width,hw,480,true);
                        var l=NativeAccountXpLayout.Dock(d.Width,d.Height,hx,hw,40,480,m.Rows,m.Status,m.Dev);var c=NativeAccountXpLayout.CardOn(d.Width,d.Height,m.Rows,m.Status,false);
                        sink+=l.Panel.H+l.LineBox(0).Y+c.Panel.Y+l.StatusY;
                    }
                    badge.Build(sums[f%sums.Length],null);sink+=badge.Fill+badge.Level+(badge.Shown?badge.LevelText.Length+badge.Number.Length:0);
                    sink+=NativeAccountXpText.RetryIn(f%130).Length+(NativeAccountXpText.Attempt(f%25)?.Length??0)+NativeAccountXpText.Ups(f%40).Length+(NativeAccountXpText.More(f%40)?.Length??0);
                }
                long used=GC.GetAllocatedBytesForCurrentThread()-before;if(warm==1)Check(used==0,"the panel, the menu level, the layouts and their words allocate nothing per frame ("+used+" bytes)");
            }
            Check(!float.IsNaN(sink),"sink");
        }

        // ---- layouts ----------------------------------------------------------------------------------------------------------------
        private static void Layouts()
        {
            var required=new (int w,int h,float s)[]{(1280,720,1.5f),(1920,1080,1),(2560,1080,1),(3440,1440,1),(3840,2160,1)};
            var others=new (int w,int h,float s)[]{(1280,720,1),(1600,1200,1),(1024,768,1),(2560,1440,1),(1920,1080,1.5f),(1280,720,2),(1920,1080,2.4f),(800,600,1)};
            var statuses=new[]{AccountXpStatus.Confirmed,AccountXpStatus.Pending,AccountXpStatus.Failed,AccountXpStatus.None};
            foreach(var c in required.Concat(others))
            {
                var ui=NativeUiMotion.Measure(c.w,c.h,c.s);float W=ui.Width,H=ui.Height;bool must=required.Contains(c);string at0=c.w+"x"+c.h+" @"+c.s;
                // Without an award the results keep their exact place.
                Check(NativeAccountXpLayout.HostRoom(W,480,false)==W&&NativeAccountXpLayout.HostLeft(W,Math.Min(640,W-32),480,false)==(W-Math.Min(640,W-32))/2,at0+": no award, the results unchanged");
                foreach(var (pref,min,name) in new[]{(640f,480f,"PvP"),(1180f,420f,"PvE")})
                {
                    float hw=Math.Min(pref,NativeAccountXpLayout.HostRoom(W,min,true)-32),hx=NativeAccountXpLayout.HostLeft(W,hw,min,true);
                    if(must)Check(NativeAccountXpLayout.Fits(W,min)&&hw>=min&&hw<=pref,at0+" "+name+": the results keep at least their minimum ("+hw+")");
                    foreach(float hostH in new[]{458f,594f,866f})foreach(var st in statuses)for(int rows=0;rows<=8;rows++)foreach(bool dev in new[]{false,true})
                    {
                        float hy=Math.Max(16,(H-Math.Min(hostH,H-32))/2);var l=NativeAccountXpLayout.Dock(W,H,hx,hw,hy,min,rows,st,dev);string at=at0+" "+name+" "+st+" rows="+rows+(dev?" dev":"");
                        Check(l.Panel.Inside(W,H)&&l.Panel.X>=NativeAccountXpLayout.Margin-.01f&&l.Panel.Right<=W-NativeAccountXpLayout.Margin+.01f&&l.Panel.Y>=NativeAccountXpLayout.Margin-.01f&&l.Panel.Bottom<=H-NativeAccountXpLayout.Margin+.01f,at+": inside the canvas and its margins");
                        Rows(l,rows,st,dev,at);
                        if(must)
                        {
                            var host=new AccountXpBox(hx,hy,hw,Math.Min(hostH,H-32));
                            Check(l.Docked&&!l.Mini&&!l.Panel.Overlaps(host)&&l.Panel.X>=host.Right+NativeAccountXpLayout.Gap-.01f,at+": docked beside the results, apart");
                            Check(l.Panel.W>=NativeAccountXpLayout.MinWidth&&l.Panel.W<=NativeAccountXpLayout.MaxWidth&&Math.Abs((hx-0)-(W-l.Panel.Right))<1,at+": its own column, the pair centred");
                            Check(l.ShownLines==rows&&!l.Compact,at+": every line, normal rows");
                            Check(Math.Abs(l.Panel.Y-hy)<.01f||l.Panel.Bottom>=H-NativeAccountXpLayout.Margin-.01f,at+": level with the results' top");
                        }
                        else if(!l.Mini)Check(l.ShownLines>=1||rows==0,at+": at least one line");
                    }
                }
                foreach(var st in statuses)for(int rows=0;rows<=8;rows++)
                {
                    var l=NativeAccountXpLayout.CardOn(W,H,rows,st,true);string at=at0+" card "+st+" rows="+rows;
                    Check(l.Card&&l.Panel.Inside(W,H)&&Math.Abs(l.Panel.X+l.Panel.W/2-W/2)<.5f&&l.Panel.W<=NativeAccountXpLayout.CardWidth,at+": centred inside the canvas");
                    Check(l.FootY+(l.Compact?34:40)<=l.Panel.Bottom+.01f,at+": the Close row inside");Rows(l,rows,st,true,at);
                    if(must)Check(l.ShownLines==rows,at+": every line");
                }
            }
            var small=NativeUiMotion.Measure(1280,720,2);var mini=NativeAccountXpLayout.Dock(small.Width,small.Height,0,640,16,480,4,AccountXpStatus.Confirmed,false);
            Check(mini.Mini&&mini.Panel.Inside(small.Width,small.Height)&&mini.ShownLines==0&&mini.Panel.Right<=small.Width-NativeAccountXpLayout.Margin+.01f,"720p @2: too narrow for both, the mini card at the top right without the breakdown");
            var tall=NativeAccountXpLayout.Dock(1600,360,300,640,16,480,8,AccountXpStatus.Failed,true);Check(tall.Compact&&tall.ShownLines<8&&tall.ShownLines>=1&&tall.Panel.Bottom<=360-16+.01f,"a short canvas: compact rows, then fewer lines (+n MORE)");
        }
        private static void Rows(in NativeAccountXpLayout l,int rows,AccountXpStatus st,bool dev,string at)
        {
            float[] ys={l.HeadY,l.TitleY,l.BarY,l.ProgressY,l.RuleA,l.LinesY,l.RuleB,l.StatusY,l.DetailY,l.RetryY,l.DevY,l.FootY};
            for(int i=1;i<ys.Length;i++)Check(ys[i]>=ys[i-1]-.01f,at+": rows in order ("+i+")");
            Check(l.HeadY>=l.Panel.Y&&l.FootY<=l.Panel.Bottom+.01f&&l.InnerX>l.Panel.X&&l.InnerX+l.InnerW<l.Panel.Right,at+": rows inside the panel");
            if(!l.Mini)for(int i=0;i<Math.Max(1,l.ShownLines);i++){var b=l.LineBox(i);Check(b.Y>=l.RuleA-.01f&&b.Bottom<=l.RuleB+.01f,at+": line "+i+" between the rules");}
            Check(l.BadgeSize<=l.TitleH,at+": the hexagon fits its row");
        }

        // ---- wiring: presentation only ---------------------------------------------------------------------------------------------
        private static string Body(string text,string signature)
        {
            int i=text.IndexOf(signature,StringComparison.Ordinal);if(i<0)return null;int b=text.IndexOf('{',i);if(b<0)return null;int depth=0;
            for(int j=b;j<text.Length;j++){if(text[j]=='{')depth++;else if(text[j]=='}'&&--depth==0)return text.Substring(i,j-i+1);}return null;
        }
        private static void Wiring(Func<string,string> R,string root)
        {
            var overlays=R("Assets/ChipArena/Runtime/NativeMatchOverlays.cs");var end=Body(overlays,"private void MatchEndPanel()");var pve=Body(overlays,"private void PveResultsPanel()");
            Check(end.Contains("pw=Mathf.Min(640,AccountXpHostRoom(AccountXpSurface.Pvp,W,480)-32)")&&end.Contains("px=AccountXpHostX(AccountXpSurface.Pvp,W,pw,480)")&&end.Contains("U.Pop();\n            // Permanent account XP")&&end.TrimEnd().EndsWith("AccountXpDocked(AccountXpSurface.Pvp,px,pw,py,480,enter);\n        }"),"PvP match end: the results make room, the XP panel docks beside them after they are drawn");
            Check(pve.Contains("pw=Mathf.Min(1180,AccountXpHostRoom(AccountXpSurface.Pve,W,420)-32)")&&pve.Contains("px=AccountXpHostX(AccountXpSurface.Pve,W,pw,420)")&&pve.TrimEnd().EndsWith("AccountXpDocked(AccountXpSurface.Pve,px,pw,py,420,enter);\n        }"),"PvE results: likewise");
            var home=R("Assets/ChipArena/Runtime/NativeHomeMenu.cs");Check(Body(home,"private void Nameplate(float alpha)").Contains("AccountXpNameplate(nx,ny+26,alpha);"),"the home nameplate's third line");
            var live=R("Assets/ChipArena/Runtime/NativeExpeditionLiveUI.cs");var gui=Body(live,"private bool ExpeditionLiveGui(");
            Check(Body(live,"private void ExpeditionLiveSync()").Contains("AccountXpExpeditionSync();")&&gui.Contains("bool modal=expeditionLiveDice.Open||AccountXpCardOpen;")&&gui.Contains("AccountXpCardButton(new Rect(foot.xMax-266,foot.y+1,126,30),modal);")&&gui.TrimEnd().EndsWith("AccountXpExpeditionCard(width,height);\n            return true;\n        }"),"the Expedition screen: opens on a new or newly confirmed award, the card modal and last, ACCOUNT XP reopens it");
            var dev=R("Assets/ChipArena/Runtime/NativeDevPanel.cs");
            Check(NativeDevActions.Find("preview-account-xp")?.Kind==NativeDevKind.Presentation&&NativeDevActions.Find("preview-account-xp").Tab==NativeDevTab.Previews&&dev.Contains("case \"preview-account-xp\":AccountXpPreviewToggle();break;")&&dev.Contains("AccountXpPreviewHud(W,Hh);")&&Body(dev,"private float DevPreviewsTab(").Contains("y=AccountXpPreviewPanel(x,y,w);"),"DEV panel · Previews: the MOCK preview (a view tool)");
            var ui=R("Assets/ChipArena/Runtime/NativeAccountXpUI.cs");
            Check(ui.Contains("private bool AccountXpPreviewShown=>accountXpPreview&&AccountXpDev&&accountXpMock!=null;")&&ui.Contains("private bool AccountXpPreviewMenu=>AccountXpDev&&"),"the MOCK preview is for developers only");
            Check(Body(ui,"private NativeAccountXpPanel AccountXpFor(").Contains("var a=mock?accountXpMock:AccountXpAwardOf(surface);")&&Body(ui,"private void AccountXpNameplate(").Contains("accountXpBadge.Build(mock?accountXpMockSummary:AccountXpSummaryOf(),mock?null:AccountXpViewer);"),"the surfaces read the bindings (or the developer's MOCK)");
            // Presentation only: the XP files send, decide and write nothing but the local record.
            foreach(var f in Presentation)
            {
                var path=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),f,SearchOption.AllDirectories).Single();var code=Code(File.ReadAllText(path));
                foreach(var api in new[]{"Send(","SendServer(","SendEncoded(","SendDevCommand(","BeginRequest(","UnityWebRequest","NativeNetMessage","NativeNetCodec","account.Save(","Accepted","grantVersion","unlocks","Execute(","Broadcast("})
                    Check(!code.Contains(api),f+" calls no network, account or authority API ("+api+")");
                Check(!Regex.IsMatch(code,@"\b(a|award|s|summary|source)\.(previousTotal|newTotal|previousLevel|newLevel|status|lines|bands|revision|total|level|known|pending)\s*=[^=]")||f=="NativeAccountXpFixtures.cs","writes nothing into an award or summary ("+f+")");
                Check(!Regex.IsMatch(code,@"File\.(Write|Append|Delete|Move|Copy)|NativeFileStore\.Write\(")||f=="NativeAccountXpUI.cs",f+" writes no file");
            }
            Check(Regex.Matches(ui,@"NativeFileStore\.Write\(|File\.(Write|Append|Delete|Move|Copy)").Count==1&&ui.Contains("NativeFileStore.Write(AccountXpSeenPath,s.Serialize());")&&ui.Contains("NativeUserDataPath,NativeAccountXpSeen.File"),"the only write: the local record of celebrated completions, in the user data folder");
            Check(!Regex.IsMatch(Code(R("Assets/ChipArena/Core/NativeAccountXp.cs"))+Code(R("Assets/ChipArena/Core/NativeAccountXpPanel.cs")),"\\bthrow\\b"),"the model never throws on an award");
            // The authority, network and account files do not know the XP presentation exists.
            foreach(var f in new[]{"Runtime/NativeNetwork.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeExpeditionNetwork.cs","Runtime/NativeStageGateNetwork.cs","Runtime/NativeAccount.cs","Runtime/NativeAdmin.cs","Runtime/NativeProfileUI.cs","Runtime/NativeExpeditionPersistence.cs","Runtime/NativeExpeditionM1.cs","Runtime/NativePve.cs","Runtime/NativeEndFlowLogic.cs",
                "Core/NativeMatch.cs","Core/NativeExpedition.cs","Core/NativeExpeditionRewards.cs","Core/NativeExpeditionSave.cs","Core/NativeExpeditionView.cs","Core/NativeExpeditionWire.cs","Core/WireCodec.cs","Core/NativeRelay.cs","Core/NativeAccess.cs","Core/NativeEndFlow.cs","Core/NativePveRules.cs"})
                Check(!R("Assets/ChipArena/"+f).Contains("AccountXp"),f+" does not touch the XP presentation");
            var users=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories).Where(f=>File.ReadAllText(f).Contains("AccountXp")).Select(Path.GetFileName).OrderBy(x=>x).ToArray();
            Check(users.SequenceEqual(new[]{"NativeAccountXp.cs","NativeAccountXpFixtures.cs","NativeAccountXpHud.cs","NativeAccountXpPanel.cs","NativeAccountXpUI.cs","NativeDevPanel.cs","NativeExpeditionLiveUI.cs","NativeHomeMenu.cs","NativeMatchOverlays.cs"}.OrderBy(x=>x)),"only the results, the home menu, the Expedition screen and the DEV panel reach the XP presentation ("+string.Join(", ",users)+")");
            if(Directory.Exists(Path.Combine(root,"Relay")))Check(!Directory.GetFiles(Path.Combine(root,"Relay"),"*.cs",SearchOption.AllDirectories).Any(f=>File.ReadAllText(f).Contains("AccountXp")),"the relay does not touch it");
            // Draw paths: no strings built per frame; every clock-driven motion respects reduced effects.
            var hud=Code(R("Assets/ChipArena/Runtime/NativeAccountXpHud.cs"));
            Check(!Regex.IsMatch(hud,"\\+\\s*\"|\"\\s*\\+|\\$\"|ToString\\(|string\\.Format|new List|Substring\\(|new\\[\\]|\\.Select\\(|\\.Where\\("),"the HUD builds no strings, arrays or queries per frame");
            foreach(var line in hud.Split('\n').Where(l=>Regex.IsMatch(l,@"\bnow\s*\*")))Check(line.Contains("reduced?"),"a time-driven motion respects reduced effects: "+line.Trim());
            foreach(var mark in new[]{"!reduced&&m.UpAge<M.Pop","!reduced&&m.UpAge<M.Burst","reduced||m.UpAge>=M.Pop"})Check(hud.Contains(mark),"the pop, burst and glow are off with reduced effects ("+mark+")");
            var uiCode=Code(ui);foreach(var draw in new[]{"private void AccountXpDocked(","private bool AccountXpExpeditionCard(","private void AccountXpNameplate("})Check(!Regex.IsMatch(Body(uiCode,draw)??"x","\\+\\s*\"|\"\\s*\\+|\\$\"|ToString\\("),draw+" builds no strings");
            // Metas.
            var guids=new HashSet<string>();
            foreach(var m in Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories)){var g=Regex.Match(File.ReadAllText(m),"guid: ([0-9a-f]{32})");if(g.Success)Check(guids.Add(g.Groups[1].Value),"unique guid "+m);}
            foreach(var f in new[]{"Core/NativeAccountXp.cs","Core/NativeAccountXpPanel.cs","Core/NativeAccountXpFixtures.cs","Runtime/NativeAccountXpHud.cs","Runtime/NativeAccountXpUI.cs"})Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its meta");
            Check(File.Exists(Path.Combine(root,"../../docs/ACCOUNT-XP-PRESENTATION.md"))&&File.ReadAllText(Path.Combine(root,"../../docs/ACCOUNT-XP-PRESENTATION.md")).Contains("AccountXpAwardOf"),"the contract doc names the bindings");
        }
    }
}
#endif
