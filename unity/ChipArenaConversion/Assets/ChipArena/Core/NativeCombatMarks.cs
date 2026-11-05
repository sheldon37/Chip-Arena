using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public struct NativeTechniqueMark
    {
        public string Kind;public int By;public float At,Until;
        public bool Valid()=>!string.IsNullOrEmpty(Kind)&&Kind.Length<=64&&By>0&&MathUtil.Finite(At)&&MathUtil.Finite(Until)&&Math.Abs(At)<10000000&&Until>=At&&Until<10000000;
    }
    [Serializable] public struct NativeChipGauge
    {
        public string Kind;public float Value,Until;
        public bool Valid()=>Kind!=null&&Kind.StartsWith("wild-",StringComparison.Ordinal)&&Kind.Length<=64&&MathUtil.Finite(Value)&&Value>=0&&Value<=1&&MathUtil.Finite(Until)&&Math.Abs(Until)<10000000;
    }
    public static class NativeCombatMarks
    {
        public const int MaxMarks=6,MaxGauges=8;
        public static bool Valid(NativeTechniqueMark[] marks,NativeChipGauge[] gauges)=>(marks==null||marks.Length<=MaxMarks&&marks.All(x=>x.Valid())&&marks.Select(x=>(x.Kind,x.By)).Distinct().Count()==marks.Length)&&(gauges==null||gauges.Length<=MaxGauges&&gauges.All(x=>x.Valid())&&gauges.Select(x=>x.Kind).Distinct().Count()==gauges.Length);
        public static bool Has(NativeTechniqueMark[] marks,string kind,float now)
        {if(marks!=null)foreach(var x in marks)if(x.Kind==kind&&now>=x.At&&now<x.Until)return true;return false;}
        public static NativeTechniqueMark[] Put(NativeTechniqueMark[] marks,string kind,int by,float now,float until)
        {
            var keep=(marks??Array.Empty<NativeTechniqueMark>()).Where(x=>x.Until>now&&(x.Kind!=kind||x.By!=by)).OrderBy(x=>x.At).ToList();
            while(keep.Count>=MaxMarks)keep.RemoveAt(0);keep.Add(new NativeTechniqueMark{Kind=kind,By=by,At=now,Until=until});return keep.ToArray();
        }
    }
    public static class NativeAbilityChipRules
    {
        public const double OfferChance=.35;
        public static bool Slotted(ConversionContent content,string spec,string map,string chip)
        {
            string needed=NativeAbilityChips.Requires(chip);if(needed==null)return true;
            foreach(string slot in new[]{"q","e"}){var row=NativeTechniqueLoadout.Ability(content,map??"",spec,slot);if(row?.live==true&&row.id==needed)return true;}return false;
        }
    }
}
