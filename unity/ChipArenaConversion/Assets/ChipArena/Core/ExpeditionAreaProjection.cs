using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public sealed class ExpeditionAreaRecipient
    {
        public string Account;public ExpeditionView Public;public ExpeditionPrivateFrame Private;
    }
    public static class ExpeditionAreaProjection
    {
        // Provider must return a detached projection built from this area's director.
        // Never derive area phase/loading from the host's currently displayed arena.
        public static ExpeditionAreaRecipient Build(string account,ExpeditionCohorts cohorts,Func<string,string,ExpeditionView> project,ExpeditionJoinRules joins)
        {
            var member=cohorts?.Member(account);
            if(member==null||!member.Connected||project==null)throw new ArgumentException("Connected recipient and area projection required.");
            var ticket=member.Ticket;
            // During transfer, the client loads the ticket's destination while the
            // physical pilot remains in the source until the barrier commits.
            var view=project(ticket.area,account);
            if(view==null||view.run!=ticket.run||view.epoch!=ticket.epoch||view.area!=ticket.area||view.viewer!=account||view.mine?.account!=account)
                throw new InvalidOperationException("Area projection does not match recipient identity.");
            if(view.anchors.Any(a=>a.area!=ticket.area)||view.closures.Any(c=>c.area!=ticket.area)||view.bossArrivals.Any(a=>a.area!=ticket.area)||
                view.chests.Any(c=>!view.anchors.Any(a=>a.id==c.id&&a.kind=="chest"))||
                view.loot.Any(r=>!r.id.StartsWith(view.loop+":"+ticket.area+":",StringComparison.Ordinal))||
                view.dice.Any(d=>!d.boss.StartsWith(view.loop+":"+ticket.area+":",StringComparison.Ordinal)))
                throw new InvalidOperationException("Cross-area public data refused.");
            if(view.hub!=null&&ticket.area!=view.world)throw new InvalidOperationException("Hub state is not an instance projection.");
            var barrier=member.Pending==null?null:cohorts.View(member.Pending);
            view.areaBarrier=new ExpeditionAreaBarrierView{target=ticket.area,layout=ticket.layout,generation=ticket.serial,
                active=member.Pending!=null||!member.Ready,required=barrier?.Required??(member.Ready?0:1),ready=barrier?.Ready??(member.Ready?1:0)};
            view.loading=view.areaBarrier.active;view.areaIdentity=ticket.Copy();
            view.joins=ticket.area==view.world?joins?.Views??Array.Empty<ExpeditionJoinView>():Array.Empty<ExpeditionJoinView>();
            foreach(var p in view.members){var peer=cohorts.Member(p.account);p.area=peer?.Area;p.pendingArea=peer?.Pending==null?null:peer.Ticket.area;p.atJoin=joins?.AtJoin(p.account)==true;}
            var personal=new ExpeditionPrivateFrame{run=view.run,epoch=view.epoch,revision=view.revision,viewer=account,mine=view.mine,areaIdentity=ticket.Copy()};
            view.mine=null;view.viewer=null;
            return new ExpeditionAreaRecipient{Account=account,Public=view,Private=personal};
        }
    }
}
