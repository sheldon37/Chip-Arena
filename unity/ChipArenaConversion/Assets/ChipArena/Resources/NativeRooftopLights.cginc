// The rooftop city's light (owner 2026-10-29: "Improve the graphics immensely~ utilize lighting"), shared by its facade and
// prop shaders (NativeRooftopFacade, NativeRooftopProp): the holo screens', signs' and lamps' spill (eight soft point lights
// the presentation writes each frame, Core NativeRooftopCity.Lights), the glow the street throws up the towers, the night
// sky's ambient and the wet sky reflection. Unity frame. Small and branch free: each shader that includes it stays one short
// pass (the large rooftop pass crashed the Windows Editor's shader compiler; DEV_LOG 2026-10-24).
#ifndef NATIVE_ROOFTOP_LIGHTS_INCLUDED
#define NATIVE_ROOFTOP_LIGHTS_INCLUDED
// xyz the light's centre, w its reach (metres); rgb its HDR colour, a how much it wraps round a surface (0 a point, 1 an area).
float4 _RoofLightPos[8],_RoofLightCol[8];
// The spill on a surface at `p` facing `n`: each light falls off as reach^2 / (d^2 + reach^2), wrapped by its area share.
float3 RoofSpill(float3 p,float3 n)
{
    float3 c=float3(0,0,0);
    [unroll] for(int k=0;k<8;k++)
    {
        float3 l=_RoofLightPos[k].xyz-p;float d2=dot(l,l);float r=_RoofLightPos[k].w;float3 ld=l*rsqrt(max(d2,1e-4));
        float wrap=_RoofLightCol[k].a;float lam=saturate((dot(n,ld)+wrap)/(1.0+wrap));
        c+=_RoofLightCol[k].rgb*lam*(r*r/(d2+r*r));
    }
    return c;
}
// The glow the lit street throws up a facade (magenta at the foot, violet higher, gone by the skyline) and the sky's navy
// from above.
float3 RoofCityGlow(float3 p,float3 n)
{
    float h=max(0.0,p.y-ROOF_STREET);float foot=exp(-h/70.0),mid=exp(-h/220.0);
    float3 up=float3(.030,.034,.085)*saturate(n.y*.5+.5);
    float3 glow=(float3(.30,.05,.22)*foot+float3(.06,.03,.12)*mid)*saturate(.65-n.y*.5);
    return up+glow;
}
// The night sky mirrored in a wet or glazed surface at `p` facing `n`, by Schlick's Fresnel from `f0`.
float3 RoofMirror(float3 p,float3 n,float f0)
{
    float3 v=normalize(p-_WorldSpaceCameraPos);float3 r=reflect(v,n);float fres=f0+(1.0-f0)*pow(1.0-saturate(-dot(v,n)),5.0);
    return RoofSkyBase(normalize(float3(r.x,abs(r.y)+.02,r.z)))*fres;
}
#endif
