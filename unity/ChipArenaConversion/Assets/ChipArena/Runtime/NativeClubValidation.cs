#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Substation 128's native gate (docs/arenas/CLUB.md): the authored hazards through the real authority step, in Unity.
        // The bass drop's vent damages and shoves along its axis (NativeStageHazards.VentShove), the laser wall kills a standing
        // pilot and passes under a jumping one, the charged floor ticks only on its live section, nothing harms in the arm delay.
        public void ValidateClubRuntime()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Club runtime: "+why);}
            const double fight=10;void Clock(double time)=>Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=time,fightAt=fight,round=1,set=1});
            ReplicaScene=root.scene;Reset();var victim=Pilots[0];
            foreach(var other in Pilots.Skip(1))other.Body.position=new Vector3(0,1.1f,-17);
            void Restore(Vector3 at)
            {victim.Hp=1000;victim.Stats["hp"]=1000;victim.Stats["damageTaken"]=1;victim.Stats["knockbackTaken"]=1;victim.Seat.alive=true;victim.Seat.connected=true;victim.Body.isKinematic=false;victim.Collider.enabled=true;victim.Body.position=at;victim.ShieldUntil=0;victim.Death=null;victim.LastAttacker=null;victim.Knockback=Vector3.zero;victim.Kernel=null;victim.Roll=null;victim.RollEnd=0;Events.Clear();}
            void Run(double from,double seconds){for(int i=0;i<=(int)Math.Round(seconds*60);i++){Clock(from+i/60d);StepAuthoredHazards();}}
            // the drop: three ticks of its damage and three shoves of its push along -z
            var drop=Map.hazards.Single(h=>h.id=="club-drop-w");double burst=fight+NativeFactoryHazards.ArmDelay+drop.start+drop.offset+drop.period-drop.dwell;
            Restore(new Vector3(drop.a.x,.85f,drop.a.z));Run(burst-.05,drop.dwell+.2);
            Check(Math.Abs(victim.Hp-(1000-drop.damage*3))<.01f,"the drop's three ticks ("+victim.Hp+")");
            Check(Math.Abs(victim.Knockback.z+drop.push*3)<.01f&&Math.Abs(victim.Knockback.x)<.001f&&Math.Abs(victim.Knockback.y)<.001f,"the drop shoves along its axis ("+victim.Knockback+")");
            Restore(new Vector3(drop.a.x+3.5f,.85f,drop.a.z));Run(burst-.05,drop.dwell+.2);Check(victim.Hp==1000&&victim.Knockback==Vector3.zero,"beside the stack, untouched");
            // the laser wall: a standing pilot dies (maglev, its id), a jump clears it
            var laser=Map.hazards.Single(h=>h.kind=="maglev");double pass=fight+laser.first;
            Restore(new Vector3(0,.85f,0));Run(pass-.05,(laser.size.x+laser.length)/laser.speed+.2);
            Check(victim.Hp==0&&victim.Death!=null&&victim.Death.damageKind=="maglev"&&victim.Death.hazardId==laser.id,"the laser wall kills a standing pilot");
            var frame=NativeFrameCodec.Clone(NetworkFrame());Check(frame.Valid(Content),"the laser's death record survives the wire");
            Restore(new Vector3(0,1.75f,0));Run(pass-.05,(laser.size.x+laser.length)/laser.speed+.2);Check(victim.Hp==1000,"a jump clears the laser wall (feet 0.9 m)");
            // the charged floor: the + group's north section is live; its 8 ticks there, nothing on the X centre beside it
            var north=Map.hazards.Single(h=>h.id=="club-live-c-n");double live=fight+north.first+north.period-north.live-1;
            Restore(new Vector3(north.center.x,.85f,north.center.z));Run(live-.05,north.live+.1);Check(Math.Abs(victim.Hp-(1000-north.damage*8))<.01f,"eight live ticks ("+victim.Hp+")");
            var centre=Map.hazards.Single(h=>h.id=="club-live-c-c");Restore(new Vector3(centre.center.x,.85f,centre.center.z));Run(live-.05,north.live+.1);Check(victim.Hp==1000,"the X centre stays safe while the + is live");
            // the arm delay: nothing anywhere for the fight's first 4 s
            foreach(var h in Map.hazards){var at=h.kind=="vent"?h.a+new V3(0,.85f,0):new V3(h.center.x,.85f,h.center.z);Restore(Vec(at));Run(fight,3.98);Check(victim.Hp==1000,"arm delay "+h.id);}
            Reset();Debug.Log("PASS Club runtime: the drop's damage and shove, the laser wall's kill and jump, the live floor's ticks, the arm delay");
        }
    }
}
#endif
