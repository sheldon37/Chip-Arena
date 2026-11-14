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
        Check(NativeTechniqueLoadout.Normalize(null)==""&&NativeTechniqueLoadout.Slot(null,"sword")=="","old saves preserve defaults");
        Check(NativeTechniqueLoadout.Normalize(" sword : q ,rifle:q,rifle:e,fire:e,siren:q,sword:e")=="rifle:e=grapple-line,fire:e=ignition-leap,sword:e=riposte","host normalization and duplicate picks");
        Check(!NativeTechniqueLoadout.Valid("rifle:q")&&!NativeTechniqueLoadout.Valid("siren:e")&&!NativeTechniqueLoadout.Valid(new string('x',NativeTechniqueLoadout.MaxLength+1)),"invalid wire choices rejected");
        Check(NativeTechniqueLoadout.With("sword:q,rifle:e","sword","e")=="rifle:e=grapple-line,sword:e=riposte","move replacement between slots");
        Check(NativeTechniqueLoadout.With("sword:q,rifle:e","sword","")=="rifle:e=grapple-line","restore both defaults");
        string both=NativeTechniqueLoadout.With(NativeTechniqueLoadout.With("","sword","q","riposte"),"sword","e","parting-line");
        Check(both=="sword:q=riposte,sword:e=parting-line","two independent slots canonical");
        Check(NativeTechniqueLoadout.In(both,"sword","q")=="riposte"&&NativeTechniques.Equipped(both,"sword","e").Id=="parting-line","presentation reads per-slot id");
        Check(NativeTechniqueLoadout.With(both,"sword","q","parting-line")=="sword:q=parting-line","moving an id clears its old slot");
        Check(NativeTechniqueLoadout.With(both,"sword","q","")=="sword:e=parting-line","default clears just selected slot");
        Check(NativeTechniqueLoadout.Normalize("sword:q=sonar-dart,rifle:q=sonar-dart,sword:e=unknown")=="","foreign/unknown ids rejected");
        string all="";foreach(var spec in NativeTechniqueLoadout.Weapons)foreach(var slot in new[]{"q","e"})all=NativeTechniqueLoadout.With(all,spec,slot,NativeTechniques.Of(spec)[slot=="q"?0:1].Id);
        Check(all.Length>256&&NativeTechniqueLoadout.Valid(all),"full multi-slot map fits wire budget");
        var trail=new NativeTechnique{Kind="wildfire-run",At=0,Release=0,Until=6,Path=new[]{new V3(1,2,3)}};
        var copy=trail.ViewCopy();trail.Path[0].x=99;Check(copy.Path[0].x==1,"trail copies isolate snapshots/replay");
        trail.Path=new V3[33];Check(!trail.Valid(),"trail count bounded");trail.Path=new[]{new V3(float.NaN,0,0)};Check(!trail.Valid(),"trail coordinates finite");
        foreach(var spec in NativeTechniqueLoadout.Weapons)
        {
            foreach(string slot in new[]{"q","e"})
            {
                bool allowed=spec!="rifle"||slot=="e";string map=NativeTechniqueLoadout.With("",spec,slot);
                Check(NativeTechniqueLoadout.Slot(map,spec)==(allowed?slot:""),"legal choice "+spec+slot);
                Check(NativeTechniqueLoadout.Ability(content,map,spec,slot)?.id==(allowed?NativeTechniques.For(spec).Id:content.Ability(spec,slot)?.id),"resolved slot "+spec+slot);
                string other=slot=="q"?"e":"q";Check(ReferenceEquals(NativeTechniqueLoadout.Ability(content,map,spec,other),content.Ability(spec,other)),"other ability preserved "+spec+slot);
                var seat=new NativeSeat{spec=spec,weaponTechniques=map};var saved=JsonSerializer.Deserialize<NativeSeat>(JsonSerializer.Serialize(seat,options),options);
                Check(saved.weaponTechniques==map,"seat save roundtrip "+spec+slot);
            }
        }
        foreach(var def in NativeTechniques.All)
        {
            var rows=content.abilities.Where(a=>a.spec==def.Spec&&a.slot=="technique"&&a.id==def.Id).ToArray();Check(rows.Length==1,"one row per id "+def.Spec);var row=rows[0];Check(row.id==def.Id&&row.live&&row.kind=="technique"&&row.cost==def.Cost&&row.cooldown==def.Cooldown,"content numbers "+def.Id);
            Check(native.EnumerateArray().Count(a=>a.GetProperty("id").GetString()==def.Id&&a.GetProperty("slot").GetString()=="technique")==1,"export source row "+def.Id);
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
        var ghost=new NativeTechnique{Kind="grave-walk",At=0,Release=.2f,Until=1.7f,RecoverUntil=2.04f};
        Check(NativeTechniqueRules.Recast(ghost,.3f)&&!NativeTechniqueRules.Recast(ghost,1.7f)&&NativeTechniqueRules.Rooted(ghost,1.8f),"grave recast window and rise lock");
        var walker=new NativePredictionState{Time=.3f,CanMove=true,Technique=ghost,Grounded=true};
        var walking=NativePrediction.Step(walker,new InputFrame{X=1},(old,next)=>next);Check(Math.Abs(walking.Position.x-7f/60)<.001,"grave guest steering uses pending movement");
        walker.Time=1.8f;walker.Velocity=new V3(7,0,0);var rising=NativePrediction.Step(walker,new InputFrame{X=1},(old,next)=>next);Check(rising.Position.x==0,"guest rise cannot move");
        Check(content.abilities.Count(a=>a.slot=="technique"&&NativeTechniques.For(a.spec)!=null)==NativeTechniques.All.Length,"no DEV technique collision");
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
