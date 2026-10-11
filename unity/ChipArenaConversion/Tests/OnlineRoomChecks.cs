using System;
using System.Collections.Generic;
using System.Linq;
using ChipArena.UnityConversion;
internal static class OnlineRoomChecks
{
    private static void Check(bool pass,string why){if(!pass)throw new InvalidOperationException(why);}
    public static void Run()
    {
        foreach(var input in new[]{"7KMR4P","7kmr4p"," 7km r4p ","7KM-R4P","CA-7KMR4P"})Check(NativeRoomCodes.RawOnline(input)=="7KMR4P","short room codes accept lowercase, spacing and separators");
        Check(NativeRoomCodes.RawOnline("ca-abcd-2345")=="ABCD2345"&&NativeRoomCodes.Online("ABCDEFGH")=="ABCDEFGH","legacy codes stay pasteable without an added prefix");
        Check(NativeRoomCodes.RawOnline("CA-ABCD")=="CAABCD","short code beginning CA is not mistaken for a legacy prefix");
        foreach(var invalid in new[]{null,"","ABC12","123456","ABC0EF","ABCDEF7","ABCDEF!","AБCDEF",new string('A',65)})Check(!NativeRoomCodes.TryNormalize(invalid,out _),"invalid code rejected before connecting");
        for(int i=0;i<1024;i++){string code=NativeRoomCodes.Create();Check(code.Length==6&&NativeRoomCodes.Valid(code)&&code.All(c=>!"01IO".Contains(c)),"six unambiguous cryptographically sampled characters");}
        var occupied=new HashSet<string>{"ABC234"};var candidates=new Queue<string>(new[]{"ABC234","ABC234","DEF567"});
        Check(NativeRoomCodes.TryReserve(occupied.Add,()=>candidates.Dequeue())&&occupied.SetEquals(new[]{"ABC234","DEF567"}),"collisions retry and never overwrite an existing room");
        int attempts=0;Check(!NativeRoomCodes.TryReserve(_=>false,()=>{attempts++;return "ABC234";})&&attempts==32,"collision retries are bounded");
        string invite=NativeRoomCodes.Invite("7kmr4p");Check(NativeRoomCodes.ParseInvite(invite,out var parsed)&&parsed=="7KMR4P","online invite round trips without exposing a relay address");
        foreach(var bad in new[]{"https://example.org/?code=7KMR4P","chiparena://room?code=7KMR4P&origin=evil","chiparena://user@room?code=7KMR4P","chiparena://room/path?code=7KMR4P","chiparena://room?code=7KMR4P#evil"})Check(!NativeRoomCodes.ParseInvite(bad,out _),"invite cannot select an arbitrary endpoint");
        Check(NativeLanDiscovery.ParseInvite(NativeLanDiscovery.Invite("192.168.1.4",29941,"7KMR4P"),out _,out _,out var local)&&local=="7KMR4P","short LAN invite accepted");
        Console.WriteLine("PASS simple online rooms: 6-character codes, legacy paste, collision retries, LAN/online invites");
    }
}
