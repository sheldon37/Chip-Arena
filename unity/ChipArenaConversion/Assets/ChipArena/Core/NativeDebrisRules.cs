using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public static class NativeDebrisRules
    {
        public static double Damage(double mass,double speed)
        {if(double.IsNaN(mass)||double.IsInfinity(mass)||mass<=0||double.IsNaN(speed)||double.IsInfinity(speed)||speed<3.6)return 0;return Math.Min(65,.035*Math.Max(0,.5*mass*speed*speed-45));}
    }
    public sealed class NativeImpactLedger
    {
        private sealed class Pair {public double last=double.NegativeInfinity;public bool touching,seen;}
        private readonly Dictionary<string,Pair> pairs=new Dictionary<string,Pair>();private readonly Dictionary<string,(double at,double damage)> groups=new Dictionary<string,(double,double)>();
        public void Begin(){foreach(var p in pairs.Values)p.seen=false;}
        public double Claim(string pair,string group,double time,double damage,double groupCap=75)
        {if(!pairs.TryGetValue(pair,out var p)){p=new Pair();pairs[pair]=p;}p.seen=true;bool allowed=!p.touching&&time-p.last>=.4;p.touching=true;if(!allowed||damage<=0)return 0;p.last=time;if(!groups.TryGetValue(group,out var g)||time-g.at>.35)g=(time,0);double amount=Math.Min(damage,Math.Max(0,groupCap-g.damage));groups[group]=(g.at,g.damage+amount);return amount;}
        public void End(double time){foreach(var item in pairs.ToArray()){if(!item.Value.seen)item.Value.touching=false;if(time-item.Value.last>10&&!item.Value.touching)pairs.Remove(item.Key);}foreach(var g in groups.ToArray())if(time-g.Value.at>2)groups.Remove(g.Key);}
        public void Clear(){pairs.Clear();groups.Clear();}
    }
}
