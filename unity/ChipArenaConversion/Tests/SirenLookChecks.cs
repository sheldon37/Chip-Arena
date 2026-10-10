#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Siren's animated look (Core/NativeSirenLook.cs) against tools/export_siren_look.mjs's run of the pinned source
    // (Tests/siren-look-parity.json): every piece's joint and transform after applySirenLook for scripted states, the
    // shapeEye outlines, and 60 fps timelines of animateSirenRig (melee rend, ranged hellfire, swaps to ranged and back, a
    // melee slash, damnation and the grab) replayed from the replicated inputs only (clip key, age, length, ranged flag and
    // the snapshot's time) through the exported face tracks (Resources/SirenLook.json) and NativeSirenLookSmoother; for the
    // swaps and the slash also the body (the idle clips blended through the swap with its shudder, else the clip itself)
    // against every source joint, and the swaps again as a guest whose render clock trails its newest frame.
    internal static class SirenLookChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Siren look parity: "+what);count++;}
        private static void Near(double a,double b,double tolerance,string what){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static SirenLookState State(JsonElement s)=>new SirenLookState{Ranged=D(s,"ranged"),Open=D(s,"open"),Squint=D(s,"squint"),Anger=D(s,"anger"),Glow=D(s,"glow"),Time=D(s,"time"),Lean=D(s,"lean"),Flare=D(s,"flare"),Clock=D(s,"clock")};
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        // The source's top three rows (12 values) of one piece against the Core's row-major 16.
        private static void Piece(double[] m,int piece,JsonElement rows,double tolerance,string what)
        {int i=0;foreach(var v in rows.EnumerateArray()){Near(m[piece*16+i/4*4+i%4],v.GetDouble(),tolerance,what+" piece "+piece+" ["+i+"]");i++;}Check(i==12,what+" rows");}
        private sealed class Motion {public string key;public float duration;public bool loop;public int frames;public float[] values;}
        // Resources/SourceMotion.json's Siren body clips (tools/export_motion.mjs: 22 bones × position, rotation per frame).
        private static Dictionary<string,Motion> Bodies(string root)
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceMotion.json")));var env=doc.RootElement;
            Check(env.GetProperty("schema").GetInt32()==1&&env.GetProperty("commit").GetString()=="05950eaef80ff2483a9c44d898cad98be1a76bbc","motion envelope");
            using var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(env.GetProperty("data").GetString())),CompressionMode.Decompress);using var reader=new StreamReader(zip);
            using var set=JsonDocument.Parse(reader.ReadToEnd());var map=new Dictionary<string,Motion>();
            foreach(var c in set.RootElement.GetProperty("clips").EnumerateArray())
            {
                string key=c.GetProperty("key").GetString();if(!key.StartsWith("siren-"))continue;var m=new Motion{key=key,duration=(float)c.GetProperty("duration").GetDouble(),loop=c.GetProperty("loop").GetBoolean(),frames=c.GetProperty("frames").GetInt32()};
                var values=new List<float>();foreach(var v in c.GetProperty("values").EnumerateArray())values.Add((float)v.GetDouble());m.values=values.ToArray();Check(m.values.Length==m.frames*22*7,"motion length "+key);map.Add(key,m);
            }
            return map;
        }
        // Runtime/NativeSourceMotion.Sample: looped clips wrap, others span `duration`; positions lerp, rotations slerp.
        private static void Sample(Motion clip,float age,float duration,CapturedRig.Pose pose)
        {
            float wrapped=age-(float)Math.Floor(age/clip.duration)*clip.duration,fraction=clip.loop?Math.Clamp(wrapped,0,clip.duration)/clip.duration:Math.Clamp(age/(duration>0?duration:clip.duration),0,1);
            float index=fraction*(clip.frames-1);int a=Math.Min(clip.frames-1,(int)index),b=Math.Min(clip.frames-1,a+1);float t=index-a;var v=clip.values;
            for(int bone=0;bone<22;bone++)
            {
                int first=(a*22+bone)*7,last=(b*22+bone)*7;
                pose.Position[bone]=System.Numerics.Vector3.Lerp(new System.Numerics.Vector3(v[first],v[first+1],v[first+2]),new System.Numerics.Vector3(v[last],v[last+1],v[last+2]),t);
                pose.Rotation[bone]=System.Numerics.Quaternion.Slerp(new System.Numerics.Quaternion(v[first+3],v[first+4],v[first+5],v[first+6]),new System.Numerics.Quaternion(v[last+3],v[last+4],v[last+5],v[last+6]),t);
            }
        }
        // The body the runtime shows for this frame: NativeSourceMotion.Pilot's clip, or (idle, while the swap moves the body)
        // ArenaPresentation.SirenSwapBody's blend of the two idle clips.
        private static void Body(Dictionary<string,Motion> bodies,string key,double time,double age,double duration,NativeSirenLookSmoother smoother,in SirenLookState state,CapturedRig.Pose pose,CapturedRig.Pose other)
        {
            if(key.StartsWith("siren-idle-")&&smoother.Swapping(time))
            {
                Sample(bodies[NativeSirenLook.IdleMelee],(float)time,0,pose);Sample(bodies[NativeSirenLook.IdleRanged],(float)time,0,other);
                NativeSirenLook.SwapBody(pose,other,state.Ranged,smoother.Shudder(time),time,pose);
            }
            else Sample(bodies[key],(float)(key.StartsWith("siren-idle-")?time:age),(float)duration,pose);
        }
        private static double BodyGap(CapturedRig.Pose pose,JsonElement body,string what)
        {
            var q=new List<double>();foreach(var v in body.EnumerateArray())q.Add(v.GetDouble());Check(q.Count==22*4,what+" body joints");double worst=0;
            for(int b=0;b<22;b++)
            {
                var r=pose.Rotation[b];double sign=r.X*q[b*4]+r.Y*q[b*4+1]+r.Z*q[b*4+2]+r.W*q[b*4+3]<0?-1:1;
                double gap=Math.Max(Math.Max(Math.Abs(sign*r.X-q[b*4]),Math.Abs(sign*r.Y-q[b*4+1])),Math.Max(Math.Abs(sign*r.Z-q[b*4+2]),Math.Abs(sign*r.W-q[b*4+3])));
                Check(gap<=2e-4,what+" joint "+b+" rotation off by "+gap);worst=Math.Max(worst,gap);
            }
            return worst;
        }
        public static Dictionary<string,SirenLookClip> Tracks(string root)
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SirenLook.json")));var env=doc.RootElement;
            Check(env.GetProperty("schema").GetInt32()==1&&env.GetProperty("commit").GetString()=="05950eaef80ff2483a9c44d898cad98be1a76bbc","track envelope");
            using var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(env.GetProperty("data").GetString())),CompressionMode.Decompress);using var reader=new StreamReader(zip);
            var set=JsonSerializer.Deserialize<SirenLookSet>(reader.ReadToEnd(),new JsonSerializerOptions{IncludeFields=true});Check(set.fps==60,"track fps");
            var map=new Dictionary<string,SirenLookClip>();foreach(var c in set.clips){Check(c.values.Length==c.frames*2,"track length "+c.key);map.Add(c.key,c);}return map;
        }
        // The black flame (owner playtest 2026-10-10): flames.ts's palette, the baked flame pools of the Siren's entrance and
        // kills, and the Unity finish (ink rims on the normal, per-part strengths, rimmed flame cubes, breathing), so neither
        // the lavender 0xaea4c7 cubes nor the flat rimless body can come back. Returns the flame rows checked.
        private static readonly float[] Fallback={.423268f,.371238f,.571125f};
        private static bool IsFallback(float r,float g,float b)=>Math.Abs(r-Fallback[0])<.004f&&Math.Abs(g-Fallback[1])<.004f&&Math.Abs(b-Fallback[2])<.004f;
        private static JsonDocument Bank(string root,string name)
        {
            using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/"+name+".json")));
            using var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString())),CompressionMode.Decompress);using var reader=new StreamReader(zip);
            return JsonDocument.Parse(reader.ReadToEnd());
        }
        private static int Flames(string root)
        {
            // flames.ts TONES and FADES: black cores (ink toward smoke) that age to violet tips; ember, smoke and soot as authored.
            Check(NativeFlameField.Ink==0x0a080e&&NativeFlameField.SmokeTint==0x17121f&&NativeFlameField.EmberTint==0xe6defa,"flame start palette");
            Check(NativeFlameField.Fade(NativeFlameTone.Black)==0x5b5078&&NativeFlameField.Fade(NativeFlameTone.Soot)==0x2b2436&&NativeFlameField.Fade(NativeFlameTone.Smoke)==0x7d7299&&NativeFlameField.Fade(NativeFlameTone.Ember)==0x2a2233,"flame fade palette");
            {var rng=new Random(5);for(int i=0;i<64;i++){NativeFlameField.Start(NativeFlameTone.Black,rng.NextDouble,out double r,out double g,out double b);Check(Math.Max(r,Math.Max(g,b))<.012,"black flame starts black: "+r+","+g+","+b);}}
            // The bank: no section anywhere keeps the exporter's fallback colour; the entrance's and the kills' flame slots
            // (one 24-vertex cube per FlameField slot) are white and unlit, tinted per frame by their scene track.
            var slots=new Dictionary<string,HashSet<int>>{{"stage-dev-siren",new HashSet<int>()},{"death-siren-crush",new HashSet<int>()},{"death-siren-dissipate",new HashSet<int>()}};
            using(var bank=Bank(root,"SourceModels"))foreach(var m in bank.RootElement.GetProperty("models").EnumerateArray())
            {
                string id=m.GetProperty("id").GetString();slots.TryGetValue(id,out var mine);
                foreach(var sec in m.GetProperty("sections").EnumerateArray())
                {
                    var c=sec.GetProperty("colors");var e=sec.GetProperty("emission");int n=c.GetArrayLength()/4;
                    Check(n==0||!IsFallback(c[0].GetSingle(),c[1].GetSingle(),c[2].GetSingle()),"fallback colour in "+id);
                    if(mine!=null&&n==24&&!sec.GetProperty("ink").GetBoolean()&&c[0].GetSingle()==1&&c[1].GetSingle()==1&&c[2].GetSingle()==1&&e[0].GetSingle()==1&&e[2].GetSingle()==1)mine.Add(sec.GetProperty("bone").GetInt32());
                }
            }
            int rows=0;
            using(var scenes=Bank(root,"SceneMotion"))foreach(var clip in scenes.RootElement.GetProperty("clips").EnumerateArray())
            {
                string key=clip.GetProperty("key").GetString();if(!slots.TryGetValue(key,out var tracks))continue;
                Check(tracks.Count>=420,key+" flame slots: "+tracks.Count);int frames=clip.GetProperty("frames").GetInt32();var v=clip.GetProperty("values");int shown=0,dark=0,varying=0;
                foreach(int t in tracks)
                {
                    bool varies=false;float r0=0,g0=0,b0=0;
                    for(int f=0;f<frames;f++)
                    {
                        int o=(t*frames+f)*20;float sx=v[o].GetSingle(),sy=v[o+1].GetSingle(),sz=v[o+2].GetSingle(),r=v[o+17].GetSingle(),g=v[o+18].GetSingle(),b=v[o+19].GetSingle();
                        if(f==0){r0=r;g0=g;b0=b;}else if(r!=r0||g!=g0||b!=b0)varies=true;
                        Check(!IsFallback(r,g,b),key+" flame row in the fallback colour");
                        if(v[o+16].GetSingle()<=.001f||sx*sx+sy*sy+sz*sz<1e-10f)continue;shown++;if(Math.Max(r,Math.Max(g,b))<.06f)dark++;
                    }
                    if(varies)varying++;
                }
                // Black flame: most burning cubes stay near black (raw violet tips, smoke and embers are the rest).
                Check(shown>1000&&dark>=shown*.7&&varying>=tracks.Count*.9,key+" black flame rows: "+dark+" dark of "+shown+", "+varying+" varying slots");rows+=shown;
            }
            Check(rows>0,"flame rows");
            // The exporter keeps the per-instance colour of colour-less ShaderMaterials (the FlameField) as a display value.
            string exporter=File.ReadAllText(Path.Combine(root,"tools/export_cinematics.mjs"));
            Check(exporter.Contains("rawInstanceColor")&&exporter.Contains("convertSRGBToLinear()"),"exporter keeps raw flame instance colours");
            // The Unity finish: the ink rim follows the surface normal (NativeSourceLit, NativePortrait), buildSiren's per-part
            // strengths, rimmed flame cubes unless reduced, and a breath that peaks at the source's look.
            string lit=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeSourceLit.shader")),portrait=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativePortrait.shader"));
            Check(lit.Contains("dot(normalize(i.viewDir),normalize(i.worldNormal))")&&!lit.Contains("dot(normalize(i.viewDir),float3(0,0,1))"),"ink rim on the surface normal");
            Check(portrait.Contains("abs(dot(n,toCamera))")&&!portrait.Contains("dot(toCamera,float3(0,0,1))"),"portrait ink rim on the surface normal");
            Check(NativeSirenLook.BodyRim==.2&&NativeSirenLook.BodyFlow==.025&&NativeSirenLook.ClawRim==.75&&NativeSirenLook.ClawFlow==.02&&NativeSirenLook.TongueRim==.42&&NativeSirenLook.TongueFlow==.1,"buildSiren ink strengths");
            Check(NativeSirenLook.FlameRim>0&&NativeSirenLook.FlameRim<=.5,"flame cube rim");
            string look=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeSirenLookPresentation.cs")),fx=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDevEffects.cs"));
            Check(look.Contains("NativeSirenLook.ClawRim")&&look.Contains("NativeSirenLook.TongueRim")&&look.Contains("NativeSirenLook.BodyRim"),"per-part ink strengths applied");
            Check(fx.Contains("if(flat)draw.Cube(")&&fx.Contains("draw.FlameCube(")&&fx.Contains("bool flat=HaloReduced"),"rimmed flame cubes, flat when reduced");
            for(double t=0;t<6;t+=.05){double k=NativeSirenLook.Breath(t),grin=NativeSirenLook.GrinGlow(k),halo=NativeSirenLook.HaloSize(k);Check(k>=0&&k<=1&&grin>=.88&&grin<=1&&halo>=.92&&halo<=1,"breath range at "+t);}
            Check(NativeSirenLook.GrinGlow(1)==1&&NativeSirenLook.HaloSize(1)==1&&look.Contains("t.Pose.Breath=HaloReduced?1:"),"breath peaks at the source look; reduced effects hold it");
            return rows;
        }
        public static void Run(string root)
        {
            count=0;var tracks=Tracks(root);
            Check(NativeSirenRules.Rule("hellfire").Windup==(float)NativeSirenLook.HellfireHit&&NativeSirenRules.Rule("damnation").Windup==(float)NativeSirenLook.DamnationHit,"flare releases are the rule windups");
            // Every Siren body clip (tools/motion-export.json) has its face track on the same frames.
            using(var motion=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/motion-export.json"))))
            {
                int matched=0;foreach(var c in motion.RootElement.GetProperty("clips").EnumerateArray())
                {
                    string key=c.GetProperty("key").GetString();if(!key.StartsWith("siren-"))continue;Check(tracks.TryGetValue(key,out var t),"face track for "+key);
                    Check(t.frames==c.GetProperty("frames").GetInt32()&&Math.Abs(t.duration-c.GetProperty("duration").GetDouble())<1e-6&&t.loop==c.GetProperty("loop").GetBoolean(),"face track timing "+key);matched++;
                }
                Check(matched==tracks.Count&&matched==23,"face tracks "+matched+"/"+tracks.Count);
            }
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/siren-look-parity.json")));var f=doc.RootElement;
            Check(f.GetProperty("fps").GetInt32()==60&&D(f,"smoothing")==NativeSirenLook.FaceRate&&D(f,"swap")==NativeSirenLook.Swap,"fixture constants");
            int n=0;foreach(var j in f.GetProperty("joints").EnumerateArray()){Check(NativeSirenLook.Joint(n)==j.GetInt32(),"joint of piece "+n);n++;}Check(n==NativeSirenLook.Count,"piece count "+n);
            var xy=new double[NativeSirenLook.EyeSegments*6];int eyes=0;
            foreach(var e in f.GetProperty("eyes").EnumerateArray())
            {
                NativeSirenLook.EyeOutline(e.GetProperty("side").GetInt32(),D(e,"anger"),xy);int i=0;
                foreach(var v in e.GetProperty("xy").EnumerateArray()){Near(xy[i],v.GetDouble(),2e-6,"eye "+eyes+" ["+i+"]");i++;}Check(i==xy.Length,"eye vertices");eyes++;
            }
            var m=new double[NativeSirenLook.Count*16];var shown=new bool[NativeSirenLook.Count];int states=0;
            foreach(var s in f.GetProperty("states").EnumerateArray())
            {
                var state=State(s.GetProperty("state"));NativeSirenLook.Evaluate(state,m,shown);string what="state "+states;int p=0;
                foreach(var rows in s.GetProperty("pieces").EnumerateArray())Piece(m,p++,rows,2e-7,what);Check(p==NativeSirenLook.Count,what+" pieces");
                p=0;foreach(var v in s.GetProperty("shown").EnumerateArray()){Check(shown[p]==(v.GetInt32()==1),what+" shown "+p);p++;}
                Near(NativeSirenLook.EyeHaloAlpha(state),D(s,"eyeHalo"),1e-9,what+" eye halo");Near(NativeSirenLook.Quantized(state.Anger),D(s,"anger"),1e-9,what+" anger");states++;
            }
            // Timelines: the face targets come from the 60 fps tracks at the replicated age (interpolated between track frames
            // for clips whose length is not a whole number of frames), smoothed by the runtime's own frame step. On the host the
            // snapshot's time is the frame's own; a guest (lag frames) renders `lag` frames behind its newest frame, whose
            // mode, clip and time it reads while its ages run on the render clock.
            var keys=new List<string>();foreach(var k in f.GetProperty("keys").EnumerateArray())keys.Add(k.GetString());
            var keep=new List<int>();foreach(var k in f.GetProperty("timelinePieces").EnumerateArray())keep.Add(k.GetInt32());
            var bodies=Bodies(root);var pose=new CapturedRig.Pose();var other=new CapturedRig.Pose();
            double worst=0,worstBody=0;int frames=0,balls=0,bodyFrames=0,guestFrames=0,swaps=0;var ballMatrices=new double[32];
            void Replay(string name,List<double> drive,int count2,Dictionary<int,JsonElement> samples,int lag)
            {
                var smoother=new NativeSirenLookSmoother();
                for(int i=0;i<count2;i++)
                {
                    int newest=Math.Min(count2-1,i+lag);string key=keys[(int)drive[i*5]];double time=drive[i*5+1],age=drive[i*5+2],duration=drive[i*5+3];
                    bool ranged=drive[newest*5+4]>.5;double stamp=drive[newest*5+1];if(lag>0)key=keys[(int)drive[newest*5]];
                    Check(NativeSirenLook.Targets(tracks[key],age,duration,out double open,out double squint),name+" track "+key);
                    string action=key.StartsWith("siren-idle-")?null:key.Substring(6).Split('-')[0];
                    var state=smoother.Step(time,1.0/60,open,squint,NativeSirenLook.Flare(action,age),ranged,0,true,stamp);frames++;if(lag>0)guestFrames++;
                    if(action=="swipe")Check(state.Ranged==0,name+" frame "+i+": the claws stay at full length through a melee slash");
                    if(!samples.TryGetValue(i,out var sample))continue;var look=sample.GetProperty("look");string what=name+(lag>0?" (guest "+lag+" frames behind)":"")+" frame "+i;
                    foreach(var (field,value) in new[]{("ranged",state.Ranged),("open",state.Open),("squint",state.Squint),("anger",state.Anger),("flare",state.Flare),("lean",state.Lean),("glow",state.Glow)})
                    {double expected=D(look,field);worst=Math.Max(worst,Math.Abs(value-expected));Near(value,expected,1e-4,what+" "+field);}
                    var charge=sample.GetProperty("charge");bool ball=NativeSirenLook.Charge(action,age,time,ballMatrices);Check(ball==(charge.ValueKind==JsonValueKind.Array),what+" Damnation ball shown");
                    if(ball){int c=0;foreach(var v in charge.EnumerateArray()){Near(ballMatrices[c/12*16+c%12/4*4+c%4],v.GetDouble(),1e-4,what+" ball ["+c+"]");c++;}Check(c==24,what+" ball rows");balls++;}
                    NativeSirenLook.Evaluate(state,m,shown);int p=0;
                    foreach(var rows in sample.GetProperty("pieces").EnumerateArray())Piece(m,keep[p++],rows,1e-4,what);
                    p=0;foreach(var v in sample.GetProperty("shown").EnumerateArray()){Check(shown[p]==(v.GetInt32()==1),what+" shown "+p);p++;}
                    if(!sample.TryGetProperty("body",out var body))continue;
                    Body(bodies,key,time,age,duration,smoother,state,pose,other);worstBody=Math.Max(worstBody,BodyGap(pose,body,what));bodyFrames++;
                }
            }
            foreach(var t in f.GetProperty("timelines").EnumerateArray())
            {
                string name=t.GetProperty("name").GetString();var drive=new List<double>();foreach(var v in t.GetProperty("drive").EnumerateArray())drive.Add(v.GetDouble());
                var samples=new Dictionary<int,JsonElement>();foreach(var s in t.GetProperty("samples").EnumerateArray())samples[s.GetProperty("frame").GetInt32()]=s;
                int count2=t.GetProperty("frames").GetInt32();Check(drive.Count==count2*5,name+" drive");
                Replay(name,drive,count2,samples,0);
                // A guest sees the swap's frame five frames early on its render clock; its blend still starts at the swap.
                if(name.StartsWith("swap-to-")){Replay(name,drive,count2,samples,5);swaps++;}
            }
            Check(swaps==2&&bodyFrames>40,"swap timelines with bodies: "+swaps+", "+bodyFrames+" body frames");
            // The mode stamp, and a Siren drawn again after a gap (respawn, first person, the killcam) settling into its mode.
            Check(NativeSirenLook.ModeStamp(10.1,10)==10.1&&NativeSirenLook.ModeStamp(9.99,10)==9.99&&NativeSirenLook.ModeStamp(50,10)==10&&NativeSirenLook.ModeStamp(double.NaN,10)==10,"mode stamp");
            {
                var g=new NativeSirenLookSmoother();g.Step(10,0,0,0,0,false,0,true,10);var a=g.Step(10+1.0/60,1.0/60,0,0,0,true,0,true,10+1.0/60);Check(a.Ranged==0&&g.Swapping(10.1)&&g.ModeAt==10+1.0/60,"a swap starts at its stamp");
                var b=g.Step(11.5,.05,0,0,0,false,0,true,11.5);Check(b.Ranged==0&&!g.Swapping(11.5),"after a gap the mode is settled, not swapped again");
                var c=g.Step(5,0,0,0,0,true,0,true,5);Check(c.Ranged==1&&!g.Swapping(5),"a replay that jumps back starts settled");
                Check(NativeSirenLook.ShudderWeight(10,10.2)==0&&NativeSirenLook.ShudderWeight(10.75,10)==0&&Math.Abs(NativeSirenLook.ShudderWeight(10.25,10)-1)<1e-12,"shudder window");
            }
            // Late join / lost flip frame / replay seek retain the actual swap age, even on the first rendered frame.
            foreach(double renderAt in new[]{10.10,10.22,11.0})
            {
                var exact=new NativeSirenLookSmoother();var state=exact.Step(renderAt,0,0,0,0,true,0,true,renderAt+.08,10);
                Near(state.Ranged,NativeSirenLook.SwapBlend(true,renderAt,10),1e-9,"replicated mode age");Check(exact.ModeAt==10,"late sample moved mode deadline");
                exact.Step(renderAt+.01,.01,0,0,0,true,0,true,renderAt+.09,10);Check(exact.ModeAt==10,"unchanged mode restarts blend");
            }
            // The Siren takes no sword-set layer (the blade floor guard bent her right claw through the Slam and the Rush).
            Check(!NativePoseRoute.SwordLayers(new Actor{SourceSpec="siren",Weapon=WeaponId.Sword})&&NativePoseRoute.SwordLayers(new Actor{SourceSpec="sword",Weapon=WeaponId.Sword}),"no sword layers on the Siren");
            int flameRows=Flames(root);
            Console.WriteLine($"PASS Siren look parity: {flameRows} baked black-flame rows, {NativeSirenLook.Count} piece joints, {eyes} eye outlines, {states} applySirenLook states, {frames} timeline frames ({guestFrames} as a guest; largest look-state gap {worst:0.0e+0}, {balls} Damnation balls), {bodyFrames} body frames (largest joint gap {worstBody:0.0e+0}), {tracks.Count} face tracks; {count} checks");
        }
    }
}
#endif
