// C18 Soul Drift ghost (ragdoll-vfx.ts: every part copy's material cloned transparent, depthWrite false, colour 0x91dcec,
// opacity .72 - age * .12, emissive kept). voxel-kit.ts draws a see-through voxel part as its plain box, so the body is drawn
// as plain boxes (vertex colour white, vertex emission 1); a hat or weapon with round meshes is drawn from its model. The
// albedo is replaced by _SceneTint (flat: a plain box has shade 1), the emission is the vertex emission times _GlowTint and
// _BodyTint.a is the opacity. Same instanced properties as NativeSourceLit, so it shares the source-model batches.
Shader "ChipArena/KillGhost" {
 Properties { _SceneTint("Albedo (linear)",Color)=(1,1,1,1) _MainTex("Texture",2D)="white"{} _BodyTint("Opacity in alpha",Color)=(1,1,1,1) _AccentTint("Unused",Color)=(1,1,1,1) _GlowTint("Emission tint (linear)",Vector)=(1,1,1,1) _Cull("Cull",Float)=2 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" } Cull [_Cull] ZWrite Off
 CGPROGRAM
 #pragma surface surf Lambert alpha:fade vertex:vert
 #pragma target 3.5
 #pragma multi_compile_instancing
 sampler2D _MainTex;
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_AccentTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_SceneTint)
 UNITY_DEFINE_INSTANCED_PROP(float4,_GlowTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float2 uv_MainTex;float4 color:COLOR;float4 emission;};
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.emission=v.texcoord1;}
 void surf(Input i,inout SurfaceOutput o)
 {
  float4 tex=tex2D(_MainTex,i.uv_MainTex);o.Albedo=UNITY_ACCESS_INSTANCED_PROP(Props,_SceneTint).rgb*tex.rgb;
  o.Emission=i.emission.rgb*UNITY_ACCESS_INSTANCED_PROP(Props,_GlowTint).rgb;o.Alpha=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint).a*tex.a;
 }
 ENDCG
 } Fallback "Transparent/Diffuse"
}
