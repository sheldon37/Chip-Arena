using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // An expedition's own weapon pick (owner 2026-11-19: "I want Blind Pick choice with 5 options by default for expedition", and the
    // Play vs Bots Weapon pick must stop changing it). Every expedition is Blind Pick with dealt choices: each pilot is dealt N weapons
    // or classes and picks one, on PvP's Blind Pick screen (NativeBlindChoiceUI.cs, the weapon flourishes), N five until the host
    // changes it. Where: at the Deployment Station, when the host presses Deploy and before the eject, so the eject, the cradle card and
    // the drop run on unbroken (the cradle continues the console's card) and the pods carry the picks; and the party is final there (a
    // pilot cannot join a deployed station, or during a pick).
    //
    // How: the opening's own Blind Pick, exactly as a versus room deals it (BeginOpening, NativeOpeningDraft): the host deals each
    // pilot's private offers from its own random stream, every client draws only its own view and sends opening-choice, the host checks
    // and locks, a random offer at 0 s, the 2 s reveal. When it ends the host keeps each pilot's pick for this station (keptPicks) and on
    // its seat, and deploys as the button did (DeployExpedition). StartExpeditionM1's own ApplyOpeningWeapon puts the kept picks on the
    // run's seats (whatever a seat was rebuilt from in between: a guest's profile, my preferences), so the run starts with them, its
    // Restart keeps them, and the hub's load is not interrupted.
    //
    // The count: the host's expeditionChoices, carried to guests in the room options' own blindPick / blindChoices fields
    // (NativeRoomPick.Apply in CaptureRoomOptions and RulesNow), changed through the room options' Weapon choices row, stored for the
    // expedition alone (NativeRoomPick.Store in SetRoomOptions). It lasts the session (5 again at the next launch): keeping it across
    // launches would be a new preference field, left to Astra (docs/DEV_LOG.md). Presentation and wiring only: no rule, message, ticket
    // or save changes.
    public sealed partial class ConversionGame
    {
        private int expeditionChoices=NativeRoomPick.ExpeditionDefault;
        /// <summary>The pick this room's opening deals (the host's or an offline pilot's own room).</summary>
        private NativeRoomPick.Pick RoomPick=>NativeRoomPick.Of(NativeRoomPick.ModeOf(prefs.pve,prefs.expedition),prefs.blindPick,prefs.blindChoices,expeditionChoices);
        /// <summary>The expedition's choices as this viewer knows them: the host's own count, or a guest's copy of the host's rules.</summary>
        private int ExpPickChoices=>RulesGuest?NativeRoomPick.Count(GuestRules?.blindChoices??NativeRoomPick.ExpeditionDefault):NativeRoomPick.Count(expeditionChoices);

        // The station's Deploy (the console's DeployIntent.Deploy): the pick first, then the deploy with it. A refused opening (a pilot
        // not ready, an old client, a party the run refuses) says why in networkStatus and deploys nothing, as a refused Deploy did.
        private void DeployWithPick(int revision)
        {
            if(BeginOpening(()=>WithOpening(()=>{KeepExpeditionPicks();DeployExpedition(revision);}),true))return;
            DeployExpedition(revision);
        }
        // Under WithOpening: every pilot's pick kept for this station's run (by seat; a new station, or none, keeps nothing), each guest's
        // on its seat and mine in my preferences (Profile; the reveal's view set it already), and the lobby told, so the station's pads
        // and its eject show the picks.
        private ExpeditionDeploymentRoom keptPickRoom;private readonly Dictionary<int,string> keptPicks=new Dictionary<int,string>();
        private void KeepExpeditionPicks()
        {
            keptPicks.Clear();keptPickRoom=deploymentRoom;foreach(var p in openingRun.Pilots.Values)keptPicks[p.Seat.id]=p.Weapon;
            foreach(var peer in peers)if(peer.Seat!=null)ApplyOpeningWeapon(peer.Seat);
            if(!dedicated&&openingRun.Pilots.TryGetValue(1,out var me)&&me.Weapon!=prefs.weapon&&System.Array.Exists(content.weapons,w=>w.id==me.Weapon&&AllowedWeapon(w))){prefs.weapon=me.Weapon;Save();}
            LobbyBroadcast();
        }

        /// <summary>The weapon this seat picked at this station's Deploy (ApplyOpeningWeapon puts it on the run's seat), if still allowed.</summary>
        private bool KeptExpeditionPick(NativeSeat seat,out string id)
        {
            id=null;return deploymentRoom!=null&&ReferenceEquals(keptPickRoom,deploymentRoom)&&keptPicks.TryGetValue(seat.id,out id)&&OpeningWeaponAllowed(seat,id);
        }

        // The room options' rows for an expedition (in place of the co-op room's old Blind Pick All box, which wrote the Play vs Bots
        // preference): the count select, 2 up to the ordinary pool, its default marked, and the note. A guest's rows are read-only.
        // The count shown is the expedition's own (ExpPickChoices: mine, or a guest's copy of the host's), whatever mode `o` was
        // captured in.
        private string[] expCountLabels;
        private void ExpeditionPickRows(NativeRoomOptions o)
        {
            expCountLabels??=NativeBlindChoices.CountLabels(botWeaponIds.Length,NativeRoomPick.ExpeditionDefault);
            int n=ExpPickChoices;
            RuleSelect(18764,RoomOpt.BlindChoices,"Weapon choices",expCountLabels,Mathf.Clamp(n-NativeBlindChoices.Min,0,expCountLabels.Length-1),0,NativeBlindChoices.Min);
            RuleFine(18768,NativeWeaponPools.Labels[(int)NativeTilesets.Selected(DeploymentState).WeaponPool]+". "+NativeRoomPick.Note(n),0,8);
        }
    }
}
