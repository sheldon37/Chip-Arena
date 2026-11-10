using System;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
namespace ChipArena.UnityConversion
{
    // Create Room (redesigned 2026-11-10 at the owner's request: "Improve UI for create room screen ... overhaul the UI for pvp and create a room.
    // Make them much better quality and a very clean / beautiful design"). The setup on the left in sections: the mode as three cards; for a
    // versus room the arena as a grid of tiles (Random first), the format (free-for-all or teams as one switch, then the sets, rounds, bots and
    // the rest of the match rows), the draft and intros; the connection last. On the right: the pilot, a live summary of the room and Create
    // Room. Every choice goes through the rules the form always used (ApplyRule, SetRoomOptions) and the room opens as before (HostRoom /
    // HostRelay).
    public sealed partial class ConversionGame
    {
        private Vector2 createScroll;
        private void CreateRoomScreen(float age)
        {
            screenContent=4000;
            var l=BeginScreen("CHIP ARENA","Host a room",Mathf.Min(1340,U.W-48),age,false,actions:false);if(l.Closed){CloseNetwork();Go(MenuScreen.Home);return;}
            float e=l.Enter;var b=l.Body;float side=U.W<1200?330:370,gap=28;
            var form=new Rect(b.x,b.y,b.width-side-gap,b.height);var col=new Rect(form.xMax+gap,b.y,side,b.height);
            U.Fill(new Rect(col.x-gap/2f,col.y,1,col.height),C.Edge(.5f*e));
            var c=GUI.color;GUI.color=new Color(c.r,c.g,c.b,c.a*e);U.BeginDeferred();GUILayout.BeginArea(form);
            createScroll=GUILayout.BeginScrollView(createScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            try{CreateForm(form.width-18);}
            finally{GUI.enabled=true;GUILayout.Space(8);GUILayout.EndScrollView();GUILayout.EndArea();GUI.color=c;U.EndDeferred(form,createScroll);}
            CreateSide(col,e);
        }
        // ---- The form ----------------------------------------------------------------------------------------------------
        private void CreateForm(float width)
        {
            BeginRules(false,width);var o=rules;if(o==null)return;
            CreateSection("MODE","How the room plays");
            CreateModes(o.expedition?2:o.cooperative?1:0,width);
            if(!o.cooperative)
            {
                CreateSection("ARENA",o.map=="random"?"Random · any arena in the rotation":"Where the match is fought");
                CreateArenas(o,width);
                CreateSection("MATCH","Format, sets and bots");
                CreateSwitch(7600,"Match","Free-for-all","Teams",o.teams?1:0,v=>ApplyRule(RoomOpt.Mode,v,0));
                MatchOptions(RulesPart.Advanced,false,false);
                CreateSection("DRAFT & INTROS","Chip choices between sets");
                DraftOptions();IntroOptions();RuleCheck(7486,RoomOpt.BothDraft,"All players draft after each set",o.bothDraft);
            }
            else{CreateSection("PARTY","Set in the room");RuleFine(7488,"Party bots and where the run starts are set in the lobby.",0,6);IntroOptions();}
            CreateSection("CONNECTION",internetRooms?"Friends on any network join with your code":"Players on your local network");
            CreateSwitch(7604,"Connection","Online","Local network",internetRooms?0:1,v=>ApplyRule(RoomOpt.Online,v,0));
            RuleFine(7484,internetRooms?"This computer hosts the match. Share your room code so friends can join from any network.":"Only players on the same local network can join. The room shows your LAN address and invite link once it is open.",0,4);
            if(!internetRooms){Field("Port");portText=GUILayout.TextField(portText,5,GUILayout.Width(120));}
            else if(UseManagedRelay){listedRoom=NativeControllerPointer.Toggle(listedRoom,"Show in public room list");}
            RuleFine(7494,"Keep your game open while friends are in your room.",8,6);
        }
        private void CreateSection(string label,string note)
        {
            float e=GUI.color.a;GUILayout.Space(10);var r=GUILayoutUtility.GetRect(0,40,GUILayout.ExpandWidth(true));
            U.Defer(()=>{if(Event.current.type==EventType.Repaint)LbHeading(new Rect(r.x+4,r.y+8,r.width-8,28),label,note==null?null:"· "+note.ToUpperInvariant(),e);});
        }
        // The three modes as cards: its glyph, its name and what it is; the picked one lit.
        private static readonly string[] ModeIcons={"swords","skull","footprints"},ModeNotes={"Free-for-all or teams, up to 8 pilots, with bots if you like.","The party against the Siren. Party bots fill the seats.","The co-op campaign for 1–4 signed-in pilots."};
        private void CreateModes(int pick,float width)
        {
            float e=GUI.color.a;var r=GUILayoutUtility.GetRect(0,112,GUILayout.ExpandWidth(true));
            U.Defer(()=>
            {
                float g=12,cw=(r.width-8-g*2)/3;
                for(int i=0;i<3;i++)
                {
                    var cr=new Rect(r.x+4+i*(cw+g),r.y+6,cw,98);bool on=i==pick;float h=U.HoverAmount(7610+i,cr,!on&&!MenuBlocked());Color tone=i==0?C.Cyan():i==1?C.Magenta():C.Violet();
                    if(Event.current.type==EventType.Repaint)
                    {
                        var d=new Rect(cr.x,cr.y-2*h,cr.width,cr.height);if(on)U.Glow(d,U.A(tone,.2f*e),16);
                        U.RoundGradient(d,U.A(U.Mix(C.Indigo(),tone,on?.16f:.04f+.06f*h),.96f*e),C.Navy(.97f*e),10,on?U.A(tone,e):U.A(U.Mix(C.Edge(),tone,.4f*h),e),1,true,on?1.5f:1);
                        if(on)U.Fill(new Rect(d.x+12,d.y,d.width-24,2),U.A(tone,e));
                        var well=new Rect(d.x+14,d.y+16,34,34);U.Round(well,C.Navy(e),8,U.A(tone,(on?.9f:.45f)*e));NativeIcons.Draw(new Rect(well.center.x-10,well.center.y-10,20,20),ModeIcons[i],U.A(U.Mix(tone,Color.white,.2f),e));
                        U.Text(new Rect(well.xMax+12,d.y+16,d.width-well.width-40,34),VersusLabels[i],17,C.Ink(e),TextAnchor.MiddleLeft,bold:true);
                        var lines=U.Wrap(ModeNotes[i],12,d.width-28,false,2);for(int k=0;k<lines.Count;k++)U.Text(new Rect(d.x+14,d.y+58+k*16,d.width-28,16),lines[k],12,C.Muted(e),TextAnchor.MiddleLeft);
                        if(on){var tick=new Rect(d.xMax-14-18,d.y+14,18,18);U.Round(tick,U.A(tone,e),9);NativeIcons.Draw(new Rect(tick.x+3,tick.y+3,12,12),"check",C.Navy(e));}
                    }
                    if(!on&&U.Button(cr,!MenuBlocked())){Click();ApplyRule(RoomOpt.Versus,i,0);}
                }
            });
        }
        // The arenas as tiles: Random, then every arena in the menu's order, each with its colour and its line; picking one also turns the menu
        // backdrop to it.
        private void CreateArenas(NativeRoomOptions o,float width)
        {
            float e=GUI.color.a;int n=mapIds.Length+1,cols=Mathf.Clamp(Mathf.FloorToInt((width-8+10)/(200+10)),2,4),rows=Mathf.CeilToInt(n/(float)cols);float th=70,g=10;
            var r=GUILayoutUtility.GetRect(0,rows*th+(rows-1)*g+10,GUILayout.ExpandWidth(true));int pick=MapIndex(o);
            U.Defer(()=>
            {
                float cw=(r.width-8-g*(cols-1))/cols;
                for(int i=0;i<n;i++)
                {
                    var cr=new Rect(r.x+4+(i%cols)*(cw+g),r.y+4+(i/cols)*(th+g),cw,th);bool on=i==pick;float h=U.HoverAmount(7620+i,cr,!on&&!MenuBlocked());
                    var map=i==0?null:content.Map(mapIds[i-1]);Color tone=map!=null?U.Hex(map.accent):C.CyanSoft();
                    if(Event.current.type==EventType.Repaint)
                    {
                        var d=new Rect(cr.x,cr.y-1.5f*h,cr.width,cr.height);if(on)U.Glow(d,U.A(tone,.18f*e),14);
                        U.RoundGradient(d,U.A(U.Mix(C.Indigo(),tone,on?.14f:.03f+.06f*h),.96f*e),C.Navy(.97f*e),8,on?U.A(tone,e):U.A(U.Mix(C.Edge(),tone,.45f*h),e),1,true,on?1.5f:1);
                        U.Fill(new Rect(d.x+1,d.y+10,3,d.height-20),U.A(tone,(on?1:.55f+.45f*h)*e));
                        if(i==0)NativeIcons.Draw(new Rect(d.x+16,d.y+13,16,16),"shuffle",U.A(tone,e));
                        float tx=d.x+(i==0?40:16);U.Text(new Rect(tx,d.y+10,d.xMax-tx-30,22),i==0?"Random":Fit(map.name,15,d.xMax-tx-30,true,0),15,C.Ink(e),TextAnchor.MiddleLeft,bold:true);
                        string line=i==0?"Any arena in the rotation":map.tag;var lines=U.Wrap(line??"",11,d.width-28,false,2);for(int k=0;k<lines.Count;k++)U.Text(new Rect(d.x+16,d.y+34+k*14,d.width-28,14),lines[k],11,C.Muted(e),TextAnchor.MiddleLeft);
                        if(on){var tick=new Rect(d.xMax-10-16,d.y+12,16,16);U.Round(tick,U.A(tone,e),8);NativeIcons.Draw(new Rect(tick.x+3,tick.y+3,10,10),"check",C.Navy(e));}
                    }
                    if(!on&&U.Button(cr,!MenuBlocked())){Click();ApplyRule(RoomOpt.Map,i,0);}
                }
            });
        }
        // A two-way switch row: its label, then two segments, the picked one lit.
        private void CreateSwitch(int id,string label,string a,string b,int pick,Action<int> set)
        {
            float e=GUI.color.a;var r=GUILayoutUtility.GetRect(0,58,GUILayout.ExpandWidth(true));r=new Rect(r.x+4,r.y+8,r.width-8,42);
            U.Defer(()=>
            {
                float lw=Mathf.Max(140,r.width*.38f);if(Event.current.type==EventType.Repaint)U.Text(new Rect(r.x,r.y,lw,r.height),label,14,C.Ink(.92f*e),TextAnchor.MiddleLeft);
                var box=new Rect(r.x+lw+12,r.y,r.width-lw-25,r.height);float sw=box.width/2;
                if(Event.current.type==EventType.Repaint){U.Round(box,C.Navy(.9f*e),8,C.Edge(e));}
                for(int i=0;i<2;i++)
                {
                    var s=new Rect(box.x+3+i*sw,box.y+3,sw-6,box.height-6);bool on=i==pick;float h=U.HoverAmount(id+i,s,!on&&!MenuBlocked());
                    if(Event.current.type==EventType.Repaint)
                    {
                        if(on){U.Glow(s,C.Cyan(.18f*e),10);U.Round(s,U.A(U.Mix(C.Indigo(),C.Cyan(),.28f),e),6,C.Cyan(.9f*e));}else if(h>0)U.Round(s,C.Indigo(.8f*h*e),6);
                        U.Text(s,i==0?a:b,14,on?C.Ink(e):U.A(U.Mix(C.Muted(),C.Ink(),h),e),TextAnchor.MiddleCenter,bold:on);
                    }
                    if(!on&&U.Button(s,!MenuBlocked())){Click();set(i);}
                }
            });
        }
        // ---- The side: the pilot, the room in a few lines, Create Room ------------------------------------------------------
        private void CreateSide(Rect r,float e)
        {
            var o=rules??RulesNow();float btnH=56,sumH=o!=null&&!o.cooperative?40+5*30:40+2*30;
            var btn=new Rect(r.x,r.yMax-btnH,r.width,btnH);var sum=new Rect(r.x,btn.y-22-sumH,r.width,sumH);var card=new Rect(r.x,r.y+4,r.width,sum.y-18-r.y-4);
            // The pilot as the room will show it (the lobby's card, without the ready band).
            if(card.height>=200){var me=new LobbyPilot{id=1,you=true,name=NativeCosmeticRules.Name(prefs.name),weapon=prefs.weapon,techniques=prefs.weaponTechniques,tag="YOU",ready=true,connected=true,team=prefs.team,accent=HexColor(prefs.accentColor,0x70e1d0),body=HexColor(prefs.bodyColor,0xe5e7eb)};LbPilotCard(card,me,0,e,false,Time.unscaledTime,false,false);}
            else{sum.y=r.y;sum.height=btn.y-22-r.y;}
            // The room in a few lines.
            LbHeading(new Rect(sum.x,sum.y,sum.width,28),"YOUR ROOM",null,e);
            if(o!=null)
            {
                int i=0;void Line(string k,string v,Color tone){float y=sum.y+38+i*30;U.Text(new Rect(sum.x,y,96,24),k,10,C.Muted(e),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.14f);U.Text(new Rect(sum.x+100,y,sum.width-100,24),Fit(v,14,sum.width-100,true,0),14,U.A(tone,e),TextAnchor.MiddleLeft,bold:true);U.Fill(new Rect(sum.x,y+26,sum.width,1),C.Edge(.35f*e));i++;}
                Line("MODE",VersusLabels[o.expedition?2:o.cooperative?1:0]+(o.cooperative?"":o.teams?" · Teams":" · Free-for-all"),C.Ink());
                if(!o.cooperative)
                {
                    var map=o.map=="random"?null:content.Map(o.map);Line("ARENA",map?.name??"Random",map!=null?U.Mix(U.Hex(map.accent),Color.white,.35f):C.CyanSoft());
                    Line("FORMAT","First to "+o.sets+(o.sets==1?" set":" sets")+" · "+o.rounds+(o.rounds==1?" round":" rounds"),C.Ink());
                    Line("BOTS",o.bots<=0?"None":o.bots+(o.bots==1?" bot":" bots")+" · "+Capital(o.botDifficulty),C.Ink());
                }
                Line("JOIN",internetRooms?"Online · room code":"Local network · port "+portText,C.Ink());
            }
            bool busy=relayConnecting!=null;
            if(!string.IsNullOrEmpty(networkStatus))U.Text(new Rect(r.x,btn.y-18,r.width,16),Fit(networkStatus,12,r.width,false,0),12,NetworkError?C.Danger(e):C.Muted(e),TextAnchor.MiddleLeft);
            if(MenuButton(btn,"play",busy?"Starting server…":"Create Room",null,true,!busy&&connecting==null,e,18,true)){if(internetRooms)HostRelay();else HostRoom();}
        }
        private static string Capital(string s)=>string.IsNullOrEmpty(s)?"":char.ToUpperInvariant(s[0])+s.Substring(1);
    }
}
