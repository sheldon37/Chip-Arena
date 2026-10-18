using System;

namespace ChipArena.UnityConversion
{
    // The party panel on the stage loading screen (Claude, 2026-10-18; owner: "lets add a loading indicator for players (When
    // starting expeditions, so we can see where each player is on loading screen)"). Engine-free: who is in the party, where each
    // member is in this load and what their bar shows, read from the public ExpeditionView only (members[]: account, name,
    // weapon, seat, host, connected, extracted, area, pendingArea; areaBarrier: active, target, ready, required) plus this
    // client's own load (the loading screen's shown progress and its readiness). Host and guests read the same fields, so they
    // draw the same thing; nothing here decides a rule, writes the view or sends anything.
    //
    // What the view says today, per member:
    //  - DROPPED: members[].connected is false (a lost link, or a load window that ran out and dropped the peer).
    //  - EXTRACTED: members[].extracted.
    //  - An area transfer (my own members[].pendingArea is set): my cohort is every member whose pendingArea is my destination.
    //    The view names no member's readiness, only the cohort's count (areaBarrier.ready of required), so a cohort member is
    //    LOADING until that count shows every other member ready (READY), and IN WORLD once the transfer commits.
    //  - The run's first load and a new loop's (the barrier's target is the city and no building is entered: the whole party
    //    loads the city together): every other member in the city is LOADING; the view has no per-member readiness for it.
    //  - A member already standing in the destination (members[].area, no pendingArea) is IN WORLD and counted as in.
    //  - Anyone else is IN WORLD where members[].area says (or LOADING into their own pendingArea elsewhere), not counted.
    //  - Me: LOADING with my real progress, READY once my presentation is ready while the game still holds me, IN WORLD after.
    // A replicated per-member value (Astra contract, docs/DEV_LOG.md 2026-10-18: members[].loadProgress and members[].loadState)
    // is read through ReplicatedProgress and ReplicatedState only; today both report unknown.

    /// <summary>A party member's chip on the loading screen's party panel.</summary>
    public enum PartyLoadState { Waiting, Loading, Ready, InWorld, Dropped, Extracted }

    /// <summary>One row of the party panel (NativePartyLoadModel.Build), in drawing order.</summary>
    public struct PartyLoadRow
    {
        public string Account,Name,Weapon,Place,Chip;public int Seat,PlaceAccent;public PartyLoadState State;
        /// <summary>The viewing account (always the first row), the run's host, and a member taking part in this load (counted by
        /// the footer and the header's ready count).</summary>
        public bool You,Host,Counted;
        /// <summary>The bar's real value (0..1: my own load, or a replicated one), or -1: none known (an indeterminate bar while
        /// loading).</summary>
        public float Progress;
        /// <summary>Seconds since this row's state changed in this load (the fill-and-tick on arrival; large when the row was first
        /// seen in its state), and since the row first showed in this load (its entrance).</summary>
        public float StateAge,Age;
        /// <summary>The row's place in the panel (the entrance stagger).</summary>
        public int Order;
        public bool Arrived=>State==PartyLoadState.Ready||State==PartyLoadState.InWorld;
    }

    /// <summary>
    /// The party panel's rows, header and footer from one view (one instance per screen, single thread). Order: me first (YOU),
    /// then the members in this load by seat, then everyone else by seat (an unbound seat last). The footer reads
    /// "WAITING FOR n PLAYERS…" while a counted member (me included) is still loading, then "EVERYONE'S IN". Allocation-free once
    /// each name has been seen (labels cached, fixed arrays).
    /// </summary>
    public sealed class NativePartyLoadModel
    {
        public const int Max=8;
        /// <summary>Unknown progress.</summary>
        public const float Unknown=-1;
        /// <summary>The row entrance (s) and its stagger per row, the arrival's fill, when its tick lands and its pop.</summary>
        public const float RowIn=.32f,Stagger=.06f,Fill=.35f,TickAt=.18f,TickPop=.22f;
        /// <summary>A state first seen this long ago counts as settled (no fill or pop for a member already in when the panel opened).</summary>
        public const float Settled=10;
        public static readonly string[] Chips={"WAITING","LOADING","READY","IN WORLD","DROPPED","EXTRACTED"};
        public const string Everyone="EVERYONE'S IN",ReadyPrefix="READY ";
        private static readonly string[] heads={"PARTY","PARTY · 1","PARTY · 2","PARTY · 3","PARTY · 4","PARTY · 5","PARTY · 6","PARTY · 7","PARTY · 8"};
        private static readonly string[] waiting=MakeWaiting();
        private static readonly string[] more={"","+1 MORE","+2 MORE","+3 MORE","+4 MORE","+5 MORE","+6 MORE","+7 MORE"};
        private static string[] MakeWaiting(){var s=new string[Max+1];for(int i=0;i<=Max;i++)s[i]="WAITING FOR "+i+(i==1?" PLAYER…":" PLAYERS…");return s;}
        /// <summary>The footer while n counted members are still loading (n clamped to 0..Max).</summary>
        public static string WaitingFor(int n)=>waiting[n<0?0:n>Max?Max:n];
        /// <summary>The footer's note for rows the panel could not fit (k 1..7), else null.</summary>
        public static string More(int k)=>k>0&&k<more.Length?more[k]:null;

        public readonly PartyLoadRow[] Rows=new PartyLoadRow[Max];public int Count;
        /// <summary>Checks only: stand-ins for the replicated adapters (null: ReplicatedProgress and ReplicatedState).</summary>
        internal Func<ExpeditionViewMember,float> ProgressOverride;internal Func<ExpeditionViewMember,PartyLoadState?> StateOverride;
        /// <summary>Members taking part in this load, how many of them are still loading (the footer's n) and how many are in.</summary>
        public int Counted,Pending,ReadyCount;
        public string Header,Aggregate,Footer;public bool EveryoneIn;
        /// <summary>This load as the rows remember it (NativeStageLoading.Serial): a new load starts every entrance again.</summary>
        public int Serial{get;private set;}=int.MinValue;

        private struct Memory{public string Account;public PartyLoadState State;public float Seen,Changed;public bool Used;}
        private readonly Memory[] memory=new Memory[Max];
        private readonly PartyLoadRow[] scratch=new PartyLoadRow[Max];private readonly int[] order=new int[Max];
        private readonly bool[] unknown=new bool[Max];

        /// <summary>No panel this frame.</summary>
        public void Clear(){Count=Counted=Pending=ReadyCount=0;Header=Aggregate=Footer=null;EveryoneIn=false;}

        /// <summary>
        /// Rebuilds the rows from `v` (the live view, host and guests alike) and this client's own load: `localProgress` the
        /// loading screen's shown progress (0..1), `localReady` its presentation is ready (the readiness it reports). `serial` is
        /// the loading screen's load (a new one restarts the entrances), `now` unscaled seconds.
        /// </summary>
        public void Build(ExpeditionView v,float localProgress,bool localReady,int serial,float now,ExpeditionLabels labels)
        {
            Clear();if(v?.members==null||labels==null)return;
            if(serial!=Serial){Serial=serial;for(int i=0;i<memory.Length;i++)memory[i]=default;}
            string viewer=v.viewer??v.mine?.account;var barrier=v.areaBarrier;bool held=barrier!=null&&barrier.active;
            ExpeditionViewMember self=null;var members=v.members;
            for(int i=0;i<members.Length;i++){var m=members[i];if(m?.account!=null&&m.account==viewer){self=m;break;}}
            // Where this load goes: my own transfer's destination, else the barrier's target, else the view's area.
            bool transfer=self?.pendingArea!=null;string target=transfer?self.pendingArea:barrier?.target??v.area;
            bool together=!transfer&&held&&target!=null&&target==v.world&&!AnyEntered(v);
            int n=0,others=0,known=0,cohort=0,cohortKnown=0,open=0;
            for(int i=0;i<members.Length&&n<Max;i++)
            {
                var m=members[i];if(m?.account==null)continue;
                var r=new PartyLoadRow{Account=m.account,Name=labels.Who(string.IsNullOrEmpty(m.name)?m.account:m.name),Weapon=string.IsNullOrEmpty(m.weapon)?null:m.weapon,
                    Seat=m.seat,Host=m.host,You=m.account==viewer,Progress=Unknown};
                string at=m.pendingArea??m.area;r.Place=NativeExpeditionJoinZones.Place(at,v.world);r.PlaceAccent=NativeExpeditionJoinZones.PlaceAccent(at,v.world);
                var state=StateOverride!=null?StateOverride(m):ReplicatedState(m);float progress=ProgressOverride!=null?ProgressOverride(m):ReplicatedProgress(m);bool guess=false;
                if(m.extracted)r.State=PartyLoadState.Extracted;
                else if(!m.connected)r.State=PartyLoadState.Dropped;
                else if(r.You)
                {
                    r.Counted=true;
                    if(localReady){r.State=held?PartyLoadState.Ready:PartyLoadState.InWorld;r.Progress=1;}
                    else{r.State=PartyLoadState.Loading;r.Progress=Clamp01(localProgress);}
                }
                else
                {
                    // This load's members: my transfer's cohort (their pendingArea is my destination), or the whole party in the city
                    // for a first load; a member already standing in the destination is in it too (arrived).
                    bool loading=transfer?m.pendingArea==target:together&&m.pendingArea==null&&m.area==target;
                    bool resident=!loading&&m.pendingArea==null&&target!=null&&m.area==target;
                    if(loading)
                    {
                        r.Counted=true;others++;cohort++;r.Progress=progress;
                        if(state!=null)r.State=state.Value;
                        else if(progress>=1)r.State=PartyLoadState.Ready;
                        else{r.State=PartyLoadState.Loading;guess=progress<0;}
                        if(r.Arrived){known++;cohortKnown++;}else if(guess)open++;
                    }
                    else if(resident){r.Counted=true;others++;r.State=state??PartyLoadState.InWorld;if(r.Arrived)known++;}
                    else if(m.pendingArea!=null){r.State=state??PartyLoadState.Loading;r.Progress=progress;}
                    else r.State=state??PartyLoadState.InWorld;
                }
                unknown[n]=guess;scratch[n]=r;n++;
            }
            // The cohort's count (a transfer only: a first load's barrier counts this client alone). My own report may still be on
            // its way, so I am taken off it once I am locally ready: an uncertain count never shows a member in early. The count
            // names nobody, so it only marks the unnamed members in once it covers all of them.
            int ready=known;
            if(transfer&&barrier!=null)
            {
                int counted=Math.Max(0,Math.Min(cohort,barrier.ready-(localReady?1:0)));
                int cohortReady=Math.Max(cohortKnown,Math.Min(counted,cohortKnown+open));ready=known-cohortKnown+cohortReady;
                if(open>0&&cohortReady==cohortKnown+open)for(int i=0;i<n;i++)if(unknown[i]){scratch[i].State=PartyLoadState.Ready;unknown[i]=false;}
            }
            int me=0,meReady=0;for(int i=0;i<n;i++)if(scratch[i].You&&scratch[i].Counted){me=1;if(scratch[i].Arrived)meReady=1;}
            Counted=me+others;Pending=Math.Max(0,Counted-meReady-ready);ReadyCount=Counted-Pending;EveryoneIn=Counted>0&&Pending==0;
            Footer=EveryoneIn?Everyone:WaitingFor(Pending);Aggregate=Counted>0?labels.Pair(ReadyPrefix,ReadyCount,Counted):null;
            // Order: me, then this load's members, then the rest; by seat (unbound last), then as listed.
            for(int i=0;i<n;i++)order[i]=i;
            for(int i=1;i<n;i++){int k=order[i],j=i-1;while(j>=0&&Before(scratch[k],k,scratch[order[j]],order[j])){order[j+1]=order[j];j--;}order[j+1]=k;}
            for(int i=0;i<n;i++)
            {
                var r=scratch[order[i]];r.Order=i;r.Chip=Chips[(int)r.State];
                int slot=Slot(r.Account,r.State,now);ref var mm=ref memory[slot];
                if(mm.State!=r.State){mm.State=r.State;mm.Changed=now;}
                r.Age=Math.Max(0,now-mm.Seen);r.StateAge=Math.Max(0,now-mm.Changed);Rows[i]=r;
            }
            Count=n;Header=heads[n];
        }
        private static bool Before(in PartyLoadRow a,int ai,in PartyLoadRow b,int bi)
        {
            if(a.You!=b.You)return a.You;if(a.Counted!=b.Counted)return a.Counted;
            int sa=a.Seat>0?a.Seat:int.MaxValue,sb=b.Seat>0?b.Seat:int.MaxValue;if(sa!=sb)return sa<sb;return ai<bi;
        }
        // A row's memory in this load: first seen now (its state counted as settled, so a member already in shows no fill or pop).
        private int Slot(string account,PartyLoadState state,float now)
        {
            int free=-1;for(int i=0;i<memory.Length;i++){if(memory[i].Used&&memory[i].Account==account)return i;if(!memory[i].Used&&free<0)free=i;}
            if(free<0){free=0;for(int i=1;i<memory.Length;i++)if(memory[i].Seen<memory[free].Seen)free=i;}
            memory[free]=new Memory{Account=account,Used=true,Seen=now,Changed=now-Settled,State=state};
            return free;
        }
        // ---- motion (the HUD samples these; reduced effects: no rise, sweep, fill or pop) -------------------------------------------
        /// <summary>A row's entrance (0..1): a CSS ease-out curve over RowIn, staggered by its order; whole with reduced effects.</summary>
        public static float Entrance(in PartyLoadRow p,bool reduced)=>reduced?1:NativeUiMotion.Bezier(.2f,.8f,.2f,1,NativeUiMotion.Clamp01((p.Age-p.Order*Stagger)/RowIn));
        /// <summary>An arrival's bar fill (0..1) over Fill seconds from the state change; full with reduced effects.</summary>
        public static float ArrivalFill(in PartyLoadRow p,bool reduced)=>reduced?1:NativeUiMotion.OutCubic(NativeUiMotion.Clamp01(p.StateAge/Fill));
        /// <summary>The arrival tick's scale: 0 until TickAt, then a pop from 1.45 settling to 1 over TickPop; 1 at once with reduced effects.</summary>
        public static float TickScale(in PartyLoadRow p,bool reduced){if(reduced)return 1;float age=p.StateAge-TickAt;return age<0?0:1+.45f*(1-NativeUiMotion.OutCubic(age/TickPop));}
        /// <summary>Where an indeterminate bar's sweep stands (0..1): each row on its own phase; still (the middle) with reduced effects.</summary>
        public static float Sweep(float now,int order,bool reduced){if(reduced)return .5f;float t=(now*.6f+order*.17f)%1;return t<0?t+1:t;}

        private static bool AnyEntered(ExpeditionView v)
        {var list=v.instances;if(list!=null)for(int i=0;i<list.Length;i++)if(list[i]!=null&&list[i].entered)return true;return false;}
        private static float Clamp01(float x)=>x>=0?x<=1?x:1:0;

        // ---- the replicated per-member load (Astra contract; unknown until the member view carries it) ---------------------------
        /// <summary>The member's replicated load progress (0..1), or -1 unknown. The member view carries none today. Binding the
        /// contract's members[].loadProgress is this one line: <c>=>m==null||ParseState(m.loadState)==null?Unknown:CleanProgress(m.loadProgress);</c></summary>
        public static float ReplicatedProgress(ExpeditionViewMember m)=>m==null||ParseState(m.loadState)==null?Unknown:CleanProgress(m.loadProgress);
        /// <summary>The member's replicated load state, or null unknown (today). Binding members[].loadState is this one line:
        /// <c>=>ParseState(m?.loadState);</c></summary>
        public static PartyLoadState? ReplicatedState(ExpeditionViewMember m)=>ParseState(m?.loadState);
        /// <summary>A replicated progress as the panel takes it: 0..1, anything else (negative, above 1, NaN) unknown.</summary>
        public static float CleanProgress(float raw)=>raw>=0&&raw<=1?raw:Unknown;
        /// <summary>The contract's loadState words ("waiting", "loading", "ready", "world", "dropped"); anything else (null, "", an
        /// older host) unknown.</summary>
        public static PartyLoadState? ParseState(string s)
        {
            switch(s)
            {
                case "waiting":return PartyLoadState.Waiting;case "loading":return PartyLoadState.Loading;case "ready":return PartyLoadState.Ready;
                case "world":return PartyLoadState.InWorld;case "dropped":return PartyLoadState.Dropped;default:return null;
            }
        }
    }

    /// <summary>
    /// The party panel's place on the loading screen (NativeLoadingLayout.Measure with a party): under the title block in the
    /// title column, the two centred together. One column up to four members, two for five to eight when the column is wide
    /// enough. It keeps the most it can: normal rows, then compact rows, then without the map's tag line, then two columns, then a
    /// smaller title (never under MinTitle); only a canvas too short for every member clips the rows (the footer says +n MORE).
    /// </summary>
    public struct NativePartyLayout
    {
        public const float RowNormal=40,RowCompact=30,PadNormal=8,PadCompact=6,HeadNormal=18,HeadCompact=16,FootNormal=18,FootCompact=16,Gap=14,Inset=10;
        public const float MinColumn=190,MaxSingle=560,MaxColumn=400,MinTitle=34,Space=6;
        public LoadingBox Box;public int Columns,PerColumn,Shown,Count;public float Row,Column,Pad,Head,Foot;public bool Compact,Clipped;
        public LoadingBox Header=>new LoadingBox(Box.X+12,Box.Y+Pad,Box.W-24,Head);
        public LoadingBox Footer=>new LoadingBox(Box.X+12,Box.Bottom-Pad-Foot,Box.W-24,Foot);
        /// <summary>Row i's box (column-major: the first PerColumn rows fill the left column).</summary>
        public LoadingBox RowBox(int i)
        {
            int c=PerColumn>0?i/PerColumn:0,r=PerColumn>0?i%PerColumn:0;
            return new LoadingBox(Box.X+Inset+c*(Column+Gap),Box.Y+Pad+Head+Space+r*Row,Column,Row);
        }
        public static float Height(int rows,bool compact)=>compact?2*PadCompact+HeadCompact+FootCompact+Space+4+rows*RowCompact:2*PadNormal+HeadNormal+FootNormal+Space+4+rows*RowNormal;
        // The title block above the panel, from the eyebrow's top to the panel's (NativeLoadingLayout's own spacing).
        private static float Above(float title,bool tag)=>44+title*1.1f+(tag?82:44);

        /// <summary>Places a panel of `count` members (1..Max) in l's title column, re-centring the title block over it.</summary>
        public static void Fit(ref NativeLoadingLayout l,int count)
        {
            int n=Math.Max(1,Math.Min(NativePartyLoadModel.Max,count));var t=l.Text;
            bool two=t.W>=2*MinColumn+Gap+2*Inset;int natural=n>4&&two?2:1,wide=two&&n>1?2:natural;
            float full=l.TitleSize;bool compact=false,tag=true;int cols=natural;float title=full;bool fit=false;
            for(int step=0;step<5&&!fit;step++)
            {
                compact=step>0;tag=step<2;cols=step>=3?wide:natural;title=full;
                float h=Height((n+cols-1)/cols,compact);
                if(step==4)title=Math.Max(MinTitle,Math.Min(full,(t.H-h-Above(0,false))/1.1f));
                fit=Above(title,tag)+h<=t.H+.01f;
            }
            int per=(n+cols-1)/cols;
            if(!fit)per=Math.Max(1,Math.Min(per,(int)Math.Floor((t.H-Above(title,false)-Height(0,true))/RowCompact)));
            float w=cols==1?Math.Min(t.W,MaxSingle):Math.Min(t.W,2*MaxColumn+Gap+2*Inset);
            var p=new NativePartyLayout{Count=n,Columns=cols,PerColumn=per,Shown=Math.Min(n,cols*per),Compact=compact,Row=compact?RowCompact:RowNormal,
                Pad=compact?PadCompact:PadNormal,Head=compact?HeadCompact:HeadNormal,Foot=compact?FootCompact:FootNormal,Column=(w-2*Inset-(cols-1)*Gap)/cols};
            p.Clipped=p.Shown<n;float height=Height(per,compact),total=Above(title,tag)+height,y=t.Y+Math.Max(0,(t.H-total)/2);
            l.TitleSize=title;l.TagHidden=!tag;l.Eyebrow=y;l.Title=y+22;l.Rule=l.Title+title*1.1f+8;l.Mode=l.Rule+14;l.Tag=l.Mode+36;
            float top=y+Above(title,tag);l.Party=top;p.Box=new LoadingBox(t.X,top,w,height);l.Roster=p;
        }
    }
}
