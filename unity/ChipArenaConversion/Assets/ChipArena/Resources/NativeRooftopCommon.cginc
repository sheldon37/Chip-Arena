// Shared by the rooftop scene's shaders (NativeRooftop, NativeRooftopSky, NativeRooftopScreen, NativeRooftopSprite): the
// home screen's cyber-city night sky, its haze and the city fog. Globals set each frame by NativeRooftopPresentation: the
// scene clock (_RoofTime, wrapped by the presentation), the sign and cyan spill lights (Unity frame).
#ifndef NATIVE_ROOFTOP_INCLUDED
#define NATIVE_ROOFTOP_INCLUDED
float _RoofTime;float4 _RoofSignPos,_RoofSignColor,_RoofCyanPos;
#define ROOF_STREET (-140.0)
float RoofHash1(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
float RoofHash2(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float RoofNoise(float3 p)
{
    float3 i=floor(p),f=frac(p);f=f*f*(3.0-2.0*f);
    float a=lerp(lerp(RoofHash1(i),RoofHash1(i+float3(1,0,0)),f.x),lerp(RoofHash1(i+float3(0,1,0)),RoofHash1(i+float3(1,1,0)),f.x),f.y);
    float b=lerp(lerp(RoofHash1(i+float3(0,0,1)),RoofHash1(i+float3(1,0,1)),f.x),lerp(RoofHash1(i+float3(0,1,1)),RoofHash1(i+float3(1,1,1)),f.x),f.y);
    return lerp(a,b,f.z);
}
float RoofFbm(float3 p){return RoofNoise(p)*.5+RoofNoise(p*2.03)*.25+RoofNoise(p*4.07)*.125+RoofNoise(p*8.1)*.0625;}
// Navy above; a light-polluted horizon swinging between magenta and cyan around the city (Unity frame: the azimuth's x is
// the mirror of the source's, so the swing is mirrored with it).
float3 RoofSkyBase(float3 d)
{
    float h=d.y;float3 zen=float3(.002,.003,.012),mid=float3(.010,.010,.034),hor=float3(.085,.018,.080);
    float az=atan2(-d.x,-d.z);float3 horC=lerp(hor,float3(.012,.060,.085),.5+.5*sin(az*1.3+.8));
    float3 c=lerp(horC,mid,smoothstep(-.02,.16,h));c=lerp(c,zen,smoothstep(.12,.7,h));
    if(h<0.0)c=lerp(horC,float3(.08,.02,.08),smoothstep(0.0,.3,-h));
    return c;
}
float3 RoofHaze(float3 d,float y)
{
    float3 c=RoofSkyBase(normalize(float3(d.x,max(d.y,0.0)*.3+.02,d.z)));float low=exp(-max(0.0,y-(ROOF_STREET+20.0))/55.0);
    return lerp(c*.8,float3(.09,.02,.08),low*.55);
}
// Distance haze into the night plus the glowing street-level fog.
float3 RoofFog(float3 c,float3 w)
{
    float3 v=w-_WorldSpaceCameraPos;float z=length(v);float3 d=v/max(z,1e-4);float aerial=1.0-exp(-z*0.0013);
    float low=exp(-max(0.0,w.y-ROOF_STREET)/40.0)*(1.0-exp(-z/90.0));
    return lerp(c,RoofHaze(d,w.y),saturate(min(.96,aerial+low*.55)));
}
#endif
