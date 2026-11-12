using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The home screen's modelled rooftop (owner 2026-11-11: the menu out of its boxy look; docs/MENU-ROOFTOP-ART.md; Core
    /// NativeRooftopBank): the local bank ChipArenaStageRooftop read once per quality tier into two meshes, the solid scenery
    /// (our roof, the towers' built detail, the actors' roofs, the screens' frames: ChipArena/NativeRooftopModel) and the towers'
    /// walls (the menu's own facade material: their bays and rooms stay procedural), and the flying cars, drawn instanced
    /// along Core's lanes. NativeRooftopScene draws it in place of the quads its bank covers. Without the bank (CI, a build
    /// without the Drive zip, `--ca-no-pack-banks`) Load returns null and every quad draws exactly as before. Presentation only.
    /// </summary>
    public sealed class NativeRooftopModels:IDisposable
    {
        public const string ShaderName="NativeRooftopModel";public const int CarCapacity=128;
        public readonly NativeRooftopBank.Plan Plan;public readonly int Triangles,Vertices;
        /// <summary>The worker's time reading the bank, and the main thread's time in Load (its wait for the worker included), ms.</summary>
        public readonly float ReadMs,LoadMs;
        private readonly Mesh solid,facade;private readonly Mesh[] carNear,carFar;private readonly Material material;
        private readonly Matrix4x4[][] nearPlace,farPlace;private readonly int[] nearCount,farCount;private readonly bool instanced;
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*8000);
        public bool HasCars=>carNear.Length>0;
        // A mesh's arrays, made off the main thread (plain structs: no engine call), handed to a Mesh on it.
        private sealed class Arrays {public Vector3[] Pos,Nrm;public Vector2[] Uv0,Uv1;public Vector4[] Wide0,Wide1;public Color32[] Col;public int[] Tri;}
        private sealed class Prepared {public NativeRooftopBank.Plan Plan;public Arrays Solid,Facade;public Arrays[] CarNear,CarFar;public bool Low;public float Ms;}
        private static System.Threading.Tasks.Task<Prepared> job;private static bool jobLow;
        /// <summary>
        /// Starts reading the bank for the tier on a worker thread (the bytes are taken here, on the main thread; the parse and
        /// every vertex array are the worker's), so a later <see cref="Load"/> only hands the arrays to its meshes. Anything
        /// that shows before the home screen (a start-up intro) may call it; Load calls it itself otherwise and waits.
        /// </summary>
        public static void Prewarm(bool low)
        {
            if(job!=null&&jobLow==low||NativePackBanks.Off)return;
            var asset=Resources.Load<TextAsset>(NativeRooftopBank.Name);if(asset==null){job=null;return;}
            byte[] bytes=asset.bytes;Resources.UnloadAsset(asset);jobLow=low;
            job=System.Threading.Tasks.Task.Run(()=>
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();var bank=NativePackBank.Read(bytes);var plan=NativeRooftopBank.Read(bank,low);if(plan==null)return null;
                var p=new Prepared{Plan=plan,Low=low,Solid=Solid(bank,plan.Static),Facade=Facade(bank,plan.Facade),CarNear=new Arrays[plan.Cars.Count],CarFar=new Arrays[plan.Cars.Count]};
                var one=new List<int>(1){0};
                for(int k=0;k<plan.Cars.Count;k++)
                {
                    one[0]=plan.CarNear[k];p.CarNear[k]=Solid(bank,one);
                    if(plan.CarFar[k]!=plan.CarNear[k]&&plan.CarFar[k]>=0){one[0]=plan.CarFar[k];p.CarFar[k]=Solid(bank,one);}
                }
                p.Ms=(float)watch.Elapsed.TotalMilliseconds;return p;
            });
        }
        /// <summary>The bank's art for the tier, or null (no bank, no shader, an unreadable bank: a warning).</summary>
        public static NativeRooftopModels Load(bool low)
        {
            if(NativePackBanks.Off)return null;var shader=Resources.Load<Shader>(ShaderName);if(shader==null||!shader.isSupported)return null;
            float t0=Time.realtimeSinceStartup;Prewarm(low);var task=job;job=null;if(task==null)return null;
            Prepared p;try{p=task.Result;}catch(Exception e){Debug.LogWarning("The rooftop art did not read ("+e.GetBaseException().Message+"); the menu draws its quads.");return null;}
            return p==null?null:new NativeRooftopModels(p,shader,t0);
        }
        private NativeRooftopModels(Prepared p,Shader shader,float t0)
        {
            Plan=p.Plan;material=new Material(shader){name="Rooftop models",enableInstancing=true,hideFlags=HideFlags.HideAndDontSave};instanced=SystemInfo.supportsInstancing;
            solid=Make("Rooftop models",p.Solid);facade=Make("Rooftop model facades",p.Facade);
            int kinds=Plan.Cars.Count;carNear=new Mesh[kinds];carFar=new Mesh[kinds];nearPlace=new Matrix4x4[kinds][];farPlace=new Matrix4x4[kinds][];nearCount=new int[kinds];farCount=new int[kinds];
            for(int k=0;k<kinds;k++)
            {
                carNear[k]=Make("Rooftop car "+Plan.Cars[k],p.CarNear[k]);carFar[k]=p.CarFar[k]==null?carNear[k]:Make("Rooftop car "+Plan.Cars[k]+" far",p.CarFar[k]);
                nearPlace[k]=new Matrix4x4[CarCapacity];farPlace[k]=new Matrix4x4[CarCapacity];
            }
            foreach(var m in new[]{solid,facade}){if(m==null)continue;Vertices+=m.vertexCount;Triangles+=(int)(m.GetIndexCount(0)/3);m.UploadMeshData(true);}
            ReadMs=p.Ms;LoadMs=(Time.realtimeSinceStartup-t0)*1000;
        }
        // The arrays as a mesh (the solid layout: colours and two 2D channels; the facade's: two 4D channels).
        private static Mesh Make(string name,Arrays a)
        {
            if(a==null)return null;
            var mesh=new Mesh{name=name,indexFormat=a.Pos.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16,hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=a.Pos;mesh.normals=a.Nrm;
            if(a.Wide0!=null){mesh.SetUVs(0,a.Wide0);mesh.SetUVs(1,a.Wide1);}else{mesh.uv=a.Uv0;mesh.uv2=a.Uv1;mesh.colors32=a.Col;}
            mesh.triangles=a.Tri;mesh.bounds=Everywhere;return mesh;
        }
        // The bank's place (System.Numerics: row vectors, the translation in its fourth row, as NativePackBanks.Place reads it)
        // applied to a point and to a direction.
        private static Vector3 Point(System.Numerics.Matrix4x4 m,System.Numerics.Vector3 p)=>new Vector3(m.M11*p.X+m.M21*p.Y+m.M31*p.Z+m.M41,m.M12*p.X+m.M22*p.Y+m.M32*p.Z+m.M42,m.M13*p.X+m.M23*p.Y+m.M33*p.Z+m.M43);
        private static Vector3 Turn(System.Numerics.Matrix4x4 m,System.Numerics.Vector3 p)=>new Vector3(m.M11*p.X+m.M21*p.Y+m.M31*p.Z,m.M12*p.X+m.M22*p.Y+m.M32*p.Z,m.M13*p.X+m.M23*p.Y+m.M33*p.Z).normalized;
        // The props' meshes as one mesh's arrays for the model shader (their sub-meshes too: a glowing finish is a vertex's kind).
        private static Arrays Solid(NativePackBank.Bank bank,List<int> props)
        {
            int n=0,ix=0;foreach(int p in props)foreach(var part in bank.Props[p].Parts){var src=bank.Meshes[part.Mesh];if(src.Positions==null||src.Colors==null||src.Uv1==null)continue;n+=src.Positions.Length;foreach(var s in src.Submeshes)ix+=s.Indices.Length;}
            if(n==0)return null;
            var a=new Arrays{Pos=new Vector3[n],Nrm=new Vector3[n],Uv0=new Vector2[n],Uv1=new Vector2[n],Col=new Color32[n],Tri=new int[ix]};int v=0,t=0;
            foreach(int p in props)foreach(var part in bank.Props[p].Parts)
            {
                var src=bank.Meshes[part.Mesh];if(src.Positions==null||src.Colors==null||src.Uv1==null)continue;var place=part.Place;bool move=!place.IsIdentity;int count=src.Positions.Length;
                for(int i=0;i<count;i++)
                {
                    var q=src.Positions[i];var nn=src.Normals[i];
                    a.Pos[v+i]=move?Point(place,q):new Vector3(q.X,q.Y,q.Z);a.Nrm[v+i]=move?Turn(place,nn):new Vector3(nn.X,nn.Y,nn.Z);a.Uv0[v+i]=new Vector2(src.Uv0[i].X,src.Uv0[i].Y);a.Uv1[v+i]=new Vector2(src.Uv1[i].X,src.Uv1[i].Y);
                    a.Col[v+i]=new Color32(src.Colors[i*4],src.Colors[i*4+1],src.Colors[i*4+2],src.Colors[i*4+3]);
                }
                foreach(var s in src.Submeshes){var ind=s.Indices;for(int i=0;i<ind.Length;i++)a.Tri[t+i]=ind[i]+v;t+=ind.Length;}
                v+=count;
            }
            return a;
        }
        // The facade props as one mesh's arrays in the facade shader's layout (NativeRooftopFacade: uv0 the face's metric
        // coordinates, the tower mode and its top; uv1 its seed, style, hue and lit share).
        private static Arrays Facade(NativePackBank.Bank bank,List<int> props)
        {
            int n=0,ix=0;foreach(int p in props)foreach(var part in bank.Props[p].Parts){var src=bank.Meshes[part.Mesh];if(src.Positions==null||src.Colors==null||src.Uv1==null)continue;n+=src.Positions.Length;foreach(var s in src.Submeshes)ix+=s.Indices.Length;}
            if(n==0)return null;
            var a=new Arrays{Pos=new Vector3[n],Nrm=new Vector3[n],Wide0=new Vector4[n],Wide1=new Vector4[n],Tri=new int[ix]};int v=0,t=0;
            foreach(int p in props)foreach(var part in bank.Props[p].Parts)
            {
                var src=bank.Meshes[part.Mesh];if(src.Positions==null||src.Colors==null||src.Uv1==null)continue;int count=src.Positions.Length;
                for(int i=0;i<count;i++)
                {
                    a.Pos[v+i]=new Vector3(src.Positions[i].X,src.Positions[i].Y,src.Positions[i].Z);a.Nrm[v+i]=new Vector3(src.Normals[i].X,src.Normals[i].Y,src.Normals[i].Z);
                    NativeRooftopBank.Facade(src.Uv1[i].Y,out float style,out float share);
                    a.Wide0[v+i]=new Vector4(src.Uv0[i].X,src.Positions[i].Y,NativeRooftopCity.Tower,src.Uv0[i].Y);a.Wide1[v+i]=new Vector4(src.Uv1[i].X,style,src.Colors[i*4]/255f,share);
                }
                foreach(var s in src.Submeshes){var ind=s.Indices;for(int i=0;i<ind.Length;i++)a.Tri[t+i]=ind[i]+v;t+=ind.Length;}
                v+=count;
            }
            return a;
        }
        /// <summary>The scenery for `camera`: the solid meshes in their own material, the walls in the scene's facade material.</summary>
        public void Draw(Camera camera,Material facadeMaterial)
        {
            if(solid!=null)Graphics.DrawMesh(solid,Matrix4x4.identity,material,0,camera,0,null,ShadowCastingMode.Off,false);
            if(facade!=null)Graphics.DrawMesh(facade,Matrix4x4.identity,facadeMaterial,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        /// <summary>The flying cars along the lanes at `time` (Core NativeRooftopCity.CarU / LanePoint: the box cars' places),
        /// the near lane's in their fine models. Allocation free.</summary>
        public void Cars(Camera camera,List<RooftopLane> lanes,float time)
        {
            int kinds=carNear.Length;if(kinds==0)return;for(int k=0;k<kinds;k++){nearCount[k]=0;farCount[k]=0;}
            for(int l=0;l<lanes.Count;l++)
            {
                var lane=lanes[l];
                for(int k=0;k<lane.Count;k++)
                {
                    var p=NativeRooftopCity.LanePoint(lane,NativeRooftopCity.CarU(lane,k,time),out var tg);var f=tg*lane.Dir;int kind=NativeRooftopBank.CarKind(l,k,kinds);
                    var place=Matrix4x4.TRS(new Vector3(-p.X,p.Y,p.Z),Quaternion.LookRotation(new Vector3(-f.X,f.Y,f.Z),Vector3.up),Vector3.one);
                    if(lane.Near){if(nearCount[kind]<CarCapacity)nearPlace[kind][nearCount[kind]++]=place;}
                    else if(farCount[kind]<CarCapacity)farPlace[kind][farCount[kind]++]=place;
                }
            }
            for(int k=0;k<kinds;k++){Batch(camera,carNear[k],nearPlace[k],nearCount[k]);Batch(camera,carFar[k],farPlace[k],farCount[k]);}
        }
        private void Batch(Camera camera,Mesh mesh,Matrix4x4[] places,int count)
        {
            if(mesh==null||count==0)return;
            if(instanced)Graphics.DrawMeshInstanced(mesh,0,material,places,count,null,ShadowCastingMode.Off,false,0,camera);
            else for(int i=0;i<count;i++)Graphics.DrawMesh(mesh,places[i],material,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose()
        {
            var seen=new HashSet<Mesh>();foreach(var m in new[]{solid,facade})if(m!=null&&seen.Add(m))CapturedAnimation.Release(m);
            foreach(var m in carNear)if(m!=null&&seen.Add(m))CapturedAnimation.Release(m);foreach(var m in carFar)if(m!=null&&seen.Add(m))CapturedAnimation.Release(m);
            if(material!=null)CapturedAnimation.Release(material);
        }
    }
}
