using System;
using System.Linq;
using UnityEngine;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        internal static System.Numerics.Vector3 PodFeet(NativePilot p){var v=p.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight;return new System.Numerics.Vector3(v.x,v.y,v.z);}
        public NativePodRecord[] PodRecords=Array.Empty<NativePodRecord>();
        internal Func<NativePilot,string> BoardPod;
        public bool PodDeparting(int seat)=>PodRecords.Any(r=>r.seat==seat&&r.launchAt>=0&&!r.departed);
        internal bool PodInteraction(NativePilot pilot,out string notice)
        {
            notice=null;if(BoardPod==null)return false;var own=PodRecords.FirstOrDefault(r=>r.seat==pilot.Seat.id);
            if(own==null||!NativePodRun.InReach(own,PodFeet(pilot)))return false;
            notice=BoardPod?.Invoke(pilot)??"Pod extraction unavailable.";return true;
        }
    }
    public sealed partial class ConversionGame
    {
        private NativePodRun splitPods;
        private void PrepareSplitPods(NativeArena hub)
        {
            splitPods=deploymentBundle==null?null:new NativePodRun(splitSeats.Select((s,i)=>
            {
                var p=hub.Pilots.First(p=>p.Seat.id==s.id);var feet=p.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight;
                return new NativePodRecord{seat=s.id,order=i,name=s.name,bodyColor=s.bodyColor,accentColor=s.accentColor,look=NativePodRun.Look(s.pod),x=feet.x,y=feet.y,z=feet.z,yaw=p.Yaw};
            }).ToArray());
            hub.BoardPod=p=>
            {
                if(SplitCommandBlock!=null)return SplitCommandBlock;
                if(!PodCanExtract(p.Seat.id))return "Clear an instance and return safely to extract. The host must remain while other pilots are in the run.";
                if(!splitPods.Board(p.Seat.id,NativeArena.PodFeet(p),hub.Time,true))return "Pod is not ready.";
                splitRun.VoteExtraction(p.Seat.access.AccountId,false);
                var record=splitPods.Snapshot().First(r=>r.seat==p.Seat.id);hub.PlaceTransferredPilot(p,new Vector3(record.x,record.y+NativeLocomotionRules.HalfHeight,record.z),record.yaw);
                hub.HoldTransferPilot(p,true);UpdatePodRecords();return "Pod departing…";
            };
        }
        private bool PodCanExtract(int id)
        {
            var seat=splitSeats?.FirstOrDefault(s=>s.id==id);string who=seat?.access?.AccountId;
            var member=who==null?null:splitCohorts.Member(who);var party=splitRun.PartySnapshot();
            return member!=null&&member.Area==expeditionM1Layout.world&&member.Ready&&member.Pending==null&&member.Connected&&
                splitRun.CanExtract(who)&&!(who==party.host&&party.members.Any(m=>m.account!=who&&m.connected&&!m.extracted));
        }
        private bool PodDeparting(string who)=>splitPods?.Departing(SplitSeat(who))==true;
        private void UpdatePodRecords()
        {
            if(splitPods==null)return;var records=splitPods.Snapshot();
            foreach(var area in expeditionM1Layout.areas)splitAreas.Find(area.id).Arena.PodRecords=records;
        }
        // After the run-wide load barrier, step only the match countdown. Economy, transfers and encounters remain paused.
        private bool StepPodCountdown()
        {
            if(splitPods==null)return false;
            var hub=splitAreas.Find(expeditionM1Layout.world);var m=hub.Arena.Match;
            if(!splitPods.Started){splitPods.Start(m);UpdatePodRecords();}
            if(m.Phase!=NativePhase.Countdown)return false;
            hub.Input=new InputFrame();hub.Step(++splitTick);
            foreach(var area in expeditionM1Layout.areas)splitAreas.Find(area.id).Arena.Match.SynchronizeExpeditionClock(splitTick/60d);
            pending=new InputFrame();ExpeditionView=SplitView(account.Access.AccountId);return true;
        }
        private void TickPodDepartures()
        {
            if(splitPods==null)return;var hub=splitAreas.Find(expeditionM1Layout.world).Arena;
            splitPods.Tick(hub.Time,PodCanExtract,id=>
            {
                var seat=splitSeats.First(s=>s.id==id);
                var result=splitSessions[expeditionM1Layout.world].Extract(seat.access.AccountId);
                if(result.state!=ExpeditionCommandState.Pending)return false;
                hub.RestoreExpeditionMembers();return true;
            });
            UpdatePodRecords();
        }
        private void PodUsePrompt(float width,float height)
        {
            if(arena?.Local==null||arena.GeneratedRun?.World?.Map?.id!=arena.Map.id||match.Phase!=NativePhase.Fight||arena.Local.Hp<=0)return;
            var p=arena.Local;var record=arena.PodRecords.FirstOrDefault(r=>r.seat==p.Seat.id);
            if(record==null||!NativePodRun.InReach(record,NativeArena.PodFeet(p)))return;
            U.Text(new Rect(width*.5f-180,height-130,360,22),"INTERACT · Board your pod to extract",13,Color.white,TextAnchor.MiddleCenter);
        }
    }
}
