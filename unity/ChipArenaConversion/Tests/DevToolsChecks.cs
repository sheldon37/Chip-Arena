#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The DEV panel and DEV Abuse (owner 2026-10-14: "Improve sandbox tools- Better UI, more control ... a "DEV Abuse" Toggle in
    // dev settings, this will allow dev options in matches (online included) for only dev accounts"; Core NativeDevTools,
    // Runtime NativeDevPanel / NativeDevOverlays / NativeSandbox DevRun). Against the engine-free rules: the toggle only for a
    // verified dev and off by default; the panel's availability across sandbox, offline match, online host and guest, dev and
    // not; every sandbox action routing to the same NativeArena call with the same arguments and limits as the old list;
    // DEV commands only through Astra's SendDevCommand (docs/DEV-ABUSE-AUTHORITY.md) with the session opt-in in an active round,
    // clamped to NativeDevRules.Validate's limits, nothing reported done before the authority's reply and a timeout shown as
    // unknown; the replicated DEV state drawn, never written; forced hazard pulses from the replicated records; the view
    // tools (overlays, fly camera) read-only and drawn from the authority's own volumes; no per-frame allocation (warmed by the
    // identical pass first); the cyber palette, no GUILayout or LINQ in the panel, the hooks, the hotkeys and the metas.
    internal static class DevToolsChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("DEV tools: "+what);count++;}
        public static void Run(string root)
        {
            rootPath=root;string R(string f)=>f.Length==0?"":File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            Toggle(R);Matrix();Routes(R);Authority(R);ReadOnly(R);Volumes();Reveal();Hazards(root,R);Log();Chips();Layout(root);Allocations();Sources(root,R);
            Console.WriteLine("PASS DEV tools ("+count+" checks: the DEV Abuse toggle only for a verified dev, off by default; the panel in the sandbox and, with DEV Abuse, every offline / host / guest match, never for a non-dev; "+NativeDevActions.All.Count(a=>a.SandboxRoute&&a.Kind!=NativeDevKind.Presentation)+" sandbox routes on the old NativeArena calls; DEV commands only through SendDevCommand with the opt-in in a round, on the authority's limits, nothing done before its reply, a timeout unknown; forced hazard pulses from the replicated records; the proposed monster commands (args, HOST NEEDS MONSTER COMMANDS, adapter off, rift, ward entries); overlays and fly camera read-only on the authority's own volumes; no per-frame allocation; palette, hooks, hotkeys and metas)");
        }
        private static NativeDevContext Ctx(bool inMatch=true,bool sandbox=false,bool online=false,bool hosting=false,bool dev=true,bool abuse=false,bool commands=false,bool fight=true,bool monsters=true)=>NativeDevContext.Of(inMatch,sandbox,online,hosting,dev,abuse,commands,fight,monsters);
        private static void Toggle(Func<string,string> R)
        {
            Check(NativeDevGate.ToggleVisible(true)&&!NativeDevGate.ToggleVisible(false),"the toggle shows only for a verified dev");
            var game=R("Runtime/ConversionGame.cs");Check(Regex.IsMatch(game,@"public bool devAbuse;")&&!Regex.IsMatch(game,@"devAbuse\s*=\s*true"),"DEV Abuse is a saved preference, false unless the dev turns it on");
            var settings=R("Runtime/NativeSettings.cs");int dev=settings.IndexOf("if(account.Developer)",StringComparison.Ordinal),toggle=settings.IndexOf("NativeDevGate.ToggleVisible(account.Developer)",StringComparison.Ordinal),credits=settings.IndexOf("Field(\"Credits\")",StringComparison.Ordinal);
            Check(dev>0&&toggle>dev&&toggle<credits&&settings.Contains("prefs.devAbuse=abuse;DevAbuseEnabled=NativeDevGate.CommandsEnabled(abuse,account.Developer);Save();"),"Settings · Developer shows the toggle inside the verified-dev block, saves it and sets the session opt-in");
            Check(NativeDevGate.CommandsEnabled(true,true)&&!NativeDevGate.CommandsEnabled(true,false)&&!NativeDevGate.CommandsEnabled(false,true),"the session opt-in follows the saved toggle for a verified dev only");
            var net=R("Runtime/NativeDevNetwork.cs");Check(net.Contains("public bool DevAbuseEnabled {get;set;}")&&!Regex.IsMatch(net+R("Runtime/NativeDevPanel.cs"),@"DevAbuseEnabled\s*=\s*true"),"DevAbuseEnabled defaults off and is set only from the toggle");
            Check(R("Runtime/NativeDevPanel.cs").Contains("DevAbuseEnabled=NativeDevGate.CommandsEnabled(prefs!=null&&prefs.devAbuse,account!=null&&account.Developer);"),"the saved toggle applies to the session (default off)");
            Check(R("Runtime/NativeAdmin.cs").Contains("public bool Developer=>Access.Developer&&Access.Expires>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();"),"account.Developer is the verified, unexpired claim");
            Check(!NativeAccess.Guest.Developer&&NativeAccess.Verify("forged.token",s=>new NativePlayerClaim{developer=true,exp=double.MaxValue},0).Developer==false,"an unsigned claim never becomes a developer");
            var panel=R("Runtime/NativeDevPanel.cs");Check(panel.Contains("account!=null&&account.Developer")&&panel.Contains("prefs!=null&&prefs.devAbuse")&&panel.Contains("arena.Sandbox&&!arena.Replica"),"the panel's context reads the verified account, the opt-in and the offline sandbox");
        }
        private static void Matrix()
        {
            // Developer Sandbox: the tools as today, no opt-in.
            Check(NativeDevGate.Mode(Ctx(sandbox:true))==NativeDevMode.Sandbox&&NativeDevGate.Mode(Ctx(sandbox:true,abuse:true))==NativeDevMode.Sandbox,"sandbox: the panel, with or without DEV Abuse");
            Check(NativeDevGate.Mode(Ctx(sandbox:true,dev:false))==NativeDevMode.Off&&NativeDevGate.Mode(Ctx(sandbox:true,dev:false,abuse:true))==NativeDevMode.Off,"sandbox: never for a non-dev");
            Check(NativeDevGate.Mode(Ctx(sandbox:true,online:true,hosting:true))==NativeDevMode.Off&&NativeDevGate.Mode(Ctx(sandbox:true,online:true,abuse:true))==NativeDevMode.Abuse,"a sandbox flag online is not the sandbox");
            foreach(var (online,hosting,name) in new[]{(false,false,"offline match"),(true,true,"online host"),(true,false,"online guest")})
            {
                Check(NativeDevGate.Mode(Ctx(online:online,hosting:hosting,abuse:true))==NativeDevMode.Abuse,name+": DEV Abuse on gives the panel");
                Check(NativeDevGate.Mode(Ctx(online:online,hosting:hosting))==NativeDevMode.Off,name+": off without DEV Abuse");
                Check(NativeDevGate.Mode(Ctx(online:online,hosting:hosting,dev:false,abuse:true))==NativeDevMode.Off,name+": a non-dev with a stale opt-in never gets it");
                Check(NativeDevGate.Indicator(Ctx(online:online,hosting:hosting,abuse:true))&&!NativeDevGate.Indicator(Ctx(online:online,hosting:hosting,dev:false,abuse:true))&&!NativeDevGate.Indicator(Ctx(online:online,hosting:hosting)),name+": the DEV ABUSE ON tag only while it is in effect");
            }
            Check(NativeDevGate.Mode(Ctx(inMatch:false,abuse:true))==NativeDevMode.Off&&NativeDevGate.Mode(Ctx(inMatch:false,sandbox:true))==NativeDevMode.Off,"no panel without a match");
        }
        // The old Sandbox Tools list's calls (base 09096af NativeSandbox.cs SandboxBody / StructureFxRows), now one case each.
        private static readonly (string id,string[] calls)[] OldCalls=
        {
            ("me-restore",new[]{"arena.SandboxAction(\"restore\",true)"}),("me-hit",new[]{"arena.SandboxAction(\"hit\",true)"}),("me-down",new[]{"arena.SandboxAction(\"down\",true)"}),
            ("me-summons",new[]{"arena.SandboxAction(\"summons\",true)"}),("me-vent",new[]{"arena.SandboxAction(\"vent\",true)"}),
            ("dummy-restore",new[]{"arena.SandboxAction(\"restore\")"}),("dummy-hit",new[]{"arena.SandboxAction(\"hit\")"}),("dummy-down",new[]{"arena.SandboxAction(\"down\")"}),
            ("dummy-summons",new[]{"arena.SandboxAction(\"summons\")"}),("dummy-vent",new[]{"arena.SandboxAction(\"vent\")"}),
            ("dummy-cast",new[]{"arena.SandboxCast(dummySpec,dummyKey)"}),
            ("dummy-respawn",new[]{"arena.DummyRespawn=Mathf.Round(Mathf.Clamp(devRespawn,0,30))"}),
            ("dummy-repeat",new[]{"arena.SandboxRepeat=Mathf.Round(Mathf.Clamp(devRepeat,0,60))","if(arena.SandboxRepeat>0)arena.SandboxRepeat=Math.Max(2,arena.SandboxRepeat)"}),
            ("chip-add",new[]{"arena.SandboxChip(devChipSeat,devChipId,1)"}),("chip-remove",new[]{"arena.SandboxChip(devChipSeat,devChipId,-1)"}),
            ("restore-all",new[]{"arena.SandboxAction(\"restore\",true);arena.SandboxAction(\"restore\");"}),("arena-reset",new[]{"arena.SandboxReset()"}),("test-block",new[]{"arena.SandboxTestBlock()"}),
            ("ward-spawn",new[]{"arena.SandboxWardEntry(entries[wardSandboxEntry].e)","arena.Map.look!=\"quarantine\""}),
            ("debris",new[]{"Mathf.Clamp(debrisMass,1,150)","Mathf.Clamp(debrisSpeed,1,100)","arena.SandboxLaunchDebris(debrisMass,debrisSpeed,debrisMaterial,debrisImpact)"}),
            ("fx-collapse",new[]{"PreviewCollapse(kit)"}),("fx-bursts",new[]{"kit.PreviewBursts(arena,spot,Quaternion.Euler(0,yaw,0)*Vector3.right)"}),("fx-clear",new[]{"kit.Clear()"}),
            ("preview-draft",new[]{"arena.SandboxPreview(\"draft\")"}),("preview-rarity",new[]{"arena.SandboxPreview(\"rarity\",previewRarity)"}),("preview-knockout",new[]{"arena.SandboxPreview(\"knockout\")"}),("preview-death",new[]{"arena.SandboxPreview(\"my-death\")"}),
            ("gallery-minions",new[]{"presentation.MinionGallery=!presentation.MinionGallery"}),("gallery-cyber-jump",new[]{"presentation.CyberBoostPreviewOn=!presentation.CyberBoostPreviewOn"}),
            ("gallery-saw",new[]{"presentation.SawSplitGallery=!presentation.SawSplitGallery"}),("gallery-revive",new[]{"presentation.ReviveGallery=!presentation.ReviveGallery"}),("gallery-chip-select",new[]{"presentation.ChipSelectPreview=!presentation.ChipSelectPreview"}),
            ("time-scale",new[]{"SetSandboxTimeScale(devSpeed)","Time.fixedDeltaTime=1f/60"}),("safe-mode",new[]{"arena.SafeMode=!arena.SafeMode"}),
        };
        // Existing sandbox calls the old list did not offer, now on the panel unchanged (more control, same authority gate).
        private static readonly (string id,string call)[] NewCalls=
        {
            ("me-weapon","arena.SandboxConfigure(weapon:devWeapon)"),("dummy-difficulty","arena.SandboxConfigure(difficulty:devDifficulty)"),("load-map","arena.SandboxConfigure(map:devMap)"),
            ("chip-cast","arena.SandboxCastChip(devChipId)"),("chip-cycle","arena.SandboxCastEveryChip()"),("gallery-hazard-kit","presentation.HazardKitGallery=!presentation.HazardKitGallery"),("gallery-hazard-music","presentation.HazardKitMusic=!presentation.HazardKitMusic"),("splat-delay","arena.SandboxSplatDelay=Mathf.Round(Mathf.Clamp(devSplat,1,30))"),("preview-reveal","DevRevealStart(arena.Local.Seat.id,arena.Local.Seat.name,true)"),
            ("gallery-cyber-kit2-ranged","presentation.CyberKit2RangedPreviewOn=!presentation.CyberKit2RangedPreviewOn"),("gallery-cyber-kit2-melee","presentation.CyberKit2MeleePreviewOn=!presentation.CyberKit2MeleePreviewOn"),
        };
        private static Dictionary<string,string> Cases(string sandbox)
        {
            int a=sandbox.IndexOf("private string DevRun(string id)",StringComparison.Ordinal);Check(a>0,"DevRun exists in NativeSandbox.cs");var body=sandbox.Substring(a);
            var cases=new Dictionary<string,string>();foreach(Match m in Regex.Matches(body,"case \"([a-z0-9-]+)\":(.*?)(?=\\n\\s*(case |// |\\}))",RegexOptions.Singleline))cases[m.Groups[1].Value]=m.Groups[2].Value;return cases;
        }
        private static void Routes(Func<string,string> R)
        {
            var sandbox=R("Runtime/NativeSandbox.cs");var cases=Cases(sandbox);
            foreach(var (id,calls) in OldCalls){Check(cases.TryGetValue(id,out var body),"the sandbox route "+id+" exists");foreach(var call in calls)Check(body.Contains(call),id+" calls "+call+" as the old list did");}
            foreach(var (id,call) in NewCalls){Check(cases.TryGetValue(id,out var body)&&body.Contains(call),id+" calls the existing "+call);}
            var routed=NativeDevActions.All.Where(a=>a.SandboxRoute&&a.Kind!=NativeDevKind.Presentation).Select(a=>a.Id).OrderBy(s=>s).ToArray();
            Check(routed.SequenceEqual(cases.Keys.OrderBy(s=>s)),"every action with a sandbox route has exactly one DevRun case and nothing else does ("+string.Join(",",routed.Except(cases.Keys))+" / "+string.Join(",",cases.Keys.Except(routed))+")");
            Check(OldCalls.Length+NewCalls.Length==cases.Count,"the routes are the old list's calls plus the existing sandbox calls only");
            // The old list's sliders and grids kept their limits and defaults; the NativeArena half of NativeSandbox.cs is unchanged.
            Check(sandbox.Contains("debrisMass=8,debrisSpeed=15")&&sandbox.Contains("private string dummySpec=\"pistol\",dummyKey=\"ai\";private float sandboxSpeed=1;")&&sandbox.Contains("DummyInputs={\"ai\",\"primary\",\"e\",\"q\",\"block\",\"dash\",\"jump\",\"1\",\"2\",\"3\",\"4\",\"mode\"}")&&sandbox.Contains("DebrisMaterials={\"wood\",\"stone\",\"metal\"},DebrisImpacts={\"face\",\"edge\",\"corner\"}"),"defaults and choices as the old list");
            Check(sandbox.Contains("private bool SandboxAllowed=>Sandbox&&!Replica&&Local?.Seat.access?.Developer==true&&Local.Seat.access.Expires>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();")&&!sandbox.Contains("GUILayout"),"the authority gate stays; the GUILayout list is gone");
            var panel=R("Runtime/NativeDevPanel.cs");
            Check(panel.Contains("if(NativeDevActions.RunsInSandbox(a,c)){string result=DevRun(id);")&&Regex.Matches(panel,@"DevRun\(").Count==1,"the panel reaches the sandbox calls only through DevInvoke's sandbox check");
            Check(panel.Contains("\"Mass (kg)\",debrisMass,1,150,0")&&panel.Contains("\"Speed (m/s)\",debrisSpeed,1,100,0")&&panel.Contains("\"Dummy respawn seconds (0 disables)\",shown,0,30,0")&&panel.Contains("\"Repeat cast seconds (0 disables)\",shown,0,60,0")&&panel.Contains("\"Simulation speed\",shown,NativeDevContract.TimeScaleMin,NativeDevContract.TimeScaleMax,2"),"the sliders keep the old ranges");
            Check(NativeDevContract.TimeScaleMin==.1f&&NativeDevContract.TimeScaleMax==2&&R("Runtime/NativeSandboxTools.cs").Contains("sandboxSpeed=Mathf.Clamp(scale,.1f,2)"),"the speed range is the sandbox's");
        }
        private static void Authority(Func<string,string> R)
        {
            var off=Ctx(online:true,abuse:true);var idle=Ctx(online:true,abuse:true,commands:true,fight:false);var live=Ctx(online:true,abuse:true,commands:true);var sandbox=Ctx(sandbox:true);var sandboxOn=Ctx(sandbox:true,abuse:true,commands:true);
            foreach(var a in NativeDevActions.All)
            {
                var s=NativeDevActions.Resolve(a,live,out string why);
                if(a.Kind==NativeDevKind.Contract)
                {
                    Check(s==NativeDevState.Live&&why==NativeDevActions.ToHost,a.Id+" goes to the match's DEV authority with the opt-in in a round");
                    Check(NativeDevActions.Resolve(a,off,out why)==NativeDevState.NeedsOptIn&&why==NativeDevActions.OptIn,a.Id+" says to turn on DEV Abuse without the session opt-in");
                    Check(NativeDevActions.Resolve(a,idle,out why)==NativeDevState.NotNow&&why==NativeDevActions.NotNow,a.Id+" waits for an active round (the authority refuses otherwise)");
                    Check(NativeDevContract.Known(a.Contract)||a.Proposed,a.Id+" sends the authority's action "+a.Contract+" (or a proposed monster command)");
                    if(a.Proposed)Check(NativeDevActions.Resolve(a,Ctx(online:true,abuse:true,commands:true,monsters:false),out why)==NativeDevState.NeedsHost&&why==NativeDevActions.NeedsHost&&NativeDevActions.Resolve(a,Ctx(sandbox:true,abuse:true,commands:true,monsters:false),out _)==NativeDevState.NeedsHost,a.Id+" waits for an authority that offers monster commands (HOST NEEDS MONSTER COMMANDS), the sandbox included");
                    else Check(NativeDevActions.Resolve(a,Ctx(online:true,abuse:true,commands:true,monsters:false),out _)==NativeDevState.Live,a.Id+" does not need the monster protocol");
                }
                else if(a.Kind==NativeDevKind.Presentation)Check(s==NativeDevState.Live&&NativeDevActions.Resolve(a,off,out _)==NativeDevState.Live,a.Id+" (a view tool) is live wherever the panel is");
                else Check(s==NativeDevState.SandboxOnly&&why==NativeDevActions.SandboxOnly,a.Id+" stays sandbox only");
                var t=NativeDevActions.Resolve(a,sandbox,out why);
                Check(a.Kind==NativeDevKind.Contract&&!a.SandboxRoute?t==NativeDevState.NeedsOptIn:t==NativeDevState.Live,a.Id+" in the sandbox: "+(a.SandboxRoute?"its sandbox call":"a DEV command with the opt-in"));
                Check(NativeDevActions.Resolve(a,sandboxOn,out _)==NativeDevState.Live,a.Id+" in the sandbox with the opt-in");
                Check(a.Kind!=NativeDevKind.Contract||NativeDevActions.RunsInSandbox(a,sandboxOn)==a.SandboxRoute,a.Id+": the sandbox keeps its own call where it has one");
                Check(NativeDevActions.Resolve(a,Ctx(dev:false,online:true,abuse:true,commands:true),out why)==NativeDevState.Unavailable,a.Id+" never for a non-dev");
            }
            Check(NativeDevActions.Badge(NativeDevState.NeedsOptIn)=="OPT-IN"&&NativeDevActions.Badge(NativeDevState.NotNow)=="ROUND"&&NativeDevActions.Badge(NativeDevState.SandboxOnly)=="SBX"&&NativeDevActions.Badge(NativeDevState.Live)==null,"disabled buttons wear their reason's badge");
            // The panel covers exactly the authority's actions.
            var mine=NativeDevContract.Personal.Concat(NativeDevContract.Room).Concat(NativeDevContract.Interact).Concat(NativeDevContract.Monsters).ToArray();
            Check(mine.Length==NativeDevRules.Actions.Length&&new HashSet<string>(mine).SetEquals(NativeDevRules.Actions),"the panel's action groups are the authority's actions");
            foreach(var name in NativeDevRules.Actions)Check(NativeDevActions.All.Any(a=>a.Contract==name),"the panel has a button for "+name);
            Monsters(R);
            // The limits the panel clamps to are the validator's (probed at each edge).
            bool V(string action,params string[] args)=>NativeDevRules.Validate(new NativeDevCommand{transaction="t1",action=action,target=0,args=args})==null;
            string N(float v)=>NativeDevLink.Number(v);
            Check(V("debris",N(NativeDevContract.DebrisMassMin),"1","wood","face")&&V("debris",N(NativeDevContract.DebrisMassMax),N(NativeDevContract.DebrisSpeedMax),"wood","corner")&&!V("debris",N(NativeDevContract.DebrisMassMax+.5f),"1","wood","face")&&!V("debris","1",N(NativeDevContract.DebrisSpeedMax+.5f),"wood","face")&&!V("debris",N(NativeDevContract.DebrisMassMin-.5f),"1","wood","face"),"debris 1-500 kg, 1-100 m/s");
            Check(V("freeze","0")&&V("freeze",N(NativeDevContract.FreezeMax))&&!V("freeze",N(NativeDevContract.FreezeMax+.1f)),"freeze 0-30 s");
            Check(V("launch",N(NativeDevContract.LaunchMin))&&V("launch",N(NativeDevContract.LaunchMax))&&!V("launch",N(NativeDevContract.LaunchMax+.1f))&&!V("launch","0.5"),"launch 1-100 m/s");
            Check(V("spawn-dummies-around",NativeDevContract.AroundMin.ToString())&&V("spawn-dummies-around",NativeDevContract.AroundMax.ToString())&&!V("spawn-dummies-around","8")&&!V("spawn-dummies-around","0"),"dummies around 1-7");
            Check(V("time-scale",N(NativeDevContract.TimeScaleMin))&&V("time-scale",N(NativeDevContract.TimeScaleMax))&&!V("time-scale","2.1")&&!V("time-scale","0.05"),"time scale .1-2");
            Check(V("chip-add","vital","1")&&V("chip-add","vital",NativeDevContract.ChipCountMax.ToString())&&!V("chip-add","vital","101")&&!V("chip-add","vital","0"),"chip count 1-100");
            foreach(var input in NativeDevContract.SpawnInputs)Check(V("spawn-dummy","pistol",input,"0")&&V("spawn-dummy","pistol",input,NativeDevContract.TeamMax.ToString()),"spawn input "+input);
            Check(!V("spawn-dummy","pistol","mode","0")&&!V("spawn-dummy","pistol","ai","8"),"spawn inputs and teams outside the authority's are not offered");
            Check(NativeDevContract.MaxArgs==4&&NativeDevContract.MaxArgLength==64&&!V("weapon",new string('x',65))&&!NativeDevContract.ShapeOk("launch",0,new[]{"1","2"}),"four arguments of at most 64 characters, per action");
            // The panel's arguments: each action's arity (docs/DEV-ABUSE-AUTHORITY.md "Action arguments") and a sample the validator accepts.
            var arity=new Dictionary<string,(int n,string[] sample)>{["spawn-monster"]=(4,new[]{"shambler","3","near","hunt"}),["god"]=(1,new[]{"on"}),["infinite-ammo"]=(1,new[]{"off"}),["no-cooldowns"]=(1,new[]{"on"}),["chip-add"]=(2,new[]{"vital","1"}),["chip-remove"]=(2,new[]{"vital","1"}),["weapon"]=(1,new[]{"pistol"}),["swap-weapon"]=(1,new[]{"sword"}),["teleport"]=(3,new[]{"1","2","3"}),["send"]=(3,new[]{"1","2","3"}),["launch"]=(1,new[]{"20"}),["freeze"]=(1,new[]{"3"}),["spawn-dummy"]=(3,new[]{"pistol","ai","1"}),["spawn-dummies-around"]=(1,new[]{"3"}),["time-scale"]=(1,new[]{"0.5"}),["hazard-trigger"]=(1,new[]{"saw-1"}),["debris"]=(4,new[]{"8","15","wood","face"}),["debris-rain"]=(1,new[]{"stone"})};
            var panelSrc=R("Runtime/NativeDevPanel.cs");var args=Method(panelSrc,"private string[] DevArgs(NativeDevAction a)");
            foreach(var name in NativeDevRules.Actions)
            {
                var (n,sample)=arity.TryGetValue(name,out var e)?e:(0,Array.Empty<string>());Check(NativeDevContract.ShapeOk(name,0,sample),name+" takes "+n+" arguments");
                var m=Regex.Match(args,@"case """+Regex.Escape(name)+@""":(?:case ""[a-z-]+"":)*(?:\{[^\n]*?)?return new\[\]\{(.*?)\};");int got=m.Success?Regex.Split(Regex.Replace(m.Groups[1].Value,@"\([^()]*(\([^()]*\))*[^()]*\)",""),",").Length:0;
                if(name=="god"||name=="infinite-ammo"||name=="no-cooldowns"){Check(panelSrc.Contains("if(DevButton(on,\"ON\",state,reason,now))DevSend(id,\"on\");if(DevButton(off,\"OFF\",state,reason,!now))DevSend(id,\"off\");"),name+" sends on / off");continue;}
                Check(got==n,"DevArgs gives "+name+" "+n+" arguments (found "+got+")");
            }
            Check(panelSrc.Contains("DevSend(a.Id,id,NativeDevText.Int(count));")&&panelSrc.Contains("Mathf.Min(NativeDevContract.ChipCountMax,"),"chip stacks send their count, at most 100");
            foreach(var c in new[]{"NativeDevContract.LaunchMin,NativeDevContract.LaunchMax","0,NativeDevContract.FreezeMax","NativeDevContract.AroundMin,NativeDevContract.AroundMax","NativeDevContract.TimeScaleMin,NativeDevContract.TimeScaleMax","NativeDevContract.DebrisMassMin,NativeDevContract.DebrisMassMax","NativeDevContract.DebrisSpeedMin,NativeDevContract.DebrisSpeedMax","0,NativeDevContract.TeamMax"})Check(args.Contains(c),"DevArgs clamps to "+c);
            Check(NativeDevContract.Command=="dev-command"&&NativeDevContract.Reply=="dev-reply"&&NativeDevContract.RevealedField=="devRevealed"&&new[]{NativeDevContract.Accepted,NativeDevContract.Refused,NativeDevContract.Done}.SequenceEqual(new[]{"accepted","refused","done"}),"the wire names");
            // The link: nothing without a sender; only the authority's reply says done; the timeout reads as unknown.
            var link=new NativeDevLink();Check(!link.Available&&link.Send("restore",0,null,"Restore me")==null&&link.Status==""&&link.Pending==0,"no link: nothing is sent and no status claims success");
            var sent=new List<(string a,int t,string[] g)>();int next=0;NativeDevReply now=null;link.Sender=(a,t,g)=>{sent.Add((a,t,g));string id="tx"+(++next);if(now!=null){now.transaction=id;link.Receive(now);now=null;}return id;};
            var tx=link.Send("launch",4,new[]{"20"},"Launch",1);Check(tx=="tx1"&&sent.Count==1&&sent[0].a=="launch"&&sent[0].t==4&&sent[0].g.SequenceEqual(new[]{"20"})&&link.Outcome==NativeDevOutcome.Sent&&link.Status=="Launch · sent, waiting for the host","a send waits for the authority");
            Check(link.Send("noclip",0,null,"x")==null&&link.Send("launch",0,new[]{"500"},"x")==null&&sent.Count==1,"unknown actions and out-of-range arguments are never sent");
            Check(link.Receive(new NativeDevReply{transaction="tx1",state="accepted",reason=""})&&link.Outcome==NativeDevOutcome.Accepted&&link.Pending==1,"accepted keeps waiting");
            Check(link.Receive(new NativeDevReply{transaction="tx1",state="done",reason=""})&&link.Status=="Launch · done"&&link.Pending==0&&!link.StatusBad,"done only on the authority's reply");
            now=new NativeDevReply{state="refused",reason="Target must be alive."};var t2=link.Send("freeze",4,new[]{"3"},"Freeze",2);
            Check(t2=="tx2"&&link.Status=="Freeze · refused: Target must be alive."&&link.StatusBad&&link.Pending==0,"an immediate refusal (the offline host, an older host) shows its reason");
            link.Send("bring",4,null,"Bring",3);Check(link.Receive(new NativeDevReply{transaction="tx3",state="refused",reason="Host reply timed out; command outcome is unknown."})&&link.Outcome==NativeDevOutcome.Unknown&&link.Status=="Bring · outcome unknown: no host reply in 10 s","the authority's timeout reads as unknown, never undone or done");
            link.Send("grow",4,null,"Grow",4);link.Tick(4+NativeDevLink.Timeout-.1f);Check(link.Outcome==NativeDevOutcome.Sent,"no backstop before the timeout");link.Tick(4+NativeDevLink.Timeout+.1f);Check(link.Outcome==NativeDevOutcome.Unknown&&link.Pending==0&&link.Status.Contains("outcome unknown"),"the backstop also reads as unknown");
            Check(!link.Receive(new NativeDevReply{transaction="tx99",state="done",reason=""})&&link.Outcome==NativeDevOutcome.Unknown,"a reply for another transaction changes nothing");
            var panel=R("Runtime/NativeDevPanel.cs");
            Check(panel.Contains("if(state!=NativeDevState.Live){DevSay((a?.Label??id)+\" · \"+reason,true);return;}")&&!Regex.IsMatch(panel,"DevSay\\([^;]*(\"done|NativeDevContract\\.Done|accepted)"),"a disabled action only says why; the panel never reports a command done itself");
            Check(Regex.Matches(panel,@"devLink\.Send\(").Count==2&&panel.Contains("if(devLink.Status!=devLinkSeen){devLinkSeen=devLink.Status;")&&panel.Contains("private string DevSender(string action,int target,string[] args)=>SendDevCommand(action,target,args);")&&panel.Contains("DevReplyReceived+=DevOnReply;")&&Regex.Matches(panel,"SendDevCommand\\(").Count==1,"commands go out only through SendDevCommand; the status line shows the authority's replies");
            Check(!Regex.IsMatch(panel+R("Runtime/NativeDevOverlays.cs"),@"\b(DevGod|DevInfiniteAmmo|DevNoCooldowns|devRevealed|devScale|devCommandsRan|devDummy|DevTimeScale)\s*=[^=]")&&R("Runtime/NativeDevOverlays.cs").IndexOf("devLink",StringComparison.Ordinal)<0,"the presentation never writes the replicated DEV state (no optimistic effects)");
            Check(panel.Contains("me.Seat.devCommandsRan")&&panel.Contains("p.Seat.devDummy?NativeDevTags.DevDummy")&&panel.Contains("arena.DevTimeScale")&&panel.Contains("me.DevGod")&&panel.Contains("DevOldHost"),"the panel shows the replicated state: progression, DEV dummies, room speed, pilot flags, an older host");
        }
        private static void Monsters(Func<string,string> R)
        {
            // The proposed arguments: [kind, count 1-8, place, behaviour]; clear-monsters none.
            string[] A(params string[] v)=>v;
            Check(NativeDevContract.ShapeOk("spawn-monster",0,A("shambler","3","near","hunt"))&&NativeDevContract.ShapeOk("spawn-monster",4,A("hulk","1","entries","idle"))&&NativeDevContract.ShapeOk("clear-monsters",0,null),"the proposed shapes pass the panel's check");
            foreach(var bad in new[]{A("shambler","0","near","hunt"),A("shambler","9","near","hunt"),A("shambler","3","inside","hunt"),A("shambler","3","near","sleep"),A("","3","near","hunt"),A("shambler","3","near"),A(new string('x',65),"3","near","hunt"),A("shambler","2.5","aim","frenzy")})
                Check(!NativeDevContract.ShapeOk("spawn-monster",0,bad),"spawn-monster refuses "+string.Join("|",bad));
            Check(!NativeDevContract.ShapeOk("clear-monsters",0,A("all"))&&!NativeDevContract.ShapeOk("spawn-monster",-1,A("shambler","3","near","hunt")),"clear takes no arguments; a target is a seat");
            Check(NativeDevMonsters.ClampCount(0)==1&&NativeDevMonsters.ClampCount(99)==8&&NativeDevMonsters.ClampCount(5)==5,"the count clamps to 1-8");
            Check(NativeDevMonsters.Places.SequenceEqual(new[]{"near","aim","drop","entries"})&&NativeDevMonsters.Behaviours.SequenceEqual(new[]{"hunt","frenzy","idle"})&&NativeDevMonsters.PlaceHelp.Length==4&&NativeDevMonsters.BehaviourHelp.Length==3&&NativeDevMonsters.Protocol==2,"places, behaviours and the protocol as proposed");
            Check(NativeDevMonsters.RoomCap==24&&NativeDevMonsters.KindCap("hulk")==2&&NativeDevMonsters.KindCap("spitter")==NativeSpitterRules.Cap&&NativeDevMonsters.KindCap("shambler")==24,"caps: 24 a room, 2 hulks, NativeSpitterRules.Cap spitters");
            // The kinds come from the content's adds (no hard-coded list).
            var pve=JsonSerializer.Deserialize<NativePveContent>(File.ReadAllText(Path.Combine(RootOf(R),"Assets/Conversion/Resources/PveContent.json")),new JsonSerializerOptions{IncludeFields=true});
            var kinds=NativeDevMonsters.Kinds(pve.adds);Check(kinds.SequenceEqual(pve.adds.Select(a=>a.id))&&kinds.Contains("hulk")&&kinds.Contains("spitter")&&!kinds.Contains("crawler"),"the kinds are the content's adds ("+string.Join(",",kinds)+")");
            foreach(var k in kinds)Check(NativeDevMonsters.Icon(k)!="ghost","kind "+k+" has its own icon");
            var tab=R("Runtime/NativeDevMonsters.cs");
            Check(tab.Contains("NativeDevMonsters.Kinds(adds)")&&tab.Contains("var adds=NativeArena.StageContent.adds;"),"the tab lists the kinds from the arena content");
            // The adapter: off until Astra's flag exists; everything goes through it.
            Check(tab.Contains("public static bool DevMonsterOf(NativeMinion m)=>false;")||Regex.IsMatch(tab,@"public static bool DevMonsterOf\(NativeMinion m\)=>m!=null&&m\.\w*[Dd]evMonster\w*;"),"the adapter is off by default (or bound to the replicated flag in one line)");
            int uses=Regex.Matches(tab+R("Runtime/NativeDevOverlays.cs")+R("Runtime/NativeDevPanel.cs"),@"NativeDevMonsterAdapter\.DevMonsterOf\(").Count;Check(uses>=3,"the tag, the rift and the count all go through the adapter ("+uses+")");
            foreach(var f in Directory.GetFiles(Path.Combine(RootOf(R),"Assets/ChipArena/Runtime"),"*.cs"))if(!f.EndsWith("NativeDevMonsters.cs",StringComparison.Ordinal)&&!f.EndsWith("NativeDevMonsterAuthority.cs",StringComparison.Ordinal)&&!f.EndsWith("NativeDevAuthorityValidation.cs",StringComparison.Ordinal))Check(!Regex.IsMatch(File.ReadAllText(f),@"\.DevMonster\b|devMonster\b")||!f.Contains("NativeDev"),Path.GetFileName(f)+" reads DEV monsters only through the adapter");
            Check(tab.Contains("if(NativeDevMonsterAdapter.Entered(m)||NativeDevMonsterAdapter.SpawnAge(m,now)>NativeDevMonsterLook.FreshAge||blasts==null)continue;")&&R("Runtime/NativeDevPanel.cs").Contains("presentation.DevMonsterRifts(arena,prefs.reducedEffects);")&&R("Runtime/NativeBlastPresentation.cs").Contains("public void DevMonsterRift(Vector3 at,float height,bool reduced,double quality){int n=NativeDevMonsterLook.Rift(NativeArena.Vec(at),height,reduced,plan);Run(n,quality);}"),"the rift plays once per new DEV monster (not entry spawns, not late joins) through the shared effects renderer");
            Check(R("Runtime/NativeDevOverlays.cs").Contains("DevTags(height);devLines=0;DevPulseMarks();DevMonsterTags();"),"the DEV MONSTER tag draws for every client");
            // The panel: one path through SendDevCommand, gated by the host's protocol; the ward entries kept in the sandbox.
            var panel=R("Runtime/NativeDevPanel.cs");
            Check(panel.Contains("private bool DevMonsterCommands=>server!=null?devHostProtocol>=NativeDevMonsters.Protocol:NativeDevContract.Known(NativeDevMonsters.Spawn);")&&panel.Contains("private bool DevOldHost=>server!=null&&devHostProtocol<1;"),"monster commands need devProtocol 2 from a host (or the local authority's own); an older host is protocol 0");
            Check(panel.Contains("case \"spawn-monster\":return new[]{devMonsterKind??\"\",NativeDevText.Int(NativeDevMonsters.ClampCount(devMonsterCount)),NativeDevMonsters.Places[Mathf.Clamp(devMonsterPlace,0,NativeDevMonsters.Places.Length-1)],NativeDevMonsters.Behaviours[Mathf.Clamp(devMonsterBehaviour,0,NativeDevMonsters.Behaviours.Length-1)]};")&&panel.Contains("if(a.Tab==NativeDevTab.Interact||a.Tab==NativeDevTab.Monsters||a.Id==\"debris\")return devTarget==arena?.LocalId?0:devTarget;"),"spawn-monster sends the four clamped arguments for the picked target (0: me)");
            Check(NativeDevActions.Find("ward-spawn").Tab==NativeDevTab.Monsters&&tab.Contains("DevAct(new Rect(x+half+8,y,half,32),\"ward-spawn\");")&&tab.Contains("e.kind==\"breach\"||e.hulk?\"HULK\":e.kind==\"drop\"?\"BRUTE\":\"SHAMBLER\"")&&R("Runtime/NativeWardBreaches.cs").Contains("e.kind==\"breach\"||e.hulk?\"hulk\":e.kind==\"drop\"?\"brute\":\"shambler\""),"Ward Zero's sandbox entry spawn moved to Monsters, labelled with what SandboxWardEntry spawns");
            Check(tab.Contains("if(devMode!=NativeDevMode.Sandbox)return y;\n            y=DevHead(x,y,w,\"WARD ZERO ENTRIES (SANDBOX)\");"),"the Ward Zero entry picker shows only in the sandbox (where ward-spawn runs)");
            Check(tab.Contains("string info=U.Measure(devMonsterInfo[i],10,false,true).x<=r.width-46?devMonsterInfo[i]:devMonsterHp[i];")&&tab.Contains("y+=40;y=DevNote(x,y,w,devMonsterCapNote,C.Muted());"),"kind cards fall back to HP alone when narrow; the caps note wraps under the stepper");
            // The rift look: the DEV palette, fewer cues with Reduced effects, deterministic.
            var plan=new NativeImpactCommand[NativeImpactCues.MaxCommands];int full=NativeDevMonsterLook.Rift(new V3(1,0,2),2.6f,false,plan);bool magenta=plan.Take(full).Any(c=>c.Color==NativeDevMonsterLook.Magenta),beams=plan.Take(full).Count(c=>c.Op==NativeImpactOp.Beam)==3;
            int reduced=NativeDevMonsterLook.Rift(new V3(1,0,2),2.6f,true,plan);Check(full>8&&full<=plan.Length&&magenta&&beams&&reduced<=3&&reduced<full,"the rift: "+full+" cues (3 beams, magenta), "+reduced+" with Reduced effects");
            var theme=R("Runtime/NativeCyberTheme.cs");Check(theme.Contains("MagentaHex=0x"+NativeDevMonsterLook.Magenta.ToString("x6"))&&theme.Contains("CyanHex=0x"+NativeDevMonsterLook.Cyan.ToString("x6"))&&theme.Contains("VioletHex=0x"+NativeDevMonsterLook.Violet.ToString("x6"))&&theme.Contains("CyanSoftHex=0x"+NativeDevMonsterLook.CyanSoft.ToString("x6")),"the rift's colours are the cyber palette's");
            // First seen: once per id, a new round starts over.
            var seen=new NativeDevMonsterSeen();Check(!seen.Observe(1,7,false,0)&&seen.Observe(1,8,true,1)&&!seen.Observe(1,8,true,2)&&Math.Abs(seen.Age(8,3)-2)<1e-5&&seen.Age(9,3)<0&&seen.Observe(2,8,true,4),"a DEV monster's summon moment is its first sighting, again after a new round");
            // The link shows how many spawned (the reply's reason on done).
            var link=new NativeDevLink();link.Sender=(a,t,g)=>"m1";link.Send("restore",0,null,"Spawn monsters",0);Check(link.Receive(new NativeDevReply{transaction="m1",state="done",reason="Spawned 3 of 5 (cap)."})&&link.Status=="Spawn monsters · done: Spawned 3 of 5 (cap).","a done reply's reason (how many spawned) shows");
        }
        private static string rootPath;private static string RootOf(Func<string,string> R)=>rootPath;
        private static string Method(string src,string signature)
        {
            int a=src.IndexOf(signature,StringComparison.Ordinal);Check(a>=0,"source has "+signature);int depth=0,i=src.IndexOf('{',a);
            for(int j=i;j<src.Length;j++){if(src[j]=='{')depth++;else if(src[j]=='}'&&--depth==0)return src.Substring(a,j-a+1);}return src.Substring(a);
        }
        private static void ReadOnly(Func<string,string> R)
        {
            var overlays=R("Runtime/NativeDevOverlays.cs");
            foreach(var bad in new[]{@"arena\.[A-Za-z_][\w.]*\s*(\+|-|\*)?=[^=]",@"\b(p|s|h|a|m)\.(Hp|Yaw|Swing|Down|Juggle|Seat|Velocity|Position|Life|Range)\s*=[^=]",@"\.position\s*=[^=]",@"Sandbox(Action|Chip|Cast|Reset|Preview|Launch|Configure|WardEntry)",@"\bDamage\(",@"\bEmit\(",@"Events\.Add",@"Physics\.Simulate",@"isKinematic\s*=",@"\.enabled\s*=",@"linearVelocity",@"AddComponent|new GameObject|Instantiate"})
                Check(!Regex.IsMatch(overlays,bad),"the overlays write nothing back (no "+bad+")");
            Check(overlays.Contains("NativeSpecialRules.Ballistic(ref v,s.Gravity,h)")&&overlays.Contains("var v=NativeArena.Vec(s.Velocity);"),"shot paths step a copy with the authority's own ballistic rule");
            Check(overlays.Contains("NativeMeleeRules.Angle(s,")&&overlays.Contains("var col=p.Collider;")&&overlays.Contains("NativeDevVolumes.HeadY"),"the volumes come from the swing, the collider and the head-box numbers");
            var panel=R("Runtime/NativeDevPanel.cs");var camera=Method(panel,"private void DevCamera()");
            Check(camera.Contains("t.SetPositionAndRotation(new Vector3(devFly.X,devFly.Y,devFly.Z),Quaternion.Euler(devFly.Pitch,devFly.Yaw,0));")&&!Regex.IsMatch(camera,@"arena\.|pending|yaw=|pitch=|Body|Input"),"the fly camera moves only the match camera");
            var update=Method(panel,"private void DevUpdate(bool typing)");Check(!Regex.IsMatch(update,@"arena\.[A-Za-z_][\w.]*\s*=[^=]")&&!update.Contains("pending"),"the panel's update writes no arena state or input");
            var game=R("Runtime/ConversionGame.cs");
            Check(game.Contains("arena.Pve?.Holding!=true&&!DevHoldsPilot;")&&game.Contains("bool look=!BoardOpen&&!ExpeditionScreenOpen&&DevLook(")&&panel.Contains("private bool DevHoldsPilot=>devFly.On||devOpen&&(devCursorFree||devTyping);"),"flying or using the panel's cursor sends the neutral input (the pilot holds still)");
            // The fly camera is pure: its own pose, the pilot camera's look mapping, clamped pitch and speed.
            var f=new NativeDevFly();f.Place(1,2,3,90,10);f.Move(0,0,1,false,false,.1f);
            Check(Math.Abs(f.X-(1+Math.Cos(10*Math.PI/180)))<1e-3&&Math.Abs(f.Y-(2-Math.Sin(10*Math.PI/180)))<1e-3&&Math.Abs(f.Z-3)<1e-3,"forward is Unity's Euler(pitch,yaw,0) forward ("+f.X+","+f.Y+","+f.Z+")");
            f.Place(0,0,0,0,0);f.Move(1,0,0,false,false,.1f);Check(Math.Abs(f.X-1)<1e-3&&Math.Abs(f.Z)<1e-3,"right is +x at yaw 0");f.Move(0,1,0,true,false,.1f);Check(Math.Abs(f.Y-3)<1e-3,"Shift is x3 and up is world up");
            f.Place(0,0,0,0,0);f.Move(0,0,1,false,false,5);Check(Math.Abs(f.Z-1)<1e-3,"a long frame moves at most .1 s worth");
            f.Look(0,-1000,1);Check(f.Pitch==NativeDevFly.PitchLimit,"pitch clamps");f.Look(3,0,.5f);Check(Math.Abs(f.Yaw-3)<1e-4,"yaw takes the pilot camera's x2 sensitivity mapping");
            f.Wheel(100);Check(f.Speed==NativeDevFly.MaxSpeed,"speed clamps high");f.Wheel(-200);Check(f.Speed==NativeDevFly.MinSpeed,"and low");float x=f.X;f.Move(1,0,0,false,false,float.NaN);Check(f.X==x,"no move on a bad frame time");
        }
        private static void Volumes()
        {
            // The head box NativeCombatRules.Headshot tests: centre +HeadY, half HeadHalf (no pad at zero radius).
            var at=new V3(3,1,-2);bool Head(float dy,float dz)=>NativeCombatRules.Headshot(at,0,new V3(at.x-5,at.y+NativeDevVolumes.HeadY+dy,at.z+dz),new V3(1,0,0),0,false);
            Check(Head(NativeDevVolumes.HeadHalf-.002f,0)&&!Head(NativeDevVolumes.HeadHalf+.002f,0)&&Head(0,NativeDevVolumes.HeadHalf-.002f)&&!Head(0,NativeDevVolumes.HeadHalf+.002f)&&!NativeCombatRules.Headshot(at,0,new V3(at.x-5,at.y+NativeDevVolumes.HeadY,at.z),new V3(1,0,0),0,true),"the drawn head box is the rule's (and none while down)");
            // The melee sector NativeMeleeRules.Contact tests for a pilot (extra PilotExtra): range, height band and cone.
            var s=new NativeMeleeSwing{Spec="sword",HitAt=0,HitUntil=1,Range=2,Yaw=0,Arc=0};float e=NativeDevVolumes.PilotExtra,reach=s.Range+e;
            bool Hit(float d,float angle,float y)=>NativeMeleeRules.Contact(s,.5f,new V3((float)Math.Sin(angle)*d,y,(float)Math.Cos(angle)*d),e);
            float half=NativeDevVolumes.Cone+(float)Math.Asin(Math.Min(.95,e/reach));
            Check(Hit(reach-.01f,0,0)&&!Hit(reach+.01f,0,0)&&Hit(1,0,NativeDevVolumes.MeleeHeight+e-.01f)&&!Hit(1,0,NativeDevVolumes.MeleeHeight+e+.01f),"the drawn reach and height band are the rule's");
            Check(Hit(reach-.02f,half-.01f,0)&&!Hit(reach-.02f,half+.03f,0)&&Math.Abs(NativeMeleeRules.Angle(s,.5f))<1e-5,"the drawn cone is the rule's at full reach");
        }
        private static void Reveal()
        {
            var seat=new NativeSeat{id=3,name="Sheldon",access=NativeAccess.Guest};Check(!NativeDevTags.Revealed(seat)&&!NativeDevTags.Revealed(null),"nobody is tagged until revealed");
            Check(typeof(NativeSeat).GetField(NativeDevContract.RevealedField)!=null,"NativeSeat.devRevealed is the replicated field");seat.devRevealed=true;Check(NativeDevTags.Revealed(seat),"the tag follows devRevealed");seat.devRevealed=false;Check(!NativeDevTags.Revealed(seat),"and only it (never the access grant)");
            var r=new NativeDevReveal();Check(!r.Observe(3,"Sheldon",true,0)&&!r.Active(0),"a pilot already revealed when first seen gets the tag, not the moment");
            Check(!r.Observe(4,"Astra",false,1)&&r.Observe(4,"Astra",true,2)&&r.Active(2)&&r.Seat==4&&!r.Preview&&r.Active(2+NativeDevReveal.Seconds-.01f)&&!r.Active(2+NativeDevReveal.Seconds),"false to true plays the moment for its length");
            Check(!r.Observe(4,"Astra",true,3)&&!r.Observe(4,"Astra",false,4)&&r.Observe(4,"Astra",true,5),"it plays again only on a new reveal");
            r.Start(1,"Me",10,true);Check(r.Previewing(1,10+NativeDevReveal.Seconds+1)&&!r.Previewing(2,11)&&!r.Previewing(1,10+NativeDevReveal.Seconds+NativeDevReveal.PreviewTag),"the sandbox preview tags my pilot for a while");
            float last=-1;bool rising=true;for(float t=0;t<=NativeDevReveal.Seconds;t+=.05f){float a=NativeDevReveal.Alpha(t);if(float.IsNaN(a)||a<0||a>1)rising=false;last=a;}
            Check(rising&&NativeDevReveal.Alpha(-1)==0&&NativeDevReveal.Alpha(NativeDevReveal.Seconds)==0&&NativeDevReveal.Alpha(1.5f)==1&&NativeDevReveal.Open(1)==1&&NativeDevReveal.Open(0)==0,"the moment fades in, holds and goes");
            Check(NativeDevReveal.Typed(0,19,false)==0&&NativeDevReveal.Typed(5,19,false)==19&&NativeDevReveal.Typed(0,19,true)==19,"the eyebrow types in (all at once with Reduced effects)");
            int shards=0;for(int i=0;i<NativeDevReveal.Shards;i++)if(NativeDevReveal.Shard(i,1,false,out float x,out float y,out float a,out float size,out int tone)){shards++;Check(!float.IsNaN(x+y+a+size)&&a>=0&&a<=1&&tone>=0&&tone<4,"shard "+i+" is finite");}
            Check(shards>NativeDevReveal.Shards/2&&!NativeDevReveal.Shard(0,1,true,out _,out _,out _,out _,out _),"the burst shows, and none with Reduced effects");
            Check(NativeDevReveal.Shard(5,1.1f,false,out float x1,out float y1,out _,out _,out _)&&NativeDevReveal.Shard(5,1.1f,false,out float x2,out float y2,out _,out _,out _)&&x1==x2&&y1==y2,"the burst is deterministic (every client draws the same)");
        }
        private static void Hazards(string root,Func<string,string> R)
        {
            // Every authored hazard kind gets a pulse look from the shared cues, sized from its volume.
            var o=new JsonSerializerOptions{IncludeFields=true};var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),o).maps;
            var plan=new NativeImpactCommand[NativeImpactCues.MaxCommands];int kinds=0;var seen=new HashSet<string>();
            foreach(var m in maps)foreach(var h in m.hazards??Array.Empty<MapHazard>())
            {
                float reach=NativeDevHazardLook.Reach(h);Check(reach>=.6f&&reach<=6,h.id+" pulse reach "+reach);
                int n=NativeDevHazardLook.Plan(h.kind,new V3(1,2,3),reach,plan);Check(n>0&&n<=plan.Length,h.kind+" pulse plans "+n+" cues");
                for(int i=0;i<n;i++)Check(!double.IsNaN(plan[i].Radius)&&!float.IsNaN(plan[i].A.x+plan[i].B.x)&&plan[i].Color!=0,h.kind+" cue "+i+" is finite and coloured");
                if(seen.Add(h.kind))kinds++;
            }
            Check(kinds>=8&&NativeDevHazardLook.Plan("unknown",new V3(),1,plan)>0&&NativeDevHazardLook.Reach(null)==1.5f,"every hazard kind ("+kinds+") and an unknown one have a pulse");
            Check(NativeDevHazardLook.Plan("saw-slot",new V3(),1,plan)>0&&plan[0].Color==NativeImpactCues.SawColor&&NativeDevHazardLook.Plan("burner",new V3(),1,plan)>0&&plan[0].Color==NativeImpactCues.LavaColor,"saws and burners use the existing saw and fire cues");
            var blast=R("Runtime/NativeBlastPresentation.cs");var overlays=R("Runtime/NativeDevOverlays.cs");var panel=R("Runtime/NativeDevPanel.cs");
            Check(blast.Contains("public void DevHazardPulse(Vector3 at,string kind,float reach,double quality){int n=NativeDevHazardLook.Plan(kind,NativeArena.Vec(at),reach,plan);Run(n,quality);}")&&overlays.Contains("foreach(var h in arena.DevHazardPulses)")&&overlays.Contains("if(devPulsesBefore.Contains(key)||arena.Time-h.at>.6f||blasts==null)continue;")&&panel.Contains("presentation.DevHazardPulses(arena);"),"each forced pulse record plays once through the shared effects renderer, from the replicated records");
            Check(!Regex.IsMatch(overlays,@"NativeFactoryHazards|NativeCyberCityHazards\.Evaluate|NativeWardZeroHazards|hazardClock"),"the scheduled hazard presentation is not retimed");
            // Grown or shrunk pilots keep their nameplates over their heads (devScale is in the rig's scale already).
            foreach(var f in new[]{"NativeCombatHud.cs","NativeChipSelectHud.cs","NativeDevOverlays.cs"})Check(R("Runtime/"+f).Contains("Vector3.up*1.3f*(p.Seat.spec==\"siren\"?p.Pose.RigScale:p.Seat.devScale)"),f+" lifts the nameplate with devScale");
        }
        private static void Log()
        {
            foreach(EventKind k in Enum.GetValues(typeof(EventKind)))Check(NativeDevLog.Category(k)!=NativeDevLogFilter.None&&NativeDevLog.KindName(k)==k.ToString().ToUpperInvariant(),"every event kind has a category and a name ("+k+")");
            var log=new NativeDevLog();var events=new NativeEventBuffer();string Name(int id)=>id==1?"Sheldon":id==2?"Dummy":null;
            events.Add(new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=1,Target=2,Amount=18,Time=1});events.Add(new ArenaEvent{Id=2,Kind=EventKind.Death,Source=1,Target=2,Time=1.2f});
            Check(log.Observe(events,1,Name)==2&&log.Count==2&&log[0].Kind==EventKind.Death&&log[1].Text.Contains("HIT")&&log[1].Text.Contains("Sheldon → Dummy")&&log[1].Text.Contains("18"),"new events become lines, newest first");
            Check(log.Observe(events,1,Name)==0,"an event is logged once");
            events.Add(new ArenaEvent{Id=3,Kind=EventKind.Heal,Source=2,Amount=5,Time=2});Check(log.Observe(events,1,Name)==1&&log[0].Category==NativeDevLogFilter.Heals,"later events append");
            events.Clear();events.Add(new ArenaEvent{Id=1,Kind=EventKind.Block,Source=2,Time=0});Check(log.Observe(events,2,Name)==1&&log[0].Kind==EventKind.Block,"a new round's stream starts over");
            Check(NativeDevLog.Shows(log[0],NativeDevLogFilter.Defense,false,1)&&!NativeDevLog.Shows(log[0],NativeDevLogFilter.Damage,false,1)&&!NativeDevLog.Shows(log[0],NativeDevLogFilter.All,true,1)&&NativeDevLog.Shows(log[0],NativeDevLogFilter.All,true,2),"filters by category and by my seat");
            for(int i=0;i<NativeDevLog.Capacity+10;i++){events.Add(new ArenaEvent{Id=100+i,Kind=EventKind.Land,Source=1,Time=3});}log.Observe(events,2,Name);Check(log.Count==NativeDevLog.Capacity&&log[0].Id==100+NativeDevLog.Capacity+9,"the ring keeps the newest "+NativeDevLog.Capacity);
        }
        private static void Chips()
        {
            var chips=new[]{new ContentChip{id="b",name="Beta",rarity="rare",description="bounce",max=3},new ContentChip{id="a",name="Alpha",rarity="common",description="armour",max=5},new ContentChip{id="c",name="Gamma",rarity="epic",description="crit",max=1}};
            var installed=new Dictionary<string,int>{["c"]=1};int Of(string id)=>installed.TryGetValue(id,out int n)?n:0;var q=new NativeDevChips();
            Check(q.Update(chips,Of,1)&&q.Shown.Select(i=>chips[i].id).SequenceEqual(new[]{"a","b","c"}),"rarity order (common, rare, epic)");Check(!q.Update(chips,Of,1),"unchanged: no recompute");
            q.Sort=NativeDevChips.SortInstalled;q.Update(chips,Of,1);Check(chips[q.Shown[0]].id=="c","installed first");
            q.Search="BOUN";q.Update(chips,Of,1);Check(q.Shown.Count==1&&chips[q.Shown[0]].id=="b","search covers name, id and description");q.Search="";
            q.Filter=NativeDevChips.FilterFavourites;q.Favourite("a");q.Update(chips,Of,1);Check(q.Shown.Count==1&&chips[q.Shown[0]].id=="a"&&q.FavouritesText()=="a","favourites (saved as text)");
            var r=new NativeDevChips();r.LoadFavourites("a,c");Check(r.Favourites.SetEquals(new[]{"a","c"}),"favourites load");
            q.Filter=NativeDevChips.FilterRecent;q.Touch("c");q.Touch("b");q.Update(chips,Of,1);Check(q.Shown.Select(i=>chips[i].id).SequenceEqual(new[]{"b","c"}),"recent, newest first");
            q.Filter=NativeDevChips.FilterInstalled;q.Update(chips,Of,2);Check(q.Shown.Count==1&&chips[q.Shown[0]].id=="c","installed only");
            for(int i=0;i<12;i++)q.Touch("x"+i);Check(q.Recent.Count==NativeDevChips.RecentMax,"recent keeps "+NativeDevChips.RecentMax);
            Check(NativeDevChips.Rank("common")<NativeDevChips.Rank("rare")&&NativeDevChips.Rank("mythic")<NativeDevChips.Rank("siren")&&NativeDevChips.Rank("??")>NativeDevChips.Rank("siren"),"rarity ranks as NativeChipRanks.RarityRank");
            foreach(var id in new[]{"common","uncommon","rare","epic","legendary","mythic","mishima","ansatsuken","l44t","siren","x"})Check(NativeDevChips.Rank(id)==NativeChipRanks.RarityRank(id),"rank of "+id+" matches the ledger's");
        }
        private static void Layout(string root)
        {
            foreach(var (sw,sh,scale) in new[]{(1280f,720f,1f),(1920f,1080f,1f),(1920f,1080f,1.5f),(2560f,1440f,1.25f),(3440f,1440f,1f),(5120f,1440f,1f),(1280f,720f,1.5f),(3840f,2160f,.75f),(1280f,1024f,1f)})
            {
                var l=NativeUiMotion.Measure(sw,sh,scale);var p=NativeDevLayout.Panel(l.Width,l.Height);string at=sw+"x"+sh+" @"+scale;
                Check(p.X>=0&&p.X+p.W<=l.Width-NativeDevLayout.Margin+.01f&&p.Y>=0&&p.Y+p.H<=l.Height-NativeDevLayout.Margin+.01f,at+": the panel fits the canvas");
                Check(p.W<=720&&p.W<=l.Width*.45f&&p.BodyH>=300&&p.BodyW>=330,at+": it leaves the fight in view and keeps a usable body ("+p.W+" x "+p.BodyH+")");
            }
            Check(NativeDevLayout.TabNames.Length==NativeDevLayout.Tabs&&NativeDevLayout.TabTitles.Length==NativeDevLayout.Tabs&&NativeDevLayout.TabIcons.Length==NativeDevLayout.Tabs&&Enum.GetValues(typeof(NativeDevTab)).Length==NativeDevLayout.Tabs&&NativeDevLayout.Tabs==10&&NativeDevLayout.TabNames[(int)NativeDevTab.Monsters]=="MONSTERS","ten tabs, Monsters among them");
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json")));var icons=new HashSet<string>(doc.RootElement.GetProperty("icons").EnumerateArray().Select(e=>e.GetProperty("id").GetString()));
            var art=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ClassIcons.json"))).RootElement;var artIcons=new HashSet<string>(art.GetProperty("icons").EnumerateArray().Select(e=>e.GetProperty("id").GetString()));
            foreach(var id in NativeDevLayout.TabIcons.Concat(new[]{"x","scan","bot","sparkles"}))Check(icons.Contains(id)||artIcons.Contains(id),"icon "+id+" exists");
            foreach(NativeDevTab t in Enum.GetValues(typeof(NativeDevTab)))Check(NativeDevActions.All.Any(a=>a.Tab==t),"tab "+t+" has actions");
            Check(NativeDevActions.All.Select(a=>a.Id).Distinct().Count()==NativeDevActions.All.Length&&NativeDevActions.All.All(a=>NativeDevActions.Find(a.Id)==a)&&NativeDevActions.Find("nope")==null,"action ids are unique and found");
            Check(NativeDevActions.All.Where(a=>a.Key!=null).All(a=>NativeDevKeys.All.Contains(a.Key)),"every action key is a panel hotkey");
        }
        private static readonly NativeDevMonsterSeen monsterSeen=new NativeDevMonsterSeen();private static readonly string[] monsterArgs={"brute","3","near","hunt"};
        private static readonly string[] PulseKinds={"saw-rail","press","vent","grid","maglev","burner","gas","shutter","crane","x"};private static readonly NativeImpactCommand[] pulsePlan=new NativeImpactCommand[NativeImpactCues.MaxCommands];
        private static void Allocations()
        {
            var contexts=new[]{Ctx(sandbox:true),Ctx(abuse:true),Ctx(online:true,hosting:true,abuse:true),Ctx(online:true,abuse:true,commands:true),Ctx(dev:false,abuse:true)};
            var events=new NativeEventBuffer();for(int i=1;i<=40;i++)events.Add(new ArenaEvent{Id=i,Kind=(EventKind)(i%10),Source=1+i%3,Target=2,Amount=i,Time=i*.1f});
            var log=new NativeDevLog();Func<int,string> name=id=>id==1?"A":id==2?"B":"C";log.Observe(events,1,name);
            var graph=new NativeDevGraph(120);var fly=new NativeDevFly();var chips=new ContentChip[30];for(int i=0;i<chips.Length;i++)chips[i]=new ContentChip{id="c"+i,name="Chip "+i,rarity=i%2==0?"rare":"common",description="d",max=3};
            var q=new NativeDevChips();Func<string,int> installed=id=>id=="c3"?1:0;q.Update(chips,installed,7);var reveal=new NativeDevReveal();var seat=new NativeSeat{id=2,name="B"};
            long gate=0,actions=0,logB=0,graphB=0,flyB=0,chipB=0,text=0,revealB=0,layout=0;int sink=0;
            void Pass(bool split)
            {
                for(int frame=0;frame<20;frame++)
                {
                    long b0=GC.GetAllocatedBytesForCurrentThread();
                    foreach(var c in contexts){sink+=(int)NativeDevGate.Mode(c);sink+=NativeDevGate.Indicator(c)?1:0;}
                    long b1=GC.GetAllocatedBytesForCurrentThread();
                    foreach(var c in contexts)foreach(var a in NativeDevActions.All){sink+=(int)NativeDevActions.Resolve(a,c,out string why)+why.Length;sink+=NativeDevActions.RunsInSandbox(a,c)?1:0;}sink+=NativeDevActions.Find("dummy-cast")!=null?1:0;
                    long b2=GC.GetAllocatedBytesForCurrentThread();
                    log.Observe(events,1,name);for(int i=0;i<log.Count;i++){var e=log[i];if(NativeDevLog.Shows(e,NativeDevLogFilter.All&~NativeDevLogFilter.World,frame%2==0,1))sink+=e.Text.Length;}
                    long b3=GC.GetAllocatedBytesForCurrentThread();
                    graph.Push(frame*.7f);sink+=(int)(graph.Max()+graph.Average()+graph.Last);for(int i=0;i<graph.Count;i++)sink+=(int)graph.At(i);
                    long b4=GC.GetAllocatedBytesForCurrentThread();
                    fly.Look(.3f,-.2f,1);fly.Move(1,0,1,frame%3==0,false,1/60f);fly.Wheel(frame%2==0?1:-1);NativeDevFly.Forward(fly.Yaw,fly.Pitch,out float fx,out _,out _);sink+=(int)fx;
                    long b5=GC.GetAllocatedBytesForCurrentThread();
                    sink+=q.Update(chips,installed,7)?1:0;sink+=q.Shown.Count;
                    long b6=GC.GetAllocatedBytesForCurrentThread();
                    for(int i=0;i<30;i++){sink+=NativeDevText.Int(i*37).Length+NativeDevText.Value(i*1.5f,1," m/s").Length+NativeDevText.Value(i,0," s").Length+NativeDevText.Count(i,30).Length+NativeDevText.Value(i*.01f,2," ms").Length;}
                    long b7=GC.GetAllocatedBytesForCurrentThread();
                    reveal.Observe(2,"B",NativeDevTags.Revealed(seat),frame);sink+=reveal.Active(frame)?1:0;sink+=reveal.Previewing(2,frame)?1:0;
                    for(int i=0;i<NativeDevReveal.Shards;i++)if(NativeDevReveal.Shard(i,1+frame*.02f,false,out float sx,out _,out _,out _,out _))sink+=(int)sx;
                    sink+=(int)(NativeDevReveal.Alpha(frame*.1f)+NativeDevReveal.Open(frame*.1f)+NativeDevReveal.NameIn(frame*.1f)+NativeDevReveal.Flash(frame*.01f)+NativeDevReveal.Ring(frame))+NativeDevReveal.Typed(frame*.1f,19,false);
                    long b8=GC.GetAllocatedBytesForCurrentThread();
                    var p=NativeDevLayout.Panel(1600+frame,900);sink+=(int)(p.BodyX+p.BodyY+p.BodyW+p.BodyH);foreach(var k in PulseKinds)sink+=NativeDevHazardLook.Plan(k,new V3(frame,0,0),1.5f,pulsePlan);
                    sink+=NativeDevMonsterLook.Rift(new V3(frame,0,0),2.6f,frame%2==0,pulsePlan);sink+=monsterSeen.Observe(1,frame%5,true,frame)?1:0;sink+=(int)monsterSeen.Age(frame%5,frame);
                    sink+=NativeDevMonsters.ArgsOk("spawn-monster",monsterArgs)?1:0;sink+=NativeDevMonsters.KindCap("hulk")+NativeDevMonsters.ClampCount(frame)+NativeDevMonsters.Icon("brute").Length;
                    long b9=GC.GetAllocatedBytesForCurrentThread();
                    if(split){gate+=b1-b0;actions+=b2-b1;logB+=b3-b2;graphB+=b4-b3;flyB+=b5-b4;chipB+=b6-b5;text+=b7-b6;revealB+=b8-b7;layout+=b9-b8;}
                }
            }
            // The identical pass first, twice (one-time runtime work on first use, type set-up and tier-up, is not the frame's), then
            // up to three measured passes: a real per-frame allocation allocates in every pass, so one clean pass proves there is
            // none, while a background tier-up landing in one pass on a busy CI runner does not fail the check. The failure names
            // the calls that allocated in the cleanest pass.
            Pass(false);Pass(false);long used=long.MaxValue;string detail="";
            for(int attempt=0;attempt<3&&used!=0;attempt++)
            {
                gate=actions=logB=graphB=flyB=chipB=text=revealB=layout=0;long before=GC.GetAllocatedBytesForCurrentThread();Pass(true);long got=GC.GetAllocatedBytesForCurrentThread()-before;
                if(got<used){used=got;detail="gate "+gate+", actions "+actions+", log "+logB+", graph "+graphB+", fly "+flyB+", chips "+chipB+", text "+text+", reveal "+revealB+", layout "+layout;}
            }
            Check(used==0,"no allocations per frame ("+used+" bytes over 20 frames: "+detail+"; "+sink%2+")");
        }
        private static void Sources(string root,Func<string,string> R)
        {
            string panel=R("Runtime/NativeDevPanel.cs"),overlays=R("Runtime/NativeDevOverlays.cs"),core=R("Core/NativeDevTools.cs"),both=panel+overlays+R("Runtime/NativeDevMonsters.cs");
            Check(!core.Contains("UnityEngine"),"the DEV rules are engine-free Core");
            Check(!both.Contains("GUILayout")&&!both.Contains("using System.Linq")&&!both.Contains("$\"")&&!Regex.IsMatch(both,@"(?<!Mathf|Math|NativeDevText)\.(Where|Select|ToList|Any|First|FirstOrDefault|OrderBy|Count|Sum|Max|Min)\("),"kit-drawn, no LINQ or interpolation in the panel and overlays");
            Check(!Regex.IsMatch(both,@"0x[0-9a-fA-F]{6}|U\.Hex\(|NativeUi\.Hex\(|new Color\("),"colours only from the cyber palette (and the content's rarity colours)");
            Check(panel.Contains("ColorUtility.TryParseHtmlString(r.color,out var col)"),"rarity colours from the content");
            // Hooks (one line each in the shared files).
            var game=R("Runtime/ConversionGame.cs");
            Check(game.Contains("CameraKeys(typing);DevUpdate(typing);")&&game.Contains("if(DevScreenshot())return;")&&game.Contains("if(arena!=null)DevGui(width,height);\n            if(!menu&&arena!=null){ReplayHud(width,height);PveHud(width,height);"),"ConversionGame calls the panel's update, screenshot mode and GUI");
            int shake=game.IndexOf("SprintSway();",StringComparison.Ordinal),fly=game.IndexOf("DevCamera();",StringComparison.Ordinal),render=game.IndexOf("presentation.Render(frame,renderTime,false);",StringComparison.Ordinal);
            Check(shake>0&&fly>shake&&render>fly,"the fly camera replaces the view after the pilot camera's shakes, before the frame draws");
            var combat=R("Runtime/NativeCombatHud.cs");Check(combat.Contains("ChipSelectTags(scale,height);")&&combat.IndexOf("DevWorld(scale,height);",StringComparison.Ordinal)>combat.IndexOf("ChipSelectTags(scale,height);",StringComparison.Ordinal)&&combat.Contains("DevFeedTags(r,lines.Length,e);"),"the DEV tags over the nameplates and on the kill feed");
            Check(R("Runtime/NativeDeathBroadcastPresentation.cs").Contains("time>=i.At+1.05f&&time<i.At+5.15f")&&overlays.Contains("time>=i.At+1.05f&&time<i.At+5.15f"),"the kill-feed tags read the lines DeathMessages shows");
            var home=R("Runtime/NativeHomeMenu.cs");Check(home.Contains("if(DevPanelAvailable)items.Add((\"flask-conical\",\"DEV Tools\",false,true,()=>DevOpen(true)));")&&!home.Contains("SandboxScreen(")&&!home.Contains("SandboxBody"),"the pause menu opens the DEV panel; the old screen is gone");
            Check(!R("Runtime/NativeStructureFxPresentation.cs").Contains("StructureFxRows")&&!R("Runtime/NativeStructureFxPresentation.cs").Contains("GUILayout"),"the structure preview rows moved to the panel");
            // Hotkeys: the panel's keys are exactly NativeDevKeys and no other Runtime file binds them.
            var keys=Regex.Matches(panel,@"GetKeyDown\(KeyCode\.(\w+)\)").Select(m=>m.Groups[1].Value).Distinct().OrderBy(s=>s).ToArray();
            Check(keys.SequenceEqual(NativeDevKeys.All.OrderBy(s=>s)),"the panel's hotkeys are NativeDevKeys ("+string.Join(",",keys)+")");
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
            {
                if(f.EndsWith("NativeDevPanel.cs",StringComparison.Ordinal))continue;var src=File.ReadAllText(f);
                foreach(var k in NativeDevKeys.All)Check(!src.Contains("KeyCode."+k+")")&&!src.Contains("KeyCode."+k+";")&&!src.Contains("KeyCode."+k+","),Path.GetFileName(f)+" does not bind "+k);
            }
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Editor"),"*.cs"))foreach(var k in NativeDevKeys.All)Check(!File.ReadAllText(f).Contains("KeyCode."+k+")"),Path.GetFileName(f)+" does not bind "+k);
            // New scripts carry their own metas.
            var guids=new HashSet<string>();
            foreach(var f in new[]{"Core/NativeDevTools.cs","Runtime/NativeDevPanel.cs","Runtime/NativeDevOverlays.cs","Runtime/NativeDevMonsters.cs"})
            {
                var meta=Path.Combine(root,"Assets/ChipArena",f+".meta");Check(File.Exists(meta),f+" has a meta");var g=Regex.Match(File.ReadAllText(meta),@"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),f+" has its own guid");
            }
            int clash=0;foreach(var meta in Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories)){var m=Regex.Match(File.ReadAllText(meta),@"guid: ([0-9a-f]{32})");if(m.Success&&guids.Contains(m.Groups[1].Value)&&!meta.Contains("NativeDev"))clash++;}
            Check(clash==0,"the guids are fresh");
        }
    }
}
#endif
