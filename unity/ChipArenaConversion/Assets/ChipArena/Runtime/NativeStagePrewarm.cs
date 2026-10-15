using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        /// <summary>
        /// Builds the stage's lazily built presentation now, behind the stage loading screen, through the same guards its first
        /// arena frame would take (so nothing is built twice and nothing is drawn): the Factory kit and rigs, the Brickfall
        /// scenery, the Cyber City exits, kit and fx scene, the Expedition town kit, fx scene and lights, the revamped Ward
        /// Zero kit, fx scene and lights, and the shared hazard kit's material. Without it an entrance intro hides the arena,
        /// so these built on the intro's last frame, a visible stall as the countdown began. Returns how many stage kits it
        /// prepared. Presentation only: it reads the map's content records, never the simulation.
        /// </summary>
        public int PrewarmStage(NativeArena arena)
        {
            if(arena==null||sourceModels==null)return 0;var map=arena.Map;string id=arena.MapId;int built=0;
            if(NativeFactoryScenery.Active(id)){if(factoryStage==null)factoryStage=gameObject.AddComponent<NativeFactoryStage>();if(factoryStage.Prepare(arena))built++;}
            if(NativeBrickfallScenery.Active(id)&&EnsureBrickfall())built++;
            if(NativeCyberCityLooks.Active(id)&&EnsureCyberCity()){if(!ReferenceEquals(arena,metroArena)||arena.Generation!=metroGeneration)MetroRebind(arena);built++;}
            if(map!=null&&NativeExpeditionCityKit.Active(id)&&EnsureExpeditionCity()){if(!ReferenceEquals(map,townMap))ExpeditionCityBuild(map);built++;}
            if(map!=null&&NativeWardZeroLooks.Active(map)&&EnsureWardZero()){if(!ReferenceEquals(map,wzMap))WardZeroBuild(map);built++;}
            EnsureHazardKit();
            return built;
        }
    }

    public sealed partial class NativeFactoryStage
    {
        /// <summary>The Factory's materials, scenery sectors and this map's kit and rig builder, as Render builds them on its
        /// first frame (ArenaPresentation.PrewarmStage); nothing is drawn.</summary>
        public bool Prepare(NativeArena arena)
        {
            if(arena==null||!Ensure())return false;var map=arena.Map;if(map==null)return false;
            if(!ReferenceEquals(map,kitMap))
            {
                var hz=Hazards(arena.MapId);kitMap=map;
                try{looks=NativeFactoryKit.Resolve(NativeLighting.Look(map).kitLooks);rigBuilder=new NativeFactoryRig(looks);Pack(NativeFactoryKit.Build(map.blocks,hz,looks));}
                catch(Exception e){Debug.LogWarning("Factory dressing disabled: "+e.Message);kit=Array.Empty<Batch>();rigBuilder=null;}
            }
            return true;
        }
    }

    /// <summary>
    /// The loading screen's shader warm-up: every shader in Resources drawn once, plain and instanced, casting and receiving
    /// shadows, into a hidden 32x32 target by a disabled camera that copies the live camera's settings, under the stage's own
    /// fog and lights, so the GPU programs the stage's effects need are created behind the loading screen and not on their
    /// first use in play (a death effect, a blast, a hit screen, a revive hologram). It runs once per fog state (fog off, or
    /// its mode: the variant the stage's fog picks), PerFrame shaders a frame, and destroys its materials when it finishes.
    /// Nothing reaches the screen. A shader that cannot be drawn is skipped; any failure ends the warm-up quietly.
    /// </summary>
    internal sealed class NativeShaderWarmup:IDisposable
    {
        public const int Layer=31,PerFrame=8,Size=32;
        private Shader[] shaders;private Material[] plain,instanced;private int next,key=-1,warmed;private bool failed;
        private Camera camera;private RenderTexture target;private Mesh quad;private readonly Matrix4x4[] pair=new Matrix4x4[2];
        public int Warmed=>warmed;
        public int Total=>shaders?.Length??0;
        /// <summary>The fog state the variants depend on: 0 off, else 1 + the fog mode.</summary>
        public static int FogKey()=>RenderSettings.fog?1+(int)RenderSettings.fogMode:0;
        public bool Needed(int fog)=>!failed&&(shaders==null||fog!=key||next<shaders.Length);
        public float Progress=>shaders==null||shaders.Length==0?0:next/(float)shaders.Length;
        public void Step(Camera reference,int fog,int budget)
        {
            if(failed||reference==null)return;
            try
            {
                if(shaders==null){shaders=Resources.LoadAll<Shader>("");plain=new Material[shaders.Length];instanced=new Material[shaders.Length];}
                if(fog!=key){key=fog;next=0;}
                if(next>=shaders.Length)return;Ensure(reference);
                var t=camera.transform;Vector3 at=t.position+t.forward*3;
                int end=Math.Min(shaders.Length,next+Math.Max(1,budget));
                for(int i=next;i<end;i++)
                {
                    var s=shaders[i];if(s==null||!s.isSupported)continue;
                    // Plain (the compatibility path and the shader's only variant), and instanced where the shader has the variant.
                    var m=plain[i];if(m==null){m=plain[i]=new Material(s){name="Warm-up "+s.name,hideFlags=HideFlags.HideAndDontSave,enableInstancing=false};}
                    var place=Matrix4x4.TRS(at+t.right*((i-next)*.2f-.7f),t.rotation,Vector3.one*.5f);
                    Graphics.DrawMesh(quad,place,m,Layer,camera,0,null,ShadowCastingMode.On,true);
                    if(SystemInfo.supportsInstancing&&StagePreloadPlan.Instanced(s.name))
                    {
                        var mi=instanced[i];if(mi==null){mi=instanced[i]=new Material(s){name="Warm-up instanced "+s.name,hideFlags=HideFlags.HideAndDontSave,enableInstancing=true};}
                        pair[0]=place;pair[1]=Matrix4x4.TRS(at-t.up*.4f,t.rotation,Vector3.one*.4f);
                        try{Graphics.DrawMeshInstanced(quad,0,mi,pair,2,null,ShadowCastingMode.On,true,Layer,camera);}catch(InvalidOperationException){}catch(ArgumentException){}
                    }
                    warmed++;
                }
                next=end;camera.Render();
                if(next>=shaders.Length)ReleaseMaterials();
            }
            catch(Exception e){failed=true;ReleaseMaterials();Debug.LogWarning("Shader warm-up skipped: "+e.Message);}
        }
        private void Ensure(Camera reference)
        {
            if(quad==null)
            {
                quad=new Mesh{name="Warm-up quad",hideFlags=HideFlags.HideAndDontSave};
                quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)};
                quad.normals=new[]{Vector3.back,Vector3.back,Vector3.back,Vector3.back};quad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)};
                quad.colors=new[]{Color.white,Color.white,Color.white,Color.white};quad.SetUVs(1,new[]{Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
                quad.triangles=new[]{0,2,1,2,3,1};quad.RecalculateBounds();quad.bounds=new Bounds(Vector3.zero,Vector3.one*4);quad.UploadMeshData(false);
            }
            if(target==null){target=new RenderTexture(Size,Size,24,reference.allowHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default){name="Shader warm-up",hideFlags=HideFlags.HideAndDontSave};target.Create();}
            if(camera==null){var go=new GameObject("Shader warm-up"){hideFlags=HideFlags.HideAndDontSave};camera=go.AddComponent<Camera>();camera.enabled=false;}
            // The live camera's settings (HDR, depth mode, clipping), pointed away from the stage at its own target and layer.
            camera.CopyFrom(reference);camera.enabled=false;camera.targetTexture=target;camera.cullingMask=1<<Layer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.allowMSAA=false;camera.transform.SetPositionAndRotation(reference.transform.position+Vector3.down*400,Quaternion.identity);
        }
        private void ReleaseMaterials(){Release(plain);Release(instanced);}
        private static void Release(Material[] set){if(set==null)return;for(int i=0;i<set.Length;i++)if(set[i]!=null){CapturedAnimation.Release(set[i]);set[i]=null;}}
        public void Dispose()
        {
            ReleaseMaterials();if(camera!=null){CapturedAnimation.Release(camera.gameObject);camera=null;}
            if(target!=null){target.Release();CapturedAnimation.Release(target);target=null;}if(quad!=null){CapturedAnimation.Release(quad);quad=null;}
        }
    }
}
