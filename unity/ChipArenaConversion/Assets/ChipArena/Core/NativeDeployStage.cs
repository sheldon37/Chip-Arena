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
        /// <summary>The viewer's own pad (the eject's shots follow its pilot: NativeDeployEject.ShotOf) and the eject's 4 s variant
        /// (DEV preview only; the live station plays NativeDeployEject.Played, which TravelAt follows).</summary>
        public int Mine;public bool Short;
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
        public enum Model{Room,Table,Map,Holo,Beacon,Pad,Instance,Lever,DrumBand,DrumRims,Mutator,Card,Quad,Gate,DoorLeft,DoorRight,Lift,Console,Lamp,Beam,Hatch,Well,
            // the eject's set under the floor (NativeDeployEjectModels; items only after Deploy): an opened pad, the drop tubes (a
            // shallow and a deep pod's), the roof collar, the seat and its harness bar, a docking clamp, the belly and the rolled
            // city (built per run by the scene from the frozen state: an empty model here)
            PadRing,Iris,Tube,TubeDeep,Collar,Seat,Harness,Clamp,Belly,City}
        public static readonly int ModelCount=Enum.GetValues(typeof(Model)).Length;
        public enum Blend{Opaque,Additive,Alpha}
        /// <summary>Texture slots: white, the holo map, the stamps, the soft glow (NativeDeployArt.GlowTile), the spark
        /// (SparkTile), then the strips (machine 0 the instance reels, 1 the dials).</summary>
        public const int TexWhite=0,TexMap=1,TexStamp=2,TexGlow=3,TexSpark=4,TexStrip=5,TextureCount=TexStrip+6;
        public static int StripTexture(int machine,int reel)=>TexStrip+machine*3+reel;
        /// <summary>The draw list's size: the station's own items (the eject's set under the floor after Deploy, EjectItems at
        /// most), then the slot sparks (NativeDeploySparks.Limit), drawn last.</summary>
        public const int EjectItems=80,Capacity=96+EjectItems+NativeDeploySparks.Limit,MapWidth=200,MapHeight=152;
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
        /// <summary>The slot sparks this frame (NativeDeploySparks; both machines), drawn as the last items.</summary>
        public readonly NativeDeploySparks.Spark[] Sparks=new NativeDeploySparks.Spark[NativeDeploySparks.Limit];public int SparkCount;
        /// <summary>The rolls' moments heard this frame, for sound (NativeDeploySparks.Heard; Astra's audio plays them).</summary>
        public readonly NativeDeploySparks.Cue[] Cues=new NativeDeploySparks.Cue[24];public int CueCount;
        /// <summary>The eject this frame (NativeDeployEject): the clock it reads (NaN before Deploy), its cut and the viewer's pad;
        /// each pad's pod frame (station; base at the origin, doorway +z) and whether it shows (the deploy cast draws the pods from
        /// the source-model bank); the eject's moments heard this frame (NativeEjectSounds; Runtime/NativeEjectAudio.cs plays them).</summary>
        public float EjectSince=float.NaN;public NativeDeployEject.Cut EjectCut;public int Mine;public bool Reduced;
        public readonly Matrix4x4[] PodWorld=new Matrix4x4[NativeDeployPresentation.Pads];public readonly bool[] PodShown=new bool[NativeDeployPresentation.Pads];
        public readonly NativeEjectSounds.Cue[] EjectCues=new NativeEjectSounds.Cue[NativeEjectSounds.Limit];public int EjectCueCount;
        private float ejectHeard=float.NaN;

        private readonly string[][] strips={Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>()};
        private readonly Dictionary<string,string> themes=new Dictionary<string,string>(StringComparer.Ordinal);
        private object instanceSource;private string[] mutatorSource;private readonly int[] seenRolls={-1,-1};private readonly int[] from=new int[6],land=new int[6];
        private readonly Vector4[] seat=new Vector4[NativeDeployPresentation.Pads];
        private readonly int[] landColor=new int[3];private readonly NativeDeployPresentation.Polarity[] cardPolarity=new NativeDeployPresentation.Polarity[3];
        private readonly NativeDeploySparks.Cue[] schedule=new NativeDeploySparks.Cue[12];private readonly float[] heard={float.NaN,float.NaN};

        public NativeDeployStage()
        {
            Models[(int)Model.Room]=NativeDeployModels.StationRoom();Models[(int)Model.Table]=NativeDeployModels.WarTable();Models[(int)Model.Map]=NativeDeployModels.MapPlane();
            Models[(int)Model.Holo]=NativeDeployModels.HoloVolume();Models[(int)Model.Beacon]=NativeDeployModels.Beacon();Models[(int)Model.Pad]=NativeDeployModels.PartyPad();
            Models[(int)Model.Instance]=NativeDeployModels.InstanceMachine();Models[(int)Model.Lever]=NativeDeployModels.Lever();Models[(int)Model.DrumBand]=NativeDeployModels.Drum();
            Models[(int)Model.DrumRims]=NativeDeployModels.DrumRims();Models[(int)Model.Mutator]=NativeDeployModels.MutatorMachine();Models[(int)Model.Card]=NativeDeployModels.CardDial();
            Models[(int)Model.Quad]=NativeDeployModels.Quad();Models[(int)Model.Gate]=NativeDeployModels.GateFrame();Models[(int)Model.DoorLeft]=NativeDeployModels.DoorLeaf(-1);
            Models[(int)Model.DoorRight]=NativeDeployModels.DoorLeaf(1);Models[(int)Model.Lift]=NativeDeployModels.LiftDeck();Models[(int)Model.Console]=NativeDeployModels.DeployConsole();
            Models[(int)Model.Lamp]=NativeDeployModels.Lamp();Models[(int)Model.Beam]=NativeDeployModels.Beam();
            Models[(int)Model.Hatch]=NativeDeployModels.HatchLeaf();Models[(int)Model.Well]=NativeDeployModels.Well();
            Models[(int)Model.PadRing]=NativeDeployModels.PadRing();Models[(int)Model.Iris]=NativeDeployModels.HatchLeaf();
            Models[(int)Model.Tube]=NativeDeployEjectModels.TubeOf(NativeDeployEject.TubeLength(0));Models[(int)Model.TubeDeep]=NativeDeployEjectModels.TubeOf(NativeDeployEject.TubeLength(1));
            Models[(int)Model.Collar]=NativeDeployEjectModels.CollarModel();Models[(int)Model.Seat]=NativeDeployEjectModels.SeatModel();Models[(int)Model.Harness]=NativeDeployEjectModels.HarnessModel();
            Models[(int)Model.Clamp]=NativeDeployEjectModels.ClampModel();Models[(int)Model.Belly]=NativeDeployEjectModels.BellyModel();
            Models[(int)Model.City]=new WeaponMesh{Spec="deploy-"+NativeDeployEjectModels.City,Tier=0};
        }

        // ---- the strips (allocate only when the catalog or a result changes) ----
        // Equivalent defensive copies must not invalidate six asynchronous texture builds every frame.
        private static bool SameCatalog(string[] before,string[] next)
        {
            if(ReferenceEquals(before,next))return true;
            if(before==null||next==null||before.Length!=next.Length)return false;
            for(int i=0;i<before.Length;i++)if(before[i]!=next[i])return false;
            return true;
        }
        private bool OnStrip(int k,string id)=>id==null||Array.IndexOf(strips[k],id)>=0;
        private void Strips(in DeployInput f)
        {
            var s=f.State;bool rebuild=!ReferenceEquals(instanceSource,f.Instances)||!SameCatalog(mutatorSource,f.Mutators);
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
            var cut=f.Short?NativeDeployEject.Cut.Short:NativeDeployEject.Played;float travel=NativeDeployEject.TravelOf(cut);
            Launch=NativeDeployPresentation.DeployConsole(s,f.PartyReady,f.Host);Deploy=NativeDeployPresentation.Deploying(s!=null&&s.deployed?f.SinceDeploy:float.NaN,cut);
            if(s!=null&&s.deployed&&!(f.SinceDeploy>=0))Deploy=NativeDeployPresentation.Deploying(travel,cut);
            Controls=ControlsOf(s,InstanceLook,MutatorLook,Launch,f.Host);
            if(s!=null&&(!s.deployed||MapRevision<0)&&s.revision!=MapRevision)MapRevision=s.revision;
            var P=NativeDeployPresentation.Phase.Spinning;bool deployed=s!=null&&s.deployed;var dep=Deploy;
            // the eject's clock (an unknown deploy time reads as over: a late joiner sees it done) and the viewer's pad
            float since=deployed?(f.SinceDeploy>=0?f.SinceDeploy:travel):float.NaN;int mine=Math.Max(0,Math.Min(NativeDeployPresentation.Pads-1,f.Mine));
            EjectSince=since;EjectCut=cut;Mine=mine;Reduced=f.Reduced;
            // what each machine's reels landed on (the sparks' colours and polarities) and the cabinets' kick as they land
            int destinations=s!=null&&s.instanceRolls>0?Math.Min(3,s.districts.Length):0,cards=s!=null&&s.mutatorRolls>0?Math.Min(3,s.mutators.Length):0;
            for(int r=0;r<3;r++)
            {
                var dd=r<destinations?s.districts[r]:null;landColor[r]=dd!=null?NativeDeployPresentation.DestinationOf(dd.instance,dd.theme).Color:NativeDeployPresentation.UnknownColor;
                cardPolarity[r]=r<cards?NativeDeployPresentation.MutatorOf(s.mutators[r]).Polarity:NativeDeployPresentation.Polarity.Unknown;
            }
            float kickI=NativeDeploySparks.Kick(0,f.SinceInstanceRoll,null,destinations,f.Reduced),kickM=NativeDeploySparks.Kick(1,f.SinceMutatorRoll,cardPolarity,cards,f.Reduced);
            float flicker=f.Reduced?.95f:.86f+.14f*(float)Math.Sin(t*23)*(float)Math.Sin(t*7.3f)-(Hash((float)Math.Floor(t*9))>.93f?.45f:0);
            float pulse=.5f+.5f*(float)Math.Sin(t*3.1f),breath=.5f+.5f*(float)Math.Sin(t*1.6f);

            // the camera: the spinning machine, the pads on Deploy, then the eject's shots on my pad (they cut in), else the overview
            Shot=NativeDeployPresentation.Framing(InstanceLook,MutatorLook,s);if(deployed&&Shot==NativeDeployPresentation.Shot.Deploy)Shot=NativeDeployEject.ShotOf(mine,since,cut);
            bool cuts=NativeDeployEject.Cuts(Shot);var want=cuts?NativeDeployEject.ViewOf(Shot,mine,since,cut):NativeDeployPresentation.ShotOf(Shot);
            if(!cameraSet||!(dt>=0)||cuts){Camera=want;cameraSet=true;}
            else{float k=1-(float)Math.Exp(-dt/(Shot==NativeDeployPresentation.Shot.Deploy?.9f:.55f));Camera=new NativeDeployPresentation.View{Eye=Vector3.Lerp(Camera.Eye,want.Eye,k),Target=Vector3.Lerp(Camera.Target,want.Target,k),Fov=Camera.Fov+(want.Fov-Camera.Fov)*k};}

            // the room: light bars dim for the gate, the table's ring breathing, the walkway lit when the party may go
            {
                ref var it=ref Add(Model.Room,Matrix4x4.Identity);float room=1-.65f*dep.Dim;it.Light=Grey(1.5f*room);it.Extra0=Col(0x7fd6ff,(1.1f+.4f*breath)*room);
                // at Deploy the light bars turn red and pulse with the klaxon
                if(dep.Red>0)it.Extra0=Vector4.Lerp(it.Extra0,Col(0xff2a22,1.5f+1.1f*(.5f+.5f*(float)Math.Sin(t*9.5f))),dep.Red);
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
            // the floor's light pools: the table, the gate
            Pool(NativeDeployPresentation.Table,5.4f,5.4f,0,Col(0x48d4ff,.32f));
            Pool(NativeDeployPresentation.Gate+new Vector3(0,0,-1.6f),7.5f,4.4f,0,Col(0xcfe6ff,.16f));
            // the pads and their ready lamps (ready green, waiting amber, empty dim); at Deploy the occupied pads sound the alarm
            // (red, flashing), split their hatches open over the lit well into their drop tubes and drop their pilots through, cyan
            int seats=Math.Max(0,Math.Min(NativeDeployPresentation.Pads,f.Seats));
            bool alarmOn=Frac(t*4)<.5f;
            for(int i=0;i<NativeDeployPresentation.Pads;i++)
            {
                bool here=i<seats,ready=here&&f.Ready!=null&&i<f.Ready.Length&&f.Ready[i];var at=NativeDeployPresentation.Pad(i);
                seat[i]=!here?Grey(.12f):ready?Col(0x6dffa8,2.2f):Col(0xffb02e,1.1f+.6f*pulse);
                if(deployed&&here)seat[i]=dep.Alarm?(alarmOn?Col(0xff3a3a,2.8f):Col(0xff3a3a,.5f)):Col(0x9ff0ff,2.2f+2*dep.Hatch);
                ref var pad=ref Add(deployed&&here?Model.PadRing:Model.Pad,T(at));pad.Light=seat[i];if(here)Pool(at,2f,2f,0,seat[i]*.16f);
                if(!deployed||!here)continue;
                float drop=NativeDeployEject.Drop(i,since,cut),open=dep.Hatch*NativeDeployModels.HatchTravel;
                var glow=dep.Alarm?(alarmOn?Col(0xff3a3a,2.6f):Col(0xff3a3a,.5f)):Col(0x9ff0ff,1.6f+2.4f*Smooth(1-drop/2));
                if(dep.Hatch>0){ref var well=ref Add(Model.Well,T(at));well.Light=Col(0x9ff0ff,1.4f+1.6f*dep.Hatch);}
                ref var leafA=ref Add(Model.Hatch,T(at+new Vector3(open,0,0)));leafA.Light=glow;
                ref var leafB=ref Add(Model.Hatch,Matrix4x4.CreateRotationY((float)Math.PI)*T(at+new Vector3(-open,0,0)));leafB.Light=glow;
            }

            // the INSTANCE MACHINE: cabinet lights at its power, the marquee chase, the approach strip in the districts' colours,
            // the lever, the three reels on the authority's result
            {
                var root=NativeDeployModels.Root(NativeDeployPresentation.InstanceMachineAt,NativeDeployPresentation.InstanceYaw);var look=InstanceLook;bool spin=look.Phase==P;
                float landed=f.SinceInstanceRoll-NativeDeployPresentation.RollDuration,flash=landed>=0&&landed<.8f?1-landed/.8f:0;
                ref var body=ref Add(Model.Instance,root);body.Light=Grey(1.7f*look.Power*kickI);
                Pool(NativeDeployPresentation.InstanceMachineAt+NativeDeployModels.Facing(NativeDeployPresentation.InstanceYaw)*.9f,4.2f,4.2f,0,Col(0xffa850,.34f*look.Power*kickI));
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
                ref var body=ref Add(Model.Mutator,root);body.Light=Grey(1.5f*look.Power*flicker*kickM);
                Pool(NativeDeployPresentation.MutatorMachineAt+NativeDeployModels.Facing(NativeDeployPresentation.MutatorYaw)*.9f,4.2f,4.2f,0,Col(0x9d8cff,.3f*look.Power*flicker*kickM));
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
            // the DEPLOY GATE: the sign and the header's lamps per seat; it stays shut (the party drops through the pads), its doors
            // flashing red while the party goes
            {
                var at=T(NativeDeployPresentation.Gate);Vector4 sign;
                switch(Launch)
                {
                    case NativeDeployPresentation.Launch.NeedDestination:sign=Grey(.35f);break;
                    case NativeDeployPresentation.Launch.WaitingParty:sign=Col(0xffb02e,1.2f+.8f*pulse);break;
                    case NativeDeployPresentation.Launch.Launching:sign=Col(0xe8fff2,2.2f+3*(1-dep.Drop));break;
                    default:sign=Col(0x6dffa8,1.6f+1.0f*breath);break;
                }
                ref var gate=ref Add(Model.Gate,at);gate.Light=Col(0xd4f2ff,1.3f);gate.Extra0=sign;
                bool warn=deployed&&dep.Drop<1&&Frac(t*3)<.5f;
                ref var left=ref Add(Model.DoorLeft,at);left.Light=warn?Col(0xff3a3a,2.4f):Grey(1.4f);
                ref var right=ref Add(Model.DoorRight,at);right.Light=left.Light;
                ref var lift=ref Add(Model.Lift,T(NativeDeployModels.LiftAt)*at);lift.Light=Grey(1.8f);
                for(int i=0;i<NativeDeployPresentation.Pads;i++){ref var l=ref Add(Model.Lamp,T(NativeDeployModels.GateLampAt(i))*at);l.Light=seat[i];}
                var ca=T(NativeDeployModels.ConsoleAt);ref var console=ref Add(Model.Console,ca);console.Light=Col(0x7fd6ff,1.2f);console.Extra0=sign;
                for(int i=0;i<NativeDeployPresentation.Pads;i++){ref var l=ref Add(Model.Lamp,T(NativeDeployModels.ConsoleLampAt(i))*ca);l.Light=seat[i];}
            }
            // the party: on their pads facing the room's front; at Deploy each drops through its pad in turn down its lit tube into
            // its pod's seat and away with its pod (NativeDeployEject.Drop: only ever down), a burst of light up its shaft as it goes;
            // out of sight at travel
            Pilots=seats;
            for(int i=0;i<NativeDeployPresentation.Pads;i++)
            {
                var pad=NativeDeployPresentation.Pad(i);var front=NativeDeployEject.Facing(i);
                float drop=deployed?NativeDeployEject.Drop(i,since,cut):0;
                PilotFeet[i]=pad+new Vector3(0,NativeDeployModels.PadTop-drop,0);PilotFacing[i]=front;PilotShown[i]=i<seats&&!(since>=travel)?1:0;
                float burst=deployed?(since-(NativeDeployPresentation.DropAt+NativeDeployPresentation.DropStagger*i))/.5f:-1;
                if(i<seats&&burst>0&&burst<1)
                {
                    // the shaft turns to face the camera about its pad's vertical (a soft ribbon reads as light from any shot)
                    var toEye=Camera.Eye-pad;float yaw=(float)Math.Atan2(toEye.X,toEye.Z);
                    ref var beam=ref Add(Model.Beam,Matrix4x4.CreateRotationY(yaw)*T(pad));beam.Blend=Blend.Additive;beam.Screen=Col(0x9ff0ff,1.1f*(float)Math.Sin(Math.PI*burst));
                }
            }
            // the eject's set under the floor, after Deploy only: every occupied pad's tube, its pod's collar, roof hatch, seat, harness
            // bar and clamps, the belly over the pods, and the rolled city once the release shot looks down at it
            EjectSet(seats,since,cut,mine,t);
            // the slot sparks, drawn last (the station's own items always fit), and the rolls' moments heard for sound
            SparkCount=NativeDeploySparks.Instance(Sparks,0,f.SinceInstanceRoll,NativeDeployModels.Root(NativeDeployPresentation.InstanceMachineAt,NativeDeployPresentation.InstanceYaw),landColor,destinations,f.Reduced);
            SparkCount=NativeDeploySparks.Mutator(Sparks,SparkCount,f.SinceMutatorRoll,NativeDeployModels.Root(NativeDeployPresentation.MutatorMachineAt,NativeDeployPresentation.MutatorYaw),cardPolarity,cards,f.Reduced);
            for(int i=0;i<SparkCount&&Count<Capacity;i++)Streak(Sparks[i]);
            int moments=NativeDeploySparks.Schedule(schedule,0,destinations,null);CueCount=NativeDeploySparks.Heard(schedule,moments,heard[0],f.SinceInstanceRoll,Cues,0);heard[0]=f.SinceInstanceRoll;
            moments=NativeDeploySparks.Schedule(schedule,1,cards,cardPolarity);CueCount=NativeDeploySparks.Heard(schedule,moments,heard[1],f.SinceMutatorRoll,Cues,CueCount);heard[1]=f.SinceMutatorRoll;
            // the light: each machine's practical at its power (and its kick), the table, the gate opening, the console's button
            float consoleLevel=Launch==NativeDeployPresentation.Launch.Ready||Launch==NativeDeployPresentation.Launch.HostOnly?.6f+.4f*breath:Launch==NativeDeployPresentation.Launch.Launching?1:.1f;
            NativeDeployModels.StationLights(Lights,InstanceLook.Power*kickI,MutatorLook.Power*kickM,flicker,1,0,consoleLevel,dep.Dim);
            if(deployed)EjectLights(Shot,mine,since,cut,t,dep.Red);
            // the eject's moments on my pad, heard once (Runtime/NativeEjectAudio.cs)
            EjectCueCount=deployed?NativeEjectSounds.Heard(mine,cut,ejectHeard,since,EjectCues):0;ejectHeard=deployed?since:float.NaN;
        }

        // ---- the eject's set under the floor (NativeDeployEject; drawn only after Deploy) ----
        private void EjectSet(int seats,float since,NativeDeployEject.Cut cut,int mine,float t)
        {
            for(int i=0;i<NativeDeployPresentation.Pads;i++)PodShown[i]=false;
            if(!(since>=0))return;bool tube=Shot==NativeDeployPresentation.Shot.Tube,release=Shot==NativeDeployPresentation.Shot.Release;
            for(int i=0;i<seats&&i<NativeDeployPresentation.Pads&&Count<Capacity-12;i++)
            {
                var pad=NativeDeployPresentation.Pad(i);float landed=since-NativeDeployEject.LandAt(i),hatch=Deploy.Hatch;
                // the tube: its rings cool and dim, mine chasing up as I fall (three ring banks lit in turn)
                ref var tb=ref Add(NativeDeployEject.Deep(i)?Model.TubeDeep:Model.Tube,Matrix4x4.CreateRotationY(NativeDeployEject.Yaw(i))*T(new Vector3(pad.X,NativeDeployEject.TubeTop,pad.Z)));
                float level=.35f+.85f*hatch;
                if(i==mine&&tube){for(int b=0;b<3;b++){var c=Col(0xd4f2ff,level*(.4f+3.6f*NativeDeployEject.RingChase(b,since)));if(b==0)tb.Light=c;else if(b==1)tb.Extra0=c;else tb.Extra1=c;}}
                else{var c=Col(0xb8e6ff,level*(1.1f+.5f*NativeDeployEject.RingChase(i%3,since)));tb.Light=c;tb.Extra0=c;tb.Extra1=c;}
                // the dock: the collar on its pod's roof (amber, red once sealed) and the roof hatch's iris leaves sliding shut
                var dock=NativeDeployEject.DockRoot(i);float seal=NativeDeployEject.Seal(i,since);
                ref var collar=ref Add(Model.Collar,T(new Vector3(0,NativeDeployEject.PodHeight,0))*dock);collar.Light=Vector4.Lerp(Col(0xff9a2e,1.7f),Col(0xff3a3a,2.4f),seal);
                float k=NativeDeployEjectModels.IrisScale,open=(1-seal)*NativeDeployModels.HatchTravel*k,iy=NativeDeployEject.PodHeight+.19f-NativeDeployModels.PadTop-.001f;
                var leaf=Matrix4x4.CreateScale(k,1,k);var lit=Col(0xff3a3a,.6f+1.8f*seal);
                ref var irisA=ref Add(Model.Iris,leaf*T(new Vector3(open,iy,0))*dock);irisA.Light=lit;
                ref var irisB=ref Add(Model.Iris,leaf*Matrix4x4.CreateRotationY((float)Math.PI)*T(new Vector3(-open,iy,0))*dock);irisB.Light=lit;
                // the clamps: shut on the pod's shoulders, red; swung out green as it drops (Full), flashing as they fire
                float swing=NativeDeployEject.Clamps(i,since,cut)*NativeDeployEjectModels.ClampOpen,flash=NativeDeployEject.ReleaseFlash(i,since,cut);var h=NativeDeployEjectModels.ClampHinge;
                var tell=flash>0?Col(0xffffff,2+6*flash):Col(swing>0?0x6dffa8:0xff3a3a,1.6f);
                ref var ca=ref Add(Model.Clamp,Matrix4x4.CreateRotationZ(swing)*T(h)*dock);ca.Light=tell;
                ref var cb=ref Add(Model.Clamp,Matrix4x4.CreateRotationZ(swing)*Matrix4x4.CreateRotationY((float)Math.PI)*T(new Vector3(-h.X,h.Y,h.Z))*dock);cb.Light=tell;
                // the pod (the deploy cast draws it from the source-model bank), its seat (the strip red once its pilot is in) and
                // its harness bar (red while it comes down, green locked)
                var pod=NativeDeployEject.PodRoot(i,since,cut);PodWorld[i]=pod;PodShown[i]=true;
                ref var st=ref Add(Model.Seat,pod);st.Light=Col(0xff3a3a,landed>=0?.9f+1.8f*Smooth(landed/.25f):.55f);
                float bar=NativeDeployEject.Harness(i,since);
                ref var hb=ref Add(Model.Harness,Matrix4x4.CreateRotationX(NativeDeployEjectModels.HarnessAngle(bar))*T(NativeDeployEjectModels.HarnessPivot)*pod);hb.Light=bar>=1?Col(0x6dffa8,2.2f):Col(0xff3a3a,1.3f);
                if(flash>0)Glow(Vector3.Transform(new Vector3(0,NativeDeployEject.PodHeight,0),dock),2.2f,Col(0xfff0d8,2.4f*flash));
            }
            if(Count<Capacity-1){ref var belly=ref Add(Model.Belly,Matrix4x4.Identity);belly.Light=Col(0xbfe6ff,1.3f);}
            if(release&&Count<Capacity-1){ref var city=ref Add(Model.City,T(new Vector3(0,NativeDeployEject.CityY,0)));city.Light=Col(0xffffff,1.7f+.25f*(float)Math.Sin(t*1.7f));}
        }
        // A soft camera-facing glow (the release's flash at the clamps).
        private void Glow(Vector3 at,float size,Vector4 tint)
        {
            var d=at-Camera.Eye;float dl=d.Length();if(!(dl>1e-3f))return;d/=dl;var x=Vector3.Cross(Vector3.UnitY,d);x=x.LengthSquared()<1e-6f?Vector3.UnitX:Vector3.Normalize(x);var y=Vector3.Cross(d,x);
            ref var it=ref Add(Model.Quad,new Matrix4x4(x.X*size,x.Y*size,x.Z*size,0,y.X*size,y.Y*size,y.Z*size,0,d.X,d.Y,d.Z,0,at.X,at.Y,at.Z,1));
            it.Texture=TexGlow;it.Blend=Blend.Additive;it.Screen=tint;
        }
        // The eject's shots' light (the room's rig, StationLights, reddened while the room's shots hold; the tube, the seat and the
        // release their own): always a key and eight practicals, in the source frame.
        private void EjectLights(NativeDeployPresentation.Shot shot,int mine,float since,NativeDeployEject.Cut cut,float t,float red)
        {
            var l=Lights;var pad=NativeDeployPresentation.Pad(mine);
            Vector3 S(Vector3 v)=>NativeDeployModels.Source(v);Vector3 Hx(int hex,float k)=>NativeEntranceFx.Hex(hex,k);
            switch(shot)
            {
                case NativeDeployPresentation.Shot.Tube:
                {
                    l.Clear();l.Background=Hx(0x020308,1);l.Fog=Hx(0x04060c,1);l.FogNear=5;l.FogFar=26;l.Sky=Hx(0x5a7ab0,.55f);l.Ground=Hx(0x140c1a,.45f);
                    l.Directional(S(new Vector3(0,1,.2f)),Hx(0x9fc0ff,.25f));
                    // the lit rings round my pilot (their chase), the collar's glow below, the room's spill above
                    float depth=NativeDeployEject.Drop(mine,since,cut);int at=(int)Math.Floor((depth+.6f)/NativeDeployEjectModels.RingPitch);
                    for(int k=at-2;k<=at+2;k++)
                    {
                        if(k<0)continue;float y=NativeDeployEject.TubeTop-(.3f+k*NativeDeployEjectModels.RingPitch);if(y<NativeDeployEject.PodRoof(mine))continue;
                        l.Add(S(new Vector3(pad.X,y,pad.Z)),Hx(0xd4f2ff,1.2f+9*NativeDeployEject.RingChase(k%3,since)),2.6f);
                    }
                    l.Add(S(new Vector3(pad.X,NativeDeployEject.PodRoof(mine)+.5f,pad.Z)),Hx(0xff6a2a,9),4.5f);
                    l.Add(S(new Vector3(pad.X,1.2f,pad.Z)),Hx(0xff4a3a,26*(.6f+.4f*red)),6);
                    l.Add(S(Camera.Eye+new Vector3(0,.4f,0)),Hx(0xa8c8ff,2.4f),3);
                    return;
                }
                case NativeDeployPresentation.Shot.Seat:
                {
                    l.Clear();l.Background=Hx(0x020203,1);l.Fog=Hx(0x050306,1);l.FogNear=3;l.FogFar=16;l.Sky=Hx(0x40507a,.35f);l.Ground=Hx(0x1a0a0c,.4f);
                    l.Directional(S(new Vector3(0,1,.3f)),Hx(0xffd0c0,.12f));
                    var pod=NativeDeployEject.PodRoot(mine,since,cut);Vector3 P(float x,float y,float z)=>Vector3.Transform(new Vector3(x,y,z),pod);
                    float landed=since-NativeDeployEject.LandAt(mine),seal=NativeDeployEject.Seal(mine,since),cabin=Smooth(landed/.25f);
                    // the cabin goes red; the white from the roof hatch dies as it seals; a cool spill through the door's porthole
                    l.Add(S(P(0,NativeDeployEject.PodHeight-.55f,-.25f)),Hx(0xff2a1e,3+7*cabin),3.2f);
                    l.Add(S(P(.3f,1.1f,-.35f)),Hx(0xff3a2a,1.5f+3*cabin),2.2f);
                    l.Add(S(P(-.3f,1.1f,-.35f)),Hx(0xff3a2a,1.5f+3*cabin),2.2f);
                    l.Add(S(P(0,NativeDeployEject.PodHeight+.2f,0)),Hx(0xe6f0ff,12*(1-seal)),3.6f);
                    l.Add(S(P(0,1.4f,.75f)),Hx(0x7fa8ff,1.6f),2.4f);
                    l.Add(S(P(0,.45f,.55f)),Hx(0xff6a3a,1.2f*cabin),1.6f);
                    l.Add(S(P(.5f,1.9f,.3f)),Hx(0xffb48a,.8f),2.2f);
                    l.Add(S(P(-.5f,1.9f,.3f)),Hx(0xffb48a,.8f),2.2f);
                    return;
                }
                case NativeDeployPresentation.Shot.Release:
                {
                    l.Clear();l.Background=Hx(0x03050c,1);l.Fog=Hx(0x070b18,1);l.FogNear=70;l.FogFar=260;l.Sky=Hx(0x6078b8,.7f);l.Ground=Hx(0x2a1c20,.6f);
                    l.Directional(S(new Vector3(-.4f,1,-.5f)),Hx(0xb8c8ff,.55f));
                    float flash=NativeDeployEject.ReleaseFlash(mine,since,cut);var dock=new Vector3(pad.X,NativeDeployEject.PodRoof(mine),pad.Z);
                    l.Add(S(dock+new Vector3(0,.4f,0)),Hx(0xfff0d8,4+140*flash),9);
                    l.Add(S(new Vector3(pad.X+3,NativeDeployEjectModels.BellyBottom-.4f,pad.Z-2)),Hx(0xbfe6ff,40),14);
                    l.Add(S(new Vector3(pad.X-3,NativeDeployEjectModels.BellyBottom-.4f,pad.Z+2)),Hx(0xbfe6ff,40),14);
                    var podAt=Vector3.Transform(new Vector3(0,1.6f,0),NativeDeployEject.PodRoot(mine,since,cut));
                    l.Add(S(podAt+new Vector3(1.6f,-.5f,0)),Hx(0xffb45a,10),7);
                    l.Add(S(new Vector3(0,NativeDeployEject.CityY+40,0)),Hx(0xffa860,9000),220);
                    l.Add(S(new Vector3(30,NativeDeployEject.CityY+30,20)),Hx(0x6fc8ff,5000),200);
                    l.Add(S(new Vector3(-30,NativeDeployEject.CityY+30,-20)),Hx(0xff7a50,5000),200);
                    l.Add(S(new Vector3(pad.X,NativeDeployEjectModels.BellyBottom-2,pad.Z)),Hx(0x8fb0ff,14),10);
                    return;
                }
                default:
                {
                    // the room's own shots: the practicals bleed toward the alarm's red
                    if(red<=0)return;
                    for(int i=0;i<l.Points;i++){var p=l.Point[i];double sum=(p.R+p.G+p.B)/3;double k=.55*red;p.R=p.R*(1-k)+sum*2.1*k;p.G=p.G*(1-k)+sum*.35*k;p.B=p.B*(1-k)+sum*.3*k;l.Point[i]=p;}
                    return;
                }
            }
        }
        // A spark as a camera-facing quad: stretched along its streak as the camera sees it (round when it has none).
        private void Streak(in NativeDeploySparks.Spark sp)
        {
            var d=sp.At-Camera.Eye;float dl=d.Length();if(!(dl>1e-3f))return;d/=dl;
            var a=sp.Axis-d*Vector3.Dot(sp.Axis,d);float len=a.Length();Vector3 x,y;
            if(len>sp.Width*.25f){y=a/len;x=Vector3.Normalize(Vector3.Cross(y,d));}
            else{x=Vector3.Cross(Vector3.UnitY,d);x=x.LengthSquared()<1e-6f?Vector3.UnitX:Vector3.Normalize(x);y=Vector3.Cross(d,x);len=0;}
            float w=sp.Width,h=sp.Width+len;
            ref var it=ref Add(Model.Quad,new Matrix4x4(x.X*w,x.Y*w,x.Z*w,0,y.X*h,y.Y*h,y.Z*h,0,d.X,d.Y,d.Z,0,sp.At.X,sp.At.Y,sp.At.Z,1));
            it.Texture=TexSpark;it.Blend=Blend.Additive;it.Screen=sp.Color;
        }
    }
}
