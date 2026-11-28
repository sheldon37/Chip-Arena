using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace ChipArena.UnityConversion
{
    // A pack bank (docs/PACK-BANK.md): meshes, materials and props from the owner's licensed packs, converted by
    // tools/pack_bank/build_pack_bank.py into Resources/<bank>.bytes inside the git-ignored Assets/LocalAssets (the
    // licensed-assets zip), with its textures beside it as Resources/PackBank/<name>.png. Meshes are already in Unity space
    // (metres, Y up, the X mirror of the model import applied, clockwise front faces). Without the zip there is no bank and
    // every caller keeps today's look. Little-endian:
    // "CAPB", version 1; textures: count, per texture its name (length, UTF-8) and flags; materials: count, per material its
    // name, albedo and emission texture (-1: none), colour rgba, emission rgb and flags; meshes: count, per mesh its name,
    // bounds (min, max), vertex count, attribute flags, positions, normals, uv0, [uv1], [colours as bytes], then submeshes:
    // count, per submesh its material and index count and the indices (16 bit up to 65535 vertices, else 32); props: count,
    // per prop its name, group, bounds, part count and per part its mesh and 3x4 affine rows (mesh space to prop space).
    public static class NativePackBank
    {
        public const int Version=1;
        public const byte AttrNormal=1, AttrUv0=2, AttrUv1=4, AttrColor=8;
        public const byte TexAlpha=1, TexLinear=2;
        public const byte MatCutout=1, MatTransparent=2, MatTwoSided=4, MatGlass=8;
        public const int MaxVertices=1<<20, MaxEntries=100000;

        public sealed class Texture{public string Name;public byte Flags;}
        public sealed class Material{public string Name;public int Albedo,Emission;public Vector4 Color;public Vector3 EmissionColor;public byte Flags;}
        public sealed class Submesh{public int Material;public int[] Indices;}
        public sealed class Mesh
        {
            public string Name;public Vector3 Min,Max;public byte Attributes;
            public Vector3[] Positions,Normals;public Vector2[] Uv0,Uv1;public byte[] Colors;public Submesh[] Submeshes;
        }
        public struct Part{public int Mesh;public Matrix4x4 Place;}
        public sealed class Prop{public string Name,Group;public Vector3 Min,Max;public Part[] Parts;public Vector3 Size=>Max-Min;}
        public sealed class Bank
        {
            public readonly List<Texture> Textures=new List<Texture>();
            public readonly List<Material> Materials=new List<Material>();
            public readonly List<Mesh> Meshes=new List<Mesh>();
            public readonly List<Prop> Props=new List<Prop>();
            public readonly Dictionary<string,int> PropIndex=new Dictionary<string,int>(StringComparer.Ordinal);
            public Prop Find(string name)=>name!=null&&PropIndex.TryGetValue(name,out var i)?Props[i]:null;
            public IEnumerable<Prop> Group(string group){foreach(var p in Props)if(p.Group==group)yield return p;}
            public Texture TextureOf(int index)=>index>=0&&index<Textures.Count?Textures[index]:null;
        }

        private static string Str(BinaryReader r)
        {
            int n=r.ReadInt32();if(n<0||n>4096)throw new InvalidDataException("pack bank string");
            return Encoding.UTF8.GetString(r.ReadBytes(n));
        }
        private static int Count(BinaryReader r,int max){int n=r.ReadInt32();if(n<0||n>max)throw new InvalidDataException("pack bank count");return n;}
        private static Vector3 V3(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());

        public static Bank Read(byte[] bytes)
        {
            if(bytes==null)throw new ArgumentNullException(nameof(bytes));
            using(var r=new BinaryReader(new MemoryStream(bytes),Encoding.UTF8))
            {
                if(r.ReadByte()!='C'||r.ReadByte()!='A'||r.ReadByte()!='P'||r.ReadByte()!='B')throw new InvalidDataException("not a pack bank");
                if(r.ReadInt32()!=Version)throw new InvalidDataException("pack bank version");
                var bank=new Bank();
                int textures=Count(r,MaxEntries);
                for(int i=0;i<textures;i++)bank.Textures.Add(new Texture{Name=Str(r),Flags=r.ReadByte()});
                int materials=Count(r,MaxEntries);
                for(int i=0;i<materials;i++)
                {
                    var m=new Material{Name=Str(r),Albedo=r.ReadInt32(),Emission=r.ReadInt32(),Color=new Vector4(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),EmissionColor=V3(r),Flags=r.ReadByte()};
                    if(m.Albedo>=textures||m.Emission>=textures)throw new InvalidDataException("pack bank material texture");
                    bank.Materials.Add(m);
                }
                int meshes=Count(r,MaxEntries);
                for(int i=0;i<meshes;i++)
                {
                    var m=new Mesh{Name=Str(r),Min=V3(r),Max=V3(r)};int n=Count(r,MaxVertices);m.Attributes=r.ReadByte();
                    m.Positions=new Vector3[n];for(int k=0;k<n;k++)m.Positions[k]=V3(r);
                    m.Normals=new Vector3[n];if((m.Attributes&AttrNormal)!=0)for(int k=0;k<n;k++)m.Normals[k]=V3(r);
                    m.Uv0=new Vector2[n];if((m.Attributes&AttrUv0)!=0)for(int k=0;k<n;k++)m.Uv0[k]=new Vector2(r.ReadSingle(),r.ReadSingle());
                    if((m.Attributes&AttrUv1)!=0){m.Uv1=new Vector2[n];for(int k=0;k<n;k++)m.Uv1[k]=new Vector2(r.ReadSingle(),r.ReadSingle());}
                    if((m.Attributes&AttrColor)!=0)m.Colors=r.ReadBytes(n*4);
                    int subs=Count(r,256);m.Submeshes=new Submesh[subs];bool wide=n>65535;
                    for(int s=0;s<subs;s++)
                    {
                        var sm=new Submesh{Material=r.ReadInt32()};int c=Count(r,MaxVertices*6);
                        if(sm.Material<0||sm.Material>=materials||c%3!=0)throw new InvalidDataException("pack bank submesh");
                        sm.Indices=new int[c];
                        for(int k=0;k<c;k++){int v=wide?(int)r.ReadUInt32():r.ReadUInt16();if(v>=n)throw new InvalidDataException("pack bank index");sm.Indices[k]=v;}
                        m.Submeshes[s]=sm;
                    }
                    bank.Meshes.Add(m);
                }
                int props=Count(r,MaxEntries);
                for(int i=0;i<props;i++)
                {
                    var p=new Prop{Name=Str(r),Group=Str(r),Min=V3(r),Max=V3(r)};int parts=Count(r,4096);p.Parts=new Part[parts];
                    for(int k=0;k<parts;k++)
                    {
                        int mesh=r.ReadInt32();if(mesh<0||mesh>=meshes)throw new InvalidDataException("pack bank part");
                        float[] a=new float[12];for(int j=0;j<12;j++)a[j]=r.ReadSingle();
                        // Rows of the column-vector affine, stored as System.Numerics' row-vector transpose.
                        p.Parts[k]=new Part{Mesh=mesh,Place=new Matrix4x4(a[0],a[4],a[8],0,a[1],a[5],a[9],0,a[2],a[6],a[10],0,a[3],a[7],a[11],1)};
                    }
                    bank.PropIndex[p.Name]=bank.Props.Count;bank.Props.Add(p);
                }
                if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("pack bank trailing bytes");
                return bank;
            }
        }

        // The same layout, for tests and tools that build a bank in managed code.
        public static byte[] Write(Bank bank)
        {
            using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms,Encoding.UTF8))
            {
                void S(string s){var b=Encoding.UTF8.GetBytes(s??"");w.Write(b.Length);w.Write(b);}
                void W3(Vector3 v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
                w.Write((byte)'C');w.Write((byte)'A');w.Write((byte)'P');w.Write((byte)'B');w.Write(Version);
                w.Write(bank.Textures.Count);foreach(var t in bank.Textures){S(t.Name);w.Write(t.Flags);}
                w.Write(bank.Materials.Count);
                foreach(var m in bank.Materials){S(m.Name);w.Write(m.Albedo);w.Write(m.Emission);w.Write(m.Color.X);w.Write(m.Color.Y);w.Write(m.Color.Z);w.Write(m.Color.W);W3(m.EmissionColor);w.Write(m.Flags);}
                w.Write(bank.Meshes.Count);
                foreach(var m in bank.Meshes)
                {
                    S(m.Name);W3(m.Min);W3(m.Max);int n=m.Positions.Length;w.Write(n);
                    byte attrs=(byte)(AttrNormal|AttrUv0|(m.Uv1!=null?AttrUv1:0)|(m.Colors!=null?AttrColor:0));w.Write(attrs);
                    foreach(var p in m.Positions)W3(p);foreach(var p in m.Normals)W3(p);foreach(var p in m.Uv0){w.Write(p.X);w.Write(p.Y);}
                    if(m.Uv1!=null)foreach(var p in m.Uv1){w.Write(p.X);w.Write(p.Y);}
                    if(m.Colors!=null)w.Write(m.Colors);
                    w.Write(m.Submeshes.Length);bool wide=n>65535;
                    foreach(var s in m.Submeshes){w.Write(s.Material);w.Write(s.Indices.Length);foreach(var i in s.Indices){if(wide)w.Write((uint)i);else w.Write((ushort)i);}}
                }
                w.Write(bank.Props.Count);
                foreach(var p in bank.Props)
                {
                    S(p.Name);S(p.Group);W3(p.Min);W3(p.Max);w.Write(p.Parts.Length);
                    foreach(var part in p.Parts)
                    {
                        var m=part.Place;w.Write(part.Mesh);
                        float[] a={m.M11,m.M21,m.M31,m.M41,m.M12,m.M22,m.M32,m.M42,m.M13,m.M23,m.M33,m.M43};foreach(var f in a)w.Write(f);
                    }
                }
                w.Flush();return ms.ToArray();
            }
        }

        // Where a prop sits in a box (a collision block or a kit part): its footprint centred on the box's, its base on the
        // box's floor, turned by yaw quarter turns (0-3, clockwise seen from above: Unity's rotation about +Y), scaled
        // uniformly to fill the box's height or footprint, whichever limits it, then each axis stretched toward the box by at
        // most stretch (0: keep the prop's own proportions). Returns the prop-space to box-space transform (System.Numerics
        // row-vector convention: v' = v * M).
        public static Matrix4x4 Fit(Prop prop,Vector3 box,int yaw,float stretch)
        {
            var size=prop.Size;bool turned=(yaw&1)==1;
            float sx=turned?size.Z:size.X,sz=turned?size.X:size.Z,sy=size.Y;
            float u=MathF.Min(box.Y/MathF.Max(sy,1e-4f),MathF.Min(box.X/MathF.Max(sx,1e-4f),box.Z/MathF.Max(sz,1e-4f)));
            float k=Math.Clamp(stretch,0f,1f);
            float ax=u+(box.X/MathF.Max(sx,1e-4f)-u)*k,ay=u+(box.Y/MathF.Max(sy,1e-4f)-u)*k,az=u+(box.Z/MathF.Max(sz,1e-4f)-u)*k;
            var centre=new Vector3((prop.Min.X+prop.Max.X)*.5f,prop.Min.Y,(prop.Min.Z+prop.Max.Z)*.5f);
            var turn=Matrix4x4.CreateRotationY(-(yaw&3)*MathF.PI*.5f);
            return Matrix4x4.CreateTranslation(-centre)*turn*Matrix4x4.CreateScale(ax,ay,az)*Matrix4x4.CreateTranslation(0,-box.Y*.5f,0);
        }
    }
}
