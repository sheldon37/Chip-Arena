using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

// Magic gait parity: the original character animation's magic pilots (tools/export_magic_motion.mjs: walk, run, sprint,
// turn, stop, strafe, jump and casts on the move for all four schools) against MagicMotion over the same timelines, every
// recorded frame: joints, hand and toe tips, and the staff's shaft and face axes. The owner's playtest regression (the
// torso frozen while walking) is checked directly: the pelvis, spine and chest must swing through the stride.
internal static class MagicMotionChecks
{
    private static readonly int[] Ends={CapturedRig.RHand,CapturedRig.Head,CapturedRig.RHand,CapturedRig.LToe,CapturedRig.RToe};
    private static readonly Vector3[] Local={new Vector3(0,0,1),new Vector3(0,.3f,0),new Vector3(0,.2f,0),new Vector3(0,0,.1f),new Vector3(0,0,.1f)};
    private static readonly int[] Torso={CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest};
    public static IEnumerable<string> Run(string root)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/magic-motion-parity.json")));
        int total=0;float worst=0;
        foreach(var scenario in document.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            string spec=scenario.GetProperty("spec").GetString(),name=scenario.GetProperty("name").GetString();
            int element=spec=="fire"?1:spec=="water"?2:spec=="lightning"?3:spec=="darkness"?4:0;Check(element>0,"Unknown magic spec "+spec);
            var motion=new MagicMotion();var pose=new CapturedRig.Pose();var channels=new float[13];
            var a=new Actor{Id=7,Weapon=WeaponId.Sword,MagicVisual=element,SourceSpec=spec,CastRate=1};
            int f=0,compared=0,n=0;float max=0,sum=0;
            // Torso joint yaw range (local Euler y, radians) over the grounded walking and running frames.
            var portLo=new float[3];var portHi=new float[3];
            for(int k=0;k<3;k++){portLo[k]=float.MaxValue;portHi[k]=float.MinValue;}
            foreach(var frame in scenario.GetProperty("frames").EnumerateArray())
            {
                var i=frame.GetProperty("i");float In(int k)=>(float)i[k].GetDouble();
                double FrameTime(int x)=>1+x/60.0+.0013*((x*.618034)%1);float t=(float)FrameTime(f);f++;
                a.Yaw=-In(2)*180/(float)Math.PI;a.Velocity=new V3(-In(3),In(4),In(5));a.Grounded=In(6)>0;a.Sprinting=In(7)>0;a.AttackAt=In(8);a.LastShot=In(9);
                bool hasOut=frame.TryGetProperty("out",out var expected);
                motion.Rig.GroupPosition=new Vector3(In(0),0,In(1));motion.Rig.GroupYaw=hasOut?(float)expected.GetProperty("yaw").GetDouble():In(2);
                motion.Sample(a,t,pose,channels,0);motion.Finish(pose,t,false);
                if(!hasOut)continue;
                var points=expected.GetProperty("p");int at=0;compared++;
                Vector3 Want(){var v=new Vector3((float)points[at].GetDouble(),(float)points[at+1].GetDouble(),(float)points[at+2].GetDouble());at+=3;return v;}
                var got=new Vector3[MeleeRig.N+Ends.Length+2];
                for(int b=0;b<MeleeRig.N;b++)got[b]=motion.Rig.ChainPoint(b,-1);
                for(int e=0;e<Ends.Length;e++)got[MeleeRig.N+e]=motion.Rig.ChainPoint(Ends[e],-1)+Vector3.Transform(Local[e],motion.Rig.ChainQuat(Ends[e],-1));
                // The port holds the staff on the wrist; its frame is the source weapon's frame (orientHeld's target).
                Quaternion staff=motion.Rig.ChainQuat(CapturedRig.LHand,-1);
                got[MeleeRig.N+Ends.Length]=Vector3.Transform(Vector3.UnitZ,staff);got[MeleeRig.N+Ends.Length+1]=Vector3.Transform(Vector3.UnitY,staff);
                for(int b=0;b<got.Length;b++)
                {
                    var want=Want();float e=Vector3.Distance(got[b],want);sum+=e;n++;if(e>max)max=e;
                    if(e>.005f)throw new Exception(spec+" "+name+" frame "+(f-1)+" point "+b+" differs by "+e.ToString("0.0000")+" m: "+got[b]+" vs "+want);
                }
                float speed=(float)Math.Sqrt(In(3)*In(3)+In(5)*In(5));
                if(speed>2&&In(6)>0)
                    for(int k=0;k<3;k++){float yaw=motion.Rig.E[Torso[k]].Y;portLo[k]=Math.Min(portLo[k],yaw);portHi[k]=Math.Max(portHi[k],yaw);}
            }
            if(name.Contains("walk")||name.Contains("run"))
                for(int k=0;k<3;k++)
                    Check(portHi[k]-portLo[k]>.06f,spec+" "+name+" torso joint "+Torso[k]+" frozen through the stride: yaw range "+(portHi[k]-portLo[k]).ToString("0.000")+" rad");
            total+=compared;worst=Math.Max(worst,max);
            yield return "magic gait parity "+spec+" "+name+": "+compared+" frames, max "+(max*1000).ToString("0.00")+" mm, mean "+(sum/Math.Max(1,n)*1000).ToString("0.000")+" mm, torso yaw swing "+
                (portHi[0]-portLo[0]).ToString("0.00")+"/"+(portHi[1]-portLo[1]).ToString("0.00")+"/"+(portHi[2]-portLo[2]).ToString("0.00")+" rad";
        }
        yield return "magic gait parity: "+total+" frames within "+(worst*1000).ToString("0.00")+" mm of the original";
        // The first-person view model keeps its own staff framing (MagicPose.Apply), unchanged by the third-person gait.
        var fp=new CapturedRig.Pose();var fa=new Actor{MagicVisual=1,Velocity=new V3(0,0,4)};MagicPose.Apply(fp,fa,1,true,new float[13],0);
        Check(Math.Abs(MeleeMath.AngleTo(fp.Rotation[CapturedRig.Spine],Quaternion.Identity))<1e-6f,"First-person magic torso changed");
        yield return "magic first-person view model keeps its settled torso";
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
