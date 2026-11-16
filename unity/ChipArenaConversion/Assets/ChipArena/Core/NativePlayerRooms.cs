using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChipArena.UnityConversion
{
    public enum RoomPrivacy { Private, Friends, Public }
    public enum PlayerRoomState { Lobby, InProgress, Full }

    public sealed class PlayerRoomInfo
    {
        public string Id, Name, Host, Mode, Map, Region;
        public int Players, Capacity, EstimatedPing = -1;
        public RoomPrivacy Privacy;
        public PlayerRoomState State;
        public bool Joinable => State == PlayerRoomState.Lobby && Capacity >= 1 && Capacity <= 8 &&
            Players >= 1 && Players < Capacity;
        public bool Matches(string filter)
        {
            return Joinable && (string.IsNullOrWhiteSpace(filter) ||
                new[]{Name, Host, Mode, Map, Region}.Any(s => s?.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0));
        }
        public static string Label(string value, int limit = 40)
        {
            // Peer-provided labels go into rich-text Unity controls.
            return new string((value ?? "").Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(limit).ToArray()).Trim();
        }
        public static string ModeName(bool cooperative, bool expedition, bool teams, bool deathmatch) =>
            cooperative ? (expedition ? "Expedition" : "Boss Battle") :
            deathmatch ? (teams ? "Team Deathmatch" : "Deathmatch") : (teams ? "Teams" : "Free-for-all");
        public static string Compatibility(string source, string revision)
        {
            using(var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                    "chip-arena-steam-1\n" + NativeConnection.Protocol + "\n" + source + "\n" + revision))).Replace("-", "");
        }
        public static bool LobbyNumber(string text, out ulong id) =>
            ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0 &&
            text == id.ToString(CultureInfo.InvariantCulture);
    }

    public interface IPlayerRoomBackend
    {
        Task<INativeOnlineHost> Create(PlayerRoomInfo room, string compatibility, CancellationToken token);
        Task<NativeConnection> Join(string id, string compatibility, CancellationToken token);
    }
    public interface IPlayerRoomBrowser
    {
        Task<PlayerRoomInfo[]> Browse(string compatibility, CancellationToken token, bool friendsOnly = false);
    }

    public sealed class CloudflareRoomBackend : IPlayerRoomBackend
    {
        private readonly string executable;
        private readonly Func<string,NativeDirectoryEntry> decode;
        public CloudflareRoomBackend(string executable, Func<string,NativeDirectoryEntry> decode)
        { this.executable = executable; this.decode = decode; }
        public async Task<INativeOnlineHost> Create(PlayerRoomInfo room, string compatibility, CancellationToken token) =>
            await NativeCloudflareHost.Start(executable, decode, token).ConfigureAwait(false);
        public Task<NativeConnection> Join(string id, string compatibility, CancellationToken token) =>
            NativeCloudflareHost.Join(id, decode, token);
    }

    // One reader and one writer, supplied by NativeConnection. The carrier guarantees
    // ordered reliable messages. Fragment below Steam's single-message ceiling so the
    // original frame length, compression limits and latest-snapshot policy still apply.
    public interface IOrderedRoomSocket : IDisposable
    {
        byte[] TryRead();
        bool TryWrite(byte[] buffer, int offset, int count);
    }
    public sealed class OrderedRoomStream : Stream
    {
        public const int ChunkSize = 48 * 1024;
        private readonly IOrderedRoomSocket socket;
        private byte[] incoming;
        private int position;
        private int disposed;
        public OrderedRoomStream(IOrderedRoomSocket socket) { this.socket = socket; }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if(count == 0) return 0;
            while(incoming == null)
            {
                Check(token);
                incoming = socket.TryRead();
                if(incoming == null) await Task.Delay(5, token).ConfigureAwait(false);
                else if(incoming.Length == 0 || incoming.Length > ChunkSize) throw new InvalidDataException("Invalid room transport chunk.");
            }
            int size = Math.Min(count, incoming.Length - position);
            Buffer.BlockCopy(incoming, position, buffer, offset, size);
            position += size;
            if(position == incoming.Length) { incoming = null; position = 0; }
            return size;
        }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            var stalled = new System.Diagnostics.Stopwatch();
            while(count > 0)
            {
                Check(token);
                int size = Math.Min(count, ChunkSize);
                if(socket.TryWrite(buffer, offset, size))
                { offset += size; count -= size; stalled.Reset(); }
                else
                {
                    stalled.Start();
                    if(stalled.ElapsedMilliseconds >= 12000) throw new IOException("Room transport send queue stalled.");
                    await Task.Delay(10, token).ConfigureAwait(false);
                }
            }
        }
        private void Check(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(OrderedRoomStream));
        }
        protected override void Dispose(bool disposing)
        { if(disposing && Interlocked.Exchange(ref disposed, 1) == 0) socket.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override void Flush() {}
        public override int Read(byte[] b,int o,int n) => ReadAsync(b,o,n,CancellationToken.None).GetAwaiter().GetResult();
        public override void Write(byte[] b,int o,int n) => WriteAsync(b,o,n,CancellationToken.None).GetAwaiter().GetResult();
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long o,SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long n) => throw new NotSupportedException();
    }
}
