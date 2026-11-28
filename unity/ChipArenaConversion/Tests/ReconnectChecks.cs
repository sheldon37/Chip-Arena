using System;
using ChipArena.UnityConversion;
internal static class ReconnectChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var w=new NativeReconnectWindow();
        Check(!w.TryAttempt(0,false),"no unsolicited joins");w.Begin(10);
        Check(!w.TryAttempt(10,false)&&!w.TryAttempt(11,true),"delay and single in-flight attempt");
        Check(w.TryAttempt(11,false)&&!w.TryAttempt(12,false)&&w.TryAttempt(13,false),"retry backoff");
        w.Begin(70);Check(w.Expired(85)&&!w.TryAttempt(85,false),"failed attempts cannot extend reservation");
        w.Reset();Check(!w.Active&&!w.Expired(100)&&!w.TryAttempt(100,false),"leave/rejection/success stops retries");
        w.Begin(100);Check(w.TryAttempt(101,false)&&!w.Expired(174)&&w.Expired(175),"new interruption gets its own window");
        Console.WriteLine("PASS reconnect: delay, backoff, in-flight exclusion, fixed deadline and cancellation");
    }
}
