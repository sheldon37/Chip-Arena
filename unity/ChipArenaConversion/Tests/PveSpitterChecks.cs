using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class PveSpitterChecks
    {
        public static void Run(string root)
        {
            int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("PvE spitter/waves: "+why);}
            var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var data=JsonSerializer.Deserialize<NativePveContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")),options);
            var revised=NativeMapRules.PveRevision("ward","spitter");Check(revised!=NativeMapRules.PveRevision("ward","legacy")&&!NativeMapRules.Compatible(NativeConnection.Protocol,"test","ward",new ConversionContent{sourceCommit="test",contentRevision=revised}),"older peers cannot join spitter content; relay protocol unchanged");
            var spit=data.adds.Single(x=>x.id=="spitter");Check(spit.attack=="spit"&&spit.hit==NativeSpitterRules.Hit&&spit.cycle==NativeSpitterRules.Cycle&&spit.reach==11&&spit.hp==45,"content uses actual spit timings");
            Check(data.waves.Skip(1).All(x=>x.mix.Any(m=>m.name=="spitter"&&m.value>0))&&data.bossMix.All(x=>x.mix.Any(m=>m.name=="spitter"&&m.value>0)),"spitters enabled after first wave and throughout boss");
            foreach(bool skip in new[]{false,true})foreach(double resolved in new[]{-1d,1,9})
            {
                var schedule=new NativeWaveSchedule(0,skip);PveWaveEvent first=null;
                for(double t=0;t<=10;t+=.25){var e=schedule.Step(t,0,resolved<0?(double?)null:resolved);if(e!=null){first=e;break;}}
                Check(first!=null&&first.kind==(skip?"boss":"wave-start"),"opening / skip preserved");if(skip)continue;
                for(int wave=1;wave<=3;wave++)
                {
                    Check(schedule.Step(schedule.At+3,2,null)==null,"two remaining cannot clear");Check(schedule.Step(schedule.At+151,1,null)==null,"one remaining survives old timeout");Check(schedule.Step(schedule.At+10000,1,null)==null,"no timed forced clear");
                    var clear=schedule.Step(schedule.At+10001,0,null);Check(clear?.kind=="wave-clear"&&!clear.forced,"last enemy clears immediately");
                    Check(schedule.Step(schedule.Until-.01,0,null)==null,"intermission retained");var next=schedule.Step(schedule.Until,0,null);Check(next.kind==(wave<3?"wave-start":"boss"),"three waves then boss");
                }
            }
            for(int members=1;members<=8;members++)foreach(var wave in data.waves)
            {
                int budget=NativePveRules.Budget(wave,members),spawned=0,alive=0;
                while(spawned<budget){int n=NativePveRules.SpawnAllowance(budget,spawned,alive,40,8);spawned+=n;alive+=n;if(alive>=40)alive-=7;Check(spawned<=budget,"batch never exceeds fixed total");}
                Check(NativePveRules.SpawnAllowance(budget,spawned,1,40,8)==0&&NativePveRules.Remaining(budget,spawned,1)==1,"last zombie does not trigger refill");Check(NativePveRules.Remaining(budget,spawned,0)==0,"spent budget and no living enemies clears");
            }
            Check(NativePveRules.Remaining(20,19,0)==1,"no living zombies but one unspawned cannot clear");Check(NativePveRules.SpawnAllowance(-1,900,2,40,8)==8,"boss reinforcement remains unlimited");
            Check(NativeSpitterRules.Interrupt(1,10.79f,10,false)&&!NativeSpitterRules.Interrupt(0,10.4f,10,false)&&!NativeSpitterRules.Interrupt(1,10.8f,10,false)&&!NativeSpitterRules.Interrupt(1,10.4f,10,true),"only damaging pre-release hit interrupts");
            Check(Math.Abs(NativeSpitterRules.Retry(10)-11.56f)<.001f,"partial retry");
            Check(NativeHordeKinds.Strike(spit,true,2|4)==.8f&&NativeHordeKinds.AttackOf(NativeHordeKinds.Spitter,"spit",true)==NativeHordeKinds.Attack.Spit,"damaged spitter keeps mouth wind-up");
            var lead=NativeSpitterRules.Lead(new V3(),new V3(8,0,0),new V3(20,0,0));Check(Math.Abs(lead.Length-11)<.001f,"lead clamped to range");
            Check(NativeSpitterRules.Touch(new V3(),new V3(0,.85f,0),.85f,.42f,1.3f)&&!NativeSpitterRules.Touch(new V3(),new V3(0,6.85f,0),.85f,.42f,1.3f),"puddles cannot poison through floors");
            Check(NativeSpitterRules.KeepDistance(3)==-1&&NativeSpitterRules.KeepDistance(6)==0&&NativeSpitterRules.KeepDistance(10)==1,"retreat / hold / advance");
            var acid=new NativeAcidRecord{id=1,actor=10001,from=new V3(0,1.2f,0),to=new V3(0,0,8),at=10,flight=1,damageScale=1};
            var wire=JsonSerializer.Deserialize<NativeAcidRecord>(JsonSerializer.Serialize(acid,options),options);Check(wire.Valid()&&wire.to.z==8,"acid JSON round trip");Check(!NativeAcidRecord.ValidAll(new[]{acid,acid.Copy()}),"duplicate record rejected");var bad=acid.Copy();bad.flight=float.NaN;Check(!bad.Valid()&&acid.Valid(),"invalid record and detached copy");
            Check(NativeHulkBreach.Shorter(20,8,0)&&!NativeHulkBreach.Shorter(8,8,0)&&!NativeHulkBreach.Shorter(20,8,6)&&NativeHulkBreach.Shorter(float.PositiveInfinity,8,0),"shorter same-floor path only, including no route");
            var wall=new ContentBlock{pos=new V3(0,2,0),size=new V3(.4f,4,6)};var exit=NativeHulkBreach.Exit(wall,new V3(-3,1.53f,0),new V3(3,1.53f,0),.954f);Check(exit.x>1.154f&&exit.y==1.53f,"exit clears full Hulk capsule");
            // Broadphase must find every true neighbor, including negative cells and cell boundaries.
            var grid=new NativeCrowdGrid();var positions=new V3[192];var random=new Random(41);
            for(int i=0;i<positions.Length;i++){positions[i]=new V3((float)random.NextDouble()*60-30,0,(float)random.NextDouble()*50-25);grid.Add(i,positions[i].x,positions[i].z);}
            int tested=0;var found=new HashSet<int>();
            for(int i=0;i<positions.Length;i++){found.Clear();int x=NativeCrowdGrid.Cell(positions[i].x),z=NativeCrowdGrid.Cell(positions[i].z);for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)for(int j=grid.Head(x+dx,z+dz);j>=0;j=grid.Next(j)){found.Add(j);tested++;}for(int j=0;j<positions.Length;j++)if((positions[j]-positions[i]).Length<.9f)Check(found.Contains(j),"broadphase includes close neighbor");}
            Check(tested<positions.Length*positions.Length/10,"spread horde avoids over 90 percent of pair candidates");
            for(int k=0;k<10;k++){grid.Clear();for(int i=0;i<positions.Length;i++)grid.Add(i,positions[i].x,positions[i].z);}
            long bytes=GC.GetAllocatedBytesForCurrentThread();for(int k=0;k<100;k++){grid.Clear();for(int i=0;i<positions.Length;i++)grid.Add(i,positions[i].x,positions[i].z);for(int i=0;i<positions.Length;i++)grid.Head(NativeCrowdGrid.Cell(positions[i].x),NativeCrowdGrid.Cell(positions[i].z));}
            Check(GC.GetAllocatedBytesForCurrentThread()==bytes,"warmed grid allocates zero bytes");
            Console.WriteLine("PASS PvE spitter/waves "+checks+" checks; 192-add separation candidates "+tested+" vs "+positions.Length*positions.Length+" all-pairs; zero warmed grid allocations");
        }
    }
}
