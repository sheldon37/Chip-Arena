using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativePveContent
    {public NativeBossTrack bossMusic;public PveBoss boss;public PvePoint[] hideSpots;public string sourceCommit;public PveMachine[] machines;public PveEntry[] entries;public PvePoint[] pads;public ContentBlock[] machineBlocks;public PveAddStats[] adds;public PveWaveData[] waves;public PveMix[] bossMix;public NumberValue[] dropOdds;public PveWeights[] offerWeights;public ContentChip[] moneyChips;}
    [Serializable] public sealed class PvePoint {public float x,y,z;public V3 Position(float height=0)=>new V3(x,y+height,z);}
    [Serializable] public sealed class PveMachine {public int m;public string id,kind;public float x,y,z,yaw;public PvePoint front,dispense,chipDrop;}
    [Serializable] public sealed class PveBoss {public PvePoint spawn,rescue;public float yaw;}
    [Serializable] public sealed class PveEntry {public int e;public string id,kind,surface,block;public PvePoint from,@exit;public float yaw,height,clear;public int level,song=-1;public bool pit,hulk;public V3 center,size,normal;}
    [Serializable] public sealed class PveAddStats {public string id,attack;public float hp,damage,walk,run,reach,startRange,hit,cycle,lunge,impulse,reward,limbFrac,headFrac,stagger,smashRadius;}
    [Serializable] public sealed class PveWaveData {public int count,maxAlive;public float rate;public NumberValue[] mix;}
    [Serializable] public sealed class PveMix {public int phase;public NumberValue[] mix;}
    [Serializable] public sealed class PveWeights {public string id;public NumberValue[] weights;}
    [Serializable] public sealed class PveWaveEvent {public string kind;public int wave;public double at;public bool forced;}
    public sealed class NativeWaveSchedule
    {
        public string Stage="opening";public int Wave;public double At,Until=10;private double low=-1;private readonly bool skip;
        public NativeWaveSchedule(double time,bool skip){this.skip=skip;At=time;Until=time+10;}
        public PveWaveEvent Step(double time,int remaining,double? resolvedAt)
        {
            if(Stage=="opening"){Until=resolvedAt.HasValue?Math.Min(At+10,Math.Max(At+4,resolvedAt.Value+3)):At+10;if(time>=Until)return Next(skip?4:1,Until);}
            else if(Stage=="wave"){if(remaining<=2){if(low<0)low=time;}else low=-1;double quiet=low<0?double.PositiveInfinity:low+3,forced=At+150;if(time>=Math.Min(quiet,forced)){At=Math.Min(quiet,forced);Stage="intermission";low=-1;Until=At+30;return new PveWaveEvent{kind="wave-clear",wave=Wave,at=At,forced=forced<quiet};}}
            else if(Stage=="intermission"&&time>=Until)return Next(Wave+1,Until);return null;
        }
        private PveWaveEvent Next(int wave,double at){Wave=wave;At=at;low=-1;Stage=wave>3?"done":"wave";return new PveWaveEvent{kind=wave>3?"boss":"wave-start",wave=wave>3?0:wave,at=at};}
    }
    public static class NativePveRules
    {
        public static int BossHp(int members)=>(int)Math.Round(6000*(1+.5*(Math.Max(1,members)-1)));
        public static float BossDamage(int members)=>new[]{.2f,.22f,.24f,.26f}[Math.Max(0,Math.Min(3,members-1))];
        public static int Budget(PveWaveData w,int members)=>(int)Math.Floor(w.count*(1+.6*(Math.Max(1,members)-1))+.5);
        public static float Rate(PveWaveData w,int members)=>w.rate*(1+.35f*(Math.Max(1,members)-1));
        public static int Cap(PveWaveData w,int members,bool relay)=>Math.Min(relay?28:40,w.maxAlive+5*(Math.Max(1,members)-1));
        public static string Weighted(NumberValue[] table,Func<double> next){double roll=next()*table.Where(v=>v.value>0).Sum(v=>v.value);string last="";foreach(var v in table)if(v.value>0){last=v.name;roll-=v.value;if(roll<0)return last;}return last;}
        public static int ChipPrice(int bought)=>Math.Min(350,175+25*bought);
        public static int RevivePrice(int revived)=>Math.Min(400,150+50*revived);
        public static int Money(IReadOnlyDictionary<string,int> held,string reason,double amount)
        {
            if(amount<=0||!new[]{"kill","assist","recruit","possessed","head","wave-clear","boss","boss-threshold"}.Contains(reason))return (int)Math.Floor(amount+.5);
            int N(string id,int max)=>held.TryGetValue(id,out int n)?Math.Max(0,Math.Min(max,n)):0;
            double flat=(reason=="wave-clear"?25*N("payday-loop",3)+40*N("midas-core",1):reason=="head"?6*N("headhunter",3):0),bonus=.3*N("gold-rush",1)+.5*N("midas-core",1);
            if(new[]{"kill","assist","recruit","possessed"}.Contains(reason))bonus+=.15*N("bounty-scanner",4);if(reason=="boss"||reason=="boss-threshold")bonus+=.3*N("boss-contract",2);return (int)Math.Floor((amount+flat)*(1+bonus)+.5);
        }
    }
}
