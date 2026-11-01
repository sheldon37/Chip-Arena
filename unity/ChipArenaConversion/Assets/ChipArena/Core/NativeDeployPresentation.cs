using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Expedition Deployment Station's presentation rules (docs/EXPEDITION-DEPLOYMENT.md "Presentation"): how the two
    // machines, the holo map and the Deploy console read Astra's pre-run authority (NativeExpeditionDeployment). Presentation
    // only and engine-free: nothing here rolls, decides or writes state. Every client derives the same pictures from the same
    // replicated ExpeditionDeploymentState plus its own local clock since it saw a roll (a late joiner sees the result landed).
    //  - Reels always come to rest on the authority's result: the instance machine's three reels on the three districts
    //    (east, west, south), the mutator machine's on the rolled ids. They spin from where the last roll left them.
    //  - The mutator machine never shows a mutator as active before the host applies it at Deploy: rolled ids read PENDING.
    //  - Deploy darkens and clamps both machines, freezes the map and opens the gate; the travel hand-off follows.
    //  - The holo map and the world's theme blend sample the blueprint's cell influences bilinearly, so districts fade into
    //    one another with no seam between cells.
    public static class NativeDeployPresentation
    {
        // ---- display tables -----------------------------------------------------------------------------------------------
        public readonly struct Destination
        {
            public readonly string Id,Name,Tag,Emblem;public readonly int Color;
            public Destination(string id,string name,string tag,string emblem,int color){Id=id;Name=name;Tag=tag;Emblem=emblem;Color=color;}
        }
        // The authored instances (ExpeditionLayout areas) and the themes the authority carries; an unknown id falls back to its
        // theme's look, then to a neutral one, so a new instance never draws blank.
        private static readonly Destination[] KnownDestinations=
        {
            new Destination("exp-ward-zero","WARD ZERO","Quarantine wing","biohazard",0x8dff5a),
            new Destination("exp-transit","CENTRAL STATION","Transit concourse","rail",0x3fd8ff),
            new Destination("exp-foundry","CITY IRONWORKS","Foundry floor","anvil",0xff8a2a),
        };
        private static readonly Dictionary<string,(string tag,string emblem,int color)> Themes=new Dictionary<string,(string,string,int)>(StringComparer.Ordinal)
        {
            {"quarantine",("Quarantine wing","biohazard",0x8dff5a)},{"rail",("Transit concourse","rail",0x3fd8ff)},
            {"industrial",("Foundry floor","anvil",0xff8a2a)},{"catacombs",("Catacombs","crypt",0xb08cff)},
        };
        public const int NeutralColor=0x3a4150,UnknownColor=0x9aa3b2,BossColor=0xff3048;
        public static Destination DestinationOf(string id,string theme)
        {
            foreach(var d in KnownDestinations)if(d.Id==id)return d;
            string name=string.IsNullOrEmpty(id)?"UNKNOWN":id.Replace("exp-","").Replace('-',' ').ToUpperInvariant();
            if(theme!=null&&Themes.TryGetValue(theme,out var t))return new Destination(id,name,t.tag,t.emblem,t.color);
            return new Destination(id,name,"Uncharted","unknown",UnknownColor);
        }

        public enum Polarity{Boon,Bane,Chaos,Unknown}
        public readonly struct Mutator
        {
            public readonly string Id,Name,Line;public readonly Polarity Polarity;
            public Mutator(string id,string name,string line,Polarity polarity){Id=id;Name=name;Line=line;Polarity=polarity;}
            public int Color=>PolarityColor(Polarity);
        }
        // Display names and tone only: no mutator gameplay is agreed (docs/EXPEDITION-DEPLOYMENT.md). The lines describe the
        // fiction, never numbers; Astra's catalog decides what each id does. Unknown ids read as UNKNOWN, chaos-toned.
        private static readonly Mutator[] KnownMutators=
        {
            new Mutator("surge","SURGE","Everything moves faster.",Polarity.Chaos),
            new Mutator("storm","STORM","The weather turns on you.",Polarity.Chaos),
            new Mutator("darkness","DARKNESS","The city's lights fail.",Polarity.Bane),
            new Mutator("brittle","BRITTLE","Cover breaks easily.",Polarity.Bane),
            new Mutator("windfall","WINDFALL","Caches run deeper.",Polarity.Boon),
            new Mutator("second-wind","SECOND WIND","Revives come quicker.",Polarity.Boon),
        };
        public static Mutator MutatorOf(string id)
        {
            foreach(var m in KnownMutators)if(m.Id==id)return m;
            return new Mutator(id,"UNKNOWN","An untested anomaly.",Polarity.Unknown);
        }
        public static int PolarityColor(Polarity p)=>p==Polarity.Boon?0x5dff9a:p==Polarity.Bane?0xff4d5e:p==Polarity.Chaos?0xc77dff:UnknownColor;
        public static string PolarityLabel(Polarity p)=>p==Polarity.Boon?"BOON":p==Polarity.Bane?"BANE":p==Polarity.Chaos?"CHAOS":"UNKNOWN";

        // ---- reels --------------------------------------------------------------------------------------------------------
        public const int Reels=3;
        public const float SpinUp=.3f,Decel=.6f,FirstStop=1.6f,Stagger=.45f,Overshoot=.22f,Settle=.28f,MinCruise=11f;
        /// <summary>When reel i comes to rest after the roll was seen (it reaches the overshoot then settles back).</summary>
        public static float StopTime(int reel)=>FirstStop+reel*Stagger;
        public static float RollDuration=>StopTime(Reels-1)+Settle;
        public struct ReelPose{public float Offset,Speed;public bool Landed;}
        private static int Mod(int a,int n)=>((a%n)+n)%n;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        /// <summary>
        /// A reel's strip position (in symbols; symbol k is centred in the window when Offset is k mod the strip length) a time
        /// <paramref name="since"/> after the roll was seen, spinning forward from <paramref name="from"/> to rest on
        /// <paramref name="land"/>. Spin up over SpinUp, cruise, ease out to the overshoot at the reel's stop time and settle
        /// back onto the symbol. An unknown roll time (negative or NaN: the result was already there) is the landed pose.
        /// </summary>
        public static ReelPose Reel(int reel,float since,int from,int land,int strip)
        {
            int n=Math.Max(1,strip);land=Mod(land,n);from=Mod(from,n);
            float stop=StopTime(reel);
            if(!(since>=0)||since>=stop+Settle)return new ReelPose{Offset=land,Speed=0,Landed=true};
            // forward travel: at least a cruise's worth, whole turns of the strip, ending exactly on the result
            int travel=Mod(land-from,n);float need=MinCruise*(stop-SpinUp-Decel)+MinCruise*(SpinUp/2+Decel/3);
            while(travel<need)travel+=n;
            float total=travel+Overshoot,cruise=stop-SpinUp-Decel;
            float v=total/(SpinUp/2+cruise+Decel/3);
            float p,speed;
            if(since<SpinUp){float u=since/SpinUp;p=v*SpinUp*u*u/2;speed=v*u;}
            else if(since<SpinUp+cruise){p=v*SpinUp/2+v*(since-SpinUp);speed=v;}
            else if(since<stop){float u=(since-SpinUp-cruise)/Decel;float k=1-u;p=v*SpinUp/2+v*cruise+v*Decel/3*(1-k*k*k);speed=v*k*k;}
            else{float u=(since-stop)/Settle;p=total-Overshoot*Smooth(u);speed=0;}
            return new ReelPose{Offset=from+p,Speed=speed,Landed=false};
        }
        /// <summary>The symbols on reel i, as indices into the sorted catalog: every entry once, rotated per reel so the three
        /// reels never show the same row while spinning.</summary>
        public static string[] Strip(IEnumerable<string> catalog,int reel)
        {
            var ids=(catalog??Array.Empty<string>()).Where(x=>!string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(ids.Length==0)return ids;int r=Mod(reel*(ids.Length/3+1),ids.Length);
            return ids.Skip(r).Concat(ids.Take(r)).ToArray();
        }
        /// <summary>Where <paramref name="id"/> sits on a strip (appending it when the strip lacks it, so a result the
        /// presentation does not know still lands).</summary>
        public static int Slot(ref string[] strip,string id)
        {
            int i=Array.IndexOf(strip,id);if(i>=0)return i;
            strip=strip.Concat(new[]{id}).ToArray();return strip.Length-1;
        }
        /// <summary>The lever's pull (0 up, 1 down) a time after the roll was seen: down in .2 s, back up by .8 s.</summary>
        public static float Lever(float since)=>!(since>=0)?0:since<.2f?Smooth(since/.2f):1-Smooth((since-.2f)/.6f);

        // ---- machines and the console ----------------------------------------------------------------------------------------
        public enum Phase{Dormant,Spinning,Landed,Unavailable,Optional,Pending,Skipped,Active,Locked}
        public struct MachineLook{public Phase Phase;public float Power;public bool Usable;public string Banner;}
        /// <summary>The instance machine: dormant until the first roll, spinning for RollDuration after a roll is seen, landed,
        /// and dark and clamped once deployed (its result stays legible on the dimmed reels).</summary>
        public static MachineLook InstanceMachine(ExpeditionDeploymentState s,float sinceRoll,float sinceDeploy,bool host)
        {
            if(s==null)return new MachineLook{Phase=Phase.Unavailable,Power=.2f,Banner="STANDING BY"};
            if(s.deployed)return new MachineLook{Phase=Phase.Locked,Power=1-.8f*Deploying(sinceDeploy).Dim,Banner="DESTINATIONS LOCKED"};
            if(!s.worldRolled)return new MachineLook{Phase=Phase.Dormant,Power=.6f,Usable=host,Banner=host?"PULL TO ROLL DESTINATIONS":"WAITING FOR THE HOST TO ROLL"};
            if(sinceRoll>=0&&sinceRoll<RollDuration)return new MachineLook{Phase=Phase.Spinning,Power=1,Banner="ROLLING DESTINATIONS"};
            return new MachineLook{Phase=Phase.Landed,Power=1,Usable=host,Banner=host?"DESTINATIONS SET · PULL TO REROLL":"DESTINATIONS SET"};
        }
        /// <summary>The mutator machine: unavailable before the first destination roll, optional (skip is fine) until rolled,
        /// spinning, then PENDING (never active before Deploy), skipped, and at Deploy either ACTIVE (its cards stay lit while
        /// the cabinet darkens) or simply locked.</summary>
        public static MachineLook MutatorMachine(ExpeditionDeploymentState s,float sinceRoll,float sinceDeploy,bool host)
        {
            if(s==null)return new MachineLook{Phase=Phase.Unavailable,Power=.2f,Banner="STANDING BY"};
            float dark=1-.8f*Deploying(sinceDeploy).Dim;
            if(s.deployed)return s.mutators.Length>0?new MachineLook{Phase=Phase.Active,Power=dark,Banner="MUTATORS ACTIVE"}:new MachineLook{Phase=Phase.Locked,Power=dark,Banner="NO MUTATORS"};
            if(!s.worldRolled)return new MachineLook{Phase=Phase.Unavailable,Power=.25f,Banner="ROLL DESTINATIONS FIRST"};
            if(s.mutatorRolls==0)return new MachineLook{Phase=Phase.Optional,Power=.7f,Usable=host,Banner=host?"OPTIONAL · ROLL, OR DEPLOY WITHOUT":"OPTIONAL · HOST MAY ROLL"};
            if(s.mutators.Length==0)return new MachineLook{Phase=Phase.Skipped,Power=.6f,Usable=host,Banner="SKIPPED · NO MUTATORS"};
            if(sinceRoll>=0&&sinceRoll<RollDuration)return new MachineLook{Phase=Phase.Spinning,Power=1,Banner="ROLLING MUTATORS"};
            return new MachineLook{Phase=Phase.Pending,Power=1,Usable=host,Banner="PENDING · APPLIED AT DEPLOY"};
        }
        public enum Launch{NeedDestination,WaitingParty,Ready,HostOnly,Launching}
        public static Launch DeployConsole(ExpeditionDeploymentState s,bool partyReady,bool host)
        {
            if(s!=null&&s.deployed)return Launch.Launching;
            if(s==null||!s.worldRolled)return Launch.NeedDestination;
            if(!partyReady)return Launch.WaitingParty;
            return host?Launch.Ready:Launch.HostOnly;
        }
        public static string ConsoleLabel(Launch l)=>l==Launch.NeedDestination?"ROLL DESTINATIONS TO DEPLOY":l==Launch.WaitingParty?"WAITING FOR THE PARTY":
            l==Launch.Ready?"DEPLOY":l==Launch.HostOnly?"WAITING FOR THE HOST":"DEPLOYING";

        // ---- the deploy sequence ------------------------------------------------------------------------------------------------
        public const float TravelAt=4f;
        public struct DeployPose{public float Dim,Clamp,Gate,Lift,Fade;public bool Travel;}
        /// <summary>After Deploy: the machines dim (.6 s) and clamp (.2–.5 s), the gate opens (.8–2.2 s), the lift carries the
        /// party out (2.2–3.6 s), the screen fades (3.4–4 s) and the travel hand-off begins at 4 s. Before Deploy, all zero.</summary>
        public static DeployPose Deploying(float since)
        {
            if(!(since>=0))return default;
            return new DeployPose{Dim=Smooth(since/.6f),Clamp=Smooth((since-.2f)/.3f),Gate=Smooth((since-.8f)/1.4f),Lift=Smooth((since-2.2f)/1.4f),
                Fade=Smooth((since-3.4f)/.6f),Travel=since>=TravelAt};
        }

        // ---- the blueprint: theme weights and the holo map ------------------------------------------------------------------------
        public const float CellMetres=NativeExpeditionDeployment.CellMetres;
        public static readonly int HalfX=NativeExpeditionDeployment.GridWidth/2,HalfZ=NativeExpeditionDeployment.GridHeight/2;
        public static float WorldHalfX=>(HalfX+.5f)*CellMetres;
        public static float WorldHalfZ=>(HalfZ+.5f)*CellMetres;
        /// <summary>The final boss's gate on the map: the north edge's centre (the generator's temporary anchor).</summary>
        public static Vector2 BossAnchor=>new Vector2(0,HalfZ*CellMetres);
        private static ExpeditionWorldCell CellAt(ExpeditionDeploymentState s,int x,int z)
        {
            x=Math.Max(-HalfX,Math.Min(HalfX,x));z=Math.Max(-HalfZ,Math.Min(HalfZ,z));
            int i=(x+HalfX)*NativeExpeditionDeployment.GridHeight+(z+HalfZ);
            return s.cells!=null&&i>=0&&i<s.cells.Length?s.cells[i]:null;
        }
        /// <summary>
        /// The theme blend at a world point (metres, the blueprint's frame: cell (x,z) centred at x*10, z*10): the four
        /// influences [neutral, district 0, 1, 2] of the four nearest cell centres, smoothstep-weighted, normalised to sum 1.
        /// Continuous everywhere, so dressing driven by it fades between districts with no seam at cell borders.
        /// </summary>
        public static float[] Weights(ExpeditionDeploymentState s,float wx,float wz)
        {
            var w=new float[4];if(s==null||s.cells==null||s.cells.Length==0){w[0]=1;return w;}
            float fx=wx/CellMetres,fz=wz/CellMetres;int x0=(int)Math.Floor(fx),z0=(int)Math.Floor(fz);float tx=Smooth(fx-x0),tz=Smooth(fz-z0);
            void Add(int x,int z,float k){var c=CellAt(s,x,z);if(c==null||c.influences==null||c.influences.Length<4)return;for(int i=0;i<4;i++)w[i]+=k*c.influences[i];}
            Add(x0,z0,(1-tx)*(1-tz));Add(x0+1,z0,tx*(1-tz));Add(x0,z0+1,(1-tx)*tz);Add(x0+1,z0+1,tx*tz);
            float sum=w[0]+w[1]+w[2]+w[3];if(sum<=0){w[0]=1;return w;}
            for(int i=0;i<4;i++)w[i]/=sum;return w;
        }
        /// <summary>How much of a road the point is on (0..1), smooth like the weights.</summary>
        public static float Road(ExpeditionDeploymentState s,float wx,float wz)
        {
            if(s==null||s.cells==null)return 0;float fx=wx/CellMetres,fz=wz/CellMetres;int x0=(int)Math.Floor(fx),z0=(int)Math.Floor(fz);float tx=Smooth(fx-x0),tz=Smooth(fz-z0);
            float R(int x,int z){var c=CellAt(s,x,z);return c!=null&&c.road?1:0;}
            return R(x0,z0)*(1-tx)*(1-tz)+R(x0+1,z0)*tx*(1-tz)+R(x0,z0+1)*(1-tx)*tz+R(x0+1,z0+1)*tx*tz;
        }
        /// <summary>The district colours [neutral, district 0, 1, 2] for a state.</summary>
        public static int[] Palette(ExpeditionDeploymentState s)
        {
            var p=new int[4];p[0]=NeutralColor;
            for(int i=0;i<3;i++){var d=s?.districts!=null&&i<s.districts.Length?s.districts[i]:null;p[i+1]=d==null?UnknownColor:DestinationOf(d.instance,d.theme).Color;}
            return p;
        }
        private static int Lerp(int a,int b,float t){int C(int sh)=>(int)Math.Round(((a>>sh)&255)+(((b>>sh)&255)-((a>>sh)&255))*Clamp01(t));return (C(16)<<16)|(C(8)<<8)|C(0);}
        /// <summary>
        /// The holo map, width x height ARGB pixels, row 0 the north edge, column 0 the west edge, covering the blueprint's
        /// whole footprint: district colours blended by Weights (dark, so it reads as a map), roads brighter, the neutral plaza
        /// ring, each entrance as a ring in its colour, the boss gate as a red diamond on the north edge. A state without a
        /// world roll is an empty grid.
        /// </summary>
        public static int[] Preview(ExpeditionDeploymentState s,int width,int height)
        {
            var px=new int[Math.Max(1,width)*Math.Max(1,height)];var pal=Palette(s);bool world=s!=null&&s.worldRolled&&s.cells!=null&&s.cells.Length>0;
            float hx=WorldHalfX,hz=WorldHalfZ;
            for(int j=0;j<height;j++)for(int i=0;i<width;i++)
            {
                float wx=-hx+(i+.5f)/width*2*hx,wz=hz-(j+.5f)/height*2*hz;int col;
                bool grid=Math.Abs(((wx+hx)%CellMetres+CellMetres)%CellMetres)<CellMetres*.06f||Math.Abs(((wz+hz)%CellMetres+CellMetres)%CellMetres)<CellMetres*.06f;
                if(!world)col=grid?0x1c2531:0x0d1219;
                else
                {
                    var w=Weights(s,wx,wz);float r=0,g=0,b=0;
                    for(int k=0;k<4;k++){r+=w[k]*((pal[k]>>16)&255);g+=w[k]*((pal[k]>>8)&255);b+=w[k]*(pal[k]&255);}
                    col=((int)(r*.42f)<<16)|((int)(g*.42f)<<8)|(int)(b*.42f);
                    float road=Road(s,wx,wz);if(road>.5f)col=Lerp(col,0xd7e3f0,.18f+.5f*Smooth((road-.5f)/.35f));
                    if(grid)col=Lerp(col,0x000000,.25f);
                    float rad=(float)Math.Sqrt(wx*wx+wz*wz);if(Math.Abs(rad-24f)<1.4f)col=Lerp(col,0xe8f1ff,.7f);
                    for(int d=0;d<3&&d<(s.districts?.Length??0);d++)
                    {
                        var dd=s.districts[d];float ex=dd.x*CellMetres,ez=dd.z*CellMetres;float dist=(float)Math.Sqrt((wx-ex)*(wx-ex)+(wz-ez)*(wz-ez));
                        if(dist<4.5f)col=Lerp(col,pal[d+1],dist<3?1:.85f);else if(dist<6)col=Lerp(col,0xffffff,.8f);
                    }
                    var bo=BossAnchor;if(Math.Abs(wx-bo.X)+Math.Abs(wz-bo.Y)<7)col=Lerp(col,BossColor,.95f);
                }
                px[j*width+i]=unchecked((int)0xff000000)|col;
            }
            return px;
        }

        // ---- the station set (the station's own frame: metres, x right, z away from the default camera, y up) -----------------
        public const int Pads=8;
        public static readonly Vector3 Table=new Vector3(0,0,2f),InstanceMachineAt=new Vector3(-5.6f,0,3.6f),MutatorMachineAt=new Vector3(5.6f,0,3.6f),Gate=new Vector3(0,0,9f);
        /// <summary>The machines face the table (instance machine's yaw +55 degrees, mutator's -55; 0 faces -z, the camera).</summary>
        public const float InstanceYaw=55f,MutatorYaw=-55f,TableRadius=1.6f,PadArc=4.4f;
        /// <summary>Pad i of the party arc in front of the table, the host on the centre pad, the rest alternating outward.</summary>
        public static Vector3 Pad(int i)
        {
            int k=Math.Max(0,Math.Min(Pads-1,i));int side=k==0?0:(k%2==1?-1:1);int step=(k+1)/2;
            double a=side*step*(140.0/(Pads-1))*Math.PI/180;
            return Table+new Vector3((float)(PadArc*Math.Sin(a)),0,(float)(-PadArc*Math.Cos(a)));
        }
        public enum Shot{Overview,InstanceMachine,MutatorMachine,Map,Deploy}
        public struct View{public Vector3 Eye,Target;public float Fov;}
        public static View ShotOf(Shot shot)
        {
            switch(shot)
            {
                case Shot.InstanceMachine:return new View{Eye=new Vector3(-2.4f,2.3f,.6f),Target=InstanceMachineAt+new Vector3(0,1.5f,0),Fov=42};
                case Shot.MutatorMachine:return new View{Eye=new Vector3(2.4f,2.3f,.6f),Target=MutatorMachineAt+new Vector3(0,1.5f,0),Fov=42};
                case Shot.Map:return new View{Eye=new Vector3(0,4.6f,-1.2f),Target=Table+new Vector3(0,1,0),Fov=46};
                case Shot.Deploy:return new View{Eye=new Vector3(0,3.3f,-6.2f),Target=Gate+new Vector3(0,2.5f,0),Fov=46};
                default:return new View{Eye=new Vector3(0,5.1f,-9.2f),Target=new Vector3(0,1.5f,3.6f),Fov=48};
            }
        }
        /// <summary>The shot to frame: the machine that is spinning, the gate while deploying, else the overview.</summary>
        public static Shot Framing(MachineLook instance,MachineLook mutator,ExpeditionDeploymentState s)
        {
            if(s!=null&&s.deployed)return Shot.Deploy;
            if(instance.Phase==Phase.Spinning)return Shot.InstanceMachine;
            if(mutator.Phase==Phase.Spinning)return Shot.MutatorMachine;
            return Shot.Overview;
        }
    }
}
