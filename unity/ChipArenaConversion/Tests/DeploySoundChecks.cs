using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ChipArena.UnityConversion;
using Pol=ChipArena.UnityConversion.NativeDeployPresentation.Polarity;
internal static class DeploySoundChecks
{
    private static int count;
    private static void Check(bool ok,string message){count++;if(!ok)throw new Exception("Deployment audio: "+message);}
    public static void Run(string root)
    {
        count=0;var keys=new HashSet<string>();var schedule=new NativeDeploySparks.Cue[12];var heard=new NativeDeploySparks.Cue[24];
        foreach(Pol polarity in Enum.GetValues(typeof(Pol)))for(int machine=0;machine<2;machine++)
        {
            int n=NativeDeploySparks.Schedule(schedule,machine,3,new[]{polarity,polarity,polarity});
            for(int i=0;i<n;i++){var cue=schedule[i];var sound=NativeDeploySounds.Of(cue);Check(sound.Key!=null&&sound.Index>=0&&sound.Index<NativeDeploySounds.Count,"every scheduled cue has a synthesized sound");keys.Add(sound.Key);Check(Math.Abs(sound.Pan)<=.55f&&sound.Gain>0&&sound.Gain<=1&&sound.Rate>.9f&&sound.Rate<1.1f,"bounded mix");if(cue.Reel>=0)Check(Math.Sign(sound.Pan)==Math.Sign(cue.Reel-1),"left/centre/right panning");}
            var seen=new HashSet<string>();float before=float.NaN;
            for(int frame=0;frame<240;frame++){float now=frame/60f;int got=NativeDeploySparks.Heard(schedule,n,before,now,heard,0);before=now;for(int i=0;i<got;i++)Check(seen.Add(heard[i].Kind+":"+heard[i].Reel),"cue heard once across frames");}
            Check(seen.Count==n,"all moments heard");Check(NativeDeploySparks.Heard(schedule,n,float.NaN,99,heard,0)==0,"late observation does not replay settled audio");
            Check(NativeDeploySparks.Heard(schedule,n,4,0,heard,0)==1&&heard[0].Kind==NativeDeploySparks.CueKind.Spin,"reroll restarts only at Spin");
        }
        Check(keys.Count==NativeDeploySounds.Count,"all twelve sounds reachable");
        var clock=new NativeDeployClock();var state=new ExpeditionDeploymentState{instanceRolls=3,mutatorRolls=2};clock.Observe(state,100);
        int scheduled=NativeDeploySparks.Schedule(schedule,1,3,new[]{Pol.Boon,Pol.Boon,Pol.Boon});
        Check(NativeDeploySparks.Heard(schedule,scheduled,float.NaN,clock.SinceMutatorRoll(100),heard,0)==0,"real late-join clock is silent");
        clock.Observe(state,101);Check(NativeDeploySparks.Heard(schedule,scheduled,float.NaN,clock.SinceMutatorRoll(101),heard,0)==0,"unchanged late-join state remains silent");
        state.mutatorRolls++;clock.Observe(state,102);Check(NativeDeploySparks.Heard(schedule,scheduled,float.NaN,clock.SinceMutatorRoll(102),heard,0)==1,"new authoritative roll heard");
        for(int i=0;i<NativeDeploySounds.Count;i++)
        {
            var pcm=NativeDeploySounds.Samples(i);Check(pcm.Length==(int)Math.Ceiling(NativeDeploySounds.Duration(i)*NativeDeploySounds.SampleRate),"clip duration matches its declared envelope");
            Check(pcm.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&Math.Abs(x)<1),"finite unclipped samples");Check(pcm[0]==0&&pcm[pcm.Length-1]==0,"click-free endpoints");
            Check(pcm.Any(x=>Math.Abs(x)>.05)&&pcm.Select(x=>(double)x*x).Average()>.00001,"audible signal");Check(pcm.SequenceEqual(NativeDeploySounds.Samples(i)),"deterministic synth without global random state");
        }
        Check(!NativeDeploySounds.Samples(0).SequenceEqual(NativeDeploySounds.Samples(3)),"distinct machine timbres");
        Check(NativeDeploySounds.Duration(0)==NativeDeployPresentation.StopTime(2),"motor ends with final reel");
        var limiter=new NativeSoundLimiter();Check(limiter.Cue("deploy.destination.stop:0",0,.7)>0&&limiter.Cue("deploy.destination.stop:0",.01,.7)==0&&limiter.Cue("deploy.destination.stop:1",.01,.7)>0,"existing limiter suppresses duplicates, preserves different reels");
        string runtime=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDeployAudio.cs")),cast=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDeployCast.cs"));
        Check(runtime.Contains("limiter.Cue(")&&runtime.Contains("keyed>=8")&&runtime.Contains("gain*Effects")&&runtime.Contains("Muted||Effects<=0"),"shared effects controls and voice limits");
        Check(cast.Contains("deployScene.Draw(camera,input,dt);deployAudio=audio;deployAudio?.DeployFrame(deployScene.Stage)")&&cast.Contains("deployAudio?.DeployStop();deployAudio=null;"),"draw edge consumed immediately and leave stops station audio");
        Console.WriteLine("PASS Deployment audio: "+count+" checks (cue mapping, exact-once/late join/reroll, panning, deterministic PCM, envelopes and limiter wiring)");
    }
}
