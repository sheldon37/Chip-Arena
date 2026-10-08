using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeConnectionReport
    {public int protocol=NativeConnection.Protocol,samples;public double rttMs,rttP95Ms,snapshotGapMs,snapshotGapP95Ms,decodedKiB,largestFrameKiB;public bool connected;}
    public sealed class NativeConnectionMetrics
    {
        private readonly Queue<double> rtts=new Queue<double>(),gaps=new Queue<double>();private readonly Dictionary<int,double> probes=new Dictionary<int,double>();private int serial;private double lastSnapshot=-1;private long bytes,largest;
        public static double Now=>(double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency;
        private static void Add(Queue<double> window,double value){window.Enqueue(value);while(window.Count>128)window.Dequeue();}
        private static double P95(Queue<double> values){if(values.Count==0)return 0;var sorted=values.OrderBy(v=>v).ToArray();return sorted[(int)Math.Ceiling(sorted.Length*.95)-1];}
        public int Ping(double now){if(probes.Count>=8)probes.Remove(probes.OrderBy(p=>p.Value).First().Key);if(serial==int.MaxValue)serial=0;probes[++serial]=now;return serial;}
        public bool Pong(int id,double now){if(!probes.TryGetValue(id,out var sent))return false;probes.Remove(id);double ms=(now-sent)*1000;if(ms<0||ms>12000||double.IsNaN(ms))return false;Add(rtts,ms);return true;}
        public void Received(int count){Interlocked.Add(ref bytes,count);long old;do{old=Interlocked.Read(ref largest);if(old>=count)return;}while(Interlocked.CompareExchange(ref largest,count,old)!=old);}
        public void Snapshot(double now){if(lastSnapshot>=0&&now>=lastSnapshot)Add(gaps,(now-lastSnapshot)*1000);lastSnapshot=now;}
        public NativeConnectionReport Report(bool connected)=>new NativeConnectionReport{connected=connected,samples=rtts.Count,rttMs=rtts.LastOrDefault(),rttP95Ms=P95(rtts),snapshotGapMs=gaps.LastOrDefault(),snapshotGapP95Ms=P95(gaps),decodedKiB=Interlocked.Read(ref bytes)/1024d,largestFrameKiB=Interlocked.Read(ref largest)/1024d};
    }
}
