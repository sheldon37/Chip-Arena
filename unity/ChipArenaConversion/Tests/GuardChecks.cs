using System;
using System.IO;
using ChipArena.UnityConversion;
// The controls (owner 2026-11-09): "The shield/parry will be moved to F key, right click will now "aim" for ranged weapons (zoom in and
// give shots less spread) while it will be a held block for melee." "The block for melee will essentially reduce damage by a huge
// portion as long as user is facing where the damage is coming from. Stamina however will be used on blocked hits." F parries unless an
// interaction in reach takes the press (the owner's pick). The guard's arc, share, stamina, break and walk; the keys; the wiring.
internal static class GuardChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("Guard: "+why);n++;}
        float S(double d)=>(float)Math.Sin(d*Math.PI/180);float C(double d)=>(float)Math.Cos(d*Math.PI/180);
        Check(NativeMeleeRules.GuardFaces(0,1,0,1),"a hit from straight ahead is guarded");
        Check(NativeMeleeRules.GuardFaces(0,1,S(70),C(70))&&NativeMeleeRules.GuardFaces(0,1,-S(70),C(70)),"70 degrees off either side is covered");
        Check(!NativeMeleeRules.GuardFaces(0,1,S(80),C(80))&&!NativeMeleeRules.GuardFaces(0,1,0,-1)&&!NativeMeleeRules.GuardFaces(0,1,1,0),"80 degrees off, the side and behind are not");
        Check(NativeMeleeRules.GuardFaces(3,0,10,.5f),"facing and direction need not be unit length");
        Check(!NativeMeleeRules.GuardFaces(0,0,0,1)&&!NativeMeleeRules.GuardFaces(0,1,0,0),"no facing or no direction, no guard");
        Check(NativeMeleeRules.Guards("melee")&&!NativeMeleeRules.Guards("ranged")&&!NativeMeleeRules.Guards("magic")&&!NativeMeleeRules.Guards(null),"melee weapons guard; guns and staves do not");
        Check(NativeMeleeRules.GuardShare>=.7f&&NativeMeleeRules.GuardShare<1&&NativeMeleeRules.GuardStamina>0&&NativeMeleeRules.GuardBreak>0&&NativeMeleeRules.GuardMove<1&&NativeMeleeRules.GuardMove>.3f,"a huge share stopped, paid in stamina, a break, a slower walk");
        var held=new InputFrame{Guard=true,Parry=true,Block=true}.HeldOnly();Check(held.Guard&&!held.Parry&&!held.Block,"the guard is held, the parry a press");
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        string arena=R("Runtime/NativeArena.cs"),guard=R("Runtime/NativeGuard.cs"),game=R("Runtime/ConversionGame.cs"),net=R("Runtime/NativeNetworkFrame.cs"),pve=R("Runtime/NativePve.cs"),content=R("Core/ConversionContent.cs");
        Check(arena.Contains("(p.Input.Block||p.Input.Parry&&p.Input.Interact&&ZoneBoss?.fallen==null))TryBlock(p);")&&arena.Contains("if(p.Hp>0&&p.Seat.connected)StepGuard(p);"),"F parries unless an interaction took it; the guard each tick");
        Check(arena.Contains("if(!forced)amount=GuardDamage(target,source,amount,direction,hitPoint);"),"the guard in the damage, after the parry");
        Check(arena.Contains("a.Guarding=p.Guarding;a.GuardAt=p.GuardAt;")&&arena.Contains("bool sprint=i.Sprint&&!p.Guarding&&")&&arena.Contains("*(p.Guarding?NativeMeleeRules.GuardMove:1)"),"the guard on the actor; no sprint and a slower walk while it is up");
        Check(guard.Contains("Emit(EventKind.Block,target,0,at,blocked,\"hold\")")&&guard.Contains("\"guard-break\"")&&guard.Contains("target.Stamina-=cost;"),"blocked hits cost stamina and are events; a break too");
        Check(game.Contains("pending.Parry|=Input.GetKeyDown(KeyCode.F)")&&game.Contains("pending.Guard=NativeMeleeRules.Guards(p.Weapon?.@class)&&(Input.GetMouseButton(1)")
            &&game.Contains("bool aiming=ads!=null&&p.Weapon?.@class==\"ranged\"&&(Input.GetMouseButton(1)")&&!game.Contains("pending.Block|=(Input.GetMouseButtonDown(1)"),"the keys: F parry, right click aim or guard");
        Check(net.Contains("input.Parry|=taps.Parry;")&&pve.Contains("Pve.Content.machines.Any(x=>Flat(Vec(x.front.Position()))<=2.2f))p.Input.Interact=false;"),"a remote parry press is kept; a PvE revive or machine in reach takes F");
        Check(content.Contains("public ContentAds Ads(string spec) {if(ads!=null)foreach(var x in ads)if(x.spec==spec)return x;return null;}"),"every gun with an aim row aims");
        Console.WriteLine("PASS guard: "+n+" checks (F parries unless an interaction takes it; right click aims a gun or guards a melee weapon; the guard's arc, share, stamina, break and walk)");
    }
}
