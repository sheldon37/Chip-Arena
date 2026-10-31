using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using NMatrix=System.Numerics.Matrix4x4;
using NVector4=System.Numerics.Vector4;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition Deployment Station drawn for a camera (docs/EXPEDITION-DEPLOYMENT.md "Presentation"; Core NativeDeployStage
    /// poses it, NativeDeployModels sculpts it). Presentation only: it reads the replicated ExpeditionDeploymentState and never
    /// calls the authority (the console raises DeployIntent; the room or the DEV preview answers). Built once into meshes (the
    /// Core models' sections, in Unity's frame as authored), then each frame: the stage's practical lights through
    /// NativeLighting.Entrance (the source shaders' stage block), the camera on the stage's eased shot, and every item's sections:
    ///  - lit parts on NativeSourceLit with the stage block on (_StageLit), their light bones scaled by the item's tints;
    ///  - glass on NativeSourceTransparent;
    ///  - screen faces on NativeDeployScreen: the reel and card strips (rasterised on workers from NativeDeployArt whenever the
    ///    strips change), the holo maps (Core NativeDeployPresentation.Preview, regenerated on a worker when the state's revision
    ///    changes, frozen after Deploy), the stamps, the floor's light pools and the light columns.
    /// The draw allocates nothing per frame (one reused property block, the stage's fixed item buffer).
    /// </summary>
    public sealed class NativeDeployScene:IDisposable
    {
        public readonly NativeDeployStage Stage=new NativeDeployStage();
        private readonly Mesh[][] meshes=new Mesh[NativeDeployStage.ModelCount][];private readonly int[][] bones=new int[NativeDeployStage.ModelCount][];
        private readonly Material lit,glass,strip,additive,alpha;private readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        private readonly Texture2D[] textures=new Texture2D[NativeDeployStage.TextureCount];private readonly Texture2D white;
        private readonly Task<float[]>[] stripJobs=new Task<float[]>[6];private Task<float[]> stampJob;private Task<int[]> mapJob;
        private int stripsBuilt=-1,mapAsked=-1,mapShown=-1;
        private static readonly int SceneTintId=Shader.PropertyToID("_SceneTint"),BodyTintId=Shader.PropertyToID("_BodyTint"),AccentTintId=Shader.PropertyToID("_AccentTint"),GlowTintId=Shader.PropertyToID("_GlowTint"),StageLitId=Shader.PropertyToID("_StageLit");
        private static readonly int MainTexId=Shader.PropertyToID("_MainTex"),TintId=Shader.PropertyToID("_Tint"),RepId=Shader.PropertyToID("_Rep"),ScrollId=Shader.PropertyToID("_Scroll"),BlurId=Shader.PropertyToID("_Blur"),GainId=Shader.PropertyToID("_Gain");
        private static readonly int SweepId=Shader.PropertyToID("_Sweep"),ChromaId=Shader.PropertyToID("_Chroma"),HoloId=Shader.PropertyToID("_Holo"),TexelId=Shader.PropertyToID("_Texel");
        private static int supported=-1;
        /// <summary>Whether the station's shaders load and run here (asked once).</summary>
        public static bool Supported
        {
            get
            {
                if(supported<0){supported=1;foreach(var n in new[]{"NativeSourceLit","NativeSourceTransparent","NativeDeployScreen"}){var s=Resources.Load<Shader>(n);if(s==null||!s.isSupported){supported=0;Debug.LogWarning("The Deployment Station needs shader "+n+".");break;}}}
                return supported==1;
            }
        }
        public NativeDeployScene()
        {
            lit=new Material(Resources.Load<Shader>("NativeSourceLit")){name="Deploy station",hideFlags=HideFlags.HideAndDontSave};lit.SetInt("_Cull",2);lit.SetFloat("_Ink",0);
            glass=new Material(Resources.Load<Shader>("NativeSourceTransparent")){name="Deploy station glass",hideFlags=HideFlags.HideAndDontSave};glass.SetInt("_Cull",0);glass.SetFloat("_Ink",0);glass.SetFloat("_Unlit",0);
            var screen=Resources.Load<Shader>("NativeDeployScreen");
            strip=Screen(screen,"Deploy strips",BlendMode.One,BlendMode.Zero,true,2000,2,false);
            additive=Screen(screen,"Deploy light",BlendMode.One,BlendMode.One,false,3010,0,true);
            alpha=Screen(screen,"Deploy stamps",BlendMode.One,BlendMode.OneMinusSrcAlpha,false,3005,0,false);
            for(int m=0;m<NativeDeployStage.ModelCount;m++)
            {
                var model=Stage.Models[m];var list=new List<Mesh>();var b=new List<int>();
                foreach(var s in model.Sections){var mesh=Upload(s,model.Spec+" / "+s.Bone);if(mesh==null)continue;list.Add(mesh);b.Add(s.Bone);}
                meshes[m]=list.ToArray();bones[m]=b.ToArray();
            }
            white=Texture(new[]{1f,1f,1f,1f},1,1,"Deploy white",false);textures[NativeDeployStage.TexWhite]=white;
            textures[NativeDeployStage.TexGlow]=Texture(NativeDeployArt.GlowTile(),NativeDeployArt.GlowPixels,NativeDeployArt.GlowPixels,"Deploy glow",false);
            stampJob=Task.Run(()=>NativeDeployArt.Stack(new[]{NativeDeployArt.StampTile(false),NativeDeployArt.StampTile(true)}));
        }
        private static Material Screen(Shader shader,string name,BlendMode src,BlendMode dst,bool zwrite,int queue,int cull,bool add)
        {
            var m=new Material(shader){name=name,hideFlags=HideFlags.HideAndDontSave,renderQueue=queue};m.SetFloat("_SrcBlend",(float)src);m.SetFloat("_DstBlend",(float)dst);
            m.SetFloat("_ZWrite",zwrite?1:0);m.SetFloat("_Cull",cull);m.SetFloat("_Additive",add?1:0);return m;
        }
        // A Core section as a mesh (Unity's frame: no mirror): positions, normals, colours, the emission in uv1 (NativeSourceLit's
        // per-vertex emission), the screen uv in uv0.
        private static Mesh Upload(WeaponSection s,string name)
        {
            int n=s.Positions.Count;if(n==0)return null;
            var pos=new Vector3[n];var nrm=new Vector3[n];var col=new Color[n];var uv=new Vector2[n];var em=new List<Vector4>(n);
            for(int i=0;i<n;i++){var p=s.Positions[i];var q=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];pos[i]=new Vector3(p.X,p.Y,p.Z);nrm[i]=new Vector3(q.X,q.Y,q.Z);col[i]=new Color(c.X,c.Y,c.Z,c.W);em.Add(new Vector4(e.X,e.Y,e.Z,e.W));if(s.Data.Count==n)uv[i]=new Vector2(s.Data[i].X,s.Data[i].Y);}
            var mesh=new Mesh{name="Deploy "+name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16,hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=pos;mesh.normals=nrm;mesh.colors=col;mesh.uv=uv;mesh.SetUVs(1,em);mesh.SetTriangles(s.Indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        // Straight RGBA floats, bottom row first, as an sRGB texture (mipmapped; strips wrap vertically).
        private static Texture2D Texture(float[] rgba,int w,int h,string name,bool wrap)
        {
            var c=new Color32[w*h];for(int i=0;i<c.Length;i++)c[i]=new Color32(B(rgba[i*4]),B(rgba[i*4+1]),B(rgba[i*4+2]),B(rgba[i*4+3]));
            var t=new Texture2D(w,h,TextureFormat.RGBA32,true,false){name=name,wrapModeU=TextureWrapMode.Clamp,wrapModeV=wrap?TextureWrapMode.Repeat:TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4,hideFlags=HideFlags.HideAndDontSave};
            t.SetPixels32(c);t.Apply(true,true);return t;
        }
        private static byte B(float v)=>(byte)Mathf.RoundToInt(Mathf.Clamp01(v)*255);
        private void Replace(int slot,Texture2D t){var old=textures[slot];textures[slot]=t;if(old!=null&&old!=white)CapturedAnimation.Release(old);}

        // ---- the screens' pictures: built off the frame on workers, uploaded when ready ----
        private void Pictures(ExpeditionDeploymentState s)
        {
            if(Stage.StripVersion!=stripsBuilt)
            {
                stripsBuilt=Stage.StripVersion;
                for(int m=0;m<2;m++)for(int r=0;r<3;r++)
                {
                    var ids=(string[])Stage.Strip(m,r).Clone();var themes=new string[ids.Length];for(int i=0;i<ids.Length;i++)themes[i]=Stage.ThemeOf(ids[i]);bool dials=m==1;
                    stripJobs[m*3+r]=Task.Run(()=>{var tiles=new List<float[]>();for(int i=0;i<ids.Length;i++)tiles.Add(NativeDeployArt.Tile(ids[i],dials,themes[i]));if(dials)tiles.Add(NativeDeployArt.BackTile());return NativeDeployArt.Stack(tiles);});
                }
            }
            for(int k=0;k<6;k++){var job=stripJobs[k];if(job==null||!job.IsCompleted)continue;stripJobs[k]=null;
                if(job.Status==TaskStatus.RanToCompletion)Replace(NativeDeployStage.TexStrip+k,Texture(job.Result,NativeDeployArt.TilePixels,job.Result.Length/4/NativeDeployArt.TilePixels,"Deploy strip "+k,true));else Debug.LogWarning("A Deployment Station strip did not rasterise: "+job.Exception?.GetBaseException().Message);}
            if(stampJob!=null&&stampJob.IsCompleted){if(stampJob.Status==TaskStatus.RanToCompletion)Replace(NativeDeployStage.TexStamp,Texture(stampJob.Result,NativeDeployArt.TilePixels,NativeDeployArt.StampRows*2,"Deploy stamps",true));stampJob=null;}
            // the map: the stage's revision (frozen at Deploy), one job at a time
            if(Stage.MapRevision!=mapAsked&&mapJob==null&&s!=null){mapAsked=Stage.MapRevision;var copy=s.Copy();mapJob=Task.Run(()=>NativeDeployPresentation.Preview(copy,NativeDeployStage.MapWidth,NativeDeployStage.MapHeight));}
            if(mapJob!=null&&mapJob.IsCompleted)
            {
                if(mapJob.Status==TaskStatus.RanToCompletion&&mapAsked!=mapShown)
                {
                    int w=NativeDeployStage.MapWidth,h=NativeDeployStage.MapHeight;var px=mapJob.Result;var rgba=new float[w*h*4];
                    // Preview's row 0 is the north edge; the texture's row 0 is the south (the map plane's v 0).
                    for(int j=0;j<h;j++)for(int x=0;x<w;x++){int c=px[j*w+x],k=((h-1-j)*w+x)*4;rgba[k]=((c>>16)&255)/255f;rgba[k+1]=((c>>8)&255)/255f;rgba[k+2]=(c&255)/255f;rgba[k+3]=1;}
                    Replace(NativeDeployStage.TexMap,Texture(rgba,w,h,"Deploy holo map",false));mapShown=mapAsked;
                }
                mapJob=null;
            }
        }

        // ---- per frame (allocates nothing) ----
        private static Matrix4x4 U(in NMatrix m)
        {
            var o=new Matrix4x4();o.m00=m.M11;o.m01=m.M21;o.m02=m.M31;o.m03=m.M41;o.m10=m.M12;o.m11=m.M22;o.m12=m.M32;o.m13=m.M42;o.m20=m.M13;o.m21=m.M23;o.m22=m.M33;o.m23=m.M43;o.m30=0;o.m31=0;o.m32=0;o.m33=1;return o;
        }
        private static Vector4 V(in NVector4 v)=>new Vector4(v.X,v.Y,v.Z,v.W);
        /// <summary>Poses the station for `input` and draws it for `camera` (its framing, the lights and every model); `dt` eases
        /// the camera (negative: cut to the shot).</summary>
        public void Draw(Camera camera,in DeployInput input,float dt)
        {
            Stage.Frame(input,dt);Pictures(input.State);
            var view=Stage.Camera;var eye=new Vector3(view.Eye.X,view.Eye.Y,view.Eye.Z);var look=new Vector3(view.Target.X-view.Eye.X,view.Target.Y-view.Eye.Y,view.Target.Z-view.Eye.Z);
            if(look.sqrMagnitude>1e-6f)camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look,Vector3.up));camera.fieldOfView=view.Fov;camera.nearClipPlane=.1f;
            NativeLighting.Entrance(Stage.Lights,camera);
            for(int i=0;i<Stage.Count;i++)
            {
                ref var it=ref Stage.Items[i];int m=(int)it.Model;var parts=meshes[m];var b=bones[m];var world=U(it.World);
                for(int k=0;k<parts.Length;k++)
                {
                    int bone=b[k];block.Clear();Material mat;bool shadows=false;
                    if(bone==NativeDeployModels.Screen)
                    {
                        mat=it.Blend==NativeDeployStage.Blend.Additive?additive:it.Blend==NativeDeployStage.Blend.Alpha?alpha:strip;
                        block.SetTexture(MainTexId,textures[it.Texture]??white);block.SetVector(TintId,V(it.Screen));block.SetFloat(RepId,it.Rep);block.SetFloat(ScrollId,it.Scroll);block.SetFloat(BlurId,it.Blur);
                        block.SetFloat(GainId,it.Gain);block.SetFloat(SweepId,it.Sweep);block.SetFloat(ChromaId,it.Chroma);block.SetFloat(HoloId,it.Holo?1:0);block.SetVector(TexelId,new Vector4(1f/NativeDeployStage.MapWidth,1f/NativeDeployStage.MapHeight,0,0));
                    }
                    else if(bone==NativeDeployModels.Glass){mat=glass;block.SetVector(BodyTintId,new Vector4(1,1,1,it.Glass));block.SetVector(SceneTintId,Vector4.one);block.SetVector(AccentTintId,Vector4.one);block.SetVector(GlowTintId,Vector4.one);block.SetFloat(StageLitId,1);}
                    else{mat=lit;shadows=true;block.SetVector(SceneTintId,V(it.Tint(bone)));block.SetVector(BodyTintId,Vector4.one);block.SetVector(AccentTintId,Vector4.one);block.SetVector(GlowTintId,Vector4.one);block.SetFloat(StageLitId,1);}
                    Graphics.DrawMesh(parts[k],world,mat,0,camera,0,block,shadows?ShadowCastingMode.On:ShadowCastingMode.Off,shadows);
                }
            }
        }
        public void Dispose()
        {
            foreach(var list in meshes)if(list!=null)foreach(var m in list)if(m!=null)CapturedAnimation.Release(m);
            foreach(var m in new[]{lit,glass,strip,additive,alpha})if(m!=null)CapturedAnimation.Release(m);
            for(int i=0;i<textures.Length;i++){if(textures[i]!=null&&textures[i]!=white)CapturedAnimation.Release(textures[i]);textures[i]=null;}if(white!=null)CapturedAnimation.Release(white);
        }
    }
}
