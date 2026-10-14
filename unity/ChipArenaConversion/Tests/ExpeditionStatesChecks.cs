#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // The Expedition state presentation (docs/EXPEDITION-PRESENTATION.md "Doors, gate, sanctuary, transfer and boss arrival")
    // against Astra's real director: door leaves and seals from closures[] (state and `began`, the one-second transitions), the
    // final gate and its beacon cue, the sanctuary SAFE chip from members[].safe only, the transfer screen from areaBarrier, the
    // boss intro once per bossArrivals[] entry (late joiners only the plate), the presentation clock, no allocation per frame and
    // source scans (no mutators, no collision, no randomness, palette-only HUD).
    internal static class ExpeditionStatesChecks
    {
        private static int count;private static string Root;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition states: "+what);count++;}
        private const string City="exp-city",Ward="exp-ward-zero",CityDoor="exp-city:door:exp-ward-zero",WardDoor="exp-ward-zero:door:exit",Gate="exp-city:gate:final";
        // A small layout with the manifest's real door, gate and sanctuary records (exp-layout-2's numbers).
        private static string LayoutJson()
        {
            var slots=Enumerable.Range(0,8).Select(i=>new{x=-3.5f+i,y=0,z=-2,yaw=0}).ToArray();var returns=Enumerable.Range(0,8).Select(i=>new{x=38f,y=0,z=-3.5f+i,yaw=270}).ToArray();
            var entries=Enumerable.Range(0,8).Select(i=>new{x=-34f,y=0,z=-27f+i,yaw=90}).ToArray();var point=new{x=0,y=0,z=0,yaw=0};
            var city=new{id=City,role="world",map="expedition-city",
                anchors=new object[]{new{id=CityDoor,kind="door",building=Ward,x=43.4f,y=0,z=0,yaw=270},new{id="exp-city:cache:0",kind="chest",building="",x=-5.3f,y=0,z=-5.3f,yaw=45},new{id="exp-city:machine:0",kind="machine",building="",x=5.3f,y=0,z=5.3f,yaw=225}},
                doors=new[]{new{anchor=CityDoor,interact=new{x=43f,y=1.5f,z=0,sx=3,sy=3,sz=2,yaw=270},closure=new{x=43.8f,y=1.7f,z=0,sx=3.2f,sy=3.4f,sz=.4f,yaw=270},approach=new{x=42.4f,y=0,z=0},entries=Array.Empty<object>(),returns}},
                interactions=new[]{new{anchor="exp-city:cache:0",x=-4.7f,y=0,z=-4.7f,range=2f},new{anchor="exp-city:machine:0",x=4.66f,y=0,z=4.66f,range=2.5f}},
                sanctuary=new{volumes=new[]{new{x=0,y=4,z=0,sx=24,sy=8,sz=24,yaw=0}},slots},
                gates=new[]{new{id=Gate,opens="final",closure=new{x=0,y=3,z=37,sx=12,sy=6,sz=.6f,yaw=0}}},
                boss=new{points=new[]{new{x=0,y=0,z=47,yaw=180}},trigger=new{x=0,y=4,z=47.5f,sx=44,sy=8,sz=17,yaw=0}},
                spawns=new[]{new{e=0,id="exp-city:spawn:0",kind="door",from=point,exit=new{x=20,y=0,z=20},yaw=0,level=0,clear=1.2f,allow=new[]{"shambler"}}}};
            var ward=new{id=Ward,role="instance",map="expedition-ward-zero",anchors=new[]{new{id=WardDoor,kind="door",building=Ward,x=-37.4f,y=0,z=-23.4f,yaw=270}},
                doors=new[]{new{anchor=WardDoor,interact=new{x=-36.9f,y=1.5f,z=-23.4f,sx=3,sy=3,sz=2,yaw=270},closure=new{x=-37.75f,y=1.7f,z=-23.4f,sx=3.2f,sy=3.4f,sz=.4f,yaw=270},approach=new{x=-36.4f,y=0,z=-23.4f},entries,returns=Array.Empty<object>()}},
                boss=new{points=new[]{point}},spawns=new[]{new{e=0,id="exp-ward-zero:spawn:0",kind="door",from=point,exit=new{x=5,y=0,z=5},yaw=0,level=0,clear=1.2f,allow=new[]{"shambler"}}}};
            return JsonSerializer.Serialize(new{schema=1,revision="exp-states-test",world=City,instances=new[]{Ward},areas=new object[]{city,ward}});
        }
        private sealed class Party
        {
            public NativeExpedition Run;public ExpeditionSession Session;public ExpeditionDirector Director;public V3 At=new V3(43,0,0);public string Fingerprint=new string('c',64);
            public ExpeditionView View(string who="host")=>NativeExpeditionView.Build(Run,who,Session.Sources);
        }
        private static Party Start(ConversionContent content,ExpeditionLayout layout)
        {
            var p=new Party{Run=NativeExpedition.Create("states-run",City,"host","pistol",23,new[]{new ExpeditionInstance{id=Ward,target=1}})};p.Run.Join("guest","pistol");
            p.Session=new ExpeditionSession(p.Run,content,_=>{});p.Director=new ExpeditionDirector(p.Session,layout,_=>p.At,(_,__)=>true,10,20,30);p.Director.BindLayoutIdentity(p.Fingerprint);
            return p;
        }
        private static ExpeditionEye Eye(float x,float y,float z,float tx,float ty,float tz)
        {
            float fx=tx-x,fy=ty-y,fz=tz-z,l=(float)Math.Sqrt(fx*fx+fy*fy+fz*fz);fx/=l;fy/=l;fz/=l;float rx=fz,rz=-fx,rl=(float)Math.Sqrt(rx*rx+rz*rz);rx/=rl;rz/=rl;
            return new ExpeditionEye{X=x,Y=y,Z=z,Rx=rx,Rz=rz,Ux=fy*rz,Uy=fz*rx-fx*rz,Uz=-fy*rx};
        }

        public static void Run(string root)
        {
            count=0;Root=root;var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});
            var layout=NativeExpeditionLayout.Parse(LayoutJson());
            Phases();Clock();Doors(content,layout);GateAndCue(content,layout);Sanctuary(content,layout);Transfer(content,layout);Boss(content,layout);
            Manifest(root);NoAllocation(content,layout);Sources(root);
            Console.WriteLine("PASS Expedition states ("+count+" checks)");
        }

        // 1. The closure phases: blocking exactly when the state is not "open" (the collider), the one-second transitions.
        private static void Phases()
        {
            foreach(var s in new[]{"open","sealing","sealed","opening","tampered",null})
            {
                var c=new ExpeditionClosureView{id="d",state=s,began=10};
                Check(NativeExpeditionStates.Blocking(c)==c.Blocking,"blocking mirrors the collider predicate ("+(s??"null")+")");
            }
            ExpeditionClosureView C(string s)=>new ExpeditionClosureView{id="d",state=s,began=10};
            Check(NativeExpeditionStates.Shut(C("open"),9)==0&&NativeExpeditionStates.Shut(C("open"),99)==0,"open is open");
            Check(NativeExpeditionStates.Shut(C("sealed"),9)==1&&NativeExpeditionStates.Shut(C("tampered"),11)==1,"sealed, and an unknown state, are shut");
            var sealing=C("sealing");var opening=C("opening");float last=-1;
            for(double t=9.5;t<=11.5;t+=.05)
            {
                float s=NativeExpeditionStates.Shut(sealing,t),o=NativeExpeditionStates.Shut(opening,t);
                Check(s>=last-1e-6f&&s>=0&&s<=1&&Math.Abs(s+o-1)<1e-5f,"sealing closes monotonically; opening mirrors it (t "+t.ToString("0.00")+")");last=s;
            }
            Check(NativeExpeditionStates.Shut(sealing,10)==0&&NativeExpeditionStates.Shut(sealing,11)==1&&Math.Abs(NativeExpeditionStates.Shut(sealing,10.5)-.5f)<1e-5f,"sealing travels over exactly one second from `began`");
            Check(NativeExpeditionStates.Shut(opening,10)==1&&NativeExpeditionStates.Shut(opening,11)==0&&NativeExpeditionStates.Shut(opening,10.25)>NativeExpeditionStates.Shut(opening,10.75),"opening travels over exactly one second from `began`");
            Check(NativeExpeditionStates.Bolts(sealing,10.5)==0&&NativeExpeditionStates.Bolts(sealing,10.95)>0&&NativeExpeditionStates.Bolts(C("sealed"),0)==1&&NativeExpeditionStates.Bolts(opening,10.2)==0&&NativeExpeditionStates.Bolts(C("open"),0)==0,"bolts engage only at the end of sealing and draw back at the start of opening");
            float rise=NativeExpeditionStates.GateRise(opening,10.1,out float unlock);Check(rise==0&&unlock>0&&unlock<1,"the gate unlocks before it rises");
            rise=NativeExpeditionStates.GateRise(opening,11,out unlock);Check(rise==1&&unlock==1,"the gate is up at the end of the opening second");
            rise=NativeExpeditionStates.GateRise(C("sealed"),11,out unlock);Check(rise==0&&unlock==0,"a sealed gate is down and locked");
            Check(NativeExpeditionStates.AreaName(Ward)=="WARD ZERO"&&NativeExpeditionStates.BossName("test-hulk")=="TEST HULK"&&NativeExpeditionStates.BossName("TEST")=="TEST","area and boss names; TEST kept");
            Check(NativeExpeditionStates.BossName("hulk_test").Contains("TEST")&&NativeExpeditionStates.BossName("scientist")=="SCIENTIST","a test archetype always shows TEST");
        }

        // 2. The presentation clock: the authority's elapsed run forward between views (capped), frozen while loading/migrating.
        private static void Clock()
        {
            var c=new ExpeditionPresentationClock();var v=new ExpeditionView{run="a",elapsed=50};
            Check(c.Observe(v,100)==50&&Math.Abs(c.Observe(v,100.1)-50.1)<1e-9&&Math.Abs(c.Observe(v,105)-50.25)<1e-9,"runs forward from the last view, at most MaxLead");
            v.elapsed=50.05;Check(c.Observe(v,105.01)>=50.25-1e-9,"never steps back by a sliver");v.elapsed=50.4;Check(Math.Abs(c.Observe(v,105.02)-50.4)<1e-9,"follows a newer view");
            v.loading=true;v.elapsed=50.45;Check(Math.Abs(c.Observe(v,106)-50.45)<1e-9,"holds still while the run is loading");v.loading=false;v.migrating=true;Check(Math.Abs(c.Observe(v,107)-50.45)<1e-9,"and during a host transfer");
            Check(c.Observe(new ExpeditionView{run="b",elapsed=3},108)==3,"a new run starts it over");Check(c.Observe(null,109)==0,"no view, no clock");
        }

        // 3. Doors through the real director: entry seals both doors over one second; leaves and seal follow closures[].
        private static void Doors(ConversionContent content,ExpeditionLayout layout)
        {
            var p=Start(content,layout);var cityRig=ExpeditionStateRig.For(layout,City);var wardRig=ExpeditionStateRig.For(layout,Ward);
            Check(cityRig.Doors.Length==1&&wardRig.Doors.Length==1&&cityRig.Gates.Length==1&&cityRig.Sanctuary.Length==1&&wardRig.Sanctuary.Length==0,"rigs from the manifest");
            var cf=cityRig.Doors[0].Frame;var wf=wardRig.Doors[0].Frame;
            Check(Math.Abs(cf.Fx+1)<1e-5f&&Math.Abs(cf.Fz)<1e-5f&&Math.Abs(wf.Fx-1)<1e-5f,"the town door faces the plaza (its approach); the instance exit faces into the building");
            Check(cityRig.Doors[0].Look==ExpeditionDoorLook.Biocontainment&&!cityRig.Doors[0].Exit&&wardRig.Doors[0].Exit,"Ward Zero's doors are biocontainment doors");
            var gf=cityRig.Gates[0].Frame;Check(gf.Fz<0&&Math.Abs(gf.W-12)<1e-5f&&Math.Abs(gf.H-6)<1e-5f&&gf.Cy==0,"the gate faces the sanctuary");
            var scene=new NativeExpeditionStateScene();var cues=new ExpeditionStateCues();var eye=Eye(36,1.7f,-3,44,2,0);
            var town=p.View();cues.Observe(town,town.elapsed);scene.Build(town,cityRig,cues,town.elapsed,eye,false,false);
            Check(town.closures.Single(c=>c.id==CityDoor).state=="open"&&scene.HasLeaf(CityDoor)&&!SealUp(scene,cf),"the town door stands open: leaves in the pockets, no seal");Leaves(scene,cf,0,"open");
            scene.Build(town,null,cues,0,eye,false,false);Check(scene.Parts.Count==0&&scene.Quads.Count==0&&!scene.HasLeaf(CityDoor),"no rig, nothing drawn");
            scene.Build(town,wardRig,cues,0,eye,false,false);Check(scene.Parts.Count==0&&!scene.HasLeaf(WardDoor),"a rig of another area draws nothing");
            var stripped=p.View();stripped.closures=Array.Empty<ExpeditionClosureView>();scene.Build(stripped,cityRig,cues,0,eye,false,false);
            Check(!scene.HasLeaf(CityDoor)&&scene.DoorsDrawn==0&&scene.Parts.Count==0,"a door without its closure record has no leaf (the HUD hologram stands in)");
            // Enter: the barrier, then the instance; both doors of Ward Zero seal from the authority's `began`.
            p.At=new V3(43,0,0);Check(p.Director.EnterDoor("host",CityDoor),"enter");Check(p.Director.Ready("host",Ward,1,p.Fingerprint)&&p.Director.Ready("guest",Ward,1,p.Fingerprint),"ready");
            Check(p.Director.CompleteAreaLoad((_,__)=>true),"placed");var v=p.View();var exit=v.closures.Single(c=>c.id==WardDoor);
            Check(exit.state=="sealing"&&v.closures.Single(c=>c.id==CityDoor).state=="sealing","the authority seals both doors");double began=exit.began;
            for(double dt=0;dt<=1.0001;dt+=.125)
            {
                scene.Build(v,wardRig,cues,began+dt,eye,false,false);float shut=NativeExpeditionStates.Shut(exit,began+dt);
                Check(SealUp(scene,wf),"sealing blocks from its first instant: the seal covers the closure face (t+"+dt+")");Leaves(scene,wf,shut,"sealing t+"+dt);
            }
            for(int i=0;i<4;i++)p.Session.Tick(.25);v=p.View();exit=v.closures.Single(c=>c.id==WardDoor);
            Check(exit.state=="sealed"&&Math.Abs(exit.began-(began+1))<1e-9,"sealed at exactly one second");scene.Build(v,wardRig,cues,v.elapsed,eye,false,false);
            Check(SealUp(scene,wf),"sealed: the seal is up");Leaves(scene,wf,1,"sealed");Check(scene.Parts.Count(b=>b.Look==NativeCyberCityLooks.Metal&&b.Hy<.07f&&b.Hx<=.17f)==3,"sealed: three lock bolts");
            // Clear the building: the doors open over one second, and the seal holds until the state is open.
            Check(p.Run.Progress("kill",1)&&p.Run.StartBosses(new[]{"TEST-boss"})&&p.Director.BossDefeated("TEST-boss",new V3(0,0,0)),"cleared");p.Director.Step();
            v=p.View();exit=v.closures.Single(c=>c.id==WardDoor);Check(exit.state=="opening","the authority opens the exit");began=exit.began;
            for(double dt=0;dt<1;dt+=.125)
            {
                scene.Build(v,wardRig,cues,began+dt,eye,false,false);Check(SealUp(scene,wf),"opening still blocks: the seal stays over the doorway (t+"+dt+")");Leaves(scene,wf,NativeExpeditionStates.Shut(exit,began+dt),"opening t+"+dt);
            }
            scene.Build(v,wardRig,cues,began+1.2,eye,false,false);Check(SealUp(scene,wf),"the seal follows the state, not the clock: still up until the view says open");
            for(int i=0;i<4;i++)p.Session.Tick(.25);v=p.View();exit=v.closures.Single(c=>c.id==WardDoor);Check(exit.state=="open"&&!exit.Blocking,"open after one second");
            scene.Build(v,wardRig,cues,v.elapsed,eye,false,false);Check(!SealUp(scene,wf),"open: the seal is gone the moment the collider releases");Leaves(scene,wf,0,"open again");
            scene.Build(v,wardRig,cues,v.elapsed,eye,true,true);Check(scene.Parts.Count>0&&!SealUp(scene,wf),"reduced and low tiers still draw the door");
        }
        /// <summary>The seal's fill: one premultiplied quad whose corners are exactly the closure face at the seal depth.</summary>
        private static bool SealUp(NativeExpeditionStateScene s,ExpeditionFrame f)
        {
            float d=NativeExpeditionStateScene.SealDepth(f),h=f.W/2;var want=new float[4][];
            f.Point(-h,0,d,out float ax,out float ay,out float az);f.Point(h,0,d,out float bx,out float by,out float bz);f.Point(h,f.H,d,out float cx,out float cy,out float cz);f.Point(-h,f.H,d,out float dx,out float dy,out float dz);
            want[0]=new[]{ax,ay,az};want[1]=new[]{bx,by,bz};want[2]=new[]{cx,cy,cz};want[3]=new[]{dx,dy,dz};var q=s.Quads;
            for(int i=0;i<q.Count;i++)
            {
                if(q.Additive[i]!=0)continue;bool same=true;
                for(int k=0;k<4&&same;k++)for(int j=0;j<3;j++)if(Math.Abs(q.Corners[i*12+k*3+j]-want[k][j])>1e-3f)same=false;
                if(same)return true;
            }
            return false;
        }
        /// <summary>The two leaves at `shut`: shut, they cover the doorway's whole width and height between them; open, both sit
        /// beyond the pockets' inner edge (|u| >= W/2 - 0.12, inside the unit).</summary>
        private static void Leaves(NativeExpeditionStateScene s,ExpeditionFrame f,float shut,string when)
        {
            var leaves=s.Parts.Where(b=>b.Look==NativeWardZeroLooks.Panel&&b.Flags==NativeCyberCityLooks.Bank).ToArray();Check(leaves.Length==2,"two leaves ("+when+")");
            float half=f.W/2;float U(ExpeditionKitBox b)=>(b.X-f.Cx)*f.Ux+(b.Z-f.Cz)*f.Uz;
            var l=leaves.OrderBy(U).ToArray();float l0=U(l[0])-l[0].Hx,l1=U(l[0])+l[0].Hx,r0=U(l[1])-l[1].Hx,r1=U(l[1])+l[1].Hx;
            float gap=r0-l1;Check(Math.Abs(gap-2*(1-shut)*(half+.12f))<2e-3f,"the leaves part by the shut amount ("+when+")");
            if(shut>=.9999f)Check(l0<=-half+1e-3f&&r1>=half-1e-3f&&gap<=1e-3f&&l.All(b=>b.Y-b.Hy<=f.Cy+.01f&&b.Y+b.Hy>=f.Cy+f.H-.03f),"shut, the leaves cover the whole doorway ("+when+")");
            if(shut<=1e-4f)Check(l1<=-(half-.12f)+1e-3f&&r0>=half-.12f-1e-3f,"open, both leaves sit in the pockets ("+when+")");
            Check(l.All(b=>Math.Abs((b.X-f.Cx)*f.Fx+(b.Z-f.Cz)*f.Fz)<=f.D/2+1e-4f),"the leaves stay inside the closure volume's depth ("+when+")");
        }

        // 4. The final gate: FinalUnlocked opens it over one second (unlock, rise); the lattice holds until open; one beacon cue.
        private static void GateAndCue(ConversionContent content,ExpeditionLayout layout)
        {
            var p=Start(content,layout);var rig=ExpeditionStateRig.For(layout,City);var f=rig.Gates[0].Frame;var scene=new NativeExpeditionStateScene();var cues=new ExpeditionStateCues();var eye=Eye(-4,1.7f,25,0,3.6f,37);
            var v=p.View();cues.Observe(v,v.elapsed);scene.Build(v,rig,cues,v.elapsed,eye,false,false);var g=v.closures.Single(c=>c.id==Gate);
            Check(g.state=="sealed"&&scene.GatesDrawn==1&&Lattice(scene,f)&&GateBars(scene,f)>0&&!cues.Cue(Gate,v.elapsed,out _),"a sealed gate: down, locked, lattice up, no cue");
            // Clear the only building through the director, back in town.
            p.At=new V3(43,0,0);p.Director.EnterDoor("host",CityDoor);p.Director.Ready("host",Ward,1,p.Fingerprint);p.Director.Ready("guest",Ward,1,p.Fingerprint);p.Director.CompleteAreaLoad((_,__)=>true);
            cues.Observe(p.View(),p.View().elapsed);
            Check(p.Run.Progress("kill",1)&&p.Run.StartBosses(new[]{"TEST-boss"})&&p.Director.BossDefeated("TEST-boss",new V3(0,0,0)),"cleared");p.Director.Step();
            v=p.View();g=v.closures.Single(c=>c.id==Gate);Check(g.state=="opening"&&p.Run.FinalUnlocked,"FinalUnlocked opens the gate");
            var town=Area(v,City);cues.Observe(v,v.elapsed);Check(cues.Cue(Gate,g.began+.5,out double age)&&Math.Abs(age-.5)<1e-9,"the beacon cue is timed from the authority's `began`");
            float last=-1;
            for(double dt=0;dt<1;dt+=.125)
            {
                scene.Build(town,rig,cues,g.began+dt,eye,false,false);float rise=NativeExpeditionStates.GateRise(g,g.began+dt,out _);
                Check(Lattice(scene,f),"opening still blocks: the lattice frame stays up (t+"+dt+")");Check(rise>=last,"the gate rises monotonically");last=rise;
                if(dt>=.25)Check(Beacon(scene,f),"the beacon pillar rises while the gate opens (t+"+dt+")");
            }
            for(int i=0;i<4;i++)p.Session.Tick(.25);v=p.View();g=v.closures.Single(c=>c.id==Gate);Check(g.state=="open"&&!g.Blocking,"open after one second");
            cues.Observe(v,v.elapsed);town=Area(v,City);scene.Build(town,rig,cues,v.elapsed,eye,false,false);
            Check(!Lattice(scene,f)&&GateBars(scene,f)==0,"open: no lattice, the gate is up in the gatehouse");
            Check(cues.Cue(Gate,g.began-1+NativeExpeditionStates.CueSeconds-.1,out _)&&!cues.Cue(Gate,g.began-1+NativeExpeditionStates.CueSeconds+.1,out _),"the cue lasts CueSeconds from the opening");
            // A client that first sees the gate open (resume, late join) gets no cue; one that first sees it opening does.
            var late=new ExpeditionStateCues();late.Observe(v,v.elapsed);Check(!late.Cue(Gate,v.elapsed,out _),"first seen open: no cue");
            var mid=new ExpeditionStateCues();var opening=p.View();opening.closures.Single(c=>c.id==Gate).state="opening";mid.Observe(opening,opening.elapsed);Check(mid.Cue(Gate,opening.elapsed,out _),"first seen opening: the cue plays");
        }
        private static ExpeditionView Area(ExpeditionView v,string area){var c=JsonSerializer.Deserialize<ExpeditionView>(JsonSerializer.Serialize(v,new JsonSerializerOptions{IncludeFields=true}),new JsonSerializerOptions{IncludeFields=true});c.area=area;return c;}
        private static bool Lattice(NativeExpeditionStateScene s,ExpeditionFrame f)
        {
            float d=f.D/2+.15f,h=f.W/2;f.Point(-h,0,d,out float ax,out _,out float az);f.Point(h,f.H,d,out float cx,out float cy,out float cz);var q=s.Quads;
            for(int i=0;i<q.Count;i++)if(q.Additive[i]==0&&Math.Abs(q.Corners[i*12]-ax)<1e-3f&&Math.Abs(q.Corners[i*12+2]-az)<1e-3f&&Math.Abs(q.Corners[i*12+6]-cx)<1e-3f&&Math.Abs(q.Corners[i*12+7]-cy)<1e-3f&&Math.Abs(q.Corners[i*12+8]-cz)<1e-3f)return true;
            return false;
        }
        private static int GateBars(NativeExpeditionStateScene s,ExpeditionFrame f)=>s.Parts.Count(b=>b.Hx<.08f&&Math.Abs(b.Hy-f.H/2)<1e-3f);
        private static bool Beacon(NativeExpeditionStateScene s,ExpeditionFrame f){var q=s.Quads;for(int i=0;i<q.Count;i++)if(q.Additive[i]!=0&&q.Corners[i*12+7]>f.Cy+f.H+1)return true;return false;}

        // 5. The sanctuary: SAFE from members[].safe and mine.safe only; the boundary lit only where the authority grants `safe`.
        private static void Sanctuary(ConversionContent content,ExpeditionLayout layout)
        {
            var p=Start(content,layout);var labels=new ExpeditionLabels();var m=new ExpeditionSafeModel();
            p.At=new V3(1,0,1);p.Director.Step();var v=p.View();Check(v.mine.safe&&v.Member("guest").safe,"the authority grants sanctuary membership");
            m.From(v,labels);Check(m.Shown&&m.Safe==2&&m.Party==2&&m.Line=="PARTY 2 / 2 SAFE"&&m.Names=="GUEST","the SAFE chip and the party line from the view");
            Check(!ExpeditionSafeModel.Chip.Contains("PROTECT")&&!ExpeditionSafeModel.Chip.Contains("INVULN")&&!ExpeditionSafeModel.Chip.Contains("SHIELD"),"the chip never claims damage protection");
            p.At=new V3(30,0,30);p.Director.Step();v=p.View();m.From(v,labels);Check(!v.mine.safe&&!m.Shown&&m.Line==null,"outside: no chip");
            // Position never grants it: a view saying not safe shows nothing, wherever the pilot stands and whatever crossed.
            var cues=new ExpeditionStateCues();var rig=ExpeditionStateRig.For(layout,City);cues.Pilot(7,30,0,30,rig,1);cues.Pilot(7,1,0,1,rig,2);
            m.From(v,labels);Check(!m.Shown&&cues.Ripples.Count(r=>r.At==2)==1,"a crossing makes a ripple, never a SAFE chip");
            var forged=p.View();forged.mine.safe=true;forged.mine.account=null;m.From(forged,labels);Check(!m.Shown,"a non-member view shows no chip");
            var stranger=p.View("stranger");m.From(stranger,labels);Check(!m.Shown,"a stranger has no chip");
            cues.Pilot(7,1,0,1,rig,3);Check(cues.Ripples.Count(r=>r.At==3)==0,"no flip, no ripple");cues.Pilot(8,1,0,1,rig,4);Check(cues.Ripples.Count(r=>r.At==4)==0,"a pilot's first sighting only records its side");
            var rp=cues.Ripples.Single(r=>r.At==2);Check(Math.Abs(Math.Abs(rp.X)-12)<1e-3f||Math.Abs(Math.Abs(rp.Z)-12)<1e-3f,"the ripple stands on the boundary");
            // Lit (warm) in town and victory; a dim outline in the final-boss phase (the authority grants nobody `safe` then).
            var scene=new NativeExpeditionStateScene();var eye=Eye(-17,2.6f,-17,-4,0,-4);v.closures=Array.Empty<ExpeditionClosureView>();v.phase=ExpeditionPhase.Town;scene.Build(v,rig,cues,2.3,eye,false,false);int lit=scene.Quads.Count;float litAlpha=EdgeAlpha(scene);
            v.phase=ExpeditionPhase.FinalBoss;scene.Build(v,rig,cues,2.3,eye,false,false);Check(!NativeExpeditionStates.SanctuaryLit(v)&&EdgeAlpha(scene)<litAlpha*.5f&&scene.Quads.Count<lit,"the final-boss phase dims the sanctuary to an outline ("+EdgeAlpha(scene)+" vs "+litAlpha+", "+scene.Quads.Count+" vs "+lit+")");
            v.phase=ExpeditionPhase.Town;scene.Build(v,rig,null,2.3,eye,false,true);Check(scene.Quads.Count>0&&scene.Quads.Count<lit,"the low tier keeps only the floor edge");
            for(int i=0;i<scene.Quads.Count*4;i++)Check(Math.Abs(scene.Quads.Corners[i*3+1]-.025f)<.005f,"the low tier's edge lies on the sanctuary floor");
        }
        private static float EdgeAlpha(NativeExpeditionStateScene s){float best=0;for(int i=0;i<s.Quads.Count;i++)best=Math.Max(best,s.Quads.Colors[i*4+3]);return best;}

        // 6. The transfer screen: the barrier's target, ready / required from the view, the fade with the map load.
        private static void Transfer(ConversionContent content,ExpeditionLayout layout)
        {
            var p=Start(content,layout);var labels=new ExpeditionLabels();var t=new ExpeditionTransferModel();
            t.From(p.View(),labels);Check(!t.Active,"no barrier, no screen");
            p.At=new V3(43,0,0);p.Director.EnterDoor("host",CityDoor);var v=p.View();t.From(v,labels);
            Check(t.Active&&t.Title=="ENTERING WARD ZERO"&&t.Required==2&&t.ReadyCount==0&&t.Ready=="PARTY READY 0 / 2"&&t.Detail==ExpeditionTransferModel.Paused,"entering Ward Zero, nobody ready yet");
            p.Director.Ready("host",Ward,1,p.Fingerprint);t.From(p.View(),labels);Check(t.ReadyCount==1&&t.Ready=="PARTY READY 1 / 2"&&Math.Abs(t.Fill-.5f)<1e-6f,"one of two ready");
            p.Director.Ready("guest",Ward,1,p.Fingerprint);p.Director.CompleteAreaLoad((_,__)=>true);t.From(p.View(),labels);Check(!t.Active&&t.Title=="ENTERING WARD ZERO","the barrier drops; the title stays for the fade");
            p.Run.Progress("kill",1);p.Run.StartBosses(new[]{"b"});p.Director.BossDefeated("b",new V3(0,0,0));p.Run.VoteExit("host",true);p.Run.VoteExit("guest",true);p.Session.Tick(.1);
            v=p.View();t.From(v,labels);Check(t.Active&&v.areaBarrier.target==City&&t.Title==ExpeditionTransferModel.Back,"returning to the city after a building");
            var first=new ExpeditionView{world=City,areaBarrier=new ExpeditionAreaBarrierView{active=true,target=City,required=0}};t.From(first,labels);Check(t.Title==ExpeditionTransferModel.First&&t.Ready==ExpeditionTransferModel.Waiting,"the run's first load enters the city");
            var odd=new ExpeditionView{world=City,areaBarrier=new ExpeditionAreaBarrierView{active=true,target=Ward,ready=9,required=3}};t.From(odd,labels);Check(t.ReadyCount==3&&t.Fill==1,"counts are clamped for display only");
            var f=new ExpeditionTransferFade();float a=0;for(int i=0;i<5;i++)a=f.Step(true,false,.05f);Check(Math.Abs(a-1)<1e-5f,"fades in over FadeIn");
            for(int i=0;i<10;i++)a=f.Step(false,false,.1f);Check(a==1,"holds while the map has not loaded");a=f.Step(false,true,.2f);Check(a<1&&a>0,"fades out with the map");
            for(int i=0;i<4;i++)a=f.Step(false,true,.1f);Check(a==0,"gone after FadeOut");f.Reset();f.Step(true,false,1);for(int i=0;i<40;i++)a=f.Step(false,false,.1f);Check(a==0,"never held past HoldMax");
        }

        // 7. The boss intro: once per arrival from its authority spawn time, TEST kept; late joiners see only the plate.
        private static void Boss(ConversionContent content,ExpeditionLayout layout)
        {
            var p=Start(content,layout);p.At=new V3(43,0,0);p.Director.EnterDoor("host",CityDoor);p.Director.Ready("host",Ward,1,p.Fingerprint);p.Director.Ready("guest",Ward,1,p.Fingerprint);p.Director.CompleteAreaLoad((_,__)=>true);
            Check(p.Run.Progress("kill",1)&&p.Run.StartBosses(new[]{"TEST-boss"})&&p.Director.RecordBossArrival("TEST-boss","test-hulk",a=>a=="test-hulk"),"the authority records the arrival");
            var v=p.View();var arrival=v.bossArrivals.Single();var cues=new ExpeditionStateCues();var model=new ExpeditionBossModel();int intros=0,frames=0;double start=arrival.spawned;bool was=false;
            for(double t=start-.5;t<start+8;t+=1/60.0)
            {
                cues.Observe(v,t);model.From(v,cues,t);bool on=model.Intro>=0;if(on&&!was)intros++;was=on;frames++;
                if(on)Check(model.Name=="TEST HULK"&&model.Area=="BOSS ARRIVAL · WARD ZERO"&&Math.Abs(model.Age-(t-start))<1e-9,"the intro is timed from `spawned`");
                Check(model.PlateCount==1&&model.PlateNames[0]=="TEST HULK"&&!model.PlateDown[0],"the plate names the boss");
            }
            Check(intros==1,"exactly one intro per arrival ("+intros+" over "+frames+" frames)");
            var late=new ExpeditionStateCues();late.Observe(v,start+NativeExpeditionStates.LateJoin+.2);model.From(v,late,start+NativeExpeditionStates.LateJoin+.2);
            Check(model.Intro<0&&model.PlateCount==1&&double.IsPositiveInfinity(model.PlateAge[0]),"a late joiner sees only the HUD plate");
            var early=new ExpeditionStateCues();early.Observe(v,start+2.5);model.From(v,early,start+2.5);Check(model.Intro>=0&&Math.Abs(model.Age-2.5)<1e-9,"a joiner within LateJoin sees the rest of the intro, in step");
            p.Director.BossDefeated("TEST-boss",new V3(0,0,0));var down=p.View();cues.Observe(down,start+9);model.From(down,cues,start+9);Check(model.PlateDown[0],"the plate reads DOWN once the building lists it defeated");
            // Another area's arrival plays nothing here; a new loop forgets the arrivals (the director clears them).
            var other=Area(v,City);var oc=new ExpeditionStateCues();oc.Observe(other,start);model.From(other,oc,start);Check(model.Intro<0&&model.PlateCount==0,"another area's boss plays nothing");
            var next=Area(v,Ward);next.loop=1;cues.Observe(next,start+.5);model.From(next,cues,start+.5);Check(model.Intro>=0,"a new loop's arrival with the same id plays again");
            // The bound crack: deterministic, finite, gone after 2.6 s.
            var scene=new NativeExpeditionStateScene();var eye=Eye(-9,2.2f,-10,0,.8f,0);scene.Clear();scene.BossCrack(0,0,0,.5,7,eye,false);int n=scene.Quads.Count;var first=scene.Quads.Corners.Take(n*12).ToArray();
            scene.Clear();scene.BossCrack(0,0,0,.5,7,eye,false);Check(n>40&&scene.Quads.Corners.Take(n*12).SequenceEqual(first),"the crack is deterministic");
            scene.Clear();scene.BossCrack(0,0,0,2.7,7,eye,false);Check(scene.Quads.Count==0,"and over after 2.6 s");
            Check(!File.ReadAllText(Path.Combine(Root,"Assets/ChipArena/Runtime/NativeExpeditionStatesPresentation.cs")).Contains("BossCrack("),"the live game draws no crack without a boss position binding");
        }

        // 8. The imported manifest: every door and gate of exp-layout-2 has a rig whose leaves stay in the closure volume.
        private static void Manifest(string root)
        {
            var layout=NativeExpeditionLayout.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json")));
            foreach(var area in layout.areas)
            {
                var rig=ExpeditionStateRig.For(layout,area.id);Check(rig.Doors.Length==area.doors.Length&&rig.Gates.Length==area.gates.Length,"every door and gate of "+area.id+" has a rig");
                foreach(var d in rig.Doors)
                {
                    var door=area.doors.Single(x=>x.anchor==d.Anchor);float side=(door.approach.x-d.Frame.Cx)*d.Frame.Fx+(door.approach.z-d.Frame.Cz)*d.Frame.Fz;
                    Check(side>0,"the door faces its approach ("+d.Anchor+")");Check(Math.Abs(d.Frame.Cy-(door.closure.y-door.closure.sy/2))<1e-5f,"the leaves stand on the closure's floor");
                }
            }
        }

        // 9. No allocation per frame: clock, cues, crossings, the scene (doors, gate, sanctuary, ripples, crack) and the HUD models.
        private static void NoAllocation(ConversionContent content,ExpeditionLayout layout)
        {
            var p=Start(content,layout);p.At=new V3(1,0,1);p.Director.Step();var v=p.View();
            foreach(var c in v.closures)if(c.id==CityDoor){c.state="sealing";c.began=v.elapsed;}else if(c.id==Gate){c.state="opening";c.began=v.elapsed;}
            v.bossArrivals=new[]{new ExpeditionBossArrivalView{id="b",area=City,archetype="test-hulk",spawned=v.elapsed}};v.areaBarrier=new ExpeditionAreaBarrierView{active=true,target=Ward,ready=1,required=2};
            var rig=ExpeditionStateRig.For(layout,City);var clock=new ExpeditionPresentationClock();var cues=new ExpeditionStateCues();var scene=new NativeExpeditionStateScene();var labels=new ExpeditionLabels();
            var transfer=new ExpeditionTransferModel();var fade=new ExpeditionTransferFade();var safe=new ExpeditionSafeModel();var boss=new ExpeditionBossModel();var eye=Eye(-6,2,10,0,3,37);
            double local=100;
            void Frame(int i)
            {
                local+=1/60.0;double t=clock.Observe(v,local);cues.Observe(v,t);cues.Pilot(1,(i%40)<20?11.5f:12.5f,0,0,rig,t);cues.Pilot(2,0,0,0,rig,t);
                scene.Build(v,rig,cues,t,eye,false,false);scene.BossCrack(0,0,47,.5,3,eye,false);scene.HasLeaf(CityDoor);
                transfer.From(v,labels);fade.Step(transfer.Active,false,1/60f);safe.From(v,labels);boss.From(v,cues,t);
            }
            for(int i=0;i<120;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<240;i++)Frame(i);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"the state presentation allocates nothing per frame ("+bytes+" bytes over 240 frames)");
            Check(scene.Parts.Count>40&&scene.Quads.Count>200&&scene.Quads.Count<NativeExpeditionStateScene.QuadCapacity&&scene.Parts.Count<NativeExpeditionStateScene.PartCapacity,"the measured frame drew doors, gate, sanctuary and crack within capacity");
            for(int i=0;i<scene.Quads.Count*12;i++)Check(!float.IsNaN(scene.Quads.Corners[i])&&!float.IsInfinity(scene.Quads.Corners[i]),"finite quads");
            foreach(var b in scene.Parts)Check(b.Hx>0&&b.Hy>0&&b.Hz>0&&!float.IsNaN(b.X+b.Y+b.Z),"valid kit boxes");
        }

        // 10. Source scans: presentation only (no mutators, no closure writes, no collision), no randomness, palette-only HUD.
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            string rules=R("Core/NativeExpeditionStates.cs"),scene=R("Core/NativeExpeditionStateScene.cs"),draw=R("Runtime/NativeExpeditionStatesPresentation.cs"),hud=R("Runtime/NativeExpeditionStatesHud.cs");
            foreach(var (name,src) in new[]{("rules",rules),("scene",scene),("draw",draw),("HUD",hud)})
            {
                Check(!src.Contains("Random")&&!src.Contains("NativePveRandom"),name+": no randomness");
                Check(!Regex.IsMatch(src,@"\bNativeExpedition\b"),name+": no authority reference");
                Check(!Regex.IsMatch(src,@"\.\s*(state|began|ready|required|active|safe|spawned)\s*=[^=]"),name+": never writes the view's state");
                Check(!Regex.IsMatch(src,@"\b(Director|director|Session|session|Run|run)\s*\.\s*(EnterDoor|Ready|CompleteAreaLoad|BeginAreaLoad|RecordBossArrival|SetSafe|Step|Tick|Enter|BeginFinal|TryBeginFinal)\("),name+": calls no director or run mutator");
                Check(!Regex.IsMatch(src,@"\b(Collider|Physics|Rigidbody|SetActive|enabled\s*=)"),name+": touches no collision");
            }
            Check(!Regex.IsMatch(hud,@"0x[0-9a-fA-F]{6}")&&hud.Contains("NativeCyberTheme"),"the HUD restates no colour (palette only)");
            foreach(var statement in hud.Split(';'))if(statement.Contains("Text("))foreach(var tone in new[]{"Violet","Dim","Edge","CyanDeep"})Check(!statement.Contains("C."+tone+"("),"no "+tone+" text");
            Check(!Regex.IsMatch(hud,@"Text\([^;]*""[^""]*""\s*\+"),"the HUD concatenates no strings per frame");
        }
    }
}
#endif
