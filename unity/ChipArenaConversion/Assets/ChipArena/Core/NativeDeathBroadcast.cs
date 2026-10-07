using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public sealed class NativeBroadcastItem {public int Seed,Victim,Owner,Slot;public float At;public string Text;}
    public sealed class NativeDeathBroadcast
    {
        public readonly List<NativeBroadcastItem> Items=new List<NativeBroadcastItem>();private readonly Dictionary<int,float> seen=new Dictionary<int,float>();private float next=-1,last=-1;private int slot;
        public void Tick(float time,bool clear=false){if(clear||time<last){Items.Clear();seen.Clear();next=-1;slot=0;}last=time;Items.RemoveAll(x=>time-x.At>=5.6f);foreach(int key in seen.Where(p=>time-p.Value>30).Select(p=>p.Key).ToArray())seen.Remove(key);}
        public void Add(int seed,int victim,int owner,float deathAt,float now,string name,string killer)
        {if(seen.ContainsKey(seed))return;seen[seed]=deathAt;if(now-deathAt>2||now<deathAt)return;float at=Math.Max(deathAt,next);next=at+1.5f;Items.Add(new NativeBroadcastItem{Seed=seed,Victim=victim,Owner=owner,Slot=slot++%3,At=at,Text=killer==null?name+" made an oopsie.":name+" was killed by "+killer+"."});}
    }
}
