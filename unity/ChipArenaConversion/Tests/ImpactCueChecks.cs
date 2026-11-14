using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ChipArena.UnityConversion;

// Tests/impact-cue-parity.json is written by tools/export_impact_cues.mjs from the pinned client/event-vfx.ts EventVfx.event,
// client/chaos-visuals.ts summons (minionShot) and client/standard-melee-effects.ts (flash sparks, slam dust and stones).
// Each event scenario names the Unity event that stands for the source events; Core/NativeImpactCues must plan exactly the
// draw calls the source made, and NativeMeleeFlash / NativeSlamDust must place what the source's scene graph holds.
internal static class ImpactCueChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Impact cue check failed: "+what);}
    private static V3 Vec(JsonElement a)=>new V3((float)a[0].GetDouble(),(float)a[1].GetDouble(),(float)a[2].GetDouble());
    private static bool Near(double a,double b,double eps=1e-4)=>Math.Abs(a-b)<=eps;
    private static bool Near(V3 a,JsonElement b,double eps=1e-4)=>Near(a.x,b[0].GetDouble(),eps)&&Near(a.y,b[1].GetDouble(),eps)&&Near(a.z,b[2].GetDouble(),eps);
    private static double Num(JsonElement e,string name,double fallback=0)=>e.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.Number?v.GetDouble():fallback;
    private static void Same(NativeImpactCommand[] got,int n,JsonElement calls,string what)
    {
        Check(n==calls.GetArrayLength(),what+": "+n+" draws, source "+calls.GetArrayLength());
        for(int i=0;i<n;i++)
        {
            var c=calls[i];var g=got[i];string op=c.GetProperty("op").GetString(),at=what+" #"+i+" "+op;
            Check(g.Op.ToString()==op,at+": op "+g.Op);
            Check(g.Color==c.GetProperty("color").GetInt32(),at+": colour "+g.Color.ToString("x6"));
            Check(Near(g.A,c.GetProperty("a")),at+": point");
            switch(g.Op)
            {
                case NativeImpactOp.Burst:Check(Near(g.Count,Num(c,"count"))&&Near(g.Power,Num(c,"power")),at+": count/power");break;
                case NativeImpactOp.Beam:case NativeImpactOp.Paddle:Check(Near(g.B,c.GetProperty("b")),at+": end/direction");break;
                case NativeImpactOp.Impact:Check(Near(g.Radius,Num(c,"radius"))&&g.Ground==c.GetProperty("ground").GetBoolean(),at+": radius/ground");break;
                case NativeImpactOp.Line:Check(Near(g.B,c.GetProperty("b"))&&g.Color1==c.GetProperty("color1").GetInt32()&&Near(g.Jitter,Num(c,"jitter"))&&Near(g.Life,Num(c,"life"))&&Near(g.Sweep,Num(c,"sweep")),at+": line");break;
            }
        }
    }
    private static bool SameRotation(double qx,double qy,double qz,double qw,JsonElement q,double eps=2e-5)
    {
        double s=qx*q[0].GetDouble()+qy*q[1].GetDouble()+qz*q[2].GetDouble()+qw*q[3].GetDouble();
        return Math.Abs(Math.Abs(s)-1)<eps;
    }
    public static List<string> Run(string root,ConversionContent data)
    {
        var passed=new List<string>();
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/impact-cue-parity.json")));var json=document.RootElement;
        var plan=new NativeImpactCommand[NativeImpactCues.MaxCommands];
        // The colours the presentation resolves from the content are the source's (WEAPONS.lightning, config.ts MATERIALS).
        Check(NativeImpactCues.Lit==json.GetProperty("lightning").GetInt32(),"LIT is WEAPONS.lightning.color");
        int lit=0;foreach(var w in data.weapons)if(w.id=="lightning")lit=(int)w.color;Check(lit==NativeImpactCues.Lit,"the content's lightning colour is LIT");
        foreach(var m in json.GetProperty("materials").EnumerateObject())Check(NativeImpactCues.MaterialColor(data.materials,m.Name)==m.Value.GetInt32(),"material colour "+m.Name);
        Check(NativeImpactCues.MaterialColor(data.materials,"glass")==0&&NativeImpactCues.MaterialColor(data.materials,null)==0&&NativeImpactCues.MaterialColor(null,"wood")==0,"unknown materials have no colour");
        int events=0;
        foreach(var s in json.GetProperty("events").EnumerateArray())
        {
            var u=s.GetProperty("unity");var at=Vec(s.GetProperty("position"));
            var e=new NativeImpactEvent{Kind=Enum.Parse<EventKind>(u.GetProperty("kind").GetString()),Detail=u.GetProperty("detail").ValueKind==JsonValueKind.String?u.GetProperty("detail").GetString():null,Position=at,
                From=u.GetProperty("from").ValueKind==JsonValueKind.Array?Vec(u.GetProperty("from")):at,Color=u.GetProperty("color").GetInt32(),Amount=(float)u.GetProperty("amount").GetDouble(),Radius=(float)u.GetProperty("radius").GetDouble(),
                TargetPilot=u.GetProperty("targetPilot").GetBoolean(),Trails=u.GetProperty("trails").GetBoolean()};
            if(u.GetProperty("target").ValueKind==JsonValueKind.Array){e.HasTarget=true;e.Target=Vec(u.GetProperty("target"));}
            Same(plan,NativeImpactCues.Plan(e,plan),s.GetProperty("calls"),s.GetProperty("name").GetString());
            // A guest's absent Detail arrives as "" (JsonUtility) and plans like none.
            if(e.Detail==null){var guest=e;guest.Detail="";Same(plan,NativeImpactCues.Plan(guest,plan),s.GetProperty("calls"),s.GetProperty("name").GetString()+" (guest)");}
            events++;
        }
        passed.Add(events+" event-vfx.ts event scenarios: hit/debris/block/break/collapse sparks, impact streaks, procs, pong rallies, stage hazards, L33t and arcs");
        int records=0;
        foreach(var r in json.GetProperty("records").EnumerateArray())
        {
            int element=r.GetProperty("element").GetInt32();var at=Vec(r.GetProperty("position"));var from=Vec(r.GetProperty("from"));
            int n=element==-1?NativeImpactCues.SpikeRise(at,plan):element==-2?NativeImpactCues.Dud(at,plan):NativeImpactCues.Record(element,r.GetProperty("radius").GetDouble(),at,from,r.GetProperty("hop").GetBoolean(),plan);
            Same(plan,n,r.GetProperty("calls"),r.GetProperty("name").GetString());records++;
        }
        passed.Add(records+" record/derived scenarios: L33t possession and Slenderman hop, toy shot, blink/arc/lock records, spike rise, rocket dud");
        // Unity's events without From leave it zero (surface impacts) or equal to the position (Emit): no streak.
        Check(!NativeImpactCues.HasFrom(new V3(1,2,3),new V3(1,2,3))&&!NativeImpactCues.HasFrom(new V3(1,2,3),new V3())&&NativeImpactCues.HasFrom(new V3(1,2,3),new V3(1,2,2.9f)),"from point present");
        int sparks=0;
        foreach(var f in json.GetProperty("flashes").EnumerateArray())
        {
            string spec=f.GetProperty("spec").GetString();double quality=f.GetProperty("quality").GetDouble(),at0=f.GetProperty("at").GetDouble(),weight=NativeMeleeFlash.Weight(spec);
            Check(!NativeMeleeFlash.Flashes(spec,"burn")&&!NativeMeleeFlash.Flashes(spec,"poison")&&f.GetProperty("burnFlashes").GetInt32()==0,spec+": burn and poison ticks never flash");
            foreach(var frame in f.GetProperty("frames").EnumerateArray())
            {
                double age=frame.GetProperty("age").GetDouble();var list=frame.GetProperty("sparks");
                int count=NativeMeleeFlash.Flashes(spec,"melee")?NativeMeleeFlash.Sparks(weight,quality):0;Check(count==list.GetArrayLength(),spec+" q"+quality+": spark count "+count+" vs "+list.GetArrayLength());
                for(int i=0;i<count;i++)
                {
                    var p=list[i];NativeMeleeFlash.Spark(at0,weight,i,age,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double length,out double alpha);
                    string what=spec+" q"+quality+" age "+age+" spark "+i;var pos=p.GetProperty("p");
                    Check(Near(x,pos[0].GetDouble())&&Near(y,pos[1].GetDouble())&&Near(z,pos[2].GetDouble()),what+": offset");
                    Check(SameRotation(qx,qy,qz,qw,p.GetProperty("q")),what+": rotation");
                    Check(Near(length/NativeMeleeFlash.SparkLength,p.GetProperty("sz").GetDouble())&&Near(alpha,p.GetProperty("alpha").GetDouble()),what+": length/opacity");
                    Check(p.GetProperty("color").GetInt32()==NativeMeleeFlash.Color,what+": colour");sparks++;
                }
            }
        }
        // At most 8 live flashes, the oldest dropped; a flash leaves after .28 s.
        var pool=new NativeMeleeFlash();for(int i=0;i<10;i++)pool.Add(i*.01,1,new V3(i,0,0),1);
        Check(pool.Count==NativeMeleeFlash.Cap&&pool[0].Position.x==2&&pool[7].Position.x==9,"flash cap drops the oldest");
        pool.Prune(.02+NativeMeleeFlash.Life+.001);Check(pool.Count==NativeMeleeFlash.Cap-1&&pool[0].Position.x==3,"flashes expire after .28 s");
        passed.Add(sparks+" standard-melee-effects.ts flash sparks (weights, quality, offsets, rotations, stretch, fade)");
        int dust=0,stones=0;
        foreach(var s in json.GetProperty("slams").EnumerateArray())
        {
            double quality=s.GetProperty("quality").GetDouble();
            foreach(var frame in s.GetProperty("frames").EnumerateArray())
            {
                double age=frame.GetProperty("age").GetDouble();string what="slam q"+quality+" age "+age;var points=frame.GetProperty("points");var list=frame.GetProperty("stones");
                Check(Near(frame.GetProperty("root")[1].GetDouble()-1.2,NativeSlamDust.Lift),what+": lift");
                Check(frame.GetProperty("dustColor").GetInt32()==NativeSlamDust.DustColor&&Near(frame.GetProperty("dustSize").GetDouble(),NativeSlamDust.DustSize)&&Near(frame.GetProperty("dustOpacity").GetDouble(),NativeSlamDust.DustOpacity(age)),what+": dust look");
                Check(points.GetArrayLength()==NativeSlamDust.Dust(quality)&&list.GetArrayLength()==NativeSlamDust.Stones(quality),what+": counts");
                for(int i=0;i<points.GetArrayLength();i++)
                {NativeSlamDust.DustPoint(i,age,out double x,out double y,out double z);var p=points[i];Check(Near(x,p[0].GetDouble())&&Near(y,p[1].GetDouble())&&Near(z,p[2].GetDouble()),what+": dust "+i);dust++;}
                for(int i=0;i<list.GetArrayLength();i++)
                {
                    var o=list[i];NativeSlamDust.Stone(i,age,out double x,out double y,out double z,out double scale,out double qx,out double qy,out double qz,out double qw);var p=o.GetProperty("p");
                    Check(Near(x,p[0].GetDouble())&&Near(y,p[1].GetDouble())&&Near(z,p[2].GetDouble())&&Near(scale,o.GetProperty("s").GetDouble())&&SameRotation(qx,qy,qz,qw,o.GetProperty("q")),what+": stone "+i);
                    Check(o.GetProperty("color").GetInt32()==NativeSlamDust.StoneColor&&o.GetProperty("radius").GetDouble()==1&&o.GetProperty("detail").GetInt32()==0,what+": stone look");stones++;
                }
            }
        }
        // three's PointsMaterial size attenuation: .3 at a 60 degree vertical field of view covers .3 tan(30 deg) m.
        Check(Near(NativeSlamDust.PointWorldSize(.3,60),.3*Math.Tan(Math.PI/6),1e-9),"dust point size");
        passed.Add(dust+" slam dust points and "+stones+" stones (standard-melee-effects.ts melee-slam)");
        // The Siren's own effect records draw nothing (siren/fx.ts draws them from the events); the prototype lab alone keeps
        // the ParticleSystem burst.
        Check(NativeBlastCues.Classify(false,1,4,null,true)==NativeBlastCues.Cue.None&&NativeBlastCues.Classify(false,5,4,null,true)==NativeBlastCues.Cue.None&&NativeBlastCues.Classify(false,1,4,"tidal-wave",true,true)==NativeBlastCues.Cue.None,"siren records draw nothing");
        foreach(EventKind kind in Enum.GetValues(typeof(EventKind)))Check(!NativeEventFeedback.HitBurst(kind,true),"native arenas draw no legacy burst: "+kind);
        passed.Add("Siren records draw no halos; native arenas draw no legacy particle burst");
        return passed;
    }
}
