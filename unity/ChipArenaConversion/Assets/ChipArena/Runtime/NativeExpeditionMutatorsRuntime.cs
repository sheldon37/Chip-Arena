using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private string[] boundMutators=Array.Empty<string>();
        public string[] ActiveMutators=>GeneratedRun?.Mutators??boundMutators;
        public bool Mutator(string id)=>NativeExpeditionMutators.Has(ActiveMutators,id);
        private bool PilotMutator(NativePilot p,string id)=>p!=null&&!p.Seat.AuthorityBoss&&Mutator(id);
        private void MutatorHeadSweep(Collider collider,Vector3 center,float headRadius,Vector3 from,Vector3 delta,float projectileRadius,Func<Collider,bool> accepts,ref NativeContact? closest)
        {
            if(!collider.enabled||!accepts(collider))return;float length=delta.magnitude;if(length<.000001f)return;
            var direction=delta/length;var offset=from-center;float radius=headRadius+projectileRadius,b=Vector3.Dot(offset,direction),c=offset.sqrMagnitude-radius*radius,disc=b*b-c;
            if(disc<0)return;float distance=c<=0?0:-b-Mathf.Sqrt(disc);if(distance<0||distance>length||closest.HasValue&&distance>=closest.Value.Distance)return;
            var point=from+direction*distance;closest=new NativeContact{Collider=collider,Distance=distance,Point=point,Contact=point,Normal=(point-center).normalized};
        }
        private void MutatorVampire(NativePilot p,float damage)
        {
            if(!PilotMutator(p,"vampire")||p.Hp<=0||damage<=0)return;
            string account=p.Seat.access?.AccountId;if(string.IsNullOrEmpty(account))account=ExpeditionMemberFor(p)?.account;if(account!=null)Heal(p,Expedition.Session.Run.VampireHeal(account,damage));
        }
        private readonly Queue<(NativePilot source,Vector3 at,float radius,float zombies,float pilots,string effect)> mutatorBlasts=new Queue<(NativePilot,Vector3,float,float,float,string)>();
        private void QueueMutatorBlast(NativePilot source,Vector3 at,float radius,float zombies,float pilots,string effect)
        {if(mutatorBlasts.Count<128)mutatorBlasts.Enqueue((source,at,radius,zombies,pilots,effect));}
        private readonly List<(Vector3 at,double due,NativePilot source)> mutatorStorm=new List<(Vector3,double,NativePilot)>();
        private int stormCycle;private float roulettePoll;
        private void StepRunMutators()
        {
            if(Replica||Expedition==null||ActiveMutators.Length==0||Pve?.Holding==true)return;
            if(!Expedition.Session.Run.CombatRunning){ProcessMutatorBlasts();return;}
            var run=Expedition.Session.Run;double elapsed=run.Elapsed;
            ExpeditionSnapshot state=null;if(Mutator("roulette")&&Time>=roulettePoll){roulettePoll=Time+.25f;state=run.Snapshot();}
            if(Mutator("storm"))
            {
                int cycle=(int)(elapsed/20);if(cycle>stormCycle){var living=Pilots.Where(p=>!p.Seat.AuthorityBoss&&p.Hp>0&&p.Seat.connected&&!p.ExpeditionTransferHeld&&!HubInside(p.Body.position)).ToArray();
                stormCycle=cycle;var rng=new NativePveRandom(NativePveRandom.Hash(run.Seed,MapId,"storm:"+cycle));if(living.Length>0)for(int i=0;i<3;i++){var p=living[(int)(rng.Next()*living.Length)];var at=p.Body.position+new Vector3((float)(rng.Next()*4-2),0,(float)(rng.Next()*4-2));mutatorStorm.Add((at,elapsed+2,p));Emit(EventKind.Impact,p,0,at,2,"mutator-storm-warning",radius:3);}}
                for(int i=mutatorStorm.Count-1;i>=0;i--)if(elapsed>=mutatorStorm[i].due){var s=mutatorStorm[i];QueueMutatorBlast(s.source,s.at,3,60,60,"mutator-storm");mutatorStorm.RemoveAt(i);}
            }
            foreach(var p in Pilots)if(!p.Seat.AuthorityBoss&&p.Hp>0)
            {
                if(Mutator("bottomless")){p.Ammo=Magazine(p);p.ReloadEnd=0;}
                if(state!=null&&Mutator("roulette"))
                {
                    var member=state.members.FirstOrDefault(m=>Expedition.Session.Sources.Seat?.Invoke(m.account)==p.Seat.id);int cycle=(int)(state.elapsed/90);
                    if(member!=null&&cycle>member.rouletteCycle&&!p.ExpeditionTransferHeld&&!ExpeditionChoosing(p))
                    {
                        var pool=Content.weapons.Where(w=>new[]{"pistol","machinegun","rifle","bow","sword","daggers","shield","greatsword","arcane","fire","water","lightning"}.Contains(w.id)).OrderBy(w=>w.id,StringComparer.Ordinal).ToArray();
                        if(pool.Length>0){var rng=new NativePveRandom(NativePveRandom.Hash(state.seed,member.account,"roulette:"+cycle));var weapon=pool[(int)(rng.Next()*pool.Length)];if(run.Roulette(member.account,weapon.id,cycle)){DevCancel(p);p.Fan=null;p.BowDrawAt=-1;p.BowRainAt=-100;p.OverdriveAt=p.OverdriveUntil=p.VentUntil=0;p.Seat.spec=weapon.id;p.Weapon=weapon;p.Pose.Weapon=weapon.id=="pistol"?WeaponId.Pistol:weapon.id=="rifle"?WeaponId.Rifle:weapon.id=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;p.Pose.VisualSet=weapon.id=="daggers"?1:weapon.id=="greatsword"?2:weapon.id=="shield"?3:0;p.Pose.MagicVisual=weapon.id=="fire"?1:weapon.id=="water"?2:weapon.id=="lightning"?3:0;RefreshLiveStats(p);p.Ammo=Magazine(p);p.ReloadEnd=0;Emit(EventKind.Impact,p,p.Seat.id,p.Body.position,cycle,"mutator-roulette");}}
                    }
                }
            }
            ProcessMutatorBlasts();
        }
        private void ProcessMutatorBlasts()
        {
            // Process only the current batch: death blasts cannot recurse or grow the call stack.
            int count=Math.Min(128,mutatorBlasts.Count);while(count-->0)
            {
                var b=mutatorBlasts.Dequeue();Emit(EventKind.Impact,b.source,0,b.at,b.radius,b.effect,radius:b.radius);
                if(b.zombies>0&&b.source!=null)foreach(var m in Minions.ToArray())if(m.Pve!=null&&m.Hp>0&&(m.Body.position-b.at).sqrMagnitude<=b.radius*b.radius&&!SceneLinecast(b.at,m.Body.position,WorldMask))HitMinion(m,b.source,b.zombies,(m.Body.position-b.at).normalized,250,kind:b.effect=="mutator-storm"?"mutator-storm":"blast",radius:b.radius);
                if(b.pilots>0)foreach(var p in Pilots.ToArray())if(p.Hp>0&&!p.ExpeditionTransferHeld&&(b.effect!="mutator-explosive"||p==b.source||p.Seat.AuthorityBoss||Mutator("friendly-fire"))&&(p.Body.position-b.at).sqrMagnitude<=b.radius*b.radius&&!HubInside(p.Body.position)&&!SceneLinecast(b.at,p.Body.position,WorldMask))
                {float damage=b.pilots*(b.effect=="mutator-explosive"&&p==b.source?.5f:1)*(b.effect=="mutator-explosive"&&PilotMutator(b.source,"glass-cannon")?3:1);Damage(p,null,damage,(p.Body.position-b.at).normalized,250,false,direct:false,primary:false,stage:true,damageKind:b.effect);}
            }
        }
    }
}
