using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // DEV PREVIEW ONLY (Claude, 2026-10-31): the Expedition Deployment Station (Runtime/NativeDeployScene.cs, the console
    // NativeDeployConsoleUI.cs) in place of the home screen, only when the player is started with --ca-deploy-preview (the
    // --ca-expedition-preview pattern; no menu button leads here). It owns a LOCAL NativeExpeditionDeployment (station "preview",
    // host the local account id, seed from the clock) and one to four stand-in party members with readiness toggles, and is the
    // only place the console's intents reach an authority: roll destinations (the three authored instances), roll or skip
    // mutators (the display table's ids), deploy. Deploy plays the whole sequence, says that no generated world exists yet and
    // returns to the menu. It touches no room, network frame, Expedition start flow or save.
    public sealed partial class ConversionGame
    {
        private static int deployPreviewFlag=-1;
        private static bool DeployPreviewRequested{get{if(deployPreviewFlag<0)deployPreviewFlag=Array.IndexOf(Environment.GetCommandLineArgs(),"--ca-deploy-preview")>=0?1:0;return deployPreviewFlag==1;}}
        private static readonly ExpeditionInstanceTheme[] DeployPreviewInstances={new ExpeditionInstanceTheme{id="exp-ward-zero",theme="quarantine"},new ExpeditionInstanceTheme{id="exp-transit",theme="rail"},new ExpeditionInstanceTheme{id="exp-foundry",theme="industrial"}};
        private static readonly string[] DeployPreviewMutators={"surge","storm","darkness","brittle","windfall","second-wind"};
        private static readonly string[] DeployStandInNames={"YOU","PILOT 2","PILOT 3","PILOT 4"},DeployStandInSpecs={"pistol","sword","rifle","daggers"},DeployStandInBodies={"#e5e7eb","#2f3644","#cfd5e2","#3a4256"},DeployStandInAccents={"#22e6ff","#ff3dcf","#ffd84a","#6dffa8"},DeployStandInPoses={"weapon","crossed","weapon","crossed"};
        private bool deployPreviewClosed,deployShown,deployAsGuest;private NativeExpeditionDeployment deployAuthority;private ExpeditionDeploymentState deployState;private string deployHost;
        private readonly NativeDeployClock deployClock=new NativeDeployClock();private readonly NativeDeployConsole.Party deployParty=new NativeDeployConsole.Party();
        private readonly DeployPilot[] deployPilots=new DeployPilot[NativeDeployPresentation.Pads];private float deployStarted,deployFov,deployNear;private int deployPartySize=3;
        private bool DeployPreviewShown=>DeployPreviewRequested&&!deployPreviewClosed&&arena==null&&menu&&!InRoom;
        private float DeployNow=>Time.unscaledTime-deployStarted;

        private void DeployPreviewStart()
        {
            string me=account?.Access?.AccountId;deployHost=ExpeditionValidation.Id(me)?me:"preview-host";
            deployAuthority=new NativeExpeditionDeployment("preview",deployHost,unchecked((uint)DateTime.UtcNow.Ticks));deployState=deployAuthority.Snapshot();
            deployClock.Reset();deployStarted=Time.unscaledTime;deployClock.Observe(deployState,0);
            for(int i=0;i<NativeDeployPresentation.Pads;i++)deployParty.Ready[i]=i==0;
        }
        // The console's intents, answered by the preview's own authority (the live room's adapter is Astra's).
        private void DeployPreviewIntent(DeployIntent intent)
        {
            if(deployAuthority==null||deployAsGuest)return;bool ready=DeployPartyReady;
            switch(intent)
            {
                case DeployIntent.RollDestinations:deployAuthority.RollInstances(deployHost,DeployPreviewInstances);break;
                case DeployIntent.RollMutators:deployAuthority.RollMutators(deployHost,DeployPreviewMutators);break;
                case DeployIntent.SkipMutators:deployAuthority.SkipMutators(deployHost);break;
                case DeployIntent.Deploy:deployAuthority.Deploy(deployHost,ready);break;
                default:return;
            }
            deployState=deployAuthority.Snapshot();deployClock.Observe(deployState,DeployNow);
        }
        private bool DeployPartyReady{get{for(int i=0;i<deployPartySize;i++)if(!deployParty.Ready[i])return false;return true;}}

        /// <summary>The station in place of the home screen's backdrop (LateFrame, MenuBackdrop); false when the preview is not on.</summary>
        private bool DeployPreviewBackdrop()
        {
            if(!DeployPreviewShown||presentation==null||!presentation.DeploySupported){DeployPreviewLeave();return false;}
            if(deployAuthority==null)DeployPreviewStart();
            if(!deployShown){RooftopLeave();deployShown=true;deployFov=cameraView.fieldOfView;deployNear=cameraView.nearClipPlane;}
            viewModelCamera.enabled=false;float now=DeployNow;deployClock.Observe(deployState,now);
            // the party: you on the host pad, stand-ins after
            deployParty.Count=deployPartySize;string bodySkin=BodySkinShown();
            for(int i=0;i<deployPartySize;i++)
            {
                deployParty.Names[i]=DeployStandInNames[i];
                deployPilots[i]=i==0?new DeployPilot{Look=new RooftopPilot{Spec=prefs.weapon,BodySkin=NativeBodySkins.Index(bodySkin),Hat=prefs.hat,Body=prefs.bodyColor,Skins=RooftopSkins(bodySkin),Accent=prefs.accentColor,HatColor=prefs.hatColor},Pose=prefs.lobbyPose,Outfit=prefs.look}
                    :new DeployPilot{Look=new RooftopPilot{Spec=DeployStandInSpecs[i],BodySkin=NativeBodySkins.StandardIndex,Hat="none",Body=DeployStandInBodies[i],Accent=DeployStandInAccents[i]},Pose=DeployStandInPoses[i],Outfit=""};
            }
            var input=new DeployInput{State=deployState,SinceInstanceRoll=deployClock.SinceInstanceRoll(now),SinceMutatorRoll=deployClock.SinceMutatorRoll(now),SinceDeploy=deployClock.SinceDeploy(now),Time=now,
                Host=!deployAsGuest,PartyReady=DeployPartyReady,Reduced=prefs.reducedEffects,Seats=deployPartySize,Ready=deployParty.Ready,Instances=DeployPreviewInstances,Mutators=DeployPreviewMutators};
            if(!presentation.RenderDeployStation(cameraView,input,deployPilots,deployShownFrames++==0?-1:Time.unscaledDeltaTime,prefs.reducedEffects)){DeployPreviewLeave();return false;}
            // the travel hand-off: no generated world exists yet, so the preview closes back to the menu
            if(deployState.deployed&&deployClock.SinceDeploy(now)>NativeDeployPresentation.TravelAt+2.5f){deployPreviewClosed=true;DeployPreviewLeave();}
            return true;
        }
        private int deployShownFrames;
        private void DeployPreviewLeave()
        {
            if(!deployShown)return;deployShown=false;deployShownFrames=0;presentation?.DeployLeave();
            if(cameraView!=null){cameraView.fieldOfView=deployFov;cameraView.nearClipPlane=deployNear;NativeLighting.EndIntro(content.Map(PreviewMapId),cameraView);}backdropMap=null;
        }

        /// <summary>The console and the DEV bar over the station (GuiFrame); false (draw the normal menu) unless the preview is on.</summary>
        private bool DeployPreviewGui(float width,float height)
        {
            if(!DeployPreviewShown||!deployShown)return false;var stage=presentation?.DeployStage;if(stage==null)return false;
            for(int i=0;i<deployPartySize;i++)deployParty.Names[i]=DeployStandInNames[i];
            var intent=NativeDeployConsole.Draw(stage,deployState,deployParty,!deployAsGuest,width,height,DeployNow);
            if(intent!=DeployIntent.None)DeployPreviewIntent(intent);
            if(stage.Deploy.Fade<.5f)DeployPreviewBar(width);
            if(Input.GetKeyDown(KeyCode.Escape)){deployPreviewClosed=true;DeployPreviewLeave();}
            return true;
        }
        // The DEV bar (top right): what this is, the party's size and readiness, whose view, restart and exit.
        private void DeployPreviewBar(float W)
        {
            var bar=new Rect(W-660,18,632,74);NativeHudChrome.Panel(bar,1,C.Warn(),10,NativeHudChrome.Corners.Diagonal,true,.94f);
            U.Text(new Rect(bar.x+14,bar.y+8,420,16),"DEPLOYMENT STATION PREVIEW · DEV ONLY · LOCAL AUTHORITY",10,C.Warn(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            if(U.Btn(new Rect(bar.xMax-84,bar.y+8,72,24),"Exit",false,true,1,11)){deployPreviewClosed=true;DeployPreviewLeave();return;}
            if(U.Btn(new Rect(bar.xMax-168,bar.y+8,78,24),"Restart",false,true,1,11)){DeployPreviewStart();return;}
            float x=bar.x+14,y=bar.y+38;U.Text(new Rect(x,y,46,26),"PARTY",10,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);x+=48;
            for(int n=1;n<=4;n++){if(U.Btn(new Rect(x,y,28,26),DeployCounts[n-1],n==deployPartySize,!deployState.deployed,1,11))deployPartySize=n;x+=32;}
            x+=10;U.Text(new Rect(x,y,50,26),"READY",10,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);x+=52;
            for(int i=0;i<deployPartySize;i++){if(U.Btn(new Rect(x,y,70,26),DeployStandInNames[i],deployParty.Ready[i],!deployState.deployed,1,10))deployParty.Ready[i]=!deployParty.Ready[i];x+=74;}
            if(U.Btn(new Rect(bar.xMax-126,y,114,26),deployAsGuest?"View: guest":"View: host",deployAsGuest,true,1,11))deployAsGuest=!deployAsGuest;
        }
        private static readonly string[] DeployCounts={"1","2","3","4"};
    }
}
