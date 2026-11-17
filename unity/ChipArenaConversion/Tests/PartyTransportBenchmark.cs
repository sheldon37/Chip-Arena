using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ChipArena.UnityConversion;

// Fixed workload isolates packet fan-out and coalescing from Unity rendering and physics.
internal static class PartyTransportBenchmark
{
    public static async Task Run()
    {
        foreach (int party in new[] { 2, 4, 6 })
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var hosts = new List<NativeConnection>();
            var guests = new List<NativeConnection>();
            try
            {
                for (int i = 1; i < party; i++)
                {
                    var accepting = listener.AcceptTcpClientAsync();
                    guests.Add(await NativeConnection.Connect("127.0.0.1",
                        ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None));
                    hosts.Add(new NativeConnection(await accepting));
                }
                string pilots = string.Join(",", Enumerable.Range(1, party).Select(i =>
                    "{\"id\":" + i + ",\"hp\":100,\"x\":" + i + ",\"y\":1,\"z\":10}"));
                for (int pass = 1; pass <= 2; pass++)
                {
                    var elapsed = Stopwatch.StartNew();
                    var arrivals = new List<double>();
                    int bytes = 0;
                    for (int frame = 0; frame < 40; frame++)
                    {
                        string state = "{\"version\":7,\"epoch\":1,\"type\":\"frame\",\"tick\":" + frame + ",\"pilots\":[" + pilots + "]}";
                        var watch = Stopwatch.StartNew();
                        var encoded = NativeConnection.Encode(state);
                        foreach (var host in hosts) host.SendEncoded(encoded, true);
                        bytes += (encoded.Length + 8) * hosts.Count;
                        foreach (var guest in guests)
                        {
                            string message;
                            while (!guest.TryReceive(out message))
                            {
                                if (guest.Closed || watch.ElapsedMilliseconds > 2000) throw new Exception("Guest missed frame " + frame);
                                await Task.Delay(1);
                            }
                            if (message != state) throw new Exception("Mismatched snapshot");
                        }
                        arrivals.Add(watch.Elapsed.TotalMilliseconds);
                        int wait = (frame + 1) * 50 - (int)elapsed.ElapsedMilliseconds;
                        if (wait > 0) await Task.Delay(wait);
                    }
                    arrivals.Sort();
                    Console.WriteLine($"LOOPBACK party={party} pass={pass} frames=40 fanoutP95Ms={arrivals[37]:F2} worstMs={arrivals[39]:F2} sentKiBps={bytes / 1024d / elapsed.Elapsed.TotalSeconds:F2}");
                }
            }
            finally
            {
                foreach (var connection in guests.Concat(hosts)) connection.Dispose();
                listener.Stop();
            }
        }
    }
}
