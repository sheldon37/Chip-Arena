using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// The voxel ratchet (owner 2026-11-18: "All the new things ive been making, I want to remove the voxel dependency and move forward
/// with a more detailed framing"; docs/VOXEL-STYLE.md; docs/VOXEL-RETIREMENT.md V0). It pins, per Core and Runtime file outside the
/// voxel style, the counts of the plan's five token families (voxel gates, voxel measures, rig reads, rig authoring, voxel-frame
/// draws) in Tests/voxel-ratchet.txt. A count above its pin fails, and a file the table does not list starts at zero, so new work
/// cannot add a voxel dependency by accident; the message names the model-native route. A package that removes dependencies lowers
/// its pins on purpose; a pin is raised only with a "Voxel dependency" line in DEV_LOG saying why. Tokens, not types: an
/// engine-free check cannot resolve C# types, so the text is counted as it stands (comments too), and new rig reads go through the
/// joint-frame resolver once V1 lands. The voxel style's own files (the table's `style` lines: docs/VOXEL-RETIREMENT.md 2.4) are
/// counted and reported, never failed. `--voxel-ratchet-table` prints the counts in the table's format; `--voxel-ratchet-repin`
/// prints, for a merged head, the line to paste for every file over its pin, with its tokens and a "Voxel dependency" note slot.
/// </summary>
internal static class VoxelRatchetChecks
{
    public const string Fixture="Tests/voxel-ratchet.txt";
    public static readonly string[] Families={"gate","measure","read","author","draw"};
    public static readonly string[][] Tokens=
    {
        new[]{"ClassicBodies","VisualBodiesOn"},
        new[]{"CapturedRig.Boxes","WoundedPilot("},
        new[]{"rig.Bones[","viewRig.Bones[","introRig","draftRig","hordeQueueRig","Rigs[","Player.Bones["},
        new[]{"new CapturedRig.Pose(","new MeleeRig(",".AddE(",".WritePose(","CapturedRig.Sample(",".Rotation[CapturedRig.",".Position[CapturedRig.",".P[CapturedRig.",".Q[CapturedRig."},
        new[]{"WeaponShape.Cube","QueueHeld(","Queue(\"weapon-","Queue(\"loose-","Queue(\"hat-","Queue(\"pilot","Queue(\"horde-","Queue(\"summon-"},
    };
    /// <summary>What new work does instead, per family (the failure's message).</summary>
    public static readonly string[] Routes=
    {
        "draw the model only: new content needs no Classic or no-bake branch (V1's ModelBody predicate replaces these gates; a voxel branch belongs to the voxel style)",
        "measure the model (its skinned bounds, the bake's rest skeleton, ModelSeat), not the voxel boxes",
        "read the drawn model's joints (VisualJointFrame and the drawn bones; V1's JointFrame resolver), not the 22-joint rig's",
        "author on the model's motion bank (the pack's clips through NativeVisualMotion, tools/motion), not a CapturedRig pose",
        "draw the model's own weapon (VisualWeapons, WeaponAt), a mesh bank or a modelled prop, not a voxel id or a WeaponShape.Cube piece",
    };
    /// <summary>The five family counts of one file's text.</summary>
    public static int[] Count(string text)
    {
        var per=TokenCounts(text);var n=new int[Families.Length];
        for(int f=0;f<Families.Length;f++)n[f]=per[f].Sum();
        return n;
    }
    /// <summary>Each family's count per token (Tokens' order) in one file's text: occurrences, no overlap.</summary>
    public static int[][] TokenCounts(string text)
    {
        var n=new int[Families.Length][];
        for(int f=0;f<Families.Length;f++){n[f]=new int[Tokens[f].Length];for(int k=0;k<Tokens[f].Length;k++){string t=Tokens[f][k];int at=0;while((at=text.IndexOf(t,at,StringComparison.Ordinal))>=0){n[f][k]++;at+=t.Length;}}}
        return n;
    }
    private sealed class Table{public readonly Dictionary<string,int[]> Pins=new Dictionary<string,int[]>(StringComparer.Ordinal);public readonly Dictionary<string,int> Lines=new Dictionary<string,int>(StringComparer.Ordinal);public readonly HashSet<string> Style=new HashSet<string>(StringComparer.Ordinal);}
    private static Table Read(string root)
    {
        var path=Path.Combine(root,Fixture);if(!File.Exists(path))throw new Exception("Voxel ratchet: "+Fixture+" is missing");
        return Parse(File.ReadAllLines(path));
    }
    private static Table Parse(IEnumerable<string> lines)
    {
        var t=new Table();int line=0;
        foreach(var raw in lines)
        {
            line++;var s=raw;int hash=s.IndexOf('#');if(hash>=0)s=s.Substring(0,hash);s=s.Trim();if(s.Length==0)continue;
            var parts=s.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
            if(parts[0]=="style"&&parts.Length==2){if(!t.Style.Add(parts[1]))throw new Exception("Voxel ratchet: "+Fixture+":"+line+" lists the style file "+parts[1]+" twice");continue;}
            if(parts.Length!=1+Families.Length)throw new Exception("Voxel ratchet: "+Fixture+":"+line+" is not `path gate measure read author draw` or `style path`");
            var pin=new int[Families.Length];for(int f=0;f<pin.Length;f++)if(!int.TryParse(parts[1+f],out pin[f])||pin[f]<0)throw new Exception("Voxel ratchet: "+Fixture+":"+line+" has a bad count");
            if(t.Pins.ContainsKey(parts[0]))throw new Exception("Voxel ratchet: "+Fixture+":"+line+" pins "+parts[0]+" twice");t.Pins[parts[0]]=pin;t.Lines[parts[0]]=line;
        }
        foreach(var s in t.Style)if(t.Pins.ContainsKey(s))throw new Exception("Voxel ratchet: "+s+" is both a style file and pinned");
        return t;
    }
    /// <summary>Every Core and Runtime file's counts, by its path under Assets/ChipArena ("Core/X.cs"; in a subfolder "Runtime/Sub/X.cs").</summary>
    public static SortedDictionary<string,int[]> Scan(string root)=>Scan(root,Count);
    private static SortedDictionary<string,T> Scan<T>(string root,Func<string,T> count)
    {
        var all=new SortedDictionary<string,T>(StringComparer.Ordinal);
        foreach(var dir in new[]{"Core","Runtime"})
        {
            var top=Path.Combine(root,"Assets/ChipArena",dir);
            foreach(var f in Directory.GetFiles(top,"*.cs",SearchOption.AllDirectories))all[dir+"/"+Path.GetRelativePath(top,f).Replace(Path.DirectorySeparatorChar,'/')]=count(File.ReadAllText(f));
        }
        return all;
    }
    public static void Run(string root)
    {
        SelfTest();
        var table=Read(root);var scan=Scan(root);var sb=new StringBuilder();
        var fails=Evaluate(table,scan,out int pinned,out int slackFiles,out int slack,out int styleFiles,out var totals,out var styleTotals);
        foreach(var s in table.Style)if(!scan.ContainsKey(s))throw new Exception("Voxel ratchet: the style file "+s+" is gone: the voxel style is kept (docs/VOXEL-STYLE.md); restore it or take its line out on purpose");
        int away=table.Pins.Keys.Count(k=>!scan.ContainsKey(k));
        if(fails.Count>0)
        {
            sb.AppendLine("Voxel ratchet: "+fails.Count+" count(s) above "+Fixture+" (docs/VOXEL-STYLE.md: new work is authored and socketed on the model):");
            foreach(var f in fails)sb.AppendLine(f);
            sb.AppendLine("Move the dependency onto the model, or, only with a \"Voxel dependency\" line in DEV_LOG saying why, raise the pin in "+Fixture+" to these lines"
                +" (`dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --voxel-ratchet-repin` prints them with their tokens, the line each replaces and the DEV_LOG lines):");
            foreach(var pair in scan)if(!table.Style.Contains(pair.Key)&&fails.Any(x=>x.StartsWith("  "+pair.Key+":",StringComparison.Ordinal)))sb.AppendLine("  "+Line(pair.Key,pair.Value));
            throw new Exception(sb.ToString());
        }
        Console.WriteLine("PASS voxel ratchet: "+scan.Count+" Core and Runtime files at or under "+Fixture+" ("+pinned+" pinned, the rest at zero; "+string.Join(", ",Families.Select((f,i)=>f+" "+totals[i]))+"); "
            +styleFiles+" voxel-style files counted ("+string.Join(", ",Families.Select((f,i)=>f+" "+styleTotals[i]))+"); "+slackFiles+" file(s) under their pins by "+slack+" (lower them on purpose); "+away+" pin(s) for files in flight elsewhere");
    }
    // Each count against its pin: above fails (a file the table does not list at zero), the style files only counted.
    private static List<string> Evaluate(Table table,IDictionary<string,int[]> scan,out int pinned,out int slackFiles,out int slack,out int styleFiles,out int[] totals,out int[] styleTotals)
    {
        var fails=new List<string>();pinned=slackFiles=slack=styleFiles=0;totals=new int[Families.Length];styleTotals=new int[Families.Length];
        foreach(var pair in scan)
        {
            var n=pair.Value;
            if(table.Style.Contains(pair.Key)){styleFiles++;for(int f=0;f<n.Length;f++)styleTotals[f]+=n[f];continue;}
            table.Pins.TryGetValue(pair.Key,out var pin);pin??=new int[Families.Length];bool low=false;if(table.Pins.ContainsKey(pair.Key))pinned++;
            for(int f=0;f<n.Length;f++)
            {
                totals[f]+=n[f];
                if(n[f]>pin[f])fails.Add("  "+pair.Key+": "+Families[f]+" "+n[f]+" > pin "+pin[f]+" ("+string.Join(" ",Tokens[f])+"). New work: "+Routes[f]+".");
                else if(n[f]<pin[f]){low=true;slack+=pin[f]-n[f];}
            }
            if(low)slackFiles++;
        }
        return fails;
    }
    // The ratchet's own rule on a made-up tree: a count over its pin and a new file's first token fail, a style file and a lowered
    // count do not, and the counting is the plan's (tokens in the text, comments included, no overlap).
    private static void SelfTest()
    {
        var t=new Table();t.Pins["Core/A.cs"]=new[]{0,0,1,0,0};t.Pins["Core/Low.cs"]=new[]{2,0,0,0,0};t.Style.Add("Core/Style.cs");
        var scan=new Dictionary<string,int[]>
        {
            ["Core/A.cs"]=Count("var m=rig.Bones[3];var n=rig.Bones[4];"),["Core/New.cs"]=Count("// the rig's ClassicBodies gate\nif(ClassicBodies)return;"),
            ["Core/Low.cs"]=Count("if(VisualBodiesOn)x();"),["Core/Style.cs"]=Count("new CapturedRig.Pose();p.Rotation[CapturedRig.Head]=q;"),["Core/Clean.cs"]=Count("VisualJointFrame(a,j)"),
        };
        var fails=Evaluate(t,scan,out int pinned,out int slackFiles,out int slack,out int styleFiles,out var totals,out _);
        bool ok=fails.Count==2&&fails[0].StartsWith("  Core/A.cs: read 2 > pin 1",StringComparison.Ordinal)&&fails[0].Contains("VisualJointFrame")&&fails[1].StartsWith("  Core/New.cs: gate 2 > pin 0",StringComparison.Ordinal)
            &&pinned==2&&slackFiles==1&&slack==1&&styleFiles==1&&totals[3]==0&&Count(".P[CapturedRig.Head].Q[CapturedRig.Head]viewRig.Bones[0]")[3]==2&&Count("viewRig.Bones[0]")[2]==1;
        if(!ok)throw new Exception("Voxel ratchet: its own rule failed on the made-up tree ("+string.Join(" | ",fails)+")");
        // The re-pin on the same tree: a line for each file over its pin (the pinned one's line named, the new one added), its tokens,
        // the note slot; the table with those lines pasted passes, and the style file and the files at their pins print nothing.
        var table=Parse(new[]{"style Core/Style.cs","Core/A.cs 0 0 1 0 0","Core/Low.cs 2 0 0 0 0"});
        var tokens=new Dictionary<string,int[][]>{["Core/A.cs"]=TokenCounts("var m=rig.Bones[3];var n=rig.Bones[4];"),["Core/New.cs"]=TokenCounts("// the rig's ClassicBodies gate\nif(ClassicBodies)return;"),
            ["Core/Low.cs"]=TokenCounts("if(VisualBodiesOn)x();"),["Core/Style.cs"]=TokenCounts("new CapturedRig.Pose();p.Rotation[CapturedRig.Head]=q;"),["Core/Clean.cs"]=TokenCounts("VisualJointFrame(a,j)")};
        var o=new StringWriter();int over=Repin(table,tokens,o);var text=o.ToString();
        var paste=text.Split('\n').Select(l=>l.TrimEnd('\r')).Where(l=>l.Contains("   # Voxel dependency: ")).ToArray();
        var pasted=Parse(new[]{"style Core/Style.cs","Core/Low.cs 2 0 0 0 0"}.Concat(paste));
        var after=Evaluate(pasted,tokens.ToDictionary(p=>p.Key,p=>p.Value.Select(x=>x.Sum()).ToArray()),out _,out _,out _,out _,out _,out _);
        bool repin=over==2&&paste.Length==2&&paste[0].StartsWith(Line("Core/A.cs",new[]{0,0,2,0,0}),StringComparison.Ordinal)&&paste[1].StartsWith(Line("Core/New.cs",new[]{2,0,0,0,0}),StringComparison.Ordinal)
            &&text.Contains("Core/A.cs: replaces line 2 (pinned 0 0 1 0 0)")&&text.Contains("read 1 -> 2: rig.Bones[ x2.")&&text.Contains("Core/New.cs: add (not listed")&&text.Contains("gate 0 -> 2: ClassicBodies x2.")
            &&text.Contains("- `Core/New.cs` gate 0 -> 2: <why")&&!text.Contains("Core/Style.cs")&&!text.Contains("Core/Low.cs")&&!text.Contains("Core/Clean.cs")&&after.Count==0
            &&Repin(pasted,tokens,new StringWriter())==0;
        if(!repin)throw new Exception("Voxel ratchet: the re-pin's lines failed on the made-up tree ("+over+" over; "+string.Join(" | ",after)+")\n"+text);
    }
    /// <summary>A table line for `path` at `n`.</summary>
    public static string Line(string path,int[] n)=>path.PadRight(56)+" "+string.Join(" ",n.Select(x=>x.ToString().PadLeft(3)));
    /// <summary>
    /// --voxel-ratchet-repin (docs/VOXEL-STYLE.md "Re-pinning"): for the tree as it stands (a merged head), every file over its pin with
    /// the table line to paste (today's counts, so its families under their pins come down with it), the line of Tests/voxel-ratchet.txt
    /// it replaces (or "add" for a file the table does not list), the tokens behind each raised family, a "Voxel dependency" note slot on
    /// the line, and the DEV_LOG lines to fill in. It prints only: nothing is written, so no pin moves without someone saying why.
    /// Returns the number of files over their pins.
    /// </summary>
    public static int Repin(string root,TextWriter o)=>Repin(Read(root),Scan(root,TokenCounts),o);
    private static int Repin(Table table,IDictionary<string,int[][]> scan,TextWriter o)
    {
        var over=new List<(string path,int[] now,int[] pin,int line,int[][] tokens)>();
        foreach(var pair in scan)
        {
            if(table.Style.Contains(pair.Key))continue;
            var now=pair.Value.Select(x=>x.Sum()).ToArray();bool listed=table.Pins.TryGetValue(pair.Key,out var pin);pin??=new int[Families.Length];
            if(now.Where((n,f)=>n>pin[f]).Any())over.Add((pair.Key,now,pin,listed?table.Lines[pair.Key]:0,pair.Value));
        }
        if(over.Count==0){o.WriteLine("Voxel ratchet re-pin: every Core and Runtime file is at or under its pin in "+Fixture+"; nothing to paste.");return 0;}
        const string Slot="<why it still reads the voxel layer; the package that moves it onto the model>";
        o.WriteLine("Voxel ratchet re-pin: "+over.Count+" file(s) over their pins in "+Fixture+" (docs/VOXEL-STYLE.md \"Re-pinning\").");
        o.WriteLine("Move a dependency onto the model where you can (each family's route is below). Raise a pin only for one kept on purpose, its note filled in.");
        o.WriteLine();
        o.WriteLine("1. "+Fixture+": each line replaces the one named below, or goes in the \"In flight\" block at the end for a file the table does not list:");
        o.WriteLine();
        foreach(var x in over)o.WriteLine(Line(x.path,x.now)+"   # Voxel dependency: "+Slot);
        o.WriteLine();
        foreach(var x in over)
        {
            o.WriteLine("   "+x.path+": "+(x.line>0?"replaces line "+x.line+" (pinned "+string.Join(" ",x.pin)+")":"add (not listed, so pinned 0 0 0 0 0)"));
            for(int f=0;f<Families.Length;f++)if(x.now[f]>x.pin[f])
                o.WriteLine("     "+Families[f]+" "+x.pin[f]+" -> "+x.now[f]+": "+string.Join(", ",Tokens[f].Select((t,k)=>(t,n:x.tokens[f][k])).Where(p=>p.n>0).Select(p=>p.t+" x"+p.n))+". Model-native route: "+Routes[f]+".");
        }
        o.WriteLine();
        o.WriteLine("2. The merge's DEV_LOG entry, under \"Voxel dependency\":");
        o.WriteLine();
        foreach(var x in over)
            o.WriteLine("- `"+x.path+"` "+string.Join(", ",Enumerable.Range(0,Families.Length).Where(f=>x.now[f]>x.pin[f]).Select(f=>Families[f]+" "+x.pin[f]+" -> "+x.now[f]))+": "+Slot+".");
        return over.Count;
    }
    /// <summary>--voxel-ratchet-table: every file with a count, in the table's format (style files marked), for re-pinning on purpose.</summary>
    public static void Print(string root)
    {
        Table table=null;try{table=Read(root);}catch(Exception){}
        foreach(var pair in Scan(root))
        {
            if(pair.Value.Sum()==0)continue;bool style=table!=null&&table.Style.Contains(pair.Key);
            Console.WriteLine((style?"# style  ":"")+Line(pair.Key,pair.Value)+(table!=null&&!style&&table.Pins.TryGetValue(pair.Key,out var pin)&&!pin.SequenceEqual(pair.Value)?"   # pinned "+string.Join(" ",pin):""));
        }
    }
}
