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
        public int Epoch{get;private set;}= -1;private int generation=-1;private double time=-1;private long sequence;
        public void Reset(){Epoch=-1;generation=-1;time=-1;sequence=0;}
        public bool Control(int epoch){if(epoch<0||epoch<Epoch)return false;if(epoch>Epoch){Epoch=epoch;generation=-1;time=-1;sequence=0;}return true;}
        // Siren cinematics freeze simulation time while their presentation and chip/control state keeps changing.
        // A host capture sequence orders these frames without advancing gameplay timers; zero retains legacy ordering.
        public bool Frame(int epoch,int nextGeneration,double nextTime,long nextSequence=0)
        {
            if(epoch<0||epoch<Epoch||nextGeneration<0||nextSequence<0||double.IsNaN(nextTime)||double.IsInfinity(nextTime)||nextTime<0)return false;
            if(epoch==Epoch)
            {
                if(nextGeneration<generation)return false;
                if(nextGeneration==generation&&(nextTime<time||(nextSequence>0?nextSequence<=sequence:sequence>0||nextTime<=time)))return false;
            }
            if(!Control(epoch))return false;generation=nextGeneration;time=nextTime;sequence=nextSequence;return true;
        }
    }
    public sealed class NativeMessageBudget
    {
        private double since;private int count;
        public bool Allow(double now){if(now-since>=1||now<since){since=now;count=0;}return ++count<=8;}
    }
}
