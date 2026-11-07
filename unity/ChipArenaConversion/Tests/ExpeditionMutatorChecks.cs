using System;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class ExpeditionMutatorChecks
    {
        private static int checks;
        private static void Check(bool ok,string why){if(!ok)throw new Exception("Mutators: "+why);checks++;}
        private static void Near(double a,double b,string why)=>Check(Math.Abs(a-b)<.0001,why);
        public static void Run(string root)
        {
            checks=0;var options=new JsonSerializerOptions{IncludeFields=true};
            string Read(string name)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name));
            string mapJson=Read("UnityMaps.json"),layoutJson=Read("ExpeditionLayout.json");
            var content=new ConversionContent{maps=JsonSerializer.Deserialize<UnityMapContent>(mapJson,options).maps,contentRevision=NativeMapRules.Revision(mapJson)};
            var layout=NativeExpeditionLayout.Parse(layoutJson);layout.compatibility=NativeExpeditionLayout.Compatibility(layoutJson,content);
            var ids=NativeExpeditionMutators.Ids;Check(ids.Length==30&&ids.OrderBy(x=>x).SequenceEqual(NativeDeployPresentation.MutatorIds.OrderBy(x=>x)),"rules/card parity");
            string live=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDeploymentLive.cs"));
            Check(live.Contains("LiveDeployMutators=NativeExpeditionMutators.Ids")&&live.Contains("Mutators=LiveDeployMutators}")&&live.Contains("action==DeployIntent.RollMutators)RollDeploymentMutators(state.revision)"),"live button supplies catalog and dispatches authority command");
            Check(live.Contains("prefs.reducedEffects,nativeAudio)"),"live mutator roll preserves station sound wiring");
            var controlsState=new ExpeditionDeploymentState{worldRolled=true};
            Check(NativeDeployStage.ControlsOf(controlsState,default,default,NativeDeployPresentation.Launch.Ready,true).Mutate,"host can roll after destination roll");
            Check(!NativeDeployStage.ControlsOf(controlsState,default,default,NativeDeployPresentation.Launch.Ready,false).Mutate,"guest cannot roll host machine");
            controlsState.deployed=true;Check(!NativeDeployStage.ControlsOf(controlsState,default,default,NativeDeployPresentation.Launch.Ready,true).Mutate,"deployed run locks button");
            var seen=new System.Collections.Generic.HashSet<string>();
            for(uint seed=1;seed<=200;seed++)
            {
                var room=new ExpeditionDeploymentRoom("room","host",seed,"black-siren",layout,content);
                Check(!room.Execute("host",0,ExpeditionDeploymentIntent.RollMutators),"roll requires destinations");
                Check(room.Execute("host",0,ExpeditionDeploymentIntent.RollInstances),"destinations");
                var initial=room.Snapshot();
                for(int draw=0;draw<4;draw++)
                {
                    int revision=room.Snapshot().revision;
                    Check(!room.Execute("guest",revision,ExpeditionDeploymentIntent.RollMutators),"guest rejected");
                    Check(room.Execute("host",revision,ExpeditionDeploymentIntent.RollMutators),"live roll enabled");
                    var s=room.Snapshot();Check(s.mutators.Length==3&&NativeExpeditionMutators.Valid(s.mutators),"valid exclusions and uniqueness");foreach(var id in s.mutators)seen.Add(id);
                    Check(s.worldSeed==initial.worldSeed,"independent draw streams");
                    var ticket=JsonSerializer.Deserialize<ExpeditionDeploymentTicket>(JsonSerializer.Serialize(room.Ticket,options),options);
                    var receiver=new ExpeditionDeploymentReceiver();Check(receiver.Accept(ticket,layout,content)&&receiver.Snapshot.mutators.SequenceEqual(s.mutators),"wire reconstructs draw");
                    var promoted=ExpeditionDeploymentRoom.Restore(ticket,"next",layout,content);Check(promoted.Snapshot().mutators.SequenceEqual(s.mutators),"promotion retains effects");
                    var bad=ticket.Copy();bad.mutatorRolls++;Check(!receiver.Accept(bad,layout,content),"forged indices rejected");
                    if(draw%2==0){Check(room.Execute("host",s.revision,ExpeditionDeploymentIntent.SkipMutators),"skip");Check(receiver.Accept(room.Ticket,layout,content)&&receiver.Snapshot.mutators.Length==0,"skip replicated");}
                }
                var final=room.Snapshot();var digest=new string('a',64);Check(room.Execute("host",final.revision,ExpeditionDeploymentIntent.Deploy,true,digest),"deploy");
                Check(!room.Execute("host",room.Snapshot().revision,ExpeditionDeploymentIntent.RollMutators),"immutable run");
                Check(ExpeditionDeploymentRoom.Restore(room.Ticket,"next",layout,content).Snapshot().mutators.SequenceEqual(final.mutators),"locked reconnect");
            }
            Check(seen.Count==30,"all cards reachable");
            foreach(var pair in new[]{new[]{"jackpot","broke"},new[]{"windfall","broke"},new[]{"second-wind","permadeath"},new[]{"darkness","whiteout"}})Check(!NativeExpeditionMutators.Valid(pair),"excluded pair "+pair[0]);
            Near(NativeExpeditionMutators.Payout(new[]{"iron-hide","brittle","permadeath"}),1.75,"three bane payout");
            Near(NativeExpeditionMutators.Money(new[]{"jackpot","friendly-fire","hulkamania"},"hulk"),12.15,"multiplicative money with additive payout");
            Near(NativeExpeditionMutators.Money(Array.Empty<string>(),"hulk"),1,"legacy money");
            Check(NativeExpeditionMutators.CachePrice(new[]{"windfall"},101)==51&&NativeExpeditionMutators.CachePrice(new[]{"broke"},100)==200,"cache price rounding");
            Check(NativeExpeditionMutators.Cap(new[]{"swarm"},60)==72&&NativeExpeditionMutators.Cap(new[]{"hulkamania","swarm"},60)==6,"crowd cost bounds");
            Near(NativeExpeditionMutators.Interval(new[]{"hulkamania","swarm"}),2.5,"hulk interval wins");
            Check(NativeExpeditionMutators.Kind(new[]{"hulkamania"},"rigger")=="hulk"&&NativeExpeditionMutators.Kind(new[]{"sprinters"},"shambler")=="runner","spawn conversion");
            var stats=new ChipStats(new[]{new NumberValue{name="speed",value=1},new NumberValue{name="hp",value=100},new NumberValue{name="playerGravity",value=1},new NumberValue{name="knockbackTaken",value=1}});
            NativeExpeditionMutators.PilotStats(new[]{"overclock","surge","moon-boots"},stats);Near(stats["speed"],1.6625,"speed stack");Near(stats["playerGravity"],.5,"gravity");
            NativeExpeditionMutators.PilotStats(new[]{"paper-pilots","bounce-house"},stats);Near(stats["hp"],50,"paper health");Near(stats["knockbackTaken"],3,"knockback");
            NativeExpedition Run(params string[] active)=>NativeExpedition.Create("run","city","host","pistol",1,new[]{new ExpeditionInstance{id="ward",target=3}},active);
            var free=Run("second-wind");free.Join("guest","sword");free.Defeat("guest");Check(free.Reprint("host","guest",100,true)&&free.Snapshot().members[0].wallet==0,"free reprint at zero wallet");
            var permanent=Run("permadeath");permanent.Join("guest","sword");permanent.Credit("host","fund",500);permanent.Defeat("guest");Check(!permanent.Reprint("host","guest",100,true),"permadeath reprint refused");Check(permanent.Revive("host","guest",50),"body revive retained");
            var vampire=Run("vampire","roulette");vampire.EnableAreas();var city=vampire.ForArea("city");Near(city.VampireHeal("host",1000),15,"vampire capped");Near(city.VampireHeal("host",1000),0,"same second exhausted");
            var restored=new NativeExpedition(vampire.PartySnapshot());Near(restored.VampireHeal("host",1000),0,"snapshot preserves cap");
            for(int i=0;i<4;i++)city.Tick(.25);Near(city.VampireHeal("host",100),8,"next window");
            Check(!city.Roulette("host","bow",1),"roulette early refused");for(int i=0;i<356;i++)city.Tick(.25);Check(city.Roulette("host","bow",1)&&!city.Roulette("host","rifle",1),"roulette once per run interval");
            Check(vampire.PartySnapshot().members[0].weapon=="bow","weapon durable ledger");
            var detached=vampire.Mutators;detached[0]="broke";Check(vampire.HasMutator("vampire"),"ids detached");
            Check(!NativeCombatRules.Headshot(new V3(),0,new V3(.3f,.64f,-2),new V3(0,0,1),0,false)&&NativeCombatRules.Headshot(new V3(),0,new V3(.3f,.64f,-2),new V3(0,0,1),0,false,2),"double head radius");
            Console.WriteLine("PASS Expedition mutators: "+checks+" checks");
        }
    }
}
