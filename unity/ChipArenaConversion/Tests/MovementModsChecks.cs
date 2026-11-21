using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class MovementModsChecks
{
    public static void Run(string root)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Movement/mods: "+why);}
        ConversionContent Content(){var c=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});NativeMovementContent.Apply(c);return c;}
        void Reject(Action f,string why){bool failed=false;try{f();}catch(ArgumentException){failed=true;}Check(failed,why);}
        var data=Content();data.Validate();var held=new Dictionary<string,int>{{"air-step",3},{"skybound",1},{"acrobat",1},{"stride",5},{"momentum-cell",3}};
        var stats=NativeContentRules.Stats(data,"pistol",held);Check(stats.Get("extraAirJumps")==6,"extra jump stacks");
        Check(Math.Abs(stats["speed"]-Math.Pow(1.08,5)*Math.Pow(1.1,3))<1e-9,"speed multiplies per copy");
        Check(stats["hp"]==data.baseStats.Single(x=>x.name=="hp").value,"new speed chips do not reduce health");
        foreach(var spec in data.weapons.Select(w=>w.id))
        {
            int baseline=NativeJumpSimulation.CanAirJump(spec)?1:0;
            foreach(int bonus in new[]{0,1,2,6,999})
            {
                int budget=NativeJumpSimulation.Budget(spec,bonus),used=0;Check(budget==Math.Min(6,baseline+bonus),"intrinsic jump preserved: "+spec);
                for(int i=0;i<budget;i++)Check(NativeJumpSimulation.Try(spec,true,false,false,false,-8,8.5f,ref used,out var launch,bonus)==NativeJumpKind.Air&&Math.Abs(launch-7.65f)<.0001f,"each extra jump launches");
                Check(NativeJumpSimulation.Try(spec,true,false,false,false,-8,8.5f,ref used,out _,bonus)==NativeJumpKind.None,"budget exhausted");
                Check(NativeJumpSimulation.Try(spec,true,true,false,false,0,8.5f,ref used,out _,bonus)==NativeJumpKind.Ground&&used==0,"landing restores full budget");
                Check(NativeJumpSimulation.Try(spec,true,false,false,true,0,8.5f,ref used,out _,bonus)==NativeJumpKind.None&&used==0,"lock does not consume");
                Check(NativeJumpSimulation.Try(spec,false,false,false,false,0,8.5f,ref used,out _,bonus)==NativeJumpKind.None&&used==0,"no edge does not consume");
            }
        }
        var prediction=new NativePredictionState{JumpSpec="pistol",ExtraAirJumps=3,CanMove=true,JumpSpeed=8.5f,Gravity=23,Position=new V3(0,10,0)};
        for(int i=1;i<=3;i++){prediction=NativePrediction.Step(prediction,new InputFrame{Jump=true},null);Check(prediction.AirJumps==i,"guest predicts each jump");}
        prediction=NativePrediction.Step(prediction,new InputFrame{Jump=true},null);Check(prediction.AirJumps==3,"guest budget enforced");
        Check(NativeContentRules.Eligible(data,"pistol",new Dictionary<string,int>()).Any(c=>c.id=="air-step"),"movement chip offered");
        Check(!NativeContentRules.Eligible(data,"pistol",held).Any(c=>c.id=="air-step"),"max stacks excluded");
        NativeLoadedPack Pack(string id)=>new NativeLoadedPack{Digest=NativeMapRules.Revision(id),Pack=new NativeChipPack{schema=1,id=id,name="Test pack",version="1.0.0",chips=new[]{new ContentChip{id="hops",name="Hops",description="+1 air jump",rarity="rare",max=2,tags=new[]{"all"},effects=new[]{NativeMovementContent.Effect("extraAirJumps","add",1)}}}}};
        var aa=Pack("aa");var bb=Pack("bb");var one=Content();var two=Content();
        Check(NativeMods.Apply(one,3,new[]{aa,bb})==NativeMods.Apply(two,3,new[]{bb,aa})&&one.contentRevision==two.contentRevision,"file order cannot change compatibility");
        Check(one.chips.Any(c=>c.id=="aa:hops")&&one.chips.Any(c=>c.id=="bb:hops"),"namespaces avoid chip collisions");
        Check(one.baseStats.Single(s=>s.name=="extraAirJumps").value==2&&one.baseStats.Single(s=>s.name=="speed").value==1.25,"party rules apply");
        var clean=Content();string before=clean.contentRevision;Check(NativeMods.Apply(clean,0,Array.Empty<NativeLoadedPack>())=="Vanilla"&&clean.contentRevision==before,"empty configuration retains vanilla compatibility");
        var changed=Pack("aa");changed.Digest=NativeMapRules.Revision("different bytes");var other=Content();NativeMods.Apply(other,3,new[]{changed,bb});Check(other.contentRevision!=one.contentRevision,"same version with changed file rejected by handshake");
        Reject(()=>NativeMods.Apply(clean,0,new[]{aa,aa}),"duplicate packs rejected");
        var bad=Pack("bad");bad.Pack.requires=new[]{"missing@1.0.0"};Reject(()=>NativeMods.Apply(clean,0,new[]{bad}),"dependency checked");Check(clean.contentRevision==before&&!clean.chips.Any(c=>c.id=="bad:hops"),"failure is atomic");
        bad=Pack("bad");bad.Pack.chips[0].effects[0].stat="execute-code";Reject(()=>NativeMods.Apply(clean,0,new[]{bad}),"unsupported effect rejected");
        bad=Pack("bad");bad.Pack.chips[0].effects[0].value=double.NaN;Reject(()=>NativeMods.Apply(clean,0,new[]{bad}),"nonfinite rejected");
        bad=Pack("bad");bad.Pack.chips[0].effects[0].value=1.5;Reject(()=>NativeMods.Apply(clean,0,new[]{bad}),"fractional jumps rejected");
        bad=Pack("bad");bad.Pack.schema=2;Reject(()=>NativeMods.Apply(clean,0,new[]{bad}),"schema mismatch");
        bad=Pack("bad");bad.Pack.chips[0].rarity="siren";Reject(()=>NativeMods.Apply(clean,0,new[]{bad}),"exclusive rarity blocked");
        bad=Pack("bad");bad.Pack.chips[0].mechanic="custom-script";Reject(()=>NativeMods.Apply(clean,0,new[]{bad}),"scripts blocked");
        var pinball=Content();NativeMods.Apply(pinball,NativeMods.Pinball,Array.Empty<NativeLoadedPack>());Check(pinball.baseStats.Single(s=>s.name=="impulse").value==2,"pinball modifier");
        var extreme=Content();var stress=Pack("stress");stress.Pack.chips=Enumerable.Range(0,64).Select(i=>new ContentChip{id="boost-"+i,name="Boost",description="Stress",rarity="common",max=10,tags=new[]{"all"},effects=Enumerable.Range(0,6).Select(_=>NativeMovementContent.Effect("damage","mul",3)).ToArray()}).ToArray();NativeMods.Apply(extreme,0,new[]{stress});var extremeHeld=extreme.chips.Where(c=>c.id.StartsWith("stress:")).ToDictionary(c=>c.id,c=>10);Check(NativeContentRules.Stats(extreme,"pistol",extremeHeld)["damage"]==1000000,"legal pack combinations stay finite");
        JumpSimulationChecks.Run(Content());
        Console.WriteLine("PASS movement chips/mod packs: "+checks+" checks plus existing jump simulation suite");
    }
}
