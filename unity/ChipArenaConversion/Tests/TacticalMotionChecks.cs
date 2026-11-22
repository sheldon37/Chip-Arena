#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

/// <summary>
/// Third person on the Tactical Shooter Pack's clips (Core/NativeTacticalMotion, Runtime/NativeTacticalMotionPresentation.cs; owner
/// 2026-11-21; docs/TP-TACTICAL.md). Held without the licensed bank (a synthetic book stands in): a bank without the pack's clips
/// keeps every gun's own set; with them each gun plays its pack clips on the upper body (the aim, the shot, the reload, the carry)
/// over its own set's match idles, strides and jumps; every clip clock sits on the rules (a reload back on the gun at ReloadUntil,
/// the rifle's bolt home on the interval, the raise in the gun's Settle); no rule is written and no pack code or bank is in git.
/// </summary>
internal static class TacticalMotionChecks
{
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new InvalidOperationException("Tactical motion: "+what);}
    private static bool Near(float a,float b,float e=1e-3f)=>Math.Abs(a-b)<=e;
    // The bake's lengths (s) for the pack clips the guns play; any other clip 1.2 s (the game's own sets).
    private sealed class Book:NativeVisualMotion.IClips
    {
        public bool Pack=true;
        public bool Has(string c)=>c!=null&&(Pack||!c.StartsWith("A_FP_",StringComparison.Ordinal));
        public float Length(string c)=>c=="A_FP_WK-11_Viper_Fire"?.667f:c=="A_FP_SRM-12_Fire"?1.667f:c=="A_FP_WK-11_Viper_Reload_Tactical"?3.467f:c=="A_FP_SRM-12_Reload_Tactical"?3.083f
            :c=="A_FP_TR15_Reload_Tactical"?3.483f:c==NativeTacticalMotion.PackWalk?4.05f:c==NativeTacticalMotion.PackSprint?2.7f:c!=null&&c.Contains("Idle_Pose")?.083f:1.2f;
        public float Hit(string c)=>Length(c)*.5f;
    }
    public static void Run(string root)
    {
        count=0;var book=new Book();
        Sets(book);Timing(book);Layers(book);Carry(book);Sources(root);
        Console.WriteLine("PASS tactical motion (third person): "+count+" checks: the old sets without the pack's clips, each gun's pack clips over its own idles and strides, the clips on the rules' clocks (reload, shot, bolt, settle, kick), the carry, presentation only, no pack code or bank in git");
    }
    private static void Sets(Book book)
    {
        var pistol=NativeVisualMotion.For("pistol");var rifle=NativeVisualMotion.For("rifle");
        book.Pack=false;
        Check(NativeVisualMotion.Tactical("pistol",pistol,book)==pistol&&NativeVisualMotion.Tactical("rifle",rifle,book)==rifle&&NativeVisualMotion.Tactical("machinegun",rifle,book)==rifle,"a bank without the pack's clips: every gun keeps its own set");
        book.Pack=true;
        Check(NativeVisualMotion.Tactical("sword",NativeVisualMotion.For("sword"),book)==NativeVisualMotion.For("sword")&&NativeVisualMotion.Tactical("bow",NativeVisualMotion.For("bow"),book)==NativeVisualMotion.For("bow"),"no pack set for a blade or the bow");
        Check(NativeVisualMotion.Tactical(NativeBombs.Spec,NativeVisualMotion.For(NativeBombs.Spec),book)==NativeVisualMotion.For(NativeBombs.Spec),"no pack set for the Bombadier");
        foreach(var (spec,baseSet,gun) in new[]{("pistol",pistol,NativeTacticalMotion.Pistol),("rifle",rifle,NativeTacticalMotion.Rifle),("machinegun",rifle,NativeTacticalMotion.MachineGun)})
        {
            var t=NativeVisualMotion.Tactical(spec,baseSet,book);
            Check(t!=baseSet&&t.Tactical==gun&&t.Base==baseSet&&NativeVisualMotion.Tactical(spec,baseSet,book)==t,spec+": its pack set, made once");
            Check(t.Aim.All(c=>c==gun.Idle)&&t.Reload==gun.Reload&&!t.FireFromHit&&t.Gun,spec+": the aim on the pack's ready hold at every pitch, its reload the pack's");
            Check(gun.Fire!=null?t.Fire.All(c=>c==gun.Fire):t.Fire==baseSet.Fire,spec+": its shot the pack's (the machine gun keeps its own set's shot clip for the techniques; its kick is procedural)");
            // The match idles, strides, jumps, landings, reactions and techniques stay the gun's own set's (owner 2026-11-10, 11-21).
            Check(t.Idle==baseSet.Idle&&t.RestArms==baseSet.RestArms&&t.RestReady==baseSet.RestReady&&t.RestLow&&t.Locomotion==baseSet.Locomotion&&t.Jump==baseSet.Jump&&t.Fall==baseSet.Fall&&t.Land==baseSet.Land&&t.Reactions==baseSet.Reactions,spec+": the match idles, strides and jumps unchanged");
            foreach(var c in new[]{gun.Idle,gun.Fire,gun.Reload,gun.Walk,gun.Sprint})if(c!=null)Check(Array.IndexOf(t.All,c)>=0,spec+": the bank's list carries "+c);
            Check(Array.IndexOf(NativeTacticalMotion.Clips,gun.Idle)>=0&&Array.IndexOf(NativeTacticalMotion.Clips,gun.Reload)>=0,spec+": the bake list names its clips");
        }
        Check(NativeVisualMotion.For("pistol")==pistol&&NativeVisualMotion.For("machinegun")==rifle,"the gun sets themselves are untouched (the pack sets are copies)");
        Check(NativeTacticalMotion.For("pistol")==NativeTacticalMotion.Pistol&&NativeTacticalMotion.For("sword")==null,"the guns the pack covers");
    }
    private static void Timing(Book book)
    {
        // A shot: the rifle's bolt home (the pack's OnActionEnded, 1.167 s) on the rules' 1.15 s interval; the pistol's kick settled on its
        // .42 s; never slower than the clip's own pace, at most MaxFireRate times it; then on at its own pace.
        var r=NativeTacticalMotion.Rifle;var p=NativeTacticalMotion.Pistol;float ri=GunRules.Get(WeaponId.Rifle).Interval,pi=GunRules.Get(WeaponId.Pistol).Interval;
        Check(Near(NativeTacticalMotion.FireTime(r,ri,ri),r.FireReady),"the rifle's bolt is home when the next shot is allowed (the interval)");
        Check(Near(NativeTacticalMotion.FireTime(p,pi,pi),p.FireReady),"the pistol's kick has settled by its interval");
        Check(Near(NativeTacticalMotion.FireTime(r,ri+.3f,ri),r.FireReady+.3f),"past the ready moment the shot plays at its own pace");
        Check(Near(NativeTacticalMotion.FireTime(r,.1f,5),.1f)&&Near(NativeTacticalMotion.FireTime(r,.1f,.01f),.1f*NativeTacticalMotion.MaxFireRate),"never slower than the clip, at most MaxFireRate times it");
        Check(NativeTacticalMotion.FireTime(r,0,ri)==0&&NativeTacticalMotion.FireTime(r,-1,ri)==0,"no time before the shot");
        // A reload: its start on ReloadAt, back on the gun (the pack's OnActionEnded) exactly at ReloadUntil, whatever the span.
        foreach(var (g,w) in new[]{(p,WeaponId.Pistol),(r,WeaponId.Rifle),(NativeTacticalMotion.MachineGun,WeaponId.MachineGun)})
        {
            float at=60,until=at+GunRules.Get(w).Reload;
            Check(NativeTacticalMotion.ReloadTime(g,at,at,until)==0&&Near(NativeTacticalMotion.ReloadTime(g,until,at,until),g.ReloadEnd)&&Near(NativeTacticalMotion.ReloadTime(g,(at+until)*.5f,at,until),g.ReloadEnd*.5f),g.Spec+": the reload fills ReloadAt..ReloadUntil, back on the gun at its end");
            Check(g.ReloadEnd<=book.Length(g.Reload)+1e-4f,g.Spec+": the reload's end lies in its clip");
            Check(Near(NativeTacticalMotion.ReloadTime(g,at+1,at,at+.5f),g.ReloadEnd),g.Spec+": a shorter reload (a chip) still ends on the gun");
        }
        // The raise onto the aim over the gun's Settle (pistol .15, rifle .3, machine gun .2); a shot from the hip at once.
        foreach(var w in new[]{WeaponId.Pistol,WeaponId.Rifle,WeaponId.MachineGun})
        {
            var a=new Actor{Weapon=w,Aiming=true};Check(Near(NativeTacticalMotion.RaiseTime(a,.12f),Math.Max(.12f,GunRules.Get(w).Settle)),w+": aiming rises over the rules' Settle");
            a.Aiming=false;Check(Near(NativeTacticalMotion.RaiseTime(a,.12f),.12f),w+": a hip shot rises at once");
        }
        // The machine gun's kick: up at once, gone by KickTime; nothing for the guns with a shot clip.
        var mg=NativeTacticalMotion.MachineGun;
        Check(NativeTacticalMotion.Kick(mg,0)==1&&NativeTacticalMotion.Kick(mg,NativeTacticalMotion.KickTime)==0&&NativeTacticalMotion.Kick(mg,-.01f)==0&&NativeTacticalMotion.Kick(r,0)==0&&NativeTacticalMotion.Kick(p,0)==0,"the machine gun's kick per shot");
        Check(NativeTacticalMotion.KickTime>GunRules.Get(WeaponId.MachineGun).Interval,"held fire keeps the kick going (it outlasts the interval)");
    }
    private static void Layers(Book book)
    {
        var rifle=NativeVisualMotion.Tactical("rifle",NativeVisualMotion.For("rifle"),book);var mg=NativeVisualMotion.Tactical("machinegun",NativeVisualMotion.For("machinegun"),book);var pistol=NativeVisualMotion.Tactical("pistol",NativeVisualMotion.For("pistol"),book);
        var gun=new Actor{Grounded=true,Weapon=WeaponId.Rifle,LastShot=-100};NativeVisualMotion.Upper up;
        Check(NativeVisualMotion.Layer(rifle,gun,50,book,out up,true)&&!up.Raised&&up.A==rifle.Tactical.Idle,"at rest, arms free: lowered (the match idle's own carry shows)");
        gun.Aiming=true;Check(NativeVisualMotion.Layer(rifle,gun,50,book,out up)&&up.Raised&&up.A==NativeTacticalMotion.Rifle.Idle&&up.B==null,"aimed: the pack's ready hold (the spine pitches it)");
        gun.Aiming=false;gun.LastShot=50;NativeVisualMotion.Layer(rifle,gun,50.5f,book,out up);
        Check(up.Raised&&up.A==NativeTacticalMotion.Rifle.Fire&&Near(up.TimeA,NativeTacticalMotion.FireTime(NativeTacticalMotion.Rifle,.5f,GunRules.Get(WeaponId.Rifle).Interval)),"just after a shot: raised, the shot and bolt on the rules' interval");
        NativeVisualMotion.Layer(rifle,gun,50+1.67f,book,out up);Check(up.A==NativeTacticalMotion.Rifle.Idle,"the shot played out: back on the ready hold");
        NativeVisualMotion.Layer(rifle,gun,50+NativeVisualMotion.ShotHold(gun)+.01f,book,out up);Check(!up.Raised,"lowered again once the shot is past (the rules' interval and a little)");
        var gunner=new Actor{Grounded=true,Weapon=WeaponId.MachineGun,LastShot=70};NativeVisualMotion.Layer(mg,gunner,70.04f,book,out up);
        Check(up.Raised&&up.A==NativeTacticalMotion.MachineGun.Idle,"the machine gun's shot: raised on its ready hold (the kick is procedural)");
        gun.ReloadAt=60;gun.ReloadUntil=62.6f;NativeVisualMotion.Layer(rifle,gun,61.3f,book,out up);
        Check(up.Raised&&up.A==NativeTacticalMotion.Rifle.Reload&&Near(up.TimeA,NativeTacticalMotion.Rifle.ReloadEnd*.5f),"the reload: the pack's, fitted to ReloadAt..ReloadUntil");
        var shooter=new Actor{Grounded=true,Weapon=WeaponId.Pistol,LastShot=80};NativeVisualMotion.Layer(pistol,shooter,80.1f,book,out up);
        Check(up.Raised&&up.A==NativeTacticalMotion.Pistol.Fire&&Near(up.TimeA,.1f),"the pistol's shot: its kick at its own pace (its interval fits it)");
        shooter.LastShot=-100;shooter.Sprinting=true;NativeVisualMotion.Layer(pistol,shooter,90,book,out up,true);Check(!up.Raised,"sprinting: carried, not raised");
        // The gun's Q/E keep their own clips (the grenade, the flourish, the rocket held on the aim).
        var thrower=new Actor{Grounded=true,Weapon=WeaponId.Pistol,AbilityId="frag-grenade",AbilityAt=120,AbilityRelease=120.2f};
        Check(NativeVisualMotion.Layer(pistol,thrower,120.1f,book,out up)&&up.A==pistol.AbilityStarts[0],"the pistol's grenade keeps its throw");
        var rocket=new Actor{Grounded=true,Weapon=WeaponId.MachineGun,AbilityId="rocket",AbilityAt=130,AbilityRelease=130.12f,LastShot=-100};
        Check(NativeVisualMotion.Layer(mg,rocket,130.3f,book,out up,true)&&up.Raised&&up.A==NativeTacticalMotion.MachineGun.Idle,"the rocket: held on the pack's aim");
        // Nothing here is written to the pilot.
        Check(gun.ReloadAt==60&&gun.LastShot==50&&!gun.Aiming&&gunner.LastShot==70,"the layer reads the pilot and writes nothing");
    }
    private static void Carry(Book book)
    {
        NativeTacticalMotion.Carry(NativeTacticalMotion.Rifle,5,0,book,out var a,out var ta,out var b,out var tb,out var mix,out var w);
        Check(a==NativeTacticalMotion.PackWalk&&Near(ta,5%4.05f)&&b==NativeTacticalMotion.PackSprint&&mix==0&&w==1,"a long gun on the move: the pack walk's ready hold on its own clock");
        NativeTacticalMotion.Carry(NativeTacticalMotion.MachineGun,1,.6f,book,out a,out ta,out b,out tb,out mix,out w);
        Check(b==NativeTacticalMotion.PackSprint&&Near(mix,.6f)&&w==1,"sprinting: the pack sprint's port carry by the sprint's share");
        NativeTacticalMotion.Carry(NativeTacticalMotion.Pistol,1,.6f,book,out a,out ta,out b,out tb,out mix,out w);
        Check(a==NativeTacticalMotion.Pistol.Idle&&b==null&&Near(w,.4f),"the pistol carries on its ready hold, its own sprint carry taking over as it sprints");
        book.Pack=false;NativeTacticalMotion.Carry(NativeTacticalMotion.Rifle,1,0,book,out a,out ta,out b,out tb,out mix,out w);Check(a==null&&w==0,"no carry without the clips");book.Pack=true;
    }
    private static void Sources(string root)
    {
        string core=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeTacticalMotion.cs")),pres=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeTacticalMotionPresentation.cs"));
        string motion=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeVisualMotionPresentation.cs")).Replace("\r\n","\n");
        foreach(var s in new[]{core,pres})Check(!s.Contains("KINEMATION")&&!s.Contains("Kinemation")&&!s.Contains("KShooter")&&!s.Contains("TacticalShooterPack"),"no pack code or pack type named");
        // Presentation only: the core reads the rules and the pilot, never writes them.
        foreach(var w in new[]{"a.LastShot=","a.ReloadAt=","a.ReloadUntil=","a.Aiming=",".Interval=",".Reload=",".Settle=","a.Weapon="})Check(!core.Contains(w)&&!pres.Contains(w),"nothing written: "+w);
        Check(motion.Contains("NativeVisualMotion.Tactical(spec,NativeVisualMotion.For(spec),VisualMotionBank)"),"a body plays the pack set only when the bank carries it");
        int carry=motion.IndexOf("VisualTacticalCarry(m,pick,time,carryWeight);",StringComparison.Ordinal),upper=motion.IndexOf("System.Array.Clear(m.UpperMix,0,m.UpperMix.Length);",StringComparison.Ordinal);
        Check(carry>0&&upper>carry,"the carry goes on before the upper layer (an aim, a shot or a reload lays over it)");
        Check(motion.Contains("Quaternion.Slerp(m.HoldRotation,m.TacHold,1-(1-m.CarryShare)*(1-m.Upper))"),"the hand's turn on the gun: the pack's ready hold by the pack's share, the gun's own set's at rest");
        // The bank and the clips are local (LocalAssets is git-ignored): none in the repository.
        var assets=Path.Combine(root,"Assets");
        Check(!Directory.EnumerateFiles(assets,"*.bytes",SearchOption.AllDirectories).Any(f=>Path.GetFileName(f).StartsWith("ChipArenaVisualMotion",StringComparison.Ordinal)&&!f.Replace('\\','/').Contains("/LocalAssets/")),"no motion bank outside LocalAssets");
        Check(!Directory.EnumerateFiles(assets,"A_FP_*",SearchOption.AllDirectories).Any(f=>!f.Replace('\\','/').Contains("/LocalAssets/")),"no pack clip outside LocalAssets");
    }
}
#endif
