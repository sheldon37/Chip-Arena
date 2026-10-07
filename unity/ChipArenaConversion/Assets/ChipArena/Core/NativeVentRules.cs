using System;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeEntryView {public int entry;public string kind,mode;public float openAt,closeAt;}
    public static class NativeVentRules
    {
        public static float Open(NativeEntryView entry,float time,bool drawn=false)
        {if(entry==null)return 0;float start=entry.openAt+(entry.kind=="vent"&&entry.mode=="spawn"?.35f:0),end=entry.closeAt;if(drawn&&entry.kind=="vent"&&entry.mode=="dive"){start-=.5f;end+=.5f;}float open=entry.kind=="vent"?.45f:entry.kind=="door"?.6f:.5f,close=entry.kind=="vent"?.4f:entry.kind=="door"?.7f:.6f;return MathUtil.Smooth((time-start)/open)*(1-MathUtil.Smooth((time-end)/close));}
        public static NativeEntryView Ward(int index,float time)
        {float offset=index*1.5f,start=offset+((float)Math.Floor((time-offset-5.5f)/12)+1)*12;return new NativeEntryView{entry=index,kind="vent",mode="dive",openAt=start,closeAt=start+4.5f};}
        public static float Warning(NativeEntryView entry,float time){float at=entry.openAt-.5f;return time>=at||time<at-1?0:MathUtil.Smooth(1-(at-time));}
        public static bool Falls(V3 position,V3 vent,float opening)=>opening>=.5f&&Math.Abs(position.x-vent.x)<=.78f&&Math.Abs(position.z-vent.z)<=.78f&&position.y<=.9f+.35f;
    }
}
