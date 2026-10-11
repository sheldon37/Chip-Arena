using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
namespace ChipArena.UnityConversion
{
    // These are presentation notifications; simulation damage is never dropped.
    public sealed class NativeEventBuffer:List<ArenaEvent>
    {
        public const int CapacityLimit=512,PerTickEffects=32;
        private float tickTime=-1;private int effects;
        private static bool Transient(EventKind kind)=>kind==EventKind.Hit||kind==EventKind.Headshot||kind==EventKind.Impact||kind==EventKind.Shot||kind==EventKind.BurnTick||kind==EventKind.PoisonTick||kind==EventKind.Arc||kind==EventKind.Heal;
        public new void Add(ArenaEvent value)
        {
            if(value.Time!=tickTime){tickTime=value.Time;effects=0;}
            if(Transient(value.Kind)&&++effects>PerTickEffects)return;
            if(Count==CapacityLimit){int at=FindIndex(e=>Transient(e.Kind));RemoveAt(at<0?0:at);}
            base.Add(value);
        }
        public new void AddRange(IEnumerable<ArenaEvent> values){foreach(var value in values)Add(value);}
        public new void Clear(){base.Clear();tickTime=-1;effects=0;}
    }
    public sealed class NativeFrameOrder
    {
        public int Epoch{get;private set;}= -1;private int generation=-1;private double time=-1;
        public void Reset(){Epoch=-1;generation=-1;time=-1;}
        public bool Control(int epoch){if(epoch<0||epoch<Epoch)return false;if(epoch>Epoch){Epoch=epoch;generation=-1;time=-1;}return true;}
        public bool Frame(int epoch,int nextGeneration,double nextTime)
        {
            if(epoch<0||epoch<Epoch||nextGeneration<0||double.IsNaN(nextTime)||double.IsInfinity(nextTime)||nextTime<0)return false;
            if(epoch==Epoch&&(nextGeneration<generation||nextGeneration==generation&&nextTime<=time))return false;
            if(!Control(epoch))return false;generation=nextGeneration;time=nextTime;return true;
        }
    }
    public sealed class NativeMessageBudget
    {
        private double since;private int count;
        public bool Allow(double now){if(now-since>=1||now<since){since=now;count=0;}return ++count<=8;}
    }
}
