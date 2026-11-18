using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The cradle card's wiring (Core NativeCradleCard, Runtime NativeCradleHud; docs/EXPEDITION-OVERHAUL.md R8 and W3). A pod run's
    /// first load (the generated run's hub, after the station's eject) shows the inside of my pod in place of the stage loading
    /// screen: the station's console draws it under its fade as the eject ends (LiveCradle: the station's party, all loading),
    /// then the loading screen carries it on (CradleGui: the party loading model's lamps and my load), from the host's own
    /// cover or a guest's first frame alike. On a pod run every peer holds on the card until the hub's countdown starts
    /// (PodCradleHold, read by StageSignals as a game hold), so an early guest never sees pilots standing on bare slots; then the
    /// card lifts away. Presentation only: no simulation, authority or network state is read beyond the replicated phase and view.
    /// </summary>
    public sealed partial class ConversionGame
    {
        private readonly NativeCradleCard liveCradle=new NativeCradleCard(),loadCradle=new NativeCradleCard();
        public const string CradleHolding="HOLDING FOR THE SQUAD";
        /// <summary>The station's card under its fade, before the load starts: the station's party, all loading, me marked.</summary>
        private NativeCradleCard LiveCradle()
        {
            var stage=presentation?.DeployStage;liveCradle.FromParty(liveDeployParty.Names,liveDeployParty.Count,stage?.Mine??0);CradleSpots(liveCradle);return liveCradle;
        }
        // Each member's landing spot on the card's map: the generated hub's slots in seat order (the pods' spots).
        private void CradleSpots(NativeCradleCard card)
        {
            var slots=deploymentBundle?.World?.Area?.slots;
            for(int i=0;i<card.Count;i++)card.Spots[i]=slots!=null&&i<slots.Length&&slots[i]!=null?new System.Numerics.Vector2(slots[i].x,slots[i].z):new System.Numerics.Vector2(float.NaN,float.NaN);
        }
        /// <summary>This load is a pod run's first: the generated run's hub (or the arena not in place yet) on an Expedition start.</summary>
        private bool PodCradle=>deploymentBundle?.World?.Map!=null&&stageLoad.Kind==StageLoadKind.Expedition&&(arena==null||arena.MapId==deploymentBundle.World.Map.id);
        /// <summary>The hold: on a pod run the card stays until the hub's countdown (the hub waits in Lobby for the whole party).</summary>
        internal bool PodCradleHold=>PodCradle&&arena!=null&&match!=null&&match.Phase==NativePhase.Lobby;
        /// <summary>StageLoadingGui on a pod run's first load: the card, at full strength while the screen covers or holds (it continues
        /// the console's), fading and lifting away on the reveal.</summary>
        private void CradleGui(float shown)
        {
            var roster=PartyLoadingRoster();
            if(roster!=null)loadCradle.FromRoster(roster,shown);else{loadCradle.FromParty(null,0,0);loadCradle.Load=shown;}
            CradleSpots(loadCradle);
            bool reveal=stageLoad.Phase==StageLoadPhase.Reveal;float r=reveal?stageLoad.RevealProgress:0;
            string step=PodCradleHold&&stageReadiness.Ready?CradleHolding:stagePlan.Label;
            NativeCradleHud.Draw(loadCradle,DeploymentState,stageView.Title,step,U.W,U.H,1-r*r,r,Time.unscaledTime,prefs.reducedEffects);
        }
    }
}
