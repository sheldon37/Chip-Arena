Shader "ChipArena/SourceLit" {
 Properties { _SceneTint("Scene tint",Color)=(1,1,1,1) _MainTex("Texture",2D)="white"{} _BodyTint("Body tint",Color)=(1,1,1,1) _AccentTint("Accent tint",Color)=(1,1,1,1) _GlowTint("Emission tint (linear)",Vector)=(1,1,1,1) _Ink("Siren ink",Float)=0 _InkRim("Ink rim",Float)=.3 _InkFlow("Ink flow",Float)=.025 _InkFill("Ink fill",Float)=0 _InkColor("Ink colour",Color)=(.039,.031,.055,1) _Cull("Cull",Float)=2 _MinionGlow("Siren minion glow",Float)=0 }
 SubShader { Tags { "RenderType"="Opaque" } Cull [_Cull]
 CGPROGRAM
 #pragma surface surfArmor Lambert fullforwardshadows addshadow vertex:vert finalcolor:StageTone
 #pragma target 3.5
 #pragma multi_compile_instancing
 sampler2D _MainTex;float _Ink,_InkRim,_InkFlow,_InkFill,_MinionGlow,_MinionClock;float4 _InkColor;
 #include "NativeArmorLight.cginc"
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_SceneTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_GlowTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float2 uv_MainTex;float4 color:COLOR;float4 emission;float3 worldPos;float3 viewDir;float3 worldNormal;};
 // Entrance-stage point lights (Core/NativeSirenIntroLight.cs NativeStageLighting; arrays set by NativeLighting.Intro, Unity
 // frame, xyz position and w cutoff distance, rgb linear colour x intensity and w decay). three's point light on Lambert:
 // albedo/PI x colour x max(0,N.L) x 1/max(d^decay,.01) x saturate(1-(d/distance)^4)^2, added as emission so fog covers it
 // as it covers the source. Only an entrance's own draws set _StageLit (NativeSourceModels.Stage); unused slots are black.
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
 // The bank stores a MeshBasic or ShaderMaterial part (unlit in the source) with its emission equal to its colour.
 bool Unlit(float3 color,float3 emission){return dot(abs(emission-color),float3(1,1,1))<1e-4&&dot(emission,float3(1,1,1))>0;}
 // Actor lighting (Ward Zero only: NativeLighting.WardField sets _ArenaActorLit while its live arena or killcam draws): the
 // arena's practical slots (NativeBlockLit's, with their beacon beams) and the ceiling light field light a lit part as they
 // light the walls round it (NativeWardLight.cginc ActorLights), added as emission like the entrance stage's lights. Off (0)
 // on every other map, on an entrance stage (_StageLit), for unlit parts and the Siren's ink fill.
 float _ArenaActorLit;float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];
 #include "NativeWardLight.cginc"
 // The guns' muzzle lights (NativeMuzzleLight.cginc): on every map, a lit part takes them as emission (wrapped like the
 // actor lights), never on an entrance stage, an unlit part or the ink; nothing while no muzzle light is live.
 #include "NativeMuzzleLight.cginc"
 // StageTone (finalcolor, before fog as three tone-maps before fog): renderer.ts's ACES filmic tone mapping at exposure .92
 // (three r180 ACESFilmicToneMapping) on an entrance's lit parts, as the source tone-maps every lit material; unlit parts
 // (glows, toneMapped false in the source) stay raw. Skipped in additive light passes, which only directional key lights add.
 float3 StageRrt(float3 v){float3 a=v*(v+0.0245786)-0.000090537;float3 b=v*(0.983729*v+0.4329510)+0.238081;return a/b;}
 float3 StageAces(float3 c)
 {
  c*=.92/0.6;
  c=float3(dot(c,float3(0.59719,0.35458,0.04823)),dot(c,float3(0.07600,0.90834,0.01566)),dot(c,float3(0.02840,0.13383,0.83777)));
  c=StageRrt(c);
  c=float3(dot(c,float3(1.60475,-0.53108,-0.07367)),dot(c,float3(-0.10208,1.10813,-0.00605)),dot(c,float3(-0.00327,-0.07276,1.07602)));
  return saturate(c);
 }
 void StageGrade(Input i,inout fixed4 color)
 {
  if(ArmorTone(color,i.emission.w,_StageLit))return;
  if(_StageLit>.5&&!Unlit(i.color.rgb,i.emission.rgb))color.rgb=StageAces(color.rgb);
 }
 // The hit flash (weapons effects milestone; Core/NativeHitFlash.cs): a struck pilot's body parts carry a code in the per-
 // instance accent tint's unused w (1: none, 1 + k: a hit at strength k, 3 + k: a headshot), set by the arena's Queue. After
 // the grade, so the flash stays HDR for the bloom: the lit colour is pulled toward white decaying through a red damage tint
 // (headshots brighter and redder), a little more on the silhouette. Every other draw writes w = 1 and is untouched.
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
 void StageTone(Input i,SurfaceOutput o,inout fixed4 color)
 {
  #ifndef UNITY_PASS_FORWARDADD
  StageGrade(i,color);
  HitFlash(i,color);
  #endif
 }
 // The Siren ink (InkMaterial): a pale fresnel rim pow(1-|N.V|,3) and slow rising streaks, added as emission. surf never
 // writes o.Normal, so viewDir and worldNormal are both world space (dotting viewDir with a fixed axis lit the whole body
 // by the camera's heading and left the silhouette without its rim). _InkRim and _InkFlow are set per part (the claws and
 // flame tongues burn brighter: NativeSourceModels.SplitSirenPieces).
 // Siren minion glow cells (materials.ts MinionMaterial; Core/NativeMinionFx.cs Flicker is the same expression): the bank's
 // emission.w is 3 + flicker class (0 shell .35, 1 accent plate .85, 2 glow strip .9) + .98 x the cell's hash. Each cell
 // waves at its own 4-13 Hz on the arena's presentation time (_MinionClock, set per frame) and, about one 1/7 s slot in
 // seven, the add's whole glow drops by .8 of its flicker share. _GlowTint.w carries the add's phase plus the drop bits
 // (Core NativeMinionFx.GlowW: + 8 while class 0 drops, + 16 while classes 1 and 2 do; horde-* materials only, their
 // _ArmorLight is 0 so nothing else reads it). The drop is hashed once per add and frame on the CPU in double precision,
 // so host, guests and the killcam dim the same slots as the crack's haze on any GPU; the per-cell wave hash is float, so
 // the cells' wave phases may differ between GPU vendors. A charred joint (charred = 1 - _SceneTint.a, the burn-away's
 // char) keeps flickering embers: materials.ts charred glow 3.2 over the class's live glow and flicker .6.
 float MinionFlicker(float code,float w,float charred)
 {
  float d12=step(15.5,w);w-=16*d12;float d0=step(7.5,w),phase=w-8*d0;
  float cls=clamp(floor(code-2.999),0,2),cell=saturate((code-3-cls)/.98),f=lerp(cls<.5?.35:cls<1.5?.85:.9,.6,charred),drop=cls>.5?d12:d0;phase+=cls>.5?.37:0;
  float h=frac(sin(cell*289.37+phase*7.13)*43758.5453),wave=.5+.5*sin(_MinionClock*(4+9*h)+h*40+phase*3);
  return lerp(1,wave,f)*(1-drop*f*.8)*lerp(1,cls<.5?1.882353:cls<1.5?2.133333:2,charred);
 }
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.emission=v.texcoord1;}
 void surf(Input i,inout SurfaceOutput o){float4 shell=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);if(shell.a<.999)clip(shell.a-frac(sin(dot(floor(i.worldPos*120),float3(12.9898,78.233,37.719)))*43758.5453));float4 accent=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint);float3 tint=i.emission.w>2.5?1:i.emission.w>1.5?accent.rgb:i.emission.w>.5?shell.rgb:1;float4 sceneTint=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint);tint*=sceneTint.rgb;float4 tex=tex2D(_MainTex,i.uv_MainTex);o.Albedo=i.color.rgb*tint*tex.rgb;float4 glowTint=UNITY_ACCESS_INSTANCED_PROP(Props,_GlowTint);o.Emission=i.emission.rgb*tint*glowTint.rgb;if(_MinionGlow>.5&&i.emission.w>2.5)o.Emission*=MinionFlicker(i.emission.w,glowTint.w,1-sceneTint.a);if(_InkFill>.5){o.Albedo=_InkColor.rgb;o.Emission=float3(0,0,0);}float rim=pow(1-saturate(abs(dot(normalize(i.viewDir),normalize(i.worldNormal)))),3);float rise=sin(i.worldPos.y*23-_Time.y*6.5+sin(i.worldPos.x*13+_Time.y*1.7)*2.2+i.worldPos.z*9);o.Emission+=_Ink*float3(.62,.56,.82)*(rim*_InkRim+smoothstep(.7,1,rise)*_InkFlow);if(_StageLit>.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=StageLight(o.Albedo,i.worldPos,normalize(i.worldNormal));o.Alpha=i.color.a*tex.a*shell.a;}
 // Pilot armor lights (NativeArmorLight.cginc): surf, then the trims' profile and their spill on the armour around them;
 // inked (_InkFill) and non-pilot materials (_ArmorLight 0) are untouched.
 void surfArmor(Input i,inout SurfaceOutput o)
 {
  surf(i,o);if(_InkFill>.5)return;
  if(_ArenaActorLit>.5&&_StageLit<.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=o.Albedo*ActorLights(i.worldPos,normalize(i.worldNormal));
  if(_StageLit<.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=o.Albedo*MuzzleLights(i.worldPos,normalize(i.worldNormal),.35);
  float3 accent=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint).rgb;float4 glow=UNITY_ACCESS_INSTANCED_PROP(Props,_GlowTint);
  ArmorLight(o,i.emission.w,i.worldPos,i.worldNormal,i.viewDir,accent,_ArmorExportEmber.rgb*accent*UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint).rgb*glow.rgb,glow.w,_StageLit);
 }
 ENDCG
 } Fallback "Diffuse"
}
