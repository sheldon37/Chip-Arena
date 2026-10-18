using System;
using System.Linq;
using System.Text;
namespace ChipArena.UnityConversion
{
    public static class NativeFrameBudget
    {
        public const int MaxJsonBytes=1024*1024,MaxPacketBytes=256*1024;
        public static byte[] Encode(NativeGameFrame frame,int epoch,ExpeditionView publicView=null,ExpeditionPrivateFrame privateView=null)
        {
            // Everything trimmed here is a view/effect, never an authority entity or a combat operation.
            if(frame.snapshot.Events.Length>NativeEventBuffer.CapacityLimit)frame.snapshot.Events=frame.snapshot.Events.Skip(frame.snapshot.Events.Length-NativeEventBuffer.CapacityLimit).ToArray();
            while(true)
            {
                string json=NativeNetCodec.Encode(new NativeNetMessage{type=publicView==null?"frame":"expedition-state",epoch=epoch,frame=frame,expeditionPublic=publicView,expeditionPrivate=privateView});
                if(Encoding.UTF8.GetByteCount(json)<=MaxJsonBytes){var bytes=NativeConnection.Encode(json);if(bytes.Length<=MaxPacketBytes)return bytes;}
                if(frame.snapshot.Events.Length>0){frame.snapshot.Events=frame.snapshot.Events.Skip((frame.snapshot.Events.Length+1)/2).ToArray();continue;}
                if(frame.corpses!=null&&frame.corpses.Length>0){frame.corpses=frame.corpses.Skip((frame.corpses.Length+1)/2).ToArray();continue;}
                if(frame.effects.Length>0){frame.effects=frame.effects.Take(frame.effects.Length/2).ToArray();continue;}
                if(frame.snapshot.Shots.Length>0){frame.snapshot.Shots=frame.snapshot.Shots.Take(frame.snapshot.Shots.Length/2).ToArray();continue;}
                // A future authority schema expansion must not terminate every peer or escape Update.
                return null;
            }
        }
    }
}
