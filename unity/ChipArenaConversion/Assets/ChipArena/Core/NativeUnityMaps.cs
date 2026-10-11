using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class UnityMapContent { public int schema; public ContentMaterial[] materials; public ContentMap[] maps; }
    [Serializable] public sealed class MapBounds { public float minX,maxX,minZ,maxZ,ceiling,killY; }
    [Serializable] public sealed class MapSpawn { public V3 pos; public float yaw; }
    [Serializable] public sealed class MapTeamSpawns { public int side; public MapSpawn[] points; }
    [Serializable] public sealed class MapSpawns { public MapSpawn[] ffa; public MapTeamSpawns[] teams; }
    [Serializable] public sealed class MapVolume { public V3 center,size; }
    [Serializable] public sealed class MapPilotBlock { public string id; public V3 center,size; public float bottom; }
    [Serializable] public sealed class MapNavNode { public string id; public V3 pos; public int layer; }
    [Serializable] public sealed class MapNavLink { public string id,a,b,kind; public string[] requires,after; }
    [Serializable] public sealed class MapNav { public MapNavNode[] nodes; public MapNavLink[] links; }
    public static class NativeMapRules
    {
        public const float PilotRadius=.42f;
        public static bool Finite(V3 p)=>MathUtil.Finite(p.x)&&MathUtil.Finite(p.y)&&MathUtil.Finite(p.z);
        public static bool Box(V3 p,V3 size)=>Finite(p)&&Finite(size)&&size.x>0&&size.y>0&&size.z>0;
        public static string Revision(string text) { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant(); }
        public static bool Compatible(int protocol,string source,string revision,ConversionContent c)=>protocol==NativeConnection.Protocol&&source==c.sourceCommit&&revision==c.contentRevision;
        public static void Merge(ConversionContent c,UnityMapContent u,string text)
        {
            if(u==null||u.schema!=1||u.maps==null||u.materials==null)throw new InvalidOperationException("Invalid UnityMaps schema");
            if(c.maps.Any(m=>m.unity))throw new InvalidOperationException("UnityMaps already loaded");
            // GameContent's legacy maps use global spawns/bounds and the legacy bot path.
            // JsonUtility can create empty inline objects for these absent supplemental fields.
            // Normalize at the shared load boundary so every runtime/build-check consumer agrees.
            // Authored UnityMaps below still require their own validated tables and bounds.
            foreach(var m in c.maps){m.bounds=null;m.spawns=null;m.nav=null;m.segments=null;m.pilotBlocks=null;}
            c.contentRevision=Revision(text);c.materials=c.materials.Concat(u.materials).ToArray();
            if(c.materials.Select(m=>m.id).Distinct().Count()!=c.materials.Length)throw new InvalidOperationException("Duplicate material");
            foreach(var m in u.maps){m.unity=true;Validate(m);}
            c.maps=c.maps.Concat(u.maps).ToArray();c.Validate();
        }
        public static V3 Clamp(ContentMap m,V3 p,bool bot=false)
        {
            var b=m.bounds;float x=bot?28:31,z=bot?24:27;
            return new V3(MathUtil.Clamp(p.x,b==null?-x:b.minX+PilotRadius,b==null?x:b.maxX-PilotRadius),p.y,MathUtil.Clamp(p.z,b==null?-z:b.minZ+PilotRadius,b==null?z:b.maxZ-PilotRadius));
        }
        public static MapSpawn Spawn(ConversionContent c,ContentMap m,IReadOnlyList<NativeSeat> seats,int index,bool teams)
        {
            if(m.spawns==null){var p=c.spawns[Math.Min(index,c.spawns.Length-1)];return new MapSpawn{pos=p,yaw=(float)(Math.Atan2(-p.x,-p.z)*180/Math.PI)};}
            var groups=seats.GroupBy(s=>s.team).OrderBy(g=>g.Key).ToArray();
            if(teams&&groups.Length==2&&groups.All(g=>g.Count()<=4))
            {int side=groups[0].Key==seats[index].team?0:1;int seat=seats.Take(index).Count(s=>s.team==seats[index].team);return m.spawns.teams.First(t=>t.side==side).points[seat];}
            return m.spawns.ffa[Math.Min(index,7)];
        }
        // Infinite upward columns cannot become a standable ceiling, including at extreme chip jump heights.
        public static V3 ExcludeTops(ContentMap m,V3 p)
        {
            var original=p;
            foreach(var v in m.pilotBlocks??Array.Empty<MapPilotBlock>())
            {
                if(p.y+.85f<v.bottom)continue;float loX=v.center.x-v.size.x/2-PilotRadius,hiX=v.center.x+v.size.x/2+PilotRadius,loZ=v.center.z-v.size.z/2-PilotRadius,hiZ=v.center.z+v.size.z/2+PilotRadius;
                if(p.x<=loX||p.x>=hiX||p.z<=loZ||p.z>=hiZ)continue;
                float[] gaps={p.x-loX,hiX-p.x,p.z-loZ,hiZ-p.z};int n=Array.IndexOf(gaps,gaps.Min());if(n==0)p.x=loX-.01f;else if(n==1)p.x=hiX+.01f;else if(n==2)p.z=loZ-.01f;else p.z=hiZ+.01f;
            }return m.bounds!=null&&(p-original).Length>.0001f?Clamp(m,p):p;
        }
        public static void Validate(ContentMap m)
        {
            void Need(bool ok,string what){if(!ok)throw new InvalidOperationException("Unity map "+m.id+": "+what);}
            var b=m.bounds;Need(b!=null&&MathUtil.Finite(b.minX)&&MathUtil.Finite(b.maxX)&&MathUtil.Finite(b.minZ)&&MathUtil.Finite(b.maxZ)&&b.maxX-b.minX>2&&b.maxZ-b.minZ>2,"bounds");
            Need(m.spawns?.ffa?.Length==8&&m.spawns.teams?.Length==2,"spawn tables");
            foreach(var side in new[]{0,1})Need(m.spawns.teams.Count(t=>t.side==side&&t.points?.Length==4)==1,"team table");
            foreach(var s in m.spawns.ffa.Concat(m.spawns.teams.SelectMany(t=>t.points)))Need(MathUtil.Finite(s.pos.x)&&MathUtil.Finite(s.pos.y)&&MathUtil.Finite(s.pos.z)&&MathUtil.Finite(s.yaw)&&(Clamp(m,s.pos)-s.pos).Length<.001,"spawn bounds");
            Need(m.blocks!=null&&m.blocks.Length>0,"blocks");
            foreach(var x in m.blocks)Need(!string.IsNullOrEmpty(x.id)&&Box(x.pos,x.size)&&new[]{"static","structural","masonry","carried","cover"}.Contains(x.role)&&MathUtil.Finite(x.hp)&&(!new[]{"structural","masonry","cover"}.Contains(x.role)||x.hp>0),"block geometry/role/hp "+x.id);
            foreach(var x in m.blocks)Need(x.stages==null||x.stages.Length==0||x.stages.Length==2&&MathUtil.Finite(x.stages[0])&&MathUtil.Finite(x.stages[1])&&x.stages[0]<1&&x.stages[0]>x.stages[1]&&x.stages[1]>0,"block stage thresholds "+x.id);
            foreach(var x in m.pilotBlocks??Array.Empty<MapPilotBlock>())Need(!string.IsNullOrEmpty(x.id)&&Finite(x.center)&&Finite(x.size)&&x.size.x>0&&x.size.z>0&&MathUtil.Finite(x.bottom),"pilot exclusion");
            NativeStructureRules.Validate(m);
            Need(m.nav?.nodes!=null&&m.nav.links!=null,"nav");var nodes=new HashSet<string>(m.nav.nodes.Select(n=>n.id));Need(nodes.Count==m.nav.nodes.Length,"nav node IDs");
            Need(m.nav.links.Select(l=>l.id).Distinct().Count()==m.nav.links.Length,"nav link IDs");
            var segments=new HashSet<string>((m.segments??Array.Empty<StructureDefinition>()).Select(s=>s.id));var links=new HashSet<string>(m.nav.links.Select(l=>l.id));
            foreach(var n in m.nav.nodes)Need(!string.IsNullOrEmpty(n.id)&&Finite(n.pos),"nav node");
            foreach(var l in m.nav.links)Need(!string.IsNullOrEmpty(l.id)&&nodes.Contains(l.a)&&nodes.Contains(l.b)&&l.a!=l.b&&(l.requires??Array.Empty<string>()).Concat(l.after??Array.Empty<string>()).All(segments.Contains),"nav endpoints/segment references");
            foreach(var s in m.segments??Array.Empty<StructureDefinition>())Need((s.nav?.remove??Array.Empty<string>()).Concat(s.nav?.add??Array.Empty<string>()).All(links.Contains),"segment nav references");
        }
    }
}
