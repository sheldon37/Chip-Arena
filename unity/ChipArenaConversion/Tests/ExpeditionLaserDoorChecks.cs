#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChipArena.UnityConversion
{
    // The instance exit's laser door (Claude, 2026-10-17; docs/EXPEDITION-PRESENTATION.md "The laser door"): the phase mapping
    // (stage, beam levels, charge, embers), the live split-area director driving each of the three instances' exits (sealed from
    // the first committed cohort, the lock from authority time, the unlock from the clear's `began`), beams and field sheet
    // standing exactly while the closure blocks, teammates' re-seal pulse, the DEV preview, the tiers, the hub's state pieces
    // untouched (a digest pinned from the base code), no allocation per frame and source scans (no collider, rule or randomness).
    internal static class ExpeditionLaserDoorChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Laser door: "+what);count++;}
        private static ExpeditionClosureView C(string state,double began,string id="d",string area="a")=>new ExpeditionClosureView{id=id,area=area,state=state,began=began};

        public static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};
            var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
            var layout=NativeExpeditionLayout.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json")));
            Mapping();Beams();Live(content,layout);Pulse(content,layout);Hub(layout);Preview(content,layout);Tiers(content,layout);NoAllocation(content,layout);Sources(root);
            Console.WriteLine("PASS expedition laser door ("+count+" checks: the stage from the closure and the arrival; beams fired floor-up, put out top-down, the floor beam never out while it blocks; the live split director seals each exit at its first committed arrival and opens it on the clear; beams and sheet exactly while blocking in Ward Zero, Central Station and City Ironworks; the zone palettes; teammates' re-seal; the DEV preview; low and reduced tiers; the hub's doors, gate and sanctuary byte-for-byte the base's; no per-frame allocation; no collider, rule or randomness)");
        }

        // 1. The stage: sealing and opening follow `began` exactly; a sealed exit plays the arrival lock once; unknown states are sealed.
        private static void Mapping()
        {
            double lockAt=50.5,arrived=50;
            var st=NativeExpeditionLaserDoor.Stage(C("sealing",10),10.4,double.NaN,double.NaN,out double age);Check(st==ExpeditionLaserStage.Lock&&Math.Abs(age-.4)<1e-9,"sealing is the lock, timed from `began`");
            st=NativeExpeditionLaserDoor.Stage(C("sealing",10),12,double.NaN,double.NaN,out age);Check(st==ExpeditionLaserStage.Lock&&age==NativeExpeditionLaserDoor.LockSeconds,"a sealing state past its second stays the lock's end until the view says sealed");
            st=NativeExpeditionLaserDoor.Stage(C("opening",10),10.25,double.NaN,double.NaN,out age);Check(st==ExpeditionLaserStage.Unlock&&Math.Abs(age-.25)<1e-9,"opening is the unlock, timed from `began`");
            st=NativeExpeditionLaserDoor.Stage(C("open",10),11,lockAt,arrived,out age);Check(st==ExpeditionLaserStage.Open&&Math.Abs(age-1)<1e-9,"open, with its age since it opened");
            st=NativeExpeditionLaserDoor.Stage(C("sealed",0),60,double.NaN,double.NaN,out _);Check(st==ExpeditionLaserStage.Sealed,"sealed without an arrival: steady");
            st=NativeExpeditionLaserDoor.Stage(C("sealed",0),60,double.PositiveInfinity,double.PositiveInfinity,out age);Check(st==ExpeditionLaserStage.Lock&&age<0,"in the area but not presented yet: the lock stands by");
            st=NativeExpeditionLaserDoor.Stage(C("sealed",0),50.2,lockAt,arrived,out age);Check(st==ExpeditionLaserStage.Lock&&Math.Abs(age+.3)<1e-9,"arrived: standing by until ArriveDelay after the arrival");
            st=NativeExpeditionLaserDoor.Stage(C("sealed",0),51,lockAt,arrived,out age);Check(st==ExpeditionLaserStage.Lock&&Math.Abs(age-.5)<1e-9,"the arrival lock plays from lockAt");
            st=NativeExpeditionLaserDoor.Stage(C("sealed",0),51.6,lockAt,arrived,out _);Check(st==ExpeditionLaserStage.Sealed,"then the steady hum");
            st=NativeExpeditionLaserDoor.Stage(C("sealed",51),51.2,lockAt,arrived,out _);Check(st==ExpeditionLaserStage.Sealed,"a door this client saw sealing is not locked again");
            st=NativeExpeditionLaserDoor.Stage(C("tampered",0),60,double.NaN,double.NaN,out _);Check(st==ExpeditionLaserStage.Sealed&&NativeExpeditionStates.Blocking(C("tampered",0)),"an unknown state reads sealed, as the collider blocks");
            Check(NativeExpeditionLaserDoor.LockSeconds==NativeExpeditionStates.TransitionSeconds,"the lock and unlock take the director's one-second transition");
            for(int n=1;n<=9;n+=4)for(int i=1;i<n;i++)
            {
                Check(NativeExpeditionLaserDoor.Height(i,n,3.4f)>NativeExpeditionLaserDoor.Height(i-1,n,3.4f)&&NativeExpeditionLaserDoor.Height(i,n,3.4f)<3.4f,"beams climb inside the doorway ("+n+")");
                Check(NativeExpeditionLaserDoor.FireAt(i,n)>NativeExpeditionLaserDoor.FireAt(i-1,n)&&NativeExpeditionLaserDoor.OffAt(i,n)<NativeExpeditionLaserDoor.OffAt(i-1,n),"fired floor-up and put out top-down ("+n+")");
            }
            Check(NativeExpeditionLaserDoor.FireAt(0,9)==0&&Math.Abs(NativeExpeditionLaserDoor.FireAt(8,9)-NativeExpeditionLaserDoor.ChargeEnd)<1e-6&&Math.Abs(NativeExpeditionLaserDoor.OffAt(0,9)-1)<1e-9&&NativeExpeditionLaserDoor.OffAt(8,9)>NativeExpeditionLaserDoor.UnlockStutter,"the floor beam fires first and goes out last, at the end of the opening second");
            Check(NativeExpeditionLaserDoor.Count(false)==9&&NativeExpeditionLaserDoor.Count(true)==5,"nine beams, five on low graphics");
        }

        // 2. Beam levels: lit exactly while the stage blocks; the floor beam is never out until the view says open.
        private static void Beams()
        {
            foreach(bool reduced in new[]{false,true})foreach(int n in new[]{9,5})
            {
                for(int i=0;i<n;i++){Check(NativeExpeditionLaserDoor.Level(ExpeditionLaserStage.Open,.3,i,n,10,7,reduced,out _,out _)==0,"open: no beam");Check(NativeExpeditionLaserDoor.Level(ExpeditionLaserStage.Sealed,.3,i,n,10,7,reduced,out _,out _)==1,"sealed: every beam");}
                for(double a=-1;a<=1.0001;a+=.01)
                {
                    int lit=0;
                    for(int i=0;i<n;i++)
                    {
                        float lv=NativeExpeditionLaserDoor.Level(ExpeditionLaserStage.Lock,a,i,n,10+a,7,reduced,out float flash,out float reach);
                        if(i==0)Check(lv>=NativeExpeditionLaserDoor.StandbyFloor-1e-6f&&reach==1,"lock: the floor beam stands from the first frame (a "+a.ToString("0.00")+")");
                        else Check((lv>0)==(a>=NativeExpeditionLaserDoor.FireAt(i,n)),"lock: beam "+i+" lit exactly from its firing (a "+a.ToString("0.00")+")");
                        if(reduced)Check(flash==0&&(lv<=0||reach==1),"reduced: no flash or travel");if(lv>0)lit++;
                    }
                    for(int i=0;i<lit;i++)Check(NativeExpeditionLaserDoor.Level(ExpeditionLaserStage.Lock,a,i,n,10+a,7,reduced,out _,out _)>0,"lock: the lit beams are the lowest ones (floor-up)");
                }
                float last=-1;for(double a=-.2;a<=1;a+=.02){float c=NativeExpeditionLaserDoor.Charge(ExpeditionLaserStage.Lock,a);Check(c>=last&&c>=0&&c<=1,"lock: the charge climbs");last=c;}
                last=2;for(double a=0;a<=1;a+=.02){float c=NativeExpeditionLaserDoor.Charge(ExpeditionLaserStage.Unlock,a);Check(c<=last&&c>=0&&c<=1,"unlock: the charge drains");last=c;}
                for(double a=0;a<=1.0001;a+=.005)for(int i=0;i<n;i++)
                {
                    float lv=NativeExpeditionLaserDoor.Level(ExpeditionLaserStage.Unlock,a,i,n,40+a,7,reduced,out _,out _);double off=NativeExpeditionLaserDoor.OffAt(i,n);
                    if(i==0)Check(lv>=NativeExpeditionLaserDoor.StutterFloor-1e-6f,"unlock: the floor beam holds through the opening second (a "+a.ToString("0.000")+")");
                    else if(a>=off)Check(lv==0,"unlock: beam "+i+" out from its time (top-down)");
                    else if(reduced||a>=NativeExpeditionLaserDoor.UnlockStutter*NativeExpeditionLaserDoor.LockSeconds&&a<off-NativeExpeditionLaserDoor.BeamStutter)Check(lv>0,"unlock: beam "+i+" lit before it goes");
                    float em=NativeExpeditionLaserDoor.Ember(ExpeditionLaserStage.Unlock,a,i,n);Check((em>0)==(a>=off),"unlock: an emitter cools only once its beam is out");
                }
                Check(NativeExpeditionLaserDoor.Ember(ExpeditionLaserStage.Open,.1,0,n)>0&&NativeExpeditionLaserDoor.Ember(ExpeditionLaserStage.Open,NativeExpeditionLaserDoor.CoolSeconds+.1,0,n)==0,"open: the emitters cool down, then go dark");
            }
            for(double t=0;t<6;t+=.013){float h=NativeExpeditionLaserDoor.Hum(3,9,t,7,false);Check(h>.45f&&h<1.6f,"the hum stays a hum");}
            Check(NativeExpeditionLaserDoor.Hum(3,9,1.234,7,true)==1&&NativeExpeditionLaserDoor.Pulse(-.1,0,9)==0&&NativeExpeditionLaserDoor.Pulse(NativeExpeditionLaserDoor.PulseSeconds,0,9)==0&&NativeExpeditionLaserDoor.Pulse(.05,0,9)>.1f,"reduced effects: a steady hum; the pulse only inside its window");
            Check(NativeExpeditionLaserDoor.Word(ExpeditionLaserStage.Lock,.1)==NativeExpeditionLaserDoor.WordLocking&&NativeExpeditionLaserDoor.Word(ExpeditionLaserStage.Lock,.9)==NativeExpeditionLaserDoor.WordSealed&&NativeExpeditionLaserDoor.Word(ExpeditionLaserStage.Unlock,.5)==NativeExpeditionLaserDoor.WordUnlocking&&NativeExpeditionLaserDoor.Word(ExpeditionLaserStage.Open,0)==NativeExpeditionLaserDoor.WordOpen,"LOCKING, SEALED, UNLOCKING, OPEN");
            Check(NativeExpeditionLaserDoor.WordCells.All(w=>w.Length>0&&w.Length%4==0),"the words' glyphs are cached");
        }

        // ---- the live split-area run (the activation's director, one area session per instance) ----
        private sealed class Split
        {
            public NativeExpedition Run;public ExpeditionLayout Layout;
            public readonly Dictionary<string,ExpeditionSession> Sessions=new Dictionary<string,ExpeditionSession>();public readonly Dictionary<string,ExpeditionDirector> Directors=new Dictionary<string,ExpeditionDirector>();
            public void Tick(double dt){Sessions[Layout.world].Tick(dt);foreach(var id in Layout.instances)Sessions[id].Tick(dt);}
            /// <summary>The area's projection as the runtime builds it (ConversionGame.AreaView): its own closures only.</summary>
            public ExpeditionView View(string area,string who="host"){var v=NativeExpeditionView.Build(Sessions[area].Run,who,Sessions[area].Sources);v.closures=v.closures.Where(c=>c.area==area).ToArray();return v;}
            public ExpeditionClosureView Exit(string area)=>View(area).closures.Single(c=>c.id==area+":door:exit");
        }
        private static Split Start(ConversionContent content,ExpeditionLayout layout)
        {
            var p=new Split{Layout=layout,Run=NativeExpedition.Create("laser-door",layout.world,"host","pistol",7,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=1}).ToArray())};
            Check(p.Run.Join("guest","pistol"),"a guest joins");p.Run.EnableAreas();
            foreach(var a in layout.areas){var s=new ExpeditionSession(p.Run.ForArea(a.id),content);p.Sessions[a.id]=s;p.Directors[a.id]=new ExpeditionDirector(s,layout,_=>null,(_,__)=>true,10,20,30);}
            return p;
        }
        private static ExpeditionEye EyeAt(in ExpeditionFrame f,float metres)
        {
            f.Point(0,1.6f,metres,out float x,out float y,out float z);float fx=f.Cx-x,fy=f.Cy+1.6f-y,fz=f.Cz-z,l=(float)Math.Sqrt(fx*fx+fy*fy+fz*fz);fx/=l;fy/=l;fz/=l;
            float rx=fz,rz=-fx,rl=(float)Math.Sqrt(rx*rx+rz*rz);rx/=rl;rz/=rl;return new ExpeditionEye{X=x,Y=y,Z=z,Rx=rx,Rz=rz,Ux=fy*rz,Uy=fz*rx-fx*rz,Uz=-fy*rx};
        }
        /// <summary>The field sheet: one premultiplied quad whose corners are exactly the closure face at the seal depth.</summary>
        private static bool SealUp(NativeExpeditionStateScene s,ExpeditionFrame f)
        {
            float d=NativeExpeditionStateScene.SealDepth(f),h=f.W/2;var want=new float[4][];
            f.Point(-h,0,d,out float ax,out float ay,out float az);f.Point(h,0,d,out float bx,out float by,out float bz);f.Point(h,f.H,d,out float cx,out float cy,out float cz);f.Point(-h,f.H,d,out float dx,out float dy,out float dz);
            want[0]=new[]{ax,ay,az};want[1]=new[]{bx,by,bz};want[2]=new[]{cx,cy,cz};want[3]=new[]{dx,dy,dz};var q=s.Quads;
            for(int i=0;i<q.Count;i++){if(q.Additive[i]!=0)continue;bool same=true;for(int k=0;k<4&&same;k++)for(int j=0;j<3;j++)if(Math.Abs(q.Corners[i*12+k*3+j]-want[k][j])>1e-3f)same=false;if(same)return true;}
            return false;
        }
        private static void Blocks(NativeExpeditionStateScene s,ExpeditionFrame f,bool blocking,string when)
        {
            Check(s.LaserDrawn&&s.DoorsDrawn==1,"the exit draws ("+when+")");
            Check((s.LaserBeamsLit>0)==blocking&&s.LaserSheet==blocking&&SealUp(s,f)==blocking,(blocking?"blocking: beams and the field sheet over exactly the closure face":"open: no beam, no sheet")+" ("+when+")");
        }

        // 3. Each live instance through the real split director: sealed behind the party from the run's start, the lock from this
        // client's arrival, the clear opens it over one second, the beams follow the closure exactly.
        private static void Live(ConversionContent content,ExpeditionLayout layout)
        {
            Check(layout.instances.Length==3&&layout.instances.SequenceEqual(new[]{NativeExpeditionLaserDoor.WardZero,NativeExpeditionLaserDoor.Station,NativeExpeditionLaserDoor.Ironworks}),"the three live instances");
            var p=Start(content,layout);var palettes=new HashSet<int>();
            foreach(var id in layout.instances)
            {
                var area=layout.Area(id);var rig=ExpeditionStateRig.For(layout,id);var door=rig.Doors.Single();var f=door.Frame;var eye=EyeAt(f,4.5f);
                Check(area.role=="instance"&&door.Exit&&door.Look==ExpeditionDoorLook.Laser&&door.Anchor==id+":door:exit"&&area.doors.Single().entries.Length>0,id+": its one exit is the laser door, the party placed at its entries");
                foreach(var e in area.doors.Single().entries){float side=(e.x-f.Cx)*f.Fx+(e.z-f.Cz)*f.Fz,face=(float)(Math.Sin(e.yaw*Math.PI/180)*f.Fx+Math.Cos(e.yaw*Math.PI/180)*f.Fz);Check(side>.5f&&face>.9f,id+": the party arrives in front of the exit, facing away from it (the door behind us)");}
                var exit=p.Exit(id);Check(exit.state=="open"&&!exit.Blocking,id+": an unused instance exit starts open");
                Check(p.View(layout.world).closures.Single(c=>c.id==layout.world+":door:"+id).state=="open",id+": the hub's door to it stays open (the split hub never seals it)");
                Check(p.Run.MoveMembers(layout.world,id,new[]{"host","guest"}),id+": the party transfers in");p.Tick(.25);
                var v=p.View(id);exit=v.closures.Single(c=>c.id==door.Anchor);Check(v.area==id&&exit.state=="sealing"&&exit.began==v.elapsed,id+": committed arrival starts authority sealing");
                // This client arrives (the runtime holds the arrival while its loading screen is up).
                var cues=new ExpeditionStateCues();var hub=p.View(layout.world);cues.Observe(hub,v.elapsed-1);
                cues.ArrivalHeld=true;cues.Observe(v,v.elapsed);Check(double.IsPositiveInfinity(cues.LockAt(id))&&double.IsNaN(cues.LockAt(layout.world)),id+": held behind the loading screen, the lock waits");
                var scene=new NativeExpeditionStateScene();scene.Build(v,rig,cues,v.elapsed,eye,false,false);Blocks(scene,f,true,id+" waiting");Check(scene.LaserStage==ExpeditionLaserStage.Lock&&scene.LaserBeamsLit==1,id+": standing by: the floor beam and the sheet only");
                double sealStart=exit.began;double arrive=v.elapsed+.2;cues.ArrivalHeld=false;cues.Observe(v,arrive);Check(cues.ArrivedAt(id)==arrive&&cues.LockAt(id)==arrive+NativeExpeditionStates.ArriveDelay,id+": presented: the lock starts ArriveDelay after the arrival");
                int lastLit=0;
                for(double dt=.025;dt<=2.6;dt+=.05)
                {
                    p.Tick(.05);v=p.View(id);double t=v.elapsed,age=t-sealStart;scene.Build(v,rig,cues,t,eye,false,false);Blocks(scene,f,true,id+" arrival +"+dt.ToString("0.00"));
                    int want=age<0?1:age>=NativeExpeditionLaserDoor.LockSeconds?NativeExpeditionLaserDoor.Beams:Enumerable.Range(0,NativeExpeditionLaserDoor.Beams).Count(i=>NativeExpeditionLaserDoor.FireAt(i,NativeExpeditionLaserDoor.Beams)<=age);
                    Check(scene.LaserBeamsLit==want&&scene.LaserBeamsLit>=lastLit,id+": the beams fire one after another floor-up (+"+dt.ToString("0.00")+": "+scene.LaserBeamsLit+" of "+want+")");lastLit=scene.LaserBeamsLit;
                    Check(scene.LaserStage==(age<NativeExpeditionLaserDoor.LockSeconds?ExpeditionLaserStage.Lock:ExpeditionLaserStage.Sealed)&&Math.Abs(scene.LaserLockStart-sealStart)<1e-7,id+": lock, then sealed on the authority clock");
                }
                var pal=NativeExpeditionLaserDoor.Palette(id);Check(palettes.Add(pal.Beam)&&scene.Parts.Any(b=>b.Hex==pal.Beam&&b.Look==NativeCyberCityLooks.Tube),id+": the beams in the zone's own colour");
                Check(!scene.Parts.Any(b=>Math.Abs((b.X-f.Cx)*f.Ux+(b.Z-f.Cz)*f.Uz)+b.Hx<f.W/2-1e-3f&&Math.Abs((b.X-f.Cx)*f.Fx+(b.Z-f.Cz)*f.Fz)<f.D/2&&b.Y-b.Hy>f.Cy+.05f&&b.Y+b.Hy<f.Cy+f.H),id+": nothing solid stands inside the closure face");
                // The clear: the director opens the exit over one second; the beams go out top-down, the floor beam with the state.
                Check(p.Sessions[id].Run.Progress("kill",1)&&p.Sessions[id].Run.StartBosses(new[]{"TEST-boss"})&&p.Directors[id].BossDefeated("TEST-boss",new V3(0,0,0)),id+": cleared");p.Directors[id].Step();
                v=p.View(id);exit=v.closures.Single(c=>c.id==door.Anchor);Check(exit.state=="opening",id+": the clear opens the exit");double began=exit.began;cues.Observe(v,began);
                lastLit=99;
                for(double dt=0;dt<1;dt+=.05)
                {
                    scene.Build(v,rig,cues,began+dt,eye,true,false);Blocks(scene,f,true,id+" opening +"+dt.ToString("0.00"));Check(scene.LaserStage==ExpeditionLaserStage.Unlock&&scene.LaserUnlockStart==began&&scene.LaserBeamsLit<=lastLit,id+": the unlock follows `began`, top-down");lastLit=scene.LaserBeamsLit;
                    scene.Build(v,rig,cues,began+dt,eye,false,false);Blocks(scene,f,true,id+" opening (stutter) +"+dt.ToString("0.00"));
                }
                scene.Build(v,rig,cues,began+1.4,eye,false,false);Blocks(scene,f,true,id+": the view still says opening past its second");
                for(int i=0;i<4;i++)p.Tick(.25);v=p.View(id);exit=v.closures.Single(c=>c.id==door.Anchor);Check(exit.state=="open"&&!exit.Blocking,id+": open after one second");cues.Observe(v,v.elapsed);
                scene.Build(v,rig,cues,v.elapsed,eye,false,false);Blocks(scene,f,false,id+" open");Check(scene.LaserStage==ExpeditionLaserStage.Open&&scene.LaserUnlockStart==exit.began-1,id+": open the moment the collider releases");
                scene.Build(v,rig,cues,v.elapsed+10,eye,false,false);Blocks(scene,f,false,id+" long open");
                Check(p.Run.MoveMembers(id,layout.world,new[]{"host","guest"}),id+": back to the hub");p.Tick(.25);
            }
        }

        // 4. A teammate arriving well after this client re-seals the door behind them; the party arriving together does not.
        private static void Pulse(ConversionContent content,ExpeditionLayout layout)
        {
            var p=Start(content,layout);string id=layout.instances[0];var rig=ExpeditionStateRig.For(layout,id);var f=rig.Doors[0].Frame;var eye=EyeAt(f,4);
            Check(p.Run.MoveMembers(layout.world,id,new[]{"host"}),"the host goes first");p.Tick(.25);
            // The projection lists every member with where it is (ConversionGame.AreaView builds members[] from the cohorts).
            ExpeditionView V(string guestArea,string pending){var x=p.View(id);x.members=new[]{new ExpeditionViewMember{account="host",area=id,connected=true,alive=true,seat=1},new ExpeditionViewMember{account="guest",area=guestArea,pendingArea=pending,connected=true,alive=true,seat=2}};return x;}
            var cues=new ExpeditionStateCues();double t=p.View(id).elapsed;cues.Observe(V(layout.world,null),t);Check(!cues.DoorPulse(id,t,out _),"the first sight of a teammate records it");
            cues.Observe(V(layout.world,id),t+.5);Check(!cues.DoorPulse(id,t+.5,out _),"a teammate still loading has not arrived");
            cues.Observe(V(id,null),t+.8);Check(!cues.DoorPulse(id,t+.8,out _),"arriving with the party (inside its lock) is not a re-seal");
            cues.Observe(V(layout.world,null),t+1);
            for(int tick=0;tick<24;tick++)p.Tick(.25);var late=V(id,null);double at=t+6;cues.Observe(late,at);
            Check(cues.DoorPulse(id,at+.1,out double age)&&Math.Abs(age-.1)<1e-9&&!cues.DoorPulse(id,at+NativeExpeditionLaserDoor.PulseSeconds,out _)&&!cues.DoorPulse(layout.world,at+.1,out _),"a teammate arriving later: the re-seal pulse, in this area only");
            var scene=new NativeExpeditionStateScene();scene.Build(late,rig,cues,at+.1,eye,false,false);Blocks(scene,f,true,"re-seal");Check(scene.LaserStage==ExpeditionLaserStage.Sealed&&scene.LaserBeamsLit==NativeExpeditionLaserDoor.Beams,"the re-seal never darkens a beam");
        }

        // 5. The hub: its doors, gate and sanctuary are exactly the base code's (digest pinned from dfb8aac's Core), no laser door.
        //    Re-measured on exp-layout-7 (2026-10-20: the town's three doorways moved 24 m out; the state-piece code unchanged; it
        //    was 45418637975.04724 on exp-layout-6's doors).
        private const double HubDigestBase=45418413071.04724;
        private static void Hub(ExpeditionLayout layout)
        {
            foreach(var a in layout.areas){var rig=ExpeditionStateRig.For(layout,a.id);foreach(var d in rig.Doors)Check((d.Look==ExpeditionDoorLook.Laser)==(a.role=="instance")&&d.Exit==(a.role=="instance"),a.id+": only an instance's exit is a laser door");}
            var hub=ExpeditionStateRig.For(layout,layout.world);Check(hub.Doors.All(d=>d.Look==(d.Building==NativeExpeditionLaserDoor.WardZero?ExpeditionDoorLook.Biocontainment:ExpeditionDoorLook.Heavy)),"the hub's doors keep their airlocks");
            double digest=HubDigest(layout,null);Check(Math.Abs(digest-HubDigestBase)<.05,"the hub's state pieces match the base code ("+digest.ToString("R")+")");
            var preview=new ExpeditionClosureView();NativeLaserDoorPreview.Fill(preview,layout.instances[0]+":door:exit",layout.instances[0],2);
            Check(Math.Abs(HubDigest(layout,preview)-HubDigestBase)<.05,"the DEV preview never touches the hub");
        }
        /// <summary>The hub's state pieces over every door and gate state, tier and phase, summed position-weighted (rounded to 1e-3).</summary>
        private static double HubDigest(ExpeditionLayout layout,ExpeditionClosureView preview)
        {
            var area=layout.Area(layout.world);var rig=ExpeditionStateRig.For(layout,area.id);var scene=new NativeExpeditionStateScene{DoorPreview=preview};double sum=0;int k=0;
            string[] states={"open","sealed","sealing","opening"};double[] prog={0,0,.3,.6};var eye=new ExpeditionEye{X=0,Y=1.7f,Z=0,Rx=1,Ry=0,Rz=0,Ux=0,Uy=1,Uz=0};
            for(int si=0;si<states.Length;si++)for(int tier=0;tier<4;tier++)
            {
                var closures=new List<ExpeditionClosureView>();
                foreach(var d in area.doors)closures.Add(new ExpeditionClosureView{id=d.anchor,area=area.id,state=states[si],began=100-prog[si]});
                foreach(var g in area.gates)closures.Add(new ExpeditionClosureView{id=g.id,area=area.id,state=states[(si+1)%4],began=100-prog[(si+1)%4]});
                var v=new ExpeditionView{run="r",world=layout.world,area=area.id,phase=si%2==0?ExpeditionPhase.Town:ExpeditionPhase.Instance,elapsed=100,closures=closures.ToArray()};
                scene.Build(v,rig,null,100,eye,(tier&1)!=0,(tier&2)!=0);Check(!scene.LaserDrawn,"no laser door in the hub");
                foreach(var p in scene.Parts){sum+=W(ref k,p.X)+W(ref k,p.Y)+W(ref k,p.Z)+W(ref k,p.Hx)+W(ref k,p.Hy)+W(ref k,p.Hz)+W(ref k,p.Yaw)+W(ref k,p.Gain)+W(ref k,p.Level)+W(ref k,p.Look)+W(ref k,p.Flags)+W(ref k,p.Hex)+W(ref k,p.Seed);}
                var q=scene.Quads;for(int i=0;i<q.Count;i++){for(int j=0;j<12;j++)sum+=W(ref k,q.Corners[i*12+j]);for(int j=0;j<4;j++)sum+=W(ref k,q.Colors[i*4+j]);sum+=W(ref k,q.Modes[i])+W(ref k,q.Additive[i]);}
                sum+=W(ref k,scene.DoorsDrawn)+W(ref k,scene.GatesDrawn)+W(ref k,scene.Parts.Count)+W(ref k,q.Count);
            }
            return sum;
        }
        private static double W(ref int k,double x){k++;return Math.Round(x,3)*(k%7+1);}

        // 6. The DEV preview: lock, sealed, unlock, open on a 7 s cycle, drawn in place of the record (which it never writes).
        private static void Preview(ConversionContent content,ExpeditionLayout layout)
        {
            var c=new ExpeditionClosureView();
            NativeLaserDoorPreview.Fill(c,"x","a",14.4);Check(c.state=="sealing"&&c.began==14&&c.id=="x"&&c.area=="a","the cycle starts with the lock");
            NativeLaserDoorPreview.Fill(c,"x","a",16);Check(c.state=="sealed"&&c.began==15,"then sealed");
            NativeLaserDoorPreview.Fill(c,"x","a",18.5);Check(c.state=="opening"&&c.began==18,"then the unlock");
            NativeLaserDoorPreview.Fill(c,"x","a",20.2);Check(c.state=="open"&&c.began==19,"then open");
            var p=Start(content,layout);string id=layout.instances[1];var rig=ExpeditionStateRig.For(layout,id);var f=rig.Doors[0].Frame;var v=p.View(id);var record=v.closures.Single(x=>x.id==rig.Doors[0].Anchor);
            var scene=new NativeExpeditionStateScene{DoorPreview=c};NativeLaserDoorPreview.Fill(c,record.id,id,20.2);scene.Build(v,rig,null,20.2,EyeAt(f,4),false,false);
            Check(scene.LaserStage==ExpeditionLaserStage.Open&&scene.LaserBeamsLit==0&&record.state=="open"&&p.Exit(id).state=="open","the preview draws its stand-in; the record and the director are untouched");
            Check(NativeDevActions.Find("preview-door-lock")?.Kind==NativeDevKind.Presentation,"the DEV panel's view tool");
        }

        // 7. Tiers: five beams on low graphics; no flash with reduced effects; fewer quads on low.
        private static void Tiers(ConversionContent content,ExpeditionLayout layout)
        {
            var p=Start(content,layout);string id=layout.instances[2];Check(p.Run.MoveMembers(layout.world,id,new[]{"host"}),"tier fixture enters");for(int tick=0;tick<8;tick++)p.Tick(.25);var rig=ExpeditionStateRig.For(layout,id);var f=rig.Doors[0].Frame;var v=p.View(id);var eye=EyeAt(f,4);
            var full=new NativeExpeditionStateScene();full.Build(v,rig,null,30,eye,false,false);var low=new NativeExpeditionStateScene();low.Build(v,rig,null,30,eye,false,true);var calm=new NativeExpeditionStateScene();calm.Build(v,rig,null,30,eye,true,false);
            Check(full.LaserBeamsLit==9&&low.LaserBeamsLit==5&&calm.LaserBeamsLit==9,"nine beams, five on low");Check(low.Quads.Count<full.Quads.Count&&low.Parts.Count<full.Parts.Count&&calm.Quads.Count<=full.Quads.Count,"low graphics and reduced effects draw less");
            Blocks(low,f,true,"low");Blocks(calm,f,true,"reduced");
            Check(full.Parts.Count<=NativeExpeditionStateScene.PartCapacity/4&&full.Quads.Count<=NativeExpeditionStateScene.QuadCapacity/4,"the door fits well inside the state pieces' budgets ("+full.Parts.Count+" parts, "+full.Quads.Count+" quads)");
        }

        // 8. No allocation per frame once warm: the cues and the scene through lock, sealed, unlock, open, all tiers.
        private static void NoAllocation(ConversionContent content,ExpeditionLayout layout)
        {
            var p=Start(content,layout);string id=layout.instances[0];var rig=ExpeditionStateRig.For(layout,id);var eye=EyeAt(rig.Doors[0].Frame,4);
            Check(p.Run.MoveMembers(layout.world,id,new[]{"host","guest"}),"in");p.Tick(.25);var v=p.View(id);var cues=new ExpeditionStateCues();var scene=new NativeExpeditionStateScene();var preview=new ExpeditionClosureView();
            void Frame(double t,int tier){cues.Observe(v,t);NativeLaserDoorPreview.Fill(preview,rig.Doors[0].Anchor,id,t);scene.DoorPreview=tier==3?preview:null;scene.Build(v,rig,cues,t,eye,(tier&1)!=0,(tier&2)!=0);}
            for(int i=0;i<600;i++)Frame(v.elapsed+i/60.0,i%4);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<1200;i++)Frame(v.elapsed+10+i/60.0,i%4);
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"no allocation per frame ("+bytes+" bytes over 1200 frames)");
        }

        // 9. Sources: presentation only (no collider, no rule, no randomness); the sound keys and their fallbacks.
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            var core=R("Core/NativeExpeditionLaserDoor.cs")+R("Core/NativeExpeditionStateScene.Laser.cs");var runtime=R("Runtime/NativeLaserDoorPresentation.cs")+R("Runtime/NativeLaserDoorAudio.cs");
            foreach(var bad in new[]{"Random","DateTime","Collider","Physics","expeditionClosures",".Blocking=","SetSafe","MoveMembers","Progress("})Check(!core.Contains(bad)&&!runtime.Contains(bad),"no "+bad+" in the laser door");
            Check(!Regex(runtime,@"\bv\.closures\b|view\.closures\b"),"the runtime never writes the view's closures");
            Check(runtime.Contains("\"expedition.door.lock\"")&&runtime.Contains("\"expedition.door.lock.snap\"")&&runtime.Contains("\"expedition.door.unlock\"")&&runtime.Contains("\"expedition.door.hum\""),"the sound keys");
            foreach(var clip in new[]{"shield","electric-hook-hit","electric-hook"})Check(Directory.GetFiles(Path.Combine(root,"Assets/Conversion/Resources/Audio/audio"),clip+".*").Any(x=>!x.EndsWith(".meta")),"fallback clip "+clip+" ships");
            Check(NativeKitSynth.Samples("alarm.beacon")?.Length>0,"the hum's fallback: the built-in synthesized beacon hum");
            Check(R("Runtime/NativeDevPanel.cs").Contains("case \"preview-door-lock\":NativeLaserDoorPreview.On=!NativeLaserDoorPreview.On"),"the DEV toggle");
            Check(R("Runtime/NativeExpeditionStatesPresentation.cs").Contains("expeditionCues.ArrivalHeld=StageLoadingShown||!mapReady;expeditionCues.Observe(v,t);"),"the arrival waits for the loading screen and the map");
        }
        private static bool Regex(string s,string pattern)=>System.Text.RegularExpressions.Regex.IsMatch(s,pattern);
    }
}
#endif
