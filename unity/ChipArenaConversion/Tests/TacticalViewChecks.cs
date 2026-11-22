#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The first-person tactical view (owner 2026-11-21: "Utilize new FPS system / animations from tactical pack"; docs/FPS-TACTICAL.md).
    // Held here without the licensed bank (a synthetic one stands in): the bank's reader, every clip clock on the game's rules (a
    // reload fills ReloadAt..ReloadUntil exactly, the rifle's bolt is home at ShootReady, aiming settles in the gun's Settle), the
    // solve's sight picture, its hands on the model, no allocation per frame, and the runtime's boundaries: first person only,
    // presentation only (it writes no pilot state), no pack code or bank in git. Version 3: the pack guns (owner 2026-11-21: "Use
    // pack guns"): their entries and weapon clips read back, the pack-gun solve (both hands on the pack's places, the aim point
    // on the eye, the gun's own clips on the rules' clocks), the jumps, and the runtime's boundaries for the gun and the arms.
    internal static class TacticalViewChecks
    {
        private static int count;
        private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Tactical view: "+what);}
        private static bool Near(float a,float b,float e=1e-3f)=>Math.Abs(a-b)<=e;
        private static bool Near(Vector3 a,Vector3 b,float e=1e-3f)=>Vector3.Distance(a,b)<=e;

        public static void Run(string root)
        {
            count=0;
            var bytes=SyntheticBank();var bank=NativeTacticalBank.Parse(bytes);
            Check(bank.Count==Keys.Length,"the synthetic bank reads back every clip");
            foreach(var g in new[]{NativeTacticalGun.Pistol,NativeTacticalGun.Rifle,NativeTacticalGun.MachineGun})Check(bank.Covers(g),g.Weapon+"'s set is covered");
            Check(NativeTacticalGun.For(WeaponId.Sword)==null,"no tactical set for the sword");
            Bad(()=>NativeTacticalBank.Parse(new byte[]{1,2,3}),"a short file");
            Bad(()=>{var b=(byte[])bytes.Clone();b[0]=(byte)'X';NativeTacticalBank.Parse(b);},"a wrong magic");
            Bad(()=>NativeTacticalBank.Parse(bytes.Take(bytes.Length-5).ToArray()),"a truncated bank");
            Bad(()=>{var b=(byte[])bytes.Clone();BitConverter.GetBytes(NativeTacticalBank.Version+1).CopyTo(b,4);NativeTacticalBank.Parse(b);},"another version");
            Bad(()=>{var b=(byte[])bytes.Clone();BitConverter.GetBytes(float.NaN).CopyTo(b,b.Length-4);NativeTacticalBank.Parse(b);},"a non-finite value");
            Bad(()=>{var b=SyntheticBank(gunNaN:true);NativeTacticalBank.Parse(b);},"a non-finite gun");
            Check(bank.Gun("pistol")!=null&&bank.Gun("rifle")!=null&&bank.Gun("machinegun")!=null&&bank.Gun("sword")==null,"the pack guns read back");
            Check(bank.WeaponClip("pistol.w.fire")?.Bones==1&&bank.WeaponClip("pistol.w.fire").Paths[0]=="Model/slide","a weapon clip's bones read back");
            {
                var wc=bank.WeaponClip("pistol.w.fire");var p=new Vector3[1];var q=new Quaternion[1];wc.Sample(wc.Length*.5f,p,q);
                Check(Near(p[0].Z,-.04f,2e-3f)&&Near(q[0].W,1,1e-4f),"a weapon clip samples its bone's place and turn ("+p[0]+")");
                wc.Sample(float.NaN,p,q);Check(Near(p[0].Z,0),"a weapon clip clamps a bad time to its start");
            }
            var old=NativeTacticalBank.Parse(SyntheticBank(guns:false));Check(old.Gun("pistol")==null&&!old.WeaponClips.Any(),"a bank without pack guns reads (the game's models stay)");
            Timing();
            Solve(bank);
            Pack(bank);
            Sources(root);
            Console.WriteLine("PASS tactical view: "+count+" checks: the bank's reader, every clip on the rules' clocks (reload, bolt, kick, draw, aim), the sight picture, the hands on the model, the pack guns (their clips, hands, aim point and jumps), no allocation per frame, first person and presentation only, no pack code or bank in git");
        }
        private static void Bad(Action a,string what){try{a();}catch(InvalidOperationException){count++;return;}catch(ArgumentException){count++;return;}throw new Exception("Tactical view: "+what+" was read");}

        private static readonly string[] Keys={"general.idlepose","general.idleloop","general.walkpose","general.walkloop","general.sprintloop",
            "pistol.idle","pistol.fire","pistol.reload","pistol.quickdraw","rifle.idle","rifle.fire","rifle.reload","rifle.draw","ar.idle","ar.reload","ar.draw",
            "general.jumpstart","general.jumploop","general.jumpend"};
        // A bank in the bake's format: the gun 30 cm ahead, the right wrist on it, the left 20 cm ahead of that; the action clips
        // move the gun down and back (so they show), the fire clips kick it; the additive bone at the weapon offset's rest.
        private static byte[] SyntheticBank(bool guns=true,bool gunNaN=false)
        {
            var ms=new MemoryStream();var w=new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("CAVM"));w.Write(NativeTacticalBank.Version);w.Write(60f);w.Write(Keys.Length);w.Write(NativeTacticalClip.Stride);
            foreach(var key in Keys)
            {
                bool action=key.EndsWith("reload")||key.EndsWith("draw")||key=="rifle.fire";int frames=key.EndsWith("idle")||key.EndsWith("pose")?6:action?121:41;
                var name=Encoding.UTF8.GetBytes(key);w.Write(name.Length);w.Write(name);w.Write(frames);w.Write((frames-1)/60f);w.Write(action?.01f:-1f);w.Write(action?.8f:-1f);
                for(int k=0;k<frames;k++)
                {
                    float u=frames>1?k/(float)(frames-1):0,arc=(float)Math.Sin(Math.PI*u);
                    var gun=new Vector3(.03f,-.1f,.3f);if(action)gun+=new Vector3(0,-.08f,-.05f)*arc;if(key.EndsWith(".fire")&&!action)gun+=new Vector3(0,0,-.02f)*arc;
                    float[] f=new float[NativeTacticalClip.Stride];
                    void P(int at,Vector3 v){f[at]=v.X;f[at+1]=v.Y;f[at+2]=v.Z;}void Q(int at,Quaternion q){f[at]=q.X;f[at+1]=q.Y;f[at+2]=q.Z;f[at+3]=q.W;}
                    P(0,gun);Q(3,Quaternion.Identity);P(7,gun+new Vector3(0,-.03f,-.12f));Q(10,Quaternion.Identity);P(14,gun+new Vector3(-.05f,-.06f,.08f)+(action?new Vector3(0,-.1f,0)*arc:Vector3.Zero));Q(17,Quaternion.Identity);
                    P(21,new Vector3(.25f,-.35f,.05f));P(24,new Vector3(-.2f,-.35f,.1f));P(27,new Vector3(.2f,-.2f,-.1f));P(30,new Vector3(-.2f,-.2f,-.1f));
                    for(int i=0;i<30;i++)P(33+i*3,Vector3.UnitZ);Q(123,Quaternion.Identity);P(127,key.StartsWith("general.jump")?new Vector3(0,-.04f,0):Vector3.Zero);Q(130,Quaternion.Identity);Q(134,Quaternion.Identity);Q(138,Quaternion.Identity);
                    foreach(var v in f)w.Write(v);
                }
            }
            // Version 3: the pack guns (their muzzle 20 cm ahead, the aim point 20 cm behind and 4 cm up) and their weapon clips (a
            // slide back 4 cm at the middle of the shot's, the reload's long, the rest one frame).
            void Str(string v){var b=Encoding.UTF8.GetBytes(v);w.Write(b.Length);w.Write(b);}
            if(!guns){w.Write(0);w.Write(0);return ms.ToArray();}
            var packs=new[]{"pistol","rifle","machinegun"};w.Write(packs.Length);
            foreach(var gk in packs){Str(gk);Str("Model");w.Write(0f);w.Write(0f);w.Write(.2f);w.Write(0f);w.Write(gunNaN&&gk=="rifle"?float.NaN:.04f);w.Write(-.2f);}
            w.Write(packs.Length*3);
            foreach(var gk in packs)foreach(var kind in new[]{"idle","fire","reload"})
            {
                int frames=kind=="idle"?1:kind=="fire"?41:121;Str(gk+".w."+kind);Str(gk);w.Write(frames);w.Write((frames-1)/60f);w.Write(1);Str("Model/slide");
                for(int k=0;k<frames;k++){float u=frames>1?k/(float)(frames-1):0,arc=(float)Math.Sin(Math.PI*u);w.Write(0f);w.Write(0f);w.Write(-.04f*arc);w.Write(0f);w.Write(0f);w.Write(0f);w.Write(1f);}
            }
            return ms.ToArray();
        }

        private static void Timing()
        {
            foreach(var g in new[]{NativeTacticalGun.Pistol,NativeTacticalGun.Rifle,NativeTacticalGun.MachineGun})
            {
                var gun=GunRules.Get(g.Weapon);string n=g.Weapon.ToString();
                // The reload: the rule's span exactly, the whole clip, back at rest (weight 0) when the reload ends.
                float at=10,until=at+gun.Reload,len=3.47f;
                Check(Near(NativeTacticalTiming.ReloadClipTime(at,at,until,len,g.ReloadWarp),0),n+": the reload clip starts with the reload");
                Check(Near(NativeTacticalTiming.ReloadClipTime(until-1e-4f,at,until,len,g.ReloadWarp),len,.01f),n+": the reload clip ends as the reload does ("+gun.Reload+" s)");
                Check(NativeTacticalTiming.ReloadClipTime(until,at,until,len,g.ReloadWarp)<0&&NativeTacticalTiming.ReloadWeight(until,at,until)==0,n+": nothing of the reload shows once it is over");
                Check(NativeTacticalTiming.ReloadWeight(at-.01f,at,until)==0,n+": nothing before it starts");
                Check(Near(NativeTacticalTiming.ReloadWeight(at+gun.Reload*.5f,at,until),1),n+": the reload is whole through its middle");
                float prev=-1;bool mono=true;for(int i=0;i<=200;i++){float v=NativeTacticalTiming.Warp(g.ReloadWarp,i/200f);if(v<prev-1e-6f)mono=false;prev=v;}
                Check(mono&&Near(NativeTacticalTiming.Warp(g.ReloadWarp,0),0)&&Near(NativeTacticalTiming.Warp(g.ReloadWarp,1),1),n+": the reload's warp is monotone from start to end");
                // Aiming down the sights settles in the gun's own Settle.
                float p=0;int steps=0;while(p<1&&steps<1000){p=NativeTacticalTiming.AimStep(p,true,g.Weapon,1/240f);steps++;}
                Check(Math.Abs(steps/240f-Math.Max(.08f,gun.Settle))<=1.5f/240,n+": aiming down the sights takes its Settle ("+gun.Settle+" s)");
                Check(NativeTacticalTiming.AimEase(0)==0&&Near(NativeTacticalTiming.AimEase(1),1),n+": the aim eases from hip to sights");
                // The interval: ShootReady when carried, else the rule.
                Check(Near(NativeTacticalTiming.Interval(g.Weapon,5,5+gun.Interval*1.3f),gun.Interval*1.3f),n+": the clips follow ShootReady (chips change the fire rate)");
                Check(Near(NativeTacticalTiming.Interval(g.Weapon,5,0),gun.Interval),n+": the rule's interval without ShootReady");
                // The draw: none on its own; a shot cuts it within DrawOut.
                Check(NativeTacticalTiming.DrawWeight(1+g.DrawSeconds*.5f,1,g.DrawSeconds,-100)>0&&NativeTacticalTiming.DrawWeight(1+g.DrawSeconds+NativeTacticalTiming.DrawOut+1e-3f,1,g.DrawSeconds,-100)==0,n+": the draw plays, then ends");
                Check(NativeTacticalTiming.DrawWeight(1.2f+NativeTacticalTiming.DrawOut,1,g.DrawSeconds,1.2f)==0,n+": a shot cuts the draw");
            }
            // The rifle's bolt: its action (to OnActionEnded) fills the interval, home exactly when the next shot is allowed.
            var rifle=GunRules.Get(WeaponId.Rifle);float action=1.167f,length=1.667f;
            Check(Near(NativeTacticalTiming.FireActionTime(20+rifle.Interval-1e-4f,20,rifle.Interval,action,length),action,.01f),"the rifle's bolt is home when the next shot is allowed ("+rifle.Interval+" s)");
            Check(NativeTacticalTiming.FireActionTime(20+rifle.Interval+(length-action)+.01f,20,rifle.Interval,action,length)<0,"the bolt's settle ends at the clip's own pace");
            // The pistol's kick fits in its interval.
            var pistol=GunRules.Get(WeaponId.Pistol);
            Check(NativeTacticalTiming.FireKickTime(30+pistol.Interval,30,pistol.Interval,.667f)<0&&NativeTacticalTiming.FireKickTime(30+pistol.Interval*.5f,30,pistol.Interval,.667f)>0,"the pistol's kick ends within its interval ("+pistol.Interval+" s)");
        }

        private static void Solve(NativeTacticalBank bank)
        {
            var view=new NativeTacticalView(bank);var s=new NativeTacticalSolution();
            var g=NativeTacticalGun.Pistol;var sight=new Vector3(0,.09f,-.04f);
            var cal=view.Calibrate(g,new Vector3(.01f,-.05f,-.03f),Quaternion.CreateFromAxisAngle(Vector3.UnitX,.3f),sight);
            var gun=GunRules.Get(WeaponId.Pistol);
            NativeTacticalInput I(float t,bool aim=false,float lastShot=-100,float reloadAt=-100,float reloadUntil=-100,bool fresh=false)
                =>new NativeTacticalInput{Time=t,Weapon=WeaponId.Pistol,LastShot=lastShot,ReloadAt=reloadAt,ReloadUntil=reloadUntil,Aim=aim,Grounded=true,Shake=1,Fresh=fresh};
            Check(view.Solve(I(0,fresh:true),cal,s),"the solve runs on a covered bank");
            Check(s.Action=="pistol.quickdraw","a fresh view draws its gun");
            for(float t=0;t<2;t+=1/60f)view.Solve(I(t),cal,s);
            Check(s.Action==""&&s.ActionWeight==0,"the draw is over in its seconds");
            // At rest the right hand is on the model's grip and the model on the pack's gun: the hand target is the grip through the weapon frame.
            var grip=Vector3.Transform(cal.RightGrip,s.WeaponTurn)+s.Weapon;
            Check(Near(grip,s.Right,1e-3f),"the right hand holds the model's grip");
            // Fully aimed: the model's rear sight on the eye's axis, EyeRelief ahead, the bore along the view.
            for(float t=2;t<3;t+=1/60f)view.Solve(I(t,aim:true),cal,s);
            var rear=s.Weapon+Vector3.Transform(sight,s.WeaponTurn);var bore=Vector3.Transform(Vector3.UnitZ,s.WeaponTurn);
            Check(Math.Abs(rear.X)<.02f&&Math.Abs(rear.Y)<.02f&&Near(rear.Z,g.EyeRelief,.03f),"aimed, the rear sight sits on the eye's axis ("+rear+")");
            Check(bore.Z>.995f,"aimed, the bore runs along the view");
            Check(Near(s.Ads,1),"the aim is whole after its settle");
            // A reload plays its clip over the rule's span only.
            view.Solve(I(3.5f,reloadAt:3.4f,reloadUntil:3.4f+gun.Reload),cal,s);
            Check(s.Action=="pistol.reload"&&s.ActionWeight>.9f,"a reload plays the reload clip");
            view.Solve(I(3.4f+gun.Reload,reloadAt:3.4f,reloadUntil:3.4f+gun.Reload),cal,s);
            Check(s.Action!="pistol.reload","the reload clip is gone when the reload is done");
            // No allocation per frame.
            for(int i=0;i<200;i++)view.Solve(I(5+i/60f,aim:i%50<25,lastShot:5+(i/30)*.5f),cal,s);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<600;i++)view.Solve(I(9+i/60f,aim:i%50<25,lastShot:9+(i/30)*.5f,reloadAt:12,reloadUntil:12+gun.Reload),cal,s);
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the solve allocates nothing per frame");
            // The rifle: the bolt plays from the shot; the machine gun: recoil alone.
            var rv=new NativeTacticalView(bank);var rc=rv.Calibrate(NativeTacticalGun.Rifle,Vector3.Zero,Quaternion.Identity,sight);
            var ri=new NativeTacticalInput{Time=4,Weapon=WeaponId.Rifle,LastShot=3.9f,ShootReady=3.9f+GunRules.Get(WeaponId.Rifle).Interval,Grounded=true,Shake=1};rv.Solve(ri,rc,s);
            Check(s.Action=="rifle.fire","the rifle's shot plays its bolt");
            Vector3 Mg(bool shot)
            {
                var mv=new NativeTacticalView(bank);var mc=mv.Calibrate(NativeTacticalGun.MachineGun,Vector3.Zero,Quaternion.Identity,sight);
                var mi=new NativeTacticalInput{Time=4,Weapon=WeaponId.MachineGun,LastShot=-100,Grounded=true,Shake=1};mv.Solve(mi,mc,s);
                if(shot)mi.LastShot=4.01f;mi.Time=4.01f;mv.Solve(mi,mc,s);mi.Time=4.05f;mv.Solve(mi,mc,s);return s.Weapon;
            }
            Check(Vector3.Distance(Mg(false),Mg(true))>1e-3f,"the machine gun's shot kicks it (procedural recoil)");
        }

        // The pack guns: both hands on the pack's own places, the aim point on the eye, the gun's own clips on the rules' clocks, the jumps.
        private static void Pack(NativeTacticalBank bank)
        {
            var s=new NativeTacticalSolution();var g=NativeTacticalGun.Pistol;var gun=GunRules.Get(WeaponId.Pistol);
            var view=new NativeTacticalView(bank);var cal=view.CalibratePack(g);
            Check(cal.Set&&cal.PackGun&&Near(cal.Sight,new Vector3(0,.04f,-.2f)),"the pack calibration takes the gun's aim point");
            Check(!new NativeTacticalView(NativeTacticalBank.Parse(SyntheticBank(guns:false))).CalibratePack(g).Set,"no pack calibration without the pack gun");
            NativeTacticalInput I(float t,bool aim=false,float lastShot=-100,float reloadAt=-100,float reloadUntil=-100,bool fresh=false,bool grounded=true)
                =>new NativeTacticalInput{Time=t,Weapon=WeaponId.Pistol,LastShot=lastShot,ReloadAt=reloadAt,ReloadUntil=reloadUntil,Aim=aim,Grounded=grounded,Shake=1,Fresh=fresh};
            view.Solve(I(0,fresh:true),cal,s);for(float t=0;t<2;t+=1/60f)view.Solve(I(t),cal,s);
            // At rest: the right wrist and the left hand on the pack's own places on its gun (the synthetic clip's), the fingers the clip's.
            var inv=Quaternion.Inverse(s.WeaponTurn);
            Check(Near(Vector3.Transform(s.Right-s.Weapon,inv),new Vector3(0,-.03f,-.12f),2e-3f),"the pack gun: the right hand on the pack's own grip");
            Check(Near(Vector3.Transform(s.Left-s.Weapon,inv),new Vector3(-.05f,-.06f,.08f),2e-3f),"the pack gun: the left hand on the pack's own place");
            Check(s.RightFingerWeight==1&&s.LeftFingerWeight==1,"the pack gun: both hands take the clip's fingers");
            Check(s.WeaponClip=="pistol.w.idle","the pack gun rests on its idle clip");
            float k=view.PackScale(g,NativeWeaponDressing.MuzzleZ("pistol"));Check(k>=.1f&&k<=1&&Near(k,(.2f+.12f)/NativeWeaponDressing.MuzzleZ("pistol"),1e-3f),"the effects' frame takes the pack gun's reach ("+k+")");
            // Fully aimed: the aim point on the eye, the bore along the view.
            for(float t=2;t<3;t+=1/60f)view.Solve(I(t,aim:true),cal,s);
            var aimAt=s.Weapon+Vector3.Transform(cal.Sight,s.WeaponTurn);
            Check(aimAt.Length()<.02f&&Vector3.Transform(Vector3.UnitZ,s.WeaponTurn).Z>.995f,"aimed, the pack gun's aim point is on the eye ("+aimAt+")");
            // A shot: the weapon's fire clip from the shot, within the interval; a reload: its reload clip on the reload's share.
            view.Solve(I(3.2f,lastShot:3.2f-gun.Interval*.4f),cal,s);var wf=bank.WeaponClip("pistol.w.fire");
            Check(s.WeaponClip=="pistol.w.fire"&&s.WeaponTime>0&&s.WeaponTime<wf.Length,"a shot plays the gun's own fire clip");
            Check(NativeTacticalTiming.FireKickTime(30+gun.Interval,30,gun.Interval,wf.Length)<0,"the gun's fire clip ends within the interval");
            view.Solve(I(3.4f+gun.Reload*.5f,reloadAt:3.4f,reloadUntil:3.4f+gun.Reload),cal,s);var wr=bank.WeaponClip("pistol.w.reload");var ar=bank.Clip("pistol.reload");
            Check(s.WeaponClip=="pistol.w.reload"&&Near(s.WeaponTime/wr.Length,s.ActionTime/ar.Length,1e-3f),"a reload plays the gun's own reload clip with the arms'");
            view.Solve(I(3.4f+gun.Reload,reloadAt:3.4f,reloadUntil:3.4f+gun.Reload),cal,s);Check(s.WeaponClip!="pistol.w.reload","the gun's reload clip ends with the reload");
            // The rifle's bolt on the pack gun: its weapon clip with the arms' action (home when the next shot is allowed).
            var rv=new NativeTacticalView(bank);var rc=rv.CalibratePack(NativeTacticalGun.Rifle);float ri=GunRules.Get(WeaponId.Rifle).Interval;
            rv.Solve(new NativeTacticalInput{Time=4,Weapon=WeaponId.Rifle,LastShot=3.9f,ShootReady=3.9f+ri,Grounded=true,Shake=1},rc,s);
            Check(s.Action=="rifle.fire"&&s.WeaponClip=="rifle.w.fire","the rifle's shot plays its bolt on the pack gun");
            // The machine gun: every shot's weapon clip fits its interval (the automatic's recoil is procedural).
            var mv=new NativeTacticalView(bank);var mc=mv.CalibratePack(NativeTacticalGun.MachineGun);float mi=GunRules.Get(WeaponId.MachineGun).Interval;
            mv.Solve(new NativeTacticalInput{Time=5,Weapon=WeaponId.MachineGun,LastShot=5-mi*.5f,Grounded=true,Shake=1},mc,s);
            Check(s.WeaponClip=="machinegun.w.fire"&&NativeTacticalTiming.FireKickTime(5+mi,5,mi,bank.WeaponClip("machinegun.w.fire").Length)<0,"the machine gun's fire clip fits each interval ("+mi+" s)");
            // The jump: the gun moves while airborne, and is back once the landing has played.
            var jv=new NativeTacticalView(bank);var jc=jv.CalibratePack(g);
            jv.Solve(I(10,fresh:true),jc,s);for(float t=10;t<11;t+=1/60f)jv.Solve(I(t),jc,s);var ground=s.Weapon;
            for(float t=11;t<11.5f;t+=1/60f)jv.Solve(I(t,grounded:false),jc,s);var air=s.Weapon;
            for(float t=11.5f;t<13;t+=1/60f)jv.Solve(I(t),jc,s);
            Check(Vector3.Distance(ground,air)>.01f&&Vector3.Distance(ground,s.Weapon)<2e-3f,"the jump plays airborne and is gone after the landing");
            // No allocation per frame on the pack gun either.
            for(int i=0;i<100;i++)view.Solve(I(20+i/60f,lastShot:20+(i/30)*.5f),cal,s);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<600;i++)view.Solve(I(30+i/60f,aim:i%50<25,lastShot:30+(i/30)*.5f,reloadAt:33,reloadUntil:33+gun.Reload,grounded:i%120>20),cal,s);
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the pack solve allocates nothing per frame");
        }

        private static void Sources(string root)
        {
            string runtime=Path.Combine(root,"Assets/ChipArena/Runtime"),core=Path.Combine(root,"Assets/ChipArena/Core");
            string pres=File.ReadAllText(Path.Combine(runtime,"NativeTacticalViewPresentation.cs")),view=File.ReadAllText(Path.Combine(runtime,"NativeViewPresentation.cs")),game=File.ReadAllText(Path.Combine(runtime,"ConversionGame.cs"));
            // First person only: the pose is called from RenderFirstPerson alone, after the view body is posed.
            int pose=view.IndexOf("VisualViewPose(viewRig,shown,time,ScopeActive);",StringComparison.Ordinal),tac=view.IndexOf("TacticalViewPose(shown,time);",StringComparison.Ordinal);
            Check(pose>0&&tac>pose,"RenderFirstPerson poses the tactical view after the view body");
            foreach(var f in Directory.GetFiles(runtime,"*.cs"))if(!f.EndsWith("NativeViewPresentation.cs",StringComparison.Ordinal)&&!f.EndsWith("NativeTacticalViewPresentation.cs",StringComparison.Ordinal))
                Check(!File.ReadAllText(f).Contains("TacticalViewPose("),Path.GetFileName(f)+" never poses the tactical view (third person is untouched)");
            Check(game.Contains("if(!menu&&Spectator==null&&!cinematic.Directing&&presentation.FirstPerson&&prefs.cameraShake)presentation.TacticalCameraKick(cameraView.transform,prefs.reducedEffects?.4f:1);"),
                "the clips' camera motion is first person with camera shake on, reduced with reduced effects");
            Check(pres.Contains("tacticalKickFrame=-1;if(view==null||!(scale>0)||!FirstPerson||tacticalFrame<Time.frameCount-2)return;"),"the camera motion needs a tactical first-person frame");
            // Presentation only: nothing writes a pilot's state.
            Check(!Regex.IsMatch(pres,@"\ba\.[A-Z]\w*\s*(=[^=]|\+=|-=)"),"the presentation writes no pilot field");
            Check(!Regex.IsMatch(File.ReadAllText(Path.Combine(core,"NativeTacticalView.cs")),@"\bi\.[A-Z]\w*\s*(=[^=]|\+=|-=)"),"the solve writes no input field");
            // No pack code, no bank and no pack asset in git: the bank is local (LocalAssets), read by name from Resources.
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories))
                Check(!Regex.IsMatch(File.ReadAllText(f),@"KINEMATION\.[A-Z]|using\s+KINEMATION|KShooter|RecoilAnimation|TacticalShooter(Player|Weapon)"),Path.GetFileName(f)+" references no KINEMATION code");
            Check(Directory.GetFiles(Path.Combine(root,"Assets"),"ChipArenaViewMotion*",SearchOption.AllDirectories).All(f=>f.Replace('\\','/').Contains("/Assets/LocalAssets/")),"the view-motion bank is only ever a local asset");
            Check(pres.Contains("Resources.Load<TextAsset>(NativeTacticalBank.Resource)")&&File.ReadAllText(Path.Combine(core,"NativeTacticalView.cs")).Contains("public const string Resource=\"ChipArenaViewMotion\",GunsResource=\"ChipArenaViewGuns\";"),"the bank is read by its resource name");
            // The pack guns: a local asset read by name, drawn for the view rig alone (the game's model in third person and on every
            // other rig), hidden the frame SourceCharacter does not draw them; the arms cut at the shoulders only for the tactical view.
            Check(pres.Contains("Resources.Load<GameObject>(NativeTacticalBank.GunsResource)"),"the pack guns are read by their resource name");
            Check(Directory.GetFiles(Path.Combine(root,"Assets"),"ChipArenaViewGuns*",SearchOption.AllDirectories).All(f=>f.Replace('\\','/').Contains("/Assets/LocalAssets/")),"the pack guns are only ever a local asset");
            string weapon=File.ReadAllText(Path.Combine(runtime,"NativeVisualWeaponPresentation.cs")),bodyFile=File.ReadAllText(Path.Combine(runtime,"NativeVisualBodyPresentation.cs"));
            Check(weapon.Contains("if(rig==viewRig&&TacticalPackDrawn(viewBody))return true;")&&weapon.Contains("TacticalPackAfterDraw();"),"the pack gun replaces the model for the view rig alone, and hides when not drawn");
            foreach(var f in Directory.GetFiles(runtime,"*.cs"))if(!f.EndsWith("NativeVisualWeaponPresentation.cs",StringComparison.Ordinal)&&!f.EndsWith("NativeTacticalViewPresentation.cs",StringComparison.Ordinal))
                Check(!File.ReadAllText(f).Contains("TacticalPackDrawn("),Path.GetFileName(f)+" never draws the pack gun");
            Check(bodyFile.Contains("if(skin!=null)TacticalViewSkin(body,skin,skin.sharedMesh,cut);")&&pres.Contains("bool shoulders=on||TacticalShoulderCut&&VisualViewDrawn(viewRig);")&&pres.Contains("TacticalArmCut(shoulders);"),"the view body's arms are cut at the shoulders while the view body draws in first person");
        }
    }
}
#endif
