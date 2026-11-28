using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Pier 9 (map `harbor`, docs/arenas/HARBOR.md; rebuilt 2026-11-11 as a seaside amusement pier at night). Shared here: which
    /// map and look this is, the kit kinds its static detail and rigs draw with (Core NativePierKit and NativePierRig; resolved per
    /// look from MapLooks.json `kitLooks`, "pier-kit-*" ids, an unlisted kind taking <see cref="Defaults"/>), the places the
    /// backdrop, the rigs, the effects and the modelled art agree on (the sea, the Ferris wheel, the carousel, the band stand, the
    /// coaster's track) and the set pieces' and hazards' poses as pure functions of the clocks and the replicated state: the wheel
    /// and the carousel turn on the render clock; the coaster's train (kind `maglev`) and the drop towers' gondolas (kind `press`)
    /// stand where the simulation's states put them. Presentation only: none of this is sim data, collides, damages or enters the
    /// network frame. Pure and allocation free.
    /// </summary>
    public static class NativePierLooks
    {
        public const string MapId="harbor",Look="harbor-pier-lights";
        /// <summary>Whether a map id is Pier 9: every hook here is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==MapId;

        /// <summary>Kit kinds (index = kind), resolved once per look through `kitLooks`.</summary>
        public static readonly string[] Kinds={"pier-kit-timber","pier-kit-timber-dark","pier-kit-cream","pier-kit-red","pier-kit-teal","pier-kit-gold","pier-kit-iron","pier-kit-brass",
            "pier-kit-glass","pier-kit-bulb","pier-kit-bulb-pink","pier-kit-bulb-cyan","pier-kit-canvas","pier-kit-mirror","pier-kit-navy","pier-kit-dark","pier-kit-rail","pier-kit-bulb-red",
            "pier-kit-bulb-dim","pier-kit-hull","pier-kit-lilac","pier-kit-white"};
        public const int Timber=0,TimberDark=1,Cream=2,Red=3,Teal=4,Gold=5,Iron=6,Brass=7,Glass=8,Bulb=9,BulbPink=10,BulbCyan=11,Canvas=12,Mirror=13,Navy=14,Dark=15,Rail=16,BulbRed=17,BulbDim=18,
            Hull=19,Lilac=20,White=21;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it.</summary>
        public static readonly (string look,int color)[] Defaults={("timber",0x8A6242),("timber",0x4E3828),("plain",0xF2E6CC),("plain",0xC8322E),("plain",0x1F8E96),("plain",0xE0B040),
            ("steel-green",0x2A2E36),("steel-green",0xB08A40),("window-lit",0xFFD890),("neon",0xFFC870),("neon",0xFF5FA8),("neon",0x5FE8FF),("plain",0xE8E0D0),("window-lit",0xBFD8E8),
            ("plain",0x1C2A4A),("plain",0x121418),("steel-green",0x9AA2AA),("neon",0xFF4030),("plain",0x4A3A2A),("plain",0x7A2E2A),("plain",0x9A7AD0),("plain",0xF8F4EC)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "pier-kit-*" id wins; else the default).</summary>
        public static NativeFactoryKit.Looks Resolve(MapLookBlock[] kitLooks)
        {
            var l=new NativeFactoryKit.Looks();
            for(int k=0;k<Kinds.Length;k++)
            {
                var c=NativeBlockLooks.Resolve(kitLooks,null,Kinds[k],"",0xFFFFFF);
                if(!c.Listed){var d=Defaults[k];var e=new MapLookBlock{block=Kinds[k],look=d.look,color=d.color};c=NativeBlockLooks.Resolve(new[]{e},null,Kinds[k],"",0xFFFFFF);}
                l.Look[k]=c.Look;l.Color[k]=c.Color;l.Flags[k]=c.Flags&~(NativeBlockLooks.Band|NativeBlockLooks.Tag);
            }
            return l;
        }
        /// <summary>A stable 0..1 hash of three integers.</summary>
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+9001,b+53,c+211);
        public static double Frac(double v)=>v-Math.Floor(v);

        // ---------------- the places the backdrop, rigs, effects and art share ----------------
        /// <summary>The sea's mean level (a fall dies at killY -5.5, just under it) and the boardwalk's underside.</summary>
        public const double Sea=-4.6,Under=-.4;
        /// <summary>The Ferris wheel off the west side: its hub, its radius, its turn (rad/s; a turn in about 80 s), its gondolas
        /// and the half gap between its two rims. It turns in the y-z plane (its axle along x); the coaster's track passes under it.</summary>
        public const double WheelX=-34,WheelY=27.5,WheelZ=0,WheelR=21,WheelTurn=.078,WheelGap=1.3;public const int Gondolas=16;
        /// <summary>The wheel's angle at render time t (rad, about -x: a rim point at angle a stands at y = R sin a, z = R cos a).</summary>
        public static double WheelAngle(double t)=>(t*WheelTurn)%(2*Math.PI);
        /// <summary>Gondola i's pivot on the rim and its sway (rad about x) at render time t.</summary>
        public static void Gondola(int i,double t,out double x,out double y,out double z,out double sway)
        {double a=WheelAngle(t)+i*2*Math.PI/Gondolas;x=WheelX;y=WheelY+WheelR*Math.Sin(a);z=WheelZ+WheelR*Math.Cos(a);sway=.05*Math.Sin(t*.9+i*1.3)+.03*Math.Cos(a);}
        /// <summary>The carousel (south pier, west) and the band stand (north pier, east): their centres (the rides' drums), the
        /// upper deck's top and the canopy's eave.</summary>
        public const double CarouselX=-10,CarouselZ=-15,BandX=10,BandZ=15,RideDeck=3.0,RideEave=5.9;
        /// <summary>The carousel's turn (rad/s), its mounts, the radii of its two rows and how far a mount rises and falls.</summary>
        public const double CarouselTurn=.42,MountLift=.34;public const int Mounts=10;
        public static double CarouselAngle(double t)=>(t*CarouselTurn)%(2*Math.PI);
        /// <summary>Mount i at render time t: its place (the saddle's middle), its heading (rad, from +z toward +x: it faces the
        /// way it travels) and its rise 0..1.</summary>
        public static void Mount(int i,double t,out double x,out double y,out double z,out double yaw,out double rise)
        {
            double a=CarouselAngle(t)+i*2*Math.PI/Mounts,r=i%2==0?3.3:2.35;rise=.5+.5*Math.Sin(t*1.9+i*Math.PI*.8);
            x=CarouselX+Math.Sin(a)*r;z=CarouselZ+Math.Cos(a)*r;y=RideDeck+1.02+MountLift*2*rise;yaw=a+Math.PI/2;
        }
        /// <summary>The band stand's dolls on its cornice: doll i's place, its heading and its bob at render time t (they keep the
        /// beat: two bobs a second, every other one a half beat behind).</summary>
        public const int Dolls=8;
        public static void Doll(int i,double t,out double x,out double y,out double z,out double yaw,out double tilt)
        {
            double a=i*2*Math.PI/Dolls+Math.PI/8,beat=t*2*Math.PI+(i%2)*Math.PI;x=BandX+Math.Sin(a)*3.55;z=BandZ+Math.Cos(a)*3.55;y=RideDeck+.62+.05*Math.Abs(Math.Sin(beat));yaw=a+.5*Math.Sin(t*1.05+i);tilt=.16*Math.Sin(beat);
        }
        /// <summary>The star over the band stand's dome: its centre's height and its turn at render time t.</summary>
        public const double StarY=9.2;
        public static double StarAngle(double t)=>(t*.6)%(2*Math.PI);

        // ---------------- the coaster (kind `maglev`) ----------------
        /// <summary>The track's line in the plane of the lane, by its coordinate s (m along it; within the lane s is x from the
        /// lane's centre): level across the pier and <see cref="TrackLevel"/> m past each edge (west under the Ferris wheel), then
        /// up an arc of radius <see cref="TrackR"/> into a straight spike at <see cref="TrackTilt"/> to <see cref="TrackTop"/> m of
        /// track past the edge. The rails' top stands <see cref="RailY"/> over the boards.</summary>
        public const double TrackLevel=22,TrackR=20,TrackTilt=50*Math.PI/180,TrackTop=77,RailY=.12;
        /// <summary>The point of the track at coordinate s for a lane of half length `half`: x from the lane's centre, y, and the
        /// pitch of the way toward +s (rad about z: positive climbs toward +x).</summary>
        public static void Track(double s,double half,out double x,out double y,out double pitch)
        {
            double sg=s<0?-1:1,d=Math.Abs(s)-half;if(d<=TrackLevel){x=s;y=RailY;pitch=0;return;}
            double arc=TrackR*TrackTilt,e=d-TrackLevel;
            if(e<=arc){double f=e/TrackR;x=sg*(half+TrackLevel+TrackR*Math.Sin(f));y=RailY+TrackR*(1-Math.Cos(f));pitch=sg*f;return;}
            e-=arc;x=sg*(half+TrackLevel+TrackR*Math.Sin(TrackTilt)+e*Math.Cos(TrackTilt));y=RailY+TrackR*(1-Math.Cos(TrackTilt))+e*Math.Sin(TrackTilt);pitch=sg*TrackTilt;
        }
        /// <summary>The train: <see cref="Cars"/> cars of <see cref="CarLength"/> m (the hazard's `length`), braking to a stand in
        /// <see cref="Brake"/> s up a spike and falling back out of it as fast.</summary>
        public const int Cars=5;public const double CarLength=2.6,Brake=4;
        /// <summary>The train's place: `Min`, the low end of the length of track it covers (its cars stand at Min + CarLength (j +
        /// .5)); `Side` -1 on the west spike, 1 on the east, 0 running the lane (then Min..Min + length is the simulation's kill
        /// box along x); `Speed` m/s along +s.</summary>
        public struct Train {public double Min,Speed;public int Side;}
        /// <summary>The train of maglev hazard `h` at match time `now`. In the pass it is the simulation's train (head and tail at
        /// `speed`); before it, it falls back down the spike it stood on, reaching the lane's edge at the pass's first instant at
        /// `speed`; after it, it runs up the far spike and stands until the next pass, which comes back the other way. Outside the
        /// fight it stands on the spike its first pass will come from.</summary>
        public static Train TrainAt(MapHazard h,NativePhase phase,int round,double fightAt,double now)
        {
            double half=h.size.x/2,len=h.length,v=h.speed,acc=v/Brake,rest=v*Brake/2;var t=new Train();
            int Dir(int k)=>((k+round)%2+2)%2==0?1:-1;
            double Out(double tau)=>tau>=Brake?rest:Math.Max(0,v*tau-acc*tau*tau/2);
            bool live=(phase==NativePhase.Fight||phase==NativePhase.Result)&&h.period>0&&v>0;double u=live?now-fightAt-h.first:double.NegativeInfinity;
            int side;double q;
            if(double.IsNaN(u)||u<0){double tau=double.IsNaN(u)?double.PositiveInfinity:-u;side=-Dir(0);q=Out(tau);t.Speed=tau<Brake?-side*(v-acc*tau):0;}
            else
            {
                int k=(int)Math.Floor(u/h.period);double c=u-k*h.period,dur=(h.size.x+len)/v;int d=Dir(k);
                if(c<dur){double lead=-d*half+d*v*c;t.Min=d>0?lead-len:lead;t.Side=0;t.Speed=d*v;return t;}
                double tp=c-dur,tn=h.period-c;side=d;q=Out(tp);t.Speed=tp<Brake?side*(v-acc*tp):0;if(tn<Brake){q=Math.Min(q,Out(tn));t.Speed=-side*(v-acc*tn);}
            }
            t.Side=side;t.Min=side>0?half+q:-(half+q+len);return t;
        }
        /// <summary>Car j of a train: its middle on the track (x from the lane's centre, y, pitch).</summary>
        public static void Car(in Train t,int j,double half,out double x,out double y,out double pitch)=>Track(t.Min+CarLength*(j+.5),half,out x,out y,out pitch);

        // ---------------- the drop towers (kind `press`) ----------------
        /// <summary>A gondola's moment: its underside's height (the simulation's ram), `Warn` 0..1 through its telegraph (1 in the
        /// fall), `Since` seconds since it last slammed the boards (∞ before the first), whether its kill volume is live, the
        /// phase (NativeFactoryHazards) and the cycle.</summary>
        public struct Drop {public double Y,Warn,Since;public bool Live;public int Phase,Cycle;}
        public static Drop DropAt(MapHazard h,int round,double clock)
        {
            var st=NativeFactoryHazards.State(h,round,clock);var d=new Drop{Y=st.Center.y-h.size.y/2,Since=double.PositiveInfinity,Live=st.Lethal,Phase=st.Phase,Cycle=st.Cycle};
            d.Warn=st.Phase==NativeFactoryHazards.Warning?st.Warn:st.Phase==NativeFactoryHazards.Slamming?1:0;
            if(NativeFactoryHazards.Cycle(h,clock,out int n,out double u))
            {double impact=Math.Max(0,h.period-h.warn-h.rise-h.dwell-h.travel)+h.warn+h.rise;if(u>=impact)d.Since=u-impact;else if(n>0)d.Since=u+h.period-impact;}
            return d;
        }

        // ---------------- audio cues ----------------
        public const int CueDropWarn=0,CueDropSlam=1,CueDropLift=2,CueCoasterWarn=3,CueCoasterDanger=4,CueCoasterPass=5;
        /// <summary>
        /// Every cue in (from, to] (seconds of match time) for these hazards in round `round` with the fight from `fightAt`, into
        /// `into` (the count is returned). Pure and stateless: a caller that keeps its last time hears each cue once. The drop
        /// towers' come from NativeFactoryHazards.Cues on the hazard clock; the coaster's bells, release and pass from its pass
        /// times.
        /// </summary>
        public static int Cues(MapHazard[] hazards,int round,double fightAt,double from,double to,FactoryCue[] into,FactoryCue[] scratch)
        {
            int count=0;if(hazards==null||into==null||!(to>from))return 0;
            double c0=from-fightAt-NativeFactoryHazards.ArmDelay,c1=to-fightAt-NativeFactoryHazards.ArmDelay;
            int n=NativeFactoryHazards.Cues(hazards,round,c0,c1,scratch);
            for(int i=0;i<n&&count<into.Length;i++)
            {
                var c=scratch[i];int kind=c.Kind==NativeFactoryHazards.CuePressHiss?CueDropWarn:c.Kind==NativeFactoryHazards.CuePressSlam?CueDropSlam:c.Kind==NativeFactoryHazards.CuePressLift?CueDropLift:-1;
                if(kind<0)continue;var h=hazards[c.Hazard];into[count++]=new FactoryCue{Kind=kind,Hazard=c.Hazard,Cycle=c.Cycle,Time=c.Time,At=new V3(h.b.x,kind==CueDropWarn?h.a.y:h.b.y-h.size.y/2,h.b.z)};
            }
            for(int i=0;i<hazards.Length&&count<into.Length;i++)
            {
                var h=hazards[i];if(!NativeCyberCityHazards.IsMaglev(h)||!(h.period>0))continue;
                double lo=from-fightAt-h.first,hi=to-fightAt-h.first;
                for(double k=Math.Max(0,Math.Floor((lo-1)/h.period));k*h.period-h.warn<=hi&&count<into.Length;k++)
                {
                    double pass=k*h.period;int d=((int)k+round)%2==0?1:-1;float near=(float)(h.center.x-d*h.size.x/2);
                    void Add(int kind,double at){if(at>lo&&at<=hi&&count<into.Length)into[count++]=new FactoryCue{Kind=kind,Hazard=i,Cycle=(int)k,Time=at+fightAt+h.first,At=new V3(near,h.center.y,h.center.z)};}
                    Add(CueCoasterWarn,pass-h.warn);Add(CueCoasterDanger,pass-h.danger);Add(CueCoasterPass,pass);
                }
            }
            return count;
        }
    }
}
