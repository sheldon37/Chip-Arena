#ifndef NATIVE_VISUAL_BODY_FX_INCLUDED
#define NATIVE_VISUAL_BODY_FX_INCLUDED
// The visual body's hit flash and match-start spawn-in (docs/BOZO-INTEGRATION.md step 4), shared by its toon
// (NativeVisualBodyToon.shader: the body and its outline) and its hologram (NativeVisualBodyGhost.shader). The arena writes
// them per renderer each frame a pilot draws (Runtime/NativeVisualBodyPresentation.cs VisualBodyLook, from the same flash
// code and spawn-in frame the voxel parts read). Unset, every value leaves the body as it is.
//  _HitFlash:    the struck pilot's flash code (Core NativeHitFlash.Code: 1 none, 1 + k a hit at strength k, 3 + k a headshot)
//  _SpawnFront:  x the solid front's world height (the build front less its lag; far above while no spawn-in builds), y the
//                band, z the heat span, w the hologram slice's lead past the solid front (Core NativeSpawnIn's constants)
//  _SpawnTint:   rgb the hardlight tint, w its share (the frame's tint)
//  _SpawnGlitch: x the completion flash, y the RGB split's offset (m), z the split copies' opacity, w the scan clock
//  _SpawnHolo:   rgb the hologram's colour (linear), w the slice's opacity (0 on the low graphics tier)
float _HitFlash;float4 _SpawnFront;float4 _SpawnTint;float4 _SpawnGlitch;float4 _SpawnHolo;
// ---- HIT FLASH SHARED BEGIN (HLSL and GLSL: float literals only) ----
float3 HitFlashColor(float k,float head)
{
 float3 red=head>0.5?float3(1.0,0.16,0.1):float3(1.0,0.42,0.32);
 float w=smoothstep(0.0,1.0,(k-(head>0.5?0.45:0.2))/(head>0.5?0.55:0.8));
 return lerp(red,float3(1.0,1.0,1.0),w)*(head>0.5?2.6:1.6);
}
float HitFlashBlend(float k,float head,float rim)
{
 return min(1.0,k*(head>0.5?0.95:0.82)*(0.82+0.18*rim));
}
// ---- HIT FLASH SHARED END ----
// SourceLit's flash: the lit colour pulled toward white through the damage red, a little more on the silhouette (rim 1).
float3 VisualHitFlash(float3 c,float rim)
{
 if(_HitFlash<1.001)return c;
 float head=_HitFlash>2.5?1.0:0.0;float k=saturate(_HitFlash-(head>0.5?3.0:1.0));
 return lerp(c,HitFlashColor(k,head),HitFlashBlend(k,head,rim));
}
// Core NativeSpawnIn.Solid, Heat and Ghost at a pixel's world height `y` instead of a part's.
float SpawnSolid(float y){float t=saturate((_SpawnFront.x-y)/_SpawnFront.y);return t*t*(3.0-2.0*t);}
float SpawnHeat(float y){return 1.0-saturate((_SpawnFront.x-_SpawnFront.y-y)/_SpawnFront.z);}
float SpawnSlice(float y){return saturate((_SpawnFront.x+_SpawnFront.w-y)/_SpawnFront.y)*(1.0-SpawnSolid(y));}
// The voxel parts' dithered reveal (SourceLit): a world cell hash against the solid share. The hash is floored above 0:
// at world-sized arguments it resolves to about 1/256, and a cell hashing to 0 would otherwise draw where nothing has formed
// (SourceLit skips an unformed part whole, so it never meets that cell).
void SpawnClip(float3 world)
{
 float s=SpawnSolid(world.y);
 if(s<0.999)clip(s-max(frac(sin(dot(floor(world*120.0),float3(12.9898,78.233,37.719)))*43758.5453),0.001));
}
// A directed entrance's per-bone reveal (Runtime/NativeVisualBodyPresentation.cs VisualRevealLook: the voxel parts'
// partOpacity, the Sheldon Intro's glitch drop-outs): each game bone a world segment, _RevealA[k].xyz to _RevealB[k].xyz,
// and its solid share (_RevealA[k].w). A pixel takes its nearest segment's share and is dithered by it in SpawnClip's world
// cells, so a bone of the model drops out or flickers in as the voxel part does. With _RevealOn 0 nothing is read or cut.
float _RevealOn;float4 _RevealA[21];float4 _RevealB[21];
void RevealClip(float3 world)
{
 if(_RevealOn<0.5)return;
 float best=1e9;float share=1.0;
 for(int k=0;k<21;k++)
 {
  float3 a=_RevealA[k].xyz,ab=_RevealB[k].xyz-a;float t=saturate(dot(world-a,ab)/max(dot(ab,ab),1e-6));float3 d=world-(a+ab*t);
  float e=dot(d,d);if(e<=best){best=e;share=_RevealA[k].w;}
 }
 if(share<0.999)clip(share-max(frac(sin(dot(floor(world*120.0),float3(12.9898,78.233,37.719)))*43758.5453),0.001));
}
// The pilot's skin tone (Core NativeLook's skin entry; Runtime/NativeVisualBodyPresentation.cs VisualSkinToneOne, written on the
// body's skin parts only): _SkinTone rgb the picked tone and w on, _SkinRef rgb the part's base skin (its palette's brightest
// skin colour), both in the colour space the shader works in. A pixel that reads as skin (Core NativeColorWheel.SkinShare,
// tested on its sRGB colour) is scaled by the tone over the base per channel, so the shade, the blush and the lips keep their
// relation to the skin; any other pixel keeps its colour. With _SkinTone.w 0 (unset, or no tone picked) nothing changes.
float4 _SkinTone;float4 _SkinRef;
float SkinShare(float3 c)
{
 #ifndef UNITY_COLORSPACE_GAMMA
 c=sqrt(max(c,float3(0.0,0.0,0.0)));
 #endif
 float hi=max(c.r,max(c.g,c.b)),lo=min(c.r,min(c.g,c.b));float sat=(hi-lo)/max(hi,0.0001);
 float warm=saturate((c.r-c.g)*60.0+0.5)*saturate((c.g-c.b)*60.0+0.5);
 return warm*smoothstep(0.05,0.12,sat)*(1.0-smoothstep(0.80,0.92,sat))*smoothstep(0.03,0.08,hi);
}
float3 SkinToned(float3 c)
{
 if(_SkinTone.w<0.5)return c;
 return lerp(c,saturate(c*(_SkinTone.rgb/max(_SkinRef.rgb,float3(0.0003,0.0003,0.0003)))),SkinShare(c));
}
// A zombie on the model (Runtime/NativeZombieModelPresentation.cs; Core/NativeZombieLooks), per renderer, after the skin tone:
//  _ZombieTint:  a clothing or hair piece's colour, rgb (the shader's colour space), w one over the piece's own main brightness:
//                each pixel takes the colour at its own brightness against the piece's, so seams, folds and prints keep their
//                light and shade; brighter parts (pockets, trims, a hoodie's lining) rise more gently and fade toward a dirty
//                grey, so a light second colour never reads as bare skin or a glowing band; 0 off
//  _ZombieWear:  x grime (darker, duller, browner in soft patches), y torn patches (the skin, _ZombieSkin, through ragged holes
//                with a frayed dark rim), z rot (bruised blotches on skin), w the add's seed; all read on the part's own
//                texture coordinates, so the patches stay on the cloth as the body moves. The pack packs each piece into a
//                corner of its texture (a leg is about a tenth of it across), so the noise is fine: holes a few centimetres wide.
//  _ZombieEye:   the blank eyes: rgb their colour, w on (the eye mesh drawn in it, iris and pupil gone); past 1 they glow by
//                w - 1 (the surface pass's emission: brighter through a wind-up)
//  _ZombieSkin:  rgb the skin under torn clothes (raw and bloodied toward a hole's middle); w dried blood soaked into the cloth
//  On skin the rot also opens gashes: long strokes of raw red flesh in a dark crusted edge, the bone's ivory at the deepest.
// Unset (every pilot), nothing changes.
float4 _ZombieTint;float4 _ZombieWear;float4 _ZombieEye;float4 _ZombieSkin;
float4 _ZombieChar;float4 _ZombieCharGlow;float _ZombieFoil;
float ZombieHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float ZombieNoise(float2 p)
{
 float2 i=floor(p),f=frac(p);f=f*f*(3.0-2.0*f);
 return lerp(lerp(ZombieHash(i),ZombieHash(i+float2(1.0,0.0)),f.x),lerp(ZombieHash(i+float2(0.0,1.0)),ZombieHash(i+float2(1.0,1.0)),f.x),f.y);
}
// A zombie's reflective tape (a hi-vis jacket's rings: Core NativeZombieLooks.Variant.Tape, Runtime ZombieTape), per renderer:
//  _ZombieBands the rings set (0 on every other renderer); _ZombieBandAt[k] xyz a ring's centre, w how far from its axis it
//  reaches (the torso's or the limb's girth, so a ring round the chest stops short of the arms); _ZombieBandIn[k] xyz its axis,
//  w the tape's half-width. All in the meshes' rest space (their fourth texture coordinates), so a ring rides the cloth as it
//  folds and stretches. The tape is silver over the piece's colour, under its grime and tears, keeping the cloth's shading.
float _ZombieBands;float4 _ZombieBandAt[6];float4 _ZombieBandIn[6];
float ZombieBand(float3 rest)
{
 if(_ZombieBands<0.5)return 0.0;
 float b=0.0;
 for(int k=0;k<6;k++)
 {
  if(k>=_ZombieBands)break;
  float3 v=rest-_ZombieBandAt[k].xyz;float t=dot(v,_ZombieBandIn[k].xyz);float3 w=v-_ZombieBandIn[k].xyz*t;
  if(dot(w,w)>_ZombieBandAt[k].w*_ZombieBandAt[k].w)continue;
  b=max(b,1.0-smoothstep(_ZombieBandIn[k].w-0.004,_ZombieBandIn[k].w,abs(t)));
 }
 return b;
}
// The tape's colour at a cloth shade `k` (the tint's: its folds and seams kept).
float3 ZombieTapeColour(float k){return float3(0.74,0.77,0.78)*lerp(0.72,1.08,saturate(k));}
// A zombie's stripes (a vintage swimsuit's: Core NativeZombieLooks.Variant.Stripes), per renderer: _ZombieStripe x the period
// up the rest space (m), y the stripe's share of it, z..w the rest heights they cover; _ZombieStripeTint their colour. Unset, none.
float4 _ZombieStripe;float4 _ZombieStripeTint;
float ZombieStripe(float3 rest)
{
 if(_ZombieStripe.x<=0.0||rest.y<_ZombieStripe.z||rest.y>_ZombieStripe.w)return 0.0;
 float f=frac(rest.y/_ZombieStripe.x),e=0.003/_ZombieStripe.x;
 return smoothstep(0.0,e,f)*(1.0-smoothstep(_ZombieStripe.y-e,_ZombieStripe.y,f));
}
// A starved body's (the Hound's: Core NativeZombieLooks.Variant.Ribs), per renderer, a shade over the zombie look: _ZombieRibs x the
// age creases on a head (the pack's age mask, _ZombieAge, in the head's UVs: low red where a crease lies), y the ribs and the
// spine on a body over the rest space (z..w the ribs' rest heights): six grooves down each flank falling toward the front with a
// lit ridge over each, the spine's knuckles down the back. Unset (zeros), 1.
sampler2D _ZombieAge;float4 _ZombieRibs;
float ZombieLean(float3 rest,float2 uv)
{
 float k=1.0;
 if(_ZombieRibs.x>0.0){float a=saturate((1.0-tex2D(_ZombieAge,uv).r)*1.8);k*=1.0-_ZombieRibs.x*a*0.42;}
 if(_ZombieRibs.y>0.0)
 {
  float ax=abs(rest.x),flank=smoothstep(0.04,0.08,ax)*(1.0-smoothstep(0.16,0.2,ax));
  float span=smoothstep(_ZombieRibs.z-0.03,_ZombieRibs.z,rest.y)*(1.0-smoothstep(_ZombieRibs.w-0.02,_ZombieRibs.w+0.02,rest.y));
  float f=frac((rest.y+0.3*rest.z)/0.032),g=(f-0.55)/0.11,h=(f-0.25)/0.13;
  k*=1.0-_ZombieRibs.y*flank*span*(0.32*exp(-g*g)-0.1*exp(-h*h));
  float spine=(1.0-smoothstep(0.012,0.028,ax))*smoothstep(-0.03,-0.08,rest.z)*smoothstep(0.92,0.98,rest.y)*(1.0-smoothstep(1.42,1.48,rest.y));
  float v=frac(rest.y/0.027),n=(v-0.5)/0.16;
  k*=1.0-_ZombieRibs.y*spine*(0.3*exp(-n*n)-0.08);
 }
 return k;
}
float3 ZombieLook(float3 c,float2 uv,float band,float stripe)
{
 if(_ZombieEye.w>0.5)return _ZombieEye.rgb;
 if(_ZombieTint.w<=0.0&&_ZombieWear.x+_ZombieWear.y+_ZombieWear.z<=0.0)return c;
 float luma=dot(c,float3(0.299,0.587,0.114));
 if(_ZombieTint.w>0.0)
 {
  float x=luma*_ZombieTint.w,k=x<1.0?x:sqrt(x);
  c=_ZombieTint.rgb*min(k,1.6);c=lerp(c,dot(c,float3(0.299,0.587,0.114))*float3(1.0,0.97,0.9),saturate(k-1.0)*0.6);
  if(band>0.0)c=lerp(c,ZombieTapeColour(k),band);
  if(stripe>0.0)c=lerp(c,_ZombieStripeTint.rgb*lerp(0.72,1.05,saturate(k)),stripe);
 }
 float2 s=float2(frac(_ZombieWear.w*0.0137)*91.0,frac(_ZombieWear.w*0.0071)*57.0);
 if(_ZombieWear.x>0.0)
 {
  float g=ZombieNoise(uv*18.0+s)*0.6+ZombieNoise(uv*52.0+s.yx)*0.4;
  float k=_ZombieWear.x*smoothstep(0.25,0.85,g);
  c=lerp(c,dot(c,float3(0.299,0.587,0.114))*float3(1.0,0.96,0.86),k*0.45)*(1.0-0.38*k);
 }
 if(_ZombieWear.y>0.0)
 {
  float t=ZombieNoise(uv*30.0+s*1.7)*0.7+ZombieNoise(uv*90.0+s.yx)*0.3,edge=1.0-0.45*_ZombieWear.y;
  float hole=smoothstep(edge,edge+0.012,t),rim=saturate(smoothstep(edge-0.035,edge,t)-hole);
  // (A burned kind's cloth, _ZombieChar.x under 0: its holes burned black, no raw flesh.)
  float3 under=_ZombieChar.x<0.0?float3(0.01,0.008,0.007):lerp(_ZombieSkin.rgb*0.5,float3(0.3,0.04,0.04),smoothstep(edge+0.006,edge+0.035,t));
  c=lerp(c,under,hole)*(1.0-0.7*rim);
 }
 if(_ZombieSkin.w>0.0)
 {
  float b=ZombieNoise(uv*11.0+s.yx*0.7)*0.65+ZombieNoise(uv*37.0+s)*0.35;float k=_ZombieSkin.w*smoothstep(0.6,0.78,b);
  c=lerp(c,float3(0.17,0.025,0.02)+c*0.15,k*0.85);
 }
 if(_ZombieWear.z>0.0)
 {
  float r=ZombieNoise(uv*14.0+s.yx*1.3)*0.7+ZombieNoise(uv*42.0+s)*0.3;
  float b=_ZombieWear.z*smoothstep(0.52,0.82,r);
  c=lerp(c,c*float3(0.70,0.60,0.68),b);
  float2 g2=float2(uv.x*55.0+uv.y*18.0,uv.y*14.0-uv.x*6.0)+s*2.3;
  float gash=ZombieNoise(g2)*0.75+ZombieNoise(uv*70.0+s.yx)*0.25,e=1.0-0.36*_ZombieWear.z;
  float open=smoothstep(e,e+0.015,gash),crust=saturate(smoothstep(e-0.05,e,gash)-open);
  float3 flesh=lerp(float3(0.42,0.06,0.06),float3(0.22,0.02,0.03),smoothstep(e+0.02,e+0.07,gash));
  flesh=lerp(flesh,float3(0.86,0.8,0.66),smoothstep(e+0.085,e+0.1,gash));
  c=lerp(c*(1.0-0.55*crust)+float3(0.08,0.0,0.0)*crust,flesh,open);
 }
 return c;
}
float3 ZombieLook(float3 c,float2 uv,float band){return ZombieLook(c,uv,band,0.0);}
float3 ZombieLook(float3 c,float2 uv){return ZombieLook(c,uv,0.0,0.0);}
// The tape's retroreflection: it shines back toward the eye (the surface pass's emission), brightest facing the view.
float3 ZombieTapeShine(float band,float3 n,float3 view){return band>0.0?float3(0.5,0.53,0.55)*band*pow(saturate(dot(n,view)),3.0):float3(0.0,0.0,0.0);}
// A body burned to char (Core NativeZombieLooks.Variant.Char: the Ironworks' Cinderling, the Slagback's bared skin), per renderer: _ZombieChar x how far (the skin
// toward black), y the cracks' glow, z ash in patches, w a seed; _ZombieCharGlow its colour. Over the rest space, so the cracks ride the skin. Unset, nothing.
float ZombieNoise3(float3 p)
{
 float3 i=floor(p),f=frac(p);f=f*f*(3.0-2.0*f);
 float h000=frac(sin(dot(i,float3(127.1,311.7,74.7)))*43758.5453),h100=frac(sin(dot(i+float3(1,0,0),float3(127.1,311.7,74.7)))*43758.5453),h010=frac(sin(dot(i+float3(0,1,0),float3(127.1,311.7,74.7)))*43758.5453),h110=frac(sin(dot(i+float3(1,1,0),float3(127.1,311.7,74.7)))*43758.5453);
 float h001=frac(sin(dot(i+float3(0,0,1),float3(127.1,311.7,74.7)))*43758.5453),h101=frac(sin(dot(i+float3(1,0,1),float3(127.1,311.7,74.7)))*43758.5453),h011=frac(sin(dot(i+float3(0,1,1),float3(127.1,311.7,74.7)))*43758.5453),h111=frac(sin(dot(i+float3(1,1,1),float3(127.1,311.7,74.7)))*43758.5453);
 return lerp(lerp(lerp(h000,h100,f.x),lerp(h010,h110,f.x),f.y),lerp(lerp(h001,h101,f.x),lerp(h011,h111,f.x),f.y),f.z);
}
void ZombieChar(float3 rest,inout float3 albedo,inout float3 emission)
{
 if(_ZombieChar.x<=0.0)return;
 float3 p=rest+_ZombieChar.w*float3(0.37,0.71,0.13);
 float blot=ZombieNoise3(p*9.0)*0.6+ZombieNoise3(p*27.0)*0.4;
 float3 black=float3(0.011,0.009,0.008)*(0.6+0.8*ZombieNoise3(p*61.0));
 albedo=lerp(albedo,black,saturate(_ZombieChar.x*1.2));
 albedo=lerp(albedo,float3(0.13,0.12,0.11),_ZombieChar.z*smoothstep(0.6,0.78,blot));
 float warp=ZombieNoise3(p*5.3)*0.35;
 float v1=abs(ZombieNoise3(p*11.0+warp*5.0)-0.5)*2.0,v2=abs(ZombieNoise3(p*29.0+warp*9.0+11.3)-0.5)*2.0;
 float where=smoothstep(0.34,0.56,ZombieNoise3(p*3.7+7.7));
 float hot=saturate((1.0-smoothstep(0.0,0.032,v1))*where+(1.0-smoothstep(0.0,0.05,v2))*where*smoothstep(0.5,0.7,ZombieNoise3(p*7.1+3.1))*0.6);
 float rim=saturate((1.0-smoothstep(0.02,0.12,v1))*where);
 albedo*=1.0-0.8*rim;
 emission+=_ZombieCharGlow.rgb*hot*_ZombieChar.y+float3(0.5,0.12,0.03)*rim*(1.0-hot)*0.35*_ZombieChar.y*0.4;
}
// An aluminised suit (Variant.Foil: the Stoker's): the cloth takes the light as bright edges: a hard rim, and the key's highlight broken along the folds' normals.
void ZombieFoil(float3 n,float3 view,inout float3 albedo,inout float3 emission)
{
 if(_ZombieFoil<=0.0)return;
 float ndv=saturate(dot(n,view));float3 h=normalize(_WorldSpaceLightPos0.xyz+view);float ndh=saturate(dot(n,h));
 emission+=_ZombieFoil*albedo*(pow(1.0-ndv,3.5)*1.6+pow(ndh,26.0)*1.8+pow(ndh,160.0)*3.0);
}
// A zombie's decay openings (Core NativeZombieDecay; Runtime/NativeZombieModelPresentation.cs), per renderer: _DecayAt xyz an
// opening's centre on the surface and w its radius, _DecayIn xyz inward and w how deep it reaches, both in the meshes' own rest
// space (root space at rest, written to their fourth texture coordinates), so an opening rides the skin as it deforms and cuts
// every layer over it (skin and clothes alike); its rim is ragged (NativeZombieDecay.Rag). A flesh pit with the skeleton in it
// closes it. _Decays the number set; 0 for every pilot.
float _Decays;float4 _DecayAt[4];float4 _DecayIn[4];
void DecayClip(float3 rest)
{
 if(_Decays<0.5)return;
 for(int k=0;k<4;k++)
 {
  if(k>=_Decays)break;
  float3 v=rest-_DecayAt[k].xyz;float3 n=_DecayIn[k].xyz;float t=dot(v,n);if(t<-0.03||t>_DecayIn[k].w)continue;
  float3 w=v-n*t;float3 a=normalize(cross(n,abs(n.y)<0.9?float3(0.0,1.0,0.0):float3(1.0,0.0,0.0)));float3 b=cross(n,a);
  float ang=atan2(dot(w,b),dot(w,a)),seed=_DecayAt[k].x*13.7+_DecayAt[k].z*7.3;
  float rad=_DecayAt[k].w*(0.82+0.18*sin(ang*5.0+seed*97.0)+0.08*sin(ang*11.0+3.0));
  if(dot(w,w)<rad*rad)clip(-1.0);
 }
}
// The spawn-in's hardlight tint (SpawnTintOf): the tint colour by its share, hottest where the body has just formed, then
// the completion flash toward white.
float3 SpawnTinted(float3 c,float y)
{
 float k=saturate(_SpawnTint.w*(0.35+0.65*SpawnHeat(y)))*0.8;
 float3 t=lerp(float3(1.0,1.0,1.0),_SpawnTint.rgb,k);
 return c*lerp(t,float3(2.2,2.3,2.5),_SpawnGlitch.x*0.7);
}
// The dead pilot's dissolve (Runtime/NativeVisualBodyPresentation.cs VisualCorpseShown and VisualSceneShown, as the corpse
// fades): _Dissolve 0 (whole) to 1 (gone) across the noise the bake carries (_VisualDissolveNoise, global: the owner's
// dissolve pack), read on the part's own texture coordinates, so the cells stay on the cloth as the ragdoll moves. The
// front's band glows _DissolveEdge (linear, the pilot's accent). At 0 nothing is read or cut. Returns the band's share.
sampler2D _VisualDissolveNoise;float _Dissolve;float4 _DissolveEdge;
float DissolveFront(float2 uv)
{
 if(_Dissolve<=0.0)return 0.0;
 float n=tex2D(_VisualDissolveNoise,uv*3.0).r,cut=_Dissolve*1.08;
 clip(n-cut);
 return 1.0-saturate((n-cut)/0.07);
}
#ifdef VISUAL_BODY_CUT
// A model corpse's cut (Runtime/NativeVisualCutPresentation.cs; owner 2026-10-28: "slice right on the trail of a hit"), no mesh
// copied: _CutTex holds, per vertex of the body's skinned meshes (texel _CutBase + the vertex's index, in rows _CutWidth long),
// two keep values; _CutPick picks one, (1,0) or (0,1). A split corpse's halves each keep their side of the blade's plane (r for
// half 0, g for half 1: the vertex's signed distance from it in the death pose, positive on that half's side, or 1 and -1 where
// its body stays whole on half 0); a body severed at its joints keeps each vertex all of whose weight is on one piece (r: 1, else
// -1); the death broadcast's bust its bust bones'. The value is interpolated, so a pixel under 0 lies past the cut. With
// _CutPick (0,0) (unset) nothing is read or cut. (The surface shader's analysis pass reads no texture objects.)
float _CutBase;float _CutWidth;float4 _CutPick;
// Open wounds (Runtime/NativeVisualWoundPresentation.cs): up to four holes through the body's skin and clothes, each a capsule
// round the wound's line in the surface: _WoundAt xyz its centre on the skin and w its radius, _WoundIn xyz inward and w how deep
// it reaches, _WoundAlong xyz the line and w its half length (0 for a round hole); _Wounds the number set. Its pit is a mesh of
// its own. Unset (0) nothing is cut.
float _Wounds;float4 _WoundAt[4];float4 _WoundIn[4];float4 _WoundAlong[4];
void WoundClip(float3 world)
{
 if(_Wounds<0.5)return;
 for(int k=0;k<4;k++)
 {
  if(k>=_Wounds)break;
  float3 v=world-_WoundAt[k].xyz;float t=dot(v,_WoundIn[k].xyz);if(t<-0.012||t>_WoundIn[k].w)continue;
  float a=dot(v,_WoundAlong[k].xyz);float3 w=v-_WoundIn[k].xyz*t-_WoundAlong[k].xyz*a;float e=max(abs(a)-_WoundAlong[k].w,0.0);
  if(e*e+dot(w,w)<_WoundAt[k].w*_WoundAt[k].w)clip(-1.0);
 }
}
#ifdef SHADER_TARGET_SURFACE_ANALYSIS
float CutKeep(uint vid){return 1.0;}
#else
Texture2D<float2> _CutTex;
float CutKeep(uint vid)
{
 if(_CutPick.x+_CutPick.y<0.5)return 1.0;
 uint i=(uint)_CutBase+vid;uint w=max((uint)_CutWidth,1u);
 return dot(_CutTex.Load(int3(i%w,i/w,0)).rg,_CutPick.xy);
}
#endif
#endif
#endif
