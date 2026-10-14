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
    // authority actions disabled with the reason outside the sandbox and nothing reported done until a host reply; the view
    // tools (overlays, fly camera) read-only and drawn from the authority's own volumes; no per-frame allocation (warmed by the
    // identical pass first); the cyber palette, no GUILayout or LINQ in the panel, the hooks, the hotkeys and the metas.
    internal static class DevToolsChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("DEV tools: "+what);count++;}
        public static void Run(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            Toggle(R);Matrix();Routes(R);Authority(R);ReadOnly(R);Volumes();Reveal();Log();Chips();Layout(root);Allocations();Sources(root,R);
            Console.WriteLine("PASS DEV tools ("+count+" checks: the DEV Abuse toggle only for a verified dev, off by default; the panel in the sandbox and, with DEV Abuse, every offline / host / guest match, never for a non-dev; "+NativeDevActions.All.Count(a=>a.SandboxRoute&&a.Kind!=NativeDevKind.Presentation)+" sandbox routes on the old NativeArena calls; contract actions disabled with the reason until Astra's authority, nothing done before a reply; overlays and fly camera read-only on the authority's own volumes; no per-frame allocation; palette, hooks, hotkeys and metas)");
        }
        private static NativeDevContext Ctx(bool inMatch=true,bool sandbox=false,bool online=false,bool hosting=false,bool dev=true,bool abuse=false,bool authority=false)=>NativeDevContext.Of(inMatch,sandbox,online,hosting,dev,abuse,authority);
        private static void Toggle(Func<string,string> R)
        {
            Check(NativeDevGate.ToggleVisible(true)&&!NativeDevGate.ToggleVisible(false),"the toggle shows only for a verified dev");
            var game=R("Runtime/ConversionGame.cs");Check(Regex.IsMatch(game,@"public bool devAbuse;")&&!Regex.IsMatch(game,@"devAbuse\s*=\s*true"),"DEV Abuse is a saved preference, false unless the dev turns it on");
            var settings=R("Runtime/NativeSettings.cs");int dev=settings.IndexOf("if(account.Developer)",StringComparison.Ordinal),toggle=settings.IndexOf("NativeDevGate.ToggleVisible(account.Developer)",StringComparison.Ordinal),credits=settings.IndexOf("Field(\"Credits\")",StringComparison.Ordinal);
            Check(dev>0&&toggle>dev&&toggle<credits&&settings.Contains("prefs.devAbuse=abuse;Save();"),"Settings · Developer shows the toggle inside the verified-dev block and saves it on change");
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
            ("chip-cast","arena.SandboxCastChip(devChipId)"),("chip-cycle","arena.SandboxCastEveryChip()"),("splat-delay","arena.SandboxSplatDelay=Mathf.Round(Mathf.Clamp(devSplat,1,30))"),("preview-reveal","DevRevealStart(arena.Local.Seat.id,arena.Local.Seat.name,true)"),
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
            var abuse=Ctx(online:true,abuse:true);var sandbox=Ctx(sandbox:true);var linked=Ctx(online:true,abuse:true,authority:true);
            foreach(var a in NativeDevActions.All)
            {
                var s=NativeDevActions.Resolve(a,abuse,out string why);
                if(a.Kind==NativeDevKind.Contract)Check(s==NativeDevState.NeedsAuthority&&why=="Needs authority (Astra): not implemented",a.Id+" waits for Astra's authority outside the sandbox");
                else if(a.Kind==NativeDevKind.Presentation)Check(s==NativeDevState.Live,a.Id+" (a view tool) is live in DEV Abuse");
                else Check(s==NativeDevState.SandboxOnly&&why==NativeDevActions.SandboxOnly,a.Id+" stays sandbox only");
                var t=NativeDevActions.Resolve(a,sandbox,out why);
                Check(a.Kind==NativeDevKind.Contract&&!a.SandboxRoute?t==NativeDevState.NeedsAuthority:t==NativeDevState.Live,a.Id+" in the sandbox: "+(a.SandboxRoute?"its sandbox call":"needs authority"));
                var u=NativeDevActions.Resolve(a,linked,out why);Check(a.Kind==NativeDevKind.Contract?u==NativeDevState.Live&&why==NativeDevActions.ToHost:u==NativeDevActions.Resolve(a,abuse,out _),a.Id+" once the authority link exists");
                Check(NativeDevActions.Resolve(a,Ctx(dev:false,online:true,abuse:true),out why)==NativeDevState.Unavailable,a.Id+" never for a non-dev");
                if(a.Kind==NativeDevKind.Contract)Check(NativeDevContract.Known(a.Contract),a.Id+" sends the contract action "+a.Contract);
            }
            foreach(var name in NativeDevContract.Personal.Concat(NativeDevContract.Room).Concat(NativeDevContract.Interact))Check(NativeDevActions.All.Any(a=>a.Contract==name),"the panel has a button for "+name);
            Check(NativeDevContract.Command=="dev-command"&&NativeDevContract.Reply=="dev-reply"&&NativeDevContract.Gate=="DevAllowed"&&NativeDevContract.RevealedField=="devRevealed"&&new[]{NativeDevContract.Accepted,NativeDevContract.Refused,NativeDevContract.Done}.SequenceEqual(new[]{"accepted","refused","done"}),"the wire names");
            // The link: nothing without a sender; a reply alone says done.
            var link=new NativeDevLink();Check(!link.Available&&link.Send("restore",1,null,"Restore me")==null&&link.Status==""&&link.Pending==0,"no authority link: nothing is sent and no status claims success");
            var sent=new List<NativeDevCommand>();link.Sender=c=>{sent.Add(c);return true;};
            var cmd=link.Send("launch",4,new[]{"20"},"Launch");Check(cmd!=null&&cmd.action=="launch"&&cmd.target==4&&cmd.args.SequenceEqual(new[]{"20"})&&cmd.transaction==1&&sent.Count==1&&link.Status.Contains("waiting")&&!link.Status.Contains("done"),"a send waits for the host");
            Check(link.Send("noclip",1,null,"x")==null&&link.Send("launch",1,new[]{"1","2","3","4","5"},"x")==null&&link.Send("launch",1,new[]{new string('x',33)},"x")==null&&sent.Count==1,"unknown actions and oversized arguments are never sent");
            Check(link.Receive(new NativeDevReply{transaction=1,state="accepted"})&&link.Status.Contains("accepted")&&link.Pending==1,"accepted keeps the transaction");
            Check(link.Receive(new NativeDevReply{transaction=1,state="done"})&&link.Status=="Launch · done"&&link.Pending==0&&!link.StatusRefused,"done only on the host's reply");
            var r2=link.Send("freeze",4,new[]{"3"},"Freeze");Check(link.Receive(new NativeDevReply{transaction=r2.transaction,state="refused",reason="rate limited"})&&link.Status=="Freeze · refused: rate limited"&&link.StatusRefused,"a refusal shows its reason");
            Check(!link.Receive(new NativeDevReply{transaction=99,state="done"})&&link.Status=="Freeze · refused: rate limited","an unknown transaction changes nothing");
            var panel=R("Runtime/NativeDevPanel.cs");
            Check(panel.Contains("if(state!=NativeDevState.Live){DevSay((a?.Label??id)+\" · \"+reason,true);return;}")&&!Regex.IsMatch(panel,"DevSay\\([^;]*(\"done|NativeDevContract\\.Done|accepted)"),"a disabled action only says why; the panel never reports a command done itself");
            Check(Regex.Matches(panel,@"devLink\.Send\(").Count==2&&panel.Contains("if(devLink.Status!=devLinkSeen){devLinkSeen=devLink.Status;"),"commands go out only through the link; the status line shows the link's");
            Check(R("Runtime/NativeDevOverlays.cs").IndexOf("devLink",StringComparison.Ordinal)<0&&!Regex.IsMatch(panel+R("Runtime/NativeDevOverlays.cs"),@"devRevealed\s*="),"the presentation never sets devRevealed or sends from the overlays");
        }
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
            Check(game.Contains("arena.Pve?.Holding!=true&&!DevHoldsPilot;")&&game.Contains("bool look=!BoardOpen&&DevLook(")&&panel.Contains("private bool DevHoldsPilot=>devFly.On||devOpen&&(devCursorFree||devTyping);"),"flying or using the panel's cursor sends the neutral input (the pilot holds still)");
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
            var seat=new NativeSeat{id=3,name="Sheldon"};Check(!NativeDevTags.Revealed(seat)&&!NativeDevTags.Revealed(null),"dormant: nobody is tagged until devRevealed exists");
            var field=typeof(NativeSeat).GetField(NativeDevContract.RevealedField);
            if(field!=null){field.SetValue(seat,true);Check(NativeDevTags.Revealed(seat),"NativeSeat.devRevealed exists: NativeDevTags.Revealed must read it");field.SetValue(seat,false);Check(!NativeDevTags.Revealed(seat),"and follow it back");}
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
            Check(NativeDevLayout.TabNames.Length==9&&NativeDevLayout.TabTitles.Length==9&&NativeDevLayout.TabIcons.Length==9&&Enum.GetValues(typeof(NativeDevTab)).Length==9,"nine tabs");
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json")));var icons=new HashSet<string>(doc.RootElement.GetProperty("icons").EnumerateArray().Select(e=>e.GetProperty("id").GetString()));
            var art=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ClassIcons.json"))).RootElement;var artIcons=new HashSet<string>(art.GetProperty("icons").EnumerateArray().Select(e=>e.GetProperty("id").GetString()));
            foreach(var id in NativeDevLayout.TabIcons.Concat(new[]{"x","scan","bot","sparkles"}))Check(icons.Contains(id)||artIcons.Contains(id),"icon "+id+" exists");
            foreach(NativeDevTab t in Enum.GetValues(typeof(NativeDevTab)))Check(NativeDevActions.All.Any(a=>a.Tab==t),"tab "+t+" has actions");
            Check(NativeDevActions.All.Select(a=>a.Id).Distinct().Count()==NativeDevActions.All.Length&&NativeDevActions.All.All(a=>NativeDevActions.Find(a.Id)==a)&&NativeDevActions.Find("nope")==null,"action ids are unique and found");
            Check(NativeDevActions.All.Where(a=>a.Key!=null).All(a=>NativeDevKeys.All.Contains(a.Key)),"every action key is a panel hotkey");
        }
        private static void Allocations()
        {
            var contexts=new[]{Ctx(sandbox:true),Ctx(abuse:true),Ctx(online:true,hosting:true,abuse:true),Ctx(online:true,abuse:true,authority:true),Ctx(dev:false,abuse:true)};
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
                    var p=NativeDevLayout.Panel(1600+frame,900);sink+=(int)(p.BodyX+p.BodyY+p.BodyW+p.BodyH);
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
            string panel=R("Runtime/NativeDevPanel.cs"),overlays=R("Runtime/NativeDevOverlays.cs"),core=R("Core/NativeDevTools.cs"),both=panel+overlays;
            Check(!core.Contains("UnityEngine"),"the DEV rules are engine-free Core");
            Check(!both.Contains("GUILayout")&&!both.Contains("using System.Linq")&&!both.Contains("$\"")&&!Regex.IsMatch(both,@"(?<!Mathf|Math|NativeDevText)\.(Where|Select|ToList|Any|First|FirstOrDefault|OrderBy|Count|Sum|Max|Min)\("),"kit-drawn, no LINQ or interpolation in the panel and overlays");
            Check(!Regex.IsMatch(both,@"0x[0-9a-fA-F]{6}|U\.Hex\(|NativeUi\.Hex\(|new Color\("),"colours only from the cyber palette (and the content's rarity colours)");
            Check(panel.Contains("ColorUtility.TryParseHtmlString(r.color,out var col)"),"rarity colours from the content");
            // Hooks (one line each in the shared files).
            var game=R("Runtime/ConversionGame.cs");
            Check(game.Contains("CameraKeys(typing);DevUpdate(typing);")&&game.Contains("if(DevScreenshot())return;")&&game.Contains("if(arena!=null)DevGui(width,height);\n            if(!menu&&arena!=null){ReplayHud(width,height);PveHud(width,height);return;}"),"ConversionGame calls the panel's update, screenshot mode and GUI");
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
            foreach(var f in new[]{"Core/NativeDevTools.cs","Runtime/NativeDevPanel.cs","Runtime/NativeDevOverlays.cs"})
            {
                var meta=Path.Combine(root,"Assets/ChipArena",f+".meta");Check(File.Exists(meta),f+" has a meta");var g=Regex.Match(File.ReadAllText(meta),@"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),f+" has its own guid");
            }
            int clash=0;foreach(var meta in Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories)){var m=Regex.Match(File.ReadAllText(meta),@"guid: ([0-9a-f]{32})");if(m.Success&&guids.Contains(m.Groups[1].Value)&&!meta.Contains("NativeDev"))clash++;}
            Check(clash==0,"the guids are fresh");
        }
    }
}
#endif
