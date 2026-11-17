using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private void RespawnDeathmatchPilots()
        {
            foreach (var p in Pilots)
            {
                var seat = p.Seat;
                if (!seat.connected) continue;
                if (seat.bot && seat.offers.Count > 0)
                    InstallDeathmatchChip(seat.id, seat.pickTicket, seat.offers[0]);
                if (!Match.TryDeathmatchRespawn(seat)) continue;
                int index = Match.Seats.IndexOf(seat);
                var spawn = NativeMapRules.Spawn(Content, Map, Match.Seats, index, Match.Teams);
                RevivePve(p, Vec(spawn.pos), true);
                p.Death = null;
                p.LastAttacker = null;
                p.Yaw = spawn.yaw;
                p.Mana = (float)p.Stats["maxMana"];
                p.Stamina = (float)p.Stats["maxStamina"];
                p.Ammo = Magazine(p);
                p.ECharges = NativeAbilityRules.Charges(p.Stats);
            }
        }

#if UNITY_EDITOR
        // Called in the native builder with two isolated arenas, including replica serialization.
        public void ValidateDeathmatchReplica(NativeArena guest)
        {
            Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,deathmatchTarget=4});
            Match.ComebackChips=true;
            var killer=Pilots[0];var victim=Pilots[1];
            void Check(bool ok,string name){if(!ok)throw new Exception("Deathmatch native gate: "+name);}
            void Deliver()
            {
                var wire=NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=1,frame=NetworkFrame()});
                var frame=NativeNetCodec.Decode(wire).frame;
                Check(frame.Valid(Content),"valid serialized match");
                guest.ApplyFrame(frame,victim.Seat.id);
            }
            for(int i=1;i<=4;i++)
            {
                Damage(victim,killer,victim.Hp+1000,Vector3.zero,0,false,forced:true);
                Deliver();
                Check(guest.Match.DeathmatchTarget==4&&guest.Match.Seat(killer.Seat.id).score==i,"replicated target and score");
                if(i==4)break;
                for(int t=0;t<12;t++)Match.Tick(.25);
                RespawnDeathmatchPilots();Deliver();
                Check(guest.Local.Hp>0&&guest.Local.Seat.alive&&guest.Local.Death==null,"respawn clears replica corpse");
                if(i==3)
                {
                    var seat=guest.Local.Seat;
                    Check(seat.offers.Count>0&&InstallDeathmatchChip(seat.id,seat.pickTicket,seat.offers[0]),"deferred chip");
                    Deliver();Check(guest.Local.Seat.chips.Count==1,"replica receives chip");
                }
            }
            Check(guest.Match.Phase==NativePhase.Result&&guest.Match.FinalVictim==victim.Seat.id,"final replay victim");
        }
#endif

        public bool InstallDeathmatchChip(int seatId, int ticket, string chip)
        {
            var p = Pilots.Find(candidate => candidate.Seat.id == seatId);
            if (p == null || !Match.TakeComebackChip(p.Seat, ticket, chip)) return false;
            RefreshLiveStats(p);
            Emit(EventKind.Install, p, seatId, p.Body.position, p.Seat.chips[chip], chip);
            return true;
        }
    }

    public sealed partial class ConversionGame
    {
        private bool deathPickOpen, deathPickWasDead;
        private NativeMatch deathPickMatch;
        private int deathPickTicket = -1;

        private void DeathmatchPickKeys()
        {
            var seat = arena?.Local?.Seat;
            if (match != deathPickMatch || match?.Phase != NativePhase.Fight ||
                match.DeathmatchTarget <= 0 || seat == null || seat.offers.Count == 0)
            {
                deathPickOpen = false;
                deathPickTicket = -1;
                deathPickMatch = match;
                return;
            }
            if (deathPickWasDead && seat.alive) deathPickOpen=false;
            deathPickWasDead=!seat.alive;
            if (deathPickTicket != seat.pickTicket)
            {
                deathPickOpen = !seat.alive;
                deathPickTicket = seat.pickTicket;
            }
            if (!menu && (Input.GetKeyDown(KeyCode.C) || PadDown(PadAction.Chips))) deathPickOpen = !deathPickOpen;
        }

        // ---- Presentation (Claude, 2026-11-17; Astra's handoff of 2026-11-16) ------------------------------------------------
        // The respawn countdown, the comeback chip's prompt and its picker on the chip draft's tray, and the score panel. They
        // read the replicated seat (alive, respawnAt, offers, pickTicket, score) and send the same install as before; nothing
        // here waits on presentation, so the respawn keeps the rules' time. The countdown's ring spans NativeDeathmatch's
        // three seconds.
        private const float DeathmatchRespawnSeconds=3;
        private bool deathPickWasOpen,deathWasAlive=true;private float deathPickOpenedAt=-10,deathDiedAt=-10,deathOfferAt=-10;private int deathOfferTicket=-1;private Tray deathTray;
        private readonly Dictionary<int,int> dmScoreSeen=new Dictionary<int,int>();private readonly Dictionary<int,float> dmScoreAt=new Dictionary<int,float>();private NativeMatch dmScoreMatch;

        private bool DeathmatchPickGui(float w, float h)
        {
            if (menu || arena?.Local == null || match.DeathmatchTarget <= 0 || match.Phase != NativePhase.Fight) return false;
            var seat = arena.Local.Seat;float now=Time.unscaledTime;
            if(deathWasAlive&&!seat.alive)deathDiedAt=now;deathWasAlive=seat.alive;
            if(seat.offers.Count>0&&deathOfferTicket!=seat.pickTicket){deathOfferTicket=seat.pickTicket;deathOfferAt=now;}
            bool picking=deathPickOpen&&seat.offers.Count>0;
            if(picking&&!deathPickWasOpen){deathPickOpenedAt=now;draftFocus=-1;ResetHolo();}deathPickWasOpen=picking;
            if(picking)ComebackPicker(seat,w,h,now);
            if(!seat.alive&&seat.respawnAt>=0)RespawnCountdown(seat,w,h,now,picking);
            if(!picking&&seat.offers.Count>0)ComebackPrompt(w,h,now);
            return picking;
        }

        // Eliminated: a red edge that fades, ELIMINATED (with the event glitch), the seconds in a draining ring and a bar. While
        // the picker is open it sits small over the tray's header.
        private void RespawnCountdown(NativeSeat seat,float W,float H,float now,bool compact)
        {
            if(Event.current.type!=EventType.Repaint)return;bool calm=prefs.reducedEffects;float left=Mathf.Max(0,(float)(seat.respawnAt-match.Time)),k=Mathf.Clamp01(left/DeathmatchRespawnSeconds),age=now-deathDiedAt;
            var red=NativeCyberTheme.Danger();
            if(!compact)
            {
                float edge=calm?.35f:Mathf.Lerp(.75f,.3f,M.Clamp01(age/.8f));U.Glow(new Rect(-40,-40,W+80,H+80),U.A(red,.35f*edge),-70);
                var c=new Vector2(W/2,H*.3f);float enter=calm?1:M.Bezier(.16f,.8f,.22f,1,M.Clamp01(age/.35f));
                U.Push(c,new Vector2(0,-14*(1-enter)),.9f+.1f*enter);
                var eyebrow=new Rect(c.x-200,c.y-92,400,22);NativeHudChrome.Glitch(eyebrow,"ELIMINATED",15,age,enter,TextAnchor.MiddleCenter,calm,true,true,.42f);
                U.Text(eyebrow,"ELIMINATED",15,U.A(U.Mix(red,Color.white,.15f),enter),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.42f,glow:U.A(red,.4f*enter),shadow:new Color(0,0,0,.7f*enter));
                var dial=new Rect(c.x-46,c.y-56,92,92);U.Glow(dial,U.A(red,.22f*enter),16);U.Disc(dial,NativeCyberTheme.Void(.8f*enter));U.Pie(new Rect(dial.x+4,dial.y+4,dial.width-8,dial.height-8),U.A(NativeCyberTheme.Cyan(),.85f*enter),k);
                U.Disc(new Rect(dial.x+11,dial.y+11,dial.width-22,dial.height-22),U.Hex(0x070b18,enter));
                int secs=Mathf.CeilToInt(left);float pop=calm?0:1-M.Clamp01((Mathf.Ceil(left)-left)/.18f);
                U.Push(dial.center,Vector2.zero,1+.12f*pop);U.Text(new Rect(dial.x,dial.y-3,dial.width,dial.height),secs.ToString(),34,U.A(NativeCyberTheme.Ink(),enter),TextAnchor.MiddleCenter,bold:true,mono:true);U.Pop();
                U.Text(new Rect(c.x-150,dial.yMax+10,300,16),"RESPAWNING",11,NativeCyberTheme.Muted(enter),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.36f);
                var bar=new Rect(c.x-110,dial.yMax+34,220,NativeHudChrome.Snap(2));NativeHudChrome.Line(bar,1-k,NativeCyberTheme.Cyan(.9f*enter),NativeCyberTheme.Edge(.5f*enter));
                U.Pop();
            }
            else
            {
                string text="RESPAWNING IN "+Mathf.CeilToInt(left);float tw=U.Measure(text,11,true,true,.24f).x+40;var r=new Rect(W/2-tw/2,H<=760?16:22,tw,28);
                NativeHudChrome.Panel(r,1,red);U.Pie(new Rect(r.x+10,r.center.y-6,12,12),NativeCyberTheme.Cyan(.9f),k);U.Text(new Rect(r.x+28,r.y,tw-34,r.height),text,11,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);
            }
        }

        // The prompt while a comeback chip waits: a chamfered tag with a chip glyph and the C keycap; it flares magenta as an
        // offer arrives and breathes until opened.
        private void ComebackPrompt(float W,float H,float now)
        {
            if(Event.current.type!=EventType.Repaint)return;bool calm=prefs.reducedEffects;float age=now-deathOfferAt,enter=calm?1:M.Bezier(.16f,.8f,.22f,1,M.Clamp01(age/.4f));
            string title="COMEBACK CHIP READY",sub="Open chips";float tw=U.Measure(title,12,true,true,.18f).x,kw=NativeHudChrome.KeyWidth("C",22),w=14+26+12+tw+18+kw+14;
            var r=new Rect(22-(1-enter)*30,H-145,w,44);var tone=NativeCyberTheme.Magenta();
            NativeHudChrome.Flare(r,age,enter,calm,true);NativeHudChrome.Panel(r,enter,tone);NativeHudChrome.Flare(r,age,enter,calm,false);
            float b=calm?1:.75f+.25f*Mathf.Sin(now*3.2f);var ic=new Rect(r.x+14,r.center.y-13,26,26);U.Radial(new Rect(ic.x-12,ic.y-12,50,50),U.A(tone,.3f*b*enter));
            NativeIcons.Draw(ic,NativeIcons.Has("chip")?"chip":"shuffle",U.A(U.Mix(tone,Color.white,.35f),enter));
            U.Text(new Rect(ic.xMax+12,r.y+6,tw+4,16),title,12,U.A(NativeCyberTheme.Ink(),enter),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            U.Text(new Rect(ic.xMax+12,r.y+23,tw+4,15),sub,11.5f,NativeCyberTheme.Muted(enter),TextAnchor.MiddleLeft);
            NativeHudChrome.Key(new Rect(r.xMax-14-kw,r.center.y-11,kw,22),"C",enter,U.Mix(tone,Color.white,.4f));
        }

        // The picker: the arena stays in view behind a deepening shade; the offers boot onto the chip tray as in the draft (the
        // holo highlight, 1-9 and the arrows, Enter), and Choose later closes it (C again opens it).
        private void ComebackPicker(NativeSeat seat,float W,float H,float now)
        {
            float m=H<=760?22:28,age=now-deathPickOpenedAt;bool calm=prefs.reducedEffects;var chips=new List<ContentChip>();foreach(var id in seat.offers){var c=OpeningChip(id);if(c!=null)chips.Add(c);}
            float shade=calm?1:M.Clamp01(age/.25f);U.Gradient(new Rect(0,0,W,H),U.Hex(0x05070d,.35f*shade),U.Hex(0x05070d,.9f*shade));
            DraftHeader("DEATHMATCH  ·  COMEBACK CHIP",NativeCyberTheme.Magenta(),"Choose a comeback chip","Respawn continues while you choose. The fight goes on around you.",W,H,out float top);
            if(chips.Count==0)return;deathTray=LayoutTray(chips,top,W,H);LowerTray(deathTray,top,H-m-80);
            int pick=DrawTray(deathTray,chips,i=>seat.chips.TryGetValue(chips[i].id,out int n)?n:0,age,true,-1,i=>false,true);int key=DraftKeys(chips.Count,true,true);if(key>=0)pick=key;
            if(pick>=0)
            {
                string id=chips[pick].id;nativeAudio?.UiCue("ui.draft.pick","ui",.4f);
                if (server == null) arena.InstallDeathmatchChip(seat.id,seat.pickTicket,id);
                else SendServer(new NativeNetMessage{type="dm-chip",chip=id,pick=seat.pickTicket});
                deathPickOpen=false;return;
            }
            string how="Press 1–"+Mathf.Clamp(chips.Count,1,9)+" or click a chip";float hw=U.Measure(how,12,false,true).x,bw=180,fw=hw+12+bw+24;var foot=new Rect(W-m-fw,H-m-56,fw,56);U.Round(foot,U.Hex(0x0d0f16,.9f),12);
            U.Text(new Rect(foot.x+12,foot.y,hw+4,foot.height),how,12,U.Hex(0x9799a6),TextAnchor.MiddleLeft,mono:true);
            if(U.Btn(new Rect(foot.xMax-12-bw,foot.y+8,bw,40),"Choose later  C",false,true,1,14))deathPickOpen=false;
        }

        // The score panel (top right, in MatchOverview's place): FIRST TO n; each side's colour, name and kills with a bar
        // toward the target, the leader marked; a side's row flashes and its count pops as it scores.
        private void DeathmatchScores(float x,float y,float w,List<NativeSeat> sides)
        {
            float now=Time.unscaledTime;bool calm=prefs.reducedEffects;var me=arena.Local.Seat;int target=Math.Max(1,match.DeathmatchTarget);
            if(dmScoreMatch!=match){dmScoreMatch=match;dmScoreSeen.Clear();dmScoreAt.Clear();}
            const float rowH=34,head=40;var panel=new Rect(x,y,w,head+rowH*sides.Count+10);NativeHudChrome.Panel(panel);
            U.Text(new Rect(x+14,y+12,w-28,16),"DEATHMATCH",12,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            U.Text(new Rect(x+14,y+12,w-28,16),"FIRST TO "+target,10,NativeCyberTheme.Muted(),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.14f);
            U.HGradient(new Rect(x+14,y+head-6,(w-28)*.6f,NativeHudChrome.Snap(1)),NativeCyberTheme.Edge(.8f),Color.clear);
            int best=sides.Count>0?sides.Max(s=>s.score):0,rank=0;float ry=y+head;
            foreach(var side in sides.OrderByDescending(s=>s.score))
            {
                rank++;int key=match.Teams?1000+side.team:side.id;
                if(!dmScoreSeen.TryGetValue(key,out int seen)||seen!=side.score){if(dmScoreSeen.ContainsKey(key)&&side.score>seen)dmScoreAt[key]=now;dmScoreSeen[key]=side.score;}
                float since=dmScoreAt.TryGetValue(key,out float at)?now-at:10,flash=calm?0:1-M.Clamp01(since/.7f),pop=calm?0:1-M.Clamp01(since/.25f);
                bool you=match.Teams?side.team==me.team:side.id==me.id,lead=side.score==best&&best>0;var color=match.Teams?TeamColor(side.team):SeatColor(side);
                string name=match.Teams?NativeMatch.TeamNames[side.team]+" team":side.name;var row=new Rect(x+8,ry,w-16,rowH-4);
                if(you)U.Round(row,U.A(NativeCyberTheme.Cyan(),.06f),4);if(flash>0){U.Glow(row,U.A(color,.25f*flash),8);U.Round(row,U.A(color,.16f*flash),4);}
                U.Text(new Rect(row.x+4,row.y,16,row.height-8),rank.ToString(),10,NativeCyberTheme.Dim(),TextAnchor.MiddleLeft,mono:true);
                U.Fill(new Rect(row.x+20,row.y+4,NativeHudChrome.Snap(2),row.height-12),color);
                U.Text(new Rect(row.x+28,row.y,w-120,row.height-8),name+(you?"  YOU":""),13,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,bold:you);
                if(lead){float lx=row.x+28+U.Measure(name+(you?"  YOU":""),13,you).x+8;U.Text(new Rect(lx,row.y,20,row.height-8),"▲",9,U.A(color,.9f),TextAnchor.MiddleLeft);}
                var sr=new Rect(row.xMax-60,row.y,52,row.height-8);U.Push(sr.center,Vector2.zero,1+.3f*pop);
                U.Text(sr,side.score.ToString(),17,U.Mix(NativeCyberTheme.Ink(),color,.25f+.5f*flash),TextAnchor.MiddleRight,bold:true,mono:true,glow:U.A(color,.35f*flash));U.Pop();
                NativeHudChrome.Line(new Rect(row.x+28,row.yMax-6,row.width-36,NativeHudChrome.Snap(2)),Mathf.Clamp01(side.score/(float)target),U.A(color,.85f),NativeCyberTheme.Edge(.35f));
                ry+=rowH;
            }
        }
    }
}
