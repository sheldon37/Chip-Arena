Shader "ChipArena/NativeNightSky" {
Properties { _Horizon("Horizon",Color)=(.04,.06,.12,1) _Zenith("Zenith",Color)=(.003,.005,.016,1) _MoonSize("Moon size",Float)=.028 }
SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct appdata { float4 vertex:POSITION; };struct v2f { float4 pos:SV_POSITION;float3 dir:TEXCOORD0; };
float4 _Horizon,_Zenith;float _SceneTime;float _MoonSize;
v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);float z0=lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y);float z1=lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y);return lerp(z0,z1,f.z);}
float fbm(float3 p){return noise(p)*.55+noise(p*2.03)*.27+noise(p*4.07)*.13+noise(p*8.1)*.05;}
float4 frag(v2f i):SV_Target {float3 d=normalize(i.dir);float up=max(0,d.y);float3 col=lerp(_Horizon.rgb,_Zenith.rgb,smoothstep(0,.85,up));col=lerp(col,_Horizon.rgb*.45,smoothstep(0,.45,-d.y));float3 md=normalize(float3(-.4,.46,-.78));float distance=length(d-md),halo=exp(-distance*12);col+=float3(.16,.21,.3)*halo;float disc=1-smoothstep(_MoonSize*.96,_MoonSize,distance);col=lerp(col,float3(.79,.86,1)*(.76+.24*fbm(d*170)),disc);float3 stars=d*230,cell=floor(stars);float star=step(.996,hash(cell))*pow(max(0,1-length(frac(stars)-.5)*2),8)*smoothstep(.08,.3,d.y);col+=float3(.62,.72,.9)*star*(.8+.2*sin(_SceneTime*.4+hash(cell)*30));float3 cloud=float3(d.x/(abs(d.y)+.28)*2+_SceneTime*.008,.35,d.z/(abs(d.y)+.28)*2+_SceneTime*.004);float density=smoothstep(.40,.72,fbm(cloud))*smoothstep(-.04,.22,d.y);col=lerp(col,lerp(_Horizon.rgb*.85,float3(.24,.29,.40),halo),density*.76);return float4(col,1);}
ENDCG } } Fallback Off }
