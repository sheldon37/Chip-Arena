using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private readonly NativeEndFlow endFlow=new NativeEndFlow();private int guestVotes,guestRequired;private float guestReturnUntil=-1;
        public bool HostLost=>arena?.Replica==true&&server==null;
        public int RematchVotes=>server!=null?guestVotes:endFlow.Votes;
        public int RematchRequired=>server!=null?guestRequired:endFlow.Required;
        public float LobbyReturnSeconds=>Math.Max(0,server!=null?guestReturnUntil-Time.unscaledTime:(float)endFlow.ReturnAt-Time.unscaledTime);
        public void VoteRematch()
        {
            if(HostLost){ReturnMainMenu();return;}if(match?.Phase!=NativePhase.Match)return;
            if(server!=null)SendServer(new NativeNetMessage{type="rematch"});else{endFlow.Reconcile(match.Seats.Where(s=>!s.bot&&s.connected).Select(s=>s.id));endFlow.Vote(1);}
        }
        public void RequestLobbyReturn(){if(server!=null||arena==null)return;endFlow.RequestReturn(Time.unscaledTime);}
        public void CancelLobbyReturn(){if(server==null)endFlow.CancelReturn();}
        public void ReturnMainMenu()=>LeaveRoom();
        private void EndFlowTick()
        {
            if(arena==null||arena.Replica)return;
            if(endFlow.Due(Time.unscaledTime)){endFlow.Reset();ReturnToLobby();return;}
            if(match.Phase!=NativePhase.Match)return;
            endFlow.Reconcile(match.Seats.Where(s=>!s.bot&&s.connected).Select(s=>s.id));
            if(endFlow.Unanimous){foreach(var p in peers)if(p.Seat!=null)p.Ready=true;StartMatch();}
        }
    }
}
