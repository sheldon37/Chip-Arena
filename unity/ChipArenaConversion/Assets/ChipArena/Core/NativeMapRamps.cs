using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Authored geometry is retained for drawing and corpse physics. Only the pilot world uses these wedges.
    public static class NativeMapRamps
    {
        public static bool IsVisual(ContentMap map,string id)=>(map.ramps??Array.Empty<MapRamp>()).Any(r=>r.blocks.Contains(id));
        public static NativeCutStair[] Flights(ContentMap map)=>(map.ramps??Array.Empty<MapRamp>()).Select(r=>
            new NativeCutStair{Id=r.id,Low=r.low,High=r.high,Width=r.width,Bottom=r.bottom,Landing=r.low,
                Steps=r.blocks.Select(id=>map.blocks.Single(b=>b.id==id)).ToArray()}).ToArray();
        public static void Validate(ContentMap map)
        {
            void Need(bool ok,string why){if(!ok)throw new InvalidOperationException("Map ramps "+map.id+": "+why);}
            var ramps=map.ramps??Array.Empty<MapRamp>();
            Need(ramps.All(r=>r!=null)&&ramps.Select(r=>r.id).Distinct().Count()==ramps.Length,"IDs");
            foreach(var r in ramps)
            {
                Need(!string.IsNullOrEmpty(r.id)&&(r.kind=="stair"||r.kind=="ramp"),"kind/id");
                Need(NativeMapRules.Finite(r.low)&&NativeMapRules.Finite(r.high)&&MathUtil.Finite(r.width)&&MathUtil.Finite(r.bottom)&&r.width>NativeMapRules.PilotRadius*2&&r.bottom<=r.low.y&&r.high.y>r.low.y,"geometry");
                var d=r.high-r.low;float run=(float)Math.Sqrt(d.x*d.x+d.z*d.z);Need(run>0&&d.y/run<1,"slope");
                Need(r.blocks!=null&&r.blocks.Length>0&&r.blocks.Distinct().Count()==r.blocks.Length&&r.blocks.All(id=>map.blocks.Any(b=>b.id==id&&b.role=="static")),"visual blocks");
            }
            foreach(var id in map.navHints?.collisionOff??Array.Empty<string>())Need(map.blocks.Any(b=>b.id==id&&b.role=="static"),"collisionOff");
            foreach(var d in map.navHints?.drops??Array.Empty<MapDrop>())Need(d!=null&&!string.IsNullOrEmpty(d.id)&&NativeMapRules.Finite(d.a)&&NativeMapRules.Finite(d.b)&&d.a.y>d.b.y,"drop");
            foreach(var n in map.navHints?.safe??Array.Empty<MapSafe>())Need(n!=null&&NativeMapRules.Finite(n.at)&&(map.hazards??Array.Empty<MapHazard>()).Any(h=>h.id==n.hazard),"safe");
        }
    }
}
