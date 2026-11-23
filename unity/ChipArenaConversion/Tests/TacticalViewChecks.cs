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
            // The pack's own lens: its FPCamera's 90 degrees, each gun's TacAnimSettings aimFov.
            Check(NativeTacticalGun.Pistol.HipFov==90&&NativeTacticalGun.Rifle.HipFov==90&&NativeTacticalGun.MachineGun.HipFov==90&&NativeTacticalGun.Rifle.AimFov==70&&NativeTacticalGun.MachineGun.AimFov==70,"the view model's lens is the pack's (90; aimed 70 for the SRM-12 and TR15)");
            // The pistol aimed larger (owner 2026-11-23: "The pistol ADS still looks a bit small"): its rear sight 24 cm from the eye on its aim
            // row's own 55 degrees (it was 34 cm on 80, under half the pack's own Viper picture).
            Check(NativeTacticalGun.Pistol.AimFov==55&&NativeTacticalGun.Pistol.AimFov==GunRules.Get(WeaponId.Pistol).Fov&&Near(NativeTacticalGun.Pistol.EyeRelief,.24f),"the aimed pistol: 24 cm eye relief on its aim row's 55 degree lens");
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
            AimKick(bank);
            Sources(root);
            ViewArms(root);
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
            // The front post (owner 2026-11-23: "aim the iron sights where the shots are going"): aimed, the rear sight and the front post
            // both on the eye's axis (the camera's centre ray, the shots' line), so a post that stands over the aperture's line (the TR15's
            // tip, 4.8 mm over it 38 cm on) tips the gun, not the sight picture; without one the bore runs along the view as before.
            {
                var mg=NativeTacticalGun.MachineGun;var rearS=new Vector3(0,.0062f,.005f);var frontS=new Vector3(0,.011f,.384f);
                Vector3 Aimed(bool fronted,out Vector3 front,out float ads)
                {
                    var fv=new NativeTacticalView(bank);var fs=new NativeTacticalSolution();
                    var fc=fv.Calibrate(mg,new Vector3(.05f,-.13f,-.12f),Quaternion.Identity,rearS);fc.Fronted=fronted;fc.Front=frontS;
                    for(float t=0;t<3;t+=1/60f)fv.Solve(new NativeTacticalInput{Time=t,Weapon=WeaponId.MachineGun,LastShot=-100,Aim=t>1.5f,Grounded=true,Shake=1},fc,fs);
                    front=fs.Weapon+Vector3.Transform(frontS,fs.WeaponTurn);ads=fs.Ads;return fs.Weapon+Vector3.Transform(rearS,fs.WeaponTurn);
                }
                var r=Aimed(true,out var f,out float fa);
                Check(Near(fa,1),"the machine gun's aim is whole after its settle");
                Check(Math.Abs(r.X)<1e-3f&&Math.Abs(r.Y)<1e-3f&&Near(r.Z,mg.EyeRelief,1e-3f),"aimed, the TR15's aperture sits on the eye's axis at its eye relief ("+r+")");
                Check(Math.Abs(f.X)<1e-3f&&Math.Abs(f.Y)<1e-3f&&f.Z>r.Z+.3f,"aimed, the front post sits on the same axis: the sights point along the shots' line ("+f+")");
                var r0=Aimed(false,out var f0,out _);
                Check(Math.Abs(r0.Y)<1e-3f&&f0.Y>.004f,"without a front post the bore runs along the view (the post stands "+f0.Y+" m over the axis)");
                var turn=NativeTacticalMath.SightTurn(new Vector3(.01f,.0048f,.379f));var laid=Vector3.Transform(Vector3.Normalize(new Vector3(.01f,.0048f,.379f)),turn);
                Check(Near(laid,Vector3.UnitZ,1e-5f)&&Math.Abs(Vector3.Transform(Vector3.UnitX,turn).Y)<1e-5f,"SightTurn lays a sight line along +z without rolling the gun");
                Check(NativeTacticalView.AimSteady==1,"fully aimed, the procedural layer turns the gun but no longer moves it");
                Check(NativeTacticalGun.Pistol.ModelFront!=Vector3.Zero&&Near(NativeTacticalGun.Pistol.ModelFront.Y,NativeTacticalGun.Pistol.ModelSight.Y),"the Army pistol's front sight is measured, level with its rear (the slide's top)");
            }
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

        // Every other weapon and class (owner 2026-11-22: "Go ahead and fix"): the eye-anchored arms on the old view model's hands.
        private static void ViewArms(string root)
        {
            foreach(var g in new[]{"pistol","rifle","machinegun","siren",NativeTopDog.Spec,CyberLordSpec.Id,"",null})Check(!NativeViewArms.Eligible(g),"no eye-anchored arms for "+(g??"null")+" (the tactical view, the Siren's voxel arms, the Top Dog and the Cyber Lord keep theirs)");
            foreach(var g in new[]{"sword","greatsword","katana","daggers","shield","glaive","fangs","bow","fire","water","lightning","darkness","leet",NativeBombs.Spec,"emperor","ansatsuken"})Check(NativeViewArms.Eligible(g),g+" takes the eye-anchored arms");
            string runtime=Path.Combine(root,"Assets/ChipArena/Runtime");
            string arms=File.ReadAllText(Path.Combine(runtime,"NativeViewArmsPresentation.cs")),pres=File.ReadAllText(Path.Combine(runtime,"NativeTacticalViewPresentation.cs")),view=File.ReadAllText(Path.Combine(runtime,"NativeViewPresentation.cs"));
            Check(pres.Contains("bool on=TacticalViewPosed(a,time),arms=on||ViewArmsPose(a,time);TacticalArmCut(arms);TacticalFill(arms);"),"the arms come after the tactical view declines, with its cut and fill light");
            Check(view.Contains("+ViewArmsLift(shown,time);"),"the class's lift raises the first-person hold only");
            // Each class's own hold (F2 2026-11-22): finite and small, shoulders and elbow poles under the eye, a free hand only where
            // the class's other hand holds nothing, tucked downward.
            var specs=new[]{"sword","greatsword","katana","daggers","shield","glaive","fangs","bow","fire","water","lightning","darkness","leet",NativeBombs.Spec,"emperor","ansatsuken",NativeDevMotions.Greatsword,NativeDevMotions.Katana};
            bool Fin(Vector3 v)=>MathUtil.Finite(v.X)&&MathUtil.Finite(v.Y)&&MathUtil.Finite(v.Z);
            foreach(var g in specs)
            {
                var h=NativeViewArms.Of(g);Check(h!=NativeViewArms.Default&&h.Spec==g,g+" has its own hold");
                Check(Fin(h.Lift)&&h.Lift.Length()<.7f&&Fin(h.CarryLift)&&h.CarryLift.Length()<.7f,g+"'s lift is a small finite offset");
                Check(Fin(h.Shoulder)&&h.Shoulder.Y<-.1f&&Math.Abs(h.Shoulder.X)<.05f&&h.PoleRight.Y<0&&h.PoleLeft.Y<0&&h.PoleRight.X>0&&h.PoleLeft.X<0,g+"'s shoulders and elbows hang under the eye, the elbows out to their sides");
                Check(h.Free>=-1&&h.Free<=1&&(h.Free==0?h.Tuck==Vector3.Zero:h.Tuck.Y<-.2f),g+"'s free hand, if any, tucks downward");
            }
            Check(NativeViewArms.Of("greatsword").Free==0&&NativeViewArms.Of("daggers").Free==0&&NativeViewArms.Of(NativeFangs.Spec).Free==0&&NativeViewArms.Of(NativeBombs.Spec).Free==0&&NativeViewArms.Of("emperor").Free==0&&NativeViewArms.Of("ansatsuken").Free==0,"two-handed and dual holds tuck no hand");
            Check(NativeViewArms.Of("sword").Free==-1&&NativeViewArms.Of("fire").Free==1&&NativeViewArms.Of("leet").Free==1&&NativeViewArms.Of("bow").Free==1,"the sword's left, the staffs' right and the bow's string hand are free");
            Check(NativeViewArms.Of("no-such-class")==NativeViewArms.Default&&NativeViewArms.Lift("sword")==NativeViewArms.Of("sword").Lift,"an unknown class takes the default hold");
            {
                var bow=NativeViewArms.Of("bow");Check(NativeViewArms.LiftOf(bow,0)==bow.Lift+bow.CarryLift&&NativeViewArms.LiftOf(bow,1)==bow.Lift&&NativeViewArms.LiftOf(bow,float.NaN)==bow.Lift+bow.CarryLift&&Vector3.Distance(NativeViewArms.LiftOf(bow,.5f),bow.Lift+bow.CarryLift*.5f)<1e-5f,"a bow's carry lift fades out as it is raised onto the arrow line");
                var sw=NativeViewArms.Of("sword");Check(NativeViewArms.LiftOf(sw,0)==sw.Lift&&NativeViewArms.LiftOf(sw,1)==sw.Lift,"no carry lift but the bow's");
                Check(NativeViewArms.TuckWeight(new Vector3(0,0,.5f))==0&&NativeViewArms.TuckWeight(new Vector3(.2f,-.3f,.5f))==1&&NativeViewArms.TuckWeight(new Vector3(0,-.1f,0))==1&&NativeViewArms.TuckWeight(new Vector3(0,-.1f,-.2f))==1,"a free hand raised into view stays; one hanging low, at or behind the eye tucks away");
                float mid=NativeViewArms.TuckWeight(new Vector3(0,-.0875f,.5f));Check(mid>.2f&&mid<.8f,"the tuck eases in between ("+mid+")");
                Check(NativeViewArms.TuckWeight(new Vector3(float.NaN,float.NaN,float.NaN))==1,"a bad place tucks the hand away");
                var c=NativeViewArms.Look.Cloth;Check(c.X>0&&c.Y>0&&c.Z>0&&c.X<=.1f&&c.Y<=.1f&&c.Z<=.1f,"the dark cloth's lift is a small linear colour");
            }
            string res=Path.Combine(root,"Assets/ChipArena/Resources");
            string fx=File.ReadAllText(Path.Combine(res,"NativeVisualBodyFx.cginc")),toon=File.ReadAllText(Path.Combine(res,"NativeVisualBodyToon.shader"));
            Check(fx.Contains("if(_ViewCloth.w<0.5)return c;")&&fx.Contains("return c+_ViewCloth.rgb*(1.0-smoothstep(0.01,0.08,l));"),"the dark cloth's lift is off unless set, and only on near-black colours");
            Check(toon.Contains("  if(_VertexAlbedo<=0)o.Albedo=ViewCloth(o.Albedo);")&&toon.Contains("   if(_VertexAlbedo<=0)albedo=ViewCloth(albedo);"),"the body and its outline take the dark cloth's lift alike");
            Check(arms.Contains("ViewArmsCloth(bool on)")&&arms.Contains("Vector4 want=on?new Vector4(c.X,c.Y,c.Z,1):Vector4.zero;")&&pres.Contains("TacticalFill(arms);ViewArmsCloth(arms&&!on);"),"the view body's cloth is lifted while the eye-anchored arms draw, put back for the guns' tactical view");
            foreach(var f in Directory.GetFiles(runtime,"*.cs"))if(!f.EndsWith("NativeViewArmsPresentation.cs",StringComparison.Ordinal))Check(!File.ReadAllText(f).Contains("_ViewCloth"),Path.GetFileName(f)+" never sets the view body's cloth lift (third person and the guns keep their look)");
            Check(arms.Contains("if(d>reach){upper.position=a+to/d*(d-reach);a=upper.position;b=lower.position;to=target-a;d=to.magnitude;}"),"the arms' reach reads the elbow again once the shoulder moves (no hand left off its wrist)");
            Check(pres.Contains("if(d>reach){upper.position=a+to/d*(d-reach);a=upper.position;to=target-a;d=to.magnitude;}"),"the guns' own reach is as it was");
            string body=File.ReadAllText(Path.Combine(runtime,"NativeVisualBodyPresentation.cs"));
            Check(body.Replace("\r\n","\n").Contains("if(held!=null)VisualWeaponGrasp(ref grasp,held);\n            ViewArmsSeat(ref grasp,a);")&&arms.Contains("if(viewArmsFrame<Time.frameCount-1||ScopeActive||a.WeaponAway||chipView.Frame==Time.frameCount||!NativeViewArms.Eligible(a.SourceSpec))return;"),"the left fist closes on what it carries only while the eye-anchored arms draw (not the guns, a scope or the chip select's wrist)");
            foreach(var f in Directory.GetFiles(runtime,"*.cs"))if(!f.EndsWith("NativeTacticalViewPresentation.cs",StringComparison.Ordinal)&&!f.EndsWith("NativeViewArmsPresentation.cs",StringComparison.Ordinal))
                Check(!File.ReadAllText(f).Contains("ViewArmsPose("),Path.GetFileName(f)+" never poses the eye-anchored arms (third person is untouched)");
            Check(!Regex.IsMatch(arms,@"a\.[A-Z]\w*\s*(=[^=]|\+=|-=)"),"the arms write no pilot field");
            Check(arms.Contains("arms.Hand[1].SetPositionAndRotation(hr,qr);")&&arms.Contains("arms.Hand[0].SetPositionAndRotation(hl,ql);"),"the hands stay where the old view put them (the weapon, its trails and effects stay put)");
        }

        // The aimed recoil (owner 2026-11-23: "The pistol ADS still looks a bit small and simulates no recoil"; Core/NativeAimKick.cs): the
        // pack's recoil data played aimed: each shot visibly turns the gun and kicks it back, and the sights are back on the eye's axis (the
        // shots' line) by the time the next shot is allowed; the machine gun's burst climbs and settles once it stops; the pistol's slide
        // cycles; the camera shakes only aimed and within the camera-shake share; the rifle's scope kicks and settles before its bolt is
        // home; the hip is as it was. Presentation only.
        private static float SightError(NativeTacticalSolution s,Vector3 rear,Vector3 front)
        {
            float Ang(Vector3 p)=>(float)(Math.Atan2(Math.Sqrt(p.X*p.X+p.Y*p.Y),p.Z)*180/Math.PI);
            return Math.Max(Ang(s.Weapon+Vector3.Transform(rear,s.WeaponTurn)),Ang(s.Weapon+Vector3.Transform(front,s.WeaponTurn)));
        }
        private static float CameraAngle(NativeTacticalSolution s)=>(float)(2*Math.Acos(Math.Min(1,Math.Abs(s.Camera.W)))*180/Math.PI);
        private static void AimKick(NativeTacticalBank bank)
        {
            var report=new StringBuilder();
            // The pack's shapes, read as data: every one starts at rest and is at rest by its end; the targets are the pack's.
            foreach(var k in new[]{NativeAimKick.Pistol,NativeAimKick.MachineGun,NativeAimKick.Rifle})
            {
                string n=k.Weapon.ToString();
                foreach(var sh in new[]{k.RiseShape,k.YawShape,k.RollShape,k.BackShape})
                    Check(sh!=null&&sh.Length==k.RiseShape.Length&&sh[0]==0&&sh.All(MathUtil.Finite)&&sh.Max()<=1.02f&&NativeAimKick.Shape(sh,k.Span)==0&&NativeAimKick.Shape(sh,-1)==0,n+": a recoil shape starts and ends at rest");
                Check(k.Rise1>=k.Rise0&&k.Rise0>0&&k.Back1>=k.Back0&&k.Back0>0&&k.CameraShare>0&&k.CameraShare<=.5f,n+": the shot's rise and kick back, a share of the camera shake");
                Check(NativeAimKick.For(k.Weapon)==k,n+"'s aimed recoil");
            }
            Check(NativeAimKick.For(WeaponId.Sword)==null,"no aimed recoil for the sword");
            Check(NativeAimKick.Pistol.Rise0==9&&NativeAimKick.Pistol.Rise1==12&&NativeAimKick.Pistol.Back0==.014f&&NativeAimKick.Pistol.Rate==1.1f&&NativeAimKick.Pistol.SlideShape?.Length==5,"the Viper's own recoil (9-12 degrees, 14-16 mm, its slide)");
            Check(NativeAimKick.MachineGun.Auto&&NativeAimKick.MachineGun.ExtraRise==2.5f&&NativeAimKick.MachineGun.SmoothRise==30&&NativeAimKick.MachineGun.Pivot.Y==-.045f,"the TR15's own automatic recoil (smoothed, its pivot)");
            Check(!NativeAimKick.Pistol.Auto&&!NativeAimKick.Rifle.Auto&&NativeAimKick.Rifle.Rise0==1.8f&&NativeAimKick.Rifle.Back1==.015f,"the SRM-12's own recoil, aimed (x2 pitch, x10 kick)");
            // The pistol: aimed and settled, a shot turns it well off the axis, the slide cycles, and the sights are back on the axis by the
            // next shot the rule allows (its aimed interval); a string at that rate does the same every shot.
            {
                var g=NativeTacticalGun.Pistol;var v=new NativeTacticalView(bank);var s=new NativeTacticalSolution();Vector3 rear=g.ModelSight,front=g.ModelFront;
                var c=v.Calibrate(g,new Vector3(.01f,-.05f,-.03f),Quaternion.Identity,rear);c.Fronted=true;c.Front=front;
                var rule=GunRules.Get(WeaponId.Pistol);float interval=rule.Interval*rule.AimInterval;
                NativeTacticalInput In(float t,float last,bool aim=true,float shake=1)=>new NativeTacticalInput{Time=t,Weapon=WeaponId.Pistol,LastShot=last,ShootReady=last+interval,Aim=aim,Grounded=true,Shake=shake};
                float last=-100;for(int f=0;f<120;f++)v.Solve(In(f/60f,last),c,s);
                Check(Near(s.Ads,1)&&SightError(s,rear,front)<.01f,"aimed at rest the pistol's sights are on the axis");
                for(int n=0;n<5;n++)
                {
                    last=2+n*interval;float peak=0,slide=0,cam=0,back=0;
                    for(int f=0;f/60f<interval-1e-3f;f++){v.Solve(In(last+f/60f,last),c,s);peak=Math.Max(peak,SightError(s,rear,front));slide=Math.Max(slide,s.Slide);cam=Math.Max(cam,CameraAngle(s));back=Math.Max(back,s.KickBack);}
                    v.Solve(In(last+interval-1e-3f,last),c,s);float next=SightError(s,rear,front);
                    Check(peak>2,"shot "+n+": the aimed pistol visibly kicks (its sights "+peak+" degrees off at the peak)");
                    Check(back>.01f&&slide>.9f&&cam>.1f&&cam<2,"shot "+n+": it kicks back toward the eye ("+back+" m), the slide cycles, the camera shakes a little ("+cam+" degrees)");
                    Check(next<.05f,"shot "+n+": the sights are back on the aim ray by the next shot allowed ("+next+" degrees at "+interval+" s)");
                    if(n==0)report.Append("pistol: peak "+peak.ToString("0.00")+" deg off the axis, kick back "+(back*1000).ToString("0.0")+" mm, camera "+cam.ToString("0.00")+" deg, at the next shot ("+interval.ToString("0.000")+" s) "+next.ToString("0.000")+" deg; ");
                }
                // The hip is as it was: no aimed recoil, no camera shake; the slide still cycles.
                var hv=new NativeTacticalView(bank);var hs=new NativeTacticalSolution();var hc=hv.Calibrate(g,new Vector3(.01f,-.05f,-.03f),Quaternion.Identity,rear);
                float hipKick=0,hipCam=0,hipSlide=0;for(int f=0;f<120;f++){float t=f/60f;hv.Solve(In(t,t>=1?1:-100,aim:false),hc,hs);hipKick=Math.Max(hipKick,Math.Abs(hs.KickPitch)+hs.KickBack+Math.Abs(hs.CameraPitch));if(t<.9f)hipCam=Math.Max(hipCam,CameraAngle(hs));hipSlide=Math.Max(hipSlide,hs.Slide);}
                Check(hipKick==0&&hipCam==0&&hipSlide>.9f,"at the hip the aimed recoil and its camera shake stay out; the slide cycles");
                // Camera shake off: no camera turn from a shot.
                var ov=new NativeTacticalView(bank);var os=new NativeTacticalSolution();var oc=ov.Calibrate(g,new Vector3(.01f,-.05f,-.03f),Quaternion.Identity,rear);float offCam=0;
                for(int f=0;f<180;f++){float t=f/60f;ov.Solve(In(t,t>=2?2:-100,shake:0),oc,os);if(t>=2)offCam=Math.Max(offCam,CameraAngle(os));}
                Check(offCam<1e-3f,"with camera shake off the camera never turns ("+offCam+")");
                // A fresh view does not replay the shot already fired.
                var fv=new NativeTacticalView(bank);var fs=new NativeTacticalSolution();var fc=fv.Calibrate(g,new Vector3(.01f,-.05f,-.03f),Quaternion.Identity,rear);
                fv.Solve(new NativeTacticalInput{Time=5.01f,Weapon=WeaponId.Pistol,LastShot=5,ShootReady=5+interval,Grounded=true,Shake=1,Fresh=true},fc,fs);
                Check(fs.Slide==0&&fv.AimKick.Rise==0,"a fresh view does not replay the last shot");
            }
            // The machine gun: aimed, a burst at its rate climbs (capped), and once it stops the sights settle back on the axis quickly.
            {
                var g=NativeTacticalGun.MachineGun;var v=new NativeTacticalView(bank);var s=new NativeTacticalSolution();var rear=new Vector3(0,.0062f,.005f);var front=new Vector3(0,.011f,.384f);
                var c=v.Calibrate(g,new Vector3(.05f,-.13f,-.12f),Quaternion.Identity,rear);c.Fronted=true;c.Front=front;float interval=GunRules.Get(WeaponId.MachineGun).Interval;
                float last=-100;int f=0;
                for(;f<120;f++)v.Solve(new NativeTacticalInput{Time=f/60f,Weapon=WeaponId.MachineGun,LastShot=last,ShootReady=last+interval,Aim=true,Grounded=true,Shake=1},c,s);
                Check(SightError(s,rear,front)<.01f,"aimed at rest the machine gun's sights are on the axis");
                float start=2,peak=0,climb=0,settled=-1;int rounds=14;float end=start+(rounds-1)*interval;
                for(;f<60*4;f++)
                {
                    float t=f/60f;if(t>=start&&t<=end+1e-4f)last=start+(float)Math.Floor((t-start)/interval+1e-4f)*interval;
                    v.Solve(new NativeTacticalInput{Time=t,Weapon=WeaponId.MachineGun,LastShot=last,ShootReady=last+interval,Aim=true,Grounded=true,Shake=1},c,s);
                    float e=SightError(s,rear,front);if(t<=end+interval){peak=Math.Max(peak,e);climb=Math.Max(climb,s.Climb);settled=-1;}else if(e<.05f){if(settled<0)settled=t-end;}else settled=-1;
                }
                Check(peak>.8f&&climb>.3f&&climb<=NativeAimKick.MachineGun.ClimbMax+1e-4f,"a burst visibly kicks and climbs (its sights "+peak+" degrees off, the climb "+climb+", capped)");
                Check(settled>0&&settled<=interval+.2f,"once the burst stops the sights are back on the aim ray within about 0.15 s of the next round's time ("+settled+" s after the last round)");
                report.Append("machine gun: 14 rounds peak "+peak.ToString("0.00")+" deg, climb "+climb.ToString("0.00")+" deg, within 0.05 deg "+settled.ToString("0.000")+" s after the last round; ");
            }
            // The rifle's scope: the shot lifts the reticle, then it is back on the aim ray before the next shot is allowed.
            {
                var k=NativeAimKick.Rifle;float interval=GunRules.Get(WeaponId.Rifle).Interval;float peak=0;
                for(int f=0;f<70;f++){k.ScopeKick(3,f/60f,interval,out float r,out _,out _,out _);peak=Math.Max(peak,r);}
                k.ScopeKick(3,interval*NativeAimKick.Fit,interval,out float r1,out float y1,out float z1,out float b1);
                Check(peak>1.5f&&r1==0&&y1==0&&z1==0&&b1==0,"the scope kicks (its reticle "+peak+" degrees up) and is back on the aim ray before the bolt is home");
                k.ScopeKick(float.NaN,.1f,interval,out float r2,out _,out _,out _);k.ScopeKick(3,-.1f,interval,out float r3,out _,out _,out _);Check(r2==0&&r3==0,"no shot, no scope kick");
                report.Append("rifle scope: reticle up "+peak.ToString("0.00")+" deg, at rest by "+(interval*NativeAimKick.Fit).ToString("0.00")+" s");
            }
            Console.WriteLine("PASS aimed recoil (tactical view): "+report);
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
            // The held gun (owner 2026-11-22: "use whatever gun we are holding"): the game's model and skin, the support hand on its own
            // handguard by third person's grip, the pack guns no longer shipped, a wide sleeve ended at the elbow.
            Check(pres.Contains("bool pack=false;")&&pres.Contains("var grip=NativeTacticalMotion.For(spec)?.Support;")&&pres.Contains("gripAt=W(s.Weapon)+wq*(held.Support+"),"first person holds the game's own gun, the support hand on its handguard");
            Check(!File.ReadAllText(Path.Combine(root,"tools/pack_visual_body.py")).Contains("glob('ChipArenaViewGuns"),"the pack guns' prefab is not packed");
            Check(pres.Contains("TacticalIsWide(SkinnedMeshRenderer r)=>r.name.IndexOf(\"Kimono\"")&&pres.Contains("c.Shoulder=wide?TacticalSleeveCut(c.Skin,c.Source,arm):ArmMesh(")&&pres.Contains("private const float TacticalSleeveEnd=.78f;"),"a wide (robe) sleeve is cut cleanly at mid-forearm in first person, the forearm's skin below it");
            Check(NativeTacticalGun.Rifle.GripBack>0&&NativeTacticalGun.MachineGun.GripBack>0&&NativeTacticalGun.Pistol.GripBack==0&&pres.Contains("grip.Offset.Z-g.GripBack"),"the long guns' support hand sits back toward the receiver in first person");
            Check(Directory.GetFiles(Path.Combine(root,"Assets"),"ChipArenaViewGuns*",SearchOption.AllDirectories).All(f=>f.Replace('\\','/').Contains("/Assets/LocalAssets/")),"the pack guns are only ever a local asset");
            string weapon=File.ReadAllText(Path.Combine(runtime,"NativeVisualWeaponPresentation.cs")),bodyFile=File.ReadAllText(Path.Combine(runtime,"NativeVisualBodyPresentation.cs"));
            // The sights on the shots' line (owner 2026-11-23): the bake's SightFront read for the aim, the procedural layer still while aimed.
            Check(pres.Contains("TacticalFront(g,held,ref tacticalCal);")&&weapon.Contains("entry.Find(\"SightFront\")")&&File.ReadAllText(Path.Combine(core,"NativeTacticalView.cs")).Contains("gp+=addP*(1-AimSteady*ads)"),"first person lines the rear sight up with the bake's front post on the eye's axis");
            Check(weapon.Contains("if(rig==viewRig&&TacticalPackDrawn(viewBody))return true;")&&weapon.Contains("TacticalPackAfterDraw();"),"the pack gun replaces the model for the view rig alone, and hides when not drawn");
            foreach(var f in Directory.GetFiles(runtime,"*.cs"))if(!f.EndsWith("NativeVisualWeaponPresentation.cs",StringComparison.Ordinal)&&!f.EndsWith("NativeTacticalViewPresentation.cs",StringComparison.Ordinal))
                Check(!File.ReadAllText(f).Contains("TacticalPackDrawn("),Path.GetFileName(f)+" never draws the pack gun");
            // The pilot's arms: the skin under the garments hidden, the girth put back every frame, the fill light on the view model alone.
            Check(pres.Contains("if(!(under[tris[i]]&&under[tris[i+1]]&&under[tris[i+2]]))")&&pres.Contains("TacticalIsSkin(SkinnedMeshRenderer r)=>r.name.StartsWith(\"Body\""),"the body's skin under a garment is hidden in the tactical view");
            Check(pres.Contains("c.Skin.enabled=mesh!=null&&shown;")&&pres.Contains("bool shown=c.Shown||shoulders&&tacticalWide&&TacticalIsSkin(c.Skin)&&c.Source!=null&&c.Source.name==\"lowerarms\";")&&pres.Contains("Shown=skin.enabled"),"the arm cut never turns on a renderer the outfit switched off (the hologram proxy), but the forearm's skin below a wide sleeve");
            Check(pres.Contains("if(Twist[i][k]!=null)Twist[i][k].localScale=TwistScale[i][k];")&&pres.Contains("TacticalSlim(arms);"),"the slimmer forearms are the tactical view's alone (Rest puts them back every frame)");
            Check(pres.Contains("tacticalFill.cullingMask=1<<10;tacticalFill.shadows=LightShadows.None;"),"the fill light lights the view-model layer alone, without shadows");
            // The aimed recoil (owner 2026-11-23): presentation only. The slide moves the view body's own held model and is put back before
            // the view is posed again; the scope overlay reads its kick; the aimed recoil's code writes no pilot or input field.
            Check(pres.Replace("\r\n","\n").Contains("tacticalArms.Rest();\n            TacticalSlideRest();"),"the slide is back at rest before the view is posed");
            Check(pres.Contains("VisualWeaponFit(rh,held);TacticalSlidePose(held,s.Slide);}")&&pres.Contains("if(ScopeActive){tacticalScopedFrame=Time.frameCount;ScopeKickSeen(a,time);return false;}"),"the slide on the held model in first person; the scope sees its shot");
            string scope=File.ReadAllText(Path.Combine(runtime,"NativeScopePresentation.cs")),kick=File.ReadAllText(Path.Combine(core,"NativeAimKick.cs"));
            Check(scope.Contains("bool kicked=presentation.ScopeKick(out float kRise,out float kYaw,out float kRoll,out float kBack);")&&scope.Contains("GUI.matrix=matrix;"),"the scope overlay takes the shot's kick and puts its matrix back");
            Check(!Regex.IsMatch(kick,@"\b(a|i|input)\.[A-Z]\w*\s*(=[^=]|\+=|-=)")&&!kick.Contains("GunRules.")&&!kick.Contains("Actor "),"the aimed recoil reads and writes no rule or pilot state");
            Check(bodyFile.Contains("if(skin!=null)TacticalViewSkin(body,skin,skin.sharedMesh,cut);")&&pres.Contains("TacticalArmCut(arms);"),"the view body's arms are cut at the clavicles only while the tactical view or the eye-anchored arms draw");
        }
    }
}
#endif
