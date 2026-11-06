using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class FallenRulesChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception("NOCTIEL rules: "+why);}
        var approach=new NativeFallenApproach();const float dt=1f/60;
        Check(Math.Abs(approach.Step(1,30,dt)-1.4f*dt)<1e-6f,"walk at 30 m");
        Check(Math.Abs(approach.Step(1,31,dt)-9f*dt)<1e-6f,"far glide");
        Check(approach.Step(1,5,dt)==0&&approach.Step(1,4,dt)==0,"stop inside 5 m");
        Check(Math.Abs(approach.Step(1,5.01f,dt)-.01f)<1e-5f,"no stop overshoot");
        approach.Reset();float step=0;
        for(int i=0;i<182;i++){step=approach.Step(1,20,dt);if(i%30==0)approach.Pause();}
        Check(Math.Abs(step-9*dt)<1e-6f,"stalled chase glides across attack pauses");
        for(int i=0;i<61;i++)step=approach.Step(1,20-i*.1f,dt);
        Check(Math.Abs(step-1.4f*dt)<1e-6f,"catch-up returns to walk");
        approach.Reset();for(int i=0;i<600;i++)Check(Math.Abs(approach.Step(1,29-i*1.4f*dt,dt)-1.4f*dt)<1e-6f,"closing walk never stalls");
        approach.Reset();for(int i=0;i<182;i++)approach.Step(1,20,dt);
        Check(Math.Abs(approach.Step(2,20,dt)-1.4f*dt)<1e-6f,"target change resets catch-up");
        var f=new NativeFallenRecord{seed=47,hush=new[]{new NativeFallenSeat{id=1}}};Check(f.Valid(),"initial valid");
        for(int phase=2;phase<=4;phase++){f.chipOffer=NativeFallenRules.Chips(47,phase);Check(f.chipOffer.Length==3&&f.chipOffer.Distinct().Count()==3&&f.chipOffer.SequenceEqual(NativeFallenRules.Chips(47,phase)),"seeded unique modifiers");}
        f.chipOffer=new[]{"haste"};foreach(var m in NativeFallenBoss.Moves.Where(m=>m.Kills))Check(NativeFallenRules.Wind(m,f)>=2.4f,"OHK wind floor "+m.Id);
        for(int seed=0;seed<100;seed++)for(int x=-79;x<=79;x+=3)for(int z=-79;z<=79;z+=3)
        {
            var q=new Vector2(x,z);if(q.Length()>80)continue;
            var pool=NativeFallenRules.Pool(q,seed);Check(pool.Length()<=72.01f&&Vector2.Distance(q,pool)<=14,"reachable Eclipse pool");
            // From every occupied column a 4 m/s walker can reach one of its gutters well before a 2.4 s tell.
            if(NativeFallenRules.InColumn(q,seed))Check(new[]{new Vector2((float)Math.Round(q.X/8)*8,q.Y),new Vector2(q.X,(float)Math.Round(q.Y/8)*8)}.Any(goal=>Vector2.Distance(q,goal)<=4&&!NativeFallenRules.InColumn(goal,seed)),"walkable Fall escape");
        }
        for(int y=0;y<360;y+=5){var d=NativeFallenRules.Dir(y);var outOfLine=new Vector2(d.Y,-d.X)*2;Check(NativeFallenRules.Line(outOfLine,Vector2.Zero,y)>1.5f,"walkable Verdict");}
        for(float t=0;t<100;t+=.1f){float next=NativeFallenRules.NextBar(t,0);Check(next>t&&next-t<=NativeFallenSong.Bar+.001f,"next song bar");}
        var copy=f.Copy();copy.hush[0].lives=0;Check(f.hush[0].lives==5,"deep copy lives");copy=f.Copy();copy.pools=new[]{new V3(float.NaN,0,0)};Check(!copy.Valid(),"reject malformed geometry");
        var options=new JsonSerializerOptions{IncludeFields=true};var wire=JsonSerializer.Deserialize<NativeFallenRecord>(JsonSerializer.Serialize(f,options),options);Check(wire.Valid()&&wire.hush[0].lives==5,"record roundtrip");
        Check(new InputFrame{InteractHeld=true,Interact=true}.HeldOnly().InteractHeld&&!new InputFrame{Interact=true}.HeldOnly().Interact,"hold survives tap consumption");
        var map=NativeFallenRules.Arena();Check(map.blocks.All(b=>b.pos.y+b.size.y/2<=0)&&map.spawns.ffa.Length==8,"flat arena and party landings");
        // Off-centre targets must still warn the opposite edge: authority lines span the entire disc.
        foreach(string move in new[]{"verdict","last","rain"})
        {
            var fx=new NativeFallenBossFx();var frame=new FallenBossFrame{Live=new NativeFallenRecord(),Phase=2,P=2.3f,Actions=new[]{new NativeFallenBossPreview.Act{Id=move,At=0,Hit=2.4f,End=3,From=new Vector3(0,0,79),To=new Vector3(0,0,79),Aim=0,Seed=0}}};
            fx.Build(frame,new Vector3(0,20,0),Vector3.UnitX,Vector3.UnitY,null,false);
            float minZ=float.MaxValue,maxZ=float.MinValue;for(int i=0;i<fx.Quads.Count*12;i+=3){minZ=Math.Min(minZ,fx.Quads.Corners[i+2]);maxZ=Math.Max(maxZ,fx.Quads.Corners[i+2]);}
            // The warnings themselves (Core NativeTelegraph, Claude 2026-11-06): their quads count as what the disc shows.
            for(int i=0;i<fx.Telegraphs.Count;i++){NativeTelegraph.Corners(fx.Telegraphs.Items[i],out var ca,out var cb,out var cc,out var cd,out _,out _,out _,out _);foreach(var c in new[]{ca,cb,cc,cd}){minZ=Math.Min(minZ,c.Z);maxZ=Math.Max(maxZ,c.Z);}}
            Check(minZ<=-80&&maxZ>=80,"full-disc live warning "+move);
        }
        Console.WriteLine("PASS NOCTIEL rules: "+n+" checks");
    }
}
