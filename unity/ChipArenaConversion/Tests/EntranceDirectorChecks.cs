using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

// Engine-free checks for the directed entrances (Core/NativeEntranceDirector.cs and friends; owner 2026-10-11 rework): every
// non-DEV entrance is directed, keeps the 4.8 s phase and the 3.2 s name card (the weapon signatures their sting's hit), its
// presentation clock slows only between the hit and the name card, and everything it draws is a pure function of the age
// (any order of evaluation gives the same pose, camera, lights, set and effects). The camera never dips under the floor,
// never comes inside the pilot or a solid prop, moves smoothly outside its authored cuts, whips and hit-stop, keeps the pilot in frame
// and settles still before the hand-off. Effects stay inside their budget, the effects setting thins them, the blast pools
// stay at capacity, no glowing disc or ring lies on the floor and no bolt or shock is blue or cyan. A playthrough allocates
// nothing. Unity's rendering is not exercised (the offline sheets are the visual check).
internal static class EntranceDirectorChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Entrance director: "+what);}
    private sealed class Sink:INativeEntranceFx,INativeEntranceProps
    {
        public int Count,Glows,Streaks,Smokes;public double Hash;public bool Record;public readonly List<(WeaponShape shape,bool glow,Vector3 p,Vector3 s,Vector3 c)> Pieces=new List<(WeaponShape,bool,Vector3,Vector3,Vector3)>();
        private void Mix(Vector3 a,float k){Hash=Hash*1.000001+a.X*.37+a.Y*.61+a.Z*.89+k;}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Count++;Glows++;Mix(at,size+color.X);Finite(at,"glow");}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Count++;Streaks++;Mix(head,width+velocity.Y);Finite(head,"streak");}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Count++;Smokes++;Mix(at,opacity+size);Finite(at,"smoke");}
        public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color){Count++;Mix(position,scale.X);if(Record)Pieces.Add((shape,glow,position,scale,color));}
        public void Reset(){Count=Glows=Streaks=Smokes=0;Hash=0;Pieces.Clear();}
        private static void Finite(Vector3 v,string what){if(!(float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)))throw new Exception("Entrance director: non-finite "+what);}
    }
    private sealed class Null:INativeEntranceFx,INativeEntranceProps
    {
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){}
        public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color){}
    }
    public static IEnumerable<string> Run(string root)
    {
        // The eighteen non-DEV entrances, the DEV Sheldon Intro (Core/NativeSheldonIntro.cs) and the DEV Cyber Lord's Hardlight
        // Boot (Core/NativeCyberLordIntro.cs); neither DEV one is in NativeIntroRules.Entrances.
        var clips=Clips(root);var directed=NativeIntroRules.Entrances.Where(NativeEntranceDirector.Directed).Append(NativeSheldonIntro.Style).Append(CyberLordSpec.Entrance).ToArray();
        Check(directed.Length==20&&NativeEntranceDirector.Styles.Count()==20,"twenty directed entrances");
        var scripts=new Dictionary<string,NativeEntranceScript>();
        foreach(var s in directed){var sc=NativeEntranceDirector.Script(s);if(sc is NativeWeaponEntrance){Check(!sc.Ready,s+" waits for its clip");var c=clips["intro-"+s];sc.Bind(new NativeEntranceClip(c.values,c.roots,c.frames,c.duration));}Check(sc.Ready&&sc.Style==s,s+" is ready");scripts[s]=sc;sc.Prepare();}
        // 1. Timing: the match's phase and name card are untouched; the clock is the age up to the hit and again by the name card.
        double worstLead=0;
        foreach(var (s,sc) in scripts)
        {
            Check(NativeIntroRules.Duration(s)==4.8&&NativeIntroRules.NameAt(s)==3.2&&NativeEntranceScript.Duration==4.8f&&NativeEntranceScript.NameAt==3.2f,s+" timing");
            if(sc is NativeWeaponEntrance)Check(Math.Abs(sc.Hit-NativeIntroMotion.Hit(s))<1e-6f,s+" hits on its sting's accent");
            float back=sc.Hit+sc.SlowHold+sc.SlowCatchUp;Check(back<=NativeEntranceScript.NameAt+1e-4f,s+" catches up before the name card");
            float prev=-1;
            for(float age=0;age<=5.2f;age+=1/480f)
            {
                float t=sc.Clock(age);Check(float.IsFinite(t)&&t>=prev-1e-6f,s+" clock runs backwards at "+age);prev=t;
                if(age<=sc.Hit||age>=back&&age<=4.8f)Check(Math.Abs(t-age)<2e-4f,s+" clock is the age at "+age.ToString("0.000")+" ("+t+")");
                worstLead=Math.Max(worstLead,Math.Abs(t-age));
            }
            Check(sc.Clock(float.NaN)==0&&sc.Clock(-1)==0,s+" clamps a bad clock");
        }
        yield return $"entrance timing: {scripts.Count} directed entrances keep 4.8 s and the 3.2 s card; weapon signatures hit on their sting; the slow-motion beat stays between the hit and the card (largest lag {worstLead:0.000} s)";
        // 2. Purity: the same age gives the same frame whatever was evaluated before (shuffled, backwards, repeated).
        var pose=new CapturedRig.Pose();var sink=new Sink();var lights=new NativeEntranceLights();long frames=0;var rng=new Random(2026);
        foreach(var (s,sc) in scripts)
        {
            var ages=Enumerable.Range(0,97).Select(i=>i*.05f).ToArray();var first=new Dictionary<float,(Vector3 root,Vector3 eye,Vector3 look,double fx,double props,double light,Vector3 hand)>();
            foreach(var pass in new[]{ages,ages.Reverse().ToArray(),ages.OrderBy(_=>rng.Next()).ToArray()})foreach(float age in pass)
            {
                float t=sc.Clock(age);sc.Pose(t,pose,out var at,out _);pose.World();sc.Camera(age,out var view);sink.Reset();sc.Effects(t,sink,1);double fx=sink.Hash;sink.Reset();sc.Props(t,sink,1);double props=sink.Hash;sc.Lights(t,lights);
                double light=lights.Points+lights.Point.Take(lights.Points).Sum(p=>p.X*.3+p.R*.7+p.G+p.Distance);var frame=(root:at,eye:view.Eye,look:view.Look,fx,props,light,hand:pose.WorldPosition[CapturedRig.RHand]);
                Check(lights.Points<=NativeStageLighting.Max&&lights.Directionals<=NativeEntranceLights.MaxDirectional,s+" light count");
                if(!first.TryGetValue(age,out var was)){first[age]=frame;continue;}
                Check(was.root==frame.root&&was.eye==frame.eye&&was.look==frame.look&&was.fx==frame.fx&&was.props==frame.props&&was.light==frame.light&&was.hand==frame.hand,s+" differs at "+age+" s when evaluated in another order");frames++;
            }
        }
        yield return $"entrance purity: {frames} re-evaluations (reversed, shuffled) of all {scripts.Count} entrances give the identical pose, camera, lights, set and effects";
        // 3. The camera: clear of the floor, the pilot and solid props; smooth outside cuts, whips and the hit-stop (a freeze by design); the pilot in frame; still at the end.
        double worstAccel=0,worstTurn=0;string accelAt="",turnAt="";int framed=0,visible=0;var boxes=new Vector4[64];const float dt=1/240f,aspect=16/9f;
        foreach(var (s,sc) in scripts)
        {
            var cuts=new List<(float from,float to)>{(sc.Hit-2.5f*dt,sc.Hit+2.5f*dt)};
            for(int k=1;k<sc.Shots.Length;k++){var sh=sc.Shots[k];float at=AgeOf(sc,sh.At);if(sh.In==NativeEntranceCut.Cut)cuts.Add((at-2.5f*dt,at+2.5f*dt));else if(sh.In==NativeEntranceCut.Whip)cuts.Add((AgeOf(sc,sh.At-sh.InTime*.5f)-2.5f*dt,AgeOf(sc,sh.At+sh.InTime*.5f)+2.5f*dt));}
            NativeEntranceView a=default,b=default,c=default;
            for(int i=0;i*dt<=4.8f;i++)
            {
                float age=i*dt,t=sc.Clock(Math.Min(age,NativeEntranceScript.Settle));a=b;b=c;sc.Camera(age,out c);
                Check(c.Eye.Y>=NativeEntranceStage.FloorY+NativeEntranceCamera.EyeClearance-1e-3f,s+" camera under the floor at "+age.ToString("0.000"));
                var head=sc.Anchor(NativeEntranceAnchor.Head,t);var pelvis=sc.Anchor(NativeEntranceAnchor.Pelvis,t);var feet=(sc.Anchor(NativeEntranceAnchor.LeftFoot,t)+sc.Anchor(NativeEntranceAnchor.RightFoot,t))*.5f;
                float body=Math.Min(Body(c.Eye,head,pelvis),Body(c.Eye,pelvis,feet));Check(body>=NativeEntranceCamera.BodyClearance-.02f,s+" camera inside the pilot at "+age.ToString("0.000")+" ("+body.ToString("0.000")+" m)");
                int n=sc.Boxes(t,boxes);for(int k=0;k+1<n;k+=2){var l=c.Eye-new Vector3(boxes[k].X,boxes[k].Y,boxes[k].Z);var h=new Vector3(boxes[k+1].X,boxes[k+1].Y,boxes[k+1].Z);Check(Math.Abs(l.X)>=h.X||Math.Abs(l.Y)>=h.Y||Math.Abs(l.Z)>=h.Z,s+" camera inside a solid prop at "+age.ToString("0.000"));}
                if(age>=NativeEntranceScript.Settle)Check(c.Eye==b.Eye&&c.Look==b.Look&&c.Fov==b.Fov||age<NativeEntranceScript.Settle+dt,s+" camera still moving after it settled at "+age.ToString("0.000"));
                if(i>=2&&!cuts.Any(w=>age-2*dt>=w.from&&age-2*dt<=w.to||age>=w.from&&age<=w.to))
                {
                    double acc=((c.Eye-2*b.Eye+a.Eye)/(dt*dt)).Length();var fa=Vector3.Normalize(a.Look-a.Eye);var fb=Vector3.Normalize(b.Look-b.Eye);var fc=Vector3.Normalize(c.Look-c.Eye);double turn=((fc-2*fb+fa)/(dt*dt)).Length();
                    if(acc>worstAccel){worstAccel=acc;accelAt=s+" "+age.ToString("0.000");}if(turn>worstTurn){worstTurn=turn;turnAt=s+" "+age.ToString("0.000");}
                    Check(acc<=80,s+" camera jerks at "+age.ToString("0.000")+" ("+acc.ToString("0")+" m/s^2)");Check(turn<=60,s+" camera turn jerks at "+age.ToString("0.000")+" ("+turn.ToString("0")+" rad/s^2)");
                }
                // Framing: the chest in frame whenever the pilot is shown; at the hero the head clears the name card.
                bool shown=sc.Solid(t,CapturedRig.Chest)>.5f||sc.Holo(t,out _,out float ho,out _)&&ho>.02f;
                if(shown&&i%4==0)
                {
                    visible++;bool inFrame=InFrame(c,sc.Anchor(NativeEntranceAnchor.Chest,t),aspect,out _,out _);if(inFrame)framed++;
                    if(age>=NativeEntranceScript.NameAt){Check(inFrame,s+" loses the pilot under the name card at "+age.ToString("0.000"));InFrame(c,head,aspect,out _,out float hy);Check(hy>-.3f&&hy<.92f,s+" hero frame puts the head at "+hy.ToString("0.00"));}
                }
            }
        }
        Check(framed>=visible*.9,"the pilot is in frame only "+framed+" of "+visible+" samples");
        yield return $"entrance cameras: never under the floor, inside the pilot or a prop; still from {NativeEntranceScript.Settle} s; outside cuts, whips and the hit-stop the eye accelerates at most {worstAccel:0} m/s^2 ({accelAt}) and turns at most {worstTurn:0} rad/s^2 ({turnAt}); the pilot is in frame in {framed} of {visible} samples and always under the name card";
        // 4. Effects: a budget per frame, thinned by the effects setting; the entrance blast sim replayed at 60 Hz stays at capacity.
        int peak=0,peakLow=0;string peakAt="";long sumHigh=0,sumLow=0;int peakBlast=0;
        foreach(var (s,sc) in scripts)
        {
            for(float age=0;age<=4.8f;age+=1/30f){float t=sc.Clock(age);sink.Reset();sc.Effects(t,sink,1);if(sink.Count>peak){peak=sink.Count;peakAt=s+" "+age.ToString("0.00");}sumHigh+=sink.Count;sink.Reset();sc.Effects(t,sink,.3f);peakLow=Math.Max(peakLow,sink.Count);sumLow+=sink.Count;}
            foreach(float q in new[]{1f,.3f})
            {
                var sim=new NativeBlastSim(new NativeBlastRandom(0x1e7a).Next);int next=0;var blasts=sc.Blasts;
                for(int step=0;step<(int)(4.8f*60);step++)
                {
                    double from=step/60.0;
                    while(next<blasts.Length&&blasts[next].At<from+1/60.0){var b=blasts[next++];sim.Solid.SizeScale=sim.Glow.SizeScale=b.Size>0?b.Size:1;if(b.Impact)sim.Impact(-b.Position.X,b.Position.Y,b.Position.Z,b.Radius,b.ImpactColor,q,false,false,null,NativeEntranceStage.FloorY);else sim.Blast(b.Style,-b.Position.X,b.Position.Y,b.Position.Z,b.Radius,q,false,b.Tint,NativeEntranceStage.FloorY);}
                    sim.Update(1/60.0);if(q==1)peakBlast=Math.Max(peakBlast,sim.Solid.Drawn+sim.Glow.Drawn);
                }
                Check(next==blasts.Length,s+" fires all its blasts");Check(sim.Solid.Capacity==NativeBlastSim.SolidCapacity&&sim.Glow.Capacity==NativeBlastSim.GlowCapacity,s+" blast pools grew past their capacity");
                foreach(var b in blasts){Check(b.At>0&&b.At<NativeEntranceScript.Duration&&float.IsFinite(b.Position.X)&&float.IsFinite(b.Position.Y),s+" blast placement");if(b.Style==NativeBlastStyle.Shock&&!b.Impact)Check(b.Tint.HasValue&&!NativeBlastSim.BlueCyan(b.Tint.Value.Hot)&&!NativeBlastSim.BlueCyan(b.Tint.Value.Cool),s+" draws a blue or cyan shock");}
            }
        }
        Check(peak<=600,"an entrance draws "+peak+" effect particles in a frame ("+peakAt+")");Check(peak+peakBlast<=1024,"effects and blasts overflow the renderer's initial capacity");Check(sumLow<sumHigh*.6,"the effects setting does not thin the entrance effects ("+sumLow+" of "+sumHigh+")");
        yield return $"entrance effects: at most {peak} closed-form particles a frame ({peakAt}) plus at most {peakBlast} blast particles, within the renderer's 1024; the low effects setting draws {100.0*sumLow/sumHigh:0}% of them (peak {peakLow}); every blast pool stays at its capacity";
        // 5. Owner rules: no flat glowing disc or ring on the floor; the lightning and its light are violet, never blue or cyan.
        int pieces=0;
        foreach(var (s,sc) in scripts)for(float age=0;age<=4.8f;age+=.1f)
        {
            float t=sc.Clock(age);sink.Reset();sink.Record=true;sc.Props(t,sink,1);sink.Record=false;
            foreach(var p in sink.Pieces)
            {
                pieces++;if(!p.glow||p.p.Y>NativeEntranceStage.FloorY+.25f)continue;
                bool flat=p.s.Y<.12f&&Math.Min(p.s.X,p.s.Z)>.25f;Check(!(flat||p.shape==WeaponShape.Ring),s+" lays a glowing "+(p.shape==WeaponShape.Ring?"ring":"disc")+" on the floor at "+age.ToString("0.0"));
            }
        }
        foreach(int hex in new[]{0xc9a6ff,0xf3ecff,0xd8c4ff,0xc8aaff})Check(!NativeBlastSim.BlueCyan(hex),"Thunderstep's violet "+hex.ToString("x6")+" reads blue");
        yield return $"entrance rules: {pieces} set pieces checked, no glowing disc or ring on the floor; the only lightning (Thunderstep) and its shock are violet";
        // 6. A playthrough allocates nothing (the anchors are built on first use, above).
        var none=new Null();
        foreach(var (s,sc) in scripts)Frame(sc,0,pose,lights,none);
        long before=GC.GetAllocatedBytesForCurrentThread();
        foreach(var (s,sc) in scripts)for(float age=0;age<=4.8f;age+=1/60f)Frame(sc,age,pose,lights,none);
        long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"a playthrough allocates "+bytes+" bytes");
        yield return $"entrance playthroughs: 0 bytes allocated over 60 Hz playthroughs of all {scripts.Count} entrances (pose, camera, lights, set, effects)";
    }
    private static void Frame(NativeEntranceScript sc,float age,CapturedRig.Pose pose,NativeEntranceLights lights,Null none)
    {
        float t=sc.Clock(age);sc.Pose(t,pose,out _,out _);sc.Camera(age,out _);sc.Lights(t,lights);sc.Props(t,none,1);sc.Effects(t,none,1);sc.Ghosts(t,ghosts);sc.Holo(t,out _,out _,out _);for(int b=0;b<22;b++)sc.Solid(t,b);
    }
    private static readonly NativeEntranceGhost[] ghosts=new NativeEntranceGhost[8];
    // The match age at which presentation time `t` is reached (the clock is monotone).
    private static float AgeOf(NativeEntranceScript sc,float t){float lo=0,hi=5;for(int i=0;i<40;i++){float m=(lo+hi)*.5f;if(sc.Clock(m)<t)lo=m;else hi=m;}return hi;}
    private static float Body(Vector3 eye,Vector3 a,Vector3 b)
    {
        var ab=b-a;float u=Math.Max(0,Math.Min(1,Vector3.Dot(eye-a,ab)/Math.Max(1e-6f,ab.LengthSquared())));var n=a+ab*u;var off=eye-n;return (float)Math.Sqrt(off.X*off.X+off.Z*off.Z+(off.Y*.35f)*(off.Y*.35f));
    }
    private static bool InFrame(NativeEntranceView v,Vector3 p,float aspect,out float x,out float y)
    {
        var f=Vector3.Normalize(v.Look-v.Eye);var right=Vector3.Normalize(Vector3.Cross(f,v.Up));var up=Vector3.Cross(right,f);var d=p-v.Eye;float z=Vector3.Dot(d,f);
        float tan=(float)Math.Tan(v.Fov*Math.PI/360);x=z>0?Vector3.Dot(d,right)/(z*tan*aspect):9;y=z>0?Vector3.Dot(d,up)/(z*tan):9;return z>.05f&&Math.Abs(x)<=1&&Math.Abs(y)<=1;
    }
    private sealed class Clip{public float[] values,roots;public int frames;public float duration;}
    private static Dictionary<string,Clip> Clips(string root)
    {
        using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceMotion.json")));
        using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
        using var doc=JsonDocument.Parse(reader.ReadToEnd());var map=new Dictionary<string,Clip>();
        foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())
        {
            string key=c.GetProperty("key").GetString();if(!key.StartsWith("intro-weapon-"))continue;
            map[key]=new Clip{values=c.GetProperty("values").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray(),roots=c.GetProperty("roots").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray(),frames=c.GetProperty("frames").GetInt32(),duration=(float)c.GetProperty("duration").GetDouble()};
        }
        return map;
    }
}
