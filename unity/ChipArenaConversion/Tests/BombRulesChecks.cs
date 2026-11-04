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
        NativeBombPreview.Write(k,System.Numerics.Vector3.Zero,0,0,4,2);cpy=k.ViewCopy();Check(!ReferenceEquals(cpy.Bombs[0],k.Bombs[0]),"deep charges");cpy.Bombs[0].Pos.x=float.NaN;Check(!cpy.Valid()&&k.Valid(),"reject corrupt charge without altering live state");k.MineCharges=3;Check(!k.Valid(),"invalid mine charges");
        var hop=new NativeTechnique{Kind="parting-line",Phase="hop",Release=1,At=0,Until=2};Check(NativeTechniqueRules.Moving(hop,1.1f),"hop moving");hop.Phase="line";Check(!NativeTechniqueRules.Moving(hop,1.1f),"early wall completion no anchor fallback");hop.Phase="hop";Check(!NativeTechniqueRules.Moving(hop,1.29f),"hop ends at .28");
        Console.WriteLine("PASS Bombadier authority rules: "+n+" checks");
    }
}
