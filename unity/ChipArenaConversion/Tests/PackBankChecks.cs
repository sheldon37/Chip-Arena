#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The pack banks (Core/NativePackBank, docs/PACK-BANK.md). Tests/pack-bank-fixture.bytes is the bank the Python tools write
    // for a synthetic pack (Tests/test_pack_bank.py rebuilds it and compares bytes), so the C# reader is checked against the
    // writer the owner runs: its textures, materials, the one mesh both parts share, the prop's parts and bounds; the managed
    // writer gives the same bytes back; damaged banks are refused; the box fit keeps a prop inside its box. Source scans pin
    // the runtime loader: Resources only, each failure a null (today's look), the off switch, and no pack names in code.
    internal static class PackBankChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Pack bank: "+what);count++;}
        private static bool Near(float a,float b,float e=1e-4f)=>MathF.Abs(a-b)<=e;
        public static void Run(string root)
        {
            var bytes=File.ReadAllBytes(Path.Combine(root,"Tests/pack-bank-fixture.bytes"));
            var bank=NativePackBank.Read(bytes);

            // 1. What the synthetic pack holds.
            Check(bank.Textures.Count==1&&bank.Textures[0].Name=="syn-paint"&&bank.Textures[0].Flags==0,"the texture");
            Check(bank.Materials.Count==1&&bank.Materials[0].Name=="syn/Paint"&&bank.Materials[0].Albedo==0&&bank.Materials[0].Emission==-1,"the material");
            var c=bank.Materials[0].Color;Check(Near(c.X,1)&&Near(c.Y,.5f)&&Near(c.Z,.25f)&&Near(c.W,1),"the material colour");
            Check(bank.Meshes.Count==1,"one mesh shared by both parts");
            var mesh=bank.Meshes[0];
            Check(mesh.Positions.Length==8&&mesh.Submeshes.Length==1&&mesh.Submeshes[0].Indices.Length==12&&mesh.Submeshes[0].Material==0,"the mesh's two quads");
            foreach(var p in mesh.Positions)Check(Near(p.X,-1)||Near(p.X,-2),"positions mirrored and offset (metres)");
            foreach(var n in mesh.Normals)Check(Near(n.Length(),1)&&Near(n.X,0),"unit normals");
            Check(Near(mesh.Min.X,-2)&&Near(mesh.Max.X,-1),"mesh bounds");
            var prop=bank.Find("syn-box");
            Check(prop!=null&&prop.Group=="box"&&prop.Parts.Length==2&&bank.Find("missing")==null,"the prop");
            int lid=Array.FindIndex(prop.Parts,q=>!q.Place.IsIdentity);
            Check(lid>=0,"the lid part is placed");
            var moved=Vector3.Transform(new Vector3(1,0,0),prop.Parts[lid].Place);
            Check(Near(moved.X,0)&&Near(moved.Y,2)&&Near(moved.Z,-2),"the lid's place (turned a quarter, raised, doubled)");
            Check(prop.Min.Y>=-1e-4f&&prop.Max.Y>2&&prop.Size.X>0&&prop.Size.Z>0,"prop bounds over both parts");

            // 2. The managed writer gives the same bytes.
            var again=NativePackBank.Write(bank);
            Check(again.Length==bytes.Length,"rewrite length");
            for(int i=0;i<bytes.Length;i++)if(again[i]!=bytes[i]){Check(false,"rewrite byte "+i);break;}
            count++;

            // 3. Damaged banks are refused.
            bool Refused(byte[] b){try{NativePackBank.Read(b);return false;}catch(InvalidDataException){return true;}catch(EndOfStreamException){return true;}}
            var bad=(byte[])bytes.Clone();bad[0]=(byte)'X';Check(Refused(bad),"bad magic");
            bad=(byte[])bytes.Clone();bad[4]=9;Check(Refused(bad),"bad version");
            Check(Refused(bytes[..(bytes.Length-5)]),"truncated");
            var longer=new byte[bytes.Length+1];bytes.CopyTo(longer,0);Check(Refused(longer),"trailing bytes");

            // 4. The box fit: the prop's footprint and height stay inside the box, its base on the box's floor.
            foreach(int yaw in new[]{0,1,2,3})foreach(float stretch in new[]{0f,.5f,1f})
            {
                var box=new Vector3(3,1.5f,2);var fit=NativePackBank.Fit(prop,box,yaw,stretch);
                var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);
                foreach(var part in prop.Parts)foreach(var p in bank.Meshes[part.Mesh].Positions)
                {var q=Vector3.Transform(Vector3.Transform(p,part.Place),fit);lo=Vector3.Min(lo,q);hi=Vector3.Max(hi,q);}
                Check(lo.X>=-box.X*.5f-1e-3f&&hi.X<=box.X*.5f+1e-3f&&lo.Z>=-box.Z*.5f-1e-3f&&hi.Z<=box.Z*.5f+1e-3f&&hi.Y<=box.Y*.5f+1e-3f,"fit inside the box yaw "+yaw+" stretch "+stretch);
                Check(Near(lo.Y,-box.Y*.5f,1e-3f),"fit stands on the box's floor");
                if(stretch>=1)Check(Near(hi.X-lo.X,box.X,1e-3f)&&Near(hi.Y-lo.Y,box.Y,1e-3f)&&Near(hi.Z-lo.Z,box.Z,1e-3f),"full stretch fills the box");
            }

            // 5. The runtime loader.
            string src=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativePackBanks.cs"));
            Check(src.Contains("Resources.Load<TextAsset>(name)")&&src.Contains("Resources.Load<Texture2D>(TextureFolder+"),"banks and textures come from Resources");
            Check(src.Contains("catch(Exception e){Why=")&&src.Contains("banks[name]=loaded;"),"a bank that fails to read is null, once");
            Check(src.Contains("OffArgument=\"--ca-no-pack-banks\"")&&src.Contains("if(Off||string.IsNullOrEmpty(name))return null;"),"the off switch");
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories))
            {
                string text=File.ReadAllText(file);
                foreach(var pack in new[]{"Toon Harbor","Synty","polyperfect","Kronnect","Archanor","kripto289"})
                    Check(!text.Contains(pack),"no pack name in "+Path.GetFileName(file)+": "+pack);
            }
            Console.WriteLine("PASS pack bank: "+count+" checks");
        }
    }
}
#endif
