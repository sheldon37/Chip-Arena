#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Real-Unity gate: authority hits and finite wire state, after the split-area perf fixture.
        public void ValidateZoneCombat(Action<NativeGameFrame> replicaCheck=null)
        {
            if(!NativeZoneCombat.Active(MapId))return;
            void Check(bool ok,string why){if(!ok)throw new Exception("Native zone combat "+MapId+": "+why);}
            void Clock(float at)=>Match.SynchronizeExpeditionClock(at);
            foreach(var m in Minions.ToArray())RemoveMinion(m,false);
            Pve.Wave=4;Pve.Cap=40;Pve.Holding=false;Pve.Stage="wave";Pve.Rate=0;
            var p=Pilots.First(x=>!x.Seat.AuthorityBoss);p.Hp=1000;p.Stats["hp"]=1000;p.Stats["damageTaken"]=1;p.Seat.connected=true;p.DevGod=false;p.Roll=null;p.BlockEnd=p.ShieldUntil=p.ScareUntil=0;p.Down=null;p.Death=null;p.Seat.alive=true;p.Body.isKinematic=false;p.Collider.enabled=true;
            Clock(Math.Max(100,Time+1));
            var kinds=NativeZoneCombat.Station(MapId)?NativeStationKinds.Kinds:NativeIronworksKinds.Kinds;
            foreach(var kind in kinds)
            {
                var m=SpawnPveAdd(kind.Id,true);Check(m!=null,"authored spawn "+kind.Id);var a=m.Pve;
                Check(Math.Abs(m.Collider.height-kind.Height)<.001f&&Math.Abs(m.Collider.radius-kind.Radius)<.001f,"capsule "+kind.Id);
                m.MaxHp=m.Hp=kind.Proposed.hp;a.ActiveAt=a.ExposedAt=a.SpawnAt=Time;a.DamageScale=1;a.Struck=true;a.StaggerAt=a.RecoverAt=0;a.Target=p;a.ZoneClimbs=2;a.ZoneReady=Time;
                a.Zone.hang=-1;m.Body.isKinematic=false;m.Body.position=new Vector3(1000,100,1000);m.Body.transform.position=m.Body.position;m.Yaw=0;
                p.Body.position=new Vector3(1000,100,1002);p.Body.transform.position=p.Body.position;p.Hp=1000;p.StunEnd=0;p.Knockback=Vector3.zero;Physics.SyncTransforms();float start=Time;
                if(kind.Id=="linesman")
                {Check(StepZoneEnemy(m,p)&&a.Zone.attack=="arc","arc starts");Clock(start+1.09f);StepZoneEnemy(m,p);Check(p.Hp==1000,"arc windup harmless");Clock(start+1.11f);StepZoneEnemy(m,p);Check(p.Hp<1000&&a.Zone.arcTargets.Contains(p.Seat.id)&&p.StunEnd>Time,"arc hit and target event");}
                else if(kind.Id=="undertow")
                {StepZoneEnemy(m,p);Clock(start+.61f);StepZoneEnemy(m,p);Check(a.Zone.grabTarget==p.Seat.id&&Math.Abs(a.Zone.releaseAt-Time-1)<.01f,"solo grab limit");ZoneIncoming(m,p,40,Vector3.back,null,false,0,"direct");Check(a.Zone.grabTarget<0&&a.ZoneReady>=Time+4.99f,"victim breaks grab and cooldown starts");}
                else if(kind.Id=="pileup")
                {StepZoneEnemy(m,p);Clock(start+.71f);StepZoneEnemy(m,p);Check(p.Hp<1000&&p.Knockback.z>=6,"shove hit and knock");Check(Math.Abs(ZoneIncoming(m,p,100,Vector3.back,null,false,0,"direct")-25)<.001f,"front door armour");}
                else if(kind.Id=="conductor")
                {var friend=SpawnPveAdd("shambler",true);Check(friend!=null,"whistle fixture");friend.Body.position=m.Body.position+Vector3.right;StepZoneEnemy(m,p);Clock(start+.81f);StepZoneEnemy(m,p);Check(friend.Pve.Zone.aboardUntil>Time+5.9f,"whistle buffs other add");ZoneIncoming(m,p,40,Vector3.back,m.Body.position,false,0,"direct");Check(a.Zone.lanternBroken,"lantern breaks");RemoveMinion(friend,false);}
                else if(kind.Id=="stoker")
                {p.Body.position=m.Body.position+Vector3.forward*4;p.Body.transform.position=p.Body.position;Physics.SyncTransforms();StepZoneEnemy(m,p);Check(a.Zone.pullAt==Time,"hook windup");ZoneIncoming(m,p,14,Vector3.back,null,false,0,"direct");Clock(start+.9f);StepZoneEnemy(m,p);Check(a.Zone.pullTarget<0,"15 percent interrupt prevents pull");a.ZoneReady=a.RecoverAt=a.StaggerAt=0;Clock(Time+1);StepZoneEnemy(m,p);Clock(Time+.85f);StepZoneEnemy(m,p);Check(a.Zone.pullTarget==p.Seat.id,"uninterrupted hook lands");}
                else if(kind.Id=="slagback")
                {Check(Math.Abs(ZoneIncoming(m,p,100,Vector3.back,null,false,0,"direct")-40)<.001f,"front crust reduction");ZoneIncoming(m,p,20,Vector3.back,null,false,0,"direct");Check(a.Zone.shedAt==Time&&a.StaggerAt>=Time+.79f,"120 damage sheds crust");}
                else if(kind.Id=="ladleman")
                {StepZoneEnemy(m,p);Check(a.Zone.pourAt==Time,"pour windup");Clock(start+1.7f);StepZoneEnemy(m,p);Check(p.Hp<1000,"pool burns inside arc");ZoneIncoming(m,p,200,Vector3.back,m.Body.position+Vector3.up*.6f,false,0,"direct");Check(a.Zone.spilledAt==Time&&a.StaggerAt>=Time+1.99f,"ladle break and stun");}
                else if(kind.Id=="rigger")
                {p.Body.position=m.Body.position+Vector3.forward*.5f;Physics.SyncTransforms();StepRigger(m,p);Clock(start+.61f);StepRigger(m,p);Check(p.Hp==1000,"swing inner metre is safe");a.Zone.hang=a.Entry.e;float amount=HitPveAdd(m,p,40,Vector3.back,0,m.Body.position+Vector3.up*1.1f,"direct",0,false);Check(a.Zone.chainCutAt==Time&&Math.Abs(amount-m.MaxHp*.2f)<.001f,"chain break records fall and self damage");a.Zone.hang=-1;}
                else if(kind.Id=="cinderling")
                {Check(ZoneIncoming(m,p,10,Vector3.back,null,false,0,"fire")==0,"fire immunity");int before=zonePops.Count;ZoneEnemyDeath(m,p,false);Check(zonePops.Count==before,"ranged kill has no pop");ZoneEnemyDeath(m,p,true);Check(zonePops.Count==before+1&&zonePops.Last().when>Time+.44f,"melee kill schedules fuse");zonePops.Clear();}
                var wire=NativeFrameCodec.Clone(NetworkFrame());Check(wire.Valid(Content),"valid event frame "+kind.Id);var record=wire.minions.Single(x=>x.id==m.Id);Check(record.zone!=null&&record.zone.Valid()&&record.zone.hang==a.Zone.hang,"replicated state "+kind.Id);
                replicaCheck?.Invoke(wire);RemoveMinion(m,false);Clock(Time+2);
            }
            ZoneHazards=new NativeZoneHazardFrame{running=true,start=Time,clock=0,trainAt=Time+60};
            var raw=NetworkFrame();Check(raw.Valid(Content),"valid authority hazard frame");var frame=NativeFrameCodec.Clone(raw);Check(frame.Valid(Content),"valid decoded hazard frame");Check(frame.pve?.zone!=null,"hazard state survives codec");float expected=ZoneHazards.trainAt;Check(Math.Abs(frame.pve.zone.trainAt-expected)<.0001f,"hazard clock survives codec: expected "+expected.ToString("R")+", decoded "+frame.pve.zone.trainAt.ToString("R"));
            replicaCheck?.Invoke(frame);frame.pve.zone.clock=float.NaN;Check(!frame.Valid(Content),"invalid hazard clock rejected");
            Debug.Log("PASS native zone combat "+MapId+": authored spawns, capsules, special windups/hits/interrupts, armour and weak points, finite event and hazard frames. Automated fixture.");
        }
    }
}
#endif
