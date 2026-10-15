using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The chip draft over the 3D pilot stage (draft-presentation.ts, draft-view.ts, draft-screen.css) and the Siren
    // personal pick screen that reuses its tray (pve/chip-screen.ts). Rules, timers and the network stay as they are: this
    // only draws the authoritative draft (offers, ready time, pick, pick time) and sends the same choose messages.
    public sealed partial class ConversionGame
    {
        private int draftFocus=-1;private float draftTrayLine=-1,draftTrayBottom=-1,draftHeadLine=-1;private readonly float[] cardHolo=new float[16],cardHoloAt=new float[16];private readonly NativeChipCard.Reveal[] cardReveal=new NativeChipCard.Reveal[16];
        private static float DraftRevealSeconds(string id)=>id.StartsWith("l44t-")?4.6f:id.StartsWith("arcade-")||id.StartsWith("mishima-")?4.8f:3.2f;
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
        private sealed class Tray {public Rect[] Cards=Array.Empty<Rect>();public int Rows;public float Top,Bottom;public ChipSize Size;}
        // draft-timeline.ts draftPerRow/draftRows and the .draft-row width rule; cards in a row share the tallest height and a
        // short row sits centred (NativeDraftRules.RowX).
        private Tray LayoutTray(IList<ContentChip> chips,float top,float W,float H)
        {
            var t=new Tray();int n=chips.Count;if(n==0){t.Bottom=top;return t;}int fit=W<=760||H<=480?n:Mathf.Clamp(Mathf.FloorToInt((W-42)/164),2,6);int[] rows=n<=fit?new[]{n}:new[]{(n+1)/2,n/2};t.Rows=rows.Length;
            bool large=W>=1600&&H>=900;int desc=H<=540?0:H<=600?2:H<=760?3:4;if(t.Rows==2)desc=H<=640?0:H<=820?Math.Min(desc,2):desc;t.Size=ChipSize.Tray(large,desc);
            float tray=W-56,gap=14,rowGap=H<=760||t.Rows==2&&H<=820?14:20,cap=t.Rows==2?214:large?272:236,y=top;t.Cards=new Rect[n];int at=0;
            foreach(int cols in rows)
            {
                float w=Mathf.Min(cap,Mathf.Max(118,(tray-(cols-1)*gap)/cols)),h=0;for(int i=0;i<cols;i++)h=Mathf.Max(h,NativeChipCard.Height(chips[at+i],w,t.Size));
                y+=10;
                for(int i=0;i<cols;i++){float x=NativeDraftRules.RowX(i,cols,w,tray,gap,W);t.Cards[at+i]=new Rect(x,y,w,h);}
                at+=cols;y+=h+8+rowGap;
            }
            t.Top=t.Cards[0].y;t.Bottom=y-rowGap;return t;
        }
        // The tray sits low, in front of the pilot (owner 2026-10-11: the pilot close, from the solar plexus up; NativeDraftStage
        // frames the plexus on the tray's top line): moved down so its last row ends on `bottom` (above the nameplate and the
        // footer), never above `top` (the heading's bottom edge). Card sizes and spacing are LayoutTray's.
        private static void LowerTray(Tray t,float top,float bottom)
        {
            float off=Mathf.Max(top-t.Top+10,bottom-t.Bottom);for(int i=0;i<t.Cards.Length;i++)t.Cards[i].y+=off;t.Top+=off;t.Bottom+=off;
        }
        // draft-screen.css :before / :after shades, moved with the composition: a light one behind the heading (the pilot's head
        // stands below it) and the lower one on the stage's framing (ArenaPresentation.DraftShade: NativeDraftStage.ShadeLine /
        // ShadeDeep): from 12 px above the plexus line (the tray's top edge, or under the resting plexus when a tall tray covers
        // it) it deepens to opaque a little under the plexus, with the pilot's size, and holds to the bottom, in the stage's
        // backdrop colour (the draft camera clears to about #06080e). It stays after the pick (on the tray's last layout), so the
        // pilot reads from the solar plexus up in every state: the lap, the resting hands and the legs never show, while the
        // cards, the nameplate, the spotlight and a hand raised above the line (the considering lift, the catch and the plant)
        // draw over it.
        private void DraftShades(Tray tray,float W,float H)
        {
            float head=Mathf.Min(H*.24f,230);U.Fill(new Rect(0,0,W,head*.4f),U.Hex(0x06070b,.42f));U.Fade(new Rect(0,head*.4f,W,head*.6f),U.Hex(0x06070b,.42f));
            float top=tray!=null&&tray.Cards.Length>0?tray.Top/H:NativeDraftStage.DefaultTray;presentation.DraftShade(top,out float line,out float deep);line*=H;deep*=H;var shade=U.Hex(0x06070b,NativeDraftStage.ShadeOpaque);
            U.Fade(new Rect(0,line-12,W,deep-line+12),shade,false);U.Fill(new Rect(0,deep,W,H-deep),shade);
        }
        private static void Nameplate(NativeSeat seat,string label,float x,float y)
        {
            float nw=U.Measure(seat.name,13).x,lw=U.Measure(label.ToUpperInvariant(),10,true,true,.14f).x,w=10+9+8+nw+8+lw+13;var r=new Rect(x,y-30,w,30);
            U.Glow(new Rect(r.x,r.y+6,r.width,r.height),new Color(0,0,0,.45f),20);U.Pill(r,U.Hex(0x0d0f16,.8f),new Color(1,1,1,.12f));var dot=new Rect(r.x+10,r.y+10.5f,9,9);U.Glow(dot,U.A(SeatColor(seat),.6f),8);U.Round(dot,SeatColor(seat),5);
            U.Text(new Rect(dot.xMax+8,r.y,nw+4,r.height),seat.name,13,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft);U.Text(new Rect(dot.xMax+8+nw+8,r.y,lw+4,r.height),label.ToUpperInvariant(),10,U.Hex(0xa6a7ad),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
        }
        // Card entrance, the highlight and focus; returns the index clicked (or -1). `enabled` gates the highlight, focus and
        // clicks. The highlighted card is the one under the pointer (the unlifted rect), else the keyboard focus; while the
        // pointer crosses a gap the stage's held card (NativeDraftHover.Reaching) stays up at NativeHoloScan.Held. It lifts and
        // wears the holo scan (NativeChipArt), its level eased per card on Repaint and its timeline restarted only from fully off
        // (NativeHoloScan); the others recede under a veil while it is up, and the keyboard focus keeps still brackets while the
        // pointer is on another card. The stage's gaze and reach follow the highlighted card (draftHover). The skipped (chosen)
        // card's level stays as it was at the pick, for FlyToPalm.
        private readonly NativeHoverFocus hoverSoundFocus=new NativeHoverFocus();
        private int DrawTray(Tray tray,IList<ContentChip> chips,Func<int,int> copies,float elapsed,bool enabled,int skip,Func<int,bool> hidden,bool badges=false)
        {
            int clicked=-1,n=Math.Min(tray.Cards.Length,cardHolo.Length),under=-1;float now=Time.unscaledTime;bool repaint=Event.current.type==EventType.Repaint,calm=prefs.reducedEffects;if(repaint)draftHover=-1;
            for(int i=0;i<n;i++){bool shown=i!=skip&&!hidden(i);cardReveal[i]=shown?NativeChipCard.Entrance(chips[i].rarity,elapsed-i*.22f):default;if(shown&&enabled&&cardReveal[i].Alpha>0&&U.Hover(tray.Cards[i]))under=i;}
            int lit=under>=0?under:draftFocus>=0&&draftFocus<n&&cardReveal[draftFocus].Alpha>0?draftFocus:-1;bool held=enabled&&lit<0&&draftHeld.Reaching;if(enabled&&repaint&&lit>=0)draftHover=lit;
            // LayoutTray returns a new object every GUI pass. Track the actual mouse/keyboard highlight, never that object.
            // Chip regions use SilentButton below; only this focus cue and the screen's explicit pick cue may sound.
            if(repaint&&hoverSoundFocus.Step(lit,lit>=0?chips[lit].id:null,enabled))nativeAudio?.UiCue("ui.draft.hover",null,.5f);
            // Levels (Repaint only), then the most highlighted visible cards for the others' veil.
            int top=-1;float first=0,second=0;
            for(int i=0;i<n;i++)
            {
                if(i==skip||hidden(i))continue;
                if(repaint){float target=enabled&&cardReveal[i].Alpha>0?NativeHoloScan.Target(lit==i,held&&draftHeld.Card==i):0;if(NativeHoloScan.Engages(cardHolo[i],target))cardHoloAt[i]=now;cardHolo[i]=NativeHoloScan.Step(cardHolo[i],target,Time.unscaledDeltaTime);}
                float e=cardReveal[i].Alpha>=1?cardHolo[i]:0;if(e>first){second=first;first=e;top=i;}else if(e>second)second=e;
            }
            for(int i=0;i<tray.Cards.Length;i++)
            {
                if(i==skip||hidden(i))continue;var c=chips[i];var r=tray.Cards[i];var reveal=i<n?cardReveal[i]:NativeChipCard.Entrance(c.rarity,elapsed-i*.22f);if(reveal.Alpha<=0)continue;
                float h=i<n?cardHolo[i]:0,recede=reveal.Alpha>=1?NativeHoloScan.Recede(h,i==top?second:first):0;bool ghost=enabled&&under>=0&&draftFocus==i&&i!=under;
                // The card stands on the tray's display: its contact shadow and soft reflection stay on the surface (with the
                // entrance, not the lift), the shadow spreading and the glow brightening as the card lifts.
                U.Push(r.center,new Vector2(reveal.Dx,reveal.Dy),reveal.Scale*reveal.ScaleX,reveal.Degrees,reveal.Scale*reveal.ScaleY);TrayStand(r,c.rarity,reveal.Alpha,h);U.Pop();
                U.Push(r.center,new Vector2(reveal.Dx,reveal.Dy+NativeHoloScan.Lift(h)),reveal.Scale*reveal.ScaleX,reveal.Degrees,reveal.Scale*reveal.ScaleY);
                // The Siren pick's number badge sits on the card's top-left corner, under the holo scan's brackets (NativeChipCard).
                var look=new ChipLook{Alpha=reveal.Alpha,Hover=h,Time=now,SweepAt=-10,Bright=reveal.Bright,Focus=enabled&&draftFocus==i,Enabled=enabled,Plain=reveal.Degrees!=0,Holo=h,HoloAt=i<n?cardHoloAt[i]:-10,Calm=calm,Recede=recede,Ghost=ghost,Badge=badges&&i<9?i+1:0,Unbuilt=1-reveal.Build};
                NativeChipCard.Draw(r,c,copies(i),tray.Size,look);
                U.Pop();if(enabled&&reveal.Alpha>.5f&&U.SilentButton(r))clicked=i;
            }
            return clicked;
        }
        // A new tray (a new turn, another picker, the next Siren pick): no card starts highlighted.
        private void ResetHolo(){hoverSoundFocus.Reset();Array.Clear(cardHolo,0,cardHolo.Length);for(int i=0;i<cardHoloAt.Length;i++)cardHoloAt[i]=-10;}
        // A card's footing on the tray's display: a soft contact shadow along its lower edge and a faint glow of its rarity on
        // the surface under it. `hover` 0..1 (the lift).
        private static void TrayStand(Rect r,string rarity,float alpha,float hover)
        {
            if(alpha<=0)return;U.Glow(new Rect(r.x+12,r.yMax-5,r.width-24,8),new Color(0,0,0,.55f*alpha*(1-.3f*hover)),10+8*hover);
            U.Fade(new Rect(r.x+10,r.yMax+3,r.width-20,16),U.A(NativeChipCard.Rarity(rarity),(.1f+.08f*hover)*alpha));
        }
        // Arrow keys cycle the enabled cards (reading order, wrapping); Enter picks the focused one; 1-9 pick directly (PvE).
        private int DraftKeys(int count,bool enabled,bool digits)
        {
            var e=Event.current;if(!enabled||count==0||e.type!=EventType.KeyDown)return -1;
            if(e.keyCode==KeyCode.RightArrow||e.keyCode==KeyCode.DownArrow){draftFocus=draftFocus<0?0:(draftFocus+1)%count;e.Use();}
            else if(e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.UpArrow){draftFocus=draftFocus<0?count-1:(draftFocus+count-1)%count;e.Use();}
            else if((e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)&&draftFocus>=0&&draftFocus<count){e.Use();return draftFocus;}
            else if(digits&&e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha9){int n=e.keyCode-KeyCode.Alpha1;if(n<count){e.Use();return n;}}
            return -1;
        }
        private void DraftHeader(string eyebrow,Color eyebrowColor,string title,string hint,float W,float H,out float bottom)
        {
            float pad=H<=760?16:22,x=H<=760?22:28,y=pad;var shade=new Color(0,0,0,.85f);
            U.Text(new Rect(x,y,W*.55f,15),eyebrow,12,eyebrowColor,TextAnchor.MiddleLeft,bold:true,tracking:.2f,shadow:shade);y+=15+6;
            float F=H<=760?24:Mathf.Clamp(W*.025f,24,34);U.Text(new Rect(x,y,W*.6f,F*1.08f),title,F,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft,bold:true,tracking:-.03f,shadow:shade);y+=F*1.08f+4;
            if(!string.IsNullOrEmpty(hint)&&H>760){var lines=U.Wrap(hint,14,Mathf.Min(W*.45f,46*7.2f));foreach(var line in lines){U.Text(new Rect(x,y,W*.5f,14*1.45f),line,14,U.Hex(0xc3c5ce),TextAnchor.MiddleLeft,shadow:shade);y+=14*1.45f;}}
            bottom=y+Mathf.Clamp(H*.024f,4,30);draftHeadLine=bottom/H;
        }

        // ---- PvP draft ----
        private void DraftScreen()
        {
            var seat=match.Seat(match.DraftSeat);if(seat==null)return;float W=U.W,H=U.H,now=(float)match.Time;bool own=!seat.bot&&seat.id==arena.Local.Seat.id;
            var chips=seat.offers.Select(id=>content.Chip(id)).ToList();var chosen=match.Chosen!=null?content.Chip(match.Chosen):null;float age=chosen==null?-1:(float)(match.Time-match.ChosenAt);
            // The fly, shatter and spotlight follow the stage's smooth copy of the pick's age (the replicated clock steps).
            if(age>=0&&Math.Abs(presentation.DraftAge-age)<.3f)age=presentation.DraftAge;
            int turn=Mathf.Max(1,DraftOrder().FindIndex(p=>p.id==seat.id)+1),total=Math.Max(1,DraftOrder().Count);
            string title=chosen!=null?"Upgrade secured":own?"Make your next move":seat.name+" is choosing",hint=chosen!=null?"":own?"Choose one Chip. Everyone can see this draft.":"Watch the draft. All players see the same choices and the selected upgrade.";
            if(chosen==null||draftTraySize==Vector2.zero)draftTraySize=new Vector2(W,H);
            DraftShades(draftTray,W,H);DraftHeader("CHIP DRAFT / SET "+match.Set,U.Hex(0xc0c1ca),title,hint,W,H,out float top);if(chosen!=null)top=draftTrayTop;else draftTrayTop=top;
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
            // The tray (low, in front of the pilot): offers boot in reading order from the turn's start; only the picker's cards
            // enable, after the ready time.
            if(chosen==null){draftTray=LayoutTray(chips,top,W,H);LowerTray(draftTray,top,H-(H<=760?54:66));}var tray=draftTray;if(tray==null||tray.Cards.Length!=chips.Count)return;draftCards=tray.Cards;draftTrayLine=tray.Top/H;draftTrayBottom=tray.Bottom/H;
            float elapsed=(float)(now-DraftStarted(seat));bool enabled=own&&chosen==null&&match.Time>=match.ReadyAt;int index=chosen!=null?chips.IndexOf(chosen):-1;
            int pick=DrawTray(tray,chips,i=>seat.chips.TryGetValue(chips[i].id,out int n)?n-(i==index?1:0):0,elapsed,enabled,index,i=>chosen!=null,false);
            int key=DraftKeys(chips.Count,enabled,false);if(key>=0)pick=key;if(pick>=0&&enabled){nativeAudio.UiCue("ui.draft.pick","ui",.4f);ChooseChip(seat.id,chips[pick].id);}
            if(!enabled&&chosen==null&&draftFocus>=0&&!own)draftFocus=-1;
            if(chosen!=null)
            {
                // The others shatter and the chosen card flies into the pilot's palm; the pilot plants the chip into the other
                // wrist (NativeDraftPlant; the install cue lands on the plant, NativeAudio), then the details spotlight.
                PickedTray(tray,chips,index,seat.chips.TryGetValue(chosen.id,out int c)?c-1:0,age);
                float details=age-NativeDraftPlant.DetailsAt;if(details>=0)Spotlight(chosen,seat,details,DraftRevealSeconds(chosen.id)-NativeDraftPlant.DetailsAt);
            }
            if(chosen==null||age<NativeDraftPlant.DetailsAt)Nameplate(seat,chosen!=null?"Installing":own?"Your pick":"Picking",H<=760?22:28,H-(H<=760?14:22));
        }
        private Tray draftTray;private float draftTrayTop;private Vector2 draftTraySize;
        // A tray after its pick, `age` seconds on: the others shatter in turn, the chosen card (`copies` owned before it) flies
        // into the pilot's palm and fades as the hand closes. It leaves from its highlight as it was at the pick (DrawTray skips
        // it, so its level holds; a spectator's is 0).
        private void PickedTray(Tray tray,IList<ContentChip> chips,int index,int copies,float age)
        {
            for(int i=0;i<chips.Count&&i<tray.Cards.Length;i++)if(i!=index)Shatter(tray.Cards[i],chips[i],age-i*.035f,i,tray.Size);
            if(index>=0&&index<tray.Cards.Length)FlyToPalm(tray.Cards[index],chips[index],copies,age,tray.Size,index<cardHolo.Length?cardHolo[index]:0);
        }
        // chipFracture .85 s: still until 8%, a flash at 18%, then six shards of the cartridge fly apart, spin, shrink and fade (each
        // a chamfered fragment of its body with the bezel's edge and the tier's light inside).
        private void Shatter(Rect r,ContentChip chip,float t,int index,ChipSize size)
        {
            if(t<0){NativeChipCard.Draw(r,chip,0,size,ChipLook.Idle(Time.unscaledTime));return;}float k=t/.85f;if(k>=1)return;
            if(k<.18f){NativeChipCard.Draw(r,chip,0,size,ChipLook.Idle(Time.unscaledTime));if(k>.08f)NativeChipArt.Package(r,size.Radius,0,new Color(1,1,1,.5f*(k-.08f)/.1f));return;}
            float u=M.Bezier(.15f,.65f,.3f,1,(k-.18f)/.82f);Color rare=NativeChipCard.Rarity(chip.rarity),edge=U.Mix(U.Hex(NativeChipCartridge.Of(chip.rarity).MetalMid),rare,.45f);
            float[] xs={0,.52f,1},ys={0,.38f,.71f,1};
            for(int n=0;n<6;n++)
            {
                int col=n%2,row=n/2;var piece=new Rect(r.x+r.width*xs[col],r.y+r.height*ys[row],r.width*(xs[col+1]-xs[col]),r.height*(ys[row+1]-ys[row]));
                float dx=(n%2==1?1:-1)*(45+index*5),dy=(n-2.5f)*44,spin=(n%2==1?1:-1)*(15+n*7);
                U.Push(piece.center,new Vector2(dx*u,dy*u),1-.18f*u,spin*u);float a=1-u,glow=Mathf.Lerp(2.4f,.5f,u);
                var cut=n%2==0?NativeHudChrome.Corners.TL|NativeHudChrome.Corners.BR:NativeHudChrome.Corners.TR|NativeHudChrome.Corners.BL;
                NativeHudChrome.Cut(piece,U.Hex(0x0d1018,a),5,cut);NativeHudChrome.Cut(new Rect(piece.x+2,piece.y+2,piece.width-4,piece.height-4),U.A(rare,.12f*a*glow),4,cut);NativeHudChrome.Outline(piece,U.A(edge,a),5,cut,1.5f);U.Pop();
            }
        }
        // draft-view.ts: translate toward the palm by fly, scale 1-.88 fly, rotate -9 deg x fly, fade with the grab (the stage's
        // slowed timeline, NativeDraftStage). It flies straight to where the palm meets it at the contact (the stage's solved
        // catch, draftCatch), not the live palm still rising from the lap, so it never dips toward the hand first; the hand
        // arrives there as the card fades. It leaves from the highlight's lift at `level` (frozen at the pick), its die keeps
        // the highlight's settled charge, and the holo scan locks onto it (NativeChipArt.HoloLock, the die's flare) for every
        // viewer.
        private void FlyToPalm(Rect r,ContentChip chip,int copies,float age,ChipSize size,float level=0)
        {
            float fly=NativeDraftStage.Fly(age),fade=NativeDraftStage.Fade(age);if(fade>=1)return;Vector2 target=draftCatch!=Vector2.zero?draftCatch:draftPalm;if(target==Vector2.zero)target=r.center;bool calm=prefs.reducedEffects;
            U.Push(r.center,(target-r.center)*fly+new Vector2(0,NativeHoloScan.Lift(level)*(1-fly)),1-.88f*fly,-9*fly);var rare=NativeChipCard.Rarity(chip.rarity);U.Glow(r,U.A(rare,.55f*(1-fade)),42);NativeChipArt.PackageEdge(r,size.Radius,2,U.A(rare,1-fade),2);
            var look=ChipLook.Idle(Time.unscaledTime);look.Alpha=1-fade;look.Plain=true;look.Calm=calm;look.Charge=NativeHoloScan.DieRest*Mathf.Clamp01(level)+NativeHoloScan.LockFlare(age,calm);NativeChipCard.Draw(r,chip,copies,size,look);
            NativeChipArt.HoloLock(r,size.Radius,chip.rarity,rare,age,calm,1-fade);U.Pop();
        }
        // .selection-spotlight: radial backdrop and the focus block (owner, big card, confirm line) over the reveal hold.
        private void Spotlight(ContentChip chip,NativeSeat owner,float t,float duration)
        {
            if(duration<=0||t>duration)return;float k=t/duration,W=U.W,H=U.H,env=k<.05f?0:k<.16f?(k-.05f)/.11f:k<.82f?1:1-(k-.82f)/.18f;
            U.RadialStops(new Rect(-W*.207f,-H*.207f,W*1.414f,H*1.414f),U.Hex(0x080b16,env),new[]{0,.4f,.78f,1},new[]{.93f,.8f,.2f,.2f});
            Func<float,float> ease=x=>M.Bezier(.16f,.8f,.22f,1,x);float dy,sc;if(k<.05f){dy=35;sc=.72f;}else if(k<.2f){float u=ease((k-.05f)/.15f);dy=35*(1-u);sc=.72f+.28f*u;}else if(k<.82f){dy=0;sc=1;}else{float u=ease((k-.82f)/.18f);dy=-24*u;sc=1+.06f*u;}
            string tier=chip.rarity;float bw=Mathf.Min(tier=="l44t"?500:tier=="ansatsuken"?490:tier=="siren"?480:tier=="mishima"?470:440,W*.9f);var size=ChipSize.Spotlight;float ch=Mathf.Max(260,NativeChipCard.Height(chip,bw,size));
            float blockH=25*1.2f+9+15+24+ch+25+16,bx=(W-bw)/2,by=(H-blockH)/2;var rare=NativeChipCard.Rarity(tier);
            U.Push(new Vector2(W/2,H/2),new Vector2(0,dy),sc);
            U.Text(new Rect(bx-40,by,bw+80,30),owner.name,25,U.A(Color.white,env),TextAnchor.MiddleCenter,bold:true,shadow:new Color(0,0,0,env));
            U.Text(new Rect(bx,by+39,bw,15),"INSTALLED",12,U.A(tier=="siren"?U.Hex(0xc9bfff):rare,env),TextAnchor.MiddleCenter,mono:true,tracking:.35f);
            var card=new Rect(bx,by+39+15+24,bw,ch);U.Glow(card,U.A(rare,.32f*env),60);U.Glow(new Rect(card.x,card.y+20,card.width,card.height),new Color(0,0,0,.6f*env),80);
            int copies=owner.chips.TryGetValue(chip.id,out int n)?n:1;var look=ChipLook.Idle(Time.unscaledTime);look.Alpha=env;look.Chosen=true;NativeChipCard.Draw(card,chip,Math.Max(0,copies-1),size,look);
            string confirm=tier=="mishima"?"POWER WITHOUT LIMIT":tier=="l44t"?"ROOT ACCESS GRANTED":tier=="ansatsuken"?"THE WARRIOR AWAKENS":tier=="siren"?"THE GRIN WIDENS":"UPGRADE SECURED",times="×"+copies;
            float cw=U.Measure(confirm,13,false,true,.2f).x,xw=U.Measure(times,13,true,true,.2f).x,cx=W/2-(cw+12+xw)/2,cy=card.yMax+25;Color cc=tier=="l44t"?U.Hex(0x9effbd):tier=="ansatsuken"?U.Hex(0xffb2bc):tier=="mishima"?U.Hex(0xff8199):tier=="siren"?U.Hex(0xd9d0ff):U.Hex(0xd8e4ef);
            U.Text(new Rect(cx,cy,cw+4,16),confirm,13,U.A(cc,env),TextAnchor.MiddleLeft,mono:true,tracking:.2f);U.Text(new Rect(cx+cw+12,cy,xw+4,16),times,13,U.A(rare,env),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            U.Pop();
        }

        // ---- Siren / Ward Zero personal pick (pve/chip-screen.ts) ----
        private float pveOpenAt=-1;private int pveOpenId=-1;private Tray pveTray;
        private static string TierName(string t)=>t=="rainbow"?"Rainbow":NativeChipCard.RarityName(t);
        // pve/tracks.ts AttackedTrack: any hit on me with damage, burn and poison ticks included.
        private bool PveUnderAttack(){int me=arena.Local?.Seat.id??0;foreach(var e in arena.Events)if(NativeEventFeedback.Attacked(e,me,arena.Time,1.2f))return true;return false;}
        private void PveChipScreen()
        {
            if(PveFollowing){PveFollowScreen(U.W,U.H);return;}
            var v=arena.Pve;if(v==null||!v.Rows.TryGetValue(arena.Local.Seat.id,out var row)||row.Open==null||row.Open.Offers==null)return;var pick=row.Open;float W=U.W,H=U.H;
            if(pveOpenId!=pick.Id){pveOpenId=pick.Id;pveOpenAt=Time.unscaledTime;draftFocus=-1;ResetHolo();}float elapsed=Time.unscaledTime-pveOpenAt;
            var chips=pick.Offers.Select(id=>arena.PveChip(id)).Where(c=>c!=null).ToList();bool rainbow=pick.Tier=="rainbow";Color tier=rainbow?Color.white:NativeChipCard.Rarity(pick.Tier);
            string source=rainbow?"RAINBOW · 8 MAX-RARITY CHOICES":pick.Source=="start"?"BEGINNING CHIP":(pick.Source=="wave"?"WAVE REWARD · ":"DISPENSED · ")+TierName(pick.Tier).ToUpperInvariant();
            DraftShades(pveTray,W,H);DraftHeader(source,tier,"Make your next move","Choose one Chip. This choice is yours alone.",W,H,out float top);
            // Meta (top right): the queue and the vulnerability warning.
            float x=W-28,y=22;string warn="YOU ARE VULNERABLE WHILE CHOOSING";float ww=U.Measure(warn,11,true,true,.08f).x;U.Text(new Rect(x-ww,y,ww+2,24),warn,11,U.Hex(0xd8dae4),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
            int queued=Math.Max(0,row.Picks.Count-1);if(queued>0){string q=(queued+" more waiting").ToUpperInvariant();float qw=U.Measure(q,11,true,true,.08f).x+20;var r=new Rect(x-ww-12-qw,y,qw,24);U.Pill(r,U.Hex(0xff6b35,.13f),Color.clear);U.Text(r,q,11,U.Hex(0xffb38f),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);}
            pveTray=LayoutTray(chips,top,W,H);LowerTray(pveTray,top,H-142);draftCards=pveTray.Cards;draftTrayLine=pveTray.Top/H;draftTrayBottom=pveTray.Bottom/H;
            int choice=DrawTray(pveTray,chips,i=>arena.Local.Seat.chips.TryGetValue(chips[i].id,out int n)?n:0,elapsed,true,-1,i=>false,true);int key=DraftKeys(chips.Count,true,true);if(key>=0)choice=key;
            if(choice>=0){RecordPvePick(chips,choice,pick);pveOpenId=-1;ChoosePveChip(pick.Id,chips[choice].id);return;}
            Nameplate(arena.Local.Seat,"Your pick",28,H-22-66);
            // Footer: how to pick, and Choose later (C / Esc).
            string how="Press 1–"+Mathf.Clamp(chips.Count,1,9)+" or click a chip";float hw=U.Measure(how,12,false,true).x,bw=200,fw=hw+12+bw+24;var foot=new Rect(W-28-fw,H-22-56,fw,56);U.Round(foot,U.Hex(0x0d0f16,.9f),12);
            U.Text(new Rect(foot.x+12,foot.y,hw+4,foot.height),how,12,U.Hex(0x9799a6),TextAnchor.MiddleLeft,mono:true);if(U.Btn(new Rect(foot.xMax-12-bw,foot.y+8,bw,40),"Choose later  C · Esc",false,true,1,14))PveOpen(false);
            // Under attack: the blinking alarm and the red vignette, updated without moving the cards.
            if(PveUnderAttack())
            {
                bool on=Mathf.Repeat(Time.unscaledTime,.25f)<.125f;U.Round(new Rect(0,0,W,H),Color.clear,2,U.Hex(0xff2b3d,on?1:.15f),4);U.Glow(new Rect(-30,-30,W+60,H+60),U.Hex(0xff1f33,(on?.6f:.09f)),-60);
                string alarm="WARNING — YOU ARE UNDER ATTACK",sub="Choose later to defend yourself";float aw=U.Measure(alarm,14,true,true,.14f).x+U.Measure(sub,12).x+56;var ar=new Rect(W/2-aw/2,H-100-36,aw,36);
                U.Round(ar,on?U.Hex(0x5a0d18):U.Hex(0x2a070c),8,U.Hex(0xff3b4b));U.Text(new Rect(ar.x+12,ar.y,aw,ar.height),"☠  "+alarm,14,Color.white,TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);U.Text(new Rect(ar.x,ar.y,aw-12,ar.height),sub,12,U.Hex(0xffb3ba),TextAnchor.MiddleRight);
            }
            if(rainbow)U.Round(new Rect(0,0,W,H),Color.clear,2,Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime/3,1),.7f,1),2);
        }
    }
}
