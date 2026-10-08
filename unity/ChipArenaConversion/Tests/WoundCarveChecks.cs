#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class WoundCarveChecks
    {
        public static void Run(string root)
        {
            var options=new JsonSerializerOptions{IncludeFields=true};using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/wound-carve-parity.json")));int index=0;
            foreach(var sample in doc.RootElement.GetProperty("cases").EnumerateArray())
            {
                var spec=JsonSerializer.Deserialize<WoundCarveSpec>(sample.GetProperty("spec").GetRawText(),options);var result=NativeWoundCarve.Carve(new[]{new WoundSolid{min=new WoundVector(-.2,-.25,-.12),max=new WoundVector(.2,.25,.12),color=new WoundVector(1,1,1)}},spec);var expected=sample.GetProperty("expected");string label=index+" "+spec.kind+" "+spec.trauma+" axis "+spec.axis+" sign "+spec.sign;
                if(result.Count!=expected.GetProperty("count").GetInt32()||result.Count(s=>s.flesh)!=expected.GetProperty("flesh").GetInt32())throw new Exception("Carve topology: "+label+" actual "+result.Count+"/"+result.Count(s=>s.flesh)+" expected "+expected.GetProperty("count")+"/"+expected.GetProperty("flesh"));
                double[] sums=new double[10];for(int i=0;i<result.Count;i++){var s=result[i];double[] values={s.min.x,s.min.y,s.min.z,s.max.x,s.max.y,s.max.z,s.color.x,s.color.y,s.color.z};for(int j=0;j<9;j++)sums[j]+=values[j]*(i+1);sums[9]+=(s.max.x-s.min.x)*(s.max.y-s.min.y)*(s.max.z-s.min.z);}
                int n=0;foreach(var value in expected.GetProperty("sums").EnumerateArray()){double want=value.GetDouble();if(Math.Abs(sums[n]-want)>Math.Max(1e-8,Math.Abs(want)*1e-8))throw new Exception("Carve geometry/material: "+label+" component "+n+" "+sums[n]+" != "+want);n++;}index++;
            }
            int h=0;foreach(var want in doc.RootElement.GetProperty("hash").EnumerateArray()){if(NativeWoundCarve.Hash(9137,h,-h,h*7)!=want.GetDouble())throw new Exception("Wound hash "+h);h++;}
            Console.WriteLine("PASS original voxel wound carving: "+index+" topology/geometry/material cases and "+h+" unsigned hashes");
        }
    }
}
#endif
