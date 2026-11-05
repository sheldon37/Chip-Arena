using System;
using System.IO;
using System.Diagnostics;
using ChipArena.UnityConversion;
internal static class IntroPerformanceChecks
{
    public static void Run(string root)
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception("Intro performance: "+message);}
        var sync=new NativeMusicSync();double dsp=0,target=0,play=0;Check(!sync.SeekStable(0,0,0),"fresh aligned clock");
        for(int i=0;i<30;i++){dsp+=.35;play+=.35;target+=.1;Check(!sync.SeekStable(play,target,dsp),"capped simulation during repeated stalls must not rewind the song");}
        int seeks=0;for(int i=0;i<120;i++){dsp+=1d/60;play+=1d/60;target+=1d/60;if(sync.SeekStable(play,target,dsp)){seeks++;play=target;}}
        Check(seeks==1,"one correction after the game clock settles");dsp+=.3;target+=7;Check(sync.SeekStable(play,target,dsp),"authored cut still seeks");
        dsp+=.3;target=0;Check(sync.SeekStable(play,0,dsp),"loop wrap still seeks");sync.Reset();Check(sync.SeekStable(90,0,0),"new epoch resets settling state");
        var watch=Stopwatch.StartNew();var original=NativeDeployArt.BackTile();var bytes=NativeDeployCardCache.Pack(original);var decoded=NativeDeployCardCache.Unpack(bytes);watch.Stop();
        Check(bytes.Length==256*256*4&&decoded.Length==original.Length,"full RGBA tile");for(int i=0;i<bytes.Length;i++)Check(Math.Abs(decoded[i]-Math.Max(0,Math.Min(1,original[i])))<=.5/255+.000001,"art preserved within storage quantization");
        Check(NativeDeployCardCache.Key("../bad",true)==null,"cache key cannot escape resource path");foreach(var id in NativeExpeditionMutators.Ids)Check(NativeDeployCardCache.Key(id,true)!=null,"every mutator cache key");
        bool bad=false;try{NativeDeployCardCache.Unpack(new byte[4]);}catch(FormatException){bad=true;}Check(bad,"reject partial tile");
        var scene=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDeployScene.cs"));Check(scene.Contains("NativeDeployCardCache.Unpack(pixels)")&&scene.Contains("Deploy card back"),"runtime uses baked art and existing back while strips upload");
        Console.WriteLine("PASS intro performance: "+checks+" checks; cold reference back raster/pack/decode "+watch.Elapsed.TotalMilliseconds.ToString("F1")+" ms (not Unity/GPU)");
    }
}
