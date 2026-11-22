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
        Sets(book);Timing(book);Layers(book);Carry(book);Grips();Sources(root);
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
        // The draw: no rule draws a gun, so it plays over the DrawTime before the fight's start (the round's GO), its end (the pack's
        // OnActionEnded) on GO; nothing outside it.
        foreach(var g in new[]{p,r,mg})
        {
            Check(g.Draw!=null&&g.DrawTime>0&&g.DrawEnd>0&&NativeTacticalMotion.DrawClipTime(g,0)==0&&Near(NativeTacticalMotion.DrawClipTime(g,g.DrawTime*.5f),g.DrawEnd*.5f),g.Spec+": the draw fitted to its time");
            Check(float.IsNaN(NativeTacticalMotion.DrawClipTime(g,-.01f))&&float.IsNaN(NativeTacticalMotion.DrawClipTime(g,g.DrawTime))&&float.IsNaN(NativeTacticalMotion.DrawClipTime(g,float.NaN)),g.Spec+": no draw outside it");
            Check(g.DrawEnd/g.DrawTime<=2.2f,g.Spec+": the draw at most 2.2 times its pace");
        }
        Check(Array.IndexOf(NativeTacticalMotion.Clips,NativeTacticalMotion.SupportFist)>=0,"the support fist is baked with the guns' clips");
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
        // The draw on the upper layer, before the fight's start; it gives way to everything after.
        var drawer=new Actor{Grounded=true,Weapon=WeaponId.Rifle,LastShot=-100};float half=NativeTacticalMotion.Rifle.DrawTime*.5f;
        Check(NativeVisualMotion.Layer(rifle,drawer,3,book,out up,true,half)&&up.Raised&&up.A==NativeTacticalMotion.Rifle.Draw&&Near(up.TimeA,NativeTacticalMotion.Rifle.DrawEnd*.5f),"the draw before GO");
        Check(NativeVisualMotion.Layer(rifle,drawer,3,book,out up,true,NativeTacticalMotion.Rifle.DrawTime+.1f)&&!up.Raised,"after GO: lowered to the match idle");
        Check(NativeVisualMotion.Layer(NativeVisualMotion.For("rifle"),drawer,3,book,out up,true,half)&&!up.Raised,"the gun's own set has no draw");
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
    // The support hand (owner 2026-11-22: "think we need to fix this here"): it grips the model in every state the pack's layers hold the gun
    // in, lets go only for the reload's free hand and a draw's first part, and its hold frames are sound.
    private static void Grips()
    {
        foreach(var g in new[]{NativeTacticalMotion.Pistol,NativeTacticalMotion.Rifle,NativeTacticalMotion.MachineGun})
        {
            var s=g.Support;Check(s!=null&&s.Fingers.Length()>.5f&&s.Thumb.Length()>.5f&&Math.Abs(System.Numerics.Vector3.Dot(System.Numerics.Vector3.Normalize(s.Fingers),System.Numerics.Vector3.Normalize(s.Thumb)))<.8f&&s.Along>0&&s.Along<=1&&s.Depth>0&&s.Depth<.05f,g.Spec+": a sound hold frame");
            var a=new Actor{Weapon=WeaponId.Rifle,ReloadAt=-100,ReloadUntil=-99};
            Check(Near(NativeTacticalMotion.GripWeight(g,1,a,10,float.NaN),1)&&Near(NativeTacticalMotion.GripWeight(g,.4f,a,10,float.NaN),.4f)&&NativeTacticalMotion.GripWeight(g,0,a,10,float.NaN)==0,g.Spec+": gripped by the pack's share (aim, shot, carry, jump)");
            a.ReloadAt=20;a.ReloadUntil=21.1f;
            Check(Near(NativeTacticalMotion.GripWeight(g,1,a,20,float.NaN),1)&&NativeTacticalMotion.GripWeight(g,1,a,20+NativeTacticalMotion.ReloadLetGo,float.NaN)<1e-4f&&NativeTacticalMotion.GripWeight(g,1,a,20.55f,float.NaN)<1e-4f,g.Spec+": the reload's start lets go over ReloadLetGo");
            float back=NativeTacticalMotion.GripWeight(g,1,a,21.1f-NativeTacticalMotion.ReloadRegrip*.5f,float.NaN);
            Check(back>.2f&&back<.8f&&Near(NativeTacticalMotion.GripWeight(g,1,a,21.1f,float.NaN),1)&&Near(NativeTacticalMotion.GripWeight(g,1,a,21.3f,float.NaN),1),g.Spec+": the reload's end takes it again by ReloadUntil");
            a.ReloadAt=-100;a.ReloadUntil=-99;
            Check(NativeTacticalMotion.GripWeight(g,1,a,10,0)==0&&NativeTacticalMotion.GripWeight(g,1,a,10,g.DrawTime*NativeTacticalMotion.DrawJoinFrom)==0&&Near(NativeTacticalMotion.GripWeight(g,1,a,10,g.DrawTime*NativeTacticalMotion.DrawJoinTo),1),g.Spec+": the draw's support hand joins late");
            Check(NativeTacticalMotion.GripWeight(null,1,a,10,float.NaN)==0,"no grip without a gun");
        }
        Check(NativeTacticalMotion.Rifle.Support==NativeTacticalMotion.ForeEnd&&NativeTacticalMotion.MachineGun.Support==NativeTacticalMotion.ForeEnd&&NativeTacticalMotion.Pistol.Support==NativeTacticalMotion.Wrap,"the long guns hold the fore-end, the pistol wraps");
        Check(NativeTacticalMotion.ForeEnd.Thumb.Z>.9f&&NativeTacticalMotion.ForeEnd.Fingers.X>.9f&&NativeTacticalMotion.Wrap.Thumb.Y>.9f&&NativeTacticalMotion.Wrap.Fingers.Z>.9f,"the fore-end from below (fingers across to the far side, thumb forward); the pistol thumbs-forward (fingers round the front, thumb up the frame)");
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
        int fist=motion.IndexOf("VisualTacticalFist(m);",StringComparison.Ordinal),inertia=motion.IndexOf("VisualMotionInertia(m,jumped,dt,keepLegs);",StringComparison.Ordinal);
        Check(fist>0&&inertia>fist&&pres.Contains("n[i].StartsWith(\"LeftHand.\",System.StringComparison.Ordinal)")&&pres.Contains("if(m.Set.Tactical==null||m.Grip<=0)return;")&&motion.Contains("m.Grip=m.Set.Tactical!=null?NativeTacticalMotion.GripWeight("),"the support hand closes (its fingers only, by the grip's weight), before the pose goes on");
        Check(motion.Contains("float drawAge=VisualTacticalDrawAge(m,body,time);")&&motion.Contains("NativeVisualMotion.Layer(m.Set,a,time,VisualMotionBank,out up,ready,drawAge)")&&pres.Contains("NativeSpawnIn.Age(match.Phase,match.PhaseEnd,match.FightAt,time)"),"the draw on the round's GO as the spawn-in times it");
        // The anchoring (owner 2026-11-22: "sometimes it appears the gun and hand aren't connected"): the gun's aim turn and low carry turn the
        // hand's wrist and the gun is placed on the hand again after them; the support hand is seated last, after the weapon; the hit
        // puppet's bend comes after the motion and carries the rig's hands with the drawn ones; the model draws after every pose.
        int wrist=motion.IndexOf("if(m.Set.Gun&&!reloading)VisualGunWrist(m,weaponHand,a,wgt,heldTurn);",StringComparison.Ordinal),hang=motion.IndexOf("VisualVictimHang(weaponHand,",StringComparison.Ordinal),seat=motion.IndexOf("VisualTacticalGrip(m,weaponHand,body.Held.Support,body.Root.transform,m.Grip*wgt);",StringComparison.Ordinal),support=motion.IndexOf("ArmReachTo(m.LeftArm,m.LeftForearm,m.LeftHand,m.LeftHand.position+(grip-fist)*m.Support*wgt",StringComparison.Ordinal);
        Check(wrist>0&&hang>wrist&&support>hang&&seat>support,"the wrist's turns, then the hang, the old support reach and the pack's grip last");
        Check(motion.Contains("if(m.Set.Tactical!=null){m.RightHand.rotation=m.RightHand.rotation*heldTurn*Quaternion.Inverse(m.TacHold);heldTurn=m.TacHold;}")&&motion.IndexOf("heldTurn=m.TacHold;}",StringComparison.Ordinal)<wrist,"a pack gun sits in the hand at one hold (the pack's); the two holds' difference is the wrist's");
        Check(motion.Contains("(1-(m.Set.RestArms!=null?Ease(m.Rest):0))"),"a long gun at rest grips its handguard too");
        Check(motion.Contains("m.GripHold=m.Set.Tactical!=null&&m.Set.RestArms!=null?Mathf.MoveTowards(m.GripHold,ready?1:0,dt>0?dt/UpperEase:1):0;")&&motion.Contains("GripWeight(m.Set.Tactical,Mathf.Max(packShare,Ease(m.GripHold)),"),"a long gun grips through the hand-over between its rest, carry and raise (the body's readiness, eased once)");
        Check(motion.Contains("m.RightHand.rotation=wrist*m.RightHand.rotation;")&&motion.Contains("weapon.SetPositionAndRotation(m.RightHand.position+m.RightHand.rotation*m.HoldPosition,m.RightHand.rotation*held);")&&!motion.Contains("weaponHand.rotation=Quaternion.AngleAxis(CarryTilt"),"the gun's turns are the wrist's: no turn of the gun alone inside the palm");
        string bodyPres=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeVisualBodyPresentation.cs")).Replace("\r\n","\n");
        int pose=bodyPres.IndexOf("VisualMotionPose(body,rig,a,time);",StringComparison.Ordinal),fit=bodyPres.IndexOf("VisualHandFit(body,rig,held);",StringComparison.Ordinal),puppet=bodyPres.IndexOf("HitPuppetPilot(body,rig,a,time);",StringComparison.Ordinal);
        Check(pose>0&&fit>pose&&puppet>fit,"the motion, then the hand's fit, then the hit puppet's bend (which moves the rig's hands with the drawn ones)");
        string hitPres=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHitPuppetPresentation.cs"));
        Check(hitPres.Contains("hand.SetPositionAndRotation(drawn.position+turn*(hand.position-hitPuppetHandAt[side]),turn*hand.rotation);"),"a bent hand carries its weapon frame");
        string arenaPres=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/ArenaPresentation.cs"));
        Check(arenaPres.Contains("VisualBodyRestore(a.Id);PrepareCapturedPose(a,p,renderTime);VisualBodyPose(a,renderTime);")&&arenaPres.Contains("// Pose everyone before drawing"),"every body poses before any model draws (the same frame, no lag)");
        // The bank and the clips are local (LocalAssets is git-ignored): none in the repository.
        var assets=Path.Combine(root,"Assets");
        Check(!Directory.EnumerateFiles(assets,"*.bytes",SearchOption.AllDirectories).Any(f=>Path.GetFileName(f).StartsWith("ChipArenaVisualMotion",StringComparison.Ordinal)&&!f.Replace('\\','/').Contains("/LocalAssets/")),"no motion bank outside LocalAssets");
        Check(!Directory.EnumerateFiles(assets,"A_FP_*",SearchOption.AllDirectories).Any(f=>!f.Replace('\\','/').Contains("/LocalAssets/")),"no pack clip outside LocalAssets");
    }
}
#endif
