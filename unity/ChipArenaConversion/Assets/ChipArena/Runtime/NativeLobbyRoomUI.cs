using System;
using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
namespace ChipArena.UnityConversion
{
    // The PvP room (owner 2026-11-10: "Improve UI for create room screen and pvp lobby. Lets give players the options to change their weapon
    // abilities in lobby as well ... much better quality and a very clean / beautiful design"): the roster of pilot cards (the seat's portrait,
    // name, weapon, both abilities and whether it is ready), the pilot's own loadout (the weapon and the Q and E slots, each opening its picker
    // in a sheet over the room), and the room code chip. A change of weapon or ability reaches the room as it is made: the host's own seat in its
    // next broadcast, a guest's through the existing profile message (or, while ready, the ready message, which keeps it ready), a moment
    // after the last change (LbSync). An ability pick is also saved to the pilot, as the Locker's Save does (SavePilot).
    public sealed partial class ConversionGame
    {
        // ---- Layout ----------------------------------------------------------------------------------------------------
        private const float LbLoadoutH=236,LbCodeW=300,LbCardGap=14;
        private Rect lbPanel;
        // A section's heading: the mono label in soft cyan, a muted note after it, then the hairline with its short cyan lead.
        private static void LbHeading(Rect r,string label,string note,float e)
        {
            float lw=U.Measure(label,11,true,true,.18f).x;U.Text(new Rect(r.x,r.y,lw+6,16),label,11,C.CyanSoft(.95f*e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.18f);
            if(!string.IsNullOrEmpty(note))U.Text(new Rect(r.x+lw+14,r.y,Mathf.Max(10,r.width-lw-14),16),note,11,C.Muted(.9f*e),TextAnchor.MiddleLeft,mono:true,tracking:.08f);
            U.Fill(new Rect(r.x,r.y+24,r.width,1),C.Edge(.6f*e));U.Fill(new Rect(r.x,r.y+24,28,1),C.Cyan(.9f*e));
        }

        // ---- The room code chip in the header ----------------------------------------------------------------------------
        private void LbCodeChip(Rect r,float e)
        {
            string code=ShownRoomCode();bool has=!string.IsNullOrEmpty(code);
            H.Cut(r,C.Navy(.92f*e),9);H.Outline(r,C.Edge(.9f*e),9);U.Fill(new Rect(r.x+9,r.y,r.width-18,1),C.Cyan(.5f*e));
            U.Text(new Rect(r.x+16,r.y+8,r.width-110,14),Fit(RoomCodeLabel(),9.5f,r.width-110,false,.14f),9.5f,C.Muted(e),TextAnchor.MiddleLeft,mono:true,tracking:.14f);
            U.Text(new Rect(r.x+16,r.y+22,r.width-110,26),has?code:"—",22,C.CyanSoft(e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f,glow:C.Cyan(.25f*e));
            var copy=new Rect(r.xMax-12-38,r.y+7,38,38);if(LbIconButton(copy,"copy",62001,has,e)){GUIUtility.systemCopyBuffer=code;Notice("Room code copied.");}
            var link=new Rect(copy.x-8-38,copy.y,38,38);if(inviteLink!=null&&LbIconButton(link,"link",62002,true,e)){GUIUtility.systemCopyBuffer=inviteLink;Notice("Invite link copied.");}
        }
        // A square icon button: navy glass, its edge and icon lighting cyan on hover.
        private bool LbIconButton(Rect r,string icon,int id,bool enabled,float e)
        {
            float h=U.HoverAmount(id,r,enabled&&!MenuBlocked());
            if(Event.current.type==EventType.Repaint)
            {
                float a=e*(enabled?1:.45f);U.Round(r,U.A(U.Mix(C.Indigo(),C.Panel(),.4f+.6f*h),a),7,U.A(U.Mix(C.Edge(),C.Cyan(),h),a));if(h>0)U.Glow(r,C.Cyan(.16f*h*a),10);
                NativeIcons.Draw(new Rect(r.center.x-9,r.center.y-9,18,18),icon,U.A(U.Mix(C.Ink(),C.CyanSoft(),h),a));
            }
            return U.Button(r,enabled);
        }

        // ---- The roster ------------------------------------------------------------------------------------------------
        private void LbRoster(Rect r,List<LobbyPilot> roster,float e,bool teams)
        {
            int count=roster.Count,max=8,ready=0,bots=0;foreach(var p in roster){if(p.ready&&p.connected)ready++;if(p.bot)bots++;}
            LbHeading(new Rect(r.x,r.y,r.width-170,28),"PILOTS",count+" / "+max+(teams?" · TEAMS":" · FREE-FOR-ALL"),e);
            LbReadyMeter(new Rect(r.xMax-160,r.y-6,160,30),ready,count,e);
            var grid=new Rect(r.x,r.y+42,r.width,r.height-42);
            int cols=grid.width<560?2:grid.width<800?3:4;int slots=count<cols?cols:Mathf.Min(max,Mathf.CeilToInt(Mathf.Min(max,count+1)/(float)cols)*cols);if(slots<count)slots=count;
            int rows=Mathf.Max(1,Mathf.CeilToInt(slots/(float)cols));float g=LbCardGap,cw=(grid.width-g*(cols-1))/cols,ch=Mathf.Min((grid.height-g*(rows-1))/rows,cw*1.62f);
            bool host=Hosting&&server==null;int lastBot=-1;foreach(var p in roster)if(p.bot)lastBot=Mathf.Max(lastBot,p.id);
            int seats=0;foreach(var p in roster)if(!p.bot)seats++;bool canAdd=host&&count<max&&!prefs.pve;float t=Time.unscaledTime;
            for(int i=0;i<slots;i++)
            {
                var cr=new Rect(grid.x+(i%cols)*(cw+g),grid.y+(i/cols)*(ch+g),cw,ch);float k=Mathf.Clamp01((LbAge-.05f*i)/.3f);k=k*k*(3-2*k);cr.y+=(1-k)*10;float a=e*k;
                if(i<count)LbPilotCard(cr,roster[i],i,a,teams,t,host&&roster[i].bot&&roster[i].id==lastBot);
                else LbOpenSlot(cr,i,a,canAdd&&i==count,host);
            }
        }
        private float LbAge=>prefs.reducedEffects?9:Time.unscaledTime-lbEnteredAt;
        // The READY meter: how many are ready of how many there are, green when everyone is.
        private static void LbReadyMeter(Rect r,int ready,int total,float e)
        {
            bool all=total>0&&ready>=total;string n=ready+"/"+total;float nw=U.Measure(n,17,true,true).x,lw=U.Measure("READY",11,true,true,.16f).x,w=14+nw+8+lw+14;var pill=new Rect(r.xMax-w,r.y,w,r.height);
            if(all)U.Glow(pill,C.Good(.22f*e),16);H.Cut(pill,all?U.A(U.Mix(C.Navy(),C.Good(),.18f),e):C.Navy(.9f*e),7);H.Outline(pill,all?C.Good(.9f*e):C.Edge(.9f*e),7);
            U.Text(new Rect(pill.x+14,pill.y,nw+4,pill.height),n,17,C.Ink(e),TextAnchor.MiddleLeft,bold:true,mono:true);
            U.Text(new Rect(pill.x+14+nw+8,pill.y+1,lw+4,pill.height),"READY",11,all?C.Good(e):C.CyanSoft(e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
        }
        // A pilot's card: the accent (its team's colour in a teams room) along the top, the seat and what it is, the portrait, the name, the weapon,
        // both abilities, and the ready band at the foot. The host may promote a guest, and take away the room's last bot.
        private void LbPilotCard(Rect r,LobbyPilot p,int index,float e,bool teams,float t,bool removable)
        {
            var weapon=LobbyWeapon(p.weapon);Color style=weapon!=null?U.Hex((int)weapon.color):C.Muted();bool ready=p.ready&&p.connected;float a=e*(p.connected?1:.55f);
            int team=TeamIndex(p.team);Color band=teams?TeamColor(team):p.accent;bool paint=Event.current.type==EventType.Repaint;
            if(paint)
            {
                U.Glow(new Rect(r.x,r.y+14,r.width,r.height),new Color(0,0,0,.35f*a),36);if(ready)U.Glow(r,C.Good(.14f*a),22);
                U.RoundGradient(r,U.A(U.Mix(C.Indigo(),style,.06f),.96f*a),C.Navy(.97f*a),10,ready?C.Good(.85f*a):p.you?C.Cyan(.75f*a):C.Edge(a),1,false,ready||p.you?1.5f:1);
                U.RadialStops(new Rect(r.x-r.width*.2f,r.y+r.height*.05f,r.width*1.4f,r.height*.8f),U.A(p.accent,.14f*a),new[]{0f,.62f,1f},new[]{1f,0f,0f});
                U.Fill(new Rect(r.x+10,r.y,r.width-20,2),U.A(band,.9f*a));U.Glow(new Rect(r.x+10,r.y,r.width-20,2),U.A(band,.35f*a),6);
                // The seat and what it is: YOU in cyan, the room's host with the crown, a bot in violet.
                U.Text(new Rect(r.x+12,r.y+10,30,18),(index+1).ToString("00"),12,U.A(band,a),TextAnchor.MiddleLeft,mono:true,bold:true);
                string tag=p.you?"YOU":p.bot?"BOT":p.id==1?"HOST":"PILOT";Color tone=p.you?C.Cyan():p.bot?C.Violet():p.id==1?C.Warn():C.Muted();
                float tw=U.Measure(tag,9.5f,true,true,.14f).x+(p.id==1?30:16);var chip=new Rect(r.x+40,r.y+10,tw,18);H.Cut(chip,U.A(U.Mix(C.Navy(),tone,.18f),a),4);H.Outline(chip,U.A(tone,.7f*a),4);
                float cx=chip.x+8;if(p.id==1){NativeIcons.Draw(new Rect(cx,chip.y+3,12,12),"crown",U.A(C.Warn(),a));cx+=14;}U.Text(new Rect(cx,chip.y,tw,18),tag,9.5f,U.A(U.Mix(tone,Color.white,.3f),a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
            }
            // The team chip (a teams room): the viewer's own card and, for the host, every seat open the team list.
            if(teams){var tr=new Rect(r.xMax-12-86,r.y+8,86,22);LbTeamChip(tr,p,team,a,p.you||Hosting);}
            else if(removable&&LbIconButton(new Rect(r.xMax-12-24,r.y+7,24,24),"x",62100+p.id,!MenuBlocked(),a)){var o=RoomOptions;if(o!=null)ApplyRule(RoomOpt.Bots,Mathf.Max(0,o.bots-1),0);}
            // The portrait, as tall as the card allows over its text.
            float foot=26,text=r.height>250?92:r.height>200?70:48,top=r.y+36,stageH=r.height-36-text-foot-6;
            var stage=new Rect(r.x+4,top,r.width-8,Mathf.Max(0,stageH));
            if(stage.height>=60){float ring=Mathf.Min(stage.height*.32f,r.width*.27f);LobbyPortrait(stage,new Vector2(stage.center.x,stage.center.y+2),ring,p,index,a,t,weapon,style,false);}
            if(paint)
            {
                float ty=stage.yMax+2,pad=14,w=r.width-pad*2;
                U.Text(new Rect(r.x+pad,ty,w,22),Fit(p.name,17,w,true,-.02f),17,C.Ink(a),TextAnchor.MiddleLeft,bold:true,tracking:-.02f);
                if(text>=70)
                {
                    string wn=weapon?.name??"Random";string cls=weapon!=null?ClassName(weapon.@class):"Mixed";
                    NativeIcons.Draw(new Rect(r.x+pad,ty+26,15,15),weapon!=null?NativeIcons.Spec(weapon.id):"shuffle",U.A(style,a));
                    float nw=Mathf.Min(w-22,U.Measure(wn,13,true).x+2);U.Text(new Rect(r.x+pad+21,ty+22,nw,22),Fit(wn,13,w-22,true,0),13,U.A(U.Mix(Color.white,style,.42f),a),TextAnchor.MiddleLeft,bold:true);
                    U.Text(new Rect(r.x+pad+21+nw+6,ty+22,Mathf.Max(0,w-27-nw),22),"· "+cls,12,C.Muted(a),TextAnchor.MiddleLeft);
                    ClassUnderline(new Rect(r.x+pad,ty+46,Mathf.Min(w,nw+21),2),weapon?.id,style,a,t+index*.9f);
                }
                if(text>=92&&weapon!=null&&!p.bot){var names=LbAbilityNames(weapon,p.you?prefs.weaponTechniques:p.techniques);float half=(w-6)/2;LbAbilityChip(new Rect(r.x+pad,ty+56,half,22),"Q",names[0],names[2],a);LbAbilityChip(new Rect(r.x+pad+half+6,ty+56,half,22),"E",names[1],names[3],a);}
                // The ready band.
                var b=new Rect(r.x+1,r.yMax-foot-1,r.width-2,foot);string state=!p.connected?"OFFLINE":ready?"READY":"NOT READY";Color st=!p.connected?C.Dim():ready?C.Good():C.Muted();
                U.Fill(new Rect(b.x+8,b.y,b.width-16,1),C.Edge(.55f*a));if(ready)U.HGradient(new Rect(b.x+8,b.y+1,b.width-16,b.height-2),C.Good(.0f),C.Good(.16f*a));
                NativeIcons.Draw(new Rect(b.x+14,b.center.y-6.5f,13,13),!p.connected?"circle-dashed":ready?"check":"clock-3",U.A(st,a));U.Text(new Rect(b.x+33,b.y,b.width-40,b.height),state,10.5f,U.A(st,a),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
            }
            PromotionButton(new Rect(r.x+12,stage.yMax-30,r.width-24,26),p,e);
        }
        // An ability under a card: its key and name, in the technique's colour when the pilot swapped one in.
        private static void LbAbilityChip(Rect r,string key,string name,string accent,float a)
        {
            Color tone=accent!=null?U.Hex(Convert.ToInt32(accent,16)):C.CyanSoft();U.Round(r,C.Navy(.85f*a),5,U.A(U.Mix(C.Edge(),tone,accent!=null?.55f:.15f),a));
            U.Text(new Rect(r.x+7,r.y,12,r.height),key,10,U.A(tone,a),TextAnchor.MiddleLeft,bold:true,mono:true);U.Text(new Rect(r.x+21,r.y,r.width-26,r.height),Fit(name,10.5f,r.width-26,false,0),10.5f,C.Ink(.88f*a),TextAnchor.MiddleLeft);
        }
        // A seat's two abilities' names (and the accents of the techniques among them), cached by the weapon and its loadout.
        private readonly Dictionary<string,string[]> lbAbilityNames=new Dictionary<string,string[]>();private readonly List<NativeLocker.AbilityOption> lbScratch=new List<NativeLocker.AbilityOption>(8);
        private string[] LbAbilityNames(ContentWeapon w,string techniques)
        {
            string key=w.id+"\n"+(techniques??"");if(lbAbilityNames.TryGetValue(key,out var names))return names;if(lbAbilityNames.Count>64)lbAbilityNames.Clear();names=new string[4];
            for(int s=0;s<2;s++)
            {
                string slot=s==0?"q":"e";var d=NativeTechniques.Equipped(techniques,w.id,slot);
                if(d!=null){names[s]=d.Name;names[s+2]=d.Accent.ToString("x6");continue;}
                NativeLocker.SlotOptions(content,w,slot,lbScratch);names[s]=lbScratch.Count>0?lbScratch[0].Name:"—";
            }
            lbAbilityNames[key]=names;return names;
        }
        // A team chip on a card in a teams room: the team's dot and name; editable opens the team list under it.
        private void LbTeamChip(Rect r,LobbyPilot p,int team,float a,bool editable)
        {
            Color tc=TeamColor(team);float h=U.HoverAmount(7800+p.id,r,editable&&!MenuBlocked()),s=editable?1:.6f;
            if(Event.current.type==EventType.Repaint)
            {
                H.Cut(r,U.A(U.Mix(C.Navy(),tc,.16f),a),5);H.Outline(r,U.A(U.Mix(U.Mix(C.Edge(),tc,.6f),C.CyanSoft(),h),a*s),5);
                var dot=new Rect(r.x+9,r.center.y-4,8,8);U.Radial(new Rect(dot.x-6,dot.y-6,20,20),U.A(tc,.35f*a));U.Disc(dot,U.A(tc,a));
                U.Text(new Rect(r.x+22,r.y,r.width-38,r.height),TeamNames[team],11,U.A(C.Ink(),a*s),TextAnchor.MiddleLeft,bold:true);
                if(editable)NativeIcons.DrawMarkup(new Rect(r.xMax-17,r.center.y-6,12,12),"chevron-down",ChevronDown,24,U.A(C.Ink(),.7f*a));
            }
            if(U.Button(r,editable&&!MenuBlocked())){Click();teamPopupSeat=p.id;teamPopupSelf=p.you;teamPopupTeam=team;teamPopupAnchor=r;teamPopupScroll=lobbyRosterScroll;}
        }
        // A seat nobody holds yet: the dashed frame and what fills it (the host adds a bot to the first one).
        private void LbOpenSlot(Rect r,int index,float e,bool add,bool host)
        {
            if(Event.current.type==EventType.Repaint)
            {
                U.Round(r,C.Navy(.35f*e),10);DashedRound(r,C.Edge(.9f*e),10);
                var ring=new Rect(r.center.x-22,r.center.y-44,44,44);U.Round(ring,C.Navy(.6f*e),22,C.Edge(.8f*e));NativeIcons.Draw(new Rect(ring.center.x-10,ring.center.y-10,20,20),"plus",C.Muted(e));
                U.Text(new Rect(r.x+10,ring.yMax+10,r.width-20,16),"OPEN SLOT",10.5f,C.Muted(e),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.18f);
                if(!add)U.Text(new Rect(r.x+14,ring.yMax+30,r.width-28,18),host?"Share your room code":"Waiting for a pilot",12,C.Dim(e),TextAnchor.MiddleCenter);
            }
            if(add){float w=Mathf.Min(r.width-28,150);if(MenuButton(new Rect(r.center.x-w/2,r.center.y+16,w,38),"bot","Add a bot",null,false,!MenuBlocked(),e,13,true)){var o=RoomOptions;if(o!=null)ApplyRule(RoomOpt.Bots,Mathf.Min(7,o.bots+1),0);}}
        }

        // ---- The pilot's loadout ------------------------------------------------------------------------------------------
        private readonly List<NativeLocker.AbilityOption> lbQ=new List<NativeLocker.AbilityOption>(8),lbE=new List<NativeLocker.AbilityOption>(8);private string lbAbilitySpec;
        private void LbAbilities()
        {
            if(Event.current.type!=EventType.Layout&&lbAbilitySpec!=null)return;var w=MenuWeapon();string id=w?.id??"";if(id==lbAbilitySpec)return;lbAbilitySpec=id;
            NativeLocker.SlotOptions(content,w,"q",lbQ);NativeLocker.SlotOptions(content,w,"e",lbE);NativeLocker.AddTechniques(content,w,"q",lbQ);NativeLocker.AddTechniques(content,w,"e",lbE);
        }
        private void LbLoadoutPanel(Rect r,float e)
        {
            LbAbilities();var w=MenuWeapon();
            LbHeading(new Rect(r.x,r.y,r.width,28),"YOUR LOADOUT",LbSyncNote(),e);
            // The weapon: its icon well, name and class; it opens the weapon picker.
            var wr=new Rect(r.x,r.y+38,r.width,72);float h=U.HoverAmount(62010,wr,!MenuBlocked());Color tone=w!=null?U.Hex((int)w.color):C.Cyan();
            if(Event.current.type==EventType.Repaint)
            {
                var d=new Rect(wr.x,wr.y-2*h,wr.width,wr.height);U.RoundGradient(d,U.A(U.Mix(C.Indigo(),tone,.1f+.06f*h),.95f*e),C.Navy(.96f*e),9,U.A(U.Mix(C.Edge(),tone,.35f+.5f*h),e),1,true);
                var well=new Rect(d.x+12,d.y+12,48,48);U.Round(well,C.Navy(e),9,U.A(tone,.7f*e));U.Radial(new Rect(well.x-10,well.y-10,68,68),U.A(tone,.18f*e));NativeIcons.Draw(new Rect(well.center.x-13,well.center.y-13,26,26),w!=null?NativeIcons.Spec(w.id):"shuffle",U.A(U.Mix(tone,Color.white,.2f),e));
                U.Text(new Rect(well.xMax+14,d.y+14,d.width-well.width-70,24),w?.name??"Pick a weapon",18,C.Ink(e),TextAnchor.MiddleLeft,bold:true,tracking:-.02f);
                U.Text(new Rect(well.xMax+14,d.y+38,d.width-well.width-70,18),(w!=null?ClassName(w.@class).ToUpperInvariant()+" · ":"")+"CHANGE WEAPON",10,U.A(U.Mix(C.Muted(),C.CyanSoft(),h),e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);
                NativeIcons.DrawMarkup(new Rect(d.xMax-30,d.center.y-8,16,16),"chevron-right",ChevronRight,24,U.A(U.Mix(C.Muted(),C.CyanSoft(),h),e));
            }
            if(U.Button(wr,!MenuBlocked())){Click();LbOpenDrawer("weapon");}
            // The two ability slots.
            float tw=(r.width-12)/2;var qr=new Rect(r.x,wr.yMax+12,tw,104);var er=new Rect(qr.xMax+12,qr.y,tw,104);
            LbSlotTile(qr,"q",lbQ,w,e,62012);LbSlotTile(er,"e",lbE,w,e,62014);
            U.Text(new Rect(r.x,qr.yMax+8,r.width,16),Hosting?"Your picks save to your pilot and reach the room at once.":"Your picks save to your pilot and reach the host at once.",11,C.Dim(e),TextAnchor.MiddleLeft);
        }
        private string LbSyncNote()=>Hosting?"":server==null?"":lbPendingAt>=0?"· SENDING…":"· SYNCED";
        // A slot: its key, what is in it, and whether it can change.
        private void LbSlotTile(Rect r,string slot,List<NativeLocker.AbilityOption> options,ContentWeapon w,float e,int id)
        {
            bool can=options.Count>1;int eq=options.Count>0?LockerEquippedAbility(w,slot,options):-1;var o=eq>=0?options[eq]:null;bool tech=o?.Technique!=null;Color tone=tech?U.Hex(o.Technique.Accent):C.Cyan();
            float h=U.HoverAmount(id,r,can&&!MenuBlocked()),flash=lbPickSlot==slot&&!prefs.reducedEffects?Mathf.Clamp01(1-(Time.unscaledTime-lbPickAt)/.5f):0;
            if(Event.current.type==EventType.Repaint)
            {
                var d=new Rect(r.x,r.y-2*h,r.width,r.height);if(flash>0)U.Glow(d,U.A(tone,.35f*flash*e),18);
                U.RoundGradient(d,U.A(U.Mix(C.Indigo(),tone,.07f+.08f*h+.2f*flash),.95f*e),C.Navy(.96f*e),9,U.A(U.Mix(C.Edge(),tone,(tech?.55f:.2f)+.45f*h),e),1,true);
                if(tech)U.Fill(new Rect(d.x+10,d.y,d.width-20,2),U.A(tone,.9f*e));
                var cap=new Rect(d.x+10,d.y+10,26,26);U.Round(cap,C.Navy(e),6,U.A(tone,.75f*e));U.Text(cap,slot=="q"?"Q":"E",13,U.A(U.Mix(tone,Color.white,.35f),e),TextAnchor.MiddleCenter,bold:true,mono:true);
                if(o==null){U.Text(new Rect(d.x+10,d.y+46,d.width-20,40),NativeLocker.EmptySlot(w,slot),11,C.Muted(e),TextAnchor.UpperLeft,wrap:true);return;}
                NativeIcons.Draw(new Rect(cap.xMax+8,cap.y+5,16,16),o.Icon,U.A(U.Mix(tone,Color.white,.15f),e));
                U.Text(new Rect(cap.xMax+30,cap.y,d.width-cap.width-48,26),Fit(o.Meta??"",9.5f,d.width-cap.width-48,false,.04f),9.5f,U.A(U.Mix(tone,Color.white,.25f),.9f*e),TextAnchor.MiddleLeft,mono:true);
                var lines=U.Wrap(o.Name,14,d.width-20,true,2);for(int i=0;i<lines.Count;i++)U.Text(new Rect(d.x+10,d.y+42+i*18,d.width-20,18),lines[i],14,C.Ink(e),TextAnchor.MiddleLeft,bold:true);
                U.Text(new Rect(d.x+10,d.yMax-22,d.width-20,14),can?(tech?o.Technique.Tag+" · CHANGE":"DEFAULT · CHANGE"):"FIXED",9.5f,U.A(can?U.Mix(C.Muted(),tone,.4f+.6f*h):C.Dim(),e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.12f);
            }
            if(U.Button(r,can&&!MenuBlocked())){Click();LbOpenDrawer(slot);}
        }

        // ---- The picker sheet --------------------------------------------------------------------------------------------
        // Over the room's right side, under a scrim: the weapon picker (the class and weapon cards), or every option for one ability slot. A click
        // outside it, Escape or the pad's B closes it; while it is open nothing under it takes the pointer or the pad.
        private string lbDrawer="";private float lbDrawerAt=-10,lbDrawerScroll,lbDrawerContent;private Vector2 lbDrawerLayout;private bool lbDrawerClosing;
        private bool LbDrawerShown=>lbDrawer.Length>0;
        private void LbOpenDrawer(string mode){lbDrawer=mode;lbDrawerAt=Time.unscaledTime;lbDrawerScroll=0;lbDrawerLayout=Vector2.zero;teamPopupSeat=0;}
        private void LbCloseDrawer(){if(lbDrawer.Length==0)return;lbDrawer="";lbDrawerAt=Time.unscaledTime;}
        private Rect LbDrawerRect(float k)
        {
            var p=lbPanel;float dw=Mathf.Min(620,Mathf.Max(440,p.width*.42f));return new Rect(p.xMax-dw-12+(1-k)*48,p.y+12,dw,p.height-24);
        }
        // Before the room draws: the keys that close the sheet.
        private void LbDrawerInput()
        {
            if(!LbDrawerShown)return;var ev=Event.current;
            if(ev.type==EventType.KeyDown&&(ev.keyCode==KeyCode.Escape||ev.keyCode==KeyCode.Backspace)){ev.Use();Click();LbCloseDrawer();return;}
            if(ev.type==EventType.Layout&&Input.GetKeyDown(KeyCode.JoystickButton1)){Click();LbCloseDrawer();}
        }
        private void LbDrawer(float e)
        {
            float now=Time.unscaledTime,since=now-lbDrawerAt;bool open=LbDrawerShown;float k=open?Mathf.Clamp01(since/.22f):1-Mathf.Clamp01(since/.16f);if(prefs.reducedEffects)k=open?1:0;if(k<=0||e<=0)return;
            float q=LbEase(k)*e;var r=LbDrawerRect(LbEase(k));var ev=Event.current;
            if(open&&ev.type==EventType.MouseDown&&!U.Hover(r)){ev.Use();Click();LbCloseDrawer();return;}
            if(ev.type==EventType.Repaint)
            {
                U.Fill(new Rect(lbPanel.x,lbPanel.y,lbPanel.width,lbPanel.height),C.Void(.5f*q));U.Glow(new Rect(r.x,r.y+30,r.width,r.height),new Color(0,0,0,.5f*q),50);U.Backdrop(r,0,16,q);
                H.Panel(r,q,C.Cyan(),14,H.Corners.Diagonal,true,.97f);
            }
            if(!open)return;
            var w=MenuWeapon();string mode=lbDrawer;bool weapon=mode=="weapon";
            string eyebrow=weapon?"LOADOUT · WEAPON":"ABILITY · "+mode.ToUpperInvariant()+" SLOT",title=weapon?"Choose your weapon":(w?.name??"Weapon")+" · "+(mode=="q"?"Q ability":"E ability");
            U.Text(new Rect(r.x+26,r.y+22,r.width-100,16),eyebrow,11,C.CyanSoft(.95f*q),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.22f);
            U.Text(new Rect(r.x+26,r.y+40,r.width-100,32),title,22,C.Ink(q),TextAnchor.MiddleLeft,bold:true,tracking:-.02f);
            if(LbIconButton(new Rect(r.xMax-26-38,r.y+22,38,38),"x",62020,true,q)){LbCloseDrawer();return;}
            var body=new Rect(r.x+26,r.y+88,r.width-52,r.height-88-22);
            if(weapon)
            {
                var c=GUI.color;GUI.color=new Color(c.r,c.g,c.b,c.a*q);GUILayout.BeginArea(body);U.BeginDeferred();lbDrawerLayout=GUILayout.BeginScrollView(lbDrawerLayout,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
                try{BeginRules(false,body.width);ClassRow("Your class",ClassCardStyle.Stacked);WeaponCards("Your weapon");}
                finally{GUI.enabled=true;GUILayout.Space(4);GUILayout.EndScrollView();GUILayout.EndArea();GUI.color=c;U.EndDeferred(body,lbDrawerLayout);}
                return;
            }
            LbAbilityList(body,w,mode,mode=="q"?lbQ:lbE,q);
        }
        private static float LbEase(float k)=>NativeUiMotion.Bezier(.2f,.8f,.2f,1,k);
        // Every option for the slot: the default and the weapon's techniques, each with its icon, name, cost and what it does. A pick takes at
        // once, flashes, and reaches the room a moment later.
        private void LbAbilityList(Rect body,ContentWeapon w,string slot,List<NativeLocker.AbilityOption> options,float q)
        {
            if(w==null||options.Count==0){U.Text(body,NativeLocker.EmptySlot(w,slot),13,C.Muted(q),TextAnchor.UpperLeft,wrap:true);return;}
            var ev=Event.current;if(ev.type==EventType.ScrollWheel&&U.Hover(body)){lbDrawerScroll=Mathf.Clamp(lbDrawerScroll+ev.delta.y*24,0,Mathf.Max(0,lbDrawerContent-body.height));ev.Use();}
            int eq=LockerEquippedAbility(w,slot,options);float cw=body.width,y=body.y-lbDrawerScroll,age=prefs.reducedEffects?9:Time.unscaledTime-lbDrawerAt;
            NativeControllerPointer.SetClip(body);U.BeginBand(body);
            try
            {
                for(int i=0;i<options.Count;i++)
                {
                    var o=options[i];var lines=U.Wrap(o.Text??"",12.5f,cw-92,false,5);float ch=64+lines.Count*17+14;var cr=new Rect(body.x,y,cw,ch);y+=ch+10;
                    float settle=Mathf.Clamp01((age-.04f*i)/.2f);settle=settle*settle*(3-2*settle);cr.x+=(1-settle)*16;
                    if(cr.yMax<body.y||cr.y>body.yMax)continue;
                    if(LbAbilityCard(cr,o,i==eq,62200+(slot=="q"?0:64)+i,w,slot,lines,q*settle)){prefs.weaponTechniques=NativeTechniques.Equip(prefs.weaponTechniques,w.id,slot,o.Technique?.Id);lbPickSlot=slot;lbPickAt=Time.unscaledTime;lbPickIndex=i;lbSaveNeeded=true;nativeAudio?.UiCue("ui.loadout.select","install",.4f);}
                }
            }
            finally{U.EndBand();NativeControllerPointer.ClearClip();}
            lbDrawerContent=y+lbDrawerScroll-body.y;
        }
        private string lbPickSlot;private float lbPickAt=-10;private int lbPickIndex=-1;private bool lbSaveNeeded;
        private bool LbAbilityCard(Rect r,NativeLocker.AbilityOption o,bool on,int id,ContentWeapon w,string slot,List<string> lines,float e)
        {
            bool tech=o.Technique!=null;float h=U.HoverAmount(id,r,!on);Color tone=tech?U.Hex(o.Technique.Accent):C.Cyan();
            float pop=lbPickSlot==slot&&lbPickIndex>=0&&on&&!prefs.reducedEffects?Mathf.Clamp01(1-(Time.unscaledTime-lbPickAt)/.45f):0;
            if(Event.current.type==EventType.Repaint)
            {
                var d=new Rect(r.x,r.y-2*h,r.width,r.height);if(on)U.Glow(d,U.A(tone,(.16f+.35f*pop)*e),14+10*pop);
                U.RoundGradient(d,U.A(U.Mix(C.Indigo(),tone,(on?.14f:.04f)+.06f*h+.2f*pop),.96f*e),C.Navy(.97f*e),10,on?U.A(tone,e):U.A(U.Mix(C.Edge(),tone,.25f+.5f*h),e),1,true,on?1.5f:1);
                if(tech)U.Fill(new Rect(d.x+12,d.y,d.width-24,on?2:1),U.A(tone,(on?1:.5f+.5f*h)*e));
                var well=new Rect(d.x+14,d.y+14,40,40);U.Round(well,C.Navy(e),9,U.A(tone,(on?.9f:.45f+.4f*h)*e));U.Radial(new Rect(well.x-8,well.y-8,56,56),U.A(tone,(.1f+.15f*h)*e));
                float ic=22+6*pop;NativeIcons.Draw(new Rect(well.center.x-ic/2,well.center.y-ic/2,ic,ic),o.Icon,U.A(U.Mix(tone,Color.white,.18f),e));
                float tx=well.xMax+14,tw=d.xMax-tx-44;U.Text(new Rect(tx,d.y+14,tw,22),Fit(o.Name,16,tw,true,-.01f),16,C.Ink(e),TextAnchor.MiddleLeft,bold:true);
                U.Text(new Rect(tx,d.y+36,tw,16),o.Meta??"",10,U.A(U.Mix(tone,Color.white,.25f),e),TextAnchor.MiddleLeft,mono:true);
                for(int i=0;i<lines.Count;i++)U.Text(new Rect(tx,d.y+60+i*17,d.width-tx+d.x-18,17),lines[i],12.5f,C.Muted(e),TextAnchor.MiddleLeft);
                string tag=tech?o.Technique.Tag:o.FromWeapon?"WEAPON "+slot.ToUpperInvariant():"DEFAULT";float fw=U.Measure(tag,9,true,true,.12f).x+14;var badge=new Rect(d.xMax-14-fw,d.y+14,fw,17);
                H.Cut(badge,C.Navy(e),3);H.Outline(badge,U.A(U.Mix(C.Edge(),tone,.6f),e),3);U.Text(badge,tag,9,U.A(U.Mix(tone,Color.white,.35f),e),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
                string other=slot=="q"?"e":"q";bool elsewhere=tech&&!on&&NativeTechniques.Equipped(prefs.weaponTechniques,w.id,other)==o.Technique;
                if(on){var tick=new Rect(d.xMax-14-22,d.yMax-14-22,22,22);U.Round(tick,U.A(tone,e),11);NativeIcons.Draw(new Rect(tick.x+4,tick.y+4,14,14),"check",C.Navy(e));}
                else if(elsewhere)U.Text(new Rect(d.xMax-90,d.yMax-26,76,14),"ON "+other.ToUpperInvariant()+" · MOVES",9,U.A(tone,e),TextAnchor.MiddleRight,bold:true,mono:true,tracking:.1f);
            }
            bool click=!on&&U.Button(r,true);if(click)Click();return click;
        }

        // ---- Keeping the room up to date --------------------------------------------------------------------------------
        private string lbSent,lbPending;private float lbPendingAt=-1,lbEnteredAt;
        private string LbWant()=>prefs.weapon+"|"+NativeTechniqueLoadout.Normalize(prefs.weaponTechniques);
        // On entering the room: what the room has is what the pilot has.
        private void LbEnter(){lbSent=LbWant();lbPending=null;lbPendingAt=-1;lbEnteredAt=Time.unscaledTime;lbDrawer="";lbDrawerAt=-10;lbSaveNeeded=false;}
        // A third of a second after the last change of weapon or ability: the host's seat goes out with its next broadcast; a guest sends its
        // profile (still ready: the ready message, which carries the profile and keeps it ready). An ability pick is saved to the pilot then.
        private void LbSync()
        {
            if(Event.current.type!=EventType.Layout)return;string want=LbWant();float now=Time.unscaledTime;
            if(want==lbSent){lbPendingAt=-1;return;}
            if(want!=lbPending){lbPending=want;lbPendingAt=now;return;}
            if(now-lbPendingAt<.33f)return;
            lbSent=want;lbPending=null;lbPendingAt=-1;if(lbSaveNeeded){lbSaveNeeded=false;SavePilot();}
            if(Hosting)LobbyBroadcast();
            else if(server!=null){if(networkReady)GuestReady(true);else SendServer(new NativeNetMessage{type="profile",profile=Profile(),token=account?.Token});}
        }
    }
}
