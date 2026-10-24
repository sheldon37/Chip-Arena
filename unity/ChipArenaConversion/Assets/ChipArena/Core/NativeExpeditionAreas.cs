using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    // Area facades share one run ledger. No callback temporarily changes the run's area.
    public sealed partial class NativeExpedition
    {
        private sealed class AreaState
        {
            public string Id;public ExpeditionPhase Phase;public bool Loading;
            public double ExitAt=-1;public string[] ExitVotes=Array.Empty<string>();
        }
        private Dictionary<string,AreaState> areaStates;
        private AreaState scope;
        public bool SplitAreas=>areaStates!=null;
        public Func<string,string,bool> ReprintPlacement;
        private string Area {get=>scope?.Id??s.area;set{if(scope!=null)throw new InvalidOperationException("Use a cohort to change membership.");s.area=value;}}
        private ExpeditionPhase Phase {get=>s.phase==ExpeditionPhase.Ended?s.phase:scope?.Phase??s.phase;set{if(scope==null)s.phase=value;else{scope.Phase=value;if(scope.Id==s.world)s.phase=value;}}}
        private bool Loading {get=>scope?.Loading??s.loading;set{if(scope==null)s.loading=value;else scope.Loading=value;}}
        private double ExitAt {get=>scope?.ExitAt??s.exitAt;set{if(scope==null)s.exitAt=value;else scope.ExitAt=value;}}
        private string[] ExitVotes {get=>scope?.ExitVotes??s.exitVotes;set{if(scope==null)s.exitVotes=value;else scope.ExitVotes=value;}}
        private bool InArea(ExpeditionMember p)=>!SplitAreas||p.area==Area;
        private bool AllHome=>!SplitAreas||s.members.Where(p=>!p.extracted).All(p=>p.area==s.world&&!p.transferring);
        public ExpeditionSnapshot PartySnapshot()=>s.Copy();
        private ExpeditionSnapshot ScopedSnapshot()
        {
            var copy=s.Copy();if(!SplitAreas)return copy;
            copy.area=Area;copy.phase=Phase;copy.loading=Loading;copy.exitAt=ExitAt;copy.exitVotes=(string[])ExitVotes.Clone();
            copy.members=copy.members.Where(InArea).ToArray();
            copy.anchors=copy.anchors.Where(a=>a.area==Area).ToArray();
            copy.chests=copy.chests.Where(c=>copy.anchors.Any(a=>a.id==c.id)).ToArray();
            return copy;
        }
        public void EnableAreas()
        {
            if(SplitAreas||s.elapsed!=0||s.area!=s.world||s.loading)throw new InvalidOperationException("Enable areas before launch.");
            areaStates=new Dictionary<string,AreaState>(StringComparer.Ordinal);
            areaStates.Add(s.world,new AreaState{Id=s.world,Phase=s.phase});
            foreach(var i in s.instances)areaStates.Add(i.id,new AreaState{Id=i.id,Phase=ExpeditionPhase.Instance});
            foreach(var p in s.members)p.area=s.world;
            scope=areaStates[s.world];Changed();
        }
        private NativeExpedition(NativeExpedition owner,AreaState next){s=owner.s;areaStates=owner.areaStates;scope=next;}
        public NativeExpedition ForArea(string area)
        {
            if(!SplitAreas||!areaStates.TryGetValue(area,out var next))throw new ArgumentException("Unknown run area.");
            return new NativeExpedition(this,next);
        }
        public bool HoldMembers(string[] accounts,bool held)
        {
            if(!SplitAreas||accounts==null||accounts.Any(a=>Member(a)==null))return false;
            bool changed=false;foreach(var a in accounts){var p=Member(a);changed|=p.transferring!=held;p.transferring=held;if(held){p.safe=p.choosing=false;p.choosingUntil=0;}}
            if(changed)Changed();return true;
        }
        public bool MoveMembers(string source,string destination,string[] accounts)
        {
            if(!SplitAreas||!areaStates.ContainsKey(destination)||accounts==null||accounts.Length==0||accounts.Distinct().Count()!=accounts.Length||accounts.Any(a=>Member(a)==null||Member(a).area!=source||Member(a).extracted))return false;
            foreach(var a in accounts){var p=Member(a);p.area=destination;p.safe=false;p.carriedBy=null;p.choosing=false;p.choosingUntil=0;p.transferring=false;}
            var encounter=s.instances.FirstOrDefault(i=>i.id==destination);if(encounter!=null){if(!encounter.entered)encounter.scalingParty=accounts.Length;encounter.entered=true;}
            foreach(var state in areaStates.Values){state.ExitVotes=state.ExitVotes.Where(a=>Member(a)?.area==state.Id).ToArray();if(state.ExitVotes.Length==0)state.ExitAt=-1;}
            Changed();return true;
        }
        public string[] DueReturns()=>SplitAreas&&Area!=s.world&&ExitAt>=0&&Elapsed>=ExitAt?Living():Array.Empty<string>();
    }
}
