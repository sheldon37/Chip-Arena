using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The Siren / Ward Zero pick's follow-through (presentation only). The rules close the pick the moment it is made, so the
    // stage keeps a snapshot of the offers and plays the PvP draft's pick on it (the others shatter, the card flies to the
    // palm, the pilot plants the chip into the other wrist with its pulse) until NativeDraftPlant.ReturnEnd. Play resumes with
    // the pick, so any key or click, being attacked, or leaving the fight ends it early.
    public sealed partial class ConversionGame
    {
        private readonly List<ContentChip> pveFollowChips=new List<ContentChip>();
        private int pveFollowIndex=-1,pveFollowSeat=-1,pveFollowOwned;private float pveFollowAt,pveFollowOpened;private string pveFollowTier,pveFollowSource;
        private void RecordPvePick(List<ContentChip> chips,int index,NativePvePick pick)
        {
            pveFollowChips.Clear();pveFollowChips.AddRange(chips);pveFollowIndex=index;pveFollowAt=Time.unscaledTime;pveFollowOpened=pveOpenAt;
            pveFollowSeat=arena.Local.Seat.id;pveFollowTier=pick.Tier;pveFollowSource=pick.Source;pveFollowOwned=arena.Local.Seat.chips.TryGetValue(chips[index].id,out int n)?n:0;
        }
        private void EndPveFollow(){pveFollowIndex=-1;pveFollowChips.Clear();}
        private float PveFollowAge=>Time.unscaledTime-pveFollowAt;
        private bool PveFollowing
        {
            get
            {
                if(pveFollowIndex<0)return false;
                if(pveFollowIndex>=pveFollowChips.Count||PveFollowAge>NativeDraftPlant.ReturnEnd||menu||arena==null||arena.Pve==null||arena.Pve.Holding||arena.Local.Seat.id!=pveFollowSeat||arena.Local.Hp<=0||ReplayActive||match.Phase!=NativePhase.Fight){EndPveFollow();return false;}
                return true;
            }
        }
        // NativeAudio plays my held Install cue at the follow-through's plant (NativeInstallCue).
        private void PveFollowAudio(){bool on=PveFollowing;nativeAudio.FollowChip=on?pveFollowChips[pveFollowIndex].id:null;nativeAudio.FollowStarted=pveFollowAt;nativeAudio.FollowAge=on?PveFollowAge:0;}
        // The overlay over the follow-through: the header, the tray in its picked state and the nameplate; nothing to click.
        private void PveFollowScreen(float W,float H)
        {
            float age=PveFollowAge;if(Event.current.type==EventType.Repaint)draftHover=-1;
            if(age>.12f&&Input.anyKeyDown||PveUnderAttack()){EndPveFollow();return;}
            bool rainbow=pveFollowTier=="rainbow";
            string source=rainbow?"RAINBOW · 8 MAX-RARITY CHOICES":pveFollowSource=="start"?"BEGINNING CHIP":(pveFollowSource=="wave"?"WAVE REWARD · ":"DISPENSED · ")+TierName(pveFollowTier).ToUpperInvariant();
            DraftShades(pveTray?.Rows??1,W,H);DraftHeader(source,rainbow?Color.white:NativeChipCard.Rarity(pveFollowTier),"Upgrade secured","",W,H,out _);
            var tray=pveTray;if(tray!=null&&tray.Cards.Length==pveFollowChips.Count)PickedTray(tray,pveFollowChips,pveFollowIndex,pveFollowOwned,age);
            Nameplate(arena.Local.Seat,"Installing",28,H-22-66);
        }
    }
}
