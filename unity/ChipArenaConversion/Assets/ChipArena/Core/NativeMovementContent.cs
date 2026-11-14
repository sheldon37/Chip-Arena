using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public static class NativeMovementContent
    {
        public static ChipEffect Effect(string stat,string op,double value)=>new ChipEffect{stat=stat,op=op,value=value};
        private static ContentChip Chip(string id,string name,string description,string rarity,int max,params ChipEffect[] effects)=>
            new ContentChip{id=id,name=name,description=description,rarity=rarity,max=max,icon="wind",tags=new[]{"all"},excludes=Array.Empty<string>(),effects=effects};
        public static ContentChip[] Chips()=>new[]{
            Chip("stride","Stride Circuit","+8% movement speed. No health penalty.","common",5,Effect("speed","mul",1.08)),
            Chip("air-step","Air Step","+1 extra mid-air jump. Restored on landing. Up to 6 air jumps total.","rare",3,Effect("extraAirJumps","add",1)),
            Chip("acrobat","Acrobat","+1 extra mid-air jump and +12% dodge distance.","epic",1,Effect("extraAirJumps","add",1),Effect("rollDistance","mul",1.12)),
            Chip("skybound","Skybound","+2 extra mid-air jumps and 15% less gravity. Up to 6 air jumps total.","legendary",1,Effect("extraAirJumps","add",2),Effect("playerGravity","mul",.85)),
            Chip("momentum-cell","Momentum Cell","+10% movement speed and 15% faster stamina regeneration.","uncommon",3,Effect("speed","mul",1.10),Effect("staminaRegen","mul",1.15))};
        public static void Apply(ConversionContent content)
        {
            var chips=Chips();if(content.chips.Any(c=>chips.Any(n=>n.id==c.id)))throw new InvalidOperationException("Movement chip ID collision");
            content.baseStats=content.baseStats.Concat(new[]{new NumberValue{name="extraAirJumps",value=0}}).ToArray();
            content.chips=content.chips.Concat(chips).ToArray();
            // Versioned native overlay: the pinned legacy export stays byte-for-byte intact.
            content.contentRevision=NativeMapRules.Revision(content.contentRevision+"\nmovement-chips-v1-air-budget-6");
        }
    }
}
