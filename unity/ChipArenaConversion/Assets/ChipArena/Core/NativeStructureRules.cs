using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class StructureSupport { public string member,segment; public float w; }
    [Serializable] public sealed class StructureCrush { public V3 center,size; public float damage,push,cap,capHpFraction; }
    [Serializable] public sealed class StructurePose { public string block; public V3 pivot,axis; public float angle,drop,delay; }
    [Serializable] public sealed class StructureNav { public string[] remove,add; }
    [Serializable] public sealed class StructureDefinition
    {
        public string id,building,mode,material;public string[] blocks;public StructureSupport[] supports;
        public float failBelow,telegraph,duration,maxAccel,angle,mass;public float[] stagger;
        public V3 pivot,axis,direction;public StructureCrush crush;public MapVolume[] sweeps;public StructurePose[] poses;public ContentBlock[] rubble;public StructureNav nav;
    }
    [Serializable] public sealed class StructureFrame { public string id;public int state;public float t0; }
    public struct StructureTransform { public V3 position;public ContentRotation rotation; }
    public sealed class StructureChange { public string id,member;public int state;public float t0; }
    public struct StructureDamage { public string member;public int pilot;public float amount,at; }
    public static class NativeStructureRules
    {
        public const int MaxFalling=3,MaxFragments=80;
        public const float ArmDelay=4,MinTelegraph=.8f;
        public static uint Seed(string map,string segment,int round){uint h=2166136261;foreach(byte b in Encoding.UTF8.GetBytes(map+"/"+segment+"/"+round.ToString(System.Globalization.CultureInfo.InvariantCulture)))h=unchecked((h^b)*16777619);return h;}
        public static bool Contains(V3 p,V3 center,V3 size,float padding=0)=>Math.Abs(p.x-center.x)<=size.x/2+padding&&Math.Abs(p.y-center.y)<=size.y/2+padding&&Math.Abs(p.z-center.z)<=size.z/2+padding;
        public static bool Swept(StructureDefinition s,V3 p,float padding=.42f)=>s.sweeps.Any(v=>Contains(p,v.center,v.size,padding));
        public static bool Crosses(V3 a,V3 b,MapVolume box,float pad=0)
        {
            float lo=0,hi=1;bool Slab(float start,float end,float c,float size){float d=end-start,min=c-size/2-pad,max=c+size/2+pad;if(Math.Abs(d)<.000001)return start>=min&&start<=max;float x=(min-start)/d,y=(max-start)/d;lo=Math.Max(lo,Math.Min(x,y));hi=Math.Min(hi,Math.Max(x,y));return lo<=hi;}
            return Slab(a.x,b.x,box.center.x,box.size.x)&&Slab(a.y,b.y,box.center.y,box.size.y)&&Slab(a.z,b.z,box.center.z,box.size.z);
        }
        public static float DamageScale(string role,string kind,float demolition)
        {if(role=="static"||role=="carried"||role=="rubble")return 0;if(role=="structural")return kind=="blast"?Math.Max(0,Math.Min(1.5f,demolition)):0;if(role=="masonry")return (kind=="blast"?1.5f:kind=="melee"?.5f:kind=="bullet"?.25f:1)*demolition;return demolition;}
        public static float Allowance(float maxHp,float already)=>Math.Max(0,Math.Min(60,.6f*maxHp)-already);
        public static int Credit(IEnumerable<StructureDamage> hits,ISet<string> members,float time)
        {var best=hits.Where(x=>x.pilot>0&&x.amount>0&&x.at<=time&&time-x.at<=10&&members.Contains(x.member)).GroupBy(x=>x.pilot).OrderByDescending(g=>g.Sum(x=>x.amount)).ThenByDescending(g=>g.Max(x=>x.at)).ThenBy(g=>g.Key).FirstOrDefault();return best?.Key??0;}
        public static int FallbackCredit(int structural,int lastPilot,float lastAt,float time)=>structural!=0?structural:time>=lastAt&&time-lastAt<=6?lastPilot:0;
        // Cubic ease with zero endpoint velocity: max |acceleration| = 6*distance/duration².
        public static float DropDuration(float distance,float maxAccel)=>Math.Max(.01f,(float)Math.Sqrt(6*Math.Abs(distance)/Math.Min(6.9f,maxAccel)));
        private static V3 Cross(V3 a,V3 b)=>new V3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static V3 Rotate(V3 v,V3 axis,float degrees){axis=axis.Normalized;double a=degrees*Math.PI/180;return v*(float)Math.Cos(a)+Cross(axis,v)*(float)Math.Sin(a)+axis*(V3.Dot(axis,v)*(1-(float)Math.Cos(a)));}
        public static ContentRotation Rotation(V3 axis,float degrees,ContentRotation q)
        {axis=axis.Normalized;double a=degrees*Math.PI/360;var v=axis*(float)Math.Sin(a);float w=(float)Math.Cos(a);return new ContentRotation{x=w*q.x+v.x*q.w+v.y*q.z-v.z*q.y,y=w*q.y-v.x*q.z+v.y*q.w+v.z*q.x,z=w*q.z+v.x*q.y-v.y*q.x+v.z*q.w,w=w*q.w-v.x*q.x-v.y*q.y-v.z*q.z};}
        public static StructureTransform Pose(StructureDefinition s,ContentBlock b,float t0,float time)
        {
            var p=s.poses.FirstOrDefault(x=>x.block==b.id);if(p==null)return new StructureTransform{position=b.pos,rotation=b.rotation};
            float age=Math.Max(0,time-t0-p.delay),duration=s.mode=="pancake"&&p.angle==0?DropDuration(p.drop,s.maxAccel):Math.Max(.01f,s.duration-p.delay);
            float u=MathUtil.Smooth(age/duration),angle=p.angle*u,drop=p.drop*u;
            if(s.mode=="tear-hinge"){angle=p.angle*MathUtil.Smooth(age/.7f);drop=p.drop*MathUtil.Smooth((age-.3f)/.4f);}
            if(s.mode=="topple"){float lean=Math.Sign(p.angle)*8;angle=time<t0?lean*MathUtil.Smooth((time-t0+s.telegraph)/s.telegraph):lean+(p.angle-lean)*u;}
            V3 at=p.pivot+Rotate(b.pos-p.pivot,p.axis,angle);at.y-=drop;
            return new StructureTransform{position=at,rotation=Rotation(p.axis,angle,b.rotation)};
        }
        public static float Duration(StructureDefinition s)=>Math.Max(s.duration,s.poses.Select(p=>p.delay+(s.mode=="pancake"&&p.angle==0?DropDuration(p.drop,s.maxAccel):Math.Max(.01f,s.duration-p.delay))).DefaultIfEmpty(0).Max());
        public static void Validate(ContentMap m)
        {
            var defs=m.segments??Array.Empty<StructureDefinition>();var ids=new HashSet<string>(defs.Select(s=>s.id));var blocks=new HashSet<string>(m.blocks.Select(b=>b.id));var owned=new HashSet<string>();var rubble=new HashSet<string>();
            void Need(bool ok,string msg){if(!ok)throw new InvalidOperationException("Structure "+m.id+": "+msg);}
            Need(ids.Count==defs.Length,"duplicate segments");
            foreach(var s in defs)
            {
                Need(!string.IsNullOrEmpty(s.id)&&new[]{"pancake","tear-hinge","peel","topple"}.Contains(s.mode)&&s.telegraph>=MinTelegraph&&MathUtil.Finite(s.telegraph)&&s.duration>0&&MathUtil.Finite(s.duration)&&s.failBelow>0&&MathUtil.Finite(s.failBelow)&&s.mass>0&&MathUtil.Finite(s.mass),"timing/mode/mass");
                Need(s.supports!=null&&s.supports.Length>0&&s.poses!=null&&s.blocks!=null&&s.rubble!=null&&s.sweeps!=null&&s.sweeps.Length>0&&s.crush!=null,"incomplete definition");
                foreach(var p in s.supports)Need(p.w>0&&MathUtil.Finite(p.w)&&(string.IsNullOrEmpty(p.member)!=string.IsNullOrEmpty(p.segment))&&(string.IsNullOrEmpty(p.member)?ids.Contains(p.segment)&&p.segment!=s.id:blocks.Contains(p.member)&&m.blocks.First(b=>b.id==p.member).role=="structural"),"support");
                Need(NativeMapRules.Box(s.crush.center,s.crush.size)&&s.sweeps.All(v=>NativeMapRules.Box(v.center,v.size)),"crush/sweep geometry");
                Need(s.supports.Sum(p=>p.w)>=s.failBelow,"initially unsupported");
                foreach(var id in s.blocks)Need(blocks.Contains(id)&&owned.Add(id),"block ownership "+id);
                Need(s.poses.Length==s.blocks.Length&&s.poses.Select(p=>p.block).Distinct().Count()==s.poses.Length&&s.poses.All(p=>s.blocks.Contains(p.block)&&m.blocks.First(b=>b.id==p.block).segment==s.id&&p.delay>=0&&MathUtil.Finite(p.delay)&&p.drop>=0&&MathUtil.Finite(p.angle)&&MathUtil.Finite(p.drop)&&NativeMapRules.Finite(p.pivot)&&NativeMapRules.Finite(p.axis)&&p.axis.Length>.99f),"poses");
                if(s.mode=="pancake")Need(s.maxAccel>0&&s.maxAccel<=6.9001f,"rider acceleration");
                foreach(var b in s.rubble)Need(b.role=="rubble"&&!blocks.Contains(b.id)&&rubble.Add(b.id)&&b.rotation!=null&&b.size.x>0&&b.size.y>0&&b.size.z>0,"rubble");
            }
            var seen=new HashSet<string>();var active=new HashSet<string>();void Visit(string id){if(seen.Contains(id))return;Need(active.Add(id),"cyclic supports");foreach(var p in defs.First(s=>s.id==id).supports.Where(p=>!string.IsNullOrEmpty(p.segment)))Visit(p.segment);active.Remove(id);seen.Add(id);}foreach(var s in defs)Visit(s.id);
        }
    }
    public sealed class NativeStructureState
    {
        private readonly StructureDefinition[] definitions;
        private readonly Dictionary<string,StructureFrame> states;
        public int Revision {get;private set;}
        public NativeStructureState(ContentMap m){definitions=(m.segments??Array.Empty<StructureDefinition>()).OrderBy(s=>s.id,StringComparer.Ordinal).ToArray();states=definitions.ToDictionary(s=>s.id,s=>new StructureFrame{id=s.id});}
        public StructureFrame Get(string id)=>states[id];
        public StructureFrame[] Snapshot()=>definitions.Select(d=>{var s=states[d.id];return new StructureFrame{id=s.id,state=s.state,t0=s.t0};}).ToArray();
        public bool Valid(StructureFrame[] values,float time)=>values!=null&&values.Length==states.Count&&values.Count(s=>s!=null&&s.state==2)<=3&&values.Select(s=>s?.id).Distinct().Count()==values.Length&&values.All(s=>s!=null&&states.ContainsKey(s.id)&&s.state>=0&&s.state<=3&&MathUtil.Finite(s.t0)&&s.t0>=0&&s.t0<time+120&&(s.state!=0||s.t0==0)&&(s.state<2||s.t0<=time+.02f));
        public void Restore(StructureFrame[] values,float time){if(!Valid(values,time))throw new ArgumentException("Invalid segments");foreach(var s in values){if(states[s.id].state!=s.state)Revision++;states[s.id]=new StructureFrame{id=s.id,state=s.state,t0=s.t0};}}
        public List<StructureChange> Step(float time,float fightAt,Func<string,bool> alive)
        {
            var changes=new List<StructureChange>();if(time-fightAt<NativeStructureRules.ArmDelay)return changes;
            // Fixed point in ordinal ID order. Ten segments bound work to at most 100 visits.
            bool changed;do{changed=false;foreach(var d in definitions)
            {
                var s=states[d.id];if(s.state!=0)continue;
                bool Intact(StructureSupport p)=>string.IsNullOrEmpty(p.member)?states[p.segment].state==0:alive(p.member);
                if(d.supports.Where(Intact).Sum(p=>p.w)>=d.failBelow)continue;
                float start=time+d.telegraph;var scheduled=definitions.Where(x=>states[x.id].state==1||states[x.id].state==2).ToArray();
                if(scheduled.Length>0)start=Math.Max(start,scheduled.Max(x=>states[x.id].t0)+.3f);
                while(true){var overlaps=scheduled.Where(x=>states[x.id].t0<=start&&states[x.id].t0+NativeStructureRules.Duration(x)>start).ToArray();if(overlaps.Length<NativeStructureRules.MaxFalling)break;start=overlaps.Min(x=>states[x.id].t0+NativeStructureRules.Duration(x))+.001f;}
                s.state=1;s.t0=start;Revision++;changed=true;changes.Add(new StructureChange{id=d.id,state=1,t0=start,member=d.supports.Where(p=>!Intact(p)).Select(p=>p.member??p.segment).OrderBy(x=>x,StringComparer.Ordinal).First()});
            }}while(changed);
            foreach(var d in definitions){var s=states[d.id];if(s.state==1&&time>=s.t0){s.state=2;Revision++;changes.Add(new StructureChange{id=d.id,state=2,t0=s.t0});}if(s.state==2&&time>=s.t0+NativeStructureRules.Duration(d)){s.state=3;Revision++;changes.Add(new StructureChange{id=d.id,state=3,t0=s.t0});}}
            return changes;
        }
    }
}
