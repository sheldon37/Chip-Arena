using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class MapBounds
    {
        public float minX=-32,maxX=32,minZ=-28,maxZ=28,ceiling=24,killY=-6,cleanupY=-15;
        public bool Contains(V3 p,float inset=0)=>p.x>=minX+inset&&p.x<=maxX-inset&&p.z>=minZ+inset&&p.z<=maxZ-inset;
        public V3 Clamp(V3 p,float inset=1)=>new V3(MathUtil.Clamp(p.x,minX+inset,maxX-inset),p.y,MathUtil.Clamp(p.z,minZ+inset,maxZ-inset));
    }
    [Serializable] public sealed class MapTeamSpawns {public int team;public V3[] points;}
    [Serializable] public sealed class MapGroup {public string id,building;}
    [Serializable] public sealed class MapHazard
    {public string id,kind;public V3 position,size;public float off=4,warning=1,active=2,offset,damage=8,interval=.5f;}
    [Serializable] public sealed class MapGameplay
    {
        public bool enabled;public MapBounds bounds;public V3[] ffa;public MapTeamSpawns[] teams;
        public MapGroup[] groups;public MapHazard[] hazards;public float spawnProtection=2.5f,telegraph=.75f;
        public int maxFalling=16,maxCollapses=2;
    }
    // Opaque to gameplay. The presentation owner can version/extend this independently.
    [Serializable] public sealed class MapVisual {public int version;public string json;}
    [Serializable] public sealed class MapCatalog {public int schema=1;public ContentMap[] maps;}
    public static class NativeMapRules
    {
        public static bool Enabled(ContentMap m)=>m.gameplay!=null&&m.gameplay.enabled;
        public static MapBounds Bounds(ContentMap m)=>Enabled(m)?m.gameplay.bounds:new MapBounds();
        public static string Fingerprint(ConversionContent c,ContentMap m)
        {
            using(var bytes=new MemoryStream())using(var w=new BinaryWriter(bytes))
            {
                void Point(V3 p){w.Write(p.x);w.Write(p.y);w.Write(p.z);}
                var g=m.gameplay;var b=g.bounds;w.Write(m.id);w.Write(b.minX);w.Write(b.maxX);w.Write(b.minZ);w.Write(b.maxZ);w.Write(b.ceiling);w.Write(b.killY);w.Write(b.cleanupY);w.Write(g.spawnProtection);w.Write(g.telegraph);w.Write(g.maxFalling);w.Write(g.maxCollapses);
                w.Write(g.ffa.Length);foreach(var p in g.ffa)Point(p);w.Write(g.teams?.Length??0);foreach(var t in g.teams??Array.Empty<MapTeamSpawns>()){w.Write(t.team);w.Write(t.points.Length);foreach(var p in t.points)Point(p);}
                foreach(var group in g.groups??Array.Empty<MapGroup>()){w.Write(group.id);w.Write(group.building);}
                foreach(var d in m.blocks.OrderBy(d=>d.id,StringComparer.Ordinal)){w.Write(d.id);w.Write(d.material);w.Write(c.Material(d.material).durability);w.Write(c.Material(d.material).indestructible);w.Write(d.group??"");w.Write(d.materialClass??"");w.Write(d.anchor);w.Write(d.mass);Point(d.pos);Point(d.size);foreach(string edge in d.support??Array.Empty<string>())w.Write(edge);w.Write("");}
                foreach(var h in g.hazards??Array.Empty<MapHazard>()){w.Write(h.id);w.Write(h.kind??"");Point(h.position);Point(h.size);w.Write(h.off);w.Write(h.warning);w.Write(h.active);w.Write(h.offset);w.Write(h.damage);w.Write(h.interval);}
                w.Flush();using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        public static void Append(ConversionContent content,MapCatalog catalog)
        {
            if(catalog==null||catalog.schema!=1||catalog.maps==null)throw new InvalidOperationException("Invalid arena catalog");
            if(catalog.maps.Any(m=>m==null||content.maps.Any(old=>old.id==m.id)))throw new InvalidOperationException("Arena catalog replaces existing map");
            content.maps=content.maps.Concat(catalog.maps).ToArray();content.Validate();
        }
        public static IEnumerable<ContentMap> Rotation(ConversionContent c)=>c.maps.Where(m=>m.id!="siren"&&!m.experimental);
        public static IEnumerable<ContentMap> Selectable(ConversionContent c,bool experimental)=>c.maps.Where(m=>!m.experimental||experimental);
        public static IEnumerable<V3> Spawns(ConversionContent c,ContentMap m)
        {return !Enabled(m)?c.spawns:m.gameplay.ffa.Concat((m.gameplay.teams??Array.Empty<MapTeamSpawns>()).SelectMany(t=>t.points));}
        public static V3 Spawn(ConversionContent c,ContentMap m,int index,int team,int teamIndex,bool teams)
        {
            V3[] points=c.spawns;
            if(Enabled(m)){points=m.gameplay.ffa;var set=teams?m.gameplay.teams?.FirstOrDefault(s=>s.team==team):null;if(set!=null){points=set.points;index=teamIndex;}}
            return points[Math.Max(0,Math.Min(index,points.Length-1))];
        }
        public static bool Protected(ConversionContent c,ContentMap m,V3 p,float x=0,float z=0)
        {
            if(!Enabled(m))return false;float r=m.gameplay.spawnProtection;
            return Spawns(c,m).Any(s=>{float dx=Math.Max(0,Math.Abs(s.x-p.x)-x),dz=Math.Max(0,Math.Abs(s.z-p.z)-z);return dx*dx+dz*dz<r*r;});
        }
        public static bool Finite(V3 p)=>MathUtil.Finite(p.x)&&MathUtil.Finite(p.y)&&MathUtil.Finite(p.z);
        private static bool Id(string s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=64;
        public static void Validate(ConversionContent c,ContentMap m)
        {
            if(m==null||!Id(m.id)||m.blocks==null)throw new InvalidOperationException("Invalid arena");
            if(!Enabled(m))return;
            void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(m.id+": "+why);}
            var g=m.gameplay;var b=g.bounds;
            Require(b!=null,"missing bounds");
            Require(new[]{b.minX,b.maxX,b.minZ,b.maxZ,b.ceiling,b.killY,b.cleanupY,g.spawnProtection,g.telegraph}.All(MathUtil.Finite),"nonfinite gameplay");
            Require(b.maxX-b.minX>=8&&b.maxZ-b.minZ>=8&&b.maxX-b.minX<=128&&b.maxZ-b.minZ<=128&&Math.Abs(b.minX)<500&&Math.Abs(b.minZ)<500&&b.ceiling>3&&b.ceiling<=100&&b.killY<0&&b.cleanupY<b.killY,"invalid bounds");
            Require(g.ffa!=null&&g.ffa.Length==8&&g.ffa.All(p=>Finite(p)&&b.Contains(p,1)&&p.y>b.killY+1&&p.y<b.ceiling-1),"eight valid FFA spawns required");
            Require(g.spawnProtection>=1.5f&&g.spawnProtection<=6&&g.telegraph>=.5f&&g.telegraph<=1&&g.maxFalling>=1&&g.maxFalling<=32&&g.maxCollapses>=1&&g.maxCollapses<=4,"unsafe collapse limits");
            if(g.teams!=null&&g.teams.Length>0)Require(g.teams.Length==8&&g.teams.All(t=>t!=null&&t.team>=0&&t.team<8&&t.points!=null&&t.points.Length==8&&t.points.All(p=>Finite(p)&&b.Contains(p,1)))&&g.teams.Select(t=>t.team).Distinct().Count()==8,"provide eight spawn sets for eight teams");
            var groups=g.groups??Array.Empty<MapGroup>();
            Require(groups.Length<=64&&groups.All(x=>x!=null&&Id(x.id)&&Id(x.building))&&groups.Select(x=>x.id).Distinct().Count()==groups.Length,"invalid groups");
            Require(m.blocks.Length<=400&&m.blocks.All(x=>x!=null&&Id(x.id))&&m.blocks.Select(x=>x.id).Distinct().Count()==m.blocks.Length,"invalid blocks");
            var byId=m.blocks.ToDictionary(x=>x.id,StringComparer.Ordinal);var groupIds=groups.ToDictionary(x=>x.id,StringComparer.Ordinal);
            foreach(var block in m.blocks)
            {
                bool structural=!string.IsNullOrEmpty(block.group);
                Require(block.rotation!=null&&Math.Abs(block.rotation.x)<.0001&&Math.Abs(block.rotation.y)<.0001&&Math.Abs(block.rotation.z)<.0001&&Math.Abs(block.rotation.w-1)<.0001,"gameplay boxes must be axis aligned: "+block.id);
                Require(Finite(block.pos)&&Finite(block.size)&&block.size.x>0&&block.size.y>0&&block.size.z>0&&b.Contains(block.pos)&&block.pos.x-block.size.x/2>=b.minX&&block.pos.x+block.size.x/2<=b.maxX&&block.pos.z-block.size.z/2>=b.minZ&&block.pos.z+block.size.z/2<=b.maxZ,"invalid geometry: "+block.id);
                if(!structural){Require(c.Material(block.material).indestructible&&!block.dynamic,"unassigned collision must be static and indestructible");continue;}
                Require(groupIds.ContainsKey(block.group)&&new[]{"concrete","steel","wood","glass"}.Contains(block.materialClass)&&!block.dynamic,"invalid structural identity: "+block.id);
                Require(MathUtil.Finite(block.mass)&&block.mass>=0&&MathUtil.Finite(c.Material(block.material).durability)&&c.Material(block.material).durability>0,"invalid structural mass/durability");
                if(block.anchor)Require(m.blocks.Any(f=>string.IsNullOrEmpty(f.group)&&c.Material(f.material).indestructible&&Math.Abs(f.pos.x-block.pos.x)<(f.size.x+block.size.x)/2&&Math.Abs(f.pos.z-block.pos.z)<(f.size.z+block.size.z)/2&&Math.Abs(f.pos.y+f.size.y/2-(block.pos.y-block.size.y/2))<.05f),"anchor has no permanent footing: "+block.id);
                Require(!Protected(c,m,block.pos,block.size.x/2,block.size.z/2),"structure crosses protected spawn: "+block.id);
                Require(block.support==null||block.support.All(id=>byId.TryGetValue(id,out var support)&&!string.IsNullOrEmpty(support.group)&&groupIds.ContainsKey(support.group)&&groupIds[support.group].building==groupIds[block.group].building&&id!=block.id),"invalid structural edge: "+block.id);
            }
            foreach(var group in groups)Require(m.blocks.Count(x=>x.group==group.id)>0&&m.blocks.Count(x=>x.group==group.id)<=g.maxFalling,"group exceeds falling budget: "+group.id);
            var supported=new HashSet<string>(m.blocks.Where(x=>!string.IsNullOrEmpty(x.group)&&x.anchor).Select(x=>x.id));bool changed;
            do{changed=false;foreach(var block in m.blocks.Where(x=>!string.IsNullOrEmpty(x.group)))if(!supported.Contains(block.id)&&(block.support?.Any(supported.Contains)??false)){supported.Add(block.id);changed=true;}}while(changed);
            Require(m.blocks.Where(x=>!string.IsNullOrEmpty(x.group)).All(x=>supported.Contains(x.id)),"initial support graph has unanchored blocks/cycle");
            foreach(var p in Spawns(c,m))
            {
                Require(Finite(p)&&b.Contains(p,1)&&p.y>.85f+b.killY&&p.y<b.ceiling-1,"invalid spawn");
                Require(m.blocks.Any(x=>c.Material(x.material).indestructible&&Math.Abs(p.x-x.pos.x)<=x.size.x/2-.42f&&Math.Abs(p.z-x.pos.z)<=x.size.z/2-.42f&&Math.Abs(x.pos.y+x.size.y/2-(p.y-.9f))<.15f),"spawn has no permanent floor");
                Require(!m.blocks.Any(x=>Math.Abs(p.x-x.pos.x)<x.size.x/2+.42f&&Math.Abs(p.z-x.pos.z)<x.size.z/2+.42f&&x.pos.y+x.size.y/2>p.y-.85f+.05f&&x.pos.y-x.size.y/2<p.y+.85f),"spawn intersects collision");
            }
            var hazards=g.hazards??Array.Empty<MapHazard>();Require(hazards.Length<=32&&hazards.All(h=>h!=null&&Id(h.id))&&hazards.Select(h=>h.id).Distinct().Count()==hazards.Length,"invalid hazards");
            foreach(var h in hazards)Require(Finite(h.position)&&Finite(h.size)&&h.size.x>0&&h.size.y>0&&h.size.z>0&&b.Contains(h.position)&&new[]{h.off,h.warning,h.active,h.offset,h.damage,h.interval}.All(MathUtil.Finite)&&h.off>=0&&h.off<=120&&h.warning>=.5f&&h.warning<=30&&h.active>0&&h.active<=120&&Math.Abs(h.offset)<=10000&&h.interval>=.1f&&h.interval<=10&&h.damage>=0&&h.damage<=100&&!Protected(c,m,h.position,h.size.x/2,h.size.z/2),"unsafe hazard: "+h.id);
        }
    }
}
