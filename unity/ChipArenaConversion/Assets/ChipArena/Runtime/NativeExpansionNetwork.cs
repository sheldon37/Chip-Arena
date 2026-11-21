using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeAbilityExpansionFrame
    {
        public float knife,hook,blink,sky,rain=-100;public bool skyAir,blinkSecond,diving,crouched;public Vector3 stepFrom;
        public bool Valid()=>NativeExpansionWire.Numbers(knife,hook,blink,sky,rain)&&NativeExpansionWire.Vector(stepFrom);
    }
    [Serializable] public sealed class NativeExpansionAreaFrame
    {
        public int owner,target;public string kind;public Vector3 point,to;public float at,until,next,damage,radius;
        public bool Valid()=>owner>0&&target>=0&&kind!=null&&kind.Length<=64&&NativeExpansionWire.Vector(point)&&NativeExpansionWire.Vector(to)&&NativeExpansionWire.Numbers(at,until,next,damage,radius)&&until>=at&&until-at<=60&&damage>=0&&damage<=100000&&radius>=0&&radius<=80;
    }
    [Serializable] public sealed class NativeExpansionEchoFrame
    {
        public int owner,target,id,attack,bounces,pierces;public string kind,spec,detail;public Vector3 point,velocity;public float at,damage,radius,gravity,life,travel,burn,poison,curve;public bool direct;public ChipCount[] mods;
        public bool Valid(ConversionContent c)=>owner>0&&id>0&&target>=0&&kind!=null&&kind.Length<=64&&c.weapons.Any(w=>w.id==spec)&&NativeExpansionWire.Vector(point)&&NativeExpansionWire.Vector(velocity)&&NativeExpansionWire.Numbers(at,damage,radius,gravity,life,travel,burn,poison,curve)&&damage>=0&&damage<=100000&&radius>0&&radius<=20&&life>=0&&life<=60&&travel>=0&&travel<=10000&&(mods==null||mods.Length<=128&&mods.Where(m=>m!=null).Select(m=>m.id).Distinct().Count()==mods.Length&&mods.All(m=>m!=null&&m.id!=null&&m.id.Length<=64&&m.count>=0&&m.count<=100));
    }
    public static class NativeExpansionWire
    {
        public static bool Numbers(params float[] values)=>values.All(v=>MathUtil.Finite(v)&&Math.Abs(v)<10000000);
        public static bool Vector(Vector3 v)=>Numbers(v.x,v.y,v.z);
        public static NativeWildState Copy(NativeWildState s)=>s==null?new NativeWildState():s.Copy();
        public static bool Valid(NativeWildState s)=>s==null||s.Round>=0&&s.Round<1000000&&s.Spin>=0&&s.Spin<=1&&Vector(s.BatterFrom)&&Vector(s.BatterDir)&&Numbers(s.SpinAt,s.Spin,s.ReplyUntil,s.HayAt,s.DownUntil,s.ImmuneUntil,s.AuraAt,s.ReloadAt,s.StompAt,s.TwinAt,s.BatterAt,s.PeakY,s.PreviousY,s.LastYaw,s.YawRate);
        public static bool Valid(NativeWildAffliction a)=>a!=null&&a.By>=0&&a.Target>0&&a.Kind!=null&&a.Kind.Length<=64&&a.Count>=0&&a.Count<=1000000&&a.Stacks>=0&&a.Stacks<=3&&a.Damage>=0&&a.Damage<=100000&&Numbers(a.Until,a.Next,a.Damage)&&Vector(a.Point)&&Vector(a.Direction);
    }
    public sealed partial class NativeArena
    {
        private void CaptureExpansion(NativeGameFrame f)
        {
            f.wildAfflictions=wildAfflictions.Select(a=>a.Copy()).ToArray();
            f.expansionAreas=expansionAreas.Select(a=>new NativeExpansionAreaFrame{owner=a.Owner.Seat.id,target=a.Target,kind=a.Kind,point=a.Point,to=a.To,at=a.At,until=a.Until,next=a.Next,damage=a.Damage,radius=a.Radius}).ToArray();
            f.expansionEchoes=expansionEchoes.Select(e=>new NativeExpansionEchoFrame{owner=e.Owner.Seat.id,target=e.Target,id=e.Shot.Id,attack=e.Shot.Attack,kind=e.Kind,spec=e.Shot.Spec,detail=e.Shot.Detail,point=e.Shot.Position,velocity=e.Shot.Velocity,at=e.At,damage=e.Shot.Damage,radius=e.Shot.Radius,gravity=e.Shot.Gravity,life=e.Shot.Life,travel=e.Shot.Travel,burn=e.Shot.Burn,poison=e.Shot.Poison,curve=e.Shot.ExpansionCurve,bounces=e.Shot.Bounces,pierces=e.Shot.Pierces,direct=e.Shot.Direct,mods=e.Shot.Mods.Select(m=>new ChipCount{id=m.Key,count=m.Value}).ToArray()}).ToArray();
        }
        private void ApplyExpansion(NativeGameFrame f)
        {
            wildAfflictions.Clear();if(f.wildAfflictions!=null)wildAfflictions.AddRange(f.wildAfflictions.Select(a=>a.Copy()));expansionAreas.Clear();expansionEchoes.Clear();
            foreach(var a in f.expansionAreas??Array.Empty<NativeExpansionAreaFrame>()){var p=Pilots.Find(p=>p.Seat.id==a.owner);if(p!=null)expansionAreas.Add(new ExpansionArea{Owner=p,Target=a.target,Kind=a.kind,Point=a.point,To=a.to,At=a.at,Until=a.until,Next=a.next,Damage=a.damage,Radius=a.radius});}
            foreach(var e in f.expansionEchoes??Array.Empty<NativeExpansionEchoFrame>())
            {var p=Pilots.Find(p=>p.Seat.id==e.owner);if(p==null)continue;expansionEchoes.Add(new ExpansionEcho{Owner=p,Target=e.target,At=e.at,Kind=e.kind,Shot=new NativeBullet{Id=e.id,Attack=e.attack,Owner=e.owner,Original=e.owner,Spec=e.spec,Detail=e.detail,Born=e.at,Position=e.point,Velocity=e.velocity,BaseVelocity=e.velocity,Damage=e.damage,Radius=e.radius,Gravity=e.gravity,Life=e.life,Travel=e.travel,Burn=e.burn,Poison=e.poison,ExpansionCurve=e.curve,Bounces=e.bounces,Pierces=e.pierces,Direct=e.direct,TechniqueNoHead=!e.direct,Mods=(e.mods??Array.Empty<ChipCount>()).ToDictionary(m=>m.id,m=>m.count)}});nextBullet=Math.Max(nextBullet,e.id+1);}
        }
    }
}
