using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The join circles' presentation (Claude, 2026-10-17): Core NativeExpeditionJoinModel's mapping of the view's joins[] and
    // members[] to each circle's state (idle, charging, deploy at expiry, cancel when empty, held, cleared), the viewer's relation
    // and the party list; the pads' scene (NativeExpeditionJoinScene: no flat floor outline, the curtain on the counted radius, the
    // posts clear of the live town's geometry and records, low graphics and reduced effects); the world piece standing aside for
    // the pads; the hub's signs naming the live zones (no SEALED for a live route) and the entrances' dressing; no per-frame
    // allocation.
    internal static class ExpeditionJoinPresentationChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Join presentation: "+what);count++;}
        private sealed class Pilots:IExpeditionJoinPilots
        {
            public readonly Dictionary<string,V3> At=new Dictionary<string,V3>(StringComparer.Ordinal);
            public bool Feet(string account,int seat,out float x,out float y,out float z){if(account!=null&&At.TryGetValue(account,out var p)){x=p.x;y=p.y;z=p.z;return true;}x=y=z=0;return false;}
        }
        private sealed class Sink{public readonly List<float[]> Floor=new List<float[]>();}
        private static ExpeditionLayout layout;private static ExpeditionAreaLayout hub;
        private static readonly string[] Accounts={"nova","astra","kestrel","sheldon"};
        /// <summary>The hub's view as the authority projects it: the live layout's joins, doors and instances; `inside` per area as
        /// (count, started); members' atJoin and areas.</summary>
        private static ExpeditionView View(double elapsed,string viewer,Dictionary<string,(int inside,double started)> joins=null,string[] atJoin=null,Dictionary<string,string> areas=null,string[] cleared=null,Action<ExpeditionViewMember> edit=null)
        {
            var v=new ExpeditionView{run="r",world=layout.world,area=layout.world,phase=ExpeditionPhase.Town,elapsed=elapsed,viewer=viewer,revision=(long)(elapsed*1000)};
            v.instances=layout.instances.Select(i=>new ExpeditionViewInstance{id=i,target=40,cleared=cleared?.Contains(i)==true,entered=cleared?.Contains(i)==true}).ToArray();
            v.anchors=hub.anchors.Select(a=>a.Copy()).ToArray();
            v.joins=hub.doors.Where(d=>d.join!=null).Select(d=>{var a=hub.anchors.Single(x=>x.id==d.anchor);var j=new ExpeditionJoinView{doorId=d.anchor,area=a.building,x=d.join.x,y=d.join.y,z=d.join.z,r=d.join.r,duration=ExpeditionJoinRules.Duration};
                if(joins!=null&&joins.TryGetValue(a.building,out var s)){j.joinInside=s.inside;j.joinStartedAt=s.started;}return j;}).ToArray();
            int seat=1;v.members=Accounts.Select(a=>{var m=new ExpeditionViewMember{account=a,name=char.ToUpperInvariant(a[0])+a.Substring(1),area=areas!=null&&areas.TryGetValue(a,out var ar)?ar:layout.world,atJoin=atJoin?.Contains(a)==true,connected=true,alive=true,seat=seat++,host=a=="nova"};edit?.Invoke(m);return m;}).ToArray();
            v.host="nova";v.mine=new ExpeditionViewPersonal{account=viewer,connected=true,alive=true};return v;
        }
        private static int Ring(NativeExpeditionJoinModel m,string area){for(int i=0;i<m.RingCount;i++)if(m.Rings[i].Area==area)return i;throw new Exception("no ring "+area);}
        private static int Cues(NativeExpeditionJoinModel m,ExpeditionJoinCueKind k)=>Enumerable.Range(0,m.CueCount).Count(i=>m.Cues[i].Kind==k);

        internal static void Run(string root)
        {
            count=0;string res=Path.Combine(root,"Assets/Conversion/Resources");string text=File.ReadAllText(Path.Combine(res,"ExpeditionLayout.json"));
            layout=NativeExpeditionLayout.Parse(text);hub=layout.Area(layout.world);var labels=new ExpeditionLabels();
            const string St="exp-transit",Iw="exp-foundry",Wz="exp-ward-zero";
            var rings=hub.doors.Where(d=>d.join!=null).ToArray();Check(rings.Length==3,"the live hub has three join circles ("+rings.Length+")");
            var st=rings.Single(d=>hub.anchors.Single(a=>a.id==d.anchor).building==St).join;
            var pilots=new Pilots();V3 In(float dx,float dz)=>new V3(st.x+dx,0,st.z+dz);
            pilots.At["nova"]=In(-.8f,.6f);pilots.At["astra"]=In(.9f,-.7f);pilots.At["kestrel"]=In(3.6f,5.5f);pilots.At["sheldon"]=new V3(0,0,0);

            // ---- zones and names ----
            Check(NativeExpeditionPresentation.Name(St)=="CENTRAL STATION"&&NativeExpeditionPresentation.Name(Iw)=="CITY IRONWORKS"&&NativeExpeditionPresentation.Name(Wz)=="WARD ZERO","the screens name the zones (the loading screen, board and transfer screen read Name)");
            Check(NativeExpeditionJoinZones.Place(layout.world,layout.world)=="HUB"&&NativeExpeditionJoinZones.Place(St,layout.world)=="CENTRAL STATION"&&NativeExpeditionJoinZones.Place(Iw,layout.world)=="CITY IRONWORKS"&&NativeExpeditionJoinZones.Place(Wz,layout.world)=="WARD ZERO","party places");
            var accents=new[]{St,Iw,Wz}.Select(a=>NativeExpeditionJoinZones.Of(a).Accent).ToArray();Check(accents.Distinct().Count()==3&&!accents.Contains(NativeExpeditionJoinZones.HubHex),"one accent per zone, none the hub's");
            Check(P(new[]{St,Iw,Wz}).All(a=>NativeExpeditionJoinZones.Of(a).Threat.Length>0),"each zone has its threat line");

            // ---- idle ----
            var m=new NativeExpeditionJoinModel();m.Build(View(100,"nova"),100,pilots,labels);
            Check(m.InHub&&m.RingCount==3&&Enumerable.Range(0,3).All(i=>m.Rings[i].Phase==ExpeditionJoinPhase.Idle&&m.Rings[i].Status=="STEP IN TO DEPLOY"&&m.Rings[i].Power>0),"idle: three circles at rest, inviting");
            Check(m.Rings[Ring(m,St)].Title=="CENTRAL STATION"&&m.Rings[Ring(m,Iw)].Title=="CITY IRONWORKS"&&m.Rings[Ring(m,Wz)].Title=="WARD ZERO","idle: each names its zone");
            Check(m.CueCount==0&&m.ToastCount==0,"first sight cues nothing");
            int si=Ring(m,St);var sr=m.Rings[si];Check(Math.Abs(sr.Dz+1)<1e-3&&Math.Abs(sr.Az-1)<1e-3,"the station pad faces its door (south) and its approach (north)");
            // My pilot inside but not counted yet (the authority has not stepped): standing in it, not deploying.
            Check(m.Viewer==ExpeditionJoinViewer.NotCounted&&m.ViewerRing==si&&m.ViewerReason==NativeExpeditionJoinModel.Waiting,"in the circle but not counted reads so, with the reason");

            // ---- charging: two inside, one bystander outside ----
            var two=new Dictionary<string,(int,double)>{{St,(2,100)}};
            for(double t=100;t<=101.51;t+=.05)m.Build(View(t,"nova",two,new[]{"nova","astra"}),t,pilots,labels);
            var r=m.Rings[si];
            Check(r.Phase==ExpeditionJoinPhase.Charging&&Math.Abs(r.Charge-.5f)<.02f&&r.Seconds==2&&Math.Abs(r.Left-1.5f)<.06f&&r.Inside==2,"charging: the fill and the seconds from joinStartedAt over the duration ("+r.Charge+", "+r.Seconds+")");
            int nova=Enumerable.Range(0,m.PartyCount).Single(i=>m.Party[i].Account=="nova"),astra=Enumerable.Range(0,m.PartyCount).Single(i=>m.Party[i].Account=="astra"),kestrel=Enumerable.Range(0,m.PartyCount).Single(i=>m.Party[i].Account=="kestrel");
            Check(r.Pilots==(1<<nova|1<<astra)&&r.Names=="YOU · ASTRA"&&r.Status=="DEPLOYING IN 2","charging: who goes (YOU first)");
            Check(m.Viewer==ExpeditionJoinViewer.Deploying&&m.ViewerRing==si&&m.Flash==0,"charging: I am deploying; no flash yet");
            Check(m.Party[nova].Deploying&&m.Party[nova].Status=="DEPLOYING · 2s"&&m.Party[astra].Deploying&&!m.Party[kestrel].Deploying&&m.Party[kestrel].Ring<0,"the party list: the two inside deploy, the bystander does not");
            var other=new NativeExpeditionJoinModel();other.Build(View(101.5,"kestrel",two,new[]{"nova","astra"}),101.5,pilots,labels);var ro=other.Rings[Ring(other,St)];
            Check(ro.Names=="NOVA · ASTRA"&&other.Viewer==ExpeditionJoinViewer.Near&&!ro.Mine,"the bystander sees who goes and a prompt, never her own deploy");
            // The cues: one charge, a tick at each whole second (3 to 2, 2 to 1), none on the frames between.
            var cue=new NativeExpeditionJoinModel();int charge=0,ticks=0;
            cue.Build(View(99.9,"nova"),99.9,pilots,labels);
            for(double t=100;t<102.99;t+=1/60.0){cue.Build(View(Math.Floor(t*20)/20,"nova",two,new[]{"nova","astra"}),t,pilots,labels);charge+=Cues(cue,ExpeditionJoinCueKind.Charge);ticks+=Cues(cue,ExpeditionJoinCueKind.Tick);}
            Check(charge==1&&ticks==2,"cues: one charge and two ticks over the count ("+charge+", "+ticks+")");
            m.Build(View(102.9,"nova",two,new[]{"nova","astra"}),102.9,pilots,labels);Check(m.Flash>.2f&&m.Flash<1,"my flash rises over the last moment ("+m.Flash+")");
            // A late joiner sees the running count, not a start.
            var late=new NativeExpeditionJoinModel();late.Build(View(101,"sheldon",two,new[]{"nova","astra"}),101,pilots,labels);Check(Cues(late,ExpeditionJoinCueKind.Charge)==0&&late.Rings[Ring(late,St)].Phase==ExpeditionJoinPhase.Charging,"a late joiner: the count without a start cue");

            // ---- expiry: the next view shows the circle empty at or after joinStartedAt + duration: a deploy ----
            m.Build(View(102.98,"kestrel",two,new[]{"nova","astra"}),102.98,pilots,labels);
            var watcher=new NativeExpeditionJoinModel();watcher.Build(View(102.95,"kestrel",two,new[]{"nova","astra"}),102.95,pilots,labels);
            watcher.Build(View(103.0,"kestrel"),103.0,pilots,labels);r=watcher.Rings[Ring(watcher,St)];
            Check(r.Phase==ExpeditionJoinPhase.Deploying&&r.Taken==(1<<nova|1<<astra)&&Cues(watcher,ExpeditionJoinCueKind.Deploy)==1,"expiry: the circle deploys the two it counted");
            Check(watcher.ToastCount==1&&watcher.Toasts[0].Text=="NOVA · ASTRA DEPLOYED TO CENTRAL STATION","the hub hears who deployed where ("+(watcher.ToastCount>0?watcher.Toasts[0].Text:"none")+")");
            Check(watcher.Party[nova].EnRoute&&watcher.Party[nova].Status=="DEPLOYING"&&watcher.Party[nova].Place=="HUB","until their area changes they read as deploying, still in the hub");
            watcher.Build(View(103.5,"kestrel",null,null,new Dictionary<string,string>{{"nova",St},{"astra",St}}),103.5,pilots,labels);
            Check(watcher.Party[nova].Place=="CENTRAL STATION"&&!watcher.Party[nova].EnRoute&&watcher.ToastCount==1,"their area changes: placed in the zone, no second note");
            watcher.Build(View(104.8,"kestrel",null,null,new Dictionary<string,string>{{"nova",St},{"astra",St}}),104.8,pilots,labels);Check(watcher.Rings[Ring(watcher,St)].Phase==ExpeditionJoinPhase.Idle,"after the deploy the pad rests");
            // Coming back: the note, the line's flash and the arrival cue where the pilot appears.
            pilots.At["astra"]=new V3(-3,0,-72);watcher.Build(View(130,"kestrel",null,null,new Dictionary<string,string>{{"nova",St}}),130,pilots,labels);
            Check(watcher.Party[astra].Place=="HUB"&&watcher.Party[astra].Returned>=0&&watcher.Party[astra].Status=="BACK IN THE HUB"&&Cues(watcher,ExpeditionJoinCueKind.Arrive)==1&&watcher.Toasts[0].Text=="ASTRA RETURNED FROM CENTRAL STATION","a return: placed in the hub, flashed, noted, cued");
            pilots.At["astra"]=In(.9f,-.7f);
            // The deployer's own view: the flash falls away after the deploy (the loading screen cuts in).
            var me=new NativeExpeditionJoinModel();me.Build(View(102.95,"nova",two,new[]{"nova","astra"}),102.95,pilots,labels);me.Build(View(103.0,"nova"),103.0,pilots,labels);
            Check(me.Flash>.9f&&me.ToastCount==1&&me.Toasts[0].Text=="ASTRA DEPLOYED TO CENTRAL STATION","my deploy: full flash; the note names only the others");
            Check(me.Viewer==ExpeditionJoinViewer.Away&&me.Party[nova].EnRoute,"held in the hub after my deploy: no not-counted panel or prompt under the flash");

            // ---- cancel: empty before expiry ----
            var c=new NativeExpeditionJoinModel();c.Build(View(101.8,"kestrel",two,new[]{"nova","astra"}),101.8,pilots,labels);c.Build(View(101.85,"kestrel"),101.85,pilots,labels);r=c.Rings[Ring(c,St)];
            Check(r.Phase==ExpeditionJoinPhase.Cancelled&&r.Was>.55f&&Cues(c,ExpeditionJoinCueKind.Cancel)==1&&Cues(c,ExpeditionJoinCueKind.Deploy)==0&&c.ToastCount==0,"cancel: powers down from its charge, no deploy");
            NativeExpeditionJoinScene.CurtainOf(r,out float ch0,out _);c.Build(View(102.2,"kestrel"),102.2,pilots,labels);NativeExpeditionJoinScene.CurtainOf(c.Rings[Ring(c,St)],out float ch1,out _);
            Check(ch1<ch0,"cancel: the curtain drops");c.Build(View(103,"kestrel"),103,pilots,labels);Check(c.Rings[Ring(c,St)].Phase==ExpeditionJoinPhase.Idle,"cancel: then at rest");

            // ---- held: still occupied after expiry (the authority refused and restarts) ----
            var h=new NativeExpeditionJoinModel();h.Build(View(102.95,"nova",two,new[]{"nova","astra"}),102.95,pilots,labels);h.Build(View(103.0,"nova",new Dictionary<string,(int,double)>{{St,(2,-1)}},new[]{"nova","astra"}),103.0,pilots,labels);
            Check(Cues(h,ExpeditionJoinCueKind.Held)==1&&Cues(h,ExpeditionJoinCueKind.Deploy)==0&&h.Rings[Ring(h,St)].Hold>0,"held: no deploy, an amber hold");
            h.Build(View(103.02,"nova",new Dictionary<string,(int,double)>{{St,(2,103.02)}},new[]{"nova","astra"}),103.02,pilots,labels);
            Check(h.Rings[Ring(h,St)].Phase==ExpeditionJoinPhase.Charging&&h.Rings[Ring(h,St)].Hold>0&&Cues(h,ExpeditionJoinCueKind.Charge)==1,"held: the restarted count charges under the flicker");

            // ---- cleared ----
            var cl=new NativeExpeditionJoinModel();cl.Build(View(100,"nova",null,null,null,new[]{St}),100,pilots,labels);r=cl.Rings[Ring(cl,St)];
            NativeExpeditionJoinScene.CurtainOf(r,out _,out float ca);Check(r.Phase==ExpeditionJoinPhase.Cleared&&r.Status=="CLEARED"&&ca==0&&cl.Viewer==ExpeditionJoinViewer.Cleared,"cleared: no curtain, CLEARED, the viewer told");

            // ---- not counted reasons; near and away ----
            var n=new NativeExpeditionJoinModel();n.Build(View(100,"nova",null,null,null,null,x=>{if(x.account=="nova")x.choosing=true;}),100,pilots,labels);
            Check(n.Viewer==ExpeditionJoinViewer.NotCounted&&n.ViewerReason==NativeExpeditionJoinModel.Picking,"picking a chip: in it, not counted, why");
            n.Build(View(100,"nova",null,null,null,null,x=>{if(x.account=="nova")x.alive=false;}),100,pilots,labels);Check(n.ViewerReason==NativeExpeditionJoinModel.Down,"down: why");
            pilots.At["nova"]=In(0,st.r+4);n.Build(View(100,"nova"),100,pilots,labels);Check(n.Viewer==ExpeditionJoinViewer.Near,"4 m outside the edge: a prompt");
            pilots.At["nova"]=In(0,st.r+12);n.Build(View(100,"nova"),100,pilots,labels);Check(n.Viewer==ExpeditionJoinViewer.Away&&n.ViewerRing<0,"12 m outside: nothing");
            pilots.At["nova"]=In(-.8f,.6f);
            // In an instance: no circles, the party list still places everyone.
            var inside=View(100,"nova",null,null,new Dictionary<string,string>{{"nova",Wz},{"astra",Iw}});inside.area=Wz;inside.joins=Array.Empty<ExpeditionJoinView>();
            n.Build(inside,100,pilots,labels);Check(!n.InHub&&n.RingCount==0&&n.Party[nova].Place=="WARD ZERO"&&n.Party[astra].Place=="CITY IRONWORKS"&&n.Party[kestrel].Place=="HUB","in a zone: the party list says where each member is");
            n.Build(View(100,"nova",null,null,null,null,x=>{if(x.account=="sheldon"){x.connected=false;}if(x.account=="kestrel")x.alive=false;if(x.account=="astra")x.extracted=true;}),100,pilots,labels);
            Check(n.Party[3].Status=="OFFLINE"&&n.Party[kestrel].Status=="DOWN"&&n.Party[astra].Status=="EXTRACTED"&&n.Party[nova].You&&n.Party[nova].Host,"statuses: offline, down, extracted; mine marked");

            // ---- the pads ----
            var scene=new NativeExpeditionJoinScene();var eye=new ExpeditionEye{X=st.x+3,Y=1.8f,Z=st.z+9,Rx=-1,Ry=0,Rz=0,Ux=0,Uy=1,Uz=0};
            foreach(var (name,model) in new[]{("idle",Idle()),("charging",m2()),("deploying",watcherAt(103.1)),("cleared",cl)})
            {
                scene.Build(model,model==cl?100:101.5,eye,false,false);var q=scene.Quads;int ring=Ring(model,St);var rr=model.Rings[ring];
                Check(scene.PadsDrawn==3&&scene.Parts.Count>=Posts(3)&&q.Count>0,name+": three pads drawn ("+scene.Parts.Count+" parts, "+q.Count+" quads)");
                // No flat painted outline: whatever lies flat on the floor near a pad is a soft radial fill or inside the emitter
                // plate, except the deploy's shock ring (an event).
                int outline=0;for(int i=0;i<q.Count;i++){if(!Flat(q,i,rr.Y+.06f))continue;float d=Reach(q,i,rr.X,rr.Z);if(d>rr.R+6)continue;bool soft=q.Modes[i]==1;if(!soft&&d>.75f)outline++;}
                if(rr.Phase!=ExpeditionJoinPhase.Deploying)Check(outline==0,name+": no flat outline on the floor ("+outline+" quads)");
                if(rr.Phase!=ExpeditionJoinPhase.Cleared)Check(Curtain(q,rr)>=16,name+": the curtain stands exactly on the counted radius");else Check(Curtain(q,rr)==0,"cleared: no curtain");
            }
            {
                // The lock columns mark exactly the counted pilots.
                var mm=m2();scene.Build(mm,101.5,eye,false,false);Check(scene.PilotsMarked==2,"two counted pilots marked ("+scene.PilotsMarked+")");
                scene.Build(mm,101.5,eye,false,false,nova);Check(scene.PilotsMarked==2&&!Near(scene.Quads,pilots.At["nova"],.6f,2.2f),"my own column is not drawn in first person");
                int full=scene.Quads.Count;scene.Build(mm,101.5,eye,false,true);Check(scene.Quads.Count<full,"low graphics draws less ("+scene.Quads.Count+" < "+full+")");
                var a1=Snapshot(scene,Idle(),100,eye,true);var a2=Snapshot(scene,Idle(),101.7,eye,true);Check(a1.SequenceEqual(a2),"reduced effects: the idle pad holds still");
                var b1=Snapshot(scene,Idle(),100,eye,false);var b2=Snapshot(scene,Idle(),101.7,eye,false);Check(!b1.SequenceEqual(b2),"full effects: the idle pad lives");
                var far=new ExpeditionEye{X=0,Y=1.8f,Z=140,Rx=1,Uy=1};scene.Build(mm,101.5,far,false,false);Check(scene.PadsDrawn==0&&scene.Quads.Count>0,"from across the town: no pads, the sky beacons still lead there");
            }
            // The posts stand clear of the live town: no static block, return, door volume or the circle itself.
            var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(res,"UnityMaps.json")),new JsonSerializerOptions{IncludeFields=true});var city=maps.maps.Single(x=>x.id==hub.map);
            var idle=Idle();int posts=0;
            for(int i=0;i<idle.RingCount;i++)for(int k=0;k<NativeExpeditionJoinScene.Posts;k++)
            {
                var rr=idle.Rings[i];NativeExpeditionJoinScene.Post(rr,k,out float x,out float z,out _);posts++;
                Check(Math.Sqrt((x-rr.X)*(x-rr.X)+(z-rr.Z)*(z-rr.Z))>rr.R+.15,"post outside the counted radius");
                foreach(var b in city.blocks){if(b.id.Contains("-ground")||b.id.Contains("-slab")||b.id.Contains("paving"))continue;NativeExpeditionCityKit.Extent(b.pos.x,b.pos.y,b.pos.z,b.size.x/2,b.size.y/2,b.size.z/2,b.rotation?.x??0,b.rotation?.y??0,b.rotation?.z??0,b.rotation?.w??1,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
                    if(y1<.05||y0>1.3)continue;Check(x<x0-.25||x>x1+.25||z<z0-.25||z>z1+.25,rr.Area+" post "+k+" clear of "+b.id);}
                foreach(var d in hub.doors){foreach(var p in d.returns??Array.Empty<ExpeditionPoint>())Check(Math.Sqrt((x-p.x)*(x-p.x)+(z-p.z)*(z-p.z))>.9,rr.Area+" post "+k+" clear of a return");
                    var box=NativeExpeditionCityKit.Aabb(d.interact,.1f);Check(x<box[0]||x>box[1]||z<box[4]||z>box[5],rr.Area+" post "+k+" clear of the door's interaction volume");}
            }
            Check(posts==18,"six posts per pad");

            // ---- the world piece stands aside for the pads; without them the stand-in stays ----
            var world=new NativeExpeditionWorld();var cv=View(101.5,"nova",two,new[]{"nova","astra"});world.Build(cv);var piece=world.Pieces.Single(p=>p.anchor==hub.doors.Single(d=>d.join==st).anchor);
            Check(!piece.joinPad&&piece.joinRadius>0&&!piece.quiet,"no pads: the stand-in ring and label stay");
            world.JoinPadsDrawn=true;world.Build(cv);piece=world.Pieces.Single(p=>p.anchor==hub.doors.Single(d=>d.join==st).anchor);var pc=new PieceCount();NativeExpeditionWorld.Emit(pc,piece,1,false);
            Check(piece.joinPad&&piece.quiet&&pc.Vertices==0,"pads drawn: the door piece draws nothing and labels nothing");

            // ---- the hub's signs and entrances ----
            var maplooks=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(res,"MapLooks.json")),new JsonSerializerOptions{IncludeFields=true});
            var hubRecord=ExpeditionHubLayout.Parse(text,hub.id);var kit=NativeExpeditionCityKit.Build(city.blocks,hub,hubRecord,city.bounds);var words=kit.Lettering;
            Check(!words.Any(w=>w.Contains("SEALED")||w.Contains("STATION CLOSED")||w.Contains("EXP-")),"no live route's sign reads SEALED, CLOSED or a raw area id ("+string.Join(" | ",words.Where(w=>w.Contains("SEALED")||w.Contains("CLOSED")||w.Contains("EXP-")))+")");
            Check(words.Any(w=>w.StartsWith("CENTRAL STATION ",StringComparison.Ordinal)&&w.EndsWith(" M",StringComparison.Ordinal))&&words.Any(w=>w.StartsWith("CITY IRONWORKS ",StringComparison.Ordinal)&&w.EndsWith(" M",StringComparison.Ordinal))&&words.Any(w=>w.StartsWith("WARD ZERO ",StringComparison.Ordinal)),"the gate headers name each zone and its distance");
            Check(words.Contains("NO 1 GATE")&&words.Contains("CITY IRONWORKS")&&words.Contains("TRANSIT"),"the entrances' own signs are drawn (the foundry gate, its lettering, the station canopy)");
            foreach(var to in new[]{St,Iw})Check(kit.Glows.Any(g=>{var d=hub.doors.Single(x=>hub.anchors.Single(a=>a.id==x.anchor).building==to);return Math.Abs(g.X-d.join.x)<.01&&Math.Abs(g.Z-d.join.z)<.01;}),to+": a light at its circle");
            Check(NativeExpeditionCityKit.RouteSign(new ExpeditionHubRoute{To=Iw,Reserved=true},false)=="IRONWORKS SEALED"&&NativeExpeditionCityKit.RouteSign(new ExpeditionHubRoute{To=St},false)=="CENTRAL STATION"&&NativeExpeditionCityKit.RouteSign(new ExpeditionHubRoute{To=St},true)=="STATION","a reserved route still reads SEALED; a live one its name");
            // The reserved layout (exp-layout-3's reserved doorways) keeps the sealed frontage.
            var sealedHub=ExpeditionHubLayout.Parse(text,hub.id);foreach(var rt in sealedHub.Routes)if(rt.To!=Wz)rt.Reserved=true;
            var sealedKit=NativeExpeditionCityKit.Build(city.blocks,hub,sealedHub,city.bounds);Check(!sealedKit.Lettering.Contains("NO 1 GATE")&&sealedKit.Lettering.Any(w=>w.StartsWith("STATION SEALED",StringComparison.Ordinal)||w.Contains("SEALED")),"a reserved route keeps its sealed signs");

            // ---- no per-frame allocation (warm: the same sequence again) ----
            var hot=new NativeExpeditionJoinModel();var hs=new NativeExpeditionJoinScene();var seq=Sequence(two);
            for(int pass=0;pass<2;pass++)
            {
                hot.Reset();long before=GC.GetAllocatedBytesForCurrentThread();
                foreach(var (view,t) in seq){hot.Build(view,t,pilots,labels);hs.Build(hot,t,eye,false,false);hs.Build(hot,t,eye,true,true);}
                long spent=GC.GetAllocatedBytesForCurrentThread()-before;if(pass==1)Check(spent==0,"the circles, the party and the pads allocate nothing per frame once warm ("+spent+" bytes over "+seq.Count+" frames)");
            }
            Console.WriteLine("PASS expedition join presentation ("+count+" checks: zones and names; idle, charging, ticks, my flash, deploy at expiry with its note and en-route rows, return note and cue, cancel, held, cleared; not counted, near, away; the party list's places and states; pads with no flat outline, the curtain on the counted radius, counted pilots marked, my column hidden in first person, low graphics and reduced effects, beacons from afar; posts clear of the town's blocks, returns and door volumes; the world piece stands aside; live signs and entrances, sealed kept for a reserved route; no per-frame allocation)");

            NativeExpeditionJoinModel Idle(){var x=new NativeExpeditionJoinModel();x.Build(View(100,"kestrel"),100,pilots,labels);return x;}
            NativeExpeditionJoinModel m2(){var x=new NativeExpeditionJoinModel();x.Build(View(101.5,"nova",two,new[]{"nova","astra"}),101.5,pilots,labels);return x;}
            NativeExpeditionJoinModel watcherAt(double t){var x=new NativeExpeditionJoinModel();x.Build(View(102.95,"kestrel",two,new[]{"nova","astra"}),102.95,pilots,labels);x.Build(View(103,"kestrel"),t,pilots,labels);return x;}
        }
        private static IEnumerable<string> P(string[] s)=>s;
        private static int Posts(int pads)=>pads*NativeExpeditionJoinScene.Posts*4;
        private sealed class PieceCount:IExpeditionWorldSink{public int Vertices;public int Vertex(float x,float y,float z,float u,float v,float flat,ExpeditionTone tone,float level,float alpha)=>Vertices++;public void Triangle(int a,int b,int c){}}
        private static bool Flat(NativeFxQuads q,int i,float top){int o=i*12;float y0=q.Corners[o+1];for(int k=0;k<4;k++){float y=q.Corners[o+k*3+1];if(Math.Abs(y-y0)>1e-4f||y>top)return false;}return true;}
        private static float Reach(NativeFxQuads q,int i,float cx,float cz){int o=i*12;float best=0;for(int k=0;k<4;k++){float dx=q.Corners[o+k*3]-cx,dz=q.Corners[o+k*3+2]-cz;best=Math.Max(best,(float)Math.Sqrt(dx*dx+dz*dz));}return best;}
        /// <summary>Vertical quads whose four corners lie on the counted radius (the curtain).</summary>
        private static int Curtain(NativeFxQuads q,in ExpeditionJoinRing r)
        {
            int n=0;for(int i=0;i<q.Count;i++){int o=i*12;bool on=true;float ymin=float.MaxValue,ymax=float.MinValue;
                for(int k=0;k<4;k++){float dx=q.Corners[o+k*3]-r.X,dz=q.Corners[o+k*3+2]-r.Z,y=q.Corners[o+k*3+1];ymin=Math.Min(ymin,y);ymax=Math.Max(ymax,y);if(Math.Abs(Math.Sqrt(dx*dx+dz*dz)-r.R)>1e-3)on=false;}
                if(on&&ymax-ymin>.1f)n++;}
            return n;
        }
        private static bool Near(NativeFxQuads q,V3 p,float radius,float below)
        {
            // A quad round the pilot (every corner within `radius`), below `below` and rising off the floor: a column of its own.
            for(int i=0;i<q.Count;i++){int o=i*12;bool all=true;float top=float.MinValue;for(int k=0;k<4;k++){float dx=q.Corners[o+k*3]-p.x,dz=q.Corners[o+k*3+2]-p.z,y=q.Corners[o+k*3+1]-p.y;top=Math.Max(top,y);if(Math.Sqrt(dx*dx+dz*dz)>radius||y>below)all=false;}if(all&&top>.3f)return true;}
            return false;
        }
        private static float[] Snapshot(NativeExpeditionJoinScene s,NativeExpeditionJoinModel m,double t,ExpeditionEye eye,bool reduced)
        {s.Build(m,t,eye,reduced,false);var o=new float[s.Quads.Count*16];for(int i=0;i<s.Quads.Count;i++){for(int k=0;k<12;k++)o[i*16+k]=(float)Math.Round(s.Quads.Corners[i*12+k],4);for(int k=0;k<4;k++)o[i*16+12+k]=(float)Math.Round(s.Quads.Colors[i*4+k],4);}return o;}
        /// <summary>A minute of hub frames at 60 Hz with 20 Hz views: rest, a count, a deploy, a return, a cancel.</summary>
        private static List<(ExpeditionView,double)> Sequence(Dictionary<string,(int,double)> two)
        {
            var o=new List<(ExpeditionView,double)>();ExpeditionView v=null;double lastView=-1;
            for(int f=0;f<600;f++)
            {
                double t=98+f/60.0;
                if(Math.Floor(t*20)!=Math.Floor(lastView*20))
                {
                    double e=Math.Floor(t*20)/20;
                    if(e<100)v=View(e,"kestrel");
                    else if(e<103)v=View(e,"kestrel",two,new[]{"nova","astra"});
                    else if(e<104)v=View(e,"kestrel");
                    else if(e<104.5)v=View(e,"kestrel",null,null,new Dictionary<string,string>{{"nova","exp-transit"},{"astra","exp-transit"}});
                    else if(e<105)v=View(e,"kestrel",new Dictionary<string,(int,double)>{{"exp-foundry",(1,104.5)}},new[]{"astra"});
                    else v=View(e,"kestrel");
                    lastView=t;
                }
                o.Add((v,t));
            }
            return o;
        }
    }
}
