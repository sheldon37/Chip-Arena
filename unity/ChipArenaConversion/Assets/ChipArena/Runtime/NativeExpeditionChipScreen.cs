using System;
using System.Collections.Generic;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using P=ChipArena.UnityConversion.NativeExpeditionPresentation;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    // An Expedition's chip pick: the Boss Test's chip tray as an overlay over the live view (Claude, 2026-10-15; owner: "We should also
    // utilize the same chip selection screen (That we utilize in the Boss Test) instead of selecting on the J menu."; then: choosers stay
    // mobile and vulnerable; docs/EXPEDITION-PRESENTATION.md "Chip picks"). Core/NativeExpeditionPick.cs decides what shows and when; this
    // draws PveChipScreen's cards (DrawTray: the entrances, the holo highlight, the number badges, the hover cue), keys 1-9 / arrows /
    // Enter (DraftKeys) and a follow-through on the same pieces (Shatter, FlyToPalm), docked at the bottom centre between the health box
    // and the cooldowns so the crosshair and the world stay in view. No 3D draft stage and the arena stays drawn: the pilot keeps moving, fighting
    // and taking damage. It sends Claim / OpenChoice / CloseChoice / Select through ExpeditionLiveSubmit (host: the session's commands;
    // guest: the command channel); the host only publishes members[].choosing (no lock), which drives every choosing pilot's wrist look
    // and SELECTING CHIP tag (ArenaPresentation.ExpeditionChoosing), and a successful Select emits the Install event and its cue.
    // Client input routing while the tray is up (never an authority lock): digits 1-9 pick instead of starting abilities; holding Alt
    // frees the cursor to click a card (mouse look, attack and block pause only while it is held); WASD, jump, sprint, roll, Q, E, F and
    // the mouse otherwise work as usual; C or Escape closes it (CloseChoice).
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
        // The follow-through: the others shatter and the chosen card drops out of view (PickedTray), then the confirmation holds a beat.
        // It reads "Installing" until the view confirms the pick, for at least the first beat (a host-local confirmation lands within a
        // frame or two and would otherwise flicker the heading).
        private const float ExpeditionFollowSpan=1.6f,ExpeditionFollowSettle=.35f;

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
            if(presentation!=null)presentation.ExpeditionChoosing=v!=null&&arena!=null?expeditionChoosing??=ExpeditionWrist:null;
            expeditionChoosers.Step(v,arena?.Time??0);if(expeditionChoosers.Count==0)expeditionChooserPicks.Clear();
            if(v==null){expeditionPick.Step(null,null,null,false);expeditionFollowIndex=-1;return;}
            var controls=LiveExpeditionControls();var act=expeditionPick.Step(v,controls.State,controls.Last,ExpeditionPickCanShow);
            if(act.Send.intent!=ExpeditionIntent.None)ExpeditionLiveSubmit(act.Send);
            if(act.Notice!=null)Notice(act.Notice);
            ExpeditionPickCards(v,expeditionPick.Choosing(v));
        }
        /// <summary>
        /// Update, after the play input is read (client input routing only: the host locks nothing, choosers stay mobile and vulnerable).
        /// While the tray is up the digit keys pick a chip, so the abilities they would start (1 / 2 / the 1-5 kit keys) are dropped; while
        /// Alt frees the pointer, the mouse buttons click cards instead of attacking or blocking. Movement, jump, sprint, roll, Q, E, F and
        /// reload are untouched.
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
        // re-deals its cards, and steps aside in first person while my tray is on screen (the overlay already shows the cards; the raised
        // wrist would cover the weapon I am still using). Anyone else's has no offers, so its cards draw cyan.
        private NativePvePick ExpeditionWrist(int id)
        {
            if(!expeditionChoosers.At(id,out var e))return null;
            if(e.Mine)
            {
                if(FirstPersonView&&(ExpeditionPickOverlay||ExpeditionFollowing))return null;
                var r=expeditionPick.Choosing(LiveExpedition);if(r!=null&&expeditionWrist!=null&&expeditionWrist.Id==NativeExpeditionPick.Id(r.id))return expeditionWrist;
            }
            if(!expeditionChooserPicks.TryGetValue(e.Pick,out var pick))expeditionChooserPicks[e.Pick]=pick=new NativePvePick{Id=e.Pick,Source="expedition",At=(float)e.Since};
            return pick;
        }
        private static int ExpeditionHeld(ExpeditionView v,string chip){foreach(var c in v.mine.chips)if(c.id==chip)return c.count;return 0;}

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
                ExpeditionTrayAlarm(W,H,left,right,t.Top,attacked);
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
        // Under attack: PveChipScreen's blinking red vignette and alarm, the alarm over the tray's header (the cards do not move).
        private static void ExpeditionTrayAlarm(float W,float H,float left,float right,float top,bool attacked)
        {
            if(!attacked)return;bool on=Mathf.Repeat(Time.unscaledTime,.25f)<.125f;
            U.Round(new Rect(0,0,W,H),Color.clear,2,C.Danger(on?1:.15f),4);U.Glow(new Rect(-30,-30,W+60,H+60),C.Danger(on?.6f:.09f),-60);
            string alarm="WARNING — YOU ARE UNDER ATTACK",sub="C closes the chips · you can still move and fight";float aw=U.Measure(alarm,14,true,true,.14f).x+U.Measure(sub,12).x+56;var ar=new Rect((left+right)/2-aw/2,top-66-46,aw,36);
            U.Round(ar,C.Danger(on?.42f:.2f),8,C.Danger());U.Text(new Rect(ar.x+12,ar.y,aw,ar.height),"☠  "+alarm,14,Color.white,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);U.Text(new Rect(ar.x,ar.y,aw-12,ar.height),sub,12,C.Ink(),TextAnchor.MiddleRight);
        }

        // ---- The follow-through over the live view ----
        private void ExpeditionFollowStart(List<ContentChip> chips,int index,ExpeditionViewReward reward,int owned)
        {
            expeditionFollowChips.Clear();expeditionFollowChips.AddRange(chips);expeditionFollowIndex=index;expeditionFollowOwned=owned;expeditionFollowAt=Time.unscaledTime;
            expeditionFollowReward=reward.id;expeditionFollowEyebrow=NativeExpeditionPick.Eyebrow(reward);expeditionFollowTone=NativeExpeditionPick.Tone(reward);expeditionFollowTray=expeditionTray;
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
            // The others shatter; the chosen card drops toward the pilot's hands (below the bottom edge), shrinking and fading.
            draftCatch=draftPalm=new Vector2((left+right)/2,H+60);PickedTray(t,expeditionFollowChips,expeditionFollowIndex,expeditionFollowOwned,age);
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
