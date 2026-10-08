using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private uint roomSeed=NativeMatch.NewSeed();private string lastLobbyOptions="";private NativeRoomOptions receivedOptions;
        public IReadOnlyList<NativeSeatFrame> LobbySeats=>lobbySeats;
        public NativeRoomOptions RoomOptions=>Hosting||!Online?CaptureRoomOptions():receivedOptions;
        private string PreviewMapId=>NativeLobbyRules.PreviewMap(prefs.map,prefs.pve);
        private string SelectMatchMap()
        {var maps=content.maps.Where(m=>m.id!="siren").ToArray();return prefs.pve?"siren":prefs.map=="random"?maps[(int)(NativeMatch.NewSeed()%(uint)maps.Length)].id:prefs.map;}
        private NativeRoomOptions CaptureRoomOptions()=>new NativeRoomOptions{map=prefs.map,cooperative=prefs.pve,teams=prefs.teams,friendlyFire=prefs.friendlyFire,bots=prefs.bots,rounds=prefs.rounds,sets=prefs.sets,choices=prefs.choices,selectionSeconds=prefs.selectionSeconds,skipBotDrafts=prefs.skipBotDrafts,bothDraft=prefs.bothDraft,skipPlayerIntros=prefs.skipPlayerIntros,randomizeMap=prefs.randomizeMap,mirrorWeapons=prefs.mirrorWeapons,stationary=prefs.stationary,skipWaves=prefs.skipWaves,botDifficulty=prefs.botDifficulty,botOptions=prefs.botOptions??Array.Empty<NativeBotOption>(),hostTeam=prefs.team,autoStart=prefs.autoStart};
        public void SetRoomOptions(NativeRoomOptions options)
        {
            if(options==null||server!=null||arena!=null)return;options.Normalize(content);prefs.map=options.map;prefs.pve=options.cooperative;prefs.teams=options.teams;prefs.friendlyFire=options.friendlyFire;prefs.bots=options.bots;prefs.rounds=options.rounds;prefs.sets=options.sets;prefs.choices=options.choices;prefs.selectionSeconds=options.selectionSeconds;prefs.skipBotDrafts=options.skipBotDrafts;prefs.bothDraft=options.bothDraft;prefs.skipPlayerIntros=options.skipPlayerIntros;prefs.randomizeMap=options.randomizeMap;prefs.mirrorWeapons=options.mirrorWeapons;prefs.stationary=options.stationary;prefs.skipWaves=options.skipWaves;prefs.botDifficulty=options.botDifficulty;prefs.botOptions=options.botOptions;prefs.team=options.hostTeam;prefs.autoStart=options.autoStart;LobbyBroadcast();
        }
        public void GuestReady(bool ready)
        {if(server==null||arena!=null)return;networkReady=ready;SendServer(new NativeNetMessage{type="ready",ready=ready,profile=Profile(),token=account?.Token});}
        public void ChangeTeam(int team)
        {team=Math.Max(0,Math.Min(7,team));if(arena!=null)return;if(server!=null)SendServer(new NativeNetMessage{type="team",team=team});else{prefs.team=team;LobbyBroadcast();}}
        private NativeSeatFrame LobbySeat(NativeSeat s,bool ready)=>new NativeSeatFrame{id=s.id,name=s.name,spec=s.spec,team=s.team,connected=s.connected,ready=ready,bot=s.bot,difficulty=s.difficulty,bodyColor=s.bodyColor,accentColor=s.accentColor,hat=s.hat,killEffect=s.killEffect,intro=s.intro,introText=s.introText};
        private NativeSeat[] LobbyBots(int humans)
        {var options=CaptureRoomOptions();options.Normalize(content);return Enumerable.Range(0,Math.Max(0,Math.Min(options.bots,(prefs.pve?4:8)-humans))).Select(i=>NativeLobbyRules.Bot(content,options,i,prefs.weapon,roomSeed)).ToArray();}
        private void LobbyTick()
        {
            if(!Hosting||arena!=null)return;
            string options=JsonUtility.ToJson(CaptureRoomOptions());if(options!=lastLobbyOptions){lastLobbyOptions=options;LobbyBroadcast();}
            var guests=peers.Where(p=>p.Seat!=null&&p.Connection?.Closed==false).ToArray();
            if(!dedicated&&prefs.autoStart&&guests.Length>0&&guests.All(p=>p.Ready))StartMatch();
        }
    }
}
