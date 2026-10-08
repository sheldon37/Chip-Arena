using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeHazard
    { public string id,kind;public float x,y,z,sx,sz,extension;public bool active,warning; }
    public static class NativeHazards
    {
        public static List<NativeHazard> At(string map,double time)
        {
            var result=new List<NativeHazard>();
            if(map=="sawmill")for(int i=0;i<2;i++)result.Add(new NativeHazard{id="saw-"+i,kind="saw",x=(float)(Math.Sin(time*.65+i*Math.PI)*17),y=1.05f,z=i==0?-8:8,sx=2.4f,sz=.65f,active=true});
            if(map=="spikevault")for(int i=0;i<2;i++)for(int j=0;j<2;j++)
            {double cycle=(time+i*1.5+j*.65)%5;result.Add(new NativeHazard{id="spikes-"+i+"-"+j,kind="spikes",x=i==0?-8:8,z=j==0?-5:5,sx=6,sz=5,active=cycle>=3.6,warning=cycle>=2.6&&cycle<3.6,extension=(float)(cycle<3.6?0:cycle<3.78?(cycle-3.6)/.18:cycle<4.65?1:Math.Max(0,(5-cycle)/.35))});}
            if(map=="lavaworks")for(int i=0;i<2;i++)result.Add(new NativeHazard{id="lava-"+i,kind="lava",x=i==0?-5:5,y=-1.1f,sx=4,sz=34,active=true});
            return result;
        }
        public static bool InLava(string map,V3 p)=>map=="lavaworks"&&Math.Abs(p.z)<17&&(Math.Abs(p.x+5)<2||Math.Abs(p.x-5)<2)&&p.y<-.45f&&p.y>-4;
        // Each channel owns its contacts; testing the union for both channels would double each tick.
        public static bool InLavaChannel(NativeHazard h,V3 p)=>h.kind=="lava"&&Math.Abs(p.x-h.x)<h.sx/2&&Math.Abs(p.z-h.z)<h.sz/2&&p.y<-.45f&&p.y>-4;
    }
}
