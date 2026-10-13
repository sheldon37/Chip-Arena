using System;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeAcidRecord
    {
        public int id,actor;public V3 from,to;public float at,flight,damageScale=1;
        public NativeAcidRecord Copy()=>(NativeAcidRecord)MemberwiseClone();
        public bool Valid()=>id>0&&actor>=10000&&Good(at)&&at>=0&&Good(flight)&&flight>=.35f&&flight<=3&&Good(damageScale)&&damageScale>0&&damageScale<=100&&Point(from)&&Point(to);
        private static bool Good(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        private static bool Point(V3 v)=>Good(v.x)&&Good(v.y)&&Good(v.z)&&Math.Abs(v.x)<10000&&Math.Abs(v.y)<10000&&Math.Abs(v.z)<10000;
        public static bool ValidAll(NativeAcidRecord[] records)
        {if(records==null)return true;if(records.Length>16)return false;for(int i=0;i<records.Length;i++){if(records[i]==null||!records[i].Valid())return false;for(int j=0;j<i;j++)if(records[j].id==records[i].id)return false;}return true;}
    }
    public static class NativeSpitterRules
    {
        public const int Cap=4;
        public const float Hit=.8f,Cycle=2.6f,Range=11,Radius=1.3f,Duration=4,DirectRadius=.6f,Damage=7,Dps=4,DotDuration=.5f,DotInterval=.25f,CorrosionInterval=.5f,Corrosion=6,DirectCorrosion=10;
        public static bool Interrupt(float damage,float now,float attack,bool struck)=>damage>0&&!struck&&now>=attack&&now<attack+Hit;
        public static float Retry(float now)=>now+Cycle*.6f;
        public static V3 Lead(V3 origin,V3 target,V3 velocity)
        {var delta=target-origin;delta.y=0;var lead=target+velocity*NativeHordeFx.Flight(delta.Length);delta=lead-origin;delta.y=0;if(delta.Length>Range){delta=delta.Normalized*Range;lead.x=origin.x+delta.x;lead.z=origin.z+delta.z;}lead.y=target.y;return lead;}
        public static bool Touch(V3 floor,V3 center,float halfHeight,float radius,float area)
        {var d=center-floor;return Math.Abs(d.y-halfHeight)<.6f&&(double)d.x*d.x+(double)d.z*d.z<=(double)(radius+area)*(radius+area);}
        public static float KeepDistance(float distance)=>distance<4?-1:distance<=8?0:1;
    }
}
