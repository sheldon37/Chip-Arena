#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateWardSimulation()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Ward native simulation: "+why);}
            // Keep the same round used to calculate alternating hazard schedules.
            void Clock(double now)=>Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=now,fightAt=10,round=Match.Round,set=Match.Set});
            ReplicaScene=root.scene;Reset();var p=Local;var connected=Pilots.ToDictionary(q=>q.Seat.id,q=>q.Seat.connected);
            try
            {
                foreach(var other in Pilots.Where(q=>q!=p)){other.Seat.connected=false;other.Collider.enabled=false;}
                void Place(Vector3 at)
                {
                    p.Hp=1000;p.Stats["hp"]=1000;p.Stats["damageTaken"]=.5;p.Seat.connected=p.Seat.alive=true;
                    p.Collider.enabled=true;p.Body.isKinematic=false;p.Body.position=at;p.Body.linearVelocity=p.Knockback=Vector3.zero;
                    p.ShieldUntil=0;p.Death=null;p.Kernel=null;p.Roll=null;p.RollEnd=0;Physics.SyncTransforms();
                }
                foreach(var h in Map.hazards.Where(h=>h.kind=="burner"||h.kind=="gas"))
                {
                    double live=10+h.first+(StageRound%2==0?h.offset:0)+h.warn;
                    var area=h.kind=="burner"?h.zones[0]:new MapVolume{center=h.center,size=h.size};
                    Place(Vec(new V3(area.center.x,area.center.y-area.size.y/2+.9f,area.center.z)));
                    for(int i=0;i<(int)(h.live*60);i++){Clock(live+i/60d);StepAuthoredHazards();}
                    float expected=1000-h.damage*h.live/h.tick*.5f;
                    Check(Math.Abs(p.Hp-expected)<.02,h.id+" mitigated tick count: expected HP "+expected+", actual "+p.Hp+", stage round "+StageRound);
                    float hp=p.Hp;Clock(live+h.live+.2);StepAuthoredHazards();Check(p.Hp==hp,h.id+" clear harmless");
                    if(h.kind=="burner"){Place(Vec(h.center));Clock(live);StepAuthoredHazards();Check(p.Hp==1000,h.id+" pocket harmless");}
                }
                var shutter=Map.hazards.First(h=>h.kind=="shutter");double closes=10+shutter.first+(StageRound%2==0?shutter.offset:0)+shutter.warn;
                Place(Vec(new V3(shutter.center.x,shutter.center.y-shutter.size.y/2+.9f,shutter.center.z)));
                Clock(closes);PoseStageLoads(Match.Time,true);
                Check(!stageLoads[shutter.id].enabled,"occupied shutter interlocks");
                for(int i=1;i<90;i++){Clock(closes+i/60d);PoseStageLoads(Match.Time,true);Physics.SyncTransforms();}
                Check(p.Hp==1000,"shutter does no damage");
                Check(!NativeStageHazards.Touch(Vec(p.Body.position),shutter.center,shutter.size)&&stageLoads[shutter.id].enabled,"shutter pushes clear before solid");
                var entry=StageContent.entries.First(e=>e.kind=="breach");Clock(100);BeginWardBreach(entry,Time+NativeBreach.Knock+NativeBreach.Through);
                var record=wardBreaches.Single();var panel=StaticBlocks.Single(b=>b.Definition.id==entry.block).GetComponent<Collider>();
                Place(Vec(entry.@exit.Position(.9f)));Clock(record.breakAt);PoseWardBreaches();Check(!panel.enabled,"panel breaks");
                Place(Vec(entry.center));Clock(record.restoreAt);float previous=record.restoreAt;PoseWardBreaches();Check(record.restoreAt>previous&&!panel.enabled,"occupied restore postponed");
                Place(Vec(entry.@exit.Position(.9f)));Clock(NativeBreach.SolidAt(record.restoreAt));PoseWardBreaches();Check(panel.enabled,"panel solid after rebuild");
                var frame=NativeFrameCodec.Clone(NetworkFrame());
                frame.pve=new NativePveFrame{breaches=wardBreaches.Select(b=>b.ViewCopy()).ToArray(),rows=Array.Empty<NativePveRowFrame>(),pickups=Array.Empty<NativePickupFrame>()};
                frame=NativeFrameCodec.Clone(frame);Check(frame.Valid(Content)&&frame.pve.breaches[0].restoreAt==record.restoreAt,"real JsonUtility breach frame");
                frame.pve.breaches[0].restoreAt=float.NaN;Check(!frame.Valid(Content),"malformed breach rejected");
                foreach(string kind in new[]{"burner","gas"})
                {frame=NativeFrameCodec.Clone(NetworkFrame());frame.pilots[0].death=new NativeDeath{damageKind=kind};frame=NativeFrameCodec.Clone(frame);Check(frame.Valid(Content),"wire death "+kind);}
                Debug.Log("PASS Ward native hazard ticks, interlock, breach overlap/solidity and JsonUtility");
            }
            finally{foreach(var q in Pilots)if(connected.TryGetValue(q.Seat.id,out bool value))q.Seat.connected=value;Reset();}
        }
    }
}
#endif
