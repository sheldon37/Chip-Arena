using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using P=ChipArena.UnityConversion.NativeExpeditionPresentation;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // An Expedition's chip pick: the Boss Test's chip tray as an overlay over the live view (Claude, 2026-10-15; owner: "We should also
    // utilize the same chip selection screen (That we utilize in the Boss Test) instead of selecting on the J menu."; then: "Can we change
    // the chip selection to just pressing C and it'll be us in a "Chip Selection" animation? I purposely wanted to make people vulnerable
    // when doing this." / "(Like we had for ward zero, but can keep the open view. I just want players to stay in place while doing
    // this.)" / "Still make them put their chip in their wrist after selected, then they are free to move."; docs/EXPEDITION-PRESENTATION.md
    // "Chip picks"). Core/NativeExpeditionPick.cs decides what shows and when; this draws PveChipScreen's cards (DrawTray: the entrances,
    // the holo highlight, the number badges, the hover cue), keys 1-9 / arrows / Enter (DraftKeys) and a follow-through on the same pieces
    // (Shatter, FlyToPalm), docked at the bottom centre between the health box and the cooldowns so the crosshair and the world stay in
    // view. No 3D draft stage and the arena stays drawn. It sends Claim / OpenChoice / CloseChoice / Select through ExpeditionLiveSubmit
    // (host: the session's commands; guest: the command channel). The authority holds the chooser in place from OpenChoice (PR #237:
    // movement and actions cancelled, camera rotation kept, damage and the clocks live) and, after the final pick, until its Install plus
    // NativeDraftPlant.ReturnEnd. Meanwhile members[].choosing drives every choosing pilot's wrist-hologram look and SELECTING CHIP tag
    // (ArenaPresentation.ExpeditionChoosing), mine included in first person, and each pick plays the wrist plant (Core/NativeChipPlant.cs:
    // the chosen card leaves the hologram as the chip and goes into the cuff, the install cue at the seat; ArenaPresentation.ExpeditionPlant:
    // mine from my pick, anyone else's from the authority's Install event): the tray's picked card flies into that hologram as the hand
    // takes it. Client routing while the tray is up (the tray's own keys; the hold is the authority's): digits 1-9 pick instead of
    // starting abilities; holding Alt frees the cursor to click a card; C or Escape closes it (CloseChoice).
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
        // The tray's layout this frame, and the follow-through's snapshot of the picked tray.
        private Tray expeditionTray,expeditionFollowTray;private readonly List<ContentChip> expeditionFollowChips=new List<ContentChip>();
        private int expeditionFollowIndex=-1,expeditionFollowOwned;private float expeditionFollowAt;private string expeditionFollowReward,expeditionFollowEyebrow,expeditionFollowTone;
        // The follow-through: the others shatter and the chosen card flies into my wrist hologram (PickedTray, FlyToPalm toward
        // ChipSelectFocus), then the confirmation holds while the plant runs; it spans the plant (NativeChipPlant.Span, the authority's
        // final hold). It reads "Installing" until the view confirms the pick, for at least the first beat (a host-local confirmation lands
        // within a frame or two and would otherwise flicker the heading).
        private const float ExpeditionFollowSpan=NativeChipPlant.Span,ExpeditionFollowSettle=.35f;
        // My wrist plant (ArenaPresentation.ExpeditionPlant): when I picked (unscaled), the reward and chip, the card it took on my wrist
        // hologram and a key per pick. It runs its whole span whatever the tray does (the tray closing never drops the look), and stops
        // only when that pick is refused or fails.
        private float expeditionPlantAt=-100;private string expeditionPlantReward,expeditionPlantChip;private int expeditionPlantKey,expeditionPlantChosen=-1;private Func<int,float,ChipPlantCue> expeditionPlant;

        /// <summary>The overlay could show now (an open's Claim and OpenChoice are sent only then): a live fight view, not a menu, the J
        /// screen, an intro, a hold, a cutscene or a replay, and my pilot standing.</summary>
        private bool ExpeditionPickCanShow=>!menu&&arena?.Local!=null&&!ExpeditionScreenOpen&&arena.Local.Hp>0&&arena.Pve?.Holding!=true&&!ReplayActive&&!IntroActive&&!cinematic.Directing&&match?.Phase==NativePhase.Fight;
        /// <summary>The tray can be up over the live view (not during a follow-through): what ExpeditionPickGui draws.</summary>
        private bool ExpeditionPickOverlay=>arena!=null&&ExpeditionPickCanShow&&!ExpeditionFollowing&&expeditionPick.Choosing(LiveExpedition)!=null;
        /// <summary>The tray is on screen (drawn in the last frames; a screen that skips the HUD, such as the Siren reveal or the DEV
        /// screenshot mode, leaves it off): only then do the digits pick, Alt free the cursor and Escape close it.</summary>
        private bool ExpeditionPickShown=>ExpeditionPickOverlay&&Time.frameCount-expeditionPickDrawn<=2;private int expeditionPickDrawn=-10;
        /// <summary>Alt is held over the tray: the cursor is free to click a card; mouse look, attack and block wait until it is released.</summary>
        private bool ExpeditionPickPointer=>ExpeditionPickShown&&(Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt));

        /// <summary>Update (ExpeditionLiveKeys): follows the bound view, sends the flow's intent (Claim, OpenChoice or CloseChoice), shows a
        /// refusal's notice, and follows who is choosing for the wrist look's hook.</summary>
        private void ExpeditionPickStep()
        {
            ExpeditionLiveSync();var v=expeditionLiveView;
            if(presentation!=null){presentation.ExpeditionChoosing=v!=null&&arena!=null?expeditionChoosing??=ExpeditionWrist:null;presentation.ExpeditionPlant=v!=null&&arena!=null?expeditionPlant??=ExpeditionPlantCue:null;presentation.ChipTrayShown=v!=null&&arena!=null&&(ExpeditionPickShown||ExpeditionFollowing);}
            expeditionChoosers.Step(v,arena?.Time??0);if(expeditionChoosers.Count==0)expeditionChooserPicks.Clear();
            if(v==null){expeditionPick.Step(null,null,null,false);expeditionFollowIndex=-1;return;}
            var controls=LiveExpeditionControls();var act=expeditionPick.Step(v,controls.State,controls.Last,ExpeditionPickCanShow);
            if(act.Send.intent!=ExpeditionIntent.None)ExpeditionLiveSubmit(act.Send);
            if(act.Notice!=null)Notice(act.Notice);
            ExpeditionPickCards(v,expeditionPick.Choosing(v));
        }
        /// <summary>
        /// Update, after the play input is read (the tray's own keys only: the hold itself is the authority's, PR #237, which cancels the
        /// chooser's movement and actions and keeps its camera rotation). While the tray is up the digit keys pick a chip, so the abilities
        /// they would start (1 / 2 / the 1-5 kit keys) are dropped; while Alt frees the pointer, the mouse buttons click cards instead of
        /// attacking or blocking.
        /// </summary>
        private void ExpeditionPickRoute()
        {
            if(!ExpeditionPickShown)return;
            pending.Hadoken=pending.HadokenHeld=pending.Uppercut=false;pending.SirenAbility=0;
            if(ExpeditionPickPointer){pending.Shoot=pending.Attack=pending.Block=false;}
        }
        /// <summary>C: opens the tray on the waiting rewards or closes it (Choose later); from the J screen it closes J and opens it.</summary>
        private void ExpeditionPickKey(bool fromJ)
        {
            var v=LiveExpedition;if(v==null||arena==null)return;
            if(fromJ)expeditionPick.Show(v);else expeditionPick.Toggle(v);
        }
        // The shown reward's cards, read once per reward (the follow-through keeps its own snapshot).
        private void ExpeditionPickCards(ExpeditionView v,ExpeditionViewReward reward)
        {
            if(reward==null)return;string key=v.run+"|"+reward.id;if(key==expeditionPickKey)return;
            expeditionPickKey=key;expeditionPickTone=NativeExpeditionPick.Tone(reward);expeditionPickChips.Clear();
            foreach(var id in reward.offers){var c=arena?.PveChip(id);if(c!=null)expeditionPickChips.Add(c);}
            expeditionWrist=new NativePvePick{Id=NativeExpeditionPick.Id(reward.id),Tier=expeditionPickTone,Source=reward.source,Offers=new List<string>(reward.offers),At=arena?.Time??0};
        }
        // ArenaPresentation.ExpeditionChoosing: pilot `id`'s open pick while the view shows it choosing (the host's public flag), as the
        // Boss Test reads each row's replicated open pick. Mine carries the shown reward's offers and tone (my own view), so a new reward
        // re-deals its cards; it shows in first person too (the authority holds me, so the raised wrist covers no weapon I could use).
        // Anyone else's has no offers, so its cards draw cyan.
        private NativePvePick ExpeditionWrist(int id)
        {
            if(!expeditionChoosers.At(id,out var e))return null;
            if(e.Mine)
            {
                var r=expeditionPick.Choosing(LiveExpedition);if(r!=null&&expeditionWrist!=null&&expeditionWrist.Id==NativeExpeditionPick.Id(r.id))return expeditionWrist;
            }
            if(!expeditionChooserPicks.TryGetValue(e.Pick,out var pick))expeditionChooserPicks[e.Pick]=pick=new NativePvePick{Id=e.Pick,Source="expedition",At=(float)e.Since};
            return pick;
        }
        private static int ExpeditionHeld(ExpeditionView v,string chip){foreach(var c in v.mine.chips)if(c.id==chip)return c.count;return 0;}
        /// <summary>
        /// ArenaPresentation.ExpeditionPlant: pilot `id`'s wrist plant at presentation time `time`. Mine runs from my pick on the unscaled
        /// clock (immediate, whatever the tray does) and stops only if that pick is refused or fails; it stays on the hologram (Stay) when
        /// the tray will show another of my rewards next. Anyone else's runs from the authority's Install event for them (one per successful
        /// pick, public like the Boss Test's), newest first within the plant's span; Stay is unknown there (the flag reads choosing through
        /// the final plant as well), so their look lowers after each plant and rises again for a queued pick.
        /// </summary>
        private ChipPlantCue ExpeditionPlantCue(int id,float time)
        {
            if(arena?.Local==null)return ChipPlantCue.None;
            if(id==arena.Local.Seat.id)
            {
                float age=Time.unscaledTime-expeditionPlantAt;if(!ExpeditionMyPlant(age))return ChipPlantCue.None;var v=LiveExpedition;
                bool stay=expeditionPick.Open&&v!=null&&NativeExpeditionPick.MoreWaiting(v,expeditionPlantReward)>0;
                return new ChipPlantCue{Age=age,Chosen=expeditionPlantChosen,Chip=expeditionPlantChip,Stay=stay,Key=expeditionPlantKey};
            }
            var events=arena.Events;
            for(int i=events.Count-1;i>=0;i--){var e=events[i];float age=time-e.Time;if(age>=NativeChipPlant.Span)break;if(e.Kind==EventKind.Install&&e.Target==id&&age>=0)return new ChipPlantCue{Age=age,Chosen=-1,Chip=e.Detail,Key=e.Id};}
            return ChipPlantCue.None;
        }
        // My plant runs while its pick is in flight or confirmed, within the span; a refusal or failure ends it (the tray returns).
        private bool ExpeditionMyPlant(float age)
        {
            if(expeditionPlantReward==null||!NativeChipPlant.Active(age))return false;
            if(expeditionPick.Sent!=expeditionPlantReward&&expeditionPick.Installed!=expeditionPlantReward){expeditionPlantReward=null;return false;}
            return true;
        }
        /// <summary>LateFrame, after the Boss Test's PveFollowAudio: my plant holds my Install cue for the seat (NativeInstallCue at
        /// NativeDraftPlant.SoundAt, the plant's own moment), as the Boss Test's stage plant does.</summary>
        private void ExpeditionPlantAudio()
        {
            float age=Time.unscaledTime-expeditionPlantAt;if(nativeAudio==null||!ExpeditionMyPlant(age))return;
            nativeAudio.FollowChip=expeditionPlantChip;nativeAudio.FollowStarted=expeditionPlantAt;nativeAudio.FollowAge=age;
        }

        /// <summary>ExpeditionLiveHud, after the in-scene HUD: the follow-through, or the tray over the live view.</summary>
        private void ExpeditionPickGui(float W,float H)
        {
            if(ExpeditionFollowing){ExpeditionFollowGui(W,H);return;}
            var v=LiveExpedition;var reward=ExpeditionPickOverlay?expeditionPick.Choosing(v):null;if(reward==null)return;ExpeditionPickCards(v,reward);expeditionPickDrawn=Time.frameCount;
            // A new reward restarts the tray's entrance and focus at its first draw, as PveChipScreen does for a new pick.
            if(expeditionPickShown!=expeditionPickKey){expeditionPickShown=expeditionPickKey;pveOpenAt=Time.unscaledTime;draftFocus=-1;ResetHolo();}
            var commands=LiveExpeditionCommands();string blocked=commands==null?"Expedition is not bound.":commands.Blocked(new ExpeditionCommand(ExpeditionIntent.Select,reward.id));
            var chips=expeditionPickChips;
            int pick=ExpeditionPickTray(reward,chips,NativeExpeditionPick.MoreWaiting(v,reward.id),id=>ExpeditionHeld(v,id),blocked,expeditionPick.Refusal,PveUnderAttack(),Time.unscaledTime-pveOpenAt,ExpeditionPickPointer,W,H);
            if(pick==-2){expeditionPick.Later();return;}
            if(pick<0)return;var cmd=expeditionPick.Choose(v,chips[pick].id);if(cmd.intent==ExpeditionIntent.None)return;
            ExpeditionFollowStart(chips,pick,reward,ExpeditionHeld(v,chips[pick].id));ExpeditionLiveSubmit(cmd);
        }
        /// <summary>The tray's cards for the overlay: PveChipScreen's tray laid out in a band of at most 52% of the width (one row of up to
        /// six, two description lines; none if that would still reach the crosshair), centred and docked over the bottom edge.</summary>
        private Tray ExpeditionTrayLayout(List<ContentChip> chips,float W,float H)
        {
            float band=Mathf.Clamp(W*.52f,560,900),bottom=H-62;Tray t=null;
            foreach(float h in new[]{600f,540f}){t=LayoutTray(chips,0,band,h);float dy=bottom-t.Bottom;if(t.Top+dy>=H*.56f||h==540f)break;}
            float dx=(W-band)/2,shift=bottom-t.Bottom;for(int i=0;i<t.Cards.Length;i++){t.Cards[i].x+=dx;t.Cards[i].y+=shift;}t.Top+=shift;t.Bottom+=shift;
            return t;
        }
        /// <summary>
        /// The tray over the live view: a soft dark band under it, the header (the reward's source and kind, no tier; YOU ARE VULNERABLE
        /// WHILE CHOOSING and N more waiting), the cards (DrawTray with the number badges; keys from DraftKeys), the key line and Choose
        /// later under them, a status line (why picking is unavailable, or the last pick's refusal) and the under-attack alarm. Without Alt
        /// the pointer is kept off the cards (the locked cursor sits on the crosshair). Returns the card picked, -2 for Choose later, or -1.
        /// </summary>
        private int ExpeditionPickTray(ExpeditionViewReward reward,List<ContentChip> chips,int more,Func<string,int> held,string blocked,string refusal,bool attacked,float elapsed,bool pointer,float W,float H)
        {
            var e=Event.current;var mouse=e.mousePosition;if(!pointer)e.mousePosition=new Vector2(-1e5f,-1e5f);
            try
            {
                bool enabled=blocked==null;var t=expeditionTray=ExpeditionTrayLayout(chips,W,H);if(t.Cards.Length==0)return -1;
                float left=float.MaxValue,right=float.MinValue;foreach(var r in t.Cards){left=Mathf.Min(left,r.x);right=Mathf.Max(right,r.xMax);}
                U.Fade(new Rect(0,t.Top-110,W,H-t.Top+110),C.Void(.66f),false);
                ExpeditionTrayHeader(NativeExpeditionPick.Eyebrow(reward),NativeChipCard.Rarity(NativeExpeditionPick.Tone(reward)),"Choose a chip",left,right,t.Top,more,blocked!=null?"Unavailable · "+blocked:refusal,blocked!=null?C.Warn():C.Danger());
                int choice=DrawTray(t,chips,i=>held(chips[i].id),elapsed,enabled,-1,i=>false,true);int key=DraftKeys(chips.Count,enabled,true);if(key>=0)choice=key;
                if(choice>=0&&enabled)return choice;
                // The key line and Choose later under the cards.
                float fy=t.Bottom+8;string how="1–"+Mathf.Clamp(chips.Count,1,9)+" PICK  ·  HOLD ALT TO CLICK  ·  C OR ESC CLOSES";
                U.Text(new Rect(left,fy,right-left-180,26),how,11,C.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f,shadow:new Color(0,0,0,.8f));
                bool later=U.Btn(new Rect(right-170,fy-1,170,28),"Choose later  C · Esc",false,true,1,12);
                ExpeditionTrayAlarm(W,H,left,right,t.Top,attacked,prefs.reducedEffects);
                return later?-2:-1;
            }
            finally{e.mousePosition=mouse;}
        }
        // The overlay's header over the tray (canvas units; `top` is the cards' top edge): the eyebrow in the reward's tone, the title,
        // and at the right YOU ARE VULNERABLE WHILE CHOOSING with the queue pill; a status pill under it when there is one.
        private static void ExpeditionTrayHeader(string eyebrow,Color tone,string title,float left,float right,float top,int more,string status,Color statusTone)
        {
            var shade=new Color(0,0,0,.85f);float y=top-66;
            U.Text(new Rect(left,y,(right-left)*.6f,15),eyebrow,12,tone,TextAnchor.MiddleLeft,bold:true,tracking:.2f,shadow:shade);
            U.Text(new Rect(left,y+18,(right-left)*.6f,28),title,22,C.Ink(),TextAnchor.MiddleLeft,bold:true,tracking:-.02f,shadow:shade);
            string warn="YOU ARE VULNERABLE WHILE CHOOSING";float ww=U.Measure(warn,11,true,true,.08f).x;U.Text(new Rect(right-ww,y-4,ww+2,24),warn,11,C.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f,shadow:shade);
            if(more>0){string q=(more+" more waiting").ToUpperInvariant();float qw=U.Measure(q,11,true,true,.08f).x+20;var r=new Rect(right-ww-12-qw,y-4,qw,24);U.Pill(r,C.Magenta(.16f),Color.clear);U.Text(r,q,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);}
            if(!string.IsNullOrEmpty(status)){float sw=Mathf.Min(U.Measure(status,12,true).x+28,(right-left)*.55f);var r=new Rect(right-sw,y+24,sw,26);U.Round(r,C.Void(.9f),8,U.A(statusTone,.7f));U.Text(new Rect(r.x+14,r.y,r.width-20,r.height),status,12,statusTone,TextAnchor.MiddleLeft,bold:true);}
        }
        // Under attack: PveChipScreen's blinking red border and alarm, the alarm over the tray's header (the cards do not move). Its
        // full-screen red wash is kept faint here (the Boss Test's .6 wash would hide the fight this pilot is still in); steady under
        // Reduced effects.
        private static void ExpeditionTrayAlarm(float W,float H,float left,float right,float top,bool attacked,bool reduced)
        {
            if(!attacked)return;bool on=reduced||Mathf.Repeat(Time.unscaledTime,.25f)<.125f;
            U.Round(new Rect(0,0,W,H),Color.clear,2,C.Danger(on?1:.15f),4);U.Glow(new Rect(-30,-30,W+60,H+60),C.Danger(reduced?.06f:on?.12f:.04f),-60);
            string alarm="☠  WARNING — YOU ARE UNDER ATTACK",sub="C closes the chips to move and fight";float aw=U.Measure(alarm,14,true,true,.14f).x+U.Measure(sub,12).x+48;var ar=new Rect((left+right)/2-aw/2,top-66-46,aw,36);
            U.Round(ar,C.Danger(on?.42f:.2f),8,C.Danger());U.Text(new Rect(ar.x+12,ar.y,aw,ar.height),alarm,14,Color.white,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);U.Text(new Rect(ar.x,ar.y,aw-12,ar.height),sub,12,C.Ink(),TextAnchor.MiddleRight);
        }

        // ---- The follow-through over the live view ----
        private void ExpeditionFollowStart(List<ContentChip> chips,int index,ExpeditionViewReward reward,int owned)
        {
            expeditionFollowChips.Clear();expeditionFollowChips.AddRange(chips);expeditionFollowIndex=index;expeditionFollowOwned=owned;expeditionFollowAt=Time.unscaledTime;
            expeditionFollowReward=reward.id;expeditionFollowEyebrow=NativeExpeditionPick.Eyebrow(reward);expeditionFollowTone=NativeExpeditionPick.Tone(reward);expeditionFollowTray=expeditionTray;
            // The plant: the card it takes is the chip's place among the offers my wrist hologram shows (its first three).
            expeditionPlantAt=expeditionFollowAt;expeditionPlantReward=reward.id;expeditionPlantChip=chips[index].id;expeditionPlantKey++;
            expeditionPlantChosen=expeditionWrist!=null&&expeditionWrist.Id==NativeExpeditionPick.Id(reward.id)?expeditionWrist.Offers.IndexOf(chips[index].id):-1;if(expeditionPlantChosen>2)expeditionPlantChosen=-1;
        }
        /// <summary>A pick's follow-through is playing: until its span ends, or at once when the pick is neither in flight nor confirmed
        /// (refused or unconfirmed: the tray returns with the caption) or the overlay could not show.</summary>
        private bool ExpeditionFollowing
        {
            get
            {
                if(expeditionFollowIndex<0)return false;
                if(expeditionFollowIndex>=expeditionFollowChips.Count||expeditionFollowTray==null||Time.unscaledTime-expeditionFollowAt>ExpeditionFollowSpan||!ExpeditionPickCanShow||
                    expeditionPick.Sent!=expeditionFollowReward&&expeditionPick.Installed!=expeditionFollowReward){expeditionFollowIndex=-1;return false;}
                return true;
            }
        }
        private void ExpeditionFollowGui(float W,float H)
        {
            float age=Time.unscaledTime-expeditionFollowAt;var t=expeditionFollowTray;var chip=expeditionFollowChips[expeditionFollowIndex];
            float left=float.MaxValue,right=float.MinValue;foreach(var r in t.Cards){left=Mathf.Min(left,r.x);right=Mathf.Max(right,r.xMax);}
            float fade=Mathf.Clamp01((ExpeditionFollowSpan-age)/.3f);U.Fade(new Rect(0,t.Top-110,W,H-t.Top+110),C.Void(.66f*fade),false);
            bool confirmed=expeditionPick.Installed==expeditionFollowReward&&age>=ExpeditionFollowSettle;
            string status=null;if(!confirmed&&age>=ExpeditionFollowSettle){var state=expeditionIntents.State(new ExpeditionCommand(ExpeditionIntent.Select,expeditionFollowReward),expeditionLiveView,Time.unscaledTime);status=P.Feedback(state,out _)??"Sent · waiting for the authority";}
            ExpeditionTrayHeader(expeditionFollowEyebrow,U.A(NativeChipCard.Rarity(expeditionFollowTone),fade),confirmed?"Upgrade secured · "+chip.name:"Installing · "+chip.name,left,right,t.Top,0,status,C.CyanSoft());
            // The others shatter; the chosen card flies into my wrist hologram, shrinking and fading as the hand takes it there (the plant's
            // contact), or toward the hands below the bottom edge when the hologram is not in view.
            draftCatch=draftPalm=ExpeditionFollowTarget((left+right)/2,W,H);PickedTray(t,expeditionFollowChips,expeditionFollowIndex,expeditionFollowOwned,age);
        }
        // My plant's chip (or hologram) on screen (canvas units), from the frame's pose and the camera that draws it.
        private Vector2 ExpeditionFollowTarget(float centre,float W,float H)
        {
            var fallback=new Vector2(centre,H+60);if(presentation==null||arena?.Local==null||!presentation.ChipSelectFocus(arena.Local.Seat.id,out var world,out bool view))return fallback;
            var cam=view?presentation.ViewCamera:cameraView;if(cam==null)return fallback;var sp=cam.WorldToScreenPoint(world);if(sp.z<=.05f)return fallback;
            float scale=Screen.width/Mathf.Max(1,W);var at=new Vector2(sp.x/scale,(Screen.height-sp.y)/scale);
            return at.x<-W*.1f||at.x>W*1.1f||at.y<-H*.1f||at.y>H*1.1f?fallback:at;
        }

        /// <summary>The in-scene CHIPS tag (the Boss Test's PveChipsTag): the waiting count and the C key, pulsing for 6 s after it grows;
        /// hidden while the tray is wanted open.</summary>
        private void ExpeditionChipsTag(ExpeditionView v)
        {
            int n=NativeExpeditionPick.Waiting(v);if(n!=expeditionChipsSeen){if(n>expeditionChipsSeen)expeditionChipsAt=Time.unscaledTime;expeditionChipsSeen=n;}
            if(n>0&&!expeditionPick.Open)PveChipsTag(U.W,U.H,n,Time.unscaledTime-expeditionChipsAt);
        }
    }
}
