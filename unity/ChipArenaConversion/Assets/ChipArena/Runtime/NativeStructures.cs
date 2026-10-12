using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public static class NativeUnityMapLoader
    {
        public static void Load(ConversionContent content)
        {var asset=Resources.Load<TextAsset>("UnityMaps");if(asset==null)throw new InvalidOperationException("UnityMaps.json is missing");NativeMapRules.Merge(content,JsonUtility.FromJson<UnityMapContent>(asset.text),asset.text);}
    }
    public sealed partial class NativeArena
    {
        private readonly List<StructureDamage> structuralDamage=new List<StructureDamage>();
        private readonly Dictionary<string,HashSet<string>> triggeringMembers=new Dictionary<string,HashSet<string>>();
        private readonly Dictionary<int,(int pilot,float at)> lastPilotDamage=new Dictionary<int,(int,float)>();
        private readonly Dictionary<string,float> crushSpent=new Dictionary<string,float>();
        private readonly HashSet<string> crushHits=new HashSet<string>();
        private readonly Dictionary<string,(string segment,string block,float at)> riders=new Dictionary<string,(string,string,float)>();
        private readonly Dictionary<string,float> fragmentRest=new Dictionary<string,float>();
        public int NavigationRevision=>Props.Count+(Structures?.Revision??0)*10000;
        private void ResetStructureRuntime(){structuralDamage.Clear();triggeringMembers.Clear();lastPilotDamage.Clear();crushSpent.Clear();crushHits.Clear();riders.Clear();fragmentRest.Clear();navRoutes.Clear();}
        private bool FilterStructureDamage(NativePropBody prop,ref float amount,NativePilot source,string kind,float demolition)
        {
            var b=prop.Definition;if(string.IsNullOrEmpty(b.role))return true;
            if(b.role=="structural"&&!NativeStructureRules.Armed(Match.Phase,Match.FightAt,Time))return false;
            amount*=NativeStructureRules.DamageScale(b.role,kind,demolition);if(amount<=0||!MathUtil.Finite(amount))return false;
            if(b.role=="structural"&&source!=null)structuralDamage.Add(new StructureDamage{member=b.id,pilot=source.Seat.id,amount=Math.Min(prop.Health,amount),at=Time});return true;
        }
        // Both explosion entry points use this exact closest-point + LOS rule for authored roles only.
        private void StructureBlast(NativePropBody p,NativePilot source,Vector3 at,float radius,float damage,float impulse)
        {
            if(p.Indestructible||radius<=0)return;var collider=p.GetComponent<Collider>();Vector3 point=collider.ClosestPoint(at),delta=point-at;float distance=delta.magnitude;
            if(distance>=radius||distance>.001f&&SceneLinecast(at,point,out var hit,WorldMask)&&hit.collider!=collider)return;
            float fall=1-Mathf.Clamp(distance/radius,0,.85f);DamageProp(p,damage*fall,(p.transform.position-at+Vector3.up*.3f).normalized*impulse*fall,at,source,"blast",(float)(source?.Stats["demolition"]??1));
        }
        private string BlockSegment(ContentBlock b)=>b.segment??Map.segments?.FirstOrDefault(s=>s.supports.Any(x=>x.member==b.id))?.id;
        private void FractureStructureBlock(NativePropBody prop,Vector3 extra,NativePilot source)
        {
            string segment=BlockSegment(prop.Definition);Vector3 at=prop.transform.position,size=Vec(prop.Definition.size);Quaternion rotation=prop.transform.rotation;
            Props.Remove(prop);prop.GetComponent<Collider>().enabled=false;Emit(EventKind.Break,source,0,at,0,prop.Definition.material,surfaceId:prop.Definition.id);
            for(int i=0;i<4;i++)
            {
                while(Props.Count(p=>p.Fragment)>=80||Props.Count>=360){var old=Props.FirstOrDefault(p=>p.Fragment);if(old==null)break;RemoveStructureProp(old);}
                if(Props.Count>=360)break;
                Vector3 offset=new Vector3((i%2==0?-1:1)*size.x*.25f,(i<2?-1:1)*size.y*.25f,0);
                var b=new ContentBlock{id="chunk-"+nextChunk++,parent=prop.Definition.id,segment=segment,role="fragment",material=prop.Definition.material,pos=Vec(at+rotation*offset),size=Vec(size*.35f),rotation=new ContentRotation{x=rotation.x,y=rotation.y,z=rotation.z,w=rotation.w},dynamic=true,mass=Math.Max(1,(prop.Body?.mass??26)/4)};
                var chunk=AddProp(b,true,segment??prop.Definition.id,source);chunk.Body.linearVelocity=Vector3.ClampMagnitude(extra/Math.Max(1,b.mass),12)+new Vector3(i%2==0?-1:1,1,i<2?-1:1);
            }CapturedAnimation.Release(prop.gameObject);
        }
        private void RemoveStructureProp(NativePropBody p){Props.Remove(p);StaticBlocks.Remove(p);p.GetComponent<Collider>().enabled=false;CapturedAnimation.Release(p.gameObject);}
        private void StepStructureFragments()
        {
            if(!Map.unity)return;
            foreach(var p in Props.Where(p=>p.Fragment).ToArray())
            {if(Time-p.SourceAt>=20){RemoveStructureProp(p);continue;}if(p.Body==null||p.Body.isKinematic)continue;string id=p.Definition.id;if(p.Body.linearVelocity.sqrMagnitude<.04f&&p.Body.angularVelocity.sqrMagnitude<.04f){if(!fragmentRest.TryGetValue(id,out float at))fragmentRest[id]=Time;else if(Time-at>=2.5f)p.Body.isKinematic=true;}else fragmentRest.Remove(id);}
        }
        private HashSet<string> LostMembers(StructureDefinition s)
        {
            var members=new HashSet<string>();void Visit(StructureDefinition d){foreach(var p in d.supports){if(!string.IsNullOrEmpty(p.member)){if(!Props.Any(b=>b.Definition.id==p.member&&b.Health>0))members.Add(p.member);}else if(Structures.Get(p.segment).state!=0){if(triggeringMembers.TryGetValue(p.segment,out var inherited))members.UnionWith(inherited);else Visit(Map.segments.First(x=>x.id==p.segment));}}}Visit(s);return members;
        }
        private int Credit(StructureDefinition s)=>NativeStructureRules.Credit(structuralDamage,triggeringMembers.TryGetValue(s.id,out var members)?members:LostMembers(s),Time);
        private float CollapseDamage(NativePilot p,string segment,float amount,Vector3 direction,float push)
        {
            if(string.IsNullOrEmpty(segment))return 0;string key=segment+":"+p.Seat.id;crushSpent.TryGetValue(key,out float spent);float cap=NativeStructureRules.Allowance((float)p.Stats["hp"],spent);if(cap<=0)return 0;
            int id=Credit(Map.segments.First(s=>s.id==segment));if(lastPilotDamage.TryGetValue(p.Seat.id,out var last))id=NativeStructureRules.FallbackCredit(id,last.pilot,last.at,Time);
            var source=Pilots.FirstOrDefault(x=>x.Seat.id==id);float actual=Damage(p,source,amount,direction,push*65,false,direct:false,primary:false,damageKind:"collapse",collapseSegment:segment,finalCap:cap);
            crushSpent[key]=spent+actual;return actual;
        }
        private void StepStructures()
        {
            if(Structures==null||Map.segments==null)return;
            var changes=Structures.Step(Time,(float)Match.FightAt,id=>Props.Any(p=>p.Definition.id==id&&p.Health>0));
            foreach(var c in changes)
            {
                var d=Map.segments.First(s=>s.id==c.id);if(c.state==1)triggeringMembers[c.id]=LostMembers(d);
                var e=new ArenaEvent{Id=nextEvent++,Kind=c.state==1?EventKind.StructureStress:c.state==2?EventKind.StructureFall:EventKind.StructureLand,Time=Time,Detail=d.id,Position=d.crush.center,Radius=d.crush.size.Length/2,Amount=c.state==1?c.t0-Time:c.state==2?d.mass:1,Normal=d.direction,SurfaceId=c.state==1?c.member:c.state==2?d.material:null};Events.Add(e);
                if(c.state==3)LandStructure(d);
            }
            PoseStructures(Time,Match.Phase==NativePhase.Fight);structuralDamage.RemoveAll(x=>Time-x.at>10);
        }
        private readonly List<(NativePropBody body,Bounds previous,StructureDefinition segment)> posedStructureBlocks=new List<(NativePropBody,Bounds,StructureDefinition)>();
        public void PoseStructures(float time,bool authority=false)
        {
            PoseStageLoads(time);
            if(Structures==null||Map.segments==null||Map.segments.Length==0)return;posedStructureBlocks.Clear();bool moved=false;
            foreach(var d in Map.segments)
            {
                var s=Structures.Get(d.id);if(s.state!=2&&!(s.state==1&&(time>=s.t0||d.mode=="topple")))continue;
                foreach(var p in Props.Where(p=>p.Definition.segment==d.id&&!p.Fragment).ToArray())
                {
                    var oldBounds=p.GetComponent<Collider>().bounds;var pose=NativeStructureRules.Pose(d,p.Definition,s.t0,time);Vector3 next=Vec(pose.position);float dy=next.y-p.transform.position.y;
                    if(authority&&d.mode=="pancake"&&p.Definition.role=="carried"&&d.poses.First(x=>x.block==p.Definition.id).angle==0)
                    foreach(var pilot in Pilots.Where(x=>x.Hp>0&&!x.Body.isKinematic))
                    {var at=pilot.Body.position;float feet=at.y-.85f;if(Math.Abs(feet-oldBounds.max.y)<.2f&&at.x>=oldBounds.min.x-.1f&&at.x<=oldBounds.max.x+.1f&&at.z>=oldBounds.min.z-.1f&&at.z<=oldBounds.max.z+.1f&&pilot.Body.linearVelocity.y<=.1f&&!pilot.Input.Jump){pilot.Body.position+=Vector3.up*dy;riders[d.id+":"+pilot.Seat.id]=(d.id,p.Definition.id,time);}}
                    p.transform.SetPositionAndRotation(next,new Quaternion(pose.rotation.x,pose.rotation.y,pose.rotation.z,pose.rotation.w));moved=true;
                    if(authority&&s.state==2)posedStructureBlocks.Add((p,oldBounds,d));
                }
            }
            // One broadphase synchronization per pose batch, not one per falling panel.
            if(moved)Physics.SyncTransforms();
            foreach(var posed in posedStructureBlocks)
            foreach(var pilot in Pilots.Where(x=>x.Hp>0))
            {
                var d=posed.segment;string key=d.id+":"+pilot.Seat.id;if(crushHits.Contains(key)||(riders.TryGetValue(key,out var rider)&&time-rider.at<.06f))continue;
                var at=pilot.Body.position;var b=posed.body.GetComponent<Collider>().bounds;float head=at.y+.85f;
                if(at.x>=b.min.x-.42f&&at.x<=b.max.x+.42f&&at.z>=b.min.z-.42f&&at.z<=b.max.z+.42f&&b.min.y<=head&&posed.previous.max.y>=at.y-.85f&&NativeStructureRules.Contains(Vec(at),d.crush.center,d.crush.size,.42f)){crushHits.Add(key);CollapseDamage(pilot,d.id,45,(at-Vec(d.crush.center)).normalized,6);}
            }
        }
        private void LandStructure(StructureDefinition d)
        {
            foreach(var p in Props.Where(p=>!p.Fragment&&(p.Definition.segment==d.id||d.supports.Any(x=>x.member==p.Definition.id))).ToArray())RemoveStructureProp(p);
            foreach(var b in d.rubble)if(!StaticBlocks.Any(p=>p.Definition.id==b.id))AddProp(b);
            Physics.SyncTransforms();
            foreach(var p in Pilots.Where(p=>p.Hp>0))
            {string key=d.id+":"+p.Seat.id;if(Match.Phase==NativePhase.Fight&&(riders.TryGetValue(key,out var rider)&&Time-rider.at<.06f))CollapseDamage(p,d.id,15,Vector3.zero,0);if(StaticBlocks.Any(b=>b.Definition.role=="rubble"&&b.GetComponent<Collider>().bounds.Intersects(p.Collider.bounds)))EjectRubble(p);}
        }
        private bool Walkable(Vector3 at,out Vector3 found)
        {
            found=at;var scene=root.scene.GetPhysicsScene();var cursor=at+Vector3.up*3.85f;var hits=new Collider[16];float best=float.PositiveInfinity;bool valid=false;
            // Check lower floors too: a surviving hotel slab must not hide a clear ground exit.
            for(int i=0;i<12&&cursor.y>at.y-3.9f;i++)
            {
                if(!scene.Raycast(cursor,Vector3.down,out var floor,cursor.y-(at.y-3.9f),WorldMask,QueryTriggerInteraction.Ignore))break;cursor=floor.point-Vector3.up*.02f;
                var spot=new Vector3(at.x,floor.point.y+.88f,at.z);if(floor.normal.y<.85f)continue;
                int n=scene.OverlapCapsule(spot-Vector3.up*.42f,spot+Vector3.up*.42f,.415f,hits,WorldMask,QueryTriggerInteraction.Ignore);
                if(n==0&&(Vec(NativeMapRules.ExcludeTops(Map,Vec(spot)))-spot).sqrMagnitude<.000001f&&Math.Abs(spot.y-at.y)<best){found=spot;best=Math.Abs(spot.y-at.y);valid=true;}
            }return valid;
        }
        private bool FindRubbleExit(Vector3 origin,out Vector3 exit)
        {
            exit=origin;float distance=9.0001f;bool found=false;
            for(int x=-12;x<=12;x++)for(int z=-12;z<=12;z++)
            {
                float horizontal=(x*x+z*z)*.0625f;if(horizontal>distance)continue;
                if(Walkable(origin+new Vector3(x*.25f,0,z*.25f),out var spot)){float sq=(spot-origin).sqrMagnitude;if(sq<distance){exit=spot;distance=sq;found=true;}}
            }return found;
        }
        private void EjectRubble(NativePilot p)
        {
            if(FindRubbleExit(p.Body.position,out var exit)){p.Body.position=exit;p.Body.linearVelocity=Vector3.zero;return;}
            // Invalid geometry must be caught by the Unity build gate, never hidden by an unbounded teleport.
            Debug.LogError("Brickfall rubble has no capsule-safe ejection within 3 m at "+p.Body.position);
        }
        private void RestoreStructures(StructureFrame[] frames,float time,NativePropFrame[] visible)
        {
            Structures.Restore(frames,time);
            foreach(var d in Map.segments??Array.Empty<StructureDefinition>())
            {int state=Structures.Get(d.id).state;foreach(var p in StaticBlocks.Where(p=>d.rubble.Any(b=>b.id==p.Definition.id)&&state!=3).ToArray())RemoveStructureProp(p);
                if(state==3){foreach(var p in Props.Where(p=>!p.Fragment&&(p.Definition.segment==d.id||d.supports.Any(x=>x.member==p.Definition.id))).ToArray())RemoveStructureProp(p);foreach(var b in d.rubble)if(!StaticBlocks.Any(p=>p.Definition.id==b.id))AddProp(b);}
                else foreach(var b in Map.blocks.Where(b=>(b.segment==d.id||d.supports.Any(x=>x.member==b.id))&&(b.role=="carried"||visible.Any(p=>p.id==b.id)))){var p=Props.FirstOrDefault(p=>p.Definition.id==b.id)??AddProp(b);if(state<2)p.transform.SetPositionAndRotation(Vec(b.pos),new Quaternion(b.rotation.x,b.rotation.y,b.rotation.z,b.rotation.w));}
            }PoseStructures(time);
        }
        private bool TrySandboxSpawn(NativePilot p,out Vector3 result)
        {
            result=Vector3.zero;var preferred=NativeMapRules.Spawn(Content,Map,Match.Seats,Math.Max(0,Pilots.IndexOf(p)),Match.Teams);
            var points=new[]{preferred.pos}.Concat(Map.spawns?.ffa.Select(x=>x.pos)??Content.spawns);
            foreach(var point in points){var at=Vec(point);if(Map.unity&&(Pilots.Any(q=>q!=p&&q.Hp>0&&Enemy(p,q)&&Vector3.Distance(q.Body.position,at)<8)||UnsafeStructure(at)||UnsafeStagePath(at,at,3)))continue;if(!Map.unity||Walkable(at,out at)){result=at;return true;}}return false;
        }
        private bool UnsafeStructure(Vector3 at)=>Map.segments!=null&&Map.segments.Any(s=>(Structures.Get(s.id).state==1||Structures.Get(s.id).state==2)&&NativeStructureRules.Swept(s,Vec(at),.85f));
    }
}
