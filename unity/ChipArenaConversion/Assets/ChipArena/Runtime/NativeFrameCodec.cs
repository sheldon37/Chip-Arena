using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Unity inline serialization replaces null classes with default instances. Presence is
    // wire data, not inferred from those defaults. Used by networking AND replay deep copies.
    public sealed partial class NativeGameFrame
    {
        public void CapturePresence()
        {
            hasPve=pve!=null;
            if(snapshot?.Actors!=null)foreach(var a in snapshot.Actors)if(a!=null)a.HasKernel=a.Kernel!=null;
            if(props!=null)foreach(var p in props)if(p!=null)p.hasDefinition=p.definition!=null;
            if(pilots!=null)foreach(var p in pilots)if(p!=null)
            {p.hasDeath=p.death!=null;p.hasFighter=p.fighter!=null;p.hasDown=p.down!=null;CaptureDeath(p.death);}
            if(corpses!=null)foreach(var d in corpses)CaptureDeath(d);
            if(pve?.rows!=null)foreach(var row in pve.rows)if(row?.picks!=null)foreach(var pick in row.picks)if(pick!=null)pick.hasOffers=pick.offers!=null;
        }
        private static void CaptureDeath(NativeDeath d)
        {if(d!=null){d.hasActor=d.actor!=null;d.hasWound=d.wound!=null;if(d.actor!=null)d.actor.HasKernel=d.actor.Kernel!=null;}}
        private static void RestoreDeath(NativeDeath d)
        {if(d!=null){if(!d.hasActor)d.actor=null;if(d.actor!=null&&!d.actor.HasKernel)d.actor.Kernel=null;if(!d.hasWound)d.wound=null;}}
        public void RestorePresence()
        {
            if(devTimeScale==0)devTimeScale=1;
            if(seats!=null)foreach(var seat in seats)if(seat!=null&&seat.devScale==0)seat.devScale=1;
            if(!hasFinalKillcam){finalVictim=finalSeed=0;resultAt=0;}
            // JsonUtility turns null strings into empty strings. Runtime draft/end-state gates use null as "not chosen/ended".
            chosen=string.IsNullOrEmpty(chosen)?null:chosen;introStyle=string.IsNullOrEmpty(introStyle)?null:introStyle;
            if(!hasPve)pve=null;
            if(snapshot?.Actors!=null)foreach(var a in snapshot.Actors)if(a!=null&&!a.HasKernel)a.Kernel=null;
            if(pve!=null){pve.cinematic=string.IsNullOrEmpty(pve.cinematic)?null:pve.cinematic;pve.outcome=string.IsNullOrEmpty(pve.outcome)?null:pve.outcome;pve.notice=string.IsNullOrEmpty(pve.notice)?null:pve.notice;}
            if(props!=null)foreach(var p in props)if(p!=null&&!p.hasDefinition)p.definition=null;
            if(pilots!=null)foreach(var p in pilots)if(p!=null)
            {if(!p.hasDeath)p.death=null;if(!p.hasFighter)p.fighter=null;if(!p.hasDown)p.down=null;RestoreDeath(p.death);}
            if(corpses!=null)foreach(var d in corpses)RestoreDeath(d);
            if(pve?.rows!=null)foreach(var row in pve.rows)if(row?.picks!=null)foreach(var pick in row.picks)if(pick!=null&&!pick.hasOffers)pick.offers=null;
        }
    }
    public static class NativeFrameCodec
    {
        public static NativeGameFrame Clone(NativeGameFrame frame)
        {frame.CapturePresence();var result=JsonUtility.FromJson<NativeGameFrame>(JsonUtility.ToJson(frame));result.RestorePresence();return result;}
    }
    public static class NativeNetCodec
    {
        [Serializable] private sealed class DevCommandEnvelope
        {public int version=NativeConnection.Protocol,epoch,generation;public string type="dev-command";public NativeDevCommand devCommand;}
        [Serializable] private sealed class DevReplyEnvelope
        {public int version=NativeConnection.Protocol,epoch;public string type="dev-reply";public NativeDevReply devReply;}
        [Serializable] private sealed class FrameEnvelope
        {public int version=NativeConnection.Protocol,epoch;public string type="frame";public NativeGameFrame frame;}
        [Serializable] private sealed class LobbyEnvelope
        {public int version=NativeConnection.Protocol,epoch;public string type="lobby";public NativeSeatFrame[] seats;public NativeRoomOptions options;}
        [Serializable] private sealed class ExpeditionPublicEnvelope
        {public int version=NativeConnection.Protocol;public string type="expedition-public";public ExpeditionView expeditionPublic;}
        [Serializable] private sealed class ExpeditionPrivateEnvelope
        {public int version=NativeConnection.Protocol;public string type="expedition-private";public ExpeditionPrivateFrame expeditionPrivate;}
        [Serializable] private sealed class ExpeditionCommandEnvelope
        {public int version=NativeConnection.Protocol;public string type="expedition-command";public ExpeditionCommandRequest expeditionCommand;}
        [Serializable] private sealed class ExpeditionReplyEnvelope
        {public int version=NativeConnection.Protocol;public string type="expedition-reply";public ExpeditionCommandReply expeditionReply;}
        [Serializable] private sealed class ExpeditionAreaReadyEnvelope
        {public int version=NativeConnection.Protocol;public string type="expedition-area-ready";public ExpeditionAreaReadyRequest expeditionAreaReady;}
        public static string Encode(NativeNetMessage message)
        {
            if(message.type=="dev-command")return JsonUtility.ToJson(new DevCommandEnvelope{epoch=message.epoch,generation=message.generation,devCommand=message.devCommand});
            if(message.type=="dev-reply")return JsonUtility.ToJson(new DevReplyEnvelope{epoch=message.epoch,devReply=message.devReply});
            if(message.type=="expedition-area-ready")return JsonUtility.ToJson(new ExpeditionAreaReadyEnvelope{expeditionAreaReady=message.expeditionAreaReady});
            if(message.type=="expedition-public")return JsonUtility.ToJson(new ExpeditionPublicEnvelope{expeditionPublic=message.expeditionPublic});
            if(message.type=="expedition-private")return JsonUtility.ToJson(new ExpeditionPrivateEnvelope{expeditionPrivate=message.expeditionPrivate});
            if(message.type=="expedition-command")return JsonUtility.ToJson(new ExpeditionCommandEnvelope{expeditionCommand=message.expeditionCommand});
            if(message.type=="expedition-reply")return JsonUtility.ToJson(new ExpeditionReplyEnvelope{expeditionReply=message.expeditionReply});
            if(message.type=="lobby")return JsonUtility.ToJson(new LobbyEnvelope{epoch=message.epoch,seats=message.seats,options=message.options});
            if(message.type!="frame")return JsonUtility.ToJson(message);
            if(message.frame==null)throw new ArgumentException("Missing match frame");
            message.frame.CapturePresence();
            return JsonUtility.ToJson(new FrameEnvelope{epoch=message.epoch,frame=message.frame});
        }
        public static NativeNetMessage Decode(string json)
        {
            var message=JsonUtility.FromJson<NativeNetMessage>(json);
            if(message?.type=="dev-command")message.devCommand=JsonUtility.FromJson<DevCommandEnvelope>(json).devCommand;
            if(message?.type=="dev-reply")message.devReply=JsonUtility.FromJson<DevReplyEnvelope>(json).devReply;
            if(message?.type=="expedition-area-ready")message.expeditionAreaReady=JsonUtility.FromJson<ExpeditionAreaReadyEnvelope>(json).expeditionAreaReady;
            if(message?.type=="expedition-public")message.expeditionPublic=JsonUtility.FromJson<ExpeditionPublicEnvelope>(json).expeditionPublic;
            if(message?.type=="expedition-private")message.expeditionPrivate=JsonUtility.FromJson<ExpeditionPrivateEnvelope>(json).expeditionPrivate;
            if(message?.type=="expedition-command")message.expeditionCommand=JsonUtility.FromJson<ExpeditionCommandEnvelope>(json).expeditionCommand;
            if(message?.type=="expedition-reply")message.expeditionReply=JsonUtility.FromJson<ExpeditionReplyEnvelope>(json).expeditionReply;
            if(message?.type=="frame")
            {message.frame=JsonUtility.FromJson<FrameEnvelope>(json).frame;message.frame?.RestorePresence();}
            if(message?.type=="lobby")message.options=JsonUtility.FromJson<LobbyEnvelope>(json).options;
            return message;
        }
    }
}
