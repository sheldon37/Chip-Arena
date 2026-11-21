using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        [Serializable] private sealed class PartyWindow
        {
            public string utc, map, segment, role;
            public int epoch, seat, humans, visit, frames;
            public double seconds, p95Ms, worstMs, maxSimulationMs, maxPresentationMs;
            public long sentBytes;
            public bool disconnected;
        }

        private PartyWindow partyWindow;
        private readonly List<double> partyFrames = new List<double>(1024);
        private readonly Dictionary<string,int> bossVisits = new Dictionary<string,int>();
        private bool wasBossIntro;
        private long lastPartyBytes;
        private int lastPartyEpoch = -1;
        private string lastPartyMap;
        private double lobbySeconds;
        private string lastPartyRole;
        private int lastPartyVisit,lastPartySeat,lastPartySize;
        private static readonly string[] benchmarkPhases=Enum.GetNames(typeof(NativePhase));

        private void FlushPartyWindow()
        {
            if (partyWindow == null) return;
            if (partyFrames.Count > 0)
            {
                partyFrames.Sort();
                partyWindow.frames = partyFrames.Count;
                partyWindow.p95Ms = partyFrames[(int)Math.Ceiling(partyFrames.Count * .95) - 1];
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "PARTY_BENCH {0}", JsonUtility.ToJson(partyWindow));
            }
            partyWindow = null;
            partyFrames.Clear();
        }

        private void ObservePartyFrame(double frameMs, double presentationMs)
        {
            if (arena == null && lastPartyEpoch >= 0 && lobbySeconds < 2)
            {
                if (partyWindow?.segment != "return")
                {
                    FlushPartyWindow();
                    partyWindow=new PartyWindow { utc=DateTime.UtcNow.ToString("O"),map=lastPartyMap,
                        epoch=lastPartyEpoch,visit=lastPartyVisit,role=lastPartyRole,seat=lastPartySeat,
                        humans=lastPartySize,segment="return",disconnected=!InRoom };
                }
                partyFrames.Add(frameMs);partyWindow.seconds+=frameMs/1000;
                partyWindow.worstMs=Math.Max(partyWindow.worstMs,frameMs);
                lobbySeconds+=frameMs/1000;
                if(lobbySeconds>=2)FlushPartyWindow();
                return;
            }
            if (arena == null || match == null || (!InRoom && !HostLost) || previewArena != null)
            {
                FlushPartyWindow();
                wasBossIntro = false;
                lastPartyEpoch = -1;
                return;
            }
            int humans = 0;
            foreach (var seat in match.Seats) if (!seat.bot && seat.connected) humans++;
            bool intro = arena.Pve?.Holding == true && arena.Pve.Cinematic == "intro";
            string segment = HostLost ? "disconnect" : intro ? "boss-intro" :
                arena.Local?.Hp <= 0 ? "down" : arena.Pve?.Stage ?? benchmarkPhases[(int)match.Phase];
            string map = arena.MapId;
            bossVisits.TryGetValue(map, out int visit);
            bool newArena = lastPartyEpoch != networkEpoch || lastPartyMap != map;
            if (intro && (!wasBossIntro || newArena)) bossVisits[map] = ++visit;
            wasBossIntro = intro;
            lastPartyEpoch = networkEpoch;
            lastPartyMap = map;
            bool lostPeer=!newArena && humans<lastPartySize;
            lastPartyVisit=visit;lastPartySize=humans;lastPartySeat=arena.LocalId;
            lastPartyRole=arena.Replica?"guest":"host";lobbySeconds=0;
            long total = server?.SentWireBytes ?? 0;
            if (Hosting) foreach (var peer in peers) total += peer.Connection?.SentWireBytes ?? 0;
            long sent = newArena ? 0 : Math.Max(0, total - lastPartyBytes);
            lastPartyBytes = total;
            if (partyWindow != null && (newArena || partyWindow.segment != segment || partyWindow.humans != humans ||
                partyWindow.seconds >= 5 || partyFrames.Count >= 4096)) FlushPartyWindow();
            if (partyWindow == null) partyWindow = new PartyWindow { utc = DateTime.UtcNow.ToString("O"),
                map = map, epoch = networkEpoch, seat = arena.LocalId, humans = humans, visit = visit,
                segment = segment, role = arena.Replica ? "guest" : "host", disconnected = HostLost || lostPeer };
            partyFrames.Add(frameMs);
            partyWindow.seconds += frameMs / 1000;
            partyWindow.worstMs = Math.Max(partyWindow.worstMs, frameMs);
            partyWindow.maxSimulationMs = Math.Max(partyWindow.maxSimulationMs, hitchFixed);
            partyWindow.maxPresentationMs = Math.Max(partyWindow.maxPresentationMs, presentationMs);
            partyWindow.sentBytes += sent;
        }
    }
}
