using System;
using System.Linq;
using ChipArena.UnityConversion;

internal static class ExpeditionCohortChecks
{
    private sealed class Placement:IExpeditionCohortPlacement
    {
        public bool Valid=true,ThrowApply,ThrowRollback,ThrowValidate;public int Applies,Rollbacks,ActorsAtDestination;
        public Action During;
        public bool Validate(){if(ThrowValidate)throw new Exception("injected validation failure");return Valid;}
        public void Apply(){Applies++;ActorsAtDestination=1;During?.Invoke();if(ThrowApply)throw new InvalidOperationException("injected halfway through placement");}
        public void Rollback(){Rollbacks++;if(ThrowRollback)throw new InvalidOperationException("injected rollback failure");ActorsAtDestination=0;}
    }
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){if(!ok)throw new Exception("Cohorts: "+why);checks++;}
        void Reject(Action action,string why){bool thrown=false;try{action();}catch{thrown=true;}Check(thrown,why);}
        ExpeditionCohorts Create()
        {
            var c=new ExpeditionCohorts("run",1,new string('a',64),"hub","ward");
            foreach(var a in new[]{"host","guest1","guest2","guest3"}){Check(c.Add(a,"hub"),"add member");Check(c.Acknowledge(a,c.Member(a).Ticket),"initial area ack");}
            return c;
        }
        var c=Create();var p=new Placement();var captured=new[]{"guest1","guest2"};int prepares=0;
        var old=c.Member("guest1").Ticket;
        Check(c.Begin("first","hub","ward",captured,30,(s,d,a)=>{prepares++;a[0]="host";return p;}),"prepare subset");captured[0]="host";
        Check(c.View("first").Members.SequenceEqual(new[]{"guest1","guest2"}),"captured cohort resists caller and factory mutation");
        var view=c.View("first");view.Members[0]="host";Check(c.View("first").Members[0]=="guest1","view detached");
        Check(c.Member("guest1").Area=="hub"&&c.Member("guest1").Pending=="first","membership uncommitted while loading");
        Check(c.Accepts("host",c.Member("host").Ticket)&&c.Accepts("guest3",c.Member("guest3").Ticket),"noncohort source members keep control");
        Check(!c.Accepts("guest1",old)&&!c.Acknowledge("guest1",old),"old commands and acks refused during transfer");
        Check(!c.Acknowledge("host",c.Member("guest1").Ticket),"authenticated sender cannot ack another member");
        Check(c.Begin("first","hub","ward",new[]{"guest2","guest1"},30,(s,d,a)=>{prepares++;return new Placement();})&&prepares==1,"duplicate begin reserves once");
        Check(!c.Begin("first","hub","ward",new[]{"host"},30,(s,d,a)=>p),"same id cannot change cohort");
        Check(!c.Begin("overlap","hub","ward",new[]{"guest1"},30,(s,d,a)=>p),"member cannot enter two transfers");
        Check(c.Acknowledge("guest1",c.Member("guest1").Ticket)&&!c.Commit("first"),"partial readiness does not commit");
        var stale=c.Member("guest2").Ticket;
        Check(c.Disconnect("guest2")&&c.View("first").Required==1,"disconnect releases required ack");
        Check(c.Commit("first")&&c.Commit("first")&&p.Applies==1,"whole immutable cohort commits once");
        Check(c.Member("guest2").Area=="ward"&&!c.Member("guest2").Connected,"disconnected member retains committed destination");
        var fresh=c.Reconnect("guest2");Check(fresh!=null&&!fresh.Matches(stale),"reconnect gets fresh destination ticket");
        Check(!c.Acknowledge("guest2",stale)&&!c.Accepts("guest2",fresh)&&c.Acknowledge("guest2",fresh)&&c.Accepts("guest2",fresh),"reconnect control waits for correct ack");
        var bad=fresh.Copy();bad.epoch++;Check(!c.Acknowledge("guest2",bad),"stale epoch refused");
        bad=fresh.Copy();bad.area="hub";Check(!c.Accepts("guest2",bad),"claimed area cannot override membership");
        bad=fresh.Copy();bad.cohort="forged";Check(!c.Accepts("guest2",bad),"cohort identity checked");
        bad=fresh.Copy();bad.layout=new string('b',64);Check(!c.Acknowledge("guest2",bad),"layout checked");
        bad=fresh.Copy();bad.run="another-run";Check(!c.Acknowledge("guest2",bad),"run identity checked");
        var newcomer=new Placement();Check(c.Begin("newcomer","hub","ward",new[]{"guest3"},30,(s,d,a)=>newcomer),"new cohort joins same Ward");
        Check(c.View("newcomer").Required==1&&c.Accepts("guest1",c.Member("guest1").Ticket)&&c.Accepts("guest2",fresh),"running destination occupants not in new barrier");
        Check(c.Cancel("newcomer")&&newcomer.Rollbacks==1&&!c.Member("guest3").Ready,"cancel releases reservation and requires source ack");
        Check(c.Member("guest3").Area=="hub"&&c.Acknowledge("guest3",c.Member("guest3").Ticket),"cancel recoverable at source");

        c=Create();p=new Placement{Valid=false};
        Check(!c.Begin("invalid","hub","ward",new[]{"guest1"},30,(s,d,a)=>p)&&p.Rollbacks==1&&c.Member("guest1").Ready,"invalid slots refused before holding source");
        Check(!c.Begin("exception","hub","ward",new[]{"guest1"},30,(s,d,a)=>throw new Exception("load failed"))&&!c.Faulted,"prepare exception leaves source usable");
        p=new Placement{ThrowValidate=true};Check(!c.Begin("validation-exception","hub","ward",new[]{"guest1"},30,(s,d,a)=>p)&&p.Rollbacks==1&&!c.Faulted,"prepare validation exception releases reservation");
        p=new Placement();Check(c.Begin("obstructed","hub","ward",new[]{"guest1"},30,(s,d,a)=>p),"reserve safe slot");
        c.Acknowledge("guest1",c.Member("guest1").Ticket);p.Valid=false;
        Check(!c.Commit("obstructed")&&p.Applies==0&&p.Rollbacks==1,"recheck destination before commit");
        c.Acknowledge("guest1",c.Member("guest1").Ticket);p=new Placement{ThrowApply=true};
        c.Begin("partial","hub","ward",new[]{"guest1"},30,(s,d,a)=>p);stale=c.Member("guest1").Ticket;c.Acknowledge("guest1",stale);
        Check(!c.Commit("partial")&&p.Applies==1&&p.Rollbacks==1&&p.ActorsAtDestination==0&&!c.Faulted,"partial native apply rolls back exactly once");
        Check(!c.Commit("partial")&&p.Applies==1&&!c.Acknowledge("guest1",stale),"cancelled operation cannot replay");
        c.Acknowledge("guest1",c.Member("guest1").Ticket);p=new Placement();c.Begin("timeout","hub","ward",new[]{"guest1"},3,(s,d,a)=>p);
        c.Advance(3);Check(c.View("timeout").Status==ExpeditionCohortStatus.Cancelled&&p.Rollbacks==1,"inclusive timeout cancels");
        c.Advance(4);Check(p.Rollbacks==1,"timeout cannot rollback twice");Reject(()=>c.Advance(2),"clock cannot rewind");
        c.Acknowledge("guest1",c.Member("guest1").Ticket);p=new Placement();c.Begin("commit-validation","hub","ward",new[]{"guest1"},30,(s,d,a)=>p);c.Acknowledge("guest1",c.Member("guest1").Ticket);p.ThrowValidate=true;
        Check(!c.Commit("commit-validation")&&p.Rollbacks==1&&!c.Faulted,"commit validation exception returns to source");
        c.Acknowledge("guest1",c.Member("guest1").Ticket);p=new Placement();c.Begin("all-offline","hub","ward",new[]{"guest1"},30,(s,d,a)=>p);c.Disconnect("guest1");
        Check(!c.Commit("all-offline")&&c.View("all-offline").Status==ExpeditionCohortStatus.Cancelled,"all disconnected returns cohort to source");
        fresh=c.Reconnect("guest1");Check(fresh.area=="hub"&&c.Acknowledge("guest1",fresh),"offline cancellation reconnects to source");
        p=new Placement();p.During=()=>Reject(()=>c.Cancel("reentrant"),"placement cannot reenter coordinator");
        c.Begin("reentrant","hub","ward",new[]{"guest1"},30,(s,d,a)=>p);c.Acknowledge("guest1",c.Member("guest1").Ticket);Check(c.Commit("reentrant"),"guarded commit succeeds");
        p=new Placement{ThrowApply=true,ThrowRollback=true};c.Begin("fault","hub","ward",new[]{"guest2"},30,(s,d,a)=>p);c.Acknowledge("guest2",c.Member("guest2").Ticket);
        Reject(()=>c.Commit("fault"),"unrecoverable native rollback propagates");Check(c.Faulted&&!c.Accepts("host",c.Member("host").Ticket),"uncertain state fails closed");
        Reject(()=>c.Commit("fault"),"fault cannot replay placement");
        c=Create();p=new Placement{Valid=false,ThrowRollback=true};Reject(()=>c.Begin("failed-reservation","hub","ward",new[]{"guest1"},30,(s,d,a)=>p),"failed reservation rollback propagates");
        Check(c.Faulted&&p.Rollbacks==1,"failed rollback never retries uncertain side effects");
        Console.WriteLine("PASS Expedition cohort foundation: "+checks+" checks (immutable subsets, isolated barriers, rollback, reconnect and stale tickets; no live native adapter)");
    }
}
