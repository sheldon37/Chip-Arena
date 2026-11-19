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
/// counted and reported, never failed. `--voxel-ratchet-table` prints the counts in the table's format for re-pinning.
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
        var n=new int[Families.Length];
        for(int f=0;f<Families.Length;f++)foreach(var t in Tokens[f]){int at=0;while((at=text.IndexOf(t,at,StringComparison.Ordinal))>=0){n[f]++;at+=t.Length;}}
        return n;
    }
    private sealed class Table{public readonly Dictionary<string,int[]> Pins=new Dictionary<string,int[]>(StringComparer.Ordinal);public readonly HashSet<string> Style=new HashSet<string>(StringComparer.Ordinal);}
    private static Table Read(string root)
    {
        var t=new Table();var path=Path.Combine(root,Fixture);if(!File.Exists(path))throw new Exception("Voxel ratchet: "+Fixture+" is missing");
        int line=0;
        foreach(var raw in File.ReadAllLines(path))
        {
            line++;var s=raw;int hash=s.IndexOf('#');if(hash>=0)s=s.Substring(0,hash);s=s.Trim();if(s.Length==0)continue;
            var parts=s.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
            if(parts[0]=="style"&&parts.Length==2){if(!t.Style.Add(parts[1]))throw new Exception("Voxel ratchet: "+Fixture+":"+line+" lists the style file "+parts[1]+" twice");continue;}
            if(parts.Length!=1+Families.Length)throw new Exception("Voxel ratchet: "+Fixture+":"+line+" is not `path gate measure read author draw` or `style path`");
            var pin=new int[Families.Length];for(int f=0;f<pin.Length;f++)if(!int.TryParse(parts[1+f],out pin[f])||pin[f]<0)throw new Exception("Voxel ratchet: "+Fixture+":"+line+" has a bad count");
            if(t.Pins.ContainsKey(parts[0]))throw new Exception("Voxel ratchet: "+Fixture+":"+line+" pins "+parts[0]+" twice");t.Pins[parts[0]]=pin;
        }
        foreach(var s in t.Style)if(t.Pins.ContainsKey(s))throw new Exception("Voxel ratchet: "+s+" is both a style file and pinned");
        return t;
    }
    /// <summary>Every Core and Runtime file's counts, by its path under Assets/ChipArena ("Core/X.cs").</summary>
    public static SortedDictionary<string,int[]> Scan(string root)
    {
        var all=new SortedDictionary<string,int[]>(StringComparer.Ordinal);
        foreach(var dir in new[]{"Core","Runtime"})foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena",dir),"*.cs",SearchOption.TopDirectoryOnly))
            all[dir+"/"+Path.GetFileName(f)]=Count(File.ReadAllText(f));
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
            sb.AppendLine("Move the dependency onto the model, or, only with a \"Voxel dependency\" line in DEV_LOG saying why, raise the pin in "+Fixture+" to these lines:");
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
    }
    /// <summary>A table line for `path` at `n`.</summary>
    public static string Line(string path,int[] n)=>path.PadRight(56)+" "+string.Join(" ",n.Select(x=>x.ToString().PadLeft(3)));
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
