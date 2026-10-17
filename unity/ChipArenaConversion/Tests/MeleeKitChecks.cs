using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class MeleeKitChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Melee kit: "+why);}
    public static void Run(ConversionContent data)
    {
        KunaiAim();
        var kits=data.abilities.Where(a=>NativeMeleeRules.Standard(a.spec)).ToArray();
        Check(kits.Length==8&&kits.Select(a=>a.spec+":"+a.slot).Distinct().Count()==8,"eight unique Q/E entries");
        foreach(var a in kits)
        {
            Check(a.live&&a.fx==a.id&&a.bot!=null&&a.bot.max>a.bot.min&&a.cost>0,"content/bot contract "+a.id);
            Check(a.damage<=(a.slot=="q"?30:60),"base damage budget "+a.id);
            foreach(float rate in new[]{.45f,1,2,3,8})
            {
                var stats=NativeContentRules.Stats(data,a.spec,new Dictionary<string,int>());stats["attackRate"]=rate;
                Check(NativeMeleeKitRules.Windup(a,stats)>=.15f,"telegraph floor with attack-rate chips "+a.id);
            }
        }
        Check(data.Ability("shield","q").damage+10<=30&&data.Ability("shield","e").damage*3<=60,"shield combined budgets");
        Check(data.Ability("sword","e").damage+22<=60&&data.Ability("daggers","e").damage+28<=60,"recast budgets");
        foreach(string spec in new[]{"sword","daggers","shield","greatsword"})
        {
            var stats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>());
            var swing=NativeMeleeFeel.Begin(data.Weapon(spec),stats,10,1,.75f);
            Check(NativeMeleeKitRules.BufferOpen(swing,swing.HitAt),"hit-window opening accepts edge");
            Check(NativeMeleeKitRules.BufferOpen(swing,swing.HitUntil-.001f),"late active queue");
            Check(!NativeMeleeKitRules.ChainReady(swing,swing.HitUntil),"chain must not cut active frames");
            Check(NativeMeleeKitRules.ChainReady(swing,NativeMeleeKitRules.ChainAt(swing))&&NativeMeleeKitRules.ChainAt(swing)<swing.Until,"mid-recovery chain");
            Check(!NativeMeleeKitRules.BufferOpen(swing,swing.Until),"expired buffer window");
            swing.Remaining=1;Check(!NativeMeleeKitRules.ChainReady(swing,swing.Until-.001f),"do not cancel extra strikes");
            swing.Remaining=0;swing.Followup=true;Check(NativeMeleeKitRules.ChainReady(swing,swing.Until-.001f),"chain after final extra strike");
            V3 travelled=new V3();for(float at=swing.At;at<swing.Until;at+=1f/120)travelled+=NativeMeleeRules.Step(swing,at,1f/120)*(1f/120);
            Check(Math.Abs(travelled.Length-swing.Distance)<.002f&&Math.Abs(Math.Atan2(travelled.x,travelled.z)-swing.Yaw)<.001,"committed authoritative step "+spec);
        }
        var kit=new NativeMeleeKit{Kind="wolf-knight",Phase="active",Release=2,Until=5,SuperArmour=true,ShieldChain=new[]{2,3,4},MarkTarget=7,MarkUntil=6,RecastUntil=6,Absorbed=3,FissureDirection=new V3(0,0,1),FissureSpeed=25};
        Check(NativeMeleeKitRules.LightArmour(kit,3,24.99f)&&!NativeMeleeKitRules.LightArmour(kit,3,25)&&!NativeMeleeKitRules.LightArmour(kit,1,10)&&!NativeMeleeKitRules.LightArmour(kit,5,10),"armour threshold and phase");
        var actor=new Actor{MeleeKit=kit};var copy=actor.ViewCopy();kit.ShieldChain[0]=99;kit.MarkTarget=99;
        Check(copy.MeleeKit.MarkTarget==7&&copy.MeleeKit.ShieldChain[0]==2,"killcam deep copy");
        var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var wire=JsonSerializer.Deserialize<Actor>(JsonSerializer.Serialize(copy,options),options);
        Check(wire.MeleeKit.Valid()&&wire.MeleeKit.ShieldChain.SequenceEqual(new[]{2,3,4})&&wire.MeleeKit.FissureSpeed==25&&wire.MeleeKit.RecastUntil==6,"managed snapshot fields");
        wire.MeleeKit.FissureUntil=float.NaN;Check(!wire.MeleeKit.Valid(),"reject nonfinite phase");wire.MeleeKit.FissureUntil=0;wire.MeleeKit.ShieldChain=new int[4];Check(!wire.MeleeKit.Valid(),"bound shield chain");
        Func<string,bool> allowed=s=>NativeIntroRules.Procedural.Contains(s);
        var pending=NativePendingEffect.Remember(null,"a","orbital-drop","neon-drop",allowed);
        Check(NativePendingEffect.Restore(pending,"a","neon-drop",allowed)=="orbital-drop","old service fallback");
        Check(NativePendingEffect.Restore(pending,"a","neon-drop",allowed)=="orbital-drop","upgrade preserves pre-service pending choice until acknowledged");
        Check(NativePendingEffect.Restore(pending,"a","blackout",allowed)=="blackout"&&pending[0].account==null,"new service wins across devices and clears fallback");
        Check(NativePendingEffect.Restore(pending,"b","hero-landing",allowed)=="hero-landing","account isolation");
        pending=NativePendingEffect.Remember(null,"a","overclock","hero-landing",allowed);
        Check(NativePendingEffect.Restore(pending,"a","hero-landing",allowed)=="overclock","failed native-aware save keeps local choice");
        Check(NativePendingEffect.Restore(pending,"a","overclock",allowed)=="overclock"&&pending[0].account==null,"acknowledged native save clears fallback");
        Console.WriteLine("PASS melee kit: eight ability/bot entries, combined budgets, chip telegraph floor, buffer/chain boundaries, authoritative steps, armour threshold, snapshot isolation/validation and account fallback migration");
    }
    private static void KunaiAim()
    {
        int cases=0;var origin=new V3(2,3,4);
        foreach(float speed in new[]{30,45,60})foreach(float gravity in new[]{0,9,18,27})
        foreach(float range in new[]{3,8,16,22})foreach(float height in new[]{-6,0,6})foreach(float yaw in new[]{0,90,210})
        {
            var target=origin+V3.Forward(yaw)*range+new V3(0,height,0);
            var direction=NativeMeleeKitRules.KunaiAim(origin,target,speed,gravity);var velocity=direction*speed;
            float travel=range/new V3(velocity.x,0,velocity.z).Length;var position=origin;
            for(float left=travel;left>.000001f;){float dt=Math.Min(left,Rules.Dt);position+=NativeSpecialRules.Ballistic(ref velocity,gravity,dt);left-=dt;}
            Check((position-target).Length<.003f,"locked kunai trajectory at height "+height+", range "+range+", speed "+speed+", gravity "+gravity);
            Check(Math.Abs(direction.Length-1)<.0001f,"lock preserves projectile speed");cases++;
        }
        var old=GunRules.Direction(0,-3.438f)*30;float at=10/old.z;
        float miss=Math.Abs(old.y*at-9*at*at+.1f);
        Check(miss>1.4f,"reproduce camera-downward aim plus gravity missing same-height body");
        var fallback=NativeMeleeKitRules.KunaiAim(new V3(),new V3(0,20,24),5,18);
        Check((fallback-new V3(0,20,24).Normalized).Length<.0001f,"unreachable target uses true direction without artificial range");
        Check(NativeMeleeKitRules.KunaiAim(new V3(),new V3(0,5,0),30,18).y==1&&NativeMeleeKitRules.KunaiAim(new V3(),new V3(),30,18).Length==0,"vertical and coincident aim are finite");
        Console.WriteLine("PASS locked Kunai aim: downward-camera miss reproduced; "+cases+" integrated flights hit low/level/high targets with speed/gravity modifiers; finite vertical/unreachable fallback");
    }
}
