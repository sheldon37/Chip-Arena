Shader "ChipArena/NativeNightSky" {
// The clouded sky dome (night-world.ts). Every property's default is the constant this shader hard-coded before it had
// properties, so a fresh material draws today's night; Resources/MapLooks.json picks the values per map (NativeLighting.Sky,
// Core/NativeMapLooks.cs). Vectors are linear and not converted; the dusk variant is the same code with other values.
Properties { _Horizon("Horizon",Color)=(.04,.06,.12,1) _Zenith("Zenith",Color)=(.003,.005,.016,1) _MoonSize("Disc size",Float)=.028
 _SunDir("Sun or moon direction",Vector)=(-.4,.46,-.78,0) _DiscColor("Disc colour (linear)",Vector)=(.79,.86,1,0) _DiscShade("Disc base, crater",Vector)=(.76,.24,0,0)
 _HaloColor("Halo colour (linear)",Vector)=(.16,.21,.3,0) _HaloFalloff("Halo falloff",Float)=12 _Stars("Star amount",Float)=1
 _CloudTint("Lit cloud tint (linear)",Vector)=(.24,.29,.40,0) _CloudSpeed("Cloud drift speed",Float)=1
 _SkyToon("Anime sky: on, cloud cover, cloud scale, horizon band height",Vector)=(0,0,0,0) _SkyToonLit("Anime cloud light (linear)",Vector)=(0,0,0,0)
 _SkyToonShade("Anime cloud shade (linear)",Vector)=(0,0,0,0) _SkyToonBand("Anime horizon band (linear)",Vector)=(0,0,0,0)
 _SkyToonFog("Anime sky: fog colour (linear) and how far the horizon blends into it",Vector)=(0,0,0,0) }
SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct appdata { float4 vertex:POSITION; };struct v2f { float4 pos:SV_POSITION;float3 dir:TEXCOORD0; };
float4 _Horizon,_Zenith,_SunDir,_DiscColor,_DiscShade,_HaloColor,_CloudTint;float _SceneTime;float _MoonSize,_HaloFalloff,_Stars,_CloudSpeed;
// The anime map style's sky (Core NativeMapStyle; MapLooks.json toon.skyToon): _SkyToon x on (0 draws the sky above unchanged),
// y the cloud cover, z the cloud scale, w the horizon band's height (of the sky's up); colours linear.
float4 _SkyToon,_SkyToonLit,_SkyToonShade,_SkyToonBand,_SkyToonFog;
v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);float z0=lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y);float z1=lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y);return lerp(z0,z1,f.z);}
float fbm(float3 p){return noise(p)*.55+noise(p*2.03)*.27+noise(p*4.07)*.13+noise(p*8.1)*.05;}
// The anime sky: the zenith eased over the horizon colour with a bright band at the horizon (brighter on the sun's side),
// the sun or moon and its halo as above, and cumulus in three layers (far, low and hazy to near, high and large): cells round
// the horizon (_SkyToon.y the share that hold a cloud, .z their size), each cloud a few round puffs cut flat at its base, a
// crisp silhouette one pixel soft, two tones turned by the puff's own round normal to the sun (lit, shade), a soft gradient
// up from a darker base and a warm rim on the sun's side.
// The azimuth of v (x, z), an atan2 without the intrinsic (the same text runs as GLSL).
float SkyAz(float2 v){float ax=abs(v.x),ay=abs(v.y);float a=min(ax,ay)/max(max(ax,ay),0.000001);float s=a*a;float r=((-0.0464964749*s+0.15931422)*s-0.327622764)*s*a+a;if(ay>ax)r=1.5707963-r;if(v.x<0)r=3.1415927-r;if(v.y<0)r=-r;return r;}
float SkyWrap(float a){return a-6.2831853*floor(a/6.2831853+0.5);}
void SkyPuff(float2 p,float2 c,float r,inout float sd,inout float3 n)
{
float2 q=(p-c)/r;float d=length(p-c)-r;if(d<sd){sd=d;float l=min(length(q),1);n=float3(q*min(1,1/max(length(q),0.0001))*l,sqrt(max(0,1-l*l)));}
}
float4 SkyCumulus(float az,float el,float px,float cells,float base,float size,float seed,float cover,float sunAz,float sunEl)
{
float4 acc=float4(0,0,0,0);float cell=6.2831853/cells;float c0=floor(az/cell);
for(float k=-1;k<=1;k++)
{
float ci=c0+k;float cw=ci-cells*floor(ci/cells);
float h1=hash(float3(cw,seed,1)),h2=hash(float3(cw,seed,2)),h3=hash(float3(cw,seed,3)),h4=hash(float3(cw,seed,4)),h5=hash(float3(cw,seed,5));
if(h1>cover)continue;
float cx=(ci+0.5+(h2-0.5)*0.5)*cell;float w=cell*(0.3+0.2*h3)*size;float b=base*size+(h4-0.5)*0.3*w;
float sd=1000;float3 n=float3(0,0,1);float2 p=float2(az,el);
float r0=w*(0.3+0.08*h5),r1=w*(0.5+0.1*h4),r2=w*(0.44+0.1*h2),r3=w*(0.3+0.1*h3);
SkyPuff(p,float2(cx-0.62*w,b+0.45*r0),r0,sd,n);SkyPuff(p,float2(cx-0.2*w,b+0.55*r1),r1,sd,n);
SkyPuff(p,float2(cx+0.24*w,b+0.5*r2),r2,sd,n);SkyPuff(p,float2(cx+0.64*w,b+0.4*r3),r3,sd,n);
float cut=max(sd,b-el);float a=1-smoothstep(-px,px,cut);if(a<=0)continue;
float sx=clamp(SkyWrap(sunAz-cx)*1.6,-1,1);float3 L=normalize(float3(sx,0.45+0.55*clamp(sunEl*3,0,1),0.55));
float t=smoothstep(0.08,0.2,dot(n,L));float3 tone=lerp(_SkyToonShade.rgb,_SkyToonLit.rgb,t);
tone*=lerp(0.86,1.06,smoothstep(0,0.6*w,el-b));
float rimw=2.5*px+0.035*w;float side=smoothstep(0.15,0.6,dot(normalize(n.xy+float2(0.0001,0)),normalize(L.xy)));
tone=lerp(tone,_SkyToonLit.rgb*1.2+_HaloColor.rgb*0.5,(1-smoothstep(0,rimw,-cut))*side);
acc.rgb+=(1-acc.a)*tone*a;acc.a+=(1-acc.a)*a;
}
return acc;
}
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
float az=SkyAz(float2(d.x,d.z))+_SceneTime*_CloudSpeed*.0004;float el=asin(clamp(d.y,-1,1));float px=max(length(fwidth(d)),.0004);
float sunAz=SkyAz(float2(md.x,md.z)),sunEl=asin(clamp(md.y,-1,1));float cover=_SkyToon.y,size=_SkyToon.z;
float4 c=SkyCumulus(az,el,px,9,.1,size,1,cover*1.1,sunAz,sunEl);
float4 m=SkyCumulus(az+.9,el,px,15,.055,size*.62,2,cover,sunAz,sunEl);m.rgb=lerp(m.rgb,_SkyToonBand.rgb*m.a,.2);c.rgb+=(1-c.a)*m.rgb;c.a+=(1-c.a)*m.a;
float4 f=SkyCumulus(az+2.1,el,px,26,.025,size*.36,3,cover*.9,sunAz,sunEl);f.rgb=lerp(f.rgb,_SkyToonBand.rgb*f.a,.45);c.rgb+=(1-c.a)*f.rgb;c.a+=(1-c.a)*f.a;
// The horizon into the fog colour (_SkyToonFog.w; 0 on the stage 1 looks), over twice the band's height and below it.
return lerp(col*(1-c.a)+c.rgb,_SkyToonFog.rgb,_SkyToonFog.w*(1-smoothstep(0,2*_SkyToon.w,up)));
}
float4 frag(v2f i):SV_Target {float3 d=normalize(i.dir);if(_SkyToon.x>0.5)return float4(AnimeSky(d),1);float up=max(0,d.y);float3 col=lerp(_Horizon.rgb,_Zenith.rgb,smoothstep(0,.85,up));col=lerp(col,_Horizon.rgb*.45,smoothstep(0,.45,-d.y));float3 md=normalize(_SunDir.xyz);float distance=length(d-md),halo=exp(-distance*_HaloFalloff);col+=_HaloColor.rgb*halo;float disc=1-smoothstep(_MoonSize*.96,_MoonSize,distance);col=lerp(col,_DiscColor.rgb*(_DiscShade.x+_DiscShade.y*fbm(d*170)),disc);float3 stars=d*230,cell=floor(stars);float star=step(.996,hash(cell))*pow(max(0,1-length(frac(stars)-.5)*2),8)*smoothstep(.08,.3,d.y)*_Stars;col+=float3(.62,.72,.9)*star*(.8+.2*sin(_SceneTime*.4+hash(cell)*30));float drift=_SceneTime*_CloudSpeed;float3 cloud=float3(d.x/(abs(d.y)+.28)*2+drift*.008,.35,d.z/(abs(d.y)+.28)*2+drift*.004);float density=smoothstep(.40,.72,fbm(cloud))*smoothstep(-.04,.22,d.y);col=lerp(col,lerp(_Horizon.rgb*.85,_CloudTint.rgb,halo),density*.76);return float4(col,1);}
ENDCG } } Fallback Off }
