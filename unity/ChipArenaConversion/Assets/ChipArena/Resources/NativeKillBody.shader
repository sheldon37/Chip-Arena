// C18 Frostbreak recolour (ragdoll-vfx.ts: material.color.set(0xbdefff), emissive.set(0x244655) on every part mesh). The
// source-model meshes bake material colour times voxel shade into the vertex colour; the shade is recovered against the
// export's shell / accent colours (emission.w tags 1 / 2) and the albedo replaced by _SceneTint. _GlowTint replaces the
// emission. Same instanced properties and dithered fade (_BodyTint.a) as NativeSourceLit, so it shares its batches.
Shader "ChipArena/KillBody" {
 Properties { _SceneTint("Albedo (linear)",Color)=(1,1,1,1) _MainTex("Texture",2D)="white"{} _BodyTint("Opacity in alpha",Color)=(1,1,1,1) _AccentTint("Unused",Color)=(1,1,1,1) _GlowTint("Emission (linear)",Vector)=(0,0,0,1) _ShellSource("Export shell colour",Vector)=(.784,.799,.831,1) _AccentSource("Export accent colour",Vector)=(.162,.753,.631,1) _Cull("Cull",Float)=2 }
 SubShader { Tags { "RenderType"="Opaque" } Cull [_Cull]
 CGPROGRAM
 #pragma surface surf Lambert fullforwardshadows addshadow vertex:vert
 #pragma target 3.5
 #pragma multi_compile_instancing
 sampler2D _MainTex;float4 _ShellSource,_AccentSource;
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_SceneTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_GlowTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float2 uv_MainTex;float4 color:COLOR;float4 emission;float3 worldPos;};
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.emission=v.texcoord1;}
 void surf(Input i,inout SurfaceOutput o)
 {
  float4 shell=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint);if(shell.a<.999)clip(shell.a-frac(sin(dot(floor(i.worldPos*120),float3(12.9898,78.233,37.719)))*43758.5453));
  float3 base=i.emission.w>1.5?_AccentSource.rgb:i.emission.w>.5?_ShellSource.rgb:i.color.rgb;float shade=dot(i.color.rgb,float3(1,1,1))/max(1e-4,dot(base,float3(1,1,1)));
  float4 tex=tex2D(_MainTex,i.uv_MainTex);o.Albedo=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint).rgb*shade*tex.rgb;o.Emission=UNITY_ACCESS_INSTANCED_PROP(Props,_GlowTint).rgb;o.Alpha=i.color.a*tex.a*shell.a;
 }
 ENDCG
 } Fallback "Diffuse"
}
