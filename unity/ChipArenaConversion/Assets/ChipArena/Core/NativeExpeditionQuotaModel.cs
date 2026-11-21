using System;

namespace ChipArena.UnityConversion
{
    /// <summary>Where the viewer's zone quota stands (NativeExpeditionQuota.Of): none, the kills running (calm, closing in, the last
    /// few marked), met before the boss starts, the boss fight, the zone cleared.</summary>
    public enum ExpeditionQuotaStage { None, Running, Closing, Last, Complete, Boss, Cleared }

    /// <summary>
    /// The in-run quota module's rules (Claude, 2026-10-20; owner: "improve ui of expedition quota"; docs/EXPEDITION-ENDING-UI.md).
    /// Engine-free and pure: everything is read from the replicated ExpeditionView (the viewer's current instance's progress, target,
    /// cleared, bosses and defeated, the area's boss arrivals, the members' area, alive, connected and extracted flags). Nothing is
    /// decided for the authority. Runtime/NativeExpeditionQuotaHud.cs draws ExpeditionQuotaModel in the cyber HUD kit.
    /// </summary>
    public static class NativeExpeditionQuota
    {
        /// <summary>Escalating emphasis from this many kills left (amber, a breathing glow); the last few hand off to the markers.</summary>
        public const int CloseAt=10;
        /// <summary>The LAST n hand-off: the quota markers' own threshold (Core/NativeQuotaMarks), so the module and the kites turn on together.</summary>
        public static int LastAt=>NativeQuotaMarks.Threshold;
        /// <summary>One bar cell per kill up to this target; a larger target groups kills per cell (NativeHudCyber.SegmentStep).</summary>
        public const int MaxCells=48;

        // ---- motion (s) ---------------------------------------------------------------------------------------------------------------
        /// <summary>A kill: the new cells flash for KillFlash, the count pops by PopScale over CountPop, a light runs along the fill for Sweep.</summary>
        public const float KillFlash=.42f,CountPop=.28f,PopScale=.2f,Sweep=.3f;
        /// <summary>The quota-complete beat: the flash, the title's pop, the hint's entrance, the beat's end; then the module collapses.</summary>
        public const float BeatFlash=.32f,BeatTitle=.5f,BeatHintAt=.9f,BeatEnd=2.7f;
        /// <summary>Height changes (s for a full change): collapsing to the compact strip, growing back, the LAST row's reveal.</summary>
        public const float Collapse=.35f,Grow=.3f,LastReveal=.28f;
        /// <summary>The closing-in glow's breath (s per cycle; steady with Reduced effects).</summary>
        public const float BreathPeriod=1.6f;
        /// <summary>Age with no event yet.</summary>
        public const float Never=1e6f;

        // ---- layout (canvas units, NativeUiMotion.Measure: 900 tall) --------------------------------------------------------------------
        /// <summary>The module's top, its width (narrower canvases shrink it to MinWidth), the tall body, the LAST row, the compact strip,
        /// the beat's hint row. SideReserve keeps it clear of the wallet panel (24 + 220) on the left and the J tag on the right.</summary>
        public const float Top=14,Width=372,MinWidth=300,TallH=62,LastRow=22,CompactH=32,HintRow=24,SideReserve=260;
        /// <summary>The module's width on a canvas W wide.</summary>
        public static float WidthOn(float W)=>Math.Max(MinWidth,Math.Min(Width,W-2*SideReserve));
        /// <summary>The tallest the module gets (the beat's hint, or the LAST row, under the tall body).</summary>
        public static float MaxHeight=>TallH+Math.Max(LastRow,HintRow);
        /// <summary>The crosshair and combat-number band the module must never reach: the centre of the screen, where the aim ray and the
        /// damage numbers over the targets in front of it are (canvas units). The module stays above its top.</summary>
        public static float CombatTop(float H)=>H*.28f;

        /// <summary>What the module reads this frame.</summary>
        public struct Read
        {
            public ExpeditionQuotaStage Stage;public int Progress,Target,Remaining,Loop,Bosses,BossesDown,Party,Alive;public string Run,Area;
        }
        /// <summary>
        /// The viewer's current zone quota from the replicated view only: phase Instance, the current instance of the viewer's own area,
        /// a positive target. Cleared, then the boss phase (bosses[] or a boss arrival in the area), then the quota met, then the kills
        /// left (Last at the markers' threshold, Closing from CloseAt). The party is the members in this area (split runs) or the run,
        /// connected and not extracted; Alive counts those up. Loading and a host transfer turn it off (the objective tag says so).
        /// </summary>
        public static Read Of(ExpeditionView v)
        {
            var q=default(Read);
            if(v==null||v.loading||v.migrating||v.phase!=ExpeditionPhase.Instance||v.instances==null)return q;
            var b=v.CurrentBuilding();if(b==null||b.target<=0)return q;
            if(!string.IsNullOrEmpty(v.area)&&!string.Equals(b.id,v.area,StringComparison.Ordinal))return q;
            q.Progress=Math.Max(0,Math.Min(b.progress,b.target));q.Target=b.target;q.Remaining=b.target-q.Progress;q.Run=v.run;q.Loop=v.loop;q.Area=b.id;
            q.Bosses=b.bosses?.Length??0;if(b.defeated!=null&&b.bosses!=null)foreach(var d in b.defeated)if(Array.IndexOf(b.bosses,d)>=0)q.BossesDown++;
            if(v.members!=null)foreach(var m in v.members)
            {
                if(m==null||!m.connected||m.extracted)continue;if(v.areaIdentity!=null&&!string.Equals(m.area,b.id,StringComparison.Ordinal))continue;
                q.Party++;if(m.alive)q.Alive++;
            }
            bool arrived=false;if(v.bossArrivals!=null)foreach(var a in v.bossArrivals)if(a!=null&&string.Equals(a.area,b.id,StringComparison.Ordinal)){arrived=true;break;}
            if(b.cleared)q.Stage=ExpeditionQuotaStage.Cleared;
            else if(q.Bosses>0||arrived)q.Stage=ExpeditionQuotaStage.Boss;
            else if(q.Remaining<=0)q.Stage=ExpeditionQuotaStage.Complete;
            else if(q.Remaining<=LastAt)q.Stage=ExpeditionQuotaStage.Last;
            else if(q.Remaining<=CloseAt)q.Stage=ExpeditionQuotaStage.Closing;
            else q.Stage=ExpeditionQuotaStage.Running;
            return q;
        }
        /// <summary>The module owns the top centre this frame (the objective tag steps aside for it): a zone quota is the objective.</summary>
        public static bool Owns(ExpeditionView v)=>Of(v).Stage!=ExpeditionQuotaStage.None;

        /// <summary>The stage's tone: calm cyan, closing amber, the last few in the markers' hunt tone, met and cleared green, the boss magenta.</summary>
        public static ExpeditionTone Tone(ExpeditionQuotaStage s)
        {
            switch(s)
            {
                case ExpeditionQuotaStage.Closing:return ExpeditionTone.Warn;case ExpeditionQuotaStage.Last:return ExpeditionTone.Danger;
                case ExpeditionQuotaStage.Complete:case ExpeditionQuotaStage.Cleared:return ExpeditionTone.Good;case ExpeditionQuotaStage.Boss:return ExpeditionTone.Magenta;
                default:return ExpeditionTone.Cyan;
            }
        }
        /// <summary>The closing-in emphasis (0..1): none above CloseAt, rising to full at the markers' threshold.</summary>
        public static float Heat(int remaining,int target)
        {
            if(target<=0||remaining<=0||remaining>CloseAt)return 0;if(remaining<=LastAt)return 1;
            return .35f+.65f*(CloseAt-remaining)/(float)Math.Max(1,CloseAt-LastAt);
        }
        /// <summary>The bar's cells: one per kill up to MaxCells, else the kills one cell stands for (NativeHudCyber.SegmentStep).</summary>
        public static int Cells(int target,out float step)
        {
            if(target<=0){step=1;return 1;}if(target<=MaxCells){step=1;return target;}
            step=NativeHudCyber.SegmentStep(target,MaxCells);return NativeHudCyber.SegmentCount(target,step);
        }
        /// <summary>The cell a kill count reaches into (cells filled by `progress`).</summary>
        public static int CellOf(int progress,int target)
        {
            int n=Cells(target,out float step);if(progress<=0)return 0;return Math.Min(n,(int)Math.Ceiling(progress/step-1e-4));
        }
        /// <summary>The breathing glow (0..1) at time t; steady at .5 with Reduced effects.</summary>
        public static float Breath(double t,bool reduced)
        {
            if(reduced||double.IsNaN(t)||double.IsInfinity(t))return .5f;double p=t/BreathPeriod;p-=Math.Floor(p);return (float)(.5-.5*Math.Cos(p*2*Math.PI));
        }
        internal static float Out(float t){t=t<0?0:t>1?1:t;float u=1-t;return 1-u*u*u;}

        // ---- text (cached; nothing is built per frame) -----------------------------------------------------------------------------------
        public const string Eyebrow="KILL QUOTA",CompleteTitle="QUOTA COMPLETE",BossHint="BOSS INCOMING",ExitHint="EXIT UNSEALED · VOTE TO LEAVE",PartyLabel="PARTY";
        private static readonly string[] lefts=new string[1000],targets=new string[1000];
        /// <summary>"5 LEFT" (cached).</summary>
        public static string Left(int n)=>n>=0&&n<lefts.Length?lefts[n]??=NativeHudText.Int(n)+" LEFT":n+" LEFT";
        /// <summary>"/ 36" (cached).</summary>
        public static string Of(int target)=>target>=0&&target<targets.Length?targets[target]??="/ "+NativeHudText.Int(target):"/ "+target;
    }

    /// <summary>
    /// The quota module's frame (Claude, 2026-10-20). One per screen, single thread: Step it every repaint with the bound view, this
    /// client's unscaled clock and the Reduced effects setting; the HUD draws the fields. A kill (the view's progress rising within the
    /// same quota) flashes the new cells and pops the count; the quota met while this client watched plays the complete beat once
    /// (CueNow on its first frame), then the module collapses to the compact strip for the boss and the cleared zone. A first sight of a
    /// quota already met, a new run, loop or zone, or a re-entry shows the state as it is, without the beat. Allocation-free per frame:
    /// words are built when the quota's identity or counts change.
    /// </summary>
    public sealed class ExpeditionQuotaModel
    {
        private const float Never=NativeExpeditionQuota.Never;
        // ---- the frame ------------------------------------------------------------------------------------------------------------------
        public bool Shown;public ExpeditionQuotaStage Stage;public ExpeditionTone Tone;
        public int Progress,Target,Remaining,Party,Alive,Cells,Bosses,BossesDown;
        /// <summary>The fill (0..1), the closing-in emphasis (0..1), the tall body (1) against the compact strip (0), the LAST row's
        /// reveal, the beat's hint row's reveal (0..1), the count's pop (added scale).</summary>
        public float Fill,Heat,Tall,LastIn,HintIn,Pop;
        /// <summary>Seconds since the last kill (Never: none), and the cells it filled: [FlashFrom, FlashTo).</summary>
        public float KillAge=Never;public int FlashFrom,FlashTo;
        /// <summary>Seconds since the complete beat began (Never: none), and whether it is playing; CueNow on its first frame only.</summary>
        public float BeatAge=Never;public bool Beat,CueNow;
        /// <summary>The module's box (canvas units): X, Y, width and height this frame.</summary>
        public float X,Y,W,H;
        /// <summary>The zone's accent (0xRRGGBB, Core NativeExpeditionJoinZones), for the eyebrow's tick.</summary>
        public int Accent;
        /// <summary>The words: the zone, KILL QUOTA (with the loop after the first), the count and target, the kills left, the
        /// compact strip's state, the beat's count line and hint, the LAST row (the markers' own NativeQuotaMarks.Tag).</summary>
        public string AreaText,LoopText,EyebrowText,CountText,TargetText,LeftText,PhaseText,CompactText,DoneText,HintText,LastText;
        /// <summary>The bottom of the module (canvas units): what sits under it (the transfer banner, the boss plates) starts below.</summary>
        public float Bottom=>Shown?Y+H:0;

        // ---- state ---------------------------------------------------------------------------------------------------------------------------
        private string run,area;private int loop=-1,seenProgress=-1,seenRemaining=-1,seenBosses=-1,seenDown=-1,seenLoop=-1;private ExpeditionQuotaStage seenStage;private bool watched;
        private float killAt=float.NaN,beatAt=float.NaN,last=float.NaN;private string beatRun,beatArea,seenArea;private int beatLoop=-1;

        /// <summary>Forget the quota (no binding, the run ended): the next sight shows its state without a beat.</summary>
        public void Clear(){Shown=false;Stage=ExpeditionQuotaStage.None;run=area=null;loop=-1;seenProgress=-1;watched=false;killAt=beatAt=float.NaN;Tall=1;LastIn=HintIn=0;last=float.NaN;}

        /// <summary>The frame for view `v` at unscaled time `now` on a canvas W wide.</summary>
        public void Step(ExpeditionView v,float now,bool reduced,float canvasW)
        {
            CueNow=false;float dt=float.IsNaN(last)?0:Math.Max(0,Math.Min(.25f,now-last));last=now;
            var q=NativeExpeditionQuota.Of(v);
            if(q.Stage==ExpeditionQuotaStage.None){Shown=false;Stage=q.Stage;if(v==null||v.run!=run)Clear();watched=false;Beat=false;BeatAge=Never;return;}
            // A new run, loop or zone: no beat and no kill flash for what was already there.
            bool same=watched&&string.Equals(q.Run,run,StringComparison.Ordinal)&&q.Loop==loop&&string.Equals(q.Area,area,StringComparison.Ordinal);
            if(!same)
            {
                run=q.Run;loop=q.Loop;area=q.Area;killAt=float.NaN;beatAt=float.NaN;seenProgress=q.Progress;seenStage=q.Stage;watched=true;
                bool tall=q.Stage<=ExpeditionQuotaStage.Complete;Tall=tall?1:0;LastIn=q.Stage==ExpeditionQuotaStage.Last?1:0;HintIn=0;
            }
            // A kill: the new cells flash and the count pops (presentation of the authority's own count; nothing is predicted).
            if(same&&q.Progress>seenProgress){FlashFrom=NativeExpeditionQuota.CellOf(seenProgress,q.Target);FlashTo=NativeExpeditionQuota.CellOf(q.Progress,q.Target);killAt=now;}
            // The beat: a quota this client watched running is met (or the boss already started, or the zone cleared, in the same update).
            bool running=seenStage>=ExpeditionQuotaStage.Running&&seenStage<=ExpeditionQuotaStage.Last,done=q.Stage>=ExpeditionQuotaStage.Complete;
            bool beaten=beatLoop==q.Loop&&string.Equals(beatRun,q.Run,StringComparison.Ordinal)&&string.Equals(beatArea,q.Area,StringComparison.Ordinal);
            if(same&&running&&done&&!beaten){beatRun=q.Run;beatLoop=q.Loop;beatArea=q.Area;beatAt=now;CueNow=true;}
            seenProgress=q.Progress;seenStage=q.Stage;

            Shown=true;Stage=q.Stage;Progress=q.Progress;Target=q.Target;Remaining=q.Remaining;Party=q.Party;Alive=q.Alive;Bosses=q.Bosses;BossesDown=q.BossesDown;
            Cells=NativeExpeditionQuota.Cells(q.Target,out _);Fill=q.Target>0?Math.Min(1f,q.Progress/(float)q.Target):0;
            KillAge=float.IsNaN(killAt)?Never:Math.Max(0,now-killAt);BeatAge=float.IsNaN(beatAt)?Never:Math.Max(0,now-beatAt);Beat=BeatAge<NativeExpeditionQuota.BeatEnd;
            Heat=NativeExpeditionQuota.Heat(q.Remaining,q.Target);
            Pop=reduced||KillAge>=NativeExpeditionQuota.CountPop?0:NativeExpeditionQuota.PopScale*(1-NativeExpeditionQuota.Out(KillAge/NativeExpeditionQuota.CountPop));
            // The tall body while the kills run and through the beat; the compact strip for the boss and the cleared zone.
            float tallTarget=Beat||q.Stage<=ExpeditionQuotaStage.Complete?1:0;
            Tall=reduced?tallTarget:Approach(Tall,tallTarget,dt/(tallTarget>Tall?NativeExpeditionQuota.Grow:NativeExpeditionQuota.Collapse));
            float lastTarget=!Beat&&q.Stage==ExpeditionQuotaStage.Last?1:0;LastIn=reduced?lastTarget:Approach(LastIn,lastTarget,dt/NativeExpeditionQuota.LastReveal);
            float hintTarget=Beat&&BeatAge>=NativeExpeditionQuota.BeatHintAt?1:0;HintIn=reduced?hintTarget:Approach(HintIn,hintTarget,dt/NativeExpeditionQuota.LastReveal);
            Tone=Beat?ExpeditionTone.Good:NativeExpeditionQuota.Tone(q.Stage);
            Words(q);
            W=NativeExpeditionQuota.WidthOn(canvasW);X=(canvasW-W)/2;Y=NativeExpeditionQuota.Top;
            H=NativeExpeditionQuota.CompactH+(NativeExpeditionQuota.TallH-NativeExpeditionQuota.CompactH)*Tall+NativeExpeditionQuota.LastRow*LastIn*Tall+NativeExpeditionQuota.HintRow*HintIn*Tall;
        }
        private static float Approach(float v,float target,float step){if(!(step>0))return v;return v<target?Math.Min(target,v+step):Math.Max(target,v-step);}

        // The words: rebuilt only when the quota's identity or a count they show changes.
        private void Words(NativeExpeditionQuota.Read q)
        {
            if(!string.Equals(q.Area,seenArea,StringComparison.Ordinal)||q.Loop!=seenLoop)
            {
                seenArea=q.Area;seenLoop=q.Loop;var zone=NativeExpeditionJoinZones.Of(q.Area);AreaText=zone.Name;Accent=zone.Accent;LoopText=q.Loop>0?"LOOP "+NativeHudText.Int(q.Loop+1):null;
                EyebrowText=LoopText==null?NativeExpeditionQuota.Eyebrow:NativeExpeditionQuota.Eyebrow+" · "+LoopText;seenBosses=-1;seenRemaining=-1;seenTarget=-1;
            }
            CountText=NativeHudText.Int(q.Progress);TargetText=NativeExpeditionQuota.Of(q.Target);
            if(q.Target!=seenTarget){seenTarget=q.Target;DoneText=NativeHudText.Int(q.Target)+" / "+NativeHudText.Int(q.Target)+" KILLS";}
            if(q.Remaining!=seenRemaining){seenRemaining=q.Remaining;LeftText=NativeExpeditionQuota.Left(q.Remaining);LastText=NativeQuotaMarks.Tag(q.Remaining);}
            if(q.Bosses!=seenBosses||q.BossesDown!=seenDown||CompactText==null||PhaseStage!=q.Stage)
            {
                seenBosses=q.Bosses;seenDown=q.BossesDown;PhaseStage=q.Stage;
                // Cached per stage (for this zone and boss count): the stage changing back and forth builds each strip once.
                int k=(int)q.Stage;
                if(stageCompact[k]==null||!ReferenceEquals(stageArea[k],AreaText)||stageBosses[k]!=q.Bosses||stageDown[k]!=q.BossesDown)
                {
                    string phase;
                    switch(q.Stage)
                    {
                        case ExpeditionQuotaStage.Cleared:phase="ZONE CLEARED · VOTE TO LEAVE";break;
                        case ExpeditionQuotaStage.Boss:phase=q.Bosses>0?"BOSS · "+NativeHudText.Int(q.BossesDown)+" / "+NativeHudText.Int(q.Bosses)+" DOWN":"BOSS FIGHT";break;
                        case ExpeditionQuotaStage.Complete:phase="QUOTA MET · BOSS INCOMING";break;
                        default:phase=NativeExpeditionQuota.Eyebrow;break;
                    }
                    stagePhase[k]=phase;stageCompact[k]=q.Stage==ExpeditionQuotaStage.Cleared?AreaText+" CLEARED · VOTE TO LEAVE":AreaText+" · "+phase;
                    stageArea[k]=AreaText;stageBosses[k]=q.Bosses;stageDown[k]=q.BossesDown;
                }
                PhaseText=stagePhase[k];CompactText=stageCompact[k];
            }
            HintText=Stage==ExpeditionQuotaStage.Cleared?NativeExpeditionQuota.ExitHint:NativeExpeditionQuota.BossHint;
        }
        private ExpeditionQuotaStage PhaseStage=(ExpeditionQuotaStage)(-1);private int seenTarget=-1;
        private const int Stages=(int)ExpeditionQuotaStage.Cleared+1;
        private readonly string[] stagePhase=new string[Stages],stageCompact=new string[Stages],stageArea=new string[Stages];private readonly int[] stageBosses=new int[Stages],stageDown=new int[Stages];
    }
}
