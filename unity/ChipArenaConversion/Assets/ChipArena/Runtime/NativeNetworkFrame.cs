using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeMarkView {public int id,owner,target;public float lockAt,throwAt;}
    [Serializable] public sealed class NativeSeatFrame
    {public int id,team,roundWins,score;public string name,spec,bodyColor,accentColor,hat,killEffect,intro,introText;public bool bot,connected,alive,ready,boss;public ChipCount[] chips;public string[] offers;}
    [Serializable] public sealed class NativePilotFrame
    {public int id,ammo,charges,controlling;public NumberValue[] stats;public NativeDeath death;public NativeWound[] wounds;public float revivedAt;public float hp,mana,stamina,yaw,pitch,reloadAt,reloadEnd,blockEnd,blockReady,rollAt,rollEnd,ready,qReady,eReady,stunEnd,holdAt,scareAt,scareUntil,frozenUntil;public NativeRecovery down;public Vector3 position,velocity;}
    [Serializable] public sealed class NativePropFrame {public string id;public ContentBlock definition;public Vector3 position;public Quaternion rotation;public float health;}
    [Serializable] public sealed class NativeMinionFrame
    {public int id,owner,lost,entry;public string kind,addKind;public float hp,maxHp,yaw,lastAttack,activeAt,spawnAt,diveAt;public Vector3 position,velocity;public bool crawl,possessed,first,travelling;}
    [Serializable] public sealed class NativeEffectFrame
    {public int id,owner,original,element;public string type,kind,spec,ability;public Vector3 position,velocity,from;public float at,until,radius,release;public bool beam,shield;}
    [Serializable] public sealed class NativePveRowFrame
    {public int id,money,earned,spent,kills,assists,deaths,revives;public float selfAt;public int open;public NativePvePickFrame[] picks;}
    [Serializable] public sealed class NativePvePickFrame {public int id;public string tier;public string[] offers;public float at;}
    [Serializable] public sealed class NativePveFrame
    {public int boss,wave,phase,reveal;public string stage,cinematic,outcome,notice;public float maxHp,holdLeft,holdDuration,stageUntil;public bool hidden,holding;public NativePveRowFrame[] rows;public NativePickupFrame[] pickups;}
    [Serializable] public sealed class NativePickupFrame {public int id,owner;public string tier;public Vector3 position;public float born;}
    [Serializable] public sealed class NativeGameFrame:NativeMatchFrame
    {
        public int version=2,generation;public string map,source;public float bossSongAt,bossSongLead;
        public NativeMarkView[] marks;public NativeEntryView[] entries;public NativeDeath[] corpses;public Snapshot snapshot;public NativeSeatFrame[] seats;public NativePilotFrame[] pilots;public NativePropFrame[] props;public NativeMinionFrame[] minions;public NativeEffectFrame[] effects;public NativePveFrame pve;
        public bool Valid(ConversionContent content)
        {
            if(seats==null||seats.Any(s=>s==null)||pilots==null||pilots.Any(p=>p==null)||props==null||props.Any(p=>p==null)||minions==null||minions.Any(m=>m==null)||effects==null||effects.Any(f=>f==null)||snapshot?.Actors==null||snapshot.Actors.Any(a=>a==null)||snapshot.Shots==null||snapshot.Shots.Any(s=>s==null)||snapshot.Events==null)return false;
            bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);bool Number(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);bool NativeVector(V3 v)=>Number(v.x)&&Number(v.y)&&Number(v.z)&&Math.Abs(v.x)<100000&&Math.Abs(v.y)<100000&&Math.Abs(v.z)<100000;bool Vector(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z)&&v.sqrMagnitude<1e10;
            if(!Enum.IsDefined(typeof(NativePhase),phase)||!Number(time)||!Number(phaseEnd)||!Number(fightAt)||!Number(readyAt)||!Number(introStarted)||time>10000000||!Finite(bossSongAt)||!Finite(bossSongLead))return false;
            if(version!=2||source!=content.sourceCommit||generation<0||time<0||double.IsInfinity(time)||double.IsNaN(time)||!content.maps.Any(m=>m.id==map)||seats==null||seats.Length>8||seats.Length<1||seats.Select(s=>s.id).Distinct().Count()!=seats.Length||pilots==null||pilots.Length>8||props==null||props.Length>500||minions==null||minions.Length>256||effects==null||effects.Length>4096||snapshot?.Actors==null||snapshot.Actors.Length>8||snapshot.Shots==null||snapshot.Shots.Length>1024||snapshot.Events==null||snapshot.Events.Length>8192)return false;
            if(pilots.Select(p=>p.id).Distinct().Count()!=pilots.Length||minions.Select(m=>m.id).Distinct().Count()!=minions.Length||props.Select(p=>p.id).Distinct().Count()!=props.Length)return false;
            foreach(var s in seats)if(s.id<1||s.name==null||s.name.Length>40||!content.weapons.Any(w=>w.id==s.spec)||s.chips==null||s.chips.Length>250||s.offers==null||s.offers.Length>12||s.chips.Any(c=>c==null||c.count<0||c.count>100||c.id==null||c.id.Length>64)||s.offers.Any(id=>id==null||id.Length>64))return false;
            if(marks!=null&&(marks.Length>32||marks.Any(m=>m==null||!Finite(m.lockAt)||!Finite(m.throwAt)||!seats.Any(s=>s.id==m.owner)||!seats.Any(s=>s.id==m.target))))return false;
            if(entries!=null&&(entries.Length>256||entries.Any(e=>e==null||e.entry<0||e.entry>15||(e.kind!="vent"&&e.kind!="door"&&e.kind!="cell")||(e.mode!="spawn"&&e.mode!="dive")||!Finite(e.openAt)||!Finite(e.closeAt))))return false;
            if(corpses!=null&&(corpses.Length>32||corpses.Any(d=>d==null||!Vector(d.position)||!Vector(d.velocity))))return false;
            foreach(var p in pilots)if(p.stats==null||p.stats.Length!=content.baseStats.Length||p.stats.Select(s=>s?.name).Distinct().Count()!=p.stats.Length||p.stats.Any(s=>s==null||!content.baseStats.Any(b=>b.name==s.name)||double.IsNaN(s.value)||double.IsInfinity(s.value))||p.wounds!=null&&(p.wounds.Length>8||p.wounds.Any(w=>w==null||w.part<0||w.part>=22))||p.death!=null&&(!Vector(p.death.position)||!Vector(p.death.velocity))||!seats.Any(s=>s.id==p.id)||!Vector(p.position)||!Vector(p.velocity)||!Finite(p.yaw)||!Finite(p.pitch)||!Finite(p.hp)||!Finite(p.mana)||!Finite(p.stamina)||!Finite(p.ready)||!Finite(p.qReady)||!Finite(p.eReady)||!Finite(p.reloadEnd)||!Finite(p.blockEnd)||!Finite(p.rollEnd)||!Finite(p.stunEnd))return false;
            if(snapshot.Actors.Any(a=>!seats.Any(s=>s.id==a.Id)||!Finite(a.Position.x)||!Finite(a.Position.y)||!Finite(a.Position.z)))return false;
            foreach(var p in props)if(p.definition!=null&&(!content.materials.Any(m=>m.id==p.definition.material)||p.definition.rotation==null||!NativeVector(p.definition.size)||p.definition.id!=p.id||p.definition.size.Length>100||p.definition.size.x<=0||p.definition.size.y<=0||p.definition.size.z<=0)||p.id==null||p.id.Length>100||!Vector(p.position)||!Finite(p.rotation.x)||!Finite(p.rotation.y)||!Finite(p.rotation.z)||!Finite(p.rotation.w)||Math.Abs(Quaternion.Dot(p.rotation,p.rotation)-1)>.05f)return false;
            foreach(var m in minions)if(m.id<10000||!seats.Any(s=>s.id==m.owner)||!Vector(m.position)||!Vector(m.velocity)||!Finite(m.hp)||!Finite(m.maxHp)||!Finite(m.yaw)||!Finite(m.lastAttack)||!Finite(m.spawnAt)||!Finite(m.activeAt)||m.kind==null||m.kind.Length>30)return false;
            if(snapshot.Shots.Any(s=>!NativeVector(s.Position)||!NativeVector(s.Velocity)||!Finite(s.Radius)||s.Radius<=0||s.Radius>100)||snapshot.Events.Any(e=>!NativeVector(e.Position)||!NativeVector(e.Normal)||!Finite(e.Time)||!Finite(e.Amount)))return false;
            foreach(var f in effects)if(f.type==null||f.type.Length>30||!Finite(f.at)||!Finite(f.until)||!Vector(f.position)||!Vector(f.velocity)||!Vector(f.from)||!Finite(f.radius)||Math.Abs(f.radius)>1000)return false;
            if(pve!=null&&(pve.rows==null||pve.rows.Length>8||pve.pickups==null||pve.pickups.Length>32||pve.rows.Any(r=>r==null||r.picks==null||r.picks.Length>100||r.picks.Any(p=>p==null||p.offers!=null&&p.offers.Length>12))))return false;return true;
        }
    }
    public sealed partial class NativeArena
    {
        public int LocalId=1;public bool Replica;private NativeGameFrame replicaFrame;
        private readonly Dictionary<int,(InputFrame input,float at)> remoteInputs=new Dictionary<int,(InputFrame,float)>();
        public void ClearRemoteInput(int id)=>remoteInputs.Remove(id);
        public void AcceptInput(int id,InputFrame input)
        {if(!input.Valid||id==LocalId)return;if(remoteInputs.TryGetValue(id,out var old)&&input.Sequence<=old.input.Sequence)return;if(remoteInputs.TryGetValue(id,out old)){var taps=old.input;if(Match.Seat(id)?.spec is string spec&&Content.Weapon(spec).@class=="melee")input.Shoot|=taps.Shoot;input.Block|=taps.Block;input.Roll|=taps.Roll;input.Jump|=taps.Jump;input.Reload|=taps.Reload;input.AbilityQ|=taps.AbilityQ;input.AbilityE|=taps.AbilityE;input.AbilityEReleased|=taps.AbilityEReleased;input.Hadoken|=taps.Hadoken;input.Uppercut|=taps.Uppercut;input.Interact|=taps.Interact;if(input.SirenAbility==0)input.SirenAbility=taps.SirenAbility;}remoteInputs[id]=(input.Sanitized(),Time);}
        private InputFrame RemoteInput(int id)
        {if(!remoteInputs.TryGetValue(id,out var value))return new InputFrame();if(Time-value.at>.75f)return new InputFrame{Yaw=value.input.Yaw,Pitch=value.input.Pitch};var held=value.input.HeldOnly();if(Match.Seat(id)?.spec is string spec&&Content.Weapon(spec).@class=="melee")held.Shoot=false;remoteInputs[id]=(held,value.at);return value.input;}
        public NativeGameFrame NetworkFrame()
        {
            var f=new NativeGameFrame{marks=Marks().ToArray(),introSeat=Match.IntroSeat,introStarted=Match.IntroStarted,introStyle=Match.IntroStyle,entries=Entries.ToArray(),corpses=MinionDeaths.Where(d=>Time-d.at<=5).ToArray(),generation=Generation,source=Content.sourceCommit,map=MapId,phase=Match.Phase,time=Match.Time,phaseEnd=Match.PhaseEnd,fightAt=Match.FightAt,readyAt=Match.ReadyAt,round=Match.Round,set=Match.Set,winner=Match.Winner,setWinner=Match.SetWinner,draftSeat=Match.DraftSeat,chosen=Match.Chosen,result=Match.Result,teams=Match.Teams,cooperative=Match.Cooperative,snapshot=Snapshot(),bossSongAt=BossSongAt,bossSongLead=BossSongLead};
            f.seats=Match.Seats.Select(s=>new NativeSeatFrame{id=s.id,name=s.name,spec=s.spec,team=s.team,roundWins=s.roundWins,score=s.score,bot=s.bot,connected=s.connected,alive=s.alive,boss=s.AuthorityBoss,bodyColor=s.bodyColor,accentColor=s.accentColor,hat=s.hat,killEffect=s.killEffect,intro=s.intro,introText=s.introText,chips=s.chips.Select(c=>new ChipCount{id=c.Key,count=c.Value}).ToArray(),offers=s.offers.ToArray()}).ToArray();
            f.pilots=Pilots.Select(p=>new NativePilotFrame{id=p.Seat.id,stats=Content.baseStats.Select(s=>new NumberValue{name=s.name,value=p.Stats[s.name]}).ToArray(),death=p.Death,wounds=p.Wounds.ToArray(),revivedAt=p.RevivedAt,hp=p.Hp,mana=p.Mana,stamina=p.Stamina,ammo=p.Ammo,charges=p.ECharges,yaw=p.Yaw,pitch=p.Pitch,position=p.Body.position,velocity=p.Body.isKinematic?Vector3.zero:p.Body.linearVelocity,ready=p.Ready,qReady=p.QReady,eReady=p.EReady,reloadAt=p.ReloadAt,reloadEnd=p.ReloadEnd,blockEnd=p.BlockEnd,blockReady=p.BlockReady,rollAt=p.RollAt,rollEnd=p.RollEnd,stunEnd=p.StunEnd,down=p.Down,holdAt=p.HoldAt,controlling=p.Controlling?.Seat.id??0,scareAt=p.ScareAt,scareUntil=p.ScareUntil,frozenUntil=p.FrozenUntil}).ToArray();
            f.props=Props.Select(p=>new NativePropFrame{id=p.Definition.id,definition=p.Fragment?p.Definition:null,position=p.transform.position,rotation=p.transform.rotation,health=p.Indestructible?0:p.Health}).ToArray();
            f.minions=Minions.Select(m=>new NativeMinionFrame{id=m.Id,owner=m.Owner.Seat.id,kind=m.Kind,hp=m.Hp,maxHp=m.MaxHp,yaw=m.Yaw,position=m.Body.position,velocity=m.Body.isKinematic?Vector3.zero:m.Body.linearVelocity,lastAttack=m.LastAttack,addKind=m.Pve?.Stats.id,lost=m.Pve?.Lost??0,entry=m.Pve?.Entry.e??0,activeAt=m.Pve?.ActiveAt??0,spawnAt=m.Pve?.SpawnAt??0,diveAt=m.Pve?.DiveAt??0,first=m.Pve?.First??true,travelling=m.Pve?.Travelling??false,crawl=m.Pve?.Crawl??false,possessed=m.Pve?.Possessed??false}).ToArray();
            var fx=new List<NativeEffectFrame>();
            fx.AddRange(AbilityShots.Select(s=>new NativeEffectFrame{type="ability",id=s.Id,owner=s.Owner.Seat.id,ability=s.Definition.id,position=s.Position,velocity=s.Velocity,radius=s.Radius}));
            fx.AddRange(AbilityZones.Select(s=>new NativeEffectFrame{type="zone",owner=s.Owner.Seat.id,ability=s.Definition.id,kind=s.Kind,position=s.Position,radius=s.Radius,until=s.Until}));
            fx.AddRange(AbilityEffects.Select(s=>new NativeEffectFrame{type="fx",position=s.Position,from=s.From,radius=s.Radius,until=s.Until,beam=s.Beam,element=s.Element}));
            fx.AddRange(MeleeShots.Select(s=>new NativeEffectFrame{type="melee",id=s.Id,owner=s.Owner.Seat.id,original=s.Original.Seat.id,spec=s.Spec,position=s.Position,velocity=s.Velocity,radius=s.Radius,at=s.At,shield=s.Shield}));
            fx.AddRange(MeleeImpacts.Select(s=>new NativeEffectFrame{type="slam",position=s.Position,radius=s.Radius,at=s.At}));
            fx.AddRange(ChipFields.Select(s=>new NativeEffectFrame{type="chip",id=s.Id,owner=s.Owner.Seat.id,kind=s.Kind,position=s.Position,radius=s.Radius,at=s.Born,until=s.Until}));
            fx.AddRange(SirenFields.Select(s=>new NativeEffectFrame{type="siren",id=s.Id,owner=s.Owner.Seat.id,kind=s.Kind,position=s.Position,radius=s.Radius,at=s.At,until=s.Until}));
            fx.AddRange(LeetFields.Select(s=>new NativeEffectFrame{type="leet",id=s.Id,owner=s.Owner.Seat.id,kind=s.Kind,position=s.Position,from=s.From,at=s.At,until=s.Until,radius=s.Sanity}));
            fx.AddRange(StormTrails.Select(s=>new NativeEffectFrame{type="storm",owner=s.Owner.Seat.id,position=s.Position,at=s.At,until=s.Until}));f.effects=fx.ToArray();
            if(Pve!=null)f.pve=new NativePveFrame{boss=Pve.Boss.Seat.id,wave=Pve.Wave,phase=Pve.Phase,reveal=Pve.Reveal,stage=Pve.Stage,cinematic=Pve.Cinematic,outcome=Pve.Outcome,notice=Pve.Notice,maxHp=Pve.MaxHp,holding=Pve.Holding,hidden=Pve.Hidden,holdLeft=Pve.HoldLeft,holdDuration=Pve.HoldDuration,stageUntil=Pve.StageUntil,rows=Pve.Rows.Values.Select(r=>new NativePveRowFrame{id=r.Pilot.Seat.id,money=r.Money,earned=r.Earned,spent=r.Spent,kills=r.Kills,assists=r.Assists,deaths=r.Deaths,revives=r.Revives,selfAt=r.SelfAt,open=r.Open?.Id??0,picks=r.Picks.Select(p=>new NativePvePickFrame{id=p.Id,tier=p.Tier,offers=p.Offers?.ToArray(),at=p.At}).ToArray()}).ToArray(),pickups=Pve.Pickups.Select(p=>new NativePickupFrame{id=p.Id,owner=p.Owner.Seat.id,tier=p.Tier,position=p.Position,born=p.Born}).ToArray()};return f;
        }
        public void ApplyFrame(NativeGameFrame f,int localId)
        {
            Replica=true;LocalId=localId;bool reset=replicaFrame==null||replicaFrame.generation!=f.generation;Match.ApplyNetwork(f);
            foreach(var sf in f.seats){var seat=Match.Seat(sf.id);if(seat==null){seat=new NativeSeat{id=sf.id};Match.Seats.Add(seat);}seat.name=sf.name;seat.spec=sf.spec;seat.team=sf.team;seat.bot=sf.bot;seat.connected=sf.connected;seat.alive=sf.alive;seat.score=sf.score;seat.roundWins=sf.roundWins;seat.AuthorityBoss=sf.boss;seat.bodyColor=sf.bodyColor;seat.accentColor=sf.accentColor;seat.hat=sf.hat;seat.killEffect=sf.killEffect;seat.intro=sf.intro;seat.introText=sf.introText;seat.offers=sf.offers.ToList();seat.chips.Clear();foreach(var c in sf.chips)seat.chips[c.id]=c.count;}
            Match.Seats.RemoveAll(s=>!f.seats.Any(x=>x.id==s.id));if(reset){Reset();Generation=f.generation;}
            if(f.pve!=null&&Pve==null)ConfigurePve(JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text),false);
            foreach(var pf in f.pilots)
            {
                var p=Pilots.Find(p=>p.Seat.id==pf.id)??SpawnPilot(Match.Seat(pf.id),Pilots.Count);p.Weapon=Content.Weapon(p.Seat.spec);p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);p.Body.isKinematic=true;p.Collider.enabled=pf.hp>0&&p.Seat.connected;
                if(pf.stats!=null)foreach(var stat in pf.stats)p.Stats[stat.name]=stat.value;p.Death=pf.death;p.RevivedAt=pf.revivedAt;p.Wounds.Clear();if(pf.wounds!=null)p.Wounds.AddRange(pf.wounds);p.Hp=pf.hp;p.Mana=pf.mana;p.Stamina=pf.stamina;p.Ammo=pf.ammo;p.ECharges=pf.charges;p.Yaw=pf.yaw;p.Pitch=pf.pitch;p.Ready=pf.ready;p.QReady=pf.qReady;p.EReady=pf.eReady;p.ReloadAt=pf.reloadAt;p.ReloadEnd=pf.reloadEnd;p.BlockEnd=pf.blockEnd;p.BlockReady=pf.blockReady;p.RollAt=pf.rollAt;p.RollEnd=pf.rollEnd;p.StunEnd=pf.stunEnd;p.Down=pf.down;p.HoldAt=pf.holdAt;p.ScareAt=pf.scareAt;p.ScareUntil=pf.scareUntil;p.FrozenUntil=pf.frozenUntil;
                p.Pose=f.snapshot.Actors.FirstOrDefault(a=>a.Id==pf.id)??p.Pose;if(reset||Vector3.Distance(p.Body.position,pf.position)>5)p.Body.position=pf.position;
            }
            foreach(var pf in f.pilots)Pilots.Find(p=>p.Seat.id==pf.id).Controlling=Pilots.Find(p=>p.Seat.id==pf.controlling);
            foreach(var p in Pilots.Where(p=>!f.pilots.Any(x=>x.id==p.Seat.id)).ToArray()){Pilots.Remove(p);CapturedAnimation.Release(p.Body.gameObject);}
            foreach(var p in Props.ToArray()){var pf=Array.Find(f.props,x=>x.id==p.Definition.id);if(pf==null){Props.Remove(p);p.GetComponent<Collider>().enabled=false;CapturedAnimation.Release(p.gameObject);continue;}if(p.Body!=null)p.Body.isKinematic=true;p.transform.SetPositionAndRotation(pf.position,pf.rotation);if(!p.Indestructible)p.Health=pf.health;}
            foreach(var pf in f.props)if(pf.definition!=null&&!Props.Any(p=>p.Definition.id==pf.id)){var prop=AddProp(pf.definition,true);prop.Body.isKinematic=true;prop.transform.SetPositionAndRotation(pf.position,pf.rotation);}
            ApplyMinions(f);ApplyEffects(f);ApplyPve(f.pve);BossSongAt=f.bossSongAt;BossSongLead=f.bossSongLead;Events.Clear();Events.AddRange(f.snapshot.Events);Entries.Clear();if(f.entries!=null)Entries.AddRange(f.entries);MinionDeaths.Clear();if(f.corpses!=null)MinionDeaths.AddRange(f.corpses);foreach(var sf in f.seats)Match.Seat(sf.id).connected=sf.connected;replicaFrame=f;
        }
        private void ApplyMinions(NativeGameFrame f)
        {
            foreach(var m in Minions.Where(m=>!f.minions.Any(x=>x.id==m.Id)).ToArray())RemoveMinion(m,false);
            foreach(var mf in f.minions)
            {
                var m=Minions.Find(m=>m.Id==mf.id);if(m==null){var go=new GameObject("Remote summon");go.layer=11;go.transform.SetParent(root.transform);var body=go.AddComponent<Rigidbody>();body.isKinematic=true;var collider=go.AddComponent<CapsuleCollider>();collider.radius=.42f;collider.height=1.7f;m=new NativeMinion{Id=mf.id,Body=body,Collider=collider};go.AddComponent<NativeMinionBody>().Minion=m;Minions.Add(m);}
                m.Owner=Pilots.Find(p=>p.Seat.id==mf.owner);m.Kind=mf.kind;m.Hp=mf.hp;m.MaxHp=mf.maxHp;m.Yaw=mf.yaw;m.LastAttack=mf.lastAttack;m.Body.position=mf.position;m.NetworkVelocity=mf.velocity;
                if(mf.addKind!=null&&Pve!=null){var stats=Pve.Content.adds.FirstOrDefault(s=>s.id==mf.addKind);if(stats!=null)m.Pve=new NativePveAdd{Stats=stats,Entry=Pve.Content.entries.FirstOrDefault(e=>e.e==mf.entry)??Pve.Content.entries[0],ActiveAt=mf.activeAt,SpawnAt=mf.spawnAt,DiveAt=mf.diveAt,First=mf.first,Travelling=mf.travelling,Lost=mf.lost,Crawl=mf.crawl,Possessed=mf.possessed};}
            }
        }
        private void ApplyEffects(NativeGameFrame f)
        {
            AbilityShots.Clear();AbilityZones.Clear();AbilityEffects.Clear();MeleeShots.Clear();MeleeImpacts.Clear();ChipFields.Clear();SirenFields.Clear();LeetFields.Clear();StormTrails.Clear();Bullets.Clear();
            foreach(var e in f.effects){var p=Pilots.Find(p=>p.Seat.id==e.owner);var d=Content.abilities.FirstOrDefault(d=>d.id==e.ability);
                if(e.type=="fx")AbilityEffects.Add(new NativeAbilityFx{Position=e.position,From=e.from,Radius=e.radius,Until=e.until,Beam=e.beam,Element=e.element});
                else if(e.type=="slam")MeleeImpacts.Add(new NativeMeleeImpact{Position=e.position,Radius=e.radius,At=e.at});
                else if(p==null)continue;
                else if(e.type=="ability"&&d!=null)AbilityShots.Add(new NativeAbilityShot{Id=e.id,Owner=p,Definition=d,Position=e.position,Velocity=e.velocity,Radius=e.radius});
                else if(e.type=="zone"&&d!=null)AbilityZones.Add(new NativeAbilityZone{Owner=p,Definition=d,Kind=e.kind,Position=e.position,Radius=e.radius,Until=e.until});
                else if(e.type=="melee")MeleeShots.Add(new NativeMeleeShot{Id=e.id,Owner=p,Original=Pilots.Find(q=>q.Seat.id==e.original)??p,Spec=e.spec,Position=e.position,Velocity=e.velocity,Radius=e.radius,At=e.at,Shield=e.shield});
                else if(e.type=="chip")ChipFields.Add(new NativeChipField{Id=e.id,Owner=p,Kind=e.kind,Position=e.position,Radius=e.radius,Born=e.at,Until=e.until});
                else if(e.type=="siren")SirenFields.Add(new NativeSirenField{Id=e.id,Owner=p,Kind=e.kind,Position=e.position,Radius=e.radius,At=e.at,Until=e.until});
                else if(e.type=="leet")LeetFields.Add(new NativeLeetField{Id=e.id,Owner=p,Kind=e.kind,Position=e.position,From=e.from,At=e.at,Until=e.until,Sanity=e.radius});
                else if(e.type=="storm")StormTrails.Add(new NativeStormTrail{Owner=p,Position=e.position,At=e.at,Until=e.until});
            }
            foreach(var s in f.snapshot.Shots)Bullets.Add(new NativeBullet{Id=s.Id,Owner=s.Owner,Position=Vec(s.Position),Velocity=Vec(s.Velocity),Radius=s.Radius,Spec=Pilots.Find(p=>p.Seat.id==s.Owner)?.Seat.spec??"pistol"});
        }
        private void ApplyPve(NativePveFrame f)
        {
            if(f==null||Pve==null)return;var v=Pve;v.Boss=Pilots.Find(p=>p.Seat.id==f.boss);v.Wave=f.wave;v.Phase=f.phase;v.Reveal=f.reveal;v.Stage=f.stage;v.Cinematic=f.cinematic;v.Outcome=f.outcome;v.Notice=f.notice;v.MaxHp=f.maxHp;v.Hidden=f.hidden;v.Holding=f.holding;v.HoldLeft=f.holdLeft;v.HoldDuration=f.holdDuration;v.StageUntil=f.stageUntil;v.Rows.Clear();
            foreach(var rf in f.rows){var p=Pilots.Find(p=>p.Seat.id==rf.id);if(p==null)continue;var r=new NativePveRow{Pilot=p,Money=rf.money,Earned=rf.earned,Spent=rf.spent,Kills=rf.kills,Assists=rf.assists,Deaths=rf.deaths,Revives=rf.revives,SelfAt=rf.selfAt};foreach(var pf in rf.picks){var pick=new NativePvePick{Id=pf.id,Tier=pf.tier,Offers=pf.offers?.ToList(),At=pf.at};r.Picks.Add(pick);if(pf.id==rf.open)r.Open=pick;}v.Rows[p.Seat.id]=r;}
            v.Pickups.Clear();foreach(var pf in f.pickups){var owner=Pilots.Find(p=>p.Seat.id==pf.owner);if(owner!=null)v.Pickups.Add(new NativePvePickup{Id=pf.id,Owner=owner,Tier=pf.tier,Position=pf.position,Born=pf.born});}
        }
        public void InterpolateReplica(float delta)
        {if(replicaFrame==null)return;float blend=1-Mathf.Exp(-24*delta);foreach(var pf in replicaFrame.pilots){var p=Pilots.Find(p=>p.Seat.id==pf.id);if(p!=null)p.Body.position=Vector3.Lerp(p.Body.position,pf.position,blend);}Physics.SyncTransforms();}
        private Snapshot ReplicaSnapshot()
        {var snapshot=replicaFrame.snapshot;return new Snapshot{Tick=snapshot.Tick,Actors=snapshot.Actors.Select(a=>{var p=Pilots.Find(p=>p.Seat.id==a.Id);var v=a.ViewCopy();if(p!=null){v.Position=Vec(p.Body.position-Vector3.up*.865f);v.Yaw=v.AimYaw=p.Yaw;v.Pitch=p.Pitch;}return v;}).ToArray(),Covers=snapshot.Covers,Shots=snapshot.Shots,Events=snapshot.Events};}
    }
}
