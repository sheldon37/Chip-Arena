using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    internal static class ExpeditionAreaWireChecks
    {
        private sealed class Placement:IExpeditionCohortPlacement{public bool Validate()=>true;public void Apply(){}public void Rollback(){}}
        public static void Run()
        {
            int count=0;void Check(bool ok,string why){if(!ok)throw new Exception("Area wire: "+why);count++;}
            var cohorts=new ExpeditionCohorts("run",1,new string('a',64),"hub","ward");cohorts.Add("host","hub");cohorts.Add("guest","hub");
            var host=cohorts.Member("host").Ticket;var old=cohorts.Member("guest").Ticket;cohorts.Acknowledge("host",host);cohorts.Acknowledge("guest",old);
            Check(!ExpeditionSplitCapability.Supported(1,new[]{1,0})&&ExpeditionSplitCapability.Supported(1,new[]{1,1,1}),"old clients rejected");
            var gate=new ExpeditionAreaCommandGate(cohorts);Check(gate.Accept("guest",old,1)&&!gate.Accept("guest",old,1),"input sequences deduplicated");
            Check(!gate.Accept("host",old,2),"authenticated account cannot borrow another ticket");
            Check(cohorts.Begin("transfer","hub","ward",new[]{"guest"},30,(a,b,c)=>new Placement()),"cohort begins");
            var current=cohorts.Member("guest").Ticket;
            Check(!gate.Accept("guest",old,2)&&!gate.Accept("guest",current,1)&&gate.Accept("host",host,1),"newcomer blocked while hub keeps accepting input");
            Check(cohorts.Acknowledge("guest",current)&&cohorts.Commit("transfer"),"new area committed");
            Check(gate.Accept("guest",current,1)&&!gate.Accept("guest",old,100),"new identity resets stream, old identity stays stale");
            var tx=new ExpeditionAreaChannel<string>();var rx=new ExpeditionAreaChannel<string>();tx.Bind(old);rx.Bind(old);var stale=tx.Frame("hub");Check(rx.Accept(stale)&&!rx.Accept(stale),"frame duplicate rejected");
            tx.Bind(current);rx.Bind(current);Check(!rx.Accept(stale)&&rx.Accept(tx.Frame("ward")),"cross-area frame rejected after explicit rebind");
            Check(!rx.Bind(old),"stale control ticket cannot roll back scene");
            var forged=current.Copy();forged.epoch++;Check(!rx.Accept(new ExpeditionAreaEnvelope<string>{identity=forged,sequence=999,payload="bad"}),"frame cannot silently advance epoch");
            var detached=rx.Identity;detached.area="hub";Check(rx.Identity.area=="ward","identity detached");
            ExpeditionView Project(string area,string viewer)=>new ExpeditionView{run="run",epoch=1,revision=5,world="hub",area=area,viewer=viewer,phase=area=="hub"?ExpeditionPhase.Town:ExpeditionPhase.Instance,mine=new ExpeditionViewPersonal{account=viewer,wallet=viewer=="host"?10:20},members=new[]{new ExpeditionViewMember{account="host"},new ExpeditionViewMember{account="guest"}}};
            var h=ExpeditionAreaProjection.Build("host",cohorts,Project,null);var g=ExpeditionAreaProjection.Build("guest",cohorts,Project,null);
            Check(h.Public.area=="hub"&&g.Public.area=="ward"&&h.Public.phase==ExpeditionPhase.Town&&g.Public.phase==ExpeditionPhase.Instance,"viewer-local phase and area");
            Check(h.Public.mine==null&&g.Public.mine==null&&h.Private.mine.wallet==10&&g.Private.mine.wallet==20,"private state separated per recipient");
            Check(g.Public.members.Single(m=>m.account=="host").area=="hub"&&g.Public.members.Single(m=>m.account=="guest").area=="ward","party area list");
            var receiver=new ExpeditionViewReceiver("guest");Check(!receiver.Public(g.Public),"split data refused before control binding");Check(receiver.BindArea(current),"control binds receiver");
            Check(!receiver.Public(g.Public)&&receiver.Private(g.Private)&&receiver.View.area=="ward","paired area projection");
            Check(!receiver.Private(h.Private),"another recipient private frame refused");
            bool leak=false;try{ExpeditionAreaProjection.Build("guest",cohorts,(area,viewer)=>{var v=Project(area,viewer);v.anchors=new[]{new ExpeditionWorldAnchor{id="leak",area="hub"}};return v;},null);}catch(InvalidOperationException){leak=true;}
            Check(leak,"cross-area anchors fail closed");
            cohorts.Disconnect("guest");var rejoin=cohorts.Reconnect("guest");Check(!cohorts.Acknowledge("guest",current)&&cohorts.Acknowledge("guest",rejoin),"reconnect rotates ack ticket");
            Check(receiver.BindArea(rejoin)&&receiver.View==null&&!receiver.Private(g.Private),"reconnect clears stale display and private data");
            ExpeditionView commandView=new ExpeditionView{run="run",epoch=1,viewer="guest",areaIdentity=rejoin};ExpeditionCommandRequest sent=null;
            var commands=new ExpeditionGuestCommands("guest",()=>commandView,r=>sent=r);var command=new ExpeditionCommand(ExpeditionIntent.CancelExit);commands.Send(command);
            commands.Reply(new ExpeditionCommandReply{transaction=sent.transaction,state=ExpeditionCommandState.Pending,areaIdentity=current});Check(!commands.Result(command).Shown,"old-transfer ack ignored");
            commands.Reply(new ExpeditionCommandReply{transaction=sent.transaction,state=ExpeditionCommandState.Pending,areaIdentity=rejoin});Check(commands.Result(command).Shown,"current ack accepted");
            commandView.areaIdentity=host;Check(!commands.Result(command).Shown,"cached reply cannot cross identity boundary");
            Console.WriteLine("PASS Expedition area wire: "+count+" checks");
        }
    }
}
