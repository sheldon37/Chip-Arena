#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Real authority paths and isolated-scene PhysX LOS, run by NativeBuildChecks on Ward Zero.
        public void ValidateHordeDrops()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Native brute drop gate: "+why);}
            bool Near(float a,float b)=>Math.Abs(a-b)<.002f;
            foreach(var n in Minions.ToArray())RemoveMinion(n,false);
            Pve.Holding=false;Pve.Rate=0;Pve.DropCursor=0;pveEntries.Clear();
            var entries=Pve.Content.entries;var drop=entries.Single(NativeHordeDrop.Is);
            var pilot=Members().First();var landing=Vec(drop.@exit.Position());
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Brute slam LOS gate";wall.transform.SetParent(root.transform);wall.layer=PropLayer;wall.SetActive(false);
            void Pilot(Vector3 delta,bool block=false)
            {
                foreach(var p in Pilots.Where(p=>p!=pilot&&!p.Seat.AuthorityBoss)){p.Body.position=landing+Vector3.one*30;p.Collider.enabled=false;}
                pilot.Seat.chips.Clear();pilot.Seat.spec="pistol";pilot.Weapon=Content.Weapon("pistol");pilot.Stats=NativeContentRules.Stats(Content,"pistol",pilot.Seat.chips);
                pilot.Seat.alive=pilot.Seat.connected=true;pilot.Hp=100;pilot.Down=null;pilot.Juggle=null;pilot.Roll=null;pilot.ScareBy=null;pilot.Kernel=null;
                pilot.ShieldUntil=pilot.RollEnd=0;pilot.BlockEnd=block?Time+1:0;pilot.BlockProcUsed=false;pilot.Knockback=Vector3.zero;pilot.Body.position=landing+delta;pilot.Collider.enabled=true;Physics.SyncTransforms();
            }
            try
            {
                Pilot(Vector3.up*.9f+Vector3.right*4);
                var brute=SpawnPveAdd("brute",true);var a=brute.Pve;
                Check(a.Entry==drop&&Near(a.ActiveAt-a.SpawnAt,NativeHordeDrop.Duration(drop.height)),"data selection/timeline");
                brute.Body.position+=Vector3.one*3;StepPveHorde();
                Check(brute.Body.isKinematic&&!brute.Collider.enabled&&Vector3.Distance(brute.Body.position,landing+Vector3.up*.9f)<.001f,"held at authored exit, no blocking collider");
                Check(!Enemy(pilot,brute)&&!CanHit(pilot,brute)&&HitMinion(brute,pilot,10,Vector3.forward,0)==0,"held add untargetable and immune");
                // Place the timeline boundary at the current clock without running unrelated match AI.
                a.ActiveAt=Time+.899f;a.ExposedAt=NativeHordeDrop.LandAt(a.ActiveAt);a.SpawnAt=a.ActiveAt-NativeHordeDrop.Duration(drop.height);
                Pilot(new Vector3(1.2f,.9f,0));Events.Clear();StepPveHorde();
                Check(a.DropLanded&&!brute.Body.isKinematic&&brute.Collider.enabled&&Enemy(pilot,brute)&&CanHit(pilot,brute),"touchdown enables physics and targeting");
                Check(Near(pilot.Hp,92.5f)&&Near(pilot.Knockback.x,380f/65)&&Near(pilot.Knockback.y,2.5f),"actual landing damage/falloff and pop");
                Check(a.Struck&&Events.Count(e=>e.Detail=="minion-drop")==1,"recovery does not attack; one landing event");
                StepPveHorde();Check(Near(pilot.Hp,92.5f)&&Events.Count(e=>e.Detail=="minion-drop")==1,"slam is once only");
                Check(HitMinion(brute,pilot,1,Vector3.forward,0)>0,"hittable during recovery");
                foreach(var sample in new[]{(0f,10f),(2.4f,5f),(2.41f,0f)})
                {Pilot(new Vector3(sample.Item1,.9f,0));PveDropSlam(brute,landing);Check(Near(pilot.Hp,100-sample.Item2),"actual radius/falloff "+sample.Item1);}
                Pilot(new Vector3(1,2.41f,0));PveDropSlam(brute,landing);Check(pilot.Hp==100,"strict vertical exclusion");
                Pilot(new Vector3(1,.9f,0),true);Events.Clear();PveDropSlam(brute,landing);
                Check(pilot.Hp==100&&Near(pilot.Knockback.x,190f/65)&&Near(pilot.Knockback.y,1.25f)&&Events.Any(e=>e.Detail=="clash"),"block clashes with half shove");
                Pilot(new Vector3(2,.9f,0));wall.transform.position=landing+new Vector3(1,1,0);wall.transform.localScale=new Vector3(.2f,3,3);wall.SetActive(true);Physics.SyncTransforms();PveDropSlam(brute,landing);
                Check(pilot.Hp==100&&pilot.Knockback==Vector3.zero,"world cover stops slam");wall.SetActive(false);Physics.SyncTransforms();
                var other=SpawnPveAdd("shambler",true);other.Pve.ActiveAt=other.Pve.ExposedAt=Time-1;other.Body.position=landing+new Vector3(0,.9f,1);other.Collider.enabled=true;Physics.SyncTransforms();
                PveDropSlam(brute,landing);Check(Near(other.Pve.External.z,NativePveParity.AddImpulse(200)),"nearby add shove");RemoveMinion(other,false);
                brute.Yaw=0;brute.Body.position=landing+Vector3.up*.9f;a.DamageScale=1;
                Pilot(new Vector3(0,.9f,1));Events.Clear();PveAddStrike(brute);
                Check(pilot.Hp<100&&Near(pilot.Knockback.z,-420f/65)&&Near(pilot.Knockback.y,2.5f)&&pilot.Down==null&&pilot.Juggle==null&&Events.Any(e=>e.Detail=="minion-slam"),"smash outward from disc, upward, no knock-down");
                Pilot(new Vector3(0,.9f,1),true);PveAddStrike(brute);Check(pilot.Hp==100&&pilot.Knockback==Vector3.zero&&Near(a.StaggerAt,Time+.6f),"smash block behavior preserved");
                Pilot(new Vector3(0,.9f,1));a.Target=pilot;a.ThinkAt=Time+1;a.StaggerAt=a.RecoverAt=0;a.ActiveAt=Time;a.Struck=true;StepPveHorde();
                Check(!a.Struck&&Near(brute.StrikeAt-Time,.43f),"attack starts after activation with unchanged .43 hit");
                var frame=NativeFrameCodec.Clone(NetworkFrame());Check(frame.Valid(Content),"drop entry survives frame validation");
                var guest=new NativeArena(Content,new NativeMatch(Content),MapId){Replica=true,ReplicaScene=root.scene};
                try
                {
                    guest.ApplyFrame(frame,LocalId);var replica=guest.Minions.Single(n=>n.Id==brute.Id);
                    Check(replica.Pve.Entry.e==drop.e&&replica.Collider.enabled&&Near(replica.Pve.ExposedAt,NativeHordeDrop.LandAt(a.ActiveAt)),"replica derives touchdown from existing clocks/index");
                    var airborne=frame.minions.Single(n=>n.id==brute.Id);airborne.activeAt=(float)frame.time+2;guest.ApplyFrame(frame,LocalId);
                    Check(!guest.Minions.Single(n=>n.Id==brute.Id).Collider.enabled,"replica airborne body does not block");
                }
                finally{guest.Dispose();}
                RemoveMinion(brute,false);Pve.Content.entries=entries.Where(e=>!NativeHordeDrop.Is(e)).ToArray();
                var fallback=SpawnPveAdd("brute",true);Check(fallback!=null&&!NativeHordeDrop.Is(fallback.Pve.Entry),"legacy fallback with no drops");RemoveMinion(fallback,false);
                Debug.Log("PASS native brute drop hold/landing, target/damage gates, recovery, radius/falloff/block/LOS/add shove, smash shove/block, network and legacy fallback");
            }
            finally{Pve.Content.entries=entries;UnityEngine.Object.DestroyImmediate(wall);}
        }
    }
}
#endif
