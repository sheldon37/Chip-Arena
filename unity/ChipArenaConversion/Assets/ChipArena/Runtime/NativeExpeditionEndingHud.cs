using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using E=ChipArena.UnityConversion.NativeExpeditionHud;
using H=ChipArena.UnityConversion.NativeHudChrome;
using N=ChipArena.UnityConversion.NativeExpeditionEnding;
using R=ChipArena.UnityConversion.NativeHudCyber;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition's end sequence (Claude, 2026-10-20; owner: "improve ui of expedition quota + expedition ending/completion";
    /// docs/EXPEDITION-ENDING-UI.md), drawn from Core ExpeditionEndingModel and ExpeditionEndingLayout in the cyber HUD kit:
    ///  - the title moment: the world dims behind the kit's blur, letterbox bars come in, the outcome (EXPEDITION COMPLETE, EXTRACTED,
    ///    WIPED, EXPEDITION FAILED, RUN ENDED) lands with a scale, a blur and a glitch settling, its rule, its subtitle and a light sweep;
    ///  - the title rises into the summary's header: the run's time, zones cleared, quota kills and bosses counting up, the route through
    ///    the zones to the world boss (where the run ended marked), the party's rows (weapon, name, YOU / HOST, status), your build,
    ///    run wallet and loot;
    ///  - the account XP column docks beside it (the account XP panel itself, NativeAccountXpUI.cs), then the actions with keyboard focus.
    /// Reduced effects: the settled summary at once, no motion. Draws cached words only (nothing is concatenated per frame).
    /// </summary>
    public static class NativeExpeditionEndingHud
    {
        private const string Node="<path d=\"M12 2.5L21.5 12L12 21.5L2.5 12Z\" fill=\"#fff\"/>";
        private static float measuredAt=-1,titleUnit,eyebrowW,subW;private static string titleMeasured,eyebrowMeasured,subMeasured;
        private static void Measure(ExpeditionEndingModel m)
        {
            if(measuredAt!=U.S){measuredAt=U.S;titleMeasured=eyebrowMeasured=subMeasured=null;}
            if(!ReferenceEquals(titleMeasured,m.Title)){titleMeasured=m.Title;titleUnit=m.Title==null?0:U.Measure(m.Title,100,true,false,.08f).x/100;}
            if(!ReferenceEquals(eyebrowMeasured,m.Eyebrow)){eyebrowMeasured=m.Eyebrow;eyebrowW=m.Eyebrow==null?0:U.Measure(m.Eyebrow,12,true,true,.26f).x;}
            if(!ReferenceEquals(subMeasured,m.Subtitle)){subMeasured=m.Subtitle;subW=m.Subtitle==null?0:U.Measure(m.Subtitle,13,true,true,.2f).x;}
        }

        // ---- the backdrop -----------------------------------------------------------------------------------------------------------------
        /// <summary>The world behind: the kit's blurred copy and the dim, the tone's flash, the letterbox bars.</summary>
        public static void Backdrop(ExpeditionEndingModel m,float W,float Hh,bool reduced)
        {
            if(Event.current.type!=EventType.Repaint)return;var full=new Rect(0,0,W,Hh);
            U.Backdrop(full,0,16,Mathf.Clamp01(m.Dim/.86f));U.Fill(full,C.Void(m.Dim));
            if(!reduced&&m.Flash>0)U.Fill(full,U.A(E.Tone(m.Tone),m.Flash));
            if(m.Bars>0){float b=Hh*.09f*m.Bars;U.Fill(new Rect(0,0,W,b),C.Void(.96f));U.Fill(new Rect(0,Hh-b,W,b),C.Void(.96f));U.Fill(new Rect(0,b-H.Snap(1),W,H.Snap(1)),U.A(E.Tone(m.Tone),.35f*m.Bars));U.Fill(new Rect(0,Hh-b,W,H.Snap(1)),U.A(E.Tone(m.Tone),.35f*m.Bars));}
            // A soft vignette in the outcome's tone behind the title while it holds the centre.
            float hold=1-m.Rise;if(hold>0&&m.TitleIn>0)U.Radial(new Rect(W*.15f,Hh*.12f,W*.7f,Hh*.6f),U.A(E.Tone(m.Tone),.12f*hold*m.TitleIn));
        }

        // ---- the title ----------------------------------------------------------------------------------------------------------------------
        /// <summary>The eyebrow, title, rule and subtitle: centred big, rising into the summary's header (left, smaller) at m.Rise.</summary>
        public static void Title(ExpeditionEndingModel m,in ExpeditionEndingLayout l,float W,float Hh,bool reduced)
        {
            if(Event.current.type!=EventType.Repaint||m.Title==null||m.TitleIn<=0)return;Measure(m);
            Color tone=E.Tone(m.Tone);float rise=m.Rise,a=m.TitleIn;
            float size=Mathf.Lerp(l.BigTitle,l.TitleSize,rise),width=titleUnit*size;
            float bigX=W/2-titleUnit*l.BigTitle/2,bigY=l.TitleCenterY-l.BigTitle/2;
            float x=Mathf.Lerp(bigX,l.X+l.Pad,rise),y=Mathf.Lerp(bigY,l.HeaderTitleY,rise);
            var box=new Rect(x,y-size*.1f,width+size*.5f,size*1.2f);
            // The eyebrow over it, the subtitle and the rule under it (centred, then left in the header).
            float eyY=Mathf.Lerp(bigY-34,l.HeaderY,rise),eyX=Mathf.Lerp(W/2-eyebrowW/2,l.X+l.Pad,rise);
            if(m.Eyebrow!=null)U.Text(new Rect(eyX,eyY,eyebrowW+8,16),m.Eyebrow,12,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.26f,shadow:C.Void(.8f*a));
            float subY=Mathf.Lerp(bigY+l.BigTitle*1.18f+22,l.SubY,rise),subX=Mathf.Lerp(W/2-subW/2,l.X+l.Pad,rise),sa=m.SubIn*a;
            if(m.Subtitle!=null&&sa>0)U.Text(new Rect(subX,subY,subW+8,20),m.Subtitle,13,U.A(m.Tone==ExpeditionTone.Danger?C.Danger():C.Ink(),sa),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f,shadow:C.Void(.8f*sa));
            float hold=1-rise;if(m.RuleIn>0&&hold>0){float reach=Mathf.Min(W*.3f,520)*m.RuleIn,ry=bigY+l.BigTitle*1.18f+8;U.Fill(new Rect(W/2-reach,ry,reach*2,H.Snap(2)),U.A(tone,.85f*hold*a));U.Glow(new Rect(W/2-reach*.6f,ry,reach*1.2f,H.Snap(2)),U.A(tone,.35f*hold*a),8);}
            if(m.Note!=null&&rise<=0&&sa>0)U.Text(new Rect(0,subY+26,W,18),m.Note,11,C.Muted(sa),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.18f,shadow:C.Void(.8f*sa));
            // The title itself: a scale and a blur settling, the glitch's split copies, then the light sweeping across it.
            U.Push(box.center,Vector2.zero,1+(reduced?0:m.TitleGrow));
            if(!reduced&&m.Glitch>=0){U.SplitCopy(box,box,m.Glitch,false,m.Title,size,a,TextAnchor.MiddleLeft,.08f);U.SplitCopy(box,box,(m.Glitch+.37f)%1,true,m.Title,size,a,TextAnchor.MiddleLeft,.08f);}
            U.Text(box,m.Title,size,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.08f,glow:U.A(tone,.55f*a),shadow:C.Void(.9f*a),blur:reduced?0:m.TitleBlur);
            if(!reduced&&m.Sweep>=0){float s=m.Sweep,sx=box.x+(box.width+80)*s-40,k=1-Mathf.Abs(2*s-1);U.Glow(new Rect(sx-8,box.y+4,16,box.height-8),C.Ink(.22f*k*a),18);}
            U.Pop();
        }

        // ---- the summary --------------------------------------------------------------------------------------------------------------------
        /// <summary>The summary panel and everything in it but the title and the actions; chip names and rarity ticks from the caller.</summary>
        public static void Summary(ExpeditionEndingModel m,in ExpeditionEndingLayout l,Func<string,string> chipName,Func<string,Color> chipTone,bool reduced)
        {
            float a=m.PanelIn;if(a<=0||Event.current.type!=EventType.Repaint)return;Color tone=E.Tone(m.Tone);
            var p=new Rect(l.X,l.Y+(reduced?0:14*(1-a)),l.W,l.H);
            U.Glow(new Rect(p.x,p.y+30,p.width,p.height),C.Void(.45f*a),60);U.Backdrop(p,0,16,a);H.Panel(p,a,tone,14,H.Corners.Diagonal,true,.94f);
            float dy=p.y-l.Y,ix=l.X+l.Pad,iw=l.W-2*l.Pad;
            if(m.Note!=null&&m.Rise>=1)U.Text(new Rect(ix,l.HeaderY+dy,iw,16),m.Note,10,m.Tone==ExpeditionTone.Danger?C.Danger(a):C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.14f);
            // The stats: four tiles counting up.
            for(int i=0;i<m.StatCount;i++)
            {
                l.Tile(i,m.StatCount,out float tx,out float tw);float ia=a*m.StatIn[i];if(ia<=0)continue;var t=new Rect(tx,l.StatsY+dy+(reduced?0:8*(1-m.StatIn[i])),tw,l.StatsH);
                H.Cut(t,C.Indigo(.9f*ia),R.CardCut);H.Outline(t,C.Edge(ia),R.CardCut);U.Fill(new Rect(t.x+R.CardCut,t.y,Mathf.Min(40,t.width*.3f),H.Snap(2)),U.A(E.Tone(m.StatTone[i]==ExpeditionTone.Ink?m.Tone:m.StatTone[i]),.8f*ia));
                U.Text(new Rect(t.x+14,t.y+8,t.width-28,14),m.StatLabel[i],10,C.Muted(ia),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
                U.Text(new Rect(t.x+14,t.y+22,t.width-28,t.height-26),m.StatText[i],l.Compact?20:28,E.Tone(m.StatTone[i],ia),TextAnchor.MiddleLeft,bold:true,mono:true);
            }
            if(l.ZonesShown&&m.ZoneCount>0)Route(m,l,dy,a);
            Party(m,l,dy,a);
            Rewards(m,l,dy,a,chipName,chipTone);
        }
        // The route: each zone of the loop, then the world boss, connected; where the run ended is outlined in the danger tone.
        private static void Route(ExpeditionEndingModel m,in ExpeditionEndingLayout l,float dy,float a)
        {
            for(int i=0;i<m.ZoneCount;i++)
            {
                l.Node(i,m.ZoneCount,out float nx,out float nw);float ia=a*m.ZoneIn[i];if(ia<=0)continue;var n=new Rect(nx,l.ZonesY+dy,nw,l.ZonesH);
                if(i>0){float cx=n.x-18,cy=n.center.y;U.Fill(new Rect(cx+3,cy,12,H.Snap(1)),C.Edge(ia));NativeIcons.DrawMarkup(new Rect(cx+11,cy-3.5f,7,7),"ending-node",Node,24,C.Edge(ia));}
                Color zt=E.Tone(m.ZoneTone[i]);H.Cut(n,C.Navy(.92f*ia),R.TagCut);H.Outline(n,m.ZoneHere[i]?C.Danger(ia):C.Edge(ia),R.TagCut,H.Corners.Diagonal,m.ZoneHere[i]?2:1);
                if(m.ZoneHere[i])U.Glow(n,C.Danger(.18f*ia),8);
                U.Fill(new Rect(n.x+6,n.y+8,H.Snap(2),n.height-16),U.A(U.Hex(m.ZoneAccent[i]),ia));
                U.Text(new Rect(n.x+14,n.y+5,n.width-20,18),m.ZoneName[i],l.Compact?11:12,C.Ink(ia),TextAnchor.MiddleLeft,bold:true,tracking:.06f);
                U.Text(new Rect(n.x+14,n.y+(l.Compact?20:24),n.width-20,14),m.ZoneState[i],9,U.A(zt,ia),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
                H.Line(new Rect(n.x+14,n.yMax-(l.Compact?7:9),n.width-26,3),m.ZoneFill[i],U.A(zt,ia),C.Void(.7f*ia));
            }
        }
        // The party: a row per pilot (you first), two columns from three pilots; the victory's loop vote under it.
        private static void Party(ExpeditionEndingModel m,in ExpeditionEndingLayout l,float dy,float a)
        {
            float ix=l.X+l.Pad,iw=l.W-2*l.Pad;
            U.Text(new Rect(ix,l.PartyY+dy,iw,14),ExpeditionEndingModel.PartyLabel,10,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            U.Text(new Rect(ix,l.PartyY+dy,iw,14),m.PartyText,10,C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.12f);
            for(int i=0;i<m.RowCount;i++)
            {
                l.Row(i,out float rx,out float ry,out float rw);float ia=a*m.RowIn[i];if(ia<=0)continue;var r=new Rect(rx,ry+dy,rw,l.RowH-4);
                H.Cut(r,m.RowYou[i]?C.Indigo(.95f*ia):C.Navy(.85f*ia),R.TagCut);H.Outline(r,m.RowYou[i]?C.Cyan(.6f*ia):C.Edge(.8f*ia),R.TagCut);
                if(m.RowWeapon[i]!=null)NativeIcons.Draw(new Rect(r.x+9,r.center.y-7,14,14),NativeIcons.Spec(m.RowWeapon[i]),C.CyanSoft(ia));
                Color st=E.Tone(m.RowTone[i]);float inset=l.Compact?2:4;var tag=new Rect(r.xMax-8-r.width*.42f,r.y+inset,r.width*.42f,r.height-2*inset);
                // The name, then YOU and HOST as small marks after it (the name keeps the room it needs; a long one is clipped by the tag).
                float nx=r.x+30,room=tag.x-nx-8,nw=Mathf.Min(room,U.Measure(m.RowName[i],13,true).x);
                U.Text(new Rect(nx,r.y,room,r.height),m.RowName[i],13,m.RowYou[i]?C.CyanSoft(ia):C.Ink(ia),TextAnchor.MiddleLeft,bold:true);
                float mx=nx+nw+10;
                if(m.RowYou[i]&&mx+30<tag.x){U.Text(new Rect(mx,r.y,30,r.height),"YOU",9,C.Cyan(ia),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);mx+=34;}
                if(m.RowHost[i]&&mx+36<tag.x)U.Text(new Rect(mx,r.y,36,r.height),"HOST",9,C.Warn(ia),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                H.Tag(tag,st,ia);
                U.Text(new Rect(tag.x+10,tag.y,tag.width-12,tag.height),m.RowStatus[i],9,U.A(st,ia),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            }
            float ca=m.RowCount>0?a*m.RowIn[m.RowCount-1]:a;
            if(m.ContinueText!=null&&ca>0)
            {
                var c=new Rect(ix,l.ContinueY+dy,iw,16);U.Text(c,m.ContinueText,10,E.Tone(m.ContinueTone,ca),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                if(m.ContinueFill>=0)H.Line(new Rect(ix+iw*.45f,c.y+6,iw*.55f,3),m.ContinueFill,E.Tone(m.ContinueTone,ca),C.Void(.7f*ca));
            }
        }
        // Your run: the build (chip tags with their rarity tick), the run wallet counting up, the world boss loot and unopened rewards.
        private static void Rewards(ExpeditionEndingModel m,in ExpeditionEndingLayout l,float dy,float a,Func<string,string> chipName,Func<string,Color> chipTone)
        {
            float ra=a*m.RewardsIn;if(ra<=0)return;float ix=l.X+l.Pad,iw=l.W-2*l.Pad,y=l.RewardsY+dy,walletW=Mathf.Min(220,iw*.3f),left=iw-walletW-20;
            U.Fill(new Rect(ix,y-6,iw,H.Snap(1)),C.Edge(.8f*ra));
            U.Text(new Rect(ix,y,left,14),ExpeditionEndingModel.ChipsLabel,10,C.CyanSoft(ra),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            U.Text(new Rect(ix,y,left,14),m.ChipsText,10,C.Muted(ra),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.12f);
            float x=ix,cy=y+18;
            for(int i=0;i<m.ChipShown;i++)
            {
                string name=chipName!=null?chipName(m.ChipId[i]):m.ChipId[i];if(string.IsNullOrEmpty(name))continue;float w=U.Measure(name,11,true).x+20+(m.ChipCount[i]>1?16:0);
                if(x+w>ix+left){if(m.ChipsMore==null&&i<m.ChipShown)U.Text(new Rect(x,cy,40,20),"…",11,C.Muted(ra),TextAnchor.MiddleLeft);break;}
                var t=new Rect(x,cy,w,20);H.Tag(t,chipTone!=null?chipTone(m.ChipId[i]):C.CyanSoft(),ra,true);
                U.Text(new Rect(t.x+9,t.y,w-9,t.height),name,11,C.Ink(ra),TextAnchor.MiddleLeft,bold:true);
                if(m.ChipCount[i]>1)U.Text(new Rect(t.x,t.y,w-6,t.height),NativeHudText.Int(m.ChipCount[i]),10,C.Muted(ra),TextAnchor.MiddleRight,bold:true,mono:true);
                x+=w+6;
            }
            if(m.ChipsMore!=null&&x+30<ix+left)U.Text(new Rect(x,cy,40,20),m.ChipsMore,10,C.Muted(ra),TextAnchor.MiddleLeft,bold:true,mono:true);
            float ly=cy+(l.Compact?22:26),end=l.RewardsY+dy+l.RewardsH;
            if(m.LootText!=null&&ly+14<=end+2){U.Text(new Rect(ix,ly,left,16),m.LootText,10,E.Tone(m.LootTone,ra),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);ly+=16;}
            if(m.WaitingText!=null&&ly+14<=end+2)U.Text(new Rect(ix,ly,left,14),m.WaitingText,10,C.Magenta(.9f*ra),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            // The run wallet (the run's own money, apart from account XP's ultraviolet), counting up.
            var wr=new Rect(ix+iw-walletW,y,walletW,l.RewardsH-8);H.Cut(wr,C.Indigo(.9f*ra),R.CardCut);H.Outline(wr,C.Warn(.45f*ra),R.CardCut);
            U.Text(new Rect(wr.x+14,wr.y+8,wr.width-28,14),ExpeditionEndingModel.WalletLabel,10,C.Muted(ra),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            U.Text(new Rect(wr.x+14,wr.y+22,wr.width-28,wr.height-26),m.WalletText,l.Compact?20:24,C.Warn(ra),TextAnchor.MiddleLeft,bold:true,mono:true,glow:C.Warn(.25f*ra));
        }

        // ---- the actions ------------------------------------------------------------------------------------------------------------------
        /// <summary>The actions with their focus (hover moves it); the index of a clicked enabled action, else -1. `live`: clicks count.</summary>
        public static int Actions(ExpeditionEndingModel m,in ExpeditionEndingLayout l,bool live)
        {
            float a=m.ActionsIn*m.PanelIn;if(a<=0||m.ActionCount==0)return -1;int clicked=-1;float dy=m.PanelIn<1?14*(1-m.PanelIn):0;
            for(int i=0;i<m.ActionCount;i++)
            {
                l.Button(i,m.ActionCount,out float bx,out float bw);var r=new Rect(bx,l.ActionsY+dy,bw,l.ActionsH);bool on=m.ActionEnabled[i];
                if(live&&on&&U.Hover(r))m.Focus=i;
                if(i==m.Focus&&on&&Event.current.type==EventType.Repaint){H.Outline(new Rect(r.x-4,r.y-4,r.width+8,r.height+8),C.Cyan(.9f*a),R.TagCut,H.Corners.Diagonal,2);U.Glow(r,C.Cyan(.18f*a),10);}
                if(U.Btn(r,m.ActionLabel[i],m.ActionPrimary[i],on&&live,a,14)&&on&&live)clicked=i;
            }
            if(Event.current.type!=EventType.Repaint)return clicked;
            float ny=l.ActionsY+dy+l.ActionsH+6;
            if(m.ActionNote!=null)U.Text(new Rect(l.X+l.Pad,ny,l.W-2*l.Pad,14),m.ActionNote,10,C.Muted(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            return clicked;
        }
        /// <summary>The in-scene strip while an ending holds and the screen is closed (top centre, where the objective tag is).</summary>
        public static void Tag(ExpeditionEndingModel m,float W)
        {
            if(m.TagText==null)return;if(!ReferenceEquals(tagMeasured,m.TagText)||tagAt!=U.S){tagMeasured=m.TagText;tagAt=U.S;tagW=U.Measure(m.TagText,12,true,true,.14f).x;}
            Color tone=E.Tone(m.Tone);float w=tagW+44;var r=new Rect(W/2-w/2,20,w,30);H.Tag(r,tone);U.Glow(r,U.A(tone,.12f),8);
            U.Text(new Rect(r.x+16,r.y,w-20,r.height),m.TagText,12,U.A(tone,1),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
        }
        private static string tagMeasured;private static float tagW,tagAt=-1;
        /// <summary>The key hints under the panel (and SPACE · SKIP while the sequence plays).</summary>
        public static void Hints(ExpeditionEndingModel m,in ExpeditionEndingLayout l,float W,float Hh)
        {
            if(Event.current.type!=EventType.Repaint)return;
            if(m.Playing){U.Text(new Rect(W-260,Hh-34,236,16),N.Skip,10,C.Muted(.85f),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.16f);return;}
            float y=Mathf.Min(Hh-20,l.Y+l.H+6);U.Text(new Rect(l.X,y,l.W,14),N.Keys,10,C.Muted(.8f*m.ActionsIn),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
        }
    }
}
