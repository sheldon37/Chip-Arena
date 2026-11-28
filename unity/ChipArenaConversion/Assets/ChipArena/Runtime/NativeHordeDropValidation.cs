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
            void Check(bool ok,string why){if(!ok)throw new Exception("Native drop entry gate: "+why);}
            bool Near(float a,float b)=>Math.Abs(a-b)<.002f;
            foreach(var n in Minions.ToArray())RemoveMinion(n,false);
            Pve.Holding=false;Pve.Rate=0;Pve.DropCursor=0;pveEntries.Clear();
            var entries=Pve.Content.entries;var drop=entries.First(e=>NativeHordeDrop.Is(e)&&!e.hulk);
            // Isolate from map walls along Z, but keep the radius-probe axis at zero.
            // At X=1000, (1000f+2.4f)-1000f is 2.400024f: outside the intended edge.
            var fixtureDrop=new PveEntry{e=drop.e,id=drop.id,kind="drop",height=drop.height,from=new PvePoint{x=0,z=1000},@exit=new PvePoint{x=0,z=1000}};
            Pve.Content.entries=(PveEntry[])entries.Clone();Pve.Content.entries[drop.e]=fixtureDrop;drop=fixtureDrop;
            var pilot=Members().First();var landing=Vec(drop.@exit.Position());
            void Pilot(Vector3 delta,bool block=false)
            {
                foreach(var p in Pilots.Where(p=>p!=pilot&&!p.Seat.AuthorityBoss)){p.Body.position=landing+Vector3.one*30;p.Collider.enabled=false;}
                pilot.Seat.chips.Clear();pilot.Seat.spec="pistol";pilot.Weapon=Content.Weapon("pistol");pilot.Stats=NativeContentRules.Stats(Content,"pistol",pilot.Seat.chips);
                pilot.Seat.alive=pilot.Seat.connected=true;pilot.Hp=100;pilot.Down=null;pilot.Juggle=null;pilot.Roll=null;pilot.ScareBy=null;pilot.Kernel=null;
                pilot.ShieldUntil=pilot.RollEnd=0;pilot.BlockEnd=block?Time+1:0;pilot.BlockProcUsed=false;pilot.Knockback=Vector3.zero;pilot.Body.position=landing+delta;pilot.Collider.enabled=true;Physics.SyncTransforms();
            }
            try
            {
                // The drop entry is an emergence from the floor on the same clock (Core/NativeHordeEmerge; owner 2026-11-21: the drop-in
                // and its landing slam removed): under the floor with no collider until the torso is out (ExposedAt = the old LandAt),
                // then the capsule rises with the drawn body, hittable, standing at ActiveAt; nothing near it is struck or shoved.
                Pilot(Vector3.up*.9f+Vector3.right*4);
                var brute=SpawnPveAdd("brute",true,drop.e);var a=brute.Pve;float height=brute.Collider.height,standing=height/2+.025f;
                Check(a.Entry==drop&&Near(a.ActiveAt-a.SpawnAt,NativeHordeDrop.Duration(drop.height))&&Near(a.ExposedAt,NativeHordeDrop.LandAt(a.ActiveAt)),"data selection/timeline unchanged");
                brute.Body.position+=Vector3.one*3;StepPveHorde();
                Check(brute.Body.isKinematic&&!brute.Collider.enabled&&Vector3.Distance(brute.Body.position,landing+Vector3.up*(standing-height))<.001f,"held under the authored exit, no blocking collider");
                Check(!Enemy(pilot,brute)&&!CanHit(pilot,brute)&&HitMinion(brute,pilot,10,Vector3.forward,0)==0,"under the floor: untargetable and immune");
                var other=SpawnPveAdd("shambler",true);other.Pve.ActiveAt=other.Pve.ExposedAt=Time-1;other.Body.position=landing+new Vector3(0,.9f,1);other.Collider.enabled=true;other.Pve.StaggerAt=Time+100;Physics.SyncTransforms();
                // The torso comes out at the old touchdown: no damage, no knockback, no event, no shove, whoever stands at the old slam's radius.
                foreach(float x in new[]{0f,1.2f,2.4f})
                {
                    a.ActiveAt=Time+.899f;a.ExposedAt=NativeHordeDrop.LandAt(a.ActiveAt);a.SpawnAt=a.ActiveAt-NativeHordeDrop.Duration(drop.height);
                    Pilot(new Vector3(x,.9f,0));Events.Clear();other.Pve.External=Vector3.zero;StepPveHorde();
                    Check(pilot.Hp==100&&pilot.Knockback==Vector3.zero&&!Events.Any(e=>e.Detail=="minion-drop"||e.Target==pilot.Seat.id)&&other.Pve.External==Vector3.zero,"no slam at "+x+" m: hp="+pilot.Hp+" knockback="+pilot.Knockback);
                }
                RemoveMinion(other,false);
                float sink=brute.Body.position.y-(landing.y+standing);
                Check(a.DropLanded&&brute.Body.isKinematic&&brute.Collider.enabled&&Enemy(pilot,brute)&&CanHit(pilot,brute),"torso out: targetable, still held");
                Check(sink<-.25f*height&&sink>-.55f*height&&Near(sink,NativeHordeEmerge.Sink(Time-a.SpawnAt,a.ExposedAt-a.SpawnAt,a.ActiveAt-a.SpawnAt,height)),"the capsule rises with the drawn body ("+sink+")");
                Check(a.Struck&&HitMinion(brute,pilot,1,Vector3.forward,0)>0,"hittable while it rises; it does not attack");
                a.ActiveAt=Time+.3f;a.ExposedAt=Time-.6f;StepPveHorde();Check(brute.Body.position.y>landing.y+standing+sink+.05f&&brute.Body.position.y<landing.y+standing-.05f&&brute.Body.isKinematic,"still rising");
                a.ActiveAt=Time;StepPveHorde();Check(!brute.Body.isKinematic&&brute.Collider.enabled,"risen at ActiveAt: physical");
                Check(pilot.Hp>=99&&pilot.Knockback==Vector3.zero,"nothing struck on the way out");
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
                    Check(replica.Pve.Entry.e==drop.e&&replica.Collider.enabled&&Near(replica.Pve.ExposedAt,NativeHordeDrop.LandAt(a.ActiveAt)),"replica derives the torso-out time from existing clocks/index");
                    var under=frame.minions.Single(n=>n.id==brute.Id);under.activeAt=(float)frame.time+2;guest.ApplyFrame(frame,LocalId);
                    Check(!guest.Minions.Single(n=>n.Id==brute.Id).Collider.enabled,"replica under the floor does not block");
                }
                finally{guest.Dispose();}
                RemoveMinion(brute,false);Pve.Content.entries=entries.Where(e=>!NativeHordeDrop.Is(e)).ToArray();
                var fallback=SpawnPveAdd("brute",true);Check(fallback!=null&&!NativeHordeDrop.Is(fallback.Pve.Entry),"legacy fallback with no drops");RemoveMinion(fallback,false);
                Debug.Log("PASS native drop entry emergence: under-floor hold, torso-out targeting, rising capsule, no slam (damage/knockback/event/add shove), smash shove/block, network and legacy fallback");
            }
            finally{Pve.Content.entries=entries;}
        }
    }
}
#endif
