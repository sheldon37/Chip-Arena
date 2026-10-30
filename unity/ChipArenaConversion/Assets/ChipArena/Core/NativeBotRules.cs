using System;
namespace ChipArena.UnityConversion
{
    public sealed class NativeBotBrain
    {
        public double ThinkAt;public V3 Aim;public bool Reacted;
        public V3 Observe(double time,string difficulty,V3 target,Func<double> random)
        {int level=NativeBotRules.Level(difficulty);Reacted=time>=ThinkAt;if(Reacted){ThinkAt=time+NativeBotRules.Reaction(level);Aim=target+new V3((float)((random()-.5)*NativeBotRules.Error(level)),(float)(.15+(random()-.5)*.2),(float)((random()-.5)*.6));}return Aim;}
    }
    public static class NativeBotRules
    {
        public static int Level(string difficulty)=>difficulty=="hard"?2:difficulty=="normal"?1:0;
        public static double Reaction(int level)=>level==2?.1:level==1?.18:.32;
        public static double Error(int level)=>level==2?.4:level==1?.8:1.8;
        public static double Chance(int level)=>level==2?.85:level==1?.65:.4;
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
