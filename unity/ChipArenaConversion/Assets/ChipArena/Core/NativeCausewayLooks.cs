using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Starfall Causeway (map `causeway`, docs/arenas/CAUSEWAY.md): an ancient causeway of pale stone and star-metal hung in deep
    /// space between two broken towers. Shared here: which map and look this is, the kit kinds its static detail and rigs draw with
    /// (Core NativeCausewayKit; resolved per look from MapLooks.json `kitLooks`, "causeway-kit-*" ids, an unlisted kind taking
    /// <see cref="Defaults"/>), the places the backdrop, rigs and effects agree on (the ringed planet, the river of stars) and the
    /// hazards' presentation poses as pure functions of the replicated state: the falling stars (kind `press`), the comet pass (kind
    /// `maglev`) and the solar wind (kind `vent` with `push`), plus their audio cues. Presentation only: none of this is sim data,
    /// collides, damages or enters the network frame. Pure and allocation free.
    /// </summary>
    public static class NativeCausewayLooks
    {
        public const string MapId="causeway",Look="causeway-starfall";
        /// <summary>Whether a map id is the Causeway: every Causeway hook is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==MapId;

        /// <summary>The blocks the kit sculpts in full (the wardens, watchers, braziers and fallen drums): the dressing leaves their own
        /// boxes undrawn while the kit draws (Runtime NativeArenaDressing, ArenaPresentation.CausewayKitReady); every sculpted part
        /// stays inside its block grown by NativeCausewayKit.Proud.</summary>
        public static bool KitBlock(string id)=>id!=null&&(id.StartsWith("causeway-warden-",StringComparison.Ordinal)||id.StartsWith("causeway-watcher-",StringComparison.Ordinal)
            ||id.StartsWith("causeway-brazier-",StringComparison.Ordinal)||id.StartsWith("causeway-fallen-",StringComparison.Ordinal));
        /// <summary>Kit kinds (index = kind), resolved once per look through `kitLooks`.</summary>
        public static readonly string[] Kinds={"causeway-kit-stone","causeway-kit-stone-dark","causeway-kit-trim","causeway-kit-metal","causeway-kit-metal-edge","causeway-kit-rune",
            "causeway-kit-rune-cold","causeway-kit-crystal","causeway-kit-crystal-violet","causeway-kit-bronze","causeway-kit-glass","causeway-kit-scorch","causeway-kit-ember","causeway-kit-rock",
            "causeway-kit-cloth","causeway-kit-comet","causeway-kit-statue","causeway-kit-dark","causeway-kit-gold","causeway-kit-cloth-edge"};
        public const int Stone=0,StoneDark=1,Trim=2,Metal=3,MetalEdge=4,Rune=5,RuneCold=6,Crystal=7,CrystalViolet=8,Bronze=9,Glass=10,Scorch=11,Ember=12,Rock=13,Cloth=14,Comet=15,Statue=16,Dark=17,Gold=18,ClothEdge=19;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it.</summary>
        public static readonly (string look,int color)[] Defaults={("cyber-panel",0xD9D2C2),("cyber-panel",0x9C968C),("plain",0xE8E2D4),("cyber-metal",0x3A4466),("plain",0xA9B8DA),("neon",0xFFB45A),
            ("neon",0x8FE6FF),("cyber-tube",0x7FE0FF),("cyber-tube",0xB48CFF),("steel-green",0xA07A48),("cyber-tube",0xFFD9A0),("plain",0x1A1416),("cyber-tube",0xFF8A3A),("plain",0x3A3440),
            ("plain",0x2E3A78),("cyber-tube",0xCFF4FF),("plain",0xE4DED0),("plain",0x14141C),("plain",0xD8B060),("plain",0xD8B060)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "causeway-kit-*" id wins; else the default).</summary>
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

        // ---------------- the places the backdrop, rigs and effects share ----------------
        /// <summary>The ringed planet, low in the south-east: its centre, radius, ring radii (inner, outer) and the ring's tilt
        /// (rad about the axis toward the camera's side) and turn (rad about y).</summary>
        public const double PlanetX=520,PlanetY=60,PlanetZ=-760,PlanetR=230,RingIn=300,RingOut=470,RingTilt=.36,RingTurn=-.5;
        /// <summary>The light the planet's day side faces (a unit vector toward the distant sun, up and west).</summary>
        public const double SunX=-.62,SunY=.52,SunZ=.58;
        /// <summary>The river of stars far below: its depth, its half width and how fast its stars drift along it (m/s).</summary>
        public const double RiverY=-420,RiverWidth=90,RiverDrift=3.2;
        /// <summary>The river's centre line at parameter s (-1..1 along x, m): a slow S across the void.</summary>
        public static void River(double s,out double x,out double z){x=s*1400;z=Math.Sin(s*2.4+.7)*260-120*s;}
        /// <summary>A stable 0..1 hash of three integers.</summary>
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+5501,b+97,c+193);
        public static double Frac(double v)=>v-Math.Floor(v);

        // ---------------- the falling stars (kind `press`, re-rigged as a meteor) ----------------
        /// <summary>A falling star's moment: `Warn` 0..1 through its telegraph (0 outside it), `Fall` 0..1 through its fall (-1 not
        /// falling), `Since` seconds since its last impact (∞ before the first), the impact point (floor), the star's drawn position
        /// and its unit direction of travel, the cycle.</summary>
        public struct Star {public double Warn,Fall,Since,X,Y,Z,Px,Py,Pz,Dx,Dy,Dz;public int Cycle;public bool Live;}
        /// <summary>How far up the slanted path a star kindles (m) and its fall's slant (rad from the vertical).</summary>
        public const double StarReach=150,StarSlant=.62;
        /// <summary>The star of press hazard `h` at hazard clock `clock` (NativeFactoryHazards.Clock): the simulation's own state
        /// decides the phases; the slanted path only changes where the star is drawn, never where it lands or when.</summary>
        public static Star StarAt(MapHazard h,int round,double clock)
        {
            var st=NativeFactoryHazards.State(h,round,clock);var s=new Star{Fall=-1,Since=double.PositiveInfinity,X=h.b.x,Y=h.b.y-h.size.y/2,Z=h.b.z,Cycle=st.Cycle,Live=st.Lethal};
            double w=h.warn,sl=h.rise,open=Math.Max(0,h.period-w-sl-h.dwell-h.travel);
            int cyc=st.Cycle;double dir=Hash(cyc,(int)Math.Round(h.b.x*10),(int)Math.Round(h.b.z*10))*2*Math.PI;
            double ca=Math.Cos(dir),sa=Math.Sin(dir),sin=Math.Sin(StarSlant),cos=Math.Cos(StarSlant);s.Dx=-sa*sin;s.Dy=-cos;s.Dz=-ca*sin;
            if(NativeFactoryHazards.Cycle(h,clock,out int n,out double u))
            {
                double impact=open+w+sl;
                if(u>=impact)s.Since=u-impact;else if(n>0)s.Since=u+h.period-impact;
                if(u>=open&&u<open+w)s.Warn=(u-open)/w;
                if(u>=open+w&&u<impact){s.Fall=(u-open-w)/sl;s.Warn=1;}
            }
            double reach=s.Fall>=0?StarReach*(1-Math.Pow(s.Fall,1.6)):StarReach;
            s.Px=s.X-s.Dx*reach;s.Py=s.Y-s.Dy*reach;s.Pz=s.Z-s.Dz*reach;return s;
        }

        // ---------------- the comet (kind `maglev`) ----------------
        /// <summary>The comet's head along the span's lane (x; it flies on beyond the portals both ways), its direction (+1 east),
        /// the phase and the telegraph's level.</summary>
        public static NativeHazardState CometAt(MapHazard h,NativePhase phase,int round,double fightAt,double now)=>NativeCyberCityHazards.At(h,phase,round,fightAt,now);
        /// <summary>Where the comet's head is drawn: far out along the bridge's axis and high through the warning, sweeping down to
        /// its near portal at the lane's height for the pass (the simulation's head x throughout); `near` 0..1 how close it is; false
        /// when it is not in the sky (idle, clear).</summary>
        public static bool CometHead(MapHazard h,in NativeHazardState st,out double x,out double y,out double z,out double near)
        {
            x=y=z=near=0;if(st.Phase!=NativeHazardPhase.Warn&&st.Phase!=NativeHazardPhase.Danger&&st.Phase!=NativeHazardPhase.Active)return false;
            int dir=st.Direction==0?1:st.Direction;double x0=h.center.x-h.size.x/2,x1=h.center.x+h.size.x/2;x=st.Head;double dist=dir>0?x0-x:x-x1;
            y=h.center.y+Math.Max(0,dist-6)*.16;z=h.center.z+Math.Max(0,dist-6)*.06;near=st.Phase==NativeHazardPhase.Active?1:Math.Max(0,Math.Min(1,1-dist/320));return true;
        }

        // ---------------- the solar wind (kind `vent` with `push`) ----------------
        /// <summary>The gust's telegraph (0..1, 0 outside it), its strength (0..1 while it blows) and the seconds since it ended.</summary>
        public static void WindAt(MapHazard h,int round,double clock,out double warn,out double gust,out double after)
        {
            var st=NativeFactoryHazards.State(h,round,clock);warn=st.Phase==NativeFactoryHazards.Warning?st.Warn:0;gust=st.Phase==NativeFactoryHazards.Bursting?st.U:0;after=double.PositiveInfinity;
            // the burst closes each cycle (quiet, warning, burst): the time into a later cycle is the time since the last gust
            if(st.Phase==NativeFactoryHazards.Parked&&NativeFactoryHazards.Cycle(h,clock,out int n,out double u)&&n>0)after=u;
        }

        // ---------------- audio cues ----------------
        public const int CueStarWarn=0,CueStarFall=1,CueStarImpact=2,CueCometWarn=3,CueCometDanger=4,CueCometPass=5,CueWindWarn=6,CueWindGust=7;
        /// <summary>
        /// Every cue in (from, to] (seconds of match time) for these hazards in round `round` with the fight from `fightAt`, into
        /// `into` (the count is returned). Pure and stateless: a caller that keeps its last time hears each cue once. The falling
        /// stars and the wind come from NativeFactoryHazards.Cues on the hazard clock; the comet's warning, danger and pass from its
        /// pass times.
        /// </summary>
        public static int Cues(MapHazard[] hazards,int round,double fightAt,double from,double to,FactoryCue[] into,FactoryCue[] scratch)
        {
            int count=0;if(hazards==null||into==null||!(to>from))return 0;
            double c0=from-fightAt-NativeFactoryHazards.ArmDelay,c1=to-fightAt-NativeFactoryHazards.ArmDelay;
            int n=NativeFactoryHazards.Cues(hazards,round,c0,c1,scratch);
            for(int i=0;i<n&&count<into.Length;i++)
            {
                var c=scratch[i];int kind=c.Kind==NativeFactoryHazards.CuePressHiss?CueStarWarn:c.Kind==NativeFactoryHazards.CuePressSlam?CueStarImpact:c.Kind==NativeFactoryHazards.CueVentHiss?CueWindWarn:c.Kind==NativeFactoryHazards.CueVentBurst?CueWindGust:-1;
                if(kind<0)continue;var h=hazards[c.Hazard];
                if(kind==CueStarWarn){var s=StarAt(h,round,c.Time+1e-6);into[count++]=new FactoryCue{Kind=kind,Hazard=c.Hazard,Cycle=c.Cycle,Time=c.Time,At=new V3((float)s.X,(float)s.Y,(float)s.Z)};
                    double fall=c.Time+h.warn;if(fall>c0&&fall<=c1&&count<into.Length)into[count++]=new FactoryCue{Kind=CueStarFall,Hazard=c.Hazard,Cycle=c.Cycle,Time=fall,At=new V3((float)s.X,(float)s.Y,(float)s.Z)};}
                else into[count++]=new FactoryCue{Kind=kind,Hazard=c.Hazard,Cycle=c.Cycle,Time=c.Time,At=kind==CueStarImpact?new V3(h.b.x,h.b.y-h.size.y/2,h.b.z):new V3(h.a.x,h.a.y+1.6f,h.a.z)};
            }
            // a star whose telegraph began before the window but falls inside it
            for(int i=0;i<hazards.Length&&count<into.Length;i++)
            {
                var h=hazards[i];if(h?.kind!=NativeFactoryHazards.Press||!(h.period>0))continue;double open=Math.Max(0,h.period-h.warn-h.rise-h.dwell-h.travel),origin=h.start+h.offset+open+h.warn;
                double k=Math.Floor((c1-origin)/h.period);double t=origin+k*h.period;
                if(k>=0&&t>c0&&t<=c1&&!(t-h.warn>c0)){var s=StarAt(h,round,t+1e-6);into[count++]=new FactoryCue{Kind=CueStarFall,Hazard=i,Cycle=(int)k,Time=t,At=new V3((float)s.X,(float)s.Y,(float)s.Z)};}
            }
            for(int i=0;i<hazards.Length&&count<into.Length;i++)
            {
                var h=hazards[i];if(!NativeCyberCityHazards.IsMaglev(h)||!(h.period>0))continue;
                double lo=from-fightAt-h.first,hi=to-fightAt-h.first;
                for(double k=Math.Max(0,Math.Floor((lo-1)/h.period));k*h.period-h.warn<=hi&&count<into.Length;k++)
                {
                    double pass=k*h.period;int d=((int)k+round)%2==0?1:-1;float near=(float)(h.center.x-d*h.size.x/2);
                    void Add(int kind,double at){if(at>lo&&at<=hi&&count<into.Length)into[count++]=new FactoryCue{Kind=kind,Hazard=i,Cycle=(int)k,Time=at+fightAt+h.first,At=new V3(near,h.center.y,h.center.z)};}
                    Add(CueCometWarn,pass-h.warn);Add(CueCometDanger,pass-h.danger);Add(CueCometPass,pass);
                }
            }
            return count;
        }
    }
}
