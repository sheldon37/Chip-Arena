using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    public static class NativePracticeRules
    {
        // Stable account-storage bits. Never derive these from the visual roster order.
        public static readonly string[] Bosses = {"siren","station","converter","hum","deepend","noctiel"};
        public const int All = 63;
        public static int Bit(string id)
        {
            int index = Array.IndexOf(Bosses,id);
            return index < 0 ? 0 : 1 << index;
        }
        public static int Phases(string id) => NativeExpeditionCatalogue.Bosses.FirstOrDefault(b=>b.Id==id)?.Phases??0;
        public static bool Available(string id,int earned,bool developer,bool mock)
            => Bit(id)!=0 && ((earned & Bit(id))!=0 || developer && !mock);
    }

    public sealed class NativeLabBuild
    {
        public const int Slots = 5;
        public readonly string Weapon, Techniques;
        public readonly string[] Chips;
        public NativeLabBuild(string weapon, string techniques, string[] chips)
        {
            Weapon=weapon;
            Techniques=string.Join(",", NativeTechniqueLoadout.Normalize(techniques).Split(',').Where(x=>x.StartsWith(weapon+":",StringComparison.Ordinal)));
            Chips=(string[])chips.Clone();
        }
        public string Code => string.Join("|","CA1",Weapon,Techniques,string.Join(",",Chips));
        public Dictionary<string,int> Counts() => Chips.GroupBy(x=>x).ToDictionary(g=>g.Key,g=>g.Count());
        public static NativeLabBuild Decode(string code,ConversionContent data,NativeAccess access)
        {
            if (string.IsNullOrEmpty(code) || code.Length>1024) return null;
            var parts=code.Trim().Split('|');
            if(parts.Length!=4 || parts[0]!="CA1")return null;
            var w=data.weapons.FirstOrDefault(item=>item.id==parts[1]);
            if(w==null || w.devOnly && !(access??NativeAccess.Guest).Allows("weapon:"+w.id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))return null;
            var chips=parts[3].Length==0 ? Array.Empty<string>() : parts[3].Split(',');
            if(chips.Length>Slots)return null;
            var build=new NativeLabBuild(w.id,parts[2],chips);
            if(build.Techniques!=parts[2])return null;
            var held=new Dictionary<string,int>();
            foreach(var id in chips)
            {
                if(!NativeContentRules.Eligible(data,w.id,held,build.Techniques).Any(c=>c.id==id))return null;
                held.TryGetValue(id,out int n);held[id]=n+1;
            }
            return build;
        }
    }

    public sealed class NativeLabSample
    {
        public float Started {get; private set;} = -1;
        public float Total {get; private set;}
        public float Last {get; private set;}
        public int Hits {get; private set;}
        public readonly Dictionary<string,int> Procs = new Dictionary<string,int>();
        public bool Finished(float now)=>Started>=0 && now>=Started+10;
        public void Hit(float now,float damage)
        {
            if(damage<=0 || Finished(now))return;
            if(Started<0)Started=now;
            Last=damage;Total+=damage;Hits++;
        }
        public void Proc(float now,string chip)
        {
            if(Started<0 || Finished(now) || string.IsNullOrEmpty(chip))return;
            Procs.TryGetValue(chip,out int n);Procs[chip]=n+1;
        }
    }
}
