using System;
using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Siren / Ward Zero pick's follow-through (presentation only). The rules close the pick the moment it is made, so the
    // stage keeps a snapshot of the offers and plays the PvP draft's install on it (NativeInstallShot: the others break, the card
    // flies and bursts into the cartridge, the pilot installs it and rises into the power) until NativeInstallShot.ShortEnd (the
    // old cap, 1.95 s), a bloom of the chip's light from the device opening back onto the live view. Play resumes with the pick, so
    // any key or click, being attacked, or leaving the fight ends it early (with the bloom's .12 s fade only).
    public sealed partial class ConversionGame
    {
        private readonly List<ContentChip> pveFollowChips=new List<ContentChip>();
        private int pveFollowIndex=-1,pveFollowSeat=-1,pveFollowOwned;private float pveFollowAt,pveFollowOpened;private string pveFollowTier,pveFollowSource;
        private void RecordPvePick(List<ContentChip> chips,int index,NativePvePick pick)
        {
            pveFollowChips.Clear();pveFollowChips.AddRange(chips);pveFollowIndex=index;pveFollowAt=Time.unscaledTime;pveFollowOpened=pveOpenAt;
            pveFollowSeat=arena.Local.Seat.id;pveFollowTier=pick.Tier;pveFollowSource=pick.Source;pveFollowOwned=arena.Local.Seat.chips.TryGetValue(chips[index].id,out int n)?n:0;
        }
        private void EndPveFollow(){if(pveFollowIndex>=0&&pveFollowIndex<pveFollowChips.Count)InstallBloomStart(RarityColor(pveFollowChips[pveFollowIndex].rarity),PveFollowAge);pveFollowIndex=-1;pveFollowChips.Clear();}
        private float PveFollowAge=>Time.unscaledTime-pveFollowAt;
        private bool PveFollowing
        {
            get
            {
                if(pveFollowIndex<0)return false;
                if(pveFollowIndex>=pveFollowChips.Count||PveFollowAge>NativeInstallShot.ShortEnd||menu||arena==null||arena.Pve==null||arena.Pve.Holding||arena.Local.Seat.id!=pveFollowSeat||arena.Local.Hp<=0||ReplayActive||match.Phase!=NativePhase.Fight){EndPveFollow();return false;}
                return true;
            }
        }
        // The follow-through's heading: "Installing" until the clack, then "Upgrade secured" typed in.
        private static string FollowTitle(float a){const string done="Upgrade secured";int typed=NativeInstallShot.Secured(a,done.Length);return typed<0?"Installing":done.Substring(0,Math.Max(1,typed));}
        // The short cut's bloom (NativeInstallShot.Bloom): from the device's screen point, the chip's light grows over the last .15 s; when the
        // follow-through ends (at its cap, a key, an attack) it fades over the live view in .12 s.
        private float installBloomAt=-10,installBloomFrom;private Color installBloomColor;
        private void InstallBloomStart(Color c,float age){installBloomAt=Time.unscaledTime;installBloomColor=c;NativeInstallShot.Bloom(age,-1,prefs.reducedEffects,out installBloomFrom,out _);}
        private void InstallBloom(Color c,float a,float W,float H)
        {
            NativeInstallShot.Bloom(a,-1,prefs.reducedEffects,out float reach,out float alpha);if(alpha<=0)return;var at=draftDevice2d!=Vector2.zero?draftDevice2d:new Vector2(W*.5f,H*.6f);
            float r=Mathf.Lerp(40,Mathf.Max(W,H)*.85f,reach);U.RadialStops(new Rect(at.x-r,at.y-r,r*2,r*2),U.A(c,alpha),BloomAt,BloomAlpha);
        }
        private static readonly float[] BloomAt={0,.35f,.75f,1},BloomAlpha={1,.8f,.25f,0};
        /// <summary>The bloom's fade over the live view after the follow-through (gameplay GUI, every frame; nothing once it has gone).</summary>
        private void InstallBloomOut()
        {
            float after=Time.unscaledTime-installBloomAt;if(after<0||after>.2f)return;NativeInstallShot.Bloom(NativeInstallShot.ShortEnd,after,prefs.reducedEffects,out _,out float alpha);if(alpha<=0)return;
            float W=U.W,H=U.H,reach=Mathf.Max(.3f,installBloomFrom);var at=draftDevice2d!=Vector2.zero?draftDevice2d:new Vector2(W*.5f,H*.6f);float r=Mathf.Max(W,H)*.85f*reach;
            U.RadialStops(new Rect(at.x-r,at.y-r,r*2,r*2),U.A(installBloomColor,alpha*reach),BloomAt,BloomAlpha);
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
            float a=Math.Abs(presentation.DraftAge-age)<.3f?presentation.DraftAge:age;
            DraftShades(pveTray,W,H);DraftHeader(source,rainbow?Color.white:NativeChipCard.Rarity(pveFollowTier),FollowTitle(a),"",W,H,out _);
            var tray=pveTray;if(tray!=null&&tray.Cards.Length==pveFollowChips.Count)PickedTray(tray,pveFollowChips,pveFollowIndex,pveFollowOwned,a);InstallFlash(a);
            Nameplate(arena.Local.Seat,NativeInstallShot.Installed(a)?"Installed":"Installing",28,H-22-66);
            if(pveFollowIndex>=0&&pveFollowIndex<pveFollowChips.Count)InstallBloom(RarityColor(pveFollowChips[pveFollowIndex].rarity),a,W,H);
        }
    }
}
