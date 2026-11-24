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
        public static bool IsPad(int seatId)=>seatId>=FirstDummy&&seatId<FirstDummy+MaxPads;
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
        private static bool ValidMeter(float value)=>value>=0&&MathUtil.Finite(value);
        public static bool ValidFrame(ContentMap map,(int id,bool bot,int team,bool devDummy)[] seats,TrainingReadout[] rows)
        {
            if(map.id!="training")return rows==null||rows.Length==0;
            if(map.trainingPads?.Length!=MaxPads||rows==null||rows.Length!=MaxPads)return false;
            uint pads=0,reported=0;
            int participants=0;
            foreach(var seat in seats)
            {
                if(IsPad(seat.id))
                {
                    if(!seat.bot||seat.devDummy||seat.team!=1)return false;
                    pads|=1u<<(seat.id-FirstDummy);
                }
                else if(seat.id>=FirstDummy||++participants>8||
                    (seat.devDummy?!seat.bot:seat.bot||seat.team!=0))return false;
            }
            foreach(var row in rows)
            {
                if(row==null||!IsPad(row.actor))return false;
                int index=row.actor-FirstDummy;uint bit=1u<<index;
                if((pads&bit)==0||(reported&bit)!=0||map.trainingPads[index].id!=row.pad)return false;
                if(row.source!=null&&row.source.Length>100||!ValidMeter(row.total)||!ValidMeter(row.last)||
                    !ValidMeter(row.at)||!ValidMeter(row.distance)||!ValidMeter(row.dps))return false;
                if(row.sourceActor!=0&&!Array.Exists(seats,s=>s.id==row.sourceActor))return false;
                reported|=bit;
            }
            return true;
        }
    }
    public sealed class TrainingDamageMeter
    {
        private readonly Queue<(float time,float damage)> hits=new Queue<(float,float)>();private float window;
        private TrainingReadout row;
        public TrainingDamageMeter(TrainingPad pad,int actor){row=new TrainingReadout{pad=pad.id,actor=actor};}
        public void Hit(float now,float damage,bool head,int sourceActor,string source,float distance)
        {
            if(damage<=0)return;
            hits.Enqueue((now,damage));window+=damage;
            row=new TrainingReadout{pad=row.pad,actor=row.actor,total=row.total+damage,last=damage,headshot=head,
                sourceActor=sourceActor,source=source,distance=distance,at=now,dps=Math.Max(0,window)/5};
            Expire(now);
        }
        private void Expire(float now)
        {
            if(hits.Count==0||hits.Peek().time>now-5)return;
            while(hits.Count>0&&hits.Peek().time<=now-5)window-=hits.Dequeue().damage;
            row=new TrainingReadout{pad=row.pad,actor=row.actor,total=row.total,last=row.last,headshot=row.headshot,
                sourceActor=row.sourceActor,source=row.source,distance=row.distance,at=row.at,dps=Math.Max(0,window)/5};
        }
        // Frames retain these rows for replay. Replace a changed row so earlier frames keep their own values.
        public TrainingReadout Read(float now){Expire(now);return row;}
    }
}
