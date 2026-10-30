using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The advanced lighting's runtime (docs/LIGHTING-ADVANCED.md; Core NativeAdvancedLooks, NativeLightVolume, NativeWeatherFx;
    /// Resources/NativeAdvancedLight.cginc): every global the world shaders read for it, the light volume's bake and texture,
    /// the lightning and the wet clock. NativeLighting.Apply hands it each map's look (<see cref="Apply"/>); a stage, the draft
    /// and the rooftop turn it off (<see cref="Off"/>, from NativeLighting.StyleOff and Rooftop) until the next Apply. The tier
    /// comes from Settings (<see cref="Graphics"/>): on the low tier, and for every look without `advanced`, every global is 0
    /// and the shaders draw exactly today's look.
    /// <para>The volume: the first frame after an Apply that the arena draws (<see cref="Frame"/>; by then the map's presentation
    /// has handed its light candidates, <see cref="Candidates"/>) starts the bake on a worker thread (Core NativeLightVolume.Build
    /// over the map at rest, the styled look's key, ambient and lights); the frame it is done uploads it once (an RGBAHalf
    /// 3D texture, bilinear, clamped) and turns the volume on. A map or look already baked is reused (the killcam's replay, a
    /// round's reset, a rematch). A failed bake or a platform without 3D half textures keeps the volume off.</para>
    /// Nothing allocates per frame once a volume is up.
    /// </summary>
    public static class NativeAdvancedLighting
    {
        public static NativeGraphicsTier Tier{get;private set;}=NativeGraphicsTier.High;public static bool Reduced{get;private set;}
        /// <summary>What applies now (all off while a stage owns the screen or the look has none).</summary>
        public static readonly AdvancedLightState State=new AdvancedLightState();
        private static ContentMap map;private static MapLook look;private static bool on,written;private static int appliedFrame=-10;
        private static readonly int GiId=Shader.PropertyToID("_ArenaGI"),GiBoxId=Shader.PropertyToID("_ArenaGIBox"),GiSizeId=Shader.PropertyToID("_ArenaGISize"),GiVolumeId=Shader.PropertyToID("_ArenaGIVolume"),LightningId=Shader.PropertyToID("_ArenaLightning"),
            ScatterId=Shader.PropertyToID("_ArenaScatter"),ScatterFogId=Shader.PropertyToID("_ArenaScatterFog"),ScatterKeyId=Shader.PropertyToID("_ArenaScatterKey"),ScatterDirId=Shader.PropertyToID("_ArenaScatterDir"),
            WetId=Shader.PropertyToID("_ArenaWet"),WetSkyId=Shader.PropertyToID("_ArenaWetSky"),SkyFlashId=Shader.PropertyToID("_SkyFlash"),CoverId=Shader.PropertyToID("_ArenaWetCover"),CoverBoxId=Shader.PropertyToID("_ArenaWetCoverBox");
        private static Texture2D coverTexture;private static NativeWeatherCover coverOf;
        // The volume: the bake in flight (its key), the one up (its key and texture).
        private static System.Threading.Tasks.Task<(NativeLightVolume volume,ushort[] half)> bake;private static string bakeKey,volumeKey;private static NativeLightVolume volume;private static Texture3D volumeTexture;
        private static readonly List<NativeVolumeLight> candidates=new List<NativeVolumeLight>();private static string candidatesMap;
        /// <summary>The light volume's cell size and bake time once up (the HUD's diagnostics; 0 while none).</summary>
        public static float VolumeCell=>volume?.Cell??0;public static double VolumeMs=>volume?.BakeMs??0;

        /// <summary>The tier and Reduced effects (Settings); re-applies the current look.</summary>
        public static void Graphics(NativeGraphicsTier tier,bool reduced){if(tier==Tier&&reduced==Reduced)return;Tier=tier;Reduced=reduced;if(map!=null&&look!=null)Apply(map,look);}
        /// <summary>A map's look applied (NativeLighting.Apply, after its own globals): resolves what this tier draws and writes it.</summary>
        public static void Apply(ContentMap m,MapLook l)
        {
            if(!ReferenceEquals(m,map))candidates.Clear();map=m;look=l;keyStale=true;appliedFrame=Time.frameCount;NativeAdvancedLooks.Resolve(l,Tier,Reduced,State);on=State.Any;
            if(!on){Off();return;}
            Write();
        }
        /// <summary>A map's own light candidates for the volume (the town's, the station's, the Ironworks' practical lights: more
        /// than the eight slots), handed by its presentation before the bake starts; the look's eight are always in.</summary>
        public static void Candidates(string mapId,IList<NativeVolumeLight> lights)
        {
            if(map==null||map.id!=mapId||candidatesMap==mapId&&candidates.Count>0)return;candidatesMap=mapId;candidates.Clear();keyStale=true;
            if(lights!=null)for(int i=0;i<lights.Count&&candidates.Count<NativeLightVolume.MaxLights-NativeMapLooks.MaxLights;i++)candidates.Add(lights[i]);
        }
        /// <summary>Everything off (a stage, the draft, the rooftop; the low tier; a look without it). Apply turns it back on.</summary>
        public static void Off()
        {
            if(!written)return;written=false;
            Shader.SetGlobalVector(GiId,Vector4.zero);Shader.SetGlobalVector(LightningId,Vector4.zero);Shader.SetGlobalVector(ScatterId,Vector4.zero);Shader.SetGlobalVector(WetId,Vector4.zero);Shader.SetGlobalFloat(SkyFlashId,0);
        }
        private static bool Live=>on&&written;
        // The static part of the globals (the per-frame part is Frame's).
        private static void Write()
        {
            written=true;var s=State;
            // The frame's part starts from nothing (Frame writes it each frame the arena draws; behind the menu it stays 0).
            Shader.SetGlobalVector(LightningId,Vector4.zero);Shader.SetGlobalFloat(SkyFlashId,0);Shader.SetGlobalVector(ScatterKeyId,Vector4.zero);Shader.SetGlobalVector(WetId,Vector4.zero);
            if(s.Scatter){var c=Lin(s.ScatterColor);Shader.SetGlobalVector(ScatterId,new Vector4(c.r,c.g,c.b,s.Max));Shader.SetGlobalVector(ScatterFogId,new Vector4(s.Density,s.Height,s.Falloff,s.Lights));}else Shader.SetGlobalVector(ScatterId,Vector4.zero);
            // What a wet face mirrors: the sky's light and the city's glow in the fog.
            if(s.Wet>0){var sky=RenderSettings.ambientSkyColor+RenderSettings.fogColor;var c=QualitySettings.activeColorSpace==ColorSpace.Linear?sky.linear:sky;Shader.SetGlobalVector(WetSkyId,new Vector4(c.r*1.5f,c.g*1.5f,c.b*1.5f,1));}
            VolumeGlobals();
        }
        private static void VolumeGlobals()
        {
            var s=State;bool ready=s.Gi&&volumeTexture!=null&&volumeKey==Key();
            if(!ready){Shader.SetGlobalVector(GiId,Vector4.zero);return;}
            var v=volume;Shader.SetGlobalTexture(GiVolumeId,volumeTexture);Shader.SetGlobalVector(GiBoxId,new Vector4(v.MinX,v.MinY,v.MinZ,v.Cell));Shader.SetGlobalVector(GiSizeId,new Vector4(v.Nx*v.Cell,v.Ny*v.Cell,v.Nz*v.Cell,s.GiActors));
            Shader.SetGlobalVector(GiId,new Vector4(s.GiOcclusion,s.GiBounce,s.GiFloor,1));
        }
        // The volume's identity (map, look, cell, rays, the candidates' count), built again only when one of them changes (Apply,
        // Candidates): Frame and VolumeGlobals read it every frame and must not allocate.
        private static string keyCache;private static bool keyStale=true;
        private static string Key()
        {
            if(keyStale){keyCache=map==null||look==null?null:map.id+"|"+look.look+"|"+State.GiCell.ToString("0.###")+"|"+State.GiRays+"|"+candidates.Count;keyStale=false;}
            return keyCache;
        }
        /// <summary>
        /// Each frame the arena draws (ArenaPresentation's AdvancedLightingWorld; `time` the presentation clock): starts or takes
        /// the volume's bake, and writes the scattering's key (the live key light: the zone boss song's house light moves it),
        /// the lightning and the wet clock.
        /// </summary>
        public static void Frame(float time)
        {
            if(!Live)return;var s=State;
            if(s.Gi)Bake();
            var sun=RenderSettings.sun;Vector3 toward=Vector3.up;Color key=Color.black;
            if(sun!=null&&sun.isActiveAndEnabled){toward=-sun.transform.forward;key=GraphicsSettings.lightsUseLinearIntensity?sun.color.linear*sun.intensity:(sun.color*sun.intensity).linear;}
            if(s.Scatter){Shader.SetGlobalVector(ScatterKeyId,new Vector4(key.r*s.Key,key.g*s.Key,key.b*s.Key,s.Phase));Shader.SetGlobalVector(ScatterDirId,new Vector4(toward.x,toward.y,toward.z,0));}
            float flash=s.Lightning>0?(float)NativeWeatherFx.Lightning(time,s.Interval,map!=null?map.id.Length*7919:0,Reduced)*s.Lightning:0;
            if(flash>.001f){var c=Lin(s.LightningColor!=0?s.LightningColor:0xC8D4FF)*flash;Shader.SetGlobalVector(LightningId,new Vector4(c.r,c.g,c.b,1));Shader.SetGlobalFloat(SkyFlashId,flash);}
            else{Shader.SetGlobalVector(LightningId,Vector4.zero);Shader.SetGlobalFloat(SkyFlashId,0);}
            if(s.Wet>0)Shader.SetGlobalVector(WetId,new Vector4(s.Wet,s.Puddles,time,s.Weather==NativeAdvancedLooks.Snow?0:Mathf.Clamp01(s.Amount*1.4f)));
        }
        // The bake: started a frame after the Apply (the presentation's candidates are in by then), once per key; taken when done.
        private static void Bake()
        {
            string key=Key();if(key==null)return;
            if(bake!=null&&bake.IsCompleted)
            {
                var done=bake;string doneKey=bakeKey;bake=null;bakeKey=null;
                if(done.IsFaulted||done.IsCanceled)Debug.LogWarning("Light volume disabled: "+(done.Exception?.GetBaseException().Message??"cancelled"));
                else if(doneKey==key)Upload(done.Result.volume,done.Result.half,key);
            }
            if(volumeKey==key||bake!=null||Time.frameCount<appliedFrame+2)return;
            if(!SystemInfo.supports3DTextures||!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf)){volumeKey=key;return;}
            var input=NativeLightVolume.From(map,look,MaterialColor,candidates,State.GiCell,State.GiRays);bakeKey=key;
            bake=System.Threading.Tasks.Task.Run(()=>{var v=NativeLightVolume.Build(input);return (v,NativeLightVolume.Half(v.Data));});
        }
        private static void Upload(NativeLightVolume v,ushort[] half,string key)
        {
            Release();
            try
            {
                var tex=new Texture3D(v.Nx,v.Ny,v.Nz,TextureFormat.RGBAHalf,false){name="Light volume "+(map?.id??""),wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,anisoLevel=0};
                tex.SetPixelData(half,0);tex.Apply(false,true);volumeTexture=tex;v.Data=Array.Empty<float>();volume=v;volumeKey=key;
                Debug.Log($"Light volume {map?.id}: {v.Nx}x{v.Ny}x{v.Nz} cells of {v.Cell:0.00} m, {v.Lights} lights, {v.BakeMs:0} ms");
            }
            catch(Exception e){Debug.LogWarning("Light volume disabled: "+e.Message);Release();volumeKey=key;}
            VolumeGlobals();
        }
        private static void Release(){if(volumeTexture!=null)CapturedAnimation.Release(volumeTexture);volumeTexture=null;volume=null;volumeKey=null;}
        /// <summary>The weather's cover for the wet faces (ArenaPresentation's weather hands it once per map): an RHalf height map,
        /// bilinear, clamped; without RHalf, no cover (everything wet).</summary>
        public static void Cover(NativeWeatherCover c)
        {
            if(ReferenceEquals(c,coverOf))return;coverOf=c;if(coverTexture!=null)CapturedAnimation.Release(coverTexture);coverTexture=null;
            if(c==null||c.Roof.Length==0||!SystemInfo.SupportsTextureFormat(TextureFormat.RHalf)){Shader.SetGlobalVector(CoverBoxId,Vector4.zero);return;}
            try
            {
                var tex=new Texture2D(c.MapX,c.MapZ,TextureFormat.RHalf,false,true){name="Weather cover",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                tex.SetPixelData(NativeLightVolume.Half(c.Roof),0);tex.Apply(false,true);coverTexture=tex;
                Shader.SetGlobalTexture(CoverId,tex);Shader.SetGlobalVector(CoverBoxId,new Vector4(c.OriginX,c.OriginZ,c.MapX,c.MapZ));
            }
            catch(Exception e){Debug.LogWarning("Weather cover disabled: "+e.Message);Shader.SetGlobalVector(CoverBoxId,Vector4.zero);}
        }
        // The content materials' colours (the dressing's base colour for a block without a look), from the arena's content.
        private static readonly Dictionary<string,int> materialColors=new Dictionary<string,int>();private static ContentMaterial[] materialsFrom;
        /// <summary>The arena's content materials (the arena hands them each frame; read once per content).</summary>
        public static void Materials(ContentMaterial[] list){if(ReferenceEquals(list,materialsFrom))return;materialsFrom=list;materialColors.Clear();foreach(var m in list??Array.Empty<ContentMaterial>())if(m?.id!=null)materialColors[m.id]=m.color;}
        private static int MaterialColor(string id)=>materialColors.TryGetValue(id??"",out int v)?v:0x8A9496;
        private static Color Lin(int hex){var c=NativeArena.ColorOf(hex);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}
    }
}
