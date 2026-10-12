#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The effects sound bank (Core/NativeSoundBank.cs, Core/NativeSoundCues.cs, Runtime/NativeEffectsAudio.cs). The licensed
    // clips are never in this repository: the owner's builder copies them to Assets/LocalAssets (tools/install_local_assets.py).
    // Always checked: the key contract and its length caps; the manifest schema (valid and broken manifests); the fallback with
    // LocalAssets absent and with empty keys, sound for sound against the parity router over every Tests/event-feedback-parity
    // scenario and a sweep of kinds and flags; the seeded non-repeating variation bag; every key the code names exists; the
    // voice pool's caps (8 gated, 4 reserved, high steals low, 3 loops); text scans that every hook point goes through the
    // bank and that no bank audio sits under a tracked folder. With LocalAssets present: its manifest validates, names only
    // known keys, covers every key the code names, and every clip exists with a .meta, fits its cap and the 12 MB budget.
    internal static class EffectsAudioChecks
    {
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Effects audio: "+what);}
        private static readonly Regex KeyLiteral=new Regex("\"((?:gun|magic|melee|dev|blast|break|chip|death|field|status|hit|impact|hazard|move|ui|pve|siren|structure)\\.[a-z0-9.-]+)\"",RegexOptions.Compiled);
        public static IEnumerable<string> Run(string root)
        {
            var passed=new List<string>();
            // Repeated GUI repaints/recreated layouts must not retrigger a stationary highlight. A real focus change,
            // duplicate-chip slot, leave/re-enter or new draft still gives one cue (mouse and keyboard share this gate).
            var focus=new NativeHoverFocus();int hoverPlays=0;
            for(int frame=0;frame<10000;frame++)if(focus.Step(2,new string(new[]{'c','h','i','p'}),true))hoverPlays++;
            Check(hoverPlays==1,"stationary chip is silent after its first highlight over 10,000 repaints");
            Check(focus.Step(3,"chip",true)&&!focus.Step(3,"chip",true),"another slot with the same chip cues once");
            Check(focus.Step(3,"another-chip",true),"replaced offer in the same slot cues once");
            Check(!focus.Step(-1,null,true)&&focus.Step(3,"another-chip",true),"leave and re-enter cues once");
            Check(!focus.Step(3,"another-chip",false)&&!focus.Step(3,"another-chip",false)&&focus.Step(3,"another-chip",true),"disabled/spectator tray is silent");
            focus.Reset();Check(focus.Step(3,"another-chip",true),"new draft reset permits its first highlight");
            foreach(bool chip in new[]{false,true})
            {
                var pcm=NativeHoverSound.Samples(chip);double duration=pcm.Length/(double)NativeHoverSound.SampleRate;
                Check(duration>0&&duration<=.07&&duration<NativeHoverSound.Cooldown,"hover ends before rapid navigation can overlap it");
                Check(pcm.SequenceEqual(NativeHoverSound.Samples(chip)),"identical hover samples on every play");
                Check(pcm[0]==0&&pcm[pcm.Length-1]==0&&pcm.All(s=>!float.IsNaN(s)&&!float.IsInfinity(s)&&Math.Abs(s)<1)&&pcm.Any(s=>Math.Abs(s)>.1),"finite audible hover with silent ends and no clipping");
            }
            passed.Add("hover audio: 10,000 stationary repaints cue once; slot/offer/keyboard edges, disabled/re-entry/reset, fixed 40/70 ms ticks and overlap budget pass");
            // ---------- key contract ----------
            var keys=NativeSoundKeys.All;Check(keys.Length==192&&keys.Select(k=>k.Key).Distinct().Count()==keys.Length&&keys.Count(k=>NativeSoundKeys.IsIntro(k.Key))==19,"173 effect keys and 19 entrance-music keys (the Sheldon Intro's included), unique ("+keys.Length+")");
            foreach(var k in keys)
            {
                Check(Regex.IsMatch(k.Key,"^[a-z]+(\\.[a-z0-9-]+)+$")&&NativeSoundKeys.Known(k.Key),k.Key+" name");
                Check(k.Cap>0&&k.Cap<=(k.Space==NativeSoundSpace.Loop?6:5)+1e-6,k.Key+" hard length cap "+k.Cap);
                if(k.Key.StartsWith("ui.",StringComparison.Ordinal))Check(k.Space==NativeSoundSpace.Flat&&k.Priority==NativeSoundPriority.High,k.Key+" UI is 2D, high priority");
                if(k.Key.StartsWith("impact.",StringComparison.Ordinal)||k.Key.StartsWith("move.step.",StringComparison.Ordinal)||k.Key.EndsWith("-tick",StringComparison.Ordinal))Check(k.Priority==NativeSoundPriority.Low,k.Key+" frequent detail is low priority");
            }
            // The plan's voice-budget caps for the busiest cues.
            float Cap(string key){Check(NativeSoundKeys.Find(key,out var d),key+" known");return d.Cap;}
            Check(Cap("gun.machinegun.shot")<=.32f&&Cap("melee.daggers.swing")<=.4f&&Cap("impact.stone")<=.45f&&Cap("move.step.concrete")<=.35f&&Cap("status.burn-tick")<=.35f&&Cap("blast.leet-nuke")<=5&&Cap("blast.transient")<=.1f,"high-rate cues keep the plan's short caps");
            Check(!NativeSoundKeys.Known("amb.brickfall.city")&&!NativeSoundKeys.Known(null)&&!NativeSoundKeys.Known("gun.pistol"),"unknown keys");
            passed.Add("effects audio: "+keys.Length+" bank keys with space, priority and hard length caps (MG .32 s, impacts .45 s, steps .35 s, nuke 5 s, loops 6 s)");

            // ---------- manifest schema ----------
            const string good="{\"schema\":1,\"note\":\"x\",\"events\":{\"gun.pistol.shot\":{\"clips\":[\"0\",\"1\",\"2\"],\"gain\":0.8,\"rate\":1,\"jitter\":0.04,\"priority\":\"normal\",\"space\":\"3d\",\"maxLength\":0.6,\"cooldown\":0},"+
                "\"ui.click\":{\"clips\":[\"a\"],\"space\":\"2d\"},\"field.acid\":{\"clips\":[\"0\"],\"space\":\"loop\"},\"hit.confirm\":{\"clips\":[]}}}";
            var bank=NativeSoundBank.Parse(good,7);
            Check(bank.Errors.Count==0&&bank.Count==4,"a valid manifest parses: "+string.Join("; ",bank.Errors));
            var pistol=bank.Entry("gun.pistol.shot");Check(pistol.Count==3&&Math.Abs(pistol.Gain-.8f)<1e-6&&Math.Abs(pistol.Jitter-.04f)<1e-6&&pistol.Positional&&!pistol.Loop&&Math.Abs(pistol.MaxLength-.6f)<1e-6&&pistol.Path(1)=="EffectsAudio/gun.pistol.shot/1","entry fields and resource path");
            var click=bank.Entry("ui.click");Check(!click.Positional&&click.Priority==NativeSoundPriority.High&&Math.Abs(click.MaxLength-.25f)<1e-6&&click.Gain==1&&click.Rate==1,"registry defaults fill absent fields");
            Check(bank.Entry("field.acid").Loop&&bank.Has("field.acid")&&!bank.Has("hit.confirm")&&bank.Entry("hit.confirm")!=null&&!bank.Has("gun.rifle.shot"),"empty clip lists and absent keys have no clips");
            string Broken(string events)=>"{\"schema\":1,\"events\":{"+events+"}}";
            var broken=new (string json,string why)[]{
                (Broken("\"amb.brickfall.city\":{\"clips\":[\"0\"]}"),"unknown key"),(Broken("\"gun.pistol.shot\":{\"clips\":[\"Bad Name\"]}"),"clip name"),
                (Broken("\"gun.pistol.shot\":{\"clips\":[\"0\",\"0\"]}"),"duplicate clip"),(Broken("\"gun.pistol.shot\":{\"clips\":[\"0\"],\"gain\":9}"),"gain range"),
                (Broken("\"gun.pistol.shot\":{\"clips\":[\"0\"],\"maxLength\":0.9}"),"maxLength over the cap"),(Broken("\"gun.pistol.shot\":{\"clips\":[\"0\"],\"space\":\"2d\"}"),"space mismatch"),
                (Broken("\"gun.pistol.shot\":{\"clips\":[\"0\"],\"volume\":1}"),"unknown field"),(Broken("\"gun.pistol.shot\":{\"clips\":[\"0\"],\"priority\":\"urgent\"}"),"priority"),
                (Broken("\"gun.pistol.shot\":{\"clips\":\"0\"}"),"clips array"),(Broken("\"gun.pistol.shot\":{\"clips\":[\"0\"],\"jitter\":\"x\"}"),"jitter type"),
                (Broken("\"gun.pistol.shot\":{\"clips\":["+string.Join(",",Enumerable.Range(0,17).Select(i=>"\""+i+"\""))+"]}"),"too many clips")};
            foreach(var (json,why) in broken){var b=NativeSoundBank.Parse(json);Check(b.Errors.Count>0&&!b.Has("gun.pistol.shot")&&!b.Has("amb.brickfall.city"),"rejects "+why);}
            var mixed=NativeSoundBank.Parse(Broken("\"gun.pistol.shot\":{\"clips\":[\"0\"],\"gain\":9},\"gun.rifle.shot\":{\"clips\":[\"0\"]}"));Check(mixed.Errors.Count==1&&mixed.Has("gun.rifle.shot")&&!mixed.Has("gun.pistol.shot"),"a bad entry drops alone");
            foreach(var junk in new[]{"","   ",null,"{","[1,2]","{\"schema\":2,\"events\":{}}","{\"schema\":1}","{\"schema\":1,\"events\":[]}","{\"schema\":1,\"events\":{},\"x\":1}","{\"schema\":1,\"events\":{}} trailing",
                "{\"schema\":1,\"schema\":1,\"events\":{}}",new string('[',40)+new string(']',40),"{\"schema\":1,\"events\":{\"gun.pistol.shot\":{\"clips\":[\"\\u00\"]}}}"})
            {NativeSoundBank b=null;try{b=NativeSoundBank.Parse(junk);}catch(Exception x){Check(false,"malformed manifest threw "+x.GetType().Name);}Check(b.Count==0,"malformed manifest gives an empty bank: "+junk);}
            Check(NativeSoundJson.Parse("{\"a\":[1,-2.5e1,true,false,null,\"\\u0041\\n\"]}") is Dictionary<string,object> top&&top["a"] is List<object> values&&(double)values[1]==-25&&(string)values[5]=="A\n","JSON reader values");
            passed.Add("effects audio: manifest schema (fields, registry defaults, empty keys) and "+broken.Length+" broken entries plus 13 malformed manifests rejected without throwing; a bad entry drops alone");

            // ---------- fallback: LocalAssets absent, empty keys ----------
            // The runtime reads Resources "EffectsAudio/manifest", which only the ignored LocalAssets folder can provide.
            NativeSoundBank Load(string project){string f=Path.Combine(project,NativeSoundKeys.LocalRoot,"manifest.json");return NativeSoundBank.Parse(File.Exists(f)?File.ReadAllText(f):null);}
            string local=Path.Combine(root,NativeSoundKeys.LocalRoot,"manifest.json");bool present=File.Exists(local);
            var absent=Load(Path.Combine(Path.GetTempPath(),"chip-arena-no-local-assets-"+Guid.NewGuid().ToString("N")));Check(absent.Count==0&&absent.Errors.Count==0,"LocalAssets absent: an empty bank");
            if(!present)Check(Load(root).Count==0,"this checkout has no LocalAssets bank");
            var emptyKeys=NativeSoundBank.Parse("{\"schema\":1,\"events\":{"+string.Join(",",keys.Select(k=>"\""+k.Key+"\":{\"clips\":[]}"))+"}}");
            Check(emptyKeys.Errors.Count==0&&emptyKeys.Count==keys.Length&&keys.All(k=>!emptyKeys.Has(k.Key)),"every key present with no clips: "+string.Join("; ",emptyKeys.Errors.Take(3)));
            int compared=0;var routed=new NativeEventSounds.Sound[NativeEventSounds.MaxSounds];var plan=new NativeSoundPlay[NativeSoundPlan.MaxPlays];
            void Same(in NativeEventSounds.Context c,in NativeSoundDetail d,string what)
            {
                int n=NativeEventSounds.Route(c,routed);
                foreach(var b in new[]{absent,emptyKeys})
                {
                    int m=NativeSoundPlan.Event(b,c,d,routed,n,plan);Check(m==n,what+": "+m+" plays for "+n+" routed");
                    for(int i=0;i<n;i++)Check(plan[i].Key==null&&plan[i].Clip==routed[i].Clip&&plan[i].Gain==routed[i].Gain&&plan[i].Rate==routed[i].Rate&&plan[i].Gate==routed[i].Gate&&plan[i].Positional==routed[i].Positional&&plan[i].Tone==routed[i].Tone&&plan[i].Delay==0,what+" sound "+i+" is the routed "+routed[i].Clip);
                }
                compared++;
            }
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/event-feedback-parity.json"))))
                foreach(var scenario in doc.RootElement.GetProperty("sounds").EnumerateArray())foreach(var u in scenario.GetProperty("unity").EnumerateArray())
                {
                    string Text(string f)=>u.TryGetProperty(f,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;bool Flag(string f)=>u.TryGetProperty(f,out var v)&&v.ValueKind==JsonValueKind.True;float Num(string f)=>u.TryGetProperty(f,out var v)&&v.ValueKind==JsonValueKind.Number?v.GetSingle():0;
                    var c=new NativeEventSounds.Context{Kind=(EventKind)Enum.Parse(typeof(EventKind),Text("Kind")),Detail=Text("Detail"),SourceSpec=Text("SourceSpec"),Material=Text("Material"),Amount=Num("Amount"),Radius=Num("Radius"),TargetMe=Flag("TargetMe"),SourceMe=Flag("SourceMe"),TargetPilot=Flag("TargetPilot"),Ally=Flag("Ally"),Pve=Flag("Pve"),Supernova=Flag("Supernova")};
                    Same(c,new NativeSoundDetail{WeaponDetail=Flag("Weapon"),KillStyle="confetti"},scenario.GetProperty("name").GetString());
                }
            int parity=compared;
            string[] details={null,"","debris","static","blast","rocket","meteor-strike","geyser-wall","orbital-mark","ball-lightning","shackles","flame-lance","tidal-wave","leet-nuke","chip","fireworks","frost","void","quake","saw","lava","blink-strike","wormhole","panic-button","thunderstep","leet-lock","reveal","mishima-x","arcade-y","swipe","slam","eruption","supernova","block-pulse"};
            string[] specs={null,"pistol","rifle","machinegun","fire","water","lightning","darkness","sword","greatsword","daggers","shield","emperor","ansatsuken","leet","siren"};
            string[] materials={null,"wood","stone","metal","foundation","glass","brick","timber"};
            foreach(EventKind kind in Enum.GetValues(typeof(EventKind)))for(int flags=0;flags<64;flags+=5)foreach(var detail in details)
            {
                string spec=specs[(flags+detail?.Length??0)%specs.Length],material=materials[flags%materials.Length];
                var c=new NativeEventSounds.Context{Kind=kind,Detail=detail,SourceSpec=spec,Material=material,Amount=flags%3==0?.04f:12,Radius=flags%4==0?.3f:flags%4==1?3:4,TargetMe=(flags&1)!=0,SourceMe=(flags&2)!=0,TargetPilot=(flags&4)!=0,Ally=(flags&8)!=0,Pve=(flags&16)!=0,Supernova=(flags&32)!=0};
                Same(c,new NativeSoundDetail{WeaponDetail=(flags&8)!=0,KillStyle=flags%2==0?"ragdoll-frost":"confetti"},kind+" "+detail+" "+spec);
            }
            passed.Add("effects audio: with LocalAssets "+(present?"present, an empty bank still":"absent")+" and with every key empty, the bank plan is the parity route sound for sound ("+parity+" parity scenarios, "+(compared-parity)+" swept contexts); no extra layer plays");

            // A filled bank: keys replace their routed clip, covers drop the layer their recording holds, recipes and extras join.
            var filled=NativeSoundBank.Parse("{\"schema\":1,\"events\":{"+string.Join(",",new[]{"gun.pistol.shot","blast.fire","blast.transient","blast.debris","magic.geyser","blast.leet-nuke","hit.headshot","hit.kill-confirm","melee.sword.hit","hit.body","status.frost-shatter","status.freeze","impact.glass","melee.shield.block","siren.slam"}.Select(k=>"\""+k+"\":{\"clips\":[\"0\",\"1\"]}"))+"}}");
            Check(filled.Errors.Count==0,"filled bank: "+string.Join("; ",filled.Errors));
            NativeSoundPlay[] Plan(NativeEventSounds.Context c,NativeSoundDetail d=default){int n=NativeEventSounds.Route(c,routed);int m=NativeSoundPlan.Event(filled,c,d,routed,n,plan);return plan.Take(m).ToArray();}
            var shot=Plan(new NativeEventSounds.Context{Kind=EventKind.Shot,SourceSpec="pistol",SourceMe=true});Check(shot.Length==1&&shot[0].Key=="gun.pistol.shot"&&shot[0].Clip=="pistol"&&shot[0].Gain==.65f&&shot[0].Priority==NativeSoundPriority.High,"my pistol shot plays the bank, high priority, keeping its source clip as the load fallback");
            var other=Plan(new NativeEventSounds.Context{Kind=EventKind.Shot,SourceSpec="pistol"});Check(other[0].Priority==NativeSoundPriority.Normal,"another pilot's shot is normal priority");
            var rifle=Plan(new NativeEventSounds.Context{Kind=EventKind.Shot,SourceSpec="rifle"});Check(rifle.Length==1&&rifle[0].Key==null&&rifle[0].Clip=="rifle","a key without clips keeps today's clip");
            var blast=Plan(new NativeEventSounds.Context{Kind=EventKind.Explosion,Detail="sticky-bomb",Radius=3});
            Check(blast.Select(p=>p.Key).SequenceEqual(new[]{"blast.fire","blast.transient","blast.debris"})&&blast[2].Delay>.1f&&Math.Abs(blast[2].Gain-.65f*.4f)<1e-5,"a fire-style blast: body + transient + debris tail at +.12 s");
            var geyser=Plan(new NativeEventSounds.Context{Kind=EventKind.Explosion,Detail="geyser-wall",Radius=3});Check(geyser.Length==1&&geyser[0].Key=="magic.geyser","the geyser's eruption covers its synth explosion layer");
            var nuke=Plan(new NativeEventSounds.Context{Kind=EventKind.Explosion,Detail="leet-nuke",Radius=11});Check(nuke.Length==1&&nuke[0].Key=="blast.leet-nuke"&&nuke[0].Gain==1,"the nuke plays once, its default layer covered");
            var head=Plan(new NativeEventSounds.Context{Kind=EventKind.Headshot,SourceSpec="pistol",SourceMe=true,TargetPilot=true,Amount=20});Check(head.Length==2&&head[0].Clip=="hit"&&head[0].Key==null&&head[1].Key=="hit.headshot"&&!head[1].Positional,"my headshot: the hitmarker plus the 2D dink");
            var third=Plan(new NativeEventSounds.Context{Kind=EventKind.Hit,SourceSpec="sword",TargetPilot=true,Amount=20});Check(third.Length==1&&third[0].Key=="melee.sword.hit"&&third[0].Positional,"a third-party sword hit is heard positionally");
            var gun3=Plan(new NativeEventSounds.Context{Kind=EventKind.Hit,SourceSpec="rifle",TargetPilot=true,Amount=20});Check(gun3.Length==1&&gun3[0].Key=="hit.body","a third-party gun hit plays the body thud");
            var mine=Plan(new NativeEventSounds.Context{Kind=EventKind.Hit,SourceSpec="rifle",SourceMe=true,TargetPilot=true,Amount=20});Check(mine.Length==1&&mine[0].Clip=="hit","my gun hit keeps only the hitmarker");
            var proc=Plan(new NativeEventSounds.Context{Kind=EventKind.Hit,SourceSpec="sword",TargetPilot=true,Amount=20,Detail="burn"});Check(proc.Length==0,"proc damage gets no weapon impact");
            var kill=Plan(new NativeEventSounds.Context{Kind=EventKind.Death,SourceMe=true,TargetPilot=true},new NativeSoundDetail{KillStyle="ragdoll-frost"});Check(kill.Count(p=>p.Key=="hit.kill-confirm")==1&&kill.Count(p=>p.Key=="status.frost-shatter")==1&&kill.Count(p=>p.Clip=="stone")==1,"my kill: confirm + frost shatter on today's death layers");
            var freeze=Plan(new NativeEventSounds.Context{Kind=EventKind.Freeze,Amount=1.2f});Check(freeze.Length==2&&freeze[1].Key=="status.frost-shatter"&&Math.Abs(freeze[1].Delay-1.2f)<1e-6,"a freeze shatters when it ends");
            var glass=Plan(new NativeEventSounds.Context{Kind=EventKind.Impact,Material="glass"});Check(glass.Length==1&&glass[0].Key=="impact.glass"&&glass[0].Gate=="impact","glass impacts take their own key, keeping the impact gate");
            var shieldBlock=Plan(new NativeEventSounds.Context{Kind=EventKind.Block,SourceSpec="shield"});var parry=Plan(new NativeEventSounds.Context{Kind=EventKind.Block,SourceSpec="ansatsuken"});Check(shieldBlock[0].Key=="melee.shield.block"&&parry[0].Clip=="arcade-parry"&&parry[0].Key==null,"blocks by blocker spec; the arcade parry stays");
            var slam=Plan(new NativeEventSounds.Context{Kind=EventKind.Siren,Detail="slam"});Check(slam.Length==1&&slam[0].Key=="siren.slam"&&slam[0].AtSource,"a Siren move cue plays at the Siren");
            Check(NativeSoundCues.Style(NativeBlastSim.StyleOf("frost"))=="blast.frost"&&NativeSoundCues.Style(NativeBlastSim.StyleOf("vacuum"))=="blast.void"&&NativeSoundCues.Style(NativeBlastSim.StyleOf("implosion-bomb"))=="blast.implosion"&&NativeSoundCues.Style(NativeBlastSim.StyleOf("thunder"))=="blast.shock"&&Enum.GetValues(typeof(NativeBlastStyle)).Cast<NativeBlastStyle>().Select(NativeSoundCues.Style).Distinct().Count()==11,"blast keys follow the visual style (11 styles, 11 keys)");
            int dummy=NativeEventSounds.Route(new NativeEventSounds.Context{Kind=EventKind.Explosion,Detail="rocket",Radius=.3f},routed);Check(NativeSoundCues.Pick(new NativeEventSounds.Context{Kind=EventKind.Explosion,Detail="rocket",Radius=.3f},default,routed[0],0,out _)=="gun.rocket.dud","an unarmed rocket clanks");
            Check(NativeSoundCues.Structure("structure/groan")=="structure.groan"&&NativeSoundCues.Structure("wood")=="structure.land.timber"&&NativeSoundCues.Structure("dash")=="structure.whoosh"&&NativeStructureSounds.Synthesized.All(s=>NativeSoundKeys.Known(NativeSoundCues.Structure("structure/"+s))),"Brickfall's 11 synthesized layers and 3 borrowed clips have override keys");
            passed.Add("effects audio: a filled bank replaces routed clips (keeping them as the load fallback), covers the layers its recordings hold, builds the per-style blast recipe and adds the headshot, kill, impact, freeze/shatter and Siren hooks");

            // ---------- seeded, non-repeating variation ----------
            for(int n=1;n<=8;n++)
            {
                string Json(int count)=>"{\"schema\":1,\"events\":{\"impact.stone\":{\"clips\":["+string.Join(",",Enumerable.Range(0,count).Select(i=>"\""+i+"\""))+"]}}}";
                var a=NativeSoundBank.Parse(Json(n),42);var b=NativeSoundBank.Parse(Json(n),42);var c=NativeSoundBank.Parse(Json(n),43);
                var sa=Enumerable.Range(0,600).Select(_=>a.Next("impact.stone")).ToArray();var sb=Enumerable.Range(0,600).Select(_=>b.Next("impact.stone")).ToArray();var sc=Enumerable.Range(0,600).Select(_=>c.Next("impact.stone")).ToArray();
                Check(sa.SequenceEqual(sb),"same seed, same sequence ("+n+")");Check(sa.All(v=>v>=0&&v<n),"in range ("+n+")");
                if(n>=2){for(int i=1;i<sa.Length;i++)Check(sa[i]!=sa[i-1],"no immediate repeat ("+n+" clips, draw "+i+")");for(int r=0;r+n<=sa.Length;r+=n)Check(sa.Skip(r).Take(n).Distinct().Count()==n,"each bag round plays every clip once ("+n+")");}
                if(n>=3)Check(!sa.SequenceEqual(sc),"another seed shuffles differently ("+n+")");
            }
            Check(NativeSoundBank.Parse(null).Next("impact.stone")==0,"no entry: variation 0");
            var timed=NativeSoundBank.Parse("{\"schema\":1,\"events\":{\"pve.money\":{\"clips\":[\"0\"],\"cooldown\":0.12}}}");
            Check(timed.Admit("pve.money",1)&&!timed.Admit("pve.money",1.05)&&timed.Admit("pve.money",1.13)&&timed.Admit("pve.money",.5)&&!timed.Admit("ui.click",1),"dedup window per key; a clock rewind admits");
            var jit=NativeSoundBank.Parse("{\"schema\":1,\"events\":{\"impact.stone\":{\"clips\":[\"0\"],\"jitter\":0.06,\"rate\":1.1}}}").Entry("impact.stone");
            Check(Math.Abs(NativeSoundBank.Rate(jit,0)-1.1f*.94f)<1e-5&&Math.Abs(NativeSoundBank.Rate(jit,1)-1.1f*1.06f)<1e-5&&Math.Abs(NativeSoundBank.Rate(jit,.5f,1.6f)-1.76f)<1e-5,"rate = base x jitter x the hook's scale");
            passed.Add("effects audio: seeded shuffle bags never repeat a clip back to back and play every clip once per round (1-8 clips); per-key dedup window; rate jitter");

            // ---------- every key the code names ----------
            var named=new SortedSet<string>(StringComparer.Ordinal);
            foreach(var file in Directory.EnumerateFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories))
            {if(Path.GetFileName(file)=="NativeSoundBank.cs")continue;foreach(Match m in KeyLiteral.Matches(File.ReadAllText(file)))named.Add(m.Groups[1].Value);}
            var unknown=named.Where(k=>!NativeSoundKeys.Known(k)).ToList();Check(unknown.Count==0,"code names unknown keys: "+string.Join(", ",unknown));
            var unhooked=keys.Select(k=>k.Key).Where(k=>!named.Contains(k)&&!NativeSoundKeys.IsIntro(k)).ToList();Check(named.Count>=150,"hooked keys "+named.Count);
            passed.Add("effects audio: all "+named.Count+" bank keys named in code exist in the key contract; not hooked yet: "+string.Join(", ",unhooked));

            // ---------- voice budget ----------
            {
                var rng=new Random(5);var busy=new bool[NativeVoicePool.Voices];var keyed=new bool[busy.Length];var held=new NativeSoundPriority[busy.Length];int steals=0,lowRefused=0,keyRefused=0;
                for(int step=0;step<20000;step++)
                {
                    if(rng.NextDouble()<.45){int v=rng.Next(busy.Length);busy[v]=false;keyed[v]=false;held[v]=NativeSoundPriority.Normal;}
                    var p=(NativeSoundPriority)rng.Next(3);bool gated=rng.NextDouble()<.4;int free=busy.Count(b=>!b),keyedNow=Enumerable.Range(0,busy.Length).Count(i=>busy[i]&&keyed[i]);
                    int pick=NativeVoicePool.Pick(busy,keyed,held,gated,p,out bool steal);
                    if(gated&&keyedNow>=NativeVoicePool.KeyedCap){Check(pick<0,"the 8 gated voices cap holds");keyRefused++;continue;}
                    if(p==NativeSoundPriority.Low&&free<=NativeVoicePool.Reserve){Check(pick<0,"a low-priority sound never takes the last 4 free voices");lowRefused++;continue;}
                    if(pick<0){Check(free==0&&(p!=NativeSoundPriority.High||held.Where((h,i)=>busy[i]).All(h=>h!=NativeSoundPriority.Low)),"a voice was available");continue;}
                    if(steal){Check(busy[pick]&&held[pick]==NativeSoundPriority.Low&&p==NativeSoundPriority.High&&free==0,"only a high sound steals, only a low voice");steals++;}else Check(!busy[pick],"a free voice");
                    busy[pick]=true;keyed[pick]=gated;held[pick]=p;
                    Check(Enumerable.Range(0,busy.Length).Count(i=>busy[i]&&keyed[i])<=NativeVoicePool.KeyedCap&&busy.Length==24,"never more than 8 gated of 24 voices");
                }
                Check(steals>0&&lowRefused>0&&keyRefused>0,"the simulation reached every cap ("+steals+" steals, "+lowRefused+" low refusals, "+keyRefused+" gate refusals)");
                string fx=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeEffectsAudio.cs"));
                Check(!fx.Contains("AddComponent<AudioSource>")&&fx.Contains("new LoopSlot[NativeVoicePool.MaxLoops]")&&NativeVoicePool.MaxLoops==3&&fx.Contains("int v=BankVoice(false,NativeSoundPriority.Low);")&&fx.Contains("NativeVoicePool.Pick(busyScratch,keyedVoices,heldScratch,gated,priority,out bool steal)"),"loops and one-shots share the 24 voices through the pool, at most 3 loops, no new AudioSource");
                Check(fx.Contains("gain=(float)limiter.Cue(gate,Time.unscaledTime,gain);")&&fx.Contains("if(!bank.Admit(key,Time.unscaledTimeAsDouble))return true;"),"bank plays keep the limiter gate and their dedup window");
                passed.Add("effects audio: voice pool over 20000 random plays: at most 8 gated of 24, 4 voices reserved from low priority, high steals only low, at most 3 field loops; no new AudioSource");
            }

            // ---------- length caps ----------
            {
                string fx=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeEffectsAudio.cs")),st=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeStructureAudio.cs"));
                Check(fx.Contains("Math.Min(clip.length-startAt,e.MaxLength)/rate")&&fx.Contains("if(now>=bankVoiceEnd[v]){if(voices[v].isPlaying)voices[v].Stop();")&&fx.Contains("FlushUi();BankCaps();"),"every bank one-shot stops at its entry's hard cap");
                Check(st.Contains("float seconds=banked!=null?Math.Min(clip.length,banked.MaxLength):clip.length;")&&st.Contains("if(!structureMix.Fits(start,start+NativeStructureSounds.Length(seconds,rate)))continue;"),"structure overrides count their capped length in the six-voice ledger");
                passed.Add("effects audio: hard length caps enforced at play (bank voices and structure overrides) and checked against every clip when LocalAssets is present");
            }

            // ---------- hook points route through the bank ----------
            {
                string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
                string audio=R("NativeAudio.cs"),scene=R("NativeSceneAudio.cs"),st=R("NativeStructureAudio.cs"),ui=R("NativeUi.cs"),fx=R("NativeEffectsAudio.cs");
                foreach(var hook in new[]{"int plays=NativeSoundPlan.Event(bank,context,detail,routed,n,planned);","for(int i=0;i<plays;i++)Perform(planned[i],pos,from,arena.Time-e.Time,context.SourceMe||context.TargetMe);",
                    "Cue(NativeSoundCues.Roll(roll),roll,pos,.45f,mine:mine);","Cue(NativeSoundCues.Swing(p.Seat.spec,swing),swing,pos,.4f,cover:NativeSoundCues.SwingCover(p.Seat.spec),mine:mine);",
                    "Cue(NativeSoundCues.Cast(ability,p.Seat.spec,out string covered),clip,pos,.6f,cover:covered,mine:mine);","Cue(NativeSoundCues.Fighter(beat),beat,","Cue(NativeSoundCues.Fighter(hit),hit,pos,.9f,mine:mine);",
                    "Cue(\"hazard.spikes\",s.Clip,","Cue(\"ui.countdown\",\"countdown\",null,.7f);","Cue(\"ui.go\",\"go\",null,.7f);","Cue(result==\"win\"?\"ui.round-won\":\"ui.round-lost\",result,null,.65f);",
                    "Cue(\"pve.wave\",\"countdown\",null,.6f);","Cue(\"pve.clear\",\"go\",null,.6f);","Cue(\"siren.reveal\",\"go\",null,.5f);","Cue(\"ui.draft.plant\",null,null,.6f,mine:true);",
                    "NativeUi.Sound+=UiHeard;","NativeUi.Sound-=UiHeard;","StructureQueue(arena);EffectsFrame(arena,menu);","foreach(var v in voices){v.Stop();v.loop=false;}"})Check(audio.Contains(hook),"NativeAudio hook: "+hook);
                foreach(var hook in new[]{"Cue(\"death.broadcast\",\"explosion\",null,.7f);","Cue(\"hazard.entry\",\"metal\",null,.18f);","Cue(\"ui.error\",\"ui\",null,.3f,mine:true);"})Check(scene.Contains(hook),"scene hook: "+hook);
                Check(st.Contains("var clip=BankStructureClip(layer.Clip,out var banked);if(clip==null)clip=StructureClip(layer.Clip);if(clip==null)continue;"),"structure layers take the bank override, else the synth");
                Check(ui.Contains("public static bool Button(Rect r,bool enabled=true)=>Pressed(r,enabled,NativeUiSounds.Click);")&&ui.Contains("return Pressed(r,enabled,alpha>.5f?NativeUiSounds.Button(label,primary):null)&&alpha>.5f;")&&ui.Contains("Sound?.Invoke(NativeUiSounds.Error);")&&ui.Contains("Sound?.Invoke(NativeUiSounds.Hover);")&&ui.Contains("if(hit&&enabled&&sound!=null)Sound?.Invoke(sound);"),"U.Button / U.Btn raise click, back, confirm, error and hover once");
                Check(R("NativeLoadoutUI.cs").Contains("nativeAudio?.UiCue(\"ui.loadout.select\",\"install\",.4f);")&&R("NativeDraftScreen.cs").Contains("nativeAudio.UiCue(\"ui.draft.pick\",\"ui\",.4f);")&&R("NativeDraftScreen.cs").Contains("nativeAudio?.UiCue(\"ui.draft.hover\",null,.5f);")&&R("NativeHomeMenu.cs").Contains("nativeAudio?.UiCue(\"ui.click\",\"ui\",.4f);")&&R("NativeNotice.cs").Contains("nativeAudio?.Cue(\"ui.notice\",null,null,.5f,mine:true);"),"screen UI cues use the shared audio entrypoints (hover has fixed built-in ticks)");
                Check(fx.Contains("if(key!=null&&BankPlay(key,position,gain,0,gate,mine))return;")&&fx.Contains("if(cover!=null&&bank.Has(cover))return;")&&fx.Contains("if(clip!=null)Play(clip,position,gain,rate,gate);"),"Cue falls back to exactly today's Play");
                Check(fx.Contains("if(s.Key==null){if(s.Tone)ArcadeTone();else Play(s.Clip,s.Positional?at:(Vector3?)null,s.Gain,s.Rate,s.Gate);return;}"),"an unbanked plan entry is today's play");
                Check(fx.Contains("FieldBorn(NativeFieldSounds.Born(f.Kind,fx),")&&fx.Contains("BankPlay(\"chip.mine.arm\",at,.6f);")&&fx.Contains("FuseBeeps(f.Id,at,f.Born,f.Until,t);")&&fx.Contains("WantLoop((long)f.Id*2,loop,at);")&&fx.Contains("WantLoop((long)z.Id*2+1,loop,z.Position);")&&fx.Contains("BankPlay(\"move.jump\",")&&fx.Contains("BankPlay(\"move.land\",")&&fx.Contains("BankPlay(\"gun.ads\",null,.5f,0,null,true);"),"field, fuse, loop and movement hooks play only through the bank");
                Check(fx.Contains("if(arena==null||menu||bank.Count==0){FadeLoops(false,null);return;}"),"no hook runs in menus or without a bank");
                // Only the documented direct plays remain in NativeAudio: the Slender scare and the DEV kill clips (timed to their clips).
                int direct=Regex.Matches(audio+scene,"[^a-zA-Z.]Play\\(\"").Count;Check(direct==4,"direct source-clip plays left in NativeAudio: "+direct);
                passed.Add("effects audio: text scans: events, state cues, scene cues, structure layers, UI helpers and screens, fields, fuses, loops and movement all route through the bank, falling back to today's Play");
                // Beeps and mines follow the plan's fuse schedule.
                Check(NativeFieldSounds.Beep(10,11.2,0,out double b0,out float r0)&&Math.Abs(b0-10.1)<1e-9&&r0>=1&&!NativeFieldSounds.Beep(10,10.2,1,out _,out _),"fuse beeps start at born + .1 s and stop before the detonation");
                double last=0,gap=1;float lastRate=0;for(int i=0;NativeFieldSounds.Beep(0,1.2,i,out double at,out float rate);i++){Check(at>last-1e-9&&(i<2||at-last<=gap+1e-9)&&rate>=lastRate&&rate<=1.6f+1e-6,"beeps accelerate and rise");if(i>0)gap=at-last;last=at;lastRate=rate;}
                Check(NativeFieldSounds.Born("time",null)=="field.time"&&NativeFieldSounds.Born("bomb","sticky-bomb")=="chip.bomb.attach"&&NativeFieldSounds.Born("bomb","delayed-gratification")=="chip.delayed.charge"&&NativeFieldSounds.Loop("acid",null)=="field.acid"&&NativeFieldSounds.Loop("implosion","singularity")=="magic.singularity"&&NativeFieldSounds.Loop("bomb","sticky-bomb")==null,"field cues by kind and chip");
                Check(Math.Abs(NativeFieldSounds.SweepRate(2,2.2f)-2/2.2f)<1e-6&&Math.Abs(NativeFieldSounds.StartOffset(2.5f,.9f)-1.6f)<1e-5&&NativeFieldSounds.StartOffset(1,3)==0,"warning sweeps and the meteor whistle end on the detonation");
                Check(NativeUiSounds.Button("Back to customization",false)==NativeUiSounds.Back&&NativeUiSounds.Button("×",true)==NativeUiSounds.Back&&NativeUiSounds.Button("Start match",true)==NativeUiSounds.Confirm&&NativeUiSounds.Button("Settings",false)==NativeUiSounds.Click,"UI button sounds by label");
            }

            // ---------- no licensed audio in the repository; LocalAssets when present ----------
            {
                string assets=Path.Combine(root,"Assets");
                var stray=Directory.EnumerateFileSystemEntries(assets,"*",SearchOption.AllDirectories).Select(p=>Path.GetRelativePath(root,p).Replace('\\','/'))
                    .Where(p=>!p.StartsWith("Assets/LocalAssets/",StringComparison.Ordinal)&&p!="Assets/LocalAssets"&&p!="Assets/LocalAssets.meta"&&(p.EndsWith(".ogg",StringComparison.OrdinalIgnoreCase)||p.Split('/').Any(part=>part=="EffectsAudio"||part=="EffectsAudio.meta"))).ToList();
                Check(stray.Count==0,"bank audio outside the ignored Assets/LocalAssets: "+string.Join(", ",stray.Take(5)));
                string ignore=File.ReadAllText(Path.Combine(root,".gitignore"));Check(ignore.Contains("/Assets/LocalAssets/")&&ignore.Contains("/Assets/LocalAssets.meta"),"Assets/LocalAssets is git-ignored");
                Check(File.ReadAllText(Path.Combine(root,"tools/package_source.py")).Contains("'LocalAssets'"),"the source package skips LocalAssets");
                if(!present)passed.Add("effects audio: no bank audio or EffectsAudio folder under tracked Assets; LocalAssets absent, so the game uses today's sounds");
                else
                {
                    string folder=Path.GetDirectoryName(local);var installed=NativeSoundBank.Parse(File.ReadAllText(local));
                    Check(installed.Errors.Count==0,"LocalAssets manifest: "+string.Join("; ",installed.Errors.Take(5)));
                    var missing=named.Where(k=>installed.Entry(k)==null).ToList();Check(missing.Count==0,"LocalAssets manifest lacks keys the code names: "+string.Join(", ",missing));
                    long bytes=Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length);Check(bytes<NativeSoundKeys.BudgetBytes,"EffectsAudio "+bytes+" bytes over the 12 MB budget");
                    int clips=0;double seconds=0;var listed=new HashSet<string>(StringComparer.Ordinal);
                    foreach(var e in installed.Entries)for(int i=0;i<e.Count;i++)
                    {
                        string file=Path.Combine(folder,e.Key,e.Clips[i]+".ogg");listed.Add(Path.GetFullPath(file));Check(File.Exists(file)&&File.Exists(file+".meta"),e.Key+"/"+e.Clips[i]+".ogg and its .meta");
                        Check(OggInfo(file,out double length,out int channels,out int rate),e.Key+"/"+e.Clips[i]+" is Ogg Vorbis");
                        Check(length<=e.MaxLength+.02&&length>0,e.Key+"/"+e.Clips[i]+" "+length.ToString("0.000")+" s over its "+e.MaxLength+" s cap");
                        Check(rate==44100&&(e.Positional?channels==1:channels<=2),e.Key+"/"+e.Clips[i]+" format "+channels+" ch "+rate+" Hz");
                        clips++;seconds+=length;
                    }
                    var extra=Directory.EnumerateFiles(folder,"*.ogg",SearchOption.AllDirectories).Select(Path.GetFullPath).Where(f=>!listed.Contains(f)).ToList();Check(extra.Count==0,"unlisted clips: "+string.Join(", ",extra.Take(3)));
                    foreach(var dir in Directory.EnumerateDirectories(folder,"*",SearchOption.AllDirectories))Check(File.Exists(dir+".meta"),"folder meta "+dir);
                    passed.Add("effects audio: LocalAssets present: "+installed.Count+" keys, "+clips+" clips, "+seconds.ToString("0")+" s, "+(bytes/1e6).ToString("0.00")+" MB < 12 MB; every clip has a .meta, fits its cap, 44.1 kHz, mono when positional; covers all "+named.Count+" named keys");
                }
            }
            return passed;
        }
        /// <summary>Length (the last page's granule position), channels and rate of an Ogg Vorbis file.</summary>
        public static bool OggInfo(string path,out double seconds,out int channels,out int rate)
        {
            seconds=0;channels=0;rate=0;var b=File.ReadAllBytes(path);if(b.Length<64||b[0]!='O'||b[1]!='g'||b[2]!='g'||b[3]!='S')return false;
            int segments=b[26],body=27+segments;if(body+16>b.Length||b[body]!=1||System.Text.Encoding.ASCII.GetString(b,body+1,6)!="vorbis")return false;
            channels=b[body+11];rate=BitConverter.ToInt32(b,body+12);
            // Walk the pages to the last one; its granule position is the stream's length in samples.
            int at=0;long granule=0;
            while(at+27<=b.Length&&b[at]=='O'&&b[at+1]=='g'&&b[at+2]=='g'&&b[at+3]=='S')
            {int count=b[at+26];if(at+27+count>b.Length)return false;int size=27+count;for(int k=0;k<count;k++)size+=b[at+27+k];granule=BitConverter.ToInt64(b,at+6);at+=size;}
            seconds=rate>0?granule/(double)rate:0;return at==b.Length&&rate>0&&granule>0;
        }
    }
}
#endif
