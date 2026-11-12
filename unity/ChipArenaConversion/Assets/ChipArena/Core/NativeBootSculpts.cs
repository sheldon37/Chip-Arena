using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The pure sculpts behind NativeSourceModels' slowest builds, made on a worker while Unity's splash plays under the logo
    /// intro (docs/BOOT-INTRO.md; started by NativeBootPrefetch): the Siren minion kinds and the zone kinds' props
    /// (HordeKindModels, IronworksModels, StationPropModels), the robot helmets (HelmModels) and the Standard weapons (WeaponSkins).
    /// The builders still run where they always did, in presentation.Initialize, and in the same order; each asks Take for its
    /// sculpt, which waits for the whole worker the first time (so no other sculpt runs beside it on the main thread) and hands
    /// over that one result, or sculpts it there as before when nothing was made (no intro, a key not in the list, a second
    /// bank). Engine-free: every job is a Core function of its arguments alone (CoreTests run them on several threads at once and
    /// hold the results to the single-threaded ones).
    /// </summary>
    public static class NativeBootSculpts
    {
        public static string Horde(int kind,int look,int tier)=>"horde/"+kind+"/"+look+"/"+tier;
        public static string Cream(int look,int tier)=>"cream/"+look+"/"+tier;
        public static string Prop(int prop,int tier)=>"iw-prop/"+prop+"/"+tier;
        public static string Crust(int look,int tier)=>"iw-crust/"+look+"/"+tier;
        public static string StationProp(NativeHordeModels.StationProp prop,int tier)=>"station-prop/"+(int)prop+"/"+tier;
        public const string Helm="helm/full",HalfMask="helm/half";public static string BrokenHelm(int variant)=>"helm/broken/"+variant;
        public static string Weapon(string spec,int tier)=>"weapon/"+spec+"/"+tier;public static string Loose(string spec)=>"weapon-loose/"+spec;
        /// <summary>The Standard weapons WeaponSkins builds, in its order (the designed ones among them).</summary>
        public static IEnumerable<string> WeaponSpecs()
        {
            foreach(var spec in NativeWeaponSkins.Weapons)yield return spec;
            yield return NativeBombs.Spec;yield return NativeTopDog.Spec;yield return NativeGlaive.Spec;yield return NativeKatana.Spec;yield return NativeFangs.Spec;
        }
        /// <summary>Every job, keyed as the builders ask for it.</summary>
        public static IEnumerable<(string key,Func<object> make)> Jobs()
        {
            for(int kind=0;kind<NativeHordeKinds.All;kind++)for(int look=0;look<NativeHordeKinds.Looks[kind];look++)for(int tier=0;tier<NativeHordeModels.Tiers;tier++)
            {int k=kind,l=look,t=tier;yield return (Horde(k,l,t),()=>NativeHordeModels.Build(k,l,t));}
            for(int p=0;p<NativeHordeModels.PropCount;p++)for(int t=0;t<NativeHordeModels.PropTiers;t++){int pp=p,tt=t;yield return (Prop(pp,tt),()=>NativeHordeModels.IronworksProp(pp,tt));}
            for(int l=0;l<2;l++)for(int t=0;t<NativeHordeModels.Tiers;t++){int ll=l,tt=t;yield return (Crust(ll,tt),()=>NativeHordeModels.IronworksCrust(ll,tt));}
            for(int l=0;l<NativeHordeKinds.Looks[NativeHordeKinds.Brute];l++)for(int t=0;t<NativeHordeModels.Tiers;t++){int ll=l,tt=t;yield return (Cream(ll,tt),()=>NativeHordeModels.Build(NativeHordeKinds.Brute,ll,tt,NativeIronworksKinds.BruteSeam));}
            foreach(NativeHordeModels.StationProp prop in Enum.GetValues(typeof(NativeHordeModels.StationProp)))for(int t=0;t<NativeHordeModels.Tiers;t++){var pp=prop;int tt=t;yield return (StationProp(pp,tt),()=>NativeHordeModels.BuildStationProp(pp,tt));}
            yield return (Helm,()=>NativeRobotHelm.Helm());yield return (HalfMask,()=>NativeRobotHelm.Half());
            for(int b=0;b<NativeRobotHelm.BrokenCount;b++){int bb=b;yield return (BrokenHelm(bb),()=>NativeRobotHelm.Broken(bb));}
            foreach(var spec in WeaponSpecs())
            {
                if(!NativeWeaponModels.Designed(spec))continue;var s=spec;
                for(int t=0;t<NativeWeaponModels.Tiers;t++){int tt=t;yield return (Weapon(s,tt),()=>NativeWeaponModels.Build(s,tt));}
                yield return (Loose(s),()=>NativeWeaponModels.Loose(s));
            }
        }
        private static Task worker;private static Dictionary<string,object> made;
        /// <summary>Taken from the worker, sculpted by the caller after all (diagnostics: BOOT_HANDOFF and CoreTests).</summary>
        public static int Made,Taken,Fallbacks;
        public static double WorkerMs{get;private set;}
        public static bool Started=>worker!=null;
        /// <summary>Starts the worker (once until Reset).</summary>
        public static void Start()
        {
            if(worker!=null)return;var into=new Dictionary<string,object>();made=into;
            worker=Task.Run(()=>{var at=System.Diagnostics.Stopwatch.StartNew();foreach(var (key,make) in Jobs()){var r=make();lock(into){into[key]=r;Made++;}}WorkerMs=at.Elapsed.TotalMilliseconds;});
        }
        /// <summary>A new run (Unity's SubsystemRegistration): nothing made, nothing pending.</summary>
        public static void Reset(){worker=null;made=null;Made=Taken=Fallbacks=0;WorkerMs=0;}
        /// <summary>The sculpt for `key`: the worker's (waiting for the worker to finish, the first time), handed over once; else
        /// `make()` here, as the builder always did. A worker that failed leaves its unfinished keys to `make()`, which fails the
        /// same way.</summary>
        public static T Take<T>(string key,Func<T> make) where T:class
        {
            var w=worker;var into=made;
            if(w!=null&&into!=null)
            {
                try{w.Wait();}catch(AggregateException){}
                lock(into)if(into.TryGetValue(key,out var r)&&r is T t){into.Remove(key);Taken++;return t;}
            }
            Fallbacks++;return make();
        }
        /// <summary>Made but never asked for (a list that drifted from its builders shows here).</summary>
        public static int Left{get{var into=made;if(into==null)return 0;lock(into)return into.Count;}}
    }
}
