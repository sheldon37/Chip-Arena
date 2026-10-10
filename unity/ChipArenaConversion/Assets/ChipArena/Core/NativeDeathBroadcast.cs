using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    /// <summary>One sky broadcast (death-announcements.ts Announcement): the victim's name (and upper-cased, as the nameplate
    /// draws it), the killer's name or null, and the HUD line.</summary>
    public sealed class NativeBroadcastItem {public int Seed,Victim,Owner,Slot;public float At;public string Text,Name,Upper,Killer;}
    public sealed class NativeDeathBroadcast
    {
        public readonly List<NativeBroadcastItem> Items=new List<NativeBroadcastItem>();private readonly Dictionary<int,float> seen=new Dictionary<int,float>();private readonly List<int> expired=new List<int>();private float next=-1,last=-1;private int slot;
        public void Tick(float time,bool clear=false)
        {
            if(clear||time<last){Items.Clear();seen.Clear();next=-1;slot=0;}last=time;
            for(int i=Items.Count-1;i>=0;i--)if(time-Items[i].At>=NativeBroadcastFx.Duration)Items.RemoveAt(i);
            expired.Clear();foreach(var p in seen)if(time-p.Value>30)expired.Add(p.Key);foreach(int key in expired)seen.Remove(key);
        }
        public bool Seen(int seed)=>seen.ContainsKey(seed);
        public void Add(int seed,int victim,int owner,float deathAt,float now,string name,string killer)
        {
            if(seen.ContainsKey(seed))return;seen[seed]=deathAt;if(now-deathAt>2||now<deathAt)return;float at=Math.Max(deathAt,next);next=at+NativeBroadcastFx.Spacing;
            Items.Add(new NativeBroadcastItem{Seed=seed,Victim=victim,Owner=owner,Slot=slot++%3,At=at,Name=name,Upper=(name??"").ToUpperInvariant(),Killer=killer,Text=killer==null?name+" made an oopsie.":name+" was killed by "+killer+"."});
        }
        /// <summary>death-announcements.ts update: only the newest three launched broadcasts show (active.filter(started).slice(-3)).</summary>
        public bool Shown(int index,float time)
        {
            if(index<0||index>=Items.Count||time<Items[index].At)return false;int newer=0;
            for(int i=index+1;i<Items.Count;i++)if(time>=Items[i].At)newer++;return newer<NativeBroadcastFx.Visible;
        }
    }
}
