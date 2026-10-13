using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Unity-serializable vectors; the presentation's System.Numerics preview record stays presentation-owned.
    [Serializable] public sealed class NativeCyberRangedState
    {
        public bool Ranged;public float ModeAt=-100,ModeReady=-100;
        public float[] At=New(),Release=New(),Until=New(),Ready=New();
        public int BoltHand;public V3 LanceFrom,LanceTo,FieldFrom,FieldPoint,SwapFrom,SwapTo,RootFrom,RootTo;
        public float FieldLand=-100,FieldUntil=-100,MineAt=-100;
        static float[] New()=>Enumerable.Repeat(-100f,CyberRangedSpec.Slots).ToArray();
        public NativeCyberRangedState ViewCopy(){var c=(NativeCyberRangedState)MemberwiseClone();c.At=(float[])At.Clone();c.Release=(float[])Release.Clone();c.Until=(float[])Until.Clone();c.Ready=(float[])Ready.Clone();return c;}
        public bool Valid()
        {
            bool F(float n)=>MathUtil.Finite(n)&&Math.Abs(n)<1e8;
            bool V(V3 v)=>NativeMapRules.Finite(v)&&Math.Abs(v.x)<10000&&Math.Abs(v.y)<10000&&Math.Abs(v.z)<10000;
            var arrays=new[]{At,Release,Until,Ready};if(arrays.Any(a=>a==null||a.Length!=CyberRangedSpec.Slots||a.Any(n=>!F(n))))return false;
            for(int i=0;i<CyberRangedSpec.Slots;i++)if(Release[i]<At[i]||Until[i]<Release[i]||Ready[i]<At[i])return false;
            return F(ModeAt)&&F(ModeReady)&&ModeReady>=ModeAt&&BoltHand>=0&&BoltHand<=1&&F(FieldLand)&&F(FieldUntil)&&FieldUntil>=FieldLand&&F(MineAt)&&
                new[]{LanceFrom,LanceTo,FieldFrom,FieldPoint,SwapFrom,SwapTo,RootFrom,RootTo}.All(V);
        }
    }
    public static class NativeCyberRangedRules
    {
        public static bool Switching(NativeCyberKit kit,float time)=>kit?.Ranged!=null&&time<kit.Ranged.ModeAt+CyberRangedSpec.SwitchSeconds;
        public static bool Switch(NativeCyberKit kit,float time,bool blocked)
        {
            var r=kit.Ranged??(kit.Ranged=new NativeCyberRangedState());
            if(blocked||time<kit.Until||time<kit.DashUntil||time<kit.LinkUntil||time<r.ModeReady)return false;
            r.Ranged=!r.Ranged;r.ModeAt=time;r.ModeReady=time+CyberRangedSpec.SwitchCooldown;
            kit.Kind="mode-switch";kit.At=kit.Release=time;kit.Until=time+CyberRangedSpec.SwitchSeconds;kit.Resolved=true;return true;
        }
        public static int Slot(InputFrame i)=>i.SirenAbility>=1&&i.SirenAbility<=4?i.SirenAbility:i.Attack||i.Shoot?0:-1;
        public static bool Begin(NativeCyberKit kit,int slot,float time,ref float mana,float rate,float costScale,float cooldownScale)
        {
            var r=kit.Ranged;if(r==null||!r.Ranged||slot<0||slot>=CyberRangedSpec.Slots||time<kit.Until||time<r.Ready[slot])return false;
            float cost=CyberRangedSpec.Costs[slot]*costScale;if(mana<cost)return false;
            rate=slot==0?Math.Max(.01f,rate):1;mana-=cost;
            r.At[slot]=time;r.Release[slot]=time+CyberRangedSpec.Windup[slot]/rate;r.Until[slot]=time+CyberRangedSpec.Seconds[slot]/rate;
            r.Ready[slot]=time+CyberRangedSpec.Cooldowns[slot]/rate*(slot==0||slot==4?1:cooldownScale);
            if(slot==0)r.BoltHand=1-r.BoltHand;
            kit.Kind=CyberRangedSpec.Ids[slot];kit.At=time;kit.Release=r.Release[slot];kit.Until=r.Until[slot];kit.Resolved=false;return true;
        }
        public static void Interrupt(NativeCyberKit kit,float time)
        {
            int slot=CyberRangedSpec.Slot(kit.Kind);if(slot<0||kit.Ranged==null||time>=kit.Until)return;
            if(time<kit.Ranged.Release[slot]){if(slot==CyberRangedSpec.Field)kit.Ranged.FieldLand=kit.Ranged.FieldUntil=-100;else if(slot==CyberRangedSpec.Swap)kit.Ranged.MineAt=-100;}
            kit.Ranged.Until[slot]=time;kit.Ranged.Release[slot]=Math.Min(time,kit.Ranged.Release[slot]);kit.Until=time;kit.Release=Math.Min(time,kit.Release);kit.Resolved=true;
        }
        public static float RootDamage(float hp,float maxHp)=>Math.Min(CyberRangedSpec.RootCap,CyberRangedSpec.RootDamage+Math.Max(0,maxHp-hp)*CyberRangedSpec.RootMissing);
        public static int BotSlot(float distance,float health,float enemyHealth,bool blocking,bool visible,Func<int,bool> available=null)
        {
            bool Ready(int slot)=>available==null||available(slot);
            if(distance<3&&health<.5f&&Ready(CyberRangedSpec.Swap))return CyberRangedSpec.Swap;
            if(!visible)return -1;
            if(distance>=10&&distance<=40&&!blocking&&enemyHealth<.6f&&Ready(CyberRangedSpec.Root))return CyberRangedSpec.Root;
            if(distance>=6&&distance<=20&&Ready(CyberRangedSpec.Field))return CyberRangedSpec.Field;
            return distance>=8&&distance<=40&&Ready(CyberRangedSpec.Lance)?CyberRangedSpec.Lance:-1;
        }
    }
}
