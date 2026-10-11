#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Actual JsonUtility + scene collision gate. Invoked during the next Unity export, not by the managed suite.
        public void ValidateArenaFoundation(NativeArena guest,NativeArena late)
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("Arena foundation Unity: "+message);}
            Check(Structures!=null&&Pilots.Count==8,"experimental loader / eight pilots");
            var initial=NativeFrameCodec.Clone(NetworkFrame());Check(initial.Valid(Content),"initial frame / optional presence");guest.ApplyFrame(initial,1);
            var actor=Local;float hp=actor.Hp;actor.Body.position=new Vector3(-34,.9f,-10);
            var west=Props.Where(p=>p.Definition.anchor&&p.Definition.group=="west-supports").ToArray();
            foreach(var pillar in west)
            {
                var at=pillar.transform.position;
                // Use the actual area-damage entrypoint, so support damage cannot pass only in an isolated core fixture.
                Blast(actor,at,1.5f,1000,0);
            }
            Check(west.All(p=>Structures.Find(p.Definition.id).state==StructureState.Destroyed),"area damage breaks the support blocks");
            var start=StructureTick;Structures.Step(start);SyncStructures(start);
            Check(Structures.Blocks.Any(b=>b.state==StructureState.Telegraph)&&Structures.Falling==0,"telegraph retains support collision");
            var warning=NativeFrameCodec.Clone(NetworkFrame());Check(warning.Valid(Content),"warning codec");guest.ApplyFrame(warning,1);
            Check(guest.Structures.Blocks.Count(b=>b.state==StructureState.Telegraph)==Structures.Blocks.Count(b=>b.state==StructureState.Telegraph),"guest warning");
            for(int t=start+1;t<start+240;t++)
            {
                Structures.Step(t,LandingOccupied);SyncStructures(t);
                Check(Structures.Falling<=Content.Map(MapId).gameplay.maxFalling,"falling cap");
                foreach(var b in Structures.Blocks.Where(b=>b.state==StructureState.Falling))Check(!structuralProps[b.id].GetComponent<Collider>().enabled&&structuralProps[b.id].Body==null,"falling box cannot crush");
            }
            var settled=NetworkFrame();settled.time=(start+240)/60d;settled=NativeFrameCodec.Clone(settled);Check(settled.Valid(Content),"settled codec");
            guest.ApplyFrame(settled,1);late.ApplyFrame(NativeFrameCodec.Clone(settled),1);
            foreach(var b in Structures.Blocks.Where(b=>b.state==StructureState.Settled))
            {
                var prop=structuralProps[b.id];Check(prop.Body==null&&prop.GetComponent<Collider>().enabled,"rubble is static collision");
                foreach(var replica in new[]{guest,late})
                {var other=replica.Props.Single(p=>p.Definition.id==b.id);Check(other.Body==null&&other.GetComponent<Collider>().enabled&&Vector3.Distance(prop.transform.position,other.transform.position)<.0001f,"guest/late join collision agrees");}
                var physics=Root.scene.GetPhysicsScene();Check(physics.Raycast(prop.transform.position+Vector3.up*2,Vector3.down,out var hit,4,WorldMask,QueryTriggerInteraction.Ignore)&&hit.collider==prop.GetComponent<Collider>(),"settled rubble ray collision");
            }
            Check(Local.Hp==hp,"falling box did not damage pilot");
            var packet=NativeFrameBudget.Encode(settled,1);Check(packet!=null&&packet.Length<=NativeFrameBudget.MaxPacketBytes,"structural packet fits budget");
            late.ApplyFrame(initial,1);Check(late.Structures.Blocks.All(b=>b.state==StructureState.Intact)&&late.Props.Where(p=>!string.IsNullOrEmpty(p.Definition.group)).All(p=>p.GetComponent<Collider>().enabled&&p.gameObject.activeSelf),"killcam rewind restores collision");
            actor.Body.position=new Vector3(100,.9f,100);ClampMapPilots();Check(Bounds.Contains(Vec(actor.Body.position),.42f),"physical play bounds");
            Check(SpawnPosition(actor.Seat,0)==Vec(Content.Map(MapId).gameplay.ffa[0]),"map-specific respawn point");
            Debug.Log("PASS arena foundation Unity: real area damage, telegraph, bounded analytical fall, static rubble collision, host/guest/late-join codec and packet budget, killcam rewind, bounds and spawn selection");
        }
    }
}
#endif
