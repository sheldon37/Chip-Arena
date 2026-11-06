using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Fixed test arenas: no run roll, elapsed-time growth, mutators or progression.
    public static class NativeBossBattle
    {
        public const int Seed=6472026;
        public static string Map(string id)=>id=="station"?"expedition-transit":id=="converter"?"expedition-foundry":id=="hum"?NativeBackroomsGenerator.InstanceMap:id=="deepend"?NativePoolroomsGenerator.InstanceMap:null;
        public static string Id(string map)=>map=="expedition-transit"?"station":map=="expedition-foundry"?"converter":map==NativeBackroomsGenerator.InstanceMap?"hum":map==NativePoolroomsGenerator.InstanceMap?"deepend":null;
        public static bool Zone(string id)=>Map(id)!=null;
        public static string Pick(string id)=>string.IsNullOrEmpty(id)?"siren":id;
        public static int Index(string id){int i=Array.FindIndex(NativeExpeditionCatalogue.Bosses,b=>b.Id==Pick(id));return i<0?NativeExpeditionCatalogue.DefaultBoss:i;}
        public static float Health(string id,int party)
        {if(!Zone(id))throw new ArgumentException("Unknown direct boss.");return NativePveRules.BossHp(party)*(float)new ExpeditionScaling().Evaluate(0,party,0,true).health*(id=="hum"?1.1f:id=="deepend"?1.25f:1);}
        public static float Damage(int party)=>NativePveRules.BossDamage(party)*(float)new ExpeditionScaling().Evaluate(0,party,0,true).damage;
    }
}
