using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeBreachRecord
    {
        public int entry;public float breakAt,restoreAt;
        public NativeBreachRecord ViewCopy()=>(NativeBreachRecord)MemberwiseClone();
        public bool Valid()=>entry>=0&&entry<256&&MathUtil.Finite(breakAt)&&MathUtil.Finite(restoreAt)&&breakAt>=0&&restoreAt>=breakAt&&restoreAt<1e8;
    }
    public static class NativeWardEntries
    {
        public static bool Drop(PveEntry e)=>e.kind=="drop"||e.kind=="vent"&&e.surface=="ceiling";
        public static bool Allows(PveEntry e,string kind)=>kind=="hulk"?(e.kind=="breach"||e.kind=="drop"&&e.hulk):e.kind!="breach"&&(e.kind!="drop"||kind=="brute"&&!e.hulk);
        public static (float walk,float duration) Timeline(PveEntry e,bool first,string kind)
        {
            if(e.kind=="breach")return(NativeBreach.Knock,NativeBreach.Knock+NativeBreach.Through);
            if(Drop(e))return(0,NativeHordeDrop.Duration(NativeHordeDrop.HeightOf(e),kind=="hulk"?NativeHordeDrop.Hulk:NativeHordeDrop.Brute));
            return NativeEntryRules.Timeline(e.kind,first);
        }
        public static void Validate(NativePveContent data,ContentMap map)
        {
            void Need(bool ok,string why){if(!ok)throw new InvalidOperationException("Ward entries: "+why);}
            Need(data.entries!=null&&data.entries.Length<=256&&data.entries.Select(e=>e?.id).Distinct().Count()==data.entries.Length,"IDs");
            Need(data.boss?.spawn!=null&&data.boss.rescue!=null&&MathUtil.Finite(data.boss.yaw),"boss");
            foreach(var e in data.entries)
            {
                Need(e!=null&&e.e>=0&&e.e<data.entries.Length&&ReferenceEquals(data.entries[e.e],e)&&!string.IsNullOrEmpty(e.id),"index");
                Need(new[]{"vent","cell","door","drop","breach"}.Contains(e.kind)&&e.from!=null&&e.@exit!=null&&NativeMapRules.Finite(e.from.Position())&&NativeMapRules.Finite(e.@exit.Position())&&MathUtil.Finite(e.yaw),"point/kind");
                Need(e.level>=0&&e.level<=1&&e.song>=-1&&e.song<=1&&MathUtil.Finite(e.height)&&MathUtil.Finite(e.clear)&&e.clear>=0,"fields");
                if(e.kind=="vent")Need(string.IsNullOrEmpty(e.surface)||new[]{"floor","wall","ceiling"}.Contains(e.surface),"surface");
                if(Drop(e))Need(e.height>0,"height");
                if(e.kind=="breach")Need(map.blocks.Any(b=>b.id==e.block&&b.role=="static")&&NativeMapRules.Box(e.center,e.size)&&NativeMapRules.Finite(e.normal)&&Math.Abs(e.normal.Length-1)<.01,"panel");
            }
            Need(data.entries.Count(e=>e.song>=0)==8&&data.entries.Take(8).All(e=>e.song>=0&&e.kind=="vent"&&e.pit),"song entries");
        }
    }
}
