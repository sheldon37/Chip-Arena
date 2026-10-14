using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private ExpeditionSession expeditionSession;private ExpeditionCommandRouter expeditionRouter;
        private ExpeditionViewReceiver expeditionReceiver;private ExpeditionGuestCommands expeditionGuest;
        private string expeditionStamp;private float expeditionNextSend;private IExpeditionCommands expeditionLocal;
        public ExpeditionView ExpeditionView{get;private set;}
        public IExpeditionCommands ExpeditionCommands=>server!=null?(IExpeditionCommands)expeditionGuest:
            expeditionLocal;

        /// <summary>Scene/director entry point. No menu is enabled until authored scene and combat adapters exist.</summary>
        public void BindExpedition(ExpeditionSession session)
        {
            if(server!=null)throw new InvalidOperationException("Only the authority binds a run.");
            var id=account?.Access.AccountId;
            if(session==null||string.IsNullOrEmpty(id)||session.Run.Snapshot().host!=id)throw new ArgumentException("Expedition host must be the signed-in account.");
            expeditionSession=session;expeditionRouter=new ExpeditionCommandRouter(session);expeditionStamp=null;expeditionNextSend=0;
            expeditionLocal=new ExpeditionLocalCommands(session,id);
            session.Sources.DisplayName=who=>who==account?.Access.AccountId?NativeCosmeticRules.Name(account.Account?.profile?.name):peers.FirstOrDefault(p=>p.Seat?.access?.AccountId==who)?.Seat.name;
            session.Sources.Seat=who=>match?.Seats.FirstOrDefault(s=>s.access?.AccountId==who)?.id??0;
            ExpeditionView=NativeExpeditionView.Build(session.Run,id,session.Sources);
        }
        /// <summary>Uses the existing durable desktop store. Scene supplies anchors and interaction evidence before binding.</summary>
        public ExpeditionSession CreateExpeditionSession(NativeExpedition run)
        {var store=NativeExpeditionPersistence.Open(run.Snapshot().run);var saved=store.Read();if(store.Exists&&saved==null)throw new InvalidOperationException("Existing Expedition save is unreadable; refusing to overwrite it.");return new ExpeditionSession(run,content,store.Write,saved);}
        /// <summary>Authored scene supplies actual positions and measured interactions; this overload completes session binding.</summary>
        public ExpeditionSession BindExpeditionScene(NativeExpedition run,ExpeditionWorldAnchor[] anchors,ExpeditionChestOffer[] chests,
            Func<string,string,bool> canClaim,Func<string,string,bool> canPurchase,
            Func<string,string,ExpeditionReprintEvidence> reprint,Func<string,int> reprintCost)
        {
            if(server!=null||run==null||run.Snapshot().host!=account?.Access.AccountId)throw new InvalidOperationException("Only the signed-in authority can bind the scene.");
            if(canClaim==null||canPurchase==null||reprint==null||reprintCost==null)throw new ArgumentException("Scene interaction evidence is required.");
            var session=CreateExpeditionSession(run);session.SetWorld(anchors,chests);
            session.CanClaim=canClaim;session.CanPurchase=canPurchase;session.ReprintEvidence=reprint;session.Sources.ReprintCost=reprintCost;
            BindExpedition(session);return session;
        }
        /// <summary>Pickup/cache interactions use the same authenticated command path locally and online.</summary>
        public ExpeditionCommandResult InteractExpedition(string target,bool purchase=false)
        {
            var commands=ExpeditionCommands;
            return commands==null?ExpeditionCommandResult.Of(ExpeditionCommandState.Unavailable,"No Expedition session is bound."):
                commands.Send(new ExpeditionCommand(purchase?ExpeditionIntent.Purchase:ExpeditionIntent.Claim,target));
        }
        private void ResetExpeditionNetwork()
        {expeditionSession=null;expeditionLocal=null;expeditionRouter=null;expeditionReceiver=null;expeditionGuest=null;ExpeditionView=null;expeditionStamp=null;}
        private void ExpeditionNetworkTick()
        {
            try{ExpeditionNetworkStep();}
            catch(Exception e)when(e is System.IO.IOException||e is InvalidOperationException||e is ArgumentException){networkStatus="Expedition synchronization failed: "+e.Message;}
        }
        private void ExpeditionNetworkStep()
        {
            if(expeditionSession==null||server!=null)return;
            expeditionSession.Tick(Time.unscaledDeltaTime);
            // Projection and transport metrics are bounded to 20 Hz, including offline sessions.
            if(Time.unscaledTime<expeditionNextSend)return;expeditionNextSend=Time.unscaledTime+.05f;
            // Link metrics are measured. No endpoint has been reserved/probed, so nobody is migration-capable yet.
            expeditionSession.Sources.Candidates=peers.Where(p=>p.Seat?.access?.AccountId!=null&&p.Connection!=null).Select(p=>
            {var m=p.Connection.Metrics.Report(!p.Connection.Closed);return new ExpeditionHostCandidate{account=p.Seat.access.AccountId,loss=m.loss,latencyMs=m.rttMs,jitterMs=m.jitterMs,reachable=false,capable=false};}).ToArray();
            ExpeditionView=NativeExpeditionView.Build(expeditionSession.Run,account?.Access.AccountId,expeditionSession.Sources);
            if(!Hosting)return;
            var snapshot=expeditionSession.Run.Snapshot();
            var recipients=peers.Where(p=>p.Connection?.Closed==false&&p.Seat?.access!=null&&p.Seat.access.Expires>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()&&snapshot.members.Any(m=>m.account==p.Seat.access.AccountId)).ToArray();
            var batch=ExpeditionViewBatch.Build(expeditionSession.Run,recipients.Select(p=>p.Seat.access.AccountId),expeditionSession.Sources);
            string common=NativeNetCodec.Encode(new NativeNetMessage{type="expedition-public",expeditionPublic=batch.Public});
            // Also refresh when source-only progress/save metadata or the peer set changes at an unchanged rule revision.
            string stamp=common+"|"+string.Join(",",recipients.Select(p=>p.Resume));if(stamp==expeditionStamp)return;expeditionStamp=stamp;
            byte[] packet=NativeConnection.Encode(common);
            foreach(var peer in recipients)
            {
                peer.Connection.SendEncoded(packet);
                peer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-private",expeditionPrivate=batch.Private.First(p=>p.viewer==peer.Seat.access.AccountId)}));
            }
        }
        private bool ExpeditionHostMessage(NativePeer peer,NativeNetMessage message)
        {
            if(message.type!="expedition-command")return false;
            var request=message.expeditionCommand;ExpeditionCommandReply reply;
            if(!peer.Changes.Allow(Time.unscaledTime))reply=new ExpeditionCommandReply{transaction=request?.transaction,state=ExpeditionCommandState.Refused,reason="Too many commands. Try again shortly."};
            else if(expeditionRouter==null||peer.Seat?.access==null||peer.Seat.access.Expires<=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                reply=new ExpeditionCommandReply{transaction=request?.transaction,state=ExpeditionCommandState.Unavailable,reason="Expedition or account authentication is unavailable."};
            else reply=expeditionRouter.Execute(peer.Seat.access.AccountId,request);
            peer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-reply",expeditionReply=reply}));return true;
        }
        private bool ExpeditionClientMessage(NativeNetMessage message)
        {
            if(message.type!="expedition-public"&&message.type!="expedition-private"&&message.type!="expedition-reply")return false;
            string id=account?.Access.AccountId;if(string.IsNullOrEmpty(id)||networkId<1)return true;
            if(expeditionReceiver==null)
            {
                expeditionReceiver=new ExpeditionViewReceiver(id);
                expeditionGuest=new ExpeditionGuestCommands(id,()=>ExpeditionView,request=>SendServer(new NativeNetMessage{type="expedition-command",expeditionCommand=request}));
            }
            if(message.type=="expedition-public")expeditionReceiver.Public(message.expeditionPublic);
            else if(message.type=="expedition-private")expeditionReceiver.Private(message.expeditionPrivate);
            else expeditionGuest.Reply(message.expeditionReply);
            ExpeditionView=expeditionReceiver.View;return true;
        }
        private sealed class ExpeditionLocalCommands:IExpeditionCommands
        {
            private readonly ExpeditionSession session;public string Viewer{get;}
            public ExpeditionLocalCommands(ExpeditionSession session,string viewer){this.session=session;Viewer=viewer;}
            public string Blocked(ExpeditionCommand c)=>new ExpeditionHostCommands(session.Run,Viewer,session.Hooks()).Blocked(c);
            public ExpeditionCommandResult Send(ExpeditionCommand c)=>session.Execute(Viewer,c);
        }
    }
}
