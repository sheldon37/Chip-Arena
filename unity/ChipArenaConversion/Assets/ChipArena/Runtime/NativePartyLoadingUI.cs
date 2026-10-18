using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The party panel's wiring on the stage loading screen (Core NativePartyLoadModel; owner 2026-10-18: "lets add a loading
    /// indicator for players (When starting expeditions, so we can see where each player is on loading screen)"). Shown for an
    /// Expedition start or area load only, from the live view every client holds (LiveExpedition: the host's own projection, a
    /// guest's received one) and this client's own load (the screen's shown progress and its readiness). PvP and PvE rooms keep
    /// the pilots' count: their stage gate sends only Ready of Required, no member's state. Presentation only: it reads the view
    /// and the loading screen, and sends, decides and writes nothing.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private readonly NativePartyLoadModel partyLoad=new NativePartyLoadModel();
        private readonly ExpeditionLabels partyLoadLabels=new ExpeditionLabels();

        /// <summary>This client's own load progress, for Astra's relay (the 2026-10-18 contract in docs/DEV_LOG.md): 1 once the
        /// stage's presentation is ready, the loading screen's shown progress (0..1) while it is up, -1 when no load is shown.</summary>
        internal float StageLoadProgress=>stageReadiness.Ready?1:stageLoad.Visible?stageLoad.Shown:-1;

        /// <summary>The loading screen's party panel this frame (OnGUI repaint), or null outside an Expedition load.</summary>
        private NativePartyLoadModel PartyLoadingRoster()
        {
            var v=LiveExpedition;var k=stageLoad.Kind;
            if(v==null||k!=StageLoadKind.Expedition&&k!=StageLoadKind.Area){partyLoad.Clear();return null;}
            partyLoad.Build(v,stageLoad.Shown,stageReadiness.Ready,stageLoad.Serial,Time.unscaledTime,partyLoadLabels);
            return partyLoad.Count>0?partyLoad:null;
        }
    }
}
