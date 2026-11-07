using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ChipStatText {public string text,tone;}
    // Runtime-added chips have no row in the pinned ChipText export. Use the same per-copy
    // stat semantics as src/shared/chip-stats.ts; the existing card renderer owns the colors.
    public static class NativeChipStatText
    {
        private static readonly HashSet<string> Lower=new HashSet<string>(StringComparer.Ordinal){"damageTaken","knockbackTaken","dashCooldown","debrisTaken","reloadSpeed","resourceCost","blockCooldown","rollCost","abilityCooldown","abilityCost"};
        private static readonly HashSet<string> Neutral=new HashSet<string>(StringComparer.Ordinal){"projectileGravity","playerGravity","recoil","fanDegrees"};
        private static readonly HashSet<string> Fraction=new HashSet<string>(StringComparer.Ordinal){"lifeSteal","execute","closeDamage","farDamage","backstab","critical","thorns","reach","demolition","ammoCapacity","bounceRetention","lastRound","qEmpower","manaSurge","abilityLeech","arcaneSurge"};
        private static readonly HashSet<string> Seconds=new HashSet<string>(StringComparer.Ordinal){"blockWindow","rollWindow"};
        private static readonly HashSet<string> Switch=new HashSet<string>(StringComparer.Ordinal){"pong","boomerang","zigzag","chaos","spellEcho"};
        private static readonly Dictionary<string,string> Labels=new Dictionary<string,string>(StringComparer.Ordinal){
            {"hp","Health"},
            {"damage","Damage"},
            {"attackRate","Attack speed"},
            {"speed","Move speed"},
            {"damageTaken","Damage taken"},
            {"knockbackTaken","Knockback taken"},
            {"dashCooldown","Dodge cooldown"},
            {"impulse","Knockback dealt"},
            {"demolition","Structure damage"},
            {"debrisTaken","Debris damage taken"},
            {"ammoCapacity","Magazine size"},
            {"maxMana","Max mana"},
            {"maxStamina","Max stamina"},
            {"projectileSpeed","Projectile speed"},
            {"blockCooldown","Block cooldown"},
            {"blockWindow","Block window"},
            {"reflectDamage","Reflected damage"},
            {"reflectSpeed","Reflected speed"},
            {"blockForce","Block knockback"},
            {"manaRegen","Mana regen"},
            {"staminaRegen","Stamina regen"},
            {"resourceCost","Ability cost"},
            {"reloadSpeed","Reload time"},
            {"projectileGravity","Projectile drop"},
            {"projectileSize","Projectile size"},
            {"jumpPower","Jump height"},
            {"playerGravity","Gravity"},
            {"reach","Melee reach"},
            {"meleeArc","Swing width"},
            {"fanDegrees","Spread"},
            {"bounceRetention","Bounce damage kept"},
            {"lifeSteal","Life steal"},
            {"execute","Damage to enemies below 30% health"},
            {"closeDamage","Close-range damage"},
            {"farDamage","Long-range damage"},
            {"backstab","Backstab damage"},
            {"critical","Critical chance"},
            {"thorns","Damage reflected"},
            {"lastRound","Last-round damage"},
            {"regeneration","Health / s regen"},
            {"reloadHeal","Health per reload"},
            {"blockHeal","Health per Block"},
            {"blockAmmo","Ammo per Block"},
            {"dashBlast","Dodge blast damage"},
            {"recoil","Recoil"},
            {"vacuum","Hit pull"},
            {"bounces","Bounce"},
            {"split","Split level"},
            {"pierces","Pierce"},
            {"burn","Burn DPS"},
            {"poison","Poison DPS"},
            {"staticDamage","Discharge damage"},
            {"shrapnel","Blast damage"},
            {"extraProjectiles","Projectile"},
            {"extraStrikes","Follow-up strike"},
            {"homing","Homing °/s"},
            {"blockPulseDamage","Pulse damage"},
            {"blockPulseImpulse","Pulse knockback"},
            {"rollDistance","Roll distance"},
            {"rollSpeed","Roll speed"},
            {"rollCost","Roll cost"},
            {"rollWindow","Roll invulnerability"},
            {"abilityCooldown","Ability cooldown"},
            {"abilityPower","Ability damage"},
            {"abilityRadius","Ability radius"},
            {"abilityCost","Spell mana cost"},
            {"abilityCharges","E charge"},
            {"qEmpower","Next bolts after Q"},
            {"manaSurge","Ability damage as mana"},
            {"abilityLeech","Ability life steal"},
            {"arcaneSurge","Ability damage at high mana"},
            {"extraAirJumps","Air jump"}};
        private static string Number(double v){double a=Math.Abs(v);return Math.Round(a,a>=10?0:a>=1?1:a>=.1?2:3,MidpointRounding.AwayFromZero).ToString("0.###",CultureInfo.InvariantCulture);}
        private static string Percent(double v){double a=Math.Abs(v*100);return Math.Round(a,a>=10?0:1,MidpointRounding.AwayFromZero).ToString("0.#",CultureInfo.InvariantCulture)+"%";}
        private static string Label(string stat)=>Labels.TryGetValue(stat,out var label)?label:Regex.Replace(stat,"([A-Z])"," $1");
        private static string Tone(string stat,bool up)=>Neutral.Contains(stat)?"neutral":up!=Lower.Contains(stat)?"good":"bad";
        public static ChipStatText Effect(ChipEffect f)
        {
            if(f==null||string.IsNullOrEmpty(f.stat)||double.IsNaN(f.value)||double.IsInfinity(f.value)||Switch.Contains(f.stat))return null;
            string stat=f.stat;double value=f.value;
            if(stat=="staticEvery")return f.op=="set"?new ChipStatText{text="Discharge every "+Number(value)+" hits",tone="good"}:null;
            if(stat=="vacuum")return value>0?new ChipStatText{text="Hits pull enemies inward",tone="good"}:null;
            // Absolute values without a baseline cannot be called a buff or penalty.
            if(f.op=="set")return new ChipStatText{text=Number(value)+(stat=="fanDegrees"?"°":"")+" "+Label(stat),tone="neutral"};
            if(f.op!="add"&&f.op!="mul")return null;
            double delta=f.op=="mul"?value-1:value;if(Math.Abs(delta)<1e-9)return null;
            bool up=delta>0;string amount=f.op=="mul"||Fraction.Contains(stat)?Percent(delta):Number(delta)+(stat=="fanDegrees"?"°":Seconds.Contains(stat)?" s":"");
            bool plural=f.op=="add"&&Math.Abs(value)!=1&&(stat=="bounces"||stat=="pierces"||stat=="extraProjectiles"||stat=="extraStrikes"||stat=="abilityCharges"||stat=="extraAirJumps");
            return new ChipStatText{text=(up?"+":"-")+amount+" "+Label(stat)+(plural?"s":""),tone=Tone(stat,up)};
        }
        public static ChipStatText[] Lines(ContentChip chip)=>(chip.effects??Array.Empty<ChipEffect>()).Select(Effect).Where(x=>x!=null).OrderBy(x=>x.tone=="good"?0:x.tone=="neutral"?1:2).ToArray();
    }
}
