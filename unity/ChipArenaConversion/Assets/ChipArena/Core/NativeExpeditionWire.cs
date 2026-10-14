using System;
using System.Collections.Generic;
using System.Linq;

namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class ExpeditionPrivateFrame
    {public string run,viewer;public int epoch;public long revision;public ExpeditionViewPersonal mine;}
    [Serializable] public sealed class ExpeditionCommandRequest
    {public string run,transaction,target,chip;public int epoch;public ExpeditionIntent intent;}
    [Serializable] public sealed class ExpeditionCommandReply
    {public string transaction,reason;public ExpeditionCommandState state;}
    public sealed class ExpeditionViewBatch
    {
        public ExpeditionView Public;public ExpeditionPrivateFrame[] Private;
        public static ExpeditionViewBatch Build(NativeExpedition run,IEnumerable<string> viewers,ExpeditionViewSources sources)
        {
            var common=NativeExpeditionView.Build(run,null,sources);common.mine=null;common.viewer=null;
            return new ExpeditionViewBatch{Public=common,Private=viewers.Distinct().Select(a=>
            {var v=NativeExpeditionView.Build(run,a,sources);return new ExpeditionPrivateFrame{run=v.run,viewer=a,epoch=v.epoch,revision=v.revision,mine=v.mine};}).ToArray()};
        }
    }
    /// <summary>Pair only matching versions; never expose new public state with stale personal permissions/offers.</summary>
    public sealed class ExpeditionViewReceiver
    {
        private ExpeditionView pending;private ExpeditionPrivateFrame personal;
        private readonly string viewer;private string run;
        public ExpeditionView View{get;private set;}
        public ExpeditionViewReceiver(string viewer){this.viewer=viewer??throw new ArgumentNullException(nameof(viewer));}
        private bool Fresh(string id,int epoch,long revision)
        {return ExpeditionValidation.Id(id)&&epoch>0&&revision>=0&&(run==null||run==id)&&(View==null||epoch>View.epoch||epoch==View.epoch&&revision>=View.revision);}
        private static bool Older(int epoch,long revision,int otherEpoch,long otherRevision)=>epoch<otherEpoch||epoch==otherEpoch&&revision<otherRevision;
        public bool Public(ExpeditionView frame)
        {
            if(frame==null||!Fresh(frame.run,frame.epoch,frame.revision)||!string.IsNullOrEmpty(frame.viewer)||frame.mine!=null&&(!string.IsNullOrEmpty(frame.mine.account)||frame.mine.wallet!=0||(frame.mine.rewards?.Length??0)>0||(frame.mine.chips?.Length??0)>0))return false;
            if(pending!=null&&Older(frame.epoch,frame.revision,pending.epoch,pending.revision))return false;
            run=frame.run;pending=NativeExpeditionView.Normalize(frame);return Join();
        }
        public bool Private(ExpeditionPrivateFrame frame)
        {
            if(frame==null||frame.viewer!=viewer||frame.mine?.account!=viewer||!Fresh(frame.run,frame.epoch,frame.revision))return false;
            if(personal!=null&&Older(frame.epoch,frame.revision,personal.epoch,personal.revision))return false;
            run=frame.run;personal=frame;return Join();
        }
        private bool Join()
        {
            if(pending==null||personal==null||pending.run!=personal.run||pending.epoch!=personal.epoch||pending.revision!=personal.revision)return false;
            pending.viewer=viewer;pending.mine=personal.mine;View=NativeExpeditionView.Normalize(pending);pending=null;personal=null;return true;
        }
    }
    /// <summary>Authenticated transport supplies account; payload has no sender identity. Bounded dedupe fails closed.</summary>
    public sealed class ExpeditionCommandRouter
    {
        private readonly ExpeditionSession session;
        private readonly Dictionary<string,(ExpeditionCommandRequest request,ExpeditionCommandReply reply)> history=new Dictionary<string,(ExpeditionCommandRequest,ExpeditionCommandReply)>();
        public ExpeditionCommandRouter(ExpeditionSession session){this.session=session;}
        public ExpeditionCommandReply Execute(string authenticatedAccount,ExpeditionCommandRequest request)
        {
            ExpeditionCommandReply Refuse(string reason)=>new ExpeditionCommandReply{transaction=request?.transaction,state=ExpeditionCommandState.Refused,reason=reason};
            var s=session.Run.Snapshot();
            if(request==null||!ExpeditionValidation.Id(request.transaction)||request.run!=s.run||request.epoch!=s.epoch||!s.members.Any(m=>m.account==authenticatedAccount&&m.connected&&!m.extracted)||
                !Enum.IsDefined(typeof(ExpeditionIntent),request.intent)||(request.target?.Length??0)>128||(request.chip?.Length??0)>128)return Refuse("Invalid, stale, or unauthenticated Expedition command.");
            string key=authenticatedAccount+"|"+request.transaction;
            if(history.TryGetValue(key,out var old))return old.request.intent==request.intent&&old.request.target==request.target&&old.request.chip==request.chip?old.reply:Refuse("Transaction was already used for another command.");
            if(history.Count>=ExpeditionValidation.MaxRecords)return Refuse("Expedition command journal is full.");
            var result=session.Execute(authenticatedAccount,new ExpeditionCommand(request.intent,request.target,request.chip));
            var reply=new ExpeditionCommandReply{transaction=request.transaction,state=result.state,reason=result.reason};
            history[key]=(new ExpeditionCommandRequest{intent=request.intent,target=request.target,chip=request.chip},reply);return reply;
        }
    }
}
