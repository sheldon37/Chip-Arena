#if UNITY_EDITOR || ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;

namespace ChipArena.UnityPrototype
{
    public static class RangedRegressionTests
    {
        private static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
        private static void Advance(ArenaWorld world,int ticks) { for(int i=0;i<ticks;i++) world.Step(new InputFrame(),new InputFrame()); }
        private static ArenaWorld Range(WeaponId weapon)
        {
            var w=new ArenaWorld(); w.Covers.Clear(); w.Find(1).Position=new V3(); w.AddHuman(2).Position=new V3(0,0,12);
            Assert(w.Equip(w.Find(1),weapon),"equip failed"); Advance(w,16); return w;
        }
        public static List<string> Run()
        {
            var passed=new List<string>();
            void Check(string name,Action test) { try { test(); passed.Add(name); } catch(Exception e) { throw new Exception(name+": "+e.Message,e); } }
            Check("All three gun definitions preserve source primary and ADS values",()=>{
                Assert(GunRules.All[1].Damage==23&&GunRules.All[2].Damage==58&&GunRules.All[3].Damage==8,"damage");
                Assert(GunRules.All[1].Magazine==7&&GunRules.All[2].Magazine==3&&GunRules.All[3].Magazine==48,"magazines");
                Assert(GunRules.All[1].Gravity==32&&GunRules.All[2].Speed==150&&GunRules.All[3].Interval==.085f,"ballistics/cadence");
            });
            Check("Gun magazines deplete at bounded fire cadence and empty triggers auto reload",()=>{
                foreach(var id in new[]{WeaponId.Pistol,WeaponId.Rifle,WeaponId.MachineGun})
                {
                    var w=Range(id);w.RemoveGuest();var a=w.Find(1);var gun=GunRules.Get(id);float previous=-100;
                    for(int i=0;i<900&&a.ReloadUntil==0;i++)
                    {
                        int count=a.ShotsFired;w.Step(new InputFrame { Shoot=true },new InputFrame());
                        if(a.ShotsFired!=count) { Assert(a.LastShot-previous>=gun.Interval-.00001f,"fire rate bypass");previous=a.LastShot; }
                    }
                    Assert(a.ShotsFired==gun.Magazine&&a.Ammo==0&&a.ReloadUntil>w.Time,"magazine/auto reload");
                }
            });
            Check("Reload cannot refill early or fire while incomplete",()=>{
                var w=Range(WeaponId.Pistol);var a=w.Find(1);w.Step(new InputFrame { Shoot=true },new InputFrame());
                w.Step(new InputFrame { Reload=true },new InputFrame());float end=a.ReloadUntil;int fired=a.ShotsFired;
                while(w.Time+Rules.Dt<end-.00001f) { w.Step(new InputFrame { Shoot=true,Reload=true },new InputFrame());Assert(a.Ammo==6&&a.ShotsFired==fired,"early refill/fire"); }
                w.Step(new InputFrame(),new InputFrame());Assert(a.Ammo==7&&a.ReloadUntil==0,"no refill");
            });
            Check("Switching preserves magazine and cannot bypass cooldown or reload",()=>{
                var w=Range(WeaponId.Rifle);var a=w.Find(1);w.Step(new InputFrame { Shoot=true },new InputFrame());
                Assert(!w.Equip(a,WeaponId.Pistol),"cooldown bypass");Advance(w,71);Assert(w.Equip(a,WeaponId.Pistol),"switch failed");
                Advance(w,16);Assert(w.Equip(a,WeaponId.Rifle)&&a.Ammo==2,"switch refilled gun");
                w.Step(new InputFrame { Reload=true },new InputFrame());Assert(!w.Equip(a,WeaponId.Sword),"reload bypass");
            });
            Check("ADS converges on a distant crosshair point despite gravity",()=>{
                foreach(var id in new[]{WeaponId.Pistol,WeaponId.Rifle,WeaponId.MachineGun})
                {
                    var w=Range(id);w.Find(2).Position=new V3(0,0,20);var a=w.Find(1);V3 point=new V3(0,1.05f,20);
                    for(int i=0;i<22;i++)w.Step(new InputFrame { Aim=true,AimTarget=point },new InputFrame());
                    w.Step(new InputFrame { Aim=true,Shoot=true,AimTarget=point },new InputFrame());Advance(w,45);
                    Assert(w.Find(2).Hp<100,"ADS missed "+id);Assert(w.Events.Exists(e=>e.Kind==EventKind.Hit),"no hit receipt");
                }
            });
            Check("Ballistic integration retains gravity and cannot tunnel through thin cover",()=>{
                var w=Range(WeaponId.Rifle);w.RemoveGuest();w.Step(new InputFrame { Shoot=true },new InputFrame());
                var shot=w.Shots[0];float vy=shot.Velocity.y,y=shot.Position.y;w.Step(new InputFrame(),new InputFrame());
                Assert(Math.Abs(shot.Velocity.y-(vy-12*Rules.Dt))<.0001f,"gravity");
                Assert(Math.Abs(shot.Position.y-(y+vy*Rules.Dt-.5f*12*Rules.Dt*Rules.Dt))<.0001f,"ballistic displacement");
                w=Range(WeaponId.Rifle);w.Covers.Add(new Cover { Id=99,Position=new V3(0,0,3),HalfX=2,HalfZ=.025f,Height=3,Hp=100 });
                w.Step(new InputFrame { Shoot=true,Aim=true,AimTarget=new V3(0,1.05f,12) },new InputFrame());Advance(w,12);
                Assert(w.Find(2).Hp==100&&w.Covers[0].Hp==42,"thin cover tunnelling");
            });
            Check("Muzzle overlapping cover cannot shoot past it",()=>{
                var w=Range(WeaponId.Rifle);w.Covers.Add(new Cover { Id=1,Position=new V3(.2f,0,.44f),HalfX=.04f,HalfZ=.025f,Height=3,Hp=100 });
                w.Step(new InputFrame { Shoot=true,AimTarget=new V3(0,1,12) },new InputFrame());Advance(w,20);
                Assert(w.Covers[0].Hp==42&&w.Find(2).Hp==100,"muzzle cover bypass");
            });
            Check("Gun headshot uses 1.5 multiplier and body hits retain base damage",()=>{
                var w=Range(WeaponId.Rifle);var a=w.Find(1);V3 point=new V3(0,1.51f,12);
                for(int i=0;i<22;i++) w.Step(new InputFrame { Aim=true,AimTarget=point },new InputFrame());
                w.Step(new InputFrame { Aim=true,Shoot=true,AimTarget=point },new InputFrame());Advance(w,10);
                Assert(w.Find(2).Hp==13&&w.Events.Exists(e=>e.Kind==EventKind.Headshot&&e.Amount==87),"headshot");
                w=Range(WeaponId.Rifle);point=new V3(0,.85f,12);
                for(int i=0;i<22;i++) w.Step(new InputFrame { Aim=true,AimTarget=point },new InputFrame());
                w.Step(new InputFrame { Aim=true,Shoot=true,AimTarget=point },new InputFrame());Advance(w,10);Assert(w.Find(2).Hp==42,"body damage");
            });
            Check("ADS slows movement, tightens spread and exits for reload or roll",()=>{
                var w=Range(WeaponId.Pistol);var a=w.Find(1);
                for(int i=0;i<35;i++) w.Step(new InputFrame { X=1,Aim=true,Sprint=true },new InputFrame());
                Assert(a.Aiming&&!a.Sprinting&&Math.Abs(a.Velocity.x-Rules.Speed*.75f)<.001f,"aim movement");
                Assert(Math.Abs(GunRules.Spread(a,w.Time)-.002f)<.00001f,"settled spread");
                w.Step(new InputFrame { Shoot=true },new InputFrame());w.Step(new InputFrame { Aim=true,Reload=true },new InputFrame());Assert(!a.Aiming,"reload ADS");
                Advance(w,70);w.Step(new InputFrame { Aim=true,Roll=true },new InputFrame());Assert(!a.Aiming&&a.Rolling(w.Time),"roll ADS");
            });
            Check("Ranged inputs and state survive the versioned wire protocol",()=>{
                var i=WireCodec.ReadInput(WireCodec.Input(new InputFrame { Sequence=2,Yaw=24,Pitch=-12,Aim=true,Reload=true,SelectWeapon=4,AimTarget=new V3(3,2,10) }));
                Assert(i.Aim&&i.Reload&&i.SelectWeapon==4&&i.Pitch==-12&&i.AimTarget.z==10,"ranged input lost");
                var w=Range(WeaponId.Pistol);w.Step(new InputFrame { Aim=true,Shoot=true },new InputFrame());
                var state=WireCodec.ReadState(WireCodec.State(w.Capture()));var a=state.Actors[0];
                Assert(a.Weapon==WeaponId.Pistol&&a.Ammo==6&&a.ShotsFired==1&&a.Aiming&&a.LastShot==w.Find(1).LastShot,"ranged state lost");
                Assert(state.Shots[0].Gravity==32&&state.Shots[0].Weapon==WeaponId.Pistol,"shot data lost");
                byte[] old=WireCodec.Input(new InputFrame());old[0]=0x31;bool rejected=false;
                try { WireCodec.ReadInput(old); }catch(InvalidDataException){rejected=true;}Assert(rejected,"CAP1 accepted");
                rejected=false;try{WireCodec.ReadInput(WireCodec.Input(new InputFrame { SelectWeapon=99 }));}catch(InvalidDataException){rejected=true;}Assert(rejected,"invalid selection accepted");
            });
            Check("Ranged bot workload and packets stay bounded",()=>{
                var w=new ArenaWorld { RangedBots=true };w.SetBots(192);
                for(int i=0;i<600;i++) { w.Step(new InputFrame(),new InputFrame());if(i%60==0) WireCodec.ReadState(WireCodec.State(w.Capture())); }
                Assert(w.Shots.Count<=Rules.MaxProjectiles,"projectile budget");Assert(w.Actors.Exists(a=>a.Bot&&a.ShotsFired>0),"bots never fired");
            });
            Check("Ranged pose aims the barrel and preserves articulated bones across reload",()=>{
                var p=new CapturedRig.Pose();var row=new float[64]; int samples=0;
                foreach(var id in new[]{WeaponId.Pistol,WeaponId.Rifle,WeaponId.MachineGun}) foreach(float pitch in new[]{-50f,0f,50f}) for(int i=0;i<=100;i++)
                {
                    var a=new Actor { Weapon=id,Pitch=pitch,AimYaw=20,LastShot=-100 };
                    if(i>0) { a.ReloadAt=0;a.ReloadUntil=GunRules.Get(id).Reload; }
                    float time=i/100f*GunRules.Get(id).Reload;
                    CapturedClips.Sets[0][0].Sample(time,row);CapturedRig.Sample(row,0,p);RangedPose.Apply(p,a,time,1);samples++;
                    for(int b=0;b<CapturedRig.Bones.Length;b++)Assert(Math.Abs(p.Rotation[b].Length()-1)<.001f&&p.WorldPosition[b].Length()<4,"invalid ranged joint");
                    if(i==0)
                    {
                        Vector3 barrel=CapturedRig.Mirror(Vector3.Transform(Vector3.UnitZ,p.WorldRotation[CapturedRig.RHand]));V3 want=GunRules.Direction(20,pitch);
                        Assert(Vector3.Dot(barrel,new Vector3(want.x,want.y,want.z))>.9999f,"barrel aim differs from sight");
                    }
                }
                Assert(samples==909,"pose count");
            });
            Check("Real LAN carries guest loadout, fire, ammo and reload",()=>{
                var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();
                int port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
                using(var host=LanLink.Host(port,true)) using(var guest=LanLink.Join("127.0.0.1",port))
                {
                    Wait(()=>host.Connected&&guest.Connected);var w=new ArenaWorld();w.Covers.Clear();w.AddHuman(2);
                    guest.Send(WireCodec.Input(new InputFrame { Sequence=1,SelectWeapon=3 }));byte[] input=null;Wait(()=> (input=host.Receive())!=null);
                    w.Step(new InputFrame(),WireCodec.ReadInput(input));Advance(w,16);
                    guest.Send(WireCodec.Input(new InputFrame { Sequence=2,Shoot=true,Yaw=180,AimTarget=new V3(3,1,-20) }));Wait(()=> (input=host.Receive())!=null);
                    w.Step(new InputFrame(),WireCodec.ReadInput(input));host.Send(WireCodec.State(w.Capture()));byte[] packet=null;Wait(()=> (packet=guest.Receive())!=null);
                    var state=WireCodec.ReadState(packet);Actor remote=Array.Find(state.Actors,a=>a.Id==2);
                    Assert(remote.Weapon==WeaponId.Rifle&&remote.Ammo==2&&remote.ShotsFired==1,"guest firing state");
                    guest.Send(WireCodec.Input(new InputFrame { Sequence=3,Reload=true }));Wait(()=> (input=host.Receive())!=null);
                    w.Step(new InputFrame(),WireCodec.ReadInput(input));host.Send(WireCodec.State(w.Capture()));Wait(()=> (packet=guest.Receive())!=null);
                    remote=Array.Find(WireCodec.ReadState(packet).Actors,a=>a.Id==2);Assert(remote.ReloadUntil>w.Time,"guest reload state");
                }
            });
            return passed;
        }
        private static void Wait(Func<bool> condition) { var end=DateTime.UtcNow.AddSeconds(3);while(!condition()){if(DateTime.UtcNow>end)throw new Exception("LAN timeout");Thread.Sleep(5);} }
    }
}
#endif
