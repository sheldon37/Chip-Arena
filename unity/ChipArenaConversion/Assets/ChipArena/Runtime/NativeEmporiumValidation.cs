#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Build-time native gate for Top Dog Emporium (docs/arenas/EMPORIUM.md) on the real arena and its host step: the wiener
        // kills in its lane as a maglev does, the bun press kills under its ram, a griddle burns a pilot standing on a live plate
        // and not one on a cold one, and a geyser launches a pilot standing in it once per burst (the vent push rule), only while
        // bursting and only in the fight.
        public void ValidateEmporiumRuntime()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Emporium runtime: "+why);}
            void Clock(double time,NativePhase phase=NativePhase.Fight)=>Match.ApplyNetwork(new NativeMatchFrame{phase=phase,time=time,fightAt=10});
            ReplicaScene=root.scene;Reset();var p=Pilots[0];
            foreach(var other in Pilots.Skip(1))other.Body.position=new Vector3(-27.4f,1.1f,0);
            void Restore(Vector3 at){p.Hp=1000;p.Stats["hp"]=1000;p.Stats["damageTaken"]=1;p.Seat.alive=p.Seat.connected=true;p.Body.isKinematic=false;p.Collider.enabled=true;p.Body.position=at;p.Body.linearVelocity=Vector3.zero;p.Knockback=Vector3.zero;p.Death=null;p.ShieldUntil=0;p.LastAttacker=null;}
            // the wiener
            var lane=Map.hazards.First(h=>h.kind=="maglev");double pass=10+lane.first+.8;Clock(pass);var ls=NativeCyberCityHazards.Evaluate(lane,StageRound,10,pass);
            bool runs=NativeCyberCityHazards.TrainBox(lane,ls,out var wc,out _);Check(ls.Phase==NativeHazardPhase.Active&&runs,"the wiener runs");
            Restore(new Vector3(wc.x,.9f,wc.z));StepAuthoredHazards();Check(p.Hp==0&&p.Death?.damageKind=="maglev"&&p.Death.hazardId==lane.id,"the wiener kills in its lane");
            Restore(new Vector3(wc.x,.9f,wc.z+lane.size.z/2+1));StepAuthoredHazards();Check(p.Hp==1000,"beside the lane is safe");
            // the bun press
            var press=Map.hazards.First(h=>h.kind=="press");double open=press.period-press.warn-press.rise-press.dwell-press.travel,slam=10+NativeFactoryHazards.ArmDelay+press.start+press.offset+open+press.warn+press.rise+.1;
            Restore(new Vector3(press.a.x,.9f,press.a.z));Clock(slam);StepAuthoredHazards();Check(p.Hp==0&&p.Death?.hazardId==press.id,"the bun press kills under its ram");
            Restore(new Vector3(press.a.x,.9f,press.a.z));Clock(slam-press.rise-press.warn-.5);StepAuthoredHazards();Check(p.Hp==1000,"the open press is safe");
            // a griddle: a live plate burns, the other pair's cold plate does not
            var hot=Map.hazards.First(h=>h.kind=="burner");double live=10+hot.first+(StageRound%2==0?hot.offset:0)+hot.warn+.05;var hz=hot.zones[0];
            Restore(new Vector3(hz.center.x,.9f,hz.center.z));for(int i=0;i<30;i++){Clock(live+i/60d);StepAuthoredHazards();}Check(p.Hp<1000&&p.Hp>1000-hot.damage*4-.01,"a live griddle plate burns ("+(1000-p.Hp)+")");
            var cold=Map.hazards.First(h=>h.kind=="burner"&&h!=hot&&Math.Sign(h.center.x)==Math.Sign(hot.center.x));var cz=cold.zones[0];
            Restore(new Vector3(cz.center.x,.9f,cz.center.z));for(int i=0;i<30;i++){Clock(live+i/60d);StepAuthoredHazards();}Check(p.Hp==1000,"the other pair is cold while this one burns");
            // a geyser: the push launch
            var g=Map.hazards.First(h=>h.kind=="vent"&&h.push>0);double quiet=g.period-g.warn-g.dwell,burst=10+NativeFactoryHazards.ArmDelay+g.start+g.offset+quiet+g.warn+.2;var at=new Vector3(g.a.x,g.a.y+.9f,g.a.z);
            Restore(at);Clock(burst);StepAuthoredHazards();Check(Math.Abs(p.Knockback.y-g.push)<1e-3,"a burst launches the pilot ("+p.Knockback.y+")");
            Restore(at);p.Body.linearVelocity=new Vector3(0,g.push,0);Clock(burst);StepAuthoredHazards();Check(p.Knockback.y==0,"a rising pilot is not launched again");
            Restore(at);Clock(burst-g.dwell-.3);StepAuthoredHazards();Check(p.Knockback.y==0,"no launch while it pumps");
            Restore(at);Clock(burst,NativePhase.Result);StepAuthoredHazards();Check(p.Knockback.y==0,"no launch outside the fight");
            Restore(at);int launches=0;for(int i=0;i<(int)(g.dwell*60);i++){Clock(burst-.2+i/60d);StepAuthoredHazards();if(p.Knockback.y>0){launches++;p.Body.linearVelocity=new Vector3(0,g.push,0);}p.Knockback=Vector3.zero;}
            Check(launches==1,"one launch per burst ("+launches+")");Check(p.Hp<1000&&1000-p.Hp<=g.damage*Math.Ceiling(g.dwell/g.tick)+.01,"a dab of damage ("+(1000-p.Hp)+")");
            Clock(0,NativePhase.Countdown);Reset();
            Debug.Log("PASS Emporium runtime: the wiener and the bun press kill, the griddles burn in turns, the geysers launch once per burst");
        }
    }
}
#endif
