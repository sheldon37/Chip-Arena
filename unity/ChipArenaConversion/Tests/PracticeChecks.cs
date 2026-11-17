using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class PracticeChecks
{
    public static void Run(string root)
    {
        int checks=0;
        void Check(bool ok,string label){if(!ok)throw new Exception("Practice: "+label);checks++;}
        int phases=0;
        foreach(var boss in NativeExpeditionCatalogue.Bosses)
        {
            phases+=boss.Phases;
            int bit=NativePracticeRules.Bit(boss.Id);
            Check(bit!=0 && NativePracticeRules.Phases(boss.Id)==boss.Phases,boss.Id);
            Check(!NativePracticeRules.Available(boss.Id,0,false,false),"fresh account");
            Check(NativePracticeRules.Available(boss.Id,0,true,false),"DEV");
            Check(!NativePracticeRules.Available(boss.Id,0,true,true),"mock fresh");
            Check(NativePracticeRules.Available(boss.Id,bit,true,true),"mock earned");
            Check(NativePracticeRules.Available(boss.Id,bit,false,false),"normal earned");
            Check(!NativePracticeRules.Available(boss.Id,63^bit,false,false),"other boss");
        }
        Check(phases==20 && !NativePracticeRules.Available("bogus",63,true,false),"catalogue");
        var data=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});
        var first=NativeContentRules.Eligible(data,"pistol",new System.Collections.Generic.Dictionary<string,int>(),"").First();
        var build=new NativeLabBuild("pistol","",new[]{first.id});
        Check(NativeLabBuild.Decode(build.Code,data,NativeAccess.Guest)?.Code==build.Code,"round trip");
        Check(NativeLabBuild.Decode("CA1|pistol||bogus",data,NativeAccess.Guest)==null,"removed chip");
        Check(NativeLabBuild.Decode("CA1|bogus||",data,NativeAccess.Guest)==null,"unknown weapon");
        Check(NativeLabBuild.Decode("CA1|pistol|garbage|",data,NativeAccess.Guest)==null,"unknown technique");
        Check(NativeLabBuild.Decode("CA1|pistol||"+string.Join(",",Enumerable.Repeat(first.id,6)),data,NativeAccess.Guest)==null,"slot cap");
        var sample=new NativeLabSample();
        sample.Proc(1,"early");sample.Hit(4,10);sample.Proc(4,"arc");sample.Hit(13.9f,20);sample.Proc(13.99f,"arc");sample.Hit(14,100);sample.Proc(14,"late");
        Check(sample.Finished(14)&&sample.Hits==2&&sample.Total==30&&sample.Last==20&&sample.Procs.Count==1&&sample.Procs["arc"]==2,"fixed sample");
        Console.WriteLine("PASS "+checks+" Chip Lab / practice checks");
    }
}
