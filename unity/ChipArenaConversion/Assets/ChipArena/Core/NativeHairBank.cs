using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The pilot's hair bank (owner 2026-11-26: "Lets add a lot more hairstyles, some anime ones as well (Think like sasuke from
    // naruto) [For both male and female]. Add magica cloth into these to simulate hair movement realistically"; docs/PILOT-BODY-HAIR.md
    // section 3). A local resource (Resources/ChipArenaHair.bytes, written by tools/gear/hair.py from the licensed pack's hair and
    // generated anime locks, so kept local like the bake): its hairstyles (Core/NativeLook.cs hair ids, each a front and a back
    // piece) and each piece's mesh in the head bone's frame, skinned to the head and to chains of its own bones (rows of joints down
    // the hair, each chain's root fixed on the head) that the local cloth bridge simulates (Magica BoneCloth: a chain root is a bone
    // named Hair_B<kind><group>_..., its kind the cloth's tuning, its group the chains joined as one sheet). Runtime
    // NativeHairBankPresentation.cs builds the worn style's pieces on a body. Presentation only: no rule, hit volume, timing or
    // network message reads it; a look naming a style the bank lacks draws the default hair (ArenaPresentation.VisualLookOf).
    public static class NativeHairBank
    {
        public const string Resource="ChipArenaHair";
        /// <summary>The bank's version: 2, its body (everything after the version) deflated (raw deflate; a bank's meshes are about
        /// half their size so); 1, the same body as it is (still read).</summary>
        public const int Version=2,Plain=1,MaxBody=64<<20;
        /// <summary>Limits the reader holds a bank to (a malformed or hostile file is refused whole).</summary>
        public const int MaxTextures=64,MaxPieces=128,MaxBones=255,MaxVertices=65535,MaxIndices=600000,MaxShapes=16,MaxStyles=128,MaxString=128,MaxTextureBytes=4<<20;
        /// <summary>The cloth kinds a chain's root name carries (its seventh character): stiff spikes that spring back, medium
        /// locks, a free long fall, a connected curtain, a tied tail.</summary>
        public const char Spike='S',Lock='L',Fall='F',Curtain='C',Tail='T';
        public const string ChainPrefix="Hair_B";

        public sealed class Bone{public string Name;public int Parent;public float X,Y,Z;}
        public sealed class Shape{public string Name;public int[] Vertices;public float[] Deltas;}
        public sealed class Texture{public string Name;public byte[] Png;}
        public sealed class Piece
        {
            public string Renderer;public int Texture;public float Cutoff;public byte Cull;public Bone[] Bones;
            public int VertexCount;public float[] Positions,Normals,Uvs;public byte[] Colors;
            /// <summary>Four per vertex: bone 0 the head, k the bank bone k-1; weights summing to one.</summary>
            public byte[] BoneIndex;public float[] BoneWeight;
            public int[] Triangles;public Shape[] Shapes;
            /// <summary>Each bank bone's point in the head's frame (the sum of its own and its parents' offsets).</summary>
            public float[] HeadPoint(int bone)
            {
                float x=0,y=0,z=0;for(int b=bone;b>=0;b=Bones[b].Parent){x+=Bones[b].X;y+=Bones[b].Y;z+=Bones[b].Z;}
                return new[]{x,y,z};
            }
        }
        public sealed class Bank
        {
            public Texture[] Textures;public Piece[] Pieces;
            public readonly Dictionary<string,int[]> Styles=new Dictionary<string,int[]>(StringComparer.Ordinal);
            public readonly List<string> StyleOrder=new List<string>();
            public bool Has(string style)=>style!=null&&Styles.ContainsKey(style);
        }

        /// <summary>The bank in `data`, or null when it is not one (a wrong magic or version, a count or index out of range, a
        /// weight that is not finite, a truncated file).</summary>
        public static Bank Read(byte[] data)
        {
            if(data==null||data.Length<12)return null;
            try
            {
                using var r=new BinaryReader(new MemoryStream(data,false),Encoding.UTF8);
                if(r.ReadByte()!='H'||r.ReadByte()!='A'||r.ReadByte()!='I'||r.ReadByte()!='R')return null;
                int version=r.ReadInt32();if(version!=Version&&version!=Plain)return null;
                if(version==Version)
                {
                    // the body inflated (no larger than MaxBody), then read as a plain bank's
                    var body=new MemoryStream();
                    using(var z=new DeflateStream(new MemoryStream(data,8,data.Length-8,false),CompressionMode.Decompress))
                    {
                        var buf=new byte[81920];int n;while((n=z.Read(buf,0,buf.Length))>0){body.Write(buf,0,n);if(body.Length>MaxBody)return null;}
                    }
                    var plain=new byte[8+body.Length];Buffer.BlockCopy(data,0,plain,0,4);plain[4]=Plain;body.Position=0;body.Read(plain,8,(int)body.Length);
                    return Read(plain);
                }
                var bank=new Bank();
                int nt=Count(r,MaxTextures);bank.Textures=new Texture[nt];
                for(int i=0;i<nt;i++){var name=Str(r);int len=Count(r,MaxTextureBytes);bank.Textures[i]=new Texture{Name=name,Png=Bytes(r,len)};}
                int np=Count(r,MaxPieces);bank.Pieces=new Piece[np];
                for(int i=0;i<np;i++)bank.Pieces[i]=ReadPiece(r,nt);
                int ns=Count(r,MaxStyles);
                for(int i=0;i<ns;i++)
                {
                    var id=Str(r);int n=Count(r,8);var pieces=new int[n];
                    for(int k=0;k<n;k++){pieces[k]=r.ReadInt32();if(pieces[k]<0||pieces[k]>=np)return null;}
                    if(id.Length==0||bank.Styles.ContainsKey(id))return null;bank.Styles[id]=pieces;bank.StyleOrder.Add(id);
                }
                return r.BaseStream.Position==r.BaseStream.Length?bank:null;
            }
            catch(EndOfStreamException){return null;}
            catch(InvalidDataException){return null;}
            catch(IOException){return null;}
        }
        private static Piece ReadPiece(BinaryReader r,int textures)
        {
            var p=new Piece{Renderer=Str(r),Texture=r.ReadInt32(),Cutoff=r.ReadSingle(),Cull=r.ReadByte()};
            if(!(p.Renderer.StartsWith("HairFront.",StringComparison.Ordinal)||p.Renderer.StartsWith("HairBack.",StringComparison.Ordinal)))throw new InvalidDataException();
            if(p.Texture<-1||p.Texture>=textures||!Finite(p.Cutoff))throw new InvalidDataException();
            int nb=Count(r,MaxBones);p.Bones=new Bone[nb];
            for(int b=0;b<nb;b++)
            {
                var bone=new Bone{Name=Str(r),Parent=r.ReadInt32(),X=r.ReadSingle(),Y=r.ReadSingle(),Z=r.ReadSingle()};
                if(bone.Parent<-1||bone.Parent>=b||!Finite(bone.X)||!Finite(bone.Y)||!Finite(bone.Z)||!bone.Name.StartsWith(ChainPrefix,StringComparison.Ordinal))throw new InvalidDataException();
                p.Bones[b]=bone;
            }
            int nv=Count(r,MaxVertices);p.VertexCount=nv;
            p.Positions=Floats(r,nv*3);p.Normals=Floats(r,nv*3);p.Uvs=Floats(r,nv*2);p.Colors=Bytes(r,nv*4);
            p.BoneIndex=Bytes(r,nv*4);p.BoneWeight=Floats(r,nv*4);
            for(int i=0;i<nv*4;i++)if(p.BoneIndex[i]>nb||p.BoneWeight[i]<0||p.BoneWeight[i]>1.0001f)throw new InvalidDataException();
            int ni=Count(r,MaxIndices);if(ni%3!=0)throw new InvalidDataException();p.Triangles=new int[ni];
            for(int i=0;i<ni;i++){int t=r.ReadInt32();if(t<0||t>=nv)throw new InvalidDataException();p.Triangles[i]=t;}
            int nsh=Count(r,MaxShapes);p.Shapes=new Shape[nsh];
            for(int s=0;s<nsh;s++)
            {
                var shape=new Shape{Name=Str(r)};int n=Count(r,nv);shape.Vertices=new int[n];
                for(int i=0;i<n;i++){int v=r.ReadInt32();if(v<0||v>=nv)throw new InvalidDataException();shape.Vertices[i]=v;}
                shape.Deltas=Floats(r,n*3);p.Shapes[s]=shape;
            }
            return p;
        }
        private static int Count(BinaryReader r,int max){int n=r.ReadInt32();if(n<0||n>max)throw new InvalidDataException();return n;}
        private static string Str(BinaryReader r){int n=Count(r,MaxString);return Encoding.UTF8.GetString(Bytes(r,n));}
        private static byte[] Bytes(BinaryReader r,int n){var b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return b;}
        private static float[] Floats(BinaryReader r,int n)
        {
            var raw=Bytes(r,n*4);var f=new float[n];Buffer.BlockCopy(raw,0,f,0,raw.Length);
            if(!BitConverter.IsLittleEndian)throw new InvalidDataException();
            for(int i=0;i<n;i++)if(!Finite(f[i]))throw new InvalidDataException();
            return f;
        }
        private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);

        /// <summary>A bank chain root's cloth kind (Spike, Lock, Fall, Curtain, Tail) and group (chains of one group are one sheet),
        /// or false for any other bone.</summary>
        public static bool ChainOf(string bone,out char kind,out int group)
        {
            kind='\0';group=0;
            if(bone==null||bone.Length<9||!bone.StartsWith(ChainPrefix,StringComparison.Ordinal))return false;
            kind=bone[6];if(kind!=Spike&&kind!=Lock&&kind!=Fall&&kind!=Curtain&&kind!=Tail)return false;
            if(!char.IsDigit(bone[7])||!char.IsDigit(bone[8]))return false;group=(bone[7]-'0')*10+(bone[8]-'0');return true;
        }
    }
}
