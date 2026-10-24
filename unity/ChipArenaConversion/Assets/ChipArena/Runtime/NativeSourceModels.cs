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
    {public string part;public int bone,texture;public bool transparent,doubleSide,ink;public float[] positions,normals,colors,emission,uv;public int[] indices;}
    // Source-authored geometry is converted once at build time. Batches are shared across all actors using that model.
    public sealed partial class NativeSourceModels:IDisposable
    {
        private sealed class Part {public string Name;public int Bone;public Mesh Mesh;public Material Material;public bool NoShadow;
            /// <summary>The mesh's Core arrays as uploaded (pilot bodies, their wound cuts and hats: what a bisect cuts), its closed
            /// stand-in for a cut's cap (a wound cut's unwounded part), and whether its section is never capped (glass, flat cloth).</summary>
            public WeaponSection Core,Fill;public bool Open;}
        private sealed class Draws {public Part Part;public int Layer;public Camera Camera;public readonly List<Matrix4x4> Matrices=new List<Matrix4x4>();public readonly List<Vector4> Bodies=new List<Vector4>(),Accents=new List<Vector4>(),Tints=new List<Vector4>(),Glows=new List<Vector4>();}
        private readonly Dictionary<string,Part[]> models=new Dictionary<string,Part[]>();private readonly Dictionary<(Part,int),Draws> draws=new Dictionary<(Part,int),Draws>();
        private readonly List<Texture2D> textures=new List<Texture2D>();private readonly List<Material> materials=new List<Material>();private readonly Matrix4x4[] buffer=new Matrix4x4[512];private readonly Vector4[] bodies=new Vector4[512],accents=new Vector4[512],tints=new Vector4[512],glows=new Vector4[512];private readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        public static string Decode(string resource)
        {var asset=Resources.Load<TextAsset>(resource);if(asset==null)throw new InvalidOperationException(resource+" is missing");var envelope=JsonUtility.FromJson<SourceEnvelope>(asset.text);if(envelope.schema!=1||envelope.commit!="05950eaef80ff2483a9c44d898cad98be1a76bbc")throw new InvalidDataException("Unverified "+resource);using(var bytes=new MemoryStream(Convert.FromBase64String(envelope.data)))using(var zip=new GZipStream(bytes,CompressionMode.Decompress))using(var reader=new StreamReader(zip))return reader.ReadToEnd();}
        private static Texture2D CreateTexture(SourceTexture source,bool readable=false)
        {
            if(source==null||source.channels!=4||source.width<=0||source.height<=0)
                throw new InvalidDataException("Source texture must have positive dimensions and RGBA32 pixels");
            byte[] pixels=Convert.FromBase64String(source.pixels??"");
            if(pixels.LongLength!=(long)source.width*source.height*4)
                throw new InvalidDataException("Source texture byte count does not match its base mip dimensions");
            var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,true,false);
            try
            {
                texture.name="Source procedural texture";
                // The export stores mip 0 only. LoadRawTextureData requires every allocated mip.
                texture.SetPixelData<byte>(pixels,0);
                texture.wrapMode=TextureWrapMode.Repeat;
                texture.Apply(true,!readable); // Generate the smaller mips from the supplied base image.
                return texture;
            }
            catch {CapturedAnimation.Release(texture);throw;}
        }
#if UNITY_EDITOR
        public static void ValidateTextureImports()
        {
            var data=JsonUtility.FromJson<SourceModelSet>(Decode("SourceModels"));
            if(data.textures==null||data.textures.Length!=3)throw new InvalidDataException("Source texture bank is incomplete");
            foreach(var source in data.textures)
            {
                var texture=CreateTexture(source,true);
                try
                {
                    var expected=Convert.FromBase64String(source.pixels);var actual=texture.GetPixels32(0);
                    if(actual.Length*4!=expected.Length||texture.mipmapCount<=1)throw new InvalidDataException("Source texture lost its base image or mip chain");
                    for(int i=0;i<actual.Length;i++)
                        if(actual[i].r!=expected[i*4]||actual[i].g!=expected[i*4+1]||actual[i].b!=expected[i*4+2]||actual[i].a!=expected[i*4+3])
                            throw new InvalidDataException("Source texture pixels changed during upload");
                }
                finally {CapturedAnimation.Release(texture);}
            }
            var invalid=new SourceTexture{width=2,height=2,channels=4,pixels=Convert.ToBase64String(new byte[15])};
            bool rejected=false;
            try {var texture=CreateTexture(invalid);CapturedAnimation.Release(texture);}
            catch(InvalidDataException){rejected=true;}
            if(!rejected)throw new InvalidDataException("Truncated source texture was accepted");
            Debug.Log("PASS native texture imports: three base images preserved, mip chains retained and truncated pixels rejected");
        }
#endif
        public NativeSourceModels()
        {
            var data=JsonUtility.FromJson<SourceModelSet>(Decode("SourceModels"));
            foreach(var source in data.textures)textures.Add(CreateTexture(source));
            // Stage hazard parts (tools/export_hazards.mjs) share the source format; they carry no textures.
            var hazards=JsonUtility.FromJson<SourceModelSet>(Decode("HazardModels"));if(hazards.models.Any(m=>m.sections.Any(x=>x.texture>=0)))throw new InvalidDataException("Hazard models must not reference textures");
            foreach(var model in data.models.Concat(hazards.models))
            {
                var parts=new List<Part>();foreach(var s in model.sections)
                {
                    int count=s.positions.Length/3;if(count==0)continue;if(s.normals.Length!=count*3||s.colors.Length!=count*4||s.emission.Length!=count*4||s.uv.Length!=count*2||s.indices.Any(i=>i<0||i>=count))throw new InvalidDataException("Invalid source model "+model.id);
                    var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];var emission=new List<Vector4>(count);var uv=new Vector2[count];
                    for(int i=0;i<count;i++){vertices[i]=new Vector3(s.positions[i*3],s.positions[i*3+1],s.positions[i*3+2]);normals[i]=new Vector3(s.normals[i*3],s.normals[i*3+1],s.normals[i*3+2]);colors[i]=new Color(s.colors[i*4],s.colors[i*4+1],s.colors[i*4+2],s.colors[i*4+3]);emission.Add(new Vector4(s.emission[i*4],s.emission[i*4+1],s.emission[i*4+2],s.emission[i*4+3]));uv[i]=new Vector2(s.uv[i*2],s.uv[i*2+1]);}
                    var mesh=new Mesh{name=model.id+" / joint "+s.bone,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,emission);mesh.triangles=s.indices;mesh.RecalculateBounds();mesh.UploadMeshData(true);
                    var shader=Resources.Load<Shader>(s.transparent?"NativeSourceTransparent":"NativeSourceLit");if(shader==null)throw new InvalidOperationException("Source material shader is missing");var material=new Material(shader){enableInstancing=true};material.SetInt("_Cull",s.doubleSide?0:2);material.SetFloat("_Ink",s.ink?1:0);
                    if(s.texture>=0){material.mainTexture=textures[s.texture];var repeat=data.textures[s.texture].repeat;material.mainTextureScale=new Vector2(repeat[0],repeat[1]);}materials.Add(material);parts.Add(new Part{Name=s.part,Bone=s.bone,Mesh=mesh,Material=material,Core=Banked(model.id)?CoreOf(s):null,Open=s.transparent||s.doubleSide});
                }ArmorLights(model,parts);models.Add(model.id,parts.ToArray());HatTwin(model,parts);
            }
            SplitSirenPieces(data);HordeMaterials();WeaponSkins();SheldonModels();PilotBodies();BowModels();HordeKindModels();MachineSkins();
        }
        public void Begin(){foreach(var d in draws.Values){d.Matrices.Clear();d.Bodies.Clear();d.Accents.Clear();d.Tints.Clear();d.Glows.Clear();}}
        public bool Has(string id)=>id!=null&&models.ContainsKey(id);
        // `look` poses the Siren's animated pieces (NativeSirenLookPresentation.cs); without one they hold the rest look.
        // `hitFlash`: the struck pilot's flash code (Core NativeHitFlash.Code, 1 = none), carried in the accent tint's unused w
        // to NativeSourceLit's StageTone, so the flash needs no extra per-instance array and every batch stays as it was.
        public void Queue(string id,Matrix4x4 root,Func<int,Matrix4x4> bone=null,Func<int,bool> visible=null,int layer=0,Camera camera=null,Color? body=null,Color? accent=null,float opacity=1,Func<int,float> partOpacity=null,Func<int,Color> partTint=null,Vector4? glow=null,NativeSirenLookPose look=null,float hitFlash=1)
        {
            if(!models.TryGetValue(id,out var model))return;Color bc=body??Color.white,ac=accent??Color.white;
            foreach(var part in model){if(visible!=null&&!visible(part.Bone))continue;var key=(part,layer);if(!draws.TryGetValue(key,out var d)){d=new Draws{Part=part,Layer=layer,Camera=camera};draws.Add(key,d);}var matrix=part.Bone>=0&&bone!=null?bone(part.Bone):root;d.Matrices.Add(weaponStretch.Count>0?WeaponStretched(part.Mesh,matrix):matrix);d.Bodies.Add(new Vector4(bc.r,bc.g,bc.b,opacity*(partOpacity?.Invoke(part.Bone)??1)));d.Accents.Add(new Vector4(ac.r,ac.g,ac.b,hitFlash));d.Tints.Add(partTint?.Invoke(part.Bone)??Color.white);d.Glows.Add(glow??Vector4.one);}
            if(lookPieces.Count>0&&lookPieces.TryGetValue(id,out var list))QueuePieces(list,root,bone,visible,layer,camera,bc,ac,opacity,partOpacity,partTint,glow,look);
        }
        /// <summary>
        /// True while an entrance stage is flushed (ArenaPresentation.RenderIntro): its draws read the stage point lights from
        /// the shaders' stage block (`_StageLit`, NativeLighting.Intro), which every other draw leaves off.
        /// </summary>
        public bool Stage;private static readonly int StageLitId=Shader.PropertyToID("_StageLit");
        public void Flush(bool instancing)
        {
            foreach(var d in draws.Values)for(int start=0;start<d.Matrices.Count;start+=512)
            {
                int count=Math.Min(512,d.Matrices.Count-start);d.Matrices.CopyTo(start,buffer,0,count);d.Bodies.CopyTo(start,bodies,0,count);d.Accents.CopyTo(start,accents,0,count);d.Tints.CopyTo(start,tints,0,count);d.Glows.CopyTo(start,glows,0,count);properties.Clear();properties.SetVectorArray("_BodyTint",bodies);properties.SetVectorArray("_AccentTint",accents);properties.SetVectorArray("_SceneTint",tints);properties.SetVectorArray("_GlowTint",glows);properties.SetFloat(StageLitId,Stage?1:0);
                bool drawn=false;if(instancing&&SystemInfo.supportsInstancing)try{Graphics.DrawMeshInstanced(d.Part.Mesh,0,d.Part.Material,buffer,count,properties,d.Layer==10||d.Part.NoShadow?ShadowCastingMode.Off:ShadowCastingMode.On,true,d.Layer,d.Camera);drawn=true;}catch(InvalidOperationException){instancing=false;}
                if(!drawn)for(int i=0;i<count;i++){properties.Clear();properties.SetVector("_BodyTint",bodies[i]);properties.SetVector("_AccentTint",accents[i]);properties.SetVector("_SceneTint",tints[i]);properties.SetVector("_GlowTint",glows[i]);properties.SetFloat(StageLitId,Stage?1:0);Graphics.DrawMesh(d.Part.Mesh,buffer[i],d.Part.Material,d.Layer,d.Camera,0,properties,d.Layer==10||d.Part.NoShadow?ShadowCastingMode.Off:ShadowCastingMode.On,true);}
            }
        }
        public void Dispose(){ClearWounds();ClearBisects();ClearReviveModels();DisposePieces();foreach(var mesh in models.Values.SelectMany(p=>p).Select(p=>p.Mesh).Distinct())CapturedAnimation.Release(mesh);foreach(var m in materials)CapturedAnimation.Release(m);foreach(var t in textures)CapturedAnimation.Release(t);models.Clear();draws.Clear();}
    }
}
