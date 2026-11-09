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
        string arena=R("Runtime/NativeArena.cs"),guard=R("Runtime/NativeGuard.cs"),game=R("Runtime/ConversionGame.cs"),net=R("Runtime/NativeNetworkFrame.cs"),pve=R("Runtime/NativePve.cs"),content=R("Core/ConversionContent.cs"),melee=R("Runtime/NativeMelee.cs");
        Check(arena.Contains("(p.Input.Block||p.Input.Parry&&p.Input.Interact&&!FallenGiveInReach(p)))TryBlock(p);")&&arena.Contains("if(p.Hp>0&&p.Seat.connected)StepGuard(p);"),"F parries unless an interaction took it; the guard each tick");
        var fallenAuth=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeFallenAuthority.cs"));
        Check(fallenAuth.Contains("var f=ZoneBoss?.fallen;if(f==null||q==null||!f.hush.Any(h=>h.id==q.Seat.id&&h.lives>1))return false;")&&fallenAuth.Contains("p.Hp<=0&&Vector3.Distance(q.Body.position,p.Body.position)<=3)return true;")&&fallenAuth.Contains("q.Input.InteractHeld&&Vector3.Distance(q.Body.position,p.Body.position)<=3&&f.hush.Any(h=>h.id==q.Seat.id&&h.lives>1)"),"in NOCTIEL's fight F parries unless it would give a life (a hushed teammate out of lives within 3 m, a life to spare)");
        Check(arena.Contains("if(!forced)amount=GuardDamage(target,source,amount,direction,hitPoint);"),"the guard in the damage, after the parry");
        Check(arena.Contains("a.Guarding=p.Guarding;a.GuardAt=p.GuardAt;")&&arena.Contains("bool sprint=i.Sprint&&!p.Guarding&&")&&arena.Contains("*(p.Guarding?NativeMeleeRules.GuardMove:1)"),"the guard on the actor; no sprint and a slower walk while it is up");
        Check(melee.Contains("||p.Hp<=0||p.GuardedAt==Time||")&&arena.Contains("if(target.GuardedAt!=Time||target.Hp<=0){Wound("),"a hit taken on the guard neither staggers nor wounds");
        Check(guard.Contains("at,blocked,\"hold\");")&&guard.Contains("\"guard-break\"")&&guard.Contains("target.Stamina-=cost;"),"blocked hits cost stamina and are events; a break too");
        Check(game.Contains("pending.Parry|=Input.GetKeyDown(KeyCode.F)")&&game.Contains("pending.Guard=NativeMeleeRules.Guards(p.Weapon?.@class)&&(Input.GetMouseButton(1)")
            &&game.Contains("bool aiming=ads!=null&&p.Weapon?.@class==\"ranged\"&&(Input.GetMouseButton(1)")&&game.Contains("pending.Block|=PadDown(PadAction.Block);"),"the keys: F parry, right click aim or guard (a pad's block button parries and guards)");
        Check(net.Contains("input.Parry|=taps.Parry;")&&pve.Contains("Pve.Content.machines.Any(x=>Flat(Vec(x.front.Position()))<=2.2f))p.Input.Interact=false;"),"a remote parry press is kept; a PvE revive or machine in reach takes F");
        Check(content.Contains("public ContentAds Ads(string spec) {if(ads!=null)foreach(var x in ads)if(x.spec==spec)return x;return null;}"),"every gun with an aim row aims");
        // The look (owner 2026-11-09): no shield on the held guard, the block pose alone; the parry a dome of hexagon cells round the body.
        var blockFx=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeBlockFxPresentation.cs"));
        Check(!File.Exists(Path.Combine(root,"Assets/ChipArena/Runtime/NativeGuardShieldPresentation.cs"))&&!File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeGuardShield.shader"))&&!blockFx.Contains("RenderGuardShields"),"the held guard shows no shield");
        Check(blockFx.Contains("ObserveBlocks(s,time);RenderParryBarriers(s,time);")&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeParryBarrier.shader")),"the parry draws its barrier with the block effects");
        foreach(int f in new[]{NativeParryBarrier.Low,NativeParryBarrier.Full})
        {
            var cells=NativeParryBarrier.Cells(f);int five=0,six=0;
            foreach(var c in cells)
            {
                if(c.Corners.Length==5)five++;else if(c.Corners.Length==6)six++;
                Check(Math.Abs(c.Middle.Length()-1)<1e-4f&&c.Seed>=0&&c.Seed<1,"a cell's middle on the sphere, its seed in range");
                float near=float.MaxValue,far=0;for(int k=0;k<c.Corners.Length;k++){var e=c.Corners[k]-c.Corners[(k+1)%c.Corners.Length];near=Math.Min(near,e.Length());far=Math.Max(far,e.Length());}
                Check(far<near*2.2f,"a cell's corners go round it in order (frequency "+f+")");
            }
            Check(cells.Length==10*f*f+2&&five==12&&six==cells.Length-12,"frequency "+f+": "+cells.Length+" cells, twelve pentagons and the rest hexagons");
            var mesh=NativeParryBarrier.Build(f);Check(mesh.Cells==cells.Length&&mesh.P.Count==mesh.Uv.Count&&mesh.P.Count==mesh.Middle.Count&&mesh.T.Count==(five*5+six*6)*3,"its mesh: a fan for every cell");
        }
        var front=new System.Numerics.Vector3(0,0,1);
        Check(NativeParryBarrier.Delay(front,front,0)==0&&NativeParryBarrier.Delay(-front,front,0)>NativeParryBarrier.Sweep*.99f,"the cells snap in from the pilot's front round to its back");
        Check(NativeParryBarrier.In(0,0)==0&&NativeParryBarrier.In(NativeParryBarrier.Snap*.8f,0)>1&&NativeParryBarrier.In(NativeParryBarrier.Snap,0)==1,"each cell snaps in with an overshoot");
        Check(NativeParryBarrier.Gone(-1,.5f)==0&&NativeParryBarrier.Gone(NativeParryBarrier.After+.001f,1)==1&&NativeParryBarrier.Gone(.05f,0)>0,"and bursts away after the window shuts");
        var barrier=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeParryBarrier.shader"));
        Check(barrier.Contains("float delay=(1-dot(c,normalize(_Facing.xyz)))*.5*.13+seed*.035;float s=snapIn((_Age-delay)/.1);")&&barrier.Contains("saturate((_Closed-seed*.06)/.2)")&&NativeParryBarrier.Sweep==.13f&&NativeParryBarrier.Scatter==.035f&&NativeParryBarrier.Snap==.1f&&NativeParryBarrier.Burst==.2f&&NativeParryBarrier.BurstScatter==.06f,"the shader's moments are the Core's");
        // A hologram (owner 2026-11-09: "too much glow and we can't actually see the player in there or the shape of hexagons"): thin
        // outlines over a clear face, a fresnel toward the silhouette, a glow where it cuts into the scene; the body's own hologram unfilled.
        var holo=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeParryHolo.shader"));
        Check(barrier.Contains("float aa=max(fwidth(edge),1e-4),width=max(.05,aa*1.35);")&&barrier.Contains("float rim=pow(1-abs(dot(n,view)),3);")&&barrier.Contains("float fill=.004+.045*rim")
            &&barrier.Contains("SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture")&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeParryBarrierPresentation.cs")).Contains("parryBarrierBlock.SetFloat(BarrierSoftId,soft);")
            &&holo.Contains("float glow=rim*.42+")&&!holo.Contains("float glow=.025+"),"the barrier is a hologram: thin cell outlines over a clear face, brighter to its silhouette, a glow where it meets the scene; the body shows through");
        Console.WriteLine("PASS guard: "+n+" checks (F parries unless an interaction takes it; right click aims a gun or guards a melee weapon; the guard's arc, share, stamina, break and walk; no guard shield; the parry's holographic hexagon barrier)");
    }
}
