using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // Ids returns a defensive copy; keep one immutable catalog for the station cache.
        private static readonly ExpeditionInstanceTheme[] LiveDeployInstances={
            new ExpeditionInstanceTheme{id="exp-ward-zero",theme="quarantine"},new ExpeditionInstanceTheme{id="exp-transit",theme="rail"},new ExpeditionInstanceTheme{id="exp-foundry",theme="industrial"},
            new ExpeditionInstanceTheme{id=NativeBackroomsGenerator.AreaId,theme="backrooms"},new ExpeditionInstanceTheme{id=NativePoolroomsGenerator.AreaId,theme="poolrooms"}};
        private static readonly string[] LiveDeployMutators=NativeExpeditionMutators.Ids;
        private readonly NativeDeployClock liveDeployClock=new NativeDeployClock();
        private readonly NativeDeployConsole.Party liveDeployParty=new NativeDeployConsole.Party();
        private readonly DeployPilot[] liveDeployPilots=new DeployPilot[NativeDeployPresentation.Pads];
        private readonly int[] liveDeploySeats=new int[NativeDeployPresentation.Pads];
        private bool liveDeployShown;private float liveDeployFov,liveDeployNear;
        private bool LiveDeployment=>arena==null&&(deploymentRoom!=null||deploymentReceiver?.HasState==true);
        private bool LiveDeploymentBackdrop()
        {
            if(!LiveDeployment)
            {
                LeaveLiveDeployment();return false;
            }
            if(!liveDeployShown){RooftopLeave();liveDeployShown=true;liveDeployFov=cameraView.fieldOfView;liveDeployNear=cameraView.nearClipPlane;liveDeployClock.Reset();}
            var state=DeploymentState;float now=Time.unscaledTime;liveDeployClock.Observe(state,now);
            var seats=server!=null?lobbySeats.Where(s=>!s.bot&&s.connected).ToArray():
                new[]{LobbySeat(SanitizedSeat(1,Profile(),account?.Token),true)}.Concat(peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false).Select(p=>LobbySeat(p.Seat,p.Ready))).ToArray();
            liveDeployParty.Count=Math.Min(seats.Length,liveDeployPilots.Length);int mine=0;
            for(int i=0;i<liveDeployParty.Count;i++)
            {
                var s=seats[i];liveDeploySeats[i]=s.id;liveDeployParty.Names[i]=s.name;liveDeployParty.Ready[i]=s.ready;if(server!=null&&s.id==networkId)mine=i;
                liveDeployPilots[i]=new DeployPilot{Look=new RooftopPilot{Spec=s.spec,BodySkin=NativeBodySkins.Of(s.weaponSkins),Hat=s.hat,Body=s.bodyColor,Accent=s.accentColor,HatColor=s.hatColor,Skins=s.weaponSkins},Pose=s.lobbyPose,Outfit=s.look,Pod=s.pod};
            }
            viewModelCamera.enabled=false;
            // (Mine: the eject's shots follow my own pad: the host's is 0, a guest's its seat's place on the arc)
            var input=new DeployInput{State=state,SinceInstanceRoll=liveDeployClock.SinceInstanceRoll(now),SinceMutatorRoll=liveDeployClock.SinceMutatorRoll(now),SinceDeploy=liveDeployClock.SinceDeploy(now),Time=now,
                Host=server==null&&!PromotionBusy,PartyReady=server==null&&GuestsReady,Reduced=prefs.reducedEffects,NoShake=!prefs.cameraShake,Seats=liveDeployParty.Count,Mine=mine,Ready=liveDeployParty.Ready,Instances=LiveDeployInstances,Mutators=LiveDeployMutators};
            presentation.RenderDeployStation(cameraView,input,liveDeployPilots,Time.unscaledDeltaTime,prefs.reducedEffects,nativeAudio);
            return true;
        }
        private void LeaveLiveDeployment()
        {
            if(!liveDeployShown)return;
            liveDeployShown=false;presentation?.DeployLeave();
            if(cameraView!=null){cameraView.fieldOfView=liveDeployFov;cameraView.nearClipPlane=liveDeployNear;}
            backdropMap=null;
        }
        private bool LiveDeploymentGui(float width,float height)
        {
            if(!LiveDeployment)return false;
            var state=DeploymentState;
            var action=NativeDeployConsole.Draw(presentation?.DeployStage,state,liveDeployParty,server==null&&!PromotionBusy,width,height,Time.unscaledTime,LiveCradle());
            if(action==DeployIntent.RollDestinations)RollDeploymentInstances(state.revision);
            else if(action==DeployIntent.RollMutators)RollDeploymentMutators(state.revision);
            else if(action==DeployIntent.SkipMutators)SkipDeploymentMutators(state.revision);
            else if(action==DeployIntent.Deploy)DeployWithPick(state.revision);
            if(server!=null&&!state.deployed&&NativeControllerPointer.Button(new Rect(width-210,height-64,180,36),networkReady?"Not ready":"Ready"))GuestReady(!networkReady);
            if(NativeControllerPointer.Button(new Rect(width-210,20,180,32),"Leave station")){CloseNetwork();menu=true;}
            if(!string.IsNullOrEmpty(networkStatus))GUI.Label(new Rect(28,height-30,width-56,26),networkStatus);
            return true;
        }
    }
}
