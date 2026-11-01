using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NumberValue { public string name; public double value; }
    [Serializable] public sealed class ChipCount { public string id; public int count; }
    [Serializable] public sealed class ContentWeapon
    {
        public string id,name,@class,description,secondary; public bool devOnly;
        public double damage,interval,speed,gravity,range,impulse,ammo,reload,cost,spread,windup,arc,secondaryCooldown,secondaryCost,color,burn,blast,drain,chain,shotRadius,weight;
    }
    [Serializable] public sealed class ChipEffect { public string stat,op;public double value; }
    [Serializable] public sealed class ContentChip
    { public string id,name,description,rarity,icon,mechanic,author,sourcePack,sourceCard;public string[] tags,excludes;public int max;public ChipEffect[] effects; }
    [Serializable] public sealed class RarityData { public string id,name,color; public double weight,leetWeight; }
    [Serializable] public sealed class ExclusionData { public string spec; public string[] ids; }
    [Serializable] public sealed class ExclusiveOdds { public string spec;public int guaranteed;public double chance; }
    [Serializable] public sealed class ContentRotation { public float x,y,z,w=1; }
    [Serializable] public sealed class ContentBlock
    { public string id,material,role,parent,segment; public float hp; public float[] stages; public V3 pos,size;public ContentRotation rotation;public string[] support;public bool dynamic;public float mass; }
    [Serializable] public sealed class ContentMap
    { public string id,name,tag,theme,music,ambience,look; public int sky,floor,accent;public bool rotation;[NonSerialized] public bool unity;public ContentBlock[] blocks;public MapBounds bounds;public MapSpawns spawns;public StructureDefinition[] segments;public MapNav nav;public MapPilotBlock[] pilotBlocks;public MapHazard[] hazards;public MapRamp[] ramps;public MapNavHints navHints;public TrainingPad[] trainingPads; }
    [Serializable] public sealed class ContentMaterial
    { public string id;public int color;public float mass,durability,friction;public bool indestructible; }
    [Serializable] public sealed class ContentAbilityBot { public float min,max,lead;public bool point,ballistic; }
    [Serializable] public sealed class ContentAbility
    {
        public string spec,slot,id,name,@short,text,kind,fx;public ContentAbilityBot bot;
        public double cooldown,cost,windup,lockout,range,damage,burn,impulse,speed,gravity,shotRadius,pierce,radius,fuse,closeFuse,duration,speedFactor,slowFor,tick,force,arm;
        public bool live;
    }
    [Serializable] public sealed class ContentAds
    { public string spec,overlay;public double fov,spread,settle,move,sens,kick,interval,bloom; }
    [Serializable] public sealed class ConversionContent
    {
        public int schema;public string sourceCommit;[NonSerialized] public string contentRevision;
        public ContentWeapon[] weapons;public ContentChip[] chips;public ContentMap[] maps;
        public ContentMaterial[] materials;public ContentAbility[] abilities;public ContentAds[] ads;
        public NumberValue[] baseStats,config,weights;public RarityData[] rarities;
        public ExclusionData[] incompatibilities;public ExclusiveOdds[] exclusiveOdds;public V3[] spawns;
        public ContentWeapon Weapon(string id) => Array.Find(weapons,x=>x.id==id)??throw new ArgumentException("Unknown weapon: "+id);
        public ContentChip Chip(string id) => Array.Find(chips,x=>x.id==id)??throw new ArgumentException("Unknown chip: "+id);
        public ContentMap Map(string id) {foreach(var map in maps)if(map.id==id)return map;throw new ArgumentException("Unknown map: "+id);}
        public ContentMaterial Material(string id) => Array.Find(materials,x=>x.id==id)??throw new ArgumentException("Unknown material: "+id);
        // Older/incomplete content must still allow rolling; shipped entries are checked by Validate.
        public double Weight(string spec) => (weights==null?null:Array.Find(weights,x=>x!=null&&x.name==spec))?.value??4;
        public ContentAbility Ability(string spec,string slot) => Array.Find(abilities,x=>x.spec==spec&&x.slot==slot);
        public ContentAds Ads(string spec) => NativeRangedRules.Scope(spec)?Array.Find(ads,x=>x.spec==spec):null;
        public void Validate()
        {
            if(schema!=1||sourceCommit==null||sourceCommit.Length!=40)throw new InvalidOperationException("Invalid conversion content receipt");
            if(weapons==null||chips==null||maps==null||baseStats==null)throw new InvalidOperationException("Missing conversion content");
            if(weapons.Select(x=>x.id).Distinct().Count()!=weapons.Length||chips.Select(x=>x.id).Distinct().Count()!=chips.Length||maps.Select(x=>x.id).Distinct().Count()!=maps.Length)throw new InvalidOperationException("Duplicate content IDs");
            foreach(var w in weapons)
                if(weights==null||!weights.Any(x=>x!=null&&x.name==w.id&&!double.IsNaN(x.value)&&!double.IsInfinity(x.value)&&x.value>=0))
                    throw new InvalidOperationException("Missing or invalid weapon weight: "+w.id);
            var stats=new HashSet<string>(baseStats.Select(x=>x.name));
            foreach(var c in chips)foreach(var e in c.effects)if(!stats.Contains(e.stat)||!new[]{"add","mul","set"}.Contains(e.op)||double.IsNaN(e.value)||double.IsInfinity(e.value))throw new InvalidOperationException("Invalid chip effect "+c.id);
            foreach(var map in maps)
            {
                if(map.blocks.Select(b=>b.id).Distinct().Count()!=map.blocks.Length)throw new InvalidOperationException("Duplicate block IDs: "+map.id);
                var ids=new HashSet<string>(map.blocks.Select(b=>b.id));
                foreach(var b in map.blocks)
                {
                    Material(b.material);
                    if(!MathUtil.Finite(b.pos.x)||!MathUtil.Finite(b.pos.y)||!MathUtil.Finite(b.pos.z)||!MathUtil.Finite(b.size.x)||!MathUtil.Finite(b.size.y)||!MathUtil.Finite(b.size.z)||b.size.x<=0||b.size.y<=0||b.size.z<=0)throw new InvalidOperationException("Invalid block: "+b.id);
                    if(b.support!=null&&b.support.Any(s=>!ids.Contains(s)))throw new InvalidOperationException("Unknown support: "+b.id);
                    if(b.rotation==null)throw new InvalidOperationException("Missing block rotation: "+b.id);
                }
            }
        }
    }
    public sealed class ChipStats
    {
        private readonly Dictionary<string,double> values=new Dictionary<string,double>();
        public double this[string key] { get=>values[key];set=>values[key]=value; }
        public double Get(string key,double fallback=0)=>values.TryGetValue(key,out var value)?value:fallback;
        public ChipStats(NumberValue[] defaults) { foreach(var v in defaults)values.Add(v.name,v.value); }
    }
    public static class NativeContentRules
    {
        private static double Clamp(double x,double low,double high)=>Math.Max(low,Math.Min(high,x));
        public static ChipStats Stats(ConversionContent data,string spec,IReadOnlyDictionary<string,int> held)
        {
            var s=new ChipStats(data.baseStats);if(spec=="siren")s["hp"]=15000;
            foreach(var chip in data.chips)
            {
                int count;held.TryGetValue(chip.id,out count);
                for(int i=0;i<Math.Min(chip.max,Math.Max(0,count));i++)foreach(var e in chip.effects)
                    s[e.stat]=Clamp(e.op=="add"?s[e.stat]+e.value:e.op=="mul"?s[e.stat]*e.value:e.value,-1000000,1000000);
            }
            s["playerGravity"]=Clamp(s["playerGravity"],.1,3);s["jumpPower"]=Clamp(s["jumpPower"],.1,2.5);
            s["extraAirJumps"]=Clamp(Math.Floor(s.Get("extraAirJumps")),0,NativeJumpSimulation.MaxAirJumps);
            s["hp"]=Math.Max(15,s["hp"]);s["speed"]=Clamp(s["speed"],.25,2.5);s["attackRate"]=Clamp(s["attackRate"],.15,5);
            foreach(var pair in new[]{("extraProjectiles",11d),("extraStrikes",4d),("split",6d),("projectileSpeed",4d),("critical",.75),("lifeSteal",1d),("jumpPower",2.5),("impulse",12d),("blockForce",6d),("abilityPower",2d),("abilityCharges",2d),("qEmpower",1d),("manaSurge",1d),("abilityLeech",.6),("arcaneSurge",1d),("spellEcho",1d)})s[pair.Item1]=Math.Min(pair.Item2,s[pair.Item1]);
            s["projectileSize"]=Clamp(s["projectileSize"],.4,6);s["knockbackTaken"]=Clamp(s["knockbackTaken"],.04,3);
            s["abilityCooldown"]=Clamp(s["abilityCooldown"],.45,3);s["abilityRadius"]=Clamp(s["abilityRadius"],.5,1.5);s["abilityCost"]=Math.Max(.3,s["abilityCost"]);
            return s;
        }
        public static List<ContentChip> Eligible(ConversionContent data,string spec,IReadOnlyDictionary<string,int> held)
        {
            var weapon=data.Weapon(spec);var tags=new HashSet<string>{"all",weapon.@class};if(spec!="ansatsuken")tags.Add("roll");
            if(new[]{"leet","ansatsuken","emperor","siren"}.Contains(spec))tags.Add(spec);
            if(weapon.@class!="melee")tags.Add("projectile");if(weapon.ammo>0)tags.Add("gun");
            if(!weapon.devOnly&&(weapon.@class=="ranged"||weapon.@class=="magic"))
            {tags.Add("ability");if(weapon.@class=="magic"){tags.Add("spellcraft");tags.Add("spell-"+spec);}else tags.Add("gunplay");}
            bool Has(string id)=>held.TryGetValue(id,out int n)&&n>0;
            var blocked=new HashSet<string>(data.chips.Where(c=>Has(c.id)&&c.excludes!=null).SelectMany(c=>c.excludes));
            var excluded=Array.Find(data.incompatibilities,x=>x.spec==spec);
            return data.chips.Where(c=>c.tags.Any(tags.Contains)&&(!held.TryGetValue(c.id,out int n)||n<c.max)&&!blocked.Contains(c.id)&&!(c.excludes?.Any(Has)??false)&&!(excluded?.ids.Contains(c.id)??false)).ToList();
        }
        public static List<string> Offers(ConversionContent data,string spec,IReadOnlyDictionary<string,int> held,Func<double> random,int count=6,Func<ContentChip,bool> filter=null,Dictionary<string,double> weights=null,bool exclusives=true)
        {
            var pool=Eligible(data,spec,held);if(filter!=null)pool.RemoveAll(c=>!filter(c));var result=new List<string>();count=Math.Max(1,Math.Min(12,count));
            var odds=exclusives?Array.Find(data.exclusiveOdds,x=>x.spec==spec):null;
            while(pool.Count>0&&result.Count<count)
            {
                var exclusive=odds==null?new List<ContentChip>():pool.Where(c=>c.tags.Contains(spec)).ToList();ContentChip pick;
                if(exclusive.Count>0&&(result.Count<odds.guaranteed||random()<odds.chance))pick=exclusive[Math.Min(exclusive.Count-1,(int)Math.Floor(Math.Max(0,random())*exclusive.Count))];
                else
                {
                    var tiers=data.rarities.Where(r=>pool.Any(c=>c.rarity==r.id)).ToArray();bool leetWeights=tiers.Any(r=>r.leetWeight>0);
                    double Weight(RarityData r)=>weights!=null?(weights.TryGetValue(r.id,out double value)?value:0):spec=="leet"?(leetWeights?r.leetWeight:1):r.weight;
                    double roll=Clamp(random(),0,.999999999)*tiers.Sum(Weight);var tier=tiers[tiers.Length-1];
                    foreach(var r in tiers){roll-=Weight(r);if(roll<0){tier=r;break;}}
                    var choices=pool.Where(c=>c.rarity==tier.id).ToArray();pick=choices[Math.Min(choices.Length-1,(int)Math.Floor(Math.Max(0,random())*choices.Length))];
                }
                result.Add(pick.id);pool.Remove(pick);
            }
            return result;
        }
        public static RollValues Roll(ConversionContent data,string spec,ChipStats s)
        {
            double weight=data.Weight(spec),duration=(.4+weight*.0125)/Clamp(s["rollSpeed"],.75,1.65);
            return new RollValues{weight=weight,duration=duration,distance=(6.2-weight*.18)*Clamp(s["rollDistance"],.65,2),cost=Math.Ceiling((20+weight*.75)*Clamp(s["rollCost"],.4,2)),iframeStart=.075,iframeSeconds=Math.Min(Math.Min(.18+Clamp(s["rollWindow"],0,.10),duration*.6),duration-.075-.09),cooldown=Math.Max(duration+.12,.75*s["dashCooldown"])};
        }
    }
    [Serializable] public sealed class RollValues { public double weight,duration,distance,cost,iframeStart,iframeSeconds,cooldown; }
    public sealed class SourceRandom
    { private uint state;public SourceRandom(uint seed){state=seed;}public double Next(){state=unchecked(state*1664525+1013904223);return state/4294967296d;} }
}
