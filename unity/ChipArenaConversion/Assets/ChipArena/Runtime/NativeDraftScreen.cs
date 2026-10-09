using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The chip draft overlay over the 3D table stage (NativeDraftPresentation: the drafter seated with the offers laid out on
    // the table) and the Siren personal pick screen that shares it (pve/chip-screen.ts). Rules, timers and the network stay
    // as they are: this only draws the authoritative draft (offers, ready time, pick, pick time) and sends the same choose
    // messages. Hovering or arrowing to a chip lifts it on the table and shows its full card beside the table.
    public sealed partial class ConversionGame
    {
        private int draftFocus=-1,draftContactKey,draftPress=-1;
        // NativeMatch.NextDraft's offer pause, recovered from the replicated ready time (the turn's start is not sent).
        private double DraftStarted(NativeSeat seat)
        {
            if(seat.bot&&match.SkipBotDrafts)return match.ReadyAt;int n=seat.offers.Count;bool exclusive=seat.offers.Any(id=>id.StartsWith("l44t-")||id.StartsWith("arcade-")||id.StartsWith("mishima-"));
            return match.ReadyAt-Math.Max(1.2,(n-1)*.22+(exclusive?1.6:1));
        }
        // The draft queue as NativeMatch builds it (losers first; everyone with both-sides drafting), for the turn pills.
        private List<NativeSeat> DraftOrder()
        {
            var winner=match.Seat(match.SetWinner);return match.Seats.Where(p=>p.connected&&!p.AuthorityBoss&&(match.BothDraft||winner==null||!match.SameSide(p,winner))).OrderBy(p=>winner!=null&&match.SameSide(p,winner)).ToList();
        }
        // draft-screen.css :before / :after shades behind the heading and along the bottom edge.
        private static void DraftShades(float W,float H)
        {
            float top=Mathf.Min(H*.44f,460);U.Fill(new Rect(0,0,W,top*.52f),U.Hex(0x06070b,.5f));U.Fade(new Rect(0,0,W,top*.52f),U.Hex(0x06070b,.32f));U.Fade(new Rect(0,top*.52f,W,top*.48f),U.Hex(0x06070b,.5f));
            U.Fade(new Rect(0,H*.84f,W,H*.16f),U.Hex(0x06070b,.6f),false);
        }
        private static void Nameplate(NativeSeat seat,string label,float x,float y)
        {
            float nw=U.Measure(seat.name,13).x,lw=U.Measure(label.ToUpperInvariant(),10,true,true,.14f).x,w=10+9+8+nw+8+lw+13;var r=new Rect(x,y-30,w,30);
            U.Glow(new Rect(r.x,r.y+6,r.width,r.height),new Color(0,0,0,.45f),20);U.Pill(r,U.Hex(0x0d0f16,.8f),new Color(1,1,1,.12f));var dot=new Rect(r.x+10,r.y+10.5f,9,9);U.Glow(dot,U.A(SeatColor(seat),.6f),8);U.Round(dot,SeatColor(seat),5);
            U.Text(new Rect(dot.xMax+8,r.y,nw+4,r.height),seat.name,13,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft);U.Text(new Rect(dot.xMax+8+nw+8,r.y,lw+4,r.height),label.ToUpperInvariant(),10,U.Hex(0xa6a7ad),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
        }
        // The side card column: the table frames itself to its left.
        private static float DraftCardWidth(float W)=>Mathf.Clamp(W*.25f,360,430);
        private static float DraftCardLeft(float W,float H)=>W-(H<=760?22:28)-DraftCardWidth(W);
        // The chip under the pointer (offer index), from the areas the table projected this frame: a chip's own footprint wins,
        // then the gap around it, so neighbours never fight over the pointer and the lifted chip cannot slip out from under it.
        // Chips still dropping onto the table (their entrance) are not hoverable yet.
        private int TableHover(int count,float elapsed,bool allowed)
        {
            if(!allowed||count==0||draftCardCount!=count)return -1;
            for(int pass=0;pass<2;pass++)for(int k=0;k<count;k++){int i=draftOrder[k];if(i<0||i>=count||elapsed-k*.22f<.3f)continue;if(U.Hover(pass==0?draftCores[i]:draftCards[i]))return i;}
            return -1;
        }
        // A click on a chip: mouse-up over the chip that took the mouse-down (a GUI.Button's click), only while picking is enabled.
        private int TableClick(int hovered,bool enabled)
        {
            var e=Event.current;if(!enabled){draftPress=-1;return -1;}
            if(e.type==EventType.MouseDown&&e.button==0&&hovered>=0){draftPress=hovered;e.Use();}
            else if(e.type==EventType.MouseUp&&e.button==0&&draftPress>=0){int was=draftPress;draftPress=-1;if(was==hovered){e.Use();return hovered;}}
            return -1;
        }
        // Arrow keys cycle the enabled chips in the table's reading order (wrapping); Enter picks the focused one; 1-9 pick
        // directly (PvE), numbered in that same order as the badges on the table show.
        private int DraftKeys(int count,bool enabled,bool digits)
        {
            var e=Event.current;if(!enabled||count==0||e.type!=EventType.KeyDown)return -1;bool table=draftCardCount==count;
            int At(int k)=>table?draftOrder[k]:k;
            int Pos(int i){if(!table)return i;for(int k=0;k<count;k++)if(draftOrder[k]==i)return k;return -1;}
            int p=draftFocus>=0&&draftFocus<count?Pos(draftFocus):-1;
            if(e.keyCode==KeyCode.RightArrow||e.keyCode==KeyCode.DownArrow){draftFocus=At(p<0?0:(p+1)%count);e.Use();}
            else if(e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.UpArrow){draftFocus=At(p<0?count-1:(p+count-1)%count);e.Use();}
            else if((e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)&&draftFocus>=0&&draftFocus<count){e.Use();return draftFocus;}
            else if(digits&&e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9){int n=e.keyCode-KeyCode.Alpha1;if(n<count){e.Use();return At(n);}}
            return -1;
        }
        private void DraftHeader(string eyebrow,Color eyebrowColor,string title,string hint,float W,float H,out float bottom)
        {
            float pad=H<=760?16:22,x=H<=760?22:28,y=pad;var shade=new Color(0,0,0,.85f);
            U.Text(new Rect(x,y,W*.55f,15),eyebrow,12,eyebrowColor,TextAnchor.MiddleLeft,bold:true,tracking:.2f,shadow:shade);y+=15+6;
            float F=H<=760?24:Mathf.Clamp(W*.025f,24,34);U.Text(new Rect(x,y,W*.6f,F*1.08f),title,F,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft,bold:true,tracking:-.03f,shadow:shade);y+=F*1.08f+4;
            if(!string.IsNullOrEmpty(hint)&&H>760){var lines=U.Wrap(hint,14,Mathf.Min(W*.45f,46*7.2f));foreach(var line in lines){U.Text(new Rect(x,y,W*.5f,14*1.45f),line,14,U.Hex(0xc3c5ce),TextAnchor.MiddleLeft,shadow:shade);y+=14*1.45f;}}
            bottom=y+Mathf.Clamp(H*.024f,4,30);
        }

        // ---- the side card ----
        private ContentChip sideCur,sidePrev;private float sideCurA,sidePrevA,sideSweep=-10;private string sideLabel="";private int sideLabelPos=-1,sideLabelCount=-1;
        private static readonly string[] KeyDigits={"1","2","3","4","5","6","7","8","9"};
        // The lifted chip's full card beside the table (NativeChipCard at the spotlight size, its whole design). A new chip
        // cross-fades in over .2 s with a short slide while the last one fades out over .14 s, so the panel never pops. After
        // the pick it holds the chosen card and, once the chip is planted, marks it INSTALLED with the owner's name and the
        // tier's confirm line (the old details spotlight, now beside the table instead of over it).
        private void DraftSideCard(ContentChip show,int position,int count,int copies,bool focus,bool enabled,bool installed,NativeSeat owner,string hint,float W,float H)
        {
            float now=Time.unscaledTime;
            if(Event.current.type==EventType.Repaint)
            {
                if(show!=sideCur){if(sideCur!=null&&sideCurA>0){sidePrev=sideCur;sidePrevA=sideCurA;}sideCur=show;sideCurA=0;sideSweep=now;}
                float dt=Mathf.Min(.1f,Time.unscaledDeltaTime);sideCurA=Mathf.MoveTowards(sideCurA,sideCur!=null?1:0,dt/.2f);sidePrevA=Mathf.MoveTowards(sidePrevA,0,dt/.14f);if(sidePrevA<=0)sidePrev=null;
            }
            if(position!=sideLabelPos||count!=sideLabelCount){sideLabelPos=position;sideLabelCount=count;sideLabel=position>=0?"CHIP "+(position+1)+" OF "+count:"";}
            float cw=DraftCardWidth(W),x=DraftCardLeft(W,H);var size=ChipSize.Spotlight;
            if(sidePrev!=null)Card(sidePrev,sidePrevA,false);
            if(sideCur!=null)Card(sideCur,sideCurA,true);
            void Card(ContentChip chip,float a,bool current)
            {
                if(a<=.002f)return;float e=M.Bezier(.16f,.84f,.24f,1,a),ch=Mathf.Max(260,NativeChipCard.Height(chip,cw,size)),head=installed&&current?58:30,block=head+ch+22+18;
                float y=Mathf.Clamp((H-block)/2+16,H<=760?118:150,Mathf.Max(H<=760?118:150,H-block-96));var card=new Rect(x,y+head,cw,ch);Color rare=NativeChipCard.Rarity(chip.rarity);
                U.Push(card.center,new Vector2(current?16*(1-e):-10*(1-e),0),current?.97f+.03f*e:1);
                if(installed&&current)
                {
                    U.Text(new Rect(x-20,y,cw+40,28),owner.name,22,U.A(Color.white,e),TextAnchor.MiddleCenter,bold:true,shadow:new Color(0,0,0,e));
                    U.Text(new Rect(x,y+31,cw,15),"INSTALLED",12,U.A(chip.rarity=="siren"?U.Hex(0xc9bfff):rare,e),TextAnchor.MiddleCenter,mono:true,tracking:.35f);
                    U.Glow(card,U.A(rare,.32f*e),60);
                }
                else if(current&&sideLabel.Length>0)U.Text(new Rect(x+2,y,cw,16),sideLabel,11,U.A(U.Hex(0xa6a7ad),e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f,shadow:new Color(0,0,0,.7f*e));
                U.Glow(new Rect(card.x,card.y+20,card.width,card.height),new Color(0,0,0,.55f*e),70);
                var look=ChipLook.Idle(now);look.Alpha=e;look.Hover=installed?0:1;look.SweepAt=current?sideSweep:-10;look.Focus=current&&focus;look.Enabled=enabled;look.Chosen=current&&installed;
                NativeChipCard.Draw(card,chip,Math.Max(0,copies),size,look);
                float fy=card.yMax+22;
                if(current&&installed)
                {
                    string tier=chip.rarity,confirm=tier=="mishima"?"POWER WITHOUT LIMIT":tier=="l44t"?"ROOT ACCESS GRANTED":tier=="ansatsuken"?"THE WARRIOR AWAKENS":tier=="siren"?"THE GRIN WIDENS":"UPGRADE SECURED",times=TimesText(copies+1);
                    float tw=U.Measure(confirm,13,false,true,.2f).x,xw=U.Measure(times,13,true,true,.2f).x,cx=card.center.x-(tw+12+xw)/2;Color cc=tier=="l44t"?U.Hex(0x9effbd):tier=="ansatsuken"?U.Hex(0xffb2bc):tier=="mishima"?U.Hex(0xff8199):tier=="siren"?U.Hex(0xd9d0ff):U.Hex(0xd8e4ef);
                    U.Text(new Rect(cx,fy,tw+4,16),confirm,13,U.A(cc,e),TextAnchor.MiddleLeft,mono:true,tracking:.2f);U.Text(new Rect(cx+tw+12,fy,xw+4,16),times,13,U.A(rare,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
                }
                else if(current&&!string.IsNullOrEmpty(hint))U.Text(new Rect(x,fy,cw,16),hint,11,U.A(U.Hex(0x9799a6),e),TextAnchor.MiddleCenter,mono:true,tracking:.08f,shadow:new Color(0,0,0,.7f*e));
                U.Pop();
            }
        }
        private string timesCache="";private int timesCount=-1;
        private string TimesText(int n){if(n!=timesCount){timesCount=n;timesCache="×"+n;}return timesCache;}
        // The table position of an offer (reading order), or -1.
        private int TablePosition(int offer,int count){if(draftCardCount!=count)return offer;for(int k=0;k<count;k++)if(draftOrder[k]==offer)return k;return -1;}

        // ---- PvP draft ----
        private void DraftScreen()
        {
            var seat=match.Seat(match.DraftSeat);if(seat==null)return;float W=U.W,H=U.H;bool own=!seat.bot&&seat.id==arena.Local.Seat.id;
            var chips=DraftChips(false,seat);int count=Math.Min(chips.Count,NativeDraftTable.MaxChips),index=-1;ContentChip chosen=null;
            if(match.Chosen!=null)for(int i=0;i<count;i++)if(chips[i].id==match.Chosen){chosen=chips[i];index=i;break;}
            float age=chosen==null?-1:(float)(match.Time-match.ChosenAt);
            int turn=Mathf.Max(1,DraftOrder().FindIndex(p=>p.id==seat.id)+1),total=Math.Max(1,DraftOrder().Count);
            string title=chosen!=null?"Upgrade secured":own?"Make your next move":seat.name+" is choosing",hint=chosen!=null?"":own?"Choose one Chip. Everyone can see this draft.":"Watch the draft. All players see the same choices and the selected upgrade.";
            DraftShades(W,H);DraftHeader("CHIP DRAFT / SET "+match.Set,U.Hex(0xc0c1ca),title,hint,W,H,out float top);
            // Turn order and the timer (top right); the timer leaves with the pick.
            float x=W-(H<=760?22:28),y=H<=760?16:22;var order=DraftOrder();int at=order.FindIndex(p=>p.id==seat.id);
            string turnText="TURN "+turn+" / "+total;float tw=U.Measure(turnText,12,true).x;U.Text(new Rect(x-tw,y,tw,28),turnText,12,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft,bold:true);float px=x-tw-6;
            foreach(var p in match.Seats.Where(s=>!s.AuthorityBoss).Reverse())
            {
                int pos=order.FindIndex(o=>o.id==p.id);bool current=p.id==seat.id;string state=current?(chosen!=null?" · INSTALLED":" · PICKING"):pos>=0&&pos<at?" · READY":pos>at?" · UP NEXT":"";string label=p.name+state;
                float lw=U.Measure(label,12).x+20+8;var r=new Rect(px-lw,y,lw,28);if(r.x<W*.45f){r.y+=34;}U.Pill(r,current?U.Hex(0x36251f,.9f):U.Hex(0x15161c,.8f),current?U.Hex(0xff6b35):new Color(1,1,1,.12f));
                var dot=new Rect(r.x+10,r.y+10,8,8);U.Round(dot,SeatColor(p),4);U.Text(new Rect(dot.xMax+6,r.y,lw,r.height),label,12,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft);px=r.x-7;
            }
            if(chosen==null){string timer=Math.Max(0,Math.Ceiling(match.PhaseEnd-match.Time))+" seconds";float w=U.Measure(timer,13,true,true).x+22;var r=new Rect(x-w,y+38,w,28);U.Pill(r,U.Hex(0x15161c,.85f),new Color(1,1,1,.14f));U.Text(r,timer,13,U.Hex(0xff996f),TextAnchor.MiddleCenter,bold:true,mono:true);}
            // The table: offers boot in reading order from the turn's start; everyone may inspect (lift and read) a chip, only
            // the picker picks, after the ready time.
            float elapsed=(float)(match.Time-DraftStarted(seat));bool enabled=own&&chosen==null&&match.Time>=match.ReadyAt;
            int hovered=TableHover(count,elapsed,chosen==null),pick=TableClick(hovered,enabled),key=DraftKeys(count,enabled,false);if(key>=0)pick=key;
            if(pick>=0&&enabled){nativeAudio.Play("ui",null,.4f);ChooseChip(seat.id,chips[pick].id);}
            if(!enabled&&chosen==null&&draftFocus>=0&&!own)draftFocus=-1;
            int lifted=chosen!=null?-1:hovered>=0?hovered:own?draftFocus:-1;if(Event.current.type==EventType.Repaint)draftHover=lifted;
            // One install sound per pick as the chip is planted in the wrist, for every viewer (late joiners stay quiet).
            if(chosen!=null){int pickKey=(seat.id*397)^match.Round^(int)(match.ChosenAt*1000);if(age>=NativeDraftTable.SoundAt&&draftContactKey!=pickKey){draftContactKey=pickKey;if(age<NativeDraftTable.SoundAt+.3f)nativeAudio.Play("install",null,.5f);}}
            int show=chosen!=null?index:lifted,owned=show>=0&&seat.chips.TryGetValue(chips[show].id,out int n)?n-(show==index?1:0):0;
            string cue=chosen!=null?"":enabled?"CLICK OR PRESS ENTER TO INSTALL":own?"":seat.name.ToUpperInvariant()+" IS CHOOSING";
            DraftSideCard(show>=0?chips[show]:null,show>=0?TablePosition(show,count):-1,count,owned,show==draftFocus&&hovered<0,enabled,chosen!=null&&age>=NativeDraftTable.InstalledAt,seat,cue,W,H);
            Nameplate(seat,chosen!=null?"Installing":own?"Your pick":"Picking",H<=760?22:28,H-(H<=760?14:22));
        }

        // ---- Siren / Ward Zero personal pick (pve/chip-screen.ts) ----
        private float pveOpenAt=-1;private int pveOpenId=-1;
        private static string TierName(string t)=>t=="rainbow"?"Rainbow":NativeChipCard.RarityName(t);
        // pve/tracks.ts AttackedTrack: any hit on me with damage, burn and poison ticks included.
        private bool PveUnderAttack(){int me=arena.Local?.Seat.id??0;foreach(var e in arena.Events)if(NativeEventFeedback.Attacked(e,me,arena.Time,1.2f))return true;return false;}
        // The key badges on the table (1-9 in reading order), fading in with each chip's entrance.
        private void TableBadges(int count,float elapsed)
        {
            if(draftCardCount!=count)return;
            for(int k=0;k<count&&k<9;k++){int i=draftOrder[k];float a=Mathf.Clamp01((elapsed-k*.22f-.25f)/.3f);if(a<=0)continue;var b=new Rect(draftBadges[i].x-15,draftBadges[i].y-15,22,22);
                U.Glow(new Rect(b.x,b.y+2,b.width,b.height),new Color(0,0,0,.6f*a),5);U.Round(b,U.Hex(0xf1f2f6,a),6);U.Text(b,KeyDigits[k],12,U.Hex(0x0f1015,a),TextAnchor.MiddleCenter,bold:true,mono:true);}
        }
        private void PveChipScreen()
        {
            var v=arena.Pve;if(v==null||!v.Rows.TryGetValue(arena.Local.Seat.id,out var row)||row.Open==null||row.Open.Offers==null)return;var pick=row.Open;float W=U.W,H=U.H;
            PveOpenClock(pick);float elapsed=Time.unscaledTime-pveOpenAt;
            var chips=DraftChips(true,arena.Local.Seat);int count=Math.Min(chips.Count,NativeDraftTable.MaxChips);bool rainbow=pick.Tier=="rainbow";Color tier=rainbow?Color.white:NativeChipCard.Rarity(pick.Tier);
            string source=rainbow?"RAINBOW · 8 MAX-RARITY CHOICES":pick.Source=="start"?"BEGINNING CHIP":(pick.Source=="wave"?"WAVE REWARD · ":"DISPENSED · ")+TierName(pick.Tier).ToUpperInvariant();
            DraftShades(W,H);DraftHeader(source,tier,"Make your next move","Choose one Chip. This choice is yours alone.",W,H,out float top);
            // Meta (top right): the queue and the vulnerability warning.
            float x=W-28,y=22;string warn="YOU ARE VULNERABLE WHILE CHOOSING";float ww=U.Measure(warn,11,true,true,.08f).x;U.Text(new Rect(x-ww,y,ww+2,24),warn,11,U.Hex(0xd8dae4),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
            int queued=Math.Max(0,row.Picks.Count-1);if(queued>0){string q=(queued+" more waiting").ToUpperInvariant();float qw=U.Measure(q,11,true,true,.08f).x+20;var r=new Rect(x-ww-12-qw,y,qw,24);U.Pill(r,U.Hex(0xff6b35,.13f),Color.clear);U.Text(r,q,11,U.Hex(0xffb38f),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);}
            int hovered=TableHover(count,elapsed,true),choice=TableClick(hovered,true),key=DraftKeys(count,true,true);if(key>=0)choice=key;
            if(choice>=0){pveOpenId=-1;ChoosePveChip(pick.Id,chips[choice].id);return;}
            int lifted=hovered>=0?hovered:draftFocus;if(Event.current.type==EventType.Repaint)draftHover=lifted;
            TableBadges(count,elapsed);
            int position=lifted>=0?TablePosition(lifted,count):-1;
            DraftSideCard(lifted>=0&&lifted<count?chips[lifted]:null,position,count,lifted>=0&&lifted<count&&arena.Local.Seat.chips.TryGetValue(chips[lifted].id,out int n)?n:0,lifted==draftFocus&&hovered<0,true,false,arena.Local.Seat,position>=0&&position<9?PveCue(position):"CLICK TO INSTALL",W,H);
            Nameplate(arena.Local.Seat,"Your pick",28,H-22-66);
            // Footer: how to pick, and Choose later (C / Esc).
            string how="Press 1–"+Mathf.Clamp(count,1,9)+" or click a chip";float hw=U.Measure(how,12,false,true).x,bw=200,fw=hw+12+bw+24;var foot=new Rect(W-28-fw,H-22-56,fw,56);U.Round(foot,U.Hex(0x0d0f16,.9f),12);
            U.Text(new Rect(foot.x+12,foot.y,hw+4,foot.height),how,12,U.Hex(0x9799a6),TextAnchor.MiddleLeft,mono:true);if(U.Btn(new Rect(foot.xMax-12-bw,foot.y+8,bw,40),"Choose later  C · Esc",false,true,1,14))PveOpen(false);
            // Under attack: the blinking alarm and the red vignette, updated without moving the table.
            if(PveUnderAttack())
            {
                bool on=Mathf.Repeat(Time.unscaledTime,.25f)<.125f;U.Round(new Rect(0,0,W,H),Color.clear,2,U.Hex(0xff2b3d,on?1:.15f),4);U.Glow(new Rect(-30,-30,W+60,H+60),U.Hex(0xff1f33,(on?.6f:.09f)),-60);
                string alarm="WARNING — YOU ARE UNDER ATTACK",sub="Choose later to defend yourself";float aw=U.Measure(alarm,14,true,true,.14f).x+U.Measure(sub,12).x+56;var ar=new Rect(W/2-aw/2,H-100-36,aw,36);
                U.Round(ar,on?U.Hex(0x5a0d18):U.Hex(0x2a070c),8,U.Hex(0xff3b4b));U.Text(new Rect(ar.x+12,ar.y,aw,ar.height),"☠  "+alarm,14,Color.white,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);U.Text(new Rect(ar.x,ar.y,aw-12,ar.height),sub,12,U.Hex(0xffb3ba),TextAnchor.MiddleRight);
            }
            if(rainbow)U.Round(new Rect(0,0,W,H),Color.clear,2,Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime/3,1),.7f,1),2);
        }
        private static readonly string[] PveCues={"PRESS 1 OR CLICK TO INSTALL","PRESS 2 OR CLICK TO INSTALL","PRESS 3 OR CLICK TO INSTALL","PRESS 4 OR CLICK TO INSTALL","PRESS 5 OR CLICK TO INSTALL","PRESS 6 OR CLICK TO INSTALL","PRESS 7 OR CLICK TO INSTALL","PRESS 8 OR CLICK TO INSTALL","PRESS 9 OR CLICK TO INSTALL"};
        private static string PveCue(int position)=>PveCues[position];
    }
}
