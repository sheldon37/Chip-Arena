using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using R=ChipArena.UnityConversion.NativeHudCyber;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition state screens (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md "Doors, gate, sanctuary, transfer and
    /// boss arrival"), in the cyber HUD kit from the Core models only (ExpeditionTransferModel, ExpeditionSafeModel,
    /// ExpeditionBossModel), whose strings are cached: nothing is concatenated per frame here.
    ///  - Transfer: the area transfer screen while areaBarrier is active (ENTERING WARD ZERO / RETURNING TO THE CITY, the party's
    ///    ready / required as pips and a bar, the clock note), faded in and out with the map load.
    ///  - Safe: the local pilot's SAFE · SANCTUARY chip (mine.safe) and the party's safe count and names (members[].safe).
    ///  - BossIntro: the arrival banner with the archetype's name (TEST kept) and a short sting at the screen's edges that never
    ///    moves the camera or covers the crosshair; BossPlates: the HUD boss plates after it (and for late joiners).
    ///  - GateNotice: FINAL GATE OPEN when this client sees the final gate open (with the 3D beacon).
    /// </summary>
    public static class NativeExpeditionStatesHud
    {
        public const string GateTitle="FINAL GATE OPEN",GateSub="THE FINAL AREA IS UNLOCKED",Active="ACTIVE",Down="DOWN",Boss="BOSS";

        // ---- Area transfer -----------------------------------------------------------------------------------------------
        public static void Transfer(ExpeditionTransferModel m,float W,float Hh,float a,float t,bool reduced)
        {
            if(a<=0||m.Title==null)return;
            U.Fill(new Rect(0,0,W,Hh),C.Void(.93f*a));
            // Edge rules with ticks, and a slow chevron drift behind the panel.
            for(int i=0;i<2;i++){float ry=i==0?Hh*.16f:Hh*.84f;U.Fill(new Rect(W*.06f,ry,W*.88f,H.Snap(1)),C.Edge(.8f*a));for(int k=0;k<=12;k++)U.Fill(new Rect(W*.06f+W*.88f*k/12,ry-(k%3==0?5:2),H.Snap(1),k%3==0?10:4),C.CyanSoft(.35f*a));}
            float drift=reduced?0:(t*28)%80;
            for(float x=-80+drift;x<W;x+=80){U.Fill(new Rect(x,Hh*.5f-1,34,H.Snap(2)),C.Cyan(.06f*a));}
            float pw=Mathf.Min(900,W-96),ph=236,px=W/2-pw/2,py=Hh/2-ph/2+(1-a)*10;var r=new Rect(px,py,pw,ph);
            H.Panel(r,a,C.Cyan(),R.Cut,H.Corners.Diagonal,true,.95f);
            U.Text(new Rect(px+28,py+22,pw-56,16),ExpeditionTransferModel.Eyebrow,12,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            float size=Mathf.Clamp(pw*.052f,26,44);
            U.Text(new Rect(px+28,py+46,pw-56,size+12),m.Title,size,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.06f,glow:C.Cyan(.35f*a),shadow:new Color(0,0,0,.7f*a));
            float y=py+56+size+18;U.Fill(new Rect(px+28,y,pw-56,H.Snap(1)),C.Edge(.9f*a));y+=18;
            // The party's readiness: one pip per member the barrier waits for (at most eight), then the bar.
            int n=Mathf.Min(8,m.Required);float pip=26,gap=8;
            for(int i=0;i<n;i++)
            {
                var c=new Rect(px+28+i*(pip+gap),y,pip,12);bool ready=i<m.ReadyCount;
                if(ready){U.Fill(c,C.Good(a));U.Glow(c,C.Good(.25f*a),4);}else{U.Fill(c,C.Navy(.9f*a));H.Outline(c,C.Edge(a),0,H.Corners.None);}
            }
            U.Text(new Rect(px+28+(n>0?n*(pip+gap)+6:0),y-5,pw-56,22),m.Ready,15,m.Required>0&&m.ReadyCount==m.Required?C.Good(a):C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            y+=28;H.Line(new Rect(px+28,y,pw-56,4),m.Fill,C.Good(a),C.Void(.8f*a));y+=16;
            if(m.Detail!=null)U.Text(new Rect(px+28,y,pw-56,16),m.Detail,11,C.Muted(a),TextAnchor.MiddleLeft,mono:true,tracking:.1f);
            // The loader: a bright run along the panel's foot, and the scan sweep over the panel.
            var foot=new Rect(px+28,py+ph-22,pw-56,3);U.Fill(foot,C.Navy(.9f*a));
            if(!reduced){float k=(t/1.4f)%1;float run=foot.width*.22f,x0=foot.x+(foot.width+run)*k-run;var seg=new Rect(Mathf.Max(foot.x,x0),foot.y,Mathf.Min(foot.xMax,x0+run)-Mathf.Max(foot.x,x0),foot.height);if(seg.width>0)U.Fill(seg,C.Cyan(a));
                float sy=py+ph*((t/2.2f)%1);U.Fill(new Rect(px+4,sy,pw-8,H.Snap(1)),C.CyanSoft(.12f*a));}
            else U.Fill(new Rect(foot.x,foot.y,foot.width*.22f,foot.height),C.Cyan(a));
        }

        // ---- Sanctuary membership ----------------------------------------------------------------------------------------
        /// <summary>The SAFE chip at (x, y) when the authority has the local pilot in the sanctuary, and the party line under it.</summary>
        public static void Safe(ExpeditionView v,ExpeditionSafeModel m,float x,float y)
        {
            if(!m.Shown&&m.Line==null)return;
            if(m.Shown)
            {
                float w=U.Measure(ExpeditionSafeModel.Chip,12,true,true,.14f).x+34;var r=new Rect(x,y,w,28);H.Tag(r,C.Good(),1);U.Glow(r,C.Good(.12f),6);
                U.Text(new Rect(r.x+14,r.y,w-14,r.height),ExpeditionSafeModel.Chip,12,C.Good(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);y+=32;
            }
            if(m.Line==null)return;
            U.Text(new Rect(x+2,y,320,15),m.Line,10,C.Good(.9f),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f,shadow:new Color(0,0,0,.7f));
            if(m.Names!=null)U.Text(new Rect(x+2,y+15,320,15),m.Names,10,C.Muted(),TextAnchor.MiddleLeft,mono:true,shadow:new Color(0,0,0,.7f));
        }

        // ---- Boss arrival --------------------------------------------------------------------------------------------------
        public static void BossIntro(ExpeditionBossModel m,float W,float Hh,float age,bool reduced)
        {
            if(m.Name==null||age<0)return;float end=(float)NativeExpeditionStates.IntroSeconds,a,s=1,dy=0;
            if(age<.18f){float k=age/.18f;a=k;s=reduced?1:1.3f-.3f*k*(2-k);}else if(age<end-.8f)a=1;else{float k=Mathf.Clamp01((age-(end-.8f))/.8f);a=1-k;dy=-10*k;}
            // The sting: a magenta pulse at the screen's edges (never the centre, never the camera).
            float sting=reduced?0:Mathf.Clamp01(1-age/.7f);
            if(sting>0){float e=Mathf.Min(W,Hh)*.05f;U.Glow(new Rect(-e,0,e,Hh),C.Magenta(.45f*sting),e*1.6f);U.Glow(new Rect(W,0,e,Hh),C.Magenta(.45f*sting),e*1.6f);U.Glow(new Rect(0,-e,W,e),C.Magenta(.35f*sting),e*1.4f);}
            float F=Mathf.Clamp(W*.05f,34,72),top=Hh*.22f;
            U.Push(new Vector2(W/2,top+F/2),new Vector2(0,dy),s);
            float ew=U.Measure(m.Area,12,true,true,.2f).x+36;var tag=new Rect(W/2-ew/2,top-36,ew,26);H.Tag(tag,C.Magenta(),a);
            U.Text(tag,m.Area,12,C.Magenta(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.2f);
            var box=new Rect(0,top,W,F);
            if(!reduced&&age<.6f){float g=age/.6f;U.SplitCopy(box,box,g,false,m.Name,F,a,TextAnchor.MiddleCenter,.06f);U.SplitCopy(box,box,(g+.37f)%1,true,m.Name,F,a,TextAnchor.MiddleCenter,.06f);}
            U.Text(box,m.Name,F,C.Ink(a),TextAnchor.MiddleCenter,bold:true,tracking:.06f,glow:C.Magenta(.7f*a),shadow:new Color(0,0,0,a));
            float rule=Mathf.Min(W*.5f,640)*Mathf.Clamp01(age/.3f);U.Fill(new Rect(W/2-rule/2,top+F+10,rule,H.Snap(2)),C.Magenta(.85f*a));
            if(!reduced&&age<.5f){float sy=top-30+(F+50)*(age/.5f);U.Fill(new Rect(W*.2f,sy,W*.6f,H.Snap(1)),C.CyanSoft(.5f*(1-age/.5f)));}
            U.Pop();
        }
        /// <summary>The HUD boss plates: each arrived boss of this area by archetype, its health bar and percentage from
        /// bossArrivals[].hp / maxHp (none on a legacy record: ACTIVE), DOWN once defeated. A plate waits for its intro to finish,
        /// so late joiners see only the plate.</summary>
        public static void BossPlates(ExpeditionBossModel m,float W,float y,float t,bool reduced)
        {
            float pw=380,x=W/2-pw/2;
            for(int i=0;i<m.PlateCount;i++)
            {
                double age=m.PlateAge[i];float a=age==double.PositiveInfinity?1:Mathf.Clamp01((float)(age-(NativeExpeditionStates.IntroSeconds-.4))/.4f);if(a<=0)continue;
                bool down=m.PlateDown[i];float health=m.PlateHealth[i];Color tone=down?C.Muted():C.Magenta();var r=new Rect(x,y,pw,health>=0?40:32);H.Panel(r,a,tone,R.TagCut);
                if(!down&&!reduced)U.Glow(new Rect(r.x,r.yMax-2,r.width,2),C.Magenta(.25f*a*(.6f+.4f*Mathf.Sin(t*3))),4);
                U.Text(new Rect(r.x+14,r.y,60,32),Boss,10,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
                U.Text(new Rect(r.x+64,r.y,pw-150,32),m.PlateNames[i],14,down?C.Muted(a):C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.06f);
                string state=down?Down:m.PlatePercent[i]??Active;float sw=U.Measure(state,10,true,true,.12f).x+22;var st=new Rect(r.xMax-12-sw,r.y+6,sw,20);H.Tag(st,tone,a);
                U.Text(new Rect(st.x+10,st.y,sw-10,st.height),state,10,U.A(tone,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
                if(health>=0)H.Line(new Rect(r.x+14,r.y+31,pw-28,4),health,down?C.Muted(a):C.Magenta(a),C.Void(.8f*a));
                y+=r.height+6;
            }
        }
        // ---- SAFE over a pilot ------------------------------------------------------------------------------------------
        private static float safeWidth;
        /// <summary>The SAFE tag over a pilot: `bar` is the overhead bar's top centre (canvas units); it sits over the name row,
        /// in the chip-select tag's slot. A green tick and diamond, no shield (it is sanctuary membership, not protection).</summary>
        public static void SafeTag(Vector2 bar,float a)
        {
            if(a<=0)return;if(safeWidth<=0)safeWidth=U.Measure(ExpeditionSafeTags.Text,10,true,true,.14f).x;
            float h=18,w=20+safeWidth+10;var r=new Rect(Mathf.Round(Mathf.Clamp(bar.x-w/2,8,U.W-8-w)),Mathf.Round(bar.y-27-4-h),w,h);
            H.Cut(r,C.Navy(.82f*a),R.TagCut);H.Outline(r,C.Good(.6f*a),R.TagCut);U.Fill(new Rect(r.x+2,r.y+r.height*.28f,2,r.height*.44f),C.Good(a));
            U.Fill(new Rect(r.x+9,r.center.y-3,6,6),C.Good(a));
            U.Text(new Rect(r.x+20,r.y,safeWidth+6,h),ExpeditionSafeTags.Text,10,C.Good(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f,shadow:new Color(0,0,0,.7f*a));
        }
        /// <summary>FINAL GATE OPEN, for GateNoticeSeconds from the gate's opening (this client saw it open).</summary>
        public static void GateNotice(float W,float Hh,float age,bool reduced)
        {
            float end=(float)NativeExpeditionStates.GateNoticeSeconds,a=age<.2f?age/.2f:age>end-.6f?Mathf.Clamp01((end-age)/.6f):1;if(a<=0)return;
            float F=Mathf.Clamp(W*.036f,26,52),top=Hh*.3f;
            U.Text(new Rect(0,top,W,F),GateTitle,F,C.Ink(a),TextAnchor.MiddleCenter,bold:true,tracking:.08f,glow:C.Magenta(.6f*a),shadow:new Color(0,0,0,a));
            float sw=U.Measure(GateSub,12,true,true,.16f).x+30;var r=new Rect(W/2-sw/2,top+F+10,sw,24);H.Tag(r,C.Magenta(),a,false);
            U.Text(r,GateSub,12,C.CyanSoft(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.16f);
            if(!reduced&&age<.8f){float w=W*.5f*(age/.8f);U.Fill(new Rect(W/2-w/2,top-8,w,H.Snap(2)),C.Magenta((1-age/.8f)*a));}
        }
    }
}
