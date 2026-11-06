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
        Console.WriteLine("PASS NOCTIEL rules: "+n+" checks");
    }
}
