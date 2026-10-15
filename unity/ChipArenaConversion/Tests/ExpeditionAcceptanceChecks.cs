#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // Expedition presentation acceptance (Claude, 2026-10-15; docs/EXPEDITION-PRESENTATION.md "Acceptance pass"). One run
    // played through Astra's real session and director on the imported manifest (exp-layout-2) and its real fingerprint: the
    // host's own view and guests' views decoded from the real public/private wire (ExpeditionViewBatch, JSON, the paired
    // ExpeditionViewReceiver) with latency, each through the real presentation models (clock, cues, scene, transfer, boss, SAFE),
    // checked on every 60 Hz frame. The test plays the scene adapter (loads, ready tickets, placement) and combat callbacks the
    // way Astra's contract describes; presentation code calls none of them. Covers: seals and the gate lattice over the whole
    // blocking interval (never open before the authority), startup binding and the transfer screen, SAFE tags by seat, boss
    // intro / crack / plates through damage, healing, max-HP change and death, late joiners, Continue into a second loop with
    // the same boss ids, sanctuary crossings versus placements, the airlock's clearance and the frame budget.
    internal static class ExpeditionAcceptanceChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition acceptance: "+what);count++;}
        private const string City="exp-city",Ward="exp-ward-zero",CityDoor="exp-city:door:exp-ward-zero",WardDoor="exp-ward-zero:door:exit",Gate="exp-city:gate:final",Boss="TEST-boss",Final="final",Hulk="test-hulk";
        private static readonly JsonSerializerOptions Codec=new JsonSerializerOptions{IncludeFields=true};
        private const double Dt=1/60.0;

        public static void Run(string root)
        {
            count=0;var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),Codec);
            string json=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json"));var layout=NativeExpeditionLayout.Parse(json);
            string fingerprint=NativeExpeditionLayout.Compatibility(json,content);
            Journey(content,layout,fingerprint);Binding(content,layout,fingerprint);Seats(content,layout,fingerprint);Crossings(layout);
            Clearance(root,layout);Budget(content,layout,fingerprint);Sources(root);
            Console.WriteLine("PASS Expedition acceptance ("+count+" checks)");
        }

        // ---- The harness: the authority, the wire and the clients ---------------------------------------------------------
        private sealed class Client
        {
            public string Name,Account;public double Latency,PositionLatency,LoadSeconds=.6;public ExpeditionViewReceiver Receiver;
            public ExpeditionView View;public string Map;public ExpeditionStateRig Rig;public bool MapReady;public double T;
            public readonly ExpeditionPresentationClock Clock=new ExpeditionPresentationClock();public readonly ExpeditionStateCues Cues=new ExpeditionStateCues();
            public readonly NativeExpeditionStateScene Scene=new NativeExpeditionStateScene();public readonly ExpeditionLabels Labels=new ExpeditionLabels();
            public readonly ExpeditionTransferModel Transfer=new ExpeditionTransferModel();public readonly ExpeditionTransferFade Fade=new ExpeditionTransferFade();
            public readonly ExpeditionBossModel Boss=new ExpeditionBossModel();public readonly ExpeditionSafeTags Tags=new ExpeditionSafeTags();
            public readonly Queue<(double at,string pub,string mine)> Inbox=new Queue<(double,string,string)>();
            public readonly Queue<(double at,Dictionary<string,V3> where)> Moves=new Queue<(double,Dictionary<string,V3>)>();
            public Dictionary<string,V3> Seen=new Dictionary<string,V3>();
            public ExpeditionAreaReadyRequest Ticket;public double LoadDone;public long Loaded=-1;
            // What this client showed: intros and cracks per arrival (loop:area:id), gate cues, sanctuary ripples, titles.
            public readonly Dictionary<string,int> Intros=new Dictionary<string,int>(),Cracks=new Dictionary<string,int>();
            public string IntroKey;public bool CrackWas,CueWas;public int GateCues,GateLate,Ripples,Frames;public readonly HashSet<string> Titles=new HashSet<string>();
            public readonly double[] RippleAt=new double[ExpeditionStateCues.RippleSlots];
            public int Intro(string key)=>Intros.TryGetValue(key,out int n)?n:0;public int Crack(string key)=>Cracks.TryGetValue(key,out int n)?n:0;
        }
        private sealed class World
        {
            public NativeExpedition Run;public ExpeditionSession Session;public ExpeditionDirector Director;public ExpeditionLayout Layout;public string Fingerprint;
            public readonly Dictionary<string,V3> At=new Dictionary<string,V3>();public readonly Dictionary<string,int> Seats=new Dictionary<string,int>();
            public readonly List<Client> Clients=new List<Client>();public double Local=100;public int Frame;
            public readonly Dictionary<string,ExpeditionStateRig> Rigs=new Dictionary<string,ExpeditionStateRig>();
            public ExpeditionStateRig Rig(string area){if(area==null)return null;if(!Rigs.TryGetValue(area,out var r))r=Rigs[area]=ExpeditionStateRig.For(Layout,area);return r;}
        }
        private static World Start(ConversionContent content,ExpeditionLayout layout,string fingerprint,string run="accept-run")
        {
            var w=new World{Layout=layout,Fingerprint=fingerprint};
            w.Run=NativeExpedition.Create(run,City,"host","pistol",29,new[]{new ExpeditionInstance{id=Ward,target=1}});w.Run.Join("guest","pistol");
            w.Session=new ExpeditionSession(w.Run,content,_=>{});
            w.Director=new ExpeditionDirector(w.Session,layout,a=>w.At.TryGetValue(a,out var p)?p:(V3?)null,(_,__)=>true,10,20,30);w.Director.BindLayoutIdentity(fingerprint);
            w.Session.Sources.Seat=a=>w.Seats.TryGetValue(a,out int s)?s:0;w.Seats["host"]=1;w.Seats["guest"]=2;
            w.At["host"]=new V3(1,1,1);w.At["guest"]=new V3(2.5f,1,-1);
            return w;
        }
        private static Client Add(World w,string name,string account,double latency,double positions)
        {
            var c=new Client{Name=name,Account=account,Latency=latency,PositionLatency=positions,Map=w.Layout.Area(w.Run.Snapshot().area).map};
            if(account!="host")c.Receiver=new ExpeditionViewReceiver(account);
            // A client joining now has the latest frames (its join snapshot), then the stream.
            c.View=Project(w,c);c.Seen=new Dictionary<string,V3>(w.At);w.Clients.Add(c);return c;
        }
        private static ExpeditionView Project(World w,Client c)
        {
            if(c.Receiver==null)return NativeExpeditionView.Build(w.Run,c.Account,w.Session.Sources);
            var batch=ExpeditionViewBatch.Build(w.Run,new[]{c.Account},w.Session.Sources);
            c.Receiver.Public(JsonSerializer.Deserialize<ExpeditionView>(JsonSerializer.Serialize(batch.Public,Codec),Codec));
            c.Receiver.Private(JsonSerializer.Deserialize<ExpeditionPrivateFrame>(JsonSerializer.Serialize(batch.Private.Single(),Codec),Codec));return c.Receiver.View;
        }
        /// <summary>Runs `seconds` of 60 Hz frames: the authority ticks (Session.Tick: the run and the director's Step), views are
        /// projected at 20 Hz (the host's own view; guests' frames through JSON with their latency), positions reach each client
        /// on their own channel, the clients' scene adapters load and acknowledge, the host places, then every client presents.</summary>
        private static void Play(World w,double seconds,Action<World> each=null)
        {
            int frames=(int)Math.Round(seconds/Dt);
            for(int f=0;f<frames;f++)
            {
                w.Local+=Dt;w.Frame++;w.Session.Tick(Dt);each?.Invoke(w);
                if(w.Frame%3==0)
                {
                    var batch=ExpeditionViewBatch.Build(w.Run,w.Clients.Where(c=>c.Receiver!=null).Select(c=>c.Account),w.Session.Sources);string pub=JsonSerializer.Serialize(batch.Public,Codec);
                    foreach(var c in w.Clients)
                    {
                        if(c.Receiver==null)c.View=NativeExpeditionView.Build(w.Run,c.Account,w.Session.Sources);
                        else c.Inbox.Enqueue((w.Local+c.Latency,pub,JsonSerializer.Serialize(batch.Private.Single(p=>p.viewer==c.Account),Codec)));
                    }
                }
                foreach(var c in w.Clients)
                {
                    c.Moves.Enqueue((w.Local+c.PositionLatency,new Dictionary<string,V3>(w.At)));while(c.Moves.Count>0&&c.Moves.Peek().at<=w.Local+1e-9)c.Seen=c.Moves.Dequeue().where;
                    while(c.Inbox.Count>0&&c.Inbox.Peek().at<=w.Local+1e-9)
                    {
                        var m=c.Inbox.Dequeue();c.Receiver.Public(JsonSerializer.Deserialize<ExpeditionView>(m.pub,Codec));c.Receiver.Private(JsonSerializer.Deserialize<ExpeditionPrivateFrame>(m.mine,Codec));
                        c.View=c.Receiver.View;
                    }
                    Load(w,c);
                }
                var b=w.Director.Barrier;
                if(b.active&&b.required>0&&b.ready==b.required)
                    Check(w.Director.CompleteAreaLoad((area,assigned)=>{foreach(var kv in assigned)w.At[kv.Key]=new V3(kv.Value.x,kv.Value.y+1,kv.Value.z);return true;}),"the host places the party");
                foreach(var c in w.Clients)Present(w,c);
            }
        }
        /// <summary>A client's scene adapter (Astra's sequence): capture the ticket when the barrier shows, load, then report.</summary>
        private static void Load(World w,Client c)
        {
            var v=c.View;var b=v?.areaBarrier;if(b==null||!b.active||!v.loading||c.Loaded==b.generation)return;
            if(c.Ticket==null||c.Ticket.generation!=b.generation){c.Ticket=ExpeditionAreaReady.Capture(v);c.LoadDone=w.Local+c.LoadSeconds;Check(c.Ticket!=null,"a ticket for "+c.Name);return;}
            if(w.Local<c.LoadDone)return;c.Map=w.Layout.Area(c.Ticket.target).map;
            var sent=JsonSerializer.Deserialize<ExpeditionAreaReadyRequest>(JsonSerializer.Serialize(c.Ticket,Codec),Codec);
            Check(ExpeditionAreaReady.Accept(w.Session,c.Account,sent),"the authority accepts "+c.Name+"'s ready ticket");c.Loaded=b.generation;
        }
        private static ExpeditionEye EyeFor(string area)=>area==Ward?Eye(-30,1.7f,-22,-38,2,-23.4f):Eye(22,1.7f,14,10,2,20);

        /// <summary>One presentation frame, as ConversionGame's ExpeditionStatesFrame and HUD do it, then the invariants.</summary>
        private static void Present(World w,Client c)
        {
            var v=c.View;double t=c.T=c.Clock.Observe(v,w.Local);c.Cues.Observe(v,t);c.Frames++;
            c.Rig=v!=null&&NativeExpeditionStates.Bound(v,w.Fingerprint)?w.Rig(v.area):null;c.MapReady=NativeExpeditionStates.MapReady(c.Rig,c.Map);
            c.Transfer.From(v,c.Labels);c.Fade.Step(c.Transfer.Active,c.MapReady,(float)Dt);if(c.Transfer.Active)c.Titles.Add(c.Transfer.Title);
            if(c.Rig!=null&&c.MapReady)
            {
                foreach(var m in v.members)if(m.seat>0&&m.connected&&m.alive&&c.Seen.TryGetValue(m.account,out var p))c.Cues.Pilot(m.seat,p.x,p.y-1,p.z,c.Rig,t);
                c.Scene.Build(v,c.Rig,c.Cues,t,EyeFor(v.area),false,false);
            }
            else c.Scene.Clear();
            c.Boss.From(v,c.Cues,t,c.Labels);c.Tags.From(v);
            if(v==null)return;
            // Transfer: the screen follows the barrier; while the view names an area whose map is not loaded, it covers the frame.
            Check(c.Transfer.Active==(v.areaBarrier?.active==true),c.Name+": the transfer screen follows areaBarrier.active");
            if(c.Transfer.Active)Check(c.Transfer.Required==Math.Max(0,v.areaBarrier.required)&&c.Transfer.ReadyCount==Math.Min(Math.Max(0,v.areaBarrier.ready),c.Transfer.Required)&&(c.Transfer.Detail!=null)==v.loading,c.Name+": ready / required and the paused note from the barrier");
            if(c.Rig!=null&&!c.MapReady)Check(c.Transfer.Active||c.Fade.Alpha>0,c.Name+": a view naming an unloaded map is under the transfer screen");
            // Doors and gate: drawn sealed exactly while the view's closure blocks, and never open before the authority opens.
            if(c.Rig!=null&&c.MapReady)foreach(var cl in v.closures)
            {
                if(cl?.id==null||cl.area!=c.Rig.Area)continue;int d=c.Rig.Door(cl.id),g=c.Rig.Gate(cl.id);if(d<0&&g<0)continue;
                bool shut=d>=0?SealUp(c.Scene,c.Rig.Doors[d].Frame):Lattice(c.Scene,c.Rig.Gates[g].Frame);
                Check(shut==NativeExpeditionStates.Blocking(cl),c.Name+": "+cl.id+" draws its seal exactly while it blocks ("+cl.state+")");
                if(!shut){var a=w.Director.Closures.Single(x=>x.id==cl.id);Check(a.state=="open"||a.state=="sealing"&&a.began>=v.elapsed,c.Name+": "+cl.id+" never looks open before the authority opens it (authority "+a.state+")");}
                if(d>=0&&shut)Check(c.Scene.HasLeaf(cl.id),c.Name+": a blocking door has its leaves");
            }
            // Cues: count each intro, crack and gate beacon as it starts; ripples as they are written.
            string key=null;if(c.Boss.Intro>=0){var a=c.Cues.ArrivalAt(c.Boss.Intro);key=v.loop+":"+a.Area+":"+a.Id;}
            if(key!=null&&key!=c.IntroKey)c.Intros[key]=c.Intro(key)+1;c.IntroKey=key;
            bool crack=c.Scene.CracksDrawn>0;if(crack&&!c.CrackWas)for(int i=0;i<c.Cues.ArrivalCount;i++){var a=c.Cues.ArrivalAt(i);double age=t-a.Spawned;if(a.Intro&&a.Placed&&age>=0&&age<=NativeExpeditionStateScene.CrackSeconds){string k=v.loop+":"+a.Area+":"+a.Id;c.Cracks[k]=c.Crack(k)+1;}}
            // The gate's beacon and notice as this client can see them: in the town on its map, the transfer screen mostly gone.
            // A cue first visible well into its run (it played while the party was elsewhere) counts as late.
            c.CrackWas=crack;bool visible=c.Rig!=null&&c.MapReady&&c.Rig.Gate(Gate)>=0&&!c.Transfer.Active&&c.Fade.Alpha<.5f;
            double cueAge=0;bool cue=visible&&c.Cues.Cue(Gate,t,out cueAge);if(cue&&!c.CueWas){if(cueAge<.3)c.GateCues++;else c.GateLate++;}c.CueWas=cue;
            for(int i=0;i<c.RippleAt.Length;i++)if(c.Cues.Ripples[i].At!=c.RippleAt[i]){c.RippleAt[i]=c.Cues.Ripples[i].At;if(!double.IsNegativeInfinity(c.RippleAt[i]))c.Ripples++;}
        }
        /// <summary>Walks pilots in a straight line at `speed` m/s (real crossings), playing frames as they go.</summary>
        private static void Walk(World w,Dictionary<string,V3> to,float speed=7)
        {
            for(int guard=0;guard<6000;guard++)
            {
                bool moving=false;
                Play(w,Dt,x=>{foreach(var kv in to){var p=x.At[kv.Key];var d=kv.Value-p;float l=d.Length;if(l<1e-3f)continue;moving=true;x.At[kv.Key]=l<=speed*Dt?kv.Value:p+d*(float)(speed*Dt/l);}});
                if(!moving)return;
            }
            throw new Exception("Expedition acceptance: walk did not finish");
        }
        private static void Until(World w,Func<bool> done,double max,string what)
        {for(double s=0;s<max;s+=Dt){if(done())return;Play(w,Dt);}Check(done(),"in time: "+what);}

        // ---- 1-5. The journey -------------------------------------------------------------------------------------------------
        private static void Journey(ConversionContent content,ExpeditionLayout layout,string fingerprint)
        {
            var w=Start(content,layout,fingerprint);var host=Add(w,"host","host",0,0);var guest=Add(w,"guest","guest",.12,.05);
            Play(w,.5);
            // 2. Startup: the inactive barrier names the layout; both clients bind the town's rig on the town's map.
            foreach(var c in new[]{host,guest})
            {
                Check(!c.View.areaBarrier.active&&c.View.areaBarrier.generation==0&&c.Rig?.Area==City&&c.MapReady,c.Name+": the startup view binds the town (generation 0)");
                Check(c.Scene.HasLeaf(CityDoor)&&!SealUp(c.Scene,c.Rig.Doors[0].Frame)&&Lattice(c.Scene,c.Rig.Gates[0].Frame)&&!c.Transfer.Active&&c.Fade.Alpha==0,c.Name+": the door stands open, the gate sealed, no transfer screen");
                Check(c.Tags.Count==2&&c.Tags.Has(1)&&c.Tags.Has(2),c.Name+": SAFE over both pilots in the sanctuary");
            }
            // Walk out of the sanctuary to the town door: real crossings ripple (on the guest too, from its position stream).
            Walk(w,new Dictionary<string,V3>{["host"]=new V3(43,1,0),["guest"]=new V3(42.6f,1,1)});Play(w,.3);
            Check(host.Ripples>=2&&guest.Ripples>=2,"walking out of the sanctuary ripples at the boundary ("+host.Ripples+", "+guest.Ripples+")");
            foreach(var c in new[]{host,guest})Check(c.Tags.Count==0,c.Name+": no SAFE once the authority has the pilots outside");
            // Enter Ward Zero: the barrier, the transfer screen on both, the loads and the placement.
            Check(w.Director.EnterDoor("host",CityDoor),"enter Ward Zero");int ripples=host.Ripples+guest.Ripples;
            Until(w,()=>guest.Transfer.Active,1,"the guest sees the barrier");
            Check(host.Transfer.Title=="ENTERING WARD ZERO"&&guest.Transfer.Title=="ENTERING WARD ZERO","the transfer screen names Ward Zero");
            Until(w,()=>!w.Director.Barrier.active&&guest.View.areaBarrier.active==false,4,"the load completes");Play(w,1);
            foreach(var c in new[]{host,guest})Check(c.View.area==Ward&&c.Rig?.Area==Ward&&c.MapReady&&c.Fade.Alpha==0&&c.Scene.HasLeaf(WardDoor),c.Name+": in Ward Zero, the exit drawn, the screen gone");
            Check(host.Ripples+guest.Ripples==ripples,"entering a building ripples nothing");
            Play(w,1.2);foreach(var c in new[]{host,guest})Check(c.View.closures.Single(x=>x.id==WardDoor).state=="sealed",c.Name+": the exit sealed behind the party");

            // 4. The boss: crack and intro once at the recorded spawn, plates through damage, healing, max HP and death.
            Check(w.Run.Progress("kill",1)&&w.Run.StartBosses(new[]{Boss}),"quota and bosses");var spawn=new V3(0,0,8.6f);
            Check(w.Director.RecordBossArrival(Boss,Hulk,a=>a==Hulk,spawn,100,100),"the combat authority records the arrival");string k0="0:"+Ward+":"+Boss;
            Play(w,4.5);
            foreach(var c in new[]{host,guest})
            {
                Check(c.Intro(k0)==1&&c.Crack(k0)==1,c.Name+": one intro and one crack ("+c.Intro(k0)+", "+c.Crack(k0)+")");
                var a=c.Cues.ArrivalAt(0);Check(a.Placed&&a.X==spawn.x&&a.Y==spawn.y&&a.Z==spawn.z,c.Name+": the crack stands at the recorded spawn");
                Check(c.Boss.PlateCount==1&&c.Boss.PlateHealth[0]==1&&c.Boss.PlatePercent[0]=="100%",c.Name+": the plate at full health");
            }
            var late=AddMember(w,"late",3,new V3(-30,1,-23),.1,.05);Play(w,1);
            Check(late.Intro(k0)==0&&late.Crack(k0)==0&&late.Boss.PlateCount==1&&double.IsPositiveInfinity(late.Boss.PlateAge[0]),"a late joiner sees the plate only");
            double spawned=w.Director.BossArrivals.Single().spawned;
            foreach(var (hp,max,want) in new[]{(61.5,100.0,"62%"),(80.0,100.0,"80%"),(80.0,160.0,"50%"),(.3,160.0,"1%")})
            {
                Check(w.Director.UpdateBossHealth(Ward,Boss,hp,max),"health "+hp+" / "+max);Play(w,.4);
                foreach(var c in new[]{host,guest,late})
                {
                    Check(Math.Abs(c.Boss.PlateHealth[0]-(float)(hp/max))<1e-5f&&c.Boss.PlatePercent[0]==want&&!c.Boss.PlateDown[0],c.Name+": the plate follows "+hp+" / "+max+" ("+c.Boss.PlatePercent[0]+")");
                    var a=c.Cues.ArrivalAt(0);Check(c.Cues.ArrivalCount==1&&a.Spawned==spawned&&a.Z==spawn.z&&c.Intro(k0)==(c==late?0:1)&&c.Crack(k0)==(c==late?0:1),c.Name+": health never restarts the intro or the crack");
                }
            }
            // Death: DOWN at zero; then the doors open over the authority's second, sealed throughout; a joiner mid-opening.
            Check(w.Director.BossDefeated(Boss,new V3(0,0,8.6f)),"the boss dies");Client mid=null;
            Play(w,Dt,x=>{});var exit=w.Director.Closures.Single(x=>x.id==WardDoor);Check(exit.state=="opening","the authority opens the exit");
            Play(w,.5);mid=AddMember(w,"mid",4,new V3(-31,1,-22),.08,.04);Check(mid.View.closures.Single(x=>x.id==WardDoor).state=="opening","the mid joiner's first view is mid-opening");
            Play(w,Dt);Check(SealUp(mid.Scene,mid.Rig.Doors[0].Frame)&&NativeExpeditionStates.Shut(mid.View.closures.Single(x=>x.id==WardDoor),mid.T)<1,"a joiner mid-opening sees the leaves parting behind the seal, in step");
            Play(w,1.2);foreach(var c in new[]{host,guest,late,mid}){Check(c.Boss.PlateDown[0]&&c.Boss.PlateHealth[0]==0,c.Name+": DOWN once defeated");Check(!SealUp(c.Scene,c.Rig.Doors[0].Frame)&&c.View.closures.Single(x=>x.id==WardDoor).state=="open",c.Name+": the exit open");}

            // Back to the city: RETURNING; the final gate opens with one beacon cue per client; the lattice holds while it blocks.
            // One pilot does not vote: the exit runs its countdown (ExitSeconds), longer than the gate's 7 s cue.
            foreach(var a in new[]{"host","guest","late"})Check(w.Run.VoteExit(a,true),a+" votes to leave");
            Until(w,()=>host.Transfer.Active,NativeExpedition.ExitSeconds+1,"the return barrier");Check(host.Transfer.Title==ExpeditionTransferModel.Back,"returning to the city");
            Until(w,()=>!w.Director.Barrier.active&&w.Clients.All(c=>c.View.areaBarrier.active==false&&c.View.area==City),5,"the return completes");
            Play(w,1.5);
            // The gate opened while everyone was inside Ward Zero: each client sees the beacon and notice from their start, back in town.
            foreach(var c in w.Clients){Check(c.GateCues==1&&c.GateLate==0,c.Name+": the gate's beacon seen once, from its start, back in town ("+c.GateCues+" on time, "+c.GateLate+" late)");Check(c.View.closures.Single(x=>x.id==Gate).state=="open"&&!Lattice(c.Scene,c.Rig.Gates[0].Frame),c.Name+": the gate open");}
            var fresh=AddMember(w,"fresh",5,new V3(20,1,10),.1,.3); // positions later than views: placements arrive as jumpsPlay(w,.5);Check(fresh.GateCues==0&&fresh.GateLate==0,"a gate first seen open cues nothing");

            // The final boss in the city, then Victory.
            Walk(w,new Dictionary<string,V3>{["host"]=new V3(0,1,47),["guest"]=new V3(3,1,46),["late"]=new V3(-3,1,46),["mid"]=new V3(5,1,45),["fresh"]=new V3(-5,1,45)},12);
            Check(w.Director.TryBeginFinal(),"the final encounter begins");Check(w.Director.RecordBossArrival(Final,Hulk,a=>a==Hulk,new V3(0,0,47),300,300),"the final boss arrives");
            string f0="0:"+City+":"+Final;Play(w,4);foreach(var c in w.Clients)Check(c.Intro(f0)==1&&c.Crack(f0)==1&&c.Boss.PlateCount==1,c.Name+": the final boss's intro and crack once");
            Check(w.Director.UpdateBossHealth(City,Final,150,300),"final damage");Play(w,.4);foreach(var c in w.Clients)Check(c.Boss.PlatePercent[0]=="50%"&&c.Intro(f0)==1,c.Name+": final boss at 50%");
            Check(w.Director.FinalDefeated(new V3(0,0,47)),"victory");Play(w,.5);foreach(var c in w.Clients)Check(c.View.phase==ExpeditionPhase.Victory&&c.Boss.PlateDown[0],c.Name+": the final plate DOWN at victory");

            // 5. Continue: a fresh barrier into the same city, old arrivals gone, the gate sealing again, STARTING LOOP 2.
            ripples=w.Clients.Sum(c=>c.Ripples);foreach(var c in w.Clients)w.Run.VoteContinue(c.Account,true);
            Play(w,Dt);Check(w.Run.Snapshot().loop==1&&w.Director.Barrier.active&&w.Director.Barrier.target==City&&w.Director.Barrier.generation==3,"the next loop's fresh barrier");
            Until(w,()=>w.Clients.All(c=>c.View.loop==1),1,"every client sees loop 2");
            foreach(var c in w.Clients)
            {
                Check(c.Transfer.Active&&c.Transfer.Title=="STARTING LOOP 2",c.Name+": the transfer screen says STARTING LOOP 2 ("+c.Transfer.Title+")");
                Check(c.Cues.ArrivalCount==0&&c.Boss.PlateCount==0&&c.Boss.Intro<0,c.Name+": the old loop's plates and intros are gone");
                Check(Lattice(c.Scene,c.Rig.Gates[0].Frame)&&NativeExpeditionStates.Blocking(c.View.closures.Single(x=>x.id==Gate)),c.Name+": the gate blocks again behind its lattice");
            }
            Until(w,()=>!w.Director.Barrier.active&&w.Clients.All(c=>c.View.areaBarrier.active==false),5,"the loop's placement");Play(w,1.5);
            foreach(var c in w.Clients){Check(c.View.closures.Single(x=>x.id==Gate).state=="sealed"&&c.Fade.Alpha==0,c.Name+": the gate sealed again, the screen gone");Check(c.Tags.Count==5,c.Name+": SAFE over the whole party at the sanctuary slots ("+c.Tags.Count+")");}
            Check(w.Clients.Sum(c=>c.Ripples)==ripples,"the placement into the sanctuary is not a crossing ("+(w.Clients.Sum(c=>c.Ripples)-ripples)+" ripples)");

            // Loop 2 reuses the boss ids: each intro and crack plays again, once; the gate's beacon cues again.
            var party=new[]{"host","guest","late","mid","fresh"};
            Walk(w,party.Select((a,i)=>(a,new V3(42.2f+.2f*i,1,-1+.5f*i))).ToDictionary(p=>p.a,p=>p.Item2),12);
            Check(w.Director.EnterDoor("host",CityDoor),"enter again in loop 2");Until(w,()=>!w.Director.Barrier.active&&w.Clients.All(c=>c.View.area==Ward&&!c.View.areaBarrier.active),5,"loop 2 entry");Play(w,1.2);
            Check(w.Run.Progress("kill",1)&&w.Run.StartBosses(new[]{Boss})&&w.Director.RecordBossArrival(Boss,Hulk,a=>a==Hulk,spawn,100,100),"loop 2 boss arrives");Play(w,4);
            string k1="1:"+Ward+":"+Boss;foreach(var c in w.Clients)Check(c.Intro(k1)==1&&c.Crack(k1)==1&&c.Intro(k0)==(c==late||c==fresh||c==mid?0:1),c.Name+": loop 2's arrival with the same id gets its own intro and crack");
            Check(w.Director.BossDefeated(Boss,spawn),"loop 2 boss dies");Play(w,1.2);foreach(var a in party)w.Run.VoteExit(a,true);
            Until(w,()=>!w.Director.Barrier.active&&w.Clients.All(c=>c.View.area==City&&!c.View.areaBarrier.active),5,"loop 2 return");Play(w,1.5);
            foreach(var c in w.Clients)Check(c.GateCues==(c==fresh?1:2)&&c.GateLate==0,c.Name+": the gate's beacon cues again in loop 2, from its start ("+c.GateCues+" on time, "+c.GateLate+" late)");
            Walk(w,party.Select((a,i)=>(a,new V3(-4+2*i,1,46))).ToDictionary(p=>p.a,p=>p.Item2),12);
            Check(w.Director.TryBeginFinal()&&w.Director.RecordBossArrival(Final,Hulk,a=>a==Hulk,new V3(0,0,47),300,300),"loop 2 final boss");Play(w,4);
            string f1="1:"+City+":"+Final;foreach(var c in w.Clients)Check(c.Intro(f1)==1&&c.Crack(f1)==1&&c.Boss.PlateCount==1&&c.Boss.PlateHealth[0]==1,c.Name+": loop 2's final boss intro, crack and a fresh plate");
            foreach(var c in w.Clients)Check(c.Frames>1000,c.Name+" presented "+c.Frames+" frames");
        }
        private static Client AddMember(World w,string account,int seat,V3 at,double latency,double positions)
        {
            Check(w.Run.Join(account,"pistol"),"join "+account);w.Seats[account]=seat;w.At[account]=at;
            var c=Add(w,account,account,latency,positions);c.Map=w.Layout.Area(w.Run.Snapshot().area).map;return c;
        }

        // ---- 2. Binding: fingerprints, the arena's map, a guest's decoded startup view ---------------------------------------
        private static void Binding(ConversionContent content,ExpeditionLayout layout,string fingerprint)
        {
            var w=Start(content,layout,fingerprint,"bind-run");var v=NativeExpeditionView.Build(w.Run,"host",w.Session.Sources);
            Check(NativeExpeditionStates.Bound(v,fingerprint)&&!NativeExpeditionStates.Bound(v,new string('e',64))&&!NativeExpeditionStates.Bound(v,null),"only this manifest's fingerprint binds");
            var city=ExpeditionStateRig.For(layout,City);var ward=ExpeditionStateRig.For(layout,Ward);
            Check(city.Map=="expedition-city"&&ward.Map=="expedition-ward-zero"&&city.Doors.Length==1&&ward.Doors.Length==1&&city.Gates.Length==1,"the rigs name their maps");
            Check(NativeExpeditionStates.MapReady(city,"expedition-city")&&!NativeExpeditionStates.MapReady(ward,"expedition-city")&&!NativeExpeditionStates.MapReady(ward,null)&&NativeExpeditionStates.MapReady(null,"siren"),"world art only on the bound area's map; no rig keeps the anchor fallback");
            // A mismatched fingerprint: no rig, nothing in the world, no 3D leaf (the HUD hologram stands in).
            var scene=new NativeExpeditionStateScene();var cues=new ExpeditionStateCues();cues.Observe(v,v.elapsed);
            ExpeditionStateRig rig=NativeExpeditionStates.Bound(v,new string('e',64))?city:null;scene.Build(v,rig,cues,v.elapsed,EyeFor(City),false,false);
            Check(scene.Parts.Count==0&&scene.Quads.Count==0&&!scene.HasLeaf(CityDoor),"a mismatched layout draws nothing in the world");
            // The view names Ward Zero before the arena holds it: the host's frame right after EnterDoor.
            w.At["host"]=new V3(43,1,0);w.At["guest"]=new V3(42.6f,1,1);w.Director.Step();Check(w.Director.EnterDoor("host",CityDoor),"enter");
            var entering=NativeExpeditionView.Build(w.Run,"host",w.Session.Sources);var r=NativeExpeditionStates.Bound(entering,fingerprint)?ExpeditionStateRig.For(layout,entering.area):null;
            Check(entering.area==Ward&&r?.Area==Ward&&!NativeExpeditionStates.MapReady(r,"expedition-city"),"while the city is still loaded the Ward Zero rig (and every anchor of it) waits for its map");
            var t=new ExpeditionTransferModel();t.From(entering,new ExpeditionLabels());Check(t.Active&&t.Title=="ENTERING WARD ZERO"&&t.ReadyCount==0&&t.Required==2,"and the transfer screen is up");
            // Startup on a guest: the decoded public/private pair binds the same way.
            var g=Start(content,layout,fingerprint,"bind-guest");var receiver=new ExpeditionViewReceiver("guest");var batch=ExpeditionViewBatch.Build(g.Run,new[]{"guest"},g.Session.Sources);
            receiver.Public(JsonSerializer.Deserialize<ExpeditionView>(JsonSerializer.Serialize(batch.Public,Codec),Codec));receiver.Private(JsonSerializer.Deserialize<ExpeditionPrivateFrame>(JsonSerializer.Serialize(batch.Private.Single(),Codec),Codec));
            Check(receiver.View!=null&&NativeExpeditionStates.Bound(receiver.View,fingerprint)&&receiver.View.areaBarrier.generation==0&&!receiver.View.areaBarrier.active,"a guest's decoded startup view binds the town");
        }

        // ---- 3. SAFE tags: the seat, not the account; replaced members, reconnects; range, self and the claim ------------------
        private static void Seats(ConversionContent content,ExpeditionLayout layout,string fingerprint)
        {
            var w=Start(content,layout,fingerprint,"seat-run");var host=Add(w,"host","host",0,0);var guest=Add(w,"guest","guest",.12,.05);Play(w,.5);
            foreach(var c in new[]{host,guest})Check(c.Tags.Count==2&&c.Tags.Has(1)&&c.Tags.Has(2),c.Name+": a tag per safe pilot's seat");
            w.Seats["guest"]=6;Play(w,.4);foreach(var c in new[]{host,guest})Check(c.Tags.Has(6)&&!c.Tags.Has(2),c.Name+": a reassigned seat moves the tag to the pilot now in it");
            w.Seats["guest"]=0;Play(w,.4);foreach(var c in new[]{host,guest})Check(c.Tags.Count==1&&c.Tags.Has(1),c.Name+": an unavailable pilot (seat 0) has no tag");
            w.Seats["guest"]=2;w.Run.Disconnect("guest");Play(w,.4);Check(host.Tags.Count==1&&host.Tags.Has(1),"a disconnected member loses the tag");
            // Seat 2 handed to a replacement member standing outside: no tag (not safe), until the authority has them inside.
            Check(w.Run.Join("swap","pistol"),"a replacement joins");w.Seats["swap"]=2;w.At["swap"]=new V3(30,1,30);Play(w,.4);
            Check(!host.Tags.Has(2),"the replacement in seat 2 is outside: no tag over seat 2");
            w.At["swap"]=new V3(-2,1,2);Play(w,.4);Check(host.Tags.Has(2),"the replacement inside: SAFE over seat 2");
            w.Seats["swap"]=7;Check(w.Run.Join("guest","pistol"),"the guest reconnects");Play(w,.4);
            Check(host.Tags.Has(2)&&host.Tags.Has(7)&&host.Tags.Count==3,"the reconnected guest takes its seat and the authority's sanctuary flag ("+host.Tags.Count+")");
            Check(ExpeditionSafeTags.Alpha(10,false,true)==1&&ExpeditionSafeTags.Alpha(35,false,false)==1&&Math.Abs(ExpeditionSafeTags.Alpha(45,false,false)-.5f)<1e-5f,"full to 35 m, fading to 55 m");
            Check(ExpeditionSafeTags.Alpha(55.01f,false,false)==0&&ExpeditionSafeTags.Alpha(float.NaN,false,false)==0&&ExpeditionSafeTags.Alpha(-1,false,false)==0,"none past 55 m");
            Check(ExpeditionSafeTags.Alpha(3,true,true)==0&&ExpeditionSafeTags.Alpha(3,true,false)==1,"never over my own pilot in first person (third person shows it)");
        }

        // ---- Crossings: walks ripple, placements and respawns do not ----------------------------------------------------------
        private static void Crossings(ExpeditionLayout layout)
        {
            var rig=ExpeditionStateRig.For(layout,City);var cues=new ExpeditionStateCues();var v=new ExpeditionView{run="x",loop=0,areaBarrier=new ExpeditionAreaBarrierView()};
            int Count()=>cues.Ripples.Count(r=>!double.IsNegativeInfinity(r.At));double t=0;
            cues.Observe(v,t);for(float x=9;x<=15;x+=.2f){t+=Dt;cues.Pilot(1,x,0,0,rig,t);}Check(Count()==1,"walking across the edge: one ripple");
            cues.Pilot(1,0,0,40,rig,t+=Dt);cues.Pilot(1,2,0,2,rig,t+=Dt);Check(Count()==1,"a jump into the sanctuary (respawn, late position) is no crossing");
            for(float x=2;x<=14;x+=.2f){t+=Dt;cues.Pilot(1,x,0,2,rig,t);}Check(Count()==2,"walking out again ripples");
            v.loading=true;v.areaBarrier.active=true;cues.Observe(v,t);cues.Pilot(1,1,0,1,rig,t+=Dt);v.loading=false;v.areaBarrier.active=false;cues.Observe(v,t);
            cues.Pilot(1,1,0,1,rig,t+=Dt);cues.Pilot(1,1.1f,0,1,rig,t+=Dt);Check(Count()==2,"a placement behind the barrier is no crossing");
        }

        // ---- 6. The airlock keeps the doorway clear: nothing solid inside the closure face (the Ward exit's jambs bound it) ------
        private static void Clearance(string root,ExpeditionLayout layout)
        {
            using var maps=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")));
            foreach(var area in layout.areas)
            {
                var rig=ExpeditionStateRig.For(layout,area.id);
                for(int d=0;d<rig.Doors.Length;d++)
                {
                    var door=rig.Doors[d];var f=door.Frame;float half=f.W/2,dB=-f.D/2;
                    foreach(var (state,low) in new[]{("open",false),("open",true)})
                    {
                        var v=new ExpeditionView{area=area.id,closures=new[]{new ExpeditionClosureView{id=door.Anchor,area=area.id,state=state,began=0}}};
                        var scene=new NativeExpeditionStateScene();scene.Build(v,rig,null,10,Eye(f.Cx+f.Fx*6,1.7f,f.Cz+f.Fz*6,f.Cx,1.7f,f.Cz),false,low);Check(scene.Parts.Count>(low?16:20)&&scene.DoorsDrawn==1,"the unit draws ("+door.Anchor+", low "+low+": "+scene.Parts.Count+" parts)");
                        foreach(var b in scene.Parts)
                        {
                            float u=(b.X-f.Cx)*f.Ux+(b.Z-f.Cz)*f.Uz,dd=(b.X-f.Cx)*f.Fx+(b.Z-f.Cz)*f.Fz,vv=b.Y-f.Cy;
                            // The walkable opening: inside the face, above the 3 cm sill, from 10 cm off the wall face (no pilot's
                            // radius reaches closer; the airlock's back panel and light sit there) to 1.2 m in front.
                            bool inside=u+b.Hx>-half+1e-3f&&u-b.Hx<half-1e-3f&&vv+b.Hy>.031f&&vv-b.Hy<f.H-1e-3f&&dd+b.Hz>dB+.1f&&dd-b.Hz<f.D/2+1.2f;
                            Check(!inside,"nothing solid in "+door.Anchor+"'s open doorway (part at u "+u.ToString("0.00")+" ± "+b.Hx.ToString("0.00")+", v "+vv.ToString("0.00")+" ± "+b.Hy.ToString("0.00")+")");
                        }
                    }
                }
            }
            // The Ward exit's jambs and lintel bound exactly the closure face (the manifest's 3.2 x 3.4 doorway), so the unit's
            // pockets and header, which stop at the face, stay on the jambs and the lintel.
            var exit=ExpeditionStateRig.For(layout,Ward).Doors[0].Frame;var blocks=maps.RootElement.GetProperty("maps").EnumerateArray().Single(m=>m.GetProperty("id").GetString()=="expedition-ward-zero").GetProperty("blocks").EnumerateArray().ToArray();
            float Inner(string id,int sign){var b=blocks.Single(x=>x.GetProperty("id").GetString()==id);var p=b.GetProperty("pos");var s=b.GetProperty("size");float u=((float)p.GetProperty("x").GetDouble()-exit.Cx)*exit.Ux+((float)p.GetProperty("z").GetDouble()-exit.Cz)*exit.Uz;return u-sign*(float)s.GetProperty("z").GetDouble()/2;}
            float a=Inner("expedition-ward-zero-exit-jamb-s",Math.Sign(((float)-25.2-exit.Cz)*exit.Uz)),b2=Inner("expedition-ward-zero-exit-jamb-n",Math.Sign(((float)-21.6-exit.Cz)*exit.Uz));
            Check(Math.Abs(Math.Abs(a)-exit.W/2)<.02f&&Math.Abs(Math.Abs(b2)-exit.W/2)<.02f,"the Ward exit's jambs stand at the closure face's edges ("+a.ToString("0.00")+", "+b2.ToString("0.00")+" vs "+exit.W/2+")");
            var lintel=blocks.Single(x=>x.GetProperty("id").GetString()=="expedition-ward-zero-exit-lintel");float bottom=(float)(lintel.GetProperty("pos").GetProperty("y").GetDouble()-lintel.GetProperty("size").GetProperty("y").GetDouble()/2);
            Check(Math.Abs(bottom-(exit.Cy+exit.H))<.02f,"the lintel's underside is the closure's top ("+bottom+")");
        }

        // ---- 6. The frame budget on the real manifest: the busiest town frame stays inside capacity and allocates nothing --------
        private static void Budget(ConversionContent content,ExpeditionLayout layout,string fingerprint)
        {
            var w=Start(content,layout,fingerprint,"budget-run");w.Director.Step();var v=NativeExpeditionView.Build(w.Run,"host",w.Session.Sources);var rig=ExpeditionStateRig.For(layout,City);
            foreach(var c in v.closures)if(c.id==CityDoor){c.state="sealing";c.began=v.elapsed;}else if(c.id==Gate){c.state="opening";c.began=v.elapsed;}
            v.bossArrivals=new[]{new ExpeditionBossArrivalView{id=Final,area=City,archetype=Hulk,spawned=v.elapsed,x=0,y=0,z=47,hp=300,maxHp=300}};
            var cues=new ExpeditionStateCues();var scene=new NativeExpeditionStateScene();var clock=new ExpeditionPresentationClock();
            v.closures.Single(c=>c.id==Gate).state="sealed";cues.Observe(v,v.elapsed);v.closures.Single(c=>c.id==Gate).state="opening";
            int maxQ=0,maxP=0;double local=50;
            // Every pilot slot crossing the edge at once fills every ripple slot.
            void Frame(int i){local+=Dt;double t=clock.Observe(v,local);cues.Observe(v,t);for(int p=0;p<16;p++)cues.Pilot(p+1,(i+p)%2==0?11.6f:12.4f,0,-10+p,rig,t);scene.Build(v,rig,cues,t,Eye(6,2,20,0,3,37),false,false);maxQ=Math.Max(maxQ,scene.Quads.Count);maxP=Math.Max(maxP,scene.Parts.Count);}
            for(int i=0;i<60;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<120;i++)Frame(i);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,"the busiest town frame allocates nothing ("+bytes+" bytes)");
            Check(cues.Ripples.All(r=>!double.IsNegativeInfinity(r.At))&&cues.Cue(Gate,clock.Now,out _),"the measured frames rippled every slot and played the gate cue");
            Check(scene.CracksDrawn==0||scene.CracksDrawn==1,"crack bounded");Check(maxQ<NativeExpeditionStateScene.QuadCapacity*3/4&&maxP<NativeExpeditionStateScene.PartCapacity/2,"within capacity with headroom ("+maxQ+" / "+NativeExpeditionStateScene.QuadCapacity+" quads, "+maxP+" / "+NativeExpeditionStateScene.PartCapacity+" parts)");
            Console.WriteLine("  expedition acceptance budget: busiest town frame "+maxQ+" quads, "+maxP+" kit parts");
        }

        // ---- Sources: presentation never drives the authority; the SAFE pieces never claim protection --------------------------
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            foreach(var f in new[]{"Core/NativeExpeditionStates.cs","Core/NativeExpeditionStateScene.cs","Runtime/NativeExpeditionStatesPresentation.cs","Runtime/NativeExpeditionStatesHud.cs","Runtime/NativeExpeditionWorldPresentation.cs"})
            {
                var src=R(f);
                Check(!Regex.IsMatch(src,@"\b(ReportExpeditionAreaLoaded|CompleteAreaLoad|BeginAreaLoad|UpdateBossHealth|RecordBossArrival|SetSafe|ExpeditionAreaReady\.Accept)\s*\("),f+": presentation reports no load, placement, health or safety");
            }
            // Every string literal the SAFE chip, tags and the other state screens can show (code, not comments).
            int literals=0;
            foreach(var line in (R("Runtime/NativeExpeditionStatesHud.cs")+"\n"+R("Core/NativeExpeditionStates.cs")).Split('\n'))
            {
                string code=line.TrimStart();if(code.StartsWith("//"))continue;
                foreach(Match m in Regex.Matches(code,@"""([^""\\]*)"""))
                {literals++;Check(!Regex.IsMatch(m.Groups[1].Value,@"INVULN|PROTECT|IMMUNE|SHIELD|GOD ?MODE|UNTOUCHABLE",RegexOptions.IgnoreCase),"no state string claims protection: "+m.Value);}
            }
            Check(literals>20&&ExpeditionSafeTags.Text=="SAFE"&&ExpeditionSafeModel.Chip=="SAFE · SANCTUARY","the SAFE tag and chip read SAFE and SAFE · SANCTUARY");
        }

        // ---- Geometry helpers (as ExpeditionStatesChecks) -------------------------------------------------------------------
        private static ExpeditionEye Eye(float x,float y,float z,float tx,float ty,float tz)
        {
            float fx=tx-x,fy=ty-y,fz=tz-z,l=(float)Math.Sqrt(fx*fx+fy*fy+fz*fz);fx/=l;fy/=l;fz/=l;float rx=fz,rz=-fx,rl=(float)Math.Sqrt(rx*rx+rz*rz);rx/=rl;rz/=rl;
            return new ExpeditionEye{X=x,Y=y,Z=z,Rx=rx,Rz=rz,Ux=fy*rz,Uy=fz*rx-fx*rz,Uz=-fy*rx};
        }
        /// <summary>The door seal's fill: the quad covering exactly the closure face at the seal depth.</summary>
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
        /// <summary>The gate lattice's fill over the gate's face.</summary>
        private static bool Lattice(NativeExpeditionStateScene s,ExpeditionFrame f)
        {
            float d=f.D/2+.15f,h=f.W/2;f.Point(-h,0,d,out float ax,out _,out float az);f.Point(h,f.H,d,out float cx,out float cy,out float cz);var q=s.Quads;
            for(int i=0;i<q.Count;i++)if(q.Additive[i]==0&&Math.Abs(q.Corners[i*12]-ax)<1e-3f&&Math.Abs(q.Corners[i*12+2]-az)<1e-3f&&Math.Abs(q.Corners[i*12+6]-cx)<1e-3f&&Math.Abs(q.Corners[i*12+7]-cy)<1e-3f&&Math.Abs(q.Corners[i*12+8]-cz)<1e-3f)return true;
            return false;
        }
    }
}
#endif
