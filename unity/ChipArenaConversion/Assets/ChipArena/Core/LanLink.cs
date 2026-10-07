using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ChipArena.UnityConversion
{
    // Two-player trusted LAN diagnostic. No discovery, accounts, external services, encryption or NAT traversal.
    // All socket operations run off Unity's main thread; bounded queues prevent slow peers growing memory.
    public sealed class LanLink : IDisposable
    {
        private readonly object gate = new object();
        private readonly Queue<byte[]> incoming = new Queue<byte[]>(), outgoing = new Queue<byte[]>();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private TcpListener listener;
        private TcpClient peer;
        private Thread connector, reader, writer;
        private byte[] latestState;
        private volatile bool stopping, connected;
        private string status = "Offline";
        private readonly bool host;
        public bool Connected { get { return connected; } }
        public string Status { get { lock (gate) return status; } }
        public long BytesReceived { get; private set; }
        public long BytesSent { get; private set; }
        private LanLink(bool host) { this.host = host; }
        public static LanLink Host(int port, bool loopbackOnly = false)
        {
            var link = new LanLink(true);
            link.listener = new TcpListener(loopbackOnly ? IPAddress.Loopback : IPAddress.Any, port);
            link.listener.Start(1); link.status = "Listening on port " + port;
            link.connector = link.StartThread(() =>
            {
                try { var client = link.listener.AcceptTcpClient(); link.listener.Stop(); if (link.stopping) client.Close(); else link.Attach(client); }
                catch (Exception e) { if (!link.stopping) link.Fail(e.Message); }
            });
            return link;
        }
        public static LanLink Join(string address, int port)
        {
            IPAddress parsed; if (!IPAddress.TryParse(address, out parsed) || parsed.AddressFamily != AddressFamily.InterNetwork) throw new ArgumentException("Enter an IPv4 address");
            var link = new LanLink(false); link.status = "Connecting...";
            link.peer = new TcpClient(AddressFamily.InterNetwork);
            link.connector = link.StartThread(() =>
            {
                try
                {
                    var task = link.peer.ConnectAsync(parsed, port);
                    if (!task.Wait(5000)) throw new TimeoutException("Connection timed out");
                    if (!link.stopping) link.Attach(link.peer);
                }
                catch (Exception e) { if (!link.stopping) link.Fail(e.GetBaseException().Message); }
            });
            return link;
        }
        private Thread StartThread(ThreadStart action) { var t = new Thread(action) { IsBackground = true }; t.Start(); return t; }
        private void Attach(TcpClient client)
        {
            lock (gate)
            {
                if (stopping) { client.Close(); return; }
                peer = client; peer.NoDelay = true; peer.ReceiveTimeout = 10000; peer.SendTimeout = 3000;
                connected = true; status = "Connected";
            }
            reader = StartThread(ReadLoop); writer = StartThread(WriteLoop);
        }
        public void Send(byte[] packet)
        {
            if (packet == null || packet.Length == 0 || packet.Length > WireCodec.MaxPacket) throw new ArgumentException("Invalid packet");
            if (!connected) return;
            lock (gate)
            {
                if (host) latestState = packet;
                else { if (outgoing.Count >= 120) { Fail("Peer is not consuming inputs"); return; } outgoing.Enqueue(packet); }
            }
            wake.Set();
        }
        public byte[] Receive()
        {
            lock (gate) return incoming.Count > 0 ? incoming.Dequeue() : null;
        }
        private void ReadLoop()
        {
            try
            {
                NetworkStream stream = peer.GetStream(); byte[] header = new byte[4];
                while (!stopping && connected)
                {
                    ReadExact(stream, header); int length = header[0] | header[1] << 8 | header[2] << 16 | header[3] << 24;
                    if (length <= 0 || length > WireCodec.MaxPacket) throw new InvalidDataException("Invalid frame length");
                    byte[] packet = new byte[length]; ReadExact(stream, packet); BytesReceived += length + 4;
                    lock (gate)
                    {
                        if (host && incoming.Count >= 120) throw new InvalidDataException("Input queue overflow");
                        if (!host) incoming.Clear(); // Snapshots replace obsolete snapshots; input edges are never dropped.
                        incoming.Enqueue(packet);
                    }
                }
            }
            catch (Exception e) { if (!stopping) Fail(e.Message); }
        }
        private void WriteLoop()
        {
            try
            {
                NetworkStream stream = peer.GetStream();
                while (!stopping && connected)
                {
                    byte[] packet = null;
                    lock (gate)
                    {
                        if (host) { packet = latestState; latestState = null; }
                        else if (outgoing.Count > 0) packet = outgoing.Dequeue();
                    }
                    if (packet == null) { wake.WaitOne(100); continue; }
                    int n = packet.Length; byte[] h = { (byte)n, (byte)(n >> 8), (byte)(n >> 16), (byte)(n >> 24) };
                    stream.Write(h, 0, 4); stream.Write(packet, 0, n); BytesSent += n + 4;
                }
            }
            catch (Exception e) { if (!stopping) Fail(e.Message); }
        }
        private static void ReadExact(Stream stream, byte[] buffer)
        {
            int at = 0;
            while (at < buffer.Length) { int n = stream.Read(buffer, at, buffer.Length - at); if (n == 0) throw new EndOfStreamException("Peer disconnected"); at += n; }
        }
        private void Fail(string message)
        {
            connected = false;
            lock (gate) { status = "Disconnected: " + message; incoming.Clear(); outgoing.Clear(); latestState = null; }
            try { peer?.Close(); } catch (SocketException) { }
            wake.Set();
        }
        public void Dispose()
        {
            if(stopping) return;
            stopping = true; connected = false; listener?.Stop(); peer?.Close(); wake.Set();
            foreach (Thread t in new[] { connector, reader, writer }) if (t != null && t != Thread.CurrentThread) t.Join(1000);
            if((connector==null||!connector.IsAlive)&&(reader==null||!reader.IsAlive)&&(writer==null||!writer.IsAlive)) wake.Dispose();
            // If a connect timeout outlives Dispose, the wait handle is released by its normal finalizer.
            lock (gate) { incoming.Clear(); outgoing.Clear(); latestState = null; status = "Offline"; }
        }
    }
}
