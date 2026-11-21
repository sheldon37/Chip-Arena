using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The ranged intros' own checks (I4, docs/INTROS-RANGED.md; owner 2026-11-20 on One Breath: "arms looked really odd"): Sundown, One
    /// Breath, Brass Storm and Lightfall bound onto the measured model as the runtime binds a v2 weapon intro (the match's hold of the
    /// intro's weapon, then Core NativeIntroGrip.Grasp: the off fist on the drawn weapon, a long gun shouldered while it aims), every 1/30 s:
    /// - **the shot leaves the drawn weapon toward its target:** the pistol's bore on the bell's strike, the rifle's on the drone's eye,
    ///   each machine-gun round's on its strike on the shutter, the bow's arrow line where its bolt climbs;
    /// - **a trained shooter's hold:** a long gun's stock in the drawn shoulder pocket and the cheek on the stock while it aims (the drawn
    ///   eyes within a hand of the bore's line, the head facing along it), the off fist on the handguard (IntroPoseChecks' grip), the
    ///   bow's string hand at the drawn jaw at full draw with the bow arm straight;
    /// - **the rest:** a long gun at the low carry (muzzle down) under the name, off the shoulder.
    /// `--intro-ranged-dump ID OUT` writes the drawn body, its weapon and the camera every 1/30 s (JSON lines) for the stick-figure previews.
    /// </summary>
    internal static class IntroRangedChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Ranged intros: "+what);count++;}
        private const float Deg=57.29578f;
        private static float Angle(Vector3 a,Vector3 b)=>(float)(Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)),-1,1))*Deg);

        // ---- the drawn body (as Runtime VisualBodyBind binds a v2 weapon intro's) ----
        /// <summary>One frame of the drawn body: the root, the bound state, and the drawn weapon frames (world, source frame).</summary>
        internal sealed class Drawn
        {
            public readonly NativeVisualRig.Rig Rig;public readonly NativeVisualRig.State State;public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();
            public Vector3 Root;public Quaternion Rotation;public NativeIntroGripAt Grip;public bool GripOn;public string Spec;
            public Drawn(NativeVisualRig.Rig rig){Rig=rig;State=new NativeVisualRig.State(rig);}
            /// <summary>A model bone's world place (by name).</summary>
            public Vector3 Bone(string name){int v=Array.IndexOf(Rig.Names,name);return v<0?Root:Root+Vector3.Transform(State.Position[v],Rotation);}
            /// <summary>A model bone's world turn off the keyed rig's rest (+z ahead, +y up).</summary>
            public Quaternion Turn(string name){int v=Array.IndexOf(Rig.Names,name);return v<0?Rotation:Quaternion.Normalize(Rotation*State.World[v]*Quaternion.Inverse(Rig.AlignedWorld[v]));}
            /// <summary>The drawn weapon frame in hand `left` (world): the frame's origin and its turn.</summary>
            public void Weapon(bool left,out Vector3 at,out Quaternion turn)
            {
                if(left)NativeVisualRig.LeftWeaponFrame(State,Pose,out at,out turn);else NativeVisualRig.WeaponFrame(State,Pose,out at,out turn);
                at=Root+Vector3.Transform(at,Rotation);turn=Quaternion.Normalize(Rotation*turn);
            }
            /// <summary>A point of hand `left`'s weapon frame (world).</summary>
            public Vector3 OnWeapon(bool left,Vector3 local){Weapon(left,out var at,out var turn);return at+Vector3.Transform(local,turn);}
            /// <summary>The drawn gun's muzzle (the Standard skin's crown, as the match's flash leaves it) and its bore (world).</summary>
            public Vector3 Muzzle(out Vector3 bore){Weapon(false,out var at,out var turn);bore=Vector3.Transform(Vector3.UnitZ,turn);return at+Vector3.Transform(new Vector3(0,0,NativeMuzzleFx.Crown(Spec,true)),turn);}
            /// <summary>The drawn eyes (between the head bone and its crown, ahead of it) and the head's facing (world).</summary>
            public Vector3 Eyes(out Vector3 facing){var q=Turn("head");facing=Vector3.Transform(Vector3.UnitZ,q);return Bone("head")+Vector3.Transform(new Vector3(0,.07f,.09f),q);}
        }
        /// <summary>Binds script `s` (holding `spec`) at intro time `t` onto `d`: the match's hold of the weapon, the intro's grip over it.</summary>
        internal static void Bind(NativeIntroV2Script s,string spec,Drawn d,float t)
        {
            d.Spec=spec;s.Pose(t,d.Pose,out d.Root,out d.Rotation);d.Pose.World();
            var g=new NativeVisualRig.Grasp{Relax=Math.Clamp(s.ArmsRelax(t),0,1),Still=0,Exact=IntroPoseChecks.Packed(s,t,NativeIntroPack.Arms)};
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
            if(GunRules.Ranged(w)){g.RightGrip=1;g.Support=1;g.Right=NativeVisualRig.PistolGrip(w);g.SupportHold=NativeVisualRig.GunSupport(w,d.Pose);g.Stock=NativeVisualRig.Stock(w);g.Unshoulder=1;}
            else if(NativeBowWire.Is(spec)){g.LeftGrip=1;g.Left=NativeVisualRig.BowRiser;g.LeftCarries=true;}
            d.GripOn=s is INativeIntroGrip ig&&ig.GripAt(t,out d.Grip)&&d.Grip.Weight>.001f;if(!d.GripOn)d.Grip=default;
            if(d.GripOn)NativeIntroGrip.Grasp(spec,d.Grip,d.Pose,ref g);
            NativeVisualRig.Pose(d.State,d.Pose,g,false,false);
        }

        // ---- the dump (for the stick-figure previews; scratch tool) ----
        private static readonly string[] DumpBones={"pelvis","spine_03","spine_05","neck_01","head","clavicle_l","upperarm_l","lowerarm_l","hand_l","middle_01_l","clavicle_r","upperarm_r","lowerarm_r","hand_r","middle_01_r",
            "thigh_l","calf_l","foot_l","ball_l","thigh_r","calf_r","foot_r","ball_r"};
        private static string V(Vector3 v)=>"["+v.X.ToString("0.####",CultureInfo.InvariantCulture)+","+v.Y.ToString("0.####",CultureInfo.InvariantCulture)+","+v.Z.ToString("0.####",CultureInfo.InvariantCulture)+"]";
        private static string F(float f)=>f.ToString("0.####",CultureInfo.InvariantCulture);
        /// <summary>The drawn body, its weapon and the camera of intro `id` every 1/30 s of presentation age, as JSON lines into `path`.</summary>
        public static void Dump(string id,string path)
        {
            // (an intro by id, or a guide pose of Tests/IntroPoseGallery as gallery:NAME)
            NativeIntroV2Script s;string spec;float duration;
            if(id.StartsWith("gallery:",StringComparison.Ordinal)){var g=IntroPoseGallery.Make(id.Substring(8));s=g;spec=g.Spec;duration=g.Seconds;}
            else{var e=NativeIntroCatalog.Find(id)??throw new Exception("no intro "+id);s=(NativeIntroV2Script)e.Make();spec=e.Spec;duration=e.Duration;}
            var d=new Drawn(ModelPoseChecks.Measured());var sb=new StringBuilder();
            bool bow=NativeBowWire.Is(spec);
            for(int i=0;i<=duration*30+.5f;i++)
            {
                float age=i/30f,t=s.Clock(age);Bind(s,spec,d,t);s.Camera(age,out var view);
                sb.Append("{\"age\":").Append(F(age)).Append(",\"t\":").Append(F(t)).Append(",\"eye\":").Append(V(view.Eye)).Append(",\"look\":").Append(V(view.Look)).Append(",\"fov\":").Append(F(view.Fov));
                sb.Append(",\"bones\":{");for(int b=0;b<DumpBones.Length;b++){if(b>0)sb.Append(',');sb.Append('"').Append(DumpBones[b]).Append("\":").Append(V(d.Bone(DumpBones[b])));}sb.Append('}');
                var eyes=d.Eyes(out var facing);sb.Append(",\"eyes\":").Append(V(eyes)).Append(",\"facing\":").Append(V(facing));
                d.Weapon(bow,out var wat,out var wq);sb.Append(",\"weapon\":{\"at\":").Append(V(wat)).Append(",\"z\":").Append(V(Vector3.Transform(Vector3.UnitZ,wq))).Append(",\"y\":").Append(V(Vector3.Transform(Vector3.UnitY,wq)));
                if(NativeIntroWeapon.Tip(spec,bow,out var tip))sb.Append(",\"tip\":").Append(V(wat+Vector3.Transform(tip,wq)));
                if(NativeIntroWeapon.Muzzle(spec,bow,out var mz))sb.Append(",\"muzzle\":").Append(V(wat+Vector3.Transform(NativeWeaponDressing.Gun(spec)?new Vector3(0,0,NativeMuzzleFx.Crown(spec,true)):mz,wq)));
                if(NativeWeaponDressing.Gun(spec))sb.Append(",\"butt\":").Append(V(wat+Vector3.Transform(new Vector3(0,-.04f,-NativeVisualRig.Stock(spec=="machinegun"?WeaponId.MachineGun:spec=="rifle"?WeaponId.Rifle:WeaponId.Pistol)),wq)));
                sb.Append('}');
                if(d.GripOn&&NativeIntroGrip.Model(NativeIntroGrip.Of(spec),spec,d.Grip.Along,d.Grip.Pull,d.Pose,out var hold))
                {
                    bool left=NativeIntroGrip.SupportLeft(NativeIntroGrip.Of(spec));var want=d.OnWeapon(!left,hold.Center-hold.Axis*d.State.SupportSlide);sb.Append(",\"support\":").Append(V(want));
                }
                var fw=Vector3.Transform(Vector3.UnitZ,d.Rotation);sb.Append(",\"root\":").Append(V(d.Root)).Append(",\"yaw\":").Append(F((float)Math.Atan2(fw.X,fw.Z)));
                sb.Append(",\"grip\":").Append(F(d.GripOn?d.Grip.Weight:0)).Append(",\"shouldered\":").Append(F(d.State.Shouldered)).Append("}\n");
            }
            File.WriteAllText(path,sb.ToString());Console.WriteLine("dumped "+id+" ("+(int)(duration*30+1)+" frames) to "+path);
        }

        // ---- the cheek on the stock (a probe for authoring the long guns' aims; scratch use) ----
        /// <summary>Where the drawn head sits on a shouldered long gun: its centre (the runtime's HeadCentre off the head bone) in the bore's
        /// frame from the weapon frame's origin (`lateral` toward the pilot's left, `up`, `along` the bore), and the head's facing off the bore.</summary>
        internal static void Cheek(Drawn d,out float lateral,out float up,out float along,out float facingOff)
        {
            d.Weapon(false,out var at,out var turn);var z=Vector3.Transform(Vector3.UnitZ,turn);var x=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,z));var y=Vector3.Cross(z,x);
            var q=d.Turn("head");var c=d.Bone("head")+Vector3.Transform(new Vector3(0,.07f,.033f),q)-at;
            lateral=Vector3.Dot(c,x);up=Vector3.Dot(c,y);along=Vector3.Dot(c,z);facingOff=Angle(Vector3.Transform(Vector3.UnitZ,q),z);
        }
        /// <summary>`--intro-ranged-fit KIND`: searches the neck and head (and the blade) of a long gun's aim (KIND kneel or stand) for the
        /// cheek on the stock within the anatomical limits, and prints the best few (scratch use while authoring).</summary>
        /// <summary>The pistol's blow: searches the gun hand's hold (the wrist in the chest's frame, the bore's yaw and pitch off the facing) for
        /// the drawn muzzle just before the drawn mouth, a little to its right, with the wrist inside its range (scratch use while authoring).</summary>
        private static void FitBlow()
        {
            var rig=ModelPoseChecks.Measured();var s=(NativeIntroV2Script)NativeIntroCatalog.Find(NativeIntroSundown.Id).Make();float t=NativeIntroSundown.BlowAt;
            var natural=NativeIntroGrip.Natural(NativeIntroGrip.Kind.Pistol,false);var found=new List<(float cost,string what)>();
            // (the keyed wrist kept ahead of the keyed chest's front, z .15 + a fist, so no hand is inside the torso)
            foreach(float x in new[]{-.3f,-.26f,-.22f,-.18f,-.14f})foreach(float y in new[]{0f,.06f,.12f,.18f,.24f})foreach(float z in new[]{.18f,.22f,.27f,.32f})
            foreach(float yaw in new[]{-.4f,-.2f,0f,.2f,.4f})foreach(float pitch in new[]{1.2f,1.32f,1.44f,1.54f})
            {
                var d=new Drawn(rig){Spec="pistol"};s.Pose(t,d.Pose,out d.Root,out d.Rotation);
                NativeIntroGrip.Hold(d.Pose,false,"pistol",new Vector3(x,y,z),NativeIntroGrip.Aim(d.Pose,yaw,pitch),new Vector3(-1,-.7f,-.2f),1);d.Pose.World();
                var h=Vector3.Transform(-Vector3.UnitY,Quaternion.Normalize(d.Pose.Rotation[CapturedRig.RHand]*Quaternion.Inverse(natural)));float bend=Angle(-Vector3.UnitY,h);if(bend>78)continue;
                var g=new NativeVisualRig.Grasp{RightGrip=1,Support=1,Right=NativeVisualRig.PistolGrip(WeaponId.Pistol),SupportHold=NativeVisualRig.GunSupport(WeaponId.Pistol,d.Pose),Unshoulder=1};
                NativeVisualRig.Pose(d.State,d.Pose,g,false,false);
                var eyes=d.Eyes(out _);var q=d.Turn("head");var mouth=eyes+Vector3.Transform(new Vector3(0,-.065f,.03f),q);var want=eyes+Vector3.Transform(new Vector3(-.13f,.3f,.06f),q);// (the barrel up beside the right cheek, its muzzle over the brow)
                var m=d.Muzzle(out var bore);float cost=Vector3.Distance(m,want);
                found.Add((cost,"wrist ("+x+","+y+","+z+") yaw "+yaw+" pitch "+pitch+": muzzle off "+(cost*100).ToString("0")+" cm, wrist bend "+bend.ToString("0")+" deg, bore up "+(Math.Asin(Math.Clamp(bore.Y,-1,1))*Deg).ToString("0")+" deg"));
            }
            foreach(var f in found.OrderBy(f=>f.cost).Take(12))Console.WriteLine(f.what);
        }
        public static void Fit(string kind)
        {
            if(kind=="blow"){FitBlow();return;}
            // (the head's centre wanted on the rifle's scope: its eyepiece .135 behind the frame's origin and .175 over the bore, the helmet's
            // visor at it, a little left of its line for the right eye; the machine gun's sights lower, on its carry handle)
            var rig=ModelPoseChecks.Measured();var d=new Drawn(rig);bool kneel=kind.StartsWith("kneel");string spec=kind.EndsWith("mg")?"machinegun":"rifle";float pitch=kneel?.09f:-.05f;
            float wantLat=.04f,wantUp=spec=="rifle"?.19f:.14f,wantAlong=spec=="rifle"?-.3f:-.26f;
            var found=new List<(float cost,string what,IntroPoseGallery.Pose pose)>();
            foreach(float blade in new[]{.45f,.6f,.75f})foreach(float nx in new[]{.3f,.45f,.6f,.75f})foreach(float hx in new[]{-.45f,-.3f,-.15f,0f})foreach(float nz in new[]{.1f,.25f,.4f})foreach(float hz in new[]{0f,.15f,.3f})foreach(float lean in new[]{.1f,.22f})
            {
                float sp=-blade*.45f,ch=-blade*.55f;
                var basis=NativeEntranceKeys.With(kneel?NativeIntroPoses.Kneel():NativeIntroPoses.RifleAim(),v=>{NativeEntranceKeys.Set(v,CapturedRig.Body,lean,0,0);NativeEntranceKeys.Set(v,CapturedRig.Spine,.02f,sp,0);NativeEntranceKeys.Set(v,CapturedRig.Chest,.03f,ch,0);
                    NativeEntranceKeys.Set(v,CapturedRig.Neck,nx,0,nz);NativeEntranceKeys.Set(v,CapturedRig.Head,hx,blade,hz);NativeEntranceKeys.Set(v,CapturedRig.LClav,0,-.6f,0);NativeEntranceKeys.Set(v,CapturedRig.RClav,0,.9f,0);});
                var stance=kneel?IntroPoseGallery.Pose.StanceOf(.13f,.42f,.1f,-.13f,-.1f,-.3f):IntroPoseGallery.Pose.StanceOf(.14f,.18f,.3f,-.14f,-.16f,-.6f);
                var p=new IntroPoseGallery.Pose("fit",spec,1.2f,(pp,k,plan)=>{pp.Stance(stance);k.Key(0,basis).Root(0,.88f,0,0);k.Key(1.2f,basis).Root(0,.88f,0,0);},
                    (t,pose,root,rot)=>NativeIntroPoses.AimRifle(pose,spec,0,pitch),t=>new NativeIntroGripAt(1,default,0,0,1),true,kneel?(t,l)=>l?0:1:null);
                Bind(p,spec,d,.6f);Bind(p,spec,d,.6f);Cheek(d,out float lat,out float up,out float along,out float off);
                float cost=(lat-wantLat)*(lat-wantLat)+(up-wantUp)*(up-wantUp)+.5f*(along-wantAlong)*(along-wantAlong)+(off>10?(off-10)*(off-10)*2e-4f:0)+.01f*(nz+hz)*(nz+hz);
                found.Add((cost,"blade "+blade+" lean "+lean+" N "+nx+",0,"+nz+" Hd "+hx+","+blade+","+hz+": lateral "+lat.ToString("0.000")+" up "+up.ToString("0.000")+" along "+along.ToString("0.000")+" facing off "+off.ToString("0")+" deg",p));
            }
            int shown=0;
            foreach(var f in found.OrderBy(f=>f.cost))
            {
                var l=IntroPoseChecks.Limits("fit",f.pose,1.2f,spec);if(l.Count>0)continue;
                Console.WriteLine(f.cost.ToString("0.0000")+"  "+f.what);if(++shown==10)break;
            }
        }

        /// <summary>`--intro-ranged-limits`: IntroPoseChecks' limits, feet and grip for the four ranged intros alone (what --intro-limits lists
        /// for them; quick while authoring). Returns the findings' count.</summary>
        public static int Limits(bool print=true)
        {
            var rig=ModelPoseChecks.Measured();int n=0;
            foreach(var id in Ids)
            {
                var e=NativeIntroCatalog.Find(id);var l=IntroPoseChecks.Limits(e).Select(f=>f.ToString()).ToList();
                var feet=IntroPoseChecks.Feet(e.Make(),e.Duration,rig);
                if(feet.Slide>IntroPoseChecks.SlideMost)l.Add(id+" "+feet.SlideAt.ToString("0.00")+" s the drawn "+feet.SlideFoot+" foot slides at "+feet.Slide.ToString("0.00")+" m/s");
                if(feet.Deepest>IntroPoseChecks.Through)l.Add(id+" "+feet.DeepestAt.ToString("0.00")+" s the drawn "+feet.DeepBone+" is "+(feet.Deepest*100).ToString("0")+" cm into the floor");
                var grip=IntroPoseChecks.Grip(e.Make(),e.Duration,e.Spec,rig);if(grip.Worst>IntroPoseChecks.GripNear)l.Add(id+" "+grip.WorstAt.ToString("0.00")+" s the drawn off fist is "+(grip.Worst*100).ToString("0")+" cm off the support point");
                if(print){Console.WriteLine("LIMITS "+id+": "+(l.Count==0?"none":l.Count+" finding(s)")+" (feet: slide at most "+feet.Slide.ToString("0.00")+" m/s, "+(feet.Deepest*100).ToString("0")+" cm in; grip: worst "+(grip.Worst*100).ToString("0.0")+" cm over "+grip.Frames+" frames)");foreach(var f in l)Console.WriteLine("  "+f);}
                n+=l.Count;
            }
            return n;
        }
        private static readonly string[] Ids={NativeIntroSundown.Id,NativeIntroOneBreath.Id,NativeIntroBrassStorm.Id,NativeIntroLightfall.Id};

        /// <summary>`--intro-ranged-twist ID`: per frame, how far each drawn arm segment's twist about its length is from the keyed one (the
        /// mapping check's measure, IntroPoseChecks.Mapping, which holds it within 60 degrees); scratch use while authoring.</summary>
        public static void Twist(string id)
        {
            var e=NativeIntroCatalog.Find(id);var s=e.Make();var rig=ModelPoseChecks.Measured();var pose=new CapturedRig.Pose();var st=new NativeVisualRig.State(rig);
            (string n,int j,int c)[] segs={("Rupper",CapturedRig.RArm,CapturedRig.RElbow),("Rfore",CapturedRig.RElbow,CapturedRig.RHand),("Lupper",CapturedRig.LArm,CapturedRig.LElbow),("Lfore",CapturedRig.LElbow,CapturedRig.LHand)};
            for(int i=0;i<=e.Duration*30;i++)
            {
                float t=s.Clock(i/30f);s.Pose(t,pose,out _,out _);pose.World();float relax=s is NativeIntroV2Script v2s?Math.Clamp(v2s.ArmsRelax(t),0,1):0;
                NativeVisualRig.Pose(st,pose,new NativeVisualRig.Grasp{Relax=relax,Exact=IntroPoseChecks.Packed(s,t,NativeIntroPack.Arms)},false,false);
                var line=new StringBuilder("t "+t.ToString("0.00"));
                foreach(var (n,j,c) in segs)
                {
                    int v=rig.Of[j],vc=rig.Of[c];var drawn=Quaternion.Normalize(st.World[v]*Quaternion.Inverse(rig.AlignedWorld[v]));var keyed=pose.WorldRotation[j];
                    var kd=pose.WorldPosition[c]-pose.WorldPosition[j];var dd=st.Position[vc]-st.Position[v];var swing=CapturedRig.FromTo(Vector3.Normalize(kd),Vector3.Normalize(dd));
                    var q=Quaternion.Normalize(Quaternion.Inverse(swing)*drawn*Quaternion.Inverse(keyed));var ax=Vector3.Normalize(kd);float a=2*(float)Math.Atan2(Vector3.Dot(new Vector3(q.X,q.Y,q.Z),ax),q.W)*Deg;while(a>180)a-=360;while(a<-180)a+=360;
                    line.Append("  "+n+" "+a.ToString("0"));
                }
                Console.WriteLine(line);
            }
        }
        // ---- the checks ----
        private static NativeIntroV2Script Make(string id)=>(NativeIntroV2Script)NativeIntroCatalog.Find(id).Make();
        /// <summary>Binds `s` up to `t` from a third of a second before (the binding's weights settle frame to frame, as in the game).</summary>
        private static void Settle(NativeIntroV2Script s,string spec,Drawn d,float t){for(float a=t-.33f;a<t;a+=1/30f)Bind(s,spec,d,a);Bind(s,spec,d,t);}
        private static float Pitch(Vector3 v)=>(float)(Math.Asin(Math.Clamp(Vector3.Normalize(v).Y,-1,1))*Deg);
        public static void Run()
        {
            count=0;var rig=ModelPoseChecks.Measured();string F2(float v)=>v.ToString("0.00");
            foreach(var id in Ids)Check(NativeIntroCatalog.Find(id)!=null,id+" is registered");
            int findings=Limits(false);Check(findings==0,findings+" finding(s) in the ranged intros' limits, feet or grips (--intro-ranged-limits lists them)");
            // Sundown: the shot leaves the drawn muzzle along the drawn bore onto the bell's strike, from where the camera's bullet ride starts
            {
                var s=Make(NativeIntroSundown.Id);var d=new Drawn(rig);Settle(s,"pistol",d,NativeIntroSundown.ShotAt);var m=d.Muzzle(out var bore);
                float off=Angle(bore,NativeIntroSundown.AimTarget(NativeIntroSundown.ShotAt)-m);Check(off<2.5f,"Sundown: the drawn bore on the bell's strike at the shot ("+F2(off)+" deg off)");
                float from=Vector3.Distance(m,NativeIntroSundown.ShotFrom);Check(from<.06f,"Sundown: the drawn muzzle where the shot's line starts ("+F2(from)+" m off)");
            }
            // One Breath: the stock in the drawn shoulder, the cheek on it and the eyes on the scope, the bore on the drone; the low carry at the end
            {
                var s=Make(NativeIntroOneBreath.Id);var d=new Drawn(rig);
                for(float t=1.0f;t<=NativeIntroOneBreath.ShotAt+.3f;t+=1/30f)
                {
                    Bind(s,"rifle",d,t);Check(d.State.Shouldered>.95f,"One Breath: the stock in the drawn shoulder while aiming (t "+F2(t)+": "+F2(d.State.Shouldered)+")");
                    Cheek(d,out float lat,out float up,out float along,out float face);
                    Check(lat>-.02f&&lat<.14f&&up>.15f&&up<.3f&&along>-.42f&&along<-.2f&&face<16,"One Breath: the cheek on the stock, the visor at the scope (t "+F2(t)+": lateral "+F2(lat)+", up "+F2(up)+", along "+F2(along)+", facing "+face.ToString("0")+" deg off the bore)");
                }
                Settle(s,"rifle",d,NativeIntroOneBreath.ShotAt);var m=d.Muzzle(out var bore);float off=Angle(bore,NativeIntroOneBreath.Drone-m);
                Check(off<1.5f,"One Breath: the drawn bore on the drone at the shot ("+F2(off)+" deg off)");
                d.Weapon(false,out var grip,out _);float camera=Vector3.Distance(grip,NativeIntroOneBreath.AimWrist);Check(camera<.04f,"One Breath: the drawn rifle where the camera's lens and shot frames expect it ("+F2(camera)+" m off)");
                d=new Drawn(rig);Settle(s,"rifle",d,NativeIntroOneBreath.Total-.05f);d.Muzzle(out bore);
                Check(Pitch(bore)<-30&&d.State.Shouldered<.05f,"One Breath: the rifle rests low at the name (bore "+Pitch(bore).ToString("0")+" deg, shouldered "+F2(d.State.Shouldered)+")");
            }
            // Brass Storm: every round leaves the drawn bore onto its strike, the stock in the shoulder and the head on the gun through the burst; low at the end
            {
                var s=Make(NativeIntroBrassStorm.Id);var d=new Drawn(rig);int round=0;float worst=0,worstFace=0;
                for(float t=NativeIntroBrassStorm.FireFrom-.3f;t<=NativeIntroBrassStorm.FireTo;t+=1/120f)
                {
                    Bind(s,"machinegun",d,t);if(t<NativeIntroBrassStorm.FireFrom)continue;
                    Check(d.State.Shouldered>.95f,"Brass Storm: the stock in the drawn shoulder through the burst (t "+F2(t)+": "+F2(d.State.Shouldered)+")");
                    Cheek(d,out _,out _,out _,out float face);worstFace=Math.Max(worstFace,face);
                    while(round<NativeIntroBrassStorm.Rounds&&NativeIntroBrassStorm.Fired(round)<=t)
                    {
                        var m=d.Muzzle(out var bore);float off=Angle(bore,NativeIntroBrassStorm.Strike(round)-m);worst=Math.Max(worst,off);
                        Check(off<3,"Brass Storm: round "+round+" leaves the drawn bore onto its strike ("+F2(off)+" deg off)");round++;
                    }
                }
                Check(round==NativeIntroBrassStorm.Rounds,"Brass Storm: every round checked ("+round+")");Check(worstFace<22,"Brass Storm: the head on the gun through the burst (worst "+worstFace.ToString("0")+" deg off the bore)");
                d=new Drawn(rig);Settle(s,"machinegun",d,NativeIntroBrassStorm.Total-.05f);d.Muzzle(out var low);
                Check(Pitch(low)<-30&&d.State.Shouldered<.05f,"Brass Storm: the gun rests low at the name (bore "+Pitch(low).ToString("0")+" deg, shouldered "+F2(d.State.Shouldered)+")");
                Console.WriteLine("  Brass Storm: "+round+" rounds, the drawn bore at most "+F2(worst)+" deg off its strike, the head at most "+worstFace.ToString("0")+" deg off the bore");
            }
            // Lightfall: at full draw the bow arm straight and the string hand at the drawn jaw, the string on the arrow's nock
            {
                var s=Make(NativeIntroLightfall.Id);var d=new Drawn(rig);
                for(float t=NativeIntroLightfall.Full;t<NativeIntroLightfall.Release-.02f;t+=1/30f)
                {
                    Bind(s,"bow",d,t);var palm=d.Bone("middle_01_r");var eyes=d.Eyes(out _);float bend=180-Angle(d.Bone("upperarm_l")-d.Bone("lowerarm_l"),d.Bone("hand_l")-d.Bone("lowerarm_l"));
                    float near=Vector3.Distance(palm,eyes),below=eyes.Y-palm.Y;
                    Check(bend<25,"Lightfall: the bow arm straight at full draw (t "+F2(t)+": the elbow bent "+bend.ToString("0")+" deg)");
                    Check(near<.25f&&below>-.02f&&below<.15f,"Lightfall: the string hand at the drawn jaw at full draw (t "+F2(t)+": "+F2(near)+" m from the eyes, "+F2(below)+" m under them)");
                }
            }
            Console.WriteLine("PASS ranged intros: "+count+" checks (four registered; limits, feet and grips clean; the shots on their marks from the drawn weapons; the long guns shouldered with the cheek on the stock and resting low at the name; the bow's straight arm and jaw anchor)");
        }
    }
}
