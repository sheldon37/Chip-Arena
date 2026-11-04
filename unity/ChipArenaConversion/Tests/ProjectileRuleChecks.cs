using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class ProjectileRuleChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("Projectile rules: "+why);n++;}
        var data=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});data.Validate();
        var ids=new[]{"pistol","machinegun","fire","water","lightning","darkness"};var speeds=new[]{250d,200,100,110,120,80};
        for(int i=0;i<ids.Length;i++)
        {
            var w=data.Weapon(ids[i]);var stats=NativeContentRules.Stats(data,w.id,new Dictionary<string,int>());
            Check(w.speed==speeds[i]&&NativeProjectileRules.Gravity(w,stats)==0&&!NativeProjectileRules.Instant(w,stats,false),"straight profile "+w.id);
            var v=new V3(0,0,(float)w.speed);var p=new V3();for(int tick=0;tick<30;tick++)p+=NativeSpecialRules.Ballistic(ref v,NativeProjectileRules.Gravity(w,stats),1f/60);
            Check(p.y==0&&Math.Abs(p.z-w.speed*.5)<.001,"integrated half-second path "+w.id);
            stats["projectileGravityAdd"]=20;Check(NativeProjectileRules.Gravity(w,stats)==20,"add gravity to straight shot");stats["projectileGravity"]=.5;Check(NativeProjectileRules.Gravity(w,stats)==10,"gravity modifier combines");
        }
        var rifle=data.Weapon("rifle");var s=NativeContentRules.Stats(data,"rifle",new Dictionary<string,int>());
        Check(NativeProjectileRules.Instant(rifle,s,false),"plain rifle instant");Check(!NativeProjectileRules.Instant(rifle,s,true),"runtime travel effect");
        foreach(string stat in new[]{"homing","boomerang","zigzag","pong","projectileGravityAdd","projectileSpeed"})
        {double old=s[stat];s[stat]=stat=="projectileSpeed"?.5:1;Check(!NativeProjectileRules.Instant(rifle,s,false),"travel stat "+stat);s[stat]=old;}
        foreach(string stat in new[]{"split","extraProjectiles","bounces","pierces","burn","poison"})
        {double old=s[stat];s[stat]=2;Check(NativeProjectileRules.Instant(rifle,s,false),"instant-compatible "+stat);s[stat]=old;}
        foreach(string chip in new[]{"lobber","meteor"})
        {var stats=NativeContentRules.Stats(data,"rifle",new Dictionary<string,int>{{chip,1}});Check(NativeProjectileRules.Gravity(rifle,stats)>12&&!NativeProjectileRules.Instant(rifle,stats,false),"existing ballistic chip "+chip);}
        var mixed=NativeContentRules.Stats(data,"pistol",new Dictionary<string,int>{{"lobber",1},{"feather",1}});
        Check(Math.Abs(NativeProjectileRules.Gravity(data.Weapon("pistol"),mixed)-32*2.2*.45)<.001,"ballistic plus reduction chip composition");
        string profile=NativeProjectileRules.ContentKey(data.weapons);double priorSpeed=rifle.speed;rifle.speed+=1;
        Check(profile!=NativeProjectileRules.ContentKey(data.weapons),"speed changes compatibility key");rifle.speed=priorSpeed;
        var culture=System.Globalization.CultureInfo.CurrentCulture;
        try{System.Globalization.CultureInfo.CurrentCulture=new System.Globalization.CultureInfo("fr-FR");Check(profile==NativeProjectileRules.ContentKey(data.weapons),"profile identity is locale independent");}
        finally{System.Globalization.CultureInfo.CurrentCulture=culture;}
        var legacy=new ContentWeapon();Check(NativeProjectileRules.Range(legacy)==160&&NativeProjectileRules.Lifetime(legacy)==3&&legacy.gravityMultiplier==1&&legacy.projectileBounceRetention==1,"old content defaults");
        legacy.projectileRange=42;legacy.projectileLifetime=.5;Check(NativeProjectileRules.Range(legacy)==42&&NativeProjectileRules.Lifetime(legacy)==.5,"configured budgets");
        legacy.projectileMode="bad";bool rejected=false;try{NativeProjectileRules.ValidateProfile(legacy);}catch(InvalidOperationException){rejected=true;}Check(rejected,"invalid mode rejected");
        Check(data.Weapon("bombadier").speed==28&&data.Weapon("bombadier").gravity==23&&data.Weapon("bombadier").range==25,"longer TNT throws retained");
        Check(data.Weapon("bow").speed==45&&data.Weapon("bow").gravity==30,"bow retained");
        // Exercise every shipped chip's stat resolution, including composed multiplier limits.
        foreach(var chip in data.chips){var stats=NativeContentRules.Stats(data,"rifle",new Dictionary<string,int>{{chip.id,chip.max}});Check(!float.IsNaN(NativeProjectileRules.Gravity(rifle,stats)),"chip profile "+chip.id);NativeProjectileRules.Instant(rifle,stats,false);}
        Console.WriteLine("PASS projectile rules: "+n+" checks");
    }
}
