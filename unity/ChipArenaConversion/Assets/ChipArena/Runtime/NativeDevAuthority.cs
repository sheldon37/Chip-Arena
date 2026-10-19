using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot
    {public bool DevGod,DevInfiniteAmmo,DevNoCooldowns;public string DevInput="ai";public float DevTapAt;}
    [Serializable] public sealed class NativeDevHazardPulse
    {public string id;public Vector3 position;public float at;}
    public sealed partial class NativeArena
    {
        private string devActionMessage;
        private readonly NativeDevCommands devCommands=new NativeDevCommands();
        private readonly Dictionary<int,string> devOriginalWeapons=new Dictionary<int,string>();
        public bool DevClockChanged;public float DevTimeScale=1;
        public readonly List<NativeDevHazardPulse> DevHazardPulses=new List<NativeDevHazardPulse>();
        public NativeDevReply DevCommand(NativeSeat sender,NativeDevCommand command,int epoch,int generation,int expectedEpoch,double clock)
        {
            if(Replica)return new NativeDevReply{transaction=command?.transaction,state="refused",reason="Only the match authority can execute DEV commands."};
            devActionMessage=null;var reply=devCommands.Run(sender,command,epoch,generation,expectedEpoch,Generation,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),clock,()=>ApplyDevCommand(sender,command),completedReason:()=>devActionMessage);
            return reply;
        }
        private string ApplyDevCommand(NativeSeat sender,NativeDevCommand c)
        {
            if(!NativeDevRules.DevAllowed(sender,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))return "Developer grant expired.";
            var actor=Pilots.Find(p=>p.Seat==sender);if(actor==null)return "Developer seat is not in this match.";
            if(Match.Phase!=NativePhase.Fight)return "DEV actions require an active round.";
            var target=Pilots.Find(p=>p.Seat.id==(c.target==0?sender.id:c.target));if(target==null||!target.Seat.connected)return "Target pilot is unavailable.";
            var a=c.args;float N(int i)=>float.Parse(a[i],CultureInfo.InvariantCulture);int I(int i)=>int.Parse(a[i],CultureInfo.InvariantCulture);
            bool weapon=c.action=="weapon"||c.action=="swap-weapon"||c.action=="spawn-dummy";
            if(weapon&&!Content.weapons.Any(w=>w.id==a[0]))return "Unknown weapon.";
            if(c.action=="chip-add"||c.action=="chip-remove")
            {
                var chip=Content.chips.FirstOrDefault(x=>x.id==a[0]);if(chip==null)return "Unknown chip.";
                target.Seat.chips.TryGetValue(a[0],out int count);
                if(c.action=="chip-add"&&(count+I(1)>chip.max||!NativeContentRules.Eligible(Content,target.Seat.spec,target.Seat.chips).Any(x=>x.id==a[0])))return "Chip is ineligible or exceeds its stack limit.";
                if(c.action=="chip-remove"&&count<I(1))return "Target does not have that many chips.";
            }
            int spawn=c.action=="spawn-dummy"?1:c.action=="spawn-dummies-around"?I(0):0;
            if(spawn>0&&Match.Seats.Count-spawnFreeDummies()+spawn>8)return "No free pilot slots (eight including dummies).";
            if((c.action=="debris"||c.action=="debris-rain")&&(!Content.materials.Any(m=>m.id==a[c.action=="debris"?2:0]&&!m.indestructible)||Props.Count(p=>p.Fragment)+(c.action=="debris"?1:6)>NativeStructureRules.MaxFragments||Props.Count>350))return "Unknown material or debris budget exceeded.";
            if((c.action=="debris"||c.action=="debris-rain")&&Map.blocks.Length==0)return "Arena has no debris anchor.";
            MapHazard hazard=null;
            if(c.action=="hazard-at"||c.action=="hazard-trigger")
            {
                hazard=c.action=="hazard-at"?(Map.hazards??Array.Empty<MapHazard>()).OrderBy(h=>(DevHazardAnchor(h)-target.Body.position).sqrMagnitude).FirstOrDefault():(Map.hazards??Array.Empty<MapHazard>()).FirstOrDefault(h=>h.id==a[0]);
                if(hazard==null)return "No matching stage hazard in this arena.";
            }
            if(c.action=="restore"&&!TrySandboxSpawn(target,out _))return "No clear restore position.";
            bool needsAlive=new[]{"down","launch","freeze","bring","send","teleport"}.Contains(c.action);
            if(needsAlive&&(target.Hp<=0||target.Body.isKinematic))return "Target must be alive.";
            if(c.action=="bring"&&actor.Hp<=0)return "Developer must be alive to bring a pilot.";
            Vector3 point=target.Body.position;
            if(c.action=="teleport"||c.action=="send")point=new Vector3(N(0),N(1),N(2));
            if(c.action=="bring")point=actor.Body.position+Vec(V3.Forward(actor.Yaw))*2;
            if(c.action=="teleport"||c.action=="send"||c.action=="bring")
            {if((ClampMap(point)-point).sqrMagnitude>.01f||point.y<(Map.bounds?.killY??-6)+1||point.y>(Map.bounds?.ceiling??100)-1)return "Destination is outside arena bounds.";}
            // Quarantine this account before mutations; even a partially failed action cannot earn receipts.
            foreach(var seat in Match.Seats.Where(s=>s==sender||!string.IsNullOrEmpty(sender.access.AccountId)&&s.access.AccountId==sender.access.AccountId))seat.devCommandsRan=true;
            Expedition?.Session.Run.DisableAccountXp();
            Debug.Log("DEV_ACTION seat="+sender.id+" transaction="+c.transaction+" action="+c.action+" target="+target.Seat.id);
            switch(c.action)
            {
                case "spawn-monster":return DevSpawnMonsters(actor,target,a[0],I(1),a[2],a[3]);
                case "clear-monsters":DevClearMonsters();break;
                case "restore":DevCancel(target);SandboxRestore(target);break;
                case "down":DevCancel(target);bool god=target.DevGod;target.DevGod=false;Damage(target,null,target.Hp+1,Vector3.up,0,false,forced:true,stageLethal:true);target.DevGod=god;break;
                case "god":target.DevGod=a[0]=="on";break;
                case "infinite-ammo":target.DevInfiniteAmmo=a[0]=="on";break;
                case "no-cooldowns":target.DevNoCooldowns=a[0]=="on";break;
                case "chip-add":case "chip-remove":
                    target.Seat.chips.TryGetValue(a[0],out int count);count+=I(1)*(c.action=="chip-add"?1:-1);if(count==0)target.Seat.chips.Remove(a[0]);else target.Seat.chips[a[0]]=count;
                    target.Stats=NativeContentRules.Stats(Content,target.Seat.spec,target.Seat.chips);target.Hp=Math.Min(target.Hp,(float)target.Stats["hp"]);break;
                case "weapon":case "swap-weapon":DevWeapon(actor,target,a[0]);break;
                case "teleport":case "bring":case "send":DevCancel(target);target.Body.position=point;target.Body.linearVelocity=target.MoveVelocity=target.Knockback=Vector3.zero;target.FrozenPosition=null;break;
                case "launch":DevCancel(target);target.FrozenUntil=target.ScareUntil=0;target.Knockback=Vector3.up*N(0);target.Body.linearVelocity=target.Knockback;break;
                case "freeze":DevCancel(target);target.FrozenUntil=Time+N(0);target.FrozenPosition=target.Body.position;break;
                case "shrink":target.Seat.devScale=.5f;break;
                case "grow":target.Seat.devScale=2;break;
                case "reveal":sender.devRevealed=true;break;
                case "time-scale":DevClockChanged=true;DevTimeScale=N(0);break;
                case "arena-reset":var pveContent=Pve?.Content;bool skip=Pve?.Skip??false;Reset();if(pveContent!=null)ConfigurePve(pveContent,skip,NativeMatch.NewSeed());break;
                case "round-end":Match.DevEndRound();break;
                case "spawn-dummy":DevSpawn(actor,a[0],a[1],I(2),target.Body.position+Vector3.right*2);break;
                case "spawn-dummies-around":for(int i=0;i<spawn;i++){float angle=i*Mathf.PI*2/spawn;DevSpawn(actor,"pistol","ai",(target.Seat.team+1)%8,ClampMap(target.Body.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*3));}break;
                case "clear-dummies":foreach(var p in Pilots.Where(p=>p.Seat.devDummy).ToArray()){DevCancel(p);p.Hp=0;p.Seat.alive=false;p.Seat.connected=false;p.Seat.disconnectedAt=Time-12;p.Collider.enabled=false;p.Body.isKinematic=true;}break;
                case "debris":var direction=target==actor?Vec(V3.Forward(actor.Yaw)):(target.Body.position-actor.Body.position).normalized;DevDebris(actor,actor.Body.position+Vector3.up+direction*2,direction*N(1),a[2],N(0),a[3]);break;
                case "debris-rain":for(int i=0;i<6;i++)DevDebris(actor,target.Body.position+new Vector3((i%3-1)*1.3f,7+i/3,(i/3-.5f)*1.3f),Vector3.down*10,a[0],8,"face");break;
                case "hazard-at":case "hazard-trigger":DevHazard(hazard,c.action=="hazard-at"?target.Body.position:DevHazardAnchor(hazard));break;
            }
            return null;
        }
        private void DevCancel(NativePilot p)
        {foreach(var caster in Pilots.Where(x=>x.Kernel?.Active==true&&(x==p||x.Kernel.Victim==p.Seat.id)).ToArray())EndKernel(caster,3);CancelSiren(p);CancelFighter(p);CancelSpecial(p);CancelMelee(p);CancelAbilities(p);ReleaseControl(p);p.Down=null;p.Juggle=null;}
        private void DevWeapon(NativePilot actor,NativePilot p,string id)
        {if(!devOriginalWeapons.ContainsKey(p.Seat.id))devOriginalWeapons[p.Seat.id]=p.Seat.spec;p.Seat.devWeaponAccess=NativeAccess.ScopedWeapon(actor.Seat.access,id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());DevCancel(p);p.Seat.spec=id;p.Seat.chips.Clear();p.Weapon=Content.Weapon(id);p.Stats=NativeContentRules.Stats(Content,id,p.Seat.chips);p.Hp=Math.Min(p.Hp,(float)p.Stats["hp"]);SandboxRefresh(p);}
        private void DevSpawn(NativePilot actor,string weapon,string input,int team,Vector3 point)
        {
            var old=Pilots.FirstOrDefault(p=>p.Seat.devDummy&&!p.Seat.connected);
            if(old!=null){old.Seat.connected=true;old.Seat.disconnectedAt=-1;old.Seat.team=team;old.Seat.chips.Clear();DevWeapon(actor,old,weapon);SandboxRestore(old);old.Hp=(float)old.Stats["hp"];old.Seat.alive=true;old.Body.isKinematic=false;old.Collider.enabled=true;old.Body.position=point;old.DevInput=input;old.DevGod=old.DevNoCooldowns=old.DevInfiniteAmmo=false;return;}
            int id=1;while(Match.Seats.Any(s=>s.id==id))id++;
            var seat=new NativeSeat{id=id,name="DEV dummy",spec=weapon,team=team,bot=true,devDummy=true,access=NativeAccess.ScopedWeapon(actor.Seat.access,weapon,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())};
            Match.Seats.Add(seat);var p=SpawnPilot(seat,Match.Seats.Count-1);p.Body.position=point;p.DevInput=input;
        }
        private int spawnFreeDummies()=>Match.Seats.Count(s=>s.devDummy&&!s.connected);
        private void DevDispose()
        {foreach(var seat in Match.Seats){if(devOriginalWeapons.TryGetValue(seat.id,out var spec))seat.spec=spec;seat.devWeaponAccess=null;seat.devScale=1;seat.devRevealed=false;}Match.Seats.RemoveAll(s=>s.devDummy);devOriginalWeapons.Clear();}
        private void DevDebris(NativePilot source,Vector3 point,Vector3 velocity,string material,float mass,string impact)
        {
            var rotation=Quaternion.Euler(impact=="corner"?45:0,impact=="face"?0:45,0);
            var definition=new ContentBlock{id="dev-debris-"+nextChunk++,parent=Map.blocks[0].id,role="fragment",pos=Vec(point),size=new V3(.8f,.8f,.8f),rotation=new ContentRotation{x=rotation.x,y=rotation.y,z=rotation.z,w=rotation.w},material=material,dynamic=true,mass=mass};
            AddProp(definition,true,source:source).Body.linearVelocity=velocity;
        }
        private Vector3 DevHazardAnchor(MapHazard h)=>NativeStageHazards.City(h)||NativeStageHazards.Ward(h)?Vec(h.center):Vec(h.kind=="crane"&&h.zones?.Length>0?h.zones[0].center:h.kind=="press"?h.b:h.a);
        private void DevHazard(MapHazard hazard,Vector3 point)
        {
            DevHazardPulses.RemoveAll(p=>Time-p.at>2);if(DevHazardPulses.Count>=32)DevHazardPulses.RemoveAt(0);
            DevHazardPulses.Add(new NativeDevHazardPulse{id=hazard.id,position=point,at=Time});
            foreach(var p in Pilots.Where(p=>p.Hp>0&&p.Seat.connected).ToArray())if(NativeStageHazards.Touch(Vec(p.Body.position),Vec(point),hazard.size))
            {p.Knockback+=Vector3.up*Math.Max(2,hazard.push);Damage(p,null,hazard.lethal?p.Hp:Math.Max(1,hazard.damage),Vector3.up,0,false,forced:hazard.lethal,direct:false,primary:false,stage:true,rollAvoidable:false,damageKind:"hazard",stageHazard:hazard.id,stageLethal:hazard.lethal);}
        }
        private void DevStep()
        {
            if(Replica)return;
            if(Match.Phase!=NativePhase.Fight)DevClearMonsters();
            foreach(var p in Pilots.Where(p=>p.Hp>0))
            {
                if(p.DevInfiniteAmmo){p.Ammo=Magazine(p);p.ReloadEnd=0;}
                if(!p.DevNoCooldowns)continue;
                p.Ready=p.QReady=p.EReady=p.RollReady=p.BlockReady=p.KernelReady=Time;p.ECharges=NativeAbilityRules.Charges(p.Stats);
                if(p.Siren!=null)p.Siren.Ready.Clear();
                p.Cyber.QReady=p.Cyber.EReady=p.Cyber.PacketReady=p.Cyber.LinkReady=p.Cyber.CrashReady=Time;
                if(p.Cyber.Ranged!=null){p.Cyber.Ranged.ModeReady=Time;for(int i=0;i<p.Cyber.Ranged.Ready.Length;i++)p.Cyber.Ranged.Ready[i]=Time;}
                foreach(var key in chipCooldowns.Keys.Where(k=>k.StartsWith(p.Seat.id+":")).ToArray())chipCooldowns.Remove(key);
            }
        }
        private InputFrame DevBot(NativePilot p)
        {
            if(!p.Seat.devDummy)return Bot(p);
            var target=Pilots.Where(q=>q.Hp>0&&q.Seat.connected&&Enemy(p,q)).OrderBy(q=>(q.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();
            return target==null?new InputFrame{Yaw=p.Yaw}:BotFocus(p,target.Body.position,PilotVelocity(target),target.Collider,target,true);
        }
        private InputFrame DevDummyInput(NativePilot p,InputFrame input)
        {
            if(!p.Seat.devDummy||p.DevInput=="ai")return input;
            bool tap=Time>=p.DevTapAt;if(tap)p.DevTapAt=Time+1;
            return new InputFrame{Yaw=input.Yaw,Pitch=input.Pitch,AimTarget=input.AimTarget,AbilityTarget=input.AbilityTarget,Shoot=p.DevInput=="primary",Attack=tap&&p.DevInput=="primary",AbilityQ=tap&&p.DevInput=="q",AbilityE=tap&&p.DevInput=="e",Block=tap&&p.DevInput=="block",Roll=tap&&p.DevInput=="dash",Jump=tap&&p.DevInput=="jump",SirenAbility=tap&&byte.TryParse(p.DevInput,out byte n)?n:(byte)0,Hadoken=tap&&p.DevInput=="1",Uppercut=tap&&p.DevInput=="2"};
        }
    }
}
