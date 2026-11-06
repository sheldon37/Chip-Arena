using System;
namespace ChipArena.UnityConversion
{
    // Reconstructed from a bounded authenticated ticket. Never accept maps/layouts from the network.
    public sealed class ExpeditionRunBundle
    {
        public readonly ExpeditionGeneratedWorld World;
        public readonly BackroomsGenerated Backrooms;
        public readonly PoolroomsGenerated Poolrooms;
        public readonly string[] Mutators;
        public readonly ExpeditionLayout Layout;
        public readonly ConversionContent Content;
        public readonly ExpeditionDeploymentTicket Ticket;
        public string Identity=>Layout.compatibility;
        private ExpeditionRunBundle(ExpeditionDeploymentRoom room,ConversionContent content,string authored)
        {
            Ticket=room.Ticket;Mutators=room.Snapshot().mutators;World=NativeExpeditionGeneratedWorld.Build(room);
            if(Array.IndexOf(room.Snapshot().instances,NativeBackroomsGenerator.AreaId)>=0)Backrooms=NativeBackroomsGenerator.Build(unchecked((int)(room.Snapshot().worldSeed^0xbac00001u)),true);
            if(Array.IndexOf(room.Snapshot().instances,NativePoolroomsGenerator.AreaId)>=0)Poolrooms=NativePoolroomsGenerator.Build(unchecked((int)(room.Snapshot().worldSeed^0xbac00002u)),true);
            Layout=NativeExpeditionGeneratedWorld.ComposeLayout(World,authored,Backrooms==null?null:NativeBackroomsExpedition.Area(Backrooms),Poolrooms==null?null:NativePoolroomsExpedition.Area(Poolrooms));
            Layout.compatibility=NativeMapRules.Revision(Layout.compatibility+"\n"+NativeExpeditionMutators.Version+"\n"+string.Join("\n",Mutators));
            Content=content.WithGeneratedMap(World.Map);
            if(Backrooms!=null){Content=Content.WithGeneratedMap(Backrooms.Map);Layout.compatibility=NativeMapRules.Revision(Layout.compatibility+"\n"+Backrooms.Identity);}
            if(Poolrooms!=null){Content=Content.WithGeneratedMap(Poolrooms.Map);Layout.compatibility=NativeMapRules.Revision(Layout.compatibility+"\n"+Poolrooms.Identity);}
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
        // Standalone boss arenas reuse the same generated geometry records, without a station or world roll.
        private ExpeditionRunBundle(string boss,ConversionContent content,string authored)
        {
            if(!NativeBossBattle.Zone(boss))throw new ArgumentException("Unknown direct boss.");
            Mutators=Array.Empty<string>();Layout=NativeExpeditionLayout.Parse(authored);Content=content;
            ExpeditionAreaLayout area=null;
            if(boss=="hum"){Backrooms=NativeBackroomsGenerator.Build(NativeBossBattle.Seed,true);Content=content.WithGeneratedMap(Backrooms.Map);area=NativeBackroomsExpedition.Area(Backrooms);}
            if(boss=="deepend"){Poolrooms=NativePoolroomsGenerator.Build(NativeBossBattle.Seed,true);Content=content.WithGeneratedMap(Poolrooms.Map);area=NativePoolroomsExpedition.Area(Poolrooms);}
            if(boss=="noctiel"){Content=content.WithGeneratedMap(NativeFallenRules.Arena());area=NativeFallenRules.Layout();}
            if(area!=null)Layout.areas=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(System.Linq.Enumerable.Where(Layout.areas,a=>a.id!=area.id),new[]{area}));
        }
        public static ExpeditionRunBundle BossBattle(string boss,ConversionContent content,string authored)=>new ExpeditionRunBundle(boss,content,authored);
        public bool Matches(ExpeditionDeploymentTicket ticket)=>Ticket!=null&&ticket!=null&&ticket.schema==1&&ticket.revision==(long)ticket.instanceRolls+ticket.mutatorRolls+ticket.mutatorSkips+(ticket.deployed?1:0)&&ticket.mutatorRolls==Ticket.mutatorRolls&&ticket.mutatorSkips==Ticket.mutatorSkips&&ticket.mutatorsSkipped==Ticket.mutatorsSkipped&&ticket.station==Ticket.station&&
            ticket.seed==Ticket.seed&&ticket.originHost==Ticket.originHost&&ticket.instanceRolls==Ticket.instanceRolls&&
            ticket.compatibility==Ticket.compatibility&&(!ticket.deployed||ticket.generatedCompatibility==Identity);
    }
}
