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
}
