using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class BossBattleChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool yes,string why){n++;if(!yes)throw new Exception("Boss Battle: "+why);}
        var codec=new JsonSerializerOptions{IncludeFields=true};string res=Path.Combine(root,"Assets/Conversion/Resources");
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(res,"GameContent.json")),codec);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(res,"UnityMaps.json")),codec);content.maps=maps.maps;
        string json=File.ReadAllText(Path.Combine(res,"ExpeditionLayout.json"));
        var legacy=JsonSerializer.Deserialize<NativeRoomOptions>("{\"cooperative\":true,\"skipWaves\":true,\"bots\":3}",codec);legacy.Normalize(content);
        Check(legacy.boss=="siren"&&legacy.bots==3,"old room retains Siren and bots");
        Check(NativeBossBattle.Index(null)==0&&!NativeBossBattle.Zone("noctiel")&&!NativeBossBattle.Zone("bogus"),"legacy fallback and unknown/final exclusions");
        foreach(string id in new[]{"station","converter","hum","deepend"})
        {
            var o=new NativeRoomOptions{cooperative=true,boss=id,bots=3};o.Normalize(content);
            var copy=JsonSerializer.Deserialize<NativeRoomOptions>(JsonSerializer.Serialize(o,codec),codec);
            Check(copy.boss==id&&copy.bots==0,"room pick and human-only "+id);
            Check(NativeExpeditionCatalogue.BossAt(NativeBossBattle.Index(copy.boss)).Id==id,"guest pick "+id);
            var host=ExpeditionRunBundle.BossBattle(id,content,json);var guest=ExpeditionRunBundle.BossBattle(id,content,json);
            var area=host.Layout.areas.Single(a=>a.map==NativeBossBattle.Map(id));
            Check(area.id==NativeExpeditionCatalogue.BossAt(NativeBossBattle.Index(id)).Destination,"destination "+id);
            Check(host.World==null&&host.Ticket==null&&host.Mutators.Length==0,"no station/world roll "+id);
            Check(host.Content.Map(area.map).blocks.Length==guest.Content.Map(area.map).blocks.Length,"guest geometry "+id);
            Check(!host.Matches(new ExpeditionDeploymentTicket()),"cannot masquerade as run ticket");
            if(id=="hum")Check(host.Backrooms.Identity==guest.Backrooms.Identity,"Hum deterministic arena");
            else if(id=="deepend")Check(host.Poolrooms.Identity==guest.Poolrooms.Identity,"Deep End deterministic arena");
            else Check(area.doors[0].entries.Length>=4,"four authored entry slots "+id);
            for(int party=1;party<=4;party++)
            {var scale=new ExpeditionScaling().Evaluate(0,party,0,true);float expected=NativePveRules.BossHp(party)*(float)scale.health*(id=="hum"?1.1f:id=="deepend"?1.25f:1);Check(NativeBossBattle.Health(id,party)==expected,"run-start HP "+id+party);Check(NativeBossBattle.Damage(party)==NativePveRules.BossDamage(party)*(float)scale.damage,"run-start damage");}
        }
        Check(!content.maps.Any(m=>m.id==NativeBackroomsGenerator.InstanceMap||m.id==NativePoolroomsGenerator.InstanceMap),"base content untouched");
        Console.WriteLine("PASS Boss Battle: "+n+" checks (four destinations, deterministic guests, scaling, legacy rooms, no run roll)");
    }
}
