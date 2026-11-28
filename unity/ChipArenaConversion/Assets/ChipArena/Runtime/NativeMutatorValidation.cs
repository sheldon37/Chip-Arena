#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public static void ValidateMutatorRules(ConversionContent content)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Native mutators: "+why);}
            string json=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text;
            var layout=NativeExpeditionLayout.Parse(json);var area=layout.areas.Single(a=>a.map=="expedition-ward-zero");
            foreach(var active in new[]{new[]{"overclock","moon-boots","paper-pilots"},new[]{"bottomless","adrenaline","vampire"},new[]{"iron-hide","glass-cannon","headhunter"},new[]{"hulkamania","swarm","sprinters"},new[]{"storm","martyr","bloated"}})
            {
                var match=new NativeMatch(content){Cooperative=true,Teams=true,ShowIntros=false};
                match.Seats.Add(new NativeSeat{id=1,name="mutator pilot",spec="pistol",team=0,connected=true});
                match.Seats.Add(new NativeSeat{id=900,name="mutator boss",spec="siren",team=1,bot=true,AuthorityBoss=true,connected=true});
                using(var arena=new NativeArena(content,match,area.map))
                {
                    arena.LocalId=1;arena.Reset();match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=10,round=1,set=1,cooperative=true,teams=true});
                    var run=NativeExpedition.Create("native-mutators",layout.world,"host","pistol",42,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=36}).ToArray(),active);
                    Check(run.Enter(area.id),"enter");var session=new ExpeditionSession(run,content);session.Sources.Seat=_=>1;
                    var director=new ExpeditionDirector(session,layout,_=>Vec(arena.Local.Body.position),(a,b)=>true,100,100,60);
                    arena.BindExpeditionCombat(director,area,JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text));
                    var p=arena.Local;
                    if(arena.Mutator("overclock")){var baseline=NativeContentRules.Stats(content,"pistol",p.Seat.chips);Check(Math.Abs(p.Stats["speed"]-baseline["speed"]*1.33)<.001,"speed");Check(Math.Abs(p.Stats["hp"]-baseline["hp"]*.5)<.001&&p.Hp<=p.Stats["hp"],"health clamp");Check(Math.Abs(p.Stats["playerGravity"]-baseline["playerGravity"]*.5)<.001,"gravity");arena.RefreshLiveStats(p);Check(Math.Abs(p.Stats["speed"]-baseline["speed"]*1.33)<.001,"stat refresh never stacks");}
                    if(arena.Mutator("bottomless")){int ammo=p.Ammo;Check(arena.FireRound(p)&&p.Ammo==ammo,"magazine unchanged");p.Ammo--;arena.Reload(p);Check(p.ReloadEnd==0,"manual reload suppressed");p.Hp=10;arena.MutatorVampire(p,1000);arena.MutatorVampire(p,1000);Check(Math.Abs(p.Hp-25)<.001,"native vampire cap");p.Stamina=0;p.Grounded=true;Check(arena.TryRoll(p)&&p.Stamina==0,"zero-stamina roll");}
                    var add=arena.SpawnPveAdd("shambler",true);Check(add!=null,"spawn exists");
                    if(arena.Mutator("hulkamania"))Check(add.Pve.Stats.id=="hulk"&&arena.Pve.Cap==6,"Hulk conversion and cap");
                    if(arena.Mutator("iron-hide")){Check(Math.Abs(add.MaxHp-add.Pve.Stats.hp*2)<.01,"zombie health doubled");add.Pve.ExposedAt=add.Pve.ActiveAt=0;float before=add.Hp;arena.HitPveAdd(add,p,1,Vector3.zero,0,null,"direct",0,false);Check(Math.Abs(before-add.Hp-3)<.01,"glass damage exactly once");}
                    if(arena.Mutator("storm")){for(int i=0;i<80;i++)run.Tick(.25);arena.StepRunMutators();Check(arena.mutatorStorm.Count==3,"three storm telegraphs");for(int i=0;i<8;i++)run.Tick(.25);arena.StepRunMutators();Check(arena.mutatorStorm.Count==0&&arena.Events.Any(e=>e.Detail=="mutator-storm"),"storm resolves after two seconds");}
                }
            }
            Debug.Log("PASS Expedition native mutator gate: stats, refresh, magazines, vampire, spawn conversion, damage and storm clocks. Rendered VFX and human co-op require playtest.");
        }
    }
}
#endif
