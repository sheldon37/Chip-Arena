#if CHIP_ARENA_STEAM
using System;
using System.IO;
using System.Runtime.InteropServices;
using Steamworks;

namespace ChipArena.UnityConversion
{
    // The game reader and writer run off Unity's simulation thread. Serialize native IO
    // with close/shutdown so a cancelled read never touches an invalid Steam handle.
    internal sealed class SteamRoomSocket : IOrderedRoomSocket
    {
        internal readonly HSteamNetConnection Handle;
        internal readonly ulong Peer;
        private readonly object gate;
        private readonly IntPtr[] incoming = new IntPtr[1];
        internal bool Closed { get; private set; }
        internal SteamRoomSocket(HSteamNetConnection handle, ulong peer, object gate) { Handle = handle; Peer = peer; this.gate = gate; }

        public byte[] TryRead()
        {
            lock(gate)
            {
                if(Closed) throw new IOException("Steam socket closed.");
                int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(Handle, incoming, 1);
                if(count < 0) throw new IOException("Steam receive failed.");
                if(count == 0) return null;
                try
                {
                    var message = SteamNetworkingMessage_t.FromIntPtr(incoming[0]);
                    if(message.m_cbSize < 1 || message.m_cbSize > OrderedRoomStream.ChunkSize)
                        throw new InvalidDataException("Invalid Steam message size.");
                    var data = new byte[message.m_cbSize];
                    Marshal.Copy(message.m_pData, data, 0, data.Length);
                    return data;
                }
                finally { SteamNetworkingMessage_t.Release(incoming[0]); }
            }
        }
        public bool TryWrite(byte[] data, int offset, int count)
        {
            lock(gate)
            {
                if(Closed) throw new IOException("Steam socket closed.");
                var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    var result = SteamNetworkingSockets.SendMessageToConnection(Handle, IntPtr.Add(pin.AddrOfPinnedObject(),offset),
                        (uint)count, Constants.k_nSteamNetworkingSend_ReliableNoNagle, out _);
                    if(result == EResult.k_EResultLimitExceeded) return false;
                    if(result != EResult.k_EResultOK) throw new IOException("Steam send failed: " + result);
                    return true;
                }
                finally { pin.Free(); }
            }
        }
        public void Dispose()
        {
            lock(gate)
            {
                if(Closed) return;
                Closed = true;
                SteamNetworkingSockets.CloseConnection(Handle, 0, "Room connection ended", false);
            }
        }
    }
}
#endif
