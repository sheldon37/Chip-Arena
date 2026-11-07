// The entrances' point lights on the visual body (owner 2026-10-29: the intros are lit for the voxel pilot). The directed and
// baked entrances put their rim, fill, flash and set lights in the stage block (NativeLighting.Intro and Entrance, which set
// _VisualStageLit; EndIntro and the draft clear it); SourceLit's voxel pilot takes them there, and the model takes them here,
// as irradiance in SourceLit's units (the caller multiplies by the albedo), its top end eased (a light at arm's length would
// burn a flat patch into the toon ramp where SourceLit's stage tone curve rolls it off).
#ifndef NATIVE_VISUAL_STAGE_LIGHT
#define NATIVE_VISUAL_STAGE_LIGHT
float _VisualStageLit;float4 _StageLightPos[8];float4 _StageLightColor[8];
float3 VisualStagePoints(float3 world,float3 normal)
{
 float3 sum=0;
 [unroll] for(int k=0;k<8;k++)
 {
  float3 d=_StageLightPos[k].xyz-world;float len=length(d);float q=_StageLightPos[k].w>0?len/_StageLightPos[k].w:0;q*=q;q*=q;float cut=saturate(1-q);
  sum+=_StageLightColor[k].rgb*(saturate(dot(normal,d/max(len,1e-4)))*cut*cut/max(pow(len,_StageLightColor[k].w),.01));
 }
 sum*=.31830988618;return sum/(1+sum*.6);
}
#endif
