using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Breakwater (map `breakwater`, docs/arenas/BREAKWATER.md; rebuilt 2026-11-11): a storm-battered granite breakwater at blue
    /// hour, the lighthouse at its head. Shared here: which map and look this is, the kit kinds its box kit and rig draw with
    /// (Core NativeBreakwaterKit; resolved per look from MapLooks.json `kitLooks`, "breakwater-kit-*" ids, an unlisted kind taking
    /// <see cref="Defaults"/>), the places the backdrop, rig, effects and art agree on (the sea's level, the lighthouse and its
    /// lamp, the bell buoy, the fishing boat) and the stage's motion as pure functions of the clock: the lamp's turn, the swells
    /// that run in on the wave wall and break, the lanterns' swing, the buoy's rocking, the gulls' wheeling; the wash (kind `vent`
    /// with `push`) as a function of the replicated hazard state; and the audio cues. Presentation only: none of this is sim
    /// data, collides, damages or enters the network frame. Pure and allocation free.
    /// </summary>
    public static class NativeBreakwaterLooks
    {
        public const string MapId="breakwater",Look="breakwater-blue-hour";
        /// <summary>Whether a map id is Breakwater: every Breakwater hook is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==MapId;

        /// <summary>Kit kinds (index = kind), resolved once per look through `kitLooks`.</summary>
        public static readonly string[] Kinds={"breakwater-kit-granite","breakwater-kit-granite-dark","breakwater-kit-coping","breakwater-kit-wet","breakwater-kit-iron","breakwater-kit-rust",
            "breakwater-kit-timber","breakwater-kit-rope","breakwater-kit-lamp","breakwater-kit-glass","breakwater-kit-brass","breakwater-kit-lime","breakwater-kit-band","breakwater-kit-copper",
            "breakwater-kit-slate","breakwater-kit-sea","breakwater-kit-foam","breakwater-kit-gull","breakwater-kit-hull","breakwater-kit-red","breakwater-kit-flag","breakwater-kit-beam"};
        public const int Granite=0,GraniteDark=1,Coping=2,Wet=3,Iron=4,Rust=5,Timber=6,Rope=7,Lamp=8,Glass=9,Brass=10,Lime=11,Band=12,Copper=13,Slate=14,Sea=15,Foam=16,Gull=17,Hull=18,Red=19,Flag=20,Beam=21;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it.</summary>
        public static readonly (string look,int color)[] Defaults={("cobble",0x7C8088),("cobble",0x565A62),("concrete-board",0x9A9C9E),("plain",0x3A4048),("plain",0x23262C),("plain",0x6A4634),
            ("timber",0x6E5238),("plain",0xB09A6C),("neon",0xFFC878),("window-lit",0xFFD79A),("plain",0xB89A52),("concrete-board",0xE4E0D4),("plain",0xA83A30),("plain",0x4F8A72),
            ("tar",0x39414C),("plain",0x1C3A52),("plain",0xDCE8EE),("plain",0xE6E8EA),("timber",0x2E4A5A),("plain",0xC2382C),("plain",0xB8322A),("neon",0xFFF0C8)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "breakwater-kit-*" id wins; else the default).</summary>
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
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+4409,b+173,c+59);
        public static double Frac(double v)=>v-Math.Floor(v);

        // ---------------- the places the backdrop, rig, effects and art share ----------------
        /// <summary>The sea's mean level (m): a metre under the quay, 3.4 m under the deck; a pilot who falls goes under before
        /// killY (-5) takes them.</summary>
        public const double SeaY=-3.4;
        /// <summary>The mole's long faces (the wave wall's seaward face north, the quay's harbour face south) and the walk's top.</summary>
        public const double WallZ=8.4,QuayZ=-9.5,WalkY=2.4,QuayY=-2.4;
        /// <summary>The lighthouse past the east hall, outside the bounds: its axis, the rock it stands on, the gallery and the
        /// lantern's centre (the lamp), the lamp's turn (rad/s) and its two beams' reach (m) and dip (rad below the level).</summary>
        public const double LightX=31,LightZ=0,LightFoot=1.2,GalleryY=22.4,LanternY=24.6,BeamTurn=.52,BeamReach=95,BeamDip=.16;
        /// <summary>The lamp's heading (rad, from +z toward +x) at render time `t`: a slow, steady sweep (two opposite beams, so
        /// one crosses the arena about every six seconds).</summary>
        public static double BeamHeading(double t)=>Frac(t*BeamTurn/(2*Math.PI))*2*Math.PI;
        /// <summary>How squarely a beam points at (x, z) from the lamp: 0..1, the practical light's own falloff (cos^6).</summary>
        public static double BeamOn(double t,double x,double z)
        {
            double a=BeamHeading(t),dx=x-LightX,dz=z-LightZ,l=Math.Sqrt(dx*dx+dz*dz);if(l<1e-6)return 1;double c=Math.Abs((dx*Math.Sin(a)+dz*Math.Cos(a))/l);return c*c*c*c*c*c;
        }

        // ---------------- the swells: the sea running in on the wave wall ----------------
        /// <summary>The swells that roll in from the north and break on the wall: how many, where they rise (z), how fast (m/s).</summary>
        public const int Swells=5;public const double SwellFar=56,SwellSpeed=3.6;
        public static double SwellPeriod=>(SwellFar-WallZ)/SwellSpeed;
        /// <summary>A swell's moment: its crest's centre (x, z), half length, height over the sea, how far through its run (0..1)
        /// and which run this is.</summary>
        public struct Swell {public double X,Z,Half,Height,U;public int Cycle;}
        private static void Lane(int i,int cycle,out double x,out double half){x=(Hash(cycle,i,1)-.5)*40;half=8+Hash(cycle,i,2)*7;}
        /// <summary>Swell `i` at render time `t`: it rises far out, steepens as it shoals and falls flat in the last metre.</summary>
        public static Swell SwellAt(int i,double t)
        {
            double p=t/SwellPeriod+(double)i/Swells;int c=(int)Math.Floor(p);double u=p-c;var s=new Swell{U=u,Cycle=c,Z=SwellFar-u*(SwellFar-WallZ)};Lane(i,c,out s.X,out s.Half);
            double rise=Math.Min(1,u/.12),fall=u>.95?(1-u)/.05:1;s.Height=(.3+1.15*u*u)*rise*fall;return s;
        }
        /// <summary>Seconds since swell `i` last broke on the wall, and where (the crest's centre and half length).</summary>
        public static double SinceBreak(int i,double t,out double x,out double half)
        {
            double p=t/SwellPeriod+(double)i/Swells;int c=(int)Math.Floor(p);Lane(i,c-1,out x,out half);return (p-c)*SwellPeriod;
        }
        /// <summary>How long a break's spray stands (s).</summary>
        public const double SprayLife=1.7;
        /// <summary>The sea's height over <see cref="SeaY"/> at (x, z) and render time `t`: the long swell and a cross chop (the
        /// buoy and the boats ride it).</summary>
        public static double SeaAt(double x,double z,double t)=>.32*Math.Sin(z*.21+t*.75+x*.03)+.14*Math.Sin(x*.37-t*1.1+z*.11)+.07*Math.Sin((x+z)*.9+t*1.9);

        // ---------------- the bell buoy, the boat, the lanterns, the gulls ----------------
        /// <summary>The bell buoy out in the north-west sea and the fishing boat moored in the harbour (both outside the bounds).</summary>
        public const double BuoyX=-13.5,BuoyZ=26,BoatX=8.5,BoatZ=-19.5,DinghyX=-11,DinghyZ=-16.5;
        /// <summary>A floating thing's pose on the sea at (x, z): its lift over <see cref="SeaY"/> and its lean about x and z
        /// (rad), `k` how lively it rides.</summary>
        public static void Ride(double x,double z,double t,double k,out double lift,out double leanX,out double leanZ)
        {
            lift=SeaAt(x,z,t);const double d=.8;leanX=Math.Atan((SeaAt(x,z+d,t)-SeaAt(x,z-d,t))/(2*d))*k;leanZ=-Math.Atan((SeaAt(x+d,z,t)-SeaAt(x-d,z,t))/(2*d))*k;
            leanX+=.05*k*Math.Sin(t*1.7+x);leanZ+=.04*k*Math.Sin(t*1.3+z);
        }
        /// <summary>The buoy's lamp: a short red flash every 4 s (0..1).</summary>
        public static double BuoyLamp(double t){double u=Frac(t/4);return u<.12?1:u<.2?1-(u-.12)/.08:0;}
        /// <summary>When the buoy's bell sounds: about every 5.3 s, as the swell tips it over.</summary>
        public const double BuoyBellEvery=5.3;
        /// <summary>A lantern's swing on its bracket (rad): the wind's steady sway and gusts, livelier (`gust` 0..1) while a sea
        /// breaks over its part of the mole.</summary>
        public static double LanternSwing(int i,double t,double gust)=>.16*Math.Sin(t*1.9+i*1.7)+.07*Math.Sin(t*3.3+i*.9)+gust*(.5*Math.Sin(t*5.1+i)+.18);
        /// <summary>Gulls wheeling over the mole and round the lantern: how many, and gull `i`'s place, heading (rad from +z
        /// toward +x), bank (rad, into its turn) and wing angle (rad above the level: it flaps in bursts and glides between).</summary>
        public const int Gulls=9;
        public static void GullAt(int i,double t,out double x,out double y,out double z,out double heading,out double bank,out double flap)
        {
            // three round the lantern, six on wide, slow ovals over the mole and the sea, every one above the ceiling
            bool tower=i<3;double cx=tower?LightX:(-16+Hash(71,i)*26),cz=tower?LightZ:(-4+Hash(71,i,1)*22),rx=tower?7+i*2.2:13+Hash(71,i,2)*13,rz=tower?7+i*2.2:8+Hash(71,i,3)*9;
            double w=(tower?.42:.26+Hash(71,i,4)*.12)*(i%2==0?1:-1),a=t*w+i*1.9;
            x=cx+Math.Sin(a)*rx;z=cz+Math.Cos(a)*rz;y=(tower?LanternY+1.5+i*1.3:17.5+Hash(71,i,5)*7)+1.1*Math.Sin(t*.37+i*2.1);
            double vx=Math.Cos(a)*rx*w,vz=-Math.Sin(a)*rz*w;heading=Math.Atan2(vx,vz);bank=(w>0?1:-1)*(tower?.42:.24);
            double beat=Frac(t*.11+Hash(71,i,6));flap=beat<.34?.55*Math.Sin(t*9.5+i):.1+.05*Math.Sin(t*1.3+i);
        }

        // ---------------- the wash (kind `vent` with `push`) ----------------
        /// <summary>The wash's moment: `warn` 0..1 through its telegraph (0 outside it), `wash` 0..1 through its run across the
        /// mole (-1 while it is not running), `level` its strength (0..1 while it runs) and the seconds since it ended.</summary>
        public static void WashAt(MapHazard h,int round,double clock,out double warn,out double wash,out double level,out double after)
        {
            var st=NativeFactoryHazards.State(h,round,clock);warn=st.Phase==NativeFactoryHazards.Warning?st.Warn:0;level=st.Phase==NativeFactoryHazards.Bursting?st.U:0;wash=-1;after=double.PositiveInfinity;
            if(!NativeFactoryHazards.Cycle(h,clock,out int n,out double u))return;
            if(st.Phase==NativeFactoryHazards.Bursting&&h.dwell>0)wash=Math.Max(0,Math.Min(1,(u-(h.period-h.dwell))/h.dwell));
            // the burst closes each cycle (quiet, warning, burst): the time into a later cycle is the time since the last wash
            if(st.Phase==NativeFactoryHazards.Parked&&n>0)after=u;
        }
        /// <summary>The comber, the sea that will come over the wall: its crest's z and height over the sea through the telegraph
        /// (it gathers 26 m out and reaches the wall as the warning ends) and through the wash (it stands over the breach).</summary>
        public static void Comber(double warn,double wash,out double z,out double height)
        {
            if(wash>=0){z=WallZ+.4;height=3.4*(1-wash*wash)+2.2;return;}
            double e=warn*warn*(3-2*warn);z=WallZ+.6+26*(1-e);height=.6+5.0*warn*warn;
        }
        /// <summary>The wash's front on the mole (z) at `wash` 0..1: over the wall's top at once, across the deck and down onto
        /// the quay by the end.</summary>
        public static double WashFront(double wash)=>WallZ-wash*1.25*(WallZ-QuayZ);

        // ---------------- audio cues ----------------
        public const int CueWashWarn=0,CueWashBreak=1,CueBreak=2,CueBuoyBell=3;
        /// <summary>
        /// Every cue in (from, to] (seconds of match time) in round `round` with the fight from `fightAt`, into `into` (the count is
        /// returned). Pure and stateless: a caller that keeps its last time hears each cue once. The wash's telegraph and its
        /// break come from NativeFactoryHazards.Cues on the hazard clock; the swells' breaks on the wall and the buoy's bell from
        /// the match time itself.
        /// </summary>
        public static int Cues(MapHazard[] hazards,int round,double fightAt,double from,double to,FactoryCue[] into,FactoryCue[] scratch)
        {
            int count=0;if(into==null||!(to>from))return 0;
            if(hazards!=null&&scratch!=null)
            {
                double c0=from-fightAt-NativeFactoryHazards.ArmDelay,c1=to-fightAt-NativeFactoryHazards.ArmDelay;int n=NativeFactoryHazards.Cues(hazards,round,c0,c1,scratch);
                for(int i=0;i<n&&count<into.Length;i++)
                {
                    var c=scratch[i];int kind=c.Kind==NativeFactoryHazards.CueVentHiss?CueWashWarn:c.Kind==NativeFactoryHazards.CueVentBurst?CueWashBreak:-1;if(kind<0)continue;var h=hazards[c.Hazard];
                    into[count++]=new FactoryCue{Kind=kind,Hazard=c.Hazard,Cycle=c.Cycle,Time=c.Time,At=new V3(h.a.x,(float)WalkY,(float)(kind==CueWashWarn?WallZ+6:WallZ-2))};
                }
            }
            double period=SwellPeriod;
            for(int i=0;i<Swells&&count<into.Length;i++)
            {
                // swell i breaks when t / period + i / Swells crosses a whole number
                double k=Math.Floor(to/period+(double)i/Swells),at=(k-(double)i/Swells)*period;
                if(at>from&&at<=to){Lane(i,(int)k-1,out double x,out _);into[count++]=new FactoryCue{Kind=CueBreak,Hazard=i,Cycle=(int)k,Time=at,At=new V3((float)x,(float)WalkY,(float)WallZ)};}
            }
            {double k=Math.Floor(to/BuoyBellEvery),at=k*BuoyBellEvery;if(at>from&&at<=to&&count<into.Length)into[count++]=new FactoryCue{Kind=CueBuoyBell,Hazard=0,Cycle=(int)k,Time=at,At=new V3((float)BuoyX,(float)SeaY+2,(float)BuoyZ)};}
            return count;
        }
    }
}
