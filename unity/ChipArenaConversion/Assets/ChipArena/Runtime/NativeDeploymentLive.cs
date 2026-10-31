using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private readonly NativeDeployClock liveDeployClock=new NativeDeployClock();
        private readonly NativeDeployConsole.Party liveDeployParty=new NativeDeployConsole.Party();
        private readonly DeployPilot[] liveDeployPilots=new DeployPilot[NativeDeployPresentation.Pads];
        private bool liveDeployShown;private float liveDeployFov,liveDeployNear;
        private bool LiveDeployment=>arena==null&&DeploymentState!=null;
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
            liveDeployParty.Count=Math.Min(seats.Length,liveDeployPilots.Length);
            for(int i=0;i<liveDeployParty.Count;i++)
            {
                var s=seats[i];liveDeployParty.Names[i]=s.name;liveDeployParty.Ready[i]=s.ready;
                liveDeployPilots[i]=new DeployPilot{Look=new RooftopPilot{Spec=s.spec,BodySkin=NativeBodySkins.Of(s.weaponSkins),Hat=s.hat,Body=s.bodyColor,Accent=s.accentColor,HatColor=s.hatColor,Skins=s.weaponSkins},Pose=s.lobbyPose,Outfit=s.look};
            }
            viewModelCamera.enabled=false;
            var input=new DeployInput{State=state,SinceInstanceRoll=liveDeployClock.SinceInstanceRoll(now),SinceMutatorRoll=liveDeployClock.SinceMutatorRoll(now),SinceDeploy=liveDeployClock.SinceDeploy(now),Time=now,
                Host=server==null&&!PromotionBusy,PartyReady=server==null&&GuestsReady,Reduced=prefs.reducedEffects,Seats=liveDeployParty.Count,Ready=liveDeployParty.Ready,Instances=DeployPreviewInstances,Mutators=Array.Empty<string>()};
            presentation.RenderDeployStation(cameraView,input,liveDeployPilots,Time.unscaledDeltaTime,prefs.reducedEffects);
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
            var action=NativeDeployConsole.Draw(presentation?.DeployStage,state,liveDeployParty,server==null&&!PromotionBusy,width,height,Time.unscaledTime);
            if(action==DeployIntent.RollDestinations)RollDeploymentInstances(state.revision);
            else if(action==DeployIntent.SkipMutators)SkipDeploymentMutators(state.revision);
            else if(action==DeployIntent.Deploy)DeployExpedition(state.revision);
            if(server!=null&&!state.deployed&&GUI.Button(new Rect(width-210,height-64,180,36),networkReady?"Not ready":"Ready"))GuestReady(!networkReady);
            if(GUI.Button(new Rect(width-210,20,180,32),"Leave station")){CloseNetwork();menu=true;}
            if(!string.IsNullOrEmpty(networkStatus))GUI.Label(new Rect(28,height-30,width-56,26),networkStatus);
            return true;
        }
    }
}
