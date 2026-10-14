using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NVector3=System.Numerics.Vector3;
using NVector4=System.Numerics.Vector4;
using NMatrix=System.Numerics.Matrix4x4;

namespace ChipArena.UnityConversion
{
    // A body split in two (Core NativeBisect), as bank models: the pilot model a corpse draws (its body skin's tier, wound cuts
    // included) and its hat, cut by the plane once, each half registered as two models (body and hat) that draw on that
    // half's own ragdoll bones. A piece wholly on one side is the bank's own part (shared mesh and material); a cut piece is a
    // new mesh on its source part's material; a body's cap is a new mesh on one shared plain material. Built when the
    // corpse is made, released with it; nothing here runs per frame.
    public sealed partial class NativeSourceModels
    {
        /// <summary>Models whose Core arrays are kept as uploaded: the Prototype pilot and the hats (the body skins keep theirs).</summary>
        private static bool Banked(string id)=>id==NativeBodySkins.PrototypeModel||id!=null&&id.StartsWith("hat-",StringComparison.Ordinal);
        private static WeaponSection CoreOf(SourceSection s)
        {
            int n=s.positions.Length/3;var m=new WeaponSection{Bone=s.bone};
            for(int i=0;i<n;i++){m.Positions.Add(new NVector3(s.positions[i*3],s.positions[i*3+1],s.positions[i*3+2]));m.Normals.Add(new NVector3(s.normals[i*3],s.normals[i*3+1],s.normals[i*3+2]));m.Colors.Add(new NVector4(s.colors[i*4],s.colors[i*4+1],s.colors[i*4+2],s.colors[i*4+3]));m.Emission.Add(new NVector4(s.emission[i*4],s.emission[i*4+1],s.emission[i*4+2],s.emission[i*4+3]));}
            m.Indices.AddRange(s.indices);return m;
        }
        private static WeaponSection CoreOf(IList<Vector3> v,IList<Vector3> n,IList<Color> c,IList<Vector4> e,IList<int> indices)
        {
            var m=new WeaponSection();
            for(int i=0;i<v.Count;i++){m.Positions.Add(new NVector3(v[i].x,v[i].y,v[i].z));m.Normals.Add(new NVector3(n[i].x,n[i].y,n[i].z));m.Colors.Add(new NVector4(c[i].r,c[i].g,c[i].b,c[i].a));m.Emission.Add(new NVector4(e[i].x,e[i].y,e[i].z,e[i].w));}
            m.Indices.AddRange(indices);return m;
        }
        /// <summary>A carved Prototype part's boxes as CarveMesh uploads them (NativeWoundGeometry.cs), in Core arrays.</summary>
        private static WeaponSection CarveCore(NativeWoundPart d,List<WoundSolid> solids)
        {
            var m=new NMatrix(d.matrix[0],d.matrix[1],d.matrix[2],d.matrix[3],d.matrix[4],d.matrix[5],d.matrix[6],d.matrix[7],d.matrix[8],d.matrix[9],d.matrix[10],d.matrix[11],d.matrix[12],d.matrix[13],d.matrix[14],d.matrix[15]);
            m=NMatrix.CreateScale((float)(1/d.size.x),(float)(1/d.size.y),(float)(1/d.size.z))*m;NMatrix.Invert(m,out var inv);var nm=NMatrix.Transpose(inv);var w=new WeaponSection{Bone=d.bone};
            var corners=new[]{new System.Numerics.Vector2(-1,-1),new System.Numerics.Vector2(1,-1),new System.Numerics.Vector2(1,1),new System.Numerics.Vector2(-1,1)};
            foreach(var box in solids)
            {
                var mid=(box.min+box.max)*.5;var ext=(box.max-box.min)*.5;var tint=new NVector3((float)box.color.x,(float)box.color.y,(float)box.color.z);
                var paint=box.flesh?tint:new NVector3(d.color[0]*tint.X,d.color[1]*tint.Y,d.color[2]*tint.Z);var em=box.flesh?new NVector4(tint*.3f,0):new NVector4(d.emission[0]*tint.X,d.emission[1]*tint.Y,d.emission[2]*tint.Z,d.tag);
                for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1})
                {
                    var n=NVector3.Zero;if(axis==0)n.X=sign;else if(axis==1)n.Y=sign;else n.Z=sign;var u=axis==0?NVector3.UnitY:axis==1?NVector3.UnitZ:NVector3.UnitX;var v=NVector3.Cross(n,u);int start=w.Positions.Count;
                    foreach(var c in corners)
                    {
                        var q=n+u*c.X+v*c.Y;var p=NVector3.Transform(new NVector3((float)(mid.x+q.X*ext.x),(float)(mid.y+q.Y*ext.y),(float)(mid.z+q.Z*ext.z)),m);p.X=-p.X;var nn=NVector3.Normalize(NVector3.TransformNormal(n,nm));nn.X=-nn.X;
                        w.Positions.Add(p);w.Normals.Add(nn);w.Colors.Add(new NVector4(paint,1));w.Emission.Add(em);
                    }
                    w.Indices.Add(start);w.Indices.Add(start+2);w.Indices.Add(start+1);w.Indices.Add(start);w.Indices.Add(start+3);w.Indices.Add(start+2);
                }
            }
            return w;
        }
        private sealed class Halves{public readonly List<Part> Owned=new List<Part>();public string[] Models;}
        private readonly Dictionary<string,Halves> halves=new Dictionary<string,Halves>();private readonly Dictionary<string,Part[]> jobParts=new Dictionary<string,Part[]>();private Material capMaterial;
        /// <summary>Bank models currently held for bisected bodies (the checks and the dev overlay).</summary>
        public int BisectModels=>halves.Count;
        private Material CapMaterial()
        {
            if(capMaterial!=null)return capMaterial;var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)return null;
            capMaterial=new Material(shader){name="Bisect cut",enableInstancing=true};capMaterial.SetInt("_Cull",2);capMaterial.SetFloat("_Ink",0);materials.Add(capMaterial);return capMaterial;
        }
        private static NMatrix Num(Matrix4x4 m)=>new NMatrix(m.m00,m.m10,m.m20,m.m30,m.m01,m.m11,m.m21,m.m31,m.m02,m.m12,m.m22,m.m32,m.m03,m.m13,m.m23,m.m33);
        /// <summary>A split being made: the meshes to cut (gathered on the main thread from the bank), the cut once it is
        /// done (possibly on a worker: Core NativeBisect.Split only reads them), and the models it became.</summary>
        public sealed class BisectJob
        {
            public string Key;public List<BisectSource> Sources;public NMatrix[] Draw;public BisectPlane Plane;public BisectLook Look;public int Tier;public uint Seed,Mask=uint.MaxValue;
            public volatile BisectCut Cut;public System.Threading.Tasks.Task Task;public Exception Error;public string[] Names;public bool Finished;
            public bool Done=>Finished||(Task==null?Cut!=null||Error!=null:Task.IsCompleted);
            public void Split(){try{Cut=NativeBisect.Split(Sources,Draw,Plane,new[]{Look,BisectLook.Hat},Tier,Seed,Mask);}catch(Exception e){Error=e;}}
        }
        /// <summary>
        /// Gathers model `body` and hat model `hat` (null for none), posed by `bind` (each joint's draw matrix: mesh to world, as a
        /// corpse's Bind), for a split along `plane` under `key`; `look` is the body's cap (NativeBodyModels.CutLook), `tier` its
        /// detail (cap cells), `mask` the bodies the cut may cross (BisectRequest.Mask). Null when a model is missing or lacks
        /// its Core arrays (the caller keeps the whole body).
        /// </summary>
        public BisectJob BisectBegin(string key,string body,string hat,Matrix4x4[] bind,in BisectPlane plane,BisectLook look,int tier,uint seed,uint mask=uint.MaxValue)
        {
            ReleaseBisect(key);if(body==null||!models.TryGetValue(body,out var bodyParts))return null;
            Part[] hatParts=hat!=null&&models.TryGetValue(hat,out var hp)?hp:Array.Empty<Part>();
            var parts=new List<Part>();var sources=new List<BisectSource>();
            foreach(var p in bodyParts){if(p.Core==null||p.Bone<0||p.Bone>=22)return null;parts.Add(p);sources.Add(new BisectSource{Bone=p.Bone,Group=0,Mesh=p.Core,Fill=p.Fill,Cap=!p.Open});}
            foreach(var p in hatParts){if(p.Core==null||p.Bone<0||p.Bone>=22)continue;parts.Add(p);sources.Add(new BisectSource{Bone=p.Bone,Group=1,Mesh=p.Core,Cap=!p.Open});}
            var draw=new NMatrix[22];for(int b=0;b<22;b++)draw[b]=Num(bind[b]);
            jobParts[key]=parts.ToArray();return new BisectJob{Key=key,Sources=sources,Draw=draw,Plane=plane,Look=look,Tier=tier,Seed=seed,Mask=mask};
        }
        /// <summary>Runs the split now, or on a worker thread (`background`; it touches nothing of Unity's).</summary>
        public static void BisectRun(BisectJob job,bool background)
        {
            if(job==null||job.Task!=null||job.Cut!=null)return;
            if(background)try{job.Task=System.Threading.Tasks.Task.Run(job.Split);return;}catch(Exception){job.Task=null;}
            job.Split();
        }
        /// <summary>Waits for the split if it is still running, then uploads its pieces and registers the halves under the job's
        /// key: [0] and [1] the body's halves, [2] and [3] the hat's (null without one). Null when the split failed.</summary>
        public string[] BisectFinish(BisectJob job)
        {
            if(job==null)return null;if(job.Finished)return job.Names;job.Finished=true;
            if(job.Task!=null&&!job.Task.IsCompleted)try{job.Task.Wait();}catch(Exception e){job.Error=e;}
            var cut=job.Cut;if(cut==null){if(job.Error!=null)Debug.LogWarning("Bisect failed: "+job.Error.Message);return null;}
            if(!jobParts.TryGetValue(job.Key,out var parts))return null;jobParts.Remove(job.Key);var made=new Halves();var lists=new[]{new List<Part>(),new List<Part>(),new List<Part>(),new List<Part>()};var cap=CapMaterial();
            // A wound's own mesh is released with its wounds (a revived pilot's new set): a half keeps its own copy of one.
            var transient=new HashSet<Part>();foreach(var c in wounds.Values.Concat(woundsB.Values))if(c.Owned!=null)foreach(var p in c.Owned)transient.Add(p);foreach(var c in skinCuts.Values)foreach(var p in c.Owned)transient.Add(p);
            bool hat=false;
            foreach(var piece in cut.Pieces)
            {
                var list=lists[(piece.Group==1?2:0)+piece.Half];if(piece.Group==1)hat=true;
                if(piece.Whole)
                {
                    var whole=parts[piece.Source];if(!transient.Contains(whole)&&whole.Mesh!=null){list.Add(whole);continue;}
                    var copy=BodyMesh(whole.Core,job.Key+" / "+whole.Name+" "+piece.Half);if(copy==null)continue;var kept=new Part{Name=whole.Name,Bone=whole.Bone,Mesh=copy,Material=whole.Material,NoShadow=whole.NoShadow};made.Owned.Add(kept);list.Add(kept);continue;
                }
                Part part;
                if(piece.IsCap){if(cap==null)continue;var mesh=BodyMesh(piece.Mesh,job.Key+" / cut "+piece.Half+" / joint "+piece.Bone);if(mesh==null)continue;part=new Part{Name="cut",Bone=piece.Bone,Mesh=mesh,Material=cap,NoShadow=true};}
                else{var source=parts[piece.Source];var mesh=BodyMesh(piece.Mesh,job.Key+" / "+source.Name+" "+piece.Half);if(mesh==null)continue;part=new Part{Name=source.Name,Bone=source.Bone,Mesh=mesh,Material=source.Material,NoShadow=source.NoShadow};}
                made.Owned.Add(part);list.Add(part);
            }
            var names=new string[4];
            for(int k=0;k<4;k++){if(k>=2&&!hat)continue;names[k]=job.Key+"/"+k;models[names[k]]=lists[k].ToArray();}
            made.Models=names;halves[job.Key]=made;job.Names=names;return names;
        }
        /// <summary>A split made at once (a body seen long after its death, another detail tier, the Dev Sandbox preview).</summary>
        public string[] Bisect(string key,string body,string hat,Matrix4x4[] bind,in BisectPlane plane,BisectLook look,int tier,uint seed,out BisectCut cut,uint mask=uint.MaxValue)
        {
            var job=BisectBegin(key,body,hat,bind,plane,look,tier,seed,mask);cut=null;if(job==null)return null;BisectRun(job,false);var names=BisectFinish(job);cut=job.Cut;return names;
        }
        /// <summary>Drops a bisected body's models and the meshes they own (and every draw queued on them).</summary>
        public void ReleaseBisect(string key)
        {
            if(key==null)return;jobParts.Remove(key);if(!halves.TryGetValue(key,out var made))return;halves.Remove(key);
            foreach(var name in made.Models)if(name!=null)models.Remove(name);
            foreach(var part in made.Owned)
            {
                foreach(var k in draws.Keys.Where(k=>k.Item1==part).ToArray())draws.Remove(k);
                if(shadowlessParts.TryGetValue(part,out var twin)){shadowlessParts.Remove(part);if(twin!=null)foreach(var k in draws.Keys.Where(k=>k.Item1==twin).ToArray())draws.Remove(k);}
                CapturedAnimation.Release(part.Mesh);
            }
        }
        private void ClearBisects(){foreach(var key in halves.Keys.ToArray())ReleaseBisect(key);}
    }
}
