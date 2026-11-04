using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    public sealed class NativeBombSnapshot:IBombSource
    {
        public Snapshot Snapshot;
        public NativeBombKit Of(int id){if(Snapshot?.Actors!=null)foreach(var a in Snapshot.Actors)if(a.Id==id)return a.Bomb;return null;}
    }
    // Private authority history, per owner AND victim. The wire exposes only the current chain and scorch windows.
    public sealed class NativeBombChain
    {
        public float Last=-100,ScorchUntil=-100,Spent;public int Link;public V3 From;
        public void Prepare(float now){if(now>=ScorchUntil&&now-Last>1.2f){Link=0;Spent=0;Last=-100;}}
        public bool Scorched(float now)=>now<ScorchUntil;
        public int Next(float now)=>Last>-50&&now-Last<=1.2f?Math.Min(3,Link+1):0;
        public float Scale(float now)=>Scorched(now)?.5f:Next(now)==0?1:Next(now)==1?.8f:Next(now)==2?.6f:.45f;
        // This includes follow-up hits during Scorched, not just the four launching blasts.
        public float Budget(float maxHp)=>Math.Max(0,Math.Min(60,maxHp*.6f)-Spent);
        public int Hit(float now,float actual,V3 pos)
        {
            if(actual<=0)return -1;Spent+=actual;if(Scorched(now))return -1;
            Link=Next(now);Last=now;From=pos;if(Link>=3)ScorchUntil=now+NativeBombs.ScorchSeconds;return Link;
        }
    }
}
