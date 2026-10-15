using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using P=ChipArena.UnityConversion.NativeExpeditionPresentation;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // An Expedition's chip pick on the Boss Test's personal pick screen (Claude, 2026-10-15; owner: "We should also utilize the same chip
    // selection screen (That we utilize in the Boss Test) instead of selecting on the J menu."; docs/EXPEDITION-PRESENTATION.md "Chip
    // picks" and "Chip screen authority contract"). Core/NativeExpeditionPick.cs decides what shows and when; this draws it with
    // PveChipScreen's pieces (the 3D stage through PvePickScreen / DraftFrame, the tray and its entrances, keys 1-9, the header, meta,
    // footer and alarm, the follow-through) and sends Claim / OpenChoice / CloseChoice / Select through ExpeditionLiveSubmit (host: the
    // session's commands; guest: the command channel). The host holds a choosing pilot (D18) and publishes members[].choosing, which
    // drives every choosing pilot's wrist look and SELECTING CHIP tag (ArenaPresentation.ExpeditionChoosing); a successful Select emits
    // the Install event the follow-through's install cue waits for.
    public sealed partial class ConversionGame
    {
        private readonly ExpeditionPickFlow expeditionPick=new ExpeditionPickFlow();
        // The shown reward's cards (cached per run and reward), its tone, the wrist look's stand-in pick and its hook.
        private readonly List<ContentChip> expeditionPickChips=new List<ContentChip>();private string expeditionPickKey,expeditionPickShown,expeditionPickTone="common";
        private NativePvePick expeditionWrist;private Func<int,NativePvePick> expeditionChoosing;
        // Who is choosing (the view's public flag, per seat) and the wrist look's stand-in pick for each open (no offers: cyan cards).
        private readonly ExpeditionChoosers expeditionChoosers=new ExpeditionChoosers();private readonly Dictionary<int,NativePvePick> expeditionChooserPicks=new Dictionary<int,NativePvePick>();
        // The CHIPS tag's pulse: the waiting count last seen and when it grew.
        private int expeditionChipsSeen;private float expeditionChipsAt=-10;
        // The follow-through reads "Installing" until the view confirms the pick, for at least the shatter's first beat (a host-local
        // confirmation lands within a frame or two and would otherwise flicker the heading).
        private const float ExpeditionFollowSettle=.35f;

        /// <summary>The Expedition's tray is up: it takes the screen in place of the arena (PvePickScreen) and owns the pointer and keys
        /// (the cursor is free for the cards and 1-9 pick), as the Boss Test's open pick does. The hold itself is the host's (D18, the
        /// view's choosing flag after OpenChoice): this only keeps local play input off the screen.</summary>
        private bool ExpeditionPickOpen=>arena!=null&&!ExpeditionScreenOpen&&expeditionPick.Choosing(LiveExpedition)!=null;
        /// <summary>The screen could show now (an open's Claim is sent only then).</summary>
        private bool ExpeditionPickCanShow=>!menu&&arena?.Local!=null&&!ExpeditionScreenOpen&&arena.Local.Hp>0&&arena.Pve?.Holding!=true&&!ReplayActive&&!IntroActive&&match?.Phase==NativePhase.Fight;

        /// <summary>Update (ExpeditionLiveKeys): follows the bound view, sends the flow's intent (Claim, OpenChoice or CloseChoice), shows a
        /// refusal's notice, and follows who is choosing for the wrist look's hook.</summary>
        private void ExpeditionPickStep()
        {
            ExpeditionLiveSync();var v=expeditionLiveView;
            if(presentation!=null)presentation.ExpeditionChoosing=v!=null&&arena!=null?expeditionChoosing??=ExpeditionWrist:null;
            expeditionChoosers.Step(v,arena?.Time??0);if(expeditionChoosers.Count==0)expeditionChooserPicks.Clear();
            if(v==null){expeditionPick.Step(null,null,null,false);return;}
            var controls=LiveExpeditionControls();var act=expeditionPick.Step(v,controls.State,controls.Last,ExpeditionPickCanShow);
            if(act.Send.intent!=ExpeditionIntent.None)ExpeditionLiveSubmit(act.Send);
            if(act.Notice!=null)Notice(act.Notice);
            ExpeditionPickCards(v,expeditionPick.Choosing(v));
        }
        /// <summary>C: opens the screen on the waiting rewards or closes it (Choose later); from the J screen it closes J and opens it.</summary>
        private void ExpeditionPickKey(bool fromJ)
        {
            var v=LiveExpedition;if(v==null||arena==null)return;
            if(fromJ)expeditionPick.Show(v);else expeditionPick.Toggle(v);
        }
        // The shown reward's cards, read once per reward (the follow-through keeps its own snapshot), for the tray and DraftFrame.
        private void ExpeditionPickCards(ExpeditionView v,ExpeditionViewReward reward)
        {
            if(reward==null)return;string key=v.run+"|"+reward.id;if(key==expeditionPickKey)return;
            expeditionPickKey=key;expeditionPickTone=NativeExpeditionPick.Tone(reward);expeditionPickChips.Clear();
            foreach(var id in reward.offers){var c=arena?.PveChip(id);if(c!=null)expeditionPickChips.Add(c);}
            expeditionWrist=new NativePvePick{Id=NativeExpeditionPick.Id(reward.id),Tier=expeditionPickTone,Source=reward.source,Offers=new List<string>(reward.offers),At=arena?.Time??0};
        }
        // ArenaPresentation.ExpeditionChoosing: pilot `id`'s open pick while the view shows it choosing (the host's public flag), as the
        // Boss Test reads each row's replicated open pick. Mine carries the shown reward's offers and tone (my own view), so a new reward
        // re-deals its cards; anyone else's has no offers, so its cards draw cyan.
        private NativePvePick ExpeditionWrist(int id)
        {
            if(!expeditionChoosers.At(id,out var e))return null;
            if(e.Mine){var r=expeditionPick.Choosing(LiveExpedition);if(r!=null&&expeditionWrist!=null&&expeditionWrist.Id==NativeExpeditionPick.Id(r.id))return expeditionWrist;}
            if(!expeditionChooserPicks.TryGetValue(e.Pick,out var pick))expeditionChooserPicks[e.Pick]=pick=new NativePvePick{Id=e.Pick,Source="expedition",At=(float)e.Since};
            return pick;
        }
        private static int ExpeditionHeld(ExpeditionView v,string chip){foreach(var c in v.mine.chips)if(c.id==chip)return c.count;return 0;}

        /// <summary>PveChipScreen's Expedition branch: the shown reward's tray; a pick sends Select and starts the follow-through.</summary>
        private void ExpeditionChipScreen()
        {
            var v=LiveExpedition;var reward=expeditionPick.Choosing(v);if(reward==null||arena?.Local==null)return;ExpeditionPickCards(v,reward);
            // A new reward on the tray restarts its entrance and focus at its first draw, as PveChipScreen does for a new pick (not while
            // the last pick's follow-through still flies its card from its highlight).
            if(expeditionPickShown!=expeditionPickKey){expeditionPickShown=expeditionPickKey;pveOpenAt=Time.unscaledTime;draftFocus=-1;ResetHolo();}
            var commands=LiveExpeditionCommands();string blocked=commands==null?"Expedition is not bound.":commands.Blocked(new ExpeditionCommand(ExpeditionIntent.Select,reward.id));
            var chips=expeditionPickChips;
            int pick=ExpeditionPickTray(reward,chips,NativeExpeditionPick.MoreWaiting(v,reward.id),id=>ExpeditionHeld(v,id),blocked,expeditionPick.Refusal,PveUnderAttack(),Time.unscaledTime-pveOpenAt,arena.Local.Seat);
            if(pick==-2){expeditionPick.Later();return;}
            if(pick<0)return;var cmd=expeditionPick.Choose(v,chips[pick].id);if(cmd.intent==ExpeditionIntent.None)return;
            RecordExpeditionPick(chips,pick,reward,ExpeditionHeld(v,chips[pick].id));ExpeditionLiveSubmit(cmd);
        }
        /// <summary>
        /// The Expedition pick over the 3D stage in PveChipScreen's layout: the shades, the header (the reward's source and kind; no tier),
        /// the meta (vulnerable while choosing, N more waiting), the tray (1-9, arrows and Enter, a click), the nameplate, the footer (Choose
        /// later), a status line (why picking is unavailable, or the last pick's refusal) and the under-attack alarm. Returns the card picked,
        /// -2 for Choose later, or -1.
        /// </summary>
        private int ExpeditionPickTray(ExpeditionViewReward reward,List<ContentChip> chips,int more,Func<string,int> held,string blocked,string refusal,bool attacked,float elapsed,NativeSeat seat)
        {
            float W=U.W,H=U.H;bool enabled=blocked==null;
            DraftShades(pveTray,W,H);DraftHeader(NativeExpeditionPick.Eyebrow(reward),NativeChipCard.Rarity(NativeExpeditionPick.Tone(reward)),"Make your next move","Choose one Chip. This choice is yours alone.",W,H,out float top);
            PickMeta(W,more);
            pveTray=LayoutTray(chips,top,W,H);LowerTray(pveTray,top,H-142);draftCards=pveTray.Cards;draftTrayLine=pveTray.Top/H;draftTrayBottom=pveTray.Bottom/H;
            int choice=DrawTray(pveTray,chips,i=>held(chips[i].id),elapsed,enabled,-1,i=>false,true);int key=DraftKeys(chips.Count,enabled,true);if(key>=0)choice=key;
            if(choice>=0&&enabled)return choice;
            Nameplate(seat,"Your pick",28,H-22-66);
            bool later=PickFooter(W,H,chips.Count);
            if(blocked!=null)PickStatus(W,H,"Unavailable · "+blocked,C.Warn());else if(refusal!=null)PickStatus(W,H,refusal,C.Danger());
            PickAlarm(W,H,attacked);
            return later?-2:-1;
        }

        // ---- The follow-through (NativeDraftFollow.cs) for an Expedition pick ----
        /// <summary>It ends when its pick is neither in flight nor confirmed (refused or unconfirmed: the tray returns with the caption).</summary>
        private bool ExpeditionFollowEnded=>pveFollowReward!=null&&expeditionPick.Sent!=pveFollowReward&&expeditionPick.Installed!=pveFollowReward;
        private string ExpeditionFollowTitle(float age)=>expeditionPick.Installed==pveFollowReward&&age>=ExpeditionFollowSettle?"Upgrade secured":"Installing";
        /// <summary>While the pick waits for the authority past the first beat: its tracked caption over the footer's place.</summary>
        private void ExpeditionFollowStatus(float W,float H,float age)
        {
            if(expeditionPick.Sent!=pveFollowReward||age<ExpeditionFollowSettle)return;
            var state=expeditionIntents.State(new ExpeditionCommand(ExpeditionIntent.Select,pveFollowReward),expeditionLiveView,Time.unscaledTime);
            PickStatus(W,H,P.Feedback(state,out _)??"Sent · waiting for the authority",C.CyanSoft());
        }

        /// <summary>The in-scene CHIPS tag (the Boss Test's PveChipsTag): the waiting count and the C key, pulsing for 6 s after it grows;
        /// hidden while the screen is wanted open.</summary>
        private void ExpeditionChipsTag(ExpeditionView v)
        {
            int n=NativeExpeditionPick.Waiting(v);if(n!=expeditionChipsSeen){if(n>expeditionChipsSeen)expeditionChipsAt=Time.unscaledTime;expeditionChipsSeen=n;}
            if(n>0&&!expeditionPick.Open)PveChipsTag(U.W,U.H,n,Time.unscaledTime-expeditionChipsAt);
        }
    }
}
