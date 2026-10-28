#ifndef NATIVE_ADVANCED_LIGHT_INCLUDED
#define NATIVE_ADVANCED_LIGHT_INCLUDED
// The advanced lighting (docs/LIGHTING-ADVANCED.md; Core NativeAdvancedLooks, NativeLightVolume; Runtime NativeAdvancedLighting
// writes every global). Shared by ChipArena/NativeBlockLit (blocks, kits, skylines), ChipArena/SourceLit (scenery, pilots,
// machines), ChipArena/ZombieLit and ChipArena/VisualBodyToon. The includer declares _ArenaLightPos[8] and _ArenaLightColor[8]
// first (the practical slots). Every part is off while its switch is 0, and an unset global is 0: each function then returns
// its input unchanged (or adds nothing), so every map without the look's `advanced`, the low graphics tier, a stage, the
// draft and the rooftop draw exactly as before.
//  - ArenaGI: the light volume (one 3D texture, RGBA = bounce light, occlusion): the ambient darkened toward the occlusion
//    (never below the floor) plus the bounce, read at the surface moved out half a cell along its normal; the lightning's
//    flash on whatever sees the sky (the occlusion's open share). Returns the light to add to the includer's own.
//  - ArenaScatter: height fog (exponential above a base height, its optical depth integrated along the view ray) with the key
//    scattered toward the eye (Henyey-Greenstein) and the practical lights' glow in the air (their fall-off integrated along
//    the ray in closed form), applied before Unity's own fog.
//  - ArenaWet*: wet surfaces (NativeBlockLit only): darker albedo, puddles on up-facing faces with rain rings, and a sheen
//    mirroring the sky with the key's and the practical lights' glints.
sampler3D _ArenaGIVolume;
float4 ArenaGITap(float3 uvw){return tex3Dlod(_ArenaGIVolume,float4(uvw,0.0));}
// SHARED BEGIN (plain HLSL that the offline harness also runs as GLSL: float literals keep their decimal point)
//  _ArenaGI:        x the occlusion's strength, y the bounce's gain, z the occlusion floor, w on (1) / off (0)
//  _ArenaGIBox:     xyz the volume's min corner (m), w its cell (m)
//  _ArenaGISize:    xyz the volume's size (m), w a body's share of it (pilots, monsters)
//  _ArenaLightning: rgb the flash's light now (linear), w on
float4 _ArenaGI;float4 _ArenaGIBox;float4 _ArenaGISize;float4 _ArenaLightning;
//  _ArenaScatter:      rgb the height fog's colour (linear), w its greatest opacity (0 off)
//  _ArenaScatterFog:   x the density (1/m) at the base, y the base height (m), z the falloff height (m), w the lights' share
//  _ArenaScatterKey:   rgb the key's colour (linear, times its share), w the phase's g
//  _ArenaScatterDir:   xyz toward the key
float4 _ArenaScatter;float4 _ArenaScatterFog;float4 _ArenaScatterKey;float4 _ArenaScatterDir;
//  _ArenaWet:     x wetness (0 dry), y puddles (0..1), z the rain's clock (s), w the rings' strength (rain falling)
//  _ArenaWetSky:  rgb the sky a wet face mirrors (linear), w the sheen's strength
float4 _ArenaWet;float4 _ArenaWetSky;
// NativeLightVolume.Offset: a face reads the volume half a cell out along its normal; the box's last cell fades the volume out.
#define GI_OFFSET 0.5
#define GI_EDGE 1.5
// The lightning reaches what sees the sky: the occlusion's share from GI_SKY_LO (under a roof) to GI_SKY_HI (open).
#define GI_SKY_LO 0.55
#define GI_SKY_HI 0.92
// The volume at a surface (rgb bounce, a occlusion), the open value (0, 0, 0, 1) outside its box or while it is off.
float4 ArenaGIAt(float3 world,float3 n)
{
    if(_ArenaGI.w<0.5)return float4(0.0,0.0,0.0,1.0);
    float3 p=world+n*(_ArenaGIBox.w*GI_OFFSET);float3 uvw=(p-_ArenaGIBox.xyz)/_ArenaGISize.xyz;
    float3 e=min(uvw,1.0-uvw)*_ArenaGISize.xyz/max(_ArenaGIBox.w*GI_EDGE,0.001);float fade=saturate(min(e.x,min(e.y,e.z)));
    if(fade<=0.0)return float4(0.0,0.0,0.0,1.0);
    float4 g=ArenaGITap(saturate(uvw));
    return lerp(float4(0.0,0.0,0.0,1.0),float4(max(g.rgb,float3(0.0,0.0,0.0)),saturate(g.a)),fade);
}
// The light to add for an ambient `amb` (the includer's own ambient term at this point), `share` 1 for the world, the body
// share for characters: the ambient times (occlusion - 1), the bounce, and the lightning on what sees the sky.
float3 ArenaGIShare(float3 amb,float3 world,float3 n,float share)
{
    if(_ArenaGI.w<0.5&&_ArenaLightning.w<0.5)return float3(0.0,0.0,0.0);
    float4 g=ArenaGIAt(world,n);
    float occ=lerp(1.0,max(g.a,_ArenaGI.z),_ArenaGI.x*share);
    float sky=smoothstep(GI_SKY_LO,GI_SKY_HI,g.a)*(0.6+0.4*saturate(n.y+0.5));
    return amb*(occ-1.0)+g.rgb*(_ArenaGI.y*share)+_ArenaLightning.rgb*(sky*_ArenaLightning.w);
}
float3 ArenaGI(float3 amb,float3 world,float3 n){return ArenaGIShare(amb,world,n,1.0);}
float3 ArenaGIActor(float3 amb,float3 world,float3 n){return ArenaGIShare(amb,world,n,_ArenaGISize.w);}
// Henyey-Greenstein, normalised so g = 0 gives 1 (isotropic).
float ScatterPhase(float c,float g){float g2=g*g;float d=max(1.0+g2-2.0*g*c,0.0001);return (1.0-g2)/(d*sqrt(d));}
// A practical light's fall-off (1 - r^2/R^2)^2 integrated along the ray from u0 to u1 (u measured from the ray's closest point
// to the light, h its distance there), divided by the range: (c^2 u - 2c u^3/3R^2 + u^5/5R^4)/R with c = 1 - h^2/R^2.
float ScatterChord(float u,float c,float r2){float u2=u*u;return u*(c*c-2.0*c*u2/(3.0*r2)+u2*u2/(5.0*r2*r2));}
float3 ScatterLights(float3 eye,float3 v,float D,float density)
{
    float3 sum=float3(0.0,0.0,0.0);
    for(int k=0;k<8;k++)
    {
        float R=_ArenaLightPos[k].w;if(_ArenaLightPos[k].y<-5000.0||R<=0.0)continue;
        float3 w=_ArenaLightPos[k].xyz-eye;float t0=dot(w,v);float h2=max(dot(w,w)-t0*t0,0.0);float r2=R*R;if(h2>=r2)continue;
        float hw=sqrt(r2-h2);float a=clamp(t0-hw,0.0,D)-t0;float b=clamp(t0+hw,0.0,D)-t0;if(b<=a)continue;
        float c=1.0-h2/r2;sum+=_ArenaLightColor[k].rgb*((ScatterChord(b,c,r2)-ScatterChord(a,c,r2))/R);
    }
    return sum*density;
}
// The height fog's opacity between `eye` and `world` (the mean of exp(-(y - base)/H) along the ray is the density's own
// integral, flat below the base), capped at its greatest; `dense` the density as dense as at the eye or the point.
float ScatterOpacity(float3 world,float3 eye,float D,out float dense)
{
    float H=max(_ArenaScatterFog.z,0.1);float ea=max(eye.y-_ArenaScatterFog.y,0.0)/H;float eb=max(world.y-_ArenaScatterFog.y,0.0)/H;
    float k=abs(eb-ea)>0.001?(exp(-ea)-exp(-eb))/(eb-ea):exp(-ea);dense=_ArenaScatterFog.x*exp(-min(ea,eb));
    return min(1.0-exp(-_ArenaScatterFog.x*k*D),_ArenaScatter.w);
}
// The height fog and its light over `col` at `world` seen from `eye` (Unity's fog follows).
float3 ArenaScatter(float3 col,float3 world,float3 eye)
{
    if(_ArenaScatter.w<=0.0)return col;
    float3 d=world-eye;float D=length(d);if(D<0.01)return col;float3 v=d/D;float dense=0.0;float fogA=ScatterOpacity(world,eye,D,dense);
    float3 key=_ArenaScatterKey.rgb*ScatterPhase(dot(v,_ArenaScatterDir.xyz),_ArenaScatterKey.w);
    return lerp(col,_ArenaScatter.rgb+key,fogA)+ScatterLights(eye,v,D,dense*_ArenaScatterFog.w);
}
// An add pass's share: only the fade (the fog's own light is added once, in the base pass).
float3 ArenaScatterFade(float3 col,float3 world,float3 eye)
{
    if(_ArenaScatter.w<=0.0)return col;float D=length(world-eye);if(D<0.01)return col;float dense=0.0;return col*(1.0-ScatterOpacity(world,eye,D,dense));
}
// ---- wet surfaces (NativeBlockLit) ----
float WetHash(float2 p){float3 q=frac(float3(p.x,p.y,p.x)*0.1031);q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);}
float WetNoise(float2 p){float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);
    return lerp(lerp(WetHash(i),WetHash(i+float2(1.0,0.0)),u.x),lerp(WetHash(i+float2(0.0,1.0)),WetHash(i+float2(1.0,1.0)),u.x),u.y);}
// The puddle share of an up-facing face (0 elsewhere): broad soft patches over the low spots of the noise.
float ArenaPuddle(float3 world,float3 n)
{
    if(_ArenaWet.x<=0.0||n.y<0.7)return 0.0;
    float m=WetNoise(world.xz*0.23)*0.65+WetNoise(world.xz*0.71+float2(5.2,1.3))*0.35;
    return smoothstep(0.62-0.3*_ArenaWet.y,0.7-0.3*_ArenaWet.y,m)*_ArenaWet.y*smoothstep(0.7,0.95,n.y);
}
// Water fills the pores: darker, most in a puddle.
float3 ArenaWetAlbedo(float3 albedo,float puddle,float3 n)
{
    float wet=_ArenaWet.x*lerp(0.55,1.0,saturate(n.y));return albedo*(1.0-0.38*wet-0.25*puddle);
}
// A puddle's rain rings: each 0.6 m cell rings out from its own centre at its own phase; the normal leans along the ring.
float3 ArenaRipple(float3 world,float puddle)
{
    if(_ArenaWet.w<=0.0||puddle<=0.01)return float3(0.0,0.0,0.0);
    float2 p=world.xz/0.6;float2 c=floor(p);float2 off=float2(WetHash(c),WetHash(c+float2(17.0,3.0)))*0.5+0.25;float2 d=p-c-off;float r=length(d);
    float age=frac(_ArenaWet.z*1.3+WetHash(c+float2(7.0,11.0)));float ring=r-age*0.45;
    float k=exp(-ring*ring*900.0)*(1.0-age)*_ArenaWet.w*puddle;float2 lean=r>0.0001?d/r:float2(0.0,0.0);
    return float3(lean.x,0.0,lean.y)*k;
}
// The sheen: the sky mirrored at the Fresnel share, the key's and the practical lights' glints, all times the wet share.
float3 ArenaWetSheen(float3 world,float3 n,float3 v,float puddle,float3 keyDir,float3 key,float atten)
{
    if(_ArenaWet.x<=0.0)return float3(0.0,0.0,0.0);
    float3 m=normalize(n+ArenaRipple(world,puddle));float up=saturate(m.y);float wet=_ArenaWet.x*(0.25+0.75*up)*(0.45+0.55*puddle);
    float nv=saturate(dot(m,v));float f=0.04+0.96*pow(1.0-nv,5.0);
    float gloss=lerp(60.0,260.0,puddle);
    float3 glint=key*(pow(saturate(dot(m,normalize(keyDir+v))),gloss)*atten*2.0);
    for(int k=0;k<8;k++)
    {
        float3 dl=_ArenaLightPos[k].xyz-world;float len=max(length(dl),0.0001);float a=saturate(1.0-len/max(_ArenaLightPos[k].w*1.6,0.0001));if(a<=0.0)continue;
        float3 hv=normalize(dl/len+v);glint+=_ArenaLightColor[k].rgb*(pow(saturate(dot(m,hv)),gloss*0.5)*a*a*1.5);
    }
    return (_ArenaWetSky.rgb*f+glint*(0.3+0.7*f))*wet*_ArenaWetSky.w;
}
// SHARED END
// The surface shaders' hooks (SourceLit, ZombieLit, VisualBodyToon). ArenaGIBody: the volume on a lit part as emission
// (albedo times it): the lighting model's ambient (Unity's SH, or the style's flattened SH when NativeToon.cginc was included
// first) times (occlusion - 1) at the body's share, plus the bounce and the lightning. Nothing while both are off.
float3 ArenaGIBody(float3 world,float3 n)
{
    if(_ArenaGI.w<0.5&&_ArenaLightning.w<0.5)return float3(0.0,0.0,0.0);
    float3 sh=ShadeSH9(float4(n,1.0));
    #ifdef NATIVE_TOON_INCLUDED
    if(_ArenaToon.x>0.5)sh=ToonAmbient(sh,ShadeSH9(float4(0.0,1.0,0.0,1.0)),ShadeSH9(float4(0.0,-1.0,0.0,1.0)),n.y);
    #endif
    return ArenaGIActor(sh,world,n);
}
// A surface shader's finalcolor: the base pass takes the whole scattering, an add pass its fade only.
void ArenaScatterFinal(float3 world,inout fixed4 color)
{
    #ifdef UNITY_PASS_FORWARDADD
    color.rgb=ArenaScatterFade(color.rgb,world,_WorldSpaceCameraPos);
    #else
    color.rgb=ArenaScatter(color.rgb,world,_WorldSpaceCameraPos);
    #endif
}
#endif
