using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChipArena.UnityConversion;

// Engine-free checks for the menu class and weapon cards' text and grouping (NativeLoadoutText) against the source
// (client/main.ts:119,157, shared/content.ts:175, shared/lobby-presentation.ts:12-21). Drawing is not exercised.
internal static class LoadoutChecks
{
    public static IEnumerable<string> Run(ConversionContent data)
    {
        var expected=new Dictionary<string,string>
        {
            ["siren"]="9 DMG · 2.5 / SEC · 2.3 M",["emperor"]="20 DMG · 2.6 / SEC · 6 STAMINA · 2.4 M",["ansatsuken"]="39 DMG · 1.1 / SEC · 12 STAMINA · 2 M",
            ["leet"]="32 DMG · 1.7 / SEC · 7 MANA",["pistol"]="23 DMG · 2.4 / SEC",["rifle"]="58 DMG · 0.9 / SEC",["machinegun"]="8 DMG · 11.8 / SEC",
            ["fire"]="24 DMG · 2.0 / SEC · 8 MANA",["water"]="20 DMG · 2.6 / SEC · 6 MANA",["lightning"]="15 DMG · 3.6 / SEC · 5 MANA",["darkness"]="34 DMG · 1.5 / SEC · 10 MANA",
            ["sword"]="30 DMG · 1.6 / SEC · 14 STAMINA · 2.5 M",["daggers"]="15 DMG · 2.9 / SEC · 7 STAMINA · 1.75 M",["shield"]="23 DMG · 1.3 / SEC · 16 STAMINA · 2.1 M",
            ["greatsword"]="52 DMG · 0.8 / SEC · 20 STAMINA · 3.4 M",["bow"]="18\u201362 DMG · 0.8 S DRAW",
        };
        if(data.weapons.Length!=expected.Count)throw new Exception("Weapon count changed: "+data.weapons.Length);
        var previous=CultureInfo.CurrentCulture;
        // A comma-decimal locale (built by hand, so the check also runs where ICU culture data is unavailable).
        var comma=(CultureInfo)CultureInfo.InvariantCulture.Clone();comma.NumberFormat.NumberDecimalSeparator=",";comma.NumberFormat.NumberGroupSeparator=".";
        try
        {
            // A comma-decimal Windows locale must not change the card text.
            foreach(var culture in new[]{CultureInfo.InvariantCulture,comma})
            {
                CultureInfo.CurrentCulture=culture;
                foreach(var w in data.weapons){string s=NativeLoadoutText.Stats(w);if(s!=expected[w.id])throw new Exception("Stat line "+w.id+" ("+culture.NumberFormat.NumberDecimalSeparator+"): '"+s+"' expected '"+expected[w.id]+"'");}
            }
        }
        finally{CultureInfo.CurrentCulture=previous;}
        yield return "Weapon card stat lines match the source's specialization chips for all 15 weapons and the Bow's draw range, in any locale";
        var hinted=data.weapons.Where(w=>NativeLoadoutText.Description(w).EndsWith(" Projectiles fall; aim higher at range.",StringComparison.Ordinal)).Select(w=>w.id).ToArray();
        if(string.Join(",",hinted)!="pistol,rifle,machinegun,fire,water,lightning,darkness,bow")throw new Exception("Gravity hint weapons: "+string.Join(",",hinted));
        if(data.weapons.Any(w=>!NativeLoadoutText.Description(w).StartsWith(w.description,StringComparison.Ordinal)))throw new Exception("Descriptions must stay verbatim");
        yield return "Descriptions stay verbatim and only the eight falling-projectile weapons (the Bow's arrows too) get the aim-higher hint";
        string List(string c,bool developer,Func<ContentWeapon,bool> allows)=>string.Join(",",data.weapons.Where(w=>NativeLoadoutText.InClass(w,c,developer,allows(w))).Select(w=>w.id));
        void Expect(string what,string actual,string wanted){if(actual!=wanted)throw new Exception(what+": "+actual+" expected "+wanted);}
        Func<ContentWeapon,bool> guest=w=>!w.devOnly,dev=w=>true,granted=w=>!w.devOnly||w.id=="siren";
        Expect("guest ranged",List("ranged",false,guest),"pistol,rifle,machinegun,bow");Expect("guest magic",List("magic",false,guest),"fire,water,lightning,darkness");
        Expect("guest melee",List("melee",false,guest),"sword,daggers,shield,greatsword");Expect("guest dev",List("dev",false,guest),"");
        Expect("developer magic",List("magic",true,dev),"leet,fire,water,lightning,darkness");Expect("developer melee",List("melee",true,dev),"siren,emperor,ansatsuken,sword,daggers,shield,greatsword");
        Expect("developer dev",List("dev",true,dev),"siren,emperor,ansatsuken,leet");Expect("granted melee",List("melee",false,granted),"siren,sword,daggers,shield,greatsword");Expect("granted dev",List("dev",false,granted),"");
        if(string.Join(",",NativeLoadoutText.Classes.Where(c=>NativeLoadoutText.Offered(c,false)))!="ranged,magic,melee"||!NativeLoadoutText.Offered("dev",true))throw new Exception("Offered classes");
        yield return "Class grouping follows weaponInClass for guests, developers and single-weapon grants (DEV class for developers only)";
        Expect("names",string.Join("|",NativeLoadoutText.Classes.Select(NativeLoadoutText.Name)),"Ranged|Magic|Melee|???");
        Expect("taglines",string.Join("|",NativeLoadoutText.Classes.Select(NativeLoadoutText.Tagline)),"PRECISION & PRESSURE|ELEMENTS & CONTROL|CLOSE & PERSONAL|DEV CLASSES");
        Expect("keep in class",NativeLoadoutText.Pick(data.weapons,"daggers","melee",false,guest),"daggers");Expect("class default",NativeLoadoutText.Pick(data.weapons,"daggers","magic",false,guest),"fire");
        Expect("dev default",NativeLoadoutText.Pick(data.weapons,"pistol","dev",true,dev),"emperor");Expect("developer keeps a DEV weapon under its class",NativeLoadoutText.Pick(data.weapons,"emperor","melee",true,dev),"emperor");
        Expect("first in class when the default is unavailable",NativeLoadoutText.Pick(data.weapons,"pistol","melee",false,w=>!w.devOnly&&w.id!="sword"),"daggers");
        Expect("class of a developer's restricted weapon",NativeLoadoutText.ClassOf(data.Weapon("leet"),true),"dev");Expect("class of a granted weapon",NativeLoadoutText.ClassOf(data.Weapon("siren"),false),"melee");
        yield return "Class names, taglines and the class switch's weapon choice match CLASS_INFO and preferredWeapon";
    }
}
