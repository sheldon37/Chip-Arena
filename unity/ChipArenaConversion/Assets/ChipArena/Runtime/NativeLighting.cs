using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    public static class NativeLighting
    {
        private static Material sky;private static Light moon,rim;private static bool introActive;private static readonly List<Light> stageLights=new List<Light>();
        // Per-map looks (Resources/MapLooks.json, Core/NativeMapLooks.cs): the one Apply last used, and the low graphics tier's
        // shadow fallback. The rim's own direction (`rimFrom`/`rimHome`): Create aims it from (14,7,12) and only Create writes
        // it. The draft stage turns the rim for itself, and Apply puts a look without a rim direction back on (14,7,12), so the
        // rim no longer keeps the draft's angle after the first draft (owner fix, as Electron keeps (14,7,12) throughout).
        private static MapLookSet looks;private static MapLook current;private static bool lowShadows;private static Vector3 rimFrom;private static Quaternion rimHome=Quaternion.identity;
        // What Apply writes for the current look (Core NativeMapLooks.Applied, the mapping MapLookChecks compares to the frozen
        // ce3c506 oracle), and the fog an entrance or the draft stage found when it took the screen (EndIntro gives it back).
        private static readonly MapLookState applied=new MapLookState();private static MapFogState stageFog;
        // The practical lights' globals for NativeBlockLit's own light block (only dressed blocks read them): position and
        // range, linear colour times intensity (times the flicker each frame), and the count. Unused slots sit far below.
        private static readonly Vector4[] arenaPos=new Vector4[NativeMapLooks.MaxLights],arenaBase=new Vector4[NativeMapLooks.MaxLights],arenaColor=new Vector4[NativeMapLooks.MaxLights];private static int arenaCount;private static bool arenaFlicker;
        private static readonly int ArenaPosId=Shader.PropertyToID("_ArenaLightPos"),ArenaColorId=Shader.PropertyToID("_ArenaLightColor"),ArenaCountId=Shader.PropertyToID("_ArenaLightCount");
        private static readonly int HorizonId=Shader.PropertyToID("_Horizon"),ZenithId=Shader.PropertyToID("_Zenith"),MoonSizeId=Shader.PropertyToID("_MoonSize"),SunDirId=Shader.PropertyToID("_SunDir"),DiscColorId=Shader.PropertyToID("_DiscColor"),DiscShadeId=Shader.PropertyToID("_DiscShade");
        private static readonly int HaloColorId=Shader.PropertyToID("_HaloColor"),HaloFalloffId=Shader.PropertyToID("_HaloFalloff"),StarsId=Shader.PropertyToID("_Stars"),CloudTintId=Shader.PropertyToID("_CloudTint"),CloudSpeedId=Shader.PropertyToID("_CloudSpeed");
        // The entrance stage's point lights for the source shaders' stage block (NativeStageLighting; source frame in `points`,
        // Unity frame in the uniform arrays). Unused slots sit far below the stage with no colour and decay 2, so the block's
        // pow() never sees a zero distance with a zero exponent.
        private static readonly NativeStageLight[] points=new NativeStageLight[NativeStageLighting.Max];private static readonly Vector4[] pointPos=new Vector4[NativeStageLighting.Max],pointColor=new Vector4[NativeStageLighting.Max];
        private static readonly int StagePosId=Shader.PropertyToID("_StageLightPos"),StageColorId=Shader.PropertyToID("_StageLightColor");
        // The arrays are globals; only the entrance's own draws enable the block (NativeSourceModels.Stage sets `_StageLit`).
        private static void StageBlock(int count)
        {
            for(int i=0;i<pointPos.Length;i++)
            {
                if(i<count){var l=points[i];pointPos[i]=new Vector4(-(float)l.X,(float)l.Y,(float)l.Z,(float)l.Distance);pointColor[i]=new Vector4((float)l.R,(float)l.G,(float)l.B,(float)Math.Max(.01,l.Decay));}
                else{pointPos[i]=new Vector4(0,-10000,0,0);pointColor[i]=new Vector4(0,0,0,2);}
            }
            Shader.SetGlobalVectorArray(StagePosId,pointPos);Shader.SetGlobalVectorArray(StageColorId,pointColor);
        }
        public static void EndIntro(ContentMap map,Camera camera){if(!introActive)return;introActive=false;foreach(var light in stageLights)if(light!=null)light.enabled=false;if(moon!=null){moon.enabled=true;moon.intensity=.9f;}if(rim!=null){rim.enabled=true;rim.intensity=.24f;}Ambient();WriteFog(stageFog);Apply(map,camera);}
        // An entrance or the draft takes the screen: it keeps the fog it found for EndIntro (all of it, the density included).
        private static void EnterStage(){if(introActive)return;introActive=true;stageFog=ReadFog();}
        private static MapFogState ReadFog(){var c=RenderSettings.fogColor;return new MapFogState{On=RenderSettings.fog,Mode=(MapFogMode)(int)RenderSettings.fogMode,WritesDensity=true,Density=RenderSettings.fogDensity,Color=new MapLookColor{r=c.r,g=c.g,b=c.b,a=c.a},Start=RenderSettings.fogStartDistance,End=RenderSettings.fogEndDistance};}
        private static void WriteFog(in MapFogState f){RenderSettings.fog=f.On;RenderSettings.fogMode=(FogMode)(int)f.Mode;if(f.WritesDensity)RenderSettings.fogDensity=f.Density;RenderSettings.fogColor=C(f.Color);RenderSettings.fogStartDistance=f.Start;RenderSettings.fogEndDistance=f.End;}
        // The stages' shadows (NativeMapLooks.StageCascades and StageShadowDistance: every old map's own), written only on a change.
        private static void StageShadows(){if(QualitySettings.shadowCascades!=NativeMapLooks.StageCascades)QualitySettings.shadowCascades=NativeMapLooks.StageCascades;if(QualitySettings.shadowDistance!=NativeMapLooks.StageShadowDistance)QualitySettings.shadowDistance=NativeMapLooks.StageShadowDistance;}
        private static Color C(MapLookColor c)=>new Color(c.r,c.g,c.b,c.a);
        // The chip-draft stage: the cool key and the rim in the stage tint (draft-stage.ts), the rim briefly brighter while a
        // chip planted into the wrist pulses (`flash` 0..1). It owns the fog (NativeMapLooks.DraftFog: on, linear, into its own
        // backdrop from 30 to 100 m) and the shadows (2 cascades over 65 m, a soft key at full strength), so a map's look
        // (Brickfall's linear fog, 4 cascades, .85 key) never reaches it; on every old map those are the values it had, but for
        // the fog colour, which was the map's and now is the draft's backdrop (nothing the draft draws lies beyond the 30 m start).
        public static void Draft(Camera camera,Color tint,float flash=0)
        {EnterStage();foreach(var light in stageLights)if(light!=null)light.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=C(NativeMapLooks.DraftBackground);RenderSettings.skybox=null;WriteFog(NativeMapLooks.DraftFog);StageShadows();Ambient();if(moon!=null){moon.enabled=true;moon.color=new Color(.8f,.88f,1);moon.intensity=1.4f;moon.transform.rotation=Quaternion.Euler(35,-145,0);moon.shadows=LightShadowsOf(NativeMapLooks.StageKeyShadows);moon.shadowStrength=NativeMapLooks.StageShadowStrength;}if(rim!=null){rim.enabled=true;rim.color=tint;rim.intensity=1.2f+1.6f*Mathf.Clamp01(flash);rim.transform.rotation=Quaternion.Euler(15,35,0);}}
        private static void Ambient(){RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=NativeArena.ColorOf(0x92b4f0)*.48f;RenderSettings.ambientEquatorColor=NativeArena.ColorOf(0x9aaccd)*.27f;RenderSettings.ambientGroundColor=NativeArena.ColorOf(0x171320)*.48f;}
        // An entrance stage from the scene bank (NativeSceneMotion): three's linear backdrop, fog and lights. Colours go to sRGB
        // (Unity linearises them in Linear space); directional lights become Unity lights through NativeEntranceLight, so the
        // light Unity applies is the source's diffuse light whatever the colour space and light intensity mode. The stage owns
        // the fog (on and linear, the clip's colour and range: NativeMapLooks.StageFog) and the shadows (2 cascades over 65 m),
        // so a map's look never leaks into it; EndIntro gives the map's back. Point lights
        // go to the source shaders' stage block instead (NativeStageLighting): in the owner's builds Unity's point lights did not
        // light the instanced stage draws, so the Siren cell (lit by point lights alone) stayed black. `siren` (the boss song's beats) lights the dev-siren cell
        // from NativeSirenIntroLight at the song time `age` in place of the bank's 30 Hz samples of its bulb, fill and ambient.
        public static void Intro(NativeSceneClip clip,float age,Camera camera,double[] siren=null)
        {
            EnterStage();if(moon!=null)moon.enabled=false;if(rim!=null)rim.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;int f=Math.Min(clip.frames-1,(int)(Mathf.Clamp01(age/clip.duration)*(clip.frames-1))),e=f*8;var env=clip.environment;camera.backgroundColor=new Color(env[e],env[e+1],env[e+2]).gamma;var fog=new Color(env[e+3],env[e+4],env[e+5]).gamma;WriteFog(NativeMapLooks.StageFog(MapLookColor.Of(fog.r,fog.g,fog.b),env[e+6],env[e+7]));StageShadows();RenderSettings.skybox=null;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear,linearIntensity=GraphicsSettings.lightsUseLinearIntensity;float skyR=0,skyG=0,skyB=0,groundR=0,groundG=0,groundB=0;int count=0,stage=0;
            foreach(var track in clip.lights)
            {
                int j=f*11;var v=track.values;float intensity=v[j+6];
                if(track.kind=="ambient"||track.kind=="hemisphere"){if(siren==null){skyR+=v[j+3]*intensity;skyG+=v[j+4]*intensity;skyB+=v[j+5]*intensity;groundR+=v[j+8]*intensity;groundG+=v[j+9]*intensity;groundB+=v[j+10]*intensity;}continue;}
                if(intensity<=0)continue;
                // Every source intro point light has three's default decay 2 except the Siren's bulb, which NativeSirenIntroLight carries.
                if(track.kind!="directional"){if(siren==null)stage=NativeStageLighting.Add(points,stage,new NativeStageLight(v[j],v[j+1],v[j+2],v[j+3]*intensity,v[j+4]*intensity,v[j+5]*intensity,v[j+7],2));continue;}
                if(count>=16)continue;while(stageLights.Count<=count)stageLights.Add(null);var light=stageLights[count];if(light==null){var go=new GameObject("Entrance light "+count);if(moon!=null)go.transform.SetParent(moon.transform.parent);light=go.AddComponent<Light>();stageLights[count]=light;}count++;
                NativeEntranceLight.Light(v[j+3],v[j+4],v[j+5],intensity,linear,linearIntensity,out double r,out double g,out double b,out double unity);
                light.enabled=true;light.type=LightType.Directional;light.color=new Color((float)r,(float)g,(float)b);light.intensity=(float)unity;light.transform.position=new Vector3(-v[j],v[j+1],v[j+2]);light.transform.LookAt(Vector3.zero);light.shadows=LightShadows.Soft;
            }
            for(int i=count;i<stageLights.Count;i++)if(stageLights[i]!=null)stageLights[i].enabled=false;
            if(siren!=null)
            {
                stage=NativeSirenIntroLight.Lights(age,siren,points,out double level);double a=NativeSirenIntroLight.Ambient(level);
                skyR=groundR=(float)(NativeSirenIntroLight.Linear(NativeSirenIntroLight.AmbientHex,16)*a);skyG=groundG=(float)(NativeSirenIntroLight.Linear(NativeSirenIntroLight.AmbientHex,8)*a);skyB=groundB=(float)(NativeSirenIntroLight.Linear(NativeSirenIntroLight.AmbientHex,0)*a);
            }
            StageBlock(stage);
            Color upper=new Color((float)NativeEntranceLight.Ambient(skyR,linear),(float)NativeEntranceLight.Ambient(skyG,linear),(float)NativeEntranceLight.Ambient(skyB,linear));
            Color lower=new Color((float)NativeEntranceLight.Ambient(groundR,linear),(float)NativeEntranceLight.Ambient(groundG,linear),(float)NativeEntranceLight.Ambient(groundB,linear));
            Color middle=new Color((float)NativeEntranceLight.Ambient((skyR+groundR)*.5,linear),(float)NativeEntranceLight.Ambient((skyG+groundG)*.5,linear),(float)NativeEntranceLight.Ambient((skyB+groundB)*.5,linear));
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=upper;RenderSettings.ambientEquatorColor=middle;RenderSettings.ambientGroundColor=lower;
        }
        public static void Clock(float time){if(sky!=null)sky.SetFloat("_SceneTime",time);if(arenaFlicker)PracticalLights(time);}
        /// <summary>Resources/MapLooks.json, loaded and validated once.</summary>
        public static MapLookSet LookSet()
        {
            if(looks==null){var asset=Resources.Load<TextAsset>("MapLooks");if(asset==null)throw new InvalidOperationException("MapLooks.json is missing");var set=JsonUtility.FromJson<MapLookSet>(asset.text);NativeMapLooks.Validate(set);looks=set;}
            return looks;
        }
        /// <summary>The map's look: its MapLooks.json entry, or today's look worked out from the map (NativeMapLooks.Fallback).</summary>
        public static MapLook Look(ContentMap map)=>NativeMapLooks.Resolve(LookSet(),map.id,map.theme,map.sky,map.accent);
        private static readonly int ArenaRimDirId=Shader.PropertyToID("_ArenaRimDir"),ArenaRimColorId=Shader.PropertyToID("_ArenaRimColor");
        /// <summary>
        /// NativeBlockLit's rim (it has no add pass, and Apply keeps the rim per-pixel on dressed maps, so Unity's second
        /// directional light never reaches it): the rim light's live direction and its colour times intensity as Unity applies
        /// them (NativeEntranceLight.Applied); black while it is off.
        /// </summary>
        public static void ArenaRim()
        {
            if(rim==null||!rim.isActiveAndEnabled||!(rim.intensity>0)){Shader.SetGlobalVector(ArenaRimColorId,Vector4.zero);return;}
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear,linearIntensity=GraphicsSettings.lightsUseLinearIntensity;var c=rim.color;float i=rim.intensity;
            Shader.SetGlobalVector(ArenaRimDirId,-rim.transform.forward);
            Shader.SetGlobalVector(ArenaRimColorId,new Vector4((float)NativeEntranceLight.Applied(c.r,i,linear,linearIntensity),(float)NativeEntranceLight.Applied(c.g,i,linear,linearIntensity),(float)NativeEntranceLight.Applied(c.b,i,linear,linearIntensity),0));
        }
        // The map's look (MapLooks.json), through Core NativeMapLooks.Applied (the one mapping, which Tests/MapLookChecks compares
        // to the ce3c506 oracle and fixture for the 15 old maps bit for bit). Camera, fog, sky, shadow settings, the key's colour
        // and aim and both lights' shadow modes are written every time; the intensities, the ambient and the rim's aim wait while
        // an entrance or the draft stage owns the lights (EndIntro restores them and calls Apply again), and the fog written then
        // is also what EndIntro gives back. On the 15 old maps every value is the one this method and Create wrote before the
        // looks moved into data, except Cinder Crossing's sky (forge theme).
        public static void Apply(ContentMap map,Camera camera)
        {
            var look=Look(map);current=look;var s=applied;NativeMapLooks.Applied(look,map.sky,NativeArenaDressing.Preview,s);
            camera.farClipPlane=s.FarClip;camera.backgroundColor=C(s.Background);camera.clearFlags=s.Skybox?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;
            WriteFog(s.Fog);if(introActive)stageFog=ReadFog();
            Sky(s);RenderSettings.skybox=s.Skybox?sky:null;Shadows();
            if(moon!=null){moon.color=C(s.KeyColor);Aim(moon,s.KeyAim);moon.shadows=LightShadowsOf(s.KeyShadows);moon.shadowStrength=s.KeyStrength;}
            // On a dressed map the rim is always a pixel light: an Auto rim that pixel lights crowd out (3 slots on the low tier
            // against the forced combat, blast and seeker lights) lands in the SH, which NativeBlockLit adds on top of its own rim
            // term. Other maps keep Unity's default (Auto), as before.
            if(rim!=null){rim.color=C(s.RimColor);rim.shadows=LightShadowsOf(s.RimShadows);rim.shadowStrength=s.RimStrength;rim.renderMode=s.RimPixel?LightRenderMode.ForcePixel:LightRenderMode.Auto;}
            if(!introActive)
            {
                RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=C(s.AmbientSky);RenderSettings.ambientEquatorColor=C(s.AmbientEquator);RenderSettings.ambientGroundColor=C(s.AmbientGround);
                if(moon!=null)moon.intensity=s.KeyIntensity;if(rim!=null){rim.intensity=s.RimIntensity;if(s.RimAim.Has)Aim(rim,s.RimAim);else rim.transform.SetPositionAndRotation(rimFrom,rimHome);}
            }
            PracticalLights(look,Time.unscaledTime);
        }
        /// <summary>The graphics tier's shadows (NativeSettings): the low tier takes the look's fallback cascades and distance.</summary>
        public static void Graphics(bool low){lowShadows=low;Shadows();}
        private static void Shadows(){if(current==null)return;QualitySettings.shadowCascades=applied.ShadowCascades(lowShadows);QualitySettings.shadowDistance=applied.Shadows(lowShadows);}
        private static LightShadows LightShadowsOf(string kind)=>kind=="soft"?LightShadows.Soft:kind=="hard"?LightShadows.Hard:LightShadows.None;
        // A light at `from` looking at the origin (today's rig), or 24 m out toward its elevation and bearing, looking back.
        private static void Aim(Light light,MapLookAim a)
        {
            if(a.From){light.transform.position=new Vector3(a.X,a.Y,a.Z);light.transform.LookAt(Vector3.zero);return;}
            var toward=new Vector3(a.X,a.Y,a.Z);light.transform.SetPositionAndRotation(toward*24,Quaternion.LookRotation(-toward));
        }
        // NativeNightSky's properties; a tint of 0 keeps the shader's default (today's night constant), others go to linear.
        private static void Sky(MapLookState s)
        {
            if(sky==null)sky=new Material(Resources.Load<Shader>("NativeNightSky"));sky.SetColor(HorizonId,C(s.Horizon));sky.SetColor(ZenithId,C(s.Zenith));sky.SetFloat(MoonSizeId,s.MoonSize);
            sky.SetVector(SunDirId,new Vector4(s.SunX,s.SunY,s.SunZ,0));sky.SetVector(DiscColorId,Tint(s.Disc));sky.SetVector(DiscShadeId,new Vector4(s.DiscBase,s.Crater,0,0));
            sky.SetVector(HaloColorId,Tint(s.Halo));sky.SetFloat(HaloFalloffId,s.HaloFalloff);sky.SetFloat(StarsId,s.Stars);sky.SetVector(CloudTintId,Tint(s.Cloud));sky.SetFloat(CloudSpeedId,s.CloudSpeed);
        }
        private static Vector4 Tint(MapLookTint t){if(t.Hex==0)return new Vector4(t.R,t.G,t.B,0);var c=Linear(NativeArena.ColorOf(t.Hex));return new Vector4(c.r,c.g,c.b,0);}
        private static Color Linear(Color c)=>QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;
        private static void PracticalLights(MapLook look,float time)
        {
            var lights=look.lights??Array.Empty<MapLookPractical>();arenaCount=Math.Min(lights.Length,NativeMapLooks.MaxLights);arenaFlicker=false;
            for(int i=0;i<arenaPos.Length;i++)
            {
                if(i>=arenaCount){arenaPos[i]=new Vector4(0,-10000,0,1);arenaBase[i]=Vector4.zero;continue;}
                var l=lights[i];var c=Linear(NativeArena.ColorOf(l.color));arenaPos[i]=new Vector4(l.pos.x,l.pos.y,l.pos.z,l.range);arenaBase[i]=new Vector4(c.r,c.g,c.b,0)*l.intensity;arenaFlicker|=NativeMapLooks.Flickers(l);
            }
            PracticalLights(time);
        }
        private static void PracticalLights(float time)
        {
            var lights=current?.lights;for(int i=0;i<arenaColor.Length;i++)arenaColor[i]=i<arenaCount?arenaBase[i]*(float)NativeMapLooks.Flicker(lights[i],time):Vector4.zero;
            Shader.SetGlobalVectorArray(ArenaPosId,arenaPos);Shader.SetGlobalVectorArray(ArenaColorId,arenaColor);Shader.SetGlobalInt(ArenaCountId,arenaCount);
        }
        // arena-lighting.ts palette, adapted to the built-in Unity renderer: the rig and the defaults each map's look (Apply) then sets.
        public static void Create(Transform parent)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=NativeArena.ColorOf(0x92b4f0)*.48f;
            RenderSettings.ambientEquatorColor=NativeArena.ColorOf(0x9aaccd)*.27f;
            RenderSettings.ambientGroundColor=NativeArena.ColorOf(0x171320)*.48f;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=NativeArena.ColorOf(0x060810);RenderSettings.fogStartDistance=30;RenderSettings.fogEndDistance=105;
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=65;
            QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowCascades=2;QualitySettings.pixelLightCount=9;StageBlock(0);
            Light Directional(string name,Vector3 position,int color,float intensity,bool shadows)
            {
                var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=position;go.transform.LookAt(Vector3.zero);
                var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=NativeArena.ColorOf(color);light.intensity=intensity;
                light.shadows=shadows?LightShadows.Soft:LightShadows.None;light.shadowBias=.025f;light.shadowNormalBias=.15f;return light;
            }
            Vector3 Rig(float[] p)=>new Vector3(p[0],p[1],p[2]);
            RenderSettings.sun=moon=Directional("Moon key",Rig(NativeMapLooks.RigKey),0xb2c6ff,.9f,true);
            rim=Directional("Teal rim",Rig(NativeMapLooks.RigRim),0x58aab4,.24f,false);rimFrom=rim.transform.position;rimHome=rim.transform.rotation;
        }
    }
    public sealed partial class NativeArena
    {
        private void DetailProp(NativePropBody prop)
        {
            var block=prop.Definition;if(block.material=="foundation")return;
            Vector3 size=Vec(block.size);bool wood=block.material=="wood",metal=block.material=="metal";
            if(!wood&&!metal)return;
            string key=wood?"wood trim":"metal trim";
            if(!materials.ContainsKey(key))materials[key]=new Material(Resources.Load<Shader>("NativeLit")){color=ColorOf(wood?0xf4bd70:0x74b9c4)};
            void Strip(Vector3 p,Vector3 s)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=key;var col=go.GetComponent<Collider>();col.enabled=false;CapturedAnimation.Release(col);
                go.transform.SetParent(prop.transform,false);go.transform.localPosition=new Vector3(p.x/size.x,p.y/size.y,p.z/size.z);
                go.transform.localScale=new Vector3(s.x/size.x,s.y/size.y,s.z/size.z);go.GetComponent<Renderer>().sharedMaterial=materials[key];
            }
            foreach(float sign in new[]{-1f,1f})
                if(wood)Strip(new Vector3(sign*size.x*.32f,0,size.z*.5f+.008f),new Vector3(.045f,size.y*.92f,.025f));
                else Strip(new Vector3(0,sign*size.y*.32f,size.z*.5f+.008f),new Vector3(size.x*.9f,.06f,.025f));
            if(wood)Strip(new Vector3(0,0,size.z*.5f+.009f),new Vector3(size.x*.93f,.045f,.026f));
        }
    }
}
