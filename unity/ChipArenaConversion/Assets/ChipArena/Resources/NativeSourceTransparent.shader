Shader "ChipArena/SourceTransparent" {
 Properties { _SceneTint("Scene tint",Color)=(1,1,1,1) _MainTex("Texture",2D)="white"{} _BodyTint("Body tint",Color)=(1,1,1,1) _AccentTint("Accent tint",Color)=(1,1,1,1) _GlowTint("Emission tint (linear)",Vector)=(1,1,1,1) _Ink("Siren ink",Float)=0 _Cull("Cull",Float)=0 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull [_Cull] ZWrite Off
 CGPROGRAM
 #pragma surface surf Lambert alpha:fade vertex:vert finalcolor:StageTone
 #pragma target 3.5
 #pragma multi_compile_instancing
 sampler2D _MainTex;
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_SceneTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_GlowTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float2 uv_MainTex;float4 color:COLOR;float4 emission;float3 worldPos;float3 worldNormal;};
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
 void StageTone(Input i,SurfaceOutput o,inout fixed4 color)
 {
  #ifndef UNITY_PASS_FORWARDADD
  if(_StageLit>.5&&!Unlit(i.color.rgb,i.emission.rgb))color.rgb=StageAces(color.rgb);
  #endif
 }
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.emission=v.texcoord1;}
 void surf(Input i,inout SurfaceOutput o){float4 shell=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);float4 accent=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint);float3 tint=i.emission.w>1.5?accent.rgb:i.emission.w>.5?shell.rgb:1;tint*=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint).rgb;float4 tex=tex2D(_MainTex,i.uv_MainTex);o.Albedo=i.color.rgb*tint*tex.rgb;o.Emission=i.emission.rgb*tint*UNITY_ACCESS_INSTANCED_PROP(Props,_GlowTint).rgb;if(_StageLit>.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=StageLight(o.Albedo,i.worldPos,normalize(i.worldNormal));o.Alpha=i.color.a*tex.a*shell.a;}
 ENDCG
 } Fallback "Transparent/Diffuse"
}
