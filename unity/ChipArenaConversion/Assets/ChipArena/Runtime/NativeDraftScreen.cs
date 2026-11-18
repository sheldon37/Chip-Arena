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
        // The turn's hold after the pick (NativeMatch's RevealSeconds, as NativeInstallShot.Turn has it; the Katana's and the Fangs' chips too).
        private static float DraftRevealSeconds(string id)=>NativeBombChips.Is(id)?4.8f:NativeDogChips.Is(id)?(float)NativeDogChips.RevealSeconds:NativeGlaiveChips.Is(id)?(float)NativeGlaiveChips.RevealSeconds:NativeKatanaChips.Is(id)?(float)NativeKatanaChips.RevealSeconds:NativeFangsChips.Is(id)?(float)NativeFangsChips.RevealSeconds:id.StartsWith("l44t-")?4.6f:id.StartsWith("arcade-")||id.StartsWith("mishima-")?4.8f:3.2f;
        // NativeMatch.NextDraft's offer pause, recovered from the replicated ready time (the turn's start is not sent).
        private double DraftStarted(NativeSeat seat)
        {
            if(seat.bot&&match.SkipBotDrafts)return match.ReadyAt;int n=seat.offers.Count;bool exclusive=seat.offers.Any(id=>NativeBombChips.Is(id)||NativeDogChips.Is(id)||NativeGlaiveChips.Is(id)||NativeKatanaChips.Is(id)||NativeFangsChips.Is(id)||id.StartsWith("l44t-")||id.StartsWith("arcade-")||id.StartsWith("mishima-"));
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
            // The install's words (NativeInstallShot): "Installing <chip>" from the pick, "Upgrade secured" typed in at the clack.
            string title=chosen!=null?InstallTitle(chosen,seat,own,age,W,H):own?"Make your next move":seat.name+" is choosing",hint=chosen!=null?"":own?"Choose one Chip. Everyone can see this draft.":"Watch the draft. All players see the same choices and the selected upgrade.";
            if(chosen==null||draftTraySize==Vector2.zero)draftTraySize=new Vector2(W,H);
            DraftShades(draftTray,W,H);DraftHeader("CHIP DRAFT / SET "+match.Set,U.Hex(0xc0c1ca),title,hint,W,H,out float top);if(chosen!=null)top=draftTrayTop;else draftTrayTop=top;
            // Turn order and the timer (top right); the timer leaves with the pick.
            float x=W-(H<=760?22:28),y=H<=760?16:22;var order=DraftOrder();int at=order.FindIndex(p=>p.id==seat.id);
            string turnText="TURN "+turn+" / "+total;float tw=U.Measure(turnText,12,true).x;U.Text(new Rect(x-tw,y,tw,28),turnText,12,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft,bold:true);float px=x-tw-6;
            bool installed=chosen!=null&&NativeInstallShot.Installed(age);
            foreach(var p in match.Seats.Where(s=>!s.AuthorityBoss).Reverse())
            {
                int pos=order.FindIndex(o=>o.id==p.id);bool current=p.id==seat.id;string state=current?(chosen!=null?(installed?" · INSTALLED":" · INSTALLING"):" · PICKING"):pos>=0&&pos<at?" · READY":pos>at?" · UP NEXT":"";string label=p.name+state;
                float lw=U.Measure(label,12).x+20+8;var r=new Rect(px-lw,y,lw,28);if(r.x<W*.45f){r.y+=34;}
                // The current pill stamps INSTALLED at the clack.
                float stamp=current&&chosen!=null?NativeInstallShot.Stamp(age):1;if(stamp!=1)U.Push(r.center,Vector2.zero,stamp);
                U.Pill(r,current?U.Hex(0x36251f,.9f):U.Hex(0x15161c,.8f),current?(installed?U.Mix(U.Hex(0xff6b35),RarityColor(chosen.rarity),.6f):U.Hex(0xff6b35)):new Color(1,1,1,.12f));
                var dot=new Rect(r.x+10,r.y+10,8,8);U.Round(dot,SeatColor(p),4);U.Text(new Rect(dot.xMax+6,r.y,lw,r.height),label,12,U.Hex(0xf4f4f5),TextAnchor.MiddleLeft);if(stamp!=1)U.Pop();px=r.x-7;
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
                // The others break apart and the chosen card flies at the pilot and bursts into the 3D cartridge (NativeInstallShot); the
                // pilot installs it (the install cue lands on the clack, NativeAudio), then the details unfold beside the pilot.
                PickedTray(tray,chips,index,seat.chips.TryGetValue(chosen.id,out int c)?c-1:0,age);InstallFlash(age);
                float details=age-NativeInstallShot.DetailsAt;if(details>=0)Spotlight(chosen,seat,details,DraftRevealSeconds(chosen.id)-NativeInstallShot.DetailsAt,NativeInstallShot.DetailsBeside,age);
            }
            if(chosen==null||age<NativeInstallShot.DetailsAt)Nameplate(seat,chosen!=null?(installed?"Installed":"Installing"):own?"Your pick":"Picking",H<=760?22:28,H-(H<=760?14:22));
        }
        // The heading through the install: "Installing <chip>" (another's pick: "<name> is installing"; too wide: "Installing") until the
        // clack, then "Upgrade secured" typed in over .2 s.
        private static string InstallTitle(ContentChip chip,NativeSeat seat,bool own,float age,float W,float H)
        {
            const string done="Upgrade secured";int typed=NativeInstallShot.Secured(age,done.Length);if(typed>=0)return done.Substring(0,Math.Max(1,typed));
            string t=own?"Installing "+chip.name:seat.name+" is installing";float F=H<=760?24:Mathf.Clamp(W*.025f,24,34);return U.Measure(t,F,true).x>W*.58f?"Installing":t;
        }
        // The clack's two-frame flash over the screen (none with Reduced effects).
        private void InstallFlash(float age){if(prefs.reducedEffects||!(age>=NativeInstallShot.Seat)||age>=NativeInstallShot.Seat+2/60f)return;U.Fill(new Rect(0,0,U.W,U.H),new Color(1,1,1,.12f));}
        private Tray draftTray;private float draftTrayTop;private Vector2 draftTraySize;
        // A tray after its pick, `age` seconds on: the others break apart nearest first, the chosen card (`copies` owned before it) flies
        // at the pilot and bursts into the 3D cartridge (NativeInstallShot.Burst). It leaves from its highlight as it was at the pick (DrawTray
        // skips it, so its level holds; a spectator's is 0).
        private void PickedTray(Tray tray,IList<ContentChip> chips,int index,int copies,float age)
        {
            var from=index>=0&&index<tray.Cards.Length?tray.Cards[index].center:new Vector2(U.W/2,U.H*.8f);float w=index>=0&&index<tray.Cards.Length?tray.Cards[index].width:160;
            for(int i=0;i<chips.Count&&i<tray.Cards.Length;i++)if(i!=index)
            {
                int d=Mathf.Max(1,Mathf.RoundToInt((tray.Cards[i].center-from).magnitude/Mathf.Max(60,w+14)));Shatter(tray.Cards[i],chips[i],age,NativeInstallShot.ShatterAt(d),i,tray.Size,from);
            }
            if(index>=0&&index<tray.Cards.Length)FlyToPalm(tray.Cards[index],chips[index],copies,age,tray.Size,index<cardHolo.Length?cardHolo[index]:0);
        }
        // The cards' fracture: eight chamfered shards about a centre a little off the middle (fractions of the card: x right, y down).
        private static readonly Vector2[][] Fracture=
        {
            new[]{new Vector2(0,0),new Vector2(.42f,0),new Vector2(.55f,.45f),new Vector2(0,.3f)},new[]{new Vector2(.42f,0),new Vector2(1,0),new Vector2(1,.22f),new Vector2(.55f,.45f)},
            new[]{new Vector2(1,.22f),new Vector2(1,.62f),new Vector2(.55f,.45f)},new[]{new Vector2(1,.62f),new Vector2(1,1),new Vector2(.7f,1),new Vector2(.55f,.45f)},
            new[]{new Vector2(.7f,1),new Vector2(.3f,1),new Vector2(.55f,.45f)},new[]{new Vector2(.3f,1),new Vector2(0,1),new Vector2(0,.74f),new Vector2(.55f,.45f)},
            new[]{new Vector2(0,.74f),new Vector2(0,.3f),new Vector2(.55f,.45f)},new[]{new Vector2(.42f,0),new Vector2(.55f,.45f),new Vector2(.22f,.15f)},
        };
        private static readonly Vector2[] FractureCentre=Fracture.Select(s=>new Vector2(s.Average(v=>v.x),s.Average(v=>v.y))).ToArray();
        private static readonly float[] FlatAt={0,1},FlatAlpha={1,1},SideAt={0,.55f,1},SideAlpha={0,.82f,.82f},VignetteAt={0,.6f,.85f,1},VignetteAlpha={0,0,.6f,1};
        private static readonly Vector2[] SideShade={new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
        // A line of light from a to b (screen px), `width` wide.
        private static void Beam(Vector2 a,Vector2 b,float width,Color c){var d=b-a;float l=d.magnitude;if(l<.5f||c.a<=0)return;var mid=(a+b)*.5f;U.Push(mid,Vector2.zero,1,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);U.Fill(new Rect(mid.x-l*.5f,mid.y-width*.5f,l,width),c);U.Pop();}
        // A card breaking (NativeInstallShot.ShatterAt, its own .70 s from `start`): the lock's white crack along its fracture (every card at
        // once), its cracks brightening in its rarity, a flash at 18%, then eight shards with lit rims and hot inner edges flying away from
        // the chosen card (`from`) with spin, fake depth and gravity, ten sparks off the crack ends. Reduced effects: four shards, no sparks.
        private void Shatter(Rect r,ContentChip chip,float age,float start,int index,ChipSize size,Vector2 from)
        {
            float t=age-start,k=t/NativeInstallShot.ShatterSpan;bool calm=prefs.reducedEffects;var rare=NativeChipCard.Rarity(chip.rarity);var centre=new Vector2(r.x+r.width*.55f,r.y+r.height*.45f);
            if(k>=1)return;
            if(k<.18f)
            {
                NativeChipCard.Draw(r,chip,0,size,ChipLook.Idle(Time.unscaledTime));
                // The cracks: a white line at the lock (all cards), then the card's own in its rarity, brightening.
                float lockLine=age<1/30f?1:0,own=k<0?0:Mathf.Clamp01(k/.10f);var c=lockLine>0?new Color(1,1,1,.9f):U.A(rare,.85f*own);
                if(c.a>0)for(int s=0;s<Fracture.Length;s++){var v=Fracture[s];Beam(centre,new Vector2(r.x+r.width*v[v.Length>3?1:0].x,r.y+r.height*v[v.Length>3?1:0].y),1.4f,c);}
                if(k>.08f)NativeChipArt.Package(r,size.Radius,0,new Color(1,1,1,.5f*Mathf.Clamp01((k-.08f)/.1f)));if(k>.12f)U.Glow(r,U.A(rare,.8f*(k-.12f)/.06f),18);
                return;
            }
            float u=M.Bezier(.15f,.65f,.3f,1,(k-.18f)/.82f),a=1-u,glow=Mathf.Lerp(2.4f,.5f,u);var away=(r.center-from);away=away.sqrMagnitude>1?away.normalized:Vector2.right;
            // (Each shard a fragment of the cartridge: its body, the bezel's metal lit by the tier's light at its rim.)
            int shards=calm?4:Fracture.Length;var body=U.Hex(0x0d1018);var rim=U.Mix(U.Hex(NativeChipCartridge.Of(chip.rarity).MetalMid),rare,.45f);
            for(int s=0;s<shards;s++)
            {
                int n=calm?s*2:s;var c=FractureCentre[n];var pivot=new Vector2(r.x+r.width*c.x,r.y+r.height*c.y);var radial=(pivot-centre);radial=radial.sqrMagnitude>1?radial.normalized:away;
                float seed=NativeEntranceMath.Rand(9100+index,n),t2=t-NativeInstallShot.ShatterSpan*.18f;var move=(radial*.6f+away*.7f).normalized*(160+120*seed)*u+(calm?Vector2.zero:new Vector2(0,450*t2*t2));
                float spin=(seed>.5f?1:-1)*(25+70*seed)*u,depth=calm?1:Mathf.Abs(Mathf.Cos((1.5f+2*seed)*u));
                U.Push(pivot,move,(1-.25f*u)*Mathf.Max(.15f,depth),spin,1-.25f*u);
                U.Polygon(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),Fracture[n],FlatAt,FlatAlpha,U.A(U.Mix(rim,rare,Mathf.Clamp01(glow-.5f)),.9f*a*Mathf.Min(1,glow)));
                U.Polygon(r,Fracture[n],FlatAt,FlatAlpha,U.A(U.Mix(body,rare,.12f*glow),a));
                U.Pop();
            }
            if(!calm&&t<NativeInstallShot.ShatterSpan*.18f+.25f)
            {
                float st=(t-NativeInstallShot.ShatterSpan*.18f)/.25f;if(st>=0)for(int s=0;s<10;s++)
                {
                    float seed=NativeEntranceMath.Rand(9200+index,s),ang=(s/10f+seed*.08f)*Mathf.PI*2;var dir=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));var head=centre+dir*(20+140*st*(.6f+.4f*seed));
                    Beam(head-dir*(10+14*(1-st)),head,1.6f,U.A(U.Mix(rare,Color.white,st),(1-st)*.9f));
                }
            }
        }
        // The chosen card's flight (NativeInstallShot.CardAt): it pops at the lock (1 to 1.07 and back over .10 s, its bezel's light running
        // round once, the die's flare), then from FlyStart flies on a rising arc at the catch (draftCatch, re-projected each frame as the
        // camera moves), banking and shrinking so that at the burst its width is the 3D cartridge's there, three ghosts trailing a frame
        // apart; at the burst it flashes white and its frame breaks into six lit shards, the 3D cartridge taking over in the same place.
        private void FlyToPalm(Rect r,ContentChip chip,int copies,float age,ChipSize size,float level=0)
        {
            bool calm=prefs.reducedEffects;var rare=NativeChipCard.Rarity(chip.rarity);float W=U.W,H=U.H;Vector2 target=draftCatch!=Vector2.zero?draftCatch:draftPalm;if(target==Vector2.zero)target=r.center;
            float end=presentation.DraftBursting&&presentation.DraftBurstWidth>0?presentation.DraftBurstWidth*W/Mathf.Max(1,r.width):.18f;
            if(age<NativeInstallShot.Burst)
            {
                for(int g=calm?0:3;g>=0;g--)
                {
                    float a=age-g/60f;if(g>0&&a<NativeInstallShot.FlyStart)continue;var at=NativeInstallShot.CardAt(new System.Numerics.Vector2(r.center.x/W,r.center.y/H),new System.Numerics.Vector2(target.x/W,target.y/H),a);
                    float pop=1+.07f*NativeInstallShot.Bump(age/.10f),scale=NativeInstallShot.CardScale(a,end)*(g==0?pop:1),fly=NativeInstallShot.Fly(a),alpha=g==0?1:g==1?.4f:g==2?.25f:.12f;
                    U.Push(r.center,new Vector2(at.X*W,at.Y*H)-r.center+new Vector2(0,NativeHoloScan.Lift(level)*(1-fly)),scale,NativeInstallShot.FlyBank*fly);
                    U.Glow(r,U.A(rare,.5f*alpha),36);NativeChipArt.PackageEdge(r,size.Radius,2,U.A(rare,alpha*(1+NativeInstallShot.Bump(age/.12f))),2);
                    var look=ChipLook.Idle(Time.unscaledTime);look.Alpha=alpha;look.Plain=true;look.Calm=calm;look.Charge=NativeHoloScan.DieRest*Mathf.Clamp01(level)+NativeHoloScan.LockFlare(age,calm);NativeChipCard.Draw(r,chip,copies,size,look);
                    if(g==0)NativeChipArt.HoloLock(r,size.Radius,chip.rarity,rare,age,calm,1);U.Pop();
                }
                return;
            }
            // The burst: the white flash (two frames) and the frame's six shards.
            float t=age-NativeInstallShot.Burst;if(t>.32f)return;var hand=NativeInstallShot.CardAt(new System.Numerics.Vector2(r.center.x/W,r.center.y/H),new System.Numerics.Vector2(target.x/W,target.y/H),NativeInstallShot.Burst);var c=new Vector2(hand.X*W,hand.Y*H);
            float s=end;if(t<2/60f){U.Push(r.center,c-r.center,s,NativeInstallShot.FlyBank);U.Glow(r,new Color(1,1,1,.9f),24);U.Round(r,new Color(1,1,1,.85f),size.Radius);U.Pop();}
            float u=t/.32f;
            for(int k=0;k<(calm?3:6);k++)
            {
                float ang=(k/6f+.08f)*Mathf.PI*2;var dir=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));var at=c+dir*(r.width*s*.5f+90*u);
                Beam(at-dir*(r.width*s*.4f*(1-u)),at,2,U.A(U.Mix(rare,Color.white,.4f),(1-u)*(1-u)));
            }
        }
        // .selection-spotlight: the focus block (owner, big card, confirm line) over the reveal hold. Beside the pilot (`beside`, the PvP
        // install's default: NativeInstallShot.DetailsBeside): the block unfolds from a line of light at the screen's right (ReadoutX, its card
        // at most .42 of the width), a leader of light from the device, a side shade from the block to the pilot (the pilot keeps the left);
        // else centred over a radial shade, as before.
        private void Spotlight(ContentChip chip,NativeSeat owner,float t,float duration,bool beside=false,float age=-1)
        {
            if(duration<=0||t>duration)return;float k=t/duration,W=U.W,H=U.H,env=k<.05f?0:k<.16f?(k-.05f)/.11f:k<.82f?1:1-(k-.82f)/.18f;
            float unfold=1,aspect=W/Mathf.Max(1,H);if(beside){env=k<.82f?1:1-(k-.82f)/.18f;unfold=NativeInstallShot.Unfold(age);}
            float shift=beside&&age>=0?NativeInstallShot.ReadoutShift(aspect,age)*W:0,cx=beside?NativeInstallShot.ReadoutX(aspect)*W+shift:W/2;
            if(beside)
            {
                float pilot=NativeInstallShot.PilotX(aspect)*W+shift;U.Polygon(new Rect(pilot,0,W-pilot,H),SideShade,SideAt,SideAlpha,U.Hex(0x080b16,env));
                U.RadialStops(new Rect(-W*.207f,-H*.207f,W*1.414f,H*1.414f),U.Hex(0x080b16,env*.2f),VignetteAt,VignetteAlpha);
            }
            else U.RadialStops(new Rect(-W*.207f,-H*.207f,W*1.414f,H*1.414f),U.Hex(0x080b16,env),new[]{0,.4f,.78f,1},new[]{.93f,.8f,.2f,.2f});
            Func<float,float> ease=x=>M.Bezier(.16f,.8f,.22f,1,x);float dy,sc;if(beside){dy=k<.82f?0:-24*ease((k-.82f)/.18f);sc=1;}else if(k<.05f){dy=35;sc=.72f;}else if(k<.2f){float u=ease((k-.05f)/.15f);dy=35*(1-u);sc=.72f+.28f*u;}else if(k<.82f){dy=0;sc=1;}else{float u=ease((k-.82f)/.18f);dy=-24*u;sc=1+.06f*u;}
            string tier=chip.rarity;float bw=Mathf.Min(tier==NativeBombChips.Tier||tier==NativeDogChips.Tier||tier==NativeGlaiveChips.Tier||tier==NativeKatanaChips.Tier||tier==NativeFangsChips.Tier||NativeBossChips.IsTier(tier)?480:tier=="l44t"?500:tier=="ansatsuken"?490:tier=="siren"?480:tier=="mishima"?470:440,W*(beside?.42f:.9f));var size=ChipSize.Spotlight;float ch=Mathf.Max(260,NativeChipCard.Height(chip,bw,size));
            float blockH=25*1.2f+9+15+24+ch+25+16,bx=cx-bw/2,by=(H-blockH)/2;var rare=NativeChipCard.Rarity(tier);
            if(beside)
            {
                // The leader from the device to the block's near edge, drawn as the block unfolds; the line of light the block opens from.
                var dev=draftDevice2d;float lead=NativeInstallShot.Leader(age);if(dev!=Vector2.zero&&lead>0){var to=new Vector2(bx-14,H/2);Beam(dev,Vector2.Lerp(dev,to,lead),2,U.A(rare,.8f*env));U.Glow(new Rect(dev.x-4,dev.y-4,8,8),U.A(rare,.7f*env),10);}
                if(unfold<1){U.Fill(new Rect(bx-20,H/2-1.5f,bw+40,3),U.A(U.Mix(rare,Color.white,.5f),(1-unfold)*env));U.Glow(new Rect(bx,H/2-2,bw,4),U.A(rare,(1-unfold)*.8f*env),18);}
            }
            U.Push(new Vector2(cx,H/2),new Vector2(0,dy),sc,0,sc*unfold);
            U.Text(new Rect(bx-40,by,bw+80,30),owner.name,25,U.A(Color.white,env),TextAnchor.MiddleCenter,bold:true,shadow:new Color(0,0,0,env));
            U.Text(new Rect(bx,by+39,bw,15),"INSTALLED",12,U.A(tier=="siren"?U.Hex(0xc9bfff):rare,env),TextAnchor.MiddleCenter,mono:true,tracking:.35f);
            var card=new Rect(bx,by+39+15+24,bw,ch);U.Glow(card,U.A(rare,.32f*env),60);U.Glow(new Rect(card.x,card.y+20,card.width,card.height),new Color(0,0,0,.6f*env),80);
            int copies=owner.chips.TryGetValue(chip.id,out int n)?n:1;var look=ChipLook.Idle(Time.unscaledTime);look.Alpha=env;look.Chosen=true;bool bomb=tier==NativeBombChips.Tier;var knock=bomb?NativeBombChipArt.Shake(chip.id,t,prefs.reducedEffects):Vector2.zero;if(bomb)U.Push(card.center,knock);bool dog=tier==NativeDogChips.Tier;if(dog){NativeDogChipArt.Pose(chip.id,t,prefs.reducedEffects,out var dk,out float dsx,out float dsy,out float ddeg);U.Push(new Vector2(card.center.x,card.yMax),dk,dsx,ddeg,dsy);}bool ring=tier==NativeGlaiveChips.Tier;if(ring){NativeGlaiveChipArt.Pose(chip.id,t,prefs.reducedEffects,out var gk,out float gsx,out float gsy,out float gdeg);U.Push(new Vector2(card.center.x,card.yMax),gk,gsx,gdeg,gsy);}bool blade=tier==NativeKatanaChips.Tier||tier==NativeFangsChips.Tier;if(blade){NativeBladeChipArt.Pose(chip.id,t,prefs.reducedEffects,out var bk,out float bsx,out float bsy,out float bdeg);U.Push(new Vector2(card.center.x,card.yMax),bk,bsx,bdeg,bsy);}NativeChipCard.Draw(card,chip,Math.Max(0,copies-1),size,look);if(dog){U.Pop();NativeDogChipArt.Reveal(card,chip.id,t,env,prefs.reducedEffects);}if(ring){U.Pop();NativeGlaiveChipArt.Reveal(card,chip.id,t,env,prefs.reducedEffects);}if(blade){U.Pop();NativeBladeChipArt.Reveal(card,chip.id,t,env,prefs.reducedEffects);}if(bomb){U.Pop();NativeBombChipArt.Reveal(card,chip.id,t,env,prefs.reducedEffects);}if(NativeBossChips.IsTier(tier))NativeBossChipArt.Reveal(card,chip.id,t,env,prefs.reducedEffects);
            string confirm=NativeBossChips.IsTier(tier)?NativeBossChips.Sets[NativeBossChips.SetOf(tier)].Confirm:tier==NativeBombChips.Tier?NativeBombChips.Confirm(chip.id):tier==NativeDogChips.Tier?NativeDogChips.Confirm(chip.id):tier==NativeGlaiveChips.Tier?NativeGlaiveChips.Confirm(chip.id):tier==NativeKatanaChips.Tier?NativeKatanaChips.Confirm(chip.id):tier==NativeFangsChips.Tier?NativeFangsChips.Confirm(chip.id):tier=="mishima"?"POWER WITHOUT LIMIT":tier=="l44t"?"ROOT ACCESS GRANTED":tier=="ansatsuken"?"THE WARRIOR AWAKENS":tier=="siren"?"THE GRIN WIDENS":"UPGRADE SECURED",times="×"+copies;
            float cw=U.Measure(confirm,13,false,true,.2f).x,xw=U.Measure(times,13,true,true,.2f).x,ccx=cx-(cw+12+xw)/2,cy=card.yMax+25;Color cc=NativeBossChips.IsTier(tier)?U.Hex(NativeBossChips.Sets[NativeBossChips.SetOf(tier)].Label):tier==NativeDogChips.Tier?U.Hex(0xffcc22):tier==NativeGlaiveChips.Tier?U.Hex(0xb8f6ff):tier==NativeKatanaChips.Tier?U.Hex(0xffd0dc):tier==NativeFangsChips.Tier?U.Hex(0xd4ff9e):tier=="l44t"?U.Hex(0x9effbd):tier=="ansatsuken"?U.Hex(0xffb2bc):tier=="mishima"?U.Hex(0xff8199):tier=="siren"?U.Hex(0xd9d0ff):U.Hex(0xd8e4ef);
            U.Text(new Rect(ccx,cy,cw+4,16),confirm,13,U.A(cc,env),TextAnchor.MiddleLeft,mono:true,tracking:.2f);U.Text(new Rect(ccx+cw+12,cy,xw+4,16),times,13,U.A(rare,env),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            // The unfold's scanlines over the block.
            if(beside&&unfold<1)for(int s=0;s<8;s++){float yy=by+blockH*(s+.5f)/8;U.Fill(new Rect(bx,yy,bw,1.5f),U.A(rare,.25f*(1-unfold)*env));}
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
            int opening=0;foreach(var openingPick in row.Picks)if(openingPick.Source==NativeBossPicks.Source)opening++;
            string source=arena.BossOpening||pick.Source==NativeBossPicks.Source?"BOSS BATTLE · CHIP "+(NativeBossPicks.Count-opening+1)+" / "+NativeBossPicks.Count:arena.ZoneBoss?.fallen!=null?"ANSWER NOCTIEL · PHASE "+arena.ZoneBoss.phase:rainbow?"RAINBOW · 8 MAX-RARITY CHOICES":pick.Source=="start"?"BEGINNING CHIP":(pick.Source=="wave"?"WAVE REWARD · ":"DISPENSED · ")+TierName(pick.Tier).ToUpperInvariant();
            DraftShades(pveTray,W,H);DraftHeader(source,tier,"Make your next move","Choose one Chip. This choice is yours alone.",W,H,out float top);
            // Meta (top right): the queue and the vulnerability warning.
            float x=W-28,y=22;string warn="YOU ARE VULNERABLE WHILE CHOOSING";if(arena.BossOpening)warn="FIGHT PAUSED · ALL PLAYERS CHOOSE 5 CHIPS";else if(arena.ZoneBoss?.fallen!=null)warn="FIGHT PAUSED · "+Math.Max(0,(int)Math.Ceiling(arena.ZoneBoss.fallen.resumeAt-arena.Time))+"s";float ww=U.Measure(warn,11,true,true,.08f).x;U.Text(new Rect(x-ww,y,ww+2,24),warn,11,U.Hex(0xd8dae4),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
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
