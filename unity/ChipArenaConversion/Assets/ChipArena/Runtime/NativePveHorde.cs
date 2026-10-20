using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed class NativePveAdd : NativeHulkHunt
    {
        public bool BossSpawn;public bool ZoneLanded;public readonly HashSet<int> ZoneSwingHits=new HashSet<int>();public CapsuleCollider ZoneChain,ZoneHead;public NativeZoneEnemyFrame Zone=new NativeZoneEnemyFrame();public float ZoneReady,ZoneBusy,ZoneStrike=-100,ZoneCallReady,ZoneCallStrike=-100,ZoneArmor,ZoneExposureDamage,ZoneSelfDamage,HoldDamage,ZoneOtherDamage,ZoneOutsideAt=-100,ZoneDropHeight,ZoneLandingY;public int ZoneClimbs,ZoneTarget=-1;public Vector3 ZoneClimbFrom;
        public string HubRing;public bool HubCredit;public HubZombieView Hub;
        public Vector3 BreachShortcut;public float BreachThinkAt;public bool HasBreachShortcut;public float SpitCancelAt=-100;public NativeHulkBreach.Progress WallProgress;public NativeBreachRecord WallBreach;public PveAddStats Stats;public PveEntry Entry;public int Lost,Side=1;public float ActiveAt,ExposedAt,SpawnAt,ThinkAt,RecoverAt,StaggerAt,DamageScale,HpScale,PossessedAt,SlowUntil,SlowFactor=1,LastDamage=-100,VentReady,WalkAt,DiveAt,VentUntil;public bool Landed,DropLanded,Struck=true,Possessed,Crawl,Limp,First=true,Travelling;public float HideThinkAt;public Vector3 HideDestination;public Vector3 External;public NativePilot Target;public NativeMinion KillerSummon;public NativePilot LastHit;
        public readonly Dictionary<int,float> Dealt=new Dictionary<int,float>();public readonly Dictionary<int,float> Parts=new Dictionary<int,float>();
    }
    public sealed partial class NativeArena
    {
        private readonly Dictionary<int,float> pveEntries=new Dictionary<int,float>();private NativePveNavigation pveNavigation;
        private bool WardLandingClear(PveEntry e)
        {
            if(e.clear<=0)return true;var at=Vec(e.@exit.Position());
            bool Near(Vector3 p,float radius)=>Math.Abs(p.y-at.y)<4&&new Vector2(p.x-at.x,p.z-at.z).magnitude<e.clear+radius;
            return !Pilots.Any(p=>p.Hp>0&&p.Seat.connected&&Near(p.Body.position,p.Collider.radius))&&!Minions.Any(m=>m.Hp>0&&(Near(m.Body.position,m.Collider.radius)||NativeWardEntries.Drop(m.Pve?.Entry??e)&&m.Pve!=null&&Time<m.Pve.ActiveAt&&Near(Vec(m.Pve.Entry.@exit.Position()),m.Collider.radius)));
        }
        public double HordeMs {get;private set;}public double HordeSpawnMs {get;private set;}
        private NativeMinion SpawnPveAdd(string kind,bool force=false,int forcedEntry=-1)
        {
            long at=System.Diagnostics.Stopwatch.GetTimestamp();
            try{return SpawnPveAddCore(kind,force,forcedEntry);}
            finally{HordeSpawnMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-at)*1000d/System.Diagnostics.Stopwatch.Frequency;}
        }
        private NativeMinion SpawnPveAddCore(string kind,bool force,int forcedEntry)
        {
            var v=Pve??wardSandboxState;if(v==null||kind==null)return null;
            if(!ZoneKindRoom(kind))return null;
            if(Minions.Count(m=>m.Pve!=null&&!m.DevMonster)>=v.Cap&&v.Cap>0)return null;
            if(kind=="spitter"&&Minions.Count(m=>m.Hp>0&&m.Pve?.Stats.id=="spitter")>=NativeSpitterRules.Cap)return null;
            if(kind=="hulk"&&Minions.Count(m=>m.Pve?.Stats.id=="hulk")>=Math.Max(1,Math.Min(2,v.Cap/2)))return null;
            var members=(Pve==null?Pilots.Where(p=>!p.Seat.AuthorityBoss):Members()).Where(p=>p.Hp>0).ToArray();
            var candidates=v.Content.entries.Where(e=>ZoneEntryAllowed(e,kind)&&EntryQueue(e.e)<3&&
                (e.kind!="vent"||!VentHazard(e.e))&&(e.kind!="breach"||BreachAvailable(e))&&WardLandingClear(e)&&
                !UnsafeStagePath(Vec(e.@exit.Position(.9f)),Vec(e.@exit.Position(.9f)))&&(forcedEntry<0||e.e==forcedEntry))
                .Select(e=>new{entry=e,weight=NativeEntryRules.Weight(force?float.PositiveInfinity:members.Select(p=>(p.Body.position-Vec(e.@exit.Position(.9f))).magnitude).DefaultIfEmpty(float.PositiveInfinity).Min(),e.kind,v.Stage=="wave",v.Hidden)*
                    (members.Any(p=>Math.Abs(p.Body.position.y-e.@exit.y)<2)?2:1)/
                    (1+Minions.Count(m=>m.Pve!=null&&m.Pve.Entry.level==e.level))}).Where(e=>e.weight>0).ToArray();
            float total=candidates.Sum(e=>e.weight);if(total<=0)return null;
            double roll=v.SpawnRandom.Next()*total;var entry=candidates[candidates.Length-1].entry;
            foreach(var choice in candidates){roll-=choice.weight;if(roll<0){entry=choice.entry;break;}}
            if(NativeHordeDropRules.UsesDrop(kind,v.Content.entries)&&forcedEntry<0)
            {entry=NativeHordeDropRules.Next(v.Content.entries,ref v.DropCursor,e=>candidates.Any(c=>c.entry==e));if(entry==null)return null;}
            var times=PlanEntry(entry,kind);var stats=v.Content.adds.First(x=>x.id==kind);int wave=v.Stage=="boss"?3:Math.Max(1,v.Wave);float hpScale=1+.12f*(wave-1),damageScale=1+.06f*(wave-1);
            bool heavy=kind=="hulk";float height=heavy?NativeHordeKinds.HulkHeight:1.7f;
            var obj=new GameObject("Siren "+kind);obj.layer=11;obj.transform.SetParent(root.transform);obj.transform.position=Vec(entry.from.Position(height/2));
            var c=obj.AddComponent<CapsuleCollider>();c.radius=heavy?NativeHordeKinds.HulkRadius:.42f;c.height=height;c.enabled=false;c.sharedMaterial=surfaces["pilot"];
            var body=obj.AddComponent<Rigidbody>();body.mass=heavy?160:40;body.constraints=RigidbodyConstraints.FreezeRotation;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
            var profile=heavy?NativeHordeDrop.Hulk:NativeHordeDrop.Brute;
            var a=new NativePveAdd{Stats=stats,Entry=entry,HpScale=hpScale,DamageScale=damageScale,SpawnAt=times.spawn,WalkAt=times.walk,First=times.first,ActiveAt=times.active,
                ExposedAt=NativeWardEntries.Drop(entry)?NativeHordeDrop.LandAt(times.active,profile):entry.kind=="vent"?times.active-1.15f:times.walk};
            if(entry.kind=="breach")BeginWardBreach(entry,times.active);
            var m=new NativeMinion{Id=nextMinion++,Owner=v.Boss,Kind="siren-add",Body=body,Collider=c,Hp=stats.hp*hpScale,MaxHp=stats.hp*hpScale,Until=float.PositiveInfinity,Yaw=entry.yaw*Mathf.Rad2Deg,Pve=a};
            obj.AddComponent<NativeMinionBody>().Minion=m;Minions.Add(m);InitZoneEnemy(m);return m;
        }
        private void WardEntryPose(NativeMinion m)
        {
            var a=m.Pve;var e=a.Entry;Vector3 to=Vec(e.@exit.Position(m.Collider.height/2+.025f));
            if(NativeWardEntries.Drop(e))
            {
                var profile=a.Stats.id=="hulk"?NativeHordeDrop.Hulk:NativeHordeDrop.Brute;
                NativeHordeDrop.At(Time-a.SpawnAt,a.ActiveAt-a.SpawnAt,e.height,profile,out _,out float lift);
                m.Body.position=to+Vector3.up*lift;
                if(!a.Landed&&Time>=NativeHordeDrop.LandAt(a.ActiveAt,profile))
                {a.Landed=true;WardLandingSlam(m,profile);}
            }
            else if(e.kind=="vent"&&(string.IsNullOrEmpty(e.surface)||e.surface=="floor"))m.Body.position=to+Quaternion.Euler(0,m.Yaw,0)*Vec(NativeEntryRules.Climb(Time,a.ActiveAt));
            else m.Body.position=Vector3.Lerp(Vec(e.from.Position(e.surface=="wall"?.44f:m.Collider.height/2+.025f)),to,Mathf.Clamp01((Time-a.WalkAt)/Math.Max(.1f,a.ActiveAt-a.WalkAt)));
            m.Collider.enabled=Time>=a.ExposedAt;
        }
        private void WardLandingSlam(NativeMinion m,NativeHordeDrop.DropProfile profile)
        {
            var at=Vec(m.Pve.Entry.@exit.Position(.45f));
            foreach(var p in Pilots.Where(p=>p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss))
            {
                var d=p.Body.position-at;float distance=new Vector2(d.x,d.z).magnitude;
                if(Math.Abs(d.y)>2||distance>profile.SlamRadius||!MeleeVisible(at,p.Body.position,m.Owner,p.Collider,true))continue;
                var dir=new Vector3(d.x,0,d.z).normalized;bool block=Time<p.BlockEnd;
                if(block){BlockProc(p,m.Owner,p.Body.position,"clash");p.Knockback+=dir*3;}
                else {Damage(p,null,profile.Heavy?Mathf.Lerp(25,10,distance/profile.SlamRadius):Mathf.Lerp(12,5,distance/profile.SlamRadius),dir,profile.Heavy?900:400,false,primary:false,melee:true);p.Knockback.y=Math.Max(p.Knockback.y,profile.Heavy?4:2);}
            }
        }
        private readonly List<NativeMinion> hordeAdds=new List<NativeMinion>(192);
        private readonly List<NativePilot> hordeEnemies=new List<NativePilot>(8);
        private readonly Dictionary<int,int> hordeTargets=new Dictionary<int,int>(8);
        private readonly NativeCrowdGrid hordeGrid=new NativeCrowdGrid();
        private int HordeTargetCount(int id)=>hordeTargets.TryGetValue(id,out int count)?count:0;
        private string PickHordeKind(NumberValue[] mix,NativePveState v)
        {
            int spitters=0,hulks=0;foreach(var m in Minions)if(m.Hp>0){if(m.Pve?.Stats.id=="spitter")spitters++;if(m.Pve?.Stats.id=="hulk")hulks++;}
            bool Allowed(string id)=>id!="spitter"||spitters<NativeSpitterRules.Cap;
            bool Room(string id)=>ZoneKindRoom(id)&&Allowed(id)&&(id!="hulk"||hulks<Math.Max(1,Math.Min(2,v.Cap/2)));
            double total=0;foreach(var row in mix)if(row.value>0&&Room(row.name))total+=row.value;
            double roll=v.SpawnRandom.Next()*total;string last=null;foreach(var row in mix)if(row.value>0&&Room(row.name)){last=row.name;roll-=row.value;if(roll<0)return last;}return last;
        }
        private Vector3 HordeSeparation(NativeMinion m,Vector3 at)
        {
            var push=Vector3.zero;int count=0,cx=NativeCrowdGrid.Cell(at.x),cz=NativeCrowdGrid.Cell(at.z);
            for(int z=cz-1;z<=cz+1&&count<6;z++)for(int x=cx-1;x<=cx+1&&count<6;x++)
            for(int i=hordeGrid.Head(x,z);i>=0&&count<6;i=hordeGrid.Next(i))
            {var other=hordeAdds[i];if(other==m||other.Hp<=0||Math.Abs(at.y-other.Body.position.y)>1.8f)continue;var diff=at-other.Body.position;diff.y=0;float sq=diff.sqrMagnitude;if(sq>=.81f)continue;count++;float dist=Mathf.Sqrt(sq);push+=dist<.0001f?Vector3.right*(m.Id<other.Id?-1.8f:1.8f):diff/dist*(.9f-dist)*2;}
            return push;
        }
        private void StepPveHorde()
        {
            long at=System.Diagnostics.Stopwatch.GetTimestamp();
            try{StepPveHordeCore();}
            finally{HordeMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-at)*1000d/System.Diagnostics.Stopwatch.Frequency;}
        }
        private void StepPveHordeCore()
        {
            var v=Pve;const float dt=1f/60;pveNavigation=pveNavigation??new NativePveNavigation(this);
            int active=Minions.Count(m=>m.Pve!=null&&!m.DevMonster),entering=Minions.Count(m=>m.Pve!=null&&!m.DevMonster&&Time<m.Pve.ActiveAt);
            if(v.Rate>0&&Time>=v.SpawnAt&&active<v.Cap&&entering<(v.Hidden?64:16)&&(v.SpawnBudget<0||v.Spawned<v.SpawnBudget))
            {int count=NativePveRules.SpawnAllowance(v.SpawnBudget,v.Spawned,active,v.Cap,Math.Min(2,1+(int)((Time-v.SpawnAt)*v.Rate)));for(int n=0;n<count&&active<v.Cap;n++){var mix=ZoneBoss!=null?NativeZoneCombat.BossEncounter(MapId,ZoneBoss.phase):NativeZoneCombat.Active(MapId)?NativeZoneCombat.Encounter(MapId,v.Wave,v.Stage=="boss"):v.Stage=="boss"?v.Content.bossMix[v.Phase-1].mix:v.Content.waves[v.Wave-1].mix;var created=SpawnPveAdd(PickHordeKind(mix,v));if(created==null)break;v.Spawned++;active++;v.SpawnAt+=1/v.Rate;if(v.SpawnBudget>=0&&v.Spawned>=v.SpawnBudget)break;}}else if(active>=v.Cap||entering>=(v.Hidden?64:16))v.SpawnAt=Time;
            var enemies=hordeEnemies;enemies.Clear();foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss)enemies.Add(p);
            var adds=hordeAdds;adds.Clear();hordeTargets.Clear();hordeGrid.Clear();
            foreach(var m in Minions)if(m.Pve!=null&&!m.DevMonster){adds.Add(m);if(m.Pve.Target!=null){int id=m.Pve.Target.Seat.id;hordeTargets.TryGetValue(id,out int count);hordeTargets[id]=count+1;}if(m.Hp>0&&Time>=m.Pve.ActiveAt)hordeGrid.Add(adds.Count-1,m.Body.position.x,m.Body.position.z);}
            foreach(var m in adds)
            {
                if(m.Hp<=0)continue;var a=m.Pve;
                if(a.Travelling){if(Time>=a.VentUntil)Reemerge(m);continue;}
                bool bruteDrop=a.Stats.id=="brute"&&NativeHordeDrop.Is(a.Entry);
                if(bruteDrop&&StepPveDrop(m))continue;
                if(!bruteDrop&&Time<a.ActiveAt)
                {WardEntryPose(m);continue;}
                if(m.Body.isKinematic){m.Body.isKinematic=false;m.Collider.enabled=true;m.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;}
                if(m.Body.position.y<-6){if(a.Possessed)FoundPveBoss(BossRescue);RemoveMinion(m,false);continue;}
                foreach(var pair in m.Dots.ToArray()){if(a.Stats.id=="cinderling"&&pair.Key.EndsWith(":fire",StringComparison.Ordinal)){m.Dots.Remove(pair.Key);continue;}var dot=pair.Value;while(m.Hp>0&&dot.Next<=Time+1e-8&&dot.Next<=dot.Until+1e-8){if(!dot.HubNoReward)HitMinion(m,Pilots.Find(p=>p.Seat.id==dot.Source),(float)(dot.Dps*.25),Vector3.zero,0,kind:"field",from:m.Body.position);dot.Next+=.25;}if(Time>dot.Until)m.Dots.Remove(pair.Key);}if(m.Hp<=0)continue;
                // Landed drops can take hits/DoTs and be shoved during recovery, but cannot move voluntarily or attack.
                if(Time<a.ActiveAt){a.External*=Mathf.Exp(-6*dt);m.Body.linearVelocity=new Vector3(a.External.x,m.Body.linearVelocity.y,a.External.z);continue;}
                Vector3 at=m.Body.position,navAt=Vec(NativeNavSearch.BodyAnchor(Vec(at),m.Collider.height/2));var target=a.Target;
                bool roaming=HubOn&&hubMobs.ContainsKey(m.Id);
                if(roaming){if(StepHubMind(m,enemies,out target))continue;}
                else if(Time>=a.ThinkAt||target==null||target.Hp<=0||!target.Seat.connected||!HubCanTarget(target,target))
                {
                    if(target!=null&&!HubCanTarget(target,target)){a.Struck=true;if(hubMobs.TryGetValue(m.Id,out var hubMob)&&hubMob.LostAt<0)hubMob.LostAt=Time;}
                    a.ThinkAt=Time+.75f;target=enemies.Where(p=>p.Hp>0&&p.Seat.connected&&HubCanTarget(p,a.Target)&&(!a.Possessed||!p.Seat.bot)).OrderBy(p=>ZoneTargetPriority(m,p)+pveNavigation.Distance(at,p.Body.position,p.Seat.id)+3*Math.Max(0,HordeTargetCount(p.Seat.id)-6)-(a.Target==p?2:0)-(v.Recruits.Any(r=>r.Pilot==p&&r.Role=="tank")&&Vector3.Distance(p.Body.position,at)<8?6:0)).FirstOrDefault();a.Target=target;if(target!=null&&TryVentTravel(m,target))continue;
                }
                if(StepZoneEnemy(m,target))continue;
                if(StepHulkHunt(m,target))continue;
                if(target!=null&&StepHulkBreach(m,target.Body.position)){m.Body.linearVelocity=new Vector3(0,m.Body.linearVelocity.y,0);continue;}
                if(!a.Struck&&Time>=m.StrikeAt){a.Struck=true;PveAddStrike(m);if(m.Hp<=0)continue;}
                if(target==null){m.Body.linearVelocity=new Vector3(0,m.Body.linearVelocity.y,0);continue;}Vector3 d=target.Body.position-at;float distance=new Vector2(d.x,d.z).magnitude;
                bool left=(a.Lost&(2|8))!=0,right=(a.Lost&(4|16))!=0,armless=left&&right;float reach=a.Crawl?1.3f:armless?1.2f:a.Stats.reach,start=a.Stats.id=="spitter"?NativeSpitterRules.Range:a.Crawl?1:armless?.95f:a.Stats.startRange,hit=a.Stats.id=="spitter"?a.Stats.hit:a.Crawl||armless?.34f:a.Stats.hit;
                if(a.Stats.id=="hulk"&&!a.Crawl&&!armless)start=Math.Max(start,NativeHulkMoves.PunchReach);
                if(a.Stats.id!="rigger"&&a.Stats.id!="undertow"&&a.Struck&&!a.Possessed&&Time>=a.RecoverAt&&Time>=a.StaggerAt&&distance<=start&&(a.Stats.id!="spitter"||distance>=4)&&Math.Abs(d.y)<1.6f&&(a.Stats.id!="hulk"&&a.Stats.id!="spitter"||MeleeVisible(at,target.Body.position,m.Owner,target.Collider,true)))
                {a.Zone.attack=null;if(a.Stats.id=="hulk")hit=ChooseHulkMove(m,target,hit);m.LastAttack=Time;m.StrikeAt=Time+hit;a.Struck=false;a.RecoverAt=Time+Math.Max(a.Stats.cycle,hit+.05f);if(a.Zone.aboardUntil>Time)a.RecoverAt=Time+Math.Max(a.Stats.cycle/1.15f,hit+.05f);if(a.Move!=0)a.RecoverAt=Time+NativeHulkMoves.Recover(NativeHulkMoves.Of(a.Move),a.Stats.cycle,a.Crawl,a.Lost);a.Side=-a.Side;if(a.Side==1&&right)a.Side=-1;else if(a.Side==-1&&left)a.Side=1;m.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;}
                Vector3 move=Vector3.zero;
                if(Time<a.StaggerAt){}
                else if(!a.Struck){m.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;if(a.Stats.id!="spitter"&&distance>.8f)move=Vec(V3.Forward(m.Yaw))*((a.Crawl?.15f:armless?.45f:a.Stats.lunge)/Math.Max(.05f,hit));}
                else if(Time>=a.RecoverAt)
                {
                    float speed=(distance<=7||m.Hp<m.MaxHp*.5f?a.Stats.run:a.Stats.walk)*(a.Crawl?.35f:a.Limp?.55f:1);
                    Vector3 destination=target.Body.position;bool hiding=a.Possessed&&Time-a.PossessedAt<60;
                    bool hideSpot=hiding&&Map.unity&&v.Content.hideSpots?.Length>0;
                    if(hideSpot)
                    {
                        if(Time>=a.HideThinkAt)
                        {
                            a.HideThinkAt=Time+.75f;
                            var hide=v.Content.hideSpots.OrderBy(h=>enemies.Count(p=>Math.Abs(p.Body.position.y-h.y)<2))
                                .ThenByDescending(h=>enemies.Select(p=>(p.Body.position-Vec(h.Position(.9f))).sqrMagnitude).DefaultIfEmpty(0).Min()).First();
                            a.HideDestination=Vec(hide.Position(NativeLocomotionRules.HalfHeight));
                        }
                        destination=a.HideDestination;
                    }
                    // Resolve hiding before requesting a route: the old chase-then-hide
                    // calls invalidated each other's cached target twice every fixed tick.
                    Vector3 dir=hideSpot&&(destination-navAt).magnitude<1?Vector3.zero:
                        Map.unity?HubNavMove(m,navAt,destination,(destination-navAt).normalized,hideSpot||adds.Count<12?(int?)null:target.Seat.id):
                        pveNavigation.Direction(at,destination,target.Seat.id);
                    if(a.HasBreachShortcut)dir=a.BreachShortcut;
                    if(a.WallBreach!=null&&Time>=a.WallBreach.breakAt&&Time<a.WallBreach.breakAt+3f)dir=Vec(a.WallBreach.normal);
                    if(hiding)
                    {
                        if(!hideSpot){float age=Time-a.PossessedAt;if(age%6>=5.2f||distance>=8&&distance<=14)dir=Vector3.zero;else if(distance<8)dir=-dir;}
                        speed=(m.Hp<m.MaxHp*.5f?a.Stats.run:a.Stats.walk)*(a.Crawl?.35f:a.Limp?.55f:1);
                    }
                    if(a.Stats.id=="spitter"&&!a.Possessed&&MeleeVisible(at,target.Body.position,m.Owner,target.Collider,true))dir=NativeSpitterRules.KeepDistance(distance)<0?-d.normalized:NativeSpitterRules.KeepDistance(distance)==0?Vector3.zero:dir;
                    if(a.Stats.id=="stoker"&&ZoneVisible(m,target,5))dir=distance<3?-d.normalized:distance<=4.5f?Vector3.zero:dir;
                    if(a.Stats.id=="cinderling")dir=Quaternion.Euler(0,(m.Id%3-1)*25,0)*dir;
                    move=distance>1.1f||a.Possessed||a.Stats.id=="spitter"?dir*speed:Vector3.zero;if(!SupportedStageMove(navAt,move))move=Vector3.zero;
                }
                if(move.sqrMagnitude>.001f){float factor=Time<a.SlowUntil?a.SlowFactor:1;foreach(var f in ChipFields)if(CanHit(f.Owner,m)&&Vector3.Distance(f.Position,at)<f.Radius)factor=Math.Min(factor,f.Kind=="frost"?.45f:f.Kind=="time"?.6f:1);move*=factor*(a.Zone.aboardUntil>Time?1.25f:1)*(a.Stats.id=="slagback"&&a.Zone.shedAt>=0?1.25f:1);move+=HordeSeparation(m,at);float oldYaw=m.Yaw;m.Yaw=Mathf.Atan2(move.x,move.z)*Mathf.Rad2Deg;if(a.Stats.id=="pileup"||a.Stats.id=="slagback")m.Yaw=Mathf.MoveTowardsAngle(oldYaw,m.Yaw,(a.Stats.id=="pileup"?60:90)*dt);}
                if(!SupportedStageMove(navAt,move))move=Vector3.zero;a.External*=Mathf.Exp(-6*dt);move+=Vector3.ClampMagnitude(a.External,6);if(HubCross(at,at+move*dt,m.Collider.radius))move=Vector3.zero;m.Body.linearVelocity=new Vector3(move.x,m.Body.linearVelocity.y,move.z);
            }
        }
        private bool TryVentTravel(NativeMinion m,NativePilot target)
        {
            var a=m.Pve;if(HubOn||NativeZoneCombat.Active(MapId))return false;if(a.Stats.id=="hulk"||a.Crawl||!a.Struck||Time<a.VentReady||Time-a.LastDamage<2||a.Possessed&&Time-a.PossessedAt<10)return false;
            int e=Array.FindIndex(StageVents,v=>Math.Abs(v.y-m.Body.position.y)<2&&new Vector2(v.x-m.Body.position.x,v.z-m.Body.position.z).magnitude<=2);if(e<0||pveNavigation.Distance(m.Body.position,target.Body.position,target.Seat.id)<=28||Minions.Count(n=>n.Pve?.Travelling==true)>=2||VentHazard(e,.9f)||Pve.SpawnRandom.Next()>=.25)return false;
            a.Entry=Pve.Content.entries.First(x=>x.e==e);a.Travelling=true;a.DiveAt=Time;a.VentUntil=Time+2.4f;a.VentReady=Time+20;a.External=Vector3.zero;m.Body.linearVelocity=Vector3.zero;m.Body.isKinematic=true;m.Collider.enabled=false;m.Body.position=StageVents[e]+Vector3.up*.9f;m.Yaw=Mathf.Atan2(-m.Body.position.x,-m.Body.position.z)*Mathf.Rad2Deg;DiveEntry(a.Entry);return true;
        }
        private void Reemerge(NativeMinion m)
        {
            var a=m.Pve;var entry=Pve.Content.entries.Where(e=>e.kind=="vent"&&(e.surface=="floor"||string.IsNullOrEmpty(e.surface))&&EntryQueue(e.e)<3&&!VentHazard(e.e)).OrderBy(e=>a.Target==null?e.e:pveNavigation.Distance(Vec(e.@exit.Position(.9f)),a.Target.Body.position,a.Target.Seat.id)).FirstOrDefault();if(entry==null)return;
            var times=PlanEntry(entry);a.Entry=entry;a.Travelling=false;a.First=times.first;a.SpawnAt=times.spawn;a.WalkAt=times.walk;a.ActiveAt=times.active;a.ExposedAt=times.active-1.15f;m.Yaw=entry.yaw*Mathf.Rad2Deg;
        }
        private void PveAddStrike(NativeMinion m)
        {
            var a=m.Pve;if(a.Stats.id=="hulk"&&a.Move!=0&&!a.Crawl&&!NativeHordeKinds.Armless(a.Lost)){HulkMoveStrike(m);return;}if(a.Stats.id=="spitter"){LaunchPveSpit(m);return;}Vector3 at=m.Body.position,dir=Vec(V3.Forward(m.Yaw));bool left=(a.Lost&(2|8))!=0,right=(a.Lost&(4|16))!=0,armless=left&&right;float damage=a.Stats.damage*a.DamageScale*(a.Crawl?.6f:armless?.35f:left!=right?.65f:1),reach=a.Crawl?1.3f:armless?1.2f:a.Stats.reach,impulse=a.Stats.impulse*(a.Crawl||armless?.5f:1);
            bool smash=a.Stats.attack=="smash"&&!a.Crawl;
            if(smash&&a.Stats.id=="brute")Emit(EventKind.Impact,m.Owner,m.Id,at+dir*1.2f-Vector3.up*.9f,0,"minion-slam",radius:1.6f);
            foreach(var q in Pilots)
            {
                if(q.Hp<=0||!q.Seat.connected||q.Seat.AuthorityBoss)continue;Vector3 d=q.Body.position-at;float dist=new Vector2(d.x,d.z).magnitude;
                if(a.Stats.attack=="smash"&&!a.Crawl){Vector3 center=at+dir*NativeHordeKinds.SmashAheadOf(NativeHordeKinds.Resolve(a.Stats.id));if(new Vector2(q.Body.position.x-center.x,q.Body.position.z-center.z).magnitude>NativeHordeKinds.SmashRadiusOf(NativeHordeKinds.Resolve(a.Stats.id)))continue;}
                else if(q!=a.Target||dist>reach||dist>.001f&&Vector3.Dot(dir,new Vector3(d.x,0,d.z).normalized)<(a.Stats.attack=="lunge"?.5f:.35f))continue;
                if(Math.Abs(d.y)>(a.Stats.id=="hulk"?2.5f:1.8f)||!MeleeVisible(at+Vector3.up*(a.Crawl?.1f:.45f),q.Body.position,m.Owner,q.Collider,true)||Invulnerable(q))continue;
                var push=new Vector3(d.x,0,d.z).normalized;
                if(a.Stats.id=="hulk"){var away=q.Body.position-(at+dir*NativeHordeKinds.HulkSmashAhead);away.y=0;push=away.normalized;}
                if(Time<q.BlockEnd){BlockProc(q,m.Owner,q.Collider.ClosestPoint(m.Body.position),"clash");if(a.Stats.id=="hulk")q.Knockback+=push*3;else{a.StaggerAt=Time+.6f;a.External=-d.normalized*3;}}
                else if(smash&&a.Stats.id=="brute")
                {var shove=q.Body.position-(at+dir*NativeHordeKinds.SmashAheadOf(NativeHordeKinds.Brute));float actual=Damage(q,null,damage,Vec(NativeHordeDropRules.Outward(Vec(shove),Vec(dir))),0,false,primary:false,melee:true);if(actual>0)PveSlamShove(q,shove,dir,impulse,false);}
                else{Damage(q,null,damage,push,impulse,false,primary:false,melee:true);if(a.Stats.id=="hulk")q.Knockback.y=Math.Max(q.Knockback.y,4);}
            }
        }
        private float HitPveAdd(NativeMinion m,NativePilot source,float amount,Vector3 dir,float impulse,Vector3? point,string kind,float radius,bool head,int projectile=0,bool pierced=false,Vector3? from=null)
        {
            var a=m.Pve;if(HubDamageBlocked(source,m.Body.position,from,projectile))return 0;if(source==null||a.Travelling||Time<a.ExposedAt||Pve?.Holding==true)return 0;
            var hitData=new NativeHordeHit(amount);
            // The fallback has no limb damage or blast-share rolls. Resolve only deterministic direct-hit anatomy.
            if(Pve==null&&kind!="blast")hitData.Share(NativeHordeDamage.Part(new V3(-m.Body.position.x,m.Body.position.y,m.Body.position.z),-m.Yaw,a.Crawl,point.HasValue?(V3?)new V3(-point.Value.x,point.Value.y,point.Value.z):null,head),amount);
            if(Pve==null&&kind=="blast")hitData.Head=head;
            if(Pve==null){float hit=Math.Min(m.Hp,amount);m.Hp-=hit;m.LastHitAt=Time;CancelHulkWind(m,hit);if(!a.Charging&&NativeHordeKinds.Staggers(NativeHordeKinds.Resolve(a.Stats.id),hit,m.MaxHp,impulse>=400)){a.StaggerAt=Time+a.Stats.stagger;a.Struck=true;}Emit(EventKind.Hit,source,m.Id,m.Body.position,hit,kind,from:from,projectile:projectile,pierced:pierced,hordeHit:hitData);if(m.Hp<=0)RemoveMinion(m);return hit;}float actual=0;amount=ZoneIncoming(m,source,amount,dir,point,head,impulse,kind);if(a.ZoneSelfDamage>0){actual=Math.Min(m.Hp,a.ZoneSelfDamage);m.Hp-=actual;a.ZoneSelfDamage=0;}
            foreach(var share in NativeHordeDamage.Shares(amount,new V3(-m.Body.position.x,m.Body.position.y,m.Body.position.z),-m.Yaw,a.Crawl,point.HasValue?(V3?)new V3(-point.Value.x,point.Value.y,point.Value.z):null,kind,radius>0?radius:3,head,Pve.SpawnRandom.Next))
            {
                hitData.Share(share.part,share.damage);
                int limb=(a.Lost&share.part)!=0?0:share.part;float applied=share.damage*(limb==1?1.5f:limb==0?1:.9f),taken=Math.Max(0,Math.Min(m.Hp,applied));m.Hp-=taken;actual+=taken;
                // A severed limb leaves a red stump and a thrown limb (NativeGorePresentation / horde gore, from PveGib). The first port also recorded a fire-school ability effect here, which drew a fireball on
                // every gun kill of a Siren add (owner playtest 2026-10-10). Presentation feed only; damage and limbs are unchanged.
                if(limb>0){a.Parts.TryGetValue(limb,out float pool);pool+=taken;a.Parts[limb]=pool;if(pool>=m.MaxHp*(limb==1?a.Stats.headFrac:a.Stats.limbFrac)){a.Lost|=NativeHordeDamage.Descendants(limb);Emit(EventKind.PveGib,source,m.Id,point??m.Body.position,limb);if(limb==1)m.Hp=0;bool l=(a.Lost&(32|128))!=0,r=(a.Lost&(64|256))!=0;a.Crawl=(a.Lost&(32|64))!=0||l&&r;a.Limp=!a.Crawl&&(l||r);if(a.Crawl){m.Collider.height=.88f;m.Collider.radius=.32f;}}}
            }
            if(NativeSpitterRules.Interrupt(actual,Time,m.LastAttack,a.Struck)&&a.Stats.id=="spitter")
            {a.SpitCancelAt=Time;a.Struck=true;a.RecoverAt=NativeSpitterRules.Retry(Time);Emit(EventKind.Impact,source,m.Id,m.Body.position,0,"minion-spit-cancel");}
            a.HubCredit=!HubDamageBlocked(source,m.Body.position,from,projectile);a.LastHit=source;a.LastDamage=Time;a.Dealt.TryGetValue(source.Seat.id,out float dealt);a.Dealt[source.Seat.id]=dealt+actual;
            float zoneStagger=a.StaggerAt;CancelHulkWind(m,actual>0?amount:0);
            if(!a.Charging)if(NativeHordeKinds.Staggers(NativeHordeKinds.Resolve(a.Stats.id),amount,m.MaxHp,impulse>=400)){a.StaggerAt=Time+a.Stats.stagger;a.Struck=true;if(a.Stats.id!="spitter"||actual<=0)a.RecoverAt=Time;}if(Time>=a.ExposedAt)a.External=Vector3.ClampMagnitude(a.External+new Vector3(dir.x,0,dir.z)*NativePveParity.AddImpulse(impulse),6);
            if(NativeZoneCombat.Active(MapId))a.StaggerAt=Math.Max(a.StaggerAt,zoneStagger);
            m.LastHitAt=Time;Emit(EventKind.Hit,source,m.Id,point??m.Body.position,actual,kind=="energy"?"ability":kind=="projectile"||kind==null?"direct":kind=="dot"?"field":kind,normal:dir.x*dir.x+dir.z*dir.z>1e-12f?dir:Vector3.zero,from:from,projectile:projectile,pierced:pierced,hordeHit:hitData);
            MinionImpact(m,source,actual,point,kind,dir);
            if(m.Hp<=0){Vector3 at=m.Body.position;ZoneEnemyDeath(m,source,kind=="melee"||kind=="slash"||kind=="blunt");PveAddKilled(m,source);RemoveMinion(m,false);if(a.Possessed)FoundPveBoss(at);}return actual;
        }
        private void PveAddKilled(NativeMinion m,NativePilot source)
        {
            if(Expedition!=null){ExpeditionKilled(m,source);return;}
            if(m.DevMonster)return; // Defense in depth: environmental kills can also reach this adapter.
            var a=m.Pve;var recruit=Pve.Recruits.Find(r=>r.Pilot==source);NativePilot paid=recruit?.Owner??source;bool head=(a.Lost&1)!=0;float reward=a.Stats.reward;
            if(recruit!=null)GrantPveMoney(paid,(reward+(head?10:0))*.5f,"recruit");else{GrantPveMoney(paid,reward,"kill");if(head)GrantPveMoney(paid,10,"head");}
            if(Pve.Rows.TryGetValue(paid.Seat.id,out var row))row.Kills++;
            foreach(var hit in a.Dealt)if(hit.Key!=source.Seat.id&&hit.Key!=paid.Seat.id&&hit.Value>=m.MaxHp*.2f){var p=Pilots.Find(x=>x.Seat.id==hit.Key);GrantPveMoney(p,reward*.4f,"assist");if(Pve.Rows.TryGetValue(hit.Key,out var assist))assist.Assists++;}
            if(a.Possessed)foreach(var p in Members())GrantPveMoney(p,p==paid?300:100,"possessed");Emit(EventKind.Death,source,m.Id,m.Body.position,0);
        }
        private void StepPveRecruits()
        {
            foreach(var hire in Pve.Hires.Where(h=>Time>=h.at).ToArray())
            {
                Pve.Hires.Remove(hire);var row=Pve.Rows[hire.owner.Seat.id];if(Pilots.Count>=8||Pve.Recruits.Count>=3){if(Expedition!=null)GrantPveMoney(hire.owner,300,"hire-refund");else row.Money+=300;row.Spent-=300;continue;}double roll=Pve.EconomyRandom.Next();string role=roll<.4?"dps":roll<.7?"tank":"medic",spec=role=="tank"?"shield":role=="medic"?"pistol":new[]{"pistol","rifle","machinegun","sword","daggers","shield","greatsword"}[(int)(Pve.EconomyRandom.Next()*7)];var seat=new NativeSeat{id=nextRecruit++,name=role=="dps"?"Recruit":role=="tank"?"Guardian":"Medic",spec=spec,team=0,bot=true};Match.Seats.Add(seat);var p=SpawnPilot(seat,0);p.Stats["hp"]=role=="tank"?250:role=="medic"?80:100;if(role=="tank")p.Stats["damageTaken"]*=.7;p.Hp=(float)p.Stats["hp"];var machine=Pve.Content.machines[hire.machine];p.Body.position=Vec(machine.dispense.Position(.9f));Pve.Recruits.Add(new NativePveRecruit{Pilot=p,Owner=hire.owner,Role=role,At=Time});row.Hired++;
            }
            MedicPulse();
            foreach(var recruit in Pve.Recruits.ToArray())
            {
                var p=recruit.Pilot;if(p.Hp<=0){if(recruit.RemoveAt==0)recruit.RemoveAt=Time+2.5f;if(Time>=recruit.RemoveAt){Pve.Recruits.Remove(recruit);Pilots.Remove(p);Match.Seats.Remove(p.Seat);CapturedAnimation.Release(p.Body.gameObject);}continue;}

            }
        }
        private InputFrame PveBot(NativePilot p,InputFrame input)
        {
            if(Pve==null)return input;if(p.Seat.AuthorityBoss)return SirenBot(p,input);var recruit=Pve.Recruits.Find(r=>r.Pilot==p);var anchor=recruit?.Owner.Hp>0?recruit.Owner:Members().Where(q=>!q.Seat.bot&&q.Hp>0).OrderBy(q=>(q.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();
            var minion=Minions.Where(m=>m.Pve!=null&&m.Hp>0&&Time>=m.Pve.ExposedAt&&(anchor==null||Vector3.Distance(m.Body.position,anchor.Body.position)<=18)&&MeleeVisible(p.Body.position+Vector3.up*.5f,m.Body.position,p,m.Collider)).OrderBy(m=>m.Pve.Target==anchor?0:1).ThenBy(m=>(m.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();Vector3? target=minion?.Body.position??(!Pve.Hidden&&Pve.Boss.Hp>0?(Vector3?)Pve.Boss.Body.position:null);
            if(recruit?.Role=="medic")target=null;Vector3 move=Vector3.zero;
            // PvE fighters must fight even when saved PvP practice settings specify
            // stationary targets or dummies. Medics still have no combat target.
            if(target.HasValue){input=BotFocus(p,target.Value,minion!=null?MinionVelocity(minion):PilotVelocity(Pve.Boss),minion?.Collider??Pve.Boss.Collider,minion==null?Pve.Boss:null,force:true);move=new Vector3(input.X,0,input.Z);}else input=new InputFrame{Yaw=p.Yaw};
            if(anchor!=null&&Vector3.Distance(anchor.Body.position,p.Body.position)>(target.HasValue?12:5))move=Map.unity?StructureNavMove(p,anchor.Body.position,(anchor.Body.position-p.Body.position).normalized):pveNavigation?.Direction(p.Body.position,anchor.Body.position,anchor.Seat.id)??(anchor.Body.position-p.Body.position).normalized;if(!SupportedStageMove(p.Body.position,move)){move=Vector3.zero;input.Jump=input.Roll=input.Sprint=false;}input.X=move.x;input.Z=move.z;return input;
        }
        private InputFrame SirenBot(NativePilot p,InputFrame input)
        {
            if(Pve.Hidden)return new InputFrame{Yaw=p.Yaw};var q=Members().Where(t=>t.Hp>0&&t.Seat.connected).OrderBy(t=>(t.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();if(q==null)return new InputFrame{Yaw=p.Yaw};p.Seat.difficulty=NativeBotRules.BossDifficulty(PartySize);
            input=BotFocus(p,q.Body.position,PilotVelocity(q),q.Collider,q,force:true);var s=Siren(p);float distance=Vector3.Distance(q.Body.position,p.Body.position);
            Vector3 move=distance>(s.Ranged?12:2)?(Map.unity?StructureNavMove(p,q.Body.position,(q.Body.position-p.Body.position).normalized):pveNavigation.Direction(p.Body.position,q.Body.position,q.Seat.id)):Vector3.zero;if(!SupportedStageMove(p.Body.position,move)){move=Vector3.zero;input.Jump=input.Roll=input.Sprint=false;}input.X=move.x;input.Z=move.z;return input;
        }
    }
    internal sealed class NativePveNavigation
    {
        private readonly NativeArena arena;private readonly bool[] blocked=new bool[63*55];private readonly Dictionary<int,(int target,int version,int[] steps)> flows=new Dictionary<int,(int,int,int[])>();private int version,propCount=-1,gridAt=-1;private float poll,gridTick=-1;private readonly bool[] rebuilding=new bool[63*55];private readonly int[] queue=new int[63*55];private readonly Dictionary<int,float> flowReady=new Dictionary<int,float>();private Collider[] overlaps=new Collider[32];
        public NativePveNavigation(NativeArena arena){this.arena=arena;}
        private static int Cell(Vector3 p)=>Mathf.Clamp(Mathf.RoundToInt(p.x)+31,0,62)+Mathf.Clamp(Mathf.RoundToInt(p.z)+27,0,54)*63;
        private static Vector3 Center(int i)=>new Vector3(i%63-31,.9f,i/63-27);
        private void Grid()
        {
            if(gridTick==arena.Time)return;gridTick=arena.Time;
            if(gridAt<0){if(arena.Time<poll&&propCount>=0)return;poll=arena.Time+.5f;if(propCount==arena.NavigationRevision)return;propCount=arena.NavigationRevision;gridAt=0;}
            var scene=arena.Root.scene.GetPhysicsScene();int end=Math.Min(blocked.Length,gridAt+256);
            for(;gridAt<end;gridAt++){Vector3 at=Center(gridAt);int n;while((n=scene.OverlapCapsule(at-Vector3.up*.2f,at+Vector3.up*.5f,.43f,overlaps,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))==overlaps.Length)Array.Resize(ref overlaps,overlaps.Length*2);rebuilding[gridAt]=false;for(int k=0;k<n;k++)if(overlaps[k].bounds.max.y>.18f){rebuilding[gridAt]=true;break;}}
            if(gridAt==blocked.Length){Array.Copy(rebuilding,blocked,blocked.Length);version++;gridAt=-1;}
        }

        private int[] Flow(Vector3 target,int id)
        {
            Grid();int cell=Cell(target);bool exists=flows.TryGetValue(id,out var cached);
            if(exists&&((cached.target==cell&&cached.version==version)||flowReady.TryGetValue(id,out float ready)&&arena.Time<ready))return cached.steps;
            int[] steps=exists?cached.steps:new int[blocked.Length];for(int i=0;i<steps.Length;i++)steps[i]=int.MaxValue;int read=0,write=0;steps[cell]=0;queue[write++]=cell;
            void Visit(int to,int cost){if(to<0||blocked[to]||steps[to]<=cost)return;steps[to]=cost;queue[write++]=to;}
            while(read<write){int at=queue[read++],x=at%63,z=at/63,cost=steps[at]+1;Visit(x>0?at-1:-1,cost);Visit(x<62?at+1:-1,cost);Visit(z>0?at-63:-1,cost);Visit(z<54?at+63:-1,cost);}
            flows[id]=(cell,version,steps);flowReady[id]=arena.Time+.12f;return steps;
        }

        public float Distance(Vector3 from,Vector3 to,int id){if(arena.Map.unity)return Vector3.Distance(from,to)+Math.Abs(from.y-to.y)*2;var f=Flow(to,id);return f[Cell(from)]==int.MaxValue?1000+Vector3.Distance(from,to):f[Cell(from)];}
        public Vector3 Direction(Vector3 from,Vector3 to,int id)
        {
            var delta=to-from;delta.y=0;if(!arena.Root.scene.GetPhysicsScene().SphereCast(from+Vector3.up*.15f,.42f,delta.normalized,out _,delta.magnitude,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))return delta.normalized;
            var f=Flow(to,id);int cell=Cell(from),best=cell,score=f[cell];for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++){int tx=cell%63+x,tz=cell/63+z;if(tx<0||tx>62||tz<0||tz>54)continue;int next=tx+tz*63;if(x!=0&&z!=0&&(blocked[cell+x]||blocked[cell+z*63]))continue;if(!blocked[next]&&f[next]<score){best=next;score=f[next];}}
            Vector3 dir=Center(best)-from;dir.y=0;return dir.normalized;
        }
    }
}
