using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // The Expedition hub (Claude, 2026-10-15; docs/EXPEDITION-HUB.md): the town's fenced sanctuary, its four animated gates, the
    // ward at the fence line, and the wilds. This file is presentation and layout data only: the gate records the manifest's
    // additive `hub` field carries (exp-layout-3), the proposed replicated gate state, the gates' eased motion, their models,
    // dust and ward shimmer, their sound cue schedule and the DEV preview's synthetic state. Nothing here opens a gate,
    // collides, spawns or pays: a gate's collision, interaction, auto-close and the sanctuary rules are the director's
    // (Astra's) to implement against the contract in the doc. Engine-free and allocation-free per frame once warm.

    /// <summary>A hub gate in the fence (manifest `hub.gates[]`): its centre on the fence plane, the way out of the sanctuary
    /// (`Yaw`, degrees, 0 faces +z), its opening, the closure the director's collider fills, the interaction volume (both
    /// sides), the auto-close sensor, the threshold points either side and the two fence blocks that flank it.</summary>
    public sealed class ExpeditionHubGate
    {
        public string Id,Kind,Motion,Side;public float X,Y,Z,Yaw,Width,Height;public ExpeditionVolume Closure,Interact,Sensor;public ExpeditionPoint Inside,Outside;public string[] Fence=Array.Empty<string>();
        public bool Wood=>Kind=="wood";public bool Slides=>Motion=="slide";
    }
    /// <summary>A hub spawn zone: its ring (near, mid, far; the doc proposes each ring's density, money and kinds), its volume,
    /// the town spawn records (`e`) whose exits lie in it, and the walking distance (m) from the nearest gate to its nearest and
    /// farthest spawn.</summary>
    public sealed class ExpeditionHubZone {public string Id,Ring;public float Near,Far;public ExpeditionVolume Volume;public int[] Spawns=Array.Empty<int>();}
    /// <summary>A route from a gate to an instance door's approach (or a reserved doorway's), walked on the floor.</summary>
    public sealed class ExpeditionHubRoute {public string To,Gate;public bool Reserved;public float Path,Straight,Run,Sprint,X,Z,Yaw;public ExpeditionPoint Approach;}
    /// <summary>A fence section block and its kind and gate.</summary>
    public sealed class ExpeditionHubFence {public string Block,Kind,Side,Gate;}
    /// <summary>A wilds district (darker, foggier; the far ring).</summary>
    public sealed class ExpeditionHubWilds {public string Id,Side;public ExpeditionVolume Volume;}

    /// <summary>The town area's `hub` record (exp-layout-3). Additive: the loader (NativeExpeditionLayout) never reads it.</summary>
    public sealed class ExpeditionHubLayout
    {
        public string Area;public float FenceHeight=3.2f,FenceThickness=.4f;
        public ExpeditionHubFence[] Fence=Array.Empty<ExpeditionHubFence>();public ExpeditionHubGate[] Gates=Array.Empty<ExpeditionHubGate>();
        public ExpeditionHubZone[] Zones=Array.Empty<ExpeditionHubZone>();public ExpeditionHubWilds[] Wilds=Array.Empty<ExpeditionHubWilds>();public ExpeditionHubRoute[] Routes=Array.Empty<ExpeditionHubRoute>();
        /// <summary>The gates' timing (s; constants of the contract, docs/EXPEDITION-HUB.md, not manifest data): a full opening and
        /// a full closing as drawn, the proposed auto-close delay once the sensor is empty, and the proposed collider moments
        /// (released this far into an opening, blocking this far into a closing). The aprons are the manifest's.</summary>
        public float Open=.9f,Close=1f,AutoClose=2.5f,Release=.5f,Block=.65f,SpawnApron=10,ZoneApron=8;
        public ExpeditionHubGate Gate(string id){foreach(var g in Gates)if(g.Id==id)return g;return null;}
        public bool InWilds(double x,double z){foreach(var w in Wilds)if(w.Volume!=null&&w.Volume.Contains(new V3((float)x,w.Volume.y,(float)z)))return true;return false;}

        private static Dictionary<string,object> O(object x)=>x as Dictionary<string,object>??throw new FormatException("Expedition hub: expected object.");
        private static List<object> A(Dictionary<string,object> o,string k)=>o.TryGetValue(k,out var v)&&v is List<object> l?l:new List<object>();
        private static string S(Dictionary<string,object> o,string k)=>o.TryGetValue(k,out var v)&&v is string s?s:throw new FormatException("Expedition hub: missing "+k);
        private static float F(Dictionary<string,object> o,string k)
        {if(!o.TryGetValue(k,out var v)||!(v is double d)||double.IsNaN(d)||double.IsInfinity(d)||Math.Abs(d)>100000)throw new FormatException("Expedition hub: invalid "+k);return (float)d;}
        private static ExpeditionVolume V(object x){var o=O(x);var v=new ExpeditionVolume{x=F(o,"x"),y=F(o,"y"),z=F(o,"z"),sx=F(o,"sx"),sy=F(o,"sy"),sz=F(o,"sz"),yaw=F(o,"yaw")};if(!(v.sx>0&&v.sy>0&&v.sz>0))throw new FormatException("Expedition hub: empty volume.");return v;}
        private static ExpeditionPoint P(object x){var o=O(x);return new ExpeditionPoint{x=F(o,"x"),y=F(o,"y"),z=F(o,"z")};}
        /// <summary>The `hub` record of area `area` in the manifest text, or null when the manifest has none (exp-layout-2 and
        /// earlier). Throws FormatException on a malformed record.</summary>
        public static ExpeditionHubLayout Parse(string manifest,string area="exp-city")
        {
            if(string.IsNullOrEmpty(manifest))return null;var root=O(NativeSoundJson.Parse(manifest));
            foreach(var ao in A(root,"areas"))
            {
                var a=O(ao);if(!(a.TryGetValue("id",out var id)&&id as string==area))continue;if(!a.TryGetValue("hub",out var hx)||hx==null)return null;var h=O(hx);
                var l=new ExpeditionHubLayout{Area=area};
                if(h.TryGetValue("fence",out var fx)){var f=O(fx);l.FenceHeight=F(f,"height");l.FenceThickness=F(f,"thickness");var segs=new List<ExpeditionHubFence>();foreach(var s in A(f,"segments")){var so=O(s);segs.Add(new ExpeditionHubFence{Block=S(so,"block"),Kind=S(so,"kind"),Side=S(so,"side"),Gate=S(so,"gate")});}l.Fence=segs.ToArray();}
                var gates=new List<ExpeditionHubGate>();
                foreach(var gx in A(h,"gates"))
                {
                    var g=O(gx);var fence=new List<string>();foreach(var b in A(g,"fence"))if(b is string bs)fence.Add(bs);
                    var gate=new ExpeditionHubGate{Id=S(g,"id"),Kind=S(g,"kind"),Motion=S(g,"motion"),Side=S(g,"side"),X=F(g,"x"),Y=F(g,"y"),Z=F(g,"z"),Yaw=F(g,"yaw"),Width=F(g,"width"),Height=F(g,"height"),
                        Closure=V(g["closure"]),Interact=V(g["interact"]),Sensor=V(g["sensor"]),Inside=P(g["inside"]),Outside=P(g["outside"]),Fence=fence.ToArray()};
                    if(!ExpeditionValidation.Id(gate.Id)||gate.Kind!="wood"&&gate.Kind!="metal"||gate.Motion!="swing"&&gate.Motion!="slide"||!(gate.Width>0&&gate.Height>0))throw new FormatException("Expedition hub: invalid gate "+gate.Id);
                    gates.Add(gate);
                }
                l.Gates=gates.ToArray();
                if(h.TryGetValue("apron",out var px)){var p=O(px);l.SpawnApron=F(p,"spawn");l.ZoneApron=F(p,"zone");}
                var zones=new List<ExpeditionHubZone>();
                foreach(var zx in A(h,"zones"))
                {
                    var z=O(zx);var spawns=new List<int>();foreach(var e in A(z,"spawns"))if(e is double d)spawns.Add((int)d);
                    var gd=A(z,"gateDistance");zones.Add(new ExpeditionHubZone{Id=S(z,"id"),Ring=S(z,"ring"),Volume=V(z["volume"]),Spawns=spawns.ToArray(),
                        Near=gd.Count>0&&gd[0] is double n0?(float)n0:0,Far=gd.Count>1&&gd[1] is double n1?(float)n1:0});
                }
                l.Zones=zones.ToArray();
                var wilds=new List<ExpeditionHubWilds>();foreach(var wx in A(h,"wilds")){var w=O(wx);wilds.Add(new ExpeditionHubWilds{Id=S(w,"id"),Side=S(w,"side"),Volume=V(w["volume"])});}l.Wilds=wilds.ToArray();
                var routes=new List<ExpeditionHubRoute>();
                foreach(var rx in A(h,"routes")){var r=O(rx);var ap=P(r["approach"]);routes.Add(new ExpeditionHubRoute{To=S(r,"to"),Gate=S(r,"gate"),Path=F(r,"path"),Straight=F(r,"straight"),Run=F(r,"run"),Sprint=F(r,"sprint"),Approach=ap,X=ap.x,Z=ap.z});}
                foreach(var rx in A(h,"reserved")){var r=O(rx);routes.Add(new ExpeditionHubRoute{To=S(r,"building"),Gate=S(r,"gate"),Reserved=true,Path=F(r,"path"),Straight=F(r,"straight"),Run=F(r,"run"),Sprint=F(r,"sprint"),Approach=P(r["approach"]),X=F(r,"x"),Z=F(r,"z"),Yaw=F(r,"yaw")});}
                l.Routes=routes.ToArray();return l;
            }
            return null;
        }
    }

    /// <summary>
    /// PROPOSED replicated gate state (docs/EXPEDITION-HUB.md "Astra contract"): the gate's id, whether the authority holds it
    /// open, and the authority-clock time (run seconds, the closures' clock) the state began. The presentation eases the leaves
    /// from that moment, so every client sees the same swing; a state seen late snaps to where the motion would be.
    /// </summary>
    [Serializable] public struct ExpeditionGateState {public string Id;public bool Open;public double ChangedAt;}
    /// <summary>What the hub presentation reads each frame: the gates' states (none: every gate rests open, see
    /// NativeExpeditionHubGates.RestOpen), whether the proposed sanctuary rules are live (`Holster`: weapons holstered inside;
    /// `Ward`: the fence-line ward) and whether this is the DEV preview's synthetic state (labelled as such on screen).</summary>
    [Serializable] public sealed class ExpeditionHubView
    {
        public ExpeditionGateState[] Gates=Array.Empty<ExpeditionGateState>();public int Count;public bool Holster,Ward,Preview;
        public bool Find(string id,out ExpeditionGateState s){for(int i=0;i<Count&&i<Gates.Length;i++)if(Gates[i].Id==id){s=Gates[i];return true;}s=default;return false;}
    }

    /// <summary>
    /// DEV PREVIEW ONLY (DEV panel · Camera · "Hub gates preview"): a synthetic hub state so the gates, the ward and the
    /// holster tag can be looked at without the authority. Each gate opens and closes on its own 8 s cycle (open 4 s, shut 4 s,
    /// staggered), the ward and holster read as on. It never reaches the simulation: no collider, no input, no network.
    /// </summary>
    public static class NativeExpeditionHubPreview
    {
        public static bool On;public const double Period=8,Stagger=1.7;
        public static void Fill(ExpeditionHubView v,ExpeditionHubLayout l,double t)
        {
            if(v==null)return;v.Preview=true;v.Holster=v.Ward=true;int n=l?.Gates.Length??0;if(v.Gates.Length<n)v.Gates=new ExpeditionGateState[n];v.Count=n;
            for(int i=0;i<n;i++){double off=i*Stagger,half=Period/2;double k=Math.Floor((t-off)/half);v.Gates[i]=new ExpeditionGateState{Id=l.Gates[i].Id,Open=((long)k&1)==0,ChangedAt=off+k*half};}
        }
    }

    /// <summary>The pose of one gate at a moment: `Open` 0 shut .. 1 open (the leaves' swing or slide; it overshoots a little at
    /// the stops), `Latch` 0 latched .. 1 lifted, `Moving` while the leaves travel, and the motion's start/stop times and its
    /// direction (for the dust and the cues).</summary>
    public struct ExpeditionGatePose {public float Open,Latch;public bool Moving,Opening;public double Began,Stops;}

    /// <summary>
    /// The hub gates' motion, models, dust and ward shimmer (presentation of the proposed GateState). Per gate it remembers
    /// the state it last saw so a reversal mid-swing eases from where the leaves are; built into reused buffers:
    /// <see cref="Parts"/> (solid boxes drawn with NativeBlockLit, full quaternions: the leaves swing about their hinges) and
    /// <see cref="Quads"/> (dust, sparks and the ward's shimmer, NativeSirenFx's effects batch).
    ///  - Wooden palisade gate (north, west): two leaves of five pointed logs on battens with a Z-brace and iron straps,
    ///    hinged at the posts' inner corners; they swing inward 160° and rest 20° off the fence. Opening: the iron
    ///    flip latch lifts (0.18 s), the leaves swing with a little overshoot; closing: they swing shut accelerating, slam,
    ///    bounce and the latch drops.
    ///  - Scrap-metal gate (east, south): two welded frames of mismatched scrap sheet on a top track, bi-parting into pockets
    ///    behind the fence sections on the inner side. Opening: the cane bolt lifts, the leaves roll apart with a judder;
    ///    closing: they roll shut and bang, the bolt drops.
    ///  - Without a state a gate rests OPEN (<see cref="RestOpen"/>): the opening carries no collider until the director's gate
    ///    authority exists, and a shut leaf that pilots and zombies walk through would fake a barrier.
    ///  - The ward (when the view says it is on): a pale gold shimmer on the fence's outer face and a brighter curtain across
    ///    every gate's opening, whether the gate is open or shut (zombies never cross it); motes rise along it.
    /// </summary>
    public sealed class NativeExpeditionHubGates
    {
        public const bool RestOpen=true;
        public const float WoodOpenDeg=160,LatchLift=.18f,SlideOffset=2.08f;public const int PartCapacity=640,QuadCapacity=1200;
        public const int TimberHex=0x6E5236,TimberDarkHex=0x4E3A26,TimberLightHex=0x8A6A44,IronHex=0x2A2826,RustHex=0x6A3A22,GalvHex=0x8A9094,
            PaintBlueHex=0x3A5068,PaintGreenHex=0x4A5A3E,PaintRedHex=0x7A3028,FrameHex=0x3A3C3E,DustHex=0x6E625A,WardHex=0xFFD890,SparkHex=0xFFA040;
        public readonly List<NativeHubPart> Parts=new List<NativeHubPart>(PartCapacity);
        public readonly NativeFxQuads Quads=new NativeFxQuads(QuadCapacity);
        /// <summary>Gates drawn this frame (for the checks and the budget lines).</summary>
        public int GatesDrawn,WardQuads,DustQuads;

        private sealed class Memory {public string Id;public bool Seen,Open;public double ChangedAt;public float From;}
        private readonly Dictionary<string,Memory> memory=new Dictionary<string,Memory>();
        public void Forget()=>memory.Clear();

        // ---------------- motion ----------------
        private static float Clamp01(double v)=>(float)(v<0?0:v>1?1:v);
        private static double Smooth(double u){u=u<0?0:u>1?1:u;return u*u*(3-2*u);}
        private static double InOut(double u){u=u<0?0:u>1?1:u;return u<.5?4*u*u*u:1-Math.Pow(-2*u+2,3)/2;}
        private static double OutBack(double u){u=u<0?0:u>1?1:u;const double c1=1.3,c3=c1+1;return 1+c3*Math.Pow(u-1,3)+c1*Math.Pow(u-1,2);}
        /// <summary>The pose of a gate whose state is `s` (seen from `from`, the leaves' opening when the state began) at `t`.</summary>
        public static ExpeditionGatePose PoseOf(ExpeditionHubGate g,bool open,double changedAt,float from,double t,ExpeditionHubLayout l)
        {
            double a=t-changedAt;float dur=open?(l?.Open??.9f):(l?.Close??1f);var p=new ExpeditionGatePose{Opening=open,Began=changedAt};
            if(open)
            {
                // The latch lifts first, then the leaves travel the rest of the way (a reversal mid-swing: the remaining part).
                double travel=Math.Max(.15,(dur-LatchLift)*(1-from));p.Stops=changedAt+LatchLift+travel;p.Latch=Clamp01(Smooth(a/LatchLift));
                double u=(a-LatchLift)/travel;if(u<=0){p.Open=from;return p;}
                double k=g!=null&&g.Wood?OutBack(u):InOut(u);if(g!=null&&!g.Wood&&u<1)k+=.012*Math.Sin(u*Math.PI*9)*Math.Sin(u*Math.PI);
                if(g!=null&&!g.Wood&&u>=1){double b=a-LatchLift-travel;k=1+.02*Math.Exp(-b*9)*Math.Sin(b*28);}
                p.Open=(float)(from+(1-from)*k);p.Moving=u<1;return p;
            }
            else
            {
                // The leaves swing or roll shut accelerating, slam and bounce; the latch drops as they meet.
                double travel=Math.Max(.15,dur*.85*from);p.Stops=changedAt+travel;double u=a/travel;
                if(u<1){double k=u*u*u;p.Open=(float)(from*(1-k));p.Moving=a>=0;p.Latch=1;return p;}
                double b=a-travel;p.Open=(float)(Math.Max(0,(g!=null&&g.Wood?.035:.012)*Math.Exp(-b*7)*Math.Abs(Math.Sin(b*22))));
                p.Latch=1-Clamp01(Smooth((b-.04)/.12));return p;
            }
        }
        /// <summary>The pose of gate `g` this frame from the view, remembering states so a reversal starts where the leaves were.</summary>
        public ExpeditionGatePose Pose(ExpeditionHubGate g,ExpeditionHubView v,double t,ExpeditionHubLayout l)
        {
            if(v==null||!v.Find(g.Id,out var s)){if(memory.TryGetValue(g.Id,out var gone))gone.Seen=false;return RestPose();}
            if(!memory.TryGetValue(g.Id,out var m))memory[g.Id]=m=new Memory{Id=g.Id};
            if(!m.Seen){m.Seen=true;m.Open=s.Open;m.ChangedAt=s.ChangedAt;m.From=s.Open?0:1;}
            else if(m.Open!=s.Open||m.ChangedAt!=s.ChangedAt)
            {
                // A new state: its motion starts where the leaves were at that moment under the state before it (the same
                // direction issued again travels the whole way).
                float at=m.Open!=s.Open?PoseOf(g,m.Open,m.ChangedAt,m.From,s.ChangedAt,l).Open:(s.Open?0:1);
                m.Open=s.Open;m.ChangedAt=s.ChangedAt;m.From=Math.Max(0,Math.Min(1,at));
            }
            return PoseOf(g,m.Open,m.ChangedAt,m.From,t,l);
        }
        /// <summary>A gate with no state: open, latch lifted, still.</summary>
        public static ExpeditionGatePose RestPose()=>new ExpeditionGatePose{Open=RestOpen?1:0,Latch=RestOpen?1:0,Began=double.NegativeInfinity,Stops=double.NegativeInfinity};

        // ---------------- frames ----------------
        /// <summary>A point in the gate's frame (u along the fence, the viewer's right looking out; v up; d out of the sanctuary)
        /// to world.</summary>
        public static void World(ExpeditionHubGate g,double u,double v,double d,out double x,out double y,out double z)
        {double a=g.Yaw*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);x=g.X+u*c+d*s;y=g.Y+v;z=g.Z-u*s+d*c;}
        private static void Yaw(double deg,out double qy,out double qw){double h=deg*Math.PI/360;qy=Math.Sin(h);qw=Math.Cos(h);}
        private static void Mul(double ax,double ay,double az,double aw,double bx,double by,double bz,double bw,out double x,out double y,out double z,out double w)
        {x=aw*bx+ax*bw+ay*bz-az*by;y=aw*by-ax*bz+ay*bw+az*bx;z=aw*bz+ax*by-ay*bx+az*bw;w=aw*bw-ax*bx-ay*by-az*bz;}
        // The moving frame of a leaf: its pivot (u, d) in the gate frame and its turn about y (degrees); a part is given in it as
        // (x along the leaf, v, n out of its outer face) with its own roll about n (degrees).
        private ExpeditionHubGate gate;private double pu,pd,phi;private int seedBase;
        private void Frame(ExpeditionHubGate g,double u,double d,double turn){gate=g;pu=u;pd=d;phi=turn;}
        private void Part(double x,double v,double n,double hx,double hv,double hn,int look,int hex,double roll=0,int flags=0,double gain=1,double level=1,bool fixedPart=false,int seed=0)
        {
            if(Parts.Count>=PartCapacity)return;double a=phi*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);
            double u=pu+x*c+n*s,d=pd-x*s+n*c;World(gate,u,v,d,out double wx,out double wy,out double wz);
            Yaw(gate.Yaw+phi,out double qy,out double qw);double rx=0,ry=0,rz=0,rw=1;if(roll!=0){double h=roll*Math.PI/360;rz=Math.Sin(h);rw=Math.Cos(h);}
            Mul(0,qy,0,qw,rx,ry,rz,rw,out double ox,out double oy,out double oz,out double ow);
            World(gate,u,v,d,out double restX,out double restY,out double restZ);
            Parts.Add(new NativeHubPart{X=(float)wx,Y=(float)wy,Z=(float)wz,Hx=(float)hx,Hy=(float)hv,Hz=(float)hn,Qx=(float)ox,Qy=(float)oy,Qz=(float)oz,Qw=(float)ow,Look=look,Flags=flags,Hex=hex,Gain=(float)gain,Level=(float)level,
                RestX=(float)(restX+x*.001),RestY=(float)restY,RestZ=(float)restZ,Seed=NativeExpeditionStates.Hash(seedBase,seed),Fixed=fixedPart});
        }

        // ---------------- models ----------------
        /// <summary>Appends gate `g`'s model at pose `p` to <see cref="Parts"/>.</summary>
        public void Model(ExpeditionHubGate g,in ExpeditionGatePose p)
        {
            seedBase=NativeExpeditionStates.StableHash(g.Id);double half=g.Width/2;
            if(g.Wood)
            {
                double ang=WoodOpenDeg*p.Open;
                for(int side=0;side<2;side++){double sg=side==0?1:-1;Frame(g,-sg*half,-.2,sg*ang);WoodLeaf(g,sg,side,p);}
            }
            else
            {
                double slide=SlideOffset*p.Open;
                for(int side=0;side<2;side++){double sg=side==0?1:-1;Frame(g,-sg*(half+slide),-.3,0);MetalLeaf(g,sg,side,p);}
                // The top track over the opening and the pockets (fixed hardware, above the closure's 3.2 m).
                Frame(g,0,-.3,0);Part(0,3.3,0,half+2.2,.06,.07,NativeBlockLooks.SteelGreen,FrameHex,fixedPart:true,seed:90);
                for(int k=-1;k<=1;k+=2)Part(k*(half+2.1),3.3,.12,.06,.12,.12,NativeBlockLooks.SteelGreen,IronHex,fixedPart:true,seed:91+k);
            }
        }
        // A wooden leaf in its frame: x from the hinge toward the gate's middle (sg flips it for the right leaf), n out of the
        // outer face (the planks n 0..0.12, the battens and brace 0.12..0.17, straps and latch on them).
        private void WoodLeaf(ExpeditionHubGate g,double sg,int side,in ExpeditionGatePose p)
        {
            const double w=.37,gap=.02,t=.12;int timber=NativeBlockLooks.Timber;
            for(int k=0;k<5;k++)
            {
                double x0=.02+k*(w+gap),cx=sg*(x0+w/2),h=2.74+.2*NativeExpeditionStates.Hash(seedBase,side*10+k);int hex=k%3==0?TimberHex:k%3==1?TimberDarkHex:TimberLightHex;
                Part(cx,.06+h/2,t/2,w/2,h/2,t/2,timber,hex,seed:side*20+k);
                // A stepped point on every log.
                Part(cx,.06+h+.05,t/2,w*.32,.05,t/2*.9,timber,hex,seed:side*20+k+5);Part(cx,.06+h+.13,t/2,w*.14,.04,t/2*.8,timber,hex,seed:side*20+k+10);
            }
            double len=5*(w+gap);
            for(int r=0;r<2;r++){double v=r==0?.55:2.3;Part(sg*(.02+len/2),v,t+.025,len/2-.02,.08,.025,timber,TimberDarkHex,seed:side*20+15+r);}
            // The Z-brace between the battens, from the hinge foot to the latch head.
            double bx0=.18,bv0=.66,bx1=len-.18,bv1=2.19,bl=Math.Sqrt((bx1-bx0)*(bx1-bx0)+(bv1-bv0)*(bv1-bv0)),roll=Math.Atan2(bv1-bv0,bx1-bx0)*180/Math.PI;
            Part(sg*(bx0+bx1)/2,(bv0+bv1)/2,t+.025,bl/2,.07,.024,timber,TimberHex,sg*roll,seed:side*20+18);
            // Iron straps and hinge knuckles at the hinge side.
            for(int r=0;r<3;r++){double v=.4+1.1*r;Part(sg*.48,v,t+.058,.44,.035,.008,NativeBlockLooks.SteelGreen,IronHex,seed:side*20+(int)(v*10));Part(sg*.045,v,.045,.04,.07,.045,NativeBlockLooks.SteelGreen,IronHex,seed:side*20+(int)(v*10)+3);}
            // The flip latch: a bar pivoting on the left leaf's meeting stile, dropping into the keeper on the right leaf.
            if(side==0){double lift=70*p.Latch,lx=len-.32;Part(lx+.22*Math.Cos(lift*Math.PI/180),1.3+.22*Math.Sin(lift*Math.PI/180),t+.06,.3,.025,.012,NativeBlockLooks.SteelGreen,IronHex,lift,seed:40);Part(lx,1.3,t+.05,.035,.035,.02,NativeBlockLooks.SteelGreen,RustHex,seed:41);}
            else Part(sg*(len-.12),1.3,t+.055,.06,.06,.015,NativeBlockLooks.SteelGreen,IronHex,seed:42);
        }
        // A scrap-metal leaf: a welded frame (x 0..2.0 toward the middle), three bands of mismatched sheet, a brace, a handle,
        // wheels on the bottom; the cane bolt on the left leaf's meeting edge lifts as it unlatches.
        private void MetalLeaf(ExpeditionHubGate g,double sg,int side,in ExpeditionGatePose p)
        {
            const double len=1.98,fr=.045;int steel=NativeBlockLooks.SteelGreen,corr=NativeBlockLooks.Corrugated;
            Part(sg*.05,1.55,0,fr,1.45,fr,steel,FrameHex,seed:side*30);Part(sg*(len-.05),1.55,0,fr,1.45,fr,steel,FrameHex,seed:side*30+1);
            for(int r=0;r<4;r++){double v=r==0?.12:r==1?1.05:r==2?2.0:2.96;Part(sg*len/2,v,0,len/2,fr,fr,steel,FrameHex,seed:side*30+2+r);}
            for(int b=0;b<3;b++){double v0=b==0?.16:b==1?1.09:2.04,v1=b==0?1.01:b==1?1.96:2.92;double inset=.05*NativeExpeditionStates.Hash(seedBase,side*7+b);
                int band=b==0?(side==0?RustHex:GalvHex):b==1?(side==0?PaintBlueHex:RustHex):(side==0?GalvHex:PaintGreenHex);
                Part(sg*(len/2+inset*.5),(v0+v1)/2,(b%2==0?.02:-.02),len/2-.06-inset*.5,(v1-v0)/2,.012,b==1&&side==0?NativeBlockLooks.Plain:corr,band,seed:side*30+10+b);}
            double bl=Math.Sqrt(1.9*1.9+1.8*1.8),roll=Math.Atan2(1.8,1.9)*180/Math.PI;Part(sg*len/2,1.55,-.045,bl/2,.03,.02,steel,FrameHex,sg*roll,seed:side*30+14);
            Part(sg*(len-.25),1.15,.06,.025,.18,.03,steel,IronHex,seed:side*30+15);
            for(int r=0;r<2;r++){double x=r==0?.3:len-.3;Part(sg*x,.05,0,.07,.05,.04,steel,IronHex,seed:side*30+16+r);}
            if(side==0){double lift=.18*p.Latch;Part(sg*(len-.1),.55+lift,-.06,.015,.5,.015,steel,GalvHex,seed:50);Part(sg*(len-.1),1.06+lift,-.075,.05,.015,.015,steel,GalvHex,seed:51);}
            else Part(sg*(len-.06),1.4,.05,.03,.06,.02,steel,PaintRedHex,seed:52);
        }

        // ---------------- dust, sparks and the ward ----------------
        private static float Lin(int hex,int shift){double c=((hex>>shift)&255)/255.0;return (float)(c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4));}
        private static double H(int a,int b,int c)=>NativeExpeditionStates.Hash(a*131+b,c);
        private void Sprite(in ExpeditionEye e,double x,double y,double z,double size,int hex,double gain,double alpha,bool additive)
        {
            double rx=e.Rx*size,ry=e.Ry*size,rz=e.Rz*size,ux=e.Ux*size,uy=e.Uy*size,uz=e.Uz*size;
            Quads.Add(x-rx-ux,y-ry-uy,z-rz-uz,x+rx-ux,y+ry-uy,z+rz-uz,x+rx+ux,y+ry+uy,z+rz+uz,x-rx+ux,y-ry+uy,z-rz+uz,Lin(hex,16)*gain*(additive?1:alpha),Lin(hex,8)*gain*(additive?1:alpha),Lin(hex,0)*gain*(additive?1:alpha),alpha,1,additive,1);
        }
        /// <summary>Dust kicked up by the leaves while they travel and a burst where they stop (sparks off the metal track).</summary>
        public void Dust(ExpeditionHubGate g,in ExpeditionGatePose p,double t,in ExpeditionEye e,bool reduced)
        {
            if(double.IsInfinity(p.Began))return;int seed=NativeExpeditionStates.StableHash(g.Id);double half=g.Width/2;int puffs=reduced?4:10;
            // Drag dust: puffs left behind along the leaves' bottom edge while they move, each living 1.1 s.
            for(int k=0;k<puffs;k++)
            {
                double start=p.Began+(p.Opening?LatchLift:0),emit=start+Math.Max(0,p.Stops-start)*(k+.5)/puffs,age=t-emit;if(age<0||age>1.1)continue;
                double tip=(k%2==0?1:-1);double u=g.Wood?tip*(half-1.6*H(seed,k,1)):tip*(half+.3);double d=g.Wood?-.6-.9*H(seed,k,2):-.3;
                World(g,u+.4*age*(H(seed,k,3)-.5),.12+.35*age,d-.2*age,out double x,out double y,out double z);
                Sprite(e,x,y,z,.18+.55*age,DustHex,1,.28*(1-age/1.1),false);DustQuads++;
            }
            // The stop: a burst at the meeting edge (closing) or the hinges (opening); sparks off a metal gate's track.
            double sa=t-p.Stops;if(sa<0||sa>1.4)return;int burst=reduced?5:12;
            for(int k=0;k<burst;k++)
            {
                double ang=H(seed,k,5)*Math.PI*2,r=(.3+.9*H(seed,k,6))*(1-Math.Exp(-sa*4));double u=p.Opening?(k%2==0?-half:half):0;
                World(g,u+Math.Cos(ang)*r,.1+.25*sa+.15*H(seed,k,7),-.25+Math.Sin(ang)*r*.6,out double x,out double y,out double z);
                Sprite(e,x,y,z,.22+.7*sa,DustHex,1,.34*(1-sa/1.4),false);DustQuads++;
            }
            if(!g.Wood&&sa<.35&&!reduced)for(int k=0;k<6;k++)
            {
                double u=p.Opening?(k%2==0?-1:1)*(half+SlideOffset):0;World(g,u+(H(seed,k,8)-.5)*.6*sa*4,.08+3.2*sa*H(seed,k,9),-.3+(H(seed,k,10)-.5)*sa*2,out double x,out double y,out double z);
                Sprite(e,x,y,z,.035,SparkHex,3,1-sa/.35,true);DustQuads++;
            }
        }
        /// <summary>The ward's shimmer on the outer face of fence segments (world end points a and b along the fence's outer
        /// face, height h) and the curtain across each gate's opening; rising motes. Only when the view's ward is on.</summary>
        public void Ward(ExpeditionHubLayout l,IReadOnlyList<float[]> segments,double t,in ExpeditionEye e,bool reduced)
        {
            for(int s=0;s<segments.Count;s++)
            {
                var f=segments[s];double ax=f[0],az=f[1],bx=f[2],bz=f[3],h=f[4];double len=Math.Sqrt((bx-ax)*(bx-ax)+(bz-az)*(bz-az));int cols=Math.Max(1,(int)Math.Ceiling(len/1.0));
                for(int c=0;c<cols;c++)
                {
                    double t0=(double)c/cols,t1=(c+1.0)/cols,x0=ax+(bx-ax)*t0,z0=az+(bz-az)*t0,x1=ax+(bx-ax)*t1,z1=az+(bz-az)*t1;
                    // Faint, brightest at the foot and fading up in three bands, so the fence still reads through it.
                    double band=.5+.5*Math.Sin(t*1.7+c*.9+s*2.1),a=(.03+.04*band)*(reduced?.6:1),y1=h*.4,y2=h*.75;
                    Quads.Add(x0,0,z0,x1,0,z1,x1,y1,z1,x0,y1,z0,Lin(WardHex,16),Lin(WardHex,8),Lin(WardHex,0),a,0,true,1);WardQuads++;
                    Quads.Add(x0,y1,z0,x1,y1,z1,x1,y2,z1,x0,y2,z0,Lin(WardHex,16),Lin(WardHex,8),Lin(WardHex,0),a*.62,0,true,1);WardQuads++;
                    Quads.Add(x0,y2,z0,x1,y2,z1,x1,h+.5,z1,x0,h+.5,z0,Lin(WardHex,16),Lin(WardHex,8),Lin(WardHex,0),a*.3,0,true,1);WardQuads++;
                }
            }
            foreach(var g in l.Gates)
            {
                double half=g.Width/2;World(g,-half,0,.24,out double x0,out double _,out double z0);World(g,half,0,.24,out double x1,out _,out double z1);
                for(int k=0;k<3;k++){double a=(.045+.025*Math.Sin(t*2.3+k*2.1+g.X))*(reduced?.6:1),top=g.Height+.6-k*.9;
                    Quads.Add(x0,0,z0,x1,0,z1,x1,top,z1,x0,top,z0,Lin(WardHex,16),Lin(WardHex,8),Lin(WardHex,0),a,0,true,1);WardQuads++;}
                // The ward's floor line across the threshold.
                World(g,-half,.02,.05,out double fx0,out double fy,out double fz0);World(g,half,.02,.05,out double fx1,out _,out double fz1);World(g,half,.02,.45,out double fx2,out _,out double fz2);World(g,-half,.02,.45,out double fx3,out _,out double fz3);
                Quads.Add(fx0,fy,fz0,fx1,fy,fz1,fx2,fy,fz2,fx3,fy,fz3,Lin(WardHex,16),Lin(WardHex,8),Lin(WardHex,0),.35,0,true,1);WardQuads++;
                if(reduced)continue;int seed=NativeExpeditionStates.StableHash(g.Id);
                for(int k=0;k<8;k++){double life=2.6,ph=(t/life+H(seed,k,20))%1,u=(H(seed,k,21)-.5)*g.Width;World(g,u,.2+ph*3.2,.3,out double mx,out double my,out double mz);Sprite(e,mx,my,mz,.05,WardHex,2,.6*Math.Sin(ph*Math.PI),true);WardQuads++;}
            }
        }

        /// <summary>Builds the frame: every gate's model at its pose (from the view, or at rest), its dust, and the ward if on.</summary>
        public void Build(ExpeditionHubLayout l,ExpeditionHubView v,IReadOnlyList<float[]> wardSegments,double t,in ExpeditionEye e,bool reduced,float cull=120)
        {
            Parts.Clear();Quads.Clear();GatesDrawn=WardQuads=DustQuads=0;if(l==null)return;
            foreach(var g in l.Gates)
            {
                var p=Pose(g,v,t,l);float dx=g.X-e.X,dz=g.Z-e.Z;if(dx*dx+dz*dz>cull*cull)continue;
                Model(g,p);Dust(g,p,t,e,reduced);GatesDrawn++;
            }
            if(v!=null&&v.Ward&&wardSegments!=null)Ward(l,wardSegments,t,e,reduced);
        }

        // ---------------- sound ----------------
        public const int CueLatch=0,CueCreak=1,CueStop=2,CueSlam=3,CueBolt=4;
        /// <summary>The cues of gate `g` in state `s` due in (from, to] of the authority clock, into `into`: opening, the latch
        /// clank (0.05 s), the creak or rumble as the leaves start (0.2 s) and the stop; closing, the creak or rumble at once,
        /// the slam where they meet and the latch dropping just after. Returns the count.</summary>
        public static int Cues(ExpeditionHubGate g,in ExpeditionGateState s,double from,double to,ExpeditionHubLayout l,int[] kinds,double[] times)
        {
            int n=0;if(g==null||kinds==null||times==null||!(to>from))return 0;double dur=s.Open?(l?.Open??.9):(l?.Close??1);
            void Add(int kind,double at){if(at>from&&at<=to&&n<kinds.Length&&n<times.Length){kinds[n]=kind;times[n]=at;n++;}}
            if(s.Open){Add(CueLatch,s.ChangedAt+.05);Add(CueCreak,s.ChangedAt+.2);Add(CueStop,s.ChangedAt+dur);}
            else{Add(CueCreak,s.ChangedAt+.05);Add(CueSlam,s.ChangedAt+dur*.85);Add(CueBolt,s.ChangedAt+dur*.85+.07);}
            return n;
        }
    }
    /// <summary>One solid box of the hub gates (NativeBlockLit): world centre, half extents, full turn (the leaves swing about
    /// their hinges), look, flags, sRGB colour with an HDR gain and light level, the pattern's rest point and seed; `Fixed`
    /// marks hardware that does not move with the leaves (the metal gates' track).</summary>
    public struct NativeHubPart {public float X,Y,Z,Hx,Hy,Hz,Qx,Qy,Qz,Qw,Gain,Level,RestX,RestY,RestZ,Seed;public int Look,Flags,Hex;public bool Fixed;}
}
