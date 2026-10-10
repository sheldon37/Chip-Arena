#ifndef NATIVE_ARMOR_LIGHT_INCLUDED
#define NATIVE_ARMOR_LIGHT_INCLUDED
// Pilot armor lights (ChipArena/SourceLit): each glowing trim reads as a light instead of a flat sticker.
//  - emitter parts (accent trims and armor strips): a bright core easing to each voxel face's border (strip faces across
//    their short axis), a grazing-view gain and a hot core on the strips;
//  - receiver parts (shell, joints, gloves, boots, and the gun held by a cuffed hand): wrapped Lambert spill from up to
//    eight bone-local emitter boxes with an area-light near field, a glossy sheen lobe and a contact bleed hugging each
//    trim's border (the diffuse share x .6 on an entrance stage; the sternum, the chest's core, at 1.15 of its level;
//    corpse draws skip the boxes across a severable joint, so a severed limb carries no light from a trim it left);
//  - a slow breathing pulse that climbs the body, and hue-preserving highlight handling (arena shoulder, StageTone ACES).
// Material constants come from NativeSourceModels.ArmorLights (Core/NativeArmorLight.cs) once at load; globals from
// NativeArmorLights.Apply. _ArmorLight 0 (every non-pilot material, ink, ghosts) leaves the shader bit-identical.

float _ArmorLight;          // 0 off, 1 receiver, 2 emitter, 3 hand-held weapon (a receiver only where the draw opts in)
float _ArmorLevel;          // emitter: its baked emission level (1.4 trim, 2.4 strip)
float _ArmorCount;          // boxes in use (0..8): a receiver's emitters, an emitter's own voxel boxes
float _ArmorPhase;          // rest height (m) of the part above the feet: the pulse climbs the body
float4 _ArmorEmit[8];       // box centre xyz (bone-local, Unity frame), w light level; receiver: its emitters' boxes, emitter: its own
                            // voxel boxes (w over _ArmorLevel lifts the face: the sternum's 1.15, Core NativeArmorLight.SternumBoost)
float4 _ArmorSize[8];       // box half extents xyz; receiver: w = near-field radius sigma (m), negative across a severable joint
                            // (skipped on corpse and regeneration draws, glow.w < 1: NativeArmorLight.CorpseGlow)
float4 _ArmorExportEmber;   // global: ember('#70e1d0') linear, the exported accent colour that _AccentTint scales
float _ArmorQuality;        // global: 0 off (current look), .5 reduced (profile + diffuse spill), 1 full
float _ArmorPulse;          // global: breathing amplitude (.06; 0 with reduced effects)

#define ARMOR_TWO_PI 6.2831853
#define ARMOR_EDGE .62      // emitter border level
#define ARMOR_CORE 1.28     // emitter core level
#define ARMOR_TUBE 2.2      // face aspect above which the profile runs across the short axis only
#define ARMOR_GRAZE .55     // emission gain at grazing view: 1 + graze (1 - |N.V|)^2
#define ARMOR_HOT .06       // strip core: share toward its own luminance (hot core, hue kept)
#define ARMOR_SPILL .8      // diffuse spill gain (1 = Lambert of an area emitter of the trim's radiance)
#define ARMOR_WRAP .55      // wrapped N.L: the trim's side faces reach coplanar armour
#define ARMOR_REACH .32     // spill window radius (m)
#define ARMOR_SHEEN .10     // glossy lobe strength (dielectric F0 ~ .05, x2 for the voxel's flat faces)
#define ARMOR_GLOSS 24      // Blinn exponent of the sheen
#define ARMOR_BLEED .22     // contact bleed strength
#define ARMOR_BLEED_R .012  // contact bleed radius (m)
#define ARMOR_SHELL .80     // flat shell self-glow (GLOW.body) scale: the spill replaces part of it near the trims (Core Shell)
#define ARMOR_STAGE_SPILL .6 // diffuse spill share on an entrance stage (_StageLit): its ACES and stage lights already light it (Core StageSpill)
#define ARMOR_KNEE .60      // arena highlight shoulder: identity below, -> 1 above (max channel, hue preserved)
#define ARMOR_HUE_KEEP .70  // StageTone: share of the hue-preserving ACES on emitter voxels
#define ARMOR_HZ .27        // breathing frequency
#define ARMOR_CLIMB 2.4     // breathing phase climb per metre of rest height

// Emitter face coordinates: the part's own voxel boxes are its material's _ArmorEmit/_ArmorSize (bone-local); the
// fragment's box is the one whose surface it lies on. xy: the two in-face coordinates (-1..1), zw: their half extents
// (0 off every box: a carved face); light: that box's light level.
float4 ArmorFace(float3 p,float3 n,out float light)
{
 int count=(int)_ArmorCount,best=0;float bestGap=1e9;light=_ArmorLevel;
 for(int k=0;k<count;k++){float3 d=abs(p-_ArmorEmit[k].xyz)-_ArmorSize[k].xyz;float gap=abs(max(max(d.x,d.y),d.z));if(gap<bestGap){bestGap=gap;best=k;}}
 if(count<1||bestGap>.004)return float4(0,0,0,0);
 light=_ArmorEmit[best].w;
 float3 h=_ArmorSize[best].xyz,q=(p-_ArmorEmit[best].xyz)/max(h,1e-4),a=abs(n);
 if(a.x>=a.y&&a.x>=a.z)return float4(q.y,q.z,h.y,h.z);
 if(a.y>=a.z)return float4(q.z,q.x,h.z,h.x);
 return float4(q.x,q.y,h.x,h.y);
}
// Hue-preserving shoulder on the brightest channel.
float3 ArmorRolloff(float3 c,float knee)
{
 float m=max(max(c.r,c.g),c.b);
 if(m<=knee)return c;
 return c*((knee+(1-knee)*(1-exp(-(m-knee)/(1-knee))))/m);
}
// Slow pulse: per pilot (the accent's hash) and climbing the body (the part's rest height). No flicker: <= +-6 %, .27 Hz.
float ArmorBreathe(float3 accent)
{
 float seed=frac(dot(accent,float3(12.9898,78.233,37.719)))*ARMOR_TWO_PI;
 return 1+_ArmorPulse*sin(ARMOR_TWO_PI*ARMOR_HZ*_Time.y+seed+ARMOR_CLIMB*_ArmorPhase);
}
// Spill at bone-local point p, normal n, view v from the material's emitter boxes: x diffuse, y sheen, z contact bleed.
// severed: a corpse or regeneration draw, which skips the boxes across a severable joint.
float3 ArmorSpill(float3 p,float3 n,float3 v,float full,bool severed)
{
 float3 s=float3(0,0,0);
 int count=(int)min(_ArmorCount,full>.5?8:4);
 for(int k=0;k<count;k++)
 {
  float3 c=_ArmorEmit[k].xyz,h=_ArmorSize[k].xyz;float level=_ArmorEmit[k].w,sigma=_ArmorSize[k].w;
  if(sigma<0){if(severed)continue;sigma=-sigma;}
  float3 L=clamp(p,c-h,c+h)-p;float d2=dot(L,L),inv=rsqrt(d2+1e-8),cosl=dot(L,n)*inv;
  float win=saturate(1-d2*(1.0/(ARMOR_REACH*ARMOR_REACH)));
  float nearField=level*win*win*sigma*sigma/(d2+sigma*sigma);
  s.x+=nearField*saturate((cosl+ARMOR_WRAP)/(1+ARMOR_WRAP));
  if(full>.5)
  {
   float3 hv=normalize(L*inv+v);
   s.y+=nearField*saturate(cosl)*pow(saturate(dot(hv,n)),ARMOR_GLOSS)*((ARMOR_GLOSS+8)/(8*3.14159265));
   s.z+=cosl>-.25?level*win*exp(-sqrt(d2)*(1.0/ARMOR_BLEED_R)):0;
  }
 }
 return s;
}
// Surface stage, after the shader has written o.Albedo and o.Emission. tag: the vertex's emission.w (1 shell, 2 accent).
// ember: the pilot's glow colour (export ember x accent tint x scene tint x glow tint), held: the draw's glow.w, stage:
// _StageLit. worldPos, worldNormal, viewDir in world space (surf never writes o.Normal, so worldNormal is the
// interpolated world normal).
void ArmorLight(inout SurfaceOutput o,float tag,float3 worldPos,float3 worldNormal,float3 viewDir,float3 accent,float3 ember,float held,float stage)
{
 // A weapon material lights only in draws that hold it in a cuffed hand on the plain hand matrix (glow.w 2:
 // ArenaPresentation.SourceCharacter); intro, death and loose draws of the same model stay as they are.
 if(_ArmorLight<.5||_ArmorQuality<=0||(_ArmorLight>2.5&&held<1.5))return;
 float full=_ArmorQuality>.75?1:0,breathe=ArmorBreathe(accent);
 float3 p=mul(unity_WorldToObject,float4(worldPos,1)).xyz;
 float3 n=normalize(mul(normalize(worldNormal),(float3x3)unity_ObjectToWorld));
 if(_ArmorLight>1.5&&_ArmorLight<2.5)
 {
  if(tag<1.5)return; // carved flesh in a wounded trim keeps its own colour
  float light;float4 face=ArmorFace(p,n,light);float ha=face.z,hb=face.w,gain=1,core=0;
  if(ha>0&&hb>0)
  {
   float r2=max(ha,hb)/min(ha,hb)>ARMOR_TUBE?(ha<hb?face.x*face.x:face.y*face.y):(face.x*face.x+face.y*face.y)*.5;
   float pr=1-saturate(r2);core=pr*pr*(3-2*pr);gain=lerp(ARMOR_EDGE,ARMOR_CORE,core)*light/max(_ArmorLevel,1e-4);
  }
  float nv=abs(dot(normalize(worldNormal),normalize(viewDir)));
  o.Emission*=gain*(1+ARMOR_GRAZE*(1-nv)*(1-nv))*breathe;
  if(_ArmorLevel>2)o.Emission=lerp(o.Emission,dot(o.Emission,float3(.2126,.7152,.0722)).xxx,ARMOR_HOT*core*core);
  return;
 }
 float3 v=normalize(mul((float3x3)unity_WorldToObject,viewDir));
 float3 s=ArmorSpill(p,n,v,full,held<1);
 if(tag>.5&&tag<1.5)o.Emission*=ARMOR_SHELL;
 o.Emission+=breathe*ember*(o.Albedo*(s.x*ARMOR_SPILL*(stage>.5?ARMOR_STAGE_SPILL:1))+s.y*ARMOR_SHEEN+s.z*ARMOR_BLEED*(.35+.65*o.Albedo));
}
// StageTone's ACES (three r180 ACESFilmicToneMapping at exposure .92, as the shader's StageAces).
float3 ArmorAces(float3 c)
{
 c*=.92/0.6;
 c=float3(dot(c,float3(0.59719,0.35458,0.04823)),dot(c,float3(0.07600,0.90834,0.01566)),dot(c,float3(0.02840,0.13383,0.83777)));
 c=(c*(c+0.0245786)-0.000090537)/(c*(0.983729*c+0.4329510)+0.238081);
 c=float3(dot(c,float3(1.60475,-0.53108,-0.07367)),dot(c,float3(-0.10208,1.10813,-0.00605)),dot(c,float3(-0.00327,-0.07276,1.07602)));
 return saturate(c);
}
// Called first in StageTone (base pass): armor pixels get the arena's hue-preserving shoulder (the arena is not tone
// mapped), or in an entrance ACES with the emitter voxels' hue kept (ACES of the brightest channel on their own RGB ratio,
// ARMOR_HUE_KEEP of it). False for every other material, which StageTone then handles as before.
bool ArmorTone(inout fixed4 color,float tag,float stage)
{
 if(_ArmorLight<.5||_ArmorLight>2.5||_ArmorQuality<=0)return false; // held weapons keep their own tone
 float3 c=color.rgb;
 if(stage<.5){color.rgb=ArmorRolloff(c,ARMOR_KNEE);return true;}
 float3 a=ArmorAces(c);
 if(_ArmorLight>1.5&&_ArmorLight<2.5&&tag>1.5){float m=max(max(c.r,c.g),c.b);a=lerp(a,saturate(c*(ArmorAces(m.xxx).r/max(m,1e-4))),ARMOR_HUE_KEEP);}
 color.rgb=a;return true;
}
#endif
