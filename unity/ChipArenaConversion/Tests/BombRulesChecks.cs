using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class BombRulesChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool ok,string text){if(!ok)throw new Exception("Bomb rules: "+text);n++;}
        var d=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});d.Validate();
        Check(d.Weapon("bombadier").devOnly&&d.Weapon("bombadier").ammo==6&&d.Weapon("bombadier").reload==1.7,"DEV belt content");
        var stats=NativeContentRules.Stats(d,"bombadier",new Dictionary<string,int>());Check(stats["hp"]==90&&Math.Abs(stats["speed"]-.95)<.0001,"health and speed");Check(NativeRangedRules.KeyboardQ(d,"bombadier",true),"keyboard Q");Check(!NativeAccess.Guest.Allows("weapon:bombadier",0),"guest denied");
        foreach(var id in NativeBombs.Slot.Concat(new[]{NativeBombs.GrandFinale}))Check(d.abilities.Any(a=>a.spec==NativeBombs.Spec&&a.id==id&&a.live),"live row "+id);
        var chain=new NativeBombChain();float total=0;float[] scales={1,.8f,.6f,.45f};
        for(int i=0;i<4;i++){float now=i*.2f;chain.Prepare(now);Check(Math.Abs(chain.Scale(now)-scales[i])<.0001,"link scale "+i);float hit=16*chain.Scale(now);Check(chain.Hit(now,hit,new V3(i,0,0))==i,"link index "+i);total+=hit;}
        Check(Math.Abs(total-45.6f)<.001&&chain.Scorched(.6f)&&chain.Scale(.7f)==.5f,"third link ends launches");
        for(int i=0;i<50;i++){chain.Prepare(.7f+i*.02f);chain.Hit(.7f+i*.02f,Math.Min(10000,chain.Budget(100)),default);}Check(chain.Spent<=60&&chain.Budget(100)==0,"unlimited followups cannot exceed budget");
        var independent=new NativeBombChain();Check(!independent.Scorched(1)&&independent.Budget(100)==60,"other owner has independent chain");chain.Prepare(3.11f);Check(chain.Spent==0&&!chain.Scorched(3.11f),"escape window expiry");
        foreach(int hp in new[]{15,90,100,200}){var c=new NativeBombChain();Check(c.Budget(hp)<hp,"budget under max health "+hp);}
        var k=new NativeBombKit();NativeBombPreview.Write(k,System.Numerics.Vector3.Zero,0,10,25,2);Check(k.Valid(),"preview wire accepted");var cpy=k.ViewCopy();Check(!ReferenceEquals(k.Ready,cpy.Ready)&&!ReferenceEquals(k.Finale,cpy.Finale)&&!ReferenceEquals(k.Finale.Circles,cpy.Finale.Circles),"deep finale and cooldown copy");float x=cpy.Finale.Circles[0].Pos.x;k.Finale.Circles[0].Pos.x+=100;Check(cpy.Finale.Circles[0].Pos.x==x,"replay owns circles");
        NativeBombPreview.Write(k,System.Numerics.Vector3.Zero,0,0,4,2);cpy=k.ViewCopy();Check(!ReferenceEquals(cpy.Bombs[0],k.Bombs[0]),"deep charges");cpy.Bombs[0].Pos.x=float.NaN;Check(!cpy.Valid()&&k.Valid(),"reject corrupt charge without altering live state");k.MineCharges=4;Check(k.Valid(),"two Betty stacks accepted");k.MineCharges=5;Check(!k.Valid(),"invalid mine charges");
        NativeBombChips.Apply(d);d.Validate();Check(NativeBombChips.Ids.All(id=>d.Chip(id)!=null),"live ten-chip overlay");
        var held=new Dictionary<string,int>{{NativeBombChips.Bandolier,1},{NativeBombChips.BlackPowder,1}};
        stats=NativeContentRules.Stats(d,NativeBombs.Spec,held);Check(Math.Ceiling(6*stats["ammoCapacity"])==8&&Math.Abs(1.7*stats["reloadSpeed"]-1.36)<.0001&&Math.Abs(stats["damage"]-1.15)<.0001,"belt and powder stats");
        Check(NativeContentRules.Eligible(d,NativeBombs.Spec,new Dictionary<string,int>()).All(c=>!c.tags.Contains("projectile")),"unsupported projectile chips excluded");
        foreach(var spec in new[]{"pistol","emperor","fire"})Check(!NativeContentRules.Eligible(d,spec,new Dictionary<string,int>()).Any(c=>NativeBombChips.Is(c.id)),"signature rows exclusive "+spec);
        for(int stacks=0;stacks<=2;stacks++){Check(Math.Abs(NativeBombChipRules.PopFuse(stacks)-(.9f-.25f*stacks))<.0001,"fuse stack");Check(Math.Abs(NativeBombChipRules.PopSpeed(stacks)-(float)Math.Pow(1.15,stacks))<.0001,"speed stack");Check(Math.Abs(NativeBombChipRules.MineLift(stacks)*NativeBombChipRules.MineLift(stacks)/46-8*Math.Pow(1.25,stacks))<.0001,"mine height (not velocity) multiplier");Check(Math.Abs(NativeBombChipRules.JumpCooldown(stacks)-8*Math.Pow(.7,stacks))<.0001,"jump cooldown stack");}
        var daisy=new NativeBombChain{Window=1.6f,Daisy=true};float[] improved={1,.9f,.75f,.6f};
        for(int i=0;i<4;i++){float now=i*1.4f;daisy.Prepare(now);Check(Math.Abs(daisy.Scale(now)-improved[i])<.0001,"Daisy scale and window");daisy.Hit(now,16*daisy.Scale(now),default);}
        Check(daisy.Scorched(4.3f)&&daisy.Spent<60,"Daisy still ends at third link");
        var fire=new NativeBombChain();fire.Prepare(10);fire.Spent=12;fire.LastDamage=10;fire.Prepare(10.1f);Check(fire.Spent==12&&fire.Next(10.1f)==0,"floor damage preserves budget without making a link");fire.Prepare(11.7f);Check(fire.Spent==0,"floor budget expires after gap");
        var hop=new NativeTechnique{Kind="parting-line",Phase="hop",Release=1,At=0,Until=2};Check(NativeTechniqueRules.Moving(hop,1.1f),"hop moving");hop.Phase="line";Check(!NativeTechniqueRules.Moving(hop,1.1f),"early wall completion no anchor fallback");hop.Phase="hop";Check(!NativeTechniqueRules.Moving(hop,1.29f),"hop ends at .28");
        Console.WriteLine("PASS Bombadier authority rules: "+n+" checks");
    }
}
