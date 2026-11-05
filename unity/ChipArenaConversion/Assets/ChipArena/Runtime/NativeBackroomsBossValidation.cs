#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateBackroomsBossAuthority(Action<NativeGameFrame> replicaCheck)
        {
            string kind=NativeZoneBossRules.Archetype(MapId);if(!NativeBackroomsBossRules.Is(kind))return;
            int checks=0;void Check(bool value,string why){checks++;if(!value)throw new Exception("Native Backrooms boss: "+why);}
            foreach(var old in Minions.ToArray())RemoveMinion(old,false);ResetZoneBoss();
            var p=Pilots.First(a=>!a.Seat.AuthorityBoss);p.Seat.connected=true;p.Seat.alive=true;p.DevGod=false;p.Down=null;p.Death=null;p.Roll=null;p.BlockEnd=p.ShieldUntil=p.ScareUntil=0;p.Stats["hp"]=10000;p.Stats["damageTaken"]=1;p.Hp=10000;
            Pve.Holding=false;Pve.Stage="boss";Pve.Rate=0;Pve.MaxHp=6000;Pve.Boss.Hp=6000;Match.SynchronizeExpeditionClock(Math.Max(Time+1,300));
            SpawnZoneBoss(kind,Vector3.zero,180);var z=ZoneBoss;var b=z.backrooms;var m=zoneBossActor;z.action=null;PoseBackroomBody();
            Check(z.Valid(),"initial record finite");Check(backroomParts.Count>=9,"joint-driven compound hit volumes");
            float half=kind==NativeHumBoss.Archetype?NativeHumArena.Size/2:20;
            var spots=new[]{new Vector3(-half+1,0,-half+1),new Vector3(half-1,0,-half+1),new Vector3(-half+1,0,half-1),new Vector3(half-1,0,half-1),new Vector3(0,0,-half+1),new Vector3(half-1,0,0),new Vector3(0,0,half-1),new Vector3(-half+1,0,0),new Vector3(0,0,-14)};
            foreach(var spot in spots)
            {
                p.Hp=10000;p.StunEnd=0;p.Body.position=new Vector3(b.cx,0,b.cz)+spot+Vector3.up*.85f;p.Body.transform.position=p.Body.position;Physics.SyncTransforms();
                float began=Time;b.next=Time;b.meters=Array.Empty<NativeBossMeter>();b.hits=Array.Empty<int>();z.action=null;z.cancelAt=-100;
                for(int tick=1;tick<=510&&p.Hp>=10000;tick++){Match.SynchronizeExpeditionClock(began+tick/60f);StepZoneBoss();}
                Check(p.Hp<10000,"corner/wall/door/spawn threatened "+spot);
            }
            var packet=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=NetworkFrame()})).frame;
            Check(packet!=null&&packet.Valid(Content),"real Unity codec accepts full state");Check(packet.pve.zoneBoss.backrooms.meters.Length==z.backrooms.meters.Length,"meters survive codec");replicaCheck?.Invoke(packet);
            var copy=z.Copy();copy.backrooms.shiftMask^=1;Check(copy.backrooms.shiftMask!=z.backrooms.shiftMask,"snapshot nested copy isolated");
            m.Hp=m.MaxHp*.64f;StepZoneBoss();Check(z.phase==2&&z.action=="phase","65 percent phase");
            float before=m.Hp;HitZoneBoss(m,p,100,m.Body.position,"bullet");Check(m.Hp==before,"phase transition invulnerable");
            Match.SynchronizeExpeditionClock(Time+4);m.Hp=m.MaxHp*.29f;StepZoneBoss();Check(z.phase==3,"30 percent phase");
            m.Hp=0;StepZoneBoss();Check(z.deathAt>=0&&backroomSeals.All(c=>!c.enabled),"death opens room");
            Debug.Log("PASS native Backrooms boss authority: "+kind+" "+checks+" checks; authority contacts, phase, codec and guest application");
        }
    }
}
#endif
