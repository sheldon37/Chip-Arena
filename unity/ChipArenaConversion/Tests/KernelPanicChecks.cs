using System;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class KernelPanicChecks
{
    internal static void Run()
    {
        void Check(bool yes,string why){if(!yes)throw new Exception("Kernel Panic: "+why);}
        var s=new NativeKernelPanic{Id=5,Caster=1,Victim=2,Outcome=1,At=10,ConnectedAt=10.25f,Until=14.25f,Ready=40,Seed=713,Facing=new V3(0,0,1),CasterStart=new V3(0,1,0),VictimStart=new V3(0,1,3)};
        s.CasterPoints=new[]{new V3(0,1,0),new V3(0,2,10),new V3(0,8,11),new V3(1,3,11),new V3(0,1,10),new V3(0,1,10)};
        s.VictimPoints=new[]{new V3(0,1,3),new V3(0,2.5f,11),new V3(0,7.5f,11),new V3(0,3,11),new V3(0,1,11),new V3(0,1,11)};
        Check(s.Valid(),"valid resolved choreography");
        var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var copy=JsonSerializer.Deserialize<NativeKernelPanic>(JsonSerializer.Serialize(s,options),options);
        for(int i=0;i<300;i++)
        {
            float t=10+i/60f;NativeKernelPanicRules.Sample(s,t,out var a,out var b);NativeKernelPanicRules.Sample(copy,t,out var c,out var d);
            Check((a-c).Length<.00001f&&(b-d).Length<.00001f,"late join samples same clock without event history");
            if(t<s.ConnectedAt+.6f)Check((a-s.CasterStart).Length==0&&(b-s.VictimStart).Length==0,"cut-in freezes both");
        }
        var shallow=s.ViewCopy();shallow.CasterPoints[0]=new V3(99,0,0);Check(s.CasterPoints[0].x==0,"snapshot points deep copied");
        for(int health=1;health<=100;health++)
        {
            var debt=s.ViewCopy();float hp=health,applied=0;
            for(int step=1;step<=5;step++)for(int j=0;j<(step==4?3:1);j++)
            {
                float damage=NativeKernelPanicRules.HoldLethal(debt,hp,NativeKernelPanicRules.Damage[step-1],step);applied+=damage;hp=Math.Max(0,hp-damage);
                if(step<5)Check(hp>=1,"no pre-finisher death at any starting HP");
            }
            Check(Math.Abs(applied-75)<.001f&&debt.DamageDebt==0,"all 75 damage paid once, including debt");
        }
        Check(!NativeKernelPanicRules.AllowsDamage(s,2,null,3,0),"third party excluded");
        Check(!NativeKernelPanicRules.AllowsDamage(s,1,s,2,1),"caster immune even to victim thorns");
        Check(NativeKernelPanicRules.AllowsDamage(s,2,s,1,1),"sequence hit admitted");
        Check(NativeKernelPanicRules.AllowsDamage(null,3,s,1,0)&&NativeKernelPanicRules.AllowsDamage(null,3,s,1,5),"outgoing effects retain ordinary rules against outsiders");
        s.Next=0;int count=0;while(NativeKernelPanicRules.Due(s,20)){s.Next++;count++;}Check(count==5&&!NativeKernelPanicRules.Due(s,20),"skipped frames catch up steps exactly once");
        var bad=s.ViewCopy();bad.VictimPoints=new V3[7];Check(!bad.Valid(),"reject unbounded waypoint array");bad=s.ViewCopy();bad.Facing.x=float.NaN;Check(!bad.Valid(),"reject nonfinite frame");bad=s.ViewCopy();bad.Outcome=9;Check(!bad.Valid(),"reject unknown outcome");
        s.Outcome=3;Check(!s.Busy(11)&&!s.Active,"early termination releases rule locks");
        Console.WriteLine("PASS Kernel Panic: deterministic late join, cut-in freeze, deep snapshots, damage debt across 100 HP cases, isolation, once-only steps and invalid frames");
    }
}
