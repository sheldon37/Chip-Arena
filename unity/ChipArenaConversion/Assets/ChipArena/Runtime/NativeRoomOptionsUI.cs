using System;
using System.Collections.Generic;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    // The source's match and room options as its form rows (main.ts matchOptions / draftOptions / introOptions, lobby-ui.ts
    // botLoadouts, team-ui.ts practiceTeamSetup / botDraftOption, style.css .settings-row / .match-options / .fine, lobby.css
    // .bot-loadouts / .team-setup and its narrow .lobby-room-settings rules), shared by Play vs Bots, Create Room, Test Boss
    // and the room lobby. Each row reserves its rect with GetRect in every event (its height from the width of the last
    // repaint) and draws and hit-tests in the body's deferred kit replay (U.Defer); selects are SelectField steppers.
    // The values come from RulesNow(): the host's or offline player's own preferences, or a guest's copy of the host's
    // RoomOptions, drawn disabled as the source draws a guest's rules. A change goes through RoomOptions / SetRoomOptions
    // (NativeLobbyLogic.cs), which re-broadcasts a host's room; the player's own team goes through ChangeTeam. UI only.
    public sealed partial class ConversionGame
    {
        private enum RoomOpt{Mode,Versus,FriendlyFire,Sets,Rounds,Map,RandomMap,Bots,Difficulty,SkipBotDrafts,SkipIntros,BothDraft,Choices,Timer,Mirror,Stationary,BotWeapon,BotTeam,HostTeam,SkipWaves,Online,Connection}
        private enum RulesPart{All,Essential,Advanced}
        private static readonly string[] ModeLabels={"Free-for-all","Teams"},VersusLabels={"Versus","Boss Test"},ConnectionLabels={"Local network","Internet relay"};
        private static readonly string[] SetLabels=Numbered(10,5),RoundLabels=Numbered(10,2),ChoiceLabels=Numbered(12,6);
        private static readonly string[] DifficultyIds={"dummy","easy","normal","hard"},DifficultyLabels={"Dummy","Easy","Normal","Hard"};
        private static readonly string[] BetweenSetsLabels={"Players who lost the set draft","All players draft"},PartyBotLabels={"0","1","2","3"};
        private static readonly string[] BotSlots={"01","02","03","04","05","06","07"},BotNames={"Bot 1","Bot 2","Bot 3","Bot 4","Bot 5","Bot 6","Bot 7"};
        private static readonly string[] BotTeamLabels={"Bot 1 team","Bot 2 team","Bot 3 team","Bot 4 team","Bot 5 team","Bot 6 team","Bot 7 team"};
        private static readonly string[] OpponentLabels={"0 opponents","1 opponent","2 opponents","3 opponents","4 opponents","5 opponents","6 opponents","7 opponents"};
        // BOTS: 1-7 in Play vs Bots (the source drops 0 there), 0..max in a room or Create Room.
        private static readonly string[] PracticeBotLabels=BotCounts(1,7);private static readonly string[][] RoomBotLabels={BotCounts(0,0),BotCounts(0,1),BotCounts(0,2),BotCounts(0,3),BotCounts(0,4),BotCounts(0,5),BotCounts(0,6),BotCounts(0,7)};
        private static string[] Numbered(int n,int standard){var a=new string[n];for(int i=0;i<n;i++)a[i]=(i+1)+(i+1==standard?" (default)":"");return a;}
        private static string[] BotCounts(int from,int to){var a=new string[to-from+1];for(int n=from;n<=to;n++)a[n-from]=n+(n==1?" bot":" bots");return a;}
        private string[] mapIds,mapLabels,botWeaponIds,botWeaponLabels;
        // ARENA: Random, then every arena (main.ts matchOptions arena); bot loadouts: Random weapon, then the ordinary weapons
        // by class (lobby-ui.ts botLoadouts; NativeRoomOptions.Normalize keeps no DEV weapon for a bot).
        private void RuleLists()
        {
            if(mapIds!=null)return;var maps=content.maps??new ContentMap[0];mapIds=new string[maps.Length];mapLabels=new string[maps.Length+1];mapLabels[0]="Random";
            for(int i=0;i<maps.Length;i++){mapIds[i]=maps[i].id;mapLabels[i+1]=maps[i].name;}
            var weapons=new List<ContentWeapon>();foreach(var c in NativeLoadoutText.Classes)foreach(var w in content.weapons)if(!w.devOnly&&w.@class==c)weapons.Add(w);
            botWeaponIds=new string[weapons.Count];botWeaponLabels=new string[weapons.Count+1];botWeaponLabels[0]="Random weapon";for(int i=0;i<weapons.Count;i++){botWeaponIds[i]=weapons[i].id;botWeaponLabels[i+1]=weapons[i].name;}
        }

        // ---- What the rows show ------------------------------------------------------------------------------------------
        private readonly NativeRoomOptions rulesMirror=new NativeRoomOptions();private NativeRoomOptions rules;private bool rulesLocked,rulesNarrow;private float rulesL=4,rulesR=4,rulesEstimate=578;
        // A guest is a viewer the host has welcomed (InRoom). A connection still waiting for the welcome is not in a room
        // yet: the menus keep the player's own preferences then (RoomOptions is the host's copy for any open connection,
        // and none before the first lobby message).
        private bool RulesGuest=>InRoom&&!Hosting;
        // receivedOptions and lobbySeats outlive the connection that brought them: nothing clears them before the next
        // lobby message (needsAstra: clear both in CloseNetwork and JoinRoom). The Join screen, the only place a join
        // starts and never in a room, marks what is held as stale every frame, so a guest who joins another room sees the
        // "host sets the mode" note and no seats until this connection's own first lobby message replaces them.
        private NativeRoomOptions staleRules;private NativeSeatFrame[] staleSeats;
        private void MarkRoomStale(){staleRules=receivedOptions;staleSeats=lobbySeats;}
        private NativeRoomOptions GuestRules{get{var o=RoomOptions;return o!=null&&!ReferenceEquals(o,staleRules)?o:null;}}
        private NativeSeatFrame[] RoomSeats=>!Hosting&&ReferenceEquals(lobbySeats,staleSeats)?Array.Empty<NativeSeatFrame>():lobbySeats;
        // A guest in the lobby reads the host's broadcast rules (null until they arrive; the lobby draws its note then).
        // Everywhere else, and for the host, the rows read the player's own preferences, mirrored into one reused object
        // (RoomOptions would allocate a copy per call).
        private NativeRoomOptions RulesNow()
        {
            if(RulesGuest&&menuScreen==MenuScreen.Lobby)return GuestRules;var m=rulesMirror;
            m.map=prefs.map;m.cooperative=prefs.pve;m.teams=prefs.teams;m.friendlyFire=prefs.friendlyFire;m.bots=prefs.bots;m.rounds=prefs.rounds;m.sets=prefs.sets;m.choices=prefs.choices;m.selectionSeconds=prefs.selectionSeconds;
            m.skipBotDrafts=prefs.skipBotDrafts;m.bothDraft=prefs.bothDraft;m.skipPlayerIntros=prefs.skipPlayerIntros;m.randomizeMap=prefs.randomizeMap;m.mirrorWeapons=prefs.mirrorWeapons;m.stationary=prefs.stationary;m.skipWaves=prefs.skipWaves;
            m.botDifficulty=prefs.botDifficulty;m.botOptions=prefs.botOptions??Array.Empty<NativeBotOption>();m.hostTeam=prefs.team;m.autoStart=prefs.autoStart;return m;
        }
        // Starts a body's rows. `narrow` is the 300 px room column, where labels stack over their selects (lobby.css narrow
        // .settings-row); `estimate` is the row width before a repaint has measured it; `inset` matches the form's 4 px margins.
        // The rows rest disabled while a guest connection is open, in a room or still joining (SetRoomOptions refuses then,
        // and the Join screen's connection should not change under a pending join).
        private bool GuestConnection=>!Hosting&&(server!=null||connecting!=null);
        private void BeginRules(bool narrow,float estimate,float inset=4){rules=RulesNow();rulesLocked=GuestConnection;rulesNarrow=narrow;rulesEstimate=estimate;rulesL=rulesR=inset;RuleLists();}
        private static NativeBotOption BotOption(NativeRoomOptions o,int i)=>o?.botOptions!=null&&i<o.botOptions.Length?o.botOptions[i]:null;
        // A bot's team as NativeLobbyRules.Bot seats it: its own choice, else the alternating default.
        private static int BotTeamOf(NativeRoomOptions o,int i){var b=BotOption(o,i);return b!=null&&b.team>=0?b.team:(i+1)%2;}
        private int BotWeaponIndex(NativeRoomOptions o,int i){var b=BotOption(o,i);if(b==null||b.weapon==null||b.weapon=="random")return 0;int k=Array.IndexOf(botWeaponIds,b.weapon);return k<0?0:k+1;}
        private int MapIndex(NativeRoomOptions o){if(o.map=="random")return 0;int k=Array.IndexOf(mapIds,o.map);return k<0?1:k+1;}
        private static int DifficultyIndex(string d){int k=Array.IndexOf(DifficultyIds,d);return k<0?2:k;}
        // Play vs Bots in teams mode can start only with two sides (main.ts practice: Start is disabled when every bot is on
        // the player's team).
        private bool PracticeTeamsBlocked()
        {
            if(!prefs.teams||prefs.pve)return false;var o=RulesNow();int bots=Mathf.Clamp(prefs.bots,1,7);
            for(int i=0;i<bots;i++)if(BotTeamOf(o,i)!=prefs.team)return false;return true;
        }

        // ---- Changes -----------------------------------------------------------------------------------------------------
        // Applied in the replay's mouse event, so the next layout pass already sees the new rows.
        private void ApplyRule(RoomOpt opt,int v,int arg)
        {
            if(opt==RoomOpt.Online||opt==RoomOpt.Connection){internetRooms=v==1;return;}
            // An open guest connection changes no rules: RoomOptions is then the host's copy (or none) and SetRoomOptions
            // refuses; a guest's own team goes through the lobby's team picker.
            if(!Hosting&&server!=null)return;
            if(opt==RoomOpt.HostTeam){ChangeTeam(v);return;}
            var o=RoomOptions;if(o==null)return;
            switch(opt)
            {
                case RoomOpt.Mode:o.teams=v==1;break;
                case RoomOpt.Versus:o.cooperative=v==1;break;
                case RoomOpt.FriendlyFire:o.friendlyFire=v==1;break;
                case RoomOpt.Sets:o.sets=v+1;break;
                case RoomOpt.Rounds:o.rounds=v+1;break;
                case RoomOpt.Map:o.map=v<=0||v>mapIds.Length?"random":mapIds[v-1];break;
                case RoomOpt.RandomMap:o.randomizeMap=v==1;break;
                case RoomOpt.Bots:o.bots=v;break;
                // One BOT DIFFICULTY for every bot, as the source has: per-bot entries follow it.
                case RoomOpt.Difficulty:o.botDifficulty=DifficultyIds[Mathf.Clamp(v,0,3)];o.botOptions=CopyBots(o.botOptions,0,o.botDifficulty);foreach(var b in o.botOptions)b.difficulty=o.botDifficulty;break;
                case RoomOpt.SkipBotDrafts:o.skipBotDrafts=v==1;break;
                case RoomOpt.SkipIntros:o.skipPlayerIntros=v==1;break;
                case RoomOpt.BothDraft:o.bothDraft=v==1;break;
                case RoomOpt.Choices:o.choices=v+1;break;
                case RoomOpt.Timer:o.selectionSeconds=v;break;
                case RoomOpt.Mirror:o.mirrorWeapons=v==1;break;
                case RoomOpt.Stationary:o.stationary=v==1;break;
                case RoomOpt.SkipWaves:o.skipWaves=v==1;break;
                case RoomOpt.BotWeapon:{var list=CopyBots(o.botOptions,arg+1,o.botDifficulty);list[arg].weapon=v<=0||v>botWeaponIds.Length?"random":botWeaponIds[v-1];o.botOptions=list;break;}
                case RoomOpt.BotTeam:{var list=CopyBots(o.botOptions,arg+1,o.botDifficulty);list[arg].team=Mathf.Clamp(v,0,7);o.botOptions=list;break;}
            }
            SetRoomOptions(o);
        }
        // A copy of the per-bot entries with at least `count` of them (RoomOptions shares the preference array); new
        // entries take the room's difficulty, a random weapon and the default team.
        private static NativeBotOption[] CopyBots(NativeBotOption[] from,int count,string difficulty)
        {
            from=from??Array.Empty<NativeBotOption>();var list=new NativeBotOption[Math.Min(7,Math.Max(from.Length,count))];
            for(int i=0;i<list.Length;i++){var b=i<from.Length?from[i]:null;list[i]=b!=null?new NativeBotOption{weapon=b.weapon,difficulty=b.difficulty,team=b.team}:new NativeBotOption{difficulty=difficulty};}
            return list;
        }

        // ---- Rows --------------------------------------------------------------------------------------------------------
        // Row ids (their widths here, their hover in SelectField / RuleCheck) start at 7460: the weapon cards hover as 7400
        // plus the content index (NativeLoadoutUI.cs SpecCards), and Play and the lobby draw both.
        private readonly Dictionary<int,float> ruleWidths=new Dictionary<int,float>();
        private float RuleWidth(int id)=>ruleWidths.TryGetValue(id,out float w)?w:Mathf.Max(60,rulesEstimate-rulesL-rulesR);
        // The row's layout rect less the current insets; its width is kept for the next layout pass.
        private Rect RuleRow(int id,float height)
        {
            var r=GUILayoutUtility.GetRect(0,height,GUILayout.ExpandWidth(true));r=new Rect(r.x+rulesL,r.y,Mathf.Max(10,r.width-rulesL-rulesR),r.height);
            if(Event.current.type==EventType.Repaint)ruleWidths[id]=r.width;return r;
        }
        // .settings-row with a select: the 14 px label in the first grid column (minmax(140px,38%)) and the select across
        // the rest, 13 px short of the edge (the empty checkbox column), 23 px between rows; stacked in the narrow column.
        // A null label is a full-width select under a field label. `first` offsets the value (BOTS from 1).
        private void RuleSelect(int id,RoomOpt opt,string label,string[] labels,int index,int arg=0,int first=0,bool enabled=true)
        {
            bool narrow=rulesNarrow,locked=rulesLocked||!enabled;float e=GUI.color.a;
            var r=RuleRow(id,label==null?(narrow?52:44):narrow?27+44+14:44+23);
            U.Defer(()=>
            {
                bool paint=Event.current.type==EventType.Repaint;Rect sel;
                if(label==null)sel=new Rect(r.x,r.y,r.width,44);
                else if(narrow){if(paint)U.Text(new Rect(r.x,r.y,r.width,21),label,14,U.Hex(0xe6e6ea,e),TextAnchor.MiddleLeft);sel=new Rect(r.x,r.y+27,r.width,44);}
                else{float lw=Mathf.Max(140,r.width*.38f);var row=new Rect(r.x,r.y+11.5f,r.width,44);if(paint)U.Text(new Rect(row.x,row.y,lw,44),label,14,U.Hex(0xe6e6ea,e),TextAnchor.MiddleLeft);sel=new Rect(row.x+lw+12,row.y,row.width-lw-25,44);}
                int pick=SelectField(id,sel,labels,index,!locked,e);if(pick!=index&&!locked)ApplyRule(opt,pick+first,arg);
            });
        }
        // .settings-row with a checkbox: the label (wrapping in the narrow column) and the 18 px box at the right; the whole
        // row toggles, as the label wrapping the input does in the source.
        private void RuleCheck(int id,RoomOpt opt,string label,bool value,bool enabled=true)
        {
            bool locked=rulesLocked||!enabled;float e=GUI.color.a;var lines=U.Wrap(label,14,RuleWidth(id)-34,false,3);float th=Mathf.Max(24,lines.Count*20);
            var r=RuleRow(id,th+22);
            U.Defer(()=>
            {
                var row=new Rect(r.x,r.y+11,r.width,th);var box=new Rect(row.xMax-4-18,row.center.y-9,18,18);float h=U.HoverAmount(id,row,!locked&&!MenuBlocked());
                if(Event.current.type==EventType.Repaint){float y=row.center.y-lines.Count*10;foreach(var l in lines){U.Text(new Rect(row.x,y,row.width-34,20),l,14,U.Hex(0xe6e6ea,e),TextAnchor.MiddleLeft);y+=20;}CheckBox(box,value,!locked,h,e);}
                if(U.Button(row,!locked)){Click();ApplyRule(opt,value?0:1,0);}
            });
        }
        // input[type=checkbox] as the source renders it: an 18 px box, #3b3b3b with a #858585 edge, or the #ff6b35 accent
        // with a dark tick; the edge warms on hover; half strength when disabled.
        private const string CheckTick="<path d=\"M5 12.5 L10 17.5 L19 6.5\" fill=\"none\" stroke=\"#fff\" stroke-width=\"3.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
        private static void CheckBox(Rect b,bool on,bool enabled,float hover,float e)
        {
            float a=e*(enabled?1:.5f);
            if(on){U.Round(b,U.A(U.Mix(U.Hex(0xff6b35),U.Hex(0xff865b),hover),a),4);NativeIcons.DrawMarkup(new Rect(b.x+2,b.y+2,14,14),"room-check",CheckTick,24,U.Hex(0x3b3b3b,a));}
            else U.Round(b,U.Hex(0x3b3b3b,a),4,U.A(U.Mix(U.Hex(0x858585),U.Hex(0xff784b),hover),a));
        }
        // label.field: 12 px caps #c0c1ca, 10 px above it and 8 px to the control below.
        private void RuleField(int id,string text)
        {
            float e=GUI.color.a;var r=RuleRow(id,34);
            U.Defer(()=>{if(Event.current.type==EventType.Repaint)U.Text(new Rect(r.x,r.y+10,r.width,16),text,12,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,bold:true);});
        }
        // p.fine: 13 px #a6a7ad at a 20 px line.
        private void RuleFine(int id,string text,float top=6,float bottom=10)
        {
            if(string.IsNullOrEmpty(text))return;float e=GUI.color.a;var lines=U.Wrap(text,13,RuleWidth(id),false,8);var r=RuleRow(id,top+lines.Count*20+bottom);
            U.Defer(()=>{if(Event.current.type!=EventType.Repaint)return;float y=r.y+top;foreach(var l in lines){U.Text(new Rect(r.x,y,r.width,20),l,13,U.Hex(0xa6a7ad,e),TextAnchor.MiddleLeft);y+=20;}});
        }
        // .bot-loadouts / .team-setup heading: the #4b414c rule `top` px below the last row, then .section-title (18 px title,
        // 14 px note at the right); in the narrow column the 23 px title with the note under it.
        private void RulesSection(int id,string title,string note,float top)
        {
            bool narrow=rulesNarrow;float e=GUI.color.a;var r=RuleRow(id,narrow?top+1+16+30+8+20+10:top+1+54);
            U.Defer(()=>
            {
                if(Event.current.type!=EventType.Repaint)return;U.Fill(new Rect(r.x,r.y+top,r.width,1),U.Hex(0x4b414c,e));float y=r.y+top+1;
                if(narrow){U.Text(new Rect(r.x,y+16,r.width,30),title,23,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true,tracking:-.025f);U.Text(new Rect(r.x,y+16+38,r.width,20),note,14,U.Hex(0xb8aabb,e),TextAnchor.MiddleLeft);return;}
                U.Text(new Rect(r.x,y,r.width,54),title,18,U.Hex(0xf4f4f5,e),TextAnchor.MiddleLeft,bold:true,tracking:-.02f);U.Text(new Rect(r.x,y,r.width,54),note,14,U.Hex(0xb8aabb,e),TextAnchor.MiddleRight);
            });
        }

        // ---- Groups ------------------------------------------------------------------------------------------------------
        // .match-options box (10 px corners, #45464e edge, 125deg #23242ae6 → #1a1b20e6): its rows sit `pad` inside. The box
        // is drawn first in the replay (under its rows) at the rect this pass measured between its top and bottom rows.
        private readonly Rect[] ruleBoxes=new Rect[4];private readonly float[] ruleBoxTop=new float[4];
        private void RulesBoxBegin(int k,float padTop,float pad)
        {
            float e=GUI.color.a;var t=RuleRow(7500+k,padTop);if(Event.current.type==EventType.Repaint){ruleBoxTop[k]=t.y;ruleBoxes[k].x=t.x;ruleBoxes[k].width=t.width;}
            U.Defer(()=>{var b=ruleBoxes[k];if(Event.current.type==EventType.Repaint&&b.height>0)U.RoundGradient(b,U.Hex(0x23242a,.9f*e),U.Hex(0x1a1b20,.9f*e),10,U.Hex(0x45464e,e),1,true);});
            rulesL+=pad;rulesR+=pad;
        }
        private void RulesBoxEnd(int k,float padBottom,float pad)
        {rulesL-=pad;rulesR-=pad;var b=RuleRow(7504+k,padBottom);if(Event.current.type==EventType.Repaint){ruleBoxes[k].y=ruleBoxTop[k];ruleBoxes[k].height=b.yMax-ruleBoxTop[k];}}
        // details.lobby-advanced ("Draft & intro rules"): the summary row, and when open a frame around its rows. The frame's
        // fill goes down now (under the timer field), at the height measured by the last repaint; edge and summary deferred.
        private readonly bool[] ruleFolds=new bool[2];private readonly float[] ruleFoldHeight=new float[2],ruleFoldTop=new float[2];
        private bool RulesFoldBegin(int k,string title)
        {
            float e=GUI.color.a;GUILayout.Space(14);var r=RuleRow(7520+k,50);bool open=ruleFolds[k];if(open&&Event.current.type==EventType.Repaint)ruleFoldTop[k]=r.yMax;
            var frame=open?new Rect(r.x,r.y,r.width,r.height+ruleFoldHeight[k]):r;if(open)U.UnderRound(frame,U.Hex(0x13141a,.4f*e),9);
            U.Defer(()=>
            {
                if(Event.current.type==EventType.Repaint)
                {
                    float h=U.HoverAmount(7525+k,r,!MenuBlocked());U.Round(frame,open?Color.clear:U.Hex(0x13141a,.4f*e),9,U.Hex(0x4c434e,e));if(h>0)U.Round(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(1,1,1,.03f*h*e),9);if(open)U.Fill(new Rect(r.x+1,r.yMax-1,r.width-2,1),U.Hex(0x4c434e,e));
                    FoldChevron(new Vector2(r.x+19,r.center.y),open,e);
                    U.Text(new Rect(r.x+33,r.y,r.width-45,r.height),title,15,U.Hex(0xded5e0,e),TextAnchor.MiddleLeft,bold:true);
                }
                if(U.Button(r)){ruleFolds[k]=!ruleFolds[k];Click();}
            });
            if(open){rulesL+=16;rulesR+=16;GUILayout.Space(4);}return open;
        }
        private void RulesFoldEnd(int k){rulesL-=16;rulesR-=16;var b=RuleRow(7528+k,12);if(Event.current.type==EventType.Repaint)ruleFoldHeight[k]=Mathf.Max(0,b.yMax-ruleFoldTop[k]);}

        // ---- The source's option blocks ----------------------------------------------------------------------------------
        // main.ts matchOptions(lobby,part): Match mode, friendly fire (teams), sets, rounds, ARENA with its description,
        // Randomize map after each set, BOTS and BOT DIFFICULTY, and the slot note; 'essential' is the room's mode and arena,
        // 'advanced' the rest. `practice` offers 1-7 bots; a room offers 0 up to the free seats.
        private void MatchOptions(RulesPart part,bool practice,bool lobby)
        {
            var o=rules;bool narrow=rulesNarrow;int k=part==RulesPart.Advanced?2:part==RulesPart.Essential?1:0;
            float pad=!narrow?20:RuleWidth(7500+k)<230?10:part==RulesPart.Advanced?12:16;RulesBoxBegin(k,narrow?12:14,pad);
            if(part!=RulesPart.Advanced)RuleSelect(7460,RoomOpt.Mode,"Match mode",ModeLabels,o.teams?1:0);
            if(part!=RulesPart.Essential)
            {
                if(o.teams){RuleCheck(7540,RoomOpt.FriendlyFire,"Friendly fire",o.friendlyFire);RuleFine(7541,o.friendlyFire?"Teammates can damage each other.":"Allied attacks and harmful effects pass through teammates.",0,8);}
                RuleSelect(7542,RoomOpt.Sets,"Sets to win the match",SetLabels,Mathf.Clamp(o.sets,1,10)-1);
                RuleSelect(7546,RoomOpt.Rounds,"Rounds to win a set",RoundLabels,Mathf.Clamp(o.rounds,1,10)-1);
            }
            if(part!=RulesPart.Advanced)MapGrid();
            if(part!=RulesPart.Essential)
            {
                RuleCheck(7549,RoomOpt.RandomMap,"Randomize map after each set",o.randomizeMap);
                if(practice)BotsAndDifficulty(PracticeBotLabels,Mathf.Clamp(o.bots,1,7)-1,1,DifficultyIndex(o.botDifficulty));
                else{int max=RoomBotMax(lobby);BotsAndDifficulty(RoomBotLabels[max],Mathf.Clamp(lobby?LobbyBotSeats():o.bots,0,max),0,DifficultyIndex(o.botDifficulty));}
                RuleFine(7560,lobby?"Up to 8 pilots total. Bots use player slots. Only the room owner can change these settings.":"Up to 8 pilots total. Bots use player slots.",2,4);
            }
            RulesBoxEnd(k,!narrow?22:part==RulesPart.Essential?6:10,pad);
        }
        // The room's bot seats as broadcast (the source counts its bot players), and how many seats the humans leave.
        private int LobbyBotSeats(){int n=0;foreach(var s in lobbySeats)if(s!=null&&s.bot)n++;return n;}
        private int RoomBotMax(bool lobby){if(!lobby)return 7;int humans=0;foreach(var s in lobbySeats)if(s!=null&&!s.bot)humans++;return Mathf.Clamp(8-Mathf.Max(1,humans),0,7);}
        // .row of BOTS and BOT DIFFICULTY side by side; stacked in the narrow column, where two steppers would not fit.
        private void BotsAndDifficulty(string[] counts,int index,int first,int difficulty)
        {
            if(rulesNarrow){RuleField(7550,"BOTS");RuleSelect(7552,RoomOpt.Bots,null,counts,index,0,first);RuleField(7556,"BOT DIFFICULTY");RuleSelect(7558,RoomOpt.Difficulty,null,DifficultyLabels,difficulty);return;}
            bool locked=rulesLocked;float e=GUI.color.a;var r=RuleRow(7550,8+16+8+44+4);
            U.Defer(()=>
            {
                float cw=(r.width-13)/2;if(Event.current.type==EventType.Repaint){U.Text(new Rect(r.x,r.y+8,cw,16),"BOTS",12,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,bold:true);U.Text(new Rect(r.x+cw+13,r.y+8,cw,16),"BOT DIFFICULTY",12,U.Hex(0xc0c1ca,e),TextAnchor.MiddleLeft,bold:true);}
                int b=SelectField(7552,new Rect(r.x,r.y+32,cw,44),counts,index,!locked,e);if(b!=index&&!locked)ApplyRule(RoomOpt.Bots,b+first,0);
                int d=SelectField(7558,new Rect(r.x+cw+13,r.y+32,cw,44),DifficultyLabels,difficulty,!locked,e);if(d!=difficulty&&!locked)ApplyRule(RoomOpt.Difficulty,d,0);
            });
        }
        // lobby-ui.ts botLoadouts: the section, a row per bot (slot number, "Bot N", the weapon select), the Random note. The
        // two Unity checks follow: "Bots use my weapon" overrides the loadouts (their selects then rest disabled) and
        // "Stationary targets" holds every bot still.
        private void BotLoadouts(int count,float top)
        {
            var o=rules;bool narrow=rulesNarrow,mirror=o.mirrorWeapons;count=Mathf.Clamp(count,0,7);RulesSection(7590,"Bot loadouts",OpponentLabels[count],top);
            for(int i=0;i<count;i++)BotLoadoutRow(i,!mirror,narrow);
            RuleFine(7594,"Random chooses an ordinary weapon when the match is created.",12,4);
            RuleCheck(7596,RoomOpt.Mirror,"Bots use my weapon",mirror);RuleCheck(7598,RoomOpt.Stationary,"Stationary targets",o.stationary);
        }
        // .bot-loadout: 28 px slot (14 px mono #ce9f7c), 60 px name, then the select; 10 px padding and a #ffffff0b rule. The
        // narrow column puts the select under the slot and name.
        private void BotLoadoutRow(int i,bool enabled,bool narrow)
        {
            bool locked=rulesLocked||!enabled;float e=GUI.color.a;int id=7600+i*4,index=BotWeaponIndex(rules,i);var r=RuleRow(id,narrow?10+24+6+44+10:66);
            U.Defer(()=>
            {
                bool paint=Event.current.type==EventType.Repaint;Rect sel;
                if(narrow){if(paint){U.Text(new Rect(r.x,r.y+10,28,24),BotSlots[i],14,U.Hex(0xce9f7c,e),TextAnchor.MiddleLeft,mono:true);U.Text(new Rect(r.x+34,r.y+10,r.width-34,24),BotNames[i],14,U.Hex(0xe6e6ea,e),TextAnchor.MiddleLeft);}sel=new Rect(r.x,r.y+40,r.width,44);}
                else{if(paint){U.Text(new Rect(r.x,r.y+10,28,46),BotSlots[i],14,U.Hex(0xce9f7c,e),TextAnchor.MiddleLeft,mono:true);U.Text(new Rect(r.x+38,r.y+10,60,46),BotNames[i],14,U.Hex(0xe6e6ea,e),TextAnchor.MiddleLeft);}sel=new Rect(r.x+108,r.y+11,r.width-108,44);}
                if(paint)U.Fill(new Rect(r.x,r.yMax-1,r.width,1),new Color(1,1,1,.043f*e));
                int pick=SelectField(id,sel,botWeaponLabels,index,!locked,e);if(pick!=index&&!locked)ApplyRule(RoomOpt.BotWeapon,pick,i);
            });
        }
        // team-ui.ts practiceTeamSetup: Teams / "8 colors available", Your team, a team per bot and the two-sides note.
        private void TeamSetup(int count)
        {
            var o=rules;count=Mathf.Clamp(count,0,7);RulesSection(7690,"Teams","8 colors available",20);
            RuleSelect(7694,RoomOpt.HostTeam,"Your team",TeamNames,TeamIndex(o.hostTeam));
            for(int i=0;i<count;i++)RuleSelect(7700+i*4,RoomOpt.BotTeam,BotTeamLabels[i],TeamNames,TeamIndex(BotTeamOf(o,i)),i);
            RuleFine(7730,"Choose at least two different teams. Teammates share round and set wins.",0,6);
        }
        // main.ts draftOptions: Chip choices per draft, the Chip selection timer (seconds), its note and the bot-draft check
        // (team-ui.ts botDraftOption).
        private void DraftOptions()
        {
            var o=rules;RuleSelect(7740,RoomOpt.Choices,"Chip choices per draft",ChoiceLabels,Mathf.Clamp(o.choices,1,12)-1);TimerRow(o.selectionSeconds);
            RuleFine(7748,"5–120 seconds per player. Also applies to specialization selection.",0,6);
            RuleCheck(7750,RoomOpt.SkipBotDrafts,"Skip bot chip choices",o.skipBotDrafts);RuleFine(7752,"Bots still receive a Chip instantly. Their selection screens and reveals are skipped.",0,6);
        }
        // main.ts introOptions.
        private void IntroOptions(){RuleCheck(7754,RoomOpt.SkipIntros,"Skip player intros",rules.skipPlayerIntros);RuleFine(7756,"Intros play once per match, before the opening countdown.",0,6);}
        // "Chip selection timer" with its 84 px number field and "seconds" at the right (an IMGUI text field, so it sits
        // under the deferred labels in GUILayout order); 5-120 is applied as typed, the field returns to the value on blur.
        private string timerText,timerShown;private int timerValue=-1;private GUIStyle timerStyle;
        private void TimerRow(int seconds)
        {
            if(timerStyle==null){timerStyle=new GUIStyle(GUI.skin.textField){fontSize=15,alignment=TextAnchor.MiddleCenter};}
            if(seconds!=timerValue){timerValue=seconds;timerShown=seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);}
            bool focused=GUI.GetNameOfFocusedControl()=="room-timer";if(!focused||timerText==null)timerText=timerShown;float e=GUI.color.a;
            GUILayout.BeginHorizontal();GUILayout.Space(rulesL);var label=GUILayoutUtility.GetRect(0,67,GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(GUILayout.Width(84));GUILayout.Space(11.5f);GUI.SetNextControlName("room-timer");string typed=GUILayout.TextField(timerText,3,timerStyle,GUILayout.Width(84),GUILayout.Height(44));GUILayout.EndVertical();
            var unit=GUILayoutUtility.GetRect(67,67,GUILayout.Width(67));GUILayout.Space(rulesR);GUILayout.EndHorizontal();
            if(typed!=timerText){timerText=typed;if(int.TryParse(typed,out int v)&&v>=5&&v<=120&&!rulesLocked)ApplyRule(RoomOpt.Timer,v,0);}
            U.Defer(()=>{if(Event.current.type!=EventType.Repaint)return;U.Text(new Rect(label.x,label.y,label.width,67),"Chip selection timer",14,U.Hex(0xe6e6ea,e),TextAnchor.MiddleLeft);U.Text(new Rect(unit.x+11,unit.y,unit.width-11,67),"seconds",14,U.Hex(0xe6e6ea,e),TextAnchor.MiddleLeft);});
        }
        // The two long text fields sit on a row reserved like every other (RuleRow) and draw with GUI.TextField on it: a
        // GUILayout.TextField's layout entry is at least as wide as its text, so a pasted link would widen the whole
        // scroll content (or overrun a fixed fold) past the panel instead of scrolling inside the field.
        // A read-only invite field (input.invite-url): selectable text that is never edited.
        private GUIStyle inviteStyle;
        private void InviteField(string invite)
        {
            if(inviteStyle==null){inviteStyle=new GUIStyle(GUI.skin.textField){fontSize=12,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip,wordWrap=false};inviteStyle.padding=new RectOffset(14,14,8,8);}
            GUI.TextField(RuleRow(7786,44),invite??"",512,inviteStyle);
        }

        // input.room-code-input (maxlength 2048, the source's placeholder): a code is kept in capitals as typed, a pasted
        // link (anything with a ':') verbatim, since JoinInvite parses it case by case. The placeholder is drawn over the
        // empty field.
        private GUIStyle joinStyle;
        private void JoinCodeField()
        {
            if(joinStyle==null){joinStyle=new GUIStyle(GUI.skin.textField){fontSize=22,alignment=TextAnchor.MiddleLeft,font=U.MonoFont,clipping=TextClipping.Clip,wordWrap=false};joinStyle.padding=new RectOffset(15,15,10,10);}
            var field=RuleRow(7498,46);string typed=GUI.TextField(field,joinCode??"",2048,joinStyle);
            if(typed!=joinCode)joinCode=typed.IndexOf(':')>=0?typed:typed.ToUpperInvariant();
            float e=GUI.color.a;bool empty=string.IsNullOrEmpty(joinCode);
            U.Defer(()=>{if(empty&&Event.current.type==EventType.Repaint)U.Text(new Rect(field.x+15,field.y,field.width-30,field.height),"CA-ABCD-2345 OR ROOM INVITE LINK",15,U.Hex(0x8b8c94,e),TextAnchor.MiddleLeft,tracking:.06f);});
        }

        // ---- Cached strings ----------------------------------------------------------------------------------------------
        private int firstToKey=-1,lobbyNoteKey=-1,draftNoteKey=-1,partyNoteKey=-1;private string firstToText,lobbyNoteText,draftNoteText,partyNoteText;
        private string FirstToText(int sets,int rounds)
        {int key=sets*1000+rounds;if(key!=firstToKey){firstToKey=key;firstToText="First to "+sets+(sets==1?" set":" sets")+". Win "+rounds+(rounds==1?" round":" rounds")+" to take a set.";}return firstToText;}
        // roomAdvanced's note: "First to N sets · N rounds per set · N bots[ · friendly fire on/off]".
        private string LobbyRulesNote(NativeRoomOptions o,int bots)
        {
            int key=((o.sets*100+o.rounds)*10+bots)*4+(o.teams?2:0)+(o.friendlyFire?1:0);
            if(key!=lobbyNoteKey){lobbyNoteKey=key;lobbyNoteText="First to "+o.sets+" set"+(o.sets==1?"":"s")+" · "+o.rounds+" round"+(o.rounds==1?"":"s")+" per set · "+bots+" bot"+(bots==1?"":"s")+(o.teams?" · friendly fire "+(o.friendlyFire?"on":"off"):"");}
            return lobbyNoteText;
        }
        // "{choices} choices per draft · {s}s to pick · All players draft | Set losers draft · Intros on | off" (main.ts:156).
        private string DraftNote(NativeRoomOptions o)
        {
            int key=(o.choices*1000+o.selectionSeconds)*4+(o.bothDraft?2:0)+(o.skipPlayerIntros?1:0);
            if(key!=draftNoteKey){draftNoteKey=key;draftNoteText=o.choices+" choices per draft · "+o.selectionSeconds+"s to pick · "+(o.bothDraft?"All players draft":"Set losers draft")+" · "+(o.skipPlayerIntros?"Intros off":"Intros on");}
            return draftNoteText;
        }
        private string PartyNote(NativeRoomOptions o,bool guest)
        {
            int bots=Mathf.Clamp(o.bots,0,3),key=bots*8+(guest?4:0)+(o.skipWaves?2:0)+(o.mirrorWeapons?1:0);
            if(key!=partyNoteKey){partyNoteKey=key;partyNoteText=bots+" party bot"+(bots==1?"":"s")+" · "+(o.skipWaves?"starts at the Siren":"opening waves on")+" · "+(o.mirrorWeapons?guest?"bots use the host's weapon":"bots use your weapon":"mixed bot loadouts");}
            return partyNoteText;
        }
    }
}
