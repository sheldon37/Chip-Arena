Shader "ChipArena/NativeNightSky" {
// The clouded sky dome (night-world.ts). Every property's default is the constant this shader hard-coded before it had
// properties, so a fresh material draws today's night; Resources/MapLooks.json picks the values per map (NativeLighting.Sky,
// Core/NativeMapLooks.cs). Vectors are linear and not converted; the dusk variant is the same code with other values.
Properties { _Horizon("Horizon",Color)=(.04,.06,.12,1) _Zenith("Zenith",Color)=(.003,.005,.016,1) _MoonSize("Disc size",Float)=.028
 _SunDir("Sun or moon direction",Vector)=(-.4,.46,-.78,0) _DiscColor("Disc colour (linear)",Vector)=(.79,.86,1,0) _DiscShade("Disc base, crater",Vector)=(.76,.24,0,0)
 _HaloColor("Halo colour (linear)",Vector)=(.16,.21,.3,0) _HaloFalloff("Halo falloff",Float)=12 _Stars("Star amount",Float)=1
 _CloudTint("Lit cloud tint (linear)",Vector)=(.24,.29,.40,0) _CloudSpeed("Cloud drift speed",Float)=1
 _SkyToon("Anime sky: on, cloud cover, cloud scale, horizon band height",Vector)=(0,0,0,0) _SkyToonLit("Anime cloud light (linear)",Vector)=(0,0,0,0)
 _SkyToonShade("Anime cloud shade (linear)",Vector)=(0,0,0,0) _SkyToonBand("Anime horizon band (linear)",Vector)=(0,0,0,0) }
SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct appdata { float4 vertex:POSITION; };struct v2f { float4 pos:SV_POSITION;float3 dir:TEXCOORD0; };
float4 _Horizon,_Zenith,_SunDir,_DiscColor,_DiscShade,_HaloColor,_CloudTint;float _SceneTime;float _MoonSize,_HaloFalloff,_Stars,_CloudSpeed;
// The anime map style's sky (Core NativeMapStyle; MapLooks.json toon.skyToon): _SkyToon x on (0 draws the sky above unchanged),
// y the cloud cover, z the cloud scale, w the horizon band's height (of the sky's up); colours linear.
float4 _SkyToon,_SkyToonLit,_SkyToonShade,_SkyToonBand;
v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);float z0=lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y);float z1=lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y);return lerp(z0,z1,f.z);}
float fbm(float3 p){return noise(p)*.55+noise(p*2.03)*.27+noise(p*4.07)*.13+noise(p*8.1)*.05;}
// The anime sky: the zenith eased over the horizon colour with a bright band at the horizon (brighter on the sun's side),
// the sun or moon and its halo as above, and painted clouds: a soft-edged body over a warped fbm field on the cloud plane,
// two flat tones (shade, lit where the field thins toward the sun) and a thin lit rim, kept off the horizon and the zenith.
float3 AnimeSky(float3 d)
{
float3 md=normalize(_SunDir.xyz);float up=max(0,d.y);
float3 col=lerp(_Horizon.rgb,_Zenith.rgb,pow(smoothstep(0,.9,up),.7));
float2 hs=normalize(float2(md.x,md.z)+float2(.0001,0));float side=.5+.5*dot(normalize(float2(d.x,d.z)+float2(.0001,0)),hs);
col=lerp(col,_SkyToonBand.rgb,(1-smoothstep(0,_SkyToon.w,up))*(.55+.45*side));
col=lerp(col,_Horizon.rgb*.5,smoothstep(0,.35,-d.y));
float dist=length(d-md),halo=exp(-dist*_HaloFalloff);col+=_HaloColor.rgb*halo;
float disc=1-smoothstep(_MoonSize*.96,_MoonSize,dist);col=lerp(col,_DiscColor.rgb,disc);
float3 stars=d*230,cell=floor(stars);col+=float3(.62,.72,.9)*step(.997,hash(cell))*pow(max(0,1-length(frac(stars)-.5)*2),8)*smoothstep(.15,.4,d.y)*_Stars;
float drift=_SceneTime*_CloudSpeed;float2 cp=float2(d.x,d.z)/(abs(d.y)+.22)*_SkyToon.z+float2(drift*.006,drift*.003);
float2 w=float2(fbm(float3(cp*1.3,1.7)),fbm(float3(cp*1.3+5.2,3.1)));float f=fbm(float3(cp+w*.6,.35));
float th=lerp(.74,.42,_SkyToon.y);float body=smoothstep(th,th+.035,f)*smoothstep(0,.12,d.y)*(1-smoothstep(.6,.98,d.y));
float g=fbm(float3(cp+w*.6+hs*.09,.35));float lit=smoothstep(0,.03,f-g);
float3 cc=lerp(_SkyToonShade.rgb,_SkyToonLit.rgb,lit*.8+.2*halo);
cc+=_SkyToonLit.rgb*smoothstep(th,th+.012,f)*(1-smoothstep(th+.012,th+.06,f))*(.3+.5*side);
return lerp(col,cc,body);
}
float4 frag(v2f i):SV_Target {float3 d=normalize(i.dir);if(_SkyToon.x>0.5)return float4(AnimeSky(d),1);float up=max(0,d.y);float3 col=lerp(_Horizon.rgb,_Zenith.rgb,smoothstep(0,.85,up));col=lerp(col,_Horizon.rgb*.45,smoothstep(0,.45,-d.y));float3 md=normalize(_SunDir.xyz);float distance=length(d-md),halo=exp(-distance*_HaloFalloff);col+=_HaloColor.rgb*halo;float disc=1-smoothstep(_MoonSize*.96,_MoonSize,distance);col=lerp(col,_DiscColor.rgb*(_DiscShade.x+_DiscShade.y*fbm(d*170)),disc);float3 stars=d*230,cell=floor(stars);float star=step(.996,hash(cell))*pow(max(0,1-length(frac(stars)-.5)*2),8)*smoothstep(.08,.3,d.y)*_Stars;col+=float3(.62,.72,.9)*star*(.8+.2*sin(_SceneTime*.4+hash(cell)*30));float drift=_SceneTime*_CloudSpeed;float3 cloud=float3(d.x/(abs(d.y)+.28)*2+drift*.008,.35,d.z/(abs(d.y)+.28)*2+drift*.004);float density=smoothstep(.40,.72,fbm(cloud))*smoothstep(-.04,.22,d.y);col=lerp(col,lerp(_Horizon.rgb*.85,_CloudTint.rgb,halo),density*.76);return float4(col,1);}
ENDCG } } Fallback Off }
