using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

/// <summary>
/// The movement chips' presentation (Core/NativeAirJumpFx, NativeJumpMotion's air step, Core/NativeChipIcons; Astra's chips and
/// stackable air jumps, PR #282): every weapon's air jump reads, once per spent charge (1st..6th), on the host and on a guest
/// (20 Hz, .1 s behind, flags of the newest frame) at 30-240 Hz; the Cyber Lord keeps its own boost and never bursts here; the
/// daggers and the Bow keep their burst exactly (a legacy re-run of the old presentation, call for call, bit for bit); a Kick
/// weapon's chained charges pop a little smaller and brighter, above the floor; Reduced effects, Low graphics and first person
/// thin or skip parts; the air step drives one knee and kicks the other back, alternating, without moving the root, the arms or
/// the greatsword's grip, and hands back to the route's own pose exactly; no allocation per frame; the five chips' card icons
/// resolve to pinned icons; the soft sound; the hooks; and no authority, network or content file reads any of it.
/// </summary>
internal static class MoveVisualChecks
{
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new InvalidOperationException("Move visuals: "+what);}
    private static readonly string[] Weapons={"sword","daggers","greatsword","shield","pistol","rifle","machinegun","bow","fire","water","lightning","darkness","leet","emperor","ansatsuken",CyberLordSpec.Id,"siren"};
    public static void Run(string root)
    {
        count=0;var log=new List<string>();
        Kinds(log);Charges(log);Legacy(log);Tiers(log);Allocation(log);Step(log);Icons(root,log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS move visuals: "+line);
        Console.WriteLine("PASS move visuals: "+count+" checks");
    }

    // ---- kinds and the per-charge look ----
    private static void Kinds(List<string> log)
    {
        foreach(var spec in Weapons)
        {
            int kind=NativeAirJumpLook.KindOf(spec);int want=spec==CyberLordSpec.Id?NativeAirJumpLook.Own:spec=="daggers"||spec=="bow"?NativeAirJumpLook.Flip:NativeAirJumpLook.Kick;
            Check(kind==want,spec+": its air-jump kind ("+kind+")");Check(NativeAirJumpLook.Bursts(spec)==(spec!=CyberLordSpec.Id)&&NativeAirJumpLook.Kicks(spec)==(want==NativeAirJumpLook.Kick),spec+": bursts and steps");
        }
        for(int c=1;c<=NativeJumpSimulation.MaxAirJumps;c++)
        {
            Check(NativeAirJumpLook.Scale(NativeAirJumpLook.Flip,c)==1&&NativeAirJumpLook.Gain(NativeAirJumpLook.Flip,c)==1&&NativeAirJumpLook.White(NativeAirJumpLook.Flip,c)==0,"a flip charge keeps the dagger's burst ("+c+")");
            if(c>1)Check(NativeAirJumpLook.Scale(NativeAirJumpLook.Kick,c)<NativeAirJumpLook.Scale(NativeAirJumpLook.Kick,c-1)&&NativeAirJumpLook.Gain(NativeAirJumpLook.Kick,c)>NativeAirJumpLook.Gain(NativeAirJumpLook.Kick,c-1)&&NativeAirJumpLook.White(NativeAirJumpLook.Kick,c)>NativeAirJumpLook.White(NativeAirJumpLook.Kick,c-1),"each chained Kick charge pops smaller, brighter and whiter ("+c+")");
            if(c>1)Check(Math.Abs(NativeAirJumpLook.KickPitch(c)/NativeAirJumpLook.KickPitch(c-1)-Math.Pow(2,1/12.0))<1e-4,"the Kick sound rises a semitone per charge");
            Check(NativeAirJumpLook.LeadRight(c)==(c%2==1),"the lead leg alternates (odd charges lead right)");
        }
        Check(NativeAirJumpLook.Scale(NativeAirJumpLook.Kick,1)==1&&NativeAirJumpLook.Gain(NativeAirJumpLook.Kick,1)==1&&NativeAirJumpLook.White(NativeAirJumpLook.Kick,1)==0,"a Kick's first charge is the dagger's burst");
        Check(NativeAirJumpLook.Charge(0)==1&&NativeAirJumpLook.Charge(9)==NativeJumpSimulation.MaxAirJumps&&NativeAirJumpLook.Scale(NativeAirJumpLook.Kick,40)>=.65f,"the charge clamps to the budget");
        Check(NativeAirJumpLook.KickWeight(-.01f)==0&&NativeAirJumpLook.KickWeight(NativeAirJumpLook.KickSeconds)==0&&NativeAirJumpLook.KickWeight(NativeAirJumpLook.KickIn)>.999f&&NativeAirJumpLook.KickWeight(NativeAirJumpLook.KickHold)>.999f&&NativeAirJumpLook.KickWeight(float.NaN)==0,"the step's weight: in over KickIn, held to KickHold, out by KickSeconds");
        float prev=0,step=0;for(float a=0;a<NativeAirJumpLook.KickSeconds+.02f;a+=1/60f){float w=NativeAirJumpLook.KickWeight(a);step=Math.Max(step,Math.Abs(w-prev));prev=w;}
        Check(step<.45f,"the step's weight never jumps at 60 Hz ("+step+")");
        Check(NativeAirJumpLook.KickSeconds>=.3f&&NativeAirJumpLook.KickSeconds<=.4f,"the step lasts .3-.4 s");
        log.Add("kinds: the daggers and the Bow flip (their burst every charge), the Cyber Lord boosts (no burst here), the other "+Weapons.Count(w=>NativeAirJumpLook.Kicks(w))+" step off the air; a chained Kick charge pops "+(NativeAirJumpLook.ScaleStep*100).ToString("0")+"% smaller, "+(NativeAirJumpLook.GainStep*100).ToString("0")+"% brighter and whiter, its sound a semitone higher; the lead leg alternates; the step lasts "+NativeAirJumpLook.KickSeconds+" s with no jump in its weight");
    }

    // ---- a chain of six air jumps (the authority's 60 Hz tick: takeoff 8.5 m/s, each air jump 7.65 m/s, gravity 23) ----
    private sealed class Tick{public float T,Y;public bool G;public int Jumps;public float JumpAt=-100;}
    private sealed class Sim{public readonly List<Tick> Ticks=new List<Tick>();public readonly List<float> AirAt=new List<float>();public float Contact=-1;}
    private static Sim Chain(int air,float gap=.28f,float at=1,float until=6,float gravity=23)
    {
        var s=new Sim();float y=0,vy=0;bool g=true;int jumps=0;float jumpAt=-100;
        for(int f=0;f/60f<=until;f++)
        {
            float t=f/60f;
            if(g&&Math.Abs(t-at)<.5f/60){vy=8.5f;g=false;jumps=0;}
            for(int k=1;k<=air;k++)if(!g&&Math.Abs(t-(at+gap*k))<.5f/60){vy=7.65f;jumps++;jumpAt=t;s.AirAt.Add(t);}
            if(!g){vy-=gravity/60f;y+=vy/60f;if(y<=0&&vy<=0){y=0;vy=0;g=true;jumps=0;s.Contact=t;}}
            s.Ticks.Add(new Tick{T=t,Y=y,G=g,Jumps=jumps,JumpAt=jumpAt});
        }
        return s;
    }
    private static Tick At(Sim s,float t){int i=(int)Math.Round(t*60);return s.Ticks[Math.Max(0,Math.Min(s.Ticks.Count-1,i))];}
    // What a peer presents at wall time t: the host its rigidbody between ticks with the newest flags and signal on its own clock; a
    // guest a remote pilot on its render clock .1 s behind the newest 20 Hz frame (interpolated there), with that frame's flag and
    // replicated AirJumps / AirJumpAt (authority times, compared against the render clock). `clock` is the presentation clock.
    private static void Present(Sim s,float t,bool guest,out float y,out bool grounded,out float signal)=>Present(s,t,guest,out y,out grounded,out signal,out _);
    private static void Present(Sim s,float t,bool guest,out float y,out bool grounded,out float signal,out float clock)
    {
        clock=t;
        if(guest)
        {
            float newest=(float)Math.Floor(t*20)/20,shown=t-.1f;int a=(int)Math.Floor(shown*20);float u=shown*20-a;clock=shown;
            Tick A=At(s,a/20f),B=At(s,(a+1)/20f),N=At(s,newest);y=A.Y+(B.Y-A.Y)*u;grounded=N.G;signal=N.Jumps>0?N.JumpAt:-100;return;
        }
        int k=(int)Math.Floor(t*60);Tick p=At(s,(k-1)/60f),q=At(s,k/60f);float w=t*60-k;y=p.Y+(q.Y-p.Y)*w;grounded=q.G;signal=q.Jumps>0?q.JumpAt:-100;
    }
    private sealed class Counter:IAirJumpFxSink
    {
        public int Rings,Streaks,Puffs,Motes;public float LowestStreak=10,Floor;public readonly List<float> RingSizes=new List<float>();public bool Keep;
        public void Ring(Vector3 at,Vector3 color,float size,float u,float seed){Rings++;if(Keep)RingSizes.Add(size);}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Streaks++;LowestStreak=Math.Min(LowestStreak,Math.Min(head.Y,head.Y-velocity.Y)-Floor);}
        public void Puff(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Puffs++;}
        public void Mote(Vector3 at,Vector3 color,float size,float u,float seed){Motes++;}
        public void Reset(){Rings=Streaks=Puffs=Motes=0;RingSizes.Clear();}
    }
    private static void Charges(List<string> log)
    {
        int runs=0;float worstLag=0;
        var chain=Chain(6);Check(chain.AirAt.Count==6&&chain.Contact>chain.AirAt[5],"the scripted chain spends six charges and lands");
        foreach(var spec in Weapons)foreach(bool guest in new[]{false,true})foreach(float fps in new[]{30f,60f,144f,240f})
        {
            var b=new NativeAirJumpBursts();var seen=new List<(int charge,float at)>();var sink=new Counter();
            for(float t=0;t<6;t+=1/fps)
            {
                Present(chain,t,guest,out float y,out bool g,out float signal,out float clock);
                int c=b.Observe(7,spec,clock,g,new Vector3(1,y,2),false,signal,true,"#70e1d0");if(c>0)seen.Add((c,clock));b.Draw(sink,clock,1,false,-1,false);
            }
            string what=spec+" "+(guest?"guest":"host")+" "+fps+" Hz";
            if(spec==CyberLordSpec.Id){Check(seen.Count==0&&b.Started==0&&sink.Rings==0,what+": the Cyber Lord never bursts here (its boost draws its own)");continue;}
            Check(seen.Count==6&&seen.Select(x=>x.charge).SequenceEqual(new[]{1,2,3,4,5,6}),what+": one burst per charge, 1st to 6th ("+string.Join(",",seen.Select(x=>x.charge))+")");
            for(int i=0;i<6;i++){float lag=seen[i].at-chain.AirAt[i];worstLag=Math.Max(worstLag,lag);Check(lag>=-1/60f-1e-3f&&lag<=1/fps+.05f+1e-3f,what+": charge "+(i+1)+" shows with its presented impulse ("+lag+" s)");}
            Check(b.Live==0,what+": every burst has finished");runs++;
        }
        // A landing refills the budget: the next takeoff counts from the first charge again; blocked and dead pilots never burst.
        {
            var twice=Chain(2,.3f,1,5);var b=new NativeAirJumpBursts();var seen=new List<int>();
            var second=Chain(3,.3f,3,6);
            for(float t=0;t<6;t+=1/60f){Present(t<2.9f?twice:second,t,false,out float y,out bool g,out float signal);int c=b.Observe(3,"greatsword",t,g,new Vector3(0,y,0),false,signal,true,"#ffae55");if(c>0)seen.Add(c);}
            Check(seen.SequenceEqual(new[]{1,2,1,2,3}),"a landing refills the charges ("+string.Join(",",seen)+")");
            // (The authority grants no air jump while a stun, attack or roll owns the pilot; the replicated signal is trusted as the dagger's was.)
            var dead=new NativeAirJumpBursts();
            for(float t=0;t<6;t+=1/60f){Present(chain,t,false,out float y,out bool g,out float signal);Check(dead.Observe(1,"pistol",t,g,new Vector3(0,y,0),false,signal,false,"#ffffff")==0,"a dead pilot does not burst");}
            Check(dead.Started==0,"nothing started for the dead pilot");
        }
        log.Add(runs+" presentations ("+(Weapons.Length-1)+" weapons; host interpolated, guest .1 s behind 20 Hz frames; 30-240 Hz) of a six-charge chain: exactly one burst per charge in order, at most "+(worstLag*1000).ToString("0")+" ms after its impulse on the presentation clock; the Cyber Lord none; a landing refills from the first charge; dead pilots none");
    }

    // ---- the daggers' and the Bow's burst against the previous presentation, call for call ----
    private sealed class Rec:IAirJumpFxSink
    {
        public readonly List<string> Calls=new List<string>();
        private static string V(Vector3 v)=>BitConverter.SingleToInt32Bits(v.X)+","+BitConverter.SingleToInt32Bits(v.Y)+","+BitConverter.SingleToInt32Bits(v.Z);
        private static string F(float f)=>BitConverter.SingleToInt32Bits(f).ToString();
        public void Ring(Vector3 at,Vector3 color,float size,float u,float seed)=>Calls.Add("ring "+V(at)+" "+V(color)+" "+F(size)+" "+F(u)+" "+F(seed));
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>Calls.Add("streak "+V(head)+" "+V(velocity)+" "+V(color)+" "+F(width)+" "+F(minLength)+" "+F(blur)+" "+F(taper)+" "+F(u)+" "+F(seed));
        public void Puff(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>Calls.Add("puff "+V(at)+" "+V(albedo)+" "+F(opacity)+" "+F(size)+" "+F(u)+" "+F(seed)+" "+F(floor));
        public void Mote(Vector3 at,Vector3 color,float size,float u,float seed)=>Calls.Add("mote "+V(at)+" "+V(color)+" "+F(size)+" "+F(u)+" "+F(seed));
    }
    // The presentation before the movement chips (Runtime/NativeJumpFxPresentation at 2fd5f6a), its loop transcribed onto the sink.
    private sealed class LegacyBursts
    {
        private struct Burst{public float At,Floor;public Vector3 Feet,Accent;public int Seed,Owner;}
        private readonly Dictionary<int,NativeJumpTrack> tracks=new Dictionary<int,NativeJumpTrack>();private readonly List<Burst> bursts=new List<Burst>(16);private int seeds;
        public void Frame(IAirJumpFxSink s,int id,string spec,float now,bool grounded,Vector3 feet,bool blocked,float airJumpAt,float hp,string accent,float quality,bool low,int viewId,bool firstPerson)
        {
            if(!tracks.TryGetValue(id,out var t))tracks[id]=t=new NativeJumpTrack();
            int jumps=t.AirJumps;t.Explicit=true;t.Observe(now,grounded,feet.Y,blocked);t.AirJumpSignal(airJumpAt);
            if(t.AirJumps>jumps&&hp>0&&NativeJumpRules.Flips(spec)&&!CyberLordAirBoost.Handles(spec))
            {var tint=NativeGlowRules.ArmorTint(accent);if(bursts.Count>=16)bursts.RemoveAt(0);bursts.Add(new Burst{At=now,Feet=feet,Floor=t.Floor,Seed=++seeds,Owner=id,Accent=tint});}
            for(int i=bursts.Count-1;i>=0;i--)if(now-bursts[i].At>=NativeJumpFx.Life||now<bursts[i].At-.25f)bursts.RemoveAt(i);
            int streaks=NativeJumpFx.Count(NativeJumpFx.Streaks,NativeJumpFx.ReducedStreaks,quality,low),puffs=NativeJumpFx.Count(NativeJumpFx.Puffs,NativeJumpFx.ReducedPuffs,quality,low);
            foreach(var b in bursts)
            {
                float age=now-b.At;var c=NativeJumpFx.Centre(b.Feet,b.Floor);
                if(NativeJumpFx.Ring(age,out float size,out float alpha))s.Ring(c,b.Accent*(NativeJumpFx.RingGain*alpha),size,age/NativeJumpFx.RingLife,b.Seed);
                for(int k=0;k<streaks;k++)
                    if(NativeJumpFx.Streak(k,streaks,b.Seed,age,c,b.Floor,out var head,out var dir,out float length,out float fade))
                        s.Streak(head,dir*length,b.Accent*(NativeJumpFx.StreakGain*fade),NativeJumpFx.StreakWidth,length,1,.8f,age/NativeJumpFx.StreakLife,b.Seed*31+k);
                if(firstPerson&&b.Owner==viewId)continue;
                for(int k=0;k<puffs;k++)
                    if(NativeJumpFx.Puff(k,puffs,b.Seed,age,b.Feet,out var at,out float ps,out float opacity))
                    {float life=age/NativeJumpFx.PuffLife;s.Puff(at,b.Accent,opacity,ps,life,b.Seed*17+k,b.Floor);s.Mote(at,b.Accent*(NativeJumpFx.PuffGain*opacity),ps*.8f,life,b.Seed*13+k);}
            }
        }
    }
    private static void Legacy(List<string> log)
    {
        int calls=0;
        foreach(var spec in new[]{"daggers","bow"})foreach(float q in new[]{1f,.3f})foreach(bool low in new[]{false,true})foreach(bool fp in new[]{false,true})
        {
            var chain=Chain(6,.26f,1,5,23*.85f);var now=new NativeAirJumpBursts();var old=new LegacyBursts();var a=new Rec();var b=new Rec();
            for(float t=0;t<5;t+=1/60f)
            {
                Present(chain,t,false,out float y,out bool g,out float signal);var feet=new Vector3(.3f,y+.02f,-1.7f);
                now.Observe(5,spec,t,g,feet,false,signal,true,"#ce8aff");now.Draw(a,t,q,low,5,fp);old.Frame(b,5,spec,t,g,feet,false,signal,100,"#ce8aff",q,low,5,fp);
            }
            Check(a.Calls.Count>0&&a.Calls.SequenceEqual(b.Calls),spec+(fp?" first person":"")+" quality "+q+(low?" low":"")+": the burst is the previous presentation's, call for call and bit for bit ("+a.Calls.Count+" vs "+b.Calls.Count+")");calls+=a.Calls.Count;
        }
        log.Add("the daggers' and the Bow's air burst over a six-charge Skybound chain is the previous presentation's exactly ("+calls+" draw calls compared bit for bit: full and Reduced effects, Low graphics, first person)");
    }

    // ---- quality tiers, first person, floor safety and the per-charge pop ----
    private static void Tiers(List<string> log)
    {
        foreach(var (q,low,streaks,puffs) in new[]{(1f,false,NativeJumpFx.Streaks,NativeJumpFx.Puffs),(.3f,false,NativeJumpFx.ReducedStreaks,NativeJumpFx.ReducedPuffs),(1f,true,(NativeJumpFx.Streaks+1)/2,(NativeJumpFx.Puffs+1)/2),(.3f,true,Math.Max(1,(NativeJumpFx.ReducedStreaks+1)/2),Math.Max(1,(NativeJumpFx.ReducedPuffs+1)/2))})
        foreach(bool fp in new[]{false,true})foreach(string spec in new[]{"greatsword","pistol","fire","daggers"})
        {
            var b=new NativeAirJumpBursts();var s=new Counter();float floor=0;
            b.Observe(2,spec,0,true,new Vector3(0,floor,0),false,-100,true,"#70e1d0");b.Observe(2,spec,.02f,false,new Vector3(0,.2f,0),false,-100,true,"#70e1d0");
            b.Observe(2,spec,.3f,false,new Vector3(0,1.4f,0),false,.3f,true,"#70e1d0");s.Reset();b.Draw(s,.31f,q,low,2,fp);
            Check(s.Rings==1&&s.Streaks==streaks,spec+" quality "+q+(low?" low":"")+": one ring and "+streaks+" streaks ("+s.Rings+", "+s.Streaks+")");
            Check(fp?s.Puffs==0&&s.Motes==0:s.Puffs==puffs&&s.Motes==puffs,spec+(fp?" first person: no afterimage on the own pilot":": "+puffs+" afterimage puffs")+" ("+s.Puffs+")");
            s.Reset();b.Draw(s,.31f,q,low,9,fp);Check(s.Puffs==puffs,spec+": another pilot's afterimage still shows in first person");
        }
        // Kick charges pop smaller each time and stay off the floor at any height.
        float lowest=10;var sizes=new List<float>();
        foreach(float h in new[]{.05f,.4f,2f})
        {
            var chain=Chain(6);var b=new NativeAirJumpBursts();var s=new Counter{Keep=true};
            for(float t=0;t<5;t+=1/60f)
            {
                Present(chain,t,false,out float y,out bool g,out float signal);int c=b.Observe(4,"greatsword",t,g,new Vector3(0,y,0),false,signal,true,"#ffae55");
                if(c>0){s.Floor=b.Track(4).Floor;}s.RingSizes.Clear();b.Draw(s,t,1,false,-1,false);
                for(int i=0;i<b.Live;i++){var x=b.Get(i);if(Math.Abs(t-x.At)<1e-5f&&h==.4f)sizes.Add(x.Scale);}
            }
            lowest=Math.Min(lowest,s.LowestStreak);
            // A burst right at the floor (a low air jump just off the ground): the centre and streaks keep their clearance.
            var low=new NativeAirJumpBursts();var ls=new Counter();low.Observe(1,"rifle",0,true,new Vector3(0,0,0),false,-100,true,"#ffffff");low.Observe(1,"rifle",.02f,false,new Vector3(0,h*.1f,0),false,-100,true,"#ffffff");
            for(int k=0;k<6;k++)low.Observe(1,"rifle",.2f+.25f*k,false,new Vector3(0,h*.2f,0),false,.2f+.25f*k,true,"#ffffff");
            for(float t=1.45f;t<1.9f;t+=1/120f)low.Draw(ls,t,1,false,-1,false);lowest=Math.Min(lowest,ls.LowestStreak);
        }
        Check(lowest>=NativeJumpFx.FloorClear-1e-4f,"every streak, any charge, stays FloorClear above the floor it left ("+lowest+")");
        Check(sizes.Count==6&&sizes.Zip(sizes.Skip(1),(x,y)=>y<x).All(v=>v),"a chain's bursts shrink charge by charge ("+string.Join(",",sizes.Select(x=>x.ToString("0.00")))+")");
        var kb=new NativeAirJumpBursts();var kc=Chain(6);
        for(float t=0;t<5;t+=1/60f){Present(kc,t,false,out float y,out bool g,out float signal);kb.Observe(1,"staff-less-weapon",t,g,new Vector3(0,y,0),false,signal,true,"#ffae55");}
        Check(NativeAirJumpLook.Kicks("staff-less-weapon"),"an unknown weapon still steps and bursts");
        log.Add("Reduced effects and Low graphics thin each burst ("+NativeJumpFx.Streaks+"/"+NativeJumpFx.ReducedStreaks+" streaks, "+NativeJumpFx.Puffs+"/"+NativeJumpFx.ReducedPuffs+" puffs, halved on Low); first person skips only the own afterimage; every charge's streaks stay "+(lowest*100).ToString("0")+" cm or more above the floor; a chain's pops shrink to "+(NativeAirJumpLook.Scale(NativeAirJumpLook.Kick,6)*100).ToString("0")+"% by the sixth");
    }

    private sealed class Null:IAirJumpFxSink
    {
        public void Ring(Vector3 at,Vector3 color,float size,float u,float seed){}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){}
        public void Puff(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){}
        public void Mote(Vector3 at,Vector3 color,float size,float u,float seed){}
    }
    private static void Allocation(List<string> log)
    {
        var chain=Chain(6);var b=new NativeAirJumpBursts();var s=new Null();string[] specs={"greatsword","daggers","pistol","fire",CyberLordSpec.Id};
        void Pass(float off){for(float t=0;t<5;t+=1/60f){Present(chain,t,false,out float y,out bool g,out float signal);for(int i=0;i<specs.Length;i++)b.Observe(i,specs[i],t+off,g,new Vector3(i,y,0),false,signal>-90?signal+off:-100,true,"#70e1d0");b.Draw(s,t+off,1,false,0,true);}}
        Pass(0);Pass(10);long before=GC.GetAllocatedBytesForCurrentThread();Pass(20);long used=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(used==0,"no allocation per frame in the bursts ("+used+" bytes)");
        var p=new Pilot("greatsword",true);var chainP=Chain(6);foreach(var tick in chainP.Ticks)p.Frame(chainP,tick.T);p=new Pilot("greatsword",true);foreach(var tick in chainP.Ticks)p.Frame(chainP,tick.T);
        long b2=GC.GetAllocatedBytesForCurrentThread();foreach(var tick in chainP.Ticks)p.Frame(chainP,tick.T,10);long used2=GC.GetAllocatedBytesForCurrentThread()-b2;
        Check(used2==0,"no allocation per frame in the air step ("+used2+" bytes)");
        log.Add("no allocation per frame: five pilots' bursts over a six-charge chain, and the greatsword's air step");
    }

    // ---- the air step on the routes (Runtime/CapturedAnimation's arena rigs) ----
    private sealed class Pilot
    {
        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly Actor A;public MeleeMotion Melee;public GunMotion Gun;public MagicMotion Magic;
        public readonly NativeJumpMotion Jump=new NativeJumpMotion();private readonly MeleeView v=new MeleeView();private readonly NativeJuggleTrack juggle=new NativeJuggleTrack();private readonly float[] channels=new float[13];
        private float last=-100;public float RootY;public readonly string Route;
        public Pilot(string spec,bool steps)
        {
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;int mv=spec=="fire"?1:spec=="water"?2:spec=="lightning"?3:spec=="darkness"?4:0;
            A=new Actor{Id=4,SourceSpec=spec,Weapon=w,MagicVisual=mv,Grounded=true,Hp=100,CastRate=1,Ammo=w==WeaponId.Sword?0:GunRules.Get(w).Magazine,VisualSet=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0};
            Jump.AirSteps=steps;Route=GunRules.Ranged(w)?"gun":mv>0||spec=="leet"?"magic":spec=="emperor"||spec=="ansatsuken"?"fighter":"melee";
        }
        public MeleeRig Rig=>Melee?.Rig??Gun?.Rig??Magic.Rig;
        public void Frame(Sim s,float t,float offset=0)
        {
            Present(s,t,false,out float y,out bool grounded,out float signal);var q=At(s,t);float time=t+offset;
            A.Position=new V3(0,y,0);A.Velocity=new V3(0,0,0);A.Grounded=grounded;A.AirJumps=q.Jumps;A.AirJumpAt=q.Jumps>0?q.JumpAt+offset:-100;RootY=y+.865f;
            float dt=last<-90||time<last||time-last>.35f?1/60f:Math.Min(.1f,time-last);last=time;
            if(Route=="gun"){Gun??=new GunMotion();Gun.JumpLayer=Jump;Gun.Rig.GroupPosition=new Vector3(0,RootY,0);Gun.Rig.GroupYaw=0;Gun.Pose(A,time,Pose,false,juggle,out _);Gun.Finish(Pose,time);Pose.World();return;}
            if(Route=="magic"){Magic??=new MagicMotion();Magic.JumpLayer=Jump;Magic.Rig.GroupPosition=new Vector3(0,RootY,0);Magic.Rig.GroupYaw=0;Magic.Pose(A,time,Pose,channels,0,false,juggle,out _);Magic.Finish(Pose,time);Pose.World();return;}
            Melee??=new MeleeMotion();Melee.JumpLayer=Jump;v.Id="4";v.Spec=A.SourceSpec;v.Vel=new Vector3(0,0,0);v.Grounded=grounded;v.Hp=A.Hp;v.HitStunUntil=0;v.Juggle=false;v.Down=false;v.AirJumpAt=A.AirJumps>0?A.AirJumpAt:-100;
            v.RollAt=-100;v.RollUntil=-100;v.LastAttack=-100;v.SwingAt=v.SwingUntil=float.NaN;v.LastBlock=float.NaN;v.ActionKind=null;
            Melee.Rig.GroupPosition=new Vector3(0,RootY,0);Melee.Rig.GroupYaw=0;
            if(Route=="fighter"){Melee.FighterBase(v,time,dt);Melee.FighterFinish(v,time,dt);}else Melee.Sample(v,time,dt,r=>{});
            Melee.Rig.WritePose(Pose);Pose.World();
        }
        public Vector3 World(int bone)=>new Vector3(0,RootY,0)+Pose.WorldPosition[bone];
    }
    private static float Diff(CapturedRig.Pose a,CapturedRig.Pose b)
    {
        float d=0;for(int i=0;i<CapturedRig.Bones.Length;i++){d=Math.Max(d,Vector3.Distance(a.WorldPosition[i],b.WorldPosition[i]));Quaternion p=a.WorldRotation[i],q=b.WorldRotation[i];if(Quaternion.Dot(p,q)<0)q=-q;d=Math.Max(d,Math.Max(Math.Max(Math.Abs(p.X-q.X),Math.Abs(p.Y-q.Y)),Math.Max(Math.Abs(p.Z-q.Z),Math.Abs(p.W-q.W))));}
        return d;
    }
    // A hand in the chest's frame (the arms as the route holds them).
    private static Vector3 InChest(CapturedRig.Pose p,int hand)=>Vector3.Transform(p.WorldPosition[hand]-p.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(p.WorldRotation[CapturedRig.Chest]));
    private static void Step(List<string> log)
    {
        string[] specs={"sword","greatsword","shield","pistol","rifle","machinegun","fire","water","lightning","darkness","leet","emperor","ansatsuken"};
        float worstRoot=0,worstHand=0,worstGrip=0,lowest=1,leadLift=10,worstReturn=0;int steps=0;var chain=Chain(6);
        foreach(var spec in specs)
        {
            var on=new Pilot(spec,true);var off=new Pilot(spec,false);var charges=new List<int>();int lastCharge=0;bool fighterLegs=spec=="emperor"||spec=="ansatsuken";
            foreach(var tick in chain.Ticks)
            {
                float t=tick.T;on.Frame(chain,t);off.Frame(chain,t);var j=on.Jump;
                for(int bone=0;bone<CapturedRig.Bones.Length;bone++)Check(MathUtil.Finite(on.Pose.WorldPosition[bone].X+on.Pose.WorldPosition[bone].Y+on.Pose.WorldPosition[bone].Z),spec+": finite bones");
                Check(j.FlipAngle==0,spec+" never flips");Check(off.Jump.KickWeight==0,spec+": the step off shows none");
                if(j.KickWeight>.5f&&j.KickCharge!=lastCharge){lastCharge=j.KickCharge;charges.Add(j.KickCharge);Check(j.State==NativeJumpMotion.AirStep,spec+": the layer names the step");}
                float since=chain.AirAt.Where(a=>a<=t+1e-4f).DefaultIfEmpty(-100).Max();
                if(since>-90&&t-since>.075f&&t-since<.115f)
                {
                    Check(j.KickWeight>.99f,spec+": the step in full "+(t-since).ToString("0.00")+" s after the impulse ("+j.KickWeight+")");steps++;
                    // The lead knee is up and the trailing foot down (the lead alternates with the charge).
                    bool right=j.KickLeadRight;var lk=on.World(right?CapturedRig.RKnee:CapturedRig.LKnee);var tk=on.World(right?CapturedRig.LKnee:CapturedRig.RKnee);
                    var lf=on.World(right?CapturedRig.RFoot:CapturedRig.LFoot);var tf=on.World(right?CapturedRig.LFoot:CapturedRig.RFoot);
                    if(!fighterLegs||on.Rig.LegsLayered){leadLift=Math.Min(leadLift,lk.Y-tk.Y);Check(lk.Y>tk.Y+.08f&&lf.Y>tf.Y+.1f,spec+" charge "+j.KickCharge+": the lead knee drives up and the trailing foot kicks down ("+(lk.Y-tk.Y)+", "+(lf.Y-tf.Y)+")");}
                }
                if(!tick.G)
                {
                    // The root, the arms and the grip: the rig's root and hips stay where the route puts them, the hands where the arms hold them.
                    float root=Math.Max(Vector3.Distance(on.Pose.WorldPosition[CapturedRig.Body],off.Pose.WorldPosition[CapturedRig.Body]),Math.Abs(on.RootY-off.RootY));worstRoot=Math.Max(worstRoot,root);
                    Check(root<1e-5f,spec+": the step moves no root ("+root+")");
                    float hand=Math.Max(Vector3.Distance(InChest(on.Pose,CapturedRig.RHand),InChest(off.Pose,CapturedRig.RHand)),Vector3.Distance(InChest(on.Pose,CapturedRig.LHand),InChest(off.Pose,CapturedRig.LHand)));worstHand=Math.Max(worstHand,hand);
                    Check(hand<.03f,spec+": the arms keep the weapon as the route holds it ("+hand+" m)");
                    if(spec=="greatsword"){var r=on.Rig;Vector3 grip=r.ChainPoint(CapturedRig.RHand,-1)+Vector3.Transform(r.Reached(MeleeRig.SupportGrip),r.ChainQuat(CapturedRig.RHand,-1));float gap=Vector3.Distance(grip,r.ChainPoint(CapturedRig.LHand,-1));worstGrip=Math.Max(worstGrip,gap);Check(gap<.07f,"greatsword: the support hand keeps the hilt through the step ("+gap+")");}
                    // In the air (as presented) the soles stay on or above the floor left, never lower than the jump layer's own.
                    float Sole(Pilot x)=>Math.Min(Math.Min(x.World(CapturedRig.LFoot).Y,x.World(CapturedRig.RFoot).Y)-.07f,Math.Min(x.World(CapturedRig.LToe).Y,x.World(CapturedRig.RToe).Y)-.05f);
                    if(j.Track.Airborne&&t-j.Track.AirAt>.1f){float sole=Sole(on);lowest=Math.Min(lowest,sole);Check(sole>=Math.Min(-.005f,Sole(off)-.002f),spec+": feet above the floor in the air ("+sole+" at "+t+", "+j.State+", without the step "+Sole(off)+")");}
                }
                // Past the last step (no landing inside it) the pose is exactly the route's own again.
                if(since>-90&&t-since>NativeAirJumpLook.KickSeconds+.02f&&(chain.AirAt.All(a=>a<=t-NativeAirJumpLook.KickSeconds-.02f||a>t))&&!tick.G){float d=Diff(on.Pose,off.Pose);worstReturn=Math.Max(worstReturn,d);Check(d<1e-5f,spec+": back to the jump layer's own pose after the step ("+d+")");}
            }
            Check(charges.SequenceEqual(new[]{1,2,3,4,5,6}),spec+": one step per charge ("+string.Join(",",charges)+")");
            Check(on.Jump.Track.Landings==1,spec+": one landing");
        }
        // The daggers and the Cyber Lord's route never step (flip and boost keep theirs).
        foreach(var spec in new[]{"daggers"})
        {
            var p=new Pilot(spec,true);bool flipped=false;
            foreach(var tick in chain.Ticks){p.Frame(chain,tick.T);Check(p.Jump.KickWeight==0&&p.Jump.State!=NativeJumpMotion.AirStep,spec+" never steps");flipped|=p.Jump.FlipAngle>1;}
            Check(flipped,spec+" still flips");
        }
        log.Add(specs.Length+" Kick weapons (melee, gun, magic and fighter routes) through a six-charge chain: one air step per charge, full by 75 ms, the lead knee "+(leadLift*100).ToString("0")+" cm or more above the trailing one, alternating; the root unmoved ("+worstRoot.ToString("0e0")+"), the hands in the chest's frame within "+(worstHand*1000).ToString("0")+" mm, the greatsword's grip within "+(worstGrip*1000).ToString("0")+" mm, soles "+(lowest*1000).ToString("0")+" mm or more above the floor; exactly the jump layer's pose after each step ("+worstReturn.ToString("0e0")+"); the daggers flip, never step");
    }

    // ---- the five movement chips' card icons ----
    private static void Icons(string root,List<string> log)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json")));
        var icons=new HashSet<string>(doc.RootElement.GetProperty("icons").EnumerateArray().Select(x=>x.GetProperty("id").GetString()));
        var mapped=new HashSet<string>(doc.RootElement.GetProperty("chips").EnumerateArray().Select(x=>x.GetProperty("id").GetString()));
        var chips=NativeMovementContent.Chips();
        Check(chips.Length==5&&NativeChipIcons.Ids.Length==5&&NativeChipIcons.Icons.Length==5,"five movement chips and five icons");
        foreach(var c in chips)
        {
            string icon=NativeChipIcons.Native(c.id);
            Check(icon!=null&&icons.Contains(icon),c.id+": its card icon "+icon+" is in the pinned icon set");
            Check(icon!="cpu"&&icon!="wind",c.id+": a distinct icon, not the fallback or the shared wind");
            Check(!mapped.Contains(c.id),c.id+": the source map does not list it (the override applies)");
        }
        Check(NativeChipIcons.Icons.Distinct().Count()==5,"the five icons are distinct");
        Check(NativeChipIcons.Native("vital")==null&&NativeChipIcons.Native(null)==null&&NativeChipIcons.Native("example-mobility:hop-drive")==null,"other chips keep the source map or the cpu fallback");
        Check(chips.All(c=>c.icon=="wind"),"the chips' content data is untouched (their own icon field stays Astra's)");
        var src=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeIcons.cs"));
        Check(src.Contains("return chips.TryGetValue(id??\"\",out var i)?i:NativeChipIcons.Native(id)??\"cpu\";"),"NativeIcons.Chip: the source map, then the native icons, then cpu");
        Check(Regex.Matches(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeChipCard.cs")),"NativeIcons\\.Chip\\(c\\.id\\)").Count==1,"the chip card draws its icon through NativeIcons.Chip");
        log.Add("the movement chips' card icons: "+string.Join(", ",NativeChipIcons.Ids.Zip(NativeChipIcons.Icons,(i,n)=>i+" "+n))+" (all in the pinned set, distinct, the source map first; content data untouched)");
    }

    // ---- the wiring, and no authority, network or content file reads any of it ----
    private static string Code(string s)=>Regex.Replace(Regex.Replace(s,@"//[^\n]*",""),@"/\*.*?\*/","",RegexOptions.Singleline);
    private static void Hooks(string root,List<string> log)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
        var fx=R("Runtime/NativeJumpFxPresentation.cs");
        Check(fx.Contains("arena.PresentedJump(p,out bool grounded,out float airJumpAt);")&&fx.Contains("bursts.Observe(p.Seat.id,p.Seat.spec,now,grounded,new NVector(feet.x,feet.y,feet.z),blocked,airJumpAt,a.Hp>0,a.AccentColor);")&&fx.Contains("bursts.Draw(sink,now,quality,low,viewId,firstPerson);"),"the presentation feeds every pilot's presented signal to the bursts and draws them");
        Check(fx.Contains("Vfx.Glow(U(at),C(color),size,NativeVfxDraw.Ring,u,seed)")&&fx.Contains("Vfx.SoftSmoke(U(at),C(albedo),opacity,size,u,seed,floor)")&&fx.Contains("Vfx.Glow(U(at),C(color),size,NativeVfxDraw.Mote,u,seed)"),"the sink draws through the shared effects renderer (ring, soft smoke, mote)");
        Check(R("Runtime/ArenaPresentation.cs").Contains("JumpFx(arena,fields);")&&fx.Contains("jumpFx.Render(arena,now,WeaponEffectsQuality,LowGraphics,arena.ViewPilot?.Seat.id??-1,FirstPerson);"),"wired with the effects quality, Low graphics and first person");
        var audio=R("Runtime/NativeMoveFeelAudio.cs");
        Check(audio.Contains("else if(track.AirJumps>jumps&&a.Hp>0&&NativeAirJumpLook.Kicks(p.Seat.spec)){bool mine=p==me;BankPlay(NativeAirJumpLook.KickSound,p.Body.position,mine?NativeAirJumpLook.KickOwnGain:NativeAirJumpLook.KickGain,0,null,mine,null,NativeAirJumpLook.KickPitch(track.AirJumps));}")
            &&audio.Contains("public const float FlipWhooshRate=1.2f,FlipWhooshGain=.5f,FlipWhooshOwnGain=.4f;")&&audio.Contains("BankPlay(\"melee.daggers.swing\",p.Body.position,mine?FlipWhooshOwnGain:FlipWhooshGain,0,null,mine,null,FlipWhooshRate);"),"the Kick sound per charge; the flip whoosh and the boost unchanged");
        Check(NativeAirJumpLook.KickSound=="move.jump"&&NativeAirJumpLook.KickGain<.5f&&NativeAirJumpLook.KickOwnGain<.4f,"the Kick sound is the jump's own key, softer than the takeoff");
        var motion=R("Core/NativeJumpMotion.cs");
        Check(motion.Contains("if(t.AirJumps>0&&t.AirJumpAt>kickAt&&t.Airborne&&NativeAirJumpLook.Kicks(spec)){kickAt=t.AirJumpAt;kickCharge=t.AirJumps;}")&&motion.Contains("NativeJumpRules.Flips(spec)&&!CyberLordAirBoost.Handles(spec)"),"the jump layer starts a step per charge beside the flip and the boost");
        var step=Code(motion.Substring(motion.IndexOf("private void Step(int set,float[] f,float k,bool leadRight)"),900));
        Check(!step.Contains("r.P[")&&!step.Contains("GroupPosition")&&!step.Contains("MeleeCapture.By")&&!step.Contains("MeleeCapture.Pelvis")&&!step.Contains("Clav")&&!step.Contains("Grip"),"the step writes leg and spine channels only (no root, hips height, pelvis, arms or grip)");
        // Presentation only: the new pieces send, decide and write nothing that the authority, prediction, wire or content read.
        var core=Code(R("Core/NativeAirJumpFx.cs"))+Code(R("Core/NativeChipIcons.cs"));
        foreach(var api in new[]{"Stats[","content.","contentRevision","effects","Velocity","Grounded=","AirJumps=","AirJumpAt=","Send(","Codec","NativeNetwork"})Check(!core.Contains(api),"the new presentation pieces touch no rule, content or wire state ("+api+")");
        var allowed=new[]{"Core/NativeAirJumpFx.cs","Core/NativeJumpFx.cs","Core/NativeChipIcons.cs","Core/NativeJumpMotion.cs","Runtime/NativeJumpFxPresentation.cs","Runtime/NativeMoveFeelAudio.cs","Runtime/NativeIcons.cs"};
        foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories))
        {
            string rel=f.Substring(Path.Combine(root,"Assets/ChipArena").Length+1).Replace('\\','/');if(allowed.Contains(rel))continue;var text=File.ReadAllText(f);
            Check(!text.Contains("NativeAirJumpLook")&&!text.Contains("NativeAirJumpBursts")&&!text.Contains("IAirJumpFxSink")&&!text.Contains("NativeChipIcons")&&!text.Contains(".AirSteps"),rel+" does not use the air-jump presentation or the chip icons");
        }
        foreach(var f in new[]{"Core/ArenaWorld.cs","Core/NativePrediction.cs","Core/NativeJumpSimulation.cs","Core/NativeMovementContent.cs","Core/ConversionContent.cs","Runtime/NativeArena.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeReplicaMotion.cs","Runtime/NativeJumpValidation.cs","Runtime/NativeModsUI.cs"})
            Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f))&&!R(f).Contains("NativeJumpFx")&&!R(f).Contains("NativeAirJump")&&!R(f).Contains("NativeChipIcons"),f+" (authority, prediction, wire or content) reads no air-jump look or chip icon");
        log.Add("hooks: every pilot's presented signal to the bursts through the shared effects renderer with quality, Low and first person; the Kick sound beside the unchanged whoosh and boost; the jump layer's step beside the flip and boost, writing legs and spine only; the authority, prediction, wire, Mods and content files read none of it");
    }
}
