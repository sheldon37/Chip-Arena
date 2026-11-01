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
    ///    drops against reserved boss chips, eligibility and claimed state), personal chip panel (waiting rewards, held chips; picks
    ///    happen on the Boss Test's chip screen, NativeExpeditionChipScreen.cs), sanctuary (checkpoint,
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
            /// <summary>The intent last sent under this command's key (ExpeditionIntentTracker.Last), so a vote withdrawn since reads so.</summary>
            public Func<ExpeditionCommand,ExpeditionIntent> Last=_=>ExpeditionIntent.None;
            /// <summary>The view the controls are drawn for (set by the caller each frame).</summary>
            public ExpeditionView View;
            public Func<string,string> Name=a=>string.IsNullOrEmpty(a)?"?":a.ToUpperInvariant();
            public Func<string,string> ChipName=id=>id;
            /// <summary>A chip's rarity colour (content data), drawn as a tick, never as text.</summary>
            public Func<string,Color> ChipTone=_=>C.CyanSoft();
            /// <summary>Opens the chip pick screen (the chip panel's CHOOSE CHIP line); null where there is none (the DEV preview).</summary>
            public Action Choose;
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
            // A confirmed vote the authority has since withdrawn (a replacing vote, a downed pilot) no longer reads as confirmed.
            if(state.state==ExpeditionCommandState.Confirmed&&c.View!=null&&ExpeditionIntentTracker.IsVote(command.intent))
            {var sent=c.Last(command);if(sent!=ExpeditionIntent.None&&!P.VoteHeld(sent,c.View)){caption=P.Withdrawn(sent,c.View);tone=ExpeditionTone.Muted;}}
            if(caption==null&&possible&&blocked!=null&&!quiet){caption="Unavailable · "+blocked;tone=ExpeditionTone.Warn;}
            return Lines(r.x,r.yMax+4,Mathf.Max(r.width,260),caption,11,Tone(tone,a),3);
        }
        private static Rect Inner(Rect r,float y,float h)=>new Rect(r.x+18,y,r.width-36,h);
        private static void Rule(Rect r,float y,float a)=>U.Fill(new Rect(r.x+18,y,r.width-36,H.Snap(1)),C.Edge(.8f*a));

        // ---- Minimal in-match HUD ------------------------------------------------------------------------------------------
        /// <summary>The minimal HUD: wallet top left (as the PvE money panel), the objective tag top centre with the running
        /// countdown under it, and a magenta tag top right while chips of yours wait to be opened or picked (`chips`; in the scene the
        /// Boss Test's CHIPS tag with its C key stands in for it).</summary>
        /// <summary>The in-scene HUD sets this around its Minimal call while the quota module (or the ending's strip) holds the top
        /// centre (NativeExpeditionQuotaHud.cs, NativeExpeditionEndingUI.cs): the objective tag steps aside for it. The J screen and the
        /// DEV preview never set it.</summary>
        public static bool SceneQuota;
        public static void Minimal(ExpeditionView v,float W,float a=1,bool chips=true)
        {
            if(P.Has(v.mine.account)&&!v.mine.extracted)
            {
                var mr=new Rect(24,24,220,58);H.Panel(mr,a,C.Warn());
                U.Text(new Rect(mr.x+14,mr.y+8,mr.width-28,26),P.Money(v.mine.wallet),22,C.Warn(a),TextAnchor.MiddleLeft,bold:true,mono:true,glow:C.Warn(.3f*a));
                U.Text(new Rect(mr.x+14,mr.y+34,mr.width-28,16),v.mine.catchup>0?"YOUR WALLET · "+v.mine.catchup+" CATCH-UP":"YOUR WALLET",11,C.Muted(a),TextAnchor.MiddleLeft,mono:true,tracking:.08f);
            }
            if(!SceneQuota)
            {
                string objective=P.Objective(v,out var tone);float ow=U.Measure(objective,12,true,true,.14f).x+44;var or=new Rect(W/2-ow/2,20,ow,30);
                H.Tag(or,Tone(tone),a);U.Text(new Rect(or.x+16,or.y,ow-20,30),objective,12,Tone(tone,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            }
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
            if(chips&&waiting>0&&!v.mine.extracted)
            {
                string tag="YOUR CHIPS · "+waiting+" WAITING";float cw=U.Measure(tag,12,true,true,.1f).x+30;var cr=new Rect(W-24-cw,24,cw,30);
                H.Tag(cr,C.Magenta(),a);U.Text(new Rect(cr.x+14,cr.y,cw,cr.height),tag,12,C.Magenta(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
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
        /// <summary>Your wallet, your waiting rewards (purchased, reserved, picked up, catch-up: ready with offers, or unopened) and the
        /// CHOOSE CHIP line that opens the Boss Test's chip screen on them (owner 2026-10-15: picks happen there, not on the J screen),
        /// your picks this loop and the chips you hold. Only the viewer's state exists in the view, so nothing here can show another
        /// pilot's choice.</summary>
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
            // Waiting rewards, one row each (ready with its offers, or unopened). The Boss Test's chip screen opens and picks them.
            int waiting=0,listed=0;
            foreach(var reward in me.rewards)
            {
                if(reward.consumed)continue;waiting++;bool boss=P.Has(reward.boss);if(y+(boss?36:22)>bottom-62)continue;var head=Inner(r,y,18);listed++;
                float tag=StateTag(head.xMax,head.y-1,reward.claimed?"READY · "+reward.offers.Length+" OFFERS":"UNOPENED",reward.claimed?ExpeditionTone.Magenta:ExpeditionTone.Muted,a);
                U.Text(new Rect(head.x,head.y,tag-head.x-6,head.height),SourceLabel(reward.source),11,Tone(SourceTone(reward.source),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);y+=22;
                if(boss){U.Text(Inner(r,y-4,14),P.Name(reward.boss),10,C.Muted(a),TextAnchor.MiddleLeft,mono:true);y+=14;}
            }
            if(listed<waiting){U.Text(Inner(r,y,14),"+"+(waiting-listed)+" more",10,C.Muted(a),TextAnchor.MiddleLeft,mono:true);y+=18;}
            if(waiting>0&&!me.extracted)
            {
                bool can=c.Choose!=null&&blocked==null;
                if(U.Btn(new Rect(r.x+18,y+2,r.width-36,34),"C · CHOOSE CHIP ("+waiting+")",can,can,a,13)&&can)c.Choose();y+=42;
                y=Lines(r.x+18,y,r.width-36,c.Choose==null?"Picks open on the chip tray in a live run.":"Opens the chip tray over the live view. C opens it anywhere in the run.",10,C.Muted(a),2)+8;
            }
            // Your picks this loop (private): the chip chosen from each reward.
            var picks=P.History(v);
            if(picks.Length>0&&y+40<bottom)
            {
                Rule(r,y,a);y+=8;U.Text(Inner(r,y,14),"PICKED THIS LOOP · YOURS ONLY",10,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);y+=18;
                for(int i=picks.Length-1;i>=0&&y+18<=bottom;i--)
                {
                    var row=Inner(r,y,18);U.Fill(new Rect(row.x,row.y+4,3,row.height-8),U.A(c.ChipTone(picks[i].chip),a));
                    U.Text(new Rect(row.x+10,row.y,row.width-10,row.height),c.ChipName(picks[i].chip),12,C.Ink(a),TextAnchor.MiddleLeft,bold:true);
                    U.Text(row,picks[i].from,10,C.Muted(a),TextAnchor.MiddleRight,mono:true,tracking:.08f);y+=20;
                }
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
        /// <summary>The sanctuary: checkpoint state (a fresh run has none), individual extraction, the group extraction vote and the reprint machine.</summary>
        public static ExpeditionCommand Sanctuary(Rect r,ExpeditionView v,Controls c,float a=1)
        {U.BeginBand(r);try{
            var result=default(ExpeditionCommand);H.Panel(r,a,C.Good());float y=Header(r,v.freshRun?"SAFE LOCATION · EXTRACTION AND REPRINT":"SAFE LOCATION · CHECKPOINT AND EXTRACTION","Sanctuary",C.Ink(),a);
            U.Text(Inner(r,y,16),v.freshRun?"FRESH RUN · NO SAVING OR RESUMING":v.savedRevision>=0?"CHECKPOINT SAVED · REVISION "+v.savedRevision:"NO CHECKPOINT SAVED YET",10,v.savedRevision>=0?C.Good(a):C.Muted(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);y+=24;
            y=ExtractionBlock(r,y,v,c,a,ref result);Rule(r,y,a);y+=10;
            y=ReprintBlock(r,y,v,c,a,ref result);
            return result;
        }finally{U.EndBand();}}
        private static float ExtractionBlock(Rect r,float y,ExpeditionView v,Controls c,float a,ref ExpeditionCommand result)
        {
            var m=P.Extraction(v);float half=(r.width-36-16)/2,x0=r.x+18,x1=x0+half+16;
            // Individual extraction (left).
            U.Text(new Rect(x0,y,half,18),m.title,12,Tone(m.extracted?m.tone:ExpeditionTone.Warn,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            float ly=Lines(x0,y+22,half,m.detail,12,m.hostNeedsTransfer?C.Warn(a):C.Ink(a),m.hostNeedsTransfer?5:3)+6;
            if(!m.extracted){ly=Action(new Rect(x0,ly,Mathf.Min(m.hostNeedsTransfer?230:170,half),32),m.button,new ExpeditionCommand(ExpeditionIntent.Extract),m.can,c,a,!m.hostNeedsTransfer,out bool go);if(go)result=new ExpeditionCommand(ExpeditionIntent.Extract);}
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
                U.Text(new Rect(row.x,row.y,row.width-200,row.height),who,12,C.Ink(a),TextAnchor.MiddleLeft,bold:true);
                var cmd=new ExpeditionCommand(ExpeditionIntent.Reprint,m.account);bool quoted=P.HasQuote(m),able=v.mine.alive&&v.mine.safe&&m.connected;
                float after=Action(new Rect(row.xMax-190,row.y,190,30),"Reprint · "+P.Quote(m),cmd,able&&quoted,c,a,false,out bool go);if(go)result=cmd;
                if(able&&!quoted&&!c.State(cmd).Shown)after=Math.Max(after,Lines(row.x,row.y+row.height-4,row.width-200,"Unavailable · "+P.NoQuote,11,C.Warn(a),2));
                y=Mathf.Max(row.yMax+6,after+4);
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
            y=NativeQuotaMarksHud.Building(r,y,v,a);
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
            y=Lines(r.x+18,y,r.width-36,"Extract now with your build, or stay for loop "+(v.loop+2)+": the board resets, your build and the clock carry on, and enemies get harder. Each pilot chooses alone.",13,C.Ink(a),3)+6;
            // The world boss's loot, from its stored dice record (the reveal and your chips panel hold the detail).
            foreach(var d in v.dice)
            {
                if(P.Name(d.boss)!=P.WorldBoss)continue;var share=P.Share(v,d);var line=Inner(r,y,18);Marker(new Rect(line.x,line.y+1,16,16),P.LootKind.Reserved,share.eligible?P.LootState.ReservedForYou:P.LootState.NotEligible,a);
                U.Text(new Rect(line.x+24,line.y,line.width-24,18),"WORLD BOSS LOOT · "+P.ShareLine(share).ToUpperInvariant()+(share.eligible?" · YOURS "+share.yours:" · NOT ELIGIBLE"),11,share.eligible?C.Magenta(a):C.Danger(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.06f);y+=24;
            }
            y=ExtractionBlock(r,y+4,v,c,a,ref result);Rule(r,y,a);y+=10;
            // Each connected pilot chooses; the authority advances only after all remaining pilots consent (downed ones can) and one is alive.
            var k=P.Continue(v);var head=Inner(r,y,18);U.Text(head,k.label,12,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            U.Text(head,k.votes+" / "+k.remaining+" READY",13,Tone(k.allAgreed?k.tone:ExpeditionTone.Muted,a),TextAnchor.MiddleRight,bold:true,mono:true);y+=22;
            H.Line(Inner(r,y,3),k.remaining>0?Mathf.Clamp01(k.votes/(float)k.remaining):0,Tone(k.allAgreed?k.tone:ExpeditionTone.Cyan,a),C.Void(.7f*a));y+=10;
            var next=new ExpeditionCommand(k.voted?ExpeditionIntent.CancelContinue:ExpeditionIntent.VoteContinue);
            y=Action(new Rect(r.x+18,y,220,32),k.button,next,k.canVote,c,a,!k.voted,out bool go)+6;if(go)result=next;
            y=Lines(r.x+18,y,r.width-36,k.detail,12,k.needsLiving?C.Warn(a):C.Ink(a),3)+2;
            y=Lines(r.x+18,y,r.width-36,"Continue and the group-extraction vote replace each other. Extracting alone stays your own choice.",11,C.Muted(a),2)+6;
            Rule(r,y,a);y+=10;U.Text(Inner(r,y,16),"CHOICES",11,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);y+=20;
            foreach(var m in P.ContinueRows(v))
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

        // ---- Host candidates -------------------------------------------------------------------------------------------------
        /// <summary>The transport's measured links (RTT, jitter, ping timeouts) and what is not probed or supported yet.</summary>
        public static float Candidates(Rect r,float y,ExpeditionView v,Controls c,float a)
        {
            U.Text(Inner(r,y+2,14),"CONNECTIONS · MEASURED BY THE TRANSPORT",10,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);y+=20;
            foreach(var cand in P.Candidates(v))
            {
                if(y+30>r.yMax-6)break;var line=Inner(r,y,16);
                U.Text(line,c.Name(cand.account)+(cand.elected?"  · ELECTED":""),12,cand.elected?C.Cyan(a):C.Ink(a),TextAnchor.MiddleLeft,bold:true);
                U.Text(line,cand.rtt+(cand.jitter.Length>0?"  ·  "+cand.jitter:"")+"  ·  "+cand.timeouts,10,C.Ink(a),TextAnchor.MiddleRight,mono:true);
                U.Text(new Rect(line.x,line.y+15,line.width,14),cand.probe+"  ·  "+cand.support,10,Tone(cand.tone,a),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.08f);y+=34;
            }
            return y;
        }
        /// <summary>A compact host-transfer line for the in-match HUD (the full overlay is on the Expedition screen).</summary>
        public static void TransferBanner(Rect r,ExpeditionView v,float a=1)
        {
            if(v.transfer.stage==ExpeditionTransferStage.None)return;var steps=P.Transfer(v,out string headline,out var tone);
            H.Panel(r,a,Tone(tone),R.TagCut);U.Text(new Rect(r.x+14,r.y+5,r.width-28,18),headline,12,Tone(tone,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.1f);
            string now="";foreach(var s in steps)if(s.state==ExpeditionStep.Failed||s.state==ExpeditionStep.Active||s.state==ExpeditionStep.Unavailable){now=s.label+" · "+s.detail;break;}
            U.Text(new Rect(r.x+14,r.y+24,r.width-28,16),now,10,C.Muted(a),TextAnchor.MiddleLeft,mono:true);
        }

        // ---- Chip caches -------------------------------------------------------------------------------------------------------
        /// <summary>The scene's chip caches before purchase: price, stock generation, cooldown against the clock, and why buying
        /// is not a control here (the scene's interaction at the cache).</summary>
        public static void Caches(Rect r,ExpeditionView v,Controls c,float a=1)
        {U.BeginBand(r);try{
            H.Panel(r,a,C.Warn());float y=Header(r,"CHIP CACHES · THE BUYER OWNS THE CHIP","Caches",C.Ink(),a);
            var rows=P.Caches(v);if(rows.Length==0){U.Text(Inner(r,y,18),"No chip caches in this world yet.",12,C.Muted(a),TextAnchor.MiddleLeft);return;}
            foreach(var row in rows)
            {
                if(y+40>r.yMax-38)break;var line=Inner(r,y,36);CacheGlyph(new Rect(line.x,line.y+6,22,22),Tone(row.tone),row.restocking?.5f*a:a);
                U.Text(new Rect(line.x+32,line.y,line.width-32,18),row.stock,11,C.CyanSoft(a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
                StateTag(line.xMax,line.y,row.state,row.tone,a);
                U.Text(new Rect(line.x+32,line.y+18,line.width-32,16),row.detail,11,C.Muted(a),TextAnchor.MiddleLeft);y+=40;
            }
            Lines(r.x+18,r.yMax-36,r.width-36,P.NoPurchase,10,C.Muted(a),2);
        }finally{U.EndBand();}}

        // ---- World glyphs ------------------------------------------------------------------------------------------------------
        /// <summary>The reprint machine's glyph: a pedestal frame with the reprint cross.</summary>
        public static void MachineGlyph(Rect r,Color tone,float a)
        {
            H.Cut(r,C.Indigo(.85f*a),r.width*.18f);H.Outline(r,U.A(tone,a),r.width*.18f,H.Corners.Diagonal,1.5f);
            float w=r.width*.14f,l=r.width*.52f;U.Fill(new Rect(r.center.x-w/2,r.center.y-l/2,w,l),U.A(tone,a));U.Fill(new Rect(r.center.x-l/2,r.center.y-w/2,l,w),U.A(tone,a));
        }
        /// <summary>A chip cache's glyph: a crate with its lid line and a chip tick.</summary>
        public static void CacheGlyph(Rect r,Color tone,float a)
        {
            var body=new Rect(r.x,r.y+r.height*.28f,r.width,r.height*.72f);H.Cut(body,C.Indigo(.85f*a),r.width*.12f);H.Outline(body,U.A(tone,a),r.width*.12f,H.Corners.Diagonal,1.5f);
            U.Fill(new Rect(r.x-r.width*.04f,r.y+r.height*.16f,r.width*1.08f,r.height*.12f),U.A(tone,a));U.Fill(new Rect(r.center.x-r.width*.1f,body.y+body.height*.3f,r.width*.2f,body.height*.4f),U.A(C.Ink(),a));
        }
        // Label declutter: the nearest pieces claim their label areas first; a farther label that would overlap is not drawn.
        private static readonly Rect[] claimed=new Rect[128];private static int claimedCount;
        public static void LabelsReset(){claimedCount=0;}
        /// <summary>Reserves a piece's label area (call nearest first); false when the piece has no label at this size or a nearer
        /// label already covers it. The same layout as Piece.</summary>
        public static bool LabelClaim(in ExpeditionWorldPiece p,Vector2 label,float k,float minText=0)
        {
            float g=Mathf.Clamp(k*.5f,10,72),size=Mathf.Max(minText,Mathf.Clamp(k*.16f,0,14));if(p.quiet||size<8||string.IsNullOrEmpty(p.label))return false;
            bool detail=!string.IsNullOrEmpty(p.detail),extra=!string.IsNullOrEmpty(p.extra);float block=size*1.4f+(detail?size*1.25f:0)+(extra?size*1.3f:0);
            int chars=Math.Max(p.label.Length,Math.Max(detail?p.detail.Length:0,extra?p.extra.Length:0));float w=chars*size*.62f+12;
            // A margin, so a farther piece's lines never read as part of a nearer label.
            float y=p.kind==ExpeditionPieceKind.Pickup?label.y+g/2+4:label.y-g/2-6-block;var r=new Rect(label.x-w/2-10,y-12,w+20,block+24);
            for(int i=0;i<claimedCount;i++){var c=claimed[i];if(r.x<c.xMax&&c.x<r.xMax&&r.y<c.yMax&&c.y<r.yMax)return false;}
            if(claimedCount<claimed.Length)claimed[claimedCount++]=r;return true;
        }
        /// <summary>
        /// A world piece's hologram label at its projected points (Core NativeExpeditionWorld): the glyph (the loot Marker, the Door, the
        /// machine or cache glyph; skipped where the 3D piece draws its own shape) sized by `k` canvas units per metre, and its three
        /// lines under the label point while labelled (a cluster's lead labels at its anchor; quiet pieces have none).
        /// </summary>
        public static void Piece(in ExpeditionWorldPiece p,Vector2 glyph,Vector2 label,float k,bool labelled,bool drawGlyph,float a,float minText=0)
        {
            float alpha=a*(p.settled?.55f:1),g=Mathf.Clamp(k*.5f,10,72);var r=new Rect(glyph.x-g/2,glyph.y-g/2,g,g);
            if(drawGlyph)switch(p.kind)
            {
                case ExpeditionPieceKind.Pickup:Marker(r,p.look==ExpeditionPieceLook.Ordinary?P.LootKind.Ordinary:P.LootKind.Reserved,p.state,alpha);break;
                case ExpeditionPieceKind.Door:Door(new Rect(glyph.x-g*.75f,glyph.y-g*.5f,g*1.5f,g),p.look==ExpeditionPieceLook.DoorSealed,p.tone,alpha);break;
                case ExpeditionPieceKind.Machine:MachineGlyph(r,Tone(p.tone),alpha);break;
                default:CacheGlyph(r,Tone(p.tone),alpha);break;
            }
            float size=Mathf.Max(minText,Mathf.Clamp(k*.16f,0,14));if(!labelled||p.quiet||size<8)return;
            // Under a floating pickup; over the pieces that stand on the floor (door, machine, cache), clear of their shapes.
            bool detail=!string.IsNullOrEmpty(p.detail),extra=!string.IsNullOrEmpty(p.extra);float block=size*1.4f+(detail?size*1.25f:0)+(extra?size*1.3f:0);
            float y=p.kind==ExpeditionPieceKind.Pickup?label.y+g/2+4:label.y-g/2-6-block,w=Mathf.Max(220,g*4);var shade=C.Void(.85f*a);float la=p.labelAtAnchor?a:alpha;
            U.Text(new Rect(label.x-w/2,y,w,size*1.5f),p.label,size,Tone(p.tone,la),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f,shadow:shade);y+=size*1.4f;
            if(detail){U.Text(new Rect(label.x-w/2,y,w,size*1.4f),p.detail,size*.82f,C.Ink(la),TextAnchor.MiddleCenter,mono:true,shadow:shade);y+=size*1.25f;}
            if(extra)U.Text(new Rect(label.x-w/2,y,w,size*1.3f),p.extra,size*.76f,C.Muted(la),TextAnchor.MiddleCenter,mono:true,shadow:shade);
        }

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
            if(v.transfer.candidates.Length>0&&y+20<r.yMax)y=Candidates(r,y,v,c,a);
        }finally{U.EndBand();}}
    }
}
