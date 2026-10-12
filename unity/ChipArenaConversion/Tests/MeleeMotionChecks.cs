using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

// Captured-melee runtime parity: the original character animation's poses (tools/export_melee_motion.mjs) against
// MeleeMotion over the same scripted timelines (walk/run/sprint, combos, Echo Strike, Hurricane, block impacts,
// staggers, light hits, parries, strafing and jumps, and Extended Reach stretches), all four captured weapons, every
// recorded frame.
// Owner-requested deviations (docs/MELEE-FEEL.md) are Unity-owned: a scenario listed in Deviations is compared with
// Tests/melee-motion-pinned.json, written from MeleeMotion with CHIP_PIN_MELEE=1 after an intended change, instead of the
// original; every other scenario stays on the original within 5 mm. A torso-only deviation still keeps its lower body
// (body, pelvis, legs and feet) on the original within 5 mm.
internal static class MeleeMotionChecks
{
    private static readonly string[] Keys={"gx","gz","yaw","vx","vy","vz","g","sp","la","ai","sa","su","bt","lb","hs","ca","cu","cy","sta","stu","sd","fu","rk","ra","ru","ry","rdx","rdy","rdz","rpx","rpy","rpz","rz","rt","rs"};
    private static readonly int[] Ends={CapturedRig.RHand,CapturedRig.LHand,CapturedRig.Head,CapturedRig.RHand,CapturedRig.LHand,CapturedRig.LToe,CapturedRig.RToe};
    private static readonly Vector3[] Local={new Vector3(0,0,1),new Vector3(0,0,1),new Vector3(0,.3f,0),new Vector3(0,.2f,0),new Vector3(0,.2f,0),new Vector3(0,0,.1f),new Vector3(0,0,.1f)};
    public const string PinnedFile="Tests/melee-motion-pinned.json";
    private const string Since="2026-10-12";
    // Scenario (without its -reach suffix) -> what the owner asked to change. "torso": the steady locomotion torso (owner
    // 2026-10-12, "notice how the body rocks when moving"): the captured walk/run torso swing is damped about its carry posture
    // and the procedural pelvis twist countered, so walking and running frames move the chest, arms and blade.
    private static readonly Dictionary<string,string> Deviations=new Dictionary<string,string>{{"stand-walk-run-stop","torso"},{"run-attack-block","torso"},{"strafe-jump","torso"}};
    private static string Deviation(string name){int r=name.IndexOf("-reach",StringComparison.Ordinal);return Deviations.TryGetValue(r<0?name:name.Substring(0,r),out var d)?d:null;}
    // The points a torso-only deviation leaves on the original: body, pelvis, spine root, both legs and the toe tips.
    private static bool LowerBody(int point)=>point<=2||point>=CapturedRig.LHip&&point<MeleeRig.N||point>=MeleeRig.N+5;
    private const float SourceTolerance=.005f,PinnedTolerance=.0005f;
    public static IEnumerable<string> Run(string root)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("MELEE_FIXTURE")??Path.Combine(root,"Tests/melee-motion-parity.json")));
        bool pin=Environment.GetEnvironmentVariable("CHIP_PIN_MELEE")=="1";string pinPath=Path.Combine(root,PinnedFile);
        JsonDocument pinnedDoc=pin?null:JsonDocument.Parse(File.ReadAllText(pinPath));var pinned=new Dictionary<string,JsonElement>();
        if(!pin)
        {
            var source=pinnedDoc.RootElement.GetProperty("source");
            Check(source.GetProperty("kind").GetString()=="unity"&&source.GetProperty("since").GetString()==Since,"the pinned melee frames are Unity-owned since "+Since);
            foreach(var s in pinnedDoc.RootElement.GetProperty("scenarios").EnumerateArray())pinned[s.GetProperty("spec").GetString()+" "+s.GetProperty("name").GetString()]=s;
        }
        var written=new StringBuilder();int pinnedScenarios=0;
        float dt=(float)document.RootElement.GetProperty("dt").GetDouble();int total=0,deviated=0,lowerFrames=0;float worst=0,worstPinned=0,worstLower=0;
        foreach(var scenario in document.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            string spec=scenario.GetProperty("spec").GetString(),name=scenario.GetProperty("name").GetString(),key=spec+" "+name,deviation=Deviation(name);float reach=scenario.TryGetProperty("reach",out var stretch)?(float)stretch.GetDouble():1;
            JsonElement pinnedFrames=default;bool hasPin=false;
            if(deviation!=null&&!pin){Check(pinned.TryGetValue(key,out var ps),"the pinned melee frames hold "+key+" (regenerate with CHIP_PIN_MELEE=1)");Check(ps.GetProperty("deviation").GetString()==deviation,key+" is pinned for its "+deviation+" deviation");pinnedFrames=ps.GetProperty("frames");hasPin=true;pinned.Remove(key);}
            var motion=new MeleeMotion{Impacts=new MeleeImpacts()};var input=new Dictionary<string,double?>();int f=0,compared=0,pinIndex=0;float max=0,sum=0,sourceMax=0,lowerMax=0;int n=0;
            if(pin&&deviation!=null){if(pinnedScenarios++>0)written.Append(',');written.Append("{\"spec\":\"").Append(spec).Append("\",\"name\":\"").Append(name).Append("\",\"deviation\":\"").Append(deviation).Append("\",\"frames\":[");}
            foreach(var frame in scenario.GetProperty("frames").EnumerateArray())
            {
                foreach(string k in Keys)if(frame.TryGetProperty(k,out var v))input[k]=v.ValueKind==JsonValueKind.Null?(double?)null:v.GetDouble();
                double FrameTime(int i)=>1+i/60.0+.0013*((i*.618034)%1);float t=(float)FrameTime(f),step=f>0?(float)(FrameTime(f)-FrameTime(f-1)):dt;f++;
                double? I(string k)=>input.TryGetValue(k,out var v)?v:null;float F(string k,float d=0)=>(float)(I(k)??d);
                var view=new MeleeView{Id="pilot-"+spec,Spec=spec,Yaw=F("yaw"),Vel=new Vector3(F("vx"),F("vy"),F("vz")),Grounded=F("g")>0,Sprinting=F("sp")>0,LastAttack=F("la",-100),
                    Windup=(float)scenario.GetProperty("windup").GetDouble(),AttackRate=(float)scenario.GetProperty("rate").GetDouble(),AttackIndex=(int)F("ai"),SwingAt=I("sa").HasValue?F("sa"):float.NaN,SwingUntil=I("su").HasValue?F("su"):float.NaN,
                    Hurricane=scenario.GetProperty("hurricane").GetBoolean(),BlockTime=F("bt"),LastBlock=F("lb"),HitStunUntil=F("hs"),Reach=reach};
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
                var points=expected.GetProperty("p");int i=0;compared++;
                JsonElement want2=hasPin?pinnedFrames[pinIndex]:default;
                if(pin&&deviation!=null){if(pinIndex>0)written.Append(',');written.Append('[').Append(Q(motion.Rig.GroupYaw));}
                if(hasPin)Check(Math.Abs(MeleeMath.Wrap(motion.Rig.GroupYaw-(float)(want2[0].GetInt32()*1e-4)))<1e-3f,spec+" "+name+" presented yaw differs from its pin at frame "+(f-1));
                else if(deviation==null)Check(Math.Abs(MeleeMath.Wrap(motion.Rig.GroupYaw-(float)expected.GetProperty("yaw").GetDouble()))<1e-3f,spec+" "+name+" presented yaw differs at frame "+(f-1));
                for(int b=0;b<MeleeRig.N+Ends.Length;b++)
                {
                    Vector3 got=b<MeleeRig.N?motion.Rig.ChainPoint(b,-1):motion.Rig.ChainPoint(Ends[b-MeleeRig.N],-1)+Vector3.Transform(Local[b-MeleeRig.N],motion.Rig.ChainQuat(Ends[b-MeleeRig.N],-1));
                    var want=new Vector3((float)points[i].GetDouble(),(float)points[i+1].GetDouble(),(float)points[i+2].GetDouble());i+=3;
                    float e=Vector3.Distance(got,want);sourceMax=Math.Max(sourceMax,e);
                    if(pin&&deviation!=null)written.Append(',').Append(Q(got.X)).Append(',').Append(Q(got.Y)).Append(',').Append(Q(got.Z));
                    if(deviation!=null)
                    {
                        // The pinned point (0.1 mm units), and for a torso-only deviation the lower body on the original.
                        if(deviation=="torso"&&LowerBody(b)){lowerMax=Math.Max(lowerMax,e);if(e>SourceTolerance)throw new Exception(spec+" "+name+" frame "+(f-1)+" lower-body point "+b+" left the original by "+e.ToString("0.0000")+" m ("+motion.State+")");}
                        if(!hasPin)continue;
                        var p=new Vector3(want2[1+b*3].GetInt32()*1e-4f,want2[2+b*3].GetInt32()*1e-4f,want2[3+b*3].GetInt32()*1e-4f);float ep=Vector3.Distance(got,p);sum+=ep;n++;if(ep>max)max=ep;
                        if(ep>PinnedTolerance)throw new Exception(spec+" "+name+" frame "+(f-1)+" point "+b+" differs from its Unity-owned pin by "+ep.ToString("0.0000")+" m: "+got+" vs "+p+" ("+motion.State+")");
                        continue;
                    }
                    sum+=e;n++;if(e>max)max=e;if(Environment.GetEnvironmentVariable("MELEE_TRACE")==name+":"+spec&&e>.001f)Console.WriteLine("  frame "+(f-1)+" t="+t.ToString("0.000")+" point "+b+" err "+(e*1000).ToString("0.0")+"mm "+motion.State);
                    if(e>SourceTolerance&&Environment.GetEnvironmentVariable("MELEE_TRACE")==null)throw new Exception(spec+" "+name+" frame "+(f-1)+" point "+b+" differs by "+e.ToString("0.0000")+" m: "+got+" vs "+want+" ("+motion.State+")");
                }
                if(pin&&deviation!=null)written.Append(']');
                pinIndex++;
            }
            if(pin&&deviation!=null)written.Append("]}");
            if(deviation!=null)
            {
                // A listed scenario must still deviate (else it returns to the original and leaves the list).
                Check(sourceMax>SourceTolerance,key+" no longer deviates from the original: remove it from MeleeMotionChecks.Deviations");
                if(hasPin)Check(pinnedFrames.GetArrayLength()==compared,key+" pins every compared frame");
                deviated+=compared;worstPinned=Math.Max(worstPinned,max);if(deviation=="torso"){lowerFrames+=compared;worstLower=Math.Max(worstLower,lowerMax);}
                yield return "captured melee "+deviation+" deviation "+spec+" "+name+": "+compared+" frames on the Unity-owned pin (max "+(max*1000).ToString("0.00")+" mm; the original differs by up to "+(sourceMax*1000).ToString("0")+" mm"+(deviation=="torso"?", lower body within "+(lowerMax*1000).ToString("0.00")+" mm of it":"")+")";
                continue;
            }
            total+=compared;worst=Math.Max(worst,max);
            yield return "captured melee parity "+spec+" "+name+": "+compared+" frames, max "+(max*1000).ToString("0.00")+" mm, mean "+(sum/Math.Max(1,n)*1000).ToString("0.000")+" mm";
        }
        if(pin)
        {
            File.WriteAllText(pinPath,"{\"source\":{\"kind\":\"unity\",\"core\":\"Assets/ChipArena/Core/MeleeMotion.cs\",\"since\":\""+Since+"\",\"why\":\"owner-requested melee feel changes (docs/MELEE-FEEL.md): these scenarios leave the original character animation\",\"inputs\":\"Tests/melee-motion-parity.json (same timelines)\",\"units\":\"per recorded frame [presented yaw, then the 29 points x,y,z] in 1e-4 rad and 1e-4 m\",\"regenerate\":\"CHIP_PIN_MELEE=1 (Tests/MeleeMotionChecks.cs)\"},\"scenarios\":["+written+"]}\n");
            yield return "captured melee: wrote "+pinnedScenarios+" Unity-owned pinned scenarios to "+PinnedFile;
        }
        else{Check(pinned.Count==0,"the pinned melee frames hold no stale scenario ("+string.Join(", ",pinned.Keys)+")");pinnedDoc.Dispose();}
        yield return "captured melee parity: "+total+" frames within "+(worst*1000).ToString("0.00")+" mm of the original; "+deviated+" owner-deviation frames within "+(worstPinned*1000).ToString("0.00")+" mm of their Unity-owned pins ("+lowerFrames+" torso-only frames keep the lower body within "+(worstLower*1000).ToString("0.00")+" mm of the original)";
    }
    private static string Q(float v)=>((int)Math.Round(v*1e4)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
