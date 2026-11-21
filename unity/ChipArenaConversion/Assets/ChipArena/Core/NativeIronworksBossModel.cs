using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The City Ironworks boss, THE CONVERTER (Claude, 2026-10-17; docs/EXPEDITION-BOSS-CITY-IRONWORKS.md). A concept: the
    /// presentation's proposal for Astra and the owner, inert and unreferenced by the live runtime (the managed checks hold that).
    /// Vessel C, the third Bessemer converter of the converter hall, walking on a four-legged trunnion yoke, crewed by the dead
    /// foreman on its pulpit. These are the action table and the numbers the doc's Astra contract proposes; the authority owns them.
    /// </summary>
    public static class NativeIronworksBoss
    {
        /// <summary>The proposed archetype id, the HUD plate's name and the stencil on the vessel.</summary>
        public const string Archetype="ironworks-converter",PlateName="THE CONVERTER",Stencil="VESSEL C";
        /// <summary>Phase thresholds (health fractions): phase 2 below .70, phase 3 below .35.</summary>
        public const float Phase2At=.70f,Phase3At=.35f;
        /// <summary>Action codes (the replicated `bossAction`): attacks 1..8, scripted moments from 20.</summary>
        public const byte None=0,Pour=1,Blow=2,Stomp=3,Grab=4,Slam=5,Deslag=6,Call=7,Charge=8,Arrive=20,Shed=21,Break=22,Choke=23,Kneel=24,Crash=25,Die=26;
        /// <summary>One action's timeline from its authority start time (s): anticipation (the telegraph, harmless), the strike
        /// (the harmful window), the follow-through (still moving, the strike's leftovers) and the recovery (the punish window);
        /// the cooldown before the same action may start again; the first phase that uses it.</summary>
        public struct Action
        {
            public byte Code;public string Id;public int From;public float Windup,Strike,Follow,Recover,Cooldown;
            public float Total=>Windup+Strike+Follow+Recover;
        }
        /// <summary>The proposed action table (docs/EXPEDITION-BOSS-CITY-IRONWORKS.md section 6; Astra's numbers to accept).</summary>
        public static readonly Action[] Actions=
        {
            A(Pour,"pour",1,1.2f,1.6f,.5f,1.0f,7f),
            A(Blow,"blow",1,1.5f,3.8f,.4f,1.5f,16f),
            A(Stomp,"stomp",1,.7f,.15f,.35f,.6f,3f),
            A(Grab,"grab",1,.9f,.25f,1.6f,.9f,10f),
            A(Slam,"slam",2,1.3f,.6f,.6f,1.6f,12f),
            A(Deslag,"deslag",2,1.4f,1.2f,.9f,1.2f,14f),
            A(Call,"call",2,1.0f,0f,.8f,.6f,25f),
            A(Charge,"charge",3,1.0f,1.2f,.6f,1.0f,9f),
            A(Arrive,"arrive",1,5.5f,0f,0f,0f,0f),
            A(Shed,"shed",2,.6f,0f,1.8f,.8f,0f),
            A(Break,"break",3,.8f,0f,2.0f,.8f,0f),
            A(Choke,"choke",1,.3f,0f,0f,2.5f,0f),
            A(Kneel,"kneel",1,.4f,0f,0f,3.0f,0f),
            A(Crash,"crash",3,.2f,0f,0f,2.0f,0f),
            A(Die,"die",1,7.0f,0f,0f,0f,0f),
        };
        private static Action A(byte code,string id,int from,float windup,float strike,float follow,float recover,float cooldown)=>new Action{Code=code,Id=id,From=from,Windup=windup,Strike=strike,Follow=follow,Recover=recover,Cooldown=cooldown};
        /// <summary>An action by id, or a None action.</summary>
        public static Action Find(string id){foreach(var a in Actions)if(a.Id==id)return a;return default;}
        /// <summary>The phase a health fraction falls in (1..3).</summary>
        public static int PhaseOf(float health)=>health<Phase3At?3:health<Phase2At?2:1;
    }

    /// <summary>
    /// A pose of the Converter's concept model (model space: metres, +y up, +z the boss's forward, the root on the ground under the
    /// trunnions): the body (the trunnion axis' height, pitch nose-up and roll right-up in degrees, a shift), the vessel's tilt on
    /// its trunnions (degrees, + pours forward), the four foot targets on the ground (y the lift; FL, FR, RL, RR) with each foot's
    /// pitch, the front tongs' opening, the foreman (lever pulled back, lean forward), the pulpit torn away (0..1), the glow
    /// levels (mouth heat, the tuyères' blow, the phase 3 seams, the death's cooling), the slag crust present (bits: front, left,
    /// right, back) and a missing plate's flight (0..1), and the hanging chains' swing (degrees about x and z).
    /// </summary>
    public struct IronworksBossPose
    {
        public float Height,Pitch,Roll,ShiftX,ShiftZ,Tilt,OpenL,OpenR,Lever,Lean,Pulpit,Heat,Blow,Seams,Cool,Shed,SwingX,SwingZ;
        public Vector3 FL,FR,RL,RR;public float PitchFL,PitchFR,PitchRL,PitchRR;public int Crust;
        public Vector3 Foot(int leg)=>leg==0?FL:leg==1?FR:leg==2?RL:RR;
        public float FootPitch(int leg)=>leg==0?PitchFL:leg==1?PitchFR:leg==2?PitchRL:PitchRR;
    }

    /// <summary>The built concept model of one detail tier: the body (one section per joint, each in its joint's rest frame) and
    /// the chain props (a link, a crane hook and a shackle, each on its own matrix).</summary>
    public sealed class IronworksBossMesh
    {
        public int Tier;public WeaponMesh Body,Link,Hook,Shackle;
        public int Triangles=>Body.Triangles+Link.Triangles+Hook.Triangles+Shackle.Triangles;
    }

    /// <summary>
    /// THE CONVERTER's concept model (Claude, 2026-10-17; docs/EXPEDITION-BOSS-CITY-IRONWORKS.md section 3): presentation only,
    /// inert, built in the Ironworks enemies' part style (NativeWeaponSculpt's chamfered boxes, lathes and tubes; the zone's
    /// palette) at boss quality, and posed in its key poses by a two-bone IK per leg. Nothing in the live runtime references it.
    /// <list type="bullet">
    /// <item>The vessel: a banded Bessemer converter (an eccentric nose, the mouth's white metal, molten tears down the nose,
    /// the trunnion ring and pins, the bull gear on the right trunnion, the blast pipe down the left side to the wind box and
    /// its tuyère grate underneath (a weak point that glows when it blows), the VESSEL C nameplate), crusted with glassy slag
    /// plates in four breakable groups, and the phase 3 seams.</item>
    /// <item>The yoke: two riveted side girders on the trunnion bearings, the tilt pinion and motor, two cold work lamps at the
    /// front (its eyes), the rear arch carrying the pulpit, where the dead foreman in his silver heat suit and cyan visor works
    /// the tilt levers.</item>
    /// <item>Four crab legs (a slewing coxa, a box-girder femur with its ram, a long tibia, the foot): ingot tongs on the front
    /// feet (closed they are its hooves, open they grab), crane-truck bogies on the rear.</item>
    /// <item>The chains hanging under the yoke and their hook and shackles are props on their own matrices (<see cref="Chains"/>).</item>
    /// </list>
    /// Deterministic and engine-free; <see cref="Solve"/> allocates nothing.
    /// </summary>
    public static class NativeIronworksBossModel
    {
        // ---------------- joints ----------------
        public const int Root=0,Yoke=1,Vessel=2,VesselHeat=3,WindBox=4,Seams=5,CrustFront=6,CrustLeft=7,CrustRight=8,CrustBack=9,Pulpit=10,Lever=11,Foreman=12,ForemanArms=13,
            LegBase=14,LegJoints=4,Coxa=0,Femur=1,Tibia=2,Foot=3,JawBase=30,Count=34;
        public const int FL=0,FR=1,RL=2,RR=3,Legs=4;
        /// <summary>A leg's joint (Coxa, Femur, Tibia or Foot).</summary>
        public static int LegJoint(int leg,int part)=>LegBase+leg*LegJoints+part;
        /// <summary>A front foot's jaws: 0 the front jaw, 1 the back jaw (FL and FR only).</summary>
        public static int Jaw(int leg,int which)=>JawBase+leg*2+which;
        public static readonly string[] Names={"root","yoke","vessel","vessel-heat","wind-box","seams","crust-front","crust-left","crust-right","crust-back","pulpit","lever","foreman","foreman-arms",
            "fl-coxa","fl-femur","fl-tibia","fl-foot","fr-coxa","fr-femur","fr-tibia","fr-foot","rl-coxa","rl-femur","rl-tibia","rl-foot","rr-coxa","rr-femur","rr-tibia","rr-foot","fl-jaw-front","fl-jaw-back","fr-jaw-front","fr-jaw-back"};
        /// <summary>Each joint's parent (-1 the root).</summary>
        public static readonly int[] Parent={-1,Root,Yoke,Vessel,Vessel,Vessel,Vessel,Vessel,Vessel,Vessel,Yoke,Pulpit,Pulpit,Foreman,
            Yoke,14,15,16, Yoke,18,19,20, Yoke,22,23,24, Yoke,26,27,28, 17,17,21,21};
        /// <summary>Glow groups: what scales a joint's emission (<see cref="GlowOf"/>).</summary>
        public const byte GlowSteady=0,GlowHeat=1,GlowBlow=2,GlowSeams=3;
        public static byte Group(int joint)=>joint==VesselHeat?GlowHeat:joint==WindBox?GlowBlow:joint==Seams?GlowSeams:GlowSteady;
        /// <summary>The emission scale of a joint in a pose (the runtime multiplies the section's emission by it).</summary>
        public static float GlowOf(int joint,in IronworksBossPose p)
        {
            float k=1-Clamp01(p.Cool);
            switch(Group(joint)){case GlowHeat:return Math.Max(0,p.Heat)*k;case GlowBlow:return Math.Max(0,p.Blow)*k;case GlowSeams:return Math.Max(0,p.Seams)*k;default:return .25f+.75f*k;}
        }
        /// <summary>Whether a joint draws in a pose: a missing crust plate draws only while it flies off (Shed between 0 and 1).</summary>
        public static bool Visible(int joint,in IronworksBossPose p)
        {
            if(joint==Root)return false;
            if(joint>=CrustFront&&joint<=CrustBack){bool on=(p.Crust&(1<<(joint-CrustFront)))!=0;return on||p.Shed>0&&p.Shed<1;}
            if(joint==Seams)return p.Seams>0;
            return true;
        }

        // ---------------- dimensions (m, the yoke frame: origin on the trunnion axis) ----------------
        /// <summary>The trunnion axis' standing height; the side girders' centre x; the hips (yoke frame, left side; front and rear
        /// z); the coxa's reach to the femur pivot; femur and tibia lengths; the ankles' heights over the ground (front tongs, rear
        /// bogies); the tongs' hinge under the ankle.</summary>
        public const float StandHeight=3.7f,GirderX=2.46f,GirderHalf=.27f,HipX=2.98f,HipY=-.3f,HipFrontZ=1.8f,HipRearZ=-2.45f,CoxaReach=.42f,FemurLength=2.35f,TibiaLength=3.6f,
            JawHinge=-.3f,JawRadius=.47f,AnkleFront=-(JawHinge-2*JawRadius-.1f),AnkleRear=.88f;
        /// <summary>The vessel's scale over its profile (the profile below is authored at 1).</summary>
        public const float VesselScale=1.1f;
        /// <summary>The pulpit's rest (yoke frame), the lever's pivot and the foreman's feet and shoulders (pulpit frame).</summary>
        public static readonly Vector3 PulpitRest=new Vector3(0,2.24f,-3.3f),LeverPivot=new Vector3(0,.55f,.36f),ForemanFeet=new Vector3(0,0,-.22f),Shoulders=new Vector3(0,1.48f,0);
        /// <summary>The pulpit's hinge when it tears away (yoke frame): it swings down behind the rear arch.</summary>
        public static readonly Vector3 PulpitHinge=new Vector3(0,2.0f,-3.9f);
        /// <summary>Triangle budgets per tier (full, reduced, low); the joint count is fixed.</summary>
        public static readonly int[] Budget={40000,26000,9000};
        /// <summary>The body's collision proposal: a capsule (radius, height, its centre's z) and the reach box every key pose stays in
        /// (x half width, z from and to, y from and to; model space).</summary>
        public const float CapsuleRadius=2.7f,CapsuleHeight=7.0f,CapsuleZ=-.6f,ReachX=6.0f,ReachZ0=-6.2f,ReachZ1=7.6f,ReachY0=-3.4f,ReachY1=9.4f;

        // ---------------- the vessel's outer profile (radius, height over the trunnion axis) ----------------
        private static readonly float[] ProfileR={.95f,1.12f,1.30f,1.52f,1.68f,1.76f,1.78f,1.76f,1.70f,1.52f,1.28f,1.10f,1.02f};
        private static readonly float[] ProfileY={-1.98f,-1.90f,-1.72f,-1.36f,-.92f,-.42f,0f,.42f,.86f,1.36f,1.80f,2.12f,2.40f};
        /// <summary>The shell's radius at height y (linear in the profile).</summary>
        public static float ShellR(float y)
        {
            if(y<=ProfileY[0])return ProfileR[0];int n=ProfileY.Length;if(y>=ProfileY[n-1])return ProfileR[n-1];
            for(int i=1;i<n;i++)if(y<=ProfileY[i]){float t=(y-ProfileY[i-1])/(ProfileY[i]-ProfileY[i-1]);return ProfileR[i-1]+(ProfileR[i]-ProfileR[i-1])*t;}
            return ProfileR[n-1];
        }
        private static float ShellSlope(float y){const float h=.04f;return (ShellR(y+h)-ShellR(y-h))/(2*h);}
        /// <summary>The eccentric nose: how far the shell's centre leans forward (+z) at height y.</summary>
        public static float Nose(float y){float t=Clamp01((y-1.3f)/1.2f);return .24f*t*t*(3-2*t);}
        /// <summary>The mouth: its centre's height and forward offset, its opening's radius and the metal's level (vessel frame).</summary>
        private const float MouthAt=2.72f,MouthOpen=.80f,MetalAt=2.46f,BottomAt=-2.36f;
        /// <summary>The mouth (vessel frame, metres, scaled): its centre's height and forward offset, its opening's radius, the metal's
        /// level, and the tuyère grate's height under the vessel.</summary>
        public const float MouthY=MouthAt*VesselScale,MouthR=MouthOpen*VesselScale,MetalY=MetalAt*VesselScale,BottomY=BottomAt*VesselScale;
        public static float MouthZ=>Nose(MouthAt)*VesselScale;

        // ---------------- palette (sRGB) ----------------
        private const int IronHex=0x24211e,IronDarkHex=0x161412,IronEdgeHex=0x4f4b46,SteelHex=0x5a5751,SteelLightHex=0x8a877f,SootHex=0x110f0d,YellowHex=0xc9a23a,YellowDarkHex=0x8a6e26,StripeHex=0x1a1714,
            LiningHex=0x5a3a2a,SlagHex=0x131010,SlagEdgeHex=0x2a2427,GreenHex=0x2f4038,SilverHex=0xb4b1a9,SilverDarkHex=0x6a6863,QuiltHex=0x8a877f,LeatherHex=0x342112,HatHex=0xd8d2c0,
            PaintHex=0xd8d4c8,ScaleHex=0x4a2e1e,GlassHex=0x0e1114,RubberHex=0x151515;
        private static WeaponPaint P(int hex,float sheen=0)=>WeaponPaint.Solid(hex,sheen);
        private static WeaponPaint G(int hex,float strength,float albedo=.3f)=>WeaponPaint.Glow(hex,strength,albedo);
        private static WeaponPaint Iron=>P(IronHex,.03f);private static WeaponPaint IronDark=>P(IronDarkHex,.02f);private static WeaponPaint Edge=>P(IronEdgeHex,.05f);private static WeaponPaint Steel=>P(SteelHex,.06f);
        private static WeaponPaint SteelLight=>P(SteelLightHex,.1f);private static WeaponPaint Yellow=>P(YellowHex,.05f);private static WeaponPaint YellowDark=>P(YellowDarkHex,.03f);private static WeaponPaint Stripe=>P(StripeHex);
        private static WeaponPaint Slag=>P(SlagHex,.12f);private static WeaponPaint SlagEdge=>P(SlagEdgeHex,.18f);
        private static WeaponPaint Metal=>G(NativeIronworksKinds.WhiteHot,6.0f,.06f);private static WeaponPaint Molten=>G(NativeIronworksKinds.Molten,2.6f,.08f);
        private static WeaponPaint Tuyere=>G(NativeIronworksKinds.WhiteHot,3.4f,.08f);private static WeaponPaint SeamGlow=>G(NativeIronworksKinds.WhiteHot,4.2f,.05f);
        private static WeaponPaint CrackGlow=>G(NativeIronworksKinds.SlagCrack,1.7f,.2f);private static WeaponPaint TongHot=>G(NativeIronworksKinds.SlagCrack,1.2f,.4f);
        private static WeaponPaint Visor=>G(NativeIronworksKinds.Cyan,3.2f,.2f);private static WeaponPaint Gauge=>G(NativeIronworksKinds.Cyan,2.4f,.25f);private static WeaponPaint Lamp=>G(NativeIronworksKinds.ColdWhite,5.0f,.4f);

        // ================================================================================================================
        // build
        // ================================================================================================================
        /// <summary>Builds a tier (0 full, 1 reduced, 2 low). Pure: the same tier gives the same triangles.</summary>
        public static IronworksBossMesh Build(int tier)
        {
            tier=Math.Max(0,Math.Min(2,tier));
            var o=new IronworksBossMesh{Tier=tier,Body=new WeaponMesh{Spec="boss-converter",Tier=tier},Link=new WeaponMesh{Spec="boss-converter-link",Tier=tier},
                Hook=new WeaponMesh{Spec="boss-converter-hook",Tier=tier},Shackle=new WeaponMesh{Spec="boss-converter-shackle",Tier=tier}};
            var b=new B(o.Body,tier);
            BuildYoke(b);BuildVessel(b);BuildCrust(b);BuildSeams(b);BuildPulpit(b);BuildForeman(b);
            for(int leg=0;leg<Legs;leg++){BuildCoxa(b,leg);BuildFemur(b,leg);BuildTibia(b,leg);if(leg<2){BuildTongs(b,leg);}else BuildBogie(b,leg);}
            BuildLink(new B(o.Link,tier));BuildHook(new B(o.Hook,tier));BuildShackle(new B(o.Shackle,tier));
            return o;
        }

        /// <summary>The sculpting helper: one WeaponSculpt over the mesh, a section per joint.</summary>
        private sealed class B
        {
            public readonly WeaponSculpt S;public readonly int Tier;
            public B(WeaponMesh m,int tier){S=new WeaponSculpt(m,tier);Tier=tier;}
            public bool Fine=>Tier==0;public bool Mid=>Tier<=1;
            public void On(int joint)=>S.Into(joint);
            public int Sides(int full)=>S.Sides(full);
            public void Box(in WeaponPaint p,float x,float y,float z,float sx,float sy,float sz,float bevel=0,float rx=0,float ry=0,float rz=0)
            {if(Tier==1&&bevel<.02f)bevel=0;S.Box(p,new Vector3(x,y,z),new Vector3(sx,sy,sz),bevel,rx,ry,rz);}
            public void Span(in WeaponPaint p,float x0,float x1,float y0,float y1,float z0,float z1,float bevel=0)
                =>Box(p,(x0+x1)*.5f,(y0+y1)*.5f,(z0+z1)*.5f,Math.Abs(x1-x0),Math.Abs(y1-y0),Math.Abs(z1-z0),bevel);
            /// <summary>A cylinder from a to b (radius r).</summary>
            public void Rod(in WeaponPaint p,Vector3 a,Vector3 b,float r,int sides,float bevel=0)
            {var d=b-a;float l=d.Length();if(l<1e-5f)return;S.Push(Frame(a,d/l));S.Cylinder(p,r,0,l,S.Sides(sides),bevel);S.Pop();}
            /// <summary>A box placed in a frame (centre and size in that frame).</summary>
            public void BoxIn(Matrix4x4 frame,in WeaponPaint p,Vector3 c,Vector3 size,float bevel=0,float rz=0)
            {S.Push(frame);Box(p,c.X,c.Y,c.Z,size.X,size.Y,size.Z,bevel,0,0,rz);S.Pop();}
            /// <summary>A body of revolution about +y: (radius, height) pairs, its centre leaning forward by `nose(y)`.</summary>
            public void LatheY(Func<int,WeaponPaint> paint,float[] r,float[] y,int sides,Func<float,float> nose=null,int smooth=1,bool caps=false)
            {
                var rings=new List<Vector3[]>(r.Length);
                for(int i=0;i<r.Length;i++){float n=nose==null?0:nose(y[i]);rings.Add(WeaponSculpt.Circle(sides,r[i],r[i],y[i],(float)Math.PI/sides,0,-n));}
                S.Push(WeaponSculpt.Rx(-(float)Math.PI/2));S.Loft(rings,(i,k)=>paint(i),smooth,caps,caps);S.Pop();
            }
        }
        /// <summary>A row-vector frame taking +z to `dir` (unit) at `at` (its x kept horizontal unless dir is vertical).</summary>
        private static Matrix4x4 Frame(Vector3 at,Vector3 dir)
        {
            var z=dir;var up=Math.Abs(z.Y)>.98f?Vector3.UnitZ:Vector3.UnitY;var x=Vector3.Normalize(Vector3.Cross(up,z));var y=Vector3.Cross(z,x);
            return Rows(x,y,z,at);
        }
        private static Matrix4x4 Rows(Vector3 x,Vector3 y,Vector3 z,Vector3 t)=>new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,t.X,t.Y,t.Z,1);
        /// <summary>A frame on the vessel's outer shell at azimuth phi (radians from +z toward +x) and height y, lifted `lift` off
        /// it: +z the outward normal, +y up the surface, +x round it.</summary>
        private static Matrix4x4 OnShell(float phi,float y,float lift)
        {
            float s=(float)Math.Sin(phi),c=(float)Math.Cos(phi),r=ShellR(y),k=ShellSlope(y);
            var n=Vector3.Normalize(new Vector3(s,-k,c));var t=Vector3.Normalize(new Vector3(k*s,1,k*c));var x=Vector3.Cross(t,n);
            var p=new Vector3(s*r,y,c*r+Nose(y))+n*lift;return Rows(x,t,n,p);
        }

        // ---------------- the yoke: girders, bearings, the tilt drive, the lamps, the rear arch ----------------
        private static void BuildYoke(B b)
        {
            b.On(Yoke);const float gh=.5f,gw=GirderHalf;
            foreach(float s in new[]{-1f,1f})
            {
                float x=s*GirderX;
                // The side girder: web, flanges (the top's edges worn yellow), stiffeners, rivets.
                b.Span(Iron,x-.08f,x+.08f,-gh+.06f,gh-.06f,-2.75f,2.05f);
                b.Span(Iron,x-gw,x+gw,gh-.1f,gh,-2.75f,2.05f,.02f);b.Span(Iron,x-gw,x+gw,-gh,-gh+.1f,-2.75f,2.05f,.02f);
                b.Span(Yellow,x-gw,x+gw,gh,gh+.02f,-2.65f,1.95f);b.Span(Stripe,x-gw+.06f,x+gw-.06f,gh+.005f,gh+.025f,-2.6f,1.9f);
                for(float z=-2.5f;z<1.95f;z+=.55f){b.Span(Edge,x-gw+.01f,x-.08f,-gh+.1f,gh-.1f,z-.03f,z+.03f);b.Span(Edge,x+.08f,x+gw-.01f,-gh+.1f,gh-.1f,z-.03f,z+.03f);}
                if(b.Fine)for(float z=-2.65f;z<2.0f;z+=.3f)foreach(float ox in new[]{-.21f,.21f}){b.Box(Steel,x+ox,gh+.02f,z,.045f,.03f,.045f);b.Box(Steel,x+ox,-gh-.01f,z,.045f,.03f,.045f);}
                // A steam main along the outer face, its brackets.
                b.Rod(IronDark,new Vector3(x+s*(gw+.12f),.18f,-2.6f),new Vector3(x+s*(gw+.12f),.18f,1.4f),.08f,8);if(b.Mid)for(float z=-2.2f;z<1.4f;z+=.9f)b.Span(Edge,x+s*gw,x+s*(gw+.2f),.12f,.24f,z-.04f,z+.04f);
                // Hazard stripes on the front end, a cold work lamp on top of it (the eyes), the end plate.
                b.Span(IronDark,x-gw-.01f,x+gw+.01f,-gh-.01f,gh+.01f,2.05f,2.1f);
                if(b.Mid)for(int i=0;i<5;i++)b.Box(i%2==0?Yellow:Stripe,x,-.38f+i*.19f,2.11f,gw*2,.1f,.02f,0,0,0,.6f*s);
                b.Span(IronDark,x-.23f,x+.23f,gh+.02f,gh+.42f,1.6f,2.08f,.03f);b.Span(Lamp,x-.17f,x+.17f,gh+.08f,gh+.36f,2.08f,2.11f);
                if(b.Mid){b.Span(Iron,x-.2f,x+.2f,gh+.39f,gh+.45f,1.8f,2.2f);for(int i=0;i<3;i++)b.Span(IronDark,x-.18f,x+.18f,gh+.13f+i*.09f,gh+.15f+i*.09f,2.11f,2.13f);}
                // The trunnion bearing: a block on the girder, its pillow cap and bolts.
                b.Span(IronDark,x-.34f,x+.34f,-.7f,.5f,-.58f,.58f,.04f);b.Box(Iron,x,.62f,0,.7f,.4f,1.0f,.08f);
                if(b.Mid)foreach(float bz in new[]{-.42f,.42f})foreach(float bx in new[]{-.22f,.22f})b.Box(Steel,x+bx,.84f,bz,.1f,.09f,.1f,.01f);
                // Hip brackets out to the coxae, gusseted.
                foreach(float hz in new[]{HipFrontZ,HipRearZ}){b.Span(Iron,x+s*gw,s*HipX,-.62f,.4f,hz-.46f,hz+.46f,.03f);if(b.Mid){b.Span(Edge,x+s*gw,s*(HipX-.3f),.4f,.44f,hz-.4f,hz+.4f);b.Span(Edge,x+s*gw,s*(HipX-.3f),-.66f,-.62f,hz-.4f,hz+.4f);}}
                // The chains' eye plates under the girder.
                foreach(float cz in new[]{.9f,-1.4f})b.Span(Steel,x-.05f,x+.05f,-.68f,-.5f,cz-.11f,cz+.11f);
            }
            // The tilt drive: the pinion meshing with the bull gear's underside, its motor slung under the right girder.
            var pin=new Vector3(3.02f,-1.24f,-.7f);b.Rod(GreenOf(),pin-new Vector3(.1f,0,0),pin+new Vector3(.17f,0,0),.29f,14,.02f);
            b.Span(GreenOf(),2.45f,3.25f,-1.75f,-1.12f,-1.5f,-.82f,.05f);b.Span(Iron,2.2f,2.65f,-1.12f,-.5f,-1.35f,-.95f);
            if(b.Fine)for(int i=0;i<7;i++)b.Span(Edge,2.5f,3.2f,-1.74f,-1.72f,-1.45f+i*.1f,-1.42f+i*.1f);
            // The rear arch: two heavy posts up from the girders' ends, the beam the pulpit stands on, braces under it.
            foreach(float s in new[]{-1f,1f})
            {
                var a=new Vector3(s*GirderX,.45f,-2.6f);var t=new Vector3(s*1.62f,1.95f,-3.15f);var d=Vector3.Normalize(t-a);float l=(t-a).Length();
                b.S.Push(Frame(a,d));b.Box(Iron,0,0,l*.5f,.56f,.56f,l+.3f,.04f);if(b.Mid){b.Box(Yellow,0,.285f,l*.5f,.42f,.02f,l*.8f);b.Box(Edge,.285f,0,l*.5f,.02f,.4f,l*.9f);b.Box(Edge,-.285f,0,l*.5f,.02f,.4f,l*.9f);}b.S.Pop();
            }
            b.Span(Iron,-1.95f,1.95f,1.72f,2.14f,-3.5f,-3.0f,.04f);b.Span(Yellow,-1.9f,1.9f,2.14f,2.16f,-3.45f,-3.05f);
            if(b.Mid){foreach(float s in new[]{-1f,1f})b.Rod(Iron,new Vector3(s*1.7f,1.75f,-3.25f),new Vector3(0,.95f,-3.3f),.09f,8);b.Span(IronDark,-.5f,.5f,.8f,1.1f,-3.45f,-3.15f,.03f);}
        }
        private static WeaponPaint GreenOf()=>P(GreenHex,.05f);

        // ---------------- the vessel ----------------
        private static void BuildVessel(B b)
        {
            b.On(Vessel);int sides=b.Sides(32);b.S.Push(WeaponSculpt.S(VesselScale,VesselScale,VesselScale));
            // The shell: up the outside from the bottom plate to the lip, over the lip and down the refractory lining to the metal.
            float[] r=new float[ProfileR.Length+5],y=new float[ProfileY.Length+5];Array.Copy(ProfileR,r,ProfileR.Length);Array.Copy(ProfileY,y,ProfileY.Length);int n=ProfileR.Length;
            r[n]=1.09f;y[n]=2.52f;r[n+1]=1.10f;y[n+1]=2.66f;r[n+2]=.95f;y[n+2]=2.75f;r[n+3]=MouthOpen;y[n+3]=2.70f;r[n+4]=.76f;y[n+4]=2.5f;
            b.LatheY(i=>i>=n+1?P(LiningHex,.04f):i>=n-1?Edge:Iron,r,y,sides,Nose,1,false);
            // The bottom plate and the wind box drum under it.
            b.LatheY(_=>IronDark,new[]{.95f,.92f,.90f,.86f,.70f},new[]{-1.98f,-2.04f,-2.2f,-2.3f,-2.34f},sides,null,1,false);
            // Hoop bands and the trunnion ring.
            foreach(float hy in new[]{-1.5f,-.95f,.9f,1.42f,2.05f}){float hr=ShellR(hy);b.LatheY(_=>Edge,new[]{hr+.005f,hr+.05f,hr+.05f,hr+.005f},new[]{hy-.09f,hy-.07f,hy+.07f,hy+.09f},sides,Nose,0,false);}
            b.LatheY(_=>Iron,new[]{1.79f,1.97f,1.99f,1.99f,1.97f,1.79f},new[]{-.38f,-.34f,-.3f,.3f,.34f,.38f},sides,null,1,false);
            if(b.Fine)for(int i=0;i<24;i++){double a=i*Math.PI/12;foreach(float ry in new[]{-.2f,.2f})b.Box(Steel,(float)Math.Sin(a)*2.0f,ry,(float)Math.Cos(a)*2.0f,.05f,.05f,.05f);}
            if(b.Fine)foreach(float hy in new[]{-1.5f,-.95f,.9f,1.42f})for(int i=0;i<20;i++){double a=i*Math.PI/10;float hr=ShellR(hy)+.06f;b.Box(Steel,(float)Math.Sin(a)*hr,hy,(float)Math.Cos(a)*hr+Nose(hy),.04f,.04f,.04f);}
            // Vertical plate seams between the bands (lit edges catch the light: the curve reads).
            if(b.Mid)for(int i=0;i<8;i++){float phi=(float)(i*Math.PI/4+Math.PI/8);foreach(var (y0,y1) in new[]{(-1.4f,-1.05f),(-.85f,-.42f),(.42f,.8f),(1.0f,1.32f)}){float ym=(y0+y1)*.5f;var f=OnShell(phi,ym,.01f);b.BoxIn(f,Edge,Vector3.Zero,new Vector3(.03f,y1-y0,.02f));}}
            // The trunnion pins (the right one carries the bull gear, the left one the blast pipe).
            b.Rod(Steel,new Vector3(1.9f,0,0),new Vector3(2.98f,0,0),.3f,16,.03f);b.Rod(Steel,new Vector3(-1.9f,0,0),new Vector3(-2.62f,0,0),.3f,16,.03f);
            // The bull gear: rim, spokes, hub, teeth (it turns with the vessel; the pinion under it is the yoke's).
            {
                const float gx=2.68f,gw=.17f,gr=.95f;int segs=b.Sides(28);
                for(int i=0;i<segs;i++){double a0=i*Math.PI*2/segs,a1=(i+1)*Math.PI*2/segs;var p0=new Vector3(gx+gw*.5f,(float)Math.Sin(a0)*gr,(float)Math.Cos(a0)*gr);var p1=new Vector3(gx+gw*.5f,(float)Math.Sin(a1)*gr,(float)Math.Cos(a1)*gr);
                    var mid=(p0+p1)*.5f;b.S.Push(Frame(mid,Vector3.Normalize(p1-p0)));b.Box(GreenOf(),0,0,0,gw,.17f,(p1-p0).Length()+.02f);b.S.Pop();}
                for(int i=0;i<6;i++){double a=i*Math.PI/3;b.Rod(GreenOf(),new Vector3(gx+gw*.5f,0,0),new Vector3(gx+gw*.5f,(float)Math.Sin(a)*(gr-.05f),(float)Math.Cos(a)*(gr-.05f)),.06f,6);}
                b.Rod(GreenOf(),new Vector3(gx,0,0),new Vector3(gx+gw+.04f,0,0),.33f,16,.03f);
                if(b.Mid){int teeth=b.Fine?40:24;for(int i=0;i<teeth;i++){double a=i*Math.PI*2/teeth;var c=new Vector3(gx+gw*.5f,(float)Math.Sin(a)*(gr+.1f),(float)Math.Cos(a)*(gr+.1f));b.S.Push(Frame(c,new Vector3(0,(float)Math.Sin(a),(float)Math.Cos(a))));b.Box(Edge,0,0,0,gw,.07f,.1f);b.S.Pop();}}
            }
            // The blast pipe: out of the left trunnion, round its elbow and down the shell into the wind box.
            {
                var path=new List<Vector3>{new Vector3(-2.6f,0,0),new Vector3(-2.86f,0,0),new Vector3(-2.92f,-.3f,0),new Vector3(-2.7f,-.85f,.0f),new Vector3(-2.2f,-1.45f,.05f),new Vector3(-1.62f,-1.92f,.05f),new Vector3(-1.1f,-2.18f,0),new Vector3(-.86f,-2.2f,0)};
                b.S.Tube(Iron,path,.19f,b.Sides(12),Vector3.UnitZ,1,true);
                if(b.Mid)foreach(int k in new[]{1,3,5})b.Rod(Edge,path[k]-Vector3.Normalize(path[k+1]-path[k-1])*.04f,path[k]+Vector3.Normalize(path[k+1]-path[k-1])*.04f,.25f,12);
            }
            // The VESSEL C nameplate on the front of the lower shell (cream stencil, chipped). Seen from the front the boss's +x is the
            // viewer's left, so the letters run toward -x.
            {
                var f=OnShell(0,-1.06f,.02f);b.BoxIn(f,IronDark,Vector3.Zero,new Vector3(1.46f,.46f,.04f),.01f);
                if(b.Mid){var paint=P(PaintHex,.04f);const float px=.044f;float x0=(Glyphs.Length*4-1)*px*.5f;
                    for(int g=0;g<Glyphs.Length;g++)for(int row=0;row<5;row++)for(int col=0;col<3;col++)if(Glyphs[g][row*3+col]=='1'){float gx=x0-(g*4+col)*px-px*.5f,gy=(2-row)*px*1.25f;if(b.Fine&&((g*7+row*3+col)%11==4))continue;b.BoxIn(f,paint,new Vector3(gx,gy,.025f),new Vector3(px*.86f,px*1.15f,.01f));}}
                if(b.Fine)foreach(float cx in new[]{-.66f,.66f})foreach(float cy in new[]{-.17f,.17f})b.BoxIn(f,Steel,new Vector3(cx,cy,.03f),new Vector3(.04f,.04f,.02f));
            }
            b.S.Pop();
            // The mouth's white metal, the hot lip and the metal slopping over the spout down the nose (glow: heat).
            b.On(VesselHeat);b.S.Push(WeaponSculpt.S(VesselScale,VesselScale,VesselScale));
            b.LatheY(_=>Metal,new[]{.001f,.76f},new[]{MetalAt,MetalAt},b.Sides(24),Nose,0,false);
            b.LatheY(_=>Molten,new[]{.84f,.80f,.77f},new[]{2.71f,2.62f,2.52f},b.Sides(24),Nose,0,false);
            b.LatheY(_=>Molten.Scaled(.55f),new[]{1.04f,.98f,.86f},new[]{2.672f,2.765f,2.735f},b.Sides(24),Nose,0,false);
            // The shell's heat showing in the seams under the hoop bands (dull red; most of it hidden under the crust until it sheds).
            foreach(float hy in new[]{-1.5f,-.95f,.9f,1.42f}){float hr=ShellR(hy-.11f)+.012f;b.LatheY(_=>Molten.Scaled(.3f),new[]{hr,hr},new[]{hy-.135f,hy-.1f},b.Sides(24),Nose,0,false);}
            // The spill: a tongue of metal over the front lip, thinning as it runs down the nose, and two trickles beside it.
            // Orange, not white: the white is the mouth's alone, and a row of white bars reads as lettering from the front.
            foreach(var (phi,len,w0) in new[]{(0f,1.0f,.34f),(-.4f,.55f,.12f),(.33f,.78f,.14f)})
            {
                int seg=b.Mid?7:3;for(int i=0;i<seg;i++)
                {
                    float u0=i/(float)seg,u1=(i+1)/(float)seg;float y0=2.7f-len*u0,y1=2.7f-len*u1,ym=(y0+y1)*.5f;float w=w0*(1-.6f*(u0+u1)*.5f)*(.85f+.3f*(float)Hash(i,(int)(phi*10),41));
                    var f=OnShell(phi+.05f*(float)Math.Sin(i*1.9f+phi*5),ym,.035f);b.BoxIn(f,Molten.Scaled(1.1f-.5f*u0),Vector3.Zero,new Vector3(w,y0-y1+.05f,.05f),.015f,.12f*(float)Math.Sin(i*2.3f+phi));
                }
                if(b.Mid){var d=OnShell(phi+.03f,2.7f-len-.05f,.06f);b.BoxIn(d,Molten.Scaled(.7f),Vector3.Zero,new Vector3(w0*.42f,w0*.5f,.07f),.02f);}
            }
            // The tuyère grate under the wind box (glow: blow): the white air-hot plate and its dark bars.
            b.On(WindBox);
            b.LatheY(_=>Tuyere,new[]{.62f,.001f},new[]{-2.35f,-2.35f},b.Sides(20),null,0,false);
            for(int i=-2;i<=2;i++){b.Span(IronDark,-.6f,.6f,-2.39f,-2.36f,i*.24f-.025f,i*.24f+.025f);b.Span(IronDark,i*.24f-.025f,i*.24f+.025f,-2.4f,-2.37f,-.6f,.6f);}
            b.LatheY(_=>Tuyere,new[]{.93f,.93f},new[]{-2.06f,-2.01f},b.Sides(24),null,0,false);
            b.S.Pop();
        }
        private static readonly string[] Glyphs={"101101101101010","111100110100111","011100010001110","011100010001110","111100110100111","100100100100111","000000000000000","011100100100011"};

        // ---------------- the slag crust: four breakable groups of glassy plates ----------------
        private static void BuildCrust(B b)
        {
            for(int zone=0;zone<4;zone++)
            {
                b.On(CrustFront+zone);b.S.Push(WeaponSculpt.S(VesselScale,VesselScale,VesselScale));float centre=zone==0?0:zone==1?-(float)Math.PI/2:zone==2?(float)Math.PI/2:(float)Math.PI;
                // Overlapping rows of big glassy plates (each row offset half a plate, the upper row over the lower), a dim seam
                // glowing under each plate's edge, a few drips off the lowest row.
                float[] ys=b.Mid?new[]{-.95f,-.5f,.58f,1.0f,1.42f,1.82f}:new[]{-.7f,.7f,1.4f};int cols=b.Mid?4:2;bool side=zone==1||zone==2;
                for(int row=0;row<ys.Length;row++)for(int col=0;col<cols;col++)
                {
                    float y=ys[row]+(float)(Hash(zone,row,col)-.5)*.08f;
                    if(side&&Math.Abs(y)<.6f)continue;if(zone==0&&y<-.45f)continue;if(y>1.9f)continue;
                    float off=(row%2==0?0:.5f);float phi=centre+((col+.5f+off*.6f)/cols-.5f)*1.5f+(float)(Hash(zone,col,row+7)-.5)*.08f;
                    float w=.82f+(float)Hash(zone,row,col+3)*.3f-.12f*Math.Max(0,y-1.2f),h=.6f+(float)Hash(zone,row+5,col)*.18f,t=.16f+(float)Hash(zone,row,col+9)*.05f;
                    var f=OnShell(phi,y,t*.3f);float turn=(float)(Hash(zone,row+2,col+4)-.5)*.3f;
                    var plate=Hash(zone,row,col+11)<.3?SlagEdge:Slag;
                    b.S.Push(f);b.S.Push(WeaponSculpt.Rx(-.12f));b.Box(plate,0,0,0,w,h,t,.05f,0,0,turn);
                    if(b.Mid)b.Box(CrackGlow,0,-h*.5f+.01f,-t*.15f,w*.82f,.035f,t*.7f,0,0,0,turn);
                    if(b.Fine&&Hash(zone,row,col+15)<.55)b.Box(SlagEdge,w*.18f,h*.16f,t*.5f,w*.42f,h*.42f,.05f,.02f,0,0,turn-.2f);
                    b.S.Pop();b.S.Pop();
                    bool lowest=row==0||zone==0&&row==1||side&&row==2;
                    if(b.Mid&&lowest&&Hash(zone,row,col+17)<.75){int drips=b.Fine?2:1;for(int d=0;d<drips;d++){float dx=(d-(drips-1)*.5f)*w*.4f;float len=.12f+(float)Hash(zone,row*3+d,col+19)*.14f;
                        b.S.Push(f);b.S.Push(WeaponSculpt.T(dx,-h*.5f-len*.4f,0));b.Box(Slag,0,0,0,.09f,len*.8f,.08f,.02f);b.Box(Slag,0,-len*.5f,0,.055f,len*.4f,.05f,.015f);b.S.Pop();b.S.Pop();}}
                }
                b.S.Pop();
            }
        }

        // ---------------- the phase 3 seams: white-hot cracks through the shell ----------------
        private static void BuildSeams(B b)
        {
            b.On(Seams);b.S.Push(WeaponSculpt.S(VesselScale,VesselScale,VesselScale));
            for(int k=0;k<7;k++)
            {
                float phi=(float)(k*Math.PI*2/7+.3),y=2.3f;int steps=b.Mid?9:4;float drop=3.6f/steps;
                for(int i=0;i<steps;i++)
                {
                    float y1=y-drop,phi1=phi+(float)(Hash(k,i,31)-.5)*.32f;float ym=(y+y1)*.5f,pm=(phi+phi1)*.5f;var f=OnShell(pm,ym,.012f);
                    float r=ShellR(ym);float dx=(phi1-phi)*r,len=(float)Math.Sqrt(dx*dx+drop*drop);float turn=(float)Math.Atan2(dx,drop);
                    b.BoxIn(f,SeamGlow,Vector3.Zero,new Vector3(.05f-.003f*i,len+.03f,.02f),0,turn);
                    if(b.Fine&&i%3==1){b.BoxIn(f,SeamGlow,new Vector3(.12f,-.05f,0),new Vector3(.025f,.32f,.02f),0,turn+.9f);}
                    y=y1;phi=phi1;
                }
            }
            b.S.Pop();
        }

        // ---------------- the pulpit and its levers ----------------
        private static void BuildPulpit(B b)
        {
            b.On(Pulpit);var grate=P(0x2a2724,.04f);
            b.Span(grate,-1.1f,1.1f,-.1f,0,-.62f,.62f);if(b.Mid)for(int i=0;i<10;i++)b.Span(IronDark,-1.08f+i*.24f,-1.04f+i*.24f,0,.01f,-.6f,.6f);
            b.Span(Iron,-1.12f,1.12f,-.18f,-.1f,-.64f,.64f);
            // The railing (worn yellow) on the sides and the back, the heat shield in front with its slit.
            foreach(float x in new[]{-1.06f,1.06f})foreach(float z in new[]{-.58f,0f,.56f})b.Span(Yellow,x-.03f,x+.03f,0,1.05f,z-.03f,z+.03f);
            foreach(float x in new[]{-.36f,.36f})b.Span(Yellow,x-.03f,x+.03f,0,1.05f,-.61f,-.55f);
            foreach(float h in new[]{.55f,1.04f}){b.Span(Yellow,-1.09f,-1.03f,h-.03f,h+.03f,-.6f,.6f);b.Span(Yellow,1.03f,1.09f,h-.03f,h+.03f,-.6f,.6f);b.Span(Yellow,-1.08f,1.08f,h-.03f,h+.03f,-.61f,-.55f);}
            b.Span(Iron,-.92f,.92f,0,1.08f,.54f,.62f,.02f);b.Span(P(GlassHex,.2f),-.5f,.5f,.82f,.94f,.62f,.63f);if(b.Mid)b.Span(Molten.Scaled(.25f),-.48f,.48f,.83f,.93f,.63f,.635f);
            if(b.Fine)for(int i=0;i<7;i++)b.Box(Steel,-.84f+i*.28f,1.05f,.6f,.04f,.04f,.04f);
            // The lever quadrant (the levers themselves turn on Lever), the blast valve wheel, the gauge box with its cyan dials.
            b.Span(IronDark,-.4f,.4f,.0f,.55f,.24f,.48f,.02f);b.Span(Edge,-.36f,.36f,.52f,.58f,.26f,.46f);
            var wheel=new Vector3(-.78f,1.02f,.3f);b.Rod(Iron,new Vector3(-.78f,0,.3f),wheel,.04f,6);
            b.S.Push(WeaponSculpt.T(wheel.X,wheel.Y,wheel.Z));b.S.Torus(P(GreenHex,.05f),Vector3.Zero,.26f,.03f,b.Sides(16),b.Sides(6));for(int i=0;i<3;i++){float a=i*(float)Math.PI/3;b.Box(P(GreenHex,.05f),0,0,0,.5f,.03f,.03f,0,0,0,a);}b.S.Pop();
            b.Span(IronDark,.58f,.98f,.8f,1.38f,.3f,.5f,.02f);for(int i=0;i<3;i++){float gy=.92f+i*.16f;b.S.Push(WeaponSculpt.T(.78f,gy,.286f));b.S.Cylinder(Gauge,.06f,0,.014f,b.Sides(10));b.S.Pop();if(b.Mid)b.Span(IronDark,.775f,.785f,gy-.005f,gy+.05f,.28f,.284f);}
            // The shift whistle on its pipe at the back right (it blows the end of the shift when the Converter dies).
            b.Rod(Iron,new Vector3(.9f,0,-.5f),new Vector3(.9f,1.7f,-.5f),.05f,8);b.Rod(P(0x8a6a2a,.25f),new Vector3(.9f,1.7f,-.5f),new Vector3(.9f,2.05f,-.5f),.09f,10,.01f);b.Rod(Iron,new Vector3(.9f,2.05f,-.5f),new Vector3(.9f,2.1f,-.5f),.11f,10);
            // The levers (turning on Lever about the pivot): two rods and their knobs.
            b.On(Lever);foreach(float x in new[]{-.2f,.2f}){b.Rod(Steel,LeverPivot+new Vector3(x,0,0),LeverPivot+new Vector3(x,.88f,-.08f),.03f,6);b.Box(P(0x7a1e18,.08f),LeverPivot.X+x,LeverPivot.Y+.9f,LeverPivot.Z-.08f,.08f,.1f,.08f,.02f);}
            b.Rod(Edge,LeverPivot+new Vector3(-.3f,0,0),LeverPivot+new Vector3(.3f,0,0),.06f,8);
        }

        // ---------------- the dead foreman: silver heat suit, hood with a cyan visor, a white hard hat ----------------
        private static void BuildForeman(B b)
        {
            b.On(Foreman);var silver=P(SilverHex,.1f);var dark=P(SilverDarkHex,.05f);var quilt=P(QuiltHex,.06f);var glove=P(LeatherHex,.03f);var boot=P(RubberHex);
            foreach(float s in new[]{-1f,1f})
            {
                b.Box(boot,s*.12f,.08f,.04f,.15f,.16f,.3f,.03f);b.Box(silver,s*.12f,.5f,0,.17f,.72f,.18f,.03f);if(b.Mid)b.Box(dark,s*.12f,.34f,.0f,.18f,.04f,.19f);
            }
            // The coat to the knees, belt, chest, the hood's cape.
            b.Box(silver,0,1.0f,0,.52f,.92f,.34f,.05f);if(b.Mid){for(int i=0;i<4;i++)b.Box(quilt,0,.68f+i*.18f,.172f,.46f,.012f,.008f);b.Box(dark,0,.8f,-.172f,.4f,.5f,.008f);}
            b.Box(P(0x2a2622),0,1.0f,0,.54f,.07f,.36f,.01f);b.Box(silver,0,1.38f,0,.6f,.3f,.38f,.05f);b.Box(silver,0,1.54f,-.01f,.68f,.08f,.42f,.03f);
            // The respirator canister and its cyan lamp.
            b.Box(P(0x1c1a18,.04f),.12f,1.36f,.2f,.11f,.13f,.08f,.02f);if(b.Mid)b.Box(Gauge,.12f,1.44f,.245f,.035f,.02f,.01f);
            // The hood: square, silver, the visor a wide cyan slit; a white foreman's hard hat over it.
            b.Box(silver,0,1.78f,.01f,.36f,.38f,.36f,.05f);b.Box(P(GlassHex,.25f),0,1.8f,.19f,.28f,.1f,.01f);b.Box(Visor,0,1.8f,.196f,.26f,.05f,.008f);
            b.Box(P(HatHex,.12f),0,2.02f,.01f,.36f,.13f,.4f,.05f);b.Box(P(HatHex,.1f),0,1.96f,.03f,.46f,.025f,.5f,.01f);if(b.Mid)b.Box(P(0x2a2622),0,2.02f,.01f,.37f,.03f,.41f);
            // The arms (turning on ForemanArms with the levers): sleeves, gauntlets on the knobs.
            b.On(ForemanArms);
            foreach(float s in new[]{-1f,1f})
            {
                var sh=new Vector3(s*.33f,1.48f,.02f);var el=new Vector3(s*.31f,1.2f,.3f);var hand=new Vector3(s*.22f,1.36f,.56f);
                b.Rod(silver,sh,el,.085f,8);b.Rod(silver,el,hand,.075f,8);b.Box(glove,hand.X,hand.Y,hand.Z,.12f,.12f,.12f,.02f);
                b.Box(silver,s*.36f,1.52f,0,.2f,.14f,.22f,.04f);
            }
        }

        // ---------------- legs: coxa, femur, tibia ----------------
        private static void BuildCoxa(B b,int leg)
        {
            b.On(LegJoint(leg,Coxa));
            // Frame: +y the yoke's up, +z out toward the foot. A slewing housing, its gear ring, the stub to the femur pivot.
            b.S.Push(WeaponSculpt.Rx(-(float)Math.PI/2));b.S.Cylinder(Iron,.48f,-.62f,.55f,b.Sides(18),.05f);b.S.Cylinder(Edge,.56f,.34f,.44f,b.Sides(18),.01f);b.S.Cylinder(IronDark,.5f,-.66f,-.56f,b.Sides(18),.02f);b.S.Pop();
            if(b.Fine)for(int i=0;i<20;i++){double a=i*Math.PI/10;b.Box(Steel,(float)Math.Sin(a)*.58f,.39f,(float)Math.Cos(a)*.58f,.06f,.08f,.06f);}
            b.Span(Iron,-.3f,.3f,-.38f,.32f,.12f,CoxaReach+.05f,.04f);
            foreach(float s in new[]{-1f,1f})b.Span(IronDark,s*.36f-.06f,s*.36f+.06f,-.46f,.4f,CoxaReach-.32f,CoxaReach+.24f,.03f);
            b.Box(Yellow,0,.56f,0,.62f,.03f,.62f,.01f,0,(float)Math.PI/4,0);if(b.Mid)b.Box(Stripe,0,.575f,0,.5f,.01f,.12f,0,0,(float)Math.PI/4,0);
        }
        private static void BuildFemur(B b,int leg)
        {
            b.On(LegJoint(leg,Femur));const float L=FemurLength;
            // Frame: +z along the bone to the knee, +y its top. A tapered riveted box girder with stiffeners, a ram on top, hoses.
            var rings=new List<Vector3[]>{WeaponSculpt.Rect(.74f,.8f,.12f,0),WeaponSculpt.Rect(.72f,.78f,.12f,L*.28f),WeaponSculpt.Rect(.58f,.62f,.1f,L*.9f),WeaponSculpt.Rect(.54f,.56f,.09f,L)};
            b.S.Loft(rings,(i,k)=>k==1||k==2?Edge:Iron,0,true,true);
            if(b.Mid)for(int i=0;i<4;i++){float z=.3f+i*.5f,hw=.37f-.035f*i,hh=.4f-.036f*i;b.Span(Edge,-hw-.015f,hw+.015f,-hh-.015f,hh+.015f,z-.035f,z+.035f);}
            if(b.Fine)for(float z=.12f;z<L*.9f;z+=.15f)foreach(float x in new[]{-.27f,.27f}){float hh=.4f-.07f*z/L;b.Box(Steel,x*(1-.2f*z/L),hh+.005f,z,.04f,.025f,.04f);}
            // The ram: barrel and chrome rod along the top, clevises.
            b.Rod(IronDark,new Vector3(0,.58f,.12f),new Vector3(0,.55f,L*.6f),.15f,12,.02f);b.Rod(SteelLight,new Vector3(0,.55f,L*.6f),new Vector3(0,.5f,L*.94f),.075f,8);
            b.Span(IronDark,-.11f,.11f,.38f,.62f,.02f,.22f);b.Span(IronDark,-.1f,.1f,.3f,.56f,L*.88f,L*.99f);
            if(b.Mid)foreach(float s in new[]{-1f,1f}){var path=new List<Vector3>{new Vector3(s*.2f,.42f,.15f),new Vector3(s*.3f,.5f,L*.35f),new Vector3(s*.26f,.36f,L*.7f)};b.S.Tube(P(RubberHex),path,.04f,b.Sides(6),Vector3.UnitY,1,true);}
            // The knee hub on the hinge (local x), its striped caps.
            b.S.Push(WeaponSculpt.Ry((float)Math.PI/2));b.S.Cylinder(Iron,.5f,-.42f,.42f,b.Sides(18),.05f,-L,0);b.S.Pop();
            if(b.Mid)foreach(float s in new[]{-1f,1f}){b.S.Push(WeaponSculpt.Ry(s*(float)Math.PI/2));b.S.Cylinder(Yellow,.38f,.42f,.46f,b.Sides(18),0,-s*L,0);b.S.Cylinder(IronDark,.14f,.46f,.52f,b.Sides(10),.01f,-s*L,0);b.S.Pop();
                for(int i=0;i<3;i++)b.Box(Stripe,s*.461f,0,L+(i-1)*.2f,.01f,.68f,.08f,0,.55f,0,0);}
        }
        private static void BuildTibia(B b,int leg)
        {
            b.On(LegJoint(leg,Tibia));const float L=TibiaLength;
            // Frame: +z along the bone to the ankle, +y its outer face. A long tapered box with lightening holes, a shin guard on its
            // outer face, a ram on the inner face, heat-scarred lower plates.
            var rings=new List<Vector3[]>{WeaponSculpt.Rect(.62f,.68f,.1f,.3f),WeaponSculpt.Rect(.6f,.66f,.1f,L*.35f),WeaponSculpt.Rect(.46f,.5f,.08f,L*.9f),WeaponSculpt.Rect(.44f,.48f,.08f,L)};
            b.S.Loft(rings,(i,k)=>k==1||k==2?Edge:Iron,0,true,true);
            if(b.Mid)for(int i=0;i<3;i++){float z=.8f+i*.72f,h=.2f-.02f*i;foreach(float s in new[]{-1f,1f})b.Span(IronDark,s*(.3f-.02f*i)-.008f,s*(.3f-.02f*i)+.008f,-h,h,z-.24f,z+.24f);}
            // The shin guard: three overlapping plates down the outer face.
            for(int i=0;i<3;i++){float z0=.45f+i*.82f,z1=z0+.95f,w=.6f-.06f*i;b.Box(i==0?Yellow:Iron,0,.36f-.03f*i,(z0+z1)*.5f,w,.06f,z1-z0,.02f,-.04f);if(b.Fine)for(int k=0;k<3;k++)b.Box(Steel,(k-1)*w*.38f,.395f-.03f*i,z0+.08f,.04f,.03f,.04f);}
            b.Rod(IronDark,new Vector3(0,-.4f,.3f),new Vector3(0,-.36f,L*.42f),.12f,10,.02f);b.Rod(SteelLight,new Vector3(0,-.36f,L*.42f),new Vector3(0,-.3f,L*.66f),.06f,8);
            b.Span(IronDark,-.09f,.09f,-.46f,-.3f,.2f,.38f);b.Span(IronDark,-.08f,.08f,-.32f,-.22f,L*.64f,L*.7f);
            var scale=P(ScaleHex,.03f);b.Span(scale,-.24f,.24f,-.26f,.26f,L*.72f,L*.93f,.03f);if(b.Fine)for(int i=0;i<4;i++)b.Span(P(SootHex),-.245f,.245f,-.265f,.265f,L*.74f+i*.05f,L*.74f+i*.05f+.012f);
            b.S.Push(WeaponSculpt.Ry((float)Math.PI/2));b.S.Cylinder(Iron,.27f,-.3f,.3f,b.Sides(12),.02f,-L,0);b.S.Pop();
        }

        // ---------------- feet: the front ingot tongs, the rear bogies ----------------
        private static void BuildTongs(B b,int leg)
        {
            // Foot frame: origin the ankle (AnkleFront over the ground), +y up, +z the body's forward. The ankle housing and the hinge.
            b.On(LegJoint(leg,Foot));b.Box(Iron,0,-.04f,0,.6f,.48f,.66f,.05f);b.Box(Yellow,0,.21f,0,.5f,.03f,.56f,.01f);
            b.S.Push(WeaponSculpt.Ry((float)Math.PI/2));b.S.Cylinder(Steel,.15f,-.4f,.4f,b.Sides(12),.02f,0,JawHinge);b.S.Pop();
            for(int which=0;which<2;which++)
            {
                b.On(Jaw(leg,which));float s=which==0?1:-1;const int segs=8;var hinge=new Vector3(0,JawHinge,0);const float rad=JawRadius;var centre=hinge+new Vector3(0,-rad,0);
                // A curved bar from the hinge round the front (s +1) or back to a tip under the ankle, teeth inside, the tip hot.
                for(int i=0;i<segs;i++)
                {
                    double a0=Math.PI*(.04+.92*i/segs),a1=Math.PI*(.04+.92*(i+1)/segs);
                    var p0=centre+new Vector3(0,(float)Math.Cos(a0)*rad,s*(float)Math.Sin(a0)*rad);var p1=centre+new Vector3(0,(float)Math.Cos(a1)*rad,s*(float)Math.Sin(a1)*rad);
                    float th=.21f-.012f*i;var mid=(p0+p1)*.5f;b.S.Push(Frame(mid,Vector3.Normalize(p1-p0)));b.Box(i>=segs-2?TongHot:Iron,0,0,0,.26f,th,(p1-p0).Length()+.06f,.025f);
                    if(b.Mid&&i>0&&i<segs-2)b.Box(Edge,0,th*.5f,0,.22f,.02f,(p1-p0).Length()*.7f);b.S.Pop();
                    if(b.Mid&&i>0&&i<segs-1){var inner=centre+(mid-centre)*.76f;b.S.Push(Frame(inner,Vector3.Normalize(centre-mid)));b.Box(Edge,0,0,0,.18f,.06f,.1f);b.S.Pop();}
                }
                var tip=centre+new Vector3(0,-rad-.05f,s*.03f);b.Box(TongHot,0,tip.Y,tip.Z,.2f,.1f,.14f,.02f);
                b.Box(Iron,0,JawHinge,0,.3f,.36f,.36f,.04f);
            }
        }
        private static void BuildBogie(B b,int leg)
        {
            // Foot frame: origin the ankle (AnkleRear over the ground). A crane truck: the frame, two flanged wheels, buffers.
            b.On(LegJoint(leg,Foot));b.Box(Iron,0,-.04f,0,.56f,.46f,.6f,.05f);b.Box(Yellow,0,.2f,0,.44f,.03f,.48f,.01f);
            b.Span(Iron,-.38f,.38f,-.6f,-.24f,-.82f,.82f,.05f);b.Span(Yellow,-.39f,.39f,-.28f,-.24f,-.78f,.78f);if(b.Mid)for(int i=0;i<6;i++)b.Box(Stripe,-.395f,-.42f,-.6f+i*.24f,.01f,.3f,.07f,0,.6f,0,0);
            foreach(float z in new[]{-.5f,.5f}){b.S.Push(WeaponSculpt.Ry((float)Math.PI/2));b.S.Cylinder(Steel,.34f,-.24f,.24f,b.Sides(16),.02f,-z,-.5f);b.S.Cylinder(IronDark,.38f,-.24f,-.18f,b.Sides(16),0,-z,-.5f);b.S.Cylinder(IronDark,.1f,.24f,.32f,b.Sides(8),0,-z,-.5f);b.S.Pop();}
            foreach(float z in new[]{-.88f,.88f})b.Box(IronDark,0,-.44f,z,.42f,.18f,.1f,.02f);
            if(b.Mid)b.Span(P(SootHex),-.36f,.36f,-.84f,-.82f,-.72f,.72f);
        }

        // ---------------- chain props ----------------
        private static void BuildLink(B b)
        {
            // One link along +z (.15 m), its bar .03 m: two sides and two ends.
            b.On(0);var p=P(0x3a3632,.08f);b.Box(p,-.035f,0,0,.026f,.026f,.15f);b.Box(p,.035f,0,0,.026f,.026f,.15f);b.Box(p,0,0,-.068f,.096f,.026f,.026f);b.Box(p,0,0,.068f,.096f,.026f,.026f);
        }
        private static void BuildHook(B b)
        {
            // A crane hook hanging from its eye at the origin (down -y): the eye, the shank, the bowl and the point.
            b.On(0);var p=P(0x2c2926,.06f);b.Box(p,0,-.06f,0,.14f,.16f,.1f,.02f);b.Box(p,0,-.3f,0,.08f,.34f,.08f,.015f);
            for(int i=0;i<7;i++){double a=Math.PI*(1.15-i/6.0*1.3);b.Box(p,(float)Math.Cos(a)*.17f,-.62f-(float)Math.Sin(a)*.17f,0,.1f,.1f,.09f,.015f,0,0,(float)a);}
            b.Box(p,.16f,-.5f,0,.06f,.14f,.07f,.01f);
        }
        private static void BuildShackle(B b)
        {
            b.On(0);var p=P(0x4a463f,.08f);b.Box(p,-.07f,-.1f,0,.04f,.2f,.04f);b.Box(p,.07f,-.1f,0,.04f,.2f,.04f);b.Box(p,0,-.21f,0,.18f,.04f,.04f);b.Box(p,0,0,0,.2f,.035f,.035f);
        }

        // ================================================================================================================
        // posing
        // ================================================================================================================
        /// <summary>The key poses the concept is drawn in (the doc's animation section names their place in each action).</summary>
        public const int KeyIdle=0,KeyWalk=1,KeyPourWindup=2,KeyPour=3,KeyBlowInhale=4,KeyBlow=5,KeyStompLift=6,KeyStomp=7,KeyGrabReach=8,KeyGrabHold=9,KeyRear=10,KeySlam=11,
            KeyDeslag=12,KeyChargeWindup=13,KeyCharge=14,KeyKneel=15,KeyArrive=16,KeyShed=17,KeyRunaway=18,KeyDead=19,Keys=20;
        public static readonly string[] KeyNames={"idle","walk","pour-windup","pour","blow-inhale","blow","stomp-lift","stomp","grab-reach","grab-hold","rear","slam","deslag","charge-windup","charge","kneel","arrive","shed","runaway","dead"};
        /// <summary>Key poses that stand on the floor (every planted foot reaches its target; nothing goes through the floor).</summary>
        public static bool Standing(int key)=>key!=KeyArrive&&key!=KeyDead&&key!=KeyKneel;
        public static int KeyIndex(string name){for(int i=0;i<Keys;i++)if(KeyNames[i]==name)return i;return -1;}
        /// <summary>The standing pose every key starts from.</summary>
        public static IronworksBossPose Stance()=>new IronworksBossPose{Height=StandHeight,Tilt=10,FL=new Vector3(-3.95f,0,3.25f),FR=new Vector3(3.95f,0,3.25f),RL=new Vector3(-3.75f,0,-3.45f),RR=new Vector3(3.75f,0,-3.45f),
            Heat=.85f,Blow=.15f,Crust=15,OpenL=2,OpenR=2};
        /// <summary>A key pose (`u` a phase within it where it has one: the walk's cycle).</summary>
        public static IronworksBossPose Key(int key,float u=.5f)
        {
            var p=Stance();
            switch(key)
            {
                case KeyWalk:
                {
                    // A diagonal trot: FL and RR swing while FR and RL bear the weight; the body rides forward and rolls onto the planted pair.
                    float s=(float)Math.Sin(u*Math.PI);float sw=-1.2f+2.4f*u;
                    p.FL=new Vector3(-3.95f,.75f*s,3.25f+sw);p.RR=new Vector3(3.75f,.65f*s,-3.45f+sw);p.FR=new Vector3(3.95f,0,3.25f-sw*.6f);p.RL=new Vector3(-3.75f,0,-3.45f-sw*.6f);
                    p.PitchFL=-18*s;p.PitchRR=10*s;p.ShiftZ=.25f;p.Height=3.62f;p.Roll=2.5f;p.Tilt=2;p.SwingX=-9;p.SwingZ=3;break;
                }
                case KeyPourWindup:
                    p.Tilt=-14;p.Pitch=4;p.Height=3.72f;p.FL=new Vector3(-4.3f,0,3.6f);p.FR=new Vector3(4.3f,0,3.6f);p.Lever=28;p.Lean=-8;p.Heat=1.25f;p.SwingX=-7;break;
                case KeyPour:
                    p.Tilt=80;p.Pitch=-6;p.Height=3.35f;p.FL=new Vector3(-4.4f,0,3.45f);p.FR=new Vector3(4.4f,0,3.45f);p.RL=new Vector3(-3.95f,0,-3.7f);p.RR=new Vector3(3.95f,0,-3.7f);
                    p.Lever=-30;p.Lean=12;p.Heat=1.45f;p.SwingX=12;break;
                case KeyBlowInhale:
                    p.Tilt=-4;p.Height=3.38f;p.Pitch=2;p.Blow=1.0f;p.Heat=1.3f;p.Lean=-6;p.Lever=10;break;
                case KeyBlow:
                    p.Tilt=-8;p.Height=3.95f;p.Pitch=5;p.Blow=1.3f;p.Heat=2.0f;p.FL=new Vector3(-4.25f,0,3.45f);p.FR=new Vector3(4.25f,0,3.45f);p.RL=new Vector3(-4.0f,0,-3.65f);p.RR=new Vector3(4.0f,0,-3.65f);
                    p.SwingX=-5;p.Lean=-10;p.Lever=10;break;
                case KeyStompLift:
                    p.FR=new Vector3(4.55f,2.5f,4.35f);p.PitchFR=-30;p.OpenR=10;p.Roll=-6;p.ShiftX=-.3f;p.Height=3.72f;p.Tilt=2;p.SwingZ=-8;break;
                case KeyStomp:
                    p.FR=new Vector3(4.35f,0,4.05f);p.OpenR=3;p.Height=3.3f;p.Roll=3;p.Tilt=15;p.Heat=1.25f;p.SwingX=12;p.SwingZ=6;p.Lean=10;break;
                case KeyGrabReach:
                    p.FL=new Vector3(-2.7f,1.55f,6.3f);p.PitchFL=-62;p.OpenL=44;p.Pitch=-5;p.ShiftZ=.5f;p.Roll=4;p.Height=3.45f;p.Tilt=10;p.SwingX=8;break;
                case KeyGrabHold:
                    p.FL=new Vector3(-2.8f,2.8f,4.7f);p.PitchFL=-28;p.OpenL=5;p.Pitch=3;p.Height=3.7f;p.Tilt=-6;p.Lever=15;p.SwingX=-6;break;
                case KeyRear:
                    p.Pitch=24;p.Height=4.15f;p.ShiftZ=-.55f;p.FL=new Vector3(-3.9f,2.5f,3.8f);p.FR=new Vector3(3.9f,2.8f,3.95f);p.PitchFL=34;p.PitchFR=34;p.OpenL=26;p.OpenR=28;
                    p.RL=new Vector3(-4.0f,0,-3.95f);p.RR=new Vector3(4.0f,0,-3.95f);p.Tilt=-12;p.Heat=1.4f;p.SwingX=-20;p.Lean=-16;break;
                case KeySlam:
                    p.Pitch=-9;p.Height=3.05f;p.FL=new Vector3(-4.3f,0,4.15f);p.FR=new Vector3(4.3f,0,4.15f);p.OpenL=6;p.OpenR=6;p.Tilt=34;p.Heat=1.6f;p.SwingX=24;p.Lean=18;break;
                case KeyDeslag:
                    p.Tilt=-100;p.Pitch=-4;p.Lever=40;p.Lean=-10;p.Heat=1.0f;p.SwingX=-6;break;
                case KeyChargeWindup:
                    p.Height=2.95f;p.Pitch=-9;p.RL=new Vector3(-3.9f,0,-4.15f);p.RR=new Vector3(3.9f,0,-4.15f);p.FL=new Vector3(-3.7f,0,3.0f);p.FR=new Vector3(3.7f,0,3.0f);p.Tilt=24;p.Heat=1.3f;p.Lean=10;
                    p.Crust=0;p.Seams=1;p.Pulpit=1;break;
                case KeyCharge:
                    p.FL=new Vector3(-3.9f,.95f,4.7f);p.PitchFL=-22;p.FR=new Vector3(3.9f,0,3.8f);p.RL=new Vector3(-3.8f,0,-2.65f);p.RR=new Vector3(3.8f,1.05f,-4.05f);p.PitchRR=15;
                    p.Pitch=-6;p.Height=3.3f;p.Tilt=20;p.ShiftZ=.4f;p.SwingX=-26;p.Heat=1.5f;p.Crust=0;p.Seams=1;p.Pulpit=1;break;
                case KeyKneel:
                    p.FL=new Vector3(-3.3f,0,2.35f);p.FR=new Vector3(3.3f,0,2.35f);p.Pitch=-15;p.Height=2.75f;p.Tilt=20;p.Blow=.7f;p.Heat=.9f;p.Lean=24;p.SwingX=18;p.OpenL=12;p.OpenR=12;break;
                case KeyArrive:
                    p.Height=1.25f;p.Pitch=13;p.FL=new Vector3(-3.0f,0,3.25f);p.FR=new Vector3(3.0f,0,3.35f);p.RL=new Vector3(-2.4f,-1.8f,-2.5f);p.RR=new Vector3(2.4f,-1.8f,-2.5f);
                    p.Tilt=16;p.Heat=1.9f;p.Lean=-6;p.SwingX=14;p.OpenL=22;p.OpenR=22;break;
                case KeyShed:
                    p.Pitch=14;p.Height=3.95f;p.Crust=0;p.Shed=.45f;p.Tilt=-14;p.FL=new Vector3(-4.1f,1.0f,3.75f);p.PitchFL=20;p.OpenL=18;p.Heat=1.6f;p.SwingX=-12;p.Lean=-12;p.Lever=35;break;
                case KeyRunaway:
                    p.Crust=0;p.Seams=1;p.Pulpit=1;p.Height=3.45f;p.Pitch=-4;p.Tilt=12;p.Heat=1.8f;p.Blow=.4f;p.SwingX=6;p.Lean=20;break;
                case KeyDead:
                    p.Height=1.65f;p.Pitch=-8;p.Roll=9;p.FL=new Vector3(-4.6f,0,3.0f);p.FR=new Vector3(4.5f,0,3.6f);p.RL=new Vector3(-4.5f,0,-3.0f);p.RR=new Vector3(4.2f,0,-3.8f);
                    p.Tilt=62;p.Heat=0;p.Blow=0;p.Seams=0;p.Cool=1;p.Crust=0;p.Pulpit=1;p.Lean=30;p.OpenL=18;p.OpenR=8;break;
            }
            // The keys are authored on a 3.95 m stance; the body stands on a wider one.
            p.FL=Wide(p.FL);p.FR=Wide(p.FR);p.RL=Wide(p.RL);p.RR=Wide(p.RR);
            return p;
        }

        private static Vector3 Wide(Vector3 f)=>new Vector3(f.X*StanceWide,f.Y,f.Z*StanceLong);
        /// <summary>How much wider and longer the stance is than the keys' authored 3.95 m.</summary>
        public const float StanceWide=1.09f,StanceLong=1.07f;
        /// <summary>The hips (yoke frame) per leg.</summary>
        public static Vector3 Hip(int leg)=>new Vector3(leg==FL||leg==RL?-HipX:HipX,HipY,leg<2?HipFrontZ:HipRearZ);
        public static float Ankle(int leg)=>leg<2?AnkleFront:AnkleRear;

        /// <summary>
        /// Every joint's matrix for a pose (row vectors, local to model space: model = local x M), into `m` (Count long). The legs
        /// solve a two-bone IK in the plane of the yoke's up and the foot (the knee bends up and out; out of reach the ankle stops
        /// short along the line). Allocates nothing.
        /// </summary>
        public static void Solve(in IronworksBossPose p,Matrix4x4[] m)
        {
            m[Root]=Matrix4x4.Identity;
            var yoke=Matrix4x4.CreateRotationZ(Rad(p.Roll))*Matrix4x4.CreateRotationX(-Rad(p.Pitch))*Matrix4x4.CreateTranslation(p.ShiftX,p.Height,p.ShiftZ);m[Yoke]=yoke;
            var vessel=Matrix4x4.CreateRotationX(Rad(p.Tilt))*yoke;m[Vessel]=m[VesselHeat]=m[WindBox]=m[Seams]=vessel;
            for(int c=0;c<4;c++)m[CrustFront+c]=(p.Crust&(1<<c))!=0||!(p.Shed>0)?vessel:Fling(c,p.Shed)*vessel;
            // The pulpit, torn away: swung down behind the arch about its hinge.
            float tear=Clamp01(p.Pulpit);var pul=Matrix4x4.CreateTranslation(PulpitRest);
            if(tear>0)pul=pul*Matrix4x4.CreateTranslation(-PulpitHinge)*Matrix4x4.CreateRotationZ(Rad(14*tear))*Matrix4x4.CreateRotationX(Rad(-72*tear))*Matrix4x4.CreateTranslation(PulpitHinge);
            m[Pulpit]=pul*yoke;
            m[Lever]=Matrix4x4.CreateTranslation(-LeverPivot)*Matrix4x4.CreateRotationX(-Rad(p.Lever))*Matrix4x4.CreateTranslation(LeverPivot)*m[Pulpit];
            m[Foreman]=Matrix4x4.CreateRotationX(Rad(p.Lean))*Matrix4x4.CreateTranslation(ForemanFeet)*m[Pulpit];
            m[ForemanArms]=Matrix4x4.CreateTranslation(-Shoulders)*Matrix4x4.CreateRotationX(-Rad(p.Lever*.55f))*Matrix4x4.CreateTranslation(Shoulders)*m[Foreman];
            var up=Vector3.Normalize(new Vector3(yoke.M21,yoke.M22,yoke.M23));var fwd=new Vector3(yoke.M31,0,yoke.M33);fwd=fwd.LengthSquared()>1e-8f?Vector3.Normalize(fwd):Vector3.UnitZ;
            for(int leg=0;leg<Legs;leg++)
            {
                var h=Vector3.Transform(Hip(leg),yoke);var f=p.Foot(leg);var a=new Vector3(f.X,f.Y+Ankle(leg),f.Z);
                Leg(h,a,up,leg==FL||leg==RL?-1:1,yoke,out var coxa,out var femur,out var tibia,out var ankle);
                m[LegJoint(leg,Coxa)]=coxa;m[LegJoint(leg,Femur)]=femur;m[LegJoint(leg,Tibia)]=tibia;
                var fx=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,fwd));var foot=Matrix4x4.CreateRotationX(-Rad(p.FootPitch(leg)))*Rows(fx,Vector3.UnitY,fwd,ankle);m[LegJoint(leg,Foot)]=foot;
                if(leg<2)
                {
                    float open=Rad(leg==FL?p.OpenL:p.OpenR);var hinge=new Vector3(0,JawHinge,0);
                    m[Jaw(leg,0)]=Matrix4x4.CreateTranslation(-hinge)*Matrix4x4.CreateRotationX(-open)*Matrix4x4.CreateTranslation(hinge)*foot;
                    m[Jaw(leg,1)]=Matrix4x4.CreateTranslation(-hinge)*Matrix4x4.CreateRotationX(open)*Matrix4x4.CreateTranslation(hinge)*foot;
                }
            }
        }
        /// <summary>One leg: the coxa turned toward the ankle about the yoke's up, the femur and tibia by the law of cosines with the
        /// knee bent up; `side` -1 left, +1 right (the way out when the ankle is straight under the hip).</summary>
        private static void Leg(Vector3 hip,Vector3 target,Vector3 up,float side,Matrix4x4 yoke,out Matrix4x4 coxa,out Matrix4x4 femur,out Matrix4x4 tibia,out Vector3 ankle)
        {
            var d=target-hip;var o=d-up*Vector3.Dot(d,up);
            if(o.LengthSquared()<1e-6f){var sx=new Vector3(yoke.M11,yoke.M12,yoke.M13)*side;o=sx-up*Vector3.Dot(sx,up);}o=Vector3.Normalize(o);
            coxa=Rows(Vector3.Cross(up,o),up,o,hip);
            var pivot=hip+o*CoxaReach;var c=target-pivot;float dist=c.Length();var cd=dist>1e-6f?c/dist:-up;
            float lo=Math.Abs(FemurLength-TibiaLength)+.01f,hi=FemurLength+TibiaLength-.01f;float dc=Math.Max(lo,Math.Min(hi,dist));
            float cosA=(FemurLength*FemurLength+dc*dc-TibiaLength*TibiaLength)/(2*FemurLength*dc);cosA=Math.Max(-1,Math.Min(1,cosA));float sinA=(float)Math.Sqrt(1-cosA*cosA);
            var bend=up-cd*Vector3.Dot(up,cd);bend=bend.LengthSquared()>1e-8f?Vector3.Normalize(bend):o;
            var knee=pivot+(cd*cosA+bend*sinA)*FemurLength;ankle=pivot+cd*dc;
            var nrm=Vector3.Normalize(Vector3.Cross(o,up));
            var fz=Vector3.Normalize(knee-pivot);var fy=Vector3.Normalize(Vector3.Cross(nrm,fz));femur=Rows(Vector3.Cross(fy,fz),fy,fz,pivot);
            var tz=Vector3.Normalize(ankle-knee);var ty=Vector3.Normalize(Vector3.Cross(nrm,tz));tibia=Rows(Vector3.Cross(ty,tz),ty,tz,knee);
        }
        /// <summary>A crust plate group's flight as it sheds (vessel frame): out from its side, falling, tumbling.</summary>
        private static Matrix4x4 Fling(int zone,float t)
        {
            var dir=zone==0?Vector3.UnitZ:zone==1?-Vector3.UnitX:zone==2?Vector3.UnitX:-Vector3.UnitZ;
            var move=dir*(3.6f*t)+new Vector3(0,2.6f*t-5.2f*t*t,0);var spin=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,dir));var c=(dir*1.8f+new Vector3(0,.6f,0))*VesselScale;
            return Matrix4x4.CreateTranslation(-c)*Matrix4x4.CreateFromAxisAngle(spin,2.4f*t)*Matrix4x4.CreateTranslation(c+move);
        }

        // ---------------- chains ----------------
        /// <summary>The hanging chains: anchors under the girders (yoke frame), lengths, and the end prop (0 a hook, 1 a shackle).</summary>
        public static readonly Vector3[] ChainAnchor={new Vector3(-GirderX,-.66f,.9f),new Vector3(GirderX,-.66f,.9f),new Vector3(-GirderX,-.66f,-1.4f),new Vector3(GirderX,-.66f,-1.4f)};
        public static readonly float[] ChainLength={1.9f,1.6f,1.4f,2.1f};
        public static readonly byte[] ChainEnd={0,1,1,0};
        public const float LinkPitch=.13f;public const int ChainLinkMax=80,Chains=4;
        /// <summary>
        /// The chains' link matrices into `links` (row vectors, the link mesh to model space) and each chain's end (the hook or
        /// shackle mesh) into `ends`: each hangs from its anchor, swung by the pose's swing (degrees about the yoke's x and z) and
        /// bowed by the body's tilt; a stand-in for the runtime's verlet chain. Returns the links written.
        /// </summary>
        public static int ChainPose(in IronworksBossPose p,Matrix4x4[] joints,Matrix4x4[] links,Matrix4x4[] ends)
        {
            int n=0;var yoke=joints[Yoke];
            for(int c=0;c<Chains;c++)
            {
                var a=Vector3.Transform(ChainAnchor[c],yoke);float swing=p.SwingX*(c%2==0?1:.85f)+(c>=2?4:0),side=p.SwingZ*(c%2==0?-1:1);
                var dir=Vector3.Normalize(new Vector3((float)Math.Sin(Rad(side)),-(float)Math.Cos(Rad(swing))*(float)Math.Cos(Rad(side)),(float)Math.Sin(Rad(swing))));
                int count=(int)(ChainLength[c]/LinkPitch);var prev=a;var at=a;
                for(int i=0;i<count&&n<links.Length;i++)
                {
                    // The chain bows a little toward the vertical below its first links (gravity against the swing).
                    float u=(i+.5f)/count;var d=Vector3.Normalize(Vector3.Lerp(dir,-Vector3.UnitY,.25f*u));at=prev+d*LinkPitch;
                    var mid=(prev+at)*.5f;var f=Frame(mid,d);if((i&1)==1)f=Matrix4x4.CreateRotationZ((float)Math.PI/2)*f;links[n++]=f;prev=at;
                }
                if(ends!=null&&c<ends.Length){var down=Vector3.Normalize(at-a);var ex=Vector3.Normalize(Vector3.Cross(down,Vector3.UnitZ));if(float.IsNaN(ex.X))ex=Vector3.UnitX;var ey=-down;var ez=Vector3.Cross(ex,ey);ends[c]=Rows(ex,ey,ez,at);}
            }
            return n;
        }

        private static float Rad(float deg)=>deg*(float)Math.PI/180;
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static double Hash(int a,int b,int c)=>NativeIronworksKit.Hash(a+0x51,b,c);
    }
}
