using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The source's top layer: the modal Connection report <dialog> (main.ts:116 shell, :183-184, :271; style.css:53) and
    // the fixed #lobby-return banner (main.ts:363 drawHud; lobby.css:96-97, z-index 1600). Both sit above every menu, the
    // pause menu and the HUD, so they are drawn from this component's own OnGUI at a lower GUI.depth than ConversionGame's:
    // IMGUI paints it last and hands it input events first, which is how the dialog stays modal and the banner's Cancel
    // stays clickable over the menus without a hook in the game's OnGUI. Presentation only: it reads the end-flow and
    // connection APIs (docs/UNITY-LOGIC-HANDOFF.md) and calls their actions.
    public sealed class NativeTopLayer:MonoBehaviour
    {
        private ConversionGame game;private float lookAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){if(FindFirstObjectByType<NativeTopLayer>()!=null)return;var go=new GameObject("Chip Arena Top Layer");DontDestroyOnLoad(go);go.AddComponent<NativeTopLayer>();}
        // Kit drawing only (no GUILayout), so the Layout event is skipped.
        private void Awake()=>useGUILayout=false;
        private void OnGUI()
        {
            if(game==null){if(Time.unscaledTime<lookAt)return;lookAt=Time.unscaledTime+1;game=FindFirstObjectByType<ConversionGame>();if(game==null)return;}
            GUI.depth=-100;game.TopLayerGUI();
        }
    }

    public sealed partial class ConversionGame
    {
        // Called by NativeTopLayer in every event. Notices that follow the end flow tick once per painted frame.
        internal void TopLayerGUI()
        {
            if(dedicated||prefs==null||content==null||error.Length>0)return;
            var e=Event.current;if(e.type==EventType.Repaint){EndFlowNotices();SessionSamples();}
            if(reportOpen&&(arena!=reportArena||arena!=null&&!menu))reportOpen=false;
            bool banner=arena!=null&&LobbyReturnSeconds>0&&!TopLayerCovered();if(!banner&&!reportOpen)return;
            GUI.skin=NativeMenuSkin.Skin;GUI.matrix=Matrix4x4.identity;GUI.color=GUI.contentColor=Color.white;GUI.enabled=true;U.Begin(prefs.uiScale);
            // The menus below set menuBlockedNow while the dialog is open (and the home screen's update card blocks the buttons
            // under it); neither blocks these.
            bool blocked=menuBlockedNow;menuBlockedNow=false;
            // The dialog is above the banner: painted after it, but given input first so its backdrop blocks the banner's Cancel.
            if(e.type==EventType.Repaint){if(banner)LobbyReturnBanner();if(reportOpen)ConnectionReportDialog();}
            else{if(reportOpen)ConnectionReportDialog();if(banner)LobbyReturnBanner();}
            menuBlockedNow=blocked;
        }
        // The Slender jumpscare replaces the whole GUI (SlenderGUI returns true): nothing goes over it. The stalk static is
        // drawn under the HUD, which stays, so the banner stays too (the source's .slender-static is a translucent
        // pointer-events:none layer over it).
        private bool TopLayerCovered()=>!menu&&arena?.Local!=null&&arena.Local.ScareUntil>arena.Time;

        // ---- Notices from the end flow and the account ----------------------------------------------------------------
        // main.ts:182: each rematch vote broadcast shows "n/N pilots ready for rematch." (rooms only: an offline vote
        // restarts at once). main.ts:392 refreshAccount: "Saved pilot loaded." when Reload saved pilot completes, else the
        // service's message.
        private int seenVotes;private bool reloadPending;
        private void EndFlowNotices()
        {
            if(arena==null||match==null||match.Phase!=NativePhase.Match)seenVotes=0;
            else{int v=RematchVotes;if(v>seenVotes&&Online&&!HostLost)Notice(v+"/"+RematchRequired+" pilots ready for rematch.");seenVotes=v;}
            if(reloadPending&&account!=null&&!account.Busy){reloadPending=false;string s=account.Status??"";Notice(s.StartsWith("Signed in as",StringComparison.Ordinal)?"Saved pilot loaded.":s);}
        }

        // ---- #lobby-return banner ----------------------------------------------------------------------------------------
        // Over the match (paused or not) while the three-second return runs: "Returning to lobby in N…" (at least 1, as
        // main.ts rounds up) over "Everyone will return together." in a room or "Your match will reset." offline, with
        // Cancel for the host and offline. lobby.css .lobby-return: top 22, centred, #171c27, 1 px #ffac70, radius 12,
        // 16 / 22 px padding, 24 px gap, 0 12px 45px #0009; strong 20 px #fff, span 14 px #c8ceda 4 px below; the .btn is
        // at least 90 px wide. It takes the clicks over it (pointer-events:auto).
        private int bannerSeconds=-1;private string bannerTitle="";
        private void LobbyReturnBanner()
        {
            int s=Mathf.Max(1,Mathf.CeilToInt(LobbyReturnSeconds));if(s!=bannerSeconds){bannerSeconds=s;bannerTitle="Returning to lobby in "+s+"…";}
            // The source runs a local boss test as a room of one (mode 'online' over a LocalRoom, main.ts:197), so it gets the
            // room line; its local host keeps Cancel (mode 'local' or the room owner).
            string sub=Online||arena.Pve!=null?"Everyone will return together.":"Your match will reset.";bool cancel=!Online||Hosting;
            float tw=Mathf.Max(U.Measure(bannerTitle,20,true).x,U.Measure(sub,14).x),bw=cancel?Mathf.Max(90,U.Measure("Cancel",15,true).x+38):0,content=cancel?48:45;
            float w=Mathf.Min(U.W-32,1+22+tw+(cancel?24+bw:0)+22+1),h=1+16+content+16+1;var r=new Rect((U.W-w)/2,22,w,h);
            U.Glow(new Rect(r.x,r.y+12,r.width,r.height),new Color(0,0,0,.6f),40);U.Round(r,U.Hex(0x171c27),12,U.Hex(0xffac70));
            float ty=r.y+17+(content-45)/2,tx=r.x+23,room=r.width-46-(cancel?24+bw:0);
            U.Text(new Rect(tx,ty,room,24),bannerTitle,20,Color.white,TextAnchor.MiddleLeft,bold:true);U.Text(new Rect(tx,ty+28,room,17),sub,14,U.Hex(0xc8ceda),TextAnchor.MiddleLeft);
            if(cancel&&MenuButton(new Rect(r.xMax-23-bw,r.y+17,bw,48),null,"Cancel",null,false,true,1,15,true)){CancelLobbyReturn();if(!Online)afterMatch=MenuScreen.Home;}
            var e=Event.current;if((e.isMouse||e.type==EventType.ScrollWheel)&&U.Hover(r))e.Use();
        }

        // Where an offline return lands, as main.ts sends a local match back to its practice screen: Play vs Bots, or Boss
        // Test for a local boss run; the sandbox goes home.
        private MenuScreen OfflineLobby()=>arena==null||arena.Sandbox?MenuScreen.Home:arena.Pve!=null?MenuScreen.Boss:MenuScreen.Play;

        // ---- Connection report ---------------------------------------------------------------------------------------
        // main.ts:183 connectionReport(), shown by showConnectionReport() from Settings, the pause menu and the lobby's
        // Advanced Options. The text is taken once when the dialog opens, as the source fills it then. It names the
        // connection (the top-bar label), room code, phase, pilots, frame rate and step time, then the measured metrics: a
        // guest's (and an offline client's) own report from ConnectionReportJson(), spliced in as the source spreads its
        // summary; the host's per-guest PeerMetrics as "peers". No account or resume tokens, addresses or URLs go in.
        private bool reportOpen;private NativeArena reportArena;private string reportText="";private float reportScroll,reportPreH,reportWrapW=-1,reportWrapS=-1;
        private readonly List<string> reportLines=new List<string>();private Action reportDraw;
        private void OpenConnectionReport()
        {
            var inv=CultureInfo.InvariantCulture;var entries=new List<string>();
            entries.Add("  \"transport\": "+JsonText(TransportLabel()));entries.Add("  \"room\": "+JsonText(Online?(Hosting?roomCode:joinCode):""));
            entries.Add("  \"phase\": "+JsonText(match!=null?match.Phase.ToString().ToLowerInvariant():"lobby"));
            entries.Add("  \"players\": "+(match!=null?match.Seats.Count:InRoom?lobbySeats.Length:0).ToString(inv));
            entries.Add("  \"fps\": "+Mathf.RoundToInt(FrameRate()).ToString(inv));entries.Add("  \"simulationTickMs\": "+stepMs.ToString("0.##",inv));
            if(Hosting)
            {
                var peers=PeerMetrics??Array.Empty<NativeConnectionReport>();var sb=new StringBuilder("  \"peers\": [");
                for(int i=0;i<peers.Length;i++){sb.Append(i==0?"\n":",\n").Append(Reindent(JsonUtility.ToJson(peers[i],true),"    "));}
                sb.Append(peers.Length>0?"\n  ]":"]");entries.Add(sb.ToString());
            }
            else
            {
                // The client report's fields at the top level (its outer braces dropped), in the 2-space style of JSON.stringify.
                // Once the host is lost the link's metrics are gone (ConnectionMetrics reads zeros), so the last ones measured
                // while it was up are used, marked disconnected: the source keeps networkMetrics until disconnect() on leave.
                string json;if(HostLost&&lastMetrics!=null&&lastMetricsArena==arena){lastMetrics.connected=false;json=JsonUtility.ToJson(lastMetrics,true);}else json=ConnectionReportJson()??"{}";
                var lines=Reindent(json,"").Split('\n');var inner=new StringBuilder();
                for(int i=1;i<lines.Length-1;i++){if(inner.Length>0)inner.Append('\n');inner.Append(lines[i]);}if(inner.Length>0)entries.Add(inner.ToString());
            }
            reportText="{\n"+string.Join(",\n",entries)+"\n}";reportWrapW=-1;reportScroll=0;reportArena=arena;reportOpen=true;
        }
        // Sampled once per painted frame: the frame rate as renderer.ts measures it (the mean of the last 120 real frame
        // times, so the sandbox's simulation speed does not scale it), and the guest's link metrics twice a second while the
        // host is reachable (kept for a report taken after the host is lost).
        private readonly float[] frameTimes=new float[120];private int frameCount,frameHead;private double frameSum;
        private NativeConnectionReport lastMetrics;private NativeArena lastMetricsArena;private float lastMetricsAt=-9;
        private void SessionSamples()
        {
            float dt=Time.unscaledDeltaTime;if(dt>0){if(frameCount==frameTimes.Length)frameSum-=frameTimes[frameHead];else frameCount++;frameTimes[frameHead]=dt;frameSum+=dt;frameHead=(frameHead+1)%frameTimes.Length;}
            if(server!=null&&arena!=null&&Time.unscaledTime>=lastMetricsAt){lastMetricsAt=Time.unscaledTime+.5f;lastMetrics=ConnectionMetrics;lastMetricsArena=arena;}
        }
        private float FrameRate()=>frameCount>0&&frameSum>0?(float)(frameCount/frameSum):1/Mathf.Max(.0001f,Time.unscaledDeltaTime);
        private static string JsonText(string s)=>"\""+(s??"").Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
        // JsonUtility's pretty print, re-indented to two spaces per level (its own unit measured from the first indented
        // line) under `prefix`.
        private static string Reindent(string json,string prefix)
        {
            var lines=json.Replace("\r","").Trim().Split('\n');int unit=0;foreach(var l in lines){int n=0;while(n<l.Length&&l[n]==' ')n++;if(n>0){unit=n;break;}}
            var sb=new StringBuilder();for(int i=0;i<lines.Length;i++){string l=lines[i];int n=0;while(n<l.Length&&l[n]==' ')n++;if(i>0)sb.Append('\n');sb.Append(prefix).Append(' ',unit>0?n/unit*2:n).Append(l,n,l.Length-n);}
            return sb.ToString();
        }
        // The pre's lines, broken anywhere when wider than the box (white-space:pre-wrap; overflow-wrap:anywhere).
        private void WrapReport(float width)
        {
            reportWrapW=width;reportWrapS=U.S;reportLines.Clear();
            foreach(var line in reportText.Split('\n'))
            {
                string rest=line;float w;
                while(rest.Length>1&&(w=U.Measure(rest,12,false,true).x)>width){int n=Mathf.Clamp(Mathf.FloorToInt(rest.Length*width/w),1,rest.Length-1);reportLines.Add(rest.Substring(0,n));rest=rest.Substring(n);}
                reportLines.Add(rest);
            }
        }
        // style.css .network-report: min(620px, 94vw), at most 85vh, #101820 with a 1 px #46727b edge, radius 12, 24 px
        // padding, over a #000a backdrop; "Connection report" (the UA h2: 24 px bold, .83em margins) in #e3f1fa, the 13 px
        // #a8bac9 line, the report in 12 px mono at line-height 1.5, then .actions (24 px above, gap 10): Copy report
        // (primary) and Close, no icons. The pre scrolls when the dialog would pass 85vh (the source scrolls the dialog).
        // Escape closes it, as a modal <dialog> does; so does leaving the screen it was opened over.
        private void ConnectionReportDialog()
        {
            var e=Event.current;if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){reportOpen=false;e.Use();return;}
            float W=U.W,H=U.H,dw=Mathf.Min(620,W*.94f),iw=dw-50;if(reportWrapW!=iw||reportWrapS!=U.S)WrapReport(iw);
            float full=reportLines.Count*18,chrome=1+24+20+29+20+16+13+24+48+24+1;reportPreH=Mathf.Min(full,Mathf.Max(54,H*.85f-chrome));float dh=chrome+reportPreH,max=Mathf.Max(0,full-reportPreH);
            var d=new Rect(Mathf.Round((W-dw)/2),Mathf.Round((H-dh)/2),dw,dh);
            U.Fill(new Rect(0,0,W,H),new Color(0,0,0,.667f));U.Round(d,U.Hex(0x101820),12,U.Hex(0x46727b));
            float x=d.x+25,y=d.y+25+20;
            U.Text(new Rect(x,y,iw,29),"Connection report",24,U.Hex(0xe3f1fa),TextAnchor.MiddleLeft,bold:true);y+=29+20;
            U.Text(new Rect(x,y,iw,16),"Take this report from both players while the lag is happening.",13,U.Hex(0xa8bac9),TextAnchor.MiddleLeft);y+=16+13;
            var pre=new Rect(x,y,iw,reportPreH);
            if(e.type==EventType.ScrollWheel&&U.Hover(d)){reportScroll+=e.delta.y*20;e.Use();}reportScroll=Mathf.Clamp(reportScroll,0,max);
            if(e.type==EventType.Repaint)
            {
                U.BeginDeferred();U.Defer(reportDraw??(reportDraw=DrawReportLines));U.EndDeferred(pre,new Vector2(0,reportScroll));
                if(max>0){float th=Mathf.Max(24,reportPreH*reportPreH/full);U.Round(new Rect(pre.xMax+6,pre.y+(reportPreH-th)*(reportScroll/max),4,th),U.Hex(0x46727b,.8f),4);}
            }
            y=pre.yMax+24;float bw=(iw-10)/2;
            if(MenuButton(new Rect(x,y,bw,48),null,"Copy report",null,true,true,1,15,true)){GUIUtility.systemCopyBuffer=reportText;Notice("Connection report copied.");}
            if(MenuButton(new Rect(x+bw+10,y,bw,48),null,"Close",null,false,true,1,15,true))reportOpen=false;
            // Modal: nothing under the backdrop takes the mouse or keys while it is open.
            if(e.isMouse||e.type==EventType.ScrollWheel||e.type==EventType.KeyDown)e.Use();
        }
        private void DrawReportLines()
        {
            int first=Mathf.Max(0,Mathf.FloorToInt(reportScroll/18)),last=Mathf.Min(reportLines.Count,Mathf.CeilToInt((reportScroll+reportPreH)/18)+1);var ink=U.Hex(0xe3f1fa);
            for(int i=first;i<last;i++)U.Text(new Rect(0,i*18,reportWrapW,18),reportLines[i],12,ink,TextAnchor.MiddleLeft,mono:true);
        }
        // The lobby's Advanced Options entry (main.ts:156 roomAdvanced, pve/menu.ts:43): the full-width .btn with the
        // activity icon at 14 px under the INVITE LINK (22 px above, 15 px below), on the room rows' insets (RuleRow).
        private void ConnectionReportButton()
        {
            var r=GUILayoutUtility.GetRect(0,22+48+15,GUILayout.ExpandWidth(true));float a=GUI.color.a,l=rulesL,w=Mathf.Max(10,r.width-rulesL-rulesR);
            U.Defer(()=>{if(MenuButton(new Rect(r.x+l,r.y+22,w,48),"activity","Connection report",null,false,true,a,14))OpenConnectionReport();});
        }

        // ---- Connection label -----------------------------------------------------------------------------------------
        // main.ts `connection`: the room's link (DISCONNECTED once the host's link closes, CONNECTION STALLED after 3 s
        // without a message from it), or the source's local labels outside a room.
        private string TransportLabel()
        {
            if(HostLost)return "DISCONNECTED";
            if(!Online)return arena==null?"LOCAL ENGINE":arena.Sandbox?"LOCAL SANDBOX":arena.Pve!=null?"LOCAL BOSS TEST":"LOCAL PRACTICE";
            bool lost=!Hosting&&(server==null||server.Closed),stalled=!Hosting&&!lost&&(DateTime.UtcNow-server.LastReceived).TotalSeconds>3;
            return lost?"DISCONNECTED":stalled?"CONNECTION STALLED":listener!=null?"LAN · HOST":Hosting?"INTERNET RELAY · HOST":internetRooms?"INTERNET RELAY":"LOCAL NETWORK";
        }
        // The lobby pill's text with the source's " · N MS" (main.ts drawHud, mode online): the round trip from
        // ConnectionMetrics, read twice a second; rebuilt only when a part changes.
        private string pillText="",pillLabel,pillCode;private int pillMs=-1,pillMsNow;private float pillMsAt=-9;
        private string ConnectionPillText(out bool lost)
        {
            string label=TransportLabel(),code=Hosting?roomCode:joinCode;lost=label=="DISCONNECTED";
            if(Time.unscaledTime>=pillMsAt){pillMsAt=Time.unscaledTime+.5f;pillMsNow=Mathf.Max(0,Mathf.RoundToInt((float)ConnectionMetrics.rttMs));}
            if(label!=pillLabel||code!=pillCode||pillMsNow!=pillMs){pillLabel=label;pillCode=code;pillMs=pillMsNow;pillText=label+(string.IsNullOrEmpty(code)?"":" / "+code)+" · "+pillMs+" MS";}
            return pillText;
        }
    }
}
