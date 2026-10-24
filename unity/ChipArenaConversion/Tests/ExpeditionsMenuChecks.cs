#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using K=ChipArena.UnityConversion.NativeExpeditionCatalogue;
using X=ChipArena.UnityConversion.NativeExpeditionsMenu;
namespace ChipArena.UnityConversion
{
    // The Expeditions screen (owner 2026-10-15; Core NativeExpeditionCatalogue and NativeExpeditionsMenu, Runtime
    // NativeExpeditionsMenu): the catalogue's shape and its ids against the layout and the maps; the mode and wave mapping
    // (Wave 1 a normal start, the Siren skipWaves, Waves 2-3 locked) and the launch gates; every action of the old boss-test
    // screen and the room lobby still reachable through the same calls; the layout fits from 1280x720 at a 1.5 interface scale
    // to 3440x1440 and 4K; a guest is read-only; no room field or message was added.
    internal static class ExpeditionsMenuChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expeditions menu: "+what);count++;}
        public static void Run(string root)
        {
            count=0;Catalogue(root);Mapping();Gates();Fit();Sources(root);
            Console.WriteLine("PASS Expeditions menu: "+count+" checks (catalogue and ids, wave mapping, launch gates, fit 720p@1.5-3440x1440-4K, old actions reachable, guest read-only, no room fields)");
        }

        private static void Catalogue(string root)
        {
            string res=Path.Combine(root,"Assets/Conversion/Resources");
            var maps=new HashSet<string>();using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(res,"UnityMaps.json"))))foreach(var m in doc.RootElement.GetProperty("maps").EnumerateArray())maps.Add(m.GetProperty("id").GetString());
            string world;var areas=new Dictionary<string,string>();
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(res,"ExpeditionLayout.json")))){world=doc.RootElement.GetProperty("world").GetString();foreach(var a in doc.RootElement.GetProperty("areas").EnumerateArray())areas[a.GetProperty("id").GetString()]=a.GetProperty("map").GetString();}
            Check(K.Count>=3,"at least the current expedition and two placeholders ("+K.Count+")");
            Check(K.All.Select(x=>x.Id).Distinct().Count()==K.Count&&K.All.Select(x=>x.Title).Distinct().Count()==K.Count&&K.All.Select(x=>x.Accent).Distinct().Count()==K.Count,"ids, titles and accents are distinct");
            Check(!K.At(K.Default).Locked&&K.Default==0,"the default pick is the first, playable expedition");
            Check(K.Wrap(-1)==K.Count-1&&K.Wrap(K.Count)==0&&ReferenceEquals(K.At(-1),K.All[K.Count-1]),"the carousel wraps both ways");
            foreach(var x in K.All)
            {
                string n=x.Title;
                Check(!string.IsNullOrWhiteSpace(x.Title)&&x.Title==x.Title.ToUpperInvariant()&&!string.IsNullOrWhiteSpace(x.Kicker)&&!string.IsNullOrWhiteSpace(x.Tagline)&&x.Tagline.Length<=90,n+": title (caps), kicker and a tag line that fits two lines");
                Check(x.Areas!=null&&x.Areas.Length==4&&x.Areas.Take(3).All(a=>!a.Boss)&&x.Areas[3].Boss,n+": three areas, then the final boss");
                Check(x.Threats!=null&&x.Threats.Length>=1&&x.Threats.Length<=3&&x.Threats.All(t=>t==t.ToUpperInvariant()),n+": one to three threat tags");
                Check(x.Areas.All(a=>!string.IsNullOrWhiteSpace(a.Name)&&a.Name==a.Name.ToUpperInvariant()),n+": area names in caps");
                Check(!string.IsNullOrWhiteSpace(x.Party)&&!string.IsNullOrWhiteSpace(x.Duration)&&!string.IsNullOrWhiteSpace(x.Hub),n+": hub, party and length lines");
                if(x.Locked)
                {
                    Check(x.LayoutWorld==null&&!x.WaveDefense&&x.LiveAreas==0&&x.Areas.All(a=>a.State==K.AreaState.Uncharted&&a.Map==null&&a.LayoutArea==null),n+": a placeholder has no live area, map, layout id or wave defense");
                    continue;
                }
                Check(x.LayoutWorld==world&&areas.ContainsKey(world)&&areas[world]==x.HubMap&&maps.Contains(x.HubMap),n+": the layout's world and its hub map ("+x.LayoutWorld+" -> "+x.HubMap+")");
                foreach(var a in x.Areas)
                {
                    if(a.State==K.AreaState.Live)Check(a.LayoutArea!=null&&areas.TryGetValue(a.LayoutArea,out var m)&&m==a.Map&&maps.Contains(a.Map),n+" "+a.Name+": a live area is a layout area with its map");
                    else Check(a.LayoutArea==null&&a.Map==null&&a.State==K.AreaState.InDevelopment,n+" "+a.Name+": an area not built yet has no layout id or map and reads IN DEVELOPMENT");
                }
                Check(x.LiveAreas>=1,n+": at least one live area");
            }
            var siren=K.All.Single(x=>x.WaveDefense);Check(siren.Id=="black-siren"&&siren.Areas[0].Name=="WARD ZERO"&&siren.Areas[0].State==K.AreaState.Live&&siren.Areas[3].Name=="THE SIREN","Wave Defense belongs to the current expedition (Ward Zero, then the Siren)");
            Check(siren.Areas[1].Name=="CENTRAL STATION"&&siren.Areas[2].Name=="CITY IRONWORKS"&&siren.Areas[3].Place.StartsWith("City Hall",StringComparison.Ordinal),"the owner's areas: Central Station, City Ironworks, the Siren at City Hall");
            Check(K.StateLabel(K.AreaState.Live)=="LIVE"&&K.StateLabel(K.AreaState.InDevelopment)=="IN DEVELOPMENT"&&K.ShortLabel(K.AreaState.InDevelopment)=="IN DEV","state labels");
        }

        private static void Mapping()
        {
            Check(X.WaveStarts==4&&X.Siren==3&&X.WaveLabels.SequenceEqual(new[]{"WAVE 1","WAVE 2","WAVE 3","SIREN"}),"four starts: Wave 1, Wave 2, Wave 3, the Siren");
            Check(X.WaveSupported(0)&&!X.WaveSupported(1)&&!X.WaveSupported(2)&&X.WaveSupported(3),"Wave 1 and the Siren can start; Waves 2 and 3 are locked (no start wave in the room)");
            Check(!X.SkipWaves(0)&&X.SkipWaves(3),"Wave 1 is a normal start, the Siren is skipWaves");
            Check(X.StartOf(false)==0&&X.StartOf(true)==3&&X.StartOf(X.SkipWaves(0))==0&&X.StartOf(X.SkipWaves(3))==3,"the room's skipWaves round-trips to the shown start");
            Check(X.DefaultMode(X.Seat.Solo,true)==X.Mode.Expedition&&X.DefaultMode(X.Seat.Solo,false)==X.Mode.Waves&&X.DefaultMode(X.Seat.Host,true)==X.Mode.Waves&&X.DefaultMode(X.Seat.Guest,true)==X.Mode.Waves,"the screen opens on the full run only where it can start");
        }

        private static void Gates()
        {
            var siren=K.At(K.Default);var locked=K.All.First(x=>x.Locked);
            X.Launch R(K.Expedition p,X.Mode m,int s,X.Seat seat,bool dev=true,bool busy=false,bool ready=true,int guests=0)=>X.Resolve(p,m,s,seat,dev,busy,ready,guests);
            var a=R(siren,X.Mode.Expedition,0,X.Seat.Solo);Check(a.Enabled&&a.Kind==X.LaunchKind.ExpeditionM1&&a.Label==X.ExpeditionLabel,"solo developer: Start Expedition runs DEV M1");
            Check(!R(siren,X.Mode.Expedition,0,X.Seat.Solo,busy:true).Enabled,"not while a connection is open");
            a=R(siren,X.Mode.Expedition,0,X.Seat.Solo,dev:false);Check(a.Enabled&&a.Kind==X.LaunchKind.ExpeditionM1,"signed-in players can launch Expedition without a developer role");
            foreach(int g in new[]{0,2}){a=R(siren,X.Mode.Expedition,0,X.Seat.Host,guests:g);Check(a.Enabled&&a.Kind==X.LaunchKind.ExpeditionM1,"ready host can launch Expedition with "+g+" guests");}
            foreach(var seat in new[]{X.Seat.Solo,X.Seat.Host})
            {
                a=R(siren,X.Mode.Waves,0,seat);Check(a.Enabled&&a.Kind==X.LaunchKind.WaveMatch&&a.Detail=="WARD ZERO · WAVE 1",seat+": Wave 1 launches the boss-test match");
                a=R(siren,X.Mode.Waves,X.Siren,seat);Check(a.Enabled&&a.Kind==X.LaunchKind.WaveMatch&&a.Detail=="WARD ZERO · SIREN",seat+": the Siren launches the boss-test match (skipWaves)");
                foreach(int w in new[]{1,2}){a=R(siren,X.Mode.Waves,w,seat);Check(!a.Enabled&&a.Kind==X.LaunchKind.None&&a.Reason==X.WaveLocked,seat+": Wave "+(w+1)+" is locked");}
                foreach(var m in new[]{X.Mode.Expedition,X.Mode.Waves}){a=R(locked,m,0,seat);Check(!a.Enabled&&a.Reason==X.Placeholder&&a.Detail==K.ComingSoon,seat+" "+m+": a placeholder never starts");}
            }
            Check(!R(siren,X.Mode.Waves,0,X.Seat.Host,ready:false,guests:2).Enabled&&R(siren,X.Mode.Waves,0,X.Seat.Host,ready:false,guests:2).Reason==X.GuestsWaiting,"the host waits for every guest to ready up");
            Check(!R(siren,X.Mode.Waves,0,X.Seat.Solo,busy:true).Enabled,"Wave Defense waits while a connection is open (StartMatch refuses then)");
            // A guest: read-only, the launch is Ready.
            Check(!X.Editable(X.Seat.Guest)&&X.Editable(X.Seat.Host)&&X.Editable(X.Seat.Solo),"a guest cannot change the pick, mode, wave or bots");
            foreach(var m in new[]{X.Mode.Expedition,X.Mode.Waves})foreach(var p in new[]{siren,locked}){a=R(p,m,1,X.Seat.Guest);Check(a.Kind==X.LaunchKind.Ready&&a.Enabled&&a.Label=="READY","a guest's launch is Ready whatever is shown");}
            Check(!R(siren,X.Mode.Waves,0,X.Seat.Guest,busy:true).Enabled,"a guest without a connection cannot ready");
        }

        private static void Fit()
        {
            var cases=new[]{(1920,1080,1f),(3440,1440,1f),(1280,720,1.5f),(1280,720,1f),(3840,2160,1f),(2560,1080,1f),(1920,1080,1.25f),(1600,900,1f),(2560,1440,1.5f),(1366,768,1.25f)};
            foreach(var (w,h,ui) in cases)
            {
                var c=NativeUiMotion.Measure(w,h,ui);var L=X.Measure(c.Width,c.Height);string n=w+"x"+h+"@"+ui;
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
                var cr=X.Carousel(L.Browser,L.Compact);foreach(var b in new[]{cr.Label,cr.Card,cr.Prev,cr.Next,cr.Dots})Check(L.Browser.Contains(b),n+": a carousel part inside the browser");
                Check(!cr.Card.Overlaps(cr.Prev)&&!cr.Card.Overlaps(cr.Next)&&!cr.Card.Overlaps(cr.Label)&&!cr.Card.Overlaps(cr.Dots)&&cr.Prev.H>=150,n+": the card between the spines ("+cr.Prev.H.ToString("0")+" tall spines)");
                var k=X.CardParts(cr.Card,L.Compact);Check(cr.Card.Contains(k.Art)&&cr.Card.Contains(k.Info)&&!k.Art.Overlaps(k.Info)&&k.Info.W>=280&&k.Art.W>=200,n+": the key art and the details ("+k.Art.W.ToString("0")+", "+k.Info.W.ToString("0")+")");
                foreach(var x in K.All)Check(X.InfoHeight(k,L.Compact,x.Areas.Length)<=k.Info.H+.01f,n+": "+x.Title+"'s details and route fit ("+X.InfoHeight(k,L.Compact,x.Areas.Length).ToString("0")+" of "+k.Info.H.ToString("0")+")");
                var p=X.Panel(L.Mode,L.Compact);foreach(var b in new[]{p.Label,p.Expedition,p.Waves,p.Detail,p.Loadout})Check(L.Mode.Contains(b),n+": a mode panel part inside it");
                Check(!p.Expedition.Overlaps(p.Waves)&&!p.Waves.Overlaps(p.Detail)&&!p.Detail.Overlaps(p.Loadout)&&p.Detail.H>=X.WaveRow(L.Compact),n+": the mode tiles, the wave row ("+p.Detail.H.ToString("0")+" for "+X.WaveRow(L.Compact)+") and the loadout");
                // The design scale shrinks the canvas text at most to 80%; the smallest label (8.5 units) keeps 6.5 physical px at
                // the canvas floor (1280x720 at 1.0, every menu at 0.8) and 8 at the owner's 720p 1.5 case.
                float px=8.5f*L.Scale*c.Scale;Check(L.Scale>=.8f&&px>=6.5f&&(ui<1.5f||px>=8),n+": the smallest label is "+px.ToString("0.0")+" physical px (design scale "+L.Scale.ToString("0.00")+")");
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
            Check(!home.Contains("BossCard("),"the boss card screen is retired");
            // Every action of the old screen and the lobby, through the same calls.
            var calls=new Dictionary<string,string>
            {
                ["RequestStage(StageLoadKind.Boss,StartMatch)"]="Start Boss Fight / the room's Start (Wave Defense)",
                ["RequestStage(StageLoadKind.Expedition,StartExpeditionM1)"]="DEV Expedition M1 (Start Expedition)",
                ["{prefs.pve=true;Go(MenuScreen.Create);}"]="Host Co-op Room (Host Room)",
                ["Go(MenuScreen.Join)"]="Join Room",
                ["ApplyRule(RoomOpt.Bots,"]="party bots (add / remove)",
                ["RuleSelect(7580,RoomOpt.Bots,\"Party bots\""]="party bots (the drawer's select)",
                ["RuleCheck(7586,RoomOpt.Mirror,"]="party bots use my weapon",
                ["ApplyRule(RoomOpt.SkipWaves,X.SkipWaves(start)?1:0,0)"]="Start at the Siren (the wave row)",
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
            Check(Regex.IsMatch(exp,@"case X\.LaunchKind\.WaveMatch:prefs\.expedition=false;prefs\.pve=true;sandboxWanted=false;RequestStage\(StageLoadKind\.Boss,StartMatch\);"),"Wave Defense starts exactly as Start Boss Fight did");
            Check(home.Contains("if(expDrawer&&(at==MenuScreen.Boss||at==MenuScreen.Lobby)){ExpToggleDrawer();return;}"),"Escape closes the loadout drawer first");
            // A guest is read-only: the shown pick and mode are the room's; turns, modes, waves and bots need Editable.
            Check(exp.Contains("if(seat==X.Seat.Guest){expPick=K.Default;expMode=receivedOptions?.expedition==true?X.Mode.Expedition:X.Mode.Waves;}"),"a guest sees the room's run: the default expedition in Wave Defense");
            Check(Regex.Matches(exp,@"if\(edit\)ExpTurnTo").Count==2&&exp.Contains("if(!edit)return;if(!X.WaveSupported(start))")&&exp.Contains("bool live=ExpLive(a)&&edit;float h=U.HoverAmount(9500+index")&&exp.Contains("ExpPartyCard(slot,party[i],i,ce,now,seat,edit&&i==newest,bots)")&&exp.Contains("bool canAdd=edit&&"),"a guest's keys, mode tiles, wave row and bot buttons do nothing");
            Check(exp.Contains("BeginRules(false,width);var o=rules;"),"the drawer's party rows go through BeginRules (read-only on a guest connection)");
            // Presentation only: no room field or message added (Astra's contract in DEV_LOG).
            var fields=typeof(NativeRoomOptions).GetFields().Select(f=>f.Name).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
            Check(string.Join(",",fields)=="botDifficulty,botOptions,bothDraft,bots,choices,cooperative,expedition,friendlyFire,hostTeam,map,mirrorWeapons,randomizeMap,rounds,selectionSeconds,sets,skipBotDrafts,skipPlayerIntros,skipWaves,stationary,teams","NativeRoomOptions keeps its fields ("+string.Join(",",fields)+")");
            Check(!exp.Contains("type=\"expedition")&&!Regex.IsMatch(exp,@"new NativeNetMessage\{type=""(?!profile"")"),"the screen sends no new message");
            // The transitions read EXPEDITIONS / PVE for this screen and a PvE room.
            Check(Regex.Match(mt,"MenuTitles=\\{([^}]*)\\}").Groups[1].Value.Split(',')[5].Trim()=="\"EXPEDITIONS\""&&Regex.Match(mt,"MenuEyebrows=\\{([^}]*)\\}").Groups[1].Value.Split(',')[5].Trim()=="\"PVE\"","the transition titles EXPEDITIONS / PVE");
            Check(mt.Contains("if(dest==MenuScreen.Boss||dest==MenuScreen.Lobby&&prefs.pve){eyebrow=MenuEyebrows[(int)MenuScreen.Boss];title=MenuTitles[(int)MenuScreen.Boss];}"),"a PvE room's transition reads EXPEDITIONS too");
            // Reduced effects: every motion of the screen has a calm branch.
            Check(exp.Contains("if(!prefs.reducedEffects){float y=Mathf.Repeat(now*34")&&exp.Contains("float sweep=calm?2:")&&exp.Contains("if(calm){x=0;sx=1;}")&&exp.Contains("if(lit&&!prefs.reducedEffects)Sheen(d,a);")&&exp.Contains("float p=calm?0:Mathf.Repeat(now*.8f"),"Reduced effects: no drift, sweep, card turn, sheen or pulse");
            // Keyboard and controller.
            foreach(var key in new[]{"KeyCode.Q","KeyCode.E","KeyCode.A","KeyCode.D","KeyCode.LeftArrow","KeyCode.RightArrow","KeyCode.Tab","KeyCode.L","KeyCode.Return","KeyCode.Alpha1","KeyCode.JoystickButton0","KeyCode.JoystickButton1","KeyCode.JoystickButton4","KeyCode.JoystickButton5"})Check(exp.Contains(key),"navigable: "+key);
            Check(exp.Contains("GUIUtility.keyboardControl!=0||updateOpen")&&exp.Contains("expPadFrame!=Time.frameCount"),"keys wait for text fields and the update card; the controller is read once a frame");
        }
    }
}
#endif
