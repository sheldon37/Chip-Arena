using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    public static class NativeBombChipRules
    {
        public const int MaxMineCharges=4,MaxFireFields=32;
        public static float PopFuse(int stacks)=>.9f-.25f*Math.Max(0,Math.Min(2,stacks));
        public static float PopSpeed(int stacks)=>(float)Math.Pow(1.15,Math.Max(0,Math.Min(2,stacks)));
        public static float MineLift(int stacks)=>(float)Math.Sqrt(2*23*8*Math.Pow(1.25,Math.Max(0,Math.Min(2,stacks))));
        public static float JumpCooldown(int stacks)=>8*(float)Math.Pow(.7,Math.Max(0,Math.Min(2,stacks)));
        public static float JumpDistance(int stacks)=>(float)Math.Pow(1.2,Math.Max(0,Math.Min(2,stacks)));
    }
    public sealed class NativeBombSnapshot:IBombSource
    {
        public Snapshot Snapshot;
        public NativeBombKit Of(int id){if(Snapshot?.Actors!=null)foreach(var a in Snapshot.Actors)if(a.Id==id)return a.Bomb;return null;}
    }
    // Private authority history, per owner AND victim. The wire exposes only the current chain and scorch windows.
    public sealed class NativeBombChain
    {
        public float Last=-100,LastDamage=-100,ScorchUntil=-100,Spent;public float Window=1.2f;public bool Daisy;public int Link;public V3 From;
        public void Prepare(float now){if(now>=ScorchUntil&&now-Math.Max(Last,LastDamage)>Window){Link=0;Spent=0;Last=LastDamage=-100;}}
        public bool Scorched(float now)=>now<ScorchUntil;
        public int Next(float now)=>Last>-50&&now-Last<=Window?Math.Min(3,Link+1):0;
        public float Scale(float now)=>Scorched(now)?.5f:Next(now)==0?1:Next(now)==1?(Daisy?.9f:.8f):Next(now)==2?(Daisy?.75f:.6f):(Daisy?.6f:.45f);
        // This includes follow-up hits during Scorched, not just the four launching blasts.
        public float Budget(float maxHp)=>Math.Max(0,Math.Min(60,maxHp*.6f)-Spent);
        public int Hit(float now,float actual,V3 pos)
        {
            if(actual<=0)return -1;Spent+=actual;LastDamage=now;if(Scorched(now))return -1;
            Link=Next(now);Last=now;From=pos;if(Link>=3)ScorchUntil=now+NativeBombs.ScorchSeconds;return Link;
        }
    }
}
