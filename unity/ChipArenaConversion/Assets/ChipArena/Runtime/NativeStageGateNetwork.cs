using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private NativeArena gateArena;private string gateMap;private int gateEpoch=-1,gateSerial,guestGateSerial,guestGateGeneration;
        private float gateNextSend,gateNextReady;
        private readonly NativeStageBarrier stageBarrier=new NativeStageBarrier();
        private void StageEnsureGate()
        {
            if(arena==null||arena.Replica)return;
            if(ReferenceEquals(gateArena,arena)&&gateMap==arena.MapId&&gateEpoch==networkEpoch)return;
            gateArena=arena;gateMap=arena.MapId;gateEpoch=networkEpoch;gateSerial++;gateNextSend=0;
            var gateRequired=new HashSet<int>();
            if(!dedicated)gateRequired.Add(arena.LocalId);
            if(Hosting)foreach(var peer in peers)if(peer.Seat!=null&&peer.Seat.connected&&peer.Connection?.Closed==false&&match.Seat(peer.Seat.id)!=null)gateRequired.Add(peer.Seat.id);
            stageBarrier.Begin(networkEpoch,gateMap,gateSerial,gateRequired,Time.unscaledTime);StageGate.Waiting=stageBarrier.Waiting;
        }
        private bool StageAuthorityReady()
        {
            StageEnsureGate();if(arena==null||arena.Replica)return true;
            if(!dedicated&&ReferenceEquals(stageSeen,arena)&&stageSeenMap==arena.MapId&&StagePresentationReady&&StagePresentation.Generation==arena.Generation)stageBarrier.Report(arena.LocalId,networkEpoch,gateMap,gateSerial);
            stageBarrier.Step(Time.unscaledTime,id=>id==arena.LocalId||peers.Any(p=>p.Seat?.id==id&&p.Seat.connected&&p.Connection?.Closed==false));
            StageGate.Required=stageBarrier.Required;StageGate.Ready=stageBarrier.Ready;StageGate.Remaining=stageBarrier.Remaining(Time.unscaledTime);StageGate.Waiting=stageBarrier.Waiting;
            return !StageGate.Waiting;
        }
        private void StageNetworkTick()
        {
            if(arena==null){StageGate.Clear();gateArena=null;return;}
            if(!arena.Replica)
            {
                StageAuthorityReady();if(!Hosting||Time.unscaledTime<gateNextSend)return;gateNextSend=Time.unscaledTime+.25f;
                string packet=NativeNetCodec.Encode(new NativeNetMessage{type="stageGate",epoch=networkEpoch,generation=arena.Generation,stageMap=arena.MapId,stageSerial=gateSerial,stageWaiting=StageGate.Waiting,stageReady=StageGate.Ready,stageRequired=StageGate.Required,remainingSeconds=StageGate.Remaining});
                foreach(var peer in peers)if(peer.Seat!=null&&peer.Connection?.Closed==false)peer.Connection.Send(packet);
            }
            else if(server?.Closed==false&&guestGateSerial>0&&StagePresentationReady&&StagePresentation.Generation==guestGateGeneration&&Time.unscaledTime>=gateNextReady)
            {
                gateNextReady=Time.unscaledTime+.5f;
                SendServer(new NativeNetMessage{type="stage-ready",stageMap=arena.MapId,stageSerial=guestGateSerial});
            }
        }
        private bool StageHostMessage(NativePeer peer,NativeNetMessage message)
        {
            if(message.type!="stage-ready")return false;StageEnsureGate();
            if(peer.Seat!=null&&arena!=null&&message.epoch==networkEpoch&&message.generation==arena.Generation&&message.stageMap==gateMap&&message.stageSerial==gateSerial)stageBarrier.Report(peer.Seat.id,message.epoch,message.stageMap,message.stageSerial);
            return true;
        }
        private bool StageClientMessage(NativeNetMessage message)
        {
            if(message.type!="stageGate")return false;
            if(arena==null||!arena.Replica||message.epoch!=networkEpoch||message.generation!=arena.Generation||message.stageMap!=arena.MapId)return true;
            if(message.stageSerial<1||message.stageReady<0||message.stageRequired<message.stageReady||message.stageRequired>8||!MathUtil.Finite(message.remainingSeconds)||message.remainingSeconds<0||message.remainingSeconds>12)throw new ArgumentException("Invalid stage gate");
            if(guestGateSerial!=message.stageSerial){guestGateSerial=message.stageSerial;gateNextReady=0;}
            guestGateGeneration=message.generation;StageGate.Waiting=message.stageWaiting;StageGate.Ready=message.stageReady;StageGate.Required=message.stageRequired;StageGate.Remaining=message.remainingSeconds;return true;
        }
    }
}
