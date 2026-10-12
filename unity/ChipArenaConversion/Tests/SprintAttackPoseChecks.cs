using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// The melee sprint attacks (owner 2026-10-12: "can we give melee's a "sprint" attack? Of course each melee gets their own.";
/// Core/NativeSprintAttackVisuals, NativePackSprintAttacks.Generated, MeleeMotion.SprintCut, Core/MeleeFootwork):
/// - the exported pack takes match tools/pack-sprint-attacks-provenance.json, and every captured melee weapon has its own;
/// - the clip time plays each take's lead-in, strike and follow-through on the swing's timing, continuous and monotone (the
///   daggers' twin cuts out over the window's first half and back over its second);
/// - the shield's bash drives the shield out inside the hit window;
/// - on Astra's sprint profiles (NativeSprintAttack.Apply, with the greatsword's hop) a swing entered from a sprint plays the
///   take (a later swing, or one not from a sprint, keeps the cuts), its strike inside the hit window, with every sole on the
///   floor pinned, the twin's two sweeps opposed, and the greatsword leaping with both feet tucked under the hopping body
///   (the jump layer leaves it to the footwork) and landing them as the body lands;
/// - the effects' watch fires one entry per sprint attack and one greatsword landing ring (none if it is not down in the
///   window), the twin's crescents draw its two sweeps, and the effects stay above the floor and thin with the quality.
/// Presentation only: no swing, step, hitbox or network change.
/// </summary>
internal static class SprintAttackPoseChecks
{
    private const float Dt=1/60f;
    private static readonly string[] Specs={"sword","daggers","shield","greatsword"};
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Sprint attack: "+what);}
    public static void Run(string root,ConversionContent data)
    {
        var log=new List<string>();
        Clips(root,log);Timing(log);Poses(data,log);Fx(log);Wiring(root,log);
        foreach(var line in log)Console.WriteLine("PASS sprint attack: "+line);
    }
    // ---- the exported takes ----
    private static void Clips(string root,List<string> log)
    {
        Check(string.Join(",",NativePackSprintAttacks.Names)=="sword,daggers,shield,greatsword"&&NativePackSprintAttacks.Clips.Length==4,"four sprint takes in weapon order");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-sprint-attacks-provenance.json")));
        var prov=new Dictionary<string,JsonElement>();foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())prov[c.GetProperty("clip").GetString()]=c;
        Check(prov.Count==4&&doc.RootElement.GetProperty("channels").GetInt32()==CapturedClip.Channels,"the provenance lists every take");
        var row=new float[CapturedClip.Channels];int frames=0;
        foreach(var spec in Specs)
        {
            var c=NativeSprintAttackRules.Clip(spec);Check(c!=null&&c.Name=="sprint/"+spec,spec+" has its sprint take");var p=prov[c.Name];frames+=c.Frames;
            Check(p.GetProperty("frames").GetInt32()==c.Frames&&p.GetProperty("fps").GetInt32()==c.Fps&&p.GetProperty("source").GetString()==c.Source,c.Name+": size and source match the provenance");
            Check(Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(c.Data))).ToLowerInvariant()==p.GetProperty("sha256").GetString(),c.Name+": track is the exporter's output");
            Check(c.Duration>=MeleeSwing.For(c).FollowEnd,c.Name+": the take outlasts its follow-through");
            for(float t=0;t<=c.Duration;t+=1f/120){c.Sample(t,row);foreach(float v in row)Check(float.IsFinite(v)&&Math.Abs(v)<50,c.Name+" decodes finite at "+t.ToString("0.000"));}
        }
        foreach(var spec in new[]{"pistol","rifle","fire","emperor","siren"})Check(NativeSprintAttackRules.Clip(spec)==null,spec+" keeps no sprint take");
        Check(!NativeSprintAttackRules.Plays("sword",true,true)&&!NativeSprintAttackRules.Plays("sword",false,false)&&NativeSprintAttackRules.Plays("sword",true,false),"only a grounded sprint-flagged swing plays the take");
        foreach(var f in Directory.EnumerateFiles(Path.Combine(root,"Assets"),"*",SearchOption.AllDirectories))Check(!f.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase),"no pack FBX in the project: "+f);
        log.Add("4 takes ("+frames+" frames) match the exporter's provenance (SHA-256 per track), decode finite; guns, magic and the DEV styles keep none; only a grounded sprint-flagged swing plays one; no pack FBX in the project");
    }
    // ---- the clip time and the bash ----
    private static void Timing(List<string> log)
    {
        float worst=0;
        foreach(var spec in Specs)
        {
            var c=NativeSprintAttackRules.Clip(spec);var s=MeleeSwing.For(c);bool twin=NativeSprintAttackRules.Twin(spec);
            foreach(float windup in new[]{.09f,.12f,.2f,.48f})foreach(float active in new[]{.08f,.16f,.24f,.48f})foreach(float recovery in new[]{.12f,.25f,.5f,1.4f})
            {
                float prev=-1;
                for(float age=0;age<=windup+active+recovery;age+=1f/240)
                {
                    // (the twin plays back over the window's second half)
                    float ct=NativeSprintAttackRules.ClipTime(c,spec,age,windup,active,recovery);bool back=twin&&age-1f/240>windup+.5f*active&&age<=windup+active,turn=twin&&age>windup+.5f*active&&age-1f/240<=windup+.5f*active;
                    Check((turn||(back?ct<=prev+1e-5f:ct>=prev-1e-5f||twin&&age>windup+active))&&ct>=s.Start-1e-4f&&ct<=s.FollowEnd+1e-4f,c.Name+" plays "+(back?"back":"forward")+" at "+age.ToString("0.000"));
                    if(prev>=0)worst=Math.Max(worst,Math.Abs(ct-prev));prev=ct;
                }
                float T(float age)=>NativeSprintAttackRules.ClipTime(c,spec,age,windup,active,recovery);
                Check(Math.Abs(T(windup)-s.SweepStart)<1e-3f&&Math.Abs(T(windup+(twin?.5f:1)*active)-s.SweepEnd)<1e-3f&&(!twin||Math.Abs(T(windup+active)-s.SweepStart)<1e-3f&&Math.Abs(T(windup+active+recovery)-s.SweepStart)<1e-3f),c.Name+" plays its strike across the hit window"+(twin?" (the twin out and back)":""));
            }
        }
        Check(worst<.025f,"the sprint clip time never jumps ("+(worst*1000).ToString("0.0")+" ms of clip in 1/240 s)");
        // The bash (on the shield's sprint profile): none as the swing starts, at its height inside the hit window, gone by the
        // recovery's middle, continuous.
        {
            float windup=.12f,active=.16f,recovery=.5f,peak=0,peakAt=0,prev=NativeSprintAttackRules.Bash(0,windup,active,recovery),step=0;
            Check(prev<1e-3f,"the bash is home as the swing starts");
            for(float age=1f/240;age<=windup+active+recovery;age+=1f/240){float k=NativeSprintAttackRules.Bash(age,windup,active,recovery);step=Math.Max(step,Math.Abs(k-prev));prev=k;if(k>peak){peak=k;peakAt=age;}}
            Check(peak>.99f&&peakAt>=windup&&peakAt<=windup+active,"the bash peaks in the hit window ("+peakAt.ToString("0.000")+" s)");
            Check(NativeSprintAttackRules.Bash(windup+active+.5f*recovery,windup,active,recovery)<1e-3f&&step<.05f,"the bash is drawn back by mid-recovery, continuously");
        }
        log.Add("the clip time plays each take's lead-in, strike (across the hit window; the twin out over its first half and back over its second) and follow-through, continuous at every windup, window and recovery (largest step "+(worst*1000).ToString("0.0")+" ms of clip); the shield's bash peaks inside the window and is home by mid-recovery");
    }
    // ---- sprint entries posed through MeleeMotion (as the arena rigs are: with the jump layer) ----
    private sealed class Pilot
    {
        public readonly MeleeMotion Motion=new MeleeMotion{Impacts=new MeleeImpacts(),JumpLayer=new NativeJumpMotion()};public readonly MeleeView View;public Vector3 Position;private float last=-100;
        public Pilot(string spec){View=new MeleeView{Id="sprint-"+spec,Spec=spec};}
        public void Pose(float t){float dt=last<-90?Dt:t-last;last=t;var r=Motion.Rig;r.GroupPosition=Position;r.GroupYaw=Motion.PresentedYaw(View,t,View.Yaw);Motion.Sample(View,t,dt);}
    }
    // Pins: per foot its toe joint, heel back edge and sole centre (a sole holds still about one of them: turning, not sliding).
    private struct Frame{public float T,Tip,Reach,Side,Low0,Low1,Air,JumpAir;public string State;public Vector3[] Pins;public bool[] On;}
    // A sprint run-up along the facing that stops dead into one swing (a sprint attack on Astra's profile, NativeSprintAttack:
    // its step, its timing and the greatsword's hop under 23 m/s2; or a plain swing), then (chain) a second swing at the
    // first one's ChainAt. The floor stays at the start's height. Times are seconds after the press.
    // fall: the gravity the body comes down under (a presented body falling slower than the hop was launched for, as a guest's
    // smoothed height can, lands after the plan).
    private static List<Frame> Drive(ConversionContent data,string spec,bool sprintAttack,bool chain,out NativeMeleeSwing first,float fall=23)
    {
        var w=data.Weapon(spec);var stats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>());var p=new Pilot(spec);var v=p.View;var set=MeleeSet.For(spec);
        const float T0=1,Gravity=23;NativeMeleeSwing swing=null,a=null;float end=0,vy=0,rate=(float)stats["attackRate"];bool grounded=true;int count=0;var frames=new List<Frame>();Vector3? prevTip=null;first=null;
        for(int f=0;f*Dt<T0+2.1f;f++)
        {
            float t=f*Dt;
            if(count==0&&Math.Abs(t-T0)<Dt*.5f||chain&&count==1&&swing!=null&&Math.Abs(t-swing.ChainAt)<Dt*.5f)
            {
                swing=NativeMeleeFeel.Begin(w,stats,t,count+1,0);if(count==0&&sprintAttack)NativeSprintAttack.Apply(swing,rate);swing.Until=Math.Max(swing.Until,t+(float)((w.windup+.28)/rate));end=swing.Until;count++;if(count==1)a=swing;
                if(NativeSprintAttack.Hop(swing)){vy=NativeSprintAttack.HopSpeed(swing,Gravity);grounded=false;}
            }
            bool live=swing!=null&&t<end;var st=live?NativeMeleeRules.Step(swing,t,Dt):new V3();bool run=count==0;
            Vector3 vel=new Vector3(st.x,0,(run?9.18f:0)+st.z);v.Vel=new Vector3(-vel.X,vy,vel.Z);v.Sprinting=run;v.Yaw=0;v.Grounded=grounded;
            if(swing!=null)
            {
                v.LastAttack=swing.At;v.Windup=swing.HitAt-swing.At;v.AttackIndex=swing.Index;bool window=t<=swing.HitUntil;v.SwingAt=window?swing.HitAt:float.NaN;v.SwingUntil=window?swing.HitUntil:float.NaN;
                v.Commit=live;if(live){v.CommitAt=swing.At;v.CommitUntil=end;v.CommitYaw=0;v.StepAt=swing.StepAt;v.StepUntil=swing.StepUntil;v.StepDistance=swing.Distance;}v.SprintAttack=live&&swing.SprintAttack;
            }
            p.Pose(t);var r=p.Motion.Rig;Quaternion g=Quaternion.CreateFromAxisAngle(Vector3.UnitY,r.GroupYaw);
            Vector3 grip=r.ChainPoint(CapturedRig.RHand,-1),tip=Vector3.Transform(grip+Vector3.Transform(r.Reached(set.Tip),r.ChainQuat(CapturedRig.RHand,-1)),g)+r.GroupPosition;
            Vector3 local=grip+Vector3.Transform(r.Reached(set.Tip),r.ChainQuat(CapturedRig.RHand,-1));
            var fr=new Frame{T=t-T0,Tip=prevTip.HasValue?Vector3.Distance(tip,prevTip.Value)/Dt:0,Reach=grip.Z,Side=local.X,Air=p.Position.Y,JumpAir=p.Motion.JumpLayer.AirWeight,State=p.Motion.State,Pins=new Vector3[6],On=new bool[2]};prevTip=tip;
            for(int k=0;k<2;k++)
            {
                int fb=k==0?CapturedRig.LFoot:CapturedRig.RFoot;float low=float.MaxValue,floor=MeleeRig.FLOOR-MeleeFootwork.Sole;
                foreach(var box in CapturedRig.Boxes){if(box.Bone!=fb&&box.Bone!=fb+1)continue;Quaternion q=r.ChainQuat(box.Bone,-1);Vector3 o=r.ChainPoint(box.Bone,-1);
                    for(int c=0;c<8;c++){Vector3 h=new Vector3((c&1)!=0?.5f:-.5f,(c&2)!=0?.5f:-.5f,(c&4)!=0?.5f:-.5f)*box.Size;low=Math.Min(low,(Vector3.Transform(o+Vector3.Transform(box.Position+h,q),g)+r.GroupPosition).Y-floor);}}
                Quaternion fq=r.ChainQuat(fb,-1);Vector3 ankle=r.ChainPoint(fb,-1);Vector3 W(Vector3 v)=>Vector3.Transform(v,g)+r.GroupPosition;
                fr.Pins[k*3]=W(r.ChainPoint(fb+1,-1));fr.Pins[k*3+1]=W(ankle+Vector3.Transform(new Vector3(0,-.07f,-.135f),fq));fr.Pins[k*3+2]=W(ankle+Vector3.Transform(new Vector3(0,-.07f,-.025f),fq));
                fr.On[k]=low<.012f;if(k==0)fr.Low0=low;else fr.Low1=low;
            }
            frames.Add(fr);p.Position+=new Vector3(v.Vel.X,0,v.Vel.Z)*Dt;
            if(!grounded){vy-=fall*Dt;p.Position.Y+=vy*Dt;if(p.Position.Y<=0){p.Position.Y=0;vy=0;grounded=true;}}
        }
        first=a;return frames;
    }
    private static void Poses(ConversionContent data,List<string> log)
    {
        float worstSlip=0,worstSink=0;string slipAt="";var notes=new List<string>();
        foreach(var spec in Specs)
        {
            var fr=Drive(data,spec,true,false,out var s);float hitAt=s.HitAt-1,hitUntil=s.HitUntil-1,end=s.Until-1;
            foreach(var f in fr)if(f.T>=.05f&&f.T<end-.02f)Check(f.State.StartsWith("sprint attack"),spec+" sprint entry presents "+f.State+" at "+f.T.ToString("0.000"));
            // The soles on the floor hold about a pivot (the toe joint, the heel edge or the sole centre), and none sinks.
            for(int i=1;i<fr.Count;i++)for(int k=0;k<2;k++){var a=fr[i-1];var b=fr[i];if(b.T<0||b.T>end+.1f)continue;
                worstSink=Math.Max(worstSink,-(k==0?b.Low0:b.Low1));
                if(a.On[k]&&b.On[k]){float sp=float.MaxValue;for(int j=0;j<3;j++){Vector3 d=b.Pins[k*3+j]-a.Pins[k*3+j];d.Y=0;sp=Math.Min(sp,d.Length()/Dt);}if(sp>worstSlip){worstSlip=sp;slipAt=spec+" at "+b.T.ToString("0.000");}}}
            // The strike lands in the hit window: the blade's fastest frame (the sword's slash, the greatsword's cleave), the
            // daggers' blades thrust furthest out over the twin's first sweep, the shield driven furthest out (its bash).
            float mid=.5f*(hitAt+hitUntil),strikeEnd=spec=="daggers"?mid:end;
            Frame best=fr[0];foreach(var f in fr){if(f.T<0||f.T>strikeEnd)continue;bool better=spec=="sword"||spec=="greatsword"?f.Tip>best.Tip:f.Reach>best.Reach;if(better||best.T<0)best=f;}
            Check(best.T>=hitAt-2*Dt&&best.T<=(spec=="daggers"?mid:hitUntil)+2*Dt,spec+" strikes at "+best.T.ToString("0.000")+" s (hit window "+hitAt.ToString("0.00")+"-"+hitUntil.ToString("0.00")+")");
            notes.Add(spec+" "+(spec=="sword"||spec=="greatsword"?best.Tip.ToString("0")+" m/s":"reach "+(best.Reach*100).ToString("0")+" cm")+" at "+best.T.ToString("0.000"));
            // The daggers' twin: the right blade crosses one way over the window's first half and back over its second (the
            // authority's two sweeps, the second reversed).
            if(spec=="daggers")
            {
                float Side(float at){Frame q=fr[0];foreach(var f in fr)if(Math.Abs(f.T-at)<Math.Abs(q.T-at))q=f;return q.Side;}
                float outward=Side(mid)-Side(hitAt),back=Side(hitUntil)-Side(mid);
                Check(Math.Abs(outward)>=.25f&&Math.Abs(back)>=.25f&&outward*back<0,"the twin sweeps out and back ("+(outward*100).ToString("0")+" then "+(back*100).ToString("0")+" cm across)");
                notes.Add("the twin crosses "+(Math.Abs(outward)*100).ToString("0")+" cm and opens back "+(Math.Abs(back)*100).ToString("0")+" cm");
            }
            // The greatsword leaps with the authority's hop: both feet leave the floor with the body and ride tucked under it
            // (the jump layer leaves the pose to the footwork), and land as the body lands, with the cleave.
            if(spec=="greatsword")
            {
                float up=float.NaN,down=float.NaN,feetDown=float.NaN,apex=0,tuck=0,jump=0;
                foreach(var f in fr){if(f.T<0||f.T>hitUntil)continue;if(f.Air>1e-4f){if(float.IsNaN(up))up=f.T;apex=Math.Max(apex,f.Air);tuck=Math.Max(tuck,Math.Min(f.Low0,f.Low1)-f.Air);jump=Math.Max(jump,f.JumpAir);}
                    else if(!float.IsNaN(up)&&float.IsNaN(down))down=f.T;
                    if(!float.IsNaN(up)&&float.IsNaN(feetDown)&&f.T>up+.1f&&(f.On[0]||f.On[1]))feetDown=f.T;}
                bool offThrough=true;foreach(var f in fr)if(f.T>up+3*Dt&&f.T<down-Dt&&(f.On[0]||f.On[1]))offThrough=false;
                Check(apex>.4f&&offThrough,"the greatsword's feet stay off the floor through its hop ("+(apex*100).ToString("0")+" cm)");
                Check(tuck>.1f&&jump<1e-4f,"the greatsword's feet ride tucked under the hopping body ("+(tuck*100).ToString("0")+" cm over the stance), the jump layer leaving them to the footwork");
                Check(Math.Abs(feetDown-down)<=2*Dt&&feetDown>=hitAt-.03f&&feetDown<=hitUntil,"the greatsword's feet land at "+feetDown.ToString("0.000")+" s as its body does ("+down.ToString("0.000")+"), with the cleave");
                notes.Add("the greatsword hops "+(apex*100).ToString("0")+" cm with its feet tucked "+(tuck*100).ToString("0")+" cm and lands them at "+feetDown.ToString("0.000"));
                // A body that comes down late (falling at 18 m/s2): the feet wait for it, then land pinned.
                var late=Drive(data,spec,true,false,out _,18);float bodyDown=float.NaN,lateFeet=float.NaN,lateSlip=0,lateSink=0;bool aloft=false;
                for(int i=0;i<late.Count;i++){var f=late[i];if(f.T<0||f.T>end)continue;if(f.Air>1e-4f)aloft=true;else if(aloft&&float.IsNaN(bodyDown))bodyDown=f.T;
                    if(aloft&&float.IsNaN(lateFeet)&&f.T>.1f&&(f.On[0]||f.On[1]))lateFeet=f.T;lateSink=Math.Max(lateSink,-Math.Min(f.Low0,f.Low1));
                    if(i>0)for(int k=0;k<2;k++)if(late[i-1].On[k]&&f.On[k]){float sp=float.MaxValue;for(int j=0;j<3;j++){Vector3 d=f.Pins[k*3+j]-late[i-1].Pins[k*3+j];d.Y=0;sp=Math.Min(sp,d.Length()/Dt);}lateSlip=Math.Max(lateSlip,sp);}}
                Check(bodyDown>hitAt+.05f&&Math.Abs(lateFeet-bodyDown)<=2*Dt&&lateSlip<=.35f&&lateSink<=.005f,"a greatsword coming down late ("+bodyDown.ToString("0.000")+" s) lands its feet with it ("+lateFeet.ToString("0.000")+" s), pinned ("+lateSlip.ToString("0.00")+" m/s, "+(lateSink*1000).ToString("0")+" mm)");
                notes.Add("coming down late ("+bodyDown.ToString("0.000")+" s) its feet wait and land with the body ("+lateFeet.ToString("0.000")+" s, "+lateSlip.ToString("0.00")+" m/s)");
            }
            // A swing not from a sprint keeps its cut; a chain after a sprint attack plays the next cut from the take's pose.
            var plain=Drive(data,spec,false,false,out var ps);foreach(var f in plain)if(f.T>=.05f&&f.T<ps.Until-1-.02f)Check(f.State.StartsWith("cut"),spec+" plain swing keeps its cut ("+f.State+" at "+f.T.ToString("0.000")+")");
            var chained=Drive(data,spec,true,true,out var c0);string after=null;foreach(var f in chained)if(f.T>c0.ChainAt-1+.05f){after=f.State;break;}
            Check(after!=null&&after.StartsWith("cut"),spec+" chains into "+after+" after its sprint attack");
        }
        Check(worstSlip<=.35f&&worstSink<=.005f,"sprint attacks keep their soles on the floor pinned ("+worstSlip.ToString("0.00")+" m/s at most, "+slipAt+"; "+(worstSink*1000).ToString("0")+" mm into it)");
        log.Add("a swing entered from a sprint plays the weapon's take through its swing ("+string.Join(", ",notes)+", each within two frames of the hit window), its soles pinned (at most "+worstSlip.ToString("0.00")+" m/s, "+(worstSink*1000).ToString("0")+" mm into the floor); swings not from a sprint keep their cuts and a chain after one plays the next cut");
    }
    // ---- the hooks: the arena rigs, the first-person view, the effects and the sounds read the same rules ----
    private static void Wiring(string root,List<string> log)
    {
        string Read(string f)=>File.ReadAllText(Path.Combine(root,f)).Replace("\r\n","\n");
        string mm=Read("Assets/ChipArena/Core/MeleeMotion.cs"),ca=Read("Assets/ChipArena/Runtime/CapturedAnimation.cs"),fx=Read("Assets/ChipArena/Runtime/NativeAirAttackFxPresentation.cs");
        Check(mm.Contains("JumpLayer.Leap=leapLive;")&&mm.Contains("p.SprintAttack?0:p.AirAttack")&&mm.Contains("NativeSprintAttackRules.ClipTime(clip,p.Spec,"),"the rig hands the leap to the footwork, never reads a sprint attack as a jump attack and plays the twin's clip time");
        Check(ca.Contains("NativeSprintAttackRules.ClipTime(clip,a.SourceSpec,")&&ca.Contains("Plays(a.SourceSpec,a.SprintAttack,false)")&&ca.Contains("a.AirAttack?1:0,time,blocked"),"the first-person view keeps the take through the hop and plays the twin");
        // (the view, the jump-attack impact and its sound take Astra's replicated air flag, never set on a sprint attack)
        Check(fx.Split("a.AirAttack?1:0,").Length==3,"the jump attack's impact and sound never fire for a sprint attack's landing");
        Check(Read("Assets/ChipArena/Runtime/ArenaPresentation.cs").Contains("SprintAttackFx(arena,fields);")&&Read("Assets/ChipArena/Runtime/NativeEffectsAudio.cs").Contains("SprintAttackCues(arena);"),"the effects and the cues are wired");
        log.Add("hooks: the arena rig hands the greatsword's hop to the footwork (the jump layer's leap hook), the rig, first-person view, jump-attack effects and sounds never read a sprint attack as a jump attack, the twin plays in both views, the effects and cues are wired");
    }
    // ---- the effects' watch and math ----
    private static void Fx(List<string> log)
    {
        foreach(var spec in Specs)
        {
            // The authority's greatsword hops from the swing's start: the watch still sees the sprint attack, and its landing
            // ring goes live once it is down inside the hit window.
            bool leaps=NativeSprintAttackRules.Leaps(spec);var w=new NativeSprintAttackWatch();float at=1,hitAt=1.48f,hitUntil=1.64f,land=1.467f;
            for(float t=.5f;t<2.4f;t+=Dt){bool live=t>=at,window=live&&t<=hitUntil;w.Observe(live?at:-100,live,window?hitAt:-100,window?hitUntil:-100,!(leaps&&t>=at&&t<land),spec,t);}
            Check(w.Entries==1&&w.Impacts==(leaps?1:0),spec+" sprint attack fires "+w.Entries+" entries and "+w.Impacts+" landing rings");
            var n=new NativeSprintAttackWatch();for(float t=.5f;t<2.4f;t+=Dt)n.Observe(t>=at?at:-100,false,hitAt,hitUntil,true,spec,t);Check(n.Entries==0&&n.Impacts==0,spec+" plain swing fires no sprint effect");
            var o=new NativeSprintAttackWatch();for(float t=.5f;t<2.4f;t+=Dt)o.Observe(t>=at?at:-100,true,hitAt,hitUntil,true,spec,t,true);Check(o.Entries==0,spec+" sprint attack owned by a special fires none");
            // Off a ledge the greatsword is still falling through its window: no ring, no landing effect.
            if(leaps){var l=new NativeSprintAttackWatch();for(float t=.5f;t<2.4f;t+=Dt){bool live=t>=at,window=live&&t<=hitUntil;l.Observe(live?at:-100,live,window?hitAt:-100,window?hitUntil:-100,!(t>=at&&t<2.2f),spec,t);}Check(l.Entries==1&&l.Impacts==0,"a greatsword still in the air through its window fires no landing ring");}
        }
        Vector3 feet=new Vector3(2,1,3);float floor=1;int kicks=0,streaks=0,puffs=0,motes=0;float ringOut=0;
        for(float age=0;age<.6f;age+=Dt)
        {
            for(int k=0;k<NativeSprintAttackFx.Burst;k++)if(NativeSprintAttackFx.Kick(k,NativeSprintAttackFx.Burst,5,age,feet,30,floor,out var p,out float size,out float op)){kicks++;Check(p.Y-size*.3f>floor&&op>=0&&op<=.5f,"an entry puff stays above the floor");}
            for(int k=0;k<NativeSprintAttackFx.Streaks;k++)if(NativeSprintAttackFx.Streak(k,NativeSprintAttackFx.Streaks,5,age/.6f,feet,30,out var from,out var along,out float a)){streaks++;Check(from.Y>floor+.3f&&(from+along).Y>floor+.3f&&a>=0&&a<=1,"a speed streak stays at body height");}
            for(int k=0;k<NativeSprintAttackFx.Puffs;k++)if(NativeSprintAttackFx.Puff(k,NativeSprintAttackFx.Puffs,5,age,feet,floor,out var q,out float s2,out float o2)){puffs++;Vector3 d=q-feet;d.Y=0;Check(q.Y-s2*.3f>floor&&o2>=0&&d.Length()<=NativeSprintAttackRules.RingRadius*1.05f+1e-3f,"a landing-ring puff stays above the floor, inside the ring");}
            for(int k=0;k<NativeSprintAttackFx.Motes;k++)if(NativeSprintAttackFx.Mote(k,NativeSprintAttackFx.Motes,age,feet,floor,out var m,out float s3,out float a3)){motes++;Vector3 d=m-feet;d.Y=0;ringOut=Math.Max(ringOut,d.Length());Check(m.Y>floor&&a3>=0&&a3<=1&&d.Length()<=NativeSprintAttackRules.RingRadius+1e-3f,"a landing-ring mote stays over the floor, inside the ring");}
        }
        Check(kicks>0&&streaks>0&&puffs>0&&motes>0,"the effects draw");
        Check(ringOut>NativeSprintAttackRules.RingRadius*.9f&&Math.Abs(NativeSprintAttackRules.RingRadius-1.6f)<1e-4f,"the landing ring spreads to the authority's 1.6 m ("+ringOut.ToString("0.00")+" m)");
        Check(NativeSprintAttackFx.Count(NativeSprintAttackFx.Burst,NativeSprintAttackFx.ReducedBurst,.3f,false)<NativeSprintAttackFx.Burst&&NativeSprintAttackFx.Count(NativeSprintAttackFx.Motes,NativeSprintAttackFx.ReducedMotes,1,true)<NativeSprintAttackFx.Motes,"reduced effects and Low graphics thin the effects");
        // The twin's crescents (NativeSlashFx): the first over the window's first half, the trailing one over its second, the
        // other way round; a plain dagger swing keeps its trailing copy a quarter window behind, the same way.
        {
            var s=new NativeMeleeSwing{Spec="daggers",Index=1,At=1,HitAt=1.12f,HitUntil=1.36f,Yaw=0,Arc=2,SprintAttack=true};NativeSlashFx.Look("daggers",out var look);
            bool f1=NativeSlashFx.Frame(s,look,1.2f,false,out float t1,out float h1,out _,out _),f2=NativeSlashFx.Frame(s,look,1.2f,true,out _,out _,out _,out _);
            bool g2=NativeSlashFx.Frame(s,look,1.33f,true,out float t2,out float h2,out _,out _),g1=NativeSlashFx.Frame(s,look,1.33f+look.Fade,false,out _,out _,out _,out _);
            Check(f1&&!f2&&g2&&!g1&&Math.Sign(h1-t1)==-Math.Sign(h2-t2)&&Math.Abs(h1-t1)>.05f&&Math.Abs(h2-t2)>.05f,"the twin's two crescents draw its two sweeps, opposed");
            s.SprintAttack=false;bool p2=NativeSlashFx.Frame(s,look,1.32f,true,out float t3,out float h3,out _,out _);NativeSlashFx.Frame(s,look,1.32f,false,out float t4,out float h4,out _,out _);
            Check(p2&&Math.Abs(h3-t3)>.05f&&Math.Sign(h3-t3)==Math.Sign(h4-t4),"a plain dagger swing keeps its trailing copy, the same way");
        }
        log.Add("the effects' watch fires one entry dust burst per sprint attack (also as the greatsword already hops; none for a plain swing or one a special owns) and the greatsword's one landing ring as it is down in its window (none still falling); the entry dust and the ring's dust and light stay over the floor, the ring spreading to the authority's 1.6 m, the speed streaks at body height; the twin's crescents draw its two opposed sweeps; reduced effects and Low graphics thin them");
    }
}
