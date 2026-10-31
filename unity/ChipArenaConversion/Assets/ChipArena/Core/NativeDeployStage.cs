using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>What the console's buttons ask the authority for (the scene and the console only raise these; Astra's room, or
    /// the DEV preview, decides).</summary>
    public enum DeployIntent{None,RollDestinations,RollMutators,SkipMutators,Deploy}
    /// <summary>The console's host buttons for a frame (guests have none).</summary>
    public struct DeployControls{public bool Roll,Mutate,Skip,Deploy;}
    /// <summary>One frame's input to the station: the replicated state, this client's times since it saw the rolls and the
    /// deploy (negative or NaN: unknown, shown landed), the room's readiness and the catalogs the reels carry.</summary>
    public struct DeployInput
    {
        public ExpeditionDeploymentState State;public float SinceInstanceRoll,SinceMutatorRoll,SinceDeploy,Time;public bool Host,PartyReady,Reduced;
        /// <summary>Seats on the pads (the host on pad 0) and each seat's readiness (Ready[i] for i below Seats).</summary>
        public int Seats;public bool[] Ready;
        /// <summary>The eligible instances (with their themes) and mutator ids the reels carry; null: the result's ids only.</summary>
        public ExpeditionInstanceTheme[] Instances;public string[] Mutators;
    }
    /// <summary>
    /// The times this client first saw each roll and the deploy (docs/EXPEDITION-DEPLOYMENT.md "Needs from Astra": roll times
    /// are local). The first state observed keeps its rolls landed (a late joiner never sees an old roll spin).
    /// </summary>
    public sealed class NativeDeployClock
    {
        private int instanceRolls=-1,mutatorRolls=-1;private bool deployed,seen;private float instanceAt=float.NaN,mutatorAt=float.NaN,deployAt=float.NaN;
        public void Reset(){instanceRolls=mutatorRolls=-1;deployed=seen=false;instanceAt=mutatorAt=deployAt=float.NaN;}
        public void Observe(ExpeditionDeploymentState s,float now)
        {
            if(s==null)return;
            if(!seen){seen=true;instanceRolls=s.instanceRolls;mutatorRolls=s.mutatorRolls;deployed=s.deployed;return;}
            if(s.instanceRolls!=instanceRolls){instanceRolls=s.instanceRolls;instanceAt=now;}
            if(s.mutatorRolls!=mutatorRolls){mutatorRolls=s.mutatorRolls;mutatorAt=now;}
            if(s.deployed&&!deployed){deployed=true;deployAt=now;}
        }
        public float SinceInstanceRoll(float now)=>now-instanceAt;
        public float SinceMutatorRoll(float now)=>now-mutatorAt;
        public float SinceDeploy(float now)=>now-deployAt;
    }

    /// <summary>
    /// The Deployment Station posed for a frame (presentation only; Runtime/NativeDeployScene.cs uploads Models once and draws
    /// Items each frame): every model's placement and light levels from NativeDeployPresentation's rules over the replicated
    /// state, the practical lights, the camera easing between the shots, where each party pilot stands, and the strips and map
    /// the screens show. The reels and dials come to rest on the authority's result (Reel); rolled mutators read PENDING until
    /// Deploy and ACTIVE after; Deploy darkens and clamps the machines, opens the gate, lifts the party and fades. Frame
    /// allocates nothing once the strips are built (they rebuild only when a result is not on them).
    /// </summary>
    public sealed class NativeDeployStage
    {
        public enum Model{Room,Table,Map,Holo,Beacon,Pad,Instance,Lever,DrumBand,DrumRims,Mutator,Card,Quad,Gate,DoorLeft,DoorRight,Lift,Console,Lamp,Beam}
        public static readonly int ModelCount=Enum.GetValues(typeof(Model)).Length;
        public enum Blend{Opaque,Additive,Alpha}
        /// <summary>Texture slots: white, the holo map, the stamps, the soft glow (NativeDeployArt.GlowTile), then the strips
        /// (machine 0 the instance reels, 1 the dials).</summary>
        public const int TexWhite=0,TexMap=1,TexStamp=2,TexGlow=3,TexStrip=4,TextureCount=TexStrip+6;
        public static int StripTexture(int machine,int reel)=>TexStrip+machine*3+reel;
        public const int Capacity=96,MapWidth=200,MapHeight=152;
        /// <summary>One draw: a model at World (station frame, System.Numerics row vectors), its bones' light tints (linear rgb x
        /// level; Light is bone 1, Extra0.. bones 2..), the glass's opacity, and for screen sections the texture, its scroll (uv.y x
        /// Rep + Scroll), motion blur (texture v), HDR gain, blend, tint, the scan sweep (-1 none, else 0..1 up the screen), the chroma boost (1 as drawn) and whether the
        /// screen takes the holo map treatment (edge glow, the blueprint cell grid, a frame with corner brackets).</summary>
        public struct Item
        {
            public Model Model;public Matrix4x4 World;public Vector4 Light,Extra0,Extra1,Extra2,Extra3,Extra4;public float Glass;
            public int Texture;public float Scroll,Rep,Blur,Gain,Sweep,Chroma;public bool Holo;public Blend Blend;public Vector4 Screen;
            public Vector4 Tint(int bone)=>bone==NativeDeployModels.Lights?Light:bone==2?Extra0:bone==3?Extra1:bone==4?Extra2:bone==5?Extra3:bone==6?Extra4:Vector4.One;
        }
        public readonly WeaponMesh[] Models=new WeaponMesh[ModelCount];
        public readonly Item[] Items=new Item[Capacity];public int Count;
        public readonly NativeEntranceLights Lights=new NativeEntranceLights();
        /// <summary>The camera this frame (eased toward Shot).</summary>
        public NativeDeployPresentation.View Camera;public NativeDeployPresentation.Shot Shot;private bool cameraSet;
        public NativeDeployPresentation.MachineLook InstanceLook,MutatorLook;public NativeDeployPresentation.Launch Launch;public NativeDeployPresentation.DeployPose Deploy;
        public DeployControls Controls;
        /// <summary>The party on the pads: feet (station frame), facing (unit, xz), how much of each is shown (0 lifted away).</summary>
        public readonly Vector3[] PilotFeet=new Vector3[NativeDeployPresentation.Pads],PilotFacing=new Vector3[NativeDeployPresentation.Pads];public readonly float[] PilotShown=new float[NativeDeployPresentation.Pads];public int Pilots;
        /// <summary>The reel and dial strips (ids; the dials' textures carry NativeDeployArt.Back after them) and a version that
        /// changes when any strip does; the state revision the map shows (-1 none; frozen after Deploy).</summary>
        public int StripVersion{get;private set;}public int MapRevision{get;private set;}=-1;
        public string[] Strip(int machine,int reel)=>strips[machine*3+reel];
        public string ThemeOf(string id)=>id!=null&&themes.TryGetValue(id,out var t)?t:null;
        /// <summary>The reels' and dials' strip positions this frame (in symbols) and their speeds.</summary>
        public readonly NativeDeployPresentation.ReelPose[] Poses=new NativeDeployPresentation.ReelPose[6];

        private readonly string[][] strips={Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>()};
        private readonly Dictionary<string,string> themes=new Dictionary<string,string>(StringComparer.Ordinal);
        private object instanceSource,mutatorSource;private readonly int[] seenRolls={-1,-1};private readonly int[] from=new int[6],land=new int[6];
        private readonly Vector4[] seat=new Vector4[NativeDeployPresentation.Pads];

        public NativeDeployStage()
        {
            Models[(int)Model.Room]=NativeDeployModels.StationRoom();Models[(int)Model.Table]=NativeDeployModels.WarTable();Models[(int)Model.Map]=NativeDeployModels.MapPlane();
            Models[(int)Model.Holo]=NativeDeployModels.HoloVolume();Models[(int)Model.Beacon]=NativeDeployModels.Beacon();Models[(int)Model.Pad]=NativeDeployModels.PartyPad();
            Models[(int)Model.Instance]=NativeDeployModels.InstanceMachine();Models[(int)Model.Lever]=NativeDeployModels.Lever();Models[(int)Model.DrumBand]=NativeDeployModels.Drum();
            Models[(int)Model.DrumRims]=NativeDeployModels.DrumRims();Models[(int)Model.Mutator]=NativeDeployModels.MutatorMachine();Models[(int)Model.Card]=NativeDeployModels.CardDial();
            Models[(int)Model.Quad]=NativeDeployModels.Quad();Models[(int)Model.Gate]=NativeDeployModels.GateFrame();Models[(int)Model.DoorLeft]=NativeDeployModels.DoorLeaf(-1);
            Models[(int)Model.DoorRight]=NativeDeployModels.DoorLeaf(1);Models[(int)Model.Lift]=NativeDeployModels.LiftDeck();Models[(int)Model.Console]=NativeDeployModels.DeployConsole();
            Models[(int)Model.Lamp]=NativeDeployModels.Lamp();Models[(int)Model.Beam]=NativeDeployModels.Beam();
        }

        // ---- the strips (allocate only when the catalog or a result changes) ----
        private bool OnStrip(int k,string id)=>id==null||Array.IndexOf(strips[k],id)>=0;
        private void Strips(in DeployInput f)
        {
            var s=f.State;bool rebuild=!ReferenceEquals(instanceSource,f.Instances)||!ReferenceEquals(mutatorSource,f.Mutators);
            if(s!=null){for(int r=0;r<3;r++){if(r<s.districts.Length&&!OnStrip(r,s.districts[r]?.instance))rebuild=true;if(r<s.mutators.Length&&!OnStrip(3+r,s.mutators[r]))rebuild=true;}}
            if(!rebuild&&StripVersion>0)return;
            instanceSource=f.Instances;mutatorSource=f.Mutators;var ids=new List<string>();var mids=new List<string>();
            if(f.Instances!=null)foreach(var x in f.Instances)if(x!=null&&x.id!=null){ids.Add(x.id);if(x.theme!=null)themes[x.id]=x.theme;}
            if(f.Mutators!=null)mids.AddRange(f.Mutators);
            if(s!=null){foreach(var d in s.districts)if(d?.instance!=null){ids.Add(d.instance);if(d.theme!=null)themes[d.instance]=d.theme;}mids.AddRange(s.mutators);}
            if(ids.Count==0)foreach(var id in new[]{"exp-ward-zero","exp-transit","exp-foundry"})ids.Add(id);
            for(int r=0;r<3;r++)
            {
                strips[r]=NativeDeployPresentation.Strip(ids,r);strips[3+r]=NativeDeployPresentation.Strip(mids,r);
                if(s!=null&&r<s.districts.Length&&s.districts[r]?.instance!=null)NativeDeployPresentation.Slot(ref strips[r],s.districts[r].instance);
                if(s!=null&&r<s.mutators.Length)NativeDeployPresentation.Slot(ref strips[3+r],s.mutators[r]);
            }
            StripVersion++;
        }
        // A new roll: each reel spins on from where the last one left it.
        private void Rolls(ExpeditionDeploymentState s)
        {
            if(s==null)return;
            if(s.instanceRolls!=seenRolls[0]){bool first=seenRolls[0]<0;seenRolls[0]=s.instanceRolls;for(int r=0;r<3;r++){int l=r<s.districts.Length?Math.Max(0,Array.IndexOf(strips[r],s.districts[r]?.instance)):0;from[r]=first?l:land[r];land[r]=l;}}
            if(s.mutatorRolls!=seenRolls[1]){bool first=seenRolls[1]<0;seenRolls[1]=s.mutatorRolls;for(int r=0;r<3;r++){int l=r<s.mutators.Length?Math.Max(0,Array.IndexOf(strips[3+r],s.mutators[r])):0;from[3+r]=first?l:land[3+r];land[3+r]=l;}}
        }

        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static float Frac(float x)=>x-(float)Math.Floor(x);
        private static Vector4 Col(int hex,float level,float a=1){var c=WeaponPaint.Linear(hex);return new Vector4(c.X*level,c.Y*level,c.Z*level,a);}
        private static Vector4 Grey(float level)=>new Vector4(level,level,level,1);
        private static float Hash(float x)=>Frac((float)Math.Sin(x*12.9898f)*43758.5453f);
        private ref Item Add(Model model,in Matrix4x4 world)
        {
            if(Count>=Capacity)Count=Capacity-1;ref var it=ref Items[Count++];
            it=new Item{Model=model,World=world,Light=Vector4.One,Extra0=Vector4.One,Extra1=Vector4.One,Extra2=Vector4.One,Extra3=Vector4.One,Extra4=Vector4.One,Glass=1,Texture=TexWhite,Rep=1,Gain=1,Sweep=-1,Chroma=1,Screen=Vector4.One};
            return ref it;
        }
        private static Matrix4x4 T(Vector3 v)=>Matrix4x4.CreateTranslation(v);
        // A soft pool of light on the floor (an additive glow decal: the practicals' spill the Lambert floor cannot show).
        private void Pool(Vector3 at,float w,float d,float yaw,Vector4 tint)
        {
            if(tint.X+tint.Y+tint.Z<=.002f)return;ref var it=ref Add(Model.Quad,Matrix4x4.CreateScale(w,d,1)*Matrix4x4.CreateRotationX((float)Math.PI/2)*Matrix4x4.CreateRotationY(yaw)*T(at+new Vector3(0,.012f,0)));
            it.Texture=TexGlow;it.Blend=Blend.Additive;it.Screen=tint;
        }

        /// <summary>The console's host buttons: roll destinations until Deploy (not while the reels spin), roll mutators once the
        /// destinations are set, skip while any are rolled (or to say "without"), Deploy when the room is ready.</summary>
        public static DeployControls ControlsOf(ExpeditionDeploymentState s,in NativeDeployPresentation.MachineLook instance,in NativeDeployPresentation.MachineLook mutator,NativeDeployPresentation.Launch launch,bool host)
        {
            if(!host||s==null||s.deployed)return default;bool spinning=instance.Phase==NativeDeployPresentation.Phase.Spinning||mutator.Phase==NativeDeployPresentation.Phase.Spinning;
            return new DeployControls{Roll=!spinning,Mutate=s.worldRolled&&!spinning,Skip=s.worldRolled&&!spinning&&s.mutators.Length>0,Deploy=launch==NativeDeployPresentation.Launch.Ready&&!spinning};
        }

        /// <summary>Poses the station for one frame; `dt` the frame's seconds for the camera's ease (negative: cut to the shot).</summary>
        public void Frame(in DeployInput f,float dt)
        {
            var s=f.State;float t=f.Time;Strips(f);Rolls(s);Count=0;
            InstanceLook=NativeDeployPresentation.InstanceMachine(s,f.SinceInstanceRoll,f.SinceDeploy,f.Host);MutatorLook=NativeDeployPresentation.MutatorMachine(s,f.SinceMutatorRoll,f.SinceDeploy,f.Host);
            Launch=NativeDeployPresentation.DeployConsole(s,f.PartyReady,f.Host);Deploy=NativeDeployPresentation.Deploying(s!=null&&s.deployed?f.SinceDeploy:float.NaN);
            if(s!=null&&s.deployed&&!(f.SinceDeploy>=0))Deploy=NativeDeployPresentation.Deploying(NativeDeployPresentation.TravelAt);
            Controls=ControlsOf(s,InstanceLook,MutatorLook,Launch,f.Host);
            if(s!=null&&(!s.deployed||MapRevision<0)&&s.revision!=MapRevision)MapRevision=s.revision;
            var P=NativeDeployPresentation.Phase.Spinning;bool deployed=s!=null&&s.deployed;var dep=Deploy;
            float flicker=f.Reduced?.95f:.86f+.14f*(float)Math.Sin(t*23)*(float)Math.Sin(t*7.3f)-(Hash((float)Math.Floor(t*9))>.93f?.45f:0);
            float pulse=.5f+.5f*(float)Math.Sin(t*3.1f),breath=.5f+.5f*(float)Math.Sin(t*1.6f);

            // the camera: the spinning machine, the gate on Deploy, else the overview
            Shot=NativeDeployPresentation.Framing(InstanceLook,MutatorLook,s);var want=NativeDeployPresentation.ShotOf(Shot);
            if(!cameraSet||!(dt>=0)){Camera=want;cameraSet=true;}
            else{float k=1-(float)Math.Exp(-dt/(Shot==NativeDeployPresentation.Shot.Deploy?.9f:.55f));Camera=new NativeDeployPresentation.View{Eye=Vector3.Lerp(Camera.Eye,want.Eye,k),Target=Vector3.Lerp(Camera.Target,want.Target,k),Fov=Camera.Fov+(want.Fov-Camera.Fov)*k};}

            // the room: light bars dim for the gate, the table's ring breathing, the walkway lit when the party may go
            {
                ref var it=ref Add(Model.Room,Matrix4x4.Identity);float room=1-.65f*dep.Dim;it.Light=Grey(1.5f*room);it.Extra0=Col(0x7fd6ff,(1.1f+.4f*breath)*room);
                bool go=Launch==NativeDeployPresentation.Launch.Ready||Launch==NativeDeployPresentation.Launch.HostOnly;
                it.Extra1=deployed?Col(0x6dffa8,2.4f+1.2f*(float)Math.Sin(t*9)):go?Col(0x6dffa8,1.2f+.8f*breath):Grey(.35f);
            }
            // the war-table, its holo map (frozen at Deploy: the sweep stops), the projector volume and the beacons
            {
                var at=T(NativeDeployPresentation.Table);ref var table=ref Add(Model.Table,at);table.Light=Col(0x7fd6ff,1.5f);
                bool world=s!=null&&s.worldRolled;
                ref var map=ref Add(Model.Map,at);map.Texture=TexMap;map.Blend=Blend.Additive;map.Gain=world?1.7f:.9f;map.Sweep=deployed?-1:Frac(t*.28f);map.Screen=Vector4.One;map.Chroma=1.8f;map.Holo=true;
                ref var holo=ref Add(Model.Holo,at);holo.Blend=Blend.Additive;holo.Screen=Col(0x48d4ff,world?.22f:.1f);holo.Gain=1;
                if(world)
                {
                    for(int d=0;d<3&&d<s.districts.Length;d++)
                    {
                        var dd=s.districts[d];if(dd==null)continue;var p=NativeDeployModels.MapPoint(dd.x*NativeDeployPresentation.CellMetres,dd.z*NativeDeployPresentation.CellMetres);
                        float bob=.012f*(float)Math.Sin(t*2+d*2.1f);ref var b=ref Add(Model.Beacon,T(p+new Vector3(0,.004f+bob,0))*at);b.Blend=Blend.Additive;b.Screen=Col(NativeDeployPresentation.DestinationOf(dd.instance,dd.theme).Color,2.2f);
                    }
                    var boss=NativeDeployPresentation.BossAnchor;ref var bb=ref Add(Model.Beacon,Matrix4x4.CreateScale(1.25f)*T(NativeDeployModels.MapPoint(boss.X,boss.Y))*at);bb.Blend=Blend.Additive;bb.Screen=Col(NativeDeployPresentation.BossColor,2.6f+.8f*pulse);
                }
            }
            // the back wall's map screens (the same picture, frozen at Deploy)
            for(int side=-1;side<=1;side+=2)
            {
                ref var wm=ref Add(Model.Quad,Matrix4x4.CreateScale(NativeDeployModels.WallMapWidth,NativeDeployModels.WallMapHeight,1)*T(NativeDeployModels.WallMapAt(side)));
                wm.Texture=TexMap;wm.Blend=Blend.Additive;wm.Gain=s!=null&&s.worldRolled?1.3f:.7f;wm.Sweep=deployed?-1:Frac(t*.21f+(side>0?.5f:0));wm.Screen=Vector4.One;wm.Chroma=1.8f;wm.Holo=true;
            }
            // the floor's light pools: the table, the gate (brightening as it opens)
            Pool(NativeDeployPresentation.Table,5.4f,5.4f,0,Col(0x48d4ff,.32f));
            Pool(NativeDeployPresentation.Gate+new Vector3(0,0,-1.6f),7.5f,4.4f,0,Col(0xcfe6ff,.16f+.9f*dep.Gate));
            // the pads and their ready lamps (ready green, waiting amber, empty dim)
            int seats=Math.Max(0,Math.Min(NativeDeployPresentation.Pads,f.Seats));
            for(int i=0;i<NativeDeployPresentation.Pads;i++)
            {
                bool here=i<seats,ready=here&&f.Ready!=null&&i<f.Ready.Length&&f.Ready[i];
                seat[i]=!here?Grey(.12f):ready?Col(0x6dffa8,2.2f):Col(0xffb02e,1.1f+.6f*pulse);if(deployed&&here)seat[i]=Col(0x9ff0ff,2.2f+2*dep.Lift);
                ref var pad=ref Add(Model.Pad,T(NativeDeployPresentation.Pad(i)));pad.Light=seat[i];if(here)Pool(NativeDeployPresentation.Pad(i),2f,2f,0,seat[i]*.16f);
            }

            // the INSTANCE MACHINE: cabinet lights at its power, the marquee chase, the approach strip in the districts' colours,
            // the lever, the three reels on the authority's result
            {
                var root=NativeDeployModels.Root(NativeDeployPresentation.InstanceMachineAt,NativeDeployPresentation.InstanceYaw);var look=InstanceLook;bool spin=look.Phase==P;
                float landed=f.SinceInstanceRoll-NativeDeployPresentation.RollDuration,flash=landed>=0&&landed<.8f?1-landed/.8f:0;
                ref var body=ref Add(Model.Instance,root);body.Light=Grey(1.7f*look.Power);
                Pool(NativeDeployPresentation.InstanceMachineAt+NativeDeployModels.Facing(NativeDeployPresentation.InstanceYaw)*.9f,4.2f,4.2f,0,Col(0xffa850,.34f*look.Power));
                float a,b;
                if(look.Phase==NativeDeployPresentation.Phase.Locked){a=b=.15f*look.Power;}
                else if(spin){bool on=Frac(t*7)<.5f;a=on?2.6f:.35f;b=on?.35f:2.6f;}
                else if(look.Phase==NativeDeployPresentation.Phase.Dormant){a=b=.7f+.6f*breath;}
                else{bool on=Frac(t*1.2f)<.5f;a=(on?2:.6f)+2*flash;b=(on?.6f:2)+2*flash;}
                body.Extra3=Col(0xffb45a,a);body.Extra4=Col(0xffb45a,b);
                for(int r=0;r<3;r++)
                {
                    int n=Math.Max(1,strips[r].Length);var pose=s!=null&&s.instanceRolls>0?NativeDeployPresentation.Reel(r,f.SinceInstanceRoll,from[r],land[r],n):new NativeDeployPresentation.ReelPose{Offset=.5f,Landed=true};Poses[r]=pose;
                    int under=NativeDeployModels.Card(pose.Offset,n);string id=strips[r].Length>0?strips[r][under]:null;
                    var color=s!=null&&s.worldRolled?NativeDeployPresentation.DestinationOf(id,ThemeOf(id)).Color:NativeDeployPresentation.NeutralColor;
                    var seg=s==null||s.instanceRolls==0?Grey(.25f):Col(color,(spin?1.4f:2.4f+1.5f*flash)*look.Power);
                    if(r==0)body.Extra0=seg;else if(r==1)body.Extra1=seg;else body.Extra2=seg;
                    var axle=T(NativeDeployModels.ReelAt(r))*root;
                    ref var band=ref Add(Model.DrumBand,axle);band.Texture=StripTexture(0,r);band.Rep=1f/n;band.Scroll=(pose.Offset+.5f)/n;band.Blur=Math.Min(.5f,pose.Speed*.028f)/n;
                    band.Gain=.35f+.75f*look.Power+.6f*flash;band.Screen=Vector4.One;
                    Add(Model.DrumRims,Matrix4x4.CreateRotationX(-pose.Offset*2*(float)Math.PI/NativeDeployModels.DrumSlots)*axle);
                }
                ref var lever=ref Add(Model.Lever,Matrix4x4.CreateRotationX(NativeDeployModels.LeverAngle(deployed?0:NativeDeployPresentation.Lever(f.SinceInstanceRoll)))*T(NativeDeployModels.LeverPivot)*root);lever.Light=Col(0xffb45a,1.6f*look.Power);
            }
            // the MUTATOR MACHINE: flickering cool lights, the polarity rings, SKIP and OPTIONAL, the three card dials and the
            // PENDING / ACTIVE stamps (never ACTIVE before Deploy)
            {
                var root=NativeDeployModels.Root(NativeDeployPresentation.MutatorMachineAt,NativeDeployPresentation.MutatorYaw);var look=MutatorLook;var ph=look.Phase;
                bool rolled=s!=null&&s.mutatorRolls>0&&s.mutators.Length>0;
                ref var body=ref Add(Model.Mutator,root);body.Light=Grey(1.5f*look.Power*flicker);
                Pool(NativeDeployPresentation.MutatorMachineAt+NativeDeployModels.Facing(NativeDeployPresentation.MutatorYaw)*.9f,4.2f,4.2f,0,Col(0x9d8cff,.3f*look.Power*flicker));
                body.Extra3=ph==NativeDeployPresentation.Phase.Skipped?Col(0xffd27a,2.4f):look.Usable&&(ph==NativeDeployPresentation.Phase.Optional||ph==NativeDeployPresentation.Phase.Pending)?Col(0xffd27a,1.1f+.7f*pulse):Grey(.15f);
                body.Extra4=ph==NativeDeployPresentation.Phase.Optional?Col(0x7fd6ff,1.5f+.9f*pulse):Grey((ph==NativeDeployPresentation.Phase.Unavailable?.3f:.8f)*look.Power*flicker);
                for(int r=0;r<3;r++)
                {
                    int n=strips[3+r].Length;bool card=rolled&&r<s.mutators.Length&&n>0;
                    var pose=card?NativeDeployPresentation.Reel(r,f.SinceMutatorRoll,from[3+r],land[3+r],n):new NativeDeployPresentation.ReelPose{Offset=0,Landed=true};Poses[3+r]=pose;
                    int tile=card?NativeDeployModels.Card(pose.Offset,n):n;var m=card?NativeDeployPresentation.MutatorOf(strips[3+r][tile]):default;
                    var ring=!card?Grey(ph==NativeDeployPresentation.Phase.Unavailable?.15f:.4f):Col(m.Color,(ph==NativeDeployPresentation.Phase.Active?2.6f:ph==NativeDeployPresentation.Phase.Pending?1.2f+.6f*pulse:1.6f)*Math.Max(look.Power,ph==NativeDeployPresentation.Phase.Active?1:0));
                    if(r==0)body.Extra0=ring;else if(r==1)body.Extra1=ring;else body.Extra2=ring;
                    var at=T(NativeDeployModels.CardAt(r))*root;
                    ref var dial=ref Add(Model.Card,Matrix4x4.CreateRotationY(-NativeDeployModels.CardTurn(card?pose.Offset:0))*at);dial.Texture=StripTexture(1,r);dial.Rep=1f/(n+1);dial.Scroll=tile/(float)(n+1);
                    dial.Gain=ph==NativeDeployPresentation.Phase.Active?1.5f:.4f+.7f*look.Power*flicker;dial.Screen=Vector4.One;
                    bool stamp=card&&pose.Landed&&(ph==NativeDeployPresentation.Phase.Pending||ph==NativeDeployPresentation.Phase.Active);
                    if(stamp)
                    {
                        ref var st=ref Add(Model.Quad,Matrix4x4.CreateScale(NativeDeployModels.StampWidth,NativeDeployModels.StampHeight,1)*Matrix4x4.CreateRotationZ(NativeDeployModels.StampTilt)*T(NativeDeployModels.StampAt(r))*root);
                        st.Texture=TexStamp;st.Blend=Blend.Alpha;st.Rep=.5f;st.Scroll=ph==NativeDeployPresentation.Phase.Active?.5f:0;st.Gain=ph==NativeDeployPresentation.Phase.Active?1.6f:1.1f+.3f*pulse;st.Screen=Vector4.One;
                    }
                }
            }
            // the DEPLOY GATE: the sign and the header's lamps per seat, the doors parting, the lift rising
            {
                var at=T(NativeDeployPresentation.Gate);Vector4 sign;
                switch(Launch)
                {
                    case NativeDeployPresentation.Launch.NeedDestination:sign=Grey(.35f);break;
                    case NativeDeployPresentation.Launch.WaitingParty:sign=Col(0xffb02e,1.2f+.8f*pulse);break;
                    case NativeDeployPresentation.Launch.Launching:sign=Col(0xe8fff2,2.2f+3*(1-dep.Gate));break;
                    default:sign=Col(0x6dffa8,1.6f+1.0f*breath);break;
                }
                ref var gate=ref Add(Model.Gate,at);gate.Light=Col(0xd4f2ff,1.3f+2.2f*dep.Gate);gate.Extra0=sign;
                bool warn=deployed&&dep.Gate<1&&Frac(t*3)<.5f;
                ref var left=ref Add(Model.DoorLeft,T(new Vector3(-NativeDeployModels.DoorTravel*dep.Gate,0,0))*at);left.Light=warn?Col(0xff3a3a,2.4f):Grey(1.4f);
                ref var right=ref Add(Model.DoorRight,T(new Vector3(NativeDeployModels.DoorTravel*dep.Gate,0,0))*at);right.Light=left.Light;
                ref var lift=ref Add(Model.Lift,T(NativeDeployModels.LiftAt+new Vector3(0,NativeDeployModels.LiftTravel*dep.Lift,0))*at);lift.Light=Grey(1.8f);
                for(int i=0;i<NativeDeployPresentation.Pads;i++){ref var l=ref Add(Model.Lamp,T(NativeDeployModels.GateLampAt(i))*at);l.Light=seat[i];}
                var ca=T(NativeDeployModels.ConsoleAt);ref var console=ref Add(Model.Console,ca);console.Light=Col(0x7fd6ff,1.2f);console.Extra0=sign;
                for(int i=0;i<NativeDeployPresentation.Pads;i++){ref var l=ref Add(Model.Lamp,T(NativeDeployModels.ConsoleLampAt(i))*ca);l.Light=seat[i];}
            }
            // the party: on their pads facing the room's front, lifted away in light columns at Deploy
            Pilots=seats;
            for(int i=0;i<NativeDeployPresentation.Pads;i++)
            {
                var pad=NativeDeployPresentation.Pad(i);var front=Vector3.Normalize(new Vector3(-pad.X*.35f,0,-1));var gate=NativeDeployPresentation.Gate-pad;gate.Y=0;
                var face=Vector3.Normalize(Vector3.Lerp(front,Vector3.Normalize(gate),Smooth(dep.Dim*1.4f-.1f)));if(face.LengthSquared()<.5f)face=front;
                PilotFeet[i]=pad+new Vector3(0,NativeDeployModels.PadTop+3.2f*dep.Lift*dep.Lift,0);PilotFacing[i]=face;PilotShown[i]=i<seats?1-Smooth((dep.Lift-.25f)/.35f):0;
                if(i<seats&&dep.Lift>0&&dep.Lift<1){ref var beam=ref Add(Model.Beam,T(pad));beam.Blend=Blend.Additive;beam.Screen=Col(0x9ff0ff,.24f*(float)Math.Sin(Math.PI*dep.Lift));}
            }
            // the light: each machine's practical at its power, the table, the gate opening, the console's button
            float consoleLevel=Launch==NativeDeployPresentation.Launch.Ready||Launch==NativeDeployPresentation.Launch.HostOnly?.6f+.4f*breath:Launch==NativeDeployPresentation.Launch.Launching?1:.1f;
            NativeDeployModels.StationLights(Lights,InstanceLook.Power,MutatorLook.Power,flicker,1,dep.Gate,consoleLevel,dep.Dim);
        }
    }
}
