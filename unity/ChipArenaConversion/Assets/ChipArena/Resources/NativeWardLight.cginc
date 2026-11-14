#ifndef NATIVE_WARD_LIGHT_INCLUDED
#define NATIVE_WARD_LIGHT_INCLUDED
// Ward Zero's light block, shared by ChipArena/NativeBlockLit (blocks, the kit) and ChipArena/SourceLit (pilots, monsters,
// machines). The includer declares _ArenaLightPos[8] and _ArenaLightColor[8] first.
//  - WardField: the ceiling light field (Core NativeWardZeroField): every troffer's and high-bay's pool, baked once per map
//    into a 3D texture (RGBA = four lighting circuits), read at the surface moved out 1 m along its normal from its own
//    floor's slices, times each circuit's level this frame and the field's colour (NativeWardZeroPresentation through
//    NativeLighting.WardField). _WardFieldColor.w 0 (every other map, a stage, a portrait) is off.
//  - WardBeam: a practical slot's beam (Core NativeWardZeroLights.Beam, NativeKitLightPool's beacons): w 0 a plain point
//    light, 1..7.3 one beam at yaw w - 1, 8..14.3 two opposite beams at yaw w - 8 (rad from +z toward +x), so a rotating
//    beacon throws its light round the room instead of a glow.
//  - ActorLights: the slots and the field (its WF_ACTOR share) on a source model (wrapped, so a body under a light reads in
//    the round).
// Unset globals are 0: the field and the beams are off and both shaders draw exactly as before.
sampler3D _WardField;
float4 WardTap(float3 uvw){return tex3Dlod(_WardField,float4(uvw,0.0));}
// SHARED BEGIN (plain HLSL that the offline harness also runs as GLSL: float literals keep their decimal point)
float4 _WardFieldBox;float4 _WardFieldLevel;float4 _WardFieldColor;
// NativeWardZeroField: Split, Offset, FlatOffset, Side, Down and the slice heights (four on the ground floor, four on the upper floor).
#define WF_SPLIT 5.8
#define WF_OFFSET 0.5
#define WF_FLAT 0.3
#define WF_SIDE 0.55
#define WF_DOWN 0.2
// NativeWardZeroField.Actor: the share of the field a body takes.
#define WF_ACTOR 0.8
#define WF_Y0 0.25
#define WF_Y1 1.85
#define WF_Y3 5.05
#define WF_Y4 6.25
#define WF_Y5 8.35
#define WF_Y7 12.55
// A beam's light off its axis: (cos)^6 over a floor of 0.12 (the lamp's own glow all round).
#define WB_FLOOR 0.12
float WardBeam(float3 d,float w)
{
    if(w<0.5)return 1.0;
    float two=step(7.5,w);float yaw=w-1.0-7.0*two;
    float2 h=-d.xz;float l=max(length(h),0.0001);float c=dot(h/l,float2(sin(yaw),cos(yaw)));c=lerp(max(c,0.0),abs(c),two);
    float c2=c*c;return WB_FLOOR+(1.0-WB_FLOOR)*c2*c2*c2;
}
float3 WardField(float3 world,float3 n)
{
    if(_WardFieldColor.w<0.5)return float3(0.0,0.0,0.0);
    float3 p=world+n*(WF_OFFSET*(1.0-(1.0-WF_FLAT)*abs(n.y)));float2 uv=(p.xz-_WardFieldBox.xy)/_WardFieldBox.zw;
    if(uv.x<0.0||uv.x>1.0||uv.y<0.0||uv.y>1.0||world.y<-2.0||world.y>16.0)return float3(0.0,0.0,0.0);
    float s=world.y<WF_SPLIT?(clamp(p.y,WF_Y0,WF_Y3)-WF_Y0)/(WF_Y1-WF_Y0):4.0+(clamp(p.y,WF_Y4,WF_Y7)-WF_Y4)/(WF_Y5-WF_Y4);
    float4 f=max(WardTap(float3(uv.x,uv.y,(s+0.5)/8.0)),float4(0.0,0.0,0.0,0.0));
    float k=n.y>=0.0?WF_SIDE+(1.0-WF_SIDE)*n.y:WF_SIDE+(WF_DOWN-WF_SIDE)*(-n.y);
    return _WardFieldColor.rgb*(dot(f,_WardFieldLevel)*k);
}
float3 ActorLights(float3 world,float3 n)
{
    float3 sum=WardField(world,n)*WF_ACTOR;
    for(int k=0;k<8;k++)
    {
        float3 d=_ArenaLightPos[k].xyz-world;float len=max(length(d),0.0001);float q=len/max(_ArenaLightPos[k].w,0.0001);float a=saturate(1.0-q*q);
        sum+=_ArenaLightColor[k].rgb*(a*a*saturate((dot(n,d/len)+0.35)/1.35))*WardBeam(d,_ArenaLightColor[k].w);
    }
    return sum;
}
// SHARED END
#endif
