using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ExpeditionPoint
    {public float x,y,z,yaw;public V3 Position=>new V3(x,y,z);}
    [Serializable] public sealed class ExpeditionVolume
    {
        public float x,y,z,sx,sy,sz,yaw;
        public bool Contains(V3 p)
        {double a=yaw*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a),dx=p.x-x,dz=p.z-z;return Math.Abs(c*dx-s*dz)<=sx/2&&Math.Abs(p.y-y)<=sy/2&&Math.Abs(s*dx+c*dz)<=sz/2;}
    }
    [Serializable] public sealed class ExpeditionDoorLayout
    {public string anchor;public ExpeditionVolume interact,closure;public ExpeditionPoint approach;public ExpeditionPoint[] entries,returns;}
    [Serializable] public sealed class ExpeditionInteractionLayout
    {public string anchor;public float x,y,z,range;}
    [Serializable] public sealed class ExpeditionGateLayout
    {public string id,opens;public ExpeditionVolume closure;}
    [Serializable] public sealed class ExpeditionSpawnLayout
    {public PveEntry entry;public string[] allow;}
    [Serializable] public sealed class ExpeditionAreaLayout
    {
        public string id,role,map,spawnSource;public ExpeditionWorldAnchor[] anchors;public ExpeditionDoorLayout[] doors;
        public ExpeditionInteractionLayout[] interactions;public ExpeditionGateLayout[] gates;public ExpeditionSpawnLayout[] spawns;
        public ExpeditionVolume[] sanctuary;public ExpeditionPoint[] slots,bossPoints;public ExpeditionVolume bossTrigger;
    }
    [Serializable] public sealed class ExpeditionLayout
    {
        public int schema;public string revision,world;public string[] instances;public ExpeditionAreaLayout[] areas;
        public ExpeditionAreaLayout Area(string id)=>areas.FirstOrDefault(a=>a.id==id);
    }
    /// <summary>Strict schema-1 reader. Missing coordinates are errors, never implicit origin positions.
    /// The existing bounded JSON parser also rejects duplicate keys and excessive nesting.</summary>
    public static class NativeExpeditionLayout
    {
        private static Dictionary<string,object> Obj(object x)=>x as Dictionary<string,object>??throw new FormatException("Expedition layout: expected object.");
        private static object Required(Dictionary<string,object> o,string key)=>o.TryGetValue(key,out var v)&&v!=null?v:throw new FormatException("Expedition layout: missing "+key);
        private static string Text(Dictionary<string,object> o,string key)=>Required(o,key) as string??throw new FormatException("Expedition layout: string required for "+key);
        private static float Number(Dictionary<string,object> o,string key)
        {var v=Required(o,key);if(!(v is double d)||!ExpeditionValidation.Number(d)||Math.Abs(d)>100000)throw new FormatException("Expedition layout: invalid "+key);return (float)d;}
        private static object[] List(object x)=>x is List<object> a?a.ToArray():throw new FormatException("Expedition layout: expected array.");
        private static T[] Items<T>(Dictionary<string,object> o,string key,Func<object,T> read,bool required=false)=>o.TryGetValue(key,out var v)?List(v).Select(read).ToArray():required?throw new FormatException("Expedition layout: missing "+key):Array.Empty<T>();
        private static string Id(object x)=>x is string s&&ExpeditionValidation.Id(s)?s:throw new FormatException("Expedition layout: invalid identifier.");
        private static ExpeditionPoint Point(object x,bool yaw=true)
        {var o=Obj(x);return new ExpeditionPoint{x=Number(o,"x"),y=Number(o,"y"),z=Number(o,"z"),yaw=yaw?Number(o,"yaw"):0};}
        private static ExpeditionVolume Volume(object x)
        {var o=Obj(x);var v=new ExpeditionVolume{x=Number(o,"x"),y=Number(o,"y"),z=Number(o,"z"),sx=Number(o,"sx"),sy=Number(o,"sy"),sz=Number(o,"sz"),yaw=Number(o,"yaw")};if(v.sx<=0||v.sy<=0||v.sz<=0)throw new FormatException("Expedition layout: empty volume.");return v;}
        public static ExpeditionLayout Parse(string json)
        {
            var o=Obj(NativeSoundJson.Parse(json));if(Number(o,"schema")!=1)throw new FormatException("Unsupported Expedition layout schema.");
            var l=new ExpeditionLayout{schema=1,revision=Id(Text(o,"revision")),world=Id(Text(o,"world")),instances=Items(o,"instances",Id,true),areas=Items(o,"areas",Area,true)};
            if(l.areas.Length<2||l.areas.Length>16||l.instances.Length<1||l.instances.Length>15||l.instances.Distinct().Count()!=l.instances.Length||l.areas.Select(a=>a.id).Distinct().Count()!=l.areas.Length||l.Area(l.world)?.role!="world"||l.areas.Count(a=>a.role=="world")!=1||l.instances.Any(i=>l.Area(i)?.role!="instance")||l.areas.Any(a=>a.id!=l.world&&!l.instances.Contains(a.id)))throw new FormatException("Expedition layout: inconsistent area roster.");
            var all=l.areas.SelectMany(a=>a.anchors).ToArray();if(all.Select(a=>a.id).Distinct().Count()!=all.Length)throw new FormatException("Expedition layout: duplicate anchor.");
            foreach(var a in l.areas)
            {
                if(!ExpeditionWorldRules.Valid(a.anchors,Array.Empty<ExpeditionChestOffer>())||a.anchors.Any(v=>v.kind=="reward"))throw new FormatException("Expedition layout: invalid authored anchors.");
                if(a.anchors.Count(v=>v.kind=="door")!=a.doors.Length||a.doors.Select(d=>d.anchor).Distinct().Count()!=a.doors.Length||a.doors.Any(d=>!a.anchors.Any(v=>v.id==d.anchor&&v.kind=="door")))throw new FormatException("Expedition layout: door metadata mismatch.");
                foreach(var d in a.doors)
                {
                    var anchor=a.anchors.First(v=>v.id==d.anchor);if(!l.instances.Contains(anchor.building)||a.id!=l.world&&anchor.building!=a.id||(a.id==l.world?d.returns:d.entries).Length!=8)throw new FormatException("Expedition layout: door building or eight slots missing.");
                }
                if(a.interactions.Select(i=>i.anchor).Distinct().Count()!=a.interactions.Length||a.interactions.Any(i=>i.range<=0||i.range>10||!a.anchors.Any(v=>v.id==i.anchor&&(v.kind=="machine"||v.kind=="chest")))||a.anchors.Any(v=>(v.kind=="machine"||v.kind=="chest")&&!a.interactions.Any(i=>i.anchor==v.id)))throw new FormatException("Expedition layout: missing or invalid interaction evidence point.");
                if(a.bossPoints.Length==0||a.bossPoints.Length>8)throw new FormatException("Expedition layout: missing boss points.");
                if(a.id==l.world&&(a.slots.Length!=8||a.sanctuary.Length==0||a.bossTrigger==null||a.gates.Count(g=>g.opens=="final")!=1))throw new FormatException("Expedition layout: incomplete town sanctuary or final gate.");
                if(a.spawnSource!=null&&(a.spawnSource!="inherit:siren"||a.id!="exp-ward-zero")||a.spawnSource==null&&a.spawns.Length==0)throw new FormatException("Expedition layout: invalid spawn source.");
                if(a.spawns.Select(s=>s.entry.e).Distinct().Count()!=a.spawns.Length||a.spawns.Select(s=>s.entry.id).Distinct().Count()!=a.spawns.Length)throw new FormatException("Expedition layout: duplicate spawn.");
            }
            foreach(string id in l.instances)if(l.Area(l.world).doors.Count(d=>all.First(a=>a.id==d.anchor).building==id)!=1||l.Area(id).doors.Length!=1)throw new FormatException("Expedition layout: missing paired doorway.");
            return l;
        }
        // The runtime probes the loaded physics scene; managed build checks supply authored geometry probes.
        // A parser pass alone never certifies clearance or navigation.
        public static void ValidateGeometry(ExpeditionLayout layout,ConversionContent content,NativePveContent horde,Func<string,V3,float,float,bool> supported)
        {
            if(supported==null)throw new ArgumentNullException(nameof(supported));
            foreach(var area in layout.areas)
            {
                var map=content.maps.FirstOrDefault(m=>m.id==area.map);var bounds=map?.bounds;
                if(bounds==null)throw new FormatException("Expedition layout: missing map "+area.map);
                void Check(V3 p,float radius,float height)
                {if(p.x-radius<bounds.minX||p.x+radius>bounds.maxX||p.z-radius<bounds.minZ||p.z+radius>bounds.maxZ||p.y<=bounds.killY||p.y+height>bounds.ceiling||!supported(area.map,p,radius,height))throw new FormatException("Expedition layout: unsupported or obstructed point in "+area.id);}
                var slots=area.slots.Concat(area.doors.SelectMany(d=>d.entries.Concat(d.returns))).ToArray();
                foreach(var p in slots)Check(p.Position,.45f,1.8f);
                foreach(var group in area.doors.Select(d=>d.entries).Concat(area.doors.Select(d=>d.returns)).Concat(new[]{area.slots}))
                    for(int i=0;i<group.Length;i++)for(int j=0;j<i;j++)if((group[i].Position-group[j].Position).Length<1)throw new FormatException("Expedition layout: overlapping party slots.");
                foreach(var p in area.bossPoints)Check(p.Position,NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight);
                foreach(var p in area.interactions)Check(new V3(p.x,p.y,p.z),.45f,1.8f);
                foreach(var d in area.doors)Check(d.approach.Position,.45f,1.8f);
                var spawns=area.spawnSource=="inherit:siren"?horde?.entries?.Select(e=>new ExpeditionSpawnLayout{entry=e,allow=NativeWardEntries.Allows(e,"hulk")?new[]{"hulk"}:new[]{"shambler"}}).ToArray():area.spawns;
                if(spawns==null||spawns.Length==0)throw new FormatException("Expedition layout: missing inherited entrances.");
                foreach(var spawn in spawns){bool heavy=spawn.allow.Contains("hulk");Check(spawn.entry.@exit.Position(),heavy?NativeHordeKinds.HulkRadius:.45f,heavy?NativeHordeKinds.HulkHeight:1.8f);}
            }
        }
        private static ExpeditionAreaLayout Area(object x)
        {
            var o=Obj(x);string id=Id(Text(o,"id"));
            var a=new ExpeditionAreaLayout{id=id,role=Text(o,"role"),map=Id(Text(o,"map")),anchors=Items(o,"anchors",v=>{var q=Obj(v);var p=Point(v);return new ExpeditionWorldAnchor{id=Id(Text(q,"id")),area=id,kind=Text(q,"kind"),building=Text(q,"building"),x=p.x,y=p.y,z=p.z,yaw=p.yaw};},true),
                doors=Items(o,"doors",v=>{var q=Obj(v);return new ExpeditionDoorLayout{anchor=Id(Text(q,"anchor")),interact=Volume(Required(q,"interact")),closure=Volume(Required(q,"closure")),approach=Point(Required(q,"approach"),false),entries=Items(q,"entries",p=>Point(p)),returns=Items(q,"returns",p=>Point(p))};},true),
                interactions=Items(o,"interactions",v=>{var q=Obj(v);var p=Point(v,false);return new ExpeditionInteractionLayout{anchor=Id(Text(q,"anchor")),x=p.x,y=p.y,z=p.z,range=Number(q,"range")};}),
                gates=Items(o,"gates",v=>{var q=Obj(v);return new ExpeditionGateLayout{id=Id(Text(q,"id")),opens=Text(q,"opens"),closure=Volume(Required(q,"closure"))};}),sanctuary=Array.Empty<ExpeditionVolume>(),slots=Array.Empty<ExpeditionPoint>()};
            var boss=Obj(Required(o,"boss"));a.bossPoints=Items(boss,"points",v=>Point(v),true);if(boss.TryGetValue("trigger",out var trigger))a.bossTrigger=Volume(trigger);
            if(o.TryGetValue("sanctuary",out var safe)){var q=Obj(safe);a.sanctuary=Items(q,"volumes",Volume,true);a.slots=Items(q,"slots",v=>Point(v),true);}
            var spawns=Required(o,"spawns");a.spawnSource=spawns as string;a.spawns=a.spawnSource!=null?Array.Empty<ExpeditionSpawnLayout>():List(spawns).Select(v=>
            {var q=Obj(v);var from=Point(Required(q,"from"),false);var to=Point(Required(q,"exit"),false);float e=Number(q,"e");var kind=Text(q,"kind");var allow=Items(q,"allow",Id,true);if(e<0||e>255||e!=(int)e||!new[]{"door","vent","drop","breach"}.Contains(kind)||allow.Length==0||allow.Any(k=>!NativeHordeKinds.Known(k)))throw new FormatException("Expedition layout: invalid spawn.");return new ExpeditionSpawnLayout{entry=new PveEntry{e=(int)e,id=Id(Text(q,"id")),kind=kind,from=new PvePoint{x=from.x,y=from.y,z=from.z},@exit=new PvePoint{x=to.x,y=to.y,z=to.z},yaw=Number(q,"yaw"),level=(int)Number(q,"level"),clear=Number(q,"clear"),height=q.ContainsKey("height")?Number(q,"height"):0,surface=q.ContainsKey("surface")?Text(q,"surface"):null},allow=allow};}).ToArray();
            return a;
        }
    }
}
