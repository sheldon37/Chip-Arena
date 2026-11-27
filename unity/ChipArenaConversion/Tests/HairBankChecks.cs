using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ChipArena.UnityConversion;
// The hair bank (Core/NativeHairBank.cs; owner 2026-11-26: "Lets add a lot more hairstyles, some anime ones as well ... Add magica
// cloth into these"; docs/PILOT-BODY-HAIR.md section 3): the reader takes a well-formed bank whole and refuses a malformed one;
// the new hair ids follow the account service's seven and keep this PC's pick until the service lists them; the presentation
// merges the bank's styles into the look data and builds the worn one before dressing; the tool writes what the reader reads.
internal static class HairBankChecks
{
    public static void Run(string root)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Hair bank: "+why);}
        // ---- the reader ----
        var good=Bank();var bank=NativeHairBank.Read(good);
        Check(bank!=null,"a well-formed bank is read");
        Check(bank.Textures.Length==1&&bank.Textures[0].Name=="t"&&bank.Textures[0].Png.Length==3,"its texture");
        Check(bank.Pieces.Length==1&&bank.Pieces[0].Renderer=="HairBack.Bank_Test"&&bank.Pieces[0].VertexCount==3&&bank.Pieces[0].Triangles.Length==3,"its piece");
        var p=bank.Pieces[0];var at=p.HeadPoint(1);
        Check(p.Bones.Length==2&&p.Bones[1].Parent==0&&Math.Abs(at[0]-.1f)<1e-6&&Math.Abs(at[1]-.3f)<1e-6&&Math.Abs(at[2]+.2f)<1e-6,"a bone's head point sums its chain's offsets");
        Check(p.Shapes.Length==1&&p.Shapes[0].Name=="Tag_Hat"&&p.Shapes[0].Vertices.Length==1&&p.Shapes[0].Vertices[0]==2,"its tag shape (sparse)");
        Check(bank.Has("test-style")&&!bank.Has("spiky")&&!bank.Has(null)&&bank.StyleOrder.Count==1&&bank.Styles["test-style"][0]==0,"its style");
        // version 2: the same body deflated reads the same; a corrupt body, or one with bytes after it, is refused
        var packed=Deflated(good);var bank2=NativeHairBank.Read(packed);
        Check(bank2!=null&&bank2.Pieces.Length==1&&bank2.Pieces[0].VertexCount==3&&bank2.Has("test-style"),"a deflated bank reads as its plain body");
        var torn=(byte[])packed.Clone();for(int i=12;i<torn.Length;i++)torn[i]^=0x5a;Check(NativeHairBank.Read(torn)==null,"a corrupt deflated body is refused");
        Check(NativeHairBank.Read(Deflated(good,new byte[3]))==null,"a deflated body with trailing bytes is refused");
        // malformed: refused whole
        Check(NativeHairBank.Read(null)==null&&NativeHairBank.Read(new byte[3])==null,"too short");
        var bad=(byte[])good.Clone();bad[0]=(byte)'X';Check(NativeHairBank.Read(bad)==null,"a wrong magic");
        bad=(byte[])good.Clone();bad[4]=2;Check(NativeHairBank.Read(bad)==null,"a plain body marked deflated");
        bad=(byte[])good.Clone();bad[4]=3;Check(NativeHairBank.Read(bad)==null,"a version this reader does not know");
        Check(NativeHairBank.Read(good.AsSpan(0,good.Length-1).ToArray())==null,"truncated");
        var longer=new byte[good.Length+1];good.CopyTo(longer,0);Check(NativeHairBank.Read(longer)==null,"trailing bytes");
        Check(NativeHairBank.Read(Bank(parent:1))==null,"a bone whose parent comes after it");
        Check(NativeHairBank.Read(Bank(tri:3))==null,"a triangle past the vertices");
        Check(NativeHairBank.Read(Bank(weight:1.5f))==null,"a weight over one");
        Check(NativeHairBank.Read(Bank(boneIndex:3))==null,"a weight on a bone the piece lacks");
        Check(NativeHairBank.Read(Bank(renderer:"Top.Bank_Test"))==null,"a piece that is not hair");
        Check(NativeHairBank.Read(Bank(bone:"Spine_x"))==null,"a bone that is not a bank chain's");
        Check(NativeHairBank.Read(Bank(nan:true))==null,"a position that is not finite");
        Check(NativeHairBank.Read(Bank(style:5))==null,"a style naming a piece the bank lacks");
        // chain names: kind and group
        Check(NativeHairBank.ChainOf("Hair_BC03_LongStreight_02_0",out char k,out int g)&&k==NativeHairBank.Curtain&&g==3,"a chain root's kind and group");
        Check(!NativeHairBank.ChainOf("Hair_Back",out _,out _)&&!NativeHairBank.ChainOf("Hair_BX01_a",out _,out _)&&!NativeHairBank.ChainOf("Hair_BSa1_a",out _,out _)&&!NativeHairBank.ChainOf(null,out _,out _),"other bones are not bank chains");

        // ---- the hair ids ----
        var hair=NativeLook.Ids[0];var labels=NativeLook.Labels[0];
        Check(hair.Length==labels.Length&&hair.Length>=NativeLook.ServiceKnownHair+16,"the new styles listed with labels");
        Check(string.Join(",",hair,0,NativeLook.ServiceKnownHair)=="spiky,wild,side-swept,tied-back,fringe-tail,bob,high-ponytail","the service's seven first, unchanged");
        Check(new HashSet<string>(hair).Count==hair.Length,"no id twice");
        foreach(var id in new[]{"raven-spikes","wolf","mane","twin-tails","hime","long-straight","lob"})Check(Array.IndexOf(hair,id)>=NativeLook.ServiceKnownHair,"listed: "+id);
        Check(NativeLook.Normalize("hair:raven-spikes,body:female")=="hair:raven-spikes,body:female"&&NativeLook.Gate("hair:twin-tails",false)=="hair:twin-tails","a new style is a public pick");
        // the account service drops an id it does not list: this PC's pick stays while the account's look has the default hair
        Check(NativeLook.KeepSkin("","hair:raven-spikes,skin:c48a62",4)=="hair:raven-spikes,skin:c48a62","schema 4 keeps this PC's new style");
        Check(NativeLook.KeepSkin("hair:bob","hair:raven-spikes",4)=="hair:bob","the account's own pick wins");
        Check(NativeLook.KeepSkin("","hair:raven-spikes",NativeLook.ServiceKeepsHairStyles)=="","a service that keeps them is the truth");
        Check(NativeLook.KeepSkin("","hair:bob",4)=="","the service's seven are its own from schema 4");

        // ---- the presentation (source) ----
        string Src(string rel)=>File.ReadAllText(Path.Combine(root,rel));
        var pres=Src("Assets/ChipArena/Runtime/NativeHairBankPresentation.cs");var body=Src("Assets/ChipArena/Runtime/NativeVisualBodyPresentation.cs");
        Check(body.Contains("if(lookState>0)VisualLookAddHair(lookData);"),"the bank's styles join the look data when it is read");
        int dress=body.IndexOf("private static void VisualDress(",StringComparison.Ordinal),built=body.IndexOf("VisualHairBankDress(root,look);",dress,StringComparison.Ordinal),named=body.IndexOf("lookNamed.Clear();foreach(var r in root.GetComponentsInChildren<Renderer>(true))lookNamed[r.name]=r;",dress,StringComparison.Ordinal);
        Check(dress>0&&built>dress&&named>built,"the worn style is built before the dressing reads the instance");
        Check(pres.Contains("Resources.Load<TextAsset>(NativeHairBank.Resource)")&&pres.Contains("NativeHairBank.Read(text.bytes)"),"the bank is the local resource, read by the Core reader");
        Check(pres.Contains("string hair=HairPreview??NativeLook.Of(look,NativeLook.Hair);"),"the worn style is the look's (or the local preview's)");
        Check(pres.Contains("bind[0]=Matrix4x4.identity;")&&pres.Contains("bind[b+1]=Matrix4x4.Translate(new Vector3(-at[0],-at[1],-at[2]));"),"bind poses: the head the identity, each chain bone its head point back");
        Check(pres.Contains("t.SetParent(bone.Parent<0?head:bones[bone.Parent+1],false);"),"chains under the head (the cloth bridge finds their roots as the bake's)");
        Check(pres.Contains("skin.rootBone=head;"),"bounds on the head");
        // ---- the tool writes what the reader reads ----
        var tool=Src("tools/gear/hair.py");
        Check(tool.Contains("data = b'HAIR' + struct.pack('<i', 2) + z.compress(out.getvalue()) + z.flush()")&&tool.Contains("zlib.compressobj(9, zlib.DEFLATED, -15)")&&NativeHairBank.Version==2,"the tool's magic and version (2: the body raw-deflated)");
        Check(tool.Contains("'Hair_B%s%02d_%s_%02d_%d'"),"the tool's chain names (kind, group)");
        foreach(var line in tool.Split('\n'))
        {
            var t=line.Trim();if(!t.StartsWith("('",StringComparison.Ordinal)||!t.Contains(", [('"))continue;
            var id=t.Substring(2,t.IndexOf('\'',2)-2);Check(Array.IndexOf(hair,id)>=NativeLook.ServiceKnownHair,"the tool's style is a listed id: "+id);
        }
        Console.WriteLine("PASS hair bank");
    }

    // A bank file the tool wrote (tools/gear/hair.py), read by the game's reader: each piece and style, or why not.
    public static int ReadFile(string path)
    {
        var bank=NativeHairBank.Read(File.ReadAllBytes(path));
        if(bank==null){Console.WriteLine("FAIL hair bank file: not a version "+NativeHairBank.Version+" bank: "+path);return 1;}
        foreach(var p in bank.Pieces)Console.WriteLine("  "+p.Renderer+": "+p.VertexCount+" vertices, "+p.Triangles.Length/3+" triangles, "+p.Bones.Length+" bones, "+p.Shapes.Length+" shapes, texture "+p.Texture);
        foreach(var id in bank.StyleOrder)Console.WriteLine("  style "+id+": "+string.Join(" + ",Array.ConvertAll(bank.Styles[id],i=>bank.Pieces[i].Renderer)));
        Console.WriteLine("PASS hair bank file: "+bank.Pieces.Length+" pieces, "+bank.StyleOrder.Count+" styles, "+bank.Textures.Length+" textures");return 0;
    }

    // A plain (version 1) bank's body deflated into a version 2 bank (optionally with bytes after its body).
    private static byte[] Deflated(byte[] plain,byte[] trailing=null)
    {
        var body=new MemoryStream();using(var z=new System.IO.Compression.DeflateStream(body,System.IO.Compression.CompressionLevel.Optimal,true)){z.Write(plain,8,plain.Length-8);if(trailing!=null)z.Write(trailing,0,trailing.Length);}
        var o=new byte[8+body.Length];Buffer.BlockCopy(plain,0,o,0,4);o[4]=2;Buffer.BlockCopy(body.ToArray(),0,o,8,(int)body.Length);return o;
    }

    // A small bank: one texture, one piece (two chain bones, three vertices, one triangle, a tag shape), one style; each fault
    // switchable.
    private static byte[] Bank(int parent=-1,int tri=2,float weight=1,int boneIndex=1,string renderer="HairBack.Bank_Test",string bone="Hair_BC01_Test_00_",bool nan=false,int style=0)
    {
        var m=new MemoryStream();var w=new BinaryWriter(m,Encoding.UTF8);
        void S(string s){var b=Encoding.UTF8.GetBytes(s);w.Write(b.Length);w.Write(b);}
        w.Write((byte)'H');w.Write((byte)'A');w.Write((byte)'I');w.Write((byte)'R');w.Write(1);
        w.Write(1);S("t");w.Write(3);w.Write(new byte[]{1,2,3});
        w.Write(1);S(renderer);w.Write(0);w.Write(.5f);w.Write((byte)2);
        w.Write(2);S(bone+"0");w.Write(parent);w.Write(0f);w.Write(.2f);w.Write(-.2f);S(bone+"1");w.Write(0);w.Write(.1f);w.Write(.1f);w.Write(0f);
        w.Write(3);
        for(int i=0;i<9;i++)w.Write(nan&&i==4?float.NaN:i*.01f);
        for(int i=0;i<9;i++)w.Write(i%3==1?1f:0f);
        for(int i=0;i<6;i++)w.Write(i*.1f);
        for(int i=0;i<12;i++)w.Write((byte)255);
        for(int v=0;v<3;v++){w.Write((byte)boneIndex);w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);}
        for(int v=0;v<3;v++){w.Write(weight);w.Write(0f);w.Write(0f);w.Write(0f);}
        w.Write(3);w.Write(0);w.Write(1);w.Write(tri);
        w.Write(1);S("Tag_Hat");w.Write(1);w.Write(2);w.Write(0f);w.Write(-.01f);w.Write(0f);
        w.Write(1);S("test-style");w.Write(1);w.Write(style);
        w.Flush();return m.ToArray();
    }
}
