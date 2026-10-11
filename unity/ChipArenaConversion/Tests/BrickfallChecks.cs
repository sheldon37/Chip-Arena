using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class BrickfallChecks
{
    public static void Run(string root,ConversionContent legacy)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Brickfall: "+why);}
        var opts=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var data=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),opts);
        NativeMapRules.Merge(data,JsonSerializer.Deserialize<UnityMapContent>(text,opts),text);var map=data.Map("brickfall");
        Check(data.maps.Length==16&&data.maps.Count(m=>m.rotation)==legacy.maps.Count(m=>m.rotation)&&!map.rotation,"old maps or rotation changed");
        Check(data.contentRevision.Length==64&&data.contentRevision!=NativeMapRules.Revision(text+" "),"revision not byte sensitive");
        Check(!NativeMapRules.Compatible(5,data.sourceCommit,data.contentRevision,data)&&!NativeMapRules.Compatible(6,data.sourceCommit,null,data)&&!NativeMapRules.Compatible(6,data.sourceCommit,NativeMapRules.Revision(text+" "),data)&&NativeMapRules.Compatible(6,data.sourceCommit,data.contentRevision,data),"old/mismatched join compatibility");
        Check((NativeMapRules.Clamp(map,new V3(100,4,-100))-new V3(29.58f,4,-25.58f)).Length<.001,"map bounds");
        Check(NativeMapRules.Clamp(legacy.maps[0],new V3(100,0,100)).x==31&&NativeMapRules.Clamp(legacy.maps[0],new V3(100,0,100),true).x==28,"legacy clamps");
        var seats=Enumerable.Range(0,8).Select(i=>new NativeSeat{id=i+1,team=i%2==0?2:7}).ToList();
        for(int i=0;i<8;i++)Check((NativeMapRules.Spawn(data,map,seats,i,true).pos-map.spawns.teams[i%2].points[i/2].pos).Length<.001,"two-team seat assignment");
        seats[7].team=2;for(int i=0;i<8;i++)Check((NativeMapRules.Spawn(data,map,seats,i,true).pos-map.spawns.ffa[i].pos).Length<.001,"5v3 fallback");seats[7].team=5;Check((NativeMapRules.Spawn(data,map,seats,7,true).pos-map.spawns.ffa[7].pos).Length<.001,"three-team fallback");
        var state=new NativeStructureState(map);Check(state.Step(3.99f,0,id=>false).Count==0,"4 second arm lock");
        var triggers=state.Step(4,0,id=>false);Check(triggers.Count==10&&triggers.All(t=>t.state==1&&t.t0>=4.8),"cascade/telegraph");
        string schedule=JsonSerializer.Serialize(state.Snapshot(),opts);var reverse=JsonSerializer.Deserialize<UnityMapContent>(text,opts).maps[0];Array.Reverse(reverse.segments);var reversed=new NativeStructureState(reverse);reversed.Step(4,0,id=>false);Check(schedule==JsonSerializer.Serialize(reversed.Snapshot(),opts),"cascade depends on author order");
        var chain=new NativeStructureState(map);chain.Step(4,0,id=>id!="hotel-pier-nw");Check(chain.Get("hotel-nw").state==1&&chain.Get("bridge-ashby").state==1&&chain.Get("hotel-ne").state==0,"transitive hotel/bridge cascade");
        for(int i=240;i<=2400;i++){state.Step(i/60f,0,id=>false);Check(state.Snapshot().Count(s=>s.state==2)<=3,"concurrent falling budget");}
        Check(state.Snapshot().All(s=>s.state==3),"queue failed to drain");
        var restore=new NativeStructureState(map);restore.Restore(state.Snapshot(),40);restore.Restore(new NativeStructureState(map).Snapshot(),0);Check(restore.Snapshot().All(s=>s.state==0),"rewind restore");
        Check(!restore.Valid(new[]{new StructureFrame{id="unknown",state=2,t0=0}},10),"unknown segment accepted");
        var bad=restore.Snapshot();bad[0].t0=float.NaN;Check(!restore.Valid(bad,10),"nonfinite segment accepted");
        bad=restore.Snapshot();foreach(var s in bad.Take(4)){s.state=2;s.t0=9;}Check(!restore.Valid(bad,10),"over-budget falling frame accepted");
        var hits=new[]{new StructureDamage{member="failed",pilot=1,amount=50,at=4},new StructureDamage{member="failed",pilot=2,amount=50,at=5},new StructureDamage{member="intact",pilot=3,amount=140,at=6}};var failed=new HashSet<string>{"failed"};
        Check(NativeStructureRules.Credit(hits,failed,10)==2,"credit tie/triggering members");Check(NativeStructureRules.Credit(hits,failed,15)==2&&NativeStructureRules.Credit(hits,failed,15.01f)==0,"10 second structural credit expiry");
        Check(NativeStructureRules.FallbackCredit(2,3,9,10)==2&&NativeStructureRules.FallbackCredit(0,3,4,10)==3&&NativeStructureRules.FallbackCredit(0,3,3.99f,10)==0,"6 second credit fallback");
        var box=new MapVolume{center=new V3(0,1,0),size=new V3(2,2,2)};Check(NativeStructureRules.Crosses(new V3(-4,1,0),new V3(4,1,0),box)&&!NativeStructureRules.Crosses(new V3(-4,1,3),new V3(4,1,3),box),"hazard route intersection");
        int samples=0;float maxAcceleration=0;
        foreach(var s in map.segments)foreach(var p in s.poses)
        {
            var b=map.blocks.First(x=>x.id==p.block);for(int i=0;i<240;i++)
            {
                float t=2-s.telegraph+(s.telegraph+NativeStructureRules.Duration(s))*i/239f;var host=NativeStructureRules.Pose(s,b,2,t);var guest=NativeStructureRules.Pose(s,b,2,t);var killcam=NativeStructureRules.Pose(s,b,2,t);Check((host.position-guest.position).Length==0&&(guest.position-killcam.position).Length==0,"pose parity");samples++;
                for(int corner=0;corner<8;corner++){var offset=new V3(b.size.x/2*((corner&1)==0?-1:1),b.size.y/2*((corner&2)==0?-1:1),b.size.z/2*((corner&4)==0?-1:1));var q=host.rotation;var axis=new V3(q.x,q.y,q.z);var cross=new V3(axis.y*offset.z-axis.z*offset.y,axis.z*offset.x-axis.x*offset.z,axis.x*offset.y-axis.y*offset.x);var twice=new V3(axis.y*cross.z-axis.z*cross.y,axis.z*cross.x-axis.x*cross.z,axis.x*cross.y-axis.y*cross.x);var at=host.position+offset+cross*(2*q.w)+twice*2;Check(s.sweeps.Any(v=>NativeStructureRules.Contains(at,v.center,v.size,.005f)),"authored pose outside fall sweep "+b.id);}
            }
            if(s.mode=="pancake"&&p.angle==0)
            {
                float duration=NativeStructureRules.DropDuration(p.drop,s.maxAccel);Check(duration+p.delay<=NativeStructureRules.Duration(s)+.001,"landing before last slab");
                // Analytic bound, plus measured central differences away from endpoint transitions.
                Check(6*p.drop/(duration*duration)<=6.901f,"pancake acceleration bound");
                for(float t=.1f;t<duration-.1f;t+=.05f){float at=2+p.delay+t;float a=NativeStructureRules.Pose(s,b,2,at-.05f).position.y,c=NativeStructureRules.Pose(s,b,2,at).position.y,z=NativeStructureRules.Pose(s,b,2,at+.05f).position.y;maxAcceleration=Math.Max(maxAcceleration,Math.Abs((z-2*c+a)/.0025f));}
            }
        }
        Check(maxAcceleration<=6.91f,"sampled slab acceleration");
        foreach(var hp in new[]{15f,45f,100f,300f}){float spent=0;for(int i=0;i<100;i++)spent+=Math.Min(45*1.59f,NativeStructureRules.Allowance(hp,spent));Check(spent<=Math.Min(60,hp*.6f)+.001&&spent<hp,"post-multiplier shared damage cap");}
        Check(NativeStructureRules.DamageScale("structural","bullet",36)==0&&NativeStructureRules.DamageScale("structural","melee",36)==0&&NativeStructureRules.DamageScale("structural","blast",36)==1.5f,"structural demolition filter");
        foreach(var held in new[]{new Dictionary<string,int>(),new Dictionary<string,int>{{"spring",1}},new Dictionary<string,int>{{"rubber",1}},new Dictionary<string,int>{{"rubber",1},{"spring",2}},new Dictionary<string,int>{{"moonboots",1},{"rubber",1}},new Dictionary<string,int>{{"moonboots",1},{"rubber",1},{"spring",3}}})
        {
            // Use real chip IDs/catalog stats; print actual computed apex instead of hard-coded draft numbers.
            Check(held.Keys.All(id=>data.chips.Any(c=>c.id==id)),"jump chip missing");var stats=NativeContentRules.Stats(data,"pistol",held);double apex=Math.Pow(8.5*stats["jumpPower"],2)/(46*stats["playerGravity"]);
            foreach(var top in map.pilotBlocks){var at=new V3(top.center.x,top.bottom+.9f+(float)apex,top.center.z);Check((NativeMapRules.ExcludeTops(map,at)-at).Length>.01,"standable blocked top");}
            Console.WriteLine("PASS Brickfall jump "+string.Join("+",held.Select(k=>k.Key+"x"+k.Value))+": apex "+apex.ToString("F3")+" m; all protected tops excluded");
        }
        Console.WriteLine("PASS Brickfall: 10-segment cascade, queue, reversal, compatibility, team spawns, damage filters/caps; "+samples+" pure pose samples; measured acceleration "+maxAcceleration.ToString("F3")+" m/s²");
    }
}
