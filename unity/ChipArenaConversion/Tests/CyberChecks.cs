using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class CyberChecks
{
    static void Check(bool b,string m){if(!b)throw new Exception("Cyber Lord: "+m);}
    public static void Run()
    {
        var facing=new V3(0,0,1);Check(NativeCyberRules.Cone(new V3(0,0,2.2f),facing,2.2f,1.4f)&&!NativeCyberRules.Cone(new V3(0,0,2.201f),facing,2.2f,1.4f),"combo range exact");
        Check(!NativeCyberRules.Cone(new V3(2,0,0),facing,2.2f,1.4f),"combo arc excludes side");
        Check(NativeCyberRules.WallCross(new V3(2.5f,2.5f,-2),new V3(2.5f,2.5f,2),new V3(),0,0,out float t)&&t==.5f,"wall exact edge");
        Check(!NativeCyberRules.WallCross(new V3(2.501f,1,-2),new V3(2.501f,1,2),new V3(),0,0,out _)&&!NativeCyberRules.WallCross(new V3(0,2.501f,-2),new V3(0,2.501f,2),new V3(),0,0,out _),"wall not oversized");
        var kit=new NativeCyberKit();float mana=100;NativeCyberRules.Predict(kit,new InputFrame{AbilityQ=true},1,new V3(),ref mana,1,1,1,(a,b)=>b);
        Check(mana==80&&kit.QReady==7&&kit.DashTo.z==7&&Math.Abs(NativeCyberRules.DashPosition(kit,1.1f).z-3.5f)<.001,"dash prediction distance, mana, cooldown");
        NativeCyberRules.Predict(kit,new InputFrame{AbilityQ=true},1.1f,new V3(),ref mana,1,1,1,(a,b)=>b);Check(mana==80,"held dash cannot double-spend");
        var actor=new Actor{Cyber=kit};var copied=actor.ViewCopy();kit.DashTo.z=2;Check(copied.Cyber.DashTo.z==7,"snapshot deep copy");
        var opts=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var decoded=JsonSerializer.Deserialize<NativeCyberKit>(JsonSerializer.Serialize(copied.Cyber,opts),opts);Check(decoded.Valid()&&decoded.DashTo.z==7,"network value roundtrip");decoded.WallUntil=float.NaN;Check(!decoded.Valid(),"malformed clock rejected");
        var interrupted=new NativeCyberKit{Kind="packet-burst",At=10,Release=10.08f,Until=10.25f,PacketAt=10,PacketRelease=10.08f,PacketUntil=10.25f};
        NativeCyberRules.Interrupt(interrupted,10.04f);NativeCyberRules.Interrupt(interrupted,10.2f);
        Check(interrupted.CutAt==10.04f&&interrupted.Until==10.04f&&interrupted.Resolved&&interrupted.PacketCutAt==10.04f,"first interrupt is stable and cancels pending release");
        mana=100;NativeCyberRules.Predict(interrupted,new InputFrame{AbilityQ=true},11,new V3(),ref mana,1,1,1,(a,b)=>b);
        Check(interrupted.CutAt==-1&&interrupted.PacketAt==10&&interrupted.PacketUntil==10.04f,"next cast clears only its own interrupt and retains packet history");
        var wire=JsonSerializer.Deserialize<NativeCyberKit>(JsonSerializer.Serialize(interrupted,opts),opts);
        Check(wire.Valid()&&wire.PacketCutAt==10.04f,"packet interrupt survives wire");wire.CutAt=float.NaN;Check(!wire.Valid(),"invalid interrupt rejected");
        var complete=new NativeCyberKit{Kind="firewall",At=1,Until=1.2f};NativeCyberRules.Interrupt(complete,2);Check(complete.Until==1.2f&&complete.CutAt==-1,"later block leaves completed cast alone");
        Check(CyberLordFeed.SiphonHeal(EventKind.Heal,"siphon-link",1,1,10,0,0),"tagged siphon heal does not require paired hit event");
        var user=new NativeAccess();typeof(NativeAccess).GetProperty("Expires").SetValue(user,1000d);((HashSet<string>)typeof(NativeAccess).GetField("grants",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(user)).Add("weapon:cyberlord");Check(!user.Allows("weapon:cyberlord",1),"grant never unlocks Cyber Lord");
        typeof(NativeAccess).GetProperty("Developer").SetValue(user,true);Check(user.Allows("weapon:cyberlord",1),"live DEV allows");var scoped=NativeAccess.ScopedWeapon(user,"cyberlord",1);Check(scoped.DeveloperWeapon("cyberlord",1)&&!scoped.DeveloperWeapon("siren",1)&&!scoped.DeveloperWeapon("cyberlord",1001),"sandbox scope exact and expires");
        Console.WriteLine("PASS Cyber Lord: exact cone/wall bounds, dash prediction/spend, deep snapshot, wire validity and DEV-only scope");
    }
}
