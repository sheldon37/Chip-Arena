using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private ExpeditionLayout expeditionReplicaLayout;private string expeditionReplicaJson,expeditionReplicaIdentity;
        private int expeditionReplicaGeneration=-1;
        private bool ExpeditionGuestBlocked=>arena?.Replica==true&&prefs.expedition&&
            (ExpeditionView==null||ExpeditionView.loading||ExpeditionView.mine?.alive!=true||ExpeditionView.mine.extracted||ExpeditionView.Member(ExpeditionView.viewer)?.choosing==true||!arena.ExpeditionCollisionReady||expeditionReplicaGeneration!=arena.Generation);
        private void ExpeditionReplicaTick()
        {
            if(!prefs.expedition||arena?.Replica!=true||ExpeditionView==null||arena.Generation!=expeditionReplicaGeneration)return;
            if(expeditionReplicaLayout==null){expeditionReplicaJson=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text;expeditionReplicaLayout=NativeExpeditionLayout.Parse(expeditionReplicaJson);expeditionReplicaIdentity=NativeExpeditionLayout.Compatibility(expeditionReplicaJson,content);}
            string json=expeditionReplicaJson;
            var view=ExpeditionView;var area=expeditionReplicaLayout.Area(view.area);
            if(area==null||arena.MapId!=area.map)return;
            arena.ApplyExpeditionCollision(view,area,json);
            if(!view.loading||!StagePresentationReady||StagePresentation.Generation!=arena.Generation||StagePresentation.Map!=area.map)return;
            var ticket=ExpeditionAreaReady.Capture(view);
            if(ticket==null||expeditionReplicaIdentity!=ticket.layout)
            {networkStatus="Expedition layout does not match the host. Update before rejoining.";return;}
            if(expeditionLoadedTicket==null)ReportExpeditionAreaLoaded(ticket);
        }
    }
    public sealed partial class NativeArena
    {
        private readonly HashSet<string> expeditionDown=new HashSet<string>();
        private readonly Dictionary<string,(string target,string machine,double began,Vector3 position,float hp)> expeditionChannels=new Dictionary<string,(string,string,double,Vector3,float)>();
        private int expeditionCollisionGeneration=-1;private ExpeditionHubLayout expeditionReplicaHub;
        public bool ExpeditionCollisionReady=>expeditionCollisionGeneration==Generation;
        private NativePilot ExpeditionPilotFor(string account)=>Pilots.Find(p=>p.Seat.id==Expedition?.Session.Sources.Seat?.Invoke(account));
        public bool PlaceExpeditionParty(IReadOnlyDictionary<string,ExpeditionPoint> placements)
        {
            // Validate the whole batch before changing any pilot's position.
            if(placements.Any(p=>ExpeditionPilotFor(p.Key)==null||!ExpeditionSupported(p.Value.Position,NativeLocomotionRules.Radius,2*NativeLocomotionRules.HalfHeight)))return false;
            foreach(var p in placements)if(!ExpeditionPlace(p.Key,p.Value))return false;
            return true;
        }
        public void RestoreExpeditionMembers()
        {
            if(Expedition==null||Replica)return;
            expeditionChannels.Clear();
            foreach(var member in Expedition.Session.Run.Snapshot().members)
            {
                var p=ExpeditionPilotFor(member.account);if(p==null)continue;
                bool active=member.alive&&!member.extracted&&member.connected;
                p.Seat.alive=active;p.Collider.enabled=active;p.Body.isKinematic=!active;
                if(!member.alive||member.extracted)p.Hp=0;
                if(!member.alive)expeditionDown.Add(member.account);
            }
        }
        private void RecordExpeditionDeaths()
        {
            foreach(var m in Expedition.Session.Run.Snapshot().members)
            {
                var p=ExpeditionPilotFor(m.account);
                if(p!=null&&p.Hp<=0&&!expeditionDown.Contains(m.account)&&m.alive&&m.connected&&!m.extracted&&Expedition.Session.Run.Defeat(m.account))expeditionDown.Add(m.account);
            }
        }
        private void ExpeditionInput(NativePilot p)
        {
            if(Expedition==null||p.Seat.bot)return;
            var member=ExpeditionMemberFor(p);
            if(p.ExpeditionTransferHeld||member==null||!member.alive||member.extracted||ExpeditionChoosing(p))
            {p.Input=new InputFrame{Yaw=p.Yaw,Pitch=p.Pitch};return;}
            if(!p.Input.Interact)return;
            bool used=TryExpeditionInteract(p,out string notice);
            if(!used&&!Expedition.Session.Run.SplitAreas)foreach(var door in expeditionArea.doors)if(Expedition.EnterDoor(member.account,door.anchor)){used=true;break;}
            if(!used&&Expedition.Session.Run.SplitAreas)foreach(var door in expeditionArea.doors)if(Expedition.AtDoor(member.account,door.anchor)&&Expedition.Session.Run.VoteExit(member.account,true)){used=true;notice="Return vote recorded";break;}
            if(used){p.Input.Interact=false;if(Pve.Rows.TryGetValue(p.Seat.id,out var row)){row.Notice=notice??"Travelling with party";row.NoticeUntil=Time+3;}}
        }
        private string BeginExpeditionReprint(NativePilot p,ExpeditionMember member,string machine)
        {
            var dead=Expedition.Session.Run.PartySnapshot().members.FirstOrDefault(m=>!m.alive&&m.connected&&!m.extracted);
            if(dead==null)return "No fallen teammates to reprint.";
            expeditionChannels[member.account]=(dead.account,machine,Expedition.Session.Run.Elapsed,p.Body.position,p.Hp);
            return "Reprinting teammate · stay still for 3 seconds";
        }
        public ExpeditionReprintEvidence ExpeditionReprintEvidence(string payer,string target)
        {
            if(!expeditionChannels.TryGetValue(payer,out var channel)||channel.target!=target)return null;
            var p=ExpeditionPilotFor(payer);
            bool uninterrupted=p!=null&&p.Hp>0&&p.Hp>=channel.hp&&p.Seat.connected&&(p.Body.position-channel.position).sqrMagnitude<.0625f;
            return Expedition.MachineEvidence(payer,channel.machine,Expedition.Session.Run.Elapsed-channel.began,uninterrupted);
        }
        private void StepExpeditionReprints()
        {
            foreach(var entry in expeditionChannels.ToArray())
            {
                var evidence=ExpeditionReprintEvidence(entry.Key,entry.Value.target);
                if(evidence==null||!evidence.uninterrupted){expeditionChannels.Remove(entry.Key);continue;}
                if(!evidence.Valid)continue;
                var result=Expedition.Session.Execute(entry.Key,new ExpeditionCommand(ExpeditionIntent.Reprint,entry.Value.target));
                var payer=ExpeditionPilotFor(entry.Key);
                if(payer!=null&&Pve.Rows.TryGetValue(payer.Seat.id,out var row)){row.Notice=result.state==ExpeditionCommandState.Pending?"Teammate reprinted":result.reason;row.NoticeUntil=Time+3;}
                expeditionChannels.Remove(entry.Key);
            }
            foreach(var m in Expedition.Session.Run.Snapshot().members)
            {
                var p=ExpeditionPilotFor(m.account);if(p==null)continue;
                if(m.extracted){p.Hp=0;p.Seat.alive=false;p.Collider.enabled=false;p.Body.isKinematic=true;continue;}
                if(m.alive&&m.connected&&expeditionDown.Contains(m.account))
                {
                    var slot=expeditionArea.slots.FirstOrDefault(s=>ExpeditionSupported(s.Position,NativeLocomotionRules.Radius,2*NativeLocomotionRules.HalfHeight)&&Pilots.All(other=>other==p||other.Hp<=0||(other.Body.position-(Vec(s.Position)+Vector3.up*NativeLocomotionRules.HalfHeight)).magnitude>2*NativeLocomotionRules.Radius+.1f));
                    if(slot==null)continue;
                    RevivePve(p,Vec(slot.Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f));expeditionDown.Remove(m.account);
                }
            }
        }
        public void ApplyExpeditionCollision(ExpeditionView view,ExpeditionAreaLayout area,string json)
        {
            if(!Replica||MapId!=area.map)return;
            BoxCollider Box(string id,ExpeditionVolume v,float y,float height)
            {
                var obj=new GameObject(id);obj.layer=PropLayer;obj.transform.SetParent(root.transform);
                obj.transform.position=new Vector3(v.x,y,v.z);obj.transform.rotation=Quaternion.Euler(0,v.yaw,0);
                var box=obj.AddComponent<BoxCollider>();box.size=new Vector3(v.sx,height,v.sz);return box;
            }
            if(expeditionCollisionGeneration!=Generation)
            {
                expeditionCollisionGeneration=Generation;
                foreach(var door in area.doors)expeditionClosures[door.anchor]=Box("Expedition closure "+door.anchor,door.closure,door.closure.y,door.closure.sy);
                foreach(var gate in area.gates)expeditionClosures[gate.id]=Box("Expedition closure "+gate.id,gate.closure,gate.closure.y,gate.closure.sy);
                if(area.role=="world")
                {
                    expeditionReplicaHub=ExpeditionHubLayout.Parse(json,area.id);
                    foreach(var gate in expeditionReplicaHub.Gates){hubDoors.Add(Box(gate.Id,gate.Closure,gate.Closure.y,gate.Closure.sy));hubTops.Add(Box(gate.Id+" no vault",gate.Closure,21.55f,36.9f));}
                }
            }
            foreach(var closure in view.closures)if(closure.area==area.id&&expeditionClosures.TryGetValue(closure.id,out var box))box.enabled=closure.Blocking;
            if(hubDoors.Count>0)
            {
                var layout=expeditionReplicaHub;
                for(int i=0;i<hubDoors.Count;i++){bool solid=view.hub==null||!view.hub.Find(layout.Gates[i].Id,out var state)||state.Blocking;hubDoors[i].enabled=hubTops[i].enabled=solid;}
            }
            Physics.SyncTransforms();
        }
    }
}
