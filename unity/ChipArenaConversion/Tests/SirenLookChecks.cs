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
    // shapeEye outlines, and 60 fps timelines of animateSirenRig (melee rend, ranged hellfire, a swap to ranged, damnation and
    // the grab) replayed from the replicated inputs only (clip key, age, length, ranged flag) through the exported face
    // tracks (Resources/SirenLook.json) and NativeSirenLookSmoother.
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
        public static Dictionary<string,SirenLookClip> Tracks(string root)
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SirenLook.json")));var env=doc.RootElement;
            Check(env.GetProperty("schema").GetInt32()==1&&env.GetProperty("commit").GetString()=="05950eaef80ff2483a9c44d898cad98be1a76bbc","track envelope");
            using var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(env.GetProperty("data").GetString())),CompressionMode.Decompress);using var reader=new StreamReader(zip);
            var set=JsonSerializer.Deserialize<SirenLookSet>(reader.ReadToEnd(),new JsonSerializerOptions{IncludeFields=true});Check(set.fps==60,"track fps");
            var map=new Dictionary<string,SirenLookClip>();foreach(var c in set.clips){Check(c.values.Length==c.frames*2,"track length "+c.key);map.Add(c.key,c);}return map;
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
            // for clips whose length is not a whole number of frames), smoothed by the runtime's own frame step.
            var keys=new List<string>();foreach(var k in f.GetProperty("keys").EnumerateArray())keys.Add(k.GetString());
            var keep=new List<int>();foreach(var k in f.GetProperty("timelinePieces").EnumerateArray())keep.Add(k.GetInt32());
            double worst=0;int frames=0,balls=0;var ballMatrices=new double[32];
            foreach(var t in f.GetProperty("timelines").EnumerateArray())
            {
                string name=t.GetProperty("name").GetString();var drive=new List<double>();foreach(var v in t.GetProperty("drive").EnumerateArray())drive.Add(v.GetDouble());
                var samples=new Dictionary<int,JsonElement>();foreach(var s in t.GetProperty("samples").EnumerateArray())samples[s.GetProperty("frame").GetInt32()]=s;
                var smoother=new NativeSirenLookSmoother();int count2=t.GetProperty("frames").GetInt32();Check(drive.Count==count2*5,name+" drive");
                for(int i=0;i<count2;i++)
                {
                    string key=keys[(int)drive[i*5]];double time=drive[i*5+1],age=drive[i*5+2],duration=drive[i*5+3];bool ranged=drive[i*5+4]>.5;
                    Check(NativeSirenLook.Targets(tracks[key],age,duration,out double open,out double squint),name+" track "+key);
                    string action=key.StartsWith("siren-idle-")?null:key.Substring(6).Split('-')[0];
                    var state=smoother.Step(time,1.0/60,open,squint,NativeSirenLook.Flare(action,age),ranged,0,true);frames++;
                    if(!samples.TryGetValue(i,out var sample))continue;var look=sample.GetProperty("look");string what=name+" frame "+i;
                    foreach(var (field,value) in new[]{("ranged",state.Ranged),("open",state.Open),("squint",state.Squint),("anger",state.Anger),("flare",state.Flare),("lean",state.Lean),("glow",state.Glow)})
                    {double expected=D(look,field);worst=Math.Max(worst,Math.Abs(value-expected));Near(value,expected,1e-4,what+" "+field);}
                    var charge=sample.GetProperty("charge");bool ball=NativeSirenLook.Charge(action,age,time,ballMatrices);Check(ball==(charge.ValueKind==JsonValueKind.Array),what+" Damnation ball shown");
                    if(ball){int c=0;foreach(var v in charge.EnumerateArray()){Near(ballMatrices[c/12*16+c%12/4*4+c%4],v.GetDouble(),1e-4,what+" ball ["+c+"]");c++;}Check(c==24,what+" ball rows");balls++;}
                    NativeSirenLook.Evaluate(state,m,shown);int p=0;
                    foreach(var rows in sample.GetProperty("pieces").EnumerateArray())Piece(m,keep[p++],rows,1e-4,what);
                    p=0;foreach(var v in sample.GetProperty("shown").EnumerateArray()){Check(shown[p]==(v.GetInt32()==1),what+" shown "+p);p++;}
                }
            }
            Console.WriteLine($"PASS Siren look parity: {NativeSirenLook.Count} piece joints, {eyes} eye outlines, {states} applySirenLook states, {frames} timeline frames (largest look-state gap {worst:0.0e+0}, {balls} Damnation balls), {tracks.Count} face tracks; {count} checks");
        }
    }
}
#endif
