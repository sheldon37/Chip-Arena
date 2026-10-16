using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    // Inactive foundation. The single-area director must NOT use this until its
    // progression, transport and persistence adapters have passed the split gates.
    [Serializable] public sealed class ExpeditionCohortTicket
    {
        public string run,area,layout,cohort;public int epoch;public long serial;
        public ExpeditionCohortTicket Copy()=>(ExpeditionCohortTicket)MemberwiseClone();
        public bool Matches(ExpeditionCohortTicket other)=>other!=null&&run==other.run&&area==other.area&&layout==other.layout&&cohort==other.cohort&&epoch==other.epoch&&serial==other.serial;
    }
    public sealed class ExpeditionCohortMember
    {
        public string Account {get;internal set;}
        public string Area {get;internal set;}
        public string Pending {get;internal set;}
        public bool Connected {get;internal set;}
        public bool Ready {get;internal set;}
        internal ExpeditionCohortTicket ticket;
        public ExpeditionCohortTicket Ticket=>ticket.Copy();
        internal ExpeditionCohortMember Copy(){var copy=(ExpeditionCohortMember)MemberwiseClone();copy.ticket=ticket.Copy();return copy;}
    }
    public enum ExpeditionCohortStatus { Loading, Committed, Cancelled, Faulted }
    public sealed class ExpeditionCohortView
    {
        public string Id {get;internal set;}
        public string Source {get;internal set;}
        public string Destination {get;internal set;}
        public ExpeditionCohortStatus Status {get;internal set;}
        public double Deadline {get;internal set;}
        public int Required {get;internal set;}
        public int Ready {get;internal set;}
        public string[] Members {get;internal set;}
    }
    // Prepare only reserves validated, distinct safe destination slots. Apply must
    // be reversible by Rollback even when it throws halfway through. It must not
    // award loot or charge wallets; transfer itself is free. Rollback releases the
    // reservation and restores source actors. The native adapter is a separate gate.
    public interface IExpeditionCohortPlacement
    {
        bool Validate();
        void Apply();
        void Rollback();
    }
    public sealed class ExpeditionCohorts
    {
        private sealed class Cohort
        {
            public string Id,Source,Destination;public string[] Members;
            public double Deadline;public ExpeditionCohortStatus Status;
            public IExpeditionCohortPlacement Placement;
        }
        private readonly string run,layout,hub,instance;private readonly int epoch;
        private readonly Dictionary<string,ExpeditionCohortMember> members=new Dictionary<string,ExpeditionCohortMember>(StringComparer.Ordinal);
        private readonly Dictionary<string,Cohort> cohorts=new Dictionary<string,Cohort>(StringComparer.Ordinal);
        private long serial;private double now;private bool busy;
        public bool Faulted {get;private set;}
        public ExpeditionCohortView[] Active=>cohorts.Values.Where(c=>c.Status==ExpeditionCohortStatus.Loading).Select(c=>View(c.Id)).ToArray();
        public ExpeditionCohortMember[] Members=>members.Values.OrderBy(m=>m.Account,StringComparer.Ordinal).Select(m=>m.Copy()).ToArray();
        public ExpeditionCohorts(string run,int epoch,string layout,string hub,string instance)
        {
            if(!ExpeditionValidation.Id(run)||epoch<1||layout==null||layout.Length!=64||layout.Any(c=>!Uri.IsHexDigit(c))||!ExpeditionValidation.Id(hub)||!ExpeditionValidation.Id(instance)||hub==instance)throw new ArgumentException("Validated run, epoch, layout and two areas required.");
            this.run=run;this.epoch=epoch;this.layout=layout;this.hub=hub;this.instance=instance;
        }
        private bool Area(string value)=>value==hub||value==instance;
        private void Guard(){if(busy||Faulted)throw new InvalidOperationException("Cohort authority is busy or faulted.");}
        private ExpeditionCohortTicket Ticket(string area,string cohort)=>new ExpeditionCohortTicket{run=run,epoch=epoch,layout=layout,area=area,cohort=cohort,serial=checked(++serial)};
        public ExpeditionCohortMember Member(string account)=>account!=null&&members.TryGetValue(account,out var m)?m.Copy():null;
        public bool Add(string account,string area)
        {
            Guard();if(!ExpeditionValidation.Id(account)||!Area(area)||members.ContainsKey(account)||members.Count>=NativeExpeditionParty.Capacity)return false;
            members.Add(account,new ExpeditionCohortMember{Account=account,Area=area,Connected=true,ticket=Ticket(area,null)});return true;
        }
        public ExpeditionCohortView View(string id)
        {
            if(id==null||!cohorts.TryGetValue(id,out var c))return null;
            var required=c.Members.Select(a=>members[a]).Where(m=>m.Connected).ToArray();
            return new ExpeditionCohortView{Id=c.Id,Source=c.Source,Destination=c.Destination,Status=c.Status,Deadline=c.Deadline,
                Members=(string[])c.Members.Clone(),Required=required.Length,Ready=required.Count(m=>m.Ready)};
        }
        public bool Accepts(string authenticatedAccount,ExpeditionCohortTicket identity)
        {return !Faulted&&!busy&&authenticatedAccount!=null&&members.TryGetValue(authenticatedAccount,out var m)&&m.Connected&&m.Ready&&m.Pending==null&&m.ticket.Matches(identity);}
        public bool Acknowledge(string authenticatedAccount,ExpeditionCohortTicket identity)
        {
            Guard();if(authenticatedAccount==null||!members.TryGetValue(authenticatedAccount,out var m)||!m.Connected||!m.ticket.Matches(identity))return false;
            m.Ready=true;return true;
        }
        // Caller supplies exactly the eligible accounts captured at countdown expiry,
        // never a live enumerable over a changing ring. This method copies it again.
        public bool Begin(string id,string source,string destination,IEnumerable<string> captured,double timeout,
            Func<string,string,string[],IExpeditionCohortPlacement> prepare)
        {
            Guard();if(!ExpeditionValidation.Id(id)||!Area(source)||!Area(destination)||source==destination||captured==null||prepare==null||!ExpeditionValidation.Number(timeout)||timeout<=0||timeout>120)return false;
            var accounts=captured.Take(NativeExpeditionParty.Capacity+1).ToArray();
            if(accounts.Length==0||accounts.Length>NativeExpeditionParty.Capacity||accounts.Any(a=>a==null)||accounts.Distinct(StringComparer.Ordinal).Count()!=accounts.Length)return false;
            Array.Sort(accounts,StringComparer.Ordinal);
            if(cohorts.TryGetValue(id,out var prior))return prior.Source==source&&prior.Destination==destination&&prior.Members.SequenceEqual(accounts)&&prior.Status!=ExpeditionCohortStatus.Cancelled&&prior.Status!=ExpeditionCohortStatus.Faulted;
            if(cohorts.Count>=ExpeditionValidation.MaxRecords)return false;
            if(accounts.Any(a=>!members.TryGetValue(a,out var m)||!m.Connected||!m.Ready||m.Pending!=null||m.Area!=source))return false;
            busy=true;IExpeditionCohortPlacement placement=null;
            try
            {
                bool valid;
                try{placement=prepare(source,destination,(string[])accounts.Clone());valid=placement?.Validate()==true;}
                catch{UndoPrepared(placement);return false;}
                if(!valid){UndoPrepared(placement);return false;}
                var c=new Cohort{Id=id,Source=source,Destination=destination,Members=accounts,Deadline=now+timeout,Placement=placement};
                // Source membership remains authoritative until the batch commits.
                foreach(var a in accounts){var m=members[a];m.Pending=id;m.Ready=false;m.ticket=Ticket(destination,id);}
                cohorts.Add(id,c);return true;
            }
            finally{busy=false;}
        }
        private void UndoPrepared(IExpeditionCohortPlacement placement)
        {try{placement?.Rollback();}catch{Faulted=true;throw;}}
        public bool Commit(string id)
        {
            Guard();if(id==null||!cohorts.TryGetValue(id,out var c))return false;
            if(c.Status==ExpeditionCohortStatus.Committed)return true;
            if(c.Status!=ExpeditionCohortStatus.Loading)return false;
            if(now>=c.Deadline){Cancel(id);return false;}
            var connected=c.Members.Select(a=>members[a]).Where(m=>m.Connected).ToArray();
            if(connected.Length==0){Cancel(id);return false;}
            if(connected.Any(m=>!m.Ready))return false;
            busy=true;
            try
            {
                bool valid;try{valid=c.Placement.Validate();}catch{Rollback(c);return false;}
                if(!valid){Rollback(c);return false;}
                try{c.Placement.Apply();}
                catch{Rollback(c);return false;}
                foreach(var a in c.Members){var m=members[a];m.Area=c.Destination;m.Pending=null;}
                c.Status=ExpeditionCohortStatus.Committed;c.Placement=null;return true;
            }
            catch{c.Status=ExpeditionCohortStatus.Faulted;Faulted=true;throw;}
            finally{busy=false;}
        }
        private void Rollback(Cohort c)
        {
            c.Placement.Rollback();c.Placement=null;c.Status=ExpeditionCohortStatus.Cancelled;
            foreach(var a in c.Members)
            {
                var m=members[a];m.Pending=null;m.Ready=false;m.ticket=Ticket(c.Source,null);
                // Require a fresh source load ack: the peer may already display the
                // destination. Never resume commands in the wrong scene on rollback.
            }
        }
        public bool Cancel(string id)
        {
            Guard();if(id==null||!cohorts.TryGetValue(id,out var c))return false;
            if(c.Status==ExpeditionCohortStatus.Cancelled)return true;
            if(c.Status!=ExpeditionCohortStatus.Loading)return false;
            busy=true;try{Rollback(c);return true;}
            catch{c.Status=ExpeditionCohortStatus.Faulted;Faulted=true;throw;}
            finally{busy=false;}
        }
        public bool Disconnect(string account)
        {
            Guard();if(account==null||!members.TryGetValue(account,out var m)||!m.Connected)return false;
            m.Connected=false;m.Ready=false;m.ticket=Ticket(m.ticket.area,m.Pending);return true;
        }
        public ExpeditionCohortTicket Reconnect(string account)
        {
            Guard();if(account==null||!members.TryGetValue(account,out var m)||m.Connected)return null;
            m.Connected=true;m.Ready=false;m.ticket=Ticket(m.Pending==null?m.Area:cohorts[m.Pending].Destination,m.Pending);return m.Ticket;
        }
        // Monotonic host elapsed time, not wall time supplied by a guest.
        public void Advance(double elapsed)
        {
            Guard();if(!ExpeditionValidation.Number(elapsed)||elapsed<now)throw new ArgumentOutOfRangeException(nameof(elapsed));
            now=elapsed;foreach(var c in cohorts.Values.Where(c=>c.Status==ExpeditionCohortStatus.Loading&&now>=c.Deadline).ToArray())Cancel(c.Id);
        }
    }
}
