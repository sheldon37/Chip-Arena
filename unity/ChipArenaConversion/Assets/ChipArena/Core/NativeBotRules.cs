using System;
namespace ChipArena.UnityConversion
{
    public sealed class NativeBotBrain
    {
        public double ThinkAt;public V3 Aim;public bool Reacted;
        /// <summary>The held guard: the blow it last decided on, and how long its guard stays up for it.</summary>
        public int GuardSwing=-1;public double GuardUntil;
        public V3 TacticalGoal;
        public double PlanAt;
        public bool SeekingCover, HasGoal;
        public V3 Observe(double time,string difficulty,V3 target,Func<double> random,V3 right=default,float distance=10)
        {
            int level=NativeBotRules.Level(difficulty);
            Reacted=time>=ThinkAt;
            if(!Reacted)return Aim;
            ThinkAt=time+NativeBotRules.Reaction(level);
            Aim=target;
            if(level==3)return Aim;
            if(right.Length<.001f)right=new V3(1,0,0);
            float error=(float)NativeBotRules.Error(level)*MathUtil.Clamp(distance/10f,.75f,2.5f);
            Aim+=right*((float)(2*random()-1)*error)+new V3(0,(float)(2*random()-1)*error*.4f,0);
            return Aim;
        }
    }
    public static class NativeBotRules
    {
        public static int Level(string difficulty)=>difficulty=="godlike"?3:difficulty=="hard"?2:difficulty=="normal"?1:0;
        public static double Reaction(int level)=>level==3?0:level==2?.12:level==1?.26:.50;
        public static double Error(int level)=>level==3?0:level==2?.35:level==1?.95:2.25;
        public static double Chance(int level)=>level==3?1:level==2?.85:level==1?.65:.4;
        // Stable for each incoming attack, including a shot that remains in range on later ticks.
        public static bool Parry(int level,int defender,int attacker,int attack,bool projectile)
        {
            if(level==3)return true;
            uint seed=unchecked((uint)attack*2654435761u^(uint)defender*2246822519u^(uint)attacker*3266489917u^(projectile?0x85ebca6bu:0u));
            seed^=seed>>16;seed=unchecked(seed*0x7feb352du);seed^=seed>>15;
            return seed/(double)4294967296 < (level==2?.68:level==1?.28:.05);
        }
        public static string BossDifficulty(int humans)=>humans<=1?"normal":"hard";
        public static float Desired(ContentWeapon weapon)=>weapon.@class=="melee"?(float)weapon.range*.65f:weapon.id=="rifle"?32:weapon.@class=="magic"?9:10;
        public static bool Ads(string spec,float distance)=>spec=="rifle"&&distance>=8&&distance<=140;
        public static (float min,float max,float lead,bool point,bool ballistic) Ability(string id)
        {
            switch(id)
            {
                case "fan-hammer":return(0,16,0,false,false);case "overdrive":return(0,40,0,false,false);
                case "frag-grenade":return(6,22,.6f,true,false);case "rocket":return(4,35,0,false,true);case "orbital-mark":return(10,90,.3f,false,true);
                case "flame-lance":return(4,40,0,false,true);case "meteor-strike":return(6,40,.6f,true,false);case "tidal-wave":return(1,12,0,false,false);
                case "geyser-wall":return(6,26,.5f,true,false);case "ball-lightning":return(5,22,0,false,false);case "shadow-shackles":return(4,30,0,false,true);case "singularity":return(6,32,.3f,true,false);
                case "blink-strike":return(1,18,0,false,false);default:return(0,0,0,false,false);
            }
        }
    }
}
