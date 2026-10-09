using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    public static class NativeLighting
    {
        private static Material sky;private static Light moon,rim;private static bool introActive;private static readonly List<Light> stageLights=new List<Light>();
        public static void EndIntro(ContentMap map,Camera camera){if(!introActive)return;introActive=false;foreach(var light in stageLights)if(light!=null)light.enabled=false;if(moon!=null){moon.enabled=true;moon.intensity=.9f;}if(rim!=null){rim.enabled=true;rim.intensity=.24f;}Ambient();Apply(map,camera);}
        // The chip-draft table: a cool key from high over the camera's shoulder (the table top and chips read flat-on), the rim
        // from behind the drafter in the stage tint, briefly brighter while a planted chip pulses (`flash` 0..1).
        public static void Draft(Camera camera,Color tint,float flash=0)
        {introActive=true;foreach(var light in stageLights)if(light!=null)light.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.032f,.055f);RenderSettings.skybox=null;RenderSettings.fogStartDistance=30;RenderSettings.fogEndDistance=100;Ambient();if(moon!=null){moon.enabled=true;moon.color=new Color(.8f,.88f,1);moon.intensity=1.35f;moon.transform.rotation=Quaternion.Euler(58,-155,0);}if(rim!=null){rim.enabled=true;rim.color=tint;rim.intensity=1.2f+1.6f*Mathf.Clamp01(flash);rim.transform.rotation=Quaternion.Euler(22,30,0);}}
        private static void Ambient(){RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=NativeArena.ColorOf(0x92b4f0)*.48f;RenderSettings.ambientEquatorColor=NativeArena.ColorOf(0x9aaccd)*.27f;RenderSettings.ambientGroundColor=NativeArena.ColorOf(0x171320)*.48f;}
        // An entrance stage from the scene bank (NativeSceneMotion): three's linear backdrop, fog and lights as Unity authors
        // them. Colours go to sRGB (Unity linearises them in Linear space) and each light through NativeEntranceLight, so the
        // light Unity applies is the source's diffuse light whatever the colour space and light intensity mode.
        public static void Intro(NativeSceneClip clip,float age,Camera camera)
        {
            introActive=true;if(moon!=null)moon.enabled=false;if(rim!=null)rim.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;int f=Math.Min(clip.frames-1,(int)(Mathf.Clamp01(age/clip.duration)*(clip.frames-1))),e=f*8;var env=clip.environment;camera.backgroundColor=new Color(env[e],env[e+1],env[e+2]).gamma;RenderSettings.fogColor=new Color(env[e+3],env[e+4],env[e+5]).gamma;RenderSettings.fogStartDistance=env[e+6];RenderSettings.fogEndDistance=env[e+7];RenderSettings.skybox=null;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear,linearIntensity=GraphicsSettings.lightsUseLinearIntensity;float skyR=0,skyG=0,skyB=0,groundR=0,groundG=0,groundB=0;int count=0;
            foreach(var track in clip.lights)
            {
                int j=f*11;var v=track.values;float intensity=v[j+6];
                if(track.kind=="ambient"||track.kind=="hemisphere"){skyR+=v[j+3]*intensity;skyG+=v[j+4]*intensity;skyB+=v[j+5]*intensity;groundR+=v[j+8]*intensity;groundG+=v[j+9]*intensity;groundB+=v[j+10]*intensity;continue;}
                if(intensity<=0)continue;if(count>=16)break;while(stageLights.Count<=count)stageLights.Add(null);var light=stageLights[count];if(light==null){var go=new GameObject("Entrance light "+count);if(moon!=null)go.transform.SetParent(moon.transform.parent);light=go.AddComponent<Light>();stageLights[count]=light;}count++;
                NativeEntranceLight.Light(v[j+3],v[j+4],v[j+5],intensity,linear,linearIntensity,out double r,out double g,out double b,out double unity);
                light.enabled=true;light.type=track.kind=="directional"?LightType.Directional:LightType.Point;light.color=new Color((float)r,(float)g,(float)b);light.intensity=(float)unity;light.range=v[j+7]>0?v[j+7]:20;light.transform.position=new Vector3(-v[j],v[j+1],v[j+2]);if(light.type==LightType.Directional)light.transform.LookAt(Vector3.zero);light.shadows=light.type==LightType.Directional?LightShadows.Soft:LightShadows.None;
            }
            for(int i=count;i<stageLights.Count;i++)if(stageLights[i]!=null)stageLights[i].enabled=false;
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
            QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowCascades=2;QualitySettings.pixelLightCount=9;
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
