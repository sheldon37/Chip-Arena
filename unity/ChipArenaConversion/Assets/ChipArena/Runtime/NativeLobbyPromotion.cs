using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private ExpeditionDeploymentRoom promotionDeployment;
        private string promotionToken,promotionCode;private NativePeer promotionPeer;
        private int promotionTeam=-1,promotionExpected;private bool promotionOnline;private double promotionDeadline,promotionMoveAt;
        private CancellationTokenSource promotionStop;private Task<INativeOnlineHost> promotionCreating;
        private INativeOnlineHost promotionHost;private NativeListener promotionListener;
        private NativeRoomOptions promotionOptions;private NativeConnection promotionPrevious;
        private bool PromotionBusy=>promotionToken!=null||promotionMoveAt>0||promotionExpected>peers.Count(p=>p.Seat!=null&&p.Connection?.Closed==false)&&NetworkNow<promotionDeadline;
        private bool CanPromote(int id)=>Hosting&&!dedicated&&arena==null&&expeditionSession==null&&!stageLoad.Requested&&
            peers.Any(p=>p.Seat?.id==id&&p.Seat.connected&&p.Connection?.Closed==false&&p.LobbyProtocol==1)&&
            peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false).All(p=>p.LobbyProtocol==1);
        private void PromotionButton(Rect r,LobbyPilot p,float alpha)
        {if(CanPromote(p.id)&&MenuButton(r,"crown","Make host",null,false,!PromotionBusy,alpha,11,true))PromoteHost(p.id);}
        private void PromoteHost(int id)
        {
            if(PromotionBusy||!CanPromote(id))return;
            promotionPeer=peers.First(p=>p.Seat?.id==id);promotionToken=RandomToken(16);promotionDeadline=NetworkNow+45;
            promotionOnline=relayHost!=null;networkStatus="Preparing "+promotionPeer.Seat.name+" as host…";
            promotionPeer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="host-prepare",deploymentHandoff=deploymentRoom==null?null:JsonUtility.ToJson(deploymentRoom.Ticket),transfer=promotionToken,transferOnline=promotionOnline,id=peers.Count(p=>p.Seat!=null&&p.Connection?.Closed==false)}));
        }
        private void CancelHostPromotion()
        {
            promotionDeployment=null;promotionStop?.Cancel();promotionStop=null;
            if(promotionCreating!=null){var task=promotionCreating;_=task.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();});}
            promotionCreating=null;promotionHost?.Dispose();promotionHost=null;promotionListener?.Dispose();promotionListener=null;
            promotionPrevious?.Dispose();promotionPrevious=null;promotionToken=null;promotionPeer=null;promotionCode=null;promotionOptions=null;promotionMoveAt=0;promotionExpected=0;promotionTeam=-1;
        }
        private bool PromotionHostMessage(NativePeer peer,NativeNetMessage message)
        {
            if(message.type!="host-prepared"&&message.type!="host-live"&&message.type!="host-failed")return false;
            if(peer!=promotionPeer||message.transfer!=promotionToken||arena!=null)return true;
            if(message.type=="host-failed"){CancelHostPromotion();networkStatus="Could not change host. The current lobby is still open.";return true;}
            if(message.type=="host-prepared")
            {
                if(!NativeRoomCodes.Valid(message.room))return true;promotionCode=message.room;
                peer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="host-commit",transfer=promotionToken}));return true;
            }
            if(promotionCode==null)return true;
            foreach(var other in peers.Where(p=>p!=peer&&p.Seat!=null&&p.Connection?.Closed==false))
                other.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="host-move",transfer=promotionToken,room=promotionCode,transferOnline=promotionOnline,team=other.Seat.team}));
            promotionTeam=prefs.team;promotionMoveAt=NetworkNow+1;networkStatus="Moving lobby to the new host…";return true;
        }
        private bool PromotionClientMessage(NativeNetMessage message)
        {
            if(message.type=="host-prepare")
            {
                if(server==null||networkId<1||arena!=null||receivedOptions==null||PromotionBusy||message.transfer?.Length!=32)return true;
                promotionExpected=Math.Max(0,Math.Min(7,message.id));promotionToken=message.transfer;promotionOnline=message.transferOnline;promotionOptions=receivedOptions;
                promotionOptions.hostTeam=lobbySeats.FirstOrDefault(s=>s.id==networkId)?.team??0;
                promotionDeadline=NetworkNow+45;networkStatus="Preparing your host room…";
                try{
                    promotionDeployment=null;
                    if(!string.IsNullOrEmpty(message.deploymentHandoff))
                    {
                        if(message.deploymentHandoff.Length>4096||account?.Access==null||account.Access.Expires<=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())throw new ArgumentException("Invalid deployment handoff.");
                        var ticket=JsonUtility.FromJson<ExpeditionDeploymentTicket>(message.deploymentHandoff);
                        if(deploymentLayout==null)deploymentLayout=LoadDeploymentLayout();
                        if(deploymentReceiver==null||!deploymentReceiver.Accept(ticket,deploymentLayout,content))throw new ArgumentException("Deployment handoff does not match the current station.");
                        promotionDeployment=ExpeditionDeploymentRoom.Restore(ticket,account.Access.AccountId,deploymentLayout,content);
                    }
                    else if(DeploymentState!=null)throw new ArgumentException("Deployment handoff is missing.");
                    if(promotionOnline){promotionStop=new CancellationTokenSource();promotionCreating=CreateOnlineRoom(promotionStop.Token);}
                    else{promotionListener=new NativeListener(0);promotionCode=NativeRoomCodes.Create();PromotionPrepared();}}
                catch(Exception e){Debug.LogWarning("Host preparation: "+e.Message);SendServer(new NativeNetMessage{type="host-failed",transfer=promotionToken});CancelHostPromotion();}
                return true;
            }
            if(message.type=="host-cancel")
            {if(message.transfer==promotionToken){CancelHostPromotion();networkStatus="Host change cancelled. Staying in this lobby.";}return true;}
            if(message.type=="host-commit")
            {
                if(message.transfer!=promotionToken||promotionCode==null||server==null||arena!=null||promotionHost==null&&promotionListener==null)return true;
                // Keep the old transport alive until its acknowledgement has had time to drain.
                SendServer(new NativeNetMessage{type="host-live",transfer=promotionToken});promotionPrevious=server;server=null;
                relayHost=promotionHost;promotionHost=null;listener=promotionListener;promotionListener=null;roomCode=promotionCode;
                deploymentRoom=promotionDeployment;promotionDeployment=null;deploymentReceiver=null;
                receivedOptions=null;SetRoomOptions(promotionOptions);networkId=1;networkEpoch++;networkReady=false;lobbySeats=Array.Empty<NativeSeatFrame>();
                if(listener!=null)try{lanDiscovery=new NativeLanDiscovery(roomCode,content.sourceCommit,listener.Port);}catch(System.Net.Sockets.SocketException){ }
                promotionMoveAt=NetworkNow+2;promotionToken=null;LobbyBroadcast();networkStatus="You are now the host · "+RoomCode;return true;
            }
            if(message.type=="host-move")
            {
                if(server==null||networkId<1||arena!=null||!NativeRoomCodes.Valid(message.room))return true;
                promotionTeam=Math.Max(0,Math.Min(7,message.team));promotionCode=message.room;promotionOnline=message.transferOnline;promotionMoveAt=NetworkNow+.1;networkStatus="Joining the new host…";return true;
            }
            return false;
        }
        private void PromotionPrepared()=>SendServer(new NativeNetMessage{type="host-prepared",transfer=promotionToken,room=promotionCode});
        private void PromotionTick()
        {
            if(promotionPrevious!=null&&NetworkNow>=promotionMoveAt){promotionPrevious.Dispose();promotionPrevious=null;promotionMoveAt=0;promotionOptions=null;promotionCode=null;}
            if(promotionMoveAt>0&&promotionPrevious==null&&NetworkNow>=promotionMoveAt)
            {
                string code=promotionCode;bool online=promotionOnline;int team=promotionTeam;CloseNetwork();promotionTeam=team;resumeToken="";resumeRoom="";
                internetRooms=online;joinCode=code;hostAddress="";JoinRoom();return;
            }
            if(promotionToken==null)return;
            if(NetworkNow>=promotionDeadline||promotionPeer!=null&&promotionPeer.Connection?.Closed!=false||promotionPeer==null&&server==null)
            {
                if(promotionPeer?.Connection?.Closed==false)promotionPeer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="host-cancel",transfer=promotionToken}));
                CancelHostPromotion();networkStatus="Host change timed out. The current lobby is still open.";return;
            }
            if(promotionCreating?.IsCompleted==true)
            {
                var task=promotionCreating;promotionCreating=null;
                if(task.IsCanceled||task.IsFaulted){SendServer(new NativeNetMessage{type="host-failed",transfer=promotionToken});CancelHostPromotion();networkStatus="Could not create the replacement host room.";return;}
                promotionHost=task.Result;promotionCode=promotionHost.Room;PromotionPrepared();
            }
        }
    }
}
