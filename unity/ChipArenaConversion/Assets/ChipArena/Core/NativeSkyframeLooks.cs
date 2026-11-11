using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Skyframe (map `skyframe`, docs/arenas/SKYFRAME.md; the 2026-11-11 rebuild): a sky garden at the top of a tower still under
    /// construction, above a sea of clouds at sunset. Shared here: which map and look this is, the kit kinds its static detail and
    /// rigs draw with (Core NativeSkyframeKit; resolved per look from MapLooks.json `kitLooks`, "skyframe-kit-*" ids, an unlisted
    /// kind taking <see cref="Defaults"/>), the places the backdrop, rigs, effects and the modelled art agree on (the tower crane,
    /// the unfinished frame, the sun, the cloud sea, the wind turbines, the airships' and the birds' ways) and the hazards'
    /// presentation poses as pure functions of the replicated state: the crane's beam drop (kind `crane`) and the crosswind (kind
    /// `vent` with `push`), plus their audio cues. Presentation only: none of this is sim data, collides, damages or enters the
    /// network frame. Pure and allocation free.
    /// </summary>
    public static class NativeSkyframeLooks
    {
        public const string MapId="skyframe",Look="skyframe-golden-hour";
        /// <summary>Whether a map id is Skyframe: every Skyframe hook is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==MapId;

        /// <summary>Kit kinds (index = kind), resolved once per look through `kitLooks`.</summary>
        public static readonly string[] Kinds={"skyframe-kit-stone","skyframe-kit-stone-dark","skyframe-kit-trim","skyframe-kit-steel","skyframe-kit-primer","skyframe-kit-glass","skyframe-kit-leaf",
            "skyframe-kit-leaf-dark","skyframe-kit-bark","skyframe-kit-soil","skyframe-kit-timber","skyframe-kit-crane","skyframe-kit-crane-dark","skyframe-kit-cable","skyframe-kit-load",
            "skyframe-kit-weight","skyframe-kit-lamp","skyframe-kit-paint","skyframe-kit-cloth","skyframe-kit-cloth-accent","skyframe-kit-white","skyframe-kit-bird","skyframe-kit-cloud",
            "skyframe-kit-water","skyframe-kit-red","skyframe-kit-lawn"};
        public const int Stone=0,StoneDark=1,Trim=2,Steel=3,Primer=4,Glass=5,Leaf=6,LeafDark=7,Bark=8,Soil=9,Timber=10,Crane=11,CraneDark=12,Cable=13,Load=14,Weight=15,Lamp=16,Paint=17,
            Cloth=18,ClothAccent=19,White=20,Bird=21,Cloud=22,Water=23,Red=24,Lawn=25;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it.</summary>
        public static readonly (string look,int color)[] Defaults={("cyber-tile",0xD8CDB8),("cyber-tile",0x9A917F),("plain",0xEFE6D2),("steel-green",0x4A4E55),("steel-green",0xB4553A),("window-lit",0x9FD8E0),
            ("plain",0x5E9A46),("plain",0x3C7036),("timber",0x6A4A32),("plain",0x4A3628),("timber",0xB98A58),("steel-green",0xF2B21E),("plain",0x2C2E33),("plain",0x1C1C1E),("steel-green",0xB4553A),
            ("concrete-board",0x9A968C),("neon",0xFFC878),("plain",0xF0B020),("plain",0xF1E7D2),("plain",0xE8742A),("plain",0xF2F0EA),("plain",0x2A2A30),("plain",0xFFF1DC),("window-lit",0x7FC4D8),
            ("neon",0xFF3A2A),("plain",0x7BAA4A)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "skyframe-kit-*" id wins; else the default).</summary>
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
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+3319,b+71,c+157);
        public static double Frac(double v)=>v-Math.Floor(v);

        // ---------------- the places the backdrop, rigs, effects and the art share ----------------
        /// <summary>The levels: the cloud terraces, the garden, the gallery and the pavilion roofs (m).</summary>
        public const double TerraceY=-3.2,GardenY=0,GalleryY=3.6;
        /// <summary>The unfinished frame over the gallery: its half extents in plan (nothing of it lies outside them, so the crane's
        /// load passes round it), its first beam level, the storey height and the storeys drawn.</summary>
        public const double FrameHalf=9.0,FrameY=9.2,Storey=4.0;public const int Storeys=5;
        /// <summary>The lift core's top (its block) and how far its concrete goes on above it.</summary>
        public const double CoreTop=12,CoreRise=6;
        /// <summary>The climbing crane: its mast up the core's middle to the slewing ring, the jib's bottom chord (the hazard's
        /// `a.y`), the jib's and the counter-jib's lengths, the jib's depth and half width, the trolley's least reach.</summary>
        public const double MastHalf=1.1,JibY=38,JibLength=34,CounterJib=12,JibDepth=2.4,JibHalf=.9,MinReach=11;
        /// <summary>The sun: its bearing (degrees from +z toward +x) and elevation, as the look's key; low in the south-south-west.</summary>
        public const double SunBearing=205,SunElevation=18;
        /// <summary>The unit vector toward the sun.</summary>
        public static void Sun(out double x,out double y,out double z){double b=SunBearing*Math.PI/180,e=SunElevation*Math.PI/180;x=Math.Sin(b)*Math.Cos(e);y=Math.Sin(e);z=Math.Cos(b)*Math.Cos(e);}
        /// <summary>The cloud sea: its top's mean level, and the wind that carries it (m/s along +x).</summary>
        public const double CloudY=-34,CloudDrift=1.6;

        /// <summary>The wind turbines at the tower's four corners (on outriggers below the garden): hub x, y, z each; the rotor's
        /// radius; they face the wind (the rotor's axis along x).</summary>
        public const int Turbines=4;public const double TurbineRadius=6.5,TurbineHubY=15;
        public static void Turbine(int i,out double x,out double y,out double z){x=(i&1)==0?-35:35;z=(i&2)==0?30:-30;y=TurbineHubY+((i==1||i==2)?1.5:0);}
        /// <summary>A turbine rotor's angle (rad) at render time t: each its own steady speed, a little faster in a gust.</summary>
        public static double TurbineAngle(int i,double t)=>t*(1.9+.35*Hash(41,i))+i*1.3;

        /// <summary>The airships: index i at render time t on its slow circuit round the tower: the hull's centre, its heading
        /// (rad, from +z toward +x) and a slight nod.</summary>
        public const int Airships=3;
        public static void Airship(int i,double t,out double x,out double y,out double z,out double heading,out double pitch)
        {
            double rx=i==0?120:i==1?190:300,rz=i==0?95:i==1?150:240,cy=i==0?22:i==1?-6:46,w=(i==1?-1:1)*(i==0?.035:i==1?.022:.012),a=t*w+i*2.1+.6;
            x=Math.Sin(a)*rx+(i==2?40:0);z=Math.Cos(a)*rz-(i==0?10:0);y=cy+Math.Sin(t*.21+i)*1.4;
            double dx=Math.Cos(a)*rx*w,dz=-Math.Sin(a)*rz*w;heading=Math.Atan2(dx,dz);pitch=Math.Sin(t*.17+i*1.9)*.03;
        }
        /// <summary>An airship's length (m) by index.</summary>
        public static double AirshipLength(int i)=>i==0?34:i==1?26:44;

        /// <summary>The birds: a loose flock wheeling round the frame. Bird i at render time t: its place, heading, bank and its
        /// wings' flap angle (rad up from level).</summary>
        public const int Birds=18;
        public static void BirdAt(int i,double t,out double x,out double y,out double z,out double heading,out double bank,out double flap)
        {
            int f=i%3;double h0=Hash(77,i),h1=Hash(77,i,1),h2=Hash(77,i,2);
            double r=(f==0?26:f==1?40:58)+h0*9,w=(f==1?-1:1)*(.16+.05*h1)*(26/r)*1.6,a=t*w+h2*1.1+f*2.4+(i/3)*.07;
            double cx=f==2?-14:0,cz=f==2?10:0;x=cx+Math.Sin(a)*r;z=cz+Math.Cos(a)*r*.82;y=(f==0?27:f==1?19:30)+h1*4+Math.Sin(t*.5+i)*1.2;
            heading=Math.Atan2(Math.Cos(a)*r*w,-Math.Sin(a)*r*.82*w);bank=(w>0?-1:1)*.32;
            double glide=Math.Sin(t*.23+h0*20);flap=glide>.55?.12:Math.Sin(t*(7.5+h1*2)+i*1.7)*.75;
        }

        /// <summary>The drifting cloud islands of the sea below: island i at render time t (carried along +x, wrapping round a
        /// <see cref="CloudSpan"/> m span): its centre, half extents and which of the <see cref="CloudShapes"/> shapes it is.</summary>
        public const int Clouds=34,CloudShapes=4;public const double CloudSpan=900;
        public static void CloudAt(int i,double t,out double x,out double y,out double z,out double hx,out double hy,out double hz,out int shape)
        {
            double h0=Hash(91,i),h1=Hash(91,i,1),h2=Hash(91,i,2),h3=Hash(91,i,3);shape=i%CloudShapes;
            // two banks, north and south of the tower's line (the static sea lies under and round the tower itself): nothing
            // drifts through the play space
            int ring=i%3;double depth=ring==0?86+h1*60:ring==1?180+h1*110:320+h1*140;
            z=(i%2==0?1:-1)*depth+(h3-.5)*30;double speed=CloudDrift*(.7+.6*h3)*(ring==0?1.25:ring==1?1:.8);
            x=(Frac(h0+t*speed/CloudSpan)-.5)*CloudSpan;
            hx=(30+h2*38)*(ring==2?1.5:1);hz=hx*(.55+.3*h1);hy=hx*(.16+.1*h3);y=CloudY+(h1-.5)*10-ring*3+hy*.4;
        }

        // ---------------- the tower crane (kind `crane`) ----------------
        /// <summary>
        /// The drawn hook for the crane at hazard clock `clock`: its heading (rad, from +z toward +x) and its reach from the mast.
        /// Travelling, the jib slews and the trolley runs from the zone it left to the one it serves on the simulation's own easing
        /// (the zones ring the tower, so the load swings round it, never through the frame); at every other phase it is the
        /// simulation's hook (X, Z). Before the first lift it waits over its first zone. True when the drawn hook is the
        /// simulation's own point.
        /// </summary>
        public static bool CraneReach(MapHazard h,in FactoryHazardState st,int round,double clock,out double heading,out double reach)
        {
            heading=0;reach=MinReach;if(h?.zones==null||h.zones.Length==0)return false;
            if(st.Cycle<0||st.Zone<0){var c=h.zones[Math.Max(0,NativeFactoryHazards.ZoneOf(h,round,0))].center;heading=Math.Atan2(c.x,c.z);reach=Math.Max(MinReach,Math.Sqrt(c.x*c.x+c.z*c.z));return false;}
            if(st.Phase==NativeFactoryHazards.Travelling&&NativeFactoryHazards.Cycle(h,clock,out _,out double u)&&h.travel>0)
            {
                var to=h.zones[st.Zone].center;double a1=Math.Atan2(to.x,to.z),r1=Math.Sqrt(to.x*to.x+to.z*to.z),a0=a1,r0=r1;
                if(st.From>=0){var from=h.zones[st.From].center;a0=Math.Atan2(from.x,from.z);r0=Math.Sqrt(from.x*from.x+from.z*from.z);}
                double d=a1-a0;while(d>Math.PI)d-=2*Math.PI;while(d<-Math.PI)d+=2*Math.PI;double q=NativeFactoryHazards.Smooth(u/h.travel);
                heading=a0+d*q;reach=Math.Max(MinReach,r0+(r1-r0)*q);return q>=1-1e-9;
            }
            heading=Math.Atan2(st.X,st.Z);reach=Math.Sqrt(st.X*st.X+st.Z*st.Z);if(reach<MinReach){reach=MinReach;return false;}return true;
        }
        /// <summary>The crane's moment for the presentation: the simulation's state, the drawn hook (heading, reach, the point
        /// under the trolley), the load's underside and the hook block's, the telegraph 0..1 and the seconds since the last
        /// impact (∞ before the first).</summary>
        public struct CraneMoment {public FactoryHazardState State;public double Heading,Reach,X,Z,LoadY,HookY,Warn,Since,FloorY;public bool Exact;public int Zone;}
        public static CraneMoment CraneAt(MapHazard h,int round,double clock)
        {
            var st=NativeFactoryHazards.State(h,round,clock);var m=new CraneMoment{State=st,LoadY=st.LoadY,HookY=st.HookY,Warn=st.Phase==NativeFactoryHazards.Warning?st.Warn:st.Phase==NativeFactoryHazards.Falling?1:0,Since=double.PositiveInfinity,Zone=st.Zone};
            m.Exact=CraneReach(h,st,round,clock,out m.Heading,out m.Reach);m.X=Math.Sin(m.Heading)*m.Reach;m.Z=Math.Cos(m.Heading)*m.Reach;
            m.FloorY=st.Zone>=0?h.zones[st.Zone].center.y:0;
            if(!double.IsNaN(st.ImpactAt)){if(clock>=st.ImpactAt)m.Since=clock-st.ImpactAt;else if(st.Cycle>0)m.Since=clock-(st.ImpactAt-h.period);}
            return m;
        }

        // ---------------- the crosswind (kind `vent` with `push`) ----------------
        /// <summary>The gust's telegraph (0..1, 0 outside it), its strength (0..1 while it blows) and the seconds since it ended.</summary>
        public static void WindAt(MapHazard h,int round,double clock,out double warn,out double gust,out double after)
        {
            var st=NativeFactoryHazards.State(h,round,clock);warn=st.Phase==NativeFactoryHazards.Warning?st.Warn:0;gust=st.Phase==NativeFactoryHazards.Bursting?st.U:0;after=double.PositiveInfinity;
            if(st.Phase==NativeFactoryHazards.Parked&&NativeFactoryHazards.Cycle(h,clock,out int n,out double u)&&n>0)after=u;
        }
        /// <summary>How hard the wind blows over the whole stage (0..1): the strongest gust or its telegraph's rise, for the trees,
        /// the windsocks and the leaves.</summary>
        public static double Windy(MapHazard[] hazards,int round,double clock)
        {
            double w=0;for(int i=0;i<(hazards?.Length??0);i++){var h=hazards[i];if(h?.kind!=NativeFactoryHazards.Vent)continue;WindAt(h,round,clock,out double warn,out double gust,out double after);
                w=Math.Max(w,Math.Max(gust,Math.Max(warn*.6,after<1.5?(1-after/1.5)*.6:0)));}
            return w;
        }

        // ---------------- audio cues ----------------
        public const int CueCraneMove=0,CueCraneHorn=1,CueCraneRelease=2,CueCraneImpact=3,CueCraneHoist=4,CueWindWarn=5,CueWindGust=6;
        /// <summary>
        /// Every cue in (from, to] (seconds of match time) for these hazards in round `round` with the fight from `fightAt`, into
        /// `into` (the count is returned). Pure and stateless: a caller that keeps its last time hears each cue once. All from
        /// NativeFactoryHazards.Cues on the hazard clock.
        /// </summary>
        public static int Cues(MapHazard[] hazards,int round,double fightAt,double from,double to,FactoryCue[] into,FactoryCue[] scratch)
        {
            int count=0;if(hazards==null||into==null||!(to>from))return 0;
            double c0=from-fightAt-NativeFactoryHazards.ArmDelay,c1=to-fightAt-NativeFactoryHazards.ArmDelay;
            int n=NativeFactoryHazards.Cues(hazards,round,c0,c1,scratch);
            for(int i=0;i<n&&count<into.Length;i++)
            {
                var c=scratch[i];int kind=c.Kind==NativeFactoryHazards.CueCraneMove?CueCraneMove:c.Kind==NativeFactoryHazards.CueHorn?CueCraneHorn:c.Kind==NativeFactoryHazards.CueRelease?CueCraneRelease:
                    c.Kind==NativeFactoryHazards.CueImpact?CueCraneImpact:c.Kind==NativeFactoryHazards.CueHoist?CueCraneHoist:c.Kind==NativeFactoryHazards.CueVentHiss?CueWindWarn:c.Kind==NativeFactoryHazards.CueVentBurst?CueWindGust:-1;
                if(kind<0)continue;var h=hazards[c.Hazard];var at=c.At;
                if(kind==CueWindWarn||kind==CueWindGust)at=new V3(h.a.x,h.a.y+1.4f,h.a.z);
                into[count++]=new FactoryCue{Kind=kind,Hazard=c.Hazard,Cycle=c.Cycle,Time=c.Time+fightAt+NativeFactoryHazards.ArmDelay,At=at};
            }
            return count;
        }
    }
}
