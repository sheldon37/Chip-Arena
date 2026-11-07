using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public sealed class NativeEndFlow
    {
        private readonly HashSet<int> votes=new HashSet<int>();private int[] members=Array.Empty<int>();
        public double ReturnAt{get;private set;}=-1;public int Votes=>votes.Count;public int Required=>members.Length;
        public int[] Voters=>votes.OrderBy(i=>i).ToArray();public bool Unanimous=>Required>0&&Votes==Required;
        public void Reconcile(IEnumerable<int> ids){members=ids.Distinct().OrderBy(i=>i).ToArray();votes.RemoveWhere(i=>!members.Contains(i));}
        public bool Vote(int id){return members.Contains(id)&&votes.Add(id);}
        public void RequestReturn(double time){ReturnAt=time+3;}
        public void CancelReturn(){ReturnAt=-1;}
        public bool Due(double time)=>ReturnAt>=0&&time>=ReturnAt;
        public void Reset(){votes.Clear();members=Array.Empty<int>();ReturnAt=-1;}
    }
}
