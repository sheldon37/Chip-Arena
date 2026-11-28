using System;
using System.Collections.Generic;
using UnityEngine;
using NVector2=System.Numerics.Vector2;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The Deathmatch's chip picks on the PvP chip draft's own screen (owner 2026-11-18: "For the deathmatch mode, For the chip
    // selection; We can bring the player model in for the chip selection like we do for the normal pvp mode. As matter of fact,
    // we can use that exact screen."): the starting chip (the opening's Chip step, in the room before the match) and the
    // comeback chips (the replicated seat's offers, during the fight). Both draw the PvP draft's stage (NativeDraftStage: the
    // pilot close, the cyber corridor, the gaze and the reach toward the highlighted card) with the local pilot on it, and its
    // GUI (the shades, the header, the tray low in front of the pilot, the nameplate), as the Siren personal pick does; a pick
    // plays the draft's follow-through (NativePickFollow). The rules are untouched: the same opening-choice and dm-chip
    // messages go out at the pick, each pilot sees only their own offers until the reveal, the step's 20 s timer runs on, the
    // comeback offers stay queued across respawn (Choose later, C), and the stage never holds a respawn: it gives the live view
    // back when the pilot respawns. Card clicks go through U.SilentButton (DrawTray), so the controller pointer clicks them.
    public sealed partial class ConversionGame
    {
        private enum PickStageKind{None,Opening,Comeback}
        // The stage's view keys in DraftFrame's draftViewSeat (never a seat id), so a change of screen restarts the stage.
        private const int OpeningStageView=-7101,ComebackStageView=-7102;
        // The offers on screen (rebuilt when their key changes: a new session's chip step, a new comeback ticket) and when they opened.
        private string pickStageKey;private float pickStageOpenedAt;private Tray pickStageTray;private readonly List<ContentChip> pickStageChips=new List<ContentChip>();
        // The follow-through after my pick: its screen, the offers' snapshot, the chosen card, my copies before it, when, whether I was down.
        private PickStageKind pickFollowKind;private string pickFollowKey;private readonly List<ContentChip> pickFollowChips=new List<ContentChip>();private Tray pickFollowTray;
        private int pickFollowIndex=-1,pickFollowOwned,pickFollowSeat;private float pickFollowAt,pickFollowOpened;private bool pickFollowDown;
        // The local pilot on the opening's stage: made once a session from the profile, as the match will seat it, holding the locked weapon.
        private NativeSeat openingStageSeat;private string openingStageSeatKey;

        // ---- which screen ----
        // The same answer for the stage (LateFrame) and its GUI (OnGUI): the opening's chip step or its follow-through while the
        // opening view is up; otherwise, in a Deathmatch fight, the open comeback picker or its follow-through.
        private PickStageKind PickStageNow(out NativeSeat seat)
        {
            seat=null;var v=openingView;
            if(v!=null)
            {
                if(v.pick!=(int)OpeningStep.Chip&&!PickFollowing(PickStageKind.Opening))return PickStageKind.None;
                seat=OpeningStageSeat(v);return PickStageKind.Opening;
            }
            if(!ComebackStageOpen)return PickStageKind.None;var local=arena.Local.Seat;
            if(!PickFollowing(PickStageKind.Comeback)&&!(deathPickOpen&&local.offers.Count>0))return PickStageKind.None;
            seat=local;return PickStageKind.Comeback;
        }
        private bool ComebackStageOpen=>!menu&&arena?.Local!=null&&match!=null&&match.DeathmatchTarget>0&&match.Phase==NativePhase.Fight&&previewArena==null;
        // The picker opens as the pilot goes down on a rewarded death (DeathmatchPickKeys) and stands in front of that death's
        // killcam, as the tray did: a killcam already running ends (as Space skips it); one not yet started waits until the
        // picker and its follow-through close (Choose later, C, the pick), then plays what is left before the respawn.
        // (Every LateFrame with an arena, before the killcam: so the comeback stage also closes here when the match leaves it.)
        private bool PickStageOverReplay(){if(PickStageNow(out _)!=PickStageKind.Comeback){PickStageLeave();return false;}if(ReplayActive)EndReplay();return true;}
        private bool PickStageShown=>PickStageNow(out _)!=PickStageKind.None;
        private NativeSeat OpeningStageSeat(NativeNetMessage v)
        {
            string key=v.pickSession+"/"+v.chip;if(openingStageSeat!=null&&openingStageSeatKey==key)return openingStageSeat;openingStageSeatKey=key;
            NativeSeat s;try{s=SanitizedSeat(server==null?1:networkId,Profile(),account?.Token);}
            catch(Exception e)when(e is ArgumentException||e is InvalidOperationException||e is KeyNotFoundException){s=new NativeSeat{id=server==null?1:networkId,name=prefs.name,bodyColor=prefs.bodyColor,accentColor=prefs.accentColor,hat=prefs.hat};}
            if(OpeningWeapon(v.chip)!=null)s.spec=v.chip;s.team=prefs.team;return openingStageSeat=s;
        }
        // Whether my follow-through on that screen still plays (the key, the attack and the snapshot end it in the GUI).
        private bool PickFollowing(PickStageKind kind)
        {
            if(pickFollowIndex<0||pickFollowKind!=kind)return false;
            bool gone;
            if(kind==PickStageKind.Opening){var v=openingView;gone=v==null||v.pickSession!=pickFollowKey||v.pick==(int)OpeningStep.Reveal;}
            else gone=!ComebackStageOpen||arena.Local.Seat.id!=pickFollowSeat;
            bool respawned=kind==PickStageKind.Comeback&&!gone&&pickFollowDown&&arena.Local.Seat.alive;
            if(pickFollowIndex>=pickFollowChips.Count||!NativePickFollow.Plays(Time.unscaledTime-pickFollowAt,false,false,respawned,gone)){EndPickFollow();return false;}
            return true;
        }
        private void EndPickFollow(){if(pickFollowIndex>=0&&pickFollowIndex<pickFollowChips.Count)InstallBloomStart(RarityColor(pickFollowChips[pickFollowIndex].rarity),Time.unscaledTime-pickFollowAt);pickFollowIndex=-1;pickFollowKind=PickStageKind.None;pickFollowChips.Clear();}
        private void StartPickFollow(PickStageKind kind,string key,int index,int owned,int seat,bool down)
        {
            pickFollowChips.Clear();pickFollowChips.AddRange(pickStageChips);pickFollowTray=pickStageTray;pickFollowIndex=index;pickFollowKind=kind;pickFollowKey=key;
            pickFollowOwned=owned;pickFollowSeat=seat;pickFollowDown=down;pickFollowAt=Time.unscaledTime;pickFollowOpened=pickStageOpenedAt;
        }
        // The chips for this screen's offers; a new set (its key) opens with no card highlighted, booting from now.
        private void PickStageOffers(string key,IList<string> ids)
        {
            if(key!=pickStageKey){pickStageKey=key;pickStageOpenedAt=Time.unscaledTime;draftFocus=-1;ResetHolo();}
            pickStageChips.Clear();if(ids!=null)foreach(var id in ids){var c=OpeningChip(id);if(c!=null&&pickStageChips.Count<16)pickStageChips.Add(c);}
        }

        // ---- the stage (LateFrame) ----
        // DraftFrame's stage for the local pilot, with the PvP draft's look (no ward, no prism, no band on your own pilot). In the
        // room there is no arena, so the stage stands in for the menu backdrop (PickStageMenuFrame) and starts, as the PvP draft
        // does, on a reset presentation. The comeback pick opens mid-fight: the arena's presentation is not reset (its corpses,
        // blood, wounds and gibs stay for the fight, as the owner's coordinator asked); only the stage restarts (its rig and model
        // made again, RestartDraftStage), and everything else in the scene is hidden while it shows and put back as it was when it
        // closes (PickStageHide / PickStageLeave), the arena simulating on. Returns false when no pick screen is up.
        private bool PickStageFrame()
        {
            if(presentation==null||cameraView==null)return false;var kind=PickStageNow(out var seat);if(kind==PickStageKind.None||seat==null){PickStageLeave();return false;}
            int view=kind==PickStageKind.Opening?OpeningStageView:ComebackStageView;
            if(!draftViewing||draftViewSeat!=view)
            {
                PickStageUnhide();RestoreLive();if(kind==PickStageKind.Comeback)presentation.RestartDraftStage();else presentation.ResetPresentation();
                draftHover=-1;draftFocus=-1;draftHeld.Reset();ResetHolo();draftViewSeat=view;draftViewing=true;draftCards=Array.Empty<Rect>();draftTrayLine=draftTrayBottom=-1;
            }
            float scale=NativeUiMotion.Measure(Screen.width,Screen.height,prefs.uiScale).Scale,width=Screen.width/scale,height=Screen.height/scale;
            bool follow=PickFollowing(kind);int count=follow?pickFollowChips.Count:pickStageChips.Count;
            float age=follow?Time.unscaledTime-pickFollowAt:-1;var chosenChip=follow?pickFollowChips[pickFollowIndex]:null;
            double turnStart=follow?pickFollowOpened:pickStageOpenedAt;float since=Time.unscaledTime-(float)turnStart;int n=Math.Min(count,draftCards.Length);
            if(draftPoints.Length<n)draftPoints=new NVector2[Math.Max(n,draftPoints.Length*2)];for(int i=0;i<n;i++)draftPoints[i]=new NVector2(draftCards[i].center.x/width,draftCards[i].center.y/height);
            int chosen=follow?pickFollowIndex:-1;if(turnStart!=draftHeldTurn){draftHeldTurn=turnStart;draftHeld.Reset();if(!follow)ResetHolo();}
            if(chosen>=0)draftHeld.Reset();else draftHeld.Step(draftHover,Time.unscaledDeltaTime);int focus=NativeDraftStage.Focus(chosen,draftHeld.Card,since,n,prefs.reducedEffects);bool consider=chosen<0&&draftHeld.Reaching;
            if(kind==PickStageKind.Comeback)PickStageHide();else if(arena!=null)HideLive();else{presentation.WeaponLoadout=null;presentation.LocalId=seat.id;}viewModelCamera.enabled=false;
            // The stage lights take my colour; the install blends them to the chip's from the clack.
            Color chipTint=chosenChip!=null?RarityColor(chosenChip.rarity):default,tint=SeatColor(seat);
            presentation.DraftHeadLine=draftHeadLine;presentation.DraftWard=false;presentation.DraftPrism=false;presentation.DraftBandTeam=-1;
            draftPalm=presentation.RenderDraft(seat,age,focus,consider,draftPoints,n,draftTrayLine,draftTrayBottom,tint,cameraView,prefs.reducedEffects,out var catchAt,chipTint,chosenChip?.rarity,chosenChip?.id,true)/scale;draftPalm.y=height-draftPalm.y;
            draftCatch=catchAt/scale;draftCatch.y=height-draftCatch.y;draftDevice2d=presentation.DraftDeviceScreen/scale;draftDevice2d.y=height-draftDevice2d.y;
            if(chosenChip!=null&&nativeAudio!=null){nativeAudio.StageChip=chosenChip.id;nativeAudio.StageTier=chosenChip.rarity;nativeAudio.StageAge=presentation.DraftAge;nativeAudio.StageBody=presentation.DraftDog?2:1;nativeAudio.StageBreaks=Math.Max(0,n-1);nativeAudio.StageShort=true;nativeAudio.StageReduced=prefs.reducedEffects;nativeAudio.StageKey=pickFollowAt;}
            return true;
        }
        // The comeback stage's hide: every renderer in the scene but the stage's own (the draft rig and the model it carries) is
        // switched off while it shows (Renderer.enabled, the hide flag of the Siren pick, the killcam and the preview; each frame, so
        // what the fight spawns meanwhile stays hidden too) and put back as it was when the stage closes. Nothing is destroyed.
        private readonly Dictionary<Renderer,bool> pickStageHidden=new Dictionary<Renderer,bool>();
        private void PickStageHide()
        {
            var own=presentation.DraftStageRoot;
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(r!=null&&r.enabled&&(own==null||!r.transform.IsChildOf(own))){pickStageHidden[r]=true;r.enabled=false;}
        }
        private void PickStageUnhide(){foreach(var pair in pickStageHidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;pickStageHidden.Clear();}
        // Leaving the comeback stage: what it hid comes back; nothing is reset (DraftFrame's own exit, which resets, is not taken).
        private void PickStageLeave(){if(draftViewing&&draftViewSeat==ComebackStageView){draftViewing=false;RestoreLive();}PickStageUnhide();}
        // LateFrame without an arena (the room's opening): the stage in place of the menu backdrop, the rooftop put away and the
        // map's look applied again once it leaves.
        private bool PickStageMenuFrame()
        {
            if(PickStageNow(out _)!=PickStageKind.None){RooftopLeave();backdropMap=null;if(PickStageFrame())return true;}
            if(draftViewing){draftViewing=false;RestoreLive();presentation?.ResetPresentation();}
            return false;
        }
        // My comeback chip's Install cue waits for the plant (NativeInstallCue), as the Siren pick's follow-through holds it.
        private void PickStageAudio()
        {
            if(nativeAudio==null||!PickFollowing(PickStageKind.Comeback))return;
            nativeAudio.FollowChip=pickFollowChips[pickFollowIndex].id;nativeAudio.FollowStarted=pickFollowAt;nativeAudio.FollowAge=Time.unscaledTime-pickFollowAt;
        }

        // ---- the GUI ----
        // After my pick: the header, the tray in its picked state (the others shatter, mine flies to the palm) and the nameplate;
        // nothing to click. False when no follow-through plays (a key, a click or an attack ends it here).
        private bool PickFollowGui(PickStageKind kind,string eyebrow,Color tone,NativeSeat seat,float W,float H)
        {
            if(seat==null||!PickFollowing(kind))return false;float age=Time.unscaledTime-pickFollowAt;if(Event.current.type==EventType.Repaint)draftHover=-1;
            bool attacked=kind==PickStageKind.Comeback&&seat.alive&&PveUnderAttack();
            if(!NativePickFollow.Plays(age,Input.anyKeyDown,attacked,false,false)){EndPickFollow();return false;}
            float a=Math.Abs(presentation.DraftAge-age)<.3f?presentation.DraftAge:age;
            DraftShades(pickFollowTray,W,H);DraftHeader(eyebrow,tone,FollowTitle(a),"",W,H,out _);
            var tray=pickFollowTray;if(tray!=null&&tray.Cards.Length==pickFollowChips.Count)PickedTray(tray,pickFollowChips,pickFollowIndex,pickFollowOwned,a,true);InstallFlash(a);
            Nameplate(seat,NativeInstallShot.Installed(a)?"Installed":"Installing",H<=760?22:28,H-(H<=760?14:22)-66);
            if(pickFollowIndex>=0&&pickFollowIndex<pickFollowChips.Count)InstallBloom(RarityColor(pickFollowChips[pickFollowIndex].rarity),a,W,H);return true;
        }
        // The tray low in front of the pilot (the Siren pick's bounds: over the nameplate and the footer); its cards go to the stage.
        private void PickStageTray(float top,float W,float H)
        {
            pickStageTray=LayoutTray(pickStageChips,top,W,H);LowerTray(pickStageTray,top,H-142);draftCards=pickStageTray.Cards;draftTrayLine=pickStageTray.Top/H;draftTrayBottom=pickStageTray.Bottom/H;
        }
        // Bottom right: how to pick, and (the comeback) Choose later. True when Choose later was pressed.
        private bool PickStageFooter(int count,bool later,float W,float H)
        {
            float m=H<=760?22:28;string how="Press 1–"+Mathf.Clamp(count,1,9)+" or click a chip";float hw=U.Measure(how,12,false,true).x,bw=later?180:0,fw=hw+24+(later?12+bw:0);var foot=new Rect(W-m-fw,H-m-56,fw,56);U.Round(foot,U.Hex(0x0d0f16,.9f),12);
            U.Text(new Rect(foot.x+12,foot.y,hw+4,foot.height),how,12,U.Hex(0x9799a6),TextAnchor.MiddleLeft,mono:true);
            return later&&U.Btn(new Rect(foot.xMax-12-bw,foot.y+8,bw,40),"Choose later  C",false,true,1,14);
        }

        // The starting chip: the PvP draft's screen for my own private offers. Top right the opening's locked-in count and the
        // step's countdown in the draft's pills; my locked weapon on the nameplate. A pick confirms at once, as before.
        private void OpeningChipStage(NativeNetMessage v,float W,float H,float now)
        {
            const string eyebrow="DEATHMATCH  /  STARTING CHIP";var seat=OpeningStageSeat(v);var tone=U.Hex(0xc0c1ca);
            if(PickFollowGui(PickStageKind.Opening,eyebrow,tone,seat,W,H))return;
            PickStageOffers("opening/"+v.pickSession,v.pickCards);var chips=pickStageChips;
            DraftShades(pickStageTray,W,H);DraftHeader(eyebrow,tone,"Pick your starting chip","One Chip to begin the match with. Only you see these choices.",W,H,out float top);
            OpeningStageMeta(v,W,H);if(chips.Count==0)return;PickStageTray(top,W,H);
            bool open=openingChipPick==null;int pick=DrawTray(pickStageTray,chips,i=>0,now-pickStageOpenedAt,open,-1,i=>false,true);int key=DraftKeys(chips.Count,open,true);if(key>=0)pick=key;
            if(pick>=0&&open)
            {
                openingChipPick=chips[pick].id;nativeAudio?.UiCue("ui.draft.pick","ui",.4f);
                StartPickFollow(PickStageKind.Opening,v.pickSession,pick,0,seat.id,false);SendOpeningChoice(chips[pick].id,true);return;
            }
            var weapon=OpeningWeapon(v.chip);Nameplate(seat,weapon!=null?weapon.name:"Your pick",H<=760?22:28,H-(H<=760?14:22)-66);
            PickStageFooter(chips.Count,false,W,H);
        }
        // The draft's top-right pills for the opening: the locked-in count (with a pip per pilot) and the step's seconds, red in
        // the last five (the countdown runs on between the host's views).
        private void OpeningStageMeta(NativeNetMessage v,float W,float H)
        {
            float x=W-(H<=760?22:28),y=H<=760?16:22;int ready=Mathf.Clamp(v.id,0,8),total=Mathf.Clamp(v.team,1,8);
            string text="LOCKED IN "+ready+" / "+total;float tw=U.Measure(text,12,true).x;U.Text(new Rect(x-tw,y,tw+2,28),text,12,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft,bold:true);
            float px=x-tw-10;for(int i=total-1;i>=0;i--){var pip=new Rect(px-9,y+9.5f,9,9);px-=14;if(i<ready){U.Glow(pip,NativeCyberTheme.Cyan(.45f),5);U.Round(pip,U.Mix(NativeCyberTheme.Cyan(),Color.white,.3f),2);}else U.Round(pip,NativeCyberTheme.Edge(.25f),2,NativeCyberTheme.Edge(.8f));}
            float left=OpeningRemaining(v);string timer=Mathf.CeilToInt(left)+" seconds";float w=U.Measure(timer,13,true,true).x+22;var r=new Rect(x-w,y+38,w,28);
            U.Pill(r,U.Hex(0x15161c,.85f),left<=5?U.A(NativeCyberTheme.Danger(),.6f):new Color(1,1,1,.14f));U.Text(r,timer,13,left<=5?NativeCyberTheme.Danger():U.Hex(0xff996f),TextAnchor.MiddleCenter,bold:true,mono:true);
        }

        // The comeback chip: the same screen over the live fight's stage (the arena hidden, still simulating). Top right, as the
        // Siren pick: standing still while choosing, and how many more picks wait; the respawn's pill sits top centre while down.
        private void ComebackStage(NativeSeat seat,float W,float H,float now)
        {
            const string eyebrow="DEATHMATCH  /  COMEBACK CHIP";var tone=NativeCyberTheme.Magenta();
            PickStageOffers("comeback/"+seat.id+"/"+seat.pickTicket,seat.offers);var chips=pickStageChips;
            DraftShades(pickStageTray,W,H);DraftHeader(eyebrow,tone,"Choose a comeback chip",seat.alive?"Choose one Chip. You stand still while you choose; the fight goes on around you.":"Respawn continues while you choose. The fight goes on around you.",W,H,out float top);
            float x=W-(H<=760?22:28),y=H<=760?16:22;
            if(seat.alive){const string warn="YOU ARE VULNERABLE WHILE CHOOSING";float ww=U.Measure(warn,11,true,true,.08f).x;U.Text(new Rect(x-ww,y,ww+2,24),warn,11,U.Hex(0xd8dae4),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);x-=ww+12;}
            int queued=Math.Max(0,seat.owedPicks-1);if(queued>0){string q=queued+" MORE WAITING";float qw=U.Measure(q,11,true,true,.08f).x+20;var r=new Rect(x-qw,y,qw,24);U.Pill(r,U.A(tone,.13f),Color.clear);U.Text(r,q,11,U.Mix(tone,Color.white,.45f),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);}
            if(chips.Count==0)return;PickStageTray(top,W,H);
            int pick=DrawTray(pickStageTray,chips,i=>seat.chips.TryGetValue(chips[i].id,out int n)?n:0,now-pickStageOpenedAt,true,-1,i=>false,true);int key=DraftKeys(chips.Count,true,true);if(key>=0)pick=key;
            if(pick>=0)
            {
                string id=chips[pick].id;nativeAudio?.UiCue("ui.draft.pick","ui",.4f);
                StartPickFollow(PickStageKind.Comeback,null,pick,seat.chips.TryGetValue(id,out int owned)?owned:0,seat.id,!seat.alive);
                if(server==null)arena.InstallDeathmatchChip(seat.id,seat.pickTicket,id);
                else SendServer(new NativeNetMessage{type="dm-chip",chip=id,pick=seat.pickTicket});
                deathPickOpen=false;return;
            }
            Nameplate(seat,"Your pick",H<=760?22:28,H-(H<=760?14:22)-66);
            if(PickStageFooter(chips.Count,true,W,H))deathPickOpen=false;
            // Attacked while choosing: the Siren pick's alarm and red edge, without moving the cards.
            if(seat.alive&&PveUnderAttack())
            {
                bool on=Mathf.Repeat(Time.unscaledTime,.25f)<.125f;U.Round(new Rect(0,0,W,H),Color.clear,2,U.Hex(0xff2b3d,on?1:.15f),4);U.Glow(new Rect(-30,-30,W+60,H+60),U.Hex(0xff1f33,on?.6f:.09f),-60);
                const string alarm="WARNING — YOU ARE UNDER ATTACK",sub="Choose later to defend yourself";float aw=U.Measure(alarm,14,true,true,.14f).x+U.Measure(sub,12).x+56;var ar=new Rect(W/2-aw/2,H-100-36,aw,36);
                U.Round(ar,on?U.Hex(0x5a0d18):U.Hex(0x2a070c),8,U.Hex(0xff3b4b));U.Text(new Rect(ar.x+12,ar.y,aw,ar.height),"☠  "+alarm,14,Color.white,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);U.Text(new Rect(ar.x,ar.y,aw-12,ar.height),sub,12,U.Hex(0xffb3ba),TextAnchor.MiddleRight);
            }
        }
    }
}
