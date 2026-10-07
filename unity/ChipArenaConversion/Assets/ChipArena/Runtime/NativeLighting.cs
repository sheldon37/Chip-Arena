using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    public static class NativeLighting
    {
        private static Material sky;private static Light moon,rim;
        public static void Apply(ContentMap map,Camera camera)
        {
            bool indoor=map.id=="siren",cold=map.id=="moonlit"||map.id=="snow",fire=map.id=="forge";camera.farClipPlane=350;camera.backgroundColor=NativeArena.ColorOf(indoor?0x050407:map.sky);camera.clearFlags=indoor?CameraClearFlags.SolidColor:CameraClearFlags.Skybox;
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
