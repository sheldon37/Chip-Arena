#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Runs in real Unity/PhysX on the Windows builder. The runtime feature gate does not bypass this fixture.
        public void ValidateZoneBossAuthority(Action<NativeGameFrame> replicaCheck=null)
        {
            if(!NativeZoneCombat.Active(MapId))return;
            void Check(bool ok,string why){if(!ok)throw new Exception("Native zone boss "+MapId+": "+why);}
            foreach(var old in Minions.ToArray())RemoveMinion(old,false);ResetZoneBoss();
            var p=Pilots.First(x=>!x.Seat.AuthorityBoss);Pve.Holding=false;Pve.Stage="boss";Pve.Rate=0;Pve.MaxHp=6000;Pve.Boss.Hp=6000;
            p.Hp=1000;p.Stats["hp"]=1000;p.Stats["damageTaken"]=1;p.Seat.connected=true;p.DevGod=false;p.Roll=null;p.Down=null;p.Death=null;p.BlockEnd=p.ShieldUntil=p.ScareUntil=0;p.Collider.enabled=true;p.Body.isKinematic=false;
            Match.SynchronizeExpeditionClock(Math.Max(Time+1,200));
            SpawnZoneBoss(NativeZoneBossRules.Archetype(MapId),new Vector3(1000,100,1000),0);var m=zoneBossActor;var z=ZoneBoss;
            Check(m!=null&&m.Pve==null&&!Pve.Boss.Seat.connected&&!Pve.Boss.Collider.enabled,"separate minion actor, reserved pilot has no collision or AI");
            Check(m.Body.GetComponentsInChildren<Collider>().Length>=6,"compound body and parts");
            Check(z.archetype==NativeZoneBossRules.Station?StationZoneBossView().live&&!IronworksZoneBossView().live:IronworksZoneBossView().live&&!StationZoneBossView().live,"only matching actor presentation binds");
            var arrival=NetworkFrame();Check(arrival.Valid(Content),"authority arrival frame valid before codec");
            var arrivalWire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=arrival})).frame;
            Check(arrivalWire.minions.Single(x=>x.id==m.Id).addKind==null,"boss stays a non-horde actor through Unity codec");
            if(!arrivalWire.Valid(Content))Debug.Log("Zone boss codec diagnostic: record="+JsonUtility.ToJson(arrivalWire.pve.zoneBoss)+", actor="+JsonUtility.ToJson(arrivalWire.minions.Single(x=>x.id==m.Id)));
            Check(arrivalWire.Valid(Content),"Unity codec accepts arrival with absent stagger");
            var idle=NetworkFrame();idle.pve.zoneBoss.action=null;idle.pve.zoneBoss.actionAt=idle.pve.zoneBoss.hitAt=-100;idle.pve.zoneBoss.duration=0;
            var idleWire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=idle})).frame;
            Check(idleWire.Valid(Content),"Unity codec accepts idle with absent action and stagger");replicaCheck?.Invoke(idleWire);
            Pve.Holding=true;Check(HitMinion(m,p,100,Vector3.forward,0)==0,"intro cannot damage boss");Pve.Holding=false;
            p.Body.position=m.Body.position+new Vector3(0,.9f,2);p.Body.transform.position=p.Body.position;Physics.SyncTransforms();
            string attack=z.archetype==NativeZoneBossRules.Station?"buffer":"stomp";BossStart(attack,p);float hit=z.hitAt;
            Match.SynchronizeExpeditionClock(hit-.01f);StepZoneBoss();Check(p.Hp==1000,"windup harmless");
            Match.SynchronizeExpeditionClock(hit+.01f);StepZoneBoss();Check(p.Hp<1000,"strike damages at authority contact time");float after=p.Hp;
            Match.SynchronizeExpeditionClock(hit+.02f);StepZoneBoss();Check(p.Hp==after,"one hit per swing");
            float hp=m.Hp;HitMinion(m,p,100,Vector3.forward,0,m.Body.position+new Vector3(0,2.8f,-5),"projectile");Check(m.Hp<hp&&Pve.Boss.Hp==m.Hp,"real weapon path updates boss health");
            m.Hp=m.MaxHp*.6f;StepZoneBoss();Check(z.phase==2&&!Pve.Holding&&z.phaseAt==Time,"phase changes without Siren reveal hold");
            var wire=NativeFrameCodec.Clone(NetworkFrame());Check(wire.Valid(Content)&&wire.pve.zoneBoss?.actor==m.Id,"finite actor and unified record round trip");replicaCheck?.Invoke(wire);
            wire.pve.zoneBoss.hitAt=float.NaN;Check(!wire.Valid(Content),"nonfinite action time rejected");
            z.holdTarget=p.Seat.id;z.holdUntil=Time+1;BossStagger("part",.8f);Check(z.holdTarget<0&&z.holdUntil==Time&&z.cancelAt==Time,"stagger cancels move and releases victim");
            m.Hp=0;StepZoneBoss();Check(z.deathAt==Time&&m.Body.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"death releases collision and has authority time");
            RemoveMinion(m,false);ResetZoneBoss();
            Debug.Log("PASS native zone boss "+MapId+": separate actor, intro guard, windup/contact/single-hit, damage route, phase transition, wire/guest and death. Automated fixture.");
        }
    }
}
#endif
