using System;

namespace ChipArena.UnityConversion
{
    /// <summary>A piece's phase at one moment: the state, how long it has run and has left (s), a 0..1 level (an arming
    /// build, 1 while active, a cooldown's fade), the cycle (-1 before the first) and the moment it last fired (NaN never).</summary>
    public struct NativeKitPhase
    {
        public NativeKitState State;public double Since,Until,FiredAt;public float Level;public int Cycle;
        public bool Live=>State==NativeKitState.Arming||State==NativeKitState.Active||State==NativeKitState.Cooldown;
        public static NativeKitPhase Idle=>new NativeKitPhase{State=NativeKitState.Idle,Since=0,Until=double.PositiveInfinity,FiredAt=double.NaN,Cycle=-1};
    }

    /// <summary>
    /// The music's beat as a kit piece hears it: the playing track's position (s, already looped) and its beat map (the boss
    /// song's shared map, NativeBossTrack, the one the Siren intro and the song vents read). Off when no beat-mapped track
    /// plays: beat-locked pieces then free-run. <see cref="Grid"/> also takes a plain tempo grid (bpm and first beat).
    /// </summary>
    public struct NativeKitBeat
    {
        public bool On;public double Song;public double[] Beats;public int FirstDownbeat;public double Bpm,Offset;
        public static NativeKitBeat Off=>new NativeKitBeat();
        public static NativeKitBeat Of(NativeBossTrack track,double song)=>track?.beats==null||track.beats.Length<8||!(song>=0)?Off:new NativeKitBeat{On=true,Song=track.Looped(song),Beats=track.beats,FirstDownbeat=track.firstDownbeat};
        public static NativeKitBeat Grid(double bpm,double offset,double song)=>!(bpm>30)||!(song>=0)?Off:new NativeKitBeat{On=true,Song=song,Bpm=bpm,Offset=offset};
        /// <summary>The beat at or before the song position, its time (s) and the beat's length (s); false when off.</summary>
        public bool At(out int index,out double time,out double period)
        {
            index=-1;time=0;period=.5;if(!On)return false;
            if(Beats==null){period=60/Bpm;double k=Math.Floor((Song-Offset)/period);index=(int)k;time=Offset+k*period;return true;}
            int lo=0,hi=Beats.Length-1,found=-1;while(lo<=hi){int mid=(lo+hi)/2;if(Beats[mid]<=Song){found=mid;lo=mid+1;}else hi=mid-1;}
            if(found<0){index=-1;time=Beats[0]-(Beats[1]-Beats[0]);period=Beats[1]-Beats[0];return true;}
            index=found;time=Beats[found];period=found+1<Beats.Length?Beats[found+1]-Beats[found]:Beats[found]-Beats[found-1];return true;
        }
        /// <summary>The beat's index within its bar (0 the downbeat).</summary>
        public int InBar(int index)=>Beats==null?((index%4)+4)%4:(((index-FirstDownbeat)%4)+4)%4;
    }

    /// <summary>
    /// The hazard kit's clocks and looks over time (docs/HAZARD-KIT.md "States" and "Alarms"): a piece's phase from its
    /// trigger, the alarm lamps' strobe, the beacon's turn, the klaxon's blasts, the sign's light, the music's trip
    /// reaction, the photosensitivity rules, the turret's preview timeline and the saw's pose. Every function is pure (the
    /// same record, clock, fire time, beat and mode give the same answer) and allocation free; host, guests and the killcam
    /// agree because the inputs are replicated (the match clock, hazard clocks, breach and DEV pulse times).
    /// </summary>
    public static class NativeHazardKitClock
    {
        /// <summary>Photosensitivity (WCAG 2.3.1 general flash threshold): no mode flashes more than 3 times a second;
        /// Reduced at most 2, with soft edges and less contrast; Calm never flashes.</summary>
        public const double MaxFlashHz=3,ReducedFlashHz=2,FreeFlashHz=2.5,CalmBreathHz=.3;
        private static double Smooth(double t){t=t<0?0:t>1?1:t;return t*t*(3-2*t);}
        private static double Frac(double v)=>v-Math.Floor(v);

        /// <summary>The phase of a piece fired at `fired` (NaN: never) or on its schedule, and following stage hazard
        /// `follow` when it has one (the stronger of the two wins: active over arming over cooldown over idle).</summary>
        public static NativeKitPhase Phase(KitRecord r,double now,double fired,bool hasFollow=false,NativeHazardState follow=default)
        {
            var p=NativeKitPhase.Idle;if(r==null)return p;string mode=NativeHazardKit.Mode(r);double w=NativeHazardKit.Warn(r),a=NativeHazardKit.Live(r),c=NativeHazardKit.Cool(r);
            if(mode=="always"){p.State=NativeKitState.Active;p.Since=now;p.Until=double.PositiveInfinity;p.Level=1;p.Cycle=0;p.FiredAt=0;}
            else if(mode=="schedule")
            {
                var t=r.trigger;double u=now-t.first-t.offset;
                if(u>=0&&t.period>0){double k=Math.Floor(u/t.period);p=Window(u-k*t.period,w,a,c,now-(u-k*t.period));p.Cycle=(int)Math.Min(k,int.MaxValue);if(p.State==NativeKitState.Idle)p.Until=t.period-(u-k*t.period);}
                else if(u<0)p.Until=-u;
            }
            else if(!double.IsNaN(fired)&&now>=fired){p=Window(now-fired,w,a,c,fired);p.Cycle=0;}
            if(hasFollow)
            {
                var f=FollowPhase(follow);if(Rank(f.State)>Rank(p.State))p=f;
            }
            return p;
        }
        private static NativeKitPhase Window(double u,double w,double a,double c,double fired)
        {
            var p=NativeKitPhase.Idle;p.FiredAt=fired;
            if(u<w){p.State=NativeKitState.Arming;p.Since=u;p.Until=w-u;p.Level=(float)Smooth(u/Math.Max(.001,w));}
            else if(u<w+a){p.State=NativeKitState.Active;p.Since=u-w;p.Until=w+a-u;p.Level=1;}
            else if(u<w+a+c){p.State=NativeKitState.Cooldown;p.Since=u-w-a;p.Until=w+a+c-u;p.Level=(float)(1-Smooth((u-w-a)/Math.Max(.001,c)));}
            else{p.Since=u-w-a-c;p.Until=double.PositiveInfinity;}
            return p;
        }
        /// <summary>A stage hazard's phase read as a kit phase (Warn = arming, Danger and Active = active, Clear = cooldown).</summary>
        public static NativeKitPhase FollowPhase(in NativeHazardState h)
        {
            var p=NativeKitPhase.Idle;p.Since=h.Since;p.Until=h.Until;p.Cycle=h.Cycle;
            switch(h.Phase)
            {
                case NativeHazardPhase.Warn:p.State=NativeKitState.Arming;p.Level=h.Level;break;
                case NativeHazardPhase.Danger:case NativeHazardPhase.Active:p.State=NativeKitState.Active;p.Level=1;break;
                case NativeHazardPhase.Clear:p.State=NativeKitState.Cooldown;p.Level=h.Level;break;
            }
            return p;
        }
        private static int Rank(NativeKitState s)=>s==NativeKitState.Active?3:s==NativeKitState.Arming?2:s==NativeKitState.Cooldown?1:0;

        // ---------------- flashes (every lamp in the kit goes through these) ----------------
        /// <summary>The flash rate (Hz) a lamp may use in `mode`, given the beat's rate (0: free): beat-locked lamps flash on
        /// every k-th beat with the smallest k that keeps under the mode's cap.</summary>
        public static double FlashHz(NativeKitFlash mode,double beatHz,out int everyBeats)
        {
            double cap=mode==NativeKitFlash.Reduced?ReducedFlashHz:MaxFlashHz;everyBeats=0;
            if(mode==NativeKitFlash.Calm)return 0;
            if(beatHz>0){everyBeats=Math.Max(1,(int)Math.Ceiling(beatHz/cap-1e-9));return beatHz/everyBeats;}
            return mode==NativeKitFlash.Reduced?1.25:FreeFlashHz;
        }
        /// <summary>
        /// A strobe lamp's light (0 off .. about 1.6 peak) at `time` in its phase. Full: a crisp 60 ms flash with a short
        /// tail, 2.5 a second (1.25 while arming), or on the beat; Reduced: a soft cosine pulse at most 2 a second between .25
        /// and .8; Calm: a steady glow breathing at .3 Hz. Idle and broken lamps are dark, a cooldown fades.
        /// </summary>
        public static float Strobe(in NativeKitPhase p,double time,NativeKitFlash mode,in NativeKitBeat beat,bool locked)
        {
            if(p.State==NativeKitState.Idle||p.State==NativeKitState.Disabled||p.State==NativeKitState.Broken)return 0;
            float gate=p.State==NativeKitState.Cooldown?p.Level:p.State==NativeKitState.Arming?.6f+.4f*p.Level:1;
            if(mode==NativeKitFlash.Calm)return gate*(float)(.42+.12*Math.Sin(2*Math.PI*CalmBreathHz*time));
            double since,period;
            if(locked&&beat.At(out int index,out double bt,out double bp))
            {
                FlashHz(mode,1/bp,out int k);int step=p.State==NativeKitState.Arming?k*2:k;
                int from=index-((index%step)+step)%step;since=beat.Song-(bt-(index-from)*bp);period=bp*step;
            }
            else
            {
                double hz=FlashHz(mode,0,out _);if(p.State==NativeKitState.Arming)hz/=2;period=1/hz;since=Frac(time*hz)*period;
            }
            if(mode==NativeKitFlash.Reduced){double u=since/period;return gate*(float)(.25+.55*(.5+.5*Math.Cos(2*Math.PI*u)));}
            double flash=since<.06?1:Math.Exp(-(since-.06)/.05);return gate*(float)(.05+1.55*flash);
        }
        /// <summary>A beacon's reflector angle (radians about its axis) at `time`: it spins up through the arming, turns at
        /// full speed while active (Full 1.1 turns a second, Reduced .6, Calm .3), coasts down through the cooldown; beat-
        /// locked, one turn every two beats (Full) or every bar, the beam forward on the beat.</summary>
        public static double BeaconAngle(in NativeKitPhase p,double time,NativeKitFlash mode,in NativeKitBeat beat,bool locked,double seed=0)
        {
            double rate=mode==NativeKitFlash.Full?1.1:mode==NativeKitFlash.Reduced?.6:.3;
            if(p.State==NativeKitState.Idle||p.State==NativeKitState.Disabled||p.State==NativeKitState.Broken)return seed*Math.PI*2;
            if(locked&&p.State==NativeKitState.Active&&beat.At(out int index,out double bt,out double bp))
            {
                int per=mode==NativeKitFlash.Full?2:4;double beats=index+(beat.Song-bt)/bp;return (beats/per)*Math.PI*2;
            }
            // Spin-up and coast: integrated speed (quadratic ramps), continuous at the phase changes.
            double w=Math.Max(.01,p.State==NativeKitState.Arming?p.Since+p.Until:1);
            if(p.State==NativeKitState.Arming)return seed*Math.PI*2+rate*Math.PI*2*p.Since*p.Since/(2*w);
            if(p.State==NativeKitState.Active)return seed*Math.PI*2+rate*Math.PI*2*(p.Since+.3);
            double c=Math.Max(.01,p.Since+p.Until);return seed*Math.PI*2+rate*Math.PI*2*(p.Since-p.Since*p.Since/(2*c)+7);
        }
        /// <summary>A beacon's light: dim while arming, full while active, fading through the cooldown (Calm: 60%).</summary>
        public static float BeaconLight(in NativeKitPhase p,NativeKitFlash mode)=>(p.State==NativeKitState.Active?1:p.State==NativeKitState.Arming?.4f+.5f*p.Level:p.State==NativeKitState.Cooldown?p.Level:0)*(mode==NativeKitFlash.Calm?.6f:1);
        /// <summary>A lit sign's level: unlit .08 idle; while arming it blinks once a second (steady in Reduced and Calm); lit
        /// with a faint shimmer while active; fading through the cooldown.</summary>
        public static float SignLight(in NativeKitPhase p,double time,NativeKitFlash mode)
        {
            switch(p.State)
            {
                case NativeKitState.Arming:return mode==NativeKitFlash.Full?(Frac(time)<.55?1.1f:.15f):.4f+.6f*p.Level;
                case NativeKitState.Active:return 1.15f+.06f*(float)Math.Sin(time*7.3);
                case NativeKitState.Cooldown:return .08f+1.07f*p.Level;
                case NativeKitState.Broken:return 0;
                default:return .08f;
            }
        }

        // ---------------- the klaxon's blasts ----------------
        /// <summary>The klaxon's blast period (s): its record's rate (default 1.6 s), or beat-locked every `beatsPer` beats.</summary>
        public static double BlastPeriod(KitRecord r,in NativeKitBeat beat,bool locked,int beatsPer,out bool onBeat)
        {
            onBeat=false;if(locked&&beat.At(out _,out _,out double bp)){onBeat=true;return bp*Math.Max(1,beatsPer);}
            return r!=null&&r.rate>0?r.rate:1.6;
        }
        /// <summary>
        /// The blasts (their times on the piece's own clock `now`) in (from, to] while active: free-running from the active
        /// start, or beat-locked on every `beatsPer`-th beat of the song (`songNow` the song position at `now`). Writes at most
        /// into.Length times; returns the count. Pure: a caller keeping its last `to` hears each blast once.
        /// </summary>
        public static int Blasts(KitRecord r,in NativeKitPhase p,double now,double from,double to,in NativeKitBeat beat,bool locked,int beatsPer,double[] into)
        {
            if(p.State!=NativeKitState.Active||into==null||!(to>from))return 0;int n=0;double start=now-p.Since,end=now+p.Until;
            double a=Math.Max(from,start-1e-9),b=Math.Min(to,end);if(!(b>a))return 0;
            if(locked&&beat.At(out int index,out double bt,out double bp))
            {
                int per=Math.Max(1,beatsPer);double offsetSong=beat.Song-now;// song time = clock + offset
                // Beats in (a, b] on the clock: beat k at song time bt+(k-index)*bp (constant tempo over a frame is enough).
                double k0=Math.Floor((a+offsetSong-bt)/bp)+1,k1=Math.Floor((b+offsetSong-bt)/bp);
                for(double k=k0;k<=k1&&n<into.Length;k++){long beatIndex=index+(long)k;if(((beatIndex-beat.FirstDownbeat)%per+per)%per!=0)continue;into[n++]=bt+k*bp-offsetSong;}
                return n;
            }
            double period=r!=null&&r.rate>0?r.rate:1.6;double first=Math.Ceiling((a-start)/period-1e-9);if(first<0)first=0;
            for(double k=first;n<into.Length;k++){double t=start+k*period;if(t>b)break;if(t>a)into[n++]=t;}
            return n;
        }
        /// <summary>Which klaxon variant fits the beat (docs/HAZARD-KIT.md "Music"): the longest clip that rings within
        /// `beatsPer` beats, sped up at most 6% to fit; -1 (free-run, natural rate) when none does.</summary>
        public static int FitVariant(float[] lengths,double beatPeriod,int beatsPer,out float rate)
        {
            rate=1;if(lengths==null||!(beatPeriod>0))return -1;double room=beatPeriod*beatsPer;int best=-1;double bestLen=0;
            for(int i=0;i<lengths.Length;i++){double l=lengths[i];if(!(l>0))continue;double fit=l<=room?l:l/1.06<=room?room:-1;if(fit>bestLen+1e-6){bestLen=fit;best=i;}}
            if(best>=0&&lengths[best]>room)rate=(float)(lengths[best]/room);return best;
        }

        // ---------------- the music's reaction to a trip ----------------
        /// <summary>
        /// The music's gain `age` s after an alarm trips (1 before and after): a quick dip (to .55 in 60 ms, held to .35 s),
        /// then a swell back over the second to 1.1 and settling at 1 by 2.4 s. Reduced and Calm dip to .8 and swell to 1.03.
        /// Multiplies the music's own volume (Settings · Music and the mute stay in charge).
        /// </summary>
        public static float MusicReact(double age,NativeKitFlash mode)
        {
            if(!(age>=0)||age>=2.4)return 1;bool soft=mode!=NativeKitFlash.Full;double dip=soft?.8:.55,peak=soft?1.03:1.1;
            if(age<.06)return (float)(1-(1-dip)*age/.06);if(age<.35)return (float)dip;
            if(age<1.1)return (float)(dip+(peak-dip)*Smooth((age-.35)/.75));return (float)(peak+(1-peak)*Smooth((age-1.1)/1.3));
        }
        /// <summary>The strongest of several trip reactions (the deepest dip wins; swells do not stack).</summary>
        public static float MusicReact(double[] trips,int count,double now,NativeKitFlash mode)
        {
            float g=1;bool any=false;for(int i=0;i<count;i++){float k=MusicReact(now-trips[i],mode);if(k==1)continue;if(!any||k<g)g=k;any=true;}
            if(!any)return 1;return g;
        }
    }

    /// <summary>The turret's behaviour state (docs/HAZARD-KIT.md "Turret contract"): offline, scanning, acquiring a target
    /// (tracking, beeping), locked (the fair warning: laser red, barrels spinning up), firing a burst, cooling, overheated
    /// (venting, cannot fire), disabled (hacked or switched off) and destroyed.</summary>
    public enum NativeTurretState : byte {Offline,Scan,Acquire,Lock,Fire,Cool,Overheat,Disabled,Destroyed}
    /// <summary>
    /// What a turret's authority replicates (the proposed contract; Astra's), and what the presentation draws from: state and
    /// the time it began, aim (yaw/pitch degrees in the turret's frame, the yaw about its mount's up), the target seat (-1
    /// none), the last round's time and the burst's round count, the barrel heat (0..1, overheated at 1) and health (0..1;
    /// under .5 it sparks and smokes), and the last round's hit point (NaN x: no hit, a miss into the distance).
    /// </summary>
    public struct NativeTurretView
    {
        public NativeTurretState State;public double StateAt,LastShot;public float Yaw,Pitch,Heat,Health;public int Target,Shots;public V3 Hit;
        public static NativeTurretView Offline=>new NativeTurretView{State=NativeTurretState.Offline,Health=1,Target=-1,LastShot=double.NegativeInfinity,Hit=new V3(float.NaN,0,0)};
    }
    /// <summary>
    /// A turret's DEV preview and render timeline (presentation only: no authority, no damage): a 16 s loop through every
    /// state at a scripted target ahead of it. 0-3 scanning, 3-3.8 acquiring, 3.8-4.6 locked (beeps, spin-up), 4.6-6.2
    /// firing (10 rounds a second), 6.2-7 cooling, 7-9 firing again until the heat reaches 1, 9-11 overheated, 11-12.5
    /// scanning damaged (health .35), 12.5-13.5 disabled, 13.5-15.4 destroyed, 15.4-16 offline (the reboot).
    /// </summary>
    public static class NativeTurretPreview
    {
        public const double Loop=16,LockTime=.8,Rate=10;
        public static NativeTurretView At(KitRecord r,double time)
        {
            double t=time-Math.Floor(time/Loop)*Loop,loopStart=time-t;float arc=r!=null&&r.arc>0?r.arc:110;var v=NativeTurretView.Offline;v.Health=1;v.Target=-1;
            double sweep=Math.Sin(t*1.1)*arc*.5;float aimYaw=14,aimPitch=-6;
            void S(NativeTurretState s,double at){v.State=s;v.StateAt=loopStart+at;}
            if(t<3){S(NativeTurretState.Scan,0);v.Yaw=(float)sweep;v.Pitch=-8;}
            else if(t<3.8){S(NativeTurretState.Acquire,3);double u=Smooth01((t-3)/.45);v.Yaw=(float)(Math.Sin(3*1.1)*arc*.5*(1-u)+aimYaw*u);v.Pitch=(float)(-8+(aimPitch+8)*u);v.Target=1;}
            else if(t<4.6){S(NativeTurretState.Lock,3.8);v.Yaw=aimYaw;v.Pitch=aimPitch;v.Target=1;}
            else if(t<6.2){S(NativeTurretState.Fire,4.6);Burst(ref v,loopStart+4.6,time,loopStart+6.2);v.Heat=(float)((t-4.6)/1.6*.45);}
            else if(t<7){S(NativeTurretState.Cool,6.2);v.Yaw=aimYaw;v.Pitch=aimPitch;v.Target=1;v.Heat=(float)(.45-.1*(t-6.2)/.8);v.LastShot=loopStart+6.2-1/Rate;}
            else if(t<9){S(NativeTurretState.Fire,7);Burst(ref v,loopStart+7,time,loopStart+9);v.Heat=(float)Math.Min(1,.35+.65*(t-7)/2);}
            else if(t<11){S(NativeTurretState.Overheat,9);v.Yaw=aimYaw;v.Pitch=aimPitch-10*(float)Smooth01((t-9)/.4);v.Heat=(float)(1-.7*(t-9)/2);v.LastShot=loopStart+9-1/Rate;}
            else if(t<12.5){S(NativeTurretState.Scan,11);v.Yaw=(float)(Math.Sin((t-11)*1.6)*arc*.4);v.Pitch=-8;v.Health=.35f;v.Heat=.2f;}
            else if(t<13.5){S(NativeTurretState.Disabled,12.5);v.Yaw=(float)(Math.Sin(1.5*1.6)*arc*.4);v.Pitch=-30*(float)Smooth01((t-12.5)/.5);v.Health=.35f;}
            else if(t<15.4){S(NativeTurretState.Destroyed,13.5);v.Yaw=(float)(Math.Sin(1.5*1.6)*arc*.4)+18;v.Pitch=-38;v.Health=0;}
            else{S(NativeTurretState.Offline,15.4);v.Pitch=-30*(float)(1-Smooth01((t-15.4)/.6));}
            // A ceiling turret looks further down at the same floor.
            if(r!=null&&r.mount=="ceiling")v.Pitch-=22;
            return v;
        }
        private static void Burst(ref NativeTurretView v,double start,double time,double end)
        {
            v.Yaw=14+(float)Math.Sin(time*9)*.6f;v.Pitch=-6+(float)Math.Sin(time*13)*.4f;v.Target=1;
            double k=Math.Floor((Math.Min(time,end)-start)*Rate);v.Shots=(int)k+1;v.LastShot=start+k/Rate;v.Hit=new V3(float.NaN,0,0);
        }
        private static double Smooth01(double t){t=t<0?0:t>1?1:t;return t*t*(3-2*t);}
        /// <summary>The barrels' spin rate (rad/s) in a state `since` s in: spin-up through the lock (to full at its end),
        /// full while firing, coasting down while cooling or overheated, still otherwise.</summary>
        public static double SpinRate(NativeTurretState s,double since)
        {
            const double Full=34;
            switch(s)
            {
                case NativeTurretState.Lock:return Full*Math.Min(1,since/LockTime);
                case NativeTurretState.Fire:return Full;
                case NativeTurretState.Cool:case NativeTurretState.Overheat:return Full*Math.Max(0,1-since/1.2);
                default:return 0;
            }
        }
        /// <summary>The lock tell's beeps in (from, to] on the turret's clock: accelerating from .22 s apart to .06 s across
        /// the lock window starting at `lockAt` (the last one on the lock's end, as the barrels reach speed).</summary>
        public static int LockBeeps(double lockAt,double from,double to,double[] into)
        {
            int n=0;if(into==null)return 0;double at=lockAt,gap=.22;
            for(int i=0;i<16&&at<=lockAt+LockTime+1e-6;i++){if(at>from&&at<=to&&n<into.Length)into[n++]=at;at+=gap;gap=Math.Max(.06,gap*.72);}
            return n;
        }
    }

    /// <summary>A kit saw's pose: the blade's hub and axle (world, unit), its angle and spin rate, how far it stands out of
    /// its slot or housing (0 hidden .. 1 fully out), whether its edge is live (the authority's to decide; the presentation
    /// draws the warning from it), and whether it grinds a surface (sparks and the scrape) and where.</summary>
    public struct NativeKitSawPose
    {
        public V3 Hub,Axle,Along,Contact,Normal;public double Spin,Rate,Exposure,Swing;public bool Live,Grinding;
    }
    public static class NativeKitSaw
    {
        public const double SpinRate=46;
        /// <summary>
        /// The saw's pose in its phase (docs/HAZARD-KIT.md "Saws"): every variant spins up through the arming (hidden in its
        /// slot or housing), comes out and runs while active and goes back in and spins down through the cooldown.
        /// floor-slot and wall: out of the slot, then back and forth along a→b; ceiling-drop: drops from a to b on its arm;
        /// rail: rides the rail a→b and back; pendulum: swings `arc` degrees about the pivot (pos) on its `length` arm.
        /// `frame` and `at` are the record's world frame; a/b are in it.
        /// </summary>
        public static NativeKitSawPose Pose(KitRecord r,in NativeKitPhase p,double now,V3 at,FactoryQ frame)
        {
            var s=new NativeKitSawPose();double w=p.State==NativeKitState.Arming?p.Since+p.Until:1,c=p.State==NativeKitState.Cooldown?p.Since+p.Until:1;
            // Spin: up through the arming, full while active, down through the cooldown.
            double rate=p.State==NativeKitState.Arming?SpinRate*Smooth(p.Since/Math.Max(.01,w)):p.State==NativeKitState.Active?SpinRate:p.State==NativeKitState.Cooldown?SpinRate*(1-Smooth(p.Since/Math.Max(.01,c))):0;
            s.Rate=rate;s.Spin=(p.State==NativeKitState.Idle?0:SpinRate*(now-(double.IsNaN(p.FiredAt)?0:p.FiredAt)))%(Math.PI*2);
            double expose=p.State==NativeKitState.Active?Smooth(Math.Min(1,p.Since/.35)):p.State==NativeKitState.Cooldown?1-Smooth(Math.Min(1,p.Since/.45)):0;s.Exposure=expose;
            V3 A=NativeHazardKit.Point(r.a,at,frame),B=NativeHazardKit.Point(r.b,at,frame);V3 up=Dir(frame,0,1,0),side=Dir(frame,1,0,0),face=Dir(frame,0,0,1);
            double radius=r.radius>0?r.radius:.55,speed=r.speed>0?r.speed:2.2;double len=(B-A).Length;
            double travel=p.State==NativeKitState.Active&&len>.01?PingPong(Math.Max(0,p.Since-.35)*speed/len):0;
            switch(r.variant)
            {
                case NativeHazardKit.SawFloor:
                {
                    var c0=Lerp(A,B,travel);s.Hub=c0+up*(float)(-radius+(radius+.08)*expose*.62);s.Axle=face;s.Along=(B-A).Normalized;s.Normal=up;
                    s.Grinding=expose>.2&&rate>5;s.Contact=c0+up*.01f;break;
                }
                case NativeHazardKit.SawWall:
                {
                    var c0=Lerp(A,B,travel);s.Hub=c0+face*(float)(-radius+(radius+.06)*expose*.62);s.Axle=up;s.Along=(B-A).Normalized;s.Normal=face;
                    s.Grinding=expose>.2&&rate>5;s.Contact=c0+face*.01f;break;
                }
                case NativeHazardKit.SawDrop:
                {
                    s.Hub=Lerp(A,B,expose);s.Axle=side;s.Along=up;s.Normal=up;s.Grinding=false;break;
                }
                case NativeHazardKit.SawRail:
                {
                    s.Hub=Lerp(A,B,travel)+up*(float)-(radius+.35);s.Axle=face;s.Along=(B-A).Normalized;s.Normal=up;s.Grinding=p.State==NativeKitState.Active&&Math.Abs(travel-.5)>.47;s.Contact=Lerp(A,B,travel);break;
                }
                default:
                {
                    // Pendulum: the swing grows through the arming, swings while active, dies away through the cooldown.
                    double armLen=r.length>0?r.length:2.1,amp=(r.arc>0?r.arc:55)*Math.PI/360,period=2*Math.PI*Math.Sqrt(armLen/9.81);
                    double k=p.State==NativeKitState.Arming?.25*Smooth(p.Since/Math.Max(.01,w)):p.State==NativeKitState.Active?.25+.75*Smooth(Math.Min(1,p.Since/1.2)):p.State==NativeKitState.Cooldown?1-Smooth(p.Since/Math.Max(.01,c)):0;
                    double swing=amp*k*Math.Sin(2*Math.PI*(now-(double.IsNaN(p.FiredAt)?0:p.FiredAt))/period);s.Swing=swing;
                    s.Hub=at+(up*(float)-Math.Cos(swing)+side*(float)Math.Sin(swing))*(float)armLen;s.Axle=face;s.Along=side;s.Normal=up;s.Exposure=p.State==NativeKitState.Idle?0:1;break;
                }
            }
            s.Live=p.State==NativeKitState.Active&&s.Exposure>.3;return s;
        }
        private static double PingPong(double u){double f=u-2*Math.Floor(u/2);return f<1?f:2-f;}
        private static double Smooth(double t){t=t<0?0:t>1?1:t;return t*t*(3-2*t);}
        private static V3 Lerp(V3 a,V3 b,double t)=>new V3((float)(a.x+(b.x-a.x)*t),(float)(a.y+(b.y-a.y)*t),(float)(a.z+(b.z-a.z)*t));
        public static V3 Dir(FactoryQ q,double x,double y,double z){q.Rotate(x,y,z,out double ox,out double oy,out double oz);return new V3((float)ox,(float)oy,(float)oz);}
        /// <summary>The blade's blood (0 clean .. 1 soaked) from the hit times it has taken (presentation only, from the
        /// authority's hit events): each hit adds .55, fading to a stain (.2 per hit, at most .6) over 40 s.</summary>
        public static float Blood(double[] hits,int count,double now)
        {
            double b=0,stain=0;for(int i=0;i<count;i++){double age=now-hits[i];if(!(age>=0))continue;b+=.55*Math.Exp(-age/14);stain+=.2;}
            return (float)Math.Min(1,Math.Max(b,Math.Min(.6,stain)));
        }
    }
}
