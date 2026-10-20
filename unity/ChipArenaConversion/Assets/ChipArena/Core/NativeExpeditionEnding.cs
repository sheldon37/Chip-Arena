using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    /// <summary>How the viewer's Expedition ended, as the replicated view says it (NativeExpeditionEnding.Classify): the loop won (the world
    /// boss down), the viewer extracted, everyone else extracted without the viewer (run ended), the whole party down (wiped), or a solo
    /// pilot down (failed). None while the run goes on, a dead pilot whose teammates still stand included.</summary>
    public enum ExpeditionEndKind { None, Victory, Extracted, Ended, Wiped, Failed }
    /// <summary>The ending's actions: choose (the victory's extract-or-continue choice on the board), the run board, close (back to the
    /// world), return to the lobby (the pause menu's), return to the main menu (the pause menu's).</summary>
    public enum ExpeditionEndAction { None, Choose, Board, Close, Lobby, Menu }

    /// <summary>What the client knows that the view does not: the pause menu's own conditions for its Return to Lobby and Return to Main
    /// Menu (hosting, offline, a lobby return already counting down, a guest whose host is gone). Flags only, set by the runtime.</summary>
    public struct ExpeditionEndContext{public bool Host,Offline,Returning,HostLost;}

    /// <summary>
    /// The Expedition's end sequence (Claude, 2026-10-20; owner: "improve ui of expedition quota + expedition ending/completion";
    /// docs/EXPEDITION-ENDING-UI.md). Engine-free and pure: the outcome, the run summary, the party and the rewards are read from the
    /// replicated ExpeditionView only (phase, loop, elapsed, instances, dice, members and the viewer's own block); nothing is decided,
    /// rolled, awarded or saved here. Runtime/NativeExpeditionEndingHud.cs draws ExpeditionEndingModel; the account XP column beside the
    /// summary is the account XP panel itself (NativeAccountXpUI.cs, AccountXpDocked).
    /// </summary>
    public static class NativeExpeditionEnding
    {
        // ---- the outcome -------------------------------------------------------------------------------------------------------------
        /// <summary>
        /// The viewer's ending: Victory while the phase is Victory and the viewer has not extracted (down or not: `down`); Extracted once
        /// the viewer's own block says extracted (any phase, the run's end included); Ended when every other pilot extracted without the
        /// viewer; Wiped (a party) or Failed (alone) when no member is connected, alive and not extracted while the viewer is still in the
        /// run (the authority stops the run there and freezes every member's account XP completion). None otherwise, and for a viewer
        /// who is not a member. A host transfer does not end an ending already decided (the victory stays, frozen, its choice waiting);
        /// a wipe is not decided while loading or frozen.
        /// </summary>
        public static ExpeditionEndKind Classify(ExpeditionView v,out bool down)
        {
            down=false;if(v==null||v.mine==null||string.IsNullOrEmpty(v.mine.account)||v.members==null||v.members.Length==0)return ExpeditionEndKind.None;
            if(v.mine.extracted)return ExpeditionEndKind.Extracted;
            if(v.phase==ExpeditionPhase.Victory){down=!v.mine.alive;return ExpeditionEndKind.Victory;}
            if(v.phase==ExpeditionPhase.Ended)return ExpeditionEndKind.Ended;
            if(v.loading||v.migrating)return ExpeditionEndKind.None;
            int inRun=0;bool standing=false;
            foreach(var m in v.members){if(m==null||m.extracted)continue;inRun++;if(m.connected&&m.alive)standing=true;}
            if(standing||v.mine.alive)return ExpeditionEndKind.None;
            down=true;return inRun>1?ExpeditionEndKind.Wiped:ExpeditionEndKind.Failed;
        }
        public static ExpeditionTone Tone(ExpeditionEndKind k)
        {
            switch(k){case ExpeditionEndKind.Victory:return ExpeditionTone.Warn;case ExpeditionEndKind.Extracted:return ExpeditionTone.Cyan;
                case ExpeditionEndKind.Wiped:case ExpeditionEndKind.Failed:return ExpeditionTone.Danger;default:return ExpeditionTone.CyanSoft;}
        }

        // ---- the sequence (s from its start) ---------------------------------------------------------------------------------------------
        /// <summary>The world dims; the letterbox bars come in; the title enters (scale, blur and a glitch settling), its rule and subtitle
        /// follow, a light sweeps it; at RiseAt the title rises into the summary's header while the bars leave.</summary>
        public const float DimIn=.5f,BarsIn=.45f,TitleAt=.25f,TitleIn=.55f,TitleScale=.18f,TitleBlur=6,GlitchFor=.6f,FlashFor=.4f,RuleAt=.35f,SubAt=.6f,SweepAt=.45f,SweepFor=.7f,RiseAt=1.6f,RiseFor=.6f;
        /// <summary>The summary: the panel, the stat count-ups (staggered), the zone route, the party rows, the rewards, the XP column, the
        /// actions; the sequence is settled at SettleAt (a skip jumps there).</summary>
        public const float PanelAt=1.8f,PanelFor=.45f,StatsAt=2.2f,StatStep=.12f,CountFor=.9f,ZonesAt=2.5f,ZoneStep=.1f,RowsAt=2.7f,RowStep=.08f,RewardsAt=3.5f,XpAt=4.1f,ActionsAt=4.6f,ItemIn=.3f,SettleAt=5.2f;
        /// <summary>Count-up granularity: a counted value walks through at most this many precomputed steps (no string per frame).</summary>
        public const int CountSteps=40;
        public static float Out(float t){t=t<0?0:t>1?1:t;float u=1-t;return 1-u*u*u;}
        public static float InOut(float t){t=t<0?0:t>1?1:t;return t<.5f?4*t*t*t:1-(float)Math.Pow(-2*t+2,3)/2;}
        /// <summary>A counted value's step index (0..CountSteps) at sequence time t for a count starting at `at`: ease-out over CountFor.</summary>
        public static int Step(float t,float at)=>(int)Math.Round(CountSteps*Out((t-at)/CountFor));
        /// <summary>The integer a count-up of `final` shows at step k (0..CountSteps): monotonic, 0 first, exactly `final` last.</summary>
        public static long Value(long final,int k){if(k<=0)return 0;if(k>=CountSteps)return final;return (long)Math.Round(final*(k/(double)CountSteps));}

        // ---- text ---------------------------------------------------------------------------------------------------------------------------
        public const string TitleVictory="EXPEDITION COMPLETE",TitleExtracted="EXTRACTED",TitleEnded="RUN ENDED",TitleWiped="WIPED",TitleFailed="EXPEDITION FAILED";
        public const string Skip="SPACE · SKIP",Keys="← → SELECT · ENTER CONFIRM · ESC CLOSE",GuestNote="THE HOST CAN RETURN EVERYONE TO THE LOBBY",LostNote="THE HOST DISCONNECTED · RETURN TO THE MAIN MENU, THEN JOIN TO RECLAIM YOUR SEAT",
            FrozenNote="HOST TRANSFER · THE RUN IS FROZEN",ReturningNote="RETURNING TO THE LOBBY…";
        public static string Clock(long seconds)=>NativeExpeditionPresentation.Clock(seconds);
        public static string Money(long n)=>"$"+n.ToString("N0",CultureInfo.InvariantCulture);
        public static string Label(ExpeditionEndAction a,int nextLoop)
        {
            switch(a){case ExpeditionEndAction.Choose:return "EXTRACT OR GO AGAIN";case ExpeditionEndAction.Board:return "RUN BOARD";case ExpeditionEndAction.Close:return "CLOSE";
                case ExpeditionEndAction.Lobby:return "RETURN TO LOBBY";case ExpeditionEndAction.Menu:return "MAIN MENU";default:return "";}
        }
    }

    /// <summary>
    /// The end sequence's frame (Claude, 2026-10-20). One per screen, single thread: Step it every frame with the bound view, this client's
    /// unscaled clock, Reduced effects and the client's flags. A transition into an ending that this client watched starts the cinematic
    /// (OpensScreen on its first frame); a first sight of a run already ended (a late joiner, a reconnect, a restart) shows the summary
    /// settled, without the cinematic, as the old victory screen did. Words and the count-ups' steps are built when what they show
    /// changes (a cheap signature of the shown fields, never the view's revision); the frame itself allocates nothing.
    /// </summary>
    public sealed class ExpeditionEndingModel
    {
        public const int MaxStats=4,MaxZones=4,MaxRows=8,MaxChips=8,MaxActions=3;
        private const int K=NativeExpeditionEnding.CountSteps;

        // ---- the frame -------------------------------------------------------------------------------------------------------------------
        public bool Shown,Settled,OpensScreen,Down,Frozen;public ExpeditionEndKind Kind;public ExpeditionTone Tone;
        /// <summary>Seconds since the sequence began (a settled ending reports SettleAt).</summary>
        public float Age;
        /// <summary>The words: over the title, the title, under it, a note (down at victory, pilots still in, a host transfer), and the
        /// compact strip the in-scene HUD shows while the screen is closed (J reopens the summary).</summary>
        public string Eyebrow,Title,Subtitle,Note,TagText;
        /// <summary>The world's dim, the letterbox bars, the title's entrance (alpha), added scale, blur (canvas units), glitch cycle (-1
        /// none), the tone flash's alpha, the rule's reach, the subtitle, the light sweep's position (-1 none), the rise into the header (0
        /// centre .. 1 header), the summary panel's entrance.</summary>
        public float Dim,Bars,TitleIn,TitleGrow,TitleBlur,Glitch=-1,Flash,RuleIn,SubIn,Sweep=-1,Rise,PanelIn;
        public int StatCount;public readonly string[] StatLabel=new string[MaxStats],StatText=new string[MaxStats];public readonly float[] StatIn=new float[MaxStats];
        public readonly ExpeditionTone[] StatTone=new ExpeditionTone[MaxStats];
        public int ZoneCount;public readonly string[] ZoneName=new string[MaxZones],ZoneState=new string[MaxZones];public readonly ExpeditionTone[] ZoneTone=new ExpeditionTone[MaxZones];
        public readonly float[] ZoneFill=new float[MaxZones],ZoneIn=new float[MaxZones];public readonly int[] ZoneAccent=new int[MaxZones];public readonly bool[] ZoneHere=new bool[MaxZones];
        public int RowCount,PartySize;public string PartyText;
        public readonly string[] RowName=new string[MaxRows],RowWeapon=new string[MaxRows],RowStatus=new string[MaxRows];public readonly ExpeditionTone[] RowTone=new ExpeditionTone[MaxRows];
        public readonly bool[] RowYou=new bool[MaxRows],RowHost=new bool[MaxRows];public readonly float[] RowIn=new float[MaxRows];
        public string ContinueText;public ExpeditionTone ContinueTone;public float ContinueFill=-1;
        public int ChipTotal,ChipShown;public readonly string[] ChipId=new string[MaxChips];public readonly int[] ChipCount=new int[MaxChips];public string ChipsText,ChipsMore,WalletText,LootText,WaitingText;public ExpeditionTone LootTone;
        public float RewardsIn,XpIn,ActionsIn;
        public int ActionCount,Focus;public readonly ExpeditionEndAction[] Action=new ExpeditionEndAction[MaxActions];public readonly string[] ActionLabel=new string[MaxActions];
        public readonly bool[] ActionEnabled=new bool[MaxActions],ActionPrimary=new bool[MaxActions];public string ActionNote;
        public const string StatsLabel="RUN SUMMARY",ZonesLabel="THE ROUTE",PartyLabel="PARTY",RewardsLabel="YOUR RUN",ChipsLabel="YOUR BUILD",WalletLabel="RUN WALLET";

        // ---- state ---------------------------------------------------------------------------------------------------------------------------
        private string run,watchedRun;private int loop=-1;private ExpeditionEndKind seenKind;private float start=float.NaN;private bool skipped;
        private long signature;private double frozenElapsed=-1;
        private readonly string[][] statSteps={new string[K+1],new string[K+1],new string[K+1],new string[K+1]};private readonly bool[] statCounts=new bool[MaxStats];
        private readonly string[] walletSteps=new string[K+1];private bool walletCounts;
        private readonly List<ExpeditionViewMember> order=new List<ExpeditionViewMember>(16);private readonly Dictionary<string,int> chipTally=new Dictionary<string,int>(StringComparer.Ordinal);

        /// <summary>No ending (no binding): the next ending is shown settled unless this client watches it happen.</summary>
        public void Clear(){Shown=false;Kind=ExpeditionEndKind.None;run=watchedRun=null;loop=-1;seenKind=ExpeditionEndKind.None;start=float.NaN;signature=0;frozenElapsed=-1;}
        /// <summary>Jump to the settled summary (a key, a click).</summary>
        public void Skip(){if(Shown)skipped=true;}
        /// <summary>The sequence is still playing (a skip has something to skip).</summary>
        public bool Playing=>Shown&&!Settled;

        /// <summary>The frame for view `v` at unscaled time `now`.</summary>
        public void Step(ExpeditionView v,float now,bool reduced,ExpeditionEndContext ctx)
        {
            OpensScreen=false;var kind=NativeExpeditionEnding.Classify(v,out bool down);
            bool live=v!=null&&watchedRun!=null&&string.Equals(watchedRun,v.run,StringComparison.Ordinal)&&seenKind==ExpeditionEndKind.None;
            watchedRun=v?.run;
            if(kind==ExpeditionEndKind.None){seenKind=kind;Shown=false;Kind=kind;return;}
            // One ending per run (per loop for a victory): the extracted pilot's ending survives the others' next loop.
            int key=kind==ExpeditionEndKind.Victory?v.loop:-1;
            bool same=Kind==kind&&loop==key&&string.Equals(run,v.run,StringComparison.Ordinal);
            if(!same)
            {
                Kind=kind;run=v.run;loop=key;skipped=false;signature=0;frozenElapsed=v.elapsed;
                // Watched happening: the cinematic from now (OpensScreen once). A first sight of it: settled.
                if(live&&!reduced){start=now;OpensScreen=true;}else{start=float.NegativeInfinity;OpensScreen=live;}
            }
            seenKind=kind;Shown=true;Down=down;Frozen=v.migrating;
            long sig=Signature(v,ctx);if(sig!=signature){signature=sig;Words(v,ctx);}
            Age=float.IsNegativeInfinity(start)||skipped||reduced?NativeExpeditionEnding.SettleAt:Math.Max(0,now-start);
            Settled=Age>=NativeExpeditionEnding.SettleAt;Sample(Settled?float.PositiveInfinity:Age);
        }

        // ---- the timeline ---------------------------------------------------------------------------------------------------------------------
        private void Sample(float t)
        {
            float O(float at,float dur)=>NativeExpeditionEnding.Out((t-at)/dur);
            Dim=.86f*O(0,NativeExpeditionEnding.DimIn);
            Bars=t<NativeExpeditionEnding.RiseAt?O(0,NativeExpeditionEnding.BarsIn):1-O(NativeExpeditionEnding.RiseAt,NativeExpeditionEnding.RiseFor);
            float title=O(NativeExpeditionEnding.TitleAt,NativeExpeditionEnding.TitleIn);TitleIn=title;TitleGrow=NativeExpeditionEnding.TitleScale*(1-title);TitleBlur=NativeExpeditionEnding.TitleBlur*(1-title);
            float g=t-NativeExpeditionEnding.TitleAt;Glitch=g>=0&&g<NativeExpeditionEnding.GlitchFor?g/NativeExpeditionEnding.GlitchFor:-1;
            Flash=g>=0&&g<NativeExpeditionEnding.FlashFor?.16f*(1-g/NativeExpeditionEnding.FlashFor):0;
            RuleIn=O(NativeExpeditionEnding.RuleAt,.45f);SubIn=O(NativeExpeditionEnding.SubAt,.4f);
            float s=t-NativeExpeditionEnding.SweepAt;Sweep=s>=0&&s<NativeExpeditionEnding.SweepFor?s/NativeExpeditionEnding.SweepFor:-1;
            Rise=NativeExpeditionEnding.InOut((t-NativeExpeditionEnding.RiseAt)/NativeExpeditionEnding.RiseFor);PanelIn=O(NativeExpeditionEnding.PanelAt,NativeExpeditionEnding.PanelFor);
            for(int i=0;i<StatCount;i++)
            {
                float at=NativeExpeditionEnding.StatsAt+i*NativeExpeditionEnding.StatStep;StatIn[i]=O(at,NativeExpeditionEnding.ItemIn);
                StatText[i]=statSteps[i][statCounts[i]?Math.Max(0,Math.Min(K,float.IsPositiveInfinity(t)?K:NativeExpeditionEnding.Step(t,at))):K];
            }
            for(int i=0;i<ZoneCount;i++)ZoneIn[i]=O(NativeExpeditionEnding.ZonesAt+i*NativeExpeditionEnding.ZoneStep,NativeExpeditionEnding.ItemIn);
            for(int i=0;i<RowCount;i++)RowIn[i]=O(NativeExpeditionEnding.RowsAt+i*NativeExpeditionEnding.RowStep,NativeExpeditionEnding.ItemIn);
            RewardsIn=O(NativeExpeditionEnding.RewardsAt,.35f);
            WalletText=walletSteps[walletCounts?Math.Max(0,Math.Min(K,float.IsPositiveInfinity(t)?K:NativeExpeditionEnding.Step(t,NativeExpeditionEnding.RewardsAt+.1f))):K];
            XpIn=O(NativeExpeditionEnding.XpAt,.4f);ActionsIn=O(NativeExpeditionEnding.ActionsAt,.35f);
            if(float.IsPositiveInfinity(t)){Glitch=-1;Sweep=-1;Flash=0;Bars=0;}
        }

        // ---- the words (on change only) -------------------------------------------------------------------------------------------------------
        // What the summary shows, hashed without allocating: the counts, flags and identities the words are built from.
        private long Signature(ExpeditionView v,ExpeditionEndContext ctx)
        {
            unchecked
            {
                long h=1469598103934665603L;void Mix(long x){h=(h^x)*1099511628211L;}
                Mix((int)Kind);Mix(Down?1:0);Mix(v.loop);Mix((int)v.phase);Mix(v.migrating?1:0);Mix(v.freshRun?1:0);Mix(v.finalUnlocked?1:0);Mix(StringHash(v.area));Mix(StringHash(v.host));
                Mix(ctx.Host?1:0);Mix(ctx.Offline?1:0);Mix(ctx.Returning?1:0);Mix(ctx.HostLost?1:0);
                foreach(var i in v.instances){if(i==null)continue;Mix(StringHash(i.id));Mix(i.progress);Mix(i.target);Mix(i.cleared?1:0);Mix(i.entered?1:0);Mix(i.bosses?.Length??0);Mix(i.defeated?.Length??0);}
                foreach(var m in v.members){if(m==null)continue;Mix(StringHash(m.account));Mix(StringHash(m.name));Mix(StringHash(m.weapon));Mix((m.alive?1:0)|(m.connected?2:0)|(m.extracted?4:0)|(m.host?8:0)|(m.extractVote?16:0));
                    if(Array.IndexOf(v.continueVotes,m.account)>=0)Mix(32);if(m.xpCompletion!=null){Mix(m.xpCompletion.seconds);Mix(m.xpCompletion.won?1:0);}}
                var me=v.mine;Mix(me.wallet);Mix(me.extractionSaved?1:0);Mix(me.alive?1:0);foreach(var c in me.chips){if(c==null)continue;Mix(StringHash(c.id));Mix(c.count);}
                foreach(var r in me.rewards)if(r!=null&&!r.consumed)Mix(StringHash(r.id));foreach(var d in v.dice){if(d==null)continue;Mix(StringHash(d.boss));Mix(d.eligible?.Length??0);}
                foreach(var l in v.loot)if(l!=null&&l.source=="boss")Mix(StringHash(l.id)^(l.claimed?3:0));
                return h;
            }
        }
        private static int StringHash(string s)=>s==null?0:s.GetHashCode();

        private void Words(ExpeditionView v,ExpeditionEndContext ctx)
        {
            var me=v.mine;var self=v.Member(v.viewer);var completion=self?.xpCompletion!=null&&self.xpCompletion.completionId!=null&&self.xpCompletion.completionId.StartsWith("exp_"+v.run+"_",StringComparison.Ordinal)?self.xpCompletion:null;
            int cleared=0,total=0,kills=0,bosses=0;var current=v.CurrentBuilding();
            foreach(var i in v.instances){if(i==null)continue;total++;if(i.cleared)cleared++;kills+=Math.Max(0,Math.Min(i.progress,i.target));if(i.defeated!=null)bosses+=i.defeated.Length;}
            bool worldDown=v.phase==ExpeditionPhase.Victory;foreach(var d in v.dice)if(d!=null&&NativeExpeditionPresentation.Name(d.boss)==NativeExpeditionPresentation.WorldBoss)worldDown=true;
            if(worldDown)bosses++;
            string where=Where(v,current);Tone=NativeExpeditionEnding.Tone(Kind);
            // The title moment.
            switch(Kind)
            {
                case ExpeditionEndKind.Victory:
                    Eyebrow="LOOP "+(v.loop+1)+" COMPLETE · THE WORLD BOSS IS DOWN";Title=NativeExpeditionEnding.TitleVictory;
                    Subtitle="EXTRACT WITH YOUR BUILD, OR GO AGAIN INTO LOOP "+(v.loop+2);Note=Down?"YOU WERE DOWN WHEN IT FELL · GET REPRINTED TO EXTRACT":null;break;
                case ExpeditionEndKind.Extracted:
                {
                    bool won=completion!=null&&completion.won;int still=StillIn(v);
                    Eyebrow=(won?"EXPEDITION COMPLETE · ":"")+"LOOP "+(v.loop+1)+" · "+cleared+" / "+total+" ZONES CLEARED";Title=NativeExpeditionEnding.TitleExtracted;
                    Subtitle=v.freshRun?"YOU LEFT THE RUN · YOUR NEXT EXPEDITION STARTS FRESH":me.extractionSaved?"EXTRACTION SAVED · YOUR BUILD AND REWARDS ARE BANKED":"WAITING FOR THE SAVE · REWARDS ARE FINAL ONCE IT IS CONFIRMED";
                    Note=v.phase==ExpeditionPhase.Ended?"THE RUN HAS ENDED · EVERY PILOT EXTRACTED":still>0?still+(still==1?" PILOT IS":" PILOTS ARE")+" STILL IN THE RUN":null;
                    if(!v.freshRun&&!me.extractionSaved)Tone=ExpeditionTone.Warn;break;
                }
                case ExpeditionEndKind.Ended:Eyebrow="LOOP "+(v.loop+1);Title=NativeExpeditionEnding.TitleEnded;Subtitle="THE OTHER PILOTS EXTRACTED · THE RUN IS OVER";Note=null;break;
                case ExpeditionEndKind.Wiped:Eyebrow="EXPEDITION FAILED · LOOP "+(v.loop+1);Title=NativeExpeditionEnding.TitleWiped;Subtitle="THE WHOLE PARTY IS DOWN"+(where!=null?" · "+where:"");Note=null;break;
                default:Eyebrow="PILOT DOWN · LOOP "+(v.loop+1);Title=NativeExpeditionEnding.TitleFailed;Subtitle="YOU WENT DOWN ALONE"+(where!=null?" · "+where:"");Note=null;break;
            }
            if(v.migrating)Note=NativeExpeditionEnding.FrozenNote;
            TagText=Title+" · J · RUN SUMMARY";
            // The stats: the run's time (the viewer's frozen completion, else the clock when the ending began), zones, quota kills, bosses.
            long seconds=completion!=null?completion.seconds:(long)Math.Floor(Math.Max(0,frozenElapsed));
            StatCount=4;
            Stat(0,"RUN TIME",seconds,ExpeditionTone.Ink,n=>NativeExpeditionEnding.Clock(n));
            Stat(1,"ZONES CLEARED",cleared,cleared==total&&total>0?ExpeditionTone.Good:ExpeditionTone.Ink,n=>n+" / "+total);
            Stat(2,"QUOTA KILLS",kills,ExpeditionTone.Ink,n=>n.ToString("N0",CultureInfo.InvariantCulture));
            Stat(3,"BOSSES DOWN",bosses,bosses>0?ExpeditionTone.Magenta:ExpeditionTone.Ink,n=>n.ToString(CultureInfo.InvariantCulture));
            // The route: each zone of this loop, then the world boss, with where the run ended.
            ZoneCount=0;
            foreach(var i in v.instances)
            {
                if(i==null||ZoneCount>=MaxZones-1)continue;int z=ZoneCount++;var zone=NativeExpeditionJoinZones.Of(i.id);ZoneName[z]=zone.Name;ZoneAccent[z]=zone.Accent;
                ZoneFill[z]=i.target>0?Math.Min(1f,i.progress/(float)i.target):0;bool here=(Kind==ExpeditionEndKind.Wiped||Kind==ExpeditionEndKind.Failed)&&v.phase==ExpeditionPhase.Instance&&string.Equals(v.area,i.id,StringComparison.Ordinal);ZoneHere[z]=here;
                string quota=Math.Min(i.progress,i.target)+" / "+i.target;
                if(i.cleared){ZoneState[z]="CLEARED";ZoneTone[z]=ExpeditionTone.Good;ZoneFill[z]=1;}
                else if(here){ZoneState[z]=(i.bosses!=null&&i.bosses.Length>0?"DOWN AT THE BOSS · ":"DOWN HERE · ")+quota;ZoneTone[z]=ExpeditionTone.Danger;}
                else if(i.entered){ZoneState[z]="UNFINISHED · "+quota;ZoneTone[z]=ExpeditionTone.Warn;}
                else{ZoneState[z]="NOT ENTERED";ZoneTone[z]=ExpeditionTone.Muted;ZoneFill[z]=0;}
            }
            if(ZoneCount>0)
            {
                int z=ZoneCount++;ZoneName[z]=NativeExpeditionPresentation.WorldBoss;ZoneAccent[z]=NativeExpeditionJoinZones.FallbackHex;ZoneHere[z]=false;
                if(worldDown){ZoneState[z]="DEFEATED";ZoneTone[z]=ExpeditionTone.Good;ZoneFill[z]=1;}
                else if(v.phase==ExpeditionPhase.FinalBoss&&(Kind==ExpeditionEndKind.Wiped||Kind==ExpeditionEndKind.Failed)){ZoneState[z]="DOWN AT THE WORLD BOSS";ZoneTone[z]=ExpeditionTone.Danger;ZoneFill[z]=1;ZoneHere[z]=true;}
                else if(v.finalUnlocked){ZoneState[z]="UNLOCKED";ZoneTone[z]=ExpeditionTone.Magenta;ZoneFill[z]=1;}
                else{ZoneState[z]="LOCKED · "+cleared+" / "+total+" ZONES";ZoneTone[z]=ExpeditionTone.Muted;ZoneFill[z]=total>0?cleared/(float)total:0;}
            }
            Party(v);
            Rewards(v);
            Actions(v,ctx);
        }
        private static int StillIn(ExpeditionView v){int n=0;foreach(var m in v.members)if(m!=null&&m.connected&&!m.extracted&&m.account!=v.viewer)n++;return n;}
        /// <summary>Where the run ended for a down party: the zone (or the hub) and what was happening there.</summary>
        private static string Where(ExpeditionView v,ExpeditionViewInstance current)
        {
            if(v.phase==ExpeditionPhase.FinalBoss)return "THE WORLD BOSS";
            if(string.IsNullOrEmpty(v.area))return null;
            string place=string.Equals(v.area,v.world,StringComparison.Ordinal)?"THE HUB":NativeExpeditionJoinZones.Of(v.area).Name;
            if(v.phase!=ExpeditionPhase.Instance||current==null)return place;
            if(current.bosses!=null&&current.bosses.Length>0&&!current.cleared)return place+" · BOSS FIGHT";
            return place+" · KILL QUOTA "+Math.Min(current.progress,current.target)+" / "+current.target;
        }
        private void Stat(int i,string label,long final,ExpeditionTone tone,Func<long,string> text)
        {
            StatLabel[i]=label;StatTone[i]=tone;statCounts[i]=final>0;var steps=statSteps[i];
            for(int k=0;k<=K;k++){long n=NativeExpeditionEnding.Value(final,k);steps[k]=k>0&&NativeExpeditionEnding.Value(final,k-1)==n?steps[k-1]:text(n);}
        }
        private void Party(ExpeditionView v)
        {
            order.Clear();ExpeditionViewMember self=null;foreach(var m in v.members){if(m==null)continue;if(m.account==v.viewer)self=m;else order.Add(m);}
            if(self!=null)order.Insert(0,self);PartySize=order.Count;RowCount=Math.Min(MaxRows,order.Count);
            for(int i=0;i<RowCount;i++)
            {
                var m=order[i];RowName[i]=string.IsNullOrEmpty(m.name)?m.account:m.name;RowWeapon[i]=m.weapon;RowYou[i]=m==self;RowHost[i]=m.host;
                bool ready1=Array.IndexOf(v.continueVotes,m.account)>=0;
                if(m.extracted){RowStatus[i]="EXTRACTED";RowTone[i]=ExpeditionTone.Warn;}
                else if(!m.connected){RowStatus[i]="OFFLINE";RowTone[i]=ExpeditionTone.Muted;}
                else if(Kind==ExpeditionEndKind.Victory&&ready1){RowStatus[i]=m.alive?"READY · LOOP "+(v.loop+2):"READY · DOWN";RowTone[i]=ExpeditionTone.Good;}
                else if(Kind==ExpeditionEndKind.Victory&&m.extractVote){RowStatus[i]="VOTED TO EXTRACT";RowTone[i]=ExpeditionTone.Warn;}
                else if(!m.alive){RowStatus[i]="DOWN";RowTone[i]=ExpeditionTone.Danger;}
                else if(Kind==ExpeditionEndKind.Victory){RowStatus[i]="DECIDING";RowTone[i]=ExpeditionTone.CyanSoft;}
                else{RowStatus[i]="STILL IN THE RUN";RowTone[i]=ExpeditionTone.Good;}
            }
            PartyText=PartySize==1?"SOLO":PartySize+" PILOTS";
            if(Kind==ExpeditionEndKind.Victory)
            {
                var c=NativeExpeditionPresentation.Continue(v);ContinueText="LOOP "+(v.loop+2)+" · "+c.votes+" / "+c.remaining+" READY";
                ContinueTone=c.allAgreed?ExpeditionTone.Good:ExpeditionTone.CyanSoft;ContinueFill=c.remaining>0?Math.Min(1f,c.votes/(float)c.remaining):0;
            }
            else{ContinueText=null;ContinueFill=-1;}
        }
        private void Rewards(ExpeditionView v)
        {
            var me=v.mine;chipTally.Clear();ChipTotal=0;
            foreach(var c in me.chips){if(c==null||string.IsNullOrEmpty(c.id)||c.count<=0)continue;ChipTotal+=c.count;chipTally.TryGetValue(c.id,out int n);chipTally[c.id]=n+c.count;}
            ChipShown=0;
            foreach(var kv in chipTally)
            {
                // The held chips, most copies first (stable by id): the build at the end.
                int slot=ChipShown<MaxChips?ChipShown++:-1;if(slot<0){int low=0;for(int j=1;j<MaxChips;j++)if(Less(j,low))low=j;if(!Before(kv.Key,kv.Value,ChipId[low],ChipCount[low]))continue;slot=low;}
                ChipId[slot]=kv.Key;ChipCount[slot]=kv.Value;
            }
            for(int a=1;a<ChipShown;a++){var id=ChipId[a];int n=ChipCount[a],b=a-1;while(b>=0&&Before(id,n,ChipId[b],ChipCount[b])){ChipId[b+1]=ChipId[b];ChipCount[b+1]=ChipCount[b];b--;}ChipId[b+1]=id;ChipCount[b+1]=n;}
            ChipsText=ChipTotal==0?"NO CHIPS":ChipTotal==1?"1 CHIP":ChipTotal+" CHIPS";int hidden=chipTally.Count-ChipShown;ChipsMore=hidden>0?"+"+hidden:null;
            walletCounts=me.wallet>0;for(int k=0;k<=K;k++){long n=NativeExpeditionEnding.Value(me.wallet,k);walletSteps[k]=k>0&&NativeExpeditionEnding.Value(me.wallet,k-1)==n?walletSteps[k-1]:NativeExpeditionEnding.Money(n);}
            // The world boss's loot from its stored dice record (the reveal and the chip panel hold the detail): yours, or why not.
            LootText=null;LootTone=ExpeditionTone.Magenta;
            foreach(var d in v.dice)
            {
                if(d==null||NativeExpeditionPresentation.Name(d.boss)!=NativeExpeditionPresentation.WorldBoss)continue;var s=NativeExpeditionPresentation.Share(v,d);
                if(s.eligible){LootText="WORLD BOSS LOOT · "+(s.yours==1?"1 CHIP":s.yours+" CHIPS")+" RESERVED FOR YOU";LootTone=ExpeditionTone.Magenta;}
                else{LootText="WORLD BOSS LOOT · NOT ELIGIBLE · YOU WERE DOWN WHEN IT FELL";LootTone=ExpeditionTone.Danger;}
            }
            int waiting=0;foreach(var r in me.rewards)if(r!=null&&!r.consumed)waiting++;
            WaitingText=waiting>0&&!me.extracted?(waiting==1?"1 REWARD UNOPENED":waiting+" REWARDS UNOPENED")+" · C OPENS THE CHIP TRAY":null;
        }
        private bool Less(int a,int b)=>Before(ChipId[b],ChipCount[b],ChipId[a],ChipCount[a]);
        private static bool Before(string id,int n,string other,int m)=>n!=m?n>m:string.CompareOrdinal(id,other)<0;
        private void Actions(ExpeditionView v,ExpeditionEndContext ctx)
        {
            ActionCount=0;ActionNote=null;
            void Add(ExpeditionEndAction a,bool enabled,bool primary){if(ActionCount>=MaxActions)return;int i=ActionCount++;Action[i]=a;ActionLabel[i]=NativeExpeditionEnding.Label(a,v.loop+2);ActionEnabled[i]=enabled;ActionPrimary[i]=primary;}
            bool lobby=(ctx.Host||ctx.Offline)&&!ctx.Returning&&!ctx.HostLost;
            if(ctx.HostLost){Add(ExpeditionEndAction.Menu,true,true);Add(ExpeditionEndAction.Board,true,false);ActionNote=NativeExpeditionEnding.LostNote;}
            else switch(Kind)
            {
                case ExpeditionEndKind.Victory:Add(ExpeditionEndAction.Choose,!v.migrating,true);Add(ExpeditionEndAction.Close,true,false);break;
                case ExpeditionEndKind.Extracted when v.phase!=ExpeditionPhase.Ended&&StillIn(v)>0:Add(ExpeditionEndAction.Menu,true,true);Add(ExpeditionEndAction.Close,true,false);Add(ExpeditionEndAction.Board,true,false);break;
                default:
                    if(ctx.Host||ctx.Offline){Add(ExpeditionEndAction.Lobby,lobby,true);Add(ExpeditionEndAction.Menu,true,false);}
                    else{Add(ExpeditionEndAction.Menu,true,true);ActionNote=NativeExpeditionEnding.GuestNote;}
                    Add(ExpeditionEndAction.Board,true,false);break;
            }
            if(ctx.Returning)ActionNote=NativeExpeditionEnding.ReturningNote;
            if(v.migrating)ActionNote=NativeExpeditionEnding.FrozenNote;
            if(Focus<0||Focus>=ActionCount||!ActionEnabled[Focus])Focus=Primary();
        }
        /// <summary>The primary enabled action's index (else the first enabled, else 0).</summary>
        public int Primary(){for(int i=0;i<ActionCount;i++)if(ActionPrimary[i]&&ActionEnabled[i])return i;for(int i=0;i<ActionCount;i++)if(ActionEnabled[i])return i;return 0;}
        /// <summary>Moves the focus to the next (or previous) enabled action, wrapping.</summary>
        public void Move(int dir)
        {
            if(ActionCount==0)return;dir=dir<0?-1:1;
            for(int n=0,i=Focus;n<ActionCount;n++){i=(i+dir+ActionCount)%ActionCount;if(ActionEnabled[i]){Focus=i;return;}}
        }
        /// <summary>The focused action when the actions are on screen and enabled (None otherwise).</summary>
        public ExpeditionEndAction Chosen=>Shown&&ActionsIn>.5f&&Focus>=0&&Focus<ActionCount&&ActionEnabled[Focus]?Action[Focus]:ExpeditionEndAction.None;
    }

    /// <summary>
    /// Where the end sequence sits (canvas units, NativeUiMotion.Measure): the summary panel centred (with the account XP column docked
    /// beside it when an award shows: NativeAccountXpLayout), the big title's centre and its header slot, the stat tiles, the route, the
    /// party block (two columns from three pilots), the rewards and the actions. A short canvas (a large interface scale) turns compact.
    /// </summary>
    public struct ExpeditionEndingLayout
    {
        public const float HostMin=560,HostMax=980,Margin=16,Gap=14;
        public float X,Y,W,H,Pad,HeaderY,EyebrowH,TitleSize,TitleGap,SubH,BlockGap,BigTitle,TitleCenterX,TitleCenterY,HeaderTitleY,SubY,StatsY,StatsH,ZonesY,ZonesH,PartyY,PartyH,RowH,RewardsY,RewardsH,ActionsY,ActionsH,ContinueY;
        public int PartyColumns;public bool Compact,Xp,ZonesShown;
        /// <summary>The summary's frame on a canvas W x H; `xp`: an account XP award will show beside it; `rows` party rows; `zones` route
        /// nodes; `extra` a continue row (victory).</summary>
        public static ExpeditionEndingLayout Of(float W,float H,bool xp,int rows,int zones,bool extra)
        {
            var l=new ExpeditionEndingLayout{Xp=xp&&NativeAccountXpLayout.Fits(W,HostMin)};
            float room=l.Xp?NativeAccountXpLayout.HostRoom(W,HostMin,true)-2*Margin:W-2*Margin;l.W=Math.Min(HostMax,Math.Max(Math.Min(HostMin,W-2*Margin),room));
            l.X=l.Xp?NativeAccountXpLayout.HostLeft(W,l.W,HostMin,true):(W-l.W)/2;
            l.PartyColumns=rows>2?2:1;l.ZonesShown=zones>0;
            for(int pass=0;pass<2;pass++)
            {
                l.Compact=pass==1;float pad=l.Compact?16:28,eyebrow=l.Compact?12:16,title=l.Compact?28:40,titleGap=l.Compact?6:8,sub=l.Compact?14:20,stats=l.Compact?48:70,zonesH=l.Compact?44:56,row=l.Compact?22:30,rewards=l.Compact?56:74,actions=l.Compact?36:46,gap=l.Compact?8:Gap;
                if(l.Compact&&H<520)l.ZonesShown=false;
                int lines=(Math.Max(1,rows)+l.PartyColumns-1)/l.PartyColumns;float party=18+lines*row+(extra?22:0);
                float h=pad+eyebrow+6+title+titleGap+sub+gap+stats+gap+(l.ZonesShown?zonesH+gap:0)+party+gap+rewards+gap+actions+pad;
                l.Pad=pad;l.EyebrowH=eyebrow;l.TitleSize=title;l.TitleGap=titleGap;l.SubH=sub;l.BlockGap=gap;l.StatsH=stats;l.ZonesH=l.ZonesShown?zonesH:0;l.RowH=row;l.RewardsH=rewards;l.ActionsH=actions;l.PartyH=party;l.H=h;
                if(h<=H-2*Margin)break;
            }
            l.H=Math.Min(l.H,H-2*Margin);l.Y=Math.Max(Margin,(H-l.H)/2);
            float y=l.Y+l.Pad;l.HeaderY=y;y+=l.EyebrowH+6;l.HeaderTitleY=y;y+=l.TitleSize+l.TitleGap;l.SubY=y;y+=l.SubH+l.BlockGap;
            l.StatsY=y;y+=l.StatsH+l.BlockGap;if(l.ZonesShown){l.ZonesY=y;y+=l.ZonesH+l.BlockGap;}
            l.PartyY=y;l.ContinueY=y+l.PartyH-(extra?20:0);y+=l.PartyH+l.BlockGap;l.RewardsY=y;y+=l.RewardsH+l.BlockGap;l.ActionsY=y;
            l.BigTitle=Math.Max(44,Math.Min(92,W*.058f));l.TitleCenterX=W/2;l.TitleCenterY=H*.42f;
            return l;
        }
        /// <summary>Stat tile i of n across the panel's inner width.</summary>
        public void Tile(int i,int n,out float x,out float w){float inner=W-2*Pad,gap=10;w=(inner-(n-1)*gap)/Math.Max(1,n);x=X+Pad+i*(w+gap);}
        /// <summary>Route node i of n across the panel's inner width (connectors between them).</summary>
        public void Node(int i,int n,out float x,out float w){float inner=W-2*Pad,gap=18;w=(inner-(n-1)*gap)/Math.Max(1,n);x=X+Pad+i*(w+gap);}
        /// <summary>Party row i: its column and line.</summary>
        public void Row(int i,out float x,out float y,out float w){float inner=W-2*Pad,gap=16;w=PartyColumns==2?(inner-gap)/2:inner;int col=PartyColumns==2?i%2:0,line=PartyColumns==2?i/2:i;x=X+Pad+col*(w+gap);y=PartyY+18+line*RowH;}
        /// <summary>Action button i of n, centred in the actions row (the primary widest).</summary>
        public void Button(int i,int n,out float x,out float w){float bw=Math.Min(240,(W-2*Pad-(n-1)*12)/Math.Max(1,n));float group=n*bw+(n-1)*12;w=bw;x=X+(W-group)/2+i*(bw+12);}
    }
}
