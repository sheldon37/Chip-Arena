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
            if(props!=null)foreach(var p in props)if(p!=null)p.hasDefinition=p.definition!=null;
            if(pilots!=null)foreach(var p in pilots)if(p!=null)
            {p.hasDeath=p.death!=null;p.hasFighter=p.fighter!=null;p.hasDown=p.down!=null;CaptureDeath(p.death);}
            if(corpses!=null)foreach(var d in corpses)CaptureDeath(d);
            if(pve?.rows!=null)foreach(var row in pve.rows)if(row?.picks!=null)foreach(var pick in row.picks)if(pick!=null)pick.hasOffers=pick.offers!=null;
        }
        private static void CaptureDeath(NativeDeath d)
        {if(d!=null){d.hasActor=d.actor!=null;d.hasWound=d.wound!=null;}}
        private static void RestoreDeath(NativeDeath d)
        {if(d!=null){if(!d.hasActor)d.actor=null;if(!d.hasWound)d.wound=null;}}
        public void RestorePresence()
        {
            // JsonUtility turns null strings into empty strings. Runtime draft/end-state gates use null as "not chosen/ended".
            chosen=string.IsNullOrEmpty(chosen)?null:chosen;introStyle=string.IsNullOrEmpty(introStyle)?null:introStyle;
            if(!hasPve)pve=null;
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
        [Serializable] private sealed class FrameEnvelope
        {public int version=NativeConnection.Protocol,epoch;public string type="frame";public NativeGameFrame frame;}
        [Serializable] private sealed class LobbyEnvelope
        {public int version=NativeConnection.Protocol,epoch;public string type="lobby";public NativeSeatFrame[] seats;public NativeRoomOptions options;}
        public static string Encode(NativeNetMessage message)
        {
            if(message.type=="lobby")return JsonUtility.ToJson(new LobbyEnvelope{epoch=message.epoch,seats=message.seats,options=message.options});
            if(message.type!="frame")return JsonUtility.ToJson(message);
            if(message.frame==null)throw new ArgumentException("Missing match frame");
            message.frame.CapturePresence();
            return JsonUtility.ToJson(new FrameEnvelope{epoch=message.epoch,frame=message.frame});
        }
        public static NativeNetMessage Decode(string json)
        {
            var message=JsonUtility.FromJson<NativeNetMessage>(json);
            if(message?.type=="frame")
            {message.frame=JsonUtility.FromJson<FrameEnvelope>(json).frame;message.frame?.RestorePresence();}
            if(message?.type=="lobby")message.options=JsonUtility.FromJson<LobbyEnvelope>(json).options;
            return message;
        }
    }
}
