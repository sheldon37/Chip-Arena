using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    public static class NativeLighting
    {
        private static Material sky;private static Light moon,rim;private static bool introActive;private static readonly List<Light> stageLights=new List<Light>();
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
        public static void EndIntro(ContentMap map,Camera camera){if(!introActive)return;introActive=false;foreach(var light in stageLights)if(light!=null)light.enabled=false;if(moon!=null){moon.enabled=true;moon.intensity=.9f;}if(rim!=null){rim.enabled=true;rim.intensity=.24f;}Ambient();Apply(map,camera);}
        // The chip-draft stage: the cool key and the rim in the stage tint (draft-stage.ts), the rim briefly brighter while a
        // chip planted into the wrist pulses (`flash` 0..1).
        public static void Draft(Camera camera,Color tint,float flash=0)
        {introActive=true;foreach(var light in stageLights)if(light!=null)light.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.032f,.055f);RenderSettings.skybox=null;RenderSettings.fogStartDistance=30;RenderSettings.fogEndDistance=100;Ambient();if(moon!=null){moon.enabled=true;moon.color=new Color(.8f,.88f,1);moon.intensity=1.4f;moon.transform.rotation=Quaternion.Euler(35,-145,0);}if(rim!=null){rim.enabled=true;rim.color=tint;rim.intensity=1.2f+1.6f*Mathf.Clamp01(flash);rim.transform.rotation=Quaternion.Euler(15,35,0);}}
        private static void Ambient(){RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=NativeArena.ColorOf(0x92b4f0)*.48f;RenderSettings.ambientEquatorColor=NativeArena.ColorOf(0x9aaccd)*.27f;RenderSettings.ambientGroundColor=NativeArena.ColorOf(0x171320)*.48f;}
        // An entrance stage from the scene bank (NativeSceneMotion): three's linear backdrop, fog and lights. Colours go to sRGB
        // (Unity linearises them in Linear space); directional lights become Unity lights through NativeEntranceLight, so the
        // light Unity applies is the source's diffuse light whatever the colour space and light intensity mode. Point lights
        // go to the source shaders' stage block instead (NativeStageLighting): in the owner's builds Unity's point lights did not
        // light the instanced stage draws, so the Siren cell (lit by point lights alone) stayed black. `siren` (the boss song's beats) lights the dev-siren cell
        // from NativeSirenIntroLight at the song time `age` in place of the bank's 30 Hz samples of its bulb, fill and ambient.
        public static void Intro(NativeSceneClip clip,float age,Camera camera,double[] siren=null)
        {
            introActive=true;if(moon!=null)moon.enabled=false;if(rim!=null)rim.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;int f=Math.Min(clip.frames-1,(int)(Mathf.Clamp01(age/clip.duration)*(clip.frames-1))),e=f*8;var env=clip.environment;camera.backgroundColor=new Color(env[e],env[e+1],env[e+2]).gamma;RenderSettings.fogColor=new Color(env[e+3],env[e+4],env[e+5]).gamma;RenderSettings.fogStartDistance=env[e+6];RenderSettings.fogEndDistance=env[e+7];RenderSettings.skybox=null;
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
        public static void Clock(float time){if(sky!=null)sky.SetFloat("_SceneTime",time);}
        public static void Apply(ContentMap map,Camera camera)
        {
            bool indoor=map.id=="siren",cold=map.theme=="moonlit"||map.theme=="snow",fire=map.id=="forge";camera.farClipPlane=350;camera.backgroundColor=NativeArena.ColorOf(indoor?0x050407:map.sky);camera.clearFlags=indoor?CameraClearFlags.SolidColor:CameraClearFlags.Skybox;
            RenderSettings.fogColor=indoor?NativeArena.ColorOf(0x0d0a12):Color.Lerp(NativeArena.ColorOf(map.sky),NativeArena.ColorOf(0x405677),.42f);RenderSettings.fogStartDistance=indoor?30:65;RenderSettings.fogEndDistance=indoor?118:210;
            if(sky==null)sky=new Material(Resources.Load<Shader>("NativeNightSky"));sky.SetColor("_Horizon",NativeArena.ColorOf(fire?0x633345:cold?0x405677:0x333f64));sky.SetColor("_Zenith",NativeArena.ColorOf(fire?0x100b1c:0x080e22));sky.SetFloat("_MoonSize",map.id=="moonlit"?.065f:.028f);RenderSettings.skybox=indoor?null:sky;
            if(moon!=null){moon.color=NativeArena.ColorOf(map.id=="outpost"?0xffc5a5:fire?0xffac9c:0xb2ceff);moon.transform.position=indoor?new Vector3(-3,30,-5):new Vector3(-8,20,-12);moon.transform.LookAt(Vector3.zero);}if(rim!=null)rim.color=NativeArena.ColorOf(map.accent);
        }
        // arena-lighting.ts palette, adapted to the built-in Unity renderer.
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
            RenderSettings.sun=moon=Directional("Moon key",new Vector3(-8,20,-12),0xb2c6ff,.9f,true);
            rim=Directional("Teal rim",new Vector3(14,7,12),0x58aab4,.24f,false);
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
