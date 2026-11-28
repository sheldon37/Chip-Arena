using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// `--blade-models <dir>`: builds the Katana, its saya and one Fang at every tier exactly as the game would (a WeaponMesh, a
    /// WeaponSculpt on the right hand, the builder), prints each tier's triangles against NativeWeaponModels.Budget (the pair of
    /// fangs counted together; exit code 1 when one is over) and the sockets, and writes each mesh to <dir>/<name>_t<tier>.ply for
    /// a preview render: ASCII PLY in the source weapon frame, per vertex the position, the normal, the linear albedo (red green
    /// blue) and the linear emission (er eg eb) with the accent-tagged paints tinted to the class's default accent.
    /// </summary>
    internal static class BladeModelExport
    {
        private const int KatanaAccent=0xff3d6e,FangsAccent=0x7dff3a;
        public static int Run(string dir)
        {
            Directory.CreateDirectory(dir);bool ok=true;
            ok&=Model(dir,"katana",NativeWeaponModels.BuildKatana,KatanaAccent,1);
            ok&=Model(dir,"saya",NativeWeaponModels.BuildSaya,KatanaAccent,1);
            ok&=Model(dir,"fang",NativeWeaponModels.BuildFang,FangsAccent,2);
            var kd=NativeWeaponModels.KatanaDesign("katana");var fd=NativeWeaponModels.FangsDesign("fangs");
            Console.WriteLine("katana sockets: tip "+F(kd.BladeTip)+" base "+F(kd.BladeBase)+" support "+F(kd.Support)+" stretch "+F(kd.StretchBase)+" .. "+F(kd.StretchTip));
            Console.WriteLine("saya: mouth "+F(NativeWeaponModels.SayaMouth)+" axis "+F(NativeWeaponModels.SayaAxis)+" end "+F(NativeWeaponModels.SayaEnd)+" length "+F(NativeWeaponModels.SayaLength)+" seat "+F(NativeWeaponModels.KatanaSayaSeat));
            Console.WriteLine("fangs sockets: tip "+F(fd.BladeTip)+" base "+F(fd.BladeBase)+" stretch "+F(fd.StretchBase)+" .. "+F(fd.StretchTip)+" dual "+fd.Dual+" mirror "+fd.MirrorLeft);
            Console.WriteLine(ok?"PASS blade models":"FAIL blade models: over budget");
            return ok?0:1;
        }
        private static string F(float v)=>v.ToString("0.####",CultureInfo.InvariantCulture);
        private static string F(Vector3 v)=>"("+F(v.X)+", "+F(v.Y)+", "+F(v.Z)+")";
        private static bool Model(string dir,string name,Action<WeaponSculpt> build,int accent,int copies)
        {
            bool ok=true;var tint=NativePortraitRules.GlowAccentTint(accent);var line=new StringBuilder(name+":");
            for(int t=0;t<NativeWeaponModels.Tiers;t++)
            {
                var mesh=new WeaponMesh{Spec=name,Tier=t};var sculpt=new WeaponSculpt(mesh,t);sculpt.Into(NativeWeaponModels.RightHand);build(sculpt);
                int tris=mesh.Triangles*copies,budget=NativeWeaponModels.Budget[t];bool fits=tris<=budget;ok&=fits;
                line.Append(" tier "+t+" "+tris+"/"+budget+(copies>1?" (the pair)":"")+(fits?"":" OVER"));
                mesh.Bounds(-1,out var min,out var max);if(t==0)line.Append(" bounds "+F(min)+" .. "+F(max)+";");
                Write(Path.Combine(dir,name+"_t"+t+".ply"),mesh,tint);
            }
            Console.WriteLine(line.ToString());return ok;
        }
        private static void Write(string path,WeaponMesh mesh,Vector3 tint)
        {
            int verts=0,faces=0;foreach(var s in mesh.Sections){verts+=s.Vertices;faces+=s.Triangles;}
            var o=new StringBuilder();var inv=CultureInfo.InvariantCulture;
            o.Append("ply\nformat ascii 1.0\nelement vertex "+verts+"\nproperty float x\nproperty float y\nproperty float z\nproperty float nx\nproperty float ny\nproperty float nz\n");
            o.Append("property float red\nproperty float green\nproperty float blue\nproperty float er\nproperty float eg\nproperty float eb\nelement face "+faces+"\nproperty list uchar int vertex_indices\nend_header\n");
            foreach(var s in mesh.Sections)for(int i=0;i<s.Vertices;i++)
            {
                var p=s.Positions[i];var n=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];
                var col=new Vector3(c.X,c.Y,c.Z);var emit=new Vector3(e.X,e.Y,e.Z);if(e.W>1.5f){col*=tint;emit*=tint;}
                o.Append(string.Format(inv,"{0:0.#####} {1:0.#####} {2:0.#####} {3:0.####} {4:0.####} {5:0.####} {6:0.####} {7:0.####} {8:0.####} {9:0.####} {10:0.####} {11:0.####}\n",p.X,p.Y,p.Z,n.X,n.Y,n.Z,col.X,col.Y,col.Z,emit.X,emit.Y,emit.Z));
            }
            int start=0;
            foreach(var s in mesh.Sections){for(int i=0;i+2<s.Indices.Count;i+=3)o.Append("3 "+(start+s.Indices[i])+" "+(start+s.Indices[i+1])+" "+(start+s.Indices[i+2])+"\n");start+=s.Vertices;}
            File.WriteAllText(path,o.ToString());
        }
    }
}
