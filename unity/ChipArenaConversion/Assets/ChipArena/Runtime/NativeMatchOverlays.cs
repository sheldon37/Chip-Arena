using System;
using System.Linq;
using UnityEngine;
using M=ChipArena.UnityConversion.NativeUiMotion;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // Match-flow overlays ported from the source DOM/CSS (main.ts drawHud, style.css visual revision, pve/pve.css). Every
    // animation samples an authoritative clock (intro age, phase remaining), as the source's negative CSS delays do, so a
    // joining or replaying client lands on the same frame. Layout is in the shared virtual canvas (NativeUi.Begin).
    public sealed partial class ConversionGame
    {
        private static readonly int[] TeamHex={0xff6575,0x69a6ff,0x7ed78a,0xf5db70,0xba94f5,0xffa565,0x65dce0,0xf18dcc};
        private static int TeamIndex(int team)=>((team%8)+8)%8;
        private static Color TeamColor(int team)=>U.Hex(TeamHex[TeamIndex(team)]);
        private static Color SeatColor(NativeSeat s)=>ColorUtility.TryParseHtmlString(s?.accentColor??"",out var c)?c:U.Hex(0xdce7f5);
        private static float Line(float age,float start,float duration=.55f)=>M.Bezier(.16f,.84f,.24f,1,M.Progress(age,start,duration));
        private bool OnMySide(NativeSeat s){var me=arena?.Local?.Seat;return s!=null&&me!=null&&(match.Teams?s.team==me.team:s.id==me.id);}

        // ---- Intro (style.css .intro-*, pve.css boss variant) ----
        private void IntroOverlay()
        {
            var pilot=IntroPilot;if(pilot==null)return;var order=match.Seats.Where(s=>s.connected&&!s.AuthorityBoss).ToList();
            IntroCard(pilot,IntroStyle,IntroAge,match.Phase!=NativePhase.Intro,Math.Max(0,order.FindIndex(s=>s.id==pilot.Seat.id)),Math.Max(1,order.Count),match.Teams&&!match.Cooperative);
        }
        // The overlay for one pilot's entrance at `rawAge`: the match intro, and the intro preview from Customize (index 0
        // of 1, no team line).
        private void IntroCard(NativePilot pilot,string style,float rawAge,bool boss,int index,int total,bool team)
        {
            float duration=(float)NativeIntroRules.OverlayDuration(style),nameAt=(float)NativeIntroRules.NameAt(style),age=Mathf.Min(rawAge,duration),W=U.W,H=U.H;
            var ink=U.Hex(boss?0x020103:0x040610);
            U.Fade(new Rect(0,0,W,H*(boss?.2f:.17f)),U.A(ink,.867f));float lower=boss?.6f:.7f;U.Fade(new Rect(0,H*lower,W,H*(1-lower)+1),U.A(ink,boss?.96f:.94f),false);
            // :before — radial-gradient(ellipse 60% 92% at 50% 100%) over the bottom 46vh, in over .7 s at the name time.
            float shade=M.EaseOut(M.Progress(age,nameAt,.7f)),ry=H*.46f*.92f;U.RadialStops(new Rect(-W*.1f,H-ry,W*1.2f,ry*2),U.A(U.Hex(0x040610),shade),new[]{0,.46f,.76f},new[]{.96f,.70f,0});
            // Topline at 88 px with 6vw margins; a 1 px rule and a progress bar that fills linearly over the intro.
            float top=M.EaseOut(M.Progress(age,0,.45f)),x0=W*.06f,tw=W*.88f,y=88;
            string left=boss?"BOSS":"MATCH INTRO / "+(index+1).ToString("00")+" — "+total.ToString("00"),right=boss?"THE SIREN":"";
            U.Text(new Rect(x0,y,tw,15),left,12,U.A(U.Hex(boss?0xd9d0ff:0xd7d8de),top),TextAnchor.UpperLeft,mono:true,tracking:.2f);
            // Owner 2026-10-12: "Remove name of intros during intro": a pilot entrance no longer names its intro style (the boss keeps THE SIREN).
            if(right.Length>0)U.Text(new Rect(x0,y,tw,15),right,12,U.A(U.Hex(boss?0xc9bfff:0xff996f),top),TextAnchor.UpperRight,mono:true,tracking:boss?.42f:.2f);
            float rule=y+29;U.Fill(new Rect(x0,rule,tw,1),U.A(U.Hex(boss?0xb7a8ff:0xff6b35),.25f*top));float fill=Mathf.Clamp01(age/duration);
            if(fill>0)U.HGradient(new Rect(x0,rule,tw*fill,1),U.A(U.Hex(boss?0x7d5cff:0xff6b35),top),U.A(U.Hex(boss?0xf6f3ff:0xffb15c),top));
            if(age<nameAt)return;
            // .intro-name: bottom 5vh, 90% wide; each line rises 14 px in over .55 s, staggered; the block leaves at duration-.35 s.
            float exit=M.Bezier(.4f,0,1,1,M.Progress(age,duration-.35f,.34f)),alpha=1-exit,bottom=H*.95f-10*exit,cx=W*.05f,cw=W*.9f;
            if(boss){BossTitle(pilot,age,nameAt,alpha,bottom,cx,cw);return;}
            var seat=pilot.Seat;string weapon=(content.weapons.FirstOrDefault(w=>w.id==seat.spec)?.name??seat.spec).ToUpperInvariant(),text=(string.IsNullOrWhiteSpace(seat.introText)?"Ready to make an impact":seat.introText).ToUpperInvariant();
            float F=Mathf.Clamp(W*.065f,30,82);float fit=U.Measure(seat.name,F,true,false,-.035f).x;if(fit>cw)F*=cw/fit;
            float ySpan=bottom-18,yStrong=ySpan-9-1.15f*F,yTeam=yStrong-13-18,ySmall=(team?yTeam-8:yStrong-9)-15;var shadow=new Color(0,0,0,.8f*alpha);
            // intro-line-in: each line also sharpens from blur(6px).
            float e=Line(age,nameAt);U.Text(new Rect(cx,ySmall+14*(1-e),cw,15),weapon,12,U.A(U.Hex(0xffd3bf),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.3f,shadow:U.A(shadow,e),blur:6*(1-e));
            if(team){e=Line(age,nameAt+.07f);U.Text(new Rect(cx,yTeam+14*(1-e),cw,18),NativeMatch.TeamNames[TeamIndex(seat.team)].ToUpperInvariant()+" TEAM",14,U.A(TeamColor(seat.team),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.12f,shadow:U.A(shadow,e),blur:6*(1-e));}
            // intro-name-in .72 s: up 22 px from scale .96 while the tracking closes from .14em to -.035em and blur(10px) clears by
            // 60% (its own eased segment).
            float n=Line(age,nameAt+.12f,.72f),nb=10*(1-Line(age,nameAt+.12f,.72f*.6f));U.Push(new Vector2(W/2,yStrong+.575f*F),new Vector2(0,22*(1-n)),.96f+.04f*n);
            U.Text(new Rect(cx,yStrong,cw,1.15f*F),seat.name,F,U.A(U.Hex(0xf6fcff),alpha*n),TextAnchor.MiddleCenter,bold:true,tracking:.14f-.175f*n,glow:U.A(SeatColor(seat),.65f*alpha*n),blur:nb);U.Pop();
            e=Line(age,nameAt+.24f);U.Text(new Rect(cx,ySpan+14*(1-e),cw,18),text,12,U.A(U.Hex(0xb9bac4),alpha*e),TextAnchor.UpperCenter,tracking:.3f,blur:6*(1-e));
        }
        // pve/hud.ts bossIntroName: "BOSS · WARD ZERO", the per-letter THE SIREN title with its red/cyan glitch, and the line.
        private void BossTitle(NativePilot pilot,float age,float nameAt,float alpha,float bottom,float cx,float cw)
        {
            float W=U.W,F=Mathf.Clamp(W*.086f,48,132),lh=.95f*F,yLine=bottom-21,yTitle=yLine-10-lh,ySmall=yTitle-6-15;var shadow=new Color(0,0,0,.8f*alpha);
            float e=Line(age,nameAt);U.Text(new Rect(cx,ySmall+14*(1-e),cw,15),"BOSS · WARD ZERO",12,U.A(U.Hex(0xc9bfff),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.5f,shadow:U.A(shadow,e),blur:6*(1-e));
            string title=(pilot.Seat.name??"The Siren").ToUpperInvariant();float gap=.06f*F;var widths=new float[title.Length];float total=0;
            for(int i=0;i<title.Length;i++){widths[i]=title[i]==' '?.3f*F:U.Measure(title[i].ToString(),F,true).x;total+=widths[i]+(i>0?gap:0);}
            // siren-title-jolt every 2.4 s from name+1.2 s (steps): -6 / +5 / -2 px at 87 / 89 / 91 %.
            float jolt=0;if(age>=nameAt+1.2f){float c=Mathf.Repeat(age-nameAt-1.2f,2.4f)/2.4f;jolt=c>=.87f&&c<.89f?-6:c>=.89f&&c<.91f?5:c>=.91f&&c<.94f?-2:0;}
            // siren-split-a/-b on the title's box (2.4 s, from name+.9 / +1 s): banded red and cyan copies of each letter.
            float Cycle(float start)=>age<start?-1:Mathf.Repeat(age-start,2.4f)/2.4f;float red=Cycle(nameAt+.9f),cyan=Cycle(nameAt+1);
            float x=W/2-total/2+jolt;var block=new Rect(x,yTitle,total,lh);
            for(int i=0;i<title.Length;i++)
            {
                if(title[i]==' '){x+=widths[i]+gap;continue;}
                // siren-letter-in .5 s per letter, 38 ms apart: rises from 40% at scaleY 1.8, overshoots left then right, settles by 70%.
                float u=Line(age,nameAt+i*.038f,.5f),seg=Mathf.Clamp01(u/.55f),op=Mathf.Clamp01(u/.4f)*alpha;
                float tx=u<.55f?-4*seg:u<.62f?-4+7*((u-.55f)/.07f):u<.7f?3-3*((u-.62f)/.08f):0;var box=new Rect(x,yTitle,widths[i]+2,lh);string ch=title[i].ToString();
                U.Push(box.center,new Vector2(tx,.4f*lh*(1-seg)),1,0,1.8f-.8f*seg);
                U.SplitCopy(block,box,red,false,ch,F,op,TextAnchor.MiddleLeft);U.SplitCopy(block,box,cyan,true,ch,F,op,TextAnchor.MiddleLeft);
                U.Text(box,ch,F,U.A(U.Hex(0xf6f3ff),op),TextAnchor.MiddleLeft,bold:true,glow:U.A(U.Hex(0x9b87ff),.8f*op),shadow:U.A(Color.black,op));U.Pop();
                x+=widths[i]+gap;
            }
            e=Line(age,nameAt+.24f);U.Text(new Rect(cx,yLine+14*(1-e),cw,21),"IT WAS WAITING BEHIND THE GREAT DOORS",14,U.A(U.Hex(0xd9d0ff),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.24f,shadow:U.A(shadow,e),italic:true,blur:6*(1-e));
        }

        // ---- Countdown (.countdown, count-tick) ----
        private void CountdownOverlay()
        {
            float remaining=(float)(match.PhaseEnd-match.Time);int tick=Mathf.CeilToInt(remaining);if(tick<=0)return;
            // count-tick 1 s: transparent at 1.5x, opaque at 1x by 18%, settling to .93x (cubic-bezier(.2,.9,.25,1) per segment).
            float t=Mathf.Clamp01(tick-remaining),a,s,blur=0;if(t<.18f){float u=M.Bezier(.2f,.9f,.25f,1,t/.18f);a=u;s=1.5f-.5f*u;blur=6*(1-u);}else{a=1;s=1-.07f*M.Bezier(.2f,.9f,.25f,1,(t-.18f)/.82f);}
            float W=U.W,top=U.H*.38f-65;U.Push(new Vector2(W/2,top+52),Vector2.zero,s);
            U.Text(new Rect(0,top,W,104),tick.ToString(),104,NativeCyberTheme.Ink(a),TextAnchor.MiddleCenter,bold:true,tracking:-.04f,glow:NativeCyberTheme.Cyan(.5f*a),shadow:new Color(0,0,0,.6f*a),blur:blur);U.Pop();
            U.Text(new Rect(0,top+114,W,17),"GET READY",13,NativeCyberTheme.CyanSoft(),TextAnchor.UpperCenter,bold:true,tracking:.32f,shadow:new Color(0,0,0,.8f));
        }

        // ---- Round result (.result-sequence) and the set-score overview it shows ----
        private void ResultOverlay()
        {
            float left=Mathf.Max(0,(float)(match.PhaseEnd-match.Time)),age=5-left,W=U.W,H=U.H;var winner=match.Seats.Find(s=>s.id==match.Winner);bool won=OnMySide(winner),final=match.Seats.Any(s=>s.score>=match.SetsToWin);
            string title=won?(final?"MATCH SECURED":match.SetWinner!=0?"SET SECURED":"ROUND SECURED"):winner!=null?"KNOCKOUT":"DOUBLE KNOCKOUT";
            float F=Mathf.Clamp(W*.046f,34,64),h2=Mathf.Clamp(W*.014f,15,19),bw=Mathf.Min(680,W*.85f),bx=(W-bw)/2;
            // result-arrive .45 s from 12 px above; result-word .6 s closes the heading's tracking from .4em to .08em.
            float arrive=M.Bezier(.16f,.8f,.2f,1,M.Progress(age,0,.45f)),word=M.Bezier(.16f,.8f,.2f,1,M.Progress(age,0,.6f)),by=H*.19f-12*(1-arrive);
            // The band: navy glass fading out at both ends, thin cyan rules along its top and bottom; the title in cyan for my
            // side's win, danger for a knockout, ink for a double knockout, with the arrival glitch (none with Reduced effects).
            float bh=16+F+12+h2*1.35f+6+8+20+10+16;var band=NativeCyberTheme.Navy(.9f*arrive);
            U.HGradient(new Rect(bx,by,bw*.22f,bh),Color.clear,band);U.Fill(new Rect(bx+bw*.22f,by,bw*.56f,bh),band);U.HGradient(new Rect(bx+bw*.78f,by,bw*.22f,bh),band,Color.clear);
            Color tc=won?NativeCyberTheme.Cyan():winner!=null?NativeCyberTheme.Danger():NativeCyberTheme.Ink();
            foreach(float ry in new[]{by,by+bh-NativeHudChrome.Snap(1)}){U.HGradient(new Rect(bx,ry,bw*.5f,NativeHudChrome.Snap(1)),Color.clear,U.A(tc,.75f*arrive));U.HGradient(new Rect(bx+bw*.5f,ry,bw*.5f,NativeHudChrome.Snap(1)),U.A(tc,.75f*arrive),Color.clear);}
            var titleLine=new Rect(bx,by+16,bw,F);NativeHudChrome.Glitch(titleLine,title,F,age-.12f,word*arrive,TextAnchor.MiddleCenter,prefs.reducedEffects,true,false,.4f-.32f*word);
            U.Text(titleLine,title,F,U.A(tc,word*arrive),TextAnchor.MiddleCenter,bold:true,tracking:.4f-.32f*word,glow:U.A(tc,.3f*word*arrive),shadow:new Color(0,0,0,.66f*word*arrive),blur:6*(1-word));
            float y=by+16+F+12;U.Text(new Rect(bx+28,y,bw-56,h2*1.35f),match.Result,h2,NativeCyberTheme.Ink(arrive),TextAnchor.MiddleCenter,bold:true);y+=h2*1.35f+14;
            string next=(final?"Match results":match.SetWinner!=0?"Chip selection":winner!=null?"Next round":"Round replay")+" in "+Mathf.CeilToInt(left)+"s";
            U.Text(new Rect(bx,y,bw,20),next,13,NativeCyberTheme.Muted(arrive),TextAnchor.MiddleCenter,mono:true);
            // p:after — a 2 px bar over the middle half that shrinks to nothing over the result's remaining time.
            float bar=bw*.5f*Mathf.Clamp01(left/5);U.Fill(new Rect(bx+bw*.25f,y+28,bw*.5f,2),NativeCyberTheme.Edge(.6f*arrive));U.Fill(new Rect(bx+bw*.25f,y+28,bar,2),NativeCyberTheme.Cyan(.85f*arrive));
            MatchOverview();
        }
        // match-score.ts matchScoreHud, top right (match-hud.css): set circles per pilot or team, then who is still alive.
        private void MatchOverview()
        {
            float W=U.W,w=310,x=W-20-w,y=62;var me=arena.Local.Seat;var players=match.Seats.Where(s=>!s.AuthorityBoss).ToList();
            var scoring=match.Teams?players.Where((p,i)=>players.FindIndex(q=>q.team==p.team)==i).ToList():players;int wins=Math.Max(1,Math.Min(20,match.SetsToWin)),columns=Math.Min(10,wins);
            float rows=Mathf.Ceil(wins/(float)columns),row=Mathf.Max(30,rows*15+10),aliveH=players.Count*34;float h=14+20+scoring.Count*row+10+9+19+aliveH+14;
            NativeHudChrome.Panel(new Rect(x,y,w,h));float cy=y+14;
            U.Text(new Rect(x+14,cy,w-28,16),"SET SCORE",12,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            U.Text(new Rect(x+14,cy,w-28,16),(match.RoundsPerSet==2?"◐ ROUND":"1/"+match.RoundsPerSet+" PER ROUND")+" · ● SET",10,NativeCyberTheme.Muted(),TextAnchor.MiddleRight);cy+=24;
            foreach(var p in scoring)
            {
                bool you=match.Teams?p.team==me.team:p.id==me.id;var color=match.Teams?TeamColor(p.team):SeatColor(p);string name=match.Teams?NativeMatch.TeamNames[TeamIndex(p.team)]+" team":p.name;
                U.Fill(new Rect(x+14,cy,2,row-4),color);U.Text(new Rect(x+25,cy,150,row-4),name+(you?"  YOU":""),13,NativeCyberTheme.Ink(),TextAnchor.MiddleLeft,bold:you);
                int sets=Mathf.Clamp(p.score,0,wins);float partial=match.SetWinner!=0?0:Mathf.Clamp01(p.roundWins/(float)Math.Max(1,match.RoundsPerSet));
                for(int i=0;i<wins;i++){int c=i%columns,r=i/columns;var dot=new Rect(x+w-14-(columns-c)*15+3,cy+(row-4-rows*15+3)/2+r*15,12,12);U.Round(dot,new Color(1,1,1,.045f),6,color);
                    if(i<sets){U.Glow(dot,U.A(color,.42f),3);U.Round(dot,color,6);}else if(i==sets&&partial>0)U.Pie(dot,color,partial);}
                cy+=row;
            }
            cy+=10;U.Fill(new Rect(x+14,cy,w-28,1),NativeCyberTheme.Edge());cy+=9;int alive=players.Count(p=>p.connected&&p.alive);
            U.Text(new Rect(x+14,cy,w-28,14),alive+" / "+players.Count+" ALIVE",11,NativeCyberTheme.CyanSoft(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);cy+=19;
            foreach(var p in players)
            {
                bool out_=!p.connected||!p.alive;var c=U.A(Color.white,out_?.55f:1);U.Fill(new Rect(x+14,cy,2,29),match.Teams?TeamColor(p.team):SeatColor(p));
                U.Text(new Rect(x+24,cy,w-90,29),p.name,13,NativeCyberTheme.Ink(c.a),TextAnchor.MiddleLeft);if(match.Teams)U.Text(new Rect(x+24+U.Measure(p.name,13).x+7,cy,60,29),NativeMatch.TeamNames[TeamIndex(p.team)],10,U.A(TeamColor(p.team),c.a),TextAnchor.MiddleLeft);
                if(out_)U.Text(new Rect(x+14,cy,w-36,29),!p.connected?"OFFLINE":"OUT",11,NativeCyberTheme.Muted(),TextAnchor.MiddleRight,mono:true);cy+=34;
            }
        }

        // ---- Match end (main.ts:161 phase 'match', .panel) ----
        // Rematch and Return to Lobby, or only Return to Main Menu once the host is gone (main.ts peerLost: "Match ended"
        // over the reason). Return to Lobby starts the cancellable three-second return and is disabled for guests and while
        // it runs; its banner and Cancel are in NativeConnectionReport.cs, and each vote's "n/N pilots ready for rematch."
        // toast is EndFlowNotices there (the source's button carries no count). The reason says the seat can be reclaimed
        // for 90 seconds, so Return to Main Menu keeps the resume token and lands on Join Room (LeaveLostHost).
        private float matchEndAt=-1;
        private void MatchEndPanel()
        {
            if(matchEndAt<0)matchEndAt=Time.unscaledTime;float enter=U.PanelIn(Time.unscaledTime-matchEndAt),W=U.W,H=U.H;var me=arena.Local.Seat;var winner=match.Seats.Find(s=>s.id==match.Winner);bool lost=HostLost;
            var players=match.Seats.Where(s=>!s.AuthorityBoss).OrderByDescending(s=>s.score).ToList();float pw=Mathf.Min(640,W-32),ph=32+22+40+60+24+players.Count*68+40+24+48+32,px=(W-pw)/2,py=Mathf.Max(16,(H-ph)/2)+10*(1-enter);
            // The in-match holographic panel (NativeHudChrome) rather than the menu .panel: the result in my side's tone (cyan
            // won, danger lost), the standings as chamfered rows (mine edged in cyan). The buttons stay the shared MenuButton.
            bool won=!lost&&OnMySide(winner),defeat=!lost&&!won&&winner!=null;Color tone=won?NativeCyberTheme.Cyan():defeat?NativeCyberTheme.Danger():NativeCyberTheme.CyanSoft();
            U.Push(new Vector2(W/2,py+ph/2),Vector2.zero,.99f+.01f*enter);var panel=new Rect(px,py,pw,ph);U.Glow(new Rect(px,py+30,pw,ph),new Color(0,0,0,.45f*enter),60);U.Backdrop(panel,0,16,enter);
            NativeHudChrome.Panel(panel,enter,tone,14,NativeHudChrome.Corners.Diagonal,true,.94f);float y=py+32,ix=px+32,iw=pw-64;
            U.Text(new Rect(ix,y,iw,16),"CHIP ARENA",12,NativeCyberTheme.CyanSoft(enter),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);y+=22;
            U.Text(new Rect(ix,y,iw,40),lost?"Match ended":won?"Match won":defeat?"Match lost":"Match ended",30,U.A(won||defeat?tone:NativeCyberTheme.Ink(),enter),TextAnchor.MiddleLeft,bold:true,tracking:-.035f,glow:U.A(tone,.25f*enter));y+=50;
            U.Text(new Rect(ix,y,iw,40),lost?(networkStatus.Length>0?networkStatus:"The host disconnected."):match.Result,16,NativeCyberTheme.Muted(enter),TextAnchor.UpperLeft,wrap:true);y+=60;
            U.Text(new Rect(ix,y,iw,20),"SETS WON",12,NativeCyberTheme.Muted(enter),TextAnchor.MiddleCenter,mono:true,tracking:.12f);y+=24;
            foreach(var p in players){var r=new Rect(ix,y+12,iw,56);bool mine=p.id==me.id;NativeHudChrome.Cut(r,NativeCyberTheme.Indigo(.92f*enter),NativeHudCyber.CardCut);NativeHudChrome.Outline(r,mine?NativeCyberTheme.Cyan(.8f*enter):NativeCyberTheme.Edge(enter),NativeHudCyber.CardCut);
                U.Text(new Rect(r.x+17,r.y,r.width-34,r.height),p.name+(mine?" · YOU":""),15,NativeCyberTheme.Ink(enter),TextAnchor.MiddleLeft);U.Text(new Rect(r.x+17,r.y,r.width-34,r.height),p.score.ToString(),15,mine?NativeCyberTheme.Cyan(enter):NativeCyberTheme.Ink(enter),TextAnchor.MiddleRight,bold:true,mono:true);y+=68;}
            U.Text(new Rect(ix,y+8,iw,20),me.chips.Values.Sum()+" CHIPS INSTALLED · BUILD COMPLETE",13,NativeCyberTheme.Muted(enter),TextAnchor.MiddleCenter,mono:true);y+=40+24;
            float bw=(iw-10)/2;
            if(lost){if(MenuButton(new Rect(ix,y,iw,48),"log-out","Return to Main Menu",null,true,true,enter,15,true))LeaveLostHost();}
            else
            {
                if(MenuButton(new Rect(ix,y,bw,48),"rotate-cw","Rematch",null,true,true,enter,15,true))VoteRematch();
                if(MenuButton(new Rect(ix+bw+10,y,bw,48),"undo-2","Return to Lobby",null,false,(Hosting||!Online)&&LobbyReturnSeconds<=0,enter,15,true)){if(!Online)afterMatch=OfflineLobby();RequestLobbyReturn();}
            }
            U.Pop();
        }
        // pve/results.ts: VICTORY / DEFEAT under the boss test eyebrow, the Siren's remaining health, a card per pilot (MVP by
        // boss damage) and Retry / Return to Menu.
        // pve/results.ts pveResults with pve.css: VICTORY / DEFEAT over the outcome line, then one result per party member,
        // each with its pilot card (portrait clamp(80px,12vh,200px), no class line), the installed chips as rarity badges
        // with ×count, and the run's stats; the MVP (most boss damage) wears the gold edge and badge. Retry and Return to
        // Menu sit centred below. Total damage and headshots are not tracked by the native PvE rows yet, so those stats are
        // left out rather than shown as zero.
        private void PveResultsPanel()
        {
            var v=arena.Pve;if(matchEndAt<0)matchEndAt=Time.unscaledTime;float enter=U.PanelIn(Time.unscaledTime-matchEndAt),W=U.W,H=U.H,t=Time.unscaledTime;bool victory=v.Outcome=="victory";
            var rows=v.Rows.Values.Where(r=>!r.Pilot.Seat.bot||r.Pilot.Seat.connected).OrderBy(r=>r.Pilot.Seat.id).ToList();var mvp=rows.OrderByDescending(r=>r.DamageBoss).ThenByDescending(r=>r.Kills).FirstOrDefault();
            // .panel:has(.pve-results): min(1180px, 100vw-32px); .pve-result-grid repeat(auto-fit, minmax(210px,1fr)), gap 14.
            float pw=Mathf.Min(1180,W-32),iw=pw-64;int cols=Mathf.Clamp(Mathf.Min(rows.Count,Mathf.FloorToInt((iw+14)/(210+14))),1,4);float cw=(iw-(cols-1)*14)/cols;
            // Portrait clamp(80px,12vh,200px); when the cards would not fit the canvas, the source's short-screen rule
            // (max-height:480px) hides the portraits and lays the stats out in three columns.
            string bossLine=PveBossLine(v,victory);float head=32+16+8+46+6+20+(bossLine!=null?26:0)+16,foot=28+48+32;int lines=Mathf.CeilToInt(rows.Count/(float)cols),statCols=2;
            float chipsH=rows.Count>0?Mathf.Max(24,rows.Max(r=>ChipBadgeRows(r.Pilot.Seat,cw-20)*28-4)):24,portrait=Mathf.Clamp(H*.12f,80,200),topH=49;int stats=StatRows(rows.FirstOrDefault()).Length;
            float ResultH()=>10+(topH+portrait+4+21.6f+17)+10+chipsH+10+Mathf.CeilToInt(stats/(float)statCols)*25+10;
            if(head+lines*ResultH()+(lines-1)*14+foot>H-32){portrait=0;topH=36;if((cw-20-20)/3>=90)statCols=3;}
            float resH=ResultH(),cardH=topH+portrait+4+21.6f+17,ph=Mathf.Min(H-32,head+lines*resH+(lines-1)*14+foot),px=(W-pw)/2,py=Mathf.Max(16,(H-ph)/2)+10*(1-enter);
            // The in-match holographic panel (NativeHudChrome): victory in the gold warn tone, defeat in danger.
            var panel=new Rect(px,py,pw,ph);U.Glow(new Rect(px,py+30,pw,ph),new Color(0,0,0,.45f*enter),60);U.Backdrop(panel,0,16,enter);NativeHudChrome.Panel(panel,enter,victory?NativeCyberTheme.Warn():NativeCyberTheme.Danger(),14,NativeHudChrome.Corners.Diagonal,true,.94f);float y=py+32,ix=px+32;
            U.Text(new Rect(ix,y,iw,16),"BOSS TEST · THE SIREN",12,NativeCyberTheme.CyanSoft(enter),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);y+=16+8;
            U.Text(new Rect(ix,y,iw,46),victory?"VICTORY":"DEFEAT",38,victory?NativeCyberTheme.Warn(enter):NativeCyberTheme.Danger(enter),TextAnchor.MiddleLeft,bold:true,tracking:.08f,glow:U.A(victory?NativeCyberTheme.Warn():NativeCyberTheme.Danger(),.4f*enter));y+=46+6;
            U.Text(new Rect(ix,y,iw,20),victory?"THE SIREN IS SILENCED":"THE SIREN SMILES",13,victory?NativeCyberTheme.CyanSoft(enter):NativeCyberTheme.Danger(enter),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.24f);y+=20;
            if(bossLine!=null){U.Text(new Rect(ix,y+6,iw,20),bossLine,15,NativeCyberTheme.Ink(enter),TextAnchor.MiddleLeft);y+=26;}y+=16;
            for(int i=0;i<rows.Count;i++)
            {
                var r=rows[i];var c=new Rect(ix+(i%cols)*(cw+14),y+(i/cols)*(resH+14),cw,resH);bool crown=r==mvp&&r.DamageBoss>0;PveResultCard(c,r,i,crown,portrait,topH,cardH,statCols,enter,t);
            }
            // .pve-result-actions: Retry (not once the host is gone) and Return to Menu, both primary, at least min(240px,40vw),
            // gap 12, centred together with the 13 px muted "Waiting for N more…" after a vote while others have not voted.
            y=py+ph-32-48;float bw=Mathf.Min(240,(iw-12)/2);bool retry=!HostLost;int votes=RematchVotes,required=RematchRequired;
            if(votes>0&&votes<required){if(required-votes!=waitingFor){waitingFor=required-votes;waitingText="Waiting for "+waitingFor+" more…";}}else waitingFor=-1;
            float ww=waitingFor>0?U.Measure(waitingText,13).x:0,group=(retry?bw+12:0)+bw+(ww>0?12+ww:0),bx=px+(pw-group)/2;
            if(retry){if(MenuButton(new Rect(bx,y,bw,48),"rotate-cw","Retry",null,true,true,enter,15,true))VoteRematch();bx+=bw+12;}
            if(MenuButton(new Rect(bx,y,bw,48),"log-out","Return to Menu",null,true,true,enter,15,true)){if(HostLost)LeaveLostHost();else{afterMatch=MenuScreen.Home;ReturnMainMenu();}}
            if(ww>0)U.Text(new Rect(bx+bw+12,y,ww+4,48),waitingText,13,NativeCyberTheme.Muted(enter),TextAnchor.MiddleLeft);
        }
        private int waitingFor=-1;private string waitingText="";
        // .pve-result-boss on a defeat: the Siren's remaining health, or the wave the party fell on before she appeared.
        private static string PveBossLine(NativePveState v,bool victory)
        {
            if(victory)return null;if(v.Boss!=null&&v.MaxHp>0&&v.Stage=="boss")return "The Siren kept "+Mathf.Max(0,Mathf.RoundToInt(v.Boss.Hp/v.MaxHp*100))+"% health ("+Mathf.Max(0,Mathf.RoundToInt(v.Boss.Hp)).ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" / "+Mathf.RoundToInt(v.MaxHp).ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+").";
            int waves=v.Content?.waves?.Length??0;return v.Wave>0&&waves>0?"The party fell on wave "+v.Wave+" of "+waves+".":null;
        }
        private static (string label,string value)[] StatRows(NativePveRow r)
        {
            if(r==null)return new (string,string)[0];string N(float x)=>Mathf.RoundToInt(x).ToString("N0",System.Globalization.CultureInfo.InvariantCulture);
            var list=new System.Collections.Generic.List<(string,string)>{("Boss damage",N(r.DamageBoss)),("Kills",N(r.Kills)),("Assists",N(r.Assists)),("Revives",N(r.Revives)),("Deaths",N(r.Deaths))};
            if(!r.Pilot.Seat.bot)list.Add(("Earned","$"+N(r.Earned)));return list.ToArray();
        }
        // .pve-result: #121319, 1 px #ffffff14, radius 12, padding 10, gap 10; MVP: #ffd36a77 edge, #ffb84a22 glow and the
        // gold badge on the top edge.
        private void PveResultCard(Rect c,NativePveRow r,int index,bool crown,float portrait,float topH,float cardH,int statCols,float e,float t)
        {
            var seat=r.Pilot.Seat;if(crown)U.Glow(c,NativeCyberTheme.Warn(.13f*e),24);NativeHudChrome.Cut(c,NativeCyberTheme.Indigo(.92f*e),NativeHudCyber.Cut);NativeHudChrome.Outline(c,crown?NativeCyberTheme.Warn(.6f*e):NativeCyberTheme.Edge(e),NativeHudCyber.Cut);
            var weapon=content.weapons.FirstOrDefault(w=>w.id==seat.spec);bool you=r.Pilot==arena.Local;
            var pilot=new LobbyPilot{name=seat.name,weapon=seat.spec,tag=seat.bot?"BOT":you?"YOU":"PILOT",ready=false,connected=true,bot=seat.bot,accent=HexColor(seat.accentColor,weapon!=null?(int)weapon.color:0xff6b35),body=HexColor(seat.bodyColor,0xe5e7eb)};
            var card=new Rect(c.x+10,c.y+10,c.width-20,cardH);LobbyCard(card,pilot,index,portrait,topH,false,false,e,t,true);
            if(crown){float bw=U.Measure("MVP",11,true,true,.1f).x+12+14+16;var b=new Rect(c.xMax-10-bw,c.y-9,bw,20);U.Glow(b,U.Hex(0xffb84a,.3f*e),10);U.Round(b,U.Hex(0xffd36a,e),10);
                NativeIcons.Draw(new Rect(b.x+8,b.center.y-6,12,12),"sparkles",U.Hex(0x2a1d05,e));U.Text(new Rect(b.x+24,b.y,bw-28,b.height),"MVP",11,U.Hex(0x2a1d05,e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);}
            float y=card.yMax+10;y=ChipBadges(new Rect(c.x+10,y,c.width-20,0),seat,e)+10;
            var stats=StatRows(r);float colW=(c.width-20-10*(statCols-1))/statCols;
            for(int i=0;i<stats.Length;i++)
            {
                var cell=new Rect(c.x+10+(i%statCols)*(colW+10),y+(i/statCols)*25,colW,21);U.Fill(new Rect(cell.x,cell.yMax,cell.width,1),new Color(1,1,1,.05f*e));
                float ls=statCols>2?10:11,room=cell.width-U.Measure(stats[i].value,12,true,true).x-6;string label=stats[i].label;if(U.Measure(label,ls).x>room&&label=="Boss damage")label="Boss dmg";
                U.Text(cell,Fit(label,ls,room,false,0),ls,NativeCyberTheme.Muted(e),TextAnchor.MiddleLeft);U.Text(cell,stats[i].value,12,NativeCyberTheme.Ink(e),TextAnchor.MiddleRight,bold:true,mono:true);
            }
        }
        // .pve-result-chip badges (flex-wrap, gap 4): rarity-tinted fill and edge, the chip icon in the rarity colour, the name
        // (11 px, 600) and ×count in mono; "No chips installed" when empty. Returns the bottom edge.
        private float ChipBadges(Rect r,NativeSeat seat,float e)
        {
            var chips=seat.chips.Where(k=>k.Value>0).Select(k=>(chip:content.chips.FirstOrDefault(c=>c.id==k.Key),count:k.Value)).Where(k=>k.chip!=null).ToList();
            if(chips.Count==0){U.Text(new Rect(r.x,r.y,r.width,24),"No chips installed",11,U.Hex(0x8f93a3,e),TextAnchor.MiddleLeft);return r.y+24;}
            float x=r.x,y=r.y;
            foreach(var (chip,count) in chips)
            {
                Color rarity=RarityColor(chip.rarity);float w=BadgeWidth(chip.name,count);if(x>r.x&&x+w>r.xMax){x=r.x;y+=28;}var b=new Rect(x,y,Mathf.Min(w,r.width),24);
                U.Round(b,U.A(U.Mix(U.Hex(0x0d0e12),rarity,.14f),e),6,U.A(U.Mix(Color.black,rarity,.6f),e));NativeIcons.Draw(new Rect(b.x+7,b.center.y-6,12,12),NativeIcons.Chip(chip.id),U.A(rarity,e));
                float nw=U.Measure(chip.name,11,true).x;U.Text(new Rect(b.x+23,b.y,Mathf.Min(nw+2,b.width-30),b.height),chip.name,11,U.Hex(0xe9eaf0,e),TextAnchor.MiddleLeft,bold:true);
                if(count>1)U.Text(new Rect(b.x+23+nw+4,b.y,30,b.height),"×"+count,10,U.A(rarity,e),TextAnchor.MiddleLeft,bold:true,mono:true);x+=w+4;
            }
            return y+24;
        }
        private static float BadgeWidth(string name,int count)=>7+12+4+U.Measure(name,11,true).x+(count>1?4+U.Measure("×"+count,10,true,true).x:0)+7;
        private int ChipBadgeRows(NativeSeat seat,float width)
        {
            float x=0;int lines=1;foreach(var k in seat.chips.Where(k=>k.Value>0)){var chip=content.chips.FirstOrDefault(c=>c.id==k.Key);if(chip==null)continue;float w=BadgeWidth(chip.name,k.Value);if(x>0&&x+w>width){lines++;x=0;}x+=w+4;}return lines;
        }
        // A guest whose host is gone: back to Join Room with the resume token kept, so Join Room within the 90-second lease
        // reclaims the seat (the pause menu's lost path). ReturnMainMenu() closes the network, which clears the token.
        private void LeaveLostHost(){afterMatch=MenuScreen.Join;ReturnToLobby();}
        private void ReturnToLobby()
        {
            ClearReplay();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();matchEndAt=-1;menu=true;
            if(Hosting){networkEpoch++;foreach(var p in peers)p.Ready=false;LobbyBroadcast();}
        }
    }
}
