using System;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class HulkBreachChecks
    {
        public static void Run(string root)
        {
            int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Hulk breach: "+why);}
            var json=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
            var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),json);
            var ward=maps.maps.Single(m=>m.id=="siren");
            var compatibility=new ConversionContent{sourceCommit="test",contentRevision="ward"};
            Check(!NativeMapRules.Compatible(6,"test","ward",compatibility)&&NativeMapRules.Compatible(NativeConnection.Protocol,"test","ward",compatibility),"old peers rejected before dynamic frames");
            var candidates=ward.blocks.Where(b=>NativeHulkBreach.Eligible(ward,b)).ToArray();
            Check(candidates.Length>50,"actual Ward interior walls available");
            foreach(var b in ward.blocks.Where(b=>b.id.Contains("shell")||b.id.Contains("floor")||b.id.Contains("ceiling")||b.role!="static"))Check(!NativeHulkBreach.Eligible(ward,b),"protected "+b.id);
            // Door 1.4 m wide: a pilot fits, the 1.908 m Hulk doesn't. Two side walls.
            var wall=new ContentBlock{id="fixture-wall-right",role="static",pos=new V3(2.7f,2,0),size=new V3(4,4,.4f)};
            var from=new V3(0,NativeHordeKinds.HulkHeight*.5f,-1.1f);var target=new V3(0,from.y,3);
            Check(NativeHulkBreach.Eligible(ward,wall),"interior wall eligible");
            Check(!NativeHulkBreach.Blocks(wall,from,target,.42f,1.7f,out _,out _),"pilot fits door");
            Check(NativeHulkBreach.Blocks(wall,from,target,NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight,out _,out var punch),"Hulk clearance blocked");
            var progress=new NativeHulkBreach.Progress();NativeBreachRecord breach=null;
            for(int tick=0;tick<240;tick++)
            {
                float now=tick/60f;bool blocked=breach==null||NativeHulkBreach.Solid(breach,now);
                if(breach==null&&progress.Step(from,now,blocked))breach=NativeHulkBreach.Begin(wall,10001,punch,target-from,now);
            }
            Check(breach!=null&&Math.Abs(breach.breakAt-3.6f)<.02f&&!NativeHulkBreach.Solid(breach,4),"blocked Hulk breaks within four seconds");
            Check(NativeHulkBreach.Solid(breach,breach.breakAt-.001f)&&!NativeHulkBreach.Solid(breach,breach.breakAt),"collider switches at break");
            Check(NativeBreach.Blows.SequenceEqual(new[]{.55f,1.2f,2.1f}),"existing blow timeline");
            Check(breach.restoreAt==breach.breakAt+12&&!NativeHulkBreach.Solid(breach,breach.restoreAt+2.59f)&&NativeHulkBreach.Solid(breach,breach.restoreAt+2.6f),"12 s open and 2.6 s rebuild");
            float solid=NativeBreach.SolidAt(breach.restoreAt);NativeHulkBreach.HoldOpen(breach,solid,true);
            Check(!NativeHulkBreach.Solid(breach,solid)&&breach.restoreAt>solid,"occupant at solidity holds opening");
            NativeHulkBreach.HoldOpen(breach,solid+1,true);float restore=breach.restoreAt;
            NativeHulkBreach.HoldOpen(breach,restore,false);Check(breach.restoreAt==restore&&NativeHulkBreach.Solid(breach,restore+2.6f),"rebuild closes after occupant leaves");
            var wire=JsonSerializer.Deserialize<NativeBreachRecord>(JsonSerializer.Serialize(breach,json),json);
            Check(wire.Valid()&&wire.block==breach.block&&wire.actor==10001&&(wire.point-breach.point).Length==0&&(wire.normal-breach.normal).Length==0&&(wire.holeSize-breach.holeSize).Length==0&&wire.restoreAt==breach.restoreAt,"guest record JSON round trip");
            var map=new ContentMap{id="siren",bounds=ward.bounds,blocks=new[]{wall}};
            Check(NativeHulkBreach.ValidRecords(new[]{wire,new NativeBreachRecord{entry=3,breakAt=2,restoreAt=14}},map),"dynamic and unchanged entry coexist");
            Check(!NativeHulkBreach.ValidRecords(new[]{wire,wire.ViewCopy()},map),"duplicate wall rejected");
            var copy=wire.ViewCopy();copy.normal.x=float.NaN;Check(!copy.Valid()&&wire.Valid(),"invalid normal and independent clone");
            copy=wire.ViewCopy();copy.block="siren-shell-n";Check(!NativeHulkBreach.ValidRecords(new[]{copy},ward),"guest cannot breach shell");
            copy=wire.ViewCopy();copy.holeSize.x+=1;Check(!NativeHulkBreach.ValidRecords(new[]{copy},map),"guest cannot enlarge opening");
            Check(!NativeHulkBreach.Blocks(wall,from+new V3(0,8,0),target+new V3(0,8,0),.954f,3.06f,out _,out _),"different floor excluded");
            Check(!NativeHulkBreach.Blocks(wall,from,target*-1,.954f,3.06f,out _,out _),"wall behind excluded");
            var moving=new NativeHulkBreach.Progress();bool stuck=false;for(int i=0;i<240;i++)stuck|=moving.Step(from+new V3(i*.01f,0,0),i/60f,true);Check(!stuck,"moving Hulk does not knock");
            moving.Step(from,5,false);Check(!moving.Step(from,6,true),"clearance resets progress timer");
            // Warm the exact per-step primitives, then measure without record creation or serialization.
            for(int i=0;i<100;i++){NativeHulkBreach.Eligible(ward,candidates[0]);NativeHulkBreach.Blocks(wall,from,target,.954f,3.06f,out _,out _);moving.Step(from,i/60f,true);}
            long bytes=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++){NativeHulkBreach.Eligible(ward,candidates[0]);NativeHulkBreach.Blocks(wall,from,target,.954f,3.06f,out _,out _);moving.Step(from,i/60f,true);}
            Check(GC.GetAllocatedBytesForCurrentThread()==bytes,"zero warmed clearance/progress allocations");
            Console.WriteLine("PASS Hulk dynamic breach "+count+" checks");
        }
    }
}
