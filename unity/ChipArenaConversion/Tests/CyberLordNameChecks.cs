using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using ChipArena.UnityConversion;

// The Cyber Lord shows Kit v2, the shipped kit, everywhere a player reads its abilities (owner 2026-10-15: "When loading in,
// we can see the old ability names for some reason? Lets fix that please."). The authority makes the v2 record lazily, at the
// pilot's first Cyber input (NativeCyberKit2Rules.Ensure behind CyberAllowed), and resets the kit on a death; before the fix
// the drawer fell back to v1's cards (Q · DATA DASH ...) whenever the view had no live record: the stage load, the intro, the
// countdown and draft, a reset round, a guest before a snapshot carries the record, a spectated or killcam pilot without it.
// Astra's content still carries v1's names and text (the weapon description, the abilities' names and text), which the
// Locker and the catalogue showed. Checked here:
//  1. the drawer with no record (a fresh Version 0 kit, a death's reset kit, a ranged-mode pilot without the record, no view
//     at all): v2's names, art icons, costs and READY / LOW MANA, in melee and ranged mode; the old screen logic showed v1's;
//  2. the drawer with the authority's live record: exactly the old screen logic's cards over a timeline (unchanged);
//  3. the menu text: the weapon description, the Locker's ability rows, the tile strip and stat line, the entrance's text, the
//     loading tips;
//  4. a text scan: no player-facing string resolves to a v1 name (the functional surfaces above and every string literal in
//     Core and Runtime, except the v1 tables kept for v1's checks); the Runtime draws no v1 table and reads the Cyber Lord's
//     weapon description through NativeLoadoutText only.
// Internal ids (data-dash, firewall ...) and comments are not player-facing. Rules, records and content are untouched.
internal static class CyberLordNameChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Cyber Lord names: "+what);count++;}
    /// <summary>The v1 kit's display names v2 replaced (Kernel Panic stays in v2).</summary>
    private static readonly string[] V1=CyberLordSpec.Names.Where(n=>n!="Kernel Panic").Concat(CyberRangedSpec.Names).ToArray();
    /// <summary>The v1 name `s` shows (any case), else null.</summary>
    private static string Stale(string s){if(string.IsNullOrEmpty(s))return null;foreach(var n in V1)if(s.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0)return n;return null;}

    private struct Shown{public string Label,State,Icon;public bool Ready,Active,Low;public float Dial;}
    private static Shown Of(NativeCyberLordHud.Card c,string label)=>new Shown{Label=label,State=c.State,Icon=c.Icon,Ready=c.Ready,Active=c.Active,Low=c.Low,Dial=c.Dial};
    private static readonly NativeCyberLordHud.Card[] cards=new NativeCyberLordHud.Card[NativeCyberLordHud.DrawerCards];
    private static readonly string[] labels=new string[NativeCyberLordHud.DrawerCards];
    private static List<Shown> Drawer(CyberLordView v,float t,float mana,float kernelReady)
    {
        int n=NativeCyberLordHud.Drawer(v,t,mana,kernelReady,cards,labels);var l=new List<Shown>();for(int i=0;i<n;i++)l.Add(Of(cards[i],labels[i]));return l;
    }
    /// <summary>The drawer as Runtime/NativeCyberLordScreen.cs built it before this fix (base 3ced89c): v2 only with a live record.</summary>
    private static List<Shown> OldDrawer(CyberLordView v,float t,float mana,float kernelReady)
    {
        var l=new List<Shown>();bool ranged=NativeCyberLordHud.ShowsRanged(v,t);
        foreach(int slot in NativeCyberLordHud.Order)
        {
            if(ranged&&slot>=CyberLordSpec.One)continue;
            bool v2=CyberKit2MeleeHud.Replaces(v,slot);var c=v2?CyberKit2MeleeHud.Of(v,slot,t,mana):NativeCyberLordHud.Of(v,slot,t,mana,slot==CyberLordSpec.Four?kernelReady:float.NaN);
            l.Add(Of(c,v2?CyberKit2MeleeHud.Labels[slot]:NativeCyberLordHud.Labels[slot]));
        }
        if(ranged&&CyberKit2RangedHud.Shows(v))foreach(int slot in CyberKit2RangedHud.Order)l.Add(Of(CyberKit2RangedHud.Of(v,slot,t,mana,kernelReady),CyberKit2RangedHud.Labels[slot]));
        else if(ranged)foreach(int slot in NativeCyberLordHud.RangedOrder)l.Add(Of(NativeCyberLordHud.RangedOf(v,slot,t,mana,kernelReady),NativeCyberLordHud.RangedLabels[slot]));
        {var c=NativeCyberLordHud.ModeOf(v,t);c.Low=false;l.Add(Of(c,ranged?NativeCyberLordHud.RangedMode:NativeCyberLordHud.MeleeMode));}
        return l;
    }
    private static bool Same(List<Shown> a,List<Shown> b)
    {
        if(a.Count!=b.Count)return false;
        for(int i=0;i<a.Count;i++){var x=a[i];var y=b[i];if(x.Label!=y.Label||x.State!=y.State||x.Icon!=y.Icon||x.Ready!=y.Ready||x.Active!=y.Active||x.Low!=y.Low||!(x.Dial==y.Dial||float.IsNaN(x.Dial)&&float.IsNaN(y.Dial)))return false;}
        return true;
    }
    private static string Text(List<Shown> l)=>string.Join(" | ",l.Select(c=>c.Label+" "+c.State+" ["+c.Icon+"]"));
    private static readonly string[] MeleeLabels={"Q · UPLINK","E · OVERCLOCK","1 · FORK BOMB","2 · GLITCH CUT","3 · METEOR BYTE","4 · KERNEL PANIC","5 · MELEE"};
    private static readonly string[] RangedLabels={"Q · UPLINK","E · OVERCLOCK","1 · DAISY CHAIN","2 · SINGULARITY","3 · PACKET STORM","4 · ORBITAL UPLINK","5 · RANGED"};
    private static readonly string[] MeleeIcons={CyberKit2Spec.Icons[CyberKit2Spec.Q],CyberKit2Spec.Icons[CyberKit2Spec.E],CyberKit2Spec.Icons[CyberKit2Spec.Fork],CyberKit2Spec.Icons[CyberKit2Spec.Cut],CyberKit2Spec.Icons[CyberKit2Spec.Meteor],NativeCyberLordHud.Icons[CyberLordSpec.Four],CyberRangedSpec.MeleeIcon};
    private static readonly string[] RangedIcons={CyberKit2Spec.Icons[CyberKit2Spec.Q],CyberKit2Spec.Icons[CyberKit2Spec.E],CyberKit2Spec.Icons[CyberKit2Spec.Chain],CyberKit2Spec.Icons[CyberKit2Spec.Singularity],CyberKit2Spec.Icons[CyberKit2Spec.Storm],CyberKit2Spec.Icons[CyberKit2Spec.Orbital],CyberRangedSpec.ModeIcon};

    public static void Run(string root,ConversionContent data)
    {
        count=0;var facts=new List<string>();
        var savedMelee=CyberLordFeed.Kit2MeleeSource;var savedRanged=CyberLordFeed.Kit2RangedSource;var savedMode=CyberLordFeed.RangedSource;
        CyberLordFeed.Kit2MeleeSource=null;CyberLordFeed.Kit2RangedSource=null;CyberLordFeed.RangedSource=null;CyberKit2Melee.Clones.Clear();
        try
        {
            // ---- 1. no record: v2's cards, READY or LOW MANA (and the old screen showed v1's) ----
            {
                var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);var v=new CyberLordView();
                Check(a.Cyber.Version==0&&a.Cyber.Kit2==null&&a.Cyber.Kit2R==null,"the fixture's kit is a fresh Version 0 kit (no record)");
                CyberLordFeed.Read(a,v);Check(!v.Kit2.Live&&!v.Kit2R.Live&&!NativeCyberLordHud.ShowsRanged(v,5),"no record: neither v2 view is live, melee mode");
                foreach(float mana in new[]{100f,5f})foreach(float t in new[]{0f,.5f,5f,30f})
                {
                    var l=Drawer(v,t,mana,0);
                    Check(l.Select(c=>c.Label).SequenceEqual(MeleeLabels),"melee, no record, t "+t+": the v2 labels ("+Text(l)+")");
                    Check(l.Select(c=>c.Icon).SequenceEqual(MeleeIcons),"melee, no record: the v2 art icons ("+Text(l)+")");
                    for(int i=0;i<5;i++){int s=CyberKit2MeleeHud.Kit2Slot(i);bool low=mana<CyberKit2Spec.Costs[s];Check(l[i].State==(low?"LOW MANA":"READY")&&l[i].Low==low&&l[i].Ready==!low&&!l[i].Active&&l[i].Dial==1,"melee, no record, "+l[i].Label+" at "+mana+" mana: "+l[i].State);}
                    Check(l.All(c=>Stale(c.Label)==null&&Stale(c.State)==null),"melee, no record: no v1 name ("+Text(l)+")");
                }
                var old=OldDrawer(v,5,100,0);Check(old.Any(c=>c.Label=="Q · DATA DASH")&&old.Any(c=>c.Label=="E · FIREWALL"),"the old screen logic showed v1's cards with no record (the owner's report): "+Text(old));
                facts.Add("before: "+string.Join(", ",old.Select(c=>c.Label)));facts.Add("after: "+string.Join(", ",Drawer(v,5,100,0).Select(c=>c.Label)));

                // Ranged mode without the v2 record (the mode state replicated, Kit2R absent).
                a.Cyber.Ranged=new NativeCyberRangedState{Ranged=true};CyberLordFeed.Read(a,v);Check(NativeCyberLordHud.ShowsRanged(v,5)&&!v.Kit2R.Live,"ranged mode, no record");
                foreach(float mana in new[]{100f,5f})
                {
                    var l=Drawer(v,5,mana,0);
                    Check(l.Select(c=>c.Label).SequenceEqual(RangedLabels)&&l.Select(c=>c.Icon).SequenceEqual(RangedIcons),"ranged, no record: the v2 labels and icons ("+Text(l)+")");
                    for(int i=2;i<6;i++){int s=CyberKit2Ranged.Spec(CyberKit2RangedHud.Order[i-2]);bool low=mana<CyberKit2Spec.Costs[s];Check(l[i].State==(low?"LOW MANA":"READY"),"ranged, no record, "+l[i].Label+" at "+mana+" mana: "+l[i].State);}
                    Check(l.All(c=>Stale(c.Label)==null),"ranged, no record: no v1 name");
                }
                Check(Drawer(v,5,100,20)[5].State=="15.0s","ranged, no record: Orbital Uplink still waits for the ultimate clock (kernelReady)");
                var oldRanged=OldDrawer(v,5,100,0);Check(oldRanged.Any(c=>c.Label=="1 · PING LANCE"),"the old screen logic showed v1's ranged set with no record: "+Text(oldRanged));
                // No view at all (the presentation has not seen the actor): the screen reads a fresh view.
                var fresh=NativeCyberLordHud.Fresh();Check(Drawer(fresh,5,100,0).Select(c=>c.Label).SequenceEqual(MeleeLabels)&&Drawer(fresh,5,100,0).All(c=>c.State=="READY"),"no view: a fresh view's v2 cards, READY");
                Check(NativeCyberLordHud.Drawer(null,5,100,0,cards,labels)==0&&NativeCyberLordHud.Drawer(v,5,100,0,new NativeCyberLordHud.Card[3],labels)==0,"a null view or a short buffer draws nothing");
            }

            // ---- 2. the authority's live record: the drawer is exactly the old screen's, over a timeline ----
            {
                var a=CyberLordFixture.Pilot(2,Vector3.Zero,0);var s=a.Cyber;NativeCyberKit2Rules.Ensure(s);float mana=100;var v=new CyberLordView();
                CyberLordFeed.Read(a,v);Check(v.Kit2.Live&&v.Kit2R.Live,"an Ensure'd kit's records are live");
                {var l=Drawer(v,.5f,100,0);Check(Same(l,OldDrawer(v,.5f,100,0))&&l.Select(c=>c.Label).SequenceEqual(MeleeLabels)&&l.All(c=>c.State=="READY"),"a live, empty record: READY v2 cards, as before ("+Text(l)+")");}
                bool Cast(int slot,float at){mana=100;return NativeCyberKit2Rules.Begin(s,slot,at,ref mana,1,1,1,1);}
                Check(Cast(CyberKit2Spec.Q,1)&&Cast(CyberKit2Spec.E,2)&&Cast(CyberKit2Spec.Fork,3)&&Cast(CyberKit2Spec.Cut,4)&&Cast(CyberKit2Spec.Meteor,5),"the authority's rules cast Q, E, 1, 2 and 3");
                int states=0,compared=0;var seen=new HashSet<string>();
                for(float t=0;t<14;t+=1/30f)
                {
                    CyberLordFeed.Read(a,v);foreach(float m in new[]{100f,12f})
                    {var l=Drawer(v,t,m,0);Check(Same(l,OldDrawer(v,t,m,0)),"melee, live record, t "+t.ToString("0.00")+": unchanged ("+Text(l)+" vs "+Text(OldDrawer(v,t,m,0))+")");compared++;foreach(var c in l)if(seen.Add(c.Label+" "+(c.State.EndsWith("s")&&c.State.Contains('.')?"COOLDOWN":c.State)))states++;}
                }
                foreach(var want in new[]{"Q · UPLINK ACTIVE","E · OVERCLOCK ACTIVE","1 · FORK BOMB ACTIVE","2 · GLITCH CUT ACTIVE","3 · METEOR BYTE ACTIVE","Q · UPLINK COOLDOWN","Q · UPLINK READY","1 · FORK BOMB LOW MANA"})Check(seen.Contains(want),"the live timeline shows "+want+" ("+string.Join(", ",seen)+")");
                // Ranged mode with the live record: the v2 ranged set from the record, as before.
                s.Ranged.Ranged=true;s.Ranged.ModeAt=20;s.Ranged.ModeReady=20+CyberRangedSpec.SwitchCooldown;
                Check(Cast(CyberKit2Spec.Chain,21)&&Cast(CyberKit2Spec.Singularity,22)&&Cast(CyberKit2Spec.Storm,23),"the authority's rules cast ranged 1, 2 and 3");
                for(float t=19.5f;t<40;t+=1/30f){CyberLordFeed.Read(a,v);var l=Drawer(v,t,100,0);Check(Same(l,OldDrawer(v,t,100,0)),"ranged, live record, t "+t.ToString("0.00")+": unchanged");compared++;if(t>21)Check(l.Select(c=>c.Label).SequenceEqual(RangedLabels),"ranged, live record: the v2 ranged labels");}
                facts.Add("record live: "+compared+" frames identical to the old drawer");
                // A rewound killcam clock before the casts: v2's names, nothing cast yet.
                CyberLordFeed.Read(a,v);{var l=Drawer(v,.2f,100,0);Check(l.Select(c=>c.Label).SequenceEqual(MeleeLabels)&&l.Take(5).All(c=>c.State=="READY"),"a rewound clock before the record's casts: v2's cards, READY ("+Text(l)+")");}
                // A death resets the kit (StepCyberWorld: a new NativeCyberKit, Version 0): v2's cards still.
                a.Cyber=new NativeCyberKit();CyberLordFeed.Read(a,v);{var l=Drawer(v,30,100,0);Check(!v.Kit2.Live&&l.Select(c=>c.Label).SequenceEqual(MeleeLabels),"a death's reset kit: v2's cards ("+Text(l)+")");}
                // A guest's copy of the record (the view copy the frame carries) draws the same as the host's.
                var g=CyberLordFixture.Pilot(3,Vector3.Zero,0);g.Cyber=s;var gv=new CyberLordView();var hv=new CyberLordView();var host=CyberLordFixture.Pilot(2,Vector3.Zero,0);host.Cyber=s;
                CyberLordFeed.Read(host,hv);CyberLordFeed.Read(g.ViewCopy(),gv);Check(Same(Drawer(hv,25,100,0),Drawer(gv,25,100,0)),"a guest's copied actor draws the host's cards");
                // No allocation per frame once warmed.
                for(int i=0;i<4;i++)NativeCyberLordHud.Drawer(hv,25+i,100,0,cards,labels);
                long before=GC.GetAllocatedBytesForCurrentThread();for(float t=0;t<40;t+=1/60f){NativeCyberLordHud.Drawer(hv,t,100,0,cards,labels);NativeCyberLordHud.Drawer(v,t,5,0,cards,labels);}
                long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"the drawer allocates "+bytes+" bytes over 40 s at 60 Hz");
                facts.Add(states+" distinct card states over the live timeline");
            }

            // ---- 3. the menu text ----
            var cyber=data.Weapon(CyberLordSpec.Id);Check(cyber!=null&&cyber.devOnly,"the Cyber Lord's content entry (DEV only)");
            var shown=new List<(string what,string text)>();
            {
                string d=NativeLoadoutText.Description(cyber);shown.Add(("weapon description",d));shown.Add(("catalogue text",NativeLoadoutText.Text(cyber)));
                Check(d==NativeCyberLordText.Description&&NativeLoadoutText.Text(cyber)==d,"the menus' description is Kit v2's: "+d);
                foreach(int s in new[]{CyberKit2Spec.Q,CyberKit2Spec.E,CyberKit2Spec.MeleeLmb,CyberKit2Spec.Fork,CyberKit2Spec.Cut,CyberKit2Spec.Meteor,CyberKit2Spec.Panic,CyberKit2Spec.RangedLmb,CyberKit2Spec.Chain,CyberKit2Spec.Singularity,CyberKit2Spec.Storm,CyberKit2Spec.Orbital})
                    Check(d.Contains(CyberKit2Spec.Keys[s]+" "+CyberKit2Spec.Names[s]),"the description names "+CyberKit2Spec.Keys[s]+" "+CyberKit2Spec.Names[s]);
                Check(d.Contains("5 Reconfigure"),"the description names the key-5 switch");
                foreach(var w in data.weapons.Where(w=>w.id!=CyberLordSpec.Id))Check(NativeLoadoutText.Text(w)==w.description,"every other weapon's text stays verbatim ("+w.id+")");
                shown.Add(("tile strip",NativeLoadoutText.Role(cyber)));shown.Add(("stat line",NativeLoadoutText.Stats(cyber)));
                Check(NativeCyberLordText.Texts.Length==CyberKit2Spec.Slots&&NativeCyberLordText.Texts.All(x=>x.Length>20&&x.EndsWith(".")),"one ability line per v2 slot");
                var options=new List<NativeLocker.AbilityOption>();var inv=System.Globalization.CultureInfo.InvariantCulture;
                foreach(var (slot,k2) in new[]{("q",CyberKit2Spec.Q),("e",CyberKit2Spec.E),("1",CyberKit2Spec.Fork),("2",CyberKit2Spec.Cut),("3",CyberKit2Spec.Meteor),("4",-1)})
                {
                    int n=NativeLocker.SlotOptions(data,cyber,slot,options);var content=data.Ability(CyberLordSpec.Id,slot);Check(n==1&&options[0].Id==content.id,"the "+slot+" row keeps the content id ("+content.id+")");var o=options[0];
                    shown.Add(("Locker "+slot+" name",o.Name));shown.Add(("Locker "+slot+" text",o.Text));shown.Add(("Locker "+slot+" meta",o.Meta));
                    if(k2>=0)Check(o.Name==CyberKit2Spec.Names[k2]&&o.Text==NativeCyberLordText.Texts[k2]&&o.Icon==CyberKit2Spec.Icons[k2]&&o.Meta==CyberKit2Spec.Cooldowns[k2].ToString("0.#",inv)+" s cooldown · "+CyberKit2Spec.Costs[k2].ToString("0.#",inv)+" mana","the Locker's "+slot+" row: "+o.Name+" / "+o.Meta+" / "+o.Icon);
                    else Check(o.Name=="Kernel Panic"&&o.Text==content.text,"the Locker's 4 row stays Kernel Panic's (unchanged in v2)");
                }
                Check(NativeLocker.SlotOptions(data,data.Weapon("pistol"),"e",options)==1&&options[0].Name==data.Ability("pistol","e").name&&options[0].Text==data.Ability("pistol","e").text,"another weapon's rows stay content's");
                shown.Add(("entrance name",NativeIntroRules.StyleName(CyberLordSpec.Entrance)));shown.Add(("entrance text",NativeLocker.IntroText(CyberLordSpec.Entrance)));
                foreach(var tip in NativeStageLabels.GeneralTips.Concat(NativeStageLabels.BossTips).Concat(NativeStageLabels.ExpeditionTips).Concat(NativeStageLabels.SandboxTips))shown.Add(("loading tip",tip));
                foreach(StageLoadKind k in Enum.GetValues(typeof(StageLoadKind))){shown.Add(("loading eyebrow",NativeStageLabels.Eyebrow(k)));shown.Add(("loading mode",NativeStageLabels.Mode(k)));}
                shown.Add(("kernel panic cut-in",KernelPanicCutIn.Title));shown.Add(("kernel panic tag",KernelPanicCutIn.Tag));
                foreach(var (what,text) in shown)Check(Stale(text)==null,what+" shows no v1 name: "+text);
            }

            // ---- 4. the text scan: every string literal a player could read (Core, Runtime), and the Runtime's wiring ----
            {
                var allowed=new Dictionary<string,string>{
                    {"Core/NativeCyberLord.cs","public static readonly string[] Names={\"Data Dash\""},     // the v1 table v1's checks key on (not drawn)
                    {"Core/CyberLordRanged.cs","public static readonly string[] Names={\"Bit Bolt\""},      // the same, for v1's ranged set
                    {"Runtime/NativeDraftScreen.cs","\"ROOT ACCESS GRANTED\""}};                              // L33T's draft tier, not the Cyber Lord
                int literals=0;var hits=new List<string>();
                foreach(var dir in new[]{"Core","Runtime"})foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena",dir),"*.cs").OrderBy(f=>f,StringComparer.Ordinal))
                {
                    string rel=dir+"/"+Path.GetFileName(file),src=File.ReadAllText(file);var lines=src.Split('\n');
                    foreach(var (line,text) in Literals(src))
                    {
                        literals++;string stale=V1.FirstOrDefault(n=>text.Contains(n)||text.Contains(n.ToUpperInvariant()));if(stale==null)continue;
                        if(allowed.TryGetValue(rel,out var mark)&&line-1<lines.Length&&lines[line-1].Contains(mark))continue;hits.Add(rel+":"+line+" \""+text+"\" ("+stale+")");
                    }
                }
                Check(hits.Count==0,"no player-facing string literal names a v1 ability: "+string.Join("; ",hits));Check(literals>5000,"the scan read the sources' literals ("+literals+")");
                facts.Add(literals+" string literals scanned");
                string runtime=string.Join("\n",Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs").Select(File.ReadAllText));
                foreach(var table in new[]{"CyberLordSpec.Names","CyberRangedSpec.Names","NativeCyberLordHud.Labels","NativeCyberLordHud.RangedLabels","NativeCyberLordHud.RangedOf(","NativeCyberLordHud.Of("})Check(!runtime.Contains(table),"the Runtime draws no v1 table ("+table+")");
                Check(!System.Text.RegularExpressions.Regex.IsMatch(runtime,@"\bw\.description\b"),"the Runtime reads weapon descriptions through NativeLoadoutText only");
                var screen=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeCyberLordScreen.cs"));var hud=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeFightHud.cs"));
                Check(screen.Contains("NativeCyberLordHud.Drawer(v,arena.Time,p.Mana,p.KernelReady,cyberCards,cyberLabels)")&&screen.Contains("??(cyberFresh??=NativeCyberLordHud.Fresh())"),"the screen draws Kit v2's drawer, a fresh view without one");
                int route=hud.IndexOf("if(CyberLordFeed.Handles(p.Seat.spec))CyberLordCards(p);",StringComparison.Ordinal),content=hud.IndexOf("var d=content.Ability(p.Seat.spec,slot);",StringComparison.Ordinal);
                Check(route>0&&content>route&&hud.Substring(route,content-route).Contains("else foreach(var slot in new[]{\"q\",\"e\"})"),"the Cyber Lord's cards replace the content Q / E cards (its content names never reach the drawer)");
                var game=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/ConversionGame.cs"));Check(game.Contains("GUILayout.Label(NativeLoadoutText.Text(w),small);"),"the catalogue shows NativeLoadoutText.Text");
            }
        }
        finally{CyberLordFeed.Kit2MeleeSource=savedMelee;CyberLordFeed.Kit2RangedSource=savedRanged;CyberLordFeed.RangedSource=savedMode;CyberKit2Melee.Clones.Clear();}
        Console.WriteLine("PASS Cyber Lord v2 names: "+count+" checks (the drawer's v2 cards with no record in both modes, unchanged with the authority's live record, the menus' description and Locker rows, the text scan; "+string.Join("; ",facts)+")");
    }

    /// <summary>A C# file's string literals with their lines (comments and char literals skipped; verbatim and interpolated
    /// strings read as text).</summary>
    private static IEnumerable<(int line,string text)> Literals(string src)
    {
        int i=0,n=src.Length,line=1;
        while(i<n)
        {
            char c=src[i];
            if(c=='\n'){line++;i++;continue;}
            if(c=='/'&&i+1<n&&src[i+1]=='/'){int j=src.IndexOf('\n',i);i=j<0?n:j;continue;}
            if(c=='/'&&i+1<n&&src[i+1]=='*'){int j=src.IndexOf("*/",i+2,StringComparison.Ordinal);if(j<0)j=n-2;for(int k=i;k<j;k++)if(src[k]=='\n')line++;i=j+2;continue;}
            if(c=='\''){int j=i+1;while(j<n&&src[j]!='\'')j+=src[j]=='\\'?2:1;i=j+1;continue;}
            bool open=c=='"'||(c=='@'||c=='$')&&i+1<n&&(src[i+1]=='"'||(src[i+1]=='@'||src[i+1]=='$')&&i+2<n&&src[i+2]=='"');
            if(!open){i++;continue;}
            bool verbatim=false;int q=i;while(src[q]!='"'){if(src[q]=='@')verbatim=true;q++;}
            int start=line;var b=new StringBuilder();int e=q+1;
            while(e<n)
            {
                if(src[e]=='\n')line++;
                if(verbatim){if(src[e]=='"'){if(e+1<n&&src[e+1]=='"'){b.Append('"');e+=2;continue;}break;}}
                else{if(src[e]=='\\'&&e+1<n){b.Append(src[e]).Append(src[e+1]);e+=2;continue;}if(src[e]=='"')break;}
                b.Append(src[e]);e++;
            }
            yield return (start,b.ToString());i=e+1;
        }
    }
}
