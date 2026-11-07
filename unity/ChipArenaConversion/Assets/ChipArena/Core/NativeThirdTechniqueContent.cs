using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public static class NativeThirdTechniqueContent
    {
        public static readonly string[] Ready={"gun-spin","overload","thunderclap","bipod","envenom","dragon-breath","combust","soul-siphon","disarm","ground-slam","reaping-sweep","bolas-round","vault-shot","hydro-jet","iaido","challenge","war-cry","sunder","ghillie","flash-freeze","lodestone","blade-drag","breacher-round","bramble-arrow","tether-arrow","bubble-trap","mirror-double","chain-hook","flash-round","slide-shot","ammo-crate","flak-burst","flame-cloak","magnetize","dread","shadow-puppet"};
        public static void Apply(ConversionContent content)
        {
            foreach(string id in Ready)
            {
                if(content.abilities.Any(a=>a.id==id))continue;var d=NativeTechniques.Find(id);
                float wind=id=="dread"?.35f:id=="iaido"?.01f:id=="war-cry"?.3f:id=="sunder"?.55f:id=="ghillie"?.6f:id=="breacher-round"?1f:id=="disarm"?.01f:id=="ground-slam"?.1f:id=="reaping-sweep"?.25f:id=="bolas-round"?.1f:id=="vault-shot"?.01f:id=="hydro-jet"?.1f:id=="gun-spin"?.45f:id=="thunderclap"?.3f:id=="bipod"?.5f:id=="envenom"?.4f:id=="dragon-breath"?.25f:id=="combust"?.2f:.1f;
                float duration=id=="mirror-double"?2.5f:id=="chain-hook"?.4f:id=="flash-round"?1:id=="slide-shot"?.75f:id=="ammo-crate"?8:id=="flak-burst"?4:id=="flame-cloak"?4:id=="magnetize"?1.5f:id=="dread"?.9f:id=="shadow-puppet"?4:id=="iaido"?1.5f:id=="challenge"?6:id=="war-cry"?4:id=="sunder"?4:id=="ghillie"?8:id=="flash-freeze"?4:id=="lodestone"?2.5f:id=="blade-drag"?2:id=="breacher-round"?.3f:id=="bramble-arrow"?2:id=="tether-arrow"?2:id=="bubble-trap"?2:id=="disarm"?.72f:id=="ground-slam"?1.1f:id=="reaping-sweep"?.5f:id=="bolas-round"?2.2f:id=="vault-shot"?1.2f:id=="hydro-jet"?1.5f:id=="thunderclap"||id=="combust"?.3f:id=="bipod"?5:id=="envenom"?6:id=="dragon-breath"?1.6f:id=="soul-siphon"?2:4;
                content.abilities=content.abilities.Concat(new[]{new ContentAbility{spec=d.Spec,slot="technique",id=id,name=d.Name,@short=d.Name,text=NativeTechniques.Text(id),kind="technique",fx=id,cooldown=d.Cooldown,cost=d.Cost,windup=wind,duration=duration,lockout=id=="thunderclap"?.2f:0,range=id=="thunderclap"?5:0,damage=id=="thunderclap"?10:0,live=true,bot=new ContentAbilityBot{max=18}}}).ToArray();
            }
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nthird-techniques-complete");
        }
    }
}
