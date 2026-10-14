using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using P=ChipArena.UnityConversion.NativeExpeditionPresentation;
using R=ChipArena.UnityConversion.NativeHudCyber;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition screens (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md), drawn in the cyber HUD kit (NativeHudChrome panels,
    /// NativeCyberTheme palette, NativeUi canvas) from an ExpeditionView and the Core models of NativeExpeditionPresentation only.
    /// Nothing here holds or changes the authority: a control returns an ExpeditionCommand for the caller to send, and its caption
    /// shows the command's tracked state (pending, confirmed by the authority, refused, unavailable with the reason, failed).
    ///  - Minimal in-match HUD: personal wallet, objective line, the running countdown, chips waiting for you.
    ///  - World board (buildings, sealed doors, quotas, final area, loop and difficulty clock, party), loot (ordinary first-pickup
    ///    drops against reserved boss chips, eligibility and claimed state), personal chip panel, sanctuary (checkpoint,
    ///    individual and group extraction, reprint machine), building (door, bosses, departure vote), victory choices,
    ///    the dice reveal of a stored record, and the host-transfer overlay.
    /// </summary>
    public static class NativeExpeditionHud
    {
        /// <summary>What the controls need from the caller: the send path's availability and tracked state, and names.</summary>
        public sealed class Controls
        {
            public Func<ExpeditionCommand,string> Blocked=_=>ExpeditionGuestCommands.NoChannel;
            public Func<ExpeditionCommand,ExpeditionCommandResult> State=_=>default;
            public Func<string,string> Name=a=>string.IsNullOrEmpty(a)?"?":a.ToUpperInvariant();
            public Func<string,string> ChipName=id=>id;
            /// <summary>A chip's rarity colour (content data), drawn as a tick, never as text.</summary>
            public Func<string,Color> ChipTone=_=>C.CyanSoft();
        }

        public static Color Tone(ExpeditionTone t,float a=1)
        {
            switch(t)
            {
                case ExpeditionTone.Muted:return C.Muted(a);case ExpeditionTone.Cyan:return C.Cyan(a);case ExpeditionTone.CyanSoft:return C.CyanSoft(a);
                case ExpeditionTone.Magenta:return C.Magenta(a);case ExpeditionTone.Warn:return C.Warn(a);case ExpeditionTone.Good:return C.Good(a);
                case ExpeditionTone.Danger:return C.Danger(a);default:return C.Ink(a);
            }
        }

        // ---- Kit -----------------------------------------------------------------------------------------------------------
        private static float Header(Rect r,string eyebrow,string title,Color tone,float a,float size=20)
        {
            U.Text(new Rect(r.x+18,r.y+14,r.width-36,14),eyebrow,11,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            U.Text(new Rect(r.x+18,r.y+30,r.width-36,size+6),title,size,U.A(tone,a),TextAnchor.MiddleLeft,bold:true,tracking:.04f,glow:U.A(tone,.25f*a));
            return r.y+38+size;
        }
        private static float Lines(float x,float y,float w,string text,float size,Color c,int max=3,bool bold=false)
        {
            if(string.IsNullOrEmpty(text))return y;float lh=size*1.4f;
            foreach(var line in U.Wrap(text,size,w,bold,max)){U.Text(new Rect(x,y,w,lh),line,size,c,TextAnchor.MiddleLeft,bold:bold);y+=lh;}
            return y;
        }
        /// <summary>A state tag at the right of a row (chamfered pill with the tone's tick); returns its left edge.</summary>
        private static float StateTag(float right,float y,string text,ExpeditionTone tone,float a,float h=20)
        {
            float w=U.Measure(text,10,true,true,.1f).x+22;var r=new Rect(right-w,y,w,h);H.Tag(r,Tone(tone),a);
            U.Text(new Rect(r.x+10,r.y,w-10,h),text,10,Tone(tone,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);return r.x;
        }
        /// <summary>
        /// A command button with its caption: enabled only when the view allows it (`possible`), the send path is open and nothing is
        /// pending; the caption is the tracked state, else the reason it cannot be sent. Returns the y under the caption.
        /// </summary>
        private static float Action(Rect r,string label,ExpeditionCommand command,bool possible,Controls c,float a,bool primary,out bool pressed,bool quiet=false)
        {
            string blocked=c.Blocked(command);var state=c.State(command);
            bool enabled=possible&&blocked==null&&state.state!=ExpeditionCommandState.Pending;
            pressed=U.Btn(r,label,primary&&enabled,enabled,a,13);
            string caption=P.Feedback(state,out var tone);
            if(caption==null&&possible&&blocked!=null&&!quiet){caption="Unavailable · "+blocked;tone=ExpeditionTone.Warn;}
            return Lines(r.x,r.yMax+4,Mathf.Max(r.width,260),caption,11,Tone(tone,a),3);
        }
        private static Rect Inner(Rect r,float y,float h)=>new Rect(r.x+18,y,r.width-36,h);
        private static void Rule(Rect r,float y,float a)=>U.Fill(new Rect(r.x+18,y,r.width-36,H.Snap(1)),C.Edge(.8f*a));

        // ---- Minimal in-match HUD ------------------------------------------------------------------------------------------
        /// <summary>The minimal HUD: wallet top left (as the PvE money panel), the objective tag top centre with the running
        /// countdown under it, and a magenta tag top right while chips of yours wait to be opened or picked.</summary>
        public static void Minimal(ExpeditionView v,float W,float a=1)
        {
            if(P.Has(v.mine.account)&&!v.mine.extracted)
            {
                var mr=new Rect(24,24,220,58);H.Panel(mr,a,C.Warn());
                U.Text(new Rect(mr.x+14,mr.y+8,mr.width-28,26),P.Money(v.mine.wallet),22,C.Warn(a),TextAnchor.MiddleLeft,bold:true,mono:true,glow:C.Warn(.3f*a));
                U.Text(new Rect(mr.x+14,mr.y+34,mr.width-28,16),v.mine.catchup>0?"YOUR WALLET · "+v.mine.catchup+" CATCH-UP":"YOUR WALLET",11,C.Muted(a),TextAnchor.MiddleLeft,mono:true,tracking:.08f);
            }
            string objective=P.Objective(v,out var tone);float ow=U.Measure(objective,12,true,true,.14f).x+44;var or=new Rect(W/2-ow/2,20,ow,30);
            H.Tag(or,Tone(tone),a);U.Text(new Rect(or.x+16,or.y,ow-20,30),objective,12,Tone(tone,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            var cd=P.Active(v);
            if(cd.active)
            {
                var cr=new Rect(W/2-160,58,320,50);H.Panel(cr,a,Tone(cd.tone),R.TagCut);
                U.Text(new Rect(cr.x+14,cr.y+6,cr.width-80,16),cd.label,11,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
                U.Text(new Rect(cr.x,cr.y+4,cr.width-14,20),cd.unanimous?"NOW":cd.seconds+"s",17,Tone(cd.tone,a),TextAnchor.MiddleRight,bold:true,mono:true);
                H.Line(new Rect(cr.x+14,cr.y+28,cr.width-28,3),cd.fraction,Tone(cd.tone,a),C.Void(.7f*a));
                U.Text(new Rect(cr.x+14,cr.y+33,cr.width-28,14),cd.votes+" / "+cd.needed+(cd.label=="GROUP EXTRACTION"?" VOTES · ONLY VOTERS LEAVE":" VOTED"),10,C.Muted(a),TextAnchor.MiddleLeft,mono:true,tracking:.08f);
            }
            int waiting=0;foreach(var r in v.mine.rewards)if(!r.consumed)waiting++;
            if(waiting>0&&!v.mine.extracted)
            {
                string chips="YOUR CHIPS · "+waiting+" WAITING";float cw=U.Measure(chips,12,true,true,.1f).x+30;var cr=new Rect(W-24-cw,24,cw,30);
                H.Tag(cr,C.Magenta(),a);U.Text(new Rect(cr.x+14,cr.y,cw,cr.height),chips,12,C.Magenta(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            }
        }

        // ---- World board ---------------------------------------------------------------------------------------------------
        /// <summary>Buildings (state, quota bar, sealed door), the final area, the difficulty clock and the party.</summary>
        public static void Board(Rect r,ExpeditionView v,Controls c,float a=1)
        {U.BeginBand(r);try{
            H.Panel(r,a,C.Cyan());float y=Header(r,P.Name(v.world)+" · LOOP "+(v.loop+1),"World board",C.Ink(),a);
            void Row(P.BoardRow b)
            {
                var row=Inner(r,y,46);if(b.current)U.Fill(new Rect(row.x-10,row.y-2,3,row.height),Tone(b.tone,a));
                U.Text(new Rect(row.x,row.y,row.width-120,20),b.name,14,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:.04f);
                StateTag(row.xMax,row.y,b.sealedDoor?b.state+" · DOOR":b.state,b.tone,a);
                H.Line(new Rect(row.x,row.y+24,row.width,4),b.fill,Tone(b.tone,.9f*a),C.Void(.7f*a));
                U.Text(new Rect(row.x,row.y+30,row.width,15),b.detail,11,C.Muted(a),TextAnchor.MiddleLeft);y+=54;
            }
            foreach(var b in P.Board(v))Row(b);
            Rule(r,y-4,a);Row(P.Final(v));Rule(r,y-4,a);
            // The difficulty clock: elapsed active time, the step and the authority's scaling read-out.
            var clock=Inner(r,y+2,30);U.Text(clock,P.Clock(v.elapsed),24,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true);
            U.Text(clock,"STEP "+v.difficultyStep+" · LOOP "+(v.loop+1),11,C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.1f);y+=34;
            var d=v.difficulty;U.Text(Inner(r,y,16),"ENEMY HP "+P.Multiplier(d.health)+"   DMG "+P.Multiplier(d.damage)+"   COUNT "+P.Multiplier(d.count),11,C.Ink(a),TextAnchor.MiddleLeft,mono:true);y+=18;
            y=Lines(r.x+18,y,r.width-36,v.loading?"Loading: the clock is paused.":v.migrating?"Host transfer: the clock is frozen.":"The clock keeps running at the sanctuary and while choosing chips.",10,C.Muted(a),2);
            y+=6;Rule(r,y,a);y+=8;
            foreach(var m in P.Roster(v))
            {
                if(y+18>r.yMax-10)break;var row=Inner(r,y,18);
                U.Text(row,c.Name(m.account)+(m.host?"  · HOST":"")+(m.you?"  · YOU":""),12,m.you?C.CyanSoft(a):C.Ink(a),TextAnchor.MiddleLeft,bold:m.you);
                U.Text(row,m.status,10,Tone(m.tone,a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.08f);y+=21;
            }
        }finally{U.EndBand();}}

        // ---- Loot ----------------------------------------------------------------------------------------------------------
        /// <summary>The loot marker shape: an ordinary drop is a cyan diamond (open to anyone); a reserved boss chip a magenta
        /// locked chip; not eligible a struck-through danger outline. Reused by world markers once drops have positions.</summary>
        private const string DiamondFill="<path d=\"M12 2L22 12L12 22L2 12Z\" fill=\"#fff\"/>",DiamondEdge="<path d=\"M12 2.5L21.5 12L12 21.5L2.5 12Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.8\"/>",
            Slash="<path d=\"M4 20L20 4\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.2\"/>";
        public static void Marker(Rect r,P.LootKind kind,P.LootState state,float a)
        {
            // Drawn from markup and axis-aligned fills (no rotated transform), so the marker survives the panels' clip bands.
            var center=r.center;bool open=state==P.LootState.Open||state==P.LootState.ReservedForYou||state==P.LootState.ClaimedByYou;
            if(state==P.LootState.NotEligible){H.Outline(r,C.Danger(a),3,H.Corners.Diagonal,1.5f);NativeIcons.DrawMarkup(r,"exp-slash",Slash,24,C.Danger(a));return;}
            Color tone=kind==P.LootKind.Ordinary?C.Cyan():C.Magenta();float fade=open?1:.45f;
            if(kind==P.LootKind.Ordinary){NativeIcons.DrawMarkup(r,"exp-diamond-fill",DiamondFill,24,U.A(tone,(open?.28f:.1f)*a));NativeIcons.DrawMarkup(r,"exp-diamond-edge",DiamondEdge,24,U.A(tone,fade*a));return;}
            H.Cut(r,U.A(tone,(open?.3f:.1f)*a),4);H.Outline(r,U.A(tone,fade*a),4,H.Corners.Diagonal,1.5f);
            // The lock: a shackle and body, for "reserved for one pilot".
            var body=new Rect(center.x-r.width*.22f,center.y-r.height*.02f,r.width*.44f,r.height*.3f);U.Fill(body,U.A(C.Ink(),fade*a));
            U.Round(new Rect(center.x-r.width*.15f,center.y-r.height*.26f,r.width*.3f,r.height*.32f),Color.clear,r.width*.15f,U.A(C.Ink(),fade*a),1.5f);
        }
        /// <summary>Ordinary drops against reserved boss chips, with the legend and each row's owner and claimed state.</summary>
        public static void Loot(Rect r,ExpeditionView v,float a=1)
        {U.BeginBand(r);try{
            H.Panel(r,a,C.Magenta());float y=Header(r,"ORDINARY: FIRST PICKUP · BOSS: RESERVED","Loot",C.Ink(),a);
            var legend=Inner(r,y,18);Marker(new Rect(legend.x,legend.y+1,16,16),P.LootKind.Ordinary,P.LootState.Open,a);
            U.Text(new Rect(legend.x+22,legend.y,150,18),"Drop · anyone",11,C.Cyan(a),TextAnchor.MiddleLeft,bold:true);
            Marker(new Rect(legend.x+150,legend.y+1,16,16),P.LootKind.Reserved,P.LootState.ReservedForYou,a);
            U.Text(new Rect(legend.x+172,legend.y,170,18),"Boss · one pilot",11,C.Magenta(a),TextAnchor.MiddleLeft,bold:true);y+=28;
            var rows=P.Loot(v);rows.Sort((x,z)=>Rank(x).CompareTo(Rank(z)));int shown=0;
            foreach(var row in rows)
            {
                if(y+40>r.yMax-24)break;var line=Inner(r,y,36);Marker(new Rect(line.x,line.y+6,22,22),row.kind,row.state,a);
                U.Text(new Rect(line.x+32,line.y,line.width-32,18),row.label,11,Tone(row.tone,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
                U.Text(new Rect(line.x+32,line.y+18,line.width-32,16),row.detail,11,row.state==P.LootState.NotEligible?C.Ink(a):C.Muted(a),TextAnchor.MiddleLeft);y+=40;shown++;
            }
            if(rows.Count==0)U.Text(Inner(r,y,18),"No loot on the field.",12,C.Muted(a),TextAnchor.MiddleLeft);
            else if(shown<rows.Count)U.Text(Inner(r,r.yMax-26,16),"+"+(rows.Count-shown)+" more",11,C.Muted(a),TextAnchor.MiddleLeft,mono:true);
        }finally{U.EndBand();}}
        private static int Rank(P.LootRow r)
        {
            switch(r.state){case P.LootState.ReservedForYou:case P.LootState.ClaimedByYou:return 0;case P.LootState.NotEligible:return 1;case P.LootState.Open:return 2;
                case P.LootState.TakenByYou:return 3;case P.LootState.ReservedForOther:return 4;default:return 5;}
        }

        // ---- Personal chip panel ---------------------------------------------------------------------------------------------
        private static string SourceLabel(string source)=>source=="chest"?"PURCHASED · YOURS":source=="boss"?"BOSS · RESERVED FOR YOU":source=="monster"?"DROP · PICKED UP":source=="catchup"?"CATCH-UP":source.ToUpperInvariant();
        private static ExpeditionTone SourceTone(string source)=>source=="chest"?ExpeditionTone.Warn:source=="boss"?ExpeditionTone.Magenta:source=="catchup"?ExpeditionTone.Good:ExpeditionTone.Cyan;
        /// <summary>Your wallet, your rewards (purchased, reserved, picked up, catch-up) with your own offers to pick from, and the
        /// chips you hold. Only the viewer's state exists in the view, so nothing here can show another pilot's choice.</summary>
        public static ExpeditionCommand Chips(Rect r,ExpeditionView v,Controls c,float a=1)
        {U.BeginBand(r);try{
            var result=default(ExpeditionCommand);H.Panel(r,a,C.Magenta());float y=Header(r,"PERSONAL · ONLY YOU SEE THIS","Your chips",C.Ink(),a);
            var me=v.mine;if(!P.Has(me.account)){U.Text(Inner(r,y,18),"You are not in this run.",12,C.Muted(a),TextAnchor.MiddleLeft);return result;}
            var wallet=Inner(r,y,24);U.Text(wallet,P.Money(me.wallet),20,C.Warn(a),TextAnchor.MiddleLeft,bold:true,mono:true);
            U.Text(wallet,me.catchup>0?me.catchup+" CATCH-UP PICKS LEFT":"WALLET",10,C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.1f);y+=32;
            float bottom=r.yMax-74;
            // One reason for every control here, rather than the same caption under each reward.
            string blocked=me.extracted?"You extracted: rewards you had not opened can no longer be opened.":c.Blocked(new ExpeditionCommand(ExpeditionIntent.Claim))??c.Blocked(new ExpeditionCommand(ExpeditionIntent.Select));
            if(blocked!=null&&me.rewards.Length>0)y=Lines(r.x+18,y,r.width-36,(me.extracted?"":"Unavailable · ")+blocked,11,me.extracted?C.Muted(a):C.Warn(a),3)+8;
            foreach(var reward in me.rewards)
            {
                if(reward.consumed)continue;if(y+56>bottom)break;var head=Inner(r,y,18);
                U.Text(head,SourceLabel(reward.source),11,Tone(SourceTone(reward.source),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
                if(P.Has(reward.boss))U.Text(head,P.Name(reward.boss),10,C.Muted(a),TextAnchor.MiddleRight,mono:true);y+=22;
                if(!reward.claimed)
                {
                    y=Action(new Rect(r.x+18,y,150,30),"Open",new ExpeditionCommand(ExpeditionIntent.Claim,reward.id),!me.extracted,c,a,true,out bool open,true)+8;if(open)result=new ExpeditionCommand(ExpeditionIntent.Claim,reward.id);continue;
                }
                U.Text(Inner(r,y,14),"CHOOSE ONE · THIS PICK IS YOURS ALONE",10,C.Muted(a),TextAnchor.MiddleLeft,mono:true,tracking:.08f);y+=18;
                foreach(var offer in reward.offers)
                {
                    if(y+34>bottom)break;var cell=Inner(r,y,30);var cmd=new ExpeditionCommand(ExpeditionIntent.Select,reward.id,offer);
                    string offerBlocked=c.Blocked(cmd);var st=c.State(cmd);bool enabled=offerBlocked==null&&st.state!=ExpeditionCommandState.Pending&&!me.extracted;
                    float hover=U.HoverAmount(offer.GetHashCode()^reward.id.GetHashCode(),cell,enabled);
                    H.Cut(cell,C.Indigo((.75f+.2f*hover)*a),R.TagCut);H.Outline(cell,U.Mix(C.Edge(a),C.Cyan(a),hover),R.TagCut);U.Fill(new Rect(cell.x+6,cell.y+7,3,cell.height-14),U.A(c.ChipTone(offer),a));
                    U.Text(new Rect(cell.x+16,cell.y,cell.width-90,cell.height),c.ChipName(offer),13,C.Ink(a*(enabled?1:.6f)),TextAnchor.MiddleLeft,bold:true);
                    U.Text(new Rect(cell.x,cell.y,cell.width-10,cell.height),"PICK",10,enabled?C.Cyan(a):C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.12f);
                    if(U.Button(cell,enabled))result=cmd;y+=34;
                }
                var pick=c.State(new ExpeditionCommand(ExpeditionIntent.Select,reward.id));string caption=P.Feedback(pick,out var tone);
                y=Lines(r.x+18,y,r.width-36,caption,11,Tone(tone,a),2)+8;
            }
            // Held chips: yours, by count.
            Rule(r,bottom,a);float hy=bottom+8;U.Text(Inner(r,hy,14),"HELD",10,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);hy+=18;
            float x=r.x+18;
            if(me.chips.Length==0)U.Text(Inner(r,hy,18),"No chips yet.",12,C.Muted(a),TextAnchor.MiddleLeft);
            foreach(var chip in me.chips)
            {
                string label=c.ChipName(chip.id)+(chip.count>1?" x"+chip.count:"");float w=U.Measure(label,11,true).x+20;if(x+w>r.xMax-18){if(hy>r.yMax-30)break;x=r.x+18;hy+=24;}
                var t=new Rect(x,hy,w,20);H.Tag(t,c.ChipTone(chip.id),a,true);U.Text(new Rect(t.x+9,t.y,w-9,t.height),label,11,C.Ink(a),TextAnchor.MiddleLeft,bold:true);x+=w+6;
            }
            return result;
        }finally{U.EndBand();}}

        // ---- Sanctuary -------------------------------------------------------------------------------------------------------
        /// <summary>The sanctuary: checkpoint confirmation, individual extraction, the group extraction vote and the reprint machine.</summary>
        public static ExpeditionCommand Sanctuary(Rect r,ExpeditionView v,Controls c,float a=1)
        {U.BeginBand(r);try{
            var result=default(ExpeditionCommand);H.Panel(r,a,C.Good());float y=Header(r,"SAFE LOCATION · CHECKPOINT AND EXTRACTION","Sanctuary",C.Ink(),a);
            U.Text(Inner(r,y,16),v.savedRevision>=0?"CHECKPOINT SAVED · REVISION "+v.savedRevision:"NO CHECKPOINT SAVED YET",10,v.savedRevision>=0?C.Good(a):C.Muted(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);y+=24;
            y=ExtractionBlock(r,y,v,c,a,ref result);Rule(r,y,a);y+=10;
            y=ReprintBlock(r,y,v,c,a,ref result);
            return result;
        }finally{U.EndBand();}}
        private static float ExtractionBlock(Rect r,float y,ExpeditionView v,Controls c,float a,ref ExpeditionCommand result)
        {
            var m=P.Extraction(v);float half=(r.width-36-16)/2,x0=r.x+18,x1=x0+half+16;
            // Individual extraction (left).
            U.Text(new Rect(x0,y,half,18),m.title,12,Tone(m.extracted?m.tone:ExpeditionTone.Warn,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            float ly=Lines(x0,y+22,half,m.detail,12,C.Ink(a),3)+6;
            if(!m.extracted){ly=Action(new Rect(x0,ly,Mathf.Min(170,half),32),m.hostNeedsTransfer?"Transfer host & extract":"Extract",new ExpeditionCommand(ExpeditionIntent.Extract),m.can,c,a,true,out bool go);if(go)result=new ExpeditionCommand(ExpeditionIntent.Extract);}
            else{var st=c.State(new ExpeditionCommand(ExpeditionIntent.Extract));string cap=P.Feedback(st,out var t);ly=Lines(x0,ly,half,cap,11,Tone(t,a),2);}
            // Group extraction vote (right).
            var g=P.GroupExtraction(v);U.Text(new Rect(x1,y,half,18),g.label,12,C.Warn(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            U.Text(new Rect(x1,y,half,18),g.active?g.seconds+"s":g.votes+" / "+g.needed,15,g.active?C.Warn(a):C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true);
            H.Line(new Rect(x1,y+22,half,3),g.active?g.fraction:g.needed>0?Mathf.Clamp01(g.votes/(float)g.needed):0,C.Warn(a),C.Void(.7f*a));
            float ry=Lines(x1,y+30,half,g.detail,12,C.Ink(a),3)+6;
            var vote=new ExpeditionCommand(g.voted?ExpeditionIntent.CancelExtraction:ExpeditionIntent.VoteExtraction);
            ry=Action(new Rect(x1,ry,Mathf.Min(170,half),32),g.voted?"Cancel my vote":"Vote to extract",vote,g.open&&P.Has(v.mine.account)&&v.mine.alive&&v.mine.safe,c,a,false,out bool pressed);if(pressed)result=vote;
            return Mathf.Max(ly,ry)+10;
        }
        private static float ReprintBlock(Rect r,float y,ExpeditionView v,Controls c,float a,ref ExpeditionCommand result)
        {
            U.Text(Inner(r,y,18),"REPRINT MACHINE",12,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);y+=22;
            y=Lines(r.x+18,y,r.width-36,"Carry a downed pilot on your wrist here; the reprint is paid from your wallet. Unabsorbed pilots can be recovered remotely at the sanctuary.",11,C.Muted(a),3)+4;
            bool any=false;
            foreach(var m in v.members)
            {
                if(m.alive||m.extracted||y+34>r.yMax-12)continue;any=true;var row=Inner(r,y,30);
                string who=c.Name(m.account)+(m.carriedBy==v.viewer?" · ON YOUR WRIST":P.Has(m.carriedBy)?" · CARRIED BY "+c.Name(m.carriedBy):" · NOT CARRIED")+(!m.connected?" · OFFLINE":"");
                U.Text(new Rect(row.x,row.y,row.width-170,row.height),who,12,C.Ink(a),TextAnchor.MiddleLeft,bold:true);
                var cmd=new ExpeditionCommand(ExpeditionIntent.Reprint,m.account);
                float after=Action(new Rect(row.xMax-160,row.y,160,30),m.reprintCost>0?"Reprint · "+m.reprintCost:"Reprint · cost unavailable",cmd,v.mine.alive&&v.mine.safe&&m.connected,c,a,false,out bool go);if(go)result=cmd;y=Mathf.Max(row.yMax+6,after+4);
            }
            if(!any)U.Text(Inner(r,y,18),"Nobody needs a reprint.",12,C.Muted(a),TextAnchor.MiddleLeft);
            return y+22;
        }

        // ---- Building --------------------------------------------------------------------------------------------------------
        /// <summary>Inside a building: the sealed entrance, quota, bosses, and the departure vote once every boss is down.</summary>
        public static ExpeditionCommand Building(Rect r,ExpeditionView v,Controls c,float a=1)
        {U.BeginBand(r);try{
            var result=default(ExpeditionCommand);var b=v.CurrentBuilding();if(b==null)return result;var row=P.Board(v)[Array.IndexOf(v.instances,b)];
            H.Panel(r,a,Tone(row.tone));Door(new Rect(r.xMax-18-96,r.y+16,96,64),!b.cleared,row.tone,a);
            float y=Header(r,"BUILDING · "+row.name,b.cleared?"Door open":b.bosses.Length>0?"Boss fight · door sealed":"Door sealed",Tone(row.tone),a);
            var bar=Inner(r,y,20);bar.width-=110;U.Text(bar,"KILL QUOTA",11,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);U.Text(bar,b.progress+" / "+b.target,14,C.Ink(a),TextAnchor.MiddleRight,bold:true,mono:true);y+=22;
            var quota=Inner(r,y,10);quota.width-=110;H.Bar(quota,b.progress,b.target,20,Tone(row.tone,a),C.Void(.75f*a));y+=20;
            if(b.bosses.Length>0)
            {
                U.Text(Inner(r,y,16),"BOSSES",11,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);y+=20;
                foreach(var boss in b.bosses)
                {
                    bool down=Array.IndexOf(b.defeated,boss)>=0;var line=Inner(r,y,20);U.Text(line,P.Name(boss),13,C.Ink(a),TextAnchor.MiddleLeft,bold:true);
                    StateTag(line.xMax,line.y,down?"DOWN":"ALIVE",down?ExpeditionTone.Good:ExpeditionTone.Magenta,a);y+=26;
                }
            }
            y=Lines(r.x+18,y+4,r.width-36,b.cleared?"Exit takes everyone together to the outdoor entrance. Dead or disconnected pilots don't vote.":"The entrance is unbreakable until every boss here is dead.",12,C.Muted(a),3)+8;
            var d=P.Departure(v);Rule(r,y,a);y+=10;
            var head=Inner(r,y,20);U.Text(head,d.label,12,C.Cyan(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            U.Text(head,d.unanimous?"NOW":d.active?d.seconds+"s":d.votes+" / "+d.needed,16,d.active?C.Cyan(a):C.Muted(a),TextAnchor.MiddleRight,bold:true,mono:true);y+=24;
            H.Line(Inner(r,y,3),d.active?d.fraction:0,C.Cyan(a),C.Void(.7f*a));y+=10;y=Lines(r.x+18,y,r.width-36,d.detail,12,C.Ink(a),2)+6;
            var cmd=new ExpeditionCommand(d.voted?ExpeditionIntent.CancelExit:ExpeditionIntent.VoteExit);
            Action(new Rect(r.x+18,y,190,32),d.voted?"Cancel my vote":"Vote to leave",cmd,d.open&&P.Has(v.mine.account)&&v.mine.alive&&v.mine.connected&&!v.mine.extracted,c,a,!d.voted,out bool pressed);if(pressed)result=cmd;
            return result;
        }finally{U.EndBand();}}
        /// <summary>The building entrance: sealed (two shut leaves with hazard bands and a lock bar in the state's tone) or open
        /// (leaves drawn apart, the gap lit). Reusable as the entrance's world-space hologram.</summary>
        public static void Door(Rect r,bool sealedDoor,ExpeditionTone tone,float a)
        {
            var t=Tone(tone);H.Outline(r,U.A(t,.8f*a),4,H.Corners.Top,1.5f);var inner=new Rect(r.x+5,r.y+5,r.width-10,r.height-5);
            float open=sealedDoor?0:inner.width*.32f,half=inner.width/2;
            for(int side=0;side<2;side++)
            {
                var leaf=side==0?new Rect(inner.x,inner.y,half-open,inner.height):new Rect(inner.x+half+open,inner.y,half-open,inner.height);if(leaf.width<=1)continue;
                U.Fill(leaf,C.Indigo(.95f*a));H.Outline(leaf,U.A(t,.55f*a),0,H.Corners.None);
                for(int k=0;k<3;k++)U.Fill(new Rect(leaf.x+2,leaf.y+leaf.height*(.2f+.25f*k),leaf.width-4,2),U.A(t,(sealedDoor?.6f:.25f)*a));
            }
            if(sealedDoor){var bar=new Rect(inner.x-3,inner.center.y-4,inner.width+6,8);U.Fill(bar,U.A(t,a));U.Fill(new Rect(inner.center.x-6,bar.y-4,12,16),C.Ink(a));}
            else U.Glow(new Rect(inner.center.x-open,inner.y,open*2,inner.height),U.A(t,.35f*a),8);
        }

        // ---- Victory and the rest of the run -----------------------------------------------------------------------------------
        /// <summary>Victory: each pilot extracts or stays; the host starts the harder loop for those who stay; everyone's choice.</summary>
        public static ExpeditionCommand Victory(Rect r,ExpeditionView v,Controls c,float a=1)
        {U.BeginBand(r);try{
            var result=default(ExpeditionCommand);H.Panel(r,a,C.Warn(),14,H.Corners.Diagonal,true,.94f);
            float y=Header(r,"LOOP "+(v.loop+1)+" COMPLETE · EVERY BOSS DOWN","VICTORY",C.Warn(),a,34);
            y=Lines(r.x+18,y,r.width-36,"Extract now with your build, or stay for loop "+(v.loop+2)+": the board resets, your build and the clock carry on, and enemies get harder. Each pilot chooses alone.",13,C.Ink(a),3)+10;
            y=ExtractionBlock(r,y,v,c,a,ref result);Rule(r,y,a);y+=10;
            // Each connected pilot chooses; the authority advances only after all remaining pilots consent.
            U.Text(Inner(r,y,18),"CONTINUE · LOOP "+(v.loop+2),12,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);y+=22;
            bool ready=v.continueVotes.Contains(v.viewer);
            var next=new ExpeditionCommand(ready?ExpeditionIntent.CancelContinue:ExpeditionIntent.VoteContinue);
            y=Action(new Rect(r.x+18,y,200,32),ready?"Cancel continue":"Continue to loop "+(v.loop+2),next,!v.mine.extracted&&v.mine.connected,c,a,true,out bool go)+6;if(go)result=next;
            y=Lines(r.x+18,y,r.width-36,"Waiting until every connected pilot chooses Continue or finishes extracting.",12,C.Muted(a),3)+6;
            Rule(r,y,a);y+=10;U.Text(Inner(r,y,16),"CHOICES",11,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);y+=20;
            foreach(var m in P.Roster(v))
            {
                if(y+20>r.yMax-10)break;var line=Inner(r,y,20);U.Text(line,c.Name(m.account)+(m.you?"  · YOU":""),13,m.you?C.CyanSoft(a):C.Ink(a),TextAnchor.MiddleLeft,bold:true);
                StateTag(line.xMax,line.y,m.status,m.tone,a);y+=26;
            }
            return result;
        }finally{U.EndBand();}}
        /// <summary>The world boss fight, and the ended run.</summary>
        public static void Phase(Rect r,ExpeditionView v,float a=1)
        {U.BeginBand(r);try{
            bool final=v.phase==ExpeditionPhase.FinalBoss;H.Panel(r,a,final?C.Magenta():C.Muted());
            float y=Header(r,final?"FINAL AREA · LOOP "+(v.loop+1):"RUN ENDED",final?"World boss":"Everyone has extracted",final?C.Magenta():C.Ink(),a);
            Lines(r.x+18,y,r.width-36,final?"Every building is cleared. World-boss rewards are reserved for pilots alive and connected when it falls.":"Extractions are final once saved; extracted pilots cannot rejoin this run.",12,C.Muted(a),3);
        }finally{U.EndBand();}}

        // ---- Dice reveal -----------------------------------------------------------------------------------------------------
        /// <summary>
        /// The boss loot split and its stored dice: the even share, then each survivor's die spinning (no number) until it locks
        /// on the roll the host stored when the boss fell, then the extra chips light on the stored winners. Nothing is rolled here.
        /// </summary>
        public static void Dice(Rect r,ExpeditionView v,ExpeditionDice d,float t,Controls c,bool reduced,float a=1)
        {U.BeginBand(r);try{
            var share=P.Share(v,d);H.Panel(r,a,C.Magenta(),14,H.Corners.Diagonal,true,.95f);
            float y=Header(r,"BOSS LOOT · "+share.name,P.ShareLine(share),C.Ink(),a,18);
            y=Lines(r.x+18,y,r.width-36,share.extra==0?"Even split: no dice needed.":"Extra chips go to the highest stored rolls, made by the host when the boss fell"+(share.tieBreak?" (a tie at the cut was settled by the host's seeded tie-break).":"."),11,C.Muted(a),2)+6;
            for(int i=0;i<d.eligible.Length;i++)
            {
                if(y+44>r.yMax-46)break;string account=d.eligible[i];bool you=account==v.viewer;var line=Inner(r,y,40);
                U.Text(new Rect(line.x,line.y,line.width*.5f,line.height),c.Name(account)+(you?"  · YOU":""),14,you?C.CyanSoft(a):C.Ink(a),TextAnchor.MiddleLeft,bold:true);
                int chips=d.each;bool winner=P.Dice.Winner(d,i,t);
                if(share.extra>0)
                {
                    // A spinning die pulses and scans (no number, no rotated transform under the clip band); a locked one shows the stored roll.
                    var die=new Rect(line.x+line.width*.5f,line.y+3,34,34);int face=P.Dice.Face(d,i,t);float spin=reduced||face>=0?0:P.Dice.Tumble(i,t)*Mathf.Deg2Rad;
                    U.Push(die.center,Vector2.zero,1+.08f*Mathf.Sin(spin*2));H.Cut(die,C.Indigo(.95f*a),5);H.Outline(die,winner?C.Magenta(a):face<0?C.Edge(a):C.CyanSoft(.8f*a),5,H.Corners.Diagonal,winner?2:1);
                    if(face<0&&!reduced)U.Fill(new Rect(die.x+3,die.y+3+(die.height-8)*(.5f+.5f*Mathf.Sin(spin)),die.width-6,2),C.CyanSoft(.5f*a));U.Pop();
                    U.Text(die,face<0?"··":face.ToString(),face<0?14:15,face<0?C.Muted(a):C.Ink(a),TextAnchor.MiddleCenter,bold:true,mono:true);
                    if(winner){U.Glow(die,C.Magenta(.35f*a),10);}
                }
                int total=chips+(winner?1:0);string count=total+(total==1?" CHIP":" CHIPS")+(winner?" · +1 BY DICE":"");
                U.Text(new Rect(line.x,line.y,line.width,line.height),P.Dice.WinnersShown(d,t)||share.extra==0?count:"",11,winner?C.Magenta(a):C.Ink(a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.08f);
                y+=44;
            }
            string mine=share.eligible?"Reserved for you: "+share.yours+" · opened "+share.yoursClaimed+" · picked "+share.yoursSelected:"You were not alive and connected when it fell: nothing is reserved for you.";
            Lines(r.x+18,r.yMax-36,r.width-36,mine,12,share.eligible?C.Magenta(a):C.Danger(a),2,true);
        }finally{U.EndBand();}}

        // ---- Host transfer ---------------------------------------------------------------------------------------------------
        /// <summary>The host-transfer overlay: headline, each step's state (authority and transport), and the transport's candidates.</summary>
        public static void Transfer(Rect r,ExpeditionView v,Controls c,float a=1)
        {U.BeginBand(r);try{
            if(v.transfer.stage==ExpeditionTransferStage.None)return;var steps=P.Transfer(v,out string headline,out var tone);
            H.Panel(r,a,Tone(tone),R.Cut,H.Corners.Diagonal,true,.95f);float y=Header(r,"HOST TRANSFER · EPOCH "+v.epoch,headline,Tone(tone),a,18);
            foreach(var s in steps)
            {
                if(y+20>r.yMax-8)break;var line=Inner(r,y,18);
                string tag=s.state==ExpeditionStep.Done?"DONE":s.state==ExpeditionStep.Active?"NOW":s.state==ExpeditionStep.Failed?"FAILED":s.state==ExpeditionStep.Unavailable?"N/A":"WAIT";
                var t=s.state==ExpeditionStep.Done?ExpeditionTone.Good:s.state==ExpeditionStep.Active?ExpeditionTone.Cyan:s.state==ExpeditionStep.Failed?ExpeditionTone.Danger:s.state==ExpeditionStep.Unavailable?ExpeditionTone.Warn:ExpeditionTone.Muted;
                StateTag(line.x+64,line.y,tag,t,a,18);float lw=Mathf.Min(260,(line.width-72)*.42f);
                U.Text(new Rect(line.x+72,line.y,lw,18),s.label,12,C.Ink(a),TextAnchor.MiddleLeft,bold:true);
                float dy=line.y;foreach(var d in U.Wrap(s.detail,11,line.width-80-lw,false,s.state==ExpeditionStep.Failed?2:1)){U.Text(new Rect(line.x+80+lw,dy,line.width-80-lw,18),d,11,s.state==ExpeditionStep.Failed?C.Danger(a):C.Muted(a),TextAnchor.MiddleLeft);dy+=16;}
                y=Mathf.Max(y+26,dy+8);
            }
            if(v.transfer.candidates.Length>0&&y+20<r.yMax)
            {
                U.Text(Inner(r,y+2,14),"CONNECTIONS (TRANSPORT REPORT)",10,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);y+=20;
                foreach(var cand in v.transfer.candidates)
                {
                    if(y+16>r.yMax-6)break;bool elected=cand.account==v.transfer.nextHost;var line=Inner(r,y,16);
                    U.Text(line,c.Name(cand.account)+(elected?"  · ELECTED":""),11,elected?C.Cyan(a):C.Ink(a),TextAnchor.MiddleLeft,bold:elected);
                    U.Text(line,(cand.reachable&&cand.capable?"":"UNREACHABLE  ")+"LOSS "+(cand.loss*100).ToString("0.#")+"%  ·  "+(cand.latencyMs+cand.jitterMs).ToString("0")+" MS",10,C.Muted(a),TextAnchor.MiddleRight,mono:true);y+=18;
                }
            }
        }finally{U.EndBand();}}
    }
}
