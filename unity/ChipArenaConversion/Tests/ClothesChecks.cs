using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;
// The pilot's new clothes (owner 2026-11-27: "Lets add more clothes options now"; docs/PILOT-BODY-HAIR.md section 4): each new id
// is drawn by something (a zombie piece the bake carries, Core/NativeWardrobeAliases.cs, or the hair bank), comes before its
// slot's DEV pieces (the Locker trims those from the end) and is a public pick; a login keeps this PC's new piece in every slot
// while the account service does not list it, and ServiceIds mirrors the service's own lists.
internal static class ClothesChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Clothes: "+why);}
        string Src(string rel)=>File.ReadAllText(Path.Combine(root,rel));
        for(int s=0;s<NativeLook.Slots.Length;s++)Check(NativeLook.Ids[s].Length==NativeLook.Labels[s].Length&&NativeLook.Ids[s].Distinct().Count()==NativeLook.Ids[s].Length,"ids and labels: "+NativeLook.Slots[s]);
        // the aliases: a slot below the hair, a listed public id, a zombie piece of the bake's Wardrobe table
        var zombies=Src("Assets/ChipArena/Core/NativeZombieLooks.cs")+Src("Assets/ChipArena/Core/NativeCampSurvivors.cs");
        foreach(var p in NativeWardrobeAliases.Pairs)
        {
            int s=NativeLook.SlotIndex(p[0]);Check(s>0,"an alias's slot: "+p[1]);
            int k=Array.IndexOf(NativeLook.Ids[s],p[1]);Check(k>0&&!NativeLook.DevOnly(p[1]),"listed and public: "+p[1]);
            Check(p[2].StartsWith("z-",StringComparison.Ordinal)&&(zombies.Contains("\""+p[2]+"|")||zombies.Contains("\""+p[2]+"\"")),"drawn by a zombie piece: "+p[1]+" by "+p[2]);
            Check(NativeWardrobeAliases.Of(p[0],p[1])==p[2],"the alias lookup: "+p[1]);
            Check(NativeLook.Gate(NativeLook.With("",p[0],p[1]),false)==p[0]+":"+p[1],"a public pick survives the gate: "+p[1]);
        }
        Check(NativeWardrobeAliases.Of(NativeLook.Upper,"tee")==null&&NativeWardrobeAliases.Of(NativeLook.Lower,"cardigan")==null,"the bake's own pieces are not aliases");
        // every id the service does not list is drawn by an alias or the hair bank, and the DEV pieces stay last
        var tool=Src("tools/gear/hair.py");
        var bankStyles=Regex.Matches(tool,@"^\s*\('([a-z-]+)', \[\('",RegexOptions.Multiline).Select(m=>m.Groups[1].Value).ToHashSet();
        var suitStyles=Regex.Matches(tool,@"\('(upper|lower)', '([a-z-]+)', '(Top|Bottom)\.Bank_",RegexOptions.Multiline).Select(m=>m.Groups[1].Value+":"+m.Groups[2].Value).ToHashSet();
        Check(suitStyles.SetEquals(new[]{"upper:"+NativeRobotSuit.SleekUnder[1],"lower:"+NativeRobotSuit.SleekUnder[2]}),"the tool makes the sleek suit's garments");
        for(int s=0;s<NativeLook.Slots.Length;s++)
        {
            var ids=NativeLook.Ids[s];
            foreach(var id in ids)if(Array.IndexOf(NativeLook.ServiceIds[s],id)<0)Check(s==0?bankStyles.Contains(id):NativeWardrobeAliases.Of(NativeLook.Slots[s],id)!=null||suitStyles.Contains(NativeLook.Slots[s]+":"+id),"drawn by something: "+NativeLook.Slots[s]+" "+id);
            int firstDev=Array.FindIndex(ids,NativeLook.DevOnly);
            if(firstDev>=0)for(int k=firstDev;k<ids.Length;k++)Check(NativeLook.DevOnly(ids[k]),"the DEV pieces last: "+NativeLook.Slots[s]);
        }
        // the account service: ServiceIds mirrors src/relay/accounts.ts LOOK_SLOTS (schema 4)
        var accounts=Src("../../src/relay/accounts.ts");
        for(int s=0;s<NativeLook.Slots.Length;s++)
        {
            var m=Regex.Match(accounts,@"\b"+NativeLook.Slots[s]+@":\[([^\]]*)\]");Check(m.Success,"the service lists "+NativeLook.Slots[s]);
            var listed=Regex.Matches(m.Groups[1].Value,"'([^']+)'").Select(x=>x.Groups[1].Value).ToArray();
            Check(listed.SequenceEqual(NativeLook.ServiceIds[s])||listed.Length>NativeLook.ServiceIds[s].Length&&NativeLook.ServiceIds[s].All(listed.Contains),"ServiceIds mirrors the service: "+NativeLook.Slots[s]);
        }
        // a login keeps this PC's new piece in each slot while the service drops it
        Check(NativeLook.KeepSkin("","upper:cardigan,lower:long-skirt,feet:heels,skin:c48a62",4)=="upper:cardigan,lower:long-skirt,feet:heels,skin:c48a62","schema 4 keeps this PC's new clothes");
        Check(NativeLook.KeepSkin("upper:tee","upper:cardigan,feet:heels",4)=="upper:tee,feet:heels","the account's own pick wins its slot");
        Check(NativeLook.KeepSkin("","upper:cardigan",NativeLook.ServiceKeepsNewPieces)=="","a service that keeps them is the truth");
        Check(NativeLook.KeepSkin("","upper:tee,feet:sneakers",4)=="","the service's own ids are its own from schema 4");
        // the presentation (source): the alias copies its piece and anchors; the dressed look falls back per slot
        var pres=Src("Assets/ChipArena/Runtime/NativeHairBankPresentation.cs");var body=Src("Assets/ChipArena/Runtime/NativeVisualBodyPresentation.cs");
        Check(pres.Contains("pieces.Add(new LookPiece{slot=pair[0],id=pair[1],renderers=from.renderers,covers=from.covers,tags=from.tags,lift=from.lift});"),"an alias draws its piece's renderers, covers, tags and lift");
        Check(pres.Contains("anchors.Add(new LookAnchor{upper=pair[1],hands=a.hands,core=a.core,device=a.device,emblem=a.emblem,radius=a.radius});"),"an alias top takes its piece's gear anchors");
        Check(body.Contains("bool local=s==0||NativeWardrobeAliases.Of(slot,piece)!=null||HairBank()?.Has(slot,piece)==true;")&&body.Contains("if(local&&!VisualLookHas(slot,piece))"),"a piece drawn by the local data that this install lacks draws its slot's default");
        // (The Hot Dog top is in no look data, and is drawn by its own code: never swapped for the default. Owner 2026-11-27: "Hot
        // Dog Top isn't working now?", after release 35 swapped every piece the look data lacked.)
        Check(NativeWardrobeAliases.Of(NativeLook.Upper,NativeHotDog.Id)==null&&Array.IndexOf(NativeLook.Ids[1],NativeHotDog.Id)>0,"the Hot Dog top is not a look-data piece");
        // the sleek pilot suit: the armour worn over its bodysuit while the bank carries it, today's garments otherwise (and the
        // zombies' fallen pilots always)
        bool was=NativeRobotSuit.SleekSuit;
        try
        {
            NativeRobotSuit.SleekSuit=false;Check(NativeRobotSuit.Dress("")=="upper:sweater,lower:skinny-jeans,hands:gloves,feet:work-boots","without the bank: today's garments");
            NativeRobotSuit.SleekSuit=true;Check(NativeRobotSuit.Dress("")=="upper:pilot-suit,lower:pilot-suit-legs,hands:gloves,feet:work-boots","with it: the bodysuit under the armour");
            Check(NativeRobotSuit.Dress("upper:tee")=="upper:tee,lower:pilot-suit-legs,hands:gloves,feet:work-boots"&&NativeRobotSuit.UnderOf(1)=="pilot-suit"&&NativeRobotSuit.Under[1]=="sweater","each robot piece over its own garment; the fallback kept");
            Check(Array.IndexOf(NativeLook.Ids[1],"pilot-suit")>0&&Array.IndexOf(NativeLook.Ids[2],"pilot-suit-legs")>0&&!NativeLook.DevOnly("pilot-suit"),"the bodysuit is a public pick of its own");
        }
        finally{NativeRobotSuit.SleekSuit=was;}
        var suitSrc=Src("Assets/ChipArena/Core/NativeRobotSuit.cs");var hb=Src("Assets/ChipArena/Runtime/NativeHairBankPresentation.cs");
        Check(suitSrc.Contains("if(NativeSleekFace.Has(plates,NativeSleekFace.Ears))NativeSleekFace.Add(k,k.Part(\"ears\",Head),plates,NativeSleekFace.Ears);else Ears(k);")&&suitSrc.Contains("Gauntlets(k);Boots(k);")&&suitSrc.Contains("if(NativeSleekFace.IsFace(pl.Name))continue;"),"the sleek plates beside the bank's ear pods (else today's ear modules), today's gauntlets and boots; never the face gear's");
        Check(hb.Contains("NativeRobotSuit.SleekSuit=hairBank!=null&&hairBank.Has(NativeLook.Upper,NativeRobotSuit.SleekUnder[1])&&hairBank.Has(NativeLook.Lower,NativeRobotSuit.SleekUnder[2])&&hairBank.Plates.Length>0;"),"the sleek suit only when the bank carries it whole");
        Check(Src("Assets/ChipArena/Runtime/NativeBodyKindPresentation.cs").Contains("p.id!=NativeRobotSuit.UnderOf(slot)"),"the plates' female fit follows the garment really worn under them");
        Console.WriteLine("PASS clothes ("+NativeWardrobeAliases.Pairs.Length+" new pieces, the sleek suit's garments)");
    }
}
