using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public static class NativeThirdTechniqueContent
    {
        public static readonly string[] Ready={"gun-spin","overload","thunderclap","bipod","envenom","dragon-breath","combust","soul-siphon"};
        public static void Apply(ConversionContent content)
        {
            foreach(string id in Ready)
            {
                if(content.abilities.Any(a=>a.id==id))continue;var d=NativeTechniques.Find(id);
                float wind=id=="gun-spin"?.45f:id=="thunderclap"?.3f:id=="bipod"?.5f:id=="envenom"?.4f:id=="dragon-breath"?.25f:id=="combust"?.2f:.1f;
                float duration=id=="thunderclap"||id=="combust"?.3f:id=="bipod"?5:id=="envenom"?6:id=="dragon-breath"?1.6f:id=="soul-siphon"?2:4;
                content.abilities=content.abilities.Concat(new[]{new ContentAbility{spec=d.Spec,slot="technique",id=id,name=d.Name,@short=d.Name,text=NativeTechniques.Text(id),kind="technique",fx=id,cooldown=d.Cooldown,cost=d.Cost,windup=wind,duration=duration,lockout=id=="thunderclap"?.2f:0,range=id=="thunderclap"?5:0,damage=id=="thunderclap"?10:0,live=true,bot=new ContentAbilityBot{max=18}}}).ToArray();
            }
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nthird-techniques-batch-2");
        }
    }
}
