using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Negotiated separately from the legacy whole-party Expedition protocol.
    public static class ExpeditionSplitCapability
    {
        public const int Version=1;
        public static bool Supported(int host,IEnumerable<int> guests)=>host==Version&&guests!=null&&guests.All(v=>v==Version);
    }
    [Serializable] public sealed class ExpeditionAreaEnvelope<T>
    {
        public ExpeditionCohortTicket identity;public long sequence;public T payload;
    }
    // A channel is bound explicitly by the authenticated load/transfer control
    // path. Receiving data never switches a client's scene or accepted identity.
    public sealed class ExpeditionAreaChannel<T>
    {
        private ExpeditionCohortTicket expected;private long received=-1,sent;
        public ExpeditionCohortTicket Identity=>expected?.Copy();
        public long Received=>received;
        public bool Bind(ExpeditionCohortTicket ticket)
        {
            if(!Valid(ticket))return false;
            if(expected!=null)
            {
                if(expected.Matches(ticket))return true;
                if(ticket.run!=expected.run||ticket.epoch<expected.epoch||ticket.epoch==expected.epoch&&ticket.serial<=expected.serial)return false;
            }
            expected=ticket.Copy();received=-1;sent=0;return true;
        }
        // Unity JsonUtility decodes an absent optional cohort as an empty string. Matches treats both as absent.
        public static bool Valid(ExpeditionCohortTicket t)=>t!=null&&ExpeditionValidation.Id(t.run)&&ExpeditionValidation.Id(t.area)&&
            t.epoch>0&&t.serial>0&&t.layout!=null&&t.layout.Length==64&&t.layout.All(Uri.IsHexDigit)&&(string.IsNullOrEmpty(t.cohort)||ExpeditionValidation.Id(t.cohort));
        public ExpeditionAreaEnvelope<T> Frame(T payload)
        {
            if(expected==null||payload==null)throw new InvalidOperationException("Bound identity and payload required.");
            return new ExpeditionAreaEnvelope<T>{identity=expected.Copy(),sequence=checked(++sent),payload=payload};
        }
        public bool Accept(ExpeditionAreaEnvelope<T> frame)
        {
            if(expected==null||frame==null||frame.payload==null||frame.sequence<1||frame.sequence<=received||!expected.Matches(frame.identity))return false;
            received=frame.sequence;return true;
        }
    }
    // Host command/input readiness is checked against the sender's current ticket,
    // never a client-supplied account or the host's displayed scene.
    public sealed class ExpeditionAreaCommandGate
    {
        private readonly ExpeditionCohorts cohorts;
        private readonly Dictionary<string,(ExpeditionCohortTicket ticket,long sequence)> accepted=new Dictionary<string,(ExpeditionCohortTicket,long)>();
        public ExpeditionAreaCommandGate(ExpeditionCohorts cohorts){this.cohorts=cohorts??throw new ArgumentNullException(nameof(cohorts));}
        public bool Accept(string authenticatedAccount,ExpeditionCohortTicket identity,long sequence)
        {
            if(sequence<1||!cohorts.Accepts(authenticatedAccount,identity))return false;
            if(accepted.TryGetValue(authenticatedAccount,out var prior)&&prior.ticket.Matches(identity)&&sequence<=prior.sequence)return false;
            accepted[authenticatedAccount]=(identity.Copy(),sequence);return true;
        }
    }
}
