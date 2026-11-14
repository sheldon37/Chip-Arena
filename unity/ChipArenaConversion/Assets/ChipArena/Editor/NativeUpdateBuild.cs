using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace ChipArena.UnityConversion.Editor
{
    public static class NativeUpdateBuild
    {
        public static string PlayerFolder(string root)
        {
            NativeLauncherCompiler.ValidateProject(root);
            string folder=Path.Combine(root,"Builds","Windows","app");
            if(Directory.Exists(folder))
            {
                string marker=Path.Combine(folder,NativeUpdateRules.IdentityFile);
                if(!File.Exists(marker)||JsonUtility.FromJson<NativeBuildIdentity>(File.ReadAllText(marker)).product!=NativeUpdateRules.Product)
                    throw new IOException("The app output folder is not a recognized native build. Move it aside before rebuilding: "+folder);
                NativeUpdateRules.RejectReparsePoints(folder);Directory.Delete(folder,true);
            }
            Directory.CreateDirectory(folder);return folder;
        }
        public static NativeBuildIdentity Package(string project,string player)
        {
            NativeTunnelBuild.Stage(project,player);
            File.Copy(Path.Combine(project,"tools","brickfall-music-credits.txt"),Path.Combine(player,"Music-Credits.txt"),true);
            var identity=new NativeBuildIdentity{version=SourceHash(project),revision=DateTime.UtcNow.Ticks};
            File.WriteAllText(Path.Combine(player,NativeUpdateRules.IdentityFile),JsonUtility.ToJson(identity,true));
            string install=Path.GetDirectoryName(player);CompileLauncher(project,install);
            File.WriteAllText(Path.Combine(install,"native-install.txt"),NativeUpdateRules.Product+"/1\n");
            string feed=Path.Combine(project,"Builds","NativeUpdateFeed");Directory.CreateDirectory(feed);Directory.CreateDirectory(Path.Combine(feed,"objects"));
            string zip=Path.Combine(project,"Builds","native-update.zip");if(File.Exists(zip))File.Delete(zip);
            var files=Directory.GetFiles(player,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).ToArray();long unpacked=0;
            using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))
            foreach(string file in files)
            {
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Build contains a symbolic link.");
                string rel=file.Substring(player.Length+1).Replace('\\','/');NativeUpdateRules.SafeRelativePath(rel);
                archive.CreateEntryFromFile(file,rel,CompressionLevel.Optimal);unpacked+=new FileInfo(file).Length;
            }
            string hash=NativeUpdateRules.HashFile(zip);var parts=new System.Collections.Generic.List<NativeUpdatePart>();
            using(var input=File.OpenRead(zip))
            {
                var buffer=new byte[65536];int index=0;
                while(input.Position<input.Length)
                {
                    string name=hash+"-"+(index++).ToString("D4")+".part",path=Path.Combine(feed,"objects",name);long bytes=0;
                    using(var output=File.Create(path))while(bytes<NativeUpdateRules.MaxPartBytes)
                    {int n=input.Read(buffer,0,(int)Math.Min(buffer.Length,NativeUpdateRules.MaxPartBytes-bytes));if(n==0)break;output.Write(buffer,0,n);bytes+=n;}
                    parts.Add(new NativeUpdatePart{name=name,bytes=bytes,sha256=NativeUpdateRules.HashFile(path)});
                }
            }
            var manifest=new NativeUpdateManifest{build=identity,sha256=hash,bytes=new FileInfo(zip).Length,unpackedBytes=unpacked,files=files.Length,parts=parts.ToArray()};
            NativeUpdateRules.ValidateManifest(manifest);File.WriteAllText(Path.Combine(feed,"latest.json"),JsonUtility.ToJson(manifest,true));
            UnityEngine.Debug.Log("NATIVE_UPDATE_FEED_READY: "+feed+" (prepared only; no publication)");return identity;
        }
        private static string SourceHash(string root)
        {
            using(var hash=SHA256.Create())
            {
                foreach(var dir in new[]{"Assets","Packages","ProjectSettings","Updater"})
                foreach(string path in Directory.GetFiles(Path.Combine(root,dir),"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal))
                {
                    string rel=path.Substring(root.Length+1).Replace('\\','/');if(rel.Split('/').Any(p=>p=="bin"||p=="obj"))continue;
                    byte[] label=Encoding.UTF8.GetBytes(rel+"\0");hash.TransformBlock(label,0,label.Length,label,0);
                    using(var input=File.OpenRead(path)){var buffer=new byte[65536];int n;while((n=input.Read(buffer,0,buffer.Length))>0)hash.TransformBlock(buffer,0,n,buffer,0);}
                }
                hash.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
            }
        }
        private static void CompileLauncher(string project,string destination)
        {
            string icon=Path.Combine(project,"Builds","native-launcher.ico");byte[] png;
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);var sized=new Texture2D(256,256,TextureFormat.RGBA32,false);
            try
            {
                if(!source.LoadImage(File.ReadAllBytes(Path.Combine(project,PrototypeBuild.IconPath))))throw new IOException("Cannot decode the CA launcher icon.");
                var pixels=new Color[256*256];
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)pixels[y*256+x]=source.GetPixelBilinear((x+.5f)/256,(y+.5f)/256);
                sized.SetPixels(pixels);sized.Apply(false);png=sized.EncodeToPNG();
            }
            finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(sized);}
            using(var output=new BinaryWriter(File.Create(icon)))
            {output.Write((ushort)0);output.Write((ushort)1);output.Write((ushort)1);output.Write((byte)0);output.Write((byte)0);output.Write((byte)0);output.Write((byte)0);output.Write((ushort)1);output.Write((ushort)32);output.Write(png.Length);output.Write(22);output.Write(png);}
            NativeLauncherCompiler.Compile(project,destination,icon);
        }
        public static void SeedZip(string install,string zip)
        {NativeStarterPackage.Create(install,zip);}
    }
}
