using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class TechniqueRulesChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){if(!ok)throw new Exception("Technique rules: "+why);count++;}
        var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),options);
        var native=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/native-content.json"))).RootElement.GetProperty("abilities");
        foreach(var def in NativeTechniques.All)
        {
            var rows=content.abilities.Where(a=>a.spec==def.Spec&&a.slot=="1").ToArray();Check(rows.Length==1,"one slot per weapon "+def.Spec);var row=rows[0];Check(row.id==def.Id&&row.live&&row.kind=="technique"&&row.cost==def.Cost&&row.cooldown==def.Cooldown,"content numbers "+def.Id);
            Check(native.EnumerateArray().Count(a=>a.GetProperty("id").GetString()==def.Id&&a.GetProperty("slot").GetString()=="1")==1,"export source row "+def.Id);
            var stats=NativeContentRules.Stats(content,def.Spec,new System.Collections.Generic.Dictionary<string,int>());stats["abilityCooldown"]=.5;stats["abilityCost"]=.8;stats["resourceCost"]=.5;
            Check(Math.Abs(NativeAbilityRules.Cooldown(row,stats)-def.Cooldown*.5f)<.001,"cooldown chips "+def.Id);Check(Math.Abs(NativeAbilityRules.Cost(row,stats)-def.Cost*.4f)<.001,"cost chips "+def.Id);
            var k=new NativeTechnique{Kind=def.Id,Phase="active",At=10,Release=10.2f,Until=14,RecoverUntil=10.5f,Point=new V3(1,2,3)};Check(k.Valid(),"valid "+def.Id);
            var actor=new Actor{Id=3,Technique=k};var snapshot=new Snapshot{Actors=new[]{actor.ViewCopy()}};k.Point=new V3(9,9,9);Check(snapshot.Actors[0].Technique.Point.x==1,"replay/late join copy "+def.Id);
            var wire=JsonSerializer.Deserialize<Actor>(JsonSerializer.Serialize(snapshot.Actors[0],options),options);Check(wire.Technique.Valid()&&wire.Technique.Kind==def.Id&&wire.Technique.Point.x==1,"managed codec "+def.Id);
            var source=new NativeTechniqueSnapshotSource(snapshot);Check(source.Of(3)==snapshot.Actors[0].Technique&&source.Of(4)==null,"snapshot source "+def.Id);
            k.Target=-1;Check(!k.Valid(),"negative target "+def.Id);k.Target=0;k.Point.x=float.NaN;Check(!k.Valid(),"NaN point "+def.Id);k.Point=default;k.Until=100;Check(!k.Valid(),"unbounded lifetime "+def.Id);
        }
        Check(!new NativeTechnique{Kind="unknown",At=0,Release=0,Until=1}.Valid(),"unknown technique rejected");
        Check(!new NativeTechnique{Kind="riposte",Phase="unknown",At=0,Release=0,Until=1}.Valid(),"unknown phase rejected");
        var moving=new NativePredictionState{Time=1,CanMove=true,Position=default,Velocity=new V3(0,0,16),Technique=new NativeTechnique{Kind="grapple-line",At=0,Release=.2f,Arm=.5f,Until=2,RecoverUntil=2}};
        var predicted=NativePrediction.Step(moving,new InputFrame{X=1},(old,next)=>next);Check(Math.Abs(predicted.Position.z-16f/60)<.001&&predicted.Position.x==0,"authority travel seed beats ordinary steering");
        Check(content.abilities.Count(a=>a.slot=="1"&&NativeTechniques.For(a.spec)!=null)==12,"no DEV technique collision");
        var before=new Snapshot{Events=new[]{new ArenaEvent{Id=4,Kind=EventKind.Teleport,Source=1,Target=2,Detail="shade-swap"}}};
        var after=new Snapshot{Events=new[]{new ArenaEvent{Id=5,Kind=EventKind.Teleport,Source=1,Target=2,Detail="shade-swap"}}};
        Check(NativeTechniqueRules.Placement(before,after,1)&&NativeTechniqueRules.Placement(before,after,2),"both swap peers discard pre-swap prediction");Check(!NativeTechniqueRules.Placement(after,after,2)&&!NativeTechniqueRules.Placement(before,after,3),"no repeated or unrelated teleport reset");
        var ended=new NativeTechnique{Kind="smoke-veil",At=0,Release=1,Until=5,RecoverUntil=1.5f};NativeTechniqueRules.End(ended,2);NativeTechniqueRules.End(ended,20);Check(ended.Until==2&&ended.RecoverUntil==1.5f,"repeated death cleanup never extends an effect");
        var cloud=new NativeTechnique{Kind="smoke-veil",Phase="cloud",At=0,Release=1,Until=5,Point=default};
        Check(NativeTechniqueRules.Hidden(new V3(10,1,0),new V3(2,1,0),cloud,2),"outside cannot see inside");Check(!NativeTechniqueRules.Hidden(new V3(1,1,0),new V3(2,1,0),cloud,2),"inside sees inside");Check(!NativeTechniqueRules.Hidden(new V3(10,1,0),new V3(2,1,0),cloud,5),"expired smoke");Check(!NativeTechniqueRules.Hidden(new V3(10,1,0),new V3(2,8,0),cloud,2),"different floor");
        var wall=new NativeTechnique{Kind="bastion",At=0,Release=0,Until=4,Dir=new V3(0,0,1)};
        foreach(float sign in new[]{-1f,1f}){Check(NativeTechniqueRules.Wall(new V3(0,1,sign*2),new V3(0,0,-sign*4),wall,0,out float f)&&Math.Abs(f-.5f)<.001,"wall two sides");Check(!NativeTechniqueRules.Wall(new V3(1.4f,1,sign*2),new V3(0,0,-sign*4),wall,0,out _),"wall width");Check(!NativeTechniqueRules.Wall(new V3(0,1.9f,sign*2),new V3(0,0,-sign*4),wall,0,out _),"wall height");}
        Check(NativeTechniqueRules.SegmentSphere(new V3(0,0,-10),new V3(0,0,20),default,.6f,out float coin)&&Math.Abs(coin-.47f)<.001,"fast coin sweep");Check(!NativeTechniqueRules.SegmentSphere(new V3(1,0,-10),new V3(0,0,20),default,.6f,out _),"coin miss");
        Check(NativeTechniqueRules.Front(new V3(0,0,1),new V3(0,0,-1))&&!NativeTechniqueRules.Front(new V3(0,0,1),new V3(0,0,1)),"front parry only");
        Check(Math.Abs(NativeTechniqueRules.LeapSpeed(23)*NativeTechniqueRules.LeapSpeed(23)/(2*23)-7)<.001,"seven metre ballistic rise");
        var anchor=new NativeTechnique{Kind="anchor-throw",At=0,Release=.3f,Arm=1,Until=4.32f};Check(!NativeTechniqueRules.Recast(anchor,.5f)&&NativeTechniqueRules.Recast(anchor,1.1f)&&!NativeTechniqueRules.Recast(anchor,4.01f),"anchor window after impact");anchor.RecastAt=2;Check(!NativeTechniqueRules.Recast(anchor,2.1f)&&NativeTechniqueRules.Moving(anchor,2.1f),"one leap per throw");
        Check(Math.Abs(NativeTechniqueRules.LineDistance(new V3(2,0,5),default,new V3(0,0,10))-2)<.001,"recall line distance");
        Console.WriteLine("PASS weapon technique rules: "+count+" checks (content, costs, bounds, snapshots, counters, geometry; not PhysX)");
    }
}
