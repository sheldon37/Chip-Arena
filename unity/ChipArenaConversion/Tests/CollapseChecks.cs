#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class CollapseChecks
    {
        private static Vector3 V(JsonElement e)=>e.ValueKind==JsonValueKind.Array?new Vector3(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle()):new Vector3(e.GetProperty("x").GetSingle(),e.GetProperty("y").GetSingle(),e.GetProperty("z").GetSingle());
        private static Quaternion Q(JsonElement e)=>new Quaternion(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle(),e[3].GetSingle());
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/collapse-parity.json")));int count=0;
            foreach(var sample in document.RootElement.GetProperty("cases").EnumerateArray())
            {
                var parts=sample.GetProperty("parts").EnumerateArray().Select(p=>{var part=new CollapsePart{Name=p.GetProperty("name").GetString(),Parent=p.TryGetProperty("parent",out var parent)?parent.GetString():null,Position=V(p.GetProperty("position")),Rotation=Q(p.GetProperty("quaternion")),Anchor=p.TryGetProperty("anchor",out var a)?V(a):Vector3.Zero,Free=p.GetProperty("free").GetBoolean(),Boxes=p.GetProperty("boxes").EnumerateArray().Select(b=>new CollapseBox{Center=V(b.GetProperty("center")),Half=V(b.GetProperty("half")),Rotation=Q(b.GetProperty("quaternion")),Ankle=b.GetProperty("ankle").GetBoolean()}).ToArray()};if(p.TryGetProperty("ankle",out var ankle)){part.HasAnkle=true;part.AnklePivot=V(ankle.GetProperty("pivot"));part.AnkleFrame=Q(ankle.GetProperty("frame"));}return part;}).ToArray();
                var m=sample.GetProperty("motion");var o=sample.GetProperty("options");var props=o.GetProperty("props").EnumerateArray().Select(p=>new CollapseProp{Position=V(p.GetProperty("pos")),Size=V(p.GetProperty("size"))}).ToArray();
                var collapse=new NativeCollapse(parts,m.GetProperty("facing").GetSingle(),V(m.GetProperty("velocity")),o.GetProperty("ground").GetSingle(),V(o.GetProperty("shot")),o.GetProperty("severity").GetSingle(),props);
                void Near(float actual,float expected,float tolerance,string name){if(!MathUtil.Finite(actual)||Math.Abs(actual-expected)>tolerance)throw new Exception("Collapse "+count+" "+name+": "+actual+" != "+expected);}
                Near(collapse.Release,sample.GetProperty("release").GetSingle(),.00001f,"release");Near(collapse.FallAngle,sample.GetProperty("fall").GetSingle(),.00003f,"cover/ground limit");
                foreach(var frame in sample.GetProperty("samples").EnumerateArray())
                {var expected=frame.GetProperty("values").EnumerateArray().ToDictionary(p=>p.GetProperty("name").GetString());float age=frame.GetProperty("age").GetSingle();int visited=0;
                    collapse.Each(age,(name,p,lin,ang)=>{var e=expected[name];Near(Vector3.Distance(p.Position,V(e.GetProperty("position"))),0,.0002f,name+" position at "+age);Near(1-Math.Abs(Quaternion.Dot(p.Rotation,Q(e.GetProperty("rotation")))),0,.000003f,name+" rotation");if(e.TryGetProperty("ankle",out var ankle))Near(1-Math.Abs(Quaternion.Dot(p.Ankle,Q(ankle))),0,.000003f,name+" foot");Near(Vector3.Distance(lin,V(e.GetProperty("linear"))),0,.008f,name+" linear handoff");Near(Vector3.Distance(ang,V(e.GetProperty("angular"))),0,.012f,name+" angular handoff");visited++;});if(visited!=expected.Count)throw new Exception("Collapse drove a detached part");}
                count++;
            }
            if(NativeSlenderRules.Static(.5f,true,1)!=.555f||Math.Abs(NativeSlenderRules.Static(0,false,.175f)-.225f)>.00001f||NativeSlenderRules.Static(1,true,0)!=1)throw new Exception("Slender source envelope");
            foreach(var samples in new[]{NativeSlenderRules.Drone(12000),NativeSlenderRules.Noise(12000)})if(samples.Any(x=>!MathUtil.Finite(x))||samples.All(x=>Math.Abs(x)<.0001))throw new Exception("Slender synth is silent or unstable");
            Console.WriteLine("PASS "+count+" original body collapses: ground/cover limits, arbitrary death pose, detached head, planted feet and release velocities; victim-static envelope and finite audio");
        }
    }
}
#endif
