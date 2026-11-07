using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// The gun and magic travel carry from the owner's pack (owner 2026-10-12: "Give both ranged and magic different run/sprint
/// animations, theirs are currently too basic"; Core/NativeRangedLocomotion, NativePackRangedLocomotion.Generated, the
/// per-weapon sprint carries in Core/NativeSprintMotion and the first-person run bob in Core/NativeViewSway): the exported
/// loops match tools/pack-ranged-locomotion-provenance.json; for the pistol, rifle, machine gun, the four schools and L33t H4x
/// at a walk, a run in eight directions and a sprint the contact foot never slips (its ankle held still on the floor, where
/// today's gait let a planted ankle out of reach skate); a raised gun's muzzle stays on the aim and the aimed torso's mean yaw and pitch stay today's; the
/// torso stays steady (MeleeFeelChecks' run and walk bounds) and the hands far calmer than the gait's arm pump; each weapon
/// reads differently; no pop through a turn, a speed ramp, a shot, a reload, a cast, a roll, a jump and landing, a stun and
/// a sprint; the first-person run bob per weapon within the camera-shake setting; no allocation per frame; the hooks.
/// Presentation only.
/// </summary>
internal static class RangedLocomotionChecks
{
    private const float Dt=1/60f,Deg=180/(float)Math.PI,D=(float)Math.PI/180;
    private static readonly string[] Specs={"pistol","rifle","machinegun","fire","water","lightning","darkness","leet"};
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Ranged locomotion: "+what);}
    public static void Run(string root)
    {
        var log=new List<string>();Clips(root,log);Feet(log);Aim(log);Travel(log);Distinct(log);Transitions(log);View(log);Allocation(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS ranged locomotion: "+line);
    }
    private static void Clips(string root,List<string> log)
    {
        var names=NativePackRangedLocomotion.Names;var slots=NativePackRangedLocomotion.Slots;
        Check(string.Join(",",names)=="pistol,shooting,staff"&&NativeRangedLocoRules.SetFor("pistol")==NativeRangedLocoRules.Pistol&&NativeRangedLocoRules.SetFor("rifle")==NativeRangedLocoRules.Shooting&&NativeRangedLocoRules.SetFor("machinegun")==NativeRangedLocoRules.Shooting
            &&NativeRangedLocoRules.SetFor("leet")==NativeRangedLocoRules.Staff&&NativeRangedLocoRules.SetFor("darkness")==NativeRangedLocoRules.Staff&&NativeRangedLocoRules.SetFor("sword")==NativeRangedLocoRules.None,"three carries: the pistol, the long guns, the staff (the four schools and L33t H4x)");
        Check(string.Join(",",slots)==string.Join(",",NativePackLocomotion.Slots),"the melee carry's slots: slow, then walk and run in eight directions counter-clockwise from ahead");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-ranged-locomotion-provenance.json")));
        var prov=new Dictionary<string,JsonElement>();foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())prov[c.GetProperty("clip").GetString()]=c;
        Check(prov.Count==names.Length*slots.Length+1,"the provenance lists every loop and the sprint");int frames=0;
        void One(CapturedClip c,string key)
        {
            Check(c!=null&&c.Name==key&&c.Loop,key+" in place, looping");var p=prov[key];
            Check(p.GetProperty("frames").GetInt32()==c.Frames&&p.GetProperty("fps").GetInt32()==c.Fps&&p.GetProperty("source").GetString()==c.Source,key+": size and source match the provenance");
            Check(Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(c.Data))).ToLowerInvariant()==p.GetProperty("sha256").GetString(),key+": track is the exporter's output");
            Check(c.Marks.Length==1&&c.Marks[0]>=0&&c.Marks[0]<=c.Duration+1e-4f,key+": its left-foot strike lies in the cycle");
            foreach(var v in c.Decode())Check(MathUtil.Finite(v)&&Math.Abs(v)<4,key+": finite, bounded channels");frames+=c.Frames;
        }
        for(int s=0;s<names.Length;s++)for(int k=0;k<slots.Length;k++){One(NativePackRangedLocomotion.Sets[s][k],names[s]+"/"+slots[k]);Check(NativeRangedLocoRules.Clip(s,k)==NativePackRangedLocomotion.Sets[s][k],"clip lookup");}
        One(NativePackRangedLocomotion.Sprint,"sprint");
        var sets=NativePackRangedLocomotion.Sets;
        Check(sets[0][NativeLocoRules.Run].Source=="Unarmed-Run-Forward"&&sets[0][NativeLocoRules.Walk].Source=="Armed-Walk"&&sets[1][NativeLocoRules.Run+2].Source=="2Hand-Shooting-Run-Left"&&sets[1][NativeLocoRules.Walk+4].Source=="2Hand-Shooting-Strafe-Backward"
            &&sets[2][NativeLocoRules.Run+2].Source=="2Hand-Staff-Run-Right (mirrored)"&&sets[2][NativeLocoRules.Walk].Source=="2Hand-Staff-Walk-WithStaff (mirrored)"&&NativePackRangedLocomotion.Sprint.Source=="Unarmed-Sprint","the pistol walks the Armed walk and runs the Unarmed run; the long guns the 2Hand-Shooting set; the mirrored staff swaps its left and right clips; the Unarmed sprint");
        log.Add(names.Length*slots.Length+1+" loops ("+frames+" frames: per carry Walk-Slow, Walk and seven strafes, the run in eight directions, and the Unarmed sprint) match the exporter's provenance");
    }
    // ---- one pilot as CapturedAnimation.Player's GunGaitPose / MagicGaitPose drive it on an arena rig ----
    private sealed class Pilot
    {
        public readonly Actor A;public readonly bool Gun;public readonly GunMotion G;public readonly MagicMotion M;public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();
        private readonly NativeJuggleTrack juggle=new NativeJuggleTrack();private readonly float[] channels=new float[13];public Vector3 Root;
        public Pilot(string spec,bool pack,int id=41)
        {
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
            int mv=spec=="fire"?1:spec=="water"?2:spec=="lightning"?3:spec=="darkness"?4:0;
            A=new Actor{Id=id,SourceSpec=spec,Weapon=w,MagicVisual=mv,Grounded=true,Hp=100,CastRate=1,RollDirection=new V3(0,0,1),Ammo=w==WeaponId.Sword?0:GunRules.Get(w).Magazine};
            Gun=GunRules.Ranged(w);var loco=pack?new NativeRangedLocomotion():null;
            if(Gun)G=new GunMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=loco};else M=new MagicMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=loco};
        }
        public MeleeRig Rig=>Gun?G.Rig:M.Rig;
        public void Step(float t,float dt)
        {
            Root+=new Vector3(A.Velocity.x,A.Velocity.y,A.Velocity.z)*dt;if(Root.Y<0){Root.Y=0;}
            bool roll=A.Rolling(t);float yaw=roll?(float)Math.Atan2(A.RollDirection.x,A.RollDirection.z)/D:A.Yaw;
            Rig.GroupPosition=new Vector3(-Root.X,.865f+Root.Y,Root.Z);Rig.GroupYaw=-yaw*D;
            if(Gun){G.Pose(A,t,Pose,false,juggle,out _);G.Finish(Pose,t);}else{M.Pose(A,t,Pose,channels,0,false,juggle,out _);M.Finish(Pose,t);}
        }
        public bool Planted(bool left)=>Gun?G.Planted(left):M.Planted(left);
        public Quaternion Group=>MeleeMath.AxisAngle(MeleeMath.Y,Rig.GroupYaw);
        public Vector3 World(int bone)=>Rig.GroupPosition+Vector3.Transform(Pose.WorldPosition[bone],Group);
        public Quaternion WorldQ(int bone)=>Group*Pose.WorldRotation[bone];
        public Vector3 InChest(int bone)=>Vector3.Transform(Pose.WorldPosition[bone]-Pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(Pose.WorldRotation[CapturedRig.Chest]));
    }
    private static V3 Velocity(float speed,float dir)=>new V3(-(float)Math.Sin(dir)*speed,0,(float)Math.Cos(dir)*speed);// dir + to the rig's left (source frame), Unity x mirrored
    private sealed class Range{public float Min=float.MaxValue,Max=float.MinValue,Sum;public int N;public void Add(float v){Min=Math.Min(Min,v);Max=Math.Max(Max,v);Sum+=v;N++;}public float PP=>Max-Min;public float Mean=>N>0?Sum/N:0;}
    private static Vector3 YawPitchRoll(Quaternion q)
    {Vector3 f=Vector3.Transform(Vector3.UnitZ,q),r=Vector3.Transform(Vector3.UnitX,q);return new Vector3((float)Math.Atan2(f.X,f.Z)*Deg,(float)Math.Asin(Math.Clamp(-f.Y,-1,1))*Deg,(float)Math.Asin(Math.Clamp(r.Y,-1,1))*Deg);}
    private static readonly (string Name,float Speed,float Dir,bool Sprint)[] Cases=
    {
        ("walk",3,0,false),("walk strafe",3,(float)Math.PI/2,false),("walk backpedal",2.5f,(float)Math.PI,false),
        ("run",6.8f,0,false),("run forward-left",6.8f,(float)Math.PI/4,false),("strafe left",6.8f,(float)Math.PI/2,false),("run back-left",6.8f,3*(float)Math.PI/4,false),
        ("backpedal",6.8f,(float)Math.PI,false),("run back-right",6.8f,-3*(float)Math.PI/4,false),("strafe right",6.8f,-(float)Math.PI/2,false),("run forward-right",6.8f,-(float)Math.PI/4,false),
        ("sprint",9.18f,0,true),
    };
    private struct Measured{public float Slip,Yaw,Roll,Nod,Sway,Accel,ChestYaw,ChestPitch,Spike;public Vector3 Right,Left;}
    // A steady case: the contact foot's slip (its ankle's world speed across the ground while the gait holds it planted), the
    // torso's MeleeFeelChecks measures, the chest's pop, the hands' mean chest-space carry and acceleration.
    private static Measured Measure(string spec,float speed,float dir,bool sprint,bool pack,bool aiming=false)
    {
        var p=new Pilot(spec,pack);p.A.Velocity=Velocity(speed,dir);p.A.Sprinting=sprint;p.A.Aiming=aiming;
        var yaw=new Range();var roll=new Range();var nod=new Range();var sway=new Range();var cy=new Range();var cp=new Range();
        float slip=0,spike=0;double accel=0;int n=0;Vector3 right=Vector3.Zero,left=Vector3.Zero;var prev=new Vector3[2];var had=new bool[2];Vector3? r1=null,r2=null,l1=null,l2=null;Vector3? c1=null,c2=null;float s1=-1;
        for(int f=0;f<300;f++)
        {
            float t=1+f*Dt;p.Step(t,Dt);if(f<90)continue;
            for(int i=0;i<2;i++)
            {
                bool lf=i==0,on=p.Planted(lf);Vector3 at=p.World(lf?CapturedRig.LFoot:CapturedRig.RFoot);
                if(on&&had[i]){Vector3 d=at-prev[i];d.Y=0;slip=Math.Max(slip,d.Length()/Dt);}
                had[i]=on;prev[i]=at;
            }
            // The chest's pop (a step's excess over its neighbours'), the hips' seat included.
            Vector3 cw=p.Pose.WorldPosition[CapturedRig.Chest];if(c1.HasValue){float st=Vector3.Distance(cw,c1.Value);if(s1>=0&&c2.HasValue){float before=Vector3.Distance(c1.Value,c2.Value);spike=Math.Max(spike,s1-(before+st)*.5f);}s1=st;}c2=c1;c1=cw;
            var c=YawPitchRoll(p.Pose.WorldRotation[CapturedRig.Chest]);yaw.Add(c.X);nod.Add(c.Y);roll.Add(c.Z);sway.Add(p.Pose.WorldPosition[CapturedRig.Head].X*100);cy.Add(c.X);cp.Add(c.Y);
            Vector3 rh=p.InChest(CapturedRig.RHand),lh=p.InChest(CapturedRig.LHand);right+=rh;left+=lh;
            if(r2.HasValue){accel+=((rh-2*r1.Value+r2.Value)/(Dt*Dt)).Length()+((lh-2*l1.Value+l2.Value)/(Dt*Dt)).Length();n++;}r2=r1;r1=rh;l2=l1;l1=lh;
        }
        return new Measured{Slip=slip,Spike=spike,Yaw=yaw.PP,Roll=roll.PP,Nod=nod.PP,Sway=sway.PP,Accel=(float)(accel/Math.Max(1,n)),Right=right/210,Left=left/210,ChestYaw=cy.Mean,ChestPitch=cp.Mean};
    }
    // The steady cases' measures (with the pack carry, and today's), shared by Feet and Travel.
    private static readonly Dictionary<string,(Measured Now,Measured Old)> measured=new Dictionary<string,(Measured,Measured)>();
    private static void Feet(List<string> log)
    {
        float worst=0,worstBase=0;string at="",baseAt="";int n=0;
        foreach(var spec in Specs)foreach(var c in Cases)
        {
            var m=Measure(spec,c.Speed,c.Dir,c.Sprint,true);var b=Measure(spec,c.Speed,c.Dir,c.Sprint,false);measured[spec+" "+c.Name]=(m,b);
            Check(m.Slip<.03f,spec+" "+c.Name+": the contact foot slips "+(m.Slip*100).ToString("0.00")+" cm/s (under 3 cm/s)");
            if(m.Slip>worst){worst=m.Slip;at=spec+" "+c.Name;}if(b.Slip>worstBase){worstBase=b.Slip;baseAt=spec+" "+c.Name;}n++;
        }
        log.Add(n+" cases (8 weapons x walk, walk strafe, walk backpedal, the run in eight directions and the sprint): the contact foot slips at most "+(worst*100).ToString("0.000")+" cm/s ("+at+"); today's gait slipped up to "+(worstBase*100).ToString("0.0")+" cm/s ("+baseAt+": the planted ankle out of the leg's reach, now seated)");
    }
    private static void Aim(List<string> log)
    {
        float worst=0,worstBase=0,torso=0;string at="";int n=0;
        foreach(var spec in new[]{"pistol","rifle","machinegun"})
            foreach(var c in Cases)
            {
                if(c.Sprint)continue;bool aiming=c.Dir==0||Math.Abs(c.Dir)<1;// ahead and the forward diagonals raise the gun by aiming; the rest raise it themselves
                var p=new Pilot(spec,true);var b=new Pilot(spec,false,43);foreach(var x in new[]{p,b}){x.A.Velocity=Velocity(c.Speed,c.Dir);x.A.Aiming=aiming;x.A.Pitch=-12;}
                var pm=new Range();var bm=new Range();var py=new Range();var by=new Range();
                for(int f=0;f<240;f++)
                {
                    float t=1+f*Dt;p.Step(t,Dt);b.Step(t,Dt);if(f<90)continue;
                    V3 want=GunRules.Direction(p.A.AimYaw,p.A.Pitch);var w=new Vector3(want.x,want.y,want.z);
                    float Off(Pilot x){Vector3 barrel=CapturedRig.Mirror(Vector3.Transform(Vector3.UnitZ,x.WorldQ(CapturedRig.RHand)));return (float)Math.Acos(MeleeMath.Clamp(Vector3.Dot(Vector3.Normalize(barrel),w),-1,1))*Deg;}
                    float o=Off(p),ob=Off(b);worstBase=Math.Max(worstBase,ob);if(o>worst){worst=o;at=spec+" "+c.Name;}
                    var cp=YawPitchRoll(p.Pose.WorldRotation[CapturedRig.Chest]);var cb=YawPitchRoll(b.Pose.WorldRotation[CapturedRig.Chest]);pm.Add(cp.Y);bm.Add(cb.Y);py.Add(cp.X);by.Add(cb.X);
                }
                Check(p.G.AimWeight>.98f,spec+" "+c.Name+": the gun is up");
                float dy=Math.Abs(py.Mean-by.Mean),dp=Math.Abs(pm.Mean-bm.Mean);torso=Math.Max(torso,Math.Max(dy,dp));
                Check(dy<1&&dp<1,spec+" "+c.Name+": the aimed torso's mean yaw and pitch stay today's ("+dy.ToString("0.00")+", "+dp.ToString("0.00")+" deg)");n++;
            }
        Check(worst<1,"the muzzle stays within 1 deg of the aim while running ("+worst.ToString("0.000")+" deg at "+at+")");
        log.Add(n+" aimed runs (3 guns x walk, walk strafe, walk backpedal and the run in eight directions, aiming ahead): the muzzle within "+worst.ToString("0.000")+" deg of the aim (today "+worstBase.ToString("0.000")+"), the aimed chest's mean yaw and pitch within "+torso.ToString("0.00")+" deg of today's");
    }
    private static void Travel(List<string> log)
    {
        float hand=0,pop=0;string summary="";int n=0;
        foreach(var spec in Specs)foreach(var c in Cases)
        {
            var (m,b)=measured[spec+" "+c.Name];string at=spec+" "+c.Name;bool walk=c.Speed<4;
            string g=": chest twist "+m.Yaw.ToString("0.0")+", roll "+m.Roll.ToString("0.0")+", nod "+m.Nod.ToString("0.0")+" deg, head sway "+m.Sway.ToString("0.0")+" cm";
            // The chest steps as smoothly as the gait's own stride (its bob's footfall kink is up to 7 mm today), the seat included.
            Check(m.Spike<=Math.Max(.006f,b.Spike+.002f),at+": the chest steps smoothly (at most "+(m.Spike*1000).ToString("0.0")+" mm a frame over its neighbours; today "+(b.Spike*1000).ToString("0.0")+")");pop=Math.Max(pop,m.Spike);
            if(walk)Check(m.Yaw<=18&&m.Roll<=5&&m.Nod<=4.5f&&m.Sway<=6.5f,at+g+" (walk bounds)");
            else Check(m.Yaw<=15&&m.Roll<=10&&m.Nod<=7.5f&&m.Sway<=6,at+g+" (run bounds)");
            if(c.Name=="run"||c.Name=="sprint")summary+=(summary.Length>0?"; ":"")+spec+" "+c.Name+" "+m.Yaw.ToString("0")+"/"+m.Roll.ToString("0")+"/"+m.Nod.ToString("0")+" deg";n++;
        }
        // The arms no longer flap: the pistol's and the staff's hands (the gait pumped them at its cadence) far calmer at a run.
        foreach(var spec in new[]{"pistol","fire","water","lightning","darkness","leet"})
        {
            var (m,b)=measured[spec+" run"];
            Check(m.Accel<=b.Accel*.45f,spec+" run: the hands far calmer than the gait's pump ("+m.Accel.ToString("0")+" vs "+b.Accel.ToString("0")+" m/s2)");hand=Math.Max(hand,m.Accel/b.Accel);
        }
        log.Add(n+" travel cases within MeleeFeelChecks' walk and run bounds (twist/roll/nod: "+summary+"), the chest stepping at most "+(pop*1000).ToString("0.0")+" mm a frame over its neighbours; the pistol and staff hands at most "+(hand*100).ToString("0")+"% of the gait pump's acceleration at a run");
    }
    private static void Distinct(List<string> log)
    {
        var run=new Dictionary<string,Measured>();var sprint=new Dictionary<string,Measured>();
        foreach(var spec in Specs){run[spec]=measured[spec+" run"].Now;sprint[spec]=measured[spec+" sprint"].Now;}
        float Gap(Measured a,Measured b)=>Vector3.Distance(a.Right,b.Right)+Vector3.Distance(a.Left,b.Left);
        // The guns run with distinct carries; every weapon and school sprints with its own.
        foreach(var x in new[]{("pistol","rifle"),("pistol","machinegun"),("rifle","machinegun")})Check(Gap(run[x.Item1],run[x.Item2])>.1f,x.Item1+" and "+x.Item2+" run with distinct carries");
        for(int i=0;i<Specs.Length;i++)for(int j=i+1;j<Specs.Length;j++)
        {
            var a=sprint[Specs[i]];var b=sprint[Specs[j]];
            Check(Gap(a,b)>.08f||Math.Abs(a.ChestPitch-b.ChestPitch)>4,Specs[i]+" and "+Specs[j]+" sprint distinctly ("+Gap(a,b).ToString("0.00")+" m, lean "+a.ChestPitch.ToString("0")+" vs "+b.ChestPitch.ToString("0")+" deg)");
        }
        // The postures: the rifle steadier than the pistol and the machine gun; fire and darkness lean in harder than water.
        Check(run["rifle"].Yaw<run["pistol"].Yaw&&run["rifle"].Yaw<run["machinegun"].Yaw,"the rifle's run torso is the steadiest of the guns");
        Check(sprint["fire"].ChestPitch>sprint["water"].ChestPitch+4&&sprint["darkness"].ChestPitch>sprint["water"].ChestPitch+6,"fire and darkness sprint leaning in harder than water's glide");
        log.Add("distinct carries: the three guns at a run, all 8 weapons and schools sprinting (sprint lean pistol "+sprint["pistol"].ChestPitch.ToString("0")+", rifle "+sprint["rifle"].ChestPitch.ToString("0")+", machine gun "+sprint["machinegun"].ChestPitch.ToString("0")+", fire "+sprint["fire"].ChestPitch.ToString("0")+", water "+sprint["water"].ChestPitch.ToString("0")+", darkness "+sprint["darkness"].ChestPitch.ToString("0")+" deg); the rifle the steadiest gun");
    }
    // Through a scripted transition: the hands' (chest space), head's and chest's largest step in a frame, and the largest
    // pop (m): a step's excess over the mean of the frames either side of it, the part its neighbours do not share, where it
    // is well over twice their mean.
    private static (float Peak,float Spike) Pops(string spec,bool pack,Action<Pilot,float> script,float from,float to)
    {
        var p=new Pilot(spec,pack);p.A.Velocity=Velocity(6.8f,0);int[] bones={CapturedRig.RHand,CapturedRig.LHand,CapturedRig.Head,CapturedRig.Chest};
        var steps=new List<float[]>();Vector3[] prev=null;
        for(int f=0;f<300;f++)
        {
            float t=1+f*Dt;script(p,t);p.Step(t,Dt);var now=new Vector3[bones.Length];for(int i=0;i<bones.Length;i++)now[i]=i<2?p.InChest(bones[i]):p.Pose.WorldPosition[bones[i]];
            if(prev!=null&&t>=from&&t<=to){var d=new float[bones.Length];for(int i=0;i<bones.Length;i++)d[i]=Vector3.Distance(now[i],prev[i]);steps.Add(d);}prev=now;
        }
        float peak=0,spike=0;
        for(int k=0;k<steps.Count;k++)for(int i=0;i<bones.Length;i++)
        {
            peak=Math.Max(peak,steps[k][i]);if(k==0||k==steps.Count-1)continue;
            // A pop: a step well over twice its neighbours' mean (a smooth ease, however fast, never is), by its excess.
            float mean=(steps[k-1][i]+steps[k+1][i])*.5f,ex=steps[k][i]>1.8f*mean+.002f?steps[k][i]-mean:0;spike=Math.Max(spike,ex);
        }
        return (peak,spike);
    }
    private static void Transitions(List<string> log)
    {
        var scripts=new (string Name,Action<Pilot,float> Do,bool Gun,bool Magic)[]
        {
            ("turn",(p,t)=>{float u=Math.Clamp((t-2)/2,0,1);p.A.Velocity=Velocity(6.8f,(float)Math.PI*2*u);},true,true),
            ("speed ramp and stop",(p,t)=>{float u=Math.Clamp((t-2)/1.5f,0,1);p.A.Velocity=Velocity(6.8f*(1-u)+(t>4?6.8f*Math.Clamp(t-4,0,1):0),0);},true,true),
            ("sprint on and off",(p,t)=>{p.A.Sprinting=t>2&&t<3.2f;p.A.Velocity=Velocity(p.A.Sprinting?9.18f:6.8f,0);},true,true),
            ("shot",(p,t)=>{if(t>=2.5f&&p.A.LastShot<2)p.A.LastShot=t;},true,false),
            ("reload",(p,t)=>{if(t>=2.5f&&p.A.ReloadAt<2){p.A.ReloadAt=t;p.A.ReloadUntil=t+GunRules.Get(p.A.Weapon).Reload;}},true,false),
            ("cast",(p,t)=>{if(t>=2.5f&&p.A.AttackAt<2){p.A.AttackAt=t;p.A.LastShot=t+MagicPose.Windup(MagicPose.School(p.A),1);}},false,true),
            ("roll",(p,t)=>{if(t>=2.5f&&p.A.RollAt<2)p.A.RollAt=t;},true,true),
            ("stun",(p,t)=>{if(t>=2.5f&&p.A.StunUntil<2)p.A.StunUntil=t+.45f;},true,true),
            ("jump and land",(p,t)=>{if(t>=2.5f&&t<2.5f+Dt*.5f){p.A.Grounded=false;p.A.Velocity=new V3(p.A.Velocity.x,8.5f,p.A.Velocity.z);}else if(!p.A.Grounded){var v=p.A.Velocity;v.y-=23*Dt;p.A.Velocity=v;if(p.Root.Y<=0&&v.y<0){p.A.Grounded=true;p.A.Velocity=new V3(v.x,0,v.z);}}},true,true),
        };
        float worst=0,worstSpike=0;string at="",spikeAt="";int n=0;var steadyOf=new Dictionary<string,(float Peak,float Spike)>();
        foreach(var spec in Specs)foreach(var s in scripts)
        {
            bool gun=spec=="pistol"||spec=="rifle"||spec=="machinegun";if(gun?!s.Gun:!s.Magic)continue;
            if(!steadyOf.TryGetValue(spec,out var steady))steadyOf[spec]=steady=Pops(spec,true,(p,t)=>{},2,4.5f);
            var now=Pops(spec,true,s.Do,2,4.5f);var old=Pops(spec,false,s.Do,2,4.5f);
            // No pop: no step its neighbours do not share beyond today's own (a reload key's kink, a hit stun's held frame: at
            // most 5 mm or 15% over it, or under 1 cm a frame; where today's snap is larger than 3 cm, no larger a step than
            // today's), and the largest step near today's (the run carries' raise to the aim travels a little further: +25%) or
            // the steady run's own.
            Check(now.Spike<=Math.Max(Math.Max(old.Spike*1.15f,old.Spike+.005f),.01f)||old.Spike>.03f&&now.Peak<=old.Peak*1.1f,spec+" "+s.Name+": no pop ("+(now.Spike*100).ToString("0.00")+" cm a frame over its neighbours; today "+(old.Spike*100).ToString("0.00")+")");
            Check(now.Peak<=Math.Max(old.Peak*1.25f,steady.Peak*1.6f)+.002f,spec+" "+s.Name+": the largest step "+(now.Peak*100).ToString("0.0")+" cm a frame (today "+(old.Peak*100).ToString("0.0")+", steady run "+(steady.Peak*100).ToString("0.0")+")");
            if(now.Peak>worst){worst=now.Peak;at=spec+" "+s.Name;}if(now.Spike>worstSpike){worstSpike=now.Spike;spikeAt=spec+" "+s.Name;}n++;
        }
        log.Add(n+" transitions (a full turn, a speed ramp and stop, sprint on and off, a shot, a reload, a cast, a roll, a stun, a jump and landing): no pop beyond today's own (the largest, "+(worstSpike*100).ToString("0.0")+" cm over its neighbours, is the "+spikeAt+"'s own sweep); the hands, head and chest move at most "+(worst*100).ToString("0.0")+" cm a frame ("+at+")");
    }
    private static void View(List<string> log)
    {
        // The first-person run bob: per weapon within the camera-shake setting (none off, half Reduced), melee unchanged.
        NativeViewSwayPose Bob(string spec,float shake,bool sprint,float at)
        {
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
            var a=new Actor{Id=5,SourceSpec=spec,Weapon=w,Hp=100,Grounded=true,Sprinting=sprint,Velocity=new V3(0,0,sprint?9.18f:6.8f),LastShot=-100,ReloadUntil=-100};
            var v=new NativeViewSway();NativeViewSwayPose p=default;for(float t=0;t<=at;t+=Dt)p=v.Sample(NativeViewSway.Input(a,t,0,0,0,shake));return p;
        }
        float Spread(string spec,float shake,bool sprint){float lo=float.MaxValue,hi=float.MinValue;for(int k=0;k<30;k++){float y=Bob(spec,shake,sprint,1.5f+k*Dt).Y;lo=Math.Min(lo,y);hi=Math.Max(hi,y);}return hi-lo;}
        var off=new Dictionary<string,float>();var on=new Dictionary<string,float>();
        foreach(var spec in new[]{"pistol","rifle","machinegun","fire","water","sword"}){off[spec]=Spread(spec,0,false);on[spec]=Spread(spec,1,false);}
        Check(Math.Abs(on["sword"]-off["sword"])<1e-6f,"a melee view keeps the source bob");
        Check(on["machinegun"]>off["machinegun"]*1.3f&&on["rifle"]<off["rifle"]*.8f&&on["water"]<off["water"]*.8f,"camera shake on: the machine gun bobs heavier, the rifle and water steadier ("+(on["machinegun"]*100).ToString("0.0")+", "+(on["rifle"]*100).ToString("0.0")+", "+(on["water"]*100).ToString("0.0")+" cm)");
        foreach(var spec in new[]{"pistol","rifle","machinegun","fire","water"}){float h=Spread(spec,.5f,false);Check(Math.Abs(h-(off[spec]+(on[spec]-off[spec])*.5f))<Math.Abs(on[spec]-off[spec])*.35f+1e-4f,spec+": Reduced effects halve the extra bob");}
        float so=Spread("pistol",1,true),sf=Spread("pistol",0,true);Check(so>sf*1.3f,"the sprint's stronger bob only with camera shake ("+(so*100).ToString("0.0")+" vs "+(sf*100).ToString("0.0")+" cm)");
        // The raw source inputs (no spec, no shake) are untouched.
        var raw=new NativeViewSway();var src=new NativeViewSway();
        for(int f=0;f<90;f++){var i=new NativeViewSwayInput{Pilot=1,Time=f*Dt,VelocityZ=6.8f,Grounded=true,Gait=true};var x=raw.Sample(i);var y=src.Sample(i);Check(x.X==y.X&&x.Y==y.Y&&x.Pitch==y.Pitch&&x.Roll==y.Roll,"raw inputs keep the source view layer");}
        log.Add("first person: the run bob per weapon within camera shake (machine gun "+(on["machinegun"]*100).ToString("0.0")+", pistol "+(on["pistol"]*100).ToString("0.0")+", rifle "+(on["rifle"]*100).ToString("0.0")+", water "+(on["water"]*100).ToString("0.0")+" cm vs the source's "+(off["rifle"]*100).ToString("0.0")+"; half with Reduced effects, none off; melee and raw inputs unchanged); the sprint bob only with camera shake");
    }
    private static void Allocation(List<string> log)
    {
        long used=0;
        foreach(var spec in new[]{"pistol","machinegun","water","leet","lightning"})
        {
            var p=new Pilot(spec,true);void Frame(int f){float t=1+f*Dt,d=f/60f;p.A.Sprinting=f%120<50;p.A.Velocity=Velocity(p.A.Sprinting?9.18f:f%120<90?6.8f:3,d);p.Step(t,Dt);}
            for(int f=0;f<240;f++)Frame(f);long before=GC.GetAllocatedBytesForCurrentThread();for(int f=240;f<600;f++)Frame(f);used+=GC.GetAllocatedBytesForCurrentThread()-before;
        }
        Check(used==0,"no allocation per frame ("+used+" bytes)");
        log.Add("no allocation per frame while turning, changing pace and sprinting (guns and staff)");
    }
    private static void Hooks(string root,List<string> log)
    {
        string Read(string f)=>File.ReadAllText(Path.Combine(root,f));
        string gm=Read("Assets/ChipArena/Core/GunMotion.cs"),mp=Read("Assets/ChipArena/Core/MagicPose.cs"),ca=Read("Assets/ChipArena/Runtime/CapturedAnimation.cs");
        Check(gm.Contains("Locomotion?.Gun(r,v,v.Spec,time,sprint);")&&gm.Contains("Locomotion?.Aim(r,v.Spec,k);")&&gm.Contains("Locomotion?.LongGun(a,lean,ref carryAt,ref carryAim);"),"GunMotion lays the pack carry under the leans, the stride swing under the aim, the long guns' run carry into the hold");
        Check(mp.Contains("float pack=Locomotion?.Magic(r,v,v.Spec,time,sprint)??0;")&&mp.Contains("(Locomotion?.StaffTilt??0)"),"MagicMotion lays the pack carry under the leans and tilts the staff with it");
        Check(ca.Contains("RangedLocomotion=>Hits!=null?rangedLocomotion??=new NativeRangedLocomotion():null;")&&ca.Contains("gunMotion.Locomotion=RangedLocomotion;")&&ca.Contains("magicMotion.Locomotion=RangedLocomotion;"),"the arena rigs (host, guests, killcam, spectators) take the pack carry; source parity and other rigs keep the gait");
        Check(Read("Assets/ChipArena/Runtime/NativeViewSwayPresentation.cs").Contains("ads,viewShake));")&&Read("Assets/ChipArena/Runtime/NativeSprintFxPresentation.cs").Contains("viewShake=shake?(reduced?.5f:1):0;"),"the first-person view takes the camera-shake setting");
        foreach(var f in new[]{"Assets/ChipArena/Core/ArenaWorld.cs","Assets/ChipArena/Core/NativePrediction.cs"})Check(!Read(f).Contains("NativeRangedLocomotion"),f+" (movement and prediction) is untouched by the presentation");
        string rp=Read("Assets/ChipArena/Core/RangedPose.cs");
        Check(rp.Contains("Vector3? carryTarget=null,Quaternion? carryAim=null")&&rp.Contains("target=Vector3.Lerp(carryTarget??CarryTarget,target,hold);"),"the gun hold takes the run carry only as its lowered carry (optional; null keeps the hold)");
        log.Add("hooks: gun and magic routes, arena rigs only, the first-person camera-shake setting; movement and prediction untouched, the gun hold's lowered carry the only hold hook");
    }
}
