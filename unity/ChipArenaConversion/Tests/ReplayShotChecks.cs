using System;
using System.Linq;
using ChipArena.UnityConversion;

internal static class ReplayShotChecks
{
    private static int checks;
    private static void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Replay shots: "+why);}
    private static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
    public static void Run(ConversionContent data)
    {
        // Reproduce the reported visual overshoot: a shot stops at x=1 at 10.01, between 20 Hz captures.
        var old=new NativeShotTracker();var owner=new object();var view=new NativeShotView{Id=1,X=-5,VX=100};
        old.Begin(owner,1,true,9.95);old.Remember(view);old.Present(ref view,9.95);
        old.Begin(owner,1,true,10);view.X=0;old.Recall(ref view);old.Present(ref view,10.04);
        Check(view.X>3.99,"old replay extrapolates through the x=1 contact");
        var sample=new NativeReplayShots();var a=new Shot{Id=1,Position=new V3(0,0,0),Velocity=new V3(100,0,0)};
        var stop=new NativeShotEnd{Id=1,At=10.01f,Position=new V3(1,0,0)};
        sample.Window(10,10.05,new[]{a},Array.Empty<Shot>(),new[]{stop});
        Check(sample.Sample(1,10.005,out var at,out _)&&Math.Abs(at.x-.5f)<.002f,"approaches the recorded contact");
        Check(!sample.Sample(1,stop.At,out _,out _)&&!sample.Sample(1,10.04,out _,out _),"ends on the contact clock, before the next frame");
        foreach(var weapon in data.weapons.Where(w=>w.@class=="ranged"))
        foreach(float speed in new[]{20f,80f,140f,400f})
        {
            a.Spec=weapon.id;a.Velocity=new V3(speed,0,0);stop.Position=new V3(speed*(stop.At-10),0,0);
            sample.Window(10,10.05,new[]{a},Array.Empty<Shot>(),new[]{stop});
            // Quarter speed and irregular render frames cannot put a bullet beyond the contact plane.
            for(int tick=0;tick<=40;tick++)
            {double cursor=10+tick/60d*.25;bool live=sample.Sample(1,cursor,out at,out _);Check(live?at.x<=stop.Position.x+.0001f:cursor>=stop.At,weapon.id+" stops at impact at speed "+speed);}
        }
        var next=new Shot{Id=1,Position=new V3(4,0,0),Velocity=new V3(-80,0,0)};
        sample.Window(10,10.05,new[]{a},new[]{next},null);
        Check(sample.Sample(1,10.025,out at,out _)&&Near(at.x,2),"surviving pierced/reflected shot interpolates instead of ending");
        Check(sample.Sample(1,11,out at,out _)&&Near(at.x,4),"buffer underrun clamps at the last known position");
        sample.Window(10,10.05,new[]{a},Array.Empty<Shot>(),null);
        Check(sample.Sample(1,10.04,out at,out _)&&Near(at.x,0),"old-host missing endpoint cannot invent flight through a victim");
        sample.Window(11,11.05,new[]{a},new[]{next},new[]{stop});
        Check(sample.Sample(1,11.025,out at,out _)&&Near(at.x,2),"stale endpoint from another window ignored");
        sample.Window(10,10.05,new[]{a},Array.Empty<Shot>(),new[]{stop});
        Check(sample.Sample(1,10.005,out var forward,out _),"sample before end");
        sample.Sample(1,10.04,out _,out _);
        Check(sample.Sample(1,10.005,out at,out _)&&at.x==forward.x,"rewind samples identically without mutating history");
        Check(Near(a.Position.x,0)&&Near(next.Position.x,4),"recorded source frames remain immutable");

        var ledger=new NativeShotEnds();ledger.Record(1,10,new V3(1,2,3));var detached=ledger.Snapshot(10);
        ledger.Record(2,10.1f,new V3(4,5,6));ledger.Clear();
        Check(detached.Length==1&&detached[0].Position.y==2,"captured endpoint array survives live changes and resets");
        ledger.Restore(detached);var guest=ledger.Snapshot(10.2f);
        Check(guest.Length==1&&guest[0].Id==1&&guest[0].At==10,"guest can record the same endpoint into its own replay");
        Check(ledger.Snapshot(11.01f).Length==0,"expired live endpoints are bounded to one second");
        for(int i=1;i<=NativeShotEnds.Max+30;i++)ledger.Record(i,20,new V3(i,0,0));
        Check(ledger.Count==NativeShotEnds.Max&&ledger.Snapshot(20)[0].Id==31,"extreme burst has a hard storage cap");
        Check(NativeShotEnds.Valid(null,10)&&NativeShotEnds.Valid(detached,10),"old host and valid current host accepted");
        Check(!NativeShotEnds.Valid(new[]{detached[0],detached[0]},10),"duplicate identity rejected");
        var bad=detached[0];bad.At=float.NaN;Check(!NativeShotEnds.Valid(new[]{bad},10),"NaN clock rejected");
        bad=detached[0];bad.At=11;Check(!NativeShotEnds.Valid(new[]{bad},10),"future clock rejected");
        bad=detached[0];bad.Position.x=float.PositiveInfinity;Check(!NativeShotEnds.Valid(new[]{bad},10),"infinite position rejected");
        bad=detached[0];bad.Id=0;Check(!NativeShotEnds.Valid(new[]{bad},10),"invalid identity rejected");
        Check(!NativeShotEnds.Valid(new NativeShotEnd[NativeShotEnds.Max+1],10),"oversized wire record array rejected");
        for(int i=0;i<1000;i++)sample.Sample(1,10.005,out _,out _);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)sample.Sample(1,10.005,out _,out _);
        Check(GC.GetAllocatedBytesForCurrentThread()==before,"replay sampling allocates nothing once warm");
        Console.WriteLine("PASS projectile replay ("+checks+" checks): impact endpoints, all ranged weapons, slow motion, surviving shots, old hosts, guest history and bounded records");
    }
}
