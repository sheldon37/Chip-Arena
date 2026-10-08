using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    // The source's class and specialization card text and grouping, for the menus' class cards and weapon cards:
    // CLASS_INFO names and defaults (shared/content.ts:175), the class taglines (client/main.ts:119), weaponInClass and
    // availableClasses (shared/lobby-presentation.ts:12-15) and the specialization chip's description and stat line
    // (client/main.ts:157). Presentation only: picking still sets the one preferred weapon the game already saves.
    public static class NativeLoadoutText
    {
        public static readonly string[] Classes={"ranged","magic","melee","dev"};
        public static string Name(string c)=>c=="ranged"?"Ranged":c=="magic"?"Magic":c=="melee"?"Melee":"???";
        public static string Tagline(string c)=>c=="ranged"?"PRECISION & PRESSURE":c=="magic"?"ELEMENTS & CONTROL":c=="melee"?"CLOSE & PERSONAL":"DEV CLASSES";
        public static string Default(string c)=>c=="ranged"?"pistol":c=="magic"?"fire":c=="melee"?"sword":"emperor";
        // The DEV class is offered to developers only; the other three to everyone.
        public static bool Offered(string c,bool developer)=>c!="dev"||developer;
        // weaponInClass: DEV holds the developer weapons (developers only); every other class holds its own weapons, a
        // restricted one only when `allowed` (a developer, or an account granted that weapon).
        public static bool InClass(ContentWeapon w,string c,bool developer,bool allowed)=>w!=null&&allowed&&(c=="dev"?developer&&w.devOnly:w.@class==c);
        // The class a weapon is shown under when no class has been picked yet.
        public static string ClassOf(ContentWeapon w,bool developer)=>w==null?"ranged":w.devOnly&&developer?"dev":w.@class;
        // preferredWeapon: the current weapon when it is in the class, otherwise the class default (or, when the default is
        // not available, the first weapon of the class in content order).
        public static string Pick(IList<ContentWeapon> weapons,string current,string c,bool developer,Func<ContentWeapon,bool> allowed)
        {
            ContentWeapon first=null,fallback=null;
            foreach(var w in weapons)
            {
                if(!InClass(w,c,developer,allowed(w)))continue;if(w.id==current)return w.id;
                if(first==null)first=w;if(w.id==Default(c))fallback=w;
            }
            return (fallback??first)?.id??current;
        }
        public static string Description(ContentWeapon w)=>(w?.description??"")+(w!=null&&w.gravity>0?" Projectiles fall; aim higher at range.":"");
        // `${damage} DMG · ${(1/interval).toFixed(1)} / SEC` + cost (STAMINA for melee, MANA otherwise) + melee range.
        // Invariant formatting: comma-decimal Windows locales must still read 2.5, not 2,5.
        public static string Stats(ContentWeapon w)
        {
            if(w==null)return "";var inv=CultureInfo.InvariantCulture;bool melee=w.@class=="melee";
            string s=w.damage.ToString(inv)+" DMG · "+(w.interval>0?(1/w.interval).ToString("0.0",inv):"0.0")+" / SEC";
            if(w.cost!=0)s+=" · "+w.cost.ToString(inv)+(melee?" STAMINA":" MANA");
            if(melee)s+=" · "+w.range.ToString(inv)+" M";
            return s;
        }
    }
}
