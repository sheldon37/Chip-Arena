using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public enum StructureState { Intact,Telegraph,Falling,Settled,Destroyed }
    public enum StructureEventKind { BlockDamaged,BlockBroken,CollapseTelegraph,CollapseStart,CollapseSettled,HazardPhaseChange }
    [Serializable] public sealed class StructureBlockState
    {
        public string id,group;public float health;public int damageStage,seed,startTick,endTick;public StructureState state;
        public V3 position,size,from,rubblePosition,rubbleSize;public ContentRotation rotation=new ContentRotation();
        public StructureBlockState Copy(){var v=(StructureBlockState)MemberwiseClone();v.rotation=new ContentRotation{x=rotation.x,y=rotation.y,z=rotation.z,w=rotation.w};return v;}
    }
    [Serializable] public sealed class StructureEvent
    {
        public int sequence,tick,seed,source,phase;public StructureEventKind kind;public string block,group,hazard,cause;
        public float amount;public V3 point,normal,impulse,direction;public float time=>tick/60f;
        public StructureEvent Copy()=>(StructureEvent)MemberwiseClone();
    }
    [Serializable] public sealed class StructureHazardState {public string id;public int phase,tick;public StructureHazardState Copy()=>(StructureHazardState)MemberwiseClone();}
    [Serializable] public sealed class StructureSnapshot
    {public string layout;public int schema=1,tick,revision,sequence;public StructureBlockState[] blocks;public StructureEvent[] events;public StructureHazardState[] hazards;}

    // All decisions use a fixed tick, stable ordinal order and quantized geometry. PhysX is never an input
    // except for the explicit occupied landing query supplied by the host (part of the deterministic input).
    public sealed class NativeStructure
    {
        public const int EventCapacity=512;
        private readonly ContentMap map;private readonly ConversionContent content;
        private readonly SortedDictionary<string,StructureBlockState> blocks=new SortedDictionary<string,StructureBlockState>(StringComparer.Ordinal);
        private readonly Dictionary<string,ContentBlock> definitions;
        private readonly List<StructureEvent> events=new List<StructureEvent>();
        private readonly Dictionary<string,StructureHazardState> hazards=new Dictionary<string,StructureHazardState>(StringComparer.Ordinal);
        private int tick,revision,sequence;private readonly uint seed;private readonly string layout;
        public int Revision=>revision;public IEnumerable<StructureBlockState> Blocks=>blocks.Values;
        public IEnumerable<StructureEvent> Events=>events;public IEnumerable<StructureHazardState> Hazards=>hazards.Values;
        public int Falling=>blocks.Values.Count(b=>b.state==StructureState.Falling);
        public int ActiveGroups=>blocks.Values.Where(b=>b.state==StructureState.Telegraph||b.state==StructureState.Falling).Select(b=>b.group).Distinct().Count();
        public NativeStructure(ConversionContent content,ContentMap map,uint seed=1)
        {
            this.content=content;this.map=map;this.seed=seed;NativeMapRules.Validate(content,map);layout=NativeMapRules.Fingerprint(content,map);
            definitions=map.blocks.ToDictionary(b=>b.id,StringComparer.Ordinal);
            foreach(var b in map.blocks.Where(b=>!string.IsNullOrEmpty(b.group)).OrderBy(b=>b.id,StringComparer.Ordinal))blocks.Add(b.id,new StructureBlockState{id=b.id,group=b.group,health=content.Material(b.material).durability,position=b.pos,from=b.pos,rubblePosition=b.pos,size=b.size,rubbleSize=b.size});
            foreach(var h in map.gameplay.hazards??Array.Empty<MapHazard>())hazards.Add(h.id,new StructureHazardState{id=h.id,phase=-1});
        }
        public StructureBlockState Find(string id)=>id!=null&&blocks.TryGetValue(id,out var b)?b:null;
        public static V3 Position(StructureBlockState b,int at)
        {
            if(b.state!=StructureState.Falling)return b.position;
            float u=MathUtil.Clamp((at-b.startTick)/(float)Math.Max(1,b.endTick-b.startTick),0,1);
            return b.from+(b.rubblePosition-b.from)*(u*u);
        }
        private void Event(StructureEvent e){e.sequence=++sequence;e.tick=tick;events.Add(e);if(events.Count>EventCapacity)events.RemoveAt(0);}
        public bool Damage(string id,float amount,V3 point,V3 normal,V3 impulse,string cause,int source,int atTick)
        {
            var b=Find(id);if(b==null||atTick<tick||!MathUtil.Finite(amount)||amount<=0||!NativeMapRules.Finite(point)||!NativeMapRules.Finite(normal)||!NativeMapRules.Finite(impulse)||b.state==StructureState.Destroyed||b.state==StructureState.Falling||b.state==StructureState.Settled||content.Material(definitions[id].material).indestructible)return false;
            tick=atTick;float actual=Math.Min(amount,b.health);b.health=Math.Max(0,b.health-amount);
            float ratio=b.health/content.Material(definitions[id].material).durability;b.damageStage=ratio>2f/3?0:ratio>1f/3?1:ratio>0?2:3;
            Event(new StructureEvent{kind=StructureEventKind.BlockDamaged,block=id,group=b.group,amount=actual,point=point,normal=normal,cause=cause,source=source});
            if(b.health<=0){b.state=StructureState.Destroyed;revision++;Event(new StructureEvent{kind=StructureEventKind.BlockBroken,block=id,group=b.group,point=point,impulse=impulse,cause=cause,source=source});}
            return true;
        }
        private HashSet<string> Supported()
        {
            var found=new HashSet<string>(blocks.Values.Where(b=>b.state==StructureState.Intact&&definitions[b.id].anchor).Select(b=>b.id),StringComparer.Ordinal);bool changed;
            do{changed=false;foreach(var b in blocks.Values)if(b.state==StructureState.Intact&&!found.Contains(b.id)&&(definitions[b.id].support?.Any(found.Contains)??false)){found.Add(b.id);changed=true;}}while(changed);
            return found;
        }
        public void Step(int atTick,Func<V3,V3,bool> occupied=null)
        {
            if(atTick<tick)throw new ArgumentOutOfRangeException(nameof(atTick));tick=atTick;
            // Falling/telegraph phases finish before new work is admitted. Repeated calls at a tick are harmless.
            foreach(var group in blocks.Values.Where(b=>b.state==StructureState.Falling).GroupBy(b=>b.group).OrderBy(g=>g.Min(b=>b.position.y)).ThenBy(g=>g.Key,StringComparer.Ordinal).ToArray())
            {
                var pieces=group.ToArray();if(pieces.Any(b=>tick<b.endTick))continue;
                if(occupied!=null&&pieces.Any(b=>occupied(b.rubblePosition,b.rubbleSize)))continue;
                foreach(var b in pieces){b.state=StructureState.Settled;b.position=b.rubblePosition;b.size=b.rubbleSize;}
                revision++;Event(new StructureEvent{kind=StructureEventKind.CollapseSettled,group=group.Key});
            }
            foreach(var group in blocks.Values.Where(b=>b.state==StructureState.Telegraph).GroupBy(b=>b.group).OrderBy(g=>g.Min(b=>b.position.y)).ThenBy(g=>g.Key,StringComparer.Ordinal).ToArray())
            {
                var pieces=group.ToArray();if(pieces.Any(b=>tick<b.endTick)||Falling+pieces.Length>map.gameplay.maxFalling)continue;
                foreach(var b in pieces.OrderBy(b=>b.position.y).ThenBy(b=>b.id,StringComparer.Ordinal))
                {
                    PlanLanding(b);b.state=StructureState.Falling;b.from=b.position;b.startTick=tick;
                    b.endTick=tick+Math.Max(12,Math.Min(120,(int)Math.Ceiling(Math.Sqrt(Math.Max(.1,b.from.y-b.rubblePosition.y)*2/23)*60)));
                }
                revision++;Event(new StructureEvent{kind=StructureEventKind.CollapseStart,group=group.Key,seed=pieces[0].seed,direction=new V3(0,-1,0)});
            }
            var supported=Supported();
            foreach(var group in blocks.Values.Where(b=>b.state==StructureState.Intact&&!supported.Contains(b.id)).GroupBy(b=>b.group).OrderBy(g=>g.Min(b=>b.position.y)).ThenBy(g=>g.Key,StringComparer.Ordinal).ToArray())
            {
                if(ActiveGroups>=map.gameplay.maxCollapses)break;
                if(blocks.Values.Any(b=>b.group==group.Key&&(b.state==StructureState.Telegraph||b.state==StructureState.Falling)))continue;
                int groupSeed=unchecked((int)(seed^Hash(group.Key)^(uint)(sequence+1)*2654435761u));
                foreach(var b in group){b.state=StructureState.Telegraph;b.startTick=tick;b.endTick=tick+(int)Math.Ceiling(map.gameplay.telegraph*60);b.seed=groupSeed;}
                revision++;Event(new StructureEvent{kind=StructureEventKind.CollapseTelegraph,group=group.Key,seed=groupSeed});
            }
            foreach(var h in map.gameplay.hazards??Array.Empty<MapHazard>())
            {
                int phase=HazardPhase(h,tick);var state=hazards[h.id];if(phase==state.phase)continue;state.phase=phase;state.tick=tick;
                Event(new StructureEvent{kind=StructureEventKind.HazardPhaseChange,hazard=h.id,phase=phase});
            }
        }
        private static uint Hash(string s){uint h=2166136261;foreach(char c in s)h=unchecked((h^c)*16777619);return h;}
        private static float Quantize(float v)=>(float)Math.Round(v*1000,MidpointRounding.AwayFromZero)/1000;
        private void PlanLanding(StructureBlockState b)
        {
            // Preserve the authored footprint; shallow rubble cannot spill into spawn columns.
            b.rubbleSize=new V3(b.size.x,Math.Min(b.size.y,.9f),b.size.z);float floor=map.gameplay.bounds.killY-2;
            bool Overlap(V3 p,V3 s)=>Math.Abs(p.x-b.position.x)<(s.x+b.size.x)/2-.001f&&Math.Abs(p.z-b.position.z)<(s.z+b.size.z)/2-.001f;
            foreach(var d in map.blocks.Where(d=>string.IsNullOrEmpty(d.group)))if(Overlap(d.pos,d.size)&&d.pos.y+d.size.y/2<=b.position.y-b.size.y/2+.01f)floor=Math.Max(floor,d.pos.y+d.size.y/2);
            foreach(var other in blocks.Values)
            {
                if(other==b||other.state==StructureState.Destroyed||other.state==StructureState.Telegraph)continue;
                V3 p=other.state==StructureState.Falling?other.rubblePosition:other.position,s=other.state==StructureState.Falling?other.rubbleSize:other.size;
                if(Overlap(p,s)&&p.y+s.y/2<=b.position.y-b.size.y/2+.01f)floor=Math.Max(floor,p.y+s.y/2);
            }
            b.rubblePosition=new V3(Quantize(b.position.x),Quantize(floor+b.rubbleSize.y/2),Quantize(b.position.z));
        }
        public static int HazardPhase(MapHazard h,int tick)
        {int off=(int)Math.Ceiling(h.off*60),warning=(int)Math.Ceiling(h.warning*60),active=(int)Math.Ceiling(h.active*60),period=off+warning+active;int t=((tick+(int)(h.offset*60))%period+period)%period;return t<off?0:t<off+warning?1:2;}
        public StructureSnapshot Capture()=>new StructureSnapshot{layout=layout,tick=tick,revision=revision,sequence=sequence,blocks=blocks.Values.Select(b=>b.Copy()).ToArray(),events=events.Select(e=>e.Copy()).ToArray(),hazards=hazards.Values.OrderBy(h=>h.id,StringComparer.Ordinal).Select(h=>h.Copy()).ToArray()};
        public static bool Valid(ConversionContent content,ContentMap map,StructureSnapshot s)
        {
            if(!NativeMapRules.Enabled(map))return s==null;
            if(s==null||s.layout!=NativeMapRules.Fingerprint(content,map)||s.schema!=1||s.tick<0||s.revision<0||s.sequence<0||s.blocks==null||s.events==null||s.hazards==null||s.events.Length>EventCapacity)return false;
            var defs=map.blocks.Where(b=>!string.IsNullOrEmpty(b.group)).ToDictionary(b=>b.id,StringComparer.Ordinal);
            if(s.blocks.Length!=defs.Count||s.blocks.Any(b=>b==null||b.id==null)||s.blocks.Select(b=>b.id).Distinct().Count()!=defs.Count)return false;
            foreach(var b in s.blocks)
            {
                if(!defs.TryGetValue(b.id,out var d)||b.group!=d.group||!Enum.IsDefined(typeof(StructureState),b.state)||b.damageStage<0||b.damageStage>3||!MathUtil.Finite(b.health)||b.health<0||b.health>content.Material(d.material).durability||b.startTick<0||b.endTick<b.startTick||!NativeMapRules.Finite(b.position)||!NativeMapRules.Finite(b.from)||!NativeMapRules.Finite(b.rubblePosition)||!NativeMapRules.Finite(b.size)||!NativeMapRules.Finite(b.rubbleSize)||b.rotation==null||b.rotation.x!=0||b.rotation.y!=0||b.rotation.z!=0||b.rotation.w!=1)return false;
                if(b.position.x!=d.pos.x||b.position.z!=d.pos.z||b.from.x!=d.pos.x||b.from.z!=d.pos.z||Math.Abs(b.rubblePosition.x-d.pos.x)>.001f||Math.Abs(b.rubblePosition.z-d.pos.z)>.001f||b.size.x!=d.size.x||b.size.z!=d.size.z||b.size.y<=0||b.size.y>d.size.y||b.rubbleSize.x!=d.size.x||b.rubbleSize.z!=d.size.z||b.rubbleSize.y<=0||b.rubbleSize.y>d.size.y||Math.Abs(b.position.y)>200||Math.Abs(b.rubblePosition.y)>200||Math.Abs(b.from.y)>200)return false;
            }
            if(s.blocks.Count(b=>b.state==StructureState.Falling)>map.gameplay.maxFalling||s.blocks.Where(b=>b.state==StructureState.Falling||b.state==StructureState.Telegraph).Select(b=>b.group).Distinct().Count()>map.gameplay.maxCollapses)return false;
            int previous=0;foreach(var e in s.events){if(e==null||e.sequence<=previous||e.sequence>s.sequence||e.tick<0||e.tick>s.tick||!Enum.IsDefined(typeof(StructureEventKind),e.kind)||!MathUtil.Finite(e.amount)||!NativeMapRules.Finite(e.point)||!NativeMapRules.Finite(e.normal)||!NativeMapRules.Finite(e.impulse)||!NativeMapRules.Finite(e.direction)||e.cause!=null&&e.cause.Length>64||!string.IsNullOrEmpty(e.block)&&!defs.ContainsKey(e.block)||!string.IsNullOrEmpty(e.group)&&!map.gameplay.groups.Any(g=>g.id==e.group))return false;previous=e.sequence;}
            var ids=(map.gameplay.hazards??Array.Empty<MapHazard>()).Select(h=>h.id).ToArray();return s.hazards.Length==ids.Length&&s.hazards.All(h=>h!=null&&ids.Contains(h.id)&&h.phase>=-1&&h.phase<=2&&h.tick>=0&&h.tick<=s.tick)&&s.hazards.Select(h=>h.id).Distinct().Count()==ids.Length;
        }
        public void Restore(StructureSnapshot snapshot)
        {
            if(!Valid(content,map,snapshot))throw new InvalidOperationException("Invalid structural snapshot");
            tick=snapshot.tick;revision=snapshot.revision;sequence=snapshot.sequence;blocks.Clear();foreach(var b in snapshot.blocks)blocks.Add(b.id,b.Copy());events.Clear();events.AddRange(snapshot.events.Select(e=>e.Copy()));hazards.Clear();foreach(var h in snapshot.hazards)hazards.Add(h.id,h.Copy());
        }
    }
}
