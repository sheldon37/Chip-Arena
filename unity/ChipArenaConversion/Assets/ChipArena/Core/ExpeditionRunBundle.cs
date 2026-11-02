using System;
namespace ChipArena.UnityConversion
{
    // Reconstructed from a bounded authenticated ticket. Never accept maps/layouts from the network.
    public sealed class ExpeditionRunBundle
    {
        public readonly ExpeditionGeneratedWorld World;
        public readonly ExpeditionLayout Layout;
        public readonly ConversionContent Content;
        public readonly ExpeditionDeploymentTicket Ticket;
        public string Identity=>Layout.compatibility;
        private ExpeditionRunBundle(ExpeditionDeploymentRoom room,ConversionContent content,string authored)
        {
            Ticket=room.Ticket;World=NativeExpeditionGeneratedWorld.Build(room);
            Layout=NativeExpeditionGeneratedWorld.ComposeLayout(World,authored);
            Content=content.WithGeneratedMap(World.Map);
            if(Ticket.deployed&&Ticket.generatedCompatibility!=Identity)
                throw new ArgumentException("Generated run identity does not match its deployment ticket.");
        }
        public static ExpeditionRunBundle Build(ExpeditionDeploymentTicket ticket,ConversionContent content,string authored)
        {
            var layout=NativeExpeditionLayout.Parse(authored);
            layout.compatibility=NativeExpeditionLayout.Compatibility(authored,content);
            var room=ExpeditionDeploymentRoom.Restore(ticket,ticket?.originHost,layout,content);
            return new ExpeditionRunBundle(room,content,authored);
        }
        public bool Matches(ExpeditionDeploymentTicket ticket)=>ticket!=null&&ticket.schema==1&&ticket.revision==(long)ticket.instanceRolls+(ticket.deployed?1:0)&&ticket.station==Ticket.station&&
            ticket.seed==Ticket.seed&&ticket.originHost==Ticket.originHost&&ticket.instanceRolls==Ticket.instanceRolls&&
            ticket.compatibility==Ticket.compatibility&&(!ticket.deployed||ticket.generatedCompatibility==Identity);
    }
}
