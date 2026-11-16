using System;

namespace ChipArena.UnityConversion
{
    internal interface ISteamPlayerRooms : IPlayerRoomBackend, IPlayerRoomBrowser, IDisposable
    {
        string PendingInvite { get; set; }
        string EndReason { get; set; }
        string Diagnostics { get; }
        string HostName { get; }
        string Region { get; }
        void Tick();
        void Leave();
        void Publish(PlayerRoomInfo room);
        bool Invite();
    }
}
