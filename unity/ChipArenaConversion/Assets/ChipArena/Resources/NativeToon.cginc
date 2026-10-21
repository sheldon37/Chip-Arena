#ifndef NATIVE_TOON_INCLUDED
#define NATIVE_TOON_INCLUDED
// The anime map style (docs/MAP-STYLE-ANIME.md; Core/NativeMapStyle.cs holds the CPU copy of every formula here and the
// per-map values, MapLooks.json `toon`). One switch: _ArenaToon.x. NativeLighting writes the globals for a map whose look
// turns the style on (and the `--ca-map-style legacy` command line has not turned it off); every other map, a stage, the
// draft, the rooftop and an unset global leave _ArenaToon.x at 0, and every includer then draws exactly its legacy look:
// each toon path is behind that one uniform branch, and the toon helpers return their input unchanged while it is off.
//  _ArenaToon:       x on (0 legacy, 1 anime), y the ramp's softness (half width of each band edge in N.L), z rim strength,
//                    w ambient flatten (0 Unity's trilight SH at the normal, 1 one sky-to-ground blend for every face)
//  _ArenaToonBand:   x the shade edge (N.L x shadow), y the light edge, z the half-tone band's level (0..1), w the soft-edge
//                    radius share (the rounded-edge shading radius per metre of a block's thinnest half extent)
//  _ArenaToonShade:  rgb the shade side's tint (multiplies the ambient: cool), w the shade lift (key light kept in shade)
//  _ArenaToonLit:    rgb the lit side's tint (multiplies the key: warm), w how far point lights are stepped (0 smooth, 1 stairs)
//  _ArenaToonRim:    rgb the rim and edge light (linear), w the edge highlight (the key on a rounded edge, 0..1)
//  _ArenaToonPaint:  x the fine variation kept (0..1 of a look's per-brick, per-plank and grain noise), y the wall's value
//                    gradient (darker low, lighter high), z the painted wash (broad soft mottling), w the base shadow (soft AO)
// ---- TOON SHARED BEGIN (HLSL and GLSL: float literals only; Core NativeMapStyle mirrors it) ----
float4 _ArenaToon;float4 _ArenaToonBand;float4 _ArenaToonShade;float4 _ArenaToonLit;float4 _ArenaToonRim;float4 _ArenaToonPaint;
// Point lights in stairs of their light's square root (NativeMapStyle.PointSteps: 3 steps from 0 to 1, a soft edge of .18).
#define TOON_POINT_STEPS 3.0
#define TOON_STAIR 0.18
// The rounded-edge radius is at most this (m) and never more than .9 of the thinnest half extent.
#define TOON_RADIUS_MAX 0.3
// The edge light's gain on 1 - N.facet (the rounded normal's turn off the flat face).
#define TOON_EDGE_GAIN 3.5
// The sky's sheen on floors and roofs at grazing angles (the painted wet-street gradient): its share of the sky ambient.
#define TOON_SHEEN 0.5
bool ToonOn(){return _ArenaToon.x>0.5;}
float ToonStep(float x,float edge,float soft){return smoothstep(edge-soft,edge+soft,x);}
// The key's ramp: 0 in shade, the half-tone level between the two edges, 1 lit.
float ToonRamp(float x)
{
    float s=max(_ArenaToon.y,0.001);
    return _ArenaToonBand.z*ToonStep(x,_ArenaToonBand.x,s)+(1.0-_ArenaToonBand.z)*ToonStep(x,_ArenaToonBand.y,s);
}
// Soft stairs: x rounded to whole steps with a smooth riser TOON_STAIR wide either side of each half.
float ToonStairs(float x){float f=floor(x);return f+smoothstep(0.5-TOON_STAIR,0.5+TOON_STAIR,x-f);}
// Point-light sums (the practical slots, the flashes, the muzzle lights, the actor lights, the ward field): stepped on the
// square root of their luminance, so a lamp's pool reads as two or three flat rings, blended by _ArenaToonLit.w. Off: as is.
float3 ToonPoints(float3 c)
{
    if(_ArenaToon.x<0.5)return c;
    float l=dot(c,float3(0.2126,0.7152,0.0722));if(l<0.000001)return c;
    float q=ToonStairs(sqrt(l)*TOON_POINT_STEPS)/TOON_POINT_STEPS;
    return c*lerp(1.0,q*q/l,_ArenaToonLit.w);
}
// The ambient: Unity's SH at the normal flattened toward one sky-to-ground blend (anime backgrounds fill shade flat).
float3 ToonAmbient(float3 shN,float3 shUp,float3 shDown,float ny){return lerp(shN,lerp(shDown,shUp,0.55+0.3*ny),_ArenaToon.w);}
// The key and ambient through the ramp: shade is the ambient in the shade tint plus the lift, lit adds the key in the lit
// tint; `atten` is the key's shadow (1 lit), so a cast shadow takes the same flat shade as a face turned away.
float3 ToonDiffuse(float3 amb,float3 key,float ndl,float atten)
{
    float r=ToonRamp(saturate(ndl)*atten);
    float3 shade=(amb+key*_ArenaToonShade.w)*_ArenaToonShade.rgb;
    return lerp(shade,amb+key*_ArenaToonLit.rgb,r);
}
// The rounded box's normal at `obj` (object space) for half extents h and radius r: the direction out of the box shrunk by
// r, so a flat face turns over its last r toward the next one (a soft edge, without a vertex more). Where the drawn facet
// is turned more than 60 degrees from it (a chip, a stub's break, a waist) the facet wins.
float3 ToonRoundNormal(float3 obj,float3 h,float r,float3 facet)
{
    float3 core=max(h-float3(r,r,r),float3(0.0,0.0,0.0));float3 q=obj-clamp(obj,-core,core);float l=length(q);
    float3 n=l>0.00001?q/l:facet;
    return dot(n,facet)>0.5?n:facet;
}
// The soft-edge radius for half extents h and drawn chamfer c: a share of the thinnest half extent, at least the chamfer,
// at most TOON_RADIUS_MAX and .9 of that half extent (NativeMapStyle.Radius).
float ToonRadius(float3 h,float c)
{
    float lo=min(h.x,min(h.y,h.z));
    return min(0.9*lo,clamp(_ArenaToonBand.w*lo,c,TOON_RADIUS_MAX));
}
// The edge light: on a rounded edge (`edge` 0 on the flat, 1 on a full turn) the key through the ramp (_ArenaToonRim.w) and
// the rim colour where the edge turns from the eye (a thin back light on silhouettes and corners).
float3 ToonEdge(float3 n,float3 v,float3 keyDir,float3 key,float atten,float edge)
{
    float lit=ToonRamp(saturate(dot(n,keyDir))*atten);
    float back=pow(1.0-saturate(dot(n,v)),2.0);
    return edge*(key*(lit*_ArenaToonRim.w)+_ArenaToonRim.rgb*(back*_ArenaToon.z));
}
// The sky's sheen on an upward face seen at a grazing angle (n, v toward the eye), in the sky ambient's colour.
float3 ToonSheen(float3 n,float3 v,float3 sky){return sky*(smoothstep(0.7,0.95,n.y)*pow(1.0-saturate(dot(n,v)),4.0)*TOON_SHEEN);}
// ---- TOON SHARED END ----
#endif
