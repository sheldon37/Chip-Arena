using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The join circles' presentation (Claude, 2026-10-17): Core NativeExpeditionJoinModel's mapping of the view's joins[] and
    // members[] to each circle's state (idle, charging, a deploy from the authority's deploy record with its first sight a
    // baseline, cancel when empty (a step-out at the deadline too), held, closed by closedReason: cleared, locked, reserved),
    // the counted height h with its fallback, the viewer's relation and the party list with LOADING rows (pendingArea); the
    // pads' scene (NativeExpeditionJoinScene: no flat floor outline, the curtain on the counted radius, the
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
        /// <summary>The hub's view as the authority projects it: the live layout's joins (with h), doors and instances; `inside`
        /// per area as (count, started); members' atJoin and areas; `deployed` per area as its deploy record (at, accounts), kept
        /// as the authority keeps it; a cleared instance's circle closed as Cleared (the live policy), `closed` any other reason.</summary>
        private static ExpeditionView View(double elapsed,string viewer,Dictionary<string,(int inside,double started)> joins=null,string[] atJoin=null,Dictionary<string,string> areas=null,string[] cleared=null,Action<ExpeditionViewMember> edit=null,
            Dictionary<string,(double at,string[] who)> deployed=null,Dictionary<string,ExpeditionJoinClosedReason> closed=null)
        {
            var v=new ExpeditionView{run="r",world=layout.world,area=layout.world,phase=ExpeditionPhase.Town,elapsed=elapsed,viewer=viewer,revision=(long)(elapsed*1000)};
            v.instances=layout.instances.Select(i=>new ExpeditionViewInstance{id=i,target=40,cleared=cleared?.Contains(i)==true,entered=cleared?.Contains(i)==true}).ToArray();
            v.anchors=hub.anchors.Select(a=>a.Copy()).ToArray();
            v.joins=hub.doors.Where(d=>d.join!=null).Select(d=>{var a=hub.anchors.Single(x=>x.id==d.anchor);var j=new ExpeditionJoinView{doorId=d.anchor,area=a.building,x=d.join.x,y=d.join.y,z=d.join.z,r=d.join.r,h=d.join.h,duration=ExpeditionJoinRules.Duration};
                if(joins!=null&&joins.TryGetValue(a.building,out var s)){j.joinInside=s.inside;j.joinStartedAt=s.started;}
                if(deployed!=null&&deployed.TryGetValue(a.building,out var rec)){j.deployedAt=rec.at;j.deployedAccounts=(string[])rec.who.Clone();}
                if(cleared?.Contains(a.building)==true)j.closedReason=ExpeditionJoinClosedReason.Cleared;
                if(closed!=null&&closed.TryGetValue(a.building,out var why))j.closedReason=why;return j;}).ToArray();
            int seat=1;v.members=Accounts.Select(a=>{var m=new ExpeditionViewMember{account=a,name=char.ToUpperInvariant(a[0])+a.Substring(1),area=areas!=null&&areas.TryGetValue(a,out var ar)?ar:layout.world,atJoin=atJoin?.Contains(a)==true,connected=true,alive=true,seat=seat++,host=a=="nova"};edit?.Invoke(m);return m;}).ToArray();
            v.host="nova";v.mine=new ExpeditionViewPersonal{account=viewer,connected=true,alive=true};return v;
        }
        private static int Ring(NativeExpeditionJoinModel m,string area){for(int i=0;i<m.RingCount;i++)if(m.Rings[i].Area==area)return i;throw new Exception("no ring "+area);}
        private static int Cues(NativeExpeditionJoinModel m,ExpeditionJoinCueKind k)=>Enumerable.Range(0,m.CueCount).Count(i=>m.Cues[i].Kind==k);
        private static int Row(NativeExpeditionJoinModel m,string account)=>Enumerable.Range(0,m.PartyCount).Single(i=>m.Party[i].Account==account);
        /// <summary>These members' transfers loading into `area` (members[].pendingArea), the rest of the view as built.</summary>
        private static Action<ExpeditionViewMember> Loading(string area,params string[] who)=>x=>{if(who.Contains(x.account))x.pendingArea=area;};

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

            // ---- a deploy: the authority's record (deployedAt, deployedAccounts), never an empty circle at the deadline ----
            var took=new Dictionary<string,(double,string[])>{{St,(103.0,new[]{"astra","nova"})}};var loadSt=Loading(St,"nova","astra");
            m.Build(View(102.98,"kestrel",two,new[]{"nova","astra"}),102.98,pilots,labels);
            var watcher=new NativeExpeditionJoinModel();watcher.Build(View(102.95,"kestrel",two,new[]{"nova","astra"}),102.95,pilots,labels);
            watcher.Build(View(103.0,"kestrel",null,null,null,null,loadSt,took),103.0,pilots,labels);r=watcher.Rings[Ring(watcher,St)];
            Check(r.Phase==ExpeditionJoinPhase.Deploying&&r.Taken==(1<<nova|1<<astra)&&Cues(watcher,ExpeditionJoinCueKind.Deploy)==1,"a new deploy record: the circle deploys exactly its deployedAccounts");
            Check(watcher.ToastCount==1&&watcher.Toasts[0].Text=="NOVA · ASTRA DEPLOYED TO CENTRAL STATION","the hub hears who deployed where ("+(watcher.ToastCount>0?watcher.Toasts[0].Text:"none")+")");
            Check(watcher.Party[nova].EnRoute&&watcher.Party[nova].Loading&&watcher.Party[nova].Status=="LOADING · CENTRAL STATION"&&watcher.Party[nova].Place=="HUB"&&watcher.Party[astra].Status=="LOADING · CENTRAL STATION","the deployed rows: LOADING into the zone while their place stays HUB");
            Check(watcher.Flash==0&&watcher.Viewer!=ExpeditionJoinViewer.Deploying,"a bystander: no flash of their own");
            // The authority keeps the record across later views: it is not a second deploy.
            watcher.Build(View(103.05,"kestrel",null,null,null,null,loadSt,took),103.05,pilots,labels);Check(Cues(watcher,ExpeditionJoinCueKind.Deploy)==0&&watcher.ToastCount==1,"the kept record: no second deploy");
            watcher.Build(View(103.5,"kestrel",null,null,new Dictionary<string,string>{{"nova",St},{"astra",St}},null,null,took),103.5,pilots,labels);
            Check(watcher.Party[nova].Place=="CENTRAL STATION"&&!watcher.Party[nova].EnRoute&&!watcher.Party[nova].Loading&&watcher.ToastCount==1&&Cues(watcher,ExpeditionJoinCueKind.Deploy)==0,"the authority commits: placed in the zone, no second note");
            watcher.Build(View(104.8,"kestrel",null,null,new Dictionary<string,string>{{"nova",St},{"astra",St}},null,null,took),104.8,pilots,labels);Check(watcher.Rings[Ring(watcher,St)].Phase==ExpeditionJoinPhase.Idle,"after the deploy the pad rests");
            // Coming back: the note, the line's flash and the arrival cue where the pilot appears.
            pilots.At["astra"]=new V3(-3,0,-72);watcher.Build(View(130,"kestrel",null,null,new Dictionary<string,string>{{"nova",St}},null,null,took),130,pilots,labels);
            Check(watcher.Party[astra].Place=="HUB"&&watcher.Party[astra].Returned>=0&&watcher.Party[astra].Status=="BACK IN THE HUB"&&Cues(watcher,ExpeditionJoinCueKind.Arrive)==1&&watcher.Toasts[0].Text=="ASTRA RETURNED FROM CENTRAL STATION","a return: placed in the hub, flashed, noted, cued");
            pilots.At["astra"]=In(.9f,-.7f);
            // The deployer's own view: the flash falls away after the deploy (the loading screen cuts in).
            var me=new NativeExpeditionJoinModel();me.Build(View(102.95,"nova",two,new[]{"nova","astra"}),102.95,pilots,labels);me.Build(View(103.0,"nova",null,null,null,null,loadSt,took),103.0,pilots,labels);
            Check(me.Flash>.9f&&me.ToastCount==1&&me.Toasts[0].Text=="ASTRA DEPLOYED TO CENTRAL STATION","my deploy: full flash; the note names only the others");
            Check(me.Viewer==ExpeditionJoinViewer.Away&&me.Party[nova].EnRoute&&me.Party[nova].Status=="LOADING · CENTRAL STATION","held in the hub after my deploy: no not-counted panel or prompt under the flash; my row loading");
            // Standing in the circle but not in the record: no flash, the note names both.
            pilots.At["kestrel"]=In(.2f,.1f);var by=new NativeExpeditionJoinModel();by.Build(View(102.95,"kestrel",two,new[]{"nova","astra"}),102.95,pilots,labels);by.Build(View(103.0,"kestrel",null,null,null,null,loadSt,took),103.0,pilots,labels);
            Check(by.Flash==0&&by.Rings[Ring(by,St)].Phase==ExpeditionJoinPhase.Deploying&&(by.Rings[Ring(by,St)].Taken&1<<kestrel)==0&&by.Toasts[0].Text=="NOVA · ASTRA DEPLOYED TO CENTRAL STATION"&&!by.Party[kestrel].EnRoute,"in the circle, not in deployedAccounts: no flash, not on the way");
            pilots.At["kestrel"]=In(3.6f,5.5f);
            // A step-out in the last view interval: empty at the deadline with no new record is a cancel, never a deploy.
            foreach(var who in new[]{"kestrel","nova"})
            {
                var so=new NativeExpeditionJoinModel();so.Build(View(102.97,who,two,new[]{"nova","astra"}),102.97,pilots,labels);so.Build(View(103.0,who),103.0,pilots,labels);r=so.Rings[Ring(so,St)];
                Check(r.Phase==ExpeditionJoinPhase.Cancelled&&r.Taken==0&&Cues(so,ExpeditionJoinCueKind.Deploy)==0&&Cues(so,ExpeditionJoinCueKind.Cancel)==1&&so.ToastCount==0&&!so.Party[nova].EnRoute&&!so.Party[astra].EnRoute&&so.Flash==0,"a step-out at the deadline ("+who+"): cancelled, no deploy, no note, nobody on the way, no flash");
            }
            // The first record seen is a baseline: a pilot who reaches the hub after a deploy sees no replay, the next record plays.
            var late2=new NativeExpeditionJoinModel();late2.Build(View(103.0,"sheldon",null,null,null,null,loadSt,took),103.0,pilots,labels);r=late2.Rings[Ring(late2,St)];
            Check(r.Phase==ExpeditionJoinPhase.Idle&&Cues(late2,ExpeditionJoinCueKind.Deploy)==0&&late2.ToastCount==0&&!late2.Party[nova].EnRoute,"a late joiner: the record it first sees is no deploy");
            Check(late2.Party[nova].Loading&&late2.Party[nova].Status=="LOADING · CENTRAL STATION","a late joiner still sees who is loading (pendingArea)");
            late2.Build(View(103.05,"sheldon",null,null,null,null,loadSt,took),103.05,pilots,labels);Check(Cues(late2,ExpeditionJoinCueKind.Deploy)==0,"the baseline record kept: still no deploy");
            late2.Build(View(106.0,"sheldon",null,null,null,null,Loading(St,"kestrel"),new Dictionary<string,(double,string[])>{{St,(106.0,new[]{"kestrel"})}}),106.0,pilots,labels);r=late2.Rings[Ring(late2,St)];
            Check(r.Phase==ExpeditionJoinPhase.Deploying&&r.Taken==1<<kestrel&&Cues(late2,ExpeditionJoinCueKind.Deploy)==1&&late2.Toasts[0].Text=="KESTREL DEPLOYED TO CENTRAL STATION","after the baseline the next record deploys");
            // Back from a zone: the deploys made while away are not replayed.
            var away=new NativeExpeditionJoinModel();away.Build(View(100,"sheldon"),100,pilots,labels);
            var inZone=View(101,"sheldon",null,null,new Dictionary<string,string>{{"sheldon",Wz}});inZone.area=Wz;inZone.joins=Array.Empty<ExpeditionJoinView>();away.Build(inZone,101,pilots,labels);
            away.Build(View(103.2,"sheldon",null,null,null,null,loadSt,took),103.2,pilots,labels);
            Check(away.Rings[Ring(away,St)].Phase==ExpeditionJoinPhase.Idle&&Cues(away,ExpeditionJoinCueKind.Deploy)==0&&away.ToastCount==0,"back in the hub from a zone: a deploy made meanwhile is a baseline");
            // A record already old on the view's clock (the circles unseen for a while: a menu) is adopted, not played late.
            var stale=new NativeExpeditionJoinModel();stale.Build(View(100,"kestrel"),100,pilots,labels);stale.Build(View(110,"kestrel",null,null,null,null,null,took),110,pilots,labels);
            Check(Cues(stale,ExpeditionJoinCueKind.Deploy)==0&&stale.ToastCount==0&&stale.Rings[Ring(stale,St)].Phase==ExpeditionJoinPhase.Idle,"a record seen "+(110-103)+" s late: no deploy");
            // A reset record (deployedAt back to -1) is no deploy either; a record after it is.
            stale.Build(View(111,"kestrel"),111,pilots,labels);Check(Cues(stale,ExpeditionJoinCueKind.Deploy)==0,"a reset record: no deploy");
            stale.Build(View(112,"kestrel",null,null,null,null,null,new Dictionary<string,(double,string[])>{{Iw,(112,new[]{"sheldon"})}}),112,pilots,labels);Check(Cues(stale,ExpeditionJoinCueKind.Deploy)==1&&stale.Rings[Ring(stale,Iw)].Phase==ExpeditionJoinPhase.Deploying&&stale.Rings[Ring(stale,St)].Phase==ExpeditionJoinPhase.Idle,"a record on another circle deploys that circle only");
            // A transfer whose load is cancelled: no longer on the way once its pendingArea clears without a move.
            var cancelled=new NativeExpeditionJoinModel();cancelled.Build(View(102.95,"kestrel",two,new[]{"nova","astra"}),102.95,pilots,labels);cancelled.Build(View(103.0,"kestrel",null,null,null,null,loadSt,took),103.0,pilots,labels);
            cancelled.Build(View(104,"kestrel",null,null,null,null,null,took),104,pilots,labels);
            Check(!cancelled.Party[nova].EnRoute&&!cancelled.Party[nova].Loading&&cancelled.Party[nova].Place=="HUB"&&cancelled.Party[nova].Status!="DEPLOYING","a cancelled load: back to an ordinary hub row ("+cancelled.Party[nova].Status+")");

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

            // ---- the counted height: joins[].h, the fallback only without it ----
            Check(m.Rings[si].H==st.h&&st.h==4,"the counted height is joins[].h ("+m.Rings[si].H+")");
            var noH=View(100,"nova");foreach(var j in noH.joins)j.h=0;var fh=new NativeExpeditionJoinModel();fh.Build(noH,100,pilots,labels);Check(fh.Rings[Ring(fh,St)].H==NativeExpeditionJoinModel.FallbackHeight,"a snapshot without h: the 4.5 m fallback");
            {
                var full=new ExpeditionJoinRing{Phase=ExpeditionJoinPhase.Charging,Charge=1,H=st.h};NativeExpeditionJoinScene.CurtainOf(full,out float fhgt,out _);
                var flashR=new ExpeditionJoinRing{Phase=ExpeditionJoinPhase.Deploying,Age=.1f,H=st.h};NativeExpeditionJoinScene.CurtainOf(flashR,out float dhgt,out _);
                var fbR=new ExpeditionJoinRing{Phase=ExpeditionJoinPhase.Charging,Charge=1,H=fh.Rings[Ring(fh,St)].H};NativeExpeditionJoinScene.CurtainOf(fbR,out float bhgt,out _);
                Check(Math.Abs(fhgt-st.h)<1e-4&&Math.Abs(dhgt-st.h)<1e-4&&Math.Abs(bhgt-NativeExpeditionJoinModel.FallbackHeight)<1e-4,"the curtain rises to the counted height and flashes over it ("+fhgt+", "+dhgt+"; fallback "+bhgt+")");
            }
            // The column is the authority's: a capsule centre (feet + 0.85 m) above h is not in the circle; under the fallback it is.
            pilots.At["nova"]=new V3(st.x-.8f,st.y+st.h-ExpeditionJoinRules.CapsuleCentreHeight+.2f,st.z+.6f);
            var tall=new NativeExpeditionJoinModel();tall.Build(View(100,"nova"),100,pilots,labels);Check(tall.Viewer==ExpeditionJoinViewer.Near,"over the counted height: near, not in the circle ("+tall.Viewer+")");
            tall.Build(noH,100.05,pilots,labels);Check(tall.Viewer==ExpeditionJoinViewer.NotCounted,"the same pilot under the 4.5 m fallback: in it ("+tall.Viewer+")");
            pilots.At["nova"]=new V3(st.x-.8f,st.y+st.h-ExpeditionJoinRules.CapsuleCentreHeight-.2f,st.z+.6f);tall.Build(View(100.1,"nova"),100.1,pilots,labels);Check(tall.Viewer==ExpeditionJoinViewer.NotCounted,"just under the counted height: in it");
            pilots.At["nova"]=In(-.8f,.6f);

            // ---- closed circles: closedReason (only Open counts) ----
            var cl=new NativeExpeditionJoinModel();cl.Build(View(100,"nova",null,null,null,new[]{St}),100,pilots,labels);r=cl.Rings[Ring(cl,St)];
            NativeExpeditionJoinScene.CurtainOf(r,out _,out float ca);Check(r.Phase==ExpeditionJoinPhase.Cleared&&r.Closure==ExpeditionJoinClosedReason.Cleared&&r.Status=="CLEARED"&&ca==0&&cl.Viewer==ExpeditionJoinViewer.Cleared&&NativeExpeditionJoinScene.Light(r)==NativeExpeditionJoinScene.GreenHex,"cleared: no curtain, green CLEARED, the viewer told");
            {
                // The exact field wins over instances[].cleared; a snapshot without the join fields still closes a cleared area.
                var reopened=View(100,"nova",null,null,null,new[]{St});foreach(var j in reopened.joins)j.closedReason=ExpeditionJoinClosedReason.Open;var ro2=new NativeExpeditionJoinModel();ro2.Build(reopened,100,pilots,labels);
                Check(ro2.Rings[Ring(ro2,St)].Phase==ExpeditionJoinPhase.Idle,"closedReason Open with h: open, whatever instances[].cleared says");
                foreach(var j in reopened.joins)j.h=0;ro2=new NativeExpeditionJoinModel();ro2.Build(reopened,100,pilots,labels);Check(ro2.Rings[Ring(ro2,St)].Phase==ExpeditionJoinPhase.Cleared,"an old snapshot (no h, no closedReason): a cleared area's circle still reads cleared");
            }
            var stray=new Dictionary<string,(int,double)>{{St,(2,100)}};
            foreach(var (why,phase,word,hex) in new[]{(ExpeditionJoinClosedReason.Locked,ExpeditionJoinPhase.Locked,"LOCKED",NativeExpeditionJoinScene.LockedHex),(ExpeditionJoinClosedReason.Reserved,ExpeditionJoinPhase.Reserved,"ROUTE RESERVED",NativeExpeditionJoinScene.ReservedHex),((ExpeditionJoinClosedReason)7,ExpeditionJoinPhase.Locked,"LOCKED",NativeExpeditionJoinScene.LockedHex)})
            {
                // Even with a stray count in the view: no countdown, no counted pilots, no charge cue; the viewer standing in it told why.
                var shut=new Dictionary<string,ExpeditionJoinClosedReason>{{St,why}};var lk=new NativeExpeditionJoinModel();
                for(double t=100;t<=101.5;t+=.05)lk.Build(View(t,"nova",stray,new[]{"nova","astra"},null,null,null,null,shut),t,pilots,labels);r=lk.Rings[Ring(lk,St)];
                NativeExpeditionJoinScene.CurtainOf(r,out _,out float la);
                Check(r.Phase==phase&&r.Status==word&&r.Detail==NativeExpeditionJoinModel.NoDeploy&&r.Seconds==0&&r.Charge==0&&r.Pilots==0&&r.Inside==0&&la==0&&NativeExpeditionJoinScene.Light(r)==hex,why+": "+word+", no countdown, curtain or counted pilots");
                Check(Cues(lk,ExpeditionJoinCueKind.Charge)==0&&!lk.Party[nova].Deploying&&lk.Viewer==ExpeditionJoinViewer.Closed&&lk.Flash==0,why+": no charge, nobody deploying, the viewer at a closed circle");
                var ls=new NativeExpeditionJoinScene();var leye=new ExpeditionEye{X=st.x+3,Y=1.8f,Z=st.z+9,Rx=-1,Ry=0,Rz=0,Ux=0,Uy=1,Uz=0};ls.Build(lk,101.5,leye,false,false);
                Check(ls.PilotsMarked==0&&Curtain(ls.Quads,r)==0&&ls.Glyphs>0&&ls.PadsDrawn==3,why+": the pad drawn with its hologram, no lock columns or curtain");
                var allShut=new Dictionary<string,ExpeditionJoinClosedReason>{{St,why},{Iw,why},{Wz,why}};var lf=new NativeExpeditionJoinModel();lf.Build(View(100,"kestrel",null,null,null,null,null,null,allShut),100,pilots,labels);
                ls.Build(lf,100,new ExpeditionEye{X=0,Y=1.8f,Z=140,Rx=1,Uy=1},false,false);Check(ls.PadsDrawn==0&&ls.Quads.Count==0,why+": no sky beacon leads to a closed circle");
            }

            // ---- loading rows: members[].pendingArea, for every member ----
            {
                var ld=new NativeExpeditionJoinModel();ld.Build(View(100,"nova",null,null,null,null,x=>{if(x.account=="astra")x.pendingArea=Iw;if(x.account=="nova")x.pendingArea=Wz;}),100,pilots,labels);
                int la=Row(ld,"astra"),ln=Row(ld,"nova"),lkr=Row(ld,"kestrel");
                Check(ld.Party[la].Loading&&ld.Party[la].Status=="LOADING · CITY IRONWORKS"&&ld.Party[la].Place=="HUB"&&ld.Party[la].PendingAccent==NativeExpeditionJoinZones.Of(Iw).Accent&&ld.Party[la].Accent==NativeExpeditionJoinZones.HubHex,"another member loading: LOADING · CITY IRONWORKS, still placed in the HUB");
                Check(ld.Party[ln].Status=="LOADING · WARD ZERO"&&ld.Party[ln].You&&!ld.Party[lkr].Loading&&ld.Viewer!=ExpeditionJoinViewer.NotCounted,"my own row loading too; the others not; no circle prompt over my load");
                var lsc=new NativeExpeditionJoinScene();lsc.Build(ld,100,new ExpeditionEye{X=st.x+3,Y=1.8f,Z=st.z+9,Rx=-1,Ry=0,Rz=0,Ux=0,Uy=1,Uz=0},false,false);Check(Near(lsc.Quads,pilots.At["astra"],.6f,2.3f),"a loading member in the hub holds in a column");
                var zv=View(100,"nova",null,null,new Dictionary<string,string>{{"nova",Wz}},null,x=>{if(x.account=="kestrel")x.pendingArea=St;});zv.area=Wz;zv.joins=Array.Empty<ExpeditionJoinView>();ld.Build(zv,100.1,pilots,labels);
                Check(!ld.InHub&&ld.Party[lkr].Status=="LOADING · CENTRAL STATION"&&ld.Party[lkr].Place=="HUB"&&ld.Party[ln].Place=="WARD ZERO","from a zone: the hub member loading into the station reads so");
                var unk=new NativeExpeditionJoinModel();unk.Build(View(100,"nova",null,null,null,null,x=>{if(x.account=="astra")x.pendingArea="exp-elsewhere";}),100,pilots,labels);Check(unk.Party[Row(unk,"astra")].Status.StartsWith("LOADING · ",StringComparison.Ordinal)&&unk.Party[Row(unk,"astra")].Status.Length>10,"an unknown destination still names where ("+unk.Party[Row(unk,"astra")].Status+")");
            }

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
                if(rr.Phase!=ExpeditionJoinPhase.Cleared){NativeExpeditionJoinScene.CurtainOf(rr,out float want,out _);float top=CurtainTop(q,rr);Check(Math.Abs(top-(rr.Y+.02f+want))<2e-3f&&want<=rr.H+1e-4f,name+": the curtain's top is its height, never over the counted column ("+top+" for "+want+", h "+rr.H+")");}
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
            Console.WriteLine("PASS expedition join presentation ("+count+" checks: zones and names; idle, charging, ticks, my flash; a deploy from the deploy record with its note, exact deployedAccounts and LOADING rows, the first record a baseline (a late joiner, back from a zone, a stale record, a reset), a step-out at the deadline no deploy, a cancelled load; return note and cue, cancel, held; the counted height h with its fallback; cleared, locked and reserved circles; LOADING rows from pendingArea for every member; not counted, near, away; the party list's places and states; pads with no flat outline, the curtain on the counted radius, counted pilots marked, my column hidden in first person, low graphics and reduced effects, beacons from afar; posts clear of the town's blocks, returns and door volumes; the world piece stands aside; live signs and entrances, sealed kept for a reserved route; no per-frame allocation)");

            NativeExpeditionJoinModel Idle(){var x=new NativeExpeditionJoinModel();x.Build(View(100,"kestrel"),100,pilots,labels);return x;}
            NativeExpeditionJoinModel m2(){var x=new NativeExpeditionJoinModel();x.Build(View(101.5,"nova",two,new[]{"nova","astra"}),101.5,pilots,labels);return x;}
            NativeExpeditionJoinModel watcherAt(double t){var x=new NativeExpeditionJoinModel();x.Build(View(102.95,"kestrel",two,new[]{"nova","astra"}),102.95,pilots,labels);x.Build(View(103,"kestrel",null,null,null,null,loadSt,took),t,pilots,labels);return x;}
        }
        private static IEnumerable<string> P(string[] s)=>s;
        private static int Posts(int pads)=>pads*NativeExpeditionJoinScene.Posts*4;
        private sealed class PieceCount:IExpeditionWorldSink{public int Vertices;public int Vertex(float x,float y,float z,float u,float v,float flat,ExpeditionTone tone,float level,float alpha)=>Vertices++;public void Triangle(int a,int b,int c){}}
        private static bool Flat(NativeFxQuads q,int i,float top){int o=i*12;float y0=q.Corners[o+1];for(int k=0;k<4;k++){float y=q.Corners[o+k*3+1];if(Math.Abs(y-y0)>1e-4f||y>top)return false;}return true;}
        private static float Reach(NativeFxQuads q,int i,float cx,float cz){int o=i*12;float best=0;for(int k=0;k<4;k++){float dx=q.Corners[o+k*3]-cx,dz=q.Corners[o+k*3+2]-cz;best=Math.Max(best,(float)Math.Sqrt(dx*dx+dz*dz));}return best;}
        /// <summary>The highest corner of the curtain's quads (-inf without one).</summary>
        private static float CurtainTop(NativeFxQuads q,in ExpeditionJoinRing r)
        {
            float top=float.NegativeInfinity;for(int i=0;i<q.Count;i++){int o=i*12;bool on=true;float ymax=float.MinValue;
                for(int k=0;k<4;k++){float dx=q.Corners[o+k*3]-r.X,dz=q.Corners[o+k*3+2]-r.Z;ymax=Math.Max(ymax,q.Corners[o+k*3+1]);if(Math.Abs(Math.Sqrt(dx*dx+dz*dz)-r.R)>1e-3)on=false;}
                if(on)top=Math.Max(top,ymax);}
            return top;
        }
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
        /// <summary>Ten seconds of hub frames at 60 Hz with 20 Hz views: rest, a count, a deploy record with its members loading,
        /// their arrival, a cancel, a locked and a reserved circle, a member loading elsewhere.</summary>
        private static List<(ExpeditionView,double)> Sequence(Dictionary<string,(int,double)> two)
        {
            var o=new List<(ExpeditionView,double)>();ExpeditionView v=null;double lastView=-1;
            var took=new Dictionary<string,(double,string[])>{{"exp-transit",(103.0,new[]{"astra","nova"})}};var arrived=new Dictionary<string,string>{{"nova","exp-transit"},{"astra","exp-transit"}};
            var shut=new Dictionary<string,ExpeditionJoinClosedReason>{{"exp-ward-zero",ExpeditionJoinClosedReason.Locked},{"exp-foundry",ExpeditionJoinClosedReason.Reserved}};
            for(int f=0;f<600;f++)
            {
                double t=98+f/60.0;
                if(Math.Floor(t*20)!=Math.Floor(lastView*20))
                {
                    double e=Math.Floor(t*20)/20;
                    if(e<100)v=View(e,"kestrel");
                    else if(e<103)v=View(e,"kestrel",two,new[]{"nova","astra"});
                    else if(e<104)v=View(e,"kestrel",null,null,null,null,Loading("exp-transit","nova","astra"),took);
                    else if(e<104.5)v=View(e,"kestrel",null,null,arrived,null,null,took);
                    else if(e<105)v=View(e,"kestrel",new Dictionary<string,(int,double)>{{"exp-foundry",(1,104.5)}},new[]{"sheldon"},arrived,null,null,took);
                    else if(e<106)v=View(e,"kestrel",null,null,arrived,null,Loading("exp-foundry","sheldon"),took,shut);
                    else v=View(e,"kestrel",null,null,arrived,null,null,took);
                    lastView=t;
                }
                o.Add((v,t));
            }
            return o;
        }
    }
}
