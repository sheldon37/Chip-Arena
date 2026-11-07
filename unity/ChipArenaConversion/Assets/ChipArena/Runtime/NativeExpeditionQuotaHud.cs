using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using E=ChipArena.UnityConversion.NativeExpeditionHud;
using H=ChipArena.UnityConversion.NativeHudChrome;
using Q=ChipArena.UnityConversion.NativeExpeditionQuota;
using R=ChipArena.UnityConversion.NativeHudCyber;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The in-run quota module (Claude, 2026-10-20; owner: "improve ui of expedition quota"; docs/EXPEDITION-ENDING-UI.md), drawn from
    /// Core ExpeditionQuotaModel in the cyber HUD kit, top centre in place of the objective tag while a zone quota is the objective:
    ///  - the zone (its accent tick) and KILL QUOTA, the party's pips in the zone (lit up, hollow down), the authority's count over the
    ///    target, a bar of one cell per kill and the kills left; every kill flashes its new cells, runs a light along the fill and pops
    ///    the count;
    ///  - closing in (10 left) the module turns amber and breathes; the last five turn the markers' hunt tone, their cells hollow, and
    ///    the LAST n · MARKED row (the markers' own words and kite) opens under it: the hand-off to the world markers;
    ///  - the quota met while watched: a flash, a light streak and a soft glow along the screen's top edge (never the centre), QUOTA
    ///    COMPLETE, then BOSS INCOMING (or the exit), and the module collapses into the compact strip for the boss and the cleared zone.
    /// A navy glass panel at the HUD's floor opacity with a faint halo in its tone, so it reads over Ward Zero's and the station's dark
    /// corridors; it stays in the top band (NativeExpeditionQuota.CombatTop): never over the crosshair or the combat numbers. Reduced
    /// effects: no flash, streak, sweep, pop or breath (the states and words are the same). Draws cached words only.
    /// </summary>
    public static class NativeExpeditionQuotaHud
    {
        private static float measuredAt=-1,countBox,targetBox,leftBox,partyW;private static string areaMeasured;private static float areaW;
        // The party's pips and the hint's diamonds as markup (no rotated transform: a rotation inside the module's clip band is clipped wrongly).
        private const string Pip="<path d=\"M12 2.5L21.5 12L12 21.5L2.5 12Z\" fill=\"#fff\"/>",PipEdge="<path d=\"M12 4L20 12L12 20L4 12Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"3\"/>";
        private static string eyebrowMeasured,compactMeasured;private static float eyebrowW,compactSize=12,compactTrack=.14f;
        private static void Measure(ExpeditionQuotaModel m)
        {
            if(measuredAt!=U.S){measuredAt=U.S;countBox=U.Measure("000",26,true,true).x;targetBox=U.Measure("/ 000",13,false,true).x;leftBox=U.Measure("00 LEFT",11,true,true,.08f).x;partyW=U.Measure(Q.PartyLabel,9,true,true,.16f).x;areaMeasured=eyebrowMeasured=compactMeasured=null;}
            if(!ReferenceEquals(areaMeasured,m.AreaText)){areaMeasured=m.AreaText;areaW=m.AreaText==null?0:U.Measure(m.AreaText,10,true,true,.2f).x;}
            if(!ReferenceEquals(eyebrowMeasured,m.EyebrowText)){eyebrowMeasured=m.EyebrowText;eyebrowW=m.EyebrowText==null?0:Mathf.Max(U.Measure(m.EyebrowText,10,true,true,.16f).x,m.DoneText==null?0:U.Measure(m.DoneText,10,true,true,.16f).x);}
            // The compact strip's words at the largest of three sizes that fits the module (a long zone name and state shrink, never clip).
            if(!ReferenceEquals(compactMeasured,m.CompactText)&&m.CompactText!=null)
            {
                compactMeasured=m.CompactText;float room=m.W-28;compactSize=10;compactTrack=.08f;
                if(U.Measure(m.CompactText,12,true,true,.14f).x<=room){compactSize=12;compactTrack=.14f;}else if(U.Measure(m.CompactText,11,true,true,.12f).x<=room){compactSize=11;compactTrack=.12f;}
            }
        }

        /// <summary>The module for this frame on a canvas W x Hh at unscaled time t; `glow` (the legibility halo and the bar's edge) off on
        /// the low graphics tier.</summary>
        public static void Module(ExpeditionQuotaModel m,float W,float Hh,float t,bool reduced,bool glow)
        {
            if(!m.Shown||Event.current.type!=EventType.Repaint)return;Measure(m);
            Color tone=E.Tone(m.Tone);var r=new Rect(m.X,m.Y,m.W,m.H);float tall=m.Tall;
            float breath=reduced?.5f:Q.Breath(t,false),beat=m.Beat?m.BeatAge:Q.Never;
            // The beat's streak and the top edge's glow, behind the module (the top band only).
            if(beat<.9f&&!reduced)
            {
                float k=beat/.9f,reach=W*.34f*Q.Out(beat/.5f),cy=r.y+Q.TallH*.5f;
                U.Glow(new Rect(W/2-reach,cy-1,reach*2,2),C.Good(.55f*(1-k)),8);U.Fill(new Rect(W/2-reach,cy,reach*2,H.Snap(1)),C.Ink(.55f*(1-k)));
                float e=Mathf.Min(W,Hh)*.045f;if(beat<.8f)U.Glow(new Rect(0,-e,W,e),C.Good(.32f*(1-beat/.8f)),e*1.4f);
            }
            // A halo in the tone so the glass reads over the darkest maps; it breathes while closing in.
            if(glow)U.Glow(r,U.A(tone,.1f+.2f*m.Heat*breath),10);
            H.Panel(r,1,tone,R.Cut,H.Corners.Diagonal,true,R.PanelAlpha);
            if(m.Heat>0)H.Outline(r,U.A(tone,.3f+.4f*m.Heat*breath),R.Cut);
            if(beat<Q.BeatFlash&&!reduced)H.Cut(r,C.Ink(.45f*(1-beat/Q.BeatFlash)),R.Cut);
            U.BeginBand(r);
            try
            {
                if(tall>.01f)Tall(m,r,tone,tall,t,reduced,glow);
                if(tall<.99f)Compact(m,r,tone,1-tall);
                if(m.LastIn>.01f)LastRow(m,r,tall);
                if(m.HintIn>.01f)HintRow(m,r,tall,t,reduced);
            }
            finally{U.EndBand();}
        }

        // The tall body: the eyebrow row and the count, bar and kills left (or QUOTA COMPLETE through the beat).
        private static void Tall(ExpeditionQuotaModel m,Rect r,Color tone,float a,float t,bool reduced,bool glow)
        {
            float x=r.x+14,y=r.y+8,right=r.xMax-12;
            U.Fill(new Rect(r.x+8,y+2,H.Snap(2),10),U.A(U.Hex(m.Accent),a));
            U.Text(new Rect(x,y,areaW+4,14),m.AreaText,10,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            // The eyebrow (KILL QUOTA, the loop) and the PARTY label give way, in that order, to the zone's name and the pips on a narrow module.
            float pips=Mathf.Min(8,m.Party)*11,label=right-pips-partyW-8,eyebrowEnd=x+areaW+16+eyebrowW;bool showLabel=m.Party>0&&eyebrowEnd+8<=label;
            if(eyebrowEnd+8<=(showLabel?label:right-pips))
            {
                U.Fill(new Rect(x+areaW+7,y+6,H.Snap(2),H.Snap(2)),C.Muted(a));
                U.Text(new Rect(x+areaW+16,y,eyebrowW+4,14),m.Beat?m.DoneText:m.EyebrowText,10,m.Beat?C.Good(a):C.Muted(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            }
            else showLabel=m.Party>0&&x+areaW+8<=label;
            Party(m,right,y+7,a,showLabel);
            float row=r.y+24;
            if(m.Beat)
            {
                // QUOTA COMPLETE: the title pops in over the full bar, the count fades out under it.
                float k=m.BeatAge,inA=Mathf.Clamp01(k/.15f),grow=reduced?0:.25f*(1-Q.Out(k/Q.BeatTitle));
                var box=new Rect(r.x,row,r.width,30);U.Push(box.center,Vector2.zero,1+grow);
                U.Text(box,Q.CompleteTitle,21,C.Good(a*inA),TextAnchor.MiddleCenter,bold:true,tracking:.12f,glow:glow?C.Good(.5f*a*inA):default,shadow:C.Void(.8f*a*inA));U.Pop();
                if(inA<1)Count(m,x,row,tone,a*(1-inA),false);
                var line=new Rect(x,r.y+Q.TallH-9,r.width-28,3);U.Fill(line,C.Good(a));
                if(!reduced&&k<.6f){float s=k/.6f;U.Fill(new Rect(line.x+(line.width-30)*s,line.y-1,30,line.height+2),C.Ink(.7f*a*(1-s)));}
                return;
            }
            Count(m,x,row,tone,a,true);
            float bx=x+countBox+4+targetBox+10,bw=right-leftBox-12-bx;
            Bar(m,new Rect(bx,row+11,bw,8),tone,a,reduced,glow);
            U.Text(new Rect(right-leftBox,row,leftBox,30),m.LeftText,11,U.A(tone,a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.08f);
        }
        // The authority's count (popping on a kill) over the target.
        private static void Count(ExpeditionQuotaModel m,float x,float row,Color tone,float a,bool pop)
        {
            var box=new Rect(x,row,countBox,30);if(pop&&m.Pop>0)U.Push(new Vector2(box.xMax,box.center.y),Vector2.zero,1+m.Pop);
            U.Text(box,m.CountText,26,C.Ink(a),TextAnchor.MiddleRight,bold:true,mono:true,glow:U.A(tone,.28f*a));if(pop&&m.Pop>0)U.Pop();
            U.Text(new Rect(x+countBox+4,row+4,targetBox,26),m.TargetText,13,C.Muted(a),TextAnchor.MiddleLeft,mono:true);
        }
        // The bar: a cell per kill (or per group of kills on a large target); new cells flash, a light runs to the fill's edge, and the
        // last few cells turn hollow in the hunt tone while they are marked.
        private static void Bar(ExpeditionQuotaModel m,Rect b,Color tone,float a,bool reduced,bool glow)
        {
            if(b.width<20)return;int n=Mathf.Max(1,m.Cells),filled=Q.CellOf(m.Progress,m.Target);float gap=b.width/n>6?2:b.width/n>3?1:0,cw=(b.width-gap*(n-1))/n;
            bool last=m.Stage==ExpeditionQuotaStage.Last;float flash=reduced||m.KillAge>=Q.KillFlash?0:1-m.KillAge/Q.KillFlash;
            for(int i=0;i<n;i++)
            {
                var c=new Rect(b.x+i*(cw+gap),b.y,cw,b.height);
                if(i<filled){U.Fill(c,U.A(tone,a));if(flash>0&&i>=m.FlashFrom&&i<m.FlashTo)U.Fill(new Rect(c.x,c.y-1,c.width,c.height+2),C.Ink(.85f*flash*a));}
                else if(last){U.Fill(c,C.Danger(.18f*a));U.Fill(new Rect(c.x,c.y,c.width,H.Snap(1)),C.Danger(.8f*a));}
                else U.Fill(c,C.Void(.75f*a));
            }
            float edge=b.x+(filled>=n?b.width:filled*(cw+gap)-gap);
            if(glow&&filled>0&&filled<n)U.Glow(new Rect(edge-2,b.y,4,b.height),U.A(tone,.45f*a),5);
            if(!reduced&&m.KillAge<Q.Sweep&&filled>0){float s=m.KillAge/Q.Sweep,x=b.x+(edge-b.x)*s;U.Fill(new Rect(Mathf.Max(b.x,x-14),b.y-1,Mathf.Min(14,x-b.x),b.height+2),C.Ink(.4f*a*(1-s)));}
        }
        // The party in this zone: a pip per pilot (at most eight), lit while up, hollow in the danger tone while down.
        private static void Party(ExpeditionQuotaModel m,float right,float cy,float a,bool label)
        {
            int n=Mathf.Min(8,m.Party);if(n<=0)return;float step=11,x=right-n*step;
            for(int i=0;i<n;i++)
            {
                var c=new Rect(x+i*step,cy-5,10,10);
                if(i<m.Alive)NativeIcons.DrawMarkup(c,"quota-pip",Pip,24,C.Good(a));else NativeIcons.DrawMarkup(c,"quota-pip-edge",PipEdge,24,C.Danger(a));
            }
            if(label)U.Text(new Rect(x-partyW-8,cy-7,partyW+4,14),Q.PartyLabel,9,C.Muted(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
        }
        // The compact strip (the boss, the cleared zone): the zone and its state, a thin full line along its foot (the quota done).
        private static void Compact(ExpeditionQuotaModel m,Rect r,Color tone,float a)
        {
            float y=r.y+(Q.CompactH-30)*.5f;U.Fill(new Rect(r.x+8,y+10,H.Snap(2),10),U.A(tone,a));
            U.Text(new Rect(r.x+14,y,r.width-24,30),m.CompactText,compactSize,U.A(tone,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:compactTrack);
            U.Fill(new Rect(r.x+12,r.y+Q.CompactH-4,r.width-24,H.Snap(2)),C.Good(.55f*a));
        }
        // LAST n · MARKED, the markers' own words and kite, in the hunt tone under the body.
        private static void LastRow(ExpeditionQuotaModel m,Rect r,float tall)
        {
            float a=m.LastIn*tall,y=r.y+Q.TallH-2,h=Q.LastRow*m.LastIn;var row=new Rect(r.x+1,y,r.width-2,h);
            U.Fill(row,C.Danger(.1f*a));U.Fill(new Rect(row.x+10,row.y,row.width-20,H.Snap(1)),C.Danger(.45f*a));
            NativeQuotaMarksHud.Kite(new Vector2(row.x+20,row.y+11),13,a,false);
            U.Text(new Rect(row.x+32,row.y+1,row.width-40,20),m.LastText,11,C.Danger(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f,shadow:C.Void(.7f*a));
        }
        // The beat's hint: what comes next (the boss, or the exit), with a slow blink.
        private static void HintRow(ExpeditionQuotaModel m,Rect r,float tall,float t,bool reduced)
        {
            bool exit=m.Stage==ExpeditionQuotaStage.Cleared;Color tone=exit?C.Good():C.Magenta();float a=m.HintIn*tall*(reduced?1:.72f+.28f*Mathf.Cos(t*5.2f));
            float y=r.y+Q.TallH-2;var row=new Rect(r.x+1,y,r.width-2,Q.HintRow*m.HintIn);
            U.Fill(row,U.A(tone,.1f*a));U.Fill(new Rect(row.x+10,row.y,row.width-20,H.Snap(1)),U.A(tone,.45f*a));
            for(int side=-1;side<=1;side+=2)NativeIcons.DrawMarkup(new Rect(row.center.x+side*(r.width*.5f-22)-4,row.y+8,8,8),"quota-pip",Pip,24,U.A(tone,a));
            U.Text(new Rect(row.x,row.y+1,row.width,22),m.HintText,11,U.A(tone,a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.22f,shadow:C.Void(.7f*a));
        }
    }

    public sealed partial class ConversionGame
    {
        private readonly ExpeditionQuotaModel expeditionQuota=new ExpeditionQuotaModel();
        /// <summary>ExpeditionLiveSync: the quota module follows the bound view every frame (a quota met while the J screen is open still
        /// plays its sting then, and the beat is not replayed when the HUD returns); the sting through the PvE clear cue (its bank key, else
        /// today's "go"), once per quota.</summary>
        private void ExpeditionQuotaSync(ExpeditionView v)
        {
            if(v==null){expeditionQuota.Clear();return;}
            expeditionQuota.Step(v,Time.unscaledTime,prefs.reducedEffects,U.W);
            if(expeditionQuota.CueNow)nativeAudio?.Cue("pve.clear","go",null,.6f,mine:true);
        }
        /// <summary>ExpeditionLiveHud, after the minimal HUD: the module in the top centre the objective tag gave up for it.</summary>
        private void ExpeditionQuotaHud(float width,float height)
        {
            if(!expeditionQuota.Shown||Event.current.type!=EventType.Repaint)return;
            NativeExpeditionQuotaHud.Module(expeditionQuota,width,height,Time.unscaledTime,prefs.reducedEffects,!presentation.LowGraphics);
        }
        /// <summary>Where the strip under the top centre starts (the transfer banner, the boss plates): under the module while it shows.</summary>
        private float ExpeditionTopStack=>Mathf.Max(58,expeditionQuota.Bottom+8);
    }
}
