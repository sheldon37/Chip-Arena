using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Polaris Station (map `polar`, docs/arenas/POLAR.md): an arctic observatory under a moving aurora, rebuilt from scratch on
    /// 2026-11-11 (the M1 stage of the same id is gone from Core NativeNewStage*). Shared here: which map and look this is, the kit
    /// kinds its box kit and rig draw with (Core NativePolarKit; resolved per look from MapLooks.json `kitLooks`, "polar-kit-*" ids,
    /// an unlisted kind taking <see cref="Defaults"/>), the places the kit, the rig, the backdrop, the effects and the modelled art
    /// agree on (the sea's level, the great dish and how it turns and tilts, the icebreaker's searchlight, the weather balloon,
    /// the aurora's curtains) and the hazards' presentation as pure functions of the replicated state: the steam vents (kind `vent`
    /// with damage) and the thin ice (collapse segments), plus the vents' audio cues. Presentation only: none of this is sim data,
    /// collides, damages or enters the network frame. Pure and allocation free.
    /// </summary>
    public static class NativePolarLooks
    {
        public const string MapId="polar",Look="polar-aurora";
        /// <summary>Whether a map id is Polaris Station: every Polaris hook is inert on any other map.</summary>
        public static bool Active(string mapId)=>mapId==MapId;

        /// <summary>Kit kinds (index = kind), resolved once per look through `kitLooks`.</summary>
        public static readonly string[] Kinds={"polar-kit-snow","polar-kit-ice","polar-kit-ice-deep","polar-kit-crystal","polar-kit-hull","polar-kit-hull-dark","polar-kit-white","polar-kit-steel",
            "polar-kit-steel-dark","polar-kit-timber","polar-kit-timber-dark","polar-kit-window","polar-kit-lamp","polar-kit-red","polar-kit-orange","polar-kit-water","polar-kit-dish","polar-kit-glass",
            "polar-kit-tarp","polar-kit-balloon","polar-kit-vent","polar-kit-vent-hot","polar-kit-stripe","polar-kit-beacon","polar-kit-deck"};
        public const int Snow=0,Ice=1,IceDeep=2,Crystal=3,Hull=4,HullDark=5,White=6,Steel=7,SteelDark=8,Timber=9,TimberDark=10,Window=11,Lamp=12,Red=13,Orange=14,Water=15,Dish=16,Glass=17,
            Tarp=18,Balloon=19,Vent=20,VentHot=21,Stripe=22,Beacon=23,Deck=24;
        /// <summary>Each kind's look key and sRGB colour when the look does not list it.</summary>
        public static readonly (string look,int color)[] Defaults={("plain",0xEAF2FA),("plain",0xA9D4EC),("plain",0x4E8FB8),("cyber-tube",0x8FE8FF),("corrugated",0x9A2F26),("plain",0x1B1E26),
            ("plain",0xECECE6),("steel-green",0x7C8894),("steel-green",0x39424E),("timber",0x9A6A42),("timber",0x5C3E28),("window-lit",0xFFC88A),("neon",0xFFD9A0),("plain",0xC8382C),
            ("plain",0xE8862A),("plain",0x0C2A3C),("plain",0xE4E8EE),("window-lit",0x6FB4D8),("plain",0x3F6A5C),("plain",0xF4F0E6),("factory-grating",0x4A5560),("neon",0xFF7A30),
            ("demolition",0),("neon",0xFF3A2A),("timber",0x8A7256)};
        /// <summary>The kinds' looks for one look's `kitLooks` (an entry by its "polar-kit-*" id wins; else the default).</summary>
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
        public static double Hash(int a,int b=0,int c=0)=>NativeBrickfallScenery.Hash(a+9241,b+53,c+131);
        public static double Frac(double v)=>v-Math.Floor(v);

        // ---------------- the places the kit, rig, backdrop, effects and art share ----------------
        /// <summary>The sea's level (m): in the channel under the thin ice and past the shelf's south edge. A body dies at the
        /// map's killY (-3), just under it.</summary>
        public const double Sea=-1.3;
        /// <summary>The shelf's south edge (z; open to the sea) and its other sides (the pressure ridges' inner faces).</summary>
        public const double ShelfZ=-25,ShelfX=33,ShelfN=27;
        /// <summary>The channel the icebreaker cut: its half width and its ends (the bow, the road before the terrace).</summary>
        public const double ChannelHalf=2.6,ChannelN=7.8,ChannelS=-8.4;

        /// <summary>The great dish: its elevation bearing on top of the pedestal's yoke (the reflector tips about it), the
        /// reflector's radius and depth, the feed's distance from the vertex along the boresight, and the vertex's distance
        /// behind the bearing.</summary>
        public const double DishX=0,DishY=12.4,DishZ=-16.6,DishRadius=6.4,DishDepth=1.7,DishFeed=5.4,DishBack=.5;
        /// <summary>How the dish moves all match long: a slow steady turn (a round in 96 s) with a drift on it, and a tilt between
        /// 32 and 72 degrees over 74 s.</summary>
        public const double DishTurn=2*Math.PI/96,ElevationMid=.905,ElevationSwing=.35,ElevationRate=2*Math.PI/74;
        /// <summary>The dish's azimuth (rad about y, 0 looking north along +z) at field clock `t`.</summary>
        public static double DishAzimuth(double t)=>t*DishTurn+.3*Math.Sin(t*.047);
        /// <summary>The boresight's elevation over the horizon (rad) at `t`.</summary>
        public static double DishElevation(double t)=>ElevationMid+ElevationSwing*Math.Sin(t*ElevationRate+.6);
        /// <summary>The yoke's turn (about y) and the reflector's (the yoke's, then tipped up by the elevation): the reflector's own
        /// frame has its boresight along +z and its up along +y at the bearing.</summary>
        public static FactoryQ YokeTurn(double t)=>FactoryQ.Axis(0,1,0,DishAzimuth(t));
        public static FactoryQ DishAim(double t)=>FactoryQ.Axis(0,1,0,DishAzimuth(t))*FactoryQ.Axis(1,0,0,-DishElevation(t));
        /// <summary>The lowest the reflector's rim comes (m, world y) at any time: over the terrace (2.4) and the windbreak (5.0).</summary>
        public static double DishLowest(){double el=ElevationMid-ElevationSwing;return DishY-DishRadius*Math.Cos(el)+(DishDepth-DishBack)*Math.Sin(el);}

        /// <summary>The icebreaker's searchlight on the wheelhouse's roof (past the map's north bound): the lamp, and its beam's
        /// far end on the ice at `t` (it pans slowly across the field and back).</summary>
        public const double LampX=-3.4,LampY=14.2,LampZ=26.4,LampReach=64;
        public static void Searchlight(double t,out double x,out double y,out double z)
        {double yaw=.75*Math.Sin(t*.11)+.2*Math.Sin(t*.037),reach=30+14*Math.Sin(t*.071+1.3);x=LampX+Math.Sin(yaw)*reach;z=LampZ-Math.Cos(yaw)*reach;y=.05;}

        /// <summary>The weather balloon: one let go every <see cref="BalloonPeriod"/> s from behind the west ridge and the east in
        /// turn (outside the map), rising on the wind across the field and away. False while none is in the sky; `sway` the
        /// sonde's swing under it (rad).</summary>
        public const double BalloonPeriod=46,BalloonFlight=40,BalloonRise=2.3,BalloonRadius=1.5,SondeDrop=4.2;
        public static bool BalloonAt(double t,out double x,out double y,out double z,out double scale,out double sway)
        {
            x=y=z=sway=0;scale=1;if(!(t>=0))return false;double k=Math.Floor(t/BalloonPeriod),u=t-k*BalloonPeriod;if(u>BalloonFlight)return false;int side=((long)k&1)==0?-1:1;
            double n=Hash((int)(k%4096),7);x=side*(44-1.45*u)+Math.Sin(u*.31+n*6)*.9;y=1.5+BalloonRise*u;z=9+n*8-.22*u+Math.Sin(u*.23+n*4)*1.4;scale=1+u*.012;sway=.16*Math.Sin(u*1.3+n*5);return true;
        }

        // ---------------- the aurora ----------------
        /// <summary>The aurora's curtains: how many, the rays along each, and a ray of curtain `k` at parameter `f` (0..1 along it)
        /// at render time `t`: its foot (x, y, z), its height, its brightness 0..1 and its lean (m, the top's drift along the
        /// curtain). The curtains stand north of the map over the ice cap and arch over it; they sway, fold and pulse along their
        /// length, slowly and never still.</summary>
        public const int Curtains=4;
        public static void AuroraRay(int k,double f,double t,out double x,out double y,out double z,out double height,out double bright,out double lean)
        {
            // each curtain an arc round the map's north, nearer ones lower and wider; folds travel along it
            double dist=250+k*95,arc=2.5-k*.28,centre=(k%2==0?-.25:.3)+.1*Math.Sin(t*.011+k*1.9),a=centre-arc/2+arc*f;
            double fold=Math.Sin(f*11+t*.13+k*2.1)*22+Math.Sin(f*27-t*.21+k)*9+Math.Sin(f*5-t*.05)*30,r=dist+fold;
            x=Math.Sin(a)*r;z=Math.Cos(a)*r;y=70+k*16+Math.Sin(f*7+t*.09+k)*8;height=(85+k*26)*(.72+.28*Math.Sin(f*17+t*.17+k*3.3));
            double pulse=.5+.5*Math.Sin(f*13-t*.52+k*1.7),breathe=.6+.4*Math.Sin(f*4.1+t*.19+k*.8),edge=Math.Sin(Math.PI*f);
            bright=Math.Max(0,Math.Pow(edge,.6)*(.25+.75*pulse*breathe));lean=Math.Sin(f*9+t*.08+k)*14;
        }

        // ---------------- the steam vents (kind `vent` with damage) ----------------
        /// <summary>A vent's telegraph (0..1 through its warning, 0 outside it) and its burst's strength (0..1 while it scalds).</summary>
        public static void VentAt(MapHazard h,int round,double clock,out double warn,out double burst)
        {var st=NativeFactoryHazards.State(h,round,clock);warn=st.Phase==NativeFactoryHazards.Warning?Math.Max(.02,st.Warn):0;burst=st.Phase==NativeFactoryHazards.Bursting?Math.Max(.05,st.U):0;}

        // ---------------- the thin ice (collapse segments) ----------------
        /// <summary>A sheet's moment from its segment's replicated state (NativeStructureState: 0 whole, 1 cracking toward t0, 2
        /// sinking since t0, 3 gone) at field clock `now`: `crack` 0..1 (its seam broken, the cracks running), `sink` 0..1 through
        /// its fall, `open` whether the water stands open, `since` the seconds since it went (0 before).</summary>
        public static void SheetAt(StructureDefinition d,int state,double t0,double now,out double crack,out double sink,out bool open,out double since)
        {
            crack=sink=since=0;open=false;if(d==null||state<=0)return;double fall=Fall(d);
            if(state==1){crack=Math.Max(.05,Math.Min(1,1-(t0-now)/Math.Max(.1,d.telegraph)));return;}
            crack=1;sink=state>=3?1:Math.Max(0,Math.Min(1,(now-t0)/fall));open=true;since=Math.Max(0,now-t0);
        }

        /// <summary>How long a sheet takes to sink (s): NativeStructureRules.Duration's value, without its allocations.</summary>
        public static double Fall(StructureDefinition d)
        {
            double f=d.duration;if(d.poses!=null)for(int i=0;i<d.poses.Length;i++){var p=d.poses[i];f=Math.Max(f,p.delay+(d.mode=="pancake"&&p.angle==0?NativeStructureRules.DropDuration(p.drop,d.maxAccel):Math.Max(.01f,d.duration-p.delay)));}
            return Math.Max(.1,f);
        }

        // ---------------- audio cues ----------------
        public const int CueVentHiss=0,CueVentBurst=1;
        /// <summary>Every vent cue in (from, to] (seconds of match time) in round `round` with the fight from `fightAt`, into `into`
        /// (the count is returned). Pure and stateless: a caller that keeps its last time hears each cue once.</summary>
        public static int Cues(MapHazard[] hazards,int round,double fightAt,double from,double to,FactoryCue[] into,FactoryCue[] scratch)
        {
            int count=0;if(hazards==null||into==null||!(to>from))return 0;
            double c0=from-fightAt-NativeFactoryHazards.ArmDelay,c1=to-fightAt-NativeFactoryHazards.ArmDelay;int n=NativeFactoryHazards.Cues(hazards,round,c0,c1,scratch);
            for(int i=0;i<n&&count<into.Length;i++)
            {
                var c=scratch[i];int kind=c.Kind==NativeFactoryHazards.CueVentHiss?CueVentHiss:c.Kind==NativeFactoryHazards.CueVentBurst?CueVentBurst:-1;if(kind<0)continue;var h=hazards[c.Hazard];
                into[count++]=new FactoryCue{Kind=kind,Hazard=c.Hazard,Cycle=c.Cycle,Time=c.Time,At=new V3(h.a.x,h.a.y+1.2f,h.a.z)};
            }
            return count;
        }
    }
}
