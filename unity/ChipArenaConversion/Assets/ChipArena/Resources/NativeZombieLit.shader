Shader "ChipArena/ZombieLit" {
 Properties { _SceneTint("Scene tint",Color)=(1,1,1,1) _BodyTint("Body tint",Color)=(1,1,1,1) _AccentTint("Accent tint",Color)=(1,1,1,1) _GlowTint("Emission tint (linear)",Vector)=(1,1,1,1) _Ink("Siren ink",Float)=0 _InkRim("Ink rim",Float)=.3 _InkFlow("Ink flow",Float)=.025 _InkFill("Ink fill",Float)=0 _InkColor("Ink colour",Color)=(.039,.031,.055,1) _Cull("Cull",Float)=2 _MinionGlow("Siren minion glow",Float)=0 }
 // The horde's zombies in voxels (docs/ZOMBIE-DESIGN.md, docs/ZOMBIE-GORE.md; Core/NativeZombieKit.cs builds the data this reads).
 // ChipArena/SourceLit's minion material, its hooks kept (the glow cells' flicker for the zone kinds that still use it, the
 // dithered fade a sinking corpse uses, the entrance stage's lights and tone, Ward Zero's actor lights, the struck flash), with
 // the zombie shading on top. Since 2026-10-19 (owner: "more standard zombies") there are no animated Siren lines: the rising
 // ink flow is gone and the rim is a faint neutral edge, not her lavender.
 //  - per vertex (uv0, the colour's alpha; NativeHordeModels.K.Pack): the surface class (skin, wet flesh, armour shell, cloth,
 //    bone, matte, wire) and the baked occlusion; the missing-piece variant and a dangling part's sway weight; the proportion
 //    region. Class 0 with no occlusion draws exactly as SourceLit (Lambert), as the zone kinds' bodies do.
 //  - per instance, the body tint's colour (NativeHordeModels.Variation; white = the model as built): the add's skin tone, its
 //    pattern seed (the mottling's offset and the head, torso and limb proportions) and its missing pieces.
 //  - lighting (LightingZombie, every pixel light): a little wrap per class (kept low so the voxels' faces stay crisp), a
 //    Blinn sheen per class (wet flesh tight and bright, armour satin, skin faint, cloth none); the occlusion (the voxels' own
 //    corners and the parts over them) darkens the ambient, the lights and the sheen; the ink rim keeps the silhouette in the
 //    dark, less in the creases.
 //  - the skin mottles in stepped cells in the joint's frame (rot and bruise patches, a grain of two shades), offset by the
 //    add's seed, so a crowd never repeats; the suit, the armour and (lightly) the skin carry stepped patches of dirt and dried
 //    blood (ZombieStain) by the same seed.
 // Nothing allocates; one material per kind as before, so the instanced batches are unchanged.
 SubShader { Tags { "RenderType"="Opaque" } Cull [_Cull]
 CGPROGRAM
 #pragma surface surfZombie Zombie fullforwardshadows addshadow vertex:vert finalcolor:ZombieTone
 #pragma target 3.5
 #pragma multi_compile_instancing
 float _Ink,_InkRim,_InkFlow,_InkFill,_MinionGlow,_MinionClock;float4 _InkColor;
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_SceneTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_GlowTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float4 color:COLOR;float4 emission;float3 worldPos;float3 viewDir;float3 worldNormal;float3 localPos;float4 zdata;};
 // Entrance-stage point lights, as SourceLit's (NativeSourceModels.Stage sets _StageLit for an entrance's own draws).
 float _StageLit;float4 _StageLightPos[8];float4 _StageLightColor[8];
 float3 StageLight(float3 albedo,float3 world,float3 normal)
 {
  float3 sum=0;
  [unroll] for(int k=0;k<8;k++)
  {
   float3 d=_StageLightPos[k].xyz-world;float len=length(d);float q=_StageLightPos[k].w>0?len/_StageLightPos[k].w:0;q*=q;q*=q;float cut=saturate(1-q);
   sum+=_StageLightColor[k].rgb*(saturate(dot(normal,d/max(len,1e-4)))*cut*cut/max(pow(len,_StageLightColor[k].w),.01));
  }
  return albedo*sum*.31830988618;
 }
 bool Unlit(float3 color,float3 emission){return dot(abs(emission-color),float3(1,1,1))<1e-4&&dot(emission,float3(1,1,1))>0;}
 // Ward Zero's actor lights (NativeWardLight.cginc), as SourceLit's.
 float _ArenaActorLit;float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];
 #include "NativeWardLight.cginc"
 // The guns' muzzle lights, as SourceLit's (NativeMuzzleLight.cginc): every map, the body's albedo, never its glow.
 #include "NativeMuzzleLight.cginc"
 float3 StageRrt(float3 v){float3 a=v*(v+0.0245786)-0.000090537;float3 b=v*(0.983729*v+0.4329510)+0.238081;return a/b;}
 float3 StageAces(float3 c)
 {
  c*=.92/0.6;
  c=float3(dot(c,float3(0.59719,0.35458,0.04823)),dot(c,float3(0.07600,0.90834,0.01566)),dot(c,float3(0.02840,0.13383,0.83777)));
  c=StageRrt(c);
  c=float3(dot(c,float3(1.60475,-0.53108,-0.07367)),dot(c,float3(-0.10208,1.10813,-0.00605)),dot(c,float3(-0.00327,-0.07276,1.07602)));
  return saturate(c);
 }
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
 void HitFlash(Input i,inout fixed4 color)
 {
  float code=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint).w;if(code<1.001)return;
  float head=code>2.5?1:0;float k=saturate(code-(head>.5?3:1));
  float rim=1-saturate(abs(dot(normalize(i.viewDir),normalize(i.worldNormal))));
  color.rgb=lerp(color.rgb,HitFlashColor(k,head),HitFlashBlend(k,head,rim));
 }
 void ZombieTone(Input i,SurfaceOutput o,inout fixed4 color)
 {
  #ifndef UNITY_PASS_FORWARDADD
  if(_StageLit>.5&&!Unlit(i.color.rgb,i.emission.rgb))color.rgb=StageAces(color.rgb);
  HitFlash(i,color);
  #endif
 }
 // The Siren minion glow cells, exactly SourceLit's MinionFlicker (Tests/MinionFxChecks holds the two the same).
 float MinionFlicker(float code,float w,float charred)
 {
  float d12=step(15.5,w);w-=16*d12;float d0=step(7.5,w),phase=w-8*d0;
  float cls=clamp(floor(code-2.999),0,2),cell=saturate((code-3-cls)/.98),f=lerp(cls<.5?.35:cls<1.5?.85:.9,.6,charred),drop=cls>.5?d12:d0;phase+=cls>.5?.37:0;
  float h=frac(sin(cell*289.37+phase*7.13)*43758.5453),wave=.5+.5*sin(_MinionClock*(4+9*h)+h*40+phase*3);
  return lerp(1,wave,f)*(1-drop*f*.8)*lerp(1,cls<.5?1.882353:cls<1.5?2.133333:2,charred);
 }
 // ---- ZOMBIE SHARED BEGIN (HLSL and GLSL: float literals only; the offline renders run this block) ----
 // A packed value (NativeHordeModels.K.Pack): the whole part, and the 0..1 value over .02 + .96 x value.
 float ZombieValue(float x){return clamp((x-floor(x)-0.02)/0.96,0.0,1.0);}
 // The proportions a pattern seed gives (NativeHordeModels.Proportions): head scale, torso width, limb girth.
 float3 ZombieProportions(float seed)
 {
  float on=step(0.0005,seed);
  return float3(1.0+on*0.05*(2.0*frac(seed*5.31+0.17)-1.0),1.0+on*0.05*(2.0*frac(seed*9.73+0.61)-1.0),1.0+on*0.07*(2.0*frac(seed*13.37+0.29)-1.0));
 }
 // The vertex: the add's proportions by region, a missing piece folded away, a dangling part's sway.
 float3 ZombieVert(float3 p,float2 d,float region,float3 body,float t)
 {
  float seed=body.g-1.0;float3 pr=ZombieProportions(seed);
  if(region>0.62&&region<0.88)p=p*pr.x;
  else if(region>0.37&&region<0.63)p.x=p.x*pr.y;
  else if(region>0.12&&region<0.38){p.x=p.x*pr.z;p.z=p.z*pr.z;}
  float variant=floor(d.y);float mask=floor((body.b-1.0)*16.0+0.5);
  if(variant>0.5&&variant<4.5){float bit=exp2(variant-1.0);if(frac(floor(mask/bit)*0.5)>0.25)p=float3(0.0,0.0,0.0);}
  float sway=ZombieValue(d.y);
  if(sway>0.001){float ph=seed*6.2831853+p.y*7.0;p.x=p.x+sway*0.018*sin(t*2.3+ph);p.z=p.z+sway*0.024*sin(t*1.7+ph*1.3+1.1);}
  return p;
 }
 float ZombieHash(float3 p){p=frac(p*0.3183099+float3(0.71,0.113,0.419));p=p*17.0;return frac(p.x*p.y*p.z*(p.x+p.y+p.z));}
 // The skin's mottling in the joint's frame, stepped like voxel art: 8 cm cells of rot (greener, darker) or bruise (violet),
 // a 3.5 cm grain of two shades, both offset by the add's seed; then the add's tone.
 float3 ZombieSkin(float3 albedo,float3 local,float3 body)
 {
  float seed=body.g-1.0;float tone=body.r-1.0;
  float3 q=floor(local*12.5+float3(seed*31.0,seed*7.0,seed*17.0));
  float big=ZombieHash(q);float fine=ZombieHash(floor(local*28.0)+q.zxy);
  float3 c=lerp(albedo,albedo*float3(0.72,0.78,0.6),step(0.8,big)*0.8);
  c=lerp(c,albedo*float3(0.84,0.66,0.86),step(0.88,1.0-big)*0.6);
  c=c*(0.96+0.08*step(0.5,fine));
  float3 hue=tone>0.0?float3(1.06,1.0,1.12):float3(0.94,1.04,0.84);
  return c*lerp(float3(1.0,1.0,1.0),hue,min(1.0,abs(tone)*1.6));
 }
 // The class's wrap, sheen (0..1; its power follows) and ink rim share.
 float3 ZombieClass(float cls)
 {
  if(cls<0.5)return float3(0.0,0.0,1.0);
  if(cls<1.5)return float3(0.18,0.08,1.0);
  if(cls<2.5)return float3(0.15,0.6,0.9);
  if(cls<3.5)return float3(0.06,0.34,0.85);
  if(cls<4.5)return float3(0.12,0.0,0.6);
  if(cls<5.5)return float3(0.12,0.2,0.9);
  if(cls<6.5)return float3(0.08,0.04,0.7);
  return float3(0.1,0.4,0.8);
 }
 // One light on a zombie surface: wrapped diffuse with the skin's warm flush in the wrap, and the class's sheen.
 float3 ZombieLight(float3 albedo,float3 n,float3 l,float3 v,float3 lightColor,float atten,float wrap,float spec)
 {
  float ndl=dot(n,l);float lam=max(ndl,0.0);float w=clamp((ndl+wrap)/(1.0+wrap),0.0,1.0);
  float3 diff=albedo*w+albedo*float3(0.3,0.06,0.05)*max(w-lam,0.0)*step(0.35,wrap);
  float power=lerp(14.0,90.0,spec);float3 h=normalize(l+v);float s=pow(max(dot(n,h),0.0),power)*spec*step(0.0,ndl)*(0.5+power*0.012);
  return (diff+float3(s,s,s))*lightColor*atten;
 }
 // Dirt and dried blood in stepped 7 cm patches (a 3.3 cm grain inside them) over the suit (class 4), the armour (3) and, lightly,
 // the skin (1), offset by the add's seed: grime ground in brown, old blood near black-red.
 float3 ZombieStain(float3 albedo,float3 local,float3 body,float cls)
 {
  float seed=body.g-1.0;float k=cls>3.5?1.0:cls>2.5?0.7:0.4;
  float3 q=floor(local*14.0+float3(seed*13.0,seed*29.0,seed*5.0));
  float h=ZombieHash(q+float3(3.1,7.7,1.3));float g=ZombieHash(floor(local*30.0)+q.yzx);
  float3 c=lerp(albedo,albedo*float3(0.6,0.52,0.42),step(0.64,h)*0.7*k);
  c=lerp(c,float3(0.15,0.02,0.022)*(0.8+0.4*g),step(0.9,h)*step(0.3,g)*0.85*k);
  return c;
 }
 // ---- ZOMBIE SHARED END ----
 half4 LightingZombie(SurfaceOutput s,half3 lightDir,half3 viewDir,half atten)
 {
  half4 c;c.rgb=ZombieLight(s.Albedo,s.Normal,lightDir,viewDir,_LightColor0.rgb,atten,s.Gloss,s.Specular);c.a=s.Alpha;return c;
 }
 void vert(inout appdata_full v,out Input o)
 {
  UNITY_INITIALIZE_OUTPUT(Input,o);float4 body=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);
  v.vertex.xyz=ZombieVert(v.vertex.xyz,v.texcoord.xy,v.color.a,body.rgb,_Time.y);
  o.emission=v.texcoord1;o.localPos=v.vertex.xyz;o.zdata=float4(v.texcoord.xy,v.color.a,0);
 }
 void surfZombie(Input i,inout SurfaceOutput o)
 {
  float4 shell=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);if(shell.a<.999)clip(shell.a-frac(sin(dot(floor(i.worldPos*120),float3(12.9898,78.233,37.719)))*43758.5453));
  float4 accent=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint);float3 tint=i.emission.w>2.5?1:i.emission.w>1.5?accent.rgb:i.emission.w>.5?shell.rgb:1;
  float4 sceneTint=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint);tint*=sceneTint.rgb;float4 glowTint=UNITY_ACCESS_INSTANCED_PROP(Props,_GlowTint);
  float cls=floor(i.zdata.x);float ao=1-ZombieValue(i.zdata.x);
  float3 albedo=i.color.rgb*tint;if(cls>.5&&cls<1.5)albedo=ZombieSkin(albedo,i.localPos,shell.rgb);
  if(cls>.5&&cls<4.5&&(cls<1.5||cls>2.5))albedo=ZombieStain(albedo,i.localPos,shell.rgb,cls);
  o.Emission=i.emission.rgb*tint*glowTint.rgb;if(_MinionGlow>.5&&i.emission.w>2.5)o.Emission*=MinionFlicker(i.emission.w,glowTint.w,1-sceneTint.a);
  float3 k=ZombieClass(cls);
  if(_InkFill>.5){albedo=_InkColor.rgb;o.Emission=float3(0,0,0);k=float3(0,0,1);ao=1;}
  o.Albedo=albedo*ao;o.Gloss=k.x;o.Specular=k.y*ao;
  // A faint neutral rim keeps the silhouette in the dark (less in the creases); no rising flow (_InkFlow is unused here).
  float rim=pow(1-saturate(abs(dot(normalize(i.viewDir),normalize(i.worldNormal)))),3);
  o.Emission+=_Ink*float3(.5,.53,.58)*(rim*_InkRim*k.z*lerp(.35,1,ao));
  if(_StageLit>.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=StageLight(o.Albedo,i.worldPos,normalize(i.worldNormal));
  if(_InkFill<.5&&_ArenaActorLit>.5&&_StageLit<.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=o.Albedo*ActorLights(i.worldPos,normalize(i.worldNormal));
  if(_InkFill<.5&&_StageLit<.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=o.Albedo*MuzzleLights(i.worldPos,normalize(i.worldNormal),.35);
  o.Alpha=shell.a;
 }
 ENDCG
 } Fallback "Diffuse"
}
