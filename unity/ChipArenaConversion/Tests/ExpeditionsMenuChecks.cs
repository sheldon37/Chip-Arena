#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using D=ChipArena.UnityConversion.NativeDeployPresentation;
using K=ChipArena.UnityConversion.NativeExpeditionCatalogue;
using X=ChipArena.UnityConversion.NativeExpeditionsMenu;
namespace ChipArena.UnityConversion
{
    // The Expeditions screen (owner 2026-10-15, revised 2026-11-06: one all-in-one run rolled at the Deployment Station, and
    // Boss Battle in place of Wave Defense; Core NativeExpeditionCatalogue and NativeExpeditionsMenu, Runtime
    // NativeExpeditionsMenu): the run's entry still feeds the station; the roster reads every boss from its own table and says
    // honestly what can start; the launch gates; the sample spin; every action still reachable through the same calls; the
    // layout fits from 1280x720 at a 1.5 interface scale to 3440x1440 and 4K; a guest is read-only; a backward-compatible boss pick.
    internal static class ExpeditionsMenuChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expeditions menu: "+what);count++;}
        public static void Run(string root)
        {
            count=0;Catalogue(root);Roster();Emblems();Gates();Spin();Fit();Sources(root);
            Console.WriteLine("PASS Expeditions menu: "+count+" checks (the run's entry, boss roster and emblems, launch gates, sample spin, fit 720p@1.5-3440x1440-4K, old actions reachable, guest read-only, replicated boss pick)");
        }

        private static void Catalogue(string root)
        {
            string res=Path.Combine(root,"Assets/Conversion/Resources");
            var maps=new HashSet<string>();using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(res,"UnityMaps.json"))))foreach(var m in doc.RootElement.GetProperty("maps").EnumerateArray())maps.Add(m.GetProperty("id").GetString());
            string world;var areas=new Dictionary<string,string>();
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(res,"ExpeditionLayout.json")))){world=doc.RootElement.GetProperty("world").GetString();foreach(var a in doc.RootElement.GetProperty("areas").EnumerateArray())areas[a.GetProperty("id").GetString()]=a.GetProperty("map").GetString();}
            // One run: there is nothing to choose between (owner 2026-11-06).
            Check(K.Count==1&&ReferenceEquals(K.Run,K.All[0])&&K.Default==0&&!K.Run.Locked,"one expedition, playable");
            var x=K.Run;
            // The Deployment Station reads this entry (ExpeditionDeploymentRoom: the id is in the run's compatibility digest).
            Check(x.Id=="black-siren","the run keeps the id the Deployment Station and its digest use ("+x.Id+")");
            Check(x.LayoutWorld==world&&areas.ContainsKey(world)&&areas[world]==x.HubMap&&maps.Contains(x.HubMap),"the layout's world and its hub map ("+x.LayoutWorld+" -> "+x.HubMap+")");
            Check(x.Areas.Length==4&&x.Areas.Take(3).All(a=>!a.Boss)&&x.Areas[3].Boss,"the authored areas, then the final boss");
            foreach(var a in x.Areas)
            {
                if(a.State==K.AreaState.Live)Check(a.LayoutArea!=null&&areas.TryGetValue(a.LayoutArea,out var m)&&m==a.Map&&maps.Contains(a.Map),a.Name+": a live area is a layout area with its map");
                else Check(a.LayoutArea==null&&a.Map==null&&a.State==K.AreaState.InDevelopment,a.Name+": an area not built yet has no layout id or map");
            }
            Check(x.Areas.Take(3).Select(a=>a.LayoutArea).SequenceEqual(new[]{"exp-ward-zero","exp-transit","exp-foundry"}),"the station's authored pool: Ward Zero, Central Station, City Ironworks");
            Check(!string.IsNullOrWhiteSpace(x.Title)&&x.Title==x.Title.ToUpperInvariant()&&!string.IsNullOrWhiteSpace(x.Kicker)&&x.Tagline.Length<=130&&!x.Title.Contains("SIREN"),"the run's title (caps, no expedition name) and a tag line that fits two lines");
            Check(K.Steps.Length==4&&K.Steps.Select(s=>s.Num).SequenceEqual(new[]{"01","02","03","04"})&&K.Steps.All(s=>s.Name==s.Name.ToUpperInvariant()&&s.Line.Length<=80&&s.Short.Length<=28),"four steps, numbered, in caps, with a line and a short line");
            Check(K.Entrances.SequenceEqual(new[]{"EAST","WEST","SOUTH"})&&K.Entrances.Length==D.Reels,"the reels' entrance labels as the station's");
            Check(K.StateLabel(K.AreaState.Live)=="LIVE"&&K.StateLabel(K.AreaState.InDevelopment)=="IN DEVELOPMENT","state labels");
            // Every destination the Instance Machine draws from has its boss in the roster.
            Check(D.DestinationCount>=5,"the station's destinations ("+D.DestinationCount+")");
            for(int i=0;i<D.DestinationCount;i++){var d=D.DestinationAt(i);Check(K.BossOf(d.Id)!=null&&d.Name==d.Name.ToUpperInvariant(),d.Name+": its boss is on the roster");}
        }

        private static void Roster()
        {
            var b=K.Bosses;
            Check(b.Length==6&&b.Select(x=>x.Id).Distinct().Count()==b.Length&&b.Select(x=>x.Name).Distinct().Count()==b.Length&&b.Select(x=>x.Accent).Distinct().Count()==b.Length,"six bosses: distinct ids, names and accents");
            Check(b.Select(x=>x.Name).SequenceEqual(new[]{"THE SIREN","THE 6:47","THE CONVERTER","THE HUM","THE DEEP END","NOCTIEL"}),"the roster's order: the fight now first, the run's bosses, the final in development");
            foreach(var x in b)
            {
                string n=x.Name;
                Check(n==n.ToUpperInvariant()&&x.Eyebrow==x.Eyebrow.ToUpperInvariant()&&x.Line==x.Line.ToUpperInvariant()&&x.Subtitle==x.Subtitle.ToUpperInvariant()&&x.Area==x.Area.ToUpperInvariant(),n+": its plate, eyebrow, line, subtitle and area in caps");
                Check(!string.IsNullOrWhiteSpace(x.Blurb)&&x.Blurb.Length<=130,n+": a blurb that fits two lines");
                Check(x.Moves!=null&&x.Moves.Length>=4&&x.Moves.All(m=>!string.IsNullOrWhiteSpace(m))&&x.Phases>=3&&x.Phases<=4,n+": its moves and phases");
                Check((x.Fight==K.BossFight.Direct)==(K.FightReason(x)==null),n+": a reason exactly when it cannot start here");
                if(x.Destination!=null)
                {
                    bool known=false;for(int i=0;i<D.DestinationCount;i++)if(D.DestinationAt(i).Id==x.Destination)known=true;
                    Check(known&&ReferenceEquals(K.BossOf(x.Destination),x)&&(x.Fight==K.BossFight.Direct||K.FightReason(x).Contains(K.DestinationName(x.Destination))),n+": fought in runs at a destination the Instance Machine rolls ("+x.Destination+")");
                }
                else Check(x.Destination==null,n+": not an area's boss");
            }
            // Each fight's own table names its moves: the roster cannot drift from it.
            Check(b[1].Moves.SequenceEqual(NativeStationBoss.Moves.Select(m=>m.Title))&&b[1].Name==NativeStationBoss.Name&&b[1].Destination==NativeStationBoss.Area,"THE 6:47 from its table");
            Check(b[2].Moves.SequenceEqual(new[]{"Pour","Blow","Stomp","Grab","Slam","Deslag","Call","Charge"})&&b[2].Name==NativeIronworksBoss.PlateName,"THE CONVERTER's attacks, not its scripted moments");
            Check(b[3].Moves.SequenceEqual(NativeHumBoss.Moves.Select(m=>m.Title))&&b[3].Line==NativeHumBoss.Card.Line&&b[3].Destination==NativeBackroomsGenerator.AreaId,"THE HUM from its table and card");
            Check(b[4].Moves.SequenceEqual(NativeDeepEndBoss.Moves.Select(m=>m.Title))&&b[4].Line==NativeDeepEndBoss.Card.Line&&b[4].Destination==NativePoolroomsGenerator.AreaId,"THE DEEP END from its table and card");
            Check(b[5].Moves.SequenceEqual(NativeFallenBoss.Moves.Select(m=>m.Title))&&b[5].Name==NativeFallenBoss.Name&&b[5].Fight==K.BossFight.Preview,"NOCTIEL from its table, in development");
            // Four zone bosses are direct; legacy rooms still default to the Siren.
            Check(b.Count(x=>x.Fight==K.BossFight.Direct)==5&&b[K.DefaultBoss].Name=="THE SIREN"&&K.DefaultBoss==0,"five direct fights, Siren is the legacy default");
            Check(K.WrapBoss(-1)==b.Length-1&&K.WrapBoss(b.Length)==0&&ReferenceEquals(K.BossAt(-1),b[b.Length-1]),"the roster wraps both ways");
            Check(K.FightLabel(K.BossFight.Direct)=="FIGHT NOW"&&K.FightShort(K.BossFight.InRun)=="IN RUNS"&&K.FightLabel(K.BossFight.Preview)=="IN DEVELOPMENT","fight labels");
            Check(ReferenceEquals(K.BossOf("exp-ward-zero"),b[0])&&K.BossOf("nowhere")==null,"Ward Zero's boss is the Siren");
        }

        // Every emblem parses and draws a silhouette (white body, near-black cut-outs) inside its box.
        private static void Emblems()
        {
            foreach(var x in K.Bosses)
            {
                Check((x.Emblem==null)==(x.Id=="siren")&&x.EmblemKey=="exp-boss-"+x.Id,x.Name+": an emblem (the Siren uses the lobby's face) and its cache key");
                if(x.Emblem==null)continue;
                const int px=64;var rgba=NativeIconRaster.Raster(NativeIconRaster.Parse(x.Emblem,1,NativeIconRaster.Mode.Markup),px,100);
                int body=0,cut=0,edge=0;for(int y=0;y<px;y++)for(int i=0;i<px;i++){int k=(y*px+i)*4;float a=rgba[k+3],v=(rgba[k]+rgba[k+1]+rgba[k+2])/3;if(a<.5f)continue;if(v>.6f)body++;else if(v<.1f)cut++;if(i==0||y==0||i==px-1||y==px-1)edge++;}
                float cover=(body+cut)/(float)(px*px);
                Check(cover>.12f&&cover<.75f&&body>px*px*.08f,x.Name+": a silhouette ("+(cover*100).ToString("0")+"% of its box)");
                Check(edge<px*.2f,x.Name+": inside its box ("+edge+" edge pixels)");
            }
            Check(K.Bosses.Where(x=>x.Emblem!=null).Select(x=>x.Emblem).Distinct().Count()==K.Bosses.Count(x=>x.Emblem!=null),"each emblem is its own");
        }

        private static void Gates()
        {
            var siren=K.Bosses[0];var station=K.Bosses[1];var noctiel=K.Bosses[5];
            X.Launch R(X.Mode m,K.Boss b,X.Seat seat,bool busy=false,bool ready=true)=>X.Resolve(m,b,seat,busy,ready);
            foreach(var seat in new[]{X.Seat.Solo,X.Seat.Host})
            {
                var a=R(X.Mode.Expedition,siren,seat);Check(a.Enabled&&a.Kind==X.LaunchKind.ExpeditionM1&&a.Label==X.ExpeditionLabel,seat+": Start Expedition enters the Deployment Station");
                Check(!R(X.Mode.Expedition,station,seat,busy:true).Enabled,seat+": not while a connection is open");
                a=R(X.Mode.Boss,siren,seat);Check(a.Enabled&&a.Kind==X.LaunchKind.BossFight&&a.Label=="FIGHT THE SIREN"&&a.Detail=="STRAIGHT TO THE BOSS · UP TO 4",seat+": the Siren's fight starts straight away");
                Check(!R(X.Mode.Boss,siren,seat,busy:true).Enabled,seat+": the boss fight waits while a connection is open (StartMatch refuses then)");
                foreach(var b in K.Bosses.Where(b=>b.Fight!=K.BossFight.Direct)){a=R(X.Mode.Boss,b,seat);Check(!a.Enabled&&a.Kind==X.LaunchKind.None&&a.Reason==K.FightReason(b)&&a.Label=="FIGHT "+b.Name,seat+": "+b.Name+" cannot start here and says why");}
                Check(R(X.Mode.Boss,station,seat).Enabled&&R(X.Mode.Boss,noctiel,seat).Detail=="IN DEVELOPMENT",seat+": the details say where");
            }
            Check(!R(X.Mode.Boss,siren,X.Seat.Host,ready:false).Enabled&&R(X.Mode.Boss,siren,X.Seat.Host,ready:false).Reason==X.GuestsWaiting&&R(X.Mode.Boss,siren,X.Seat.Host,ready:false).Kind==X.LaunchKind.BossFight,"the host's boss fight waits for every guest to ready up");
            Check(!R(X.Mode.Expedition,siren,X.Seat.Host,ready:false).Enabled&&R(X.Mode.Expedition,siren,X.Seat.Host,ready:false).Reason==X.GuestsWaiting,"the host's run waits for every guest");
            Check(R(X.Mode.Boss,siren,X.Seat.Solo,ready:false).Enabled,"offline there is no one to wait for");
            // A guest: read-only, the launch is Ready.
            Check(!X.Editable(X.Seat.Guest)&&X.Editable(X.Seat.Host)&&X.Editable(X.Seat.Solo),"a guest cannot change the mode, boss or bots");
            foreach(var m in new[]{X.Mode.Expedition,X.Mode.Boss})foreach(var b in K.Bosses){var a=R(m,b,X.Seat.Guest);Check(a.Kind==X.LaunchKind.Ready&&a.Enabled&&a.Label=="READY","a guest's launch is Ready whatever is shown");}
            Check(!R(X.Mode.Boss,siren,X.Seat.Guest,busy:true).Enabled,"a guest without a connection cannot ready");
            Check(X.ModeOf(true)==X.Mode.Expedition&&X.ModeOf(false)==X.Mode.Boss,"a room's options show the run or Boss Battle");
            Check(!Enum.GetNames(typeof(X.Mode)).Any(n=>n.Contains("Wave"))&&!Enum.GetNames(typeof(X.LaunchKind)).Any(n=>n.Contains("Wave")),"no Wave Defense mode is left");
        }

        // The run card's sample spin: three distinct destinations and mutators, the same for the same spin, a new one each spin.
        private static void Spin()
        {
            int nd=D.DestinationCount,nm=D.MutatorCount;var dest=new int[3];var mut=new int[3];var d2=new int[3];var m2=new int[3];var seen=new HashSet<string>();
            for(long i=0;i<400;i++)
            {
                X.SpinPicks(i,nd,nm,dest,mut);X.SpinPicks(i,nd,nm,d2,m2);
                Check(dest.SequenceEqual(d2)&&mut.SequenceEqual(m2),"spin "+i+": the same picks every time");
                Check(dest.All(v=>v>=0&&v<nd)&&dest.Distinct().Count()==3&&mut.All(v=>v>=0&&v<nm)&&mut.Distinct().Count()==3,"spin "+i+": three distinct destinations and mutators");
                seen.Add(string.Join(",",dest));
            }
            Check(seen.Count>=8,"spins vary ("+seen.Count+" destination sets in 400)");
            Check(X.SpinAt(0)==0&&X.SpinAt(X.SpinPeriod*2.5)==2&&Math.Abs(X.SpinAge(X.SpinPeriod*2.5)-X.SpinPeriod*.5f)<1e-3f&&X.SpinAt(-5)==0,"the spin clock");
            Check(D.RollDuration<X.MutatorsAt&&X.MutatorsAt+.9f+.7f+D.Settle+X.HoldTail<X.SpinPeriod,"the reels land, then the mutators, then a hold before the next pull");
        }

        private static void Fit()
        {
            var cases=new[]{(1920,1080,1f),(3440,1440,1f),(1280,720,1.5f),(1280,720,1f),(3840,2160,1f),(2560,1080,1f),(1920,1080,1.25f),(1600,900,1f),(2560,1440,1.5f),(1366,768,1.25f)};
            foreach(var (w,h,ui) in cases)
            {
                var c=NativeUiMotion.Measure(w,h,ui);var L=X.Measure(c.Width,c.Height);string n=w+"x"+h+"@"+ui;bool cp=L.Compact;
                Check(L.Scale>0&&L.Scale<=1&&Math.Abs(L.Width*L.Scale-c.Width)<.01f&&Math.Abs(L.Height*L.Scale-c.Height)<.01f&&L.Width>=X.MinWidth-.01f&&L.Height>=X.MinHeight-.01f,n+": the design canvas scales to the canvas ("+L.Scale.ToString("0.000")+")");
                var canvas=new X.Box(0,0,L.Width,L.Height);var regions=new[]{L.Header,L.Party,L.Browser,L.Mode,L.Bar};
                foreach(var b in regions)Check(canvas.Contains(b)&&b.W>0&&b.H>0,n+": a region inside the canvas");
                for(int i=0;i<regions.Length;i++)for(int j=i+1;j<regions.Length;j++)Check(!regions[i].Overlaps(regions[j]),n+": regions "+i+" and "+j+" do not overlap");
                Check(L.Header.Y>=X.Top-.01f,n+": under the top bar (wordmark, connection pill)");
                Check(L.Bar.Bottom<=L.Height-X.FooterRule/L.Scale-X.BarGap+.01f,n+": the bar ends above the footer rule");
                Check(L.Content.W<=X.ContentMax+.01f&&Math.Abs(L.Content.X-(L.Width-L.Content.W)/2)<.01f,n+": the content is capped and centred");
                Check(L.Browser.W>=600&&L.Mode.W>=400&&L.Mode.W<=540,n+": the browser and the mode panel keep their widths ("+L.Browser.W.ToString("0")+", "+L.Mode.W.ToString("0")+")");
                for(int i=0;i<L.Slots;i++)
                {
                    var s=L.Slot(i);Check(L.Party.Contains(s)&&s.W>=230&&s.H>=120,n+": party slot "+i+" fits ("+s.W.ToString("0")+"x"+s.H.ToString("0")+")");
                    if(i>0)Check(!L.Slot(i-1).Overlaps(s),n+": party slots do not overlap");
                    var pc=X.Party(s);Check(s.Contains(pc.Stage)&&s.Contains(pc.Info)&&!pc.Stage.Overlaps(pc.Info)&&pc.Info.W>=100&&pc.Stage.W>=64&&pc.Info.H>=4+19*1.3f+22+24+4,n+": a party card's portrait and details");
                }
                // The run's card: the machines at the left, how a run goes at the right.
                var run=X.Run(L.Browser,cp);Check(L.Browser.Contains(run.Label)&&L.Browser.Contains(run.Card)&&!run.Card.Overlaps(run.Label)&&run.Card.W==L.Browser.W,n+": the run's card takes the whole browser");
                var k=X.CardParts(run.Card,cp);Check(run.Card.Contains(k.Art)&&run.Card.Contains(k.Info)&&!k.Art.Overlaps(k.Info)&&k.Info.W>=280&&k.Art.W>=200,n+": the run's art and details ("+k.Art.W.ToString("0")+", "+k.Info.W.ToString("0")+")");
                Check(X.RunInfoHeight(k,cp)<=k.Info.H+.01f,n+": how a run goes fits ("+X.RunInfoHeight(k,cp).ToString("0")+" of "+k.Info.H.ToString("0")+")");
                var m=X.MachineParts(k.Art,cp);var parts=new[]{m.Head,m.Reels,m.Lever,m.MutHead,m.Muts,m.Foot};
                foreach(var p in parts)Check(k.Art.Contains(p)&&p.W>0&&p.H>0,n+": a machine part inside the art");
                for(int i=0;i<parts.Length;i++)for(int j=i+1;j<parts.Length;j++)Check(!parts[i].Overlaps(parts[j]),n+": machine parts "+i+" and "+j+" apart");
                for(int i=0;i<3;i++)
                {
                    var rw=X.ReelWindow(m,i);Check(m.Reels.Contains(rw)&&rw.H>=(cp?36:56)&&rw.W>=50,n+": reel "+i+"'s window ("+rw.W.ToString("0")+"x"+rw.H.ToString("0")+")");
                    var mc=X.MutCard(m,i);Check(m.Muts.Contains(mc)&&mc.H>=(cp?22:34)&&mc.W>=50,n+": mutator card "+i+" ("+mc.W.ToString("0")+"x"+mc.H.ToString("0")+")");
                    if(i>0)Check(!X.ReelWindow(m,i-1).Overlaps(rw)&&!X.MutCard(m,i-1).Overlaps(mc),n+": reels and cards side by side");
                }
                // Boss Battle: the card above the roster rail.
                var ro=X.Roster(L.Browser,cp);foreach(var b in new[]{ro.Label,ro.Card,ro.Rail})Check(L.Browser.Contains(b),n+": a roster part inside the browser");
                Check(!ro.Card.Overlaps(ro.Label)&&!ro.Card.Overlaps(ro.Rail)&&ro.Rail.H==X.RailHeight(cp),n+": the boss card above the rail");
                for(int i=0;i<K.BossCount;i++){var t=X.RailTile(ro.Rail,i,K.BossCount);Check(ro.Rail.Contains(t)&&t.W>=90,n+": rail tile "+i+" ("+t.W.ToString("0")+" wide)");if(i>0)Check(!X.RailTile(ro.Rail,i-1,K.BossCount).Overlaps(t),n+": rail tiles apart");}
                var bk=X.CardParts(ro.Card,cp);Check(ro.Card.Contains(bk.Art)&&ro.Card.Contains(bk.Info)&&!bk.Art.Overlaps(bk.Info)&&bk.Info.W>=280&&bk.Art.W>=200&&bk.Art.H>=(cp?110:160),n+": the boss's art and details ("+bk.Art.W.ToString("0")+"x"+bk.Art.H.ToString("0")+")");
                Check(X.BossInfoHeight(bk,cp)<=bk.Info.H+.01f,n+": the boss's details and moves fit ("+X.BossInfoHeight(bk,cp).ToString("0")+" of "+bk.Info.H.ToString("0")+")");
                var p2=X.Panel(L.Mode,cp);foreach(var b in new[]{p2.Label,p2.Expedition,p2.Boss,p2.Detail,p2.Loadout})Check(L.Mode.Contains(b),n+": a mode panel part inside it");
                Check(!p2.Expedition.Overlaps(p2.Boss)&&!p2.Boss.Overlaps(p2.Detail)&&!p2.Detail.Overlaps(p2.Loadout)&&p2.Detail.H>=X.DetailMin(cp),n+": the mode tiles, the notes ("+p2.Detail.H.ToString("0")+" for "+X.DetailMin(cp)+") and the loadout");
                // The design scale shrinks the canvas text at most to 80%; the smallest label (7.5 units, a mutator's polarity) keeps
                // 6 physical px at the canvas floor and 7 at the owner's 720p 1.5 case.
                float px=7.5f*L.Scale*c.Scale;Check(L.Scale>=.8f&&px>=6f&&(ui<1.5f||px>=7),n+": the smallest label is "+px.ToString("0.0")+" physical px (design scale "+L.Scale.ToString("0.00")+")");
            }
            Check(X.Measure(1600,900).Compact==false&&X.Measure(NativeUiMotion.Measure(1280,720,1.5f).Width,NativeUiMotion.Measure(1280,720,1.5f).Height).Compact,"the short design (720p at 1.5) is the compact one, 1080p is not");
        }

        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            string exp=R("Runtime/NativeExpeditionsMenu.cs"),home=R("Runtime/NativeHomeMenu.cs"),lobby=R("Runtime/NativeLobbyUI.cs"),mt=R("Runtime/NativeMenuTransitionUI.cs");
            // Start Expedition opens it, and a PvE room is it.
            Check(home.Contains("private void BossScreen(float age)=>ExpeditionsScreen(age);")&&home.Contains("HomeItems={(\"boss\",\"START EXPEDITION\",\"01\",MenuScreen.Boss)"),"Start Expedition (home key 1) opens the Expeditions screen");
            Check(lobby.Contains("if(pve){ExpeditionsScreen(age);return;}"),"a PvE room's lobby is the Expeditions screen");
            // The outdated pieces are gone: no expedition carousel, no map or sample city for a rolled run, no Wave Defense.
            Check(!exp.Contains("NativeStageSchematic")&&!exp.Contains("NativeRunPreview")&&!exp.Contains("ExpMapOf")&&!File.Exists(Path.Combine(root,"Assets/ChipArena/Core/NativeRunPreview.cs")),"a rolled run shows no map");
            Check(!exp.Contains("ExpTurnTo")&&!exp.Contains("ExpSpine")&&!exp.Contains("SELECT EXPEDITION"),"no expedition to select");
            Check(!exp.Contains("WAVE DEFENSE")&&!exp.Contains("ExpWave")&&!exp.Contains("WaveLabels"),"Wave Defense is removed");
            // Every action of the old screen and the lobby, through the same calls.
            var calls=new Dictionary<string,string>
            {
                ["RequestStage(StageLoadKind.Boss,StartMatch)"]="the boss fight / the room's Start",
                ["BeginDeploymentStation(K.Run.Id)"]="Expedition enters the authenticated Deployment Station",
                ["{prefs.pve=true;Go(MenuScreen.Create);}"]="Host Co-op Room (Host Room)",
                ["Go(MenuScreen.Join)"]="Join Room",
                ["ApplyRule(RoomOpt.Bots,"]="party bots (add / remove)",
                ["RuleSelect(7580,RoomOpt.Bots,\"Party bots\""]="party bots (the drawer's select)",
                ["RuleCheck(7586,RoomOpt.Mirror,"]="party bots use my weapon",
                ["ApplyRule(RoomOpt.SkipWaves,1,0)"]="start at the boss",
                ["ClassRow(\"Your class\",ClassCardStyle.Stacked);WeaponCards(\"Your weapon\");"]="class and weapon",
                ["GuestReady(!networkReady)"]="a guest's Ready / Cancel Ready",
                ["SendServer(new NativeNetMessage{type=\"profile\",profile=Profile(),token=account?.Token})"]="Send Loadout",
                ["LeaveRoom()"]="Leave Room",
                ["GUIUtility.systemCopyBuffer=code;Notice(\"Room code copied.\");"]="Copy Code",
                ["GUIUtility.systemCopyBuffer=inviteLink;Notice(\"Invite link copied.\");"]="Copy Invite",
                ["lanLines[0]"]="the LAN line",
                ["Go(MenuScreen.Customize)"]="Customize",
                ["Go(MenuScreen.Settings)"]="Settings",
                ["OpenConnectionReport()"]="the Connection report",
                ["expeditionError"]="the Expedition start error",
                ["Go(MenuScreen.Home)"]="close",
            };
            foreach(var kv in calls)Check(exp.Contains(kv.Key),"reachable: "+kv.Value+" ("+kv.Key+")");
            Check(exp.Contains("case X.LaunchKind.BossFight:ExpStartBoss(K.BossAt(expBoss));break;")&&Regex.IsMatch(exp,@"case ""siren"":SelectBossBattle\(""siren""\);ApplyRule\(RoomOpt\.SkipWaves,1,0\);prefs\.skipWaves=true;prefs\.expedition=false;prefs\.pve=true;sandboxWanted=false;RequestStage\(StageLoadKind\.Boss,StartMatch\);"),"the Siren's fight starts as Start Boss Fight did, at the boss");
            {int s=exp.IndexOf("private void ExpStartBoss(",StringComparison.Ordinal),e=s<0?-1:exp.IndexOf("\n        }",s,StringComparison.Ordinal);string body=s>=0&&e>s?exp.Substring(s,e-s):"";
             foreach(var b in K.Bosses.Where(b=>b.Fight==K.BossFight.Direct))Check(body.Contains("case \""+b.Id+"\":"),b.Name+": a direct boss has its own start");Check(body.Contains("default:Notice("),"a boss without a start says so instead of starting another");}
            Check(exp.Contains("ApplyRule(RoomOpt.Versus,m==X.Mode.Expedition?2:1,0);if(m==X.Mode.Boss)ApplyRule(RoomOpt.SkipWaves,1,0);"),"a mode is the room's Versus option; Boss Battle carries the skip");
            Check(home.Contains("if(expDrawer&&(at==MenuScreen.Boss||at==MenuScreen.Lobby)){ExpToggleDrawer();return;}"),"Escape closes the loadout drawer first");
            // A guest is read-only: the shown mode and boss are the room's; turns, modes and bots need Editable.
            Check(exp.Contains("if(seat==X.Seat.Guest){expMode=X.ModeOf(receivedOptions?.expedition==true);expBoss=NativeBossBattle.Index(receivedOptions?.boss);}"),"a guest sees the room's mode and the boss a room fights");
            Check(Regex.Matches(exp,@"if\(edit&&expMode==X\.Mode\.Boss\)ExpPickBoss").Count==2&&Regex.Matches(exp,@"if\(edit&&expMode==X\.Mode\.Boss&&Input\.GetKeyDown").Count==2&&exp.Contains("if(edit)ExpPickBoss(i,")&&exp.Contains("bool live=ExpLive(a)&&edit;float h=U.HoverAmount(9500+index")&&exp.Contains("ExpPartyCard(slot,party[i],i,ce,now,seat,edit&&i==newest,bots)")&&exp.Contains("bool canAdd=edit&&")&&exp.Contains("int n=K.BossCount;bool live=ExpLive(a)&&edit;"),"a guest's keys, mode tiles, roster and bot buttons do nothing");
            Check(exp.Contains("BeginRules(false,width);var o=rules;"),"the drawer's party rows go through BeginRules (read-only on a guest connection)");
            var fields=typeof(NativeRoomOptions).GetFields().Select(f=>f.Name).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
            Check(string.Join(",",fields)=="boss,botDifficulty,botOptions,bothDraft,bots,choices,cooperative,expedition,friendlyFire,hostTeam,map,mirrorWeapons,randomizeMap,rounds,selectionSeconds,sets,skipBotDrafts,skipPlayerIntros,skipWaves,stationary,teams,training","NativeRoomOptions keeps its fields ("+string.Join(",",fields)+")");
            Check(!exp.Contains("type=\"expedition")&&!Regex.IsMatch(exp,@"new NativeNetMessage\{type=""(?!profile"")"),"the screen sends no new message");
            // The roster's state and the sample are presentation: no randomness, no authority read.
            Check(!exp.Contains("Random")&&!exp.Contains("Expedition.Session")&&!exp.Contains("deploymentRoom"),"the screen rolls nothing and reads no run");
            foreach(var b in K.Bosses)Check(b.Emblem!=null||exp.Contains("\"siren-face\",SirenFace,200"),b.Name+": drawn with its emblem");
            // The transitions read EXPEDITIONS / PVE for this screen and a PvE room.
            Check(Regex.Match(mt,"MenuTitles=\\{([^}]*)\\}").Groups[1].Value.Split(',')[5].Trim()=="\"EXPEDITIONS\""&&Regex.Match(mt,"MenuEyebrows=\\{([^}]*)\\}").Groups[1].Value.Split(',')[5].Trim()=="\"PVE\"","the transition titles EXPEDITIONS / PVE");
            Check(mt.Contains("if(dest==MenuScreen.Boss||dest==MenuScreen.Lobby&&prefs.pve){eyebrow=MenuEyebrows[(int)MenuScreen.Boss];title=MenuTitles[(int)MenuScreen.Boss];}"),"a PvE room's transition reads EXPEDITIONS too");
            // Reduced effects: every motion of the screen has a calm branch.
            foreach(var calm in new[]{"if(!prefs.reducedEffects){float y=Mathf.Repeat(now*34","long index=calm?0:X.SpinAt(now);float t=calm?99:X.SpinAge(now);","if(calm){x=0;s=1;}","if(lit&&!prefs.reducedEffects)Sheen(d,a);","float spin=calm?0:now*7f;","float k=prefs.reducedEffects?1:Mathf.Clamp01((now-expModeAt)/ExpModeTurn)","expTint=prefs.reducedEffects?ExpTintTarget:","float pop=calm||fresh<0?1:","float p=prefs.reducedEffects?0:Mathf.Repeat(now*.8f,1);"})
                Check(exp.Contains(calm),"Reduced effects: "+calm);
            // Keyboard and controller.
            foreach(var key in new[]{"KeyCode.Q","KeyCode.E","KeyCode.A","KeyCode.D","KeyCode.LeftArrow","KeyCode.RightArrow","KeyCode.Tab","KeyCode.L","KeyCode.Return","KeyCode.Alpha1","KeyCode.Keypad1","KeyCode.JoystickButton0","KeyCode.JoystickButton1","KeyCode.JoystickButton3","KeyCode.JoystickButton4","KeyCode.JoystickButton5"})Check(exp.Contains(key),"navigable: "+key);
            Check(exp.Contains("GUIUtility.keyboardControl!=0||updateOpen")&&exp.Contains("expPadFrame!=Time.frameCount"),"keys wait for text fields and the update card; the controller is read once a frame");
        }
    }
}
#endif
