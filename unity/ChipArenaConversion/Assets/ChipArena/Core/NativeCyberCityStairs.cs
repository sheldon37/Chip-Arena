using System;
using System.Linq;

namespace ChipArena.UnityConversion
{
    // Collision-only description. The authored stair blocks remain the presentation contract.
    public sealed class NativeCutStair
    {
        public string Id;
        public ContentBlock[] Steps;
        public V3 Low,High,Landing;
        public float Width,Bottom;
        public V3 Direction=>new V3(High.x-Low.x,0,High.z-Low.z).Normalized;
        public V3 Street=>High+Direction*1.2f;
    }
    public static class NativeCyberCityStairs
    {
        private static readonly string[] sides={"nw","ne","se","sw"};
        public static bool IsTread(string mapId,string id)=>mapId=="cybercity"&&id!=null&&
            sides.Any(side=>id.StartsWith("cybercity-pit-"+side+"-stair-",StringComparison.Ordinal));
        public static NativeCutStair[] Flights(ContentMap map)
        {
            if(map.id!="cybercity")return Array.Empty<NativeCutStair>();
            return sides.Select(side=>
            {
                string id="cybercity-pit-"+side;
                var steps=map.blocks.Where(b=>b.id.StartsWith(id+"-stair-",StringComparison.Ordinal))
                    .OrderBy(b=>b.pos.y+b.size.y*.5f).ToArray();
                if(steps.Length!=16)throw new InvalidOperationException(id+": expected sixteen authored treads");
                var first=steps[0];var last=steps[steps.Length-1];float direction=Math.Sign(last.pos.x-first.pos.x);
                var landing=map.blocks.Single(b=>b.id==id+"-landing");
                return new NativeCutStair{Id=id,Steps=steps,Width=first.size.z,Bottom=first.pos.y-first.size.y*.5f,
                    Low=new V3(first.pos.x-direction*first.size.x*.5f,first.pos.y+first.size.y*.5f,first.pos.z),
                    // The last visible tread is .25m below the adjacent street. Include that final rise in the ramp.
                    High=new V3(last.pos.x+direction*last.size.x*.5f,last.pos.y+last.size.y*.5f+.25f,last.pos.z),
                    Landing=new V3(landing.pos.x,landing.pos.y+landing.size.y*.5f,landing.pos.z)};
            }).ToArray();
        }
    }
}
