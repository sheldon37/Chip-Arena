using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class SourceEnvelope {public int schema;public string commit,data;}
    [Serializable] public sealed class SourceModelSet {public SourceModel[] models;public SourceTexture[] textures;}
    [Serializable] public sealed class SourceModel {public string id;public SourceSection[] sections;}
    [Serializable] public sealed class SourceTexture {public int width,height,channels;public string pixels;public float[] repeat;}
    [Serializable] public sealed class SourceSection
    {public int bone,texture;public bool transparent,doubleSide,ink;public float[] positions,normals,colors,emission,uv;public int[] indices;}
    // Source-authored geometry is converted once at build time. Batches are shared across all actors using that model.
    public sealed class NativeSourceModels:IDisposable
    {
        private sealed class Part {public int Bone;public Mesh Mesh;public Material Material;}
        private sealed class Draws {public Part Part;public int Layer;public Camera Camera;public readonly List<Matrix4x4> Matrices=new List<Matrix4x4>();public readonly List<Vector4> Bodies=new List<Vector4>(),Accents=new List<Vector4>(),Tints=new List<Vector4>();}
        private readonly Dictionary<string,Part[]> models=new Dictionary<string,Part[]>();private readonly Dictionary<(Part,int),Draws> draws=new Dictionary<(Part,int),Draws>();
        private readonly List<Texture2D> textures=new List<Texture2D>();private readonly List<Material> materials=new List<Material>();private readonly Matrix4x4[] buffer=new Matrix4x4[512];private readonly Vector4[] bodies=new Vector4[512],accents=new Vector4[512],tints=new Vector4[512];private readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        public static string Decode(string resource)
        {var asset=Resources.Load<TextAsset>(resource);if(asset==null)throw new InvalidOperationException(resource+" is missing");var envelope=JsonUtility.FromJson<SourceEnvelope>(asset.text);if(envelope.schema!=1||envelope.commit!="05950eaef80ff2483a9c44d898cad98be1a76bbc")throw new InvalidDataException("Unverified "+resource);using(var bytes=new MemoryStream(Convert.FromBase64String(envelope.data)))using(var zip=new GZipStream(bytes,CompressionMode.Decompress))using(var reader=new StreamReader(zip))return reader.ReadToEnd();}
        public NativeSourceModels()
        {
            var data=JsonUtility.FromJson<SourceModelSet>(Decode("SourceModels"));
            foreach(var d in data.textures){if(d.channels!=4)throw new InvalidDataException("Unexpected texture format");var tex=new Texture2D(d.width,d.height,TextureFormat.RGBA32,true,false);tex.name="Source procedural texture";tex.LoadRawTextureData(Convert.FromBase64String(d.pixels));tex.wrapMode=TextureWrapMode.Repeat;tex.Apply(true,true);textures.Add(tex);}
            foreach(var model in data.models)
            {
                var parts=new List<Part>();foreach(var s in model.sections)
                {
                    int count=s.positions.Length/3;if(count==0)continue;if(s.normals.Length!=count*3||s.colors.Length!=count*4||s.emission.Length!=count*4||s.uv.Length!=count*2||s.indices.Any(i=>i<0||i>=count))throw new InvalidDataException("Invalid source model "+model.id);
                    var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];var emission=new List<Vector4>(count);var uv=new Vector2[count];
                    for(int i=0;i<count;i++){vertices[i]=new Vector3(s.positions[i*3],s.positions[i*3+1],s.positions[i*3+2]);normals[i]=new Vector3(s.normals[i*3],s.normals[i*3+1],s.normals[i*3+2]);colors[i]=new Color(s.colors[i*4],s.colors[i*4+1],s.colors[i*4+2],s.colors[i*4+3]);emission.Add(new Vector4(s.emission[i*4],s.emission[i*4+1],s.emission[i*4+2],s.emission[i*4+3]));uv[i]=new Vector2(s.uv[i*2],s.uv[i*2+1]);}
                    var mesh=new Mesh{name=model.id+" / joint "+s.bone,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,emission);mesh.triangles=s.indices;mesh.RecalculateBounds();mesh.UploadMeshData(true);
                    var shader=Resources.Load<Shader>(s.transparent?"NativeSourceTransparent":"NativeSourceLit");if(shader==null)throw new InvalidOperationException("Source material shader is missing");var material=new Material(shader){enableInstancing=true};material.SetInt("_Cull",s.doubleSide?0:2);material.SetFloat("_Ink",s.ink?1:0);
                    if(s.texture>=0){material.mainTexture=textures[s.texture];var repeat=data.textures[s.texture].repeat;material.mainTextureScale=new Vector2(repeat[0],repeat[1]);}materials.Add(material);parts.Add(new Part{Bone=s.bone,Mesh=mesh,Material=material});
                }models.Add(model.id,parts.ToArray());
            }
        }
        public void Begin(){foreach(var d in draws.Values){d.Matrices.Clear();d.Bodies.Clear();d.Accents.Clear();d.Tints.Clear();}}
        public bool Has(string id)=>id!=null&&models.ContainsKey(id);
        public void Queue(string id,Matrix4x4 root,Func<int,Matrix4x4> bone=null,Func<int,bool> visible=null,int layer=0,Camera camera=null,Color? body=null,Color? accent=null,float opacity=1,Func<int,float> partOpacity=null,Func<int,Color> partTint=null)
        {
            if(!models.TryGetValue(id,out var model))return;Color bc=body??Color.white,ac=accent??Color.white;
            foreach(var part in model){if(visible!=null&&!visible(part.Bone))continue;var key=(part,layer);if(!draws.TryGetValue(key,out var d)){d=new Draws{Part=part,Layer=layer,Camera=camera};draws.Add(key,d);}d.Matrices.Add(part.Bone>=0&&bone!=null?bone(part.Bone):root);d.Bodies.Add(new Vector4(bc.r,bc.g,bc.b,opacity*(partOpacity?.Invoke(part.Bone)??1)));d.Accents.Add(new Vector4(ac.r,ac.g,ac.b,1));d.Tints.Add(partTint?.Invoke(part.Bone)??Color.white);}
        }
        public void Flush(bool instancing)
        {
            foreach(var d in draws.Values)for(int start=0;start<d.Matrices.Count;start+=512)
            {
                int count=Math.Min(512,d.Matrices.Count-start);d.Matrices.CopyTo(start,buffer,0,count);d.Bodies.CopyTo(start,bodies,0,count);d.Accents.CopyTo(start,accents,0,count);d.Tints.CopyTo(start,tints,0,count);properties.Clear();properties.SetVectorArray("_BodyTint",bodies);properties.SetVectorArray("_AccentTint",accents);properties.SetVectorArray("_SceneTint",tints);
                bool drawn=false;if(instancing&&SystemInfo.supportsInstancing)try{Graphics.DrawMeshInstanced(d.Part.Mesh,0,d.Part.Material,buffer,count,properties,d.Layer==10?ShadowCastingMode.Off:ShadowCastingMode.On,true,d.Layer,d.Camera);drawn=true;}catch(InvalidOperationException){instancing=false;}
                if(!drawn)for(int i=0;i<count;i++){properties.Clear();properties.SetVector("_BodyTint",bodies[i]);properties.SetVector("_AccentTint",accents[i]);properties.SetVector("_SceneTint",tints[i]);Graphics.DrawMesh(d.Part.Mesh,buffer[i],d.Part.Material,d.Layer,d.Camera,0,properties,d.Layer==10?ShadowCastingMode.Off:ShadowCastingMode.On,true);}
            }
        }
        public void Dispose(){foreach(var p in models.Values.SelectMany(p=>p))CapturedAnimation.Release(p.Mesh);foreach(var m in materials)CapturedAnimation.Release(m);foreach(var t in textures)CapturedAnimation.Release(t);models.Clear();draws.Clear();}
    }
}
