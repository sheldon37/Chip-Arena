using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// The melee jump attack (owner 2026-10-12: "Give jump attacks to melee classes. (Think of Elden ring style jump attack
/// animations) or if theres anything in the pack."; Core/NativeAirAttack, NativePackAirAttacks.Generated, MeleeMotion.AirCut):
/// the exported pack takes match tools/pack-air-attacks-provenance.json; every captured melee weapon has one; the clip time
/// raises the weapon, hangs, chops across the hit window and follows through, continuous and monotone; a swing begun in the
/// air (a standing jump, a running jump, a dagger double jump) plays it with the blade's fastest frame in the hit window and the
/// blade coming down from overhead; a landing during it sinks deeper than a plain landing with the feet on the floor and leaves
/// one impact; a swing that ends in the air settles back with no impact; ground swings keep their cuts; the explicit hook wins
/// over the airtime; the first-person bow brings the raised weapon in front of the camera and chops it down; the impact effect
/// stays above the floor and thins with the quality. Presentation only: no jump, swing, hitbox or network change.
/// </summary>
internal static class AirAttackChecks
{
    private const float Dt=1/60f;
    private static readonly string[] Specs={"sword","daggers","shield","greatsword"};
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Jump attack: "+what);}
    public static void Run(string root,ConversionContent data)
    {
        var log=new List<string>();
        Clips(root,log);Timing(log);Poses(data,log);View(log);Fx(log);
        foreach(var line in log)Console.WriteLine("PASS jump attack: "+line);
    }
    // ---- the exported takes ----
    private static void Clips(string root,List<string> log)
    {
        Check(string.Join(",",NativePackAirAttacks.Names)=="sword,daggers,shield,greatsword"&&NativePackAirAttacks.Clips.Length==4,"four air takes in weapon order");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-air-attacks-provenance.json")));
        var prov=new Dictionary<string,JsonElement>();foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())prov[c.GetProperty("clip").GetString()]=c;
        Check(prov.Count==4&&doc.RootElement.GetProperty("channels").GetInt32()==CapturedClip.Channels,"the provenance lists every take");
        var row=new float[CapturedClip.Channels];int frames=0;
        foreach(var spec in Specs)
        {
            var c=NativeAirAttackRules.Clip(spec);Check(c!=null&&c.Name=="air/"+spec,spec+" has its air take");var p=prov[c.Name];frames+=c.Frames;
            Check(p.GetProperty("frames").GetInt32()==c.Frames&&p.GetProperty("fps").GetInt32()==c.Fps&&p.GetProperty("source").GetString()==c.Source,c.Name+": size and source match the provenance");
            Check(Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(c.Data))).ToLowerInvariant()==p.GetProperty("sha256").GetString(),c.Name+": track is the exporter's output");
            Check(Math.Abs(c.Duration-NativeAirAttackRules.FollowEnd)>0&&c.Duration>=NativeAirAttackRules.FollowEnd,c.Name+": the take outlasts its follow-through");
            for(float t=0;t<=c.Duration;t+=1f/120){c.Sample(t,row);foreach(float v in row)Check(float.IsFinite(v)&&Math.Abs(v)<50,c.Name+" decodes finite at "+t.ToString("0.000"));}
        }
        foreach(var spec in new[]{"pistol","rifle","fire","emperor","siren"})Check(NativeAirAttackRules.Clip(spec)==null,spec+" keeps no air take");
        foreach(var f in Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*",SearchOption.AllDirectories))Check(!f.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase),"no pack FBX in the project: "+f);
        log.Add("4 takes ("+frames+" frames) match the exporter's provenance (SHA-256 per track), decode finite; guns, magic and the DEV styles keep none; no pack FBX in the project");
    }
    // ---- the clip time ----
    private static void Timing(List<string> log)
    {
        float worst=0;
        foreach(var spec in Specs)
        {
            var c=NativeAirAttackRules.Clip(spec);
            foreach(float windup in new[]{.09f,.14f,.2f,.42f})
            {
                const float active=.16f;float recovery=.3f,prev=NativeAirAttackRules.ClipTime(c,0,windup,active,recovery);Check(Math.Abs(prev)<1e-5f,spec+" starts on the take's first frame");
                for(float age=Dt/4;age<=windup+active+recovery;age+=Dt/4)
                {
                    float ct=NativeAirAttackRules.ClipTime(c,age,windup,active,recovery);Check(ct>=prev-1e-5f,spec+" clip time never runs back at "+age.ToString("0.000"));
                    // (Between Raised and SweepStart the take holds its keys: the pose does not move however fast that span plays.)
                    bool held=prev>=NativeAirAttackRules.Raised-1e-4f&&ct<=NativeAirAttackRules.SweepStart+1e-4f;
                    if(!held){worst=Math.Max(worst,ct-prev);Check(ct-prev<.02f,spec+" clip time continuous at "+age.ToString("0.000"));}prev=ct;
                }
                Check(Math.Abs(NativeAirAttackRules.ClipTime(c,NativeAirAttackRules.RaiseTime(windup),windup,active,recovery)-NativeAirAttackRules.Raised)<1e-4f,spec+" raised by the hang");
                Check(Math.Abs(NativeAirAttackRules.ClipTime(c,windup,windup,active,recovery)-NativeAirAttackRules.SweepStart)<1e-4f&&Math.Abs(NativeAirAttackRules.ClipTime(c,windup+active,windup,active,recovery)-NativeAirAttackRules.Impact)<1e-4f,spec+" chops across the hit window");
                Check(Math.Abs(NativeAirAttackRules.ClipTime(c,windup+active+recovery,windup,active,recovery)-NativeAirAttackRules.FollowEnd)<1e-4f,spec+" follows through to rest");
            }
        }
        Check(NativeAirAttackRules.Frozen(.5f,float.NaN,.14f,.16f,1)==0&&Math.Abs(NativeAirAttackRules.Frozen(1,.4f,.14f,.16f,1)-NativeAirAttackRules.Hold(1))<1e-5f&&NativeAirAttackRules.Frozen(.35f,.4f,.14f,.16f,1)==0,"a landing holds the pose from the later of itself and the window's end, at most Hold");
        Check(NativeAirAttackRules.Air(-1,true)&&!NativeAirAttackRules.Air(-1,false)&&!NativeAirAttackRules.Air(0,true)&&NativeAirAttackRules.Air(1,false),"the explicit flag wins over the airtime");
        log.Add("the clip time raises over "+(NativeAirAttackRules.RaiseShare*100).ToString("0")+"% of the windup (at least "+(NativeAirAttackRules.MinRaise*1000).ToString("0")+" ms), hangs, chops across the hit window, follows through to rest; continuous and monotone at every windup (largest step "+(worst*1000).ToString("0.0")+" ms of clip)");
    }
    // ---- posed through MeleeMotion with the jump layer ----
    private sealed class Flight{public readonly List<string> States=new List<string>();public float Fastest,FastestAt,TipHang=float.MinValue,TipChop,PelvisLow=float.MaxValue,AnkleLow=float.MaxValue,Spike;public int Impacts;public float Land=-1,End;public string FastestPhase;}
    // A jump (8.5 m/s unless `launch`, gravity 23) at t=0, a press `press` s later (as the host stops the run on a swing); optional air jump.
    private static Flight Drive(ConversionContent data,string spec,float press,bool run=false,float airJump=-1,bool jump=true,int explicitAir=-1,float launch=8.5f)
    {
        var w=data.Weapon(spec);var stats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>());var m=new MeleeMotion{Impacts=new MeleeImpacts(),JumpLayer=new NativeJumpMotion()};
        var v=new MeleeView{Id="air-"+spec,Spec=spec,AirAttack=explicitAir};var set=MeleeSet.For(spec);var o=new Flight();
        const float T0=1;float y=0,vy=0,z=0,vz=run?6.8f:0,last=-100,aj=-100;bool grounded=true;NativeMeleeSwing swing=null;float end=0;Vector3? prev=null;var tips=new List<(float t,float s,string ph)>();
        for(int f=0;f*Dt<T0+2.4f;f++)
        {
            float t=f*Dt;
            if(jump&&Math.Abs(t-T0)<Dt*.5f){vy=launch;grounded=false;}
            if(airJump>0&&Math.Abs(t-T0-airJump)<Dt*.5f&&!grounded){vy=7.65f;aj=t;}
            if(Math.Abs(t-T0-press)<Dt*.5f){swing=NativeMeleeFeel.Begin(w,stats,t,1,0);swing.Until=Math.Max(swing.Until,t+(float)(w.windup+.28));end=swing.Until;o.End=end-T0;}
            bool live=swing!=null&&t<end;if(live)vz=0;
            if(!grounded){vy-=23*Dt;y+=vy*Dt;if(y<=0&&vy<=0){y=0;vy=0;grounded=true;if(o.Land<0)o.Land=t-T0;}}
            z+=vz*Dt;
            v.Vel=new Vector3(0,vy,vz);v.Grounded=grounded;v.AirJumpAt=aj>-90?aj:-100;v.Yaw=0;
            if(swing!=null){v.LastAttack=swing.At;v.Windup=swing.HitAt-swing.At;v.AttackRate=1;v.AttackIndex=1;bool win=t<=swing.HitUntil;v.SwingAt=win?swing.HitAt:float.NaN;v.SwingUntil=win?swing.HitUntil:float.NaN;
                v.Commit=live;v.CommitAt=swing.At;v.CommitUntil=end;v.CommitYaw=0;v.StepAt=swing.StepAt;v.StepUntil=swing.StepUntil;v.StepDistance=swing.Distance;}
            var r=m.Rig;r.GroupPosition=new Vector3(0,y+.865f,z);r.GroupYaw=m.PresentedYaw(v,t,0);float dt=last<-90?Dt:t-last;last=t;m.Sample(v,t,dt);
            Vector3 hand=r.ChainPoint(CapturedRig.RHand,-1),tip=hand+Vector3.Transform(r.Reached(set.Tip),r.ChainQuat(CapturedRig.RHand,-1)),world=tip+r.GroupPosition;
            float speed=prev.HasValue?Vector3.Distance(world,prev.Value)/Dt:0;prev=world;
            string phase=swing==null||t<swing.At?"":t<swing.HitAt?"windup":t<=swing.HitUntil?"active":t<end?"recovery":"after";
            if(swing!=null&&t>=swing.At){o.States.Add(m.State);tips.Add((t-T0,speed,phase));if(speed>o.Fastest&&t<end){o.Fastest=speed;o.FastestAt=t-T0;o.FastestPhase=phase;}}
            float head=r.ChainPoint(CapturedRig.Head,-1).Y;
            // The daggers' reverse grip points the blades down from the fists: their stab is read at the fist.
            float lead=spec=="daggers"?hand.Y:tip.Y;
            // Highest from the hang into the chop (the greatsword winds back at its side and brings the blade over the top in the
            // window's first half), and where the chop ends.
            if(swing!=null&&t>=swing.HitAt-.04f&&t<=swing.HitAt+.6f*(swing.HitUntil-swing.HitAt))o.TipHang=Math.Max(o.TipHang,lead-head);if(swing!=null&&Math.Abs(t-swing.HitUntil)<Dt*.5f)o.TipChop=lead-head;
            if(t>T0+.3f){o.PelvisLow=Math.Min(o.PelvisLow,r.ChainPoint(CapturedRig.Pelvis,-1).Y+y);o.AnkleLow=Math.Min(o.AnkleLow,Math.Min(r.AnkleInGroup(true).Y,r.AnkleInGroup(false).Y)+y);}
        }
        for(int i=1;i+1<tips.Count;i++)if(tips[i].s>8)o.Spike=Math.Max(o.Spike,tips[i].s/Math.Max(2,Math.Max(tips[i-1].s,tips[i+1].s)));
        o.Impacts=m.AirWatch.Impacts;return o;
    }
    private static void Poses(ConversionContent data,List<string> log)
    {
        float worstSpike=0,deeper=float.MaxValue,chopLow=float.MaxValue,ankle=float.MaxValue;
        foreach(var spec in Specs)
        {
            // Pressed so the strike lands in the air and the pilot lands during the recovery: near the apex (the daggers' 0.4 s
            // swing a little later, the greatsword's 0.42 s windup earlier).
            float apex=spec=="daggers"?.4f:spec=="greatsword"?.1f:.3f;var plain=Drive(data,spec,5);var stand=Drive(data,spec,apex);
            Check(stand.States.Count>0&&stand.States[3].StartsWith("air attack"),spec+" a swing begun in the air plays the air attack ("+stand.States[3]+")");
            // (The daggers' 90 ms windup raises both blades faster than their chop, as their ground windups reposition them.)
            Check(spec=="daggers"||stand.FastestPhase=="active",spec+" the blade is fastest in the hit window ("+stand.FastestPhase+" "+stand.Fastest.ToString("0")+" m/s at "+stand.FastestAt.ToString("0.000")+")");
            Check(stand.TipHang>(spec=="daggers"?-.1f:.15f)&&stand.TipHang-stand.TipChop>(spec=="daggers"?.4f:spec=="shield"?.6f:.8f),spec+" the blade hangs over the head and chops down ("+stand.TipHang.ToString("0.00")+" -> "+stand.TipChop.ToString("0.00")+" m from the head)");
            chopLow=Math.Min(chopLow,stand.TipHang-stand.TipChop);
            Check(stand.Land>0&&stand.Land<stand.End&&stand.Impacts==1,spec+" landing during the attack leaves one impact");
            Check(stand.PelvisLow<plain.PelvisLow-.04f,spec+" the heavy landing sinks deeper than a plain one ("+stand.PelvisLow.ToString("0.00")+" vs "+plain.PelvisLow.ToString("0.00")+" m)");deeper=Math.Min(deeper,plain.PelvisLow-stand.PelvisLow);
            Check(stand.AnkleLow>=MeleeRig.FLOOR-.02f,spec+" the feet stay on the floor (within 2 cm) through the heavy landing (lowest ankle "+((stand.AnkleLow-MeleeRig.FLOOR)*100).ToString("0.0")+" cm)");
            ankle=Math.Min(ankle,stand.AnkleLow-MeleeRig.FLOOR);worstSpike=Math.Max(worstSpike,stand.Spike);Check(stand.Spike<=2.6f,spec+" no blade pop (x"+stand.Spike.ToString("0.0")+")");
            // Ending in the air (pressed just after a high launch, 16 m/s: 1.4 s up): no impact, back out of the attack before landing.
            var early=Drive(data,spec,.02f,launch:16);Check(early.End+NativeAirAttackRules.LandGrace<early.Land&&early.Impacts==0,spec+" a jump attack that ends in the air leaves no impact (ends "+early.End.ToString("0.00")+", lands "+early.Land.ToString("0.00")+")");
            Check(early.States[early.States.Count-1].IndexOf("air attack",StringComparison.Ordinal)<0,spec+" it settles out of the attack");
            // A running jump, and a ground swing (no jump) keep their own.
            var running=Drive(data,spec,apex,run:true);Check(running.States[3].StartsWith("air attack")&&running.Impacts==1,spec+" a running jump attack");
            var ground=Drive(data,spec,.3f,jump:false);Check(ground.States[3].StartsWith("cut")&&ground.Impacts==0,spec+" a ground swing keeps its cut ("+ground.States[3]+")");
            // The explicit hook: a flagged ground swing in the air keeps its cut; a flagged air swing on the ground plays the take.
            Check(Drive(data,spec,.3f,explicitAir:0).States[3].StartsWith("cut")&&Drive(data,spec,.3f,jump:false,explicitAir:1).States[3].StartsWith("air attack"),spec+" the explicit flag wins");
        }
        var dbl=Drive(data,"daggers",.8f,airJump:.3f);Check(dbl.States[3].StartsWith("air attack")&&dbl.Impacts==1&&dbl.TipHang-dbl.TipChop>.4f,"a dagger double jump into a jump attack");
        log.Add("standing, running and dagger double-jump attacks play the air take (blade fastest in the hit window but for the daggers' raise, coming down at least "+chopLow.ToString("0.0")+" m from over the head, the daggers fists, worst pop x"+worstSpike.ToString("0.0")+"); a landing during it sinks at least "+(deeper*100).ToString("0")+" cm deeper than a plain landing with the feet on the floor (lowest ankle "+(ankle*100).ToString("0.0")+" cm) and leaves one impact; one that ends in the air leaves none; ground swings keep their cuts; the explicit flag wins");
    }
    // ---- the first-person view model ----
    private static void View(List<string> log)
    {
        float prev=0,step=0;
        for(float age=0;age<=.62f;age+=Dt/4){float p=NativeAirAttackRules.ViewPitch(age,.14f,.16f,.32f);step=Math.Max(step,Math.Abs(p-prev));prev=p;}
        Check(step<.12f&&Math.Abs(NativeAirAttackRules.ViewPitch(.62f,.14f,.16f,.32f))<1e-4f,"the view bow is continuous and ends upright");
        // The raised weapon comes in front of the camera and down through the view: the tip in the view rig's frame.
        var set=MeleeSet.For("sword");var c=NativeAirAttackRules.Clip("sword");var rig=new MeleeRig();var row=new float[CapturedClip.Channels];var pose=new CapturedRig.Pose();
        Vector3 Tip(float age){c.Sample(NativeAirAttackRules.ClipTime(c,age,.14f,.16f,.32f),row);rig.Reset();rig.WeaponVisible=true;rig.WeaponReach=1;MeleeCapture.Apply(rig,set,row,CaptureOptions.Of(1,set.Fit,MeleeClear.Turn,legs:0));rig.WritePose(pose);
            NativeFirstPersonPose.SettleTorso(pose);NativeAirAttackRules.ViewPose(pose,NativeAirAttackRules.ViewPitch(age,.14f,.16f,.32f));return pose.WorldPosition[CapturedRig.RHand]+Vector3.Transform(new Vector3(0,0,1.28f),pose.WorldRotation[CapturedRig.RHand]);}
        Vector3 hang=Tip(.14f),chop=Tip(.30f);
        Check(hang.Z>0&&hang.Y-chop.Y>.8f,"first person: the raised blade hangs in front ("+hang+") and chops down ("+chop+")");
        log.Add("first person: the bow is continuous; the raised blade hangs "+hang.Z.ToString("0.00")+" m in front and comes down "+(hang.Y-chop.Y).ToString("0.00")+" m through the view");
    }
    // ---- the impact effect ----
    private static void Fx(List<string> log)
    {
        var feet=new Vector3(3,1,-2);float floor=1;
        for(float age=0;age<NativeAirAttackFx.Life;age+=.01f)
        {
            for(int k=0;k<NativeAirAttackFx.Dust;k++)if(NativeAirAttackFx.Puff(k,NativeAirAttackFx.Dust,7,age,1,feet,floor,out var at,out float size,out float op))Check(at.Y-size*.5f>=floor-.05f&&op>=0&&op<=.5f,"dust stays on the floor");
            var point=NativeAirAttackFx.Point(feet,30,floor);for(int k=0;k<NativeAirAttackFx.Cracks;k++)if(NativeAirAttackFx.Crack(k,NativeAirAttackFx.Cracks,7,age,1,point,out var head,out var dir,out float len,out float a))Check(head.Y>=floor&&Math.Abs(dir.Y)<1e-5f&&len>0&&a<=1,"cracks run over the floor");
        }
        Check(NativeAirAttackFx.Count(NativeAirAttackFx.Dust,NativeAirAttackFx.ReducedDust,.3f,true)<NativeAirAttackFx.Count(NativeAirAttackFx.Dust,NativeAirAttackFx.ReducedDust,1,false),"the impact thins with the quality");
        Check(NativeAirAttackFx.ShakePeak(1)<=NativeStructureShake.MaxOffset&&NativeAirAttackFx.ShakePeak(0)>0,"the shake stays under the structure shake's cap");
        // One impact per landing during an attack, none for a landing outside one.
        var track=new NativeJumpTrack();var watch=new NativeAirAttackWatch();float y=0,vy=0;bool g=true;
        for(int f=0;f<200;f++){float t=1+f*Dt;if(f==10){vy=8.5f;g=false;}if(!g){vy-=23*Dt;y+=vy*Dt;if(y<=0){y=0;vy=0;g=true;}}track.Observe(t,g,y,false);watch.Observe(track,f>=30?1+30*Dt:-100,1+30*Dt+.62f,-1,t);}
        Check(watch.Impacts==1&&watch.Strength>.4f&&watch.Strength<=1,"one impact for a landing during the attack");
        // An airborne Q/E special (Wolf Knight, Skyfall) owns the pilot: never a jump attack, never an impact.
        track=new NativeJumpTrack();watch=new NativeAirAttackWatch();y=0;vy=0;g=true;
        for(int f=0;f<200;f++){float t=1+f*Dt;if(f==10){vy=8.5f;g=false;}if(!g){vy-=23*Dt;y+=vy*Dt;if(y<=0){y=0;vy=0;g=true;}}track.Observe(t,g,y,true);watch.Observe(track,f>=30?1+30*Dt:-100,1+30*Dt+.62f,-1,t,true);}
        Check(!watch.Air&&watch.Impacts==0,"an airborne special is not a jump attack");
        log.Add("dust and cracks stay on the floor, the shock rides the point of impact, the effect thins with the quality, the shake stays under the structure cap, one impact per landing during an attack and none for an airborne special");
    }
}
