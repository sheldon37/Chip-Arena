using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly NativeImpactLedger debrisLedger=new NativeImpactLedger();private int nextChunk=1;
        private readonly List<(NativePropBody prop,NativePilot pilot,NativePropBody other,Vector3 point,Vector3 normal)> contacts=new List<(NativePropBody,NativePilot,NativePropBody,Vector3,Vector3)>();
        private readonly Dictionary<int,(Vector3 velocity,Vector3 angular,Vector3 position)> beforePhysics=new Dictionary<int,(Vector3,Vector3,Vector3)>();
        public void BeforePhysics()
        {contacts.Clear();beforePhysics.Clear();foreach(var body in Props.Where(p=>p.Body!=null).Select(p=>p.Body).Concat(Pilots.Select(p=>p.Body)))beforePhysics[body.GetInstanceID()]=(body.isKinematic?Vector3.zero:body.linearVelocity,body.isKinematic?Vector3.zero:body.angularVelocity,body.position);debrisLedger.Begin();}
        public void PropContact(NativePropBody prop,Collision collision)
        {
            if(Replica||prop.Body==null||prop.Body.isKinematic||collision.contactCount==0)return;var pilot=collision.collider.GetComponent<NativePilotBody>()?.Pilot;var other=collision.collider.GetComponent<NativePropBody>();if(pilot==null&&other==null)return;var contact=collision.GetContact(0);contacts.Add((prop,pilot,other,contact.point,-contact.normal));
        }
        public void AfterPhysics()
        {
            foreach(var p in Pilots)if(!p.Body.isKinematic&&p.Hp>0&&beforePhysics.TryGetValue(p.Body.GetInstanceID(),out var before)){var change=p.Body.linearVelocity-before.velocity;p.Knockback+=new Vector3(change.x,0,change.z)*(float)p.Stats["knockbackTaken"];}
            Vector3 Velocity(Rigidbody body,Vector3 point){if(body==null)return Vector3.zero;if(beforePhysics.TryGetValue(body.GetInstanceID(),out var pre))return pre.velocity+Vector3.Cross(pre.angular,point-pre.position);return body.isKinematic?Vector3.zero:body.GetPointVelocity(point);}
            foreach(var c in contacts)
            {
                var prop=c.prop;if(prop==null||prop.Body==null)continue;Vector3 a=Velocity(prop.Body,c.point);bool recent=Time-prop.SourceAt<7;
                if(Match.Phase==NativePhase.Fight&&c.pilot!=null&&c.pilot.Hp>0)
                {var p=c.pilot;Vector3 b=Velocity(p.Body,c.point);double speed=Math.Max(0,Vector3.Dot(a-b,c.normal));double damage=a.magnitude<3.6?0:NativeDebrisRules.Damage(prop.Body.mass,speed);double capped=debrisLedger.Claim(prop.Definition.id+":"+p.Seat.id,(prop.Group??prop.Definition.id)+":"+p.Seat.id,Time,damage);if(capped>0)Damage(p,recent?prop.Source:null,(float)(capped*p.Stats["debrisTaken"]),(a-b).normalized,0,false,direct:false,primary:false,damageKind:"debris");}
                else if(c.other?.Body!=null&&!c.other.Body.isKinematic&&c.other.Source==null&&prop.Source!=null&&recent&&a.magnitude>3.6){c.other.Source=prop.Source;c.other.SourceAt=Time;}
            }
            StepVents();debrisLedger.End(Time);foreach(var p in Props.Where(p=>p.Body!=null&&!p.Body.isKinematic&&p.Body.position.y<-15).ToArray()){Props.Remove(p);CapturedAnimation.Release(p.gameObject);}
        }
        private NativePropBody AddProp(ContentBlock block,bool fragment=false,string group=null,NativePilot source=null)
        {
            var def=Content.Material(block.material);var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=block.id;obj.layer=PropLayer;obj.transform.SetParent(root.transform);obj.transform.SetPositionAndRotation(Vec(block.pos),new Quaternion(block.rotation.x,block.rotation.y,block.rotation.z,block.rotation.w));obj.transform.localScale=Vec(block.size);
            obj.GetComponent<Renderer>().sharedMaterial=materials[block.material];obj.GetComponent<Collider>().sharedMaterial=surfaces[block.material];var prop=obj.AddComponent<NativePropBody>();prop.Arena=this;prop.Definition=block;prop.Health=def.durability;prop.Indestructible=def.indestructible;prop.Fragment=fragment;prop.Group=group??block.id;prop.Source=source;prop.SourceAt=Time;
            if(!def.indestructible){prop.Body=obj.AddComponent<Rigidbody>();prop.Body.mass=block.mass>0?block.mass:def.mass;prop.Body.isKinematic=!block.dynamic;prop.Body.linearDamping=.15f;prop.Body.angularDamping=.25f;prop.Body.collisionDetectionMode=block.dynamic?CollisionDetectionMode.ContinuousDynamic:CollisionDetectionMode.Discrete;}Props.Add(prop);if(!fragment)DetailProp(prop);return prop;
        }
        private void Fracture(NativePropBody prop,Vector3 extra,NativePilot source)
        {
            if(prop.Fragment)return;
            if(Props.Count+3>360){prop.Fragment=true;prop.Body.isKinematic=false;prop.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;prop.Body.AddForce(extra,ForceMode.Impulse);prop.Source=source;prop.SourceAt=Time;CheckSupports(source);return;}
            Vector3 position=prop.transform.position,size=Vec(prop.Definition.size),velocity=prop.Body.isKinematic?Vector3.zero:prop.Body.linearVelocity,angular=prop.Body.isKinematic?Vector3.zero:prop.Body.angularVelocity;Quaternion rotation=prop.transform.rotation;float mass=prop.Body.mass;string group="fracture-"+nextChunk++;
            Props.Remove(prop);prop.GetComponent<Collider>().enabled=false;Emit(EventKind.Break,source,0,position,0,prop.Definition.material,surfaceId:prop.Definition.id);
            for(int i=0;i<4;i++)
            {
                Vector3 offset=new Vector3((i%2==1?1:-1)*size.x*.25f,(i<2?1:-1)*size.y*.25f,0),point=position+rotation*offset;
                var def=new ContentBlock{id="chunk-"+nextChunk++,pos=Vec(point),size=new V3(size.x*.46f,size.y*.44f,size.z*.88f),rotation=new ContentRotation{x=rotation.x,y=rotation.y,z=rotation.z,w=rotation.w},material=prop.Definition.material,dynamic=true,mass=mass/4};var chunk=AddProp(def,true,group,source);chunk.Body.linearVelocity=velocity+Vector3.Cross(angular,point-position)+extra/Math.Max(mass,.1f)+new Vector3((i%2==1?1:-1)*2.4f,0,(i<2?1:-1)*1.4f);chunk.Body.angularVelocity=angular;
            }
            CapturedAnimation.Release(prop.gameObject);CheckSupports(source);
        }
        private void CheckSupports(NativePilot source)
        {foreach(var p in Props)if(p.Body!=null&&p.Body.isKinematic&&p.Definition.support!=null&&p.Definition.support.Length>0&&p.Definition.support.All(id=>!Props.Any(q=>q.Definition.id==id&&q.Health>0))){p.Body.isKinematic=false;p.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;p.Source=source;p.SourceAt=Time;p.Group="collapse-"+nextChunk++;Emit(EventKind.Collapse,source,0,p.transform.position,0,p.Definition.id);}}
        private bool SceneLinecast(Vector3 from,Vector3 to,int mask)=>SceneLinecast(from,to,out _,mask);
        private bool SceneLinecast(Vector3 from,Vector3 to,out RaycastHit hit,int mask){var delta=to-from;return root.scene.GetPhysicsScene().Raycast(from,delta.normalized,out hit,delta.magnitude,mask,QueryTriggerInteraction.Ignore);}
    }
}
