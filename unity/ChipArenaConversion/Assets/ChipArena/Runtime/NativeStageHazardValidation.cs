#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Build-time native gate. It must execute in Unity to count as native verification.
        public void ValidateStageHazardRuntime()
        {
            void Check(bool value,string reason){if(!value)throw new Exception("Stage hazard runtime "+MapId+": "+reason);}
            void Clock(double time,NativePhase phase=NativePhase.Fight)=>Match.ApplyNetwork(new NativeMatchFrame{phase=phase,time=time,fightAt=10});
            ReplicaScene=root.scene;Reset();var victim=Pilots[0];var attacker=Pilots[1];
            void Restore(Vector3 at)
            {victim.Hp=1000;victim.Stats["hp"]=1000;victim.Stats["damageTaken"]=1;victim.Seat.alive=true;victim.Seat.connected=true;victim.Body.isKinematic=false;victim.Collider.enabled=true;victim.Body.position=at;victim.ShieldUntil=0;victim.Death=null;victim.LastAttacker=null;victim.Knockback=Vector3.zero;victim.Kernel=null;victim.Roll=null;victim.RollEnd=0;victim.Wounds.Clear();victim.Wounds.Add(new NativeWound{id=1,part=CapturedRig.Spine,at=0,radius=.03f,kind="puncture",trauma="bullet"});Events.Clear();}
            foreach(var other in Pilots.Skip(1))other.Body.position=new Vector3(35,1.1f,25);
            if(MapId=="cybercity")
            {
                var h=Map.hazards.Single(x=>x.kind=="maglev");Clock(29);var s=NativeCyberCityHazards.Evaluate(h,StageRound,10,29);Check(NativeCyberCityHazards.TrainBox(h,s,out var c,out _),"train live");
                Restore(Vec(c));victim.Stats["damageTaken"]=0;victim.ShieldUntil=100;victim.LastAttacker=attacker;victim.LastAttackerAt=22.1f;StepAuthoredHazards();Check(victim.Hp==0&&victim.Death.damageKind=="maglev"&&victim.Death.owner==attacker.Seat.id,"instant kill, immunity bypass and 7 second credit");
                Check(victim.Death.wound==null&&victim.Wounds.Count==1&&victim.Death.hazardId==h.id,"train clears fatal wound only and records hazard");
                var deathFrame=NativeFrameCodec.Clone(NetworkFrame());
                Check(deathFrame.Valid(Content)&&deathFrame.pilots[0].death.hazardId==h.id&&deathFrame.pilots[0].death.wound==null,"hazard id and absent wound survive JSON");
                var grid=Map.hazards.First(x=>x.kind=="grid");double live=10+grid.first+(StageRound%2==0?grid.offset:0)+grid.period-grid.live-1;
                Restore(new Vector3(grid.center.x,.85f,grid.center.z));victim.Stats["damageTaken"]=.5;
                for(int i=0;i<120;i++){Clock(live+i/60d);StepAuthoredHazards();}Check(Math.Abs(victim.Hp-948)<.01,"eight mitigated grid ticks");
                Restore(new Vector3(grid.center.x,1.96f,grid.center.z));Clock(live);StepAuthoredHazards();Check(victim.Hp==1000,"jump above grid");
                Restore(Vec(c));Clock(29,NativePhase.Result);StepAuthoredHazards();Check(victim.Hp==1000,"no Result damage");
            }
            else
            {
                var h=Map.hazards.First(x=>x.kind=="saw-slot");double clock=h.start+h.offset+h.warn+h.rise+1;Clock(14+clock);var s=NativeFactoryHazards.State(h,StageRound,clock);Restore(new Vector3(s.BoxCenter.x,.85f,s.BoxCenter.z));Clock(14+clock);victim.Stats["damageTaken"]=0;victim.ShieldUntil=100;victim.LastAttacker=attacker;victim.LastAttackerAt=Time-6.01f;StepAuthoredHazards();Check(victim.Hp==0&&victim.Death.damageKind=="hazard"&&victim.Death.owner==0,"instant saw and expired 6 second credit");
                Check(victim.Death.wound==null&&victim.Wounds.Count==1&&victim.Death.hazardId==h.id,"factory clears fatal wound only and records hazard");
                Restore(new Vector3(s.BoxCenter.x,.85f,s.BoxCenter.z+.6f));StepAuthoredHazards();Check(victim.Hp==1000,"saw sidestep");
                Restore(new Vector3(s.BoxCenter.x,.85f,s.BoxCenter.z));Clock(13.999);StepAuthoredHazards();Check(victim.Hp==1000,"arm grace");
                var crane=Map.hazards.Single(x=>x.kind=="crane");double impact=14+crane.start+crane.offset+crane.travel+crane.warn+crane.rise;Clock(impact+.1);PoseStageLoads(Match.Time);Check(stageLoads[crane.id].enabled,"landed collider");Clock(impact+crane.dwell+.1);PoseStageLoads(Match.Time);Check(stageLoads[crane.id].enabled,"regrip collider");Clock(impact+crane.dwell+1.51);PoseStageLoads(Match.Time);Check(!stageLoads[crane.id].enabled,"hoist collider removed");
            }
            Clock(30);Restore(new Vector3(35,1.1f,25));var frame=NativeFrameCodec.Clone(NetworkFrame());Check(frame.Valid(Content),"real JsonUtility frame");
            Check(frame.pilots[0].death==null&&!frame.pilots[0].hasDeath,"absent death survives JSON");
            foreach(var kind in new[]{"maglev","grid","hazard"})
            {
                frame.pilots[0].death=new NativeDeath{damageKind=kind};
                // Clone captures optional-record presence before JsonUtility, just like the wire sender.
                frame=NativeFrameCodec.Clone(frame);
                Check(frame.Valid(Content)&&frame.pilots[0].hasDeath&&frame.pilots[0].death?.damageKind==kind,"preserve and accept wire kind "+kind);
            }
            frame.pilots[0].death=new NativeDeath{damageKind="unknown"};frame=NativeFrameCodec.Clone(frame);
            Check(frame.pilots[0].hasDeath&&frame.pilots[0].death?.damageKind=="unknown","unknown kind reaches validator");
            Check(!frame.Valid(Content),"refuse unknown wire kind");
            Reset();Debug.Log("PASS native stage hazard damage, phase gating, credit, collision lifetime and JsonUtility: "+MapId);
        }
    }
}
#endif
