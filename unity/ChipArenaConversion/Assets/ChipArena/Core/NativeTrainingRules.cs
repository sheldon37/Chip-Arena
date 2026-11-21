using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class TrainingRail { public V3 a,b; }
    [Serializable] public sealed class TrainingPad { public string id,kind;public V3 pos;public float yaw,distance;public TrainingRail rail; }
    [Serializable] public sealed class TrainingReadout
    { public string pad,source;public int actor,sourceActor;public float total,last,at,distance,dps;public bool headshot; }
    public static class NativeTrainingRules
    {
        public const int FirstDummy=1000,MaxPads=28;
        public static bool RoomFits(string map,IEnumerable<NativeSeat> seats,bool teams)
        {var all=seats.ToArray();return map!="breakwater"||all.Length<=4&&(!teams||all.GroupBy(s=>s.team).Count()==2&&all.GroupBy(s=>s.team).All(g=>g.Count()<=2));}
        public static void AddDummies(NativeMatch match,ContentMap map)
        {for(int i=0;i<map.trainingPads.Length;i++)match.Seats.Add(new NativeSeat{id=FirstDummy+i,name="Target "+(i+1),spec="pistol",team=1,bot=true,difficulty="dummy"});}
        public static V3 Position(TrainingPad pad,double seconds)
        {
            if(pad.kind!="strafe"||pad.rail==null)return pad.pos;
            var a=pad.rail.a;var b=pad.rail.b;double length=(b-a).Length;
            // Sinusoid starts at the pad, turns smoothly, peak rail speed 2.5 m/s.
            float u=(float)(.5+.5*Math.Sin(seconds*5/Math.Max(.001,length)));
            return a+(b-a)*u;
        }
        public static void Validate(ContentMap map)
        {
            var pads=map.trainingPads??Array.Empty<TrainingPad>();
            if(map.id!="training"){if(pads.Length!=0)throw new InvalidOperationException("Training pads on PvP map");return;}
            if(map.rotation||pads.Length!=MaxPads||pads.Any(p=>p==null)||pads.Select(p=>p.id).Distinct().Count()!=pads.Length)throw new InvalidOperationException("Training catalog");
            foreach(var p in pads)if(p==null||string.IsNullOrEmpty(p.id)||!NativeMapRules.Finite(p.pos)||!MathUtil.Finite(p.yaw)||!MathUtil.Finite(p.distance)||p.distance<0||!new[]{"static","strafe","melee"}.Contains(p.kind)||p.kind=="strafe"&&(p.rail==null||!NativeMapRules.Finite(p.rail.a)||!NativeMapRules.Finite(p.rail.b)||(p.rail.b-p.rail.a).Length<.1f))throw new InvalidOperationException("Training pad");
        }
        public static bool ValidFrame(ContentMap map,(int id,bool bot,int team)[] seats,TrainingReadout[] rows)
        {
            if(map.id!="training")return rows==null||rows.Length==0;
            int count=map.trainingPads?.Length??0;
            if(count!=MaxPads||seats.Count(s=>s.id<FirstDummy)>8||seats.Any(s=>s.id>=FirstDummy&&(!s.bot||s.team!=1||s.id>=FirstDummy+count)||s.id<FirstDummy&&(s.bot||s.team!=0)))return false;
            if(rows==null||rows.Length!=count||rows.Select(r=>r?.actor).Distinct().Count()!=count)return false;
            foreach(var r in rows)if(r==null||r.actor<FirstDummy||r.actor>=FirstDummy+count||r.pad!=map.trainingPads[r.actor-FirstDummy].id||!seats.Any(s=>s.id==r.actor)||r.source!=null&&r.source.Length>100||new[]{r.total,r.last,r.at,r.distance,r.dps}.Any(v=>!MathUtil.Finite(v)||v<0)||r.sourceActor!=0&&!seats.Any(s=>s.id==r.sourceActor))return false;
            return true;
        }
    }
    public sealed class TrainingDamageMeter
    {
        private readonly Queue<(float time,float damage)> hits=new Queue<(float,float)>();private float window;
        public readonly TrainingReadout Row;
        public TrainingDamageMeter(TrainingPad pad,int actor){Row=new TrainingReadout{pad=pad.id,actor=actor};}
        public void Hit(float now,float damage,bool head,int sourceActor,string source,float distance)
        {if(damage<=0)return;hits.Enqueue((now,damage));window+=damage;Row.total+=damage;Row.last=damage;Row.headshot=head;Row.sourceActor=sourceActor;Row.source=source;Row.distance=distance;Row.at=now;Read(now);}
        public TrainingReadout Read(float now)
        {while(hits.Count>0&&hits.Peek().time<=now-5)window-=hits.Dequeue().damage;Row.dps=Math.Max(0,window)/5;return new TrainingReadout{pad=Row.pad,actor=Row.actor,sourceActor=Row.sourceActor,source=Row.source,total=Row.total,last=Row.last,headshot=Row.headshot,distance=Row.distance,at=Row.at,dps=Row.dps};}
    }
}
