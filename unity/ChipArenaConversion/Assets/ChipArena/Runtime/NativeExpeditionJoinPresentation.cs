using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The join circles in the live scene (Claude, 2026-10-17; docs/EXPEDITION-HUB.md "Join circle presentation"): Core
    /// NativeExpeditionJoinModel from the bound view and the arena's pilots, the pads (NativeExpeditionJoinScene) through the
    /// state pieces' draw path (NativeExpeditionStatesDraw: NativeBlockLit kit boxes and one NativeSirenFx effects batch), the
    /// cue sounds on their keys with today's clips as fallbacks, and the HUD (NativeExpeditionJoinHud). Presentation only: the
    /// circles' rule, the countdown, who transfers and every collider are the authority's (ExpeditionJoinRules, Astra).
    /// </summary>
    public sealed partial class ConversionGame
    {
        private readonly NativeExpeditionJoinModel expeditionJoin=new NativeExpeditionJoinModel();
        private readonly NativeExpeditionJoinScene expeditionJoinScene=new NativeExpeditionJoinScene();
        // Only its Parts and Quads are used: the buffers the shared state-piece draw reads.
        private readonly NativeExpeditionStateScene expeditionJoinBuffers=new NativeExpeditionStateScene();
        private readonly ExpeditionLabels expeditionJoinLabels=new ExpeditionLabels();
        private readonly JoinPilots expeditionJoinPilots=new JoinPilots();
        private NativeExpeditionStatesDraw expeditionJoinDraw;private bool expeditionJoinFailed,expeditionJoinTargeted;
        /// <summary>Cue sound keys (the owner generates the clips later; until then each plays the fallback clip named beside it).</summary>
        public const string JoinCharge="expedition.join.charge",JoinTick="expedition.join.tick",JoinCancel="expedition.join.cancel",JoinDeploy="expedition.join.deploy",JoinHeld="expedition.join.held",JoinArrive="expedition.join.arrive";

        private sealed class JoinPilots:IExpeditionJoinPilots
        {
            public NativeArena Arena;
            public bool Feet(string account,int seat,out float x,out float y,out float z)
            {
                x=y=z=0;if(Arena==null||seat<=0)return false;
                foreach(var p in Arena.Pilots){if(p?.Body==null||p.Seat==null||p.Seat.id!=seat)continue;var at=p.Body.position;x=at.x;y=at.y-NativeLocomotionRules.HalfHeight;z=at.z;return true;}
                return false;
            }
        }

        /// <summary>LateUpdate (ExpeditionWorldFrame, after the state pieces and before the world pieces): the circles' model, its
        /// sounds and the pads; the world pieces learn whether the pads stand in for the stand-in ring.</summary>
        private void ExpeditionJoinFrame()
        {
            var v=LiveExpedition;
            if(!ExpeditionWorldShown||!expeditionMapReady||v==null){if(v==null)expeditionJoin.Reset();expeditionJoinScene.Clear();JoinPadsDrawn(false);return;}
            expeditionJoinPilots.Arena=arena;expeditionJoin.Build(v,expeditionStateTime,expeditionJoinPilots,expeditionJoinLabels);
            ExpeditionJoinSounds();
            if(expeditionJoinDraw==null&&!expeditionJoinFailed){expeditionJoinDraw=NativeExpeditionStatesDraw.Create();expeditionJoinFailed=expeditionJoinDraw==null;}
            bool drawn=expeditionJoinDraw!=null&&expeditionJoin.InHub;JoinPadsDrawn(drawn);if(!drawn){expeditionJoinScene.Clear();return;}
            if(!expeditionJoinTargeted){expeditionJoinScene.Target(expeditionJoinBuffers.Parts,expeditionJoinBuffers.Quads);expeditionJoinTargeted=true;}
            var c=cameraView.transform;Vector3 e=c.position,r=c.right,u=c.up;var eye=new ExpeditionEye{X=e.x,Y=e.y,Z=e.z,Rx=r.x,Ry=r.y,Rz=r.z,Ux=u.x,Uy=u.y,Uz=u.z};
            expeditionJoinScene.Build(expeditionJoin,expeditionStateTime,eye,prefs.reducedEffects,presentation.LowGraphics,FirstPersonView?expeditionJoin.ViewerRow:-1);
            expeditionJoinDraw.Draw(expeditionJoinBuffers,cameraView,presentation.UsesInstancing,Time.unscaledTime);
        }
        private void JoinPadsDrawn(bool drawn){if(expeditionWorld!=null)expeditionWorld.World.JoinPadsDrawn=drawn;expeditionLabelWorld.JoinPadsDrawn=drawn;}
        /// <summary>This frame's circle events on their keys: the charge (shield hum), each second (the countdown tick), the cancel
        /// (the reload's drop), the deploy (lightning and a dash), a held deploy (the UI error), a return (the dash).</summary>
        private void ExpeditionJoinSounds()
        {
            if(nativeAudio==null)return;
            for(int i=0;i<expeditionJoin.CueCount;i++)
            {
                var q=expeditionJoin.Cues[i];var at=new Vector3(q.X,q.Y+1.2f,q.Z);bool mine=q.Ring>=0&&q.Ring==expeditionJoin.ViewerRing&&expeditionJoin.Viewer==ExpeditionJoinViewer.Deploying;
                switch(q.Kind)
                {
                    case ExpeditionJoinCueKind.Charge:nativeAudio.Cue(JoinCharge,"shield",at,.55f,1.15f,mine:mine);break;
                    case ExpeditionJoinCueKind.Tick:nativeAudio.Cue(JoinTick,"countdown",at,mine?.6f:.4f,mine:mine);break;
                    case ExpeditionJoinCueKind.Cancel:nativeAudio.Cue(JoinCancel,"reload-start",at,.45f,.8f);break;
                    case ExpeditionJoinCueKind.Deploy:nativeAudio.Cue(JoinDeploy,"lightning",at,.7f,1.1f);nativeAudio.Cue(JoinDeploy+".whoosh","dash",at,.55f,.9f);break;
                    case ExpeditionJoinCueKind.Held:nativeAudio.Cue(JoinHeld,"ui",at,.35f,.7f);break;
                    case ExpeditionJoinCueKind.Arrive:nativeAudio.Cue(JoinArrive,"dash",at,.5f,1.2f);break;
                }
            }
        }
        /// <summary>The circles' HUD (ExpeditionLiveHud): the party list on the left under the SAFE chip, my circle's panel over the
        /// health box, the notes under the J tag, and my deploy flash over all of it.</summary>
        private void ExpeditionJoinHud(float width,float height)
        {
            if(Event.current.type!=EventType.Repaint||!ExpeditionWorldShown)return;float t=Time.unscaledTime;bool reduced=prefs.reducedEffects;
            NativeExpeditionJoinHud.Party(expeditionJoin,24,170,t,reduced);
            NativeExpeditionJoinHud.Circle(expeditionJoin,width,height*.66f,t,reduced);
            NativeExpeditionJoinHud.Toasts(expeditionJoin,width,64);
            NativeExpeditionJoinHud.Flash(expeditionJoin,width,height,reduced);
        }
    }
}
