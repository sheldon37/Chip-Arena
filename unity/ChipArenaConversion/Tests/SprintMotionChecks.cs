using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ChipArena.UnityConversion;

/// <summary>
/// The sprint animation and its effect (owner 2026-10-12: "Also, add a sprinting animation for all weapons. (Different on
/// weapon of course)", "And a sprint effect to know when we're sprinting", "Make sprint effect visible in first person too";
/// Core/NativeSprintMotion, NativeSprintFx, NativeViewSway, Runtime/NativeSprintFxPresentation): when a pilot shows its sprint
/// (the replicated flag, grounded, travelling, not busy); a .15 s blend with no pop; a distinct carry per weapon (melee, gun
/// and magic routes) with the gait's feet (nothing slides) and the steady torso within MeleeFeelChecks' bounds; swings,
/// blocks, rolls, stuns, shots, aims, reloads and casts drop it; the first-person carry, FOV kick and bob with their Settings
/// gates and the source view layer untouched for raw inputs; the effect's streaks and dust off the floor and upright, the
/// screen lines in the outer third, thinned by Reduced effects and Low graphics; no allocation per frame; the hooks.
/// </summary>
internal static class SprintMotionChecks
{
    private const float Dt=1/60f,Deg=180/(float)Math.PI,SprintSpeed=9.18f,RunSpeed=6.8f;
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Sprint motion: "+what);}
    public static void Run(string root)
    {
        var log=new List<string>();Rules(log);Carries(log);Blend(log);Priority(log);View(log);Fx(log);Allocation(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS sprint motion: "+line);
    }
    private static void Rules(List<string> log)
    {
        Check(NativeSprintRules.Shows(true,true,9)&&!NativeSprintRules.Shows(false,true,9)&&!NativeSprintRules.Shows(true,false,9)&&!NativeSprintRules.Shows(true,true,2),"the flag shows only grounded and travelling");
        var a=new Actor{Id=1,Hp=100,Grounded=true,Sprinting=true,Velocity=new V3(0,0,SprintSpeed)};Check(NativeSprintRules.Shows(a,5),"a sprinting pilot shows it");
        foreach(var busy in new Action<Actor>[]{x=>x.AttackAt=4.8f,x=>{x.AttackAt=4.5f;x.AttackUntil=5.5f;},x=>x.BlockUntil=6,x=>x.RollAt=4.9f,x=>x.StunUntil=6,x=>x.JuggleUntil=6,x=>x.LastShot=4.9f,x=>x.Aiming=true,x=>x.ReloadUntil=6,x=>x.AbilityAt=4.8f,x=>{x.CommitAt=4.9f;x.CommitUntil=5.4f;},x=>x.Hp=0})
        {var b=new Actor{Id=1,Hp=100,Grounded=true,Sprinting=true,Velocity=new V3(0,0,SprintSpeed)};busy(b);Check(NativeSprintRules.Busy(b,5)&&!NativeSprintRules.Shows(b,5),"an action drops the sprint");}
        var carries=new HashSet<string>();
        foreach(var s in new[]{"sword","daggers","shield","greatsword","pistol","rifle","machinegun","fire","water","lightning","darkness","leet"})carries.Add(NativeSprintRules.CarryOf(s));
        Check(carries.Count==12&&!carries.Contains(NativeSprintRules.None)&&!carries.Contains(NativeSprintRules.Trail),"twelve distinct carries (each school and L33t H4x its own)");
        Check(NativeSprintRules.CarryOf("emperor")==NativeSprintRules.None&&NativeSprintRules.CarryOf("ansatsuken")==NativeSprintRules.None&&NativeSprintRules.CarryOf("siren")==NativeSprintRules.None,"the DEV fighters and the Siren keep their own locomotion");
        // The blend: a ramp over .15 s eased at both ends (its first and last frames move least), never 17% in one 60 Hz frame.
        float w=0,step=0,first=0;for(int f=0;f<9;f++){float n=NativeSprintRules.Step(w,true,Dt);float d=NativeSprintRules.Shape(n)-NativeSprintRules.Shape(w);if(f==0)first=d;step=Math.Max(step,d);w=n;}
        w=NativeSprintRules.Shape(w);Check(w>.999f&&step<.17f&&first<step*.25f,"the blend reaches "+w.ToString("0.00")+" in .15 s, at most "+step.ToString("0.00")+" a frame, starting at rest ("+first.ToString("0.000")+")");
        float back=NativeSprintRules.Step(.5f,false,Dt);Check(back<.5f&&NativeSprintRules.Shape(back)<NativeSprintRules.Shape(.5f)&&NativeSprintRules.Step(1,true,1)==1&&NativeSprintRules.Step(0,false,1)==0,"a reversal ramps back from where it is");
        log.Add("the sprint shows from the replicated flag while grounded above "+NativeSprintRules.MinSpeed+" m/s; twelve actions drop it; twelve distinct carries, the DEV fighters and the Siren keep theirs; the blend reaches "+(w*100).ToString("0")+"% in .15 s");
    }
    // One route's pilot moving along +z at `speed` (the root travels, so planted feet are planted in the world).
    private sealed class Pilot
    {
        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly Actor A;public readonly MeleeView V=new MeleeView();public readonly string Route;
        public MeleeMotion Melee;public GunMotion Gun;public MagicMotion Magic;public readonly NativeSprintMotion Layer;private readonly NativeJuggleTrack juggle=new NativeJuggleTrack();
        private readonly float[] channels=new float[13];private float last=-100;public Vector3 Root=new Vector3(0,.865f,0);
        public Pilot(string spec,bool layer)
        {
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;int mv=spec=="fire"?1:spec=="water"?2:spec=="lightning"?3:spec=="darkness"?4:0;
            A=new Actor{Id=7,SourceSpec=spec,Weapon=w,MagicVisual=mv,Grounded=true,Hp=100,CastRate=1,Ammo=w==WeaponId.Sword?0:GunRules.Get(w).Magazine,VisualSet=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0};
            Layer=layer?new NativeSprintMotion():null;Route=GunRules.Ranged(w)?"gun":mv>0||spec=="leet"?"magic":"melee";
            if(Route=="melee"){Melee=new MeleeMotion{Impacts=new MeleeImpacts(),Locomotion=new NativeLocomotionCarry(),SprintLayer=Layer};V.Id="7";V.Spec=spec;}
            else if(Route=="gun")Gun=new GunMotion{SprintLayer=Layer,Locomotion=new NativeRangedLocomotion()};else Magic=new MagicMotion{SprintLayer=Layer,Locomotion=new NativeRangedLocomotion()};
        }
        public void Frame(float t,float speed,bool sprinting)
        {
            float dt=last<-90?Dt:t-last;last=t;var vel=new Vector3(0,0,speed);Root+=vel*dt;
            A.Velocity=new V3(0,0,speed);A.Sprinting=sprinting;A.Position=new V3(Root.X,Root.Y-.865f,Root.Z);
            if(Route=="gun"){Gun.Rig.GroupPosition=Root;Gun.Rig.GroupYaw=0;Gun.Pose(A,t,Pose,false,juggle,out _);Gun.Finish(Pose,t);return;}
            if(Route=="magic"){Magic.Rig.GroupPosition=Root;Magic.Rig.GroupYaw=0;Magic.Pose(A,t,Pose,channels,0,false,juggle,out _);Magic.Finish(Pose,t);return;}
            V.Vel=vel;V.Grounded=true;V.Sprinting=sprinting;V.Hp=A.Hp;Melee.Rig.GroupPosition=Root;Melee.Rig.GroupYaw=Melee.PresentedYaw(V,t,V.Yaw);
            Melee.Sample(V,t,dt,r=>{});Melee.Rig.WritePose(Pose);Pose.World();
        }
        public Vector3 InChest(int bone)=>Vector3.Transform(Pose.WorldPosition[bone]-Pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(Pose.WorldRotation[CapturedRig.Chest]));
        public Vector3 World(int bone)=>Root+Pose.WorldPosition[bone];
        public float Weight=>Layer?.Weight??0;
    }
    private sealed class Range{public float Min=float.MaxValue,Max=float.MinValue;public void Add(float v){Min=Math.Min(Min,v);Max=Math.Max(Max,v);}public float PP=>Max-Min;}
    private static Vector3 YawPitchRoll(Quaternion q)
    {Vector3 f=Vector3.Transform(Vector3.UnitZ,q),r=Vector3.Transform(Vector3.UnitX,q);return new Vector3((float)Math.Atan2(f.X,f.Z)*Deg,(float)Math.Asin(Math.Clamp(-f.Y,-1,1))*Deg,(float)Math.Asin(Math.Clamp(r.Y,-1,1))*Deg);}
    private struct Sprinted{public float Yaw,Roll,Nod,Sway,Feet,ZPump;public Vector3 Right,Left;}
    private static readonly int[] Feet={CapturedRig.LFoot,CapturedRig.RFoot,CapturedRig.LToe,CapturedRig.RToe};
    // A steady sprint: the torso's MeleeFeelChecks measures, the hands' mean carry in chest space, and the feet against the
    // same pilot without the layer (the gait's own planted feet).
    private static Sprinted Measure(string spec,bool layer)
    {
        var p=new Pilot(spec,layer);var twin=new Pilot(spec,false);var yaw=new Range();var roll=new Range();var nod=new Range();var sway=new Range();var pump=new Range();
        Vector3 right=Vector3.Zero,left=Vector3.Zero;float feet=0;int n=0;
        for(int f=0;f<270;f++)
        {
            float t=1+f*Dt;p.Frame(t,SprintSpeed,true);twin.Frame(t,SprintSpeed,true);if(f<90)continue;
            var c=YawPitchRoll(p.Pose.WorldRotation[CapturedRig.Chest]);yaw.Add(c.X);nod.Add(c.Y);roll.Add(c.Z);sway.Add(p.Pose.WorldPosition[CapturedRig.Head].X*100);
            right+=p.InChest(CapturedRig.RHand);left+=p.InChest(CapturedRig.LHand);pump.Add(p.InChest(CapturedRig.RHand).Z);n++;
            // The planted feet (the gun and magic gaits tell which; a crouched sprint's swing foot may lift a little less).
            foreach(int b in Feet){bool lf=b==CapturedRig.LFoot||b==CapturedRig.LToe;if(!(p.Gun?.Planted(lf)??p.Magic?.Planted(lf)??true))continue;feet=Math.Max(feet,Vector3.Distance(p.World(b),twin.World(b)));}
        }
        return new Sprinted{Yaw=yaw.PP,Roll=roll.PP,Nod=nod.PP,Sway=sway.PP,Feet=feet,Right=right/n,Left=left/n,ZPump=pump.PP};
    }
    private static readonly string[] Melee={"sword","daggers","shield","greatsword"},Ranged={"pistol","rifle","machinegun"},Magic={"fire","water","lightning","darkness","leet"};
    private static void Carries(List<string> log)
    {
        var now=new Dictionary<string,Sprinted>();var old=new Dictionary<string,Sprinted>();float worstFeet=0;string torso="";
        foreach(var spec in new[]{"sword","daggers","shield","greatsword","pistol","rifle","machinegun","fire","water","lightning","darkness","leet"})
        {
            var s=Measure(spec,true);var o=Measure(spec,false);now[spec]=s;old[spec]=o;worstFeet=Math.Max(worstFeet,s.Feet);
            string g=spec+": chest twist "+s.Yaw.ToString("0.0")+", roll "+s.Roll.ToString("0.0")+", nod "+s.Nod.ToString("0.0")+" deg, head sway "+s.Sway.ToString("0.0")+" cm";
            // The owner's sword/daggers targets; the run bounds elsewhere (the greatsword's nod sat at 6.1 before the sprint). The
            // gun and magic sprints (owner 2026-10-12: a heavy lumbering machine gun, a low stalking darkness) keep MeleeFeelChecks'
            // run bounds.
            if(spec=="sword"||spec=="daggers")Check(s.Yaw<=15&&s.Roll<=6&&s.Nod<=6&&s.Sway<=3,g+" (the owner's targets)");
            else if(Array.IndexOf(Melee,spec)>=0)Check(s.Yaw<=15&&s.Roll<=6&&s.Nod<=7.5f&&s.Sway<=3,g+" (sprint bounds)");
            else Check(s.Yaw<=15&&s.Roll<=8&&s.Nod<=7.5f&&s.Sway<=6,g+" (gun and magic sprint bounds)");
            Check(s.Feet<.005f,spec+": the feet stay the gait's planted feet ("+(s.Feet*1000).ToString("0.0")+" mm)");
            Check(Vector3.Distance(s.Right,o.Right)>.08f||Vector3.Distance(s.Left,o.Left)>.08f,spec+": the sprint changes the carry ("+Vector3.Distance(s.Right,o.Right).ToString("0.00")+" m)");
            torso+=(torso.Length>0?"; ":"")+spec+" "+o.Sway.ToString("0.0")+"->"+s.Sway.ToString("0.0")+" cm";
        }
        void Distinct(string[] group){for(int i=0;i<group.Length;i++)for(int j=i+1;j<group.Length;j++){var a=now[group[i]];var b=now[group[j]];Check(Vector3.Distance(a.Right,b.Right)+Vector3.Distance(a.Left,b.Left)>.1f,group[i]+" and "+group[j]+" sprint with distinct carries");}}
        Distinct(Melee);Distinct(Ranged);
        // The shape of each carry in chest space (x the rig's left, y up, z forward).
        Check(now["sword"].Right.Z<-.05f&&now["sword"].Right.Y<-.2f,"sword: the blade hand low behind the hip ("+now["sword"].Right+")");
        // The daggers' fists are held low and close with a light pump (owner 2026-10-14: "keep the arms more still"; it was a
        // 60 cm pump; Tests/SprintArmChecks measures the arms).
        Check(now["daggers"].ZPump>=.08f&&now["daggers"].ZPump<=.2f&&now["daggers"].Right.Y<-.05f,"daggers: the fists held low with a light pump ("+now["daggers"].ZPump.ToString("0.00")+" m, "+now["daggers"].Right+")");
        Check(now["shield"].Right.Z>.2f&&now["shield"].Right.Y>-.1f,"shield: up in front of the chest ("+now["shield"].Right+")");
        Check(now["greatsword"].Right.Y>0&&Vector3.Distance(now["greatsword"].Right,now["greatsword"].Left)<.25f,"greatsword: both hands up at the shoulder ("+now["greatsword"].Right+")");
        Check(now["pistol"].ZPump>.3f&&now["pistol"].Right.Y<-.05f,"pistol: both fists pump hard, the gun hand low ("+now["pistol"].ZPump.ToString("0.00")+" m, "+now["pistol"].Right+")");
        Check(now["machinegun"].Right.Y>now["rifle"].Right.Y&&now["machinegun"].Right.X<now["rifle"].Right.X-.05f,"the machine gun heaved onto the shoulder, higher and further out than the rifle's high port ("+now["machinegun"].Right+" vs "+now["rifle"].Right+")");
        Check(now["fire"].Right.Y<-.2f&&now["fire"].ZPump>.2f&&now["fire"].Right.Z<0,"fire: both hands low, clawing hard past the hips ("+now["fire"].Right+", "+now["fire"].ZPump.ToString("0.00")+" m)");
        Check(Math.Abs(now["water"].Right.X)>.3f&&Math.Abs(now["water"].Left.X)>.3f,"water: the hands out wide to the sides ("+now["water"].Right+")");
        Check(Math.Abs(now["lightning"].Right.X)<.3f&&now["lightning"].Right.Z>-.05f&&now["lightning"].Right.Y>-.2f,"lightning: the hands tight in front ("+now["lightning"].Right+")");
        Check(now["darkness"].Right.Y<-.2f&&now["darkness"].Right.Z<-.2f&&now["darkness"].Right.Y<now["water"].Right.Y,"darkness: the hands back and low ("+now["darkness"].Right+")");
        Check(now["leet"].Right.Z<-.3f&&now["leet"].Right.Y>now["fire"].Right.Y+.1f,"L33t H4x: the arms flung straight back, higher than fire's ("+now["leet"].Right+")");
        Distinct(Magic);
        log.Add("12 weapons sprinting at "+SprintSpeed+" m/s: distinct carries (blade low behind, daggers held low with a light pump, shield up, greatsword on the shoulder, the pistol tucked with both fists pumping, high port, the machine gun on the shoulder, fire's hands clawing low past the hips, water's out wide, lightning's tight in front, darkness back and low, L33t H4x's flung back), the feet exactly the gait's (worst "+(worstFeet*1000).ToString("0.0")+" mm), the steady torso within bounds (head sway "+torso+")");
    }
    private static void Blend(List<string> log)
    {
        float worst=0,steady=0;
        foreach(var spec in new[]{"sword","greatsword","rifle","fire"})
        {
            // Run, sprint at 1.5 s, stop sprinting at 2.5 s; the hands' chest-space step per frame through the blends against a
            // steady sprint's.
            var p=new Pilot(spec,true);Vector3? r0=null,l0=null;float inAt=-1,outAt=-1,blend=0,run=0,sum=0;int n=0;
            for(int f=0;f<240;f++)
            {
                float t=1+f*Dt;bool on=t>=1.5f&&t<2.5f;p.Frame(t,on?SprintSpeed:RunSpeed,on);
                Vector3 r=p.InChest(CapturedRig.RHand),l=p.InChest(CapturedRig.LHand);
                if(r0.HasValue){float s=Math.Max((r-r0.Value).Length(),(l-l0.Value).Length());if(t>=1.5f&&t<1.66f||t>=2.5f&&t<2.66f){blend=Math.Max(blend,s);sum+=s;n++;}else if(t>2.1f&&t<2.5f)run=Math.Max(run,s);}
                r0=r;l0=l;
                if(on&&inAt<0&&p.Weight>.9f)inAt=t-1.5f;if(!on&&t>2.5f&&outAt<0&&p.Weight<.05f)outAt=t-2.5f;
            }
            Check(inAt>0&&inAt<=.16f&&outAt>0&&outAt<=.16f,spec+": the sprint blends in ("+inAt.ToString("0.000")+" s) and out ("+outAt.ToString("0.000")+" s) in about .15 s");
            // No pop: the hands' largest step through a blend stays near its mean (an eased move), or under a steady sprint's.
            float mean=sum/Math.Max(1,n);
            Check(blend<=Math.Max(mean*1.8f,run*1.2f),spec+": no pop through the blend ("+(blend*100).ToString("0.0")+" cm a frame at most, "+(mean*100).ToString("0.0")+" on average; steady sprint "+(run*100).ToString("0.0")+")");
            worst=Math.Max(worst,blend);steady=Math.Max(steady,run);
        }
        log.Add("the sprint blends in and out within .15 s (sword, greatsword, rifle, magic) with the hands moving at most "+(worst*100).ToString("0.0")+" cm a frame (steady sprint "+(steady*100).ToString("0.0")+")");
    }
    private static void Priority(List<string> log)
    {
        int cases=0;
        void Drop(string spec,string what,Action<Pilot,float> start)
        {
            var p=new Pilot(spec,true);float at=-1;bool none=false;
            for(int f=0;f<120;f++)
            {
                float t=1+f*Dt;if(t>=1.8f)start(p,1.8f);p.Frame(t,SprintSpeed,true);if(t<1.79f&&t>1.6f)Check(p.Weight>.9f,spec+": sprinting before the "+what);
                if(t>=1.8f&&at<0&&p.Weight<.05f)at=t-1.8f;if(at>=0&&p.Weight==0)none|=p.Layer.Carry==NativeSprintRules.None;
            }
            Check(at>=0&&at<=.16f&&none,spec+": a "+what+" drops the sprint within .15 s ("+at+")");cases++;
        }
        foreach(var spec in Melee)
        {
            Drop(spec,"swing",(p,t)=>{if(p.V.LastAttack<t)p.V.LastAttack=t;});
            Drop(spec,"block",(p,t)=>p.V.Guarding=true);
            Drop(spec,"roll",(p,t)=>{p.V.RollAt=t;p.V.RollUntil=t+.46f;});
            Drop(spec,"stun",(p,t)=>p.V.HitStunUntil=t+1);
        }
        foreach(var spec in new[]{"pistol","rifle","machinegun","fire","leet"})
        {
            Drop(spec,"shot or cast",(p,t)=>{p.A.LastShot=t;p.A.AttackAt=t;});
            Drop(spec,"block",(p,t)=>p.A.BlockUntil=t+1);
            Drop(spec,"roll",(p,t)=>p.A.RollAt=t);
            if(IsGun(spec))Drop(spec,"reload",(p,t)=>{p.A.ReloadAt=t;p.A.ReloadUntil=t+1;});
        }
        log.Add(cases+" cases: a swing, block, roll or stun (melee) and a shot, cast, block, roll or reload (guns and magic) drop the sprint carry within .15 s");
    }
    private static bool IsGun(string spec)=>spec=="pistol"||spec=="rifle"||spec=="machinegun";
    private static NativeViewSwayInput ViewInput(Actor a,float t){var i=NativeViewSway.Input(a,t,0,0,0);return i;}
    private static void View(List<string> log)
    {
        // The first-person carry: the arena input carries the sprint; a sword's view drops and tips down, a greatsword's lifts
        // and tips back over the shoulder; a swing fades it within .15 s.
        var pitch=new Dictionary<string,float>();float upMost=0;
        foreach(var spec in new[]{"sword","daggers","shield","greatsword","pistol","rifle","machinegun","fire"})
        {
            var a=new Actor{Id=3,SourceSpec=spec,Weapon=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword,Hp=100,Grounded=true,Sprinting=true,Velocity=new V3(0,0,SprintSpeed)};
            var b=new Actor{Id=3,SourceSpec=spec,Weapon=a.Weapon,Hp=100,Grounded=true,Sprinting=false,Velocity=new V3(0,0,SprintSpeed)};
            var on=new NativeViewSway();var off=new NativeViewSway();float inAt=-1,outAt=-1;NativeViewSwayPose po=default,pf=default;
            for(int f=0;f<120;f++)
            {
                float t=2+f*Dt;if(f==60){a.AttackAt=t;a.AttackUntil=t+.5f;}
                po=on.Sample(ViewInput(a,t));pf=off.Sample(ViewInput(b,t));
                if(f==40){pitch[spec]=po.Pitch-pf.Pitch;Check(Math.Abs(po.Pitch-pf.Pitch)>.03f||Math.Abs(po.Roll-pf.Roll)>.03f||Math.Abs(po.Y-pf.Y)>.01f,spec+": the view weapon takes its sprint carry");upMost=Math.Max(upMost,Math.Abs(po.Y-pf.Y));}
                if(f==9)inAt=on.Sprint;if(f>60&&outAt<0&&on.Sprint<.05f)outAt=(f-60)*Dt;
            }
            Check(inAt>.94f&&outAt>0&&outAt<=.16f,spec+": the view carry blends in ("+inAt+") and a swing fades it within .15 s ("+outAt+")");
        }
        Check(pitch["sword"]<-.15f&&pitch["greatsword"]>.12f&&pitch["pistol"]<-.15f&&pitch["machinegun"]<-.1f&&pitch["rifle"]>.1f,"per weapon: the sword tips down, the greatsword back over the shoulder, the pistol tucked muzzle-down, the machine gun dropped low, the rifle up at high port");
        // Raw source inputs (no Spec) keep the source view layer exactly, even with the flag.
        var raw=new NativeViewSway();var src=new NativeViewSway();
        for(int f=0;f<90;f++){var i=new NativeViewSwayInput{Pilot=1,Time=f*Dt,VelocityZ=SprintSpeed,Grounded=true,Gait=true};var s=i;s.Sprint=true;var x=raw.Sample(s);var y=src.Sample(i);Check(x.X==y.X&&x.Y==y.Y&&x.Z==y.Z&&x.Pitch==y.Pitch&&x.Roll==y.Roll&&raw.Sprint==0,"raw inputs keep the source view layer");}
        // The camera: an eased 5 deg FOV kick, half with Reduced effects, none with camera shake off; a light bob and roll.
        Check(NativeSprintRules.Fov(1,true,false)>=4&&NativeSprintRules.Fov(1,true,false)<=6&&NativeSprintRules.Fov(1,true,true)==NativeSprintRules.Fov(1,true,false)*.5f&&NativeSprintRules.Fov(1,false,false)==0&&NativeSprintRules.Fov(0,true,false)==0&&NativeSprintRules.Fov(.5f,true,false)==NativeSprintRules.Fov(1,true,false)*.5f,"the FOV kick: eased by the weight, half Reduced, none without camera shake");
        float bob=0,roll=0;for(float s=0;s<6.3f;s+=.05f){NativeSprintRules.CameraSway(1,s,true,false,out float u,out float r);bob=Math.Min(bob,u);roll=Math.Max(roll,Math.Abs(r));NativeSprintRules.CameraSway(1,s,false,false,out u,out r);Check(u==0&&r==0,"no camera bob without camera shake");NativeSprintRules.CameraSway(1,s,true,true,out float ur,out _);Check(Math.Abs(ur)<=Math.Abs(NativeSprintRules.CameraBob)*.5f+1e-6f,"Reduced effects halve the bob");}
        Check(bob<-.01f&&bob>=-.0121f&&roll>.4f&&roll<=.51f,"the camera bob (cm) and roll (deg) stay light ("+bob+", "+roll+")");
        log.Add("first person: per-weapon view carries (pitch sword "+pitch["sword"].ToString("0.00")+", greatsword +"+pitch["greatsword"].ToString("0.00")+", pistol "+pitch["pistol"].ToString("0.00")+" rad; up to "+(upMost*100).ToString("0.0")+" cm), in within .15 s and out within .15 s of a swing; raw source inputs exact; a "+NativeSprintRules.FovKick+" deg FOV kick (half Reduced, none without camera shake) and a "+(-bob*100).ToString("0.0")+" cm / "+roll.ToString("0.0")+" deg camera bob");
    }
    private static void Fx(List<string> log)
    {
        int streaks=0,dust=0,lines=0;float lowStreak=10,lowDust=10,inner=1;
        var feet=new Vector3(3,2,-1);var fwd=Vector3.Normalize(new Vector3(.3f,0,1));
        for(float t=0;t<2;t+=1/90f)
        {
            for(int k=0;k<NativeSprintFx.Anchors.Length;k++)if(NativeSprintFx.Streak(k,11,t,1,feet,fwd,fwd,out var head,out var tail,out float alpha))
            {streaks++;lowStreak=Math.Min(lowStreak,head.Y-feet.Y);Check(tail.Y==0&&alpha>0&&alpha<=1&&tail.Length()>.2f&&Vector3.Dot(tail,fwd)<0,"a streak trails level behind the body (never a disc on the floor)");}
            float age=t%NativeSprintFx.DustLife;
            for(int k=0;k<NativeSprintFx.DustPuffs;k++)if(NativeSprintFx.Dust(k,5,age,feet,fwd,out var at,out float size,out float op)){dust++;lowDust=Math.Min(lowDust,at.Y-feet.Y);Check(at.Y>=feet.Y+.04f-1e-5f&&size<=NativeSprintFx.DustSize+NativeSprintFx.DustGrow+1e-5f&&op>=0&&op<=NativeSprintFx.DustOpacity,"a dust puff rises off the floor behind the foot");}
            for(int k=0;k<NativeSprintFx.Lines;k++)if(NativeSprintFx.Line(k,7919,t,1,out _,out float i0,out float o0,out float a0)){lines++;inner=Math.Min(inner,i0);Check(i0>=.66f&&o0>i0&&a0>0&&a0<=NativeSprintFx.LineAlpha,"speed lines stay in the outer third");}
        }
        Check(lowStreak>.8f,"streaks shed off the body ("+lowStreak+" m over the feet at the lowest)");
        Check(NativeSprintFx.VignetteShape(.5f,.5f)==0&&NativeSprintFx.VignetteShape(.5f,.2f)==0&&NativeSprintFx.VignetteShape(0,0)>.99f&&NativeSprintFx.Vignette(1,1)==NativeSprintFx.VignetteAlpha&&NativeSprintFx.Vignette(1,.3f)<NativeSprintFx.VignetteAlpha&&NativeSprintFx.Vignette(0,1)==0,"the vignette leaves the middle clear, darkens the corners and dims with Reduced effects");
        Check(NativeSprintFx.LineShape(0,.5f)==0&&NativeSprintFx.LineShape(.5f,.5f)>.99f&&NativeSprintFx.LineShape(.5f,0)==0,"the line texture tapers at its ends and edges");
        Check(NativeSprintFx.Count(14,6,1,false)==14&&NativeSprintFx.Count(14,6,.3f,false)==6&&NativeSprintFx.Count(14,6,1,true)==7&&NativeSprintFx.Count(3,1,.3f,true)==1,"Reduced effects and Low graphics thin the effect");
        foreach(var s in new[]{"sword","daggers","shield","greatsword","pistol","rifle","machinegun","fire","water","lightning","darkness","leet","emperor"}){var c=NativeSprintFx.Tint(s,"#70e1d0");Check(MathUtil.Finite(c.X+c.Y+c.Z)&&c.X>=0&&c.Y>=0&&c.Z>=0&&c.X+c.Y+c.Z>.3f,s+": a visible tint");}
        Check(NativeSprintFx.WeaponTint("sword")!=NativeSprintFx.WeaponTint("greatsword")&&NativeSprintFx.WeaponTint("emperor")==NativeSprintTag.SprintColor,"tints differ per weapon; others take the HUD's SPRINTING colour");
        Check(NativeFootsteps.StrideFor(true)<NativeFootsteps.StrideFor(false)&&NativeFootsteps.StrideFor(false)==NativeFootsteps.Stride,"the sprint steps at a quicker cadence");
        log.Add("the effect: "+streaks+" streak, "+dust+" dust and "+lines+" screen-line samples; streaks trail level off the body (lowest "+lowStreak.ToString("0.00")+" m over the feet), dust rises behind the foot ("+(lowDust*100).ToString("0")+" cm up or more), lines from "+(inner*100).ToString("0")+"% of the half screen out, a clear middle; Reduced effects and Low graphics thin it; per-weapon tints; a quicker sprint footstep cadence");
    }
    private static void Allocation(List<string> log)
    {
        long used=0;
        foreach(var spec in new[]{"daggers","greatsword","rifle","fire"})
        {
            var p=new Pilot(spec,true);for(int f=0;f<200;f++){float t=1+f*Dt;p.Frame(t,f%80<40?SprintSpeed:RunSpeed,f%80<40);}
            long before=GC.GetAllocatedBytesForCurrentThread();for(int f=200;f<500;f++){float t=1+f*Dt;p.Frame(t,f%80<40?SprintSpeed:RunSpeed,f%80<40);}used+=GC.GetAllocatedBytesForCurrentThread()-before;
        }
        var view=new NativeViewSway();var a=new Actor{Id=3,SourceSpec="sword",Hp=100,Grounded=true,Sprinting=true,Velocity=new V3(0,0,SprintSpeed)};
        for(int f=0;f<60;f++)view.Sample(ViewInput(a,f*Dt));long b2=GC.GetAllocatedBytesForCurrentThread();for(int f=60;f<300;f++){a.Sprinting=f%90<45;view.Sample(ViewInput(a,f*Dt));}used+=GC.GetAllocatedBytesForCurrentThread()-b2;
        Check(used==0,"no allocation per frame ("+used+" bytes)");
        log.Add("no allocation per frame sprinting on and off (melee, gun and magic routes, the first-person view)");
    }
    private static void Hooks(string root,List<string> log)
    {
        string Read(string f)=>File.ReadAllText(Path.Combine(root,f));
        string mm=Read("Assets/ChipArena/Core/MeleeMotion.cs"),gm=Read("Assets/ChipArena/Core/GunMotion.cs"),mp=Read("Assets/ChipArena/Core/MagicPose.cs"),ca=Read("Assets/ChipArena/Runtime/CapturedAnimation.cs");
        Check(mm.Contains("SprintLayer?.Melee(r,p,time,dt);\n            JumpLayer?.Melee(r,p,time,dt);"),"MeleeMotion lays the sprint carry after the action layers, before the jump layer");
        Check(gm.Contains("float sprint=SprintLayer?.Advance(r,a,time,dt)??0;")&&gm.Contains("SprintLayer?.Lean(r,v.Spec,sprint,gait.BodyBob);")&&gm.Contains("SprintLayer?.Gun(pose,a,sprint*(1-k),r.Stride);")&&mp.Contains("SprintLayer?.Lean(r,v.Spec,sprint,gait.BodyBob);")&&mp.Contains("SprintLayer?.Magic(r,v.Spec,time,sprint);"),"the gun and magic routes take their carries");
        Check(ca.Contains("SprintLayer=>Hits!=null?sprint??=new NativeSprintMotion():null;")&&ca.Contains("melee.SprintLayer=SprintLayer;")&&ca.Contains("gunMotion.SprintLayer=SprintLayer;")&&ca.Contains("magicMotion.SprintLayer=SprintLayer;"),"the arena rigs (host, guests, killcam, spectators) get the layer; source parity and other rigs do not");
        string cg=Read("Assets/ChipArena/Runtime/ConversionGame.cs");
        Check(cg.Contains("cameraView.fieldOfView=SprintFov(first,aim,Mathf.Lerp(cameraView.fieldOfView-sprintKick,")&&cg.Contains("viewModelCamera.fieldOfView=cameraView.fieldOfView-sprintKick;")&&cg.Contains("SprintSway();"),"the camera takes the FOV kick (not the view model) and the bob after the aim point");
        Check(Read("Assets/ChipArena/Runtime/NativeJumpFxPresentation.cs").Contains("SprintFx(arena,now);")&&Read("Assets/ChipArena/Runtime/NativeEffectsAudio.cs").Contains("float stride=NativeFootsteps.StrideFor(sprint);"),"the sprint effect and footstep cadence are wired");
        foreach(var f in new[]{"Assets/ChipArena/Core/ArenaWorld.cs","Assets/ChipArena/Core/NativePrediction.cs"})Check(!Read(f).Contains("NativeSprintMotion")&&!Read(f).Contains("NativeSprintFx")&&!Read(f).Contains("NativeSprintRules"),f+" has no sprint presentation dependencies (NativeSprintAttack is simulation)");
        log.Add("hooks: melee, gun and magic routes; arena rigs only; camera FOV kick and bob; the effect and the footstep cadence; sprint simulation untouched");
    }
}
