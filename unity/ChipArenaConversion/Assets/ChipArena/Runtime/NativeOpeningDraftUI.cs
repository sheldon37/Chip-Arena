using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private NativeOpeningDraft openingRun;
        private NativeNetMessage openingView;
        // Private host-issued weapon IDs for this client; the original full-pool pick has no list.
        private string[] OpeningWeaponOffers=>openingView?.pick==(int)OpeningStep.Weapon
            ?openingView.pickCards??Array.Empty<string>():Array.Empty<string>();
        private Action openingLaunch;
        private bool applyingOpening;
        private double openingRevealUntil;
        private float nextOpeningView;
        private string highlightedWeapon;
        private Vector2 openingScroll;
        private bool BeginOpening(Action launch)
        {
            if(applyingOpening || server!=null || trainingRoom || trainingWanted || sandboxWanted)return false;
            if(openingRun!=null)return true;
            bool chips=prefs.deathmatch&&!prefs.pve&&prefs.deathmatchChips!=1;
            if(!prefs.blindPick&&(prefs.pve||!prefs.deathmatch))return false;
            bool dealt=prefs.blindPick && prefs.blindChoices!=0;
            int protocol=peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false)
                .Select(p=>p.LoadoutProtocol).DefaultIfEmpty(NativeOpeningDraft.DealtProtocol).Min();
            string refusal=NativeOpeningDraft.ProtocolRefusal(protocol,dealt);
            if(refusal!=null)
            {
                networkStatus=refusal;
                if(!dedicated)Notice(networkStatus);
                return true;
            }
            if(!prefs.blindPick&&!chips)return false;
            if(Hosting&&!GuestsReady)return true;
            var seats=MatchRoster();
            if(RefuseOpening(seats))return true;
            var rng=new SourceRandom(NativeMatch.NewSeed());
            openingRun=new NativeOpeningDraft(seats,prefs.blindPick,chips,Time.unscaledTimeAsDouble,
                p=>NativeContentRules.Offers(content,p.Weapon,new Dictionary<string,int>(),rng.Next,prefs.choices,
                    NativeArena.ImplementedChip,abilityLoadout:p.Seat.weaponTechniques??"").ToArray(),OpeningWeaponAllowed,
                dealt?prefs.blindChoices:0,dealt?content.weapons.Select(w=>w.id).ToArray():null,rng.Next);
            openingLaunch=launch;
            nextOpeningView=0;
            openingRevealUntil=0;
            OpeningTick();
            return true;
        }

        private List<NativeSeat> MatchRoster()
        {
            var seats=new List<NativeSeat>();
            if(!dedicated)
            {
                var local=SanitizedSeat(1,Profile(),account.Token);
                local.team=prefs.team;
                seats.Add(local);
            }
            seats.AddRange(peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false).Select(p=>p.Seat));
            if(applyingOpening)
                seats.AddRange(openingRun.Pilots.Values.Where(p=>p.Seat.bot).Select(p=>p.Seat));
            else if(!trainingWanted&&(!prefs.pve||!prefs.expedition&&!NativeBossBattle.Zone(bossBattlePick)))
            {
                if(!prefs.pve&&!Online&&!dedicated)prefs.bots=Math.Max(1,prefs.bots);
                seats.AddRange(LobbyBots(seats.Count));
            }
            return seats;
        }

        private bool RefuseOpening(IReadOnlyCollection<NativeSeat> seats)
        {
            string reason=prefs.pve&&prefs.expedition?ExpeditionPartyRefusal(seats):
                NativeOpeningDraft.RoomRefusal(seats,prefs.pve,prefs.teams,prefs.map);
            if(reason==null)return false;
            networkStatus=reason;
            if(!dedicated)Notice(reason);
            return true;
        }

        private void OpeningDisconnected(int id)
        {
            if(openingRun==null||!openingRun.Disconnect(id))return;
            nextOpeningView=0;
            if(openingLaunch==null||!RefuseOpening(openingRun.Pilots.Values.Select(p=>p.Seat).ToArray()))return;
            NotifyOpeningEnded(networkStatus);
            ClearOpening();
            LobbyBroadcast();
        }

        private void NotifyOpeningEnded(string reason=null)
        {
            foreach(var peer in peers)if(peer.Seat!=null&&peer.Connection?.Closed==false)
                peer.Connection.Send(NativeNetCodec.Encode(new NativeNetMessage{type="opening-end",pickSession=openingRun.Session,error=reason}));
        }

        private bool OpeningWeaponAllowed(NativeSeat seat,string id)
        {
            if(id==null||id.Length>64)return false;
            var w=content.weapons.FirstOrDefault(candidate=>candidate.id==id);
            return w!=null&&(!prefs.pve||id!="siren")&&(!w.devOnly||seat.access.Allows("weapon:"+id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        private void WithOpening(Action launch)
        {
            applyingOpening=true;
            try{launch();}
            finally{ClearOpening();}
        }

        private void ClearOpening()
        {
            openingRun=null;openingView=null;openingLaunch=null;applyingOpening=false;
            highlightedWeapon=null;openingRevealUntil=0;openingScroll=Vector2.zero;
        }

        private void ApplyOpeningWeapon(NativeSeat seat)
        {
            if(applyingOpening && openingRun.Pilots.TryGetValue(seat.id,out var p) && OpeningWeaponAllowed(seat,p.Weapon))
                seat.spec=p.Weapon;
        }

        private void ApplyOpeningChip(NativeSeat seat)
        {
            if(applyingOpening && openingRun.Pilots.TryGetValue(seat.id,out var p) &&
                seat.spec==p.Weapon && p.Chip!=null)seat.chips[p.Chip]=1;
        }

        private void OpeningTick()
        {
            if(openingRun==null||openingLaunch==null)return;
            var now=Time.unscaledTimeAsDouble;
            openingRun.Tick(now);
            if(openingRun.Refusal!=null)
            {
                networkStatus=openingRun.Refusal;
                if(!dedicated)Notice(networkStatus);
                NotifyOpeningEnded(networkStatus);ClearOpening();LobbyBroadcast();return;
            }
            if(openingRun.Ready&&openingRevealUntil==0){openingRevealUntil=now+2;nextOpeningView=0;}
            if(openingRevealUntil>0&&now>=openingRevealUntil)
            {
                var launch=openingLaunch;openingLaunch=null;openingView=null;
                NotifyOpeningEnded();
                launch();
                return;
            }
            if(Time.unscaledTime<nextOpeningView)return;
            nextOpeningView=Time.unscaledTime+.35f;
            int ready=openingRun.Pilots.Values.Count(p=>p.Step==OpeningStep.Ready);
            foreach(var p in openingRun.Pilots.Values)
            {
                if(p.Seat.bot)continue;
                var step=openingRevealUntil>0?OpeningStep.Reveal:p.Step;
                var view=new NativeNetMessage{type="opening-view",pickSession=openingRun.Session,pick=(int)step,
                    chip=p.Weapon,id=ready,team=openingRun.Pilots.Count,remainingSeconds=(float)Math.Max(0,p.Deadline-now),
                    pickCards=openingRun.CardsFor(p.Seat.id,step==OpeningStep.Reveal,id=>content.Weapon(id).name)};
                if(!dedicated&&p.Seat.id==1)AcceptOpeningView(view);
                else peers.Find(peer=>peer.Seat?.id==p.Seat.id)?.Connection?.Send(NativeNetCodec.Encode(view));
            }
        }

        private void AcceptOpeningView(NativeNetMessage view)
        {
            if(openingView?.pickSession!=view.pickSession){highlightedWeapon=view.chip;openingScroll=Vector2.zero;}
            if(openingView?.pick!=view.pick)
            {
                openingScroll=Vector2.zero;
                if(view.pick==(int)OpeningStep.Reveal&&prefs.weapon!=view.chip&&content.weapons.Any(w=>w.id==view.chip&&AllowedWeapon(w)))
                {prefs.weapon=view.chip;Save();}
            }
            openingView=view;
        }

        private bool OpeningClientMessage(NativeNetMessage m)
        {
            if(m.type=="opening-end")
            {
                if(openingView?.pickSession==m.pickSession)
                {
                    openingView=null;
                    if(!string.IsNullOrEmpty(m.error)){networkStatus=m.error;Notice(networkStatus);}
                }
                return true;
            }
            if(m.type!="opening-view")return false;
            if(m.pick>=0&&m.pick<=3&&m.id>=0&&m.team<=8&&m.id<=m.team&&m.pickSession?.Length==32&&m.pickCards?.Length<=(m.pick==(int)OpeningStep.Weapon?content.weapons.Length:12))
                AcceptOpeningView(m);
            return true;
        }

        private bool OpeningCommand(NativePeer peer,NativeNetMessage m)
        {
            if(m.type!="opening-choice")return openingRun!=null&&(m.type=="profile"||m.type=="ready"||m.type=="team");
            if(openingRun!=null&&openingLaunch!=null&&peer.Seat!=null&&m.pickSession==openingRun.Session&&
                m.chip?.Length<=64&&openingRun.Submit(peer.Seat.id,(OpeningStep)m.pick,m.chip,m.ready,Time.unscaledTimeAsDouble))
                nextOpeningView=0;
            return true;
        }

        private void SendOpeningChoice(string id,bool confirm)
        {
            var m=new NativeNetMessage{type="opening-choice",pickSession=openingView.pickSession,pick=openingView.pick,chip=id,ready=confirm};
            if(server!=null)SendServer(m);
            else if(openingRun!=null&&openingRun.Submit(1,(OpeningStep)m.pick,id,confirm,Time.unscaledTimeAsDouble))nextOpeningView=0;
        }

        // ---- Presentation (Claude, 2026-11-17; Astra's handoff of 2026-11-16) ------------------------------------------------
        // The opening's screens drawn with the chip draft's kit: the weapon pick as a grid of animated weapon cards beside a
        // hero card (NativeWeaponCard), the starting chip on the chip tray itself (LayoutTray / DrawTray), the locked-in wait
        // and the two-second group reveal, where every pilot's card flips face up in turn. They draw only the authoritative
        // view the host sends (step, locked-in count, remaining time, cards) and send the same opening-choice messages: a
        // click chooses (unconfirmed, as before, so a timeout locks the last choice), Lock in or Enter confirms. Each pilot
        // sees only their own choices until the reveal. The host sends no step start, so each beat is timed from the frame
        // this client first sees it; the countdown runs on between views from the last remaining time.
        private NativeNetMessage openingSeen;private string openingSeenSession,openingHero,openingLastLit,openingChipPick;private int openingSeenStep=-1,openingFocus=-1,openingCols=1,openingListFrame=-1;
        private float openingSeenAt,openingStepAt,openingRevealAt=-1,openingLockAt=-1,openingHeroAt=-10,openingLastLitAt=-10;
        private readonly float[] weaponHolo=new float[64],weaponHoloAt=new float[64];private float openingSelectAt=-10;private string openingSelectSeen;
        private readonly NativeHoverFocus openingHoverFocus=new NativeHoverFocus();private readonly List<ContentWeapon> openingWeapons=new List<ContentWeapon>();
        private Rect[] openingCards=Array.Empty<Rect>();private Color openingTint=NativeCyberTheme.Cyan();

        // Lookups that never throw: the views carry ids from the host, and a mismatched or unknown one must not break the frame
        // (ConversionContent.Weapon / Chip throw on an unknown id).
        private ContentWeapon OpeningWeapon(string id){if(id==null||content?.weapons==null)return null;foreach(var x in content.weapons)if(x!=null&&x.id==id)return x;return null;}
        private ContentChip OpeningChip(string id){if(id==null||content?.chips==null)return null;foreach(var x in content.chips)if(x!=null&&x.id==id)return x;return null;}

        private bool OpeningGui(float w,float h)
        {
            if(openingView==null)return false;
            var v=openingView;var step=(OpeningStep)v.pick;float now=Time.unscaledTime;OpeningTrack(v,step,now);
            var shown=step==OpeningStep.Weapon?OpeningWeapon(openingHero??highlightedWeapon??v.chip):OpeningWeapon(v.chip);
            OpeningBackdrop(w,h,shown!=null?NativeWeaponCard.Accent(shown):NativeCyberTheme.Cyan(),now);
            if(step==OpeningStep.Weapon)OpeningWeaponStep(v,w,h,now);
            else if(step==OpeningStep.Chip)OpeningChipStep(v,w,h,now);
            else if(step==OpeningStep.Ready)OpeningReadyStep(v,w,h,now);
            else OpeningRevealStep(v,w,h,now);
            return true;
        }

        // The beats this client sees: a new session clears the local state, a new view restarts the countdown's clock, a new
        // step restarts its entrance (and the card highlights).
        private void OpeningTrack(NativeNetMessage v,OpeningStep step,float now)
        {
            if(openingSeenSession!=v.pickSession)
            {
                openingSeenSession=v.pickSession;openingSeenStep=-1;openingLockAt=-1;openingRevealAt=-1;openingChipPick=null;openingHero=null;openingLastLit=null;
                openingFocus=-1;openingSelectSeen=null;openingTint=NativeCyberTheme.Cyan();
            }
            if(openingSeen!=v){openingSeen=v;openingSeenAt=now;}
            if(openingSeenStep==(int)step)return;
            if(openingSeenStep==(int)OpeningStep.Weapon&&openingLockAt<0)openingLockAt=now;
            openingSeenStep=(int)step;openingStepAt=now;draftFocus=-1;ResetHolo();openingHoverFocus.Reset();Array.Clear(weaponHolo,0,weaponHolo.Length);
            if(step==OpeningStep.Reveal)openingRevealAt=now;
        }
        private float OpeningRemaining(NativeNetMessage v)=>Mathf.Max(0,v.remainingSeconds-(Time.unscaledTime-openingSeenAt));

        // ---- the room ----
        // A deep navy room lit in the shown weapon's colour (eased as it changes), a holo floor whose lines run toward the
        // viewer, and a vignette; the lobby behind is covered, as the chip draft covers the arena.
        private void OpeningBackdrop(float W,float H,Color tint,float now)
        {
            if(Event.current.type!=EventType.Repaint)return;bool calm=prefs.reducedEffects;
            openingTint=Color.Lerp(openingTint,tint,1-Mathf.Exp(-Time.unscaledDeltaTime*6));var t=openingTint;
            U.Gradient(new Rect(0,0,W,H),U.Hex(0x0a1024),U.Hex(0x04060c));
            U.Radial(new Rect(W*.38f,-H*.35f,W*.9f,H*1.3f),U.A(t,.13f));U.Radial(new Rect(-W*.25f,-H*.4f,W*.7f,H*.9f),NativeCyberTheme.Cyan(.06f));
            // The floor: rows easing toward the viewer, and rails converging on a point above the horizon, each drawn from
            // where it crosses the horizon (no clip: IMGUI would map one through the rails' rotation).
            float horizon=H*.62f,floorH=H-horizon;float phase=calm?0:Mathf.Repeat(now*.12f,1);
            for(int i=0;i<12;i++){float k=(i+phase)/12f,y=horizon+floorH*k*k;U.Fill(new Rect(0,y,W,NativeHudChrome.Snap(1)),U.A(t,.03f+.09f*k));}
            Vector2 vp=new Vector2(W*.5f,horizon-floorH*.9f);float line=NativeHudChrome.Snap(1);
            for(int j=-9;j<=9;j++)
            {
                var foot=new Vector2(W*.5f+j*W*.09f,H);var d=foot-vp;float t0=(horizon-vp.y)/d.y;var start=vp+d*t0;float deg=-Mathf.Atan2(d.x,d.y)*Mathf.Rad2Deg,len=d.magnitude*(1-t0);
                U.Push(start,Vector2.zero,1,deg);U.Fill(new Rect(start.x-line/2,start.y,line,len),U.A(t,.07f));U.Pop();
            }
            U.Fade(new Rect(0,horizon,W,floorH*.45f),U.Hex(0x060912,.95f));
            U.Fade(new Rect(0,0,W,H*.18f),U.Hex(0x03050a,.7f));U.Fade(new Rect(0,H*.8f,W,H*.2f),U.Hex(0x03050a,.75f),false);
            U.HGradient(new Rect(0,0,W*.12f,H),U.Hex(0x03050a,.6f),Color.clear);U.HGradient(new Rect(W*.88f,0,W*.12f,H),Color.clear,U.Hex(0x03050a,.6f));
        }

        // Top right: the locked-in pips and count; under them the step's countdown (a ring that empties, red in the last five
        // seconds).
        private void OpeningMeta(NativeNetMessage v,float W,float H,bool timer,float now)
        {
            float m=H<=760?22:28,y=H<=760?16:22;int ready=Mathf.Clamp(v.id,0,8),total=Mathf.Clamp(v.team,1,8);
            string label="LOCKED IN",count=ready+" / "+total;float lw=U.Measure(label,10,true,true,.2f).x,cw=U.Measure(count,13,true,true).x,pw=total*14,bw=16+lw+12+pw+12+cw+16;
            var r=new Rect(W-m-bw,y,bw,32);NativeHudChrome.Panel(r,1,NativeCyberTheme.Cyan());
            U.Text(new Rect(r.x+16,r.y,lw+4,r.height),label,10,NativeCyberTheme.Muted(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            for(int i=0;i<total;i++)
            {
                var pip=new Rect(r.x+16+lw+12+i*14,r.center.y-4,9,9);bool on=i<ready;
                if(on){U.Glow(pip,NativeCyberTheme.Cyan(.45f),5);U.Round(pip,U.Mix(NativeCyberTheme.Cyan(),Color.white,.3f),2);}else U.Round(pip,NativeCyberTheme.Edge(.25f),2,NativeCyberTheme.Edge(.8f));
            }
            U.Text(new Rect(r.xMax-16-cw-2,r.y,cw+4,r.height),count,13,ready==total?NativeCyberTheme.Good():NativeCyberTheme.Ink(),TextAnchor.MiddleRight,bold:true,mono:true);
            if(!timer)return;
            float left=OpeningRemaining(v);int secs=Mathf.CeilToInt(left);bool urgent=left<=5;Color tone=urgent?NativeCyberTheme.Danger():NativeCyberTheme.Cyan();
            float pulse=urgent&&!prefs.reducedEffects?.5f+.5f*Mathf.Cos(Mathf.Repeat(left,1)*Mathf.PI*2):0;
            var dial=new Rect(W-m-58,r.yMax+12,58,58);U.Glow(dial,U.A(tone,.18f+.25f*pulse),10);U.Disc(dial,NativeCyberTheme.Navy(.92f));U.Pie(new Rect(dial.x+3,dial.y+3,dial.width-6,dial.height-6),U.A(tone,.85f),Mathf.Clamp01(left/20f));
            U.Disc(new Rect(dial.x+8,dial.y+8,dial.width-16,dial.height-16),U.Hex(0x070b18));
            U.Text(new Rect(dial.x,dial.y-2,dial.width,dial.height-8),secs.ToString(),20,U.Mix(NativeCyberTheme.Ink(),tone,.35f+.4f*pulse),TextAnchor.MiddleCenter,bold:true,mono:true);
            U.Text(new Rect(dial.x,dial.y+dial.height*.56f,dial.width,14),"SEC",8.5f,NativeCyberTheme.Muted(),TextAnchor.MiddleCenter,mono:true,tracking:.2f);
            string hint=urgent?"LOCKS AUTOMATICALLY":"TIME TO CHOOSE";float hw=U.Measure(hint,9.5f,true,true,.16f).x;
            U.Text(new Rect(dial.x-hw-12,dial.y,hw+4,dial.height),hint,9.5f,U.A(tone,.9f),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);
        }
        // Bottom left: the key hints, as keycaps.
        private void OpeningKeys(float H,params string[] pairs)
        {
            float m=H<=760?22:28,x=m,y=H-m-24;
            for(int i=0;i+1<pairs.Length;i+=2)
            {
                float kw=NativeHudChrome.KeyWidth(pairs[i],22);NativeHudChrome.Key(new Rect(x,y,kw,22),pairs[i]);x+=kw+8;
                float tw=U.Measure(pairs[i+1],12).x;U.Text(new Rect(x,y,tw+4,22),pairs[i+1],12,NativeCyberTheme.Muted(),TextAnchor.MiddleLeft);x+=tw+22;
            }
        }

        // ---- weapon ----
        private static int WeaponRank(ContentWeapon w)=>w.devOnly?3:w.@class=="ranged"?0:w.@class=="magic"?1:w.@class=="melee"?2:3;
        // The pilot's weapons (the menus' allowance), by class (Ranged, Magic, Melee, DEV), content order within.
        private void OpeningWeaponList()
        {
            if(openingListFrame==Time.frameCount)return;openingListFrame=Time.frameCount;openingWeapons.Clear();
            var offers=OpeningWeaponOffers;
            for(int rank=0;rank<4;rank++)foreach(var x in content.weapons)
                if(x!=null&&WeaponRank(x)==rank&&(offers.Length>0?Array.IndexOf(offers,x.id)>=0:AllowedWeapon(x)))openingWeapons.Add(x);
        }
        // The grid in `area`: the largest cards (to 168 wide) whose rows fit, centred on the area's height.
        private void OpeningGrid(Rect area)
        {
            int n=openingWeapons.Count;if(openingCards.Length!=n)openingCards=new Rect[n];if(n==0)return;const float gap=14;float cw=0,ch=0;int cols=0;
            for(int c=3;c<=12;c++)
            {
                float w=Mathf.Min(168,(area.width-(c-1)*gap)/c),h=w*1.24f;int rows=(n+c-1)/c;float total=rows*h+(rows-1)*gap;
                if(total<=area.height||c==12){cols=c;cw=w;ch=h;if(total>area.height){float k=(area.height-(rows-1)*gap)/(rows*h);cw*=k;ch*=k;}break;}
            }
            openingCols=cols;int rowsN=(n+cols-1)/cols;float gridH=rowsN*ch+(rowsN-1)*gap,gridW=cols*cw+(cols-1)*gap,x0=area.x+Mathf.Max(0,(area.width-gridW)*.5f),y0=area.y+Mathf.Max(0,(area.height-gridH)*.42f);
            for(int i=0;i<n;i++){int col=i%cols,row=i/cols;openingCards[i]=new Rect(x0+col*(cw+gap),y0+row*(ch+gap),cw,ch);}
        }
        private void OpeningWeaponStep(NativeNetMessage v,float W,float H,float now)
        {
            OpeningWeaponList();float m=H<=760?22:28,age=now-openingStepAt;bool calm=prefs.reducedEffects,repaint=Event.current.type==EventType.Repaint;
            DraftHeader("BLIND PICK  ·  WEAPON",NativeCyberTheme.Cyan(),"Pick your weapon","Your pick stays hidden until every pilot locks in.",W,H,out float top);
            OpeningMeta(v,W,H,true,now);
            float hw=Mathf.Clamp(W*.26f,280,400),hx=W-m-hw,heroTop=Mathf.Max(top,H<=760?118:136);
            OpeningGrid(new Rect(m,top+8,hx-m-40,H-m-44-(top+8)));
            string chosen=highlightedWeapon??v.chip;if(openingSelectSeen!=chosen){if(openingSelectSeen!=null)openingSelectAt=now;openingSelectSeen=chosen;}
            int n=Math.Min(openingWeapons.Count,weaponHolo.Length),under=-1;
            for(int i=0;i<n;i++)if(age-i*.035f>.2f&&U.Hover(openingCards[i]))under=i;
            int lit=under>=0?under:openingFocus>=0&&openingFocus<n?openingFocus:-1;
            if(repaint&&openingHoverFocus.Step(lit,lit>=0?openingWeapons[lit].id:null,true))nativeAudio?.UiCue("ui.draft.hover",null,.5f);
            // The hero follows the highlighted card, holds it a moment across a gap, then returns to the choice.
            if(lit>=0){openingLastLit=openingWeapons[lit].id;openingLastLitAt=now;}
            string hero=lit>=0?openingWeapons[lit].id:now-openingLastLitAt<.3f&&openingLastLit!=null?openingLastLit:chosen;
            if(hero!=openingHero){openingHero=hero;openingHeroAt=now;}
            float first=0,second=0;int top1=-1;
            for(int i=0;i<n;i++)
            {
                if(repaint){float target=NativeHoloScan.Target(lit==i,false);if(NativeHoloScan.Engages(weaponHolo[i],target))weaponHoloAt[i]=now;weaponHolo[i]=NativeHoloScan.Step(weaponHolo[i],target,Time.unscaledDeltaTime);}
                float e=weaponHolo[i];if(e>first){second=first;first=e;top1=i;}else if(e>second)second=e;
            }
            for(int i=0;i<n;i++)
            {
                var x=openingWeapons[i];float build=calm?1:M.Clamp01((age-i*.035f)/.4f),lv=weaponHolo[i];bool sel=x.id==chosen;
                var look=new NativeWeaponCard.Look{Alpha=calm?1:M.Clamp01(build*3),Build=build,Hover=lv,HoverAge=now-weaponHoloAt[i],Selected=sel?1:0,SelectAge=sel?now-openingSelectAt:-10,
                    Recede=NativeHoloScan.Recede(lv,i==top1?second:first)*.8f,Time=now,Index=i,Calm=calm,LockAge=-10};
                var r=openingCards[i];NativeWeaponCard.Draw(new Rect(r.x,r.y+NativeHoloScan.Lift(lv)*.6f+(1-build)*12,r.width,r.height),x,look);
                if(build>.5f&&U.SilentButton(r)){openingFocus=i;OpeningChoose(x.id,false);}
            }
            OpeningGridKeys(n);
            // The hero card and Lock in.
            var shown=OpeningWeapon(openingHero??chosen);float heroH=Mathf.Min(hw*1.48f,H-m-74-heroTop);var heroR=new Rect(hx,heroTop,hw,heroH);
            float heroIn=calm?1:M.Bezier(.16f,.8f,.22f,1,M.Clamp01((age-.1f)/.45f));
            if(shown!=null)
            {
                U.Push(heroR.center,new Vector2(28*(1-heroIn),0),1);
                var look=NativeWeaponCard.Look.Idle(now);look.Alpha=heroIn;look.Selected=shown.id==chosen?1:.5f;look.SelectAge=shown.id==chosen?now-openingSelectAt:-10;look.Calm=calm;
                NativeWeaponCard.Hero(heroR,shown,look,NativeWeaponCard.Measure(shown,openingWeapons),now-openingHeroAt);
                string tag=shown.id==chosen?"YOUR CHOICE":"PREVIEW";float tw=U.Measure(tag,9.5f,true,true,.2f).x+18;var tr=new Rect(heroR.xMax-tw-14,heroR.y-11,tw,22);
                NativeHudChrome.Tag(tr,shown.id==chosen?NativeWeaponCard.Accent(shown):NativeCyberTheme.Muted());U.Text(tr,tag,9.5f,shown.id==chosen?NativeCyberTheme.Ink():NativeCyberTheme.Muted(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f);
                U.Pop();
            }
            var pick=OpeningWeapon(chosen);var btn=new Rect(hx,heroR.yMax+16,hw,52);
            if(OpeningLockButton(btn,pick,heroIn,now)||OpeningConfirmKey())OpeningLockIn(chosen,now);
            OpeningKeys(H,"← → ↑ ↓","Browse","CLICK","Choose","ENTER","Lock in");
        }
        // Arrow keys move the focus through the grid (rows by the grid's columns) and choose the focused weapon; Enter locks in.
        private void OpeningGridKeys(int n)
        {
            var e=Event.current;if(n==0||e.type!=EventType.KeyDown)return;int at=openingFocus>=0?openingFocus:Math.Max(0,openingWeapons.FindIndex(x=>x.id==(highlightedWeapon??openingView?.chip))),next=at;
            if(e.keyCode==KeyCode.RightArrow)next=(at+1)%n;else if(e.keyCode==KeyCode.LeftArrow)next=(at+n-1)%n;
            else if(e.keyCode==KeyCode.DownArrow)next=Math.Min(n-1,at+openingCols);else if(e.keyCode==KeyCode.UpArrow)next=Math.Max(0,at-openingCols);else return;
            e.Use();openingFocus=next;if(openingWeapons[next].id!=highlightedWeapon)OpeningChoose(openingWeapons[next].id,false);
        }
        private bool OpeningConfirmKey(){var e=Event.current;if(e.type!=EventType.KeyDown||e.keyCode!=KeyCode.Return&&e.keyCode!=KeyCode.KeypadEnter)return false;e.Use();return true;}
        private void OpeningChoose(string id,bool confirm)
        {
            if(openingView==null||id==null)return;if(!confirm){if(highlightedWeapon!=id)nativeAudio?.UiCue("ui.loadout.select","install",.4f);highlightedWeapon=id;}SendOpeningChoice(id,confirm);
        }
        private void OpeningLockIn(string id,float now)
        {
            if(id==null||openingLockAt>=0)return;openingLockAt=now;nativeAudio?.UiCue("ui.draft.pick","ui",.4f);OpeningChoose(id,true);
        }
        // Lock in: a bar in the chosen weapon's colour with its name and the Enter keycap; a sheen crosses it, it lifts on hover.
        private bool OpeningLockButton(Rect r,ContentWeapon pick,float alpha,float now)
        {
            if(pick==null||alpha<=.01f)return false;bool calm=prefs.reducedEffects;var acc=NativeWeaponCard.Accent(pick);float h=U.HoverAmount(0x51a7e,r);var d=new Rect(r.x,r.y-2*h,r.width,r.height);
            if(Event.current.type==EventType.Repaint)
            {
                U.Glow(new Rect(d.x,d.y+10,d.width,d.height),U.A(acc,(.25f+.25f*h)*alpha),18);U.RoundGradient(d,U.A(U.Mix(acc,Color.white,.15f+.15f*h),alpha),U.A(U.Mix(acc,Color.black,.35f),alpha),6,U.A(U.Mix(acc,Color.white,.5f),alpha));
                if(!calm){float p=Mathf.Repeat(now/3.2f,1);if(p<.4f){float sx=d.x+d.width*(p/.4f);U.BeginBand(d);U.HGradient(new Rect(sx-40,d.y,40,d.height),new Color(1,1,1,0),new Color(1,1,1,.28f*alpha));U.HGradient(new Rect(sx,d.y,40,d.height),new Color(1,1,1,.28f*alpha),new Color(1,1,1,0));U.EndBand();}}
                var ink=U.Hex(0x05070d,alpha);U.Text(new Rect(d.x+18,d.y,d.width-90,d.height*.55f+4),"LOCK IN",16,ink,TextAnchor.LowerLeft,bold:true,tracking:.18f);
                U.Text(new Rect(d.x+18,d.y+d.height*.55f,d.width-90,d.height*.4f),pick.name.ToUpperInvariant(),10.5f,U.A(ink,.75f),TextAnchor.UpperLeft,bold:true,mono:true,tracking:.16f);
                float kw=NativeHudChrome.KeyWidth("ENTER",22);NativeHudChrome.Key(new Rect(d.xMax-16-kw,d.center.y-11,kw,22),"ENTER",alpha,U.Hex(0x05070d));
            }
            return U.SilentButton(r);
        }

        // ---- starting chip ----
        // The chip tray as the draft shows it (boot in reading order, the holo highlight, 1-9 and arrows, Enter), with the
        // locked weapon as a tag; a pick confirms at once.
        private Tray openingTray;
        private void OpeningChipStep(NativeNetMessage v,float W,float H,float now)
        {
            float m=H<=760?22:28,age=now-openingStepAt;var chips=new List<ContentChip>();foreach(var id in v.pickCards??Array.Empty<string>()){var c=OpeningChip(id);if(c!=null)chips.Add(c);}
            DraftHeader("BLIND PICK  ·  STARTING CHIP",NativeCyberTheme.Cyan(),"Pick your starting chip","One Chip to begin the match with. Only you see these choices.",W,H,out float top);
            OpeningMeta(v,W,H,true,now);
            openingTray=LayoutTray(chips,top,W,H);LowerTray(openingTray,top,H-m-92);if(chips.Count==0)return;
            int pick=DrawTray(openingTray,chips,i=>0,age,openingChipPick==null,-1,i=>false,true);int key=DraftKeys(chips.Count,openingChipPick==null,true);if(key>=0)pick=key;
            if(pick>=0&&openingChipPick==null){openingChipPick=chips[pick].id;nativeAudio?.UiCue("ui.draft.pick","ui",.4f);SendOpeningChoice(chips[pick].id,true);}
            OpeningWeaponTag(OpeningWeapon(v.chip),m,H-m-74,now);
            OpeningKeys(H,"1–"+Mathf.Clamp(chips.Count,1,9),"Pick","← →","Browse","ENTER","Install");
        }
        // The locked weapon, as a small tag: its icon on its colour, YOUR WEAPON and its name.
        private void OpeningWeaponTag(ContentWeapon x,float left,float y,float now)
        {
            if(x==null)return;var acc=NativeWeaponCard.Accent(x);string name=x.name.ToUpperInvariant();float nw=U.Measure(name,13,true).x,w=12+30+10+Mathf.Max(nw,U.Measure("YOUR WEAPON",9,true,true,.2f).x)+18;var r=new Rect(left,y,w,40);
            NativeHudChrome.Panel(r,1,acc);var ic=new Rect(r.x+12,r.y+6,28,28);U.Radial(new Rect(ic.x-10,ic.y-10,48,48),U.A(acc,.25f));NativeIcons.Draw(ic,NativeIcons.Spec(x.id),U.Mix(acc,Color.white,.35f));
            U.Text(new Rect(ic.xMax+10,r.y+5,w,13),"YOUR WEAPON",9,NativeCyberTheme.Muted(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);U.Text(new Rect(ic.xMax+10,r.y+18,w,18),name,13,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,bold:true);
        }

        // ---- locked in ----
        // The pilot's own card, stamped, with the installed chip beside it when one was picked here, over the wait's status.
        private void OpeningReadyStep(NativeNetMessage v,float W,float H,float now)
        {
            float age=now-openingStepAt;bool calm=prefs.reducedEffects;int ready=Mathf.Clamp(v.id,0,8),total=Mathf.Clamp(v.team,1,8),waiting=Math.Max(0,total-ready);
            DraftHeader("BLIND PICK  ·  LOCKED IN",NativeCyberTheme.Good(),waiting>0?"Locked in":"Everyone is locked in",waiting>0?"Waiting for the other pilots. Your choices stay hidden until the reveal.":"Revealing every pilot's weapon.",W,H,out float top);
            OpeningMeta(v,W,H,false,now);
            var x=OpeningWeapon(v.chip);var chip=openingChipPick!=null?OpeningChip(openingChipPick):null;
            float cw=Mathf.Clamp(W*.22f,260,340),ch=Mathf.Min(cw*1.48f,H-top-150),chipW=chip!=null?Mathf.Min(240,cw*.78f):0,gap=chip!=null?44:0,total2=cw+gap+chipW,x0=(W-total2)/2,y0=top+Mathf.Max(0,(H-top-120-ch)*.45f);
            float enter=calm?1:M.Bezier(.16f,.8f,.22f,1,M.Clamp01(age/.5f));
            if(x!=null)
            {
                var r=new Rect(x0,y0,cw,ch);U.Push(r.center,new Vector2(0,24*(1-enter)),.94f+.06f*enter);
                var look=NativeWeaponCard.Look.Idle(now);look.Alpha=enter;look.Selected=1;look.SelectAge=age;look.Locked=1;look.LockAge=openingLockAt>=0?now-openingLockAt:age;look.Calm=calm;
                NativeWeaponCard.Hero(r,x,look,NativeWeaponCard.Measure(x,content.weapons),10);U.Pop();
            }
            if(chip!=null)
            {
                var size=ChipSize.Tray(false,3);float h=NativeChipCard.Height(chip,chipW,size);var r=new Rect(x0+cw+gap,y0+(ch-h)*.5f,chipW,h);float ce=calm?1:M.Bezier(.16f,.8f,.22f,1,M.Clamp01((age-.15f)/.5f));
                U.Text(new Rect(r.x,r.y-28,r.width,16),"STARTING CHIP",10,NativeCyberTheme.Muted(ce),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.24f);
                U.Push(r.center,new Vector2(0,24*(1-ce)),.94f+.06f*ce);U.Glow(r,U.A(NativeChipCard.Rarity(chip.rarity),.25f*ce),30);var look=ChipLook.Idle(now);look.Alpha=ce;look.Chosen=true;look.Calm=calm;NativeChipCard.Draw(r,chip,0,size,look);U.Pop();
            }
            // The wait: a pip per pilot and the words, a slow comet round the row while anyone is still choosing.
            float py=H-(H<=760?70:86);string status=waiting>0?"WAITING FOR "+waiting+(waiting==1?" PILOT":" PILOTS"):"ALL PILOTS LOCKED IN";float sw=U.Measure(status,12,true,true,.24f).x;
            U.Text(new Rect(W/2-sw/2-20,py,sw+40,20),status,12,waiting>0?NativeCyberTheme.Ink(enter):NativeCyberTheme.Good(enter),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.24f);
            float pw=total*22-8;for(int i=0;i<total;i++){var pip=new Rect(W/2-pw/2+i*22,py+30,14,6);bool on=i<ready;if(on){U.Glow(pip,NativeCyberTheme.Cyan(.4f*enter),5);U.Fill(pip,U.A(U.Mix(NativeCyberTheme.Cyan(),Color.white,.3f),enter));}else{float b=calm?.4f:.25f+.25f*Mathf.Sin(now*4-i);U.Fill(pip,NativeCyberTheme.Edge(b*enter));}}
        }

        // ---- reveal ----
        // Two seconds: every pilot's card turns face up in turn (bots too), named under it in their colour; a light sweeps the
        // row as each lands. Rows are "name · weapon" as the host sends them.
        private void OpeningRevealStep(NativeNetMessage v,float W,float H,float now)
        {
            float age=now-(openingRevealAt>=0?openingRevealAt:openingStepAt);bool calm=prefs.reducedEffects;var rows=v.pickCards??Array.Empty<string>();int n=Math.Min(rows.Length,8);
            DraftHeader("BLIND PICK  ·  REVEAL",NativeCyberTheme.Magenta(),"Weapons revealed","",W,H,out float top);
            if(n==0)return;int perRow=n<=5?n:(n+1)/2,lines=(n+perRow-1)/perRow;float gap=26,avail=H-top-70,cw=Mathf.Min(196,(W-120-(perRow-1)*gap)/perRow),ch=cw*1.26f;
            if(lines*(ch+46)>avail){float k=avail/(lines*(ch+46));cw*=k;ch*=k;}
            float blockH=lines*(ch+46)-12,y0=top+Mathf.Max(0,(avail-blockH)*.45f);
            for(int i=0;i<n;i++)
            {
                SplitReveal(rows[i],out string pilot,out var weapon);int row=i/perRow,col=i%perRow,inRow=Math.Min(perRow,n-row*perRow);
                var r=new Rect(W/2-(inRow*cw+(inRow-1)*gap)/2+col*(cw+gap),y0+row*(ch+46),cw,ch);
                float flipAt=.12f+i*Mathf.Min(.14f,1.1f/n),f=calm?1:M.Clamp01((age-flipAt)/.34f),enter=calm?1:M.Clamp01((age-i*.04f)/.25f);
                var seat=OpeningRosterColor(pilot,out bool you);var acc=weapon!=null?NativeWeaponCard.Accent(weapon):NativeCyberTheme.Cyan();
                float sx=Mathf.Abs(Mathf.Cos(f*Mathf.PI)),lift=Mathf.Sin(f*Mathf.PI)*-14;
                U.Push(r.center,new Vector2(0,lift+(1-enter)*20),Mathf.Max(.02f,sx),0,1+.06f*Mathf.Sin(f*Mathf.PI));
                if(f<.5f)NativeWeaponCard.Back(r,enter,now,you?NativeCyberTheme.Cyan():NativeCyberTheme.Violet(),calm);
                else if(weapon!=null){var look=NativeWeaponCard.Look.Idle(now);look.Selected=1;look.SelectAge=age-flipAt-.17f;look.Index=i;look.Calm=calm;NativeWeaponCard.Draw(r,weapon,look);}
                U.Pop();
                // The landing: a flash on the card and a sweep across the row.
                float land=calm?0:1-M.Clamp01((age-flipAt-.17f)/.35f);if(land>0&&age>=flipAt+.17f){U.Glow(r,U.A(acc,.45f*land),24);U.Round(r,new Color(1,1,1,.18f*land),8);}
                var plate=new Rect(r.x-10,r.yMax+14,r.width+20,26);float pa=calm?1:M.Clamp01((age-flipAt-.1f)/.3f);
                U.Pill(plate,U.Hex(0x0d0f16,.85f*pa),U.A(you?NativeCyberTheme.Cyan():new Color(1,1,1,.14f),pa));var dot=new Rect(plate.x+11,plate.center.y-4,8,8);U.Glow(dot,U.A(seat,.6f*pa),6);U.Round(dot,U.A(seat,pa),4);
                string label=pilot+(you?"  ·  YOU":"");U.Text(new Rect(dot.xMax+6,plate.y,plate.width-34,plate.height),label,12,U.A(NativeCyberTheme.Ink(),pa),TextAnchor.MiddleLeft,bold:you);
            }
            if(!calm){float s=M.Clamp01(age/1.6f);var band=new Rect(-W*.3f+W*1.6f*s,top,W*.3f,H-top);U.HGradient(new Rect(band.x,band.y,band.width/2,band.height),Color.clear,new Color(1,1,1,.03f*(1-s)));U.HGradient(new Rect(band.center.x,band.y,band.width/2,band.height),new Color(1,1,1,.03f*(1-s)),Color.clear);}
            string go="DEPLOYING";float gw=U.Measure(go,11,true,true,.4f).x;U.Text(new Rect(W/2-gw/2-20,H-(H<=760?44:56),gw+40,18),go,11,NativeCyberTheme.Muted(calm?1:.6f+.4f*Mathf.Sin(now*5)),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.4f);
        }
        private void SplitReveal(string row,out string pilot,out ContentWeapon weapon)
        {
            const string sep="   ·   ";int at=row?.LastIndexOf(sep,StringComparison.Ordinal)??-1;pilot=at>=0?row.Substring(0,at):row??"";weapon=null;
            if(at<0)return;string name=row.Substring(at+sep.Length);foreach(var x in content.weapons)if(x!=null&&x.name==name){weapon=x;break;}
        }
        // The pilot's colour from the room lobby's roster (the reveal names pilots only); a pale ink when they are not in it.
        private Color OpeningRosterColor(string name,out bool you)
        {
            foreach(var p in lobbyRoster)if(p.name==name){you=p.you;return p.accent.a>0?p.accent:U.Hex(0xdce7f5);}
            you=name==prefs.name;return U.Hex(0xdce7f5);
        }
    }
}
