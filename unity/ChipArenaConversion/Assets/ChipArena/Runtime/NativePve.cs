using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed class NativePvePick {public int Id;public string Tier,Source="start";public List<string> Offers;public float At;}
    public sealed class NativePveRow
    {public string Notice;public float NoticeUntil;public NativePilot Pilot;public int Money,Earned,Spent,Kills,Assists,Deaths,Revived,Revives,Bought,Hired,SelfRevives;public float SelfAt=-1,HealAt=-1,HealReady,ThinkAt,DamageBoss;public int HealPulses=4,BossPaid;public readonly List<NativePvePick> Picks=new List<NativePvePick>();public NativePvePick Open;}
    public sealed class NativePvePickup {public int Id,Machine;public NativePilot Owner;public string Tier,Source="start";public Vector3 Position;public float Born;}
    public sealed class NativePveRecruit {public NativePilot Pilot,Owner;public string Role;public float At,HealAt,RemoveAt;}
    public sealed class NativePveState
    {
        public readonly List<NativeMachineUse> Machines=new List<NativeMachineUse>();public NativePveContent Content;public NativeWaveSchedule Schedule;public NativePilot Boss;public bool Skip,Started,Hidden,Holding;public string Stage="opening",Cinematic,Outcome,Notice;public int Wave,Phase=1,Reveal,SpawnBudget,Spawned,WaveTotal;public float Rate,SpawnAt,StageAt,StageUntil,ResolvedAt=-1,HoldLeft,HoldDuration,PossessionAt,Floor;public int Cap,Possessed,DropCursor;public bool Solo;public float MaxHp;
        public readonly Dictionary<int,NativePveRow> Rows=new Dictionary<int,NativePveRow>();public readonly List<NativePvePickup> Pickups=new List<NativePvePickup>();public readonly List<NativePveRecruit> Recruits=new List<NativePveRecruit>();
        public readonly List<(NativePilot owner,float at,int machine)> Hires=new List<(NativePilot,float,int)>();public NativePveRandom SpawnRandom,EconomyRandom;
    }
    public sealed partial class NativeArena
    {
        public NativePveState Pve{get;private set;}private int nextPick=1,nextRecruit=200;
        public void ConfigurePve(NativePveContent data,bool skip,uint seed=12345)
        {
            if(Map.look=="quarantine")NativeWardEntries.Validate(data,Map);
            uint runSeed=NativePveRandom.Hash(seed,0);Pve=new NativePveState{Content=data,Skip=skip,SpawnRandom=new NativePveRandom(NativePveRandom.Hash(runSeed,"horde")),EconomyRandom=new NativePveRandom(NativePveRandom.Hash(runSeed,"economy"))};Match.Cooperative=Match.Teams=true;FriendlyFire=false;Pve.Boss=Pilots.Find(p=>p.Seat.AuthorityBoss);if(Pve.Boss==null)throw new InvalidOperationException("PvE requires a reserved authority boss seat");SetBossHidden(true);
            BuildPveMachines(data);
        }
        private void BuildPveMachines(NativePveContent data)
        {
            foreach(var def in data.machineBlocks){var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=def.id;obj.layer=PropLayer;obj.transform.SetParent(root.transform);obj.transform.position=Vec(def.pos);obj.transform.rotation=new Quaternion(def.rotation.x,def.rotation.y,def.rotation.z,def.rotation.w);obj.transform.localScale=Vec(def.size);var prop=obj.AddComponent<NativePropBody>();prop.Definition=def;prop.LocalFixture=true;prop.Indestructible=true;prop.Health=float.PositiveInfinity;obj.GetComponent<Renderer>().sharedMaterial=materials["foundation"];Props.Add(prop);}
        }
        private IEnumerable<NativePilot> Members()=>Pilots.Where(p=>!p.Seat.AuthorityBoss&&!p.Seat.devDummy&&!Pve.Recruits.Any(r=>r.Pilot==p));
        private int PartySize=>Math.Max(1,Members().Count(p=>p.Seat.connected||p.Seat.bot));
        private void SetBossHidden(bool hide){Pve.Hidden=hide;var b=Pve.Boss;b.Seat.connected=!hide;b.Collider.enabled=!hide;b.Body.isKinematic=hide;if(hide)b.Body.position=new Vector3(0,-100,0);}
        private void StartPve()
        {
            var v=Pve;v.Started=true;v.Schedule=new NativeWaveSchedule(Time,v.Skip);v.StageAt=Time;v.StageUntil=Time+10;v.Solo=Members().Count()==1;
            foreach(var p in Members()){var row=new NativePveRow{Pilot=p,Money=p.Seat.bot?0:150,Earned=p.Seat.bot?0:150,SelfRevives=!p.Seat.bot&&v.Solo?1:0};v.Rows[p.Seat.id]=row;GrantPvePick(p,"common",true);}
        }
        private void GrantPveMoney(NativePilot p,float amount,string reason)
        {if(p==null||p.Seat.bot||!Pve.Rows.TryGetValue(p.Seat.id,out var row))return;int n=NativePveRules.Money(p.Seat.chips,reason,amount);row.Money=Math.Max(0,row.Money+n);if(n>0){row.Earned+=n;Emit(EventKind.PveMoney,p,p.Seat.id,p.Body.position,n,reason);}}
        private void GrantPvePick(NativePilot p,string tier,bool open,string source="start")
        {var row=Pve.Rows[p.Seat.id];var pick=new NativePvePick{Id=nextPick++,Tier=tier,Source=source,At=Time};row.Picks.Add(pick);if(open)OpenPvePick(p,pick.Id);}
        public bool OpenPvePick(NativePilot p,int id=0)
        {
            if(Pve==null||Pve.Holding||!Pve.Rows.TryGetValue(p.Seat.id,out var row))return false;var pick=id==0?row.Picks.FirstOrDefault():row.Picks.Find(x=>x.Id==id);if(pick==null)return false;
            if(pick.Offers==null)RollPveOffers(p,pick);row.Open=pick;row.ThinkAt=Time+2+(float)Pve.EconomyRandom.Next()*4;return true;
        }
        public void ClosePvePick(NativePilot p){if(Pve!=null&&Pve.Rows.TryGetValue(p.Seat.id,out var row))row.Open=null;}
        private void RollPveOffers(NativePilot p,NativePvePick pick)
        {
            bool rainbow=pick.Tier=="rainbow";var weights=Pve.Content.offerWeights.First(x=>x.id==pick.Tier).weights.ToDictionary(x=>x.name,x=>x.value);if(p.Seat.spec=="leet"&&!rainbow)weights["l44t"]=weights["legendary"];
            const int count=3;pick.Offers=NativeContentRules.Offers(Content,p.Seat.spec,p.Seat.chips,Pve.EconomyRandom.Next,count,c=>ImplementedChip(c)&&c.rarity!="siren",weights,!rainbow);
            if(!p.Seat.bot&&!rainbow&&Pve.EconomyRandom.Next()<.3){var available=Pve.Content.moneyChips.Where(c=>!p.Seat.chips.TryGetValue(c.id,out int n)||n<c.max).ToArray();if(available.Length>0){var tiers=Content.rarities.Where(r=>weights.TryGetValue(r.id,out double w)&&w>0&&available.Any(c=>c.rarity==r.id)).Select(r=>new NumberValue{name=r.id,value=weights[r.id]}).ToArray();string tier=tiers.Length>0?NativePveRules.Weighted(tiers,Pve.EconomyRandom.Next):null;var pool=tier!=null?available.Where(c=>c.rarity==tier).ToArray():available;if(pool.Length==0)pool=available;var chosen=pool[Math.Min(pool.Length-1,(int)(Pve.EconomyRandom.Next()*pool.Length))];if(pick.Offers.Count>=count)pick.Offers.RemoveAt(pick.Offers.Count-1);pick.Offers.Add(chosen.id);}}
        }
        public ContentChip PveChip(string id)=>Content.chips.FirstOrDefault(c=>c.id==id)??Pve?.Content.moneyChips.FirstOrDefault(c=>c.id==id);
        public bool ChoosePve(NativePilot p,int pickId,string chip)
        {
            if(Pve==null||Pve.Holding||!Pve.Rows.TryGetValue(p.Seat.id,out var row)||row.Open?.Id!=pickId||row.Open.Offers==null||!row.Open.Offers.Contains(chip))return false;var definition=PveChip(chip);if(definition==null||definition.rarity=="siren")return false;
            if(chip.StartsWith("mishima-")&&!Dev(p,"emperor")||chip.StartsWith("arcade-")&&!Dev(p,"ansatsuken")||chip.StartsWith("l44t-")&&!Dev(p,"leet"))return false;
            bool money=definition.mechanic=="pve-money";p.Seat.chips.TryGetValue(chip,out int count);if(count>=definition.max||!money&&!NativeContentRules.Eligible(Content,p.Seat.spec,p.Seat.chips).Any(c=>c.id==chip)){RollPveOffers(p,row.Open);return false;}
            Emit(EventKind.Install,p,p.Seat.id,p.Body.position,count+1,chip);p.Seat.chips[chip]=count+1;RefreshLiveStats(p);row.Picks.Remove(row.Open);row.Open=null;if(row.Picks.Count>0)OpenPvePick(p);return true;
        }
        private void RefreshLiveStats(NativePilot p)
        {float hp=(float)p.Stats["hp"];p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);if(p.Hp>0)p.Hp=Math.Min((float)p.Stats["hp"],p.Hp+Math.Max(0,(float)p.Stats["hp"]-hp));p.Mana=Math.Min(p.Mana,(float)p.Stats["maxMana"]);p.Stamina=Math.Min(p.Stamina,(float)p.Stats["maxStamina"]);p.Ammo=Math.Min(p.Ammo,Magazine(p));}
        private void PveInput(NativePilot p)
        {
            if(Expedition!=null||Pve==null||!Pve.Rows.TryGetValue(p.Seat.id,out var row))return;CollectPvePickups(p);if(p.Input.Interact&&!p.Seat.bot&&p.Hp>0&&row.Open==null)PveInteract(p,row);
            if(row.Open!=null){if(p.Seat.bot&&Time>=row.ThinkAt){var best=row.Open.Offers.Where(id=>PveChip(id)?.mechanic!="pve-money").OrderByDescending(id=>NativeChipRanks.BotRank(PveChip(id)?.rarity)).FirstOrDefault();if(best!=null)ChoosePve(p,row.Open.Id,best);}if(row.Open!=null)p.Input=new InputFrame{Yaw=p.Input.Yaw,Pitch=p.Input.Pitch};}
        }
        private void CollectPvePickups(NativePilot p)
        {
            if(Replica||Pve==null||Pve.Holding||p.Seat.bot||!p.Seat.connected||p.Hp<=0)return;
            // The host collects personal drops during normal movement. Queue the choice without opening a combat-blocking screen.
            for(int i=0;i<Pve.Pickups.Count;)
            {
                var pickup=Pve.Pickups[i];var delta=pickup.Position-p.Body.position;
                if(pickup.Owner==p&&Time>=pickup.Born&&delta.x*delta.x+delta.z*delta.z<=1.6f*1.6f&&Math.Abs(delta.y)<=1.2f)
                {Pve.Pickups.RemoveAt(i);GrantPvePick(p,pickup.Tier,false,"machine");}
                else i++;
            }
        }
        private void PveInteract(NativePilot p,NativePveRow row)
        {
            float Flat(Vector3 q)=>Math.Abs(q.y-p.Body.position.y)>1.7f?float.PositiveInfinity:new Vector2(q.x-p.Body.position.x,q.z-p.Body.position.z).magnitude;
            var dead=Members().Where(q=>q.Hp<=0&&q.Seat.connected&&!InShaft(q)&&Flat(q.Body.position)<=2.2f).OrderBy(q=>Flat(q.Body.position)).FirstOrDefault();if(dead!=null){var target=Pve.Rows[dead.Seat.id];int price=NativePveRules.RevivePrice(target.Revived);if(row.Money<price){row.Notice="Not enough money to revive";row.NoticeUntil=Time+1.4f;return;}row.Money-=price;row.Spent+=price;row.Revives++;target.Revived++;RevivePve(dead);return;}
            var m=Pve.Content.machines.Where(x=>Flat(Vec(x.front.Position()))<=2.2f).OrderBy(x=>Flat(Vec(x.front.Position()))).FirstOrDefault();if(m==null)return;void Use(string result){Pve.Machines.RemoveAll(u=>Time-u.at>4);while(Pve.Machines.Count>=32)Pve.Machines.RemoveAt(0);Pve.Machines.Add(new NativeMachineUse{machine=m.m,owner=p.Seat.id,result=result,at=Time});}int cost=m.kind=="chip"?NativePveRules.ChipPrice(row.Bought):m.kind=="recruit"?300:75;
            if(row.Money<cost){row.Notice="Not enough money";row.NoticeUntil=Time+1.4f;Use("deny");return;}
            if(m.kind=="chip")
            {if(Pve.Pickups.Count(k=>k.Owner==p)>=3){row.Notice="Collect your waiting Chips first";row.NoticeUntil=Time+1.4f;Use("deny");return;}Pve.Pickups.Add(new NativePvePickup{Id=nextPick++,Machine=m.m,Owner=p,Tier=NativePveRules.Weighted(Pve.Content.dropOdds,Pve.EconomyRandom.Next),Position=Vec(m.chipDrop.Position(.9f)),Born=Time+.6f});row.Bought++;}
            else if(m.kind=="recruit"){if(Pve.Recruits.Count(r=>r.Owner==p)+Pve.Hires.Count(h=>h.owner==p)>=2||Pve.Recruits.Count+Pve.Hires.Count>=3||Pilots.Count+Pve.Hires.Count>=8){row.Notice="Recruit limit reached";row.NoticeUntil=Time+1.4f;Use("deny");return;}Pve.Hires.Add((p,Time+.8f,m.m));}
            else{if(p.Hp>=p.Stats["hp"]){row.Notice="Health is full";row.NoticeUntil=Time+1.4f;Use("deny");return;}if(Time<row.HealReady){row.Notice="Med station is recharging";row.NoticeUntil=Time+1.4f;Use("deny");return;}row.HealAt=Time;row.HealReady=Time+12;row.HealPulses=0;}
            row.Money-=cost;row.Spent+=cost;Use(m.kind);Emit(EventKind.PveBuy,p,m.m,p.Body.position,cost,m.kind);row.Notice="";row.NoticeUntil=Time+1.4f;
        }
        private void RevivePve(NativePilot p,Vector3? respawn=null)
        {
            p.RevivedAt=Time;p.Wounds.Clear();p.AirJumps=0;p.AirJumpAt=-100;
            // The source draws no blast for pve-revive (pve/hud.ts REVIVED banner, audio.ts `go` cue). The first port recorded a
            // water-school ability effect here, which drew a frost blast. Presentation feed only.

            CancelSiren(p);CancelFighter(p);CancelMelee(p);CancelSpecial(p);CancelAbilities(p);p.Down=null;p.Juggle=null;p.Roll=null;p.RollEnd=p.StunEnd=0;p.ScareBy=null;p.ScareUntil=p.FrozenUntil=0;ReleaseControl(p);p.Dots.Clear();RefreshLiveStats(p);p.Hp=(float)p.Stats["hp"]*.5f;p.ShieldUntil=Time+1.5f;p.Seat.alive=true;p.Collider.enabled=true;p.Body.isKinematic=false;p.Body.position=respawn??p.Body.position;p.Knockback=p.MoveVelocity=p.Body.linearVelocity=Vector3.zero;Pve.Rows[p.Seat.id].SelfAt=-1;Emit(EventKind.PveRevive,p,p.Seat.id,p.Body.position,p.Hp);
        }
        private void PveDied(NativePilot p)
        {if(Pve==null||!Pve.Rows.TryGetValue(p.Seat.id,out var row))return;row.Deaths++;row.Open=null;if(Pve.Solo&&row.SelfRevives>0&&!p.Seat.bot)row.SelfAt=Time+5;}
        public bool BossVictoryWalk=>Pve!=null&&Pve.Stage=="victory"&&Match.Phase==NativePhase.Fight;
        private void BeginBossVictory()
        {
            var v=Pve;if(v.Stage=="victory"||v.Stage=="ended")return;
            v.Holding=false;v.Cinematic=null;v.Outcome="victory";v.Stage="victory";v.StageAt=Time;v.StageUntil=Time+5;v.Rate=0;v.SpawnBudget=0;
            CancelSiren(v.Boss);KillPveHorde();
            foreach(var m in Minions.Where(m=>m.Owner==v.Boss).ToArray()){m.Hp=0;Emit(EventKind.Death,null,m.Id,m.Body.position,0);RemoveMinion(m,false);}
            pveAcid.Clear();SirenFields.Clear();Bullets.Clear();
            foreach(var row in v.Rows.Values)row.Open=null;
            foreach(var p in Pilots){CancelAbilities(p);CancelMelee(p);CancelSpecial(p);CancelFighter(p);p.Dots.Clear();p.Knockback=p.MoveVelocity=Vector3.zero;}
        }
        private bool BossVictoryTick(InputFrame input)
        {
            if(!BossVictoryWalk)return false;
            Match.Tick(1d/60);
            if(Time<Pve.StageUntil)StepResultMovement(input);
            else {Pve.Stage="ended";Match.FinishCooperative(true);foreach(var p in Pilots)if(p.Body!=null&&!p.Body.isKinematic)p.Body.linearVelocity=Vector3.zero;}
            return true;
        }
        private void BeginPveHold(string cinematic,int reveal=0)
        {
            if(cinematic=="victory"){BeginBossVictory();return;}
            var v=Pve;Emit(EventKind.PvePhase,v.Boss,reveal,v.Boss.Body.position,0,cinematic);v.Holding=true;v.Cinematic=cinematic;v.Reveal=reveal;if(cinematic=="intro"){BossSongAt=Time;BossSongLead=0;}v.HoldDuration=v.HoldLeft=cinematic=="reveal"?5:cinematic=="intro"?9.0678f:cinematic=="victory"?4:3;
            CancelSiren(v.Boss);pveAcid.Clear();SirenFields.Clear();Bullets.RemoveAll(b=>b.SirenKind!=null);
            foreach(var p in Pilots){p.Dots.Clear();if(!p.Body.isKinematic)p.Body.linearVelocity=Vector3.zero;}
            foreach(var row in v.Rows.Values)if(row.HealAt>=0&&row.HealPulses<4){Heal(row.Pilot,(float)row.Pilot.Stats["hp"]*.125f*(4-row.HealPulses));row.HealPulses=4;}
        }
        // Simulation time is frozen during cinematics; every live timer resumes with its exact remaining duration.
        private bool PveHoldTick(float dt)
        {
            if(Pve==null||!Pve.Holding)return false;var v=Pve;v.HoldLeft-=dt;if(v.HoldLeft>0)return true;string cinematic=v.Cinematic;v.Holding=false;
            if(cinematic=="victory"||cinematic=="defeat"){KillPveHorde();v.Outcome=cinematic;v.Stage="ended";Match.FinishCooperative(cinematic=="victory");return true;}
            if(cinematic=="reveal"&&v.Reveal==1){var boss=v.Boss;v.MaxHp=NativePveRules.BossHp(PartySize);boss.Stats["hp"]=v.MaxHp;boss.Hp=v.MaxHp;boss.Stats["damage"]=NativePveRules.BossDamage(PartySize);SetBossHidden(false);PlacePveBoss(BossSpawn);boss.Yaw=v.Content.boss.yaw;boss.Input=new InputFrame{Yaw=boss.Yaw};v.Floor=v.MaxHp*.75f;BeginPveHold("intro");return true;}
            if(cinematic=="intro"){BossSongLead+=v.HoldDuration;v.Stage="boss";v.StageAt=Time;v.Phase=1;}
            else if(cinematic=="reveal")
            {
                v.Phase=v.Reveal;v.Floor=v.Phase==2?v.MaxHp*.5f:v.Phase==3?v.MaxHp*.25f:0;
                if(v.Phase==2){SetBossHidden(true);v.PossessionAt=Time;var candidates=Minions.Where(m=>m.Pve!=null&&!m.DevMonster&&m.Hp>0&&!m.Pve.Crawl&&!m.Pve.Travelling&&Time>=m.Pve.ActiveAt).OrderByDescending(m=>Members().Where(p=>p.Hp>0&&!p.Seat.bot).Select(p=>(p.Body.position-m.Body.position).sqrMagnitude).DefaultIfEmpty(0).Min()).ThenBy(m=>m.Id).Take(5).ToArray();var add=candidates.Length>0?candidates[(int)(v.SpawnRandom.Next()*candidates.Length)]:SpawnPveAdd("shambler",true);if(add!=null){v.Possessed=add.Id;add.Pve.Possessed=true;add.Pve.PossessedAt=Time;add.Pve.Lost=0;add.Pve.Parts.Clear();add.Pve.Struck=true;add.Pve.RecoverAt=Time;add.MaxHp*=3;add.Hp=add.MaxHp;}}
                if(v.Phase==3){var s=Siren(v.Boss);s.Scale=s.Reach=1.5f;v.Boss.Collider.height=2.55f;v.Boss.Collider.radius=.63f;v.Boss.Body.position+=Vector3.up*.425f;}
                if(v.Phase==4){var s=Siren(v.Boss);s.Cooldown=0;s.MaxRains=2;s.MaxEruptions=4;foreach(var key in s.Ready.Keys.ToArray())s.Ready[key]=Time;}
                foreach(var p in Members())GrantPveMoney(p,250,"boss-threshold");RespawnPveParty();
            }
            PveFlow();return true;
        }
        private void PveFlow()
        {
            var v=Pve;int h=PartySize;if(v.Stage=="wave"){var data=v.Content.waves[v.Wave-1];v.SpawnBudget=NativePveRules.Budget(data,h);v.WaveTotal=v.SpawnBudget;v.Spawned=0;v.Rate=NativePveRules.Rate(data,h);v.Cap=NativePveRules.Cap(data,h,false);}
            else if(v.Stage=="boss"){v.SpawnBudget=-1;v.Rate=(.6f+.4f*(h-1))*(v.Hidden?20:1);v.Cap=Math.Min(v.Hidden?192:40,(6+4*(h-1))*(v.Hidden?12:1));}else v.Rate=0;v.SpawnAt=Time;
        }
        private void FoundPveBoss(Vector3 at)
        {var v=Pve;if(!v.Hidden||v.Phase!=2)return;SetBossHidden(false);v.Boss.Hp=v.MaxHp*.5f;v.Boss.Body.position=GroundPoint(at)+Vector3.up*.9f;v.Possessed=0;BeginPveHold("reveal",3);}
        private float PveDamageRoom(NativePilot p,float amount,NativePilot source)
        {if(Pve==null)return amount;if(Pve.Holding||Pve.Stage=="victory"||Pve.Stage=="ended"||p==Pve.Boss&&Pve.Hidden)return 0;if(p==Pve.Boss)amount=Math.Min(amount,Math.Max(0,p.Hp-Pve.Floor));return amount;}
        private void PveDamaged(NativePilot p,NativePilot source,float actual)
        {
            if(Pve==null||actual<=0)return;if(Expedition!=null){if(p==Pve.Boss&&expeditionBoss!=null)Expedition.UpdateBossHealth(Expedition.Session.Run.Snapshot().area,expeditionBoss,p.Hp,Pve.MaxHp);return;}if(Pve.Rows.TryGetValue(p.Seat.id,out var row)&&p.Seat.bot&&row.Open!=null)row.ThinkAt=Time;
            if(p!=Pve.Boss||source==null)return;var recruit=Pve.Recruits.Find(r=>r.Pilot==source);if(recruit!=null)source=recruit.Owner;if(!Pve.Rows.TryGetValue(source.Seat.id,out var owner)||source.Seat.bot)return;owner.DamageBoss+=actual;int paid=(int)Math.Floor(owner.DamageBoss/Pve.MaxHp*100+1e-9);if(paid>owner.BossPaid){GrantPveMoney(source,(paid-owner.BossPaid)*10,"boss");owner.BossPaid=paid;}
        }
        private void StepPve()
        {
            var v=Pve;if(v==null||Match.Phase!=NativePhase.Fight||v.Holding)return;if(Expedition!=null){StepExpeditionCombat();return;}if(!v.Started)StartPve();
            foreach(var row in v.Rows.Values){while(row.HealAt>=0&&row.HealPulses<4&&Time>=row.HealAt+.5f*(row.HealPulses+1)){row.HealPulses++;Heal(row.Pilot,(float)row.Pilot.Stats["hp"]*.125f);}if(row.SelfAt>=0&&Time>=row.SelfAt&&row.Pilot.Hp<=0&&row.Pilot.Seat.connected&&row.SelfRevives>0&&!InShaft(row.Pilot)){row.SelfRevives--;RevivePve(row.Pilot);}}
            StepPveRecruits();
            if(v.Stage=="opening"||v.Stage=="wave"||v.Stage=="intermission")
            {
                if(v.Stage=="opening"&&v.ResolvedAt<0&&v.Rows.Values.Where(r=>!r.Pilot.Seat.bot&&r.Pilot.Seat.connected).All(r=>r.Open==null&&r.Picks.All(p=>p.Offers!=null)))v.ResolvedAt=Time;
                int remaining=NativePveRules.Remaining(v.SpawnBudget,v.Spawned,Minions.Count(m=>m.Pve!=null&&!m.DevMonster&&m.Hp>0));var transition=v.Schedule.Step(Time,remaining,v.ResolvedAt<0?(double?)null:v.ResolvedAt);v.StageUntil=(float)v.Schedule.Until;
                if(transition!=null)
                {
                    if(transition.kind=="wave-start"){v.Stage="wave";v.Wave=transition.wave;v.StageAt=(float)transition.at;PveFlow();}
                    else if(transition.kind=="wave-clear"){v.Stage="intermission";v.StageAt=(float)transition.at;v.Rate=0;pveAcid.Clear();foreach(var p in Members()){GrantPveMoney(p,100+50*v.Wave,"wave-clear");if(p.Seat.bot)GrantPvePick(p,"common",true,"wave");}RespawnPveParty();}
                    else{v.Rate=0;BeginPveHold("reveal",1);}
                }
            }
            if(v.Stage=="boss")
            {if(v.Hidden&&v.Possessed==0&&Time-v.PossessionAt>=60)FoundPveBoss(BossRescue);else if(!v.Hidden&&v.Boss.Hp<=v.Floor+.0001f){if(v.Phase<4)BeginPveHold("reveal",v.Phase+1);else BeginPveHold("victory");}}
            if(v.Stage=="victory")return;
            if(!v.Holding&&!Members().Any(p=>p.Hp>0&&(p.Seat.connected||p.Seat.bot))&&!v.Rows.Values.Any(r=>r.SelfAt>=0&&r.Pilot.Hp<=0))BeginPveHold("defeat");
            if(v.Holding)return;StepPveHorde();StepPveAcid();
        }
    }
}
