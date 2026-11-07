using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena { public int ExpeditionSpectatorSeat; }
    public sealed partial class ConversionGame
    {
        private readonly Dictionary<string,int> splitWatching=new Dictionary<string,int>();
        private NativeArena splitSpectatorArena;
        private void SyncSplitSpectators()
        {
            var party=splitRun.PartySnapshot();
            foreach(var member in splitCohorts.Members.Where(m=>m.Connected&&m.Pending==null))
            {
                var state=party.members.FirstOrDefault(m=>m.account==member.Account);
                var targets=party.members.Where(m=>m.account!=member.Account&&m.alive&&m.connected&&!m.extracted&&
                    splitCohorts.Member(m.account)?.Pending==null&&SplitPilotArena(m.account)?.Pilots.Any(p=>p.Seat.id==SplitSeat(m.account)&&p.Hp>0)==true).OrderBy(m=>SplitSeat(m.account)).ToArray();
                int wanted=splitWatching.TryGetValue(member.Account,out var prior)?prior:0;
                var target=state!=null&&!state.alive&&!state.extracted?(targets.FirstOrDefault(m=>SplitSeat(m.account)==wanted)??targets.FirstOrDefault()):null;
                if(target==null)splitWatching.Remove(member.Account);else splitWatching[member.Account]=SplitSeat(target.account);
                splitCohorts.Observe(member.Account,target==null?member.Area:splitCohorts.Member(target.account).Area);
            }
        }
        private bool SetSplitSpectator(string who,int id)
        {
            var state=splitRun.PartySnapshot();var me=state.members.FirstOrDefault(m=>m.account==who);
            var target=state.members.FirstOrDefault(m=>SplitSeat(m.account)==id);
            if(me==null||me.alive||me.extracted||target==null||!target.alive||!target.connected||target.extracted||target.account==who||splitCohorts.Member(target.account)?.Pending!=null)return false;
            splitWatching[who]=id;return true;
        }
        private bool BindSplitSpectator(string area)
        {
            var own=splitCohorts.Member(account.Access.AccountId);
            if(own.Pending!=null||own.Area==own.Ticket.area)
            {
                if(splitSpectatorArena!=null){if(ReferenceEquals(arena,splitSpectatorArena))arena=null;splitSpectatorArena.Dispose();splitSpectatorArena=null;}
                return false;
            }
            var f=SplitFrame(own.Account);
            if(splitSpectatorArena==null||splitSpectatorArena.MapId!=f.map)
            {
                splitSpectatorArena?.Dispose();splitSpectatorArena=new NativeArena(content,new NativeMatch(content),f.map){Replica=true,LocalId=1,GeneratedRun=deploymentBundle};
                foreach(var a in expeditionM1Layout.areas)splitAreas.Find(a.id).SetPresented(false);
                presentation.ResetPresentation();visualClock.Reset(f.time);NativeLighting.Apply(content.Map(f.map),cameraView);
            }
            splitSpectatorArena.ApplyFrame(f,1);splitSpectatorArena.ApplyExpeditionCollision(SplitView(own.Account),expeditionM1Layout.Area(area),null);arena=splitSpectatorArena;match=arena.Match;generation=arena.Generation;
            expeditionDirector=splitSessions[own.Area].Sources.Director;return true;
        }
        private ExpeditionViewMember[] ExpeditionSpectatorCandidates()=>ExpeditionView?.members?.Where(m=>m.account!=account?.Access.AccountId&&m.alive&&m.connected&&!m.extracted&&string.IsNullOrEmpty(m.pendingArea)&&m.seat>0).OrderBy(m=>m.seat).ToArray()??Array.Empty<ExpeditionViewMember>();
        private bool SplitSpectatorControls(int step=0)
        {
            if(ExpeditionView?.areaIdentity==null||ExpeditionView.mine?.alive!=false||ExpeditionView.mine.extracted)return false;
            var candidates=ExpeditionSpectatorCandidates();
            if(arena.ExpeditionSpectatorSeat>0)spectatorId=arena.ExpeditionSpectatorSeat;
            if(step==0){if(Input.GetKeyDown(KeyCode.RightArrow))step=1;else if(Input.GetKeyDown(KeyCode.LeftArrow))step=-1;}
            if(step!=0&&candidates.Length>0)
            {
                int at=Array.FindIndex(candidates,m=>m.seat==spectatorId);int next=candidates[(Math.Max(0,at)+step+candidates.Length)%candidates.Length].seat;
                if(ExpeditionSplit)SetSplitSpectator(account.Access.AccountId,next);else SendServer(new NativeNetMessage{type="expedition-spectate",id=next});
            }
            if(!replayRunning){yaw+=Input.GetAxisRaw("Mouse X")*2*prefs.sensitivity;pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.5f*prefs.sensitivity,-70,70);}
            return true;
        }
    }
}
