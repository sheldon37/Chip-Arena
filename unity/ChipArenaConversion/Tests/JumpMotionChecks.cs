using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// The per-weapon jump animation (owner 2026-10-12: "can we add jump animations? (Of course, changes based on weapon. Daggers
/// can double-jump)"; Core/NativeJumpMotion, NativeJumpFx, NativePackJumps.Generated): the exported pack tracks match
/// tools/pack-jumps-provenance.json; each weapon takes its pack set; phases read from the presented root on the host and on a
/// guest (flags a frame ahead, 20 Hz snapshots, stepped 60 Hz prediction) at 30-240 Hz with no false air jump and one air
/// jump per impulse; every route rises, tucks, falls and lands with the ankles above the floor, the greatsword keeps both
/// hands, and the pose returns exactly to the route's own once landed; the dagger flip turns once and lands upright (also
/// when cut short); juggles, knockdowns, rolls and stuns own the pilot; the first-person kick; the air burst stays off the
/// floor and thins with the quality; no allocation per frame; the hooks. Presentation only: no jump physics, input, network
/// or bot change (ArenaWorld, NativePrediction and NativeArena are untouched).
/// </summary>
internal static class JumpMotionChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Jump motion: "+what);}
    public static void Run(string root)
    {
        var log=new List<string>();
        Clips(root,log);Mapping(log);Tracking(log);Poses(log);Flip(log);Priority(log);View(log);Fx(log);Allocation(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS jump motion: "+line);
    }
    // ---- the exported tracks ----
    private static void Clips(string root,List<string> log)
    {
        var names=NativePackJumps.Names;var slots=NativePackJumps.Slots;
        Check(string.Join(",",names)=="sword,daggers,greatsword,shield,shooting,staff,unarmed"&&NativePackJumps.Sets.Length==names.Length,"seven jump sets in NativeJumpRules order");
        Check(string.Join(",",slots)=="guard,jump,fall,land,flip"&&NativeJumpRules.Guard==0&&NativeJumpRules.Jump==1&&NativeJumpRules.Fall==2&&NativeJumpRules.Land==3&&NativeJumpRules.Flip==4,"slot order matches NativeJumpRules");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-jumps-provenance.json")));
        var prov=new Dictionary<string,JsonElement>();foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())prov[c.GetProperty("clip").GetString()]=c;
        Check(prov.Count==names.Length*slots.Length&&doc.RootElement.GetProperty("channels").GetInt32()==CapturedClip.Channels,"the provenance lists every clip");
        int frames=0;
        for(int s=0;s<names.Length;s++)for(int k=0;k<slots.Length;k++)
        {
            var c=NativePackJumps.Sets[s][k];string key=names[s]+"/"+slots[k];Check(c!=null&&c.Name==key&&NativeJumpRules.Clip(s,k)==c,"clip "+key+" in place");
            var p=prov[key];Check(p.GetProperty("frames").GetInt32()==c.Frames&&p.GetProperty("fps").GetInt32()==c.Fps&&p.GetProperty("source").GetString()==c.Source&&p.GetProperty("loop").GetBoolean()==c.Loop,key+": size and source match the provenance");
            string sha=Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(c.Data))).ToLowerInvariant();Check(sha==p.GetProperty("sha256").GetString(),key+": track is the exporter's output");
            var a=c.Decode();var b=new CapturedClip(c.Name,c.Source,c.Fps,c.Frames,c.Loop,c.Marks,c.Pace,c.Data).Decode();
            for(int i=0;i<a.Length;i++){Check(BitConverter.SingleToInt32Bits(a[i])==BitConverter.SingleToInt32Bits(b[i]),key+": deterministic decode");Check(MathUtil.Finite(a[i])&&Math.Abs(a[i])<4,key+": finite, bounded channel");}
            if(k==NativeJumpRules.Guard)Check(c.Frames==2&&!c.Loop&&c.Marks.Length==0,key+": the guard reference is one frame");
            else if(k==NativeJumpRules.Fall)Check(c.Loop&&c.Marks.Length==0,key+": the fall loops");
            else if(k==NativeJumpRules.Jump)Check(!c.Loop&&c.Marks.Length==4&&c.Marks[0]<c.Marks[1]&&c.Marks[1]<c.Marks[2]&&c.Marks[2]<c.Marks[3]&&Math.Abs(c.Marks[3]-c.Duration)<1e-3f,key+": marks [crouch, push-off, tuck, end]");
            else if(k==NativeJumpRules.Land)Check(!c.Loop&&c.Marks.Length==3&&c.Marks[0]==0&&c.Marks[1]>0&&c.Marks[1]<c.Marks[2]&&Math.Abs(c.Marks[2]-c.Duration)<1e-3f,key+": marks [start, deepest, end]");
            else Check(!c.Loop&&c.Duration>.4f&&c.Duration<.5f,key+": the flip lasts about .45 s");
            frames+=c.Frames;
        }
        foreach(var f in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))
        {
            string rel=f.Substring(root.Length).Replace('\\','/');if(rel.Contains("/bin/")||rel.Contains("/obj/")||rel.Contains("/Library/"))continue;
            Check(!f.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)&&!f.EndsWith(".fbx.meta",StringComparison.OrdinalIgnoreCase),"no FBX in the project ("+rel+")");
        }
        log.Add(names.Length*slots.Length+" clips ("+frames+" frames) match the exporter's provenance (SHA-256 per track), decode bit-identically, finite and bounded; no pack FBX or .meta in the project");
    }
    private static void Mapping(List<string> log)
    {
        var want=new Dictionary<string,int>{{"sword",NativeJumpRules.Sword},{"daggers",NativeJumpRules.Daggers},{"greatsword",NativeJumpRules.Greatsword},{"shield",NativeJumpRules.Shield},
            {"rifle",NativeJumpRules.Shooting},{"machinegun",NativeJumpRules.Shooting},{"bow",NativeJumpRules.Unarmed},{"pistol",NativeJumpRules.Unarmed},{"emperor",NativeJumpRules.Unarmed},{"ansatsuken",NativeJumpRules.Unarmed},{"cyberlord",NativeJumpRules.Unarmed},
            {"fire",NativeJumpRules.Staff},{"water",NativeJumpRules.Staff},{"lightning",NativeJumpRules.Staff},{"darkness",NativeJumpRules.Staff},{"leet",NativeJumpRules.Staff},{"siren",NativeJumpRules.None}};
        foreach(var kv in want)Check(NativeJumpRules.SetFor(kv.Key)==kv.Value,kv.Key+" takes its pack set");
        Check(NativeJumpRules.SetFor(null)==NativeJumpRules.None&&NativeJumpRules.Clip(NativeJumpRules.None,1)==null&&NativeJumpRules.Clip(0,9)==null,"unknown specs and slots have no clip");
        Check(NativePackJumps.Sets[NativeJumpRules.Greatsword][1].Source.StartsWith("2Hand-Sword")&&NativePackJumps.Sets[NativeJumpRules.Shooting][1].Source.StartsWith("2Hand-Shooting")&&NativePackJumps.Sets[NativeJumpRules.Staff][1].Source.StartsWith("2Hand-Staff")&&NativePackJumps.Sets[NativeJumpRules.Unarmed][1].Source.StartsWith("Unarmed")&&NativePackJumps.Sets[NativeJumpRules.Shield][1].Source.StartsWith("Armed"),"the sets read their pack folders");
        Check(NativeJumpRules.Flips("daggers")&&NativeJumpRules.Flips("bow")&&!NativeJumpRules.Flips("sword")&&!NativeJumpRules.Flips("emperor"),"daggers and Bow share the air-jump flip");
        log.Add("pack sets: sword/daggers/shield Armed (daggers reverse grip, shield arm on its guard), greatsword 2Hand-Sword, rifle/machine gun 2Hand-Shooting, pistol, the DEV fighters and the Cyber Lord Unarmed, magic and L33t H4x 2Hand-Staff, Siren none (her own air tuck)");
    }

    // ---- a simulated jump (the authority's 60 Hz tick: jump 8.5, gravity 23) presented as a host or a guest draws it ----
    private sealed class Tick{public float T,Y,Vy,Floor;public bool G;}
    private sealed class Sim{public readonly List<Tick> Ticks=new List<Tick>();public float Contact=-1,Takeoff=-1;}
    // impulses: (time, vertical speed); ledge: walk off a 3 m ledge at `ledgeAt`; floorAt optional (a platform).
    private static Sim Simulate(float until,(float at,float vy)[] impulses,float ledgeAt=-1,float platformAt=-1)
    {
        var run=new Sim();float y=ledgeAt>=0?3:0,vy=0;bool g=true;
        for(int f=0;f*1/60f<=until;f++)
        {
            float t=f/60f,floor=ledgeAt>=0&&t<ledgeAt?3:platformAt>=0&&t>platformAt?.6f:0;
            foreach(var i in impulses)if(Math.Abs(t-i.at)<.5f/60){vy=i.vy;g=false;if(run.Takeoff<0)run.Takeoff=t;}
            if(!g){vy-=23/60f;y+=vy/60f;if(y<=floor&&vy<=0){y=floor;vy=0;g=true;run.Contact=t;}}
            else if(y>floor+.001f){g=false;vy=0;if(run.Takeoff<0)run.Takeoff=t;}else y=floor;
            run.Ticks.Add(new Tick{T=t,Y=y,Vy=vy,Floor=floor,G=g});
        }
        return run;
    }
    // A presented frame at render time t: the host interpolates its rigidbody between ticks (flags of the newest tick); a guest
    // draws remote pilots .1 s behind the newest 20 Hz frame (interpolated) while the flags are the newest frame's; `stepped`
    // presents the newest tick unchanged (a guest's own predicted pilot moves at the tick rate).
    // `lag`: the host's interpolation shows the previous tick at the newest tick's time.
    private static void Present(Sim r,float t,bool guest,bool stepped,out float y,out bool grounded,out float floor)
    {
        if(guest)
        {
            int newest=(int)Math.Floor(t*20);float shown=t-.1f;int a=(int)Math.Floor(shown*20);float u=shown*20-a;
            Tick A=TickAt(r,a/20f),B=TickAt(r,(a+1)/20f),N=TickAt(r,newest/20f);y=A.Y+(B.Y-A.Y)*u;grounded=N.G;floor=A.Floor;return;
        }
        // (Unity's rigidbody interpolation runs a tick behind: from the previous tick to the newest one.)
        int k=(int)Math.Floor(t*60);Tick p=TickAt(r,(k-1)/60f),q=TickAt(r,k/60f);
        if(stepped){y=q.Y;grounded=q.G;floor=q.Floor;return;}
        float w=t*60-k;y=p.Y+(q.Y-p.Y)*w;grounded=q.G;floor=p.Floor;
    }
    private static Tick TickAt(Sim r,float t){int i=(int)Math.Round(t*60);return r.Ticks[Math.Max(0,Math.Min(r.Ticks.Count-1,i))];}
    private static void Tracking(List<string> log)
    {
        int cases=0;float worstLand=0,worstTake=0;
        foreach(bool guest in new[]{false,true})foreach(float fps in new[]{30f,60f,144f,240f})foreach(bool stepped in guest?new[]{false}:new[]{false,true})
        {
            string what=(guest?"guest":"host")+(stepped?" stepped":"")+" at "+fps+" Hz";float lag=guest?.1f:0;
            // A standing jump: one takeoff, one landing at the presented contact (never before it), no air jump.
            var r=Simulate(2.4f,new[]{(.5f,8.5f)});var t=new NativeJumpTrack();float landSeen=-1,takeSeen=-1,touch=-1,rise=-1,high=0;
            for(float time=0;time<2.4f;time+=1/fps){Present(r,time,guest,stepped,out float y,out bool g,out _);t.Observe(time,g,y,false);if(rise<0&&y>1e-4f)rise=time;if(t.Airborne&&takeSeen<0)takeSeen=time;if(t.Landings>0&&landSeen<0)landSeen=time;high=Math.Max(high,y);if(touch<0&&high>.5f&&y<=1e-4f)touch=time;}
            Check(t.Takeoffs==1&&t.Landings==1&&t.AirJumps==0&&t.Jumped,what+": one jump, one landing, no air jump ("+t.Takeoffs+"/"+t.Landings+"/"+t.AirJumps+")");
            Check(landSeen>=touch-1e-4f&&landSeen-touch<=2/fps+.01f,what+": the landing follows the presented contact ("+(landSeen-touch)+" s)");
            Check(takeSeen>=rise-1e-4f&&takeSeen-rise<=2/fps+.01f,what+": the takeoff follows the presented rise ("+(takeSeen-rise)+" s)");
            Check(t.FallSpeed>7&&t.FallSpeed<9.5f,what+": the fall speed of a standing jump ("+t.FallSpeed+")");
            worstLand=Math.Max(worstLand,landSeen-touch);worstTake=Math.Max(worstTake,takeSeen-rise);
            // A double jump: exactly one air jump, near the presented impulse.
            r=Simulate(2.6f,new[]{(.5f,8.5f),(.82f,7.65f)});t=new NativeJumpTrack();float airSeen=-1;
            for(float time=0;time<2.6f;time+=1/fps){Present(r,time,guest,stepped,out float y,out bool g,out _);t.Observe(time,g,y,false);if(t.AirJumps>0&&airSeen<0)airSeen=time;}
            Check(t.AirJumps==1&&t.Takeoffs==1&&t.Landings==1,what+": one air jump for one impulse ("+t.AirJumps+")");
            float impulse=(float)Math.Round(.82f*60)/60;
            Check(airSeen>=impulse+lag-1/60f-1e-3f&&airSeen-(impulse+lag)<.09f+2/fps,what+": the air jump shows with the presented impulse ("+(airSeen-impulse-lag)+" s)");
            // Blocked (a juggle's launch): no air jump.
            t=new NativeJumpTrack();for(float time=0;time<2.6f;time+=1/fps){Present(r,time,guest,stepped,out float y,out bool g,out _);t.Observe(time,g,y,time>.8f&&time<1.1f);}
            Check(t.AirJumps==0,what+": a blocked impulse is no air jump");
            // Off a 3 m ledge: a fall (not a jump), landing with the drop's speed; onto a platform: a short landing, no air jump.
            r=Simulate(2.4f,Array.Empty<(float,float)>(),.6f);t=new NativeJumpTrack();
            for(float time=0;time<2.4f;time+=1/fps){Present(r,time,guest,stepped,out float y,out bool g,out _);t.Observe(time,g,y,false);}
            Check(t.Takeoffs==1&&!t.Jumped&&t.Landings==1&&t.AirJumps==0&&t.FallSpeed>10&&t.FallSpeed<12.5f,what+": a ledge fall ("+t.Jumped+" "+t.FallSpeed+")");
            r=Simulate(2.4f,new[]{(.5f,8.5f)},-1,.6f);t=new NativeJumpTrack();
            for(float time=0;time<2.4f;time+=1/fps){Present(r,time,guest,stepped,out float y,out bool g,out _);t.Observe(time,g,y,false);}
            Check(t.Takeoffs==1&&t.Landings==1&&t.AirJumps==0,what+": a jump onto a platform");
            cases++;
        }
        // A rewind or a gap restarts on the current state without an event.
        var k=new NativeJumpTrack();k.Observe(5,false,2,false);k.Observe(5.02f,false,1.9f,false);int takeoffs=k.Takeoffs;k.Observe(3,true,0,false);
        Check(k.Has&&!k.Airborne&&k.Takeoffs==0&&k.Landings==0&&takeoffs==0,"a rewind restarts the tracker on the current state");
        k.Observe(float.NaN,true,0,false);Check(k.Has&&!k.Airborne,"non-finite input is ignored");
        // The future explicit signal: once seen, air jumps come only from it.
        var dj=Simulate(2.6f,new[]{(.5f,8.5f),(.82f,7.65f)});var e=new NativeJumpTrack();float signalled=0;
        for(float time=0;time<2.6f;time+=1/60f){Present(dj,time,false,false,out float y,out bool g,out _);e.Observe(time,g,y,false);e.AirJumpSignal(time>=1.2f?1.2f:-100);if(e.AirJumps>0&&signalled==0)signalled=e.AirJumpAt;}
        Check(e.Explicit&&e.AirJumps==1&&Math.Abs(signalled-1.2f)<1e-4f,"an explicit air-jump time replaces the inference ("+e.AirJumps+" at "+signalled+")");
        log.Add(cases+" presentations (host interpolated and stepped, guest .1 s behind its flags; 30-240 Hz): one takeoff and landing per jump, the landing "+(worstLand*1000).ToString("0")+" ms at most after the presented contact (never before), the takeoff "+(worstTake*1000).ToString("0")+" ms after the presented rise, no false air jump (jumps, ledge falls, platforms), one per impulse, none while blocked; rewinds restart; an explicit AirJumpAt hook replaces the inference");
    }

    // ---- the routes (Runtime/CapturedAnimation's arena rigs) with and without the layer ----
    private sealed class Pilot
    {
        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly Actor A;public MeleeMotion Melee;public GunMotion Gun;public MagicMotion Magic;
        public readonly NativeJumpMotion Jump;private readonly MeleeView v=new MeleeView();private readonly NativeJuggleTrack juggle=new NativeJuggleTrack();private readonly float[] channels=new float[13];
        private float last=-100;public float RootY;public readonly string Route;
        public Pilot(string spec,bool layer)
        {
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;int mv=spec=="fire"?1:spec=="water"?2:spec=="lightning"?3:spec=="darkness"?4:0;
            A=new Actor{Id=4,SourceSpec=spec,Weapon=w,MagicVisual=mv,Grounded=true,Hp=100,CastRate=1,Ammo=w==WeaponId.Sword?0:GunRules.Get(w).Magazine,VisualSet=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0};
            Jump=layer?new NativeJumpMotion():null;Route=GunRules.Ranged(w)?"gun":mv>0||spec=="leet"?"magic":spec=="emperor"||spec=="ansatsuken"?"fighter":"melee";
        }
        public MeleeRig Rig=>Melee?.Rig??Gun?.Rig??Magic.Rig;
        public void Frame(float t,float y,float vy,bool grounded,float vz=0)
        {
            A.Position=new V3(0,y,0);A.Velocity=new V3(0,vy,vz);A.Grounded=grounded;RootY=y+.865f;
            float dt=last<-90||t<last||t-last>.35f?1/60f:Math.Min(.1f,t-last);last=t;
            if(Route=="gun"){Gun??=new GunMotion();Gun.JumpLayer=Jump;Gun.Rig.GroupPosition=new Vector3(0,RootY,0);Gun.Rig.GroupYaw=0;Gun.Pose(A,t,Pose,false,juggle,out _);Gun.Finish(Pose,t);return;}
            if(Route=="magic"){Magic??=new MagicMotion();Magic.JumpLayer=Jump;Magic.Rig.GroupPosition=new Vector3(0,RootY,0);Magic.Rig.GroupYaw=0;Magic.Pose(A,t,Pose,channels,0,false,juggle,out _);Magic.Finish(Pose,t);return;}
            Melee??=new MeleeMotion();Melee.JumpLayer=Jump;v.Id="4";v.Spec=A.SourceSpec;v.Vel=new Vector3(0,vy,vz);v.Grounded=grounded;v.Hp=A.Hp;v.HitStunUntil=A.StunUntil;v.Juggle=A.JuggleUntil>t;v.Down=false;
            v.RollAt=A.RollAt;v.RollUntil=A.RollAt>-90?A.RollAt+.46f:-100;v.LastAttack=-100;v.SwingAt=v.SwingUntil=float.NaN;v.LastBlock=float.NaN;v.ActionKind=null;
            Melee.Rig.GroupPosition=new Vector3(0,RootY,0);Melee.Rig.GroupYaw=0;
            if(Route=="fighter"){Melee.FighterBase(v,t,dt);Melee.FighterFinish(v,t,dt);}else Melee.Sample(v,t,dt,r=>{});
            Melee.Rig.WritePose(Pose);Pose.World();
        }
        public float World(int bone)=>RootY+Pose.WorldPosition[bone].Y;
    }
    private static float Diff(CapturedRig.Pose a,CapturedRig.Pose b)
    {
        float d=0;for(int i=0;i<CapturedRig.Bones.Length;i++){d=Math.Max(d,Vector3.Distance(a.WorldPosition[i],b.WorldPosition[i]));Quaternion p=a.WorldRotation[i],q=b.WorldRotation[i];if(Quaternion.Dot(p,q)<0)q=-q;d=Math.Max(d,Math.Max(Math.Max(Math.Abs(p.X-q.X),Math.Abs(p.Y-q.Y)),Math.Max(Math.Abs(p.Z-q.Z),Math.Abs(p.W-q.W))));}
        return d;
    }
    private static readonly string[] Specs={"sword","daggers","greatsword","shield","pistol","rifle","machinegun","fire","leet","emperor","ansatsuken"};
    private static void Poses(List<string> log)
    {
        float lowest=1,worstSettle=0,worstGrip=0;int frames=0;var seen=new HashSet<string>();
        foreach(var spec in Specs)foreach(var scene in new[]{"stand","run","ledge"})
        {
            var run=scene=="ledge"?Simulate(3,Array.Empty<(float,float)>(),1.0f):Simulate(3,new[]{(1.0f,8.5f)});float vz=scene=="stand"?0:scene=="run"?6.8f:3;
            var on=new Pilot(spec,true);var off=new Pilot(spec,false);bool air=false,landed=false;float settled=float.MaxValue;
            foreach(var tick in run.Ticks)
            {
                float t=tick.T;on.Frame(t,tick.Y,tick.Vy,tick.G,vz);off.Frame(t,tick.Y,tick.Vy,tick.G,vz);frames++;
                for(int b=0;b<CapturedRig.Bones.Length;b++)Check(MathUtil.Finite(on.Pose.WorldPosition[b].X+on.Pose.WorldPosition[b].Y+on.Pose.WorldPosition[b].Z),spec+" "+scene+": finite bones");
                // The foot box's sole is .07 under the ankle, the toe box's .05 under the toe joint. In the air (and landing) the
                // layer keeps both on or above the floor; on the ground it never sets a foot lower than the route's own gait.
                float ankle=Math.Min(on.World(CapturedRig.LFoot),on.World(CapturedRig.RFoot))-tick.Floor,toe=Math.Min(on.World(CapturedRig.LToe),on.World(CapturedRig.RToe))-tick.Floor;
                float own=Math.Min(Math.Min(off.World(CapturedRig.LFoot),off.World(CapturedRig.RFoot))-tick.Floor-.07f,Math.Min(off.World(CapturedRig.LToe),off.World(CapturedRig.RToe))-tick.Floor-.05f);
                float margin=Math.Min(ankle-.07f,toe-.05f),allowed=tick.G?Math.Min(-.005f,own-.002f):-.005f;if(!tick.G)lowest=Math.Min(lowest,margin);
                Check(margin>=allowed,spec+" "+scene+" at "+t.ToString("0.000")+": a foot below the floor (ankle "+ankle+", toe "+toe+", gait's own "+own+", "+on.Jump.State+")");
                if(t<run.Takeoff-.02f){float d0=Diff(on.Pose,off.Pose);Check(d0==0,spec+" "+scene+": the layer leaves the ground pose alone before any jump ("+d0+" at "+t+", "+on.Jump.State+" air "+on.Jump.Track.Airborne+")");}
                if(!tick.G&&t>run.Takeoff+.25f&&t<run.Contact-.02f){air=true;Check(on.Jump.AirWeight>.99f,spec+" "+scene+": the air pose is in full");seen.Add(on.Jump.State);
                    if(spec=="greatsword"){var r=on.Rig;Vector3 grip=r.ChainPoint(CapturedRig.RHand,-1)+Vector3.Transform(r.Reached(MeleeRig.SupportGrip),r.ChainQuat(CapturedRig.RHand,-1));float gap=Vector3.Distance(grip,r.ChainPoint(CapturedRig.LHand,-1));worstGrip=Math.Max(worstGrip,gap);Check(gap<.07f,"greatsword "+scene+": the support hand keeps the hilt in the air ("+gap+")");}}
                if(run.Contact>0&&t>=run.Contact&&t<run.Contact+.12f&&on.Jump.State==NativeJumpMotion.Landing)landed=true;
                if(run.Contact>0&&t>run.Contact+.75f){float d=Diff(on.Pose,off.Pose);worstSettle=Math.Max(worstSettle,d);if(d<1e-5f)settled=Math.Min(settled,t-run.Contact);}
            }
            Check(air&&landed,spec+" "+scene+": an air pose and a landing ("+air+" "+landed+")");
            Check(worstSettle<1e-4f,spec+" "+scene+": the pose returns to the route's own after the landing ("+worstSettle+")");
        }
        Check(seen.Contains(NativeJumpMotion.Rise)&&seen.Contains(NativeJumpMotion.Apex)&&seen.Contains(NativeJumpMotion.Fall),"rise, apex and fall all show ("+string.Join(",",seen)+")");
        log.Add(Specs.Length+" weapons x standing, running and 3 m ledge jumps ("+frames+" frames): rise, apex, fall and landing, feet on or above the floor (in the air the lowest sole "+(lowest>-5e-5f&&lowest<5e-5f?0:lowest*1000).ToString("0.0")+" mm above it; on the ground never under the gait's own), the greatsword's support hand on the hilt in the air (worst "+(worstGrip*1000).ToString("0.0")+" mm), the ground pose untouched before a jump and exactly the route's own .75 s after the landing (worst "+worstSettle.ToString("0.0e0")+")");
    }
    private static void Flip(List<string> log)
    {
        // A dagger air jump flips once (head under the hips mid-flip) and lands upright; the sword's does not flip.
        var run=Simulate(3,new[]{(1.0f,8.5f),(1.32f,7.65f)});float peak=0,headUnder=0;bool flipped=false,upright=true;var swordRun=new Pilot("sword",true);
        var dag=new Pilot("daggers",true);
        foreach(var tick in run.Ticks)
        {
            dag.Frame(tick.T,tick.Y,tick.Vy,tick.G);swordRun.Frame(tick.T,tick.Y,tick.Vy,tick.G);
            peak=Math.Max(peak,dag.Jump.FlipAngle);if(dag.Jump.State==NativeJumpMotion.AirFlip)flipped=true;
            headUnder=Math.Max(headUnder,dag.World(CapturedRig.Pelvis)-dag.World(CapturedRig.Head));
            Check(swordRun.Jump.FlipAngle==0,"the sword never flips");
            if(tick.T>1.32f+NativeJumpRules.FlipSeconds+.02f){Check(dag.Jump.FlipAngle==0,"the flip ends by FlipSeconds");upright&=dag.World(CapturedRig.Head)>dag.World(CapturedRig.Pelvis)+.4f;}
        }
        Check(flipped&&peak>MeleeMath.TAU*.95f&&headUnder>.2f&&upright&&dag.Jump.Track.AirJumps==1&&swordRun.Jump.Track.AirJumps==1,"the dagger flip turns once ("+peak+" rad, head "+headUnder+" m under the hips) and ends upright");
        Check(Math.Abs(NativeJumpRules.FlipTurn(0))<1e-6f&&Math.Abs(NativeJumpRules.FlipTurn(1)-1)<1e-6f&&NativeJumpRules.FlipTurn(.85f)==1,"the flip's turn leaves and ends at rest");
        // Cut short by the landing: a low air jump; within .1 s of the landing the turn has finished or unwound and the body is upright.
        run=Simulate(3,new[]{(1.0f,8.5f),(1.71f,3.2f)});dag=new Pilot("daggers",true);float landTurn=-1;
        foreach(var tick in run.Ticks)
        {
            dag.Frame(tick.T,tick.Y,tick.Vy,tick.G);
            if(dag.Jump.Track.Landings>0&&tick.T>=dag.Jump.Track.LandAt&&landTurn<0&&dag.Jump.Track.LandAt>1.71f)landTurn=dag.Jump.FlipAngle;
            if(dag.Jump.Track.Landings>0&&dag.Jump.Track.LandAt>1.71f&&tick.T>dag.Jump.Track.LandAt+.1f)Check(dag.Jump.FlipAngle==0&&dag.World(CapturedRig.Head)>dag.World(CapturedRig.Pelvis)+.3f,"a flip cut short by the landing resolves upright within .1 s");
        }
        Check(landTurn>.3f,"the low air jump was mid-flip at the landing ("+landTurn+")");
        log.Add("the dagger air jump flips forward once ("+peak.ToString("0.00")+" rad over "+NativeJumpRules.FlipSeconds+" s, head "+(headUnder*100).ToString("0")+" cm under the hips mid-flip) and lands upright; cut short by a landing it resolves within .1 s; other weapons never flip");
    }
    private static void Priority(List<string> log)
    {
        // A juggle, a stun or a roll in the air owns the pilot: the layer's weight is 0 (the overlays pose it).
        var run=Simulate(3,new[]{(1.0f,8.5f)});
        foreach(var spec in new[]{"sword","rifle","fire","emperor"})foreach(var kind in new[]{"juggle","stun","roll","dead"})
        {
            var p=new Pilot(spec,true);
            foreach(var tick in run.Ticks)
            {
                bool owned=tick.T>1.2f&&tick.T<1.5f;
                if(kind=="juggle")p.A.JuggleUntil=owned?1.5f:0;else if(kind=="stun")p.A.StunUntil=owned?1.5f:0;else if(kind=="roll")p.A.RollAt=owned?1.2f:-100;else p.A.Hp=owned?0:100;
                p.Frame(tick.T,tick.Y,tick.Vy,tick.G);
                if(owned&&tick.T>1.21f)Check(p.Jump.Weight==0&&p.Jump.AirWeight==0,spec+": a "+kind+" owns the airborne pilot");
            }
        }
        log.Add("a juggle, stun, roll or death in the air drops the jump layer to 0 (sword, rifle, magic and fighter routes)");
    }
    private static void View(List<string> log)
    {
        Check(NativeJumpRules.ViewHop(true,false,0,8.5f)==NativeJumpRules.ViewTakeoffKick&&NativeJumpRules.ViewHop(true,false,0,1)==0&&NativeJumpRules.ViewHop(false,false,-2,7.65f)==NativeJumpRules.ViewAirJumpKick&&NativeJumpRules.ViewHop(false,false,3,2.6f)==0,"view hop: takeoff and air jump");
        Check(Math.Abs(NativeJumpRules.ViewHop(false,true,-12,0)-(NativeJumpRules.ViewLandKick+NativeJumpRules.ViewLandFall))<1e-6f&&NativeJumpRules.ViewHop(true,true,0,0)==0&&NativeJumpRules.ViewHop(false,false,float.NaN,1)==0,"view hop: landing by the fall speed, none standing or non-finite");
        // Through the view layer: the arena view (Input sets JumpKick) hops on the takeoff and the landing; raw inputs keep the
        // source exactly (the same landing spring, no hop).
        var kicked=new NativeViewSway();var plain=new NativeViewSway();float up=0,down=0;
        for(int f=0;f<120;f++)
        {
            float t=f/60f;bool air=f>=30&&f<74;float vy=air?8.5f-23*(t-.5f):0;
            var i=new NativeViewSwayInput{Pilot=1,Time=t,VelocityY=vy,Grounded=!air,Gait=true};var k=i;k.JumpKick=true;
            var pk=kicked.Sample(k);var pp=plain.Sample(i);
            if(f>=30&&f<50)up=Math.Min(up,kicked.Hop);if(f>=74)down=Math.Min(down,kicked.Hop);
            Check(plain.Hop==0&&Math.Abs(pk.Y-pp.Y-kicked.Hop)<1e-6f&&pk.X==pp.X&&pk.Roll==pp.Roll,"the hop only adds to the arena view's height");
        }
        Check(up<-.005f&&up>-.012f&&down<-.007f&&down>-.016f,"the arena view hops on the takeoff ("+up+") and the landing ("+down+")");
        var a=new Actor{Id=1,Velocity=new V3(0,8,0),Grounded=false};Check(NativeViewSway.Input(a,1,0,0,0).JumpKick,"the arena view input enables the hop");
        log.Add("first person: the hands and weapon dip "+(-up*1000).ToString("0.0")+" mm on a takeoff (an air jump a little more) and "+(-down*1000).ToString("0.0")+" mm on a standing jump's landing, over the source's landing kick; raw source inputs keep the source view layer exactly");
    }
    private static void Fx(List<string> log)
    {
        float lowest=10;int drawn=0;
        foreach(float floor in new[]{0f,3f})foreach(float height in new[]{.05f,.3f,1.4f})
        {
            var feet=new Vector3(2,floor+height,-1);var c=NativeJumpFx.Centre(feet,floor);Check(c.Y>=floor+NativeJumpFx.FloorClear-1e-5f,"the burst centre stays off the floor");
            for(float age=0;age<NativeJumpFx.Life;age+=1/120f)
            {
                for(int k=0;k<NativeJumpFx.Streaks;k++)if(NativeJumpFx.Streak(k,NativeJumpFx.Streaks,9,age,c,floor,out var head,out var dir,out float len,out float alpha))
                {
                    drawn++;float tail=head.Y-dir.Y*len;lowest=Math.Min(lowest,Math.Min(head.Y,tail)-floor);Check(Math.Min(head.Y,tail)>=floor+NativeJumpFx.FloorClear-1e-4f&&alpha>0&&alpha<=1&&Math.Abs(dir.Length()-1)<1e-4f,"a streak stays above the floor");
                    Check(Math.Abs(dir.Y)<.6f,"streaks flare out (no flat disc on the floor, no column)");
                }
                for(int k=0;k<NativeJumpFx.Puffs;k++)if(NativeJumpFx.Puff(k,NativeJumpFx.Puffs,9,age,feet,out var at,out float size,out float op))Check(at.Y-size*.5f>=feet.Y&&op>=0&&op<=NativeJumpFx.PuffOpacity,"the afterimage stays on the body");
            }
            Check(NativeJumpFx.Ring(0,out float s0,out float a0)&&NativeJumpFx.Ring(.2f,out float s1,out float a1)&&s1>s0&&a1<a0&&!NativeJumpFx.Ring(NativeJumpFx.RingLife,out _,out _),"the ring grows and fades");
        }
        Check(NativeJumpFx.Count(12,5,1,false)==12&&NativeJumpFx.Count(12,5,.3f,false)==5&&NativeJumpFx.Count(12,5,1,true)==6&&NativeJumpFx.Count(3,1,.3f,true)==1,"Reduced effects and Low graphics thin the burst");
        log.Add("the dagger air burst: a camera-facing ring and "+NativeJumpFx.Streaks+" flared streaks under the feet ("+drawn+" streak samples), "+(lowest*100).ToString("0")+" cm or more above the floor at any height, the afterimage on the body; Reduced effects and Low graphics thin it");
    }
    private static void Allocation(List<string> log)
    {
        var run=Simulate(3,new[]{(1.0f,8.5f),(1.32f,7.65f)});long used=0;
        foreach(var spec in new[]{"daggers","rifle","fire"})
        {
            var p=new Pilot(spec,true);foreach(var tick in run.Ticks)p.Frame(tick.T,tick.Y,tick.Vy,tick.G);
            p=new Pilot(spec,true);foreach(var tick in run.Ticks)p.Frame(tick.T-3.2f,tick.Y,tick.Vy,tick.G);
            long before=GC.GetAllocatedBytesForCurrentThread();foreach(var tick in run.Ticks)p.Frame(tick.T+5,tick.Y,tick.Vy,tick.G);used+=GC.GetAllocatedBytesForCurrentThread()-before;
        }
        var track=new NativeJumpTrack();long b2=GC.GetAllocatedBytesForCurrentThread();foreach(var tick in run.Ticks)track.Observe(tick.T,tick.G,tick.Y,false);used+=GC.GetAllocatedBytesForCurrentThread()-b2;
        Check(used==0,"no allocation per frame ("+used+" bytes)");
        log.Add("no allocation per frame through a double jump (daggers, rifle, magic routes) and the tracker");
    }
    private static void Hooks(string root,List<string> log)
    {
        string Read(string f)=>File.ReadAllText(Path.Combine(root,f));
        string mm=Read("Assets/ChipArena/Core/MeleeMotion.cs"),gm=Read("Assets/ChipArena/Core/GunMotion.cs"),mp=Read("Assets/ChipArena/Core/MagicPose.cs"),ca=Read("Assets/ChipArena/Runtime/CapturedAnimation.cs");
        Check(mm.Contains("JumpLayer?.Melee(r,p,time,dt);\n            HitLayer?.Layer(r,time);")&&mm.Contains("JumpLayer?.Fighter(Rig,p,time,dt);\n            HitLayer?.Layer(Rig,time);"),"MeleeMotion lays the jump after the action layers, before the hit layer and the ground pass");
        Check(gm.Contains("JumpLayer?.Ranged(r,a,v.Spec,time,dt,.4f);\n            UpdateAim(a,time,restart);")&&mp.Contains("JumpLayer?.Ranged(r,a,v.Spec,time,dt,.5f);"),"the gun hold and the magic layer pose over the jump");
        Check(ca.Contains("JumpLayer=>Hits!=null?jump??=new NativeJumpMotion():null;")&&ca.Contains("melee.JumpLayer=JumpLayer;")&&ca.Contains("gunMotion.JumpLayer=JumpLayer;")&&ca.Contains("magicMotion.JumpLayer=JumpLayer;"),"the arena rigs (host, guests, killcam, spectators) get the layer; intro, portrait, corpse and viewer rigs do not");
        Check(Read("Assets/ChipArena/Runtime/ArenaPresentation.cs").Contains("JumpFx(arena,fields);")&&Read("Assets/ChipArena/Runtime/NativeEffectsAudio.cs").Contains("MoveCues(arena);MoveFeelCues(arena);"),"the air burst and the flip whoosh are wired");
        foreach(var f in new[]{"Assets/ChipArena/Core/ArenaWorld.cs","Assets/ChipArena/Core/NativePrediction.cs"})Check(Read(f).Contains("NativeJumpSimulation.Try"),f+" uses the shared jump commitment after the simulation handoff");
        log.Add("hooks: melee, fighter, gun and magic routes; arena rigs only; the burst and whoosh; shared jump simulation and prediction commitment");
    }
}
