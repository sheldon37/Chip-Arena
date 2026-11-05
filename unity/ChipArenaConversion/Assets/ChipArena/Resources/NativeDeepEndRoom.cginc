// What THE DEEP END's water, glaze and sheets share (docs/EXPEDITION-BOSS-BACKROOMS.md section 7): the room seen in a reflection, drawn as a box (the ceiling with its round lights on their
// grid, the tile walls, the floor below) from the arena's own numbers, and the caustic pattern. Runtime NativeDeepEndBossWater sets the globals every frame the boss draws.
#ifndef NATIVE_DEEPEND_ROOM
#define NATIVE_DEEPEND_ROOM
float4 _DeepRoom;      // the room's centre x, z, its half width, the ceiling's height (from the room's floor level, world y of the deck is _DeepRoomY)
float _DeepRoomY;
float4 _DeepLight;     // the ceiling's lights 0..1, the room's soft light 0..1, how warm 0..1, the blackout's front 0..1
float _DeepTide;       // the water's level, world y
float _DeepClock;
sampler2D _DeepGlowTex;// the light in the water from the lamps under it (rgb), over the room's square
sampler2D _DeepField;  // the surface's height (r, from the deck), foam (g)
float4 _DeepRect;      // the field: its corner x, z, its side, a texel

float3 DeepRoomColour(float3 p,float3 r)
{
    // The reflected ray from p along r, to the box of the room.
    float2 rel=p.xz-_DeepRoom.xy;float half_=_DeepRoom.z;float ceilY=_DeepRoomY+_DeepRoom.w;
    float tc=r.y>0.001?(ceilY-p.y)/r.y:1000.0;
    float tx=abs(r.x)>0.001?((r.x>0.0?half_:-half_)-rel.x)/r.x:1000.0;
    float tz=abs(r.z)>0.001?((r.z>0.0?half_:-half_)-rel.y)/r.z:1000.0;
    float t=min(tc,min(tx,tz));float3 h=p+r*t;float soft=_DeepLight.y;
    float3 warm=lerp(float3(1.0,0.98,0.93),float3(1.0,0.84,0.62),_DeepLight.z);
    float3 c;
    if(t==tc)
    {
        // The ceiling: plaster, and the round lights on their 6 m grid.
        float2 g=(h.xz-_DeepRoom.xy+float2(half_,half_))/6.0-float2(0.5,0.5)+float2(1.0/6.0,1.0/6.0)*0.0;
        float2 cell=frac((h.xz-_DeepRoom.xy+float2(half_,half_)-float2(3.0,3.0))/6.0+float2(0.5,0.5))-float2(0.5,0.5);float d=length(cell)*6.0;
        float order=length(h.xz-_DeepRoom.xy)/28.0;float lvl=_DeepLight.x*(1.0-smoothstep(0.0,0.14,_DeepLight.w*1.25-order));
        c=float3(0.60,0.62,0.60)*(0.05+0.62*soft)+warm*lvl*(smoothstep(0.62,0.40,d)*5.5+0.6*exp(-d*0.8));
    }
    else
    {
        // The walls: pale tile, a band of cooler light toward the water, the grid just suggested.
        float along=t==tx?h.z:h.x;float tile=smoothstep(0.0,0.05,abs(frac(along/0.48)-0.5))*smoothstep(0.0,0.05,abs(frac(h.y/0.48)-0.5));
        float up=saturate((h.y-_DeepRoomY)/max(_DeepRoom.w,0.1));c=lerp(float3(0.30,0.52,0.54),float3(0.62,0.68,0.66),up)*(0.05+0.66*soft)*(0.9+0.1*tile);
    }
    if(r.y<-0.02)
    {
        // Looking down: the water or the floor, dark teal.
        c=lerp(c,float3(0.05,0.17,0.19)*(0.1+soft),saturate(-r.y*3.0));
    }
    return c;
}
// A moving caustic web (the well-known iterated warp): sharp bright lines, in 0..1 and over.
float DeepCaustic(float2 p,float t)
{
    float2 q=fmod(p*1.15,6.283185)-6.283185;float2 i=q;float c=1.0;const float inten=0.005;
    for(int n=0;n<4;n++)
    {
        float tt=t*(1.0-3.5/float(n+1));i=q+float2(cos(tt-i.x)+sin(tt+i.y),sin(tt-i.y)+cos(tt+i.x));
        c+=1.0/length(float2(q.x/(sin(i.x+tt)/inten),q.y/(cos(i.y+tt)/inten)));
    }
    c/=4.0;c=1.17-pow(max(c,0.0001),1.4);return pow(abs(c),7.0);
}
#endif
