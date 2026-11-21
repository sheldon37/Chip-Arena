#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateBackroomsEntities(Action<NativeGameFrame> replicaCheck)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Native Backrooms entities: "+why);}
            foreach(var old in Minions.ToArray())RemoveMinion(old,false);
            Pve.Wave=4;Pve.Cap=40;Pve.Holding=false;Pve.Stage="wave";Pve.Rate=0;
            var p=Pilots.First(x=>!x.Seat.AuthorityBoss);p.Hp=1000;p.Stats["hp"]=1000;p.Stats["damageTaken"]=1;p.Seat.connected=true;p.DevGod=false;p.Roll=null;p.BlockEnd=p.ShieldUntil=p.ScareUntil=0;p.Down=null;p.Death=null;p.Seat.alive=true;p.Body.isKinematic=false;p.Collider.enabled=true;
            if(NativeBackroomsKinds.Level0(MapId))
            {
                Match.SynchronizeExpeditionClock(Math.Max(100,Time+5));
                var routes=NativeLevel0Spawns.Routes(EntityDarkLayout,Pilots.Where(ZonePilot).Select(x=>Vec(x.Body.position)));
                var far=Pve.Content.entries.OrderByDescending(e=>NativeLevel0Spawns.Distance(EntityDarkLayout,routes,e.@exit.Position())).FirstOrDefault(e=>e.allowed.Contains("faceling")&&NativeLevel0Spawns.Distance(EntityDarkLayout,routes,e.@exit.Position())>60&&!EntityObserved(Vec(e.@exit.Position(.85f)))&&WardLandingClear(e));
                Check(far!=null,"distant dormant fixture exists");var sleeper=SpawnPveAdd("faceling",true,far.e);Check(sleeper!=null,"distant dormant fixture spawns");
                sleeper.Pve.ActiveAt=Time-30;int deaths=MinionDeaths.Count;int progress=Expedition.Session.Run.Snapshot().instances.Single(i=>i.id==NativeBackroomsGenerator.AreaId).progress;
                nextLevel0Refresh=0;RefreshLevel0Population();Check(!Minions.Contains(sleeper),"unseen distant sleeper releases population slot");
                Check(MinionDeaths.Count==deaths&&Expedition.Session.Run.Snapshot().instances.Single(i=>i.id==NativeBackroomsGenerator.AreaId).progress==progress,"recycling grants no death or quota credit");
                var local=SpawnPveAdd("faceling",true);Check(local!=null,"normal forced Expedition spawn replenishes near players");
                float localDistance=NativeLevel0Spawns.Distance(EntityDarkLayout,routes,Vec(local.Body.position));
                Check(localDistance<NativeLevel0Spawns.Distance(EntityDarkLayout,routes,far.@exit.Position()),"forced spawn prefers nearby corridor over distant dormant anchor");RemoveMinion(local,false);
            }
            foreach(var kind in NativeBackroomsKinds.For(MapId))
            {
                Match.SynchronizeExpeditionClock(Math.Max(100,Time+5));
                var m=SpawnPveAdd(kind.Id,true);Check(m!=null,"anchor spawns "+kind.Id);var a=m.Pve;var z=a.Zone;
                Check(Math.Abs(m.Collider.height-kind.Height)<.001f,"capsule "+kind.Id);
                var spawn=Vec(a.Entry.@exit.Position(m.Collider.height/2));
                if(kind.Id=="smiler")Check(NativeBackroomsKinds.Dark(EntityDarkLayout,Vec(spawn)),"dark spawn");
                if(kind.Id=="drowned")Check(NativePoolroomsWater.Depth(Poolrooms,Vec(spawn-Vector3.up*m.Collider.height/2))>.8f,"deep spawn");
                a.ActiveAt=a.ExposedAt=a.SpawnAt=Time;a.DamageScale=1;a.Struck=true;a.StaggerAt=a.RecoverAt=0;a.Target=p;a.ZoneReady=Time;a.ZoneCallReady=Time+20;
                m.Body.isKinematic=false;m.Collider.enabled=true;m.Body.position=new Vector3(1000,100,1000);m.Body.transform.position=m.Body.position;m.Yaw=0;
                p.Body.position=m.Body.position+Vector3.forward*1.5f;p.Body.transform.position=p.Body.position;p.Hp=1000;Physics.SyncTransforms();
                if(z.dormant){Check(StepZoneEnemy(m,p)&&!z.dormant&&z.wakeAt==Time,"dormant wake "+kind.Id);Check(p.Hp==1000,"wake is harmless");}
                z.dormant=false;a.ZoneStrike=-100;a.ZoneBusy=a.RecoverAt=0;a.ZoneReady=Time;
                float start=Time;
                if(kind.Id=="hound")
                {
                    // Exercise the contact phase without replacing path/collision acceptance with a fake leap.
                    ZoneStart(m,"pounce",.85f,5);Match.SynchronizeExpeditionClock(start+.84f);StepZoneEnemy(m,p);Check(p.Hp==1000,"pounce windup harmless");
                    Match.SynchronizeExpeditionClock(start+.86f);StepZoneEnemy(m,p);Check(z.grabTarget==p.Seat.id&&z.releaseAt>Time+.89f,"pin clock");
                    ZoneIncoming(m,p,40,Vector3.back,null,false,0,"direct");Check(z.grabTarget<0,"victim breaks pin");
                }
                if(kind.Id=="smiler"||kind.Id=="drowned"||kind.Id=="lifeguard")
                {
                    StepZoneEnemy(m,p);Check(z.attack=="lunge"||z.attack=="sweep","special starts "+kind.Id);
                    Check(p.Hp==1000,"telegraph harmless");Match.SynchronizeExpeditionClock(a.ZoneStrike+.01f);StepZoneEnemy(m,p);Check(p.Hp<1000,"special contact "+kind.Id);
                }
                z.blinkAt=Time;z.blinkX=10;z.blinkY=1;z.blinkZ=20;z.dreadUntil=Time+1.5f;
                var frame=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=NetworkFrame()})).frame;
                Check(frame!=null&&frame.Valid(Content),"real frame codec "+kind.Id);var wire=frame.minions.Single(n=>n.id==m.Id);
                Check(wire.addKind==kind.Id&&wire.zone!=null&&wire.zone.blinkZ==20&&wire.zone.dreadUntil==z.dreadUntil&&wire.zone.dormant==z.dormant,"all new state survives JsonUtility");replicaCheck?.Invoke(frame);
                a.ZoneStrike=Time+1;z.pullTarget=p.Seat.id;z.grabTarget=p.Seat.id;z.grabAt=Time;z.releaseAt=Time+1;a.StaggerAt=Time+.5f;StepZoneEnemy(m,p);
                Check(a.ZoneStrike<0&&z.grabTarget<0&&z.pullTarget<0,"CC cancels special and releases pilot");RemoveMinion(m,false);
            }
            Debug.Log("PASS native Backrooms entities: anchors, wake tells, contact/pin/break, CC release, real frame codec and replica acceptance");
        }
    }
}
#endif
