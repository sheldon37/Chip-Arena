using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly List<BackroomsBossContact> backroomContacts=new List<BackroomsBossContact>();
        private readonly DeepEndBossRig deepAuthorityRig=new DeepEndBossRig();
        private readonly HumBossRig humAuthorityRig=new HumBossRig();
        // Match the flat floor used by Hum presentation; its gait requires ground on first pose.
        private static readonly BossFlatGround HumAuthorityGround=new BossFlatGround(0);
        private readonly List<Collider> backroomSeals=new List<Collider>();
        private readonly List<Collider> humPartitions=new List<Collider>();
        private readonly List<SphereCollider> backroomParts=new List<SphereCollider>();
        private int backroomMask=-1;private float backroomTick=-1;
        public float BossTide=>NativeBackroomsBossRules.Tide(ZoneBoss,Time);
        public HumBossView HumZoneBossView()
        {var z=ZoneBoss;var m=Minions.FirstOrDefault(a=>a.Id==z?.actor);if(m==null)return HumBossView.None;var p=m.Body.position;return NativeZoneBossViews.Hum(z,p.x,p.y,p.z,m.Yaw,m.Hp,m.MaxHp,BossSongTime>=0?Time-(float)BossSongTime:NativeZoneBossViews.BackroomsArrival(z),Local?.Seat.id??-1);}
        public DeepEndBossView DeepEndZoneBossView()
        {var z=ZoneBoss;var m=Minions.FirstOrDefault(a=>a.Id==z?.actor);if(m==null)return DeepEndBossView.None;var p=m.Body.position;return NativeZoneBossViews.DeepEnd(z,p.x,p.y,p.z,m.Yaw,m.Hp,m.MaxHp,BossSongTime>=0?Time-(float)BossSongTime:NativeZoneBossViews.BackroomsArrival(z),Local?.Seat.id??-1);}
        private NVector BackroomCentre(string kind)
        {
            if(kind==NativeHumBoss.Archetype){var l=GeneratedRun.Backrooms.Layout;int x=Math.Max(0,Math.Min(l.Width-12,l.ExitX-6)),z=Math.Max(0,Math.Min(l.Depth-12,l.ExitZ-6));return new HumRoom(x,z,NVector.Zero).Centre;}
            var g=GeneratedRun.Poolrooms;return NativeDeepEndArena.Centre(g.Layout,NVector.Zero,g.BossCell%g.Layout.Width,g.BossCell/g.Layout.Width);
        }
        private void ResetBackroomBoss()
        {foreach(var c in backroomSeals.Concat(humPartitions))if(c!=null)UnityEngine.Object.Destroy(c.gameObject);backroomSeals.Clear();humPartitions.Clear();backroomParts.Clear();backroomMask=-1;backroomTick=-1;deepAuthorityRig.Clear();}
        private void BackroomBody(NativeMinion m)
        {
            if(m.Body.transform.Find("Boss hull")!=null)return;bool hum=m.Kind==NativeHumBoss.Archetype;
            int count=hum?NativeHumBoss.WeakPoints.Length+2:NativeDeepEndBoss.Parts.Length;
            for(int i=0;i<count;i++)
            {
                var go=new GameObject(i==0?"Boss hull":"Boss part "+i);go.layer=11;go.transform.SetParent(m.Body.transform,false);go.AddComponent<NativeMinionBody>().Minion=m;var c=go.AddComponent<SphereCollider>();c.radius=hum?(i<9?NativeHumBoss.WeakPoints[i].Half.Length():.55f):NativeDeepEndBoss.Parts[i].Radius;backroomParts.Add(c);
                foreach(var p in Pilots)Physics.IgnoreCollision(c,p.Collider);
            }
            m.Collider.enabled=false;
        }
        private void BackroomGeometry()
        {
            var z=ZoneBoss;var b=z?.backrooms;if(b==null)return;bool hum=z.archetype==NativeHumBoss.Archetype;float half=hum?NativeHumArena.Size/2:20,height=hum?4:7.2f;
            if(backroomSeals.Count==0)for(int side=0;side<4;side++)
            {
                var go=new GameObject("Backrooms boss seal");go.layer=PropLayer;go.transform.SetParent(root.transform);bool along=side<2;float sign=side%2==0?-1:1;go.transform.position=new Vector3(b.cx+(along?0:sign*half),height/2,b.cz+(along?sign*half:0));var c=go.AddComponent<BoxCollider>();c.size=new Vector3(along?half*2:.3f,height,along?.3f:half*2);backroomSeals.Add(c);
            }
            foreach(var c in backroomSeals)c.enabled=z.deathAt<0;
            if(hum&&humPartitions.Count==0)for(int i=0;i<8;i++){var go=new GameObject("Hum partition collision");go.layer=PropLayer;go.transform.SetParent(root.transform);humPartitions.Add(go.AddComponent<BoxCollider>());}
            if(hum&&backroomMask!=b.shiftMask){var room=NativeBackroomsBossRules.Room(b);for(int i=0;i<8;i++){room.Partition(i,(b.shiftMask>>i)&1,out var p,out bool along);var c=(BoxCollider)humPartitions[i];c.transform.position=new Vector3(p.X,2,p.Z);c.size=new Vector3(along?6.4f:.16f,4,along?.16f:6.4f);}backroomMask=b.shiftMask;}
        }
        private void PoseBackroomBody()
        {
            var z=ZoneBoss;var m=zoneBossActor;if(z?.backrooms==null||m==null)return;var b=z.backrooms;BackroomGeometry();bool hum=z.archetype==NativeHumBoss.Archetype;float dt=backroomTick<0?0:Mathf.Clamp(Time-backroomTick,0,.1f);backroomTick=Time;
            if(hum)
            {
                var view=HumZoneBossView();humAuthorityRig.Update(Time,dt,view,HumAuthorityGround,NVector.Zero,false,false,NVector.Zero);
                for(int i=0;i<backroomParts.Count;i++){int joint=i<9?NativeHumBoss.WeakPoints[i].Joint:i==9?NativeHumBossLook.Chest:NativeHumBossLook.Pelvis;var p=humAuthorityRig.ToWorld(humAuthorityRig.Shown.Position[joint]);backroomParts[i].transform.position=new Vector3(p.X,p.Y,p.Z);backroomParts[i].enabled=m.Hp>0;}
            }
            else
            {
                var view=DeepEndZoneBossView();view.spawned=NativeZoneBossViews.BackroomsArrival(z);deepAuthorityRig.Update(Time,dt,view,null,new NVector(b.cx,0,b.cz),BossTide,false,NVector.Zero);
                for(int i=0;i<backroomParts.Count;i++){var part=NativeDeepEndBoss.Parts[i];NVector p;if(part.Lamp>=0)deepAuthorityRig.LampFrame(part.Lamp,out p,out _,out _);else p=part.Id=="throat"?deepAuthorityRig.MouthAt:deepAuthorityRig.Shown.Position[part.Joint];backroomParts[i].transform.position=new Vector3(p.X,p.Y,p.Z);backroomParts[i].enabled=m.Hp>0;}
            }
            m.Collider.enabled=false;
        }
        private void StepBackroomBoss()
        {
            var z=ZoneBoss;var m=zoneBossActor;var b=z.backrooms;if(z.holdTarget>=0&&Time>=z.holdUntil)BossRelease();var pilots=Pilots.Where(ZonePilot).Select(p=>new BackroomsBossPilot{Seat=p.Seat.id,Feet=new NVector(p.Body.position.x,p.Body.position.y-p.Collider.height/2,p.Body.position.z),Yaw=p.Yaw,Noise=Time-p.AttackAt<2?1:0}).Take(8).ToArray();
            NativeBackroomsBossRules.Step(z,m.Hp,m.MaxHp,Time,1f/60,pilots,backroomContacts,deepAuthorityRig);Pve.Phase=z.phase;
            m.Body.position=new Vector3(b.x,z.archetype==NativeDeepEndBoss.Archetype?deepAuthorityRig.Root.Y:0,b.z);m.Yaw=b.yaw;m.Body.rotation=Quaternion.Euler(0,m.Yaw,0);PoseBackroomBody();
            foreach(var hit in backroomContacts)
            {
                var at=new Vector3(hit.Point.X,hit.Point.Y,hit.Point.Z);if(hit.Add!=null){SpawnPveAddAt(hit.Add,at);continue;}var p=Pilots.Find(p=>p.Seat.id==hit.Seat);if(p==null||!ZonePilot(p))continue;
                if(hit.Return&&!NativeBackroomsBossRules.Inside(z,new NVector(p.Body.position.x,p.Body.position.y-p.Collider.height/2,p.Body.position.z))){p.Body.position=at+Vector3.up*p.Collider.height/2;p.Body.linearVelocity=Vector3.zero;}
                if(Invulnerable(p)||hit.Jump&&p.Body.position.y-p.Collider.height/2>Math.Max(0,BossTide)+.6f||Time<p.BlockEnd)continue;
                if(hit.Pull>0)ZoneDrag(p,at-p.Body.position,hit.Pull);
                if(hit.Damage>0){float actual=Damage(p,null,hit.Damage*(float)(Pve.Boss.Stats["damage"]/NativePveRules.BossDamage(1)),p.Body.position-at,0,false,primary:false,rollAvoidable:true,causeKind:z.archetype,attackerMinion:m);if(actual>0){p.StunEnd=Math.Max(p.StunEnd,Time+hit.Stun);if(hit.Return){p.Body.position=at+Vector3.up*p.Collider.height/2;p.Body.linearVelocity=Vector3.zero;}}}
            }
        }
        private Vector3 BackroomContactPoint(NativeMinion m,Vector3 from)
        {if(m!=zoneBossActor||ZoneBoss?.backrooms==null)return m.Body.position;Vector3 best=m.Body.position;float d=float.MaxValue;foreach(var c in backroomParts)if(c!=null&&c.enabled){var at=c.ClosestPoint(from);float dist=(at-from).sqrMagnitude;if(dist<d){d=dist;best=at;}}return best;}
        private float BackroomHitScale(NativeMinion m,NativePilot source,float damage,Vector3? point)
        {
            var z=ZoneBoss;var b=z.backrooms;bool hum=z.archetype==NativeHumBoss.Archetype;
            if(z.action=="phase"&&Time<z.actionAt+3.5f)return 0;
            if(hum&&z.action=="lunge"&&Time>=z.hitAt&&Time<z.hitAt+1.2f&&NativeHumArena.NearestWall(NativeBackroomsBossRules.Room(b),b.shiftMask,b.x,b.z,out _,out _,out _,out _)<=.4f)return 0;
            if(!hum&&deepAuthorityRig.OutOfWater==0&&b.immuneSince>=0&&Time-b.immuneSince<6&&Time>=b.exposedUntil&&Time>=z.staggerAt+z.staggerSeconds)return 0;
            float multiplier=Time<z.staggerAt+z.staggerSeconds&&z.staggerCause=="clip"?2:1;
            if(!point.HasValue)return multiplier;int closest=-1;float distance=float.MaxValue;
            for(int i=0;i<backroomParts.Count;i++){var c=backroomParts[i];float d=Vector3.Distance(c.transform.position,point.Value)-c.radius;if(d<distance){distance=d;closest=i;}}
            int part=-1;float share=0;
            if(closest>=0&&closest<9&&hum){var p=NativeHumBoss.WeakPoints[closest];part=closest>=2?closest-2:closest==0?7:-1;share=closest==0?.05f:NativeHumBoss.TubeShare;if(p.Id=="head"&&(z.action=="reach"||z.action=="stoop"))multiplier*=2;if(p.Id=="ballast")multiplier*=z.action=="pulse"&&Time<z.hitAt?2.5f:1.5f;}
            if(closest>=0&&!hum){var p=NativeDeepEndBoss.Parts[closest];part=p.Lamp;share=NativeDeepEndBoss.LampShare;if(p.Id=="throat"&&(z.action=="jet"||z.action=="drain"||z.action=="breach"))multiplier*=2;}
            if(part>=0&&(z.parts&(1<<part))==0){bossPartDamage[part]+=damage;if(bossPartDamage[part]>=m.MaxHp*share){z.parts|=1<<part;z.partAt[part]=Time;
                if(hum&&z.action=="pulse"&&Time<z.hitAt&&z.partAt.Take(7).Count(t=>t>=z.actionAt)>=3){BossStagger("choke",2);b.next=Time+2;}
                if(!hum&&b.lampsPhase!=z.phase&&z.partAt.Count(t=>t>=z.phaseAt)>=4){b.lampsPhase=z.phase;BossStagger("lamps",3);b.next=Time+3;}}}
            if(z.holdTarget>=0){if(source?.Seat.id==z.holdTarget)bossSelfDamage+=damage;else bossHoldDamage+=damage;if(bossSelfDamage>=40||bossHoldDamage>=120){BossRelease();z.cancelAt=Time;b.handUntil=Time;b.next=Time+.6f;}}
            return multiplier;
        }
    }
}
