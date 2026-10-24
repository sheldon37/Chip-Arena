using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NVector4=System.Numerics.Vector4;
namespace ChipArena.UnityConversion
{
    /// <summary>The player's look on the home screen's rooftop: the weapon id (its source spec), hat and colours (prefs).</summary>
    public struct RooftopPilot {public string Spec,Hat,Body,Accent,HatColor;public int BodySkin;}
    /// <summary>
    /// The home screen's background, "Pilot's Rooftop" (owner 2026-10-12; Core NativeRooftopCity, NativeRooftopActors,
    /// NativeRooftopArt): the deterministic cyber city built once per quality tier into a handful of meshes and drawn for the
    /// menu camera only, each home-screen frame. Presentation only: nothing here is read by the simulation, the authority, the
    /// network frame or a save. Core builds in the source frame; this boundary mirrors x (positions, normals, drift) and flips
    /// the winding, as every source model import does.
    ///  - one opaque mesh for the static city, our roof and its props (NativeRooftop.shader picks the look per vertex);
    ///  - the holo screens: a quad each, their ads rasterised from NativeRooftopArt markup on worker threads (NativeIconRaster
    ///    is engine-free) and uploaded as they finish, so the first frames are not held up (a screen glows dark until then);
    ///  - the flying cars: one dynamic mesh rebuilt each frame from the lanes (periods dividing the 48 s loop);
    ///  - the sprinkle: static sprite meshes the shader animates (mist, glows, steam, motes, rain) and one small dynamic
    ///    layer for car lights and trails, the faulty sign's sparks, the duel's tracers and deflection sparks, the beacon blink.
    /// The anime style (docs/MAP-STYLE-ANIME.md section 6; Core NativeMenuStyle): the rooftop's own toon values, which
    /// NativeLighting.Rooftop writes (the pilots' ramp, the outline and the grade), its sky material's cumulus (set once here)
    /// and its haze's depth planes (_RoofToonFog, each frame with the scene's other globals); the shaders draw it while
    /// _ArenaToon.x is on and their legacy look under `--ca-map-style legacy`.
    /// </summary>
    public sealed class NativeRooftopScene:IDisposable
    {
        public const int DynamicCapacity=1024;
        public readonly float Quality;public readonly NativeRooftopCity.World World;
        private readonly Material worldMaterial,skyMaterial,screenMaterial;private readonly Material[] layers=new Material[6];
        private readonly Mesh worldMesh,quad,cars,dynamicMesh;private readonly Mesh[] sprites=new Mesh[5];
        private readonly RooftopScreen[] screens;private readonly Matrix4x4[] screenMatrix;private readonly MaterialPropertyBlock[] screenBlock;
        private readonly Dictionary<string,Texture2D> ads=new Dictionary<string,Texture2D>();private readonly Dictionary<string,Task<float[]>> adJobs=new Dictionary<string,Task<float[]>>();
        // the dynamic layers' buffers, reused every frame (no per-frame allocation)
        private readonly Vector3[] dynPos=new Vector3[DynamicCapacity*4];private readonly Vector4[] dynUv0=new Vector4[DynamicCapacity*4],dynUv2=new Vector4[DynamicCapacity*4],dynUv3=new Vector4[DynamicCapacity*4];private int dynCount;
        private readonly Vector3[] carPos,carNormal;private readonly Vector4[] carUv;
        private readonly NVector muzzleLocal=new NVector(0,1.25f,.9f),hitLocal=new NVector(0,1.25f,.4f);
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*8000);
        private static readonly int TimeId=Shader.PropertyToID("_RoofTime"),SignPosId=Shader.PropertyToID("_RoofSignPos"),SignColorId=Shader.PropertyToID("_RoofSignColor"),CyanPosId=Shader.PropertyToID("_RoofCyanPos");
        private static readonly int MainTexId=Shader.PropertyToID("_MainTex"),GainId=Shader.PropertyToID("_Gain"),UvRectId=Shader.PropertyToID("_UvRect"),CalmId=Shader.PropertyToID("_Calm");
        private static readonly int RoofToonFogId=Shader.PropertyToID("_RoofToonFog"),RoofSkyToonId=Shader.PropertyToID("_RoofSkyToon"),RoofSkyLitId=Shader.PropertyToID("_RoofSkyLit"),RoofSkyShadeId=Shader.PropertyToID("_RoofSkyShade");
        private static readonly Vector4 roofToonFog=V4(NativeMenuStyle.RoofFog);
        private static readonly string[] ShaderNames={"NativeRooftop","NativeRooftopSky","NativeRooftopScreen","NativeRooftopSprite"};
        private static int supported=-1;
        /// <summary>Whether the four rooftop shaders load and run on this device (asked once); otherwise the menu keeps the map orbit.</summary>
        public static bool Supported
        {
            get
            {
                if(supported<0){supported=1;foreach(var n in ShaderNames){var s=Resources.Load<Shader>(n);if(s==null||!s.isSupported){supported=0;Debug.LogWarning("The home screen's rooftop needs shader "+n+"; the menu keeps the orbiting map.");break;}}}
                return supported==1;
            }
        }
        // The source frame to Unity's: x mirrored.
        private static Vector3 U(NVector v)=>new Vector3(-v.X,v.Y,v.Z);
        private static Vector4 V4(NVector4 v)=>new Vector4(v.X,v.Y,v.Z,v.W);
        public NativeRooftopScene(float quality)
        {
            Quality=quality;World=NativeRooftopCity.Build(quality);
            worldMaterial=Make(ShaderNames[0]);skyMaterial=Make(ShaderNames[1]);screenMaterial=Make(ShaderNames[2]);
            // The anime sky's cumulus (NativeMenuStyle.Rooftop's sky values, linear; the shader reads them only while the style is on).
            var style=new MapStyleState();NativeMapStyle.State(NativeMenuStyle.Rooftop,false,true,style);NativeMenuStyle.RooftopSky(style,out var skyToon,out var skyLit,out var skyShade);
            skyMaterial.SetVector(RoofSkyToonId,V4(skyToon));skyMaterial.SetVector(RoofSkyLitId,Lin(V4(skyLit)));skyMaterial.SetVector(RoofSkyShadeId,Lin(V4(skyShade)));
            for(int i=0;i<layers.Length;i++){var m=Make(ShaderNames[3]);bool additive=i!=0&&i!=2;m.SetFloat("_SrcBlend",(float)(additive?BlendMode.One:BlendMode.SrcAlpha));m.SetFloat("_DstBlend",(float)(additive?BlendMode.One:BlendMode.OneMinusSrcAlpha));m.SetFloat("_Additive",additive?1:0);m.renderQueue=3000+i;layers[i]=m;}
            worldMesh=WorldMesh(World.Quads);quad=Quad();
            // Back to front: mist, glows, steam, motes, rain (the dynamic layer draws last).
            var lists=new[]{World.Mist,World.Glows,World.Steam,World.Motes,World.Rain};for(int i=0;i<lists.Length;i++)sprites[i]=lists[i].Count>0?SpriteMesh("Rooftop sprites "+i,lists[i],lists[i].Count):null;
            dynamicMesh=SpriteMesh("Rooftop dynamic sprites",null,DynamicCapacity);dynamicMesh.MarkDynamic();
            int carCount=0;foreach(var l in World.Lanes)carCount+=l.Count;carPos=new Vector3[carCount*CarVertices];carNormal=new Vector3[carCount*CarVertices];carUv=new Vector4[carCount*CarVertices];
            var tris=new int[carCount*CarIndices];for(int c=0;c<carCount;c++)for(int f=0;f<CarVertices/4;f++){int v=c*CarVertices+f*4,t=c*CarIndices+f*6;tris[t]=v;tris[t+1]=v+2;tris[t+2]=v+1;tris[t+3]=v;tris[t+4]=v+3;tris[t+5]=v+2;}
            cars=new Mesh{name="Rooftop traffic",hideFlags=HideFlags.HideAndDontSave};cars.MarkDynamic();cars.vertices=carPos;cars.normals=carNormal;cars.SetUVs(0,carUv);cars.triangles=tris;cars.bounds=Everywhere;
            screens=World.Screens.ToArray();screenMatrix=new Matrix4x4[screens.Length];screenBlock=new MaterialPropertyBlock[screens.Length];
            for(int i=0;i<screens.Length;i++)
            {
                var s=screens[i];screenMatrix[i]=Matrix4x4.TRS(U(s.Center),Quaternion.Euler(0,-s.Yaw*Mathf.Rad2Deg,0),new Vector3(s.Width,s.Height,1));
                var uv=NativeRooftopArt.AdUv(s.Kind);var b=new MaterialPropertyBlock();b.SetFloat(GainId,s.Gain);b.SetVector(UvRectId,new Vector4(uv.u0,uv.v0,uv.du,uv.dv));screenBlock[i]=b;
                if(!adJobs.ContainsKey(s.Kind)){string kind=s.Kind;adJobs[kind]=Task.Run(()=>NativeIconRaster.Raster(NativeIconRaster.Parse(NativeRooftopArt.Ad(kind),1,NativeIconRaster.Mode.Markup),NativeRooftopArt.AdPixels,100));}
            }
        }
        private static Material Make(string shader)=>new Material(Resources.Load<Shader>(shader)){hideFlags=HideFlags.HideAndDontSave};
        // A linear colour vector for the active colour space (the style's values are linear; a gamma project's shaders read gamma), w kept.
        private static Vector4 Lin(Vector4 v){if(QualitySettings.activeColorSpace==ColorSpace.Linear)return v;var c=new Color(v.x,v.y,v.z).gamma;return new Vector4(c.r,c.g,c.b,v.w);}
        // Static quads: corners A B C D (counter-clockwise about the normal in the source frame) take the face coordinates
        // (u0,v0) (u1,v0) (u1,v1) (u0,v1); mirrored, each is wound A C B, A D C.
        private static Mesh WorldMesh(List<RooftopQuad> quads)
        {
            int n=quads.Count;var pos=new Vector3[n*4];var nrm=new Vector3[n*4];var uv0=new Vector4[n*4];var uv1=new Vector4[n*4];var tri=new int[n*6];
            for(int i=0;i<n;i++)
            {
                var q=quads[i];int v=i*4,t=i*6;float mode=q.Mode;var attr=V4(q.Attr);var nn=U(q.Normal);
                pos[v]=U(q.A);pos[v+1]=U(q.B);pos[v+2]=U(q.C);pos[v+3]=U(q.D);nrm[v]=nrm[v+1]=nrm[v+2]=nrm[v+3]=nn;uv1[v]=uv1[v+1]=uv1[v+2]=uv1[v+3]=attr;
                uv0[v]=new Vector4(q.Uv.X,q.Uv.Y,mode,0);uv0[v+1]=new Vector4(q.Uv.Z,q.Uv.Y,mode,0);uv0[v+2]=new Vector4(q.Uv.Z,q.Uv.W,mode,0);uv0[v+3]=new Vector4(q.Uv.X,q.Uv.W,mode,0);
                tri[t]=v;tri[t+1]=v+2;tri[t+2]=v+1;tri[t+3]=v;tri[t+4]=v+3;tri[t+5]=v+2;
            }
            var mesh=new Mesh{name="Rooftop city",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};mesh.vertices=pos;mesh.normals=nrm;mesh.SetUVs(0,uv0);mesh.SetUVs(1,uv1);mesh.triangles=tri;mesh.bounds=Everywhere;mesh.UploadMeshData(true);return mesh;
        }
        // A unit screen quad facing +z; seen from the front (the camera's side) u runs left to right, so the ads read.
        private static Mesh Quad()
        {
            var m=new Mesh{name="Rooftop screen",hideFlags=HideFlags.HideAndDontSave};
            m.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};m.uv=new[]{new Vector2(1,0),new Vector2(0,0),new Vector2(0,1),new Vector2(1,1)};
            m.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward};m.triangles=new[]{0,2,1,0,3,2};m.RecalculateBounds();m.UploadMeshData(true);return m;
        }
        // Sprites as four vertices at their centre: uv0 corner, size and kind; uv1 drift (mirrored) and period; uv2 colour; uv3 seed.
        private static Mesh SpriteMesh(string name,List<RooftopSprite> list,int count)
        {
            var pos=new Vector3[count*4];var uv0=new Vector4[count*4];var uv1=new Vector4[count*4];var uv2=new Vector4[count*4];var uv3=new Vector4[count*4];var tri=new int[count*6];
            for(int i=0;i<count;i++)
            {
                var s=list!=null?list[i]:new RooftopSprite{Position=new NVector(0,-9999,0)};var p=U(s.Position);var c=V4(s.Color);var mv=new Vector4(-s.Move.X,s.Move.Y,s.Move.Z,s.Move.W);
                for(int k=0;k<4;k++){int v=i*4+k;pos[v]=p;uv0[v]=new Vector4(k==1||k==2?.5f:-.5f,k>=2?.5f:-.5f,s.Size,s.Kind);uv1[v]=mv;uv2[v]=c;uv3[v]=new Vector4(s.Seed,0,0,0);}
                int t=i*6,b=i*4;tri[t]=b;tri[t+1]=b+1;tri[t+2]=b+2;tri[t+3]=b;tri[t+4]=b+2;tri[t+5]=b+3;
            }
            var m=new Mesh{name=name,indexFormat=count*4>65535?IndexFormat.UInt32:IndexFormat.UInt16,hideFlags=HideFlags.HideAndDontSave};m.vertices=pos;m.SetUVs(0,uv0);m.SetUVs(1,uv1);m.SetUVs(2,uv2);m.SetUVs(3,uv3);m.triangles=tri;m.bounds=Everywhere;
            if(list!=null)m.UploadMeshData(true);return m;
        }
        // A rasterised ad (NativeRooftopArt markup through NativeIconRaster, straight RGBA, bottom row first) as an sRGB
        // texture with mipmaps, so far screens do not shimmer.
        private static Texture2D Ad(string kind,float[] rgba)
        {
            int px=NativeRooftopArt.AdPixels;
            var c=new Color32[px*px];for(int i=0;i<c.Length;i++){float a=rgba[i*4+3];c[i]=new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(rgba[i*4])*255),(byte)Mathf.RoundToInt(Mathf.Clamp01(rgba[i*4+1])*255),(byte)Mathf.RoundToInt(Mathf.Clamp01(rgba[i*4+2])*255),(byte)Mathf.RoundToInt(Mathf.Clamp01(a)*255));}
            var t=new Texture2D(px,px,TextureFormat.RGBA32,true,false){name="Rooftop ad "+kind,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4,hideFlags=HideFlags.HideAndDontSave};t.SetPixels32(c);t.Apply(true,true);return t;
        }

        /// <summary>
        /// Places `camera` on the loop at `time` (seconds, wrapped by the caller to a multiple of every period) and draws the
        /// scene for it alone. `reduced` (reduced effects) halves the camera's breathing and calms the screens' glitch.
        /// </summary>
        public void Draw(Camera camera,float time,bool reduced)
        {
            NativeRooftopCity.Camera(time,out var eye,out var forward,reduced?.5f:1);
            camera.transform.SetPositionAndRotation(U(eye),Quaternion.LookRotation(U(forward),Vector3.up));camera.fieldOfView=NativeRooftopCity.FovDegrees;camera.nearClipPlane=.5f;
            NativeLighting.Rooftop(camera,skyMaterial);
            Shader.SetGlobalFloat(TimeId,time);Shader.SetGlobalVector(RoofToonFogId,roofToonFog);var sign=U(World.SignLight);var cyan=U(World.CyanLight);Shader.SetGlobalVector(SignPosId,new Vector4(sign.x,sign.y,sign.z,1));Shader.SetGlobalVector(SignColorId,new Vector4(2.4f,.25f,1.9f,1));Shader.SetGlobalVector(CyanPosId,new Vector4(cyan.x,cyan.y,cyan.z,1));
            Graphics.DrawMesh(worldMesh,Matrix4x4.identity,worldMaterial,0,camera,0,null,ShadowCastingMode.Off,false);
            for(int i=0;i<screens.Length;i++)
            {
                string kind=screens[i].Kind;var b=screenBlock[i];
                if(!ads.TryGetValue(kind,out var tex)&&adJobs.TryGetValue(kind,out var job)&&job.IsCompleted)
                {
                    if(job.Status==TaskStatus.RanToCompletion)tex=Ad(kind,job.Result);else Debug.LogWarning("The rooftop's "+kind+" ad did not rasterise: "+job.Exception?.GetBaseException().Message);
                    ads[kind]=tex;
                }
                if(tex!=null)b.SetTexture(MainTexId,tex);b.SetFloat(CalmId,reduced?1:0);Graphics.DrawMesh(quad,screenMatrix[i],screenMaterial,0,camera,0,b,ShadowCastingMode.Off,false);
            }
            Traffic(time);Graphics.DrawMesh(cars,Matrix4x4.identity,worldMaterial,0,camera,0,null,ShadowCastingMode.Off,false);
            for(int i=0;i<sprites.Length;i++)if(sprites[i]!=null)Graphics.DrawMesh(sprites[i],Matrix4x4.identity,layers[i],0,camera,0,null,ShadowCastingMode.Off,false);
            Dynamic(time);Graphics.DrawMesh(dynamicMesh,Matrix4x4.identity,layers[5],0,camera,0,null,ShadowCastingMode.Off,false);
        }
        // ---- flying cars: a 2.3 x .7 x 4.6 body with a 1.6 x .55 x 2.2 cabin, facing along the lane (source frame), mirrored ----
        private const int CarVertices=48,CarIndices=72;
        private static readonly (NVector min,NVector max)[] CarBoxes={(new NVector(-1.15f,-.35f,-2.3f),new NVector(1.15f,.35f,2.3f)),(new NVector(-.8f,.325f,-1.4f),new NVector(.8f,.875f,.8f))};
        private void Traffic(float time)
        {
            int v=0;
            foreach(var lane in World.Lanes)for(int k=0;k<lane.Count;k++)
            {
                var p=NativeRooftopCity.LanePoint(lane,NativeRooftopCity.CarU(lane,k,time),out var tg);var f=tg*lane.Dir;var x=NVector.Normalize(NVector.Cross(NVector.UnitY,f));var y=NVector.Cross(f,x);
                foreach(var (min,max) in CarBoxes)for(int face=0;face<6;face++)
                {
                    var n=Faces[face];for(int c=0;c<4;c++)
                    {
                        var corner=Corner(face,c,min,max);var w=p+x*corner.X+y*corner.Y+f*corner.Z;var wn=x*n.X+y*n.Y+f*n.Z;
                        carPos[v]=U(w);carNormal[v]=U(wn);carUv[v]=new Vector4(corner.X,corner.Y,3,0);v++;
                    }
                }
            }
            cars.vertices=carPos;cars.normals=carNormal;cars.SetUVs(0,carUv);cars.bounds=Everywhere;
        }
        private static readonly NVector[] Faces={NVector.UnitY,NVector.UnitZ,-NVector.UnitZ,NVector.UnitX,-NVector.UnitX,-NVector.UnitY};
        // Each face's corners counter-clockwise about its normal (as NativeRooftopCity.Box writes them; the bottom added).
        private static NVector Corner(int face,int c,NVector a,NVector b)
        {
            switch(face)
            {
                case 0:return c==0?new NVector(a.X,b.Y,b.Z):c==1?new NVector(b.X,b.Y,b.Z):c==2?new NVector(b.X,b.Y,a.Z):new NVector(a.X,b.Y,a.Z);
                case 1:return c==0?new NVector(a.X,a.Y,b.Z):c==1?new NVector(b.X,a.Y,b.Z):c==2?new NVector(b.X,b.Y,b.Z):new NVector(a.X,b.Y,b.Z);
                case 2:return c==0?new NVector(b.X,a.Y,a.Z):c==1?new NVector(a.X,a.Y,a.Z):c==2?new NVector(a.X,b.Y,a.Z):new NVector(b.X,b.Y,a.Z);
                case 3:return c==0?new NVector(b.X,a.Y,b.Z):c==1?new NVector(b.X,a.Y,a.Z):c==2?new NVector(b.X,b.Y,a.Z):new NVector(b.X,b.Y,b.Z);
                case 4:return c==0?new NVector(a.X,a.Y,a.Z):c==1?new NVector(a.X,a.Y,b.Z):c==2?new NVector(a.X,b.Y,b.Z):new NVector(a.X,b.Y,a.Z);
                default:return c==0?new NVector(a.X,a.Y,a.Z):c==1?new NVector(b.X,a.Y,a.Z):c==2?new NVector(b.X,a.Y,b.Z):new NVector(a.X,a.Y,b.Z);
            }
        }
        // ---- the dynamic sprinkle ----
        private void Put(NVector p,float r,float g,float b,float a,float size)
        {
            if(dynCount>=DynamicCapacity||a<=0)return;var u=U(p);int v=dynCount*4;var col=new Vector4(r,g,b,a);float seed=(dynCount*.137f)%1;
            for(int k=0;k<4;k++){dynPos[v+k]=u;dynUv0[v+k]=new Vector4(k==1||k==2?.5f:-.5f,k>=2?.5f:-.5f,size,NativeRooftopCity.Glow);dynUv2[v+k]=col;dynUv3[v+k]=new Vector4(seed,0,0,0);}
            dynCount++;
        }
        private static float Frac(float x)=>x-Mathf.Floor(x);
        private static NVector Turn(NVector p,float yaw,NVector local)=>p+new NVector(local.Z*Mathf.Sin(yaw)+local.X*Mathf.Cos(yaw),local.Y,local.Z*Mathf.Cos(yaw)-local.X*Mathf.Sin(yaw));
        private void Dynamic(float time)
        {
            dynCount=0;
            // car head and tail lights and their fading trails
            foreach(var lane in World.Lanes)for(int k=0;k<lane.Count;k++)
            {
                var p=NativeRooftopCity.LanePoint(lane,NativeRooftopCity.CarU(lane,k,time),out var tg);tg*=lane.Dir;
                Put(p+tg*2.3f,1.6f,1.9f,2.2f,.9f,lane.Near?2.4f:3.2f);Put(p-tg*2.3f,2.6f,.2f,1.2f,.8f,lane.Near?1.6f:2.4f);
                if(Quality>=.5f)for(int s=1;s<9;s++)Put(p-tg*(2.3f+s*1.9f),lane.Color.X,lane.Color.Y,lane.Color.Z,.16f*(1-s/9f),lane.Near?1.1f:1.7f);
            }
            // the antenna beacon's blink
            {float c=Frac(time/2);Put(World.Beacon,2.6f,.25f,1.4f,c<.08f?1:c<.2f?1-(c-.08f)/.12f:0,2.2f);}
            // the faulty sign at the chip ad's corner spits a burst of falling sparks every 3.2 s
            {
                float cyc=Mathf.Repeat(time,3.2f);
                if(cyc<.7f)for(int k=0;k<26;k++){float h=Frac(Mathf.Sin(k*91.7f+Mathf.Floor(time/3.2f)*13.1f)*43758.5f),age=Mathf.Repeat(cyc+k*.027f,.7f);var vel=new NVector((h-.5f)*3,1.5f+h*2,(Frac(h*7)-.5f)*2);
                    Put(World.SparkAt+vel*age+new NVector(0,-4.9f*age*age,0),2.2f,1.8f,2.4f,1-age/.7f,.18f);}
            }
            // the duel: tracers from the gunner's muzzle to the blade, deflected in sparks
            if(World.Sites.TryGetValue("gunner",out var g)&&World.Sites.TryGetValue("blade",out var bl))
            {
                var muzzle=Turn(g.Position,g.Yaw,muzzleLocal);var hit=Turn(bl.Position,bl.Yaw,hitLocal);
                for(int i=0;i<NativeRooftopActors.Shots.Length;i++)
                {
                    float u=NativeRooftopActors.Tracer(time,i);
                    if(u>=0){for(int k=0;k<6;k++)Put(NVector.Lerp(muzzle,hit,Mathf.Clamp01(u-k*.04f)),2.6f,.5f,2.2f,(1-k/6f)*.9f,.5f);Put(muzzle,2.8f,1.6f,2.6f,1-u,1.6f);}
                    float age=NativeRooftopActors.Deflection(time,i);if(age<0)continue;float e=NativeRooftopActors.Shots[i]+NativeRooftopActors.TracerFlight;
                    for(int k=0;k<10;k++){float h=Frac(Mathf.Sin(k*12.9898f+e*78.2f)*43758.5f);var vel=new NVector((h-.5f)*4,1+h*3,(Frac(h*13)-.5f)*4);Put(hit+vel*age+new NVector(0,-4.9f*age*age,0),2.4f,2,1.6f,1-age/.35f,.2f);}
                    Put(hit,2.6f,2.4f,2.2f,1-age/.35f,1.4f);
                }
            }
            for(int i=dynCount*4;i<dynPos.Length;i++){dynPos[i]=new Vector3(0,-9999,0);dynUv2[i]=Vector4.zero;}
            dynamicMesh.vertices=dynPos;dynamicMesh.SetUVs(0,dynUv0);dynamicMesh.SetUVs(2,dynUv2);dynamicMesh.SetUVs(3,dynUv3);dynamicMesh.bounds=Everywhere;
        }
        public void Dispose()
        {
            foreach(var m in new[]{worldMesh,quad,cars,dynamicMesh})if(m!=null)CapturedAnimation.Release(m);foreach(var m in sprites)if(m!=null)CapturedAnimation.Release(m);
            foreach(var m in new[]{worldMaterial,skyMaterial,screenMaterial})if(m!=null)CapturedAnimation.Release(m);foreach(var m in layers)if(m!=null)CapturedAnimation.Release(m);
            foreach(var t in ads.Values)if(t!=null)CapturedAnimation.Release(t);ads.Clear();
        }
    }

    public sealed partial class ArenaPresentation
    {
        // The rooftop scene (built on first use, rebuilt when the quality tier changes) and its cast: the player's seated pilot
        // (0) and the distant pilots (NativeRooftopActors.Crowd), posed by the game's own pose code each frame.
        private NativeRooftopScene rooftop;private bool rooftopFailed;private RooftopCast rooftopCast;
        /// <summary>Whether the home screen can show the rooftop (its shaders run here and it has not failed this session).</summary>
        public bool RooftopSupported=>!rooftopFailed&&sourceModels!=null&&NativeRooftopScene.Supported;
        private sealed class RooftopCast
        {
            public readonly int Count;public readonly NativeRooftopActors.Look[] Looks;public readonly NativeRooftopActors.State[] States;
            public readonly Matrix4x4[][] Bones;public readonly Matrix4x4[] Group;public readonly Func<int,Matrix4x4>[] Bone,Held;public readonly Func<int,bool>[] HeldShown;
            public readonly Color[] Body,Accent;public string SeatKey;public float SeatY;public int BodySkin=-1;public string HatColor;public Matrix4x4 Prop;public int PropBone;public bool PropShown;
            public readonly Func<int,Matrix4x4> PropMatrix;public readonly Func<int,bool> PropPart;
            public RooftopCast()
            {
                var crowd=NativeRooftopActors.Crowd;Count=crowd.Length+1;Looks=new NativeRooftopActors.Look[Count];States=new NativeRooftopActors.State[Count];Bones=new Matrix4x4[Count][];Group=new Matrix4x4[Count];
                Bone=new Func<int,Matrix4x4>[Count];Held=new Func<int,Matrix4x4>[Count];HeldShown=new Func<int,bool>[Count];Body=new Color[Count];Accent=new Color[Count];
                Looks[0]=new NativeRooftopActors.Look{Id="you",Script=NativeRooftopActors.Seated,Spec="pistol",Hat="none",Body="#e5e7eb",Accent="#22e6ff"};
                for(int i=0;i<Count;i++)
                {
                    if(i>0){Looks[i]=crowd[i-1];Body[i]=BodyTint(Looks[i].Body);Accent[i]=AccentTint(Looks[i].Accent);}
                    var s=States[i]=new NativeRooftopActors.State();var bones=Bones[i]=new Matrix4x4[CapturedRig.Bones.Length];
                    Bone[i]=b=>bones[b];
                    // A held weapon rides its hand with the pose's held offset (NativeLobbyPoses' socket; zero for the clip poses).
                    Held[i]=b=>s.Weapon.Visible&&b==s.Weapon.Bone?bones[b]*Offset(s.Weapon):s.Offhand.Visible&&b==s.Offhand.Bone?bones[b]*Offset(s.Offhand):bones[b];
                    HeldShown[i]=b=>s.Weapon.Visible&&b==s.Weapon.Bone||s.Offhand.Visible&&b==s.Offhand.Bone;
                }
                PropMatrix=_=>Prop;PropPart=b=>b==PropBone;
            }
            private static Matrix4x4 Offset(LobbyHeld h)=>Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(h.Position)),CapturedAnimation.Quat(CapturedRig.Mirror(h.Rotation)),Vector3.one);
        }
        /// <summary>
        /// Draws the home screen's rooftop for `camera` at `time` (seconds, wrapped by the caller), with the player's pilot
        /// seated on the lip in its own look. `quality` 1 high, .6 reduced effects, .34 the low graphics tier. False when the
        /// scene cannot be drawn (the menu then keeps the orbiting map); a failure is logged once and not retried.
        /// </summary>
        public bool RenderRooftop(Camera camera,float time,RooftopPilot pilot,float quality,bool reduced)
        {
            if(!RooftopSupported||camera==null)return false;
            try
            {
                if(rooftop==null||Mathf.Abs(rooftop.Quality-quality)>.01f){rooftop?.Dispose();rooftop=null;rooftop=new NativeRooftopScene(quality);}
                rooftop.Draw(camera,time,reduced);RooftopCastFrame(camera,time,pilot,reduced);return true;
            }
            catch(Exception e){Debug.LogException(e);rooftopFailed=true;rooftop?.Dispose();rooftop=null;return false;}
        }
        private void RooftopCastFrame(Camera camera,float time,RooftopPilot pilot,bool reduced)
        {
            var cast=rooftopCast??=new RooftopCast();var you=cast.Looks[0];
            string spec=string.IsNullOrEmpty(pilot.Spec)?"pistol":pilot.Spec,hat=string.IsNullOrEmpty(pilot.Hat)?"none":pilot.Hat;
            if(you.Spec!=spec||you.Hat!=hat||you.Body!=pilot.Body||you.Accent!=pilot.Accent||cast.BodySkin!=pilot.BodySkin||cast.HatColor!=pilot.HatColor||cast.SeatKey==null){you.Spec=spec;you.Hat=hat;you.Body=pilot.Body;you.Accent=pilot.Accent;cast.BodySkin=pilot.BodySkin;cast.HatColor=pilot.HatColor;cast.Body[0]=BodyTint(pilot.Body);cast.Accent[0]=BodyAccent(pilot.BodySkin,pilot.Accent,hat,pilot.HatColor);}
            // The seat height once per spec (the chair pose at t=0), so the pilot never bobs on the lip.
            if(cast.SeatKey!=spec){cast.SeatKey=spec;NativeRooftopActors.Sample(you,cast.States[0],0);cast.SeatY=NativeRooftopActors.SeatHeight(cast.States[0]);}
            sourceModels.Begin();var sites=rooftop.World.Sites;
            for(int i=0;i<cast.Count;i++)
            {
                var look=cast.Looks[i];var s=cast.States[i];NativeRooftopActors.Sample(look,s,time);if(s.Hidden)continue;
                NVector feet;float yaw;
                if(i==0){feet=NativeRooftopCity.PilotSeat+new NVector(0,cast.SeatY,0);yaw=Mathf.PI;}
                else{if(!sites.TryGetValue(look.Id,out var site))continue;yaw=site.Yaw;var o=s.Offset;feet=site.Position+new NVector(o.Z*Mathf.Sin(yaw)+o.X*Mathf.Cos(yaw),o.Y,o.Z*Mathf.Cos(yaw)-o.X*Mathf.Sin(yaw));}
                var root=Matrix4x4.TRS(new Vector3(-feet.X,feet.Y,feet.Z),Quaternion.Euler(0,-yaw*Mathf.Rad2Deg,0),Vector3.one);var group=cast.Group[i]=root*Matrix4x4.Translate(new Vector3(0,NativePortraitRules.GroupY,0));
                var bones=cast.Bones[i];var p=s.Pose;for(int b=0;b<bones.Length;b++)bones[b]=group*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(p.WorldPosition[b])),CapturedAnimation.Quat(CapturedRig.Mirror(p.WorldRotation[b])),Vector3.one);
                bool siren=look.Spec=="siren";
                // Your pilot in its body skin; the crowd in the Standard body at its distance (NativeBodySkinPresentation.cs).
                string body=siren?"siren":BodyModel(i==0?pilot.BodySkin:NativeBodySkins.StandardIndex,NativeBodySkins.Tier(LowGraphics,reduced?.3f:1,i==0?0:Vector3.Distance(camera.transform.position,group.GetColumn(3)),false));
                sourceModels.Queue(body,group,cast.Bone[i],null,0,camera,cast.Body[i],cast.Accent[i],look:siren?SirenStill(reduced?0:time):null);
                // Your pilot's hat in its colour, the Sheldon Head animated (NativeHatPresentation.cs); the crowd keeps Auto.
                string hatColor=i==0?pilot.HatColor:null;
                if(!siren&&look.Hat!="none"&&!AuditorHalo(look.Hat,bones[CapturedRig.Head],time,1,reduced,0,camera,camera,hatColor)&&!SheldonStill(look.Hat,hatColor,group,cast.Bone[i],bones[CapturedRig.Head],time,i,reduced,0,camera))sourceModels.Queue(HatModel(look.Hat,hatColor),group,cast.Bone[i],null,0,camera,body:HatBody(look.Hat,hatColor));
                if(i>0&&!siren&&(s.Weapon.Visible||s.Offhand.Visible))sourceModels.Queue("weapon-"+look.Spec,group,cast.Held[i],cast.HeldShown[i],0,camera,accent:cast.Accent[i],glow:HeldGlow);
                // The seated pilot's weapon lies flat on the lip at its right, pointing along it (fists stay on the hands).
                if(i==0&&!siren&&!NativeLobbyPoses.Fighter(look.Spec))
                {
                    cast.PropBone=NativeLobbyPoses.Magic(look.Spec)?CapturedRig.LHand:CapturedRig.RHand;
                    cast.Prop=root*Matrix4x4.TRS(new Vector3(.40f,.37f-cast.SeatY,-.02f),Quaternion.Euler(0,(Mathf.PI/2-.06f)*Mathf.Rad2Deg,0),Vector3.one);
                    sourceModels.Queue("weapon-"+look.Spec,root,cast.PropMatrix,cast.PropPart,0,camera,accent:cast.Accent[i],glow:HeldGlow);
                }
            }
            sourceModels.Flush(UsesInstancing);
        }
    }
}
