using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // Claude's settings toggle sets this opt-in. It is deliberately not sent as authorization.
        public bool DevAbuseEnabled {get;set;}
        public NativeDevReply DevLastReply {get;private set;}
        public event Action<NativeDevReply> DevReplyReceived;
        private int devHostProtocol;
        private readonly Dictionary<string,float> devPending=new Dictionary<string,float>();
        public string SendDevCommand(string action,int target=0,params string[] args)
        {
            string transaction=Guid.NewGuid().ToString("N");var command=new NativeDevCommand{transaction=transaction,action=action,target=target,args=args};
            string invalid=NativeDevRules.Validate(command);
            if((!DevAbuseEnabled&&!(arena?.Sandbox==true&&!Online&&account?.Developer==true))||arena==null||invalid!=null){DevReply(new NativeDevReply{transaction=transaction,state="refused",reason=invalid??(!DevAbuseEnabled?"Enable DEV Abuse first.":"No active match.")});return transaction;}
            if(server!=null)
            {
                if(devHostProtocol<(action=="spawn-monster"||action=="clear-monsters"?2:1)){DevReply(new NativeDevReply{transaction=transaction,state="refused",reason="This host does not support DEV commands."});return transaction;}
                if(devPending.Count>=32){DevReply(new NativeDevReply{transaction=transaction,state="refused",reason="Too many pending DEV commands."});return transaction;}
                devPending[transaction]=Time.unscaledTime;SendServer(new NativeNetMessage{type="dev-command",devCommand=command});
            }
            else
            {
                var seat=arena.Local?.Seat;
                // Local host/offline grants are verified exactly like remote grants, never from the UI toggle.
                if(seat!=null)seat.access=NativeAccess.Verify(account?.Token,s=>JsonUtility.FromJson<NativePlayerClaim>(s),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                DevReply(arena.DevCommand(seat,command,networkEpoch,arena.Generation,networkEpoch,Time.unscaledTime));
            }
            return transaction;
        }
        private void DevReply(NativeDevReply reply){DevLastReply=reply;DevReplyReceived?.Invoke(reply);}
        private bool DevHostMessage(NativePeer peer,NativeNetMessage message)
        {
            if(message.type!="dev-command")return false;
            NativeDevReply reply;
            if(message.version!=NativeConnection.Protocol)reply=new NativeDevReply{transaction=message.devCommand?.transaction?.Length<=64?message.devCommand.transaction:"",state="refused",reason="Incompatible DEV protocol."};
            else if(arena==null)reply=new NativeDevReply{transaction=message.devCommand?.transaction?.Length<=64?message.devCommand.transaction:"",state="refused",reason="No active match."};
            else reply=arena.DevCommand(peer.Seat,message.devCommand,message.epoch,message.generation,networkEpoch,Time.unscaledTime);
            peer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="dev-reply",epoch=networkEpoch,devReply=reply}));return true;
        }
        private bool DevClientMessage(NativeNetMessage message)
        {
            if(message.type!="dev-reply")return false;
            var reply=message.devReply;if(reply==null||reply.transaction==null||reply.transaction.Length>64||reply.reason==null||reply.reason.Length>200||reply.state!="accepted"&&reply.state!="refused"&&reply.state!="done")throw new ArgumentException("Invalid DEV reply");
            if(message.epoch!=networkEpoch||!devPending.ContainsKey(reply.transaction))return true;
            if(reply.state!="accepted")devPending.Remove(reply.transaction);DevReply(reply);return true;
        }
        private void ResetDevNetwork(){devHostProtocol=0;devPending.Clear();DevLastReply=null;Time.timeScale=1;}
        private void DevNetworkTick()
        {
            foreach(var transaction in new List<string>(devPending.Keys))if(Time.unscaledTime-devPending[transaction]>10){devPending.Remove(transaction);DevReply(new NativeDevReply{transaction=transaction,state="refused",reason="Host reply timed out; command outcome is unknown."});}
            if(arena!=null&&(!arena.Sandbox||arena.DevClockChanged))Time.timeScale=arena.DevTimeScale;
            else if(arena==null)Time.timeScale=1;
        }
    }
}
