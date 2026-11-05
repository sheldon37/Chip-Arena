using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public static class NativeThirdTechniqueContent
    {
        public static readonly string[] Ready={"gun-spin","overload","thunderclap"};
        public static void Apply(ConversionContent content)
        {
            foreach(string id in Ready)
            {
                if(content.abilities.Any(a=>a.id==id))continue;var d=NativeTechniques.Find(id);
                float wind=id=="gun-spin"?.45f:id=="thunderclap"?.3f:.1f;
                content.abilities=content.abilities.Concat(new[]{new ContentAbility{spec=d.Spec,slot="technique",id=id,name=d.Name,@short=d.Name,text=NativeTechniques.Text(id),kind="technique",fx=id,cooldown=d.Cooldown,cost=d.Cost,windup=wind,duration=id=="thunderclap"?.3f:4,lockout=id=="thunderclap"?.2f:0,range=id=="thunderclap"?5:0,damage=id=="thunderclap"?10:0,live=true,bot=new ContentAbilityBot{max=18}}}).ToArray();
            }
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nthird-techniques-batch-1");
        }
    }
}
