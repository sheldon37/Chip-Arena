using System;
using System.Linq;

namespace ChipArena.UnityConversion
{
    // Small, versioned wire/handoff record. The blueprint is rebuilt locally and never trusted from a peer.
    [Serializable] public sealed class ExpeditionDeploymentTicket
    {
        public int schema=1,revision,instanceRolls;
        public string station,originHost,expedition,compatibility,generatedCompatibility;
        public uint seed;public bool deployed;
        public ExpeditionDeploymentTicket Copy()=>(ExpeditionDeploymentTicket)MemberwiseClone();
    }
    public enum ExpeditionDeploymentIntent { RollInstances, SkipMutators, RollMutators, Deploy }

    public sealed class ExpeditionDeploymentRoom
    {
        public const string GeneratorVersion="deployment-blueprint-1";
        private readonly ExpeditionInstanceTheme[] catalog;
        private readonly string expedition,compatibility,owner;
        private NativeExpeditionDeployment authority;
        private string generatedCompatibility;
        public string Owner=>owner;
        public bool Deployed=>authority.Deployed;
        public ExpeditionDeploymentState Snapshot()=>authority.Snapshot();
        public ExpeditionDeploymentTicket Ticket
        {
            get
            {
                var s=authority.Snapshot();
                return new ExpeditionDeploymentTicket{station=s.station,originHost=s.host,seed=s.seed,
                    revision=s.revision,instanceRolls=s.instanceRolls,deployed=s.deployed,
                    expedition=expedition,compatibility=compatibility,generatedCompatibility=generatedCompatibility};
            }
        }
        public ExpeditionDeploymentRoom(string station,string host,uint seed,string selected,
            ExpeditionLayout layout,ConversionContent content)
        {
            if(!ExpeditionValidation.Id(host))throw new ArgumentException("Authenticated host required.");
            owner=host;expedition=selected;catalog=Catalog(selected,layout,content);
            compatibility=Compatibility(selected,layout,content,catalog);
            authority=new NativeExpeditionDeployment(station,host,seed);
        }
        // The caller obtains newOwner from the authenticated, fenced host handoff, never the wire ticket.
        public static ExpeditionDeploymentRoom Restore(ExpeditionDeploymentTicket ticket,string newOwner,
            ExpeditionLayout layout,ConversionContent content)
        {
            if(ticket==null)throw new ArgumentNullException(nameof(ticket));
            var room=new ExpeditionDeploymentRoom(ticket.station,newOwner,ticket.seed,ticket.expedition,layout,content);
            if(ticket.compatibility!=room.compatibility||
               ticket.deployed&&!Digest(ticket.generatedCompatibility)||
               !ticket.deployed&&!string.IsNullOrEmpty(ticket.generatedCompatibility))
                throw new ArgumentException("Deployment content or generated map is incompatible.");
            room.authority=NativeExpeditionDeployment.Restore(ticket,room.catalog);
            room.generatedCompatibility=ticket.generatedCompatibility;
            return room;
        }
        public bool Execute(string authenticatedAccount,int expectedRevision,ExpeditionDeploymentIntent intent,
            bool partyReady=false,string validatedGeneratedCompatibility=null)
        {
            var s=authority.Snapshot();
            if(authenticatedAccount!=owner||expectedRevision!=s.revision||s.deployed)return false;
            switch(intent)
            {
                case ExpeditionDeploymentIntent.RollInstances:return authority.RollInstances(s.host,catalog);
                case ExpeditionDeploymentIntent.SkipMutators:return authority.SkipMutators(s.host);
                // There are no approved effects. Never broadcast arbitrary IDs as active gameplay.
                case ExpeditionDeploymentIntent.RollMutators:return false;
                case ExpeditionDeploymentIntent.Deploy:
                    if(!Digest(validatedGeneratedCompatibility)||!authority.Deploy(s.host,partyReady))return false;
                    generatedCompatibility=validatedGeneratedCompatibility;return true;
                default:return false;
            }
        }
        private static bool Digest(string s)=>s!=null&&s.Length==64&&s.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
        private static string Compatibility(string selected,ExpeditionLayout layout,ConversionContent content,ExpeditionInstanceTheme[] pool)
        {
            if(string.IsNullOrEmpty(layout.compatibility)||string.IsNullOrEmpty(content.contentRevision))
                throw new ArgumentException("Validated layout and content compatibility are required.");
            return NativeMapRules.Revision(GeneratorVersion+"\n"+selected+"\n"+layout.compatibility+"\n"+
                content.contentRevision+"\n"+string.Join("\n",pool.Select(p=>p.id+":"+p.theme)));
        }
        private static ExpeditionInstanceTheme[] Catalog(string selected,ExpeditionLayout layout,ConversionContent content)
        {
            var entry=NativeExpeditionCatalogue.All.SingleOrDefault(e=>e.Id==selected&&!e.Locked);
            if(entry==null||layout==null||content?.maps==null||entry.LayoutWorld!=layout.world)
                throw new ArgumentException("Selected Expedition has no validated content.");
            var pool=entry.Areas.Where(a=>!a.Boss&&a.State==NativeExpeditionCatalogue.AreaState.Live).Select(a=>
            {
                var area=layout.Area(a.LayoutArea);var map=content.maps.SingleOrDefault(m=>m.id==a.Map);
                if(area==null||area.role!="instance"||area.map!=a.Map||!layout.instances.Contains(area.id)||
                   map==null||NativeMapRules.ArenaSelectable(map)||map.blocks==null||map.blocks.Length==0||
                   map.nav?.nodes==null||map.nav.nodes.Length==0||area.bossPoints==null||area.bossPoints.Length==0)
                    throw new ArgumentException("Authored instance is incomplete: "+a.LayoutArea);
                return new ExpeditionInstanceTheme{id=area.id,theme=area.id=="exp-foundry"?"industrial":area.id=="exp-transit"?"rail":"quarantine"};
            }).OrderBy(p=>p.id,StringComparer.Ordinal).ToArray();
            if(pool.Length<3||pool.Select(p=>p.id).Distinct().Count()!=pool.Length)
                throw new ArgumentException("Three distinct authored instances are required.");
            return pool;
        }
    }

    // Bind a fresh receiver when entering a different station. Old/stale tickets cannot reset a deployed run.
    public sealed class ExpeditionDeploymentReceiver
    {
        private ExpeditionDeploymentTicket ticket;
        private ExpeditionDeploymentState state;
        public ExpeditionDeploymentState Snapshot=>state?.Copy();
        public bool Accept(ExpeditionDeploymentTicket incoming,ExpeditionLayout layout,ConversionContent content)
        {
            if(incoming==null)return false;
            if(ticket!=null&&(incoming.station!=ticket.station||incoming.originHost!=ticket.originHost||
               incoming.seed!=ticket.seed||incoming.compatibility!=ticket.compatibility||incoming.revision<ticket.revision||
               incoming.instanceRolls<ticket.instanceRolls||ticket.deployed&&
               (incoming.revision!=ticket.revision||incoming.generatedCompatibility!=ticket.generatedCompatibility||!incoming.deployed)))return false;
            try
            {
                var restored=ExpeditionDeploymentRoom.Restore(incoming,incoming.originHost,layout,content);
                if(ticket!=null&&incoming.revision==ticket.revision&&
                   (incoming.instanceRolls!=ticket.instanceRolls||incoming.deployed!=ticket.deployed))return false;
                ticket=incoming.Copy();state=restored.Snapshot();return true;
            }
            catch(ArgumentException){return false;}
        }
    }
}
