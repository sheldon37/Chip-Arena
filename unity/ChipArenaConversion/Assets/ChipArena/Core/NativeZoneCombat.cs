using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Authority rules shared by the director, wire validator and native fixtures.
    public static class NativeZoneCombat
    {
        public static bool Station(string map)=>map==NativeStationKinds.Map;
        public static bool Ironworks(string map)=>NativeIronworksKit.Active(map);
        public static bool Active(string map)=>Station(map)||Ironworks(map);
        public static int Wave(int progress,int target)=>progress<target*.15?1:progress<target*.5?2:progress<target*.85?3:4;
        private static NumberValue[] Mix(string ids,params double[] weights)=>ids.Split(',').Select((id,i)=>new NumberValue{name=id,value=weights[i]}).ToArray();
        private static readonly NumberValue[][] StationMix={
            Mix("shambler,runner,spitter,conductor",.7,.25,.05,.03),
            Mix("shambler,runner,spitter,brute,hulk,linesman,conductor,undertow",.5,.3,.12,.06,.03,.04,.04,.03),
            Mix("shambler,runner,spitter,brute,hulk,linesman,conductor,undertow,pileup",.42,.3,.12,.1,.05,.06,.05,.05,.03),
            Mix("shambler,runner,spitter,brute,hulk,linesman,conductor,undertow,pileup",.4,.3,.1,.12,.06,.06,.06,.04,.04)};
        private static readonly NumberValue[][] IronMix={
            Mix("shambler,cinderling,stoker",.55,.35,.1),
            Mix("shambler,cinderling,stoker,slagback,rigger,spitter",.33,.33,.14,.1,.05,.05),
            Mix("shambler,cinderling,stoker,slagback,rigger,brute,ladleman,hulk",.22,.28,.13,.15,.08,.05,.05,.04),
            Mix("shambler,cinderling,stoker,slagback,rigger,brute,ladleman,hulk",.18,.27,.13,.15,.1,.05,.07,.05)};
        private static readonly NumberValue[] IronBoss=Mix("cinderling,stoker,slagback,rigger,shambler",.45,.15,.2,.1,.1),StationBoss=Mix("shambler,runner,linesman,conductor",.5,.3,.1,.1);
        private static readonly NumberValue[][] StationBossPhases={Mix("shambler,runner,conductor",.6,.3,.1),Mix("shambler,runner,linesman,conductor,undertow",.45,.25,.15,.1,.05),Mix("shambler,runner,linesman,conductor,undertow,pileup",.4,.3,.1,.1,.05,.05)};
        public static NumberValue[] BossEncounter(string map,int phase)=>Station(map)?StationBossPhases[Math.Max(0,Math.Min(2,phase-1))]:IronBoss;
        public static NumberValue[] Encounter(string map,int wave,bool boss)=>Station(map)?boss?StationBoss:StationMix[Math.Max(0,Math.Min(3,wave-1))]:boss?IronBoss:IronMix[Math.Max(0,Math.Min(3,wave-1))];
        public static int Cap(string kind,int wave,bool boss)
        {switch(kind){case "linesman":case "undertow":return 2;case "conductor":return wave>=3||boss?2:1;case "pileup":case "hulk":return 1;case "rigger":return boss||wave>=3?2:wave==2?1:0;case "ladleman":return boss?0:wave>=4?2:wave==3?1:0;default:return 40;}}
        public static void Bind(ExpeditionAreaLayout area,NativePveContent data)
        {
            if(!Active(area.map))return;
            var kinds=Station(area.map)?NativeStationKinds.Kinds:NativeIronworksKinds.Kinds;
            data.adds=(data.adds??Array.Empty<PveAddStats>()).Concat(kinds.Where(k=>!(data.adds??Array.Empty<PveAddStats>()).Any(a=>a.id==k.Id)).Select(k=>k.Proposed)).ToArray();
            foreach(var s in area.spawns)s.entry.allowed=s.allow.ToArray();
        }
        public static bool PoolContains(System.Numerics.Vector2 rel,float yaw,float pourAt,float time)
        {
            if(pourAt<0||time<pourAt+1||time>=pourAt+5.2f||rel.Length()<NativeIronworksKinds.PourInner||rel.Length()>NativeIronworksKinds.PourRange)return false;
            float angle=(float)(Math.Atan2(rel.X,rel.Y)*180/Math.PI)-yaw;while(angle>180)angle-=360;while(angle< -180)angle+=360;
            float laid=Math.Min(1,(time-pourAt-1)/NativeIronworksKinds.PourTime);return angle>=-30&&angle<=-30+60*laid;
        }
        public static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<=10000000;
        public static float Wire(float t)=>float.IsNaN(t)||float.IsInfinity(t)?-100:t;
        public static float View(float t)=>t<0?float.NaN:t;
    }
    // Explicit finite wire representation: NaN is a presentation sentinel, never network data.
    [Serializable] public sealed class NativeZoneEnemyFrame
    {
        public string attack;
        public int[] arcTargets=Array.Empty<int>(),childIds=Array.Empty<int>();
        public int grabTarget=-1,pullTarget=-1,hang=-1,climbTo=-1;
        public bool grounded,lanternBroken,submerged;
        public float arcAt=-100,lastCallAt=-100,aboardUntil=-100,surfaceAt=-100,grabAt=-100,releaseAt=-100,splitAt=-100;
        public float pullAt=-100,shedAt=-100,pourAt=-100,pourYaw,spilledAt=-100,lockAt=-100,lockX,lockZ,climbAt=-100,chainCutAt=-100;
        public bool Valid()
        {
            if(attack!=null&&attack!="arc"&&attack!="whistle"&&attack!="grab"&&attack!="shove")return false;
            if(arcTargets==null||arcTargets.Length>3||arcTargets.Any(i=>i<1)||arcTargets.Distinct().Count()!=arcTargets.Length||childIds==null||childIds.Length>3||childIds.Any(i=>i<10000)||childIds.Distinct().Count()!=childIds.Length)return false;
            if(grabTarget< -1||grabTarget>1000000||pullTarget< -1||pullTarget>1000000||hang< -1||hang>=256||climbTo< -1||climbTo>=256)return false;
            return new[]{arcAt,lastCallAt,aboardUntil,surfaceAt,grabAt,releaseAt,splitAt,pullAt,shedAt,pourAt,pourYaw,spilledAt,lockAt,lockX,lockZ,climbAt,chainCutAt}.All(NativeZoneCombat.Finite)&& (grabAt<0||releaseAt>=grabAt);
        }
        public NativeZoneEnemyFrame Copy(){var f=(NativeZoneEnemyFrame)MemberwiseClone();f.arcTargets=(int[])arcTargets.Clone();f.childIds=(int[])childIds.Clone();return f;}
        public StationView Station=>new StationView{attack=attack,arcTargets=arcTargets,arcAt=NativeZoneCombat.View(arcAt),grounded=grounded,lanternBroken=lanternBroken,lastCallAt=NativeZoneCombat.View(lastCallAt),aboardUntil=NativeZoneCombat.View(aboardUntil),submerged=submerged,surfaceAt=NativeZoneCombat.View(surfaceAt),grabTarget=grabTarget,grabAt=NativeZoneCombat.View(grabAt),releaseAt=NativeZoneCombat.View(releaseAt),splitAt=NativeZoneCombat.View(splitAt),childIds=childIds};
        public IronworksView Ironworks=>new IronworksView{pullTarget=pullTarget,pullAt=NativeZoneCombat.View(pullAt),shedAt=NativeZoneCombat.View(shedAt),pourAt=NativeZoneCombat.View(pourAt),pourYaw=pourYaw,spilledAt=NativeZoneCombat.View(spilledAt),hang=hang,lockAt=NativeZoneCombat.View(lockAt),lockX=lockX,lockZ=lockZ,climbAt=NativeZoneCombat.View(climbAt),climbTo=climbTo,chainCutAt=NativeZoneCombat.View(chainCutAt)};
    }
}
