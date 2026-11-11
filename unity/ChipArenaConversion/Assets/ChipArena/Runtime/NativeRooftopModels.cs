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
        public readonly NativeRooftopBank.Plan Plan;public readonly int Triangles,Vertices;public readonly float LoadMs;
        private readonly Mesh solid,facade;private readonly Mesh[] carNear,carFar;private readonly Material material;
        private readonly Matrix4x4[][] nearPlace,farPlace;private readonly int[] nearCount,farCount;private readonly bool instanced;
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*8000);
        public bool HasCars=>carNear.Length>0;
        /// <summary>The bank's art for the tier, or null (no bank, no shader, an unreadable bank: a warning).</summary>
        public static NativeRooftopModels Load(bool low)
        {
            if(NativePackBanks.Off)return null;var shader=Resources.Load<Shader>(ShaderName);if(shader==null||!shader.isSupported)return null;
            var asset=Resources.Load<TextAsset>(NativeRooftopBank.Name);if(asset==null)return null;
            float t0=Time.realtimeSinceStartup;NativePackBank.Bank bank;
            try{bank=NativePackBank.Read(asset.bytes);}catch(Exception e){Debug.LogWarning("The rooftop art did not read ("+e.Message+"); the menu draws its quads.");return null;}finally{Resources.UnloadAsset(asset);}
            var plan=NativeRooftopBank.Read(bank,low);if(plan==null)return null;
            return new NativeRooftopModels(bank,plan,shader,t0);
        }
        private NativeRooftopModels(NativePackBank.Bank bank,NativeRooftopBank.Plan plan,Shader shader,float t0)
        {
            Plan=plan;material=new Material(shader){name="Rooftop models",enableInstancing=true,hideFlags=HideFlags.HideAndDontSave};instanced=SystemInfo.supportsInstancing;
            solid=Solid("Rooftop models",bank,plan.Static);facade=Facade("Rooftop model facades",bank,plan.Facade);
            int kinds=plan.Cars.Count;carNear=new Mesh[kinds];carFar=new Mesh[kinds];nearPlace=new Matrix4x4[kinds][];farPlace=new Matrix4x4[kinds][];nearCount=new int[kinds];farCount=new int[kinds];
            var one=new List<int>(1){0};
            for(int k=0;k<kinds;k++)
            {
                one[0]=plan.CarNear[k];carNear[k]=Solid("Rooftop car "+plan.Cars[k],bank,one);
                if(plan.CarFar[k]==plan.CarNear[k]||plan.CarFar[k]<0)carFar[k]=carNear[k];else{one[0]=plan.CarFar[k];carFar[k]=Solid("Rooftop car "+plan.Cars[k]+" far",bank,one);}
                nearPlace[k]=new Matrix4x4[CarCapacity];farPlace[k]=new Matrix4x4[CarCapacity];
            }
            foreach(var m in new[]{solid,facade}){if(m==null)continue;Vertices+=m.vertexCount;Triangles+=(int)(m.GetIndexCount(0)/3);m.UploadMeshData(true);}
            LoadMs=(Time.realtimeSinceStartup-t0)*1000;
        }
        // The props' meshes as one mesh for the model shader (their sub-meshes too: a glowing finish is a vertex's kind).
        private static Mesh Solid(string name,NativePackBank.Bank bank,List<int> props)
        {
            int n=0,ix=0;foreach(int p in props)foreach(var part in bank.Props[p].Parts){var src=bank.Meshes[part.Mesh];if(src.Positions==null||src.Colors==null||src.Uv1==null)continue;n+=src.Positions.Length;foreach(var s in src.Submeshes)ix+=s.Indices.Length;}
            if(n==0)return null;
            var pos=new Vector3[n];var nrm=new Vector3[n];var uv0=new Vector2[n];var uv1=new Vector2[n];var col=new Color32[n];var tri=new int[ix];int v=0,t=0;
            foreach(int p in props)foreach(var part in bank.Props[p].Parts)
            {
                var src=bank.Meshes[part.Mesh];if(src.Positions==null||src.Colors==null||src.Uv1==null)continue;var place=NativePackBanks.Place(part.Place);bool move=!place.isIdentity;int count=src.Positions.Length;
                for(int i=0;i<count;i++)
                {
                    var a=new Vector3(src.Positions[i].X,src.Positions[i].Y,src.Positions[i].Z);var b=new Vector3(src.Normals[i].X,src.Normals[i].Y,src.Normals[i].Z);
                    pos[v+i]=move?place.MultiplyPoint3x4(a):a;nrm[v+i]=move?place.MultiplyVector(b).normalized:b;uv0[v+i]=new Vector2(src.Uv0[i].X,src.Uv0[i].Y);uv1[v+i]=new Vector2(src.Uv1[i].X,src.Uv1[i].Y);
                    col[v+i]=new Color32(src.Colors[i*4],src.Colors[i*4+1],src.Colors[i*4+2],src.Colors[i*4+3]);
                }
                foreach(var s in src.Submeshes){var ind=s.Indices;for(int i=0;i<ind.Length;i++)tri[t+i]=ind[i]+v;t+=ind.Length;}
                v+=count;
            }
            var mesh=new Mesh{name=name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16,hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=pos;mesh.normals=nrm;mesh.uv=uv0;mesh.uv2=uv1;mesh.colors32=col;mesh.triangles=tri;mesh.bounds=Everywhere;return mesh;
        }
        // The facade props as one mesh in the facade shader's layout (NativeRooftopFacade: uv0 the face's metric coordinates, the
        // tower mode and its top; uv1 its seed, style, hue and lit share).
        private static Mesh Facade(string name,NativePackBank.Bank bank,List<int> props)
        {
            int n=0,ix=0;foreach(int p in props)foreach(var part in bank.Props[p].Parts){var src=bank.Meshes[part.Mesh];if(src.Positions==null||src.Colors==null||src.Uv1==null)continue;n+=src.Positions.Length;foreach(var s in src.Submeshes)ix+=s.Indices.Length;}
            if(n==0)return null;
            var pos=new Vector3[n];var nrm=new Vector3[n];var uv0=new Vector4[n];var uv1=new Vector4[n];var tri=new int[ix];int v=0,t=0;
            foreach(int p in props)foreach(var part in bank.Props[p].Parts)
            {
                var src=bank.Meshes[part.Mesh];if(src.Positions==null||src.Colors==null||src.Uv1==null)continue;int count=src.Positions.Length;
                for(int i=0;i<count;i++)
                {
                    pos[v+i]=new Vector3(src.Positions[i].X,src.Positions[i].Y,src.Positions[i].Z);nrm[v+i]=new Vector3(src.Normals[i].X,src.Normals[i].Y,src.Normals[i].Z);
                    NativeRooftopBank.Facade(src.Uv1[i].Y,out float style,out float share);
                    uv0[v+i]=new Vector4(src.Uv0[i].X,src.Positions[i].Y,NativeRooftopCity.Tower,src.Uv0[i].Y);uv1[v+i]=new Vector4(src.Uv1[i].X,style,src.Colors[i*4]/255f,share);
                }
                foreach(var s in src.Submeshes){var ind=s.Indices;for(int i=0;i<ind.Length;i++)tri[t+i]=ind[i]+v;t+=ind.Length;}
                v+=count;
            }
            var mesh=new Mesh{name=name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16,hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=pos;mesh.normals=nrm;mesh.SetUVs(0,uv0);mesh.SetUVs(1,uv1);mesh.triangles=tri;mesh.bounds=Everywhere;return mesh;
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
