using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private ExpeditionDeploymentRoom deploymentRoom;
        private ExpeditionDeploymentReceiver deploymentReceiver;
        private ExpeditionLayout deploymentLayout;
        public ExpeditionDeploymentState DeploymentState=>deploymentRoom?.Snapshot()??deploymentReceiver?.Snapshot;
        // Claude's station entry point. Does not construct an arena, run, rewards or combat clock.
        public bool BeginDeploymentStation(string expedition)
        {
            if(server!=null||dedicated||arena!=null||expeditionSession!=null||PromotionBusy||deploymentRoom!=null)return false;
            var host=account?.Access;
            if(host==null||host.Expires<=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()||!ExpeditionValidation.Id(host.AccountId))return false;
            var guests=peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false).ToArray();
            if(NativeExpeditionParty.Validate(new[]{host.AccountId}.Concat(guests.Select(p=>p.Seat.access?.AccountId)),
                guests.All(p=>p.Seat.access!=null&&p.Seat.access.Expires>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),true,
                guests.All(p=>p.DeploymentProtocol==1))!=null)return false;
            try
            {
                deploymentLayout=LoadDeploymentLayout();
                deploymentRoom=new ExpeditionDeploymentRoom("deploy-"+Guid.NewGuid().ToString("N"),host.AccountId,NativeMatch.NewSeed(),expedition,deploymentLayout,content);
                LobbyBroadcast();return true;
            }
            catch(ArgumentException e){networkStatus=e.Message;return false;}
            catch(FormatException e){networkStatus=e.Message;return false;}
        }
        private ExpeditionLayout LoadDeploymentLayout()
        {
            var json=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName)?.text;
            var layout=NativeExpeditionLayout.Parse(json);
            layout.compatibility=NativeExpeditionLayout.Compatibility(json,content);return layout;
        }
        public bool RollDeploymentInstances(int revision)=>DeploymentIntent(ExpeditionDeploymentIntent.RollInstances,revision);
        public bool SkipDeploymentMutators(int revision)=>DeploymentIntent(ExpeditionDeploymentIntent.SkipMutators,revision);
        private bool DeploymentIntent(ExpeditionDeploymentIntent intent,int revision)
        {
            // Only the local authenticated host issues rolls. Remote peers are always guests.
            if(server!=null||arena!=null||expeditionSession!=null||PromotionBusy||
               account?.Access==null||account.Access.Expires<=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())return false;
            if(deploymentRoom?.Execute(account.Access.AccountId,revision,intent)!=true)return false;
            LobbyBroadcast();return true;
        }
        private void BroadcastDeployment()
        {
            if(deploymentRoom==null)return;
            var packet=NativeNetCodec.Encode(new NativeNetMessage{type="deployment-state",epoch=networkEpoch,deployment=deploymentRoom.Ticket});
            foreach(var peer in peers.Where(p=>p.Seat?.access!=null&&p.Connection?.Closed==false&&
                p.Seat.access.Expires>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))peer.Connection.Send(packet);
        }
        private bool DeploymentClientMessage(NativeNetMessage message)
        {
            if(message.type!="deployment-state")return false;
            if(server==null||networkId<1||message.epoch!=networkEpoch)return true;
            try
            {
                if(deploymentLayout==null)deploymentLayout=LoadDeploymentLayout();
                if(deploymentReceiver==null)deploymentReceiver=new ExpeditionDeploymentReceiver();
                if(!deploymentReceiver.Accept(message.deployment,deploymentLayout,content))
                    networkStatus="Deployment selection is stale or incompatible. Rejoin with the same game version.";
            }
            catch(Exception e)when(e is ArgumentException||e is FormatException)
            {networkStatus="Deployment content is incompatible: "+e.Message;}
            return true;
        }
        private void ResetDeploymentRoom(){deploymentRoom=null;deploymentReceiver=null;deploymentLayout=null;}
    }
}
