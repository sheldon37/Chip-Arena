using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ExpeditionJoinView
    {
        public string doorId,area;public int joinInside;public double joinStartedAt=-1,duration=3;
        public ExpeditionJoinView Copy()=>(ExpeditionJoinView)MemberwiseClone();
    }
    public sealed class ExpeditionJoinMember
    {
        public string Account,Area;public V3 Feet;
        public bool Alive,Connected,Extracted,Loading,Choosing,Carrying;
    }
    // Host measured feet only. Geometry and timers are independent of presentation.
    // The caller enables this only alongside a real subset-transfer implementation.
    public sealed class ExpeditionJoinRules
    {
        private sealed class Ring
        {
            public ExpeditionJoinCylinder Shape;public ExpeditionJoinView View;
        }
        private readonly string hub;private readonly Ring[] rings;
        private readonly Dictionary<string,string> selected=new Dictionary<string,string>(StringComparer.Ordinal);
        private double last=-1;private bool stepping;
        public const double Duration=3;
        public const float CapsuleCentreHeight=.85f;
        public ExpeditionJoinRules(ExpeditionLayout layout)
        {
            if(layout==null)throw new ArgumentNullException(nameof(layout));hub=layout.world;
            var world=layout.Area(hub)??throw new ArgumentException("Hub layout required.");
            rings=world.doors.Where(d=>d.join!=null).OrderBy(d=>d.anchor,StringComparer.Ordinal).Select(d=>
            {
                var a=world.anchors.Single(v=>v.id==d.anchor&&v.kind=="door");var j=d.join;
                if(!layout.instances.Contains(a.building)||!new[]{j.x,j.y,j.z,j.r,j.h}.All(v=>ExpeditionValidation.Number(v))||j.r<=0||j.h<=0)throw new ArgumentException("Validated join geometry required.");
                return new Ring{Shape=new ExpeditionJoinCylinder{x=j.x,y=j.y,z=j.z,r=j.r,h=j.h},View=new ExpeditionJoinView{doorId=d.anchor,area=a.building,duration=Duration}};
            }).ToArray();
        }
        public ExpeditionJoinView[] Views=>rings.Select(r=>r.View.Copy()).ToArray();
        public bool AtJoin(string account)=>account!=null&&selected.ContainsKey(account);
        public string DoorFor(string account)=>account!=null&&selected.TryGetValue(account,out var door)?door:null;
        public void Reset(){if(stepping)throw new InvalidOperationException("Join step is active.");selected.Clear();last=-1;foreach(var r in rings){r.View.joinInside=0;r.View.joinStartedAt=-1;}}
        public void Step(double elapsed,IEnumerable<ExpeditionJoinMember> members,Func<string,bool> admitted,
            Func<string,string,string[],bool> transfer)
        {
            if(stepping)throw new InvalidOperationException("Reentrant join step.");
            if(!ExpeditionValidation.Number(elapsed)||elapsed<0||elapsed<last||members==null||admitted==null||transfer==null)throw new ArgumentException("Monotonic host clock, membership and transfer adapters required.");
            if(elapsed==last)return;
            // Snapshot once: an expiry callback cannot alter another ring's captured
            // membership. One account resolves to the first ordinal door, every tick.
            var roster=members.ToArray();
            if(roster.Length>NativeExpeditionParty.Capacity||roster.Any(m=>m==null||!ExpeditionValidation.Id(m.Account))||roster.Select(m=>m.Account).Distinct(StringComparer.Ordinal).Count()!=roster.Length)throw new ArgumentException("Invalid join roster.");
            stepping=true;
            try
            {
                last=elapsed;selected.Clear();var allowed=rings.Where(r=>admitted(r.View.area)).ToArray();
                foreach(var m in roster.Where(m=>m.Area==hub&&m.Alive&&m.Connected&&!m.Extracted&&!m.Loading&&!m.Choosing&&!m.Carrying))
                {
                    if(!ExpeditionValidation.Number(m.Feet.x)||!ExpeditionValidation.Number(m.Feet.y)||!ExpeditionValidation.Number(m.Feet.z))continue;
                    var centre=m.Feet+new V3(0,CapsuleCentreHeight,0);var ring=allowed.FirstOrDefault(r=>r.Shape.Contains(centre));
                    if(ring!=null)selected.Add(m.Account,ring.View.doorId);
                }
                var expired=new List<(Ring ring,string[] cohort)>();
                foreach(var r in rings)
                {
                    var cohort=selected.Where(p=>p.Value==r.View.doorId).Select(p=>p.Key).OrderBy(a=>a,StringComparer.Ordinal).ToArray();
                    r.View.joinInside=cohort.Length;
                    if(cohort.Length==0){r.View.joinStartedAt=-1;continue;}
                    if(r.View.joinStartedAt<0)r.View.joinStartedAt=elapsed;
                    if(elapsed>=r.View.joinStartedAt+Duration)expired.Add((r,cohort));
                }
                foreach(var e in expired)
                {
                    // A refusal (blocked slots/closed encounter) gets a fresh full
                    // countdown, rather than hammering the adapter every fixed tick.
                    e.ring.View.joinStartedAt=-1;
                    if(transfer(e.ring.View.doorId,e.ring.View.area,(string[])e.cohort.Clone()))
                    {foreach(var a in e.cohort)selected.Remove(a);e.ring.View.joinInside=0;}
                }
            }
            finally{stepping=false;}
        }
    }
}
