using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class ThunderheartChecks
{
    public static void Run(string root)
    {
        void Need(bool value,string reason){if(!value)throw new Exception("Thunderheart: "+reason);}
        var map=NativeThunderheart.Build();NativeMapRules.Validate(map);
        var catalog=new ConversionContent{maps=new[]{map}};
        Need(NativeMapRules.Selection(catalog,map.id)==map.id,"room selection");
        foreach(int count in new[]{1,2,4,6,8})
        foreach(bool teams in new[]{false,true})
        {
            var seats=Enumerable.Range(0,count).Select(i=>new NativeSeat{id=i+1,team=i%2}).ToArray();
            var positions=seats.Select((p,i)=>NativeMapRules.Spawn(catalog,map,seats,i,teams).pos).ToArray();
            Need(positions.Distinct().Count()==count,"unique spawn per seat");
            Need(NativeMapRules.RandomEligible(map,seats,teams),"rotation and Deathmatch room fit");
        }
        var path=Path.Combine(root,"Assets/ChipArena/Resources");
        var score=new NativeMusicEnvelope(File.ReadAllBytes(Path.Combine(path,"ThunderheartScore.bytes")));
        Need(Math.Abs(score.Duration-NativeThunderheart.Length)<.08,"soundtrack length");
        float intro=0,peak=0;
        for(int i=0;i<40;i++)
        {
            intro+=score.At(i*.1).Body;peak+=score.At(172+i*.1).Body;
            Need(Math.Abs(score.At(i*.1).Bass-score.At(i*.1+score.Duration).Bass)<.001,"seam/seek");
        }
        Need(peak>intro+10,"recording's quiet intro and loud final section are distinct");
        var gate=map.hazards[0];var well=map.hazards[2];
        Need(NativeWardZeroHazards.At(gate,NativePhase.Fight,0,10,44).Phase==NativeHazardPhase.Warn,"3 s gate warning");
        Need(NativeWardZeroHazards.Closure(NativeWardZeroHazards.At(gate,NativePhase.Fight,0,10,48))==1,"closed gate");
        Need(NativeWardZeroHazards.Closure(NativeWardZeroHazards.At(gate,NativePhase.Intro,0,10,48))==0,"intro reset");
        Need(!NativeThunderheart.Fed(2,false,true)&&NativeThunderheart.Fed(3,false,true)&&NativeThunderheart.Fed(2,true,false),"opposite stop routes air");
        var state=NativeFactoryHazards.State(well,0,7);
        Need(NativeStageHazards.VentLaunch(well,state,well.a+new V3(0,.9f,0),0),"live well can launch an occupant");
        Need(!NativeStageHazards.VentLaunch(well,state,well.a+new V3(5,.9f,0),0),"well outside safe");
        using(var reader=new BinaryReader(File.OpenRead(Path.Combine(path,"ThunderheartArt.bytes"))))
        {
            Need(reader.ReadUInt32()==0x31524143,"geometry header");int pieces=reader.ReadUInt16();long triangles=0;
            for(int n=0;n<pieces;n++)
            {
                reader.ReadBytes(reader.ReadByte());reader.ReadBytes(12);int vertices=reader.ReadInt32(),faces=reader.ReadInt32();
                Need(vertices>0&&faces>0,"nonempty draw");reader.BaseStream.Position+=16L*vertices;
                for(int t=0;t<3*faces;t++)Need(reader.ReadUInt32()<vertices,"mesh index in range");
                triangles+=faces;
            }
            Need(reader.BaseStream.Position==reader.BaseStream.Length,"mesh ends exactly");
            Console.WriteLine("PASS Thunderheart art: "+pieces+" batches, "+triangles+" triangles.");
        }
        // Compare the same pure hazard queries per fight tick, excluding loading and JIT.
        var options=new JsonSerializerOptions{IncludeFields=true};
        var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),options);
        foreach(var m in new[]{map,maps.maps.Single(v=>v.id=="sanctum")})
        {
            double Run(int n)
            {
                double sum=0;
                for(int i=0;i<n;i++)foreach(var h in m.hazards)
                {
                    double t=i/60d;
                    if(NativeStageHazards.Ward(h))sum+=NativeWardZeroHazards.At(h,NativePhase.Fight,0,0,t).Level;
                    else if(h.kind=="grid")sum+=NativeCyberCityHazards.Grid(h,0,0,t).Level;
                    else sum+=NativeFactoryHazards.State(h,0,t-4).Phase;
                }
                return sum;
            }
            Run(1000);var watch=Stopwatch.StartNew();var sum=Run(60000);watch.Stop();Need(sum>0,"hazards active");
            Console.WriteLine(m.name+": "+m.blocks.Length+" authored collision blocks, "+m.hazards.Length+" hazards, "+(watch.Elapsed.TotalMilliseconds/60).ToString("F3")+" us/tick (managed hazard sampling only).");
        }
        Console.WriteLine("PASS Thunderheart roster, clock, score, binary meshes, launch and room selection.");
    }
}
