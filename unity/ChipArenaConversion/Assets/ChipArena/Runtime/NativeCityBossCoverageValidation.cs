#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateCityBossCoverage(Action<NativeGameFrame> replicaCheck=null)
        {
            string kind=NativeZoneBossRules.Archetype(MapId);if(kind!=NativeZoneBossRules.Station&&kind!=NativeZoneBossRules.Ironworks)return;
            int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("City boss coverage "+MapId+": "+why);}
            var pilot=Pilots.First(p=>!p.Seat.AuthorityBoss);var spots=new Dictionary<string,Vector3>();var seen=new HashSet<(int,int,int)>();
            var overlap=new Collider[1];int upper=0,stairs=0,doors=0;
            int Floor(string label,float x,float z,float top)
            {
                int found=0;
                foreach(var hit in NativeSceneQueries.RaycastAll(PhysicsWorld,new Ray(new Vector3(x,top,z),Vector3.down),top-(Map.bounds?.killY??-6),WorldMask,QueryTriggerInteraction.Ignore))
                {
                    if(hit.normal.y<.65f||hit.point.y>Map.bounds.ceiling-1.8f)continue;
                    var f=hit.point+Vector3.up*.04f;float radius=pilot.Collider.radius,height=pilot.Collider.height;
                    if(PhysicsWorld.OverlapCapsule(f+Vector3.up*(radius+.02f),f+Vector3.up*(height-radius),radius,overlap,WorldMask,QueryTriggerInteraction.Ignore)>0)continue;
                    found++;if(label.StartsWith("stair:"))stairs++;if(label.StartsWith("door:"))doors++;
                    if(!seen.Add((Mathf.RoundToInt(f.x*10),Mathf.RoundToInt(f.y*10),Mathf.RoundToInt(f.z*10))))continue;
                    spots[label+"/"+spots.Count]=f;if(f.y>3)upper++;
                }
                return found;
            }
            Physics.SyncTransforms();float ceiling=Map.bounds.ceiling+1;
            // Every sampled walkable level, not only the first downward hit. This includes surfaces below a balcony/roof.
            for(float x=Map.bounds.minX+.6f;x<Map.bounds.maxX;x+=4)for(float z=Map.bounds.minZ+.6f;z<Map.bounds.maxZ;z+=4)Floor("grid",x,z,ceiling);
            foreach(var n in Map.nav?.nodes??Array.Empty<MapNavNode>())Floor("nav:"+n.id,(float)n.pos.x,(float)n.pos.z,(float)n.pos.y+2);
            foreach(var ramp in Map.ramps??Array.Empty<MapRamp>())foreach(var point in new[]{ramp.low,ramp.high})
            {
                int found=0;foreach(float dx in new[]{-.6f,0,.6f})foreach(float dz in new[]{-.6f,0,.6f})found+=Floor("stair:"+ramp.id,(float)point.x+dx,(float)point.z+dz,(float)point.y+2);
                Check(found>0,"standable stair endpoint "+ramp.id+" "+point.y);
            }
            // Prop/floor corners, catwalk ends and actual door/gate collider approaches on both sides.
            foreach(var block in Map.blocks??Array.Empty<ContentBlock>())
            {
                if(block.size.x<1.2||block.size.z<1.2)continue;
                foreach(int a in new[]{-1,1})foreach(int b in new[]{-1,1})Floor("corner:"+block.id,(float)(block.pos.x+a*(block.size.x/2-.6)),(float)(block.pos.z+b*(block.size.z/2-.6)),(float)(block.pos.y+block.size.y/2+1));
            }
            foreach(var c in root.GetComponentsInChildren<Collider>())if((WorldMask&(1<<c.gameObject.layer))!=0&&(c.name.IndexOf("door",StringComparison.OrdinalIgnoreCase)>=0||c.name.IndexOf("gate",StringComparison.OrdinalIgnoreCase)>=0))
            {
                var b=c.bounds;
                // Disabled/open gates have empty Collider.bounds; use their authored transform too.
                if(c is BoxCollider box)
                {
                    b=new Bounds(box.transform.TransformPoint(box.center),Vector3.zero);
                    foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})b.Encapsulate(box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3(x,y,z))));
                }
                int found=0;foreach(int side in new[]{-1,1})
                {found+=Floor("door:"+c.name,b.center.x+side*(b.extents.x+.7f),b.center.z,b.min.y+2);found+=Floor("door:"+c.name,b.center.x,b.center.z+side*(b.extents.z+.7f),b.min.y+2);}
                Check(found>0,"standable door approaches "+c.name);
            }
            Check(spots.Count>=20&&upper>0,"nonempty ground and upper-floor sweep");Check(stairs>0,"stair endpoints resolve to standable floor");Check(doors>0,"door approaches resolve to standable floor");
            foreach(var old in Minions.ToArray())RemoveMinion(old,false);ResetZoneBoss();Pve.Holding=false;Pve.Stage="boss";Pve.Rate=0;Pve.MaxHp=6000;
            SpawnZoneBoss(kind,kind==NativeZoneBossRules.Station?new Vector3(0,0,3):new Vector3(-35,0,0),kind==NativeZoneBossRules.Station?0:90);
            var m=zoneBossActor;var record=ZoneBoss;
            pilot.Seat.connected=pilot.Seat.alive=true;pilot.ExpeditionTransferHeld=false;pilot.DevGod=false;pilot.Down=null;pilot.Death=null;pilot.Roll=null;pilot.BlockEnd=pilot.ShieldUntil=pilot.ScareUntil=0;pilot.Stats["hp"]=10000;pilot.Stats["damageTaken"]=1;
            foreach(int phase in new[]{1,2,3})
            {
                record.phase=phase;m.Hp=Pve.Boss.Hp=phase==1?6000:phase==2?3000:1000;
                foreach(var spot in spots)
                {
                    pilot.Hp=10000;pilot.StunEnd=0;pilot.Body.position=spot.Value+Vector3.up*(pilot.Collider.height*.5f);pilot.Body.transform.position=pilot.Body.position;Physics.SyncTransforms();
                    ResetCityBossPressure();bossPools.Clear();record.action=null;record.cancelAt=-100;record.holdTarget=-1;
                    // Suppress ordinary attacks to prove that a blocked/stationary boss still covers this exact floor.
                    float began=Time;bossGap=began+100;StepZoneBoss();
                    for(int tick=1;tick<=80&&pilot.Hp>=10000;tick++){Match.SynchronizeExpeditionClock(began+tick*.1f);StepZoneBoss();}
                    Check(pilot.Hp<10000,"phase "+phase+" damage by 8 s at "+spot.Key+" "+spot.Value);
                }
            }
            ResetCityBossPressure();float start=Time;StepZoneBoss();Match.SynchronizeExpeditionClock(start+6.1f);StepZoneBoss();
            var pending=record.hazards.FirstOrDefault(h=>NativeCityBossPressure.Kind(h.kind)&&h.targetSeat==pilot.Seat.id);
            Check(pending!=null&&pending.hitAt-Time>=1.49f,"full warning published before contact");
            var wire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=NetworkFrame()})).frame;
            Check(wire.Valid(Content)&&wire.pve.zoneBoss.hazards.Any(h=>h.targetSeat==pilot.Seat.id&&h.y==pending.y),"warning floor/seat survives real codec");replicaCheck?.Invoke(wire);
            float hp=pilot.Hp;var original=pilot.Body.position;
            pilot.Body.position=original+Vector3.right*4;pilot.Body.transform.position=pilot.Body.position;Physics.SyncTransforms();
            Match.SynchronizeExpeditionClock(pending.hitAt+.01f);StepZoneBoss();Check(pilot.Hp==hp,"leaving locked warning dodges impact");
            pilot.Body.position=original;pilot.Body.transform.position=original;Physics.SyncTransforms();Match.SynchronizeExpeditionClock(Time+.1f);StepZoneBoss();
            Check(pilot.Hp==hp,"spent impact does not hit a pilot who returns");
            Match.SynchronizeExpeditionClock(Time+6);StepZoneBoss();Check(record.hazards.Any(h=>NativeCityBossPressure.Kind(h.kind)),"next warning scheduled before death");
            m.Hp=0;StepZoneBoss();Match.SynchronizeExpeditionClock(Time+2);StepZoneBoss();Check(pilot.Hp==hp&&record.hazards.Length==0,"death cancels pending pressure");
            RemoveMinion(m,false);ResetZoneBoss();
            Debug.Log("PASS native city boss coverage: "+MapId+", "+spots.Count+" walkable probes x 3 phases, upper="+upper+", stair="+stairs+", door="+doors+", "+checks+" assertions; 8 s stationary deadline without ordinary attacks");
        }
    }
}
#endif
