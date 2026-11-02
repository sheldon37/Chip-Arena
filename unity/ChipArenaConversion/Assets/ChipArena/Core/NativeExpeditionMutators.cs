using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Versioned gameplay catalog. IDs are reconstructed from the authenticated deployment draw.
    public static class NativeExpeditionMutators
    {
        public const string Version="expedition-mutators-1";
        private static readonly string[] ids={"jackpot","overclock","moon-boots","bottomless","second-wind","windfall","vampire","adrenaline","chip-rain","headhunter","hulkamania","sprinters","iron-hide","swarm","paper-pilots","permadeath","darkness","whiteout","broke","bloated","brittle","surge","storm","glass-cannon","high-explosive","bounce-house","friendly-fire","roulette","martyr","big-heads"};
        public static string[] Ids=>(string[])ids.Clone();
        public static bool Has(string[] active,string id)=>active!=null&&Array.IndexOf(active,id)>=0;
        public static bool Known(string id)=>Array.IndexOf(ids,id)>=0;
        public static bool Compatible(string a,string b)=>!((a=="broke"&&(b=="jackpot"||b=="windfall"))||(b=="broke"&&(a=="jackpot"||a=="windfall"))||(a=="second-wind"&&b=="permadeath")||(b=="second-wind"&&a=="permadeath")||(a=="darkness"&&b=="whiteout")||(b=="darkness"&&a=="whiteout"));
        public static bool Valid(string[] a)=>a!=null&&a.Length<=3&&a.All(Known)&&a.Distinct().Count()==a.Length&&a.All(x=>a.All(y=>Compatible(x,y)));
        public static double Payout(string[] a)=>1+(a??Array.Empty<string>()).Sum(x=>Array.IndexOf(ids,x) is int n&&n>=10&&n<21?.25:n>=21?.1:0);
        public static double Money(string[] a,string kind)=>Payout(a)*(Has(a,"jackpot")?2:1)*(Has(a,"broke")?.5:1)*(Has(a,"friendly-fire")?1.5:1)*(Has(a,"hulkamania")&&kind=="hulk"?3:1);
        public static int CachePrice(string[] a,int price)=>Math.Max(1,(int)Math.Ceiling(price*(Has(a,"windfall")?.5:1)*(Has(a,"broke")?2:1)));
        public static int Cap(string[] a,int cap)=>Has(a,"hulkamania")?Math.Min(6,cap):Has(a,"swarm")?Math.Min(72,cap*2):cap;
        public static double Interval(string[] a)=>Has(a,"hulkamania")?2.5:Has(a,"swarm")?.5:1;
        public static string Kind(string[] a,string kind)=>Has(a,"hulkamania")?"hulk":Has(a,"sprinters")&&kind=="shambler"?"runner":kind;
        public static float ZombieSpeed(string[] a)=>(Has(a,"sprinters")?1.15f:1)*(Has(a,"surge")?1.35f:1);
        public static void PilotStats(string[] a,ChipStats stats,string spec=null)
        {
            if(Has(a,"adrenaline")){stats["rollCost"]=0;if(new[]{"sword","daggers","greatsword","shield","ansatsuken","emperor"}.Contains(spec))stats["resourceCost"]=0;}
            stats["speed"]*=(Has(a,"overclock")?1.33:1)*(Has(a,"surge")?1.25:1);
            stats["playerGravity"]*=Has(a,"moon-boots")?.5:1;
            stats["hp"]*=Has(a,"paper-pilots")?.5:1;
            stats["knockbackTaken"]*=Has(a,"bounce-house")?3:1;
        }
    }
}
