using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class InteractionChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string message){if(!ok)throw new Exception("Interaction pose: "+message);}
        using(var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/interaction-parity.json"))))
        {
            var pose=new CapturedRig.Pose();int n=0;
            foreach(var row in json.RootElement.GetProperty("rows").EnumerateArray())
            {
                float time=row.GetProperty("time").GetSingle(),at=row.GetProperty("at").GetSingle(),crush=row.GetProperty("crushAt").GetSingle();
                NativeSirenVictimPose.Sample(pose,time,at,crush);var angles=row.GetProperty("rotations");int i=0;
                foreach(var bone in row.GetProperty("bones").EnumerateArray())
                {
                    var expected=CapturedRig.Euler(angles[i++].GetSingle(),angles[i++].GetSingle(),angles[i++].GetSingle());
                    Check(Math.Abs(Quaternion.Dot(expected,pose.Rotation[bone.GetInt32()]))>.99999f,"Electron victim pose parity at "+time+" bone "+bone);
                }
                pose.World();foreach(var p in pose.WorldPosition)Check(MathUtil.Finite(p.X)&&MathUtil.Finite(p.Y)&&MathUtil.Finite(p.Z),"nonfinite held rig");n++;
            }
            Console.WriteLine("PASS Siren victim: "+n+" original Electron pose samples, every animated joint and finite hierarchy");
        }
        var a=new Actor{Id=2,GrabOwner=1,GrabAt=10,GrabCrushAt=13.2f,GrabUntil=13.8f};
        Check(NativeSirenVictimPose.Active(a,9.85f)&&NativeSirenVictimPose.Active(a,13.19f),"guest presentation lead / hold");
        Check(!NativeSirenVictimPose.Active(a,13.2f),"crush cutoff");a.GrabOwner=0;Check(!NativeSirenVictimPose.Active(a,11),"release");a.GrabOwner=1;a.Hp=0;Check(!NativeSirenVictimPose.Active(a,11),"death");
        Check(!NativeReviveRules.Active(0,-100)&&!NativeReviveRules.Active(0,float.NaN),"fresh pilot regenerated");
        Check(NativeReviveRules.Active(9.8f,10)&&NativeReviveRules.Active(11.39f,10)&&!NativeReviveRules.Active(11.41f,10),"replica lead / expiry");
        Check(Math.Abs(NativeReviveRules.Front(.64f)-1)<1e-6&&NativeReviveRules.Standing(.79f)==0&&Math.Abs(NativeReviveRules.Standing(1.1f)-.5f)<1e-5&&NativeReviveRules.Glow(1.4f)<1e-6f,"source scan / rise timeline");
        Check(NativeReviveRules.InPlace(new V3(),new V3(1,0,1))&&!NativeReviveRules.InPlace(new V3(),new V3(2,0,0)),"body / pad distinction");
        for(int i=0;i<100;i++){float s=i/99f;Check(NativeReviveRules.Flight(.8f,s)==1,"missing part incomplete at rise");var v=NativeReviveRules.Scatter(7,i);Check(v==NativeReviveRules.Scatter(7,i)&&v.Y>=.15f&&v.Length()<1.6f,"bounded deterministic reconstruction");}
        Console.WriteLine("PASS PvE repair: original scan/stand boundaries, guest clock lead, pad selection and complete deterministic voxel arrival");
    }
}
