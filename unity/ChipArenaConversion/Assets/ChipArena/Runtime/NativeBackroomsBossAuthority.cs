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
        private int backroomMask=-1;private float backroomTick=-1,backroomSealHeight=-1,drainThroatFor=-100,drainThroat;
        // NOCLIP's raised room: the level's one ceiling block is set aside and the room gets its own lid at the new height, with the rest of the level's ceiling where it was around it.
        private readonly List<Collider> humLid=new List<Collider>();private readonly List<Collider> humLevelCeiling=new List<Collider>();private bool humLidRaised;
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
        {foreach(var c in backroomSeals.Concat(humPartitions).Concat(humLid))if(c!=null)UnityEngine.Object.Destroy(c.gameObject);foreach(var c in humLevelCeiling)if(c!=null)c.enabled=true;backroomSeals.Clear();humPartitions.Clear();humLid.Clear();humLevelCeiling.Clear();humLidRaised=false;backroomParts.Clear();backroomMask=-1;backroomTick=-1;backroomSealHeight=-1;drainThroatFor=-100;drainThroat=0;deepAuthorityRig.Clear();}
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
            var z=ZoneBoss;var b=z?.backrooms;if(b==null)return;bool hum=z.archetype==NativeHumBoss.Archetype;float half=hum?NativeHumArena.Size/2:20,height=NativeBackroomsBossRules.Ceiling(z);
            if(backroomSeals.Count==0)for(int side=0;side<4;side++)
            {
                var go=new GameObject("Backrooms boss seal");go.layer=PropLayer;go.transform.SetParent(root.transform);backroomSeals.Add(go.AddComponent<BoxCollider>());
            }
            if(backroomSealHeight!=height)
            {
                backroomSealHeight=height;
                for(int side=0;side<4;side++){bool along=side<2;float sign=side%2==0?-1:1;var c=(BoxCollider)backroomSeals[side];c.transform.position=new Vector3(b.cx+(along?0:sign*half),height/2,b.cz+(along?sign*half:0));c.size=new Vector3(along?half*2:.3f,height,along?.3f:half*2);}
            }
            if(hum&&(z.phase>=3)!=humLidRaised)RaiseHumLid(b,z.phase>=3);
            foreach(var c in backroomSeals)c.enabled=z.deathAt<0;
            if(hum&&humPartitions.Count==0)for(int i=0;i<8;i++){var go=new GameObject("Hum partition collision");go.layer=PropLayer;go.transform.SetParent(root.transform);humPartitions.Add(go.AddComponent<BoxCollider>());}
            if(hum&&backroomMask!=b.shiftMask){var room=NativeBackroomsBossRules.Room(b);for(int i=0;i<8;i++){room.Partition(i,(b.shiftMask>>i)&1,out var p,out bool along);var c=(BoxCollider)humPartitions[i];c.transform.position=new Vector3(p.X,2,p.Z);c.size=new Vector3(along?6.4f:.16f,4,along?.16f:6.4f);}backroomMask=b.shiftMask;}
        }
        /// <summary>NOCLIP pushes its room's ceiling up (<see cref="NativeHumBoss.HighCeiling"/>): the level's ceiling block is switched off, and in its place the room gets a lid at the new height and
        /// the level keeps its old ceiling everywhere around the room (a real ceiling stops pilots climbing onto the maze's walls). Switched back on a reset.</summary>
        private void RaiseHumLid(NativeBackroomsBossFrame b,bool raise)
        {
            if(humLevelCeiling.Count==0)foreach(var prop in StaticBlocks)if(prop!=null&&prop.Definition?.id!=null&&prop.Definition.id.EndsWith(":ceiling",StringComparison.Ordinal))humLevelCeiling.AddRange(prop.GetComponentsInChildren<Collider>());
            if(humLevelCeiling.Count==0)return;humLidRaised=raise;
            if(humLid.Count==0)
            {
                var level=humLevelCeiling[0].bounds;foreach(var c in humLevelCeiling)level.Encapsulate(c.bounds);float half=NativeHumArena.Size/2,y=level.center.y,thick=Math.Max(.05f,level.size.y);
                float x0=b.cx-half,x1=b.cx+half,z0=b.cz-half,z1=b.cz+half;
                void Piece(string name,float ax,float az,float bx,float bz,float py){if(bx-ax<=.01f||bz-az<=.01f)return;var go=new GameObject(name);go.layer=PropLayer;go.transform.SetParent(root.transform);go.transform.position=new Vector3((ax+bx)/2,py,(az+bz)/2);var c=go.AddComponent<BoxCollider>();c.size=new Vector3(bx-ax,thick,bz-az);humLid.Add(c);}
                Piece("Hum room lid",x0,z0,x1,z1,NativeHumBoss.HighCeiling+thick/2);
                Piece("Level ceiling west",level.min.x,level.min.z,x0,level.max.z,y);Piece("Level ceiling east",x1,level.min.z,level.max.x,level.max.z,y);
                Piece("Level ceiling south",x0,level.min.z,x1,z0,y);Piece("Level ceiling north",x0,z1,x1,level.max.z,y);
            }
            foreach(var c in humLevelCeiling)if(c!=null)c.enabled=!raise;foreach(var c in humLid)if(c!=null)c.enabled=raise;
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
                if(hit.Pull>0){if(z.archetype==NativeDeepEndBoss.Archetype)ZoneCurrent(p,at-p.Body.position,hit.Pull);else ZoneDrag(p,at-p.Body.position,hit.Pull);}
                if(hit.Damage>0){float actual=Damage(p,null,hit.Damage*(float)(Pve.Boss.Stats["damage"]/NativePveRules.BossDamage(1)),p.Body.position-at,0,false,primary:false,rollAvoidable:true,causeKind:z.archetype,attackerMinion:m);if(actual>0){p.StunEnd=Math.Max(p.StunEnd,Time+hit.Stun);if(hit.Return){p.Body.position=at+Vector3.up*p.Collider.height/2;p.Body.linearVelocity=Vector3.zero;}}}
            }
        }
        /// <summary>THE DEEP END's Drain: a current added to the pilot's own movement (they keep control, so they can run or roll out of it, and fight it in the water); it holds a pilot in the
        /// air too (the owner: "which is fine"), not one rolling; never through a wall. (ZoneDrag, a grab's, takes the pilot's control away.)</summary>
        private void ZoneCurrent(NativePilot p,Vector3 direction,float speed)
        {
            if(Time<p.RollEnd)return;direction.y=0;if(direction.sqrMagnitude<1e-4f)return;
            var delta=direction.normalized*(speed/60);var at=p.Body.position;if(!SceneLinecast(at,at+delta*2,WorldMask)){p.Body.position=at+delta;p.Body.transform.position=p.Body.position;}
        }
        private Vector3 BackroomContactPoint(NativeMinion m,Vector3 from)
        {if(m!=zoneBossActor||ZoneBoss?.backrooms==null)return m.Body.position;Vector3 best=m.Body.position;float d=float.MaxValue;foreach(var c in backroomParts)if(c!=null&&c.enabled){var at=c.ClosestPoint(from);float dist=(at-from).sqrMagnitude;if(dist<d){d=dist;best=at;}}return best;}
        private float BackroomHitScale(NativeMinion m,NativePilot source,float damage,Vector3? point)
        {
            var z=ZoneBoss;var b=z.backrooms;bool hum=z.archetype==NativeHumBoss.Archetype;
            if(z.action=="phase"&&Time<z.actionAt+3.5f)return 0;
            float lungeAge=Time-z.hitAt,lungeStrike=NativeHumBoss.Move("lunge").Strike;bool passing=lungeAge>=0&&lungeAge<lungeStrike||z.phase==3&&lungeAge>=2*lungeStrike&&lungeAge<3*lungeStrike;
            if(hum&&z.action=="lunge"&&passing&&NativeHumArena.NearestWall(NativeBackroomsBossRules.Room(b),b.shiftMask,b.x,b.z,out _,out _,out _,out _)<=.4f)return 0;
            if(!hum&&deepAuthorityRig.OutOfWater==0&&b.immuneSince>=0&&Time-b.immuneSince<6&&Time>=b.exposedUntil&&Time>=z.staggerAt+z.staggerSeconds)return 0;
            float multiplier=Time<z.staggerAt+z.staggerSeconds&&z.staggerCause=="clip"?2:1;
            if(!point.HasValue)return multiplier;int closest=-1;float distance=float.MaxValue;
            for(int i=0;i<backroomParts.Count;i++){var c=backroomParts[i];float d=Vector3.Distance(c.transform.position,point.Value)-c.radius;if(d<distance){distance=d;closest=i;}}
            int part=-1;float share=0;
            if(closest>=0&&closest<9&&hum){var p=NativeHumBoss.WeakPoints[closest];part=closest>=2?closest-2:closest==0?7:-1;share=closest==0?.05f:NativeHumBoss.TubeShare;if(p.Id=="head"&&(z.action=="reach"||z.action=="stoop"))multiplier*=2;if(p.Id=="ballast")multiplier*=z.action=="pulse"&&Time<z.hitAt?2.5f:1.5f;}
            if(closest>=0&&!hum){var p=NativeDeepEndBoss.Parts[closest];part=p.Lamp;share=NativeDeepEndBoss.LampShare;if(p.Id=="throat"&&(z.action=="jet"||z.action=="drain"||z.action=="breach"))multiplier*=NativeDeepEndBoss.ThroatMultiplier;
                // The Drain's counter: hurt the open throat while it drains and it chokes shut (the presentation closes the iris and stops the pull's look on cancelAt).
                if(p.Id=="throat"&&z.action=="drain"&&z.cancelAt<0&&Time>=z.hitAt&&Time<=b.whirlUntil)
                {if(drainThroatFor!=z.actionAt){drainThroatFor=z.actionAt;drainThroat=0;}drainThroat+=damage*multiplier;if(drainThroat>=m.MaxHp*NativeDeepEndBoss.DrainThroatShare){BossStagger("throat",NativeDeepEndBoss.DrainStagger);b.whirlUntil=Time;b.next=Time+NativeDeepEndBoss.DrainStagger;}}}
            if(part>=0&&(z.parts&(1<<part))==0){bossPartDamage[part]+=damage;if(bossPartDamage[part]>=m.MaxHp*share){z.parts|=1<<part;z.partAt[part]=Time;
                if(hum&&z.action=="pulse"&&Time<z.hitAt&&z.partAt.Take(7).Count(t=>t>=z.actionAt)>=3){BossStagger("choke",2);b.next=Time+2;}
                if(!hum&&b.lampsPhase!=z.phase&&z.partAt.Count(t=>t>=z.phaseAt)>=4){b.lampsPhase=z.phase;BossStagger("lamps",3);b.next=Time+3;}}}
            if(z.holdTarget>=0){if(source?.Seat.id==z.holdTarget)bossSelfDamage+=damage;else bossHoldDamage+=damage;if(bossSelfDamage>=40||bossHoldDamage>=120){BossRelease();z.cancelAt=Time;b.handUntil=Time;b.next=Time+.6f;}}
            return multiplier;
        }
    }
}
