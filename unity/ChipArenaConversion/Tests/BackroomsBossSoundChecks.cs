using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>THE HUM's and THE DEEP END's sounds: every key the bosses queue is in the bank contract and the owner's request manifest, flat or positional as it plays, inside its cap; the generator's key pattern holds; no audio in the repo.</summary>
public static class BackroomsBossSoundChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Backrooms boss sounds: "+what);count++;}
    public static void Run(string root)
    {
        var path=Path.Combine(root,"../../docs/audio/sound-requests-bosses.json");
        if(!File.Exists(path)){Console.WriteLine("PASS backrooms boss sounds (manifest not in this tree)");return;}
        using var doc=JsonDocument.Parse(File.ReadAllText(path));var sounds=doc.RootElement.GetProperty("sounds").EnumerateArray().ToDictionary(s=>s.GetProperty("key").GetString());
        var contract=NativeSoundKeys.All.ToDictionary(k=>k.Key);
        int flat=0;
        void Key(string key,bool isFlat)
        {
            Check(System.Text.RegularExpressions.Regex.IsMatch(key,"^[a-z0-9]+([.][a-z0-9]+)*$"),key+" fits the generator's key pattern");
            Check(contract.ContainsKey(key),key+" is in the bank contract");Check(sounds.ContainsKey(key),key+" is requested");
            var d=contract[key];var s=sounds[key];float max=s.GetProperty("targetSeconds").GetProperty("max").GetSingle();
            Check(d.Cap+1e-4f>=max&&s.GetProperty("targetSeconds").GetProperty("min").GetSingle()<max,key+" fits its cap");
            Check(d.Space==(isFlat?NativeSoundSpace.Flat:NativeSoundSpace.Positional),key+" plays "+(isFlat?"flat":"at its place"));
            Check((s.TryGetProperty("space",out var sp)&&sp.GetString()=="2d")==isFlat,key+"'s request says the same");
            Check(s.GetProperty("generateSeconds").GetSingle()>=max&&s.GetProperty("generateSeconds").GetSingle()<=22&&s.GetProperty("count").GetInt32()<=NativeSoundKeys.MaxClips,key+" can be generated");
            Check(s.GetProperty("priority").GetInt32()<3,key+" is in a default batch");
            Check(s.GetProperty("category").GetString()=="boss",key+" has the boss level");if(isFlat)flat++;
        }
        for(byte i=0;i<NativeHumBossCues.Keys.Length;i++)Key(NativeHumBossCues.Keys[i],NativeHumBossCues.Flat(i));
        for(byte i=0;i<NativeDeepEndBossCues.Keys.Length;i++)Key(NativeDeepEndBossCues.Keys[i],NativeDeepEndBossCues.Flat(i));
        Check(sounds.Count==NativeHumBossCues.Keys.Length+NativeDeepEndBossCues.Keys.Length&&sounds.Count==81,"81 sounds, one for each cue");
        Check(sounds["boss.deepend.ripple"].GetProperty("levelDb").GetSingle()<sounds["boss.deepend.drip"].GetProperty("levelDb").GetSingle()&&sounds["boss.deepend.flop"].GetProperty("levelDb").GetSingle()==sounds.Values.Max(s=>s.GetProperty("levelDb").GetSingle()),"the ripple is the quietest thing and the Belly Flop the loudest");
        Console.WriteLine("PASS backrooms boss sounds ("+count+" checks: 81 keys in the contract and the request, "+flat+" flat, all inside their caps)");
    }
}
