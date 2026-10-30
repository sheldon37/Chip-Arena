using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // CONCEPT ONLY (docs/EXPEDITION-BOSS-CENTRAL-STATION.md). THE 6:47, the Central Station's boss: the evacuation special the
    // concourse waited for, which finally comes in out of the west tunnel with everyone who was aboard fused into it. A
    // station-green carriage (cream window band, brass lining, the yellow warning nose of the station's trains) whose windows
    // glow sodium amber with the passengers pressed against the glass; it walks on six legs grown out of its bogie wheels, the
    // front pair long enough to be arms; the Stationmaster stands waist-deep in its cab roof in his frock coat and top hat,
    // holding the stopped pocket watch and the last-call bell; station PA horns are grafted to the cab (the Voice of the
    // Station: every big move is announced with the PA chime). The torn rear end shows the Heart, the glowing mass of the
    // passengers (a weak point); the headlamp and the pantograph break.
    //
    // INERT: nothing in Runtime, Editor or any live Core path references these types (Tests/StationBossModelChecks holds that).
    // The model, its key poses and the proposed move table exist so the design can be rendered, reviewed and checked; the
    // rules, numbers, AI and network fields are proposals Astra owns (the doc's section 10 contract).
    //
    // Frame: +z the nose, +y up, +x the boss's right (the Unity frame; no mirror). The root sits on the floor under the
    // carriage's middle. Each bone's geometry is authored in its own frame at rest (rest rotations are identity, so a bone's
    // frame at rest is its offset alone); a pose sets each bone's local rotation and an extra local translation.

    /// <summary>The boss's identity and its proposed phases and moves (Astra owns every number; doc sections 4, 5 and 10).</summary>
    public static class NativeStationBoss
    {
        public const string Archetype="station-647",Name="THE 6:47",Subtitle="EVACUATION SPECIAL",Area="exp-transit",Map="expedition-transit";
        /// <summary>The three phases (LAST CALL, PLATFORM ALTERATION, TERMINUS) and the health fraction each starts at.</summary>
        public const int Phases=3;
        public static readonly string[] PhaseNames={"LAST CALL","PLATFORM ALTERATION","TERMINUS"};
        public static readonly float[] PhaseFrom={1f,.65f,.30f};
        /// <summary>The phase change: the boss is busy (proposed invulnerable) while the arena changes.</summary>
        public const float PhaseChangeSeconds=3.5f;
        /// <summary>The PA chime telegraph: four notes, the strike on the beat after the fourth (Departure and Terminus).</summary>
        public const int ChimeNotes=4;
        /// <summary>The phase a health fraction falls in (1..3).</summary>
        public static int Phase(float health){int p=1;for(int i=1;i<Phases;i++)if(health<=PhaseFrom[i])p=i+1;return p;}
        /// <summary>
        /// The moves as action timelines (seconds): anticipation (the telegraph), strike (the active window; travel for the
        /// charge), follow-through, recover; then the cooldown. Damage is in today's add units before party scaling. Reach,
        /// radius and arc as each move uses them; Speed for the charge and the shockwave. Chime: the PA chime's note spacing
        /// when the move is announced with it (0: no chime).
        /// </summary>
        public static readonly StationBossMove[] Moves={
            new StationBossMove{Id="buffer",Title="Buffer Swipe",Phase=1,Anticipation=.55f,Strike=.18f,Follow=.3f,Recover=.45f,Cooldown=3,Damage=20,Reach=4.4f,Arc=120},
            new StationBossMove{Id="doors",Title="All Aboard",Phase=1,Anticipation=.8f,Strike=.25f,Follow=1.5f,Recover=.6f,Cooldown=9,Damage=6,Reach=3.2f,Radius=1.6f},
            new StationBossMove{Id="departure",Title="Departure",Phase=1,Anticipation=1.2f,Strike=1.4f,Follow=.7f,Recover=.8f,Cooldown=11,Damage=34,Reach=22,Radius=1.6f,Speed=16,Chime=.3f},
            new StationBossMove{Id="alteration",Title="Platform Alteration",Phase=1,Anticipation=.6f,Strike=1.2f,Follow=.2f,Recover=.6f,Cooldown=14,Damage=18,Reach=40,Radius=1.4f},
            new StationBossMove{Id="shunt",Title="Shunt",Phase=2,Anticipation=.7f,Strike=.2f,Follow=.35f,Recover=.5f,Cooldown=6,Damage=24,Reach=5,Arc=70},
            new StationBossMove{Id="live-rail",Title="Live Rail",Phase=2,Anticipation=1.2f,Strike=4,Follow=.3f,Recover=.8f,Cooldown=18,Damage=8,Radius=2.2f},
            new StationBossMove{Id="terminus",Title="Terminus",Phase=3,Anticipation=1.4f,Strike=.15f,Follow=.85f,Recover=1,Cooldown=16,Damage=40,Reach=10,Radius=3,Speed=12,Chime=.35f},
            new StationBossMove{Id="passengers",Title="Next Stop",Phase=3,Anticipation=1,Strike=2,Follow=0,Recover=.5f,Cooldown=25,Damage=0,Radius=3}};
        public static StationBossMove Move(string id){foreach(var m in Moves)if(m.Id==id)return m;return default;}
    }

    /// <summary>One move's proposed action timeline (doc section 5; Astra owns the numbers).</summary>
    public struct StationBossMove
    {
        public string Id,Title;public int Phase;
        public float Anticipation,Strike,Follow,Recover,Cooldown,Damage,Reach,Radius,Arc,Speed,Chime;
        public float Duration=>Anticipation+Strike+Follow+Recover;
        /// <summary>The PA chime's notes before the strike (seconds after the move starts), or none.</summary>
        public float Note(int i)=>Chime>0?Anticipation-(NativeStationBoss.ChimeNotes-i)*Chime:float.NaN;
    }

    /// <summary>
    /// The motion direction as data (doc section 7): the springs each secondary channel runs on (frequency in Hz and damping
    /// ratio; under 1 overshoots), the tripod gait and its numbers, the chains. These are the values the shared boss motion
    /// library (claude/boss-motion) would be driven with; nothing here runs.
    /// </summary>
    public static class NativeStationBossMotion
    {
        public struct Channel{public string Id;public float Hz,Damping;}
        public static readonly Channel[] Springs={
            new Channel{Id="root-yaw",Hz=1.8f,Damping=.9f},new Channel{Id="root-position",Hz=2.2f,Damping=1f},
            new Channel{Id="body-heave",Hz=1.6f,Damping=.55f},new Channel{Id="body-lean",Hz=1.2f,Damping=.5f},
            new Channel{Id="stationmaster",Hz=2.2f,Damping=.35f},new Channel{Id="hat",Hz=3f,Damping=.3f},
            new Channel{Id="window-arms",Hz=1.4f,Damping=.25f},new Channel{Id="grab-arms",Hz=4f,Damping=.45f},
            new Channel{Id="pantograph",Hz=5f,Damping=.3f},new Channel{Id="headlamp-aim",Hz=3.5f,Damping=.8f}};
        /// <summary>The alternating tripods (legs by index: 0 left front, 1 right front, 2 left mid, 3 right mid, 4 left rear, 5
        /// right rear).</summary>
        public static readonly int[] TripodA={0,3,4},TripodB={1,2,5};
        /// <summary>Walk and charge: speed (m/s), turn rates (deg/s), stride (m), step heights (m), duty factors (the share
        /// of a cycle a foot is planted), and the re-step triggers when turning in place (the foot's yaw error, its reach).</summary>
        public const float WalkSpeed=2.6f,ChargeSpeed=16,TurnRate=70,TurnRateTerminus=95,Stride=1.9f,StepHeight=.45f,ChargeStepHeight=.75f,
            DutyWalk=.62f,DutyCharge=.45f,RestepYaw=25,RestepReach=.9f;
        /// <summary>Chains (verlet): the coupling chain's and the cables' link counts are the model's; gravity, damping and
        /// iterations; the Heart's additive pulse rate per phase.</summary>
        public const float ChainDamping=.02f,ChainGravity=9.8f;public const int ChainIterations=3;
        public static readonly float[] HeartPulseHz={.75f,1f,1.4f};
    }

    /// <summary>A pose of the boss: each bone's local rotation and extra local translation, and the root's placement.</summary>
    public sealed class StationBossPose
    {
        public readonly Quaternion[] Rot=new Quaternion[NativeStationBossModel.Count];
        public readonly Vector3[] Move=new Vector3[NativeStationBossModel.Count];
        public Vector3 Root;public float Yaw;
        public StationBossPose(){Reset();}
        /// <summary>Every bone at rest (identity; the legs hang straight: apply a key pose for a standing boss).</summary>
        public void Reset(){for(int i=0;i<Rot.Length;i++){Rot[i]=Quaternion.Identity;Move[i]=Vector3.Zero;}Root=Vector3.Zero;Yaw=0;}
    }

    /// <summary>The key poses the concept is drawn and checked in (doc section 7).</summary>
    public enum StationBossKey{Stand,Walk,Rolling,Arrival,DepartureCrouch,DepartureCharge,DepartureBrake,Swipe,AllAboard,Alteration,LiveRail,Shunt,TerminusRear,TerminusSlam,Opened,Stagger,Death}

    /// <summary>THE 6:47's model: its skeleton, its geometry (three tiers), its key poses and the hit volumes it is held to.</summary>
    public static class NativeStationBossModel
    {
        // ---------------- the skeleton ----------------
        public const int Root=0,Body=1,Lamp=2,Heart=3,Roof=4,PantoLow=5,PantoHigh=6,PantoHead=7,
            SmHips=8,SmChest=9,SmHead=10,SmHat=11,SmLArm=12,SmLFore=13,SmLHand=14,SmRArm=15,SmRFore=16,SmRHand=17,
            WheelFirst=18,HipFirst=24,KneeFirst=30,FootFirst=36,DoorLF=42,DoorLB=43,DoorRF=44,DoorRB=45,GrabL0=46,GrabL1=47,GrabR0=48,GrabR1=49,
            ChainFirst=50,CableAFirst=54,CableBFirst=57,ArmFirst=60,Count=68;
        public const int Legs=6,ChainLinks=4,CableLinks=3,WindowArms=4;
        public static int Wheel(int leg)=>WheelFirst+leg;public static int Hip(int leg)=>HipFirst+leg;public static int Knee(int leg)=>KneeFirst+leg;public static int Foot(int leg)=>FootFirst+leg;
        /// <summary>A leg's side (-1 left, +1 right) and row (0 front, 1 mid, 2 rear).</summary>
        public static int Side(int leg)=>(leg&1)==0?-1:1;public static int Row(int leg)=>leg>>1;
        public static float Thigh(int leg)=>Row(leg)==0?2.6f:2.7f;public static float Shin(int leg)=>Row(leg)==0?3.3f:3.5f;

        // ---------------- dimensions (metres) ----------------
        /// <summary>The carriage: half width, the torn rear, the cab, the nose; the underframe's height standing; the hand's
        /// lift (the wrist above the floor when a foot is planted).</summary>
        public const float HalfWidth=1.45f,RearZ=-4.3f,CabZ=3.05f,NoseZ=4.05f,BodyLift=1.55f,PalmLift=.14f;
        public static readonly float[] RowZ={3.45f,.4f,-2.85f};
        /// <summary>The Stationmaster's scale over a pilot's proportions (he is swollen, fused into the cab).</summary>
        public const float Rider=1.3f;
        public static readonly float[] WindowZ={-3.45f,-2.35f,.95f,2.05f};
        /// <summary>The proposed hit volumes: the carriage's box in the body bone's frame (it pitches, rolls and heaves with the
        /// body; the plough, the roof arms and the hatch lid inside it) and the Stationmaster's capsule (chest to hat, model space,
        /// standing). The legs, wheels, chain, cables and window arms are limbs outside them.</summary>
        public static readonly Vector3 BodyBoxMin=new Vector3(-1.62f,-.45f,-4.9f),BodyBoxMax=new Vector3(1.62f,3.85f,5.75f),RiderA=new Vector3(0,4.6f,2.35f),RiderB=new Vector3(0,6.75f,2.4f);
        public const float RiderRadius=.8f;
        /// <summary>The proposed navigation footprint (model space): the box a standing or walking boss stays in, hands, peeled roof
        /// and all; and the reach box every key pose stays in (the rear-up of Terminus, the slam, the charge, the rolling arrival).</summary>
        public static readonly Vector3 FootprintMin=new Vector3(-5.7f,-.12f,-6.8f),FootprintMax=new Vector3(5.7f,9.0f,7.6f),ReachMin=new Vector3(-6.2f,-1.32f,-9.8f),ReachMax=new Vector3(6.2f,10.4f,9.2f);
        /// <summary>Triangle budgets per tier (full, reduced, low): the one boss on screen, about two and a half pilots at full detail
        /// (the Standard pilot is 7,782; an elite add 3,500-5,100).</summary>
        public static readonly int[] Budget={20500,10500,4800};

        public static readonly int[] Parent=new int[Count];public static readonly Vector3[] Rest=new Vector3[Count];public static readonly string[] Names=new string[Count];
        static NativeStationBossModel()
        {
            void B(int i,string name,int parent,float x,float y,float z){Names[i]=name;Parent[i]=parent;Rest[i]=new Vector3(x,y,z);}
            B(Root,"root",-1,0,0,0);B(Body,"body",Root,0,BodyLift,0);B(Lamp,"lamp",Body,0,1.0f,5.22f);B(Heart,"heart",Body,0,1.25f,-4.05f);B(Roof,"roof",Body,0,2.58f,-3.65f);
            B(PantoLow,"panto-low",Roof,0,.36f,1.05f);B(PantoHigh,"panto-high",PantoLow,0,0,1.5f);B(PantoHead,"panto-head",PantoHigh,0,0,-1.5f);
            B(SmHips,"sm-hips",Body,0,2.7f,2.35f);B(SmChest,"sm-chest",SmHips,0,.36f*Rider,0);B(SmHead,"sm-head",SmChest,0,.66f*Rider,.03f*Rider);B(SmHat,"sm-hat",SmHead,0,.34f*Rider,.01f*Rider);
            B(SmLArm,"sm-l-arm",SmChest,-.47f*Rider,.52f*Rider,0);B(SmLFore,"sm-l-fore",SmLArm,0,-.5f*Rider,0);B(SmLHand,"sm-l-hand",SmLFore,0,-.46f*Rider,0);
            B(SmRArm,"sm-r-arm",SmChest,.47f*Rider,.52f*Rider,0);B(SmRFore,"sm-r-fore",SmRArm,0,-.5f*Rider,0);B(SmRHand,"sm-r-hand",SmRFore,0,-.46f*Rider,0);
            for(int i=0;i<Legs;i++)
            {
                int s=Side(i),r=Row(i);float y=r==0?.5f:.22f,x=r==0?1.58f:1.66f;string n=(s<0?"l":"r")+"-"+(r==0?"front":r==1?"mid":"rear");
                B(Wheel(i),"wheel-"+n,Body,s*(x-.14f),y,RowZ[r]);B(Hip(i),"hip-"+n,Body,s*x,y,RowZ[r]);B(Knee(i),"knee-"+n,Hip(i),0,-Thigh(i),0);B(Foot(i),"foot-"+n,Knee(i),0,-Shin(i),0);
            }
            B(DoorLF,"door-l-front",Body,-1.465f,1.33f,-.025f);B(DoorLB,"door-l-back",Body,-1.465f,1.33f,-.675f);B(DoorRF,"door-r-front",Body,1.465f,1.33f,-.025f);B(DoorRB,"door-r-back",Body,1.465f,1.33f,-.675f);
            B(GrabL0,"grab-l",Body,-.25f,1.25f,-.35f);B(GrabL1,"grab-l-hands",GrabL0,-1.15f,0,0);B(GrabR0,"grab-r",Body,.25f,1.25f,-.35f);B(GrabR1,"grab-r-hands",GrabR0,1.15f,0,0);
            for(int k=0;k<ChainLinks;k++)B(ChainFirst+k,"chain-"+k,k==0?Body:ChainFirst+k-1,0,k==0?.3f:-.34f,k==0?-4.42f:0);
            for(int k=0;k<CableLinks;k++){B(CableAFirst+k,"cable-a-"+k,k==0?Body:CableAFirst+k-1,k==0?-.95f:0,k==0?2.42f:-.62f,k==0?-4.36f:0);B(CableBFirst+k,"cable-b-"+k,k==0?Body:CableBFirst+k-1,k==0?.78f:0,k==0?2.5f:-.62f,k==0?-4.36f:0);}
            float[] armZ={-2.35f,2.05f,-3.45f,.95f};
            for(int a=0;a<WindowArms;a++){int s=a<2?-1:1;B(ArmFirst+a*2,"window-arm-"+a,Body,s*1.47f,1.3f,armZ[a]);B(ArmFirst+a*2+1,"window-hand-"+a,ArmFirst+a*2,0,-.56f,0);}
        }

        // ---------------- world transforms ----------------
        private const float Deg=(float)(Math.PI/180);
        /// <summary>Each bone's world matrix (row vectors: a point times the matrix); `placed` false leaves out the root's
        /// placement (model space).</summary>
        public static void World(StationBossPose p,Matrix4x4[] w,bool placed=true)
        {
            var root=placed?Matrix4x4.CreateRotationY(p.Yaw*Deg)*Matrix4x4.CreateTranslation(p.Root):Matrix4x4.Identity;
            for(int i=0;i<Count;i++){var local=Matrix4x4.CreateFromQuaternion(p.Rot[i])*Matrix4x4.CreateTranslation(Rest[i]+p.Move[i]);w[i]=Parent[i]<0?local*root:local*w[Parent[i]];}
        }
        /// <summary>Every vertex of `mesh` posed by `p` (model space unless placed), with its bone.</summary>
        public static void Posed(WeaponMesh mesh,StationBossPose p,List<Vector3> points,List<int> bones=null,bool placed=false)
        {
            var w=new Matrix4x4[Count];World(p,w,placed);
            foreach(var s in mesh.Sections)foreach(var v in s.Positions){points.Add(Vector3.Transform(v,w[s.Bone]));bones?.Add(s.Bone);}
        }
        private static Matrix4x4 Rotation(Matrix4x4 m){m.M41=0;m.M42=0;m.M43=0;return m;}
        // A frame whose local -y runs along `along` and whose local x is the hinge (orthogonalised).
        private static Matrix4x4 Frame(Vector3 along,Vector3 hinge)
        {
            var y=Vector3.Normalize(-along);var x=hinge-y*Vector3.Dot(hinge,y);
            if(x.LengthSquared()<1e-8f)x=Math.Abs(y.Z)<.9f?Vector3.Cross(y,Vector3.UnitZ):Vector3.Cross(y,Vector3.UnitX);x=Vector3.Normalize(x);var z=Vector3.Cross(x,y);
            return new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);
        }
        /// <summary>
        /// Two-bone IK for one leg (authoring only; the motion library's gait does this live): the wrist reaches `target`
        /// (model space; a planted hand's wrist sits <see cref="PalmLift"/> over the floor), the knee bends toward `pole`. A
        /// flat hand lies palm down turned `footYaw` (degrees from +z; NaN: the row's outward default); otherwise the fingers
        /// carry on along the shin. A target out of reach is clamped along its line.
        /// </summary>
        public static void Leg(StationBossPose p,int leg,Vector3 target,Vector3 pole,bool flat=true,float footYaw=float.NaN)
        {
            var w=new Matrix4x4[Count];World(p,w,false);int hip=Hip(leg),knee=Knee(leg),foot=Foot(leg);
            var h=w[hip].Translation;float l1=Thigh(leg),l2=Shin(leg);var d=target-h;float dist=d.Length();var dir=dist>1e-5f?d/dist:-Vector3.UnitY;
            dist=Math.Max(Math.Abs(l1-l2)+.02f,Math.Min(l1+l2-.02f,dist));
            float a=(l1*l1+dist*dist-l2*l2)/(2*dist),up=(float)Math.Sqrt(Math.Max(0,l1*l1-a*a));
            var pp=pole-dir*Vector3.Dot(pole,dir);if(pp.LengthSquared()<1e-8f)pp=Vector3.UnitY-dir*dir.Y;pp=Vector3.Normalize(pp);
            var k=h+dir*a+pp*up;var f=h+dir*dist;var hinge=Vector3.Cross(k-h,f-k);if(hinge.LengthSquared()<1e-8f)hinge=Vector3.Cross(dir,pp);
            var thigh=Frame(k-h,hinge);var shin=Frame(f-k,hinge);var body=Rotation(w[Parent[hip]]);
            p.Rot[hip]=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(thigh*Matrix4x4.Transpose(body)));
            p.Rot[knee]=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(shin*Matrix4x4.Transpose(thigh)));
            var hand=flat?Matrix4x4.CreateRotationY((float.IsNaN(footYaw)?FootYaw(leg):footYaw)*Deg):Matrix4x4.CreateRotationX(90*Deg)*shin;
            p.Rot[foot]=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(hand*Matrix4x4.Transpose(shin)));
        }
        /// <summary>A planted hand's default turn: the fingers splay outward, raked forward at the front and back at the rear.</summary>
        public static float FootYaw(int leg)=>Side(leg)*(Row(leg)==0?35:Row(leg)==1?90:145);
        /// <summary>Puts a bone at a model-space world matrix (a dropped hat), whatever its parents do.</summary>
        public static void Place(StationBossPose p,int bone,Matrix4x4 world)
        {
            var w=new Matrix4x4[Count];World(p,w,false);Matrix4x4.Invert(w[Parent[bone]],out var inv);var local=world*inv;
            p.Move[bone]=local.Translation-Rest[bone];p.Rot[bone]=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(Rotation(local)));
        }

        // ---------------- key poses (doc section 7) ----------------
        private static void R(StationBossPose p,int bone,float pitch,float yaw=0,float roll=0)=>p.Rot[bone]=Quaternion.CreateFromYawPitchRoll(yaw*Deg,pitch*Deg,roll*Deg);
        private static void Lean(StationBossPose p,float dx,float dy,float dz,float pitch,float yaw=0,float roll=0){p.Move[Body]=new Vector3(dx,dy,dz);R(p,Body,pitch,yaw,roll);}
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private static Vector3 Pole(int leg)=>Row(leg)==0?Vector3.Normalize(V(Side(leg)*.85f,.6f,-.15f)):Vector3.Normalize(V(Side(leg)*.45f,1,Row(leg)==2?-.2f:0));
        /// <summary>The standing stance's planted wrists (model space).</summary>
        public static Vector3 Stance(int leg){int s=Side(leg),r=Row(leg);return r==0?V(s*3.55f,PalmLift,6.4f):r==1?V(s*4.6f,PalmLift,.7f):V(s*4.3f,PalmLift,-4.3f);}
        private static void Plant(StationBossPose p,int leg,float x,float z,float yaw=float.NaN)=>Leg(p,leg,V(x,PalmLift,z),Pole(leg),true,yaw);
        private static void Stand(StationBossPose p){for(int i=0;i<Legs;i++){var t=Stance(i);Plant(p,i,t.X,t.Z);}}
        // The secondary parts at rest: the Stationmaster holding the watch and the bell, the pantograph folded, the grab arms
        // folded inside, the chain and cables hanging back, the window arms dangling out of the glass.
        private static void Neutral(StationBossPose p)
        {
            R(p,SmChest,4);R(p,SmHead,-4);R(p,SmHat,0,0,4);R(p,SmRArm,-35,0,12);R(p,SmRFore,-75);R(p,SmLArm,-10,0,-28);R(p,SmLFore,-35);
            R(p,PantoLow,-6);R(p,PantoHigh,12);R(p,PantoHead,-6);R(p,GrabL1,0,180);R(p,GrabR1,0,180);
            for(int k=0;k<ChainLinks;k++)R(p,ChainFirst+k,k==0?6:3);
            float[] a={28,20,14},b={22,18,10};for(int k=0;k<CableLinks;k++){R(p,CableAFirst+k,a[k],0,k==0?6:0);R(p,CableBFirst+k,b[k],0,k==0?-8:0);}
            for(int a2=0;a2<WindowArms;a2++){int s=a2<2?-1:1;R(p,ArmFirst+a2*2,a2%2==0?-8:10,0,s*(78+a2*4));R(p,ArmFirst+a2*2+1,0,0,-s*(62+a2*3));}
        }
        private static void Peeled(StationBossPose p,float rise=.42f){R(p,Roof,-115);p.Move[SmHips]=V(0,rise,0);}
        public static StationBossPose Key(StationBossKey key){var p=new StationBossPose();Apply(p,key);return p;}
        public static void Apply(StationBossPose p,StationBossKey key)
        {
            p.Reset();Neutral(p);
            switch(key)
            {
                case StationBossKey.Stand:Stand(p);break;
                case StationBossKey.Walk:
                    Lean(p,0,-.08f,0,1.5f,0,2.5f);
                    Leg(p,0,V(-3.1f,PalmLift+.62f,5.75f),Pole(0));Leg(p,3,V(3.75f,PalmLift+.55f,1.3f),Pole(3));Leg(p,4,V(-3.5f,PalmLift+.5f,-3.0f),Pole(4));
                    Plant(p,1,3.15f,4.4f);Plant(p,2,-3.85f,-.05f);Plant(p,5,3.6f,-4.4f);
                    R(p,SmChest,4,0,-3);R(p,ArmFirst,-25,0,-78);R(p,ArmFirst+4,20,0,82);break;
                case StationBossKey.Rolling:
                    Lean(p,0,-2.25f,0,0);p.Move[SmHips]=V(0,-.32f,0);R(p,SmChest,22);R(p,SmHead,-20);for(int k=0;k<ChainLinks;k++)R(p,ChainFirst+k,k==0?72:4);
                    for(int i=0;i<Legs;i++){int s=Side(i),r=Row(i);float z=RowZ[r]+(r==0?.4f:r==1?.1f:-.3f);Leg(p,i,V(s*2.35f,r==0?-.62f:-.92f,z),Vector3.Normalize(V(s*.15f,1,r==0?.25f:r==2?-.25f:0)),true,s*(r==2?150:30));}
                    break;
                case StationBossKey.Arrival:
                    Lean(p,0,.75f,-.2f,-16);
                    Leg(p,0,V(-3.0f,1.7f,7.3f),Vector3.Normalize(V(-1,.4f,-.2f)),false);Leg(p,1,V(3.2f,2.2f,7.1f),Vector3.Normalize(V(1,.4f,-.2f)),false);
                    Plant(p,2,-3.7f,1.4f);Plant(p,3,3.7f,1.4f);Plant(p,4,-3.4f,-3.4f);Plant(p,5,3.4f,-3.4f);
                    for(int k=0;k<ChainLinks;k++)R(p,ChainFirst+k,k==0?34:3);R(p,SmChest,-12);R(p,SmHead,-16);R(p,SmRArm,-160,0,15);R(p,SmRFore,-10);R(p,SmLArm,-30,0,-70);R(p,SmLFore,-20);break;
                case StationBossKey.DepartureCrouch:
                    Lean(p,0,-.55f,.2f,6);Plant(p,0,-2.7f,6.6f,-15);Plant(p,1,2.7f,6.6f,15);Plant(p,2,-3.75f,1.1f);Plant(p,3,3.75f,1.1f);Plant(p,4,-3.1f,-3.0f);Plant(p,5,3.1f,-3.0f);
                    R(p,SmChest,22);R(p,SmHead,-18);R(p,SmRArm,-88,0,6);R(p,SmRFore,-8);R(p,SmLArm,-140,0,-12);R(p,SmLFore,-70);break;
                case StationBossKey.DepartureCharge:
                    Lean(p,0,-.25f,0,9);
                    for(int i=0;i<Legs;i++){int s=Side(i),r=Row(i);if(r==0)Leg(p,i,V(s*2.3f,1.1f,7.5f),Vector3.Normalize(V(s*.6f,.5f,-.6f)));else if(r==1)Leg(p,i,V(s*3.4f,.75f,-.2f),Pole(i));else Plant(p,i,s*2.8f,-6.0f,s*170);}
                    for(int k=0;k<ChainLinks;k++)R(p,ChainFirst+k,k==0?55:10);R(p,CableAFirst,70);R(p,CableBFirst,66);
                    for(int a=0;a<WindowArms;a++){int s=a<2?-1:1;R(p,ArmFirst+a*2,55,0,s*70);}
                    R(p,SmChest,28);R(p,SmHead,-25);R(p,SmRArm,-80,0,8);R(p,SmLArm,-140,0,-12);R(p,SmLFore,-70);break;
                case StationBossKey.DepartureBrake:
                    Lean(p,0,-.35f,.1f,4,14,-9);
                    Plant(p,0,-2.4f,6.5f,-5);Plant(p,1,3.6f,5.7f,45);Plant(p,2,-3.6f,.3f);Plant(p,3,4.4f,1.9f);Plant(p,4,-4.9f,-2.6f);Plant(p,5,2.3f,-4.0f);
                    for(int k=0;k<ChainLinks;k++)R(p,ChainFirst+k,6,0,k==0?-40:-8);R(p,CableAFirst,24,0,-30);R(p,CableBFirst,20,0,-34);
                    R(p,SmChest,18,0,14);R(p,SmHead,-6,0,-8);R(p,SmHat,15,0,-25);p.Move[SmHat]=V(.05f,-.02f,.05f);break;
                case StationBossKey.Swipe:
                    Lean(p,0,-.1f,0,2,-16,6);Plant(p,0,-3.3f,4.6f);Leg(p,1,V(-1.3f,2.3f,6.9f),Vector3.Normalize(V(1,-.2f,.6f)),false);
                    Plant(p,2,-3.9f,.2f);Plant(p,3,3.7f,1.0f);Plant(p,4,-3.4f,-3.9f);Plant(p,5,3.7f,-3.4f);
                    R(p,SmChest,6,-18);R(p,SmRArm,-70,0,40);R(p,SmRFore,-20);break;
                case StationBossKey.AllAboard:
                    Lean(p,0,-.12f,0,0,0,-9);p.Move[DoorLF]=V(0,0,.6f);p.Move[DoorLB]=V(0,0,-.6f);p.Move[GrabL0]=V(-1.45f,-.05f,0);R(p,GrabL0,0,0,18);R(p,GrabL1,0,0,8);
                    Plant(p,0,-4.0f,5.0f);Plant(p,1,3.0f,4.9f);Plant(p,2,-4.4f,1.4f);Plant(p,3,3.6f,.5f);Plant(p,4,-4.1f,-3.9f);Plant(p,5,3.4f,-3.7f);
                    R(p,SmHead,-4,-35);R(p,SmLArm,-5,0,-88);R(p,SmLFore,-10);break;
                case StationBossKey.Alteration:
                    Lean(p,0,.3f,0,-8);Leg(p,0,V(-2.9f,2.9f,6.0f),Vector3.Normalize(V(-1,-.3f,-.3f)),false);Leg(p,1,V(2.9f,2.9f,6.0f),Vector3.Normalize(V(1,-.3f,-.3f)),false);
                    Plant(p,2,-3.9f,.9f);Plant(p,3,3.9f,.9f);Plant(p,4,-3.6f,-3.6f);Plant(p,5,3.6f,-3.6f);
                    R(p,SmHead,-30);R(p,SmRArm,-170,0,8);R(p,SmRFore,-5);R(p,SmLArm,-40,0,-60);R(p,SmLFore,-30);break;
                case StationBossKey.LiveRail:
                    Lean(p,0,-.35f,0,5);R(p,PantoLow,-52);R(p,PantoHigh,104);R(p,PantoHead,-52);
                    Plant(p,0,-1.85f,6.3f,-10);Plant(p,1,1.85f,6.3f,10);Plant(p,2,-4.0f,.7f);Plant(p,3,4.0f,.7f);Plant(p,4,-3.7f,-3.8f);Plant(p,5,3.7f,-3.8f);
                    R(p,SmHead,-20);R(p,SmLArm,-10,0,-100);R(p,SmRArm,-10,0,100);R(p,SmLFore,-15);R(p,SmRFore,-15);break;
                case StationBossKey.Shunt:
                    Lean(p,0,-.15f,.55f,3);Leg(p,0,V(-1.0f,2.7f,7.4f),Vector3.Normalize(V(-1,-.5f,-.3f)),false);Leg(p,1,V(1.0f,2.7f,7.4f),Vector3.Normalize(V(1,-.5f,-.3f)),false);
                    Plant(p,2,-3.9f,.1f);Plant(p,3,3.9f,.1f);Plant(p,4,-3.6f,-4.4f);Plant(p,5,3.6f,-4.4f);
                    R(p,SmChest,-8);R(p,SmLArm,-140,0,-12);R(p,SmLFore,-70);break;
                case StationBossKey.TerminusRear:
                    Lean(p,0,1.85f,-.6f,-28);Peeled(p);
                    Leg(p,0,V(-2.4f,8.3f,4.6f),Vector3.Normalize(V(-1,-.2f,-1)),false);Leg(p,1,V(2.4f,8.3f,4.6f),Vector3.Normalize(V(1,-.2f,-1)),false);
                    Plant(p,2,-3.9f,-.4f);Plant(p,3,3.9f,-.4f);Plant(p,4,-3.5f,-4.2f);Plant(p,5,3.5f,-4.2f);
                    R(p,SmChest,-6);R(p,SmHead,-35);R(p,SmRArm,-175,0,10);R(p,SmLArm,-175,0,-10);R(p,SmRFore,0);R(p,SmLFore,0);
                    for(int k=0;k<ChainLinks;k++)R(p,ChainFirst+k,k==0?36:2);R(p,CableAFirst,60);R(p,CableBFirst,56);break;
                case StationBossKey.TerminusSlam:
                    Lean(p,0,-.3f,.6f,10);Peeled(p);Plant(p,0,-1.9f,7.7f,-5);Plant(p,1,1.9f,7.7f,5);Plant(p,2,-4.0f,1.2f);Plant(p,3,4.0f,1.2f);Plant(p,4,-3.6f,-3.2f);Plant(p,5,3.6f,-3.2f);
                    R(p,SmChest,32);R(p,SmHead,-10);R(p,SmRArm,-60,0,10);R(p,SmLArm,-60,0,-10);for(int k=0;k<ChainLinks;k++)R(p,ChainFirst+k,k==0?-15:-4);break;
                case StationBossKey.Opened:
                    Stand(p);Peeled(p,.45f);R(p,SmLArm,-150,0,-20);R(p,SmLFore,-10);R(p,SmRArm,-60,0,30);R(p,SmHead,-12);break;
                case StationBossKey.Stagger:
                    Lean(p,0,.06f,0,11,0,-11);Plant(p,0,-2.0f,4.3f);Leg(p,0,V(-2.0f,PalmLift,4.3f),Vector3.Normalize(V(-.4f,1,.9f)));
                    Plant(p,1,3.9f,5.9f);Plant(p,2,-3.4f,.8f);Plant(p,3,4.2f,.3f);Plant(p,4,-3.6f,-3.9f);Plant(p,5,3.6f,-3.9f);
                    R(p,SmChest,38,0,-12);R(p,SmHead,30);R(p,SmRArm,-10,0,5);R(p,SmLArm,-5,0,-5);R(p,SmRFore,0);R(p,SmLFore,0);break;
                case StationBossKey.Death:
                    Lean(p,0,-.74f,0,3,0,-4);Peeled(p,.2f);
                    for(int i=0;i<Legs;i++){int s=Side(i),r=Row(i);var t=r==0?V(s*4.3f,PalmLift,6.2f):r==1?V(s*5.0f,PalmLift,.5f):V(s*4.6f,PalmLift,-4.6f);Leg(p,i,t,Vector3.Normalize(V(s*.7f,.6f,r==0?.2f:0)));}
                    R(p,SmChest,75);R(p,SmHead,25);R(p,SmRArm,-60);R(p,SmLArm,-50);R(p,SmRFore,0);R(p,SmLFore,0);
                    Place(p,SmHat,Matrix4x4.CreateRotationZ(84*Deg)*Matrix4x4.CreateRotationY(40*Deg)*Matrix4x4.CreateTranslation(2.3f,.42f,7.3f));for(int k=0;k<ChainLinks;k++)R(p,ChainFirst+k,k==0?78:4);break;
            }
        }

        // ---------------- the geometry ----------------
        public static WeaponMesh Build(int tier)
        {
            tier=Math.Max(0,Math.Min(2,tier));var mesh=new WeaponMesh{Spec=NativeStationBoss.Archetype,Tier=tier};var b=new Builder(mesh,tier);
            b.Carriage();b.Cab();b.Underframe();b.Roof();b.Pantograph();b.Stationmaster();for(int i=0;i<Legs;i++)b.Leg(i);b.Doors();b.Rear();b.WindowArms();
            return mesh;
        }

        // The palette (sRGB): the station's green, cream and brass, the trains' yellow nose, sodium amber light inside.
        public const int LiveryHex=0x2C463C,CreamHex=0xB6AA8C,BrassHex=0xC8A040,GoldHex=0xD4AE48,IronHex=0x17191B,SteelHex=0x8A949A,RustHex=0x6A3A22,RoofHex=0x262C30,
            YellowHex=0xD2A41C,AmberHex=0xFFB45A,CoreHex=0xFFD27A,EyeHex=0xFFC070,HeadlampHex=0xF2F6FF,MarkerHex=0xFFE8C0,LiveHex=0x6FD8FF,FleshHex=0x6B7366,FleshDarkHex=0x4A5148,
            CoatHex=0x111317,SkinHex=0xA2A698;
        private static readonly int[] ClothHex={0x1E2636,0x3A2E24,0x44474E,0x55503A,0x4A1E24,0x26302A};

        private sealed class Builder
        {
            private readonly WeaponSculpt s;private readonly int tier;
            private bool Fine=>tier==0;private bool Mid=>tier<=1;
            public Builder(WeaponMesh mesh,int tier){s=new WeaponSculpt(mesh,tier);this.tier=tier;}
            private static WeaponPaint P(int hex,float sheen=0)=>WeaponPaint.Solid(hex,sheen);
            private static WeaponPaint G(int hex,float k,float albedo=.4f)=>WeaponPaint.Glow(hex,k,albedo);
            private readonly WeaponPaint green=P(LiveryHex,.02f),cream=P(CreamHex,.03f),brass=P(BrassHex,.25f),gold=P(GoldHex,.3f),iron=P(IronHex,.03f),steel=P(SteelHex,.12f),rust=P(RustHex),
                roof=P(RoofHex,.02f),yellow=P(YellowHex,.04f),black=P(0x050607),frame=P(0x101214),shadow=P(0x0A0807),lining=P(0x14110E),flesh=P(FleshHex,.03f),fleshDark=P(FleshDarkHex,.02f),
                coat=P(CoatHex,.05f),coatDark=P(0x08090B),skin=P(SkinHex,.04f),skinDark=P(0x7C8074),hair=P(0x8E8C86),glove=P(0xBDB6A2,.03f),hat=P(0x0A0B0D,.12f),
                rubber=P(0x0E0F10),cable=P(0x121316,.05f),copper=P(0xC07A3A,.2f),insulator=P(0xD8D6CC,.06f),soot=P(0x101112),amber=G(AmberHex,2.1f),vein=G(AmberHex,1.5f,.5f);
            private void On(int bone)=>s.Into(bone);
            private void Box(in WeaponPaint p,float x,float y,float z,float sx,float sy,float sz,float bevel=0,float rx=0,float ry=0,float rz=0)
            {
                if(rx==0&&ry==0&&rz==0)s.Box(p,new Vector3(x,y,z),new Vector3(sx,sy,sz),bevel);else s.Box(p,new Vector3(x,y,z),new Vector3(sx,sy,sz),bevel,rx*Deg,ry*Deg,rz*Deg);
            }
            private void Ball(in WeaponPaint p,float x,float y,float z,float rx,float ry,float rz)=>s.Sphere(p,new Vector3(x,y,z),new Vector3(rx,ry,rz),Fine?5:Mid?4:3,Fine?9:Mid?7:5,!Fine);
            private void CylZ(in WeaponPaint p,float r,float z0,float z1,float x,float y,int sides=16,float bevel=0)=>s.Cylinder(p,r,z0,z1,s.Sides(sides),bevel,x,y);
            private void CylY(in WeaponPaint p,float r,float y0,float y1,float x,float z,int sides=12){s.Push(WeaponSculpt.Rx(-90*Deg)*WeaponSculpt.T(x,0,z));s.Cylinder(p,r,y0,y1,s.Sides(sides));s.Pop();}
            // Block text on an axis-aligned face: u runs along the reading direction, v up, n out of the face.
            private void Word(in WeaponPaint p,string text,Vector3 o,Vector3 u,Vector3 v,Vector3 n,float cell,float depth)
            {
                float width=text.Length*4*cell-cell,start=-width/2;var au=Vector3.Abs(u);var av=Vector3.Abs(v);var an=Vector3.Abs(n);
                for(int i=0;i<text.Length;i++)foreach(var g in NativeExpeditionStationKit.Glyph(text[i]))
                {
                    var c=o+u*(start+(i*4+g[0]+g[2]*.5f)*cell)+v*((2.5f-g[1]-g[3]*.5f)*cell)+n*(depth*.5f);var size=au*(g[2]*cell)+av*(g[3]*cell)+an*depth;
                    s.Box(p,c,size,0);
                }
            }

            // ---- the carriage's walls, windows and the passengers behind the glass ----
            public void Carriage()
            {
                On(Body);float[][] segments={new[]{RearZ,-1.0f},new[]{.3f,CabZ}};
                foreach(int side in new[]{-1,1})
                {
                    float x=side*(HalfWidth-.03f);
                    foreach(var g in segments)
                    {
                        float zc=(g[0]+g[1])/2,len=g[1]-g[0];
                        Box(green,x,.76f,zc,.06f,.72f,len,Fine?.012f:0);Box(cream,x,1.55f,zc,.055f,.86f,len);Box(green,x,2.215f,zc,.06f,.47f,len,Fine?.012f:0);
                        if(Mid){Box(brass,side*(HalfWidth+.004f),1.125f,zc,.012f,.025f,len);Box(brass,side*(HalfWidth+.004f),1.975f,zc,.012f,.025f,len);}
                    }
                    Box(green,x,2.33f,-.35f,.06f,.24f,1.3f);Box(iron,x,.43f,-.35f,.06f,.06f,1.3f);
                    for(int k=0;k<WindowZ.Length;k++)Window(side,WindowZ[k],side<0?k==1:k==2,k);
                    if(Mid)Box(lining,side*1.37f,1.44f,-.6f,.02f,1.96f,7.3f);
                    if(Fine)
                    {
                        // Soot along the roof line, rust bleeding under the windows, a flesh growth bursting at the door's edges.
                        foreach(float z in WindowZ){Box(soot,side*(HalfWidth+.006f),2.33f,z+.1f,.012f,.1f,1.1f);Box(rust,side*(HalfWidth+.006f),.96f,z-.2f,.012f,.3f,.05f);Box(rust,side*(HalfWidth+.006f),.86f,z+.25f,.012f,.42f,.04f);}
                        Ball(flesh,side*1.47f,2.12f,.32f,.09f,.16f,.12f);
                        // A panel burst outward by the mass inside: torn steel lips, flesh and amber light through the gap.
                        float bz=side<0?1.55f:-2.95f;Box(lining,side*1.43f,.78f,bz,.04f,.5f,.62f);Ball(fleshDark,side*1.42f,.78f,bz,.12f,.22f,.26f);Box(vein,side*1.5f,.8f,bz,.01f,.3f,.03f,0,25);
                        Box(green,side*1.49f,1.06f,bz,.06f,.06f,.7f,0,8);Box(green,side*1.49f,.5f,bz+.1f,.06f,.06f,.5f,0,-15);Ball(fleshDark,side*1.47f,.6f,-1.02f,.08f,.18f,.1f);Box(vein,side*1.5f,2.1f,.32f,.01f,.2f,.02f,0,20);
                    }
                }
            }
            private void Window(int side,float z,bool broken,int k)
            {
                float x=side*HalfWidth;Box(frame,x+side*.004f,1.55f,z,.02f,.74f,.98f);
                if(broken)
                {
                    Box(black,x+side*.012f,1.55f,z,.02f,.62f,.86f);
                    if(Mid){var glass=P(0x2A3438,.3f);Box(glass,x+side*.016f,1.78f,z-.3f,.01f,.18f,.24f,0,30);Box(glass,x+side*.016f,1.34f,z+.28f,.01f,.2f,.18f,0,-25);}
                    return;
                }
                Box(amber,x+side*.012f,1.55f,z,.02f,.62f,.86f);if(!Mid)return;
                // The passengers: heads and shoulders backlit, a hand pressed flat on the glass.
                float o=x+side*.024f,dz=(k%2==0?-.18f:.2f);
                Box(shadow,o,1.47f,z+dz,.012f,.2f,.17f);Box(shadow,o,1.31f,z+dz,.012f,.14f,.38f);
                if(k!=0){Box(shadow,o,1.44f,z-dz*1.1f,.012f,.18f,.15f);Box(shadow,o,1.3f,z-dz*1.1f,.012f,.12f,.32f);}
                if(Fine&&k%2==1){Box(flesh,o+side*.004f,1.66f,z-.28f,.012f,.15f,.12f);for(int f=0;f<4;f++)Box(flesh,o+side*.004f,1.77f,z-.33f+f*.033f,.012f,.09f,.025f);}
                if(Fine)Box(frame,x+side*.026f,1.55f,z,.012f,.62f,.025f);
            }

            // ---- the cab and the nose: the yellow warning front, the raked windscreen, the destination blind, buffers ----
            public void Cab()
            {
                On(Body);Box(green,0,1.43f,3.55f,2.9f,2.06f,1.0f,Fine?.03f:0);
                foreach(int side in new[]{-1,1})
                {
                    Box(frame,side*(HalfWidth+.004f),1.75f,3.55f,.02f,.56f,.58f);Box(amber,side*(HalfWidth+.012f),1.75f,3.55f,.02f,.48f,.5f);
                    if(Mid)Word(brass,"6:47",new Vector3(side*(HalfWidth+.006f),.86f,3.55f),side<0?-Vector3.UnitZ:Vector3.UnitZ,Vector3.UnitY,Vector3.UnitX*side,.055f,.012f);
                    if(Mid)Box(brass,side*1.36f,1.9f,4.92f,.035f,.72f,.035f);
                }
                Box(yellow,0,.925f,4.62f,2.76f,1.05f,1.15f,Fine?.06f:0);
                if(Fine)foreach(int side in new[]{-1,1})for(int k=0;k<2;k++)Box(black,side*(.98f-k*.32f),.62f,5.2f,.12f,.62f,.012f,0,0,0,side*-35);
                s.Push(WeaponSculpt.Rx(-12*Deg)*WeaponSculpt.T(0,1.45f,4.86f));
                Box(green,0,.5f,-.42f,2.84f,1.02f,.84f,Fine?.04f:0);Box(frame,0,.48f,.005f,2.5f,.68f,.02f);
                Box(G(AmberHex,1.5f,.35f),-.62f,.48f,.016f,1.0f,.56f,.02f);Box(black,.62f,.48f,.016f,1.0f,.56f,.02f);Box(green,0,.48f,.02f,.1f,.62f,.03f);
                if(Mid){var glass=P(0x2A3438,.35f);Box(glass,.3f,.7f,.03f,.3f,.08f,.012f,0,0,0,25);Box(glass,.95f,.3f,.03f,.2f,.14f,.012f,0,0,0,-35);Box(glass,.55f,.25f,.03f,.12f,.2f,.012f,0,0,0,10);}
                Box(black,0,.88f,.015f,2.0f,.2f,.03f);Word(G(AmberHex,2.8f,.4f),"6.47 EVAC",new Vector3(0,.88f,.032f),-Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ,.034f,.01f);
                if(Fine)foreach(int side in new[]{-1,1})Box(iron,side*.5f,.24f,.03f,.5f,.025f,.02f,0,0,0,side*-20);
                s.Pop();
                foreach(int side in new[]{-1,1})
                {
                    CylZ(steel,.11f,5.15f,5.5f,side*.98f,.62f,10);CylZ(iron,.25f,5.48f,5.58f,side*.98f,.62f,16,Fine?.015f:0);
                    CylZ(iron,.09f,5.17f,5.24f,side*1.12f,1.3f,10);CylZ(G(MarkerHex,3f),.065f,5.24f,5.26f,side*1.12f,1.3f,10);
                }
                Box(iron,0,.5f,5.32f,.32f,.22f,.36f,Fine?.02f:0);if(Mid)Box(iron,0,.42f,5.52f,.12f,.12f,.14f);
                // The plough: an iron V under the buffers, its hazard stripes worn, like a lower jaw.
                foreach(int side in new[]{-1,1}){Box(iron,side*.74f,.02f,5.18f,1.62f,.56f,.1f,Fine?.015f:0,0,side*-26);if(Fine)for(int k=0;k<3;k++)Box(k%2==0?yellow:black,side*(.33f+k*.36f),.02f,5.25f-k*.18f,.3f,.4f,.012f,0,0,side*-26);}
                Box(iron,0,-.24f,5.0f,2.4f,.08f,.8f);
                // The fixed front roof, its cant rails, the twin horns and the PA horn speakers (the Voice of the Station).
                Box(roof,0,2.585f,2.75f,2.76f,.27f,2.6f);foreach(int side in new[]{-1,1})Box(roof,side*1.33f,2.5f,2.75f,.22f,.12f,2.6f,0,0,0,side*-22);
                if(Mid)foreach(int side in new[]{-1,1})
                {
                    s.Push(WeaponSculpt.T(side*.78f,3.0f,3.55f));s.Lathe(P(0x5A6064,.1f),new[]{.07f,-.32f,.09f,-.16f,.27f,.1f},s.Sides(12));CylZ(black,.23f,.09f,.105f,0,0,12);Box(iron,0,-.2f,-.2f,.06f,.32f,.06f);s.Pop();
                    CylZ(brass,.045f,3.82f,4.15f,side*.22f,2.84f,8);CylZ(brass,.09f,4.12f,4.18f,side*.22f,2.84f,10);
                    if(Fine)s.Tube(cable,new[]{new Vector3(side*.78f,2.82f,3.3f),new Vector3(side*.6f,2.76f,2.95f),new Vector3(side*.45f,2.74f,2.75f)},.035f,5,Vector3.UnitY);
                }
                On(Lamp);
                CylZ(iron,.36f,-.06f,.16f,0,0,18,Fine?.02f:0);if(Mid){Box(iron,0,.33f,.08f,.66f,.07f,.34f,0,-10);CylZ(steel,.3f,.15f,.195f,0,0,18);}
                CylZ(G(HeadlampHex,9f,.6f),.27f,.19f,.215f,0,0,18);if(Fine){Box(iron,0,0,.218f,.56f,.025f,.008f);Box(iron,0,0,.218f,.025f,.56f,.008f);}
            }

            // ---- the underframe: solebars, the floor, equipment boxes, brake cylinders, pipes, flesh hanging under it ----
            public void Underframe()
            {
                On(Body);foreach(int side in new[]{-1,1})Box(iron,side*1.37f,.2f,-.15f,.08f,.4f,8.3f);Box(iron,0,.43f,-.13f,2.78f,.06f,8.3f);
                Box(iron,0,-.16f,-1.9f,1.8f,.42f,1.5f,Fine?.03f:0);Box(iron,0,-.14f,1.55f,1.5f,.38f,1.1f,Fine?.03f:0);
                if(Mid)foreach(int side in new[]{-1,1}){CylZ(iron,.13f,-.95f,-.2f,side*.6f,-.05f,10);CylZ(rust,.035f,-4.0f,2.9f,side*1.18f,.1f,6);}
            }

            // ---- the roof panel that peels back in TERMINUS, with the pantograph's base ----
            public void Roof()
            {
                On(NativeStationBossModel.Roof);Box(roof,0,.005f,2.55f,2.76f,.27f,5.1f);foreach(int side in new[]{-1,1})Box(roof,side*1.33f,-.08f,2.55f,.22f,.12f,5.1f,0,0,0,side*-22);
                if(Fine)for(int k=0;k<6;k++)Box(P(0x1C2124),0,.145f,.45f+k*.85f,2.6f,.03f,.06f);
                if(Mid)foreach(float z in new[]{.5f,3.0f,4.4f}){Box(iron,0,.19f,z,.3f,.12f,.3f);Box(roof,0,.27f,z,.42f,.05f,.42f);}
                if(Fine)Box(soot,.3f,.143f,2.2f,.9f,.01f,2.6f);
                // Arms reaching up out of the roof vents: the passengers trying to climb out.
                if(Mid)for(int k=0;k<3;k++)
                {
                    float x=k==1?.62f:-.55f+k*.1f,z=k==0?.5f:k==1?3.0f:4.4f,lean=k==1?-18:14;var cl=P(ClothHex[(k*2+2)%ClothHex.Length],.03f);
                    Box(flesh,x,.62f,z,.13f,.8f,.13f,0,lean*.5f,0,lean);Box(cl,x+(float)Math.Sin(-lean*Deg)*.18f,.42f,z,.17f,.32f,.17f,0,lean*.5f,0,lean);
                    var tip=new Vector3(x-(float)Math.Sin(lean*Deg)*.8f,1.0f,z);Box(flesh,tip.X,tip.Y,tip.Z,.15f,.18f,.08f,0,lean*.5f,0,lean);
                    if(Fine)for(int f=0;f<4;f++)Box(flesh,tip.X+(f-1.5f)*.035f,tip.Y+.14f,tip.Z,.03f,.13f,.035f,0,lean*.5f,0,lean+(f-1.5f)*8);
                }
                if(Mid)foreach(float x in new[]{-.42f,.42f})foreach(float z in new[]{.75f,1.35f})CylY(insulator,.065f,.14f,.3f,x,z,8);
                Box(iron,0,.32f,1.05f,1.0f,.05f,.74f);
            }
            public void Pantograph()
            {
                On(PantoLow);foreach(int side in new[]{-1,1})Box(iron,side*.3f,0,.75f,.055f,.055f,1.5f);Box(iron,0,0,1.45f,.66f,.05f,.05f);Box(iron,0,0,.06f,.7f,.08f,.12f);
                On(PantoHigh);Box(iron,0,0,-.75f,.065f,.065f,1.5f);if(Mid)Box(iron,0,0,0,.14f,.12f,.14f);
                On(PantoHead);Box(iron,0,.06f,0,1.7f,.06f,.12f);Box(copper,0,.105f,0,1.56f,.03f,.09f);foreach(int side in new[]{-1,1})Box(iron,side*.92f,0,0,.26f,.05f,.1f,0,0,0,side*-38);
                if(Mid)foreach(int side in new[]{-1,1})Box(G(LiveHex,1.8f),side*.42f,0,0,.07f,.08f,.07f);
            }

            // ---- the Stationmaster: grown out of the cab roof's hatch; frock coat, gold braid, top hat, the watch and the bell ----
            public void Stationmaster()
            {
                s.Push(WeaponSculpt.S(Rider,Rider,Rider));On(SmHips);
                s.Push(WeaponSculpt.Rx(90*Deg));s.Torus(iron,Vector3.Zero,.56f,.065f,s.Sides(18),Fine?6:4);s.Pop();
                if(Mid)Box(roof,0,.36f,-.62f,.9f,.06f,.9f,0,-72);
                for(int k=0;k<8;k++){float a=k*45f,x=(float)Math.Sin(a*Deg)*.5f,z=(float)Math.Cos(a*Deg)*.5f;if(!Mid&&k%2==1)continue;Box(k%3==0?coatDark:coat,x,-.03f,z,.36f,.12f,.3f,0,0,a,0);}
                if(Mid){Ball(flesh,.42f,-.04f,-.3f,.2f,.13f,.2f);Ball(fleshDark,-.38f,-.05f,.32f,.18f,.12f,.16f);s.Tube(cable,new[]{new Vector3(-.2f,.25f,-.22f),new Vector3(-.4f,.08f,-.5f),new Vector3(-.55f,-.04f,-.85f)},.045f,5,Vector3.UnitY);}
                if(Fine)s.Tube(cable,new[]{new Vector3(.18f,.3f,-.22f),new Vector3(.35f,.1f,-.55f),new Vector3(.4f,-.04f,-.95f)},.04f,5,Vector3.UnitY);
                Box(coat,0,.18f,0,.66f,.36f,.44f,Fine?.05f:0);
                On(SmChest);
                Box(coat,0,.3f,0,.8f,.62f,.48f,Fine?.06f:0);Box(P(0x3A1820,.04f),0,.25f,.235f,.34f,.5f,.02f);Box(P(0xB8B2A2),0,.56f,.2f,.22f,.1f,.12f);
                foreach(int side in new[]{-1,1})
                {
                    Box(coatDark,side*.13f,.38f,.243f,.1f,.34f,.02f,0,0,0,side*-14);Box(gold,side*.42f,.6f,0,.26f,.06f,.26f,Fine?.01f:0);
                    if(Mid)for(int k=0;k<4;k++)Box(brass,side*.2f,.45f-k*.11f,.245f,.045f,.045f,.02f);
                    if(Fine)for(int k=0;k<5;k++)Box(gold,side*(.31f+k*.05f),.54f,.12f,.015f,.09f,.015f);
                }
                if(Fine){for(int k=0;k<3;k++)Box(gold,0,.42f-k*.12f,.25f,.3f,.022f,.01f);Box(gold,.06f,.2f,.25f,.2f,.012f,.01f,0,0,0,-18);Box(coatDark,0,.3f,-.243f,.03f,.5f,.01f);}
                Box(skinDark,0,.66f,.02f,.17f,.14f,.17f);
                On(SmHead);
                Box(skin,0,.17f,.02f,.32f,.36f,.34f,Fine?.045f:0);Box(skinDark,0,.29f,.17f,.32f,.05f,.05f);
                foreach(int side in new[]{-1,1}){Box(black,side*.075f,.215f,.186f,.085f,.05f,.02f);Box(G(EyeHex,6f,.5f),side*.075f,.215f,.193f,.045f,.03f,.012f);}
                if(Mid)
                {
                    Box(skinDark,0,.15f,.2f,.06f,.1f,.06f);Box(hair,0,.085f,.195f,.26f,.05f,.05f);
                    foreach(int side in new[]{-1,1}){Box(hair,side*.15f,.06f,.19f,.06f,.09f,.04f,0,0,0,side*20);Box(hair,side*.165f,.1f,.07f,.04f,.2f,.17f);}
                    Box(skinDark,0,-.03f,.06f,.25f,.1f,.24f);Box(P(0x1A0A08),0,.025f,.19f,.14f,.045f,.02f);
                }
                if(Fine)foreach(int side in new[]{-1,1})Box(skin,side*.17f,.17f,0,.03f,.08f,.06f);
                On(SmHat);
                s.Push(WeaponSculpt.Rx(-90*Deg));s.Cylinder(hat,.3f,0,.035f,s.Sides(18),Fine?.008f:0);s.Lathe(hat,new[]{.19f,.03f,.195f,.3f,.21f,.48f},s.Sides(14));s.Cylinder(gold,.2f,.05f,.11f,s.Sides(14));s.Pop();
                if(Mid)Box(gold,0,.08f,.2f,.07f,.05f,.012f);
                foreach(int side in new[]{-1,1})
                {
                    int arm=side<0?SmLArm:SmRArm,fore=side<0?SmLFore:SmRFore,hand=side<0?SmLHand:SmRHand;
                    On(arm);Box(coat,0,-.24f,0,.21f,.52f,.23f,Fine?.03f:0);
                    On(fore);Box(coat,0,-.22f,0,.18f,.46f,.2f,Fine?.025f:0);Box(gold,0,-.37f,0,.2f,.05f,.22f);if(Fine)Box(gold,0,-.31f,0,.2f,.02f,.22f);
                    On(hand);Box(glove,0,-.08f,.01f,.14f,.17f,.11f,Fine?.02f:0);if(Fine)for(int k=0;k<4;k++)Box(glove,(k-1.5f)*.032f,-.2f,.03f,.028f,.1f,.03f,0,10);
                }
                // The pocket watch, stopped at 6:47 (its face glows amber), on its chain; the brass last-call bell.
                On(SmRHand);s.Push(WeaponSculpt.T(0,-.2f,.09f));CylZ(gold,.12f,-.02f,.02f,0,0,16,Fine?.006f:0);CylZ(G(AmberHex,3.4f,.5f),.1f,.02f,.028f,0,0,16);
                if(Fine){Box(black,-.02f,-.03f,.03f,.012f,.07f,.006f,0,0,0,-23);Box(black,-.04f,.01f,.032f,.09f,.01f,.006f,0,0,0,12);}
                s.Pop();if(Mid)Box(gold,0,-.1f,.06f,.012f,.2f,.012f);
                On(SmLHand);s.Push(WeaponSculpt.Rx(90*Deg)*WeaponSculpt.T(0,-.15f,0));s.Lathe(P(BrassHex,.35f),new[]{.03f,0f,.06f,.08f,.14f,.2f,.17f,.26f},s.Sides(14));s.Pop();Box(P(0x4A2E1A),0,-.08f,0,.05f,.12f,.05f);
                s.Pop();
            }

            // ---- a leg: an iron wheel, a bundle of fused limbs in the passengers' sleeves, an axle box knee, a hand for a foot ----
            public void Leg(int i)
            {
                int side=Side(i),row=Row(i);bool front=row==0;float l1=Thigh(i),l2=Shin(i),t=front?.6f:.56f,hs=front?1.3f:1.1f;
                var cloth=P(ClothHex[i],.03f);var cloth2=P(ClothHex[(i+3)%ClothHex.Length],.03f);
                On(NativeStationBossModel.Wheel(i));s.Push(WeaponSculpt.Ry(side*90*Deg));
                s.Cylinder(iron,.52f,-.07f,.07f,s.Sides(20),Fine?.02f:0);s.Cylinder(steel,.56f,-.075f,-.04f,s.Sides(20));s.Cylinder(iron,.17f,.07f,.15f,s.Sides(12));
                if(Fine)for(int k=0;k<3;k++){double a=k*Math.PI*2/3;s.Cylinder(black,.08f,.07f,.075f,8,0,(float)Math.Cos(a)*.31f,(float)Math.Sin(a)*.31f);}
                if(Mid)s.Cylinder(brass,.07f,.15f,.2f,8,0,.3f,0);
                s.Pop();
                // The thigh: a bundle of the passengers' arms, thick at the hip and tapering, a sleeve torn off at the hip, an iron
                // brake rod strapped along its top (+z here is the side the knee bends toward).
                On(Hip(i));
                Ball(flesh,0,-.05f,0,t*.8f,t*.75f,t*.8f);Box(flesh,0,-l1*.28f,0,t,l1*.6f,t,Fine?.05f:0);Box(flesh,0,-l1*.72f,0,t*.78f,l1*.6f,t*.78f,Fine?.04f:0);
                Box(cloth,0,-l1*.2f,0,t+.06f,l1*.4f,t+.06f,Fine?.03f:0);if(Mid)Box(cloth,0,-l1*.41f,0,t*.93f,.07f,t*.93f,0,6,0,4);
                if(Mid)
                {
                    Box(fleshDark,t*.42f,-l1*.52f,-t*.3f,t*.4f,l1*.78f,t*.4f,0,0,0,7);Box(flesh,-t*.4f,-l1*.48f,t*.3f,t*.36f,l1*.74f,t*.36f,0,0,0,-6);
                    Box(fleshDark,0,-l1*.56f,-t*.46f,t*.3f,l1*.62f,t*.3f,0,8,0,0);Box(flesh,0,-l1*.36f,t*.44f,t*.7f,l1*.3f,t*.26f,Fine?.03f:0);
                    Box(vein,t*.16f,-l1*.62f,t*.5f+.006f,.035f,l1*.45f,.02f,0,0,0,4);Box(vein,-t*.2f,-l1*.4f,t*.5f+.006f,.03f,l1*.3f,.02f,0,0,0,-6);
                    Box(steel,0,-l1*.52f,t*.58f,.06f,l1*.8f,.06f);Box(steel,0,-l1*.2f,t*.52f,.08f,.05f,.12f);Box(steel,0,-l1*.84f,t*.46f,.08f,.05f,.12f);
                }
                if(Fine){Box(flesh,t*.42f,-l1*.93f,-t*.3f,t*.3f,.16f,t*.34f);Box(flesh,-t*.4f,-l1*.87f,t*.3f,t*.28f,.15f,t*.3f);}
                // The knee: a knot of flesh with a bogie's axle box (brass end cap) on its outer face; the shin tapering to the wrist,
                // a second sleeve, a stopped wristwatch (amber face) on the outer side (+z here is away from the body).
                On(Knee(i));
                Ball(fleshDark,0,0,0,t*.62f,t*.6f,t*.62f);Box(iron,0,.02f,t*.32f,t*.9f,.42f,t*.6f,Fine?.03f:0);if(Mid)Box(brass,0,.02f,t*.64f,.22f,.22f,.04f);
                Box(flesh,0,-l2*.3f,0,t*.85f,l2*.62f,t*.85f,Fine?.04f:0);Box(flesh,0,-l2*.74f,0,t*.66f,l2*.48f,t*.66f,Fine?.03f:0);
                Box(cloth2,0,-l2*.2f,0,t*.85f+.06f,l2*.34f,t*.85f+.06f,Fine?.02f:0);
                if(Mid)
                {
                    Box(fleshDark,t*.36f,-l2*.55f,-t*.25f,t*.3f,l2*.7f,t*.3f,0,0,0,4);Box(flesh,-t*.32f,-l2*.5f,t*.28f,t*.26f,l2*.6f,t*.26f,0,0,0,-4);
                    Box(vein,-.03f,-l2*.6f,t*.43f+.006f,.035f,l2*.45f,.02f);
                    Box(P(0x2A1A10),0,-l2+.4f,0,t*.66f+.05f,.08f,t*.66f+.05f);Box(G(AmberHex,2.6f),0,-l2+.4f,t*.33f+.035f,.1f,.08f,.015f);
                }
                On(Foot(i));
                Box(cloth,0,.05f,-.06f*hs,.4f*hs,.14f,.3f*hs);Box(flesh,0,-.07f,.1f*hs,.5f*hs,.13f,.44f*hs,Fine?.03f:0);
                int fingers=Mid?4:2;for(int k=0;k<fingers;k++){float x=(k-(fingers-1)*.5f)*(Mid?.12f:.22f)*hs;Box(flesh,x,-.085f,.5f*hs,(Mid?.095f:.2f)*hs,.1f,.42f*hs,Fine?.015f:0,6);if(Fine)Box(P(0x2A2622),x,-.07f,.7f*hs,.07f*hs,.03f,.06f,0,6);}
                if(Mid)Box(flesh,side*-.3f*hs,-.08f,.05f,.1f,.09f,.3f*hs,0,0,side*40,0);
            }

            // ---- the doors (they slide open for All Aboard) and the arms waiting behind them ----
            public void Doors()
            {
                foreach(int bone in new[]{DoorLF,DoorLB,DoorRF,DoorRB})
                {
                    int side=bone<DoorRF?-1:1;bool lead=bone==DoorLF||bone==DoorRF;On(bone);
                    Box(green,0,0,0,.05f,1.74f,.64f,Fine?.01f:0);Box(cream,side*.006f,.22f,0,.045f,.86f,.64f);Box(frame,side*.03f,.3f,0,.015f,.58f,.4f);Box(G(AmberHex,1.6f,.35f),side*.036f,.3f,0,.012f,.5f,.32f);
                    if(Mid)Box(rubber,0,0,lead?-.31f:.31f,.056f,1.74f,.03f);
                }
                float[] dy={-.3f,-.1f,.12f,.32f,0},dz={-.22f,.18f,-.05f,.26f,-.36f};
                foreach(int side in new[]{-1,1})
                {
                    int b0=side<0?GrabL0:GrabR0,b1=side<0?GrabL1:GrabR1,n=Mid?5:2;
                    On(b0);for(int k=0;k<n;k++){Box(k%2==0?flesh:fleshDark,side*.58f,dy[k],dz[k],1.15f,.13f,.13f,0,0,(k-2)*4,(k-2)*3);if(k<3)Box(P(ClothHex[k+side+1],.03f),side*.33f,dy[k],dz[k],.6f,.17f,.17f);}
                    if(Mid)Box(vein,side*.75f,0,.02f,.8f,.03f,.03f);
                    On(b1);for(int k=0;k<n;k++){Box(flesh,side*.32f,dy[k]*.9f,dz[k]*.9f,.64f,.11f,.11f);Box(flesh,side*.72f,dy[k],dz[k],.16f,.14f,.17f);if(Fine)for(int f=0;f<3;f++)Box(flesh,side*.86f,dy[k]+(f-1)*.05f,dz[k],.14f,.035f,.035f);}
                }
            }

            // ---- the torn rear end, the Heart, the mass inside, the coupling chain and the torn cables ----
            public void Rear()
            {
                On(Body);Box(roof,0,2.585f,-3.97f,2.76f,.27f,.66f);
                for(int k=0;k<(Mid?5:2);k++)foreach(int side in new[]{-1,1})Box(green,side*1.42f,.6f+k*.42f,-4.36f-(k*37%5)*.03f,.06f,.32f,.26f,0,(k*23%40)-20,0,side*(k*17%30-15));
                for(int k=0;k<(Mid?5:2);k++)Box(roof,-1.1f+k*.55f,2.64f,-4.36f-(k*29%4)*.03f,.4f,.08f,.22f,0,(k%2==0?15:-12),k*9-18,0);
                if(Mid)foreach(int side in new[]{-1,1})for(int k=0;k<3;k++)Box(rubber,side*1.15f,1.0f+k*.45f,-4.42f,.2f,.3f,.18f,0,0,side*15,0);
                Box(iron,0,.3f,-4.55f,2.4f,.06f,.5f,0,35);if(Fine)Box(brass,-.9f,1.6f,-4.45f,.04f,.9f,.04f,0,25,0,-15);
                if(Mid)
                {
                    Box(lining,0,2.42f,-.6f,2.7f,.02f,7.3f);
                    // The passengers' mass inside (seen when the roof peels back): bodies, heads and arms, lit from within.
                    Ball(fleshDark,0,1.25f,-2.6f,1.1f,.75f,1.0f);Ball(flesh,.2f,1.3f,-1.1f,1.0f,.7f,.8f);Ball(fleshDark,-.1f,1.2f,.4f,1.05f,.7f,.9f);
                    Box(G(AmberHex,2.2f),.3f,1.85f,-2.0f,.6f,.03f,.5f,0,0,10,0);Box(G(AmberHex,2.2f),-.4f,1.8f,-.3f,.5f,.03f,.6f,0,0,-15,0);Box(G(CoreHex,2.4f),0,1.75f,-1.4f,.4f,.03f,.4f);
                    if(Fine)for(int k=0;k<6;k++){float x=-.8f+k*.32f,z=-3.0f+k*.8f;Box(k%2==0?flesh:skinDark,x,1.95f+(k%3)*.08f,z,.2f,.22f,.2f,0,k*14,0,k*-9);if(k%2==1)Box(flesh,x+.15f,2.15f,z+.2f,.08f,.45f,.08f,0,-20,0,25);}
                }
                On(Heart);
                Ball(flesh,0,0,.15f,1.05f,.85f,.6f);Ball(G(CoreHex,3.6f,.5f),.05f,.05f,-.32f,.55f,.45f,.18f);
                if(Mid)
                {
                    Ball(fleshDark,-.55f,-.35f,-.05f,.5f,.45f,.4f);Ball(flesh,.6f,.3f,-.02f,.45f,.5f,.4f);
                    Box(fleshDark,-.3f,.55f,-.35f,.22f,.25f,.22f,0,10,20);Box(flesh,.45f,-.4f,-.38f,.2f,.22f,.2f,0,-15,-10);
                    for(int k=0;k<5;k++)Box(G(CoreHex,2.6f,.5f),-.5f+k*.25f,-.3f+(k%3)*.3f,-.42f-(k%2)*.04f,.03f,.35f,.02f,0,0,0,k*35-60);
                }
                if(Fine){Box(flesh,-.7f,-.6f,-.4f,.1f,.6f,.1f,0,20,0,10);Box(flesh,.75f,-.5f,-.35f,.1f,.55f,.1f,0,15,0,-14);Box(skinDark,.1f,.65f,-.25f,.2f,.22f,.2f);}
                for(int k=0;k<ChainLinks;k++)
                {
                    On(ChainFirst+k);bool turn=k%2==1;var link=P(RustHex,.06f);
                    if(!Mid){Box(link,0,-.17f,0,.06f,.3f,.06f);continue;}
                    foreach(int side in new[]{-1,1}){if(turn)Box(link,0,-.17f,side*.06f,.03f,.3f,.03f);else Box(link,side*.06f,-.17f,0,.03f,.3f,.03f);}
                    Box(link,0,-.03f,0,turn?.03f:.15f,.03f,turn?.15f:.03f);Box(link,0,-.31f,0,turn?.03f:.15f,.03f,turn?.15f:.03f);
                    if(k==ChainLinks-1)Box(iron,0,-.42f,.05f,.12f,.14f,.2f);
                }
                foreach(int first in new[]{CableAFirst,CableBFirst})for(int k=0;k<CableLinks;k++)
                {
                    On(first+k);s.Push(WeaponSculpt.Rx(90*Deg));s.Cylinder(cable,.045f,0,.62f,s.Sides(6));s.Pop();if(Fine)Box(copper,0,-.6f,0,.1f,.06f,.1f);
                    if(k==CableLinks-1&&Mid){for(int f=0;f<3;f++)Box(copper,(f-1)*.04f,-.68f,0,.015f,.14f,.015f,0,0,0,(f-1)*25);Box(G(LiveHex,3f,.5f),0,-.74f,0,.05f,.05f,.05f);}
                }
            }

            // ---- the arms hanging out of the windows ----
            public void WindowArms()
            {
                for(int a=0;a<NativeStationBossModel.WindowArms;a++)
                {
                    var cloth=P(ClothHex[(a*2+1)%ClothHex.Length],.03f);
                    On(ArmFirst+a*2);Box(flesh,0,-.28f,0,.13f,.56f,.13f,Fine?.02f:0);Box(cloth,0,-.1f,0,.165f,.24f,.165f);
                    On(ArmFirst+a*2+1);Box(flesh,0,-.2f,0,.11f,.42f,.11f);Box(flesh,0,-.48f,.02f,.13f,.17f,.07f);if(Fine)for(int f=0;f<4;f++)Box(flesh,(f-1.5f)*.03f,-.6f,.03f,.025f,.1f,.03f);
                }
            }
        }
    }
}
