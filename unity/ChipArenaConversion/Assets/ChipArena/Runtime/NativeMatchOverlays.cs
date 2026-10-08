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
            var pilot=IntroPilot;if(pilot==null)return;bool boss=match.Phase!=NativePhase.Intro;string style=IntroStyle;
            float duration=(float)NativeIntroRules.OverlayDuration(style),nameAt=(float)NativeIntroRules.NameAt(style),age=Mathf.Min(IntroAge,duration),W=U.W,H=U.H;
            var ink=U.Hex(boss?0x020103:0x040610);
            U.Fade(new Rect(0,0,W,H*(boss?.2f:.17f)),U.A(ink,.867f));float lower=boss?.6f:.7f;U.Fade(new Rect(0,H*lower,W,H*(1-lower)+1),U.A(ink,boss?.96f:.94f),false);
            // :before — radial-gradient(ellipse 60% 92% at 50% 100%) over the bottom 46vh, in over .7 s at the name time.
            float shade=M.EaseOut(M.Progress(age,nameAt,.7f)),ry=H*.46f*.92f;U.RadialStops(new Rect(-W*.1f,H-ry,W*1.2f,ry*2),U.A(U.Hex(0x040610),shade),new[]{0,.46f,.76f},new[]{.96f,.70f,0});
            // Topline at 88 px with 6vw margins; a 1 px rule and a progress bar that fills linearly over the intro.
            float top=M.EaseOut(M.Progress(age,0,.45f)),x0=W*.06f,tw=W*.88f,y=88;var order=match.Seats.Where(s=>s.connected&&!s.AuthorityBoss).ToList();int index=Math.Max(0,order.FindIndex(s=>s.id==pilot.Seat.id)),total=Math.Max(1,order.Count);
            string left=boss?"BOSS":"MATCH INTRO / "+(index+1).ToString("00")+" — "+total.ToString("00"),right=boss?"THE SIREN":NativeIntroRules.StyleName(style).ToUpperInvariant();
            U.Text(new Rect(x0,y,tw,15),left,12,U.A(U.Hex(boss?0xd9d0ff:0xd7d8de),top),TextAnchor.UpperLeft,mono:true,tracking:.2f);
            U.Text(new Rect(x0,y,tw,15),right,12,U.A(U.Hex(boss?0xc9bfff:0xff996f),top),TextAnchor.UpperRight,mono:true,tracking:boss?.42f:.2f);
            float rule=y+29;U.Fill(new Rect(x0,rule,tw,1),U.A(U.Hex(boss?0xb7a8ff:0xff6b35),.25f*top));float fill=Mathf.Clamp01(age/duration);
            if(fill>0)U.HGradient(new Rect(x0,rule,tw*fill,1),U.A(U.Hex(boss?0x7d5cff:0xff6b35),top),U.A(U.Hex(boss?0xf6f3ff:0xffb15c),top));
            if(age<nameAt)return;
            // .intro-name: bottom 5vh, 90% wide; each line rises 14 px in over .55 s, staggered; the block leaves at duration-.35 s.
            float exit=M.Bezier(.4f,0,1,1,M.Progress(age,duration-.35f,.34f)),alpha=1-exit,bottom=H*.95f-10*exit,cx=W*.05f,cw=W*.9f;
            if(boss){BossTitle(pilot,age,nameAt,alpha,bottom,cx,cw);return;}
            var seat=pilot.Seat;string weapon=(content.weapons.FirstOrDefault(w=>w.id==seat.spec)?.name??seat.spec).ToUpperInvariant(),text=(string.IsNullOrWhiteSpace(seat.introText)?"Ready to make an impact":seat.introText).ToUpperInvariant();bool team=match.Teams&&!match.Cooperative;
            float F=Mathf.Clamp(W*.065f,30,82);float fit=U.Measure(seat.name,F,true,false,-.035f).x;if(fit>cw)F*=cw/fit;
            float ySpan=bottom-18,yStrong=ySpan-9-1.15f*F,yTeam=yStrong-13-18,ySmall=(team?yTeam-8:yStrong-9)-15;var shadow=new Color(0,0,0,.8f*alpha);
            float e=Line(age,nameAt);U.Text(new Rect(cx,ySmall+14*(1-e),cw,15),weapon,12,U.A(U.Hex(0xffd3bf),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.3f,shadow:U.A(shadow,e));
            if(team){e=Line(age,nameAt+.07f);U.Text(new Rect(cx,yTeam+14*(1-e),cw,18),NativeMatch.TeamNames[TeamIndex(seat.team)].ToUpperInvariant()+" TEAM",14,U.A(TeamColor(seat.team),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.12f,shadow:U.A(shadow,e));}
            // intro-name-in .72 s: up 22 px from scale .96 while the tracking closes from .14em to -.035em.
            float n=Line(age,nameAt+.12f,.72f);U.Push(new Vector2(W/2,yStrong+.575f*F),new Vector2(0,22*(1-n)),.96f+.04f*n);
            U.Text(new Rect(cx,yStrong,cw,1.15f*F),seat.name,F,U.A(U.Hex(0xf6fcff),alpha*n),TextAnchor.MiddleCenter,bold:true,tracking:.14f-.175f*n,glow:U.A(SeatColor(seat),.65f*alpha*n));U.Pop();
            e=Line(age,nameAt+.24f);U.Text(new Rect(cx,ySpan+14*(1-e),cw,18),text,12,U.A(U.Hex(0xb9bac4),alpha*e),TextAnchor.UpperCenter,tracking:.3f);
        }
        // pve/hud.ts bossIntroName: "BOSS · WARD ZERO", the per-letter THE SIREN title with its red/cyan glitch, and the line.
        private void BossTitle(NativePilot pilot,float age,float nameAt,float alpha,float bottom,float cx,float cw)
        {
            float W=U.W,F=Mathf.Clamp(W*.086f,48,132),lh=.95f*F,yLine=bottom-21,yTitle=yLine-10-lh,ySmall=yTitle-6-15;var shadow=new Color(0,0,0,.8f*alpha);
            float e=Line(age,nameAt);U.Text(new Rect(cx,ySmall+14*(1-e),cw,15),"BOSS · WARD ZERO",12,U.A(U.Hex(0xc9bfff),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.5f,shadow:U.A(shadow,e));
            string title=(pilot.Seat.name??"The Siren").ToUpperInvariant();float gap=.06f*F;var widths=new float[title.Length];float total=0;
            for(int i=0;i<title.Length;i++){widths[i]=title[i]==' '?.3f*F:U.Measure(title[i].ToString(),F,true).x;total+=widths[i]+(i>0?gap:0);}
            // siren-title-jolt every 2.4 s from name+1.2 s (steps): -6 / +5 / -2 px at 87 / 89 / 91 %.
            float jolt=0;if(age>=nameAt+1.2f){float c=Mathf.Repeat(age-nameAt-1.2f,2.4f)/2.4f;jolt=c>=.87f&&c<.89f?-6:c>=.89f&&c<.91f?5:c>=.91f&&c<.94f?-2:0;}
            float Split(float start,float a,float b,float c){if(age<start)return float.NaN;float k=Mathf.Repeat(age-start,2.4f)/2.4f;return k>=.65f&&k<.67f?a:k>=.67f&&k<.69f?b:k>=.69f&&k<.72f?c:float.NaN;}
            float red=Split(nameAt+.9f,-3,4,-2),cyan=Split(nameAt+1,3,-4,2);
            float x=W/2-total/2+jolt;
            for(int i=0;i<title.Length;i++)
            {
                if(title[i]==' '){x+=widths[i]+gap;continue;}
                // siren-letter-in .5 s per letter, 38 ms apart: rises from 40% at scaleY 1.8, overshoots left then right, settles by 70%.
                float u=Line(age,nameAt+i*.038f,.5f),seg=Mathf.Clamp01(u/.55f),op=Mathf.Clamp01(u/.4f)*alpha;
                float tx=u<.55f?-4*seg:u<.62f?-4+7*((u-.55f)/.07f):u<.7f?3-3*((u-.62f)/.08f):0;var box=new Rect(x,yTitle,widths[i]+2,lh);string ch=title[i].ToString();
                U.Push(box.center,new Vector2(tx,.4f*lh*(1-seg)),1,0,1.8f-.8f*seg);
                if(!float.IsNaN(red))U.Text(new Rect(box.x+red,box.y,box.width,box.height),ch,F,U.A(U.Hex(0xff2d55),.85f*op),TextAnchor.MiddleLeft,bold:true);
                if(!float.IsNaN(cyan))U.Text(new Rect(box.x+cyan,box.y,box.width,box.height),ch,F,U.A(U.Hex(0x2de2ff),.85f*op),TextAnchor.MiddleLeft,bold:true);
                U.Text(box,ch,F,U.A(U.Hex(0xf6f3ff),op),TextAnchor.MiddleLeft,bold:true,glow:U.A(U.Hex(0x9b87ff),.8f*op),shadow:U.A(Color.black,op));U.Pop();
                x+=widths[i]+gap;
            }
            e=Line(age,nameAt+.24f);U.Text(new Rect(cx,yLine+14*(1-e),cw,21),"IT WAS WAITING BEHIND THE GREAT DOORS",14,U.A(U.Hex(0xd9d0ff),alpha*e),TextAnchor.UpperCenter,bold:true,tracking:.24f,shadow:U.A(shadow,e),italic:true);
        }

        // ---- Countdown (.countdown, count-tick) ----
        private void CountdownOverlay()
        {
            float remaining=(float)(match.PhaseEnd-match.Time);int tick=Mathf.CeilToInt(remaining);if(tick<=0)return;
            // count-tick 1 s: transparent at 1.5x, opaque at 1x by 18%, settling to .93x (cubic-bezier(.2,.9,.25,1) per segment).
            float t=Mathf.Clamp01(tick-remaining),a,s;if(t<.18f){float u=M.Bezier(.2f,.9f,.25f,1,t/.18f);a=u;s=1.5f-.5f*u;}else{a=1;s=1-.07f*M.Bezier(.2f,.9f,.25f,1,(t-.18f)/.82f);}
            float W=U.W,top=U.H*.38f-65;U.Push(new Vector2(W/2,top+52),Vector2.zero,s);
            U.Text(new Rect(0,top,W,104),tick.ToString(),104,U.A(U.Hex(0xf4f4f5),a),TextAnchor.MiddleCenter,bold:true,tracking:-.04f,glow:U.A(U.Hex(0xff6b35),.44f*a),shadow:new Color(0,0,0,.6f*a));U.Pop();
            U.Text(new Rect(0,top+114,W,17),"GET READY",13,U.Hex(0xff996f),TextAnchor.UpperCenter,bold:true,tracking:.32f,shadow:new Color(0,0,0,.8f));
        }

        // ---- Round result (.result-sequence) and the set-score overview it shows ----
        private void ResultOverlay()
        {
            float left=Mathf.Max(0,(float)(match.PhaseEnd-match.Time)),age=5-left,W=U.W,H=U.H;var winner=match.Seats.Find(s=>s.id==match.Winner);bool won=OnMySide(winner),final=match.Seats.Any(s=>s.score>=match.SetsToWin);
            string title=won?(final?"MATCH SECURED":match.SetWinner!=0?"SET SECURED":"ROUND SECURED"):winner!=null?"KNOCKOUT":"DOUBLE KNOCKOUT";
            float F=Mathf.Clamp(W*.046f,34,64),h2=Mathf.Clamp(W*.014f,15,19),bw=Mathf.Min(680,W*.85f),bx=(W-bw)/2;
            // result-arrive .45 s from 12 px above; result-word .6 s closes the heading's tracking from .4em to .08em.
            float arrive=M.Bezier(.16f,.8f,.2f,1,M.Progress(age,0,.45f)),word=M.Bezier(.16f,.8f,.2f,1,M.Progress(age,0,.6f)),by=H*.19f-12*(1-arrive);
            float bh=16+F+12+h2*1.35f+6+8+20+10+16;var band=U.A(U.Hex(0x101116),.88f*arrive);
            U.HGradient(new Rect(bx,by,bw*.22f,bh),Color.clear,band);U.Fill(new Rect(bx+bw*.22f,by,bw*.56f,bh),band);U.HGradient(new Rect(bx+bw*.78f,by,bw*.22f,bh),band,Color.clear);
            Color tc=won?U.Hex(0xff6b35):winner!=null?U.Hex(0xff5a6a):Color.white;
            U.Text(new Rect(bx,by+16,bw,F),title,F,U.A(tc,word*arrive),TextAnchor.MiddleCenter,bold:true,tracking:.4f-.32f*word,shadow:new Color(0,0,0,.66f*word*arrive));
            float y=by+16+F+12;U.Text(new Rect(bx+28,y,bw-56,h2*1.35f),match.Result,h2,U.A(U.Hex(0xd7d8de),arrive),TextAnchor.MiddleCenter,bold:true);y+=h2*1.35f+14;
            string next=(final?"Match results":match.SetWinner!=0?"Chip selection":winner!=null?"Next round":"Round replay")+" in "+Mathf.CeilToInt(left)+"s";
            U.Text(new Rect(bx,y,bw,20),next,13,U.A(U.Hex(0xa6a7ad),arrive),TextAnchor.MiddleCenter);
            // p:after — a 2 px bar over the middle half that shrinks to nothing over the result's remaining time.
            float bar=bw*.5f*Mathf.Clamp01(left/5);U.Round(new Rect(bx+bw*.25f,y+28,bar,2),U.A(U.Hex(0xa6a7ad),.55f*arrive),4);
            MatchOverview();
        }
        // match-score.ts matchScoreHud, top right (match-hud.css): set circles per pilot or team, then who is still alive.
        private void MatchOverview()
        {
            float W=U.W,w=310,x=W-20-w,y=62;var me=arena.Local.Seat;var players=match.Seats.Where(s=>!s.AuthorityBoss).ToList();
            var scoring=match.Teams?players.Where((p,i)=>players.FindIndex(q=>q.team==p.team)==i).ToList():players;int wins=Math.Max(1,Math.Min(20,match.SetsToWin)),columns=Math.Min(10,wins);
            float rows=Mathf.Ceil(wins/(float)columns),row=Mathf.Max(30,rows*15+10),aliveH=players.Count*34;float h=14+20+scoring.Count*row+10+9+19+aliveH+14;
            U.Round(new Rect(x,y,w,h),U.Hex(0x13141c,.87f),10,new Color(1,1,1,.14f));float cy=y+14;
            U.Text(new Rect(x+14,cy,w-28,16),"SET SCORE",12,U.Hex(0xedf2fc),TextAnchor.MiddleLeft,tracking:.06f);
            U.Text(new Rect(x+14,cy,w-28,16),(match.RoundsPerSet==2?"◐ ROUND":"1/"+match.RoundsPerSet+" PER ROUND")+" · ● SET",10,U.Hex(0xaeb6c8),TextAnchor.MiddleRight);cy+=24;
            foreach(var p in scoring)
            {
                bool you=match.Teams?p.team==me.team:p.id==me.id;var color=match.Teams?TeamColor(p.team):SeatColor(p);string name=match.Teams?NativeMatch.TeamNames[TeamIndex(p.team)]+" team":p.name;
                U.Fill(new Rect(x+14,cy,2,row-4),color);U.Text(new Rect(x+25,cy,150,row-4),name+(you?"  YOU":""),13,U.Hex(0xedf2fc),TextAnchor.MiddleLeft,bold:you);
                int sets=Mathf.Clamp(p.score,0,wins);float partial=match.SetWinner!=0?0:Mathf.Clamp01(p.roundWins/(float)Math.Max(1,match.RoundsPerSet));
                for(int i=0;i<wins;i++){int c=i%columns,r=i/columns;var dot=new Rect(x+w-14-(columns-c)*15+3,cy+(row-4-rows*15+3)/2+r*15,12,12);U.Round(dot,new Color(1,1,1,.045f),6,color);
                    if(i<sets){U.Glow(dot,U.A(color,.42f),3);U.Round(dot,color,6);}else if(i==sets&&partial>0)U.Pie(dot,color,partial);}
                cy+=row;
            }
            cy+=10;U.Fill(new Rect(x+14,cy,w-28,1),U.Hex(0x414657));cy+=9;int alive=players.Count(p=>p.connected&&p.alive);
            U.Text(new Rect(x+14,cy,w-28,14),alive+" / "+players.Count+" ALIVE",11,U.Hex(0xc6d0e4),TextAnchor.MiddleLeft,bold:true,tracking:.08f);cy+=19;
            foreach(var p in players)
            {
                bool out_=!p.connected||!p.alive;var c=U.A(Color.white,out_?.55f:1);U.Fill(new Rect(x+14,cy,2,29),match.Teams?TeamColor(p.team):SeatColor(p));
                U.Text(new Rect(x+24,cy,w-90,29),p.name,13,U.A(U.Hex(0xedf2fc),c.a),TextAnchor.MiddleLeft);if(match.Teams)U.Text(new Rect(x+24+U.Measure(p.name,13).x+7,cy,60,29),NativeMatch.TeamNames[TeamIndex(p.team)],10,U.A(TeamColor(p.team),c.a),TextAnchor.MiddleLeft);
                if(out_)U.Text(new Rect(x+14,cy,w-36,29),!p.connected?"OFFLINE":"OUT",11,U.Hex(0xa4a9b6),TextAnchor.MiddleRight,mono:true);cy+=34;
            }
        }

        // ---- Match end (main.ts phase 'match', .panel) ----
        private float matchEndAt=-1;
        private void MatchEndPanel()
        {
            if(matchEndAt<0)matchEndAt=Time.unscaledTime;float enter=U.PanelIn(Time.unscaledTime-matchEndAt),W=U.W,H=U.H;var me=arena.Local.Seat;var winner=match.Seats.Find(s=>s.id==match.Winner);
            var players=match.Seats.Where(s=>!s.AuthorityBoss).OrderByDescending(s=>s.score).ToList();float pw=Mathf.Min(640,W-32),ph=32+40+60+24+players.Count*68+40+24+48+32,px=(W-pw)/2,py=Mathf.Max(16,(H-ph)/2)+10*(1-enter);
            U.Push(new Vector2(W/2,py+ph/2),Vector2.zero,.99f+.01f*enter);U.Panel(new Rect(px,py,pw,ph),enter);float y=py+32,ix=px+32,iw=pw-64;
            U.Text(new Rect(ix,y,iw,40),OnMySide(winner)?"Match won":winner!=null?"Match lost":"Match ended",30,U.A(U.Hex(0xf4f4f5),enter),TextAnchor.MiddleLeft,bold:true,tracking:-.035f);y+=50;
            U.Text(new Rect(ix,y,iw,40),match.Result,16,U.A(U.Hex(0xbfc0c9),enter),TextAnchor.UpperLeft,wrap:true);y+=60;
            U.Text(new Rect(ix,y,iw,20),"SETS WON",12,U.A(U.Hex(0x9799a6),enter),TextAnchor.MiddleCenter,mono:true);y+=24;
            foreach(var p in players){var r=new Rect(ix,y+12,iw,56);U.Round(r,U.Hex(0x18191f,.94f*enter),4,U.A(U.Hex(0x454650),enter));U.Text(new Rect(r.x+17,r.y,r.width-34,r.height),p.name+(p.id==me.id?" · YOU":""),15,U.A(U.Hex(0xf4f4f5),enter),TextAnchor.MiddleLeft);U.Text(new Rect(r.x+17,r.y,r.width-34,r.height),p.score.ToString(),15,U.A(Color.white,enter),TextAnchor.MiddleRight,bold:true);y+=68;}
            U.Text(new Rect(ix,y+8,iw,20),me.chips.Values.Sum()+" CHIPS INSTALLED · BUILD COMPLETE",13,U.A(U.Hex(0xa6a7ad),enter),TextAnchor.MiddleCenter);y+=40+24;
            float bw=(iw-10)/2;bool lobby=Hosting||!Online;
            if(U.Btn(new Rect(ix,y,bw,48),"Rematch",true,true,enter)){if(server!=null)SendServer(new NativeNetMessage{type="rematch"});else StartMatch();}
            if(U.Btn(new Rect(ix+bw+10,y,bw,48),"Return to Lobby",false,lobby,enter))ReturnToLobby();
            U.Pop();
        }
        private void ReturnToLobby()
        {
            ClearReplay();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();matchEndAt=-1;menu=true;
            if(Hosting){networkEpoch++;foreach(var p in peers)p.Ready=false;LobbyBroadcast();}
        }
    }
}
