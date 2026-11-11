using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Deepwell (map `rig`, docs/arenas/DEEPWELL.md): Deepwell Station, a research station on the ocean floor, rebuilt from scratch
    /// on 2026-11-11 (it was an offshore platform, one of the five M1 stages; the map keeps its id and name). Shared here: which
    /// map and look this is, the kit kinds its box kit and rig draw with (Core NativeDeepwellKit; resolved per look from
    /// MapLooks.json `kitLooks`, "rig-kit-*" ids, an unlisted kind taking <see cref="Defaults"/>), the sea outside as pure
    /// functions of the render clock (the whales, the jellyfish, the schools of fish, the kelp, the survey drone: they animate all
    /// match long and look the same on every client), the hazards' presentation poses as pure functions of the replicated state
    /// (the drill's piston, kind `press`; the bubbling vents, kind `vent` with a `wind` push) and their audio cues. Presentation
    /// only: none of this is sim data, collides, damages or enters the network frame. Pure and allocation free.
    /// </summary>
    public static class NativeDeepwellLooks
    {
        public const string MapId="rig",Look="rig-abyss";
        /// <summary>Whether a map id is Deepwell: every Deepwell hook is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==MapId;
        /// <summary>The blocks the kit draws in full (the glass: frames and mullions round a pane the effects glaze): the dressing
        /// leaves their own boxes undrawn while the kit draws (Runtime NativeArenaDressing, ArenaPresentation.DeepwellKitReady).</summary>
        public static bool KitBlock(string id)=>id!=null&&id.StartsWith("rig-glass-",StringComparison.Ordinal);

        /// <summary>Kit kinds (index = kind), resolved once per look through `kitLooks`.</summary>
        public static readonly string[] Kinds={"rig-kit-steel","rig-kit-steel-dark","rig-kit-brass","rig-kit-frame","rig-kit-lamp","rig-kit-cyan","rig-kit-core","rig-kit-stripe","rig-kit-rubber",
            "rig-kit-rock","rig-kit-sand","rig-kit-kelp","rig-kit-jelly","rig-kit-fish","rig-kit-whale","rig-kit-hull","rig-kit-red","rig-kit-coral","rig-kit-belly","rig-kit-screen"};
        public const int Steel=0,SteelDark=1,Brass=2,Frame=3,Lamp=4,Cyan=5,Core=6,Stripe=7,Rubber=8,Rock=9,Sand=10,Kelp=11,Jelly=12,Fish=13,Whale=14,Hull=15,Red=16,Coral=17,Belly=18,Screen=19;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it.</summary>
        public static readonly (string look,int color)[] Defaults={("cyber-metal",0x4E6470),("cyber-metal",0x26343C),("plain",0xC89A4A),("cyber-metal",0x5C7682),("neon",0xFFC27A),("neon",0x5FF0E0),
            ("cyber-tube",0xFF8A3A),("demolition",0),("plain",0x14191C),("plain",0x2C3438),("plain",0x4A5650),("plain",0x2E6A44),("cyber-tube",0xB8A0FF),("plain",0xB8C8D0),("plain",0x33485A),
            ("plain",0xE8E2C8),("neon",0xFF4A3A),("plain",0xC8607A),("plain",0xB8C4C8),("window-lit",0x58E0E8)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "rig-kit-*" id wins; else the default).</summary>
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
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+3301,b+149,c+211);
        public static double Frac(double v)=>v-Math.Floor(v);

        // ---------------- the places the kit, the rig, the effects and the modelled art share ----------------
        /// <summary>The seabed under and round the station (the station stands on legs over it), the moon pools' water (just
        /// under the bays' floor), the Well's dome (its glass springs from the wall top to the apex) and the wellhead's core.</summary>
        public const double Seabed=-9.0,Water=-1.95,DomeSpring=7.2,DomeApex=12.6,WellRadius=10.4,CoreY=-1.2,CoreRadius=1.5;
        /// <summary>The north moon pool (x0, x1, z0, z1); the south one is it turned half round.</summary>
        public const double PoolX0=-4,PoolX1=4,PoolZ0=13.6,PoolZ1=18.8;
        /// <summary>Whether a point in plan lies inside the station's hull (grown by `pad`): the sea's creatures, snow and
        /// bubbles keep out of it.</summary>
        public static bool Inside(double x,double z,double pad=0)
        {
            double ax=Math.Abs(x),az=Math.Abs(z);
            if(ax<10.4+pad&&az<10.4+pad)return true;                            // the Well
            if(ax<28.4+pad&&ax>17.6-pad&&az<9.4+pad)return true;                // the wings
            if(ax<18&&az<2.9+pad)return true;                                   // the spine
            if(ax<8.4+pad&&az<21.4+pad)return true;                             // the bays
            if(ax<24.9+pad&&ax>20.1-pad&&az<18+pad)return true;                 // the ring tunnels' runs outward
            if(ax<24.9+pad&&az<18+pad&&az>13.2-pad)return true;                 // and inward
            return false;
        }

        // ---------------- the whales ----------------
        public const int Whales=2;
        /// <summary>A whale's pose at render time `t`: it swims a slow oval round the station (the cow far out and high, her calf
        /// a length behind and inside her), so each great window sees it pass now and then. Position of its body's middle, its
        /// heading (rad, from +z toward +x), its pitch, the tail's stroke (rad, up and down) and its flippers' (rad).</summary>
        public static void WhaleAt(int k,double t,out double x,out double y,out double z,out double heading,out double pitch,out double tail,out double fin)
        {
            double period=132,lag=k==0?0:7.5,a=2*Math.PI*((t-lag)/period)+.6,rx=k==0?58:52,rz=k==0?46:41;
            Path(a,rx,rz,k,out x,out y,out z);Path(a+.01,rx,rz,k,out double x1,out double y1,out double z1);
            double dx=x1-x,dy=y1-y,dz=z1-z,h=Math.Sqrt(dx*dx+dz*dz);heading=Math.Atan2(dx,dz);pitch=Math.Atan2(dy,Math.Max(1e-6,h));
            double beat=t*(k==0?.95:1.5)+k*1.9;tail=Math.Sin(beat)*(k==0?.26:.34);fin=Math.Sin(beat*.5+.8)*.22;
        }
        private static void Path(double a,double rx,double rz,int k,out double x,out double y,out double z)
        {x=Math.Sin(a)*rx;z=Math.Cos(a)*rz;y=(k==0?9.5:6.0)+Math.Sin(a*2+.4+k)*3.2+Math.Sin(a*5+1.3)*.8;}
        /// <summary>A whale's length (m): the cow and her calf.</summary>
        public static double WhaleLength(int k)=>k==0?17:8.5;

        // ---------------- the jellyfish ----------------
        public const int Jellies=22;
        /// <summary>A jellyfish at render time `t`: its bell's middle, its pulse (0 relaxed .. 1 squeezed), its lean (the unit
        /// axis it swims along, mostly up), its size (the bell's radius, m) and its tint (0..2: violet, cyan, rose). They hang
        /// round the windows, each rising on its pulse and sinking between, drifting on a slow loop.</summary>
        public static void JellyAt(int i,double t,out double x,out double y,out double z,out double pulse,out double ax,out double ay,out double az,out double size,out int tint)
        {
            double h0=Hash(i,1),h1=Hash(i,2),h2=Hash(i,3),h3=Hash(i,4);tint=(int)(Hash(i,5)*3)%3;size=.38+.5*h3*h3;
            // home: a ring a few metres off the hull, bunched before the great windows (the bays' north and south, the wings' ends)
            double hx,hz;int place=i%6;
            if(place==0){hx=-34-8*h0;hz=-8+16*h1;}else if(place==1){hx=34+8*h0;hz=-8+16*h1;}
            else if(place==2){hx=-9+18*h0;hz=26+8*h1;}else if(place==3){hx=-9+18*h0;hz=-26-8*h1;}
            else{hx=((i>>1)%2==0?-1:1)*(14+(h0-.5)*.8);hz=(i%2==0?-1:1)*(6.2+(h1-.5)*.8);size=Math.Min(size,.6);}      // the four courts between the spine, the Well and the tunnels
            double drift=place<4?2.2:.8,weave=place<4?.5:.3;
            double rate=.42+.3*h2,ph=t*rate+h0*9,cycle=Frac(ph/(2*Math.PI));
            // the pulse: a quick squeeze, a slow relax; the rise follows it
            pulse=cycle<.28?Math.Sin(cycle/.28*Math.PI*.5):Math.Pow(1-(cycle-.28)/.72,1.6);
            double bob=.55*size*(pulse-.35);
            x=hx+Math.Sin(t*.07+h1*6)*drift+Math.Sin(t*.19+h2*5)*weave;z=hz+Math.Cos(t*.06+h0*7)*drift+Math.Sin(t*.17+h3*4)*weave;
            y=(place<2?3.6:place<4?2.6:4.2)+6.5*(h2-.5)+Math.Sin(t*.045+h3*6)*2.4+bob;
            ax=Math.Sin(t*.21+h1*5)*.2;az=Math.Cos(t*.18+h2*5)*.2;ay=Math.Sqrt(Math.Max(0,1-ax*ax-az*az));
        }

        // ---------------- the schools of fish ----------------
        public const int Schools=3,SchoolFish=44;
        /// <summary>A school's middle at render time `t`: each wheels on its own tilted loop past a great window.</summary>
        public static void School(int s,double t,out double x,out double y,out double z)
        {
            double a=t*(s==0?.20:s==1?-.17:.14)+s*2.1;
            if(s==0){x=-4+Math.Sin(a)*17;z=31+Math.Cos(a)*6.5;y=4.2+Math.Sin(a*2)*2.6;}                    // before the north bay's window
            else if(s==1){x=4+Math.Sin(a)*17;z=-31+Math.Cos(a)*6.5;y=4.0+Math.Sin(a*2+1)*2.6;}             // before the south bay's
            else{x=Math.Sin(a)*37;z=Math.Cos(a)*27;y=9.5+Math.Sin(a*3)*2.2;}                               // round the whole station, over the tunnels
        }
        /// <summary>Fish `i` of school `s`: where it is and its heading and pitch (it follows the school's path a little behind
        /// and beside its middle, weaving).</summary>
        public static void FishAt(int s,int i,double t,out double x,out double y,out double z,out double heading,out double pitch,out double wag)
        {
            double h0=Hash(s*97+i,11),h1=Hash(s*97+i,12),h2=Hash(s*97+i,13),lag=h0*2.6;
            At(s,i,t-lag,h0,h1,h2,out x,out y,out z);At(s,i,t-lag+.08,h0,h1,h2,out double x1,out double y1,out double z1);
            double dx=x1-x,dy=y1-y,dz=z1-z,h=Math.Sqrt(dx*dx+dz*dz);heading=Math.Atan2(dx,dz);pitch=Math.Atan2(dy,Math.Max(1e-6,h));wag=Math.Sin(t*(9+4*h1)+h2*6)*.35;
        }
        private static void At(int s,int i,double t,double h0,double h1,double h2,out double x,out double y,out double z)
        {
            School(s,t,out x,out y,out z);double sp=s==2?2.6:2.0,w=t*.8+h2*6;
            x+=(h1-.5)*2*sp+Math.Sin(w)*.35;y+=(h2-.5)*1.6*sp*.6+Math.Sin(w*1.3+h0*4)*.2;z+=(Hash(s*97+i,14)-.5)*2*sp+Math.Cos(w*.9)*.35;
        }

        // ---------------- the kelp ----------------
        public const int KelpStalks=30,KelpJoints=4;
        /// <summary>Kelp stalk `i`: its foot on the seabed, its height (m) and its turn about y (rad). The stalks stand in
        /// groves round the hull, clear of it.</summary>
        public static void KelpRoot(int i,out double x,out double z,out double height,out double turn)
        {
            double h0=Hash(i,21),h1=Hash(i,22),h2=Hash(i,23);int grove=i%5;
            double cx=grove==0?-36:grove==1?36:grove==2?-15:grove==3?15:0,cz=grove==0?13:grove==1?-13:grove==2?-27:grove==3?27:(i%2==0?33:-33);
            x=cx+(h0-.5)*9;z=cz+(h1-.5)*9;if(Inside(x,z,3)){x+=Math.Sign(x==0?1:x)*6;z+=Math.Sign(z==0?1:z)*6;}
            height=9+7*h2;turn=h0*2*Math.PI;
        }
        /// <summary>The lean (rad) of joint `k` (0 at the foot) of stalk `i` at render time `t`, about x and about z: each joint
        /// leans a little further than the one below it, on a swell that runs through the grove.</summary>
        public static void KelpSway(int i,int k,double t,out double leanX,out double leanZ)
        {
            double h=Hash(i,24),swell=t*.55-i*.37,amp=.05+.045*k;
            leanX=amp*Math.Sin(swell+k*.7+h*3)+.02*k;leanZ=amp*.8*Math.Sin(swell*.83+k*.6+h*5+1.2);
        }

        // ---------------- the survey drone ----------------
        /// <summary>The survey drone at render time `t`: it circles the Well's dome with its lamp ahead of it.</summary>
        public static void Drone(double t,out double x,out double y,out double z,out double heading)
        {
            double a=t*.11+2.2;x=Math.Sin(a)*19.5;z=Math.Cos(a)*15.5;y=10.2+Math.Sin(t*.23)*1.1;
            heading=Math.Atan2(Math.Cos(a)*19.5,-Math.Sin(a)*15.5);
        }

        // ---------------- the drill's piston (kind `press`) ----------------
        /// <summary>The piston's moment: `Warn` 0..1 through its telegraph (0 outside it), `Stroke` its travel (the press's own
        /// U: -.06 wound up .. 1 slammed), `Y` the ram's middle, `Since` seconds since its last strike (infinite before the first),
        /// `Held` while it stands on the wellhead, `Live` while it kills.</summary>
        public struct Piston {public double Warn,Stroke,Y,Since;public bool Held,Live,Moving;public int Cycle;}
        public static Piston PistonAt(MapHazard h,int round,double clock)
        {
            var st=NativeFactoryHazards.State(h,round,clock);var p=new Piston{Since=double.PositiveInfinity,Stroke=st.U,Y=st.Center.y,Cycle=st.Cycle,Live=st.Lethal,Held=st.Phase==NativeFactoryHazards.Holding,
                Moving=st.Phase==NativeFactoryHazards.Slamming||st.Phase==NativeFactoryHazards.Lifting};
            if(st.Phase==NativeFactoryHazards.Warning)p.Warn=st.Warn;else if(st.Phase!=NativeFactoryHazards.Parked)p.Warn=1;
            double open=Math.Max(0,h.period-h.warn-h.rise-h.dwell-h.travel),strike=open+h.warn+h.rise;
            if(NativeFactoryHazards.Cycle(h,clock,out int n,out double u)){if(u>=strike)p.Since=u-strike;else if(n>0)p.Since=u+h.period-strike;}
            return p;
        }
        /// <summary>The core's light (0 resting .. 1 at the strike): it breathes at rest, climbs through the telegraph and flares
        /// as the ram lands, then cools.</summary>
        public static double CoreLevel(in Piston p,double render)
        {
            double rest=.22+.06*Math.Sin(render*1.3),warn=p.Warn*p.Warn*.6,flash=double.IsInfinity(p.Since)?0:Math.Exp(-p.Since*1.4);
            return Math.Min(1,rest+warn+flash*.9);
        }

        // ---------------- the bubbling vents (kind `vent` with a `wind` push) ----------------
        /// <summary>A vent's telegraph (0..1, 0 outside it), its blast (0..1 while it blows) and the seconds since it ended.</summary>
        public static void VentAt(MapHazard h,int round,double clock,out double warn,out double blast,out double after)
        {
            var st=NativeFactoryHazards.State(h,round,clock);warn=st.Phase==NativeFactoryHazards.Warning?st.Warn:0;blast=st.Phase==NativeFactoryHazards.Bursting?st.U:0;after=double.PositiveInfinity;
            if(st.Phase==NativeFactoryHazards.Parked&&NativeFactoryHazards.Cycle(h,clock,out int n,out double u)&&n>0)after=u;
        }

        // ---------------- audio cues ----------------
        public const int CuePistonWarn=0,CuePistonSlam=1,CueVentWarn=2,CueVentBlast=3;
        /// <summary>
        /// Every cue in (from, to] (seconds of match time) for these hazards in round `round` with the fight from `fightAt`, into
        /// `into` (the count is returned). Pure and stateless: a caller that keeps its last time hears each cue once. They are
        /// NativeFactoryHazards.Cues on the hazard clock: the press's hiss and slam, the vent's hiss and burst.
        /// </summary>
        public static int Cues(MapHazard[] hazards,int round,double fightAt,double from,double to,FactoryCue[] into,FactoryCue[] scratch)
        {
            int count=0;if(hazards==null||into==null||!(to>from))return 0;
            double c0=from-fightAt-NativeFactoryHazards.ArmDelay,c1=to-fightAt-NativeFactoryHazards.ArmDelay;
            int n=NativeFactoryHazards.Cues(hazards,round,c0,c1,scratch);
            for(int i=0;i<n&&count<into.Length;i++)
            {
                var c=scratch[i];int kind=c.Kind==NativeFactoryHazards.CuePressHiss?CuePistonWarn:c.Kind==NativeFactoryHazards.CuePressSlam?CuePistonSlam:c.Kind==NativeFactoryHazards.CueVentHiss?CueVentWarn:c.Kind==NativeFactoryHazards.CueVentBurst?CueVentBlast:-1;
                if(kind<0)continue;var h=hazards[c.Hazard];
                into[count++]=new FactoryCue{Kind=kind,Hazard=c.Hazard,Cycle=c.Cycle,Time=c.Time,At=kind<=CuePistonSlam?new V3(h.b.x,h.b.y-h.size.y/2+1,h.b.z):new V3(h.a.x,h.a.y+1.2f,h.a.z)};
            }
            return count;
        }
    }
}
