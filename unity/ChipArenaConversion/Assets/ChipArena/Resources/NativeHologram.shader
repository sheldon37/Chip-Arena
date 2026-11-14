// The sky death broadcast's pilot hologram (death-announcements.ts: every portrait mesh gets one MeshBasicMaterial 0x62dcff,
// transparent, depthWrite false, DoubleSide, no fog, not tone-mapped). Flat colour; the opacity (.55 x fade x reveal x
// flicker) arrives in _BodyTint.a through the source-model batches. The scanlines are their own line set (NativeDeathFx).
Shader "ChipArena/NativeHologram" {
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct Input {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct Output {float4 pos:SV_POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
 Output vert(Input v){Output o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.pos=UnityObjectToClipPos(v.vertex);return o;}
 float4 frag(Output i):SV_Target
 {
  UNITY_SETUP_INSTANCE_ID(i);float alpha=UNITY_ACCESS_INSTANCED_PROP(Props,_BodyTint).w;
  // 0x62dcff: linear (.1221, .7157, 1); sRGB (.3843, .8627, 1) when the project renders in gamma space.
  float3 c=float3(.1221,.7157,1);
  #ifdef UNITY_COLORSPACE_GAMMA
  c=float3(.3843,.8627,1);
  #endif
  return float4(c,alpha);
 }
 ENDCG }
 } Fallback Off
}
