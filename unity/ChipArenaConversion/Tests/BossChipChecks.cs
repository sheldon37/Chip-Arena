using System;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

// The boss relics (Core NativeBossChips; docs/BOSS-CHIPS.md): two per boss, one copy each, in the boss's own tier with its own look; live, never an ordinary offer, only their boss's rare drop.
public static class BossChipChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("boss-chips: "+what);count++;}
    public static void Run(string root)
    {
        count=0;var relics=NativeBossChips.Relics;
        Check(relics.Length==12&&NativeBossChips.Sets.Length==6,"twelve relics, six bosses");
        Check(relics.Select(r=>r.Id).Distinct().Count()==relics.Length&&relics.Select(r=>r.Name).Distinct().Count()==relics.Length,"unique ids and names");
        for(int s=0;s<NativeBossChips.Sets.Length;s++)
        {
            var set=NativeBossChips.Sets[s];Check(relics.Count(r=>r.Set==s)==2,set.Name+" has two");Check(NativeBossChips.Of(set.Boss).Length==2,set.Name+" drops from its own boss");
            Check(NativeChipCartridge.Known(set.Tier)&&NativeChipCartridge.Rarity(set.Tier)==set.Rarity,set.Tier+" is a known tier in its colour");
            var look=NativeChipCartridge.Of(set.Tier);Check(look.Tier==set.Tier,set.Tier+" has its own look");Check(NativeBossChips.IsTier(set.Tier)&&!NativeBossChips.IsTier("siren")&&!NativeBossChips.IsTier("legendary"),"the tiers are the relics' own");
        }
        Check(NativeBossChips.Sets.Select(s=>NativeChipCartridge.Of(s.Tier).Inlay).Distinct().Count()==6,"six different looks");
        var icons=File.ReadAllText(Path.Combine(root,"Assets","Conversion","Resources","Icons.json"));
        foreach(var r in relics)
        {
            var chip=NativeBossChips.Chip(r);Check(chip.max==1&&chip.rarity==r.Tier&&chip.mechanic==r.Id,r.Id+": one copy, its tier, its rule");
            Check(NativeChipCopy.Find(r.Id,out var copy)&&copy.Rows!=null&&copy.Rows.Length>0&&copy.Rows.All(x=>x.tone=="good"||x.tone=="bad")&&!string.IsNullOrEmpty(copy.Short),r.Id+": a short line and green/red rows on its card");
            Check(NativeChipIcons.Native(r.Id)==r.Icon&&icons.Contains("{\"id\":\""+r.Icon+"\",\"markup\""),r.Id+": its icon is in the pinned set");
            Check(r.Rule.Length>30&&r.Rule.Length<220,r.Id+": the rule's words");
        }
        // The rows join the content where it loads (Runtime NativeStructures, beside the other overlays); the source JSON itself has none.
        var content=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        Check(!content.chips.Any(c=>NativeBossChips.Is(c.id)||NativeBossChips.IsTier(c.rarity)),"not in the source content");
        var before=new System.Collections.Generic.List<string>();for(int seed=1;seed<=40;seed++)before.AddRange(NativeContentRules.Offers(content,"sword",new System.Collections.Generic.Dictionary<string,int>(),new SourceRandom((uint)seed).Next,3));
        NativeBossChips.Apply(content);Check(content.chips.Count(c=>NativeBossChips.Is(c.id))==12,"the content takes all twelve");NativeBossChips.Apply(content);Check(content.chips.Count(c=>NativeBossChips.Is(c.id))==12,"once");
        var live=File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime","NativeStructures.cs"));Check(live.Contains("NativeBossChips.Apply(content);"),"the live content applies them");
        var arena=File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime","NativeArena.cs"));Check(arena.Contains("new HashSet<string>(NativeBossChips.Mechanics)"),"every relic's rule is supported");
        var after=new System.Collections.Generic.List<string>();
        foreach(var spec in new[]{"sword","rifle","pistol","bow","daggers"})for(int seed=1;seed<=200;seed++){var o=NativeContentRules.Offers(content,spec,new System.Collections.Generic.Dictionary<string,int>(),new SourceRandom((uint)seed).Next,6);Check(!o.Any(NativeBossChips.Is),"never an ordinary offer ("+spec+")");if(spec=="sword"&&seed<=40)after.AddRange(NativeContentRules.Offers(content,"sword",new System.Collections.Generic.Dictionary<string,int>(),new SourceRandom((uint)seed).Next,3));}
        Check(before.SequenceEqual(after),"the ordinary rolls are as without them");
        Check(NativeBossChips.DropChance>0&&NativeBossChips.DropChance<.25f,"a rare chance");
        // The drop: about DropChance of boss screens, only that boss's own, never one held or already offered, in the last place.
        int drops=0,n=20000;var rnd=new SourceRandom(77);
        for(int i=0;i<n;i++){var d=NativeBossChips.Drop(NativeHumBoss.Archetype,null,rnd.Next);if(d!=null){drops++;Check(NativeBossChips.Of(NativeHumBoss.Archetype).Any(r=>r.Id==d),"the boss's own relic");}}
        Check(Math.Abs(drops/(double)n-NativeBossChips.DropChance)<.012,"the drop's rate ("+drops+"/"+n+")");
        for(int i=0;i<400;i++){var d=NativeBossChips.Drop(NativeDeepEndBoss.Archetype,id=>id!=NativeBossChips.Warm,rnd.Next);Check(d==null||d==NativeBossChips.Undertow,"never one it can't take");}
        Check(NativeBossChips.Drop(NativeHumBoss.Archetype,id=>false,()=>0)==null&&NativeBossChips.Drop("nobody",null,()=>0)==null&&NativeBossChips.Drop(NativeHumBoss.Archetype,null,()=>.5)==null,"none when none can drop, or the chance misses");
        Check(NativeBossChips.Place(new[]{"a","b","c"},"r").SequenceEqual(new[]{"a","b","r"})&&NativeBossChips.Place(new[]{"a"},"r").SequenceEqual(new[]{"a","r"})&&NativeBossChips.Place(new[]{"a","b","c"},null).SequenceEqual(new[]{"a","b","c"}),"placed last, or added to a short screen");
        foreach(var set in NativeBossChips.Sets)Check(NativeBossChips.Of(set.Boss).All(r=>NativeBossChips.Relics.Any(x=>x.Id==r.Id)),"each set's relics");
        // A boss reward's boss: by its zone's map in the layout.
        var layout=new ExpeditionLayout{areas=new[]{new ExpeditionAreaLayout{id="exp-backrooms",map=NativeBackroomsGenerator.InstanceMap},new ExpeditionAreaLayout{id="exp-pool",map=NativePoolroomsGenerator.InstanceMap},new ExpeditionAreaLayout{id="m1-ward",map="m1-ward-map"}}};
        Check(NativeBossChips.RewardBoss("0:exp-backrooms:boss:hum:0",layout)==NativeHumBoss.Archetype&&NativeBossChips.RewardBoss("2:exp-pool:boss:x:1",layout)==NativeDeepEndBoss.Archetype,"a zone's boss by its map");
        Check(NativeBossChips.RewardBoss("0:m1-ward:boss:m1-ward-siren:0",layout)=="siren"&&NativeBossChips.RewardBoss("0:nowhere:boss:x:0",layout)==null&&NativeBossChips.RewardBoss("bad",layout)==null&&NativeBossChips.RewardBoss("0:m1-ward:boss:x:0",null)==null,"the Siren's ward; unknown zones none");
        var ids=new[]{NativeBossChips.LastVerse,NativeBossChips.Lullaby,NativeBossChips.Express,NativeBossChips.MindGap,NativeBossChips.SlagHeart,NativeBossChips.Pour,NativeBossChips.MaxHum,NativeBossChips.Noclip,NativeBossChips.Undertow,NativeBossChips.Warm,NativeBossChips.Feather,NativeBossChips.Lux};
        Check(ids.Distinct().Count()==12&&ids.All(NativeBossChips.Is),"the rules' ids are the twelve");
        Fx(ids);
        Console.WriteLine("PASS boss-chips ("+count+" checks)");
    }
    // Each relic's moment in the world (Core NativeBossRelicFx): read from its ChipProc, drawn finite through its life, fewer pieces with Reduced effects, gone after.
    private sealed class Sink:IKitFxSink
    {
        public int Calls;public bool Bad;
        static bool F(System.Numerics.Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
        public void Glow(System.Numerics.Vector3 at,System.Numerics.Vector3 color,float coverage,float size,int profile,float u,float seed){Calls++;Bad|=!F(at)||!F(color)||!float.IsFinite(size)||size<0;}
        public void Streak(System.Numerics.Vector3 head,System.Numerics.Vector3 velocity,System.Numerics.Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Calls++;Bad|=!F(head)||!F(velocity)||!F(color);}
        public void Smoke(System.Numerics.Vector3 at,System.Numerics.Vector3 albedo,float opacity,float size,float u,float seed,float floor){Calls++;Bad|=!F(at)||!float.IsFinite(opacity);}
        public void Sprite(bool view,System.Numerics.Vector3 at,System.Numerics.Vector3 color,float size,int profile){Calls++;Bad|=!F(at)||view;}
        public void Line(bool view,System.Numerics.Vector3 tail,System.Numerics.Vector3 head,System.Numerics.Vector3 color,float width,bool shard){Calls++;Bad|=!F(tail)||!F(head)||!F(color)||view;}
        public void Band(bool view,System.Numerics.Vector3 a,System.Numerics.Vector3 b,System.Numerics.Vector3 c,System.Numerics.Vector3 d,float ua,float ub,System.Numerics.Vector3 color){Calls++;Bad|=!F(a)||!F(b)||!F(c)||!F(d)||!F(color)||view;}
    }
    private static void Fx(string[] ids)
    {
        int id=1;foreach(var relic in ids)foreach(int target in new[]{0,7})
        {
            var fx=new NativeBossRelicFx();var e=new ArenaEvent{Id=id++,Kind=EventKind.ChipProc,Time=10,Source=1,Target=target,Detail=relic,Amount=relic==NativeBossChips.Express&&target==0?0:3,Position=new V3(4,.9f,6),From=new V3(1,.1f,2),Normal=new V3(1,0,0)};
            fx.Observe(new[]{e},10);Check(fx.Count==1,relic+": its proc is read");int full=0,thin=0;
            for(float age=0;age<=3.4f;age+=.05f){var s=new Sink();fx.Observe(new[]{e},10+age);fx.Draw(s,10+age,1,false);Check(!s.Bad,relic+": finite, in the world");full+=s.Calls;var t=new Sink();fx.Draw(t,10+age,.3f,true);thin+=t.Calls;}
            Check(full>0&&thin<=full,relic+" ("+target+"): drawn, thinner with Reduced effects ("+full+" / "+thin+")");Check(fx.Count==0,relic+": gone after its life");
            var other=new NativeBossRelicFx();other.Observe(new[]{new ArenaEvent{Id=1,Kind=EventKind.ChipProc,Time=10,Detail="wild-domino",Position=new V3(0,0,0)},new ArenaEvent{Id=2,Kind=EventKind.Hit,Time=10,Detail=relic,Position=new V3(0,0,0)}},10);Check(other.Count==0,"only the relics' procs");
        }
    }
}
