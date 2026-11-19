using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// The voxel style stays kept, checked and loadable (owner 2026-11-18: "remove voxel entirely BUT keep its data incase I use it for a
/// map or game mode"; docs/VOXEL-STYLE.md; docs/VOXEL-RETIREMENT.md 2.4, V0's first half of VoxelStyleChecks). Every kept data file
/// and generated table is byte-identical to its pinned digest (SHA-256 with carriage returns taken out, so a CRLF checkout hashes as
/// the LF repository does), and, engine-free: every Electron model in SourceModels and HazardModels passes the bank's own checks;
/// every SourceMotion, SceneMotion and SirenLook clip is whole and finite with unit joint rotations; the plain files parse at their
/// export commit; every voxel body skin and every horde kind's look builds within its budget at every tier; and every rig clip poses
/// at its start, middle and end with the rig's bone lengths kept. The second half (a map with the voxel keys playing on host and
/// guest) comes with V12. A digest moves only when the owner commissions new voxel data; nothing here is ever deleted.
/// </summary>
internal static class VoxelStyleChecks
{
    public const string Commit="05950eaef80ff2483a9c44d898cad98be1a76bbc";
    private static readonly (string path,string sha)[] Kept=
    {
        ("Assets/Conversion/Resources/SourceModels.json","b256a94fb2c97c7542c68613b676e513433f099816be2cbf58a80c1d254b759c"),
        ("Assets/Conversion/Resources/SourceMotion.json","aaa7c3afc447ec12779a0f66119b237ac2d39aa649cdbcdad3ef7fea1846e8da"),
        ("Assets/Conversion/Resources/SceneMotion.json","37cb40ea9e9f33cf50ecbb84f4da031496fe573cd5d75a3c7151f632b4b7c60f"),
        ("Assets/Conversion/Resources/WoundParts.json","f3edd39a7799b602e6882c701a6d68cb4e5d4d813af7f2d9bf9e8f7a10ccfe47"),
        ("Assets/Conversion/Resources/HazardModels.json","81d704765f02c39d084722d015b8303b9ecfa786a36f2dff31195ee84f3d601c"),
        ("Assets/Conversion/Resources/SceneryMotion.json","2618ca66c76572ff1cd4a0700604a6fb7bf4740bb4eeeda6acf755281df1ddcd"),
        ("Assets/Conversion/Resources/WardMotion.json","b76c0b4e7429ab2de291e61b7a1a5e151f349fea0192c1b36990d74d4698b17d"),
        ("Assets/Conversion/Resources/SlenderRig.json","ac0f833dfa8270b1085ef0e833d12cc90e323c53e19cd14adfa333bbe9e2e5d3"),
        ("Assets/Conversion/Resources/SirenLook.json","54b71e3609450c0dfa12170b7faa0e9d12944fa3ee021d158042d1965843b8b4"),
        ("Assets/ChipArena/Core/CapturedClips.Generated.cs","413565fa351e5bbad9e4554170e923a8b33170db9024f2d91b1623c2f753fdd0"),
        ("Assets/ChipArena/Core/NativeKillBoxes.Generated.cs","463040f3b66d3efd9a997a28abac93382e96bf46801a7e947eda79fdf7bc871f"),
        ("Assets/ChipArena/Core/NativeBroadcastEdges.Generated.cs","4c7cc0cb1f0c743e2bc270b6d921adb256cbea46daa2d56e310dc0069d28f28f"),
        ("Assets/ChipArena/Core/NativePackAirAttacks.Generated.cs","ea471d6c2d768211b2c890fe4f44a020fcd6622f7ef7bd87f1b8417b8338b37b"),
        ("Assets/ChipArena/Core/NativePackBow.Generated.cs","fc416f79690153cf732b9bcaf2a419fc4aa413dea41004c47b822a83de64e224"),
        ("Assets/ChipArena/Core/NativePackClips.Generated.cs","44136614d8bf71890335952cc014071708f4f633e6ef6da5a5d1810a97080d1e"),
        ("Assets/ChipArena/Core/NativePackCyberLord.Generated.cs","da73ceccd5f0673de1496493df00bbadb8afa349256d24e6b1c26b0e247cfca0"),
        ("Assets/ChipArena/Core/NativePackCyberLordRun.Generated.cs","442928b115da6bf0924af741a0fd7567f95b89cab163034233bb61b2776b346a"),
        ("Assets/ChipArena/Core/NativePackIntros.Generated.cs","344d150c03a02e2abe363fba776f0798f439b10fb5cfe8d18f2b06b3a25530e6"),
        ("Assets/ChipArena/Core/NativePackJumps.Generated.cs","60fc72a2baf8c1368e56d9b83d891f3393d8f1ae2401005de8a6a204b0dd8594"),
        ("Assets/ChipArena/Core/NativePackLocomotion.Generated.cs","97c43b2a84b6d95dbde703408f1f771b123a175bc005096e2163cf7168d8543c"),
        ("Assets/ChipArena/Core/NativePackRangedLocomotion.Generated.cs","11118ec49afff50fc8f6798a64a7dfbe5b794663d6beb23735d802604b62f319"),
        ("Assets/ChipArena/Core/NativePackSprintAttacks.Generated.cs","afd0fa8282f89751f59364e8f98e97409ac5dd50b60552db6e94e72a3c800fe3"),
        ("Assets/ChipArena/Core/NativePackZombie.Generated.cs","b525fc93234b589b15fb010952052fd62816d8ae6bed02f7a3e67f6e9c0d6ef6"),
    };
    private static int checks;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Voxel style: "+what);checks++;}
    /// <summary>A file's SHA-256 with every CRLF read as LF.</summary>
    public static string Digest(string file)
    {
        var b=File.ReadAllBytes(file);var o=new byte[b.Length];int n=0;
        for(int i=0;i<b.Length;i++){if(b[i]==13&&i+1<b.Length&&b[i+1]==10)continue;o[n++]=b[i];}
        using var sha=SHA256.Create();return string.Concat(sha.ComputeHash(o,0,n).Select(x=>x.ToString("x2")));
    }
    private static JsonDocument Envelope(string root,string name)
    {
        using var e=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name+".json")));
        Check(e.RootElement.GetProperty("schema").GetInt32()==1&&e.RootElement.GetProperty("commit").GetString()==Commit,name+" is the Electron export at "+Commit[..8]);
        using var bytes=new MemoryStream(Convert.FromBase64String(e.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
        return JsonDocument.Parse(reader.ReadToEnd(),new JsonDocumentOptions{MaxDepth=256});
    }
    public static void Run(string root)
    {
        checks=0;
        // 1. Byte-identical: every kept data file and generated table.
        foreach(var (path,sha) in Kept){var f=Path.Combine(root,path);Check(File.Exists(f),path+" is kept");Check(Digest(f)==sha,path+" is byte-identical to its pinned digest (the voxel style's data never changes by accident)");}
        // 2. The Electron models load as the bank loads them (Runtime/NativeSourceModels.cs: the sections' arrays agree, the indices in
        //    range, three textures with whole base images; the hazards carry none).
        int models=0,sections=0;long vertices=0;
        foreach(var name in new[]{"SourceModels","HazardModels"})
        {
            using var doc=Envelope(root,name);var r=doc.RootElement;var ids=new System.Collections.Generic.HashSet<string>();
            foreach(var m in r.GetProperty("models").EnumerateArray())
            {
                string id=m.GetProperty("id").GetString();Check(ids.Add(id),name+": "+id+" once");models++;
                foreach(var s in m.GetProperty("sections").EnumerateArray())
                {
                    int count=s.GetProperty("positions").GetArrayLength()/3;sections++;if(count==0)continue;vertices+=count;
                    Check(s.GetProperty("normals").GetArrayLength()==count*3&&s.GetProperty("colors").GetArrayLength()==count*4&&s.GetProperty("emission").GetArrayLength()==count*4&&s.GetProperty("uv").GetArrayLength()==count*2,name+": "+id+"'s section arrays agree");
                    bool inRange=true;foreach(var i in s.GetProperty("indices").EnumerateArray()){int v=i.GetInt32();if(v<0||v>=count){inRange=false;break;}}
                    Check(inRange&&s.GetProperty("indices").GetArrayLength()%3==0,name+": "+id+"'s triangles index its own vertices");
                    if(name=="HazardModels")Check(s.GetProperty("texture").GetInt32()<0,"a hazard model references no texture");
                }
            }
            var textures=r.GetProperty("textures");
            if(name=="SourceModels")
            {
                Check(ids.Count==140&&new[]{"pilot","siren","slender"}.All(ids.Contains)&&ids.Count(i=>i.StartsWith("weapon-"))==14&&ids.Count(i=>i.StartsWith("loose-"))==14&&ids.Count(i=>i.StartsWith("hat-"))==10&&ids.Count(i=>i.StartsWith("map-"))==15&&ids.Count(i=>i.StartsWith("stage-"))==20&&ids.Count(i=>i.StartsWith("death-"))==11,"SourceModels holds its 140 models: the pilot, the Siren, the Slenderman, the 14 Prototype weapons and their loose copies, the 10 hats, the 15 maps, the 20 entrance stages and the 11 death scenes");
                Check(textures.GetArrayLength()==3&&textures.EnumerateArray().All(t=>t.GetProperty("channels").GetInt32()==4&&Convert.FromBase64String(t.GetProperty("pixels").GetString()).LongLength==(long)t.GetProperty("width").GetInt32()*t.GetProperty("height").GetInt32()*4),"its three textures have whole base images");
            }
            else Check(textures.GetArrayLength()==0&&ids.Count==10,"HazardModels holds its 10 models and no texture");
        }
        // 3. The motion: every clip whole (frames x its channels), finite, its joint rotations unit length.
        static bool Finite(JsonElement values){foreach(var v in values.EnumerateArray()){double d=v.GetDouble();if(double.IsNaN(d)||double.IsInfinity(d))return false;}return true;}
        int motionClips=0;
        using(var doc=Envelope(root,"SourceMotion"))
        {
            var clips=doc.RootElement.GetProperty("clips");Check(clips.GetArrayLength()==165,"SourceMotion holds its 165 rig clips");
            foreach(var c in clips.EnumerateArray())
            {
                int frames=c.GetProperty("frames").GetInt32();var v=c.GetProperty("values");string key=c.GetProperty("key").GetString();motionClips++;
                Check(frames>0&&v.GetArrayLength()==frames*22*7&&Finite(v),key+" is whole and finite (22 joints x position and rotation a frame)");
                var a=v.EnumerateArray().Select(x=>(float)x.GetDouble()).ToArray();bool unit=true;
                foreach(int f in new[]{0,frames/2,frames-1})for(int j=0;j<22;j++){int o=(f*22+j)*7+3;var q=new Quaternion(a[o],a[o+1],a[o+2],a[o+3]);if(Math.Abs(q.Length()-1)>.01f)unit=false;}
                Check(unit,key+"'s joint rotations are unit quaternions");
            }
        }
        using(var doc=Envelope(root,"SceneMotion"))
        {
            var clips=doc.RootElement.GetProperty("clips");Check(clips.GetArrayLength()==31,"SceneMotion holds its 31 scenes (11 deaths, 20 entrance stages)");
            foreach(var c in clips.EnumerateArray()){int frames=c.GetProperty("frames").GetInt32(),tracks=c.GetProperty("tracks").GetInt32();var v=c.GetProperty("values");motionClips++;Check(frames>0&&tracks>0&&v.GetArrayLength()==frames*tracks*20&&Finite(v)&&Finite(c.GetProperty("victim")),c.GetProperty("key").GetString()+" is whole and finite");}
        }
        using(var doc=Envelope(root,"SirenLook"))
        {
            var clips=doc.RootElement.GetProperty("clips");Check(clips.GetArrayLength()==23&&doc.RootElement.GetProperty("fps").GetInt32()>0,"SirenLook holds its 23 clips");
            foreach(var c in clips.EnumerateArray()){int frames=c.GetProperty("frames").GetInt32();var v=c.GetProperty("values");motionClips++;Check(frames>1&&v.GetArrayLength()==frames*2&&Finite(v),c.GetProperty("key").GetString()+" is whole and finite");}
        }
        foreach(var (name,list) in new[]{("WoundParts","parts"),("SceneryMotion","maps"),("WardMotion","parts"),("SlenderRig","nodes")})
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name+".json")));
            Check(doc.RootElement.GetProperty("commit").GetString()==Commit&&doc.RootElement.GetProperty(list).GetArrayLength()>0,name+" parses at the export commit");
        }
        // 4. The voxel sculpts build engine-free within their budgets: the Standard and Sheldon voxel bodies (the Prototype is
        //    SourceModels' pilot, above) and every horde kind's look, at every tier.
        int sculpts=0;
        foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex})for(int t=0;t<NativeBodySkins.Tiers;t++)
        {var b=NativeBodyModels.Build(skin,t);sculpts++;Check(b.Triangles>0&&b.Triangles<=NativeBodySkins.Budget[t]&&b.Parts.Length==NativeBodySkins.Parts.Length,NativeBodySkins.Model(skin,t)+" builds every part within "+NativeBodySkins.Budget[t]+" triangles");}
        for(int k=0;k<NativeHordeKinds.Count;k++)for(int l=0;l<NativeHordeKinds.Looks[k];l++)for(int t=0;t<NativeHordeModels.Tiers;t++)
        {var m=NativeHordeModels.Build(k,l,t);sculpts++;Check(m.Triangles>0&&m.Triangles<=NativeHordeModels.Budget[t],NativeHordeModels.Id(k,l,t)+" builds within "+NativeHordeModels.Budget[t]+" triangles");}
        // 5. Every rig clip poses: its start, middle and end, finite, the rig's bone lengths kept.
        var row=new float[64];var pose=new CapturedRig.Pose();int rigClips=0;
        for(int set=0;set<CapturedClips.Sets.Length;set++)foreach(var clip in CapturedClips.Sets[set])
        {
            rigClips++;
            foreach(float at in new[]{0,clip.Duration*.5f,clip.Duration})
            {
                clip.Sample(at,row);CapturedRig.Sample(row,set,pose);pose.World();bool ok=true;
                for(int b=0;b<CapturedRig.Bones.Length;b++)
                {
                    var p=pose.WorldPosition[b];if(float.IsNaN(p.X)||float.IsNaN(p.Y)||float.IsNaN(p.Z)||p.Length()>4)ok=false;
                    int parent=CapturedRig.Bones[b].Parent;if(parent>=0&&Math.Abs(Vector3.Distance(p,pose.WorldPosition[parent])-CapturedRig.Bones[b].Position.Length())>.0005f)ok=false;
                }
                Check(ok,clip.Name+" poses at "+at.ToString("0.00")+" s");
            }
        }
        Console.WriteLine("PASS voxel style kept and loadable: "+Kept.Length+" data files and generated tables byte-identical; "+models+" Electron models ("+sections+" sections, "+vertices+" vertices) load as the bank loads them; "
            +motionClips+" motion clips whole and finite; "+sculpts+" voxel sculpts build within budget; "+rigClips+" rig clips pose; "+checks+" checks");
    }
}
