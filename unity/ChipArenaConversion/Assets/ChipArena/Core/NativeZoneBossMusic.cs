using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>One accent of a zone boss song: its time in the clip (s), kind (<see cref="NativeZoneBossSong"/> Accent*) and
    /// 0..1 strength.</summary>
    public struct NativeMusicAccent {public double T;public byte Kind;public float S;}

    /// <summary>
    /// A section's look for the stage (presentation only): `Light` the house and practical lights' level (1 as authored), `Pulse`
    /// how hard they pump on the beat (0..1), `Tint` the cast (-1 cold blue .. +1 warm red), `Haze` the atmosphere's density,
    /// `Heat` the furnaces' and molten glow's level (the Ironworks), `Spark` the sparks' and embers' rate, `Steam` the steam puffs.
    /// </summary>
    public struct NativeZoneBossMood
    {
        public float Light,Pulse,Tint,Haze,Heat,Spark,Steam;
        public NativeZoneBossMood(float light,float pulse,float tint,float haze,float heat,float spark,float steam){Light=light;Pulse=pulse;Tint=tint;Haze=haze;Heat=heat;Spark=spark;Steam=steam;}
        public static NativeZoneBossMood Lerp(in NativeZoneBossMood a,in NativeZoneBossMood b,float k)
        {
            k=k<0?0:k>1?1:k;float L(float x,float y)=>x+(y-x)*k;
            return new NativeZoneBossMood(L(a.Light,b.Light),L(a.Pulse,b.Pulse),L(a.Tint,b.Tint),L(a.Haze,b.Haze),L(a.Heat,b.Heat),L(a.Spark,b.Spark),L(a.Steam,b.Steam));
        }
        /// <summary>The look with nothing playing: every channel as authored.</summary>
        public static readonly NativeZoneBossMood Rest=new NativeZoneBossMood(1,0,0,1,1,1,1);
    }

    /// <summary>
    /// The zone boss song at one moment as the stage reads it (<see cref="NativeZoneBossSong.Evaluate"/>; presentation only).
    /// Off before the boss arrives, when the map has no zone song or its file is missing: then every stage channel is as
    /// authored. `Intro` while the boss intro hold runs (boss clock below the hold). Pulses are 0..1:
    /// <list type="bullet">
    /// <item>`Pulse`: the rate-capped flash channel (beats, downbeats and accents thinned so no two are closer than
    /// <see cref="NativeZoneBossSong.FlashGap"/>: at most 3 a second in Full, 2 in Reduced, none in Calm); `PulseKind` what
    /// fired it (<see cref="NativeZoneBossSong"/> Event*), `Down` the same pulse when it is a downbeat or an accent.</item>
    /// <item>`Cut`: the house lights cut (1 dark) from a cut accent until the next drop; `Flare` a drop's surge (1 at the drop,
    /// decaying over about a second); `Steps` the intro's progress (the opening's chimes or bars played, 0..1; 1 after it);
    /// `Signals` the signal lamps lit (dark at the arrival until the intro's first slam).</item>
    /// <item>`Since`, `SinceDown` and `SinceDrop`: seconds since the last kept flash event, the last that was a downbeat or an
    /// accent, and the last drop (+inf when none): puffs and showers age on them.</item>
    /// <item>`Mood` the section's look, eased from the section played before over a bar.</item>
    /// </list>
    /// </summary>
    public struct NativeZoneBossBeat
    {
        public bool On,Intro,Signals;public double Boss,Song;public int Bar,BeatInBar,Section;public float Phase,BarPhase;
        public NativeZoneBossMood Mood;public float Pulse,Down,Cut,Flare,Steps;public byte PulseKind;public NativeKitFlash Mode;public double Since,SinceDown,SinceDrop;
        /// <summary>A practical light's multiplier (the house lights, lamps, troffers, globes; never a hazard's lamp): the
        /// section's level, the cut, the drop's flare and the beat (its share `pulse` of the light at most; none while the house is cut).</summary>
        public float Light(float pulse=.35f)
        {
            if(!On)return 1;float k=Mood.Light*(1-.72f*Cut)*(1+.7f*Flare);
            return k*(1+pulse*Mood.Pulse*Pulse*(1-Cut));
        }
    }

    /// <summary>
    /// A zone boss song's map (Resources/ZoneBossMusic.json, made by tools/zone_boss_beatmap.py from the owner's MP3; docs/ZONE-BOSS-
    /// MUSIC.md): the beat grid and sections (<see cref="NativeBossTrack"/>), the loop, the accents and the intro edit that lands the
    /// song's drop on the end of the boss intro hold.
    /// <list type="bullet">
    /// <item>The intro edit (<see cref="Clip"/>): after `Preroll` s of silence the clip plays from 0 to `Cut`, then resumes at
    /// `Resume` so the drop (`Drop`, the loop's start) sounds exactly when the hold (`Hold` s) ends and the fight begins; then the
    /// loop plays (`loop.start` to `loop.end`, bar-aligned) for as long as the fight lasts. Preroll + Cut + (Drop - Resume) = Hold.
    /// With Cut 0 the edit is a plain offset (start partway, or a delay).</item>
    /// <item>Every lookup is a pure function of the shared boss clock (NativeArena.BossSongTime), so every peer hears the same bar
    /// and sees the same stage.</item>
    /// </list>
    /// </summary>
    public sealed class NativeZoneBossSong
    {
        public const byte AccentChime=0,AccentSlam=1,AccentCut=2,AccentDrop=3;
        public const byte EventBeat=0,EventDown=1,EventChime=2,EventSlam=3,EventDrop=4;
        /// <summary>The shortest gap between two flashes per mode: at most 3 a second in Full and 2 in Reduced
        /// (NativeHazardKitClock.MaxFlashHz, ReducedFlashHz; WCAG 2.3.1), with a margin; Calm has none.</summary>
        public static double FlashGap(NativeKitFlash mode)=>mode==NativeKitFlash.Reduced?1/1.96:1/2.9;
        public string Id,Title,Map;public NativeBossTrack Track;public NativeMusicAccent[] Accents=Array.Empty<NativeMusicAccent>();
        public double Hold,Preroll,Cut,Resume,Drop;public int DropBar;
        /// <summary>Beats between flash events (2: the downbeat and beat 3, for a fast song; 1: every beat).</summary>
        public int PulseEvery=1;
        private NativeZoneBossMood[] moods=Array.Empty<NativeZoneBossMood>();
        private double[] introT=Array.Empty<double>(),cycT=Array.Empty<double>(),stepT=Array.Empty<double>();private byte[] introK=Array.Empty<byte>(),cycK=Array.Empty<byte>();private float[] introS=Array.Empty<float>(),cycS=Array.Empty<float>();
        private readonly double[][] flashT=new double[3][],flashC=new double[3][];private readonly byte[][] flashK=new byte[3][],flashCK=new byte[3][];private readonly float[][] flashS=new float[3][],flashCS=new float[3][];
        private double firstSlam=double.PositiveInfinity;

        public double LoopLength=>Track.loop.end-Track.loop.start;

        // ---------------- the clock ----------------
        /// <summary>The clip's position (s) at boss time `boss` (s since the boss arrived), -1 in the preroll or before.</summary>
        public double Clip(double boss)
        {
            if(!(boss>=Preroll)||double.IsInfinity(boss))return -1;
            if(boss<Preroll+Cut)return boss-Preroll;
            if(boss<Hold)return boss-Preroll-Cut+Resume;
            double l=LoopLength;return Track.loop.start+((boss-Hold)%l+l)%l;
        }
        /// <summary>The boss time of clip position `clip` the first time it plays (NaN if it never does): the opening, the
        /// resumed run to the drop, then the loop's first pass.</summary>
        public double FirstBoss(double clip)
        {
            if(clip>=0&&clip<Cut)return Preroll+clip;
            if(clip>=Resume&&clip<Drop)return Preroll+Cut+clip-Resume;
            if(clip>=Track.loop.start&&clip<Track.loop.end)return Hold+clip-Track.loop.start;
            return double.NaN;
        }
        /// <summary>The downbeat of bar `bar` (s in the clip; bar 1 is beats[firstDownbeat]).</summary>
        public double Downbeat(int bar){var b=Track.beats;int i=Track.firstDownbeat+4*(bar-1);return i<0?b[0]:i<b.Length?b[i]:Track.duration;}
        /// <summary>The section index for bar `bar` (the first section before it, the last after).</summary>
        public int SectionAt(int bar)
        {
            var s=Track.sections;if(s.Length==0)return -1;if(bar<s[0].fromBar)return 0;
            for(int i=0;i<s.Length;i++)if(bar>=s[i].fromBar&&bar<=s[i].toBar)return i;return s.Length-1;
        }
        private int SectionOfClip(double clip)=>SectionAt(clip<0?0:Track.Position(clip).bar);

        // ---------------- the stage ----------------
        /// <summary>The song at boss time `boss` for the stage in flash mode `mode` (pure: the same clock and mode give the same
        /// answer on every peer; allocation free).</summary>
        public NativeZoneBossBeat Evaluate(double boss,NativeKitFlash mode)
        {
            var b=new NativeZoneBossBeat{Mode=mode,Mood=NativeZoneBossMood.Rest,Section=-1,Since=double.PositiveInfinity,SinceDown=double.PositiveInfinity,SinceDrop=double.PositiveInfinity};
            if(!(boss>=0)||double.IsInfinity(boss)||Track?.beats==null||Track.beats.Length<8)return b;
            b.On=true;b.Boss=boss;b.Intro=boss<Hold;double clip=Clip(boss);b.Song=clip;
            if(clip>=0){var p=Track.Position(clip);b.Bar=p.bar;b.BeatInBar=p.beat;b.Phase=(float)p.phase;b.BarPhase=(float)((p.beat-1+p.phase)/4);}
            b.Section=SectionOfClip(clip);b.Mood=MoodAt(boss,clip,b.Section,mode==NativeKitFlash.Calm?2:1);
            // The flash channel: the last kept event at or before now, shaped by the mode.
            int m=(int)mode;if(m>=0&&m<3&&mode!=NativeKitFlash.Calm&&Last(boss,flashT[m],flashK[m],flashS[m],flashC[m],flashCK[m],flashCS[m],out double at,out byte kind,out float s))
            {
                double since=boss-at;float shape=mode==NativeKitFlash.Reduced?(float)(since<.08?Smooth(since/.08):Math.Exp(-(since-.08)/.3))*.6f:(float)(since<.05?1:Math.Exp(-(since-.05)/.16));
                b.Pulse=since<2?Math.Min(1,s)*shape:0;b.PulseKind=kind;b.Down=kind!=EventBeat?b.Pulse:0;b.Since=since;
                if(Last(boss,flashT[m],flashK[m],flashS[m],flashC[m],flashCK[m],flashCS[m],out double downAt,out _,out _,NotBeat))b.SinceDown=boss-downAt;
            }
            // Cut until the next drop (a softer, slower dip in Reduced and Calm, and there the lights come back over a release instead
            // of at once); the drop's flare (rising gently in Calm).
            double ramp=mode==NativeKitFlash.Calm?.5:mode==NativeKitFlash.Reduced?.15:.06,release=mode==NativeKitFlash.Calm?2:mode==NativeKitFlash.Reduced?.15:0;float depth=mode==NativeKitFlash.Calm?.55f:1;
            if(Last(boss,introT,introK,introS,cycT,cycK,cycS,out double cutAt,out byte cutKind,out _,CutOrDrop))
            {
                if(cutKind==AccentCut)
                {
                    b.Cut=depth*(float)Smooth((boss-cutAt)/ramp);
                    // A release still running from the cut before it carries on (no jump when cuts come close).
                    if(release>0&&Last(cutAt-1e-6,introT,introK,introS,cycT,cycK,cycS,out double d0,out byte k0,out _,CutOrDrop)&&k0==AccentDrop&&boss-d0<release&&Last(d0-1e-6,introT,introK,introS,cycT,cycK,cycS,out double c0,out byte kc,out _,CutOrDrop)&&kc==AccentCut)
                        b.Cut=Math.Max(b.Cut,depth*(float)(Smooth((d0-c0)/ramp)*(1-Smooth((boss-d0)/release))));
                }
                else if(boss-cutAt<release&&Last(cutAt-1e-6,introT,introK,introS,cycT,cycK,cycS,out double before,out byte beforeKind,out _,CutOrDrop)&&beforeKind==AccentCut)
                    b.Cut=depth*(float)(Smooth((cutAt-before)/ramp)*(1-Smooth((boss-cutAt)/release)));
            }
            if(Last(boss,introT,introK,introS,cycT,cycK,cycS,out double dropAt,out _,out float ds,IsDrop))
            {
                double u=boss-dropAt;b.SinceDrop=u;float gain=mode==NativeKitFlash.Calm?.3f:mode==NativeKitFlash.Reduced?.6f:1;double rise=mode==NativeKitFlash.Calm?1.2:mode==NativeKitFlash.Reduced?.12:0;
                if(u<4)b.Flare=(float)(Math.Min(1,ds+.2)*gain*(rise>0?Smooth(u/rise):1)*Math.Exp(-u/(mode==NativeKitFlash.Calm?2.5:1.1)));
            }
            // The preroll: the power sags before the first note (and comes back with it, over the release).
            double sag=.4*depth*Smooth(Math.Min(boss,Preroll)/Math.Max(.25,ramp));
            if(boss<Preroll)b.Cut=Math.Max(b.Cut,(float)sag);else if(boss<Preroll+release)b.Cut=Math.Max(b.Cut,(float)(sag*(1-Smooth((boss-Preroll)/release))));
            // The intro's steps: the opening's chimes (or bars) played so far.
            if(b.Intro){int n=0;for(int i=0;i<stepT.Length;i++)if(stepT[i]<=boss)n++;b.Steps=stepT.Length==0?1:n/(float)stepT.Length;}else b.Steps=1;
            b.Signals=!b.Intro||boss>=firstSlam;
            return b;
        }
        private static readonly Func<byte,bool> CutOrDrop=k=>k==AccentCut||k==AccentDrop,IsDrop=k=>k==AccentDrop,NotBeat=k=>k!=EventBeat;
        // The last of a played list at or before boss time `boss`: the intro's (boss time) before the hold, then the loop's (u time,
        // repeating every LoopLength from the hold). `keep` filters kinds.
        private bool Last(double boss,double[] it,byte[] ik,float[] isv,double[] ct,byte[] ck,float[] cs,out double at,out byte kind,out float s,Func<byte,bool> keep=null)
        {
            at=0;kind=0;s=0;
            if(boss>=Hold&&ct!=null&&ct.Length>0)
            {
                double l=LoopLength,u=boss-Hold;long pass=(long)Math.Floor(u/l);u-=pass*l;
                for(int i=ct.Length-1;i>=0;i--)if(ct[i]<=u&&(keep==null||keep(ck[i]))){at=Hold+pass*l+ct[i];kind=ck[i];s=cs[i];return true;}
                if(pass>0)for(int i=ct.Length-1;i>=0;i--)if(keep==null||keep(ck[i])){at=Hold+(pass-1)*l+ct[i];kind=ck[i];s=cs[i];return true;}
            }
            if(it!=null)for(int i=it.Length-1;i>=0;i--)if(it[i]<=boss&&(keep==null||keep(ik[i]))){at=it[i];kind=ik[i];s=isv[i];return true;}
            return false;
        }
        // The section's mood, eased over `bars` bars (one; two in Calm) from the section played before it (across the intro's splice
        // and the loop's wrap).
        private NativeZoneBossMood MoodAt(double boss,double clip,int sec,int bars,int depth=0)
        {
            if(sec<0||sec>=moods.Length)return NativeZoneBossMood.Rest;if(clip<0)return moods[sec];
            // The contiguous run playing now: its first clip second, the boss time it began and the clip second heard just before
            // it (NaN: silence). The resumed run carries on through the loop's first pass (the loop starts at the drop).
            double runClip,runBoss,before,l=LoopLength;
            if(boss<Preroll+Cut){runClip=0;runBoss=Preroll;before=double.NaN;}
            else if(boss<Hold+l){runClip=Resume;runBoss=Preroll+Cut;before=Cut>0?Cut:double.NaN;}
            else{long pass=(long)Math.Floor((boss-Hold)/l);runClip=Track.loop.start;runBoss=Hold+pass*l;before=Track.loop.end;}
            double secStart=Downbeat(Track.sections[sec].fromBar),entered=Math.Max(secStart,runClip),enteredBoss=runBoss+(entered-runClip);
            int bar=Track.Position(Math.Max(entered,0)).bar;double barLen=Math.Max(.5,Downbeat(bar+1)-Downbeat(bar)),u=(boss-enteredBoss)/(barLen*bars);
            if(u>=1)return moods[sec];
            // Eased from the look on screen at the hand-over: the section heard before it, itself eased (two levels deep, then as
            // authored), so a short section never jumps.
            int prev=secStart>runClip+1e-6?sec-1:double.IsNaN(before)?sec:SectionOfClip(before-1e-3);if(prev<0)prev=sec;
            NativeZoneBossMood from=moods[prev];
            if(depth<2&&prev!=sec){double pb=enteredBoss-1e-4,pc=Clip(pb);if(pc>=0)from=MoodAt(pb,pc,SectionOfClip(pc),bars,depth+1);}
            return NativeZoneBossMood.Lerp(from,moods[sec],(float)Smooth(u));
        }

        // ---------------- building (once, on load) ----------------
        private void Prepare()
        {
            var tr=Track;if(tr?.beats==null||tr.beats.Length<8||tr.sections==null||tr.sections.Length==0||tr.loop==null)throw new FormatException(Id+": missing beat map");
            if(Math.Abs(Drop-tr.loop.start)>1e-3)throw new FormatException(Id+": the loop must start at the drop");
            if(Math.Abs(Preroll+Cut+Drop-Resume-Hold)>2e-3||Preroll<0||Cut<0||Resume<Cut||Drop<Resume)throw new FormatException(Id+": the intro edit does not land the drop on the hold");
            moods=new NativeZoneBossMood[tr.sections.Length];for(int i=0;i<moods.Length;i++)moods[i]=NativeZoneBossMoods.Of(Id,tr.sections[i].id);
            // The accents as played: the intro's (boss time) and the loop's (u time from the loop start).
            Played(Accents.Length,i=>Accents[i].T,i=>Accents[i].Kind,i=>Accents[i].S,out introT,out introK,out introS,out cycT,out cycK,out cycS);
            for(int i=0;i<introT.Length;i++)if(introK[i]==AccentSlam&&introT[i]<firstSlam)firstSlam=introT[i];
            // The intro's steps: the opening's chimes, else its downbeats.
            var steps=new List<double>();foreach(var a in Accents)if(a.Kind==AccentChime&&a.T<Cut)steps.Add(Preroll+a.T);
            if(steps.Count==0)for(int bar=1;Downbeat(bar)<Cut;bar++)if(Downbeat(bar)>=0)steps.Add(Preroll+Downbeat(bar));
            if(steps.Count==0&&Cut<=0)steps.Add(Preroll);stepT=steps.ToArray();
            // Flash candidates (clip time): the pulse beats, weighted by their section's pulse, and the accents.
            var ct=new List<double>();var ck=new List<byte>();var cs=new List<float>();
            for(int i=0;i<tr.beats.Length;i++)
            {
                int rel=i-tr.firstDownbeat,inBar=((rel%4)+4)%4;if(inBar%PulseEvery!=0)continue;int bar=rel>=0?rel/4+1:0;
                // The downbeat strongest, then beat 3, then 2 and 4; scaled by the section's pulse.
                float w=moods[SectionAt(bar)].Pulse*(inBar==0?1:inBar==2?.7f:.5f);if(w>.02f){ct.Add(tr.beats[i]);ck.Add(inBar==0?EventDown:EventBeat);cs.Add(w);}
            }
            foreach(var a in Accents)
            {
                if(a.Kind==AccentCut)continue;byte k=a.Kind==AccentChime?EventChime:a.Kind==AccentSlam?EventSlam:EventDrop;
                // A drop always keeps its flash (its lights also come back from a cut and flare there), so nothing else flashes near it.
                float w=a.Kind==AccentDrop?2:a.Kind==AccentSlam?Math.Min(1,.55f+.45f*a.S):.55f+.35f*a.S;ct.Add(a.T);ck.Add(k);cs.Add(w);
            }
            var order=new int[ct.Count];for(int i=0;i<order.Length;i++)order[i]=i;Array.Sort(order,(x,y)=>ct[x].CompareTo(ct[y]));
            var st=new double[order.Length];var sk=new byte[order.Length];var ss=new float[order.Length];for(int i=0;i<order.Length;i++){st[i]=ct[order[i]];sk[i]=ck[order[i]];ss[i]=cs[order[i]];}
            Played(st.Length,i=>st[i],i=>sk[i],i=>ss[i],out var pt,out var pk,out var ps,out var qt,out var qk,out var qs);
            foreach(var mode in new[]{NativeKitFlash.Full,NativeKitFlash.Reduced})
            {
                double gap=FlashGap(mode);int m=(int)mode;
                // The loop thinned on its circle (it repeats exactly), then the intro thinned against the loop's first pass.
                var keepC=Thin(qt,qs,gap,LoopLength);Pick(qt,qk,qs,keepC,out flashC[m],out flashCK[m],out flashCS[m]);
                var it=new List<double>(pt);var iss=new List<float>(ps);int fixedFrom=it.Count;
                foreach(var t in flashC[m])if(t<gap){it.Add(Hold+t);iss.Add(2);}
                var keepI=Thin(it.ToArray(),iss.ToArray(),gap,0,fixedFrom);var kept=new List<int>();for(int i=0;i<fixedFrom;i++)if(keepI[i])kept.Add(i);
                flashT[m]=new double[kept.Count];flashK[m]=new byte[kept.Count];flashS[m]=new float[kept.Count];for(int i=0;i<kept.Count;i++){flashT[m][i]=pt[kept[i]];flashK[m][i]=pk[kept[i]];flashS[m][i]=ps[kept[i]];}
            }
        }
        // A clip-time list as played: before the hold (the opening, then the resumed run to the drop, in boss time) and the loop
        // (u time from the loop start, sorted).
        private void Played(int n,Func<int,double> t,Func<int,byte> k,Func<int,float> s,out double[] it,out byte[] ik,out float[] isv,out double[] ct,out byte[] ck,out float[] cs)
        {
            var a=new List<(double,byte,float)>();var c=new List<(double,byte,float)>();
            for(int i=0;i<n;i++)
            {
                double x=t(i);if(x>=0&&x<Cut)a.Add((Preroll+x,k(i),s(i)));else if(x>=Resume&&x<Drop)a.Add((Preroll+Cut+x-Resume,k(i),s(i)));
                if(x>=Track.loop.start&&x<Track.loop.end)c.Add((x-Track.loop.start,k(i),s(i)));
            }
            a.Sort((p,q)=>p.Item1.CompareTo(q.Item1));c.Sort((p,q)=>p.Item1.CompareTo(q.Item1));
            it=new double[a.Count];ik=new byte[a.Count];isv=new float[a.Count];for(int i=0;i<a.Count;i++){it[i]=a[i].Item1;ik[i]=a[i].Item2;isv[i]=a[i].Item3;}
            ct=new double[c.Count];ck=new byte[c.Count];cs=new float[c.Count];for(int i=0;i<c.Count;i++){ct[i]=c[i].Item1;ck[i]=c[i].Item2;cs[i]=c[i].Item3;}
        }
        // Greedy thinning: strongest first, an event stays when no kept one lies within `gap` (on a circle of `period` when > 0).
        // Entries from `fixedFrom` on are kept first whatever their strength.
        private static bool[] Thin(double[] t,float[] s,double gap,double period,int fixedFrom=int.MaxValue)
        {
            int n=t.Length;var keep=new bool[n];var order=new int[n];for(int i=0;i<n;i++)order[i]=i;
            Array.Sort(order,(x,y)=>{bool fx=x>=fixedFrom,fy=y>=fixedFrom;if(fx!=fy)return fx?-1:1;int c=s[y].CompareTo(s[x]);return c!=0?c:t[x].CompareTo(t[y]);});
            var kept=new List<double>();
            foreach(int i in order)
            {
                bool ok=true;foreach(var k in kept){double d=Math.Abs(t[i]-k);if(period>0)d=Math.Min(d,period-d);if(d<gap){ok=false;break;}}
                if(ok){keep[i]=true;kept.Add(t[i]);}
            }
            return keep;
        }
        private static void Pick(double[] t,byte[] k,float[] s,bool[] keep,out double[] ot,out byte[] ok,out float[] os)
        {
            int n=0;foreach(var x in keep)if(x)n++;ot=new double[n];ok=new byte[n];os=new float[n];int j=0;
            for(int i=0;i<t.Length;i++)if(keep[i]){ot[j]=t[i];ok[j]=k[i];os[j]=s[i];j++;}
        }
        /// <summary>The kept flash events of mode `mode` as played from the arrival, in boss time, up to `until` s (for the checks).</summary>
        public List<double> FlashTimes(NativeKitFlash mode,double until)
        {
            var list=new List<double>();if(mode==NativeKitFlash.Calm)return list;int m=(int)mode;foreach(var t in flashT[m])if(t<=until)list.Add(t);
            for(long pass=0;;pass++){bool any=false;foreach(var t in flashC[m]){double x=Hold+pass*LoopLength+t;if(x>until)return list;list.Add(x);any=true;}if(!any)return list;}
        }
        /// <summary>The intro's step times (boss s): the opening's chimes, else its downbeats.</summary>
        public double[] StepTimes=>(double[])stepT.Clone();
        /// <summary>The intro's first slam (boss s; +inf when none): the signals light on it.</summary>
        public double FirstSlam=>firstSlam;
        private static double Smooth(double u){u=u<0?0:u>1?1:u;return u*u*(3-2*u);}

        // ---------------- parsing ----------------
        internal static NativeZoneBossSong From(Dictionary<string,object> o,double hold)
        {
            var it=Obj(o,"intro");var tr=Obj(o,"track");var loop=Obj(tr,"loop");
            var song=new NativeZoneBossSong{Id=Str(o,"id"),Title=Str(o,"title"),Map=Str(o,"map"),Hold=Num(it,"hold"),Preroll=Num(it,"preroll"),Cut=Num(it,"cut"),Resume=Num(it,"resume"),Drop=Num(it,"drop"),DropBar=(int)Num(o,"dropBar")};
            if(Math.Abs(song.Hold-hold)>1e-6)throw new FormatException(song.Id+": the intro's hold differs from the file's");
            var beats=Arr(tr,"beats");var t=new NativeBossTrack{id=Str(tr,"id"),file=Str(tr,"file"),duration=Num(tr,"duration"),introEnd=Num(tr,"introEnd"),firstDownbeat=(int)Num(tr,"firstDownbeat"),beats=new double[beats.Count],
                loop=new NativeMusicLoop{start=Num(loop,"start"),end=Num(loop,"end"),fromBar=(int)Num(loop,"fromBar"),toBar=(int)Num(loop,"toBar")}};
            for(int i=0;i<beats.Count;i++)t.beats[i]=(double)beats[i];
            var secs=Arr(tr,"sections");t.sections=new NativeMusicSection[secs.Count];for(int i=0;i<secs.Count;i++){var s=(Dictionary<string,object>)secs[i];t.sections[i]=new NativeMusicSection{id=Str(s,"id"),fromBar=(int)Num(s,"fromBar"),toBar=(int)Num(s,"toBar")};}
            song.Track=t;var acc=Arr(o,"accents");song.Accents=new NativeMusicAccent[acc.Count];
            for(int i=0;i<acc.Count;i++){var a=(Dictionary<string,object>)acc[i];string kind=Str(a,"kind");song.Accents[i]=new NativeMusicAccent{T=Num(a,"t"),S=(float)Num(a,"s"),Kind=kind=="chime"?AccentChime:kind=="slam"?AccentSlam:kind=="cut"?AccentCut:kind=="drop"?AccentDrop:throw new FormatException("unknown accent "+kind)};}
            // A fast song pulses its large lights on the downbeat and beat 3 (half time); a slower one on every beat.
            double bpm=60/((t.beats[t.beats.Length-1]-t.beats[0])/(t.beats.Length-1));song.PulseEvery=bpm>160?2:1;
            song.Prepare();return song;
        }
        private static Dictionary<string,object> Obj(Dictionary<string,object> o,string k)=>o.TryGetValue(k,out var v)&&v is Dictionary<string,object> d?d:throw new FormatException("missing "+k);
        private static List<object> Arr(Dictionary<string,object> o,string k)=>o.TryGetValue(k,out var v)&&v is List<object> a?a:throw new FormatException("missing "+k);
        private static double Num(Dictionary<string,object> o,string k)=>o.TryGetValue(k,out var v)&&v is double d&&!double.IsNaN(d)?d:throw new FormatException("missing "+k);
        private static string Str(Dictionary<string,object> o,string k)=>o.TryGetValue(k,out var v)&&v is string s?s:throw new FormatException("missing "+k);
    }

    /// <summary>
    /// The zone boss songs (Resources/ZoneBossMusic.json; Runtime reads the text once and asks <see cref="Parse"/>). Central Station
    /// (`expedition-transit`) plays Station Chime of Doom, City Ironworks (`expedition-foundry`, the draft `expedition-ironworks`)
    /// plays Hydraulic Hell; NativeZoneMusic names the tracks and falls back to the Siren's song when a file is missing.
    /// </summary>
    public sealed class NativeZoneBossMusic
    {
        public const string ResourceName="ZoneBossMusic";
        public double Hold;public NativeZoneBossSong[] Songs=Array.Empty<NativeZoneBossSong>();
        public static NativeZoneBossMusic Parse(string json)
        {
            if(string.IsNullOrEmpty(json))throw new FormatException("ZoneBossMusic: empty");
            var o=NativeSoundJson.Parse(json) as Dictionary<string,object>??throw new FormatException("ZoneBossMusic: not an object");
            var m=new NativeZoneBossMusic{Hold=o.TryGetValue("hold",out var h)&&h is double d?d:throw new FormatException("ZoneBossMusic: no hold")};
            var list=o.TryGetValue("songs",out var s)&&s is List<object> a?a:throw new FormatException("ZoneBossMusic: no songs");
            m.Songs=new NativeZoneBossSong[list.Count];for(int i=0;i<list.Count;i++)m.Songs[i]=NativeZoneBossSong.From((Dictionary<string,object>)list[i],m.Hold);
            return m;
        }
        /// <summary>The song for a music id ("music/central-station-boss") or a song id, else null.</summary>
        public NativeZoneBossSong Song(string track)
        {
            if(track==null)return null;string id=track.StartsWith("music/",StringComparison.Ordinal)?track.Substring(6):track;
            foreach(var s in Songs)if(s.Id==id)return s;return null;
        }
        /// <summary>The song a map's boss plays when its file is installed (`installed` asks for the music id), else null (the Siren's
        /// song plays and the stage does not react).</summary>
        public NativeZoneBossSong ForMap(string mapId,Func<string,bool> installed)
        {
            var track=NativeZoneMusic.BossTrack(mapId,installed);return track==NativeZoneMusic.SirenSong?null:Song(track);
        }
    }

    /// <summary>The sections' looks (art direction; docs/ZONE-BOSS-MUSIC.md "Moods"). Unknown ids rest as authored.</summary>
    public static class NativeZoneBossMoods
    {
        public static NativeZoneBossMood Of(string song,string section)
        {
            switch(song+"/"+section)
            {
                // Station Chime of Doom: the PA chime and the dark platform build to the express; the signal section goes cold
                // and blue, the rush and the last call run hot and red, the stop-start section cuts on the music's own cuts.
                case "central-station-boss/chime":return new NativeZoneBossMood(.62f,.25f,-.1f,1.05f,1,.6f,.5f);
                case "central-station-boss/arrival":return new NativeZoneBossMood(.66f,.35f,0,1.1f,1,.6f,.5f);
                case "central-station-boss/platform":return new NativeZoneBossMood(.82f,.5f,-.05f,1.05f,1,.7f,.6f);
                case "central-station-boss/doors":return new NativeZoneBossMood(.58f,.3f,-.2f,1.15f,1,.6f,.6f);
                case "central-station-boss/express":return new NativeZoneBossMood(1.02f,.8f,.1f,1,1,1,.8f);
                case "central-station-boss/signal":return new NativeZoneBossMood(.74f,.55f,-.45f,1.2f,1,.8f,.8f);
                case "central-station-boss/rush":return new NativeZoneBossMood(1.08f,1,.3f,1,1,1.2f,1);
                case "central-station-boss/stopstart":return new NativeZoneBossMood(.86f,.7f,.2f,1.1f,1,1,1);
                case "central-station-boss/lastcall":return new NativeZoneBossMood(1.14f,1,.55f,1,1,1.3f,1);
                case "central-station-boss/terminus":return new NativeZoneBossMood(.7f,.3f,0,1.2f,1,.7f,.7f);
                // Hydraulic Hell: the furnaces stoke up through the ignition, roar through the forge and the press, the quench and the
                // blowdowns breathe out in steam, the meltdown and the slam run white-hot with sparks.
                case "city-ironworks-boss/ignition":return new NativeZoneBossMood(.74f,.3f,.1f,1.05f,.55f,.4f,.6f);
                case "city-ironworks-boss/stoke":return new NativeZoneBossMood(.86f,.5f,.15f,1.05f,.8f,.7f,.8f);
                case "city-ironworks-boss/forge":return new NativeZoneBossMood(1,.8f,.2f,1,1.05f,1,1);
                case "city-ironworks-boss/quench":return new NativeZoneBossMood(.84f,.5f,-.15f,1.15f,.8f,.6f,1.5f);
                case "city-ironworks-boss/press":return new NativeZoneBossMood(1.06f,1,.3f,1,1.15f,1.2f,1.1f);
                case "city-ironworks-boss/venting":return new NativeZoneBossMood(.76f,.35f,-.1f,1.2f,.9f,.6f,1.8f);
                case "city-ironworks-boss/meltdown":return new NativeZoneBossMood(1.12f,1,.45f,1,1.3f,1.5f,1.1f);
                case "city-ironworks-boss/blowdown":return new NativeZoneBossMood(.66f,.25f,-.2f,1.25f,.9f,.5f,2);
                case "city-ironworks-boss/slam":return new NativeZoneBossMood(1.15f,1,.5f,1,1.35f,1.6f,1.2f);
                case "city-ironworks-boss/cooling":return new NativeZoneBossMood(.72f,.25f,0,1.15f,.7f,.4f,1);
                default:return NativeZoneBossMood.Rest;
            }
        }
    }
}
