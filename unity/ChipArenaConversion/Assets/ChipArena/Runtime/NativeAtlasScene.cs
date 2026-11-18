using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using NMatrix=System.Numerics.Matrix4x4;
using NVector2=System.Numerics.Vector2;
using NVector3=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// ATLAS, the Deployment Station's tileset departures board, drawn for the station's camera (Core NativeAtlasBoard rules it,
    /// NativeAtlasStage poses it each frame inside NativeDeployStage, NativeAtlasModels sculpts it; Runtime NativeDeployScene
    /// calls Draw after its own items). Presentation only:
    ///  - the cabinet on NativeSourceLit with the stage's light block (its bones: the lit body, the lettering at the header's
    ///    level, the in-service lamp in the selected tileset's accent, the other lines' dim lamps);
    ///  - the flaps, one mesh per line on NativeDeployScreen (emissive: they light nothing), sampling the one glyph atlas
    ///    (the build's bake under NativeAtlasArt.Key, else rasterised on a worker; until it is up the black face shows), their
    ///    vertices rewritten only when a flap's pose changes (the line arriving, a later re-flap);
    ///  - the bell's sparks (machine 2) as camera-facing additive quads, as the station draws its own;
    ///  - its sounds raised through <see cref="Sound"/> (NativeAudio plays them from Claude's NativeAtlasSounds table).
    /// A failure is logged once and turns the board off; it never takes the station down. The draw allocates nothing.
    /// </summary>
    public sealed class NativeAtlasScene:IDisposable
    {
        /// <summary>A moment of the arrival heard this frame (NativeAudio subscribes; NativeAtlasSounds maps it to its clip).</summary>
        public static event Action<NativeAtlasBoard.Cue> Sound;
        private readonly Mesh[] cabinet;private readonly int[] bones;
        private readonly Mesh[] lines=new Mesh[NativeTilesets.Lines];private readonly Vector3[][] positions=new Vector3[NativeTilesets.Lines][];
        private readonly Vector2[][] uvs=new Vector2[NativeTilesets.Lines][];private readonly Color[][] colors=new Color[NativeTilesets.Lines][];
        private readonly NVector3[] cellPos=new NVector3[NativeAtlasModels.CellVertices];private readonly NVector2[] cellUv=new NVector2[NativeAtlasModels.CellVertices];private readonly float[] cellShade=new float[NativeAtlasModels.CellVertices];
        private readonly Mesh quad;private readonly Material lit,flaps,light;private readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        private Texture2D atlas,spark;private Task<float[]> atlasJob;private int version=int.MinValue;private bool failed;
        private static readonly int SceneTintId=Shader.PropertyToID("_SceneTint"),BodyTintId=Shader.PropertyToID("_BodyTint"),AccentTintId=Shader.PropertyToID("_AccentTint"),GlowTintId=Shader.PropertyToID("_GlowTint"),StageLitId=Shader.PropertyToID("_StageLit");
        private static readonly int MainTexId=Shader.PropertyToID("_MainTex"),TintId=Shader.PropertyToID("_Tint"),RepId=Shader.PropertyToID("_Rep"),ScrollId=Shader.PropertyToID("_Scroll"),BlurId=Shader.PropertyToID("_Blur"),GainId=Shader.PropertyToID("_Gain");
        private static readonly int SweepId=Shader.PropertyToID("_Sweep"),ChromaId=Shader.PropertyToID("_Chroma"),HoloId=Shader.PropertyToID("_Holo");
        // the glyphs' colours: line 1 a warm white, its emblem in the tileset's accent; the other lines amber
        private static readonly Color Warm=Lin(0xfff1d6),Amber=Lin(0xff9c3a);
        private static Color Lin(int hex){var c=WeaponPaint.Linear(hex);return new Color(c.X,c.Y,c.Z,1);}

        /// <summary>The board, or null when it cannot be built here (logged; the station draws without it).</summary>
        public static NativeAtlasScene Make(){try{return new NativeAtlasScene();}catch(Exception e){Debug.LogException(e);return null;}}
        private NativeAtlasScene()
        {
            lit=new Material(Resources.Load<Shader>("NativeSourceLit")){name="ATLAS cabinet",hideFlags=HideFlags.HideAndDontSave};lit.SetInt("_Cull",2);lit.SetFloat("_Ink",0);
            var screen=Resources.Load<Shader>("NativeDeployScreen");
            flaps=Screen(screen,"ATLAS flaps",BlendMode.One,BlendMode.Zero,true,2000,false);light=Screen(screen,"ATLAS sparks",BlendMode.One,BlendMode.One,false,3010,true);
            var model=NativeAtlasModels.Cabinet();var list=new System.Collections.Generic.List<Mesh>();var b=new System.Collections.Generic.List<int>();
            foreach(var s in model.Sections){var mesh=Upload(s,"ATLAS / "+s.Bone);if(mesh==null)continue;list.Add(mesh);b.Add(s.Bone);}
            cabinet=list.ToArray();bones=b.ToArray();
            var L=NativeAtlasBoard.Board;
            for(int l=0;l<lines.Length;l++)
            {
                int n=L.LineCount[l],v=n*NativeAtlasModels.CellVertices;positions[l]=new Vector3[v];uvs[l]=new Vector2[v];colors[l]=new Color[v];var idx=new int[n*NativeAtlasModels.CellIndices];
                for(int i=0;i<n;i++)NativeAtlasModels.Indices(idx,i*NativeAtlasModels.CellIndices,i*NativeAtlasModels.CellVertices);
                var m=new Mesh{name="ATLAS line "+l,hideFlags=HideFlags.HideAndDontSave};m.MarkDynamic();m.vertices=positions[l];m.uv=uvs[l];m.colors=colors[l];m.SetTriangles(idx,0);
                m.bounds=new Bounds(new Vector3(0,NativeAtlasBoard.Height/2,-.06f),new Vector3(NativeAtlasBoard.Width,NativeAtlasBoard.Height,.4f));lines[l]=m;
            }
            quad=new Mesh{name="ATLAS spark",hideFlags=HideFlags.HideAndDontSave};quad.vertices=new[]{new Vector3(.5f,-.5f,0),new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)};
            quad.uv=new[]{new Vector2(1,0),new Vector2(0,0),new Vector2(0,1),new Vector2(1,1)};quad.colors=new[]{Color.white,Color.white,Color.white,Color.white};quad.SetTriangles(new[]{0,1,2,0,2,3},0);quad.RecalculateBounds();
            spark=Texture(NativeDeployArt.SparkTile(),NativeDeployArt.SparkPixels,NativeDeployArt.SparkPixels,"ATLAS spark");
            // the glyph atlas: this build's bake (Editor/NativeDeployCardBuild.cs), else rasterised once on a worker
            var baked=Resources.Load<TextAsset>(NativeAtlasArt.Key);
            if(baked!=null&&baked.bytes.Length==NativeAtlasArt.Bytes)atlas=Texture(baked.bytes,NativeAtlasArt.Width,NativeAtlasArt.Height,"ATLAS flap atlas");
            else atlasJob=Task.Run(()=>NativeAtlasArt.Atlas());
        }
        private static Material Screen(Shader shader,string name,BlendMode src,BlendMode dst,bool zwrite,int queue,bool add)
        {
            var m=new Material(shader){name=name,hideFlags=HideFlags.HideAndDontSave,renderQueue=queue};m.SetFloat("_SrcBlend",(float)src);m.SetFloat("_DstBlend",(float)dst);
            m.SetFloat("_ZWrite",zwrite?1:0);m.SetFloat("_Cull",0);m.SetFloat("_Additive",add?1:0);return m;
        }
        // A Core section as a mesh (Unity's frame, as authored): positions, normals, colours, the emission in uv1, the uv in uv0.
        private static Mesh Upload(WeaponSection s,string name)
        {
            int n=s.Positions.Count;if(n==0)return null;
            var pos=new Vector3[n];var nrm=new Vector3[n];var col=new Color[n];var uv=new Vector2[n];var em=new System.Collections.Generic.List<Vector4>(n);
            for(int i=0;i<n;i++){var p=s.Positions[i];var q=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];pos[i]=new Vector3(p.X,p.Y,p.Z);nrm[i]=new Vector3(q.X,q.Y,q.Z);col[i]=new Color(c.X,c.Y,c.Z,c.W);em.Add(new Vector4(e.X,e.Y,e.Z,e.W));if(s.Data.Count==n)uv[i]=new Vector2(s.Data[i].X,s.Data[i].Y);}
            var mesh=new Mesh{name=name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16,hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=pos;mesh.normals=nrm;mesh.colors=col;mesh.uv=uv;mesh.SetUVs(1,em);mesh.SetTriangles(s.Indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        private static Texture2D Texture(float[] rgba,int w,int h,string name)
        {
            var c=new Color32[w*h];for(int i=0;i<c.Length;i++)c[i]=new Color32(B(rgba[i*4]),B(rgba[i*4+1]),B(rgba[i*4+2]),B(rgba[i*4+3]));return Texture(c,w,h,name);
        }
        private static Texture2D Texture(byte[] rgba,int w,int h,string name)
        {
            var c=new Color32[w*h];for(int i=0;i<c.Length;i++)c[i]=new Color32(rgba[i*4],rgba[i*4+1],rgba[i*4+2],rgba[i*4+3]);return Texture(c,w,h,name);
        }
        private static Texture2D Texture(Color32[] c,int w,int h,string name)
        {
            var t=new Texture2D(w,h,TextureFormat.RGBA32,true,false){name=name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4,hideFlags=HideFlags.HideAndDontSave};
            t.SetPixels32(c);t.Apply(true,true);return t;
        }
        private static byte B(float v)=>(byte)Mathf.RoundToInt(Mathf.Clamp01(v)*255);
        private static Matrix4x4 U(in NMatrix m)
        {
            var o=new Matrix4x4();o.m00=m.M11;o.m01=m.M21;o.m02=m.M31;o.m03=m.M41;o.m10=m.M12;o.m11=m.M22;o.m12=m.M32;o.m13=m.M42;o.m20=m.M13;o.m21=m.M23;o.m22=m.M33;o.m23=m.M43;o.m30=0;o.m31=0;o.m32=0;o.m33=1;return o;
        }
        private static Vector4 V(in System.Numerics.Vector4 v)=>new Vector4(v.X,v.Y,v.Z,v.W);

        /// <summary>Draws the board for `camera` from the stage's pose this frame (NativeDeployScene calls it after its items).</summary>
        public void Draw(Camera camera,NativeDeployStage stage)
        {
            if(failed||camera==null||stage?.Atlas==null)return;
            try{Frame(camera,stage.Atlas);}catch(Exception e){failed=true;Debug.LogException(e);}
        }
        private void Frame(Camera camera,NativeAtlasStage a)
        {
            if(atlas==null&&atlasJob!=null&&atlasJob.IsCompleted)
            {
                if(atlasJob.Status==TaskStatus.RanToCompletion)atlas=Texture(atlasJob.Result,NativeAtlasArt.Width,NativeAtlasArt.Height,"ATLAS flap atlas");
                else Debug.LogWarning("The ATLAS flap atlas did not rasterise: "+atlasJob.Exception?.GetBaseException().Message);
                atlasJob=null;version=int.MinValue;
            }
            var root=U(NativeAtlasBoard.Root);var accent=Lin(a.Selected!=null?a.Selected.Accent:0xffc65a);
            // the cabinet: the lit body; the lettering at the header's level; the in-service lamp in the accent; the dim lamps
            for(int k=0;k<cabinet.Length;k++)
            {
                int bone=bones[k];Vector4 tint=bone==NativeAtlasModels.Lights?new Vector4(a.Header,a.Header,a.Header,1):bone==NativeAtlasModels.Accent?new Vector4(accent.r*a.LineLamp,accent.g*a.LineLamp,accent.b*a.LineLamp,1):bone==NativeAtlasModels.Dim?new Vector4(a.DimLamp,a.DimLamp,a.DimLamp,1):Vector4.one;
                block.Clear();block.SetVector(SceneTintId,tint);block.SetVector(BodyTintId,Vector4.one);block.SetVector(AccentTintId,Vector4.one);block.SetVector(GlowTintId,Vector4.one);block.SetFloat(StageLitId,1);
                Graphics.DrawMesh(cabinet[k],root,lit,0,camera,0,block,bone==NativeAtlasModels.Body?ShadowCastingMode.On:ShadowCastingMode.Off,bone==NativeAtlasModels.Body);
            }
            // the flaps, once the atlas is up: rewritten only when a pose changed
            if(atlas!=null)
            {
                if(a.Version!=version){version=a.Version;Rebuild(a,accent);}
                for(int l=0;l<lines.Length;l++)
                {
                    block.Clear();block.SetTexture(MainTexId,atlas);block.SetVector(TintId,Vector4.one);block.SetFloat(RepId,1);block.SetFloat(ScrollId,0);block.SetFloat(BlurId,0);
                    block.SetFloat(GainId,l==0?a.LineGlyph:a.Dim);block.SetFloat(SweepId,-1);block.SetFloat(ChromaId,1);block.SetFloat(HoloId,0);
                    Graphics.DrawMesh(lines[l],root,flaps,0,camera,0,block,ShadowCastingMode.Off,false);
                }
            }
            // the bell's sparks: camera-facing additive streaks
            var eye=camera.transform.position;
            for(int i=0;i<a.SparkCount;i++)
            {
                var sp=a.Sparks[i];var at=new Vector3(sp.At.X,sp.At.Y,sp.At.Z);var d=at-eye;float dl=d.magnitude;if(!(dl>1e-3f))continue;d/=dl;
                var axis=new Vector3(sp.Axis.X,sp.Axis.Y,sp.Axis.Z);var ax=axis-d*Vector3.Dot(axis,d);float len=ax.magnitude;Vector3 x,y;
                if(len>sp.Width*.25f){y=ax/len;x=Vector3.Cross(y,d).normalized;}else{x=Vector3.Cross(Vector3.up,d);x=x.sqrMagnitude<1e-6f?Vector3.right:x.normalized;y=Vector3.Cross(d,x);len=0;}
                float w=sp.Width,h=sp.Width+len;var m=new Matrix4x4();m.SetColumn(0,new Vector4(x.x*w,x.y*w,x.z*w,0));m.SetColumn(1,new Vector4(y.x*h,y.y*h,y.z*h,0));m.SetColumn(2,new Vector4(d.x,d.y,d.z,0));m.SetColumn(3,new Vector4(at.x,at.y,at.z,1));
                block.Clear();block.SetTexture(MainTexId,spark);block.SetVector(TintId,V(sp.Color));block.SetFloat(RepId,1);block.SetFloat(ScrollId,0);block.SetFloat(BlurId,0);block.SetFloat(GainId,1);block.SetFloat(SweepId,-1);block.SetFloat(ChromaId,1);block.SetFloat(HoloId,0);
                Graphics.DrawMesh(quad,m,light,0,camera,0,block,ShadowCastingMode.Off,false);
            }
            // the arrival's sounds
            for(int i=0;i<a.CueCount;i++)Sound?.Invoke(a.Cues[i]);
        }
        // Every line's flaps from the poses: each cell's three faces (Core NativeAtlasModels.Cell), tinted per line.
        private void Rebuild(NativeAtlasStage a,Color accent)
        {
            var L=a.Layout;
            for(int l=0;l<lines.Length;l++)
            {
                var pos=positions[l];var uv=uvs[l];var col=colors[l];int s=L.LineStart[l],n=L.LineCount[l];
                for(int i=0;i<n;i++)
                {
                    int c=s+i;var cell=L.Cells[c];NativeAtlasModels.Cell(cell,a.Poses[c],cellPos,cellUv,cellShade,0);
                    var tone=l>0?Amber:cell.Kind==NativeAtlasBoard.Kind.Emblem?accent:Warm;int v=i*NativeAtlasModels.CellVertices;
                    for(int j=0;j<NativeAtlasModels.CellVertices;j++){var p=cellPos[j];pos[v+j]=new Vector3(p.X,p.Y,p.Z);uv[v+j]=new Vector2(cellUv[j].X,cellUv[j].Y);float k=cellShade[j];col[v+j]=Shade(tone,k);}
                }
                var mesh=lines[l];mesh.SetVertices(pos);mesh.SetUVs(0,uv);mesh.SetColors(col);
            }
        }
        private static Color Shade(Color c,float k){c.r*=k;c.g*=k;c.b*=k;return c;}
        public void Dispose()
        {
            void Free(UnityEngine.Object o){if(o!=null)CapturedAnimation.Release(o);}
            if(cabinet!=null)foreach(var m in cabinet)Free(m);foreach(var m in lines)Free(m);Free(quad);Free(lit);Free(flaps);Free(light);Free(atlas);Free(spark);atlas=null;spark=null;failed=true;
        }
    }
}
