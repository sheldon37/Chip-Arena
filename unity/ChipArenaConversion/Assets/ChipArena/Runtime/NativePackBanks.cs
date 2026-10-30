using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The pack banks the licensed-assets zip may carry (Core NativePackBank, docs/PACK-BANK.md), read once each from
    // Resources/<bank>.bytes, with their meshes built and their textures (Resources/PackBank/<name>) loaded on first use.
    // A missing bank, mesh or texture is null: every caller keeps today's look without it (CI, cloud builds, other checkouts,
    // the public source zips). --ca-no-pack-banks turns them all off to compare against today's look.
    public static class NativePackBanks
    {
        public const string Kit="ChipArenaPackKit", Fx="ChipArenaPackFx", Weapons="ChipArenaPackWeapons", TextureFolder="PackBank/", OffArgument="--ca-no-pack-banks";
        private sealed class Loaded{public NativePackBank.Bank Bank;public Mesh[] Meshes;public Texture2D[] Textures;public bool[] TextureRead;}
        private static readonly Dictionary<string,Loaded> banks=new Dictionary<string,Loaded>(StringComparer.Ordinal);
        private static readonly Dictionary<string,Texture2D> loose=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
        private static int off=-1;
        // Why the last bank failed to read (shown by the DEV panel's assets line), or null.
        public static string Why{get;private set;}

        public static bool Off
        {
            get
            {
                if(off<0){off=0;try{foreach(var a in Environment.GetCommandLineArgs())if(a==OffArgument)off=1;}catch(Exception){}}
                return off==1;
            }
        }

        private static Loaded Load(string name)
        {
            if(Off||string.IsNullOrEmpty(name))return null;
            if(banks.TryGetValue(name,out var loaded))return loaded;
            loaded=null;
            var asset=Resources.Load<TextAsset>(name);
            if(asset!=null)
            {
                try
                {
                    var bank=NativePackBank.Read(asset.bytes);
                    loaded=new Loaded{Bank=bank,Meshes=new Mesh[bank.Meshes.Count],Textures=new Texture2D[bank.Textures.Count],TextureRead=new bool[bank.Textures.Count]};
                }
                catch(Exception e){Why=name+": "+e.Message;Debug.LogWarning("Pack bank "+name+": "+e.Message);}
                Resources.UnloadAsset(asset);
            }
            banks[name]=loaded;
            return loaded;
        }

        public static NativePackBank.Bank Bank(string name)=>Load(name)?.Bank;

        public static Mesh Mesh(string name,int index)
        {
            var l=Load(name);if(l==null||index<0||index>=l.Meshes.Length)return null;
            if(l.Meshes[index]!=null)return l.Meshes[index];
            var src=l.Bank.Meshes[index];int n=src.Positions.Length;
            var v=new Vector3[n];var nr=new Vector3[n];var uv=new Vector2[n];
            for(int i=0;i<n;i++){var p=src.Positions[i];v[i]=new Vector3(p.X,p.Y,p.Z);var q=src.Normals[i];nr[i]=new Vector3(q.X,q.Y,q.Z);var t=src.Uv0[i];uv[i]=new Vector2(t.X,t.Y);}
            var mesh=new Mesh{name=src.Name};
            if(n>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices=v;mesh.normals=nr;mesh.uv=uv;
            if(src.Uv1!=null){var uv1=new Vector2[n];for(int i=0;i<n;i++)uv1[i]=new Vector2(src.Uv1[i].X,src.Uv1[i].Y);mesh.uv2=uv1;}
            if(src.Colors!=null){var c=new Color32[n];for(int i=0;i<n;i++)c[i]=new Color32(src.Colors[i*4],src.Colors[i*4+1],src.Colors[i*4+2],src.Colors[i*4+3]);mesh.colors32=c;}
            mesh.subMeshCount=src.Submeshes.Length;
            for(int s=0;s<src.Submeshes.Length;s++)mesh.SetTriangles(src.Submeshes[s].Indices,s,false);
            mesh.bounds=new Bounds(new Vector3((src.Min.X+src.Max.X)*.5f,(src.Min.Y+src.Max.Y)*.5f,(src.Min.Z+src.Max.Z)*.5f),new Vector3(src.Max.X-src.Min.X,src.Max.Y-src.Min.Y,src.Max.Z-src.Min.Z));
            mesh.UploadMeshData(true);
            return l.Meshes[index]=mesh;
        }

        public static Texture2D Texture(string name,int index)
        {
            var l=Load(name);if(l==null||index<0||index>=l.Textures.Length)return null;
            if(!l.TextureRead[index]){l.TextureRead[index]=true;l.Textures[index]=Resources.Load<Texture2D>(TextureFolder+l.Bank.Textures[index].Name);}
            return l.Textures[index];
        }

        // A bank texture by its own name (effect sheets the selections list by id), whichever bank carries it.
        public static Texture2D Texture(string id)
        {
            if(Off||string.IsNullOrEmpty(id))return null;
            if(!loose.TryGetValue(id,out var t)){t=Resources.Load<Texture2D>(TextureFolder+id);loose[id]=t;}
            return t;
        }

        public static NativePackBank.Prop Prop(string bank,string id)=>Bank(bank)?.Find(id);

        // Unity's matrix for a part's place (System.Numerics row-vector convention to Unity's column vectors).
        public static Matrix4x4 Place(System.Numerics.Matrix4x4 m)
        {
            var u=new Matrix4x4();
            u.m00=m.M11;u.m01=m.M21;u.m02=m.M31;u.m03=m.M41;
            u.m10=m.M12;u.m11=m.M22;u.m12=m.M32;u.m13=m.M42;
            u.m20=m.M13;u.m21=m.M23;u.m22=m.M33;u.m23=m.M43;
            u.m30=0;u.m31=0;u.m32=0;u.m33=1;
            return u;
        }
    }
}
