#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateBossBattle(Action<NativeGameFrame> replicaCheck)
        {
            int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception("Direct boss: "+why);}
            string id=NativeBossBattle.Id(MapId);
            Check(Expedition==null&&Pve.Started&&Pve.Holding&&Pve.Cinematic=="intro","direct intro without run");
            Check(ZoneBoss.archetype==NativeZoneBossRules.Archetype(MapId)&&ZoneBoss.Valid(),"selected boss only");
            Check(Pve.Rows.Count==PartySize&&Pve.Rows.Values.All(r=>r.Open==null&&r.Picks.Count==0)&&Pve.Content.machines.Length==0,"no opening draft or shops");
            Check(Pve.MaxHp==NativeBossBattle.Health(id,PartySize)&&(float)Pve.Boss.Stats["damage"]==NativeBossBattle.Damage(PartySize),"party scaling");
            foreach(var p in Members())Check(ExpeditionSupported(Vec(p.Body.position)-new V3(0,NativeLocomotionRules.HalfHeight,0),NativeLocomotionRules.Radius,NativeLocomotionRules.HalfHeight*2),"supported party landing "+p.Seat.id);
            var packet=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=NetworkFrame()})).frame;
            Check(packet.Valid(Content)&&packet.areaIdentity==null,"normal co-op wire frame");replicaCheck(packet);
            PveHoldTick(Pve.HoldDuration+.01f);Match.SynchronizeExpeditionClock(4);StepZoneBoss();
            Check(!Pve.Holding&&Pve.Stage=="boss"&&ZoneBoss.action!="arrive","normal intro released");
            zoneBossActor.Hp=zoneBossActor.MaxHp*.25f;StepZoneBoss();Check(ZoneBoss.phase>=2,"normal phase authority");
            zoneBossActor.Hp=0;StepBossBattle();Check(ZoneBoss.deathAt>=0&&Match.Phase!=NativePhase.Match,"death scene before completion");
            Match.SynchronizeExpeditionClock(Time+8);StepBossBattle();Check(Match.Phase==NativePhase.Match&&Pve.Outcome=="victory","win ends fight");
            Debug.Log("PASS native direct boss: "+id+" / "+PartySize+" pilots / "+n+" checks");
        }
        public void ValidateBossBattleWipe()
        {
            PveHoldTick(Pve.HoldDuration+.01f);Match.SynchronizeExpeditionClock(4);
            foreach(var p in Members()){p.Hp=0;p.Seat.alive=false;}
            StepBossBattle();if(!Pve.Holding||Pve.Cinematic!="defeat")throw new Exception("Direct boss wipe did not end encounter");
            PveHoldTick(4);if(Match.Phase!=NativePhase.Match||Pve.Outcome!="defeat")throw new Exception("Direct boss wipe did not finish");
        }
    }
}
#endif
