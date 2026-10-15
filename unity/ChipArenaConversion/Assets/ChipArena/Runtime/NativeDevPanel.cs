using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV panel (owner 2026-10-14: "Improve sandbox tools- Better UI, more control ... a "DEV Abuse" Toggle in dev
    /// settings, this will allow dev options in matches (online included) for only dev accounts"). A kit-drawn cyber overlay
    /// on the right of the match (F3), usable while playing: F4 frees the cursor to click it (the pilot holds still while the
    /// cursor is free), the hotkeys work with it closed. A left tab rail (Pilot, Interact, Dummies, Chips, Arena, Hazards,
    /// Previews, Camera, Simulation), a header with the mode and authority state, a footer with the last action's verdict and
    /// the hotkeys. Every button resolves through Core NativeDevActions: the sandbox's own calls in the offline Developer
    /// Sandbox (NativeSandbox.cs DevRun, unchanged arguments), the view tools anywhere the panel is, DEV commands through
    /// NativeDevLink to Astra's DEV authority (with the DEV Abuse opt-in, during an active round; disabled with the reason otherwise).
    /// Repaint draws allocate nothing once warm (cached labels, no LINQ, no closures).
    /// DEV commands are Astra's (docs/DEV-ABUSE-AUTHORITY.md): SendDevCommand with the session opt-in DevAbuseEnabled (the saved
    /// toggle, verified developers only), replies through DevReplyReceived; the panel shows what the authority answered and
    /// draws every effect from replicated state (devGod / devInfiniteAmmo / devNoCooldowns, devTimeScale, devRevealed,
    /// devDummy, devCommandsRan), never optimistically.
    /// </summary>
    public sealed partial class ConversionGame
    {
        /// <summary>The panel's end of Astra's DEV commands: Sender is SendDevCommand, Receive takes DevReplyReceived.</summary>
        internal readonly NativeDevLink devLink=new NativeDevLink();private bool devReplyHooked;
        private bool devOpen,devCursorFree,devHideHud,devLogOn,devGraphOn,devTyping,devLogMine,devChipSelf=true,devBodyHot,devSearchFocus;
        private bool devOverlayHit,devOverlayMelee,devOverlayHazards,devOverlayNav,devOverlayAnchors,devOverlayShots;
        private NativeDevTab devTab=NativeDevTab.Pilot;private readonly float[] devScroll=new float[NativeDevLayout.Tabs],devContent=new float[NativeDevLayout.Tabs];
        private readonly NativeDevFly devFly=new NativeDevFly();private readonly NativeDevLog devLog=new NativeDevLog();private NativeDevLogFilter devLogFilter=NativeDevLogFilter.All&~NativeDevLogFilter.World;
        private readonly NativeDevGraph devFrameGraph=new NativeDevGraph(120),devStepGraph=new NativeDevGraph(120),devRttGraph=new NativeDevGraph(60);private float devRttAt;
        private readonly NativeDevChips devChips=new NativeDevChips();private bool devChipsLoaded;private string devChipId;private int devChipSeat;
        private readonly NativeDevReveal devReveal=new NativeDevReveal();
        private NativeArena devArena;private Func<int,string> devNameOf;private NativeDevContext devCtx;private NativeDevMode devMode;
        private string devStatus="",devHoverLabel,devHoverReason,devLinkSeen="";private bool devStatusBad;private float devStatusAt=-100,devHideAt=-100;
        private Rect devBody,devPanelRect,devSearchRect;private Vector2 devMouse;private int devWid,devDrag=-1,devTarget;
        // Panel values the sandbox routes and DEV commands read (NativeSandbox.cs DevRun, DevArgs).
        private string devWeapon="pistol",devTargetWeapon="pistol",devMap,devDifficulty="normal",devRainMaterial="wood";
        private float devRespawn=5,devRepeat,devSplat=10,devSpeed=1,devLaunchForce=20,devFreezeSeconds=3;private int devAroundCount=3,devHazard,devSpawnTeam=-1;private string devSpawnInput="ai";
        private Vector3 devPilotCamPos;private Quaternion devPilotCamRot;private bool devPilotCamValid;
        private string[] devWeaponIds,devWeaponNames,devMapIds,devMapNames;private readonly Dictionary<string,Color> devRarityColor=new Dictionary<string,Color>();
        private readonly Dictionary<string,string> devRarityName=new Dictionary<string,string>(),devChipFit=new Dictionary<string,string>();private float devChipFitWidth;
        private static readonly string[] DevDifficulties={"dummy","easy","normal","hard"},DevSelfDummy={"SELF","DUMMY"},DevOnOff={"ON","OFF"};
        private static GUIStyle devSearchStyle;

        // ---- Context ----------------------------------------------------------------------------------------------------
        private NativeDevContext DevContext()=>NativeDevContext.Of(arena!=null&&match!=null,arena!=null&&arena.Sandbox&&!arena.Replica,Online,Hosting,account!=null&&account.Developer,prefs!=null&&prefs.devAbuse,DevAbuseEnabled&&devLink.Available,match!=null&&match.Phase==NativePhase.Fight,DevMonsterCommands);
        /// <summary>A guest of a host without DEV commands (devProtocol 0): SendDevCommand refuses at once (the header says so).</summary>
        private bool DevOldHost=>server!=null&&devHostProtocol<1;
        /// <summary>The match's authority offers the proposed monster commands: a guest's host advertised devProtocol 2 or later;
        /// offline or hosting, the local NativeDevRules knows spawn-monster (docs/DEV-TOOLS.md "Monsters").</summary>
        private bool DevMonsterCommands=>server!=null?devHostProtocol>=NativeDevMonsters.Protocol:NativeDevContract.Known(NativeDevMonsters.Spawn);
        private string DevSender(string action,int target,string[] args)=>SendDevCommand(action,target,args);
        private void DevOnReply(NativeDevReply reply)=>devLink.Receive(reply);
        /// <summary>The DEV panel is open for this match (the pause menu's DEV Tools item, F3).</summary>
        private bool DevPanelAvailable=>arena!=null&&NativeDevGate.PanelAvailable(DevContext());
        /// <summary>The pilot holds still (neutral input) while the fly camera runs or the panel has the cursor or the keyboard.</summary>
        private bool DevHoldsPilot=>devFly.On||devOpen&&(devCursorFree||devTyping);
        /// <summary>The match camera's cursor lock: free while the panel has the cursor, locked while flying.</summary>
        private bool DevLook(bool look)=>devOpen&&devCursorFree?false:look||devFly.On&&!menu&&arena!=null;
        private NativePilot DevLocal(){if(arena==null)return null;foreach(var p in arena.Pilots)if(p.Seat.id==arena.LocalId)return p;return null;}
        // The dummy the Dummies tab acts on: a DEV-created dummy first, else the first bot (the sandbox's dummy).
        private NativePilot DevDummy(){if(arena==null)return null;foreach(var p in arena.Pilots)if(p.Seat.devDummy&&p.Seat.connected)return p;foreach(var p in arena.Pilots)if(p.Seat.bot&&!p.Seat.AuthorityBoss)return p;return null;}
        private NativePilot DevPilot(int seat){if(arena==null)return null;foreach(var p in arena.Pilots)if(p.Seat.id==seat)return p;return null;}
        private string DevName(int seat){var p=DevPilot(seat);return p!=null?p.Seat.name:null;}
        private void DevOpen(bool cursor){if(!DevPanelAvailable)return;devOpen=true;devCursorFree=cursor;menu=false;pauseScreen=PauseScreen.Menu;}
        private void DevSay(string text,bool bad){devStatus=text??"";devStatusBad=bad;devStatusAt=Time.unscaledTime;}

        // ---- Update (UpdateFrame, after CameraKeys) -----------------------------------------------------------------------
        private void DevUpdate(bool typing)
        {
            if(arena!=devArena)DevResetArena();
            // The session opt-in follows the saved Settings · Developer toggle, for a verified developer only (default off).
            DevAbuseEnabled=NativeDevGate.CommandsEnabled(prefs!=null&&prefs.devAbuse,account!=null&&account.Developer);
            if(!devReplyHooked){devReplyHooked=true;devLink.Sender=DevSender;DevReplyReceived+=DevOnReply;}
            devLink.Tick(Time.unscaledTime);if(arena==null)return;
            var c=DevContext();
            // Every client follows the reveal state (dormant until Astra replicates devRevealed).
            float now=Time.unscaledTime;foreach(var p in arena.Pilots)if(devReveal.Observe(p.Seat.id,p.Seat.name,NativeDevTags.Revealed(p.Seat),now))DevRevealStart(p.Seat.id,p.Seat.name,false);
            if(!NativeDevGate.PanelAvailable(c)){devOpen=devCursorFree=devHideHud=devTyping=false;devFly.On=false;return;}
            if(!devChipsLoaded){devChipsLoaded=true;devChips.LoadFavourites(prefs.devChipFavourites);}
            devFrameGraph.Push(Time.unscaledDeltaTime*1000);devStepGraph.Push((float)stepMs);
            if(Online&&now>=devRttAt){devRttAt=now+.5f;devRttGraph.Push(DevRtt());}
            devNameOf??=DevName;devLog.Observe(arena.Events,arena.Generation,devNameOf);
            devTyping=devOpen&&devSearchFocus&&typing;
            bool live=!menu&&!IntroActive&&!ReplayActive&&previewArena==null&&match.Phase!=NativePhase.Draft;if(!live)return;
            if(Input.GetKeyDown(KeyCode.F3)){devOpen=!devOpen;devCursorFree=devOpen;if(!devOpen)DevBlur();}
            if(devOpen&&Input.GetKeyDown(KeyCode.F4)){devCursorFree=!devCursorFree;if(!devCursorFree)DevBlur();}
            if(Input.GetKeyDown(KeyCode.F1))devLogOn=!devLogOn;
            if(Input.GetKeyDown(KeyCode.F2))devGraphOn=!devGraphOn;
            if(Input.GetKeyDown(KeyCode.F11)){devHideHud=!devHideHud;devHideAt=now;}
            if(Input.GetKeyDown(KeyCode.F10))DevInvoke("fly");
            if(Input.GetKeyDown(KeyCode.F5))DevInvoke("me-restore");
            if(Input.GetKeyDown(KeyCode.F6))DevInvoke("dummy-down");
            if(Input.GetKeyDown(KeyCode.F7))DevInvoke("dummy-cast");
            if(!devFly.On)return;
            if(!devTyping&&Input.GetKeyDown(KeyCode.Home))DevInvoke("fly-snap");
            if(!(devOpen&&devCursorFree)){devFly.Look(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y"),prefs.sensitivity);devFly.Wheel(Input.mouseScrollDelta.y);}
            if(devTyping)return;
            float right=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),forward=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0),up=(Input.GetKey(KeyCode.E)||Input.GetKey(KeyCode.Space)?1:0)-(Input.GetKey(KeyCode.Q)?1:0);
            devFly.Move(right,up,forward,Input.GetKey(KeyCode.LeftShift),Input.GetKey(KeyCode.LeftControl),Time.unscaledDeltaTime);
        }
        private void DevResetArena()
        {
            devArena=arena;devLog.Clear();devReveal.Reset();devFly.On=false;devPilotCamValid=false;devFrameGraph.Clear();devStepGraph.Clear();devRttGraph.Clear();devTarget=0;devWeaponIds=null;devHazard=0;
            if(arena==null){devOpen=devCursorFree=devHideHud=devTyping=false;return;}
            devRespawn=arena.DummyRespawn;devRepeat=arena.SandboxRepeat;devSplat=arena.SandboxSplatDelay;devSpeed=sandboxSpeed;devMap=arena.MapId;
        }
        private float DevRtt()
        {
            if(!Hosting)return (float)ConnectionMetrics.rttMs;float worst=0;foreach(var r in PeerMetrics)worst=Mathf.Max(worst,(float)r.rttMs);return worst;
        }
        private void DevBlur(){if(devSearchFocus)GUIUtility.keyboardControl=0;devSearchFocus=false;}

        // ---- Actions ----------------------------------------------------------------------------------------------------
        /// <summary>Runs panel action `id` where NativeDevActions allows it: the sandbox route, a view tool, or a DEV command.
        /// A disabled action only says why.</summary>
        private void DevInvoke(string id,bool quiet=false)
        {
            var a=NativeDevActions.Find(id);var c=DevContext();var state=NativeDevActions.Resolve(a,c,out string reason);
            if(state!=NativeDevState.Live){DevSay((a?.Label??id)+" · "+reason,true);return;}
            if(a.Kind==NativeDevKind.Presentation){DevView(id);return;}
            if(NativeDevActions.RunsInSandbox(a,c)){string result=DevRun(id);if(!quiet||result==DevRefused)DevSay(a.Label+" · "+result,result==DevRefused);return;}
            var sent=devLink.Send(a.Contract,DevCommandTarget(a),DevArgs(a),a.Label,Time.unscaledTime);
            if(sent==null)DevSay(a.Label+" · not sent: its arguments are outside the command's limits",true);
        }
        private void DevSend(string id,params string[] args)
        {
            var a=NativeDevActions.Find(id);var c=DevContext();if(NativeDevActions.Resolve(a,c,out string reason)!=NativeDevState.Live){DevSay((a?.Label??id)+" · "+reason,true);return;}
            if(devLink.Send(a.Contract,DevCommandTarget(a),args,a.Label,Time.unscaledTime)==null)DevSay(a.Label+" · not sent: its arguments are outside the command's limits",true);
        }
        // The command's target seat (0: the sender, as SendDevCommand reads it).
        private int DevCommandTarget(NativeDevAction a)
        {
            if(a.Tab==NativeDevTab.Interact||a.Tab==NativeDevTab.Monsters||a.Id=="debris")return devTarget==arena?.LocalId?0:devTarget;if(a.Tab==NativeDevTab.Chips)return devChipSeat==arena?.LocalId?0:devChipSeat;
            if(a.Id=="dummy-restore"||a.Id=="dummy-down")return DevDummy()?.Seat.id??0;
            return 0;
        }
        // The command arguments (docs/DEV-ABUSE-AUTHORITY.md "Action arguments"), clamped to NativeDevRules.Validate's limits.
        private string[] DevArgs(NativeDevAction a)
        {
            string N(float v)=>NativeDevLink.Number(v);
            switch(a.Contract)
            {
                case "weapon":return new[]{devWeapon};
                case "swap-weapon":return new[]{devTargetWeapon};
                case "teleport":case "send":{var at=AimPoint(60);return new[]{N(Mathf.Clamp(at.x,-1000,1000)),N(Mathf.Clamp(at.y,-100,500)),N(Mathf.Clamp(at.z,-1000,1000))};}
                case "launch":return new[]{N(Mathf.Clamp(devLaunchForce,NativeDevContract.LaunchMin,NativeDevContract.LaunchMax))};
                case "freeze":return new[]{N(Mathf.Clamp(devFreezeSeconds,0,NativeDevContract.FreezeMax))};
                case "debris-rain":return new[]{devRainMaterial};
                case "spawn-dummies-around":return new[]{NativeDevText.Int(Mathf.Clamp(devAroundCount,NativeDevContract.AroundMin,NativeDevContract.AroundMax))};
                case "spawn-dummy":return new[]{dummySpec,devSpawnInput,NativeDevText.Int(Mathf.Clamp(DevSpawnTeam(),0,NativeDevContract.TeamMax))};
                case "chip-add":case "chip-remove":return new[]{devChipId??"","1"};
                case "time-scale":return new[]{N(Mathf.Clamp(devSpeed,NativeDevContract.TimeScaleMin,NativeDevContract.TimeScaleMax))};
                case "hazard-trigger":{var hz=arena?.Map.hazards;return new[]{hz!=null&&devHazard>=0&&devHazard<hz.Length?hz[devHazard].id:""};}
                case "debris":return new[]{N(Mathf.Clamp(debrisMass,NativeDevContract.DebrisMassMin,NativeDevContract.DebrisMassMax)),N(Mathf.Clamp(debrisSpeed,NativeDevContract.DebrisSpeedMin,NativeDevContract.DebrisSpeedMax)),debrisMaterial,debrisImpact};
                case "spawn-monster":return new[]{devMonsterKind??"",NativeDevText.Int(NativeDevMonsters.ClampCount(devMonsterCount)),NativeDevMonsters.Places[Mathf.Clamp(devMonsterPlace,0,NativeDevMonsters.Places.Length-1)],NativeDevMonsters.Behaviours[Mathf.Clamp(devMonsterBehaviour,0,NativeDevMonsters.Behaviours.Length-1)]};
            }
            return Array.Empty<string>();
        }
        // A spawned dummy's team: the panel's pick, else the team after mine (hostile in a team match).
        private int DevSpawnTeam(){if(devSpawnTeam>=0)return devSpawnTeam;var me=DevLocal();return me==null?1:(me.Seat.team+1)%(NativeDevContract.TeamMax+1);}
        // The view tools (presentation only).
        private void DevView(string id)
        {
            switch(id)
            {
                case "fly":if(devFly.On)devFly.On=false;else if(cameraView!=null){var t=cameraView.transform;var e=t.rotation.eulerAngles;devFly.Place(t.position.x,t.position.y,t.position.z,e.y,e.x>180?e.x-360:e.x);devFly.On=true;}DevSay(devFly.On?"Fly camera · on (F10 to return)":"Fly camera · back to the pilot",false);break;
                case "fly-snap":if(devPilotCamValid){var e=devPilotCamRot.eulerAngles;devFly.Place(devPilotCamPos.x,devPilotCamPos.y,devPilotCamPos.z,e.y,e.x>180?e.x-360:e.x);}break;
                case "overlay-hit":devOverlayHit=!devOverlayHit;break;
                case "overlay-melee":devOverlayMelee=!devOverlayMelee;break;
                case "overlay-hazards":devOverlayHazards=!devOverlayHazards;break;
                case "overlay-nav":devOverlayNav=!devOverlayNav;break;
                case "overlay-anchors":devOverlayAnchors=!devOverlayAnchors;break;
                case "overlay-shots":devOverlayShots=!devOverlayShots;break;
                case "log":devLogOn=!devLogOn;break;
                case "graph":devGraphOn=!devGraphOn;break;
                case "hide-hud":devHideHud=!devHideHud;devHideAt=Time.unscaledTime;break;
            }
        }

        // ---- Camera (LateFrame, after the pilot camera and its shakes) ----------------------------------------------------
        private void DevCamera()
        {
            if(cameraView==null||arena==null)return;var t=cameraView.transform;devPilotCamPos=t.position;devPilotCamRot=t.rotation;devPilotCamValid=true;
            // Forced hazard pulses (NativeGameFrame.devHazards): every client plays each record once with the hazard cues.
            presentation.DevHazardPulses(arena);presentation.DevMonsterRifts(arena,prefs.reducedEffects);
            if(!devFly.On)return;if(!DevPanelAvailable){devFly.On=false;return;}
            t.SetPositionAndRotation(new Vector3(devFly.X,devFly.Y,devFly.Z),Quaternion.Euler(devFly.Pitch,devFly.Yaw,0));
            // Third person from the fly camera: my own body drawn, no view model.
            presentation.FirstPerson=false;presentation.AimHeld=false;viewModelCamera.enabled=false;
        }
        /// <summary>Hide-HUD / screenshot mode (F11): GuiFrame draws nothing over the match (a short hint after switching).</summary>
        private bool DevScreenshot()
        {
            if(!devHideHud||arena==null||menu)return false;if(!DevPanelAvailable){devHideHud=false;return false;}
            // Only the live fight view: intros, the killcam, the draft and the chip picks keep their screens.
            if(match.Phase!=NativePhase.Fight&&match.Phase!=NativePhase.Countdown&&match.Phase!=NativePhase.Result||IntroActive||ReplayActive||previewArena!=null||cinematic.Directing||PvePickOpen)return false;
            float age=Time.unscaledTime-devHideAt;if(age<1.6f&&Event.current.type==EventType.Repaint){float a=Mathf.Clamp01((1.6f-age)/.4f);var r=new Rect(U.W/2-120,18,240,24);H.Tag(r,C.Magenta(),a);U.Text(r,"HUD HIDDEN · F11 TO SHOW",10,C.Ink(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);}
            return true;
        }

        // ---- GUI (GuiFrame, after the HUD) ------------------------------------------------------------------------------
        private void DevGui(float W,float Hh)
        {
            if(menu||arena==null)return;float now=Time.unscaledTime;
            DevRevealMoment(W,Hh,now);DevTimeTag(W);
            devCtx=DevContext();devMode=NativeDevGate.Mode(devCtx);if(devMode==NativeDevMode.Off)return;
            if(devLink.Status!=devLinkSeen){devLinkSeen=devLink.Status;if(devLinkSeen.Length>0)DevSay(devLinkSeen,devLink.StatusBad);}
            if(NativeDevGate.Indicator(devCtx))DevIndicator();
            if(devGraphOn)DevGraphPanel(W);if(devLogOn)DevLogPanel(W,Hh);if(devFly.On)DevFlyTag(W);
            if(devOpen)DevPanel(W,Hh);
        }
        private static Vector2 DevPointer(){var m=GUI.matrix;GUI.matrix=Matrix4x4.identity;var p=Event.current.mousePosition;GUI.matrix=m;return p/Mathf.Max(.01f,U.S);}
        private void DevPanel(float W,float Hh)
        {
            var R=NativeDevLayout.Panel(W,Hh);var p=new Rect(R.X,R.Y,R.W,R.H);devPanelRect=p;devMouse=DevPointer();devWid=0;devHoverLabel=devHoverReason=null;
            var ev=Event.current;bool repaint=ev.type==EventType.Repaint;
            if(devCursorFree&&ev.type==EventType.MouseDown&&devSearchFocus&&!devSearchRect.Contains(devMouse))DevBlur();
            // The frame: a holographic panel (the pause menu's chrome) with a magenta accent in DEV Abuse, cyan in the sandbox.
            Color accent=devMode==NativeDevMode.Abuse?C.Magenta():C.Cyan();
            if(repaint){U.Glow(new Rect(p.x,p.y+24,p.width,p.height),C.Void(.45f),40);U.Backdrop(p,0,10,1);}
            H.Panel(p,1,accent,14,H.Corners.Diagonal,true,.95f);
            DevHeader(p,accent);DevRail(new Rect(p.x,R.BodyY,R.Rail,R.BodyH));
            var body=new Rect(R.BodyX,R.BodyY,R.BodyW,R.BodyH);U.Fill(new Rect(body.x,body.y,H.Px,body.height),C.Edge(.8f));
            int tab=(int)devTab;if(devTab==NativeDevTab.Chips){float top=DevChipsToolbar(body);body.yMin=top;}
            devBody=body;devBodyHot=devCursorFree&&body.Contains(devMouse);
            if(devBodyHot&&ev.type==EventType.ScrollWheel){devScroll[tab]+=ev.delta.y*22;ev.Use();}
            devScroll[tab]=Mathf.Clamp(devScroll[tab],0,Mathf.Max(0,devContent[tab]-body.height));
            U.BeginBand(body);float x=body.x+16,w=body.width-32,y=body.y+12-devScroll[tab],y0=y;
            switch(devTab)
            {
                case NativeDevTab.Pilot:y=DevPilotTab(x,y,w);break;
                case NativeDevTab.Interact:y=DevInteractTab(x,y,w);break;
                case NativeDevTab.Monsters:y=DevMonstersTab(x,y,w);break;
                case NativeDevTab.Dummies:y=DevDummiesTab(x,y,w);break;
                case NativeDevTab.Chips:y=DevChipsGrid(x,y,w);break;
                case NativeDevTab.Arena:y=DevArenaTab(x,y,w);break;
                case NativeDevTab.Hazards:y=DevHazardsTab(x,y,w);break;
                case NativeDevTab.Previews:y=DevPreviewsTab(x,y,w);break;
                case NativeDevTab.Camera:y=DevCameraTab(x,y,w);break;
                default:y=DevSimulationTab(x,y,w);break;
            }
            U.EndBand();devContent[tab]=y-y0+24;
            // The scroll thumb.
            if(devContent[tab]>body.height){float k=body.height/devContent[tab],th=Mathf.Max(24,body.height*k),ty=body.y+(body.height-th)*(devScroll[tab]/Mathf.Max(1,devContent[tab]-body.height));U.Fill(new Rect(body.xMax-4,ty,2,th),C.Cyan(.5f));}
            DevFooter(new Rect(p.x,p.yMax-R.Footer,p.width,R.Footer));
        }
        private void DevHeader(Rect p,Color accent)
        {
            float x=p.x+20,y=p.y+12;
            U.Text(new Rect(x,y,200,14),"CHIP ARENA · DEV",10,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            U.Text(new Rect(x,y+15,240,28),"DEV Tools",21,C.Ink(),TextAnchor.MiddleLeft,bold:true,tracking:-.02f);
            // Right: close, then the cursor, the mode and the context.
            var close=new Rect(p.xMax-40,p.y+14,26,26);float hc=devCursorFree?U.HoverAmount(8990,close):0;H.Cut(close,U.Mix(C.Indigo(.8f),C.Danger(.35f),hc),4,H.Corners.All);NativeIcons.Draw(new Rect(close.x+6,close.y+6,14,14),"x",C.Ink(.7f+.3f*hc));
            if(devCursorFree&&U.Button(close)){devOpen=false;devCursorFree=false;DevBlur();}
            float rx=close.x-8;
            rx=DevTagRight(rx,p.y+15,devCursorFree?"F4 · CURSOR FREE":"F4 · CURSOR LOCKED",devCursorFree?C.Good():C.Muted());
            rx=DevTagRight(rx,p.y+15,devMode==NativeDevMode.Sandbox?"SANDBOX":"DEV ABUSE",accent);
            float ry=p.y+38,lx=close.xMax;
            lx=DevTagRight(lx,ry,!devCtx.Online?"OFFLINE":devCtx.Hosting?"ONLINE · HOST":"ONLINE · GUEST",C.CyanSoft());
            if(DevOldHost)lx=DevTagRight(lx,ry,"HOST: NO DEV PROTOCOL",C.Danger());
            else lx=DevTagRight(lx,ry,DevAbuseEnabled?"DEV COMMANDS ON":"DEV COMMANDS: OPT-IN",DevAbuseEnabled?C.Good():C.Warn());
            var me=DevLocal();if(me!=null&&me.Seat.devCommandsRan)DevTagRight(lx,ry,NativeDevTags.NoProgress,C.Magenta());
        }
        // A small right-aligned tag ending at `right`; returns the next tag's right edge.
        private float DevTagRight(float right,float y,string text,Color tone)
        {
            float w=U.Measure(text,9,true,true,.1f).x+18;var r=new Rect(right-w,y,w,18);H.Tag(r,tone,1);U.Text(new Rect(r.x+6,r.y,r.width-8,r.height),text,9,U.A(tone,1),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);return r.x-6;
        }
        private void DevRail(Rect rail)
        {
            int n=NativeDevLayout.TabNames.Length;float th=Mathf.Min(58,(rail.height-12)/n),y=rail.y+6;
            for(int i=0;i<n;i++)
            {
                var r=new Rect(rail.x+8,y,rail.width-14,th-4);bool on=(int)devTab==i;float h=devCursorFree?U.HoverAmount(8900+i,r):0;
                if(on||h>0)H.Cut(r,on?C.Cyan(.16f):C.Indigo(.6f*h),6,H.Corners.Diagonal);if(on){U.Fill(new Rect(r.x,r.y+4,2,r.height-8),C.Cyan());H.Outline(r,C.Cyan(.5f),6);}
                float icon=Mathf.Min(18,th*.36f);NativeIcons.Draw(new Rect(r.center.x-icon/2,r.y+r.height*.5f-icon*.5f-6,icon,icon),NativeDevLayout.TabIcons[i],on?C.Cyan():U.Mix(C.Muted(),C.Ink(),h));
                U.Text(new Rect(r.x,r.yMax-15,r.width,12),NativeDevLayout.TabNames[i],8,on?C.Ink():C.Muted(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);
                if(devCursorFree&&U.Button(r)&&!on){devTab=(NativeDevTab)i;DevBlur();}
                y+=th;
            }
        }
        private void DevFooter(Rect f)
        {
            U.Fill(new Rect(f.x+14,f.y,f.width-28,H.Px),C.Edge(.9f));float x=f.x+18,y=f.y+10,w=f.width-36;
            // The line: what the hovered button would do (or why it cannot), else the last action's verdict.
            if(devHoverLabel!=null)
            {
                float lw=U.Measure(devHoverLabel,11,true).x;U.Text(new Rect(x,y,lw+4,18),devHoverLabel,11,C.Ink(),TextAnchor.MiddleLeft,bold:true);
                bool needs=ReferenceEquals(devHoverReason,NativeDevActions.OptIn)||ReferenceEquals(devHoverReason,NativeDevActions.NotNow)||ReferenceEquals(devHoverReason,NativeDevActions.SandboxOnly)||ReferenceEquals(devHoverReason,NativeDevActions.Off);
                var lines=U.Wrap(devHoverReason,11,Mathf.Max(20,w-lw-10),false,1);if(lines.Count>0)U.Text(new Rect(x+lw+10,y,w-lw-10,18),lines[0],11,needs?C.Warn():C.Muted(),TextAnchor.MiddleLeft);
            }
            else if(devStatus.Length>0){float age=Time.unscaledTime-devStatusAt;Color tone=devStatusBad?C.Warn():age<1.2f?C.Good():C.Muted();U.Fill(new Rect(x,y+5,6,6),tone);var lines=U.Wrap(devStatus,11,w-14,false,1);if(lines.Count>0)U.Text(new Rect(x+14,y,w-14,18),lines[0],11,tone,TextAnchor.MiddleLeft);}
            else{var lines=U.Wrap(devMode==NativeDevMode.Sandbox?"Sandbox: the sandbox's own tools run here; DEV commands need the DEV Abuse opt-in.":"DEV Abuse: DEV commands go to the match's authority, which checks your verified grant.",11,w,false,1);if(lines.Count>0)U.Text(new Rect(x,y,w,18),lines[0],11,C.Muted(),TextAnchor.MiddleLeft);}
            // The hotkeys.
            float kx=x,ky=y+28;var legend=NativeDevKeys.Legend;
            for(int i=0;i+1<legend.Length;i+=2)
            {
                float kw=H.KeyWidth(legend[i],16),lw=U.Measure(legend[i+1],10).x;if(kx+kw+6+lw>x+w)break;
                H.Key(new Rect(kx,ky,kw,16),legend[i]);U.Text(new Rect(kx+kw+5,ky,lw+4,16),legend[i+1],10,C.Muted(),TextAnchor.MiddleLeft);kx+=kw+5+lw+12;
            }
        }

        // ---- Widgets ----------------------------------------------------------------------------------------------------
        // A small state pill (lit when on); returns the next pill's x.
        private float DevPill(float x,float y,string text,bool on,Color tone)
        {
            float w=U.Measure(text,8,true,true,.1f).x+16;var r=new Rect(x,y,w,18);
            if(Event.current.type==EventType.Repaint&&DevVisible(r)){H.Tag(r,on?tone:C.Dim(),on?1:.55f,on);U.Text(new Rect(r.x+5,r.y,r.width-6,r.height),text,8,on?C.Ink():C.Dim(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);}
            return x+w+6;
        }
        private bool DevVisible(Rect r)=>r.yMax>=devBody.y&&r.y<=devBody.yMax;
        private bool DevClick(Rect r)=>devBodyHot&&DevVisible(r)&&U.Button(r);
        private float DevHead(float x,float y,float w,string title)
        {
            y+=6;float tw=U.Measure(title,10,true,true,.16f).x;U.Text(new Rect(x,y,tw+4,16),title,10,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            U.Fill(new Rect(x+tw+10,y+8,Mathf.Max(0,w-tw-10),H.Px),C.Edge(.9f));return y+24;
        }
        private float DevNote(float x,float y,float w,string text,Color tone)
        {
            var lines=U.Wrap(text,11,w);for(int i=0;i<lines.Count;i++){U.Text(new Rect(x,y,w,16),lines[i],11,tone,TextAnchor.MiddleLeft);y+=16;}return y+4;
        }
        /// <summary>A button in the body: its state's look (live, needs authority, sandbox only), its key and its reason on hover.</summary>
        private bool DevButton(Rect r,string label,NativeDevState state,string reason,bool on=false,string key=null)
        {
            int id=9000+devWid++;bool live=state==NativeDevState.Live;float h=devBodyHot&&DevVisible(r)?U.HoverAmount(id,r):0;
            if(h>.5f){devHoverLabel=label;devHoverReason=reason;}
            if(Event.current.type==EventType.Repaint&&DevVisible(r))
            {
                Color tone=live?C.Cyan():state==NativeDevState.SandboxOnly||state==NativeDevState.Unavailable?C.Dim():C.Warn();
                var d=new Rect(r.x,r.y-h,r.width,r.height);H.Cut(d,on?C.Cyan(.24f):U.Mix(C.Indigo(.92f),C.Indigo(),h),5,H.Corners.Diagonal);
                H.Outline(d,U.A(tone,live?.45f+.5f*h+(on?.3f:0):.4f+.3f*h),5);if(on)U.Fill(new Rect(d.x+3,d.y+d.height*.25f,2,d.height*.5f),C.Cyan());
                float right=d.xMax-8;
                if(!live){string badge=NativeDevActions.Badge(state);float bw=U.Measure(badge,8,true,true,.08f).x+10;var b=new Rect(right-bw,d.center.y-7,bw,14);H.Tag(b,tone,.9f,false);U.Text(b,badge,8,tone,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);right=b.x-6;}
                else if(key!=null){float kw=H.KeyWidth(key,15);H.Key(new Rect(right-kw,d.center.y-7.5f,kw,15),key);right-=kw+6;}
                U.Text(new Rect(d.x+12,d.y,right-d.x-12,d.height),label,12,live?C.Ink():C.Ink(.5f),TextAnchor.MiddleLeft,bold:true);
            }
            return DevClick(r);
        }
        private bool DevAct(Rect r,string id,string label=null,bool on=false)
        {
            var a=NativeDevActions.Find(id);var state=NativeDevActions.Resolve(a,devCtx,out string reason);
            if(DevButton(r,label??a?.Label??id,state,reason,on,a?.Key)){DevInvoke(id);return true;}return false;
        }
        /// <summary>Actions in a grid of `columns`; returns the y under it.</summary>
        private float DevActs(float x,float y,float w,int columns,string[] ids)
        {
            float gap=8,bw=(w-gap*(columns-1))/columns;for(int i=0;i<ids.Length;i++){int c=i%columns;DevAct(new Rect(x+c*(bw+gap),y,bw,32),ids[i]);if(c==columns-1||i==ids.Length-1)y+=40;}return y;
        }
        private bool DevToggle(Rect r,string label,bool value,NativeDevState state,string reason,string key=null)
        {
            int id=9000+devWid++;bool live=state==NativeDevState.Live;float h=devBodyHot&&DevVisible(r)?U.HoverAmount(id,r):0;if(h>.5f){devHoverLabel=label;devHoverReason=reason;}
            if(Event.current.type==EventType.Repaint&&DevVisible(r))
            {
                H.Cut(r,U.Mix(C.Indigo(.6f),C.Indigo(.95f),h),5,H.Corners.Diagonal);
                var sw=new Rect(r.xMax-44,r.center.y-8,34,16);H.Cut(sw,value?C.Cyan(live?.85f:.35f):C.Edge(.8f),3,H.Corners.All);
                U.Fill(new Rect(value?sw.xMax-14:sw.x+2,sw.y+2,12,12),value?C.Void():live?C.Ink(.8f):C.Dim());
                float right=sw.x-8;
                if(!live){string badge=NativeDevActions.Badge(state);float bw=U.Measure(badge,8,true,true,.08f).x+10;var b=new Rect(right-bw,r.center.y-7,bw,14);Color t=state==NativeDevState.SandboxOnly||state==NativeDevState.Unavailable?C.Dim():C.Warn();H.Tag(b,t,.9f,false);U.Text(b,badge,8,t,TextAnchor.MiddleCenter,bold:true,mono:true);right=b.x-6;}
                else if(key!=null){float kw=H.KeyWidth(key,15);H.Key(new Rect(right-kw,r.center.y-7.5f,kw,15),key);right-=kw+6;}
                U.Text(new Rect(r.x+12,r.y,right-r.x-12,r.height),label,12,live?C.Ink():C.Ink(.5f),TextAnchor.MiddleLeft);
            }
            return DevClick(r);
        }
        private bool DevToggleAct(Rect r,string id,bool value,string label=null)
        {
            var a=NativeDevActions.Find(id);var state=NativeDevActions.Resolve(a,devCtx,out string reason);
            if(DevToggle(r,label??a?.Label??id,value,state,reason,a?.Key)){DevInvoke(id);return true;}return false;
        }
        /// <summary>A labelled slider (drag on its track); `changed` when the value moved this event.</summary>
        private float DevSlider(Rect r,string label,float value,float min,float max,int decimals,string unit,NativeDevState state,string reason,out bool changed)
        {
            changed=false;int id=9000+devWid++;bool live=state==NativeDevState.Live;var track=new Rect(r.x,r.y+22,r.width,8);var hit=new Rect(r.x-4,r.y+14,r.width+8,22);var e=Event.current;
            if(devBodyHot&&DevVisible(r)&&hit.Contains(devMouse)){devHoverLabel=label;devHoverReason=reason;}
            if(live&&devBodyHot&&DevVisible(r)&&e.type==EventType.MouseDown&&e.button==0&&hit.Contains(devMouse)){devDrag=id;e.Use();}
            if(devDrag==id)
            {
                if(e.rawType==EventType.MouseUp||!Input.GetMouseButton(0)||!devCursorFree)devDrag=-1;
                float v=min+(max-min)*Mathf.Clamp01((devMouse.x-track.x)/Mathf.Max(1,track.width));if(decimals==0)v=Mathf.Round(v);if(v!=value){value=v;changed=true;}if(e.type==EventType.MouseDrag)e.Use();
            }
            if(e.type==EventType.Repaint&&DevVisible(r))
            {
                U.Text(new Rect(r.x,r.y,r.width,18),label,12,live?C.Ink():C.Ink(.5f),TextAnchor.MiddleLeft);
                float vw=U.Measure(NativeDevText.Value(value,decimals,unit),11,true,true).x;
                U.Text(new Rect(r.xMax-vw-4,r.y,vw+4,18),NativeDevText.Value(value,decimals,unit),11,live?C.CyanSoft():C.Dim(),TextAnchor.MiddleRight,bold:true,mono:true);
                if(!live){string badge=NativeDevActions.Badge(state);float bw=U.Measure(badge,8,true,true,.08f).x+10;var b=new Rect(r.xMax-vw-16-bw,r.y+2,bw,14);Color t=state==NativeDevState.SandboxOnly||state==NativeDevState.Unavailable?C.Dim():C.Warn();H.Tag(b,t,.9f,false);U.Text(b,badge,8,t,TextAnchor.MiddleCenter,bold:true,mono:true);}
                float k=Mathf.Clamp01((value-min)/Mathf.Max(1e-5f,max-min));H.Line(track,k,live?C.Cyan():C.Dim(),C.Edge(.7f));
                var thumb=new Rect(track.x+track.width*k-5,track.y-4,10,16);H.Cut(thumb,live?(devDrag==id?C.CyanSoft():C.Ink()):C.Dim(),2,H.Corners.All);
            }
            return value;
        }
        /// <summary>A segmented choice in rows of `columns` (0: as many as fit at 92 wide); returns the chosen index and moves y.</summary>
        private int DevChoice(float x,ref float y,float w,string[] labels,int index,NativeDevState state,string reason,int columns=0,string hover=null)
        {
            int n=labels.Length;if(n==0)return index;if(columns<=0)columns=Mathf.Max(1,Mathf.Min(n,Mathf.FloorToInt((w+6)/98)));float gap=6,cw=(w-gap*(columns-1))/columns;bool live=state==NativeDevState.Live;
            for(int i=0;i<n;i++)
            {
                int c=i%columns;var r=new Rect(x+c*(cw+gap),y,cw,26);bool on=i==index;int id=9000+devWid++;float h=devBodyHot&&DevVisible(r)?U.HoverAmount(id,r):0;if(h>.5f&&hover!=null){devHoverLabel=hover;devHoverReason=reason;}
                if(Event.current.type==EventType.Repaint&&DevVisible(r))
                {
                    H.Cut(r,on?C.Cyan(live?.22f:.1f):U.Mix(C.Indigo(.7f),C.Indigo(),h),4,H.Corners.Diagonal);H.Outline(r,on?C.Cyan(live?.8f:.3f):C.Edge(.6f+.4f*h),4);
                    U.Text(new Rect(r.x+4,r.y,r.width-8,r.height),labels[i],10,on?C.Ink(live?1:.6f):C.Muted(live?1:.6f),TextAnchor.MiddleCenter,bold:on,mono:true,tracking:.04f);
                }
                if(live&&DevClick(r))index=i;
                if(c==columns-1||i==n-1)y+=32;
            }
            return index;
        }
        private static int DevIndex(string[] values,string value){if(values==null)return 0;for(int i=0;i<values.Length;i++)if(values[i]==value)return i;return 0;}
        private void DevLists()
        {
            if(devWeaponIds!=null)return;var ids=new List<string>();var names=new List<string>();foreach(var w in content.weapons)if(AllowedWeapon(w)){ids.Add(w.id);names.Add(w.name.ToUpperInvariant());}devWeaponIds=ids.ToArray();devWeaponNames=names.ToArray();
            ids.Clear();names.Clear();foreach(var m in content.maps){if(!NativeMapRules.ArenaSelectable(m))continue;ids.Add(m.id);names.Add(m.name.ToUpperInvariant());}devMapIds=ids.ToArray();devMapNames=names.ToArray();
            if(previewRarities==null){var tiers=new List<RarityData>(content.rarities);tiers.Sort((a,b)=>NativeDevChips.Rank(a.id).CompareTo(NativeDevChips.Rank(b.id)));previewRarities=new string[tiers.Count];previewRarityNames=new string[tiers.Count];for(int i=0;i<tiers.Count;i++){previewRarities[i]=tiers[i].id;previewRarityNames[i]=tiers[i].name;}}
            devRarityColor.Clear();devRarityName.Clear();foreach(var r in content.rarities){devRarityColor[r.id]=ColorUtility.TryParseHtmlString(r.color,out var col)?col:C.Ink();devRarityName[r.id]=r.name.ToUpperInvariant();}
        }

        // ---- Tabs -------------------------------------------------------------------------------------------------------
        private float DevPilotTab(float x,float y,float w)
        {
            DevLists();var me=DevLocal();
            y=DevHead(x,y,w,"MY PILOT");
            if(me!=null&&Event.current.type==EventType.Repaint&&DevVisible(new Rect(x,y,w,44)))
            {
                var card=new Rect(x,y,w,44);H.Cut(card,C.Indigo(.7f),6,H.Corners.Diagonal);NativeIcons.Draw(new Rect(card.x+12,card.y+12,20,20),NativeIcons.Spec(me.Seat.spec),C.CyanSoft());
                U.Text(new Rect(card.x+42,card.y+4,w-160,20),me.Seat.name,13,C.Ink(),TextAnchor.MiddleLeft,bold:true);U.Text(new Rect(card.x+42,card.y+22,w-160,16),me.Weapon.name,11,C.Muted(),TextAnchor.MiddleLeft);
                float max=Mathf.Max(1,(float)me.Stats["hp"]);U.Text(new Rect(card.xMax-120,card.y+4,108,20),NativeDevText.Count(Mathf.CeilToInt(Mathf.Max(0,me.Hp)),Mathf.RoundToInt(max)),13,me.Hp>0?C.Good():C.Danger(),TextAnchor.MiddleRight,bold:true,mono:true);
                H.Bar(new Rect(card.xMax-120,card.y+28,108,5),Mathf.Max(0,me.Hp),max,10,C.Cyan(),C.Edge(.6f));
            }
            y+=52;
            // What the authority says about me (replicated: devGod / devInfiniteAmmo / devNoCooldowns, devRevealed, devScale, devCommandsRan).
            if(me!=null){float px=x;px=DevPill(px,y,"GOD",me.DevGod,C.Good());px=DevPill(px,y,"INF AMMO",me.DevInfiniteAmmo,C.Good());px=DevPill(px,y,"NO CD",me.DevNoCooldowns,C.Good());px=DevPill(px,y,"REVEALED",me.Seat.devRevealed,C.Magenta());px=DevPill(px,y,me.Seat.devScale<1?"SHRUNK":me.Seat.devScale>1?"GROWN":"SCALED",me.Seat.devScale!=1,C.Violet());y+=26;
                if(me.Seat.devCommandsRan)y=DevNote(x,y,w,"No progression this match: DEV commands ran for your account (NativeMatch.CanEarnProgression).",C.Magenta());}
            y=DevActs(x,y,w,2,DevPilotActs);
            if(me!=null&&me.Seat.devRevealed)y=DevNote(x,y,w,"Revealed for this match: everyone sees your DEV tag.",C.Magenta());
            y=DevHead(x,y,w,"DEV COMMANDS");
            foreach(var id in DevPersonalToggles)
            {
                var a=NativeDevActions.Find(id);var state=NativeDevActions.Resolve(a,devCtx,out string reason);float half=(w-8)/2;var me2=me;
                U.Text(new Rect(x,y,w-half*.9f,32),a.Label,12,state==NativeDevState.Live?C.Ink():C.Ink(.55f),TextAnchor.MiddleLeft);
                var on=new Rect(x+w-half*.9f,y,half*.45f-4,32);var off=new Rect(on.xMax+8,y,half*.45f-4,32);
                bool now=me2!=null&&(id=="me-god"?me2.DevGod:id=="me-ammo"?me2.DevInfiniteAmmo:me2.DevNoCooldowns);
                if(DevButton(on,"ON",state,reason,now))DevSend(id,"on");if(DevButton(off,"OFF",state,reason,!now))DevSend(id,"off");y+=40;
            }
            y=DevHead(x,y,w,"WEAPON");
            {var a=NativeDevActions.Find("me-weapon");var state=NativeDevActions.Resolve(a,devCtx,out string reason);int k=DevChoice(x,ref y,w,devWeaponNames,DevIndex(devWeaponIds,devWeapon),NativeDevState.Live,reason,4);devWeapon=devWeaponIds.Length>0?devWeaponIds[k]:devWeapon;}
            DevAct(new Rect(x,y,w,32),"me-weapon",devMode==NativeDevMode.Sandbox?"Swap my weapon (resets the arena)":null);y+=40;
            y=DevHead(x,y,w,"CHIPS");
            if(DevButton(new Rect(x,y,w,32),"Open my chips",NativeDevState.Live,"The Chips tab, targeting you")){devChipSelf=true;devTab=NativeDevTab.Chips;}y+=40;
            return y;
        }
        private static readonly string[] DevPersonalToggles={"me-god","me-ammo","me-cooldowns"};
        private static readonly string[] DevPilotActs={"me-restore","me-down","me-hit","me-summons","me-teleport","me-reveal"},DevInteractActs={"launch","freeze","bring","send","swap-weapon","debris-rain","shrink","grow","hazard-at","spawn-dummies-around","target-debris","target-restore","target-down"};
        private static readonly string[] DevDummyActs={"dummy-restore","dummy-down","dummy-hit","dummy-summons"},DevDummyCommands={"spawn-dummy","clear-dummies"},DevArenaActs={"restore-all","arena-reset","test-block","round-end"};
        private static readonly string[] DevFxActs={"fx-collapse","fx-bursts","fx-clear"},DevVentActs={"me-vent","dummy-vent"},DevPreviewActs={"preview-draft","preview-knockout","preview-death","preview-rarity"};
        private float DevInteractTab(float x,float y,float w)
        {
            DevLists();
            y=DevNote(x,y,w,"Match-only fun with the room: every action ends with the match and never reduces anyone's progression, purchases or rewards. Shrink and grow are looks only; hit volumes keep their size.",C.Muted());
            y=DevHead(x,y,w,"TARGET");y=DevTargetPicker(x,y,w);
            y=DevHead(x,y,w,"PARAMETERS");NativeDevActions.Resolve(NativeDevActions.Find("launch"),devCtx,out string why);
            devLaunchForce=DevSlider(new Rect(x,y,w,34),"Launch speed (up)",devLaunchForce,NativeDevContract.LaunchMin,NativeDevContract.LaunchMax,0," m/s",NativeDevState.Live,why,out _);y+=42;
            devFreezeSeconds=DevSlider(new Rect(x,y,w,34),"Freeze seconds",devFreezeSeconds,0,NativeDevContract.FreezeMax,1," s",NativeDevState.Live,why,out _);y+=42;
            devAroundCount=Mathf.RoundToInt(DevSlider(new Rect(x,y,w,34),"Dummies around",devAroundCount,NativeDevContract.AroundMin,NativeDevContract.AroundMax,0,"",NativeDevState.Live,why,out _));y+=42;
            devRainMaterial=DebrisMaterials[DevChoice(x,ref y,w,DebrisMaterials,DevIndex(DebrisMaterials,devRainMaterial),NativeDevState.Live,why,3,"Debris rain material")];
            devTargetWeapon=devWeaponIds.Length>0?devWeaponIds[DevChoice(x,ref y,w,devWeaponNames,DevIndex(devWeaponIds,devTargetWeapon),NativeDevState.Live,why,4,"Swap weapon to")]:devTargetWeapon;
            y=DevHead(x,y,w,"PLAY WITH THEM");
            y=DevActs(x,y,w,2,DevInteractActs);
            return y;
        }
        // Every connected pilot and bot (DEV DUMMY labelled); the Interact and Monsters tabs share the pick.
        private float DevTargetPicker(float x,float y,float w)
        {
            if(DevPilot(devTarget)==null){foreach(var p in arena.Pilots)if(p.Seat.connected&&p.Seat.id!=arena.LocalId&&!p.Seat.AuthorityBoss){devTarget=p.Seat.id;break;}}
            foreach(var p in arena.Pilots)
            {
                if(!p.Seat.connected||p.Seat.AuthorityBoss)continue;var r=new Rect(x,y,w,30);bool on=p.Seat.id==devTarget;int id=9000+devWid++;float h=devBodyHot&&DevVisible(r)?U.HoverAmount(id,r):0;
                if(Event.current.type==EventType.Repaint&&DevVisible(r))
                {
                    H.Cut(r,on?C.Cyan(.18f):U.Mix(C.Indigo(.55f),C.Indigo(.95f),h),5,H.Corners.Diagonal);if(on)H.Outline(r,C.Cyan(.7f),5);
                    U.Fill(new Rect(r.x+4,r.y+6,2,r.height-12),match.Teams?TeamColor(p.Seat.team):SeatColor(p.Seat));NativeIcons.Draw(new Rect(r.x+14,r.y+7,16,16),p.Seat.bot?"bot":NativeIcons.Spec(p.Seat.spec),on?C.Cyan():C.Muted());
                    U.Text(new Rect(r.x+38,r.y,w-170,r.height),p.Seat.name,12,C.Ink(),TextAnchor.MiddleLeft,bold:on);
                    U.Text(new Rect(r.xMax-130,r.y,120,r.height),p.Seat.id==arena.LocalId?"YOU":p.Seat.devDummy?NativeDevTags.DevDummy:p.Seat.bot?"BOT":"PILOT",9,p.Seat.id==arena.LocalId?C.CyanSoft():p.Seat.devDummy?C.Magenta():C.Muted(),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.1f);
                    U.Text(new Rect(r.xMax-210,r.y,70,r.height),p.Hp>0?NativeDevText.Int(Mathf.CeilToInt(p.Hp)):"OUT",10,p.Hp>0?C.Good():C.Danger(),TextAnchor.MiddleRight,mono:true);
                }
                if(DevClick(r))devTarget=p.Seat.id;y+=34;
            }
            return y;
        }
        private float DevDummiesTab(float x,float y,float w)
        {
            DevLists();var cast=NativeDevActions.Resolve(NativeDevActions.Find("dummy-cast"),devCtx,out string why);
            y=DevHead(x,y,w,"DUMMY WEAPON");{int k=DevChoice(x,ref y,w,devWeaponNames,DevIndex(devWeaponIds,dummySpec),NativeDevState.Live,"The sandbox dummy's cast and the spawned dummy's weapon",4,"Dummy weapon");if(devWeaponIds.Length>0)dummySpec=devWeaponIds[k];}
            y=DevHead(x,y,w,"DUMMY INPUT");dummyKey=DummyInputs[DevChoice(x,ref y,w,DummyInputs,DevIndex(DummyInputs,dummyKey),cast,why,6,"Dummy input")];
            DevAct(new Rect(x,y,w,34),"dummy-cast");y+=42;
            y=DevHead(x,y,w,"DUMMY ACTIONS");y=DevActs(x,y,w,2,DevDummyActs);
            y=DevHead(x,y,w,"BEHAVIOUR");
            var sr=NativeDevActions.Resolve(NativeDevActions.Find("dummy-respawn"),devCtx,out string rw);float shown=devMode==NativeDevMode.Sandbox?arena.DummyRespawn:devRespawn;
            float v=DevSlider(new Rect(x,y,w,34),"Dummy respawn seconds (0 disables)",shown,0,30,0," s",sr,rw,out bool moved);if(moved){devRespawn=v;DevInvoke("dummy-respawn",true);}y+=42;
            shown=devMode==NativeDevMode.Sandbox?arena.SandboxRepeat:devRepeat;v=DevSlider(new Rect(x,y,w,34),"Repeat cast seconds (0 disables)",shown,0,60,0," s",sr,rw,out moved);if(moved){devRepeat=v;DevInvoke("dummy-repeat",true);}y+=42;
            {var dr=NativeDevActions.Resolve(NativeDevActions.Find("dummy-difficulty"),devCtx,out string dw);if(Event.current.type==EventType.Repaint&&DevVisible(new Rect(x,y,w,18)))U.Text(new Rect(x,y,w,18),"Bot difficulty",12,dr==NativeDevState.Live?C.Ink():C.Ink(.5f),TextAnchor.MiddleLeft);y+=22;int k=DevChoice(x,ref y,w,DevDifficulties,DevIndex(DevDifficulties,devDifficulty),dr,dw,4,"Bot difficulty");if(DevDifficulties[k]!=devDifficulty){devDifficulty=DevDifficulties[k];DevInvoke("dummy-difficulty");}}
            y=DevHead(x,y,w,"SPAWN (DEV COMMAND)");
            {var sp=NativeDevActions.Resolve(NativeDevActions.Find("spawn-dummy"),devCtx,out string sw);devSpawnInput=NativeDevContract.SpawnInputs[DevChoice(x,ref y,w,NativeDevContract.SpawnInputs,DevIndex(NativeDevContract.SpawnInputs,devSpawnInput),sp,sw,7,"Spawned dummy input")];
             float team=DevSlider(new Rect(x,y,w,34),"Spawned dummy team",DevSpawnTeam(),0,NativeDevContract.TeamMax,0,"",sp,sw,out bool teamMoved);if(teamMoved)devSpawnTeam=Mathf.RoundToInt(team);y+=42;
             y=DevNote(x,y,w,"Spawns with the dummy weapon above. Clear removes only DEV-created dummies; eight pilot slots include dummies.",C.Muted());}
            y=DevActs(x,y,w,2,DevDummyCommands);
            return y;
        }
        // The Chips tab's fixed toolbar: target, search, sort, filter; returns the grid's top.
        private float DevChipsToolbar(Rect body)
        {
            DevLists();float x=body.x+16,w=body.width-32,y=body.y+12;devBodyHot=devCursorFree&&body.Contains(devMouse);devBody=body;
            var target=devChipSelf?DevLocal():DevDummy();devChipSeat=target?.Seat.id??0;
            int side=DevChoice(x,ref y,w*.5f-4,DevSelfDummy,devChipSelf?0:1,NativeDevState.Live,"The chips' target",2,"Target");y-=32;devChipSelf=side==0;
            DevAct(new Rect(x+w*.5f+4,y,w*.5f-4,26),"chip-cycle");y+=32;
            // Search: a text field in the canvas space (GUI.matrix is the canvas scale here), the kit frame around it.
            devSearchRect=new Rect(x,y,w*.5f-4,26);H.Cut(devSearchRect,C.Void(.8f),4,H.Corners.Diagonal);H.Outline(devSearchRect,devSearchFocus?C.Cyan(.8f):C.Edge(),4);
            if(devSearchStyle==null){devSearchStyle=new GUIStyle(GUI.skin.textField){fontSize=12,alignment=TextAnchor.MiddleLeft,font=U.MonoFont,clipping=TextClipping.Clip,wordWrap=false};devSearchStyle.normal.background=devSearchStyle.focused.background=devSearchStyle.hover.background=devSearchStyle.active.background=null;devSearchStyle.normal.textColor=devSearchStyle.focused.textColor=devSearchStyle.hover.textColor=C.Ink();devSearchStyle.padding=new RectOffset(26,8,4,4);}
            NativeIcons.Draw(new Rect(devSearchRect.x+7,devSearchRect.y+6,14,14),"scan",C.Muted());
            if(devCursorFree)
            {
                if(Event.current.type==EventType.MouseDown&&devSearchRect.Contains(devMouse))devSearchFocus=true;
                GUI.SetNextControlName("dev-chip-search");string typed=GUI.TextField(devSearchRect,devChips.Search??"",48,devSearchStyle);if(!ReferenceEquals(typed,devChips.Search)&&typed!=devChips.Search)devChips.Search=typed;
            }
            else U.Text(new Rect(devSearchRect.x+26,devSearchRect.y,devSearchRect.width-30,26),devChips.Search,12,C.Ink(),TextAnchor.MiddleLeft,mono:true);
            if(string.IsNullOrEmpty(devChips.Search))U.Text(new Rect(devSearchRect.x+26,devSearchRect.y,devSearchRect.width-30,26),"SEARCH CHIPS",10,C.Dim(),TextAnchor.MiddleLeft,mono:true,tracking:.1f);
            float sy=y;devChips.Sort=DevChoice(x+w*.5f+4,ref sy,w*.5f-4,NativeDevChips.SortNames,devChips.Sort,NativeDevState.Live,"Sort",3,"Sort");y+=32;
            devChips.Filter=DevChoice(x,ref y,w,NativeDevChips.FilterNames,devChips.Filter,NativeDevState.Live,"Filter",4,"Show");
            U.Fill(new Rect(body.x+12,y,body.width-24,H.Px),C.Edge(.9f));return y+4;
        }
        private float DevChipsGrid(float x,float y,float w)
        {
            var target=DevPilot(devChipSeat);if(target==null)return DevNote(x,y,w,devChipSelf?"No pilot to install chips on.":"No dummy in this match.",C.Muted());
            int signature=target.Seat.id*7919;foreach(var kv in target.Seat.chips)signature=signature*31+kv.Value+kv.Key.Length;
            devChipTarget=target;devChips.Update(content.chips,devInstalledOf??=DevInstalled,signature);
            int columns=Mathf.Max(1,Mathf.FloorToInt((w+8)/220));float gap=8,cw=(w-gap*(columns-1))/columns,ch=72;if(Mathf.Abs(cw-devChipFitWidth)>.5f){devChipFit.Clear();devChipFitWidth=cw;}
            var add=NativeDevActions.Resolve(NativeDevActions.Find("chip-add"),devCtx,out string addWhy);var cast=NativeDevActions.Resolve(NativeDevActions.Find("chip-cast"),devCtx,out string castWhy);
            var shown=devChips.Shown;if(shown.Count==0)return DevNote(x,y,w,"No chips match.",C.Muted());
            for(int i=0;i<shown.Count;i++)
            {
                int col=i%columns;var r=new Rect(x+col*(cw+gap),y,cw,ch);
                if(DevVisible(r))DevChipCard(r,content.chips[shown[i]],target,add,addWhy,cast,castWhy);
                if(col==columns-1||i==shown.Count-1)y+=ch+gap;
            }
            return y;
        }
        private NativePilot devChipTarget;private Func<string,int> devInstalledOf;
        private int DevInstalled(string id)=>devChipTarget!=null&&devChipTarget.Seat.chips.TryGetValue(id,out int n)?n:0;
        private void DevChipCard(Rect r,ContentChip chip,NativePilot target,NativeDevState add,string addWhy,NativeDevState cast,string castWhy)
        {
            target.Seat.chips.TryGetValue(chip.id,out int n);Color tone=devRarityColor.TryGetValue(chip.rarity??"",out var rc)?rc:C.Ink();int id=9000+devWid++;float h=devBodyHot?U.HoverAmount(id,r):0;
            if(h>.5f){devHoverLabel=chip.name;devHoverReason=chip.description;}
            if(Event.current.type==EventType.Repaint)
            {
                H.Cut(r,U.Mix(C.Indigo(.75f),C.Indigo(),h),6,H.Corners.Diagonal);H.Outline(r,U.A(tone,n>0?.75f:.25f+.3f*h),6);U.Fill(new Rect(r.x,r.y+6,3,r.height-12),U.A(tone,n>0?1:.5f));
                NativeIcons.Draw(new Rect(r.x+12,r.y+10,20,20),NativeIcons.Chip(chip.id),tone);
                if(!devChipFit.TryGetValue(chip.id,out var name)){name=DevFit(chip.name,12,r.width-108);devChipFit[chip.id]=name;}
                U.Text(new Rect(r.x+40,r.y+6,r.width-100,18),name,12,C.Ink(),TextAnchor.MiddleLeft,bold:true);
                U.Text(new Rect(r.x+40,r.y+22,r.width-100,14),devRarityName.TryGetValue(chip.rarity??"",out var rn)?rn:"",8,U.A(tone,.9f),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
                U.Text(new Rect(r.xMax-62,r.y+6,50,18),NativeDevText.Count(n,chip.max),12,n>0?C.Ink():C.Dim(),TextAnchor.MiddleRight,bold:true,mono:true);
                if(devChips.Favourites.Contains(chip.id))NativeIcons.Draw(new Rect(r.xMax-62-18,r.y+8,13,13),"sparkles",C.Warn());
            }
            // The row: − + MAX CLR ★ CAST.
            float bx=r.x+10,by=r.yMax-28,bw=(r.width-20-5*4)/6;
            if(DevMini(new Rect(bx,by,bw,22),"−",add,addWhy,"Remove one"))DevChip(chip.id,target,-1,1);bx+=bw+4;
            if(DevMini(new Rect(bx,by,bw,22),"+",add,addWhy,"Add one"))DevChip(chip.id,target,1,1);bx+=bw+4;
            if(DevMini(new Rect(bx,by,bw,22),"MAX",add,addWhy,"Stack to the max"))DevChip(chip.id,target,1,Mathf.Min(NativeDevContract.ChipCountMax,Mathf.Max(0,chip.max-n)));bx+=bw+4;
            if(DevMini(new Rect(bx,by,bw,22),"CLR",add,addWhy,"Remove the stack"))DevChip(chip.id,target,-1,Mathf.Min(NativeDevContract.ChipCountMax,n));bx+=bw+4;
            if(DevMini(new Rect(bx,by,bw,22),devChips.Favourites.Contains(chip.id)?"★":"☆",NativeDevState.Live,"Favourite (saved)","Favourite")){devChips.Favourite(chip.id);prefs.devChipFavourites=devChips.FavouritesText();Save();}bx+=bw+4;
            if(DevMini(new Rect(bx,by,bw,22),"CAST",cast,castWhy,"Dummy casts with this chip")){devChipId=chip.id;DevInvoke("chip-cast");devChips.Touch(chip.id);}
        }
        private bool DevMini(Rect r,string label,NativeDevState state,string reason,string hover)
        {
            int id=9000+devWid++;bool live=state==NativeDevState.Live;float h=devBodyHot&&DevVisible(r)?U.HoverAmount(id,r):0;if(h>.5f){devHoverLabel=hover;devHoverReason=reason;}
            if(Event.current.type==EventType.Repaint&&DevVisible(r)){H.Cut(r,U.Mix(C.Navy(.9f),C.Indigo(),h),3,H.Corners.BR);H.Outline(r,live?C.Cyan(.3f+.5f*h):state==NativeDevState.SandboxOnly||state==NativeDevState.Unavailable?C.Edge(.6f):C.Warn(.4f),3,H.Corners.BR);U.Text(r,label,10,live?C.Ink():C.Ink(.4f),TextAnchor.MiddleCenter,bold:true,mono:true);}
            return DevClick(r);
        }
        // `count` steps of one chip (the sandbox call per step, as the old +/− did; a DEV command carries the count).
        private void DevChip(string id,NativePilot target,int sign,int count)
        {
            if(count<=0)return;devChipId=id;devChipSeat=target.Seat.id;devChips.Touch(id);var a=NativeDevActions.Find(sign>0?"chip-add":"chip-remove");
            if(NativeDevActions.RunsInSandbox(a,DevContext())){for(int i=0;i<count&&i<64;i++)DevInvoke(a.Id,true);DevSay(a.Label+" · "+DevRan,false);return;}
            DevSend(a.Id,id,NativeDevText.Int(count));
        }
        private static string DevFit(string text,float size,float width)
        {
            text=text??"";if(U.Measure(text,size,true).x<=width)return text;while(text.Length>1&&U.Measure(text+"…",size,true).x>width)text=text.Substring(0,text.Length-1);return text.TrimEnd()+"…";
        }
        private float DevArenaTab(float x,float y,float w)
        {
            DevLists();
            y=DevHead(x,y,w,"ARENA");y=DevActs(x,y,w,2,DevArenaActs);
            y=DevHead(x,y,w,"MAP");{var mr=NativeDevActions.Resolve(NativeDevActions.Find("load-map"),devCtx,out string mw);int k=DevChoice(x,ref y,w,devMapNames,DevIndex(devMapIds,devMap),mr,mw,3,"Map");if(devMapIds.Length>0)devMap=devMapIds[k];}
            DevAct(new Rect(x,y,w,32),"load-map","Load map (resets the arena)");y+=40;
            y=DevHead(x,y,w,"DEBRIS LAUNCHER");
            {
                var dr=NativeDevActions.Resolve(NativeDevActions.Find("debris"),devCtx,out string dw);
                // The sandbox's launcher keeps its 150 kg; a DEV command takes up to 500 kg (NativeDevRules.Validate).
                if(devMode==NativeDevMode.Sandbox)debrisMass=DevSlider(new Rect(x,y,w,34),"Mass (kg)",debrisMass,1,150,0," kg",dr,dw,out _);
                else debrisMass=DevSlider(new Rect(x,y,w,34),"Mass (kg)",debrisMass,NativeDevContract.DebrisMassMin,NativeDevContract.DebrisMassMax,0," kg",dr,dw,out _);y+=42;
                debrisSpeed=DevSlider(new Rect(x,y,w,34),"Speed (m/s)",debrisSpeed,1,100,0," m/s",dr,dw,out _);y+=42;
                debrisMaterial=DebrisMaterials[DevChoice(x,ref y,w,DebrisMaterials,DevIndex(DebrisMaterials,debrisMaterial),dr,dw,3,"Material")];
                debrisImpact=DebrisImpacts[DevChoice(x,ref y,w,DebrisImpacts,DevIndex(DebrisImpacts,debrisImpact),dr,dw,3,"Impact")];
                DevAct(new Rect(x,y,w,34),"debris",devMode==NativeDevMode.Sandbox?null:"Launch at the Interact target");y+=42;
            }
            if(presentation?.Structures!=null)
            {
                y=DevHead(x,y,w,"DESTRUCTION EFFECTS PREVIEW");var fr=NativeDevActions.Resolve(NativeDevActions.Find("fx-collapse"),devCtx,out string fw);
                structureMaterial=StructureMaterials[DevChoice(x,ref y,w,StructureMaterials,DevIndex(StructureMaterials,structureMaterial),fr,fw,5,"Material")];
                structureSpot=StructureSpots[DevChoice(x,ref y,w,StructureSpots,DevIndex(StructureSpots,structureSpot),fr,fw,3,"Where")];
                y=DevActs(x,y,w,3,DevFxActs);
            }
            return y;
        }
        private float DevHazardsTab(float x,float y,float w)
        {
            y=DevHead(x,y,w,"OVERLAY");DevToggleAct(new Rect(x,y,w,32),"overlay-hazards",devOverlayHazards,"Show hazard boxes");y+=40;
            y=DevHead(x,y,w,"MAP HAZARDS");var hazards=arena.Map.hazards;
            if(hazards==null||hazards.Length==0)y=DevNote(x,y,w,"This map has no authored hazards.",C.Muted());
            else
            {
                var tr=NativeDevActions.Resolve(NativeDevActions.Find("hazard-trigger"),devCtx,out string tw);
                for(int i=0;i<hazards.Length;i++)
                {
                    var r=new Rect(x,y,w,30);if(Event.current.type==EventType.Repaint&&DevVisible(r)){H.Cut(r,C.Indigo(.55f),5,H.Corners.Diagonal);U.Text(new Rect(r.x+12,r.y,w*.5f,r.height),hazards[i].id,11,C.Ink(),TextAnchor.MiddleLeft,mono:true);U.Text(new Rect(r.x+w*.5f,r.y,w*.5f-120,r.height),hazards[i].kind,9,C.Muted(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);}
                    if(DevButton(new Rect(r.xMax-108,r.y+2,104,26),"Trigger",tr,tw)){devHazard=i;DevInvoke("hazard-trigger");}y+=34;
                }
            }
            y=DevHead(x,y,w,"SIREN VENTS");
            if(arena.MapId!="siren")y=DevNote(x,y,w,"The vent dive runs on the Siren map.",C.Muted());
            y=DevActs(x,y,w,2,DevVentActs);
            {var sr=NativeDevActions.Resolve(NativeDevActions.Find("splat-delay"),devCtx,out string sw);float v=DevSlider(new Rect(x,y,w,34),"Vent fall splat delay",devMode==NativeDevMode.Sandbox?arena.SandboxSplatDelay:devSplat,1,30,0," s",sr,sw,out bool moved);if(moved){devSplat=v;DevInvoke("splat-delay",true);}y+=42;}
            return y;
        }
        private float DevPreviewsTab(float x,float y,float w)
        {
            DevLists();
            y=DevHead(x,y,w,"DRAFT AND DEATH");y=DevActs(x,y,w,2,DevPreviewActs);
            {var pr=NativeDevActions.Resolve(NativeDevActions.Find("preview-rarity"),devCtx,out string pw);int k=DevChoice(x,ref y,w,previewRarityNames,DevIndex(previewRarities,previewRarity),pr,pw,5,"Preview rarity");previewRarity=previewRarities[k];}
            y=DevHead(x,y,w,"GALLERIES");
            for(int i=0;i<DevGalleries.Length;i++){bool on=DevGalleryOn(i);DevToggleAct(new Rect(x,y,w,32),DevGalleries[i],on,on?DevGalleryHide[i]:DevGalleryShow[i]);y+=38;}
            y=DevNote(x,y,w,"Cyber Lord: 5 switches mode; LMB and 1-4 use the active kit.",C.Muted());
            y=DevNote(x,y,w,"Cyber Kit 2 (ranged): as a Cyber Lord press 5 for ranged; the preview then loops Plasma Byte, Daisy Chain, Singularity, Packet Storm and Orbital Uplink on your pilot, locally. Move while it plays.",C.Muted());
            y=DevHead(x,y,w,"DEV REVEAL");DevAct(new Rect(x,y,w,32),"preview-reveal");y+=40;
            y=DevNote(x,y,w,"Plays the \"a developer is here\" moment on your pilot, locally. In a match it plays for the whole room once the host reveals you (DEV command reveal).",C.Muted());
            return y;
        }
        private float DevCameraTab(float x,float y,float w)
        {
            y=DevHead(x,y,w,"FLY CAMERA");DevToggleAct(new Rect(x,y,w,32),"fly",devFly.On,"Detached fly camera");y+=40;
            devFly.Speed=DevSlider(new Rect(x,y,w,34),"Fly speed",devFly.Speed,NativeDevFly.MinSpeed,NativeDevFly.MaxSpeed,0," m/s",NativeDevState.Live,NativeDevActions.Local,out _);y+=42;
            DevAct(new Rect(x,y,w,32),"fly-snap");y+=40;y=DevNote(x,y,w,NativeDevKeys.FlyHelp,C.Muted());
            y=DevHead(x,y,w,"DEBUG OVERLAYS (READ-ONLY)");
            DevToggleAct(new Rect(x,y,w,32),"overlay-hit",devOverlayHit);y+=38;DevToggleAct(new Rect(x,y,w,32),"overlay-melee",devOverlayMelee);y+=38;
            if(arena.Replica)y=DevNote(x,y,w,"Melee volumes come from the host's swing state: none on a guest.",C.Muted());
            DevToggleAct(new Rect(x,y,w,32),"overlay-hazards",devOverlayHazards);y+=38;DevToggleAct(new Rect(x,y,w,32),"overlay-nav",devOverlayNav);y+=38;
            DevToggleAct(new Rect(x,y,w,32),"overlay-anchors",devOverlayAnchors);y+=38;DevToggleAct(new Rect(x,y,w,32),"overlay-shots",devOverlayShots);y+=38;
            y=DevHead(x,y,w,"EVENT LOG");DevToggleAct(new Rect(x,y,w,32),"log",devLogOn);y+=40;
            for(int i=0;i<NativeDevLog.Filters.Length;i++)
            {
                int c=i%4;float cw=(w-18)/4;var r=new Rect(x+c*(cw+6),y,cw,26);bool on=(devLogFilter&NativeDevLog.Filters[i])!=0;
                if(DevMini(r,NativeDevLog.FilterNames[i],NativeDevState.Live,NativeDevActions.Local,on?"Hide these events":"Show these events"))devLogFilter^=NativeDevLog.Filters[i];
                if(on&&Event.current.type==EventType.Repaint&&DevVisible(r))U.Fill(new Rect(r.x+3,r.yMax-3,r.width-6,2),C.Cyan());
                if(c==3||i==NativeDevLog.Filters.Length-1)y+=32;
            }
            if(DevToggle(new Rect(x,y,w,32),"Only events involving me",devLogMine,NativeDevState.Live,NativeDevActions.Local))devLogMine=!devLogMine;y+=40;
            y=DevHead(x,y,w,"PERFORMANCE");DevToggleAct(new Rect(x,y,w,32),"graph",devGraphOn);y+=40;
            y=DevHead(x,y,w,"SCREENSHOT");DevToggleAct(new Rect(x,y,w,32),"hide-hud",devHideHud);y+=40;
            return y;
        }
        private float DevSimulationTab(float x,float y,float w)
        {
            y=DevHead(x,y,w,"SPEED");
            {var tr=NativeDevActions.Resolve(NativeDevActions.Find("time-scale"),devCtx,out string tw);float shown=devMode==NativeDevMode.Sandbox?sandboxSpeed:devSpeed;
             float v=DevSlider(new Rect(x,y,w,34),"Simulation speed",shown,NativeDevContract.TimeScaleMin,NativeDevContract.TimeScaleMax,2,"×",tr,tw,out bool moved);y+=42;
             if(moved){devSpeed=v;if(devMode==NativeDevMode.Sandbox)DevInvoke("time-scale",true);}
             if(devMode!=NativeDevMode.Sandbox){DevAct(new Rect(x,y,w,32),"time-scale","Send speed to the room");y+=40;}}
            y=DevStat(x,y,w,"ROOM SPEED (AUTHORITY)",NativeDevText.Value(arena.DevTimeScale,2,"×"));
            y=DevHead(x,y,w,"RULES");DevToggleAct(new Rect(x,y,w,32),"safe-mode",arena.SafeMode,"Safe mode: the player retains 1 HP");y+=40;
            y=DevHead(x,y,w,"STATE (READ-ONLY)");
            y=DevStat(x,y,w,"PHASE",DevPhaseName(match.Phase));y=DevStat(x,y,w,"SET",NativeDevText.Int(match.Set));y=DevStat(x,y,w,"ROUND",NativeDevText.Int(match.Round));
            y=DevStat(x,y,w,"MATCH TIME",NativeDevText.Value(Mathf.Floor(arena.Time),0," s"));y=DevStat(x,y,w,"PILOTS",NativeDevText.Int(arena.Pilots.Count));
            y=DevStat(x,y,w,"PROJECTILES",NativeDevText.Int(arena.Bullets.Count));y=DevStat(x,y,w,"MINIONS",NativeDevText.Int(arena.Minions.Count));
            y=DevStat(x,y,w,"SIM STEP",NativeDevText.Value(devStepGraph.Average(),2," ms"));y=DevStat(x,y,w,"FRAME",NativeDevText.Value(devFrameGraph.Average(),1," ms"));
            if(Online)y=DevStat(x,y,w,"ROUND TRIP",NativeDevText.Value(devRttGraph.Last,0," ms"));
            return y;
        }
        private static readonly string[] DevPhases={"LOBBY","COUNTDOWN","FIGHT","RESULT","DRAFT","MATCH","INTRO"};
        private static string DevPhaseName(NativePhase p){int i=(int)p;return i>=0&&i<DevPhases.Length?DevPhases[i]:"";}
        private float DevStat(float x,float y,float w,string label,string value)
        {
            if(Event.current.type==EventType.Repaint&&DevVisible(new Rect(x,y,w,22))){U.Text(new Rect(x,y,w*.5f,22),label,10,C.Muted(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);U.Text(new Rect(x+w*.5f,y,w*.5f,22),value,12,C.Ink(),TextAnchor.MiddleRight,mono:true);U.Fill(new Rect(x,y+22,w,H.Px),C.Edge(.5f));}
            return y+26;
        }
    }
}
