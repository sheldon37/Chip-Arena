#ifndef NATIVE_CITY_LAMPS_INCLUDED
#define NATIVE_CITY_LAMPS_INCLUDED
// The city's lamps on its surfaces (Core NativeCityLamps; Runtime NativeCityLampLight writes every global; docs/expedition/
// MERIDIAN-DRESS.md "The light"). ChipArena/StageLit includes it after its light block. The eight practical slots reach only the lamps
// nearest the eye; this lights a surface from every lamp whose reach touches its 8 m cell (at most 16, the brightest first), exactly as a
// slot would: the fall-off (1 - (d/r)^2)^2, N.L (wrapped as the caller's), the slots' highlight; a flood's cone toward its aim.
//  _CityLampGrid:     xy the grid's corner (min x, min z), z its cell (m), w on (1) / off (0: every other map, a stage, an intro)
//  _CityLampGridSize: x the cells along x, y along z, z the cell texture's width (4 texels a cell), w the lamps (rows)
//  _CityLampCells:    each cell's lamps, four to a texel, -1 past the last (point sampled)
//  _CityLampData:     a row a lamp: (x, y, z, reach), (aim x, y, z, 0)
//  _CityLampLight:    a row a lamp: this frame's (r, g, b) times its level, w a flood's cone power (0 all round)
// A slot whose colour's w is -1 carries a city lamp to the pilots and zombies: StageLit leaves it to the grid while the grid is on.
// Unset globals are 0: off, and StageLit draws exactly as before.
sampler2D _CityLampCells;sampler2D _CityLampData;sampler2D _CityLampLight;
float4 _CityLampGrid;float4 _CityLampGridSize;
// NativeCityLamps.SpotFloor: a flood's light kept behind it.
#define CITY_SPOT_FLOOR 0.08
bool CityLampSlot(float w){return _CityLampGrid.w>0.5&&w<-0.5;}
float3 CityLamps(float3 world,float3 n,float3 v,float wrap,float sharp,inout float3 spec)
{
    float3 sum=float3(0.0,0.0,0.0);
    if(_CityLampGrid.w<0.5)return sum;
    float2 c=floor((world.xz-_CityLampGrid.xy)/_CityLampGrid.z);
    if(c.x<0.0||c.y<0.0||c.x>=_CityLampGridSize.x||c.y>=_CityLampGridSize.y)return sum;
    float row=(c.y+0.5)/_CityLampGridSize.y;
    [loop]for(int t=0;t<4;t++)
    {
        float4 ids=tex2Dlod(_CityLampCells,float4((c.x*4.0+t+0.5)/_CityLampGridSize.z,row,0.0,0.0));
        [unroll]for(int j=0;j<4;j++)
        {
            float id=ids[j];if(id<0.0)return sum;
            float u=(id+0.5)/_CityLampGridSize.w;
            float4 p=tex2Dlod(_CityLampData,float4(0.25,u,0.0,0.0));
            float3 d=p.xyz-world;float len=max(length(d),0.0001);float q=len/max(p.w,0.0001);
            if(q>=1.0)continue;
            float a=1.0-q*q;float3 l=d/len;float k=a*a;
            float4 lc=tex2Dlod(_CityLampLight,float4(0.5,u,0.0,0.0));
            if(lc.w>0.0){float3 aim=tex2Dlod(_CityLampData,float4(0.75,u,0.0,0.0)).xyz;k*=CITY_SPOT_FLOOR+(1.0-CITY_SPOT_FLOOR)*pow(saturate(-dot(l,aim)),lc.w);}
            float3 col=lc.rgb*k;float ndl=saturate((dot(n,l)+wrap)/(1.0+wrap));
            sum+=col*ndl;float3 h=normalize(l+v);spec+=col*(pow(saturate(dot(n,h)),sharp)*(sharp+8.0)*0.0398*saturate(dot(n,l)));
        }
    }
    return sum;
}
#endif
