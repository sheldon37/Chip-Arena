using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

// Captured-melee runtime parity: the original character animation's poses (tools/export_melee_motion.mjs) against
// MeleeMotion over the same scripted timelines (walk/run/sprint, combos, Echo Strike, Hurricane, block impacts,
// staggers, light hits, parries, strafing and jumps), all four captured weapons, every recorded frame.
internal static class MeleeMotionChecks
{
    private static readonly string[] Keys={"gx","gz","yaw","vx","vy","vz","g","sp","la","ai","sa","su","bt","lb","hs","ca","cu","cy","sta","stu","sd","fu","rk","ra","ru","ry","rdx","rdy","rdz","rpx","rpy","rpz","rz","rt","rs"};
    private static readonly int[] Ends={CapturedRig.RHand,CapturedRig.LHand,CapturedRig.Head,CapturedRig.RHand,CapturedRig.LHand,CapturedRig.LToe,CapturedRig.RToe};
    private static readonly Vector3[] Local={new Vector3(0,0,1),new Vector3(0,0,1),new Vector3(0,.3f,0),new Vector3(0,.2f,0),new Vector3(0,.2f,0),new Vector3(0,0,.1f),new Vector3(0,0,.1f)};
    public static IEnumerable<string> Run(string root)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("MELEE_FIXTURE")??Path.Combine(root,"Tests/melee-motion-parity.json")));
        float dt=(float)document.RootElement.GetProperty("dt").GetDouble();int total=0;float worst=0;
        foreach(var scenario in document.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            string spec=scenario.GetProperty("spec").GetString(),name=scenario.GetProperty("name").GetString();
            var motion=new MeleeMotion{Impacts=new MeleeImpacts()};var input=new Dictionary<string,double?>();int f=0,compared=0;float max=0,sum=0;int n=0;
            foreach(var frame in scenario.GetProperty("frames").EnumerateArray())
            {
                foreach(string k in Keys)if(frame.TryGetProperty(k,out var v))input[k]=v.ValueKind==JsonValueKind.Null?(double?)null:v.GetDouble();
                double FrameTime(int i)=>1+i/60.0+.0013*((i*.618034)%1);float t=(float)FrameTime(f),step=f>0?(float)(FrameTime(f)-FrameTime(f-1)):dt;f++;
                double? I(string k)=>input.TryGetValue(k,out var v)?v:null;float F(string k,float d=0)=>(float)(I(k)??d);
                var view=new MeleeView{Id="pilot-"+spec,Spec=spec,Yaw=F("yaw"),Vel=new Vector3(F("vx"),F("vy"),F("vz")),Grounded=F("g")>0,Sprinting=F("sp")>0,LastAttack=F("la",-100),
                    Windup=(float)scenario.GetProperty("windup").GetDouble(),AttackRate=(float)scenario.GetProperty("rate").GetDouble(),AttackIndex=(int)F("ai"),SwingAt=I("sa").HasValue?F("sa"):float.NaN,SwingUntil=I("su").HasValue?F("su"):float.NaN,
                    Hurricane=scenario.GetProperty("hurricane").GetBoolean(),BlockTime=F("bt"),LastBlock=F("lb"),HitStunUntil=F("hs")};
                if(I("ca").HasValue){view.Commit=true;view.CommitAt=F("ca");view.CommitUntil=F("cu");view.CommitYaw=F("cy");view.StepAt=F("sta");view.StepUntil=F("stu");view.StepDistance=F("sd");view.Followup=F("fu")>0;}
                if(I("ra").HasValue){view.Reaction=true;view.Parry=F("rk")>0;view.ReactionAt=F("ra");view.ReactionUntil=F("ru");view.ReactionYaw=F("ry");view.ReactionDir=new Vector3(F("rdx"),F("rdy"),F("rdz"));view.ReactionPoint=new Vector3(F("rpx"),F("rpy"),F("rpz"));view.ReactionZone=(int)F("rz");view.ReactionTurn=F("rt");view.ReactionStrength=F("rs");}
                if(frame.TryGetProperty("ev",out var events))foreach(var e in events.EnumerateArray()){string kind=e[0].GetString();float at=(float)e[1].GetDouble();if(kind=="hit")motion.Impacts.RecordHit(view.Id,at);else motion.Impacts.RecordBlock(view.Id,at);}
                motion.Rig.GroupPosition=new Vector3(F("gx"),0,F("gz"));motion.Rig.GroupYaw=motion.PresentedYaw(view,t,view.Yaw);
                motion.Sample(view,t,step);
                if(!frame.TryGetProperty("out",out var expected))continue;
                // The source drops the shield's captured guard through a stagger (it has no captured stagger), popping the hips
                // ~16 cm to the gait height and back; the port keeps the captured guard (documented deviation). The feet the
                // gait resumes from then differ by about a centimetre, so a shield timeline is compared up to its first stun.
                if(spec=="shield"&&view.HitStunUntil>0)continue;
                Check(Math.Abs(MeleeMath.Wrap(motion.Rig.GroupYaw-(float)expected.GetProperty("yaw").GetDouble()))<1e-3f,spec+" "+name+" presented yaw differs at frame "+(f-1));
                var points=expected.GetProperty("p");int i=0;compared++;
                for(int b=0;b<MeleeRig.N+Ends.Length;b++)
                {
                    Vector3 got=b<MeleeRig.N?motion.Rig.ChainPoint(b,-1):motion.Rig.ChainPoint(Ends[b-MeleeRig.N],-1)+Vector3.Transform(Local[b-MeleeRig.N],motion.Rig.ChainQuat(Ends[b-MeleeRig.N],-1));
                    var want=new Vector3((float)points[i].GetDouble(),(float)points[i+1].GetDouble(),(float)points[i+2].GetDouble());i+=3;
                    float e=Vector3.Distance(got,want);sum+=e;n++;if(e>max)max=e;if(Environment.GetEnvironmentVariable("MELEE_TRACE")==name+":"+spec&&e>.001f)Console.WriteLine("  frame "+(f-1)+" t="+t.ToString("0.000")+" point "+b+" err "+(e*1000).ToString("0.0")+"mm "+motion.State);
                    if(e>.005f&&Environment.GetEnvironmentVariable("MELEE_TRACE")==null)throw new Exception(spec+" "+name+" frame "+(f-1)+" point "+b+" differs by "+e.ToString("0.0000")+" m: "+got+" vs "+want+" ("+motion.State+")");
                }
            }
            total+=compared;worst=Math.Max(worst,max);
            yield return "captured melee parity "+spec+" "+name+": "+compared+" frames, max "+(max*1000).ToString("0.00")+" mm, mean "+(sum/Math.Max(1,n)*1000).ToString("0.000")+" mm";
        }
        yield return "captured melee parity: "+total+" frames within "+(worst*1000).ToString("0.00")+" mm of the original";
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
