using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    // Required membership is captured once. A ready report cannot create membership or reopen a released gate.
    public sealed class NativeStageBarrier
    {
        private readonly HashSet<int> required=new HashSet<int>(),ready=new HashSet<int>();
        public int Epoch{get;private set;}public int Serial{get;private set;}public string Map{get;private set;}
        public bool Waiting{get;private set;}public float Deadline{get;private set;}
        public int Required=>required.Count;public int Ready{get{int n=0;foreach(int id in required)if(ready.Contains(id))n++;return n;}}
        public void Begin(int epoch,string map,int serial,IEnumerable<int> seats,float now)
        {Epoch=epoch;Map=map;Serial=serial;required.Clear();ready.Clear();foreach(int seat in seats)required.Add(seat);Deadline=now+12;Waiting=required.Count>0;}
        public bool Report(int seat,int epoch,string map,int serial)
        {if(epoch!=Epoch||map!=Map||serial!=Serial||!required.Contains(seat))return false;ready.Add(seat);return true;}
        public void Step(float now,Func<int,bool> connected)
        {required.RemoveWhere(id=>!connected(id));if(Ready==Required||now>=Deadline)Waiting=false;}
        public float Remaining(float now)=>Math.Max(0,Deadline-now);
    }
}
