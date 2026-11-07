using System;
using System.IO;
using ChipArena.UnityConversion;
internal static class HulkHuntChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception("Hulk hunt: "+why);}
        bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
        foreach(float start in new[]{10f,430f,10000f})
        {
            var wind=new NativeHulkHunt();wind.BeginCharge(start,1,new V3(),new V3(0,0,50));
            for(int tick=1;tick<=56&&!wind.ChargeLocked;tick++)
            {wind.LockCharge(start+tick/60f);if(tick<=53)Check(!wind.ChargeLocked,"wind-up cannot lock early at "+start);}
            Check(wind.ChargeLocked&&wind.Charging,"fixed ticks reach wind-up lock at "+start);
        }
        var h=new NativeHulkHunt();Check(!h.Charging&&h.ChargeAt==-100&&h.ChargeEndAt==-100,"idle sentinel");
        h.BeginCharge(10,3,new V3(0,1.53f,0),new V3(0,.9f,50));
        Check(h.Charging&&h.ChargeTarget==3&&!h.ChargeLocked,"start target");
        h.TrackCharge(new V3(10,.9f,50));h.LockCharge(10.8f);Check(!h.ChargeLocked&&h.ChargeHeading.x>0,"tracking before lock");
        h.LockCharge(10.9f);var locked=h.ChargeHeading;var point=h.ChargePoint;
        h.TrackCharge(new V3(-20,.9f,0));Check(h.ChargeLocked&&(locked-h.ChargeHeading).Length==0&&(point-h.ChargePoint).Length==0,"heading and point fixed");
        Check(!h.CancelCharge(11,500,1600),"running immune to stagger cancellation");
        Check(!h.Spent(h.ChargeOrigin+locked*50,20),"cross-map run beyond old 2.4 seconds");
        Check(h.Spent(h.ChargeOrigin+locked*60,20),"four metre overshoot end");
        Check(h.Spent(h.ChargeOrigin,26.9f),"sixteen second run cap");
        foreach(int how in new[]{1,2,3}){h.EndCharge(30,how);Check(!h.Charging&&h.ChargeEnd==how&&h.ChargeEndAt==30&&h.ChargeReadyAt==40,"end and cooldown "+how);}
        h.BeginCharge(50,2,new V3(),new V3(0,0,70));Check(!h.CancelCharge(50.2f,399,1600),"subthreshold hit");Check(h.CancelCharge(50.3f,400,1600)&&h.ChargeAt==-100&&Near(h.ChargeReadyAt,54.3f),"25 percent cancels with four seconds");
        for(int z=7;z<=80;z++)
        {
            var from=new V3(0,1.53f,z-2);var to=new V3(0,1.53f,z+2);
            Check(NativeHulkHunt.Sweep(from,to,new V3(0,.9f,z),.95f+.45f,out var f)&&f>0&&f<1,"swept contact "+z);
            Check(!NativeHulkHunt.Sweep(from,to,new V3(1.5f,.9f,z),.95f+.45f,out _),"1.5 m sidestep missed "+z);
        }
        Check(!NativeHulkHunt.Sweep(new V3(),new V3(0,0,5),new V3(0,2,2),1.4f,out _),"strict height boundary");
        Check(NativeHulkHunt.Sweep(new V3(),new V3(),new V3(.1f,0,0),1.4f,out var zero)&&zero==0,"initial overlap");
        var map=new ContentMap{bounds=new MapBounds{minX=-40,maxX=40,minZ=-40,maxZ=40}};
        foreach(string id in new[]{"shell-wall-a","bounds-wall-a","floor-a","ceiling-a","stair-a","ramp-a","hazard-a","door-frame-a","machine-a"})
            Check(NativeHulkHunt.Protected(map,new ContentBlock{id=id,pos=new V3(),size=new V3(1,3,1)}),"protected "+id);
        Check(NativeHulkHunt.Protected(map,new ContentBlock{id="interior-wall",pos=new V3(39.5f,2,0),size=new V3(1,4,8)}),"boundary extent");
        Check(!NativeHulkHunt.Protected(map,new ContentBlock{id="interior-wall",pos=new V3(),size=new V3(1,4,8)}),"interior extent");
        Check(NativeStructureRules.DamageScale("structural","hulk-charge",1)==1&&NativeStructureRules.DamageScale("structural","bullet",1)==0,"charge structure damage does not loosen bullets");
        Check(NativeHulkMoves.Choose(3.4f,20,1,false,0)==NativeHulkMoves.Move.Punch&&NativeHulkMoves.Choose(3.2f,75,1,false,0)==NativeHulkMoves.Move.Swipe,"inclusive choice boundaries");
        Check(NativeHulkMoves.Choose(2,0,2,false,0)==NativeHulkMoves.Move.Content,"crowd smash");
        Check(Near(NativeHulkMoves.Strike(NativeHulkMoves.Move.Punch,1.1f,false,0),.62f)&&Near(NativeHulkMoves.Recover(NativeHulkMoves.Move.Swipe,3.4f,false,0),2.4f),"attack clocks");
        var runtime=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeNetworkFrame.cs"));
        foreach(string field in new[]{"Move","ChargeAt","ChargeEndAt","ChargeEnd","ChargeTarget"})Check(runtime.Contains(field+"=mf."),"apply field "+field);
        Check(runtime.Contains("chargeAt=m.Pve?.ChargeAt??-100")&&runtime.Contains("chargeEndAt=m.Pve?.ChargeEndAt??-100"),"capture sentinels");
        Console.WriteLine("PASS Hulk hunt authority ("+n+" assertions: clocks, tracking, immutable lock, cross-map, ends, cooldown, swept hit/sidestep, protected geometry, frame adapters)");
    }
}
